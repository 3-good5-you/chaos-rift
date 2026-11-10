using Godot;
using PoC.Engine;

namespace PoC.Game.UI;

/// `--vfxlab`: the duel field with no duel running, for judging the duel's effects in game. Left: every event
/// (locked, changed as asked, new options), then the selected event's variants, a pick and a comment
/// (saved to user://vfx_lab_picks.json and written out readable to game/vfx_picks.txt).
/// Now in the sound round (DuelField.SfxLab.cs): each event's variants are its sound options over the locked effect.
/// Keys: up/down pick the event, 1-6 play a variant (while not typing a comment).
/// `--labplay=EVENT:VARIANT` (1-based, list order) plays one once the field is up (screenshots: add `--shot=... --delay=S`);
/// `--labsel=TEXT` opens the first row whose name contains TEXT; `--labcheck` plays every variant at 4x speed and quits (exit 1 if any failed).
public partial class DuelField
{
    bool lab, labBusy;
    readonly List<(string Group, string Name, (string Label, Func<Task> Run)[] Vars)> labEvents = new();
    readonly List<Button> labRows = new(), labVarButtons = new(), labPickButtons = new();
    readonly Dictionary<string, LabPick> labPicks = new();
    Label labTitle = null!, labSaved = null!;
    LineEdit labNote = null!;
    Control labPickRow = null!;
    int labSel;

    sealed class LabPick { public int Pick { get; set; } = -1; public string Note { get; set; } = ""; }

    PoC.Engine.DuelSetup LabSetup()
    {
        if (fxLab) return FxLabSetup();
        var db = CardDb.Shared;
        string[] you = { "Dark Magician", "Mystical Elf", "Celtic Guardian", "Battle Ox", "Summoned Skull", "Red-Eyes B. Dragon", "Blue-Eyes White Dragon", "Monster Reborn",
                         "Mirror Force", "Sangan", "Pot of Greed", "Time Wizard", "Black Luster Soldier", "Axe of Despair", "Kuriboh",
                         "Graceful Dice", "Kunai with Chain", "DNA Surgery", "Confiscation", "Ground Collapse",
                         "Destiny Board", "Spirit Message \"I\"", "Spirit Message \"N\"", "Spirit Message \"A\"", "Spirit Message \"L\"",
                         "Exodius the Ultimate Forbidden Lord", "Vennominaga the Deity of Poisonous Snakes", "Flying Elephant" };   // + special-win cards (UI Lab round 11)
        string[] them = { "Vorse Raider", "Mystical Elf", "Trap Hole", "Summoned Skull", "Feral Imp", "Gaia The Fierce Knight" };
        var s = new PoC.Engine.DuelSetup { Seed = 1, FirstPlayer = 0, Shuffle = false, Classic = true };
        List<int> Deck(string[] top) => top.Concat(Enumerable.Range(0, 40 - top.Length).Select(i => Filler[i % Filler.Length])).Select(n => db.Named(n).Idx).ToList();
        s.Main[0].AddRange(Deck(you)); s.Main[1].AddRange(Deck(them));
        s.Extra[0].Add(db.Named("B. Skull Dragon").Idx);
        return s;
    }

    // ---------------------------------------------------------------- staging helpers

    View C(string name, int nth = 0) => views.Values.Where(v => v.Card.Name == name).OrderBy(v => v.Card.Uid).Skip(nth).First();
    static St Mon(View v, int zone, int side, bool up = true, bool def = false) => new(Loc.Monster, zone, 0, up, def, side, v.Card.Data.Atk, v.Card.Data.Def);
    static St SpellZ(int zone, int side, bool up) => new(Loc.Spell, zone, 0, up, false, side, 0, 0);
    static St HandAt(int i, int n) => new(Loc.Hand, i, n, true, false, 0, 0, 0);
    static St GyAt(int side = 0) => new(Loc.Grave, 0, 1, true, false, side, 0, 0);
    View Put(string name, St s, int nth = 0) { var v = C(name, nth); Place(v, s); return v; }
    View PutMon(string name, int zone, int side, bool up = true, bool def = false, int nth = 0) { var v = C(name, nth); Place(v, Mon(v, zone, side, up, def)); return v; }
    void Gone(View v) => Place(v, new St(Loc.None, 0, 0, false, false, 0, 0, 0));
    Vector3 At(View v, St s) => Target(v, s).Origin;
    Vector3 GyPos(int side = 0) => Grid3(GyAt(side));
    async Task Go(View v, St s, float dur = 0.45f, float lift = 0.3f) { Move(v, s, 0, dur, lift); await Wait(dur); }

