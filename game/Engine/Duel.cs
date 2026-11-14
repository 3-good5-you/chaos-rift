namespace PoC.Engine;

/// The duel: state + a coroutine (Run) that advances until it needs a player decision.
/// Inputs are Commands only, so (Setup, Log) fully reproduces a duel: replays, LAN lockstep
/// and AI look-ahead all rebuild from it.
public sealed partial class Duel
{
    public readonly CardDb Db;
    public readonly DuelSetup Setup;
    public readonly Player[] Players = { new(0), new(1) };
    public readonly Rng Rng;
    public readonly List<Command> Log = new();
    public bool Classic => Setup.Classic;

    public int Turn;               // 1-based, counts both players' turns
    public int TurnPlayer;
    public Phase Phase;
    public Prompt? Pending { get; private set; }
    public int Winner { get; private set; } = -2; // -1 draw
    public string EndReason { get; private set; } = "";
    public bool Over => Winner != -2;

    public readonly List<Link> Chain = new();
    public Ev? Window;             // event the current response window is about
    public Ev? ChainBase;          // event the open chain started on
    public Card? Attacker, AttackTarget;
    public Card? Querying;         // attacker whose targets are being listed (Toon rules)
    public bool AttackNegated;
    public readonly HashSet<string> TurnFlags = new();   // cleared at end of turn
    public readonly Dictionary<string, int> Flags = new(); // duel-long flags/counters
    public readonly int[] TurnsTaken = new int[2];

    /// Observer for presentation (animations). Never affects the duel.
    public event Action<Ev>? OnEvent;
    public event Action<string>? OnTrace;
    /// Presentation: a card's triggered effect just did something (raised events); Seq is what it was before it ran,
    /// so the field can show the activation ahead of its results. Never affects the duel.
    public event Action<Card, int, Ev>? OnTriggered;   // card, Seq before it ran, the event it answered

    readonly List<Card> cards = new();
    public IReadOnlyList<Card> Cards => cards;
    Flow<int[]>? waiting;
    readonly Flow root;
    int resolving;
    readonly List<Ev> queued = new();
    internal readonly List<Action> endOfTurn = new();
    readonly List<(int until, Func<Q, Card?, int, bool> rule)> lingering = new();
    readonly List<(int turn, Phase phase, Func<Flow> run)> timers = new();
    public readonly HashSet<string> BattleFlags = new();   // cleared after each attack

    /// A rule that outlives its card (e.g. Mesmeric Control): forbids while Turn <= until.
    public int LingerCount => lingering.Count + watchers.Count + endOfTurn.Count + timers.Count;   // lingering effects (Engine.Check audit)
    public void Linger(int until, Func<Q, Card?, int, bool> forbid) => lingering.Add((until, forbid));

    /// Run `f` at the start of `phase` (Standby or End) of turn `turn`.
    public void Later(int turn, Phase phase, Func<Flow> f) => timers.Add((turn, phase, f));

    async Flow RunTimers(Phase ph)
    {
        foreach (var t in timers.Where(t => t.turn == Turn && t.phase == ph).ToList())
        {
            timers.Remove(t);
            await t.run();
        }
    }

    /// Number of face-up, non-negated copies of a card on the field.
    public int CountActive(string name, int player = -1) =>
        Sources().Count(s => (Scripts.NameOf(s.c) == name || (name == "Umi" && MaidenUmi(s.c))) && (player < 0 || s.c.Controller == player));

    /// Maiden of the Aqua: the field is treated as "Umi" while no Field Spell is active.
    bool MaidenUmi(Card c) => c.Name == "Maiden of the Aqua" && c.Loc == Loc.Monster && !Players.Any(p => p.FieldZone is { FaceUp: true } f && !Negated(f));

    /// Effects that outlive their card for a while ("this turn, each time..."): called on every event while Turn <= until.
    public void Watch(int until, Func<Ev, Flow> f) => watchers.Add((until, f));
    readonly List<(int until, Func<Ev, Flow> f)> watchers = new();

