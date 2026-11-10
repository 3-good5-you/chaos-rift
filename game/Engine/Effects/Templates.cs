namespace PoC.Engine;

// Layer 2: common effect patterns, parameterised.
public static partial class Scripts
{
    // ---- pattern builders (also used by the hand-written layer)

    internal static Script Spell(string name, Func<Ctx, Flow> run, Func<Ctx, bool>? can = null, Func<Ctx, Flow>? cost = null)
    {
        var s = S(name);
        s.Run = run; s.Can = can; s.Cost = cost;
        return s;
    }

    internal static Script Burn(string name, int n, int self = 0) =>
        Spell(name, async x => { await x.D.Damage(x.Opp, n, x.Self); if (self > 0) await x.D.Damage(x.Me, self, x.Self); });

    internal static Script Heal(string name, int n) => Spell(name, async x => await x.D.Gain(x.Me, n, x.Self));

    /// PoC quirk: these heal "a player of your choice"; modern text heals you.
    internal static Script HealAny(string name, int n) => Spell(name, async x =>
    {
        int who = x.Me;
        if (x.Classic && await x.D.ChooseText(x.Me, $"{name}: who gains {n} LP?", new[] { "You", "Opponent" }, x.Self) == 1) who = x.Opp;
        await x.D.Gain(who, n, x.Self);
    });

    internal static Script Draw(string name, int n) => Spell(name, async x => await x.D.Draw(x.Me, n));

    /// Trap that destroys the monster the opponent just summoned, when `ok` holds.
    internal static Script SummonTrap(string name, Func<Ctx, Card, bool> ok, bool special = false)
    {
        var s = S(name);
        s.Can = x => x.Ev is { Kind: EvKind.Summoned } e && e.Player == x.Opp && e.Card!.Valid() && e.Card.FaceUp
            && (special || e.IsNormalOrFlip()) && ok(x, e.Card);
        s.Run = async x => { if (x.Ev!.Card!.Valid()) await x.D.Destroy(x.Ev.Card, x.Self); };
        return s;
    }

    internal static bool AttackOnMe(Ctx x) => x.Ev is { Kind: EvKind.AttackDeclared } e && e.Player == x.Opp && e.Card!.Valid();

    /// Flip effect monster.
    internal static Script Flip(string name, Func<Ctx, Flow> run)
    {
        var s = S(name);
        s.On = (x, e) => e.Is(EvKind.Flipped, x.Self) ? run(x) : Flow.Done;
        return s;
    }

    /// "When sent to the Graveyard by battle" monsters.
    internal static Script OnBattleDeath(string name, Func<Ctx, Flow> run)
    {
        var s = S(name);
        s.On = (x, e) => e.Is(EvKind.Destroyed, x.Self) && e.Battle && x.Self.Loc == Loc.Grave ? run(x) : Flow.Done;
        return s;
    }

    internal static Script OnSummon(string name, Func<Ctx, Flow> run, bool normalOnly = false, bool includeFlip = false)
    {
        var s = S(name);
        s.On = (x, e) => e.Is(EvKind.Summoned, x.Self)
            && (!normalOnly || e.Summoned(SummonKind.Normal, SummonKind.Tribute) || (includeFlip && e.Summoned(SummonKind.Flip))) ? run(x) : Flow.Done;
        return s;
    }

    internal static Script OnBattleDamage(string name, Func<Ctx, Ev, Flow> run)
    {
        var s = S(name);
        s.On = (x, e) => e.Kind == EvKind.BattleDamage && e.Card == x.Self && e.Player == x.Opp ? run(x, e) : Flow.Done;
        return s;
    }

    internal static Script MyStandby(string name, Func<Ctx, Flow> run, Func<Ctx, bool>? where = null)
    {
        var s = S(name);
        s.On = (x, e) => e.Kind == EvKind.Standby && e.Player == x.Me && (where ?? (x => x.OnFieldUp()))(x) ? run(x) : Flow.Done;
        return s;
    }

    internal static Script DirectAttacker(string name)
    {
        var s = S(name);
        s.Allow = (x, q, c) => q == Q.DirectAttack && c == x.Self;
        return s;
    }

    /// Self ATK bonus from a count.
    internal static Script SelfBoost(string name, Func<Ctx, int> atk, Func<Ctx, int>? def = null)
    {
        var s = S(name);
        s.Stats = (x, c, st) => { if (c == x.Self) st.Add(atk(x), def?.Invoke(x) ?? 0); };
        return s;
    }

    /// Recruiters: when destroyed by battle, Special Summon a monster with <= 1500 ATK of an attribute.
    internal static Script Recruiter(string name, string attr, bool faceUpAttack)
    {
        return OnBattleDeath(name, x => x.Recruit(c => c.Data.Attribute == attr && c.Data.Atk <= 1500,
            $"{name}: Special Summon a {attr} monster with 1500 or less ATK", faceUpAttack ? false : null));
    }

    static void Templates(CardDb db)
    {
        Burn("Sparks", 200); Burn("Hinotama", 500); Burn("Final Flame", 600); Burn("Ookazi", 800);
        Burn("Tremendous Fire", 1000, 500); Burn("Raimei", 300);
        Spell("Restructer Revolution", async x => await x.D.Damage(x.Opp, 200 * x.Their.Hand.Count, x.Self));
        HealAny("Mooyan Curry", 200); HealAny("Goblin's Secret Remedy", 600); HealAny("Soul of the Pure", 800); HealAny("Blue Medicine", 400);
        Heal("Red Medicine", 500); Heal("Dian Keto the Cure Master", 1000);
        Spell("Rain of Mercy", async x => { await x.D.Gain(x.Me, 1000, x.Self); await x.D.Gain(x.Opp, 1000, x.Self); });
        Draw("Pot of Greed", 2); Draw("Jar of Greed", 1);
        Flip("Skelengel", x => x.D.Draw(x.Me, 1));

        SummonTrap("Trap Hole", (x, c) => x.Atk(c) >= 1000);
        SummonTrap("House of Adhesive Tape", (x, c) => x.Def(c) <= 500);
        SummonTrap("Eatgaboon", (x, c) => x.Atk(c) <= 500);

        foreach (var n in new[] { "Leghul", "Ooguchi", "Jinzo #7", "Rainbow Flower", "Queen's Double", "Mystic Lamp" }) DirectAttacker(n);

        Recruiter("Giant Rat", "EARTH", false);
        Recruiter("UFO Turtle", "FIRE", true);
        Recruiter("Shining Angel", "LIGHT", true);
        Recruiter("Mother Grizzly", "WATER", true);
        Recruiter("Flying Kamakiri #1", "WIND", true);
        Recruiter("Mystic Tomato", "DARK", false);
    }
}
