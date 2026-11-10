namespace PoC.Engine;

// Turn structure, main-phase actions and summoning.
public sealed partial class Duel
{
    public const int HandLimit = 6;

    /// Chaos Duel rules (index = Duel.ChaosRule): name and what it does, for both players, until the next rule.
    public static readonly (string Name, string Text)[] ChaosRules =
    {
        ("DOUBLE DRAW", "Draw 2 cards in your Draw Phase."),
        ("CALM", "No Battle Phase."),
        ("HEALING RIFT", "Gain 500 LP at the start of your turn."),
        ("DOUBLE SUMMON", "You may Normal Summon twice."),
        ("RIFT SURGE", "Every monster gains 500 ATK during each turn."),
    };
    /// The Chaos rule in force (-1 = none yet).
    public int ChaosRule = -1;

    async Flow PlayTurn()
    {
        int tp = TurnPlayer;
        var me = P(tp);
        me.NormalSummoned = false;
        me.ExtraSummons = 0;
        foreach (var c in AllMonsters) { c.Attacks = 0; c.PositionChangedThisTurn = false; }

        // Mischief of the Time Goddess: the opponent's turn is skipped, then this player's turn starts at its Battle Phase.
        bool jump = false;
        if (Flags.GetValueOrDefault("timeskip") is > 0 and var ts)
        {
            if (ts - 1 != tp) { TurnFlags.Clear(); return; }
            Flags.Remove("timeskip"); jump = true;
        }
        // Chaos Duel: on turns 3, 6, 9 ... the rift picks a new rule (seeded, so both PCs agree); it holds until the next.
        if (Setup.Chaos && Turn % 3 == 0) { int r; do r = Rng.Int(ChaosRules.Length); while (r == ChaosRule && ChaosRules.Length > 1); ChaosRule = r; }
        if (ChaosRule == 3) me.ExtraSummons = 1;                                         // DOUBLE SUMMON
        if (ChaosRule == 4) foreach (var c in AllMonsters) c.TempAtk += 500;             // RIFT SURGE (this turn)
        if (ChaosRule == 2) await Gain(tp, 500);                                         // HEALING RIFT
        if (!jump) {
        Phase = Phase.Draw;
        await Raise(new Ev { Kind = EvKind.DrawPhase, Player = tp });
        if (!TurnFlags.Contains($"skipdraw{tp}") && Flags.GetValueOrDefault($"skipdraw{tp}") == 0)
            await Draw(tp, ChaosRule == 0 ? 2 : 1, drawPhase: true);   // PoC (2003 rules): the starting player draws on turn 1 too; Chaos DOUBLE DRAW
        else Flags[$"skipdraw{tp}"] = 0;

        Phase = Phase.Standby;
        await RunTimers(Phase.Standby);
        if (Flags.GetValueOrDefault($"skipstandby{tp}") == 0) await Raise(new Ev { Kind = EvKind.Standby, Player = tp });
        else Flags[$"skipstandby{tp}"] = 0;

        }
        var next = Act.ToBattle;
        if (!jump && Flags.GetValueOrDefault($"skipmain1{tp}") > 0) { Flags.Remove($"skipmain1{tp}"); jump = true; }   // Timeater
        if (!jump)
        {
            Phase = Phase.Main1;
            await Raise(new Ev { Kind = EvKind.MainStart, Player = tp });
            next = await MainPhase(true);
        }
        if (next == Act.ToBattle && ChaosRule != 1)   // Chaos CALM: no Battle Phase
        {
            int battles = 1 + Flags.GetValueOrDefault($"twobattles{tp}");
            Flags[$"twobattles{tp}"] = 0;
            for (int b = 0; b < battles; b++)
            {
                Phase = Phase.Battle;
                await BattlePhase(b);
                foreach (var c in AllMonsters) c.Attacks = 0;
            }
            if (!TurnFlags.Contains("toend")) { Phase = Phase.Main2; await MainPhase(false); }
        }

        Phase = Phase.End;
        await RunTimers(Phase.End);
        await Raise(new Ev { Kind = EvKind.EndPhase, Player = tp });
        // Hand limit (Infinite Cards lifts it).
        int baseLimit = Flags.GetValueOrDefault($"handlimit{tp}") is > 0 and var hl ? hl : HandLimit;   // Hieroglyph Lithograph: 7 for the Duel
        int limit = Sources().Select(x => x.s.HandLimit?.Invoke(X(x.c), tp) ?? -1).Where(l => l >= 0).DefaultIfEmpty(baseLimit).Min();
        if (me.Hand.Count > limit && !Allowed(Q.NoHandLimit, null, tp))
        {
            var d = await Pick(tp, me.Hand, me.Hand.Count - limit, me.Hand.Count - limit, $"Discard down to {limit} cards");
            await Discard(tp, d);
        }
        foreach (var a in endOfTurn.ToList()) a();
        foreach (var c in AllMonsters.Where(c => c.Var("hat") == 1).ToList()) await Destroy(c);
        endOfTurn.Clear();
        foreach (var c in cards) { c.TempAtk = 0; c.TempDef = 0; }
        TurnFlags.Clear();
    }

    // ---------------------------------------------------------------- main phase

    async Flow<Act> MainPhase(bool main1)
    {
        while (true)
        {
            if (TurnFlags.Contains("toend")) return Act.EndTurn;   // Neko Mane King: "it becomes the End Phase"
            var opts = MainOptions(TurnPlayer, main1);
            var o = await Choose(TurnPlayer, Ask.Main, opts, main1 ? "Main Phase 1" : "Main Phase 2");
            switch (o.Act)
            {
                case Act.ToBattle: return Act.ToBattle;
                case Act.EndTurn: return Act.EndTurn;
                default: await DoMainAction(o); break;
            }
        }
    }

