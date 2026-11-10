namespace PoC.Engine;

// Battle Phase: attack declaration, response window, damage step.
public sealed partial class Duel
{
    async Flow BattlePhase(int round = 0)
    {
        int tp = TurnPlayer;
        TurnFlags.Add($"bp{tp}");   // this turn's Battle Phase was conducted
        await Raise(new Ev { Kind = EvKind.BattleStart, Player = tp, Arg = round });   // round 1: Weather Report's second Battle Phase
        while (Phase == Phase.Battle && !TurnFlags.Contains("endbattle") && !TurnFlags.Contains("toend"))
        {
            var opts = BattleOptions(tp);
            var o = await Choose(tp, Ask.Battle, opts, "Battle Phase");
            if (o.Act == Act.ToMain2) break;
            if (o.Act == Act.Attack) await DoAttack(o.Card!, o.Target);
            else if (o.Act == Act.Activate) await ActivateChain(o.Card!, tp, FxOf(o));
            // Battles an effect set up (Super Junior Confrontation): no attack declaration, they just happen.
            while (ForcedBattles.Count > 0 && Phase == Phase.Battle)
            {
                var (fa, ft) = ForcedBattles[0]; ForcedBattles.RemoveAt(0);
                if (fa.Loc == Loc.Monster && ft.Loc == Loc.Monster) await DoAttack(fa, ft, declared: false);
            }
        }
        TurnFlags.Remove("endbattle");
        await Raise(new Ev { Kind = EvKind.BattleEnd, Player = tp });
    }

    public bool CanAttack(Card a)
    {
        if (a.Loc != Loc.Monster || !a.FaceUp || a.Controller != TurnPlayer) return false;
        if (Turn == 1) return false;   // the player who goes first can't attack on their first turn
        if (a.Defense && !Allowed(Q.AttackFromDefense, a)) return false;
        int max = Allowed(Q.DoubleAttack, a) ? 2 : 1;
        if (a.Var("maxatkturn") == Turn) max = Math.Max(max, a.Var("maxatk"));   // Feather Shot, Hero Heart
        if (S(a)?.ExtraAttacks is { } more && !Negated(a)) max += more(X(a));
        if (a.Var("twice") == 1) max = Math.Max(max, 2);   // Evil HERO Infernal Gainer
        if (AttacksEach(a)) max = Math.Max(max, a.Attacks + (AttackTargets(a).Any() ? 1 : 0));
        if (a.Attacks >= max) return false;
        if (a.Var("noattack") >= Turn) return false;
        if (CountActive("The Dark Door") > 0 && P(a.Controller).MonstersOnField.Any(m => m.Attacks > 0)) return false;
        return !Forbidden(Q.Attack, a);
    }

    public List<Opt> BattleOptions(int tp)
    {
        var o = new List<Opt>();
        var opp = P(1 - tp);
        var must = MustAttack(tp);
        foreach (var a in P(tp).MonstersOnField.ToList())
        {
            if (!CanAttack(a)) continue;
            foreach (var t in AttackTargets(a)) o.Add(new Opt(Act.Attack, a, t));
            if (CanAttackDirectly(a) && must == null) o.Add(new Opt(Act.Attack, a, null));
        }
        foreach (var c in P(tp).Hand.Concat(P(tp).SpellsOnField).Concat(P(tp).MonstersOnField).ToList())
            if (CanActivate(c, tp)) o.Add(new Opt(Act.Activate, c));
        o.AddRange(FxOptions(tp));
        // Staunch Defender: every face-up monster that can attack the chosen one must do so before the Battle Phase ends.
        // Amazoness Archers: monsters that must attack this turn, if able.
        bool owed = o.Any(x => x.Act == Act.Attack && x.Card!.Var("mustattack") == Turn);
        if ((must == null || !o.Any(x => x.Act == Act.Attack)) && !owed) o.Add(new Opt(Act.ToMain2));
        return o;
    }

