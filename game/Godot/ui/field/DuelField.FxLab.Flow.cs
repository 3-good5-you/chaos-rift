using Godot;
using PoC.Engine;

namespace PoC.Game.UI;

/// FX Lab round "flow" (playtest 2026-11-12): deck shuffle samples, quicker reveal samples and menu button styles,
/// all in the one lab (`--fxlab`). Nothing here plays in duels or menus until it is picked.
public partial class DuelField
{
    void FlowLabEvents(Action<string, string, (string, Func<Task>)[]> add)
    {
        const string G = "PICK ONE PER ROW";
        add(G, "61  Rule wheel (Limited Duel: which rule you play)", new (string, Func<Task>)[]
        {
            ("Wheel: a spinning wheel of the rules slows under the pointer (3.2 s)", () => WheelLab(0)),
            ("Reel: the rule names roll like a slot reel and stop on the line (2.6 s)", () => WheelLab(1)),
            ("Cards: the rules are shuffled as face-down cards, one turns over (2 s)", () => WheelLab(2)),
        });
    }

    // ---------------------------------------------------------------- shuffle

    /// A side's Deck for the samples, bottom card first.
    List<View> LabDeck(int side, int n = 30)
    {
        var l = views.Values.Where(v => v.Card.Owner == side).OrderBy(v => v.Card.Uid).Take(n).ToList();
        for (int i = 0; i < l.Count; i++) Place(l[i], DeckOf(side, l.Count - 1 - i, l.Count));
        return l;
    }

    /// A card along a path: `at(0..1)` gives its transform.
    void Path(View v, double delay, double dur, Func<float, Transform3D> at)
    {
        v.Tw?.Kill(); v.Stat.Visible = false;
        var tw = v.Tw = CreateTween();
        tw.TweenInterval(delay + 0.001);
        tw.TweenMethod(Callable.From<float>(t => v.Root.Transform = at(t)), 0f, 1f, dur);
    }

    static float Ease(float t) => t < 0.5f ? 4 * t * t * t : 1 - Mathf.Pow(-2 * t + 2, 3) / 2;
    static float Span(float t, float from, float len) => Ease(Mathf.Clamp((t - from) / len, 0, 1));

    void Squared(List<View> deck) { foreach (var v in deck) Place(v, v.S); }

    /// The Deck splits left and right, then the two halves lace back together card by card. 0.85 s.
    async Task<double> ShuffleRiffle(List<View> d)
    {
        int n = d.Count, half = n / 2;
        var home = d.Select(v => v.Root.Transform).ToList();
        Cue("flip_st");
        for (int i = 0; i < n; i++)
        {
            int h = i < half ? -1 : 1, rank = i < half ? i : i - half, slot = i < half ? Math.Min(2 * i, n - 1) : Math.Min(2 * (i - half) + 1, n - 1);
            var from = home[i]; var to = home[slot];
            var split = new Transform3D(from.Basis * new Basis(Vector3.Back, h * 0.2f), home[0].Origin + new Vector3(h * 0.36f, 0.03f + rank * 0.0065f, 0));
            float land = 0.22f + slot * 0.5f / n;   // seconds: when this card drops into the stack
            Path(d[i], 0, 0.85, t =>
            {
                float s = t * 0.85f, a = Span(s, 0, 0.18f), b = Span(s, land, 0.1f);
                var mid = from.InterpolateWith(split, a).InterpolateWith(to, b);
                return new Transform3D(mid.Basis, mid.Origin + Vector3.Up * Mathf.Sin(Mathf.Pi * b) * 0.05f);
            });
            if (slot % 5 == 0) After(land, () => Cue("draw", slot / 5));
        }
        await Wait(0.87);
        Squared(d);
        return 0.87;
    }

