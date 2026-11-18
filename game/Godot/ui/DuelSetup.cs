using Godot;
using PoC.Engine;

namespace PoC.Game.UI;

/// Duel, step 2 (Chaos Rift, locked 2026-11-08): the rival stands in the dark; in the light, 1 DUEL TYPE (single duel
/// or match; a tournament round shows the round instead) and 2 YOUR DECK (deck cases, only decks legal under the ban
/// list in force), then START DUEL. The opponent's deck stays hidden.
public partial class DuelSetup : RiftMenu
{
    List<Deck> playable = null!;
    Control modes = null!, decks = null!;

    protected override string? MusicPath => Md("solo_gate");

    protected override void Build()
    {
        foreach (var a in OS.GetCmdlineUserArgs())
        {
            if (a.StartsWith("--rival=")) Session.Selected = int.Parse(a[8..]);
            if (a == "--tour") Session.Tour = new Tournament(new Rng(7));
        }
        Demo.Ensure();
        if (Session.Picks.Length == 0) Session.RollPicks();
        if (Session.Selected < 0) Session.Selected = 0;
        playable = Session.Decks.Where(d => DeckRules.IsLegal(d, P)).ToList();
        if (Session.Playing == null || !playable.Contains(Session.Playing)) Session.Playing = playable.FirstOrDefault();
        if (Session.Tour != null) Session.Match = true;   // a tournament round is a best-of-three match (playtest 2026-11-12)
        else if (Session.Mode != GameMode.Regular) Session.Match = false;
        if (Session.Run?.Current is { } g) Session.Selected = Array.FindIndex(Session.Roster, r => r.File == g.Rival);

        Rift(0.6f, 0.54f);
        Back(Session.Tour != null ? "TOURNAMENT" : Session.Run != null ? "GAUNTLET" : "RIVALS");
        var rv = Session.Rival;
        var tex = RivalPanels.Portrait(rv);
        float ph = H * 0.7f, pw = ph * tex.GetWidth() / tex.GetHeight();
        Pic(this, tex, new Vector2(W * 0.8f - pw / 2, H - ph), new Vector2(pw, ph));
        Put(this, rv.Name.ToUpperInvariant(), Title, 54, Silver, new Vector2(W * 0.8f - 300, 70), new Vector2(600, 70), HorizontalAlignment.Center);
        Put(this, Session.Pick.Tier, Title, 18, Session.TierColor(Session.Pick.Level), new Vector2(W * 0.8f - 300, 142), new Vector2(600, 24), HorizontalAlignment.Center);

        modes = new Control { Size = new Vector2(W, H), MouseFilter = MouseFilterEnum.Ignore };
        decks = new Control { Size = new Vector2(W, H), MouseFilter = MouseFilterEnum.Ignore };
        AddChild(modes); AddChild(decks);
        Modes();
        Decks();
        Button(this, "START DUEL", new Vector2((110 + CrackX(H * 0.6f) - 80) / 2, H - 86), Start, 32);
    }

    string Earns(bool match)
    {
        var (count, fresh) = Rewards.For(Session.Pick.Level, match);
        return (fresh > 0 ? Lang.F("win {0} card(s), {1} new", count, fresh) : Lang.F("win {0} card(s)", count));
    }

