using Godot;
using PoC.Engine;
using static PoC.Game.UI.ConceptKit;

namespace PoC.Game.UI;

/// Inner screens in the locked Chaos Rift style (2026-11-08): Options, Shop, Card List, Deck Builder, 3+ samples each.
/// Mockups (not wired). `-- --demo --screen=options|shop|cards|deck --variant=a|b|c [--hover=N]`
/// Shared look: the rift background (light left, dark right; the crack can sit anywhere), the forged logo small at the
/// top right, Perpetua Titling Bold that is graphite on the light side and silver on the dark side, gold for chosen
/// things, gold diamonds as markers, cards floating with soft shadows. No boxes or glass panels: space and hairlines.
public partial class RiftScreens : MenuBase
{
    protected float W, H;
    protected float Top = 0.58f, Bottom = 0.44f;
    protected ShaderMaterial bg = null!;
    protected Font Title = null!, Body = null!, Thin = null!;
    protected static readonly Color Ink = new(0.07f, 0.07f, 0.08f), Silver = new(0.92f, 0.92f, 0.94f), GoldC = new(0.82f, 0.62f, 0.26f), GoldDeep = new(0.6f, 0.42f, 0.12f),
        Graphite = new(0.2f, 0.2f, 0.23f), Mist = new(0.62f, 0.64f, 0.7f), GoldLight = new(1f, 0.85f, 0.5f);
    Shader? twoTone, pipShader, shadowShader;
    protected string variant = "a";
    protected readonly List<Action<bool>> focusables = new();

    protected override string? MusicPath => Md("menu_02");
    protected override int ItemCount => Math.Max(1, focusables.Count);
    protected override void OnFocus(int old, int now) { if (old >= 0 && old < focusables.Count) focusables[old](false); if (now >= 0 && now < focusables.Count) focusables[now](true); }
    protected override void OnChoose(int i) { }
    protected override void OnBack() => GetTree().Quit();

    protected override void Build()
    {
        Demo.Ensure();
        W = GetViewportRect().Size.X; H = GetViewportRect().Size.Y;
        variant = Demo.Arg("variant", "a");
        Title = new FontVariation { BaseFont = Sys(new[] { "Perpetua Titling MT" }, 700), SpacingGlyph = 6 };
        Thin = new FontVariation { BaseFont = Sys(new[] { "Perpetua Titling MT" }, 300), SpacingGlyph = 5 };
        Body = Sys(new[] { "Gill Sans MT", "Segoe UI" }, 400);
        switch (Demo.Arg("screen", "options"))
        {
            case "shop": ShopS(); break;
            case "cards": CardsS(); break;
            case "deck": DeckS(); break;
            case "decks": DeckSelect(); break;
            case "tour": TourS(); break;
            case "rival": RivalS(); break;
            case "duel": DuelS(); break;
            default: Options(); break;
        }
        if (focusables.Count > 0) SetFocus(int.TryParse(Demo.Arg("hover"), out var h) ? h : 0);
    }

    // ---------------------------------------------------------------- kit

    /// The rift background with the crack between x = top*W (at the top) and bottom*W (at the bottom).
    protected void Rift(float top, float bottom, float smoke = 1f)
    {
        Top = top; Bottom = bottom;
        var noise = new NoiseTexture2D { Width = 512, Height = 512, Seamless = true, Noise = new FastNoiseLite { Frequency = 0.01f } };
        bg = ShaderRect(this, RiftVariants.Bg, Vector2.Zero, new Vector2(W, H)).Mat().P("aspect", W / H).P("noise_tex", noise).P("top", top).P("bottom", bottom)
            .P("light_a", new Color(0.985f, 0.985f, 0.98f)).P("light_b", new Color(0.88f, 0.88f, 0.87f)).P("dark_a", new Color(0.025f, 0.025f, 0.03f)).P("dark_b", new Color(0.09f, 0.09f, 0.11f))
            .P("halo_l", new Color(0.85f, 0.75f, 0.55f)).P("halo_d", new Color(0.7f, 0.72f, 0.8f)).P("rays", 0f).P("smoke", smoke);
    }

    protected float CrackX(float yPx) => Mathf.Lerp(Top, Bottom, yPx / H) * W;
    protected bool OnLight(Vector2 p) => p.X < CrackX(p.Y);

