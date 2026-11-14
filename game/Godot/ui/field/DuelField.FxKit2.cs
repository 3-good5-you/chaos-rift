using Godot;
using PoC.Engine;

namespace PoC.Game.UI;

/// Effect kit, second layer (2026-11-12, after "lack detail / same effect in another colour / same sound"): screen
/// distortion, cards that break into pieces of their own picture, streak sparks, soft smoke, fireballs, arcs of
/// lightning between two points, and one-off shapes (sword, crescent, heart, grid well, void disc). Plus Snd: each
/// card effect plays its own Master Duel sound.
public partial class DuelField
{
    /// A sound of its own for an effect, `delay` seconds from now.
    void Snd(string name, double delay = 0) { if (delay <= 0) Sfx(name); else After(delay, () => Sfx(name)); }

    // ---------------------------------------------------------------- screen distortion

    const string WarpCode = @"shader_type spatial;
render_mode unshaded, blend_mix, cull_disabled, depth_draw_never, shadows_disabled, depth_test_disabled;
uniform sampler2D screen : hint_screen_texture, repeat_disable, filter_linear;
uniform float t = 0.0; uniform float amt = 1.0; uniform float lens = 0.0;
void fragment() {
  vec2 p = UV * 2.0 - 1.0; float r = length(p); vec2 dir = p / max(r, 0.0001);
  float rad = mix(0.05, 0.9, 1.0 - pow(1.0 - t, 3.0));
  float ring = exp(-pow((r - rad) / 0.11, 2.0)) * (1.0 - t) * (1.0 - lens);
  float pull = lens * smoothstep(1.0, 0.25, r) * smoothstep(0.0, 0.2, t) * (1.0 - smoothstep(0.85, 1.0, t)) * 0.5 / (r + 0.25);
  float k = ring + pull;
  vec2 sdir = normalize(dir * vec2(1.0, -0.6));
  ALBEDO = texture(screen, SCREEN_UV - sdir * k * amt * 0.05).rgb * (1.0 + ring * 0.25);
  ALPHA = clamp(k * 6.0, 0.0, 1.0) * smoothstep(1.0, 0.92, r);
}";

    /// The picture behind a quad bends: a ring running outward (a shockwave), or with `lens` a steady pull inward.
    MeshInstance3D Warp(Vector3 at, float size, double life, float strength = 1f, bool lens = false, Basis? basis = null)
    {
        var m = Shade(WarpCode, ("amt", strength), ("lens", lens ? 1f : 0f));
        m.RenderPriority = -8;   // under the glows, which are drawn after it
        var q = new MeshInstance3D { Mesh = new QuadMesh { Size = new Vector2(size, size) }, MaterialOverride = m, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off };
        world.AddChild(q);
        q.Transform = new Transform3D(basis ?? Flat, at);
        Run(q, m, life);
        return q;
    }

    // ---------------------------------------------------------------- sparks, smoke, fire

    static GradientTexture2D? streakTex;
    static GradientTexture2D StreakTex => streakTex ??= new GradientTexture2D { Width = 16, Height = 64, Fill = GradientTexture2D.FillEnum.Linear, FillFrom = new Vector2(0.5f, 0), FillTo = new Vector2(0.5f, 1),
        Gradient = new Gradient { Offsets = new[] { 0f, 0.15f, 1f }, Colors = new[] { new Color(1, 1, 1, 0), Colors.White, new Color(1, 1, 1, 0) } } };

