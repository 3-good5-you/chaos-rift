namespace PoC.Engine;

// Layer 3, part D: hand-written effects (Joey pool).
public static partial class Scripts
{
    static async Flow UpkeepOrDestroy(Ctx x, int lp, string name)
    {
        if (x.Self.In("Archfiend") && x.D.CountActive("Pandemonium") > 0) return;   // Pandemonium: no Archfiend Standby costs
        if (x.My.Lp > lp && await x.Ask($"Pay {lp} LP to keep {name}?")) x.D.Pay(x.Me, lp, x.Self);
        else await x.D.Destroy(x.Self);
    }

    static Script SelfTribute(string name, Func<Ctx, bool> can, Func<Ctx, Flow> run)
    {
        var s = S(name);
        s.Can = can;
        s.Cost = async x => await x.D.MoveTo(x.Self, Loc.Grave);
        s.Run = run;
        return s;
    }

    static Script TributeN(string name, int n, Func<Ctx, Flow> run)
    {
        var s = S(name);
        s.Can = x => x.MyMonsters().Count(m => !x.D.Forbidden(Q.Tribute, m)) >= n;
        s.Cost = async x => { foreach (var t in await x.PickN(x.MyMonsters().Where(m => !x.D.Forbidden(Q.Tribute, m)), n, n, $"Tribute {n} monster(s)")) await x.D.MoveTo(t, Loc.Grave); };
        s.Run = run;
        return s;
    }

    static Script GraveSpirit(string name, string attr, int count)
    {
        var s = S(name);
        s.NoNormalSummon = true;
        s.ProcCan = x => x.Self.Loc == Loc.Hand && x.My.Grave.Count(c => c.Data.IsMonster && c.Data.Attribute == attr) >= count && CanBanishGrave(x);
        s.Proc = async x =>
        {
            foreach (var c in await x.PickN(x.My.Grave.Where(c => c.Data.IsMonster && c.Data.Attribute == attr), count, count, $"Banish {count} {attr} monster(s)"))
                await BanishFromGrave(x, c);
            await x.D.SpecialSummon(x.Self, x.Me, await x.D.ChoosePosition(x.Me, x.Self));
        };
        return s;
    }

