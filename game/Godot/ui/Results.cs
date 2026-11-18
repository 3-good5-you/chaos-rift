using Godot;
using PoC.Engine;

namespace PoC.Game.UI;

/// Results after a duel (Chaos Rift, 2026-11-09, Victory Lab picks): you in the light (headline, numbers, the reward
/// cards dealt and flipped; click one to see it), the rival in the dark (sad when you win, happy when you lose).
/// Tournament champion = the podium, gauntlet cleared = the road through the rivals, a first challenge solve = the
/// tiles. The podium reveals every prize on it; the road's cards are behind SEE YOUR PRIZE. Records the duel (streak, Joey's ladder),
/// keeps the match score and grants the rewards into Session.Profile.
/// Screenshot args fake the incoming duel: `--case=win|loss|draw|match1|matchwin|matchloss --rival=N`.
public partial class Results : RiftMenu
{
    sealed record Reward(CardData Card, bool New);

    RivalPanels.Rival rival = null!;
    int outcome, scoreYou, scoreThem, prize = 1;   // prize = cards a win gives at this difficulty (Rewards.For)
    bool match, matchOver;
    readonly List<Reward> cards = new();
    readonly List<CardData> offer = new();   // Pack Duel: the cards to choose your prize from (empty once chosen)
    readonly List<Control> buttons = new();
    readonly List<string> targets = new();
    AudioStreamPlayer? sfx;
    double done;   // when the podium's prizes are all open (its buttons, SAVE REPLAY and achievement pop-ups wait for it)
    protected override double PopupAt => Math.Max(0.6, done);
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
            if (c == "gauntlet") { Session.Mode = GameMode.Gauntlet; Session.Run = Gauntlet.Climb(Tournament.Roster, new Rng(3)); Session.Run.Index = Session.Run.Order.Length - 1; Session.LastLp = 3450; }
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
                    Achievements.Bump(p, "champion");
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
            if (run.Cleared) Achievements.Bump(p, "gauntlet");
            if (run.Over)
            {
                // the run pays once, when it ends: the prize of the highest floor beaten (Gauntlet.Floors)
                var (dp, n) = run.Prize;
                prize = n; p.Dp += dp; Session.LastDp += dp;
                foreach (var (card, isNew) in Rewards.Roll(p, "tournament", Rewards.Pool("expansion"), n, false, Session.Rng, (n + 1) / 2)) cards.Add(new Reward(card, isNew));
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
            // PLAN_PACK_DUEL.md: a won duel shows 3 cards of your drafted deck, keep 1; a won best of 3 shows 6, keep 2.
            // Cards you don't own come first.
            prize = match ? 2 : 1;
            if (Won && !MatchOn)
            {
                var pool = (Session.Draft?.Cards(0) ?? pd.Main.Concat(pd.Extra)).DistinctBy(i => CardDb.Shared.Get(i).Name).ToList();
                Session.Rng.Shuffle(pool);
                offer.AddRange(pool.OrderBy(i => p.Count(i) > 0).Take(match ? 6 : 3).Select(CardDb.Shared.Get));
            }
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
        Appear(b, Math.Max(2.4, done));
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
        string sub = outcome switch { 1 => Lang.F("{0} DEFEATED", who), 0 => Lang.F("{0} WINS", who), _ => "NO ONE WINS" };
        if (Session.Mode != GameMode.Regular && Session.Tour == null) sub += "  ·  " + Modes.Of(Session.Mode).Name;
        if (Session.Tour is { } tour) sub += "  ·  " + Lang.Tr(tour.Place is int pl ? Tournament.PlaceNames[pl] : Tournament.RoundNames[Math.Min(2, tour.Round - 1)]);
        Appear(T(this, sub, Thin, 26, new Vector2(96, 210), new Vector2(1000, 34), HorizontalAlignment.Left, 0.8f), 0.4);
        Hair(this, new Vector2(98, 256), 200, GoldC); Pip(this, new Vector2(310, 257), 10);
        // the numbers
        var stats = new List<string>();
        if (Session.LastDp > 0) stats.Add(Lang.F("+{0} DP", Session.LastDp));
        if (match) stats.Add(Lang.F("MATCH  {0} – {1}", scoreYou, scoreThem));
        if (Session.Run is { } run && !run.Over) stats.Add(Lang.F("{0} LP CARRIED", run.Lp));
        if (Session.LastStarter is { } st) stats.Add(Lang.F("{0} UNLOCKED", st.Name.ToUpperInvariant()));
        for (int i = 0; i < stats.Count; i++) Appear(T(this, stats[i], Title, 30, new Vector2(98 + i * 330, 290), new Vector2(320, 40)), 0.6 + i * 0.12);
        // the cards (a match not yet over: dim backs for the cards still to win)
        int n = Math.Max(cards.Count, MatchOn ? prize : 0);
        // fit the row into the light side
        float gap = 34, ch = Math.Min(380, ((W * 0.56f + gap) / Math.Max(1, n) - gap) / 0.686f);
        if (n > 0) Deal(this, n, new Vector2(W * 0.31f, H * 0.6f), ch, gap, 0.8);
        else if (offer.Count > 0)
        {
            Control b = null!;
            b = Button(this, "CHOOSE YOUR PRIZE", new Vector2(W * 0.31f, H * 0.6f), () => ChooseOffer(b), 30);
            Appear(b, 0.8);
        }
        else Appear(T(this, MatchOn ? Lang.F("WIN THE MATCH TO EARN {0} CARDS", prize) : "NO CARDS THIS TIME", Thin, 28, new Vector2(98, H * 0.55f), new Vector2(1100, 36), HorizontalAlignment.Left, 0.7f), 0.8);
        // the rival on the dark side: sad when you win, happy when you lose
        string mood = outcome switch { 1 => "_sad", 0 => "_happy", _ => "" };
        Standing(rival, mood, H * 0.9f, new Vector2(W * 0.82f, H + 10), 0.2);
        Buttons(new Vector2(W * 0.14f, H - 80), Items.Length > 2 ? W * 0.2f : W * 0.26f);
    }