    /// Sparks as streaks: each one a short line of light along its own flight, falling and fading.
    void Streaks(Vector3 at, Color c, int n, float speed = 3f, float gravity = 2.2f, Vector3? bias = null, float spread = 1f)
    {
        for (int i = 0; i < n; i++)
        {
            var v0 = ((bias ?? Vector3.Up * 0.6f) + new Vector3((float)GD.RandRange(-1.0, 1.0), (float)GD.RandRange(-0.2, 1.0), (float)GD.RandRange(-1.0, 1.0)) * spread).Normalized() * speed * (float)GD.RandRange(0.35, 1.0);
            var m = Additive(StreakTex, c); m.AlbedoColor = new Color(c.R * 1.6f, c.G * 1.6f, c.B * 1.6f, 1);
            float w = (float)GD.RandRange(0.012, 0.03), len = (float)GD.RandRange(0.12, 0.34);
            var q = new MeshInstance3D { Mesh = new QuadMesh { Size = new Vector2(w, len) }, MaterialOverride = m, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off };
            world.AddChild(q);
            double d = GD.RandRange(0.35, 0.85);
            var tw = q.CreateTween();
            tw.TweenMethod(Callable.From<float>(k =>
            {
                float s = k * (float)d; var vel = v0 + Vector3.Down * gravity * s;
                var pos = at + v0 * s + Vector3.Down * gravity * s * s * 0.5f;
                var y = vel.Normalized(); var z = (cam.GlobalPosition - pos).Normalized(); var x = y.Cross(z).Normalized(); z = x.Cross(y);
                q.Transform = new Transform3D(new Basis(x, y, z), pos);
                m.AlbedoColor = new Color(c.R * 1.6f, c.G * 1.6f, c.B * 1.6f, 1 - k * k);
            }), 0f, 1f, d);
            tw.TweenCallback(Callable.From(q.QueueFree));
        }
    }

