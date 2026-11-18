using Godot;
using PoC.Engine;

namespace PoC.Game.UI;

/// Rows 4-7 (round 2): the TYPE filter (pick D, refined), Duel Setup's deck choice (today's look, refined; the duel
/// type is the picked toggle pill), all 21 rivals in regular duels, gauntlet rewards that grow the higher you climb.
public partial class MenuLab
{
    static string? typePick = "Dragon";

    static CardData? Named(string n) { try { return Db.Named(n); } catch (KeyNotFoundException) { return null; } }


    // ---------------------------------------------------------------- row 4: TYPE filter

    void TypeFilter(int v)
    {
        Rift(0.66f, 0.6f);
        Head("CARD LIST");
        float x0 = 84, y = 140;
        Put(this, "⌕  SEARCH", Body, 20, Graphite * new Color(1, 1, 1, 0.5f), new Vector2(x0, y));
        Hair(this, new Vector2(x0, y + 32), 200, Ink * new Color(1, 1, 1, 0.3f));
        float x = x0 + 240;
        foreach (var (w, on) in new[] { ("ALL", false), ("MONSTER", true), ("SPELL", false), ("TRAP", false) })
            x += Word(this, w, new Vector2(x, y + 2), 17, on, () => { }).W + 24;
        Put(this, "FILTERS ▴  1", Title, 16, GoldDeep, new Vector2(x + 6, y + 3));
        y += 50;
        Hair(this, new Vector2(x0, y), W * 0.56f, GoldC with { A = 0.45f });
        y += 18;
        for (int i = 0; i < CardQuery.Attrs.Length; i++)
        {
            string at = CardQuery.Attrs[i];
            var o = new Control { Position = new Vector2(x0 + i * 42, y - 2), Size = new Vector2(34, 34) };
            o.Draw += () => Kit.Orb(o, o.Size / 2, 15, at, 1f);
            AddChild(o);
        }
        y += 50;
        // the TYPE word, now a small capsule so it reads as a drop-down
        string label = Lang.Tr("TYPE") + ":  " + (typePick ?? "ANY").ToUpperInvariant() + "   ▴";
        float lw = Title.GetStringSize(label, HorizontalAlignment.Left, -1, Z(14)).X + 36;
        Plate(this, new Rect2(x0, y - 4, lw, 36), Box(Ink, default, 0, 99, 8, new Color(0, 0, 0, 0.18f), new Vector2(0, 3)));
        Mid(this, label, Title, 14, GoldLight, new Rect2(x0, y - 4, lw, 36));
        float gridTop = y + 70;
        var owned = P.Owned.Keys.Select(Db.Get).Where(c => c.IsMonster).ToList();
        var pool = owned.Where(c => typePick == null || c.Race == typePick).Take(14).ToList();
        float ch = 200, cw2 = ch * 0.686f;
        for (int i = 0; i < pool.Count; i++)
        {
            var at = new Vector2(x0 + cw2 / 2 + i % 7 * (cw2 + 18), gridTop + ch / 2 + i / 7 * (ch + 24));
            if (at.Y + ch / 2 > H - 20) break;
            Card(this, pool[i], at, ch, 0, 0.6f);
        }
        Put(this, Lang.F("{0} CARDS", pool.Count), Thin, 15, Graphite, new Vector2(x0 + lw + 24, y + 4));

        // the sheet: dims the cards behind it, a notch points at the TYPE capsule; ANY spans the top; two columns
        AddChild(new ColorRect { Position = new Vector2(0, y + 40), Size = new Vector2(CrackX(H), H - y - 40), Color = new Color(0.97f, 0.97f, 0.96f, 0.6f), MouseFilter = MouseFilterEnum.Ignore });
        var names = CardQuery.Races;
        const float cw = 250, rh = 40;
        int rows = (names.Length + 1) / 2;
        var pop = new Control { Position = new Vector2(x0, y + 50) };
        AddChild(pop);
        float pw = 2 * cw + 28, ph = rows * rh + 100;
        Plate(pop, new Rect2(-14, 0, pw, ph), Box(Colors.White, new Color(0, 0, 0, 0.07f), 1, 18, 34, new Color(0.1f, 0.1f, 0.15f, 0.25f), new Vector2(0, 12)));
        pop.AddChild(new Polygon2D { Polygon = new[] { new Vector2(30, 1), new Vector2(42, -11), new Vector2(54, 1) }, Color = Colors.White });
        Put(pop, "MONSTER TYPE", Thin, 13, Graphite, new Vector2(10, 18));
        var close = Mid(pop, "✕", Body, 18, Graphite, new Rect2(pw - 60, 10, 36, 30));
        Click(close, () => { });
        void Row(Rect2 r, string text, string? race, int count)
        {
            bool on = race == typePick;
            var hl = Plate(pop, r, Box(on ? GoldC with { A = 0.2f } : new Color(0, 0, 0, 0), default, 0, 10));
            hl.MouseFilter = MouseFilterEnum.Stop;
            var l = Put(pop, text, Body, 17, on ? GoldDeep : Ink, r.Position + new Vector2(14, 0), new Vector2(r.Size.X - 60, r.Size.Y));
            l.VerticalAlignment = VerticalAlignment.Center;
            if (v == 1) { var cl = Put(pop, count.ToString(), Body, 15, Graphite * new Color(1, 1, 1, 0.55f), r.Position, new Vector2(r.Size.X - (on ? 40 : 14), r.Size.Y), HorizontalAlignment.Right); cl.VerticalAlignment = VerticalAlignment.Center; }
            if (on) Mid(pop, "✓", Body, 17, GoldDeep, new Rect2(r.End.X - 36, r.Position.Y, 30, r.Size.Y));
            Click(hl, () => { typePick = race; UiSound(false); Play(lastVar); }, h => { if (!on) hl.AddThemeStyleboxOverride("panel", Box(h ? new Color(0, 0, 0, 0.05f) : new Color(0, 0, 0, 0), default, 0, 10)); });
        }
        Row(new Rect2(0, 48, 2 * cw, rh - 4), "Any type", null, owned.Count);
        Hair(pop, new Vector2(4, 48 + rh + 2), 2 * cw - 8, Ink * new Color(1, 1, 1, 0.08f), 1);
        for (int i = 0; i < names.Length; i++)
            Row(new Rect2(i / rows * cw, 52 + rh + i % rows * rh, cw - 8, rh - 4), names[i], names[i], owned.Count(c => c.Race == names[i]));
    }

