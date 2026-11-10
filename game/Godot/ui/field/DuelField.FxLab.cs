using Godot;
using PoC.Engine;

namespace PoC.Game.UI;

/// `--fxlab` (game/fxlab.bat): the VFX Lab panel, round "finishes, discards". All locked 2026-10-29 (Exodia finish,
/// Thief's pull, burn and redraw, banish as it was); the rows replay what duels now do.
/// `--cardlab` (game/cardlab.bat, CardLabEvents) is the card testing side: the SANDBOX, then every official card
/// (anime pool) the engine scripts, alphabetical: official text, rulings and how the game plays it; open it in the sandbox,
/// mark OK / WRONG / UNSURE + comment. BACK TO LAB (top right) or the duel's end returns here.
/// Picks: user://fx_lab_picks.json / user://card_lab_picks_pool.json, readable in game/fx_picks.txt / game/card_picks.txt
/// (the PoC rounds' picks are kept in game/card_picks_round2.txt).
public partial class DuelField
{
    bool fxLab, cardLab, backToLab;
    static string? labLaunch;        // a bug scenario to start live on the next load of this scene
    static string? labReturn;        // the lab row to come back to, by name (rows marked OK drop off on reload, so indexes shift)
    readonly Dictionary<string, Action> labShow = new();       // shown when the row is selected (scenario / effect pages)
    readonly Dictionary<string, string[]> labVerdicts = new();  // rows judged with words instead of variant letters

    string PickName(string ev, int i, int vars) =>
        labVerdicts.TryGetValue(ev, out var w) ? (i >= 0 && i < w.Length ? w[i] : "") : ((char)('A' + i)).ToString();

    // ---------------------------------------------------------------- staging

    PoC.Engine.DuelSetup FxLabSetup()
    {
        var db = CardDb.Shared;
        string[] you = { "Exodia the Forbidden One", "Left Arm of the Forbidden One", "Right Arm of the Forbidden One", "Left Leg of the Forbidden One", "Right Leg of the Forbidden One",
                         "Kuriboh", "Pot of Greed", "Dark Magician", "Celtic Guardian", "Monster Reborn", "Card Destruction", "Blue-Eyes White Dragon", "White Magical Hat",
                         "Summoned Skull", "Gaia The Fierce Knight", "Curse of Dragon", "Feral Imp", "Raigeki", "Trap Hole", "Mirror Force" };
        string[] them = { "Vorse Raider", "Gemini Elf", "Trap Hole", "Summoned Skull", "Feral Imp", "Gaia The Fierce Knight", "Mirror Force", "Raigeki", "Robbin' Goblin",
                          "Battle Ox", "Celtic Guardian", "Swords of Revealing Light", "Dark Hole", "Kuriboh", "Curse of Dragon", "Blue-Eyes White Dragon" };
        var s = new PoC.Engine.DuelSetup { Seed = 1, FirstPlayer = 0, Shuffle = false, Classic = true };
        List<int> Deck(string[] top) => top.Concat(Enumerable.Range(0, 40 - top.Length).Select(i => Filler[i % Filler.Length])).Select(n => db.Named(n).Idx).ToList();
        s.Main[0].AddRange(Deck(you)); s.Main[1].AddRange(Deck(them));
        return s;
    }

    View Of(int side, string name, int nth = 0) => views.Values.Where(v => v.Card.Owner == side && v.Card.Name == name).OrderBy(v => v.Card.Uid).Skip(nth).First();
    static St HandOf(int side, int i, int n, bool up) => new(Loc.Hand, i, n, up, false, side, 0, 0);
    static St DeckOf(int side, int i = 0, int n = 30) => new(Loc.Deck, i, n, false, false, side, 0, 0);
    static St GyOf(int side, int i = 0, int n = 1) => new(Loc.Grave, i, n, true, false, side, 0, 0);
    static Transform3D Stand(Vector3 at, float tilt = 0.9f, bool faceUp = true, float spin = 0) =>
        new(new Basis(Vector3.Up, spin) * new Basis(Vector3.Right, tilt) * new Basis(Vector3.Forward, faceUp ? 0 : Mathf.Pi), at);

    /// Free flight to any transform (Move only knows zones).
    void Fly(View v, Transform3D to, float dur, Tween.TransitionType tr = Tween.TransitionType.Cubic, Tween.EaseType ease = Tween.EaseType.Out)
    {
        v.Tw?.Kill(); Lit(v, true); v.Root.Visible = true; v.Stat.Visible = false;
        if (v.S.Loc == Loc.Hand) v.S = v.S with { Loc = Loc.None };   // out of the hand: hovering the hand must not pull it back
        var from = v.Root.GlobalTransform;
        var tw = CreateTween(); v.Tw = tw;
        tw.TweenMethod(Callable.From<float>(k => v.Root.GlobalTransform = from.InterpolateWith(to, k)), 0f, 1f, dur).SetTrans(tr).SetEase(ease);
    }

