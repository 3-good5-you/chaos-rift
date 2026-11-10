using Godot;
using PoC.Engine;

namespace PoC.Game.UI;

/// The battle UI picked in the UI Lab (game/ui_picks_round3.txt): activations lift the card with an info plate beside
/// it; chain and effect prompts are built around the card; coins and dice share one result chip; turn and phase words
/// lie on the field; a revealed hand rises in a line (picked from there, the pick burns away); the duel ends on a
/// slammed title. The UI Lab shows these same pieces.
public partial class DuelField
{
    static readonly Color Good = new(0.45f, 1f, 0.55f), Bad = new(1f, 0.42f, 0.36f);
    const float CardH = 1.458f;   // card height / width

    /// A passing HUD / world piece: in the UI Lab the variant's Hold clears it, in a duel it frees itself after `life`
    /// (0 = the caller frees it).
    T Track<T>(T n, double life) where T : Node
    {
        if (uiLab) uiTemp.Add(n);
        else if (life > 0) After(life, () => { if (IsInstanceValid(n)) n.QueueFree(); });
        return n;
    }

    /// Fades a piece out `at` seconds from now (skipped if it is gone by then).
    void FadeOutAt(CanvasItem n, double at, double dur = 0.25) => After(at, () => { if (IsInstanceValid(n)) CreateTween().TweenProperty(n, "modulate:a", 0f, dur); });