    // ---------------------------------------------------------------- row 5: Duel Setup

    static List<Deck>? labDecks;
    static Deck? chosen;
    static bool labMatch;
    static int deckPage;

    static List<Deck> LabDecks()
    {
        if (labDecks != null) return labDecks;
        var names = new[] { "Dragon Fury", "Spellcaster Rush", "Warrior Wall", "Machine Burn", "Zombie Tide", "Fiend Control", "Aqua Storm", "Insect Swarm", "Toon Time", "Hero Fusion", "Beatdown", "Graveyard Loop" };
        var src = Session.Decks.Count > 0 ? Session.Decks : new List<Deck> { new("Starter", Db.Deck(DeckRules.Starters[0].Key).Main) };
        var rng = new Rng(3);
        labDecks = names.Select((n, i) =>
        {
            var d = src[i % src.Count].Copy(n);
            var mons = d.Main.Where(c => Db.Get(c).IsMonster).Distinct().ToList();
            if (mons.Count > 0) d.Cover = mons[rng.Int(mons.Count)];
            return d;
        }).ToList();
        chosen = labDecks[0];
        return labDecks;
    }

    void DeckChoice(int v)
    {
        var decks = LabDecks();
        Rift(0.64f, 0.58f);
        Back("RIVALS");
        var rv = RivalPanels.All[1];
        var tex = RivalPanels.Portrait(rv);
        float ph = H * 0.72f, pw = ph * tex.GetWidth() / tex.GetHeight();
        Pic(this, tex, new Vector2(W * 0.82f - pw / 2, H - ph), new Vector2(pw, ph));
        Mid(this, rv.Name, Title, 54, Silver, new Rect2(W * 0.82f - 300, 70, 600, 70));
        Mid(this, "EXPERT", Title, 18, Session.TierColor(6), new Rect2(W * 0.82f - 300, 142, 600, 26));
        float left = 110, right = CrackX(H * 0.6f) - 80;
        Put(this, "1    DUEL TYPE", Title, 20, Graphite, new Vector2(left, 110));
        TypePill(new Vector2(left, 152));
        DeckArea(v, new Rect2(left, 310, right - left, H - 310 - 150), decks);
        Btn(this, "START DUEL", new Vector2((left + right) / 2, H - 86), () => Flash("START DUEL"), primary: true, size: 28, width: 340);
    }

    /// The picked duel type (round 1 C): one pill, the chosen half ink with gold text, the rule line under it.
    void TypePill(Vector2 at)
    {
        string[] names = { "SINGLE DUEL", "MATCH" };
        string[] sub = { "One duel  ·  win 2 cards, 1 new", "Best of three, side deck between duels  ·  win 4 cards, 2 new" };
        float pw = 520, h = 58;
        var rect = new Rect2(at, new Vector2(pw, h));
        Plate(this, rect, Box(new Color(0, 0, 0, 0.06f), Ink * new Color(1, 1, 1, 0.14f), 1.5f, 99));
        Plate(this, new Rect2(at + new Vector2(labMatch ? pw / 2 + 4 : 4, 4), new Vector2(pw / 2 - 8, h - 8)), Box(Ink, default, 0, 99, 10, new Color(0, 0, 0, 0.25f), new Vector2(0, 3)));
        for (int i = 0; i < 2; i++)
        {
            bool m = i == 1, on = labMatch == m;
            var l = Mid(this, names[i], Title, 19, on ? GoldLight : Ink * new Color(1, 1, 1, 0.6f), new Rect2(at + new Vector2(i * pw / 2, 0), new Vector2(pw / 2, h)));
            Click(l, () => { labMatch = m; UiSound(false); Play(lastVar); });
        }
        Put(this, sub[labMatch ? 1 : 0], Body, 19, Graphite, at + new Vector2(6, h + 14));
    }

