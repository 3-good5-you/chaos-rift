using Godot;
using PoC.Engine;

namespace PoC.Game.UI;

/// Effect kit, third layer (2026-11-13, after batch 1: "low poly pieces look off", "tornado low quality", "fireball is
/// 2003 low poly", "thunder lacks impact", "stop reusing the defeat crack"). Cards now bend, stretch, flutter and burn
/// away as one smooth sheet of their own picture (CardSheet) and come apart in soft flakes of it; a staged funnel of
/// wind (Twister: forms from a dust swirl, travels, thins out); a volumetric explosion of boiling puffs; a lightning
/// hit with a real flash; a soft soot mark (never a crack). Plus light-feather wings and a soft heart.
public partial class DuelField
{
    // ---------------------------------------------------------------- small helpers

    static float R(double a, double b) => (float)GD.RandRange(a, b);

    /// Tween one shader parameter.
    void Anim(ShaderMaterial m, string p, float a, float b, double dur, double delay = 0, Tween.TransitionType tr = Tween.TransitionType.Sine, Tween.EaseType e = Tween.EaseType.InOut)
    {
        m.SetShaderParameter(p, a);
        var tw = CreateTween();
        if (delay > 0) tw.TweenInterval(delay);
        tw.TweenMethod(Callable.From<float>(k => m.SetShaderParameter(p, k)), a, b, dur).SetTrans(tr).SetEase(e);
    }

    /// The whole screen flashes a colour (added light), `a` at its peak.
    void ScreenFlash(Color c, float a, double dur)
    {
        var r = new ColorRect { Size = new Vector2(W, H), Color = new Color(c, a), MouseFilter = MouseFilterEnum.Ignore, Material = new CanvasItemMaterial { BlendMode = CanvasItemMaterial.BlendModeEnum.Add } };
        hud.AddChild(r);
        var tw = CreateTween();
        tw.TweenProperty(r, "color:a", 0f, dur).SetTrans(Tween.TransitionType.Expo).SetEase(Tween.EaseType.Out);
        tw.TweenCallback(Callable.From(r.QueueFree));
    }

    /// The screen darkens for a moment (`a` = how dark), e.g. the beat before a strike.
    void ScreenDim(float a, double fadeIn, double hold, double fadeOut)
    {
        var r = new ColorRect { Size = new Vector2(W, H), Color = new Color(0, 0, 0.02f, 0), MouseFilter = MouseFilterEnum.Ignore };
        hud.AddChild(r); hud.MoveChild(r, 0);
        var tw = CreateTween();
        tw.TweenProperty(r, "color:a", a, fadeIn);
        tw.TweenInterval(hold);
        tw.TweenProperty(r, "color:a", 0f, fadeOut);
        tw.TweenCallback(Callable.From(r.QueueFree));
    }

    /// The zone outlines and their labels are see-through quads drawn after the screen copy, so a warp can't bend them:
    /// effects that bend the whole table fade them out (`to` 0) and back (1).
    readonly List<(ShaderMaterial M, float Strength)> zoneLines = new();
    readonly List<Label3D> zoneLabels = new();
    void FieldLines(float to, double dur)
    {
        foreach (var (m, s) in zoneLines)
        {
            float from = (float)m.GetShaderParameter("strength");
            CreateTween().TweenMethod(Callable.From<float>(k => m.SetShaderParameter("strength", k)), from, s * to, dur);
        }
        foreach (var l in zoneLabels) CreateTween().TweenProperty(l, "modulate:a", 0.45f * to, dur);
    }

    /// A round glowing ember drifting along `vel` (with a little lift and drag) and dimming out.
    void Ember(Vector3 at, Color c, Vector3 vel, double life, float size = 0.04f, float lift = 0.6f)
    {
        var m = Additive(SoftDot, c, billboard: true);
        var q = new MeshInstance3D { Mesh = new QuadMesh { Size = new Vector2(size, size) }, MaterialOverride = m, Position = at, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off };
        world.AddChild(q);
        float ph = R(0, 6.28);
        var tw = q.CreateTween();
        tw.TweenMethod(Callable.From<float>(k =>
        {
            float s = k * (float)life, drag = 1 - Mathf.Exp(-s * 2.2f);
            q.Position = at + vel * drag / 2.2f + Vector3.Up * lift * s * s * 0.5f + new Vector3(Mathf.Sin(ph + s * 7) * 0.03f, 0, Mathf.Cos(ph + s * 5) * 0.03f);
            float f = k < 0.15f ? k / 0.15f : 1 - (k - 0.15f) / 0.85f;
            m.AlbedoColor = new Color(c.R * 2.2f, c.G * 2.2f, c.B * 2.2f, f);
        }), 0f, 1f, life);
        tw.TweenCallback(Callable.From(q.QueueFree));
    }

    // ---------------------------------------------------------------- the card as a sheet