    public List<Opt> MainOptions(int p, bool main1)
    {
        var o = new List<Opt>();
        var me = P(p);
        foreach (var c in me.Hand)
        {
            if (c.Data.IsMonster)
            {
                if (CanNormalSummon(c, false)) o.Add(new Opt(Act.Summon, c));
                if (CanNormalSummon(c, true)) o.Add(new Opt(Act.Set, c));
                var s = S(c);
                if (s?.Proc != null && (s.ProcCan?.Invoke(X(c)) ?? true) && CanSpecialSummon(c, p, by: c)) o.Add(new Opt(Act.Special, c));
            }
            else
            {
                if (CanActivate(c, p)) o.Add(new Opt(Act.Activate, c));
                if (CanSetSpell(c, p)) o.Add(new Opt(Act.SetSpell, c));
            }
            if (c.Data.IsMonster && CanActivate(c, p)) o.Add(new Opt(Act.Activate, c));
        }
        foreach (var c in me.MonstersOnField)
        {
            if (!c.FaceUp && c.SetTurn != Turn && !Forbidden(Q.FlipSummon, c) && !Forbidden(Q.ChangePos, c)) o.Add(new Opt(Act.FlipSummon, c));
            if (c.FaceUp && !c.PositionChangedThisTurn && c.SummonTurn != Turn && c.Var("nochangepos") < Turn && !Forbidden(Q.ChangePos, c)) o.Add(new Opt(Act.ChangePos, c));
            if (CanActivate(c, p)) o.Add(new Opt(Act.Activate, c));
        }
        foreach (var c in me.SpellsOnField)
        {
            if (CanActivate(c, p)) o.Add(new Opt(Act.Activate, c));
            var s = S(c);
            if (c.FaceUp && s?.IgnRun != null && (s.IgnCan?.Invoke(X(c)) ?? true) && !Negated(c)) o.Add(new Opt(Act.Activate, c, Arg: 1, Text: "effect"));
        }
        foreach (var c in me.Grave.ToList())
            if (CanActivate(c, p)) o.Add(new Opt(Act.Activate, c));
        o.AddRange(FxOptions(p));
        foreach (var c in me.Deck.Concat(me.Extra).Concat(me.Grave).ToList())
        {
            var s = S(c);
            if (s?.Proc != null && (c.Loc == Loc.Grave ? s.ProcFromGrave : s.ProcFromDeck) && (s.ProcCan?.Invoke(X(c)) ?? true) && CanSpecialSummon(c, p, by: c)) o.Add(new Opt(Act.Special, c));
        }
        // Battle Phase only when one of your monsters could attack (no empty trip through it).
        if (main1 && !TurnFlags.Contains("nobattle") && me.MonstersOnField.Any(m => CanAttack(m) && (AttackTargets(m).Any() || CanAttackDirectly(m)))) o.Add(new Opt(Act.ToBattle));
        o.Add(new Opt(Act.EndTurn));
        return o;
    }

    async Flow DoMainAction(Opt o)
    {
        var c = o.Card!;
        int p = TurnPlayer;
        switch (o.Act)
        {
            case Act.Summon: await NormalSummon(c, false); break;
            case Act.Set: await NormalSummon(c, true); break;
            case Act.Special:
                {
                    var s = S(c)!;
                    await s.Proc!(X(c));
                    break;
                }
            case Act.FlipSummon:
                c.FaceUp = true; c.Defense = false; c.PositionChangedThisTurn = true;
                c.Summoned = SummonKind.Flip; c.SummonTurn = Turn;
                await Flipped(c);
                if (c.Loc == Loc.Monster) await Raise(new Ev { Kind = EvKind.Summoned, Card = c, Arg = (int)SummonKind.Flip, Player = p });
                break;
            case Act.ChangePos:
                c.PositionChangedThisTurn = true;
                await ChangePosition(c, !c.Defense);
                break;
            case Act.SetSpell:
                {
                    if (c.Data.Is("Field")) Place(c, p, Loc.Field); else Place(c, p, Loc.Spell);
                    c.FaceUp = false; c.SetTurn = Turn;
                    if (CountActive("Chain Energy") is > 0 and var ce) Pay(p, 500 * ce);
                    await Raise(new Ev { Kind = EvKind.Set, Card = c, Player = p });
                    break;
                }
            case Act.Activate:
                if (o.Arg == 1) { await S(c)!.IgnRun!(X(c)); break; }
                await ActivateChain(c, p, FxOf(o));
                break;
        }
    }

    // ---------------------------------------------------------------- summoning

    public int TributesFor(Card c)
    {
        var s = S(c);
        if (s?.TributesNeeded != null) return s.TributesNeeded(X(c));
        if (c.Var("notributeturn") == Turn) return 0;   // Ancient Gear Factory
        int lv = Level(c);
        return lv >= 7 ? 2 : lv >= 5 ? 1 : 0;
    }

    /// A card in the GY that lets this Normal Summon go without Tributes (Script.FreeTribute), not used yet.
    /// A face-up card granting one more Normal Summon of this monster this turn (Clear Cube), not used yet this turn.
    Card? ExtraSummonFrom(Card c) => TurnFlags.Contains($"extrasummon{c.Controller}") ? null
        : P(c.Controller).FieldCards.FirstOrDefault(f => f.FaceUp && !Negated(f) && S(f)?.ExtraSummon?.Invoke(X(f), c) == true);

