using Godot;
using PoC.Engine;
using Cosmetic = PoC.Engine.Shop.Cosmetic;

namespace PoC.Game.UI;

/// Playmats, card backs and icon frames as pictures: tiles for the Shop and the Card List, full textures for the field.
/// Mat previews are top-down renders of the real table (`assets/mats/preview/<key>.jpg`, from DuelField `--matview`);
/// frames are game/tools/frameforge.py renders (`assets/frames/<key>.png`, the picture shows through the round hole).
public static class Cosmetics
{
    public const float MatAspect = 7.3f / 4.9f, BackAspect = 813f / 1185, MatRadius = 0.47f / 4.9f;   // the table's rounded corners
    public const float Hole = 168f / 512;   // frame hole radius / frame size (frameforge.py HOLE / S)
    static readonly Dictionary<string, Texture2D> cache = new();

    /// A project image as pixels. Through the imported texture (lossless), because an exported build only ships the
    /// imported copy, not the original file.
    public static Image? ResImage(string path) => ResourceLoader.Exists(path) ? GD.Load<Texture2D>(path)?.GetImage() : null;

    static Texture2D? File(string path, int width)
    {
        if (cache.TryGetValue(path + width, out var t)) return t;
        if (ResImage(path) is not { } img) return null;
        if (width > 0 && img.GetWidth() > width) img.Resize(width, img.GetHeight() * width / img.GetWidth(), Image.Interpolation.Lanczos);
        img.GenerateMipmaps();
        return cache[path + width] = ImageTexture.CreateFromImage(img);
    }

    /// A card back at full size (width 0) or scaled down for a tile; the classic back for "classic" or anything unknown.
    public static Texture2D Back(string key, int width = 0) =>
        key == Shop.DefaultBack ? CardArt.Back : File($"res://assets/backs/{key}.png", width) ?? CardArt.Back;

    public static Texture2D? MatPreview(string key) => File($"res://assets/mats/preview/{key}.jpg", 0);
    public static Texture2D? Frame(string key, int width = 0) => File($"res://assets/frames/{key}.png", width) ?? File($"res://assets/frames/{Shop.DefaultFrame}.png", width);

    static float Aspect(Cosmetic k) => k switch { Cosmetic.Mat => MatAspect, Cosmetic.Back => BackAspect, _ => 1 };

    /// A round picture inside an icon frame, `size` across (the frame's full size; the picture is about 2/3 of it).
    /// Children: 0 backdrop, 1 the picture (a TextureRect, also when `pic` is null, so callers can swap it), 2 the frame.
    public static Control Framed(Texture2D? pic, string frame, float size)
    {
        var root = new Control { Size = new Vector2(size, size), MouseFilter = Control.MouseFilterEnum.Ignore };
        float d = size * Hole * 2 + 4;
        root.AddChild(new ColorRect { Position = new Vector2((size - d) / 2, (size - d) / 2), Size = new Vector2(d, d), Color = new Color(0.06f, 0.06f, 0.08f), Material = Kit.RoundedMat(1, 0.5f), MouseFilter = Control.MouseFilterEnum.Ignore });
        root.AddChild(new TextureRect { Texture = pic, ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered, Position = new Vector2((size - d) / 2, (size - d) / 2), Size = new Vector2(d, d), Material = Kit.RoundedMat(1, 0.5f), TextureFilter = CanvasItem.TextureFilterEnum.LinearWithMipmaps, MouseFilter = Control.MouseFilterEnum.Ignore });
        root.AddChild(new TextureRect { Texture = Frame(frame, size <= 256 ? 256 : 0), ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.Scale, Size = root.Size, TextureFilter = CanvasItem.TextureFilterEnum.LinearWithMipmaps, MouseFilter = Control.MouseFilterEnum.Ignore });
        return root;
    }