    /// A deck case as today: the cover card over two card backs. The name sits under it, shrunk to fit its cell so
    /// neighbours never overlap; the chosen one is lifted with a gold name and a diamond.
    void DeckCase(Control host, Deck d, Vector2 cellAt, float cellW, float cw, bool on, Action pick)
    {
        float ch = cw * 1.458f;
        var at = cellAt + new Vector2((cellW - cw) / 2, on ? -8 : 0);
        for (int k = 2; k >= 1; k--)
        {
            var b = Pic(host, UI.Cosmetics.Back(P.Back, 256), at + new Vector2(k * cw * 0.05f, -k * cw * 0.04f), new Vector2(cw, ch));
            b.PivotOffset = new Vector2(cw, ch) / 2; b.Rotation = k * 0.04f;
        }
        if (d.CoverCard() >= 0) Card(host, Db.Get(d.CoverCard()), at + new Vector2(cw, ch) / 2, ch, -0.02f, on ? 1.3f : 0.6f);
        Fit(host, d.Name.ToUpperInvariant(), BodyBold, 14, 11, on ? GoldDeep : Ink, new Rect2(cellAt + new Vector2(4, ch + 18), new Vector2(cellW - 8, 24)));
        if (on) Pip(host, cellAt + new Vector2(cellW / 2, ch + 56), 8);
        var hit = new Control { Position = cellAt, Size = new Vector2(cellW, ch + 50) };
        host.AddChild(hit);
        Click(hit, pick);
    }

    void DeckArea(int v, Rect2 r, List<Deck> decks)
    {
        Put(this, "2    YOUR DECK", Title, 20, Graphite, r.Position);
        int perRow = 5, rows = v == 1 ? 2 : 1, per = perRow * rows;
        int pages = (decks.Count + per - 1) / per;
        deckPage = Math.Clamp(deckPage, 0, pages - 1);
        int first = deckPage * per, last = Math.Min(decks.Count, first + per);
        Put(this, Lang.F("{0} - {1}  OF  {2} DECKS", first + 1, last, decks.Count), Thin, 14, Graphite, new Vector2(r.Position.X, r.Position.Y + 2), new Vector2(r.Size.X, 22), HorizontalAlignment.Right);
        float arrowW = 56, cellW = (r.Size.X - 2 * arrowW) / perRow;
        float cw = v == 1 ? Math.Min(118, cellW * 0.66f) : Math.Min(165, cellW * 0.76f), ch = cw * 1.458f, rowH = ch + 80;
        float top = r.Position.Y + 62;
        void Pick(Deck d) { chosen = d; UiSound(false); Play(lastVar); }
        for (int i = first; i < last; i++)
        {
            int k = i - first;
            DeckCase(this, decks[i], new Vector2(r.Position.X + arrowW + k % perRow * cellW, top + k / perRow * rowH), cellW, cw, decks[i] == chosen, () => Pick(decks[i]));
        }
        // round arrows at the row ends; they fade at the first / last page
        float ay = top + (rows * rowH - 80) / 2;
        foreach (int dir in new[] { -1, 1 })
        {
            bool live = dir < 0 ? deckPage > 0 : deckPage < pages - 1;
            var c = new Vector2(dir < 0 ? r.Position.X + arrowW / 2 - 6 : r.End.X - arrowW / 2 + 6, ay);
            var p = Plate(this, new Rect2(c - new Vector2(24, 24), new Vector2(48, 48)), live ? Box(Ink, default, 0, 99, 10, new Color(0, 0, 0, 0.25f), new Vector2(0, 3)) : Box(new Color(0, 0, 0, 0.06f), default, 0, 99));
            Mid(this, dir < 0 ? "‹" : "›", Title, 28, live ? GoldLight : Graphite * new Color(1, 1, 1, 0.35f), new Rect2(c - new Vector2(24, 30), new Vector2(48, 52)));
            p.MouseFilter = MouseFilterEnum.Stop;
            if (live) Click(p, () => { deckPage += dir; UiSound(false); Play(lastVar); });
        }
        // page dots
        float dy = top + rows * rowH - 6;
        for (int i = 0; i < pages; i++)
        {
            bool on = i == deckPage;
            var c = new Vector2(r.GetCenter().X + (i - (pages - 1) / 2f) * 24, dy);
            Plate(this, new Rect2(c - new Vector2(on ? 13 : 4.5f, 4.5f), new Vector2(on ? 26 : 9, 9)), Box(on ? GoldDeep : Ink * new Color(1, 1, 1, 0.22f), default, 0, 99));
        }
        if (v == 2 && chosen != null) DeckDetails(new Rect2(r.Position.X + arrowW, dy + 30, r.Size.X - 2 * arrowW, r.End.Y - dy - 20), chosen);
    }

