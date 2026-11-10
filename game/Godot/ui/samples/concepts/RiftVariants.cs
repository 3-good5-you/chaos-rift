using Godot;
using PoC.Engine;
using static PoC.Game.UI.ConceptKit;

namespace PoC.Game.UI;

/// Chaos Rift, refined (user picked concept 2, 2026-11-08): cleaner type, calmer colour, four monster treatments.
/// `-- --demo --style=a|b|c|d [--hover=N]`
/// a = Ivory & Amethyst, Perpetua Titling, feathered duotone monsters.
/// b = Monochrome, Gill Sans tracked wide, full-colour monsters in arch windows.
/// c = Champagne & Crimson, Copperplate, monsters as banknote engravings.
/// d = Pearl & Midnight, Felix Titling, no background monsters: the chosen item's card floats in the dark.
public partial class RiftVariants : MenuBase
{
    sealed record Look(Color LightA, Color LightB, Color DarkA, Color DarkB, Color HaloLight, Color HaloDark,
        Color Ink, Color Cream, Color Accent, string[] Font, int Size, int Spacing, int Weight, int Monsters, float Rays,
        Color Kanji = default, Color NameTop = default, Color NameBottom = default, bool LogoLeft = false);

    static readonly Dictionary<string, Look> Looks = new()
    {
        ["a"] = new(new(0.97f, 0.95f, 0.91f), new(0.86f, 0.82f, 0.75f), new(0.03f, 0.02f, 0.07f), new(0.15f, 0.07f, 0.27f), new(0.95f, 0.72f, 0.38f), new(0.58f, 0.36f, 1f),
            new(0.12f, 0.09f, 0.07f), new(0.93f, 0.9f, 0.98f), new(0.72f, 0.52f, 0.2f), new[] { "Perpetua Titling MT", "Castellar" }, 38, 6, 300, 0, 0.5f,
            new(0.42f, 0.24f, 0.72f), new(1f, 0.9f, 0.62f), new(0.72f, 0.5f, 0.2f)),
        ["b"] = new(new(0.985f, 0.985f, 0.98f), new(0.88f, 0.88f, 0.87f), new(0.025f, 0.025f, 0.03f), new(0.09f, 0.09f, 0.11f), new(0.85f, 0.75f, 0.55f), new(0.7f, 0.72f, 0.8f),
            new(0.06f, 0.06f, 0.07f), new(0.95f, 0.95f, 0.96f), new(0.75f, 0.6f, 0.32f), new[] { "Gill Sans MT", "Segoe UI" }, 32, 12, 300, 1, 0f,
            new(0.82f, 0.82f, 0.84f), new(1f, 1f, 1f), new(0.62f, 0.64f, 0.7f)),
        ["c"] = new(new(0.95f, 0.9f, 0.79f), new(0.82f, 0.73f, 0.56f), new(0.03f, 0.008f, 0.012f), new(0.17f, 0.025f, 0.04f), new(1f, 0.75f, 0.35f), new(0.9f, 0.62f, 0.32f),
            new(0.18f, 0.1f, 0.04f), new(0.96f, 0.88f, 0.78f), new(0.85f, 0.18f, 0.2f), new[] { "Copperplate Gothic Light", "Copperplate Gothic" }, 34, 5, 400, 2, 0.35f,
            new(0.62f, 0.06f, 0.09f), new(1f, 0.92f, 0.7f), new(0.78f, 0.55f, 0.25f)),
        ["d"] = new(new(0.95f, 0.96f, 0.99f), new(0.82f, 0.86f, 0.93f), new(0.015f, 0.02f, 0.06f), new(0.07f, 0.06f, 0.2f), new(0.6f, 0.75f, 1f), new(0.45f, 0.5f, 1f),
            new(0.06f, 0.08f, 0.15f), new(0.9f, 0.93f, 1f), new(0.4f, 0.6f, 0.95f), new[] { "Felix Titling", "Perpetua Titling MT" }, 38, 6, 400, 3, 0f,
            new(0.22f, 0.38f, 0.9f), new(0.2f, 0.26f, 0.45f), new(0.06f, 0.08f, 0.18f), LogoLeft: true),
    };

