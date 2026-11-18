using Godot;
using PoC.Engine;

namespace PoC.Game.UI;

/// Row 3: achievements (none exist yet; sample data). Round 2: the prizes are cosmetics from the shop list (card backs,
/// playmats, icon frames) plus DP, and the prize is what the screens show first. Each achievement keeps its card
/// picture as a medal (the unlock pop-up the user liked).
public partial class MenuLab
{
    enum Prize { Dp, Back, Mat, Frame }

    sealed record Ach(string Group, string Name, string Desc, int Have, int Goal, int Dp, string Card, Prize Kind = Prize.Dp, string Key = "", bool Claimed = false)
    {
        public bool Done => Have >= Goal;
        public bool Ready => Done && !Claimed;
    }

    static readonly string[] Groups = { "DUELS", "RIVALS", "COLLECTION", "MODES", "CHALLENGES" };
    static readonly Ach[] Achs =
    {
        new("DUELS", "FIRST BLOOD", "Win your first duel", 1, 1, 100, "Celtic Guardian", Claimed: true),
        new("DUELS", "DUELIST", "Win 25 duels", 18, 25, 200, "Dark Magician", Prize.Back, "01_vortex"),
        new("DUELS", "VETERAN", "Win 100 duels", 18, 100, 500, "Buster Blader", Prize.Mat, "starlane"),
        new("DUELS", "UNTOUCHED", "Win a duel without losing any LP", 1, 1, 300, "Mirror Force", Prize.Frame, "diamond"),
        new("DUELS", "FORBIDDEN ONE", "Win by gathering Exodia", 0, 1, 500, "Exodia the Forbidden One", Prize.Back, "12_millennium"),
        new("DUELS", "OVERKILL", "Deal 5,000 damage in one attack", 0, 1, 300, "Blue-Eyes Ultimate Dragon", Prize.Frame, "fire"),
        new("RIVALS", "KING OF GAMES", "Beat Yugi at EXPERT", 1, 1, 300, "Dark Magician Girl", Prize.Back, "14_art_dark_magician", Claimed: true),
        new("RIVALS", "ALL COMERS", "Beat every rival once", 9, 21, 800, "Thousand-Eyes Restrict", Prize.Mat, "pharaoh"),
        new("RIVALS", "GRUDGE", "Beat Kaiba 7 times in a row", 3, 7, 300, "Blue-Eyes White Dragon", Prize.Back, "15_art_blue_eyes"),
        new("RIVALS", "UNDERDOG", "Beat an EXPERT rival with a starter deck", 0, 1, 500, "Kuriboh", Prize.Frame, "master"),
        new("COLLECTION", "HALF WAY", "Own half of all cards", 41, 50, 500, "Pot of Greed", Prize.Mat, "radiant"),
        new("COLLECTION", "DRAGON HOARD", "Own 30 different Dragons", 30, 30, 300, "Red-Eyes B. Dragon", Prize.Back, "16_art_red_eyes"),
        new("COLLECTION", "TRIPLE WHITE", "Own 3 Blue-Eyes White Dragon", 2, 3, 300, "Blue-Eyes White Dragon", Prize.Mat, "art_blue_eyes"),
        new("COLLECTION", "DECKSMITH", "Build 10 decks", 6, 10, 200, "Card Destruction", Prize.Frame, "circuit"),
        new("MODES", "CHAMPION", "Win a tournament", 0, 1, 1000, "Gate Guardian", Prize.Mat, "cup_arena"),
        new("MODES", "RUN THE GAUNTLET", "Clear a gauntlet", 1, 1, 500, "Summoned Skull", Prize.Back, "30_chaos_vortex"),
        new("MODES", "LEGEND SLAYER", "Win LEGEND against 4 rivals", 2, 4, 600, "Black Luster Soldier", Prize.Frame, "millennium"),
        new("MODES", "SEALED", "Win a pack duel match", 0, 1, 300, "Monster Reborn"),
        new("MODES", "CHAOS RIDER", "Win 5 chaos duels", 5, 5, 300, "Chaos Emperor Dragon - Envoy of the End", Prize.Back, "11_hyperspace"),
        new("CHALLENGES", "PUZZLE MASTER", "Solve all 8 challenges", 5, 8, 800, "Jinzo", Prize.Frame, "rune"),
        new("CHALLENGES", "FIRST TRY", "Solve a challenge on the first try", 1, 1, 200, "Swords of Revealing Light", Claimed: true),
    };
    static int achGroup, achFocus = 3;