    const string PuffFx = @"void fragment() {
  vec2 p = UV * 2.0 - 1.0; float r = length(p);
  float n = texture(noise, UV * 0.7 + vec2(seed, seed * 0.37 - TIME * 0.05)).r; float n2 = texture(noise, UV * 1.9 + vec2(seed * 0.5, TIME * 0.08)).r;
  float body = smoothstep(1.0, 0.15, r + (n - 0.5) * 0.7 + (n2 - 0.5) * 0.25);
  ALBEDO = col.rgb * (0.55 + 0.6 * n2); ALPHA = body * col.a * (1.0 - t) * smoothstep(0.0, 0.12, t);
}";

    /// Soft round smoke that swells, drifts up and thins out.
    void Puffs(Vector3 at, int n, Color c, float size = 0.9f, float rise = 0.9f, float spread = 0.4f)
    {
        for (int i = 0; i < n; i++)
        {
            var from = at + new Vector3((float)GD.RandRange(-spread, spread), (float)GD.RandRange(0.0, 0.2), (float)GD.RandRange(-spread, spread));
            var drift = new Vector3((float)GD.RandRange(-0.4, 0.4), rise * (float)GD.RandRange(0.6, 1.2), (float)GD.RandRange(-0.3, 0.3));
            double d = GD.RandRange(0.8, 1.4); float s0 = size * (float)GD.RandRange(0.5, 0.8);
            var q = FxQuad(PuffFx, from, new Vector2(1, 1), d, c, Bill, mix: true);
            q.CreateTween().TweenMethod(Callable.From<float>(k => q.Transform = new Transform3D(Bill.Scaled(Vector3.One * (s0 + size * 0.9f * k)), from + drift * k)), 0f, 1f, d).SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.Out);
        }
    }

    /// A ball of fire: white-hot, boiling, swelling and burning down to dark smoke.
    void Fireball(Vector3 at, float radius, double life = 0.9)
    {
        var m = Shade(@"shader_type spatial;
render_mode unshaded, blend_mix, cull_back, depth_draw_never, shadows_disabled;
uniform float t = 0.0; uniform float seed = 0.0; uniform sampler2D noise : repeat_enable, filter_linear_mipmap;
void fragment() {
  vec2 uv = UV * vec2(2.0, 1.0) + vec2(seed, -TIME * 0.35);
  float n = texture(noise, uv).r; float n2 = texture(noise, uv * 2.7 + 0.31).r;
  float heat = clamp(1.25 - t * 1.5 + (n - 0.5) * 0.9 + (n2 - 0.5) * 0.5, 0.0, 1.0);
  vec3 c = mix(vec3(0.05, 0.03, 0.03), vec3(0.9, 0.16, 0.02), smoothstep(0.0, 0.35, heat));
  c = mix(c, vec3(1.0, 0.62, 0.1), smoothstep(0.3, 0.65, heat)); c = mix(c, vec3(1.0, 0.97, 0.8), smoothstep(0.65, 1.0, heat));
  float f = abs(dot(normalize(NORMAL), normalize(VIEW)));
  ALBEDO = c * (1.0 + heat * 1.2);
  ALPHA = smoothstep(0.0, 0.45, f + (n - 0.5) * 0.5) * (1.0 - smoothstep(0.55, 1.0, t)) * mix(1.0, 0.75, t);
}", ("noise", FxNoise), ("seed", (float)GD.RandRange(0.0, 9.0)));
        var q = new MeshInstance3D { Mesh = new SphereMesh { Radius = 1, Height = 2, RadialSegments = 40, Rings = 20 }, Position = at, Scale = Vector3.One * radius * 0.2f, MaterialOverride = m, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off };
        world.AddChild(q);
        Run(q, m, life);
        var tw = q.CreateTween().SetParallel();
        tw.TweenProperty(q, "scale", Vector3.One * radius, life).SetTrans(Tween.TransitionType.Expo).SetEase(Tween.EaseType.Out);
        tw.TweenProperty(q, "position:y", at.Y + radius * 0.5f, life);
    }

    // ---------------------------------------------------------------- cards in pieces

    /// The card breaks into pieces of its own picture. cols x rows cells (jittered by `jitter`), thrown outward by
    /// `burst`, pushed by `push` (world), turning, falling, fading. The real card is gone.
    void CardBreak(View v, int cols = 4, int rows = 5, float jitter = 0.6f, float burst = 1.6f, Vector3? push = null, float gravity = 3.2f, double life = 0.9, bool triangles = true)
    {
        var tex = v.Front || v.S.Up ? v.Face.AlbedoTexture : backs[Side(v.Card.Owner)];
        var holder = new Node3D(); world.AddChild(holder);
        holder.GlobalTransform = v.Root.GlobalTransform;
        var rng = new Random();
        var pts = new Vector2[cols + 1, rows + 1];
        for (int i = 0; i <= cols; i++)
            for (int j = 0; j <= rows; j++)
            {
                float jx = i is 0 || i == cols ? 0 : (float)(rng.NextDouble() - 0.5) * jitter, jy = j is 0 || j == rows ? 0 : (float)(rng.NextDouble() - 0.5) * jitter;
                pts[i, j] = new Vector2((i + jx) / cols, (j + jy) / rows);
            }
        var pushLocal = holder.GlobalBasis.Inverse() * (push ?? Vector3.Zero);
        void Piece(Vector2[] uv)
        {
            var mid2 = uv.Aggregate(Vector2.Zero, (a, b) => a + b) / uv.Length;
            var mid = new Vector3((mid2.X - 0.5f) * CW, 0.006f, (mid2.Y - 0.5f) * CH);
            var st = new SurfaceTool(); st.Begin(Mesh.PrimitiveType.Triangles);
            int[] order = uv.Length == 3 ? new[] { 0, 1, 2 } : new[] { 0, 1, 2, 0, 2, 3 };
            foreach (int k in order) { st.SetNormal(Vector3.Up); st.SetUV(uv[k]); st.AddVertex(new Vector3((uv[k].X - 0.5f) * CW, 0.006f, (uv[k].Y - 0.5f) * CH) - mid); }
            var mat = new StandardMaterial3D { AlbedoTexture = tex, CullMode = BaseMaterial3D.CullModeEnum.Disabled, Transparency = BaseMaterial3D.TransparencyEnum.Alpha, Roughness = 0.5f, TextureFilter = BaseMaterial3D.TextureFilterEnum.LinearWithMipmapsAnisotropic };
            var mi = new MeshInstance3D { Mesh = st.Commit(), MaterialOverride = mat, Position = mid };
            holder.AddChild(mi);
            var vel = new Vector3(mid.X, 0, mid.Z).Normalized() * burst * (0.5f + (float)rng.NextDouble()) + Vector3.Up * burst * (0.5f + (float)rng.NextDouble() * 0.8f) + pushLocal * (0.6f + (float)rng.NextDouble() * 0.8f);
            var axis = new Vector3((float)rng.NextDouble() - 0.5f, (float)rng.NextDouble() - 0.5f, (float)rng.NextDouble() - 0.5f).Normalized(); float spin = 3 + (float)rng.NextDouble() * 9;
            double d = life * (0.75 + rng.NextDouble() * 0.5);
            var tw = mi.CreateTween();
            tw.TweenMethod(Callable.From<float>(k =>
            {
                float s = k * (float)d;
                mi.Transform = new Transform3D(new Basis(axis, s * spin), mid + vel * s + Vector3.Down * gravity * s * s * 0.5f);
                mat.AlbedoColor = new Color(1, 1, 1, 1 - Mathf.SmoothStep(0.55f, 1f, k));
            }), 0f, 1f, d);
        }
        for (int i = 0; i < cols; i++)
            for (int j = 0; j < rows; j++)
            {
                var a = pts[i, j]; var b = pts[i + 1, j]; var c = pts[i + 1, j + 1]; var e = pts[i, j + 1];
                if (triangles) { Piece(new[] { a, b, e }); Piece(new[] { b, c, e }); } else Piece(new[] { a, b, c, e });
            }
        After(life * 1.3 + 0.1, () => { if (IsInstanceValid(holder)) holder.QueueFree(); });
        v.S = v.S with { Loc = Loc.None };
        HideFor(v, 0.0);
    }

    // ---------------------------------------------------------------- lightning between two points

    /// A bolt from a to b (any direction), facing the camera.
    MeshInstance3D Arc(Vector3 a, Vector3 b, Color c, float width = 1.2f, double life = 0.4)
    {
        var d = b - a; var mid = (a + b) / 2;
        var y = -d.Normalized();   // the quad's bottom (UV.y = 1, the pinned end) is b
        var z = (cam.GlobalPosition - mid).Normalized(); var x = y.Cross(z).Normalized(); z = x.Cross(y);
        return FxQuad(BoltFx, mid, new Vector2(width, d.Length()), life, c, new Basis(x, y, z));
    }

    /// A heavy strike from the sky: three bolts on one line, a white frame, a bending shock ring, a scorch that
    /// glows and cools, streaks of sparks. No clouds.
    void Thunderbolt(Vector3 at, Color c, float height = 5f, float power = 1f)
    {
        FxStand(BoltFx, at, new Vector2(2.6f * power, height), 0.55, c);
        FxStand(BoltFx, at, new Vector2(1.5f * power, height), 0.42, Colors.White);
        FxStand(BoltFx, at + new Vector3(0.1f, 0, 0), new Vector2(3.4f * power, height), 0.3, c * new Color(0.6f, 0.6f, 0.6f));
        FxStand(PillarFx, at, new Vector2(0.5f * power, height), 0.3, Colors.White, false, null, ("amt", 0.9f));
        Flash(at + Vector3.Up * 0.3f, c, 7f * power);
        Warp(at + Vector3.Up * 0.04f, 3.4f * power, 0.5, 1.2f);
        FxQuad(ScorchFx, at + Vector3.Up * 0.02f, new Vector2(2.0f, 2.0f) * power, 2.0, c, Flat, false, "md2/goldcrack");
        Streaks(at + Vector3.Up * 0.1f, c, (int)(18 * power), 3.6f);
        Streaks(at + Vector3.Up * 0.1f, Colors.White, (int)(8 * power), 2.4f);
    }

    /// A crack pattern that is white-hot at first and cools to a dull glow.
    const string ScorchFx = @"void fragment() {
  vec4 k = texture(tex, UV); float v = max(k.r, max(k.g, k.b));
  float heat = 1.0 - smoothstep(0.0, 0.5, t);
  float r = length(UV * 2.0 - 1.0);
  ALBEDO = mix(col.rgb * 0.7, vec3(1.0), heat * v); ALPHA = clamp(v * (0.5 + heat * 1.2), 0.0, 1.0) * (1.0 - smoothstep(0.6, 1.0, t)) * smoothstep(1.0, 0.7, r);
}";

    // ---------------------------------------------------------------- one-off shapes

    /// A broad sword of light drawn point down: bevelled blade with a fuller and runes, a winged guard with a gem,
    /// a bound grip, a pommel. `amt` = how much of it has formed (0..1, from the hilt down).
    const string SwordFx = @"float sbox(vec2 p, vec2 b) { vec2 d = abs(p) - b; return length(max(d, 0.0)) + min(max(d.x, d.y), 0.0); }
