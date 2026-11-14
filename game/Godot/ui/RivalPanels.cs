using Godot;
using PoC.Engine;

namespace PoC.Game.UI;

/// The rivals and the slanted panel they are drawn in, shared by the main menu and rival select.
/// Adding a rival: one line in <see cref="All"/> plus `assets/rivals/<File>.png`,
/// `assets/art/art-<Card>.jpg` and a folder of deck cards `assets/walls/<File>/`.
public static class RivalPanels
{
    public sealed record Rival(string Name, string File, string Card, Color Color);

    public static readonly Rival[] All =
    {
        new("YUGI", "yugi", "46986414", new Color(0.45f, 0.18f, 0.7f)),   // Dark Magician
        new("KAIBA", "kaiba", "89631139", new Color(0.15f, 0.42f, 0.85f)), // Blue-Eyes White Dragon
        new("JOEY", "joey", "74677422", new Color(0.85f, 0.25f, 0.15f)),  // Red-Eyes B. Dragon
        new("JADEN", "jaden", "35809262", new Color(0.9f, 0.55f, 0.1f)),  // Elemental HERO Flame Wingman (v1.0)
    };

    public static readonly Color Gold = new(1f, 0.82f, 0.4f);

    /// A tournament duelist as a rival: their signature card stands in for a portrait unless `assets/rivals/<key>.png` exists.
    public static Rival Of(Duelist d) => All.FirstOrDefault(r => r.File == d.Key)
        ?? new Rival(d.Name, d.Key, CardDb.Shared.Named(d.Card) is var c && c.Passcode > 0 ? c.Passcode.ToString() : $"i{c.Idx}", Color.FromHtml(d.Color));

    /// The rival's signature card art (square): `assets/art/art-<passcode or i+idx>.jpg`, else cut from the card scan.
    public static Texture2D Art(Rival r)
    {
        var p = $"res://assets/art/art-{r.Card}.jpg";
        if (ResourceLoader.Exists(p)) return GD.Load<Texture2D>(p);
        var db = CardDb.Shared;
        return CardArt.Picture(r.Card.StartsWith('i') ? db.Get(int.Parse(r.Card[1..])) : db.ByIdx.Values.First(c => c.Passcode.ToString() == r.Card));
    }

    /// True when the rival has a character picture (the tournament duelists use their card art instead).
    public static bool HasPortrait(Rival r) => ResourceLoader.Exists($"res://assets/rivals/{r.File}.png");

    /// The rival's picture (`mood` "_happy" / "_sad" when there is one), or their signature card art.
    public static Texture2D Portrait(Rival r, string mood = "")
    {
        foreach (var p in new[] { $"res://assets/rivals/{r.File}{mood}.png", $"res://assets/rivals/{r.File}.png" })
            if (ResourceLoader.Exists(p)) return GD.Load<Texture2D>(p);
        return Art(r);
    }
    /// Where the face is in each portrait (left, top, width as fractions of the picture), for round icons. One fixed
    /// crop for all of them cut some rivals to half a face or just hair (playtest 2026-11-12).
    static readonly Dictionary<string, Vector3> Faces = new()
    {
        ["yugi"] = new(0.24f, 0.13f, 0.5f), ["kaiba"] = new(0.34f, 0.03f, 0.36f), ["joey"] = new(0.41f, 0f, 0.42f), ["jaden"] = new(0.22f, 0.03f, 0.5f),
        // tournament duelists (portraits added 2026-11-12; boxes estimated from the cut-outs)
        ["aster"] = new(0.27f, 0f, 0.62f), ["keith"] = new(0.18f, 0f, 0.5f), ["bastion"] = new(0.28f, 0.01f, 0.34f), ["chazz"] = new(0.22f, 0f, 0.4f),
        ["crowler"] = new(0.25f, 0f, 0.42f), ["ishizu"] = new(0.25f, 0f, 0.46f), ["mai"] = new(0.1f, 0.02f, 0.8f), ["mako"] = new(0.25f, 0f, 0.5f),
        ["pegasus"] = new(0.35f, 0f, 0.51f), ["odion"] = new(0.29f, 0f, 0.45f), ["rex"] = new(0.2f, 0.05f, 0.6f), ["bakura"] = new(0.2f, 0.03f, 0.62f),
        ["alexis"] = new(0.55f, 0f, 0.42f), ["weevil"] = new(0.3f, 0.02f, 0.48f), ["marik"] = new(0.28f, 0.08f, 0.5f), ["syrus"] = new(0.2f, 0f, 0.6f),
        ["zane"] = new(0.21f, 0f, 0.38f),
    };