    const float Top = 0.58f, Bottom = 0.44f;
    internal const string CrackFn = @"
uniform float top = 0.58;
uniform float bottom = 0.44;
uniform float aspect = 1.6;
float h21(vec2 p) { vec3 q = fract(vec3(p.xyx) * 0.1031); q += dot(q, q.yzx + 33.33); return fract((q.x + q.y) * q.z); }
float n1(float x) { float i = floor(x); float f = fract(x); f = f*f*(3.0-2.0*f); return mix(h21(vec2(i, 1.7)), h21(vec2(i + 1.0, 1.7)), f); }
float crack_d(vec2 suv, float t) {
  float base = mix(top, bottom, suv.y);
  float j = (n1(suv.y * 7.0 + 3.0) - 0.5) * 0.022 + (n1(suv.y * 31.0 + t * 0.5) - 0.5) * 0.005;
  return (suv.x - base - j) * aspect;
}
";
    internal const string Bg = @"shader_type canvas_item;
uniform vec4 light_a : source_color; uniform vec4 light_b : source_color;
uniform vec4 dark_a : source_color; uniform vec4 dark_b : source_color;
uniform vec4 halo_l : source_color; uniform vec4 halo_d : source_color;
uniform float rays = 0.5;
uniform float focus_y = -1.0;
uniform float draw = 1.0;
uniform float reveal = 1.0;
uniform float specks = 0.0;
uniform float smoke = 1.0;    // dark-side smoke/colour (0 = clean black void for the intro)
uniform float burst = 0.0;    // the moment the rift tears: everything on the crack flares
uniform sampler2D noise_tex : repeat_enable, filter_linear;
" + CrackFn + @"
float fbm(vec2 p) { float a = 0.5, s = 0.0; for (int i = 0; i < 4; i++) { s += a * texture(noise_tex, p).r; p *= 2.03; a *= 0.5; } return s; }
void fragment() {
  vec2 uv = UV; float t = TIME;
  float d = crack_d(uv, t);
  vec2 lp = uv - vec2(-0.05, -0.1);
  float ang = atan(lp.y, lp.x);
  float r = pow(abs(sin(ang * 16.0 + t * 0.04)), 24.0) * exp(-length(lp) * 1.4);
  vec3 light = mix(light_a.rgb, light_b.rgb, smoothstep(0.0, 1.2, length(lp))) + halo_l.rgb * r * rays * 0.25;
  float sm = fbm(uv * vec2(1.6, 1.1) + vec2(-t * 0.01, t * 0.004));
  vec3 dark = mix(dark_a.rgb, dark_b.rgb, smoothstep(0.25, 0.85, sm) * smoothstep(1.4, 0.2, length(uv - vec2(0.85, 0.35))) * smoke);
  dark = mix(vec3(0.008, 0.008, 0.012), dark, smoke);
  dark += step(0.998, h21(floor(uv * vec2(900.0, 560.0)))) * vec3(0.7) * (0.4 + 0.4 * sin(t + uv.x * 70.0));
  light = mix(light, vec3(0.16, 0.16, 0.19), step(0.9984, h21(floor(uv * vec2(900.0, 560.0)) + 17.0)) * specks * (0.55 + 0.35 * sin(t * 1.3 + uv.y * 80.0)));
  light = mix(light, vec3(0.35, 0.35, 0.4), step(0.99965, h21(floor(uv * vec2(450.0, 280.0)) + 5.0)) * specks * 0.5);
  float lit_side = step(d, 0.0) * smoothstep(-reveal * 3.2 - 0.001, -reveal * 3.2 + 0.08, d);
  vec3 c = mix(dark, light, lit_side);
  c = mix(c, c * 0.8, smoothstep(-0.12, 0.0, d) * step(d, 0.0) * 0.5 * reveal);
  c += halo_d.rgb * exp(-max(d, 0.0) * 12.0) * 0.22 * step(0.0, d) * draw;
  float flare = focus_y < 0.0 ? 0.0 : exp(-pow((uv.y - focus_y) * 12.0, 2.0));
  float core = (1.0 + burst * 2.5) * exp(-abs(d) * max(420.0 - 200.0 * flare - 300.0 * burst, 60.0)) * (0.9 + 0.1 * n1(t * 6.0 + uv.y * 3.0));
  float halo = exp(-abs(d) * max(34.0 - 22.0 * burst, 10.0)) * (0.35 + flare * 0.6 + burst * 2.0) * (d < 0.0 ? 0.45 : 1.0);
  float torn = smoothstep(draw * 1.15, draw * 1.15 - 0.06, uv.y);
  float tip = exp(-pow((uv.y - draw * 1.15) * 30.0, 2.0)) * step(draw, 0.99) * step(0.001, draw);
  c += (vec3(1.0) * core * 1.3 + (d < 0.0 ? halo_l.rgb : halo_d.rgb) * halo * 0.5) * torn + vec3(1.0, 0.95, 1.0) * tip * exp(-abs(d) * 30.0) * 2.0;
  COLOR = vec4(c, 1.0);
}";
    /// Text / hairlines that switch colour across the crack.
    internal const string Inv = @"shader_type canvas_item;
uniform vec4 ink : source_color; uniform vec4 cream : source_color; uniform vec4 accent : source_color;
uniform float lit = 0.0;
uniform float dim = 1.0;
" + CrackFn + @"
void fragment() {
  float a = texture(TEXTURE, UV).a * COLOR.a;
  float d = crack_d(SCREEN_UV, TIME);
  vec3 c = d < 0.0 ? ink.rgb : cream.rgb;
  c = mix(c, accent.rgb * (d < 0.0 ? 0.85 : 1.25), lit);
  COLOR = vec4(c, a * mix(dim, 1.0, lit));
}";
    /// Monster art on one side of the crack. mode 0 feathered duotone, 1 arch window, 2 engraving.
    const string Side = @"shader_type canvas_item;
uniform float side = 1.0;
uniform int mode = 0;
uniform vec4 tone_dark : source_color; uniform vec4 tone_light : source_color; uniform vec4 frame : source_color;
" + CrackFn + @"
void fragment() {
  vec4 a = texture(TEXTURE, UV);
  float d = crack_d(SCREEN_UV, TIME) * side;
  float keep = smoothstep(0.02, 0.12, d);
  float l = dot(a.rgb, vec3(0.3, 0.59, 0.11));
  vec2 p = UV - 0.5;
  vec3 c; float alpha;
  if (mode == 0) {
    float m = smoothstep(0.5, 0.22, length(p * vec2(1.0, 0.85))) ;
    c = mix(tone_dark.rgb, tone_light.rgb, smoothstep(0.08, 0.85, l));
    alpha = m * 0.8;
  } else if (mode == 1) {
    // tall arch: rectangle with a round top, hairline frame
    vec2 q = vec2(abs(p.x) / 0.36, p.y);
    float arch = q.y > -0.14 ? (q.x <= 1.0 && q.y < 0.47 ? 1.0 : 0.0) : (length(vec2(abs(p.x), p.y + 0.14) / vec2(0.36, 0.33)) <= 1.0 ? 1.0 : 0.0);
    vec2 q2 = vec2(abs(p.x) / 0.375, p.y);
    float outer = q2.y > -0.14 ? (q2.x <= 1.0 && q2.y < 0.485 ? 1.0 : 0.0) : (length(vec2(abs(p.x), p.y + 0.14) / vec2(0.375, 0.345)) <= 1.0 ? 1.0 : 0.0);
    vec3 art = mix(vec3(l), a.rgb, 0.85);
    c = mix(frame.rgb, art, arch);
    alpha = max(arch, (outer - arch) * frame.a);
  } else {
    // engraving: line density follows darkness
    float lines = fract((UV.y + UV.x * 0.25) * 150.0);
    float ink = step(lines, (1.0 - l) * 0.9);
    float m = smoothstep(0.5, 0.3, length(p * vec2(1.0, 0.85)));
    c = tone_dark.rgb;
    alpha = ink * m * 0.85;
  }
  COLOR = vec4(c, alpha * keep);
}";

