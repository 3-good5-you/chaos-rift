namespace PoC.Engine;

// Official cards, batch 11 (Enraged Muka Muka .. Fog King).
public static partial class Scripts
{
    /// "Must be Special Summoned with Dark Fusion" (Dark Calling counts as Dark Fusion).
    static Script EvilHero(string name)
    {
        var s = S(name);
        s.SummonOk = (x, by, k) => k == SummonKind.Fusion && by?.Name is "Dark Fusion" or "Dark Calling";
        return s;
    }

    static void CardsP11(CardDb db)
    {
        {
            var s = S("Enraged Muka Muka");
            s.Stats = (x, c, st) => { if (c == x.Self) st.Add(400 * x.My.Hand.Count, 400 * x.My.Hand.Count); };
        }
        {
            var s = Flip("Eria the Water Charmer", async x =>
            {
                var t = await x.Pick(x.Targetable(x.OppMonsters().FaceUp().Where(m => x.IsAttr(m, "WATER"))), "Take control of which WATER monster?");
                if (t != null && await x.D.TakeControl(t, x.Me)) Tie(x, t);
            });
            var flip = s.On!;
            s.On = async (x, e) =>
            {
                await flip(x, e);
                if (TiedMonster(x) is { Loc: Loc.Monster } t && t.Controller == x.Me && !(x.Self.Loc == Loc.Monster && x.Self.FaceUp))
                { x.D.Flags.Remove($"tie{x.Self.Uid}"); await x.D.TakeControl(t, t.Owner); }
            };
        }
        Spell("Eternal Dread", async x => { foreach (var c in x.D.FieldCards.Where(c => c.FaceUp && c.Name == "Clock Tower Prison")) AddCounter(x, c, 2, "clock"); }, x => x.D.FieldCards.Any(c => c.FaceUp && c.Name == "Clock Tower Prison"));
        S("Etoile Cyber").CalcAtk = (x, c, vs, atk) => c == x.Self && x.D.Attacker == x.Self && vs == null ? atk + 500 : atk;
        {
            var s = S("Eucalyptus Mole");
            s.On = async (x, e) =>
            {
                if (e.Is(EvKind.Summoned, x.Self) && e.Arg != (int)SummonKind.Flip && Hopt(x, "1") && x.My.Deck.Any(c => c.Data.IsMonster && c.Data.Race == "Beast" && c.Data.Level <= 4)
                    && x.D.FreeMonsterZones(x.Me) > 0 && await x.Ask("Special Summon a Level 4 or lower Beast from your Deck face-down?"))
                {
                    UseHopt(x, "1");
                    var b = await x.Pick(x.My.Deck.Where(c => c.Data.IsMonster && c.Data.Race == "Beast" && c.Data.Level <= 4), "Special Summon which Beast (face-down Defense)?");
                    x.D.Shuffle(x.Me);
                    if (b != null && await x.D.SpecialSummon(b, x.Me, true, faceUp: false, by: x.Self) && x.Self.Valid() && x.Self.Attack) await x.D.ChangePosition(x.Self, true);
                }
                if (e.Is(EvKind.Destroyed, x.Self) && Hopt(x, "2") && x.My.Deck.Any(c => c.Data.IsMonster && (c.In("Koala") || c.Name == "Tree Otter")) && await x.Ask("Special Summon a Koala monster or Tree Otter from your Deck?"))
                {
                    UseHopt(x, "2");
                    if (await SummonPick(x, x.My.Deck.Where(c => c.Data.IsMonster && (c.In("Koala") || c.Name == "Tree Otter")), "Special Summon which?"))
                        x.D.Flags[$"eucalyptus{x.Me}"] = x.D.Turn + 1;
                    x.D.Shuffle(x.Me);
                }
            };
            s.AlwaysActive = true;
            s.Stats = (x, c, st) =>
            {
                // "Non-Effect Beast monsters you control gain ATK equal to their original DEF" until the end of the next turn (applied from wherever Mole is).
                if (x.D.Flags.GetValueOrDefault($"eucalyptus{x.Me}") >= x.D.Turn && c.Controller == x.Me && !c.Data.IsEffect && st.Race == "Beast") st.Add(c.Data.Def, 0);
            };
            s.Note = "The ATK bonus after it is destroyed applies while Eucalyptus Mole's script can still see the field; tell me if it drops off too early.";
        }
        {
            var s = Spell("Evil Blast", async x =>
            {
                if (x.T0 is not { } m || !m.Valid()) return;
                await x.D.Equip(x.Self, m);
                x.Self.Vars["stay"] = 1;
            }, x => x.Ev is { Kind: EvKind.Summoned } e && e.Player == x.Opp && e.Arg is (int)SummonKind.Special or (int)SummonKind.Fusion or (int)SummonKind.Ritual && e.Card!.Valid(),
            async x => x.T.Add(x.Ev!.Card!));
            s.Stats = (x, c, st) => { if (c == x.Self.EquippedTo) st.Add(500, 0); };
            s.On = async (x, e) => { if (e.Kind == EvKind.Standby && e.Player == x.Opp && x.Self.EquippedTo != null) await x.D.Damage(x.Opp, 500, x.Self); };
        }
        {
            var s = EvilHero("Evil HERO Dark Gaia");
            s.Materials = new() { Cond("1 Fiend monster", c => c.Data.IsMonster && c.Data.Race == "Fiend"), Cond("1 Rock monster", c => c.Data.IsMonster && c.Data.Race == "Rock") };
            s.Base = (x, c, st) => { if (c == x.Self) st.BaseAtk = Enumerable.Range(0, 3).Select(i => x.Self.Var($"mat{i}")).Where(u => u > 0).Sum(u => Math.Max(0, x.D.CardByUid(u - 1).Data.Atk)); };
            s.On = async (x, e) =>
            {
                if (e.Kind == EvKind.AttackDeclared && e.Card == x.Self && x.OppMonsters().Any(m => m.Defense) && await x.Ask("Change all your opponent's Defense Position monsters to face-up Attack Position?"))
                    foreach (var m in x.OppMonsters().Where(m => m.Defense).ToList()) await x.D.ChangePosition(m, false, true, flipEffect: false);
            };
        }
        {
            var s = S("Evil HERO Infernal Gainer");
            s.AddFx(f =>
            {
                f.Label = "Another Fiend attacks twice";
                f.Can = x => x.D.Phase == Phase.Main1 && x.Targetable(x.MyMonsters().FaceUp().Where(m => m != x.Self && x.IsRace(m, "Fiend"))).Any();
                f.Cost = async x =>
                {
                    await x.D.Banish(x.Self, x.Self);
                    x.Self.Vars["gainerturn"] = x.D.Turn;
                    await x.Target(x.MyMonsters().FaceUp().Where(m => x.IsRace(m, "Fiend")), "Which Fiend can attack twice?");
                };
                f.Run = async x => { if (x.T0 is { } t && t.Valid() && t.FaceUp) t.Vars["twice"] = 1; };
            });
            s.On = async (x, e) =>
            {
                if (e.Kind == EvKind.Standby && e.Player == x.Me && x.Self.Loc == Loc.Banished && x.Self.Var("gainerturn") > 0)
                {
                    int n = x.Self.Inc("gainerstandby");
                    if (n == 2) { x.Self.Vars["gainerturn"] = 0; x.Self.Vars["gainerstandby"] = 0; if (x.D.CanSpecialSummon(x.Self, x.Me, by: x.Self)) await x.D.SpecialSummon(x.Self, x.Me, false, by: x.Self); }
                }
            };
            s.Note = "The Fiend keeps its second attack while it stays face-up on your field.";
        }
        {
            var s = S("Evil HERO Infernal Prodigy");
            s.ProcCan = x => !x.MyMonsters().Any();
            s.Proc = async x => await x.D.SpecialSummon(x.Self, x.Me, false, by: x.Self);
            s.On = async (x, e) =>
            {
                if (e.Kind == EvKind.Summoned && e.Arg == (int)SummonKind.Tribute && e.Player == x.Me && e.Card!.In("HERO") && x.D.LastTributes.Contains(x.Self)) x.Self.Vars["prodigyturn"] = x.D.Turn;
                if (e.Kind == EvKind.EndPhase && x.Self.Var("prodigyturn") == x.D.Turn) { x.Self.Vars["prodigyturn"] = 0; await x.D.Draw(x.Me, 1); }
            };
        }
        {
            var s = EvilHero("Evil HERO Infernal Sniper");
            s.Forbid = (x, q, c) => q == Q.DestroyByEffect && c == x.Self && x.D.Destroyer is { Data.IsSpell: true };
            s.On = async (x, e) => { if (e.Kind == EvKind.Standby && e.Player == x.Me && Up(x) && x.Self.Defense) await x.D.Damage(x.Opp, 1000, x.Self); };
        }
        {
            var s = EvilHero("Evil HERO Inferno Wing");
            s.Allow = (x, q, c) => q == Q.Piercing && c == x.Self;
            s.On = async (x, e) => { if (KilledToGrave(x, e)) await x.D.Damage(x.Opp, Math.Max(e.Other!.Data.Atk, e.Other.Data.Def), x.Self); };
        }
        {
            var s = EvilHero("Evil HERO Lightning Golem");
            s.OncePerTurn = true;
            s.Can = x => Up(x) && x.Targetable(x.AllMonsters()).Any();
            s.Cost = async x => await x.Target(x.AllMonsters(), "Destroy which monster?");
            s.Run = async x => { if (x.T0 is { } t && t.Valid()) await x.D.Destroy(t, x.Self); };
        }
        {
            var s = S("Evil HERO Malicious Edge");
            s.TributesNeeded = x => x.OppMonsters().Any() ? 1 : 2;
            s.Allow = (x, q, c) => q == Q.Piercing && c == x.Self;
            s.Note = "The 1-Tribute Summon also applies to Setting it (the engine doesn't separate the two here).";
        }
        {
            var s = EvilHero("Evil HERO Malicious Fiend");
            s.Materials = new() { Named("Evil HERO Malicious Edge"), Cond("1 Level 6 or higher Fiend monster", c => c.Data.IsMonster && c.Data.Race == "Fiend" && c.Data.Level >= 6) };
            s.On = async (x, e) =>
            {
                if (e.Kind == EvKind.BattleStart && e.Player == x.Opp && Up(x))
                {
                    foreach (var m in x.OppMonsters().Where(m => m.Defense || !m.FaceUp).ToList()) await x.D.ChangePosition(m, false, true, flipEffect: false);
                    x.D.Flags[$"staunch{x.Opp}"] = x.Self.Uid + 1; x.D.Flags[$"staunchturn{x.Opp}"] = x.D.Turn;
                }
            };
        }
        {
            var s = EvilHero("Evil HERO Wild Cyclone");
            s.Forbid = LocksDuringAttack;
            s.On = async (x, e) => { if (e.Kind == EvKind.BattleDamage && e.Card == x.Self && e.Player == x.Opp) await x.D.Destroy(x.Spells(x.Opp).Where(c => !c.FaceUp).ToList(), x.Self); };
        }
        Spell("Exchange of the Spirit", async x =>
        {
            foreach (var p in x.D.Players)
            {
                var g = p.Grave.ToList(); var d = p.Deck.ToList();
                foreach (var c in d) await x.D.ToGrave(c, x.Self);
                foreach (var c in g) await x.D.ToDeck(c);
                x.D.Shuffle(p.Index);
            }
        }, x => x.D.Flags.GetValueOrDefault($"exchange{x.Me}") == 0 && x.My.Grave.Count >= 15 && x.Their.Grave.Count >= 15 && x.My.Lp > 1000,
        async x => { x.D.Flags[$"exchange{x.Me}"] = 1; x.D.Pay(x.Me, 1000); });
        {
            var s = S("Exodia Necross");
            s.SummonOk = (x, by, k) => by?.Name == "Contract with Exodia";
            s.Forbid = (x, q, c) => c == x.Self && (q == Q.DestroyByBattle || (q == Q.DestroyByEffect && x.D.Destroyer is { } by && !by.Data.IsMonster));
            s.On = async (x, e) =>
            {
                if (!Up(x)) return;
                if (!Duel.Exodia.All(n => x.My.Grave.Any(c => c.Name == n))) { await x.D.Destroy(x.Self, x.Self); return; }
                if (e.Kind == EvKind.Standby && e.Player == x.Me) { x.Self.Vars["modatk"] = x.Self.Var("modatk") + 500; x.D.Seq++; }
            };
        }
        {
            var s = S("Exodius the Ultimate Forbidden Lord");
            s.SummonOk = (x, by, k) => by == x.Self;
            s.LeaveToBanish = true;
            s.Proc = async x =>
            {
                foreach (var c in x.My.Grave.Where(c => c.Data.IsMonster).ToList()) await x.D.ToDeck(c);
                x.D.Shuffle(x.Me);
                await x.D.SpecialSummon(x.Self, x.Me, await x.D.ChoosePosition(x.Me, x.Self), by: x.Self);
            };
            s.Stats = (x, c, st) => { if (c == x.Self) st.Add(1000 * x.My.Grave.Count(g => g.Data.IsNormalMonster), 0); };
            s.On = async (x, e) =>
            {
                if (e.Kind != EvKind.AttackDeclared || e.Card != x.Self) return;
                var c = await x.Pick(x.My.Hand.Concat(x.My.Deck).Where(c => c.Data.IsMonster), "Send 1 monster from your hand or Deck to the GY");
                if (c == null) return;
                await x.D.ToGrave(c, x.Self);
                x.D.Shuffle(x.Me);
                if (c.Loc == Loc.Grave && c.In("Forbidden One")) x.D.Flags[$"exodius{x.Me}:{c.Name}"] = 1;
                if (x.My.Grave.Where(g => g.In("Forbidden One") && x.D.Flags.ContainsKey($"exodius{x.Me}:{g.Name}")).Select(g => g.Name).Distinct().Count() >= 5) x.D.Win(x.Me, "Exodius");
            };
        }
        {
            var s = S("Exploder Dragon");
            s.BattleDamage = (x, src, to, dmg) => x.D.Attacker == x.Self ? 0 : dmg;
            s.On = async (x, e) => { if (e.Is(EvKind.Destroyed, x.Self) && e.Battle && x.Self.Loc == Loc.Grave && e.Other is { } by && by.Valid()) await x.D.Destroy(by, x.Self); };
        }
        S("Expressroid").On = async (x, e) =>
        {
            var l = x.Targetable(x.My.Grave.Where(c => c.Data.IsMonster && c.In("roid") && c.Name != "Expressroid")).ToList();
            if (e.Is(EvKind.Summoned, x.Self) && l.Count >= 2 && await x.Ask("Add 2 \"roid\" monsters from your GY to your hand?"))
                foreach (var c in await x.PickN(l, 2, 2, "Add which 2?")) await x.D.ToHand(c, x.Self);
        };
        {
            static bool Mine(Ctx x, Card m) => x.IsRace(m, "Illusion") || x.IsRace(m, "Spellcaster");
            Spell("Eye of Illusion", async x =>
            {
                var d = x.D; int me = x.Me;
                if (x.N == 0) { d.Linger(d.Turn, (q, c, p) => q == Q.DestroyByBattle && c != null && c.Controller == me && c.FaceUp && (d.Race(c) is "Illusion" or "Spellcaster")); return; }
                if (x.T0 is not { } t || !t.Valid() || !t.FaceUp) return;
                if (x.N == 1) { if (await d.TakeControl(t, me)) d.Later(d.Turn, Phase.End, async () => { if (t.Valid() && t.Controller == me) await d.TakeControl(t, t.Owner); }); return; }
                if (d.Attacker != null) { d.AttackTarget = t; d.BattleFlags.Add("redirectself"); }
            }, x => x.MyMonsters().Any(m => m.FaceUp && Mine(x, m)),
            async x =>
            {
                var opts = new List<string> { "Your Illusion and Spellcaster monsters can't be destroyed by battle this turn" };
                if (!x.Turn()) opts.Add("Take control of 1 face-up monster until the End Phase");
                if (AttackOnMe(x)) opts.Add("Change the attack target to another of their monsters");
                int i = await x.D.ChooseText(x.Me, "Eye of Illusion", opts, x.Self);
                x.N = opts[i].StartsWith("Your") ? 0 : opts[i].StartsWith("Take") ? 1 : 2;
                if (x.N == 1) await x.Target(x.OppMonsters().FaceUp(), "Take control of which monster?");
                if (x.N == 2) await x.Target(x.OppMonsters().FaceUp().Where(m => m != x.Ev!.Card), "Which monster becomes the attack target?");
            });
        }
        Spell("Fairy of the Spring", async x => { if (x.T0 is { Loc: Loc.Grave } t) { await x.D.ToHand(t, x.Self); t.Vars["noactturn"] = x.D.Turn; } },
            x => x.Targetable(x.My.Grave.Where(c => c.Data.IsSpell && c.Data.Is("Equip"))).Any(),
            async x => await x.Target(x.My.Grave.Where(c => c.Data.IsSpell && c.Data.Is("Equip")), "Add which Equip Spell?"));
        Spell("Fake Hero", async x =>
        {
            var c = await x.Pick(x.My.Hand.Where(c => EHero(c) && x.D.CanSpecialSummon(c, x.Me, by: x.Self)), "Special Summon which Elemental HERO?");
            if (c == null || !await x.D.SpecialSummon(c, x.Me, await x.D.ChoosePosition(x.Me, c), by: x.Self)) return;
            c.Vars["noattack"] = int.MaxValue;
            var d = x.D;
            d.Later(d.Turn, Phase.End, async () => { if (c.Loc == Loc.Monster) await d.ToHand(c); });
        }, x => x.My.Hand.Any(EHero) && x.D.FreeMonsterZones(x.Me) > 0);
        {
            static bool Beast(Card c) => c.Name is "Uria, Lord of Searing Flames" or "Hamon, Lord of Striking Thunder" or "Raviel, Lord of Phantasms" or "Armityle the Chaos Phantom";
            var s = S("Fallen Paradise");
            s.Forbid = (x, q, c) => c != null && c.Controller == x.Me && c.Loc == Loc.Monster && Beast(c)
                && ((q == Q.Target && x.D.QueryPlayer == x.Opp) || (q == Q.DestroyByEffect && x.D.Destroyer?.Controller == x.Opp));
            s.AddFx(f =>
            {
                f.Label = "Draw 2"; f.Can = x => Hopt(x) && x.MyMonsters().Any(m => m.FaceUp && Beast(m));
                f.Cost = async x => UseHopt(x);
                f.Run = async x => await x.D.Draw(x.Me, 2);
            });
        }
        OnBattleDeath("Familiar Knight", async x =>
        {
            foreach (var p in new[] { x.D.TurnPlayer, 1 - x.D.TurnPlayer })
            {
                var c = await x.D.PickOne(p, x.D.P(p).Hand.Where(c => c.Data.IsMonster && c.Data.Level == 4 && x.D.CanSpecialSummon(c, p, by: x.Self)), "Special Summon 1 Level 4 monster from your hand?", x.Self, optional: true);
                if (c != null) await x.D.SpecialSummon(c, p, await x.D.ChoosePosition(p, c), by: x.Self);
            }
        });
        {
            var s = S("Familiar-Possessed - Hiita");
            static IEnumerable<Card> Charmer(Ctx x) => x.MyMonsters().Where(m => m.FaceUp && Scripts.NameOf(m) == "Hiita the Fire Charmer");
            s.ProcFromDeck = true;
            s.ProcCan = x => x.Self.Loc is Loc.Hand or Loc.Deck && Charmer(x).Any() && x.MyMonsters().Count(m => m.FaceUp && x.IsAttr(m, "FIRE")) >= 2;
            s.Proc = async x =>
            {
                var a = await x.Pick(Charmer(x), "Send Hiita the Fire Charmer to the GY");
                var b = await x.Pick(x.MyMonsters().Where(m => m != a && m.FaceUp && x.IsAttr(m, "FIRE")), "Send 1 other face-up FIRE monster to the GY");
                if (a == null || b == null) return;
                bool deck = x.Self.Loc == Loc.Deck;
                await x.D.ToGrave(a, x.Self); await x.D.ToGrave(b, x.Self);
                await x.D.SpecialSummon(x.Self, x.Me, await x.D.ChoosePosition(x.Me, x.Self), by: x.Self);
                if (deck) x.D.Shuffle(x.Me);
            };
            s.Allow = (x, q, c) => q == Q.Piercing && c == x.Self && x.Self.SummonedBy == x.Self.Name;
        }
        S("Fatal Abacus").On = async (x, e) =>
        {
            if (e.Kind == EvKind.ToGrave && e.From is Loc.Monster && e.Card!.Data.IsMonster && x.Self.OnField && x.Self.FaceUp) await x.D.Damage(e.Card.Owner, 500, x.Self);
        };
        Spell("Feather Shot", async x =>
        {
            if (x.T0 is not { } t || !t.Valid() || !t.FaceUp) return;
            t.Vars["maxatk"] = x.MyMonsters().Count(); t.Vars["maxatkturn"] = x.D.Turn; t.Vars["nodirect"] = x.D.Turn;
            int me = x.Me;   // also monsters Summoned later this turn
            x.D.Linger(x.D.Turn, (q, c, p) => q == Q.Attack && c != null && c != t && c.Controller == me);
        }, x => x.Targetable(x.MyMonsters().FaceUp().Where(m => Scripts.NameOf(m) == "Elemental HERO Avian")).Any(),
        async x => await x.Target(x.MyMonsters().FaceUp().Where(m => Scripts.NameOf(m) == "Elemental HERO Avian"), "Target Elemental HERO Avian"));
        Spell("Feather Wind", async x => await x.D.Negate(x.Ev!.Link!),
            x => x.Ev is { Kind: EvKind.Activated, Link: { IsCardActivation: true } l } && !l.Card.Data.IsMonster && l.Card != x.Self && x.MyMonsters().Any(m => m.FaceUp && Scripts.NameOf(m) == "Elemental HERO Avian"));
        {
            var s = S("Fenrir");
            s.SummonOk = (x, by, k) => by == x.Self || x.D.Flags.ContainsKey($"proper{x.Self.Uid}");
            s.ProcCan = x => x.My.Grave.Count(c => c.Data.IsMonster && c.Data.Attribute == "WATER") >= 2;
            s.Proc = async x =>
            {
                foreach (var c in await x.PickN(x.My.Grave.Where(c => c.Data.IsMonster && c.Data.Attribute == "WATER"), 2, 2, "Banish 2 WATER monsters from your GY")) await x.D.Banish(c, x.Self);
                await x.D.SpecialSummon(x.Self, x.Me, await x.D.ChoosePosition(x.Me, x.Self), by: x.Self);
            };
            s.On = async (x, e) => { if (e.Kind == EvKind.BattleDestroyedMonster && e.Card == x.Self && e.Other!.Controller == x.Opp && Up(x)) x.D.Flags[$"skipdraw{x.Opp}"] = 1; };
        }
        Flip("Fiber Jar", async x =>
        {
            foreach (var p in x.D.Players)
            {
                foreach (var c in p.Hand.Concat(p.FieldCards).Concat(p.Grave).ToList()) await x.D.ToDeck(c);
                x.D.Shuffle(p.Index);
            }
            await x.D.Draw(x.D.TurnPlayer, 5); await x.D.Draw(1 - x.D.TurnPlayer, 5);
        });
        Spell("Fiend's Sanctuary", async x =>
        {
            var t = x.D.MakeToken("Metal Fiend Token", x.Me, 0, 0, "DARK", "Fiend", 1);
            if (!await x.D.SpecialSummon(t, x.Me, await x.D.ChoosePosition(x.Me, t), by: x.Self)) return;
            t.Vars["noattack"] = int.MaxValue; t.Vars["reflectdmg"] = 1;
            var d = x.D; int me = x.Me;
            d.Watch(int.MaxValue, async e =>
            {
                if (e.Kind != EvKind.Standby || e.Player != me || t.Loc != Loc.Monster) return;
                if (d.P(me).Lp > 1000 && await d.YesNo(me, "Pay 1000 LP to keep the Metal Fiend Token?", t)) d.Pay(me, 1000); else await d.Destroy(t);
            });
        }, x => x.D.FreeMonsterZones(x.Me) > 0);
        Spell("Fifth Hope", async x =>
        {
            foreach (var t in x.T.Where(t => t.Loc == Loc.Grave).ToList()) await x.D.ToDeck(t);
            x.D.Shuffle(x.Me);
            await x.D.Draw(x.Me, x.N);
        }, x => x.Targetable(x.My.Grave.Where(c => c.In("Elemental HERO"))).Count() >= 5,
        async x =>
        {
            x.N = x.My.Hand.All(c => c == x.Self) && x.My.FieldCards.All(c => c == x.Self) ? 3 : 2;
            x.T.AddRange(await x.PickN(x.Targetable(x.My.Grave.Where(c => c.In("Elemental HERO"))), 5, 5, "Target 5 Elemental HERO cards in your GY"));
        });
        {
            var s = S("Final Attack Orders");
            s.Forbid = (x, q, c) => q == Q.ChangePos && c != null;
            s.On = async (x, e) =>
            {
                if (!x.Self.OnField || !x.Self.FaceUp) return;
                foreach (var m in x.D.FaceUpMonsters.Where(m => m.Defense).ToList()) await x.D.ChangePosition(m, false);
            };
        }
        Spell("Final Fusion", async x =>
        {
            var e = x.Ev!;
            var both = new[] { e.Card!, e.Other! };
            if (!both.All(m => m.Valid())) return;
            x.D.AttackNegated = true;
            int dmg = both.Sum(m => x.Atk(m));
            await x.D.Damage(x.Me, dmg, x.Self); await x.D.Damage(x.Opp, dmg, x.Self);
        }, x => x.Ev is { Kind: EvKind.AttackDeclared } e && e.Other != null && e.Card!.Data.IsFusion && e.Other.Data.IsFusion && e.Card.FaceUp && e.Other.FaceUp
            && new[] { e.Card, e.Other }.Count(m => m.Controller == x.Me) == 1);
        Spell("Final Light", async x =>
        {
            int n = 0;
            foreach (var t in x.T.Where(t => t.Loc == Loc.Grave).ToList())
                if (x.D.CanSpecialSummon(t, x.Me, by: x.Self) && await x.D.SpecialSummon(t, x.Me, await x.D.ChoosePosition(x.Me, t), by: x.Self)) n++;
            for (int i = 0; i < n; i++)
            {
                var c = await x.D.PickOne(x.Opp, x.Their.Grave.Where(c => c.Data.IsMonster && c.Data.Atk <= 2000 && x.D.CanSpecialSummon(c, x.Opp, by: x.Self)), "Special Summon a monster (2000 or less ATK) from your GY?", x.Self, optional: true);
                if (c == null) break;
                await x.D.SpecialSummon(c, x.Opp, await x.D.ChoosePosition(x.Opp, c), by: x.Self);
            }
        }, x => Hopt(x) && x.My.Lp > 1000 && x.D.FreeMonsterZones(x.Me) > 0 && x.Targetable(x.My.Grave.Where(c => c.Data.IsMonster && c.In("Valkyrie"))).Any(),
        async x =>
        {
            UseHopt(x);
            var l = x.Targetable(x.My.Grave.Where(c => c.Data.IsMonster && c.In("Valkyrie"))).GroupBy(c => c.Name).Select(g => g.First()).ToList();
            int max = Math.Min(Math.Min(l.Count, x.D.FreeMonsterZones(x.Me)), (x.My.Lp - 1) / 1000);
            var pick = await x.PickN(l, 1, max, "Target Valkyrie monsters with different names (1000 LP each)");
            x.D.Pay(x.Me, 1000 * pick.Count);
            x.T.AddRange(pick);
        });
        {
            var s = Spell("Fire Recovery", async x =>
            {
                if (x.T0 is not { Loc: Loc.Grave } t || x.My.Hand.Count == 0) return;
                var h = await x.Pick(x.My.Hand, "Send 1 card from your hand to the GY");
                if (h == null) return;
                await x.D.ToGrave(h, x.Self);
                if (h.Loc == Loc.Grave) await x.D.SpecialSummon(t, x.Me, await x.D.ChoosePosition(x.Me, t), by: x.Self);
            }, x => x.My.Hand.Any(c => c != x.Self) && x.D.FreeMonsterZones(x.Me) > 0 && x.Targetable(x.My.Grave.Where(c => c.Data.IsMonster && c.Data.Attribute == "FIRE")).Any(),
            async x => await x.Target(x.My.Grave.Where(c => c.Data.IsMonster && c.Data.Attribute == "FIRE"), "Special Summon which FIRE monster?"));
            s.GraveFx = new Script
            {
                Speed = 1,
                Can = x => Hopt(x) && x.My.Grave.Concat(x.My.Banished).Count(c => c != x.Self && c.Data.IsMonster && c.Data.Attribute == "FIRE") >= 3,
                Cost = async x =>
                {
                    UseHopt(x);
                    await x.D.Banish(x.Self, x.Self);
                    x.T.AddRange(await x.PickN(x.Targetable(x.My.Grave.Concat(x.My.Banished).Where(c => c.Data.IsMonster && c.Data.Attribute == "FIRE")), 3, 3, "Target 3 FIRE monsters (GY / banished)"));
                },
                Run = async x =>
                {
                    foreach (var t in x.T.Where(t => t.Loc is Loc.Grave or Loc.Banished).ToList()) await x.D.ToDeck(t);
                    x.D.Shuffle(x.Me);
                    await x.D.Draw(x.Me, 1);
                },
            };
        }
        OnSummon("Fire Trooper", async x =>
        {
            if (x.Self.Valid() && await x.Ask("Send Fire Trooper to the GY to inflict 1000 damage?")) { await x.D.ToGrave(x.Self, x.Self); await x.D.Damage(x.Opp, 1000, x.Self); }
        });
        S("Firewall").On = async (x, e) =>
        {
            if (!x.Self.OnField || !x.Self.FaceUp) return;
            if (e.Kind == EvKind.Standby && e.Player == x.Me) { await UpkeepOrDestroy(x, 500, "Firewall"); return; }
            if (e.Kind == EvKind.AttackDeclared && e.Player == x.Opp && e.Other == null && x.My.Grave.Any(c => c.Data.IsMonster && c.Data.Race == "Pyro") && await x.Ask("Banish a Pyro monster from your GY to negate the direct attack?"))
            {
                var c = await x.Pick(x.My.Grave.Where(c => c.Data.IsMonster && c.Data.Race == "Pyro"), "Banish which Pyro?");
                if (c != null) { await x.D.Banish(c, x.Self); x.D.AttackNegated = true; }
            }
        };
        {
            string[] kuris = { "Kuribah", "Kuribee", "Kuriboo", "Kuribeh", "Kuriboh" };
            Spell("Five Star Twilight", async x =>
            {
                foreach (var n in kuris)
                {
                    var c = x.My.Hand.Concat(x.My.Deck).Concat(x.My.Grave).FirstOrDefault(c => c.Name == n && x.D.CanSpecialSummon(c, x.Me, by: x.Self));
                    if (c != null && await x.D.SpecialSummon(c, x.Me, await x.D.ChoosePosition(x.Me, c), by: x.Self)) c.Vars["notribute"] = 1;
                }
                x.D.Shuffle(x.Me);
            }, x => Hopt(x) && x.MyMonsters().Count() == 1 && x.MyMonsters().First() is { FaceUp: true } m && x.D.Level(m) == 5 && kuris.All(n => x.My.Hand.Concat(x.My.Deck).Concat(x.My.Grave).Any(c => c.Name == n)),
            async x => { UseHopt(x); await x.D.Tribute(x.MyMonsters().First(), x.Self); });
        }
        {
            var s = S("Five-Headed Dragon");
            s.Materials = Enumerable.Range(0, 5).Select(_ => Cond("1 Dragon monster", c => c.Data.IsMonster && c.Data.Race == "Dragon")).ToList();
            s.Forbid = (x, q, c) => q == Q.DestroyByBattle && c == x.Self && new[] { x.D.Attacker, x.D.AttackTarget }.FirstOrDefault(m => m != null && m != x.Self) is { } o
                && x.D.Attr(o) is "DARK" or "EARTH" or "WATER" or "FIRE" or "WIND";
        }
        {
            var s = S("Flint");
            s.EquipOk = (x, m) => true;
            s.Stats = (x, c, st) => { if (c == x.Self.EquippedTo) st.Add(-300, 0); };
            s.Forbid = (x, q, c) => q is Q.Attack or Q.ChangePos && c != null && c == x.Self.EquippedTo;
            s.On = async (x, e) =>
            {
                if (e.Is(EvKind.Equipped, x.Self)) Tie(x, e.Other!);
                if (e.Kind == EvKind.Destroyed && e.Card == TiedMonster(x) && x.Self.Loc == Loc.Grave && x.Targetable(x.AllMonsters()).Any() && x.My.FreeSpellZones > 0)
                {
                    x.D.Flags.Remove($"tie{x.Self.Uid}");
                    var t = await x.Pick(x.Targetable(x.AllMonsters().Where(m => m.FaceUp)), "Equip Flint to which monster?");
                    if (t != null) await x.D.Equip(x.Self, t);
                }
            };
        }
        {
            var s = S("Flint Cragger");
            s.On = async (x, e) =>
            {
                if (e.Is(EvKind.Summoned, x.Self) && e.Arg is (int)SummonKind.Special or (int)SummonKind.Fusion or (int)SummonKind.Ritual && Hopt(x, "1") && x.My.Hand.Count > 0
                    && x.My.Extra.Any(f => f.Data.IsFusion && f.In("Fossil")) && await x.Ask("Discard 1 card to send a Fossil Fusion Monster from your Extra Deck to the GY?"))
                {
                    UseHopt(x, "1");
                    await x.D.Discard(x.Me, await x.PickN(x.My.Hand, 1, 1, "Discard 1 card"), x.Self);
                    var f = await x.Pick(x.My.Extra.Where(f => f.Data.IsFusion && f.In("Fossil")), "Send which Fossil Fusion Monster?");
                    if (f != null) await x.D.ToGrave(f, x.Self);
                }
            };
            s.AddFx(f =>
            {
                f.Label = "Burn 500";
                f.Can = x => Hopt(x, "2");
                f.Cost = async x => { UseHopt(x, "2"); await x.D.ToGrave(x.Self, x.Self); };
                f.Run = async x =>
                {
                    await x.D.Damage(x.Opp, 500, x.Self);
                    var l = x.My.Banished.Where(c => c.Name == "Fossil Fusion" || Mentions(c.Data, "Fossil Fusion")).ToList();
                    if (x.My.Grave.Any(c => c.Name == "Fossil Fusion") && l.Count > 0 && await x.Ask("Return a banished Fossil Fusion card to your GY?"))
                    { var c = await x.Pick(l, "Return which?"); if (c != null) await x.D.MoveTo(c, Loc.Grave, by: x.Self); }
                };
            });
        }
        {
            var s = S("Flying Elephant");
            s.DestroyInstead = async (x, battle) =>
            {
                if (battle || x.D.TurnPlayer != x.Opp || x.D.Destroyer?.Controller != x.Opp || x.Self.Var("elephantturn") == x.D.Turn) return false;
                x.Self.Vars["elephantturn"] = x.D.Turn;
                return true;
            };
            s.On = async (x, e) =>
            {
                if (e.Kind == EvKind.EndPhase && e.Player == x.Opp && x.Self.Var("elephantturn") == x.D.Turn && Up(x)) x.Self.Vars["elephantwin"] = x.D.Turn + 1;
                if (e.Kind == EvKind.BattleDamage && e.Card == x.Self && e.Player == x.Opp && e.Other == null && x.Self.Var("elephantwin") == x.D.Turn) x.D.Win(x.Me, "Flying Elephant");
            };
        }
        {
            var s = S("Fog King");
            s.TributesNeeded = x => 0;
            s.TributeAny = (x, t) => true;
            s.TributeMax = 1;
            s.NoSet = true;
            s.Forbid = (x, q, c) => q == Q.Tribute && Up(x);
            s.On = async (x, e) => { if (e.Is(EvKind.Summoned, x.Self) && e.Summoned(SummonKind.Normal, SummonKind.Tribute)) { x.Self.Vars["modatk"] = x.D.LastTributes.Sum(t => Math.Max(0, t.Data.Atk)); x.D.Seq++; } };
            s.Note = "When you Normal Summon it you may Tribute 1 monster (pick none for no Tribute).";
        }
        Sure("Enraged Muka Muka", "Etoile Cyber", "Eternal Dread", "Evil HERO Inferno Wing", "Evil HERO Infernal Sniper", "Fairy of the Spring", "Fatal Abacus", "Fifth Hope", "Feather Wind");
    }
}