    Card? FreeTributeFrom(Card c, bool set) => new[] { c }.Concat(P(c.Controller).Grave).Concat(P(c.Controller).FieldCards.Where(f => f.FaceUp && !Negated(f)))
        .Concat(P(1 - c.Controller).FieldZone is { FaceUp: true } fz && !Negated(fz) ? new[] { fz } : Array.Empty<Card>())   // Mausoleum of the Emperor works for both players
        .FirstOrDefault(g => g.Var("freetribute") == 0 && S(g)?.FreeTribute?.Invoke(X(g), c, set) == true);

    /// How many Tributes this monster counts as for `forCard` (The Lovers: 2 for an Arcana Force).
    public int TributeWeight(Card t, Card forCard) => Math.Max(t.FaceUp && !Negated(t) && S(t)?.TributeWeight is { } w ? w(X(t), t, forCard) : 1,
        S(forCard)?.TributeWeightFor is { } f ? f(X(forCard), t) : 1);

    /// The monsters Tributed for the latest Tribute Summon (Cloudian - Nimbusman counts its WATER ones).
    public readonly List<Card> LastTributes = new();

    /// Tribute a monster (a Tribute Summon, a Ritual, a cost): "if this card is Tributed" can tell.
    public async Flow Tribute(Card t, Card? by = null) { Flags[$"tributed{t.Uid}"] = Turn; await MoveTo(t, Loc.Grave, by: by); }

    public IEnumerable<Card> Tributable(int p, Card? forCard = null) =>
        P(p).MonstersOnField.Where(m => !Forbidden(Q.Tribute, m) && m.Var("notribute") == 0 && (forCard == null || S(forCard)?.TributeOk?.Invoke(X(forCard), m) != false))
            .Concat(TurnFlags.Contains($"soulexchange{p}") ? P(1 - p).MonstersOnField.Where(m => !Forbidden(Q.Tribute, m) && m.Var("notribute") == 0) : Array.Empty<Card>());

    bool CanNormalSummon(Card c, bool set)
    {
        int p = c.Controller;
        var me = P(p);
        if (c.Loc != Loc.Hand || !c.Data.IsMonster || c.Data.IsFusion || c.Data.IsRitual) return false;
        if (me.NormalSummoned && me.ExtraSummons == 0 && !(CountActive("Ultimate Offering") > 0 && me.Lp > 500) && ExtraSummonFrom(c) == null) return false;
        if (Phase is not (Phase.Main1 or Phase.Main2) || Chain.Count > 0) return false;
        var s = S(c);
        if (s?.NoNormalSummon == true) return false;
        if (s?.NormalSummonOk?.Invoke(X(c)) == false) return false;
        if (set && s?.NoSet == true) return false;
        if (c.Data.Race == "Divine-Beast" && s?.TributesNeeded == null) return false;   // only the scripted Divine-Beasts (the Gods, Sphere Mode); not Immortal Phoenix
        if (Forbidden(set ? Q.SetMonster : Q.NormalSummon, c, p)) return false;
        int need = TributesFor(c);
        int have = Tributable(p, c).Sum(t => TributeWeight(t, c));
        if (have < need && FreeTributeFrom(c, set) == null) return false;
        return (need > 0 && have >= need) || FreeMonsterZones(p) > 0;
    }

    async Flow NormalSummon(Card c, bool set)
    {
        int p = c.Controller;
        var me = P(p);
        int need = TributesFor(c);
        // Elemental HERO Necroshade in the GY: this Normal Summon may skip its Tributes (once per copy).
        if (need > 0 && FreeTributeFrom(c, set) is { } free && (Tributable(p).Count() < need || await YesNo(p, $"Summon {c.Name} without Tributing ({free.Name})?", free)))
        {
            if (S(free)!.OnFreeTribute is { } use) await use(X(free)); else free.Vars["freetribute"] = 1;
            need = 0;
        }
        var tr = new List<Card>();
        // Choosing the tributes comes first and can be backed out of; nothing is spent until they are chosen.
        if (need > 0 && Tributable(p).Any(t => TributeWeight(t, c) > 1))
        {
            // Some tributes count double: pick one at a time until enough.
            while (tr.Sum(t => TributeWeight(t, c)) < need)
            {
                var t = await PickOne(p, Tributable(p).Except(tr), $"Tribute for {c.Name} ({tr.Sum(t => TributeWeight(t, c))}/{need})", c, optional: true);
                if (t == null) return;
                tr.Add(t);
            }
        }
        else if (need > 0 && (tr = await Pick(p, Tributable(p, c), need, need, $"Tribute {need} monster(s) for {c.Name}", c, cancelOk: true)).Count == 0) return;
        if (me.NormalSummoned)
        {
            if (ExtraSummonFrom(c) != null) TurnFlags.Add($"extrasummon{p}");
            else if (me.ExtraSummons > 0) me.ExtraSummons--;
            else Pay(p, 500); // Ultimate Offering
        }
        me.NormalSummoned = true;
        // "You can Tribute any number of X monsters for the Tribute Summon of this card."
        if ((tr.Count > 0 || S(c)?.TributeMax > 0) && S(c)?.TributeAny is { } any)
            while (Tributable(p).Except(tr).Any(t => any(X(c), t)) && (S(c)!.TributeMax == 0 || tr.Count < S(c)!.TributeMax))
            {
                var t = await PickOne(p, Tributable(p).Except(tr).Where(t => any(X(c), t)), $"Tribute more for {c.Name}? (pick none to stop)", c, optional: true);
                if (t == null) break;
                tr.Add(t);
            }
        if (tr.Any(t => t.Controller != p)) TurnFlags.Add("nobattle"); // Soul Exchange
        LastTributes.Clear(); LastTributes.AddRange(tr);
        foreach (var t in tr) await Tribute(t);
        if (CountActive("Chain Energy") is > 0 and var ce) Pay(p, 500 * ce);
        if (!Place(c, p, Loc.Monster)) { await MoveTo(c, Loc.Grave); return; }
        c.SummonTurn = Turn;
        if (set && !Forbidden(Q.SetFaceDown, c, p))
        {
            c.FaceUp = false; c.Defense = true; c.SetTurn = Turn; c.Summoned = SummonKind.Set;
            await Raise(new Ev { Kind = EvKind.Set, Card = c, Player = p });
        }
        else
        {
            // Light of Intervention turns a Set into a face-up Defense summon.
            c.FaceUp = true; c.Defense = set; c.Summoned = need > 0 ? SummonKind.Tribute : SummonKind.Normal;
            await Raise(new Ev { Kind = EvKind.Summoned, Card = c, Arg = (int)c.Summoned, Player = p });
        }
    }