    void FadeIn(CanvasItem n, float dur = 0.2f, Vector2? slide = null)
    {
        n.Modulate = new Color(1, 1, 1, 0);
        var t = CreateTween().SetParallel();
        t.TweenProperty(n, "modulate:a", 1f, dur);
        if (slide is { } d && n is Control c) { var home = c.Position; c.Position += d; t.TweenProperty(c, "position", home, dur * 1.5f).SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out); }
    }

    /// Wrapped text shrunk until it fits its box (long card texts never spill).
    static void FitWrap(Label l, Vector2 box, int max, int min = 13)
    {
        l.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        l.ClipText = true;
        l.Size = box;   // Put grew the label to the text's one-line width
        var f = l.GetThemeFont("font");
        int size = max;
        while (size > min && f.GetMultilineStringSize(l.Text, HorizontalAlignment.Left, box.X, size).Y * 1.2f > box.Y) size--;   // Label lines sit looser than the font measures
        l.AddThemeFontSizeOverride("font_size", size);
    }

    /// The one result chip coins and dice share: a dark pill under the field's middle, sized to its text.
    void ResultChip(string text, Color c, double life = 2.2)
    {
        float w = Impact.GetStringSize(text, HorizontalAlignment.Left, -1, 28).X + 80;
        var p = Track(Box(hud, new Rect2(W / 2 - w / 2, H * 0.66f, w, 64), Pill(new Color(c.R * 0.05f, c.G * 0.05f, c.B * 0.05f, 0.92f), c, 32, 0, new Color(c, 0.4f), 18, 2)), life);
        p.MouseFilter = MouseFilterEnum.Ignore;
        Put(p, text, Impact, 28, c, new Vector2(0, 12), new Vector2(w, 40), HorizontalAlignment.Center, 5);
        FadeIn(p, 0.2f, new Vector2(0, 30));
        FadeOutAt(p, life - 0.3);
    }

    /// A caption pill across the field (reveals, the attack target hint); `life` 0 = the caller frees it.
    Panel Caption(string text, Color edge, float y = 0.56f, double life = 0)
    {
        float w = Cond.GetStringSize(text, HorizontalAlignment.Left, -1, 24).X + 80;
        var cap = Track(Box(hud, new Rect2(W / 2 - w / 2, H * y, w, 50), Pill(new Color(0.03f, 0.03f, 0.05f, 0.9f), edge, 25, 0, null, 0, 2)), life);
        cap.MouseFilter = MouseFilterEnum.Ignore;
        Put(cap, text, Cond, 24, Colors.White, new Vector2(0, 10), new Vector2(w, 30), HorizontalAlignment.Center);
        FadeIn(cap, 0.2f, new Vector2(0, 20));
        if (life > 0) FadeOutAt(cap, life - 0.3);
        return cap;
    }

    /// A card lifted off the field and tilted to face the camera (reveals, activations); returns its field place.
    Transform3D Lift(View v, Vector3 by, float scale, double dur, Vector3? from = null)
    {
        var home = v.Root.Transform;
        var to = new Transform3D(new Basis(Vector3.Right, Mathf.DegToRad(31)).Scaled(Vector3.One * scale), (from ?? home.Origin) + by);
        v.Tw?.Kill();
        Lit(v, false);
        if (v.S.Up || v.Front) v.Face.AlbedoTexture = CardArt.Large(v.Card.Data);
        v.Face.Emission = Colors.Black;
        // Unlit white under the field's glow (threshold 0.9) bloomed into a thin white haze over the art; the
        // coins dim their faces the same way. Place / SetFace restores the colour.
        v.Face.AlbedoColor = new Color(0.84f, 0.84f, 0.84f);
        CreateTween().TweenProperty(v.Root, "transform", to, dur).SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
        return home;
    }

    /// Where a field card sits on screen and how wide it looks there.
    (Vector2 At, float W) OnScreen(View v)
    {
        var o = v.Root.GlobalPosition;
        float w = cam.UnprojectPosition(o + new Vector3(CW / 2, 0, 0)).DistanceTo(cam.UnprojectPosition(o - new Vector3(CW / 2, 0, 0)));
        return (cam.UnprojectPosition(o), w);
    }

    /// A 2D copy of a card (crisp: the HUD has no tonemap or glow) centred on `at`; children "ring" (a red edge,
    /// hidden) and "art". The caller frees it.
    Control FlatCard(Texture2D tex, Vector2 at, float w)
    {
        float h = w * CardH;
        var c = Track(new Control { Position = at - new Vector2(w / 2, h / 2), Size = new Vector2(w, h), PivotOffset = new Vector2(w / 2, h / 2), MouseFilter = MouseFilterEnum.Ignore }, 0);
        hud.AddChild(c);
        var ring = new Panel { Name = "ring", Position = new Vector2(-6, -6), Size = new Vector2(w + 12, h + 12), Visible = false, MouseFilter = MouseFilterEnum.Ignore };
        ring.AddThemeStyleboxOverride("panel", Pill(new Color(0, 0, 0, 0), Bad, 8, 0, new Color(Bad, 0.6f), 18, 4));
        c.AddChild(ring);
        c.AddChild(new TextureRect { Name = "art", Texture = tex, ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, Size = new Vector2(w, h), StretchMode = TextureRect.StretchModeEnum.Scale, MouseFilter = MouseFilterEnum.Ignore, TextureFilter = TextureFilterEnum.LinearWithMipmaps });
        return c;
    }

    Label3D? words;
    Tween? wordsTw;

    /// Words lying on a side's half of the field, readable from your seat; they ride in with the zone wave. Phases can
    /// change faster than the words stay (Standby straight into Main 1): the old words are swept off before the new.
    Label3D FieldWords(string text, int side, Color c, int size, float z)
    {
        bool sweep = words != null && IsInstanceValid(words) && words.Modulate.A > 0.01f;
        if (sweep)
        {
            var old = words!;
            wordsTw?.Kill();
            var ot = CreateTween().SetParallel();
            ot.TweenProperty(old, "modulate:a", 0f, 0.14);
            ot.TweenProperty(old, "outline_modulate:a", 0f, 0.14);
            ot.TweenProperty(old, "position:x", old.Position.X + 1.4f, 0.14).SetTrans(Tween.TransitionType.Expo).SetEase(Tween.EaseType.In);
        }
        var mid = (Grid3(new St(Loc.Monster, 2, 0, false, false, side, 0, 0)) + Grid3(new St(Loc.Spell, 2, 0, false, false, side, 0, 0))) / 2;
        var l = words = Track(new Label3D { Text = text, Font = Impact, FontSize = size, PixelSize = 0.009f, Modulate = new Color(c, 0), OutlineSize = 18, OutlineModulate = new Color(0, 0, 0, 0), Shaded = false, NoDepthTest = true, RenderPriority = 6, OutlineRenderPriority = 5 }, 2.2);
        world.AddChild(l);
        l.RotationDegrees = new Vector3(-90, 0, 0);
        l.Position = mid + new Vector3(-1.4f, 0.12f, z);
        var tw = wordsTw = CreateTween();
        if (sweep) tw.TweenInterval(0.1);   // the old words clear the mat first
        tw.TweenProperty(l, "modulate:a", 0.95f, 0.25);
        tw.Parallel().TweenProperty(l, "outline_modulate:a", 0.7f, 0.25);
        tw.Parallel().TweenProperty(l, "position:x", mid.X, 0.45).SetTrans(Tween.TransitionType.Expo).SetEase(Tween.EaseType.Out);
        tw.TweenInterval(0.9);
        tw.TweenProperty(l, "modulate:a", 0f, 0.3);
        tw.Parallel().TweenProperty(l, "outline_modulate:a", 0f, 0.3);
        tw.Parallel().TweenProperty(l, "position:x", mid.X + 1.4f, 0.3).SetTrans(Tween.TransitionType.Expo).SetEase(Tween.EaseType.In);
        return l;
    }

    /// Field words for a turn or a phase, smaller for long names so they stay on the mat.
    void FieldTitle(string text, int side, Color c, int max) => FieldWords(text, side, c, Math.Min(max, 1400 / Math.Max(1, text.Length)), 0);

    // ---------------------------------------------------------------- activation: the card glows in place

    /// An activation or trigger: a card you can see only glows (GoldRays, user 2026-10-29: the rising card and its
    /// plate were too much); one you can't see shows the art cut-in. Returns the wait.
    double ShowActivation(View? v, Card c)
    {
        if (v is { S.Loc: Loc.Monster or Loc.Spell or Loc.Field or Loc.Grave } && v.Root.Visible) return 1.1;   // it glows where it is (GoldRays); nothing flies out
        Portrait(c);   // a card you can't see (hand, deck): its picture at the side
        return 1.35;
    }

    // ---------------------------------------------------------------- prompts around the card

    /// A card you can click: dark cell, gold edge on hover (the card's text shows in the left panel), a label under it.
    Panel CardButton(Control parent, Card c, Vector2 at, float cw, string label, System.Action press)
    {
        var cell = new Panel { Position = at, Size = new Vector2(cw + 28, cw * CardH + 52), MouseFilter = MouseFilterEnum.Stop };
        StyleBox Look(bool hot) => Pill(new Color(0, 0, 0, 0.4f), hot ? RivalPanels.Gold : new Color(RivalPanels.Gold, 0.45f), 10, 0, hot ? new Color(RivalPanels.Gold, 0.5f) : null, hot ? 14 : 0, 2);
        cell.AddThemeStyleboxOverride("panel", Look(false));
        cell.AddChild(new TextureRect { Texture = CardArt.Large(c.Data), ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, Position = new Vector2(14, 10), Size = new Vector2(cw, cw * CardH), StretchMode = TextureRect.StretchModeEnum.Scale, MouseFilter = MouseFilterEnum.Ignore, TextureFilter = TextureFilterEnum.LinearWithMipmaps });
        Put(cell, label, Cond, 19, RivalPanels.Gold, new Vector2(0, cw * CardH + 16), new Vector2(cw + 28, 26), HorizontalAlignment.Center);
        cell.MouseEntered += () => { cell.AddThemeStyleboxOverride("panel", Look(true)); Focus(c); UiSound("cursor"); };
        cell.MouseExited += () => cell.AddThemeStyleboxOverride("panel", Look(false));
        cell.GuiInput += e => { if (e is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left }) press(); };
        parent.AddChild(cell);
        return cell;
    }

    /// The sheet the prompt pieces sit on (closed by CloseSheet like any other).
    Panel PromptSheet(float w, float h, string title)
    {
        CloseSheet();
        sheet = Box(hud, new Rect2(W / 2 - w / 2, H * 0.45f - h / 2, w, h), Glass(RivalPanels.Gold * new Color(1, 1, 1, 0.8f), 0.25f));
        sheet.MouseFilter = MouseFilterEnum.Stop;
        var tl = Put(sheet, title, Cond, 24, Colors.White, new Vector2(20, 10), new Vector2(w - 40, 44), HorizontalAlignment.Center);
        tl.VerticalAlignment = VerticalAlignment.Center;
        FitWrap(tl, new Vector2(w - 40, 44), 24, 13);
        sheetChips.Clear(); sheetCards.Clear(); sheetLabels.Clear();
        sheet.Modulate = new Color(1, 1, 1, 0);
        CreateTween().TweenProperty(sheet, "modulate:a", 1f, 0.15);
        return sheet;
    }

    void SheetChip(Control parent, string label, Vector2 at, Vector2 size, System.Action press, int font = 20)
    {
        parent.AddChild(Chips.Make(label, at, size, press, font));
        sheetChips.Add(press); sheetLabels.Add(label);
    }

    /// A Monster Zone choice (Ground Collapse; Opt.Arg = player * 8 + zone): a ring on each free zone of the field
    /// itself, click one; a hint line over the hand.
    void ZonePick(Prompt p)
    {
        CloseSheet();
        sheet = Box(hud, new Rect2(0, 0, W, H), new StyleBoxEmpty());
        sheet.MouseFilter = MouseFilterEnum.Ignore;
        sheetChips.Clear(); sheetCards.Clear(); sheetLabels.Clear();
        string text = $"{p.Text.ToUpperInvariant()}  ·  CLICK A ZONE";
        float tw = Cond.GetStringSize(text, HorizontalAlignment.Left, -1, 24).X + 80;
        var hint = Box(sheet, new Rect2(W / 2 - tw / 2, H * 0.69f, tw, 50), Pill(new Color(0.03f, 0.03f, 0.05f, 0.9f), RivalPanels.Gold, 25, 0, null, 0, 2));
        hint.MouseFilter = MouseFilterEnum.Ignore;
        Put(hint, text, Cond, 24, Colors.White, new Vector2(0, 10), new Vector2(tw, 30), HorizontalAlignment.Center);
        for (int i = 0; i < p.Options.Count; i++)
        {
            int k = i, player = p.Options[i].Arg / 8, zone = p.Options[i].Arg % 8;
            var at = Grid3(new St(Loc.Monster, zone, 0, false, false, Side(player), 0, 0));
            // The zone's outline on screen: its four corners projected.
            var pts = new[] { new Vector3(-CW / 2, 0, -CH / 2), new Vector3(CW / 2, 0, -CH / 2), new Vector3(CW / 2, 0, CH / 2), new Vector3(-CW / 2, 0, CH / 2) }.Select(o => cam.UnprojectPosition(at + o)).ToArray();
            var r = new Rect2(pts[0], Vector2.Zero); foreach (var q in pts) r = r.Expand(q);
            var cell = new Panel { Position = r.Position, Size = r.Size, MouseFilter = MouseFilterEnum.Stop };
            StyleBox Look(bool hot) => Pill(new Color(FxRed, hot ? 0.35f : 0.12f), hot ? FxRed : new Color(FxRed, 0.6f), 8, 0, hot ? new Color(FxRed, 0.5f) : null, hot ? 12 : 0, 2);
            cell.AddThemeStyleboxOverride("panel", Look(false));
            cell.MouseEntered += () => { cell.AddThemeStyleboxOverride("panel", Look(true)); UiSound("cursor"); };
            cell.MouseExited += () => cell.AddThemeStyleboxOverride("panel", Look(false));
            System.Action press = () => Answer(k);
            cell.GuiInput += e => { if (e is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left }) press(); };
            sheet.AddChild(cell);
            sheetChips.Add(press); sheetLabels.Add(p.Options[i].Text ?? "");
        }
    }

    /// Attack or Defense Position: the card twice, standing (ATTACK) and turned on its side (DEFENSE); click one.
    void PositionSheet(Prompt p)
    {
        var c = p.Source!;
        const float cw = 170, ch = cw * CardH, w = 120 + cw + ch + 40;
        float cellH = ch + 72;
        var sh = PromptSheet(w, 58 + cellH + 20, p.Text.ToUpperInvariant());
        for (int i = 0; i < p.Options.Count; i++)
        {
            int k = i;
            bool def = p.Options[i].Arg == 1;
            var size = def ? new Vector2(ch, cw) : new Vector2(cw, ch);
            var cell = new Panel { Position = new Vector2(def ? 60 + cw + 40 : 40, 58), Size = new Vector2(size.X + 28, cellH), MouseFilter = MouseFilterEnum.Stop };
            StyleBox Look(bool hot) => Pill(new Color(0, 0, 0, 0.4f), hot ? (def ? FxIce : RivalPanels.Gold) : new Color(RivalPanels.Gold, 0.45f), 10, 0, hot ? new Color(def ? FxIce : RivalPanels.Gold, 0.5f) : null, hot ? 14 : 0, 2);
            cell.AddThemeStyleboxOverride("panel", Look(false));
            // Turned 90° about its centre, centred in the cell's card area (ch tall).
            var art = new TextureRect { Texture = CardArt.Large(c.Data), ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, Size = new Vector2(cw, ch), PivotOffset = new Vector2(cw, ch) / 2, StretchMode = TextureRect.StretchModeEnum.Scale, MouseFilter = MouseFilterEnum.Ignore, TextureFilter = TextureFilterEnum.LinearWithMipmaps };
            art.Position = new Vector2(14 + size.X / 2 - cw / 2, 10 + ch / 2 - ch / 2);
            if (def) art.Rotation = -Mathf.Pi / 2;
            cell.AddChild(art);
            Put(cell, def ? "DEFENSE" : "ATTACK", Cond, 22, def ? FxIce : RivalPanels.Gold, new Vector2(0, ch + 20), new Vector2(size.X + 28, 28), HorizontalAlignment.Center);
            Put(cell, def ? $"DEF {c.Data.Def}" : $"ATK {c.Data.Atk}", Cond, 17, new Color(1, 1, 1, 0.7f), new Vector2(0, ch + 44), new Vector2(size.X + 28, 22), HorizontalAlignment.Center);
            cell.MouseEntered += () => { cell.AddThemeStyleboxOverride("panel", Look(true)); UiSound("cursor"); };
            cell.MouseExited += () => cell.AddThemeStyleboxOverride("panel", Look(false));
            System.Action press = () => Answer(k);
            cell.GuiInput += e => { if (e is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left }) press(); };
            sh.AddChild(cell);
            sheetChips.Add(press); sheetLabels.Add(def ? "DEFENSE POSITION" : "ATTACK POSITION");
        }
    }

    /// Chain? Each card you can chain is a button (one: big; several: tiles in a row), PASS under them.
    void RespondTiles(Prompt p)
    {
        var opts = Enumerable.Range(0, p.Options.Count).Where(i => p.Options[i].Act != Act.Pass && p.Options[i].Card != null).ToList();
        int pass = p.Options.FindIndex(o => o.Act == Act.Pass);
        int n = opts.Count;
        float cw = n == 1 ? 200 : Math.Min(130, (W * 0.8f - 60) / n - 44), cellW = cw + 28, cellH = cw * CardH + 52, gap = 16;
        float rowW = n * cellW + (n - 1) * gap, w = Math.Max(n == 1 ? 380 : 560, rowW + 60);
        var sh = PromptSheet(w, 52 + cellH + 16 + 46 + 18, $"CHAIN TO {What(D.Window)}?");
        for (int j = 0; j < n; j++)
        {
            int i = opts[j];
            var o = p.Options[i];
            System.Action press = () => Answer(i);
            CardButton(sh, o.Card!, new Vector2(w / 2 - rowW / 2 + j * (cellW + gap), 52), cw, ActName(o), press);
            sheetChips.Add(press); sheetLabels.Add(o.Card!.Name);
        }
        if (pass >= 0) SheetChip(sh, "PASS", new Vector2(w / 2 - 100, 52 + cellH + 16), new Vector2(200, 46), () => Answer(pass));
    }

    /// Yes / No and "choose an effect" around the card: the big card on top (its full text is in the left panel),
    /// YES / NO or one row per choice under it, or a grid beside it when there are many (DNA Surgery's 20 Types).
    bool EffectSheet(Prompt p)
    {
        if (p.Source is not { } src || p.Kind is not (Ask.YesNo or Ask.Choose)) return false;
        Focus(src);
        int n = p.Options.Count;
        string title = p.Text.ToUpperInvariant();
        if (p.Kind == Ask.Choose && n > 4)
        {
            const float w = 960, cw = 250, ch = cw * CardH;
            int rows = (n + 3) / 4;
            float gx = cw + 60, gw = (w - gx - 30 - 3 * 10) / 4, gh = Math.Min(64, (ch - (rows - 1) * 10) / rows);
            var sh = PromptSheet(w, Math.Max(ch, rows * (gh + 10)) + 90, title);
            sh.AddChild(new TextureRect { Texture = CardArt.Large(src.Data), ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, Position = new Vector2(30, 62), Size = new Vector2(cw, ch), StretchMode = TextureRect.StretchModeEnum.Scale, MouseFilter = MouseFilterEnum.Ignore, TextureFilter = TextureFilterEnum.LinearWithMipmaps });
            for (int i = 0; i < n; i++) { int k = i; SheetChip(sh, Label(p.Options[i]), new Vector2(gx + i % 4 * (gw + 10), 62 + i / 4 * (gh + 10)), new Vector2(gw, gh), () => Answer(k), 18); }
            return true;
        }
        float cwid = p.Kind == Ask.YesNo ? 240 : 200, wid = p.Kind == Ask.YesNo ? 440 : 620, y = 58 + cwid * CardH + 14;
        var s = PromptSheet(wid, y + (p.Kind == Ask.YesNo ? 48 : n * 62 - 10) + 22, title);
        s.AddChild(new TextureRect { Texture = CardArt.Large(src.Data), ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, Position = new Vector2(wid / 2 - cwid / 2, 58), Size = new Vector2(cwid, cwid * CardH), StretchMode = TextureRect.StretchModeEnum.Scale, MouseFilter = MouseFilterEnum.Ignore, TextureFilter = TextureFilterEnum.LinearWithMipmaps });
        for (int i = 0; i < n; i++)
        {
            int k = i;
            if (p.Kind == Ask.YesNo) SheetChip(s, Label(p.Options[i]), new Vector2(i == 0 ? 30 : wid - 210, y), new Vector2(180, 48), () => Answer(k), 21);
            else SheetChip(s, $"{i + 1}   {Label(p.Options[i])}", new Vector2(30, y + i * 62), new Vector2(wid - 60, 52), () => Answer(k), 19);
        }
        return true;
    }

    /// Attack target mode without a sheet: the targets glow, one hint line (DIRECT ATTACK beside it when a card lets you
    /// go past their monsters); right-click or Esc cancels.
    void AttackHint(int uid, int direct)
    {
        CloseSheet();
        string text = $"{views[uid].Card.Name.ToUpperInvariant()}  ·  CHOOSE A TARGET  ·  RIGHT-CLICK TO CANCEL";
        float tw = Cond.GetStringSize(text, HorizontalAlignment.Left, -1, 24).X + 80, cw = direct >= 0 ? 230 : 0, w = tw + cw;
        sheet = Box(hud, new Rect2(W / 2 - w / 2, H * 0.69f, w, 50), Pill(new Color(0.03f, 0.03f, 0.05f, 0.9f), RivalPanels.Gold, 25, 0, null, 0, 2));
        sheet.MouseFilter = MouseFilterEnum.Ignore;
        Put(sheet, text, Cond, 24, Colors.White, new Vector2(0, 10), new Vector2(tw, 30), HorizontalAlignment.Center);
        sheetChips.Clear(); sheetCards.Clear(); sheetLabels.Clear();
        if (direct >= 0) SheetChip(sheet, "DIRECT ATTACK", new Vector2(tw - 10, 4), new Vector2(cw, 42), () => Answer(direct), 18);
        sheetChips.Add(CancelAttack); sheetLabels.Add("CANCEL");   // no button: right-click (the selftest presses it here)
        FadeIn(sheet, 0.2f, new Vector2(0, 20));
    }

    // ---------------------------------------------------------------- revealed cards

    /// A revealed hand: flat full-colour copies of the cards in a line over the field (theirs flip face-up on the way).
    /// It stays until the next step: a pick from it (Confiscation, The Forceful Sentry) is made by clicking the line, and
    /// a card leaving the hand from it (yours too, when the rival picks) becomes the real card at its spot, so the
    /// discard burns away there; the rest go back.
    sealed class HandLine
    {
        public readonly List<View> Views = new();
        public readonly List<Control> Flats = new();
        public readonly List<(Vector2 At, float W)> From = new();
        public int Side;
        public Panel? Cap, Go;   // the caption; the confirm button under a picked card
        public float Cw;
    }
    HandLine? handLine;

    void RaiseHand(List<View> hand, int side, string title)
    {
        ReturnHand();
        var l = handLine = new HandLine { Side = side };
        int n = hand.Count;
        float cw = l.Cw = Math.Min(180, W * 0.7f / n - 36), gap = Math.Min(36, cw * 0.2f);
        float y = side == 1 ? H * 0.3f : H * 0.5f, x0 = W / 2 - (n - 1) * (cw + gap) / 2;
        for (int i = 0; i < n; i++)
        {
            var v = hand[i];
            var from = OnScreen(v);
            bool back = side == 1 && !v.Front;
            var c = FlatCard(back ? backs[Side(1)] : CardArt.Large(v.Card.Data), from.At, cw);
            c.Scale = Vector2.One * (from.W / cw);
            v.Root.Visible = false;
            var tw = CreateTween();
            tw.TweenInterval(i * 0.06);
            tw.TweenProperty(c, "position", new Vector2(x0 + i * (cw + gap), y) - c.Size / 2, 0.34).SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
            tw.Parallel().TweenProperty(c, "scale", Vector2.One, 0.34).SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
            if (back)
            {
                var art = c.GetNode<TextureRect>("art"); var card = v.Card;
                tw.TweenProperty(c, "scale:x", 0f, 0.1);
                tw.TweenCallback(Callable.From(() => art.Texture = CardArt.Large(card.Data)));
                tw.TweenProperty(c, "scale:x", 1f, 0.12);
            }
            l.Views.Add(v); l.Flats.Add(c); l.From.Add(from);
        }
        l.Cap = Caption(title, side == 1 ? Pm.Them : Pm.You, side == 1 ? 0.5f : 0.3f);
    }

    bool LinePickable(Prompt p) => handLine is { } l && p is { Kind: Ask.Pick, Min: 1, Max: 1 } && p.Options.All(o => o.Card != null && l.Views.Any(v => v.Card == o.Card));

    /// Pick from the raised hand: the cards that can be picked get a gold edge and lift under the cursor; a click
    /// picks one (it stays lifted and ringed) and the confirm button (ADD TO HAND...) under it answers.
    void LinePick(Prompt p)
    {
        var l = handLine!;
        if (l.Cap != null) l.Cap.QueueFree();
        l.Cap = Caption($"{(p.Source?.Name ?? p.Text).ToUpperInvariant()}  ·  CHOOSE 1", RivalPanels.Gold, l.Side == 1 ? 0.5f : 0.3f);
        sheetCards.Clear();
        for (int i = 0; i < l.Flats.Count; i++)
        {
            var c = l.Flats[i]; var card = l.Views[i].Card;
            int opt = p.Options.FindIndex(o => o.Card == card);
            if (opt < 0) continue;
            c.MouseFilter = MouseFilterEnum.Stop;
            float y0 = c.Position.Y;
            c.SetMeta("y0", y0);
            var edge = new Panel { Position = new Vector2(-5, -5), Size = c.Size + new Vector2(10, 10), MouseFilter = MouseFilterEnum.Ignore };
            edge.AddThemeStyleboxOverride("panel", Pill(new Color(0, 0, 0, 0), RivalPanels.Gold, 8, 0, new Color(RivalPanels.Gold, 0.5f), 14, 3));
            c.AddChild(edge); c.MoveChild(edge, 0);
            bool Picked() => c.GetNode<Control>("ring").Visible;
            c.MouseEntered += () => { if (ask != p) return; CreateTween().TweenProperty(c, "position:y", y0 - 16, 0.12); Focus(card); UiSound("cursor"); };
            c.MouseExited += () => { if (IsInstanceValid(c) && !Picked()) CreateTween().TweenProperty(c, "position:y", y0, 0.12); };
            System.Action press = () => { if (ask != p) return; LineSelect(l, c, opt, p); };
            c.GuiInput += e => { if (e is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left }) press(); };
            sheetCards.Add(press);
        }
    }

    /// At the next step: cards that left the hand become the real card at their spot (the discard burns there), the rest
    /// go back into the hand.
    async Task SettleHand(Step s)
    {
        var l = handLine!;
        handLine = null;
        var leaving = Enumerable.Range(0, l.Views.Count).Where(i => s.Cards[l.Views[i].Card.Uid].Loc != Loc.Hand).ToList();
        if (leaving.Any(i => !l.Flats[i].GetNode<Control>("ring").Visible))
        {
            foreach (var i in leaving) l.Flats[i].GetNode<Control>("ring").Visible = true;   // the rival's pick
            Cue("pick");
            await Wait(0.45);
        }
        float px = GetViewport().GetVisibleRect().Size.Y / (2 * Mathf.Tan(Mathf.DegToRad(cam.Fov) / 2));   // pixels per unit at depth 1
        foreach (var i in leaving)
        {
            var v = l.Views[i]; var c = l.Flats[i];
            v.Tw?.Kill(); Lit(v, false);
            v.Face.AlbedoTexture = CardArt.Large(v.Card.Data); v.Face.AlbedoColor = new Color(0.84f, 0.84f, 0.84f); v.Face.Emission = Colors.Black;
            v.Root.GlobalTransform = new Transform3D(cam.GlobalBasis * new Basis(Vector3.Right, Mathf.Pi / 2), cam.ProjectPosition(c.Position + c.Size / 2, CW * px / l.Cw));
            v.Root.Visible = true;
            c.QueueFree();
        }
        ReturnFlats(l, leaving);
        if (l.Cap != null && IsInstanceValid(l.Cap)) l.Cap.QueueFree();
        if (l.Go != null && IsInstanceValid(l.Go)) l.Go.QueueFree();
    }

    /// Puts a raised hand back (nothing left it).
    void ReturnHand()
    {
        if (handLine is not { } l) return;
        handLine = null;
        ReturnFlats(l, new());
        if (l.Cap != null && IsInstanceValid(l.Cap)) l.Cap.QueueFree();
        if (l.Go != null && IsInstanceValid(l.Go)) l.Go.QueueFree();
    }

    void ReturnFlats(HandLine l, List<int> skip)
    {
        for (int i = 0; i < l.Flats.Count; i++)
        {
            if (skip.Contains(i)) continue;
            var c = l.Flats[i]; var v = l.Views[i]; var home = l.From[i];
            if (!IsInstanceValid(c)) continue;
            c.MouseFilter = MouseFilterEnum.Ignore;
            c.GetNode<Control>("ring").Visible = false;
            var tw = CreateTween();
            if (l.Side == 1 && !v.Front)
            {
                var art = c.GetNode<TextureRect>("art");
                tw.TweenProperty(c, "scale:x", 0f, 0.1);
                tw.TweenCallback(Callable.From(() => art.Texture = backs[Side(1)]));
                tw.TweenProperty(c, "scale:x", 1f, 0.1);
            }
            tw.TweenProperty(c, "position", home.At - c.Size / 2, 0.3).SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.In);
            tw.Parallel().TweenProperty(c, "scale", Vector2.One * (home.W / l.Cw), 0.3);
            tw.TweenCallback(Callable.From(() => { v.Root.Visible = v.S.Loc != Loc.None; c.QueueFree(); }));
        }
    }

    /// Revealed cards on the field (and anything that isn't one whole hand): they flip up and lift where they are,
    /// then go back.
    double LiftReveal(List<View> vs, string title, Color edge)
    {
        var homes = vs.Select(v => (v, v.S)).ToList();
        for (int i = 0; i < vs.Count; i++) Move(vs[i], vs[i].S with { Up = true }, i * 0.08f, 0.3f, 0.3f);
        After(0.65, () =>
        {
            foreach (var (v, s) in homes)
            {
                v.Front = true;
                Lift(v, new Vector3(0, 2.2f, s.Side == 0 ? -0.8f : 0.6f), 1.35f, 0.3);
            }
        });
        After(0.7, () => Caption(title, edge, 0.56f, 2.6));
        After(3.1, () => { foreach (var (v, s) in homes) CreateTween().TweenProperty(v.Root, "transform", Target(v, s), 0.35).SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.InOut); });
        After(3.45, () => { foreach (var (v, s) in homes) Place(v, s); });
        return 3.5;
    }

    // ---------------------------------------------------------------- duel end

    TextureRect HudGlow(string tex, Vector2 at, float size, Color c, double life)
    {
        var r = Track(new TextureRect { Texture = VfxTex(tex), ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.Scale, Size = new Vector2(size, size), Position = at - new Vector2(size / 2, size / 2), PivotOffset = new Vector2(size / 2, size / 2), Material = AddMat, Modulate = c, MouseFilter = MouseFilterEnum.Ignore }, life);
        hud.AddChild(r);
        return r;
    }

    /// The end of the duel: a win flashes, light rays spin and a shockwave rings out as the title slams in letter by
    /// letter; a loss goes dark red and the letters crash down and crack. A line and the subtitle follow.
    async Task EndTitle(string word, Color col, bool win, string sub)
    {
        const double life = 3.9;
        var c = new Vector2(W / 2, H * 0.42f);
        var dim = Track(new ColorRect { Size = new Vector2(W, H), Color = new Color(0, 0, 0, 0), MouseFilter = MouseFilterEnum.Ignore }, life);
        hud.AddChild(dim);
        CreateTween().TweenProperty(dim, "color:a", win ? 0.35f : 0.62f, 0.3);
        var pieces = new List<CanvasItem> { dim };
        if (win)
        {
            var flash = Track(new ColorRect { Size = new Vector2(W, H), Color = new Color(1, 0.95f, 0.8f, 0.85f), MouseFilter = MouseFilterEnum.Ignore }, 1);
            hud.AddChild(flash);
            CreateTween().TweenProperty(flash, "color:a", 0f, 0.45);
            var rays = HudGlow("md/rays", c, 1600, new Color(col, 0), life);
            var rt = CreateTween().SetParallel();
            rt.TweenProperty(rays, "modulate:a", 0.6f, 0.4);
            rt.TweenProperty(rays, "rotation", 0.7f, 4.0);
            pieces.Add(rays);
            foreach (var (tex, s0, s1, d) in new[] { ("md/thinring", 0.2f, 4.5f, 0.8), ("md/spikering", 0.3f, 2.2f, 0.6), ("md/burst", 0.3f, 1.4f, 0.5) })
            {
                var g = HudGlow(tex, c, 700, col, 1);
                g.Scale = Vector2.One * s0;
                var t = CreateTween().SetParallel();
                t.TweenProperty(g, "scale", Vector2.One * s1, d).SetTrans(Tween.TransitionType.Expo).SetEase(Tween.EaseType.Out);
                t.TweenProperty(g, "modulate:a", 0f, d);
            }
            Vignette(FxGold, 2.5);
            Shake(0.06f);
        }
        else Vignette(new Color(0.55f, 0.04f, 0.04f), 3);
        await Wait(win ? 0.1 : 0.2);
        pieces.AddRange(SlamLetters(word, c, 190, col, win, life));
        await Wait(0.75);
        if (!win)
        {
            var crack = HudGlow("md/crack", c, 900, new Color(1f, 0.35f, 0.3f, 0.9f), life);
            crack.Scale = Vector2.One * 0.6f;
            CreateTween().TweenProperty(crack, "scale", Vector2.One, 0.15).SetTrans(Tween.TransitionType.Expo).SetEase(Tween.EaseType.Out);
            Shake(0.08f); Cue("crack");
            pieces.Add(crack);
        }
        var bar = Track(new ColorRect { Position = new Vector2(W / 2, c.Y + 120), Size = new Vector2(0, 4), Color = col, MouseFilter = MouseFilterEnum.Ignore }, life);
        hud.AddChild(bar);
        CreateTween().TweenMethod(Callable.From<float>(k => { bar.Size = new Vector2(820 * k, 4); bar.Position = new Vector2(W / 2 - 410 * k, bar.Position.Y); }), 0f, 1f, 0.35).SetTrans(Tween.TransitionType.Expo).SetEase(Tween.EaseType.Out);
        pieces.Add(bar);
        if (sub.Length > 0)
        {
            var sl = Track(Put(hud, sub, Cond, 36, Colors.White, new Vector2(0, c.Y + 136), new Vector2(W, 46), HorizontalAlignment.Center, 6), life);
            FadeIn(sl, 0.3f, new Vector2(0, 20));
            pieces.Add(sl);
        }
        await Wait(2.6);
        foreach (var n in pieces) if (IsInstanceValid(n)) CreateTween().TweenProperty(n, "modulate:a", 0f, 0.35);
        await Wait(0.35);
    }

    /// One word, letter by letter: win = each letter slams down from big, lose = each letter drops from above.
    List<Label> SlamLetters(string word, Vector2 at, int size, Color c, bool win, double life)
    {
        var ls = new List<Label>();
        float sp = size * 0.06f, total = word.Sum(ch => Impact.GetStringSize(ch.ToString(), HorizontalAlignment.Left, -1, size).X + sp) - sp, x = at.X - total / 2;
        for (int i = 0; i < word.Length; i++)
        {
            float w = Impact.GetStringSize(word[i].ToString(), HorizontalAlignment.Left, -1, size).X;
            var l = Track(Put(hud, word[i].ToString(), Impact, size, c, new Vector2(x - 20, at.Y - size * 0.6f), new Vector2(w + 40, size * 1.2f), HorizontalAlignment.Center, 16), life);
            ls.Add(l);
            l.PivotOffset = l.Size / 2;
            var home = l.Position;
            l.Modulate = new Color(1, 1, 1, 0);
            var tw = CreateTween();
            tw.TweenInterval(i * (win ? 0.07 : 0.09));
            if (win)
            {
                l.Scale = Vector2.One * 2.8f;
                tw.TweenProperty(l, "modulate:a", 1f, 0.08);
                tw.Parallel().TweenProperty(l, "scale", Vector2.One, 0.22).SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
            }
            else
            {
                l.Position = home - new Vector2(0, 260); l.RotationDegrees = (i % 2 == 0 ? -1 : 1) * 8;
                tw.TweenProperty(l, "modulate:a", 1f, 0.1);
                tw.Parallel().TweenProperty(l, "position", home, 0.42).SetTrans(Tween.TransitionType.Bounce).SetEase(Tween.EaseType.Out);
                tw.Parallel().TweenProperty(l, "rotation_degrees", (i % 2 == 0 ? 1 : -1) * 3f, 0.42);
            }
            tw.TweenCallback(Callable.From(() => Shake(win ? 0.015f : 0.03f)));
            x += w + sp;
        }
        return ls;
    }
}
