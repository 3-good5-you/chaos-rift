using Godot;
using PoC.Engine;

namespace PoC.Game.UI;

/// Unlock Lab (2026-11-16, `unlocklab.bat`), round 2: how a newly won card is revealed and marked NEW. Like PoC, a
/// big NEW sits across the card for a moment, then it collapses into a small corner tag that stays until you look at
/// the card (hover it). Mix and match three rows: REVEAL (the animation), SOUND (our synths from
/// tools/unlockforge.py, or Master Duel's own) and TAG (the corner tag). The sound's hit is lined up with the reveal's.
/// Three cards: the outer two are new, the middle one is a copy you own (plain flip). Keys: R replay.
/// Picks + comment -> game/unlock_lab_picks.txt
public partial class UnlockLab : RiftMenu
{
    // Hit = the moment of impact, seconds from the start (animation and sound are aligned on it)
    static readonly (string Name, string Info, double Hit, int Sound)[] Reveals =
    {
        ("EDGE LIGHT", "Flips; a light runs around the card's edge, a sheen crosses it; NEW slides in", 0.0, 0),
        ("SLAM", "Flips; a big NEW slams down across the card (small shake)", 0.35, 1),
        ("LIFT", "The face-down card lifts toward you and glows, turns over and drops back; NEW opens out", 0.5, 2),
        ("SCAN", "A thin line scans the art in from the top; NEW wipes in", 0.6, 4),
        ("SHATTER", "The card back breaks into shards that fly off; NEW pops in", 0.0, 3),
    };
    static readonly (string Name, string Spec, double Hit)[] Sounds =
    {
        ("SNAP AIR", "gen/U_snap_air", 0.22), ("RISER HIT", "gen/U_riser_hit", 0.5), ("REVERSE SWELL", "gen/U_reverse_swell", 0.5),
        ("SHATTER", "gen/U_shatter", 0.01), ("SCAN SWEEP", "gen/U_scan_sweep", 0.6),
        ("MD CARD GET", "md/se/SE_STRUCTURE_CARD_GET", 0.05), ("MD SUPER CARD", "md/se/SE_SMN_CMN_CARD_SUPER", 0.05),
        ("MD APPEAR", "md/se/SE_CARDCHEAT_APPEAR", 0.05), ("MD REWARD", "md/se/SE_DR_REWARD", 0.03), ("MD SP DRAW", "md/se/SE_DUEL_SP_DRAW", 0.05),
    };
    static readonly (string Name, Color Accent)[] Tags =
    {
        ("CRIMSON TAB", new(1f, 0.23f, 0.29f)), ("GLOW PILL", new(0.3f, 0.85f, 1f)), ("GOLD CORNER", new(1f, 0.78f, 0.32f)),
    };
    const double Hold = 2.2;   // seconds the big NEW stays across the card

    static int reveal, sound, tag;
    static string note = "";
    Control stage = null!;
    Label info = null!;
    readonly List<Action> refresh = new();
    static Font? bold;
    static Font Bold => bold ??= Fonts.Get(new[] { "Bahnschrift", "Segoe UI" }, 700, true);
    Color Accent => Tags[tag].Accent;

    static string PicksPath => ProjectSettings.GlobalizePath("res://") + "../unlock_lab_picks.txt";
    protected override string? MusicPath => null;
    protected override void OnBack() => GetTree().Quit();

