using Godot;
using PoC.Engine;

namespace PoC.Game.UI;

/// Shared pieces for the Card List and the Deck Builder: fonts, glass panels, attribute colours.
public static class Kit
{
    static Font? impact, cond, ui, kanji;
    public static Font Impact => impact ??= Sys(new[] { "Impact", "Arial Black" }, 800, true);
    public static Font Cond => cond ??= Sys(new[] { "Bahnschrift", "Impact" }, 700, stretch: 75);
    public static Font Ui => ui ??= Sys(new[] { "Bahnschrift", "Segoe UI" }, 500);
    public static Font Kanji => kanji ??= Sys(new[] { "Yu Gothic UI", "Meiryo", "MS Gothic" }, 700);

    static Font Sys(string[] names, int weight, bool italic = false, int stretch = 100) => Fonts.Get(names, weight, italic, stretch);

    public static readonly string[] Attributes = { "DARK", "LIGHT", "EARTH", "WATER", "FIRE", "WIND" };
    public static string AttrGlyph(string a) => a switch { "DARK" => "闇", "LIGHT" => "光", "EARTH" => "地", "WATER" => "水", "FIRE" => "炎", "WIND" => "風", _ => "?" };
    public static Color AttrColor(string a) => a switch
    {
        "DARK" => new Color(0.55f, 0.3f, 0.8f), "LIGHT" => new Color(0.95f, 0.85f, 0.35f), "EARTH" => new Color(0.65f, 0.48f, 0.3f),
        "WATER" => new Color(0.3f, 0.6f, 0.95f), "FIRE" => new Color(0.95f, 0.35f, 0.2f), "WIND" => new Color(0.35f, 0.8f, 0.45f), _ => Colors.Gray,
    };
    public static Color KindColor(Kind k) => k switch { Kind.Spell => new Color(0.2f, 0.7f, 0.6f), Kind.Trap => new Color(0.8f, 0.3f, 0.55f), _ => new Color(0.85f, 0.6f, 0.3f) };

    /// Set per screen (MenuBase.Inked): the ink look for the shared pieces. Read when they are built.
    public static bool InkTheme;
    /// Highlight colour: gold, or cream ink on the ink screens.
    public static Color Accent => InkTheme ? Ink.Cream : RivalPanels.Gold;

    public static StyleBoxFlat Glass(Color edge, float glow = 0, int radius = 18) => InkTheme ? new()
    {
        BgColor = new Color(0.04f, 0.035f, 0.045f, 0.82f), BorderColor = Ink.Cream * new Color(1, 1, 1, glow > 0 ? 0.55f : 0.2f),
        BorderWidthTop = 2, BorderWidthBottom = 2, BorderWidthLeft = 2, BorderWidthRight = 2,
        CornerRadiusTopLeft = radius, CornerRadiusTopRight = radius, CornerRadiusBottomLeft = radius, CornerRadiusBottomRight = radius,
        CornerDetail = 8, AntiAliasing = true, ShadowColor = new Color(0, 0, 0, 0.45f), ShadowSize = 18,
    } : new()
    {
        BgColor = new Color(0.025f, 0.028f, 0.04f, 0.8f), BorderColor = edge,
        BorderWidthTop = 2, BorderWidthBottom = 1, BorderWidthLeft = 1, BorderWidthRight = 1,
        CornerRadiusTopLeft = radius, CornerRadiusTopRight = radius, CornerRadiusBottomLeft = radius, CornerRadiusBottomRight = radius,
        CornerDetail = 8, AntiAliasing = true,
        ShadowColor = glow > 0 ? new Color(edge, glow) : new Color(0, 0, 0, 0.45f), ShadowSize = glow > 0 ? 22 : 18,
    };

    public static Panel Box(Control parent, Rect2 r, StyleBox style)
    {
        var p = new Panel { Position = r.Position, Size = r.Size, MouseFilter = Control.MouseFilterEnum.Ignore };
        p.AddThemeStyleboxOverride("panel", style);
        parent.AddChild(p);
        return p;
    }

