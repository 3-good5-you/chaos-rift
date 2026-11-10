using PoC.Engine;

namespace PoC.Check;

/// Scripted duels on stacked decks, checking specific rules and PoC quirks.
static class Scenarios
{
    const string Filler = "Mystical Elf"; // 800/2000 vanilla, never played by the scripts below

    internal sealed class Sc
    {
        public readonly Duel D;
        public Sc(string[] deck0, string[] deck1, bool classic = true, string[]? extra0 = null)
        {
            var db = CardDb.Shared;
            var s = new DuelSetup { Seed = 11, Classic = classic, Shuffle = false };
            s.Main[0].AddRange(deck0.Concat(Enumerable.Repeat(Filler, 40 - deck0.Length)).Select(n => db.Named(n).Idx));
            s.Main[1].AddRange(deck1.Concat(Enumerable.Repeat(Filler, 40 - deck1.Length)).Select(n => db.Named(n).Idx));
            if (extra0 != null) s.Extra[0].AddRange(extra0.Select(n => db.Named(n).Idx));
            D = new Duel(s);
        }

        Prompt P => D.Pending ?? throw new Exception("duel is over");

        void Answer(int i) => D.Submit(new Command(P.Player, new[] { i }));

        /// Choose the option matching act (+ card name, + target name / "direct").
        public Sc Do(Act act, string? card = null, string? target = null)
        {
            var o = P.Options;
            int i = o.FindIndex(x => x.Act == act && (card == null || x.Card?.Name == card)
                && (target == null || (target == "direct" ? x.Target == null : x.Target?.Name == target)));
            if (i < 0) throw new Exception($"No {act} {card} {target} in {P}");
            Answer(i);
            return this;
        }

        public Sc Pass() { if (P.Kind != Ask.Respond) throw new Exception($"Expected a response window, got {P}"); Answer(P.Options.Count - 1); return this; }
        public Sc PassAll() { while (D.Pending is { Kind: Ask.Respond }) Pass(); return this; }
        public Sc Respond(string card) { if (P.Kind != Ask.Respond) throw new Exception($"Expected a response window, got {P}"); return Do(Act.Activate, card); }
        public Sc Pick(params string[] names)
        {
            var used = new HashSet<int>();
            var idx = names.Select(n => { int i = P.Options.FindIndex(o => o.Card?.Name == n && !used.Contains(o.Card.Uid)); if (i < 0) throw new Exception($"No pick {n} in {P}"); used.Add(P.Options[i].Card!.Uid); return i; }).ToArray();
            D.Submit(new Command(P.Player, idx));
            return this;
        }
        public Sc PickUid(int uid) { D.Submit(new Command(P.Player, new[] { P.Options.FindIndex(o => o.Card?.Uid == uid) })); return this; }
        public Sc Opt(string text) { Answer(P.Options.FindIndex(o => o.Text == text)); return this; }
        public Sc Yes() => Opt("Yes");
        public Sc No() => Opt("No");
        public Sc AttackPos() { if (P.Kind == Ask.Position) Opt("Attack Position"); return this; }
        public Sc EndTurn()
        {
            PassAll(); if (P.Kind == Ask.Battle) Do(Act.ToMain2); PassAll(); Do(Act.EndTurn); PassAll();
            if (D.Pending is { Kind: Ask.Pick } p && p.Text.StartsWith("Discard"))
                D.Submit(new Command(p.Player, Enumerable.Range(0, p.Options.Count).Where(i => p.Options[i].Card!.Name == Filler).Take(p.Min).ToArray()));
            PassAll();
            return this;
        }
        public bool Has(Ask k) => D.Pending?.Kind == k;
        public Card? Mon(int p, string name) => D.P(p).MonstersOnField.FirstOrDefault(m => m.Name == name);
    }