    const string SheetCode = @"shader_type spatial;
render_mode cull_disabled;
uniform sampler2D tex : source_color, filter_linear_mipmap_anisotropic;
uniform sampler2D noise : repeat_enable, filter_linear_mipmap;
uniform float burn = 0.0;
uniform vec2 origin = vec2(0.5, 0.5); uniform float from_pt = 0.0;
uniform vec4 edge_col : source_color = vec4(1.0, 0.55, 0.15, 1.0);
uniform float edge_w = 0.05; uniform float edge_e = 5.0; uniform float char_amt = 0.85;
uniform float glow = 0.0; uniform vec4 glow_col : source_color = vec4(1.0);
uniform float darken = 0.0;
uniform float pull = 0.0; uniform vec3 pull_to = vec3(0.0); uniform float twist = 0.0; uniform float lag = 0.7;
uniform float curl = 0.0; uniform float crumple = 0.0; uniform float flutter = 0.0; uniform float sink = 0.0;
uniform float seed = 0.0; uniform vec2 card = vec2(0.6, 0.87); uniform float glitch = 0.0; uniform vec4 glitch_col : source_color = vec4(1.0, 0.25, 0.2, 1.0);
void vertex() {
  vec3 v = VERTEX;
  float n1 = texture(noise, UV * 1.3 + seed).r - 0.5, n2 = texture(noise, UV * 1.3 + seed + 0.37).r - 0.5, n3 = texture(noise, UV * 2.1 + seed + 0.71).r - 0.5;
  v += vec3(n1, abs(n2) * 1.4, n3) * crumple * 0.4;
  v.xz *= 1.0 - crumple * 0.8;
  v.y += sin(TIME * 13.0 + UV.x * 5.0 + UV.y * 7.0) * flutter * 0.05 * (0.4 + UV.y) + sin(TIME * 21.0 + UV.y * 11.0) * flutter * 0.015;
  float cu = curl * UV.y * UV.y; v.y += cu * 0.45; v.z -= cu * 0.18;
  v.y -= sink * (0.4 + 0.6 * smoothstep(0.0, 0.5, length(UV - 0.5)));
  if (pull > 0.0) {
    float span = length(card) + length(pull_to);
    float d = distance(v, pull_to) / span;
    float e = clamp(pull * (1.0 + lag) - d * lag * 1.6, 0.0, 1.0); e = e * e * (3.0 - 2.0 * e);
    vec3 rel = v - pull_to; float a = twist * e;
    rel = vec3(rel.x * cos(a) - rel.z * sin(a), rel.y, rel.x * sin(a) + rel.z * cos(a));
    v = pull_to + rel * (1.0 - e);
  }
  VERTEX = v;
}
void fragment() {
  float band = step(0.62, texture(noise, vec2(floor(UV.y * 26.0) * 0.071 + seed, floor(TIME * 18.0) * 0.137)).r) * glitch;
  vec4 c = texture(tex, UV + vec2(band * 0.05 * (texture(noise, vec2(TIME * 3.0, UV.y * 4.0)).r - 0.5), 0.0));
  float st = fract(sin(dot(floor(UV * vec2(70.0, 100.0)) + floor(TIME * 30.0), vec2(12.9898, 78.233))) * 43758.5453);
  c.rgb = mix(c.rgb, glitch_col.rgb * (0.4 + 0.8 * st), clamp(glitch * (0.18 + 0.5 * band), 0.0, 0.8));
  c.rgb *= 1.0 - glitch * 0.25 * step(0.5, fract(UV.y * 90.0));
  float n = texture(noise, UV * vec2(0.9, 1.3) + seed).r * 0.65 + texture(noise, UV * 3.7 + seed * 2.0).r * 0.35;
  float dp = length((UV - origin) * card) / length(card);
  float f = mix(n, dp * 0.85 + n * 0.3, from_pt);
  float on = step(0.0005, burn);
  float cut = f - (burn * 1.3 - 0.15);
  if (on > 0.5 && cut < 0.0) discard;
  float edge = (1.0 - smoothstep(0.0, edge_w, cut)) * on;
  float charz = (1.0 - smoothstep(0.0, edge_w * 3.5, cut)) * on;
  ALBEDO = c.rgb * (1.0 - charz * char_amt) * (1.0 - darken);
  ROUGHNESS = 0.5;
  EMISSION = edge_col.rgb * edge * edge_e + glow_col.rgb * glow;
}";

    sealed class Sheet
    {
        public MeshInstance3D Mi = null!; public ShaderMaterial M = null!; public Texture2D Tex = null!;
        public void Set(string k, Variant v) => M.SetShaderParameter(k, v);
        /// A world point in the sheet's own space (for pull_to).
        public Vector3 Local(Vector3 world) => Mi.GlobalTransform.AffineInverse() * world;
        public Vector3 World(Vector2 uv) => Mi.GlobalTransform * new Vector3((uv.X - 0.5f) * CW, 0, (uv.Y - 0.5f) * CH);
    }

    /// The card replaced by a smooth sheet of its own picture that can bend, stretch, flutter and burn away (the real
    /// card is hidden and leaves the board, as CardBreak did).
    Sheet CardSheet(View v)
    {
        var tex = v.Front || v.S.Up ? v.Face.AlbedoTexture : backs[Side(v.Card.Owner)];
        var m = Shade(SheetCode, ("tex", tex), ("noise", FxNoise), ("seed", R(0, 9)), ("card", new Vector2(CW, CH)));
        var mi = new MeshInstance3D { Mesh = new PlaneMesh { Size = new Vector2(CW, CH), SubdivideWidth = 16, SubdivideDepth = 22 }, MaterialOverride = m, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            CustomAabb = new Aabb(new Vector3(-4, -4, -4), new Vector3(8, 8, 8)) };
        world.AddChild(mi);
        mi.GlobalTransform = v.Root.GlobalTransform.TranslatedLocal(new Vector3(0, 0.006f, 0));
        v.Tw?.Kill();
        v.S = v.S with { Loc = Loc.None };
        v.Root.Visible = false; v.Stat.Visible = false;   // no timed re-show: in duels its real card may still sit on the field
        sheeted.Add(v.Card.Uid);   // in duels: stays hidden while its real card is still on the field (Play)
        liveSheets.Add(mi);
        everTaken.Add(v.Card.Uid);
        return new Sheet { Mi = mi, M = m, Tex = tex };
    }

    /// Burn a sheet away over `dur` (from `origin` in card UV when given, else all over), glowing `edge` at the burn
    /// line and shedding embers from it. Frees the sheet at the end.
    void BurnAway(Sheet s, Color edge, double dur, Vector2? origin = null, double delay = 0, float embers = 1f, float edgeE = 5f, float lift = 0.6f)
    {
        s.Set("edge_col", edge); s.Set("edge_e", edgeE);
        if (origin is { } o) { s.Set("origin", o); s.Set("from_pt", 1f); }
        Anim(s.M, "burn", 0.001f, 1f, dur, delay, Tween.TransitionType.Sine, Tween.EaseType.In);
        int n = (int)(28 * embers);
        for (int i = 0; i < n; i++)
        {
            double at = delay + dur * (0.1 + 0.85 * i / n);
            After(at, () =>
            {
                if (!IsInstanceValid(s.Mi)) return;
                float k = (float)((at - delay) / dur);
                Vector2 uv;
                if (origin is { } oo)
                {
                    float rad = Mathf.Clamp((k * 1.3f - 0.15f - 0.15f) / 0.85f, 0.02f, 1.2f) * 1.05f; float a = R(0, 6.28);
                    uv = oo + new Vector2(Mathf.Cos(a) * rad * 1.05f, Mathf.Sin(a) * rad * 0.75f);
                    if (uv.X is < 0 or > 1 || uv.Y is < 0 or > 1) uv = new Vector2(R(0, 1), R(0, 1));
                }
                else uv = new Vector2(R(0.05, 0.95), R(0.05, 0.95));
                Ember(s.World(uv) + Vector3.Up * 0.02f, edge, new Vector3(R(-0.25, 0.25), R(0.15, 0.6), R(-0.25, 0.25)), R(0.7, 1.3), R(0.025, 0.05), lift);
            });
        }
        After(delay + dur + 0.05, () => { if (IsInstanceValid(s.Mi)) s.Mi.QueueFree(); });
    }