    static readonly Dictionary<string, Texture2D> faceTex = new();
    /// A square picture for round icons: the face cut from the portrait, or the signature card art without one.
    public static Texture2D FaceTex(Rival r)
    {
        if (!HasPortrait(r)) return Art(r);
        if (faceTex.TryGetValue(r.File, out var done)) return done;
        var img = Portrait(r).GetImage(); var f = Face(r);
        if (img.IsCompressed()) img.Decompress();
        var box = new Rect2I((int)(f.X * img.GetWidth()), (int)(f.Y * img.GetHeight()), (int)(f.Z * img.GetWidth()), (int)(f.W * img.GetHeight())).Intersection(new Rect2I(0, 0, img.GetWidth(), img.GetHeight()));
        return faceTex[r.File] = ImageTexture.CreateFromImage(img.GetRegion(box));
    }

    /// The square face region of Portrait(r) in UV (x, y, w, h); a duelist shown by card art gets the whole picture.
    public static Vector4 Face(Rival r)
    {
        if (!HasPortrait(r)) return new Vector4(0, 0, 1, 1);
        var t = Portrait(r);
        var f = Faces.TryGetValue(r.File, out var v) ? v : new Vector3(0.25f, 0, 0.5f);
        return new Vector4(f.X, f.Y, f.Z, f.Z * t.GetWidth() / t.GetHeight());
    }
    public const float Skew = 150, Overlap = 10;

    public const string Rim = @"shader_type canvas_item;
uniform vec4 glow : source_color = vec4(1.0, 0.85, 0.5, 1.0);
void fragment() {
  vec4 c = texture(TEXTURE, UV);
  float a = 0.0; vec2 px = TEXTURE_PIXEL_SIZE * 9.0;
  for (int i = 0; i < 12; i++) { float k = float(i) * 0.5236; a += texture(TEXTURE, UV + vec2(cos(k), sin(k)) * px).a; }
  a /= 12.0;
  COLOR = mix(vec4(glow.rgb, a * 0.55), c, c.a) * vec4(1.0, 1.0, 1.0, COLOR.a);
}";

    public const string Bar = @"shader_type canvas_item;
float hash(vec2 p) { vec3 q = fract(vec3(p.xyx) * 0.1031); q += dot(q, q.yzx + 33.33); return fract((q.x + q.y) * q.z); }  // sin-free: sin() hashes break into blocks on Intel GPUs
void fragment() {
  vec3 col = mix(vec3(0.05, 0.045, 0.06), vec3(0.015, 0.012, 0.02), UV.y);
  col += (hash(UV * 900.0 + fract(TIME)) - 0.5) * 0.03;
  col += vec3(1.0, 0.8, 0.4) * exp(-UV.y * 60.0) * 0.5;
  COLOR = vec4(col, 1.0);
}";

    /// Light band sweeping across a panel, plus a darkening toward the bottom for the name.
    public const string Sweep = @"shader_type canvas_item;
uniform vec4 tint : source_color = vec4(0.4, 0.15, 0.6, 1.0);
uniform float phase = 0.0;
void fragment() {
  float band = exp(-pow((UV.x + UV.y * 0.45 - (fract(TIME * 0.12 + phase) * 2.4 - 0.6)) * 7.0, 2.0));
  float a1 = band * 0.22, a2 = smoothstep(0.45, 1.0, UV.y) * 0.85;
  float a = a1 + a2 * (1.0 - a1);
  vec3 col = (vec3(1.0, 0.97, 0.9) * a1 + tint.rgb * 0.15 * a2 * (1.0 - a1)) / max(a, 1e-4);
  COLOR = vec4(col, a);
}";

