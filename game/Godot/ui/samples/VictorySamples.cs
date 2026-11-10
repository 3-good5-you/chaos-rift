using Godot;
using PoC.Engine;

namespace PoC.Game.UI;

/// Samples (2026-11-09): the Results (victory) screen in the Chaos Rift style, and the unlock moments that had none.
/// `-- --show=` results1 | results2 | defeat | champion | gauntlet | legend | challenge | mat
/// round 2 (champion, gauntlet, challenge rejected): champion2 | champion3 | gauntlet2 | gauntlet3 | challenge2 | challenge3
/// Everything here is demo data: nothing is paid into the save.
public partial class VictorySamples : RiftMenu
{
    string show = "results1";
    static readonly string[] Prize = { "Blue-Eyes White Dragon", "Mirror Force", "Monster Reborn", "Gemini Elf" };
    static readonly Color Crimson = new(0.85f, 0.3f, 0.26f);

    protected override string? MusicPath => null;

    /// Set by the Victory Lab before the sample is added (else `--show=`); Replay: what CONTINUE-type buttons do there.
    public string? Show;
    public System.Action? Replay;

    protected override void Build()
    {
        foreach (var a in OS.GetCmdlineUserArgs()) if (a.StartsWith("--show=")) show = a[7..];
        if (Show != null) show = Show;
        if (OS.GetCmdlineUserArgs().Contains("--prize")) GetTree().CreateTimer(1.0).Timeout += ShowPrize;   // screenshots of the prize view
        if (Session.Picks.Length == 0) Session.RollPicks();
        switch (show)
        {
            case "results1": Split(true); break;
            case "defeat": Split(false); break;
            case "results2": Stage(); break;
            case "champion": Champion(); break;
            case "gauntlet": GauntletClear(); break;
            case "legend": Legend(); break;
            case "challenge": Solved(); break;
            case "mat": NewMat(); break;
            case "mat2": MatReveal(2); break;
            case "mat3": MatReveal(3); break;
            case "mat4": MatReveal(4); break;
            case "mat5": MatReveal(5); break;
            case "mat6": MatReveal(6); break;
            case "champion2": Podium(); break;
            case "champion3": Trophy(); break;
            case "gauntlet2": Road(); break;
            case "gauntlet3": Ring(); break;
            case "challenge2": Sealed(); break;
            case "challenge3": Tiles(); break;
        }
    }

    // ---------------------------------------------------------------- shared

    /// A sample's button: the prize buttons deal the reward cards; the rest replay the sample (in the lab).
    void Press(string label)
    {
        if (label.Contains("PRIZE") || label.Contains("CARDS")) { ShowPrize(); return; }
        if (Replay != null) Replay(); else Flash(label);
    }

    static readonly string[] Prizes = { "Blue-Eyes White Dragon", "Mirror Force", "Monster Reborn", "Gemini Elf", "Dark Magician", "Summoned Skull" };

    /// The reward cards dealt over the sample: 6 for a tournament, 4 for a gauntlet, 3 otherwise. Click one to see it large.
    void ShowPrize()
    {
        int n = show.StartsWith("champion") ? 6 : show.StartsWith("gauntlet") ? 4 : 3;
        var layer = new Control { Size = new Vector2(W, H), MouseFilter = MouseFilterEnum.Stop, ZIndex = 20 };
        AddChild(layer);
        layer.AddChild(new ColorRect { Size = new Vector2(W, H), Color = new Color(0.02f, 0.02f, 0.03f, 0.88f), MouseFilter = MouseFilterEnum.Ignore });
        var head = Put(layer, "YOUR PRIZE", Title, 56, GoldLight, new Vector2(0, 90), new Vector2(W, 70), HorizontalAlignment.Center);
        Put(layer, $"{n} CARDS  ·  CLICK ONE TO SEE IT", Thin, 22, Mist, new Vector2(0, 170), new Vector2(W, 30), HorizontalAlignment.Center);
        Deal(layer, Prizes.Take(n).ToList(), new Vector2(W / 2, H * 0.5f), n > 4 ? 330 : 400, 40, 0.1, Enumerable.Range(0, n).Select(i => i < n / 2).ToArray());
        Button(layer, "CLOSE", new Vector2(W / 2, H - 90), () => layer.QueueFree(), 26);
    }

    /// One card large over everything; any click closes it.
    void Preview(CardData d)
    {
        var layer = new Control { Size = new Vector2(W, H), MouseFilter = MouseFilterEnum.Stop, ZIndex = 30 };
        AddChild(layer);
        layer.AddChild(new ColorRect { Size = new Vector2(W, H), Color = new Color(0, 0, 0, 0.75f), MouseFilter = MouseFilterEnum.Ignore });
        Card(layer, d, new Vector2(W / 2, H / 2), H * 0.82f, 0, 1.2f, true);
        layer.GuiInput += e => { if (e is InputEventMouseButton { Pressed: true }) layer.QueueFree(); };
    }

    static RivalPanels.Rival Kaiba => RivalPanels.All[1];

    TextureRect Standing(RivalPanels.Rival r, string mood, float h, Vector2 foot)
    {
        var t = RivalPanels.HasPortrait(r) ? RivalPanels.Portrait(r, mood) : RivalPanels.Art(r);
        float w = h * t.GetWidth() / t.GetHeight();
        return Pic(this, t, foot - new Vector2(w / 2, h), new Vector2(w, h));
    }