    float W, H;
    Look look = null!;
    string style = "a";
    ShaderMaterial bg = null!;
    Font font = null!, small = null!;
    readonly List<(Label L, ShaderMaterial M, float Y)> items = new();
    ColorRect mark = null!;
    Label blurb = null!;
    TextureRect? card;
    Shader? inv;

    protected override string? MusicPath => Md("menu_01");
    protected override void OnChoose(int i) { }
    protected override void OnBack() => GetTree().Quit();

    ShaderMaterial InvMat(float dim = 1f) => new ShaderMaterial { Shader = inv ??= new Shader { Code = Inv } }
        .P("aspect", W / H).P("ink", look.Ink).P("cream", look.Cream).P("accent", look.Accent).P("dim", dim);

    protected override void Build()
    {
        Demo.Ensure();
        W = GetViewportRect().Size.X; H = GetViewportRect().Size.Y;
        style = Demo.Arg("style", "a");
        look = Looks[style];
        font = new FontVariation { BaseFont = Sys(look.Font, look.Weight), SpacingGlyph = look.Spacing };
        small = new FontVariation { BaseFont = Sys(new[] { "Segoe UI" }, 400), SpacingGlyph = 2 };
        var noise = new NoiseTexture2D { Width = 512, Height = 512, Seamless = true, Noise = new FastNoiseLite { Frequency = 0.01f } };
        bg = ShaderRect(this, Bg, Vector2.Zero, new Vector2(W, H)).Mat().P("aspect", W / H).P("noise_tex", noise)
            .P("light_a", look.LightA).P("light_b", look.LightB).P("dark_a", look.DarkA).P("dark_b", look.DarkB)
            .P("halo_l", look.HaloLight).P("halo_d", look.HaloDark).P("rays", look.Rays);

        if (look.Monsters < 3)
        {
            Monster("Black Luster Soldier - Envoy of the Beginning", -1);
            Monster("Chaos Emperor Dragon - Envoy of the End", 1);
        }

        Wordmark(look.LogoLeft ? new Vector2(70, 50) : new Vector2(W - 680, 46), 600);

        // Menu on the crack.
        mark = new ColorRect { Size = new Vector2(220, 1.5f), MouseFilter = MouseFilterEnum.Ignore, Material = InvMat() };
        AddChild(mark);
        for (int i = 0; i < Items.Length; i++)
        {
            int k = i;
            float y = 0.27f + i * 0.072f;
            var m = InvMat(0.82f);
            float cx = Mathf.Lerp(Top, Bottom, y) * W;
            var l = Say(this, Items[i].ToUpperInvariant(), font, look.Size, Colors.White, new Vector2(cx - 400, y * H - look.Size * 0.7f), new Vector2(800, look.Size * 1.5f), HorizontalAlignment.Center);
            l.Material = m; l.MouseFilter = MouseFilterEnum.Stop; l.MouseEntered += () => SetFocus(k);
            items.Add((l, m, y));
        }
        float by = 0.27f + Items.Length * 0.072f + 0.03f;
        blurb = Say(this, "", small, 19, Colors.White, new Vector2(Mathf.Lerp(Top, Bottom, by) * W - 400, by * H), new Vector2(800, 30), HorizontalAlignment.Center);
        blurb.Material = InvMat(0.6f);

        // Profile, bottom left on the light side.
        var f = Cosmetics.Framed(Settings.Avatar, Session.Profile.Frame, 70);
        f.Position = new Vector2(56, H - 112); AddChild(f);
        var nm = Say(this, Settings.PlayerName.ToUpperInvariant(), font, 22, look.Ink, new Vector2(140, H - 102));
        Say(this, $"{Session.Profile.Dp:N0} DP", small, 17, look.Ink * new Color(1, 1, 1, 0.6f), new Vector2(141, H - 70));
        SetFocus(0);
    }