    /// Cards that left the field (controller, Level there, destination, turn): "if a monster you controlled was sent to the GY this turn".
    public readonly List<(Card c, int controller, int level, Loc to, int turn, bool faceUp, Dictionary<string, int> vars)> LeftFieldLog = new();
    public readonly List<(string name, int player, int turn)> SummonLog = new();   // every Summon (Lucky Cloud: "Summoned this turn")

    public Duel(DuelSetup setup, IEnumerable<Command>? replay = null, CardDb? db = null)
    {
        Db = db ?? CardDb.Shared;
        Setup = setup;
        Rng = new Rng(setup.Seed);
        for (int i = 0; i < 2; i++) Players[i].Lp = setup.Lp?[i] ?? setup.StartingLp;
        root = Run();
        Rethrow();
        if (replay != null) foreach (var c in replay) Submit(c);
    }

    public Player P(int i) => Players[i & 1];
    public Card CardByUid(int uid) => cards[uid];

    // ---------------------------------------------------------------- input

    public void Submit(Command cmd)
    {
        var p = Pending ?? throw new InvalidOperationException("No pending prompt");
        if (cmd.Player != p.Player) throw new ArgumentException($"Player {cmd.Player} answered a prompt for {p.Player}");
        var picks = cmd.Picks;
        if ((picks.Length < p.Min && !(p.CancelOk && picks.Length == 0)) || picks.Length > p.Max) throw new ArgumentException($"Need {p.Min}-{p.Max} picks, got {picks.Length}: {p}");
        if (picks.Any(i => i < 0 || i >= p.Options.Count) || picks.Distinct().Count() != picks.Length)
            throw new ArgumentException($"Bad picks {cmd} for {p}");
        var f = waiting!;
        waiting = null;
        Pending = null;
        Log.Add(cmd);
        f.Result = picks;
        f.Complete();
        Rethrow();
    }

    void Rethrow()
    {
        if (root.Error != null) throw new InvalidOperationException("Duel engine error: " + root.Error.Message, root.Error);
        if (!Over && Pending == null) throw new InvalidOperationException("Duel stalled without a prompt");
    }

    internal Flow<int[]> AskRaw(Prompt p)
    {
        var f = new Flow<int[]>();
        Seq++;
        Pending = p;
        waiting = f;
        return f;
    }

    public async Flow<Opt> Choose(int player, Ask kind, List<Opt> opts, string text, Card? src = null)
    {
        if (opts.Count == 1 && kind is Ask.Pick or Ask.Choose or Ask.Position) return opts[0];
        var r = await AskRaw(new Prompt { Player = player, Kind = kind, Options = opts, Text = text, Source = src });
        return opts[r[0]];
    }

    public async Flow<List<Card>> Pick(int player, IEnumerable<Card> cands, int min, int max, string text, Card? src = null, bool cancelOk = false)
    {
        var l = cands.Distinct().ToList();
        max = Math.Min(max, l.Count);
        min = Math.Min(min, max);
        if (max == 0) return new();
        // Even a forced pick (one target, exactly enough tributes) is asked: the player sees and confirms it.
        var r = await AskRaw(new Prompt { Player = player, Kind = Ask.Pick, Options = l.Select(c => new Opt(Act.Card, c)).ToList(), Min = min, Max = max, Text = text, Source = src, CancelOk = cancelOk });
        return r.Select(i => l[i]).ToList();
    }

    public async Flow<Card?> PickOne(int player, IEnumerable<Card> cands, string text, Card? src = null, bool optional = false)
    {
        var r = await Pick(player, cands, optional ? 0 : 1, 1, text, src);
        return r.Count > 0 ? r[0] : null;
    }

