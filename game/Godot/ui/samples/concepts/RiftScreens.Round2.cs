using Godot;
using PoC.Engine;
using static PoC.Game.UI.ConceptKit;

namespace PoC.Game.UI;

/// Round 2 (user picks 2026-11-08): Card List A with real filtering, Shop CARDS tab built to browse ~2,800 cards
/// (d = grid, e = dense list), Deck Builder A with clear Main / Fusion / Side zones, a deck switcher and deck actions.
/// States: `--state=filters` (filter panel open), `--state=decks` (deck list open).
public partial class RiftScreens
{
    static readonly string[] Attrs = { "DARK", "LIGHT", "EARTH", "WATER", "FIRE", "WIND" };
    static readonly string[] Frames = { "NORMAL", "EFFECT", "FUSION", "RITUAL" };
    static readonly string[] Races = { "DRAGON", "SPELLCASTER", "WARRIOR", "FIEND", "ZOMBIE", "BEAST", "MACHINE", "AQUA", "FAIRY", "INSECT" };
    static readonly string[] Sorts = { "NAME", "ATK", "DEF", "LEVEL", "NEWEST" };

    bool State(string s) => Demo.Arg("state") == s;

    /// A word that can be on (gold + diamond) or off (dim), drawn in plain ink for the side it sits on.
    float Word(string s, Vector2 at, int size, bool on, bool dark, Font? f = null)
    {
        f ??= Title;
        float w = f.GetStringSize(s, HorizontalAlignment.Left, -1, size).X;
        C(this, s, f, size, on ? (dark ? new Color(1f, 0.85f, 0.5f) : GoldDeep) : (dark ? Silver : Ink) * new Color(1, 1, 1, 0.5f), at);
        if (on) Pip(new Vector2(at.X + w / 2, at.Y + size * 1.55f), 7);
        return w;
    }

    /// The attribute orb (same drawing the old filters use), dimmed when off.
    void Orb(string attr, Vector2 centre, float r, bool on)
    {
        var o = new Control { Position = centre - new Vector2(r, r), Size = new Vector2(2 * r, 2 * r), MouseFilter = MouseFilterEnum.Ignore };
        o.Draw += () => Kit.Orb(o, o.Size / 2, r * 0.95f, attr, on ? 1f : 0.28f);
        AddChild(o);
    }

    /// The filter bar: ONE row (search · kind words · FILTERS (n) · sort). FILTERS opens a small panel with only
    /// attribute, level and ownership. Returns the y where the grid can start.
    float FilterBar(Vector2 at, float w, int results, bool open, bool shop = false)
    {
        bool dark = !OnLight(at);
        var ink = dark ? Silver : Ink; var dim = ink * new Color(1, 1, 1, 0.5f);
        var gold = dark ? new Color(1f, 0.85f, 0.5f) : GoldDeep;
        C(this, "⌕", Body, 24, dim, at + new Vector2(0, -3));
        C(this, open ? "dragon" : "SEARCH", open ? Body : Thin, open ? 20 : 17, open ? ink : dim, at + new Vector2(30, open ? 0 : 2));
        Hair(at + new Vector2(0, 32), 190, ink * new Color(1, 1, 1, 0.3f));
        float x = at.X + 230;
        foreach (var (k, i) in new[] { "ALL", "MONSTER", "SPELL", "TRAP" }.Select((k, i) => (k, i))) x += Word(k, new Vector2(x, at.Y + 2), 17, i == 0, dark) + 24;
        x += 6;
        C(this, open ? "FILTERS ▴" : "FILTERS ▾", Title, 16, gold, new Vector2(x, at.Y + 3));
        x += Title.GetStringSize("FILTERS ▾", HorizontalAlignment.Left, -1, 16).X + 26;
        C(this, (shop ? "NEWEST" : "NAME") + " ▾", Title, 16, dim, new Vector2(x, at.Y + 3));
        float y = at.Y + 52;
        if (!open)
        {
            C(this, $"{results:N0} CARDS", Thin, 15, dim, new Vector2(at.X, y - 6));
            return y + 36;
        }
        Hair(new Vector2(at.X, y), w, GoldC with { A = 0.45f });
        y += 20;
        for (int i = 0; i < Attrs.Length; i++) Orb(Attrs[i], new Vector2(at.X + 16 + i * 40, y + 12), 15, i == 0);
        float lx = at.X + 280;
        C(this, "LV", Thin, 15, dim, new Vector2(lx, y + 3));
        Hair(new Vector2(lx + 40, y + 12), 160, ink * new Color(1, 1, 1, 0.25f));
        Hair(new Vector2(lx + 40 + 160 * 4 / 11f, y + 11), 160 * 7 / 11f, GoldC, null, 2.5f);
        Pip(new Vector2(lx + 40 + 160 * 4 / 11f, y + 12), 12); Pip(new Vector2(lx + 200, y + 12), 12);
        C(this, "5–12", Title, 15, ink, new Vector2(lx + 214, y + 2));
        x = lx + 300;
        string[] own = shop ? new[] { "ALL", "NEW TO ME" } : new[] { "ALL", "MISSING" };
        for (int i = 0; i < own.Length; i++) x += Word(own[i], new Vector2(x, y + 1), 15, i == 0, dark) + 20;
        y += 48;
        Hair(new Vector2(at.X, y), w, GoldC with { A = 0.45f });
        C(this, $"{results:N0} CARDS", Thin, 15, dim, new Vector2(at.X, y + 8));
        return y + 40;
    }

