using Godot;
using PoC.Engine;
using static PoC.Game.UI.ConceptKit;

namespace PoC.Game.UI;

/// Concept 3 "MILLENNIUM STONE": the Pharaoh's memory. A torch-lit sandstone wall of carved glyphs; the featured
/// monster is carved into it as a stone relief that the torchlight rakes across; menu words are engraved and the
/// chosen one fills with gold leaf. Inner screen: the card list as tablets in stone niches beside a papyrus.
/// `-- --demo [--screen=menu|cards] [--hover=N]`
public partial class MillenniumStone : MenuBase
{
    const string Torches = @"
uniform vec2 torch_a = vec2(0.06, 0.32);
uniform vec2 torch_b = vec2(0.94, 0.32);
uniform float aspect = 1.6;
float h21(vec2 p) { vec3 q = fract(vec3(p.xyx) * 0.1031); q += dot(q, q.yzx + 33.33); return fract((q.x + q.y) * q.z); }
float flick(float t, float s) { return 0.85 + 0.1 * sin(t * 11.0 + s) + 0.05 * sin(t * 23.0 + s * 3.0); }
vec3 torch_light(vec2 suv, vec3 n, float t) {
  vec3 col = vec3(0.0);
  for (int i = 0; i < 2; i++) {
    vec2 tp = i == 0 ? torch_a : torch_b;
    vec3 l = vec3((tp - suv) * vec2(aspect, 1.0), 0.35);
    float dist = length(l);
    float k = max(dot(n, normalize(l)), 0.0) / (0.4 + dist * dist * 2.2);
    col += vec3(1.0, 0.72, 0.42) * k * flick(t, float(i) * 4.0);
  }
  return col;
}";
    const string Wall = @"shader_type canvas_item;
uniform sampler2D grain : repeat_enable, filter_linear;
" + Torches + @"
float glyph(vec2 p) {
  // carved glyph cells: each cell a random mix of ring, bar, wave, triangle (depth 0..1)
  vec2 cell = floor(p); vec2 f = fract(p) - 0.5;
  float r = h21(cell);
  if (r < 0.25) return 0.0;
  float d = 1.0;
  float k = fract(r * 7.0);
  if (k < 0.25) d = abs(length(f) - 0.28) - 0.05;
  else if (k < 0.5) d = max(abs(f.x) - 0.06, abs(f.y) - 0.34);
  else if (k < 0.7) d = abs(f.y - 0.12 * sin(f.x * 14.0)) - 0.05;
  else if (k < 0.85) d = max(abs(f.x) * 0.9 + f.y * 0.55 - 0.2, -f.y - 0.25);
  else d = min(length(f - vec2(0.0, -0.18)) - 0.11, max(abs(f.x) - 0.05, abs(f.y - 0.1) - 0.22));
  return smoothstep(0.03, -0.02, d);
}
float height(vec2 uv) {
  vec2 px = uv * vec2(aspect, 1.0);
  // running-bond blocks
  vec2 b = px * vec2(3.2, 6.4);
  b.x += step(1.0, mod(floor(b.y), 2.0)) * 0.5;
  vec2 bf = fract(b);
  float mortar = smoothstep(0.0, 0.04, bf.x) * smoothstep(1.0, 0.96, bf.x) * smoothstep(0.0, 0.07, bf.y) * smoothstep(1.0, 0.93, bf.y);
  float g = glyph(px * 18.0) * step(0.82, h21(floor(b) + 3.0));
  float n = texture(grain, px * 1.7).r * 0.5 + texture(grain, px * 7.0).r * 0.25;
  return mortar * 0.45 + n * 0.12 - g * 0.3;
}
void fragment() {
  float e = 0.0015;
  float h = height(UV);
  vec3 n = normalize(vec3(height(UV - vec2(e, 0.0)) - height(UV + vec2(e, 0.0)), height(UV - vec2(0.0, e)) - height(UV + vec2(0.0, e)), e * 10.0));
  vec3 stone = mix(vec3(0.5, 0.42, 0.32), vec3(0.74, 0.64, 0.5), texture(grain, UV * vec2(aspect, 1.0) * 3.0).r);
  vec3 lit = stone * (vec3(0.3, 0.26, 0.24) + torch_light(UV, n, TIME) * 1.1);
  lit *= 0.65 + 0.35 * smoothstep(-0.1, 0.8, h);
  float vig = 1.0 - 0.55 * length((UV - vec2(0.5, 0.45)) * vec2(1.2, 1.0));
  COLOR = vec4(lit * vig, 1.0);
}";
    /// Card art carved into stone: luminance is the height, raked by the same torches.
    const string Relief = @"shader_type canvas_item;
uniform sampler2D grain : repeat_enable, filter_linear;
uniform float lift = 0.0;
" + Torches + @"
float hgt(sampler2D tx, vec2 uv) { vec3 c = textureLod(tx, uv, 1.5).rgb; return dot(c, vec3(0.3, 0.59, 0.11)); }
void fragment() {
  float e = 0.004;
  vec3 n = normalize(vec3(hgt(TEXTURE, UV - vec2(e, 0.0)) - hgt(TEXTURE, UV + vec2(e, 0.0)), hgt(TEXTURE, UV - vec2(0.0, e)) - hgt(TEXTURE, UV + vec2(0.0, e)), 0.08));
  vec3 stone = mix(vec3(0.5, 0.37, 0.23), vec3(0.78, 0.62, 0.42), texture(grain, UV * 2.0).r * 0.6 + hgt(TEXTURE, UV) * 0.4);
  vec3 c = stone * (vec3(0.18, 0.14, 0.14) + torch_light(SCREEN_UV, n, TIME) * 1.6);
  // a faint memory of colour in the deep cuts when lifted (the card is 'awake')
  c = mix(c, texture(TEXTURE, UV).rgb * 0.9, lift * 0.25);
  // cartouche: oval frame groove + fade
  vec2 p = (UV - 0.5) * 2.0;
  float r = length(p * vec2(1.0, 0.92));
  float groove = smoothstep(0.03, 0.0, abs(r - 0.94));
  c *= 1.0 - groove * 0.6;
  COLOR = vec4(c, smoothstep(1.0, 0.96, r));
}";
    /// Engraved letters: dark cut with a lit lower lip; `gold` fills them with gold leaf and a passing glint.
    const string Engrave = @"shader_type canvas_item;
uniform float gold = 0.0;
void fragment() {
  vec2 px = TEXTURE_PIXEL_SIZE;
  float a = texture(TEXTURE, UV).a;
  float up = texture(TEXTURE, UV + vec2(0.0, -2.5) * px).a;   // inner shadow from the top edge of the cut
  float dn = texture(TEXTURE, UV + vec2(0.0, 2.5) * px).a;
  vec3 cut = vec3(0.13, 0.08, 0.05) * (0.6 + 0.4 * (1.0 - up));
  vec3 lip = vec3(1.0, 0.8, 0.55) * 0.35;
  float glint = exp(-pow((UV.x - fract(TIME * 0.35) * 1.6 + 0.3) * 6.0, 2.0));
  vec3 leaf = mix(vec3(0.75, 0.5, 0.12), vec3(1.0, 0.88, 0.45), UV.y) + glint * 0.7;
  vec3 c = mix(cut, leaf, gold);
  float rim = clamp(dn - a, 0.0, 1.0);
  COLOR = vec4(c * a + lip * rim * (1.0 - gold), max(a, rim * 0.6));
}";
    const string Flame = @"shader_type canvas_item;
float h21(vec2 p) { vec3 q = fract(vec3(p.xyx) * 0.1031); q += dot(q, q.yzx + 33.33); return fract((q.x + q.y) * q.z); }
float n2(vec2 p) { vec2 i = floor(p), f = fract(p); f = f*f*(3.0-2.0*f); return mix(mix(h21(i), h21(i+vec2(1,0)), f.x), mix(h21(i+vec2(0,1)), h21(i+vec2(1,1)), f.x), f.y); }
void fragment() {
  vec2 p = UV; p.y = 1.0 - p.y;
  float n = n2(vec2(p.x * 6.0, p.y * 4.0 - TIME * 3.5)) * 0.6 + n2(vec2(p.x * 13.0, p.y * 8.0 - TIME * 6.0)) * 0.4;
  float shape = smoothstep(0.42, 0.0, abs(p.x - 0.5) * (1.6 - p.y * 0.4) * 2.0 + p.y * 0.55 - n * 0.45);
  vec3 c = mix(vec3(1.0, 0.3, 0.05), vec3(1.0, 0.92, 0.6), shape * shape);
  COLOR = vec4(c * 1.6, shape * smoothstep(1.0, 0.6, p.y));
}";