    /// A hand of n cards for a side (your names from the top of the Exodia list, the rival's from theirs).
    List<View> DealHand(int side, int n, int skip = 0)
    {
        var names = side == 0 ? new[] { "Kuriboh", "Pot of Greed", "Dark Magician", "Celtic Guardian", "Monster Reborn", "Raigeki", "Trap Hole" }
                              : new[] { "Vorse Raider", "Gemini Elf", "Trap Hole", "Mirror Force", "Raigeki", "Dark Hole", "Kuriboh" };
        var l = names.Skip(skip).Take(n).Select(x => Of(side, x)).ToList();
        for (int i = 0; i < l.Count; i++) Place(l[i], HandOf(side, i, l.Count, side == 0));
        return l;
    }

    /// The rival's board for the finishes: three monsters and two set cards.
    List<View> RivalBoard()
    {
        var l = new List<View>();
        foreach (var (name, zone) in new[] { ("Summoned Skull", 1), ("Gaia The Fierce Knight", 2), ("Celtic Guardian", 3) }) { var v = Of(1, name); Place(v, Mon(v, zone, 1)); l.Add(v); }
        foreach (var (name, zone) in new[] { ("Swords of Revealing Light", 1), ("Robbin' Goblin", 3) }) { var v = Of(1, name); Place(v, SpellZ(zone, 1, false)); l.Add(v); }
        return l;
    }

    async Task Draws(int side, List<View> into, int n, double gap = 0.14)
    {
        var deck = side == 0 ? new[] { "Blue-Eyes White Dragon", "Summoned Skull", "Gaia The Fierce Knight", "Curse of Dragon", "Feral Imp", "Mirror Force" }
                             : new[] { "Battle Ox", "Swords of Revealing Light", "Curse of Dragon", "Blue-Eyes White Dragon", "Summoned Skull", "Feral Imp" };
        var news = deck.Take(n).Select(x => Of(side, x)).ToList();
        foreach (var v in news) Place(v, DeckOf(side));
        for (int i = 0; i < news.Count; i++)
        {
            int k = i;
            Move(news[i], HandOf(side, i, news.Count, side == 0), 0, 0.4f, 0.4f);
            Cue("draw", k);
            await Wait(gap);
        }
        into.AddRange(news);
        await Wait(0.5);
    }

    void ToGy(View v, int side, int i = 0) { Place(v, GyOf(side, i, i + 1)); }

    // ---------------------------------------------------------------- the events

    void FxLabEvents()
    {
        void Add(string group, string name, params (string, Func<Task>)[] vars) => labEvents.Add((group, name, vars));
        if (cardLab) { CardLabEvents(); return; }

        // All locked 2026-10-29 and playing in duels; kept here to watch them again.
        const string L = "LOCKED";
        Add(L, "1  Exodia finish (you win)", ("A  Master Duel from the fire wall on, film (DuelField.ExodiaMd.cs)", () => ExodiaLabMd(0, false)),
            ("B  The same, native Godot build (in duels; DuelField.ExodiaNative.cs)", () => ExodiaLabMd(0, true)),
            ("C  Old: the original game's Exodia charges and fires (fallback without the frames)", () => ExodiaLab(0)));
        Add(L, "2  Exodia finish (the rival wins)", ("A  Master Duel, film", () => ExodiaLabMd(1, false)), ("B  Master Duel, native", () => ExodiaLabMd(1, true)),
            ("C  Old: the ball comes at you", () => ExodiaLab(1)));
        Add(L, "3  Random discard: a card from the rival's hand", ("Thief's pull: a stream from the effect's card yanks the card out of the hand", () => DiscardThief(1)));
        Add(L, "4  Random discard: a card from your hand", ("Thief's pull", () => DiscardThief(0)));
        Add(L, "5  Card Destruction / Morphing Jar (both hands)", ("Burn and redraw: both hands burn at once, then each player draws the same number", () => RedrawBurn()));
        Add(L, "40  Banish", ("The pile behind the Deck, off the table (as it was)", () => BanishNow()));

    }

