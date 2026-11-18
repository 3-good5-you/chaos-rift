using Godot;
using PoC.Engine;

namespace PoC.Game.UI;

/// Pack Duel (PLAN_PACK_DUEL.md): the draft against the AI (Engine/Draft.cs), then ONE DUEL or BEST OF 3 with the
/// drafted decks. The draft lives in Session.Draft, so REMATCH from Results comes back here to the finished decks.
/// Screenshot args (`draftlab.bat`): --a=yugi/x_lv3 --b=kaiba/x_lv3 --seed=N --auto=N.
public partial class PackDraft : RiftMenu
{
    Draft d = null!;
    readonly HashSet<int> sel = new();
    int kit = -1;
    static bool first = true;
    Control layer = null!, prev = null!;

    protected override string? MusicPath => Md("solo_gate");
    protected override void OnBack() { Session.ClearMode(); Transition.Go(this, "res://ui/DuelModes.tscn"); }

    protected override void Build()
    {
        Demo.Ensure();
        Session.Mode = GameMode.Pack;
        Rift(0.66f, 0.6f);
        Back("MODES");
        if (Session.Draft is { } kept) { d = kept; Redraw(false); }
        else NewDraft();
    }

    void NewDraft()
    {
        string seed = first ? Demo.Arg("seed") : "", a = first ? Demo.Arg("a") : "", b = first ? Demo.Arg("b") : "";
        int skip = first ? int.Parse(Demo.Arg("auto", "0")) : 0;   // --auto=N: both sides AUTO for N rounds (screenshots)
        first = false;
        var rng = new Rng(seed != "" ? ulong.Parse(seed) : Session.Rng.Next());
        d = Session.Draft = a != "" && b != "" ? new Draft(a, b, rng.Next()) : Draft.Random(rng);
        sel.Clear(); kit = -1;
        for (int r = 0; r < skip && !d.Done; r++)
        {
            if (d.Bonus) { d.PickKit(d.AutoKit(0), d.AutoKit(1)); continue; }
            d.Pick(d.Auto(0), d.Auto(1));
        }
        Redraw(true);
    }

    float DarkX => W * 0.7f;

    void Redraw(bool reveal)
    {
        if (layer != null) layer.QueueFree();
        layer = new Control { Size = new Vector2(W, H), MouseFilter = MouseFilterEnum.Ignore };
        AddChild(layer);
        prev = new Control { Position = new Vector2(DarkX, 70), MouseFilter = MouseFilterEnum.Ignore };
        layer.AddChild(prev);
        Put(layer, "PACK DRAFT", Title, 40, Ink, new Vector2(80, 58));
        Hair(layer, new Vector2(82, 108), 140, GoldC);
        if (d.Done) Final();
        else if (d.Bonus) BonusRound();
        else PackRound(reveal);
    }

    // ---------------------------------------------------------------- rounds 1-5

    void PackRound(bool reveal)
    {
        var pack = d.Pack(0);
        int need = Math.Min(Draft.Keep, pack.Count);
        Put(layer, Lang.F("SELECT {0} TO KEEP", need), Title, 22, Graphite, new Vector2(82, 196));   // short (user 2026-11-17)
        float h = 250, w = h * 0.686f, gx = 28, gy = 44, x0 = 90, y0 = 250;
        var back = UI.Cosmetics.Back(P.Back, 256);
        for (int i = 0; i < pack.Count; i++)
        {
            int at = i;
            var c = Db.Get(pack[i].Card);
            bool on = sel.Contains(i);
            var centre = new Vector2(x0 + i % 4 * (w + gx) + w / 2, y0 + i / 4 * (h + gy) + h / 2 - (on ? 8 : 0));
            var card = Card(layer, c, centre, h, 0, on ? 1.3f : 0.7f);
            if (!on && sel.Count == need) card.Modulate = new Color(1, 1, 1, 0.45f);
            if (on) Picked(centre, w, h);
            if (reveal)
            {
                var face = card.GetChildren().OfType<TextureRect>().Last();
                var real = face.Texture;
                face.Texture = back;
                card.Modulate = new Color(1, 1, 1, 0);
                var tw = CreateTween();
                tw.TweenProperty(card, "modulate:a", 1f, 0.12).SetDelay(0.15 + i * 0.12);
                tw.TweenProperty(card, "scale:x", 0f, 0.08);
                tw.TweenCallback(Callable.From(() => face.Texture = real));
                tw.TweenProperty(card, "scale:x", 1f, 0.08);
            }
            var hit = new Control { Position = centre - new Vector2(w, h) / 2, Size = new Vector2(w, h) };
            layer.AddChild(hit);
            Click(hit, () => Toggle(at, need), hv => { if (hv) Show(c); });
        }
        // the round on the prompt's line, right end of the cards (user 2026-11-17: off the dark side, no KEPT counts)
        Put(layer, Lang.F("ROUND {0} / {1}", d.Round + 1, Draft.Rounds), Title, 20, Graphite, new Vector2(x0, 198), new Vector2(4 * (w + gx) - gx, 30), HorizontalAlignment.Right);
        Button(layer, "AUTO", new Vector2(W - 470, H - 90), () => { sel.Clear(); sel.UnionWith(d.Auto(0)); Redraw(false); });
        Button(layer, "LOCK IN", new Vector2(W - 200, H - 90), () =>
        {
            if (sel.Count != need) { Flash(Lang.F("Keep {0} cards", need)); return; }
            d.Pick(sel.ToArray(), d.Auto(1));
            sel.Clear();
            Redraw(true);
        });
    }