    /// A label; `wrap` turns on word wrap before the text goes in, so the label keeps the box width.
    public static Label Put(Control parent, string text, Font f, int size, Color c, Vector2 pos, Vector2 box = default, HorizontalAlignment align = HorizontalAlignment.Left, int outline = 0, bool wrap = false)
    {
        var l = new Label { AutowrapMode = wrap ? TextServer.AutowrapMode.WordSmart : TextServer.AutowrapMode.Off, Text = text, Position = pos, HorizontalAlignment = align, MouseFilter = Control.MouseFilterEnum.Ignore };
        if (box != default) l.Size = box;
        l.AddThemeFontOverride("font", f);
        l.AddThemeFontSizeOverride("font_size", size);
        l.AddThemeColorOverride("font_color", c);
        if (outline > 0) { l.AddThemeConstantOverride("outline_size", outline); l.AddThemeColorOverride("font_outline_color", new Color(0, 0, 0, 0.85f)); }
        parent.AddChild(l);
        return l;
    }

    /// Card art with rounded corners (uniform `aspect` = width / height of the control).
    public const string Rounded = @"shader_type canvas_item;
uniform float radius = 0.06;
uniform float aspect = 0.69;
uniform float grey = 0.0;
varying vec4 tint;
void vertex() { tint = COLOR; } // modulate only: fragment COLOR already has the texture multiplied in
void fragment() {
  vec2 p = abs(UV - 0.5) * vec2(aspect, 1.0);
  vec2 h = vec2(aspect, 1.0) * 0.5 - radius;
  float d = length(max(p - h, 0.0)) - radius;
  vec4 c = texture(TEXTURE, UV);
  c.rgb = mix(c.rgb, vec3(dot(c.rgb, vec3(0.3, 0.59, 0.11))) * 0.45, grey);
  COLOR = vec4(c.rgb * tint.rgb, c.a * (1.0 - smoothstep(-0.004, 0.004, d)) * tint.a); // keep the node's tint
}";

    static Shader? rounded;
    public static ShaderMaterial RoundedMat(float aspect = 0.69f, float radius = 0.035f)
    {
        var m = new ShaderMaterial { Shader = rounded ??= new Shader { Code = Rounded } };
        m.SetShaderParameter("aspect", aspect); m.SetShaderParameter("radius", radius);
        return m;
    }

    /// Small rounded label chip drawn on a canvas (badges).
    public static void Badge(CanvasItem ci, Rect2 r, Color bg, string text, Font f, int size, Color fg)
    {
        ci.DrawStyleBox(new StyleBoxFlat
        {
            BgColor = bg, CornerRadiusTopLeft = (int)(r.Size.Y / 2), CornerRadiusTopRight = (int)(r.Size.Y / 2), CornerRadiusBottomLeft = (int)(r.Size.Y / 2), CornerRadiusBottomRight = (int)(r.Size.Y / 2),
            CornerDetail = 6, AntiAliasing = true,
        }, r);
        var ts = f.GetStringSize(text, HorizontalAlignment.Left, -1, size);
        ci.DrawString(f, new Vector2(r.Position.X + (r.Size.X - ts.X) / 2, r.Position.Y + (r.Size.Y + f.GetAscent(size) - f.GetDescent(size)) / 2), text, HorizontalAlignment.Left, -1, size, fg);
    }

