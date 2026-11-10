using Godot;
using PoC.Engine;
using static PoC.Game.UI.ConceptKit;

namespace PoC.Game.UI;

/// Concept 4 "HOLO ARENA": the anime's hologram duels. A night stadium, a glowing duel-disk platform on a grid
/// floor, and the menu item's monster projected above it as a flickering hologram. Bold slanted panels, HUD
/// brackets, anime energy. Inner screen: rival select as slanted portrait slices.
/// `-- --demo [--screen=menu|rivals] [--hover=N]`
public partial class HoloArena : MenuBase
{
    const string Arena = @"shader_type canvas_item;
uniform float aspect = 1.6;
uniform vec2 pad = vec2(0.36, 0.78);
float h21(vec2 p) { vec3 q = fract(vec3(p.xyx) * 0.1031); q += dot(q, q.yzx + 33.33); return fract((q.x + q.y) * q.z); }
void fragment() {
  vec2 uv = UV; float t = TIME;
  float hz = 0.58;                                   // horizon
  vec3 sky = mix(vec3(0.0, 0.02, 0.06), vec3(0.02, 0.08, 0.2), smoothstep(0.0, hz, uv.y));
  // stadium rim lights: bokeh dots along a band above the horizon
  vec2 g = vec2(uv.x * 70.0, (uv.y - 0.42) * 70.0);
  vec2 cell = floor(g); vec2 f = fract(g) - 0.5;
  float on = step(0.55, h21(cell)) * step(abs(uv.y - 0.47), 0.07);
  float bok = smoothstep(0.42, 0.1, length(f)) * on * (0.4 + 0.6 * h21(cell + 7.0));
  sky += vec3(0.5, 0.8, 1.0) * bok * 0.35 * (0.8 + 0.2 * sin(t * 2.0 + cell.x));
  // sweeping searchlights
  for (int i = 0; i < 3; i++) {
    float fi = float(i);
    vec2 o = vec2(0.15 + fi * 0.35, hz);
    float a = sin(t * (0.25 + fi * 0.07) + fi * 2.0) * 0.5;
    vec2 d = uv - o;
    float ang = atan(d.x, -d.y) - a;
    float beam = exp(-ang * ang * 60.0) * step(d.y, 0.0) * smoothstep(0.0, -0.5, d.y);
    sky += vec3(0.4, 0.7, 1.0) * beam * 0.12;
  }
  vec3 c = sky;
  // floor: perspective grid below the horizon
  if (uv.y > hz) {
    float z = (uv.y - hz);
    vec2 w = vec2((uv.x - 0.5) * aspect / z, 1.0 / z);
    vec2 gl = abs(fract(w * vec2(0.6, 0.6) - vec2(0.0, t * 0.15)) - 0.5);
    float line = smoothstep(0.03 / (z * 6.0 + 0.2), 0.0, min(gl.x, gl.y) - 0.0);
    c = mix(vec3(0.0, 0.015, 0.04), vec3(0.0, 0.04, 0.09), z * 2.0) + vec3(0.1, 0.55, 1.0) * line * 0.35 * smoothstep(0.0, 0.25, z);
    // the duel disk pad: glowing rings on the floor
    vec2 pd = (uv - pad) * vec2(aspect, 3.2) * 1.9;
    float r = length(pd);
    float ring = exp(-pow((r - 0.42) * 40.0, 2.0)) + exp(-pow((r - 0.3) * 60.0, 2.0)) * 0.6 + exp(-pow((r - 0.55) * 80.0, 2.0)) * 0.4;
    float seg = step(0.5, fract(atan(pd.y, pd.x) * 3.0 / 3.14159 + t * 0.2));
    c += vec3(0.25, 0.8, 1.0) * ring * (0.6 + 0.4 * seg) + vec3(0.1, 0.4, 0.9) * exp(-r * 4.0) * 0.5;
  }
  // light cone from the pad up to the hologram
  vec2 cp = uv - pad;
  float cone = smoothstep(0.24, 0.0, abs(cp.x * aspect) - (-cp.y) * 0.35) * step(cp.y, 0.0) * smoothstep(-0.65, 0.0, cp.y);
  c += vec3(0.2, 0.6, 1.0) * cone * 0.18;
  COLOR = vec4(c, 1.0);
}";
    const string Holo = @"shader_type canvas_item;
uniform float boot = 1.0;     // 0 -> 1 when it materialises
float h21(vec2 p) { vec3 q = fract(vec3(p.xyx) * 0.1031); q += dot(q, q.yzx + 33.33); return fract((q.x + q.y) * q.z); }
void fragment() {
  float t = TIME;
  float glitch = step(0.96, h21(vec2(floor(t * 12.0), 3.0))) * (h21(vec2(floor(UV.y * 40.0), floor(t * 12.0))) - 0.5) * 0.04;
  vec2 uv = UV + vec2(glitch, 0.0);
  float r = texture(TEXTURE, uv + vec2(0.004, 0.0)).r, g = texture(TEXTURE, uv).g, b = texture(TEXTURE, uv - vec2(0.004, 0.0)).b;
  vec3 c = vec3(r, g, b);
  float l = dot(c, vec3(0.3, 0.59, 0.11));
  c = mix(c, vec3(0.3, 0.75, 1.0) * l * 1.6, 0.35);
  float scan = 0.82 + 0.18 * sin(UV.y * 520.0 + t * 8.0);
  float edge = smoothstep(0.0, 0.08, UV.x) * smoothstep(1.0, 0.92, UV.x) * smoothstep(0.0, 0.06, UV.y) * smoothstep(1.0, 0.7, UV.y);
  float build = 1.0 - smoothstep(boot * 1.2 - 0.2, boot * 1.2, 1.0 - UV.y);
  float flick = 0.9 + 0.1 * h21(vec2(floor(t * 24.0), 1.0));
  float a = edge * build * flick * 0.92;
  c += vec3(0.4, 0.85, 1.0) * smoothstep(0.03, 0.0, abs((1.0 - UV.y) - boot * 1.2 + 0.1)) * 1.5;   // build line
  COLOR = vec4(c * scan * 1.15, a);
}";
    /// Parallelogram panel (skewed), filled or outlined.
    const string Slant = @"shader_type canvas_item;
uniform float skew = 0.18;
uniform vec4 fill : source_color = vec4(0.0, 0.0, 0.0, 0.6);
uniform vec4 edge : source_color = vec4(0.3, 0.8, 1.0, 1.0);
uniform float lit = 0.0;
void fragment() {
  float x = UV.x - (1.0 - UV.y) * skew;
  float inside = step(0.0, x) * step(x, 1.0 - skew);
  float rim = inside * (1.0 - step(0.012, x) * step(x, 1.0 - skew - 0.012) * step(0.06, UV.y) * step(UV.y, 0.94));
  vec4 f = mix(fill, vec4(0.95, 0.98, 1.0, 1.0), lit);
  COLOR = mix(f * inside, edge, rim * (0.5 + lit * 0.5));
  COLOR.a = max(f.a * inside, rim * edge.a);
}";
    /// Portrait slice: skewed mask, desaturated + dark unless lit.
    const string Slice = @"shader_type canvas_item;
uniform float skew = 0.12;
uniform float lit = 0.0;
uniform vec4 tint : source_color = vec4(0.2, 0.6, 1.0, 1.0);
void fragment() {
  float x = UV.x - (1.0 - UV.y) * skew;
  float inside = step(0.0, x) * step(x, 1.0 - skew);
  vec4 a = texture(TEXTURE, UV);
  float l = dot(a.rgb, vec3(0.3, 0.59, 0.11));
  vec3 c = mix(tint.rgb * l * 0.7, a.rgb, lit);
  float border = inside * (1.0 - step(0.01, x) * step(x, 1.0 - skew - 0.01));
  COLOR = vec4(mix(c, vec3(1.0), border * 0.9), max(a.a * inside * (0.55 + 0.45 * lit), border));
}";

