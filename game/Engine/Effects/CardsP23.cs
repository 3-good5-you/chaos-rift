namespace PoC.Engine;

// Official cards, batch 23 (Specimen Inspection .. Swarm of Locusts).
public static partial class Scripts
{
    static bool DestroyText(CardData d) => System.Text.RegularExpressions.Regex.IsMatch(d.Desc, @"\bdestroy", System.Text.RegularExpressions.RegexOptions.IgnoreCase);

    static void CardsP23(CardDb db)
    {
        Spell("Specimen Inspection", async x =>
        {
            var m = await x.Pick(x.My.Hand.Where(c => c.Data.IsMonster), "Send which monster from your hand to the GY?");
            if (m == null) return;
            await x.D.ToGrave(m, x.Self);
            if (m.Loc != Loc.Grave) return;
            var races = x.D.Cards.Where(c => c.Data.IsMonster).Select(c => c.Data.Race).Where(r => !string.IsNullOrEmpty(r)).Distinct().OrderBy(r => r).ToList();
            string race = races[await x.D.ChooseText(x.Me, "Declare a Type", races, x.Self)];
            int lv = 1 + await x.D.ChooseText(x.Me, "Declare a Level", Enumerable.Range(1, 12).Select(i => $"Level {i}").ToList(), x.Self);
            var l = x.Their.Hand.Concat(x.Their.Deck).Where(c => c.Data.IsMonster && c.Data.Race == race && c.Data.Level == lv).ToList();
            if (l.Count == 0) return;
            var c = await x.D.PickOne(x.Opp, l, $"Send a Level {lv} {race} monster to the GY", x.Self);
            if (c != null) { bool deck = c.Loc == Loc.Deck; await x.D.ToGrave(c, x.Self); if (deck) x.D.Shuffle(x.Opp); }
        }, x => Hopt(x) && x.My.Hand.Any(c => c.Name == "Fossil Fusion") && x.My.Hand.Any(c => c.Data.IsMonster),
        async x => { UseHopt(x); x.D.Reveal(x.Opp, x.My.Hand.First(c => c.Name == "Fossil Fusion")); });
        {
            var s = Spell("Spell Calling", async x => { }, x => false);
            s.On = async (x, e) =>
            {
                if (!e.Is(EvKind.Destroyed, x.Self) || e.Battle || x.Self.Loc != Loc.Grave || e.Other is not { } by || by.Data.IsMonster || by.Controller != x.Opp) return;
                if (x.D.LeftFieldLog.LastOrDefault(l => l.c == x.Self) is not { c: not null } l || l.faceUp || x.My.FreeSpellZones == 0) return;
                var q = await x.Pick(x.My.Deck.Where(c => c.Data.IsSpell && c.Data.Is("Quick-Play")), "Set which Quick-Play Spell?");
                x.D.Shuffle(x.Me);
                if (q == null) return;
                x.D.Reveal(x.Opp, q);
                if (x.D.Place(q, x.Me, Loc.Spell)) { q.FaceUp = false; q.SetTurn = x.D.Turn; await x.D.Raise(new Ev { Kind = EvKind.Set, Card = q, Player = x.Me }); }
            };
            s.Note = "It has no activation: it works when your Set copy is destroyed by your opponent's Spell/Trap.";
        }
        S("Spell Canceller").Forbid = (x, q, c) => Up(x) && c != null && c.Data.IsSpell && (q == Q.Activate || (q == Q.Negate && c.OnField));
        {
            var s = Spell("Spell Chronicle", async x =>
            {
                x.Self.Vars["stay"] = 1;
                var l = x.My.Deck.Where(c => !c.Data.IsMonster).ToList();
                foreach (var c in await x.PickN(l, Math.Min(5, l.Count), Math.Min(5, l.Count), "Banish 5 Spells/Traps from your Deck"))
                { await x.D.Banish(c, x.Self); x.D.Flags[$"chronicle{c.Uid}"] = x.Self.Uid + 1; }
                x.D.Shuffle(x.Me);
            }, x => x.My.Deck.Count(c => !c.Data.IsMonster) >= 5, async x => { foreach (var c in x.My.Hand.Where(c => c != x.Self).ToList()) await x.D.ToGrave(c, x.Self); });
            static IEnumerable<Card> Mine(Ctx x) => x.My.Banished.Where(c => x.D.Flags.GetValueOrDefault($"chronicle{c.Uid}") == x.Self.Uid + 1);
            s.AddFx(f =>
            {
                f.Label = "Remove 2 counters: add a banished card"; f.InSpellZone = true;
                f.Can = x => x.Self.FaceUp && x.Self.Counters("chronicle") >= 2 && Mine(x).Any();
                f.Cost = async x => AddCounter(x, x.Self, -2, "chronicle");
                f.Run = async x => { var c = await x.D.PickOne(x.Opp, Mine(x), "Choose 1 card for your opponent to add to their hand", x.Self); if (c != null) { x.D.Flags.Remove($"chronicle{c.Uid}"); await x.D.ToHand(c, x.Self); } };
            });
            s.On = async (x, e) =>
            {
                if (e.Kind == EvKind.Resolved && x.Self.OnField && x.Self.FaceUp && e.Link is { IsCardActivation: true } l && l.Player == x.Opp && l.Card.Data.IsSpell) AddCounter(x, x.Self, 1, "chronicle");
                if (e.Kind == EvKind.LeftField && e.Card == x.Self && x.D.LeftFieldLog.LastOrDefault(k => k.c == x.Self) is { c: not null } lf && lf.faceUp)
                {
                    int n = Mine(x).Count();
                    if (n > 0) await x.D.Damage(x.Me, 500 * n, x.Self);
                }
            };
        }
        Spell("Spell Economics", async x => x.Self.Vars["stay"] = 1);
        {
            static IEnumerable<Card> Gears(Ctx x) => x.My.FieldCards.Where(c => c.FaceUp && c.In("Ancient Gear"));
            Spell("Spell Gear", async x =>
            {
                var d = x.D;
                var h = x.My.Hand.FirstOrDefault(c => c.Name == "Ancient Gear Golem" && d.CanSpecialSummon(c, x.Me, by: x.Self, ignore: true));
                if (h != null && await x.Ask("Special Summon Ancient Gear Golem from your hand?")) await d.SpecialSummon(h, x.Me, await d.ChoosePosition(x.Me, h), by: x.Self, ignore: true);
                var k = x.My.Deck.FirstOrDefault(c => c.Name == "Ancient Gear Golem" && d.CanSpecialSummon(c, x.Me, by: x.Self, ignore: true));
                if (k != null && await x.Ask("Special Summon Ancient Gear Golem from your Deck?")) { await d.SpecialSummon(k, x.Me, await d.ChoosePosition(x.Me, k), by: x.Self, ignore: true); d.Shuffle(x.Me); }
                await d.Destroy(x.MyMonsters().Where(m => Scripts.NameOf(m) != "Ancient Gear Golem").ToList(), x.Self);
                int me = x.Me;
                d.Linger(d.Turn + 2, (q, c, p) => q is Q.NormalSummon or Q.SetMonster && p == me);
            }, x => Gears(x).Count() >= 3, async x => { foreach (var c in await x.PickN(Gears(x), 3, 3, "Send 3 Ancient Gear cards to the GY")) await x.D.ToGrave(c, x.Self); });
        }
        Spell("Spell of Pain", async x => { x.D.PainLink = x.Ev!.Link; x.D.PainPlayer = x.Me; },
            x => x.Ev is { Kind: EvKind.Activated, Link: { } l } && l.Player == x.Opp && System.Text.RegularExpressions.Regex.IsMatch(l.Card.Data.Desc, @"inflict[^.]*damage|damage to (you|your opponent|each)", System.Text.RegularExpressions.RegexOptions.IgnoreCase));
        S("Spell of Pain").Note = "It answers any opponent's card whose text inflicts damage; the damage it deals you goes to them.";
        Spell("Spell Reproduction", async x => { if (x.T0 is { Loc: Loc.Grave } t) await x.D.ToHand(t, x.Self); },
            x => x.My.Hand.Count(c => c != x.Self && c.Data.IsSpell) >= 2 && x.Targetable(x.My.Grave.Where(c => c.Data.IsSpell)).Any(),
            async x => { foreach (var c in await x.PickN(x.My.Hand.Where(c => c != x.Self && c.Data.IsSpell), 2, 2, "Send 2 Spells to the GY")) await x.D.ToGrave(c, x.Self); await x.Target(x.My.Grave.Where(c => c.Data.IsSpell), "Add which Spell?"); });
        {
            var s = Spell("Spell Shield Type-8", async x => await x.D.Negate(x.Ev!.Link!), x => x.Ev is { Kind: EvKind.Activated, Link: { IsCardActivation: true } l } && l.Card.Data.IsSpell && l.Card != x.Self
                && ((l.X.T.Count == 1 && l.X.T[0].Loc == Loc.Monster) || x.My.Hand.Any(c => c != x.Self && c.Data.IsSpell)),
            async x =>
            {
                var l = x.Ev!.Link!;
                bool one = l.X.T.Count == 1 && l.X.T[0].Loc == Loc.Monster, discard = x.My.Hand.Any(c => c != x.Self && c.Data.IsSpell);
                int i = one && discard ? await x.D.ChooseText(x.Me, "Spell Shield Type-8", new[] { "It targets 1 monster: negate", "Send 1 Spell from your hand: negate" }, x.Self) : one ? 0 : 1;
                if (i == 1) await x.D.ToGrave((await x.PickN(x.My.Hand.Where(c => c != x.Self && c.Data.IsSpell), 1, 1, "Send 1 Spell to the GY"))[0], x.Self);
            });
        }
        {
            var s = DirectAttacker("Spell Striker");
            s.Allow = (x, q, c) => c == x.Self && q is Q.DirectAttack or Q.NoBattleDamageToController;
            s.ProcCan = x => x.My.Grave.Any(c => c.Data.IsSpell);
            s.Proc = async x =>
            {
                var c = await x.Pick(x.My.Grave.Where(c => c.Data.IsSpell), "Banish which Spell?");
                if (c == null) return;
                await x.D.Banish(c, x.Self);
                await x.D.SpecialSummon(x.Self, x.Me, await x.D.ChoosePosition(x.Me, x.Self), by: x.Self);
            };
        }
        {
            var s = S("Spider Web");
            s.On = async (x, e) =>
            {
                if (!x.Self.FaceUp || x.Self.Loc != Loc.Field || e.Kind != EvKind.AfterDamage || e.Card is not { Loc: Loc.Monster } a) return;
                if (a.Attack) await x.D.ChangePosition(a, true);
                var d = x.D; var self = x.Self; int until = a.Controller == d.TurnPlayer ? d.Turn + 2 : d.Turn + 1;
                d.Linger(until, (q, c, p) => q == Q.ChangePos && c == a && self.Loc == Loc.Field && self.FaceUp);
            };
        }
        S("Spirit Barrier").BattleDamage = (x, src, to, dmg) => x.Self.OnField && x.Self.FaceUp && to == x.Me && x.MyMonsters().Any() ? 0 : dmg;
        Spell("Spirit Barrier", async x => x.Self.Vars["stay"] = 1);
        Flip("Spirit Caller", async x =>
        {
            var l = x.My.Grave.Where(c => c.Data.IsNormalMonster && c.Data.Level <= 3 && x.D.CanSpecialSummon(c, x.Me, by: x.Self)).ToList();
            if (l.Count > 0) await SummonPick(x, l, "Special Summon a Level 3 or lower Normal Monster?", optional: true);
        });
        {
            static bool Ok(Ctx x, Card m) => x.D.Level(m) >= 5 && x.D.Race(m) is "Fiend" or "Zombie";
            var s = Spell("Spirit Illusion", async x => { if (x.T0 is { } t && t.Valid() && t.FaceUp) await x.D.Equip(x.Self, t); },
                x => x.Targetable(x.D.FaceUpMonsters.Where(m => Ok(x, m))).Any(), async x => await x.Target(x.D.FaceUpMonsters.Where(m => Ok(x, m)), "Equip to which monster?"));
            s.EquipOk = (x, m) => Ok(x, m);
            s.On = async (x, e) =>
            {
                if (e.Kind != EvKind.AttackDeclared || !x.Self.OnField || x.Self.EquippedTo is not { } m || (e.Card != m && e.Other != m)) return;
                var o = e.Card == m ? e.Other : e.Card;
                if (o is { Loc: Loc.Monster, FaceUp: true } && o.Controller != m.Controller && await x.Ask($"Make {o.Name} lose ATK equal to {m.Name}'s ATK?"))
                { o.Vars["modatk"] = o.Var("modatk") - x.Atk(m); x.D.Seq++; }
            };
            s.AddFx(f =>
            {
                f.Label = "Fusion Summon or Doppelganger Token"; f.InSpellZone = true;
                f.Can = x => x.Self.FaceUp && x.Self.EquippedTo != null && Hopt(x) && (x.D.FusionOptions(x.Me, by: x.Self).Any() || x.D.FreeMonsterZones(x.Me) > 0);
                f.Cost = async x => UseHopt(x);
                f.Run = async x =>
                {
                    bool fus = x.D.FusionOptions(x.Me, by: x.Self).Any(), tok = x.D.FreeMonsterZones(x.Me) > 0 && x.Self.EquippedTo != null;
                    int i = fus && tok ? await x.D.ChooseText(x.Me, "Spirit Illusion", new[] { "Fusion Summon", "Doppelganger Token" }, x.Self) : fus ? 0 : 1;
                    if (i == 0) await x.D.FusionSummon(x.Me, x.Self);
                    else if (x.Self.EquippedTo is { } m)
                    {
                        var t = x.D.MakeToken("Doppelganger Token", x.Me, x.Atk(m), 0, x.D.Attr(m), x.D.Race(m), 5);
                        await x.D.SpecialSummon(t, x.Me, await x.D.ChoosePosition(x.Me, t), by: x.Self);
                    }
                };
            });
        }
        {
            var s = S("Spirit of the Pharaoh");
            s.SummonOk = (x, by, k) => by?.Name == "The First Sarcophagus";
            s.On = async (x, e) =>
            {
                if (!e.Is(EvKind.Summoned, x.Self)) return;
                var l = x.My.Grave.Where(c => c.Data.IsNormalMonster && c.Data.Race == "Zombie" && c.Data.Level <= 2 && x.D.CanSpecialSummon(c, x.Me, by: x.Self)).ToList();
                int max = Math.Min(4, Math.Min(l.Count, x.D.FreeMonsterZones(x.Me)));
                if (max == 0 || !await x.Ask("Special Summon Level 2 or lower Zombie Normal Monsters from your GY?")) return;
                foreach (var c in await x.PickN(l, 1, max, $"Special Summon up to {max}")) await x.D.SpecialSummon(c, x.Me, await x.D.ChoosePosition(x.Me, c), by: x.Self);
            };
        }
        S("Spirit of the Pot of Greed").On = async (x, e) =>
        {
            if (e.Kind == EvKind.Resolved && Up(x) && x.Self.Attack && e.Link is { IsCardActivation: true } l && l.Card.Name == "Pot of Greed" && await x.D.YesNo(l.Player, "Spirit of the Pot of Greed: draw 1 more card?", x.Self))
                await x.D.Draw(l.Player, 1);
        };
        {
            var s = S("Spirit Ryu");
            s.Speed = 2;
            s.Can = x => Up(x) && x.D.TurnPlayer == x.Me && x.D.Phase == Phase.Battle && (x.D.Attacker == x.Self || x.D.AttackTarget == x.Self) && x.My.Hand.Any(c => c.Data.IsMonster && c.Data.Race == "Dragon");
            s.Cost = async x => await x.D.Discard(x.Me, await x.PickN(x.My.Hand.Where(c => c.Data.IsMonster && c.Data.Race == "Dragon"), 1, 1, "Discard 1 Dragon"), x.Self);
            s.Run = async x =>
            {
                if (!Up(x)) return;
                var self = x.Self; var d = x.D;
                self.Vars["modatk"] = self.Var("modatk") + 1000; self.Vars["moddef"] = self.Var("moddef") + 1000; d.Seq++;
                bool done = false;
                d.Watch(d.Turn, async ev => { if (!done && ev.Kind == EvKind.BattleEnd) { done = true; self.Vars["modatk"] = self.Var("modatk") - 1000; self.Vars["moddef"] = self.Var("moddef") - 1000; d.Seq++; } });
                d.Later(d.Turn, Phase.End, async () => { if (!done) { done = true; self.Vars["modatk"] = self.Var("modatk") - 1000; self.Vars["moddef"] = self.Var("moddef") - 1000; d.Seq++; } });
            };
        }
        {
            static IEnumerable<Card> FZ(Ctx x) => x.My.Grave.Where(c => c.Data.IsMonster && c.Data.Race is "Fiend" or "Zombie");
            var s = Spell("Spirit Shield", async x => x.Self.Vars["stay"] = 1);
            s.On = async (x, e) =>
            {
                if (!x.Self.OnField || !x.Self.FaceUp) return;
                if (e.Kind == EvKind.AttackDeclared && e.Player == x.Opp && FZ(x).Any() && await x.Ask("Spirit Shield: banish a Fiend/Zombie to negate the attack?"))
                { var c = await x.Pick(FZ(x), "Banish which?"); if (c != null) { await x.D.Banish(c, x.Self); x.D.AttackNegated = true; } }
                if (e.Kind == EvKind.Activated && e.Link is { } l && l.Player == x.Opp && DestroyText(l.Card.Data) && Hopt(x, "neg") && FZ(x).Any() && await x.Ask("Spirit Shield: banish a Fiend/Zombie to negate that activation?"))
                { UseHopt(x, "neg"); var c = await x.Pick(FZ(x), "Banish which?"); if (c != null) { await x.D.Banish(c, x.Self); await x.D.Negate(l, destroy: false); } }
                if (e.Kind == EvKind.EndPhase && !x.MyMonsters().Any(m => m.FaceUp && x.D.Race(m) is "Fiend" or "Zombie") && x.Self.Var("ssend") != x.D.Turn)
                { x.Self.Vars["ssend"] = x.D.Turn; await x.D.ToGrave(x.Self, x.Self); }
            };
            s.Note = "Its negation answers any opponent's card whose text destroys.";
        }
        {
            var s = Spell("Spiritual Energy Settle Machine", async x => x.Self.Vars["stay"] = 1);
            s.On = async (x, e) =>
            {
                if (e.Kind == EvKind.EndPhase && e.Player == x.Me && x.Self.OnField && x.Self.FaceUp)
                {
                    if (x.My.Hand.Count > 0 && await x.Ask("Discard 1 card to keep Spiritual Energy Settle Machine?")) await x.D.Discard(x.Me, await x.PickN(x.My.Hand, 1, 1, "Discard 1 card"), x.Self);
                    else await x.D.Destroy(x.Self, x.Self);
                }
                if (e.Kind == EvKind.LeftField && e.Card == x.Self && x.D.LeftFieldLog.LastOrDefault(l => l.c == x.Self) is { c: not null } lf && lf.faceUp)
                    foreach (var m in x.D.FaceUpMonsters.Where(m => Spirits.Contains(m.Name)).ToList()) await x.D.ToHand(m, x.Self);
            };
        }
        Spell("Spiritual Water Art - Aoi", async x =>
        {
            x.D.Reveal(x.Me, x.Their.Hand);
            var c = await x.Pick(x.Their.Hand, "Send which card to the GY?");
            if (c != null) await x.D.ToGrave(c, x.Self);
        }, x => x.Their.Hand.Count > 0 && x.D.Tributable(x.Me).Any(m => m.FaceUp ? x.IsAttr(m, "WATER") : m.Data.Attribute == "WATER"),
        async x => { var t = await x.Pick(x.D.Tributable(x.Me).Where(m => m.FaceUp ? x.IsAttr(m, "WATER") : m.Data.Attribute == "WATER"), "Tribute 1 WATER monster"); if (t != null) await x.D.Tribute(t, x.Self); });
        {
            var s = Spell("Spring of Rebirth", async x => x.Self.Vars["stay"] = 1);
            s.On = async (x, e) => { if (e.Kind == EvKind.ToHand && x.Self.OnField && x.Self.FaceUp && e.From == Loc.Monster) await x.D.Gain(x.Me, 500, x.Self); };
        }
        Spell("Stamping Destruction", async x =>
        {
            if (x.T0 is not { OnField: true } t) return;
            int p = t.Controller;
            await x.D.Destroy(t, x.Self);
            if (!t.OnField) await x.D.Damage(p, 500, x.Self);
        }, x => x.MyMonsters().Any(m => m.FaceUp && x.IsRace(m, "Dragon")) && x.Targetable(x.AllSpells().Where(c => c != x.Self)).Any(),
        async x => await x.Target(x.AllSpells().Where(c => c != x.Self), "Destroy which Spell/Trap?"));
        {
            var s = Spell("Statue of the Wicked", async x => { }, x => false);
            s.On = async (x, e) =>
            {
                if (!e.Is(EvKind.Destroyed, x.Self) || x.Self.Loc != Loc.Grave || x.D.LeftFieldLog.LastOrDefault(l => l.c == x.Self) is not { c: not null } l || l.faceUp || x.D.FreeMonsterZones(x.Me) == 0) return;
                var t = x.D.MakeToken("Wicked Token", x.Me, 1000, 1000, "DARK", "Fiend", 4);
                await x.D.SpecialSummon(t, x.Me, await x.D.ChoosePosition(x.Me, t), by: x.Self);
            };
            s.Note = "It has no activation: it works when your Set copy is destroyed.";
        }
        Spell("Staunch Defender", async x =>
        {
            if (x.T0 is not { Loc: Loc.Monster, FaceUp: true } t) return;
            x.D.Flags[$"staunch{x.Opp}"] = t.Uid + 1; x.D.Flags[$"staunchturn{x.Opp}"] = x.D.Turn;
            if (x.D.Attacker is { Loc: Loc.Monster } a && a.Controller == x.Opp && x.D.AttackTarget != t) x.D.AttackTarget = t;
            foreach (var m in x.OppMonsters().Where(m => m.FaceUp)) m.Vars["mustattack"] = x.D.Turn;
        }, x => AttackOnMe(x) && x.Targetable(x.MyMonsters().FaceUp()).Any(), async x => await x.Target(x.MyMonsters().FaceUp(), "Select which monster?"));
        S("Stealthroid").On = async (x, e) =>
        {
            if (e.Kind == EvKind.AttackDeclared && e.Card == x.Self) x.Self.Vars["stealthatk"] = x.D.Turn;
            if (e.Kind == EvKind.BattleEnd && Up(x) && x.Self.Var("stealthatk") == x.D.Turn && x.MyMonsters().Any(m => m != x.Self && m.FaceUp && m.Name.Contains("roid")) && x.AllSpells().Any())
            { var t = await x.Pick(x.AllSpells(), "Destroy which Spell/Trap?"); if (t != null) await x.D.Destroy(t, x.Self); }
        };
        S("Steamroid").CalcAtk = (x, c, o, atk) => c != x.Self || o == null || !Up(x) ? atk : x.D.Attacker == x.Self ? atk + 500 : x.D.AttackTarget == x.Self ? atk - 500 : atk;
        S("Stone Statue of the Aztecs").BattleDamage = (x, src, to, dmg) => Up(x) && x.D.AttackTarget == x.Self && to == x.Opp ? dmg * 2 : dmg;
        Spell("Stray Lambs", async x =>
        {
            for (int i = 0; i < 2 && x.D.FreeMonsterZones(x.Me) > 0; i++) { var t = x.D.MakeToken("Lamb Token", x.Me, 0, 0, "EARTH", "Beast", 1); await x.D.SpecialSummon(t, x.Me, true, by: x.Self); }
            var d = x.D; int me = x.Me;
            d.Linger(d.Turn, (q, c, p) => q is Q.NormalSummon or Q.SpecialSummon or Q.FlipSummon && p == me);
        }, x => x.D.FreeMonsterZones(x.Me) >= 2);
        {
            var s = S("Strike Ninja");
            s.Speed = 2; s.AnyTurn = true;
            s.Can = x => Up(x) && Hopt(x) && x.My.Grave.Count(c => c.Data.IsMonster && c.Data.Attribute == "DARK") >= 2;
            s.Cost = async x => { UseHopt(x); foreach (var c in await x.PickN(x.My.Grave.Where(c => c.Data.IsMonster && c.Data.Attribute == "DARK"), 2, 2, "Banish 2 DARK monsters")) await x.D.Banish(c, x.Self); };
            s.Run = async x =>
            {
                if (!Up(x)) return;
                var self = x.Self; var d = x.D; int me = x.Me;
                await d.Banish(self, self);
                if (self.Loc == Loc.Banished) d.Later(d.Turn, Phase.End, async () => { if (self.Loc == Loc.Banished && d.CanSpecialSummon(self, me, by: self, ignore: true)) await d.SpecialSummon(self, me, false, by: self, ignore: true); });
            };
        }
        {
            var s = Spell("Stronghold the Moving Fortress", async x => await TrapMonster(x, 0, 2000, "EARTH", "Machine", 4, defense: true));
            s.Stats = (x, c, st) => { if (c == x.Self && x.Self.Loc == Loc.Monster && new[] { "Green Gadget", "Red Gadget", "Yellow Gadget" }.All(n => x.MyMonsters().Any(m => m.FaceUp && Scripts.NameOf(m) == n))) st.Add(3000, 0); };
        }
        {
            var s = DirectAttacker("Submarineroid");
            s.BattleDamage = (x, src, to, dmg) => src == x.Self && to == x.Opp && x.D.AttackTarget == null && dmg > 0 ? Math.Max(0, x.Self.Data.Atk) : dmg;
            s.On = async (x, e) => { if (e.Kind == EvKind.AfterDamage && (e.Card == x.Self || e.Other == x.Self) && Up(x) && x.Self.Attack && await x.Ask("Change Submarineroid to Defense Position?")) await x.D.ChangePosition(x.Self, true); };
        }
        Spell("Subspace Battle", async x =>
        {
            var d = x.D; var names = new HashSet<string>();
            for (int i = 0; i < 3; i++)
            {
                var mine = x.My.Deck.Where(c => c.Data.IsMonster && c.Data.Atk >= 0).ToList();
                var theirs = x.Their.Deck.Where(c => c.Data.IsMonster && c.Data.Atk >= 0).ToList();
                if (mine.Count == 0 || theirs.Count == 0) break;
                var a = await x.Pick(mine, $"Subspace Battle {i + 1}/3: reveal 1 monster from your Deck");
                var b = await d.PickOne(x.Opp, theirs, $"Subspace Battle {i + 1}/3: reveal 1 monster from your Deck", x.Self);
                if (a == null || b == null) break;
                d.Reveal(x.Opp, a); d.Reveal(x.Me, b);
                names.Add(a.Name); names.Add(b.Name);
                if (a.Data.Atk == b.Data.Atk) { d.Shuffle(x.Me); d.Shuffle(x.Opp); continue; }
                var (win, lose) = a.Data.Atk > b.Data.Atk ? (a, b) : (b, a);
                await d.ToHand(win, x.Self);
                await d.ToGrave(lose, x.Self);
                await d.Damage(lose.Owner, 500, x.Self);
                d.Shuffle(x.Me); d.Shuffle(x.Opp);
            }
            d.Linger(d.Turn, (q, c, p) => q == Q.Activate && c != null && c.Data.IsMonster && names.Contains(c.Name));
        }, x => Hopt(x) && x.My.Deck.Any(c => c.Data.IsMonster && c.Data.Atk >= 0) && x.Their.Deck.Any(c => c.Data.IsMonster && c.Data.Atk >= 0), async x => UseHopt(x));
        S("Subspace Battle").Note = "The lower-ATK monster is sent from the Deck to the GY (\"destroyed\").";
        {
            var s = Spell("Summon Cloud", async x => x.Self.Vars["stay"] = 1);
            static IEnumerable<Card> Ok(Ctx x) => x.My.Hand.Concat(x.My.Grave).Concat(x.Their.Grave).Where(c => c.Data.IsMonster && c.In("Cloudian") && c.Data.Level <= 4 && x.D.CanSpecialSummon(c, x.Me, by: x.Self));
            s.AddFx(f =>
            {
                f.Label = "Special Summon a Cloudian"; f.InSpellZone = true;
                f.Can = x => x.Self.FaceUp && Hopt(x) && !x.MyMonsters().Any() && Ok(x).Any();
                f.Cost = async x => UseHopt(x);
                f.Run = async x =>
                {
                    var c = await x.Pick(Ok(x), "Special Summon which Cloudian?");
                    if (c == null) return;
                    bool gy = c.Loc == Loc.Grave;
                    if (await x.D.SpecialSummon(c, x.Me, await x.D.ChoosePosition(x.Me, c), by: x.Self) && gy && x.Self.OnField) await x.D.Destroy(x.Self, x.Self);
                };
            });
        }
        Spell("Summon Dice", async x =>
        {
            int r = x.D.Die(x.Me, x.Self);
            if (r <= 2) x.My.ExtraSummons++;
            else if (r <= 4) await SummonPick(x, x.My.Grave.Where(c => c.Data.IsMonster), "Special Summon a monster from your GY?", optional: true);
            else await SummonPick(x, x.My.Hand.Where(c => c.Data.IsMonster && c.Data.Level >= 5), "Special Summon a Level 5 or higher monster from your hand?", optional: true);
        }, x => Hopt(x) && x.My.Lp > 1000, async x => { UseHopt(x); x.D.Pay(x.Me, 1000, x.Self); });
        S("Summon Dice").Note = "On 1 or 2 you get one more Normal Summon this turn.";
        {
            var s = Spell("Summon Storm", async x => await SummonPick(x, x.My.Hand.Where(c => c.Data.IsMonster && c.Data.Attribute == "WIND" && c.Data.Level <= 6), "Special Summon a WIND monster"),
                x => x.My.Lp > 800 && x.D.FreeMonsterZones(x.Me) > 0 && x.My.Hand.Any(c => c.Data.IsMonster && c.Data.Attribute == "WIND" && c.Data.Level <= 6 && x.D.CanSpecialSummon(c, x.Me, by: x.Self)),
                async x => x.D.Pay(x.Me, 800, x.Self));
            s.GraveFx = new Script
            {
                Can = x => Hopt(x, "gy") && x.Self.Var("gyturn") != x.D.Turn && x.D.TurnPlayer == x.Me && x.D.Phase is Phase.Main1 or Phase.Main2 && x.D.FreeMonsterZones(x.Me) > 0
                    && x.My.Hand.Any(c => c.Data.IsMonster && c.Data.Attribute == "WIND" && c.Data.Level <= 4 && x.D.CanSpecialSummon(c, x.Me, by: x.Self)),
                Cost = async x => { UseHopt(x, "gy"); await x.D.Banish(x.Self, x.Self); },
                Run = async x => await SummonPick(x, x.My.Hand.Where(c => c.Data.IsMonster && c.Data.Attribute == "WIND" && c.Data.Level <= 4), "Special Summon a Level 4 or lower WIND monster"),
            };
        }
        {
            var s = S("Super Conductor Tyranno");
            s.OncePerTurn = true;
            s.Can = x => Up(x) && x.D.Tributable(x.Me).Any();
            s.Cost = async x => { var t = await x.Pick(x.D.Tributable(x.Me), "Tribute 1 monster"); if (t != null) await x.D.Tribute(t, x.Self); x.Self.Vars["noattack"] = x.D.Turn; };
            s.Run = async x => await x.D.Damage(x.Opp, 1000, x.Self);
        }
        Spell("Super Junior Confrontation", async x =>
        {
            x.D.AttackNegated = true;
            var a = x.OppMonsters().Where(m => m.FaceUp && m.Attack).OrderBy(m => x.Atk(m)).FirstOrDefault();
            var t = x.MyMonsters().Where(m => m.FaceUp && m.Defense).OrderBy(m => x.Def(m)).FirstOrDefault();
            var d = x.D;
            if (a != null && t != null)
            {
                d.ForcedBattles.Add((a, t));
                bool done = false;
                d.Watch(d.Turn, async ev => { if (!done && ev.Kind == EvKind.AfterDamage && ev.Card == a) { done = true; d.TurnFlags.Add("endbattle"); } });
            }
            else d.TurnFlags.Add("endbattle");
        }, AttackOnMe);
        S("Super Junior Confrontation").Note = "Ties for lowest ATK/DEF take the first monster found.";
        {
            var s = Spell("Super Polymerization", async x => await x.D.FusionSummon(x.Me, x.Self, pool: x.D.AllMonsters),
                x => x.My.Hand.Any(c => c != x.Self) && x.D.FusionOptions(x.Me, pool: x.D.AllMonsters, by: x.Self).Any(),
                async x => await x.D.Discard(x.Me, await x.PickN(x.My.Hand.Where(c => c != x.Self), 1, 1, "Discard 1 card"), x.Self));
            s.NoResponse = true;
        }
        Spell("Super Rejuvenation", async x =>
        {
            var d = x.D; int me = x.Me;
            d.Later(d.Turn, Phase.End, async () =>
            {
                int n = d.Cards.Count(c => c.Data.IsMonster && (c.AsMonster ?? c.Data).Race == "Dragon" && c.Owner == me
                    && (d.Flags.GetValueOrDefault($"discarded{c.Uid}") == d.Turn || d.Flags.GetValueOrDefault($"tributed{c.Uid}") == d.Turn));
                if (n > 0) await d.Draw(me, n);
            });
        });
        {
            var s = S("Super Roboyarou");
            s.CalcAtk = (x, c, o, atk) => c == x.Self && o != null ? atk + 1000 : atk;
            s.Can = x => Up(x) && x.Self.SummonTurn != x.D.Turn && x.My.Extra.Any(c => c.Name == "Super Robolady");
            s.Run = async x =>
            {
                if (!Up(x)) return;
                await x.D.MoveTo(x.Self, Loc.Extra, by: x.Self);
                var r = x.My.Extra.FirstOrDefault(c => c.Name == "Super Robolady");
                if (r != null && x.Self.Loc == Loc.Extra && x.D.CanSpecialSummon(r, x.Me, by: x.Self)) await x.D.SpecialSummon(r, x.Me, await x.D.ChoosePosition(x.Me, r), by: x.Self);
            };
        }
        {
            var s = S("Super Vehicroid - Stealth Union");
            s.Allow = (x, q, c) => q == Q.Piercing && c == x.Self;
            static Card? Held(Ctx x) => x.D.EquipsOf(x.Self).FirstOrDefault(e => e.Var("stealthunion") == 1);
            s.OncePerTurn = true;
            s.Can = x => Up(x) && x.My.FreeSpellZones > 0 && x.Targetable(x.D.FaceUpMonsters.Where(m => m != x.Self && !x.IsRace(m, "Machine"))).Any();
            s.Cost = async x => await x.Target(x.D.FaceUpMonsters.Where(m => m != x.Self && !x.IsRace(m, "Machine")), "Equip which monster?");
            s.Run = async x =>
            {
                if (x.T0 is not { Loc: Loc.Monster, FaceUp: true } t || !Up(x) || x.My.FreeSpellZones == 0) return;
                t.ResetFieldState();
                x.D.Place(t, x.Me, Loc.Spell); t.FaceUp = true;
                await x.D.Equip(t, x.Self);
                t.Vars["stealthunion"] = 1;
            };
            s.On = async (x, e) =>
            {
                if (Up(x) && Held(x) != null && x.Self.Var("attackeach") != x.D.Turn) x.Self.Vars["attackeach"] = x.D.Turn;
                if (e.Kind == EvKind.AttackDeclared && e.Card == x.Self && Up(x)) SetBaseAtk(x, x.Self, Math.Max(0, x.Self.Data.Atk) / 2);
            };
        }
        S("Super Vehicroid Jumbo Drill").Allow = (x, q, c) => q == Q.Piercing && c == x.Self;
        Spell("Survival Instinct", async x => await x.D.Gain(x.Me, 400 * x.Self.Var("dinos"), x.Self),
            x => x.My.Grave.Any(c => c.Data.IsMonster && c.Data.Race == "Dinosaur"),
            async x =>
            {
                var l = await x.PickN(x.My.Grave.Where(c => c.Data.IsMonster && c.Data.Race == "Dinosaur"), 1, x.My.Grave.Count(c => c.Data.IsMonster && c.Data.Race == "Dinosaur"), "Banish how many Dinosaurs?");
                foreach (var c in l) await x.D.Banish(c, x.Self);
                x.Self.Vars["dinos"] = l.Count;
            });
        {
            var s = S("Susa Soldier");
            ReturnsAtEnd(s);
            s.BattleDamage = (x, src, to, dmg) => src == x.Self && to == x.Opp ? dmg / 2 : dmg;
        }
        {
            var s = S("Swarm of Locusts");
            FlipDown(s);
            s.On = async (x, e) =>
            {
                if (e.Is(EvKind.Summoned, x.Self) && e.Arg == (int)SummonKind.Flip && x.Targetable(x.Spells(x.Opp)).Any())
                { var t = await x.Pick(x.Targetable(x.Spells(x.Opp)), "Destroy which Spell/Trap?"); if (t != null) await x.D.Destroy(t, x.Self); }
            };
        }
        Sure("Spell Canceller", "Spell Economics", "Spell Reproduction", "Spell Striker", "Spirit Barrier", "Spirit Caller", "Spirit of the Pharaoh", "Spiritual Water Art - Aoi", "Spring of Rebirth",
            "Stamping Destruction", "Statue of the Wicked", "Stone Statue of the Aztecs", "Stray Lambs", "Steamroid", "Super Conductor Tyranno", "Super Vehicroid Jumbo Drill", "Survival Instinct", "Susa Soldier", "Swarm of Locusts");
    }
}
