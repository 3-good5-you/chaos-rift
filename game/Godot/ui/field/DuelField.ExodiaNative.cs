using System.Text.Json.Nodes;
using Godot;

namespace PoC.Game.UI;

/// B, native build of the Master Duel Exodia finish (see DuelField.ExodiaMd.cs): the web rebuild
/// (remake/samples/md_exodia/full.html) ported to Godot. Same data (assets/exodia/md/data.json: MD's timeline clips,
/// particle emitters and materials), same textures, same camera and layer order; only Exodia himself is baked
/// (x_###.webp, transparent, full colour, from `__bakeSpine()`, with bones.json for the eye and hands), because his Spine
/// rig needs a Spine runtime. Silhouette, rise, fire walls, flames, explosion, eye flash and the power ball are drawn here.
public partial class DuelField
{
    const string MdDir = "res://assets/exodia/md/";
    const double MdEnd = 9.6;   // WinStart: the white flash takes over (shared with the film)

    // ---------------------------------------------------------------- data

    sealed class MdCurves
    {
        readonly Dictionary<string, Dictionary<string, float[][]>> clips = new();
        public MdCurves(JsonNode root)
        {
            foreach (var (k, v) in root["clips"]!.AsObject())
                clips[k] = v!["c"]!.AsObject().ToDictionary(c => c.Key, c => c.Value!.AsArray().Select(p => new[] { (float)p![0]!, (float)p[1]! }).ToArray());
        }
        public float this[string clip, string key, double t, float def = 0] => clips[clip].TryGetValue(key, out var a) ? Eval(a, (float)t, def) : def;
        public static float Eval(float[][] a, float t, float def = 0)
        {
            if (a.Length == 0) return def;
            if (t <= a[0][0]) return a[0][1];
            for (int i = 1; i < a.Length; i++)
                if (t <= a[i][0]) { var (p, q) = (a[i - 1], a[i]); return Mathf.Lerp(p[1], q[1], (t - p[0]) / Math.Max(1e-6f, q[0] - p[0])); }
            return a[^1][1];
        }
    }

    // Unity MinMaxCurve / MinMaxGradient as dumped by the extractor (see full.html mm / mmAt / gradAt / startColor).
    static float[][] Keys(JsonNode n) => n.AsArray().Select(p => new[] { (float)p![0]!, (float)p[1]! }).ToArray();
    static float MmAt(JsonNode? v, float x, float r)
    {
        switch (v)
        {
            case null: return 0;
            case JsonValue: return (float)v;
            case JsonArray a: return Mathf.Lerp((float)a[0]!, (float)a[1]!, r);
        }
        float k = (float)v["k"]!;
        if (v["curve"] is { } c) return k * MdCurves.Eval(Keys(c), x);
        return k * Mathf.Lerp(MdCurves.Eval(Keys(v["min"]!), x), MdCurves.Eval(Keys(v["max"]!), x), r);
    }
    static Color GradAt(JsonNode g, float x)
    {
        var c = g["c"]!.AsArray(); var a = g["a"]!.AsArray();
        Vector3 col = Vector3.One; float al = 1;
        for (int i = 0; i < c.Count; i++)
        {
            float ti = (float)c[i]![3]!;
            if (x <= ti || i == c.Count - 1)
            {
                Vector3 C(int j) => new((float)c[j]![0]!, (float)c[j]![1]!, (float)c[j]![2]!);
                if (i == 0) { col = C(0); break; }
                float tp = (float)c[i - 1]![3]!; col = C(i - 1).Lerp(C(i), Mathf.Clamp((x - tp) / Math.Max(1e-6f, ti - tp), 0, 1)); break;
            }
        }
        for (int i = 0; i < a.Count; i++)
        {
            float ti = (float)a[i]![1]!;
            if (x <= ti || i == a.Count - 1)
            {
                if (i == 0) { al = (float)a[0]![0]!; break; }
                float tp = (float)a[i - 1]![1]!; al = Mathf.Lerp((float)a[i - 1]![0]!, (float)a[i]![0]!, Mathf.Clamp((x - tp) / Math.Max(1e-6f, ti - tp), 0, 1)); break;
            }
        }
        return new Color(col.X, col.Y, col.Z, al);
    }
    static Color StartColor(JsonNode c, float r)
    {
        static Color Of(JsonArray a) => new((float)a[0]!, (float)a[1]!, (float)a[2]!, (float)a[3]!);
        if (c is JsonArray a)
        {
            if (a[0] is JsonValue) return Of(a);
            if (a[0] is JsonArray) return Of(a[0]!.AsArray()).Lerp(Of(a[1]!.AsArray()), r);
            return GradAt(a[1]!, r);
        }
        return GradAt(c, r);
    }

    // ---------------------------------------------------------------- camera (full.html: FOV 30, 5.4 units tall at z 0)

    static readonly float MdTan = Mathf.Tan(Mathf.DegToRad(15)), MdCamD = 2.7f / MdTan;
    float MdFrameW => Math.Max(W, H * 16 / 9f);
    float MdFrameH => MdFrameW * 9 / 16f;
    float MdK(float unityZ) => MdFrameH / (2 * (MdCamD + unityZ) * MdTan);   // pixels per unit at that depth
    Vector2 MdProj(Vector3 u) { float k = MdK(u.Z); return new Vector2(W / 2 + u.X * k, H / 2 - u.Y * k); }
    Vector3 MdUnproj(Vector2 n01) => new((n01.X - 0.5f) * MdFrameW / MdK(0), -(n01.Y - 0.5f) * MdFrameH / MdK(0), 0);   // bones.json -> z 0 plane