    List<CardData> Monsters(int n) => OwnedCards().Where(c => c.IsMonster && c.Attribute == "DARK" && c.Race == "Dragon").Concat(OwnedCards().Where(c => c.IsMonster)).Distinct().Take(n).ToList();

    // ---------------------------------------------------------------- Card List A, with filters
    void CardsBinder2()
    {
        bool open = State("filters");
        Rift(0.62f, 0.56f);
        Logo(new Vector2(W - 270, 90), 360);
        Heading("CARD LIST", new Vector2(80, 40), 48);
        float gy = FilterBar(new Vector2(80, 150), W * 0.5f, open ? 37 : 267, open);
        var cards = Monsters(15);
        float tw = open ? 128 : 160, th = tw * 1.458f, gx = 22, gyy = 26, x0 = 80;
        int cols = open ? 7 : 5, rows = Math.Max(1, (int)((H - 90 - gy) / (th + gyy)));
        int sel = Math.Min(6, cards.Count - 1);
        for (int i = 0; i < Math.Min(cards.Count, cols * rows); i++)
        {
            var at = new Vector2(x0 + i % cols * (tw + gx), gy + i / cols * (th + gyy));
            var c = Card(cards[i], at + new Vector2(tw, th) / 2, th, 0, true, i == sel ? 1.4f : 0.5f);
            if (i == sel) { c.Scale = new Vector2(1.07f, 1.07f); Pip(new Vector2(at.X + tw / 2, at.Y + th + 14), 9); }
        }
        var d = cards[sel];
        Card(d, new Vector2(W * 0.81f, H * 0.4f), H * 0.46f, 0.04f);
        CardText(d, new Vector2(W * 0.66f, H * 0.68f), W * 0.3f, Silver, Mist);
        C(this, open ? "37 OF 267  ·  PAGE 1 / 3" : "267 CARDS  ·  PAGE 1 / 18", Thin, 18, Graphite, new Vector2(80, H - 50));
    }

