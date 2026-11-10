using Godot;
using PoC.Engine;

namespace PoC.Game.UI;

/// The duel-screen gaps the card-lab review found (game/LAB_REPORT.md U1-U10), as picked in the UI Lab rounds 5-7
/// (game/NEXT_SESSION.md): counters as Master Duel's icon + number in a card's bottom-right corner, the turn count as a
/// blue number on a round badge there, a changed Level top-left (red = lower, blue = higher), the kept coin result and
/// a borrowed name in the card details, the "Declare" pickers, a hand kept revealed, quick NO DRAW / END PHASE words.
public partial class DuelField
{
    static readonly Font Sym = Fonts.Get(new[] { "Segoe UI Symbol" });
    static readonly Color LvUp = new(0.45f, 0.75f, 1f), CountBlue = new(0.55f, 0.9f, 1f), Borrowed = new(0.76f, 0.58f, 1f);

    /// Counter kinds as the engine names them (Card.Vars "ctr:<kind>") -> Master Duel's icon (game/Godot/assets/counters).
    static readonly Dictionary<string, string> CounterIcons = new()
    {
        ["spell"] = "magic", ["clock"] = "clock", ["fog"] = "fog", ["venom"] = "venom", ["guard"] = "guard",
        ["chronicle"] = "chronicle", ["phantasm"] = "phantasm", ["d"] = "d", ["WATER"] = "ocean",
    };

    /// An imported picture (png/webp under res://) as an Image. Exported builds ship only Godot's imported copy, not the
    /// file itself, so it is read through the resource loader (Image.LoadFromFile on the file works in the editor only).
    static Image ResImage(string res)
    {
        var img = GD.Load<Texture2D>(res).GetImage();
        if (img.IsCompressed()) img.Decompress();
        return img;
    }

    static readonly Dictionary<string, Texture2D> icons = new();
    /// An icon from assets/counters.
    static Texture2D Icon(string name)
    {
        if (icons.TryGetValue(name, out var t)) return t;
        var img = ResImage($"res://assets/counters/{name}.png");
        img.GenerateMipmaps();
        return icons[name] = ImageTexture.CreateFromImage(img);
    }