    // ---------------------------------------------------------------- materials (full.html FRAG / KIND / matColors)

    // Ported from full.html FRAG line for line. Coordinates stay in the web's convention (uv with y up); tx() turns them
    // into Godot texture lookups. A QuadMesh drawn in 2D already has UV.y going up the screen, so uv = UV.
    const string MdParticleShader = @"
uniform sampler2D uMain : repeat_enable, filter_linear; uniform sampler2D uMask : filter_linear; uniform sampler2D uNoise : repeat_enable, filter_linear;
uniform vec3 uC1; uniform vec3 uC2; uniform int uKind; uniform float uTime; uniform vec4 uP; uniform vec2 uTiles;
uniform float uMaskRot; uniform float uHasMask; uniform float uChan;
varying vec4 vCol; varying vec4 vMisc;
void vertex(){ vCol = COLOR; vMisc = INSTANCE_CUSTOM; }
#define tx(s, p) texture(s, vec2((p).x, 1.0-(p).y))
vec2 rot(vec2 p, float a){ float c=cos(a), s=sin(a); p-=.5; return vec2(c*p.x-s*p.y, s*p.x+c*p.y)+.5; }
float chan(vec4 t){ return uChan<0.5?t.r:(uChan<1.5?t.g:t.b); }
void fragment(){
  vec2 uv = UV; float seed=vMisc.x; vec3 col; float a;
  vec2 fuv = uv; if(uTiles.x>1.0){ float f=floor(vMisc.z); fuv=(uv+vec2(mod(f,uTiles.x), uTiles.y-1.0-floor(f/uTiles.x)))/uTiles; }
  if(uKind==0){
    float m = uHasMask>0.5 ? tx(uMask, rot(uv,uMaskRot)).r : 1.0;
    float v = chan(tx(uMain, fuv))*m; col = uC1*vCol.rgb*v; a = v*vCol.a;
  } else if(uKind==1){
    float m = uHasMask>0.5 ? tx(uMask, rot(uv,uMaskRot)).r : smoothstep(0.5,0.0,length(uv-.5));
    m *= smoothstep(0.5, 0.3, abs(uv.x-.5)) * smoothstep(1.0, 0.75, uv.y);
    float n = tx(uNoise, uv*uP.zw*0.5 + vec2(seed*7.13, -uTime*0.6 - seed)).b;
    float n2 = tx(uNoise, uv*uP.zw + vec2(-seed*3.1, -uTime*1.1)).g;
    float v = m*(0.55+0.7*n+0.25*n2) - (1.0-vCol.a)*0.8 - uP.x;
    a = smoothstep(0.08,0.35,v); col = mix(uC2, uC1, smoothstep(0.25,0.9,v))*vCol.rgb;
  } else if(uKind==2){
    float d = length(uv-.5)*2.0; float g = (exp(-d*d*5.0)*0.8 + exp(-d*12.0)*0.6) * (1.0-smoothstep(0.7, 1.0, d));
    col = uC1*vCol.rgb*g; a = g*vCol.a;
  } else if(uKind==4){
    float t = tx(uMain, fuv).r; col = vCol.rgb*t*0.25; a = t*vCol.a;
  } else if(uKind==5){
    vec2 p = (uv-.5)*2.0/0.62; float rr = length(p);
    float body = 1.0 - smoothstep(0.97, 1.0, rr); float z = sqrt(max(0.0, 1.0-rr*rr)); vec2 sp = p/(1.0+z);
    float n = tx(uNoise, sp*0.9 + vec2(uTime*0.35, uTime*0.12)).r;
    float n2 = tx(uNoise, sp*1.7 - vec2(uTime*0.2, -uTime*0.3)).g;
    float heat = 0.55 + 0.7*z + (n-0.5)*0.6 + (n2-0.5)*0.4;
    vec3 surf = mix(vec3(0.75,0.12,0.0), mix(vec3(1.0,0.55,0.08), vec3(1.0,0.97,0.8), smoothstep(0.75,1.15,heat)), smoothstep(0.2,0.8,heat));
    surf = surf*1.35 + vec3(1.0,0.5,0.1)*pow(1.0-z, 3.0)*0.9;
    surf = mix(surf, vec3(1.0,0.97,0.88)*1.3, vMisc.w);
    float glow = (1.0-body)*exp(-(rr-1.0)*3.5)*0.7 * (1.0-smoothstep(0.75, 1.0, length(uv-.5)*2.0));
    col = (surf*body + vec3(1.0,0.45,0.05)*glow)*vCol.a; a = body*vCol.a;
  } else if(uKind==6){
    float d = length(uv-.5)*2.0; float n = tx(uNoise, uv*2.0+uTime*0.2).r;
    float v = smoothstep(0.55,0.85,d)*smoothstep(1.0,0.86,d)*(0.6+0.6*n); col = uC1*vCol.rgb*v; a = v*vCol.a;
  } else if(uKind==7){
    vec2 p = uv-.5; float r = length(p)*2.0, th = atan(p.y,p.x);
    float rays = pow(abs(sin(th*7.0 + seed*20.0)), 18.0) + pow(abs(sin(th*11.0 + seed*9.0)), 30.0);
    float v = rays*smoothstep(1.0,0.2,r)*smoothstep(0.0,0.25,r); col = uC1*vCol.rgb*v; a = v*vCol.a;
  } else {
    float v = smoothstep(0.5,0.0,abs(uv.x-.5))*smoothstep(0.0,0.4,uv.y)*smoothstep(1.0,0.6,uv.y);
    col = mix(uC1,uC2,uv.y)*vCol.rgb*v; a = v*vCol.a;
  }
  if(uKind==4 || uKind==5) COLOR = vec4(col, a); else COLOR = vec4(min(col, vec3(4.0))*a, 1.0);   // 4 straight alpha, 5 premultiplied
}";
    const string MdWallShader = @"
shader_type canvas_item; render_mode blend_add, unshaded;
uniform sampler2D uNoise : repeat_enable, filter_linear; uniform sampler2D uMask : filter_linear;
uniform vec3 uC1; uniform vec3 uC2; uniform float uTime; uniform float uSmooth; uniform float uAlpha; uniform float uHasMask; uniform float uCover; uniform vec2 uTil;
#define tx(s, p) texture(s, vec2((p).x, 1.0-(p).y))
void fragment(){ vec2 uv = vec2(UV.x, 1.0-UV.y);   // a ColorRect's UV runs down the screen; the web's runs up
  float g = uHasMask>0.5 ? tx(uMask, vec2(uv.x, 1.0-uv.y)).r : (1.0-uv.y);
  float w = tx(uNoise, uv*vec2(1.3,0.5) + vec2(0.0, -uTime*0.2)).r;
  vec2 q = uv + vec2((w-0.5)*0.12, 0.0);
  float n = tx(uNoise, q*uTil + vec2(0.0, -uTime*0.45)).b;
  float n2 = tx(uNoise, q*uTil*vec2(2.3,1.6) + vec2(0.3, -uTime*0.9)).g;
  float v = g*1.1 + (n-0.5)*0.9 + (n2-0.5)*0.5 - 0.15 + uCover;
  float m = smoothstep(uSmooth, uSmooth+0.25, v);
  float heat = smoothstep(0.0, 1.1, v);
  vec3 col = mix(uC2*0.7, uC1, heat)*heat*1.5 + vec3(1.0,0.9,0.6)*smoothstep(0.75,1.3,v)*0.9;
  COLOR = vec4(min(col,vec3(1.6))*m*uAlpha, 1.0); }";