    float W, H;
    Font impact = null!, cond = null!;
    readonly List<(ColorRect Bar, Label L, ShaderMaterial M)> items = new();
    TextureRect holo = null!;
    Label holoName = null!, blurb = null!;
    readonly List<(TextureRect Pic, ShaderMaterial M, Label Name)> slices = new();
    Shader? slant, slice;

    protected override int ItemCount => Demo.Arg("screen") == "rivals" ? 4 : Items.Length;
    protected override string? MusicPath => Md("menu_01");
    protected override void OnChoose(int i) { }
    protected override void OnBack() => GetTree().Quit();

    protected override void Build()
    {
        Demo.Ensure();
        W = GetViewportRect().Size.X; H = GetViewportRect().Size.Y;
        impact = Sys(new[] { "Impact", "Arial Black" }, 800, italic: true);
        cond = Sys(new[] { "Bahnschrift", "Segoe UI" }, 600, stretch: 75);
        var arena = ShaderRect(this, Arena, Vector2.Zero, new Vector2(W, H)).Mat().P("aspect", W / H);
        Hud();
        if (Demo.Arg("screen") == "rivals") { arena.P("pad", new Vector2(0.5f, 0.9f)); Rivals(); return; }

        // Hologram above the pad.
        float hs = H * 0.62f;
        holo = Ink.Pic(Art(Featured[0]), new Vector2(0.36f * W - hs / 2, 0.78f * H - hs - 70), new Vector2(hs, hs), this);
        holo.StretchMode = TextureRect.StretchModeEnum.Scale;
        holo.Material = new ShaderMaterial { Shader = new Shader { Code = Holo } };
        holoName = Say(this, "", impact, 34, new Color(0.6f, 0.9f, 1f), new Vector2(0.36f * W - 400, 0.78f * H + 40), new Vector2(800, 50), HorizontalAlignment.Center);

        var logo = Tex("res://assets/title/logo_dark.png");
        Ink.Pic(logo, new Vector2(60, 50), new Vector2(440, 440f * logo.GetHeight() / logo.GetWidth()), this);

        float y = 190, x = W - 640;
        for (int i = 0; i < Items.Length; i++)
        {
            int k = i;
            var m = new ShaderMaterial { Shader = slant ??= new Shader { Code = Slant } };
            var bar = ShaderRect(this, "", new Vector2(x + i * -14, y), new Vector2(580, 78));
            bar.Material = m;
            bar.MouseFilter = MouseFilterEnum.Stop; bar.MouseEntered += () => SetFocus(k);
            var l = Say(bar, Items[i].ToUpperInvariant(), impact, 42, Colors.White, new Vector2(46, 8), new Vector2(500, 60));
            items.Add((bar, l, m));
            y += 92;
        }
        blurb = Say(this, "", cond, 26, new Color(0.6f, 0.85f, 1f), new Vector2(W - 640 - 8 * 14, y + 10), new Vector2(620, 34));
        var f = Cosmetics.Framed(Settings.Avatar, Session.Profile.Frame, 86);
        f.Position = new Vector2(70, H - 130); AddChild(f);
        Say(this, Settings.PlayerName.ToUpperInvariant(), impact, 34, Colors.White, new Vector2(170, H - 122));
        Say(this, $"DUELIST LV 12   ·   {Session.Profile.Dp:N0} DP", cond, 22, new Color(0.6f, 0.85f, 1f), new Vector2(172, H - 80));
        SetFocus(0);
    }