    /// Limit-list mark (as in Master Duel): a red circle with the allowed count, or a slash for forbidden.
    public static void LimitMark(CanvasItem ci, Vector2 c, float r, int limit)
    {
        if (limit >= DeckRules.MaxCopies) return;
        var col = limit == 0 ? new Color(0.85f, 0.12f, 0.12f) : limit == 1 ? new Color(0.95f, 0.45f, 0.1f) : new Color(0.95f, 0.75f, 0.15f);
        ci.DrawCircle(c, r + 2, new Color(0, 0, 0, 0.7f));
        ci.DrawCircle(c, r, col);
        if (limit == 0) ci.DrawLine(c + new Vector2(-r * 0.55f, r * 0.55f), c + new Vector2(r * 0.55f, -r * 0.55f), Colors.White, 3, true);
        else
        {
            var s = limit.ToString(); int fs = (int)(r * 1.5f);
            var ts = Impact.GetStringSize(s, HorizontalAlignment.Left, -1, fs);
            ci.DrawString(Impact, c + new Vector2(-ts.X / 2, (Impact.GetAscent(fs) - Impact.GetDescent(fs)) / 2), s, HorizontalAlignment.Left, -1, fs, Colors.White);
        }
    }

    /// Padlock glyph.
    public static void Lock(CanvasItem ci, Vector2 c, float s, Color col)
    {
        ci.DrawArc(c + new Vector2(0, -s * 0.15f), s * 0.32f, Mathf.Pi, Mathf.Tau, 16, col, s * 0.11f, true);
        ci.DrawStyleBox(new StyleBoxFlat { BgColor = col, CornerRadiusTopLeft = 4, CornerRadiusTopRight = 4, CornerRadiusBottomLeft = 4, CornerRadiusBottomRight = 4, AntiAliasing = true },
            new Rect2(c + new Vector2(-s * 0.48f, -s * 0.12f), new Vector2(s * 0.96f, s * 0.72f)));
    }

    /// Attribute orb (kanji on a coloured disc, like the card frame).
    public static void Orb(CanvasItem ci, Vector2 c, float r, string attr, float alpha = 1)
    {
        var col = AttrColor(attr);
        ci.DrawCircle(c, r + 1.5f, new Color(0, 0, 0, 0.6f * alpha));
        ci.DrawCircle(c, r, col.Darkened(0.35f) * new Color(1, 1, 1, alpha));
        ci.DrawCircle(c + new Vector2(0, -r * 0.18f), r * 0.78f, col * new Color(1, 1, 1, alpha));
        var g = AttrGlyph(attr); int fs = (int)(r * 1.15f);
        var ts = Kanji.GetStringSize(g, HorizontalAlignment.Left, -1, fs);
        ci.DrawString(Kanji, c + new Vector2(-ts.X / 2, (Kanji.GetAscent(fs) - Kanji.GetDescent(fs)) / 2), g, HorizontalAlignment.Left, -1, fs, new Color(1, 1, 1, alpha));
    }

    /// Keep a value of the deck (count text) bouncing when it changes.
    public static void Pop(Control c)
    {
        c.PivotOffset = c.Size / 2;
        var tw = c.CreateTween();
        tw.TweenProperty(c, "scale", new Vector2(1.25f, 1.25f), 0.07);
        tw.TweenProperty(c, "scale", Vector2.One, 0.35).SetTrans(Tween.TransitionType.Elastic).SetEase(Tween.EaseType.Out);
    }
}

/// One card in a grid: art (loaded lazily when on screen), dimmed with a padlock when not owned,
/// owned count, NEW badge, limit-list mark, and (deck builder) copies in the deck as pips.
public partial class CardTile : Control
{
    public readonly CardData Data;
    public int Owned, InDeck = -1;   // InDeck >= 0 shows pips: copies used of the copies allowed
    public bool Fresh, ShowCount = true;
    readonly TextureRect art;
    readonly Control over;
    bool loaded;

    public CardTile(CardData d, Vector2 size)
    {
        Data = d;
        CustomMinimumSize = size; Size = size;
        PivotOffset = size / 2;
        MouseFilter = MouseFilterEnum.Pass;
        art = new TextureRect
        {
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.Scale, Size = size,
            Material = Kit.RoundedMat(size.X / size.Y), MouseFilter = MouseFilterEnum.Ignore, TextureFilter = TextureFilterEnum.LinearWithMipmaps,
        };
        AddChild(art);
        over = new Control { Size = size, MouseFilter = MouseFilterEnum.Ignore };
        over.Draw += DrawOver;
        AddChild(over);
    }

