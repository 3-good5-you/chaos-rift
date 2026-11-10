using Godot;
using PoC.Engine;

namespace PoC.Game.UI;

/// Mockups for Duel Setup on the paper title card in black ink (not wired into the game). Decks are icons.
/// `-- --demo --variant=a|b|c|d [--rival=N]`
/// a = card stacks with a cover seal, b = deck boxes, c = face-off with ink seals, d = fanned decks,
/// e = A's layout with D's fans on a scrolling shelf and big numbered choices (`--decks=N` fakes N decks).
public partial class SetupSamples : MenuBase
{
    float W, H;
    RivalPanels.Rival r = null!;
    OpponentPick pick = null!;
    List<Deck> decks = null!;
    int deck, mode;
    readonly List<Action> refresh = new();
    static readonly Color Ink_ = Ink.Black, Faint = Ink.Black * new Color(1, 1, 1, 0.5f);

    protected override string? MusicPath => Md("solo_gate");
    protected override int ItemCount => 1;
    protected override void OnFocus(int old, int now) { }
    protected override void OnChoose(int i) { }
    protected override void OnBack() => GetTree().Quit();

    protected override void Build()
    {
        Demo.Ensure();
        Session.RollPicks();
        Session.Selected = int.Parse(Demo.Arg("rival", "1"));
        W = GetViewportRect().Size.X; H = GetViewportRect().Size.Y;
        r = Session.Rival;
        pick = Session.Pick;
        decks = Session.Decks.Where(d => DeckRules.IsLegal(d, Session.Profile)).ToList();
        // `--decks=N`: pad to N copies with other covers, to see a big collection.
        int want = int.Parse(Demo.Arg("decks", "0"));
        var names = new[] { "Dragon Fury", "Kaiba Starter", "Spellcaster Lock", "Burn", "Warrior Rush", "Fiend Control", "Toon World", "Exodia Draw", "Insect Swarm", "Machine Beat", "Zombie Return", "Fusion Frenzy" };
        for (int i = decks.Count; i < want; i++)
        {
            var src = decks[i % 2];
            var d = src.Copy($"{names[i % names.Length]}{(i >= names.Length ? $" {i / names.Length + 1}" : "")}");
            d.Cover = src.Main[(i * 7) % src.Main.Count];
            decks.Add(d);
        }
        Ink.Paper(this);
        switch (Demo.Arg("variant", "a"))
        {
            case "b": Boxes(); break;
            case "c": FaceOff(); break;
            case "d": Fans(); break;
            case "e": Final(); break;
            default: Stacks(); break;
        }
        Ink.Header(this, "DUEL SETUP", OnBack, onPaper: true);
        Update();
    }

    void Update() { foreach (var f in refresh) f(); }
    void SetDeck(int k) { deck = k; Update(); }
    void SetMode(int k) { mode = k; Update(); }
    static readonly string[] Modes = { "SINGLE DUEL", "MATCH  ·  BEST OF 3" };