    // ---------------------------------------------------------------- Shop CARDS: d = grid, e = list
    void ShopCardsGrid()
    {
        bool open = State("filters");
        Rift(0.62f, 0.56f);
        Logo(new Vector2(W - 270, 90), 360);
        ShopHeader(0, 80, 30);
        Dp(new Vector2(W - 380, 190), Silver);
        float gy = FilterBar(new Vector2(80, 196), W * 0.5f, open ? 212 : 2794, open, shop: true);
        var db = CardDb.Shared;
        var cards = db.ByIdx.Values.Where(c => c.IsMonster && db.Canon(c.Idx) == c.Idx && c.Atk >= 1800).GroupBy(c => c.Name).Select(g => g.First()).Take(24).ToList();
        float tw = 128, th = tw * 1.458f, gx = 22, gyy = 50;
        int cols = 7, rows = Math.Max(1, (int)((H - 80 - gy) / (th + gyy)));
        int sel = 3;
        for (int i = 0; i < Math.Min(cards.Count, cols * rows); i++)
        {
            var at = new Vector2(80 + i % cols * (tw + gx), gy + i / cols * (th + gyy));
            var c = Card(cards[i], at + new Vector2(tw, th) / 2, th, 0, true, i == sel ? 1.4f : 0.5f);
            int own = Session.Profile.Count(cards[i].Idx);
            if (i == sel) c.Scale = new Vector2(1.07f, 1.07f);
            C(this, $"{(global::PoC.Engine.Shop.Price(cards[i])):N0}", Thin, 16, i == sel ? GoldDeep : Graphite, new Vector2(at.X, at.Y + th + 8), new Vector2(tw, 22), HorizontalAlignment.Center);
            if (own == 0) C(this, "NEW", Title, 13, GoldDeep, new Vector2(at.X + 4, at.Y - 20));
            else for (int k = 0; k < Math.Min(3, own); k++) Pip(new Vector2(at.X + 10 + k * 13, at.Y - 10), 6);
        }
        var d = cards[sel];
        Card(d, new Vector2(W * 0.81f, H * 0.42f), H * 0.46f, 0.04f);
        C(this, d.Name.ToUpperInvariant(), Title, 20, Silver, new Vector2(W * 0.64f, H * 0.7f + 8), new Vector2(W * 0.34f, 30), HorizontalAlignment.Center);
        C(this, $"{(global::PoC.Engine.Shop.Price(d)):N0} DP EACH   ·   OWNED ×{Session.Profile.Count(d.Idx)}", Thin, 22, new Color(1f, 0.85f, 0.5f), new Vector2(W * 0.64f, H * 0.7f + 52), new Vector2(W * 0.34f, 30), HorizontalAlignment.Center);
        focusables.Add(Button("BUY ×1", new Vector2(W * 0.74f, H * 0.82f), 24));
        focusables.Add(Button("BUY ×3", new Vector2(W * 0.88f, H * 0.82f), 24));
        C(this, open ? "212 FOR SALE  ·  PAGE 1 / 9" : "2,794 FOR SALE  ·  PAGE 1 / 117", Thin, 18, Graphite, new Vector2(80, H - 44));
    }

    void ShopCardsList()
    {
        Rift(0.6f, 0.54f);
        Logo(new Vector2(W - 270, 90), 360);
        ShopHeader(0, 80, 30);
        Dp(new Vector2(W - 380, 190), Silver);
        float gy = FilterBar(new Vector2(80, 196), W * 0.5f, 2794, false, shop: true);
        // column headers
        float[] cx = { 80, 140, 520, 680, 760, 840 };
        string[] head = { "", "NAME", "TYPE", "ATK", "DEF", "PRICE" };
        for (int i = 0; i < head.Length; i++) C(this, head[i], Thin, 14, Graphite * new Color(1, 1, 1, 0.6f), new Vector2(cx[i], gy - 6));
        var db = CardDb.Shared;
        var cards = db.ByIdx.Values.Where(c => db.Canon(c.Idx) == c.Idx && c.Name.Length > 0).GroupBy(c => c.Name).Select(g => g.First()).OrderBy(c => c.Name).Skip(400).Take(13).ToList();
        float y = gy + 24; int sel = 4;
        for (int i = 0; i < cards.Count; i++)
        {
            var c = cards[i]; bool on = i == sel;
            if (on) Shadow(this, new Vector2(70, y - 6), new Vector2(cx[5] + 100 - 70, 56), 0.14f, new Color(0.6f, 0.45f, 0.15f));
            var art = Ink_(CardArt.Picture(c), new Vector2(cx[0], y), new Vector2(44, 44)); art.Material = Kit.RoundedMat(1, 0.12f);
            C(this, c.Name, on ? Title : Body, on ? 17 : 20, on ? GoldDeep : Ink, new Vector2(cx[1], y + 10), new Vector2(370, 28)).ClipText = true;
            C(this, c.IsMonster ? $"{c.Race}".ToUpperInvariant() : $"{c.Frame} {c.Kind}".ToUpperInvariant(), Thin, 14, Graphite, new Vector2(cx[2], y + 14), new Vector2(140, 22)).ClipText = true;
            if (c.IsMonster) { C(this, $"{c.Atk}", Thin, 18, Ink, new Vector2(cx[3], y + 11)); C(this, $"{c.Def}", Thin, 18, Ink, new Vector2(cx[4], y + 11)); }
            C(this, $"{(global::PoC.Engine.Shop.Price(c)):N0}", Title, 18, on ? GoldDeep : Ink, new Vector2(cx[5], y + 10));
            int own = Session.Profile.Count(c.Idx);
            if (own == 0) C(this, "NEW", Title, 12, GoldDeep, new Vector2(cx[5] + 76, y + 14)); else for (int k = 0; k < Math.Min(3, own); k++) Pip(new Vector2(cx[5] + 82 + k * 13, y + 22), 6);
            y += 58;
        }
        var d = cards[sel];
        Card(d, new Vector2(W * 0.81f, H * 0.42f), H * 0.46f, 0.04f);
        C(this, $"{(global::PoC.Engine.Shop.Price(d)):N0} DP EACH   ·   OWNED ×{Session.Profile.Count(d.Idx)}", Thin, 22, new Color(1f, 0.85f, 0.5f), new Vector2(W * 0.64f, H * 0.7f + 10), new Vector2(W * 0.34f, 30), HorizontalAlignment.Center);
        focusables.Add(Button("BUY ×1", new Vector2(W * 0.74f, H * 0.8f), 24));
        focusables.Add(Button("BUY ×3", new Vector2(W * 0.88f, H * 0.8f), 24));
        C(this, "2,794 FOR SALE  ·  ROWS 401–414", Thin, 18, Graphite, new Vector2(80, H - 44));
    }