    /// Staunch Defender's chosen monster (this turn, still face-up on the field), the only one tp's monsters may attack.
    Card? MustAttack(int tp) => Flags.GetValueOrDefault($"staunch{tp}") is > 0 and var u && Flags.GetValueOrDefault($"staunchturn{tp}") == Turn
        && cards[u - 1] is { Loc: Loc.Monster, FaceUp: true } c && c.Controller != tp ? c : null;

    /// Can attack each of the opponent's monsters once (Wildedge; Amazoness Call for a turn).
    bool AttacksEach(Card a) => (S(a)?.AttackEach == true && !Negated(a)) || a.Var("attackeach") == Turn;

    public readonly List<(Card a, Card t)> ForcedBattles = new();
    public bool TargetWasSet;   // the attack target was face-down when the attack was declared (8-Claws Scorpion)
    readonly List<Action> afterBattle = new();
    /// Undo at the end of this battle's Damage Step ("until the end of the Damage Step").
    public void UntilBattleEnds(Action undo) => afterBattle.Add(undo);

    public IEnumerable<Card> AttackTargets(Card a)
    {
        Querying = a;
        var mons = P(1 - a.Controller).MonstersOnField.Where(t => !Forbidden(Q.BeAttacked, t)).ToList();
        Querying = null;
        if (AttacksEach(a)) mons = mons.Where(t => a.Var($"hit{t.Uid}") != Turn).ToList();
        if (MustAttack(a.Controller) is { } forced) return mons.Contains(forced) ? new[] { forced } : Array.Empty<Card>();
        var must = mons.Where(t => Allowed(Q.MustBeAttacked, t)).ToList();
        return must.Count > 0 ? must : mons;
    }

    public bool CanAttackDirectly(Card a)
    {
        if (Forbidden(Q.DirectAttack, a) || a.Var("nodirect") == Turn) return false;
        if (AttacksEach(a) && a.Attacks > 0) return false;   // Wildedge: once it attacked a monster, no direct attack
        return !AttackTargets(a).Any() || Allowed(Q.DirectAttack, a) || a.Var("direct") == Turn;   // no monster it may attack (Legendary Fisherman alone with Umi): direct
    }

    public int BattleAtk(Card c) => Atk(c);   // Total Defense Shogun attacks from Defense with its ATK (card text)

    /// ATK during damage calculation: continuous "during damage calculation only" changes (Skyscraper, Rampart Blaster).
    public int CalcAtk(Card c, Card? vs, int atk)
    {
        foreach (var (sc, s) in Sources().ToList()) if (s.CalcAtk != null) atk = s.CalcAtk(X(sc), c, vs, atk);
        return Math.Max(0, atk);
    }

    public int CalcDef(Card c, Card? vs, int def)
    {
        foreach (var (sc, s) in Sources().ToList()) if (s.CalcDef != null) def = s.CalcDef(X(sc), c, vs, def);
        return Math.Max(0, def);
    }

    /// An equip that takes the battle destruction instead of `c` (Bubble Blaster).
    Card? ShieldOf(Card c) => EquipsOf(c).FirstOrDefault(e => e.FaceUp && !Negated(e) && S(e)?.BattleShield?.Invoke(X(e), c) == true);

