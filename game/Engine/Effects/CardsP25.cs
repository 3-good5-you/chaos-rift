namespace PoC.Engine;

// Official cards, batch 25 (Time Stream .. Valkyrie Dritte).
public static partial class Scripts
{
    /// Modern Toon monsters: cannot attack the turn they are Summoned, destroyed with Toon World, attack directly
    /// while you control Toon World and the opponent has no Toons (otherwise must attack a Toon if they have one).
    static void ModernToon(Script s, bool needsWorldToSummon)
    {
        static bool World(Ctx x) => x.D.CountActive("Toon World", x.Me) > 0;
        static bool OppToon(Ctx x) => x.OppMonsters().Any(m => m.FaceUp && IsToon(m));
        var forbid = s.Forbid;
        s.Forbid = (x, q, c) => (forbid?.Invoke(x, q, c) ?? false) || (c == x.Self && q == Q.Attack && x.Self.SummonTurn == x.D.Turn)
            || (q == Q.BeAttacked && c != null && x.D.Querying == x.Self && OppToon(x) && !IsToon(c));
        s.Allow = (x, q, c) => q == Q.DirectAttack && c == x.Self && World(x) && !OppToon(x);
        var on = s.On;
        s.On = async (x, e) =>
        {
            if (on != null) await on(x, e);
            if (e.Kind == EvKind.Destroyed && Scripts.NameOf(e.Card!) == "Toon World" && x.Self.Valid() && Up(x)) await x.D.Destroy(x.Self, x.Self);
        };
    }