    /// Corner brackets + ticker: the duel-disk HUD.
    void Hud()
    {
        var c = new Color(0.4f, 0.85f, 1f, 0.7f);
        foreach (var (p, sx, sy) in new[] { (new Vector2(24, 24), 1, 1), (new Vector2(W - 24, 24), -1, 1), (new Vector2(24, H - 24), 1, -1), (new Vector2(W - 24, H - 24), -1, -1) })
        {
            AddChild(new ColorRect { Position = p + new Vector2(sx < 0 ? -90 : 0, sy < 0 ? -3 : 0), Size = new Vector2(90, 3), Color = c, MouseFilter = MouseFilterEnum.Ignore });
            AddChild(new ColorRect { Position = p + new Vector2(sx < 0 ? -3 : 0, sy < 0 ? -90 : 0), Size = new Vector2(3, 90), Color = c, MouseFilter = MouseFilterEnum.Ignore });
        }
        Say(this, "KC DUEL ARENA  ·  HOLOGRAM SYSTEM ONLINE  ·  SOLID VISION v4", cond, 18, c, new Vector2(W - 760, H - 60), new Vector2(700, 26), HorizontalAlignment.Right);
    }

    protected override void OnFocus(int old, int now)
    {
        if (slices.Count > 0) { FocusSlice(now); return; }
        for (int i = 0; i < items.Count; i++)
        {
            bool on = i == now;
            items[i].M.P("lit", on ? 1f : 0f);
            items[i].L.AddThemeColorOverride("font_color", on ? new Color(0.02f, 0.15f, 0.35f) : Colors.White);
            CreateTween().TweenProperty(items[i].Bar, "position:x", W - 640 + i * -14 - (on ? 60 : 0), 0.2).SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
        }
        holo.Texture = Art(Featured[now]);
        holo.Mat().P("boot", 0f);
        CreateTween().TweenMethod(Callable.From<float>(v => holo.Mat().P("boot", v)), 0f, 1f, 0.6);
        holoName.Text = Featured[now].ToUpperInvariant();
        blurb.Text = Blurb[now].ToUpperInvariant();
    }

