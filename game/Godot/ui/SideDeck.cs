using Godot;
using PoC.Engine;

namespace PoC.Game.UI;

/// Side-decking between the duels of a match: card details (left), the main deck (middle), the Side Deck and
/// the Fusion Deck (right), all cards one size. Pick a card on one side and the cards it can trade with light
/// up on the other; picking one of them swaps the two (one-for-one, Fusion only with Fusion). Swaps change the
/// match's copy of the deck (Session.MatchDeck), never the saved deck. RESET undoes them.
/// Screenshot args: `-- --demo [--state=picked|swapped] [--rival=N] [--score=1-0]`.
public partial class SideDeck : MenuBase
{
    float W, H, barY;
    Deck deck = null!, start = null!;
    (CardData Card, bool Side)? pick;
    readonly List<(CardTile Tile, CardData Card, bool Side)> tiles = new();
    Control mainHost = null!, fusionHost = null!, sideHost = null!;
    Vector2 tile;
    int mainCols = 10;
    const int SideCols = 3;
    CardDetail detail = null!;
    static readonly Color SideC = new(0.45f, 0.8f, 0.85f), FusionC = new(0.75f, 0.55f, 0.95f);
    protected override string? MusicPath => Md("menu_02");
    protected override int ItemCount => 1;
    protected override void OnFocus(int old, int now) { }
    protected override void OnChoose(int i) { }
    protected override void OnBack() { if (pick != null) Pick(pick.Value.Card, pick.Value.Side); }
    static CardDb Db => CardDb.Shared;