    void LabClear()
    {
        ChainClear(); ClearBadges(); AimClear();
        foreach (var v in views.Values) { Gone(v); v.Face.Emission = Colors.Black; v.Face.AlbedoColor = Colors.White; v.Root.Scale = Vector3.One; }
        foreach (var n in uiTemp) if (IsInstanceValid(n)) n.QueueFree();
        uiTemp.Clear();
        cam.Position = camHome;
        foreach (var l in banishMarks) foreach (var m in l) m.Visible = false;
        SetLp(0, 8000, false); SetLp(1, 8000, false);
        foreach (var n in labHidden) if (IsInstanceValid(n)) n.Visible = true;
        labHidden.Clear();
        CloseSheet();
        if (duelMenu != null) CloseDuelMenu();
    }

    // ---------------------------------------------------------------- lab-only staging

    /// A chain played out: each card flips up with the trigger rays, joins the chain and gets its corner number;
    /// the links then resolve from the top.
    async Task LabChain(View[] links, bool resolve = true)
    {
        for (int i = 0; i < links.Length; i++)
        {
            var v = links[i];
            Move(v, v.S with { Up = true }, 0, 0.35f, 0.3f);
            await Wait(0.35);
            GoldRays(v); ChainLinkFx(v); Badge(v, i + 1);
            await Wait(1.0);
        }
        if (!resolve) return;
        await Wait(0.3);
        for (int i = links.Length - 1; i >= 0; i--) { ChainResolve(); await Wait(0.5); }
        ClearBadges(); ChainClear();
    }

    // ---------------------------------------------------------------- the events

