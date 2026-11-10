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
        if (Session.Tour != null || Session.Mode != GameMode.Regular) Session.Match = false;   // only a regular duel can be a match
        if (Session.Run?.Current is { } g) Session.Selected = Array.FindIndex(RivalPanels.All, r => r.File == g.Rival);

        Rift(0.6f, 0.54f);
        Back(Session.Tour != null ? "TOURNAMENT" : Session.Run != null ? "MODES" : "RIVALS");
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
        Button(this, "START DUEL", new Vector2(W * 0.4f, H - 100), Start, 32);
    }

    string Earns(bool match)
    {
        var (count, fresh) = Rewards.For(Session.Pick.Level, match);
        return $"win {count} card{(count == 1 ? "" : "s")}{(fresh > 0 ? $", {fresh} new" : "")}";
    }

    void Modes()
    {
        foreach (var c in modes.GetChildren()) c.QueueFree();
        Put(modes, "1   DUEL TYPE", Title, 20, Graphite, new Vector2(110, 120));
        if (Session.Mode != GameMode.Regular && Session.Tour == null)
        {
            var info = PoC.Engine.Modes.Of(Session.Mode);
            Put(modes, info.Name, Title, 40, GoldDeep, new Vector2(110, 170));
            string line = Session.Run is { } run ? $"Rival {run.Index + 1} of {run.Order.Length}   ·   your LP {run.Lp:N0}"
                : Session.Rule is { } rule ? $"{rule.Name}   ·   both start at {rule.Lp:N0} LP" : info.Line;
            Put(modes, line, Body, 22, Graphite, new Vector2(112, 226));
            return;
        }
        if (Session.Tour is { } t)
        {
            Put(modes, Tournament.RoundNames[t.Round], Title, 40, GoldDeep, new Vector2(110, 170));
            Put(modes, "Win to go through. Prizes are paid by where you finish.", Body, 22, Graphite, new Vector2(112, 226));
            return;
        }
        string[] names = { "SINGLE DUEL", "MATCH" };
        string[] sub = { $"One duel  ·  {Earns(false)}", $"Best of three, side deck between duels  ·  {Earns(true)}" };
        for (int i = 0; i < 2; i++)
        {
            bool m = i == 1, on = Session.Match == m;
            var at = new Vector2(110 + i * 420, 170);
            var l = Put(modes, names[i], Title, on ? 40 : 34, on ? GoldDeep : Ink * new Color(1, 1, 1, 0.5f), at);
            var s = Put(modes, sub[i], Body, 20, Graphite * new Color(1, 1, 1, on ? 0.9f : 0.5f), at + new Vector2(2, 56), new Vector2(400, 50));
            s.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            if (on) Hair(modes, at + new Vector2(0, 50), Title.GetStringSize(names[i], HorizontalAlignment.Left, -1, 40).X, GoldC, 2.5f);
            Click(l, () => { Session.Match = m; UiSound(false); Modes(); });
        }
    }

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
            Put(decks, $"{md.Main.Count} CARDS   ·   FOR THIS DUEL ONLY", Thin, 16, Graphite, new Vector2(322, 476));
            var rb = Put(decks, "REBUILD", Title, 18, GoldDeep, new Vector2(322, 520));
            Click(rb, () => Transition.Go(this, "res://ui/DeckBuilder.tscn"), h => rb.AddThemeColorOverride("font_color", h ? Ink : GoldDeep));
            return;
        }
        if (playable.Count == 0) { Put(decks, "No deck is legal under the ban list in force. Edit one in the Deck Builder.", Body, 22, Graphite, new Vector2(112, 400)); return; }
        const float cw = 150, gx = 90; float ch = cw * 1.458f;
        int cols = (int)((CrackX(600) - 200) / (cw + gx));
        Paged(decks, new Rect2(130, 420, cols * (cw + gx), ch + 120), playable.Count, cols, 1, new Vector2(cw + gx, 0), (i, host, at) =>
        {
            var d = playable[i];
            bool on = d == Session.Playing;
            for (int k = 2; k >= 1; k--)
            {
                var b = Pic(host, UI.Cosmetics.Back(P.Back, 256), at + new Vector2(k * 8, -k * 6), new Vector2(cw, ch));
                b.PivotOffset = new Vector2(cw, ch) / 2; b.Rotation = k * 0.04f;
            }
            if (d.CoverCard() >= 0) { var c = Card(host, Db.Get(d.CoverCard()), at + new Vector2(cw, ch) / 2, ch, -0.02f, on ? 1.3f : 0.6f); if (on) c.Scale = new Vector2(1.06f, 1.06f); }
            Put(host, d.Name.ToUpperInvariant(), Title, 15, on ? GoldDeep : Ink, at + new Vector2(-30, ch + 20), new Vector2(cw + 60, 22), HorizontalAlignment.Center);
            if (on) Pip(host, at + new Vector2(cw / 2, ch + 56), 8);
            var hit = new Control { Position = at - new Vector2(10, 10), Size = new Vector2(cw + 30, ch + 20) };
            host.AddChild(hit);
            Click(hit, () => { Session.Playing = d; UiSound(false); Decks(); });
        }, new Vector2(130, 420 + ch + 90));
    }

    void Start()
    {
        if (Session.ModeDeck == null && Session.Playing == null) return;
        Session.Save();
        Session.MatchWins = Session.MatchLosses = 0;
        Session.MatchDeck = Session.Match ? Session.Playing.Copy() : null;
        (Session.RivalDeck, Session.RivalSeen) = (null, new CpuSide.Seen());
        Transition.Go(this, "res://ui/TurnOrder.tscn");
    }

    protected override void OnBack() => Transition.Go(this, Session.Tour != null ? "res://ui/Tournament.tscn" : Session.Run != null ? "res://ui/DuelModes.tscn" : "res://ui/RivalSelect.tscn");
}