    protected override void Build()
    {
        DemoMatch();
        deck = Session.MatchDeck ??= Session.Playing!.Copy();
        start = deck.Copy();
        W = GetViewportRect().Size.X; H = GetViewportRect().Size.Y;
        var rival = RivalPanels.All[Session.Selected];
        AddChild(new ColorRect
        {
            AnchorRight = 1, AnchorBottom = 1, MouseFilter = MouseFilterEnum.Ignore,
            Material = Mat(@"shader_type canvas_item;
uniform vec4 tint : source_color;
void fragment() {
  vec2 p = (UV - vec2(0.45, 0.4)) * vec2(1.78, 1.0);
  COLOR = vec4(mix(tint.rgb * 0.22, vec3(0.012, 0.012, 0.018), smoothstep(0.0, 1.1, length(p))), 1.0);
}", ("tint", rival.Color)),
        });
        Bar();
        Layout();
        Render();
        var first = Db.Get(deck.Side.Count > 0 ? deck.Side[0] : deck.Main[0]);
        detail.Show(first, Session.Profile.Count(first.Idx));

        var st = Demo.Arg("state");
        if (st is "picked" or "swapped" && deck.Side.Count > 0) GetTree().CreateTimer(0.6).Timeout += () =>
        {
            var s = Db.Get(deck.Side.First(i => !Db.Get(i).IsFusion));
            Pick(s, true);
            if (st == "swapped") GetTree().CreateTimer(0.3).Timeout += () => Pick(Db.Get(deck.Main.First(i => Db.Get(i).Kind == s.Kind)), false);
        };
    }

    /// Screenshots: a match at 1–0 against Kaiba with a dozen Side Deck cards.
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

    void Bar()
    {
        const float h = 120;
        barY = H - h;
        AddChild(new ColorRect { Position = new Vector2(0, barY), Size = new Vector2(W, h), Material = Mat(RivalPanels.Bar), MouseFilter = MouseFilterEnum.Ignore });
        Kit.Put(this, "SIDE DECK", Kit.Cond, 50, Colors.White, new Vector2(64, barY + 29));
        AddChild(new ColorRect { Position = new Vector2(68, barY + 91), Size = new Vector2(110, 3), Color = RivalPanels.Gold });
        Score(new Vector2(W / 2 - 120, barY + h / 2));
        AddChild(Chips.Make("RESET", new Vector2(W - 640, barY + 28), new Vector2(200, 64), Reset));
        AddChild(Chips.Make($"DUEL {Session.MatchWins + Session.MatchLosses + 1}", new Vector2(W - 410, barY + 17), new Vector2(330, 86), Next, 32));
    }

    /// NAME ▰▱ 1 – 0 ▱▱ RIVAL (best of 3).
    void Score(Vector2 centre)
    {
        var rival = RivalPanels.All[Session.Selected];
        var root = new Control { Position = centre, MouseFilter = MouseFilterEnum.Ignore };
        AddChild(root);
        Kit.Put(root, $"{Session.MatchWins} – {Session.MatchLosses}", Kit.Impact, 56, Colors.White, new Vector2(-110, -40), new Vector2(220, 70), HorizontalAlignment.Center, 6);
        Kit.Put(root, Settings.PlayerName.ToUpperInvariant(), Kit.Cond, 24, RivalPanels.Gold, new Vector2(-420, -18), new Vector2(220, 30), HorizontalAlignment.Right);
        Kit.Put(root, rival.Name, Kit.Cond, 24, rival.Color.Lightened(0.35f), new Vector2(200, -18), new Vector2(220, 30));
        for (int i = 0; i < 2; i++)
        {
            root.AddChild(Pip(-180 + i * 30, -14, i < Session.MatchWins ? RivalPanels.Gold : new Color(1, 1, 1, 0.14f)));
            root.AddChild(Pip(124 + i * 30, -14, i < Session.MatchLosses ? rival.Color.Lightened(0.3f) : new Color(1, 1, 1, 0.14f)));
        }
    }

    static Polygon2D Pip(float x, float y, Color c) => new()
    {
        Polygon = new[] { new Vector2(x + 5, y), new Vector2(x + 26, y), new Vector2(x + 21, y + 16), new Vector2(x, y + 16) }, Color = c,
    };

    /// One card size for every box: the largest that fits the main deck's width and the right column's height
    /// (Side Deck: 15 slots in 3 columns; Fusion below it).
    void Layout()
    {
        const float m = 30, gap = 8, pad = 16, head = 46, dw = 320;
        float top = m, availH = barY - m - 20;
        int mainRows = (Math.Max(deck.Main.Count, DeckRules.MinMain) + mainCols - 1) / mainCols;
        int sideRows = (DeckRules.MaxSide + SideCols - 1) / SideCols;
        int fusionRows = Math.Max(1, (deck.Extra.Count + SideCols - 1) / SideCols);
        float boxX = m + dw + 20, availW = W - boxX - m - 16;
        // Width: 10 main columns + 3 side columns, each box with padding.
        float byW = (availW - 4 * pad - (mainCols - 1 + SideCols - 1) * gap) / (mainCols + SideCols);
        float byH = Math.Min((availH - 2 * (head + pad) - 12 - (sideRows + fusionRows) * gap) / (sideRows + fusionRows),
                             (availH - (head + pad) - mainRows * gap) / mainRows) / 1.458f;
        float tw = Math.Min(byW, byH);
        tile = new Vector2(tw, tw * 1.458f);

        detail = new CardDetail(dw, availH) { Position = new Vector2(m, top) };
        AddChild(detail);

        float mainW = pad * 2 + mainCols * tw + (mainCols - 1) * gap, sideW = pad * 2 + SideCols * tw + (SideCols - 1) * gap;
        float x = boxX + (availW - mainW - sideW) / 2;
        var main = Box(new Rect2(x, top, mainW, availH), "MAIN DECK", RivalPanels.Gold);
        mainHost = Host(main, pad, head);
        float sideH = head + sideRows * (tile.Y + gap) + pad - gap;
        var side = Box(new Rect2(x + mainW + 16, top, sideW, sideH), "SIDE DECK", SideC);
        sideHost = Host(side, pad, head);
        var fusion = Box(new Rect2(x + mainW + 16, top + sideH + 12, sideW, availH - sideH - 12), "FUSION DECK", FusionC);
        fusionHost = Host(fusion, pad, head);
    }

    Panel Box(Rect2 r, string label, Color c)
    {
        var b = Kit.Box(this, r, new StyleBoxFlat
        {
            BgColor = new Color(0.02f, 0.02f, 0.03f, 0.55f), BorderColor = c * new Color(1, 1, 1, 0.4f),
            BorderWidthTop = 2, BorderWidthBottom = 1, BorderWidthLeft = 1, BorderWidthRight = 1,
            CornerRadiusTopLeft = 16, CornerRadiusTopRight = 16, CornerRadiusBottomLeft = 16, CornerRadiusBottomRight = 16, AntiAliasing = true,
        });
        Kit.Put(b, label, Kit.Cond, 24, c, new Vector2(16, 10));
        return b;
    }

    static Control Host(Control box, float pad, float head)
    {
        var h = new Control { Position = new Vector2(pad, head), MouseFilter = MouseFilterEnum.Ignore };
        box.AddChild(h);
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
        Lay(fusionHost, Sorted(deck.Extra), SideCols, false, cameIn);
        Lay(sideHost, Sorted(deck.Side), SideCols, true, new());
        for (int i = deck.Side.Count; i < DeckRules.MaxSide; i++) Ghost(sideHost, i);
    }

    void Lay(Control host, List<CardData> cards, int cols, bool side, HashSet<int> glow)
    {
        const float gap = 8;
        for (int i = 0; i < cards.Count; i++)
        {
            var d = cards[i];
            var t = new CardTile(d, tile) { Owned = 1, ShowCount = false };
            var pos = new Vector2(i % cols * (tile.X + gap), i / cols * (tile.Y + gap));
            t.Position = pos; t.SetMeta("y", pos.Y);
            t.MouseEntered += () => { detail.Show(d, Session.Profile.Count(d.Idx)); t.CreateTween().TweenProperty(t, "scale", new Vector2(1.07f, 1.07f), 0.12); t.ZIndex = 2; };
            t.MouseExited += () => { t.CreateTween().TweenProperty(t, "scale", Vector2.One, 0.12); t.ZIndex = 0; };
            t.GuiInput += e => { if (e is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left }) Pick(d, side); };
            if (glow.Contains(d.Idx)) t.AddChild(Outline(t.Size, SideC, 2, 10));
            host.AddChild(t);
            tiles.Add((t, d, side));
        }
    }

    void Ghost(Control host, int i)
    {
        const float gap = 8;
        var g = new Panel { Position = new Vector2(i % SideCols * (tile.X + gap), i / SideCols * (tile.Y + gap)), Size = tile, MouseFilter = MouseFilterEnum.Ignore };
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
            if (on) { var r = Outline(t.Size, RivalPanels.Gold, 3, 14); r.Name = "Pick"; t.AddChild(r); }
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