    /// C: the chosen deck under the row: name, counts, its strongest monsters.
    void DeckDetails(Rect2 r, Deck d)
    {
        Sheet(this, r, false, 16);
        var cards = d.Main.Select(Db.Get).ToList();
        Put(this, d.Name.ToUpperInvariant(), Title, 22, GoldDeep, r.Position + new Vector2(26, 22));
        Put(this, Lang.F("{0} MAIN   ·   {1} FUSION   ·   {2} SIDE", d.Main.Count, d.Extra.Count, d.Side.Count), Title, 13, Graphite, r.Position + new Vector2(26, 62));
        Put(this, Lang.F("{0} monsters  ·  {1} spells  ·  {2} traps", cards.Count(c => c.IsMonster), cards.Count(c => c.Kind == Kind.Spell), cards.Count(c => c.Kind == Kind.Trap)), Body, 18, Graphite, r.Position + new Vector2(26, 88));
        var best = cards.Where(c => c.IsMonster).DistinctBy(c => c.Idx).OrderByDescending(c => c.Atk).Take(4).ToList();
        float w = Math.Min((r.Size.X * 0.5f - 3 * 14) / 4, (r.Size.Y - 36) * 0.686f), h = w / 0.686f;
        for (int i = 0; i < best.Count; i++) Card(this, best[i], new Vector2(r.End.X - 30 - w / 2 - i * (w + 14), r.GetCenter().Y), h, 0, 0.5f);
    }

    // ---------------------------------------------------------------- row 6: all rivals in regular duels

    static readonly string[] DmEra = { "yugi", "kaiba", "joey", "mai", "pegasus", "marik", "bakura", "ishizu", "odion", "weevil", "rex", "mako", "keith" };
    static string rivalFocus = "kaiba";
    static int lineupPage;
    static readonly Dictionary<string, Texture2D> busts = new();

    /// Head and shoulders cut from the portrait around the face box (a real texture, so rounded corners work).
    static Texture2D Bust(string key)
    {
        if (busts.TryGetValue(key, out var done)) return done;
        var rv = RivalPanels.Of(Tournament.Get(key));
        var t = RivalPanels.Portrait(rv);
        var img = t.GetImage();
        if (img.IsCompressed()) img.Decompress();
        float iw = img.GetWidth(), ih = img.GetHeight();
        var f = RivalPanels.Face(rv);
        float s = f.Z * iw, cx = f.X * iw + s / 2;
        float bw = Math.Min(iw, s * 2.1f), bh = Math.Min(ih, bw * 1.35f);
        bw = bh / 1.35f;
        float x = Math.Clamp(cx - bw / 2, 0, Math.Max(0, iw - bw)), y = Math.Clamp(f.Y * ih - s * 0.25f, 0, Math.Max(0, ih - bh));
        return busts[key] = ImageTexture.CreateFromImage(img.GetRegion(new Rect2I((int)x, (int)y, (int)bw, (int)bh)));
    }

    /// The focused rival large in the dark: portrait, name, difficulty, signature card, CHOOSE.
    void Spotlight(float cx, float ph)
    {
        var d = Tournament.Get(rivalFocus);
        var tex = PortraitOf(rivalFocus);
        float pw = ph * tex.GetWidth() / tex.GetHeight();
        var pic = Pic(this, tex, new Vector2(cx - pw / 2, H - ph), new Vector2(pw, ph));
        pic.Modulate = new Color(1, 1, 1, 0);
        pic.CreateTween().TweenProperty(pic, "modulate:a", 1f, 0.25);
        Put(this, d.Name, Title, 52, Silver, new Vector2(W - 660, 60), new Vector2(580, 66), HorizontalAlignment.Right);
        var pick = new OpponentPick(d.Key, d.Deck, d.Level);
        Put(this, pick.Tier + "   " + new string('●', d.Level), Title, 18, Session.TierColor(d.Level), new Vector2(W - 660, 132), new Vector2(580, 26), HorizontalAlignment.Right);
        Put(this, Lang.Tr(d.Card), Body, 20, Mist, new Vector2(W - 660, 164), new Vector2(580, 28), HorizontalAlignment.Right);
        Btn(this, "CHOOSE", new Vector2(W - 200, H - 80), () => Flash(d.Name), primary: true, size: 22, width: 220);
    }

    Texture2D PortraitOf(string key) => RivalPanels.Portrait(RivalPanels.Of(Tournament.Get(key)));