    void LabEvents()
    {
        void Add(string group, string name, params (string, Func<Task>)[] vars) => labEvents.Add((group, name, vars));
        const string L = "LOCKED";
        if (uiLab) { UiLabRift(); return; }   // round 9 (DuelField.UiLabRift.cs); rounds 3-8 are picked and built live
        if (fxLab) { FxLabEvents(); return; }

        // ---- locked: as they play
        Add(L, "1  Destroyed by battle", ("Crack, heat, shatter", async () =>
        {
            var a = PutMon("Dark Magician", 2, 0); var t = PutMon("Vorse Raider", 2, 1);
            await Wait(0.4); Lunge(a, t, 0);
            await Wait(0.46); CrackBreak(t, a.Root.GlobalPosition); await Wait(1.6); Gone(t);
        }));
        Add(L, "2  Destroyed by card effect", ("Burn to embers", async () => { var t = PutMon("Vorse Raider", 2, 1); await Wait(0.4); EffectBurn(t); HideFor(t, 5); await Wait(1.6); }));
        Add(L, "3  Placed in Attack Position", ("Sword clash", async () => { var v = C("Blue-Eyes White Dragon"); Drop(v, Mon(v, 2, 0)); After(0.45, () => SoftLand(v, FxGold)); After(0.7, () => PlacementFx(v, false)); await Wait(2.2); }));
        Add(L, "4  Placed in Defense Position", ("Light shield", async () => { var v = C("Mystical Elf"); Drop(v, Mon(v, 2, 0, def: true)); After(0.45, () => SoftLand(v, FxIce)); After(0.7, () => PlacementFx(v, true)); await Wait(2.2); }));
        Add(L, "9  Tribute Summon", ("Souls to the summon", async () =>
        {
            var t1 = PutMon("Celtic Guardian", 1, 0); var t2 = PutMon("Battle Ox", 3, 0); var n = C("Blue-Eyes White Dragon"); var s = Mon(n, 2, 0);
            await Wait(0.4); SoulsOut(t1, At(n, s)); SoulsOut(t2, At(n, s)); HideFor(t1, 5); HideFor(t2, 5);
            await Wait(1.2); Gone(t1); Gone(t2); Drop(n, s); After(0.45, () => SummonImpact(n, FxWarm, FxGold, "impact_tribute")); After(0.7, () => PlacementFx(n, false)); await Wait(2.2);
        }));
        Add(L, "10  Effect trigger", ("Gold rays", async () => { var v = PutMon("Sangan", 2, 0); await Wait(0.4); GoldRays(v); await Wait(1.6); }));
        Add(L, "13  Both destroyed", ("Meet in the middle", async () =>
        {
            var a = PutMon("Dark Magician", 2, 0); var t = PutMon("Summoned Skull", 2, 1, nth: 1); await Wait(0.3);
            MeetInMiddle(a, t, 0, h => { CrackBreak(t, h); CrackBreak(a, h); }); await Wait(2.0); Gone(a); Gone(t);   // as locked: both crack where they meet
        }));
        Add(L, "14  Direct attack", ("Shockwave on their side", async () =>
        {
            var a = PutMon("Blue-Eyes White Dragon", 2, 0); await Wait(0.3);
            Lunge(a, null, 0, onHit: _ => { DirectHit(0); LpHit(1, 8000, 5000); });
            await Wait(1.8);
        }));
        Add(L, "15  Battle damage / LP loss", ("Red screen edge", async () => { await Wait(0.3); Vignette(FxRed, 0.8); LpHit(0, 8000, 6900); await Wait(1.6); }));
        Add(L, "16  LP hits 0 / duel won", ("Their field shatters", async () =>
        {
            SetLp(1, 600, false); var o = new[] { PutMon("Vorse Raider", 1, 1), PutMon("Gaia The Fierce Knight", 3, 1), PutMon("Feral Imp", 2, 1) }; await Wait(0.3); LpHit(1, 600, 0);
            await Wait(0.4); await FieldShatters(1); Cue("banner_win"); await Banner("YOU WIN", RivalPanels.Gold, "");
        }));
        Add(L, "17  Special Summon (from GY)", ("Rebuilds from embers", async () =>
        {
            var v = C("Dark Magician"); Place(v, Mon(v, 2, 0));
            await Rebuild(v, FxViolet.Lerp(Colors.White, 0.3f), cue: "rebuild_special"); SoftLand(v, FxViolet, "land_special"); After(0.2, () => PlacementFx(v, false)); await Wait(1.6);
        }));
        Add(L, "18  Ritual Summon", ("Circle and pillars", async () =>
        {
            var v = C("Black Luster Soldier"); var s = Mon(v, 2, 0); RitualCircle(At(v, s));
            await Wait(0.7); Drop(v, s); After(0.45, () => SummonImpact(v, FxIce, FxIce, "impact_ritual")); After(0.7, () => PlacementFx(v, false)); await Wait(2.2);
        }));
        Add(L, "19  Flip Summon", ("Pops up and flips", async () => { var v = PutMon("Time Wizard", 2, 0, up: false, def: true); await Wait(0.3); Cue("flip"); await Go(v, Mon(v, 2, 0), 0.5f, 0.8f); SoftLand(v, FxGold); After(0.2, () => PlacementFx(v, false)); await Wait(1.8); }));
        Add(L, "20  Set a Spell/Trap", ("Zone lights up", async () => { var v = Put("Mirror Force", HandAt(1, 3)); await Wait(0.3); Cue("card_slide"); await Go(v, SpellZ(3, 0, false), 0.45f, 0.4f); Cue("card_place"); ZoneLight(v.Root.GlobalPosition, FxIce); await Wait(0.9); }));
        Add(L, "21  Spell/Trap activation", ("Spell green / trap pink", async () =>
        {
            var s = Put("Pot of Greed", SpellZ(1, 0, false)); var t = Put("Mirror Force", SpellZ(3, 0, false)); await Wait(0.3);
            Cue("flip_st"); await Go(s, SpellZ(1, 0, true), 0.35f, 0.3f); GoldRays(s, FxGreen); await Wait(1.0); Cue("flip_st"); await Go(t, SpellZ(3, 0, true), 0.35f, 0.3f); GoldRays(t, FxPink); await Wait(1.4);
        }));
        Add(L, "22  Draw", ("Plain move; 3 in a row", async () =>
        {
            var d = Enumerable.Range(0, 5).Select(i => Put("Silver Fang", new St(Loc.Deck, i, 5, false, false, 0, 0, 0), i)).ToArray(); await Wait(0.3);
            for (int i = 0; i < 3; i++) { Cue("draw", i); Move(d[4 - i], HandAt(i, 3), 0, 0.55f, 0.5f); await Wait(0.3); }
            await Wait(0.8);
        }));
        Add(L, "23  Sent to the Graveyard", ("Sent by an effect, then milled from the deck", async () =>
        {
            var v = PutMon("Battle Ox", 2, 0); var m = Put("Silver Fang", new St(Loc.Deck, 0, 1, false, false, 0, 0, 0)); await Wait(0.3);
            Cue("to_gy"); await Go(v, GyAt(), 0.45f, 0.4f); Cue("gy_land");
            await Wait(0.7); Cue("mill"); await Go(m, new St(Loc.Grave, 1, 2, true, false, 0, 0, 0), 0.45f, 0.4f); Cue("gy_land", 1); await Wait(0.8);
        }));
        Add(L, "24  Returned to hand", ("Flies back", async () => { var v = PutMon("Summoned Skull", 2, 0); await Wait(0.3); Cue("to_hand"); await Go(v, HandAt(1, 3), 0.5f, 0.6f); await Wait(0.7); }));
        Add(L, "28  Change battle position", ("Turn and shield", async () => { var v = PutMon("Battle Ox", 2, 0); await Wait(0.3); Cue("turn"); await Go(v, Mon(v, 2, 0, def: true), 0.4f, 0.2f); PlacementFx(v, true); await Wait(1.4); }));
        Add(L, "29  Control change", ("Arcs across", async () => { var v = PutMon("Summoned Skull", 2, 1, nth: 1); await Wait(0.3); Cue("control_out"); FrameBurst(v, FxEmber, 0.95f, 1.2f); await Go(v, Mon(v, 2, 0), 0.6f, 0.9f); Cue("control_in"); FrameBurst(v, FxGold, 0.9f, 1.3f); await Wait(0.9); }));
        Add(L, "30  LP gain", ("Rising glints", async () => { SetLp(0, 4000, false); await Wait(0.3); LpHit(0, 4000, 5000); LpGlints(0); await Wait(1.5); }));
        Add(L, "31  Phase change", ("Battle lights", async () =>
        {
            await Wait(0.3); Cue("phase_battle"); ShowPhase(Phase.Battle, 0, 3); ZoneWave(0, FxEmber, spells: false, 0.7, cue: "battle_wave"); await Wait(1.4);
        }));
        Add(L, "32  Turn start", ("Your side wakes up", async () =>
        {
            await Wait(0.3); ZoneWave(0, FxGold, spells: true, cue: "turn_wave"); await Wait(0.4); await Banner("YOUR TURN", RivalPanels.Gold, "TURN 3");
        }));
        Add(L, "33  Coin toss", ("Coin", async () => { await Wait(0.3); Coins(new Ev { Kind = EvKind.Coin, Player = Me, Other = C("Time Wizard").Card, Amount = 1, Arg = 1, Call = 1 }); await Wait(3.0); }));
        Add(L, "34  Die roll", ("Die", async () => { await Wait(0.3); await Wait(DiceCup(new Ev { Kind = EvKind.Die, Player = Me, Other = C("Time Wizard").Card, Amount = 5 })); }));
        Add(L, "35  Activation cut-in", ("Side portrait (smaller, below the phase button)", async () => { var v = PutMon("Sangan", 2, 0); await Wait(0.3); GoldRays(v); Portrait(v.Card); await Wait(1.8); }));

        Add(L, "5  Chain link", ("Corner number, no chain", async () => await LabChain(new[] { Put("Monster Reborn", SpellZ(1, 0, false)), Put("Trap Hole", SpellZ(2, 1, false)), Put("Mirror Force", SpellZ(3, 0, false)) })));
        Add(L, "6  Chain break (negated)", ("Glitch, corner number", async () =>
        {
            var c = new[] { Put("Monster Reborn", SpellZ(1, 0, false)), Put("Trap Hole", SpellZ(2, 1, false)) };
            await LabChain(c, resolve: false); NegateGlitch(c[0]); await Wait(1.2); ClearBadges(); ChainClear();
        }));
        Add(L, "7  Attack blocked (DEF higher)", ("Hit-stop with the shield", async () =>
        {
            var a = PutMon("Battle Ox", 2, 0); var t = PutMon("Mystical Elf", 2, 1, def: true, nth: 1); await Wait(0.4);
            Lunge(a, t, 0, onHit: h => BlockHit(t, h), repel: true); await Wait(2.2);
        }));
        Add(L, "8  Fusion Summon", ("Souls swirl, rebuilt on the field", async () =>
        {
            var m1 = PutMon("Summoned Skull", 1, 0); var m2 = PutMon("Red-Eyes B. Dragon", 3, 0);
            var f = C("B. Skull Dragon"); var s = Mon(f, 2, 0); var centre = At(f, s);
            await Wait(0.4);
            SwirlOut(m1, centre); SwirlOut(m2, centre); HideFor(m1, 9); HideFor(m2, 9);
            await Wait(1.3); Place(f, s);
            await Rebuild(f, FxWarm, cue: "rebuild_fusion"); SummonImpact(f, FxWarm, FxGold, "impact_fusion"); After(0.2, () => PlacementFx(f, false)); await Wait(1.8);
        }));
        Add(L, "11  Attack declared", ("Aim line until the attack lands", async () =>
        {
            var a = PutMon("Dark Magician", 2, 0); var t = PutMon("Vorse Raider", 2, 1); await Wait(0.3);
            AimShow(a, Target(t, t.S).Origin);
            await Wait(1.6);   // the response window: the line stands while either player may answer
            After(0.46, AimClear); Lunge(a, t, 0); await Wait(1.2);
        }));
        Add(L, "12  Hit, both survive (DEF equal)", ("Locked blades", async () =>
        {
            var a = PutMon("Celtic Guardian", 2, 0); var t = PutMon("Feral Imp", 2, 1, def: true); await Wait(0.3); Lunge(a, t, 0, grind: true); await Wait(2.0);
        }));
        Add(L, "25  Discard", ("Embers drift to the GY", async () =>
        {
            var v = Put("Kuriboh", HandAt(1, 3)); await Wait(0.3);
            DiscardFx(v, GyPos()); Place(v, GyAt()); HideFor(v, 1.0); After(1.0, () => _ = Rebuild(v, FxEmber, 0.5, "rebuild_gy")); await Wait(2.1);
        }));
        Add(L, "26  ATK/DEF change", ("Glow and count", async () =>
        {
            var v = PutMon("Dark Magician", 2, 0); await Wait(0.3); var was = v.S; Place(v, was with { Atk = 3000 }); UpdateStat(v, true); StatChange(v, was);
            await Wait(1.3); was = v.S; Place(v, was with { Atk = 2000 }); UpdateStat(v, true); StatChange(v, was); await Wait(1.4);   // up, then down
        }));
        Add(L, "27  Equip", ("Gold thread (hover either card to see it again)", async () =>
        {
            var e = Put("Axe of Despair", SpellZ(3, 0, true)); var m = PutMon("Dark Magician", 2, 0); await Wait(0.3); EquipThread(e, m); await Wait(1.6);
        }));
    }