    /// True when a card's activation hangs on the event at hand (Kuriboh in the damage step, Trap Hole on a summon):
    /// this window is its moment, not just one more chance for a free-chain card. Also a card for one phase only
    /// (Non Aggression Area in your Standby Phase, Return Soul in the End Phase): it couldn't go in the Main Phase.
    public bool WantsThisWindow(Card c)
    {
        var s = S(c);
        if (s?.Can == null) return false;
        try
        {
            if (!s.Can(X(c, null))) return true;
            if (Phase is Phase.Main1 or Phase.Main2) return false;
            var now = Phase;
            Phase = Phase.Main1;
            try { return !s.Can(X(c, null)); } finally { Phase = now; }
        }
        catch (Exception) { return true; }
    }

    public async Flow<bool> YesNo(int player, string text, Card? src = null)
    {
        var r = await AskRaw(new Prompt { Player = player, Kind = Ask.YesNo, Text = text, Source = src, Options = { new(Act.Option, Arg: 1, Text: "Yes"), new(Act.Option, Arg: 0, Text: "No") } });
        return r[0] == 0;
    }

    public async Flow<int> ChooseText(int player, string text, IReadOnlyList<string> options, Card? src = null)
    {
        if (options.Count == 1) return 0;
        var r = await AskRaw(new Prompt { Player = player, Kind = Ask.Choose, Text = text, Source = src, Options = options.Select((o, i) => new Opt(Act.Option, Arg: i, Text: o)).ToList() });
        return r[0];
    }

    // ---------------------------------------------------------------- scripts & queries

    /// The card's script; Arcana Call lends a monster another card's effect until the End Phase (Vars copyidx / copyturn).
    public Script? S(Card c) => c.Var("copyturn") == Turn && c.Var("copyidx") > 0 && c.Loc == Loc.Monster ? Scripts.Get(Db.Get(c.Var("copyidx") - 1)) : Scripts.Get(c.Data);
    public Ctx X(Card c, Ev? ev = null) => new() { D = this, Self = c, Ev = ev };
    internal void Trace(string s) => OnTrace?.Invoke(s);

    public IEnumerable<Card> AllMonsters => Players.SelectMany(p => p.MonstersOnField);
    public IEnumerable<Card> FieldCards => Players.SelectMany(p => p.FieldCards);
    public IEnumerable<Card> FaceUpMonsters => AllMonsters.Where(c => c.FaceUp);

    /// Cards whose continuous effects currently apply (face-up on field, not negated).
    IEnumerable<(Card c, Script s)> Sources(bool checkNegation = true)
    {
        foreach (var pl in Players)
            foreach (var c in pl.FieldCards)
            {
                if (!c.FaceUp) continue;
                var s = S(c);
                if (s == null) continue;
                if (checkNegation && Negated(c)) continue;
                yield return (c, s);
            }
    }

    int depth;
    public bool Negated(Card c)
    {
        if (Flags.ContainsKey($"neg{c.Uid}")) return true;   // negated where it lies (Ancient Gear Beast's victims, in the GY too)
        if (++depth > 400) { depth = 0; throw new InvalidOperationException($"Negation recursion at {c}"); }
        try { return NegatedCore(c); } finally { depth--; }
    }

    bool NegatedCore(Card c)
    {
        // ponytail: negation sources are not themselves checked for negation (avoids Jinzo/Royal Decree recursion).
        foreach (var (src, s) in Sources(false))
            if (src != c && s.Forbid != null && s.Forbid(X(src), Q.Negate, c)) return true;
        return false;
    }

    public int QueryPlayer = -1; // player argument for player-level queries (TakeDamage)

    public bool Forbidden(Q q, Card? c, int player = -1)
    {
        QueryPlayer = player;
        foreach (var (until, rule) in lingering)
            if (Turn <= until && rule(q, c, player)) return true;
        foreach (var (src, s) in Sources())
            if (s.Forbid != null && s.Forbid(X(src), q, c)) return true;
        return false;
    }

    public bool Allowed(Q q, Card? c, int player = -1)
    {
        QueryPlayer = player;
        foreach (var (src, s) in Sources())
            if (s.Allow != null && s.Allow(X(src), q, c)) return true;
        return false;
    }

    readonly HashSet<Card> computing = new();