    /// One tile: the picture (a frame shows around your own profile picture), its name, a pill (price, OWNED,
    /// EQUIPPED...) and a teal ring when equipped.
    public static Control Tile(Shop.Item it, float w, string pill, bool gold, bool dim, bool equipped, Action click)
    {
        var k = it.Kind;
        bool mat = k == Cosmetic.Mat;
        float h = w / Aspect(k);
        var root = new Control { CustomMinimumSize = new Vector2(w, h + 104), Size = new Vector2(w, h + 104), PivotOffset = new Vector2(w / 2, h / 2), MouseFilter = Control.MouseFilterEnum.Stop };
        Control pic = k == Cosmetic.Frame ? Framed(Settings.Avatar, it.Key, w) : new TextureRect
        {
            Texture = mat ? MatPreview(it.Key) : Back(it.Key, 360), ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.Scale,
            Size = new Vector2(w, h), Material = Kit.RoundedMat(w / h, mat ? MatRadius : 0.04f), TextureFilter = CanvasItem.TextureFilterEnum.LinearWithMipmaps, MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        if (dim) pic.Modulate = new Color(0.5f, 0.5f, 0.55f);
        root.AddChild(pic);
        if (equipped)
        {
            var ring = Kit.Box(root, new Rect2(-5, -5, w + 10, h + 10), new StyleBoxFlat { DrawCenter = false, BorderColor = Teal, BorderWidthTop = 3, BorderWidthBottom = 3, BorderWidthLeft = 3, BorderWidthRight = 3, CornerRadiusTopLeft = 14, CornerRadiusTopRight = 14, CornerRadiusBottomLeft = 14, CornerRadiusBottomRight = 14, AntiAliasing = true });
            ring.MouseFilter = Control.MouseFilterEnum.Ignore;
        }
        Kit.Put(root, it.Name.ToUpperInvariant(), Kit.Cond, mat ? 28 : 22, Colors.White, new Vector2(mat ? 4 : -10, h + 10), new Vector2(mat ? w : w + 20, 36), mat ? HorizontalAlignment.Left : HorizontalAlignment.Center);
        float pw = Math.Max(110, pill.Length * 11 + 30);
        var tag = new Control { Position = new Vector2(mat ? 0 : w / 2 - pw / 2, h + 52), Size = new Vector2(pw, 38), MouseFilter = Control.MouseFilterEnum.Ignore };
        tag.Draw += () => Kit.Badge(tag, new Rect2(Vector2.Zero, tag.Size), gold ? new Color(0.08f, 0.06f, 0.02f, 0.92f) : new Color(0.1f, 0.1f, 0.12f, 0.92f), pill, Kit.Cond, 21, gold ? (dim ? new Color(1, 1, 1, 0.4f) : Kit.Accent) : dim ? new Color(1, 1, 1, 0.4f) : Teal);
        root.AddChild(tag);
        root.MouseEntered += () => root.CreateTween().TweenProperty(root, "scale", new Vector2(1.04f, 1.04f), 0.14).SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
        root.MouseExited += () => root.CreateTween().TweenProperty(root, "scale", Vector2.One, 0.14);
        root.GuiInput += e => { if (e is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left }) click(); };
        return root;
    }

    public static Color Teal => Kit.InkTheme ? Ink.Cream : new Color(0.35f, 0.9f, 0.85f);

    /// A scrolling grid of tiles in `r` (4 mats, 8 backs or 6 frames to a row).
    public static ScrollContainer Grid(Control parent, Rect2 r, Cosmetic k, IEnumerable<Func<float, Control>> tiles)
    {
        var sc = Scroll(parent, r);
        int cols = k switch { Cosmetic.Mat => 4, Cosmetic.Back => 8, _ => 6 };
        float gap = k == Cosmetic.Mat ? 36 : 26, w = (r.Size.X - 60 - gap * (cols - 1)) / cols;
        var g = new GridContainer { Columns = cols };
        g.AddThemeConstantOverride("h_separation", (int)gap); g.AddThemeConstantOverride("v_separation", 22);
        foreach (var make in tiles) g.AddChild(make(w));
        var m = new MarginContainer();
        foreach (var s in new[] { "margin_left", "margin_right", "margin_top", "margin_bottom" }) m.AddThemeConstantOverride(s, 18);
        m.AddChild(g); sc.AddChild(m);
        return sc;
    }

    /// The gold-thumbed vertical scroller the Card List uses.
    public static ScrollContainer Scroll(Control parent, Rect2 r)
    {
        var sc = new ScrollContainer { Position = r.Position, Size = r.Size, HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
        var bar = sc.GetVScrollBar();
        bar.AddThemeStyleboxOverride("scroll", new StyleBoxFlat { BgColor = new Color(1, 1, 1, 0.04f), CornerRadiusTopLeft = 4, CornerRadiusTopRight = 4, CornerRadiusBottomLeft = 4, CornerRadiusBottomRight = 4, ContentMarginLeft = 4, ContentMarginRight = 4 });
        foreach (var s in new[] { "grabber", "grabber_highlight", "grabber_pressed" })
            bar.AddThemeStyleboxOverride(s, new StyleBoxFlat { BgColor = Kit.Accent * new Color(1, 1, 1, s == "grabber" ? 0.45f : 0.8f), CornerRadiusTopLeft = 4, CornerRadiusTopRight = 4, CornerRadiusBottomLeft = 4, CornerRadiusBottomRight = 4 });
        parent.AddChild(sc);
        return sc;
    }

    /// The tab keys and which cosmetic each lists ("cards" lists cards).
    public static Cosmetic KindOf(string tab) => tab switch { "mats" => Cosmetic.Mat, "backs" => Cosmetic.Back, _ => Cosmetic.Frame };

    /// CARDS / PLAYMATS / CARD BACKS (/ FRAMES) chips for the bottom bar; `on` = the open tab. The Card List leaves
    /// FRAMES out (frames are equipped in Options). Returns where the chips end.
    public static float Tabs(Control parent, Vector2 at, string on, Action<string> pick, bool frames = true)
    {
        float x = at.X;
        var tabs = new[] { ("cards", "CARDS", 125f), ("mats", "PLAYMATS", 164f), ("backs", "CARD BACKS", 190f), ("frames", "FRAMES", 140f) };
        foreach (var (key, label, w) in tabs.Where(t => frames || t.Item1 != "frames"))
        {
            var c = Chips.Make(label, new Vector2(x, at.Y), new Vector2(w, 56), () => pick(key));
            if (key == on) Chips.SetOn(c, true);
            parent.AddChild(c);
            x += w + 14;
        }
        return x;
    }
}