    public void Refresh()
    {
        ((ShaderMaterial)art.Material).SetShaderParameter("grey", Owned > 0 ? 0f : 0.85f);
        over.QueueRedraw();
    }

    public override void _Ready() => Refresh();

    public override void _Process(double delta)
    {
        if (loaded || !IsVisibleInTree()) return;
        if (!GetGlobalRect().Intersects(GetViewportRect().Grow(400))) return;
        loaded = true;
        SetProcess(false);
        art.Modulate = new Color(1, 1, 1, 0);
        // Decoded off the main thread (CardArt.SmallAsync); the tile may be gone by the time the picture is ready.
        CardArt.SmallAsync(Data, t =>
        {
            if (!IsInstanceValid(this)) return;
            art.Texture = t;
            CreateTween().TweenProperty(art, "modulate:a", 1f, 0.25);
        });
    }

    void DrawOver()
    {
        var s = Size;
        var ci = over;
        if (Owned <= 0) Kit.Lock(ci, s / 2 + new Vector2(0, -4), s.X * 0.28f, new Color(1, 1, 1, 0.55f));
        int fs = Math.Max(12, (int)(s.X * 0.11f));
        if (Fresh) Kit.Badge(ci, new Rect2(5, 5, fs * 3.1f, fs * 1.5f), Kit.Accent, "NEW", Kit.Cond, fs, new Color(0.16f, 0.09f, 0.02f));
        Kit.LimitMark(ci, new Vector2(s.X - fs * 0.95f, fs * 0.95f), fs * 0.7f, DeckRules.Limit(Data));
        if (InDeck >= 0)
        {
            // Pips: filled = copies in the deck, hollow = copies still free to add.
            int allowed = DeckRules.Allowed(Data, Session.Profile);
            float r = fs * 0.32f, gap = r * 2.8f, x0 = s.X / 2 - (allowed - 1) * gap / 2;
            if (allowed > 0) ci.DrawStyleBox(new StyleBoxFlat { BgColor = new Color(0, 0, 0, 0.7f), CornerRadiusTopLeft = 9, CornerRadiusTopRight = 9, CornerRadiusBottomLeft = 9, CornerRadiusBottomRight = 9, AntiAliasing = true },
                new Rect2(x0 - r * 2.2f, s.Y - r * 4.6f, (allowed - 1) * gap + r * 4.4f, r * 3.4f));
            for (int i = 0; i < allowed; i++)
            {
                var c = new Vector2(x0 + i * gap, s.Y - r * 2.9f);
                if (i < InDeck) ci.DrawCircle(c, r, Kit.Accent);
                else ci.DrawArc(c, r * 0.85f, 0, Mathf.Tau, 16, new Color(1, 1, 1, 0.6f), 1.6f, true);
            }
        }
        else if (ShowCount && Owned > 0)
            Kit.Badge(ci, new Rect2(s.X - fs * 2.6f - 5, s.Y - fs * 1.5f - 5, fs * 2.6f, fs * 1.5f), new Color(0, 0, 0, 0.78f), $"×{Owned}", Kit.Cond, fs, Colors.White);
    }
}

/// Card details like the duel field's panel: HD art, name, type line, level stars, ATK/DEF, text.
/// The panel fits its content (up to maxH).
public partial class CardDetail : Panel
{
    readonly float w, maxH;
    readonly TextureRect art;
    readonly Label name, type, stars, stats, desc, owned;
    readonly ColorRect rule;
    public CardData? Card { get; private set; }