    public string? RebirthRace;
    public Stat StatOf(Card c)
    {
        var st = new Stat(c);
        if (c.Loc == Loc.Grave && RebirthRace != null && Flags.GetValueOrDefault("rebirthturn") == Turn && !Necrovalley()) st.Race = RebirthRace;   // Rebirth Judgment
        if (c.Loc != Loc.Monster || !c.FaceUp) { st.Atk = 0; return st; }
        // A modifier that asks about the card being computed (Insect Queen counting Insects) sees its printed stats.
        if (!computing.Add(c)) return st;
        try { return StatCore(c, st); } finally { computing.Remove(c); }
    }

    Stat StatCore(Card c, Stat st)
    {
        if (c.Var("swapturn") == Turn) (st.BaseAtk, st.BaseDef) = (st.BaseDef, st.BaseAtk);
        // "its original ATK becomes X" until the end of the turn; int.MaxValue = while it stays (Sphere Mode's 4000 ATK Ra)
        if (c.Var("baseatkturn") is var bt && (bt == Turn || bt == int.MaxValue)) st.BaseAtk = c.Var("baseatk") - 1;
        if (c.Var("basedefturn") is var dt && (dt == Turn || dt == int.MaxValue)) st.BaseDef = c.Var("basedef") - 1;
        if (c.Var("hat") == 1) { st.BaseAtk = 0; st.BaseDef = 0; }
        st.Add(c.Var("modatk"), c.Var("moddef"));
        var src = Sources().ToList();
        foreach (var (sc, s) in src) s.Base?.Invoke(X(sc), c, st);
        foreach (var (sc, s) in src) s.Stats?.Invoke(X(sc), c, st);
        st.Add(c.TempAtk, c.TempDef);
        if (TurnFlags.Contains("reverse")) { st.Atk = -st.Atk; st.Def = -st.Def; }
        return st;
    }

    /// Monsters whose ATK/DEF a face-up card's continuous effect is changing right now (Field Spells, Bladefly,
    /// equips...): for the field's link lines.
    public List<Card> AffectedBy(Card src)
    {
        var s = S(src);
        if (!src.OnField || !src.FaceUp || s == null || (s.Stats == null && s.Base == null) || Negated(src)) return new();
        return FaceUpMonsters.Where(m => m != src && Changes(m)).ToList();
        bool Changes(Card m)
        {
            var st = new Stat(m);
            s.Base?.Invoke(X(src), m, st);
            s.Stats?.Invoke(X(src), m, st);
            return st.Atk != 0 || st.Def != 0 || st.BaseAtk != m.Data.Atk || st.BaseDef != m.Data.Def;
        }
    }

    /// Face-up cards locked onto one card (Spellbinding Circle, Call of the Haunted, Premature Burial...): both ends,
    /// for the field's link lines.
    public List<Card> TiedTo(Card c)
    {
        int Tie(Card s) => Math.Max(s.Var("target"), Flags.GetValueOrDefault($"burial{s.Uid}")) - 1;
        var l = FieldCards.Where(s => s != c && s.FaceUp && Tie(s) == c.Uid).ToList();
        if (c.OnField && Tie(c) is >= 0 and var u && u < cards.Count && cards[u].OnField) l.Add(cards[u]);
        return l;
    }

    public int Atk(Card c) { var s = StatOf(c); return Math.Max(0, s.BaseAtk + s.Atk); }
    public int Def(Card c) { var s = StatOf(c); return Math.Max(0, s.BaseDef + s.Def); }
    public string Race(Card c) => StatOf(c).Race;
    public string Attr(Card c) => StatOf(c).Attr;
    /// Level with continuous changes (A Legendary Ocean in the hand and on the field) and its own changes on the field.
    public int Level(Card c)
    {
        if (!c.Data.IsMonster && c.AsMonster == null) return 0;
        int lv = (c.AsMonster ?? c.Data).Level + (c.Loc == Loc.Monster ? c.Var("modlevel") : 0) - (c.Var("lvdownturn") == Turn && c.Loc is Loc.Hand or Loc.Monster ? c.Var("lvdown") : 0);
        if (c.Loc is Loc.Hand or Loc.Monster && c.Data.Level > 0) foreach (var (sc, s) in Sources()) if (s.LevelMod != null) lv += s.LevelMod(X(sc), c);
        return c.Data.Level > 0 ? Math.Max(1, lv) : lv;
    }

