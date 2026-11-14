using Godot;
using PoC.Engine;

namespace PoC.Game.UI;

/// Detailed effect pieces for the signature card effects (2026-11-12): everything here is drawn by a shader or built
/// as a real mesh, so it stays sharp at any size (the first round stretched small flat sprites and looked boxy).
/// FxQuad runs a shader on a quad with `t` going 0..1 over its life; the shaders below are its fragment bodies.
public partial class DuelField
{
    static NoiseTexture2D? fxNoise;
    static NoiseTexture2D FxNoise => fxNoise ??= new NoiseTexture2D { Width = 512, Height = 512, Seamless = true, GenerateMipmaps = true,
        Noise = new FastNoiseLite { NoiseType = FastNoiseLite.NoiseTypeEnum.Simplex, Frequency = 0.011f, FractalOctaves = 4, FractalGain = 0.55f } };

    const string FxUniforms = "uniform float t = 0.0; uniform vec4 col : source_color = vec4(1.0); uniform vec4 col2 : source_color = vec4(1.0); uniform float seed = 0.0; uniform float amt = 1.0;\n" +
        "uniform sampler2D noise : repeat_enable, filter_linear_mipmap; uniform sampler2D tex : repeat_disable, filter_linear_mipmap;\n" +
        "float env(float a, float b) { return smoothstep(0.0, a, t) * (1.0 - smoothstep(1.0 - b, 1.0, t)); }\n";
    const string FxAdd = "shader_type spatial;\nrender_mode unshaded, blend_add, cull_disabled, depth_draw_never, shadows_disabled, depth_test_disabled;\n" + FxUniforms;
    const string FxMix = "shader_type spatial;\nrender_mode unshaded, blend_mix, cull_disabled, depth_draw_never, shadows_disabled, depth_test_disabled;\n" + FxUniforms;

    static readonly Basis Flat = new(Vector3.Right, -Mathf.Pi / 2);   // lying on the table, face up
    Basis Bill => cam.GlobalBasis;                                    // standing, facing the camera

    ShaderMaterial FxMat(string frag, Color col, bool mix = false, string? tex = null, params (string, Variant)[] u)
    {
        var m = Shade((mix ? FxMix : FxAdd) + frag, ("col", col), ("noise", FxNoise), ("seed", (float)GD.RandRange(0.0, 10.0)));
        if (tex != null) m.SetShaderParameter("tex", VfxTex(tex));
        foreach (var (k, v) in u) m.SetShaderParameter(k, v);
        return m;
    }

    /// A shader quad that lives `life` seconds (t 0..1) and frees itself.
    MeshInstance3D FxQuad(string frag, Vector3 at, Vector2 size, double life, Color col, Basis? basis = null, bool mix = false, string? tex = null, params (string, Variant)[] u)
    {
        var m = FxMat(frag, col, mix, tex, u);
        var q = new MeshInstance3D { Mesh = new QuadMesh { Size = size }, MaterialOverride = m, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off };
        world.AddChild(q);
        q.Transform = new Transform3D(basis ?? Flat, at);
        Run(q, m, life);
        return q;
    }

    void Run(Node3D n, ShaderMaterial m, double life)
    {
        var tw = n.CreateTween();
        tw.TweenMethod(Callable.From<float>(k => m.SetShaderParameter("t", k)), 0f, 1f, life);
        tw.TweenCallback(Callable.From(n.QueueFree));
    }

    /// A quad standing on `foot`, facing the camera (pillars, bolts, panes).
    MeshInstance3D FxStand(string frag, Vector3 foot, Vector2 size, double life, Color col, bool mix = false, string? tex = null, params (string, Variant)[] u) =>
        FxQuad(frag, foot + Bill.Y * size.Y / 2, size, life, col, Bill, mix, tex, u);

    // ---------------------------------------------------------------- shaders