    public static float PanelWidth(float screenW) => Math.Min(600, (screenW + Overlap * (All.Length - 1) - Skew) / All.Length);

    public sealed class Panel
    {
        public Node2D Root = null!;
        public Polygon2D Shape = null!;
        public Sprite2D Portrait = null!;
        public Label Name = null!;
        public Line2D[] Edges = null!;
        public Vector2 Home;
        public float BaseY, W, H;
        public Vector2[] Polygon = null!;
        public bool Contains(Vector2 global) => Geometry2D.IsPointInPolygon(Root.GetGlobalTransform().AffineInverse() * global, Polygon);
    }

    /// Build panel i. `background` adds the panel's backdrop as the shape's first child (clipped to it).
    public static Panel Build(Node host, int i, float panelW, float panelH, float screenW, Font nameFont, Action<Polygon2D, Rival, int> background) =>
        Build(host, All[i], i, panelW, panelH, screenW, nameFont, background);

    public static Panel Build(Node host, Rival r, int i, float panelW, float panelH, float screenW, Font nameFont, Action<Polygon2D, Rival, int> background)
    {
        var p = new Panel { W = panelW, H = panelH };
        p.Polygon = new[] { new Vector2(Skew, 0), new Vector2(panelW + Skew, 0), new Vector2(panelW, panelH), new Vector2(0, panelH) };
        p.Shape = new Polygon2D { Polygon = p.Polygon, Color = r.Color.Darkened(0.75f), ClipChildren = CanvasItem.ClipChildrenMode.AndDraw };
        p.Root = new Node2D();
        p.Root.AddChild(p.Shape);
        background(p.Shape, r, i);

        // No character picture (tournament duelists): an empty sprite, the panel's card art carries it.
        var tex = HasPortrait(r) ? Portrait(r) : ImageTexture.CreateFromImage(Image.CreateEmpty(2, 3, false, Image.Format.Rgba8));
        float sc = Math.Min(panelH * 0.9f / tex.GetHeight(), (panelW + Skew * 0.6f) / tex.GetWidth());
        p.BaseY = panelH - tex.GetHeight() * sc / 2 + 30;
        var rim = new ShaderMaterial { Shader = new Shader { Code = Rim } };
        rim.SetShaderParameter("glow", r.Color.Lightened(0.5f));
        p.Portrait = new Sprite2D { Texture = tex, Scale = new Vector2(sc, sc), Position = new Vector2(panelW / 2 + Skew / 2 + 10, p.BaseY), Material = rim };
        p.Shape.AddChild(p.Portrait);

        p.Edges = new[] { (p.Polygon[0], p.Polygon[3]), (p.Polygon[1], p.Polygon[2]) }
            .Select(e => new Line2D { Points = new[] { e.Item1, e.Item2 }, Width = 3, DefaultColor = Gold * new Color(1, 1, 1, 0.85f) }).ToArray();
        foreach (var e in p.Edges) p.Root.AddChild(e);

        float nameSize = Math.Clamp(panelW * 0.12f, 44, 70);
        p.Name = new Label { Text = r.Name, Position = new Vector2(34, panelH - nameSize - 42), Rotation = -0.04f };
        p.Name.AddThemeFontOverride("font", nameFont);
        p.Name.AddThemeFontSizeOverride("font_size", (int)nameSize);
        p.Name.AddThemeColorOverride("font_color", Colors.White);
        p.Name.AddThemeConstantOverride("outline_size", 12);
        p.Name.AddThemeColorOverride("font_outline_color", r.Color.Darkened(0.65f));
        var lc = new Node2D();
        lc.AddChild(p.Name);
        p.Root.AddChild(lc);
        host.AddChild(p.Root);

        float total = All.Length * (panelW - Overlap) + Overlap + Skew;
        p.Home = new Vector2((screenW - total) / 2 + i * (panelW - Overlap), 0);
        return p;
    }

