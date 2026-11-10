using Godot;
using PoC.Engine;
using static PoC.Game.UI.ConceptKit;

namespace PoC.Game.UI;

/// Concept 2 "CHAOS RIFT": the screen is torn between Light and Darkness (Chaos = both). Left: radiant light with
/// Black Luster Soldier - Envoy of the Beginning; right: the void with Chaos Emperor Dragon - Envoy of the End; a
/// living white-hot crack between. Menu words sit ON the crack, each half inverted by the side it falls on.
/// `-- --demo [--screen=menu|setup] [--hover=N]`
public partial class ChaosRift : MenuBase
{
    // The crack: x = mix(Top, Bottom, y) in screen UV, plus noise (same formula in every shader below).
    const float Top = 0.6f, Bottom = 0.4f;
    const string CrackFn = @"
uniform float top = 0.6;
uniform float bottom = 0.4;
float h21(vec2 p) { vec3 q = fract(vec3(p.xyx) * 0.1031); q += dot(q, q.yzx + 33.33); return fract((q.x + q.y) * q.z); }
float n1(float x) { float i = floor(x); float f = fract(x); f = f*f*(3.0-2.0*f); return mix(h21(vec2(i, 1.7)), h21(vec2(i + 1.0, 1.7)), f); }
float crack_x(float y, float t) {
  float base = mix(top, bottom, y);
  float j = (n1(y * 9.0 + 3.0) - 0.5) * 0.03 + (n1(y * 37.0 + t * 0.6) - 0.5) * 0.008;
  return base + j;
}
";
    const string Bg = @"shader_type canvas_item;
uniform float aspect = 1.6;
uniform float focus_y = -1.0;
uniform sampler2D noise_tex : repeat_enable, filter_linear;
" + CrackFn + @"
float fbm(vec2 p) { float a = 0.5, s = 0.0; for (int i = 0; i < 5; i++) { s += a * texture(noise_tex, p).r; p *= 2.03; a *= 0.5; } return s; }
void fragment() {
  vec2 uv = UV; float t = TIME;
  float cx = crack_x(uv.y, t);
  float d = (uv.x - cx) * aspect;           // <0 light side, >0 dark side
  // LIGHT: warm sky with rays from the upper left
  vec2 lp = uv - vec2(-0.1, -0.15);
  float ang = atan(lp.y, lp.x);
  float rays = pow(abs(sin(ang * 22.0 + t * 0.05)), 18.0) * 0.35 + pow(abs(sin(ang * 9.0 - t * 0.03)), 8.0) * 0.25;
  float glow = exp(-length(lp) * 1.6);
  vec3 light = mix(vec3(0.95, 0.88, 0.72), vec3(1.0, 0.97, 0.9), glow) + vec3(1.0, 0.85, 0.5) * rays * glow;
  light *= 0.85 + 0.15 * fbm(uv * vec2(1.5, 1.0) + vec2(t * 0.01, 0.0));
  // DARK: violet void, drifting smoke, stars
  float sm = fbm(uv * vec2(2.0, 1.3) + vec2(-t * 0.012, t * 0.006));
  vec3 dark = mix(vec3(0.015, 0.008, 0.03), vec3(0.16, 0.05, 0.28), sm * sm * 1.4);
  float st = step(0.9975, h21(floor(uv * vec2(900.0, 560.0))));
  dark += st * vec3(0.8, 0.75, 1.0) * (0.5 + 0.5 * sin(t * 2.0 + uv.x * 90.0));
  vec3 c = d < 0.0 ? light : dark;
  // bleed: light side darkens toward the crack, dark side lit by it
  c = mix(c, c * 0.55, smoothstep(-0.25, 0.0, d) * step(d, 0.0) * 0.6);
  c += vec3(0.55, 0.3, 0.9) * exp(-max(d, 0.0) * 9.0) * 0.35 * step(0.0, d);
  // the crack: white-hot core, gold on the light side, violet on the dark
  float flick = 0.85 + 0.15 * n1(t * 7.0 + uv.y * 4.0);
  float flare = focus_y < 0.0 ? 0.0 : exp(-pow((uv.y - focus_y) * 9.0, 2.0));
  float core = exp(-abs(d) * (220.0 - 120.0 * flare)) * flick;
  float halo = exp(-abs(d) * 28.0) * (0.6 + flare * 0.8);
  c += vec3(1.0) * core * 1.6;
  c += (d < 0.0 ? vec3(1.0, 0.7, 0.25) : vec3(0.6, 0.3, 1.0)) * halo * 0.55;
  COLOR = vec4(c, 1.0);
}";
    /// Card art blended into its side: `side` -1 light (sepia, multiply-ish), +1 dark (violet ghost).
    const string ArtSide = @"shader_type canvas_item;
uniform float side = 1.0;
uniform float aspect = 1.6;
" + CrackFn + @"
void fragment() {
  vec4 a = texture(TEXTURE, UV);
  float cx = crack_x(SCREEN_UV.y, TIME);
  float d = (SCREEN_UV.x - cx) * aspect * side;
  float mask = smoothstep(0.0, 0.18, d);
  float r = length((UV - 0.5) * vec2(1.0, 1.0));
  mask *= smoothstep(0.62, 0.25, r);
  float l = dot(a.rgb, vec3(0.3, 0.59, 0.11));
  vec3 c = side < 0.0 ? mix(vec3(0.55, 0.42, 0.26), vec3(1.0, 0.95, 0.85), l) : mix(vec3(0.04, 0.0, 0.08), vec3(0.75, 0.5, 1.0), l * l);
  COLOR = vec4(c, mask * (side < 0.0 ? 0.55 : 0.75));
}";
    /// Text that inverts across the crack: black ink on the light side, cream on the dark.
    const string TextInv = @"shader_type canvas_item;
uniform float aspect = 1.6;
uniform float lit = 0.0;
" + CrackFn + @"
void fragment() {
  float a = texture(TEXTURE, UV).a * COLOR.a;
  float cx = crack_x(SCREEN_UV.y, TIME);
  float d = (SCREEN_UV.x - cx) * aspect;
  vec3 ink = mix(vec3(0.08, 0.05, 0.03), vec3(0.55, 0.3, 0.0), lit);
  vec3 cream = mix(vec3(0.92, 0.88, 0.98), vec3(1.0, 0.85, 0.5), lit);
  COLOR = vec4(d < 0.0 ? ink : cream, a);
}";