    const string FlakeCode = @"shader_type spatial;
render_mode unshaded, blend_mix, cull_disabled, depth_draw_never, shadows_disabled;
uniform sampler2D tex : source_color, filter_linear_mipmap; uniform sampler2D noise : repeat_enable, filter_linear_mipmap;
uniform vec2 uvc; uniform vec2 uvs; uniform float seed; uniform float a = 1.0; uniform vec4 rim : source_color = vec4(1.0); uniform float rim_e = 0.0;
void fragment() {
  vec2 p = UV * 2.0 - 1.0; float n = texture(noise, UV * 0.6 + seed).r;
  float d = length(p * vec2(1.0, 1.25)) + (n - 0.5) * 0.9;
  float shape = smoothstep(0.95, 0.75, d);
  float edge = smoothstep(0.55, 0.92, d) * shape;
  vec3 c = texture(tex, uvc + (UV - 0.5) * uvs).rgb;
  ALBEDO = c * 0.95 + rim.rgb * edge * rim_e; ALPHA = shape * a;
}";

    /// Flakes of a sheet's own picture: `n` soft-edged bits taken from across the card, each sent along `vel(uv)`
    /// (world units / s), tumbling, thinning out over `life`.
    void Flakes(Sheet s, int n, Func<Vector2, Vector3> vel, double life, double spread = 0.4, Color? rim = null, float size = 0.15f, float gravity = 0f)
    {
        for (int i = 0; i < n; i++)
        {
            var uv = new Vector2(R(0.06, 0.94), R(0.05, 0.95));
            var from = s.World(uv) + Vector3.Up * 0.01f;
            double d0 = R(0, spread);
            float sz = size * R(0.6, 1.25);
            var m = Shade(FlakeCode, ("tex", s.Tex), ("noise", FxNoise), ("uvc", uv), ("uvs", new Vector2(sz / CW, sz / CH)), ("seed", R(0, 9)), ("rim", rim ?? Colors.White), ("rim_e", rim != null ? 4f : 0f));
            var q = new MeshInstance3D { Mesh = new QuadMesh { Size = new Vector2(sz, sz) }, MaterialOverride = m, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off, Visible = false };
            world.AddChild(q);
            var v0 = vel(uv); var axis = new Vector3(R(-1, 1), R(-1, 1), R(-1, 1)).Normalized(); float spin = R(4, 11); double l = life * R(0.75, 1.2);
            var b0 = s.Mi.GlobalBasis * Flat.Inverse();
            q.GlobalTransform = new Transform3D(s.Mi.GlobalBasis * new Basis(Vector3.Right, -Mathf.Pi / 2), from);
            var tw = q.CreateTween();
            tw.TweenInterval(d0 + 0.001);
            tw.TweenCallback(Callable.From(() => q.Visible = true));
            var basis0 = q.GlobalBasis;
            tw.TweenMethod(Callable.From<float>(k =>
            {
                float t = k * (float)l;
                q.GlobalTransform = new Transform3D(new Basis(axis, t * spin) * basis0, from + v0 * t + Vector3.Down * gravity * t * t * 0.5f).ScaledLocal(Vector3.One * (1 - 0.6f * k * k));
                m.SetShaderParameter("a", 1 - Mathf.SmoothStep(0.5f, 1f, k));
            }), 0f, 1f, l);
            tw.TweenCallback(Callable.From(q.QueueFree));
        }
    }

    // ---------------------------------------------------------------- wind: a staged twister

    const string TwistVert = @"
uniform float height = 3.0; uniform float top_r = 0.8; uniform float foot_r = 0.08; uniform float grow = 1.0; uniform float widen = 1.0;
uniform float sway = 0.12; uniform float k_r = 1.0; uniform float spin = 1.0; uniform float fade = 0.0; uniform float seed = 0.0;
varying float hh;
void vertex() {
  float h = VERTEX.y + 0.5; hh = h;
  float r = mix(foot_r, top_r, pow(h, 1.7)) * mix(0.16, 1.0, widen) * k_r * (1.0 - fade * 0.55);
  r *= 1.0 + 0.1 * sin(h * 11.0 - TIME * 7.0 * spin + seed) + 0.05 * sin(h * 23.0 + TIME * 5.0);
  vec2 xz = normalize(VERTEX.xz + vec2(0.0001)) * r;
  float sx = sin(TIME * 1.6 + h * 2.3 + seed) * sway * h + sin(TIME * 3.1 + h * 6.0) * sway * 0.25 * h;
  float sz = cos(TIME * 1.2 + h * 1.9 + seed) * sway * 0.5 * h;
  float y = h * grow * height + fade * height * 0.35 * (1.0 - h);
  VERTEX = vec3(xz.x + sx, y, xz.y + sz);
}";

    const string TwistLight = @"shader_type spatial;
render_mode unshaded, blend_add, cull_disabled, depth_draw_never, shadows_disabled;
uniform vec4 col : source_color; uniform sampler2D noise : repeat_enable, filter_linear_mipmap; uniform float alpha = 1.0;" + TwistVert + @"
void fragment() {
  vec2 uv = vec2(UV.x * 3.0 + TIME * 1.1 * spin + hh * 1.4, hh * 0.9 - TIME * 0.55);
  float s1 = texture(noise, vec2(uv.x * 1.6, uv.y * 0.3) + seed).r;
  float s2 = texture(noise, vec2(uv.x * 4.2 + 0.31, uv.y * 0.7)).r;
  float streak = pow(s1, 3.0) * 2.4 + pow(s2, 5.0) * 2.0;
  float fres = 1.0 - abs(dot(normalize(NORMAL), normalize(VIEW)));
  float ends = smoothstep(0.0, 0.1, hh) * smoothstep(1.0, 0.7, hh);
  float er = texture(noise, vec2(UV.x * 2.0 + seed, hh * 1.3 - TIME * 0.25)).r;
  float alive = 1.0 - smoothstep(er - 0.12, er + 0.12, fade * 1.25 - 0.1 + (1.0 - hh) * 0.15);
  ALBEDO = mix(col.rgb, vec3(1.0), clamp(streak * 0.35, 0.0, 1.0));
  ALPHA = clamp(streak * (0.25 + 0.75 * fres) * ends * alive * alpha, 0.0, 1.0);
}";

