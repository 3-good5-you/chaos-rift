namespace PoC.Engine;

// Official cards, batch 22 (Satellite Cannon .. Special Hurricane).
public static partial class Scripts
{
    /// Royal Magical Library / Skilled Dark Magician: a Spell Counter each time a Spell Card resolves (max. 3).
    static void SpellCounters(Script s)
    {
        var on = s.On;
        s.On = async (x, e) =>
        {
            if (on != null) await on(x, e);
            if (e.Kind == EvKind.Resolved && Up(x) && e.Link is { IsCardActivation: true } l && l.Card.Data.IsSpell && x.Self.Counters("spell") < 3) AddCounter(x, x.Self, 1, "spell");
        };
    }

    /// Some subset of at least `min` levels adds up to `target`.
    static bool SubsetSumN(List<int> l, int target, int min, int i = 0, int n = 0)
    {
        if (target == 0) return n >= min;
        if (i == l.Count || target < 0) return false;
        return SubsetSumN(l, target - l[i], min, i + 1, n + 1) || SubsetSumN(l, target, min, i + 1, n);
    }

    static void CardsP22(CardDb db)
    {
        {
            var s = S("Satellite Cannon");
            s.Forbid = (x, q, c) => q == Q.DestroyByBattle && c == x.Self && new[] { x.D.Attacker, x.D.AttackTarget }.FirstOrDefault(m => m != null && m != x.Self) is { } o && x.D.Level(o) <= 7;
            s.Stats = (x, c, st) => { if (c == x.Self) st.Add(x.Self.Var("satatk"), 0); };
            s.On = async (x, e) =>
            {
                if (e.Kind == EvKind.EndPhase && e.Player == x.Me && Up(x)) { x.Self.Vars["satatk"] = x.Self.Var("satatk") + 1000; x.D.Seq++; }
                if (e.Kind == EvKind.AfterCalc && e.Card == x.Self) { x.Self.Vars["satatk"] = 0; x.D.Seq++; }
            };
        }
        {
            static bool Dino(Ctx x, Card m) => m.FaceUp ? x.IsRace(m, "Dinosaur") : m.Data.Race == "Dinosaur";
            var s = S("Sauropod Brachion");
            s.SummonOk = (x, by, k) => x.Self.Loc != Loc.Deck;
            s.TributesNeeded = x => x.D.Tributable(x.Me).Any(m => Dino(x, m)) ? 1 : 2;
            s.TributeOk = (x, t) => x.D.Tributable(x.Me).Any(m => Dino(x, m)) ? Dino(x, t) : true;
            FlipDown(s);
            s.On = async (x, e) =>
            {
                if (e.Is(EvKind.Summoned, x.Self) && e.Arg == (int)SummonKind.Flip)
                    foreach (var m in x.D.AllMonsters.Where(m => m != x.Self && m.FaceUp).ToList()) await x.D.SetFaceDown(m);
            };
            s.BattleDamage = (x, src, to, dmg) => Up(x) && x.D.AttackTarget == x.Self && to == x.Opp ? dmg * 2 : dmg;
            s.Note = "With a Dinosaur on your field it is Tribute Summoned with 1 Dinosaur; otherwise it takes 2 Tributes.";
        }
        OnBattleDeath("Scarr, Scout of Dark World", x => x.Search(c => c.Data.IsMonster && c.In("Dark World") && c.Data.Level <= 4, "Add a Level 4 or lower Dark World monster", optional: true));
        {
            var s = Spell("Second Coin Toss", async x => x.Self.Vars["stay"] = 1);
            s.Note = "Not wired to coin tosses yet: the engine tosses coins without a pause to ask for a redo.";
        }
        {
            var s = Union("Second Goblin", (x, m) => Scripts.NameOf(m) == "Giant Orc");
            s.AddFx(f =>
            {
                f.Label = "Change the equipped monster's position"; f.InSpellZone = true;
                f.Can = x => x.Self.Var("union") == 1 && x.Self.EquippedTo is { FaceUp: true } && Hopt(x, "pos");
                f.Cost = async x => UseHopt(x, "pos");
                f.Run = async x => { if (x.Self.EquippedTo is { Loc: Loc.Monster } m) await x.D.ChangePosition(m, !m.Defense); };
            });
        }
        {
            var s = Spell("Seismic Shockwave", async x =>
            {
                x.Self.Vars["stay"] = 1;
                int picked = 0;
                for (int i = 0; i < 3; i++)
                {
                    var opts = new List<Opt>();
                    for (int z = 0; z < 5; z++)
                        if (x.D.P(x.Opp).Spells[z] == null && (x.D.Flags.GetValueOrDefault($"blocked{x.Opp}") & (1 << (z + 8))) == 0)
                            opts.Add(new Opt(Act.Option, Arg: z, Text: $"Opponent's Spell & Trap Zone {z + 1}"));
                    if (opts.Count == 0) break;
                    var o = await x.D.Choose(x.Me, Ask.Choose, opts, "Seismic Shockwave: block which zone?", x.Self);
                    picked |= 1 << (o.Arg + 8);
                    x.D.Flags[$"blocked{x.Opp}"] = x.D.Flags.GetValueOrDefault($"blocked{x.Opp}") | (1 << (o.Arg + 8));
                }
                x.D.Flags[$"seismic{x.Self.Uid}"] = picked; x.D.Flags[$"seismicp{x.Self.Uid}"] = x.Opp;
            }, x => x.Ev is { Kind: EvKind.Destroyed, Battle: false, Card: { } c } && c.Loc == Loc.Grave && c.Data.Race == "Dinosaur"
                && x.D.LeftFieldLog.LastOrDefault(l => l.c == c) is { c: not null } l && l.controller == x.Me && l.faceUp);
            s.On = async (x, e) =>
            {
                if (e.Kind == EvKind.LeftField && e.Card == x.Self && x.D.Flags.GetValueOrDefault($"seismic{x.Self.Uid}") is var m && m != 0)
                {
                    int p = x.D.Flags.GetValueOrDefault($"seismicp{x.Self.Uid}");
                    x.D.Flags[$"blocked{p}"] = x.D.Flags.GetValueOrDefault($"blocked{p}") & ~m;
                    x.D.Flags.Remove($"seismic{x.Self.Uid}");
                }
                if (e.Kind == EvKind.Standby && e.Player == x.Me && x.Self.OnField && x.Self.FaceUp && x.Self.Var("stay") == 1 && x.Self.Inc("standbys") >= 3)
                {
                    await x.D.Destroy(x.Self, x.Self);
                    var l = x.Targetable(x.My.Grave.Where(c => c.Data.IsMonster && c.Data.Race == "Dinosaur")).ToList();
                    if (!x.Self.OnField && l.Count > 0 && await x.Ask("Add a Dinosaur from your GY to your hand?")) { var c = await x.Pick(l, "Add which Dinosaur?"); if (c != null) await x.D.ToHand(c, x.Self); }
                }
            };
        }
        S("Serpentine Princess").On = async (x, e) =>
        {
            if (e.Kind != EvKind.LeftField || e.Card != x.Self || x.Self.Loc != Loc.Deck || x.D.LeftFieldLog.LastOrDefault(l => l.c == x.Self) is not { c: not null } l || !l.faceUp || l.controller != x.Me) return;
            await SummonPick(x, x.My.Deck.Where(c => c.Data.IsMonster && c.Data.Level <= 3), "Special Summon a Level 3 or lower monster from your Deck");
            x.D.Shuffle(x.Me);
        };
        {
            var s = Flip("Shadow Tamer", async x =>
            {
                var t = await x.Pick(x.Targetable(x.OppMonsters().FaceUp().Where(m => x.IsRace(m, "Fiend"))), "Take control of which Fiend?");
                if (t == null || !await x.D.TakeControl(t, x.Me)) return;
                var d = x.D; int me = x.Me;
                d.Later(d.Turn, Phase.End, async () => { if (t.Loc == Loc.Monster && t.Controller == me) await d.TakeControl(t, t.Owner); });
            });
            s.AlsoIn = new[] { "Archfiend" };
        }
        Spell("Shadow Toon", async x => { if (x.T0 is { Loc: Loc.Monster, FaceUp: true } t) await x.D.Damage(x.Opp, x.Atk(t), x.Self); },
            x => Hopt(x) && x.My.FieldCards.Any(c => c.FaceUp && c.Name == "Toon World") && x.Targetable(x.OppMonsters().FaceUp()).Any(),
            async x => { UseHopt(x); await x.Target(x.OppMonsters().FaceUp(), "Target which monster?"); });
        S("Shell Knight").On = async (x, e) =>
        {
            if (e.Is(EvKind.Summoned, x.Self) && e.Summoned(SummonKind.Normal, SummonKind.Tribute) && x.Self.Attack && await x.Ask("Change Shell Knight to Defense Position to inflict 500 damage?"))
            { await x.D.ChangePosition(x.Self, true); if (x.Self.Defense) await x.D.Damage(x.Opp, 500, x.Self); }
            bool battle = e.Is(EvKind.Destroyed, x.Self) && e.Battle;
            bool effect = e.Kind == EvKind.ToGrave && e.Card == x.Self && e.From == Loc.Monster && e.Other != null && !x.D.Flags.ContainsKey($"battledead{x.Self.Owner}_{x.Self.Uid}") && !e.Battle;
            if (!(battle || effect) || !Hopt(x)) return;
            bool fossil = x.My.Grave.Any(c => c.Name == "Fossil Fusion");
            var l = x.My.Deck.Where(c => c.Data.IsMonster && c.Data.Race == "Rock" && c.Data.Level == 8).ToList();
            if (l.Count == 0 || !await x.Ask("Use Shell Knight's effect (Level 8 Rock from your Deck)?")) return;
            UseHopt(x);
            var c = await x.Pick(l, fossil ? "Add or Special Summon which Rock?" : "Add which Rock?");
            if (c == null) return;
            if (fossil && x.D.CanSpecialSummon(c, x.Me, by: x.Self) && await x.Ask($"Special Summon {c.Name} instead of adding it?")) await x.D.SpecialSummon(c, x.Me, await x.D.ChoosePosition(x.Me, c), by: x.Self);
            else { await x.D.ToHand(c, x.Self); x.D.Reveal(x.Opp, c); }
            x.D.Shuffle(x.Me);
            var d = x.D; int me = x.Me; string n = c.Name;
            d.Linger(d.Turn, (q, k, p) => q == Q.Activate && k != null && k.Name == n && (p == me || k.Controller == me));
        };
        Spell("Shield Crush", async x => { if (x.T0 is { Loc: Loc.Monster, Defense: true } t) await x.D.Destroy(t, x.Self); },
            x => x.Targetable(x.D.AllMonsters.Where(m => m.Defense)).Any(), async x => await x.Target(x.D.AllMonsters.Where(m => m.Defense), "Destroy which Defense Position monster?"));
        Spell("Shinato's Ark", async x => await RitualSummon(x, m => m.Name == "Shinato, King of a Higher Plane", exact: false),
            x => RitualTargets(x, m => m.Name == "Shinato, King of a Higher Plane", exact: false).Any());
        {
            var s = S("Shinato, King of a Higher Plane");
            s.SummonOk = (x, by, k) => k == SummonKind.Ritual && by?.Name == "Shinato's Ark";
            s.On = async (x, e) => { if (KilledToGrave(x, e) && x.D.Flags.GetValueOrDefault($"lastdef{e.Other!.Uid}") == 1) await x.D.Damage(x.Opp, Math.Max(0, e.Other.Data.Atk), x.Self); };
        }
        Spell("Short Circuit", async x => await x.D.Destroy(x.Their.FieldCards.ToList(), x.Self), x => x.MyMonsters().Count(m => m.FaceUp && m.In("Batteryman")) >= 3);
        Spell("Shrink", async x => { if (x.T0 is { Loc: Loc.Monster, FaceUp: true } t) SetBaseAtk(x, t, Math.Max(0, (t.AsMonster ?? t.Data).Atk) / 2); },
            x => x.Targetable(x.D.FaceUpMonsters).Any(), async x => await x.Target(x.D.FaceUpMonsters, "Halve which monster's original ATK?"));
        Spell("Silent Doom", async x => { if (x.T0 is { Loc: Loc.Grave } t && await x.D.SpecialSummon(t, x.Me, true, by: x.Self)) t.Vars["noattack"] = int.MaxValue; },
            x => x.D.FreeMonsterZones(x.Me) > 0 && x.Targetable(x.My.Grave.Where(c => c.Data.IsNormalMonster && x.D.CanSpecialSummon(c, x.Me, by: x.Self))).Any(),
            async x => await x.Target(x.My.Grave.Where(c => c.Data.IsNormalMonster && x.D.CanSpecialSummon(c, x.Me, by: x.Self)), "Special Summon which Normal Monster?"));
        {
            var s = S("Silent Swordsman LV3");
            s.On = async (x, e) =>
            {
                if (e.Kind == EvKind.Activated && Up(x) && e.Link is { } l && l.Player == x.Opp && l.Card.Data.IsSpell && l.X.T.Contains(x.Self)) { await x.D.Negate(l, destroy: false); return; }
                if (e.Kind == EvKind.Flipped && e.Card == x.Self) x.Self.Vars["flipturn"] = x.D.Turn;
                if (e.Kind == EvKind.Standby && e.Player == x.Me && Up(x) && x.Self.SummonTurn != x.D.Turn && x.Self.Var("flipturn") != x.D.Turn
                    && x.My.Hand.Concat(x.My.Deck).Any(c => c.Name == "Silent Swordsman LV5") && await x.Ask("Send Silent Swordsman LV3 to the GY to Special Summon LV5?"))
                {
                    await x.D.ToGrave(x.Self, x.Self);
                    await SummonPick(x, x.My.Hand.Concat(x.My.Deck).Where(c => c.Name == "Silent Swordsman LV5"), "Special Summon Silent Swordsman LV5");
                    x.D.Shuffle(x.Me);
                }
            };
        }
        {
            var s = S("Silent Swordsman LV5");
            s.Forbid = (x, q, c) => q == Q.AffectedBySpell && c == x.Self && Up(x) && (x.D.Affecting ?? x.D.Targeter) is { } by && by.Controller == x.Opp;
            s.On = async (x, e) =>
            {
                if (e.Kind == EvKind.BattleDamage && e.Card == x.Self && e.Player == x.Opp && e.Other == null) x.Self.Vars["directhit"] = x.D.Turn;
                if (e.Kind == EvKind.Standby && e.Player == x.Me && Up(x) && x.Self.Var("directhit") > 0 && x.Self.Var("directhit") < x.D.Turn
                    && x.My.Hand.Concat(x.My.Deck).Any(c => c.Name == "Silent Swordsman LV7") && await x.Ask("Send Silent Swordsman LV5 to the GY to Special Summon LV7?"))
                {
                    x.Self.Vars["directhit"] = 0;
                    await x.D.ToGrave(x.Self, x.Self);
                    await SummonPick(x, x.My.Hand.Concat(x.My.Deck).Where(c => c.Name == "Silent Swordsman LV7"), "Special Summon Silent Swordsman LV7");
                    x.D.Shuffle(x.Me);
                }
            };
            s.Note = "It can level up in the Standby Phase after any of its direct attacks dealt damage.";
        }
        {
            var s = S("Silent Swordsman LV7");
            s.SummonOk = (x, by, k) => by?.Name == "Silent Swordsman LV5";
            s.Forbid = (x, q, c) => q == Q.Negate && Up(x) && c != null && c.Data.IsSpell && c.OnField;
        }
        {
            var s = S("Simorgh, Bird of Divinity");
            s.SummonOk = (x, by, k) => false;
            s.TributeOk = (x, t) => t.FaceUp ? x.IsAttr(t, "WIND") : t.Data.Attribute == "WIND";
            s.On = async (x, e) =>
            {
                if (e.Kind != EvKind.EndPhase || !Up(x)) return;
                int p = e.Player, dmg = 1000 - 500 * x.D.P(p).SpellsOnField.Count();
                if (dmg > 0) await x.D.Damage(p, dmg, x.Self);
            };
        }
        {
            var s = S("Singularity Fiend");
            s.AddFx(f =>
            {
                f.Label = "Discard: destroy the summoned monster(s)"; f.FromHand = true; f.Speed = 2; f.AnyTurn = true;
                f.Can = x => Hopt(x) && x.Ev is { Kind: EvKind.Summoned, Card: { Loc: Loc.Monster } c } e && e.Player == x.Opp && e.Arg is (int)SummonKind.Special or (int)SummonKind.Fusion or (int)SummonKind.Ritual
                    && x.My.Hand.Any(h => h.Data.IsSpell);
                f.Cost = async x =>
                {
                    UseHopt(x);
                    x.T.Add(x.Ev!.Card!);
                    var sp = await x.PickN(x.My.Hand.Where(h => h.Data.IsSpell), 1, 1, "Discard 1 Spell");
                    await x.D.Discard(x.Me, sp.Prepend(x.Self).ToList(), x.Self);
                };
                f.Run = async x => { foreach (var t in x.T.Where(t => t.Loc == Loc.Monster).ToList()) await x.D.Destroy(t, x.Self); };
            });
        }
        Spell("Sinister Seeds", async x =>
        {
            int n = x.D.Flags.GetValueOrDefault($"lastbdmg{x.Me}") / 500;
            for (int i = 0; i < n && x.D.FreeMonsterZones(x.Me) > 0; i++)
            { var t = x.D.MakeToken("Sinister Seed Token", x.Me, 100, 100, "DARK", "Plant", 1); await x.D.SpecialSummon(t, x.Me, await x.D.ChoosePosition(x.Me, t), by: x.Self); }
        }, x => x.Ev is { Kind: EvKind.Destroyed, Battle: true, Card: { } c } && x.D.LeftFieldLog.LastOrDefault(l => l.c == c) is { c: not null } l && l.controller == x.Me && l.faceUp
            && x.D.Flags.GetValueOrDefault($"lastdef{c.Uid}") == 0 && x.D.Flags.GetValueOrDefault($"lastbdmg{x.Me}") >= 500 && x.D.Flags.GetValueOrDefault($"lastbdmgturn{x.Me}") == x.D.Turn);
        {
            var s = Spell("Skill Drain", async x => x.Self.Vars["stay"] = 1, x => x.My.Lp > 1000, async x => x.D.Pay(x.Me, 1000, x.Self));
            s.Forbid = (x, q, c) => q == Q.Negate && x.Self.OnField && x.Self.FaceUp && c != null && c.Loc == Loc.Monster && c.FaceUp;
        }
        {
            var s = S("Skilled Dark Magician");
            SpellCounters(s);
            s.Can = x => Up(x) && x.Self.Counters("spell") >= 3 && !x.D.Forbidden(Q.Tribute, x.Self) && x.My.Hand.Concat(x.My.Deck).Concat(x.My.Grave).Any(c => c.Name == "Dark Magician" && x.D.CanSpecialSummon(c, x.Me, by: x.Self, ignoreZones: true));
            s.Cost = async x => await x.D.Tribute(x.Self, x.Self);
            s.Run = async x => { await SummonPick(x, x.My.Hand.Concat(x.My.Deck).Concat(x.My.Grave).Where(c => c.Name == "Dark Magician"), "Special Summon Dark Magician"); x.D.Shuffle(x.Me); };
        }
        {
            var s = S("Skull Archfiend of Lightning");
            s.On = async (x, e) =>
            {
                if (e.Kind == EvKind.Standby && e.Player == x.Me && Up(x) && !(x.Self.In("Archfiend") && x.D.CountActive("Pandemonium") > 0))
                { if (x.My.Lp > 500) x.D.Pay(x.Me, 500, x.Self); else await x.D.Destroy(x.Self, x.Self); }
                if (e.Kind == EvKind.Activated && Up(x) && e.Link is { } l && l.Player == x.Opp && l.X.T.Contains(x.Self) && x.D.Die(x.Me, x.Self) is 1 or 3 or 6) await x.D.Negate(l);
            };
            s.Note = "Its die is rolled when it is targeted (not when the effect resolves).";
        }
        S("Skull-Mark Ladybug").On = async (x, e) => { if (e.Kind == EvKind.ToGrave && e.Card == x.Self) await x.D.Gain(x.Self.Owner == x.Me ? x.Me : x.Self.Owner, 1000, x.Self); };
        S("Skyscraper").CalcAtk = (x, c, o, atk) => x.Self.FaceUp && x.Self.Loc == Loc.Field && c == x.D.Attacker && o != null && c.In("Elemental HERO") && x.Atk(o) > x.Atk(c) ? atk + 1000 : atk;
        {
            var s = S("Skyscraper 2 - Hero City");
            static IEnumerable<Card> Ok(Ctx x) => x.My.Grave.Where(c => EHero(c) && x.D.Flags.ContainsKey($"battledead{c.Owner}_{c.Uid}") && x.D.CanSpecialSummon(c, x.Me, by: x.Self));
            s.OncePerTurn = true;
            s.Can = x => x.Self.FaceUp && x.D.FreeMonsterZones(x.Me) > 0 && x.Targetable(Ok(x)).Any();
            s.Cost = async x => await x.Target(Ok(x), "Special Summon which Elemental HERO?");
            s.Run = async x => { if (x.T0 is { Loc: Loc.Grave } t) await x.D.SpecialSummon(t, x.Me, await x.D.ChoosePosition(x.Me, t), by: x.Self); };
        }
        Spell("Slash Draw", async x =>
        {
            int n = Math.Min(x.My.Deck.Count, x.Their.FieldCards.Count());
            for (int i = 0; i < n; i++) await x.D.ToGrave(x.My.Deck[0], x.Self);
            if (x.My.Deck.Count == 0) return;
            await x.D.Draw(x.Me, 1);
            var c = x.My.Hand.Last();
            x.D.Reveal(x.Opp, c);
            if (c.Name == "Slash Draw")
            {
                await x.D.ToGrave(c, x.Self);
                if (c.Loc != Loc.Grave) return;
                var l = x.D.FieldCards.Where(f => f != x.Self).ToList();
                await x.D.Destroy(l, x.Self);
                int k = l.Count(f => f.Loc == Loc.Grave);
                if (k > 0) await x.D.Damage(x.Opp, 2000 * k, x.Self);
            }
            else if (n > 0)
            {
                foreach (var g in await x.PickN(x.My.Grave.Where(g => g != x.Self), Math.Min(n, x.My.Grave.Count(g => g != x.Self)), Math.Min(n, x.My.Grave.Count(g => g != x.Self)), $"Shuffle {n} cards from your GY into your Deck"))
                    await x.D.ToDeck(g);
                x.D.Shuffle(x.Me);
            }
        }, x => x.My.Hand.Any(c => c != x.Self), async x => await x.D.Discard(x.Me, await x.PickN(x.My.Hand.Where(c => c != x.Self), 1, 1, "Discard 1 card"), x.Self));
        Spell("Snake Deity's Command", async x => await x.D.Negate(x.Ev!.Link!),
            x => x.Ev is { Kind: EvKind.Activated, Link: { IsCardActivation: true } l } && l.Player == x.Opp && l.Card.Data.IsSpell && x.My.Hand.Any(c => c.Data.IsMonster && c.In("Venom")),
            async x => { var v = await x.Pick(x.My.Hand.Where(c => c.Data.IsMonster && c.In("Venom")), "Show which Venom monster?"); if (v != null) x.D.Reveal(x.Opp, v); });
        Spell("Snake Rain", async x =>
        {
            var l = x.My.Deck.Where(c => c.Data.IsMonster && c.Data.Race == "Reptile").ToList();
            foreach (var c in await x.PickN(l, Math.Min(4, l.Count), Math.Min(4, l.Count), "Send which 4 Reptiles to the GY?")) await x.D.ToGrave(c, x.Self);
            x.D.Shuffle(x.Me);
        }, x => x.My.Hand.Any(c => c != x.Self) && x.My.Deck.Count(c => c.Data.IsMonster && c.Data.Race == "Reptile") >= 4,
        async x => await x.D.Discard(x.Me, await x.PickN(x.My.Hand.Where(c => c != x.Self), 1, 1, "Discard 1 card"), x.Self));
        Spell("Snake Whistle", async x => { await SummonPick(x, x.My.Deck.Where(c => c.Data.IsMonster && c.Data.Race == "Reptile" && c.Data.Level <= 4), "Special Summon a Reptile"); x.D.Shuffle(x.Me); },
            x => x.Ev is { Kind: EvKind.Destroyed, Card: { } c } && (c.AsMonster ?? c.Data).Race == "Reptile" && x.D.LeftFieldLog.LastOrDefault(l => l.c == c) is { c: not null } l && l.controller == x.Me
                && x.D.FreeMonsterZones(x.Me) > 0 && x.My.Deck.Any(c => c.Data.IsMonster && c.Data.Race == "Reptile" && c.Data.Level <= 4));
        {
            var s = Spell("Soul Absorption", async x => x.Self.Vars["stay"] = 1);
            s.On = async (x, e) => { if (e.Kind == EvKind.Banished && x.Self.OnField && x.Self.FaceUp && e.Card != x.Self) await x.D.Gain(x.Me, 500, x.Self); };
        }
        Spell("Soul Charge", async x =>
        {
            int n = 0;
            foreach (var t in x.T.Where(t => t.Loc == Loc.Grave).ToList())
                if (x.D.FreeMonsterZones(x.Me) > 0 && await x.D.SpecialSummon(t, x.Me, await x.D.ChoosePosition(x.Me, t), by: x.Self)) n++;
            if (n > 0) x.D.Pay(x.Me, 1000 * n, x.Self);
        }, x => Hopt(x) && x.D.Phase != Phase.Main2 && x.D.FreeMonsterZones(x.Me) > 0 && x.Targetable(x.My.Grave.Where(c => c.Data.IsMonster && x.D.CanSpecialSummon(c, x.Me, by: x.Self))).Any(),
        async x =>
        {
            UseHopt(x); x.D.TurnFlags.Add("nobattle");
            var l = x.Targetable(x.My.Grave.Where(c => c.Data.IsMonster && x.D.CanSpecialSummon(c, x.Me, by: x.Self))).ToList();
            x.T.AddRange(await x.PickN(l, 1, Math.Min(l.Count, x.D.FreeMonsterZones(x.Me)), "Target which monsters?"));
        });
        {
            var s = Spell("Soul Demolition", async x => x.Self.Vars["stay"] = 1);
            s.AddFx(f =>
            {
                f.Label = "Pay 500: each banishes from the other's GY"; f.InSpellZone = true; f.Speed = 2; f.AnyTurn = true;
                f.Can = x => x.Self.FaceUp && x.My.Lp > 500 && x.MyMonsters().Any(m => m.FaceUp && x.IsRace(m, "Fiend")) && (x.Their.Grave.Any(c => c.Data.IsMonster) || x.My.Grave.Any(c => c.Data.IsMonster));
                f.Cost = async x => x.D.Pay(x.Me, 500, x.Self);
                f.Run = async x =>
                {
                    var a = await x.Pick(x.Their.Grave.Where(c => c.Data.IsMonster), "Banish 1 monster from your opponent's GY");
                    var b = await x.D.PickOne(x.Opp, x.My.Grave.Where(c => c.Data.IsMonster), "Banish 1 monster from your opponent's GY", x.Self);
                    if (a != null) await x.D.Banish(a, x.Self);
                    if (b != null) await x.D.Banish(b, x.Self);
                };
            });
        }
        Spell("Soul of Fire", async x =>
        {
            await x.D.Draw(x.Opp, 1);
            var c = await x.Pick(x.My.Deck.Where(c => c.Data.IsMonster && c.Data.Race == "Pyro"), "Banish which Pyro monster?");
            x.D.Shuffle(x.Me);
            if (c != null) { await x.D.Banish(c, x.Self); await x.D.Damage(x.Opp, Math.Max(0, c.Data.Atk) / 2, x.Self); }
            var d = x.D; int me = x.Me;
            d.Linger(d.Turn, (q, k, p) => q == Q.Attack && k != null && k.Controller == me);
        }, x => x.My.Deck.Any(c => c.Data.IsMonster && c.Data.Race == "Pyro"));
        {
            var s = Spell("Soul Resurrection", async x =>
            {
                if (x.T0 is not { Loc: Loc.Grave } t || !x.Self.OnField) return;
                if (await x.D.SpecialSummon(t, x.Me, true, by: x.Self)) { Tie(x, t); x.Self.Vars["stay"] = 1; }
            }, x => x.D.FreeMonsterZones(x.Me) > 0 && x.Targetable(x.My.Grave.Where(c => c.Data.IsNormalMonster && x.D.CanSpecialSummon(c, x.Me, by: x.Self))).Any(),
            async x => await x.Target(x.My.Grave.Where(c => c.Data.IsNormalMonster && x.D.CanSpecialSummon(c, x.Me, by: x.Self)), "Special Summon which Normal Monster?"));
            s.On = async (x, e) =>
            {
                var t = TiedMonster(x);
                if (e.Kind == EvKind.LeftField && e.Card == x.Self && t is { Loc: Loc.Monster }) { await x.D.Destroy(t, x.Self); return; }
                if (x.Self.OnField && t != null && e.Is(EvKind.Destroyed, t)) await x.D.Destroy(x.Self, x.Self);
            };
        }
        Spell("Soul Rope", async x => { await SummonPick(x, x.My.Deck.Where(c => c.Data.IsMonster && c.Data.Level == 4), "Special Summon a Level 4 monster"); x.D.Shuffle(x.Me); },
            x => x.My.Lp > 1000 && x.Ev is { Kind: EvKind.Destroyed, Battle: false, Card: { } c } && c.Loc == Loc.Grave && x.D.LeftFieldLog.LastOrDefault(l => l.c == c) is { c: not null } l && l.controller == x.Me
                && x.D.FreeMonsterZones(x.Me) > 0 && x.My.Deck.Any(c => c.Data.IsMonster && c.Data.Level == 4),
            async x => x.D.Pay(x.Me, 1000, x.Self));
        Spell("Soul Taker", async x => { if (x.T0 is { Loc: Loc.Monster } t) { await x.D.Destroy(t, x.Self); await x.D.Gain(x.Opp, 1000, x.Self); } },
            x => x.Targetable(x.OppMonsters().FaceUp()).Any(), async x => await x.Target(x.OppMonsters().FaceUp(), "Destroy which monster?"));
        {
            static IEnumerable<Card> GyPool(Ctx x) => x.My.Grave.Where(c => c.Data.IsMonster);
            Spell("Soul Union", async x =>
            {
                if (x.T.Count < 2) return;
                var (t, h) = (x.T[0], x.T[1]);
                if (t is { Loc: Loc.Monster, FaceUp: true }) x.D.Boost(t, Math.Max(0, h.Data.Atk));
                if (!x.My.FieldCards.Concat(x.My.Grave).Any(c => c.Data.IsNormalMonster && c.In("Elemental HERO"))) return;
                if (!x.D.FusionOptions(x.Me, pool: GyPool(x), resultOk: f => f.In("Elemental HERO"), by: x.Self).Any() || !await x.Ask("Fusion Summon an Elemental HERO by banishing materials from your GY?")) return;
                await x.D.FusionSummon(x.Me, x.Self, banishMats: true, pool: GyPool(x), resultOk: f => f.In("Elemental HERO"));
            }, x => Hopt(x) && x.Targetable(x.D.FaceUpMonsters).Any() && x.Targetable(x.My.Grave.Where(EHero)).Any(),
            async x =>
            {
                UseHopt(x);
                await x.Target(x.D.FaceUpMonsters, "Which monster gains ATK?");
                var h = await x.Pick(x.Targetable(x.My.Grave.Where(EHero)), "Target an Elemental HERO in your GY");
                if (h != null) x.T.Add(h);
            });
        }
        Spell("Space Gift", async x => await x.D.Draw(x.Me, x.MyMonsters().Where(m => m.FaceUp && m.In("Neo-Spacian")).Select(m => Scripts.NameOf(m)).Distinct().Count()),
            x => x.MyMonsters().Any(m => m.FaceUp && m.In("Neo-Spacian")));
        {
            static IEnumerable<Card> Dinos(Ctx x) => x.My.Grave.Where(c => c.Data.IsMonster && c.Data.Race == "Dinosaur");
            static IEnumerable<Card> Targets(Ctx x, IEnumerable<Card> banished) => x.My.Hand.Concat(x.My.Grave).Where(c => c.Data.IsMonster && c.Data.Race == "Dinosaur" && !banished.Contains(c) && x.D.CanSpecialSummon(c, x.Me, by: x.Self));
            Spell("Spacetime Transcendence", async x =>
            {
                int lv = x.Self.Var("stlv");
                var c = await x.Pick(Targets(x, Array.Empty<Card>()).Where(c => c.Data.Level == lv), $"Special Summon a Level {lv} Dinosaur");
                if (c != null && await x.D.SpecialSummon(c, x.Me, await x.D.ChoosePosition(x.Me, c), by: x.Self)) c.Vars["noattack"] = x.D.Turn;
            }, x => Dinos(x).Count() >= 2 && x.D.FreeMonsterZones(x.Me) > 0 && Targets(x, Array.Empty<Card>()).Any(t => SubsetSumN(Dinos(x).Where(d => d != t).Select(d => d.Data.Level).ToList(), t.Data.Level, 2)),
            async x =>
            {
                var target = await x.Pick(Targets(x, Array.Empty<Card>()).Where(t => SubsetSumN(Dinos(x).Where(d => d != t).Select(d => d.Data.Level).ToList(), t.Data.Level, 2)), "Special Summon which Dinosaur (choose first)?");
                if (target == null) return;
                x.Self.Vars["stlv"] = target.Data.Level;
                var used = await PickLevels(x, Dinos(x).Where(d => d != target).ToList(), target.Data.Level, "Banish Dinosaurs whose Levels add up");
                foreach (var c in used) await x.D.Banish(c, x.Self);
            });
        }
        {
            var s = Spell("Spark Blaster", async x => { if (x.T0 is { } t && t.Valid() && t.FaceUp) await x.D.Equip(x.Self, t); },
                x => x.Targetable(x.D.FaceUpMonsters.Where(m => Scripts.NameOf(m) == "Elemental HERO Sparkman")).Any(),
                async x => await x.Target(x.D.FaceUpMonsters.Where(m => Scripts.NameOf(m) == "Elemental HERO Sparkman"), "Equip to which Sparkman?"));
            s.EquipOk = (x, m) => Scripts.NameOf(m) == "Elemental HERO Sparkman";
            s.AddFx(f =>
            {
                f.Label = "Change a monster's position"; f.InSpellZone = true;
                f.Can = x => x.Self.FaceUp && x.Self.EquippedTo != null && x.D.TurnPlayer == x.Me && x.Targetable(x.D.FaceUpMonsters).Any();
                f.Cost = async x => await x.Target(x.D.FaceUpMonsters, "Change which monster's battle position?");
                f.Run = async x =>
                {
                    if (x.T0 is { Loc: Loc.Monster, FaceUp: true } t) await x.D.ChangePosition(t, !t.Defense);
                    if (x.Self.OnField && x.Self.Inc("sparkuses") >= 3) await x.D.Destroy(x.Self, x.Self);
                };
            });
        }
        {
            var s = S("Spear Dragon");
            s.Allow = (x, q, c) => q == Q.Piercing && c == x.Self;
            s.On = async (x, e) => { if (e.Kind == EvKind.AfterDamage && e.Card == x.Self && Up(x) && x.Self.Attack) await x.D.ChangePosition(x.Self, true); };
        }
        Spell("Special Hurricane", async x => await x.D.Destroy(x.D.AllMonsters.Where(m => m.Summoned is SummonKind.Special or SummonKind.Fusion or SummonKind.Ritual).ToList(), x.Self),
            x => x.My.Hand.Any(c => c != x.Self) && x.D.AllMonsters.Any(m => m.Summoned is SummonKind.Special or SummonKind.Fusion or SummonKind.Ritual), async x => await x.D.Discard(x.Me, await x.PickN(x.My.Hand.Where(c => c != x.Self), 1, 1, "Discard 1 card"), x.Self));
        Sure("Scarr, Scout of Dark World", "Shadow Toon", "Shield Crush", "Shinato's Ark", "Shinato, King of a Higher Plane", "Short Circuit", "Shrink", "Silent Doom", "Silent Swordsman LV7",
            "Skull-Mark Ladybug", "Snake Deity's Command", "Snake Rain", "Snake Whistle", "Soul Absorption", "Soul of Fire", "Soul Rope", "Soul Taker", "Space Gift", "Spear Dragon", "Special Hurricane", "Skill Drain");
    }
}