    protected override void Build()
    {
        Demo.Ensure();
        Rift(0.5f, 0.5f);
        int sb = AudioServer.GetBusIndex("Sound");
        if (sb >= 0) { AudioServer.SetBusMute(sb, false); AudioServer.SetBusVolumeDb(sb, 0); }
        var ui = new Control { Size = new Vector2(W, H), MouseFilter = MouseFilterEnum.Ignore };
        ui.SetMeta("dark", true);
        AddChild(ui);
        ui.AddChild(new ColorRect { Size = new Vector2(W, H), Color = new Color(0.03f, 0.03f, 0.05f), MouseFilter = MouseFilterEnum.Ignore });
        Row(ui, 30, "REVEAL", Reveals.Select(r => r.Name).ToArray(), () => reveal, i => { reveal = i; sound = Reveals[i].Sound; });
        Row(ui, 66, "SOUND", Sounds.Select(s => s.Name).ToArray(), () => sound, i => sound = i);
        Row(ui, 102, "TAG", Tags.Select(t => t.Name).ToArray(), () => tag, i => tag = i);
        info = Put(ui, "", BodyRegular, 18, Mist, new Vector2(72, 142));
        stage = new Control { Size = new Vector2(W, H), MouseFilter = MouseFilterEnum.Ignore };
        ui.AddChild(stage);
        Put(ui, "IN YOUR TRUNK  ·  HOVER A CARD TO CLEAR ITS TAG", Thin, 14, Mist, new Vector2(70, H - 345));
        Button(ui, "REPLAY", new Vector2(W - 230, H - 330), Replay, 22);
        Button(ui, "PICK THIS", new Vector2(W - 230, H - 240), () => { Save(); info.Text = "Saved to game/unlock_lab_picks.txt"; }, 22);
        Button(ui, "QUIT", new Vector2(W - 230, H - 150), OnBack, 22);
        var edit = new LineEdit { Text = note, PlaceholderText = "Comment: what to keep or change...", Position = new Vector2(70, H - 130), Size = new Vector2(W - 640, 44) };
        edit.TextChanged += t => note = t;
        edit.TextSubmitted += _ => { edit.ReleaseFocus(); Save(); };
        ui.AddChild(edit);
        foreach (var a in OS.GetCmdlineUserArgs())
        {
            if (a.StartsWith("--reveal=")) { reveal = int.Parse(a[9..]) - 1; sound = Reveals[reveal].Sound; }
            if (a.StartsWith("--sound=")) sound = int.Parse(a[8..]) - 1;
            if (a.StartsWith("--tag=")) tag = int.Parse(a[6..]) - 1;
        }
        Replay();
    }

    /// A row of options: "TITLE   1 NAME   2 NAME ...", the chosen one gold; choosing replays.
    void Row(Control ui, float y, string title, string[] names, Func<int> get, Action<int> set)
    {
        Put(ui, title, Title, 15, Mist * new Color(1, 1, 1, 0.55f), new Vector2(70, y + 2));
        float x = 190;
        var labels = new List<Label>();
        for (int i = 0; i < names.Length; i++)
        {
            int k = i;
            var l = Put(ui, names[i], Bold, 17, Mist, new Vector2(x, y));
            Click(l, () => { set(k); Replay(); }, h => l.AddThemeColorOverride("font_color", h || get() == k ? GoldLight : Mist));
            labels.Add(l);
            x += Bold.GetStringSize(names[i], HorizontalAlignment.Left, -1, Z(17)).X + 34;
        }
        refresh.Add(() => { for (int i = 0; i < labels.Count; i++) labels[i].AddThemeColorOverride("font_color", get() == i ? GoldLight : Mist); });
    }

    public override void _UnhandledInput(InputEvent e)
    {
        if (e is InputEventKey { Pressed: true, Echo: false, Keycode: Key.R } && GetViewport().GuiGetFocusOwner() is not LineEdit) Replay();
    }