    // ---------------------------------------------------------------- activation & chain

    /// The script acting for this card where it is: a Spell/Trap in the GY uses its GY effect (Script.GraveFx).
    /// Destiny HERO - Diamond Dude: a Normal Spell it sent to the GY may have its effect activated there next turn (Var "ddude").
    public Script? Eff(Card c) => c.Loc == Loc.Grave && !c.Data.IsMonster ? (c.Var("ddude") == Turn && Phase is Phase.Main1 or Phase.Main2 ? S(c) : S(c)?.GraveFx) : S(c);

    public int SpeedOf(Card c)
    {
        var s = Eff(c);
        if (s != null && s.Speed > 0) return s.Speed;
        if (c.Data.IsTrap) return c.Data.Is("Counter") ? 3 : 2;
        if (c.Data.IsSpell) return c.Data.Is("Quick-Play") ? 2 : 1;
        return 1;
    }

    bool HasActivation(Card c, Script? s)
    {
        if (s == null) return Ritual(c) != null && c.Data.IsSpell;
        if (c.Data.IsMonster) return s.Run != null;
        if (s.Run != null || s.Cost != null) return true;
        return c.Data.Is("Equip") || c.Data.Is("Field") || c.Data.Is("Continuous");
    }

    public Ritual? Ritual(Card c) => c.Data.IsSpell ? Db.Rituals.FirstOrDefault(r => r.Spell == c.Data.KonamiId || Db.ByKonami.TryGetValue(r.Spell, out var l) && l.Any(d => d.Name == c.Name)) : null;

    /// An extra effect (Script.Fx[fx - 1]): an effect activation, never a card activation.
    bool CanActivateFx(Card c, int p, int fx)
    {
        var s = S(c)?.Fx.ElementAtOrDefault(fx - 1);
        if (s == null || c.Controller != p) return false;
        bool chainOpen = Chain.Count > 0, window = Window != null || chainOpen;
        int speed = s.Speed > 0 ? s.Speed : 1;
        if (chainOpen && speed < Math.Max(2, Chain[^1].Speed)) return false;
        if (window && speed < 2) return false;
        if (speed == 1 && !(p == TurnPlayer && Phase is Phase.Main1 or Phase.Main2 && !window)) return false;
        bool there = s.FromHand ? c.Loc == Loc.Hand : s.FromGrave ? c.Loc == Loc.Grave : s.InSpellZone ? c.Loc == Loc.Spell && c.FaceUp
            : c.Data.IsMonster ? c.Loc == Loc.Monster && c.FaceUp : c.OnField && c.FaceUp;
        if (!there) return false;
        if (c.Data.IsMonster && !s.AnyTurn && speed >= 2 && p != TurnPlayer && !s.FromHand) return false;
        if (s.OncePerTurn && c.Var($"usedturn{fx}") == Turn) return false;
        if (c.OnField && Negated(c)) return false;
        if (Forbidden(Q.Activate, c, p)) return false;
        return s.Can == null || CanWith(c, s, out _);
    }