    // ---------------------------------------------------------------- run

    async Flow Run()
    {
        try
        {
            await Start();
            while (true)
            {
                Turn++;
                TurnsTaken[TurnPlayer]++;
                if (Flags.GetValueOrDefault($"skipturn{TurnPlayer}") > 0) Flags[$"skipturn{TurnPlayer}"] = 0; // Gamble
                else await PlayTurn();
                if (Setup.TurnLimit > 0 && Turn >= Setup.TurnLimit) throw new DuelOver(1, "Puzzle not solved");
                TurnPlayer ^= 1;
            }
        }
        catch (DuelOver e)
        {
            Winner = e.Winner;
            EndReason = e.Reason;
            Pending = null;
            Trace($"Duel over: {(Winner < 0 ? "draw" : "P" + Winner + " wins")} ({e.Reason})");
        }
    }

    async Flow Start()
    {
        for (int i = 0; i < 2; i++)
        {
            var pl = Players[i];
            foreach (var idx in Setup.Main[i]) { var c = NewCard(Db.Get(idx), i); c.Loc = Loc.Deck; pl.Deck.Add(c); }
            foreach (var idx in Setup.Extra[i]) { var c = NewCard(Db.Get(idx), i); c.Loc = Loc.Extra; pl.Extra.Add(c); }
            // Fusion monsters listed in a main deck go to the Fusion Deck.
            foreach (var c in pl.Deck.Where(c => c.Data.IsFusion).ToList()) { pl.Deck.Remove(c); c.Loc = Loc.Extra; pl.Extra.Add(c); }
            if (Setup.Shuffle) Rng.Shuffle(pl.Deck);
        }
        TurnPlayer = Setup.FirstPlayer;
        await Draw(TurnPlayer, 5);
        await Draw(1 - TurnPlayer, 5);
    }

    public Card NewCard(CardData d, int owner)
    {
        var c = new Card(cards.Count, d, owner);
        cards.Add(c);
        return c;
    }

    // ---------------------------------------------------------------- events

    static readonly HashSet<EvKind> Respondable = new()
    {
        EvKind.Summoned, EvKind.Set, EvKind.AttackDeclared, EvKind.DamageCalc, EvKind.Damage, EvKind.Destroyed,
        EvKind.ToGrave, EvKind.Equipped, EvKind.Drew, EvKind.Discarded, EvKind.ToHand, EvKind.Standby,
        EvKind.BattleStart, EvKind.EndPhase, EvKind.MainStart, EvKind.BattleEnd,
    };

    int raiseDepth;
    public int Seq;                // bumps on every observable change (coverage/debug only)
    public async Flow Raise(Ev e)
    {
        if (++raiseDepth > 200) { raiseDepth = 0; throw new InvalidOperationException($"Event recursion at {e}"); }
        if (e.Kind == EvKind.Summoned && e.Card != null) SummonLog.Add((e.Card.Name, e.Card.Controller, Turn));
        try { await RaiseCore(e); } finally { raiseDepth--; }
    }

    /// Tribute Summon: the tributes' own events (left the field, sent to the GY) are shown at once but held here, and
    /// their reactions ("when this card is sent to the GY") run after the summon (playtest 2026-11-13: a tributed Sangan
    /// searched before the summon landed, so the card hopped about before reaching the GY).
    List<Ev>? held;