    /// A spinning disk of light with spiral arms and a bright inner edge (black hole, whirlpools). amt = spin speed.
    const string VortexFx = @"void fragment() {
  vec2 p = UV * 2.0 - 1.0; float r = length(p); float a = atan(p.y, p.x) / 6.28318;
  float sw = a + 0.42 * log(r + 0.02) - TIME * 0.35 * amt;
  float arms = texture(noise, vec2(sw * 2.0, r * 1.3 - TIME * 0.25)).r;
  float fine = texture(noise, vec2(sw * 7.0 + 0.3, r * 3.5 - TIME * 0.6)).r;
  float disk = smoothstep(1.0, 0.55, r) * smoothstep(0.17, 0.3, r);
  float rim = exp(-pow((r - 0.235) / 0.035, 2.0));
  float v = disk * (0.12 + 2.2 * pow(arms, 2.5) + 1.3 * pow(fine, 4.0)) + rim * 1.8;
  ALBEDO = mix(col.rgb, col2.rgb, clamp(rim + pow(arms, 3.0) * 1.4, 0.0, 1.0));
  ALPHA = clamp(v, 0.0, 1.0) * env(0.25, 0.2);
}";

    /// The lightless middle of the vortex (drawn over everything under it).
    const string CoreFx = @"void fragment() {
  float r = length(UV * 2.0 - 1.0);
  ALBEDO = vec3(0.0); ALPHA = smoothstep(1.0, 0.8, r) * env(0.25, 0.2);
}";

    /// A jagged bolt along the quad's height that re-forks many times a second. The strike point is the bottom.
    const string BoltFx = @"void fragment() {
  float y = UV.y; float st = floor(TIME * 18.0);
  float n1 = texture(noise, vec2(y * 0.8 + seed, st * 0.173 + seed)).r - 0.5;
  float n2 = texture(noise, vec2(y * 3.1 + seed * 2.0, st * 0.311)).r - 0.5;
  float n3 = texture(noise, vec2(y * 9.0, st * 0.53 + seed)).r - 0.5;
  float loose = 0.15 + 0.85 * (1.0 - y);
  float cx = 0.5 + (n1 * 0.55 + n2 * 0.16 + n3 * 0.05) * loose;
  float d = abs(UV.x - cx);
  float bx = 0.5 + (n2 * 0.9 + n1 * 0.3) * loose + (y - 0.45) * 0.5;
  float fork = step(0.25, y) * step(y, 0.7) * 0.004 / (abs(UV.x - bx) + 0.004);
  float core = 0.007 / (d + 0.0035);
  float glow = exp(-d * 14.0) * 0.6;
  float reach = step(1.0 - smoothstep(0.0, 0.12, t), y + 0.001);
  float v = (core + glow + fork * 0.5) * reach * (1.0 - smoothstep(0.55, 1.0, t));
  ALBEDO = mix(col.rgb, vec3(1.0), clamp(core * 0.3, 0.0, 1.0));
  ALPHA = clamp(v, 0.0, 1.0) * smoothstep(0.0, 0.05, UV.x) * smoothstep(1.0, 0.95, UV.x);
}";

    /// A column of light with streaks running up it.
    const string PillarFx = @"void fragment() {
  float x = abs(UV.x - 0.5) * 2.0;
  float s1 = texture(noise, vec2(UV.x * 2.0 + seed, UV.y * 0.5 + TIME * 1.4)).r;
  float s2 = texture(noise, vec2(UV.x * 6.0, UV.y * 0.9 + TIME * 2.3)).r;
  float body = pow(max(0.0, 1.0 - x), 2.2) * (0.35 + 1.3 * s1 * s1 + 0.7 * pow(s2, 3.0));
  float core = exp(-x * x * 40.0) * 0.9;
  float v = (body + core) * smoothstep(0.0, 0.35, UV.y) * env(0.2, 0.45);
  ALBEDO = mix(col.rgb, vec3(1.0), clamp(core + s2 * s2 * 0.4, 0.0, 1.0)); ALPHA = clamp(v * amt, 0.0, 1.0);
}";

    /// A ring that runs outward with a ragged, sparkling edge.
    const string RingFx = @"void fragment() {
  vec2 p = UV * 2.0 - 1.0; float r = length(p); float a = atan(p.y, p.x) / 6.28318;
  float rad = mix(0.08, 0.92, 1.0 - pow(1.0 - t, 3.0));
  float n = texture(noise, vec2(a * 6.0 + seed, r * 2.0 - t)).r;
  float w = 0.035 + 0.05 * n;
  float ring = exp(-pow((r - rad) / w, 2.0));
  float inner = smoothstep(rad, rad - 0.35, r) * 0.25 * n;
  ALBEDO = mix(col.rgb, vec3(1.0), ring * 0.6); ALPHA = clamp((ring * 1.4 + inner) * (1.0 - t * t), 0.0, 1.0);
}";

    /// A pane of hexagons: the cells light up in a ripple from the middle. (Barriers, walls.)
    const string HexFx = @"uniform vec2 cells = vec2(14.0, 6.0);