    async Flow DoAttack(Card a, Card? t, bool declared = true)
    {
        int tp = a.Controller;
        Attacker = a; AttackTarget = t; AttackNegated = false; Flags.Remove("lastbdmg0"); Flags.Remove("lastbdmg1");
        if (declared) a.Attacks++;
        if (t != null) a.Vars[$"hit{t.Uid}"] = Turn;
        TargetWasSet = t != null && !t.FaceUp;
        a.PositionChangedThisTurn = true;   // a monster that attacked can't change its battle position this turn
        Card? flipped = null;
        try
        {
            if (declared) await Raise(new Ev { Kind = EvKind.AttackDeclared, Card = a, Other = t, Player = tp });
            if (!BattleValid(ref a, ref t)) return;

            // Damage step. A face-down defender is turned face-up; its flip effect resolves after damage
            // calculation, even if it was destroyed (recorded PoC duels: Man-Eater Bug, Hane-Hane).
            await Raise(new Ev { Kind = EvKind.DamageStart, Card = a, Other = t, Player = tp });   // "at the start of the Damage Step" (Mystic Swordsman LV2)
            if (!BattleValid(ref a, ref t)) return;
            if (t != null && !t.FaceUp)
            {
                await Flip(t, triggerFlip: false);
                flipped = t;
                if (!BattleValid(ref a, ref t)) return;
            }
            await Raise(new Ev { Kind = EvKind.DamageCalc, Card = a, Other = t, Player = tp });
            if (!BattleValid(ref a, ref t)) return;
            if (BattleFlags.Contains("nodamagecalc")) return;

            // Damage goes to each monster's controller (Mirror Gate swaps them mid-battle).
            int ca = a.Controller, ct = t?.Controller ?? 1 - ca;
            int atkA = CalcAtk(a, t, BattleAtk(a));
            Card? loser = null, loser2 = null;
            int dmgTo = -1, dmg = 0;
            if (t == null) { dmgTo = ct; dmg = atkA; }
            else if (!t.Defense)
            {
                int atkT = CalcAtk(t, a, Atk(t));
                if (atkA > atkT) { loser = t; dmgTo = ct; dmg = atkA - atkT; }
                else if (atkA < atkT) { loser = a; dmgTo = ca; dmg = atkT - atkA; }
                else if (atkA > 0) { loser = t; loser2 = a; }
            }
            else
            {
                int defT = CalcDef(t, a, Def(t));
                if (atkA > defT) { loser = t; if (Allowed(Q.Piercing, a) || a.Var("pierce") == Turn) { dmgTo = ct; dmg = atkA - defT; } }
                else if (atkA < defT) { dmgTo = ca; dmg = defT - atkA; }
            }
            // Hero Spirit: the shielded player's first battle this turn against an opponent's monster: no battle damage,
            // and the shielded player's monster isn't destroyed by it.
            int shielded = t == null ? (Flags.GetValueOrDefault($"battleshield{ct}") == Turn ? ct : -1)
                : new[] { ca, ct }.FirstOrDefault(p => Flags.GetValueOrDefault($"battleshield{p}") == Turn, -1);
            if (shielded >= 0)
            {
                Flags.Remove($"battleshield{shielded}");
                dmgTo = -1;
                if (loser2?.Controller == shielded) loser2 = null;
                if (loser?.Controller == shielded) (loser, loser2) = (loser2, null);
            }
            // Amazoness Swords Woman: the battle damage its controller would take goes to the opponent instead.
            if (dmgTo >= 0 && new[] { a, t }.Any(m => m != null && m.Controller == dmgTo && m.FaceUp && m.Loc == Loc.Monster && ((S(m)?.ReflectBattleDamage == true && !Negated(m)) || m.Var("reflectdmg") == 1))) dmgTo = 1 - dmgTo;
            // An equip destroyed instead of the loser (Bubble Blaster) may also stop its controller's battle damage.
            foreach (var l in new[] { loser, loser2 })
                if (l != null && ShieldOf(l) is { } sh && S(sh)!.ShieldNoDamage && l.Controller == dmgTo) dmgTo = -1;

            if (t != null && t.Var("hat") == 1) dmgTo = -1;
            if (dmgTo >= 0 && dmg > 0)
            {
                bool zero = TurnFlags.Contains($"nobattledamage{dmgTo}") || BattleFlags.Contains($"nodamage{dmgTo}")
                    || (dmgTo == ca && (Allowed(Q.NoBattleDamageToController, a) || a.Var("nodmg") == 1)) || (t != null && dmgTo == ct && t.Var("nodmg") == 1)
                    || (dmgTo == ca && Allowed(Q.NoBattleDamageToController, a))
                    || (t != null && dmgTo == ct && Allowed(Q.NoBattleDamageToController, t));
                if (!zero)
                {
                    var src = dmgTo == ct ? a : t!;
                    foreach (var (sc, s) in Sources().ToList()) if (s.BattleDamage != null) dmg = Math.Max(0, s.BattleDamage(X(sc), src, dmgTo, dmg));
                    if (BattleFlags.Contains($"double{dmgTo}")) dmg *= 2;   // Cross Counter
                    if (src.Var("fixeddmgturn") == Turn && (t == null || src.Var("fixeddmg") == 0)) dmg = src.Var("fixeddmg");   // Dark Scorpion Combination: 400 on direct attacks; Union Attack: 0 on every battle
                    if (src.Var("halfdirect") == Turn && t == null) dmg /= 2;   // Pegasus Wing
                    int before = P(dmgTo).Lp;
                    await Damage(dmgTo, dmg, src, battle: true);
                    int dealt = before - P(dmgTo).Lp;
                    Flags[$"lastbdmg{dmgTo}"] = dealt; Flags[$"lastbdmgturn{dmgTo}"] = Turn;   // Sinister Seeds
                    if (dealt > 0) await Raise(new Ev { Kind = EvKind.BattleDamage, Card = src, Player = dmgTo, Amount = dealt, Other = src == a ? t : a });
                }
            }

            await Raise(new Ev { Kind = EvKind.AfterCalc, Card = a, Other = t, Player = tp });   // D.D. Warrior Lady
            var dead = new List<Card>();
            foreach (var l in new[] { loser, loser2 })
                if (l != null && l.Loc == Loc.Monster && !TurnFlags.Contains($"nobattledestroy{l.Controller}") && !Forbidden(Q.DestroyByBattle, l))
                {
                    if (ShieldOf(l) is { } sh) { await Destroy(sh, l); continue; }
                    dead.Add(l);
                }
            foreach (var d in dead)
            {
                var winner = d == a ? t : a;
                Flags[$"lastdef{d.Uid}"] = d.Defense ? 1 : 0;
                var owner = d.Owner;
                await Destroy(new[] { d }, winner, battle: true);
                Flags[$"battledead{owner}_{d.Uid}"] = Turn;
                if (winner != null) await Raise(new Ev { Kind = EvKind.BattleDestroyedMonster, Card = winner, Other = d, Player = winner.Controller, Battle = true });
            }
            await Raise(new Ev { Kind = EvKind.AfterDamage, Card = a, Other = t, Player = tp });
        }
        finally
        {
            Attacker = null; AttackTarget = null; BattleFlags.Clear();
            foreach (var u in afterBattle.ToList()) u();
            afterBattle.Clear();
        }
        if (flipped != null) await Flipped(flipped);
    }