    const string TwistDust = @"shader_type spatial;
render_mode unshaded, blend_mix, cull_disabled, depth_draw_never, shadows_disabled;
uniform vec4 col : source_color; uniform sampler2D noise : repeat_enable, filter_linear_mipmap; uniform float alpha = 1.0;" + TwistVert + @"
void fragment() {
  vec2 uv = vec2(UV.x * 2.0 + TIME * 0.8 * spin + hh * 1.1, hh * 0.7 - TIME * 0.35);
  float s1 = texture(noise, vec2(uv.x * 1.2, uv.y * 0.45) + seed).r;
  float s2 = texture(noise, vec2(uv.x * 3.0 + 0.5, uv.y * 1.1)).r;
  float fres = 1.0 - abs(dot(normalize(NORMAL), normalize(VIEW)));
  float ends = smoothstep(0.0, 0.06, hh) * smoothstep(1.0, 0.6, hh);
  float er = texture(noise, vec2(UV.x * 1.5 + seed + 0.4, hh * 1.1 - TIME * 0.2)).r;
  float alive = 1.0 - smoothstep(er - 0.15, er + 0.15, fade * 1.3 - 0.12 + (1.0 - hh) * 0.2);
  float dens = (0.3 + 0.9 * s1 * s1 + 0.4 * s2) * mix(0.45, 1.0, pow(fres, 0.8));
  ALBEDO = col.rgb * (0.55 + 0.7 * s2);
  ALPHA = clamp(dens * ends * alive * alpha * col.a, 0.0, 1.0);
}";

    /// A soft swirl of dust on the table, its arms winding in (amt = spin speed).
    const string DustSwirlFx = @"void fragment() {
  vec2 p = UV * 2.0 - 1.0; float r = length(p); float a = atan(p.y, p.x) / 6.28318;
  float sw = a * 3.0 + log(r + 0.05) * 0.9 - TIME * 1.8 * amt;
  float arms = texture(noise, vec2(sw, r * 1.5 - TIME * 0.4)).r;
  float fine = texture(noise, vec2(sw * 3.0 + 0.4, r * 4.0)).r;
  float v = (pow(arms, 2.0) * 1.3 + pow(fine, 4.0) * 0.6) * smoothstep(1.0, 0.35, r) * smoothstep(0.04, 0.22, r);
  ALBEDO = col.rgb * (0.7 + 0.5 * fine); ALPHA = clamp(v * col.a, 0.0, 1.0) * env(0.25, 0.3);
}";

    /// Flames that rise off nothing: the bottom fades out softly (no line where the quad starts).
    const string SoftFireFx = @"void fragment() {
  float x = abs(UV.x - 0.5) * 2.0; float y = 1.0 - UV.y;
  float n1 = texture(noise, vec2(UV.x * 1.6 + seed, UV.y * 1.2 + TIME * 1.3)).r;
  float n2 = texture(noise, vec2(UV.x * 4.0, UV.y * 2.5 + TIME * 2.4 + seed)).r;
  float shape = (1.0 - x * x) * (1.0 - y * 1.25) - 0.25 + (n1 - 0.5) * 0.9 + (n2 - 0.5) * 0.45;
  float f = smoothstep(0.0, 0.45, shape) * smoothstep(1.0, 0.5, x) * smoothstep(0.0, 0.35, y + (n2 - 0.5) * 0.1);
  ALBEDO = mix(col.rgb, col2.rgb, smoothstep(0.5, 1.0, f) * (1.0 - y));
  ALPHA = f * 0.85 * env(0.25, 0.4) * amt;
}";

    /// A wing in the shape of `tex` (a Master Duel wing silhouette, mirrored left-right: that puts its narrow root at
    /// the body, checked against all four orientations 2026-11-13). Solid body, lit leading part, soft rim. The quad is
    /// subdivided so the feathers bend: `bend` turns each point about the root by bend x (distance / span)^2, so the
    /// tips trail behind every beat.
    const string SolidWingFx = @"uniform vec2 root_l = vec2(0.0); uniform float span_l = 1.0; uniform float bend = 0.0;