float hexd(vec2 p) { p = abs(p); return max(dot(p, vec2(0.5, 0.866025)), p.x); }
void fragment() {
  vec2 uv = UV * cells; vec2 rr = vec2(1.0, 1.7320508); vec2 h = rr * 0.5;
  vec2 a = mod(uv, rr) - h; vec2 b = mod(uv - h, rr) - h; vec2 gv = dot(a, a) < dot(b, b) ? a : b;
  vec2 id = uv - gv;
  float d = hexd(gv); float edge = smoothstep(0.40, 0.49, d);
  float dist = length((id / cells - 0.5) * vec2(2.0, 1.0));
  float ripple = 0.5 + 0.5 * sin(dist * 14.0 - TIME * 9.0);
  float lit = texture(noise, id * 0.13 + TIME * 0.05).r;
  float fill = (1.0 - edge) * (0.08 + 0.35 * pow(lit, 3.0) + 0.3 * pow(ripple, 6.0));
  vec2 q = abs(UV - 0.5) * 2.0; float frame = smoothstep(1.0, 0.75, q.x) * smoothstep(1.0, 0.6, q.y);
  float sweep = exp(-pow((UV.x + UV.y * 0.3 - (t * 2.2 - 0.4)) / 0.07, 2.0));
  ALBEDO = mix(col.rgb, vec3(1.0), clamp(edge * 0.5 + sweep, 0.0, 1.0));
  ALPHA = clamp((edge * 0.9 + fill + sweep * 0.8) * frame * amt, 0.0, 1.0) * env(0.12, 0.3);
}";

    /// A magic circle: two rings, a band of marks turning between them, a six-pointed star, turning slowly.
    const string CircleFx = @"float seg(vec2 p, float n, float rot, float r0) { float a = atan(p.y, p.x) + rot; float k = 6.28318 / n; a = mod(a, k) - k * 0.5; return abs(length(p) * cos(a) - r0); }
void fragment() {
  vec2 p = UV * 2.0 - 1.0; float r = length(p); float ang = atan(p.y, p.x);
  float spin = TIME * 0.5 * amt;
  float rings = exp(-pow((r - 0.93) / 0.012, 2.0)) + exp(-pow((r - 0.74) / 0.01, 2.0)) + exp(-pow((r - 0.36) / 0.008, 2.0));
  float band = step(0.76, r) * step(r, 0.91);
  float marks = band * step(0.45, fract((ang + spin) * 18.0 / 6.28318)) * step(0.3, texture(noise, vec2(floor((ang + spin) * 18.0 / 6.28318) * 0.21, 0.3)).r) * smoothstep(0.0, 0.05, abs(r - 0.835) - 0.02) ;
  float star = min(seg(p, 3.0, -spin, 0.37), seg(p, 3.0, -spin + 3.14159, 0.37));
  float lines = exp(-pow(star / 0.008, 2.0)) * step(r, 0.74);
  float spokes = exp(-pow(seg(p, 12.0, spin * 0.5, 0.0) / 0.004, 2.0)) * step(0.36, r) * step(r, 0.74) * 0.0;
  float v = rings + marks * 0.7 + lines + spokes;
  float draw = step(fract(ang / 6.28318 + 0.5), smoothstep(0.0, 0.3, t) * 1.01);
  ALBEDO = mix(col.rgb, vec3(1.0), clamp(v * 0.35, 0.0, 1.0)); ALPHA = clamp(v, 0.0, 1.0) * draw * (1.0 - smoothstep(0.8, 1.0, t));
}";

    /// Soft flames licking upward from the bottom edge.
    const string FireFx = @"void fragment() {
  float x = abs(UV.x - 0.5) * 2.0; float y = 1.0 - UV.y;
  float n1 = texture(noise, vec2(UV.x * 1.6 + seed, UV.y * 1.2 + TIME * 1.3)).r;
  float n2 = texture(noise, vec2(UV.x * 4.0, UV.y * 2.5 + TIME * 2.4 + seed)).r;
  float tongue = texture(noise, vec2(UV.x * 2.5 + seed, TIME * 0.9)).r;
  float shape = (1.0 - x * x) * (1.0 - y * (1.0 + tongue * 0.9)) - 0.32 + (n1 - 0.5) * 1.0 + (n2 - 0.5) * 0.45;
  float f = smoothstep(0.0, 0.5, shape) * smoothstep(1.0, 0.45, x) * smoothstep(0.0, 0.1, y);
  ALBEDO = mix(col.rgb, col2.rgb, smoothstep(0.55, 1.0, f) * (1.0 - y));
  ALPHA = f * 0.8 * env(0.2, 0.4) * amt;
}";

    /// A texture drawn as light, turning (`amt` turns a second) and fading in and out.
    const string SpinTexFx = @"void fragment() {
  vec2 p = UV - 0.5; float a = TIME * 6.28318 * amt + seed; mat2 m = mat2(vec2(cos(a), -sin(a)), vec2(sin(a), cos(a)));
  vec4 c = texture(tex, m * p + 0.5);
  float v = max(c.r, max(c.g, c.b));
  ALBEDO = mix(col.rgb, vec3(1.0), v * v * 0.6); ALPHA = v * env(0.15, 0.35) * step(length(p), 0.5);
}";

    /// A texture as it is (its own colours over what is behind it), fading in and out.
    const string TexMixFx = @"void fragment() {
  vec4 c = texture(tex, UV);
  ALBEDO = c.rgb * col.rgb; ALPHA = c.a * col.a * env(0.15, 0.3);
}";

    /// A texture whose brightness is the light (white on black art), tinted.
    const string TexAddFx = @"void fragment() {
  vec4 c = texture(tex, UV); float v = max(c.r, max(c.g, c.b));
  ALBEDO = mix(col.rgb, vec3(1.0), v * v * 0.5); ALPHA = v * env(0.1, 0.4) * amt;
}";

    /// A feather: a vane with a shaft and fine barbs, drawn by distance (sharp at any size).
    const string FeatherFx = @"void fragment() {
  vec2 p = UV * 2.0 - 1.0; float y = p.y * 0.5 + 0.5;
  float w = 0.62 * pow(sin(clamp(y, 0.0, 1.0) * 3.14159), 0.6) * (0.6 + 0.4 * y);
  float bend = p.x - 0.12 * sin(y * 2.4);
  float vane = smoothstep(w, w - 0.05, abs(bend)) * step(0.06, y);
  float barbs = 0.72 + 0.28 * sin((y * 1.0 + abs(bend) * 1.3) * 90.0);
  float shaft = exp(-pow(bend / 0.018, 2.0)) * step(-0.95, p.y);
  float v = vane * barbs * (0.75 + 0.25 * (1.0 - abs(bend) / max(w, 0.01))) + shaft * 0.6;
  ALBEDO = mix(col2.rgb, col.rgb, clamp(abs(bend) / 0.6, 0.0, 1.0)); ALPHA = clamp(v, 0.0, 1.0) * env(0.1, 0.25);
}";

    /// A wing: the texture's shape, with rows of feather lines drawn into it and a lit edge.
    const string WingFx = @"void fragment() {
  vec4 c = texture(tex, UV); float v = max(c.r, max(c.g, c.b));
  float e = 0.004; float gx = texture(tex, UV + vec2(e, 0.0)).r - texture(tex, UV - vec2(e, 0.0)).r; float gy = texture(tex, UV + vec2(0.0, e)).r - texture(tex, UV - vec2(0.0, e)).r;
  float edge = clamp(length(vec2(gx, gy)) * 6.0, 0.0, 1.0);
  float rows = 0.55 + 0.45 * sin((UV.x * 0.9 + UV.y * 1.6) * 70.0 + texture(noise, UV * 2.0).r * 6.0);
  float veins = 0.7 + 0.3 * sin((UV.x * 1.7 - UV.y * 0.6) * 160.0);
  float body = smoothstep(0.35, 0.8, v) * rows * veins;
  ALBEDO = mix(col.rgb, vec3(1.0), clamp(edge + body * 0.35, 0.0, 1.0)); ALPHA = clamp(body * 0.75 + edge * 0.9 + v * 0.12, 0.0, 1.0) * env(0.12, 0.3);
}";

    /// An ankh of light: loop, crossbar and stem.
    const string AnkhFx = @"float box(vec2 p, vec2 b) { vec2 d = abs(p) - b; return length(max(d, 0.0)) + min(max(d.x, d.y), 0.0); }
