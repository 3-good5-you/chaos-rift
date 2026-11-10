using Godot;
using PoC.Engine;

namespace PoC.Game.UI;

/// Results after a duel (Chaos Rift, 2026-11-09, Victory Lab picks): you in the light (headline, numbers, the reward
/// cards dealt and flipped; click one to see it), the rival in the dark (sad when you win, happy when you lose).
/// Tournament champion = the podium, gauntlet cleared = the road through the rivals, a first challenge solve = the
/// tiles; their cards behind SEE YOUR PRIZE. Records the duel (streak, Joey's ladder),
/// keeps the match score and grants the rewards into Session.Profile.
/// Screenshot args fake the incoming duel: `--case=win|loss|draw|match1|matchwin|matchloss --rival=N`.
public partial class Results : RiftMenu
{
    sealed record Reward(CardData Card, bool New);

    RivalPanels.Rival rival = null!;
    int outcome, scoreYou, scoreThem, prize = 1;   // prize = cards a win gives at this difficulty (Rewards.For)
    bool match, matchOver;
    readonly List<Reward> cards = new();
    readonly List<Control> buttons = new();
    readonly List<string> targets = new();
    AudioStreamPlayer? sfx;
    const float RevealStart = 1.6f, RevealStep = 0.75f;

    bool Won => outcome == 1;
    bool MatchOn => match && !matchOver;
    float RevealEnd => RevealStart + Math.Max(1, cards.Count) * RevealStep + 0.4f;
    protected override string? MusicPath => null;
    protected override int ItemCount => buttons.Count;

    string Headline => match && matchOver ? (Won ? "MATCH WON" : "MATCH LOST") : outcome switch { 1 => "VICTORY", 0 => "DEFEAT", _ => "DRAW" };

    /// Screenshot/demo: stand in for the duel that just ended.
    static void Demo()
    {
        if (Session.Picks.Length == 0) Session.RollPicks();   // started on its own (screenshots)
        foreach (var a in OS.GetCmdlineUserArgs())
        {
            if (a.StartsWith("--rival=")) Session.Selected = int.Parse(a[8..]);
            if (a == "--tour") Session.Tour ??= new Tournament(new Rng(7));   // screenshots: a tournament round's results
            if (!a.StartsWith("--case=")) continue;
            var c = a[7..];
            if (Session.Selected < 0) Session.Selected = 1;
            // the special screens: a tournament down to its final, a gauntlet at its last rival, an unsolved challenge
            if (c == "champion") { var t = Session.Tour = new Tournament(new Rng(7)); for (int r = 0; r < 2; r++) { t.PlayOthers(); t.Record(true); } }
            if (c == "gauntlet") { Session.Mode = GameMode.Gauntlet; Session.Run = new Gauntlet(Session.Picks); Session.Run.Index = Session.Run.Order.Length - 1; Session.LastLp = 3450; }
            if (c == "puzzle") { Session.Mode = GameMode.Challenges; Session.Puzzle = Puzzles.All[6]; Session.Profile.Solved.Remove(Session.Puzzle.Id); }
            Session.Match = c.StartsWith("match");
            (Session.MatchWins, Session.MatchLosses) = c switch { "matchwin" => (1, 1), "matchloss" => (0, 1), _ => (0, 0) };
            if (Session.Selected < 0) Session.Selected = 0;
            Session.FinishDuel(c is "loss" or "matchloss" ? 1 : c == "draw" ? -1 : 0, "LP reached 0");
        }
        if (Session.Selected < 0) Session.Selected = 0;
        if (Session.LastDuel == null) Session.FinishDuel(0, "LP reached 0");
    }