    /// Two packets slide out (one sideways, one up the table) and drop back in the other order. 0.7 s.
    async Task<double> ShuffleCut(List<View> d)
    {
        int n = d.Count, a = n / 3, b = 2 * n / 3; float dir = d[0].S.Side == 0 ? 1 : -1;
        var home = d.Select(v => v.Root.Transform).ToList();
        Cue("flip_st"); After(0.3, () => Cue("flip_st")); After(0.55, () => Cue("land"));
        for (int i = a; i < n; i++)
        {
            bool top = i >= b;
            var from = home[i]; var to = home[top ? a + (i - b) : a + (n - b) + (i - a)];
            var outAt = from.Origin + (top ? new Vector3(0, 0.1f, -0.95f * dir) : new Vector3(-0.8f * dir, 0.1f, 0));
            float o0 = top ? 0 : 0.1f, i0 = top ? 0.28f : 0.44f;
            Path(d[i], 0, 0.7, t =>
            {
                float s = t * 0.7f, k = Span(s, i0, 0.22f);
                return new Transform3D(from.Basis, from.Origin.Lerp(outAt, Span(s, o0, 0.2f)).Lerp(to.Origin, k) + Vector3.Up * Mathf.Sin(Mathf.Pi * k) * 0.08f);
            });
        }
        await Wait(0.72);
        Squared(d);
        return 0.72;
    }

    /// Every card spins once around the Deck in a rising spiral and settles; a soft ring under it. 0.75 s.
    async Task<double> ShuffleWhirl(List<View> d)
    {
        Decal("md/softring", FxGold, d[0].Root.Position, new Vector2(2.3f, 2.3f), 0.3f, 1.1f, 0.6, alpha: 0.7f);
        Cue("flip_st"); After(0.55, () => Cue("land"));
        for (int i = 0; i < d.Count; i++)
        {
            var h = d[i].Root.Transform; float ph = i * 0.9f, rise = 0.1f + i * 0.006f;
            Path(d[i], i * 0.006, 0.55, t =>
            {
                float e = Ease(t), r = 0.42f * Mathf.Sin(Mathf.Pi * t), ang = ph + e * Mathf.Tau;
                return new Transform3D(new Basis(Vector3.Up, e * Mathf.Tau) * h.Basis, h.Origin + new Vector3(Mathf.Cos(ang) * r, Mathf.Sin(Mathf.Pi * t) * rise, Mathf.Sin(ang) * r));
            });
        }
        await Wait(0.78);
        Squared(d);
        return 0.78;
    }

    async Task ShuffleRiffle(int side) { var d = LabDeck(side); await Wait(0.5); await ShuffleRiffle(d); await Wait(0.6); }
    async Task ShuffleCut(int side) { var d = LabDeck(side); await Wait(0.5); await ShuffleCut(d); await Wait(0.6); }
    async Task ShuffleWhirl(int side) { var d = LabDeck(side); await Wait(0.5); await ShuffleWhirl(d); await Wait(0.6); }

    // ---------------------------------------------------------------- reveal

    async Task RevealLab(int kind, int style)
    {
        List<View> vs;
        if (kind == 2) vs = RivalBoard().Where(v => v.S.Loc == Loc.Spell).ToList();
        else { var hand = DealHand(1, 4); vs = kind == 0 ? new() { hand[1] } : hand; }
        DealHand(0, 4, 0);
        string title = $"{ThemName.ToUpperInvariant()}'S CARDS  ·  REVEALED";
        await Wait(0.6);
        double w;
        if (style == 0 && kind == 1) { RaiseHand(vs, 1, title); await Wait(1.3 + 0.8); ReturnHand(); w = 0.4; }
        else w = style switch { 0 => LiftReveal(vs, title, Pm.Them), 1 => RevealFlip(vs), 2 => RevealPop(vs, title, Pm.Them), _ => RevealTray(vs, Pm.Them) };
        await Wait(w);
        // what the duel does next, so the tray can be judged with something going on
        if (style == 3) { var mine = Of(0, "Dark Magician"); Place(mine, HandOf(0, 0, 1, true)); Move(mine, Mon(mine, 2, 0), 0, 0.4f, 0.4f); await Wait(2.6); }
        await Wait(0.5);
    }