    void Modes()
    {
        foreach (var c in modes.GetChildren()) c.QueueFree();
        Put(modes, "1   DUEL TYPE", Title, 20, Graphite, new Vector2(110, 120));
        if (Session.Mode != GameMode.Regular && Session.Tour == null)
        {
            var info = PoC.Engine.Modes.Of(Session.Mode);
            Put(modes, info.Name, Title, 40, GoldDeep, new Vector2(110, 170));
            string line = Session.Run is { } run ? Lang.F("Floor {0} of {1}   ·   your LP {2:N0}", run.Index + 1, run.Order.Length, run.Lp)
                : Session.Rule is { } rule ? Lang.F("{0}   ·   both start at {1:N0} LP", rule.Name, rule.Lp) : Lang.Tr(info.Line);
            if (Session.Match) line += "   ·   " + Lang.Tr("best of three");
            Put(modes, line, Body, 22, Graphite, new Vector2(112, 226));
            return;
        }
        if (Session.Tour is { } t)
        {
            Put(modes, Tournament.RoundNames[t.Round], Title, 40, GoldDeep, new Vector2(110, 170));
            Put(modes, "Best of three, side deck between duels. Prizes are paid by where you finish.", Body, 22, Graphite, new Vector2(112, 226));
            return;
        }
        // one pill with two halves (Menu Lab pick 2026-11-16): the chosen half ink with gold text; the rule line under it
        string[] names = { "SINGLE DUEL", "MATCH" };
        string[] sub = { Lang.F("One duel  ·  {0}", Earns(false)), Lang.F("Best of three, side deck between duels  ·  {0}", Earns(true)) };
        var at = new Vector2(110, 162);
        float pw = 520, h = 58;
        var track = new Panel { Position = at, Size = new Vector2(pw, h), MouseFilter = MouseFilterEnum.Ignore };
        track.AddThemeStyleboxOverride("panel", Box(new Color(0, 0, 0, 0.06f), Ink * new Color(1, 1, 1, 0.14f), 1.5f, 99));
        modes.AddChild(track);
        var knob = new Panel { Position = at + new Vector2(Session.Match ? pw / 2 + 4 : 4, 4), Size = new Vector2(pw / 2 - 8, h - 8), MouseFilter = MouseFilterEnum.Ignore };
        knob.AddThemeStyleboxOverride("panel", Box(Ink, default, 0, 99, 10, new Color(0, 0, 0, 0.25f), new Vector2(0, 3)));
        modes.AddChild(knob);
        for (int i = 0; i < 2; i++)
        {
            bool m = i == 1, on = Session.Match == m;
            var l = Put(modes, names[i], Title, 19, on ? GoldLight : Ink * new Color(1, 1, 1, 0.6f), at + new Vector2(i * pw / 2, Z(19) * 0.12f), new Vector2(pw / 2, h), HorizontalAlignment.Center);
            l.VerticalAlignment = VerticalAlignment.Center;
            Click(l, () => { if (Session.Match == m) return; Session.Match = m; UiSound(false); Modes(); });
        }
        Put(modes, sub[Session.Match ? 1 : 0], Body, 19, Graphite, at + new Vector2(6, h + 14));
    }

    int deckPage = -1;   // -1 = open on the page of the deck in use