    /// The duel was recorded when it ended (Session.FinishDuel); here: the outcome and the rewards.
    void Apply()
    {
        Demo();
        rival = Session.Rival;
        var d = Session.LastDuel!;
        Session.LastDuel = null;
        outcome = d.Winner switch { 0 => 1, 1 => 0, _ => 2 };
        var p = Session.Profile;
        match = Session.Match;
        if (match)
        {
            matchOver = Session.MatchOver;
            (scoreYou, scoreThem) = (Session.MatchWins, Session.MatchLosses);
        }
        if (Session.Tour is { } tour)
        {
            // The other matches of this round are played, the bracket moves on; when your tournament is over the
            // prize for your finishing place is paid once (Tournament.Ladder).
            var foe = tour.Foe;
            tour.PlayOthers(); tour.Record(Won);   // a draw knocks you out
            if (tour.Place is int place && foe != null)
            {
                var (dp, n, fresh) = Tournament.Ladder[place];
                prize = n;
                p.Dp += dp; Session.LastDp = dp;
                if (n > 0) foreach (var (card, isNew) in Rewards.Roll(p, "tournament", Tournament.PrizePool(foe), n, false, Session.Rng, fresh)) cards.Add(new Reward(card, isNew));
                if (place == 0)
                {
                    Session.LastMat = Shop.PrizeMat(p);   // the next prize playmat, revealed after this screen
                    // the finalist's ace card
                    var ace = CardDb.Shared.Named(foe.Card);
                    bool isNew = p.Count(ace.Idx) == 0;
                    p.Owned[ace.Idx] = Math.Min(255, p.Count(ace.Idx) + 1);
                    if (isNew) p.Fresh.Add(ace.Idx);
                    cards.Add(new Reward(ace, isNew));
                }
            }
        }
        else if (Session.Run is { } run)
        {
            run.Record(Won, Session.LastLp);
            if (run.Cleared)
            {
                var (dp, n, fresh) = Gauntlet.ClearPrize;
                prize = n; p.Dp += dp; Session.LastDp += dp;
                foreach (var (card, isNew) in Rewards.Roll(p, "tournament", Rewards.Pool("expansion"), n, false, Session.Rng, fresh)) cards.Add(new Reward(card, isNew));
            }
        }
        else if (Session.Puzzle != null) { prize = 0; }   // DP only (Session.FinishDuel), first solve
        else if (Session.Mode == GameMode.Legend)
        {
            if (Won)
            {
                var (dp, n, fresh) = Modes.LegendPrize;
                prize = n; p.Dp += dp; Session.LastDp += dp;
                foreach (var (card, isNew) in Rewards.Roll(p, rival.File, n, false, Session.Rng, fresh)) cards.Add(new Reward(card, isNew));
            }
        }
        else if (Session.Mode == GameMode.Pack && Session.ModeDeck is { } pd)
        {
            // keep cards from your packs: 3 for a win, 1 for a loss
            var pool = (DeckBuilder.Temp?.Packs?.SelectMany(x => x) ?? pd.Main).Distinct().ToList();
            int keep = Won ? 3 : 1;
            for (int k = 0; k < keep && pool.Count > 0; k++)
            {
                var c = CardDb.Shared.Get(pool[Session.Rng.Int(pool.Count)]);
                pool.Remove(c.Idx);
                bool isNew = p.Count(c.Idx) == 0;
                p.Owned[c.Idx] = Math.Min(255, p.Count(c.Idx) + 1);
                if (isNew) p.Fresh.Add(c.Idx);
                cards.Add(new Reward(c, isNew));
            }
            prize = keep;
        }
        else
        {
            (prize, int mustBeNew) = Rewards.For(Session.Pick.Level, match);
            int count = match ? (matchOver && Won ? prize : 0) : (Won ? prize : 0);
            if (count > 0)
                foreach (var (card, isNew) in Rewards.Roll(p, rival.File, count, match, Session.Rng, mustBeNew)) cards.Add(new Reward(card, isNew));
        }
        Session.Save();
        // A finished duel or match: the next opponent pick and difficulty follow the new streak / level.
        if (!MatchOn) Session.RollPicks();
    }