    float W, H;
    Font display = null!, ui = null!;
    readonly List<(Label L, ShaderMaterial M)> items = new();
    TextureRect relief = null!;
    Label blurb = null!;
    NoiseTexture2D grain = null!;
    Shader? engrave, reliefShader;

    protected override int ItemCount => Demo.Arg("screen") == "cards" ? 1 : Items.Length;
    protected override string? MusicPath => Md("menu_01");
    protected override void OnChoose(int i) { }
    protected override void OnBack() => GetTree().Quit();

    protected override void Build()
    {
        Demo.Ensure();
        W = GetViewportRect().Size.X; H = GetViewportRect().Size.Y;
        display = Ink.Display; ui = Sys(new[] { "Georgia", "Times New Roman" }, 400, italic: true);
        grain = new NoiseTexture2D { Width = 512, Height = 512, Seamless = true, Noise = new FastNoiseLite { Frequency = 0.02f, FractalOctaves = 5 } };
        ShaderRect(this, Wall, Vector2.Zero, new Vector2(W, H)).Mat().P("aspect", W / H).P("grain", grain);
        foreach (var x in new[] { 0.06f, 0.94f }) TorchAt(new Vector2(x * W, 0.32f * H));
        Dust();
        if (Demo.Arg("screen") == "cards") { Cards(); return; }

        var logo = Tex("res://assets/title/logo_light.png");
        Ink.Pic(logo, new Vector2(W / 2 - 330, 40), new Vector2(660, 660f * logo.GetHeight() / logo.GetWidth()), this);

        relief = Ink.Pic(Art(Featured[0]), new Vector2(W * 0.52f, H * 0.3f), new Vector2(H * 0.6f, H * 0.6f), this);
        relief.StretchMode = TextureRect.StretchModeEnum.Scale;
        relief.Material = new ShaderMaterial { Shader = reliefShader ??= new Shader { Code = Relief } }.P("aspect", W / H).P("grain", grain);

        ShaderRect(this, @"shader_type canvas_item;
uniform sampler2D grain : repeat_enable, filter_linear;
void fragment() {
  vec2 p = abs(UV - 0.5) * 2.0; float d = max(p.x * 1.0, p.y);
  float bevel = smoothstep(0.93, 1.0, d);
  vec3 c = mix(vec3(0.42, 0.33, 0.24), vec3(0.52, 0.42, 0.31), texture(grain, UV * 2.0).r);
  c *= 1.0 - bevel * 0.3;
  c *= 0.9 + 0.1 * (1.0 - UV.y);
  COLOR = vec4(c, smoothstep(1.0, 0.99, d));
}", new Vector2(W * 0.17f - 60, H * 0.31f), new Vector2(640, Items.Length * 76 + 40)).Mat().P("grain", grain);
        float y = H * 0.33f;
        for (int i = 0; i < Items.Length; i++)
        {
            int k = i;
            var m = new ShaderMaterial { Shader = engrave ??= new Shader { Code = Engrave } };
            var l = Say(this, Items[i].ToUpperInvariant(), display, 54, Colors.White, new Vector2(W * 0.17f, y), new Vector2(700, 74));
            l.Material = m; l.MouseFilter = MouseFilterEnum.Stop; l.MouseEntered += () => SetFocus(k);
            items.Add((l, m));
            y += 76;
        }
        blurb = Say(this, "", ui, 28, new Color(1f, 0.88f, 0.65f), new Vector2(W * 0.52f, H * 0.3f + H * 0.6f + 16), new Vector2(H * 0.6f, 40), HorizontalAlignment.Center);
        var f = Cosmetics.Framed(Settings.Avatar, Session.Profile.Frame, 88);
        f.Position = new Vector2(60, H - 124); AddChild(f);
        var nm = Say(this, Settings.PlayerName.ToUpperInvariant(), display, 30, Colors.White, new Vector2(160, H - 112));
        nm.Material = new ShaderMaterial { Shader = engrave ??= new Shader { Code = Engrave } }.P("gold", 1f);
        SetFocus(0);
    }

    void TorchAt(Vector2 at)
    {
        // bracket + flame
        AddChild(new ColorRect { Position = at + new Vector2(-10, 30), Size = new Vector2(20, 90), Color = new Color(0.12f, 0.08f, 0.05f), Material = Kit.RoundedMat(0.22f, 0.3f), MouseFilter = MouseFilterEnum.Ignore });
        AddChild(new ColorRect { Position = at + new Vector2(-28, 22), Size = new Vector2(56, 18), Color = new Color(0.25f, 0.16f, 0.08f), Material = Kit.RoundedMat(3, 0.4f), MouseFilter = MouseFilterEnum.Ignore });
        ShaderRect(this, Flame, at + new Vector2(-55, -140), new Vector2(110, 170));
        ShaderRect(this, "shader_type canvas_item; void fragment(){ float r = length(UV - 0.5) * 2.0; COLOR = vec4(1.0, 0.55, 0.2, exp(-r * 3.0) * 0.35 * (0.9 + 0.1 * sin(TIME * 13.0))); }", at - new Vector2(260, 300), new Vector2(520, 520));
    }

    void Dust()
    {
        var p = new CpuParticles2D
        {
            Amount = 70, Lifetime = 12, Preprocess = 12, Position = new Vector2(W / 2, H + 20), EmissionShape = CpuParticles2D.EmissionShapeEnum.Rectangle,
            EmissionRectExtents = new Vector2(W / 2, 10), Direction = Vector2.Up, Spread = 30, Gravity = Vector2.Zero, InitialVelocityMin = 25, InitialVelocityMax = 70,
            ScaleAmountMin = 1.5f, ScaleAmountMax = 3.5f, Color = new Color(1f, 0.8f, 0.5f, 0.45f),
        };
        AddChild(p);
    }

    protected override void OnFocus(int old, int now)
    {
        if (items.Count == 0) return;
        for (int i = 0; i < items.Count; i++)
        {
            items[i].M.P("gold", i == now ? 1f : 0f);
            CreateTween().TweenProperty(items[i].L, "position:x", W * 0.17f + (i == now ? 28 : 0), 0.25).SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
        }
        relief.Texture = Art(Featured[now]);
        relief.Mat().P("lift", 0f);
        CreateTween().TweenMethod(Callable.From<float>(v => relief.Mat().P("lift", v)), 0f, 1f, 0.8);
        blurb.Text = Blurb[now];
    }

    // ---------------------------------------------------------------- card list: niches + papyrus

    void Cards()
    {
        var logo = Tex("res://assets/title/logo_light.png");
        Ink.Pic(logo, new Vector2(50, 30), new Vector2(330, 330f * logo.GetHeight() / logo.GetWidth()), this);
        var title = Say(this, "CARD LIST", display, 56, Colors.White, new Vector2(420, 52));
        title.Material = new ShaderMaterial { Shader = engrave ??= new Shader { Code = Engrave } }.P("gold", 1f);
        string[] names = { "Dark Magician", "Blue-Eyes White Dragon", "Red-Eyes B. Dragon", "Summoned Skull", "Dark Magician Girl", "Black Luster Soldier - Envoy of the Beginning",
            "Kuriboh", "Time Wizard", "Exodia the Forbidden One", "Magician of Black Chaos", "Elemental HERO Neos", "Chaos Emperor Dragon - Envoy of the End",
            "Obelisk the Tormentor", "Slifer the Sky Dragon", "The Winged Dragon of Ra", "Elemental HERO Flame Wingman", "Dark Magician of Chaos", "Winged Kuriboh" };
        float cw = 150, ch = cw * 1.458f, gx = 28, gy = 26, x0 = 110, y0 = 200;
        for (int i = 0; i < names.Length; i++)
        {
            var at = new Vector2(x0 + i % 6 * (cw + gx), y0 + i / 6 * (ch + gy) + (i % 2) * 0);
            // the niche: a dark recess slightly bigger than the card, then the card set into it
            AddChild(new ColorRect { Position = at - new Vector2(10, 10), Size = new Vector2(cw + 20, ch + 20), Color = new Color(0.1f, 0.06f, 0.03f, 0.75f), Material = Kit.RoundedMat((cw + 20) / (ch + 20), 0.05f), MouseFilter = MouseFilterEnum.Ignore });
            var c = Ink.Pic(Full(names[i]), at, new Vector2(cw, ch), this);
            c.StretchMode = TextureRect.StretchModeEnum.Scale;
            bool sel = i == 4;
            c.Modulate = sel ? Colors.White : new Color(0.82f, 0.72f, 0.6f);
            if (sel)
            {
                c.PivotOffset = c.Size / 2; c.Scale = new Vector2(1.08f, 1.08f); c.ZIndex = 1;
                ShaderRect(this, "shader_type canvas_item; void fragment(){ vec2 p = abs(UV - 0.5) * 2.0; float d = max(p.x, p.y); COLOR = vec4(1.0, 0.8, 0.35, smoothstep(0.86, 0.98, d) * smoothstep(1.0, 0.98, d) + exp(-(1.0 - d) * 12.0) * 0.35); }", at - new Vector2(22, 22), new Vector2(cw + 44, ch + 44)).ZIndex = 0;
            }
        }
        // Papyrus with the selected card's text.
        float px = x0 + 6 * (cw + gx) + 20, pw = W - px - 80, ph = H - 260;
        ShaderRect(this, @"shader_type canvas_item;
float h21(vec2 p) { vec3 q = fract(vec3(p.xyx) * 0.1031); q += dot(q, q.yzx + 33.33); return fract((q.x + q.y) * q.z); }
uniform sampler2D grain : repeat_enable, filter_linear;
void fragment() {
  float fib = texture(grain, UV * vec2(0.6, 6.0)).r * 0.6 + texture(grain, UV * vec2(5.0, 0.5)).r * 0.4;
  vec3 c = mix(vec3(0.82, 0.7, 0.48), vec3(0.93, 0.84, 0.64), fib);
  float edge = min(min(UV.x, 1.0 - UV.x), min(UV.y, 1.0 - UV.y));
  float torn = smoothstep(0.0, 0.02 + h21(floor(UV * 40.0)) * 0.015, edge);
  c *= 0.75 + 0.25 * smoothstep(0.0, 0.12, edge);
  COLOR = vec4(c, torn);
}", new Vector2(px, 200), new Vector2(pw, ph)).Mat().P("grain", grain);
        var d = Card("Dark Magician Girl");
        Say(this, d.Name.ToUpperInvariant(), display, 38, new Color(0.25f, 0.13f, 0.04f), new Vector2(px + 40, 240), new Vector2(pw - 80, 50));
        Say(this, $"{d.Attribute} · {d.Race} · Level {d.Level}", ui, 24, new Color(0.45f, 0.28f, 0.1f), new Vector2(px + 40, 300));
        Say(this, $"ATK {d.Atk}   DEF {d.Def}", display, 30, new Color(0.3f, 0.16f, 0.05f), new Vector2(px + 40, 350));
        var desc = Say(this, d.TcgDesc ?? d.Desc, ui, 22, new Color(0.22f, 0.13f, 0.06f), new Vector2(px + 40, 410), new Vector2(pw - 80, 300));
        desc.AutowrapMode = TextServer.AutowrapMode.WordSmart; desc.CustomMinimumSize = new Vector2(pw - 80, 0); desc.Size = new Vector2(pw - 80, 300);
        Say(this, "OWNED  ×2", display, 28, new Color(0.45f, 0.28f, 0.05f), new Vector2(px + 40, 200 + ph - 80));
    }
}