    // ---------------------------------------------------------------- panel, picks and comments

    const int LabMax = 10;   // options per event
    static bool labPlayed;

    string PicksFile => cardLab ? "user://card_lab_picks_pool.json" : fxLab ? "user://fx_lab_picks.json" : uiLab ? "user://ui_lab_picks_round11.json" : "user://sfx_lab_picks_round4.json";   // a new file per round of options, so old answers never sit on new options
    string PicksName => cardLab ? "card_picks.txt" : fxLab ? "fx_picks.txt" : uiLab ? "ui_picks_round11.txt" : "sfx_picks.txt";
    string PicksText => ProjectSettings.GlobalizePath("res://").TrimEnd('/') + "/../" + PicksName;

    void VfxLab()
    {
        lab = true;
        AudioServer.SetBusMute(AudioServer.GetBusIndex("Music"), true);   // only the effects' sounds while judging them
        detail.Visible = false;   // the card details sit where the event list is
        LoadPicks();   // before the events: the card lab leaves out what is already judged
        LabEvents();
        if (!cardLab) labEvents.Sort((a, b) => int.Parse(a.Name.Split(' ')[0]).CompareTo(int.Parse(b.Name.Split(' ')[0])));   // in event order: --labplay=N plays event N (the card lab keeps its family order)
        Button Btn(Control parent, string text, Action press, float w)
        {
            var b = new Button { Text = text, Alignment = HorizontalAlignment.Left, CustomMinimumSize = new Vector2(w, 36), FocusMode = FocusModeEnum.None,
                ClipText = true, TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis };   // long names end in "..." instead of running under the page
            b.AddThemeFontOverride("font", Ui); b.AddThemeFontSizeOverride("font_size", 18);
            b.AddThemeStyleboxOverride("normal", Pill(new Color(0.04f, 0.06f, 0.1f, 0.88f), new Color(RivalPanels.Gold, 0.3f), 10));
            b.AddThemeStyleboxOverride("hover", Pill(new Color(0.09f, 0.11f, 0.17f, 0.95f), RivalPanels.Gold, 10));
            b.AddThemeStyleboxOverride("pressed", Pill(new Color(0.16f, 0.13f, 0.05f, 0.95f), RivalPanels.Gold, 10));
            b.Pressed += press;
            parent.AddChild(b);
            return b;
        }
        // Left column: the event list, then the selected event's variants, pick and comment (the bottom of the screen is the hand).
        var scroll = new ScrollContainer { Position = new Vector2(24, 24), Size = new Vector2(350, H * (uiLab ? 0.42f : 0.5f) - 24), HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
        hud.AddChild(scroll);
        labScroll = scroll;
        var list = new VBoxContainer(); list.AddThemeConstantOverride("separation", 4);
        scroll.AddChild(list);
        string? group = null;
        for (int i = 0; i < labEvents.Count; i++)
        {
            if (labEvents[i].Group != group) { group = labEvents[i].Group; list.AddChild(Text(group, Cond, 18, RivalPanels.Gold, 4)); }
            int k = i; labRows.Add(Btn(list, "", () => LabSelect(k), 330));
        }
        var box = new VBoxContainer { Position = new Vector2(24, H * (uiLab ? 0.42f : 0.5f) + 14), Size = new Vector2(350, 0) };
        box.AddThemeConstantOverride("separation", uiLab ? 4 : 6);   // the UI Lab keeps every round's options: up to 10 a row
        hud.AddChild(box);
        labBox = box;
        labTitle = Text("", Cond, 20, RivalPanels.Gold, 6); box.AddChild(labTitle);
        for (int v = 0; v < LabMax; v++) { int k = v; labVarButtons.Add(Btn(box, "", () => LabRun(labSel, k), 350)); if (uiLab) labVarButtons[^1].CustomMinimumSize = new Vector2(350, 32); }
        // Pick + comment: your choice for this event and what to change about it.
        var pickBox = new VBoxContainer(); pickBox.AddThemeConstantOverride("separation", 6); box.AddChild(pickBox);
        labPickRow = pickBox;
        var row = new HBoxContainer(); row.AddThemeConstantOverride("separation", 6); pickBox.AddChild(row);
        row.AddChild(Text("PICK", Cond, 20, RivalPanels.Gold, 4));
        for (int v = -1; v < LabMax; v++) { int k = v; labPickButtons.Add(Btn(row, v < 0 ? "none" : ((char)('A' + v)).ToString(), () => SetPick(k), v < 0 ? 56 : 30)); }
        Btn(pickBox, "Copy all", () => { DisplayServer.ClipboardSet(PicksSummary()); labSaved.Text = $"Copied (also in game/{PicksName})"; }, 96);
        labNote = new LineEdit { PlaceholderText = "Comment: what to keep or change...", CustomMinimumSize = new Vector2(350, 36) };
        labNote.AddThemeFontOverride("font", Ui); labNote.AddThemeFontSizeOverride("font_size", 17);
        labNote.TextChanged += t => { Pick().Note = t; SavePicks(); };
        labNote.TextSubmitted += _ => { labNote.ReleaseFocus(); LabSelect(labSel); };
        pickBox.AddChild(labNote);
        labSaved = Text("", Ui, 15, new Color(1, 1, 1, 0.6f)); pickBox.AddChild(labSaved);

        LabClear();
        LabSelect(Math.Max(0, labEvents.FindIndex(e => e.Name == labReturn)));
        After(0.1, () => scroll.EnsureControlVisible(labRows[labSel]));   // back from the sandbox: the row you tested in view (once laid out)
        foreach (var a in OS.GetCmdlineUserArgs())
            if (a == "--labcheck") After(1.0, LabCheck);
            else if (a == "--labhide") { labScroll.Visible = labBox.Visible = false; }   // screenshots without the panel
            else if (a.StartsWith("--labsel=") && labEvents.FindIndex(e => e.Name.Contains(a[9..], StringComparison.OrdinalIgnoreCase)) is >= 0 and var sel) LabSelect(sel);   // screenshots of one page
            else if (a.StartsWith("--labplay=") && !labPlayed)
            {
                labPlayed = true;   // once per run: a variant that reloads the field would otherwise replay itself
                var p = a[10..].Split(':');
                int e = int.Parse(p[0]) - 1, v = p.Length > 1 ? int.Parse(p[1]) - 1 : 0;
                After(1.0, () => { LabSelect(e); LabRun(e, v); });
            }
    }

    LabPick Pick()
    {
        var key = labEvents[labSel].Name;
        if (!labPicks.TryGetValue(key, out var p)) labPicks[key] = p = new LabPick();
        return p;
    }

    void SetPick(int v) { Pick().Pick = v; SavePicks(); LabSelect(labSel); }

    void LabSelect(int i)
    {
        if (i < 0 || i >= labEvents.Count) return;
        labSel = i;
        var e = labEvents[i];
        for (int r = 0; r < labRows.Count; r++)
        {
            var ev = labEvents[r];
            string mark = ev.Group == "LOCKED" ? "" : labPicks.TryGetValue(ev.Name, out var p) ? (p.Pick >= 0 ? $"   ✓ {PickName(ev.Name, p.Pick, ev.Vars.Length)}" : "") + (p.Note.Length > 0 ? "  ✎" : "") : "";
            labRows[r].Text = ev.Name + mark;
            labRows[r].Modulate = r == i ? Colors.White : new Color(1, 1, 1, 0.7f);
        }
        labTitle.Text = $"{e.Group}  ·  {e.Name.ToUpperInvariant()}";
        for (int v = 0; v < labVarButtons.Count; v++)
        {
            labVarButtons[v].Visible = v < e.Vars.Length;
            if (v < e.Vars.Length) labVarButtons[v].Text = $"{(char)('A' + v)}   {e.Vars[v].Label}";
        }
        if (labShow.TryGetValue(e.Name, out var show) && !labBusy) { LabClear(); show(); }   // FX Lab pages (DuelField.FxLab.cs)
        bool pickable = e.Group != "LOCKED";
        labPickRow.Visible = pickable;
        if (!pickable) return;
        var pk = labPicks.TryGetValue(e.Name, out var cur) ? cur : null;
        for (int b = 0; b < labPickButtons.Count; b++)
        {
            int v = b - 1;
            bool words = labVerdicts.TryGetValue(e.Name, out var verdicts);   // PASS / BUG, OK / WRONG / UNSURE
            labPickButtons[b].Visible = v < (words ? verdicts!.Length : e.Vars.Length);
            if (v >= 0) { labPickButtons[b].Text = PickName(e.Name, v, e.Vars.Length); labPickButtons[b].CustomMinimumSize = new Vector2(words ? 84 : 30, 36); }
            labPickButtons[b].Modulate = (pk?.Pick ?? -1) == v ? RivalPanels.Gold : new Color(1, 1, 1, 0.6f);
        }
        labNote.SetBlockSignals(true); labNote.Text = pk?.Note ?? ""; labNote.SetBlockSignals(false);
    }

    void LoadPicks()
    {
        if (!Godot.FileAccess.FileExists(PicksFile)) return;
        using var f = Godot.FileAccess.Open(PicksFile, Godot.FileAccess.ModeFlags.Read);
        var d = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, LabPick>>(f.GetAsText());
        if (d != null) foreach (var kv in d) labPicks[kv.Key] = kv.Value;
    }

    void SavePicks()
    {
        using (var f = Godot.FileAccess.Open(PicksFile, Godot.FileAccess.ModeFlags.Write)) f.StoreString(System.Text.Json.JsonSerializer.Serialize(labPicks));
        using (var t = Godot.FileAccess.Open(PicksText, Godot.FileAccess.ModeFlags.Write)) t?.StoreString(PicksSummary());
        labSaved.Text = $"Saved to game/{PicksName}";
    }

    string PicksSummary() => string.Join("\n", labEvents.Where(e => e.Group != "LOCKED").Select(e =>
    {
        var p = labPicks.TryGetValue(e.Name, out var x) ? x : new LabPick();
        string pick = labVerdicts.ContainsKey(e.Name) ? (p.Pick >= 0 ? PickName(e.Name, p.Pick, 0) : "(none)")
            : p.Pick >= 0 && p.Pick < e.Vars.Length ? $"{(char)('A' + p.Pick)} — {e.Vars[p.Pick].Label}" : "(none)";
        return $"{e.Name}: {pick}{(p.Note.Length > 0 ? $"   // {p.Note}" : "")}";
    }).Concat(labPicks.Where(kv => cardLab && kv.Value.Pick == 0 && !labEvents.Any(e => e.Name == kv.Key))   // card lab rows already OK / PASS: off the list, still in the file
        .Select(kv => $"{kv.Key}: {(labVerdicts.TryGetValue(kv.Key, out var w) && w.Length > 0 ? w[0] : "OK")}{(kv.Value.Note.Length > 0 ? $"   // {kv.Value.Note}" : "")}")));

    async void LabRun(int ev, int v)
    {
        if (labBusy || ev < 0 || ev >= labEvents.Count || v < 0 || v >= labEvents[ev].Vars.Length) return;
        labBusy = true;
        try { LabClear(); await labEvents[ev].Vars[v].Run(); }
        catch (Exception e) { GD.PrintErr(e.ToString()); }
        finally { labBusy = false; }
    }

    /// `--labcheck`: plays every variant back to back at 4x speed, prints any failure, then quits (exit code 1 on failure).
    async void LabCheck()
    {
        Godot.Engine.TimeScale = 4;
        int bad = 0, n = 0;
        foreach (var e in labEvents.Where(e => !labVerdicts.ContainsKey(e.Name)))   // not the live scenarios (they reload the scene) or the review pages
            foreach (var (label, run) in e.Vars)
            {
                n++;
                try { LabClear(); await run(); await Wait(0.3); }
                catch (Exception x) { bad++; GD.PrintErr($"labcheck: {e.Name} / {label}: {x}"); }
            }
        GD.Print($"labcheck: {n - bad}/{n} variants ran");
        GetTree().Quit(bad == 0 ? 0 : 1);
    }

    /// Lab keys: up/down pick the event, 1-9 and 0 play its variants (typing in the comment box takes the keys first).
    void LabKey(InputEvent e)
    {
        if (e is not InputEventKey { Pressed: true, Echo: false } k || labNote.HasFocus()) return;
        if (k.Keycode == Key.H) { labScroll.Visible = labBox.Visible = !labBox.Visible; return; }   // hide the lab panel to see what is under it
        if (k.Keycode == Key.Up) LabSelect(labSel - 1);
        else if (k.Keycode == Key.Down) LabSelect(labSel + 1);
        else if (k.Keycode is >= Key.Key1 and <= Key.Key9) LabRun(labSel, (int)(k.Keycode - Key.Key1));
        else if (k.Keycode == Key.Key0) LabRun(labSel, 9);
    }
}