    /// The cards turn face-up where they are (a gold frame on the table; a hand card grows so it can be read), then
    /// turn back. 1.1 s.
    double RevealFlip(List<View> vs)
    {
        var homes = vs.Select(v => (v, v.S)).ToList();
        Cue("flip_st");
        for (int i = 0; i < vs.Count; i++)
        {
            // the pace of the field's other card moves (0.4 s a flip); 0.2 s looked rushed next to them
            var v = vs[i]; bool hand = v.S.Loc == Loc.Hand; float d = i * 0.06f;
            Move(v, v.S with { Up = true }, d, 0.4f, hand ? 0 : 0.3f);
            After(0.42 + d, () =>
            {
                if (!hand) { FrameBurst(v, FxGold, 0.8f, 1.2f, 0.4); return; }
                v.Face.AlbedoTexture = CardArt.Large(v.Card.Data);
                var tw = CreateTween();
                tw.TweenProperty(v.Root, "scale", Vector3.One * 1.7f, 0.12).SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
                tw.TweenInterval(0.5);
                tw.TweenProperty(v.Root, "scale", Vector3.One, 0.1);
            });
        }
        After(1.4, () => { foreach (var (v, s) in homes) Move(v, s, 0, 0.4f, s.Loc == Loc.Hand ? 0 : 0.3f); });
        return 1.85;
    }