    static void CardsD(CardDb db)
    {
        Spell("Gamble", async x =>
        {
            if (await x.D.CallCoin(x.Me, x.Self)) { int n = 5 - x.My.Hand.Count; if (n > 0) await x.D.Draw(x.Me, n); }
            else x.D.Flags[$"skipturn{x.Me}"] = 1;
        }, x => x.Their.Hand.Count >= 6 && x.My.Hand.Count <= 2);
        Continuous("Solemn Wishes").On = async (x, e) => { if (e.Kind == EvKind.Drew && e.Player == x.Me && x.Self.OnField) await x.D.Gain(x.Me, 500, x.Self); };
        Continuous("Skull Invitation").On = async (x, e) => { if (e.Kind == EvKind.ToGrave && x.Self.OnField) await x.D.Damage(e.Card!.Owner, 300, x.Self); };
        Flip("Bubonic Vermin", async x =>
        {
            var t = await x.Pick(x.My.Deck.Where(c => c.Name == "Bubonic Vermin"), "Special Summon Bubonic Vermin face-down?", optional: true);
            if (t != null) await x.D.SpecialSummon(t, x.Me, true, faceUp: false, by: x.Self);
            x.D.Shuffle(x.Me);
        });
        {
            var s = Spell("Burning Land", async x => await x.D.Destroy(x.D.Players.Where(p => p.FieldZone != null).Select(p => p.FieldZone!).ToList(), x.Self));
            s.Run = async x => await x.D.Destroy(x.D.Players.Where(p => p.FieldZone != null).Select(p => p.FieldZone!).ToList(), x.Self);
            s.On = async (x, e) => { if (e.Kind == EvKind.Standby && x.Self.OnField) await x.D.Damage(e.Player, 500, x.Self); };
        }
        Spell("Cold Wave", async x => x.D.Linger(x.D.Turn + 1, (q, c, p) => q is Q.Activate or Q.SetSpell && c != null && !c.Data.IsMonster && !c.OnField || q == Q.Activate && c != null && !c.Data.IsMonster && c.OnField && !c.FaceUp),
            x => x.D.Phase == Phase.Main1);
        {
            var s = S("Fairy Meteor Crush");
            s.EquipOk = (x, m) => true;
            s.Allow = (x, q, c) => q == Q.Piercing && c == x.Self.EquippedTo;
        }
        Spell("Limiter Removal", async x =>
        {
            var l = x.MyMonsters().FaceUp().Where(m => x.IsRace(m, "Machine")).ToList();
            foreach (var m in l) x.D.Boost(m, x.Atk(m));
            x.D.Later(x.D.Turn, Phase.End, async () => await x.D.Destroy(l.Where(m => m.Valid()).ToList(), x.Self));
        }, x => x.MyMonsters().FaceUp().Any(m => x.IsRace(m, "Machine")));
        Spell("Shift", async x =>
        {
            var e = x.Ev!;
            var old = e.Kind == EvKind.AttackDeclared ? e.Other : e.Link!.X.T0;
            var n = await x.Pick(x.MyMonsters().Where(m => m != old), "Move the target to which monster?");
            if (n == null) return;
            if (e.Kind == EvKind.AttackDeclared) x.D.AttackTarget = n; else e.Link!.X.T[0] = n;
        }, x => x.MyMonsters().Count() >= 2 && (
            x.Ev is { Kind: EvKind.AttackDeclared } a && a.Player == x.Opp && a.Other?.Controller == x.Me ||
            OppActivated(x, l => !l.Card.Data.IsMonster && l.X.T.Count == 1 && l.X.T0!.Loc == Loc.Monster && l.X.T0.Controller == x.Me)));
        Spell("Insect Imitation", async x =>
        {
            await x.Recruit(c => c.Data.Race == "Insect" && c.Data.Level == x.N + 1, $"Special Summon a Level {x.N + 1} Insect", faceDownAllowed: true);
        }, x => x.MyMonsters().Any(m => !x.D.Forbidden(Q.Tribute, m)),
        async x => { var t = await x.Pick(x.MyMonsters().Where(m => !x.D.Forbidden(Q.Tribute, m)), "Tribute which monster?"); if (t != null) { x.N = x.D.Level(t); await x.D.MoveTo(t, Loc.Grave); } });
        Spell("Dimensionhole", async x =>
        {
            if (x.T0 is not { } t || !t.Valid()) return;
            int me = x.Me, zone = t.Zone;
            await x.D.Banish(t, x.Self);
            x.D.Flags[$"blocked{me}"] = x.D.Flags.GetValueOrDefault($"blocked{me}") | (1 << zone);
            int next = x.D.TurnPlayer == me ? x.D.Turn + 2 : x.D.Turn + 1;
            x.D.Later(next, Phase.Standby, async () =>
            {
                x.D.Flags[$"blocked{me}"] &= ~(1 << zone);
                if (t.Loc == Loc.Banished) await x.D.SpecialSummon(t, me, false, by: x.Self);
            });
        }, x => x.MyMonsters().Any(), async x => await x.Target(x.MyMonsters(), "Banish which of your monsters?"));
        {
            var s = S("Ground Collapse");
            s.Can = x => x.D.Players.Sum(p => x.D.FreeMonsterZones(p.Index)) >= 2;
            s.Run = async x =>
            {
                for (int i = 0; i < 2; i++)
                {
                    // Arg = player * 8 + zone: the field picks it by clicking the zone itself (DuelField ZonePick).
                    var opts = new List<Opt>();
                    for (int p = 0; p < 2; p++) for (int z = 0; z < 5; z++)
                            if (x.D.P(p).Monsters[z] == null && (x.D.Flags.GetValueOrDefault($"blocked{p}") & (1 << z)) == 0)
                                opts.Add(new Opt(Act.Option, Arg: p * 8 + z, Text: $"{(p == x.Me ? "Your" : "Opponent's")} zone {z + 1}"));
                    if (opts.Count == 0) break;
                    var o = await x.D.Choose(x.Me, Ask.Choose, opts, "Ground Collapse: block which zone?", x.Self);
                    var k = (p: o.Arg / 8, z: o.Arg % 8);
                    x.D.Flags[$"blocked{k.p}"] = x.D.Flags.GetValueOrDefault($"blocked{k.p}") | (1 << k.z);
                    x.Self.Vars[$"z{i}"] = k.p * 8 + k.z + 1;
                }
            };
            s.On = async (x, e) =>
            {
                if (!e.Is(EvKind.LeftField, x.Self)) return;
                foreach (var key in new[] { $"z0", $"z1" })
                    if (x.D.Flags.TryGetValue($"gc{x.Self.Uid}{key}", out var v) && v > 0) { int p = (v - 1) / 8, z = (v - 1) % 8; x.D.Flags[$"blocked{p}"] &= ~(1 << z); }
            };
            // Vars are cleared when the card leaves the field, so mirror them into Flags for the cleanup above.
            var run = s.Run;
            s.Run = async x => { await run(x); for (int i = 0; i < 2; i++) x.D.Flags[$"gc{x.Self.Uid}z{i}"] = x.Self.Var($"z{i}"); };
        }
        Spell("Magic Drain", async x =>
        {
            var spells = x.Their.Hand.Where(c => c.Data.IsSpell).ToList();
            if (spells.Count > 0 && await x.D.YesNo(x.Opp, "Magic Drain: discard 1 Spell to keep your Spell?", x.Self))
            {
                var c = await x.D.PickOne(x.Opp, spells, "Discard which Spell?", x.Self);
                if (c != null) { await x.D.Discard(x.Opp, new[] { c }, x.Self); return; }
            }
            await x.D.Negate(x.Ev!.Link!);
        }, x => OppActivated(x, l => l.Card.Data.IsSpell && l.IsCardActivation));
        Continuous("Infinite Dismissal").On = async (x, e) =>
        {
            if (e.Kind == EvKind.EndPhase && x.Self.OnField)
                await x.D.Destroy(x.D.FaceUpMonsters.Where(m => m.SummonTurn == x.D.Turn && m.Summoned is SummonKind.Normal or SummonKind.Flip && m.Data.Level <= 3).ToList(), x.Self);
        };
        Continuous("Gravity Bind").Forbid = (x, q, c) => q == Q.Attack && c != null && c.Data.Level >= 4;
        Continuous("Type Zero Magic Crusher").On = async (x, e) =>
        {
            if (e.Kind == EvKind.Discarded && e.Player == x.Me && x.Self.OnField) await x.D.Damage(x.Opp, 500 * e.Cards!.Count(c => c.Data.IsSpell), x.Self);
        };
        Spell("Shadow of Eyes", async x => { var m = x.Ev!.Card!; if (m.Valid() && !m.FaceUp) await x.D.ChangePosition(m, false, true, flipEffect: false); },
            x => x.Ev is { Kind: EvKind.Set } e && e.Player == x.Opp && e.Card!.Valid() && e.Card.Data.IsMonster);
        {
            static bool Umi(Ctx x) => x.D.CountActive("Umi") > 0;
            S("The Legendary Fisherman").Forbid = (x, q, c) => q is Q.AffectedBySpell or Q.BeAttacked && c == x.Self && Umi(x);
            S("Deepsea Warrior").Forbid = (x, q, c) => q == Q.AffectedBySpell && c == x.Self && Umi(x);
            var tw = Spell("Tornado Wall", async x => { }, Umi);
            tw.Run = null;
            tw.Forbid = (x, q, c) => q == Q.TakeDamage && x.D.QueryPlayer == x.Me && Umi(x);
            tw.On = async (x, e) => { if (e.Kind == EvKind.LeftField && e.Card!.Name == "Umi" && x.Self.OnField && !Umi(x)) await x.D.Destroy(x.Self); };
        }
        {
            var s = S("Sword Hunter");
            s.On = async (x, e) =>
            {
                // TCG text: the monsters it destroyed this Battle Phase are equipped at its end.
                if (e.Kind == EvKind.BattleDestroyedMonster && e.Card == x.Self && e.Other is { Loc: Loc.Grave } v && x.Self.Valid()) v.Vars["swordhunter"] = x.Self.Uid + 1;
                else if (e.Kind == EvKind.BattleEnd)
                    foreach (var w in x.D.Players.SelectMany(p => p.Grave).Where(c => c.Var("swordhunter") == x.Self.Uid + 1).ToList())
                    {
                        w.Vars["swordhunter"] = 0;
                        if (!x.Self.Valid() || x.My.FreeSpellZones == 0) continue;
                        x.D.Place(w, x.Me, Loc.Spell);
                        await x.D.Equip(w, x.Self);
                    }
            };
            s.Stats = (x, c, st) => { if (c == x.Self) st.Add(200 * x.D.EquipsOf(c).Count(e => e.Data.IsMonster), 0); };
        }
        OnBattleDamage("Drill Bug", async (x, e) =>
        {
            var t = await x.Pick(x.My.Deck.Where(c => c.Name == "Parasite Paracide"), "Put Parasite Paracide on top of your Deck?", optional: true);
            x.D.Shuffle(x.Me);
            if (t != null) { x.My.Deck.Remove(t); x.My.Deck.Insert(0, t); }
        });
        Flip("Bite Shoes", async x => { var t = await x.Pick(x.Targetable(x.D.FaceUpMonsters), "Change the position of which monster?"); if (t != null) await x.D.ChangePosition(t, !t.Defense); });
        S("Hayabusa Knight").Allow = (x, q, c) => q == Q.DoubleAttack && c == x.Self;
        Flip("Bombardment Beetle", async x =>
        {
            var t = await x.Pick(x.Targetable(x.OppMonsters().FaceDown()), "Flip which monster?");
            if (t == null) return;
            await x.D.Flip(t, triggerFlip: false);
            if (t.Data.IsEffect) await x.D.Destroy(t, x.Self); else await x.D.SetFaceDown(t);
        });
        Flip("4-Starred Ladybug of Doom", async x => await x.D.Destroy(x.OppMonsters().FaceUp().Where(m => x.D.Level(m) == 4).ToList(), x.Self));
        S("Red-Moon Baby").On = async (x, e) =>
        {
            if (e.Kind == EvKind.BattleDestroyedMonster && e.Card == x.Self && e.Other != null && x.Self.Valid()) x.Self.Vars["victim"] = e.Other.Uid + 1;
            else if (e.Kind == EvKind.BattleEnd && x.Self.Var("victim") > 0)
            {
                var v = x.D.CardByUid(x.Self.Var("victim") - 1);
                x.Self.Vars["victim"] = 0;
                if (v.Loc == Loc.Grave && x.D.FreeMonsterZones(x.Me) > 0 && await x.Ask($"Special Summon {v.Name} to your side?"))
                    await x.D.SpecialSummon(v, x.Me, await x.D.ChoosePosition(x.Me, v), by: x.Self);
            }
        };
        S("Mad Sword Beast").Allow = (x, q, c) => q == Q.Piercing && c == x.Self;
        S("Goblin Attack Force").On = async (x, e) =>
        {
            if (e.Kind == EvKind.BattleEnd && x.Self.Valid() && x.Self.Attacks > 0)
            {
                await x.D.ChangePosition(x.Self, true);
                x.Self.Vars["nochangepos"] = x.D.Turn + 2;
            }
        };
        S("Gearfried the Iron Knight").On = async (x, e) => { if (e.Kind == EvKind.Equipped && e.Other == x.Self && e.Card!.OnField) await x.D.Destroy(e.Card, x.Self); };
        Continuous("Insect Barrier").Forbid = (x, q, c) => q == Q.Attack && c != null && c.Controller == x.Opp && x.D.Race(c) == "Insect";
        S("Revival Jam").On = async (x, e) =>
        {
            if (!e.Is(EvKind.Destroyed, x.Self) || !e.Battle || x.My.Lp <= 1000) return;
            if (!await x.Ask("Pay 1000 LP to revive Revival Jam next Standby Phase?")) return;
            x.D.Pay(x.Me, 1000);
            int me = x.Me;
            var jam = x.Self;
            int next = x.D.TurnPlayer == me ? x.D.Turn + 2 : x.D.Turn + 1;
            x.D.Later(next, Phase.Standby, async () =>
            {
                if (jam.Loc == Loc.Grave && await x.D.SpecialSummon(jam, me, true, by: jam)) jam.Vars["nochangepos"] = x.D.Turn;
            });
        };
        {
            var s = OnSummon("Nuvia the Wicked", async x => await x.D.Destroy(x.Self, x.Self), normalOnly: true);
            s.Stats = (x, c, st) => { if (c == x.Self) st.Add(-200 * x.OppMonsters().Count(), 0); };
        }
        Spell("Chosen One", async x =>
        {
            var m = await x.Pick(x.My.Hand.Where(c => c.Data.IsMonster && c != x.Self), "Choose 1 monster");
            var others = await x.PickN(x.My.Hand.Where(c => !c.Data.IsMonster && c != x.Self), 2, 2, "Choose 2 non-monster cards");
            if (m == null || others.Count < 2) return;
            var three = new List<Card> { m, others[0], others[1] };
            var picked = three[x.D.Rng.Int(3)]; // the opponent picks blind: uniform random
            if (picked == m && x.D.FreeMonsterZones(x.Me) > 0)
            {
                await x.D.SpecialSummon(m, x.Me, await x.D.ChoosePosition(x.Me, m), by: x.Self);
                foreach (var o in others) await x.D.MoveTo(o, Loc.Grave, by: x.Self);
            }
            else foreach (var c in three) await x.D.MoveTo(c, Loc.Grave, by: x.Self);
        }, x => x.My.Hand.Any(c => c.Data.IsMonster) && x.My.Hand.Count(c => !c.Data.IsMonster && c != x.Self) >= 2);
        Spell("Mask of Weakness", async x => { if (x.Ev!.Card!.Valid()) x.D.Boost(x.Ev.Card, -700); }, AttackOnMe);
        {
            var s = S("Mask of Dispel");
            s.Can = x => x.Targetable(x.AllSpells().Where(c => c.FaceUp && c.Data.IsSpell && c != x.Self)).Any();
            s.Cost = async x => await x.Target(x.AllSpells().Where(c => c.FaceUp && c.Data.IsSpell && c != x.Self), "Choose a face-up Spell");
            s.Run = async x => { if (x.T0 is { OnField: true } t) x.Self.Vars["target"] = t.Uid + 1; };
            s.On = async (x, e) =>
            {
                int u = x.Self.Var("target") - 1;
                if (u < 0 || !x.Self.OnField) return;
                var t = x.D.CardByUid(u);
                if (e.Kind == EvKind.Standby && t.OnField && e.Player == t.Controller) await x.D.Damage(t.Controller, 500, x.Self);
                else if (e.Is(EvKind.LeftField, t)) await x.D.Destroy(x.Self);
            };
        }
        Continuous("Mask of Restrict").Forbid = (x, q, c) => q == Q.Tribute;
        {
            var s = S("Mask of the Accursed");
            s.EquipOk = (x, m) => true;
            s.Forbid = (x, q, c) => q == Q.Attack && c == x.Self.EquippedTo;
            s.On = async (x, e) => { if (e.Kind == EvKind.Standby && e.Player == x.Me && x.Self.EquippedTo is { } m) await x.D.Damage(m.Controller, 500, x.Self); };
        }
        S("Mask of Brutality").On = async (x, e) => { if (e.Kind == EvKind.Standby && e.Player == x.Me && x.Self.EquippedTo != null) await UpkeepOrDestroy(x, 1000, "Mask of Brutality"); };
        Spell("Return of the Doomed", async x =>
        {
            int me = x.Me;
            x.D.Later(x.D.Turn, Phase.End, async () =>
            {
                var t = await x.D.PickOne(me, x.D.BattleDestroyedThisTurn(me), "Return which monster to your hand?", x.Self);
                if (t != null) await x.D.ToHand(t, x.Self);
            });
        }, x => x.My.Hand.Any(c => c.Data.IsMonster),
        async x => await x.D.Discard(x.Me, await x.PickN(x.My.Hand.Where(c => c.Data.IsMonster), 1, 1, "Discard 1 monster (cost)"), x.Self));
        {
            var s = S("Lightning Blade");
            s.Stats = (x, c, st) =>
            {
                if (x.Self.EquippedTo == c) st.Add(800, 0);
                if (x.Self.EquippedTo != null && st.Attr == "WATER") st.Add(-500, 0);
            };
        }
        {
            var s = Continuous("Fairy Box");
            s.On = async (x, e) =>
            {
                if (!x.Self.OnField) return;
                if (e.Kind == EvKind.AttackDeclared && e.Player == x.Opp && e.Card!.Valid() && await x.D.CallCoin(x.Me, x.Self)) x.D.Boost(e.Card, -x.Atk(e.Card));
                else if (e.Kind == EvKind.Standby && e.Player == x.Me) await UpkeepOrDestroy(x, 500, "Fairy Box");
            };
        }
        Spell("Torrential Tribute", async x => await x.D.Destroy(x.AllMonsters().ToList(), x.Self),
            x => x.Ev is { Kind: EvKind.Summoned } e && e.Card!.Valid());
        {
            var s = Continuous("Jam Breeding Machine");
            s.Forbid = (x, q, c) => q is Q.NormalSummon or Q.SetMonster or Q.SpecialSummon && c != null && !c.IsToken && (c.Controller == x.Me || (c.Loc == Loc.Hand && c.Owner == x.Me));
            s.On = async (x, e) =>
            {
                if (e.Kind == EvKind.Standby && e.Player == x.Me && x.Self.OnField && x.D.FreeMonsterZones(x.Me) > 0)
                {
                    var tok = x.D.MakeToken("Slime Token", x.Me, 500, 500, "WATER", "Aqua");
                    await x.D.SpecialSummon(tok, x.Me, false, by: x.Self);   // card text: in Attack Position
                }
            };
        }
        S("Infinite Cards").Allow = (x, q, c) => q == Q.NoHandLimit;
        Continuous("Jam Defender").On = async (x, e) =>
        {
            if (e.Kind != EvKind.AttackDeclared || e.Player != x.Opp || !x.Self.OnField) return;
            var jams = x.MyMonsters().FaceUp().Where(m => m.Name == "Revival Jam" && m != e.Other).ToList();
            if (jams.Count > 0 && await x.Pick(jams, "Jam Defender: switch the attack to which Revival Jam?", optional: true) is { } jam) x.D.AttackTarget = jam;
        };
        Continuous("Card of Safe Return").On = async (x, e) =>
        {
            if (e.Kind == EvKind.Summoned && e.Summoned(SummonKind.Special, SummonKind.Fusion, SummonKind.Ritual) && e.From == Loc.Grave && e.Card!.Owner == x.Me && x.Self.OnField
                && await x.Ask("Card of Safe Return: draw 1 card?"))
                await x.D.Draw(x.Me, 1);
        };
        SelfTribute("Lady Panther", x => x.D.BattleDestroyedThisTurn(x.Me).Any(), async x =>
        {
            var t = await x.Pick(x.D.BattleDestroyedThisTurn(x.Me), "Put which monster on top of your Deck?");
            if (t != null) await x.D.ToDeck(t);
        });
        MyStandby("The Unfriendly Amazon", async x =>
        {
            var t = await x.Pick(x.MyMonsters().Where(m => m != x.Self && !x.D.Forbidden(Q.Tribute, m)), "Tribute 1 monster to keep The Unfriendly Amazon", optional: true);
            if (t != null) await x.D.MoveTo(t, Loc.Grave); else await x.D.Destroy(x.Self);
        });
        TributeN("Amazon Archer", 2, async x => await x.D.Damage(x.Opp, 1200, x.Self));
        SelfTribute("Crimson Sentry", x => x.D.BattleDestroyedThisTurn(x.Me).Any(), async x =>
        {
            var t = await x.Pick(x.D.BattleDestroyedThisTurn(x.Me), "Return which monster to the bottom of your Deck?");
            if (t != null) await x.D.ToDeck(t, top: false);
        });
        S("Fire Princess").On = async (x, e) => { if (e.Kind == EvKind.Gain && e.Player == x.Me && x.OnFieldUp()) await x.D.Damage(x.Opp, 500, x.Self); };
        Flip("Lady Assailant of Flames", async x =>
        {
            if (x.My.Deck.Count < 3 || !await x.Ask("Banish the top 3 cards of your Deck to deal 800 damage?")) return;
            foreach (var c in x.My.Deck.Take(3).ToList()) await x.D.Banish(c, x.Self);
            await x.D.Damage(x.Opp, 800, x.Self);
        });
        Flip("Fire Sorcerer", async x =>
        {
            if (x.My.Hand.Count < 2 || !await x.Ask("Banish 2 random cards from your hand to deal 800 damage?")) return;
            for (int i = 0; i < 2; i++) await x.D.Banish(x.My.Hand[x.D.Rng.Int(x.My.Hand.Count)], x.Self);
            await x.D.Damage(x.Opp, 800, x.Self);
        });
        MyStandby("Spirit of the Breeze", async x => await x.D.Gain(x.Me, 1000, x.Self), x => x.OnFieldUp() && x.Self.Attack);
        MyStandby("Dancing Fairy", async x => await x.D.Gain(x.Me, 1000, x.Self), x => x.OnFieldUp() && x.Self.Defense);
        MyStandby("Cure Mermaid", async x => await x.D.Gain(x.Me, 800, x.Self));
        SelfTribute("Fairy Guardian", x => SentByOppThisTurn(x).Any(), async x =>
        {
            var t = await x.Pick(SentByOppThisTurn(x), "Put which Spell on the bottom of your Deck?");
            if (t != null) await x.D.ToDeck(t, top: false);
        });
        TributeN("Hysteric Fairy", 2, async x => await x.D.Gain(x.Me, 1000, x.Self));
        SelfTribute("The Forgiving Maiden", x => x.D.BattleDestroyedThisTurn(x.Me).Any(), async x =>
        {
            var t = await x.Pick(x.D.BattleDestroyedThisTurn(x.Me), "Return which monster to your hand?");
            if (t != null) await x.D.ToHand(t, x.Self);
        });
        MyStandby("Marie the Fallen One", async x => await x.D.Gain(x.Me, 200, x.Self), x => x.InGrave());
        {
            var s = S("Scroll of Bewitchment");
            s.EquipOk = (x, m) => true;
            s.Cost = async x => { await x.Target(x.D.EquipTargets(x.Self), "Equip to which monster?"); x.N = await x.D.ChooseText(x.Me, "Declare an Attribute", AttrList, x.Self); };
            s.Run = async x => { if (x.T0 is { } t && t.Valid() && t.FaceUp) { await x.D.Equip(x.Self, t); x.Self.Vars["attr"] = x.N + 1; } };
            s.Base = (x, c, st) => { if (x.Self.EquippedTo == c && x.Self.Var("attr") > 0) st.Attr = AttrList[x.Self.Var("attr") - 1]; };
        }
        {
            var s = S("United We Stand");
            s.EquipOk = (x, m) => true;
            s.Stats = (x, c, st) => { if (x.Self.EquippedTo == c) { int n = 800 * x.D.P(c.Controller).MonstersOnField.Count(m => m.FaceUp); st.Add(n, n); } };
        }
        {
            var s = S("Mage Power");
            s.EquipOk = (x, m) => true;
            s.Stats = (x, c, st) => { if (x.Self.EquippedTo == c) { int n = 500 * x.Spells(x.Me).Count(); st.Add(n, n); } };
        }
        {
            var s = S("Jowgen the Spiritualist");
            s.Can = x => x.My.Hand.Count > 0;
            s.Cost = async x => await x.D.DiscardRandom(x.Me, 1, x.Self);
            s.Run = async x => await x.D.Destroy(x.AllMonsters().Where(m => m.Summoned is SummonKind.Special or SummonKind.Fusion or SummonKind.Ritual).ToList(), x.Self);
            s.Forbid = (x, q, c) => q == Q.SpecialSummon;
        }
        {
            var s = OnBattleDamage("Kycoo the Ghost Destroyer", async (x, e) =>
            {
                foreach (var c in await x.PickN(x.Their.Grave.Where(c => c.Data.IsMonster), 0, 2, "Banish up to 2 monsters from your opponent's Graveyard")) await x.D.Banish(c, x.Self);
            });
        }
        Flip("Summoner of Illusions", async x =>
        {
            var tr = await x.Pick(x.MyMonsters().Where(m => m != x.Self && !x.D.Forbidden(Q.Tribute, m)), "Tribute 1 other monster?", optional: true);
            if (tr == null) return;
            await x.D.MoveTo(tr, Loc.Grave);
            var f = await x.Pick(x.My.Extra.Where(c => c.Data.IsFusion), "Special Summon which Fusion Monster?");
            if (f == null || !await x.D.SpecialSummon(f, x.Me, await x.D.ChoosePosition(x.Me, f), by: x.Self)) return;
            x.D.Later(x.D.Turn, Phase.End, async () => { if (f.Valid()) await x.D.Destroy(f, x.Self); });
        });
        {
            var s = S("Bazoo the Soul-Eater");
            s.OncePerTurn = true;
            s.Can = x => x.My.Grave.Count > 0 && CanBanishGrave(x);
            s.Cost = async x =>
            {
                var l = await x.PickN(x.My.Grave, 1, 3, "Banish up to 3 cards from your Graveyard");
                foreach (var c in l) await BanishFromGrave(x, c);
                x.N = l.Count;
            };
            s.Run = async x =>
            {
                if (!x.Self.Valid()) return;
                int n = 300 * x.N;
                var me = x.Self;
                me.Vars["modatk"] = me.Var("modatk") + n;
                x.D.Later(x.D.Turn + 1, Phase.End, async () => { if (me.Valid()) me.Vars["modatk"] = me.Var("modatk") - n; });
            };
        }
        {
            var s = S("Dark Necrofear");
            s.NoNormalSummon = true;
            s.ProcCan = x => x.Self.Loc == Loc.Hand && x.My.Grave.Count(c => c.Data.IsMonster && c.Data.Race == "Fiend") >= 3 && CanBanishGrave(x);
            s.Proc = async x =>
            {
                foreach (var c in await x.PickN(x.My.Grave.Where(c => c.Data.IsMonster && c.Data.Race == "Fiend"), 3, 3, "Banish 3 Fiends")) await BanishFromGrave(x, c);
                await x.D.SpecialSummon(x.Self, x.Me, await x.D.ChoosePosition(x.Me, x.Self));
            };
            s.On = async (x, e) =>
            {
                if (!e.Is(EvKind.Destroyed, x.Self) || !(e.Battle || e.Other?.Controller == 1 - x.Self.Owner)) return;
                var me = x.Self; int owner = me.Owner;
                x.D.Later(x.D.Turn, Phase.End, async () =>
                {
                    if (me.Loc != Loc.Grave || x.D.P(owner).FreeSpellZones == 0) return;
                    var t = await x.D.PickOne(owner, x.D.P(1 - owner).MonstersOnField.Where(m => m.FaceUp), "Dark Necrofear: possess which monster?", me);
                    if (t == null) return;
                    x.D.Place(me, owner, Loc.Spell);
                    me.FaceUp = true;
                    await x.D.Equip(me, t);
                    await x.D.TakeControl(t, owner);
                });
            };
        }
        {
            var s = GraveSpirit("Soul of Purity and Light", "LIGHT", 2);
            s.Stats = (x, c, st) => { if (c.Controller == x.Opp && x.D.Phase == Phase.Battle && x.D.TurnPlayer == x.Opp) st.Add(-300, 0); };
        }
        GraveSpirit("Spirit of Flames", "FIRE", 1).Stats = (x, c, st) => { if (c == x.Self && x.D.Phase == Phase.Battle && x.Turn()) st.Add(300, 0); };
        GraveSpirit("The Rock Spirit", "EARTH", 1).Stats = (x, c, st) => { if (c == x.Self && x.D.Phase == Phase.Battle && !x.Turn()) st.Add(300, 0); };
        static async Flow SpiritShift(Ctx x)
        {
            var t = await x.Pick(x.Targetable(x.OppMonsters().FaceUp()), "Change the position of which monster?", optional: true);
            if (t == null) return;
            await x.D.ChangePosition(t, !t.Defense);
            t.Vars["nochangepos"] = x.D.Turn;
        }
        GraveSpirit("Aqua Spirit", "WATER", 1).On = async (x, e) => { if (e.Kind == EvKind.Standby && e.Player == x.Opp && x.OnFieldUp()) await SpiritShift(x); };
        GraveSpirit("Garuda the Wind Spirit", "WIND", 1).On = async (x, e) => { if (e.Kind == EvKind.EndPhase && e.Player == x.Opp && x.OnFieldUp()) await SpiritShift(x); };
        OnSummon("Gilasaurus", async x =>
        {
            if (!await x.Ask("Treat Gilasaurus's summon as a Special Summon?")) return;
            x.Self.Summoned = SummonKind.Special;
            x.My.NormalSummoned = false;
            var t = await x.D.PickOne(x.Opp, x.Their.Grave.Where(c => c.Data.IsMonster), "Special Summon 1 monster from your Graveyard?", x.Self, optional: true);
            if (t != null) await x.D.SpecialSummon(t, x.Opp, await x.D.ChoosePosition(x.Opp, t), by: x.Self);
        }, normalOnly: true);
        Flip("Tornado Bird", async x => { foreach (var c in await x.PickN(x.Targetable(x.AllSpells()), 0, 2, "Return up to 2 Spell/Traps to the hand")) await x.D.ToHand(c, x.Self); });
        S("Dreamsprite").On = async (x, e) =>
        {
            if (e.Kind != EvKind.AttackDeclared || e.Other != x.Self || !x.Self.FaceUp) return;
            var t = await x.Pick(x.MyMonsters().Where(m => m != x.Self), "Redirect the attack to which monster?", optional: true);
            if (t != null) x.D.AttackTarget = t;
        };
        {
            var s = S("Zombyra the Dark");
            s.Forbid = (x, q, c) => q == Q.DirectAttack && c == x.Self;
            s.On = async (x, e) => { if (e.Kind == EvKind.BattleDestroyedMonster && e.Card == x.Self && x.Self.Valid()) x.Self.Vars["modatk"] = x.Self.Var("modatk") - 200; };
        }
        Flip("Supply", async x =>
        {
            foreach (var c in await x.PickN(x.My.Grave.Where(c => c.Var("fusionmat") == 1), 0, 2, "Add up to 2 Fusion Material monsters")) await x.D.ToHand(c, x.Self);
        });
        {
            var s = S("Maryokutai");
            s.Speed = 2; s.AnyTurn = true;
            s.Can = x => !x.Turn() && OppActivated(x, l => l.Card.Data.IsSpell && l.IsCardActivation);
            s.Cost = async x => await x.D.MoveTo(x.Self, Loc.Grave);
            s.Run = async x => await x.D.Negate(x.Ev!.Link!);
        }
        Spell("Collected Power", async x =>
        {
            if (x.T0 is not { } t || !t.Valid() || !t.FaceUp) return;
            foreach (var eq in x.AllSpells().Where(c => c.FaceUp && c.EquippedTo != null && c.EquippedTo != t).ToList())
            {
                var es = x.D.S(eq);
                if (es?.EquipOk == null || es.EquipOk(x.D.X(eq), t)) { eq.EquippedTo = t; await x.D.Raise(new Ev { Kind = EvKind.Equipped, Card = eq, Other = t, Player = eq.Controller }); }
                else await x.D.Destroy(eq, x.Self);
            }
        }, x => x.AllSpells().Any(c => c.EquippedTo != null), async x => await x.Target(x.D.FaceUpMonsters, "Gather the Equip cards on which monster?"));
        Spell("Dark Spirit of the Silent", async x =>
        {
            var old = x.Ev!.Card!;
            var n = await x.Pick(x.OppMonsters().FaceUp().Where(m => m != old && m.Attacks == 0), "Which monster attacks instead?");
            if (n == null) { x.D.AttackNegated = true; return; }
            if (n.Defense) await x.D.ChangePosition(n, false);
            x.D.Attacker = n;
        }, x => AttackOnMe(x) && x.OppMonsters().FaceUp().Any(m => m != x.Ev!.Card && m.Attacks == 0));
        Continuous("Royal Command").Forbid = (x, q, c) => q == Q.Negate && c != null && c.Data.IsMonster && db.Effects.TryGetValue(c.Data.KonamiId, out var ef) && ef.Trig.StartsWith("flip");
        Spell("Riryoku Field", async x => await x.D.Negate(x.Ev!.Link!),
            x => OppActivated(x, l => l.Card.Data.IsSpell && l.X.T.Count == 1 && l.X.T0!.Loc == Loc.Monster));
        {
            var s = Spell("Skull Lair", async x => { });
            s.Run = null;
            s.IgnCan = x => x.My.Grave.Count > 0 && CanBanishGrave(x);
            s.IgnRun = async x =>
            {
                var l = await x.PickN(x.My.Grave, 1, x.My.Grave.Count, "Skull Lair: banish cards from your Graveyard");
                foreach (var c in l) await BanishFromGrave(x, c);
                var t = await x.Pick(x.Targetable(x.D.FaceUpMonsters.Where(m => x.D.Level(m) == l.Count)), $"Destroy a Level {l.Count} monster");
                if (t != null) await x.D.Destroy(t, x.Self);
            };
        }
        {
            var s = Continuous("Graverobber's Retribution");
            s.On = async (x, e) => { if (e.Kind == EvKind.Standby && e.Player == x.Me && x.Self.OnField) await x.D.Damage(x.Opp, 100 * x.Their.Banished.Count(c => c.Data.IsMonster), x.Self); };
        }
        Spell("Deal of Phantom", async x => { if (x.T0 is { } t && t.Valid() && t.FaceUp) x.D.Boost(t, 100 * x.N); },
            x => x.D.FaceUpMonsters.Any(),
            async x => { x.N = x.My.Grave.Count(c => c.Data.IsMonster); await x.Target(x.D.FaceUpMonsters, "Boost which monster?"); });
        {
            var s = Spell("Destruction Punch", async x => { x.D.Flags[$"punch{x.Self.Uid}"] = x.Ev!.Card!.Uid + 1; },
                x => AttackOnMe(x) && x.Ev!.Other is { } d && d.Defense && x.Def(d) > x.Atk(x.Ev.Card!));   // face-down Defense counts: you know its DEF
            s.On = async (x, e) =>
            {
                if (e.Kind == EvKind.AfterDamage && x.D.Flags.GetValueOrDefault($"punch{x.Self.Uid}") == e.Card!.Uid + 1)
                {
                    x.D.Flags[$"punch{x.Self.Uid}"] = 0;
                    if (e.Card.Valid()) await x.D.Destroy(e.Card, x.Self);
                }
            };
        }
        Continuous("Blind Destruction").On = async (x, e) =>
        {
            if (e.Kind != EvKind.Standby || e.Player != x.Me || !x.Self.OnField) return;
            int r = x.D.Die(x.Me, x.Self);
            await x.D.Destroy(x.D.FaceUpMonsters.Where(m => r == 6 ? x.D.Level(m) >= 6 : x.D.Level(m) == r).ToList(), x.Self);
        };
        Continuous("The Emperor's Holiday").Forbid = (x, q, c) => q == Q.Negate && c != null && (c.Data.Is("Equip") || c.EquippedTo != null);
        S("The Dark Door").Marker = true; // Duel.CanAttack
        Spell("Spiritualism", async x => { if (x.T0 is { OnField: true } t) await x.D.ToHand(t, x.Self); },
            x => x.Targetable(x.Spells(x.Opp)).Any(), async x => await x.Target(x.Spells(x.Opp), "Return which Spell/Trap?"));
        {
            var s = S("Cyclon Laser");
            s.EquipOk = (x, m) => m.Name == "Gradius";
            s.Allow = (x, q, c) => q == Q.Piercing && c == x.Self.EquippedTo;
        }
        {
            var s = Spell("Bait Doll", async x =>
            {
                x.D.Flags[$"baitdoll{x.Self.Uid}"] = 1;
                if (x.T0 is not { OnField: true } t || t.FaceUp) return;
                if (!t.Data.IsTrap) { x.D.Reveal(x.Me, t); return; }
                var ts = x.D.S(t);
                var tx = x.D.X(t, x.Ev);
                t.FaceUp = true;
                if (ts?.Run != null && (ts.Can?.Invoke(tx) ?? true)) { if (ts.Cost != null) await ts.Cost(tx); await ts.Run(tx); }
                if (t.OnField && !t.Data.Is("Continuous")) await x.D.Destroy(t, x.Self);
            }, x => x.Targetable(x.Spells(x.Opp).Where(c => !c.FaceUp)).Any(), async x => await x.Target(x.Spells(x.Opp).Where(c => !c.FaceUp), "Force which card?"));
            s.On = async (x, e) =>
            {
                if (e.Is(EvKind.ToGrave, x.Self) && x.D.Flags.GetValueOrDefault($"baitdoll{x.Self.Uid}") == 1)
                { x.D.Flags[$"baitdoll{x.Self.Uid}"] = 0; await x.D.ToDeck(x.Self, shuffle: true); }
            };
        }
        Spell("De-Fusion", async x =>
        {
            if (x.T0 is not { } f || !f.Valid()) return;
            // The materials used for its Fusion Summon (none recorded: it wasn't Fusion Summoned, e.g. revived).
            var used = Enumerable.Range(0, 3).Select(i => f.Var($"mat{i}")).Where(u => u > 0).Select(u => x.D.CardByUid(u - 1)).ToList();
            await x.D.MoveTo(f, Loc.Extra);
            var mats = used.Where(c => c.Loc == Loc.Grave && c.Owner == x.Me).ToList();
            if (used.Count > 0 && mats.Count == used.Count && x.D.FreeMonsterZones(x.Me) >= mats.Count && await x.Ask("Special Summon the Fusion Material monsters?"))
                foreach (var m in mats) await x.D.SpecialSummon(m, x.Me, await x.D.ChoosePosition(x.Me, m), by: x.Self);
        }, x => x.Targetable(x.AllMonsters().Where(m => m.FaceUp && m.Data.IsFusion)).Any(),
        async x => await x.Target(x.AllMonsters().Where(m => m.FaceUp && m.Data.IsFusion), "Return which Fusion Monster?"));
        {
            var s = S("Fusion Gate");
            s.IgnCan = x => x.D.FusionOptions(x.Me).Any();
            s.IgnRun = async x => await x.D.FusionSummon(x.Me, x.Self, banishMats: true);
        }
        {
            var s = S("Ekibyo Drakmord");
            s.EquipOk = (x, m) => true;
            s.Forbid = (x, q, c) => q == Q.Attack && c == x.Self.EquippedTo;
            s.On = async (x, e) =>
            {
                if (e.Kind == EvKind.EndPhase && x.Self.EquippedTo is { } m && e.Player == m.Controller && x.Self.Inc("turns") >= 2)
                {
                    var me = x.Self;
                    await x.D.Destroy(m, me);
                    if (me.Loc == Loc.Grave) await x.D.ToHand(me);
                }
            };
        }
        Spell("Miracle Dig", async x =>
        {
            foreach (var c in await x.PickN(x.My.Banished.Where(c => c.Data.IsMonster), 3, 3, "Return 3 banished monsters to the Graveyard")) await x.D.MoveTo(c, Loc.Grave, by: x.Self);
        }, x => x.My.Banished.Count(c => c.Data.IsMonster) >= 5);
        S("Dragonic Attack").Base = (x, c, st) => { if (x.Self.EquippedTo == c) st.Race = "Dragon"; };
        Spell("Spirit Elimination", async x => x.D.TurnFlags.Add($"spiritelim{x.Me}"));
        Continuous("Vengeful Bog Spirit").Forbid = (x, q, c) => q == Q.Attack && c != null && c.SummonTurn == x.D.Turn;
        Spell("Scapegoat", async x =>
        {
            for (int i = 0; i < 4 && x.D.FreeMonsterZones(x.Me) > 0; i++)
            {
                var tok = x.D.MakeToken("Sheep Token", x.Me, 0, 0, "EARTH", "Beast");
                await x.D.SpecialSummon(tok, x.Me, true, by: x.Self);
                tok.Vars["notribute"] = 1;
            }
            int me = x.Me;
            x.D.Linger(x.D.Turn, (q, c, p) => q is Q.NormalSummon or Q.SetMonster or Q.SpecialSummon && (c?.Controller ?? p) == me);
        }, x => x.D.FreeMonsterZones(x.Me) >= 4);
    }

    static IEnumerable<Card> SentByOppThisTurn(Ctx x) => x.My.Grave.Where(c => c.Data.IsSpell && x.D.Flags.GetValueOrDefault($"sentbyopp{c.Uid}") == x.D.Turn);
}