    static void Save() => System.IO.File.WriteAllText(PicksPath,
        $"REVEAL: {Reveals[reveal].Name}   SOUND: {Sounds[sound].Name} ({Sounds[sound].Spec})   TAG: {Tags[tag].Name}{(note.Length > 0 ? $"\n// {note}" : "")}\n");

    // ---------------------------------------------------------------- the stage

    static CardData Find(string name) => CardDb.Shared.ByName.TryGetValue(name, out var l) ? l[0] : CardDb.Shared.ByIdx.Values.First();

    void Play(string spec, float db = 0)
    {
        var p = new AudioStreamPlayer { Bus = "Sound", VolumeDb = db, Stream = GD.Load<AudioStream>($"res://assets/audio/{spec}.wav") };
        stage.AddChild(p); p.Play();
        p.Finished += p.QueueFree;
    }

    Tween Tw() => stage.CreateTween();   // tweens die with the stage on replay
    void At(double t, Action a) { var tw = Tw(); tw.TweenInterval(Math.Max(t, 0.001)); tw.TweenCallback(Callable.From(a)); }

    void Replay()
    {
        foreach (var r in refresh) r();
        info.Text = Reveals[reveal].Info;
        foreach (var c in stage.GetChildren()) c.QueueFree();
        float h = 400, w = h * 0.686f, gap = 90;
        var names = new[] { "Blue-Eyes White Dragon", "Monster Reborn", "Dark Magician" };
        var back = Cosmetics.Back(P.Back, 512);
        for (int i = 0; i < 3; i++)
        {
            var at = new Vector2(W / 2 + (i - 1) * (w + gap), H * 0.45f);
            var card = Card(stage, Find(names[i]), at + new Vector2(0, H), h, 0.3f, 0.9f, true);
            var face = card.GetChildren().OfType<TextureRect>().Last();
            var art = face.Texture; face.Texture = back;
            var t = Tw().SetParallel();
            t.TweenProperty(card, "position:y", at.Y - h / 2, 0.55).SetDelay(i * 0.12).SetTrans(Tween.TransitionType.Expo).SetEase(Tween.EaseType.Out);
            t.TweenProperty(card, "rotation", 0f, 0.55).SetDelay(i * 0.12).SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
            if (i == 1) { At(2.6, () => { Flip(card, face, art); Play("gen/G_card_flip_snap"); }); continue; }
            Reveal(i == 0 ? 1.0 : 3.2, card, face, art, back);
        }
        var trunk = new[] { "Kuriboh", "Celtic Guardian", "Pot of Greed", "Summoned Skull", "Mirror Force", "Gaia The Fierce Knight", "Swords of Revealing Light", "Red-Eyes B. Dragon" };
        float th = 150, tw = th * 0.686f;
        for (int i = 0; i < trunk.Length; i++)
        {
            var card = Card(stage, Find(trunk[i]), new Vector2(70 + tw / 2 + i * (tw + 22), H - 300 + th / 2), th, 0, 0.6f);
            if (i is 1 or 4 or 6) Tag(card, false);
        }
    }

    void Flip(Control card, TextureRect face, Texture2D art, double dur = 0.1)
    {
        var t = Tw();
        t.TweenProperty(card, "scale:x", 0f, dur).SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.In);
        t.TweenCallback(Callable.From(() => face.Texture = art));
        t.TweenProperty(card, "scale:x", 1f, dur * 1.3).SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.Out);
    }

    /// The reveal starting at `cue`, with the sound's hit on the animation's hit.
    void Reveal(double cue, Control card, TextureRect face, Texture2D art, Texture2D back)
    {
        var (_, _, hit, _) = Reveals[reveal];
        double lead = Sounds[sound].Hit - hit;   // > 0: the sound needs a head start
        At(cue + Math.Max(0, -lead), () => Play(Sounds[sound].Spec));
        double t0 = cue + Math.Max(0, lead);
        At(t0, () => Animate(card, face, art, back));
        At(t0 + (reveal == 1 ? hit - 0.14 : hit + 0.08), () => Across(card));
    }

    void Animate(Control card, TextureRect face, Texture2D art, Texture2D back)
    {
        float w = card.Size.X, h = card.Size.Y;
        switch (reveal)
        {
            case 0:   // edge light + sheen
                Flip(card, face, art, 0.08);
                EdgeLight(card);
                Sheen(card, 0.1);
                break;
            case 1:
                Flip(card, face, art);
                break;
            case 2:   // lift, glow, turn, drop
            {
                var aura = Glow(card, Accent, 0);
                var t = Tw().SetParallel();
                t.TweenProperty(card, "scale", new Vector2(1.08f, 1.08f), 0.45).SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.Out);
                t.TweenProperty(card, "position:y", card.Position.Y - 24, 0.45).SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.Out);
                t.TweenProperty(aura, "modulate:a", 1f, 0.45);
                var y0 = card.Position.Y;
                At(0.44, () =>
                {
                    var f = Tw();
                    f.TweenProperty(card, "scale:x", 0f, 0.06);
                    f.TweenCallback(Callable.From(() => face.Texture = art));
                    f.TweenProperty(card, "scale:x", 1.08f, 0.06);
                    var d = Tw().SetParallel();
                    d.TweenProperty(card, "scale", Vector2.One, 0.3).SetDelay(0.12).SetTrans(Tween.TransitionType.Expo).SetEase(Tween.EaseType.Out);
                    d.TweenProperty(card, "position:y", y0, 0.3).SetDelay(0.12).SetTrans(Tween.TransitionType.Expo).SetEase(Tween.EaseType.Out);
                    d.TweenProperty(aura, "modulate:a", 0f, 0.8).SetDelay(0.2);
                    d.Chain().TweenCallback(Callable.From(aura.QueueFree));
                    Sheen(card, 0.12);
                });
                break;
            }
            case 3:   // scan
            {
                var o = Over(card);
                var clip = new Control { Size = new Vector2(w, 0), ClipContents = true, MouseFilter = MouseFilterEnum.Ignore };
                o.AddChild(clip);
                Pic(clip, art, Vector2.Zero, card.Size);
                var line = new Control { Size = new Vector2(w, 0), MouseFilter = MouseFilterEnum.Ignore };
                line.AddChild(new ColorRect { Position = new Vector2(0, -5), Size = new Vector2(w, 10), Color = Accent with { A = 0.3f }, MouseFilter = MouseFilterEnum.Ignore });
                line.AddChild(new ColorRect { Position = new Vector2(0, -1), Size = new Vector2(w, 2), Color = Colors.White, MouseFilter = MouseFilterEnum.Ignore });
                o.AddChild(line);
                var t = Tw().SetParallel();
                t.TweenProperty(clip, "size:y", h, 0.6).SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.InOut);
                t.TweenProperty(line, "position:y", h, 0.6).SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.InOut);
                t.Chain().TweenCallback(Callable.From(() => { face.Texture = art; o.QueueFree(); }));
                break;
            }
            default:  // shatter
                face.Texture = art;
                Shatter(card, back);
                break;
        }
    }

    /// A layer over the card, clipped to it.
    static Control Over(Control card)
    {
        var c = new Control { Size = card.Size, ClipContents = true, MouseFilter = MouseFilterEnum.Ignore };
        card.AddChild(c);
        return c;
    }

    // ---------------------------------------------------------------- the big NEW across the card, then the tag

    void Across(Control card)
    {
        float w = card.Size.X, h = card.Size.Y, bh = 86;
        var band = new Control { Position = new Vector2(-30, h * 0.42f - bh / 2), Size = new Vector2(w + 60, bh), PivotOffset = new Vector2(w + 60, bh) / 2, MouseFilter = MouseFilterEnum.Ignore };
        card.AddChild(band);
        var inner = new Control { Size = band.Size, MouseFilter = MouseFilterEnum.Ignore };
        band.AddChild(inner);
        Strip(inner, new Vector2(0, 0), new Vector2(band.Size.X, bh), new Color(0, 0, 0, 0.8f));
        Strip(inner, new Vector2(0, 0), new Vector2(band.Size.X, 2), Accent);
        Strip(inner, new Vector2(0, bh - 2), new Vector2(band.Size.X, 2), Accent);
        var l = Kit.Put(inner, "NEW", Bold, 58, Colors.White, Vector2.Zero, band.Size, HorizontalAlignment.Center);
        l.VerticalAlignment = VerticalAlignment.Center;
        l.MouseFilter = MouseFilterEnum.Ignore;
        l.AddThemeConstantOverride("outline_size", 12); l.AddThemeColorOverride("font_outline_color", Accent with { A = 0.45f });
        // a light runs through the word once it is in
        var glint = new ColorRect { Size = new Vector2(40, bh), Position = new Vector2(-60, 0), Color = new Color(1, 1, 1, 0.22f), MouseFilter = MouseFilterEnum.Ignore };
        var gclip = new Control { Size = band.Size, ClipContents = true, MouseFilter = MouseFilterEnum.Ignore };
        inner.AddChild(gclip); gclip.AddChild(glint);

        var t = Tw().SetParallel();
        switch (reveal)
        {
            case 0:   // slide in from the left
                inner.Position = new Vector2(-w * 0.5f, 0); band.Modulate = new Color(1, 1, 1, 0);
                t.TweenProperty(inner, "position:x", 0f, 0.3).SetTrans(Tween.TransitionType.Expo).SetEase(Tween.EaseType.Out);
                t.TweenProperty(band, "modulate:a", 1f, 0.15);
                break;
            case 1:   // slam
                band.Scale = new Vector2(1.8f, 1.8f); band.Modulate = new Color(1, 1, 1, 0);
                t.TweenProperty(band, "scale", Vector2.One, 0.14).SetTrans(Tween.TransitionType.Expo).SetEase(Tween.EaseType.In);
                t.TweenProperty(band, "modulate:a", 1f, 0.08);
                At(0.14, () => Shake(card, 7, 0.22));
                break;
            case 2:   // opens out from the middle
                band.Scale = new Vector2(0.05f, 1); band.Modulate = new Color(1, 1, 1, 0);
                t.TweenProperty(band, "scale:x", 1f, 0.3).SetTrans(Tween.TransitionType.Expo).SetEase(Tween.EaseType.Out);
                t.TweenProperty(band, "modulate:a", 1f, 0.12);
                break;
            case 3:   // wipes in left to right
                band.ClipContents = true;
                var full = band.Size.X; band.Size = new Vector2(0, bh);
                t.TweenProperty(band, "size:x", full, 0.3).SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.Out);
                break;
            default:  // pops
                band.Scale = new Vector2(0.6f, 0.6f); band.Modulate = new Color(1, 1, 1, 0);
                t.TweenProperty(band, "scale", Vector2.One, 0.3).SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
                t.TweenProperty(band, "modulate:a", 1f, 0.1);
                break;
        }
        var g = Tw(); g.TweenInterval(0.35); g.TweenProperty(glint, "position:x", band.Size.X + 40, 0.45).SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.InOut);
        // collapse into the corner, where the tag takes over
        var c = Tw(); c.TweenInterval(Hold);
        c.TweenProperty(band, "position", new Vector2(w - band.Size.X / 2 - 30, 18 - bh / 2), 0.28).SetTrans(Tween.TransitionType.Expo).SetEase(Tween.EaseType.In);
        c.Parallel().TweenProperty(band, "scale", new Vector2(0.22f, 0.3f), 0.28).SetTrans(Tween.TransitionType.Expo).SetEase(Tween.EaseType.In);
        c.Parallel().TweenProperty(band, "modulate:a", 0f, 0.28).SetTrans(Tween.TransitionType.Expo).SetEase(Tween.EaseType.In);
        c.TweenCallback(Callable.From(() => { band.QueueFree(); Tag(card, true); }));
    }

    /// A horizontal strip whose ends fade out.
    static void Strip(Control host, Vector2 pos, Vector2 size, Color c)
    {
        var g = new Gradient { Offsets = new[] { 0f, 0.18f, 0.82f, 1f }, Colors = new[] { c with { A = 0 }, c, c, c with { A = 0 } } };
        host.AddChild(new TextureRect { Texture = new GradientTexture2D { Gradient = g, Width = 256, Height = 4 }, Position = pos, Size = size, StretchMode = TextureRect.StretchModeEnum.Scale,
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, MouseFilter = MouseFilterEnum.Ignore });
    }

    /// The small NEW tag in the corner; it goes when the card is hovered.
    void Tag(Control card, bool fresh)
    {
        float k = Math.Clamp(card.Size.Y / 400f, 0.62f, 1f), w = card.Size.X;
        var root = new Control { MouseFilter = MouseFilterEnum.Ignore };
        card.AddChild(root);
        var a = Accent;
        Control? rim = null;
        switch (tag)
        {
            case 0:   // crimson tab hanging over the top-right edge, a shine runs over it now and then
            {
                float tw = 70 * k, th = 26 * k, sk = 8 * k;
                root.Position = new Vector2(w - tw - 6 * k, -th * 0.35f);
                root.AddChild(new Polygon2D { Polygon = new[] { new Vector2(sk, 0), new Vector2(tw, 0), new Vector2(tw - sk, th), new Vector2(0, th) },
                    VertexColors = new[] { a.Lightened(0.2f), a.Lightened(0.2f), a.Darkened(0.35f), a.Darkened(0.35f) } });
                var shine = new Polygon2D { Polygon = new[] { new Vector2(sk, 0), new Vector2(sk + 10 * k, 0), new Vector2(10 * k, th), new Vector2(0, th) }, Color = new Color(1, 1, 1, 0.55f), Position = new Vector2(-20 * k, 0) };
                var clip = new Control { Size = new Vector2(tw, th), ClipContents = true, MouseFilter = MouseFilterEnum.Ignore };
                root.AddChild(clip); clip.AddChild(shine);
                var s = Tw().SetLoops(); s.TweenInterval(2.0); s.TweenProperty(shine, "position:x", tw + 4, 0.4); s.TweenCallback(Callable.From(() => shine.Position = new Vector2(-20 * k, 0)));
                Word(root, Colors.White, new Vector2(tw, th), 16 * k);
                break;
            }
            case 1:   // dark glass pill with a pulsing dot; the card has a soft rim light while it's new
            {
                float pw = 72 * k, ph = 26 * k;
                root.Position = new Vector2(w - pw - 8 * k, 8 * k);
                var sb = new StyleBoxFlat { BgColor = new Color(0.03f, 0.05f, 0.08f, 0.88f), BorderColor = a, ShadowColor = a with { A = 0.45f }, ShadowSize = (int)(8 * k) };
                sb.SetBorderWidthAll(Math.Max(1, (int)(1.5f * k))); sb.SetCornerRadiusAll((int)(ph / 2));
                var pill = new Panel { Size = new Vector2(pw, ph), MouseFilter = MouseFilterEnum.Ignore };
                pill.AddThemeStyleboxOverride("panel", sb);
                root.AddChild(pill);
                float d = 7 * k;
                var dsb = new StyleBoxFlat { BgColor = a }; dsb.SetCornerRadiusAll(8);
                var dot = new Panel { Size = new Vector2(d, d), Position = new Vector2(10 * k, (ph - d) / 2), PivotOffset = new Vector2(d, d) / 2, MouseFilter = MouseFilterEnum.Ignore };
                dot.AddThemeStyleboxOverride("panel", dsb);
                root.AddChild(dot);
                var p = Tw().SetLoops(); p.TweenProperty(dot, "modulate:a", 0.25f, 0.6); p.TweenProperty(dot, "modulate:a", 1f, 0.6);
                var lbl = Word(root, Colors.White, new Vector2(pw, ph), 14 * k);
                lbl.Position = new Vector2(7 * k, 0);
                rim = Glow(card, a, 1, inside: true);
                var r = Tw().SetLoops(); r.TweenProperty(rim, "modulate:a", 0.35f, 1.1); r.TweenProperty(rim, "modulate:a", 1f, 1.1);
                break;
            }
            default:  // gold corner fold, NEW on the diagonal
            {
                float s = 64 * k;
                root.Position = new Vector2(w - s, 0);
                root.AddChild(new Polygon2D { Polygon = new[] { Vector2.Zero, new Vector2(s, 0), new Vector2(s, s) },
                    VertexColors = new[] { a.Lightened(0.25f), a.Lightened(0.1f), a.Darkened(0.4f) } });
                var l = Word(root, Ink, new Vector2(s, 20 * k), 14 * k);
                l.Position = new Vector2(s * 0.24f, s * 0.08f);
                l.PivotOffset = l.Size / 2; l.Rotation = Mathf.Pi / 4;
                break;
            }
        }
        if (fresh)
        {
            root.Modulate = new Color(1, 1, 1, 0);
            var t = Tw().SetParallel();
            t.TweenProperty(root, "modulate:a", 1f, 0.15);
            root.Scale = new Vector2(1.6f, 1.6f);
            t.TweenProperty(root, "scale", Vector2.One, 0.25).SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
        }
        card.MouseFilter = MouseFilterEnum.Pass;
        bool gone = false;
        card.MouseEntered += () =>
        {
            if (gone || !IsInstanceValid(root)) return;
            gone = true;
            Play("gen/U_seen", -10);
            var t = Tw().SetParallel();
            t.TweenProperty(root, "modulate:a", 0f, 0.2);
            if (rim != null) t.TweenProperty(rim, "self_modulate:a", 0f, 0.3);
        };
    }

    Label Word(Control host, Color c, Vector2 box, float size)
    {
        var l = Kit.Put(host, "NEW", Bold, (int)size, c, Vector2.Zero, box, HorizontalAlignment.Center);
        l.VerticalAlignment = VerticalAlignment.Center;
        l.MouseFilter = MouseFilterEnum.Ignore;
        return l;
    }

    // ---------------------------------------------------------------- effects

    /// A soft coloured glow around the card (behind it), or a thin rim light on it (`inside`).
    Panel Glow(Control card, Color c, float alpha, bool inside = false)
    {
        var sb = new StyleBoxFlat { BgColor = c with { A = 0 }, DrawCenter = false, BorderColor = inside ? c with { A = 0.9f } : c with { A = 0 }, ShadowColor = c with { A = inside ? 0.5f : 0.8f }, ShadowSize = inside ? 12 : 46 };
        sb.SetBorderWidthAll(inside ? 2 : 0); sb.SetCornerRadiusAll(6);
        var p = new Panel { Size = card.Size, Modulate = new Color(1, 1, 1, alpha), MouseFilter = MouseFilterEnum.Ignore };
        p.AddThemeStyleboxOverride("panel", sb);
        if (inside) card.AddChild(p);
        else { var host = card.GetParent<Control>(); p.Position = card.Position; host.AddChild(p); host.MoveChild(p, card.GetIndex()); }
        return p;
    }

    void Sheen(Control card, double delay)
    {
        var o = Over(card);
        var m = new ShaderMaterial { Shader = sheen ??= new Shader { Code = SheenCode } };
        o.AddChild(new ColorRect { Size = card.Size, Material = m, MouseFilter = MouseFilterEnum.Ignore });
        var t = Tw(); t.TweenInterval(delay);
        t.TweenMethod(Callable.From<float>(v => m.SetShaderParameter("p", v)), -0.3f, 1.3f, 0.5).SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.InOut);
        t.TweenCallback(Callable.From(o.QueueFree));
    }

    void EdgeLight(Control card)
    {
        float m = 30;
        var r = new ColorRect { Position = new Vector2(-m, -m), Size = card.Size + new Vector2(2 * m, 2 * m), MouseFilter = MouseFilterEnum.Ignore };
        var mat = new ShaderMaterial { Shader = edge ??= new Shader { Code = EdgeCode } };
        mat.SetShaderParameter("size", r.Size); mat.SetShaderParameter("half_box", card.Size / 2); mat.SetShaderParameter("col", Accent.Lightened(0.3f));
        r.Material = mat;
        card.AddChild(r);
        var t = Tw();
        t.TweenMethod(Callable.From<float>(v => mat.SetShaderParameter("p", v)), 0f, 1f, 0.7).SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.Out);
        t.TweenMethod(Callable.From<float>(v => mat.SetShaderParameter("fade", v)), 1f, 0f, 0.3);
        t.TweenCallback(Callable.From(r.QueueFree));
    }

    void Shatter(Control card, Texture2D back)
    {
        float w = card.Size.X, h = card.Size.Y;
        const int cols = 4, rows = 6;
        var rng = new RandomNumberGenerator { Seed = 9 };
        var pts = new Vector2[cols + 1, rows + 1];
        for (int x = 0; x <= cols; x++)
            for (int y = 0; y <= rows; y++)
            {
                var j = x is 0 or cols || y is 0 or rows ? Vector2.Zero : new Vector2(rng.RandfRange(-0.3f, 0.3f) * w / cols, rng.RandfRange(-0.3f, 0.3f) * h / rows);
                pts[x, y] = new Vector2(x * w / cols, y * h / rows) + j;
            }
        var uvk = back.GetSize() / card.Size;
        var hitAt = new Vector2(w * 0.5f, h * 0.42f);
        for (int x = 0; x < cols; x++)
            for (int y = 0; y < rows; y++)
                foreach (var tri in new[] { new[] { pts[x, y], pts[x + 1, y], pts[x + 1, y + 1] }, new[] { pts[x, y], pts[x + 1, y + 1], pts[x, y + 1] } })
                {
                    var c = (tri[0] + tri[1] + tri[2]) / 3;
                    var shard = new Polygon2D { Texture = back, Polygon = tri.Select(p => p - c).ToArray(), UV = tri.Select(p => p * uvk).ToArray(), Position = c };
                    card.AddChild(shard);
                    var dir = (c - hitAt).Normalized();
                    float push = rng.RandfRange(90, 260) * (1.2f - (c - hitAt).Length() / h);
                    var t = Tw().SetParallel();
                    t.TweenProperty(shard, "position", c + dir * push + new Vector2(0, rng.RandfRange(40, 120)), 0.6).SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.Out);
                    t.TweenProperty(shard, "rotation", rng.RandfRange(-2.5f, 2.5f), 0.6);
                    t.TweenProperty(shard, "scale", new Vector2(0.7f, 0.7f), 0.6);
                    t.TweenProperty(shard, "modulate:a", 0f, 0.45).SetDelay(0.15);
                    t.Chain().TweenCallback(Callable.From(shard.QueueFree));
                }
        // the flash where it breaks
        var o = Over(card);
        var f = new ColorRect { Size = card.Size, Color = new Color(1, 1, 1, 0.7f), MouseFilter = MouseFilterEnum.Ignore };
        o.AddChild(f);
        var ft = Tw(); ft.TweenProperty(f, "color:a", 0f, 0.25); ft.TweenCallback(Callable.From(o.QueueFree));
    }

    void Shake(Control c, float amp, double dur)
    {
        var home = c.Position;
        var t = Tw();
        for (int i = 0; i < 6; i++) t.TweenProperty(c, "position", home + new Vector2(GD.Randf() * 2 - 1, GD.Randf() * 2 - 1) * amp * (1 - i / 6f), dur / 7);
        t.TweenProperty(c, "position", home, dur / 7);
    }

    static Shader? sheen, edge;
    const string SheenCode = @"shader_type canvas_item;
render_mode blend_add;
uniform float p = -1.0;
void fragment() {
    float d = (UV.x * 0.8 + UV.y * 0.5) / 1.3 - p;
    COLOR = vec4(vec3(1.0), exp(-d * d * 220.0) * 0.45);
}";
    // two comets running around the card's edge
    const string EdgeCode = @"shader_type canvas_item;
render_mode blend_add;
uniform vec2 size; uniform vec2 half_box; uniform vec4 col : source_color; uniform float p = 0.0; uniform float fade = 1.0;
void fragment() {
    vec2 px = UV * size - size * 0.5;
    vec2 q = abs(px) - half_box;
    float d = length(max(q, 0.0)) + min(max(q.x, q.y), 0.0);
    float glow = exp(-abs(d) / 2.5) + exp(-abs(d) / 12.0) * 0.35;
    float a = atan(px.y, px.x) / 6.2831 + 0.5;
    float comet = exp(-fract(p - a) * 7.0) + exp(-fract(p + 0.5 - a) * 7.0);
    COLOR = vec4(col.rgb, clamp(glow * comet * fade, 0.0, 1.0));
}";
}