    public CardDetail(float width, float maxHeight, bool showOwned = true)
    {
        w = width; maxH = maxHeight;
        Size = new Vector2(w, 200);
        MouseFilter = MouseFilterEnum.Ignore;
        ClipContents = true;
        AddThemeStyleboxOverride("panel", Kit.Glass(Kit.Accent * new Color(1, 1, 1, 0.45f)));
        float iw = w - 40;
        art = new TextureRect { ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, Position = new Vector2(20, 20), Size = new Vector2(iw, iw * 1.458f), StretchMode = TextureRect.StretchModeEnum.Scale, TextureFilter = TextureFilterEnum.LinearWithMipmaps, Material = Kit.RoundedMat(1 / 1.458f, 0.02f) };
        AddChild(art);
        name = Kit.Put(this, "", Kit.Cond, 32, Colors.White, Vector2.Zero, new Vector2(iw, 40));
        name.ClipText = true;
        type = Kit.Put(this, "", Kit.Ui, 17, Kit.Accent, Vector2.Zero, new Vector2(iw, 24));
        stars = Kit.Put(this, "", Kit.Ui, 18, new Color(1f, 0.7f, 0.25f), Vector2.Zero, new Vector2(iw, 24));
        stats = Kit.Put(this, "", Kit.Impact, 30, Colors.White, Vector2.Zero, new Vector2(iw, 40), outline: 6);
        owned = Kit.Put(this, "", Kit.Cond, 24, new Color(1, 1, 1, 0.6f), Vector2.Zero, new Vector2(iw, 40), HorizontalAlignment.Right);
        owned.Visible = showOwned;
        rule = new ColorRect { Size = new Vector2(iw, 1), Color = Kit.Accent * new Color(1, 1, 1, 0.4f) };
        AddChild(rule);
        desc = Kit.Put(this, "", Kit.Ui, 17, new Color(1, 1, 1, 0.82f), Vector2.Zero, new Vector2(iw, 40));
        desc.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        Modulate = new Color(1, 1, 1, 0);
    }

    /// Art height used by the layout (narrow panels may want a shorter picture).
    public float ArtH => art.Size.Y;

    public void Show(CardData d, int ownedCount)
    {
        Card = d;
        float iw = w - 40;
        art.Texture = CardArt.Large(d);
        name.Text = d.Name;
        if (d.IsMonster)
        {
            type.Text = $"{d.Attribute}  ·  {d.Race.ToUpperInvariant()} / {d.Frame.ToUpperInvariant()}";
            stars.Text = new string('★', Math.Max(0, d.Level));
            stats.Text = $"ATK {d.AtkText}    DEF {d.DefText}";
        }
        else
        {
            type.Text = $"{d.Kind.ToString().ToUpperInvariant()}  ·  {d.Frame.ToUpperInvariant()}";
            stars.Text = ""; stats.Text = "";
        }
        owned.Text = ownedCount > 0 ? $"×{ownedCount}" : "";
        float y = 20 + art.Size.Y + 12;
        name.Position = new Vector2(20, y); type.Position = new Vector2(20, y + 40);
        stars.Position = new Vector2(20, y + 64); stats.Position = new Vector2(20, y + 88);
        owned.Position = new Vector2(20, y + 4);
        name.Size = new Vector2(iw - (ownedCount > 0 ? 60 : 0), 40);
        float ruleY = d.IsMonster ? y + 132 : y + 74;
        rule.Position = new Vector2(20, ruleY);
        desc.Text = d.TcgDesc ?? d.Desc;
        desc.Position = new Vector2(20, ruleY + 10);
        float descH = desc.GetLineCount() * desc.GetLineHeight();
        desc.Size = new Vector2(iw, descH);
        float h = Math.Min(maxH, ruleY + 10 + descH + 22);
        if (Modulate.A < 0.99f) { Size = new Vector2(w, h); CreateTween().TweenProperty(this, "modulate:a", 1f, 0.2); }
        else CreateTween().TweenProperty(this, "size:y", h, 0.2).SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
        art.Modulate = new Color(1, 1, 1, 0.3f);
        CreateTween().TweenProperty(art, "modulate:a", 1f, 0.18);
    }
}