void fragment() {
  vec2 p = (UV * 2.0 - 1.0) * vec2(1.0, -1.0);
  vec2 lp = (p - vec2(0.0, 0.42)) / vec2(0.62, 1.0);
  float loop = abs(length(lp) - 0.36) - 0.07;
  float stem = box(p - vec2(0.0, -0.42), vec2(0.075, 0.5));
  float bar = box(p - vec2(0.0, 0.0), vec2(0.42, 0.075));
  float d = min(loop, min(stem, bar));
  float body = smoothstep(0.012, -0.012, d);
  float glow = exp(-max(d, 0.0) * 9.0) * 0.55;
  float shimmer = 0.75 + 0.25 * texture(noise, UV * 1.5 + vec2(0.0, TIME * 0.4)).r;
  ALBEDO = mix(col.rgb, vec3(1.0), body * 0.7); ALPHA = clamp(body * shimmer + glow, 0.0, 1.0) * env(0.2, 0.3);
}";

    /// A clock face: rim, twelve marks and one hand at `amt` turns.
    const string ClockFx = @"void fragment() {
  vec2 p = UV * 2.0 - 1.0; float r = length(p); float a = atan(p.x, -p.y);
  float rim = exp(-pow((r - 0.92) / 0.02, 2.0)) + exp(-pow((r - 0.8) / 0.008, 2.0));
  float k = 6.28318 / 12.0; float am = mod(a + k * 0.5, k) - k * 0.5;
  float ticks = exp(-pow(am * r / 0.02, 2.0)) * step(0.62, r) * step(r, 0.78);
  float ha = amt * 6.28318; vec2 hd = vec2(sin(ha), -cos(ha)); float along = dot(p, hd); float off = abs(dot(p, vec2(hd.y, -hd.x)));
  float hand = exp(-pow(off / (0.035 * (1.0 - along * 0.8)), 2.0)) * step(0.0, along) * step(along, 0.7);
  float hub = smoothstep(0.08, 0.06, r);
  float v = rim + ticks + hand * 1.2 + hub;
  ALBEDO = mix(col.rgb, vec3(1.0), clamp(hand + hub, 0.0, 1.0) * 0.7); ALPHA = clamp(v, 0.0, 1.0) * env(0.15, 0.25);
}";

    /// A wall of water running across the quad (left to right with t), foam on its crest.
    const string WaveFx = @"void fragment() {
  float front = t * 1.5 - 0.25;
  float n = texture(noise, vec2(UV.y * 0.5 + seed, TIME * 0.25)).r;
  float x = UV.x - front + (n - 0.5) * 0.1;
  float body = smoothstep(0.0, -0.08, x) * smoothstep(-0.55, -0.15, x);
  float foam = exp(-pow(x / 0.055, 2.0)) * (0.6 + 0.6 * texture(noise, vec2(UV.y * 4.0, TIME * 1.2)).r) + 0.6 * pow(texture(noise, vec2(UV.x * 5.0 - TIME * 1.5, UV.y * 5.0)).r, 3.0) * body;
  float caust = pow(texture(noise, vec2(UV.x * 3.0 - TIME * 0.8, UV.y * 3.0 + TIME * 0.2)).r, 2.0);
  float edge = smoothstep(0.0, 0.18, UV.y) * smoothstep(1.0, 0.82, UV.y) * smoothstep(0.0, 0.08, UV.x) * smoothstep(1.0, 0.92, UV.x);
  ALBEDO = mix(col.rgb * (0.5 + caust), vec3(1.0), clamp(foam, 0.0, 1.0));
  ALPHA = clamp(body * 0.75 + foam, 0.0, 1.0) * edge * (1.0 - smoothstep(0.85, 1.0, t));
}";

    /// A dark hole in the table: a ragged black mouth with a glowing lip.
    const string PitFx = @"void fragment() {
  vec2 p = UV * 2.0 - 1.0; float r = length(p); float a = atan(p.y, p.x) / 6.28318;
  float open = env(0.3, 0.3);
  float rag = texture(noise, vec2(a * 4.0 + seed, 0.3)).r * 0.18;
  float edge = (0.72 + rag) * open;
  float hole = smoothstep(edge, edge - 0.06, r);
  float lip = exp(-pow((r - edge) / 0.05, 2.0)) * open;
  float depth = smoothstep(0.0, edge, r);
  ALBEDO = mix(vec3(0.0), col.rgb * 0.5, depth * depth * 0.5) * hole + col.rgb * lip * 1.4;
  ALPHA = clamp(hole + lip, 0.0, 1.0);
}";

    /// A streak of wind: a soft ribbon that runs along the quad's length.
    const string GustFx = @"void fragment() {
  float head = t * 1.6 - 0.3;
  float n = texture(noise, vec2(UV.x * 2.0 - TIME * 2.0 + seed, UV.y * 0.7 + seed)).r;
  float band = exp(-pow((UV.y - 0.5 + (n - 0.5) * 0.35) / 0.1, 2.0));
  float lines = pow(texture(noise, vec2(UV.x * 0.6 - TIME * 1.2, UV.y * 9.0 + seed)).r, 2.0);
  float trail = smoothstep(head, head - 0.5, UV.x) * smoothstep(head - 0.9, head - 0.5, UV.x);
  ALBEDO = col.rgb; ALPHA = clamp(band * (0.35 + lines * 1.3) * trail * amt, 0.0, 1.0);
}";

    /// A beam of energy along the quad's length, drawn from its left end with t.
    const string BeamFx = @"void fragment() {
  float y = abs(UV.y - 0.5) * 2.0;
  float n = texture(noise, vec2(UV.x * 1.2 - TIME * 3.0 + seed, UV.y * 0.6)).r; float n2 = texture(noise, vec2(UV.x * 4.0 - TIME * 5.0, UV.y * 2.0 + seed)).r;
  float core = exp(-y * y * 60.0); float body = exp(-y * y * 7.0) * (0.3 + 0.9 * n * n + 0.5 * pow(n2, 3.0));
  float reach = smoothstep(0.0, 0.03, min(1.0, t * 4.0) - UV.x);
  ALBEDO = mix(col.rgb, vec3(1.0), clamp(core, 0.0, 1.0)); ALPHA = clamp((core + body) * reach * (1.0 - smoothstep(0.6, 1.0, t)) * amt, 0.0, 1.0);
}";

    /// A beam lying over the table from a to b.
    MeshInstance3D Beam(Vector3 a, Vector3 b, float width, double life, Color c)
    {
        var d = b - a; var mid = (a + b) / 2;
        return FxQuad(BeamFx, mid, new Vector2(d.Length(), width), life, c, new Basis(Vector3.Up, Mathf.Atan2(-d.Z, d.X)) * Flat);
    }

    // ---------------------------------------------------------------- meshes

    /// Light that looks solid: an unlit colour above the bloom threshold, so the field's glow haloes it.
    static StandardMaterial3D Lit(Color c, float energy = 2.2f) => new()
    {
        ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded, AlbedoColor = new Color(c.R * energy, c.G * energy, c.B * energy), DisableReceiveShadows = true,
    };

    /// A sword of light, point down, its tip at the node's origin. `broad` = the heavy blade.
    Node3D LightSword(Color blade, Color trim, bool broad)
    {
        var root = new Node3D();
        float len = broad ? 1.25f : 1.35f, w = broad ? 0.17f : 0.085f, tip = broad ? 0.22f : 0.3f;
        void Part(Mesh mesh, Vector3 at, Material m, Vector3? rot = null) => root.AddChild(new MeshInstance3D { Mesh = mesh, Position = at, RotationDegrees = rot ?? Vector3.Zero, MaterialOverride = m, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off });
        var bladeM = Lit(blade, 1.15f); var trimM = Lit(trim * new Color(1f, 0.8f, 0.45f), 0.95f);
        Part(new PrismMesh { Size = new Vector3(w, tip, 0.03f) }, new Vector3(0, tip / 2, 0), bladeM, new Vector3(0, 0, 180));               // the point
        Part(new BoxMesh { Size = new Vector3(w, len - tip, 0.03f) }, new Vector3(0, tip + (len - tip) / 2, 0), bladeM);                    // the blade
        Part(new BoxMesh { Size = new Vector3(w * 0.22f, len - tip - 0.1f, 0.045f) }, new Vector3(0, tip + (len - tip) / 2, 0), trimM);     // the ridge down its middle
        float gy = len + 0.03f;
        Part(new BoxMesh { Size = new Vector3(broad ? 0.62f : 0.46f, 0.06f, 0.07f) }, new Vector3(0, gy, 0), trimM);                         // the guard
        foreach (int s in new[] { -1, 1 }) Part(new PrismMesh { Size = new Vector3(0.1f, 0.14f, 0.07f) }, new Vector3(s * (broad ? 0.33f : 0.25f), gy - 0.05f, 0), trimM, new Vector3(0, 0, 180));
        Part(new CylinderMesh { TopRadius = 0.03f, BottomRadius = 0.035f, Height = 0.3f, RadialSegments = 12 }, new Vector3(0, gy + 0.18f, 0), bladeM);   // the grip
        Part(new SphereMesh { Radius = 0.065f, Height = 0.13f, RadialSegments = 16, Rings = 8 }, new Vector3(0, gy + 0.37f, 0), trimM);                    // the pommel
        if (broad) Part(new SphereMesh { Radius = 0.07f, Height = 0.14f, RadialSegments = 16, Rings = 8 }, new Vector3(0, gy, 0.02f), Lit(new Color(0.3f, 0.75f, 1f), 1.3f));   // a gem in the guard
        // the halo round the blade
        var halo = new MeshInstance3D { Mesh = new QuadMesh { Size = new Vector2(1.0f, len + 1.0f) }, Position = new Vector3(0, len / 2 + 0.1f, -0.02f), CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            MaterialOverride = FxMat(@"void fragment() {
  vec2 p = UV * 2.0 - 1.0; float n = texture(noise, vec2(UV.x * 1.5 + seed, UV.y * 0.8 - TIME * 0.5)).r;
  float g = exp(-p.x * p.x * 30.0) * smoothstep(1.0, 0.5, abs(p.y)) * (0.12 + 0.22 * n);
  ALBEDO = col.rgb; ALPHA = clamp(g * amt, 0.0, 1.0);
}", blade) };
        root.AddChild(halo);
        world.AddChild(root);
        return root;
    }

    /// A funnel of wind: two turning skins, wide at the top, with streaks running round and up them.
    Node3D Tornado(Vector3 foot, float height, float radius, Color col, double life)
    {
        var root = new Node3D { Position = foot };
        world.AddChild(root);
        for (int layer = 0; layer < 2; layer++)
        {
            var m = Shade(@"shader_type spatial;
render_mode unshaded, blend_add, cull_disabled, depth_draw_never, shadows_disabled;
uniform float t = 0.0; uniform vec4 col : source_color; uniform float dir = 1.0; uniform sampler2D noise : repeat_enable, filter_linear_mipmap; uniform sampler2D tex : repeat_enable, filter_linear_mipmap;
void vertex() { float h = UV.y; VERTEX.x += sin(TIME * 5.0 + VERTEX.y * 2.5) * 0.07 * (1.0 - h); VERTEX.z += cos(TIME * 4.3 + VERTEX.y * 2.1) * 0.07 * (1.0 - h); }
void fragment() {
  vec2 uv = vec2(UV.x * 2.0 + TIME * 1.1 * dir + UV.y * 0.9, UV.y * 0.9 - TIME * 0.5);
  float a = texture(noise, uv).r; float b = texture(noise, uv * vec2(3.0, 2.0) + 0.37).r;
  float streak = pow(a, 2.0) * 1.6 + pow(b, 4.0) * 1.2;
  float rimlight = pow(1.0 - abs(dot(normalize(NORMAL), normalize(VIEW))), 1.5);
  float e = smoothstep(0.0, 0.2, t) * (1.0 - smoothstep(0.75, 1.0, t));
  ALBEDO = mix(col.rgb, vec3(1.0), clamp(streak * 0.4, 0.0, 1.0));
  ALPHA = clamp((streak * 0.55 + rimlight * 0.45) * smoothstep(1.0, 0.8, UV.y) * smoothstep(0.0, 0.1, UV.y) * e, 0.0, 1.0);
}", ("col", col), ("noise", FxNoise), ("dir", layer == 0 ? 1f : -1.6f));
            float k = layer == 0 ? 1f : 0.62f;
            var mi = new MeshInstance3D { Mesh = new CylinderMesh { TopRadius = radius * k, BottomRadius = radius * 0.16f * k, Height = height, RadialSegments = 40, Rings = 12, CapTop = false, CapBottom = false },
                Position = new Vector3(0, height / 2, 0), MaterialOverride = m, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off };
            root.AddChild(mi);
            var tw = mi.CreateTween();
            tw.TweenMethod(Callable.From<float>(v => m.SetShaderParameter("t", v)), 0f, 1f, life);
        }
        After(life + 0.05, () => { if (IsInstanceValid(root)) root.QueueFree(); });
        return root;
    }

    /// A ball with nothing in it and a lit edge (the heart of a black hole).
    MeshInstance3D DarkBall(Vector3 at, float radius, Color rim, double life)
    {
        var m = Shade(@"shader_type spatial;
render_mode unshaded, blend_mix, depth_draw_never, shadows_disabled;
uniform float t = 0.0; uniform vec4 col : source_color;
void fragment() {
  float f = pow(1.0 - abs(dot(normalize(NORMAL), normalize(VIEW))), 3.0);
  ALBEDO = col.rgb * f * 3.0; ALPHA = smoothstep(0.0, 0.15, t) * (1.0 - smoothstep(0.9, 1.0, t));
}", ("col", rim));
        var q = new MeshInstance3D { Mesh = new SphereMesh { Radius = 1, Height = 2, RadialSegments = 48, Rings = 24 }, Position = at, Scale = Vector3.One * 0.01f, MaterialOverride = m, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off };
        world.AddChild(q);
        Run(q, m, life);
        q.CreateTween().TweenProperty(q, "scale", Vector3.One * radius, life * 0.3).SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
        return q;
    }

    /// A pane of glass standing across the field that breaks into shards flying toward `to`. Returns the delay until it breaks.
    void GlassBreak(Vector3 centre, float width, float height, Color col, Vector3 to, int cols = 12, int rows = 5)
    {
        var rng = new Random();
        var basis = Bill;
        var pts = new Vector3[cols + 1, rows + 1];
        for (int i = 0; i <= cols; i++)
            for (int j = 0; j <= rows; j++)
            {
                float jx = i is 0 || i == cols ? 0 : (float)(rng.NextDouble() - 0.5) * 0.7f, jy = j is 0 || j == rows ? 0 : (float)(rng.NextDouble() - 0.5) * 0.7f;
                pts[i, j] = new Vector3((i + jx) / cols * width - width / 2, (j + jy) / rows * height - height / 2, 0);
            }
        for (int i = 0; i < cols; i++)
            for (int j = 0; j < rows; j++)
                foreach (var tri in new[] { new[] { pts[i, j], pts[i + 1, j], pts[i, j + 1] }, new[] { pts[i + 1, j], pts[i + 1, j + 1], pts[i, j + 1] } })
                {
                    var mid = (tri[0] + tri[1] + tri[2]) / 3;
                    var st = new SurfaceTool(); st.Begin(Mesh.PrimitiveType.Triangles);
                    foreach (var v in tri) st.AddVertex(v - mid);
                    float shade = 0.55f + (float)rng.NextDouble() * 0.9f;
                    var mat = new StandardMaterial3D { ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded, Transparency = BaseMaterial3D.TransparencyEnum.Alpha, BlendMode = BaseMaterial3D.BlendModeEnum.Add,
                        AlbedoColor = new Color(col.R * shade, col.G * shade, col.B * shade, 0.85f), CullMode = BaseMaterial3D.CullModeEnum.Disabled };
                    var mi = new MeshInstance3D { Mesh = st.Commit(), MaterialOverride = mat, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off };
                    world.AddChild(mi);
                    var from = centre + basis * mid;
                    mi.Transform = new Transform3D(basis, from);
                    var dir = (to - from).Normalized() + new Vector3((float)(rng.NextDouble() - 0.5), (float)rng.NextDouble() * 0.6f, (float)(rng.NextDouble() - 0.5)) * 0.9f;
                    var axis = new Vector3((float)rng.NextDouble() - 0.5f, (float)rng.NextDouble() - 0.5f, (float)rng.NextDouble() - 0.5f).Normalized();
                    float spin = 4 + (float)rng.NextDouble() * 10, dist = 2.2f + (float)rng.NextDouble() * 2.6f; double d = 0.55 + rng.NextDouble() * 0.5;
                    var tw = mi.CreateTween();
                    tw.TweenMethod(Callable.From<float>(k =>
                    {
                        float e = 1 - (1 - k) * (1 - k);
                        mi.Transform = new Transform3D(new Basis(axis, k * spin) * basis, from + dir * dist * e + Vector3.Down * k * k * 0.9f);
                        mat.AlbedoColor = mat.AlbedoColor with { A = 0.85f * (1 - k * k) };
                    }), 0f, 1f, d);
                    tw.TweenCallback(Callable.From(mi.QueueFree));
                }
    }

    /// Sparks thrown out from a point: small bright streaks that fall and fade.
    void Sparks(Vector3 at, Color c, int n, float speed = 2.2f)
    {
        for (int i = 0; i < n; i++)
        {
            var dir = new Vector3((float)GD.RandRange(-1.0, 1.0), (float)GD.RandRange(0.3, 1.4), (float)GD.RandRange(-1.0, 1.0)).Normalized() * speed * (float)GD.RandRange(0.4, 1.0);
            var m = Additive(SoftDot, c, billboard: true);
            float s = (float)GD.RandRange(0.03, 0.08);
            var q = new MeshInstance3D { Mesh = new QuadMesh { Size = new Vector2(s, s) }, MaterialOverride = m, Position = at, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off };
            world.AddChild(q);
            double d = GD.RandRange(0.35, 0.8);
            var tw = q.CreateTween();
            tw.TweenMethod(Callable.From<float>(k => { q.Position = at + dir * k * (float)d + Vector3.Down * k * k * 1.1f; m.AlbedoColor = new Color(c, 1 - k * k); }), 0f, 1f, d);
            tw.TweenCallback(Callable.From(q.QueueFree));
        }
    }

    /// A feather that drifts from `a` to `b`, rocking as it falls.
    void Feather(Vector3 a, Vector3 b, Color c, double dur, float size = 0.9f)
    {
        var q = FxQuad(FeatherFx, a, new Vector2(size * 0.5f, size), dur, c, Bill, false, null, ("col2", new Color(1, 1, 1)));
        float ph = (float)GD.RandRange(0.0, 6.28), sway = (float)GD.RandRange(0.2, 0.5);
        q.CreateTween().TweenMethod(Callable.From<float>(k =>
        {
            var p = a.Lerp(b, k) + Bill.X * Mathf.Sin(ph + k * 9f) * sway * (1 - k);
            q.Transform = new Transform3D(Bill * new Basis(Vector3.Back, Mathf.Sin(ph + k * 9f) * 0.7f + 2.6f), p);
        }), 0f, 1f, dur);
    }

    /// Lightning from the sky onto a point: the bolt, a flash, a scorch of cracks, sparks.
    void Strike(Vector3 at, Color c, float height = 5.5f, bool crack = true)
    {
        FxStand(BoltFx, at, new Vector2(2.4f, height), 0.5, c);
        FxStand(BoltFx, at, new Vector2(1.6f, height), 0.38, Colors.White);
        Flash(at + Vector3.Up * 0.3f, c, 5f);
        FxQuad(RingFx, at + Vector3.Up * 0.03f, new Vector2(2.6f, 2.6f), 0.5, c);
        if (crack) FxQuad(TexAddFx, at + Vector3.Up * 0.02f, new Vector2(1.9f, 1.9f), 1.3, c, Flat, false, "md2/goldcrack");
        Sparks(at + Vector3.Up * 0.1f, c, 14);
    }
}