    static readonly Dictionary<string, (int kind, string? main, string? mask, float maskRot, int chan)> MdKind = new()
    {
        ["fxm_flame_small_b"] = (1, null, "msk_007", 0, 0), ["fxm_flame_big_b"] = (1, null, "msk_002", 1.5708f, 0),
        ["fxm_exp_flame_01_b"] = (1, null, "msk_001", 0, 0), ["fxm_exp_flame_02_b"] = (1, null, "msk_001", 0, 0), ["fxm_exp_flame_03_b"] = (1, null, null, 0, 0),
        ["fxm_spark_01_b"] = (0, "ple_010", "msk_001", 0, 0), ["fxm_spark_02_b"] = (2, null, null, 0, 0), ["fxm_particle_01_b"] = (2, null, null, 0, 0),
        ["fxm_temperature_01_k"] = (0, "nis_002", "msk_001", 0, 1), ["fxm_exp_fog_01_k"] = (0, "nis_002", "ple_001", 0, 0),
        ["fxm_dust_01_b"] = (4, "dst_004", null, 0, 0), ["fxm_light_k"] = (2, null, null, 0, 0), ["fxm_eya_01_k"] = (2, null, null, 0, 0),
        ["fxm_flare_01_k"] = (0, "msk_012", "fre_002", 0, 0), ["fxm_ball_01_b"] = (5, null, null, 0, 0), ["fxm_Shockwave_01_b"] = (6, null, null, 0, 0),
        ["fxm_flare_line_01_b"] = (7, null, null, 0, 0), ["fxm_gatherLine_01_b"] = (8, null, null, 0, 0),
    };
    static readonly HashSet<string> MdHot = new() { "fxm_ball_01_b", "fxm_par_02_b", "fxm_Shockwave_01_b", "fxm_eya_01_k" };

    static readonly Dictionary<string, Texture2D> mdTex = new();
    static Texture2D MdTex(string n) => mdTex.TryGetValue(n, out var t) ? t
        : mdTex[n] = ImageTexture.CreateFromImage(ResImage($"{MdDir}tex/fxt_{n}.png"));

    (Vector3 c1, Vector3 c2) MdColors(JsonNode mats, string name)
    {
        var c = mats[name]?["c"]?.AsObject();
        Vector3? Pick(params string[] ks) { foreach (var k in ks) if (c?[k] is JsonArray a) return new((float)a[0]!, (float)a[1]!, (float)a[2]!); return null; }
        var c1 = Pick("_Color_01", "_Emissive", "_Emission") ?? Vector3.One;
        var c2 = Pick("_Color_02") ?? c1 * 0.4f;
        float cap = MdHot.Contains(name) ? 3 : 1.3f;
        Vector3 Norm(Vector3 v) { float mx = Math.Max(v.X, Math.Max(v.Y, v.Z)); return mx > cap ? v / mx * cap : v; }
        return (Norm(c1), Norm(c2));
    }