    public bool CanActivate(Card c, int p, int fx = 0)
    {
        if (fx > 0) return CanActivateFx(c, p, fx);
        if (c.Var("noactturn") == Turn || c.Var("noactuntil") >= Turn) return false;   // "cannot be activated this turn" (Ancient Gear Drill, Fairy of the Spring)
        var s = Eff(c);
        if (!HasActivation(c, s) && (s == null || !HasActivation(c, s))) return false;
        if (c.Controller != p) return false;
        bool chainOpen = Chain.Count > 0;
        bool window = Window != null || chainOpen;
        int speed = SpeedOf(c);
        if (chainOpen && speed < Math.Max(2, Chain[^1].Speed)) return false;
        if (window && speed < 2) return false;
        bool myMain = p == TurnPlayer && Phase is Phase.Main1 or Phase.Main2 && !window;

        if (c.Data.IsMonster)
        {
            if (s!.FromHand ? c.Loc != Loc.Hand : s.FromGrave ? c.Loc != Loc.Grave : (c.Loc != Loc.Monster || !c.FaceUp)) return false;
            if (speed == 1 && !myMain) return false;
            if (!s.AnyTurn && speed >= 2 && p != TurnPlayer && !s.FromHand) return false;
            if (s.OncePerTurn && c.Var("usedturn") == Turn) return false;
            if (c.OnField && Negated(c)) return false;
        }
        else if (c.Loc == Loc.Grave)
        {
            // A Spell/Trap's effect that activates from the GY (Hero's Rule 1, Sabatiel): an effect, not a card activation.
            if (s == null) return false;
            if (speed == 1 && !myMain) return false;
            if (!s.AnyTurn && speed >= 2 && p != TurnPlayer) return false;
            if (s.OncePerTurn && c.Var("usedturn") == Turn) return false;
        }
        else if (c.Loc == Loc.Hand)
        {
            if (c.Data.IsTrap && !TurnFlags.Contains($"traphand{p}") && s?.HandTrap?.Invoke(X(c)) != true) return false;   // Bubble Illusion lets one Trap be activated from the hand
            if (!c.Data.Is("Field") && FreeSpellZoneCount(p) == 0) return false;
            if (speed == 1 && !myMain) return false;
            if (speed >= 2 && p != TurnPlayer && !(c.Data.IsTrap && s?.HandTrap != null)) return false;
            if (Classic && window && !c.Data.Modern) return false; // PoC: hand spells only at open game state, never as a response
        }
        else if (c.Loc is Loc.Spell or Loc.Field)
        {
            if (c.FaceUp) return false;
            if (c.Data.IsTrap && c.SetTurn == Turn && !(CountActive("Temple of the Kings", p) > 0 && !TurnFlags.Contains($"temple{p}"))) return false;   // Temple of the Kings: 1 Trap the turn it was Set
            if (c.Data.IsSpell && speed >= 2 && c.SetTurn == Turn) return false;
            if (speed == 1 && !myMain) return false;
        }
        else return false;

        if (Forbidden(Q.Activate, c, p)) return false;
        if (c.Data.Is("Equip") && s?.Cost == null && s?.EquipOk != null && !EquipTargets(c).Any()) return false;
        if (s?.Can != null && !CanWith(c, s, out _)) return false;
        if (s == null || (s.Run == null && s.Cost == null && Ritual(c) != null)) return RitualPossible(c, p);
        return true;
    }

    /// Activation condition against the open events: the current window (an activation, when chaining)
    /// or the event the chain started on (PoC lets a second Trap Hole chain onto the first).
    bool CanWith(Card c, Script s, out Ev? ev)
    {
        ev = Window;
        if (s.Can == null || s.Can(X(c, Window))) return true;
        if (ChainBase != null && ChainBase != Window && s.Can(X(c, ChainBase))) { ev = ChainBase; return true; }
        return false;
    }

    int FreeSpellZoneCount(int p) => P(p).FreeSpellZones;

    bool CanSetSpell(Card c, int p) =>
        c.Loc == Loc.Hand && !c.Data.IsMonster && (c.Data.Is("Field") ? P(p).FieldZone == null : P(p).FreeSpellZones > 0)
        && Phase is Phase.Main1 or Phase.Main2 && Chain.Count == 0 && !Forbidden(Q.SetSpell, c, p);

    public IEnumerable<Card> EquipTargets(Card equip)
    {
        var s = S(equip);
        return FaceUpMonsters.Where(m => (s?.EquipOk?.Invoke(X(equip), m) ?? true) && !Forbidden(Q.Target, m) && m.Var("noequip") == 0
            && !(equip.Data.IsSpell && Forbidden(Q.AffectedBySpell, m)));   // Deepsea Warrior / Legendary Fisherman with Umi
    }

    public List<Opt> ResponseOptions(int p)
    {
        var o = new List<Opt>();
        var me = P(p);
        foreach (var c in me.SpellsOnField.Concat(me.Hand).Concat(me.MonstersOnField).Concat(me.Grave).ToList())
            if (CanActivate(c, p)) o.Add(new Opt(Act.Activate, c));
        o.AddRange(FxOptions(p));
        return o;
    }

    /// Activate a card/effect. If no chain is open, this starts one and runs it to the end.
    /// Which effect an Activate option means: 0 = the card's own activation, n = Script.Fx[n - 1].
    public static int FxOf(Opt o) => o.Arg >= 10 ? o.Arg - 9 : 0;

    /// Activate options for every card's extra effects (Script.Fx) that can be used now.
    public IEnumerable<Opt> FxOptions(int p)
    {
        var me = P(p);
        foreach (var c in me.Hand.Concat(me.MonstersOnField).Concat(me.SpellsOnField).Concat(me.Grave).ToList())
            if (S(c) is { Fx.Count: > 0 } s)
                for (int i = 0; i < s.Fx.Count; i++)
                    if (CanActivate(c, p, i + 1)) yield return new Opt(Act.Activate, c, Arg: 10 + i, Text: s.Fx[i].Label);
    }