    void Rivals(int v)
    {
        var all = Tournament.Roster.Select(d => d.Key).ToArray();
        void Focus(string k) { if (rivalFocus != k) { rivalFocus = k; UiSound(false); Play(lastVar); } }
        switch (v)
        {
            case 0:
            {
                // today's rival select (names in the light, portrait in the dark), with the names in three columns
                Rift(0.56f, 0.5f);
                Head("CHOOSE YOUR RIVAL");
                Spotlight(W * 0.78f, H * 0.82f);
                var dm = all.Where(DmEra.Contains).ToArray();
                var gx = all.Where(k => !DmEra.Contains(k)).ToArray();
                var cols = new[] { dm.Take(7).ToArray(), dm.Skip(7).ToArray(), gx };
                float colW = 320, y0 = 220, rowH = (H - y0 - 60) / 8;
                Put(this, "DUEL MONSTERS", Thin, 14, Graphite, new Vector2(110, 168));
                Put(this, "GX", Thin, 14, Graphite, new Vector2(110 + 2 * colW, 168));
                Hair(this, new Vector2(110, 194), 2 * colW - 40, Ink * new Color(1, 1, 1, 0.15f), 1);
                Hair(this, new Vector2(110 + 2 * colW, 194), colW - 40, Ink * new Color(1, 1, 1, 0.15f), 1);
                for (int c = 0; c < 3; c++)
                    for (int i = 0; i < cols[c].Length; i++)
                    {
                        var k = cols[c][i];
                        bool on = k == rivalFocus;
                        var at = new Vector2(110 + c * colW, y0 + i * rowH);
                        var d = Tournament.Get(k);
                        if (on) Pip(this, at + new Vector2(-22, rowH / 2 - 4), 10);
                        var l = Put(this, d.Name, Title, on ? 28 : 24, on ? GoldDeep : Ink * new Color(1, 1, 1, 0.62f), at + new Vector2(0, rowH / 2 - 30), new Vector2(colW - 20, 44));
                        l.ClipText = true;
                        Plate(this, new Rect2(at + new Vector2(2, rowH / 2 + 18), new Vector2(8 + d.Level * 9, 4)), Box(Session.TierColor(d.Level), default, 0, 99));
                        Click(l, () => Flash(d.Name), h => { if (h) Focus(k); });
                    }
                break;
            }
            case 1:
            {
                // all 21 at once: head-and-shoulders cards with the name and difficulty on each
                Rift(0.9f, 0.86f);
                Head("CHOOSE YOUR RIVAL");
                const int cols = 8;
                float gx = 20, cw = (CrackX(H) - 84 - 60 - (cols - 1) * gx) / cols, ch = cw * 1.35f, gy = 20;
                float y = 150;
                foreach (var (era, keys) in new[] { ("DUEL MONSTERS", all.Where(DmEra.Contains).ToArray()), ("GX", all.Where(k => !DmEra.Contains(k)).ToArray()) })
                {
                    Put(this, era, Thin, 14, Graphite, new Vector2(84, y));
                    y += 28;
                    for (int i = 0; i < keys.Length; i++)
                    {
                        var k = keys[i];
                        var d = Tournament.Get(k);
                        var at = new Vector2(84 + i % cols * (cw + gx), y + i / cols * (ch + gy));
                        var root = new Control { Position = at, Size = new Vector2(cw, ch), PivotOffset = new Vector2(cw, ch) / 2 };
                        AddChild(root);
                        var pic = Pic(root, Bust(k), Vector2.Zero, root.Size);
                        pic.Material = Kit.RoundedMat(cw / ch, 0.05f);
                        var band = new Rect2(0, ch - 52, cw, 52);
                        Plate(root, band, new StyleBoxFlat { BgColor = new Color(0.04f, 0.04f, 0.05f, 0.82f), CornerRadiusBottomLeft = (int)(cw * 0.05f), CornerRadiusBottomRight = (int)(cw * 0.05f), AntiAliasing = true });
                        Fit(root, d.Name, Title, 16, 11, Silver, new Rect2(6, ch - 50, cw - 12, 28));
                        Mid(root, new OpponentPick(k, "", d.Level).Tier, Title, 10, Session.TierColor(d.Level), new Rect2(0, ch - 26, cw, 18));
                        var ring = Plate(root, new Rect2(-3, -3, cw + 6, ch + 6), Box(new Color(0, 0, 0, 0), GoldC, 3, (int)(cw * 0.06f)));
                        ring.Visible = false;
                        Click(root, () => Flash(d.Name), h =>
                        {
                            ring.Visible = h;
                            Ink_.Only(this, root).TweenProperty(root, "scale", h ? new Vector2(1.06f, 1.06f) : Vector2.One, 0.14).SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
                            root.ZIndex = h ? 2 : 0;
                        });
                    }
                    y += (keys.Length + cols - 1) / cols * (ch + gy) + 14;
                }
                break;
            }
            default:
            {
                // a line-up: seven rivals standing side by side; the one you point at steps forward, the rest dim
                Rift(0.5f, 0.45f);
                Head("CHOOSE YOUR RIVAL");
                const int per = 7;
                int pages = (all.Length + per - 1) / per;
                lineupPage = Math.Clamp(lineupPage, 0, pages - 1);
                var keys = all.Skip(lineupPage * per).Take(per).ToArray();
                float slot = (W - 260) / per, ph = H * 0.66f, baseY = H - 110;
                var pics = new List<(Control Root, string Key)>();
                Label name = null!, tier = null!;
                for (int i = 0; i < keys.Length; i++)
                {
                    var k = keys[i];
                    var tex = PortraitOf(k);
                    float pw = ph * tex.GetWidth() / tex.GetHeight();
                    var cx = 130 + slot * (i + 0.5f);
                    var root = new Control { Position = new Vector2(cx - pw / 2, baseY - ph), Size = new Vector2(pw, ph), PivotOffset = new Vector2(pw / 2, ph), MouseFilter = MouseFilterEnum.Ignore };
                    AddChild(root);
                    Pic(root, tex, Vector2.Zero, root.Size);
                    var hit = new Control { Position = new Vector2(cx - slot / 2, baseY - ph * 0.8f), Size = new Vector2(slot, ph * 0.8f) };
                    AddChild(hit);
                    pics.Add((root, k));
                    Click(hit, () => Flash(Tournament.Get(k).Name), h => { if (h) Show(k); });
                }
                Plate(this, new Rect2(0, baseY - 6, W, H - baseY + 6), Box(new Color(0.03f, 0.03f, 0.04f, 0.92f), default, 0, 0));
                Hair(this, new Vector2(0, baseY - 6), W, GoldC with { A = 0.6f });
                name = Mid(this, "", Title, 34, Silver, new Rect2(0, baseY + 8, W, 46));
                tier = Mid(this, "", Title, 15, Silver, new Rect2(0, baseY + 56, W, 24));
                void Show(string k)
                {
                    rivalFocus = k;
                    foreach (var (root, key) in pics)
                    {
                        bool on = key == k;
                        root.ZIndex = on ? 1 : 0;
                        var tw = Ink_.Only(this, root).SetParallel();
                        tw.TweenProperty(root, "scale", on ? new Vector2(1.08f, 1.08f) : new Vector2(0.94f, 0.94f), 0.18).SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
                        tw.TweenProperty(root, "modulate", on ? Colors.White : new Color(0.45f, 0.45f, 0.5f, 0.85f), 0.18);
                    }
                    var d = Tournament.Get(k);
                    name.Text = d.Name;
                    tier.Text = new OpponentPick(k, "", d.Level).Tier + "   ·   " + Lang.Tr(d.Card);
                    tier.AddThemeColorOverride("font_color", Session.TierColor(d.Level));
                }
                Show(keys.Contains(rivalFocus) ? rivalFocus : keys[0]);
                foreach (int dir in new[] { -1, 1 })
                {
                    bool live = dir < 0 ? lineupPage > 0 : lineupPage < pages - 1;
                    var c = new Vector2(dir < 0 ? 60 : W - 60, baseY - ph * 0.4f);
                    var p = Plate(this, new Rect2(c - new Vector2(26, 26), new Vector2(52, 52)), live ? Box(Ink, GoldC, 1.5f, 99, 10, new Color(0, 0, 0, 0.3f)) : Box(new Color(0.5f, 0.5f, 0.5f, 0.15f), default, 0, 99));
                    Mid(this, dir < 0 ? "‹" : "›", Title, 30, live ? GoldLight : Mist * new Color(1, 1, 1, 0.4f), new Rect2(c - new Vector2(26, 32), new Vector2(52, 56)));
                    p.MouseFilter = MouseFilterEnum.Stop;
                    if (live) Click(p, () => { lineupPage += dir; UiSound(false); Play(lastVar); });
                }
                Put(this, Lang.F("{0} / {1}", lineupPage + 1, pages), Thin, 14, Mist, new Vector2(W - 200, baseY + 30), new Vector2(150, 22), HorizontalAlignment.Right);
                break;
            }
        }
    }