    /// Slide panels up into place, staggered.
    public static void Intro(Node owner, IReadOnlyList<Panel> panels, float screenH)
    {
        for (int i = 0; i < panels.Count; i++)
        {
            var p = panels[i];
            p.Root.Position = p.Home + new Vector2(0, screenH);
            owner.CreateTween().TweenProperty(p.Root, "position", p.Home, 0.8).SetDelay(0.1 + i * 0.15).SetTrans(Tween.TransitionType.Expo).SetEase(Tween.EaseType.Out);
        }
    }

    /// Portrait idle bob + mouse parallax.
    public static void Animate(IReadOnlyList<Panel> panels, double t, Vector2 mouse01)
    {
        var m = mouse01 - new Vector2(0.5f, 0.5f);
        for (int i = 0; i < panels.Count; i++)
        {
            var p = panels[i];
            float depth = 0.6f + 0.6f * i / Math.Max(1, panels.Count - 1);
            p.Portrait.Position = new Vector2(p.W / 2 + Skew / 2 + 10 - m.X * 30 * depth, p.BaseY + Mathf.Sin((float)t * 0.9f + i * 1.7f) * 6 - m.Y * 14);
        }
    }

    public static StyleBoxFlat ChipStyle(Color bg, Color border, float glow) => new()
    {
        BgColor = bg, BorderColor = border,
        BorderWidthBottom = 1, BorderWidthTop = 1, BorderWidthLeft = 1, BorderWidthRight = 1,
        CornerRadiusTopLeft = 14, CornerRadiusTopRight = 14, CornerRadiusBottomLeft = 14, CornerRadiusBottomRight = 14,
        CornerDetail = 8, AntiAliasing = true, Skew = new Vector2(0.12f, 0),
        ShadowColor = new Color(1f, 0.75f, 0.3f, glow), ShadowSize = (int)(glow * 26),
    };

    /// Deck card textures for a rival's wall (`assets/walls/<file>/`).
    public static List<Texture2D> WallCards(string file)
    {
        var dir = $"res://assets/walls/{file}";
        return DirAccess.GetFilesAt(dir)
            .Select(f => f.Replace(".import", "").Replace(".remap", ""))
            .Where(f => f.EndsWith(".bmp")).Distinct().OrderBy(f => f)
            .Select(f => GD.Load<Texture2D>($"{dir}/{f}")).ToList();
    }
}

/// Rounded slanted chip button (menu style): gold fill on hover/selection.
public static class Chips
{
    static Font? font;