    /// Text that is graphite on the light side and silver on the dark side; `lit` turns it gold.
    protected ShaderMaterial TwoTone() => new ShaderMaterial { Shader = twoTone ??= new Shader { Code = @"shader_type canvas_item;
uniform float lit = 0.0;
uniform float dim = 1.0;
" + RiftVariants.CrackFn + @"
void fragment() {
  float a = texture(TEXTURE, UV).a * COLOR.a;
  float d = crack_d(SCREEN_UV, TIME);
  float y = smoothstep(0.3, 0.72, UV.y);
  vec3 graphite = mix(vec3(0.26, 0.26, 0.29), vec3(0.05, 0.05, 0.06), y);
  vec3 silver = mix(vec3(1.0), vec3(0.62, 0.64, 0.7), y);
  vec3 gold = d < 0.0 ? mix(vec3(0.74, 0.54, 0.18), vec3(0.5, 0.33, 0.08), y) : mix(vec3(1.0, 0.9, 0.58), vec3(0.7, 0.48, 0.16), y);
  COLOR = vec4(mix(d < 0.0 ? graphite : silver, gold, lit), a * mix(dim, 1.0, lit));
}" } }.P("aspect", W / H).P("top", Top).P("bottom", Bottom);

    /// A label in the two-tone ink.
    protected Label T(Control host, string s, Font f, int size, Vector2 pos, Vector2 box = default, HorizontalAlignment a = HorizontalAlignment.Left, float dim = 1f)
    {
        var l = Say(host, s, f, size, Colors.White, pos, box, a);
        l.Material = TwoTone().P("dim", dim);
        return l;
    }

    /// A plain coloured label (when the side is known).
    protected Label C(Control host, string s, Font f, int size, Color c, Vector2 pos, Vector2 box = default, HorizontalAlignment a = HorizontalAlignment.Left) => Say(host, s, f, size, c, pos, box, a);

    protected Color InkFor(Vector2 p) => OnLight(p) ? Ink : Silver;

    /// The forged logo, small, top right (or wherever).
    protected void Logo(Vector2 centre, float w)
    {
        var img = Image.LoadFromFile(ProjectSettings.GlobalizePath("res://assets/title/rift/R0.png"));
        img.GenerateMipmaps();
        float h = w * img.GetHeight() / img.GetWidth();
        var r = Ink_(ImageTexture.CreateFromImage(img), centre - new Vector2(w, h) / 2, new Vector2(w, h));
        r.TextureFilter = TextureFilterEnum.LinearWithMipmaps;
    }

    protected TextureRect Ink_(Texture2D t, Vector2 pos, Vector2 size, Control? host = null)
    {
        var r = new TextureRect { ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.Scale, MouseFilter = MouseFilterEnum.Ignore, Texture = t };
        (host ?? this).AddChild(r);
        r.Position = pos; r.Size = size;
        return r;
    }

    /// Screen title: big Perpetua on the side it sits, a hairline and a diamond under it.
    protected void Heading(string s, Vector2 at, int size = 54)
    {
        T(this, s, Title, size, at);
        float w = Title.GetStringSize(s, HorizontalAlignment.Left, -1, size).X;
        Hair(new Vector2(at.X, at.Y + size * 1.25f), w * 0.6f, GoldC with { A = 0.8f });
        Pip(new Vector2(at.X + w * 0.6f + 10, at.Y + size * 1.25f), 9);
    }

    protected ColorRect Hair(Vector2 at, float w, Color c, Control? host = null, float t = 1.5f)
    {
        var r = new ColorRect { Position = at, Size = new Vector2(w, t), Color = c, MouseFilter = MouseFilterEnum.Ignore };
        (host ?? this).AddChild(r);
        return r;
    }

    /// The gold chiselled diamond (like the dot of the logo's i).
    protected ColorRect Pip(Vector2 centre, float s, Control? host = null)
    {
        var p = new ColorRect { Size = new Vector2(s, s), Position = centre - new Vector2(s, s) / 2, PivotOffset = new Vector2(s, s) / 2, Rotation = Mathf.Pi / 4, MouseFilter = MouseFilterEnum.Ignore,
            Material = new ShaderMaterial { Shader = pipShader ??= new Shader { Code = "shader_type canvas_item; void fragment(){ vec2 p = UV - 0.5; float l = 0.5 + (-p.x - p.y) * 0.9; COLOR = vec4(mix(vec3(0.55, 0.36, 0.1), vec3(1.0, 0.92, 0.6), l), COLOR.a); }" } } };
        (host ?? this).AddChild(p);
        return p;
    }

    /// Soft drop shadow for any rect-shaped thing (no visible edge).
    protected ColorRect Shadow(Control host, Vector2 pos, Vector2 size, float strength, Color col, Vector2 offset = default)
    {
        float m = Math.Max(size.X, size.Y) * 0.3f;
        var sh = ShaderRect(host, "", pos - new Vector2(m, m) + offset, size + new Vector2(2 * m, 2 * m));
        sh.Material = new ShaderMaterial { Shader = shadowShader ??= new Shader { Code = @"shader_type canvas_item;
uniform float strength; uniform vec4 col : source_color; uniform vec2 rect; uniform vec2 half_box; uniform float margin;
void fragment() {
  vec2 p = abs((UV - 0.5) * rect);
  float d = length(max(p - half_box * 0.94, 0.0));
  float k = exp(-pow(d / (margin * 0.42), 2.0)) * (1.0 - smoothstep(margin * 0.55, margin, d));
  COLOR = vec4(col.rgb, strength * k);
}" } }.P("strength", strength).P("col", col).P("rect", size + new Vector2(2 * m, 2 * m)).P("half_box", size / 2).P("margin", m);
        return sh;
    }

    /// A card standing in the air with its soft shadow. Returns the root (pivot in the middle).
    protected Control Card(CardData d, Vector2 centre, float h, float tilt = 0, bool full = true, float shadow = 1f)
    {
        float w = h * 0.686f;
        var root = new Control { Position = centre - new Vector2(w, h) / 2, Size = new Vector2(w, h), PivotOffset = new Vector2(w, h) / 2, Rotation = tilt, MouseFilter = MouseFilterEnum.Ignore };
        AddChild(root);
        bool light = OnLight(centre);
        if (shadow > 0) Shadow(root, Vector2.Zero, new Vector2(w, h), (light ? 0.3f : 0.45f) * shadow, light ? new Color(0.12f, 0.12f, 0.15f) : new Color(0.35f, 0.36f, 0.45f), light ? new Vector2(h * 0.03f, h * 0.05f) : Vector2.Zero);
        Ink_(full ? (h > 300 ? CardArt.Large(d) : CardArt.Small(d)) : CardArt.Picture(d), Vector2.Zero, new Vector2(w, h), root);
        return root;
    }

    /// A thin gold slider: hairline track, gold fill, diamond knob, value in Perpetua.
    protected void Slider(Vector2 at, float w, float v, bool onDark)
    {
        Hair(at, w, (onDark ? Silver : Ink) * new Color(1, 1, 1, 0.25f));
        Hair(at + new Vector2(0, -0.5f), w * v, GoldC, null, 2.5f);
        Pip(at + new Vector2(w * v, 0.5f), 16);
        C(this, $"{(int)(v * 100)}", Thin, 24, onDark ? Silver : Ink, at + new Vector2(w + 24, -16));
    }

    /// Choices in a row; the picked one gold with a diamond under it.
    protected void Segment(Vector2 at, string[] opts, int pick, bool onDark, int size = 24)
    {
        float x = at.X;
        for (int i = 0; i < opts.Length; i++)
        {
            float w = Title.GetStringSize(opts[i], HorizontalAlignment.Left, -1, size).X;
            C(this, opts[i], Title, size, i == pick ? (onDark ? new Color(1f, 0.85f, 0.5f) : GoldDeep) : (onDark ? Silver : Ink) * new Color(1, 1, 1, 0.45f), new Vector2(x, at.Y));
            if (i == pick) Pip(new Vector2(x + w / 2, at.Y + size * 1.45f), 8);
            x += w + 46;
        }
    }

    /// A text button: words between two diamonds, hairlines above and below that grow when focused.
    protected Action<bool> Button(string s, Vector2 centre, int size = 28)
    {
        float w = Title.GetStringSize(s, HorizontalAlignment.Left, -1, size).X;
        var l = T(this, s, Title, size, centre - new Vector2(w / 2 + 40, size * 0.72f), new Vector2(w + 80, size * 1.45f), HorizontalAlignment.Center);
        var a = Pip(centre + new Vector2(-w / 2 - 36, 0), 10); var b = Pip(centre + new Vector2(w / 2 + 36, 0), 10);
        var top = Hair(new Vector2(centre.X - w / 2 - 20, centre.Y - size * 0.95f), w + 40, GoldC with { A = 0.6f });
        var bot = Hair(new Vector2(centre.X - w / 2 - 20, centre.Y + size * 0.9f), w + 40, GoldC with { A = 0.6f });
        foreach (var x in new Control[] { top, bot }) { x.PivotOffset = new Vector2(x.Size.X / 2, 0); x.Scale = new Vector2(0.3f, 1); }
        return on =>
        {
            ((ShaderMaterial)l.Material).P("lit", on ? 1f : 0f);
            foreach (var x in new Control[] { top, bot }) CreateTween().TweenProperty(x, "scale:x", on ? 1f : 0.3f, 0.25).SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
        };
    }

    /// Owned/played card pool for mockups: the canonical printings the demo profile owns.
    protected static List<CardData> OwnedCards()
    {
        var db = CardDb.Shared; var p = Session.Profile;
        return db.ByIdx.Values.Where(c => p.Count(c.Idx) > 0 && db.Canon(c.Idx) == c.Idx).OrderBy(c => c.Kind).ThenByDescending(c => c.Level).ThenBy(c => c.Name).ToList();
    }

    protected static CardData Named(string n) => CardDb.Shared.Named(n);

    protected static Texture2D? Avatar => Settings.Avatar;
    static CardDb Db => CardDb.Shared;
}