    public async Flow ActivateChain(Card c, int p, int fx = 0)
    {
        if (Chain.Count > 0) { await AddLink(c, p, fx); return; }
        var basePrev = ChainBase;
        ChainBase = Window;
        await AddLink(c, p, fx);
        // Response loop: the other player first, then alternate until both pass in a row.
        int passes = 0, who = 1 - p;
        while (passes < 2)
        {
            var top = Chain[^1];
            if (top.S.NoResponse || top.Card.Vars.GetValueOrDefault("noresp") == Turn) break;   // "cannot be activated in response to this card's activation" (always, or set by its Cost this turn)
            var prev = Window;
            Window = new Ev { Kind = EvKind.Activated, Link = top, Card = top.Card, Player = top.Player };
            var opts = ResponseOptions(who);
            Opt pick = new(Act.Pass);
            if (opts.Count > 0)
            {
                opts.Add(new Opt(Act.Pass));
                pick = await Choose(who, Ask.Respond, opts, $"{top.Card.Name} activated. Chain another card?");
            }
            if (pick.Act == Act.Pass) { Window = prev; passes++; who = 1 - who; continue; }
            await AddLink(pick.Card!, who, FxOf(pick));
            Window = prev;
            passes = 0;
            who = 1 - who;
        }
        ChainBase = basePrev; // responses are over; windows opened while resolving must not see this chain's event
        await ResolveChain();
    }

    async Flow AddLink(Card c, int p, int fx = 0)
    {
        var s = fx > 0 ? S(c)!.Fx[fx - 1] : Eff(c);
        bool cardAct = fx == 0 && !c.Data.IsMonster && c.Loc != Loc.Grave;
        bool fromHand = c.Loc == Loc.Hand;
        if (fromHand && c.Data.IsTrap) TurnFlags.Remove($"traphand{p}");
        if (!fromHand && c.Data.IsTrap && c.SetTurn == Turn) TurnFlags.Add($"temple{p}");
        TurnFlags.Add($"act{p}");   // Chaos Emperor Dragon: "cannot activate other cards or effects this turn"
        if (cardAct)
        {
            if (fromHand)
            {
                if (c.Data.Is("Field"))
                {
                    var mine = P(p).FieldZone;
                    if (mine != null) await MoveTo(mine, Loc.Grave);
                    Place(c, p, Loc.Field);
                }
                else Place(c, p, Loc.Spell);
                if (CountActive("Chain Energy") is > 0 and var ce) Pay(p, 500 * ce);
            }
            c.FaceUp = true;
        }
        var ev = Window;
        if (s?.Can != null) CanWith(c, s, out ev);
        var x = X(c, ev);
        var link = new Link { Card = c, S = s ?? new Script(), Player = p, X = x, Speed = SpeedOf(c), FromHand = fromHand, IsCardActivation = cardAct };
        x.Link = link;
        if ((c.Data.IsMonster || !cardAct) && s?.OncePerTurn == true) c.Vars[fx > 0 ? $"usedturn{fx}" : "usedturn"] = Turn;
        if (s?.Cost != null) { paying++; try { await s.Cost(x); } finally { paying--; } }
        else if (c.Data.Is("Equip") && s?.EquipOk != null)
        {
            var t = await PickOne(p, EquipTargets(c), $"Equip {c.Name} to which monster?", c);
            if (t != null) x.T.Add(t);
        }
        // A field spell taking effect destroys the opponent's face-up field spell (PoC: one Field on the board).
        if (cardAct && c.Data.Is("Field") && P(1 - p).FieldZone is { FaceUp: true } other) await Destroy(other, c);
        Chain.Add(link);
        link.Number = Chain.Count;
        await Raise(new Ev { Kind = EvKind.Activated, Link = link, Card = c, Player = p });
    }

    async Flow ResolveChain()
    {
        resolving++;
        try
        {
            while (Chain.Count > 0)
            {
                var link = Chain[^1];
                Chain.RemoveAt(Chain.Count - 1);
                var c = link.Card;
                bool negated = link.Negated || (c.OnField && c.FaceUp && Negated(c) && link.IsCardActivation);
                if (negated && !link.Negated) OnEvent?.Invoke(new Ev { Kind = EvKind.Negated, Card = c, Player = link.Player });
                Resolving = link;
                if (!negated)
                {
                    var s = link.S;
                    if (s.Run != null) await s.Run(link.X);
                    else if (c.Data.Is("Equip") && link.X.T0 is { } t && c.OnField) { if (t.Loc == Loc.Monster && t.FaceUp) await Equip(c, t); }
                    else if (link.IsCardActivation && Ritual(c) != null) await RitualSummon(c, link.Player);
                }
                OnEvent?.Invoke(new Ev { Kind = EvKind.Resolved, Link = link, Card = c, Player = link.Player });
                foreach (var r in Reactors().ToList()) if (S(r)?.On is { } on && r.OnField && r.FaceUp && !Negated(r)) await on(X(r, null), new Ev { Kind = EvKind.Resolved, Link = link, Card = c, Player = link.Player });
                if (link.IsCardActivation && c.OnField)
                {
                    bool stays = c.Loc == Loc.Monster || !negated && (c.Data.Is("Continuous") || c.Data.Is("Field") || (c.Data.Is("Equip") && c.EquippedTo != null) || c.Var("stay") == 1);
                    if (!stays) await MoveTo(c, Loc.Grave);
                }
            }
        }
        finally { resolving--; }
        TurnFlags.RemoveWhere(f => f.StartsWith("faketrap"));
        while (queued.Count > 0 && Chain.Count == 0 && resolving == 0)
        {
            var e = queued[0];
            queued.RemoveAt(0);
            await OpenWindow(e);
        }
    }

    public Link? Resolving;   // the chain link resolving now (Different Dimension Dragon: did this effect target it?)

    public async Flow Negate(Link l, bool destroy = true)
    {
        l.Negated = true;
        OnEvent?.Invoke(new Ev { Kind = EvKind.Negated, Card = l.Card, Player = l.Player });
        if (destroy && l.Card.OnField) await Destroy(l.Card);
    }