    protected override void Build()
    {
        Apply();
        if (!Shooting)
        {
            AddChild(new AudioStreamPlayer { Stream = GD.Load<AudioStream>(Won ? "res://assets/audio/m_win.wav" : "res://assets/audio/m_lose.wav"), VolumeDb = -10, Autoplay = true, Bus = "Music" });
            sfx = new AudioStreamPlayer { VolumeDb = -4, Bus = "Sound" };
            AddChild(sfx);
        }
        // The special moments (Victory Lab picks, 2026-11-09); everything else is the split results.
        if (Session.Tour is { Place: 0 } t) Podium(t);
        else if (Session.Run is { Cleared: true } run) Road(run);
        else if (Session.Puzzle is { } z && Won && Session.LastDp > 0) Tiles(z);
        else { Split(); SaveReplay(this, new Vector2(W * 0.14f, H - 145)); return; }   // above MAIN MENU: the corner spot sat on it (playtest 2026-11-09)
        SaveReplay(this, new Vector2(150, H - 50));
    }

    /// SAVE REPLAY (asked after each duel, 2026-11-09): writes Session.LastReplay to user://replays once.
    void SaveReplay(Control host, Vector2 at)
    {
        if (Session.LastReplay is not { } r) return;
        Control b = null!;
        b = Button(host, "SAVE REPLAY", at, () => NamePrompt("NAME THIS REPLAY", r.Title, name =>
        {
            r.Name = name;
            try { r.Save(Session.ReplayDir); Session.LastReplay = null; b.Visible = false; T(host, "REPLAY SAVED", Thin, 16, at - new Vector2(80, 10), new Vector2(160, 24), HorizontalAlignment.Center, 0.8f); }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException) { GD.PrintErr($"replay: {e.Message}"); }
        }), 16);
        Appear(b, 2.4);
    }

    // ---------------------------------------------------------------- the split results (Victory Lab row 1/2: A)

    /// You in the light (the word, the numbers, the cards), the rival in the dark, standing beaten (or smug).
    void Split()
    {
        Rift(0.66f, 0.6f);
        var head = T(this, Headline, Title, 110, new Vector2(90, 70), new Vector2(1100, 130));
        if (Won) head.Mat().P("lit", 1f);
        Appear(head, 0.1, new Vector2(-60, 0));
        string who = rival.Name.ToUpperInvariant();
        string sub = outcome switch { 1 => $"{who} DEFEATED", 0 => $"{who} WINS", _ => "NO ONE WINS" };
        if (Session.Mode != GameMode.Regular && Session.Tour == null) sub += "  ·  " + Modes.Of(Session.Mode).Name;
        if (Session.Tour is { } tour) sub += "  ·  " + (tour.Place is int pl ? Tournament.PlaceNames[pl] : Tournament.RoundNames[Math.Min(2, tour.Round - 1)]);
        Appear(T(this, sub, Thin, 26, new Vector2(96, 210), new Vector2(1000, 34), HorizontalAlignment.Left, 0.8f), 0.4);
        Hair(this, new Vector2(98, 256), 200, GoldC); Pip(this, new Vector2(310, 257), 10);
        // the numbers
        var stats = new List<string>();
        if (Session.LastDp > 0) stats.Add($"+{Session.LastDp} DP");
        if (match) stats.Add($"MATCH  {scoreYou} – {scoreThem}");
        if (Session.Run is { } run && !run.Over) stats.Add($"{run.Lp} LP CARRIED");
        if (Session.LastStarter is { } st) stats.Add($"{st.Name.ToUpperInvariant()} UNLOCKED");
        if (Session.LastMat is { } lm) stats.Add($"{lm.Name.ToUpperInvariant()} PLAYMAT");
        for (int i = 0; i < stats.Count; i++) Appear(T(this, stats[i], Title, 30, new Vector2(98 + i * 330, 290), new Vector2(320, 40)), 0.6 + i * 0.12);
        // the cards (a match not yet over: dim backs for the cards still to win)
        int n = Math.Max(cards.Count, MatchOn ? prize : 0);
        // fit the row into the light side
        float gap = 34, ch = Math.Min(380, ((W * 0.56f + gap) / Math.Max(1, n) - gap) / 0.686f);
        if (n > 0) Deal(this, n, new Vector2(W * 0.31f, H * 0.6f), ch, gap, 0.8);
        else Appear(T(this, MatchOn ? $"WIN THE MATCH TO EARN {prize} CARDS" : "NO CARDS THIS TIME", Thin, 28, new Vector2(98, H * 0.55f), new Vector2(1100, 36), HorizontalAlignment.Left, 0.7f), 0.8);
        // the rival on the dark side: sad when you win, happy when you lose
        string mood = outcome switch { 1 => "_sad", 0 => "_happy", _ => "" };
        Standing(rival, mood, H * 0.9f, new Vector2(W * 0.82f, H + 10), 0.2);
        Buttons(new Vector2(W * 0.14f, H - 80), W * 0.26f);
    }

    // ---------------------------------------------------------------- tournament champion: the podium

    void Podium(Tournament t)
    {
        Rift(0.5f, 0.5f);
        // runner-up: the other finalist; third: the one you beat in the semifinal
        int si = Array.IndexOf(t.Winners[1], Tournament.You), qi = Array.IndexOf(t.Winners[0], Tournament.You);
        RivalPanels.Rival? second = si >= 0 && t.Winners[1][si ^ 1] is { } a ? RivalPanels.Of(Tournament.Get(a)) : rival;
        RivalPanels.Rival? third = qi >= 0 && t.Winners[0][qi ^ 1] is { } b ? RivalPanels.Of(Tournament.Get(b)) : null;
        var head = T(this, "CHAMPION", Title, 90, new Vector2(0, 40), new Vector2(W, 110), HorizontalAlignment.Center);
        head.Mat().P("lit", 1f);
        Appear(head, 1.6);
        Appear(T(this, $"+{Session.LastDp:N0} DP  ·  {cards.Count} CARDS", Thin, 24, new Vector2(0, 150), new Vector2(W, 32), HorizontalAlignment.Center, 0.8f), 1.8);
        (RivalPanels.Rival? who, string place, float x, float h, double at)[] steps =
            { (second, "2", W * 0.27f, H * 0.2f, 0.3), (null, "1", W * 0.5f, H * 0.32f, 1.0), (third, "3", W * 0.73f, H * 0.13f, 0.6) };
        foreach (var (who, place, x, h, at) in steps)
        {
            if (place == "3" && who == null) continue;
            float bw = W * 0.2f, top = H - 40 - h;
            var block = new Panel { Position = new Vector2(x - bw / 2, top), Size = new Vector2(bw, h + 60), MouseFilter = MouseFilterEnum.Ignore };
            var sb = new StyleBoxFlat { BgColor = new Color(0.05f, 0.05f, 0.07f, 0.95f), BorderColor = place == "1" ? GoldLight : GoldC with { A = 0.5f } };
            sb.SetBorderWidthAll(3); sb.SetCornerRadiusAll(8);
            block.AddThemeStyleboxOverride("panel", sb);
            AddChild(block);
            Put(block, place, Title, place == "1" ? 90 : 64, place == "1" ? GoldLight : Silver, new Vector2(0, 20), new Vector2(bw, 110), HorizontalAlignment.Center);
            float d = place == "1" ? 240 : 170;
            var f = Face(who, d); f.Position = new Vector2(x - d / 2, top - d - 16); AddChild(f);
            var name = Put(this, who?.Name.ToUpperInvariant() ?? Settings.PlayerName.ToUpperInvariant(), Title, 24, place == "1" ? GoldLight : Mist, new Vector2(x - 200, top - d - 56), new Vector2(400, 32), HorizontalAlignment.Center);
            foreach (var c in new Control[] { block, f, name }) Appear(c, at, new Vector2(0, 260));
        }
        Burst(1.2);
        Confetti(1.3);
        PrizeButton(new Vector2(W * 0.88f, H * 0.42f));
        Buttons(new Vector2(W * 0.84f, H * 0.6f), 0, vertical: true);
    }

    // ---------------------------------------------------------------- gauntlet cleared: the road

    void Road(Gauntlet run)
    {
        Rift(0.5f, 0.5f);
        float y = H * 0.48f, x0 = W * 0.08f, x1 = W * 0.92f;
        var road = new DrawBox { Size = new Vector2(W, H), MouseFilter = MouseFilterEnum.Ignore };
        float k = 0;
        road.Paint = b =>
        {
            b.DrawLine(new Vector2(x0, y), new Vector2(x1, y), Mist with { A = 0.3f }, 4);
            b.DrawLine(new Vector2(x0, y), new Vector2(Mathf.Lerp(x0, x1, k), y), GoldLight, 7);
        };
        AddChild(road);
        var me = Face(null, 150); me.Position = new Vector2(x0 - 75, y - 75); AddChild(me);
        var lp = Put(this, "8000", Title, 40, GoldLight, new Vector2(x0 - 100, y + 90), new Vector2(200, 50), HorizontalAlignment.Center);
        Put(this, "LP", Thin, 18, Mist, new Vector2(x0 - 100, y + 140), new Vector2(200, 24), HorizontalAlignment.Center);
        int n = run.Order.Length;
        for (int i = 0; i < n; i++)
        {
            var r = RivalPanels.All.FirstOrDefault(x => x.File == run.Order[i].Rival) ?? rival;
            float x = Mathf.Lerp(x0, x1, (i + 1f) / n) - (i == n - 1 ? 40 : 0);
            var f = Face(r, 170); f.Position = new Vector2(x - 85, y - 85); f.Modulate = new Color(0.45f, 0.45f, 0.5f); AddChild(f);
            T(this, r.Name.ToUpperInvariant(), Title, 24, new Vector2(x - 150, y + 100), new Vector2(300, 32), HorizontalAlignment.Center);
            int left = 8000 - (8000 - run.Lp) * (i + 1) / n;   // ponytail: only the final LP is kept; the steps between are drawn evenly
            GetTree().CreateTimer(0.85 + i * 0.55).Timeout += () =>
            {
                CreateTween().TweenProperty(f, "modulate", Colors.White, 0.2);
                var p = Pip(this, new Vector2(x, y - 110), 26);
                p.Scale = new Vector2(2, 2);
                CreateTween().TweenProperty(p, "scale", Vector2.One, 0.25).SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
                lp.Text = left.ToString();
                Sfx("card_open");
            };
        }
        CreateTween().TweenMethod(Callable.From<float>(v => { k = v; road.QueueRedraw(); }), 0f, 1f, 0.55 * n).SetDelay(0.4);
        var head = T(this, "GAUNTLET CLEARED", Title, 76, new Vector2(0, H * 0.1f), new Vector2(W, 96), HorizontalAlignment.Center);
        head.Mat().P("lit", 1f);
        Appear(head, 0.5 + 0.55 * n);
        Burst(0.5 + 0.55 * n);
        Appear(T(this, $"FOUR RIVALS, ONE DECK  ·  +{Session.LastDp:N0} DP  ·  {cards.Count} CARDS", Thin, 24, new Vector2(0, H * 0.1f + 100), new Vector2(W, 32), HorizontalAlignment.Center), 0.7 + 0.55 * n);
        PrizeButton(new Vector2(W / 2, H - 160));
        Buttons(new Vector2(W / 2 - 230, H - 70), 460);
    }

    // ---------------------------------------------------------------- first challenge solve: the tiles

    void Tiles(Puzzles.Puzzle z)
    {
        Rift(0.5f, 0.5f);
        var head = T(this, "CHALLENGE SOLVED", Title, 70, new Vector2(0, 60), new Vector2(W, 90), HorizontalAlignment.Center);
        head.Mat().P("lit", 1f);
        Appear(head, 1.2);
        int count = Puzzles.All.Length, cols = Math.Min(4, count), rows = (count + cols - 1) / cols;
        float tw0 = 180, th = 250, gap = 34, x0 = W / 2 - (cols * tw0 + (cols - 1) * gap) / 2, y0 = H * 0.24f;
        for (int i = 0; i < count; i++)
        {
            var pz = Puzzles.All[i];
            bool now = pz == z, solved = P.Solved.Contains(pz.Id);
            var at = new Vector2(x0 + (i % cols) * (tw0 + gap), y0 + (i / cols) * (th + gap));
            var tile = new Panel { Position = at, Size = new Vector2(tw0, th), PivotOffset = new Vector2(tw0, th) / 2, MouseFilter = MouseFilterEnum.Ignore };
            var sb = new StyleBoxFlat { BgColor = solved && !now ? new Color(0.22f, 0.17f, 0.08f) : new Color(0.06f, 0.06f, 0.08f), BorderColor = solved ? GoldC : Mist with { A = 0.3f } };
            sb.SetBorderWidthAll(3); sb.SetCornerRadiusAll(12);
            tile.AddThemeStyleboxOverride("panel", sb);
            AddChild(tile);
            var num = Put(tile, $"{i + 1:00}", Title, 70, solved && !now ? GoldLight : Mist with { A = 0.5f }, new Vector2(0, 70), new Vector2(tw0, 90), HorizontalAlignment.Center);
            if (solved && !now) Pip(tile, new Vector2(tw0 / 2, 200), 14);
            if (!now) continue;
            var flip = CreateTween();
            flip.TweenInterval(0.6);
            flip.TweenProperty(tile, "scale:x", 0f, 0.15);
            flip.TweenCallback(Callable.From(() =>
            {
                sb.BgColor = GoldC; sb.BorderColor = GoldLight;
                num.AddThemeColorOverride("font_color", Ink);
                Pip(tile, new Vector2(tw0 / 2, 200), 18);
                Sfx("reborn");
            }));
            flip.TweenProperty(tile, "scale", new Vector2(1.12f, 1.12f), 0.15);
            flip.TweenProperty(tile, "scale", Vector2.One, 0.3).SetTrans(Tween.TransitionType.Elastic).SetEase(Tween.EaseType.Out);
            Burst(0.9);
        }
        int idx = Array.IndexOf(Puzzles.All, z) + 1;
        float below = y0 + rows * (th + gap) + 20;
        Appear(T(this, $"{idx:00}  ·  {z.Name}", Title, 34, new Vector2(0, below), new Vector2(W, 44), HorizontalAlignment.Center), 1.1);
        Appear(T(this, $"FIRST SOLVE  ·  +{Session.LastDp} DP  ·  {P.Solved.Count} OF {count}", Thin, 24, new Vector2(0, below + 54), new Vector2(W, 32), HorizontalAlignment.Center, 0.8f), 1.3);
        Buttons(new Vector2(W / 2 - 230, H - 70), 460);
    }

    // ---------------------------------------------------------------- shared pieces

    (string Label, string Target)[] Items => MatchOn
        ? new[] { ("NEXT DUEL", "res://ui/field/DuelField.tscn") }
        : Session.Tour != null ? new[] { ("MAIN MENU", "res://ui/MainMenu.tscn"), ("BRACKET", "res://ui/Tournament.tscn") }
        : Session.Puzzle != null ? new[] { ("CHALLENGES", "res://ui/ChallengeSelect.tscn"), ("TRY AGAIN", "res://ui/field/DuelField.tscn") }
        : Session.Run is { Over: false } ? new[] { ("MAIN MENU", "res://ui/MainMenu.tscn"), ("NEXT RIVAL", "res://ui/DuelSetup.tscn") }
        : Session.Mode != GameMode.Regular ? new[] { ("MAIN MENU", "res://ui/MainMenu.tscn"), ("MODES", "res://ui/DuelModes.tscn") }
        : new[] { ("MAIN MENU", "res://ui/MainMenu.tscn"), ("CONTINUE", "res://ui/RivalSelect.tscn") };

    /// The way on: diamond buttons, left to right (or top to bottom), the last one the main one.
    void Buttons(Vector2 at, float step, bool vertical = false)
    {
        var items = Items;
        for (int i = 0; i < items.Length; i++)
        {
            int k = i;
            var pos = vertical ? at + new Vector2(0, i * 110) : at + new Vector2(i * step, 0);
            var b = Button(this, items[i].Label, pos, () => Choose(k), i == items.Length - 1 ? 30 : 24);
            Appear(b, Math.Min(3.0, Math.Max(1.4, RevealEnd - 0.3)));
            buttons.Add(b);
            targets.Add(items[i].Target);
        }
    }

    void PrizeButton(Vector2 at)
    {
        if (cards.Count == 0) return;
        Appear(Button(this, "SEE YOUR PRIZE", at, ShowPrize, 26), 2.2);
    }

    /// The reward cards dealt over the screen; click one to see it large.
    void ShowPrize()
    {
        var layer = new Control { Size = new Vector2(W, H), MouseFilter = MouseFilterEnum.Stop, ZIndex = 20 };
        AddChild(layer);
        layer.AddChild(new ColorRect { Size = new Vector2(W, H), Color = new Color(0.02f, 0.02f, 0.03f, 0.9f), MouseFilter = MouseFilterEnum.Ignore });
        Put(layer, "YOUR PRIZE", Title, 56, GoldLight, new Vector2(0, 90), new Vector2(W, 70), HorizontalAlignment.Center);
        Put(layer, $"{cards.Count} CARDS  ·  CLICK ONE TO SEE IT", Thin, 22, Mist, new Vector2(0, 170), new Vector2(W, 30), HorizontalAlignment.Center);
        Deal(layer, cards.Count, new Vector2(W / 2, H * 0.5f), cards.Count > 4 ? 330 : 400, 40, 0.1);
        Button(layer, "CLOSE", new Vector2(W / 2, H - 90), () => layer.QueueFree(), 26);
    }

    /// `n` cards dealt face down along a row, then flipped one by one (cards beyond the rewards stay dim backs);
    /// NEW in gold under the new ones; click a card to see it large.
    void Deal(Control host, int n, Vector2 centre, float h, float gap, double start)
    {
        float w = h * 0.686f, row = n * (w + gap) - gap;
        var back = Cosmetics.Back(P.Back, 512);
        for (int i = 0; i < n; i++)
        {
            var at = new Vector2(centre.X - row / 2 + i * (w + gap) + w / 2, centre.Y);
            bool real = i < cards.Count;
            var data = real ? cards[i].Card : CardDb.Shared.Get(0);
            var card = Card(host, data, at + new Vector2(0, H), h, 0.3f, 0.9f);
            var face = card.GetChildren().OfType<TextureRect>().Last();
            var art = face.Texture; face.Texture = back;
            var tw = CreateTween().SetParallel();
            tw.TweenProperty(card, "position:y", at.Y - h / 2, 0.55).SetDelay(start + i * 0.12).SetTrans(Tween.TransitionType.Expo).SetEase(Tween.EaseType.Out);
            tw.TweenProperty(card, "rotation", 0f, 0.55).SetDelay(start + i * 0.12).SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
            if (!real) { card.Modulate = new Color(0.35f, 0.35f, 0.4f); continue; }
            var rw = cards[i];
            Click(card, () => Preview(rw.Card));
            var flip = CreateTween();
            flip.TweenInterval(start + 0.8 + i * 0.3);
            flip.TweenProperty(card, "scale:x", 0f, 0.1);
            flip.TweenCallback(Callable.From(() => { face.Texture = CardArt.Large(rw.Card); Sfx(rw.New ? "reborn" : "card_open"); }));
            flip.TweenProperty(card, "scale:x", 1f, 0.12);
            if (!rw.New) continue;
            var tag = T(host, "NEW", Title, 22, new Vector2(at.X - 60, at.Y + h / 2 + 12), new Vector2(120, 28), HorizontalAlignment.Center);
            tag.Mat().P("lit", 1f);
            Appear(tag, start + 1.0 + i * 0.3);
        }
    }

    void Preview(CardData d)
    {
        var layer = new Control { Size = new Vector2(W, H), MouseFilter = MouseFilterEnum.Stop, ZIndex = 30 };
        AddChild(layer);
        layer.AddChild(new ColorRect { Size = new Vector2(W, H), Color = new Color(0, 0, 0, 0.75f), MouseFilter = MouseFilterEnum.Ignore });
        Card(layer, d, new Vector2(W / 2, H / 2), H * 0.82f, 0, 1.2f, true);
        layer.GuiInput += e => { if (e is InputEventMouseButton { Pressed: true }) layer.QueueFree(); };
    }

    void Standing(RivalPanels.Rival r, string mood, float h, Vector2 foot, double delay)
    {
        var t = RivalPanels.Portrait(r, mood);
        float w = Math.Min(h * t.GetWidth() / t.GetHeight(), W * 0.4f);
        h = w * t.GetHeight() / t.GetWidth();
        Appear(Pic(this, t, foot - new Vector2(w / 2, h), new Vector2(w, h)), delay, new Vector2(200, 0));
    }

    static Shader? faceShader;
    /// A rival's face in a gold circle (cut from the standing portrait), or your framed picture.
    Control Face(RivalPanels.Rival? r, float d)
    {
        if (r == null) return Cosmetics.Framed(Avatar, P.Frame, d);
        var root = new Control { Size = new Vector2(d, d), MouseFilter = MouseFilterEnum.Ignore };
        Texture2D t = RivalPanels.HasPortrait(r) ? RivalPanels.Portrait(r) : RivalPanels.Art(r);
        var region = RivalPanels.HasPortrait(r) ? new Vector4(0.25f, 0f, 0.5f, 0.5f * t.GetWidth() / t.GetHeight()) : new Vector4(0, 0, 1, 1);
        root.AddChild(new ColorRect { Size = new Vector2(d, d), Color = GoldC, Material = Kit.RoundedMat(1, 0.5f), MouseFilter = MouseFilterEnum.Ignore });
        var pic = Pic(root, t, new Vector2(5, 5), new Vector2(d - 10, d - 10));
        var m = new ShaderMaterial { Shader = faceShader ??= new Shader { Code = @"shader_type canvas_item;
uniform vec4 region;
void fragment() { float m = 1.0 - smoothstep(0.49, 0.5, length(UV - 0.5)); vec4 c = texture(TEXTURE, region.xy + UV * region.zw); COLOR = vec4(c.rgb, c.a * m); }" } };
        m.SetShaderParameter("region", region);
        pic.Material = m;
        return root;
    }

    void Confetti(double start, int n = 70)
    {
        var rng = new RandomNumberGenerator { Seed = 5 };
        for (int i = 0; i < n; i++)
        {
            var p = Pip(this, new Vector2(rng.RandfRange(0, W), -40), rng.RandfRange(8, 20));
            p.Modulate = new Color(1, 1, 1, rng.RandfRange(0.5f, 1f));
            float x = p.Position.X + rng.RandfRange(-120, 120), dur = rng.RandfRange(2.5f, 4.5f);
            var tw = CreateTween().SetParallel();
            double d = start + rng.RandfRange(0, 1.6f);
            tw.TweenProperty(p, "position", new Vector2(x, H + 40), dur).SetDelay(d);
            tw.TweenProperty(p, "rotation", p.Rotation + rng.RandfRange(-6, 6), dur).SetDelay(d);
        }
    }

    void Appear(Control c, double delay, Vector2? from = null)
    {
        c.Modulate = new Color(1, 1, 1, 0);
        var tw = CreateTween().SetParallel();
        tw.TweenProperty(c, "modulate:a", 1f, 0.4).SetDelay(delay);
        if (from is { } f) { var home = c.Position; c.Position += f; tw.TweenProperty(c, "position", home, 0.6).SetDelay(delay).SetTrans(Tween.TransitionType.Expo).SetEase(Tween.EaseType.Out); }
    }

    void Burst(double delay) => GetTree().CreateTimer(delay).Timeout += () =>
        CreateTween().TweenMethod(Callable.From<float>(v => bg.P("burst", v)), 1f, 0f, 1.4).SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);

    void Sfx(string name)
    {
        if (sfx == null) return;
        sfx.Stream = GD.Load<AudioStream>($"res://assets/audio/se/{name}.wav");
        sfx.Play();
    }

    protected override void OnFocus(int old, int now) { }

    protected override void OnChoose(int i)
    {
        if (i < 0 || i >= targets.Count) return;
        if (MatchOn && outcome != 2) Session.YouFirst = outcome == 0; // the loser of a match duel goes first
        // A rival's deck was won: its reveal comes after the rewards, then on to where this button points.
        string to = targets[i];
        if (Session.LastMat != null) { Session.AfterUnlock = to; to = "res://ui/MatUnlock.tscn"; }
        else if (Session.LastStarter != null) { Session.AfterUnlock = to; to = "res://ui/DeckUnlock.tscn"; }
        GetTree().CreateTimer(0.2).Timeout += () => Transition.Go(this, to);
    }
}