    float W, H;
    ShaderMaterial bg = null!;
    readonly List<(Label L, ShaderMaterial M, float Y)> items = new();
    Label blurb = null!;
    Font display = null!, ui = null!;
    Shader? textShader;

    protected override int ItemCount => Demo.Arg("screen") == "setup" ? 1 : Items.Length;
    protected override string? MusicPath => Md("menu_01");
    protected override void OnChoose(int i) { }
    protected override void OnBack() => GetTree().Quit();

    float CrackX(float y) => Mathf.Lerp(Top, Bottom, y);

    protected override void Build()
    {
        Demo.Ensure();
        W = GetViewportRect().Size.X; H = GetViewportRect().Size.Y;
        display = Ink.Display; ui = Sys(new[] { "Bahnschrift", "Segoe UI" }, 500, stretch: 87);
        var noise = new NoiseTexture2D { Width = 512, Height = 512, Seamless = true, Noise = new FastNoiseLite { Frequency = 0.012f } };
        bg = ShaderRect(this, Bg, Vector2.Zero, new Vector2(W, H)).Mat().P("aspect", W / H).P("noise_tex", noise);
        bool setup = Demo.Arg("screen") == "setup";
        if (setup) { bg.P("top", 0.52f).P("bottom", 0.48f); Setup(); return; }

        Side(Art("Black Luster Soldier - Envoy of the Beginning"), new Vector2(-80, 120), 820, -1);
        Side(Art("Chaos Emperor Dragon - Envoy of the End"), new Vector2(W - 760, 300), 860, 1);

        var logo = Tex("res://assets/title/logo_dark.png");
        Ink.Pic(logo, new Vector2(W - 640, 50), new Vector2(560, 560f * logo.GetHeight() / logo.GetWidth()), this);
        Say(this, "LIGHT", display, 30, new Color(0.3f, 0.22f, 0.1f, 0.7f), new Vector2(60, 50));
        Say(this, "DARKNESS", display, 30, new Color(0.85f, 0.75f, 1f, 0.55f), new Vector2(W - 260, H - 80));

        // Menu items centred on the crack.
        for (int i = 0; i < Items.Length; i++)
        {
            int k = i;
            float y = 0.2f + i * 0.085f;
            var m = new ShaderMaterial { Shader = textShader ??= new Shader { Code = TextInv } }.P("aspect", W / H);
            var l = Say(this, Items[i].ToUpperInvariant(), display, 50, Colors.White, new Vector2(CrackX(y) * W - 400, y * H - 34), new Vector2(800, 68), HorizontalAlignment.Center);
            l.Material = m; l.MouseFilter = MouseFilterEnum.Stop; l.MouseEntered += () => SetFocus(k);
            l.PivotOffset = l.Size / 2;
            items.Add((l, m, y));
        }
        blurb = Say(this, "", ui, 24, new Color(0.9f, 0.85f, 1f, 0.8f), new Vector2(W * 0.5f + 40, H * 0.92f), new Vector2(700, 34));
        Profile();
        SetFocus(0);
    }

