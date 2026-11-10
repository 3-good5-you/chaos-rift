using Godot;
using PoC.Engine;

namespace PoC.Game.UI;

/// Your input (Master Duel / PoC style). Every engine prompt for you becomes field interaction:
/// - Main/Battle: cards with legal actions glow; hover shows the action, right click cycles it, left click
///   confirms. Attack enters target mode (targets glow red, Direct Attack chip). Cards in the deck,
///   graveyard or fusion deck with actions open from their pile. Phase changes go through the phase circle.
/// - Respond: the cards you can chain glow and a dialog shows them as card buttons with Pass under them.
/// - Pick: candidates on the field glow and toggle; hidden ones (deck, graveyard...) appear in a card sheet; a
///   revealed hand is picked from its raised line (BattleUi).
/// - Yes/No and options of a card: around the big card (BattleUi); other options, position: dialog chips.
/// `--autoplay` lets the demo bot play your side; `--selftest` clicks through these same handlers at random.
public partial class DuelField
{
    enum Hint { None, Action, Target, Picked, Linked }

    sealed record CardAct(string Name, int Index); // Index -1 = attack (enter target mode)

    bool autoplay, selftest;
    Prompt? ask;
    TaskCompletionSource<Command>? answer;
    readonly Dictionary<int, List<CardAct>> acts = new();
    readonly Dictionary<int, int> actSel = new();
    readonly Dictionary<(int side, Loc loc), List<int>> pileActs = new();
    readonly Dictionary<int, Hint> hints = new();
    readonly List<int> picked = new();
    int attacker = -1;
    Panel? sheet;
    readonly List<System.Action> sheetChips = new();   // what each sheet chip does (selftest presses them)
    readonly List<System.Action> sheetCards = new();   // what each sheet card does
    readonly List<string> sheetLabels = new();
    Panel actionChip = null!;
    Label actionText = null!, actionSub = null!;
    Rng drive = new(99);

    static string ActName(Opt o) => o.Act switch
    {
        Act.Summon => "NORMAL SUMMON",
        Act.Set => "SET",
        Act.Special => "SPECIAL SUMMON",
        Act.Activate => o.Arg >= 10 ? (o.Text ?? "USE EFFECT").ToUpperInvariant() : o.Arg == 1 ? "USE EFFECT" : "ACTIVATE",
        Act.SetSpell => "SET",
        Act.FlipSummon => "FLIP SUMMON",
        Act.ChangePos => "CHANGE POSITION",
        Act.Attack => "ATTACK",
        _ => o.Act.ToString().ToUpperInvariant(),
    };

    void BuildInput()
    {
        actionChip = Box(hud, new Rect2(0, 0, 240, 62), Glass(RivalPanels.Gold * new Color(1, 1, 1, 0.9f), 0.3f));
        actionChip.Visible = false;
        actionText = Put(actionChip, "", Impact, 26, RivalPanels.Gold, new Vector2(0, 6), new Vector2(240, 32), HorizontalAlignment.Center, 4);
        actionSub = Put(actionChip, "", Cond, 15, new Color(1, 1, 1, 0.6f), new Vector2(0, 36), new Vector2(240, 20), HorizontalAlignment.Center);
    }

    // ---------------------------------------------------------------- one prompt