    /// `--cardlab` (game/cardlab.bat): the card testing lab. The sandbox, then every scripted official card, A to Z;
    /// a card marked OK drops off the list.
    void CardLabEvents()
    {
        void Add(string group, string name, params (string, Func<Task>)[] vars) => labEvents.Add((group, name, vars));

        // ---- the sandbox: any card anywhere, either side
        labVerdicts["6  SANDBOX: build any situation"] = Array.Empty<string>();
        Add("SANDBOX", "6  SANDBOX: build any situation", ("Open the sandbox (empty board, your Main Phase 1)", () => Launch("sandbox", "6  SANDBOX: build any situation")));
        labShow["6  SANDBOX: build any situation"] = () => InfoPage("Sandbox", new[]
        {
            ("WHAT IT IS", "A live duel on your turn 3 with both hands empty. The SANDBOX panel (bottom right) adds any card for you or the rival: hand, Attack / Defense / set monster, Spell & Trap zone (set or face-up), Field Zone, Graveyard, banished, top of the Deck, Fusion Deck."),
            ("HOW", "Search a card, pick YOU or RIVAL, click where it goes. Cards can be added whenever the duel is waiting on a Main or Battle Phase choice of yours. LP buttons change either side's LP."),
            ("THE RIVAL", "RIVAL PLAYED BY: CPU (it plays its own turns and responses) or ME (you answer for both sides; both hands are shown)."),
        }, null);

        // ---- official cards (anime pool): numbered A to Z over the whole pool, so a card keeps its number as more get scripted.
        // Listed by family (archetype / cards naming each other, Engine Families) so dependent cards are tested together;
        // a card with no family (or the only one of its family left to test) is listed A to Z after the families.
        var db = CardDb.Shared;
        var all = db.ByIdx.Values.Where(d => d.Modern && !d.IsNormalMonster).GroupBy(d => d.Name).Select(g => g.First()).OrderBy(d => d.Name, StringComparer.OrdinalIgnoreCase).ToList();
        var todo = new List<(int i, CardData d, PoC.Engine.Script sc)>();
        for (int i = 0; i < all.Count; i++)
        {
            var d = all[i];
            if (!Scripts.IsWired(d) || Scripts.Get(d) is not { } sc || sc.Sure) continue;   // auto-approved cards need no test
            if (labPicks.TryGetValue($"{1001 + i}  {d.Name}", out var p) && p.Pick == 0) continue;   // OK: done
            todo.Add((i, d, sc));
        }
        var famCount = todo.GroupBy(t => Families.Of(t.d)).ToDictionary(g => g.Key, g => g.Count());
        string GroupOf(CardData d)
        {
            var f = Families.Of(d);
            if (f.Length > 0 && famCount[f] >= 2) return $"FAMILY · {f.ToUpperInvariant()}";
            char first = char.ToUpperInvariant(d.Name[0]);
            return $"OFFICIAL CARDS · {(char.IsLetter(first) ? first : '#')}";
        }
        foreach (var (i, d, sc) in todo.OrderBy(t => GroupOf(t.d).StartsWith("FAMILY") ? 0 : 1).ThenBy(t => GroupOf(t.d), StringComparer.OrdinalIgnoreCase).ThenBy(t => t.d.Name, StringComparer.OrdinalIgnoreCase))
        {
            string name = $"{1001 + i}  {d.Name}";
            labVerdicts[name] = new[] { "OK", "WRONG", "UNSURE" };
            Add(GroupOf(d), name, ("Open in the SANDBOX with this card", () => Launch($"sandbox:{d.Name}", name)));
            var rows = new List<(string, string)> { ("CARD TEXT", d.Desc) };
            if (sc.Note != null) rows.Add(("HOW THE GAME PLAYS IT", sc.Note));
            if (d.Rulings.Length > 0) rows.Add(("RULINGS", string.Join("\n", d.Rulings.Take(10))));
            var rel = Families.Related(d);
            if (rel.Count > 0) rows.Add(("WORKS WITH (one click in the sandbox panel)", string.Join(", ", rel.Take(14).Select(r => r.Name)) + (rel.Count > 14 ? $" +{rel.Count - 14} more" : "")));
            labShow[name] = () => InfoPage(d.Name, rows.ToArray(), d);
        }
    }

    Task Launch(string key, string row)
    {
        labLaunch = key; labReturn = row;
        GetTree().ReloadCurrentScene();
        return Task.CompletedTask;
    }

    static string Cap(string s) => s.Length == 0 ? s : char.ToUpperInvariant(s[0]) + s[1..];

