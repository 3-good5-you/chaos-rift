using Godot;
using PoC.Engine;

namespace PoC.Game.UI;

/// Side-decking between the duels of a match, in the Chaos Rift style like the Deck Builder: the card preview
/// (left), the Main Deck and Fusion Deck in the light (middle), the score and the Side Deck in the dark (right).
/// Point at a card to see it. Pick a card on one side and the cards it can trade with stay bright on the other;
/// picking one of them swaps the two (one-for-one, Fusion only with Fusion). Swaps change the match's copy of the
/// deck (Session.MatchDeck), never the saved deck. RESET undoes them.
/// Screenshot args: `-- --demo [--state=picked|swapped] [--rival=N] [--score=1-0]`.
public partial class SideDeck : RiftMenu
{
    Deck deck = null!, start = null!;
    (CardData Card, bool Side)? pick;
    readonly List<(CardTile Tile, CardData Card, bool Side)> tiles = new();
    Control mainHost = null!, fusionHost = null!, sideHost = null!, preview = null!;
    Vector2 tile;
    int mainCols = 10, sideCols = 5;
    const float PreviewW = 360, Gap = 8;
    static readonly Color SideC = Teal;
    protected override int ItemCount => 1;
    protected override void OnBack() { if (pick != null) Pick(pick.Value.Card, pick.Value.Side); }
    static CardDb Db => CardDb.Shared;

    protected override void Build()
    {
        DemoMatch();
        deck = Session.MatchDeck ??= (Session.ModeDeck ?? Session.Playing)!.Copy();
        start = deck.Copy();
        Rift(0.668f, 0.653f);
        Put(this, "SIDE DECK", Title, 40, Ink, new Vector2(60, 60));
        Hair(this, new Vector2(62, 114), 160, GoldC);
        preview = new Control { Position = new Vector2(60, 140), Size = new Vector2(PreviewW, 10), MouseFilter = MouseFilterEnum.Ignore };
        AddChild(preview);
        Layout();
        Render();
        Show(Db.Get(deck.Side.Count > 0 ? deck.Side[0] : deck.Main[0]));

        var st = Demo.Arg("state");
        if (st is "picked" or "swapped" && deck.Side.Count > 0) GetTree().CreateTimer(0.6).Timeout += () =>
        {
            var s = Db.Get(deck.Side.First(i => !Db.Get(i).IsFusion));
            Pick(s, true);
            if (st == "swapped") GetTree().CreateTimer(0.3).Timeout += () => Pick(Db.Get(deck.Main.First(i => Db.Get(i).Kind == s.Kind)), false);
        };
    }

    CardData? shown;
    void Show(CardData d) { if (shown == d) return; shown = d; Preview(preview, d, PreviewW); }

    /// Screenshots: a match at 1-0 against Kaiba with a dozen Side Deck cards.
    static void DemoMatch()
    {
        if (!Demo.On) return;
        Demo.Ensure();
        Session.Selected = int.TryParse(Demo.Arg("rival", "1"), out var r) ? r : 1;
        var sc = Demo.Arg("score", "1-0").Split('-');
        Session.Match = true; Session.MatchWins = int.Parse(sc[0]); Session.MatchLosses = int.Parse(sc[1]);
        var m = Session.Playing!.Copy();
        foreach (var c in Db.ByIdx.Values.Where(c => Session.Profile.Count(c.Idx) > 0 && (c.IsTrap || c.IsSpell)).OrderBy(c => c.Name))
            if (m.Side.Count < 12) DeckRules.Add(m, c, Session.Profile, side: true);
        Session.MatchDeck = m;
    }

    // ---------------------------------------------------------------- layout

