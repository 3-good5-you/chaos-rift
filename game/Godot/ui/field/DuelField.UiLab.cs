using Godot;
using PoC.Engine;

namespace PoC.Game.UI;

/// `--uilab`: the VFX Lab panel over the duel's UI pieces, round 3. Every option from rounds 1 and 2 stays
/// (★ = the pick already made); NEW = this round. Picks go to user://ui_lab_picks_round3.json and game/ui_picks_round3.txt.
public partial class DuelField
{
    bool uiLab;
    readonly List<Node> uiTemp = new();

    T Tmp<T>(T n, Node? parent = null) where T : Node { (parent ?? hud).AddChild(n); uiTemp.Add(n); return n; }
    Panel TmpBox(Rect2 r, StyleBox s) { var p = Box(hud, r, s); uiTemp.Add(p); return p; }
    TextureRect Art(Control parent, Card c, Rect2 r, bool big = false) => Tmp(new TextureRect { Texture = big ? CardArt.Large(c.Data) : CardArt.Small(c.Data), ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, Position = r.Position, Size = r.Size, StretchMode = TextureRect.StretchModeEnum.Scale, MouseFilter = MouseFilterEnum.Ignore, TextureFilter = TextureFilterEnum.LinearWithMipmaps }, parent);
    static StyleBoxFlat Edge(Color c, float glow = 0.3f) => Glass(c, glow);

    /// Shows for `s` seconds, then clears what the variant made.
    async Task Hold(double s)
    {
        await Wait(s);
        CloseSheet();
        foreach (var n in uiTemp) if (IsInstanceValid(n)) n.QueueFree();
        uiTemp.Clear();
        handLine = null;
        profile[0].Visible = profile[1].Visible = true;
        if (lab) detail.Visible = false;
    }

    /// A ribbon that slides out of the round phase button to the left and back in.
    void PhaseRibbon(string text, string sub, Color c, float w, double hold)
    {
        const float h = 96;
        var cx = phaseCircle.Position.X + phaseCircle.Size.X / 2;
        var r = TmpBox(new Rect2(cx, phaseCircle.Position.Y + phaseCircle.Size.Y / 2 - h / 2, 0, h), Pill(new Color(0.03f, 0.03f, 0.045f, 0.94f), c, 48, 0, new Color(c, 0.35f), 16, 2));
        r.ClipContents = true;
        hud.MoveChild(r, phaseCircle.GetIndex());   // under the button
        var t = Put(r, text, Impact, sub.Length > 0 ? 46 : 42, c, new Vector2(0, sub.Length > 0 ? 6 : 22), new Vector2(w - 100, 56), HorizontalAlignment.Right, 6);
        Label? s = sub.Length > 0 ? Put(r, sub, Cond, 20, new Color(1, 1, 1, 0.65f), new Vector2(0, 60), new Vector2(w - 100, 26), HorizontalAlignment.Right) : null;
        void Open(float k)
        {
            r.Size = new Vector2(w * k, h); r.Position = new Vector2(cx - w * k, r.Position.Y);
            t.Position = new Vector2(w * k - w, t.Position.Y); if (s != null) s.Position = new Vector2(w * k - w, s.Position.Y);
        }
        var tw = CreateTween();
        tw.TweenMethod(Callable.From<float>(Open), 0f, 1f, 0.32).SetTrans(Tween.TransitionType.Expo).SetEase(Tween.EaseType.Out);
        tw.TweenInterval(hold);
        tw.TweenMethod(Callable.From<float>(Open), 1f, 0f, 0.22).SetTrans(Tween.TransitionType.Expo).SetEase(Tween.EaseType.In);
    }