    // ---------------------------------------------------------------- rival select: slanted slices

    static readonly string[] Keys = { "yugi", "kaiba", "joey", "jaden" };
    static readonly Color[] Tints = { new(0.6f, 0.3f, 1f), new(0.3f, 0.6f, 1f), new(1f, 0.45f, 0.2f), new(1f, 0.25f, 0.2f) };

    void Rivals()
    {
        var logo = Tex("res://assets/title/logo_dark.png");
        Ink.Pic(logo, new Vector2(60, 40), new Vector2(300, 300f * logo.GetHeight() / logo.GetWidth()), this);
        Say(this, "SELECT YOUR OPPONENT", impact, 52, Colors.White, new Vector2(400, 50));
        float sw = (W - 160) / 4f, sh = H - 260;
        for (int i = 0; i < 4; i++)
        {
            int k = i;
            var m = new ShaderMaterial { Shader = slice ??= new Shader { Code = Slice } }.P("tint", Tints[i]);
            var pic = Ink.Pic(Tex($"res://assets/rivals/{Keys[i]}.png"), new Vector2(80 + i * sw, 160), new Vector2(sw + 24, sh), this);
            pic.StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered;
            pic.Material = m; pic.MouseFilter = MouseFilterEnum.Stop; pic.MouseEntered += () => SetFocus(k);
            var n = Say(this, Keys[i].ToUpperInvariant(), impact, 74, Colors.White, new Vector2(80 + i * sw + 20, H - 200), new Vector2(sw, 90));
            n.AddThemeColorOverride("font_outline_color", Tints[i]); n.AddThemeConstantOverride("outline_size", 10);
            slices.Add((pic, m, n));
        }
        blurb = Say(this, "", cond, 28, new Color(0.6f, 0.85f, 1f), new Vector2(80, H - 100), new Vector2(W - 160, 36), HorizontalAlignment.Center);
        SetFocus(1);
    }

    static readonly string[] Lines = { "YUGI MUTO  ·  KING OF GAMES  ·  HARD", "SETO KAIBA  ·  KAIBACORP PRESIDENT  ·  HARD", "JOEY WHEELER  ·  NEVER GIVES UP  ·  NORMAL", "JADEN YUKI  ·  SLIFER RED  ·  NORMAL" };

    void FocusSlice(int now)
    {
        for (int i = 0; i < slices.Count; i++)
        {
            bool on = i == now;
            slices[i].M.P("lit", on ? 1f : 0f);
            slices[i].Pic.ZIndex = on ? 1 : 0;
            slices[i].Name.ZIndex = 2;
            slices[i].Name.Modulate = new Color(1, 1, 1, on ? 1 : 0.45f);
            slices[i].Pic.PivotOffset = slices[i].Pic.Size / 2;
            CreateTween().TweenProperty(slices[i].Pic, "scale", on ? new Vector2(1.08f, 1.08f) : Vector2.One, 0.25).SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
        }
        blurb.Text = Lines[now];
    }
}