void vertex() {
  vec2 d = VERTEX.xy - root_l; float k = clamp(length(d) / span_l, 0.0, 1.0); float a = bend * k * k;
  VERTEX.xy = root_l + vec2(d.x * cos(a) - d.y * sin(a), d.x * sin(a) + d.y * cos(a));
  VERTEX.z += bend * k * k * span_l * 0.25;
}
void fragment() {
  vec2 uv = vec2(1.0 - UV.x, UV.y);
  float m = texture(tex, uv).r;
  float body = smoothstep(0.45, 0.6, m);
  float halo = smoothstep(0.03, 0.45, m) * (1.0 - body);
  float lift = 1.0 - smoothstep(0.15, 0.85, UV.y);
  vec3 b = mix(col.rgb, col2.rgb, 0.4 * lift);
  ALBEDO = mix(col2.rgb, b, body); ALPHA = clamp(body * 0.96 + halo * 0.55, 0.0, 1.0) * amt;
}";

    sealed class Wing { public Node3D Pivot = null!; public ShaderMaterial M = null!; public int Side; }

    /// One wing rooted at `root` (basis `basis`, default facing the camera), reaching out to `side`, `size` across.
    Wing MakeWing(Vector3 root, int side, float size, Color col, Color hi, Basis? basis = null)
    {
        var pivot = new Node3D(); world.AddChild(pivot);
        pivot.GlobalTransform = new Transform3D(basis ?? Bill, root);
        // the narrow root sits near the quad's bottom inner corner: (0.95, 0.92) of a left wing in UV
        var rootL = new Vector2(-side * 0.45f * size, -0.42f * size);
        var m = FxMat(SolidWingFx, col, true, side < 0 ? "md2/wingL" : "md2/wingR", ("col2", hi), ("amt", 0f), ("root_l", rootL), ("span_l", size * 1.1f), ("bend", 0f));
        pivot.AddChild(new MeshInstance3D { Mesh = new QuadMesh { Size = new Vector2(size, size), SubdivideWidth = 24, SubdivideDepth = 24 }, MaterialOverride = m, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            Position = new Vector3(-rootL.X, -rootL.Y, 0), CustomAabb = new Aabb(new Vector3(-3, -3, -3), new Vector3(6, 6, 6)) });
        return new Wing { Pivot = pivot, M = m, Side = side };
    }

    /// Beats a wing like a wing: a quick, strong downstroke (40% of the beat) and a slower upstroke, through a wide
    /// arc about the root, the feathers trailing behind the swing (round 7: the even swing with a squash read as walking).
    void Flap(Wing w, double dur, float beats, float swing = 0.7f, float rest = 0.1f)
    {
        float top = rest + swing, bottom = rest - swing * 0.85f, last = top;
        w.Pivot.CreateTween().TweenMethod(Callable.From<float>(k =>
        {
            float u = k * beats % 1f, a;
            if (u < 0.4f) a = Mathf.Lerp(top, bottom, Mathf.SmoothStep(0, 1, u / 0.4f));
            else a = Mathf.Lerp(bottom, top, Mathf.SmoothStep(0, 1, (u - 0.4f) / 0.6f));
            w.Pivot.Rotation = new Vector3(0, 0, w.Side * a);   // + raises the tip on either side
            float vel = a - last; last = a;
            w.M.SetShaderParameter("bend", -w.Side * Mathf.Clamp(vel * 12f, -0.9f, 0.9f));   // the tips trail the swing
        }), 0f, 1f, dur);
    }

    /// A pair-half: one wing that unfolds from the body, beats `beats` times over `stay`, and fades.
    void SolidWing(Vector3 root, int side, float size, Color col, Color hi, double open, double stay, float beats = 2f)
    {
        var w = MakeWing(root, side, size, col, hi);
        w.Pivot.Rotation = new Vector3(0, 0, -side * 1.1f); w.Pivot.Scale = Vector3.One * 0.3f;   // folded down against the body
        var tw = w.Pivot.CreateTween();
        tw.TweenProperty(w.Pivot, "rotation:z", side * 0.8f, open).SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);   // open to the top of the first beat
        tw.Parallel().TweenProperty(w.Pivot, "scale", Vector3.One, open).SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
        tw.Parallel().TweenMethod(Callable.From<float>(e => w.M.SetShaderParameter("amt", e)), 0f, 1f, open * 0.5);
        tw.TweenCallback(Callable.From(() => Flap(w, stay, beats)));
        tw.TweenInterval(stay);
        tw.TweenMethod(Callable.From<float>(e => w.M.SetShaderParameter("amt", 1 - e)), 0f, 1f, 0.35);
        tw.TweenCallback(Callable.From(w.Pivot.QueueFree));
    }

    /// Time slows: the duel clock eases down to `scale` in 0.12 s (real time) and stays there until SlowEnd(), which
    /// eases it back in 0.25 s. Hard cap: whatever happens, the whole slow motion lasts at most `max` real seconds
    /// (user 2026-11-13: start just before the last impact, end right after the card is placed, 1.5 s or less).
    /// With Master Duel's slow-motion sound and a deep whoosh that pitches down with the clock and back up.
    AudioStreamPlayer? slowVoice, slowSnd; Tween? slowTw; int slowGen; ulong slowT0; bool slowEnding;
    const double SlowOut = 0.25;
    void SlowMo(double scale = 0.25, double max = 1.5, bool sound = true)
    {
        int gen = ++slowGen; slowT0 = Time.GetTicksMsec(); slowEnding = false;
        slowTw?.Kill();
        slowTw = CreateTween().SetIgnoreTimeScale(true);
        slowTw.TweenMethod(Callable.From<double>(f => { slowF = f; ApplyTs(); }), slowF, scale, 0.12).SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.Out);
        GetTree().CreateTimer(Math.Max(0.2, max - SlowOut), true, false, true).Timeout += () => { if (gen == slowGen) SlowEnd(); };
        if (!sound || Shooting) return;
        // the slow-motion sound runs 3.9 s on its own: it plays through its own player and fades out with SlowEnd
        if (slowSnd == null) { slowSnd = new AudioStreamPlayer { Bus = "Sound" }; AddChild(slowSnd); }
        slowSnd.Stream = GD.Load<AudioStream>(SfxPath("SE_DUEL_DIRECTATTACK_SLOW")); slowSnd.VolumeDb = 0; slowSnd.Play();
        if (slowVoice == null) { slowVoice = new AudioStreamPlayer { Bus = "Sound", VolumeDb = -2 }; AddChild(slowVoice); }
        slowVoice.VolumeDb = -2;
        slowVoice.Stream = GD.Load<AudioStream>(SfxPath("SE_ATTACK_A_WIND_SPECIAL_01"));
        slowVoice.PitchScale = 1f; slowVoice.Play();
        CreateTween().SetIgnoreTimeScale(true).TweenProperty(slowVoice, "pitch_scale", 0.45f, 0.3).SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.Out);
    }

    /// Ends the slow motion now (eases back over SlowOut real seconds). Safe to call twice.
    void SlowEnd(double ramp = SlowOut)
    {
        if (slowEnding || (slowF >= 1 && slowTw == null)) return;   // already easing back: never restart the ramp
        slowEnding = true; slowGen++;
        slowTw?.Kill();
        slowTw = CreateTween().SetIgnoreTimeScale(true);
        slowTw.TweenMethod(Callable.From<double>(f => { slowF = f; ApplyTs(); }), slowF, 1.0, ramp).SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.In);
        slowTw.TweenCallback(Callable.From(() => { slowF = 1; ApplyTs(); slowTw = null; slowEnding = false; if (OS.IsDebugBuild()) GD.Print($"slowmo: {Time.GetTicksMsec() - slowT0} ms real"); }));
        // its sounds end with it: the whoosh pitches back up, both fade out over the ease-back
        foreach (var pl in new[] { slowVoice, slowSnd })
            if (pl != null && pl.Playing)
            {
                var ft = CreateTween().SetIgnoreTimeScale(true).SetParallel();
                if (pl == slowVoice) ft.TweenProperty(pl, "pitch_scale", 1f, ramp);
                ft.TweenProperty(pl, "volume_db", -40f, ramp + 0.1).SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.In);
                ft.Chain().TweenCallback(Callable.From(pl.Stop));
            }
    }

    sealed class Twist
    {
        public Node3D Root = null!; public readonly List<ShaderMaterial> Ms = new();
        public void P(string k, float v) { foreach (var m in Ms) m.SetShaderParameter(k, v); }
    }

    /// A funnel of wind standing on `foot`: dust skin, two streaked light skins, a dust swirl at its foot. Starts as
    /// nothing (grow 0, widen 0); drive it with TwistLife (form, travel, thin out).
    Twist Twister(Vector3 foot, float height, float topR, Color light, Color dust)
    {
        var tw = new Twist { Root = new Node3D { Position = foot } };
        world.AddChild(tw.Root);
        float seed = R(0, 9);
        void Layer(string code, Color c, float k, float spin)
        {
            var m = Shade(code, ("col", c), ("noise", FxNoise), ("height", height), ("top_r", topR), ("foot_r", topR * 0.11f), ("k_r", k), ("spin", spin), ("seed", seed), ("grow", 0f), ("widen", 0f));
            tw.Ms.Add(m);
            tw.Root.AddChild(new MeshInstance3D { Mesh = new CylinderMesh { TopRadius = 1, BottomRadius = 1, Height = 1, RadialSegments = 56, Rings = 48, CapTop = false, CapBottom = false },
                MaterialOverride = m, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off, CustomAabb = new Aabb(new Vector3(-3, -1, -3), new Vector3(6, 8, 6)) });
        }
        Layer(TwistDust, dust, 1.0f, 0.8f);
        Layer(TwistLight, light, 0.93f, 1.0f);
        Layer(TwistLight, light * new Color(0.8f, 0.85f, 0.9f), 0.55f, -1.5f);
        Layer(TwistLight, Colors.White, 0.22f, 2.2f);
        return tw;
    }

    /// Form (a dust swirl gathers, a thin rope rises, it opens out), hold, thin out and lift away.
    void TwistLife(Twist t, double form, double hold, double end, Color dust)
    {
        var foot = t.Root.GlobalPosition;
        // the swirl of dust where it touches down, gathering first
        var swirl = FxQuad(DustSwirlFx, foot + Vector3.Up * 0.03f, new Vector2(1.7f, 1.7f), form + hold + end * 0.7, dust, null, true, null, ("amt", 1.6f));
        swirl.Reparent(t.Root);
        var tw = CreateTween().SetParallel();
        tw.TweenMethod(Callable.From<float>(k => t.P("grow", k)), 0f, 1f, form * 0.7).SetDelay(form * 0.15).SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.Out);
        tw.TweenMethod(Callable.From<float>(k => t.P("widen", k)), 0f, 1f, form * 0.6).SetDelay(form * 0.45).SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.InOut);
        tw.TweenMethod(Callable.From<float>(k => t.P("fade", k)), 0f, 1f, end).SetDelay(form + hold).SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.In);
        for (int i = 0; i < 6; i++) { double d = form * 0.1 + i * (form + hold) / 6; After(d, () => { if (IsInstanceValid(t.Root)) Puffs(t.Root.GlobalPosition + Vector3.Up * 0.05f, 2, dust * new Color(1, 1, 1, 0.5f), 0.55f, 0.5f, 0.35f); }); }
        After(form + hold + end + 0.05, () => { if (IsInstanceValid(t.Root)) t.Root.QueueFree(); });
    }

    // ---------------------------------------------------------------- fire: a boiling explosion

    const string FirePuffFx = @"void fragment() {
  vec2 p = UV * 2.0 - 1.0; float r = length(p);
  vec2 q = UV * 0.9 + vec2(seed, seed * 0.7);
  float w = texture(noise, q + vec2(0.0, -TIME * 0.12)).r;
  float n = texture(noise, q * 2.4 + vec2(w * 0.7, -TIME * 0.32)).r;
  float n2 = texture(noise, q * 6.5 + w * 1.2 + vec2(TIME * 0.1, -TIME * 0.2)).r;
  float n3 = texture(noise, q * 13.0 - w + vec2(0.0, -TIME * 0.5)).r;
  float shape = r + (n - 0.5) * 0.85 + (n2 - 0.5) * 0.45 + (n3 - 0.5) * 0.18;
  float dens = smoothstep(1.0, 0.3, shape) * (0.75 + 0.25 * n3);
  float heat = clamp((0.95 - shape) * 1.7 - t * (1.25 + amt) + (n2 - 0.5) * 0.7 + 0.35, 0.0, 1.0);
  vec3 smoke = vec3(0.075, 0.065, 0.065) * (0.55 + n2 * 0.9);
  vec3 c = mix(smoke, vec3(0.5, 0.06, 0.01), smoothstep(0.0, 0.22, heat));
  c = mix(c, vec3(1.0, 0.33, 0.04), smoothstep(0.18, 0.45, heat));
  c = mix(c, vec3(1.0, 0.72, 0.25), smoothstep(0.45, 0.75, heat));
  c = mix(c, vec3(1.0, 0.96, 0.82), smoothstep(0.78, 1.0, heat));
  ALBEDO = c * (1.0 + heat * 1.4);
  ALPHA = clamp(dens * (1.0 - smoothstep(0.6, 1.0, t)) * smoothstep(0.0, 0.04, t) * col.a, 0.0, 1.0);
}";

    const string CoreGlowFx = @"void fragment() {
  float r = length(UV * 2.0 - 1.0);
  float g = exp(-r * r * 9.0) + exp(-r * 3.5) * 0.35;
  ALBEDO = mix(col.rgb, vec3(1.0), exp(-r * r * 20.0)); ALPHA = clamp(g * (1.0 - t) * (1.0 - t), 0.0, 1.0);
}";

    /// A ball of fire that boils up out of a white flash and burns down to smoke. size ~ its radius.
    void Explosion(Vector3 at, float size = 1f, Color? glow = null)
    {
        var hot = glow ?? new Color(1f, 0.6f, 0.2f);
        FxQuad(CoreGlowFx, at + Vector3.Up * 0.3f * size, new Vector2(3.6f, 3.6f) * size, 0.35, hot, Bill);
        for (int i = 0; i < 30; i++)
        {
            var off = new Vector3(R(-1, 1), R(-0.3, 1), R(-1, 1)) * 0.28f * size;
            double d = i * 0.008 + R(0, 0.04), life = R(0.9, 1.4);
            float s0 = 0.35f * size, s1 = R(1.0, 1.45) * size;
            After(d, () =>
            {
                var q = FxQuad(FirePuffFx, at + off, Vector2.One, life, Colors.White, Bill, true, null, ("amt", R(0.0, 0.5)));
                var from = at + off + Vector3.Up * 0.25f * size; var rise = off.Normalized() * 0.45f * size + Vector3.Up * R(0.5, 0.9) * size;
                q.CreateTween().TweenMethod(Callable.From<float>(k =>
                {
                    float e = 1 - Mathf.Pow(1 - k, 3);
                    q.Transform = new Transform3D(Bill.Scaled(Vector3.One * Mathf.Lerp(s0, s1, e)), from + rise * e + Vector3.Up * k * k * 0.4f * size);
                }), 0f, 1f, life);
            });
        }
        After(0.35, () => Puffs(at + Vector3.Up * 0.9f * size, (int)(6 * size) + 2, new Color(0.09f, 0.08f, 0.08f, 0.6f), 1.2f * size, 1.1f * size, 0.5f * size));
        Flash(at + Vector3.Up * 0.4f, hot, 10f * size); Warp(at + Vector3.Up * 0.05f, 5f * size, 0.5, 2.0f);
        Streaks(at + Vector3.Up * 0.3f, new Color(1f, 0.7f, 0.3f), (int)(26 * size), 4.2f * size);
        for (int i = 0; i < 14 * size; i++) Ember(at + Vector3.Up * 0.3f * size, new Color(1f, 0.55f, 0.15f), new Vector3(R(-1, 1), R(0.3, 1.4), R(-1, 1)) * 2.4f * size, R(1.0, 1.8), R(0.03, 0.06), 0.3f);
        ScreenFlash(new Color(1f, 0.7f, 0.4f), 0.25f * Mathf.Min(1, size), 0.3);
        Shake(0.18f * size); HitStop(0.07);
        SootMark(at, 1.6f * size, 2.6);
    }

    // ---------------------------------------------------------------- marks left on the table (never a crack)

    const string SootFx = @"void fragment() {
  vec2 p = UV * 2.0 - 1.0; float r = length(p);
  float n = texture(noise, UV * 1.3 + seed).r; float n2 = texture(noise, UV * 4.5 + seed).r;
  float d = r + (n - 0.5) * 0.55;
  float soot = smoothstep(0.9, 0.15, d) * (0.55 + 0.45 * n2);
  float hot = (1.0 - smoothstep(0.0, 0.3, t)) * smoothstep(0.55, 0.0, d) * pow(n2, 1.6);
  ALBEDO = mix(vec3(0.02, 0.015, 0.012), col.rgb * 2.5, clamp(hot * 1.6, 0.0, 1.0));
  ALPHA = clamp(soot * 0.7 * amt + hot, 0.0, 1.0) * (1.0 - smoothstep(0.65, 1.0, t)) * smoothstep(0.0, 0.04, t);
}";

    /// A soft dark burn on the table, hot at first, cooling and fading over `life`.
    void SootMark(Vector3 at, float size, double life, Color? hot = null, float dark = 1f) =>
        FxQuad(SootFx, at with { Y = 0.012f }, new Vector2(size, size), life, hot ?? new Color(1f, 0.45f, 0.12f), Flat, true, null, ("amt", dark));

    // ---------------------------------------------------------------- lightning that hits

    const string GroundGlowFx = @"void fragment() {
  vec2 p = UV * 2.0 - 1.0; float r = length(p);
  float n = texture(noise, vec2(atan(p.y, p.x) * 0.8 + seed, r * 2.0 - TIME * 3.0)).r;
  float g = exp(-r * r * 7.0) * (0.7 + 0.6 * n) + exp(-r * 2.5) * 0.25;
  ALBEDO = mix(col.rgb, vec3(1.0), exp(-r * r * 30.0)); ALPHA = clamp(g * (1.0 - smoothstep(0.0, 1.0, t)), 0.0, 1.0);
}";

    /// Lightning straight down onto `at`: a dark beat, a faint leader, then the strike with a screen flash, a plasma
    /// glow on the ground, crawling arcs, sparks, a shock in the air and a soot mark. Returns the delay to the hit.
    double LightningHit(Vector3 at, Color c, float power = 1f, bool dim = true, float flash = 1f, bool bolt = true, bool stop = true)
    {
        const double lead = 0.12;
        if (dim) ScreenDim(0.35f * power, 0.08, 0.05, 0.25);
        if (bolt) FxStand(BoltFx, at, new Vector2(1.0f, 6.5f), 0.1, c * new Color(0.5f, 0.5f, 0.5f));
        After(lead, () =>
        {
            if (bolt)   // false: the caller draws its own bolt (Batch6 Fork); only the ground hit here
            {
                FxStand(BoltFx, at, new Vector2(3.4f * power, 6.8f), 0.55, c);
                FxStand(BoltFx, at, new Vector2(1.8f * power, 6.8f), 0.42, Colors.White);
                FxStand(BoltFx, at + new Vector3(R(-0.3, 0.3), 0, 0), new Vector2(4.2f * power, 6.3f), 0.28, c * new Color(0.6f, 0.6f, 0.7f));
                FxStand(PillarFx, at, new Vector2(0.45f * power, 6.8f), 0.22, Colors.White, false, null, ("amt", 1f));
            }
            ScreenFlash(c.Lerp(Colors.White, 0.6f), 0.5f * power * flash, 0.25);
            Flash(at + Vector3.Up * 0.3f, c, 16f * power);
            FxQuad(GroundGlowFx, at + Vector3.Up * 0.02f, new Vector2(1.8f, 1.8f) * power, 0.55, c);
            FxQuad(CoreGlowFx, at + Vector3.Up * 0.25f, new Vector2(1.1f, 1.1f) * power, 0.2, c, Bill);
            FxQuad(RingFx, at + Vector3.Up * 0.03f, new Vector2(1.7f, 1.7f) * power, 0.32, c);
            Warp(at + Vector3.Up * 0.05f, 4.0f * power, 0.5, 1.8f * power);
            // arcs crawling out over the table from where it hit
            for (int i = 0; i < 7; i++)
            {
                float a = i * Mathf.Tau / 7 + R(-0.3, 0.3), len = R(0.6, 1.25) * power;
                var dir = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a));
                var y = dir; var z = Vector3.Up; var x = y.Cross(z).Normalized();
                double d = R(0, 0.12);
                After(d, () => FxQuad(BoltFx, at + dir * len / 2 + Vector3.Up * 0.03f, new Vector2(0.45f, len), 0.26, c, new Basis(x, y, z)));
            }
            Streaks(at + Vector3.Up * 0.1f, c, (int)(26 * power), 4.2f);
            Streaks(at + Vector3.Up * 0.1f, Colors.White, (int)(10 * power), 3f);
            Shake(0.2f * power); if (stop) HitStop(0.05);   // stop: false for bolts in a row (the freezes read as stutter)
            After(0.2, () => Puffs(at + Vector3.Up * 0.2f, 4, new Color(0.18f, 0.18f, 0.22f, 0.5f), 0.8f, 0.8f, 0.25f));
            SootMark(at, 1.5f * power, 2.4, c, 0.85f);
        });
        return lead;
    }

    // ---------------------------------------------------------------- shapes of light

    /// A heart: soft-edged, a smooth S-curved meeting of `col` (left) and `col2` (right), a soft halo; amt = beats.
    const string SoftHeartFx = @"float dot2(vec2 v) { return dot(v, v); }