    // ---------------------------------------------------------------- row 7: gauntlet rewards

    /// Round 2 proposal: eight floors, the rivals get harder and every floor pays more than the one before. What you
    /// win is banked floor by floor, so losing keeps it. Card backs, a frame and a playmat wait at floors 3, 6 and 8.
    static readonly (int Dp, int Cards, Prize Kind, string Key)[] Floors =
    {
        // round 4 (user): each floor is the WHOLE prize for getting that far (not added up): the run pays the prize of
        // the highest floor you beat. DP rises every floor; cards go up by one every second floor (beat 3 = 250 DP +
        // 1 card, beat 5 = 600 DP + 2 cards).
        (100, 0, Prize.Dp, ""), (150, 1, Prize.Dp, ""), (250, 1, Prize.Dp, ""), (400, 2, Prize.Dp, ""),
        (600, 2, Prize.Dp, ""), (850, 3, Prize.Dp, ""), (1200, 3, Prize.Dp, ""), (2000, 4, Prize.Dp, ""),
    };
    static readonly string[] Climb = { "odion", "weevil", "mako", "alexis", "bakura", "mai", "chazz", "kaiba" };
    const int Floor = 3;   // floors cleared in the sample

    Ach FloorAch(int i) => new("", "", "", i < Floor ? 1 : 0, 1, Floors[i].Dp, "Kuriboh", Floors[i].Kind, Floors[i].Key);

    string FloorPrize(int i)
    {
        var f = Floors[i];
        string s = Lang.F("{0:N0} DP", f.Dp);
        if (f.Cards > 0) s += Lang.F("  +{0} card{1}", f.Cards, f.Cards > 1 ? "s" : "");
        return s;
    }