    static string PrizeName(Ach a) => a.Kind switch
    {
        Prize.Back => Shop.Backs.First(b => b.Key == a.Key).Name.ToUpperInvariant() + "  CARD BACK",
        Prize.Mat => Shop.Mats.First(b => b.Key == a.Key).Name.ToUpperInvariant() + "  PLAYMAT",
        Prize.Frame => Shop.Frames.First(b => b.Key == a.Key).Name.ToUpperInvariant() + "  ICON FRAME",
        _ => Lang.F("{0:N0} DP", a.Dp),
    };

    /// The prize drawn to fit a box of height `h` centred on `c`: a standing card back, a playmat, your picture in the
    /// frame, or a gold DP coin.
    void PrizeArt(Control host, Vector2 c, float h, Ach a, bool shadow = true)
    {
        bool light = OnLight(host.GlobalPosition + c);
        var dim = a.Done ? Colors.White : new Color(0.62f, 0.62f, 0.66f, 0.85f);
        switch (a.Kind)
        {
            case Prize.Back:
            {
                var sz = new Vector2(h * 0.686f, h);
                if (shadow) Shadow(host, c - sz / 2, sz, light ? 0.3f : 0.5f, light ? new Color(0.1f, 0.1f, 0.15f) : new Color(0.3f, 0.3f, 0.4f), new Vector2(0, h * 0.04f));
                Pic(host, UI.Cosmetics.Back(a.Key, h > 200 ? 0 : 256), c - sz / 2, sz).Modulate = dim;
                break;
            }
            case Prize.Mat:
            {
                var sz = new Vector2(h * 1.6f, h);
                if (shadow) Shadow(host, c - sz / 2, sz, light ? 0.3f : 0.5f, light ? new Color(0.1f, 0.1f, 0.15f) : new Color(0.3f, 0.3f, 0.4f), new Vector2(0, h * 0.04f));
                var p = Pic(host, Tex($"res://assets/mats/{a.Key}.jpg"), c - sz / 2, sz);
                p.StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered;
                p.Material = Kit.RoundedMat(1.6f, 0.06f);
                p.Modulate = dim;
                break;
            }
            case Prize.Frame:
            {
                var f = UI.Cosmetics.Framed(Avatar, a.Key, h);
                f.Position = c - new Vector2(h, h) / 2; f.Modulate = dim;
                host.AddChild(f);
                break;
            }
            default:
            {
                Plate(host, new Rect2(c - new Vector2(h, h) * 0.4f, new Vector2(h, h) * 0.8f), Box(a.Done ? GoldC : GoldC with { A = 0.4f }, GoldLight, Math.Max(2, h * 0.03f), 999, shadow ? 12 : 0, new Color(0.3f, 0.2f, 0.05f, 0.3f), new Vector2(0, 4)));
                Mid(host, "DP", Title, Math.Max(12, (int)(h * 0.22f)), Ink, new Rect2(c - new Vector2(h, h) * 0.4f, new Vector2(h, h) * 0.8f));
                break;
            }
        }
    }

    /// The achievement's medal: its card's art in a circle, gold ring when done, washed out while locked.
    void Medal(Control host, Vector2 c, float d, Ach a)
    {
        var root = new Control { Position = c - new Vector2(d, d) / 2, Size = new Vector2(d, d), MouseFilter = MouseFilterEnum.Ignore };
        host.AddChild(root);
        bool light = OnLight(host.GlobalPosition + c);
        root.AddChild(new ColorRect { Size = root.Size, Color = a.Done ? GoldC : (light ? Ink : Silver) * new Color(1, 1, 1, 0.2f), Material = Kit.RoundedMat(1, 0.5f), MouseFilter = MouseFilterEnum.Ignore });
        var art = Named(a.Card) ?? Db.Named("Kuriboh");
        var p = Pic(root, CardArt.Picture(art), new Vector2(3, 3), root.Size - new Vector2(6, 6));
        p.Material = Kit.RoundedMat(1, 0.5f);
        if (!a.Done) p.Modulate = new Color(0.5f, 0.5f, 0.55f, 0.6f);
    }