    void Decks()
    {
        foreach (var c in decks.GetChildren()) c.QueueFree();
        Put(decks, "2   YOUR DECK", Title, 20, Graphite, new Vector2(110, 340));
        if (Session.ModeDeck is { } md)
        {
            // the deck built for this mode: shown, not chosen; REBUILD goes back to the builder
            const float mw = 150; float mh = mw * 1.458f;
            if (md.CoverCard() >= 0) Card(decks, Db.Get(md.CoverCard()), new Vector2(130 + mw / 2, 420 + mh / 2), mh, -0.02f, 1.2f);
            Put(decks, md.Name, Title, 22, GoldDeep, new Vector2(320, 440));
            Put(decks, Lang.F("{0} CARDS   ·   FOR THIS DUEL ONLY", md.Main.Count), Thin, 16, Graphite, new Vector2(322, 476));
            var rb = Put(decks, "REBUILD", Title, 18, GoldDeep, new Vector2(322, 520));
            Click(rb, () => Transition.Go(this, "res://ui/DeckBuilder.tscn"), h => rb.AddThemeColorOverride("font_color", h ? Ink : GoldDeep));
            return;
        }
        if (playable.Count == 0) { Put(decks, "No deck is legal under the ban list in force. Edit one in the Deck Builder.", Body, 22, Graphite, new Vector2(112, 400)); return; }
        // two rows of five deck cases (Menu Lab pick 2026-11-16): names fit their cell, round arrows at the row ends,
        // page dots and "1 - 10 OF 12 DECKS"
        float left = 110, right = CrackX(H * 0.6f) - 80, top = 402;
        const int perRow = 5, rows = 2, per = perRow * rows;
        int pages = (playable.Count + per - 1) / per;
        deckPage = Math.Clamp(Session.Playing is { } cur && playable.IndexOf(cur) is >= 0 and var ix && deckPage < 0 ? ix / per : deckPage, 0, pages - 1);
        int first = deckPage * per, last = Math.Min(playable.Count, first + per);
        Put(decks, Lang.F("{0} - {1}  OF  {2} DECKS", first + 1, last, playable.Count), Thin, 14, Graphite, new Vector2(left, 342), new Vector2(right - left, 22), HorizontalAlignment.Right);
        float arrowW = 56, cellW = (right - left - 2 * arrowW) / perRow, cw = Math.Min(118, cellW * 0.66f), ch = cw * 1.458f, rowH = ch + 80;
        for (int i = first; i < last; i++)
        {
            var d = playable[i];
            int k = i - first;
            bool on = d == Session.Playing;
            var cell = new Vector2(left + arrowW + k % perRow * cellW, top + k / perRow * rowH);
            var at = cell + new Vector2((cellW - cw) / 2, on ? -8 : 0);
            for (int b = 2; b >= 1; b--)
            {
                var back = Pic(decks, UI.Cosmetics.Back(P.Back, 256), at + new Vector2(b * cw * 0.05f, -b * cw * 0.04f), new Vector2(cw, ch));
                back.PivotOffset = new Vector2(cw, ch) / 2; back.Rotation = b * 0.04f;
            }
            if (d.CoverCard() >= 0) Card(decks, Db.Get(d.CoverCard()), at + new Vector2(cw, ch) / 2, ch, -0.02f, on ? 1.3f : 0.6f);
            int fs = 14;
            string nm = d.Name.ToUpperInvariant();
            while (fs > 11 && BodyBold.GetStringSize(nm, HorizontalAlignment.Left, -1, Z(fs)).X > cellW - 8) fs--;
            var name = Put(decks, nm, BodyBold, fs, on ? GoldDeep : Ink, cell + new Vector2(4, ch + 18), new Vector2(cellW - 8, 24), HorizontalAlignment.Center);
            name.ClipText = true;
            if (on) Pip(decks, cell + new Vector2(cellW / 2, ch + 56), 8);
            var hit = new Control { Position = cell, Size = new Vector2(cellW, ch + 50) };
            decks.AddChild(hit);
            Click(hit, () => { Session.Playing = d; UiSound(false); Decks(); });
        }
        float ay = top + (rows * rowH - 80) / 2;
        foreach (int dir in new[] { -1, 1 })
        {
            bool live = dir < 0 ? deckPage > 0 : deckPage < pages - 1;
            if (pages <= 1) break;
            var c = new Vector2(dir < 0 ? left + arrowW / 2 - 6 : right - arrowW / 2 + 6, ay);
            var p = new Panel { Position = c - new Vector2(24, 24), Size = new Vector2(48, 48) };
            p.AddThemeStyleboxOverride("panel", live ? Box(Ink, default, 0, 99, 10, new Color(0, 0, 0, 0.25f), new Vector2(0, 3)) : Box(new Color(0, 0, 0, 0.06f), default, 0, 99));
            decks.AddChild(p);
            var l = Put(p, dir < 0 ? "‹" : "›", Title, 28, live ? GoldLight : Graphite * new Color(1, 1, 1, 0.35f), new Vector2(0, -6), new Vector2(48, 52), HorizontalAlignment.Center);
            l.VerticalAlignment = VerticalAlignment.Center;
            if (live) Click(p, () => { deckPage += dir; UiSound(false); Decks(); });
        }
        if (pages > 1)
            for (int i = 0; i < pages; i++)
            {
                bool on = i == deckPage;
                var c = new Vector2((left + right) / 2 + (i - (pages - 1) / 2f) * 24, top + rows * rowH - 6);
                var dot = new Panel { Position = c - new Vector2(on ? 13 : 4.5f, 4.5f), Size = new Vector2(on ? 26 : 9, 9), MouseFilter = MouseFilterEnum.Ignore };
                dot.AddThemeStyleboxOverride("panel", Box(on ? GoldDeep : Ink * new Color(1, 1, 1, 0.22f), default, 0, 99));
                decks.AddChild(dot);
            }
    }

    void Start()
    {
        if (Session.ModeDeck == null && Session.Playing == null) return;
        Session.Save();
        Session.MatchWins = Session.MatchLosses = 0;
        Session.MatchDeck = Session.Match ? (Session.ModeDeck ?? Session.Playing)!.Copy() : null;
        (Session.RivalDeck, Session.RivalSeen) = (null, new CpuSide.Seen());
        Transition.Go(this, "res://ui/TurnOrder.tscn");
    }

    protected override void OnBack() => Transition.Go(this, Session.Tour != null ? "res://ui/Tournament.tscn" : Session.Run != null ? "res://ui/Gauntlet.tscn" : "res://ui/RivalSelect.tscn");
}