    public static void Run(Action<bool, string> check)
    {
        int ok = 0, total = 0;
        void T(string name, Action a)
        {
            total++;
            try { a(); ok++; }
            catch (Exception e) { check(false, $"scenario '{name}': {e.InnerException?.Message ?? e.Message}"); }
        }

        T("chain resolves last-in first-out (double Trap Hole)", () =>
        {
            var s = new Sc(new[] { "Trap Hole", "Trap Hole" }, new[] { "Neo the Magic Swordsman" });
            s.Do(Act.SetSpell, "Trap Hole").Do(Act.SetSpell, "Trap Hole").EndTurn();
            var traps = s.D.P(0).Spells.Where(c => c != null).ToList();
            var destroyedBy = new List<int>();
            s.D.OnEvent += e => { if (e.Kind == EvKind.Destroyed && e.Card!.Name == "Neo the Magic Swordsman") destroyedBy.Add(e.Other!.Uid); };
            s.Do(Act.Summon, "Neo the Magic Swordsman");
            s.Respond("Trap Hole");               // P0 link 1
            var first = s.D.Chain[0].Card;
            s.Respond("Trap Hole");               // P1 has nothing; P0 chains link 2 onto its own link
            s.PassAll();
            var second = traps.Single(t => t != first);
            check(destroyedBy.SequenceEqual(new[] { second.Uid }), "link 2 destroys the monster, link 1 finds nothing");
            check(s.D.P(0).Grave.Count(c => c.Name == "Trap Hole") == 2, "both Trap Holes go to the Graveyard");
        });

        T("Yami adds 200 to a direct attack (Neo 1700 -> 1900)", () =>
        {
            var s = new Sc(new[] { "Yami", "Neo the Magic Swordsman" }, new string[0]);
            s.Do(Act.Activate, "Yami").PassAll().Do(Act.Summon, "Neo the Magic Swordsman").PassAll().EndTurn().EndTurn();   // no attacks on turn 1
            s.Do(Act.ToBattle).PassAll().Do(Act.Attack, "Neo the Magic Swordsman", "direct").PassAll();
            check(s.D.P(1).Lp == 8000 - 1900, $"direct attack dealt 1900 (LP {s.D.P(1).Lp})");
        });

        T("A monster that attacked can't change position in Main Phase 2", () =>
        {
            var s = new Sc(new[] { "Neo the Magic Swordsman" }, new string[0]);
            s.Do(Act.Summon, "Neo the Magic Swordsman").PassAll().EndTurn().EndTurn();
            s.Do(Act.ToBattle).PassAll().Do(Act.Attack, "Neo the Magic Swordsman", "direct").PassAll();
            if (s.Has(Ask.Battle)) s.Do(Act.ToMain2);
            s.PassAll();
            check(!s.D.Pending!.Options.Any(o => o.Act == Act.ChangePos), "no ChangePos offered for the attacker");
        });

        T("Kuriboh discarded in the damage step zeroes the battle damage", () =>
        {
            var s = new Sc(new[] { "Neo the Magic Swordsman" }, new[] { "Kuriboh" });
            s.Do(Act.Summon, "Neo the Magic Swordsman").PassAll().EndTurn().EndTurn().Do(Act.ToBattle).PassAll().Do(Act.Attack, "Neo the Magic Swordsman", "direct");
            while (s.Has(Ask.Respond) && !s.D.Pending!.Options.Any(o => o.Card?.Name == "Kuriboh")) s.Pass();
            s.Respond("Kuriboh").PassAll();
            check(s.D.P(1).Lp == 8000 && s.D.P(1).Grave.Any(c => c.Name == "Kuriboh"), $"no damage taken (LP {s.D.P(1).Lp})");
        });

        T("Fissure (classic) breaks ties on the leftmost zone; modern asks", () =>
        {
            foreach (var classic in new[] { true, false })
            {
                var s = new Sc(new[] { "Fissure" }, new[] { "Neo the Magic Swordsman", "Neo the Magic Swordsman" }, classic);
                s.EndTurn();
                s.Do(Act.Summon).PassAll().EndTurn();
                s.EndTurn();
                s.Do(Act.Summon).PassAll().EndTurn();
                var left = s.D.P(1).Monsters[0]!; var right = s.D.P(1).Monsters[1]!;
                s.Do(Act.Activate, "Fissure");
                s.PassAll();
                if (!classic) { check(s.Has(Ask.Pick), "modern Fissure lets you choose among ties"); s.PickUid(right.Uid); s.PassAll(); }
                check(classic ? left.Loc == Loc.Grave && right.Valid() : right.Loc == Loc.Grave && left.Valid(), $"Fissure classic={classic} destroyed the expected monster");
            }
        });

        T("Exodia in the opening hand wins", () =>
        {
            var s = new Sc(new[] { "Exodia the Forbidden One", "Left Arm of the Forbidden One", "Right Arm of the Forbidden One", "Left Leg of the Forbidden One", "Right Leg of the Forbidden One" }, new string[0]);
            check(s.D.Over && s.D.Winner == 0 && s.D.EndReason == "Exodia", "Exodia win");
        });

        T("Drawing from an empty Deck loses", () =>
        {
            var db = CardDb.Shared;
            var setup = new DuelSetup { Shuffle = false };
            setup.Main[0].AddRange(Enumerable.Repeat(db.Named(Filler).Idx, 5));
            setup.Main[1].AddRange(Enumerable.Repeat(db.Named(Filler).Idx, 40));
            var d = new Duel(setup);
            check(d.Over && d.Winner == 1 && d.EndReason == "deck out", "deck out on the first player's turn-1 draw");
        });

        T("Polymerization: Flame Swordsman from hand materials", () =>
        {
            var s = new Sc(new[] { "Polymerization", "Flame Manipulator", "Masaki the Legendary Swordsman" }, new string[0], extra0: new[] { "Flame Swordsman" });
            s.Do(Act.Activate, "Polymerization").PassAll();
            while (s.Has(Ask.Pick)) s.Pick(s.D.Pending!.Options[0].Card!.Name);
            s.AttackPos().PassAll();
            check(s.Mon(0, "Flame Swordsman") != null && s.D.P(0).Grave.Count == 3, $"Flame Swordsman fused, materials + Polymerization in the Graveyard ({string.Join(",", s.D.P(0).Grave)} / {string.Join(",", s.D.P(0).MonstersOnField)}) {s.D.Pending}");
        });

        T("Ritual Summon: Black Luster Soldier tributing 8 levels", () =>
        {
            var s = new Sc(new[] { "Black Luster Ritual", "Black Luster Soldier", "Neo the Magic Swordsman", "Neo the Magic Swordsman" }, new string[0]);
            s.Do(Act.Activate, "Black Luster Ritual").PassAll();
            while (s.Has(Ask.Pick)) s.Pick(s.D.Pending!.Options[0].Card!.Name);
            s.AttackPos().PassAll();
            check(s.Mon(0, "Black Luster Soldier") != null && s.D.P(0).Grave.Count(c => c.Name == "Neo the Magic Swordsman") == 2, "ritual monster summoned with 2 level-4 tributes");
        });

        T("Mirror Force destroys the opponent's attack-position monsters", () =>
        {
            var s = new Sc(new[] { "Neo the Magic Swordsman" }, new[] { "Mirror Force" });
            s.Do(Act.Summon).PassAll().EndTurn();
            s.Do(Act.SetSpell, "Mirror Force").EndTurn();
            s.Do(Act.ToBattle).PassAll().Do(Act.Attack, "Neo the Magic Swordsman", "direct").Respond("Mirror Force").PassAll();
            check(s.D.P(0).Grave.Any(c => c.Name == "Neo the Magic Swordsman") && s.D.P(1).Lp == 8000, "attacker destroyed, no damage");
        });

        T("Change of Heart returns control at the End Phase", () =>
        {
            var s = new Sc(new[] { "Change of Heart" }, new[] { "Neo the Magic Swordsman" });
            s.EndTurn();
            s.Do(Act.Summon).PassAll().EndTurn();
            s.Do(Act.Activate, "Change of Heart").Pick("Neo the Magic Swordsman").PassAll();
            check(s.Mon(0, "Neo the Magic Swordsman") != null, "control taken");
            s.EndTurn();
            check(s.Mon(1, "Neo the Magic Swordsman") != null, "control returned");
        });

        T("Magic Cylinder reflects the attacker's ATK", () =>
        {
            var s = new Sc(new[] { "Neo the Magic Swordsman" }, new[] { "Magic Cylinder" });
            s.Do(Act.Summon).PassAll().EndTurn();
            s.Do(Act.SetSpell, "Magic Cylinder").EndTurn();
            s.Do(Act.ToBattle).PassAll().Do(Act.Attack, "Neo the Magic Swordsman", "direct").Respond("Magic Cylinder").PassAll();
            check(s.D.P(0).Lp == 8000 - 1700 && s.D.P(1).Lp == 8000, $"1700 reflected (LP {s.D.P(0).Lp}/{s.D.P(1).Lp})");
        });

        T("Mooyan Curry (classic) can heal the opponent", () =>
        {
            var s = new Sc(new[] { "Mooyan Curry" }, new string[0]);
            s.Do(Act.Activate, "Mooyan Curry").PassAll().Opt("Opponent").PassAll();
            check(s.D.P(1).Lp == 8200 && s.D.P(0).Lp == 8000, "opponent gained 200");
        });

        T("Tribute summon needs tributes; tributed monster leaves", () =>
        {
            var s = new Sc(new[] { "Summoned Skull", "Neo the Magic Swordsman" }, new string[0]);
            check(!s.D.Pending!.Options.Any(o => o.Act == Act.Summon && o.Card!.Name == "Summoned Skull"), "no Summoned Skull without a tribute");
            s.Do(Act.Summon, "Neo the Magic Swordsman").PassAll().EndTurn().EndTurn();
            s.Do(Act.Summon, "Summoned Skull").Pick();   // changed my mind: nothing spent, still free to Normal Summon
            check(s.Mon(0, "Neo the Magic Swordsman") != null && s.D.Pending!.Options.Any(o => o.Act == Act.Summon && o.Card!.Name == "Summoned Skull"), "tribute summon can be cancelled");
            s.Do(Act.Summon, "Summoned Skull").Pick("Neo the Magic Swordsman").PassAll();
            check(s.Mon(0, "Summoned Skull") != null && s.D.P(0).Grave.Any(c => c.Name == "Neo the Magic Swordsman"), "tribute summon");
        });

        T("Sangan searches when sent from the field to the Graveyard", () =>
        {
            var s = new Sc(new[] { "Sangan", "Dark Hole" }, new string[0]);
            s.Do(Act.Summon, "Sangan").PassAll().Do(Act.Activate, "Dark Hole").PassAll();
            check(s.Has(Ask.Pick) && s.D.Pending!.Text.Contains("1500"), "Sangan search prompt");
        });

        T("Replay of a scripted duel reproduces the state", () =>
        {
            var s = new Sc(new[] { "Neo the Magic Swordsman", "Yami" }, new[] { "Kuriboh" });
            s.Do(Act.Activate, "Yami").PassAll().Do(Act.Summon).PassAll().EndTurn().EndTurn().Do(Act.ToBattle).PassAll().Do(Act.Attack, "Neo the Magic Swordsman", "direct");
            check(s.D.Replay().StateHash() == s.D.StateHash(), "replay hash matches");
        });

        // Put deck cards straight onto the field (face-up Attack) to build a board quickly.
        // A card straight into its owner's GY (Duel.Place only knows zones on the field).
        static void ToGy(Sc s, int p, Card c)
        {
            s.D.P(p).Hand.Remove(c); s.D.P(p).Deck.Remove(c);
            s.D.P(p).Grave.Add(c); c.Loc = Loc.Grave;
        }

        static Card Put(Sc s, int p, string name)
        {
            var c = s.D.P(p).Deck.Concat(s.D.P(p).Hand).First(x => x.Name == name);
            s.D.Place(c, p, Loc.Monster);
            c.FaceUp = true;
            return c;
        }

        T("Polymerization needs a zone: all 5 full and hand-only materials = no activation", () =>
        {
            var s = new Sc(new[] { "Polymerization", "Flame Manipulator", "Masaki the Legendary Swordsman" }, new string[0], extra0: new[] { "Flame Swordsman" });
            for (int i = 0; i < 5; i++) Put(s, 0, Filler); s.D.RefreshPrompt();
            check(!s.D.MainOptions(0, true).Any(o => o.Act == Act.Activate && o.Card!.Name == "Polymerization"), "Polymerization not offered with 5 monsters and hand materials");
            s.D.P(0).Monsters[4]!.Loc = Loc.Grave; s.D.P(0).Monsters[4] = null;   // free a zone: offered again
            check(s.D.MainOptions(0, true).Any(o => o.Act == Act.Activate && o.Card!.Name == "Polymerization"), "Polymerization offered with a free zone");
        });

        T("Polymerization with full zones must use a field material", () =>
        {
            var s = new Sc(new[] { "Polymerization", "Masaki the Legendary Swordsman", Filler, Filler, Filler, Filler, "Flame Manipulator" }, new string[0], extra0: new[] { "Flame Swordsman" });
            for (int i = 0; i < 4; i++) Put(s, 0, Filler);
            Put(s, 0, "Flame Manipulator");
            check(s.D.MainOptions(0, true).Any(o => o.Act == Act.Activate && o.Card!.Name == "Polymerization"), "offered: Flame Manipulator is on the field");
        });

        T("Nightmare's Steelcage destroyed: attacks are back", () =>
        {
            var s = new Sc(new[] { "Nightmare's Steelcage" }, new[] { "Neo the Magic Swordsman", "Mystical Space Typhoon" });
            s.Do(Act.Activate, "Nightmare's Steelcage").PassAll().EndTurn();
            s.Do(Act.Summon, "Neo the Magic Swordsman").PassAll();
            check(!s.D.Pending!.Options.Any(o => o.Act == Act.ToBattle), "caged: no Battle Phase");
            s.Do(Act.Activate, "Mystical Space Typhoon");
            if (s.Has(Ask.Pick)) s.Pick("Nightmare's Steelcage");
            s.PassAll();
            check(s.D.P(0).Grave.Any(c => c.Name == "Nightmare's Steelcage") && s.D.Pending!.Options.Any(o => o.Act == Act.ToBattle), $"cage gone, Battle Phase offered ({s.D.Pending})");
        });

        T("CPU: Penguin Soldier returns both opponent monsters, none of its own", () =>
        {
            var s = new Sc(new[] { "Penguin Soldier" }, new string[0]);
            s.Do(Act.Set, "Penguin Soldier").PassAll().EndTurn();
            Put(s, 1, Filler); Put(s, 1, Filler); s.D.RefreshPrompt();
            s.EndTurn();
            s.Do(Act.FlipSummon, "Penguin Soldier");
            while (s.Has(Ask.Respond)) s.Pass();
            var cmd = new Ai(0, 1).Decide(s.D);
            var picks = cmd.Picks.Select(i => s.D.Pending!.Options[i].Card!).ToList();
            check(picks.Count == 2 && picks.All(c => c.Controller == 1), $"picked {string.Join(",", picks)}");
        });

        foreach (int level in new[] { 1, 5, 6, 7 })
        foreach (ulong seed in new ulong[] { 1, 20, 37, 54 })
        T($"CPU level {level} seed {seed}: two Spellbinding Circles, two attackers -> one circle each, no damage", () =>
        {
            var s = new Sc(new[] { Filler, Filler, Filler, Filler, Filler, Filler, "Blue-Eyes White Dragon", "Gaia The Fierce Knight" }, new[] { "Spellbinding Circle", "Spellbinding Circle" });
            Put(s, 0, "Blue-Eyes White Dragon"); Put(s, 0, "Gaia The Fierce Knight");
            s.EndTurn();
            s.Do(Act.SetSpell, "Spellbinding Circle").Do(Act.SetSpell, "Spellbinding Circle").EndTurn();
            var ai = Ai.ForLevel(1, seed, level);
            int lp = s.D.P(1).Lp;
            s.Do(Act.ToBattle);
            while (s.D.Pending is { Player: 1 } || s.Has(Ask.Respond)) { if (s.D.Pending!.Player == 1) s.D.Submit(ai.Decide(s.D)); else s.Pass(); }
            s.Do(Act.Attack, "Blue-Eyes White Dragon", "direct");
            while (s.D.Pending is { Player: 1 } || s.Has(Ask.Respond)) { if (s.D.Pending!.Player == 1) s.D.Submit(ai.Decide(s.D)); else s.Pass(); }
            int up = s.D.P(1).Spells.Count(c => c != null && c.FaceUp);
            check(up == 1, $"{up} circles used");
            check(s.D.P(1).Lp == lp, $"the bound attack still hit ({s.D.P(1).Lp} LP)");
            s.Do(Act.Attack, "Gaia The Fierce Knight", "direct");
            while (s.D.Pending is { Player: 1 } || s.Has(Ask.Respond)) { if (s.D.Pending!.Player == 1) s.D.Submit(ai.Decide(s.D)); else s.Pass(); }
            up = s.D.P(1).Spells.Count(c => c != null && c.FaceUp);
            check(up == 2 && s.D.P(1).Lp == lp, $"second attacker: {up} circles, {s.D.P(1).Lp} LP");
        });

        // Strategy the learned board value has to get right (user, 2026-10-30): positions and holding a trap.
        void CpuAnswers(Sc s, Ai ai) { while (s.D.Pending is { Player: 1 } || s.Has(Ask.Respond)) { if (s.D.Pending!.Player == 1) s.D.Submit(ai.Decide(s.D)); else s.Pass(); } }
        foreach (int level in new[] { 1, 3, 5, 7 })
        foreach (ulong seed in new ulong[] { 3, 41 })
        {
            // Real plays only: the CPU's look-ahead rebuilds the duel from its log, which Put bypasses.
            T($"CPU level {level} seed {seed}: an 800/2000 monster facing a 1700 attacker goes in Defense", () =>
            {
                var s = new Sc(new[] { "Battle Ox" }, new string[0]);
                s.Do(Act.Summon, "Battle Ox").PassAll().EndTurn();
                var ai = Ai.ForLevel(1, seed, level);
                while (!s.D.Over && s.D.TurnPlayer == 1 && s.D.Pending!.Player == 1 && !s.D.P(1).MonstersOnField.Any()) s.D.Submit(ai.Decide(s.D));
                var elf = s.D.P(1).MonstersOnField.FirstOrDefault();
                check(elf != null && elf.Defense, $"CPU monster: {(elf == null ? "none" : elf.Defense ? "Defense" : "Attack")}");
            });

            T($"CPU level {level} seed {seed}: Mirror Force waits out a lone 300-ATK attacker, fires on three", () =>
            {
                var s = new Sc(new[] { "Kuriboh", "Battle Ox", "La Jinn the Mystical Genie of the Lamp" }, new[] { "Mirror Force" });
                s.Do(Act.Summon, "Kuriboh").PassAll().EndTurn();
                s.Do(Act.SetSpell, "Mirror Force").EndTurn();
                var ai = Ai.ForLevel(1, seed, level);
                s.Do(Act.ToBattle); CpuAnswers(s, ai);
                s.Do(Act.Attack, "Kuriboh", "direct"); CpuAnswers(s, ai);
                bool held = s.D.P(1).Spells.Any(c => c?.Name == "Mirror Force");
                check(held && s.D.P(1).Lp == 7700, $"lone Kuriboh: Mirror Force {(held ? "held" : "used")}, CPU LP {s.D.P(1).Lp}");
                s.Do(Act.ToMain2); CpuAnswers(s, ai);
                s.Do(Act.Summon, "Battle Ox"); CpuAnswers(s, ai);
                s.EndTurn();
                s.EndTurn();
                s.Do(Act.Summon, "La Jinn the Mystical Genie of the Lamp"); CpuAnswers(s, ai);
                s.Do(Act.ToBattle); CpuAnswers(s, ai);
                s.Do(Act.Attack, "Battle Ox", "direct"); CpuAnswers(s, ai);
                held = s.D.P(1).Spells.Any(c => c?.Name == "Mirror Force");
                check(!held && !s.D.P(0).MonstersOnField.Any(), $"three attackers: Mirror Force {(held ? "still held" : "used")}, {s.D.P(0).MonstersOnField.Count()} left");
            });
        }

        T("Legendary Fisherman alone with Umi: the rival attacks directly", () =>
        {
            var s = new Sc(new[] { Filler, Filler, Filler, Filler, Filler, Filler, "Battle Ox" }, new[] { "Umi", "The Legendary Fisherman" });
            Put(s, 0, "Battle Ox"); s.EndTurn();
            Put(s, 1, "The Legendary Fisherman"); var umi = s.D.P(1).Deck.Concat(s.D.P(1).Hand).First(c => c.Name == "Umi"); s.D.Place(umi, 1, Loc.Field); umi.FaceUp = true; s.EndTurn();
            s.Do(Act.ToBattle).PassAll();
            check(s.D.Pending!.Options.Any(o => o.Act == Act.Attack && o.Target == null), "no direct attack");
            check(!s.D.Pending!.Options.Any(o => o.Act == Act.Attack && o.Target != null), "Fisherman attackable");
        });

        T("Total Defense Shogun: Flip Summon goes to Defense; attacks from Defense with its ATK", () =>
        {
            var s = new Sc(new[] { "Total Defense Shogun" }, new[] { Filler, Filler, Filler, Filler, Filler, Filler, Filler });
            var sh = Put(s, 0, "Total Defense Shogun"); sh.FaceUp = false; sh.Defense = true; sh.SetTurn = -5;
            var elf = Put(s, 1, Filler); elf.Defense = true;
            s.EndTurn().EndTurn();
            s.Do(Act.FlipSummon, "Total Defense Shogun").PassAll();
            check(sh.Defense, "Shogun stayed in Attack Position after the Flip Summon");
            s.Do(Act.ToBattle).PassAll().Do(Act.Attack, "Total Defense Shogun", Filler).PassAll();
            check(elf.Loc == Loc.Monster, "1550 ATK destroyed a 2000 DEF monster (used its DEF)");
        });

        T("Red-Moon Baby destroyed in the same battle doesn't take the monster", () =>
        {
            var s = new Sc(new[] { "Red-Moon Baby" }, new[] { "Red-Moon Baby" });
            var a = Put(s, 0, "Red-Moon Baby"); Put(s, 1, "Red-Moon Baby");
            s.EndTurn().EndTurn();
            s.Do(Act.ToBattle).PassAll().Do(Act.Attack, "Red-Moon Baby", "Red-Moon Baby").PassAll();
            s.Do(Act.ToMain2); while (s.D.Pending is { Kind: Ask.YesNo or Ask.Respond }) s.D.Submit(new Command(s.D.Pending.Player, new[] { s.D.Pending.Options.Count - 1 }));
            check(!s.D.AllMonsters.Any(), string.Join(",", s.D.AllMonsters.Select(m => $"{m.Name}@{m.Controller}")));
        });

        T("Destruction Punch: a face-down Defense monster counts", () =>
        {
            var s = new Sc(new[] { "Vorse Raider" }, new[] { "Destruction Punch", Filler });
            Put(s, 0, "Vorse Raider"); s.EndTurn();
            var elf = Put(s, 1, Filler); elf.FaceUp = false; elf.Defense = true;
            s.Do(Act.SetSpell, "Destruction Punch").EndTurn();
            s.Do(Act.ToBattle).PassAll().Do(Act.Attack, "Vorse Raider", Filler).Respond("Destruction Punch").PassAll();
            check(s.D.P(0).Grave.Any(c => c.Name == "Vorse Raider"), "Vorse Raider not destroyed");
        });

        T("Slate Warrior flipped by an attack gains 500", () =>
        {
            var s = new Sc(new[] { "Kuriboh" }, new[] { "Slate Warrior" });
            Put(s, 0, "Kuriboh"); s.EndTurn();   // 300 ATK into its 400 DEF: it survives, then the flip makes it 900 (a stronger attacker destroys it first)
            var sw = Put(s, 1, "Slate Warrior"); sw.FaceUp = false; sw.Defense = true; s.EndTurn();
            s.Do(Act.ToBattle).PassAll().Do(Act.Attack, "Kuriboh", "Slate Warrior").PassAll();
            check(sw.Loc == Loc.Monster && s.D.Def(sw) == 900, $"Slate Warrior {sw.Loc} DEF {s.D.Def(sw)}");
        });

        T("Mysterious Puppeteer: no LP for its own summon, 500 for the next", () =>
        {
            var s = new Sc(new[] { "Mysterious Puppeteer", "Celtic Guardian" }, new string[0]);
            s.Do(Act.Summon, "Mysterious Puppeteer").AttackPos().PassAll();
            check(s.D.P(0).Lp == 8000, $"{s.D.P(0).Lp} after its own summon");
            s.EndTurn().EndTurn().Do(Act.Summon, "Celtic Guardian").AttackPos().PassAll();
            check(s.D.P(0).Lp == 8500, $"{s.D.P(0).Lp} after the next summon");
        });

        T("De-Fusion brings back the materials used (from hand and field)", () =>
        {
            var s = new Sc(new[] { "Polymerization", "Flame Manipulator", "Masaki the Legendary Swordsman", "De-Fusion" }, new string[0], extra0: new[] { "Flame Swordsman" });
            void Auto() { while (s.D.Pending is { Kind: not Ask.Main } p) s.D.Submit(new Command(p.Player, p.Kind == Ask.Respond ? new[] { p.Options.Count - 1 } : p.Kind == Ask.Pick ? Enumerable.Range(0, p.Min).ToArray() : p.Kind == Ask.YesNo ? new[] { p.Options.FindIndex(o => o.Text == "Yes") } : new[] { 0 })); }
            s.Do(Act.Activate, "Polymerization"); Auto();
            check(s.D.P(0).MonstersOnField.Any(m => m.Name == "Flame Swordsman"), "no Flame Swordsman");
            s.Do(Act.Activate, "De-Fusion"); Auto();
            var names = s.D.P(0).MonstersOnField.Select(m => m.Name).OrderBy(n => n).ToList();
            check(names.SequenceEqual(new[] { "Flame Manipulator", "Masaki the Legendary Swordsman" }), string.Join(",", names));
        });

        T("The Dark Door: only 1 monster attacks per Battle Phase", () =>
        {
            var s = new Sc(new[] { "The Dark Door", Filler, Filler, Filler, Filler, Filler, Filler, Filler, "Celtic Guardian", "Battle Ox" }, new string[0]);
            Put(s, 0, "Celtic Guardian"); Put(s, 0, "Battle Ox");
            s.EndTurn().EndTurn();
            s.Do(Act.Activate, "The Dark Door").PassAll();
            s.Do(Act.ToBattle).PassAll();
            s.Do(Act.Attack, "Celtic Guardian", "direct").PassAll();
            bool more = s.D.Pending!.Options.Any(o => o.Act == Act.Attack);
            check(!more, "a second monster could attack");
        });

        T("CPU: United We Stand goes on its own monster", () =>
        {
            var s = new Sc(new[] { "United We Stand" }, new string[0]);
            var mine = Put(s, 0, Filler);
            Put(s, 1, Filler);
            s.EndTurn().EndTurn();
            s.Do(Act.Activate, "United We Stand");
            var cmd = new Ai(0, 1).Decide(s.D);
            check(s.D.Pending!.Options[cmd.Picks[0]].Card == mine, $"equipped to {s.D.Pending.Options[cmd.Picks[0]].Card}");
        });

        T("Deck builder: Fusions (old table and newer cards) and Rituals offer what they need", () =>
        {
            var db = CardDb.Shared;
            string Need(string n) => string.Join(",", DeckRules.Needs(db.Named(n)).Select(c => c.Name));
            check(Need("Ambulance Rescueroid") == "Rescueroid,Ambulanceroid", $"Ambulance Rescueroid -> {Need("Ambulance Rescueroid")}");
            check(Need("Flame Swordsman").Contains("Polymerization") && Need("Flame Swordsman").Contains("Flame Manipulator"), $"Flame Swordsman -> {Need("Flame Swordsman")}");
            check(Need("Black Luster Soldier").Contains("Black Luster Ritual"), $"Black Luster Soldier -> {Need("Black Luster Soldier")}");
            check(Need("Thousand Knives").Contains("Dark Magician"), $"Dark Magic Attack -> {Need("Thousand Knives")}");
        });

        // The Egyptian Gods (2026-11-09).
        T("Obelisk: 3 Tributes, Trap Hole can't answer it, untargetable, its effect wipes the field and stops its attack", () =>
        {
            var s = new Sc(new[] { "Obelisk the Tormentor" }, new[] { "Trap Hole" });
            s.EndTurn();
            s.Do(Act.SetSpell, "Trap Hole").EndTurn();
            check(s.D.P(0).Hand.Any(c => c.Name == "Obelisk the Tormentor") && !s.D.Pending!.Options.Any(o => o.Act == Act.Set && o.Card!.Name == "Obelisk the Tormentor"), "Obelisk can't be Set");
            for (int i = 0; i < 5; i++) Put(s, 0, Filler); s.D.RefreshPrompt();
            s.Do(Act.Summon, "Obelisk the Tormentor").Pick(Filler, Filler, Filler);
            var ob = s.Mon(0, "Obelisk the Tormentor");
            check(ob != null && s.D.Atk(ob) == 4000 && !s.Has(Ask.Respond), $"Obelisk is out at 4000 and no response window opened ({s.D.Pending})");
            check(s.D.Forbidden(Q.Target, ob!, 1), "Obelisk can't be targeted");
            Put(s, 1, Filler); Put(s, 1, Filler); s.D.RefreshPrompt();
            s.Do(Act.Activate, "Obelisk the Tormentor").Pick(Filler, Filler).PassAll();
            check(!s.D.P(1).MonstersOnField.Any(), "every opponent monster destroyed");
            check(!s.D.CanAttack(ob!) && !s.D.Pending!.Options.Any(o => o.Act == Act.ToBattle), "Obelisk can't attack the turn it used its effect");
        });

        T("A Special Summoned God goes to the GY in the End Phase (Obelisk via Monster Reborn)", () =>
        {
            var s = new Sc(new[] { "Monster Reborn", "Obelisk the Tormentor" }, new string[0]);
            var ob = s.D.P(0).Hand.First(c => c.Name == "Obelisk the Tormentor");
            ToGy(s, 0, ob); s.D.RefreshPrompt();
            s.Do(Act.Activate, "Monster Reborn");
            if (s.Has(Ask.Pick)) s.Pick("Obelisk the Tormentor");
            s.PassAll();
            if (s.Has(Ask.Position)) s.AttackPos();
            check(ob.Loc == Loc.Monster && ob.Summoned == SummonKind.Special, $"Obelisk revived ({ob.Loc})");
            s.EndTurn();
            check(ob.Loc == Loc.Grave, $"back in the GY at the End Phase ({ob.Loc})");
        });

        T("Slifer: ATK = 1000 x hand; an opponent's Attack Position summon loses 2000 and dies at 0", () =>
        {
            var s = new Sc(new[] { "Slifer the Sky Dragon" }, new[] { "Neo the Magic Swordsman", "Gemini Elf" });
            for (int i = 0; i < 3; i++) Put(s, 0, Filler); s.D.RefreshPrompt();
            s.Do(Act.Summon, "Slifer the Sky Dragon").Pick(Filler, Filler, Filler);
            var sl = s.Mon(0, "Slifer the Sky Dragon")!;
            check(s.D.Atk(sl) == 1000 * s.D.P(0).Hand.Count && !s.Has(Ask.Respond), $"Slifer ATK {s.D.Atk(sl)} with {s.D.P(0).Hand.Count} in hand");
            s.EndTurn();
            s.Do(Act.Summon, "Gemini Elf").AttackPos().PassAll();
            var ge = s.Mon(1, "Gemini Elf");
            check(ge == null, $"Gemini Elf (1900) dropped to 0 and was destroyed ({(ge == null ? "gone" : s.D.Atk(ge).ToString())})");
        });

        T("Ra: can't be Special Summoned; pays down to 100 LP for ATK; 1000 LP destroys a monster", () =>
        {
            var s = new Sc(new[] { "The Winged Dragon of Ra", "Monster Reborn" }, new string[0]);
            var ra = s.D.P(0).Hand.First(c => c.Name == "The Winged Dragon of Ra");
            check(!s.D.CanSpecialSummon(ra, 0), "Ra can't be Special Summoned");
            for (int i = 0; i < 3; i++) Put(s, 0, Filler); s.D.RefreshPrompt();
            s.Do(Act.Summon, "The Winged Dragon of Ra").Pick(Filler, Filler, Filler);
            if (s.Has(Ask.YesNo)) s.Yes();
            check(s.D.P(0).Lp == 100 && s.D.Atk(ra) == 7900 && s.D.Def(ra) == 7900, $"LP {s.D.P(0).Lp}, Ra {s.D.Atk(ra)}/{s.D.Def(ra)}");
            s.D.P(0).Lp = 8000;
            var foe = Put(s, 1, Filler); s.D.RefreshPrompt();
            s.Do(Act.Activate, "The Winged Dragon of Ra");
            if (s.Has(Ask.Pick)) s.PickUid(foe.Uid);
            s.PassAll();
            check(s.D.P(0).Lp == 7000 && !foe.Valid(), $"paid 1000 (LP {s.D.P(0).Lp}), target destroyed");
        });

        // God edge cases (2026-11-10).
        foreach (bool attack in new[] { true, false })
            T($"Slifer: an opponent's Special Summon in {(attack ? "Attack" : "Defense")} Position {(attack ? "loses 2000" : "is left alone")}", () =>
            {
                var s = new Sc(new[] { "Slifer the Sky Dragon" }, new[] { "Monster Reborn", "Gemini Elf" });
                for (int i = 0; i < 3; i++) Put(s, 0, Filler); s.D.RefreshPrompt();
                s.Do(Act.Summon, "Slifer the Sky Dragon").Pick(Filler, Filler, Filler);
                s.EndTurn();
                var ge = s.D.P(1).Hand.First(c => c.Name == "Gemini Elf");
                ToGy(s, 1, ge); s.D.RefreshPrompt();
                s.Do(Act.Activate, "Monster Reborn");
                if (s.Has(Ask.Pick)) s.Pick("Gemini Elf");
                s.PassAll();
                if (s.Has(Ask.Position)) s.Opt(attack ? "Attack Position" : "Defense Position");
                s.PassAll();
                if (attack) check(!ge.Valid() || ge.Loc != Loc.Monster, $"Gemini Elf (1900) Special Summoned in Attack Position dropped to 0 and was destroyed ({ge.Loc})");
                else check(ge.Loc == Loc.Monster && s.D.Atk(ge) == 1900, $"Gemini Elf in Defense Position keeps 1900 ({ge.Loc} {s.D.Atk(ge)})");
            });

        T("A Special Summoned Slifer goes to the GY in the End Phase", () =>
        {
            var s = new Sc(new[] { "Monster Reborn", "Slifer the Sky Dragon" }, new string[0]);
            var sl = s.D.P(0).Hand.First(c => c.Name == "Slifer the Sky Dragon");
            ToGy(s, 0, sl); s.D.RefreshPrompt();
            s.Do(Act.Activate, "Monster Reborn");
            if (s.Has(Ask.Pick)) s.Pick("Slifer the Sky Dragon");
            s.PassAll();
            if (s.Has(Ask.Position)) s.AttackPos();
            check(sl.Loc == Loc.Monster && sl.Summoned == SummonKind.Special, $"Slifer revived ({sl.Loc})");
            s.EndTurn();
            check(sl.Loc == Loc.Grave, $"back in the GY at the End Phase ({sl.Loc})");
        });

        T("Ra in the GY: Monster Reborn can't bring it back", () =>
        {
            var s = new Sc(new[] { "Monster Reborn", "The Winged Dragon of Ra" }, new string[0]);
            var ra = s.D.P(0).Hand.First(c => c.Name == "The Winged Dragon of Ra");
            ToGy(s, 0, ra); s.D.RefreshPrompt();
            bool can = s.D.Pending!.Options.Any(o => o.Act == Act.Activate && o.Card!.Name == "Monster Reborn");
            if (can) { s.Do(Act.Activate, "Monster Reborn"); check(!(s.Has(Ask.Pick) && s.D.Pending!.Options.Any(o => o.Card == ra)), "Ra offered to Monster Reborn"); }
            check(ra.Loc == Loc.Grave, $"Ra stays in the GY ({ra.Loc})");
        });

        foreach (var answer in new[] { "Solemn Judgment", "Trap Hole", "Bottomless Trap Hole", "Torrential Tribute" })
            T($"{answer} can't answer a God's Normal Summon (Ra)", () =>
            {
                var s = new Sc(new[] { "The Winged Dragon of Ra" }, new[] { answer });
                s.EndTurn();
                s.Do(Act.SetSpell, answer).EndTurn();
                for (int i = 0; i < 3; i++) Put(s, 0, Filler); s.D.RefreshPrompt();
                s.Do(Act.Summon, "The Winged Dragon of Ra").Pick(Filler, Filler, Filler);
                check(!(s.D.Pending is { Kind: Ask.Respond, Player: 1 }), $"{answer}: the opponent got a response window ({s.D.Pending})");
                if (s.Has(Ask.YesNo)) s.No();
                s.PassAll();
                check(s.Mon(0, "The Winged Dragon of Ra") != null, "Ra stays on the field");
            });

        T("Sphere Mode: Normal Summoned with 3 Tributes, then Tributes itself for a 4000 ATK Ra from the Deck", () =>
        {
            var s = new Sc(new[] { "The Winged Dragon of Ra - Sphere Mode", "The Winged Dragon of Ra" }, new string[0]);
            for (int i = 0; i < 3; i++) Put(s, 0, Filler); s.D.RefreshPrompt();
            s.Do(Act.Summon, "The Winged Dragon of Ra - Sphere Mode").Pick(Filler, Filler, Filler).PassAll();
            check(s.Mon(0, "The Winged Dragon of Ra - Sphere Mode") != null, "Sphere Mode on the field");
            s.Do(Act.Activate, "The Winged Dragon of Ra - Sphere Mode");
            if (s.Has(Ask.Pick)) s.Pick("The Winged Dragon of Ra");
            s.PassAll();
            var ra = s.Mon(0, "The Winged Dragon of Ra");
            check(ra != null && s.D.Atk(ra) == 4000, $"Ra out at {(ra == null ? "-" : s.D.Atk(ra))} ATK");
            s.EndTurn();
            check(s.Mon(0, "The Winged Dragon of Ra") != null, "that Ra stays past the End Phase");
        });

        // The CPU and the Gods (rules brain: hand-placed boards aren't in the command log the look-ahead replays).
        T("CPU: Tribute Summons Obelisk with 3 monsters, then wipes 3 opponent monsters with it", () =>
        {
            var s = new Sc(new[] { "Obelisk the Tormentor" }, new string[0]);
            for (int i = 0; i < 5; i++) Put(s, 0, Filler);
            for (int i = 0; i < 3; i++) Put(s, 1, Filler);
            s.D.RefreshPrompt();
            var ai = new Ai(0, 3, Brain.Plus);
            for (int k = 0; k < 30 && s.D.Phase is Phase.Main1 && s.D.Pending!.Player == 0; k++) s.D.Submit(ai.Decide(s.D));
            check(s.Mon(0, "Obelisk the Tormentor") != null, "Obelisk summoned");
            check(!s.D.P(1).MonstersOnField.Any(), $"Obelisk's effect used ({s.D.P(1).MonstersOnField.Count()} opponent monsters left)");
        });

        foreach (bool lethal in new[] { true, false })
            T($"CPU: Ra pays down to 100 LP {(lethal ? "for the kill" : "never without one")}", () =>
            {
                var s = new Sc(new[] { "The Winged Dragon of Ra" }, new[] { "Trap Hole" });
                s.EndTurn();
                if (!lethal) { s.Do(Act.SetSpell, "Trap Hole"); Put(s, 1, Filler); s.D.RefreshPrompt(); }
                s.EndTurn();
                if (lethal) s.D.P(1).Lp = 5000;
                for (int i = 0; i < 3; i++) Put(s, 0, Filler); s.D.RefreshPrompt();
                s.Do(Act.Summon, "The Winged Dragon of Ra").Pick(Filler, Filler, Filler);
                check(s.Has(Ask.YesNo), $"Ra asks to pay ({s.D.Pending})");
                var cmd = new Ai(0, 3, Brain.Plus).Decide(s.D);
                bool pays = s.D.Pending!.Options[cmd.Picks[0]].Text == "Yes";
                check(pays == lethal, $"Ra pays: {pays}");
            });

        // Playtest 2026-11-09: the game's CPU sat on a strong monster and never entered the Battle Phase.
        T("CPU (game brain): a lone attacker against an empty field attacks", () =>
        {
            // Every move goes through the command log: the look-ahead brains replay it (a hand-placed board is not in it).
            var s = new Sc(new[] { "Gemini Elf" }, new string[0]);
            s.Do(Act.Summon, "Gemini Elf").AttackPos().EndTurn().EndTurn();
            var ai = Environment.GetEnvironmentVariable("AI_BRAIN") is { } br ? new Ai(0, 5, Enum.Parse<Brain>(br)) : Ai.ForLevel(0, 5, 7);
            if (Environment.GetEnvironmentVariable("BRAIN_FILE") is { } f) ai.Board = Value.FromJson(File.ReadAllText(f));   // try another model
            if (Environment.GetEnvironmentVariable("AI_TRACE") != null) Ai.Overruled = Console.WriteLine;
            int t = s.D.Turn;
            for (int k = 0; k < 60 && !s.D.Over && s.D.Turn == t && s.D.Pending!.Player == 0; k++) { var c = ai.Decide(s.D); if (Environment.GetEnvironmentVariable("AI_TRACE") != null) Console.WriteLine($"  [{s.D.Phase}] mons {string.Join(",", s.D.P(0).MonstersOnField.Select(m => m.Name + (m.Defense ? ":DEF" : ":ATK") + (m.FaceUp ? "" : ":down")))} {s.D.Pending.Kind}: {string.Join(" | ", s.D.Pending.Options)}  ->  {string.Join(",", c.Picks.Select(i => s.D.Pending.Options[i]))}"); s.D.Submit(c); }
            check(s.D.P(1).Lp <= 8000 - 1900, $"opponent LP {s.D.P(1).Lp}");
        });

        Console.WriteLine($"Scenarios: {ok}/{total} ran");
    }
}