    async Flow RaiseCore(Ev e, bool replay = false)
    {
        if (!replay)
        {
            Seq++;
            OnEvent?.Invoke(e);
            Trace(e.ToString());
            if (held != null) { held.Add(e); return; }
        }
        // Automatic (triggered/continuous) reactions. Turn player's cards first.
        // A card just banished also sees its own Banished event ("If this card is banished": Kaitoptera).
        var reactors = Reactors();
        if (e.Kind == EvKind.Banished && e.Card is { Loc: Loc.Banished } bc && !reactors.Contains(bc)) reactors.Add(bc);
        foreach (var c in reactors)
        {
            var s = S(c);
            if (s?.On == null) continue;
            if (c.OnField && ((!c.FaceUp && !s.AlwaysActive) || Negated(c))) continue;
            if (!c.OnField && Flags.ContainsKey($"neg{c.Uid}")) continue;
            int before = Seq;
            await s.On(X(c, e), e);
            if (Seq != before) OnTriggered?.Invoke(c, before, e);
        }
        foreach (var (until, f) in watchers.ToList()) if (Turn <= until) await f(e);
        watchers.RemoveAll(w => w.until < Turn);
        CheckWin();
        if (!Respondable.Contains(e.Kind)) return;
        if (Chain.Count > 0 || resolving > 0) queued.Add(e);
        else await OpenWindow(e);
    }

    List<Card> Reactors()
    {
        var l = new List<Card>();
        foreach (var i in new[] { TurnPlayer, 1 - TurnPlayer })
        {
            var p = Players[i];
            l.AddRange(p.FieldCards);
            l.AddRange(p.Hand);
            l.AddRange(p.Grave);
        }
        return l;
    }

    async Flow OpenWindow(Ev e)
    {
        // An Egyptian God's Normal Summon opens no response window: nothing can negate it or answer it.
        if (e is { Kind: EvKind.Summoned, Card: { } god } && (SummonKind)e.Arg is SummonKind.Normal or SummonKind.Tribute && S(god)?.SummonLock == true) return;
        var prev = Window;
        Window = e;
        try
        {
            foreach (var p in new[] { 1 - TurnPlayer, TurnPlayer })
            {
                var opts = ResponseOptions(p);
                if (opts.Count == 0) continue;
                opts.Add(new Opt(Act.Pass));
                var o = await Choose(p, Ask.Respond, opts, $"Respond to {e.Kind}?");
                if (o.Act == Act.Pass) continue;
                await ActivateChain(o.Card!, p, FxOf(o));
                break;
            }
        }
        finally { Window = prev; }
    }

    void CheckWin()
    {
        bool a = Players[0].Lp <= 0, b = Players[1].Lp <= 0;
        if (a && b) throw new DuelOver(-1, "both players reached 0 LP");
        if (a) throw new DuelOver(1, "LP reached 0");
        if (b) throw new DuelOver(0, "LP reached 0");
        foreach (var pl in Players)
        {
            var names = pl.Hand.Select(c => c.Name).ToHashSet();
            if (Exodia.All(names.Contains)) throw new DuelOver(pl.Index, "Exodia");
        }
    }

    public static readonly string[] Exodia =
    {
        "Exodia the Forbidden One", "Left Arm of the Forbidden One", "Right Arm of the Forbidden One",
        "Left Leg of the Forbidden One", "Right Leg of the Forbidden One",
    };

    public void Win(int player, string reason) => throw new DuelOver(player, reason);

    /// Sandbox only (cards put on the field by hand): recompute the open Main / Battle prompt's options in place, so the
    /// waiting phase loop sees them. Breaks the command log's replay; never used in a real duel.
    public void RefreshPrompt()
    {
        if (Pending is not { Kind: Ask.Main or Ask.Battle } p) return;
        var fresh = p.Kind == Ask.Main ? MainOptions(p.Player, Phase == Phase.Main1) : BattleOptions(p.Player);
        p.Options.Clear(); p.Options.AddRange(fresh);
    }