    /// One card size for all three zones: the largest that fits the middle's width (10 Main Deck columns) and its
    /// height (Main Deck rows + one Fusion row or more).
    void Layout()
    {
        float dx = W * 0.68f, dw = W - dx - 40, bx = 60 + PreviewW + 36, bw = dx - 70 - bx, top = 146;
        int mainRows = (Math.Max(deck.Main.Count, DeckRules.MinMain) + mainCols - 1) / mainCols;
        int fusionRows = Math.Max(1, (deck.Extra.Count + mainCols - 1) / mainCols);
        float byW = (bw - (mainCols - 1) * Gap) / mainCols;
        float byH = ((H - 30 - top - 2 * 46 - 18) / (mainRows + fusionRows) - Gap) / 1.458f;
        float tw = Math.Min(byW, byH);
        tile = new Vector2(tw, tw * 1.458f);
        sideCols = Math.Max(3, (int)((dw + Gap) / (tw + Gap)));

        // the middle (light): Main Deck, then Fusion Deck
        float y = top;
        mainHost = Zone("MAIN DECK", $"{deck.Main.Count}", Ink, bx, ref y, bw, mainRows);
        fusionHost = Zone("FUSION DECK", $"{deck.Extra.Count}", new Color(0.42f, 0.25f, 0.62f), bx, ref y, bw, fusionRows);

        // the right (dark): the match so far, the Side Deck, RESET and the next duel
        var rival = Session.Rival;
        Put(this, $"{Settings.PlayerName.ToUpperInvariant()}   {Session.MatchWins} – {Session.MatchLosses}   {rival.Name.ToUpperInvariant()}", Title, 26, Silver, new Vector2(dx, 138), new Vector2(dw, 36)).ClipText = true;
        Put(this, "BEST OF 3", Thin, 14, Mist, new Vector2(dx, 178));
        Hair(this, new Vector2(dx, 238), dw, Silver * new Color(1, 1, 1, 0.12f));
        // the way on, big, at the bottom of the dark side (at the top right it read as a label: playtest 2026-11-12)
        Hair(this, new Vector2(dx, H - 150), dw, Silver * new Color(1, 1, 1, 0.12f));
        Button(this, "UNDO SWAPS", new Vector2(dx + dw * 0.2f, H - 84), Reset, 20);
        Button(this, $"START DUEL {Session.MatchWins + Session.MatchLosses + 1}  ›", new Vector2(dx + dw * 0.68f, H - 84), Next, 30);
        y = 256;
        sideHost = Zone("SIDE DECK", $"{deck.Side.Count} / {DeckRules.MaxSide}", Teal, dx, ref y, dw, (DeckRules.MaxSide + sideCols - 1) / sideCols);
    }

    /// A labelled zone (pip, name, count, hairline); returns the host its cards go in and moves y past it.
    Control Zone(string name, string count, Color tint, float x, ref float y, float w, int rows)
    {
        Pip(this, new Vector2(x + 6, y + 12), 9);
        Put(this, name, Title, 20, tint, new Vector2(x + 22, y));
        Put(this, count, Title, 16, tint * new Color(1, 1, 1, 0.75f), new Vector2(x + 22 + Title.GetStringSize(name, HorizontalAlignment.Left, -1, Z(20)).X + 24, y + 3));
        Hair(this, new Vector2(x, y + 34), w, tint * new Color(1, 1, 1, 0.35f));
        var h = new Control { Position = new Vector2(x, y + 46), MouseFilter = MouseFilterEnum.Ignore };
        AddChild(h);
        y += 46 + rows * (tile.Y + Gap) + 18;
        return h;
    }

    // ---------------------------------------------------------------- cards

    static List<CardData> Sorted(IEnumerable<int> ids) => ids.Select(Db.Get).OrderBy(c => c.Kind).ThenByDescending(c => c.Level).ThenBy(c => c.Name).ToList();

    /// Lay out all three boxes; cards brought in from the Side Deck glow teal.
    void Render()
    {
        foreach (var t in tiles) t.Tile.QueueFree();
        tiles.Clear();
        var before = start.Main.Concat(start.Extra).ToList();
        var cameIn = deck.Main.Concat(deck.Extra).GroupBy(i => i).Where(g => g.Count() > before.Count(i => i == g.Key)).Select(g => g.Key).ToHashSet();
        Lay(mainHost, Sorted(deck.Main), mainCols, false, cameIn);
        Lay(fusionHost, Sorted(deck.Extra), mainCols, false, cameIn);
        Lay(sideHost, Sorted(deck.Side), sideCols, true, new());
        for (int i = deck.Side.Count; i < DeckRules.MaxSide; i++) Ghost(sideHost, i);
    }