    // ---------------------------------------------------------------- tournament champion: the podium

    /// Menu Lab pick B (2026-11-17): the podium rises, then steps back and the prizes take the stage: the finalist's ace
    /// in the middle on a beam of light, the other cards either side, each turned over in turn (NewMark for new ones);
    /// then the DP. The buttons and any achievement pop-ups wait until every card is open.
    void Podium(Tournament t)
    {
        Rift(0.5f, 0.5f);
        // runner-up: the other finalist; third: the one you beat in the semifinal
        int si = Array.IndexOf(t.Winners[1], Tournament.You), qi = Array.IndexOf(t.Winners[0], Tournament.You);
        RivalPanels.Rival? second = si >= 0 && t.Winners[1][si ^ 1] is { } a ? RivalPanels.Of(Tournament.Get(a)) : rival;
        RivalPanels.Rival? third = qi >= 0 && t.Winners[0][qi ^ 1] is { } b ? RivalPanels.Of(Tournament.Get(b)) : null;
        var head = T(this, "CHAMPION", Title, 90, new Vector2(0, 40), new Vector2(W, 110), HorizontalAlignment.Center);
        head.Mat().P("lit", 1f);
        head.PivotOffset = new Vector2(W / 2, 0);
        Appear(head, 1.6);
        var podium = new List<Control>();
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
            foreach (var c in new Control[] { block, f, name }) { Appear(c, at, new Vector2(0, 260)); podium.Add(c); }
        }
        Burst(1.2);
        Confetti(1.3);