    string Progress(Ach a) => a.Claimed ? "CLAIMED" : a.Goal == 1 ? (a.Done ? "DONE" : "NOT YET") : $"{a.Have} / {a.Goal}";

    void Achievements(int v)
    {
        switch (v)
        {
            case 0: AchList(false); break;
            case 1: AchList(true); break;
            default: AchPopup(); break;
        }
    }

    /// Group words along the top (the Card List's ALL / MONSTER / SPELL style).
    void AchWords(Vector2 at)
    {
        var names = new[] { "ALL" }.Concat(Groups).ToArray();
        float x = at.X;
        for (int i = 0; i < names.Length; i++)
        {
            int k = i;
            int ready = Achs.Count(a => a.Ready && (k == 0 || a.Group == Groups[k - 1]));
            x += Word(this, names[i], new Vector2(x, at.Y), 16, i == achGroup, () => { achGroup = k; achFocus = 0; UiSound(false); Play(lastVar); }).W;
            if (ready > 0 && i > 0) { Plate(this, new Rect2(x + 8, at.Y + 2, 22, 22), Box(GoldC, default, 0, 99)); Mid(this, ready.ToString(), Body, 12, Ink, new Rect2(x + 8, at.Y + 1, 22, 22)); x += 30; }
            x += 30;
        }
    }

