using PoC.Engine;
using Sc = PoC.Check.Scenarios.Sc;

namespace PoC.Check;

/// `labtest [filter]`: card-lab cards checked against their official text and rulings on stacked decks
/// (what the user would otherwise click through in the card lab). A pass = the card behaves as its text says
/// in the tested cases, not every interaction.
static class LabTests
{
    const string Neo = "Neo the Magic Swordsman";   // 1700/1000 Level 4 vanilla
    const string Elf = "Mystical Elf";              // 800/2000, the Sc filler

    static Card Put(Sc s, int p, string name, bool set = false)
    {
        var pl = s.D.P(p);
        var c = pl.Deck.Concat(pl.Hand).Concat(pl.Extra).First(x => x.Name == name);
        s.D.Place(c, p, Loc.Monster);
        c.FaceUp = !set; c.Defense = set;
        return c;
    }

    static Card Gy(Sc s, int p, string name)
    {
        var pl = s.D.P(p);
        var c = pl.Deck.Concat(pl.Hand).First(x => x.Name == name);
        pl.Deck.Remove(c); pl.Hand.Remove(c); c.Loc = Loc.Grave; pl.Grave.Add(c);
        return c;
    }

    /// Pass response windows until one offers `card`, then activate it.
    static Sc Until(Sc s, string card)
    {
        while (s.Has(Ask.Respond) && !s.D.Pending!.Options.Any(o => o.Card?.Name == card)) s.Pass();
        return s.Respond(card);
    }

    static bool Offers(Sc s, string card) => s.D.Pending?.Options.Any(o => o.Card?.Name == card) == true;