    void UiLabEvents()
    {
        void Add(string name, params (string, Func<Task>)[] vars) => labEvents.Add(("BATTLE UI · ROUND 3", name, vars));
        System.Action none = () => { };
        Card Of(string n, int nth = 0) => C(n, nth).Card;
        string them = ThemName.ToUpperInvariant();
        Vector2 Screen(View v) => cam.UnprojectPosition(v.Root.GlobalPosition);
        List<Card> Grave() => new[] { "Kuriboh", "Mystical Elf", "Battle Ox", "Celtic Guardian", "Sangan", "Summoned Skull" }.Select(n => Of(n)).ToList();

        // ---- 1 activation
        async Task<View> Activate(bool rays = true)
        {
            var v = Put("Trap Hole", SpellZ(2, 1, false)); await Wait(0.4);
            Move(v, v.S with { Up = true }, 0, 0.35f, 0.3f); await Wait(0.35);
            if (rays) { GoldRays(v, FxPink); ChainLinkFx(v); Badge(v, 1); }
            return v;
        }
        /// The info plate both NEW options slide out beside the card.
        Panel InfoPlate(Vector2 at, Vector2 slide)
        {
            var p = TmpBox(new Rect2(at, new Vector2(330, 140)), Pill(new Color(0.03f, 0.04f, 0.07f, 0.95f), Pm.Them, 12, 0, new Color(Pm.Them, 0.45f), 16, 2));
            Put(p, $"{them} ACTIVATED", Cond, 20, Pm.Them, new Vector2(22, 14), new Vector2(290, 26));
            FitText(Put(p, "TRAP HOLE", Impact, 44, Colors.White, new Vector2(22, 40), new Vector2(290, 56), outline: 6), 44);
            Put(p, "TRAP  ·  CHAIN 1", Cond, 20, new Color(1, 1, 1, 0.6f), new Vector2(22, 98), new Vector2(290, 26));
            FadeIn(p, 0.2f, slide);
            return p;
        }
        Add("1  Opponent reveals / activates",
            ("Original toast", async () => { Put("Trap Hole", SpellZ(2, 1, true)); Toast($"{ThemName} · Trap Hole", "CHAIN 1"); await Hold(3); }),
            ("R1: Card art + verb, slides from right", async () =>
            {
                var v = Put("Trap Hole", SpellZ(2, 1, true));
                var p = TmpBox(new Rect2(W - 460, H * 0.36f, 420, 150), Edge(Bad));
                Art(p, v.Card, new Rect2(14, 12, 86, 126));
                Put(p, $"{them} ACTIVATED", Cond, 20, Bad, new Vector2(116, 18), new Vector2(290, 26));
                Put(p, "TRAP HOLE", Impact, 38, Colors.White, new Vector2(116, 46), new Vector2(290, 50), outline: 6);
                Put(p, "Trap  ·  Chain Link 1", Cond, 18, new Color(1, 1, 1, 0.6f), new Vector2(116, 100), new Vector2(290, 26));
                FadeIn(p, 0.2f, new Vector2(120, 0)); await Hold(3);
            }),
            ("R1: Full-width gold ribbon", async () =>
            {
                Put("Trap Hole", SpellZ(2, 1, true));
                var band = Tmp(new ColorRect { Position = new Vector2(0, H * 0.4f), Size = new Vector2(W, 84), Color = new Color(RivalPanels.Gold, 0.85f), MouseFilter = MouseFilterEnum.Ignore });
                Put(band, $"{them} REVEALED  ·  TRAP HOLE", Impact, 46, new Color(0.1f, 0.07f, 0.02f), new Vector2(0, 14), new Vector2(W, 56), HorizontalAlignment.Center);
                FadeIn(band, 0.15f, new Vector2(-W * 0.3f, 0)); await Hold(2.5);
            }),
            ("R2: The card itself rises off the field", async () =>
            {
                var v = await Activate();
                var home = Lift(v, new Vector3(0, 2.6f, 1.6f), 1.7f, 0.35); await Wait(0.35);
                var at = Screen(v);
                var plate = Tmp(new Control { Position = new Vector2(at.X - 260, at.Y + 175), Size = new Vector2(520, 80), MouseFilter = MouseFilterEnum.Ignore });
                Put(plate, "TRAP HOLE", Impact, 44, Colors.White, Vector2.Zero, new Vector2(520, 52), HorizontalAlignment.Center, 8);
                Put(plate, $"{them}  ·  CHAIN 1", Cond, 22, Pm.Them, new Vector2(0, 50), new Vector2(520, 28), HorizontalAlignment.Center, 4);
                FadeIn(plate, 0.15f, new Vector2(0, 16));
                await Wait(1.3);
                CreateTween().TweenProperty(plate, "modulate:a", 0f, 0.15);
                CreateTween().TweenProperty(v.Root, "transform", home, 0.3).SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.In);
                await Wait(0.3); Place(v, v.S);
                await Hold(0.5);
            }),
            ("R2: Cut-in from their corner, name on the art", async () =>
            {
                await Activate();
                float w = 190, h = w * CardH;
                var p = TmpBox(new Rect2(W + 20, ProfH + 50, w + 300, h + 12), Pill(new Color(0.03f, 0.04f, 0.07f, 0.95f), Pm.Them, 12, 0, new Color(Pm.Them, 0.45f), 16, 2));
                Art(p, Of("Trap Hole"), new Rect2(6, 6, w, h), true);
                Put(p, $"{them} ACTIVATED", Cond, 20, Pm.Them, new Vector2(w + 24, h / 2 - 62), new Vector2(270, 26));
                FitText(Put(p, "TRAP HOLE", Impact, 44, Colors.White, new Vector2(w + 24, h / 2 - 36), new Vector2(262, 56), outline: 6), 44);
                Put(p, "TRAP  ·  CHAIN 1", Cond, 20, new Color(1, 1, 1, 0.6f), new Vector2(w + 24, h / 2 + 24), new Vector2(270, 26));
                var tw = CreateTween();
                tw.TweenProperty(p, "position:x", W - w - 300 - 36, 0.26).SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
                tw.TweenInterval(1.3);
                tw.TweenProperty(p, "position:x", W + 20, 0.22).SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.In);
                await Hold(2.2);
            }),
            ("R2: The card rises, an info plate slides out beside it", async () =>
            {
                var v = await Activate();
                var home = Lift(v, new Vector3(-1.2f, 2.2f, 1.4f), 1.5f, 0.35); await Wait(0.35);
                var at = Screen(v);
                var p = InfoPlate(new Vector2(at.X + 120, at.Y - 70), new Vector2(-60, 0));
                await Wait(1.4);
                CreateTween().TweenProperty(p, "modulate:a", 0f, 0.15);
                CreateTween().TweenProperty(v.Root, "transform", home, 0.3).SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.In);
                await Wait(0.3); Place(v, v.S);
                await Hold(0.5);
            }),
            ("R2: Big art flashes centre, then drops onto its zone", async () =>
            {
                var v = Put("Trap Hole", SpellZ(2, 1, false)); await Wait(0.4);
                float w = 250, h = w * CardH;
                var holder = Tmp(new Control { Position = new Vector2(W / 2 - w / 2, H * 0.42f - h / 2), Size = new Vector2(w, h + 90), PivotOffset = new Vector2(w / 2, h / 2), Scale = new Vector2(1.35f, 1.35f), MouseFilter = MouseFilterEnum.Ignore });
                Art(holder, v.Card, new Rect2(0, 0, w, h), true);
                Put(holder, "TRAP HOLE", Impact, 40, Colors.White, new Vector2(-150, h + 6), new Vector2(w + 300, 50), HorizontalAlignment.Center, 7);
                Put(holder, $"{them}  ·  CHAIN 1", Cond, 22, Pm.Them, new Vector2(-150, h + 54), new Vector2(w + 300, 28), HorizontalAlignment.Center, 4);
                holder.Modulate = new Color(1, 1, 1, 0);
                var tw = CreateTween();
                tw.TweenProperty(holder, "modulate:a", 1f, 0.12);
                tw.Parallel().TweenProperty(holder, "scale", Vector2.One, 0.3).SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
                tw.TweenInterval(0.9);
                var at = Screen(v);
                tw.TweenProperty(holder, "position", at - new Vector2(w / 2, h / 2), 0.25).SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.In);
                tw.Parallel().TweenProperty(holder, "scale", new Vector2(0.3f, 0.3f), 0.25);
                tw.Parallel().TweenProperty(holder, "modulate:a", 0f, 0.25);
                await Wait(1.5);
                Move(v, v.S with { Up = true }, 0, 0.2f, 0.2f); await Wait(0.2);
                GoldRays(v, FxPink); ChainLinkFx(v); Badge(v, 1);
                await Hold(1.2);
            }),
            ("NEW: Rises from its zone in full colour, plate slides out", async () =>
            {
                // Between "rises off the field" and the cut-in: the card leaves its own zone (so you see where it
                // is), grows toward the right-hand side and the cut-in's plate slides out of it. Drawn flat on
                // the HUD, so the art keeps its colour and contrast.
                var v = await Activate();
                var (at, w0) = OnScreen(v);
                const float w = 230;
                var to = new Vector2(W * 0.62f, H * 0.4f);
                var card = FlatCard(CardArt.Large(v.Card.Data), at, w);
                card.Scale = Vector2.One * (w0 / w);
                v.Root.Visible = false;
                var tw = CreateTween().SetParallel();
                tw.TweenProperty(card, "scale", Vector2.One, 0.32).SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
                tw.TweenProperty(card, "position", to - card.Size / 2, 0.32).SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
                await Wait(0.3);
                var p = InfoPlate(new Vector2(to.X + w / 2 + 14, to.Y - 70), new Vector2(-80, 0));
                hud.MoveChild(p, card.GetIndex());   // slides out from under the card
                await Wait(1.5);
                CreateTween().TweenProperty(p, "modulate:a", 0f, 0.15);
                var back = CreateTween().SetParallel();
                back.TweenProperty(card, "scale", Vector2.One * (w0 / w), 0.28).SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.In);
                back.TweenProperty(card, "position", at - card.Size / 2, 0.28).SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.In);
                await Wait(0.28);
                v.Root.Visible = true; card.Visible = false;
                await Hold(0.5);
            }),
            ("NEW: As above, the card held over its own zone", async () =>
            {
                var v = await Activate();
                var (at, w0) = OnScreen(v);
                const float w = 210;
                var to = new Vector2(at.X, H * 0.36f);
                var card = FlatCard(CardArt.Large(v.Card.Data), at, w);
                card.Scale = Vector2.One * (w0 / w);
                v.Root.Visible = false;
                var tw = CreateTween().SetParallel();
                tw.TweenProperty(card, "scale", Vector2.One, 0.32).SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
                tw.TweenProperty(card, "position", to - card.Size / 2, 0.32).SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
                await Wait(0.3);
                var p = InfoPlate(new Vector2(to.X + w / 2 + 14, to.Y - 70), new Vector2(-80, 0));
                hud.MoveChild(p, card.GetIndex());
                await Wait(1.5);
                CreateTween().TweenProperty(p, "modulate:a", 0f, 0.15);
                var back = CreateTween().SetParallel();
                back.TweenProperty(card, "scale", Vector2.One * (w0 / w), 0.28).SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.In);
                back.TweenProperty(card, "position", at - card.Size / 2, 0.28).SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.In);
                await Wait(0.28);
                v.Root.Visible = true; card.Visible = false;
                await Hold(0.5);
            }));

        // ---- 2 response prompt
        async Task<Panel> RespondPanel(float w, float h)
        {
            PutMon("Vorse Raider", 2, 1); Put("Trap Hole", SpellZ(2, 0, false)); await Wait(0.2);
            var p = TmpBox(new Rect2(W / 2 - w / 2, H * 0.45f - h / 2, w, h), Edge(RivalPanels.Gold * new Color(1, 1, 1, 0.8f), 0.25f));
            p.MouseFilter = MouseFilterEnum.Stop;
            Put(p, $"{them} SUMMONED VORSE RAIDER  ·  RESPOND?", Cond, 22, Colors.White, new Vector2(0, 14), new Vector2(w, 30), HorizontalAlignment.Center);
            FadeIn(p);
            return p;
        }
        Add("2  Response prompt (chain?)",
            ("Original sheet", async () => { PutMon("Vorse Raider", 2, 1); ShowSheet("CHAIN TO SUMMON?", null, new() { ("ACTIVATE: TRAP HOLE", none, true), ("PASS", none, true) }); await Hold(4); }),
            ("R1: Context + card + timer", async () =>
            {
                PutMon("Vorse Raider", 2, 1); Put("Trap Hole", SpellZ(2, 0, false));
                const float w = 560, h = 300;
                var p = TmpBox(new Rect2(W / 2 - w / 2, H * 0.44f - h / 2, w, h), Edge(RivalPanels.Gold * new Color(1, 1, 1, 0.8f), 0.25f));
                p.MouseFilter = MouseFilterEnum.Stop;
                Put(p, $"{them} SUMMONED VORSE RAIDER", Cond, 20, new Color(1, 1, 1, 0.65f), new Vector2(0, 16), new Vector2(w, 26), HorizontalAlignment.Center);
                Put(p, "RESPOND?", Impact, 40, Colors.White, new Vector2(0, 40), new Vector2(w, 50), HorizontalAlignment.Center, 6);
                Art(p, Of("Trap Hole"), new Rect2(40, 104, 92, 134));
                p.AddChild(Chips.Make("ACTIVATE TRAP HOLE", new Vector2(156, 112), new Vector2(360, 50), none, 21));
                p.AddChild(Chips.Make("PASS", new Vector2(156, 176), new Vector2(360, 50), none, 21));
                var bar = Tmp(new ColorRect { Position = new Vector2(24, h - 18), Size = new Vector2(w - 48, 4), Color = RivalPanels.Gold }, p);
                CreateTween().TweenProperty(bar, "size:x", 0f, 4.5);
                FadeIn(p); await Hold(4.5);
            }),
            ("R1: Compact bar beside your profile", async () =>
            {
                PutMon("Vorse Raider", 2, 1); Put("Trap Hole", SpellZ(2, 0, false));
                var p = TmpBox(new Rect2(24 + ProfW + 16, H - 124, 470, 100), Edge(RivalPanels.Gold, 0.35f));
                p.MouseFilter = MouseFilterEnum.Stop;
                Art(p, Of("Trap Hole"), new Rect2(12, 10, 56, 80));
                Put(p, "CHAIN?", Cond, 18, new Color(1, 1, 1, 0.6f), new Vector2(82, 14), new Vector2(150, 24));
                Put(p, "TRAP HOLE", Impact, 28, Colors.White, new Vector2(82, 40), new Vector2(170, 40), outline: 5);
                p.AddChild(Chips.Make("YES", new Vector2(260, 26), new Vector2(90, 48), none, 21));
                p.AddChild(Chips.Make("NO", new Vector2(362, 26), new Vector2(90, 48), none, 21));
                FadeIn(p, 0.2f, new Vector2(0, 40)); await Hold(4);
            }),
            ("R2: Big card, ACTIVATE then PASS under it", async () =>
            {
                const float w = 480, cw = 200;
                var p = await RespondPanel(w, 56 + cw * CardH + 150);
                Art(p, Of("Trap Hole"), new Rect2(w / 2 - cw / 2, 56, cw, cw * CardH), true);
                float y = 56 + cw * CardH + 14;
                p.AddChild(Chips.Make("ACTIVATE TRAP HOLE", new Vector2(70, y), new Vector2(w - 140, 50), none, 21));
                p.AddChild(Chips.Make("PASS", new Vector2(130, y + 62), new Vector2(w - 260, 44), none, 19));
                await Hold(6);
            }),
            ("R2: The card is the button, PASS under it, no timer  ★", async () =>
            {
                const float w = 380, cw = 200;
                var p = await RespondPanel(w, 56 + cw * CardH + 150);
                var cell = Tmp(new Panel { Position = new Vector2(w / 2 - cw / 2 - 14, 52), Size = new Vector2(cw + 28, cw * CardH + 52) }, p);
                cell.AddThemeStyleboxOverride("panel", Pill(new Color(0, 0, 0, 0.4f), RivalPanels.Gold, 10, 0, new Color(RivalPanels.Gold, 0.5f), 14, 2));
                Art(cell, Of("Trap Hole"), new Rect2(14, 10, cw, cw * CardH), true);
                Put(cell, "ACTIVATE", Cond, 19, RivalPanels.Gold, new Vector2(0, cw * CardH + 16), new Vector2(cw + 28, 26), HorizontalAlignment.Center);
                p.AddChild(Chips.Make("PASS", new Vector2(w / 2 - 100, 52 + cw * CardH + 68), new Vector2(200, 46), none, 20));
                await Hold(6);
            }));

        // ---- 3 summon position
        Add("3  Attack / defence selector",
            ("Original (action chip on the hand card)  ★", async () =>
            {
                var v = Put("Battle Ox", HandAt(1, 3)); Put("Mystical Elf", HandAt(0, 3)); Put("Celtic Guardian", HandAt(2, 3));
                await Wait(0.5); GetViewport().WarpMouse(cam.UnprojectPosition(v.Root.GlobalPosition)); await Hold(4);
            }),
            ("R1: Two card slots: upright or set", async () =>
            {
                var c = Of("Battle Ox");
                var p = TmpBox(new Rect2(W / 2 - 230, H * 0.42f - 150, 460, 300), Edge(RivalPanels.Gold * new Color(1, 1, 1, 0.8f), 0.25f));
                Put(p, "SUMMON BATTLE OX", Cond, 24, Colors.White, new Vector2(0, 14), new Vector2(460, 30), HorizontalAlignment.Center);
                var up = Tmp(new Panel { Position = new Vector2(50, 58), Size = new Vector2(160, 190) }, p);
                up.AddThemeStyleboxOverride("panel", Pill(new Color(0, 0, 0, 0.4f), RivalPanels.Gold, 10, 0, new Color(RivalPanels.Gold, 0.5f), 16, 2));
                Art(up, c, new Rect2(30, 10, 100, 146));
                Put(up, "ATK 1700", Cond, 20, RivalPanels.Gold, new Vector2(0, 158), new Vector2(160, 26), HorizontalAlignment.Center);
                var dn = Tmp(new Panel { Position = new Vector2(250, 58), Size = new Vector2(160, 190) }, p);
                dn.AddThemeStyleboxOverride("panel", Pill(new Color(0, 0, 0, 0.4f), new Color(1, 1, 1, 0.2f), 10));
                Tmp(new TextureRect { Texture = backs[0], ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, Position = new Vector2(153, 30), Size = new Vector2(100, 146), RotationDegrees = 90, StretchMode = TextureRect.StretchModeEnum.Scale }, dn);
                Put(dn, "SET  ·  DEF 1000", Cond, 20, new Color(1, 1, 1, 0.7f), new Vector2(0, 158), new Vector2(160, 26), HorizontalAlignment.Center);
                FadeIn(p); await Hold(4);
            }),
            ("R1: Two chips above the card on the field", async () =>
            {
                var v = Put("Battle Ox", HandAt(1, 3)); Put("Mystical Elf", HandAt(0, 3)); Put("Celtic Guardian", HandAt(2, 3));
                await Wait(0.5);
                var at = cam.UnprojectPosition(v.Root.GlobalPosition);
                var a = Tmp(Chips.Make("⚔  ATTACK", at + new Vector2(-170, -230), new Vector2(160, 50), none, 21));
                var d = Tmp(Chips.Make("SET  🛡", at + new Vector2(10, -230), new Vector2(160, 50), none, 21));
                FadeIn(a, 0.15f, new Vector2(0, 30)); FadeIn(d, 0.15f, new Vector2(0, 30)); await Hold(4);
            }));

        // ---- 4 coin call
        Prompt Call() => new() { Kind = Ask.Choose, Source = Of("Time Wizard"), Options = { new(Act.Option, Text: "Heads"), new(Act.Option, Text: "Tails") } };
        Add("4  Coin call",
            ("Original (two faces in a sheet)", async () => { PutMon("Time Wizard", 2, 0); CoinCallSheet(Call()); await Hold(4); }),
            ("R1: Effect text on the sheet", async () =>
            {
                PutMon("Time Wizard", 2, 0); CoinCallSheet(Call());
                if (sheet != null)
                {
                    sheet.Size += new Vector2(0, 50);
                    Put(sheet, "HEADS: destroy all opponent's monsters   ·   TAILS: destroy yours, take half their ATK", Cond, 17, new Color(1, 1, 1, 0.65f), new Vector2(0, 330), new Vector2(sheet.Size.X, 30), HorizontalAlignment.Center);
                }
                await Hold(4);
            }),
            ("R1: No panel: coins float over the field  ★", async () =>
            {
                PutMon("Time Wizard", 2, 0); CoinCallSheet(Call());
                if (sheet != null) sheet.AddThemeStyleboxOverride("panel", new StyleBoxEmpty());
                var dim = Tmp(new ColorRect { Size = new Vector2(W, H), Color = new Color(0, 0, 0, 0.45f), MouseFilter = MouseFilterEnum.Ignore });
                hud.MoveChild(dim, sheet?.GetIndex() ?? 0);
                await Hold(4);
            }));

        // ---- 5 coin result
        Ev Coin(int player, int amount, int arg, int call) => new() { Kind = EvKind.Coin, Player = player, Other = Of("Time Wizard"), Amount = amount, Arg = arg, Call = call };
        /// Plays the four cases one after another: you right, you wrong, the rival's call, no call (2 coins).
        async Task CoinCases(Func<int, string> text, Func<int, Color> col)
        {
            PutMon("Time Wizard", 2, 0);
            var evs = new[] { Coin(Me, 1, 1, 1), Coin(Me, 1, 1, 0), Coin(1 - Me, 1, 1, 1), Coin(Me, 2, 3, -1) };
            for (int i = 0; i < 4; i++)
            {
                Coins(evs[i], false); await Wait(evs[i].Amount > 1 ? 2.1 : 1.9);
                ResultChip(text(i), col(i)); await Hold(1.8);
                PutMon("Time Wizard", 2, 0);
            }
            await Hold(0.1);
        }
        Add("5  Coin result",
            ("Original toss", async () => { PutMon("Time Wizard", 2, 0); Coins(Coin(Me, 1, 1, 1)); await Hold(4); }),
            ("R1: Toss + result banner", async () =>
            {
                PutMon("Time Wizard", 2, 0); Coins(Coin(Me, 1, 1, 1), false);
                await Wait(2.2); _ = Banner("HEADS", Good, "TIME WIZARD SUCCEEDS"); await Hold(2.5);
            }),
            ("R1: Toss + call vs result chip", async () =>
            {
                PutMon("Time Wizard", 2, 0); Coins(Coin(Me, 1, 1, 1), false);
                await Wait(2.2); ResultChip("YOU CALLED HEADS  ·  HEADS  ✓", Good); await Hold(2.5);
            }),
            ("R2: One chip (you right / wrong / rival / no call)", async () =>
            {
                string[] t = { "YOU CALLED RIGHT", "YOU CALLED WRONG", $"{them} CALLED WRONG", "EFFECT SUCCEEDS" };
                await CoinCases(i => t[i], i => i == 1 ? Bad : Good);
            }),
            ("NEW: Short words: CALL RIGHT / CALL WRONG", async () =>
            {
                // Says only whether the call was right, in the caller's own terms (the rival's right call is
                // theirs, so it reads in their colour, not as good or bad for you).
                string[] t = { "CALL RIGHT", "CALL WRONG", $"{them}: CALL RIGHT", "2 HEADS" };
                await CoinCases(i => t[i], i => i switch { 1 => Bad, 2 => Pm.Them, _ => Good });
            }));

        // ---- 6/7 profile boxes: new layouts (the real box is hidden while a mock shows)
        Add("6  Your profile box (layout)",
            ("Original  ★", async () => await Hold(4)),
            ("R1: Wide strip + counts", async () => { MockProfile(0, 1); await Hold(5); }),
            ("R1: LP first, avatar breaking the edge", async () => { MockProfile(0, 2); await Hold(5); }),
            ("R1: Compact: avatar + LP pill", async () => { MockProfile(0, 3); await Hold(5); }));
        Add("7  Opponent profile box (layout)",
            ("Original  ★", async () => await Hold(4)),
            ("R1: Wide strip + counts", async () => { MockProfile(1, 1); await Hold(5); }),
            ("R1: LP first, avatar breaking the edge", async () => { MockProfile(1, 2); await Hold(5); }),
            ("R1: Compact: avatar + LP pill", async () => { MockProfile(1, 3); await Hold(5); }));

        // ---- 8 pile viewer
        Add("8  Graveyard / fusion viewer",
            ("Original sheet  ★", async () => { var g = Grave(); ShowSheet($"YOUR GRAVEYARD  ·  {g.Count}", g, new() { ("CLOSE", none, true) }); await Hold(4); }),
            ("R1: Tabs: your GY / fusion / their GY", async () =>
            {
                void Tab(int i)
                {
                    var (t, cards) = i switch { 1 => ("YOUR FUSION DECK", new List<Card> { Of("B. Skull Dragon") }), 2 => ($"{them}'S GRAVEYARD", new List<Card> { Of("Feral Imp"), Of("Vorse Raider") }), _ => ("YOUR GRAVEYARD", Grave()) };
                    string[] n = { "GRAVEYARD", "FUSION", "THEIR GY" };
                    var chips = n.Select((x, k) => ((k == i ? "▸ " : "") + x, (System.Action)(() => Tab(k)), true)).Append(("CLOSE", none, true)).ToList();
                    ShowSheet($"{t}  ·  {cards.Count}", cards, chips);
                }
                Tab(0); await Wait(1.5); Tab(1); await Wait(1.5); Tab(2); await Hold(2);
            }),
            ("R1: Card fan + big art", async () =>
            {
                var g = Grave();
                var p = TmpBox(new Rect2(W / 2 - 420, H * 0.42f - 170, 840, 340), Edge(RivalPanels.Gold * new Color(1, 1, 1, 0.8f), 0.25f));
                Put(p, $"YOUR GRAVEYARD  ·  {g.Count}", Cond, 24, Colors.White, new Vector2(24, 14), new Vector2(400, 30));
                for (int i = 0; i < g.Count; i++)
                {
                    var a = Art(p, g[i], new Rect2(60 + i * 62, 90, 110, 160));
                    a.PivotOffset = new Vector2(55, 260); a.RotationDegrees = (i - (g.Count - 1) / 2f) * 7;
                }
                Art(p, g[^1], new Rect2(560, 20, 210, 300));
                Put(p, g[^1].Name.ToUpperInvariant(), Cond, 20, RivalPanels.Gold, new Vector2(24, 296), new Vector2(500, 28));
                FadeIn(p); await Hold(4);
            }));

        // ---- 9 duel menu
        Add("9  Duel menu",
            ("Original", async () => { menuAudio = 0; ToggleDuelMenu(); await Wait(3); CloseDuelMenu(); menuAudio = 1; await Hold(0.2); }),
            ("R1: Menu + duel log", async () =>
            {
                Tmp(new ColorRect { Size = new Vector2(W, H), Color = new Color(0, 0, 0, 0.6f) });
                var p = TmpBox(new Rect2(W / 2 - 360, H / 2 - 190, 720, 380), Edge(RivalPanels.Gold * new Color(1, 1, 1, 0.8f), 0.25f));
                Put(p, "DUEL MENU", Impact, 36, RivalPanels.Gold, new Vector2(30, 18), new Vector2(300, 46), outline: 5);
                string[] b = { "RESUME", "OPTIONS", "SURRENDER" };
                for (int i = 0; i < 3; i++) p.AddChild(Chips.Make(b[i], new Vector2(30, 90 + i * 66), new Vector2(260, 52), none, 22));
                Tmp(new ColorRect { Position = new Vector2(320, 24), Size = new Vector2(1, 330), Color = new Color(1, 1, 1, 0.15f) }, p);
                Put(p, "DUEL LOG", Cond, 20, new Color(1, 1, 1, 0.55f), new Vector2(344, 24), new Vector2(340, 26));
                Put(p, $"T3  {ThemName}: summoned Vorse Raider\nT3  You: activated Trap Hole\nT3  Vorse Raider destroyed\nT2  You: set Mystical Elf\nT2  You: activated Pot of Greed\nT1  {ThemName}: set 1 card", Cond, 19, Colors.White, new Vector2(344, 56), new Vector2(350, 300));
                FadeIn(p); await Hold(4);
            }),
            ("R1: Slim strip, top centre (field stays visible)", async () =>
            {
                var p = TmpBox(new Rect2(W / 2 - 330, 24, 660, 66), Edge(RivalPanels.Gold * new Color(1, 1, 1, 0.8f), 0.25f));
                string[] b = { "RESUME", "OPTIONS", "LOG", "SURRENDER" };
                for (int i = 0; i < 4; i++) p.AddChild(Chips.Make(b[i], new Vector2(12 + i * 162, 10), new Vector2(150, 46), none, 20));
                FadeIn(p, 0.2f, new Vector2(0, -40)); await Hold(4);
            }),
            ("R2: + MUSIC / SOUND sliders (try them)  ★", async () => { menuAudio = 1; ToggleDuelMenu(); await Wait(8); CloseDuelMenu(); menuAudio = 1; await Hold(0.2); }),
            ("R2: + MUSIC / SOUND ten-step bars (try them)", async () => { menuAudio = 2; ToggleDuelMenu(); await Wait(8); CloseDuelMenu(); menuAudio = 1; await Hold(0.2); }));

        // ---- 10 response with several cards
        string[] resp = { "Trap Hole", "Mirror Force", "Kuriboh" };
        void RespSetup() { PutMon("Vorse Raider", 2, 1); Put("Trap Hole", SpellZ(1, 0, false)); Put("Mirror Force", SpellZ(2, 0, false)); Put("Kuriboh", HandAt(0, 3)); }
        Add("10  Response prompt, 3+ choices",
            ("Original sheet (chips wrap)", async () =>
            {
                RespSetup();
                ShowSheet("CHAIN TO ATTACK?", null, new() { ("ACTIVATE: TRAP HOLE", none, true), ("ACTIVATE: MIRROR FORCE", none, true), ("DISCARD: KURIBOH", none, true), ("PASS", none, true) }); await Hold(5);
            }),
            ("R1: Card tiles + Pass  ★", async () =>
            {
                RespSetup();
                var p = MockSheet(700, 380, "VORSE RAIDER ATTACKS  ·  RESPOND?");
                for (int i = 0; i < 3; i++)
                {
                    var cell = Tmp(new Panel { Position = new Vector2(60 + i * 200, 64), Size = new Vector2(180, 230) }, p);
                    cell.AddThemeStyleboxOverride("panel", Pill(new Color(0, 0, 0, 0.4f), i == 1 ? RivalPanels.Gold : new Color(1, 1, 1, 0.2f), 10, 0, i == 1 ? new Color(RivalPanels.Gold, 0.5f) : null, i == 1 ? 14 : 0, i == 1 ? 2 : 1));
                    Art(cell, Of(resp[i]), new Rect2(36, 10, 108, 158));
                    Put(cell, resp[i].ToUpperInvariant(), Cond, 18, Colors.White, new Vector2(0, 172), new Vector2(180, 24), HorizontalAlignment.Center);
                    Put(cell, i == 2 ? "DISCARD FROM HAND" : "ACTIVATE", Cond, 15, RivalPanels.Gold, new Vector2(0, 198), new Vector2(180, 22), HorizontalAlignment.Center);
                }
                p.AddChild(Chips.Make("PASS", new Vector2(250, 312), new Vector2(200, 48), none, 21));
                await Hold(5);
            }),
            ("R1: List rows with card thumbs", async () =>
            {
                RespSetup();
                var p = MockSheet(560, 400, "CHAIN TO ATTACK?");
                string[] how = { "Trap  ·  set", "Trap  ·  set", "Monster  ·  discard from hand" };
                for (int i = 0; i < 3; i++)
                {
                    var row = Tmp(new Panel { Position = new Vector2(24, 62 + i * 82), Size = new Vector2(512, 74) }, p);
                    row.AddThemeStyleboxOverride("panel", Pill(new Color(1, 1, 1, i == 0 ? 0.1f : 0.04f), new Color(RivalPanels.Gold, i == 0 ? 0.9f : 0.25f), 10));
                    Art(row, Of(resp[i]), new Rect2(8, 6, 42, 62));
                    Put(row, resp[i].ToUpperInvariant(), Impact, 26, Colors.White, new Vector2(64, 6), new Vector2(420, 34), outline: 4);
                    Put(row, how[i], Cond, 17, new Color(1, 1, 1, 0.6f), new Vector2(64, 40), new Vector2(420, 24));
                }
                p.AddChild(Chips.Make("PASS", new Vector2(180, 316), new Vector2(200, 48), none, 21));
                await Hold(5);
            }),
            ("R1: Cards glow on the field, small Pass bar", async () =>
            {
                RespSetup();
                foreach (var n in resp) hints[C(n).Card.Uid] = Hint.Target;
                foreach (var v in views.Values) Aura(v);
                var p = TmpBox(new Rect2(W / 2 - 260, 24, 520, 70), Edge(RivalPanels.Gold, 0.3f));
                Put(p, "CLICK A GLOWING CARD TO CHAIN", Cond, 21, Colors.White, new Vector2(20, 22), new Vector2(330, 28));
                p.AddChild(Chips.Make("PASS", new Vector2(370, 12), new Vector2(136, 46), none, 20));
                await Hold(5); hints.Clear(); foreach (var v in views.Values) Aura(v);
            }));

        // ---- 11 turn banner: the round-2 options play with the game's turn animations (phase button pulse, gold zone wave)
        async Task TurnStart(int side)
        {
            ShowPhase(Phase.Draw, side, 3, false);
            ZoneWave(side, FxGold, spells: true, cue: side == 0 ? "turn_wave" : "turn_wave_rival");
            await Wait(0.15);
        }
        Add("11  Turn banner",
            ("Original (band)", async () => { await TurnStart(0); await Wait(0.25); await Banner("YOUR TURN", RivalPanels.Gold, "TURN 3"); await Hold(0.3); }),
            ("R1: Slim side tag (field stays visible)", async () =>
            {
                var p = TmpBox(new Rect2(W - 330, H * 0.45f, 306, 70), Pill(new Color(0.03f, 0.03f, 0.05f, 0.9f), RivalPanels.Gold, 12, 0, new Color(RivalPanels.Gold, 0.4f), 16, 2));
                Put(p, "YOUR TURN", Impact, 34, RivalPanels.Gold, new Vector2(18, 10), new Vector2(200, 48), outline: 5);
                Put(p, "TURN 3", Cond, 20, new Color(1, 1, 1, 0.7f), new Vector2(200, 22), new Vector2(90, 26), HorizontalAlignment.Right);
                FadeIn(p, 0.25f, new Vector2(200, 0)); await Hold(2.5);
            }),
            ("R1: Big turn number + name", async () =>
            {
                var l = Tmp(new Control { Size = new Vector2(W, H), MouseFilter = MouseFilterEnum.Ignore });
                Put(l, "3", Impact, 220, new Color(RivalPanels.Gold, 0.9f), new Vector2(0, H * 0.22f), new Vector2(W, 260), HorizontalAlignment.Center, 12);
                Put(l, $"{Settings.PlayerName.ToUpperInvariant()}'S TURN", Cond, 34, Colors.White, new Vector2(0, H * 0.22f + 250), new Vector2(W, 44), HorizontalAlignment.Center, 6);
                FadeIn(l, 0.2f); await Hold(2.2);
            }),
            ("R2: Words ride the wave on your half of the field  ★", async () =>
            {
                await TurnStart(0); FieldWords("YOUR TURN", 0, RivalPanels.Gold, 150, 0); await Wait(2.2);
                await TurnStart(1); FieldWords($"{them}'S TURN", 1, Pm.Them, 130, 0); await Hold(2.2);
            }),
            ("R2: Ribbon out of the phase button", async () =>
            {
                await TurnStart(0); PhaseRibbon("YOUR TURN", "TURN 3", RivalPanels.Gold, 520, 1.1); await Wait(2.2);
                await TurnStart(1); PhaseRibbon($"{them}'S TURN", "TURN 4", Pm.Them, 600, 1.1); await Hold(2.2);
            }),
            ("R2: Duelist cut-in: portrait + name", async () =>
            {
                foreach (int s in new[] { 0, 1 })
                {
                    await TurnStart(s);
                    var col = s == 0 ? RivalPanels.Gold : Pm.Them;
                    var strip = Tmp(new Control { Position = new Vector2(s == 0 ? -W : W, H / 2 - 110), Size = new Vector2(W, 220), MouseFilter = MouseFilterEnum.Ignore });
                    Tmp(new ColorRect { Position = new Vector2(0, 30), Size = new Vector2(W, 160), Color = new Color(0.02f, 0.02f, 0.03f, 0.82f), MouseFilter = MouseFilterEnum.Ignore }, strip);
                    Tmp(new ColorRect { Position = new Vector2(0, 30), Size = new Vector2(W, 3), Color = col, MouseFilter = MouseFilterEnum.Ignore }, strip);
                    Tmp(new ColorRect { Position = new Vector2(0, 187), Size = new Vector2(W, 3), Color = col, MouseFilter = MouseFilterEnum.Ignore }, strip);
                    var a = (Control)profile[s].GetChild(0).Duplicate();
                    a.Scale = Vector2.One * (220 / ProfH); a.Position = new Vector2(s == 0 ? W * 0.24f : W * 0.76f - 220, 0); strip.AddChild(a);
                    float tx = s == 0 ? W * 0.24f + 250 : W * 0.24f - 250;
                    Put(strip, s == 0 ? "YOUR TURN" : $"{them}'S TURN", Impact, 84, col, new Vector2(tx, 46), new Vector2(W * 0.52f, 100), s == 0 ? HorizontalAlignment.Left : HorizontalAlignment.Right, 10);
                    Put(strip, $"TURN {3 + s}", Cond, 26, new Color(1, 1, 1, 0.7f), new Vector2(tx, 146), new Vector2(W * 0.52f, 32), s == 0 ? HorizontalAlignment.Left : HorizontalAlignment.Right);
                    var tw = CreateTween();
                    tw.TweenProperty(strip, "position:x", 0f, 0.3).SetTrans(Tween.TransitionType.Expo).SetEase(Tween.EaseType.Out);
                    tw.TweenInterval(0.9);
                    tw.TweenProperty(strip, "position:x", s == 0 ? W : -W, 0.25).SetTrans(Tween.TransitionType.Expo).SetEase(Tween.EaseType.In);
                    await Wait(2.2);
                }
                await Hold(0.2);
            }));

        // ---- 12 phase display: the round-2 options play with the game's phase animations (button pulse; Battle also gets the ember wave)
        async Task Phases(Func<Phase, Color, Task> show)
        {
            foreach (var ph in new[] { Phase.Battle, Phase.Main2, Phase.End })
            {
                ShowPhase(ph, 0, 3, false);
                if (ph == Phase.Battle) { Cue("phase_battle"); ZoneWave(0, FxEmber, spells: false, 0.7, cue: "battle_wave"); }
                await show(ph, ph == Phase.Battle ? FxEmber : RivalPanels.Gold);
                await Wait(1.6);
            }
            await Hold(0.3);
        }
        Add("12  Phase display",
            ("Original (centre callout)", async () => await Phases(async (ph, c) => { Callout(PhaseNames[(int)ph] + " PHASE", Pm.You); await Task.CompletedTask; })),
            ("R1: Phase track (all six, current lit)", async () =>
            {
                string[] ph = { "DP", "SP", "M1", "BP", "M2", "EP" };
                var p = TmpBox(new Rect2(W / 2 - 250, H * 0.5f - 30, 500, 60), Edge(RivalPanels.Gold * new Color(1, 1, 1, 0.6f), 0.2f));
                for (int i = 0; i < 6; i++)
                {
                    var on = i == 3;
                    var c = Tmp(new Panel { Position = new Vector2(12 + i * 80, 10), Size = new Vector2(76, 40) }, p);
                    c.AddThemeStyleboxOverride("panel", Pill(on ? RivalPanels.Gold : new Color(1, 1, 1, 0.05f), on ? RivalPanels.Gold : new Color(1, 1, 1, 0.15f), 8));
                    Put(c, ph[i], Impact, 24, on ? new Color(0.1f, 0.07f, 0.02f) : new Color(1, 1, 1, i < 3 ? 0.35f : 0.7f), new Vector2(0, 4), new Vector2(76, 32), HorizontalAlignment.Center);
                }
                FadeIn(p); await Hold(3);
            }),
            ("R1: Word sweep across the field", async () =>
            {
                var band = Tmp(new ColorRect { Position = new Vector2(0, H * 0.46f), Size = new Vector2(W, 3), Color = RivalPanels.Gold });
                var l = Put(hud, "BATTLE PHASE", Impact, 72, Colors.White, new Vector2(0, H * 0.46f - 92), new Vector2(W, 90), HorizontalAlignment.Center, 8);
                uiTemp.Add(l); FadeIn(l, 0.25f, new Vector2(-300, 0)); FadeIn(band, 0.25f); await Hold(2);
            }),
            ("R2: Ribbon out of the phase button", async () => await Phases(async (ph, c) => { PhaseRibbon(PhaseNames[(int)ph] + " PHASE", "", c, 430, 0.8); await Task.CompletedTask; })),
            ("R2: Words on your half of the field  ★", async () => await Phases(async (ph, c) => { FieldWords(PhaseNames[(int)ph] + " PHASE", 0, c, 110, 0); await Task.CompletedTask; })));

        // ---- 13 yes / no
        Add("13  Use effect? (yes / no)",
            ("Original", async () => { ShowSheet("USE SANGAN'S EFFECT?", null, new() { ("YES", none, true), ("NO", none, true) }); await Hold(4); }),
            ("R1: Card art + effect text", async () =>
            {
                var p = MockSheet(640, 290, "USE SANGAN'S EFFECT?");
                Art(p, Of("Sangan"), new Rect2(28, 60, 110, 160));
                Put(p, "When sent from the field to the Graveyard: add 1 monster with 1500 or less ATK from your Deck to your hand.", Cond, 19, new Color(1, 1, 1, 0.8f), new Vector2(156, 64), new Vector2(456, 120)).AutowrapMode = TextServer.AutowrapMode.WordSmart;
                p.AddChild(Chips.Make("YES", new Vector2(156, 214), new Vector2(210, 48), none, 21));
                p.AddChild(Chips.Make("NO", new Vector2(384, 214), new Vector2(210, 48), none, 21));
                await Hold(4);
            }),
            ("R1: Small bubble by the card", async () =>
            {
                var v = PutMon("Sangan", 2, 0); await Wait(0.3);
                var at = cam.UnprojectPosition(v.Root.GlobalPosition);
                var p = TmpBox(new Rect2(at + new Vector2(-150, -190), new Vector2(300, 110)), Edge(RivalPanels.Gold, 0.35f));
                Put(p, "USE EFFECT?", Impact, 26, Colors.White, new Vector2(0, 8), new Vector2(300, 36), HorizontalAlignment.Center, 4);
                p.AddChild(Chips.Make("YES", new Vector2(16, 52), new Vector2(128, 44), none, 19));
                p.AddChild(Chips.Make("NO", new Vector2(156, 52), new Vector2(128, 44), none, 19));
                FadeIn(p, 0.15f, new Vector2(0, 20)); await Hold(4);
            }),
            ("R2: Big card, text box under it (shrinks to fit)", async () =>
            {
                var c = Of("Sangan");
                const float w = 520, cw = 220;
                var p = MockSheet(w, 60 + cw * CardH + 196, "USE SANGAN'S EFFECT?");
                Art(p, c, new Rect2(w / 2 - cw / 2, 58, cw, cw * CardH), true);
                float y = 58 + cw * CardH + 12;
                var box = Tmp(new Panel { Position = new Vector2(24, y), Size = new Vector2(w - 48, 96) }, p);
                box.AddThemeStyleboxOverride("panel", Pill(new Color(0, 0, 0, 0.35f), new Color(1, 1, 1, 0.12f), 10));
                FitWrap(Put(box, c.Data.TcgDesc ?? c.Data.Desc, Cond, 19, new Color(1, 1, 1, 0.82f), new Vector2(14, 8), new Vector2(w - 76, 80)), new Vector2(w - 76, 80), 19);
                p.AddChild(Chips.Make("YES", new Vector2(40, y + 110), new Vector2(210, 48), none, 21));
                p.AddChild(Chips.Make("NO", new Vector2(w - 250, y + 110), new Vector2(210, 48), none, 21));
                await Hold(5);
            }),
            ("R2: Big card + YES / NO, full text in the left panel  ★", async () =>
            {
                var c = Of("Sangan");
                detail.Visible = true; Focus(c);
                const float w = 440, cw = 240;
                var p = MockSheet(w, 60 + cw * CardH + 84, "USE SANGAN'S EFFECT?");
                Art(p, c, new Rect2(w / 2 - cw / 2, 58, cw, cw * CardH), true);
                float y = 58 + cw * CardH + 14;
                p.AddChild(Chips.Make("YES", new Vector2(30, y), new Vector2(180, 48), none, 21));
                p.AddChild(Chips.Make("NO", new Vector2(w - 210, y), new Vector2(180, 48), none, 21));
                await Hold(5);
            }));

        // ---- 14 choose an effect. No card in the pool has 3 effects to pick from: Kunai with Chain has 2;
        // "declare" cards (DNA Surgery: 20 Types, Earthshaker: 6 Attributes) have many.
        string[] kunai = { "Change the attacker to Defense Position", "Equip to one of your monsters (+500 ATK)" };
        string[] races = { "Aqua", "Beast", "Beast-Warrior", "Dinosaur", "Dragon", "Fairy", "Fiend", "Fish", "Insect", "Machine", "Plant", "Pyro", "Reptile", "Rock", "Sea Serpent", "Spellcaster", "Thunder", "Warrior", "Winged Beast", "Zombie" };
        Add("14  Choose an effect",
            ("Original: Kunai with Chain", async () => { ShowSheet("KUNAI WITH CHAIN", null, kunai.Select(o => (o.ToUpperInvariant(), none, true)).ToList()); await Hold(4); }),
            ("R1: Stacked rows with full text (made-up card)", async () =>
            {
                var p = MockSheet(600, 300, "CHOOSE AN EFFECT");
                string[] o = { "Destroy 1 monster on the field", "Draw 1 card", "Gain 1000 Life Points" };
                for (int i = 0; i < 3; i++) p.AddChild(Chips.Make($"{i + 1}   {o[i]}", new Vector2(30, 62 + i * 70), new Vector2(540, 56), none, 21));
                await Hold(4);
            }),
            ("R1: Three tall option cards (made-up card)", async () =>
            {
                var p = MockSheet(720, 300, "CHOOSE AN EFFECT");
                string[] o = { "DESTROY", "DRAW", "GAIN LP" }, s = { "1 monster on the field", "1 card", "1000 Life Points" };
                for (int i = 0; i < 3; i++)
                {
                    var c = Tmp(new Panel { Position = new Vector2(30 + i * 226, 64), Size = new Vector2(208, 200) }, p);
                    c.AddThemeStyleboxOverride("panel", Pill(new Color(1, 1, 1, 0.05f), new Color(RivalPanels.Gold, 0.5f), 12));
                    Put(c, o[i], Impact, 34, RivalPanels.Gold, new Vector2(0, 54), new Vector2(208, 46), HorizontalAlignment.Center, 4);
                    Put(c, s[i], Cond, 18, new Color(1, 1, 1, 0.75f), new Vector2(0, 108), new Vector2(208, 26), HorizontalAlignment.Center);
                }
                await Hold(4);
            }),
            ("R2: Kunai with Chain: big card, one row per effect  ★", async () =>
            {
                var c = Of("Kunai with Chain");
                const float w = 620, cw = 200;
                var p = MockSheet(w, 60 + cw * CardH + 150, "KUNAI WITH CHAIN: CHOOSE AN EFFECT");
                Art(p, c, new Rect2(w / 2 - cw / 2, 58, cw, cw * CardH), true);
                for (int i = 0; i < 2; i++) p.AddChild(Chips.Make($"{i + 1}   {kunai[i].ToUpperInvariant()}", new Vector2(30, 58 + cw * CardH + 14 + i * 62), new Vector2(w - 60, 52), none, 19));
                await Hold(5);
            }),
            ("R2: DNA Surgery: big card + 20 Types grid  ★", async () =>
            {
                var c = Of("DNA Surgery");
                const float w = 960, cw = 250, ch = cw * CardH;
                var p = MockSheet(w, ch + 90, "DNA SURGERY: DECLARE A TYPE");
                Art(p, c, new Rect2(30, 62, cw, ch), true);
                float gx = cw + 60, gw = (w - gx - 30 - 3 * 10) / 4, gh = (ch - 4 * 10) / 5;
                for (int i = 0; i < races.Length; i++) p.AddChild(Chips.Make(races[i].ToUpperInvariant(), new Vector2(gx + i % 4 * (gw + 10), 62 + i / 4 * (gh + 10)), new Vector2(gw, gh), none, 18));
                await Hold(6);
            }));

        // ---- 15 target on the field
        Add("15  Pick a target on the field",
            ("Original (top sheet + red glow)  ★", async () =>
            {
                PutMon("Vorse Raider", 1, 1); PutMon("Feral Imp", 3, 1);
                ShowSheet("SELECT 1 MONSTER TO DESTROY  ·  CHOOSE 1", null, new(), top: true);
                foreach (var n in new[] { "Vorse Raider", "Feral Imp" }) hints[C(n).Card.Uid] = Hint.Target;
                foreach (var v in views.Values) Aura(v); await Hold(4); hints.Clear(); foreach (var v in views.Values) Aura(v);
            }),
            ("R1: Bottom instruction + counter + cancel", async () =>
            {
                PutMon("Vorse Raider", 1, 1); PutMon("Feral Imp", 3, 1);
                foreach (var n in new[] { "Vorse Raider", "Feral Imp" }) hints[C(n).Card.Uid] = Hint.Target;
                foreach (var v in views.Values) Aura(v);
                var p = TmpBox(new Rect2(W / 2 - 300, H * 0.62f, 600, 64), Pill(new Color(0.03f, 0.03f, 0.05f, 0.9f), Bad, 32, 0, null, 0, 2));
                Put(p, "DESTROY 1 MONSTER", Impact, 26, Colors.White, new Vector2(28, 14), new Vector2(300, 36), outline: 4);
                Put(p, "0 / 1", Impact, 26, Bad, new Vector2(330, 14), new Vector2(80, 36), HorizontalAlignment.Center);
                p.AddChild(Chips.Make("CANCEL", new Vector2(440, 9), new Vector2(140, 46), none, 18));
                await Hold(4); hints.Clear(); foreach (var v in views.Values) Aura(v);
            }),
            ("R1: Crosshair tags over each target", async () =>
            {
                var a = PutMon("Vorse Raider", 1, 1); var b = PutMon("Feral Imp", 3, 1); await Wait(0.3);
                foreach (var v in new[] { a, b })
                {
                    var at = cam.UnprojectPosition(v.Root.GlobalPosition);
                    var t = TmpBox(new Rect2(at + new Vector2(-55, -120), new Vector2(110, 36)), Pill(new Color(1f, 0.3f, 0.25f, 0.9f), Colors.White, 18));
                    Put(t, "◎ TARGET", Cond, 18, Colors.White, new Vector2(0, 5), new Vector2(110, 26), HorizontalAlignment.Center);
                }
                await Hold(4);
            }));

        // ---- 16 special summon from GY
        Add("16  Pick from GY / deck (hidden cards)",
            ("Original grid  ★", async () => { ShowSheet("SPECIAL SUMMON WHICH MONSTER?  ·  CHOOSE 1", Grave(), new() { ("CONFIRM", none, false) }); await Hold(4); }),
            ("R1: Strip + big preview + stats", async () =>
            {
                var g = Grave();
                var p = MockSheet(880, 400, "MONSTER REBORN  ·  CHOOSE 1");
                for (int i = 0; i < g.Count; i++) Art(p, g[i], new Rect2(24 + i * 92, 70, 84, 122)).Modulate = i == 5 ? Colors.White : new Color(1, 1, 1, 0.55f);
                Art(p, g[5], new Rect2(600, 60, 230, 320));
                Put(p, "SUMMONED SKULL", Impact, 30, Colors.White, new Vector2(24, 214), new Vector2(560, 40), outline: 4);
                Put(p, "ATK 2500   DEF 1200   ·   Level 6 DARK Fiend", Cond, 20, RivalPanels.Gold, new Vector2(24, 256), new Vector2(560, 28));
                p.AddChild(Chips.Make("SUMMON", new Vector2(24, 320), new Vector2(240, 50), none, 21));
                await Hold(5);
            }));

        // ---- 17 attack target
        Add("17  Choose attack target",
            ("Original top sheet  ★", async () =>
            {
                PutMon("Summoned Skull", 2, 0); PutMon("Vorse Raider", 2, 1);
                ShowSheet("SUMMONED SKULL: CHOOSE A TARGET", null, new() { ("DIRECT ATTACK", none, true), ("CANCEL", none, true) }, top: true); await Hold(4);
            }),
            ("R1: Win / lose preview on each target", async () =>
            {
                PutMon("Summoned Skull", 2, 0); var a = PutMon("Vorse Raider", 1, 1); var b = PutMon("Mystical Elf", 3, 1, def: true); await Wait(0.3);
                (View v, string t)[] tg = { (a, "WIN  ·  −600"), (b, "WIN  ·  DESTROY") };
                foreach (var (v, t) in tg)
                {
                    var at = cam.UnprojectPosition(v.Root.GlobalPosition);
                    var tag = TmpBox(new Rect2(at + new Vector2(-80, -125), new Vector2(160, 34)), Pill(new Color(0.03f, 0.06f, 0.04f, 0.9f), Good, 17));
                    Put(tag, t, Cond, 18, Good, new Vector2(0, 4), new Vector2(160, 26), HorizontalAlignment.Center);
                }
                await Hold(4);
            }),
            ("NEW: No sheet: targets glow, one hint line, right-click cancels", async () =>
            {
                // In game the sheet only shows when there IS a monster to hit (no targets = direct attack at once);
                // it carries CANCEL, and DIRECT ATTACK only when a card lets you go past their monsters.
                PutMon("Summoned Skull", 2, 0); var a = PutMon("Vorse Raider", 1, 1); var b = PutMon("Mystical Elf", 3, 1, def: true); await Wait(0.3);
                foreach (var v in new[] { a, b }) { hints[v.Card.Uid] = Hint.Target; Aura(v); }
                Caption("SUMMONED SKULL  ·  CHOOSE A TARGET  ·  RIGHT-CLICK TO CANCEL", RivalPanels.Gold, 0.62f);
                await Hold(4); hints.Clear(); foreach (var v in new[] { a, b }) Aura(v);
            }));

        // ---- 18 cards revealed / discarded. Effects that show cards to both players (The Stern Mystic, Graceful
        // Dice...) raised the reveal twice; fixed in game in round 2 (once, titled by whose cards).
        List<View> TheirHand()
        {
            var l = new[] { ("Feral Imp", 0), ("Gaia The Fierce Knight", 0), ("Mystical Elf", 1) }.Select(x => C(x.Item1, x.Item2)).ToList();
            for (int i = 0; i < 3; i++) Place(l[i], new St(Loc.Hand, i, 3, false, false, 1, 0, 0));
            return l;
        }
        async Task<List<View>> LiftTheirHand()
        {
            var hand = TheirHand(); await Wait(0.3);
            for (int i = 0; i < 3; i++) Move(hand[i], hand[i].S with { Up = true }, i * 0.08f, 0.3f, 0.3f);
            await Wait(0.55);
            foreach (var v in hand) v.Front = true;
            for (int i = 0; i < 3; i++) Lift(hand[i], new Vector3(0, 2.2f, 2.2f), 1.35f, 0.3, Grid3(new St(Loc.Monster, i + 1, 0, false, false, 1, 0, 0)));
            await Wait(0.3);
            return hand;
        }
        /// NEW Confiscation: the hand rises off its row as flat full-colour cards in a neat line (theirs flip
        /// face-up on the way), the Confiscation that caused it shown at the left, a caption says what is
        /// happening; the chosen card gets a red ring and burns away as in F / G, the rest go home.
        async Task Confiscate(List<View> hand, int side, int pick, bool youPick)
        {
            const float cw = 180, gap = 36;
            var cause = Put("Confiscation", SpellZ(2, 1 - side, true)); await Wait(0.2);
            GoldRays(cause, side == 1 ? FxGold : FxPink);
            await Wait(0.4);
            var from = hand.Select(OnScreen).ToList();
            float y = side == 1 ? H * 0.3f : H * 0.5f, x0 = W / 2 - (hand.Count - 1) * (cw + gap) / 2;
            var cards = new List<Control>();
            for (int i = 0; i < hand.Count; i++)
            {
                var c = FlatCard(side == 1 ? backs[Side(1)] : CardArt.Large(hand[i].Card.Data), from[i].At, cw);
                c.Scale = Vector2.One * (from[i].W / cw);
                hand[i].Root.Visible = false;
                var to = new Vector2(x0 + i * (cw + gap), y) - c.Size / 2;
                var tw = CreateTween();
                tw.TweenInterval(i * 0.06);
                tw.TweenProperty(c, "position", to, 0.34).SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
                tw.Parallel().TweenProperty(c, "scale", Vector2.One, 0.34).SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
                if (side == 1)
                {
                    var art = c.GetNode<TextureRect>("art"); var card = hand[i].Card;
                    tw.TweenProperty(c, "scale:x", 0f, 0.1);
                    tw.TweenCallback(Callable.From(() => art.Texture = CardArt.Large(card.Data)));
                    tw.TweenProperty(c, "scale:x", 1f, 0.12);
                }
                cards.Add(c);
            }
            // The cause, small, left of the line.
            var src = FlatCard(CardArt.Large(cause.Card.Data), new Vector2(x0 - cw - 60, y), cw * 0.62f);
            Put(src, "CONFISCATION", Cond, 18, RivalPanels.Gold, new Vector2(-30, src.Size.Y + 6), new Vector2(src.Size.X + 60, 24), HorizontalAlignment.Center, 4);
            FadeIn(src, 0.25f, new Vector2(-40, 0));
            await Wait(0.5);
            Caption(youPick ? "CONFISCATION  ·  CHOOSE 1" : $"{them}'S CONFISCATION",side == 1 ? RivalPanels.Gold : Pm.Them, side == 1 ? 0.5f : 0.3f);
            // Your pick: the cards lift a little under the cursor; the rival's: a short look along the line.
            for (int i = 0; i <= pick; i++)
            {
                var c = cards[i];
                CreateTween().TweenProperty(c, "position:y", c.Position.Y - 16, 0.12);
                await Wait(0.45);
                if (i < pick) CreateTween().TweenProperty(c, "position:y", c.Position.Y + 16, 0.12);
            }
            var gone = cards[pick];
            gone.GetNode<Control>("ring").Visible = true;
            Cue("pick");
            await Wait(0.5);
            for (int i = 0; i < cards.Count; i++) if (i != pick) CreateTween().TweenProperty(cards[i], "modulate", new Color(1, 1, 1, 0.45f), 0.2);
            // The burn of F / G: the real card takes the flat copy's place (same spot and size, facing the camera),
            // burns away and its embers drift to the owner's GY.
            var burnt = hand[pick];
            float px = GetViewport().GetVisibleRect().Size.Y / (2 * Mathf.Tan(Mathf.DegToRad(cam.Fov) / 2));   // pixels per unit at depth 1
            float depth = CW * px / cw;
            burnt.Tw?.Kill(); Lit(burnt, false);
            burnt.Face.AlbedoTexture = CardArt.Large(burnt.Card.Data); burnt.Face.AlbedoColor = new Color(0.84f, 0.84f, 0.84f); burnt.Face.Emission = Colors.Black;
            burnt.Root.GlobalTransform = new Transform3D(cam.GlobalBasis * new Basis(Vector3.Right, Mathf.Pi / 2), cam.ProjectPosition(gone.Position + gone.Size / 2, depth));
            burnt.Root.Visible = true; gone.Visible = false;
            DiscardFx(burnt, GyPos(side));
            await Wait(0.9);
            Place(burnt, GyAt(side));
            // The rest drop back into the hand (theirs flip face-down again).
            for (int i = 0; i < cards.Count; i++)
            {
                if (i == pick) continue;
                var c = cards[i]; var v = hand[i]; var home = from[i];
                var tw = CreateTween();
                tw.TweenProperty(c, "modulate", Colors.White, 0.1);
                if (side == 1)
                {
                    var art = c.GetNode<TextureRect>("art");
                    tw.TweenProperty(c, "scale:x", 0f, 0.1);
                    tw.TweenCallback(Callable.From(() => art.Texture = backs[Side(1)]));
                    tw.TweenProperty(c, "scale:x", 1f, 0.1);
                }
                tw.TweenProperty(c, "position", home.At - c.Size / 2, 0.3).SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.In);
                tw.Parallel().TweenProperty(c, "scale", Vector2.One * (home.W / cw), 0.3);
                tw.TweenCallback(Callable.From(() => { v.Root.Visible = true; c.Visible = false; }));
            }
            CreateTween().TweenProperty(src, "modulate:a", 0f, 0.25);
            await Hold(0.9);
        }
        Add("18  Cards revealed / discarded",
            ("Original (fixed in round 2: shown once)", async () => { TheirHand(); await Wait(0.3); ShowCards($"{them}'S CARDS  ·  REVEALED", TheirHand().Select(v => v.Card).ToList(), 3); await Hold(3.4); }),
            ("R1: Cards fly to their GY with a caption", async () =>
            {
                var g = Grave().Take(3).ToList();
                var cap = Put(hud, $"{them} DISCARDED 3", Cond, 26, Colors.White, new Vector2(0, H * 0.28f), new Vector2(W, 34), HorizontalAlignment.Center, 5);
                uiTemp.Add(cap);
                for (int i = 0; i < 3; i++)
                {
                    var a = Art(hud, g[i], new Rect2(W / 2 - 200 + i * 140, H * 0.34f, 120, 176));
                    CreateTween().TweenProperty(a, "position", new Vector2(W - 200, 120), 0.5).SetDelay(1.6 + i * 0.12).SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.In);
                    CreateTween().TweenProperty(a, "scale", new Vector2(0.3f, 0.3f), 0.5).SetDelay(1.6 + i * 0.12);
                }
                await Hold(2.6);
            }),
            ("R2: The cards themselves flip up and lift, then go back  ★ (normal reveal)", async () =>
            {
                var hand = TheirHand(); var set = Put("Trap Hole", SpellZ(2, 1, false)); await Wait(0.4);
                var all = hand.Append(set).ToList();
                var homes = all.Select(v => (v, v.S)).ToList();
                foreach (var (v, i) in all.Select((v, i) => (v, i))) Move(v, v.S with { Up = true }, i * 0.08f, 0.3f, 0.3f);
                await Wait(0.65);
                foreach (var v in all) v.Front = true;
                for (int i = 0; i < all.Count; i++) Lift(all[i], new Vector3(0, 2.2f, i < 3 ? 2.2f : 0.6f), 1.35f, 0.3, Grid3(new St(i < 3 ? Loc.Monster : Loc.Spell, i < 3 ? i + 1 : 2, 0, false, false, 1, 0, 0)));
                Caption($"{them}'S CARDS  ·  REVEALED", Pm.Them);
                await Wait(2.4);
                foreach (var (v, s) in homes) CreateTween().TweenProperty(v.Root, "transform", Target(v, s), 0.35).SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.InOut);
                await Wait(0.35);
                foreach (var (v, s) in homes) Place(v, s);
                await Hold(0.4);
            }),
            ("R2: Fan out of their side of the screen", async () =>
            {
                var hand = TheirHand(); await Wait(0.3);
                const float cw = 170, ch = cw * CardH;
                var fan = Tmp(new Control { Position = new Vector2(0, -ch - 120), Size = new Vector2(W, ch + 100), MouseFilter = MouseFilterEnum.Ignore });
                for (int i = 0; i < 3; i++)
                {
                    var a = Art(fan, hand[i].Card, new Rect2(W / 2 - cw / 2 + (i - 1) * 150, 30, cw, ch), true);
                    a.PivotOffset = new Vector2(cw / 2, -260); a.RotationDegrees = (1 - i) * 6;
                }
                Put(fan, $"{them}'S HAND  ·  REVEALED", Cond, 26, Pm.Them, new Vector2(0, ch + 56), new Vector2(W, 34), HorizontalAlignment.Center, 5);
                var tw = CreateTween();
                tw.TweenProperty(fan, "position:y", 60f, 0.35).SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
                tw.TweenInterval(2.2);
                tw.TweenProperty(fan, "position:y", -ch - 120, 0.3).SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.In);
                await Hold(3.1);
            }),
            ("R2: Cause + cards: Confiscation looks, one is discarded", async () =>
            {
                var hand = TheirHand(); await Wait(0.3);
                const float cw = 150, ch = cw * CardH, w = 860;
                var p = MockSheet(w, ch + 110, "");
                Art(p, Of("Confiscation"), new Rect2(30, 40, cw * 0.8f, ch * 0.8f), true);
                Put(p, "CONFISCATION", Cond, 20, RivalPanels.Gold, new Vector2(10, 40 + ch * 0.8f + 8), new Vector2(cw * 0.8f + 40, 26), HorizontalAlignment.Center);
                Put(p, "›", Impact, 80, new Color(RivalPanels.Gold, 0.7f), new Vector2(cw * 0.8f + 46, ch / 2 - 30), new Vector2(60, 100), HorizontalAlignment.Center);
                Put(p, $"{them}'S HAND", Cond, 22, Colors.White, new Vector2(240, 14), new Vector2(w - 260, 30), HorizontalAlignment.Center);
                var arts = new List<TextureRect>();
                for (int i = 0; i < 3; i++) arts.Add(Art(p, hand[i].Card, new Rect2(250 + i * (cw + 50), 54, cw, ch), true));
                await Wait(2);
                var gone = arts[1];
                var tag = Put(p, "DISCARDED", Impact, 30, Bad, gone.Position + new Vector2(-20, ch / 2 - 20), new Vector2(cw + 40, 40), HorizontalAlignment.Center, 6);
                uiTemp.Add(tag);
                arts[0].Modulate = arts[2].Modulate = new Color(1, 1, 1, 0.35f);
                await Wait(0.8);
                var gy = cam.UnprojectPosition(GyPos(1)) - p.Position;
                var tw = CreateTween().SetParallel();
                tw.TweenProperty(gone, "position", gy - new Vector2(cw / 4, ch / 4), 0.45).SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.In);
                tw.TweenProperty(gone, "scale", new Vector2(0.5f, 0.5f), 0.45);
                tw.TweenProperty(tag, "modulate:a", 0f, 0.2);
                await Hold(0.9);
            }),
            ("R2: You use Confiscation: their hand lifts, top sheet + red glow", async () =>
            {
                var hand = await LiftTheirHand();
                ShowSheet("CONFISCATION: DISCARD 1 CARD  ·  CHOOSE 1", null, new(), top: true);
                foreach (var v in hand) hints[v.Card.Uid] = Hint.Target;
                foreach (var v in hand) Aura(v);
                await Wait(2.2);
                hints.Clear(); foreach (var v in hand) Aura(v); CloseSheet();
                var gone = hand[1];
                DiscardFx(gone, GyPos(1)); await Wait(0.5);
                Place(gone, GyAt(1));
                await Wait(0.4);
                foreach (var v in hand.Where(v => v != gone)) Move(v, v.S with { Up = false }, 0, 0.35f, 0.3f);
                await Hold(0.6);
            }),
            ("R2: Rival uses it on you: their pick burns away in your hand", async () =>
            {
                var mine = new[] { "Dark Magician", "Mirror Force", "Pot of Greed" }.Select((n, i) => Put(n, HandAt(i, 3))).ToList();
                await Wait(0.4);
                Caption($"{them}'S CONFISCATION  ·  DISCARDS MIRROR FORCE", Pm.Them, 0.5f);
                hints[mine[1].Card.Uid] = Hint.Target; Aura(mine[1]);
                await Wait(1.2);
                hints.Clear(); Aura(mine[1]);
                DiscardFx(mine[1], GyPos(0)); await Wait(0.5);
                Place(mine[1], GyAt(0));
                await Hold(1);
            }),
            ("NEW: You use Confiscation: their hand rises in a line, you pick, it burns", async () =>
                await Confiscate(TheirHand(), 1, 1, true)),
            ("NEW: Rival uses it on you: your hand rises, they pick, it burns", async () =>
                await Confiscate(new[] { "Dark Magician", "Mirror Force", "Pot of Greed" }.Select((n, i) => Put(n, HandAt(i, 3))).ToList(), 0, 1, false)));

        // ---- 19 dice
        Add("19  Dice roll",
            ("Original", async () => { PutMon("Summoned Skull", 2, 0); DiceCup(new Ev { Kind = EvKind.Die, Player = Me, Other = Of("Graceful Dice"), Amount = 5 }); await Hold(4); }),
            ("R1: Original + result line", async () =>
            {
                PutMon("Summoned Skull", 2, 0); DiceCup(new Ev { Kind = EvKind.Die, Player = Me, Other = Of("Graceful Dice"), Amount = 5 }); await Wait(2.2);
                var p = TmpBox(new Rect2(W / 2 - 200, H * 0.68f, 400, 60), Pill(new Color(0.03f, 0.03f, 0.05f, 0.9f), RivalPanels.Gold, 30, 0, null, 0, 2));
                Put(p, "ROLLED 5  ·  EFFECT APPLIES", Impact, 26, RivalPanels.Gold, new Vector2(0, 12), new Vector2(400, 36), HorizontalAlignment.Center, 4);
                FadeIn(p, 0.2f, new Vector2(0, 30)); await Hold(2);
            }),
            ("R2: Chip like the coin's  ★", async () =>
            {
                PutMon("Summoned Skull", 2, 0);
                DiceCup(new Ev { Kind = EvKind.Die, Player = Me, Other = Of("Graceful Dice"), Amount = 5 }, false);
                await Wait(2.4); ResultChip("GRACEFUL DICE  ·  ROLLED 5", RivalPanels.Gold); await Hold(1.6);
            }),
            ("R2: Chip says what the roll does", async () =>
            {
                PutMon("Summoned Skull", 2, 0);
                DiceCup(new Ev { Kind = EvKind.Die, Player = Me, Other = Of("Graceful Dice"), Amount = 5 }, false);
                await Wait(2.4); ResultChip("ROLLED 5  ·  +500 ATK / DEF", RivalPanels.Gold); await Hold(1.6);
            }));

        // ---- 20 chain stack
        Add("20  Chain display",
            ("Original (corner numbers on cards)  ★", async () =>
            {
                var a = Put("Trap Hole", SpellZ(2, 1, false)); var b = Put("Mirror Force", SpellZ(2, 0, false));
                await LabChain(new[] { a, b }, false); await Hold(1.5);
            }),
            ("R1: + chain list on the right", async () =>
            {
                var a = Put("Trap Hole", SpellZ(2, 1, false)); var b = Put("Mirror Force", SpellZ(2, 0, false));
                var p = TmpBox(new Rect2(W - 300, H * 0.3f, 276, 190), Edge(RivalPanels.Gold * new Color(1, 1, 1, 0.6f), 0.2f));
                Put(p, "CHAIN", Cond, 20, RivalPanels.Gold, new Vector2(16, 10), new Vector2(200, 26));
                (string n, string who)[] l = { ("Mirror Force", "YOU"), ("Trap Hole", them) };
                for (int i = 0; i < 2; i++)
                {
                    Art(p, Of(l[i].n), new Rect2(16, 44 + i * 70, 40, 58));
                    Put(p, $"{2 - i}  {l[i].n.ToUpperInvariant()}", Impact, 22, Colors.White, new Vector2(66, 46 + i * 70), new Vector2(200, 30), outline: 4);
                    Put(p, l[i].who, Cond, 16, new Color(1, 1, 1, 0.55f), new Vector2(84, 74 + i * 70), new Vector2(180, 22));
                }
                FadeIn(p); await LabChain(new[] { a, b }, false); await Hold(2);
            }));

        // ---- 21 card detail
        Add("21  Card detail panel",
            ("Original (left panel)  ★", async () => { detail.Visible = true; Focus(Of("Summoned Skull")); await Hold(4); }),
            ("R1: Big art beside the text", async () =>
            {
                var c = Of("Summoned Skull");
                var p = TmpBox(new Rect2(24, 24, 520, 300), Edge(RivalPanels.Gold * new Color(1, 1, 1, 0.45f)));
                Art(p, c, new Rect2(14, 14, 186, 272));
                Put(p, c.Name.ToUpperInvariant(), Impact, 28, Colors.White, new Vector2(214, 14), new Vector2(290, 40), outline: 4);
                Put(p, "★★★★★★  DARK  ·  Fiend", Cond, 18, RivalPanels.Gold, new Vector2(214, 56), new Vector2(290, 24));
                Put(p, "ATK 2500   DEF 1200", Impact, 24, Colors.White, new Vector2(214, 86), new Vector2(290, 32));
                Put(p, "A fiend with dark powers for confusing the enemy. Among the Fiend-Type monsters, this monster boasts considerable force.", Cond, 18, new Color(1, 1, 1, 0.75f), new Vector2(214, 126), new Vector2(290, 150)).AutowrapMode = TextServer.AutowrapMode.WordSmart;
                await Hold(5);
            }));

        // ---- 22 win / lose (a plain LP win has no subtitle in game)
        Add("22  Duel end",
            ("Original banner  ★", async () => { await Banner("YOU WIN", RivalPanels.Gold, ""); await Hold(0.5); }),
            ("R1: Result card with stats", async () =>
            {
                Tmp(new ColorRect { Size = new Vector2(W, H), Color = new Color(0, 0, 0, 0.55f) });
                var p = MockSheet(560, 400, "");
                Put(p, "VICTORY", Impact, 72, RivalPanels.Gold, new Vector2(0, 20), new Vector2(560, 90), HorizontalAlignment.Center, 8);
                Put(p, "TURNS 7      LP LEFT 3200      FINISHER: SUMMONED SKULL\n\n+120 DP     ·     NEW CARD: MIRROR FORCE", Cond, 21, Colors.White, new Vector2(30, 130), new Vector2(500, 140), HorizontalAlignment.Center);
                p.AddChild(Chips.Make("CONTINUE", new Vector2(160, 320), new Vector2(240, 52), none, 22));
                await Hold(5);
            }),
            ("NEW: Shockwave title: VICTORY slams in, DEFEAT crashes down  ★", async () => { await EndSetup(1); Cue("banner_win"); await EndTitle("VICTORY", RivalPanels.Gold, true, $"{them} DEFEATED"); await EndSetup(0); Sfx("SE_DUEL_LOSE"); await EndTitle("DEFEAT", Bad, false, $"{them} WINS"); }),
            ("NEW: Winner cut-in: big portrait, the loser's cracks and falls", async () => { await EndSetup(1); await EpicCutIn(0); await EndSetup(0); await EpicCutIn(1); }),
            ("NEW: The word rises out of the field in a summoning circle", async () => { await EndSetup(1); await EpicField(true); await EndSetup(0); await EpicField(false); }));


        // ---- 22 (NEW): each plays your win, then your loss. The loser's monsters shatter first, as in game.
        async Task EndSetup(int loser)
        {
            await Hold(0.1);
            foreach (var (n, z) in new[] { ("Vorse Raider", 1), ("Mystical Elf", 3) }) PutMon(n, z, loser);
            PutMon("Summoned Skull", 2, 1 - loser);
            await Wait(0.4);
            await FieldShatters(loser);
        }
        TextureRect Glow2(string tex, Vector2 at, float size, Color c)
        {
            var r = Tmp(new TextureRect { Texture = VfxTex(tex), ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.Scale, Size = new Vector2(size, size), Position = at - new Vector2(size / 2, size / 2), PivotOffset = new Vector2(size / 2, size / 2), Material = AddMat, Modulate = c, MouseFilter = MouseFilterEnum.Ignore });
            return r;
        }
        ColorRect Shade2(float a, double dur)
        {
            var d = Tmp(new ColorRect { Size = new Vector2(W, H), Color = new Color(0, 0, 0, 0), MouseFilter = MouseFilterEnum.Ignore });
            CreateTween().TweenProperty(d, "color:a", a, dur);
            return d;
        }
        async Task FadeAll(double dur = 0.35)
        {
            foreach (var n in uiTemp.OfType<CanvasItem>().Where(n => IsInstanceValid(n) && n.GetParent() == hud)) CreateTween().TweenProperty(n, "modulate:a", 0f, dur);
            foreach (var n in uiTemp.OfType<Label3D>().Where(IsInstanceValid)) CreateTween().TweenProperty(n, "modulate:a", 0f, dur);
            await Wait(dur);
        }

        async Task EpicCutIn(int winner)
        {
            int loser = 1 - winner;
            var col = winner == 0 ? RivalPanels.Gold : Pm.Them;
            if (winner == 0) Cue("banner_win"); else Sfx("SE_DUEL_LOSE");
            Shade2(0.55f, 0.25);
            float cy = H * 0.46f, dir = winner == 0 ? 1 : -1;
            // Three diagonal slashes cut across, the middle one is the stage.
            foreach (var (h, off, a, delay) in new[] { (10f, -190f, 1f, 0.0), (300f, 0f, 0.93f, 0.06), (10f, 190f, 1f, 0.12) })
            {
                var band = Tmp(new ColorRect { Size = new Vector2(W * 1.8f, h), Position = new Vector2(-W * 0.4f, cy + off - h / 2), PivotOffset = new Vector2(W * 0.9f, h / 2), RotationDegrees = -8, Color = h > 50 ? new Color(0.02f, 0.02f, 0.03f, a) : col, MouseFilter = MouseFilterEnum.Ignore });
                var home = band.Position.X; band.Position = new Vector2(home - dir * W * 1.8f, band.Position.Y);
                var t = CreateTween(); t.TweenInterval(delay);
                t.TweenProperty(band, "position:x", home, 0.28).SetTrans(Tween.TransitionType.Expo).SetEase(Tween.EaseType.Out);
            }
            await Wait(0.25);
            // The winner, big, behind light rays.
            float big = 420, px = winner == 0 ? W * 0.26f : W * 0.74f;
            var rays = Glow2("md/rays", new Vector2(px, cy), 1100, new Color(col, 0));
            var rt = CreateTween().SetParallel(); rt.TweenProperty(rays, "modulate:a", 0.55f, 0.3); rt.TweenProperty(rays, "rotation", -0.6f, 3.5);
            var av = (Control)profile[winner].GetChild(0).Duplicate();
            Tmp(av);
            av.Scale = Vector2.One * (big / ProfH); av.Position = new Vector2(px - big / 2, cy - big / 2);
            var ah = av.Position.X; av.Position = new Vector2(ah - dir * 500, av.Position.Y); av.Modulate = new Color(1, 1, 1, 0);
            var at = CreateTween().SetParallel();
            at.TweenProperty(av, "position:x", ah, 0.35).SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
            at.TweenProperty(av, "modulate:a", 1f, 0.15);
            Flash(new Vector3(0, 1, 0), col, 3);
            Shake(0.04f);
            // Name and WINNER on the stage, on the far side of the portrait.
            float tx = winner == 0 ? W * 0.26f + big / 2 + 30 : W * 0.06f, tw = W * 0.68f - big / 2 - 30;
            var align = winner == 0 ? HorizontalAlignment.Left : HorizontalAlignment.Right;
            var w1 = Put(hud, "WINNER", Impact, 150, col, new Vector2(tx, cy - 125), new Vector2(tw, 170), align, 14);
            var w2 = Put(hud, winner == 0 ? Settings.PlayerName.ToUpperInvariant() : them, Cond, 46, Colors.White, new Vector2(tx, cy + 44), new Vector2(tw, 56), align, 6);
            uiTemp.Add(w1); uiTemp.Add(w2);
            FadeIn(w1, 0.2f, new Vector2(dir * 140, 0)); FadeIn(w2, 0.3f, new Vector2(dir * 80, 0));
            // The loser's own profile box: greys out, cracks, falls away (Hold brings it back).
            var lb = profile[loser];
            var lp = lb.Position; float small = lb.Size.Y;
            await Wait(0.5);
            CreateTween().TweenProperty(lb, "modulate", new Color(0.35f, 0.35f, 0.38f), 0.3);
            await Wait(0.35);
            var cr = Glow2("md/crack", lp + lb.Size / 2, small * 2.2f, new Color(1, 0.4f, 0.35f));
            Cue("crack"); Shake(0.03f);
            await Wait(0.4);
            var fall = CreateTween().SetParallel();
            fall.TweenProperty(lb, "position:y", lp.Y + 120, 0.5).SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.In);
            fall.TweenProperty(lb, "rotation", loser == 0 ? -0.3f : 0.3f, 0.5);
            fall.TweenProperty(lb, "modulate:a", 0f, 0.5);
            fall.TweenProperty(cr, "modulate:a", 0f, 0.5);
            await Wait(2.0);
            await FadeAll();
            lb.Position = lp; lb.Rotation = 0; lb.Modulate = Colors.White;
        }

        async Task EpicField(bool win)
        {
            var col = win ? RivalPanels.Gold : Bad;
            var fx = win ? FxGold : FxEmber;
            if (win) Cue("banner_win"); else Sfx("SE_DUEL_LOSE");
            var mid = new Vector3(0, 0, 0.3f);
            Cue("ritual_circle");
            Decal("md/magic", fx, mid, new Vector2(5.2f, 5.2f), 0.3f, 1f, 3.4, spin: 1.2f);
            ZoneWave(0, fx, true, 1.4); ZoneWave(1, fx, true, 1.4);
            Vignette(win ? FxGold : new Color(0.55f, 0.04f, 0.04f), 3);
            await Wait(0.35);
            var word = Tmp(new Label3D { Text = win ? "VICTORY" : "DEFEAT", Font = Impact, FontSize = 150, PixelSize = 0.0105f, Billboard = BaseMaterial3D.BillboardModeEnum.Enabled, Modulate = new Color(col * 0.8f, 0), OutlineSize = 26, OutlineModulate = new Color(0, 0, 0, 0.85f), Shaded = false, NoDepthTest = true, RenderPriority = 6, OutlineRenderPriority = 5, Position = mid + new Vector3(0, -0.6f, 0) }, world);
            var t = CreateTween().SetParallel();
            t.TweenProperty(word, "modulate:a", 1f, 0.25);
            t.TweenProperty(word, "position:y", 1.7f, 0.7).SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
            Glint("md/rays", col, mid + new Vector3(0, 1.7f, 0), 1500, 0.5f, 1.1f, 3.0, spin: 0.4f, alpha: 0.55f);
            After(0.45, () => { Glint("md/lensflare", col, mid + new Vector3(0, 1.7f, 0), 1100, 0.3f, 1.3f, 0.9); Flash(mid + Vector3.Up * 1.7f, col, 4); Shake(0.05f); });
            await Wait(0.8);
            var sub = Tmp(new Label3D { Text = win ? $"{them} DEFEATED" : $"{them} WINS", Font = Cond, FontSize = 70, PixelSize = 0.0105f, Billboard = BaseMaterial3D.BillboardModeEnum.Enabled, Modulate = new Color(1, 1, 1, 0), OutlineSize = 14, OutlineModulate = new Color(0, 0, 0, 0.8f), Shaded = false, NoDepthTest = true, RenderPriority = 8, OutlineRenderPriority = 7, Position = mid + new Vector3(0, 0.15f, 0) }, world);
            CreateTween().TweenProperty(sub, "modulate:a", 1f, 0.3);
            for (int i = 0; i < 14; i++)
            {
                var a = mid + new Vector3((float)GD.RandRange(-3, 3), 0, (float)GD.RandRange(-2, 2));
                After(i * 0.06, () => Mote(a, a + new Vector3(0, 2.5f, 0), fx, 1.2, 0.08f, 0.2f));
            }
            await Wait(2.4);
            await FadeAll();
        }
    }

    Panel MockSheet(float w, float h, string title)
    {
        var p = TmpBox(new Rect2(W / 2 - w / 2, H * 0.45f - h / 2, w, h), Edge(RivalPanels.Gold * new Color(1, 1, 1, 0.8f), 0.25f));
        p.MouseFilter = MouseFilterEnum.Stop;
        if (title.Length > 0) Put(p, title, Cond, 26, Colors.White, new Vector2(20, 14), new Vector2(w - 40, 34), HorizontalAlignment.Center);
        FadeIn(p);
        return p;
    }

    /// Profile box layouts 1-3 (round 1) drawn over the hidden real box (avatar copied from it).
    void MockProfile(int s, int kind)
    {
        profile[s].Visible = false;
        var col = s == 0 ? Pm.You : Pm.Them;
        string name = s == 0 ? Settings.PlayerName.ToUpperInvariant() : ThemName.ToUpperInvariant(), lp = s == 0 ? "6400" : "8000";
        float fill = s == 0 ? 0.8f : 1f;
        string counts = s == 0 ? "HAND 4   DECK 32   GY 3   FUSION 1" : "HAND 5   DECK 30   GY 2   FUSION 0";
        Control Avatar(Control parent, float size, Vector2 at)
        {
            var a = (Control)profile[s].GetChild(0).Duplicate();
            a.Scale = Vector2.One * (size / ProfH); a.Position = at; parent.AddChild(a); return a;
        }
        void Bar(Control parent, Rect2 r)
        {
            Tmp(new ColorRect { Position = r.Position, Size = r.Size, Color = new Color(0.1f, 0.1f, 0.13f) }, parent);
            Tmp(new ColorRect { Position = r.Position, Size = new Vector2(r.Size.X * fill, r.Size.Y), Color = col }, parent);
        }
        bool me = s == 0;
        switch (kind)
        {
            case 1:   // wide strip
            {
                var size = new Vector2(560, 104);
                var p = TmpBox(new Rect2(me ? new Vector2(24, H - size.Y - 24) : new Vector2(W - size.X - 24, 24), size), Glass(col * new Color(1, 1, 1, 0.4f)));
                Avatar(p, 92, new Vector2(me ? 6 : size.X - 98, 6));
                float x = me ? 110 : 20;
                Put(p, name, Impact, 26, col, new Vector2(x, 6), new Vector2(240, 34), outline: 5);
                Put(p, lp, Impact, 52, Colors.White, new Vector2(x + 220, -2), new Vector2(210, 64), HorizontalAlignment.Right, 8);
                Bar(p, new Rect2(x, 62, 430, 8));
                Put(p, counts, Cond, 17, new Color(1, 1, 1, 0.7f), new Vector2(x, 74), new Vector2(430, 24));
                break;
            }
            case 2:   // LP first
            {
                var size = new Vector2(380, 110);
                var p = TmpBox(new Rect2(me ? new Vector2(110, H - size.Y - 30) : new Vector2(W - size.X - 110, 30), size), Glass(col * new Color(1, 1, 1, 0.5f), 0.25f));
                Avatar(hud, 150, new Vector2(me ? 24 : W - 174, me ? H - 184 : 10)).ZIndex = 1;
                uiTemp.Add(hud.GetChild(hud.GetChildCount() - 1));
                float x = me ? 70 : 20;
                Put(p, "LP", Cond, 22, RivalPanels.Gold, new Vector2(x, 34), new Vector2(40, 28));
                Put(p, lp, Impact, 80, Colors.White, new Vector2(x + 30, -6), new Vector2(260, 96), outline: 10);
                Bar(p, new Rect2(x, 92, 290, 6));
                var tag = TmpBox(new Rect2(p.Position + new Vector2(me ? 70 : 20, -30), new Vector2(240, 34)), Pill(col, col, 6));
                Put(tag, name, Cond, 20, Colors.White, new Vector2(10, 3), new Vector2(220, 28));
                var cn = TmpBox(new Rect2(p.Position + new Vector2(0, me ? -64 : size.Y + 6), new Vector2(380, 30)), Pill(new Color(0, 0, 0, 0.6f), new Color(1, 1, 1, 0.15f), 15));
                Put(cn, counts, Cond, 16, new Color(1, 1, 1, 0.75f), new Vector2(0, 4), new Vector2(380, 22), HorizontalAlignment.Center);
                break;
            }
            default:   // compact
            {
                var at = me ? new Vector2(24, H - 230) : new Vector2(W - 184, 24);
                var holder = Tmp(new Control { Position = at, Size = new Vector2(160, 206), MouseFilter = MouseFilterEnum.Ignore });
                Avatar(holder, 140, new Vector2(10, 0));
                var pill = TmpBox(new Rect2(at + new Vector2(0, 118), new Vector2(160, 52)), Pill(new Color(0.03f, 0.03f, 0.05f, 0.92f), col, 26, 0, new Color(col, 0.4f), 12, 2));
                Put(pill, lp, Impact, 38, Colors.White, new Vector2(0, 2), new Vector2(160, 48), HorizontalAlignment.Center, 6);
                Put(holder, name, Cond, 18, col, new Vector2(-20, 176), new Vector2(200, 24), HorizontalAlignment.Center, 4);
                var cn = TmpBox(new Rect2(at + new Vector2(me ? 172 : -132, 128), new Vector2(120, 78)), Pill(new Color(0, 0, 0, 0.6f), new Color(1, 1, 1, 0.15f), 10));
                Put(cn, counts.Replace("   ", "\n").Replace("FUSION", "EX"), Cond, 14, new Color(1, 1, 1, 0.75f), new Vector2(10, 2), new Vector2(110, 76));
                break;
            }
        }
    }
}