    // ---------------------------------------------------------------- ritual / fusion

    bool RitualPossible(Card spell, int p)
    {
        var r = Ritual(spell);
        if (r == null) return false;
        var me = P(p);
        foreach (var m in me.Hand.Where(h => MatchesKonami(h, r.Monster)))
        {
            var mats = me.Hand.Where(h => h != m && h.Data.IsMonster).Concat(Tributable(p)).ToList();
            if (mats.Sum(x => Level(x)) >= r.Levels && (FreeMonsterZones(p) > 0 || Tributable(p).Any())) return true;
        }
        return false;
    }

    public bool MatchesKonami(Card c, int konami) =>
        c.Data.KonamiId == konami || (Db.ByKonami.TryGetValue(konami, out var l) && l.Any(d => d.Name == c.Name));

    async Flow RitualSummon(Card spell, int p)
    {
        var r = Ritual(spell)!;
        var me = P(p);
        var m = await PickOne(p, me.Hand.Where(h => MatchesKonami(h, r.Monster)), "Ritual Summon which monster?", spell);
        if (m == null) return;
        var mats = me.Hand.Where(h => h != m && h.Data.IsMonster).Concat(Tributable(p)).ToList();
        var chosen = new List<Card>();
        // ponytail: tribute until the level total is reached (PoC's subset-sum allows any total >= required).
        while (chosen.Sum(x => Level(x)) < r.Levels)
        {
            var left = mats.Except(chosen).ToList();
            if (left.Count == 0) return;
            if (FreeMonsterZones(p) == 0 && !chosen.Any(x => x.OnField)) left = left.Where(x => x.OnField).ToList();
            var t = await PickOne(p, left, $"Tribute for {m.Name} ({chosen.Sum(x => Level(x))}/{r.Levels} levels)", spell);
            if (t == null) return;
            chosen.Add(t);
        }
        foreach (var t in chosen) await MoveTo(t, Loc.Grave);
        await SpecialSummon(m, p, await ChoosePosition(p, m), kind: SummonKind.Ritual, by: spell);
    }

    public async Flow<bool> ChoosePosition(int p, Card c, bool allowSet = false)
    {
        var opts = new List<Opt> { new(Act.Option, c, Arg: 0, Text: "Attack Position"), new(Act.Option, c, Arg: 1, Text: "Defense Position") };
        var o = await Choose(p, Ask.Position, opts, $"Position for {c.Name}", c);
        return o.Arg == 1;
    }

    /// Fusion recipes possible for player p. Materials come from `pool` (default: monsters in hand and on the field);
    /// `resultOk` limits which Fusion Monsters (Miracle Fusion: "Elemental HERO" only).
    public IEnumerable<(Card result, List<Mat> mats)> FusionOptions(int p, bool fieldOnly = false, IEnumerable<Card>? pool = null, Func<Card, bool>? resultOk = null, Card? by = null)
    {
        Scripts.FusingPlayer = p;
        var me = P(p);
        var mats = (pool ?? MaterialPool(p, fieldOnly)).Where(m => (m.Data.IsMonster || m.Loc == Loc.Monster) && S(m)?.NotMaterial != true).ToList();
        foreach (var res in me.Extra.Where(e => e.Data.IsFusion && (resultOk?.Invoke(e) ?? true) && CanSpecialSummon(e, p, SummonKind.Fusion, by, ignoreZones: true)))
        {
            var rp = pool == null && SupportsFusion(p, res) ? mats.Concat(P(p).Grave.Where(g => g.Data.IsMonster)).ToList() : mats;
            foreach (var r in Recipes(res))
                if (Completable(r, 0, rp, false, FreeMonsterZones(p) == 0)) { yield return (res, r); break; }
        }
    }

    bool SupportsFusion(int p, Card res) => TurnFlags.Contains($"fusionsupport{p}") && res.Data.Race == "Machine";

    IEnumerable<Card> MaterialPool(int p, bool fieldOnly) => fieldOnly ? P(p).MonstersOnField : P(p).Hand.Where(h => h.Data.IsMonster).Concat(P(p).MonstersOnField)
        .Concat(TurnFlags.Contains($"chainmaterial{p}") ? ChainMaterialPool(p) : Enumerable.Empty<Card>()).Distinct();
    IEnumerable<Card> ChainMaterialPool(int p) => P(p).MonstersOnField.Concat(P(p).Deck.Concat(P(p).Hand).Concat(P(p).Grave).Where(c => c.Data.IsMonster));

    public Card? LastFusion;   // the latest Fusion Summoned monster (Dark Fusion protects it)

    public static readonly string[] Substitutes = { "Goddess with the Third Eye", "Beastking of the Swamps", "Versago the Destroyer", "Mystical Sheep #1" };

    /// A Fusion Monster's recipes: its own Materials (official cards), else the PoC fusion table.
    public IEnumerable<List<Mat>> Recipes(Card res)
    {
        if (S(res)?.MatRecipes is { } gen) { foreach (var r in gen(this, res)) yield return r; yield break; }   // Pair Cycroid
        if (S(res)?.Materials is { } own) { yield return own; yield break; }
        foreach (var f in Db.Fusions.Where(f => MatchesKonami(res, f.Result)))
        {
            // Blue-Eyes Ultimate Dragon is listed as BEWD + BEWD; the exe checks for a third copy in code.
            var ids = f.A == f.B && res.Name == "Blue-Eyes Ultimate Dragon" ? new[] { f.A, f.A, f.A } : new[] { f.A, f.B };
            yield return ids.Select(id => { var n = Db.ByKonami.TryGetValue(id, out var l) ? l[0].Name : "?"; return new Mat(n, c => MatchesKonami(c, id), n); }).ToList();
        }
    }