    static void Click(Control c, Action a)
    {
        c.MouseFilter = MouseFilterEnum.Stop; c.MouseDefaultCursorShape = CursorShape.PointingHand;
        c.GuiInput += e => { if (e is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left }) a(); };
    }

    Label Put(Control p, string t, int size, Color c, Vector2 pos, Vector2 box = default, HorizontalAlignment al = HorizontalAlignment.Left) => Kit.Put(p, t, Kit.Cond, size, c, pos, box, al);

    /// A word that gets a black brush stroke behind it when on (cream text), faint ink when off.
    void Word(Control parent, string text, Vector2 pos, float w, int size, Func<bool> on, Action click)
    {
        var root = new Control { Position = pos, Size = new Vector2(w, size * 1.6f) };
        parent.AddChild(root);
        var b = Ink.Brush(new Vector2(-24, 0), new Vector2(w + 48, root.Size.Y), Ink_, root);
        b.Scale = new Vector2(0, 1);
        var l = Put(root, text, size, Ink_, Vector2.Zero, root.Size, HorizontalAlignment.Center);
        l.VerticalAlignment = VerticalAlignment.Center;
        Click(root, click);
        bool was = false;
        refresh.Add(() =>
        {
            bool now = on();
            l.AddThemeColorOverride("font_color", now ? Ink.Cream : Faint);
            if (now != was) CreateTween().TweenProperty(b, "scale:x", now ? 1f : 0f, now ? 0.28 : 0.15).SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
            was = now;
        });
    }

    void Modes_(Control parent, float x, float y, float gap = 370)
    {
        for (int i = 0; i < 2; i++) { int k = i; Word(parent, Modes[i], new Vector2(x + 24 + i * gap, y), gap - 40, 32, () => mode == k, () => SetMode(k)); }
        var hint = Put(parent, "", 26, Faint, new Vector2(x, y + 68));
        refresh.Add(() => hint.Text = mode == 1 ? "Win the match to earn 3 cards." : "Win to earn 1 card.");
    }

    /// START DUEL: a black brush stroke, cream letters, swells on hover.
    void Start(Vector2 pos, float w = 480)
    {
        var root = new Control { Position = pos, Size = new Vector2(w, 110), PivotOffset = new Vector2(w, 110) / 2 };
        AddChild(root);
        Ink.Brush(new Vector2(-30, 0), new Vector2(w + 60, 110), Ink_, root);
        var l = Put(root, "START DUEL", 54, Ink.Cream, Vector2.Zero, root.Size, HorizontalAlignment.Center);
        l.VerticalAlignment = VerticalAlignment.Center;
        Click(root, () => { });
        root.MouseEntered += () => root.CreateTween().TweenProperty(root, "scale", new Vector2(1.06f, 1.06f), 0.15).SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
        root.MouseExited += () => root.CreateTween().TweenProperty(root, "scale", Vector2.One, 0.15);
    }

    /// The rival in full colour on a black ink splat (as on the main menu).
    void RivalOnSplat(Vector2 centreBottom, float ph)
    {
        var tex = RivalPanels.Portrait(r);
        float pw = ph * tex.GetWidth() / tex.GetHeight(), s = ph * 1.05f;
        var sp = Ink.Pic(GD.Load<Texture2D>("res://assets/title/ink_splash.png"), centreBottom + new Vector2(-s / 2, -ph * 0.55f - s / 2), new Vector2(s, s), this);
        var sm = Ink.BleedMat(Ink_ * new Color(1, 1, 1, 0.85f), 0f, 3); sp.Material = sm;
        Ink.To(this, sm, "progress", 1.4f, 0.9);
        Ink.Pic(tex, centreBottom - new Vector2(pw / 2, ph), new Vector2(pw, ph), this);
    }

    void VsName(float x, float y, int size = 130)
    {
        Put(this, "V S", 34, Faint, new Vector2(x + 4, y));
        Kit.Put(this, r.Name[..1] + r.Name[1..].ToLowerInvariant(), Ink.Display, size, Ink_, new Vector2(x - 6, y + 20));
    }

    // ---- deck icons

    static Texture2D Art(Deck d) => CardArt.Picture(CardDb.Shared.Get(d.CoverCard()));
    static Texture2D Back => Cosmetics.Back(Session.Profile.Back, 360);

    static TextureRect Img(Texture2D t, Vector2 pos, Vector2 size, Control parent, ShaderMaterial? m = null)
    {
        var x = Ink.Pic(t, pos, size, parent);
        x.StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered; x.Material = m;
        return x;
    }

    static Panel Box(Vector2 pos, Vector2 size, Color bg, int radius, Control parent, int border = 0, Color? borderCol = null)
    {
        var p = new Panel { Position = pos, Size = size, MouseFilter = MouseFilterEnum.Ignore };
        p.AddThemeStyleboxOverride("panel", new StyleBoxFlat
        {
            BgColor = bg, BorderColor = borderCol ?? Colors.Transparent, BorderWidthLeft = border, BorderWidthRight = border, BorderWidthTop = border, BorderWidthBottom = border,
            CornerRadiusTopLeft = radius, CornerRadiusTopRight = radius, CornerRadiusBottomLeft = radius, CornerRadiusBottomRight = radius, CornerDetail = 12, AntiAliasing = true,
        });
        parent.AddChild(p);
        return p;
    }

    /// Round cover art in a black ring with a cream inner line.
    static void Medal(Vector2 centre, float d, Deck dk, Control parent)
    {
        Box(centre - new Vector2(d / 2 + 8, d / 2 + 8), new Vector2(d + 16, d + 16), Ink.Black, (int)(d / 2 + 8), parent);
        Box(centre - new Vector2(d / 2 + 3, d / 2 + 3), new Vector2(d + 6, d + 6), Ink.Cream, (int)(d / 2 + 3), parent);
        Img(Art(dk), centre - new Vector2(d / 2, d / 2), new Vector2(d, d), parent, Kit.RoundedMat(1, 0.5f));
    }

    /// Stack of your card backs with the cover card as a round seal on top.
    static Control Stack(Deck dk, float w)
    {
        float h = w * 1.458f;
        var root = new Control { Size = new Vector2(w + 14, h + 14) };
        for (int i = 3; i >= 0; i--)
        {
            var b = Img(Back, new Vector2(i * 4.5f, i * 4.5f), new Vector2(w, h), root, Kit.RoundedMat(1 / 1.458f, 0.04f));
            b.SelfModulate = new Color(1 - i * 0.12f, 1 - i * 0.12f, 1 - i * 0.12f);
        }
        Medal(new Vector2(w / 2, h / 2), w * 0.62f, dk, root);
        return root;
    }

    /// A deck box: black case with a lid line and the cover art in its window.
    static Control DeckBox(Deck dk, float w)
    {
        float h = w * 1.3f, depth = w * 0.09f;
        var root = new Control { Size = new Vector2(w + depth, h + depth) };
        Box(new Vector2(depth, 0), new Vector2(w, h), new Color(0.2f, 0.18f, 0.18f), 16, root);   // side / back
        Box(new Vector2(0, depth), new Vector2(w, h), Ink.Black, 16, root, 3, Ink.Cream * new Color(1, 1, 1, 0.25f));
        Box(new Vector2(14, depth + h * 0.2f), new Vector2(w - 28, 3), Ink.Cream * new Color(1, 1, 1, 0.35f), 1, root);   // lid
        float a = w * 0.72f;
        Box(new Vector2((w - a) / 2 - 4, depth + h * 0.29f - 4), new Vector2(a + 8, a + 8), Ink.Cream, 12, root);
        Img(Art(dk), new Vector2((w - a) / 2, depth + h * 0.29f), new Vector2(a, a), root, Kit.RoundedMat(1, 0.06f));
        Kit.Put(root, $"{dk.Main.Count}", Kit.Cond, (int)(w * 0.12f), Ink.Cream * new Color(1, 1, 1, 0.6f), new Vector2(16, depth + 10), new Vector2(w - 32, w * 0.16f), HorizontalAlignment.Right);
        return root;
    }

    /// The cover art as a round seal on a black ink splat.
    static Control Seal(Deck dk, float w)
    {
        var root = new Control { Size = new Vector2(w, w) };
        var sp = Ink.Pic(GD.Load<Texture2D>("res://assets/title/ink_splash.png"), new Vector2(-w * 0.2f, -w * 0.2f), new Vector2(w * 1.4f, w * 1.4f), root);
        sp.Material = Ink.BleedMat(Ink.Black, 1.4f, dk.Name.Length);
        Medal(new Vector2(w / 2, w / 2), w * 0.74f, dk, root);
        return root;
    }

    /// The cover card in front with two of your backs fanned behind it.
    static Control Fan(Deck dk, float w)
    {
        float h = w * 1.458f;
        var root = new Control { Size = new Vector2(w, h) };
        foreach (var rot in new[] { -0.22f, 0.22f, -0.1f })
        {
            var b = Img(Back, Vector2.Zero, new Vector2(w, h), root, Kit.RoundedMat(1 / 1.458f, 0.04f));
            b.PivotOffset = new Vector2(w / 2, h); b.Rotation = rot;
        }
        var c = Img(CardArt.Large(CardDb.Shared.Get(dk.CoverCard())), Vector2.Zero, new Vector2(w, h), root, Kit.RoundedMat(1 / 1.458f, 0.04f));
        c.PivotOffset = new Vector2(w / 2, h); c.Rotation = 0.06f;
        return root;
    }

    /// A row of deck icons; the chosen one lifts and gets a brush stroke under its name, the rest fade.
    void Decks(Func<Deck, float, Control> icon, float x, float y, float w, float gap, int max = 6)
    {
        for (int i = 0; i < Math.Min(max, decks.Count); i++)
        {
            int k = i;
            var ic = icon(decks[i], w);
            var root = new Control { Position = new Vector2(x + i * (w + gap), y), Size = ic.Size + new Vector2(0, 70), PivotOffset = new Vector2(ic.Size.X / 2, ic.Size.Y) };
            root.AddChild(ic);
            AddChild(root);
            var ul = Ink.Brush(new Vector2(-40, ic.Size.Y + 10), new Vector2(ic.Size.X + 80, 54), Ink_, root);
            ul.Scale = new Vector2(0, 1);
            var name = Put(root, decks[i].Name.ToUpperInvariant(), 24, Ink_, new Vector2(-30, ic.Size.Y + 18), new Vector2(ic.Size.X + 60, 40), HorizontalAlignment.Center);
            Click(root, () => SetDeck(k));
            bool was = false;
            refresh.Add(() =>
            {
                bool on = k == deck;
                ic.Modulate = on ? Colors.White : new Color(1, 1, 1, 0.6f);
                name.AddThemeColorOverride("font_color", on ? Ink.Cream : Faint);
                if (on == was) return;
                was = on;
                var tw = CreateTween().SetParallel();
                tw.TweenProperty(ul, "scale:x", on ? 1f : 0f, 0.25).SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
                tw.TweenProperty(root, "scale", on ? new Vector2(1.08f, 1.08f) : Vector2.One, 0.3).SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
            });
        }
    }

    // ---- a: settings column, card-stack decks

    void Stacks()
    {
        RivalOnSplat(new Vector2(W * 0.79f, H - 30), H * 0.78f);
        float x = 100;
        VsName(x, 250);
        Ink.Drops(this, x + 150, 440, pick, onPaper: true);
        Put(this, "MODE", 30, Faint, new Vector2(x, 520));
        Modes_(this, x, 560);
        Put(this, "YOUR DECK", 30, Faint, new Vector2(x, 690));
        Decks(Stack, x + 20, 740, 128, 60);
        Start(new Vector2(x + 20, H - 135));
    }

    // ---- b: rival left, deck boxes on the right

    void Boxes()
    {
        RivalOnSplat(new Vector2(W * 0.2f, H - 30), H * 0.72f);
        float x = W * 0.42f;
        VsName(x, 250, 110);
        Ink.Drops(this, x + 560, 330, pick, onPaper: true);
        Put(this, "MODE", 30, Faint, new Vector2(x, 450));
        Modes_(this, x, 490);
        Put(this, "YOUR DECK", 30, Faint, new Vector2(x, 620));
        Decks(DeckBox, x + 20, 670, 150, 50);
        Start(new Vector2(W - 600, H - 140));
    }

    // ---- c: face-off, ink-seal decks

    void FaceOff()
    {
        float A = 220;
        var you = new Control { Position = new Vector2(W * 0.2f - A / 2, 290), Size = new Vector2(A, A), MouseFilter = MouseFilterEnum.Ignore };
        var ys = Ink.Pic(GD.Load<Texture2D>("res://assets/title/ink_splash.png"), new Vector2(-A * 0.45f, -A * 0.45f), new Vector2(A * 1.9f, A * 1.9f), you);
        ys.Material = Ink.BleedMat(Ink_ * new Color(1, 1, 1, 0.85f), 1.4f, 1);
        you.AddChild(Cosmetics.Framed(Settings.Avatar, Session.Profile.Frame, A));
        AddChild(you);
        Put(this, Settings.PlayerName.ToUpperInvariant(), 56, Ink_, new Vector2(W * 0.2f - 300, 590), new Vector2(600, 70), HorizontalAlignment.Center);
        RivalOnSplat(new Vector2(W * 0.8f, 650), 440);
        Put(this, r.Name, 56, Ink_, new Vector2(W * 0.8f - 300, 650), new Vector2(600, 70), HorizontalAlignment.Center);
        Ink.Drops(this, W * 0.8f, 750, pick, onPaper: true);
        Kit.Put(this, "vs", Ink.Display, 190, Ink_, new Vector2(0, 300), new Vector2(W, 230), HorizontalAlignment.Center);
        Modes_(this, W / 2 - 370, 560);
        int n = Math.Min(6, decks.Count);
        float w = 150, gap = 70;
        Decks(Seal, W / 2 - (n * w + (n - 1) * gap) / 2, 820, w, gap);
        Start(new Vector2(W - 580, H - 140));
    }

    // ---- d: fanned decks along the middle

    void Fans()
    {
        RivalOnSplat(new Vector2(W * 0.82f, H - 30), H * 0.74f);
        float x = 100;
        VsName(x, 250, 110);
        Ink.Drops(this, x + 560, 330, pick, onPaper: true);
        Put(this, "YOUR DECK", 30, Faint, new Vector2(x, 450));
        Decks(Fan, x + 60, 530, 150, 110, 4);
        Put(this, "MODE", 30, Faint, new Vector2(x, 850));
        Modes_(this, x, 890);
        Start(new Vector2(x + 20, H - 140));
    }

    // ---- e: A's layout, fanned decks on a scrolling shelf, numbered choices

    void Final()
    {
        RivalOnSplat(new Vector2(W * 0.8f, H - 30), H * 0.76f);
        float x = 100;
        VsName(x, 236, 110);
        Ink.Drops(this, x + 560, 316, pick, onPaper: true);
        Shelf(x, 450, 1060);
        ModeTiles(x, 840);
        Start(new Vector2(W * 0.8f - 260, H - 130), 520);
    }

    /// A numbered step heading: big gothic numeral, a label, a brush rule under both.
    void Step(int n, string text, float x, float y)
    {
        Kit.Put(this, n.ToString(), Ink.Display, 76, Ink_, new Vector2(x - 6, y - 26));
        Put(this, text, 44, Ink_, new Vector2(x + 52, y));
        Ink.Brush(new Vector2(x + 44, y + 58), new Vector2(420, 12), Ink_ * new Color(1, 1, 1, 0.45f), this);
    }

    /// Fanned decks on a shelf that scrolls (arrows, wheel); the chosen deck is kept in view.
    void Shelf(float x, float y, float viewW)
    {
        Step(1, "CHOOSE YOUR DECK", x, y);
        var count = Put(this, "", 28, Faint, new Vector2(x + 520, y + 14), new Vector2(viewW - 520, 40), HorizontalAlignment.Right);
        // Fanned backs reach `spill` past each side; gap >= 2 * spill so only whole decks show at the clip edges.
        const float w = 132, spill = 44, gap = 92, step = w + gap;
        float h = w * 1.458f, top = y + 120;
        float arrowW = 60, innerW = viewW - arrowW * 2;
        int visible = (int)((innerW - spill * 2 + gap) / step);
        float clipW = visible * step - gap + spill * 2;
        var clip = new Control { Position = new Vector2(x + arrowW + (innerW - clipW) / 2, top - 50), Size = new Vector2(clipW, h + 150), ClipContents = true, MouseFilter = MouseFilterEnum.Ignore };
        var wheel = new Control { Position = clip.Position, Size = clip.Size, MouseFilter = MouseFilterEnum.Pass };
        AddChild(wheel);   // under the decks, so their clicks still land
        AddChild(clip);
        var strip = new Control { Position = new Vector2(0, 50), MouseFilter = MouseFilterEnum.Ignore };
        clip.AddChild(strip);
        const float pad = spill;
        for (int i = 0; i < decks.Count; i++)
        {
            int k = i;
            var ic = Fan(decks[i], w);
            var root = new Control { Position = new Vector2(pad + i * step, 0), Size = ic.Size + new Vector2(0, 70), PivotOffset = new Vector2(ic.Size.X / 2, ic.Size.Y) };
            root.AddChild(ic);
            strip.AddChild(root);
            var ul = Ink.Brush(new Vector2(-40, h + 10), new Vector2(w + 80, 54), Ink_, root);
            ul.Scale = new Vector2(0, 1);
            var name = Put(root, decks[i].Name.ToUpperInvariant(), 24, Ink_, new Vector2(-40, h + 18), new Vector2(w + 80, 40), HorizontalAlignment.Center);
            name.ClipText = true;
            Click(root, () => SetDeck(k));
            bool was = false;
            refresh.Add(() =>
            {
                bool on = k == deck;
                ic.Modulate = on ? Colors.White : new Color(1, 1, 1, 0.55f);
                name.AddThemeColorOverride("font_color", on ? Ink.Cream : Faint);
                if (on == was) return;
                was = on;
                var tw = CreateTween().SetParallel();
                tw.TweenProperty(ul, "scale:x", on ? 1f : 0f, 0.25).SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
                tw.TweenProperty(root, "scale", on ? new Vector2(1.1f, 1.1f) : Vector2.One, 0.3).SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
            });
        }
        int first = 0;
        Label Arrow(string t, float ax)
        {
            var a = Kit.Put(this, t, Kit.Cond, 110, Ink_, new Vector2(ax, top + h / 2 - 80), new Vector2(arrowW, 140), HorizontalAlignment.Center);
            a.VerticalAlignment = VerticalAlignment.Center;
            return a;
        }
        var left = Arrow("‹", x);
        var right = Arrow("›", x + viewW - arrowW);
        bool scrolls = decks.Count > visible;
        left.Visible = right.Visible = scrolls;
        void Scroll(int to)
        {
            first = Math.Clamp(to, 0, Math.Max(0, decks.Count - visible));
            CreateTween().TweenProperty(strip, "position:x", -first * step, 0.35).SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
            left.Modulate = new Color(1, 1, 1, first > 0 ? 1 : 0.15f);
            right.Modulate = new Color(1, 1, 1, first < decks.Count - visible ? 1 : 0.15f);
        }
        Click(left, () => Scroll(first - visible));
        Click(right, () => Scroll(first + visible));
        // Wheel over the shelf scrolls one deck.
        wheel.GuiInput += e =>
        {
            if (e is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.WheelDown or MouseButton.WheelRight }) Scroll(first + 1);
            if (e is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.WheelUp or MouseButton.WheelLeft }) Scroll(first - 1);
        };
        refresh.Add(() =>
        {
            count.Text = scrolls ? $"{deck + 1} / {decks.Count} DECKS" : "";
            if (deck < first) Scroll(deck); else if (deck >= first + visible) Scroll(deck - visible + 1);
        });
        // `--deck=N` picks a deck for screenshots.
        deck = Math.Clamp(int.Parse(Demo.Arg("deck", "0")), 0, decks.Count - 1);
        Scroll(0);
    }

    /// The two modes as big tiles: gothic numeral of duels, name, what you win. The chosen one is solid ink.
    void ModeTiles(float x, float y)
    {
        Step(2, "CHOOSE THE DUEL", x, y);
        var opts = new[] { ("1", "SINGLE DUEL", "One duel  ·  win 1 card"), ("3", "MATCH", "Best of 3  ·  win 3 cards") };
        const float tw = 470, th = 150;
        for (int i = 0; i < 2; i++)
        {
            int k = i;
            var (num, title, sub) = opts[i];
            var tile = new Panel { Position = new Vector2(x + 30 + i * (tw + 40), y + 96), Size = new Vector2(tw, th), PivotOffset = new Vector2(tw, th) / 2 };
            AddChild(tile);
            var n = Kit.Put(tile, num, Ink.Display, 120, Ink_, new Vector2(24, -6), new Vector2(90, th), HorizontalAlignment.Center);
            n.VerticalAlignment = VerticalAlignment.Center;
            var t = Put(tile, title, 50, Ink_, new Vector2(128, 22));
            var s = Put(tile, sub, 28, Faint, new Vector2(130, 88));
            Click(tile, () => SetMode(k));
            tile.MouseEntered += () => tile.CreateTween().TweenProperty(tile, "scale", new Vector2(1.03f, 1.03f), 0.15);
            tile.MouseExited += () => tile.CreateTween().TweenProperty(tile, "scale", Vector2.One, 0.15);
            refresh.Add(() =>
            {
                bool on = mode == k;
                tile.AddThemeStyleboxOverride("panel", new StyleBoxFlat
                {
                    BgColor = on ? Ink_ : Colors.Transparent, BorderColor = on ? Ink_ : Ink_ * new Color(1, 1, 1, 0.35f),
                    BorderWidthLeft = 4, BorderWidthRight = 4, BorderWidthTop = 4, BorderWidthBottom = 4,
                    CornerRadiusTopLeft = 22, CornerRadiusTopRight = 22, CornerRadiusBottomLeft = 22, CornerRadiusBottomRight = 22, CornerDetail = 10, AntiAliasing = true,
                });
                foreach (var l in new[] { n, t }) l.AddThemeColorOverride("font_color", on ? Ink.Cream : Ink_);
                s.AddThemeColorOverride("font_color", on ? Ink.Cream * new Color(1, 1, 1, 0.7f) : Faint);
            });
        }
    }
}