    /// A page over the field: the card big on the left (if any), labelled text blocks on the right. The blocks scroll
    /// (no shrinking to fit); card text and rulings are split up and their keywords coloured (LabText).
    void InfoPage(string title, (string Label, string Text)[] rows, CardData? card)
    {
        float x0 = 400, w = W - x0 - 430, y = 30;   // the right column (rival panel, MENU, phase circle) stays clear
        if (card != null)
        {
            float ch = (H - 340) * 0.68f, cw = ch / CardH;   // smaller than the page so the text gets the width
            var art = Tmp(new TextureRect { Texture = CardArt.Large(card), ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.Scale, Position = new Vector2(x0, 30), Size = new Vector2(cw, ch), MouseFilter = MouseFilterEnum.Ignore, TextureFilter = TextureFilterEnum.LinearWithMipmaps });
            FadeIn(art);
            x0 += cw + 30; w -= cw + 30;
        }
        float ph = H - 300;
        var p = TmpBox(new Rect2(x0, y, w, ph), Edge(RivalPanels.Gold * new Color(1, 1, 1, 0.6f)));
        p.MouseFilter = MouseFilterEnum.Stop;   // the wheel scrolls the page
        FadeIn(p);
        var t = Put(p, title.ToUpperInvariant(), Impact, 40, Colors.White, new Vector2(28, 18), new Vector2(w - 56, 54), outline: 6);
        FitText(t, 40);
        var scroll = new ScrollContainer { Position = new Vector2(28, 84), Size = new Vector2(w - 40, ph - 100), HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
        p.AddChild(scroll);
        var col = new VBoxContainer { CustomMinimumSize = new Vector2(w - 70, 0) };
        col.AddThemeConstantOverride("separation", 6);
        scroll.AddChild(col);
        foreach (var (label, text) in rows)
        {
            col.AddChild(Text(label, Cond, 20, RivalPanels.Gold, 4));
            string bb = label switch { "CARD TEXT" => LabText.Effects(text), "RULINGS" => LabText.Rulings(text), _ => LabText.Mark(text) };
            var r = new RichTextLabel { BbcodeEnabled = true, Text = bb, FitContent = true, ScrollActive = false, AutowrapMode = TextServer.AutowrapMode.WordSmart,
                CustomMinimumSize = new Vector2(w - 76, 0), MouseFilter = MouseFilterEnum.Pass, SelectionEnabled = false };
            r.AddThemeFontOverride("normal_font", Ui); r.AddThemeFontOverride("bold_font", UiBold ??= Sys(new[] { "Bahnschrift", "Segoe UI" }, 700));
            r.AddThemeFontSizeOverride("normal_font_size", 21); r.AddThemeFontSizeOverride("bold_font_size", 21);
            r.AddThemeColorOverride("default_color", new Color(1, 1, 1, 0.9f));
            r.AddThemeConstantOverride("line_separation", 4); r.AddThemeConstantOverride("paragraph_separation", 10);
            col.AddChild(r);
            col.AddChild(new Control { CustomMinimumSize = new Vector2(0, 8) });
        }
    }
    Font? UiBold;

    // ---------------------------------------------------------------- Exodia

    static readonly string[] Pieces = Duel.Exodia;

    List<View> DealExodia(int side)
    {
        var l = Pieces.Select(n => Of(side == 0 ? 0 : 0, n)).ToList();   // one set of pieces (yours); the rival's finish mirrors them
        for (int i = 0; i < l.Count; i++) Place(l[i], HandOf(side, i, 5, side == 0));
        return l;
    }

    async Task WipeSide(int side, bool burn = false, double gap = 0.1)
    {
        foreach (var v in views.Values.Where(v => v.S.Side == side && v.S.Loc is Loc.Monster or Loc.Spell or Loc.Field).OrderBy(v => Math.Abs(v.S.Index - 2)).ToList())
        {
            if (burn || v.S.Loc != Loc.Monster) EffectBurn(v); else CrackBreak(v, Grid3(v.S) + (side == 1 ? Vector3.Back : Vector3.Forward));
            v.S = v.S with { Loc = Loc.None };
            HideFor(v, 0.05);   // stays hidden (Loc None): the copy burns or shatters, the real card is gone
            await Wait(gap);
        }
    }

    async Task ExodiaLab(int side)
    {
        var p = DealExodia(side); RivalBoardFor(side);
        await Wait(0.5);
        await ExodiaFinal(side, p);
        await WipeSide(1 - side, burn: true, gap: 0.05);
        await Wait(0.6);
        await Finale(side);
    }

    async Task Finale(int side)
    {
        LpHit(1 - side, 8000, 0);
        await Wait(0.6);
        if (side == 0) Cue("banner_special_win"); else Sfx("SE_DUEL_LOSE");
        await Banner(side == 0 ? "YOU WIN" : "KAIBA WINS", side == 0 ? RivalPanels.Gold : Pm.Them, "EXODIA");
    }

    void RivalBoardFor(int side)
    {
        if (side == 0) { RivalBoard(); return; }
        foreach (var (name, zone) in new[] { ("Dark Magician", 1), ("Blue-Eyes White Dragon", 2), ("Summoned Skull", 3) }) { var v = Of(0, name); Place(v, Mon(v, zone, 0)); }
        var t = Of(0, "Mirror Force"); Place(t, SpellZ(2, 0, false));
    }

    // ---------------------------------------------------------------- random discard

    View? Source(int side)
    {
        // The card whose effect makes the other player discard: White Magical Hat (yours) / Robbin' Goblin (theirs).
        if (side == 1) { var h = Of(0, "White Magical Hat"); Place(h, Mon(h, 2, 0)); return h; }
        var g = Of(1, "Robbin' Goblin"); Place(g, SpellZ(2, 1, true)); return g;
    }

    async Task DiscardThief(int side)
    {
        var hand = DealHand(side, 5); var src = Source(side)!;
        await Wait(0.5);
        GoldRays(src);
        await Wait(0.7);
        var v = hand[new Random().Next(hand.Count)];   // random every run
        SoulStream(src.Root.GlobalPosition + Vector3.Up * 0.1f, v.Root.GlobalPosition, 14, FxViolet, 0.6, 0.6f);
        await Wait(0.6);
        Fly(v, new Transform3D(src.Root.GlobalTransform.Basis, src.Root.GlobalPosition + Vector3.Up * 0.35f), 0.45f, Tween.TransitionType.Back, Tween.EaseType.In);
        Cue("control_out");
        await Wait(0.5);
        Fly(v, Stand(src.Root.GlobalPosition + Vector3.Up * 0.6f, 0.9f, faceUp: true, spin: side == 0 ? 0 : 0), 0.25f);
        FrameBurst(src, FxViolet, 0.9f, 1.3f);
        await Wait(0.8);
        Move(v, GyOf(side), 0, 0.45f, 0.4f);
        await Wait(0.45);
        _ = Rebuild(v, FxViolet, 0.4, "rebuild_gy");
        await Wait(1.0);
    }

    // ---------------------------------------------------------------- discard everything, draw the same number

    (List<View> mine, List<View> theirs) BothHands() => (DealHand(0, 5), DealHand(1, 4));

    async Task RedrawBurn()
    {
        var (a, b) = BothHands();
        var card = Of(0, "Card Destruction"); Place(card, SpellZ(2, 0, true));
        await Wait(0.4); GoldRays(card, FxGreen); await Wait(1.0);
        foreach (var (hand, side) in new[] { (a, 0), (b, 1) })
            for (int i = 0; i < hand.Count; i++)
            {
                var v = hand[i];
                if (side == 1) Place(v, v.S with { Up = true });
                EffectBurn(v, "discard_burn"); var from = v.Root.GlobalPosition;
                for (int m = 0; m < 3; m++) { int k = m; After(0.3 + k * 0.05, () => Mote(from, GyPos(side) + Vector3.Up * 0.1f, FxEmber, 0.7, 0.07f, 0.5f)); }
                Place(v, GyOf(side, i, hand.Count)); HideFor(v, 1.1);
            }
        await Wait(1.3);
        foreach (var v in a.Concat(b)) _ = Rebuild(v, FxEmber, 0.4, "rebuild_gy");
        Gone(card);
        await Wait(0.7);
        var na = new List<View>(); var nb = new List<View>();
        await Draws(0, na, a.Count); await Draws(1, nb, b.Count);
    }

    // ---------------------------------------------------------------- bug scenarios (live)

    /// One staged duel (the sandbox, the try duels): your deck top / theirs (hands = first 5, plus the turn-1/turn-2 draws),
    /// what to put on the field before play (Lay), and whether the rival's turn plays live (otherwise you start on turn 3).
    sealed record Stage(string Title, string Todo, string Expect, string[] You, string[] Them, Action<DuelField> Lay, bool CpuLive = false, string[]? Extra = null);

    static readonly string[] F5 = { "Mystical Elf", "Silver Fang", "Battle Ox", "Mystical Elf", "Silver Fang" };
    static string[] Hand(params string[] top) => top.Concat(F5).Take(Math.Max(6, top.Length)).ToArray();
    static string[] Top(string[] hand, params string[] deck) => Hand(hand).Concat(deck).ToArray();

    /// `--trycheck`: stages every effect card's four "try it" duels, has the bots play 300 moves of each, prints any
    /// failure and quits (exit 1 on a failure). `--poolcheck[=filter]`: the same for every scripted official card, and
    /// lists the ones whose script never ran (Engine.Check `poolcheck` does this too, when Smart App Control lets it run).
    void TryCheck(string? pool = null)
    {
        var db = CardDb.Shared;
        int bad = 0, n = 0;
        var names = pool == null
            ? db.Effects.Values.Where(e => e.Src == "text" && db.ByKonami.TryGetValue(e.KonamiId, out var l) && l.Any(d => d.PackMask != 0)).Select(e => db.ByKonami[e.KonamiId].First(x => x.PackMask != 0).Name).ToList()
            : db.ByIdx.Values.Where(d => d.Modern && !d.IsNormalMonster && d.Name.Contains(pool, StringComparison.OrdinalIgnoreCase) && Scripts.IsWired(d)).Select(d => d.Name).Distinct().OrderBy(x => x).ToList();
        foreach (var name in names)
            foreach (var mode in new[] { "hand", "field", "rival", "rivalhand" })
            {
                scenario = $"try:{mode}:{name}"; n++;
                try
                {
                    D = new Duel(ScenarioSetup());
                    ScenarioPlay();
                    bot = new Rng((ulong)n); cpu = Ai.ForLevel(1, (ulong)n, 4);
                    for (int i = 0; i < 300 && !D.Over; i++) D.Submit(D.Pending!.Player == 1 && pool == null ? cpu.Decide(D) : Bot(D.Pending!));   // the pool check: bots on both sides (fast)
                }
                catch (Exception x) { bad++; GD.PrintErr($"trycheck: {scenario}: {x.GetBaseException().Message}"); }
            }
        GD.Print($"trycheck: {n - bad}/{n} staged and played");
        if (pool != null)
            foreach (var name in names.Where(x => Scripts.Hits.GetValueOrDefault(x) == 0)) GD.Print($"poolcheck: never ran: {name}");
        GetTree().Quit(bad == 0 ? 0 : 1);
    }

    /// `--labsweep[=filter]`: every scripted official card that isn't auto-approved, staged in your hand and on your
    /// field (the try board, its family in your Deck) and played by bots for up to 12 turns. Your side only gets what
    /// the field shows you: response windows it passes for you (Worth) stay passed; when the card is offered you take
    /// it. Prints "labsweep: NEVER: <card>" for each card whose effect was never offered to you and never fired, then quits.
    void LabSweep(string filter)
    {
        cardLab = true; LoadPicks();
        var db = CardDb.Shared;
        var names = db.ByIdx.Values.Where(d => d.Modern && !d.IsNormalMonster && Scripts.IsWired(d) && d.Name.Contains(filter, StringComparison.OrdinalIgnoreCase)
                && Scripts.Get(d) is { Sure: false } s && (s.On != null || s.Run != null || s.IgnRun != null || s.Fx.Count > 0 || s.GraveFx != null || s.ProcCan != null))
            .Select(d => d.Name).Distinct().OrderBy(x => x).ToList();
        int n = 0, never = 0, crash = 0;
        foreach (var name in names)
        {
            bool offered = false, fired = false, could = false;   // could: its own activation condition held while it sat in your hand / on your field
            foreach (var mode in new[] { "hand", "field", "grave", "board" })
            for (int seed = 0; seed < 3 && !(offered || fired); seed++)
            {
                scenario = $"try:{mode}:{name}"; n++;
                try
                {
                    D = new Duel(ScenarioSetup());
                    D.OnTriggered += (c, _, _) => { if (c.Name == name) fired = true; };
                    ScenarioPlay();
                    bot = new Rng((ulong)n);
                    for (int i = 0; i < 400 && !D.Over && D.Turn <= 12; i++)
                    {
                        var p = D.Pending!;
                        int pick = -1;
                        if (OS.HasEnvironment("LABSWEEP_LOG")) GD.Print($"  t{D.Turn} {D.Phase} p{p.Player} {p.Kind} '{p.Text}' win={D.Window?.Kind} opts=[{string.Join(", ", p.Options.Take(8))}]");
                        // only moments you could use it: your Main Phase, or a set / face-up card in a Battle Phase or response window
                        if (p.Player == Me && p.Kind is Ask.Main or Ask.Battle or Ask.Respond)
                            foreach (var c in D.P(Me).Hand.Concat(D.P(Me).FieldCards).Where(c => c.Name == name && (p.Kind == Ask.Main || c.Loc != Loc.Hand)))
                                try { if (D.S(c) is { Can: { } can, Run: not null } && can(D.X(c))) { could = true; if (OS.HasEnvironment("LABSWEEP_LOG")) GD.Print($"  COULD t{D.Turn} {D.Phase} {c.Loc} up={c.FaceUp} p{p.Player} {p.Kind} win={D.Window?.Kind}"); } } catch (Exception) { }
                        if (p.Player == Me)
                        {
                            if (p.Kind == Ask.Respond && !Worth(p)) pick = p.Options.FindIndex(o => o.Act == Act.Pass);   // the field passes this one for you
                            else
                            {
                                if (p.Source?.Name == name) offered = true;   // its own question (Yes/No, a pick)
                                int k = p.Options.FindIndex(o => o.Act is Act.Activate or Act.Special && o.Card?.Name == name);
                                if (k >= 0) { offered = true; pick = k; }
                                else if (p.Source?.Name == name && p.Kind == Ask.YesNo) pick = 0;
                            }
                        }
                        D.Submit(pick >= 0 ? new Command(p.Player, new[] { pick }) : Bot(p));
                    }
                }
                catch (Exception x) { crash++; GD.PrintErr($"labsweep: CRASH: {scenario}: {x.GetBaseException().Message}"); }
            }
            if (offered || fired) continue;
            never++;
            int idx = db.ByIdx.Values.Where(d => d.Modern && !d.IsNormalMonster).GroupBy(d => d.Name).Select(g => g.First()).OrderBy(d => d.Name, StringComparer.OrdinalIgnoreCase).ToList().FindIndex(d => d.Name == name);
            var pk = labPicks.GetValueOrDefault($"{1001 + idx}  {name}");
            var sc = Scripts.Get(db.Named(name))!;
            string why = could ? "SUSPECT (its condition held, never offered)" : sc.Can != null && sc.Run != null
                ? (Reachable(name) is var reach && reach > 0 ? $"condition never met by the bots (holds on {reach}/300 random boards)" : "CONDITION NEVER TRUE (0/300 random boards)")
                : "trigger never fired";
            GD.Print($"labsweep: NEVER: {name} | {why}{(pk?.Pick == 0 ? " | you marked OK" : "")}");
        }
        GD.Print($"labsweep: {names.Count} cards, {n} duels, {never} never offered/fired, {crash} crashes");
        GetTree().Quit(0);
    }

    /// How many of 300 random rich boards let the card's own activation condition hold: the card in your hand, set /
    /// face-up on your field or in your GY; both fields, hands, GYs and banished filled from its family and random cards;
    /// random LP; your or the rival's Main / Battle / End Phase. 0 = its condition is likely written too strict.
    int Reachable(string name)
    {
        var db = CardDb.Shared;
        var d0 = db.Named(name);
        var fam = Families.Related(d0);
        var all = db.ByIdx.Values.Where(d => d.Name.Length > 0 && (d.PackMask != 0 || d.Modern)).ToList();
        var rng = new Rng(17);
        int held = 0;
        for (int b = 0; b < 300; b++)
        {
            scenario = $"try:hand:{name}";
            try
            {
                D = new Duel(ScenarioSetup());
                ScenarioPlay();
                CardData Any() => fam.Count > 0 && rng.Int(2) == 0 ? fam[rng.Int(fam.Count)] : all[rng.Int(all.Count)];
                for (int p = 0; p < 2; p++)
                {
                    var pl = D.P(p);
                    for (int i = 0; i < rng.Int(6); i++) { var c = D.NewCard(Any(), p); c.Loc = Loc.Grave; c.FaceUp = true; pl.Grave.Add(c); }
                    for (int i = 0; i < rng.Int(3); i++) { var c = D.NewCard(Any(), p); c.Loc = Loc.Banished; c.FaceUp = true; pl.Banished.Add(c); }
                    for (int i = 0; i < rng.Int(4); i++) { var c = D.NewCard(Any(), p); c.Loc = Loc.Hand; pl.Hand.Add(c); }
                    for (int i = 0; i < 1 + rng.Int(4); i++)
                    {
                        var c = D.NewCard(Any(), p);
                        bool mon = c.Data.IsMonster;
                        if (!D.Place(c, p, mon ? Loc.Monster : c.Data.Is("Field") ? Loc.Field : Loc.Spell)) continue;
                        c.FaceUp = rng.Int(3) != 0; c.Defense = mon && (!c.FaceUp || rng.Int(2) == 0); c.SetTurn = c.SummonTurn = -5;
                        if (mon && c.FaceUp) c.Summoned = c.Data.IsFusion ? SummonKind.Fusion : SummonKind.Normal;
                    }
                    pl.Lp = 500 + rng.Int(16) * 500;
                    for (int i = 0; i < 8; i++)   // Deck and Fusion Deck too ("add 1 ... from your Deck", Fusion Summons)
                    {
                        var c = D.NewCard(Any(), p);
                        if (c.Data.IsFusion) { c.Loc = Loc.Extra; pl.Extra.Add(c); } else { c.Loc = Loc.Deck; pl.Deck.Insert(rng.Int(pl.Deck.Count + 1), c); }
                    }
                }
                var me = D.P(Me).Hand.Concat(D.P(Me).FieldCards).FirstOrDefault(c => c.Name == name) ?? D.NewCard(d0, Me);
                switch (b % 3)
                {
                    case 1: if (me.Loc != Loc.Monster && me.Loc != Loc.Spell) { D.P(Me).Hand.Remove(me); if (D.Place(me, Me, d0.IsMonster ? Loc.Monster : d0.Is("Field") ? Loc.Field : Loc.Spell)) { me.FaceUp = d0.IsMonster || rng.Int(2) == 0; me.SetTurn = me.SummonTurn = -5; if (d0.IsMonster) me.Summoned = d0.IsFusion ? SummonKind.Fusion : SummonKind.Special; } } break;
                    case 2: if (me.Loc != Loc.Grave) { me = D.NewCard(d0, Me); me.Loc = Loc.Grave; me.FaceUp = true; D.P(Me).Grave.Add(me); } break;
                    default: if (me.Loc == Loc.None) { me.Loc = Loc.Hand; D.P(Me).Hand.Add(me); } break;
                }
                D.TurnPlayer = rng.Int(2) == 0 ? Me : 1 - Me;
                D.Phase = new[] { Phase.Main1, Phase.Main1, Phase.Battle, Phase.Main2, Phase.End, Phase.Standby }[rng.Int(6)];
                if (D.S(me) is { Can: { } can } && can(D.X(me))) held++;
            }
            catch (Exception) { }
        }
        return held;
    }

    static Stage? StageFor(string? key) => key == null ? null : key.StartsWith("try:") ? TryStage(key)
        : key.StartsWith("sandbox") ? new Stage("SANDBOX", "", "", Hand("Mystical Elf"), Hand("Mystical Elf"), f => { })
        : null;

    /// "try:MODE:Card Name" — any effect card on a busy board, to see its effect play: in your hand (a Trap or a monster
    /// that can't be Normal Summoned is set / put on your field instead), on your field, on the rival's field, or in the
    /// rival's hand with its turn live. Board: you Celtic Guardian + a set Mystical Elf, Summoned Skull and Trap Hole in
    /// the Graveyard; the rival Vorse Raider, Battle Ox, a set Feral Imp, a set Dark Hole, Curse of Dragon in the Graveyard.
    static Stage TryStage(string key)
    {
        var parts = key.Split(':', 3);
        string mode = parts[1], name = parts[2];
        var d = CardDb.Shared.Named(name);
        bool trap = d.IsTrap, extra = d.IsMonster && (d.IsFusion || d.IsRitual || (Scripts.Get(d)?.NoNormalSummon ?? false));
        bool board = mode == "board";   // "hand" plus up to 3 of its family's monsters face-up on your field ("if you control ...")
        if (board) mode = "hand";
        bool yourHand = mode == "hand" && !trap && !extra, yourField = mode == "field" || (mode == "hand" && !yourHand), yourGrave = mode == "grave";
        bool theirField = mode == "rival", theirHand = mode == "rivalhand" && !trap && !extra, theirSet = mode == "rivalhand" && !theirHand;
        string[] mine = { "Celtic Guardian", "Mystical Elf", "Summoned Skull", "Trap Hole" }, theirs = { "Vorse Raider", "Battle Ox", "Feral Imp", "Dark Hole", "Curse of Dragon" };
        var fam = Families.Related(d).Take(24).ToList();   // its family waits in your Deck (Fusions in the Fusion Deck)
        var main = fam.Where(r => !r.IsFusion).Select(r => r.Name).ToList();   // two of them in your opening hand, the rest in the Deck
        var you = Top((yourHand ? new[] { name } : new[] { "Mystical Elf" }).Concat(main.Take(2)).ToArray(), mine.Concat(yourField || yourGrave ? new[] { name } : Array.Empty<string>()).Concat(main.Skip(2)).ToArray());
        var them = Top(theirHand ? new[] { name } : new[] { "Gemini Elf" }, theirs.Concat(theirField || theirSet ? new[] { name } : Array.Empty<string>()).ToArray());
        string where = yourHand ? "It is in your hand." : yourField ? "It is on your field." : yourGrave ? "It is in your Graveyard." : theirHand ? "It is in the rival's hand; watch its turn." : "It is on the rival's field; play your turn into it.";
        return new Stage(name, where, CardDb.Shared.Effects.TryGetValue(d.KonamiId, out var e) ? Cap(e.Does) : "", you, them, f =>
        {
            f.Lay(0, "Celtic Guardian"); f.Lay(0, "Mystical Elf", up: false, def: true); f.Lay(0, "Summoned Skull", Loc.Grave); f.Lay(0, "Trap Hole", Loc.Grave);
            f.Lay(1, "Vorse Raider"); f.Lay(1, "Battle Ox"); f.Lay(1, "Feral Imp", up: false, def: true); f.Lay(1, "Dark Hole", Loc.Spell, up: false); f.Lay(1, "Curse of Dragon", Loc.Grave);
            if (yourField) f.LayTest(0, d);
            if (yourGrave) f.Lay(0, d.Name, Loc.Grave);
            if (board)
                foreach (var r in fam.Where(r => r.IsMonster).Take(3))
                    if (f.D.P(0).Deck.Concat(f.D.P(0).Extra).Any(x => x.Name == r.Name) && f.D.P(0).Monsters.Count(m => m == null) > 1) f.LayTest(0, r);
            if (theirField || theirSet) f.LayTest(1, d);
        }, CpuLive: theirHand || theirSet, Extra: fam.Where(r => r.IsFusion).Select(r => r.Name).ToArray());
    }

    /// The card under test on a field: FLIP monsters set, other monsters face-up, Spells/Traps set (Field Spells in the Field Zone).
    void LayTest(int p, CardData d)
    {
        bool flip = d.IsMonster && d.Desc.StartsWith("FLIP");
        if (d.IsMonster) Lay(p, d.Name, up: !flip, def: flip);
        else Lay(p, d.Name, d.Is("Field") ? Loc.Field : Loc.Spell, up: false);
    }

    /// Put a deck card straight where a scenario needs it (before play starts).
    Card Lay(int p, string name, Loc loc = Loc.Monster, bool up = true, bool def = false)
    {
        var pl = D.P(p);
        var c = pl.Deck.Concat(pl.Extra).First(x => x.Name == name);   // Fusion Monsters listed in a deck sit in the Fusion Deck
        if (loc == Loc.Grave) { pl.Deck.Remove(c); pl.Extra.Remove(c); c.Loc = Loc.Grave; c.FaceUp = true; pl.Grave.Add(c); return c; }
        D.Place(c, p, loc);
        c.FaceUp = up; c.Defense = def; c.SetTurn = -5; c.SummonTurn = -5;
        return c;
    }
}