    /// A picked card (user 2026-11-17: the small diamond wasn't clear, then the thick glowing frame too loud): a thin
    /// gold outline and a small gold check badge on its top right corner.
    void Picked(Vector2 centre, float w, float h)
    {
        var frame = new Panel { Position = centre - new Vector2(w, h) / 2 - new Vector2(4, 4), Size = new Vector2(w + 8, h + 8), MouseFilter = MouseFilterEnum.Ignore };
        frame.AddThemeStyleboxOverride("panel", Box(new Color(0, 0, 0, 0), GoldC, 2, 10, 6, GoldC with { A = 0.25f }));
        layer.AddChild(frame);
        var at = centre + new Vector2(w / 2 - 4, -h / 2 + 4);
        var badge = new Control { Position = at - new Vector2(13, 13), Size = new Vector2(26, 26), MouseFilter = MouseFilterEnum.Ignore };
        badge.Draw += () =>
        {
            badge.DrawCircle(new Vector2(13, 13), 12, GoldC);
            badge.DrawPolyline(new[] { new Vector2(7, 13), new Vector2(11, 17), new Vector2(19, 8) }, Ink, 2.5f, true);
        };
        layer.AddChild(badge);
    }

    void Toggle(int i, int need)
    {
        if (!sel.Remove(i))
        {
            if (sel.Count >= need) { Flash("Untick a card first"); return; }
            sel.Add(i);
        }
        UiSound(false);
        Redraw(false);
    }

    // ---------------------------------------------------------------- bonus pack

    void BonusRound()
    {
        Put(layer, "BONUS PACK   ·   PICK ONE", Title, 22, Graphite, new Vector2(82, 196));
        float h = 165, w = h * 0.686f, y0 = 240;
        for (int k = 0; k < d.Kits.Count; k++)
        {
            int at = k;
            bool on = kit == k;
            float y = y0 + k * (h + 30);
            if (on) Hair(layer, new Vector2(70, y), 4, GoldC, h);
            var adds = d.KitAdds(0, k, known: true);
            for (int j = 0; j < d.Kits[k].Count; j++)
            {
                var c = Db.Get(d.Kits[k][j]);
                var centre = new Vector2(100 + j * (w + 12) + w / 2, y + h / 2);
                var card = Card(layer, c, centre, h, 0, on ? 1.2f : 0.6f);
                if (kit >= 0 && !on) card.Modulate = new Color(1, 1, 1, 0.45f);
                if (!adds[j])
                {
                    card.Modulate = new Color(1, 1, 1, 0.25f);
                    Put(layer, "HAVE 2", Title, 16, GoldDeep, centre - new Vector2(w / 2, 12), new Vector2(w, 24), HorizontalAlignment.Center);
                }
                var hit = new Control { Position = centre - new Vector2(w, h) / 2, Size = new Vector2(w, h) };
                layer.AddChild(hit);
                Click(hit, () => { kit = at; UiSound(false); Redraw(false); }, hv => { if (hv) Show(c); });
            }
        }
        Button(layer, "AUTO", new Vector2(W - 470, H - 90), () => { kit = d.AutoKit(0); Redraw(false); });
        Button(layer, "LOCK IN", new Vector2(W - 200, H - 90), () =>
        {
            if (kit < 0) { Flash("Pick a kit"); return; }
            d.PickKit(kit, d.AutoKit(1));
            Redraw(false);
        });
    }