    void Banked(Vector2 at, bool light)
    {
        var (dp, cards, _, _) = Floors[Floor - 1];
        Put(this, Lang.F("IF THE RUN ENDS NOW  ·  FLOOR {0} BEATEN", Floor), Thin, 14, light ? Graphite : Mist, at);
        Put(this, Lang.F("{0:N0} DP  +  {1} card{2}", dp, cards, cards == 1 ? "" : "s"), Title, 18, light ? GoldDeep : GoldLight, at + new Vector2(0, 22));
        Put(this, "You take the prize of the highest floor you beat. Climb higher, it grows.", Body, 17, light ? Graphite : Mist, at + new Vector2(0, 54));
    }

    void Gauntlet(int v)
    {
        switch (v)
        {
            case 0: Tower(); break;
            case 1: Steps(); break;
            default: Ladder(); break;
        }
    }

    /// A: the tower, floor 1 at the bottom; the prize lines grow with height; the top prize shown large in the dark.
    void Tower()
    {
        Rift(0.6f, 0.55f);
        Head("GAUNTLET");
        Put(this, "YOUR LP  4,300", Title, 18, Graphite, new Vector2(84, 120));
        float top = 170, bottom = H - 50, fh = (bottom - top) / 8, x = 84, w = CrackX(H) - 84 - 90;
        for (int i = 0; i < 8; i++)
        {
            float y = bottom - (i + 1) * fh;
            bool done = i < Floor, now = i == Floor;
            var rect = new Rect2(x, y + 4, w, fh - 8);
            Plate(this, rect, now ? Box(new Color(1f, 0.96f, 0.86f), GoldC, 2.5f, 12, 14, GoldC with { A = 0.25f }, new Vector2(0, 3))
                : Box(new Color(1, 1, 1, done ? 0.5f : 0.85f), new Color(0, 0, 0, 0.08f), 1, 12));
            Mid(this, (i + 1).ToString(), Title, 22, done || now ? GoldDeep : Graphite * new Color(1, 1, 1, 0.5f), new Rect2(x + 4, y + 4, 40, fh - 8));
            var k = Climb[i];
            float d = fh - 30, fx = x + 50 + d / 2;
            Face(this, new Vector2(fx, y + fh / 2), d, k, now, done || now ? 1 : 0.6f);
            Put(this, Tournament.Get(k).Name, Title, 16, done ? Graphite : Ink, new Vector2(fx + d / 2 + 16, y + fh / 2 - 24));
            var dd = Tournament.Get(k);
            Put(this, new OpponentPick(k, "", dd.Level).Tier, Title, 11, Session.TierColor(dd.Level).Darkened(0.3f), new Vector2(fx + d / 2 + 16, y + fh / 2 + 4));
            int size = 13 + i * 2;
            if (now) Btn(this, "DUEL", new Vector2(x + w * 0.46f, y + fh / 2), () => Flash("DUEL"), primary: true, size: 16, width: 130);
            if (done) Mid(this, "✓", Title, 20, GoldDeep, new Rect2(x + w * 0.43f, y + 4, 60, fh - 8));
            float artW = Floors[i].Kind == Prize.Dp ? 0 : Floors[i].Kind == Prize.Mat ? (fh - 26) * 1.6f + 20 : fh;
            float boxW = w * 0.45f - 20 - artW;
            while (size > 11 && Title.GetStringSize(FloorPrize(i), HorizontalAlignment.Left, -1, Z(size)).X > boxW) size--;
            Put(this, FloorPrize(i), Title, size, done ? Graphite * new Color(1, 1, 1, 0.5f) : GoldDeep, new Vector2(x + w * 0.55f, y + 4), new Vector2(w * 0.45f - 20 - artW, fh - 8), HorizontalAlignment.Right).VerticalAlignment = VerticalAlignment.Center;
            if (Floors[i].Kind != Prize.Dp) PrizeArt(this, new Vector2(x + w - 14 - artW / 2 + 10, y + fh / 2), fh - 26, FloorAch(i), false);
        }
        float cx = (CrackX(H * 0.4f) + W) / 2 + 20;
        Mid(this, "AT THE TOP", Thin, 15, Mist, new Rect2(cx - 300, 120, 600, 24));
        PrizeArt(this, new Vector2(cx, 330), 240, FloorAch(7) with { Have = 1 });
        Mid(this, "OBSIDIAN PLAYMAT", Title, 26, GoldLight, new Rect2(cx - 300, 480, 600, 36));
        Mid(this, "+ 2,000 DP  +  3 cards", Title, 18, Silver, new Rect2(cx - 300, 522, 600, 28));
        Banked(new Vector2(CrackX(640) + 70, 640), false);
    }