    /// The list in the light (round 3: no group tabs; ready ones first with a CLAIM button in the row, a gold bar on
    /// their left edge); pointing at a row shows its prize large in the dark. `groups` = small group names between rows.
    void AchList(bool groups)
    {
        Rift(0.6f, 0.55f);
        Head("ACHIEVEMENTS");
        int done = Achs.Count(a => a.Done);
        float lw = CrackX(H) - 84 - 80, rowH = 88;
        Put(this, Lang.F("{0} OF {1} DONE", done, Achs.Length), Thin, 15, Graphite, new Vector2(84, 128));
        Bar(this, new Vector2(84, 156), 260, done / (float)Achs.Length, true, 6);
        // ready ones first, then in progress, then finished; with groups, each group keeps that order inside it
        var order = Achs.OrderBy(a => a.Ready ? 0 : a.Done ? 2 : 1);
        var items = new List<(string? Head, Ach? A)>();
        if (groups)
            foreach (var g in Groups) { items.Add((g, null)); items.AddRange(order.Where(a => a.Group == g).Select(a => ((string?)null, (Ach?)a))); }
        else items.AddRange(order.Select(a => ((string?)null, (Ach?)a)));
        var spot = new Control { Size = new Vector2(W, H), MouseFilter = MouseFilterEnum.Ignore };
        AddChild(spot);
        void Focus(Ach a)
        {
            foreach (var c in spot.GetChildren()) c.QueueFree();
            float cx = (CrackX(H * 0.5f) + W) / 2 + 20;
            PrizeArt(spot, new Vector2(cx, H * 0.4f), a.Kind == Prize.Mat ? 260 : a.Kind == Prize.Frame ? 240 : 360, a);
            Mid(spot, PrizeName(a), Title, 24, a.Done ? GoldLight : Silver, new Rect2(cx - 330, H * 0.66f, 660, 34));
            if (a.Kind != Prize.Dp) Mid(spot, Lang.F("+ {0:N0} DP", a.Dp), Title, 16, Mist, new Rect2(cx - 300, H * 0.66f + 38, 600, 24));
            Mid(spot, a.Desc, Body, 19, Mist, new Rect2(cx - 330, H * 0.66f + 74, 660, 26));
            if (a.Ready) Btn(spot, "CLAIM", new Vector2(cx, H * 0.66f + 146), () => Flash("CLAIMED"), primary: true, size: 22, width: 220);
            else if (a.Claimed) Mid(spot, "✓  YOURS", Title, 18, GoldLight, new Rect2(cx - 180, H * 0.66f + 124, 360, 28));
            else
            {
                Bar(spot, new Vector2(cx - 180, H * 0.66f + 124), 360, a.Have / (float)a.Goal, false, 8);
                Mid(spot, Progress(a), Title, 14, Mist, new Rect2(cx - 180, H * 0.66f + 140, 360, 22));
            }
        }
        int rows = (int)((H - 280) / rowH);
        Paged(this, new Rect2(84, 200, lw, rows * rowH), items.Count, 1, rows, new Vector2(0, rowH), (i, host, at) =>
        {
            var (head, ach) = items[i];
            if (head != null)
            {
                Put(host, head, Thin, 14, Graphite, at + new Vector2(0, rowH - 34));
                Hair(host, at + new Vector2(0, rowH - 8), lw, Ink * new Color(1, 1, 1, 0.2f), 1);
                return;
            }
            var a = ach!;
            float h = rowH - 8, cy = h / 2;
            var bg = Plate(host, new Rect2(at, new Vector2(lw, h)), Box(new Color(1, 1, 1, 0), default, 0, 10));
            if (a.Ready) Plate(host, new Rect2(at + new Vector2(0, 10), new Vector2(4, h - 20)), Box(GoldC, default, 0, 99));
            Hair(host, at + new Vector2(0, rowH - 4), lw, Ink * new Color(1, 1, 1, 0.07f), 1);
            Medal(host, at + new Vector2(46, cy), 60, a);
            Put(host, a.Name, Title, 16, a.Done ? GoldDeep : Ink, at + new Vector2(92, cy - 28));
            Fit(host, a.Desc, Body, 16, 12, Graphite, new Rect2(at + new Vector2(92, cy + 2), new Vector2(lw - 420, 24))).HorizontalAlignment = HorizontalAlignment.Left;
            float px = lw - 310;
            if (a.Ready) Btn(host, "CLAIM", at + new Vector2(px + 90, cy), () => Flash("CLAIMED"), primary: true, size: 13, width: 150);
            else if (a.Claimed) Mid(host, "✓  YOURS", Title, 12, GoldDeep, new Rect2(at + new Vector2(px, cy - 12), new Vector2(180, 24)));
            else
            {
                Bar(host, at + new Vector2(px, cy - 6), 180, a.Have / (float)a.Goal, true, 6);
                Mid(host, Progress(a), Title, 11, Graphite, new Rect2(at + new Vector2(px, cy + 4), new Vector2(180, 20)));
            }
            PrizeArt(host, at + new Vector2(lw - 56, cy), 62, a, false);
            bg.MouseFilter = MouseFilterEnum.Pass;
            Click(bg, () => { }, hv => { bg.AddThemeStyleboxOverride("panel", Box(hv ? new Color(0, 0, 0, 0.035f) : new Color(1, 1, 1, 0), default, 0, 10)); if (hv) Focus(a); });
        }, new Vector2(84 + lw - 260, H - 60));
        Focus(order.First());
    }