    // ---------------------------------------------------------------- Deck Builder A: Main, Side, Fusion (like the original)
    void DeckAcross2()
    {
        Rift(0.45f, 0.39f);
        Logo(new Vector2(W - 200, 62), 250);
        C(this, "‹  DECKS", Title, 18, Graphite, new Vector2(62, 36));
        C(this, "DECK BUILDER", Title, 40, Ink, new Vector2(60, 64));
        Hair(new Vector2(62, 118), 160, GoldC);
        float gy = FilterBar(new Vector2(60, 150), W * 0.36f, 486, false);
        var cards = Monsters(16);
        float tw = 118, th = tw * 1.458f, gx = 18, gyy = 22;
        for (int i = 0; i < cards.Count; i++)
        {
            var at = new Vector2(60 + i % 4 * (tw + gx), gy + i / 4 * (th + gyy));
            if (at.Y + th > H - 30) break;
            Card(cards[i], at + new Vector2(tw, th) / 2, th, 0, true, 0.5f);
            int inDeck = TheDeck.Copies(cards[i].Idx);
            for (int k = 0; k < inDeck; k++) Pip(new Vector2(at.X + tw / 2 - (inDeck - 1) * 8 + k * 16, at.Y + th + 8), 7);
        }

        float dx = W * 0.47f, dw = W - dx - 40;
        C(this, TheDeck.Name.ToUpperInvariant(), Title, 40, Silver, new Vector2(dx, 140));
        C(this, "2003 ✓   2004 ✓", Thin, 16, Mist, new Vector2(dx + 4, 194));
        Hair(new Vector2(dx, 226), dw, Silver * new Color(1, 1, 1, 0.12f));
        float mw = 60, mh = mw * 1.458f, mg = 6;
        int cols = (int)((dw + mg) / (mw + mg));
        void Zone(string name, string count, float y, Color tint)
        {
            Pip(new Vector2(dx + 6, y + 12), 9);
            C(this, name, Title, 20, tint, new Vector2(dx + 22, y));
            C(this, count, Thin, 18, Mist, new Vector2(dx + 22 + Title.GetStringSize(name, HorizontalAlignment.Left, -1, 20).X + 20, y + 2));
            Hair(new Vector2(dx, y + 34), dw, tint * new Color(1, 1, 1, 0.35f));
        }
        void Slots(int n, float y, Color tint)
        {
            for (int i = 0; i < n; i++)
                AddChild(new ColorRect { Position = new Vector2(dx + i % cols * (mw + mg), y + i / cols * (mh + mg)), Size = new Vector2(mw, mh), Color = tint * new Color(1, 1, 1, 0.06f), MouseFilter = MouseFilterEnum.Ignore, Material = Kit.RoundedMat(mw / mh, 0.08f) });
        }
        var main = Sorted(TheDeck.Main).ToList();
        Zone("MAIN DECK", $"{main.Count} / 40–80     MONSTER {main.Count(c => c.IsMonster)}   SPELL {main.Count(c => c.IsSpell)}   TRAP {main.Count(c => c.IsTrap)}", 246, Silver);
        float my = 292;
        for (int i = 0; i < main.Count; i++) Ink_(CardArt.Small(main[i]), new Vector2(dx + i % cols * (mw + mg), my + i / cols * (mh + mg)), new Vector2(mw, mh));
        float sy = my + ((main.Count + cols - 1) / cols) * (mh + mg) + 22;
        var teal = new Color(0.55f, 0.85f, 0.9f);
        Zone("SIDE DECK", $"{TheDeck.Side.Count} / 15", sy, teal);
        Slots(15, sy + 46, teal);
        float fy = sy + 46 + mh + 26;
        var purple = new Color(0.8f, 0.6f, 1f);
        var ex = Sorted(TheDeck.Extra).ToList();
        Zone("FUSION DECK", $"{ex.Count}", fy, purple);
        for (int i = 0; i < ex.Count; i++) Ink_(CardArt.Small(ex[i]), new Vector2(dx + i % cols * (mw + mg), fy + 46 + i / cols * (mh + mg)), new Vector2(mw, mh));
        focusables.Add(Button("SAVE", new Vector2(W - 150, H - 46), 24));
    }