    /// B: a staircase climbing left to right; each step higher, its prize larger.
    void Steps()
    {
        Rift(0.5f, 0.42f);
        Head("GAUNTLET");
        Banked(new Vector2(84, 130), true);
        float x0 = 90, stepW = (W - 180) / 8, baseY = H - 70, rise = (H - 420) / 8;
        for (int i = 0; i < 8; i++)
        {
            float x = x0 + i * stepW, top = baseY - (i + 1) * rise - 120;
            bool done = i < Floor, now = i == Floor;
            var c = new Vector2(x + stepW / 2, top);
            bool light = OnLight(c);
            Plate(this, new Rect2(x + 6, top + 70, stepW - 12, baseY - top - 70), now ? Box(light ? new Color(1f, 0.95f, 0.85f) : new Color(0.16f, 0.13f, 0.08f, 0.95f), GoldC, 2.5f, 10)
                : Box(light ? new Color(1, 1, 1, done ? 0.45f : 0.85f) : new Color(1, 1, 1, done ? 0.05f : 0.1f), new Color(0.5f, 0.5f, 0.5f, 0.15f), 1, 10));
            Face(this, c, now ? 92 : 78, Climb[i], now || done, done || now ? 1 : 0.6f);
            var ink = light ? Ink : Silver; var gold = light ? GoldDeep : GoldLight;
            Fit(this, Tournament.Get(Climb[i]).Name, Title, 14, 10, now ? gold : ink, new Rect2(x + 8, top + 80, stepW - 16, 22));
            Mid(this, Lang.F("FLOOR {0}", i + 1), Thin, 11, ink * new Color(1, 1, 1, 0.6f), new Rect2(x, top + 104, stepW, 18));
            Fit(this, Lang.F("{0:N0} DP", Floors[i].Dp), Title, 16 + i * 2, 12, done ? ink * new Color(1, 1, 1, 0.45f) : gold, new Rect2(x + 8, top + 136, stepW - 16, 40));
            if (Floors[i].Cards > 0) Mid(this, Lang.F("+ {0} CARD{1}", Floors[i].Cards, Floors[i].Cards > 1 ? "S" : ""), Title, 13 + Floors[i].Cards, done ? ink * new Color(1, 1, 1, 0.45f) : gold, new Rect2(x, top + 182, stepW, 22));
            if (done) Tag(this, "✓", new Vector2(x + stepW / 2 + 40, top - 34), GoldC, Ink, 12);
            if (now) Btn(this, "DUEL", new Vector2(x + stepW / 2, baseY - 40), () => Flash("DUEL"), primary: true, size: 16, width: stepW - 30);
        }
    }

    /// C: one track across the screen; the DP under each floor grows; the three cosmetic prizes hang over their
    /// floors as milestones.
    void Ladder()
    {
        Rift(0.5f, 0.5f);
        Head("GAUNTLET");
        Banked(new Vector2(84, 130), true);
        float y = H * 0.56f, x0 = 140, step = (W - 280) / 7;
        for (int i = 0; i < 7; i++)
            AddChild(new Line2D { Points = new[] { new Vector2(x0 + i * step + 46, y), new Vector2(x0 + (i + 1) * step - 46, y) }, Width = i < Floor ? 4 : 2, DefaultColor = i < Floor ? GoldC : Mist * new Color(1, 1, 1, 0.35f) });
        for (int i = 0; i < 8; i++)
        {
            var c = new Vector2(x0 + i * step, y);
            bool done = i < Floor, now = i == Floor, light = OnLight(c + new Vector2(0, 80));
            var ink = light ? Ink : Silver; var gold = light ? GoldDeep : GoldLight;
            Face(this, c, now ? 96 : 80, Climb[i], now || done, done || now ? 1 : 0.6f);
            Fit(this, Tournament.Get(Climb[i]).Name, Title, 14, 10, now ? gold : ink, new Rect2(c.X - step / 2 + 6, y + 56, step - 12, 22));
            Fit(this, Lang.F("{0:N0}", Floors[i].Dp), Title, 18 + i * 3, 14, done ? ink * new Color(1, 1, 1, 0.45f) : gold, new Rect2(c.X - step / 2 + 6, y + 86, step - 12, 50));
            Mid(this, Floors[i].Cards > 0 ? Lang.F("DP  +{0} card{1}", Floors[i].Cards, Floors[i].Cards > 1 ? "s" : "") : "DP", Title, 11, ink * new Color(1, 1, 1, 0.65f), new Rect2(c.X - step / 2, y + 138, step, 18));
            if (done) Tag(this, "✓", c + new Vector2(34, -36), GoldC, Ink, 12);
            if (Floors[i].Kind != Prize.Dp)
            {
                var mc = c + new Vector2(0, -200);
                AddChild(new Line2D { Points = new[] { mc + new Vector2(0, 80), c + new Vector2(0, -52) }, Width = 1.5f, DefaultColor = gold with { A = 0.5f } });
                PrizeArt(this, mc, Floors[i].Kind == Prize.Mat ? 90 : 130, FloorAch(i));
            }
            if (now) Btn(this, "DUEL", new Vector2(c.X, y + 200), () => Flash("DUEL"), primary: true, size: 18, width: 160);
        }
    }
}