    public static int Run(string filter)
    {
        int ok = 0, total = 0, fails = 0;
        void Check(bool good, string what) { if (!good) { fails++; Console.WriteLine("  FAIL " + what); } }
        void T(string card, Action<Action<bool, string>> a)
        {
            if (!card.Contains(filter, StringComparison.OrdinalIgnoreCase)) return;
            total++;
            int before = fails;
            try { a(Check); }
            catch (Exception e) { fails++; Console.WriteLine($"  FAIL {card}: {e.InnerException?.Message ?? e.Message}"); }
            if (fails == before) ok++;
            Console.WriteLine($"{(fails == before ? "PASS" : "FAIL")} {card}");
        }
        string[] none = Array.Empty<string>();

        T("Bottomless Trap Hole", c =>
        {
            var s = new Sc(new[] { Neo }, new[] { "Bottomless Trap Hole" }, classic: false);
            s.EndTurn().Do(Act.SetSpell, "Bottomless Trap Hole").EndTurn();
            s.Do(Act.Summon, Neo);
            Until(s, "Bottomless Trap Hole").PassAll();
            c(s.D.P(0).Banished.Any(x => x.Name == Neo), "1700 ATK summon destroyed and banished");
            var s2 = new Sc(new[] { Elf }, new[] { "Bottomless Trap Hole" }, classic: false);
            s2.EndTurn().Do(Act.SetSpell, "Bottomless Trap Hole").EndTurn().Do(Act.Summon, Elf);
            bool offered = false;
            while (s2.Has(Ask.Respond)) { offered |= Offers(s2, "Bottomless Trap Hole"); s2.Pass(); }
            c(!offered, "not offered on an 800 ATK summon");
        });

        T("Treeborn Frog", c =>
        {
            var frog = new Sc(new[] { "Treeborn Frog" }, none, classic: false);
            Gy(frog, 0, "Treeborn Frog");
            frog.EndTurn().EndTurn();
            if (frog.Has(Ask.YesNo)) frog.Yes();
            frog.PassAll().AttackPos().PassAll();
            c(frog.D.P(0).MonstersOnField.Any(x => x.Name == "Treeborn Frog"), "revives in the Standby Phase with no Spells/Traps");

            var f2 = new Sc(new[] { "Treeborn Frog", "Mystical Space Typhoon" }, none, classic: false);
            Gy(f2, 0, "Treeborn Frog");
            f2.Do(Act.SetSpell, "Mystical Space Typhoon").EndTurn().EndTurn();
            c(!f2.Has(Ask.YesNo) && !f2.D.P(0).MonstersOnField.Any(), "no revival while you control a Set Spell");
            // A face-down Treeborn Frog you control doesn't stop it (script counts face-up only): ruling question, asked the user.
        });

        T("Marshmallon", c =>
        {
            var s = new Sc(new[] { Neo }, new[] { "Marshmallon" }, classic: false);
            s.EndTurn();
            Put(s, 1, "Marshmallon", set: true);
            s.EndTurn();
            s.Do(Act.Summon, Neo).PassAll().Do(Act.ToBattle).PassAll().Do(Act.Attack, Neo, "Marshmallon").PassAll();
            while (s.Has(Ask.YesNo)) s.Yes();
            s.PassAll();
            c(s.D.P(1).MonstersOnField.Any(x => x.Name == "Marshmallon"), "not destroyed by battle");
            c(s.D.P(0).Lp == 7000, $"attacker took 1000 for attacking it face-down (LP {s.D.P(0).Lp})");
            s.EndTurn().EndTurn().Do(Act.ToBattle).PassAll().Do(Act.Attack, Neo, "Marshmallon").PassAll();
            c(s.D.P(0).Lp == 7000, $"no 1000 when attacked face-up (LP {s.D.P(0).Lp})");
        });

        T("Exploder Dragon", c =>
        {
            var s = new Sc(new[] { "Exploder Dragon" }, new[] { Neo }, classic: false);
            Put(s, 0, "Exploder Dragon"); Put(s, 1, Neo);
            s.EndTurn().EndTurn().Do(Act.ToBattle).PassAll().Do(Act.Attack, "Exploder Dragon", Neo).PassAll();
            c(s.D.P(0).Lp == 8000, $"no battle damage to its controller when it attacks (LP {s.D.P(0).Lp})");
            c(s.D.P(1).Grave.Any(x => x.Name == Neo) && s.D.P(0).Grave.Any(x => x.Name == "Exploder Dragon"), "both destroyed");
            var s2 = new Sc(new[] { "Exploder Dragon" }, new[] { Neo }, classic: false);
            Put(s2, 0, "Exploder Dragon"); Put(s2, 1, Neo);
            s2.EndTurn().Do(Act.ToBattle).PassAll().Do(Act.Attack, Neo, "Exploder Dragon").PassAll();
            c(s2.D.P(0).Lp == 7300 && s2.D.P(1).Grave.Any(x => x.Name == Neo), $"attacked: damage taken normally, attacker destroyed (LP {s2.D.P(0).Lp})");
        });

        T("Card Trooper", c =>
        {
            var s = new Sc(new[] { "Card Trooper" }, new[] { Neo }, classic: false);
            var t = Put(s, 0, "Card Trooper"); var neo = Put(s, 1, Neo);
            s.EndTurn().EndTurn();
            int deck = s.D.P(0).Deck.Count;
            s.Do(Act.Activate, "Card Trooper").Opt("3").PassAll();
            c(s.D.Atk(t) == 1900 && s.D.P(0).Deck.Count == deck - 3, $"3 milled, ATK {s.D.Atk(t)}");
            s.EndTurn();
            c(s.D.Atk(t) == 400, $"back to 400 next turn (ATK {s.D.Atk(t)})");
            int hand = s.D.P(0).Hand.Count;
            s.Do(Act.ToBattle).PassAll().Do(Act.Attack, Neo, "Card Trooper").PassAll();
            c(s.D.P(0).Hand.Count == hand + 1, "destroyed: draws 1");
        });

        T("Dandylion", c =>
        {
            var s = new Sc(new[] { "Dandylion" }, new[] { Neo }, classic: false);
            Put(s, 0, "Dandylion"); Put(s, 1, Neo);
            s.EndTurn().Do(Act.ToBattle).PassAll().Do(Act.Attack, Neo, "Dandylion").PassAll();
            var toks = s.D.P(0).MonstersOnField.Where(x => x.Name == "Fluff Token").ToList();
            c(toks.Count == 2 && toks.All(x => x.Defense), $"2 Fluff Tokens in Defense ({toks.Count})");
        });

        T("Fiber Jar", c =>
        {
            var s = new Sc(new[] { "Fiber Jar" }, new[] { Neo }, classic: false, extra0: new[] { "Flame Swordsman" });
            Put(s, 0, "Flame Swordsman");
            s.Do(Act.Set, "Fiber Jar").EndTurn();
            s.Do(Act.Summon, Neo).PassAll().EndTurn();
            s.Do(Act.FlipSummon, "Fiber Jar").PassAll();
            while (s.Has(Ask.YesNo)) s.Yes();
            s.PassAll();
            var p0 = s.D.P(0); var p1 = s.D.P(1);
            c(p0.Hand.Count == 5 && p1.Hand.Count == 5, $"both draw 5 ({p0.Hand.Count}/{p1.Hand.Count})");
            c(!p0.FieldCards.Any() && !p1.FieldCards.Any() && p0.Grave.Count == 0 && p1.Grave.Count == 0, "field and GYs empty");
            c(p0.Extra.Any(x => x.Name == "Flame Swordsman"), "Fusion Monster goes back to the Fusion Deck");
            c(p0.Deck.Count + p0.Hand.Count == 40 && p1.Deck.Count + p1.Hand.Count == 40, $"Main Decks whole again ({p0.Deck.Count + p0.Hand.Count}/{p1.Deck.Count + p1.Hand.Count})");
        });

        T("Creature Swap", c =>
        {
            var s = new Sc(new[] { "Creature Swap", Neo }, none, classic: false);
            var mine = Put(s, 0, Neo); var theirs = Put(s, 1, Elf);
            s.EndTurn().EndTurn().Do(Act.Activate, "Creature Swap").PassAll();
            while (s.Has(Ask.Pick)) s.D.Submit(new Command(s.D.Pending!.Player, new[] { 0 }));
            s.PassAll();
            c(mine.Controller == 1 && theirs.Controller == 0, "control switched");
            c(!s.D.Pending!.Options.Any(o => o.Act == Act.ChangePos && o.Card == theirs), "can't change position this turn");
        });

        T("Brain Control", c =>
        {
            var s = new Sc(new[] { "Brain Control" }, new[] { Neo }, classic: false, extra0: new[] { "Flame Swordsman" });
            var neo = Put(s, 1, Neo);
            s.EndTurn().EndTurn().Do(Act.Activate, "Brain Control").Pick(Neo).PassAll();
            c(neo.Controller == 0 && s.D.P(0).Lp == 7200, $"control taken for 800 LP (LP {s.D.P(0).Lp})");
            s.EndTurn();
            c(neo.Controller == 1, "returned in the End Phase");
            var s2 = new Sc(new[] { "Brain Control" }, none, classic: false, extra0: new[] { "Flame Swordsman" });
            var fs = Put(s2, 0, "Flame Swordsman"); s2.D.Place(fs, 1, Loc.Monster);
            c(!s2.D.MainOptions(0, true).Any(o => o.Act == Act.Activate && o.Card?.Name == "Brain Control"), "can't target a Fusion Monster");
        });

        T("Dimensional Fissure", c =>
        {
            var s = new Sc(new[] { "Dimensional Fissure" }, new[] { Neo }, classic: false);
            s.Do(Act.Activate, "Dimensional Fissure").PassAll();
            Put(s, 0, Elf); Put(s, 1, Neo);
            s.EndTurn();
            s.Do(Act.ToBattle).PassAll().Do(Act.Attack, Neo, Elf).PassAll();
            c(s.D.P(0).Banished.Any(x => x.Name == Elf) && !s.D.P(0).Grave.Any(x => x.Name == Elf), "destroyed monster banished instead");
        });

        T("Electromagnetic Turtle", c =>
        {
            var turtle = new Sc(new[] { "Electromagnetic Turtle" }, new[] { Neo }, classic: false);
            Gy(turtle, 0, "Electromagnetic Turtle");
            Put(turtle, 1, Neo);
            turtle.EndTurn();
            turtle.Do(Act.ToBattle).PassAll().Do(Act.Attack, Neo, "direct");
            Until(turtle, "Electromagnetic Turtle").PassAll();
            c(turtle.D.P(0).Lp == 8000 && turtle.D.P(0).Banished.Any(x => x.Name == "Electromagnetic Turtle"), $"attack stopped, Turtle banished (LP {turtle.D.P(0).Lp})");
            c(turtle.D.Phase != Phase.Battle, $"Battle Phase over (phase {turtle.D.Phase})");
        });

        T("Ojama Trio", c =>
        {
            var s = new Sc(new[] { "Ojama Trio", Neo }, none, classic: false);
            Put(s, 0, Neo);
            s.Do(Act.SetSpell, "Ojama Trio").EndTurn().EndTurn().Do(Act.Activate, "Ojama Trio").PassAll();
            var toks = s.D.P(1).MonstersOnField.Where(x => x.Name == "Ojama Token").ToList();
            c(toks.Count == 3 && toks.All(t => t.Defense), $"3 Ojama Tokens in Defense on the opponent's field ({toks.Count})");
            s.Do(Act.ToBattle).PassAll().Do(Act.Attack, Neo, "Ojama Token").PassAll();
            c(s.D.P(1).Lp == 7700, $"destroyed token: its controller takes 300 (LP {s.D.P(1).Lp})");
        });

        T("Granmarg the Rock Monarch", c =>
        {
            var s = new Sc(new[] { "Granmarg the Rock Monarch" }, new[] { "Mystical Space Typhoon" }, classic: false);
            Put(s, 0, Elf);
            s.EndTurn().Do(Act.SetSpell, "Mystical Space Typhoon").EndTurn();
            s.Do(Act.Summon, "Granmarg the Rock Monarch").Pick(Elf);
            while (!s.D.P(1).Grave.Any(x => x.Name == "Mystical Space Typhoon") && (s.Has(Ask.Pick) || s.Has(Ask.Respond) || s.Has(Ask.Position) || s.Has(Ask.YesNo)))
            {
                if (s.Has(Ask.Pick) && s.D.Pending!.Options.Any(o => o.Card?.Name == "Mystical Space Typhoon")) s.Pick("Mystical Space Typhoon");
                else if (s.Has(Ask.Position)) s.AttackPos();
                else if (s.Has(Ask.YesNo)) s.Yes();
                else s.Pass();
            }
            c(s.D.P(1).Grave.Any(x => x.Name == "Mystical Space Typhoon"), "Tribute Summon destroys the Set card");
        });

        T("Breaker the Magical Warrior", c =>
        {
            var s = new Sc(new[] { "Breaker the Magical Warrior" }, new[] { "Mystical Space Typhoon" }, classic: false);
            s.EndTurn().Do(Act.SetSpell, "Mystical Space Typhoon").EndTurn();
            s.Do(Act.Summon, "Breaker the Magical Warrior").PassAll();
            var b = s.Mon(0, "Breaker the Magical Warrior")!;
            c(s.D.Atk(b) == 1900, $"Normal Summoned: counter, 1900 ATK ({s.D.Atk(b)})");
            s.Do(Act.Activate, "Breaker the Magical Warrior").Pick("Mystical Space Typhoon").PassAll();
            c(s.D.Atk(b) == 1600 && s.D.P(1).Grave.Any(x => x.Name == "Mystical Space Typhoon"), $"counter removed, Set card destroyed ({s.D.Atk(b)})");
        });

        T("Emergency Provisions", c =>
        {
            var s = new Sc(new[] { "Emergency Provisions", "Mystical Space Typhoon", "Mystical Space Typhoon" }, none, classic: false);
            s.Do(Act.SetSpell, "Mystical Space Typhoon").PassAll().Do(Act.SetSpell, "Mystical Space Typhoon").PassAll();
            s.Do(Act.Activate, "Emergency Provisions").Pick("Mystical Space Typhoon", "Mystical Space Typhoon").PassAll();
            c(s.D.P(0).Lp == 10000, $"2 sent: +2000 (LP {s.D.P(0).Lp})");
        });

        T("Card of Sanctity", c =>
        {
            var s = new Sc(new[] { "Card of Sanctity", "Mystical Space Typhoon", Neo }, none, classic: false);
            Put(s, 0, Neo);
            s.EndTurn().EndTurn();
            int hand = s.D.P(0).Hand.Count;
            s.Do(Act.Activate, "Card of Sanctity").PassAll();
            c(s.D.P(0).Hand.Count == 2 && !s.D.P(0).MonstersOnField.Any() && s.D.P(0).Banished.Count == hand, $"hand+field banished, hand now 2 ({s.D.P(0).Hand.Count}, banished {s.D.P(0).Banished.Count})");
            var s2 = new Sc(new[] { "Card of Sanctity" }, none, classic: false);
            c(!s2.D.MainOptions(0, true).Any(o => o.Act == Act.Activate && o.Card?.Name == "Card of Sanctity"), "needs at least 1 card on the field (min. 1 from each)");
        });

        T("Cyber Dragon", c =>
        {
            var s = new Sc(new[] { "Cyber Dragon" }, new[] { Neo }, classic: false);
            c(!s.D.Pending!.Options.Any(o => o.Act == Act.Special && o.Card?.Name == "Cyber Dragon"), "no Special Summon with an empty field");
            Put(s, 1, Neo);
            c(s.D.MainOptions(0, true).Any(o => o.Act == Act.Special && o.Card?.Name == "Cyber Dragon"), "Special Summon when only the opponent has a monster");
            Put(s, 0, Elf);
            c(!s.D.MainOptions(0, true).Any(o => o.Act == Act.Special && o.Card?.Name == "Cyber Dragon"), "not while you control a monster");
        });

        T("Power Bond", c =>
        {
            var s = new Sc(new[] { "Power Bond", "Cyber Dragon", "Cyber Dragon" }, none, classic: false, extra0: new[] { "Cyber Twin Dragon" });
            s.Do(Act.Activate, "Power Bond").PassAll();
            while (s.Has(Ask.Pick)) s.Pick(s.D.Pending!.Options[0].Card!.Name);
            s.AttackPos().PassAll();
            var ctd = s.Mon(0, "Cyber Twin Dragon");
            c(ctd != null && s.D.Atk(ctd) == 5600, $"Cyber Twin Dragon at 5600 ATK ({(ctd == null ? "none" : s.D.Atk(ctd))})");
            s.EndTurn();
            c(s.D.P(0).Lp == 8000 - 2800, $"End Phase: 2800 damage (LP {s.D.P(0).Lp})");
        });

        T("Instant Fusion", c =>
        {
            var s = new Sc(new[] { "Instant Fusion", "Instant Fusion" }, none, classic: false, extra0: new[] { "Flame Swordsman" });
            s.Do(Act.Activate, "Instant Fusion").PassAll();
            while (s.Has(Ask.Pick)) s.Pick("Flame Swordsman");
            s.AttackPos().PassAll();
            c(s.Mon(0, "Flame Swordsman") != null && s.D.P(0).Lp == 7000, $"Level 5 Fusion summoned for 1000 LP (LP {s.D.P(0).Lp})");
            c(!s.D.MainOptions(0, true).Any(o => o.Act == Act.Activate && o.Card?.Name == "Instant Fusion"), "only 1 Instant Fusion per turn");
            s.EndTurn();
            c(s.Mon(0, "Flame Swordsman") == null, "destroyed in the End Phase");
        });

        T("Mind Control", c =>
        {
            var s = new Sc(new[] { "Mind Control", "Summoned Skull" }, new[] { Neo }, classic: false);
            var neo = Put(s, 1, Neo, set: true);
            s.EndTurn().EndTurn();
            s.Do(Act.Activate, "Mind Control").Pick(Neo).PassAll();
            c(neo.Controller == 0, "control of a face-down monster taken");
            c(!s.D.MainOptions(0, true).Any(o => o.Act == Act.Summon && o.Card?.Name == "Summoned Skull"), "can't be Tributed");
            s.EndTurn();
            c(neo.Controller == 1, "returned in the End Phase");
            var s2 = new Sc(new[] { "Mind Control" }, new[] { Neo }, classic: false);
            var up = Put(s2, 1, Neo);
            s2.EndTurn().EndTurn().Do(Act.Activate, "Mind Control").Pick(Neo).PassAll();
            c(!s2.D.Pending!.Options.Any(o => o.Act == Act.ToBattle), "can't attack (no Battle Phase offered: it is the only monster)");
        });

        T("Soul Charge", c =>
        {
            var s = new Sc(new[] { "Soul Charge", "Soul Charge" }, none, classic: false);
            Gy(s, 0, Elf); Gy(s, 0, Elf);
            s.EndTurn().EndTurn();
            s.Do(Act.Activate, "Soul Charge");
            while (s.Has(Ask.Pick)) s.D.Submit(new Command(0, Enumerable.Range(0, s.D.Pending!.Options.Count(o => o.Card != null)).ToArray()));
            while (s.Has(Ask.Position) || s.Has(Ask.Respond)) { s.AttackPos(); s.PassAll(); }
            c(s.D.P(0).MonstersOnField.Count() == 2 && s.D.P(0).Lp == 6000, $"2 revived, 2000 LP lost (LP {s.D.P(0).Lp})");
            c(!s.D.Pending!.Options.Any(o => o.Act == Act.ToBattle), "no Battle Phase this turn");
            var s2 = new Sc(new[] { "Soul Charge", Neo }, none, classic: false);
            Gy(s2, 0, Elf);
            Put(s2, 0, Neo);
            s2.EndTurn().EndTurn().Do(Act.ToBattle).PassAll().Do(Act.ToMain2).PassAll();
            c(!s2.D.Pending!.Options.Any(o => o.Act == Act.Activate && o.Card?.Name == "Soul Charge"), "can't be activated in Main Phase 2 (ruling)");
        });

        T("Rope of Life", c =>
        {
            var s = new Sc(new[] { "Rope of Life", Elf, Elf }, new[] { Neo }, classic: false);
            var elf = Put(s, 0, Elf); elf.Defense = false;
            s.Do(Act.SetSpell, "Rope of Life").PassAll();
            Put(s, 1, Neo);
            s.EndTurn().Do(Act.ToBattle).PassAll().Do(Act.Attack, Neo, Elf);
            Until(s, "Rope of Life");
            while (s.Has(Ask.Pick)) s.Pick(Elf);
            s.PassAll().AttackPos().PassAll();
            c(elf.Loc == Loc.Monster && s.D.Atk(elf) == 1600 && s.D.P(0).Hand.Count == 0, $"revived at 1600 ATK, whole hand discarded (ATK {s.D.Atk(elf)}, hand {s.D.P(0).Hand.Count})");
        });

        T("Hallowed Life Barrier", c =>
        {
            var s = new Sc(new[] { "Hallowed Life Barrier", Elf, "Power Bond", "Cyber Dragon", "Cyber Dragon" }, new[] { Neo }, classic: false, extra0: new[] { "Cyber Twin Dragon" });
            s.Do(Act.SetSpell, "Hallowed Life Barrier").PassAll().EndTurn();
            Put(s, 1, Neo);
            s.EndTurn();
            // Own turn: Barrier, then Power Bond; its End Phase damage comes from your own card, so it still applies.
            s.Do(Act.Activate, "Hallowed Life Barrier");
            while (s.Has(Ask.Pick)) s.Pick(Elf);
            s.PassAll().Do(Act.Activate, "Power Bond").PassAll();
            while (s.Has(Ask.Pick)) s.Pick(s.D.Pending!.Options[0].Card!.Name);
            s.AttackPos().PassAll().EndTurn();
            c(s.D.P(0).Lp == 8000 - 2800, $"Power Bond's damage (your own card) still applies (LP {s.D.P(0).Lp})");
            var s2 = new Sc(new[] { "Hallowed Life Barrier", Elf }, new[] { Neo }, classic: false);
            s2.Do(Act.SetSpell, "Hallowed Life Barrier").PassAll();
            Put(s2, 1, Neo);
            s2.EndTurn().Do(Act.ToBattle).PassAll().Do(Act.Attack, Neo, "direct");
            Until(s2, "Hallowed Life Barrier");
            while (s2.Has(Ask.Pick)) s2.Pick(Elf);
            s2.PassAll();
            c(s2.D.P(0).Lp == 8000, $"no damage from the opponent's attack (LP {s2.D.P(0).Lp})");
        });

        T("Necro Gardna", c =>
        {
            var s = new Sc(new[] { "Necro Gardna" }, new[] { Neo }, classic: false);
            Gy(s, 0, "Necro Gardna");
            Put(s, 1, Neo);
            s.EndTurn().Do(Act.ToBattle).PassAll().Do(Act.Attack, Neo, "direct");
            Until(s, "Necro Gardna").PassAll();
            c(s.D.P(0).Lp == 8000 && s.D.P(0).Banished.Any(x => x.Name == "Necro Gardna"), $"attack negated (LP {s.D.P(0).Lp})");
        });

        T("Magical Scientist", c =>
        {
            var s = new Sc(new[] { "Magical Scientist" }, none, classic: false, extra0: new[] { "Flame Swordsman" });
            Put(s, 0, "Magical Scientist");
            s.EndTurn().EndTurn().Do(Act.Activate, "Magical Scientist").PassAll();
            while (s.Has(Ask.Pick)) s.Pick("Flame Swordsman");
            s.AttackPos().PassAll();
            var fs = s.Mon(0, "Flame Swordsman");
            c(fs != null && s.D.P(0).Lp == 7000, $"Fusion summoned for 1000 LP (LP {s.D.P(0).Lp})");
            s.Do(Act.ToBattle).PassAll();
            c(!s.D.Pending!.Options.Any(o => o.Act == Act.Attack && o.Card == fs && o.Target == null), "can't attack directly");
            s.EndTurn();
            c(fs!.Loc == Loc.Extra, $"returned to the Fusion Deck at end of turn ({fs.Loc})");
        });

        T("Rescue Cat", c =>
        {
            var s = new Sc(new[] { Elf, Elf, Elf, Elf, Elf, Elf, Elf, Elf, "Rescue Cat", "Silver Fang", "Silver Fang" }, none, classic: false);
            var cat = Put(s, 0, "Rescue Cat");
            s.EndTurn().EndTurn().Do(Act.Activate, "Rescue Cat").PassAll();
            while (s.Has(Ask.Pick)) s.D.Submit(new Command(0, new[] { 0, 1 }));
            while (s.Has(Ask.Position) || s.Has(Ask.Respond)) { s.AttackPos(); s.PassAll(); }
            c(s.D.P(0).MonstersOnField.Count(x => x.Name == "Silver Fang") == 2 && cat.Loc == Loc.Grave, "2 Level 3 Beasts from the Deck");
            s.EndTurn();
            c(s.D.P(0).Grave.Count(x => x.Name == "Silver Fang") == 2, "destroyed in the End Phase");
        });

        // ---- family: Amazoness / Ancient Gear / Arcana Force (cards the user flagged WRONG)

        T("Amazoness Call", c =>
        {
            var s = new Sc(new[] { "Amazoness Paladin", Neo, "Amazoness Call", Elf }, new[] { Elf, Elf }, classic: false);
            Gy(s, 0, "Amazoness Call");
            var pal = Put(s, 0, "Amazoness Paladin"); var neo = Put(s, 0, Neo);
            Put(s, 1, Elf).Defense = false; Put(s, 1, Elf).Defense = false;
            s.EndTurn().EndTurn().Do(Act.Activate, "Amazoness Call");
            while (s.Has(Ask.Pick)) s.Pick("Amazoness Paladin");
            s.PassAll().Do(Act.Summon, Elf).AttackPos().PassAll().Do(Act.ToBattle).PassAll();
            var o = s.D.Pending!.Options;
            c(o.Count(x => x.Act == Act.Attack && x.Card == pal) == 2, $"Paladin can attack each opponent monster ({o.Count(x => x.Act == Act.Attack && x.Card == pal)})");
            c(!o.Any(x => x.Act == Act.Attack && x.Card != pal), "no other monster can attack, including one Summoned after: " + string.Join(",", o.Where(x => x.Act == Act.Attack && x.Card != pal).Select(x => x.Card!.Name).Distinct()));
        });

        T("Ultimate Ancient Gear Golem", c =>
        {
            var s = new Sc(new[] { "Dark Hole", "Ancient Gear Golem" }, none, classic: false, extra0: new[] { "Ultimate Ancient Gear Golem" });
            var agg = Gy(s, 0, "Ancient Gear Golem");
            Put(s, 0, "Ultimate Ancient Gear Golem");
            s.EndTurn().EndTurn().Do(Act.Activate, "Dark Hole").PassAll();
            bool asked = s.Has(Ask.YesNo) && s.D.Pending!.Text.Contains("Ancient Gear Golem");
            c(asked, $"asked to Special Summon Ancient Gear Golem when destroyed ({s.D.Pending})");
            if (asked) { s.Yes(); while (s.Has(Ask.Pick)) s.Pick("Ancient Gear Golem"); s.AttackPos().PassAll(); }
            c(agg.Loc == Loc.Monster, "Ancient Gear Golem revived ignoring its conditions");
        });

        T("Arcana Force EX - The Dark Ruler", c =>
        {
            var ruler = new Sc(new[] { "Dark Hole", "Mystical Space Typhoon", "Arcana Force EX - The Dark Ruler" }, new[] { "Mystical Space Typhoon" }, classic: false);
            var r = Put(ruler, 0, "Arcana Force EX - The Dark Ruler"); r.Vars["arcana"] = 2;   // Tails
            ruler.Do(Act.SetSpell, "Mystical Space Typhoon").PassAll().EndTurn().Do(Act.SetSpell, "Mystical Space Typhoon").PassAll().EndTurn();
            ruler.Do(Act.Activate, "Dark Hole").PassAll();
            while (ruler.Has(Ask.YesNo)) ruler.Yes();
            ruler.PassAll();
            c(r.Loc == Loc.Grave, "destroyed by Dark Hole");
            c(!ruler.D.P(0).SpellsOnField.Any() && !ruler.D.P(1).SpellsOnField.Any(), $"Tails: destroyed -> all cards on the field destroyed ({string.Join(",", ruler.D.FieldCards)})");
        });

        T("Arcana Force EX - The Light Ruler", c =>
        {
            var s = new Sc(new[] { "Arcana Force EX - The Light Ruler" }, new[] { "Mind Control" }, classic: false);
            var r = Put(s, 0, "Arcana Force EX - The Light Ruler"); r.Vars["arcana"] = 2;   // Tails
            s.EndTurn().Do(Act.Activate, "Mind Control").Pick("Arcana Force EX - The Light Ruler").PassAll();
            while (s.Has(Ask.YesNo)) s.Yes();
            s.PassAll();
            c(r.Controller == 0 && s.D.Atk(r) == 3000 && s.D.P(1).Grave.Any(x => x.Name == "Mind Control"), $"Tails: targeted -> loses 1000, Mind Control negated and destroyed (ctrl {r.Controller}, ATK {s.D.Atk(r)})");
        });

        // ---- family: Cyber Dragon / Cyber Angel

        T("Cybernetic Hidden Technology", c =>
        {
            var s = new Sc(new[] { "Cybernetic Hidden Technology", "Cyber Dragon" }, new[] { Neo }, classic: false);
            var cd = Put(s, 0, "Cyber Dragon");
            s.Do(Act.SetSpell, "Cybernetic Hidden Technology").PassAll();
            Put(s, 1, Neo);
            s.EndTurn();
            s.Do(Act.ToBattle).PassAll().Do(Act.Attack, Neo, "Cyber Dragon");
            Until(s, "Cybernetic Hidden Technology");                       // flips the Continuous Trap face-up
            s.Respond("Cybernetic Hidden Technology");                      // chain its effect onto it
            while (s.Has(Ask.Pick)) s.Pick("Cyber Dragon");
            s.PassAll();
            c(s.D.P(1).Grave.Any(x => x.Name == Neo) && cd.Loc == Loc.Grave, $"Cyber Dragon sent, attacker destroyed (Neo {s.D.P(1).MonstersOnField.Any()}, CD {cd.Loc})");
        });

        T("Cyber Angel Benten", c =>
        {
            var s = new Sc(new[] { "Machine Angel Ritual", "Cyber Angel Dakini", "Cyber Angel Benten", "Cyber Petit Angel", Elf, "Cyber Angel Idaten", Elf, "Cyber Petit Angel" }, none, classic: false);
            s.Do(Act.Activate, "Machine Angel Ritual").PassAll();
            while (s.Has(Ask.Pick) && s.D.Pending!.Options.Any(o => o.Card?.Name == "Cyber Angel Dakini")) s.Pick("Cyber Angel Dakini");
            while (s.Has(Ask.Pick) && s.D.Pending!.Text.StartsWith("Tribute")) s.Pick(s.D.Pending.Options.Any(o => o.Card?.Name == "Cyber Angel Benten") ? "Cyber Angel Benten" : "Cyber Petit Angel");
            bool asked = false;
            for (int i = 0; i < 12 && s.D.Pending is { Kind: not Ask.Main } p; i++)
            {
                if (p.Kind == Ask.YesNo) { asked |= p.Text.Contains("LIGHT Fairy"); s.Yes(); }
                else if (p.Kind == Ask.Pick) s.D.Submit(new Command(p.Player, new[] { 0 }));
                else if (p.Kind == Ask.Position) s.AttackPos();
                else s.Pass();
            }
            c(asked, "Tributed from the hand for a Ritual Summon: offers the LIGHT Fairy search");
        });

        T("The First Sarcophagus", c =>
        {
            var s = new Sc(new[] { "The First Sarcophagus", "The Second Sarcophagus" }, new[] { "Mystical Space Typhoon" }, classic: false);
            var second = s.D.P(0).Hand.Concat(s.D.P(0).Deck).First(x => x.Name == "The Second Sarcophagus");
            s.Do(Act.SetSpell, "The First Sarcophagus").PassAll().EndTurn().EndTurn();
            s.Do(Act.Activate, "The First Sarcophagus").PassAll().EndTurn().EndTurn();   // P1's End Phase: Second is placed
            c(second.Loc == Loc.Spell && second.FaceUp, $"Second Sarcophagus placed in your opponent's End Phase ({second.Loc})");
            s.EndTurn();
            s.Do(Act.Activate, "Mystical Space Typhoon").Pick("The Second Sarcophagus").PassAll();
            c(s.D.P(0).Grave.Any(x => x.Name == "The First Sarcophagus"), "one Sarcophagus removed: the others go to the GY");
        });

        T("Union Attack", c =>
        {
            var s = new Sc(new[] { "Union Attack", Neo, Neo }, new[] { Elf }, classic: false);
            var a = Put(s, 0, Neo); var b = Put(s, 0, Neo);
            Put(s, 1, Elf).Defense = false;
            s.EndTurn().EndTurn().Do(Act.Activate, "Union Attack").Pick(Neo).PassAll().Do(Act.ToBattle).PassAll();
            var hit = s.D.Pending!.Options.FirstOrDefault(o => o.Act == Act.Attack && o.Target?.Name == Elf)?.Card;
            c(hit != null && s.D.Atk(hit) == 3400, $"target gains the other monster's ATK in the Battle Phase ({(hit == null ? "-" : s.D.Atk(hit))})");
            c(!s.D.Pending.Options.Any(o => o.Act == Act.Attack && o.Card != hit), "the other Attack Position monster can't attack");
            s.Do(Act.Attack, hit!.Name, Elf).PassAll();
            c(s.D.P(1).Lp == 8000 && s.D.P(1).Grave.Any(x => x.Name == Elf), $"no battle damage to the opponent (LP {s.D.P(1).Lp})");
        });

        // ---- 2026-11-01 overnight pass (activation audit + lab questions)

        // ---- 2026-11-02: staple cards the random-board sweep never saw activatable
        void Offered(Action<bool, string> c, Sc s, string card, string why) =>
            c(s.D.Pending!.Options.Any(o => o.Act == Act.Activate && o.Card?.Name == card), $"{card} offered: {why} ({string.Join(", ", s.D.Pending.Options.Take(12))})");
        const string Avian = "Elemental HERO Avian", Burst = "Elemental HERO Burstinatrix", Wingman = "Elemental HERO Flame Wingman";
        T("Power Bond", c =>
        {
            var s = new Sc(new[] { "Power Bond", "Cyber Dragon", "Cyber Dragon" }, none, classic: false, extra0: new[] { "Cyber Twin Dragon" });
            Offered(c, s, "Power Bond", "2 Cyber Dragon in hand, Cyber Twin Dragon in the Extra Deck");
        });
        T("Level Up!", c =>
        {
            var s = new Sc(new[] { "Level Up!", Elf, Elf, Elf, Elf, "Armed Dragon LV3", "Armed Dragon LV5" }, none, classic: false);
            Put(s, 0, "Armed Dragon LV3"); s.D.RefreshPrompt();
            Offered(c, s, "Level Up!", "face-up Armed Dragon LV3, LV5 in the Deck");
        });
        T("Super Polymerization", c =>
        {
            var s = new Sc(new[] { "Super Polymerization", Elf, Elf, Elf, Elf, Avian, Burst }, none, classic: false, extra0: new[] { Wingman });
            Put(s, 0, Avian); Put(s, 0, Burst); s.D.RefreshPrompt();
            Offered(c, s, "Super Polymerization", "Avian + Burstinatrix on the field, a card to discard, Flame Wingman in the Extra Deck");
        });
        T("Miracle Fusion", c =>
        {
            var s = new Sc(new[] { "Miracle Fusion", Elf, Elf, Elf, Elf, Avian, Burst }, none, classic: false, extra0: new[] { Wingman });
            Put(s, 0, Avian); Gy(s, 0, Burst); s.D.RefreshPrompt();
            Offered(c, s, "Miracle Fusion", "Avian on the field + Burstinatrix in the GY, Flame Wingman in the Extra Deck");
        });
        T("Double Fusion", c =>
        {
            var s = new Sc(new[] { "Double Fusion", Avian, Burst }, none, classic: false, extra0: new[] { Wingman });
            Offered(c, s, "Double Fusion", "Avian + Burstinatrix in hand, Flame Wingman in the Extra Deck");
        });
        T("Des Croaking", c =>
        {
            var s = new Sc(new[] { "Des Croaking", Elf, Elf, Elf, Elf, "Des Frog", "Des Frog", "Des Frog" }, new[] { Elf }, classic: false);
            Put(s, 0, "Des Frog"); Put(s, 0, "Des Frog"); Put(s, 0, "Des Frog"); s.D.RefreshPrompt();
            Offered(c, s, "Des Croaking", "3 face-up Des Frog");
        });

        T("King of the Swamp", c =>
        {
            var s = new Sc(new[] { "King of the Swamp", Elf, Elf, Elf, Elf, Elf, "Polymerization" }, none, classic: false);
            Offered(c, s, "King of the Swamp", "in hand, Polymerization in the Deck");
        });
        T("Fenrir", c =>
        {
            var water = CardDb.Shared.ByIdx.Values.First(d => d.IsMonster && !d.IsFusion && d.Attribute == "WATER" && d.PackMask != 0).Name;
            var s = new Sc(new[] { "Fenrir", Elf, Elf, Elf, Elf, water, water }, none, classic: false);
            Gy(s, 0, water); Gy(s, 0, water); s.D.RefreshPrompt();
            c(s.D.Pending!.Options.Any(o => o.Act == Act.Special && o.Card?.Name == "Fenrir"), $"Special Summon offered with 2 WATER ({water}) in the GY ({string.Join(", ", s.D.Pending.Options.Take(12))})");
        });
        T("Exploder Dragon", c =>
        {
            var s = new Sc(new[] { "Exploder Dragon" }, new[] { "Battle Ox" }, classic: false);
            Put(s, 0, "Exploder Dragon");
            s.EndTurn(); Put(s, 1, "Battle Ox"); s.EndTurn();
            s.PassAll().Do(Act.ToBattle).PassAll().Do(Act.Attack, "Exploder Dragon", "Battle Ox").PassAll();
            c(s.D.P(1).Grave.Any(x => x.Name == "Battle Ox") && s.D.P(0).Grave.Any(x => x.Name == "Exploder Dragon"), "destroyed by battle, takes the attacker with it");
        });
        T("Reflect Bounder", c =>
        {
            var s = new Sc(new[] { "Reflect Bounder" }, new[] { "Battle Ox" }, classic: false);
            Put(s, 0, "Reflect Bounder");
            s.EndTurn(); Put(s, 1, "Battle Ox"); s.D.RefreshPrompt();
            s.PassAll().Do(Act.ToBattle).PassAll().Do(Act.Attack, "Battle Ox", "Reflect Bounder").PassAll();
            c(s.D.P(1).Lp == 8000 - 1700, $"the attacker's controller takes its ATK (LP {s.D.P(1).Lp})");
        });
        T("Marshmallon", c =>
        {
            var s = new Sc(new[] { "Marshmallon" }, new[] { "Battle Ox" }, classic: false);
            Put(s, 0, "Marshmallon", set: true);
            s.EndTurn(); Put(s, 1, "Battle Ox"); s.D.RefreshPrompt();
            s.PassAll().Do(Act.ToBattle).PassAll().Do(Act.Attack, "Battle Ox", "Marshmallon").PassAll();
            c(s.D.P(1).Lp == 7000 && s.Mon(0, "Marshmallon") != null, $"set Marshmallon attacked: 1000 to the attacker, not destroyed (LP {s.D.P(1).Lp})");
        });

        T("Over Destiny", c =>
        {
            var s = new Sc(new[] { "Over Destiny", Elf, Elf, Elf, Elf, "Destiny HERO - Dogma", "Destiny HERO - Doom Lord" }, none, classic: false);
            Gy(s, 0, "Destiny HERO - Dogma");
            s.D.RefreshPrompt();
            c(s.D.Pending!.Options.Any(o => o.Act == Act.Activate && o.Card?.Name == "Over Destiny"), $"offered with Dogma (Level 8) in the GY and Doom Lord (Level 3) in the Deck ({string.Join(", ", s.D.Pending.Options)})");
        });

        T("Spellbinding Circle", c =>
        {
            var s = new Sc(new[] { "Spellbinding Circle" }, new[] { Elf }, classic: false);
            s.Do(Act.SetSpell, "Spellbinding Circle");
            s.EndTurn();
            Put(s, 1, Elf, set: true);
            s.EndTurn();
            c(!s.D.Pending!.Options.Any(o => o.Act == Act.Activate && o.Card?.Name == "Spellbinding Circle"), "not activatable with only a face-down monster to bind");
            s.D.P(1).MonstersOnField.First().FaceUp = true;
            s.D.RefreshPrompt();
            c(s.D.Pending!.Options.Any(o => o.Act == Act.Activate && o.Card?.Name == "Spellbinding Circle"), "activatable on a face-up monster");
        });

        T("Armed Dragon LV3", c =>
        {
            var s = new Sc(new[] { "Armed Dragon LV3", "Armed Dragon LV5" }, none, classic: false);
            Put(s, 0, "Armed Dragon LV3");
            s.EndTurn().EndTurn();
            c(s.D.Pending is { Kind: Ask.YesNo } p && p.Text.Contains("LV5"), $"asked in your Standby Phase ({s.D.Pending})");
            if (s.D.Pending?.Kind != Ask.YesNo) return;
            s.Yes();
            if (s.D.Pending is { Kind: Ask.Pick }) s.Pick("Armed Dragon LV5");
            s.AttackPos().PassAll();
            c(s.Mon(0, "Armed Dragon LV5") != null && s.D.P(0).Grave.Any(x => x.Name == "Armed Dragon LV3"), "LV3 sent, LV5 Special Summoned");
        });

        T("Armed Dragon LV5", c =>
        {
            var s = new Sc(new[] { "Armed Dragon LV5", "Armed Dragon LV7" }, new[] { Elf }, classic: false);
            Put(s, 0, "Armed Dragon LV5");
            s.EndTurn();
            Put(s, 1, Elf);
            s.EndTurn();
            s.PassAll();
            if (s.D.Pending?.Kind == Ask.Main) s.Do(Act.ToBattle);
            s.PassAll().Do(Act.Attack, "Armed Dragon LV5", Elf).PassAll();
            c(s.D.P(1).Grave.Any(x => x.Name == Elf), "LV5 destroyed Mystical Elf by battle");
            s.PassAll(); if (s.D.Pending?.Kind == Ask.Battle) s.Do(Act.ToMain2); s.PassAll().Do(Act.EndTurn); s.PassAll();
            c(s.D.Pending is { Kind: Ask.YesNo } p && p.Text.Contains("LV7"), $"asked in your End Phase ({s.D.Pending})");
            if (s.D.Pending?.Kind != Ask.YesNo) return;
            s.Yes();
            if (s.D.Pending is { Kind: Ask.Pick }) s.Pick("Armed Dragon LV7");
            s.AttackPos().PassAll();
            c(s.Mon(0, "Armed Dragon LV7") != null, "LV7 Special Summoned");
        });

        T("Armed Dragon LV7", c =>
        {
            var s = new Sc(new[] { "Armed Dragon LV7", Neo }, new[] { Elf }, classic: false);
            Put(s, 0, "Armed Dragon LV7");
            s.EndTurn();
            Put(s, 1, Elf, set: true);
            s.EndTurn();
            s.Do(Act.Activate, "Armed Dragon LV7").Pick(Neo).PassAll();
            c(s.D.P(1).Grave.Any(x => x.Name == Elf), "face-down 800 ATK monster destroyed by sending a 1700 ATK monster");
            var s2 = new Sc(new[] { "Armed Dragon LV7", Neo }, none, classic: false);
            Put(s2, 0, "Armed Dragon LV7");
            s2.EndTurn().EndTurn();
            c(!s2.D.Pending!.Options.Any(o => o.Act == Act.Activate && o.Card?.Name == "Armed Dragon LV7"), "not offered when the opponent controls no monsters");
        });

        T("Gaia Plate the Earth Giant", c =>
        {
            var s = new Sc(new[] { "Gaia Plate the Earth Giant", "Giant Soldier of Stone", "Giant Soldier of Stone" }, new[] { "Labyrinth Wall" }, classic: false);
            Put(s, 0, "Gaia Plate the Earth Giant");
            Gy(s, 0, "Giant Soldier of Stone"); Gy(s, 0, "Giant Soldier of Stone");
            s.EndTurn();
            var wall = Put(s, 1, "Labyrinth Wall"); wall.Defense = true;
            s.EndTurn();
            while (s.Has(Ask.YesNo) || s.Has(Ask.Pick)) { if (s.Has(Ask.YesNo)) s.Yes(); else s.Pick(s.D.Pending!.Options[0].Card!.Name); }
            s.PassAll().Do(Act.ToBattle).PassAll().Do(Act.Attack, "Gaia Plate the Earth Giant", "Labyrinth Wall").PassAll();
            c(s.D.P(1).Grave.Any(x => x.Name == "Labyrinth Wall"), "3000 DEF halved to 1500 in damage calculation: destroyed by 2800");
        });

        T("Toon Kingdom", c =>
        {
            var s = new Sc(new[] { "Toon Alligator", "Toon Kingdom" }, new[] { Neo }, classic: false);
            var k = s.D.P(0).Hand.First(x => x.Name == "Toon Kingdom");
            s.D.P(0).Hand.Remove(k); s.D.Place(k, 0, Loc.Field); k.FaceUp = true;
            var t = Put(s, 0, "Toon Alligator"); t.Defense = false;
            Put(s, 1, Neo);
            s.EndTurn();
            int ban = s.D.P(0).Banished.Count;
            s.Do(Act.ToBattle).PassAll().Do(Act.Attack, Neo, "Toon Alligator").PassAll();
            while (s.Has(Ask.YesNo)) s.Yes();
            s.PassAll();
            c(s.Mon(0, "Toon Alligator") != null && s.D.P(0).Banished.Count == ban + 1, "top card banished instead of the Toon being destroyed");
        });

        T("Darkbishop Archfiend", c =>
        {
            var s = new Sc(new[] { "Darkbishop Archfiend" }, none, classic: false);
            Put(s, 0, "Darkbishop Archfiend");
            s.D.P(0).Lp = 300;
            s.EndTurn().EndTurn();
            c(s.D.P(0).Grave.Any(x => x.Name == "Darkbishop Archfiend") && s.D.P(0).Lp == 300, $"can't pay 500 at 300 LP: destroyed (LP {s.D.P(0).Lp})");
            var s2 = new Sc(new[] { "Darkbishop Archfiend" }, none, classic: false);
            Put(s2, 0, "Darkbishop Archfiend");
            s2.EndTurn().EndTurn();
            c(s2.D.P(0).Lp == 7500 && s2.Mon(0, "Darkbishop Archfiend") != null, $"pays 500 otherwise (LP {s2.D.P(0).Lp})");
        });

        T("Gravekeeper's Watcher", c =>
        {
            c(Scripts.DiscardsOnResolve("Your opponent discards 1 random card."), "a resolving discard counts");
            c(!Scripts.DiscardsOnResolve("Discard 1 card; destroy 1 monster."), "a discard cost doesn't");
            c(!Scripts.DiscardsOnResolve("If this card is discarded to the GY: draw 1 card."), "a 'when discarded' trigger doesn't");
        });

        Console.WriteLine($"labtest: {ok}/{total} cards pass");
        return fails == 0 ? 0 : 1;
    }
}