    async Task<Command> Human(Prompt p)
    {
        ask = p;
        answer = new TaskCompletionSource<Command>();
        var done = answer;
        acts.Clear(); actSel.Clear(); pileActs.Clear(); picked.Clear(); attacker = -1;
        if (handLine != null && !LinePickable(p)) ReturnHand();   // a revealed hand goes back unless this picks from it
        switch (p.Kind)
        {
            case Ask.Main or Ask.Battle:
                CollectActions(p);
                phasePrompt = p;
                pulse = CreateTween().SetLoops();
                pulse.TweenProperty(phaseCircle, "scale", new Vector2(1.06f, 1.06f), 0.5).SetTrans(Tween.TransitionType.Sine);
                pulse.TweenProperty(phaseCircle, "scale", Vector2.One, 0.5).SetTrans(Tween.TransitionType.Sine);
                break;
            case Ask.Respond:
                CollectActions(p);
                RespondTiles(p);
                break;
            case Ask.Pick:
                if (LinePickable(p)) LinePick(p); else PickSheet(p);
                break;
            case Ask.Choose when p.Options.Count == 2 && p.Options[0].Text == "Heads" && p.Options[1].Text == "Tails":
                if (p.Source != null) Focus(p.Source);
                CoinCallSheet(p);
                break;
            case Ask.Choose when DeclareSheet(p):   // "Declare ..." a card name / Attribute / Type (DuelField.Marks.cs)
                break;
            case Ask.Choose when p.Source?.Name == "Ground Collapse":
                ZonePick(p);
                break;
            case Ask.Position when p.Source != null:
                Focus(p.Source);
                PositionSheet(p);
                break;
            default: // YesNo, Choose, Position
                if (p.Source != null) Focus(p.Source);
                if (!EffectSheet(p)) ShowSheet(p.Text.ToUpperInvariant(), null, p.Options.Select((o, i) => (Label(o), (System.Action)(() => Answer(i)), true)).ToList());
                break;
        }
        RefreshHints();
        TestHooks();
        if (selftest) SelfDrive(p, done);
        var cmd = await done.Task;
        ask = null; answer = null; phasePrompt = null;
        pulse?.Kill(); phaseCircle.Scale = Vector2.One;
        acts.Clear(); pileActs.Clear(); picked.Clear(); attacker = -1;
        CloseSheet(); CloseMenu();
        RefreshHints();
        return cmd;
    }

    static string Label(Opt o) => o.Act == Act.Pass ? "PASS" : o.Act == Act.Option ? (o.Text ?? "OK").ToUpperInvariant() : o.Card != null ? o.Card.Name : o.ToString();

    void Answer(params int[] picks)
    {
        if (ask == null || answer == null) return;
        UiSound("decide");
        answer.TrySetResult(new Command(ask.Player, picks));
    }

    /// Group card options per card (Attack options collapse into one "ATTACK" action per attacker).
    void CollectActions(Prompt p)
    {
        for (int i = 0; i < p.Options.Count; i++)
        {
            var o = p.Options[i];
            if (o.Card == null) continue;
            if (!acts.TryGetValue(o.Card.Uid, out var l)) acts[o.Card.Uid] = l = new();
            if (o.Act == Act.Attack) { if (!l.Any(a => a.Index == -1)) l.Add(new CardAct("ATTACK", -1)); }
            else l.Add(new CardAct(ActName(o), i));
        }
        // Main action first (left click), alternatives on right click: attack/summon/activate, then set, then position.
        int Rank(CardAct a) => a.Index < 0 ? 0 : p.Options[a.Index].Act switch
        {
            Act.Summon or Act.Special or Act.Activate or Act.FlipSummon => 1,
            Act.Set or Act.SetSpell => 2,
            _ => 3,
        };
        foreach (var l in acts.Values) l.Sort((x, y) => Rank(x).CompareTo(Rank(y)));
        // Cards you can't click directly (deck, graveyard, fusion deck) are reached through their pile.
        foreach (var uid in acts.Keys)
        {
            var s = views[uid].S;
            if (s.Loc is Loc.Deck or Loc.Grave or Loc.Extra or Loc.Banished)
            {
                if (!pileActs.TryGetValue((s.Side, s.Loc), out var l)) pileActs[(s.Side, s.Loc)] = l = new();
                l.Add(uid);
            }
        }
    }

    View? PileTop(int side, Loc loc) => views.Values.Where(v => v.S.Side == side && v.S.Loc == loc)
        .OrderByDescending(v => loc is Loc.Deck or Loc.Extra ? -v.S.Index : v.S.Index).FirstOrDefault();

    // ---------------------------------------------------------------- highlights