    // ---------------------------------------------------------------- Deck select: between the main menu and the builder
    /// a = deck cases on the light side, the chosen deck opened up on the dark side; b = a shelf of decks across the rift.
    void DeckSelect()
    {
        if (variant == "b") { DeckShelf(); return; }
        Rift(0.56f, 0.5f);
        Logo(new Vector2(W - 230, 70), 300);
        Heading("YOUR DECKS", new Vector2(80, 50));
        var decks = DemoDecks();
        float cw = 160, ch = cw * 1.458f, gx = 100, gyy = 100, x0 = 110, y0 = 240;
        int sel = 0;
        for (int i = 0; i < decks.Count + 1; i++)
        {
            var at = new Vector2(x0 + i % 3 * (cw + gx), y0 + i / 3 * (ch + gyy));
            if (i == decks.Count)
            {
                AddChild(new ColorRect { Position = at, Size = new Vector2(cw, ch), Color = Ink * new Color(1, 1, 1, 0.05f), Material = Kit.RoundedMat(cw / ch, 0.06f), MouseFilter = MouseFilterEnum.Ignore });
                C(this, "+", Title, 60, Graphite, at + new Vector2(0, ch / 2 - 46), new Vector2(cw, 70), HorizontalAlignment.Center);
                C(this, "NEW DECK", Title, 16, Graphite, at + new Vector2(0, ch + 18), new Vector2(cw, 24), HorizontalAlignment.Center);
                break;
            }
            DeckCase(decks[i].Cover, at, cw, i == sel);
            C(this, decks[i].Name.ToUpperInvariant(), Title, 15, i == sel ? GoldDeep : Ink, at + new Vector2(-30, ch + 18), new Vector2(cw + 60, 44), HorizontalAlignment.Center).AutowrapMode = TextServer.AutowrapMode.WordSmart;
            if (i == sel) Pip(new Vector2(at.X + cw / 2, at.Y + ch + 66), 8);
        }
        C(this, "IMPORT DECK FILE", Title, 18, GoldDeep, new Vector2(x0, H - 92));
        C(this, ".ydc (original game)  ·  .ydk (YGOPro / EDOPro)", Body, 16, Graphite, new Vector2(x0, H - 58));

        // the chosen deck, opened on the dark side
        var d = decks[sel];
        float rx = W * 0.57f;
        Card(d.Cover, new Vector2(rx + 150, H * 0.47f), 400, 0.04f);
        C(this, d.Name.ToUpperInvariant(), Title, 40, Silver, new Vector2(rx + 340, H * 0.27f), new Vector2(W - rx - 380, 50));
        C(this, "IN USE", Title, 16, new Color(1f, 0.85f, 0.5f), new Vector2(rx + 342, H * 0.27f + 58));
        float iy = H * 0.27f + 110;
        foreach (var (k, v) in new[] { ("MAIN", $"{d.Main}"), ("SIDE", $"{d.Side}"), ("FUSION", $"{d.Fusion}"), ("MONSTER · SPELL · TRAP", d.Split), ("BAN LIST", "2003 ✓   2004 ✓") })
        {
            C(this, k, Thin, 15, Mist, new Vector2(rx + 342, iy)); C(this, v, Title, 20, Silver, new Vector2(rx + 342, iy + 22)); iy += 62;
        }
        focusables.Add(Button("EDIT DECK", new Vector2(rx + 150, H * 0.78f), 26));
        float ax = rx + 20;
        foreach (var a in new[] { "USE", "COPY", "RENAME", "EXPORT", "DELETE" }) { C(this, a, Title, 16, Silver * new Color(1, 1, 1, 0.65f), new Vector2(ax, H * 0.78f + 70)); ax += Title.GetStringSize(a, HorizontalAlignment.Left, -1, 16).X + 34; }
    }