    /// The Yu-Gi-Oh! logo with its kanji recoloured, and the game name set in this look's font and metal.
    void Wordmark(Vector2 at, float w)
    {
        var src = Tex("res://assets/title/yugioh_logo.png");
        var crop = new AtlasTexture { Atlas = src, Region = new Rect2(40, 400, 3760, 1350) };
        float h = w * 1350 / 3760f;
        var logo = Ink.Pic(crop, at, new Vector2(w, h), this);
        logo.StretchMode = TextureRect.StretchModeEnum.Scale;
        logo.Material = new ShaderMaterial { Shader = new Shader { Code = @"shader_type canvas_item;
uniform vec4 kanji : source_color;
void fragment() {
  vec4 c = texture(TEXTURE, UV);
  float red = smoothstep(0.25, 0.5, c.r - max(c.g, c.b));
  float l = dot(c.rgb, vec3(0.3, 0.59, 0.11));
  COLOR = vec4(mix(c.rgb, kanji.rgb * (0.75 + l * 0.6), red), c.a);
}" } }.P("kanji", look.Kanji);
        var name = Say(this, "CHAOS ASCENDANT", new FontVariation { BaseFont = Sys(look.Font, 400), SpacingGlyph = 10 }, 44, Colors.White, at + new Vector2(0, h - 4), new Vector2(w, 60), HorizontalAlignment.Center);
        name.Material = new ShaderMaterial { Shader = new Shader { Code = @"shader_type canvas_item;
uniform vec4 top_c : source_color; uniform vec4 bot_c : source_color;
void fragment() {
  float a = texture(TEXTURE, UV).a * COLOR.a;
  vec2 px = TEXTURE_PIXEL_SIZE;
  float y = fract(SCREEN_UV.y * 30.0);
  vec3 c = mix(top_c.rgb, bot_c.rgb, smoothstep(0.2, 0.8, UV.y));
  c += exp(-pow((SCREEN_UV.x - fract(TIME * 0.08) * 1.6 + 0.3) * 18.0, 2.0)) * 0.35;
  COLOR = vec4(c, a);
}" } }.P("top_c", look.NameTop).P("bot_c", look.NameBottom);
        // hairline + diamond under the name
        var line = new ColorRect { Position = at + new Vector2(w * 0.25f, h + 62), Size = new Vector2(w * 0.6f, 1.5f), Color = look.NameBottom with { A = 0.7f }, MouseFilter = MouseFilterEnum.Ignore };
        AddChild(line);
    }

    void Monster(string name, float side)
    {
        float s = look.Monsters == 1 ? 640 : 900;
        var pos = side < 0
            ? new Vector2(look.Monsters == 1 ? 150 : -60, look.Monsters == 1 ? 170 : 110)
            : new Vector2(W - s + (look.Monsters == 1 ? -150 : 40), look.Monsters == 1 ? 300 : 260);
        var r = Ink.Pic(Art(name), pos, new Vector2(s, look.Monsters == 1 ? s * 1.15f : s), this);
        r.StretchMode = look.Monsters == 1 ? TextureRect.StretchModeEnum.KeepAspectCovered : TextureRect.StretchModeEnum.Scale;
        Color dark = side < 0 ? look.LightB.Darkened(0.45f).Lerp(look.Accent, 0.25f) : look.DarkA, lightT = side < 0 ? look.LightA : look.HaloDark.Lightened(0.2f);
        if (look.Monsters == 2) dark = side < 0 ? look.Ink * new Color(1, 1, 1, 0.9f) : look.HaloDark;
        r.Material = new ShaderMaterial { Shader = new Shader { Code = Side } }.P("aspect", W / H).P("side", side).P("mode", look.Monsters)
            .P("tone_dark", dark).P("tone_light", lightT).P("frame", look.Accent with { A = 0.9f });
    }

    protected override void OnFocus(int old, int now)
    {
        for (int i = 0; i < items.Count; i++)
            items[i].M.P("lit", i == now ? 1f : 0f);
        var (l, _, y) = items[now];
        bg.P("focus_y", y);
        float w = font.GetStringSize(l.Text, HorizontalAlignment.Left, -1, look.Size).X + 60;
        mark.Size = new Vector2(w, 1.5f);
        mark.Position = new Vector2(Mathf.Lerp(Top, Bottom, y) * W - w / 2, y * H + look.Size * 0.62f);
        mark.Scale = new Vector2(0, 1); mark.PivotOffset = new Vector2(w / 2, 0);
        CreateTween().TweenProperty(mark, "scale:x", 1f, 0.3).SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
        blurb.Text = Blurb[now].ToUpperInvariant();
        if (look.Monsters == 3) Featured_(now);
    }

    /// Style d: the item's card floats on the dark side, slight tilt, soft glow behind.
    void Featured_(int now)
    {
        card?.QueueFree();
        float h = H * 0.56f, w = h * 0.686f;
        var at = new Vector2(W * 0.78f - w / 2, H * 0.36f);
        card = Ink.Pic(Full(Featured[now]), at, new Vector2(w, h), this);
        card.StretchMode = TextureRect.StretchModeEnum.Scale;
        card.PivotOffset = card.Size / 2;
        card.Rotation = 0.06f;
        card.Modulate = new Color(1, 1, 1, 0);
        var glow = ShaderRect(card, "shader_type canvas_item; uniform vec4 c : source_color; void fragment(){ vec2 p = (UV - 0.5) * 2.0; COLOR = vec4(c.rgb, exp(-dot(p, p) * 2.2) * 0.45); }", new Vector2(-w * 0.35f, -h * 0.25f), new Vector2(w * 1.7f, h * 1.5f));
        glow.Mat().P("c", look.HaloDark);
        glow.ShowBehindParent = true;
        var tw = CreateTween().SetParallel();
        tw.TweenProperty(card, "modulate:a", 1f, 0.35);
        tw.TweenProperty(card, "position:y", at.Y - 14, 0.5).From(at.Y + 20).SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
    }
}