    public bool IsSubstitute(Card m) => Substitutes.Contains(m.Name) || S(m)?.Substitute == true;
    bool FitsMaterial(Card m, Mat need, bool subUsed) => need.Ok(m) || (!subUsed && need.Name != null && IsSubstitute(m));

    /// Can needs[i..] be met from `avail`, each by a match or (once) a substitute? `needField`: with every Monster Zone
    /// full, at least one material must come from the field so the Fusion Monster has a zone to land in.
    bool Completable(List<Mat> needs, int i, List<Card> avail, bool subUsed, bool needField = false)
    {
        if (i == needs.Count) return !needField;
        foreach (var m in avail)
            if (FitsMaterial(m, needs[i], subUsed) && Completable(needs, i + 1, avail.Where(x => x != m).ToList(), subUsed || !needs[i].Ok(m), needField && m.Loc != Loc.Monster))
                return true;
        return false;
    }

    /// Fusion Summon. Materials go to the GY (or banished / to `matsTo`, e.g. shuffled into the Deck for Contact Fusion).
    public async Flow<bool> FusionSummon(int p, Card? by, bool fieldOnly = false, bool banishMats = false, IEnumerable<Card>? pool = null,
        Func<Card, bool>? resultOk = null, Loc? matsTo = null)
    {
        var opts = FusionOptions(p, fieldOnly, pool, resultOk, by).ToList();
        Scripts.FusingPlayer = p;
        if (opts.Count == 0) return false;
        var res = await PickOne(p, opts.Select(o => o.result), "Fusion Summon which monster?", by);
        if (res == null) return false;
        var needs = opts.First(o => o.result == res).mats;
        var me = P(p);
        var avail = (pool ?? MaterialPool(p, fieldOnly)).Where(m => S(m)?.NotMaterial != true).ToList();
        // Chain Material: this turn the materials may come from the field, Deck, hand or GY (banished; the Fusion is destroyed at the End Phase).
        bool chainMat = pool == null && !fieldOnly && TurnFlags.Contains($"chainmaterial{p}") && !Completable(needs, 0, avail, false, FreeMonsterZones(p) == 0)
            || (pool == null && !fieldOnly && TurnFlags.Contains($"chainmaterial{p}") && await YesNo(p, "Use Chain Material (banish materials from your field, Deck, hand or GY)?", by));
        if (chainMat) { avail = ChainMaterialPool(p).ToList(); matsTo = Loc.Banished; }
        // Cybernetic Fusion Support: once this turn, a Machine Fusion Monster's materials may also come from the GY (all banished).
        else if (pool == null && SupportsFusion(p, res) && (!Completable(needs, 0, avail, false, FreeMonsterZones(p) == 0) || await YesNo(p, "Use Cybernetic Fusion Support (banish materials from your hand, field and/or GY)?", by)))
        { avail = avail.Concat(P(p).Grave.Where(g => g.Data.IsMonster)).ToList(); matsTo = Loc.Banished; TurnFlags.Remove($"fusionsupport{p}"); }
        var chosen = new List<Card>();
        bool subUsed = false;
        for (int i = 0; i < needs.Count; i++)
        {
            var n = needs[i];
            bool needField = FreeMonsterZones(p) == 0 && !chosen.Any(x => x.Loc == Loc.Monster);
            var cands = avail.Where(m => !chosen.Contains(m) && FitsMaterial(m, n, subUsed)
                && Completable(needs, i + 1, avail.Where(x => !chosen.Contains(x) && x != m).ToList(), subUsed || !n.Ok(m), needField && m.Loc != Loc.Monster)).ToList();
            var m = await PickOne(p, cands, $"Fusion Material: {n.Label}", by);
            if (m == null) return false;
            if (!n.Ok(m)) subUsed = true;
            chosen.Add(m);
            // "1+": as many more as you like.
            while (n.More && avail.Any(o => !chosen.Contains(o) && n.Ok(o)))
            {
                var more = await PickOne(p, avail.Where(o => !chosen.Contains(o) && n.Ok(o)), $"More Fusion Material ({n.Label})? Pick none to stop", by, optional: true);
                if (more == null) break;
                chosen.Add(more);
            }
        }
        foreach (var m in chosen)
        {
            var to = matsTo ?? (banishMats ? Loc.Banished : Loc.Grave);
            if (to == Loc.Deck) { await MoveTo(m, Loc.Deck); Shuffle(m.Owner); }
            else if (to == Loc.Banished) await Banish(m); else await MoveTo(m, Loc.Grave);
            if (m.Loc == Loc.Grave) m.Vars["fusionmat"] = 1;
        }
        await SpecialSummon(res, p, await ChoosePosition(p, res), kind: SummonKind.Fusion, by: by);
        res.Vars["matcount"] = chosen.Count;
        LastFusion = res;
        if (chainMat && res.Loc == Loc.Monster) Later(Turn, Phase.End, async () => { if (res.Loc == Loc.Monster) await Destroy(res); });
        for (int i = 0; i < chosen.Count; i++) res.Vars[$"mat{i}"] = chosen[i].Uid + 1;   // De-Fusion brings back what was used (3 for Ultimate, substitutes too)
        return true;
    }

}