void fragment() {
  vec2 p = (UV * 2.0 - 1.0) * vec2(0.34, 1.0);          // x: -0.34..0.34, y: -1 (hilt end, top) .. 1 (point, bottom)
  float y = p.y; float ax = abs(p.x);
  // blade: from the guard (y = -0.5) to the point (y = 0.98), tapering over the last quarter
  float bw = 0.085 * (1.0 - smoothstep(0.55, 0.98, y)) + 0.004;
  float inBlade = step(-0.5, y) * step(y, 0.98);
  float dBlade = ax - bw;
  float blade = smoothstep(0.006, -0.006, dBlade) * inBlade;
  float bevel = smoothstep(bw, bw * 0.45, ax);                       // bright flats, darker edges
  float fuller = exp(-pow(ax / 0.012, 2.0)) * step(-0.46, y) * step(y, 0.5);
  float runeRow = step(-0.42, y) * step(y, 0.42) * step(ax, 0.035);
  float cell = floor((y + 0.42) * 16.0); float rnd = fract(sin(cell * 12.9898) * 43758.5453);
  vec2 rc = vec2(p.x / 0.035, fract((y + 0.42) * 16.0) * 2.0 - 1.0);
  float rune = runeRow * step(0.25, rnd) * step(abs(rc.y), 0.55) * (step(abs(rc.x), 0.2 + rnd * 0.5) * step(0.3, abs(rc.y) + rnd) + step(abs(abs(rc.x) - 0.6), 0.14));
  float pulse = 0.6 + 0.4 * sin(TIME * 5.0 - y * 9.0);
  // guard: a bar with down-swept wings and a gem
  float wing = sbox(vec2(ax - 0.17, y + 0.52 - ax * 0.35), vec2(0.15, 0.028 + 0.02 * (1.0 - ax / 0.32)));
  float guard = smoothstep(0.006, -0.006, wing);
  float gem = smoothstep(0.045, 0.03, length(vec2(p.x, y + 0.53)));
  // grip and pommel
  float grip = smoothstep(0.006, -0.006, sbox(vec2(p.x, y + 0.72), vec2(0.026, 0.16)));
  float wrap = 0.7 + 0.3 * step(0.5, fract((y + p.x * 1.5) * 22.0));
  float pommel = smoothstep(0.052, 0.04, length(vec2(p.x, y + 0.92)));
  vec3 steel = mix(col.rgb * 0.28, col.rgb * 0.95, bevel) + vec3(fuller) * 0.12 + vec3(1.0) * smoothstep(0.75, 1.0, bevel) * 0.12;
  vec3 c = steel * blade;
  c = mix(c, col2.rgb * (1.2 + pulse), rune * blade);
  c = mix(c, col2.rgb * (0.7 + 0.5 * (1.0 - ax / 0.32)), guard);
  c = mix(c, col2.rgb * 0.55 * wrap, grip * (1.0 - guard));
  c = mix(c, col2.rgb * 0.9, pommel);
  c = mix(c, vec3(0.45, 0.9, 1.0) * (1.3 + 0.5 * pulse), gem);
  float body = max(max(blade, guard), max(grip, pommel));
  float halo = exp(-max(dBlade, 0.0) * 24.0) * inBlade * 0.5;
  float formed = smoothstep(amt * 2.2 - 1.2, amt * 2.2 - 1.0, -y) ;
  float show = step(y, amt * 2.2 - 1.05);
  ALBEDO = c * 0.8 + col.rgb * halo * 0.4; ALPHA = clamp(body + halo * 0.6, 0.0, 1.0) * show * (1.0 - smoothstep(0.9, 1.0, t));
}";

    /// A crescent of wind or light that sweeps through an arc as t runs. amt = which way it curves (1 / -1).
    const string CrescentFx = @"void fragment() {
  vec2 p = UV * 2.0 - 1.0; p.x *= amt;
  float r = length(p - vec2(-0.55, 0.0)); float a = atan(p.y, p.x + 0.55);
  float sweep = mix(-1.5, 1.5, smoothstep(0.0, 0.55, t));
  float along = a - sweep;
  float len = smoothstep(0.0, -0.25, along) * smoothstep(-1.5, -0.4, along);
  float thick = 0.1 * len;
  float edge = smoothstep(thick, thick * 0.2, abs(r - 1.2));
  float n = texture(noise, vec2(a * 0.6 + seed, r * 3.0 - TIME * 2.0)).r;
  float lines = 0.6 + 0.4 * sin((r - 1.2) * 180.0 + n * 6.0);
  ALBEDO = mix(col.rgb, vec3(1.0), edge * 0.7); ALPHA = clamp(edge * len * (0.6 + 0.6 * lines), 0.0, 1.0) * (1.0 - smoothstep(0.6, 1.0, t));
}";

    /// A heart that beats: `amt` beats over its life, each a quick swell.
    const string HeartFx = @"float heart(vec2 p) { p.y = -p.y + 0.25; p.x = abs(p.x); float a = atan(p.x, p.y) / 3.14159; float r = length(p); float h = abs(a); float d = (13.0 * h - 22.0 * h * h + 10.0 * h * h * h) / (6.0 - 5.0 * h); return r - d * 0.62; }