    /// A crisp 2D copy of each card opens big in the middle of the screen, holds, and snaps back to its card. 1.1 s.
    double RevealPop(List<View> vs, string title, Color edge)
    {
        int n = vs.Count;
        float cw = Math.Min(230, W * 0.6f / n - 24), gap = Math.Min(24, cw * 0.15f), x0 = W / 2 - (n - 1) * (cw + gap) / 2, y = H * 0.44f;
        Cue("flip_st");
        for (int i = 0; i < n; i++)
        {
            var v = vs[i]; var from = OnScreen(v);
            var c = FlatCard(CardArt.Large(v.Card.Data), from.At, cw);
            var small = new Vector2(0, from.W / cw);   // opens like a flip, out of the card itself
            c.Scale = small;
            v.Root.Visible = false;
            var tw = CreateTween();
            tw.TweenInterval(0.001 + i * 0.03);
            tw.TweenProperty(c, "position", new Vector2(x0 + i * (cw + gap), y) - c.Size / 2, 0.2).SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
            tw.Parallel().TweenProperty(c, "scale", Vector2.One, 0.2).SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
            tw.TweenInterval(0.7);
            tw.TweenProperty(c, "position", from.At - c.Size / 2, 0.16).SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.In);
            tw.Parallel().TweenProperty(c, "scale", small, 0.16);
            tw.TweenCallback(Callable.From(() => { v.Root.Visible = v.S.Loc != Loc.None; c.QueueFree(); }));
        }
        Caption(title, edge, (y - cw * CardH / 2 - 70) / H, 1.05);
        return 1.1 + n * 0.03;
    }

    /// Copies of the cards slide to the edge of the screen on their owner's side (the rival's above the phase circle,
    /// yours below it) under a small REVEALED tag and stay 2.5 s; the duel carries on after 0.3 s.
    double RevealTray(List<View> vs, Color edge)
    {
        int n = vs.Count; bool theirs = vs[0].S.Side == 1;
        const float cw = 118; float step = n <= 1 ? 0 : Math.Min(cw + 10, 190f / (n - 1)), y = H * (theirs ? 0.335f : 0.76f), x1 = W - 34 - cw / 2;
        const double stay = 2.5;
        Cue("flip_st");
        for (int i = 0; i < n; i++)
        {
            var v = vs[i]; var from = OnScreen(v);
            var c = FlatCard(CardArt.Large(v.Card.Data), from.At, cw);
            c.Scale = Vector2.One * (from.W / cw);
            var frame = new Panel { Position = new Vector2(-3, -3), Size = c.Size + new Vector2(6, 6), MouseFilter = MouseFilterEnum.Ignore };
            frame.AddThemeStyleboxOverride("panel", Pill(new Color(0, 0, 0, 0), edge, 7, 0, new Color(edge, 0.45f), 10, 2));
            c.AddChild(frame); c.MoveChild(frame, 0);   // under the art: the glow fills its whole box
            var tw = CreateTween();
            tw.TweenInterval(0.001 + i * 0.03);
            tw.TweenProperty(c, "position", new Vector2(x1 - (n - 1 - i) * step, y) - c.Size / 2, 0.26).SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
            tw.Parallel().TweenProperty(c, "scale", Vector2.One, 0.26);
            tw.TweenInterval(stay);
            tw.TweenProperty(c, "modulate:a", 0f, 0.25);
            tw.TweenCallback(Callable.From(c.QueueFree));
        }
        float tw0 = Cond.GetStringSize("REVEALED", HorizontalAlignment.Left, -1, 18).X + 28;
        var tag = Track(Box(hud, new Rect2(W - 34 - tw0, y - cw * CardH / 2 - 38, tw0, 28), Pill(new Color(0.03f, 0.03f, 0.05f, 0.9f), edge, 14, 0, null, 0, 2)), stay + 0.5);
        tag.MouseFilter = MouseFilterEnum.Ignore;
        Put(tag, "REVEALED", Cond, 18, Colors.White, new Vector2(0, 3), new Vector2(tw0, 22), HorizontalAlignment.Center);
        FadeIn(tag, 0.2f, new Vector2(20, 0));
        FadeOutAt(tag, stay + 0.2);
        return 0.3;
    }

    // ---------------------------------------------------------------- menu buttons

    /// The menu button styles on a slice of the Rift menu (light half, dark half); they react to the mouse.
    Task ButtonLab(int style)
    {
        float pw = 860, ph = 380, x0 = W / 2 - pw / 2 + 60, y0 = H * 0.26f;
        string[] words = { "START DUEL", "SAVE", "BACK" };
        foreach (bool light in new[] { true, false })
        {
            var half = Tmp(new ColorRect { Position = new Vector2(x0 + (light ? 0 : pw / 2), y0), Size = new Vector2(pw / 2, ph), Color = light ? new Color(0.93f, 0.93f, 0.91f) : new Color(0.045f, 0.045f, 0.06f) });
            for (int i = 0; i < words.Length; i++) LabButton(half, words[i], new Vector2(pw / 4, 80 + i * 110), style, light, i == 0 ? 28 : 22);
        }
        return Task.CompletedTask;
    }

    void LabButton(Control host, string s, Vector2 centre, int style, bool light, int size)
    {
        var f = RiftMenu.Title;
        Color ink = light ? new Color(0.13f, 0.13f, 0.15f) : new Color(0.85f, 0.86f, 0.9f), gold = light ? new Color(0.6f, 0.45f, 0.14f) : new Color(0.95f, 0.8f, 0.45f);
        float w = f.GetStringSize(s, HorizontalAlignment.Left, -1, size).X, bw = w + 84, bh = size * 2.1f;
        var root = new Control { Position = centre - new Vector2(bw, bh) / 2, Size = new Vector2(bw, bh), PivotOffset = new Vector2(bw, bh) / 2, MouseDefaultCursorShape = CursorShape.PointingHand };
        host.AddChild(root);
        var plate = new Panel { Size = root.Size, MouseFilter = MouseFilterEnum.Ignore };
        root.AddChild(plate);
        var l = Put(root, s, f, size, ink, Vector2.Zero, root.Size, HorizontalAlignment.Center);
        l.VerticalAlignment = VerticalAlignment.Center; l.MouseFilter = MouseFilterEnum.Ignore;
        var lines = new List<ColorRect>();
        void Line(float y, float lw) { var r = new ColorRect { Color = gold, Size = new Vector2(lw, 2), Position = new Vector2((bw - lw) / 2, y), PivotOffset = new Vector2(lw / 2, 0), MouseFilter = MouseFilterEnum.Ignore }; root.AddChild(r); lines.Add(r); }
        if (style == 0)
        {
            Line(1, w + 30); Line(bh - 3, w + 30);
            foreach (float x in new[] { 12f, bw - 12 }) root.AddChild(new ColorRect { Color = gold, Size = new Vector2(10, 10), Position = new Vector2(x - 5, bh / 2 - 5), PivotOffset = new Vector2(5, 5), Rotation = Mathf.Pi / 4, MouseFilter = MouseFilterEnum.Ignore });
        }
        if (style == 2) Line(bh - 8, w);
        void Look(bool h)
        {
            l.AddThemeColorOverride("font_color", h ? (style == 1 ? new Color(0.08f, 0.07f, 0.05f) : gold) : ink);
            foreach (var r in lines) CreateTween().TweenProperty(r, "scale:x", h ? 1f : style == 0 ? 0.3f : 0f, 0.2).SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
            if (style == 1) plate.AddThemeStyleboxOverride("panel", Pill(h ? gold : new Color(ink, 0.07f), h ? gold : new Color(ink, 0.4f), (int)(bh / 2), 0, h ? new Color(gold, 0.35f) : null, h ? 12 : 0, 1));
            else if (style == 3) plate.AddThemeStyleboxOverride("panel", Pill(new Color(gold, h ? 0.16f : 0f), h ? gold : new Color(ink, 0.45f), 9, 0, null, 0, h ? 2 : 1));
            else plate.AddThemeStyleboxOverride("panel", new StyleBoxEmpty());
            if (style is 1 or 3) CreateTween().TweenProperty(root, "scale", Vector2.One * (h ? 1.04f : 1f), 0.12);
        }
        root.MouseEntered += () => Look(true); root.MouseExited += () => Look(false);
        Look(false);
    }

    // ---------------------------------------------------------------- rule wheel (Limited Duel)

    static readonly Color[] WheelCols = { new(0.2f, 0.2f, 0.25f), new(0.34f, 0.27f, 0.12f), new(0.14f, 0.24f, 0.3f), new(0.3f, 0.16f, 0.18f), new(0.2f, 0.26f, 0.18f) };

    /// Three ways to draw the rule for a restricted duel; the rule is random on every run. The rule's name and its
    /// one-line text show under it once it has stopped.
    async Task WheelLab(int style)
    {
        var rules = Modes.LimitedRules; int n = rules.Length, win = new Random().Next(n);
        var dim = Tmp(new ColorRect { Size = new Vector2(W, H), Color = new Color(0.02f, 0.02f, 0.03f, 0.86f), MouseFilter = MouseFilterEnum.Ignore });
        var c = new Vector2(W / 2 + 60, H * 0.4f);
        var gold = new Color(0.95f, 0.8f, 0.45f);
        var name = Put(dim, "", RiftMenu.Title, 40, gold, new Vector2(60, H * 0.74f), new Vector2(W, 54), HorizontalAlignment.Center);
        var text = Put(dim, "", RiftMenu.Body, 24, new Color(1, 1, 1, 0.85f), new Vector2(60, H * 0.74f + 62), new Vector2(W, 34), HorizontalAlignment.Center);
        void Done() { name.Text = rules[win].Name; text.Text = rules[win].Text; Cue("land"); }
        float seg = Mathf.Tau / n;
        if (style == 0)
        {
            const float R = 250;
            var wheel = new Control { Position = c, MouseFilter = MouseFilterEnum.Ignore };
            dim.AddChild(wheel);
            wheel.Draw += () =>
            {
                for (int i = 0; i < n; i++)
                {
                    var pts = new List<Vector2> { Vector2.Zero };
                    for (int k = 0; k <= 16; k++) pts.Add(Vector2.FromAngle(i * seg - seg / 2 + seg * k / 16) * R);
                    wheel.DrawColoredPolygon(pts.ToArray(), WheelCols[i % WheelCols.Length]);
                    wheel.DrawLine(Vector2.Zero, pts[1], new Color(gold, 0.5f), 1.5f, true);
                    wheel.DrawSetTransform(Vector2.Zero, i * seg, Vector2.One);
                    wheel.DrawString(Cond, new Vector2(R * 0.2f, 7), rules[i].Name, HorizontalAlignment.Left, R * 0.76f, 19, Colors.White);
                    wheel.DrawSetTransform(Vector2.Zero, 0, Vector2.One);
                }
                wheel.DrawArc(Vector2.Zero, R, 0, Mathf.Tau, 96, gold, 3, true);
                wheel.DrawCircle(Vector2.Zero, 16, gold);
            };
            dim.AddChild(new Polygon2D { Polygon = new[] { c + new Vector2(-16, -R - 22), c + new Vector2(16, -R - 22), c + new Vector2(0, -R + 10) }, Color = gold });
            Cue("flip_st");
            CreateTween().TweenProperty(wheel, "rotation", Mathf.Tau * 5 - Mathf.Pi / 2 - win * seg, 3.2).SetTrans(Tween.TransitionType.Quart).SetEase(Tween.EaseType.Out);
            await Wait(3.25);
        }
        else if (style == 1)
        {
            const float rh = 70, rw = 620;
            var win0 = new Control { Position = c - new Vector2(rw / 2, rh * 1.5f), Size = new Vector2(rw, rh * 3), ClipContents = true, MouseFilter = MouseFilterEnum.Ignore };
            dim.AddChild(win0);
            var strip = new Control { MouseFilter = MouseFilterEnum.Ignore };
            win0.AddChild(strip);
            int stop = n * 7 + win;   // the row that ends on the middle line
            for (int i = 0; i <= stop + 1; i++) Put(strip, rules[i % n].Name, RiftMenu.Title, 32, Colors.White, new Vector2(0, i * rh + 12), new Vector2(rw, 46), HorizontalAlignment.Center);
            foreach (float y in new[] { rh, rh * 2 }) dim.AddChild(new ColorRect { Position = win0.Position + new Vector2(60, y), Size = new Vector2(rw - 120, 2), Color = gold, MouseFilter = MouseFilterEnum.Ignore });
            Cue("flip_st");
            CreateTween().TweenProperty(strip, "position:y", -(stop - 1) * rh, 2.6).SetTrans(Tween.TransitionType.Quart).SetEase(Tween.EaseType.Out);
            await Wait(2.65);
        }
        else
        {
            const float cw = 150; float ch = cw * CardH;
            var cards = new List<Control>();
            for (int i = 0; i < n; i++)
            {
                var card = new Control { Size = new Vector2(cw, ch), PivotOffset = new Vector2(cw, ch) / 2, Position = c + new Vector2((i - (n - 1) / 2f) * (cw + 24) - cw / 2, -ch / 2), MouseFilter = MouseFilterEnum.Ignore };
                card.AddChild(new TextureRect { Name = "back", Texture = backs[0], ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.Scale, Size = card.Size, MouseFilter = MouseFilterEnum.Ignore });
                dim.AddChild(card); cards.Add(card);
            }
            var homes = cards.Select(x => x.Position).ToList();
            var rng = new Random();
            Cue("flip_st");
            foreach (var x in cards) CreateTween().TweenProperty(x, "position", c - new Vector2(cw, ch) / 2, 0.25).SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.In);
            await Wait(0.3);
            for (int r = 0; r < 4; r++)
            {
                foreach (var x in cards) { var tw = CreateTween(); tw.TweenProperty(x, "position:x", c.X - cw / 2 + rng.Next(-70, 71), 0.07); tw.TweenProperty(x, "position:x", c.X - cw / 2, 0.07); }
                Cue("draw", r);
                await Wait(0.16);
            }
            for (int i = 0; i < n; i++) CreateTween().TweenProperty(cards[i], "position", homes[i], 0.28).SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
            await Wait(0.45);
            var pickd = cards[rng.Next(n)];
            foreach (var x in cards) if (x != pickd) CreateTween().TweenProperty(x, "modulate", new Color(0.35f, 0.35f, 0.4f), 0.2);
            var flip = CreateTween();
            flip.TweenProperty(pickd, "scale", new Vector2(0, 1.25f), 0.14);
            flip.TweenCallback(Callable.From(() =>
            {
                pickd.GetNode<TextureRect>("back").Visible = false;
                var face = new Panel { Size = pickd.Size, MouseFilter = MouseFilterEnum.Ignore };
                face.AddThemeStyleboxOverride("panel", Pill(new Color(0.06f, 0.06f, 0.08f), gold, 10, 0, new Color(gold, 0.4f), 16, 2));
                pickd.AddChild(face);
                var l = Put(face, rules[win].Name, RiftMenu.Title, 18, gold, new Vector2(10, 0), pickd.Size - new Vector2(20, 0), HorizontalAlignment.Center);
                l.VerticalAlignment = VerticalAlignment.Center; l.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            }));
            flip.TweenProperty(pickd, "scale", new Vector2(1.25f, 1.25f), 0.16).SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
            await Wait(0.35);
        }
        Done();
        await Wait(1.2);
    }

    // ---------------------------------------------------------------- small menu text

    /// The kinds of small text the Rift menus use (captions, counts, sort words, card lines, body text) on both
    /// sides of the rift, at `k` times today's size; `heavy` = the body text in the bold cut.
    Task FontLab(float k, bool heavy)
    {
        float pw = 1100, ph = 560, x0 = W / 2 - pw / 2 + 60, y0 = H * 0.18f;
        foreach (bool light in new[] { true, false })
        {
            var half = Tmp(new ColorRect { Position = new Vector2(x0 + (light ? 0 : pw / 2), y0), Size = new Vector2(pw / 2, ph), Color = light ? new Color(0.93f, 0.93f, 0.91f) : new Color(0.045f, 0.045f, 0.06f) });
            Color ink = light ? new Color(0.13f, 0.13f, 0.15f) : new Color(0.85f, 0.86f, 0.9f), soft = new Color(ink, 0.7f), gold = light ? new Color(0.6f, 0.42f, 0.12f) : new Color(1f, 0.85f, 0.5f);
            float y = 28, w = pw / 2 - 68;
            void L(string s, Font f, int size, Color col, bool wrap = false)
            {
                int z = Mathf.RoundToInt(size * k);
                float h = wrap ? f.GetMultilineStringSize(s, HorizontalAlignment.Left, w, z).Y + 6 : z * 1.5f;
                var l = Put(half, s, f, z, col, new Vector2(34, y), new Vector2(w, h));
                if (wrap) l.AutowrapMode = TextServer.AutowrapMode.WordSmart; else l.ClipText = true;
                y += h + 14;
            }
            Put(half, "TOURNAMENT", RiftMenu.Title, 30, ink, new Vector2(34, y), new Vector2(w, 44)); y += 62;   // headings stay as they are
            L("PAGE 1 / 3        150 CARDS", RiftMenu.Thin, 15, soft);
            L("SORT: NAME        FILTERS", RiftMenu.Title, 16, gold);
            L("2003 OK     2004 NO     FOR THIS DUEL ONLY", RiftMenu.Thin, 13, soft);
            L("1,250 DP EACH   ·   OWNED ×2", RiftMenu.Thin, 20, gold);
            L("LIGHT · Dragon / Normal · Level 8", RiftMenu.BodyBold, 17, soft);
            L("Today's rule decides what your deck may hold. Both players start at 6000 LP. Win 2 duels out of 3 to take the round.", heavy ? RiftMenu.BodyBold : RiftMenu.Body, 19, ink, true);
        }
        return Task.CompletedTask;
    }
}