    /// Simple text hash of the visible state, for determinism checks.
    public string StateHash()
    {
        var sb = new System.Text.StringBuilder();
        sb.Append($"T{Turn} P{TurnPlayer} {Phase} R{Rng.State};");
        foreach (var p in Players)
        {
            sb.Append($"|{p.Lp} D{string.Join(",", p.Deck.Select(c => c.Uid))} H{string.Join(",", p.Hand.Select(c => c.Uid))}");
            sb.Append($" M{string.Join(",", p.Monsters.Select(c => c == null ? "-" : $"{c.Uid}{(c.FaceUp ? "u" : "d")}{(c.Defense ? "D" : "A")}"))}");
            sb.Append($" S{string.Join(",", p.Spells.Select(c => c == null ? "-" : $"{c.Uid}{(c.FaceUp ? "u" : "d")}"))} F{p.FieldZone?.Uid}");
            sb.Append($" G{string.Join(",", p.Grave.Select(c => c.Uid))} B{string.Join(",", p.Banished.Select(c => c.Uid))}");
        }
        return sb.ToString();
    }

    /// AI look-ahead only (on a Replay copy): re-deal everything `viewer` cannot see, so a simulation can't peek.
    /// The other player's hand and face-down cards are re-drawn from their unseen cards (hand + deck + face-downs),
    /// both deck orders are shuffled and the RNG reseeded. The copy no longer matches its log.
    public void Redeal(int viewer, ulong seed)
    {
        var rng = new Rng(seed);
        var opp = P(1 - viewer);
        var downS = opp.Spells.Where(c => c != null && !c.FaceUp && !c.IsSpellLike).Select(c => c!).ToList();
        var downM = opp.Monsters.Where(c => c != null && !c.FaceUp && c.Owner == opp.Index).Select(c => c!).ToList();
        var unseen = opp.Hand.Where(c => c.Owner == opp.Index).Concat(opp.Deck).ToList();
        // Face-down cards: each may be any unseen card of the same kind (a Set monster stays a small monster).
        void Swap(Card old, bool spellZone, Func<Card, bool> fits)
        {
            unseen.Insert(rng.Int(unseen.Count + 1), old);
            var neu = unseen.First(fits);
            unseen.Remove(neu);
            if (neu == old) return;
            var arr = spellZone ? opp.Spells : opp.Monsters;
            int zone = old.Zone;
            (int set, int sumTurn, var kind, bool def) = (old.SetTurn, old.SummonTurn, old.Summoned, old.Defense);
            old.ResetFieldState(); old.Zone = -1;
            neu.ResetFieldState();
            arr[zone] = neu; neu.Loc = spellZone ? Loc.Spell : Loc.Monster; neu.Zone = zone; neu.Controller = opp.Index;
            neu.SetTurn = set; neu.SummonTurn = sumTurn; neu.Summoned = kind; neu.Defense = def;
        }
        rng.Shuffle(unseen);
        foreach (var c in downS) Swap(c, true, x => x == c || !x.Data.IsMonster);
        foreach (var c in downM) Swap(c, false, x => x == c || (x.Data.IsMonster && !x.Data.IsFusion && x.Data.Level <= 4));
        rng.Shuffle(unseen);
        int inHand = opp.Hand.Count(c => c.Owner == opp.Index);
        opp.Hand.RemoveAll(c => c.Owner == opp.Index);
        opp.Deck.Clear();
        for (int i = 0; i < unseen.Count; i++)
        {
            var c = unseen[i];
            c.FaceUp = false; c.Controller = opp.Index;
            if (i < inHand) { c.Loc = Loc.Hand; opp.Hand.Add(c); } else { c.Loc = Loc.Deck; opp.Deck.Add(c); }
        }
        rng.Shuffle(P(viewer).Deck);
        Rng.Reseed(rng.Next());
    }

    /// StateHash as a number that is the same on every PC (string.GetHashCode is not). LAN peers compare it
    /// every turn: a mismatch means the two copies of the duel drifted apart.
    public ulong Hash()
    {
        ulong h = 14695981039346656037UL;
        foreach (char ch in StateHash()) h = (h ^ ch) * 1099511628211UL;
        return h;
    }

    /// Rebuild a duel from its setup + log (AI look-ahead, replays, LAN resync).
    public Duel Replay(int upTo = -1) => new(Setup, upTo < 0 ? Log : Log.Take(upTo), Db);
}