    void RefreshHints()
    {
        hints.Clear();
        if (ask != null)
        {
            if (attacker >= 0)
            {
                foreach (var o in ask.Options) if (o.Act == Act.Attack && o.Card?.Uid == attacker && o.Target != null) hints[o.Target.Uid] = Hint.Target;
                hints[attacker] = Hint.Picked;
            }
            else if (ask.Kind == Ask.Pick)
            {
                foreach (var o in ask.Options) if (o.Card != null) hints[o.Card.Uid] = Hint.Target;
                foreach (var i in picked) hints[ask.Options[i].Card!.Uid] = Hint.Picked;
            }
            else
            {
                foreach (var uid in acts.Keys) if (views[uid].S.Loc is Loc.Hand or Loc.Monster or Loc.Spell or Loc.Field) hints[uid] = Hint.Action;
                foreach (var (k, _) in pileActs) { var top = PileTop(k.side, k.loc); if (top != null) hints[top.Card.Uid] = Hint.Action; }
            }
        }
        foreach (var uid in linked) hints[uid] = Hint.Linked;
        foreach (var v in views.Values) Aura(v);
    }

    void Aura(View v)
    {
        var h = hints.GetValueOrDefault(v.Card.Uid);
        bool ready = v.S.Ready == 1 && v.S.Loc == Loc.Monster;
        v.Aura.Visible = h != Hint.None || ready;
        if (!v.Aura.Visible) return;
        var c = h switch
        {
            Hint.Target => new Color(1f, 0.35f, 0.25f),
            Hint.Picked => new Color(1f, 1f, 1f),
            Hint.Linked => new Color(0.4f, 0.9f, 1f),
            _ => v.S.Side == 0 ? Pm.You : Pm.Them,
        };
        ((ShaderMaterial)v.Aura.MaterialOverride).SetShaderParameter("col", c);
    }

    void UpdateActionChip()
    {
        bool show = ask != null && attacker < 0 && sheet == null && hovered >= 0 && ask.Kind is Ask.Main or Ask.Battle;
        string text = "", sub = "";
        if (show)
        {
            var v = views[hovered];
            if (acts.TryGetValue(hovered, out var l) && v.S.Loc is Loc.Hand or Loc.Monster or Loc.Spell or Loc.Field)
            {
                int k = actSel.GetValueOrDefault(hovered) % l.Count;
                text = l[k].Name;
                sub = l.Count > 1 ? $"RIGHT-CLICK: NEXT  ({k + 1}/{l.Count})" : "LEFT-CLICK";
                // Main Phase 1 with a Battle Phase ahead: say so, it's easy to think you're already in battle.
                if (l[k].Index >= 0 && ask.Options[l[k].Index].Act == Act.ChangePos && D.Phase == Phase.Main1 && ask.Options.Any(o => o.Act == Act.ToBattle))
                    sub = "MAIN PHASE 1  ·  NOT IN BATTLE YET";
            }
            else if (pileActs.TryGetValue((v.S.Side, v.S.Loc), out var pl) && PileTop(v.S.Side, v.S.Loc) == v)
            {
                text = ZoneLabel(v.S.Loc) == "" ? "CARDS" : ZoneLabel(v.S.Loc);
                sub = $"{pl.Count} CARD{(pl.Count > 1 ? "S" : "")} WITH ACTIONS";
            }
            else show = false;
        }
        actionChip.Visible = show;
        if (!show) return;
        actionText.Text = text;
        actionSub.Text = sub;
        var at = cam.UnprojectPosition(views[hovered].Root.GlobalPosition);
        actionChip.Position = new Vector2(Mathf.Clamp(at.X - 120, 10, W - 250), Mathf.Clamp(at.Y - (views[hovered].S.Loc == Loc.Hand ? 190 : 130), 10, H - 80));
    }

    // ---------------------------------------------------------------- clicks