    // ---------------------------------------------------------------- the decks

    void Final()
    {
        var me = d.Deck(0, "PACK DECK");
        Put(layer, Lang.F("YOUR DECK   ·   {0} CARDS", me.Main.Count + me.Extra.Count), Title, 22, Graphite, new Vector2(82, 196));
        var shown = me.Main.Concat(me.Extra).ToList();
        foreach (var s in d.Got[0]) shown.Remove(s.Card);   // received cards stay face down until you draw them
        var cards = shown.Select(Db.Get)
            .OrderBy(c => c.IsFusion ? 3 : c.IsMonster ? 0 : c.IsSpell ? 1 : 2).ThenByDescending(c => c.Level).ThenBy(c => c.Name).ToList();
        float h = 118, w = h * 0.686f;
        var back = UI.Cosmetics.Back(P.Back, 256);
        for (int i = 0; i < cards.Count + d.Got[0].Count; i++)
        {
            var centre = new Vector2(90 + i % 12 * (w + 6) + w / 2, 240 + i / 12 * (h + 14) + h / 2);
            if (i >= cards.Count) { Pic(layer, back, centre - new Vector2(w, h) / 2, new Vector2(w, h)); continue; }
            var c = cards[i];
            Card(layer, c, centre, h, 0, 0.4f);
            var hit = new Control { Position = centre - new Vector2(w, h) / 2, Size = new Vector2(w, h) };
            layer.AddChild(hit);
            Click(hit, () => { }, hv => { if (hv) Show(c); });
        }
        Button(layer, "NEW DRAFT", new Vector2(260, H - 90), NewDraft);
        Button(layer, "ONE DUEL", new Vector2(W - 520, H - 90), () => Play(false));
        Button(layer, "BEST OF 3", new Vector2(W - 200, H - 90), () => Play(true));
    }

    void Play(bool match)
    {
        var held = d.Kept[1].Concat(d.Got[1]).ToList();
        int src = held.Count(s => s.Src == 0) >= held.Count(s => s.Src == 1) ? 0 : 1;
        var key = d.Source[src];
        var who = Tournament.Roster.FirstOrDefault(t => t.Deck == key)?.Key ?? key.Split('/')[0];
        int ri = Math.Max(0, Array.FindIndex(Session.Roster, r => r.File == who));
        int level = Draft.Tier(key) switch { "EASY" => 2, "NORMAL" => 4, "HARD" => 5, _ => 6 };
        Session.Mode = GameMode.Pack;
        Session.ModeDeck = d.Deck(0, "PACK DECK");
        Session.Match = match;
        Session.MatchWins = Session.MatchLosses = 0;
        Session.MatchDeck = match ? Session.ModeDeck.Copy() : null;
        Session.Picks = Session.Roster.Select(r => new OpponentPick(r.File, key, level)).ToArray();
        Session.Selected = ri;
        (Session.RivalDeck, Session.RivalSeen) = (d.Deck(1, "RIVAL DRAFT"), new CpuSide.Seen());
        Session.Save();
        Transition.Go(this, "res://ui/TurnOrder.tscn");
    }

    // ---------------------------------------------------------------- dark side: the card preview

    void Show(CardData c)
    {
        foreach (var n in prev.GetChildren()) n.QueueFree();
        // the card big in the dark column, its name, stats and text under it (user 2026-11-17: it was squeezed)
        float cw = W - DarkX - 60, h = 480, w = h * 0.686f, ty = h + 22;
        Card(prev, c, new Vector2(cw / 2, h / 2), h, 0, 1f, large: true);
        Put(prev, c.Name.ToUpperInvariant(), Title, 22, Silver, new Vector2(0, ty), new Vector2(cw, 30)).ClipText = true;
        if (c.IsMonster) { Put(prev, $"★{c.Level}   {Lang.Tr("ATK")} {c.AtkText}  {Lang.Tr("DEF")} {c.DefText}", BodyBold, 18, GoldLight, new Vector2(0, ty + 38)); ty += 34; }
        CardText(prev, c, new Vector2(0, ty + 42), new Vector2(cw, H - 160 - (70 + ty + 42)), 17, light: false, min: 13);
    }
}