    /// The attack continues only while both monsters are still where they were.
    bool BattleValid(ref Card a, ref Card? t)
    {
        if (Attacker != null && Attacker != a) { a = Attacker; a.Attacks++; } // Dark Spirit of the Silent
        if (AttackNegated || a.Loc != Loc.Monster || (a.Controller != TurnPlayer && !BattleFlags.Contains("swapped")) || !a.FaceUp) return false;
        // A lock put on the attacker mid-attack (Spellbinding Circle on the attack) stops this attack too, as the PoC CPU plays it.
        int lock_ = a.Uid + 1;
        if (FieldCards.Any(f => f.FaceUp && f.Var("target") == lock_) && Forbidden(Q.Attack, a)) return false;
        if (AttackTarget != t) t = AttackTarget; // redirected (Shift, Magic-Arm Shield, Ancient Lamp...)
        if (t == null) return CanAttackDirectly(a) || Flags.GetValueOrDefault("forceddirect") > 0;
        return t.Loc == Loc.Monster && (t.Controller != a.Controller || BattleFlags.Contains("redirectself"));
    }

    public List<Card> BattleDestroyedThisTurn(int owner) =>
        P(owner).Grave.Where(c => Flags.GetValueOrDefault($"battledead{owner}_{c.Uid}") == Turn).ToList();
}