    public static Panel Make(string text, Vector2 pos, Vector2 size, Action onClick, int fontSize = 25)
    {
        font ??= Fonts.Get(new[] { "Bahnschrift", "Segoe UI" }, 600, stretch: 87);
        var chip = new Panel { Position = pos, Size = size, PivotOffset = size / 2 };
        bool ink = Kit.InkTheme;
        if (ink) chip.SetMeta("ink", true);
        chip.AddThemeStyleboxOverride("panel", ink ? InkStyle(new Color(1, 1, 1, 0.03f), Ink.Cream * new Color(1, 1, 1, 0.3f)) : RivalPanels.ChipStyle(new Color(1, 1, 1, 0.05f), RivalPanels.Gold * new Color(1, 1, 1, 0.35f), 0));
        var fill = new Panel { Name = "Fill", Size = size, Modulate = new Color(1, 1, 1, 0), MouseFilter = Control.MouseFilterEnum.Ignore };
        fill.AddThemeStyleboxOverride("panel", ink ? InkStyle(Ink.Cream, Ink.Cream) : RivalPanels.ChipStyle(new Color(0.98f, 0.8f, 0.4f), new Color(1f, 0.93f, 0.7f), 0.45f));
        var l = new Label { Name = "Text", Text = text, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, Size = size };
        l.AddThemeFontOverride("font", font);
        // Long labels shrink to stay inside the chip; past 14 px they break onto more lines (broken here: Label's
        // autowrap didn't take on these free-standing chips).
        float room = size.X - 24;
        float Wide(string s, int fs) => font.GetStringSize(s, HorizontalAlignment.Left, -1, fs).X;
        while (fontSize > 14 && Wide(text, fontSize) > room) fontSize--;
        if (Wide(text, fontSize) > room)
            for (; fontSize >= 10; fontSize--)
            {
                var lines = new List<string> { "" };
                foreach (var word in text.Split(' '))
                    if (lines[^1].Length > 0 && Wide(lines[^1] + " " + word, fontSize) > room) lines.Add(word);
                    else lines[^1] = lines[^1].Length == 0 ? word : lines[^1] + " " + word;
                l.Text = string.Join("\n", lines);
                if (lines.Count * font.GetHeight(fontSize) <= size.Y - 4 || fontSize == 10) break;
            }
        l.AddThemeFontSizeOverride("font_size", fontSize);
        // It grew to the long text's width before the fit (the size sticks until it's on screen): back to the chip's.
        l.Ready += () => Callable.From(() => { if (GodotObject.IsInstanceValid(l)) l.Size = size; }).CallDeferred();   // the scene may be gone by then
        l.AddThemeColorOverride("font_color", new Color(1, 1, 1, 0.8f));
        chip.AddChild(fill); chip.AddChild(l);
        // Hover lifts the chip a little; a press squashes it and springs back.
        chip.MouseEntered += () => { SetLit(chip, true); Scale(chip, 1.04f, 0.15); };
        chip.MouseExited += () => { if (!chip.HasMeta("on")) SetLit(chip, false); Scale(chip, 1f, 0.15); };
        chip.GuiInput += e =>
        {
            if (e is not InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left }) return;
            var tw = chip.CreateTween();
            tw.TweenProperty(chip, "scale", new Vector2(0.94f, 0.94f), 0.06);
            tw.TweenProperty(chip, "scale", new Vector2(1.04f, 1.04f), 0.35).SetTrans(Tween.TransitionType.Elastic).SetEase(Tween.EaseType.Out);
            onClick();
        };
        return chip;
    }

    /// Ink chip: rounded, no slant or glow.
    static StyleBoxFlat InkStyle(Color bg, Color border) => new()
    {
        BgColor = bg, BorderColor = border, BorderWidthBottom = 2, BorderWidthTop = 2, BorderWidthLeft = 2, BorderWidthRight = 2,
        CornerRadiusTopLeft = 14, CornerRadiusTopRight = 14, CornerRadiusBottomLeft = 14, CornerRadiusBottomRight = 14, CornerDetail = 8, AntiAliasing = true,
    };

    static void Scale(Control c, float to, double t) =>
        c.CreateTween().TweenProperty(c, "scale", new Vector2(to, to), t).SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);

    /// Light a chip (hover, or the selected option of a segmented group).
    public static void SetLit(Panel chip, bool on)
    {
        chip.CreateTween().TweenProperty(chip.GetNode<Panel>("Fill"), "modulate:a", on ? 1f : 0f, 0.15);
        bool ink = chip.HasMeta("ink");
        chip.GetNode<Label>("Text").AddThemeColorOverride("font_color", on ? (ink ? Ink.Black : new Color(0.16f, 0.09f, 0.02f)) : ink ? Ink.Cream * new Color(1, 1, 1, 0.8f) : new Color(1, 1, 1, 0.8f));
    }

    /// Keep a chip lit (segmented selection) or release it.
    public static void SetOn(Panel chip, bool on)
    {
        if (on) chip.SetMeta("on", true); else chip.RemoveMeta("on");
        SetLit(chip, on);
    }
}