    /// B: the prizes first. A wall of every prize in the dark; under each, what unlocks it and how far along you are.
    void AchWall()
    {
        Rift(0.3f, 0.26f);
        Head("ACHIEVEMENTS");
        int done = Achs.Count(a => a.Done), ready = Achs.Count(a => a.Ready);
        Put(this, "DONE", Thin, 15, Graphite, new Vector2(84, 170));
        Put(this, $"{done} / {Achs.Length}", Title, 44, GoldDeep, new Vector2(84, 192));
        Bar(this, new Vector2(86, 262), 300, done / (float)Achs.Length, true, 8);
        if (ready > 0) Btn(this, Lang.F("CLAIM ALL  ({0})", ready), new Vector2(240, 340), () => Flash("CLAIMED"), primary: true, size: 18);
        float gy = 420;
        foreach (var k in new[] { Prize.Back, Prize.Mat, Prize.Frame })
        {
            int have = Achs.Count(a => a.Kind == k && a.Done), all = Achs.Count(a => a.Kind == k);
            Put(this, k switch { Prize.Back => "CARD BACKS", Prize.Mat => "PLAYMATS", _ => "ICON FRAMES" }, Title, 16, Ink, new Vector2(84, gy));
            Put(this, $"{have} / {all}", Title, 16, GoldDeep, new Vector2(84, gy), new Vector2(300, 22), HorizontalAlignment.Right);
            Bar(this, new Vector2(86, gy + 32), 300, have / (float)all, true, 5);
            gy += 74;
        }
        var list = Achs.Where(a => a.Kind != Prize.Dp).OrderBy(a => a.Kind).ThenBy(a => a.Ready ? 0 : a.Done ? 2 : 1).ToArray();
        float x0 = CrackX(0) + 90, cw = 270, ch = 262;
        int cols = (int)((W - x0 - 40) / cw);
        Paged(this, new Rect2(x0, 110, cols * cw, H - 200), list.Length, cols, (int)((H - 200) / ch), new Vector2(cw, ch), (i, host, at) =>
        {
            var a = list[i];
            var c = at + new Vector2(cw / 2 - 10, 80);
            if (a.Ready) Plate(host, new Rect2(at + new Vector2(0, -8), new Vector2(cw - 20, ch - 4)), Box(new Color(0.15f, 0.12f, 0.07f, 0.9f), GoldLight, 2, 14));
            PrizeArt(host, c, a.Kind == Prize.Mat ? 92 : a.Kind == Prize.Frame ? 120 : 140, a);
            Fit(host, a.Name, Title, 13, 10, a.Done ? GoldLight : Silver, new Rect2(at + new Vector2(8, 162), new Vector2(cw - 36, 22)));
            Fit(host, a.Desc, Body, 15, 11, Mist, new Rect2(at + new Vector2(8, 186), new Vector2(cw - 36, 22)));
            if (a.Ready) Mid(host, "CLAIM", Title, 13, GoldLight, new Rect2(at + new Vector2(0, 212), new Vector2(cw - 20, 22)));
            else if (a.Claimed) Mid(host, "✓  YOURS", Title, 12, GoldLight, new Rect2(at + new Vector2(0, 212), new Vector2(cw - 20, 22)));
            else Bar(host, at + new Vector2(50, 222), cw - 120, a.Have / (float)a.Goal, false, 5);
        }, new Vector2(W - 320, H - 60));
    }

    /// C: what you see when one unlocks mid-menu: the card slides in at the top with the medal and the prize.
    void AchPopup()
    {
        Rift(0.58f, 0.44f);
        Put(this, "(the main menu or the results screen would be underneath)", Thin, 15, Graphite, new Vector2(84, H - 60));
        var a = Achs[3] with { Claimed = false };
        float w = 680, h = 132;
        var card = new Control { Position = new Vector2(W / 2 - w / 2, -h - 20), Size = new Vector2(w, h) };
        AddChild(card);
        Plate(card, new Rect2(Vector2.Zero, card.Size), Box(new Color(0.06f, 0.06f, 0.08f, 0.97f), GoldC, 2, 18, 30, new Color(0, 0, 0, 0.45f), new Vector2(0, 10)));
        var medal = new Control { Position = new Vector2(66, h / 2), Size = Vector2.Zero };
        card.AddChild(medal);
        Medal(medal, Vector2.Zero, 96, a);
        medal.Scale = new Vector2(0, 1);
        Put(card, "ACHIEVEMENT UNLOCKED", Thin, 13, GoldLight, new Vector2(128, 20));
        Put(card, a.Name, Title, 26, Silver, new Vector2(128, 42));
        Put(card, PrizeName(a) + Lang.F("   ·   +{0} DP", a.Dp), Title, 14, GoldLight, new Vector2(128, 86));
        var prize = new Control { Position = new Vector2(w - 70, h / 2), Size = Vector2.Zero, Modulate = new Color(1, 1, 1, 0) };
        card.AddChild(prize);
        PrizeArt(prize, Vector2.Zero, 100, a, false);
        var tw = CreateTween();
        tw.TweenInterval(0.4);
        tw.TweenProperty(card, "position:y", 40f, 0.45).SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
        tw.TweenProperty(medal, "scale:x", 1f, 0.35).SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
        tw.TweenProperty(prize, "modulate:a", 1f, 0.3);
        tw.TweenInterval(3.4);
        tw.TweenProperty(card, "position:y", -h - 20, 0.35).SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.In);
    }
}