void fragment() {
  float beat = fract(t * amt); float swell = 1.0 + 0.22 * exp(-beat * 9.0) + 0.1 * exp(-pow((beat - 0.28) / 0.07, 2.0));
  vec2 p = (UV * 2.0 - 1.0) / swell;
  float d = heart(p);
  float body = smoothstep(0.02, -0.02, d); float rim = exp(-pow(d / 0.03, 2.0)); float glow = exp(-max(d, 0.0) * 6.0) * 0.45;
  float side = smoothstep(-0.02, 0.02, p.x);
  vec3 c = mix(col.rgb, col2.rgb, side);
  float shine = smoothstep(0.25, 0.0, length(p - vec2(-0.3, -0.25)));
  ALBEDO = c * (0.75 + 0.5 * shine) + vec3(1.0) * rim * 0.5; ALPHA = clamp(body * 0.85 + rim + glow, 0.0, 1.0) * env(0.12, 0.2);
}";

    /// The table's grid sagging into a well: lines of light bending down to a point.
    const string WellFx = @"void fragment() {
  vec2 p = UV * 2.0 - 1.0; float r = length(p); float a = atan(p.y, p.x);
  float depth = env(0.3, 0.25);
  float rr = pow(r, 1.0 - 0.6 * depth);                                  // rings crowd toward the middle as it deepens
  float rings = exp(-pow((fract(rr * 7.0 - TIME * 0.6 * depth) - 0.5) / 0.07, 2.0));
  float spokes = exp(-pow((fract(a / 6.28318 * 24.0 + rr * 1.5 * depth) - 0.5) / 0.06, 2.0));
  float fade = smoothstep(1.0, 0.7, r);
  float core = smoothstep(0.22 * depth, 0.08 * depth, r);
  vec3 c = mix(col.rgb, col2.rgb, smoothstep(0.7, 0.1, r));
  ALBEDO = c * (1.0 - core); ALPHA = clamp((rings + spokes) * fade * depth * (0.35 + 0.9 * (1.0 - r)) + core * depth, 0.0, 1.0);
}";

    /// A disc of nothing spreading over the table: black, a few cold stars in it, a thin bright edge. amt = its
    /// widest reach (0..1 of the quad).
    const string VoidDiscFx = @"void fragment() {
  vec2 p = UV * 2.0 - 1.0; float r = length(p); float a = atan(p.y, p.x) / 6.28318;
  float grow = smoothstep(0.0, 0.45, t) * (1.0 - smoothstep(0.75, 1.0, t));
  float edge = amt * grow * (0.94 + 0.06 * texture(noise, vec2(a * 3.0 + seed, TIME * 0.3)).r);
  float inside = smoothstep(edge, edge - 0.02, r);
  float rim = exp(-pow((r - edge) / 0.018, 2.0)) * step(0.02, grow);
  float haze = exp(-pow((r - edge) / 0.09, 2.0)) * 0.35 * step(0.02, grow);
  vec2 g = floor(UV * 70.0); vec2 gf = fract(UV * 70.0) - 0.5; float star = step(0.985, fract(sin(dot(g, vec2(12.9898, 78.233))) * 43758.5453)) * smoothstep(0.32, 0.0, length(gf)) * (0.5 + 0.5 * sin(TIME * 3.0 + g.x));
  float swirl = pow(texture(noise, vec2(a * 2.0 + 0.35 * log(r + 0.02) - TIME * 0.2, r * 1.5)).r, 3.0) * 0.5;
  ALBEDO = (col.rgb * swirl * 0.6 + vec3(star)) * inside + col2.rgb * (rim * 1.6 + haze);
  ALPHA = clamp(inside * 0.96 + rim + haze, 0.0, 1.0);
}";
}