    record DemoDeck(string Name, CardData Cover, int Main, int Side, int Fusion, string Split);
    List<DemoDeck> DemoDecks()
    {
        var real = Session.Decks.Take(3).Select(d => new DemoDeck(d.Name, CardDb.Shared.Get(Math.Max(0, d.CoverCard())), d.Main.Count, d.Side.Count, d.Extra.Count,
            $"{d.Main.Count(i => CardDb.Shared.Get(i).IsMonster)} · {d.Main.Count(i => CardDb.Shared.Get(i).IsSpell)} · {d.Main.Count(i => CardDb.Shared.Get(i).IsTrap)}")).ToList();
        real.Add(new DemoDeck("Exodia Test", Named("Exodia the Forbidden One"), 40, 0, 0, "22 · 12 · 6"));
        real.Add(new DemoDeck("Spellcaster Control", Named("Dark Magician"), 40, 6, 1, "18 · 14 · 8"));
        return real;
    }

    /// A deck case: the cover card standing on two fanned card backs.
    void DeckCase(CardData cover, Vector2 at, float w, bool sel)
    {
        float h = w * 1.458f;
        for (int k = 2; k >= 1; k--)
        {
            var b = Ink_(CardArt.Back, at + new Vector2(k * 9, -k * 6), new Vector2(w, h));
            b.PivotOffset = new Vector2(w, h) / 2; b.Rotation = k * 0.04f; b.Modulate = new Color(1, 1, 1, 0.9f);
        }
        var c = Card(cover, at + new Vector2(w, h) / 2, h, -0.02f, true, sel ? 1.3f : 0.6f);
        if (sel) c.Scale = new Vector2(1.06f, 1.06f);
    }

    void DeckShelf()
    {
        Rift(0.53f, 0.47f);
        Logo(new Vector2(W - 230, 70), 300);
        Heading("YOUR DECKS", new Vector2(80, 50));
        var decks = DemoDecks();
        int sel = 1;
        float cy = H * 0.48f, step = 330, x0 = W / 2 - (decks.Count - 1) * step / 2f;
        for (int i = 0; i < decks.Count; i++)
        {
            float w = i == sel ? 230 : 170, h = w * 1.458f;
            var at = new Vector2(x0 + i * step - w / 2, cy - h / 2);
            DeckCase(decks[i].Cover, at, w, i == sel);
            var c = OnLight(at + new Vector2(w / 2, h)) ? Ink : Silver;
            C(this, decks[i].Name.ToUpperInvariant(), Title, i == sel ? 22 : 16, i == sel ? (OnLight(at) ? GoldDeep : new Color(1f, 0.85f, 0.5f)) : c, at + new Vector2(-40, h + 22), new Vector2(w + 80, 30), HorizontalAlignment.Center);
            C(this, $"{decks[i].Main} · {decks[i].Side} · {decks[i].Fusion}", Thin, 15, c * new Color(1, 1, 1, 0.6f), at + new Vector2(-40, h + 56), new Vector2(w + 80, 22), HorizontalAlignment.Center);
        }
        Pip(new Vector2(x0 + sel * step, cy + 230 * 1.458f / 2 + 100), 9);
        focusables.Add(Button("EDIT", new Vector2(W / 2, H - 140), 28));
        float ax = W / 2 - 360;
        foreach (var a in new[] { "+ NEW", "COPY", "RENAME", "IMPORT", "EXPORT", "DELETE" })
        {
            var p = new Vector2(ax, H - 62);
            C(this, a, Title, 16, (OnLight(p) ? Ink : Silver) * new Color(1, 1, 1, 0.7f), p); ax += Title.GetStringSize(a, HorizontalAlignment.Left, -1, 16).X + 34;
        }
    }

}