        // beat 2: the podium sinks away, the headline moves up
        double t0 = 3.4;
        var back = CreateTween().SetParallel();
        foreach (var c in podium)
        {
            back.TweenProperty(c, "modulate:a", 0f, 0.6).SetDelay(t0);
            back.TweenProperty(c, "position:y", c.Position.Y + 120, 0.6).SetDelay(t0).SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.InOut);
        }
        back.TweenProperty(head, "position:y", 10f, 0.5).SetDelay(t0);
        back.TweenProperty(head, "scale", new Vector2(0.7f, 0.7f), 0.5).SetDelay(t0);
        t0 += 0.6;

        // the prizes: the ace (the last card) in the middle on a beam, the rest either side; turned over in turn, the ace last
        double cue = t0 + 0.8;
        float y = H * 0.42f, ch = 250, step = ch * 0.686f + 22;
        var rest = cards.Take(Math.Max(0, cards.Count - 1)).ToList();
        int left = (rest.Count + 1) / 2;
        for (int i = 0; i < rest.Count; i++)
        {
            int side = i < left ? -1 : 1, k = i < left ? left - 1 - i : i - left;
            cue = Reveal(this, rest[i], new Vector2(W / 2 + side * (200 + step / 2 + k * step), y + 20), ch, t0 + 0.3 + i * 0.12, cue);
        }
        if (cards.Count > 0)
        {
            Appear(Shadow(this, new Vector2(W / 2 - 130, -100), new Vector2(260, H * 0.8f), 0.5f, new Color(1, 0.85f, 0.5f)), t0);
            cue = Reveal(this, cards[^1], new Vector2(W / 2, y), 400, t0, cue + 0.2);
        }

        // the DP under the cards; achievements this win finished slide in as the usual pop-up once it's all open (PopupAt)
        double end = cue + 0.4;
        var dp = T(this, Lang.F("+{0:N0} DP", 0), Title, 40, new Vector2(0, H * 0.7f), new Vector2(W, 50), HorizontalAlignment.Center);
        dp.Mat().P("lit", 1f);
        Appear(dp, end);
        CreateTween().TweenMethod(Callable.From<int>(n => dp.Text = Lang.F("+{0:N0} DP", n)), 0, Session.LastDp, 1.0).SetDelay(end).SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
        done = end + 1.2;
        Buttons(new Vector2(W * 0.5f - 170, H - 50), 340, at2: done);
    }

    /// One prize card: rises face down at `rise`, turns over at `cue` (a new one with the NEW reveal, NewMark);
    /// click it to see it large. Returns when the next card may turn.
    double Reveal(Control host, Reward rw, Vector2 at, float h, double rise, double cue)
    {
        var card = Card(host, rw.Card, at + new Vector2(0, H), h, 0.3f, 0.9f);
        var face = card.GetChildren().OfType<TextureRect>().Last();
        face.Texture = Cosmetics.Back(P.Back, 512);
        var tw = CreateTween().SetParallel();
        tw.TweenProperty(card, "position:y", at.Y - h / 2, 0.55).SetDelay(rise).SetTrans(Tween.TransitionType.Expo).SetEase(Tween.EaseType.Out);
        tw.TweenProperty(card, "rotation", 0f, 0.55).SetDelay(rise).SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
        Click(card, () => Preview(rw.Card));
        var flip = CreateTween();
        flip.TweenInterval(cue);
        if (rw.New) { flip.TweenCallback(Callable.From(() => NewMark.Reveal(card, face, CardArt.Large(rw.Card)))); return cue + 0.9; }
        flip.TweenProperty(card, "scale:x", 0f, 0.1);
        flip.TweenCallback(Callable.From(() => { face.Texture = CardArt.Large(rw.Card); Sfx("card_open"); }));
        flip.TweenProperty(card, "scale:x", 1f, 0.12);
        return cue + 0.3;
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
            var r = RivalPanels.Of(Tournament.Get(run.Order[i].Rival));
            float x = Mathf.Lerp(x0, x1, (i + 1f) / n) - (i == n - 1 ? 40 : 0);
            var f = Face(r, 130); f.Position = new Vector2(x - 65, y - 65); f.Modulate = new Color(0.45f, 0.45f, 0.5f); AddChild(f);
            T(this, r.Name.ToUpperInvariant(), Title, 16, new Vector2(x - 100, y + 80), new Vector2(200, 26), HorizontalAlignment.Center);
            int left = 8000 - (8000 - run.Lp) * (i + 1) / n;   // ponytail: only the final LP is kept; the steps between are drawn evenly
            GetTree().CreateTimer(0.85 + i * 0.55).Timeout += () =>
            {
                CreateTween().TweenProperty(f, "modulate", Colors.White, 0.2);
                var p = Pip(this, new Vector2(x, y - 90), 22);
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
        Appear(T(this, Lang.F("EIGHT RIVALS, ONE DECK  ·  +{0:N0} DP  ·  {1} CARDS", Session.LastDp, cards.Count), Thin, 24, new Vector2(0, H * 0.1f + 100), new Vector2(W, 32), HorizontalAlignment.Center), 0.7 + 0.55 * n);
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
        Appear(T(this, $"{idx:00}  ·  {Lang.Tr(z.Name)}", Title, 34, new Vector2(0, below), new Vector2(W, 44), HorizontalAlignment.Center), 1.1);
        Appear(T(this, Lang.F("FIRST SOLVE  ·  +{0} DP  ·  {1} OF {2}", Session.LastDp, P.Solved.Count, count), Thin, 24, new Vector2(0, below + 54), new Vector2(W, 32), HorizontalAlignment.Center, 0.8f), 1.3);
        Buttons(new Vector2(W / 2 - 230, H - 70), 460);
    }

    // ---------------------------------------------------------------- shared pieces

    (string Label, string Target)[] Items => MatchOn
        ? new[] { ("NEXT DUEL", "res://ui/field/DuelField.tscn") }
        : Session.Tour != null ? new[] { ("MAIN MENU", "res://ui/MainMenu.tscn"), ("BRACKET", "res://ui/Tournament.tscn") }
        : Session.Puzzle != null ? new[] { ("CHALLENGES", "res://ui/ChallengeSelect.tscn"), ("TRY AGAIN", "res://ui/field/DuelField.tscn") }
        : Session.Run is { Over: false } ? new[] { ("MAIN MENU", "res://ui/MainMenu.tscn"), ("NEXT FLOOR", "res://ui/Gauntlet.tscn") }
        : Session.Draft != null ? new[] { ("MAIN MENU", "res://ui/MainMenu.tscn"), ("MODES", "res://ui/DuelModes.tscn"), ("REMATCH", "res://ui/PackDraft.tscn") }
        : Session.Mode != GameMode.Regular ? new[] { ("MAIN MENU", "res://ui/MainMenu.tscn"), ("MODES", "res://ui/DuelModes.tscn") }
        : new[] { ("MAIN MENU", "res://ui/MainMenu.tscn"), ("CONTINUE", "res://ui/RivalSelect.tscn") };

    /// The way on: diamond buttons, left to right (or top to bottom), the last one the main one.
    void Buttons(Vector2 at, float step, bool vertical = false, double? at2 = null)
    {
        var items = Items;
        for (int i = 0; i < items.Length; i++)
        {
            int k = i;
            var pos = vertical ? at + new Vector2(0, i * 110) : at + new Vector2(i * step, 0);
            var b = Button(this, items[i].Label, pos, () => Choose(k), i == items.Length - 1 ? 30 : 24);
            Appear(b, at2 ?? Math.Min(3.0, Math.Max(1.4, RevealEnd - 0.3)));
            buttons.Add(b);
            targets.Add(items[i].Target);
        }
    }

    void PrizeButton(Vector2 at)
    {
        if (cards.Count == 0) return;
        Appear(Button(this, "SEE YOUR PRIZE", at, ShowPrize, 26), 2.2);
    }

    /// Pack Duel prize: pick `prize` of the offered cards from your drafted deck; KEEP adds them and deals them here.
    void ChooseOffer(Control button)
    {
        var layer = new Control { Size = new Vector2(W, H), MouseFilter = MouseFilterEnum.Stop, ZIndex = 20 };
        layer.SetMeta("dark", true);
        AddChild(layer);
        layer.AddChild(new ColorRect { Size = new Vector2(W, H), Color = new Color(0.02f, 0.02f, 0.03f, 0.9f), MouseFilter = MouseFilterEnum.Ignore });
        Put(layer, "CHOOSE YOUR PRIZE", Title, 56, GoldLight, new Vector2(0, 90), new Vector2(W, 70), HorizontalAlignment.Center);
        Put(layer, Lang.F("KEEP {0} OF {1}  ·  FROM YOUR DRAFTED DECK", prize, offer.Count), Thin, 22, Mist, new Vector2(0, 170), new Vector2(W, 30), HorizontalAlignment.Center);
        var picked = new List<CardData>();
        var row = new Control { MouseFilter = MouseFilterEnum.Ignore };
        layer.AddChild(row);
        void Draw()
        {
            foreach (var c in row.GetChildren()) c.QueueFree();
            float h = offer.Count > 3 ? 300 : 380, w = h * 0.686f, gap = 30, x0 = W / 2 - (offer.Count * (w + gap) - gap) / 2;
            foreach (var c in offer)
            {
                bool on = picked.Contains(c);
                var at = new Vector2(x0 + w / 2, H * 0.52f - (on ? 22 : 0));
                x0 += w + gap;
                var card = Card(row, c, at, h, 0, on ? 1.3f : 0.7f);
                if (!on && picked.Count == prize) card.Modulate = new Color(1, 1, 1, 0.45f);
                if (on) Pip(row, at - new Vector2(0, h / 2 + 18), 10);
                if (Session.Profile.Count(c.Idx) == 0) Put(row, "NOT OWNED", Title, 16, GoldLight, at + new Vector2(-w / 2, h / 2 + 10), new Vector2(w, 24), HorizontalAlignment.Center);
                Click(card, () =>
                {
                    if (!picked.Remove(c)) { if (picked.Count >= prize) picked.RemoveAt(0); picked.Add(c); }
                    UiSound(false);
                    Draw();
                });
            }
        }
        Draw();
        Button(layer, "KEEP", new Vector2(W / 2, H - 90), () =>
        {
            if (picked.Count != Math.Min(prize, offer.Count)) { Flash(Lang.F("Keep {0} cards", prize)); return; }
            Grant(picked);
            layer.QueueFree();
            button.Visible = false;
            float ch = Math.Min(380, ((W * 0.56f + 34) / cards.Count - 34) / 0.686f);
            Deal(this, cards.Count, new Vector2(W * 0.31f, H * 0.6f), ch, 34, 0);
        }, 28);
    }

    void Grant(IEnumerable<CardData> keep)
    {
        var p = Session.Profile;
        foreach (var c in keep)
        {
            bool isNew = p.Count(c.Idx) == 0;
            p.Owned[c.Idx] = Math.Min(255, p.Count(c.Idx) + 1);
            if (isNew) p.Fresh.Add(c.Idx);
            cards.Add(new Reward(c, isNew));
        }
        offer.Clear();
        Session.Save();
    }

    /// The reward cards dealt over the screen; click one to see it large.
    void ShowPrize()
    {
        var layer = new Control { Size = new Vector2(W, H), MouseFilter = MouseFilterEnum.Stop, ZIndex = 20 };
        layer.SetMeta("dark", true);
        AddChild(layer);
        layer.AddChild(new ColorRect { Size = new Vector2(W, H), Color = new Color(0.02f, 0.02f, 0.03f, 0.9f), MouseFilter = MouseFilterEnum.Ignore });
        Put(layer, "YOUR PRIZE", Title, 56, GoldLight, new Vector2(0, 90), new Vector2(W, 70), HorizontalAlignment.Center);
        Put(layer, Lang.F("{0} CARDS  ·  CLICK ONE TO SEE IT", cards.Count), Thin, 22, Mist, new Vector2(0, 170), new Vector2(W, 30), HorizontalAlignment.Center);
        Deal(layer, cards.Count, new Vector2(W / 2, H * 0.5f), cards.Count > 4 ? 330 : 400, 40, 0.1);
        Button(layer, "CLOSE", new Vector2(W / 2, H - 90), () => layer.QueueFree(), 26);
    }

    /// `n` cards dealt face down along a row, then flipped one by one (cards beyond the rewards stay dim backs);
    /// the new ones get the NEW reveal (NewMark); click a card to see it large.
    void Deal(Control host, int n, Vector2 centre, float h, float gap, double start)
    {
        float w = h * 0.686f, row = n * (w + gap) - gap;
        double cue = start + 0.8;
        for (int i = 0; i < n; i++)
        {
            var at = new Vector2(centre.X - row / 2 + i * (w + gap) + w / 2, centre.Y);
            if (i < cards.Count) { cue = Reveal(host, cards[i], at, h, start + i * 0.12, cue); continue; }
            var card = Card(host, CardDb.Shared.Get(0), at + new Vector2(0, H), h, 0.3f, 0.9f);
            card.GetChildren().OfType<TextureRect>().Last().Texture = Cosmetics.Back(P.Back, 512);
            card.Modulate = new Color(0.35f, 0.35f, 0.4f);
            var tw = CreateTween().SetParallel();
            tw.TweenProperty(card, "position:y", at.Y - h / 2, 0.55).SetDelay(start + i * 0.12).SetTrans(Tween.TransitionType.Expo).SetEase(Tween.EaseType.Out);
            tw.TweenProperty(card, "rotation", 0f, 0.55).SetDelay(start + i * 0.12).SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
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
        var region = RivalPanels.Face(r);   // per-duelist face box
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
        if (offer.Count > 0) Grant(offer.Take(prize).ToList());   // left without choosing: the first ones (not owned first)
        if (MatchOn && outcome != 2) Session.YouFirst = outcome == 0; // the loser of a match duel goes first
        // A rival's deck was won: its reveal comes after the rewards, then on to where this button points.
        string to = targets[i];
        if (Session.LastStarter != null) { Session.AfterUnlock = to; to = "res://ui/DeckUnlock.tscn"; }
        GetTree().CreateTimer(0.2).Timeout += () => Transition.Go(this, to);
    }
}