float sdHeart(vec2 p) { p.x = abs(p.x); if (p.y + p.x > 1.0) return sqrt(dot2(p - vec2(0.25, 0.75))) - sqrt(2.0) / 4.0; return sqrt(min(dot2(p - vec2(0.0, 1.0)), dot2(p - 0.5 * max(p.x + p.y, 0.0)))) * sign(p.x - p.y); }
void fragment() {
  float beat = fract(t * amt); float swell = 1.0 + 0.16 * exp(-beat * 8.0) + 0.07 * exp(-pow((beat - 0.26) / 0.07, 2.0));
  vec2 p = (vec2(UV.x, 1.0 - UV.y) * 2.0 - 1.0) * 1.45 / swell + vec2(0.0, 0.62);
  float d = sdHeart(p);
  float body = smoothstep(0.035, -0.035, d);
  float inner = smoothstep(0.0, -0.45, d);
  float halo = exp(-max(d, 0.0) * 5.0) * (1.0 - body) * 0.55;
  float split = smoothstep(-0.18, 0.18, p.x + sin(p.y * 4.0 - 1.0) * 0.12);
  vec3 c = mix(col.rgb, col2.rgb, split);
  vec3 lit = c * (0.75 + 0.6 * inner) + vec3(1.0) * pow(smoothstep(0.3, 0.0, length(p - vec2(-0.32, 0.72))), 2.0) * 0.5;
  ALBEDO = mix(c, lit, body); ALPHA = clamp(body * 0.92 + halo, 0.0, 1.0) * env(0.15, 0.22);
}";

    /// One feather of light: a soft vane with a bright shaft, both ends fading (no hard edge anywhere).
    const string LightFeatherFx = @"void fragment() {
  vec2 p = UV * 2.0 - 1.0; float y = 1.0 - UV.y;
  float w = 0.55 * pow(clamp(sin(y * 3.14159), 0.0, 1.0), 0.7) * (0.55 + 0.45 * y);
  float bend = p.x - 0.1 * sin(y * 2.2);
  float vane = smoothstep(w, w * 0.35, abs(bend)) * smoothstep(0.02, 0.12, y);
  float barbs = 0.8 + 0.2 * sin((y * 1.2 + abs(bend) * 1.4) * 80.0);
  float shaft = exp(-pow(bend / 0.03, 2.0)) * smoothstep(0.0, 0.15, y) * smoothstep(1.0, 0.85, y);
  float v = vane * barbs * 0.55 + shaft * 0.9;
  ALBEDO = mix(col.rgb, vec3(1.0), shaft * 0.6); ALPHA = clamp(v * amt, 0.0, 1.0);
}";

    /// A wing of light feathers rooted at `root`, reaching out to `side` (-1 left, 1 right), `span` wide: an arm
    /// curving up and out, long flight feathers hanging from it (longest at the tip), a row of short coverts over
    /// them. It opens out from the body over `open` s, stays, and fades.
    void LightWing(Vector3 root, int side, float span, Color c, double open, double stay)
    {
        var holder = new Node3D(); world.AddChild(holder);
        holder.GlobalTransform = new Transform3D(Bill, root);
        var mats = new List<ShaderMaterial>();
        Vector2 Arm(float s) => new(side * span * s, span * 0.42f * Mathf.Sin(s * Mathf.Pi * 0.55f));
        void Row(int n, float s0, float lenA, float lenB, float hang, float z, float bright)
        {
            for (int i = 0; i < n; i++)
            {
                float s = s0 + (1 - s0) * i / (n - 1);
                float len = span * Mathf.Lerp(lenA, lenB, Mathf.Pow(s, 1.4f));
                float ang = side * (hang + 1.05f * s * s);              // from hanging down to reaching out
                var dir = new Vector2(Mathf.Sin(ang), -Mathf.Cos(ang));
                var m = FxMat(LightFeatherFx, c, false, null, ("amt", 0f)); mats.Add(m);
                var q = new MeshInstance3D { Mesh = new QuadMesh { Size = new Vector2(len * 0.42f, len) }, MaterialOverride = m, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off };
                holder.AddChild(q);
                var at = Arm(s) + dir * len * 0.45f;
                q.Transform = new Transform3D(new Basis(Vector3.Back, Mathf.Atan2(-dir.X, dir.Y)), new Vector3(at.X, at.Y, z + 0.002f * i));
            }
        }
        Row(16, 0.1f, 0.3f, 0.72f, 0.1f, 0f, 0.75f);      // flight feathers
        Row(13, 0.04f, 0.15f, 0.3f, 0.3f, 0.03f, 0.9f);
        Row(9, 0.02f, 0.08f, 0.14f, 0.6f, 0.05f, 1f);       // the small feathers of the leading edge     // coverts along the arm
        holder.Scale = new Vector3(0.12f, 0.6f, 1);
        var tw = holder.CreateTween();
        tw.TweenProperty(holder, "scale", Vector3.One, open).SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
        tw.Parallel().TweenMethod(Callable.From<float>(e => { foreach (var m in mats) m.SetShaderParameter("amt", e * 1.5f); }), 0f, 1f, open * 0.6);
        tw.TweenInterval(stay);
        tw.TweenMethod(Callable.From<float>(e => { foreach (var m in mats) m.SetShaderParameter("amt", 1.5f * (1 - e)); }), 0f, 1f, 0.45);
        tw.TweenCallback(Callable.From(holder.QueueFree));
    }
}