    ShaderMaterial MdMat(JsonNode mats, string name, Vector2 tiles)
    {
        var k = MdKind.TryGetValue(name, out var kk) ? kk : (2, null, null, 0f, 0);
        string blend = k.kind == 4 ? "blend_mix" : k.kind == 5 ? "blend_premul_alpha" : "blend_add";
        var (c1, c2) = MdColors(mats, name);
        var f = mats[name]?["f"];
        var m = new ShaderMaterial { Shader = new Shader { Code = $"shader_type canvas_item; render_mode {blend}, unshaded;\n" + MdParticleShader } };
        m.SetShaderParameter("uMain", MdTex(k.main ?? "cmn_004")); m.SetShaderParameter("uMask", MdTex(k.mask ?? "cmn_004")); m.SetShaderParameter("uNoise", MdTex("nis_010"));
        m.SetShaderParameter("uC1", c1); m.SetShaderParameter("uC2", c2); m.SetShaderParameter("uKind", k.kind);
        m.SetShaderParameter("uP", new Vector4(0, 1, (float?)f?["_Tiling_X"] ?? 2, (float?)f?["_Tiling_Y"] ?? 1));
        m.SetShaderParameter("uTiles", tiles); m.SetShaderParameter("uMaskRot", k.maskRot); m.SetShaderParameter("uHasMask", k.mask != null ? 1f : 0f); m.SetShaderParameter("uChan", (float)k.chan);
        return m;
    }

    // ---------------------------------------------------------------- particle layers

    sealed class MdLayer
    {
        public MultiMesh Mm = null!; public MultiMeshInstance2D Node = null!; public int N;
        public void Put(Vector2 at, float sx, float sy, float rot, Color c, Color misc)
        {
            if (N >= Mm.InstanceCount) return;
            Mm.SetInstanceTransform2D(N, new Transform2D(rot, new Vector2(sx, sy), 0, at));
            Mm.SetInstanceColor(N, c); Mm.SetInstanceCustomData(N, misc); N++;
        }
        public void Flush(double t) { Mm.VisibleInstanceCount = N; ((ShaderMaterial)Node.Material).SetShaderParameter("uTime", (float)t); N = 0; }
    }
    MdLayer MdNewLayer(Func<Node2D, Node2D> add, ShaderMaterial mat, int max)
    {
        var mm = new MultiMesh { TransformFormat = MultiMesh.TransformFormatEnum.Transform2D, UseColors = true, UseCustomData = true, Mesh = new QuadMesh { Size = Vector2.One }, InstanceCount = max, VisibleInstanceCount = 0 };
        var node = new MultiMeshInstance2D { Multimesh = mm, Material = mat };
        add(node);
        return new MdLayer { Mm = mm, Node = node };
    }

    sealed class MdPart
    {
        public float B, Life, Speed, Size, Rot, Rv, Seed, Orb, Nz; public int Frame; public Color Col; public Vector3 P, D; public float[]? Vel;
    }
    sealed class MdSystem
    {
        public JsonNode Node = null!, Ps = null!; public double Start, Dur; public List<MdPart> Parts = new(); public Transform3D Local; public Func<double, Transform3D?> Anchor = null!; public MdLayer Layer = null!; public int Mode;
    }

    static Func<double> Rng(int seed) { uint s = (uint)seed; if (s == 0) s = 1; return () => { s = (uint)((s ^ (s >> 15)) * 0x2c1b3c6dL + 0x9e3779b9L); return s / 4294967296.0; }; }
    static Vector3 V(JsonNode a) => new((float)a[0]!, (float)a[1]!, (float)a[2]!);
    static Transform3D UMat(Vector3 p, Quaternion q, Vector3 s) => new(new Basis(q) * Basis.FromScale(s), p);
    static Quaternion UEuler(Vector3 deg) => Basis.FromEuler(deg * (Mathf.Pi / 180), EulerOrder.Yxz).GetRotationQuaternion();