    void Side(Texture2D art, Vector2 pos, float size, float side)
    {
        var r = Ink.Pic(art, pos, new Vector2(size, size), this);
        r.StretchMode = TextureRect.StretchModeEnum.Scale;
        r.Material = new ShaderMaterial { Shader = new Shader { Code = ArtSide } }.P("side", side).P("aspect", W / H);
        if (Demo.Arg("screen") == "setup") r.Mat().P("top", 0.52f).P("bottom", 0.48f);
    }

    void Profile()
    {
        var f = Cosmetics.Framed(Settings.Avatar, Session.Profile.Frame, 90);
        f.Position = new Vector2(50, H - 130);
        AddChild(f);
        Say(this, Settings.PlayerName.ToUpperInvariant(), display, 30, new Color(0.15f, 0.1f, 0.05f), new Vector2(152, H - 118));
        Say(this, $"{Session.Profile.Dp:N0} DP", ui, 20, new Color(0.5f, 0.32f, 0.05f), new Vector2(154, H - 78));
    }

    protected override void OnFocus(int old, int now)
    {
        if (items.Count == 0) return;
        for (int i = 0; i < items.Count; i++)
        {
            var (l, m, _) = items[i];
            bool on = i == now;
            m.P("lit", on ? 1f : 0f);
            CreateTween().TweenProperty(l, "scale", on ? new Vector2(1.18f, 1.18f) : Vector2.One, 0.25).SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
            l.Modulate = new Color(1, 1, 1, on ? 1 : 0.85f);
        }
        bg.P("focus_y", items[now].Y + 0.002f);
        blurb.Text = Blurb[now];
    }

    // ---------------------------------------------------------------- duel setup: you in the light, rival in the dark

    void Setup()
    {
        var you = Cosmetics.Framed(Settings.Avatar, Session.Profile.Frame, 360);
        you.Position = new Vector2(W * 0.25f - 180, H * 0.2f);
        AddChild(you);
        var rival = Tex("res://assets/rivals/kaiba.png");
        float rh = H * 0.8f, rw = rh * rival.GetWidth() / rival.GetHeight();
        var rp = Ink.Pic(rival, new Vector2(W * 0.74f - rw / 2, H - rh), new Vector2(rw, rh), this);
        rp.StretchMode = TextureRect.StretchModeEnum.Scale;
        rp.Material = new ShaderMaterial { Shader = new Shader { Code = @"shader_type canvas_item;
void fragment(){ vec4 a = texture(TEXTURE, UV); float rim = smoothstep(0.55, 1.0, UV.x) * 0.5;
 COLOR = vec4(mix(a.rgb * vec3(0.8, 0.75, 1.0), vec3(0.75, 0.5, 1.0), rim * 0.4), a.a); }" } };
        Say(this, Settings.PlayerName.ToUpperInvariant(), display, 64, new Color(0.12f, 0.08f, 0.03f), new Vector2(W * 0.25f - 400, H * 0.2f + 380), new Vector2(800, 80), HorizontalAlignment.Center);
        Say(this, "DECK  ·  " + ((Session.Playing ?? Session.Decks.FirstOrDefault())?.Name.ToUpperInvariant() ?? "—"), ui, 28, new Color(0.35f, 0.25f, 0.1f), new Vector2(W * 0.25f - 400, H * 0.2f + 470), new Vector2(800, 40), HorizontalAlignment.Center);
        Say(this, "KAIBA", display, 90, new Color(0.92f, 0.88f, 1f), new Vector2(W * 0.56f, 70));
        Say(this, "HARD  ·  BLUE-EYES DECK", ui, 26, new Color(0.75f, 0.6f, 1f), new Vector2(W * 0.56f + 6, 180));
        var vs = Say(this, "VS", display, 150, Colors.White, new Vector2(W / 2 - 200, H * 0.42f), new Vector2(400, 180), HorizontalAlignment.Center);
        vs.AddThemeColorOverride("font_outline_color", new Color(0.6f, 0.3f, 1f)); vs.AddThemeConstantOverride("outline_size", 18);
        // Mode words along the bottom of the light side, START on the crack.
        string[] modes = { "SINGLE DUEL", "MATCH (BEST OF 3)" };
        for (int i = 0; i < 2; i++)
            Say(this, modes[i], display, 34, i == 0 ? new Color(0.5f, 0.3f, 0.02f) : new Color(0.25f, 0.18f, 0.08f, 0.6f), new Vector2(80, H - 220 + i * 56));
        var go = Say(this, "BEGIN THE DUEL", display, 56, Colors.White, new Vector2(W / 2 - 400, H - 130), new Vector2(800, 80), HorizontalAlignment.Center);
        go.Material = new ShaderMaterial { Shader = new Shader { Code = TextInv } }.P("aspect", W / H).P("lit", 1f).P("top", 0.52f).P("bottom", 0.48f);
    }
}