    /// Right click = back: cancel the attack, PASS a chain prompt, NO to "activate?", close a list. Anything else
    /// (a card's next action, the phase menu) is handled where it always was.
    public override void _Input(InputEvent e)
    {
        if (lab || duelMenu != null || e is not InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Right }) return;
        if (attacker >= 0) CancelAttack();
        else if (ask is { Kind: Ask.Respond or Ask.YesNo } p && !(hovered >= 0 && acts.TryGetValue(hovered, out var l) && l.Count > 1)
            && p.Options.FindIndex(o => o.Act == Act.Pass || p.Kind == Ask.YesNo && o.Arg == 0) is >= 0 and var no) Answer(no);
        else if (sheet != null && sheetClosable) CloseSheet();
        else return;
        GetViewport().SetInputAsHandled();
    }

    public override void _UnhandledInput(InputEvent e)
    {
        if (lab) { LabKey(e); return; }
        if (e is InputEventMouseButton { Pressed: true } mb)
        {
            // Double-click your own Deck: ask to surrender (QOL, 2026-11-09).
            if (mb.ButtonIndex == MouseButton.Left && mb.DoubleClick && Watch == null && hovered >= 0 && views[hovered].S is { Side: 0, Loc: Loc.Deck } && sheet == null && !surrendered)
            {
                ShowSheet(Lan.On ? "LEAVE THIS DUEL?" : "SURRENDER THIS DUEL?", null, new() { (Lan.On ? "LEAVE" : "SURRENDER", Surrender, true), ("KEEP PLAYING", CloseSheet, true) });
                sheetClosable = true;
                return;
            }
            if (mb.ButtonIndex == MouseButton.Left) CardLeft(hovered);
            else if (mb.ButtonIndex == MouseButton.Right) CardRight(hovered);
            return;
        }
        if (e.IsActionPressed("ui_cancel")) { if (attacker >= 0) CancelAttack(); else if (sheet != null && sheetClosable) CloseSheet(); else ToggleDuelMenu(); return; }
        if (Watch != null && e is InputEventKey { Pressed: true, Echo: false, Keycode: Key.Space }) { TogglePause(); return; }
        // Space: on to the next phase (Battle, Main 2, end turn), when nothing else is waiting on you.
        if (e is InputEventKey { Pressed: true, Echo: false, Keycode: Key.Space } && ask is { Kind: Ask.Main or Ask.Battle } && sheet == null && attacker < 0 && duelMenu == null)
        {
            int i = PhaseActs.Select(a => ask.Options.FindIndex(o => o.Act == a)).FirstOrDefault(k => k >= 0, -1);
            if (i >= 0) { Answer(i); return; }
        }
        base._UnhandledInput(e);
    }

    void CardLeft(int uid)
    {
        if (uid < 0) return;
        var v = views[uid];
        // Graveyards, banished piles and your Fusion Deck can be looked through whenever no dialog is up.
        bool look = v.S.Loc is Loc.Grave or Loc.Banished || (v.S.Loc == Loc.Extra && v.S.Side == 0);
        if (ask == null) { if (look && sheet == null) PileView(v.S); return; }
        if (ask.Kind == Ask.Pick) { TogglePick(ask.Options.FindIndex(o => o.Card?.Uid == uid)); return; }
        if (attacker >= 0)
        {
            int i = ask.Options.FindIndex(o => o.Act == Act.Attack && o.Card?.Uid == attacker && o.Target?.Uid == uid);
            if (i >= 0) Answer(i);
            else if (uid == attacker) CancelAttack();
            return;
        }
        if (ask.Kind is not (Ask.Main or Ask.Battle or Ask.Respond)) return;
        if (acts.TryGetValue(uid, out var l) && v.S.Loc is Loc.Hand or Loc.Monster or Loc.Spell or Loc.Field)
        {
            var a = l[actSel.GetValueOrDefault(uid) % l.Count];
            if (a.Index >= 0) Answer(a.Index);
            else BeginAttack(uid);
            return;
        }
        if (pileActs.TryGetValue((v.S.Side, v.S.Loc), out var pl)) { Sfx("SE_DECK_WINDOW_OPEN"); PileSheet(pl); }
        else if (look && ask.Kind is Ask.Main or Ask.Battle) PileView(v.S);
        else if (ask.Kind is Ask.Main or Ask.Battle) Sfx("SE_DUEL_CANCEL");
    }

    void CardRight(int uid)
    {
        if (ask == null) return;
        if (attacker >= 0) { CancelAttack(); return; }
        if (uid >= 0 && acts.TryGetValue(uid, out var l) && l.Count > 1) { actSel[uid] = actSel.GetValueOrDefault(uid) + 1; UiSound("cursor"); }
    }

    void BeginAttack(int uid)
    {
        var opts = Enumerable.Range(0, ask!.Options.Count).Where(i => ask.Options[i].Act == Act.Attack && ask.Options[i].Card?.Uid == uid).ToList();
        // Confirm the target (even a single monster), so an attack on a monster is never one stray click.
        int direct = opts.FirstOrDefault(i => ask.Options[i].Target == null, -1);
        if (opts.Count == 1 && direct >= 0) { Answer(direct); return; }   // nothing to hit but the player: attack directly
        attacker = uid;
        Sfx("SE_DUEL_SELECT");
        AttackHint(uid, direct);
        RefreshHints();
    }

    void CancelAttack()
    {
        attacker = -1;
        CloseSheet();
        RefreshHints();
    }

    // ---------------------------------------------------------------- sheets (dialogs, card picks)

    void PickSheet(Prompt p)
    {
        if (p.Source != null) Focus(p.Source);
        // Candidates you can see on the field are clicked there; anything else is listed in the sheet.
        bool hidden = p.Options.Any(o => o.Card != null && views[o.Card.Uid].S.Loc is not (Loc.Monster or Loc.Spell or Loc.Field) && !(views[o.Card.Uid].S.Loc == Loc.Hand && views[o.Card.Uid].S.Side == 0));
        var cards = hidden ? p.Options.Select(o => o.Card!).ToList() : null;
        string count = p.Min == p.Max ? $"CHOOSE {p.Max}" : $"CHOOSE {p.Min}–{p.Max}";
        bool ok = picked.Count >= p.Min && picked.Count <= p.Max;
        var chips = new List<(string, System.Action, bool)>();
        if (p.Max > 1 || p.Min == 0) chips.Add((picked.Count == 0 && p.Min == 0 ? "SKIP" : "CONFIRM", () => { if (picked.Count >= p.Min && picked.Count <= p.Max) Answer(picked.ToArray()); }, ok));
        if (p.CancelOk) chips.Add(("CANCEL", () => Answer(), true));
        ShowSheet($"{p.Text.ToUpperInvariant()}  ·  {count}", cards, chips, onCard: i => TogglePick(i), top: !hidden);
    }

    void TogglePick(int i)
    {
        if (ask is not { Kind: Ask.Pick } p || i < 0) return;
        if (picked.Contains(i)) picked.Remove(i);
        else
        {
            if (p.Max == 1 && p.Min == 1) { Answer(i); return; }
            if (picked.Count >= p.Max) return;
            picked.Add(i);
        }
        RefreshHints();
        PickSheet(p);
    }

    void PileSheet(List<int> uids)
    {
        var cards = uids.Select(u => views[u].Card).ToList();
        ShowSheet("CHOOSE A CARD", cards, new() { ("CLOSE", CloseSheet, true) }, onCard: k =>
        {
            var l = acts[cards[k].Uid];
            if (l.Count == 1) { Answer(l[0].Index); return; }
            ShowSheet(cards[k].Name.ToUpperInvariant(), null, l.Select(a => (a.Name, (System.Action)(() => Answer(a.Index)), true)).Append(("BACK", (System.Action)(() => PileSheet(uids)), true)).ToList());
        });
    }

    void PileView(St s)
    {
        var cards = views.Values.Where(v => v.S.Side == s.Side && v.S.Loc == s.Loc).OrderByDescending(v => v.S.Index).Select(v => v.Card).ToList();
        if (cards.Count == 0) return;
        Sfx("SE_DECK_WINDOW_OPEN");
        string whose = s.Side == 0 ? "YOUR" : $"{ThemName.ToUpperInvariant()}'S";
        string what = s.Loc switch { Loc.Grave => "GRAVEYARD", Loc.Banished => "BANISHED CARDS", _ => "FUSION DECK" };
        ShowSheet($"{whose} {what}  ·  {cards.Count}", cards, new() { ("CLOSE", CloseSheet, true) });
        sheetClosable = true;
    }

    /// A modal panel: title, optional card grid, a row (or rows) of chips. `top` docks it near the top so
    /// the field stays clickable underneath.
    bool sheetClosable;   // a look-only sheet (pile view, surrender ask): Esc closes it

    void ShowSheet(string title, List<Card>? cards, List<(string label, System.Action act, bool enabled)> chips, System.Action<int>? onCard = null, bool top = false)
    {
        CloseSheet();
        sheetClosable = false;
        const float cw = 100, chh = 146, gap = 12;
        int cols = cards == null ? 0 : Math.Min(8, cards.Count);
        int rows = cards == null ? 0 : (cards.Count + 7) / 8;
        int chipCols = Math.Min(3, Math.Max(1, chips.Count));
        int chipRows = (chips.Count + chipCols - 1) / chipCols;
        // Wide enough for the title (effect prompts can be long: "UFO Turtle: Special Summon a FIRE monster..."), within reason.
        float titleW = Cond.GetStringSize(title, HorizontalAlignment.Left, -1, 26).X + 48;
        float w = Math.Min(W * 0.85f, Math.Max(Math.Max(640, titleW), cols * (cw + gap) + 48));
        float gridH = Math.Min(rows, 2.6f) * (chh + gap);
        float h = 66 + (cards != null ? gridH + 10 : 0) + chipRows * 56 + 14;
        var at = top ? new Vector2(W / 2 - w / 2, 24) : new Vector2(W / 2 - w / 2, H * 0.46f - h / 2);
        sheet = Box(hud, new Rect2(at, new Vector2(w, h)), Glass(RivalPanels.Gold * new Color(1, 1, 1, 0.8f), 0.25f));
        sheet.MouseFilter = MouseFilterEnum.Stop;
        var tl = Put(sheet, title, Cond, 26, Colors.White, new Vector2(20, 10), new Vector2(w - 40, 46), HorizontalAlignment.Center);
        tl.VerticalAlignment = VerticalAlignment.Center;
        FitWrap(tl, new Vector2(w - 40, 46), 26, 13);
        sheetChips.Clear(); sheetCards.Clear(); sheetLabels.Clear();
        float y = 62;
        if (cards != null)
        {
            var scroll = new ScrollContainer { Position = new Vector2(24, y), Size = new Vector2(w - 48, gridH), HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
            var grid = new GridContainer { Columns = 8 };
            grid.AddThemeConstantOverride("h_separation", (int)gap);
            grid.AddThemeConstantOverride("v_separation", (int)gap);
            scroll.AddChild(grid);
            sheet.AddChild(scroll);
            for (int i = 0; i < cards.Count; i++)
            {
                int k = i;
                var c = cards[i];
                var v = views[c.Uid];
                bool known = v.Front || v.S.Side == 0;
                int opt = ask?.Options.FindIndex(o => o.Card == c) ?? -1;
                bool sel = ask?.Kind == Ask.Pick && picked.Contains(opt);
                var cell = new Panel { CustomMinimumSize = new Vector2(cw, chh), MouseFilter = MouseFilterEnum.Stop };
                cell.AddThemeStyleboxOverride("panel", Pill(new Color(0, 0, 0, 0.4f), sel ? Colors.White : new Color(1, 1, 1, 0.15f), 6, 0, sel ? new Color(1, 1, 1, 0.6f) : null, sel ? 12 : 0, sel ? 3 : 1));
                cell.AddChild(new TextureRect { Texture = known ? CardArt.Small(c.Data) : backs[Side(c.Owner)], ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, Size = new Vector2(cw - 8, chh - 8), Position = new Vector2(4, 4), StretchMode = TextureRect.StretchModeEnum.Scale, MouseFilter = MouseFilterEnum.Ignore });
                if (sel && ask!.Max > 1)   // the pick order (Big Eye: 1 = the top card)
                {
                    var n = Box(cell, new Rect2(cw - 40, 6, 32, 32), Pill(RivalPanels.Gold, Colors.White, 16, 0, null, 0, 2));
                    n.MouseFilter = MouseFilterEnum.Ignore;
                    Put(n, (picked.IndexOf(opt) + 1).ToString(), Impact, 22, Colors.Black, new Vector2(0, 2), new Vector2(32, 28), HorizontalAlignment.Center);
                }
                System.Action click = () => onCard?.Invoke(ask?.Kind == Ask.Pick ? opt : k);
                cell.GuiInput += e => { if (e is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left }) click(); };
                cell.MouseEntered += () => { if (known) Focus(c); };
                grid.AddChild(cell);
                sheetCards.Add(click);
            }
            y += gridH + 10;
        }
        float cwid = (w - 48 - (chipCols - 1) * 12) / chipCols;
        for (int i = 0; i < chips.Count; i++)
        {
            var (label, act, enabled) = chips[i];
            System.Action press = () => { if (enabled) act(); };
            var chip = Chips.Make(label, new Vector2(24 + i % chipCols * (cwid + 12), y + i / chipCols * 56), new Vector2(cwid, 44), press, 19);
            if (!enabled) chip.Modulate = new Color(1, 1, 1, 0.35f);
            sheet.AddChild(chip);
            sheetChips.Add(press);
            sheetLabels.Add(label);
        }
        sheet.Modulate = new Color(1, 1, 1, 0);
        CreateTween().TweenProperty(sheet, "modulate:a", 1f, 0.15);
    }

    /// "Call it": the two coin faces float over the dimmed field (no panel), click the one you call (Millennium
    /// Puzzle = heads, Eye = tails).
    void CoinCallSheet(Prompt p)
    {
        CloseSheet();
        const float w = 620, h = 330, face = 190;
        sheet = Box(hud, new Rect2(W / 2 - w / 2, H * 0.44f - h / 2, w, h), new StyleBoxEmpty());
        sheet.MouseFilter = MouseFilterEnum.Stop;
        sheet.AddChild(new ColorRect { Position = -sheet.Position, Size = new Vector2(W, H), Color = new Color(0, 0, 0, 0.45f), MouseFilter = MouseFilterEnum.Ignore });
        Put(sheet, $"{(p.Source?.Name ?? "COIN TOSS").ToUpperInvariant()}: CALL IT", Cond, 26, Colors.White, new Vector2(20, 14), new Vector2(w - 40, 34), HorizontalAlignment.Center);
        sheetChips.Clear(); sheetCards.Clear(); sheetLabels.Clear();
        string[] img = { "puzzle", "eye" }, label = { "HEADS", "TAILS" };
        for (int i = 0; i < 2; i++)
        {
            int k = i;
            var cell = new Panel { Position = new Vector2(w / 2 - 20 - face + i * (face + 40), 62), Size = new Vector2(face, face), PivotOffset = new Vector2(face / 2, face / 2), MouseFilter = MouseFilterEnum.Stop };
            cell.AddThemeStyleboxOverride("panel", Pill(new Color(0.06f, 0.05f, 0.03f, 0.9f), RivalPanels.Gold * new Color(1, 1, 1, 0.5f), (int)(face / 2), 0, null, 0, 2));
            cell.AddChild(new TextureRect { Texture = Tex($"res://assets/coin/{img[i]}.png"), ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, Size = new Vector2(face - 40, face - 40), Position = new Vector2(20, 20), StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered, MouseFilter = MouseFilterEnum.Ignore });
            Put(sheet, label[i], Impact, 28, RivalPanels.Gold, cell.Position + new Vector2(0, face + 10), new Vector2(face, 36), HorizontalAlignment.Center, 6);
            System.Action press = () => Answer(k);
            cell.MouseEntered += () =>
            {
                cell.AddThemeStyleboxOverride("panel", Pill(new Color(0.12f, 0.09f, 0.03f, 0.95f), RivalPanels.Gold, (int)(face / 2), 0, new Color(1f, 0.75f, 0.3f, 0.6f), 24, 3));
                CreateTween().TweenProperty(cell, "scale", new Vector2(1.06f, 1.06f), 0.12);
                UiSound("cursor");
            };
            cell.MouseExited += () =>
            {
                cell.AddThemeStyleboxOverride("panel", Pill(new Color(0.06f, 0.05f, 0.03f, 0.9f), RivalPanels.Gold * new Color(1, 1, 1, 0.5f), (int)(face / 2), 0, null, 0, 2));
                CreateTween().TweenProperty(cell, "scale", Vector2.One, 0.12);
            };
            cell.GuiInput += e => { if (e is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left }) press(); };
            sheet.AddChild(cell);
            sheetChips.Add(press);
            sheetLabels.Add(label[i]);
        }
        sheet.Modulate = new Color(1, 1, 1, 0);
        CreateTween().TweenProperty(sheet, "modulate:a", 1f, 0.15);
    }

    void CloseSheet()
    {
        sheet?.QueueFree();
        sheet = null;
        sheetChips.Clear(); sheetCards.Clear();
    }

    // ---------------------------------------------------------------- self test

    /// Plays your side through the click handlers above (not by submitting), so every input path is exercised.
    async void SelfDrive(Prompt p, TaskCompletionSource<Command> done)
    {
        for (int tries = 0; tries < 40 && !done.Task.IsCompleted; tries++)
        {
            await Wait(0.25);
            if (done.Task.IsCompleted || ask != p) return;
            if (p.Kind == Ask.Pick)
            {
                var cands = Enumerable.Range(0, p.Options.Count).ToList();
                if (sheetCards.Count > 0 && drive.Int(2) == 0) sheetCards[drive.Int(sheetCards.Count)]();
                else
                {
                    var c = p.Options[cands[drive.Int(cands.Count)]].Card!;
                    var s = views[c.Uid].S;
                    if (s.Loc is Loc.Monster or Loc.Spell or Loc.Field || (s.Loc == Loc.Hand && s.Side == 0)) CardLeft(c.Uid);
                    else if (sheetCards.Count > 0) sheetCards[drive.Int(sheetCards.Count)]();
                }
                if (!done.Task.IsCompleted && picked.Count >= p.Min && sheetChips.Count > 0 && drive.Int(2) == 0) sheetChips[0]();
                continue;
            }
            if (attacker >= 0)
            {
                var targets = p.Options.Where(o => o.Act == Act.Attack && o.Card?.Uid == attacker && o.Target != null).Select(o => o.Target!.Uid).ToList();
                if (targets.Count > 0 && drive.Int(3) != 0) CardLeft(targets[drive.Int(targets.Count)]);
                else if (sheetChips.Count > 0) sheetChips[drive.Int(sheetChips.Count)]();
                continue;
            }
            if (p.Kind is Ask.Main or Ask.Battle)
            {
                var clickable = acts.Keys.Where(u => views[u].S.Loc is Loc.Hand or Loc.Monster or Loc.Spell or Loc.Field).ToList();
                if (sheet != null && sheetCards.Count > 0) { sheetCards[drive.Int(sheetCards.Count)](); continue; }
                if (sheet != null && sheetChips.Count > 0) { sheetChips[drive.Int(sheetChips.Count)](); continue; }
                int r = drive.Int(10);
                if (r < 3 || (clickable.Count == 0 && pileActs.Count == 0)) { PhaseLeft(); continue; }
                if (r == 3 && pileActs.Count > 0) { var k = pileActs.Keys.First(); CardLeft(PileTop(k.side, k.loc)!.Card.Uid); continue; }
                if (clickable.Count == 0) continue;
                int uid = clickable[drive.Int(clickable.Count)];
                // Point at the card first so the hover chip shows, as it would for you.
                GetViewport().WarpMouse(cam.UnprojectPosition(views[uid].Root.GlobalPosition));
                await Wait(0.6);
                if (done.Task.IsCompleted || ask != p) return;
                if (drive.Int(2) == 0) { CardRight(uid); await Wait(0.4); }
                CardLeft(uid);
                continue;
            }
            if (sheetChips.Count > 0) sheetChips[drive.Int(sheetChips.Count)]();
        }
        // Safety net so a test run never stalls: fall back to the bot's answer.
        if (!done.Task.IsCompleted && ask == p) { GD.PrintErr($"selftest: no answer for {p}"); done.TrySetResult(Bot(p)); }
    }
}