    static (Vector3 p, Vector3 d) ShapeSample(JsonNode sh, Func<double> r)
    {
        int t = (int)sh["type"]!; float radius = (float)sh["radius"]!; float thick = (float?)sh["thick"] ?? 1;
        if (t == 5) { var sc = V(sh["scale"]!); return (new Vector3(((float)r() - .5f) * sc.X, ((float)r() - .5f) * sc.Y, ((float)r() - .5f) * sc.Z), Vector3.Back); }
        if (t == 10)
        {
            float a = (float)r() * Mathf.Tau * ((float?)sh["arc"] ?? 360) / 360, rad = radius * (1 - (float)r() * thick);
            var d = new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0); return (d * rad, d);
        }
        if (t is 0 or 1)
        {
            float u = (float)r() * 2 - 1, a = (float)r() * Mathf.Tau, s = Mathf.Sqrt(1 - u * u);
            var d = new Vector3(s * Mathf.Cos(a), s * Mathf.Sin(a), u); return (d * radius * (t == 1 ? 1 : Mathf.Pow((float)r(), 1 / 3f)), d);
        }
        float a2 = (float)r() * Mathf.Tau, rad2 = radius * Mathf.Sqrt((float)r()) * thick, spread = Mathf.DegToRad((float)sh["angle"]!) * rad2 / (radius == 0 ? 1 : radius);
        return (new Vector3(Mathf.Cos(a2) * rad2, Mathf.Sin(a2) * rad2, 0), new Vector3(Mathf.Cos(a2) * Mathf.Sin(spread), Mathf.Sin(a2) * Mathf.Sin(spread), Mathf.Cos(spread)));
    }

    /// One MD prefab's emitters as stateless particle sets (full.html buildSystem): births and attributes come from a
    /// seeded RNG, so any time can be drawn directly.
    void MdBuild(JsonNode data, string key, double start, double dur, Func<double, Transform3D?> anchor, int seedBase, List<MdSystem> into, Func<Node2D, Node2D> add)
    {
        var list = data["ps"]![key]!.AsArray();
        var byPath = list.ToDictionary(n => (string)n!["path"]!, n => n!);
        var cache = new Dictionary<string, Transform3D>();
        Transform3D Get(string p)
        {
            if (cache.TryGetValue(p, out var m)) return m;
            var n = byPath[p]; int i = p.LastIndexOf('/');
            var r = n["rot"]!;
            var local = UMat(V(n["pos"]!), new Quaternion((float)r[0]!, (float)r[1]!, (float)r[2]!, (float)r[3]!), V(n["scale"]!));
            return cache[p] = i < 0 ? Transform3D.Identity : Get(p[..i]) * local;
        }
        for (int ni = 0; ni < list.Count; ni++)
        {
            var n = list[ni]!; var ps = n["ps"]; var rr = n["r"];
            if (ps == null || rr == null || (int)rr["mode"]! == 5 || !(bool)n["active"]!) continue;
            string mat = (string)rr["mats"]![0]!;
            if (mat.StartsWith("ext:")) continue;
            var r = Rng(seedBase * 977 + ni * 131 + 7);
            var uvm = ps["UVModule"]; var tiles = uvm != null ? new Vector2((float)uvm["tilesX"]!, (float)uvm["tilesY"]!) : Vector2.One;
            var births = new List<float>();
            float period = (float)ps["dur"]!; int loops = (bool)ps["loop"]! ? (int)Math.Ceiling(dur / period) : 1;
            for (int L = 0; L < loops; L++)
            {
                foreach (var bb in ps["bursts"]!.AsArray())
                    for (int c = 0; c < Math.Max(1, (int)bb![2]!); c++)
                    {
                        float tt = L * period + (float)bb[0]! + c * (float)bb[3]!;
                        if (tt >= period * (L + 1) || tt > dur) continue;
                        int n0 = (int)Math.Round(MmAt(bb[1], tt / period, (float)r()));
                        for (int k = 0; k < n0; k++) births.Add(tt);
                    }
                float rate = MmAt(ps["rate"], 0, 0.5f);
                if (rate > 0) for (float tt = L * period + 1 / rate * (float)r(); tt < Math.Min(period * (L + 1), dur); tt += 1 / rate) births.Add(tt);
            }
            if (births.Count == 0) continue;
            var sh = ps["shape"]!; var shapeRot = UEuler(V(sh["rot"]!)); var shapePos = V(sh["pos"]!);
            var sys = new MdSystem { Node = n, Ps = ps, Start = start, Dur = dur, Local = Get((string)n["path"]!), Anchor = anchor, Mode = (int)rr["mode"]! };
            foreach (var b in births)
            {
                var (p, d) = ShapeSample(sh, r);
                p = shapeRot * p + shapePos; d = shapeRot * d;
                float x = b % period / period;
                var vm = ps["VelocityModule"];
                sys.Parts.Add(new MdPart
                {
                    B = b, Life = Math.Max(0.05f, MmAt(ps["life"], x, (float)r())), Speed = MmAt(ps["speed"], x, (float)r()), Size = MmAt(ps["size"], x, (float)r()),
                    Rot = MmAt(ps["rot"], x, (float)r()), Rv = ps["RotationModule"] is { } rm ? MmAt(rm["curve"], 0, (float)r()) : 0,
                    Col = StartColor(ps["color"]!, (float)r()), P = p, D = d, Seed = (float)r(), Frame = (int)(r() * tiles.X * tiles.Y),
                    Vel = vm != null ? new[] { MmAt(vm["x"], 0, (float)r()), MmAt(vm["y"], 0, (float)r()), MmAt(vm["z"], 0, (float)r()) } : null,
                    Orb = vm != null ? MmAt(vm["orbitalZ"], 0, (float)r()) : 0, Nz = (float)r() * 100,
                });
            }
            sys.Layer = MdNewLayer(add, MdMat(data["mats"]!, mat, tiles), sys.Parts.Count);
            into.Add(sys);
        }
    }

    void MdDraw(MdSystem S, double T)
    {
        double lt = T - S.Start; var ps = S.Ps;
        if (lt >= 0 && lt <= S.Dur && S.Anchor(T) is { } anchor)
        {
            var world = anchor * S.Local;
            var ws = world.Basis.Scale; float sc = (Math.Abs(ws.X) + Math.Abs(ws.Y)) / 2;
            float damp = ps["ClampVelocityModule"] != null ? 2.5f : 0, grav = MmAt(ps["grav"], 0, 0.5f);
            var sizeC = ps["SizeModule"]?["curve"]; var colG = ps["ColorModule"]?["gradient"];
            float noise = ps["NoiseModule"] is { } nm ? (MmAt(nm["strength"], 0.5f, 0.5f) is var ns && ns != 0 ? ns : 0.5f) : 0;
            var uvm = ps["UVModule"];
            foreach (var P in S.Parts)
            {
                float age = (float)lt - P.B;
                if (age < 0 || age > P.Life || P.B > S.Dur) continue;
                float x = age / P.Life;
                float k = damp > 0 ? (1 - Mathf.Exp(-damp * age)) / damp : age;
                var pos = P.P + P.D * (P.Speed * k);
                if (P.Vel != null) pos += new Vector3(P.Vel[0], P.Vel[1], P.Vel[2]) * age;
                if (grav != 0) pos.Y -= 0.5f * 9.81f * grav * age * age;
                if (P.Orb != 0) { float a = P.Orb * age, c = Mathf.Cos(a), s = Mathf.Sin(a); pos = new Vector3(pos.X * c - pos.Y * s, pos.X * s + pos.Y * c, pos.Z); }
                if (noise != 0) { pos.X += Mathf.Sin(P.Nz + age * 2.3f) * noise * 0.3f * x; pos.Y += Mathf.Cos(P.Nz * 1.7f + age * 1.9f) * noise * 0.3f * x; }
                pos = world * pos;
                float size = P.Size * sc * (sizeC != null ? MmAt(sizeC, x, P.Seed) : 1);
                var g = colG == null ? Colors.White : GradAt(colG is JsonArray ga ? ga[0]! : colG, x);
                var col = new Color(P.Col.R * g.R, P.Col.G * g.G, P.Col.B * g.B, P.Col.A * g.A);
                float px = MdK(pos.Z), sx = size, sy = size, rz = P.Rot + P.Rv * age;
                if (S.Mode == 1) { var dir = world.Basis * P.D; sy = size * ((float?)S.Node["r"]!["len"] ?? 1) * 2; rz = Mathf.Atan2(-dir.X, dir.Y); }
                float frame = uvm == null ? 0 : (float)uvm["frameOverTime"]! > 0.5f ? Math.Min(uvm["tilesX"]!.GetValue<int>() * uvm["tilesY"]!.GetValue<int>() - 1, (int)(x * uvm["tilesX"]!.GetValue<int>() * uvm["tilesY"]!.GetValue<int>())) : P.Frame;
                S.Layer.Put(MdProj(pos), sx * px, sy * px, -rz, col, new Color(P.Seed, x, frame, 0));
            }
        }
        S.Layer.Flush(T);
    }

    // ---------------------------------------------------------------- the scene

    async Task ExodiaNative(List<TextureRect> big, ColorRect dim, Func<Control, Control> addCtl)
    {
        var data = JsonNode.Parse(Godot.FileAccess.GetFileAsString(MdDir + "data.json"))!;
        var bones = JsonNode.Parse(Godot.FileAccess.GetFileAsString(MdDir + "bones.json"))!;
        var bf = bones["frames"]!.AsArray(); double bFps = (double)bones["fps"]!, bT0 = (double)bones["t0"]!;
        var cv = new MdCurves(data);
        var mats = data["mats"]!;

        // Everything lives on one full-screen Control, in MD's draw order (children added back to front).
        var stage = (Control)addCtl(new Control { Size = new Vector2(W, H), MouseFilter = MouseFilterEnum.Ignore });
        T Ctl<T>(T n) where T : Control { stage.AddChild(n); return n; }
        Node2D N2(Node2D n) { stage.AddChild(n); return n; }
        float fw = MdFrameW, fh = MdFrameH; var frameAt = new Vector2((W - fw) / 2, (H - fh) / 2);

        var brack = Ctl(new ColorRect { Size = new Vector2(W, H), Color = new Color(0, 0, 0, 0), MouseFilter = MouseFilterEnum.Ignore });
        ColorRect Wall(string mat, float hasMask, Vector2 til, float cover, Vector2 sizeU, float z)
        {
            var (c1, c2) = MdColors(mats, mat);
            var m = new ShaderMaterial { Shader = new Shader { Code = MdWallShader } };
            m.SetShaderParameter("uNoise", MdTex("nis_010")); m.SetShaderParameter("uMask", MdTex("msk_004"));
            m.SetShaderParameter("uC1", c1); m.SetShaderParameter("uC2", c2); m.SetShaderParameter("uHasMask", hasMask);
            m.SetShaderParameter("uCover", cover); m.SetShaderParameter("uTil", til);
            float k = MdK(z); var sz = sizeU * k;
            return Ctl(new ColorRect { Size = sz, Position = new Vector2(W, H) / 2 - sz / 2, Material = m, MouseFilter = MouseFilterEnum.Ignore, Visible = false });
        }
        var backWall = Wall("fxm_BK_flame_02_b", 0, new Vector2(2, 0.8f), 0.7f, new Vector2(9.6f, 5.4f), 0);
        var systems = new List<MdSystem>();
        var backSys = new List<MdSystem>();
        MdBuild(data, "back", 2.583, 7.067, _ => Transform3D.Identity, 2, backSys, N2);

        // Exodia: baked frames (transparent), silhouette and rise applied here.
        var exTex = ImageTexture.CreateFromImage(ResImage($"{MdDir}x_000.webp"));
        var exo = Ctl(new TextureRect { Texture = exTex, ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.Scale, Size = new Vector2(fw, fh), Position = frameAt, MouseFilter = MouseFilterEnum.Ignore, Visible = false });

        var frontWall = Wall("fxm_BK_flame_01_b", 1, new Vector2(1.6f, 0.7f), 0.3f, new Vector2(8, 4.5f), -3);
        Transform3D? Eye(double T)
        {
            int i = Math.Clamp((int)Math.Round((T - bT0) * bFps), 0, bf.Count - 1);
            var e = MdUnproj(new Vector2((float)bf[i]![0]!, (float)bf[i]![1]!));
            return UMat(new Vector3(e.X, e.Y, -0.05f), Quaternion.Identity, Vector3.One * 0.14f);
        }
        MdBuild(data, "front", 2.583, 3.167, _ => UMat(new Vector3(0, 0, -3), Quaternion.Identity, Vector3.One), 1, systems, N2);
        MdBuild(data, "exp", 5.4, 4.25, _ => UMat(new Vector3(0, 0, -3), Quaternion.Identity, Vector3.One * 0.5f), 3, systems, N2);
        MdBuild(data, "eye", 5.75, 1.0, Eye, 9, systems, N2);

        // The power ball (full.html updateBall): layers in its draw order.
        var L = new Dictionary<string, MdLayer>();
        foreach (var (name, mat, max) in new[] { ("glow", "fxm_light_k", 3), ("streak", "fxm_gatherLine_01_b", 48), ("mote", "fxm_particle_01_b", 48), ("orb", "fxm_ball_01_b", 1),
                                                  ("star", "fxm_flare_01_k", 2), ("rays", "fxm_flare_line_01_b", 2), ("ring", "fxm_Shockwave_01_b", 2) })
            L[name] = MdNewLayer(N2, MdMat(mats, mat, Vector2.One), max);

        var snd = new AudioStreamPlayer { Stream = GD.Load<AudioStream>(MdSound), VolumeDb = -4, Bus = "Sound" };
        AddChild(snd);
        snd.Play((float)(MdT0 - 0.3));

        ulong start = Time.GetTicksUsec(); int exShown = -1;
        while (true)
        {
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            double T = MdT0 + (Time.GetTicksUsec() - start) / 1e6;
            if (T >= MdEnd) break;

            // fire walls + backdrop (MD's Front / Back infinite clips run on timeline time)
            bool backOn = T >= 2.583 && T < 2.583 + 7.067, frontOn = T >= 2.583 && T < 2.583 + 3.167;
            brack.Visible = backWall.Visible = backOn; frontWall.Visible = frontOn;
            brack.Color = new Color(0, 0, 0, cv["back", "4169315712:212/0/m_Color.a", T, 0]);
            foreach (var (w, key, smooth, alpha, sDef, aDef, aMul) in new[] { (backWall, "back", "3687629845:_MASK_Smooth", "3687629845:_Main_Alpha", -0.45f, 1f, 1f),
                                                                                 (frontWall, "front", "3349048959:_MASK_Smooth", "3349048959:_Main_Alpha", -0.3f, 0.3f, 0.55f) })
            {
                var m = (ShaderMaterial)w.Material;
                m.SetShaderParameter("uSmooth", cv[key, smooth, T, sDef]); m.SetShaderParameter("uAlpha", aMul * cv[key, alpha, T, aDef]); m.SetShaderParameter("uTime", (float)T);
            }
            if (T >= 3.0) foreach (var r in big) r.Visible = false;   // the cards are behind the black backdrop by now

            foreach (var s in backSys) MdDraw(s, T);

            // Exodia: active 3.0-9.817; shadow until the explosion (_Colorpower), rising out of the fire over ~0.9s
            bool exOn = T >= 3.0 && T < 9.817;
            exo.Visible = exOn;
            if (exOn)
            {
                int i = Math.Clamp((int)Math.Round((T - bT0) * bFps), 0, bf.Count - 1);
                if (i != exShown) { exTex.Update(ResImage($"{MdDir}x_{i:000}.webp")); exShown = i; }
                float cp = cv["spine", "0:_Colorpower", T, 0], sh = Mathf.SmoothStep(0.72f, 0.86f, cp);
                exo.SelfModulate = new Color(Mathf.Lerp(1, 0.05f, sh), Mathf.Lerp(1, 0.03f, sh), Mathf.Lerp(1, 0.02f, sh));
                float rise = Mathf.Clamp((float)(T - 2.9) / 0.9f, 0, 1);
                exo.Position = frameAt + new Vector2(0, 2.8f * (1 - rise) * (1 - rise) * MdK(0));
            }

            foreach (var s in systems) MdDraw(s, T);
            MdBall(T, cv, bf, bFps, bT0, L);
        }

        // MD's Fade layer: white at WinStart, fading back to the field (as the film does).
        var white = (ColorRect)addCtl(new ColorRect { Size = new Vector2(W, H), Color = Colors.White, MouseFilter = MouseFilterEnum.Ignore });
        stage.Visible = false;
        dim.Color = new Color(0, 0, 0, 0);
        CreateTween().TweenProperty(white, "color:a", 0f, 1.8).SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.Out);
        await Wait(0.3);
        After(2.0, () => { if (IsInstanceValid(stage)) stage.QueueFree(); if (IsInstanceValid(white)) white.QueueFree(); snd.QueueFree(); });
    }

    /// The power ball, gather -> release (full.html updateBall), on MD's ball scale / _Animation_Alpha / shockwave curves.
    void MdBall(double T, MdCurves cv, JsonArray bf, double bFps, double bT0, Dictionary<string, MdLayer> L)
    {
        static float Clamp01(float x) => Mathf.Clamp(x, 0, 1);
        static float Ease(float x) => x * x * (3 - 2 * x);
        static float Win(double T, double a, double b, double fi = 0.15, double fo = 0.15) => Clamp01((float)((T - a) / fi)) * Clamp01((float)((b - T) / fo));
        if (T >= 6.75 && T < 9.617)
        {
            int bi = Math.Clamp((int)Math.Round((T - bT0) * bFps), 0, bf.Count - 1);
            var rh = MdUnproj(new Vector2((float)bf[bi]![2]!, (float)bf[bi]![3]!)); var lh = MdUnproj(new Vector2((float)bf[bi]![4]!, (float)bf[bi]![5]!));
            var c = new Vector3((rh.X + lh.X) / 2, (rh.Y + lh.Y) / 2 + 0.08f, -1);
            float k = MdK(-1);
            void Put(string n, float x, float y, float sx, float sy, float rz, Color col, float w = 0, float seed = 0) =>
                L[n].Put(MdProj(c + new Vector3(x, y, 0)), sx * k, sy * k, -rz, col, new Color(seed, 0, 0, w));

            float s = cv["ball", "0:scale.x", T, 0.4665f];
            float R = 0.8f * s / 1.09f;   // BALL_R_FULL: floats between the palms at full charge
            float fadeIn = cv["ball", "3556829029:_Animation_Alpha", T, 1];
            float charge = Clamp01((float)(T - 6.75) / 2), release = Ease(Clamp01((float)(T - 8.97) / 0.5f));
            float pulse = 1 + 0.06f * Mathf.Sin((float)T * 18) * (1 - release);
            Put("glow", 0, 0, R * 5.2f * pulse, R * 5.2f * pulse, 0, new Color(1, 0.5f, 0.12f, 0.35f * fadeIn * (0.6f + 0.4f * charge)));
            Put("glow", 0, 0, R * 2.8f, R * 2.8f, 0, new Color(1, 0.85f, 0.5f, 0.7f * fadeIn));
            for (int i = 0; i < 48; i++)
            {
                var r0 = Rng(1000 + i); double born = 7.25 + i * 0.038 + r0() * 0.03, life = 0.42 + r0() * 0.12;
                float x = (float)((T - born) / life);
                if (x < 0 || x > 1 || born > 9.0) continue;
                float a = (float)r0() * Mathf.Tau, xe = x * x;
                float rad = Mathf.Lerp(3.2f + (float)r0() * 0.6f, R * 1.05f, xe), len = Mathf.Lerp(0.9f, 0.15f, xe);
                Put("streak", Mathf.Cos(a) * (rad + len / 2), Mathf.Sin(a) * (rad + len / 2), 0.12f + 0.06f * (float)r0(), len, a + Mathf.Pi / 2, new Color(1.6f, 1.4f, 1.0f, Mathf.Sin(Mathf.Pi * x) * Win(T, 7.25, 9.1)));
            }
            for (int i = 0; i < 48; i++)
            {
                var r0 = Rng(2000 + i); double born = 6.8 + i * 0.045, life = 0.8 + r0() * 0.4;
                float x = (float)((T - born) / life);
                if (x < 0 || x > 1 || born > 8.9) continue;
                float a = (float)r0() * Mathf.Tau + x * 2.2f, rad = Mathf.Lerp(2.4f + (float)r0(), R, x * x), sz = 0.16f + 0.12f * (float)r0();
                Put("mote", Mathf.Cos(a) * rad, Mathf.Sin(a) * rad, sz, sz, 0, new Color(1, 0.7f, 0.3f, Mathf.Sin(Mathf.Pi * x)));
            }
            Put("orb", 0, 0, 2 * R / 0.62f, 2 * R / 0.62f, 0, new Color(1, 1, 1, fadeIn), release);
            float pop = Win(T, 6.75, 7.15, 0.05, 0.3), peak = Win(T, 8.75, 9.35, 0.12, 0.35);
            if (pop > 0) Put("star", 0, 0, 3.2f, 3.2f, 0.3f, new Color(1, 0.9f, 0.6f, pop));
            if (peak > 0) Put("star", 0, 0, R * 7 * (1 + release), R * 7 * (1 + release), (float)T * 0.4f, new Color(1, 0.95f, 0.75f, peak));
            float rays = Win(T, 8.75, 9.617, 0.25, 0.12);
            if (rays > 0) { float rs = Mathf.Lerp(2.5f, 11, Ease(Clamp01((float)(T - 8.75) / 0.8f))); Put("rays", 0, 0, rs, rs, 0, new Color(1, 0.9f, 0.6f, rays), 0, 0.37f); }
            if (T >= 9.083)
            {
                float sc = cv["shock", "161824251:scale.x", T, 1] * cv["shock", "0:scale.x", T, 1], x = Clamp01((float)((T - 9.083) / 0.534));
                Put("ring", 0, 0, 1.4f * sc, 1.4f * sc, 0, new Color(1, 0.85f, 0.5f, 1 - x * x));
            }
        }
        foreach (var l in L.Values) l.Flush(T);
    }
}