    static void CardsP25(CardDb db)
    {
        {
            static IEnumerable<Card> Next(Ctx x, Card f) => x.My.Extra.Where(c => c.Data.IsFusion && c.In("Fossil") && c.Data.Level == f.Data.Level + 2);
            var s = Spell("Time Stream", async x =>
            {
                if (x.T0 is not { Loc: Loc.Monster } t) return;
                var l = Next(x, t).ToList();
                await x.D.Tribute(t, x.Self);
                var c = await x.Pick(l.Where(c => x.D.CanSpecialSummon(c, x.Me, SummonKind.Fusion, x.My.Grave.FirstOrDefault(g => g.Name == "Fossil Fusion") ?? x.Self)), "Special Summon which Fossil Fusion Monster?");
                if (c != null) await x.D.SpecialSummon(c, x.Me, await x.D.ChoosePosition(x.Me, c), kind: SummonKind.Fusion, by: x.Self, ignore: true);
            }, x => x.Targetable(x.MyMonsters().FaceUp().Where(m => m.Data.IsFusion && m.In("Fossil") && Next(x, m).Any())).Any(),
            async x => await x.Target(x.MyMonsters().FaceUp().Where(m => m.Data.IsFusion && m.In("Fossil") && Next(x, m).Any()), "Target which Fossil Fusion Monster?"));
            s.GraveFx = new Script
            {
                Can = x => x.Self.Var("gyturn") != x.D.Turn && x.D.TurnPlayer == x.Me && x.D.Phase is Phase.Main1 or Phase.Main2 && x.D.FreeMonsterZones(x.Me) > 0
                    && x.My.Grave.Count(c => c.Data.IsFusion && c.In("Fossil")) >= 2,
                Cost = async x =>
                {
                    await x.D.Banish(x.Self, x.Self);
                    var b = await x.Pick(x.My.Grave.Where(c => c.Data.IsFusion && c.In("Fossil")), "Banish which Fossil Fusion Monster?");
                    if (b != null) await x.D.Banish(b, x.Self);
                    await x.Target(x.My.Grave.Where(c => c.Data.IsFusion && c.In("Fossil")), "Special Summon which Fossil Fusion Monster?");
                },
                Run = async x => { if (x.T0 is { Loc: Loc.Grave } t && x.D.CanSpecialSummon(t, x.Me, by: x.Self)) await x.D.SpecialSummon(t, x.Me, await x.D.ChoosePosition(x.Me, t), by: x.Self); },
            };
            s.Note = "The new Fossil monster is placed as a Fusion Summon by \"Fossil Fusion\".";
        }
        S("Timeater").On = async (x, e) => { if (e.Kind == EvKind.BattleDestroyedMonster && e.Card == x.Self && e.Other!.Owner == x.Opp) x.D.Flags[$"skipmain1{x.Opp}"] = 1; };
        Spell("Token Thanksgiving", async x =>
        {
            var l = x.D.AllMonsters.Where(m => m.IsToken).ToList();
            await x.D.Destroy(l, x.Self);
            int n = l.Count(m => !m.OnField);
            if (n > 0) await x.D.Gain(x.Me, 800 * n, x.Self);
        }, x => x.D.AllMonsters.Any(m => m.IsToken));
        {
            var s = S("Toon Ancient Gear Golem");
            s.Forbid = (x, q, c) => q == Q.Activate && x.D.Attacker == x.Self && x.D.Phase == Phase.Battle && c != null && !c.Data.IsMonster && c.Controller == x.Opp;
            ModernToon(s, false);
            var allow = s.Allow;
            s.Allow = (x, q, c) => (allow?.Invoke(x, q, c) ?? false) || (q == Q.Piercing && c == x.Self);
        }
        Spell("Toon Briefcase", async x => { foreach (var c in x.T.Where(c => c.Loc == Loc.Monster).ToList()) { await x.D.ToDeck(c); x.D.Shuffle(c.Owner); } },
            x => x.Ev is { Kind: EvKind.Summoned, Card: { Loc: Loc.Monster } c } e && e.Player == x.Opp && x.MyMonsters().Any(m => m.FaceUp && IsToon(m)),
            async x => x.T.Add(x.Ev!.Card!));
        {
            var s = S("Toon Dark Magician Girl");
            s.SummonOk = (x, by, k) => by == x.Self;
            s.ProcCan = x => x.D.CountActive("Toon World", x.Me) > 0 && x.D.Tributable(x.Me).Any();
            s.Proc = async x =>
            {
                var t = await x.Pick(x.D.Tributable(x.Me), "Tribute 1 monster");
                if (t == null) return;
                await x.D.Tribute(t, x.Self);
                await x.D.SpecialSummon(x.Self, x.Me, await x.D.ChoosePosition(x.Me, x.Self), by: x.Self);
            };
            s.Stats = (x, c, st) => { if (c == x.Self) st.Add(300 * x.D.Players.Sum(p => p.Grave.Count(g => g.Name is "Dark Magician" or "Magician of Black Chaos")), 0); };
            ModernToon(s, true);
            var forbid = s.Forbid;
            s.Forbid = (x, q, c) => !(c == x.Self && q == Q.Attack && x.Self.SummonTurn == x.D.Turn) && (forbid?.Invoke(x, q, c) ?? false);
            s.Allow = (x, q, c) => q == Q.DirectAttack && c == x.Self && !x.OppMonsters().Any(m => m.FaceUp && IsToon(m));
        }
        {
            var s = S("Toon Kingdom");
            s.TreatedAs = "Toon World";
            s.On = async (x, e) =>
            {
                if (e.Kind == EvKind.Resolved && e.Link?.Card == x.Self && e.Link.IsCardActivation)
                    foreach (var c in x.My.Deck.Take(3).ToList()) { await x.D.Banish(c, x.Self); c.FaceUp = false; }
            };
            s.Forbid = (x, q, c) => q == Q.Target && x.Self.FaceUp && x.Self.Loc == Loc.Field && c != null && c.Controller == x.Me && c.Loc == Loc.Monster && IsToon(c) && x.D.Targeter?.Controller == x.Opp;
            s.GraveSave = async (x, c, battle) =>
            {
                if (x.Self.Loc != Loc.Field || c.Controller != x.Me || c.Loc != Loc.Monster || !IsToon(c) || x.My.Deck.Count == 0
                    || !await x.Ask($"Banish the top card of your Deck face-down instead of {c.Name} being destroyed?")) return false;
                var top = x.My.Deck[0];
                await x.D.Banish(top, x.Self); top.FaceUp = false; x.D.Seq++;
                return true;
            };
        }
        Spell("Toon Mask", async x =>
        {
            if (x.T0 is not { Loc: Loc.Monster } t) return;
            int lv = x.D.Level(t);
            await SummonPick(x, x.My.Hand.Concat(x.My.Deck).Where(c => c.Data.IsMonster && IsToon(c) && c.Data.Level <= lv), "Special Summon which Toon monster?", ignore: true);
            x.D.Shuffle(x.Me);
        }, x => x.D.CountActive("Toon World", x.Me) > 0 && x.Targetable(x.OppMonsters().FaceUp()).Any(t => x.My.Hand.Concat(x.My.Deck).Any(c => c.Data.IsMonster && IsToon(c) && c.Data.Level <= x.D.Level(t))),
        async x => await x.Target(x.OppMonsters().FaceUp(), "Target which monster?"));
        {
            var s = S("Toon Masked Sorcerer");
            s.On = async (x, e) => { if (e.Kind == EvKind.BattleDamage && e.Card == x.Self && e.Player == x.Opp) await x.D.Draw(x.Me, 1); };
            ModernToon(s, false);
        }
        Spell("Toon Rollback", async x => { if (x.T0 is { Loc: Loc.Monster } t) { t.Vars["maxatk"] = 2; t.Vars["maxatkturn"] = x.D.Turn; } },
            x => x.Targetable(x.MyMonsters().FaceUp().Where(IsToon)).Any(), async x => await x.Target(x.MyMonsters().FaceUp().Where(IsToon), "Which Toon attacks twice?"));
        Spell("Toon Table of Contents", x => x.Search(c => c.Name.StartsWith("Toon"), "Add which Toon card?"), x => x.My.Deck.Any(c => c.Name.StartsWith("Toon")));
        S("Torpedo Fish").Forbid = (x, q, c) => q == Q.AffectedBySpell && c == x.Self && Up(x) && x.D.CountActive("Umi") > 0;
        {
            var s = Spell("Tour of Doom", async x => x.Self.Vars["stay"] = 1);
            s.On = async (x, e) =>
            {
                if (e.Kind != EvKind.Standby || e.Player != x.Opp || !x.Self.OnField || !x.Self.FaceUp) return;
                bool heads = x.D.TossCoins(x.Me, 1, x.Self) == 1;
                var d = x.D; int who = heads ? x.Opp : x.Me, until = heads ? d.Turn : d.Turn + 1;
                d.Linger(until, (q, c, p) => q is Q.NormalSummon or Q.SetMonster or Q.FlipSummon && (p == who || c?.Controller == who) && (heads || d.TurnPlayer == who));
            };
        }
        {
            var s = S("Toy Soldier");
            s.AddFx(f =>
            {
                f.Label = "Set as a Spell"; f.FromHand = true;
                f.Can = x => x.Self.Loc == Loc.Hand && x.My.FreeSpellZones > 0 && x.D.TurnPlayer == x.Me && x.D.Phase is Phase.Main1 or Phase.Main2;
                f.Run = async x => { if (x.Self.Loc == Loc.Hand && x.D.Place(x.Self, x.Me, Loc.Spell)) { x.Self.FaceUp = false; x.Self.SetTurn = x.D.Turn; x.Self.Vars["asspell"] = 1; x.D.Seq++; } };
            });
            s.On = async (x, e) =>
            {
                if (e.Kind == EvKind.ToGrave && e.Card == x.Self && e.From == Loc.Spell && Hopt(x, "1") && x.D.CanSpecialSummon(x.Self, x.Me, by: x.Self)
                    && x.D.LeftFieldLog.LastOrDefault(l => l.c == x.Self) is { c: not null } lf && !lf.faceUp && await x.Ask("Special Summon Toy Soldier?"))
                { UseHopt(x, "1"); await x.D.SpecialSummon(x.Self, x.Me, await x.D.ChoosePosition(x.Me, x.Self), by: x.Self); }
                if (e.Is(EvKind.Summoned, x.Self) && e.Arg != (int)SummonKind.Flip && Hopt(x, "2"))
                {
                    bool box = x.My.FieldCards.Any(c => c.FaceUp && c.Name == "Toy Box");
                    var l = x.My.Deck.Where(c => c.Name == "Toy Box" || (box && c.Data.IsMonster && c.Data.Level == 4 && c.Data.Attribute == "LIGHT")).ToList();
                    if (l.Count == 0 || !await x.Ask("Add a card from your Deck to your hand?")) return;
                    UseHopt(x, "2");
                    await x.Search(c => l.Contains(c), "Add which card?");
                }
            };
        }
        Spell("Tragedy", async x => await x.D.Destroy(x.OppMonsters().Where(m => m.Defense).ToList(), x.Self),
            x => x.Ev is { Kind: EvKind.PositionChanged, Card: { Loc: Loc.Monster, FaceUp: true, Defense: true } c } && c.Controller == x.Opp);
        S("Tragedy").Note = "It answers any change of an opponent's face-up monster to Defense Position.";
        Spell("Transcendent Wings", async x => { await SummonPick(x, x.My.Hand.Concat(x.My.Deck).Where(c => c.Name == "Winged Kuriboh LV10"), "Special Summon Winged Kuriboh LV10", ignore: true); x.D.Shuffle(x.Me); },
            x => x.MyMonsters().Any(m => Scripts.NameOf(m) == "Winged Kuriboh") && x.My.Hand.Count(c => c != x.Self) >= 2 && x.My.Hand.Concat(x.My.Deck).Any(c => c.Name == "Winged Kuriboh LV10"),
            async x =>
            {
                var k = await x.Pick(x.MyMonsters().Where(m => Scripts.NameOf(m) == "Winged Kuriboh"), "Send which Winged Kuriboh?");
                if (k != null) await x.D.ToGrave(k, x.Self);
                foreach (var c in await x.PickN(x.My.Hand.Where(c => c != x.Self), 2, 2, "Send 2 cards from your hand to the GY")) await x.D.ToGrave(c, x.Self);
            });
        Spell("Trap Jammer", async x => await x.D.Negate(x.Ev!.Link!),
            x => x.D.Phase == Phase.Battle && x.Ev is { Kind: EvKind.Activated, Link: { IsCardActivation: true } l } && l.Player == x.Opp && l.Card.Data.IsTrap);
        Spell("Treasure Map", async x =>
        {
            await x.D.Draw(x.Me, 2);
            if (x.My.Hand.Count > 0) await x.D.Discard(x.Me, await x.PickN(x.My.Hand, 1, 1, "Discard 1 card"), x.Self);
        }, x => x.Self.OnField && x.Ev is { Kind: EvKind.Activated, Link: { } l } && l.X.T.Contains(x.Self) && l.Card.Data.Desc.Contains("hand"));
        S("Treasure Map").Note = "It answers an effect that targets it and mentions returning to the hand.";
        S("Treeborn Frog").On = async (x, e) =>
        {
            if (e.Kind != EvKind.Standby || e.Player != x.Me || x.Self.Loc != Loc.Grave || x.MyMonsters().Any(m => m.FaceUp && m.Name == "Treeborn Frog") || x.My.SpellsOnField.Any()) return;
            if (!Hopt(x) || !x.D.CanSpecialSummon(x.Self, x.Me, by: x.Self) || !await x.Ask("Special Summon Treeborn Frog?")) return;
            UseHopt(x);
            if (!x.My.SpellsOnField.Any()) await x.D.SpecialSummon(x.Self, x.Me, await x.D.ChoosePosition(x.Me, x.Self), by: x.Self);
        };
        {
            var s = Spell("Tri-Blaze Accelerator", async x => x.Self.Vars["stay"] = 1, x => x.MyMonsters().Concat(x.My.SpellsOnField).Any(c => c.FaceUp && c.Name == "Blaze Accelerator"),
                async x => { var b = x.My.FieldCards.First(c => c.FaceUp && c.Name == "Blaze Accelerator"); await x.D.ToGrave(b, x.Self); });
            s.AddFx(f =>
            {
                f.Label = "Send a Pyro: destroy a monster"; f.InSpellZone = true;
                f.Can = x => x.Self.FaceUp && x.My.Hand.Any(c => c.Data.IsMonster && c.Data.Race == "Pyro") && x.OppMonsters().Any();
                f.Cost = async x => { await x.D.ToGrave((await x.PickN(x.My.Hand.Where(c => c.Data.IsMonster && c.Data.Race == "Pyro"), 1, 1, "Send 1 Pyro to the GY"))[0], x.Self); };
                f.Run = async x =>
                {
                    var t = await x.Pick(x.OppMonsters(), "Destroy which monster?");
                    if (t != null) { await x.D.Destroy(t, x.Self); await x.D.Damage(x.Opp, 500, x.Self); }
                    var d = x.D; int me = x.Me; d.Linger(d.Turn, (q, c, p) => q == Q.Attack && c != null && c.Controller == me);
                };
            });
        }
        {
            var s = Spell("Tribute Burial", async x => { var d = x.D; int me = x.Me; d.TurnFlags.Add($"tributeburial{me}"); d.Linger(d.Turn, (q, c, p) => q == Q.SpecialSummon && p == me); });
            s.FreeTribute = (x, c, set) => x.Self.Loc == Loc.Grave && x.D.TurnFlags.Contains($"tributeburial{x.Me}") && c.Controller == x.Me && x.D.TributesFor(c) == 2
                && x.My.Grave.Any(g => g.Data.IsMonster) && x.Their.Grave.Any(g => g.Data.IsMonster);
            s.OnFreeTribute = async x =>
            {
                x.D.TurnFlags.Remove($"tributeburial{x.Me}");
                var a = await x.Pick(x.My.Grave.Where(g => g.Data.IsMonster), "Banish 1 monster from your GY");
                var b = await x.Pick(x.Their.Grave.Where(g => g.Data.IsMonster), "Banish 1 monster from your opponent's GY");
                if (a != null) await x.D.Banish(a, x.Self);
                if (b != null) await x.D.Banish(b, x.Self);
            };
        }
        Spell("Tribute Doll", async x =>
        {
            var c = await x.Pick(x.My.Hand.Where(c => c.Data.IsMonster && c.Data.Level == 7 && x.D.S(c)?.SummonOk == null && x.D.CanSpecialSummon(c, x.Me, by: x.Self, ignoreZones: true)), "Special Summon which Level 7 monster?");
            if (c != null && await x.D.SpecialSummon(c, x.Me, await x.D.ChoosePosition(x.Me, c), by: x.Self)) c.Vars["noattack"] = x.D.Turn;
        }, x => x.D.Tributable(x.Me).Any() && x.My.Hand.Any(c => c.Data.IsMonster && c.Data.Level == 7 && x.D.S(c)?.SummonOk == null),
        async x => { var t = await x.Pick(x.D.Tributable(x.Me), "Tribute 1 monster"); if (t != null) await x.D.Tribute(t, x.Self); });
        Spell("Tricky Spell 4", async x =>
        {
            int n = Math.Min(x.OppMonsters().Count(), x.D.FreeMonsterZones(x.Me));
            for (int i = 0; i < n; i++)
            {
                var t = x.D.MakeToken("Tricky Token", x.Me, 2000, 1200, "WIND", "Spellcaster", 5);
                if (await x.D.SpecialSummon(t, x.Me, true, by: x.Self)) t.Vars["noattack"] = int.MaxValue;
            }
        }, x => x.MyMonsters().Any(m => m.FaceUp && m.Name == "The Tricky") && x.OppMonsters().Any(),
        async x => { var t = x.MyMonsters().First(m => m.FaceUp && m.Name == "The Tricky"); await x.D.ToGrave(t, x.Self); });
        Spell("Triggered Summon", async x =>
        {
            foreach (int p in new[] { x.Me, x.Opp })
            {
                var l = x.D.P(p).Hand.Where(c => c.Data.IsMonster && c.Data.Level <= 4 && x.D.CanSpecialSummon(c, p, by: x.Self)).ToList();
                if (l.Count == 0) continue;
                var c = await x.D.PickOne(p, l, "Special Summon a Level 4 or lower monster from your hand?", x.Self, optional: true);
                if (c != null) await x.D.SpecialSummon(c, p, await x.D.ChoosePosition(p, c), by: x.Self);
            }
        }, x => x.Ev is { Kind: EvKind.Summoned } e && e.Player == x.Opp && e.Arg is (int)SummonKind.Special or (int)SummonKind.Fusion or (int)SummonKind.Ritual);
        Spell("Trojan Blast", async x =>
        {
            if (x.T0 is not { Loc: Loc.Monster } t) return;
            int atk = x.Atk(t);
            await x.D.Destroy(t, x.Self);
            if (!t.OnField) await x.D.Damage(x.Opp, atk, x.Self);
        }, x => x.Ev is { Kind: EvKind.ControlChanged, Card: { Loc: Loc.Monster } c } e && e.Player == x.Opp && c.Owner == x.Me,
        async x => x.T.Add(x.Ev!.Card!));
        S("Trojan Blast").Note = "It answers any switch of your monster to your opponent's side.";
        OnBattleDeath("Troop Dragon", async x => { await SummonPick(x, x.My.Deck.Where(c => c.Name == "Troop Dragon"), "Special Summon Troop Dragon"); x.D.Shuffle(x.Me); });
        {
            var s = S("Truckroid");
            s.On = async (x, e) =>
            {
                if (!KilledToGrave(x, e) || x.My.FreeSpellZones == 0) return;
                var o = e.Other!;
                if (!x.D.Place(o, x.Me, Loc.Spell)) return;
                o.FaceUp = true;
                await x.D.Equip(o, x.Self);
                o.Vars["truckroid"] = 1;
            };
            s.Stats = (x, c, st) => { if (c == x.Self) st.Add(x.D.EquipsOf(x.Self).Where(q => q.Var("truckroid") == 1).Sum(q => Math.Max(0, q.Data.Atk)), 0); };
        }
        S("Twin-Headed Behemoth").On = async (x, e) =>
        {
            if (e.Kind != EvKind.EndPhase || x.Self.Loc != Loc.Grave || x.D.Flags.GetValueOrDefault($"destroyed{x.Self.Uid}") != x.D.Turn || x.D.Flags.ContainsKey($"behemoth{x.Me}")) return;
            if (!x.D.CanSpecialSummon(x.Self, x.Me, by: x.Self) || !await x.Ask("Special Summon Twin-Headed Behemoth (ATK/DEF 1000)?")) return;
            x.D.Flags[$"behemoth{x.Me}"] = 1;
            if (await x.D.SpecialSummon(x.Self, x.Me, await x.D.ChoosePosition(x.Me, x.Self), by: x.Self)) x.Self.Vars["behemoth"] = 1;
        };
        S("Twin-Headed Behemoth").Base = (x, c, st) => { if (c == x.Self && x.Self.Var("behemoth") == 1) { st.BaseAtk = 1000; st.BaseDef = 1000; } };
        S("Twinheaded Beast").Allow = (x, q, c) => q == Q.DoubleAttack && c == x.Self;
        S("Tyranno Infinity").Base = (x, c, st) => { if (c == x.Self) st.BaseAtk = 1000 * x.My.Banished.Count(b => b.Data.IsMonster && b.Data.Race == "Dinosaur"); };
        {
            var s = S("Tyrant Burst Dragon");
            s.SummonOk = (x, by, k) => by?.Name == "The Fang of Critias";
            s.AttackEach = true;
            s.Can = x => Up(x) && x.My.FreeSpellZones > 0 && x.Targetable(x.MyMonsters().FaceUp().Where(m => m != x.Self)).Any();
            s.Cost = async x => await x.Target(x.MyMonsters().FaceUp().Where(m => m != x.Self), "Equip Tyrant Burst Dragon to which monster?");
            s.Run = async x =>
            {
                if (x.T0 is not { Loc: Loc.Monster, FaceUp: true } t || !Up(x)) return;
                x.Self.ResetFieldState();
                x.D.Place(x.Self, x.Me, Loc.Spell); x.Self.FaceUp = true;
                await x.D.Equip(x.Self, t);
                x.Self.Vars["asspell"] = 1;
            };
            s.Stats = (x, c, st) => { if (x.Self.Loc == Loc.Spell && c == x.Self.EquippedTo) st.Add(400, 400); };
            s.On = async (x, e) => { if (x.Self.Loc == Loc.Spell && x.Self.EquippedTo is { } m && m.Var("maxatkturn") != x.D.Turn) { m.Vars["maxatk"] = 3; m.Vars["maxatkturn"] = x.D.Turn; } };
        }
        {
            var s = S("Tyrant Dragon");
            s.SummonOk = (x, by, k) => x.Self.Loc != Loc.Grave || x.Self.Var("tyrantok") == 1;
            s.ExtraAttacks = x => x.OppMonsters().Any() ? 1 : 0;
            s.On = async (x, e) =>
            {
                if (e.Kind == EvKind.Activated && Up(x) && e.Link is { } l && l.Card.Data.IsTrap && l.X.T.Contains(x.Self)) await x.D.Negate(l);
            };
            s.Note = "It cannot be Special Summoned from the GY (the Tribute-a-Dragon exception is not offered).";
        }
        {
            var s = Spell("Tyrant Wing", async x =>
            {
                if (x.T0 is { Loc: Loc.Monster, FaceUp: true } t && x.Self.OnField) { await x.D.Equip(x.Self, t); x.Self.Vars["stay"] = 1; }
            }, x => x.Targetable(x.D.FaceUpMonsters.Where(m => x.IsRace(m, "Dragon"))).Any(), async x => await x.Target(x.D.FaceUpMonsters.Where(m => x.IsRace(m, "Dragon")), "Equip to which Dragon?"));
            s.Stats = (x, c, st) => { if (c == x.Self.EquippedTo) st.Add(400, 400); };
            s.On = async (x, e) =>
            {
                if (!x.Self.OnField || x.Self.EquippedTo is not { } m) return;
                if (m.Var("maxatkturn") != x.D.Turn) { m.Vars["maxatk"] = 2; m.Vars["maxatkturn"] = x.D.Turn; }
                if (e.Kind == EvKind.AttackDeclared && e.Card == m && e.Other != null) x.Self.Vars["wingatk"] = x.D.Turn;
                if (e.Kind == EvKind.EndPhase && x.Self.Var("wingatk") == x.D.Turn) await x.D.Destroy(x.Self, x.Self);
            };
            s.Forbid = (x, q, c) => q == Q.DirectAttack && x.Self.OnField && c != null && c == x.Self.EquippedTo && c.Attacks > 0;   // the extra attack is on monsters only
        }
        OnBattleDeath("UFOroid", x => x.Recruit(c => c.Data.Race == "Machine" && c.Data.Atk is >= 0 and <= 1500, "UFOroid: Special Summon a Machine with 1500 or less ATK", false));
        {
            var s = S("UFOroid Fighter");
            s.On = async (x, e) =>
            {
                if (!e.Is(EvKind.Summoned, x.Self) || e.Arg != (int)SummonKind.Fusion) return;
                int sum = 0;
                for (int i = 0; i < 2; i++) if (x.Self.Var($"mat{i}") > 0 && x.D.CardByUid(x.Self.Var($"mat{i}") - 1) is { } m) sum += Math.Max(0, m.Data.Atk);
                x.Self.Vars["ufo"] = sum + 1; x.D.Seq++;
            };
            s.Base = (x, c, st) => { if (c == x.Self) { int v = Math.Max(0, x.Self.Var("ufo") - 1); st.BaseAtk = v; st.BaseDef = v; } };
        }
        {
            var s = S("Ultimate Ancient Gear Golem");
            s.MustFusion = true;
            s.Allow = (x, q, c) => q == Q.Piercing && c == x.Self;
            s.Forbid = (x, q, c) => q == Q.Activate && x.D.Attacker == x.Self && x.D.Phase == Phase.Battle && c != null && !c.Data.IsMonster && c.Controller == x.Opp;
            s.On = async (x, e) =>
            {
                if (!e.Is(EvKind.Destroyed, x.Self)) return;
                var l = x.Targetable(x.My.Grave.Where(c => c.Name == "Ancient Gear Golem" && x.D.CanSpecialSummon(c, x.Me, by: x.Self, ignore: true))).ToList();
                if (l.Count > 0 && await x.Ask("Special Summon Ancient Gear Golem from your GY?")) await SummonPick(x, l, "Special Summon Ancient Gear Golem", ignore: true);
            };
        }
        {
            var s = S("Ultimate Obedient Fiend");
            s.Forbid = (x, q, c) => q == Q.Attack && c == x.Self && (x.My.FieldCards.Count() > 1 || x.My.Hand.Count > 0);
            s.On = async (x, e) => { if (e.Kind == EvKind.BattleDestroyedMonster && e.Card == x.Self) { var d = x.D; var o = e.Other!; d.Flags[$"neg{o.Uid}"] = 1; d.Later(d.Turn, Phase.End, async () => d.Flags.Remove($"neg{o.Uid}")); } };
        }
        {
            var s = S("Ultimate Tyranno");
            s.AttackEach = true;
            s.Forbid = (x, q, c) => q == Q.Attack && Up(x) && x.Self.Attack && c != null && c.Controller == x.Me && Scripts.NameOf(c) != "Ultimate Tyranno" && x.D.AttackTargets(x.Self).Any();
        }
        Spell("Ultra Evolution Pill", async x => await SummonPick(x, x.My.Hand.Where(c => c.Data.IsMonster && c.Data.Race == "Dinosaur"), "Special Summon which Dinosaur?"),
            x => x.D.Tributable(x.Me).Any(m => m.FaceUp ? x.IsRace(m, "Reptile") : m.Data.Race == "Reptile") && x.My.Hand.Any(c => c.Data.IsMonster && c.Data.Race == "Dinosaur"),
            async x => { var t = await x.Pick(x.D.Tributable(x.Me).Where(m => m.FaceUp ? x.IsRace(m, "Reptile") : m.Data.Race == "Reptile"), "Tribute 1 Reptile"); if (t != null) await x.D.Tribute(t, x.Self); });
        {
            var s = Spell("Underworld Circle", async x =>
            {
                x.Self.Vars["stay"] = 1;
                await x.D.Destroy(x.D.AllMonsters.ToList(), x.Self);
                foreach (var p in x.D.Players) foreach (var c in p.Deck.Where(c => c.Data.IsMonster).ToList()) { await x.D.Banish(c, x.Self); c.FaceUp = false; }
                var l = x.My.Grave.Where(c => c.Data.IsNormalMonster && x.D.CanSpecialSummon(c, x.Me, by: x.Self)).ToList();
                if (l.Count > 0) await SummonPick(x, l, "Special Summon a Normal Monster from your GY?", optional: true);
            }, x => Hopt(x) && x.My.Grave.Count(c => c.Data.IsMonster) >= 5 && x.Their.Grave.Count(c => c.Data.IsMonster) >= 5, async x => UseHopt(x));
            s.On = async (x, e) =>
            {
                if (e.Kind != EvKind.Standby || !x.Self.OnField || !x.Self.FaceUp || x.Self.Var("uwturn") == x.D.Turn) return;
                x.Self.Vars["uwturn"] = x.D.Turn;
                foreach (int p in new[] { x.D.TurnPlayer, 1 - x.D.TurnPlayer })
                {
                    var l = x.D.P(p).Grave.Where(c => c.Data.IsMonster && x.D.CanSpecialSummon(c, p, by: x.Self, ignore: true)).ToList();
                    if (l.Count == 0) continue;
                    var c = await x.D.PickOne(p, l, "Underworld Circle: Special Summon 1 monster from your GY?", x.Self, optional: true);
                    if (c != null && await x.D.SpecialSummon(c, p, await x.D.ChoosePosition(p, c), by: x.Self, ignore: true)) c.Vars["leavebanish"] = 1;
                }
            };
        }
        Spell("Union Attack", async x =>
        {
            if (x.T0 is not { Loc: Loc.Monster, FaceUp: true } t) return;
            int sum = x.MyMonsters().Where(m => m != t && m.FaceUp && m.Attack).Sum(m => x.Atk(m));
            var d = x.D; int me = x.Me, turn = d.Turn; bool on = false;
            void Apply() { if (!on && t.Loc == Loc.Monster) { on = true; d.Boost(t, sum); } }
            if (d.Phase == Phase.Battle) Apply();
            d.Watch(turn, async ev => { if (ev.Kind == EvKind.BattleStart && ev.Player == me) Apply(); if (ev.Kind == EvKind.BattleEnd && on) { on = false; d.Boost(t, -sum); } });
            t.Vars["fixeddmg"] = 0; t.Vars["fixeddmgturn"] = turn;
            d.Linger(turn, (q, c, p) => q == Q.Attack && c != null && c != t && c.Controller == me && c.Attack);
        }, x => x.Targetable(x.MyMonsters().FaceUp()).Any(), async x => await x.Target(x.MyMonsters().FaceUp(), "Target which monster?"));
        S("Unshaven Angler").TributeWeight = (x, t, forCard) => forCard.Data.Attribute == "WATER" ? 2 : 1;
        {
            var s = S("Uria, Lord of Searing Flames");
            s.SummonOk = (x, by, k) => by == x.Self;
            s.ProcCan = x => x.My.SpellsOnField.Count(c => c.FaceUp && c.Data.IsTrap) >= 3;
            s.Proc = async x =>
            {
                foreach (var c in await x.PickN(x.My.SpellsOnField.Where(c => c.FaceUp && c.Data.IsTrap), 3, 3, "Send 3 face-up Traps to the GY")) await x.D.ToGrave(c, x.Self);
                await x.D.SpecialSummon(x.Self, x.Me, await x.D.ChoosePosition(x.Me, x.Self), by: x.Self);
            };
            s.Stats = (x, c, st) => { if (c == x.Self) st.Add(1000 * x.My.Grave.Count(g => g.Data.IsTrap && g.Data.Is("Continuous")), 0); };
            s.OncePerTurn = true;
            s.NoResponse = true;
            s.Can = x => Up(x) && x.Targetable(x.Spells(x.Opp).Where(c => !c.FaceUp)).Any();
            s.Cost = async x => await x.Target(x.Spells(x.Opp).Where(c => !c.FaceUp), "Destroy which Set card?");
            s.Run = async x => { if (x.T0 is { OnField: true } t) await x.D.Destroy(t, x.Self); };
            s.Note = "No card can respond to its destroy effect (the text only stops Spells/Traps).";
        }
        {
            var s = S("Valkyrie Brunhilde");
            s.Forbid = (x, q, c) => q == Q.AffectedBySpell && c == x.Self && Up(x) && (x.D.Affecting ?? x.D.Targeter) is { } by && by.Controller == x.Opp;
            s.Stats = (x, c, st) => { if (c == x.Self) st.Add(500 * x.OppMonsters().Count(), 0); };
            s.On = async (x, e) =>
            {
                if (e.Kind != EvKind.AttackDeclared || e.Player != x.Opp || !Up(x) || x.Def(x.Self) < 1000 || !await x.Ask("Valkyrie Brunhilde: lose 1000 DEF so your Valkyries can't be destroyed by battle?")) return;
                x.Self.Vars["moddef"] = x.Self.Var("moddef") - 1000; x.D.Seq++;
                var d = x.D; int me = x.Me;
                d.Linger(d.Turn, (q, c, p) => q == Q.DestroyByBattle && c != null && c.Controller == me && c.In("Valkyrie"));
            };
        }
        {
            var s = S("Valkyrie Dritte");
            s.Stats = (x, c, st) => { if (c == x.Self) st.Add(200 * x.Their.Banished.Count(b => b.Data.IsMonster), 0); };
            s.On = async (x, e) =>
            {
                if (e.Is(EvKind.Summoned, x.Self) && e.Arg != (int)SummonKind.Flip && Hopt(x) && x.My.Deck.Any(c => c.In("Valkyrie") && c.Name != "Valkyrie Dritte") && await x.Ask("Add a Valkyrie card from your Deck?"))
                { UseHopt(x); await x.Search(c => c.In("Valkyrie") && c.Name != "Valkyrie Dritte", "Add which Valkyrie card?"); }
            };
        }
        Sure("Timeater", "Token Thanksgiving", "Toon Table of Contents", "Torpedo Fish", "Trap Jammer", "Troop Dragon", "Twinheaded Beast", "Tyranno Infinity", "UFOroid", "Ultra Evolution Pill",
            "Unshaven Angler", "Toon Rollback", "Toon Masked Sorcerer", "Ultimate Obedient Fiend", "Truckroid", "Tribute Doll", "Tricky Spell 4");
    }
}