    void Lay(Control host, List<CardData> cards, int cols, bool side, HashSet<int> glow)
    {
        const float gap = Gap;
        for (int i = 0; i < cards.Count; i++)
        {
            var d = cards[i];
            var t = new CardTile(d, tile) { Owned = 1, ShowCount = false };
            var pos = new Vector2(i % cols * (tile.X + gap), i / cols * (tile.Y + gap));
            t.Position = pos; t.SetMeta("y", pos.Y);
            t.MouseEntered += () => { Show(d); t.CreateTween().TweenProperty(t, "scale", new Vector2(1.07f, 1.07f), 0.12); t.ZIndex = 2; };
            t.MouseExited += () => { t.CreateTween().TweenProperty(t, "scale", Vector2.One, 0.12); t.ZIndex = 0; };
            t.GuiInput += e => { if (e is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left }) Pick(d, side); };
            if (glow.Contains(d.Idx)) t.AddChild(Outline(t.Size, SideC, 2, 10));
            host.AddChild(t);
            tiles.Add((t, d, side));
        }
    }

    void Ghost(Control host, int i)
    {
        const float gap = Gap;
        var g = new Panel { Position = new Vector2(i % sideCols * (tile.X + gap), i / sideCols * (tile.Y + gap)), Size = tile, MouseFilter = MouseFilterEnum.Ignore };
        g.AddThemeStyleboxOverride("panel", new StyleBoxFlat
        {
            BgColor = new Color(1, 1, 1, 0.025f), BorderColor = new Color(1, 1, 1, 0.07f), BorderWidthTop = 1, BorderWidthBottom = 1, BorderWidthLeft = 1, BorderWidthRight = 1,
            CornerRadiusTopLeft = 6, CornerRadiusTopRight = 6, CornerRadiusBottomLeft = 6, CornerRadiusBottomRight = 6, AntiAliasing = true,
        });
        host.AddChild(g);
    }

    static Panel Outline(Vector2 size, Color c, int width, int glow)
    {
        var r = new Panel { Name = "Ring", Position = new Vector2(-4, -4), Size = size + new Vector2(8, 8), MouseFilter = MouseFilterEnum.Ignore };
        r.AddThemeStyleboxOverride("panel", new StyleBoxFlat
        {
            DrawCenter = false, BorderColor = c, BorderWidthTop = width, BorderWidthBottom = width, BorderWidthLeft = width, BorderWidthRight = width,
            CornerRadiusTopLeft = 8, CornerRadiusTopRight = 8, CornerRadiusBottomLeft = 8, CornerRadiusBottomRight = 8, AntiAliasing = true,
            ShadowColor = c * new Color(1, 1, 1, 0.55f), ShadowSize = glow,
        });
        return r;
    }

    // ---------------------------------------------------------------- picking and swapping

    /// First pick: the card lifts with a gold ring, and on the other side only the cards it can trade with stay
    /// bright. Second pick on the other side swaps; picking it again drops it.
    void Pick(CardData d, bool side)
    {
        if (pick is { } p && p.Side != side)
        {
            var (outCard, inCard) = side ? (p.Card, d) : (d, p.Card);
            var a = tiles.FirstOrDefault(t => t.Card == outCard && !t.Side).Tile;
            var b = tiles.FirstOrDefault(t => t.Card == inCard && t.Side).Tile;
            if (!DeckRules.Swap(deck, outCard, inCard)) { Shake(side ? b : a); return; }
            pick = null;
            Fly(a, b);
            return;
        }
        pick = pick is { } q && q.Card == d && q.Side == side ? null : (d, side);
        CardTile? lifted = pick == null ? null : tiles.First(t => t.Card == d && t.Side == side).Tile;
        foreach (var (t, c, s) in tiles)
        {
            bool on = t == lifted;
            bool dim = pick is { } k && (s == k.Side ? !on : c.IsFusion != k.Card.IsFusion);
            t.GetNodeOrNull("Pick")?.QueueFree();
            if (on) { var r = Outline(t.Size, GoldC, 3, 14); r.Name = "Pick"; t.AddChild(r); }
            var tw = t.CreateTween().SetParallel();
            tw.TweenProperty(t, "position:y", t.GetMeta("y").AsSingle() - (on ? 20 : 0), 0.15).SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
            tw.TweenProperty(t, "modulate", dim ? new Color(0.3f, 0.3f, 0.34f) : Colors.White, 0.15);
        }
    }

    /// The two cards cross in an arc, then the boxes redraw.
    void Fly(CardTile? a, CardTile? b)
    {
        if (a == null || b == null) { Render(); return; }
        var layer = new Control { Size = new Vector2(W, H), ZIndex = 60, MouseFilter = MouseFilterEnum.Stop };
        AddChild(layer);
        foreach (var (from, to) in new[] { (a, b), (b, a) })
        {
            var r0 = from.GetGlobalRect(); var r1 = to.GetGlobalRect();
            var ghost = new TextureRect { Texture = CardArt.Small(from.Data), ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.Scale, Position = r0.Position, Size = r0.Size, Material = Kit.RoundedMat(r0.Size.X / r0.Size.Y), MouseFilter = MouseFilterEnum.Ignore };
            layer.AddChild(ghost);
            from.Modulate = new Color(1, 1, 1, 0);
            var mid = (r0.Position + r1.Position) / 2 + new Vector2(0, -140);
            ghost.CreateTween().TweenMethod(Callable.From<float>(t => ghost.Position = (1 - t) * (1 - t) * r0.Position + 2 * (1 - t) * t * mid + t * t * r1.Position), 0f, 1f, 0.45)
                .SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.InOut);
        }
        GetTree().CreateTimer(0.46).Timeout += () => { layer.QueueFree(); Render(); };
    }

    static void Shake(Control? c)
    {
        if (c == null) return;
        var x = c.Position.X; var tw = c.CreateTween();
        for (int i = 0; i < 3; i++) { tw.TweenProperty(c, "position:x", x + 6, 0.04); tw.TweenProperty(c, "position:x", x - 6, 0.04); }
        tw.TweenProperty(c, "position:x", x, 0.04);
    }

    void Reset()
    {
        deck.Main.Clear(); deck.Main.AddRange(start.Main);
        deck.Extra.Clear(); deck.Extra.AddRange(start.Extra);
        deck.Side.Clear(); deck.Side.AddRange(start.Side);
        pick = null;
        Render();
    }

    void Next() => Transition.Go(this, "res://ui/field/DuelField.tscn");
}