    /// The round badge under the turn count: dark, with a blue ring.
    static Texture2D? badgeTex;
    static Texture2D BadgeTex()
    {
        if (badgeTex != null) return badgeTex;
        const int n = 64;
        var img = Image.CreateEmpty(n, n, false, Image.Format.Rgba8);
        var fill = new Color(0.02f, 0.05f, 0.09f, 0.92f);
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float d = new Vector2(x + 0.5f - n / 2f, y + 0.5f - n / 2f).Length(), r = n / 2f - 1;
                var c = d > r - 5 ? CountBlue : fill;
                img.SetPixel(x, y, new Color(c, c.A * Mathf.Clamp(r - d, 0, 1)));
            }
        img.GenerateMipmaps();
        return badgeTex = ImageTexture.CreateFromImage(img);
    }

    // ---------------------------------------------------------------- marks on a card (U1, U7, U9)

    sealed class Mk
    {
        public Node3D Box = null!;   // follows the card's visibility
        public Sprite3D CtrIcon = null!, LvIcon = null!, Badge = null!;
        public Label3D CtrNum = null!, LvNum = null!;
    }
    readonly Dictionary<View, Mk> mks = new();

    Mk MarksOf(View v)
    {
        if (mks.TryGetValue(v, out var m)) return m;
        Sprite3D Spr(Texture2D t, int prio) => new()
        {
            Texture = t, PixelSize = 0.0025f, Billboard = BaseMaterial3D.BillboardModeEnum.Enabled, NoDepthTest = true, RenderPriority = prio,
            TextureFilter = BaseMaterial3D.TextureFilterEnum.LinearWithMipmaps, Visible = false,
        };
        Label3D Num(Color c) => new()
        {
            Font = Impact, FontSize = 34, PixelSize = 0.004f, Billboard = BaseMaterial3D.BillboardModeEnum.Enabled, NoDepthTest = true,
            OutlineSize = 10, Modulate = c, OutlineModulate = new Color(0, 0, 0, 0.95f), RenderPriority = 4, OutlineRenderPriority = 3,
            HorizontalAlignment = HorizontalAlignment.Left, Offset = new Vector2(20, 0), Visible = false,
        };
        m = new Mk { Box = new Node3D(), CtrIcon = Spr(Icon("normal"), 3), CtrNum = Num(Colors.White), LvIcon = Spr(Icon("level_star"), 3), LvNum = Num(LvUp), Badge = Spr(BadgeTex(), 1) };
        m.Badge.PixelSize = 0.0026f;
        foreach (var n in new Node3D[] { m.CtrIcon, m.CtrNum, m.LvIcon, m.LvNum, m.Badge }) m.Box.AddChild(n);
        world.AddChild(m.Box);
        return mks[v] = m;
    }

    /// Counters (the first kind a card carries) and a changed Level from the card's state; the badge goes with the turn count.
    void UpdateMarks(View v)
    {
        var s = v.S;
        var m = MarksOf(v);
        bool on = s.Up && s.Loc is Loc.Monster or Loc.Spell or Loc.Field;
        // ponytail: one counter kind per card shown (no card here carries two); stack them if one ever does
        var ctr = on && s.Ctr.Length > 0 ? s.Ctr.Split(',')[0].Split('=') : null;
        m.CtrIcon.Visible = m.CtrNum.Visible = ctr != null;
        if (ctr != null) { m.CtrIcon.Texture = Icon(CounterIcons.GetValueOrDefault(ctr[0], "normal")); m.CtrNum.Text = ctr[1]; }
        bool lv = on && s.Loc == Loc.Monster && s.Lv > 0 && s.Lv != s.LvWas;
        m.LvIcon.Visible = m.LvNum.Visible = lv;
        if (lv) { m.LvNum.Text = s.Lv.ToString(); m.LvNum.Modulate = s.Lv < s.LvWas ? Bad : LvUp; }
        m.Badge.Visible = v.Count.Visible;
    }

    /// Each frame: the marks ride on the card (its own frame, so a rival card's bottom-right is its owner's).
    void PlaceMarks(View v)
    {
        if (!mks.TryGetValue(v, out var m)) return;
        m.Box.Visible = v.Root.Visible;
        var t = v.Root.GlobalTransform;
        Vector3 At(float x, float z) => t * new Vector3(x, 0, z) + new Vector3(0, 0.12f, 0);
        bool badge = v.Count.Visible;
        if (badge) { var b = At(CW * 0.38f, CH * 0.4f); v.Count.Position = b; m.Badge.Position = b; }
        if (m.CtrIcon.Visible) { var c = At(badge ? -CW * 0.12f : CW * 0.12f, CH * 0.33f); m.CtrIcon.Position = c; m.CtrNum.Position = c; }
        if (m.LvIcon.Visible) { var c = At(-CW * 0.3f, -CH * 0.36f); m.LvIcon.Position = c; m.LvNum.Position = c; }
    }

    /// "spell=3,fog=2" for St.Ctr.
    static string CountersOf(Card c) => string.Join(",", c.Vars.Where(kv => kv.Key.StartsWith("ctr:") && kv.Value > 0).Select(kv => $"{kv.Key[4..]}={kv.Value}"));

    // ---------------------------------------------------------------- card details: kept coin (U2), borrowed name (U5)

    Control? detailExtra;   // rows a card adds to the details; cleared on each Focus
    Card? borrowCard;
    int borrowTab;          // 0 = NOW (the copied effect), 1 = OWN TEXT

    Control DetailExtra()
    {
        if (detailExtra == null || !IsInstanceValid(detailExtra)) { detailExtra = new Control { Size = new Vector2(DetailW, 2000), MouseFilter = MouseFilterEnum.Ignore }; detail.AddChild(detailExtra); }
        return detailExtra;
    }
    void DetailExtraClear() { if (detailExtra != null && IsInstanceValid(detailExtra)) foreach (var n in detailExtra.GetChildren()) n.QueueFree(); }

    /// A kept coin result (Arcana Force: Vars arcana 1 = Heads, 2 = Tails) whose text has "● Heads" / "● Tails" lines.
    static (string Intro, string Kept, string Other)? CoinSplit(Card c)
    {
        int k = c.Var("arcana");
        if (k is not (1 or 2) || c.Loc != Loc.Monster || !c.FaceUp) return null;
        var lines = c.Data.Desc.Split('\n');
        string? h = lines.FirstOrDefault(s => s.StartsWith("● Heads")), t = lines.FirstOrDefault(s => s.StartsWith("● Tails"));
        if (h == null || t == null) return null;
        return (string.Join("\n", lines.Where(s => s != h && s != t)), k == 1 ? h : t, k == 1 ? t : h);
    }

    /// The card whose effect it has this turn (Arcana Call, Phantom of Chaos...: Vars copyidx = idx + 1).
    CardData? CopiedFrom(Card c) => c.Loc == Loc.Monster && c.FaceUp && c.Var("copyidx") > 0 && (c.Var("copyturn") == D.Turn || c.TempName != null) ? D.Db.Get(c.Var("copyidx") - 1) : null;

    /// The main text the details show: the copied effect on the NOW tab, the coin card's text without its two lines.
    string DetailText(Card c)
    {
        if (c != borrowCard) { borrowCard = c; borrowTab = 0; }
        if (CopiedFrom(c) is { } from && borrowTab == 0) return from.Desc;
        return CoinSplit(c) is { } cs ? cs.Intro : c.Data.Desc;
    }

    /// Under ATK/DEF: "AS <NAME> · until End Phase", and NOW / OWN TEXT tabs when it copies an effect. Returns the height.
    float BorrowRows(Card c, float y)
    {
        var from = CopiedFrom(c);
        string? name = c.Loc == Loc.Monster && c.FaceUp ? c.TempName ?? from?.Name : null;
        if (name == null) return 0;
        var x = DetailExtra();
        Put(x, $"AS {name.ToUpperInvariant()}  ·  until End Phase", Cond, 20, Borrowed, new Vector2(20, y), new Vector2(DetailW - 40, 26)).ClipText = true;
        if (from == null) return 30;
        float tw = (DetailW - 48) / 2;
        for (int i = 0; i < 2; i++)
        {
            int k = i;
            bool on = i == borrowTab;
            var tab = new Panel { Position = new Vector2(20 + i * (tw + 8), y + 32), Size = new Vector2(tw, 36), MouseFilter = MouseFilterEnum.Stop };
            tab.AddThemeStyleboxOverride("panel", Pill(on ? new Color(Borrowed, 0.2f) : new Color(1, 1, 1, 0.04f), on ? Borrowed : new Color(1, 1, 1, 0.15f), 10, 0, null, 0, on ? 2 : 1));
            x.AddChild(tab);
            var l = Put(tab, i == 0 ? $"NOW  ·  {from.Name.ToUpperInvariant()}" : "OWN TEXT", Cond, 17, Colors.White, Vector2.Zero, tab.Size, HorizontalAlignment.Center);
            l.VerticalAlignment = VerticalAlignment.Center; l.ClipText = true;
            tab.GuiInput += e => { if (e is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left } && borrowTab != k) { borrowTab = k; UiSound("cursor"); Focus(c); } };
        }
        return 30 + 46;
    }

    float Lines(string t, int size) => Ui.GetMultilineStringSize(t, HorizontalAlignment.Left, DetailW - 54, size).Y * 1.4f;

    /// The two coin lines' height at `size` (0 without a kept coin).
    float CoinNeed(Card c, int size) => CoinSplit(c) is { } cs ? Lines(cs.Kept, size) + Lines(cs.Other, size) + 16 : 0;

    /// The coin lines in the card's order, under the text at `y`; a gold bar and faint gold beside the kept one.
    void CoinRows(Card c, float y, int size)
    {
        if (CoinSplit(c) is not { } cs) return;
        var x = DetailExtra();
        bool headsFirst = cs.Kept.StartsWith("● Heads");
        foreach (var line in headsFirst ? new[] { cs.Kept, cs.Other } : new[] { cs.Other, cs.Kept })
        {
            float h = Lines(line, size);
            bool kept = line == cs.Kept;
            if (kept)
            {
                var bg = new Panel { Position = new Vector2(20, y - 2), Size = new Vector2(DetailW - 40, h + 4), MouseFilter = MouseFilterEnum.Ignore };
                bg.AddThemeStyleboxOverride("panel", Pill(new Color(RivalPanels.Gold, 0.08f), new Color(0, 0, 0, 0), 4, 0, null, 0, 0));
                x.AddChild(bg);
                x.AddChild(new ColorRect { Position = new Vector2(20, y - 2), Size = new Vector2(4, h + 4), Color = RivalPanels.Gold, MouseFilter = MouseFilterEnum.Ignore });
            }
            var l = Put(x, line, Ui, size, new Color(1, 1, 1, 0.82f), new Vector2(34, y), new Vector2(DetailW - 54, h));
            l.AutowrapMode = TextServer.AutowrapMode.WordSmart; l.ClipText = true;
            l.Size = new Vector2(DetailW - 54, h);
            y += h + 8;
        }
    }

    // ---------------------------------------------------------------- a hand kept revealed (U6)

    readonly bool[] handShown = new bool[2];   // per player: kept revealed (Mind on Air, Clear World with a LIGHT monster)

    bool HandShown(int p) =>
        D.Cards.Any(c => c.OnField && c.FaceUp && c.Name == "Mind on Air" && c.Controller != p)
        || D.Cards.Any(c => c.OnField && c.FaceUp && c.Name == "Clear World") && D.Cards.Any(c => c.Loc == Loc.Monster && c.FaceUp && c.Controller == p && D.Attr(c) == "LIGHT");

    // ---------------------------------------------------------------- a raised hand stays up for its pick (U3)

    /// The raised hand settles once a card leaves it or the turn / phase moves on (the steps in between, a payment, the
    /// reveal itself, keep it up for the pick).
    bool LineDone(Step s) => handLine is { } l && (l.Views.Any(v => s.Cards[v.Card.Uid].Loc != Loc.Hand) || s.Tp != shownTp || s.Turn != shownTurn || s.Phase != shownPhase);

    static string PickVerb(Prompt p) =>
        p.Text.Contains("hand", StringComparison.OrdinalIgnoreCase) ? "ADD TO HAND"
        : p.Text.Contains("discard", StringComparison.OrdinalIgnoreCase) ? "DISCARD"
        : p.Text.Contains("destroy", StringComparison.OrdinalIgnoreCase) ? "DESTROY" : "CONFIRM";

    /// A card of the raised hand picked: it lifts, rings, and the confirm button comes up under it.
    void LineSelect(HandLine l, Control c, int opt, Prompt p)
    {
        foreach (var f in l.Flats.Where(IsInstanceValid))
        {
            f.GetNode<Control>("ring").Visible = f == c;
            if (f.HasMeta("y0")) CreateTween().TweenProperty(f, "position:y", f == c ? (float)f.GetMeta("y0") - 18 : (float)f.GetMeta("y0"), 0.12);
        }
        Cue("pick");
        if (l.Go != null && IsInstanceValid(l.Go)) l.Go.QueueFree();
        const float w = 200, h = 46;
        var go = l.Go = Track(Box(hud, new Rect2(c.Position.X + c.Size.X / 2 - w / 2, c.Position.Y + c.Size.Y + 22, w, h), Pill(new Color(0.03f, 0.03f, 0.05f, 0.94f), RivalPanels.Gold, 23, 0, new Color(RivalPanels.Gold, 0.5f), 12, 2)), 0);
        go.MouseFilter = MouseFilterEnum.Stop;
        Put(go, PickVerb(p), Impact, 22, RivalPanels.Gold, Vector2.Zero, new Vector2(w, h), HorizontalAlignment.Center, 4).VerticalAlignment = VerticalAlignment.Center;
        go.GuiInput += e => { if (ask == p && e is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left }) Answer(opt); };
        FadeIn(go, 0.15f, new Vector2(0, -10));
        picked.Clear(); picked.Add(opt);   // the selftest confirms through sheetChips[0]
        sheetChips.Clear(); sheetChips.Add(() => { if (ask == p) Answer(opt); });
    }

    // ---------------------------------------------------------------- "Declare" pickers (U4)

    static readonly string[] Attributes = { "DARK", "LIGHT", "EARTH", "WATER", "FIRE", "WIND", "DIVINE" };
    static readonly Dictionary<string, Color> AttrColor = new()
    {
        ["DARK"] = new(0.72f, 0.48f, 1f), ["LIGHT"] = new(1f, 0.88f, 0.4f), ["EARTH"] = new(0.85f, 0.62f, 0.38f), ["WATER"] = new(0.4f, 0.72f, 1f),
        ["FIRE"] = new(1f, 0.45f, 0.3f), ["WIND"] = new(0.45f, 0.95f, 0.55f), ["DIVINE"] = new(1f, 0.95f, 0.75f),
    };

    /// "Declare ..." prompts: a card name gets the search grid (A), an Attribute the six marks, a Type the chips (D).
    /// False = not one of those (the plain list shows).
    bool DeclareSheet(Prompt p)
    {
        if (!p.Text.StartsWith("Declare", StringComparison.OrdinalIgnoreCase) || p.Options.Count < 2) return false;
        var texts = p.Options.Select(o => o.Text ?? "").ToList();
        var races = CardDb.Shared.ByIdx.Values.Where(d => d.IsMonster).Select(d => d.Race).ToHashSet();
        if (texts.All(t => Attributes.Contains(t))) AttrSheet(p, texts);
        else if (texts.All(t => races.Contains(t))) TypeSheet(p, texts);
        else if (texts.Count(t => CardDb.Shared.ByName.ContainsKey(t)) >= texts.Count - 1) NameSheet(p, texts);
        else return false;
        sheetChips.Clear(); sheetCards.Clear(); sheetLabels.Clear();
        for (int i = 0; i < Math.Min(texts.Count, 12); i++) { int k = i; sheetChips.Add(() => Answer(k)); sheetLabels.Add(texts[i]); }   // the selftest / --chip= answer through these
        return true;
    }

    Panel DeclareBase(Prompt p, string sub, float w, float h)
    {
        CloseSheet();
        sheet = Box(hud, new Rect2(W / 2 - w / 2, H / 2 - h / 2, w, h), Pill(new Color(0.03f, 0.03f, 0.05f, 0.95f), RivalPanels.Gold, 18, 0, new Color(RivalPanels.Gold, 0.25f), 12, 2));
        sheet.MouseFilter = MouseFilterEnum.Stop;
        Put(sheet, (p.Source?.Name ?? p.Text).ToUpperInvariant(), Impact, 30, Colors.White, new Vector2(28, 18), new Vector2(w - 56, 40), outline: 5).ClipText = true;
        Put(sheet, sub, Cond, 19, new Color(1, 1, 1, 0.55f), new Vector2(28, 58), new Vector2(w - 56, 26));
        return sheet;
    }
    Label GoButton(Control p, Rect2 r, Func<int> pick)
    {
        var go = Box(p, r, Pill(new Color(0.03f, 0.03f, 0.05f, 0.95f), RivalPanels.Gold, (int)(r.Size.Y / 2), 0, new Color(RivalPanels.Gold, 0.4f), 12, 2));
        go.MouseFilter = MouseFilterEnum.Stop;
        var l = Put(go, "", Impact, 20, RivalPanels.Gold, Vector2.Zero, r.Size, HorizontalAlignment.Center, 3);
        l.VerticalAlignment = VerticalAlignment.Center;
        go.GuiInput += e => { if (e is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left } && pick() >= 0) Answer(pick()); };
        return l;
    }
    static void OnClick(Control c, Action a)
    {
        c.MouseFilter = MouseFilterEnum.Stop;
        c.GuiInput += e => { if (e is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left }) a(); };
    }
    static Panel Ring(Control parent, Rect2 r, Color c)
    {
        var g = new Panel { Position = r.Position, Size = r.Size, MouseFilter = MouseFilterEnum.Ignore };
        g.AddThemeStyleboxOverride("panel", Pill(new Color(0, 0, 0, 0), c, (int)Math.Min(12, r.Size.X / 2), 0, new Color(c, 0.55f), 14, 3));
        parent.AddChild(g);
        return g;
    }

    /// A card name: search box + 5x2 grid of the matching names, the picked one on the DECLARE button.
    void NameSheet(Prompt p, List<string> texts)
    {
        const float cw = 150, gap = 16; const int cols = 5, rows = 2;
        float w = cols * cw + (cols - 1) * gap + 56, h = 172 + rows * (cw * CardH + gap) + 64;
        var s = DeclareBase(p, p.Text, w, h);
        // Names the prompt offers (in its order); other options (e.g. "(a card not in your Deck)") become a chip.
        var names = texts.Select((t, i) => (t, i)).Where(x => CardDb.Shared.ByName.ContainsKey(x.t)).ToList();
        var other = texts.Select((t, i) => (t, i)).Where(x => !CardDb.Shared.ByName.ContainsKey(x.t)).ToList();
        int sel = -1;
        var go = GoButton(s, new Rect2(28, h - 66, w - 56, 48), () => sel);
        var grid = new Control { Position = new Vector2(28, 166), MouseFilter = MouseFilterEnum.Ignore };
        s.AddChild(grid);
        StyleBoxFlat Field(Color edge) { var b = Pill(new Color(0.06f, 0.07f, 0.1f, 1), edge, 23); b.ContentMarginLeft = 50; return b; }
        float sw = other.Count > 0 ? w - 56 - 260 : w - 56;
        var e = new LineEdit { PlaceholderText = "Search a card name", Position = new Vector2(28, 100), Size = new Vector2(sw, 46) };
        e.AddThemeFontOverride("font", Ui); e.AddThemeFontSizeOverride("font_size", 20);
        e.AddThemeStyleboxOverride("normal", Field(new Color(RivalPanels.Gold, 0.45f))); e.AddThemeStyleboxOverride("focus", Field(RivalPanels.Gold));
        s.AddChild(e);
        Put(s, "⌕", Sym, 24, new Color(RivalPanels.Gold, 0.8f), new Vector2(44, 106), new Vector2(30, 34));
        foreach (var (t, i) in other.Take(1))
        {
            var chip = Box(s, new Rect2(w - 28 - 248, 100, 248, 46), Pill(new Color(1, 1, 1, 0.04f), new Color(1, 1, 1, 0.3f), 23));
            Put(chip, t, Ui, 16, Colors.White, Vector2.Zero, chip.Size, HorizontalAlignment.Center).VerticalAlignment = VerticalAlignment.Center;
            OnClick(chip, () => Answer(i));
        }
        void Draw()
        {
            foreach (var n in grid.GetChildren()) n.QueueFree();
            var q = e.Text.Trim();
            var hits = names.Where(x => x.t.Contains(q, StringComparison.OrdinalIgnoreCase)).OrderBy(x => !x.t.StartsWith(q, StringComparison.OrdinalIgnoreCase)).ThenBy(x => x.t).Take(cols * rows).ToList();
            if (!hits.Any(x => x.i == sel)) sel = hits.Count > 0 ? hits[0].i : -1;
            float ch = cw * CardH;
            for (int k = 0; k < hits.Count; k++)
            {
                var (t, i) = hits[k];
                var cell = new Vector2(k % cols * (cw + gap), k / cols * (ch + gap));
                if (i == sel) Ring(grid, new Rect2(cell - new Vector2(5, 5), new Vector2(cw + 10, ch + 10)), RivalPanels.Gold);
                var pic = new TextureRect { Texture = CardArt.Small(CardDb.Shared.Named(t)), ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.Scale, Position = cell, Size = new Vector2(cw, ch), TextureFilter = TextureFilterEnum.LinearWithMipmaps };
                if (i != sel) pic.Modulate = new Color(0.8f, 0.8f, 0.8f);
                grid.AddChild(pic);
                OnClick(pic, () => { sel = i; Draw(); });
                pic.MouseEntered += () => Focus(new Card(-1, CardDb.Shared.Named(t), 0));   // its text in the details
            }
            go.Text = sel >= 0 ? $"DECLARE  ·  {texts[sel].ToUpperInvariant()}" : "NO MATCH";
            FitText(go, 20, 12);
        }
        e.TextChanged += _ => Draw();
        Draw();
        e.CallDeferred(Control.MethodName.GrabFocus);
    }

    /// An Attribute: the marks from the card scans in a row (a name tile for one without a mark).
    void AttrSheet(Prompt p, List<string> texts)
    {
        const float s = 84, gap = 26;
        float w = Math.Max(560, texts.Count * (s + gap) - gap + 96), h = 320;
        var sh = DeclareBase(p, "Declare 1 Attribute", w, h);
        int sel = -1;
        var go = GoButton(sh, new Rect2(w / 2 - 170, h - 70, 340, 48), () => sel);
        go.Text = "PICK AN ATTRIBUTE";
        var row = new Control { Position = new Vector2((w - (texts.Count * (s + gap) - gap)) / 2, 104), MouseFilter = MouseFilterEnum.Ignore };
        sh.AddChild(row);
        void Draw()
        {
            foreach (var n in row.GetChildren()) n.QueueFree();
            for (int i = 0; i < texts.Count; i++)
            {
                int k = i; var a = texts[i]; var col = AttrColor.GetValueOrDefault(a, Colors.White); var at = new Vector2(i * (s + gap), 0);
                if (i == sel) Ring(row, new Rect2(at - new Vector2(6, 6), new Vector2(s + 12, s + 12)), col);
                Control t;
                if (ResourceLoader.Exists($"res://assets/counters/attr_{a.ToLowerInvariant()}.png"))
                    t = new TextureRect { Texture = Icon("attr_" + a.ToLowerInvariant()), ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.Scale, Position = at, Size = new Vector2(s, s), TextureFilter = TextureFilterEnum.LinearWithMipmaps };
                else
                {
                    t = new Panel { Position = at, Size = new Vector2(s, s) };
                    t.AddThemeStyleboxOverride("panel", Pill(new Color(col, 0.15f), col, (int)(s / 2), 0, null, 0, 2));
                    Put(t, a[0].ToString(), Impact, 40, col, Vector2.Zero, new Vector2(s, s), HorizontalAlignment.Center, 4).VerticalAlignment = VerticalAlignment.Center;
                }
                if (sel >= 0 && i != sel) t.Modulate = new Color(0.72f, 0.72f, 0.72f);
                row.AddChild(t);
                OnClick(t, () => { sel = k; Draw(); });
                Put(row, a, Cond, 20, i == sel ? col : new Color(1, 1, 1, 0.65f), at + new Vector2(-10, s + 12), new Vector2(s + 20, 26), HorizontalAlignment.Center);
            }
            if (sel >= 0) go.Text = $"DECLARE  ·  {texts[sel]}";
        }
        Draw();
    }

    /// A Type: clean word chips, five a row.
    void TypeSheet(Prompt p, List<string> texts)
    {
        const int cols = 5;
        // the card data holds a few broken Type strings ("?23"): their options stay answerable, just not shown
        var shown = Enumerable.Range(0, texts.Count).Where(i => System.Text.RegularExpressions.Regex.IsMatch(texts[i], "^[A-Za-z][A-Za-z -]*$")).ToList();
        float w = 900, cw = (w - 56 - (cols - 1) * 10) / cols, rows = (shown.Count + cols - 1) / cols, h = 100 + rows * 52 + 90;
        var sh = DeclareBase(p, "Declare 1 Type", w, h);
        int sel = -1;
        var go = GoButton(sh, new Rect2(w / 2 - 180, h - 70, 360, 48), () => sel);
        go.Text = "PICK A TYPE";
        var box = new Control { Position = new Vector2(28, 100), MouseFilter = MouseFilterEnum.Ignore };
        sh.AddChild(box);
        void Draw()
        {
            foreach (var n in box.GetChildren()) n.QueueFree();
            for (int n = 0; n < shown.Count; n++)
            {
                int i = shown[n], k = i; bool on = i == sel;
                var c = new Panel { Position = new Vector2(n % cols * (cw + 10), n / cols * 52), Size = new Vector2(cw, 42) };
                c.AddThemeStyleboxOverride("panel", Pill(on ? new Color(RivalPanels.Gold, 0.18f) : new Color(1, 1, 1, 0.04f), on ? RivalPanels.Gold : new Color(1, 1, 1, 0.16f), 21, 0, on ? new Color(RivalPanels.Gold, 0.45f) : null, on ? 10 : 0, on ? 2 : 1));
                box.AddChild(c);
                Put(c, texts[i], Ui, 18, on ? RivalPanels.Gold : Colors.White, Vector2.Zero, c.Size, HorizontalAlignment.Center).VerticalAlignment = VerticalAlignment.Center;
                OnClick(c, () => { sel = k; Draw(); });
            }
            if (sel >= 0) go.Text = $"DECLARE  ·  {texts[sel].ToUpperInvariant()}";
        }
        Draw();
    }

    // ---------------------------------------------------------------- `--gapdemo=WHAT`: screenshots of the above

    /// On a staged duel (`--scenario=try:field:<card>`), once it stands: marks (counters, a turn count, Levels) | coin
    /// (the tried card has kept Heads) | copy (it has Jinzo's effect) | name / attr / type (the Declare pickers) |
    /// skip (NO DRAW, END PHASE) | line (the rival's hand raised for a pick, one picked). Test aid only: it writes card
    /// state straight into the duel.
    async void GapDemo(string what)
    {
        await Wait(3.0);
        var tried = scenario?.Split(':').Last();
        Card? Mine(int nth) => D.Cards.Where(c => c.Loc == Loc.Monster && c.FaceUp && c.Controller == Me).OrderBy(c => c.Zone).Skip(nth).FirstOrDefault();
        Card? Theirs(int nth) => D.Cards.Where(c => c.Loc == Loc.Monster && c.FaceUp && c.Controller != Me).OrderBy(c => c.Zone).Skip(nth).FirstOrDefault();
        void Refresh() { foreach (var c in D.Cards) if (views.TryGetValue(c.Uid, out var v) && c.OnField) Place(v, StateOf(c)); }
        Prompt Ask(string text, IEnumerable<string> opts) => new() { Player = Me, Kind = PoC.Engine.Ask.Choose, Text = text, Options = opts.Select(o => new Opt(Act.Option, Text: o)).ToList() };
        var card = D.Cards.FirstOrDefault(c => c.Name == tried && c.OnField);
        switch (what)
        {
            case "marks":
                if (Mine(0) is { } a) a.Vars["ctr:spell"] = 3;
                if (Mine(1) is { } b) { b.Vars["modlevel"] = 1; b.Vars["turns"] = 2; }
                if (Theirs(0) is { } t0) t0.Vars["ctr:venom"] = 2;
                if (Theirs(1) is { } t1) t1.Vars["modlevel"] = -1;
                Refresh();
                break;
            case "coin":
                if (card != null) { card.Vars["arcana"] = 1; Refresh(); Focus(card); }
                break;
            case "copy":
                if (card != null) { card.Vars["copyidx"] = D.Db.Named("Jinzo").Idx + 1; card.Vars["copyturn"] = D.Turn; Refresh(); Focus(card); }
                break;
            case "name": DeclareSheet(Ask("Declare 1 card name", CardDb.Shared.ByName.Keys.Where(k => k.Length > 0))); break;
            case "attr": DeclareSheet(Ask("Declare an Attribute", new[] { "DARK", "DIVINE", "EARTH", "FIRE", "LIGHT", "WATER", "WIND" })); break;
            case "type": DeclareSheet(Ask("Declare a Type", CardDb.Shared.ByIdx.Values.Where(d => d.IsMonster && d.Race.Length > 0).Select(d => d.Race).Distinct().OrderBy(r => r))); break;
            case "skip": SkipWords("NO DRAW", 0); await Wait(1.4); SkipWords("END PHASE", 0); break;
            case "line":
            {
                var hand = views.Values.Where(v => v.S.Loc == Loc.Hand && v.S.Side == 1).OrderBy(v => v.S.Index).ToList();
                RaiseHand(hand, 1, $"{ThemName.ToUpperInvariant()}'S CARDS  ·  REVEALED");
                await Wait(1.0);
                var p = new Prompt { Player = Me, Kind = PoC.Engine.Ask.Pick, Text = "Add which monster to your hand?", Source = card,
                    Options = hand.Where(v => v.Card.Data.IsMonster).Select(v => new Opt(Act.Option, v.Card)).ToList() };
                ask = p;
                LinePick(p);
                await Wait(0.6);
                if (sheetCards.Count > 0) sheetCards[0]();
                break;
            }
        }
    }

    // ---------------------------------------------------------------- skipped draw / phases (U8)

    /// Short red words on the turn player's half, gone after ~0.8 s.
    void SkipWords(string text, int side)
    {
        FieldTitle(text, side, Bad, 150);
        var l = words; var tw = wordsTw;
        After(0.8, () =>
        {
            if (l == null || !IsInstanceValid(l) || words != l) return;
            tw?.Kill();
            var t = CreateTween().SetParallel();
            t.TweenProperty(l, "modulate:a", 0f, 0.2);
            t.TweenProperty(l, "outline_modulate:a", 0f, 0.2);
        });
    }
}