    /// Reward cards dealt face down along a row, then flipped one by one; NEW in gold over the new ones.
    void Deal(Control host, IList<string> names, Vector2 centre, float h, float gap, double start, bool[]? fresh = null)
    {
        float w = h * 0.686f, row = names.Count * (w + gap) - gap;
        var back = Cosmetics.Back(P.Back, 512);
        for (int i = 0; i < names.Count; i++)
        {
            var at = new Vector2(centre.X - row / 2 + i * (w + gap) + w / 2, centre.Y);
            var data = Db.Named(names[i]);
            var card = Card(host, data, at + new Vector2(0, H), h, 0.3f, 0.9f);
            Click(card, () => Preview(data));
            var face = card.GetChildren().OfType<TextureRect>().Last();
            var real = face.Texture; face.Texture = back;
            var tw = CreateTween().SetParallel();
            tw.TweenProperty(card, "position:y", at.Y - h / 2, 0.55).SetDelay(start + i * 0.12).SetTrans(Tween.TransitionType.Expo).SetEase(Tween.EaseType.Out);
            tw.TweenProperty(card, "rotation", 0f, 0.55).SetDelay(start + i * 0.12).SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
            var flip = CreateTween();
            flip.TweenInterval(start + 0.8 + i * 0.3);
            flip.TweenProperty(card, "scale:x", 0f, 0.1);
            flip.TweenCallback(Callable.From(() => face.Texture = real));
            flip.TweenProperty(card, "scale:x", 1f, 0.12);
            if (fresh?[i] ?? i % 2 == 0)
            {
                var tag = T(host, "NEW", Title, 20, new Vector2(at.X - 60, at.Y + h / 2 + 12), new Vector2(120, 26), HorizontalAlignment.Center);
                tag.Mat().P("lit", 1f);
                tag.Modulate = new Color(1, 1, 1, 0);
                CreateTween().TweenProperty(tag, "modulate:a", 1f, 0.3).SetDelay(start + 1.0 + i * 0.3);
            }
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

    // ---------------------------------------------------------------- Results 1: the rift splits you and the rival

    /// You in the light (the word, the numbers, the cards), the rival in the dark, standing beaten (or smug).
    void Split(bool won)
    {
        Rift(0.66f, 0.6f);
        var head = T(this, won ? "VICTORY" : "DEFEAT", Title, 110, new Vector2(90, 70), new Vector2(1100, 130));
        if (won) head.Mat().P("lit", 1f);
        Appear(head, 0.1, new Vector2(-60, 0));
        var sub = T(this, won ? "KAIBA DEFEATED  ·  TURN 9" : "KAIBA WINS  ·  TURN 11", Thin, 26, new Vector2(96, 210), new Vector2(900, 34), HorizontalAlignment.Left, 0.8f);
        Appear(sub, 0.4);
        Hair(this, new Vector2(98, 256), 200, GoldC); Pip(this, new Vector2(310, 257), 10);
        // numbers: DP, streak, rival level
        string[] stat = { won ? "+120 DP" : "+20 DP", won ? "STREAK  3" : "STREAK  0", "RIVAL LEVEL  4" };
        for (int i = 0; i < 3; i++) Appear(T(this, stat[i], Title, 30, new Vector2(98 + i * 300, 290), new Vector2(280, 40)), 0.6 + i * 0.12);
        if (won) Deal(this, Prize.Take(3).ToList(), new Vector2(W * 0.3f, H * 0.6f), 380, 40, 0.8);
        else Appear(T(this, "NO CARDS THIS TIME", Thin, 28, new Vector2(98, H * 0.55f), new Vector2(900, 36), HorizontalAlignment.Left, 0.7f), 0.8);
        Standing(Kaiba, won ? "_sad" : "_happy", H * 0.9f, new Vector2(W * 0.82f, H + 10));
        Button(this, "MAIN MENU", new Vector2(W * 0.14f, H - 80), () => Press("MAIN MENU"), 24);
        Button(this, "CONTINUE", new Vector2(W * 0.4f, H - 80), () => Press("CONTINUE"), 30);
    }

    // ---------------------------------------------------------------- Results 2: the cards take the stage

    void Stage()
    {
        Rift(0.14f, 0.1f);
        Standing(Kaiba, "_sad", H * 0.8f, new Vector2(W * 0.86f, H + 10)).Modulate = new Color(1, 1, 1, 0.35f);
        var head = T(this, "VICTORY", Title, 64, new Vector2(0, 50), new Vector2(W, 80), HorizontalAlignment.Center);
        head.Mat().P("lit", 1f);
        Appear(head, 0.1);
        Appear(T(this, "KAIBA DEFEATED  ·  TURN 9", Thin, 22, new Vector2(0, 132), new Vector2(W, 30), HorizontalAlignment.Center, 0.8f), 0.3);
        Deal(this, Prize, new Vector2(W / 2, H * 0.5f), 400, 50, 0.5, new[] { true, false, true, false });
        Appear(T(this, "+120 DP      ·      STREAK 3      ·      RIVAL LEVEL 4", Title, 26, new Vector2(0, H * 0.79f), new Vector2(W, 36), HorizontalAlignment.Center), 2.2);
        Button(this, "MAIN MENU", new Vector2(W / 2 - 220, H - 80), () => Press("MAIN MENU"), 24);
        Button(this, "CONTINUE", new Vector2(W / 2 + 220, H - 80), () => Press("CONTINUE"), 30);
    }

    // ---------------------------------------------------------------- tournament champion

    /// The bracket on the dark side, your path lit in gold round by round; then CHAMPION, your picture, the prize.
    void Champion()
    {
        Rift(0.36f, 0.3f);
        string[] names = { Settings.PlayerName.ToUpperInvariant(), "REX", "MAKO", "WEEVIL", "MAI", "BANDIT KEITH", "BAKURA", "KAIBA" };
        float x0 = W * 0.42f, colW = W * 0.15f, top = H * 0.16f, rowH = H * 0.085f;
        var paint = new DrawBox { Size = new Vector2(W, H), MouseFilter = MouseFilterEnum.Ignore };
        int lit = 0;
        paint.Paint = b =>
        {
            for (int round = 0; round < 3; round++)
            {
                int n = 8 >> round; float step = rowH * (1 << round);
                for (int i = 0; i < n; i++)
                {
                    var a = new Vector2(x0 + round * colW + 150, top + step * (i + 0.5f) + rowH * ((1 << round) - 1) * 0);
                    float y = top + step * i + step / 2;
                    bool mine = i == 0;
                    var col = mine && round < lit ? GoldLight : Mist with { A = 0.35f };
                    float w2 = mine && round < lit ? 4 : 2;
                    b.DrawLine(new Vector2(x0 + round * colW + 150, y), new Vector2(x0 + (round + 1) * colW - 20, y), col, w2);
                    if (i % 2 == 0) b.DrawLine(new Vector2(x0 + (round + 1) * colW - 20, y), new Vector2(x0 + (round + 1) * colW - 20, y + step), col, w2);
                }
            }
        };
        AddChild(paint);
        for (int i = 0; i < 8; i++) Put(this, names[i], Title, 20, i == 0 ? GoldLight : Mist, new Vector2(x0 - 30, top + rowH * i + rowH / 2 - 14), new Vector2(190, 28));
        for (int r = 1; r <= 3; r++)
        {
            int rr = r;
            GetTree().CreateTimer(0.5 + r * 0.5).Timeout += () => { lit = rr; paint.QueueRedraw(); };
        }
        Put(this, "CHAMPION", Title, 24, GoldLight, new Vector2(x0 + 3 * colW, top + rowH * 4 - 14), new Vector2(300, 30));
        Burst(2.1);
        var f = Cosmetics.Framed(Avatar, P.Frame, 230); f.Position = new Vector2(W * 0.14f - 115, H * 0.18f); AddChild(f);
        Appear(f, 2.1, new Vector2(0, 40));
        var head = T(this, "CHAMPION", Title, 84, new Vector2(30, H * 0.42f), new Vector2(W * 0.3f, 100), HorizontalAlignment.Center);
        head.Mat().P("lit", 1f);
        Appear(head, 2.3);
        Appear(T(this, "DUELIST KINGDOM CUP  ·  +1,500 DP", Thin, 22, new Vector2(30, H * 0.52f), new Vector2(W * 0.3f, 30), HorizontalAlignment.Center), 2.5);
        Deal(this, Prize.Take(3).ToList(), new Vector2(W * 0.16f, H * 0.75f), 220, 24, 2.6);
        Button(this, "BRACKET", new Vector2(W * 0.72f, H - 70), () => Press("BRACKET"), 24);
    }

    // ---------------------------------------------------------------- gauntlet cleared

    /// The four rivals stand in a row; a gold seal stamps onto each, one after another; then GAUNTLET CLEARED.
    void GauntletClear()
    {
        Rift(0.5f, 0.5f);
        var head = T(this, "GAUNTLET CLEARED", Title, 70, new Vector2(0, 50), new Vector2(W, 90), HorizontalAlignment.Center);
        head.Mat().P("lit", 1f);
        Appear(head, 2.6);
        var order = RivalPanels.All.OrderBy(r => r.File == "kaiba" ? 3 : r.File == "yugi" ? 2 : r.File == "joey" ? 0 : 1).ToArray();
        for (int i = 0; i < order.Length; i++)
        {
            float x = W * (0.16f + i * 0.225f);
            var r = order[i];
            var pic = Standing(r, "_sad", H * 0.5f, new Vector2(x, H * 0.68f));
            Appear(pic, 0.1 + i * 0.1, new Vector2(0, 40));
            T(this, r.Name, Title, 26, new Vector2(x - 150, H * 0.7f), new Vector2(300, 34), HorizontalAlignment.Center);
            // the seal
            var seal = Pip(this, new Vector2(x, H * 0.79f), 44);
            seal.Scale = new Vector2(2.6f, 2.6f); seal.Modulate = new Color(1, 1, 1, 0);
            var tw = CreateTween().SetParallel();
            double d = 0.8 + i * 0.4;
            tw.TweenProperty(seal, "scale", Vector2.One, 0.22).SetDelay(d).SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
            tw.TweenProperty(seal, "modulate:a", 1f, 0.1).SetDelay(d);
            tw.TweenProperty(pic, "modulate", new Color(0.55f, 0.55f, 0.6f), 0.3).SetDelay(d);
            Appear(T(this, $"DUEL {i + 1}  ·  DEFEATED", Thin, 16, new Vector2(x - 150, H * 0.79f + 36), new Vector2(300, 24), HorizontalAlignment.Center), d);
        }
        Burst(2.6);
        Appear(T(this, "3,450 LP LEFT  ·  +1,000 DP  ·  4 CARDS", Title, 26, new Vector2(0, H * 0.13f), new Vector2(W, 36), HorizontalAlignment.Center), 2.9);
        Button(this, "SEE YOUR CARDS", new Vector2(W / 2, H - 60), () => Press("SEE YOUR CARDS"), 26);
    }

    // ---------------------------------------------------------------- Legend beaten

    /// Their strongest card stands in the dark, cracks across, and falls apart; LEGEND FALLEN.
    void Legend()
    {
        Rift(0.3f, 0.24f);
        Standing(Kaiba, "_sad", H * 0.95f, new Vector2(W * 0.8f, H + 10)).Modulate = new Color(1, 1, 1, 0.5f);
        var ace = Card(this, Db.Named("Blue-Eyes Ultimate Dragon"), new Vector2(W * 0.56f, H * 0.46f), 560, 0, 1.2f, true);
        // two halves split away along a crack
        var crack = new Line2D { Points = new[] { new Vector2(-10, 300), new Vector2(120, 250), new Vector2(200, 300), new Vector2(300, 240), new Vector2(400, 280) }, Width = 6, DefaultColor = GoldLight, Modulate = new Color(1, 1, 1, 0) };
        ace.AddChild(crack);
        var tw = CreateTween();
        tw.TweenInterval(1.0);
        tw.TweenProperty(crack, "modulate:a", 1f, 0.15);
        tw.TweenInterval(0.5);
        tw.TweenProperty(ace, "modulate", new Color(0.5f, 0.5f, 0.55f, 0.5f), 0.5);
        tw.Parallel().TweenProperty(ace, "rotation", 0.08f, 0.5);
        tw.Parallel().TweenProperty(ace, "position:y", ace.Position.Y + 60, 0.5);
        Burst(1.5);
        var head = T(this, "LEGEND FALLEN", Title, 76, new Vector2(80, H * 0.24f), new Vector2(W * 0.45f, 96));
        head.Mat().P("lit", 1f);
        Appear(head, 1.6);
        Appear(T(this, "KAIBA AT FULL STRENGTH  ·  LEVEL 7", Thin, 22, new Vector2(84, H * 0.24f + 104), new Vector2(W * 0.45f, 30), HorizontalAlignment.Left, 0.8f), 1.8);
        Deal(this, Prize.Take(3).ToList(), new Vector2(W * 0.19f, H * 0.66f), 240, 24, 2.0);
        Appear(T(this, "+600 DP", Title, 32, new Vector2(84, H * 0.86f), new Vector2(400, 40)), 2.6);
        Button(this, "CONTINUE", new Vector2(W * 0.82f, H - 70), () => Press("CONTINUE"), 28);
    }

    // ---------------------------------------------------------------- first challenge solve

    /// The puzzle's number large, SOLVED stamped across it, then the eight diamonds of the challenge list fill.
    void Solved()
    {
        Rift(0.5f, 0.46f);
        var num = T(this, "07", Title, 260, new Vector2(0, H * 0.12f), new Vector2(W, 300), HorizontalAlignment.Center, 0.25f);
        Appear(num, 0.1);
        var stamp = T(this, "SOLVED", Title, 110, new Vector2(0, H * 0.27f), new Vector2(W, 130), HorizontalAlignment.Center);
        stamp.Mat().P("lit", 1f);
        stamp.PivotOffset = new Vector2(W / 2, 65); stamp.Rotation = -0.08f; stamp.Scale = new Vector2(2.2f, 2.2f); stamp.Modulate = new Color(1, 1, 1, 0);
        var tw = CreateTween().SetParallel();
        tw.TweenProperty(stamp, "scale", Vector2.One, 0.22).SetDelay(0.8).SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
        tw.TweenProperty(stamp, "modulate:a", 1f, 0.1).SetDelay(0.8);
        Burst(0.85);
        Appear(T(this, "THE SET CARD  ·  FIRST SOLVE  ·  +200 DP", Thin, 24, new Vector2(0, H * 0.5f), new Vector2(W, 32), HorizontalAlignment.Center), 1.2);
        for (int i = 0; i < 8; i++)
        {
            var p = Pip(this, new Vector2(W / 2 + (i - 3.5f) * 70, H * 0.64f), i < 5 ? 26 : 18);
            p.Modulate = i < 4 ? Colors.White : i == 4 ? new Color(1, 1, 1, 0) : new Color(1, 1, 1, 0.25f);
            if (i == 4) CreateTween().TweenProperty(p, "modulate:a", 1f, 0.3).SetDelay(1.6);
        }
        Appear(T(this, "5 OF 8 SOLVED", Title, 26, new Vector2(0, H * 0.69f), new Vector2(W, 34), HorizontalAlignment.Center), 1.8);
        Button(this, "CHALLENGES", new Vector2(W / 2 - 200, H - 80), () => Press("CHALLENGES"), 24);
        Button(this, "NEXT PUZZLE", new Vector2(W / 2 + 200, H - 80), () => Press("NEXT PUZZLE"), 28);
    }

    // ---------------------------------------------------------------- new playmat (a cosmetic reward)

    /// The mat slides up out of the rift, a light sweeps across it; its name and EQUIP.
    void NewMat()
    {
        Rift(0.5f, 0.5f);
        var tex = GD.Load<Texture2D>("res://assets/mats/obsidian.jpg");
        float w = W * 0.56f, h = w * tex.GetHeight() / tex.GetWidth();
        var holder = new Control { Position = new Vector2(W / 2 - w / 2, H * 0.12f), Size = new Vector2(w, h), PivotOffset = new Vector2(w / 2, h), MouseFilter = MouseFilterEnum.Ignore };
        AddChild(holder);
        Shadow(holder, Vector2.Zero, new Vector2(w, h), 0.7f, new Color(0, 0, 0), new Vector2(0, 30));
        var pic = Pic(holder, tex, Vector2.Zero, new Vector2(w, h));
        pic.Material = Kit.RoundedMat(w / h, 0.04f);
        var sweep = new ColorRect { Size = new Vector2(w * 0.25f, h * 1.4f), Position = new Vector2(-w * 0.4f, -h * 0.2f), Rotation = 0.35f, Color = new Color(1, 0.95f, 0.8f, 0.22f), MouseFilter = MouseFilterEnum.Ignore };
        holder.ClipContents = true;
        holder.AddChild(sweep);
        holder.Scale = new Vector2(0.9f, 0.05f); holder.Modulate = new Color(1, 1, 1, 0);
        var tw = CreateTween();
        tw.TweenInterval(0.3);
        tw.TweenProperty(holder, "modulate:a", 1f, 0.2);
        tw.Parallel().TweenProperty(holder, "scale", Vector2.One, 0.8).SetTrans(Tween.TransitionType.Expo).SetEase(Tween.EaseType.Out);
        tw.TweenProperty(sweep, "position:x", w * 1.2f, 0.9).SetTrans(Tween.TransitionType.Sine);
        Burst(0.4);
        var head = T(this, "NEW PLAYMAT", Title, 64, new Vector2(0, H * 0.12f + h + 40), new Vector2(W, 80), HorizontalAlignment.Center);
        head.Mat().P("lit", 1f);
        Appear(head, 1.3);
        Appear(T(this, "OBSIDIAN  ·  CHAMPION'S PRIZE", Title, 28, new Vector2(0, H * 0.12f + h + 124), new Vector2(W, 36), HorizontalAlignment.Center), 1.5);
        Button(this, "EQUIP", new Vector2(W / 2 - 200, H - 70), () => Press("EQUIP"), 28);
        Button(this, "CONTINUE", new Vector2(W / 2 + 200, H - 70), () => Press("CONTINUE"), 24);
    }

    // ---------------------------------------------------------------- round 2: shared bits

    static Shader? faceShader;
    /// A rival's face in a circle (cut from the standing portrait), or your framed picture.
    Control Face(RivalPanels.Rival? r, float d)
    {
        if (r == null) return Cosmetics.Framed(Avatar, P.Frame, d);
        var root = new Control { Size = new Vector2(d, d), MouseFilter = MouseFilterEnum.Ignore };
        Texture2D t = RivalPanels.HasPortrait(r) ? RivalPanels.Portrait(r) : RivalPanels.Art(r);
        var region = RivalPanels.HasPortrait(r) ? new Vector4(0.25f, 0f, 0.5f, 0.5f * t.GetWidth() / t.GetHeight()) : new Vector4(0, 0, 1, 1);
        var ring = new ColorRect { Size = new Vector2(d, d), Color = GoldC, Material = Kit.RoundedMat(1, 0.5f), MouseFilter = MouseFilterEnum.Ignore };
        root.AddChild(ring);
        var pic = Pic(root, t, new Vector2(5, 5), new Vector2(d - 10, d - 10));
        var m = new ShaderMaterial { Shader = faceShader ??= new Shader { Code = @"shader_type canvas_item;
uniform vec4 region;
void fragment() { float m = 1.0 - smoothstep(0.49, 0.5, length(UV - 0.5)); vec4 c = texture(TEXTURE, region.xy + UV * region.zw); COLOR = vec4(c.rgb, c.a * m); }" } };
        m.SetShaderParameter("region", region);
        pic.Material = m;
        return root;
    }

    /// Gold diamonds drifting down over the whole screen, for a while.
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

    // ---------------------------------------------------------------- tournament 2: the podium

    /// Three steps: you on the top one in the middle, the runner-up left, a semifinalist right. They rise in turn.
    void Podium()
    {
        Rift(0.5f, 0.5f);
        var head = T(this, "CHAMPION", Title, 90, new Vector2(0, 40), new Vector2(W, 110), HorizontalAlignment.Center);
        head.Mat().P("lit", 1f);
        Appear(head, 1.6);
        Appear(T(this, "DUELIST KINGDOM CUP", Thin, 24, new Vector2(0, 150), new Vector2(W, 32), HorizontalAlignment.Center, 0.8f), 1.8);
        (RivalPanels.Rival? who, string place, float x, float h, double at)[] steps =
        {
            (RivalPanels.All[1], "2", W * 0.27f, H * 0.2f, 0.3), (null, "1", W * 0.5f, H * 0.32f, 1.0), (RivalPanels.All[0], "3", W * 0.73f, H * 0.13f, 0.6),
        };
        foreach (var (who, place, x, h, at) in steps)
        {
            float bw = W * 0.2f, top = H - 40 - h;
            var block = new Panel { Position = new Vector2(x - bw / 2, top), Size = new Vector2(bw, h + 60), MouseFilter = MouseFilterEnum.Ignore };
            var sb = new StyleBoxFlat { BgColor = new Color(0.05f, 0.05f, 0.07f, 0.95f), BorderColor = place == "1" ? GoldLight : GoldC with { A = 0.5f } };
            sb.SetBorderWidthAll(3); sb.SetCornerRadiusAll(8);
            block.AddThemeStyleboxOverride("panel", sb);
            AddChild(block);
            Put(block, place, Title, place == "1" ? 90 : 64, place == "1" ? GoldLight : Silver, new Vector2(0, 20), new Vector2(bw, 110), HorizontalAlignment.Center);
            float d = place == "1" ? 240 : 170;
            var f = Face(who, d); f.Position = new Vector2(x - d / 2, top - d - 16); AddChild(f);
            var name = Put(this, who?.Name ?? Settings.PlayerName.ToUpperInvariant(), Title, 24, place == "1" ? GoldLight : Mist, new Vector2(x - 200, top - d - 56), new Vector2(400, 32), HorizontalAlignment.Center);
            foreach (var c in new Control[] { block, f, name }) Appear(c, at, new Vector2(0, 260));
        }
        Burst(1.2);
        Confetti(1.3);
        Button(this, "SEE YOUR PRIZE", new Vector2(W * 0.88f, H * 0.5f), () => Press("SEE YOUR PRIZE"), 24);
    }

    // ---------------------------------------------------------------- tournament 3: the trophy card

    /// Your picture in the light, the finalist's ace (the prize card) rising in the dark on a beam, gold falling.
    void Trophy()
    {
        Rift(0.42f, 0.36f);
        var beam = Shadow(this, new Vector2(W * 0.7f - 120, -200), new Vector2(240, H + 400), 0.5f, new Color(1, 0.85f, 0.5f));   // a soft column of light
        beam.Modulate = new Color(1, 1, 1, 0);
        CreateTween().TweenProperty(beam, "modulate:a", 1f, 0.8).SetDelay(0.3);
        var ace = Card(this, Db.Named("Blue-Eyes White Dragon"), new Vector2(W * 0.7f, H + 300), 560, 0, 1.4f, true);
        var tw = CreateTween();
        tw.TweenInterval(0.4);
        tw.TweenProperty(ace, "position:y", H * 0.44f - 280, 1.2).SetTrans(Tween.TransitionType.Expo).SetEase(Tween.EaseType.Out);
        tw.TweenProperty(ace, "rotation", 0.02f, 1.6).SetTrans(Tween.TransitionType.Sine);
        Appear(T(this, "THE FINALIST'S ACE  ·  YOURS", Thin, 20, new Vector2(W * 0.7f - 300, H * 0.86f), new Vector2(600, 28), HorizontalAlignment.Center), 1.6);
        var f = Face(null, 220); f.Position = new Vector2(W * 0.2f - 110, H * 0.14f); AddChild(f);
        Appear(f, 1.0, new Vector2(0, 40));
        var head = T(this, "CHAMPION", Title, 86, new Vector2(40, H * 0.36f), new Vector2(W * 0.36f, 110), HorizontalAlignment.Center);
        head.Mat().P("lit", 1f);
        Appear(head, 1.3);
        Appear(T(this, "DUELIST KINGDOM CUP", Title, 26, new Vector2(40, H * 0.48f), new Vector2(W * 0.36f, 34), HorizontalAlignment.Center), 1.5);
        string[] lines = { "QUARTERFINAL  ·  REX", "SEMIFINAL  ·  MAI", "FINAL  ·  KAIBA" };
        for (int i = 0; i < 3; i++) Appear(T(this, lines[i], Thin, 22, new Vector2(40, H * 0.58f + i * 40), new Vector2(W * 0.36f, 30), HorizontalAlignment.Center, 0.8f), 1.8 + i * 0.2);
        Appear(T(this, "+1,500 DP  ·  6 CARDS", Title, 28, new Vector2(40, H * 0.76f), new Vector2(W * 0.36f, 36), HorizontalAlignment.Center), 2.5);
        Burst(1.5);
        Confetti(1.6, 50);
        Button(this, "SEE YOUR PRIZE", new Vector2(W * 0.2f, H - 60), () => Press("SEE YOUR PRIZE"), 24);
    }

    // ---------------------------------------------------------------- gauntlet 2: the road

    /// A road across the screen through four rivals; a gold line runs along it, lighting each as it passes,
    /// while your LP counts down beside it to what you finished with.
    void Road()
    {
        Rift(0.5f, 0.5f);
        var order = RivalPanels.All.OrderBy(r => r.File == "kaiba" ? 3 : r.File == "yugi" ? 2 : r.File == "joey" ? 0 : 1).ToArray();
        float y = H * 0.48f, x0 = W * 0.08f, x1 = W * 0.92f;
        var road = new DrawBox { Size = new Vector2(W, H), MouseFilter = MouseFilterEnum.Ignore };
        float k = 0;
        road.Paint = b =>
        {
            b.DrawLine(new Vector2(x0, y), new Vector2(x1, y), Mist with { A = 0.3f }, 4);
            b.DrawLine(new Vector2(x0, y), new Vector2(Mathf.Lerp(x0, x1, k), y), GoldLight, 7);
        };
        AddChild(road);
        var f0 = Face(null, 150); f0.Position = new Vector2(x0 - 75, y - 75); AddChild(f0);
        var lp = Put(this, "8000", Title, 40, GoldLight, new Vector2(x0 - 100, y + 90), new Vector2(200, 50), HorizontalAlignment.Center);
        Put(this, "LP", Thin, 18, Mist, new Vector2(x0 - 100, y + 140), new Vector2(200, 24), HorizontalAlignment.Center);
        int[] left = { 6900, 5400, 4200, 3450 };
        for (int i = 0; i < 4; i++)
        {
            float x = Mathf.Lerp(x0, x1, (i + 1) / 4f) - (i == 3 ? 40 : 0);
            var f = Face(order[i], 170); f.Position = new Vector2(x - 85, y - 85); f.Modulate = new Color(0.45f, 0.45f, 0.5f); AddChild(f);
            T(this, order[i].Name, Title, 24, new Vector2(x - 150, y + 100), new Vector2(300, 32), HorizontalAlignment.Center);
            double at = 0.4 + i * 0.55;
            int ii = i;
            GetTree().CreateTimer(at + 0.45).Timeout += () =>
            {
                CreateTween().TweenProperty(f, "modulate", Colors.White, 0.2);
                var p = Pip(this, new Vector2(x, y - 110), 26);
                p.Scale = new Vector2(2, 2);
                CreateTween().TweenProperty(p, "scale", Vector2.One, 0.25).SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
                lp.Text = left[ii].ToString();
            };
        }
        CreateTween().TweenMethod(Callable.From<float>(v => { k = v; road.QueueRedraw(); }), 0f, 1f, 2.3).SetDelay(0.4);
        var head = T(this, "GAUNTLET CLEARED", Title, 76, new Vector2(0, H * 0.1f), new Vector2(W, 96), HorizontalAlignment.Center);
        head.Mat().P("lit", 1f);
        Appear(head, 2.7);
        Burst(2.7);
        Appear(T(this, "FOUR RIVALS, ONE DECK  ·  +1,000 DP  ·  4 CARDS", Thin, 24, new Vector2(0, H * 0.1f + 100), new Vector2(W, 32), HorizontalAlignment.Center), 2.9);
        Button(this, "SEE YOUR CARDS", new Vector2(W / 2, H - 70), () => Press("SEE YOUR CARDS"), 26);
    }

    // ---------------------------------------------------------------- gauntlet 3: the ring

    /// The four rivals' faces circle in from the edges and close into a gold ring around your picture.
    void Ring()
    {
        Rift(0.5f, 0.5f);
        var c = new Vector2(W / 2, H * 0.47f);
        var me = Face(null, 260); me.Position = c - new Vector2(130, 130); AddChild(me);
        Appear(me, 0.2);
        var ring = new DrawBox { Size = new Vector2(W, H), MouseFilter = MouseFilterEnum.Ignore };
        float sweep = 0;
        ring.Paint = b => b.DrawArc(c, 260, -Mathf.Pi / 2, -Mathf.Pi / 2 + Mathf.Tau * sweep, 128, GoldLight, 6, true);
        AddChild(ring);
        for (int i = 0; i < 4; i++)
        {
            var r = RivalPanels.All[i];
            float a = -Mathf.Pi / 2 + Mathf.Tau * (i + 0.5f) / 4f;
            var to = c + Vector2.FromAngle(a) * 260;
            var f = Face(r, 150); AddChild(f);
            f.Position = c + Vector2.FromAngle(a) * 1100 - new Vector2(75, 75);
            var tw = CreateTween().SetParallel();
            tw.TweenProperty(f, "position", to - new Vector2(75, 75), 0.8).SetDelay(0.4 + i * 0.25).SetTrans(Tween.TransitionType.Expo).SetEase(Tween.EaseType.Out);
            tw.TweenProperty(f, "modulate", new Color(0.55f, 0.55f, 0.6f), 0.4).SetDelay(1.9);
            T(this, r.Name, Title, 20, to + new Vector2(-120, 82), new Vector2(240, 28), HorizontalAlignment.Center).Modulate = new Color(1, 1, 1, 0);
        }
        foreach (var l in GetChildren().OfType<Label>()) CreateTween().TweenProperty(l, "modulate:a", 1f, 0.3).SetDelay(1.6);
        CreateTween().TweenMethod(Callable.From<float>(v => { sweep = v; ring.QueueRedraw(); }), 0f, 1f, 0.8).SetDelay(1.6).SetTrans(Tween.TransitionType.Cubic);
        Burst(2.3);
        var head = T(this, "GAUNTLET CLEARED", Title, 66, new Vector2(0, 40), new Vector2(W, 84), HorizontalAlignment.Center);
        head.Mat().P("lit", 1f);
        Appear(head, 2.3);
        Appear(T(this, "3,450 LP LEFT  ·  +1,000 DP  ·  4 CARDS", Title, 26, new Vector2(0, H * 0.86f), new Vector2(W, 34), HorizontalAlignment.Center), 2.5);
        Button(this, "SEE YOUR CARDS", new Vector2(W / 2, H - 50), () => Press("SEE YOUR CARDS"), 24);
    }

    // ---------------------------------------------------------------- challenge 2: the sealed card

    /// The puzzle as a card (its name, goal and the cards it was solved with), a gold seal pressed onto it.
    void Sealed()
    {
        Rift(0.3f, 0.26f);
        var z = Puzzles.All[6];
        float w = 620, h = 760;
        var card = new Panel { Position = new Vector2(W * 0.62f - w / 2, H / 2 - h / 2), Size = new Vector2(w, h), PivotOffset = new Vector2(w, h) / 2, Rotation = -0.03f, MouseFilter = MouseFilterEnum.Ignore };
        var sb = new StyleBoxFlat { BgColor = new Color(0.95f, 0.93f, 0.88f), BorderColor = GoldC, ShadowColor = new Color(0, 0, 0, 0.6f), ShadowSize = 40 };
        sb.SetBorderWidthAll(4); sb.SetCornerRadiusAll(14);
        card.AddThemeStyleboxOverride("panel", sb);
        AddChild(card);
        Put(card, "CHALLENGE 07", Thin, 22, GoldDeep, new Vector2(0, 40), new Vector2(w, 30), HorizontalAlignment.Center);
        Put(card, z.Name, Title, 46, Ink, new Vector2(0, 80), new Vector2(w, 60), HorizontalAlignment.Center);
        Kit.Put(card, z.Goal, Body, 24, Graphite, new Vector2(60, 160), new Vector2(w - 120, 70), HorizontalAlignment.Center, wrap: true);
        for (int i = 0; i < z.Hand.Length; i++) Card(card, Db.Named(z.Hand[i]), new Vector2(w / 2 + (i - (z.Hand.Length - 1) / 2f) * 170, 400), 240, 0, 0.5f);
        Put(card, $"THEIR LP  {z.TheirLp:N0}  ·  WIN THIS TURN", Thin, 20, Graphite, new Vector2(0, 560), new Vector2(w, 28), HorizontalAlignment.Center);
        Appear(card, 0.1, new Vector2(0, 80));
        // the seal: a gold disc with a diamond, pressed down at an angle
        var seal = new DrawBox { Size = new Vector2(220, 220), PivotOffset = new Vector2(110, 110), Position = new Vector2(w - 230, h - 250), MouseFilter = MouseFilterEnum.Ignore };
        seal.Paint = b =>
        {
            var o = b.Size / 2;
            for (int i = 0; i < 16; i++) b.DrawCircle(o + Vector2.FromAngle(i * Mathf.Tau / 16) * 88, 24, new Color(0.62f, 0.42f, 0.12f));
            b.DrawCircle(o, 96, new Color(0.72f, 0.52f, 0.18f));
            b.DrawCircle(o, 78, new Color(0.82f, 0.62f, 0.26f));
            b.DrawArc(o, 70, 0, Mathf.Tau, 64, new Color(1, 0.9f, 0.6f, 0.8f), 3, true);
        };
        card.AddChild(seal);
        Put(seal, "SOLVED", Title, 26, new Color(0.3f, 0.2f, 0.05f), new Vector2(0, 92), new Vector2(220, 34), HorizontalAlignment.Center);
        seal.Scale = new Vector2(2.5f, 2.5f); seal.Rotation = -0.4f; seal.Modulate = new Color(1, 1, 1, 0);
        var tw = CreateTween().SetParallel();
        tw.TweenProperty(seal, "scale", Vector2.One, 0.22).SetDelay(1.0).SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
        tw.TweenProperty(seal, "rotation", -0.18f, 0.22).SetDelay(1.0);
        tw.TweenProperty(seal, "modulate:a", 1f, 0.08).SetDelay(1.0);
        Burst(1.05);
        var head = T(this, "SOLVED", Title, 80, new Vector2(80, H * 0.3f), new Vector2(W * 0.3f, 100));
        head.Mat().P("lit", 1f);
        Appear(head, 1.3);
        Appear(T(this, "FIRST SOLVE  ·  +200 DP", Title, 26, new Vector2(84, H * 0.3f + 110), new Vector2(W * 0.3f, 34)), 1.5);
        Appear(T(this, "5 OF 8 CHALLENGES SOLVED", Thin, 22, new Vector2(84, H * 0.3f + 160), new Vector2(W * 0.3f, 30), HorizontalAlignment.Left, 0.8f), 1.7);
        Button(this, "CHALLENGES", new Vector2(W * 0.1f, H - 70), () => Press("CHALLENGES"), 24);
        Button(this, "NEXT PUZZLE", new Vector2(W * 0.3f, H - 70), () => Press("NEXT PUZZLE"), 26);
    }

    // ---------------------------------------------------------------- challenge 3: the tiles

    /// All eight challenges as tiles; the one just solved turns over from dark to gold, its name under it.
    void Tiles()
    {
        Rift(0.5f, 0.5f);
        var head = T(this, "CHALLENGE SOLVED", Title, 70, new Vector2(0, 60), new Vector2(W, 90), HorizontalAlignment.Center);
        head.Mat().P("lit", 1f);
        Appear(head, 1.2);
        float tw0 = 180, th = 250, gap = 34, x0 = W / 2 - (4 * tw0 + 3 * gap) / 2, y0 = H * 0.24f;
        for (int i = 0; i < 8; i++)
        {
            bool solved = i < 4 || i == 6, now = i == 6;
            var at = new Vector2(x0 + (i % 4) * (tw0 + gap), y0 + (i / 4) * (th + gap));
            var tile = new Panel { Position = at, Size = new Vector2(tw0, th), PivotOffset = new Vector2(tw0, th) / 2, MouseFilter = MouseFilterEnum.Ignore };
            var sb = new StyleBoxFlat { BgColor = solved && !now ? new Color(0.22f, 0.17f, 0.08f) : new Color(0.06f, 0.06f, 0.08f), BorderColor = solved ? GoldC : Mist with { A = 0.3f } };
            sb.SetBorderWidthAll(3); sb.SetCornerRadiusAll(12);
            tile.AddThemeStyleboxOverride("panel", sb);
            AddChild(tile);
            var num = Put(tile, $"{i + 1:00}", Title, 70, solved && !now ? GoldLight : Mist with { A = 0.5f }, new Vector2(0, 70), new Vector2(tw0, 90), HorizontalAlignment.Center);
            if (solved && !now) Pip(tile, new Vector2(tw0 / 2, 200), 14);
            if (now)
            {
                // turns over: shrink to an edge, come back gold
                var flip = CreateTween();
                flip.TweenInterval(0.6);
                flip.TweenProperty(tile, "scale:x", 0f, 0.15);
                flip.TweenCallback(Callable.From(() =>
                {
                    sb.BgColor = GoldC; sb.BorderColor = GoldLight;
                    num.AddThemeColorOverride("font_color", Ink);
                    Pip(tile, new Vector2(tw0 / 2, 200), 18);
                }));
                flip.TweenProperty(tile, "scale:x", 1.12f, 0.15);
                flip.Parallel().TweenProperty(tile, "scale:y", 1.12f, 0.15);
                flip.TweenProperty(tile, "scale", Vector2.One, 0.3).SetTrans(Tween.TransitionType.Elastic).SetEase(Tween.EaseType.Out);
                Burst(0.9);
            }
        }
        Appear(T(this, "07  ·  CANNON FODDER", Title, 34, new Vector2(0, y0 + 2 * th + gap + 40), new Vector2(W, 44), HorizontalAlignment.Center), 1.1);
        Appear(T(this, "FIRST SOLVE  ·  +200 DP  ·  5 OF 8", Thin, 24, new Vector2(0, y0 + 2 * th + gap + 94), new Vector2(W, 32), HorizontalAlignment.Center, 0.8f), 1.3);
        Button(this, "CHALLENGES", new Vector2(W / 2 - 200, H - 70), () => Press("CHALLENGES"), 24);
        Button(this, "NEXT PUZZLE", new Vector2(W / 2 + 200, H - 70), () => Press("NEXT PUZZLE"), 26);
    }

    // ---------------------------------------------------------------- new playmat, round 2 (the rise was rejected)

    /// 2 unrolls left to right behind a gold edge; 3 two halves slide in from the light and the dark and meet
    /// at the rift; 4 lies face down and flips over; 5 is drawn in gold line first, then fills in from the centre.
    void MatReveal(int how)
    {
        Rift(0.5f, 0.5f);
        var tex = GD.Load<Texture2D>("res://assets/mats/obsidian.jpg");
        float w = W * 0.56f, h = w * tex.GetHeight() / tex.GetWidth();
        var at = new Vector2(W / 2 - w / 2, H * 0.12f);
        Appear(Shadow(this, at, new Vector2(w, h), 0.7f, new Color(0, 0, 0), new Vector2(0, 30)), 0.3);
        Control MatPic(Control host, Rect2 r, float u)
        {
            var clip = new Control { Position = r.Position, Size = r.Size, ClipContents = true, MouseFilter = MouseFilterEnum.Ignore };
            host.AddChild(clip);
            Pic(clip, tex, new Vector2(-u * w, 0), new Vector2(w, h));
            return clip;
        }
        double done = 1.6;
        if (how == 2)
        {
            var clip = MatPic(this, new Rect2(at, new Vector2(0, h)), 0);
            var edge = new ColorRect { Position = at, Size = new Vector2(8, h), Color = GoldLight, MouseFilter = MouseFilterEnum.Ignore };
            AddChild(edge);
            var tw = CreateTween().SetParallel();
            tw.TweenProperty(clip, "size:x", w, 1.2).SetDelay(0.3).SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.InOut);
            tw.TweenProperty(edge, "position:x", at.X + w - 8, 1.2).SetDelay(0.3).SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.InOut);
            tw.TweenProperty(edge, "modulate:a", 0f, 0.3).SetDelay(1.5);
        }
        else if (how == 3)
        {
            var left = MatPic(this, new Rect2(at - new Vector2(W * 0.6f, 0), new Vector2(w / 2, h)), 0);
            var right = MatPic(this, new Rect2(at + new Vector2(w / 2 + W * 0.6f, 0), new Vector2(w / 2, h)), 0.5f);
            var tw = CreateTween().SetParallel();
            tw.TweenProperty(left, "position:x", at.X, 0.8).SetDelay(0.3).SetTrans(Tween.TransitionType.Expo).SetEase(Tween.EaseType.Out);
            tw.TweenProperty(right, "position:x", at.X + w / 2, 0.8).SetDelay(0.3).SetTrans(Tween.TransitionType.Expo).SetEase(Tween.EaseType.Out);
            var seam = new ColorRect { Position = new Vector2(W / 2 - 3, at.Y), Size = new Vector2(6, h), Color = Colors.White, MouseFilter = MouseFilterEnum.Ignore, Modulate = new Color(1, 1, 1, 0) };
            AddChild(seam);
            var st = CreateTween();
            st.TweenInterval(1.0); st.TweenProperty(seam, "modulate:a", 1f, 0.05); st.TweenProperty(seam, "modulate:a", 0f, 0.6);
            done = 1.2;
        }
        else if (how == 6)
        {
            // the rival's half drops from above, yours rises from below; they meet on the centre line like the field
            Control Half(bool top, float fromY)
            {
                var clip = new Control { Position = new Vector2(at.X, fromY), Size = new Vector2(w, h / 2), ClipContents = true, MouseFilter = MouseFilterEnum.Ignore };
                AddChild(clip);
                Pic(clip, tex, new Vector2(0, top ? 0 : -h / 2), new Vector2(w, h));
                return clip;
            }
            var upper = Half(true, at.Y - H * 0.7f);
            var lower = Half(false, at.Y + h / 2 + H * 0.7f);
            var tw = CreateTween().SetParallel();
            tw.TweenProperty(upper, "position:y", at.Y, 0.8).SetDelay(0.3).SetTrans(Tween.TransitionType.Expo).SetEase(Tween.EaseType.Out);
            tw.TweenProperty(lower, "position:y", at.Y + h / 2, 0.8).SetDelay(0.3).SetTrans(Tween.TransitionType.Expo).SetEase(Tween.EaseType.Out);
            var seam = new ColorRect { Position = new Vector2(at.X, at.Y + h / 2 - 3), Size = new Vector2(w, 6), Color = Colors.White, MouseFilter = MouseFilterEnum.Ignore, Modulate = new Color(1, 1, 1, 0) };
            AddChild(seam);
            var st = CreateTween();
            st.TweenInterval(1.0); st.TweenProperty(seam, "modulate:a", 1f, 0.05); st.TweenProperty(seam, "modulate:a", 0f, 0.6);
            done = 1.2;
        }
        else if (how == 4)
        {
            var holder = new Control { Position = at, Size = new Vector2(w, h), PivotOffset = new Vector2(w / 2, h / 2), MouseFilter = MouseFilterEnum.Ignore };
            AddChild(holder);
            MatPic(holder, new Rect2(Vector2.Zero, new Vector2(w, h)), 0);
            var back = new Panel { Size = new Vector2(w, h), MouseFilter = MouseFilterEnum.Ignore };
            var sb = new StyleBoxFlat { BgColor = new Color(0.06f, 0.05f, 0.07f), BorderColor = GoldC };
            sb.SetBorderWidthAll(6); sb.SetCornerRadiusAll(20);
            back.AddThemeStyleboxOverride("panel", sb);
            holder.AddChild(back);
            Pip(back, new Vector2(w / 2, h / 2), 80);
            var tw = CreateTween();
            tw.TweenInterval(0.9);
            tw.TweenProperty(holder, "scale:x", 0f, 0.18).SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.In);
            tw.TweenCallback(Callable.From(() => back.Visible = false));
            tw.TweenProperty(holder, "scale:x", 1f, 0.25).SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
            done = 1.5;
        }
        else
        {
            var lines = new DrawBox { Position = at, Size = new Vector2(w, h), MouseFilter = MouseFilterEnum.Ignore };
            float k = 0;
            lines.Paint = b =>
            {
                var c = GoldLight;
                float len = k * 2 * (w + h);
                b.DrawLine(Vector2.Zero, new Vector2(Math.Min(len, w), 0), c, 4);
                if (len > w) b.DrawLine(new Vector2(w, 0), new Vector2(w, Math.Min(len - w, h)), c, 4);
                if (len > w + h) b.DrawLine(new Vector2(w, h), new Vector2(w - Math.Min(len - w - h, w), h), c, 4);
                if (len > 2 * w + h) b.DrawLine(new Vector2(0, h), new Vector2(0, h - Math.Min(len - 2 * w - h, h)), c, 4);
                b.DrawLine(new Vector2(0, h / 2), new Vector2(w * Math.Clamp(k * 2 - 1, 0, 1), h / 2), c, 3);
            };
            AddChild(lines);
            CreateTween().TweenMethod(Callable.From<float>(v => { k = v; lines.QueueRedraw(); }), 0f, 1f, 1.0).SetDelay(0.2);
            var clip = MatPic(this, new Rect2(at, new Vector2(w, h)), 0);
            clip.PivotOffset = new Vector2(w, h) / 2; clip.Scale = new Vector2(0.2f, 0.2f); clip.Modulate = new Color(1, 1, 1, 0);
            var tw = CreateTween().SetParallel();
            tw.TweenProperty(clip, "scale", Vector2.One, 0.6).SetDelay(1.1).SetTrans(Tween.TransitionType.Expo).SetEase(Tween.EaseType.Out);
            tw.TweenProperty(clip, "modulate:a", 1f, 0.4).SetDelay(1.1);
            tw.TweenProperty(lines, "modulate:a", 0f, 0.5).SetDelay(1.6);
            done = 1.8;
        }
        Burst(done);
        var head = T(this, "NEW PLAYMAT", Title, 64, new Vector2(0, at.Y + h + 40), new Vector2(W, 80), HorizontalAlignment.Center);
        head.Mat().P("lit", 1f);
        Appear(head, done);
        Appear(T(this, "OBSIDIAN  ·  CHAMPION PRIZE", Title, 28, new Vector2(0, at.Y + h + 124), new Vector2(W, 36), HorizontalAlignment.Center), done + 0.2);
        Button(this, "EQUIP", new Vector2(W / 2 - 200, H - 70), () => Press("EQUIP"), 28);
        Button(this, "CONTINUE", new Vector2(W / 2 + 200, H - 70), () => Press("CONTINUE"), 24);
    }
}
