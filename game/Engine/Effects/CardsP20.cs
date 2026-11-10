namespace PoC.Engine;

// Official cards, batch 20 (Possessed Dark Soul .. Red-Eyes Wyvern).
public static partial class Scripts
{
    static bool CrystalBeast(Card c) => c.In("Crystal Beast");
    static int CrystalNames(Ctx x) => x.My.FieldCards.Where(c => c.FaceUp).Concat(x.My.Grave).Where(CrystalBeast).Select(c => c.Name).Distinct().Count();

    static void CardsP20(CardDb db)
    {
        {
            var s = S("Possessed Dark Soul");
            s.Can = x => Up(x) && !x.D.Forbidden(Q.Tribute, x.Self) && x.OppMonsters().Any(m => m.FaceUp && x.D.Level(m) <= 3);
            s.Cost = async x => await x.D.Tribute(x.Self, x.Self);
            s.Run = async x => { foreach (var m in x.OppMonsters().Where(m => m.FaceUp && x.D.Level(m) <= 3).ToList()) await x.D.TakeControl(m, x.Me); };
        }
        Spell("Pot of Avarice", async x =>
        {
            var l = x.T.Where(c => c.Loc == Loc.Grave).ToList();
            if (l.Count < 5) return;
            foreach (var c in l) await x.D.ToDeck(c);
            x.D.Shuffle(x.Me);
            await x.D.Draw(x.Me, 2);
        }, x => x.Targetable(x.My.Grave.Where(c => c.Data.IsMonster)).Count() >= 5,
        async x => x.T.AddRange(await x.PickN(x.Targetable(x.My.Grave.Where(c => c.Data.IsMonster)), 5, 5, "Target 5 monsters in your GY")));
        Spell("Power Bond", async x =>
        {
            if (!await x.D.FusionSummon(x.Me, x.Self, resultOk: f => f.Data.Race == "Machine") || x.D.LastFusion is not { Loc: Loc.Monster } f) return;
            int gain = Math.Max(0, (f.AsMonster ?? f.Data).Atk);
            f.Vars["modatk"] = f.Var("modatk") + gain; x.D.Seq++;
            var d = x.D; int me = x.Me; var pb = x.Self;
            d.Later(d.Turn, Phase.End, async () => await d.Damage(me, gain, pb));
        }, x => x.D.FusionOptions(x.Me, resultOk: f => f.Data.Race == "Machine", by: x.Self).Any());
        Spell("Power Wall", async x =>
        {
            var e = x.Ev!;
            int dmg = x.Self.Var("pwdmg");
            int n = (dmg + 499) / 500;
            for (int i = 0; i < n && x.My.Deck.Count > 0; i++) await x.D.ToGrave(x.My.Deck[0], x.Self);
            x.D.BattleFlags.Add($"nodamage{x.Me}");
        }, x =>
        {
            if (x.Ev is not { Kind: EvKind.DamageCalc, Card: { } a } e || a.Controller != x.Opp) return false;
            var t = e.Other;
            int atk = x.D.CalcAtk(a, t, x.Atk(a));
            int dmg = t == null ? atk : t.Attack ? atk - x.D.CalcAtk(t, a, x.Atk(t)) : (x.D.Allowed(Q.Piercing, a) || a.Var("pierce") == x.D.Turn) ? atk - x.Def(t) : 0;
            if (dmg <= 0) return false;
            x.Self.Vars["pwdmg"] = dmg;
            return true;
        });
        {
            var s = S("Prickle Fairy");
            s.Forbid = (x, q, c) => q == Q.BeAttacked && Up(x) && c != null && c.Controller == x.Me && c.FaceUp && x.IsRace(c, "Insect") && x.D.Querying?.Controller == x.Opp;
            s.On = async (x, e) =>
            {
                if (e.Kind != EvKind.AfterDamage || !x.Self.OnField) return;
                var o = e.Card == x.Self ? e.Other : e.Other == x.Self ? e.Card : null;
                if (o is { Loc: Loc.Monster, FaceUp: true, Defense: false }) await x.D.ChangePosition(o, true);
            };
        }
        {
            var s = Spell("Prima Light", async x =>
            {
                int mode = x.Self.Var("primamode");
                if (mode == 1)
                {
                    if (x.T0 is not { Loc: Loc.Monster } t) return;
                    await x.D.Tribute(t, x.Self);
                    await SummonPick(x, x.My.Hand.Concat(x.My.Deck).Where(c => c.Data.IsMonster && c.Data.Attribute == "LIGHT" && c.Data.Race == "Warrior"), "Special Summon a LIGHT Warrior");
                    x.D.Shuffle(x.Me);
                }
                else x.My.ExtraSummons++;
            }, x => !x.D.TurnFlags.Contains($"prima{x.Me}") && (x.D.Tributable(x.Me).Any(m => m.FaceUp && x.IsAttr(m, "EARTH") && x.IsRace(m, "Warrior")) || x.D.Phase is Phase.Main1 or Phase.Main2),
            async x =>
            {
                x.D.TurnFlags.Add($"prima{x.Me}");
                bool a = x.D.Tributable(x.Me).Any(m => m.FaceUp && x.IsAttr(m, "EARTH") && x.IsRace(m, "Warrior")), b = x.D.Phase is Phase.Main1 or Phase.Main2 && x.My.Hand.Any(c => c.Data.IsMonster && c.Data.Race == "Warrior");
                int i = a && b ? await x.D.ChooseText(x.Me, "Prima Light", new[] { "Tribute an EARTH Warrior: Special Summon a LIGHT Warrior", "Normal Summon a Warrior" }, x.Self) : a ? 0 : 1;
                x.Self.Vars["primamode"] = i + 1;
                if (i == 0) x.T.Add((await x.PickN(x.D.Tributable(x.Me).Where(m => m.FaceUp && x.IsAttr(m, "EARTH") && x.IsRace(m, "Warrior")), 1, 1, "Tribute 1 EARTH Warrior"))[0]);
            });
            s.GraveFx = new Script
            {
                Can = x => !x.D.TurnFlags.Contains($"prima{x.Me}") && x.D.TurnPlayer == x.Me && x.D.Phase is Phase.Main1 or Phase.Main2 && x.My.Deck.Any(c => c.Data.IsMonster && c.In("Cyber") && c.Data.Race is "Warrior" or "Fairy"),
                Cost = async x => { x.D.TurnFlags.Add($"prima{x.Me}"); await x.D.Banish(x.Self, x.Self); },
                Run = async x => await x.Search(c => c.Data.IsMonster && c.In("Cyber") && c.Data.Race is "Warrior" or "Fairy", "Add which Cyber monster?"),
            };
            s.Note = "The Normal Summon option grants one more Normal Summon this turn (use it on a Warrior right away).";
        }
        {
            var s = S("Proto-Cyber Dragon");
            s.TreatedAs = "Cyber Dragon";
        }
        {
            var s = Spell("Psychic Wave", async x => await x.D.Damage(x.Opp, 600, x.Self),
                x => x.MyMonsters().Any(m => m.FaceUp && x.IsRace(m, "Machine")) && x.My.Hand.Concat(x.My.Deck).Any(c => c.Name == "Jinzo" && c != x.Self),
                async x => { var j = await x.Pick(x.My.Hand.Concat(x.My.Deck).Where(c => c.Name == "Jinzo"), "Send which Jinzo to the GY?"); bool deck = j?.Loc == Loc.Deck; if (j != null) await x.D.ToGrave(j, x.Self); if (deck) x.D.Shuffle(x.Me); });
            s.GraveFx = new Script
            {
                Can = x => Hopt(x, "gy") && x.Self.Var("gyturn") != x.D.Turn && x.D.TurnPlayer == x.Me && x.D.Phase is Phase.Main1 or Phase.Main2
                    && x.My.Deck.Any(c => c.Data.IsMonster && c.In("Jinzo")) && x.Targetable(x.My.Grave.Where(c => c.Data.IsMonster && c.Data.Race == "Machine")).Any(),
                Cost = async x => { UseHopt(x, "gy"); await x.D.Banish(x.Self, x.Self); await x.Target(x.My.Grave.Where(c => c.Data.IsMonster && c.Data.Race == "Machine"), "Add which Machine to your hand?"); },
                Run = async x =>
                {
                    var j = await x.Pick(x.My.Deck.Where(c => c.Data.IsMonster && c.In("Jinzo")), "Send which Jinzo monster to the GY?");
                    x.D.Shuffle(x.Me);
                    if (j == null) return;
                    await x.D.ToGrave(j, x.Self);
                    if (j.Loc == Loc.Grave && x.T0 is { Loc: Loc.Grave } t) await x.D.ToHand(t, x.Self);
                },
            };
        }
        {
            static bool Castle(Card c) => c.Name == "Golden Castle of Stromberg";
            var s = S("Pumpkin Carriage");
            s.Allow = (x, q, c) => q == Q.DirectAttack && Up(x) && c != null && c.Controller == x.Me && c.In("Prinzessin");
            s.Forbid = (x, q, c) => Up(x) && c != null && Castle(c) && c.Controller == x.Me && ((q == Q.Target && x.D.Targeter?.Controller == x.Opp) || q == Q.DestroyByEffect);
        }
        S("Puppet Master").On = async (x, e) =>
        {
            if (!e.Is(EvKind.Summoned, x.Self) || e.Arg != (int)SummonKind.Tribute || x.My.Lp <= 2000 || x.D.FreeMonsterZones(x.Me) < 2) return;
            var l = x.Targetable(x.My.Grave.Where(c => c.Data.IsMonster && c.Data.Race == "Fiend" && x.D.CanSpecialSummon(c, x.Me, by: x.Self))).ToList();
            if (l.Count < 2 || !await x.Ask("Pay 2000 LP to Special Summon 2 Fiends from your GY?")) return;
            x.D.Pay(x.Me, 2000, x.Self);
            foreach (var c in await x.PickN(l, 2, 2, "Special Summon which 2 Fiends?"))
                if (await x.D.SpecialSummon(c, x.Me, await x.D.ChoosePosition(x.Me, c), by: x.Self)) c.Vars["noattack"] = x.D.Turn;
        };
        OnBattleDeath("Pyramid Turtle", x => x.Recruit(c => c.Data.Race == "Zombie" && c.Data.Def is >= 0 and <= 2000, "Pyramid Turtle: Special Summon a Zombie with 2000 or less DEF"));
        {
            var s = Spell("Pyro Clock of Destiny", async x =>
            {
                foreach (var c in x.D.FieldCards.Where(c => c.Vars.ContainsKey("turns")).ToList()) c.Vars["turns"] = c.Var("turns") + 1;
                foreach (var c in x.D.FieldCards.Where(c => c.Counters("turn") > 0).ToList()) AddCounter(x, c, 1, "turn");
                x.D.Seq++;
            });
            s.Note = "It advances the turn counts that cards on the field keep (e.g. Swords of Revealing Light).";
        }
        {
            var s = Spell("Question", async x =>
            {
                var m = x.My.Grave.FirstOrDefault(c => c.Data.IsMonster);
                if (m == null) return;
                var names = x.D.Cards.Where(c => c.Data.IsMonster && !c.IsToken).Select(c => c.Data.Name).Distinct().OrderBy(n => n).ToList();
                int i = await x.D.ChooseText(x.Opp, "Question: name the bottom monster of your opponent's GY", names, x.Self);
                x.D.Reveal(x.Opp, m);
                if (names[i] == m.Name) await x.D.Banish(m, x.Self);
                else if (x.D.CanSpecialSummon(m, x.Me, by: x.Self)) await x.D.SpecialSummon(m, x.Me, await x.D.ChoosePosition(x.Me, m), by: x.Self);
            }, x => x.My.Grave.Any(c => c.Data.IsMonster));
            s.Note = "Your opponent picks from the monster names in this duel.";
        }
        {
            var s = Spell("Quick Summon", async x =>
            {
                x.My.ExtraSummons++;
                if (x.OppMonsters().Any()) x.D.TurnFlags.Add($"quicksummon{x.Me}");
            });
            s.FreeTribute = (x, c, set) => !set && x.Self.Loc == Loc.Grave && x.D.TurnFlags.Contains($"quicksummon{c.Controller}") && c.Owner == x.Me && x.D.TributesFor(c) > 0;
            s.OnFreeTribute = async x => x.D.TurnFlags.Remove($"quicksummon{x.Me}");
            s.GraveFx = new Script
            {
                Can = x => Hopt(x, "gy") && x.Self.Var("gyturn") != x.D.Turn && x.D.TurnPlayer == x.Me && x.D.Phase is Phase.Main1 or Phase.Main2
                    && x.My.Deck.Concat(x.My.Grave).Any(c => c.Data.IsMonster && c.Data.Level >= 5 && !c.Data.IsFusion && !c.Data.IsRitual && x.D.S(c)?.SummonOk == null),
                Cost = async x => { UseHopt(x, "gy"); await x.D.Banish(x.Self, x.Self); },
                Run = async x =>
                {
                    var c = await x.Pick(x.My.Deck.Concat(x.My.Grave).Where(c => c.Data.IsMonster && c.Data.Level >= 5 && !c.Data.IsFusion && !c.Data.IsRitual && x.D.S(c)?.SummonOk == null), "Add which monster?");
                    if (c == null) return;
                    bool deck = c.Loc == Loc.Deck;
                    await x.D.ToHand(c, x.Self); x.D.Reveal(x.Opp, c);
                    if (deck) x.D.Shuffle(x.Me);
                    x.My.ExtraSummons++;
                },
            };
            s.Note = "Both summons are given as one more Normal Summon this turn; use it right away.";
        }
        Spell("R - Righteous Justice", async x =>
        {
            int n = x.My.FieldCards.Count(c => c.FaceUp && c.In("Elemental HERO"));
            var l = x.AllSpells().Where(c => c != x.Self).ToList();
            if (n == 0 || l.Count == 0) return;
            await x.D.Destroy(await x.PickN(l, Math.Min(n, l.Count), Math.Min(n, l.Count), $"Destroy {Math.Min(n, l.Count)} Spell/Trap Card(s)"), x.Self);
        }, x => x.My.FieldCards.Any(c => c.FaceUp && c.In("Elemental HERO")) && x.AllSpells().Any(c => c != x.Self));
        {
            var s = S("Ra's Disciple");
            s.On = async (x, e) =>
            {
                if (!e.Is(EvKind.Summoned, x.Self)) return;
                var l = x.My.Hand.Concat(x.My.Deck).Where(c => c.Name == "Ra's Disciple" && x.D.CanSpecialSummon(c, x.Me, by: x.Self)).ToList();
                int max = Math.Min(2, Math.Min(l.Count, x.D.FreeMonsterZones(x.Me)));
                if (max == 0 || !await x.Ask("Special Summon Ra's Disciples from your hand and/or Deck?")) return;
                var pick = await x.PickN(l, 1, max, $"Special Summon up to {max}");
                x.D.TurnFlags.Add("rasdisciple");
                foreach (var c in pick) await x.D.SpecialSummon(c, x.Me, await x.D.ChoosePosition(x.Me, c), by: x.Self);
                x.D.TurnFlags.Remove("rasdisciple");
                x.D.Shuffle(x.Me);
            };
            s.Forbid = (x, q, c) => Up(x) && ((q == Q.Tribute && c == x.Self) || (q == Q.SpecialSummon && x.D.QueryPlayer == x.Me && !x.D.TurnFlags.Contains("rasdisciple")));
            s.Note = "Gods are not in the game yet, so it can never be Tributed.";
        }
        Spell("Rain Storm", async x =>
        {
            if (x.T0 is not { Loc: Loc.Monster, FaceUp: true } t) return;
            int atk = x.Atk(t);
            var opts = new List<string>();
            if (atk >= 1000) opts.Add("Lose 1000 ATK: destroy 1 Spell/Trap");
            if (atk >= 2000) opts.Add("Lose 2000 ATK: destroy 2 cards");
            if (opts.Count == 0) return;
            int i = await x.D.ChooseText(x.Me, "Rain Storm", opts, x.Self);
            t.Vars["modatk"] = t.Var("modatk") - (i == 0 ? 1000 : 2000); x.D.Seq++;
            if (i == 0) { var c = await x.Pick(x.Spells(x.Opp), "Destroy which Spell/Trap?"); if (c != null) await x.D.Destroy(c, x.Self); }
            else { var l = x.Their.FieldCards.ToList(); if (l.Count > 0) await x.D.Destroy(await x.PickN(l, Math.Min(2, l.Count), Math.Min(2, l.Count), "Destroy 2 cards"), x.Self); }
        }, x => x.MyMonsters().Any(m => m.FaceUp && m.In("Cloudian") && x.Atk(m) >= 1000), async x => await x.Target(x.MyMonsters().FaceUp().Where(m => m.In("Cloudian") && x.Atk(m) >= 1000), "Select which Cloudian?"));
        {
            var s = Spell("Rainbow Bridge Bifrost", async x => { }, x => false);
            s.Note = "It needs a LIGHT Synchro Monster: there are no Synchro Monsters in the game, so it can never be activated.";
        }
        {
            static IEnumerable<Card> Darks(Ctx x) => x.My.Grave.Where(c => c.Data.IsMonster && c.Data.Attribute == "DARK");
            var s = S("Rainbow Dark Dragon");
            s.AlsoIn = new[] { "Ultimate Crystal" };
            s.SummonOk = (x, by, k) => by == x.Self;
            s.ProcCan = x => Darks(x).Select(c => c.Name).Distinct().Count() >= 7;
            s.Proc = async x =>
            {
                var used = new List<Card>();
                for (int i = 0; i < 7; i++)
                {
                    var c = await x.Pick(Darks(x).Where(c => !used.Contains(c) && !used.Any(u => u.Name == c.Name)), $"Banish DARK monster {i + 1} of 7 (different names)");
                    if (c == null) return;
                    used.Add(c);
                }
                foreach (var c in used) await x.D.Banish(c, x.Self);
                await x.D.SpecialSummon(x.Self, x.Me, await x.D.ChoosePosition(x.Me, x.Self), by: x.Self);
            };
            s.Can = x => Up(x) && x.MyMonsters().Any(m => m != x.Self && x.D.Attr(m) == "DARK") || (Up(x) && Darks(x).Any());
            s.Run = async x =>
            {
                var l = x.MyMonsters().Where(m => m != x.Self && x.D.Attr(m) == "DARK").Concat(Darks(x)).ToList();
                int n = 0;
                foreach (var c in l) { await x.D.Banish(c, x.Self); if (c.Loc == Loc.Banished) n++; }
                if (Up(x) && n > 0) { x.Self.Vars["modatk"] = x.Self.Var("modatk") + 500 * n; x.D.Seq++; }
            };
        }
        {
            var s = S("Rainbow Dragon");
            s.AlsoIn = new[] { "Ultimate Crystal" };
            s.SummonOk = (x, by, k) => by == x.Self;
            s.ProcCan = x => CrystalNames(x) >= 7;
            s.Proc = async x => await x.D.SpecialSummon(x.Self, x.Me, await x.D.ChoosePosition(x.Me, x.Self), by: x.Self);
            s.AddFx(f =>
            {
                f.Label = "Send Crystal Beasts: +1000 ATK each"; f.Speed = 2; f.AnyTurn = true;
                f.Can = x => Up(x) && x.Self.SummonTurn != x.D.Turn && x.MyMonsters().Any(m => m != x.Self && m.FaceUp && CrystalBeast(m));
                f.Run = async x =>
                {
                    int n = 0;
                    foreach (var m in x.MyMonsters().Where(m => m != x.Self && m.FaceUp && CrystalBeast(m)).ToList()) { await x.D.ToGrave(m, x.Self); if (m.Loc == Loc.Grave) n++; }
                    if (Up(x) && n > 0) { x.Self.Vars["modatk"] = x.Self.Var("modatk") + 1000 * n; x.D.Seq++; }
                };
            });
            s.AddFx(f =>
            {
                f.Label = "Banish Crystal Beasts: shuffle the field";
                f.Can = x => Up(x) && x.Self.SummonTurn != x.D.Turn && x.My.Grave.Any(CrystalBeast);
                f.Cost = async x => { foreach (var c in x.My.Grave.Where(CrystalBeast).ToList()) await x.D.Banish(c, x.Self); };
                f.Run = async x => { foreach (var c in x.D.FieldCards.ToList()) await x.D.ToDeck(c); x.D.Shuffle(x.Me); x.D.Shuffle(x.Opp); };
            });
        }
        Spell("Rainbow Gravity", async x =>
        {
            await SummonPick(x, x.My.Deck.Concat(x.My.Grave).Where(c => c.Data.IsMonster && c.In("Ultimate Crystal")), "Special Summon which Ultimate Crystal monster?", ignore: true);
            x.D.Shuffle(x.Me);
        }, x => CrystalNames(x) >= 7 && x.My.Deck.Concat(x.My.Grave).Any(c => c.Data.IsMonster && c.In("Ultimate Crystal") && x.D.CanSpecialSummon(c, x.Me, by: x.Self, ignore: true)));
        Spell("Rainbow Life", async x => x.D.TurnFlags.Add($"rainbowlife{x.Me}"), x => x.My.Hand.Any(c => c != x.Self),
            async x => await x.D.Discard(x.Me, await x.PickN(x.My.Hand.Where(c => c != x.Self), 1, 1, "Discard 1 card"), x.Self));
        {
            var s = S("Rainbow Neos");
            s.Materials = new() { Named("Elemental HERO Neos"), Cond("1 \"Ultimate Crystal\" monster", c => c.Data.IsMonster && c.In("Ultimate Crystal")) };
            s.MustFusion = true;
            s.OncePerTurn = true;
            s.Can = x => Up(x) && (x.MyMonsters().Any() && x.OppMonsters().Any() || x.My.SpellsOnField.Any() && x.Spells(x.Opp).Any() || x.My.Deck.Count > 0 && x.Their.Grave.Any());
            s.Cost = async x =>
            {
                var opts = new List<(string, int)>();
                if (x.MyMonsters().Any() && x.OppMonsters().Any()) opts.Add(("Send a monster: shuffle their monsters", 0));
                if (x.My.SpellsOnField.Any() && x.Spells(x.Opp).Any()) opts.Add(("Send a Spell/Trap: shuffle their Spells/Traps", 1));
                if (x.My.Deck.Count > 0 && x.Their.Grave.Any()) opts.Add(("Mill 1: shuffle their GY", 2));
                int i = opts[await x.D.ChooseText(x.Me, "Rainbow Neos", opts.Select(o => o.Item1).ToList(), x.Self)].Item2;
                x.Self.Vars["rnmode"] = i;
                if (i == 0) { var c = await x.Pick(x.MyMonsters(), "Send which monster to the GY?"); if (c != null) await x.D.ToGrave(c, x.Self); }
                if (i == 1) { var c = await x.Pick(x.My.SpellsOnField, "Send which Spell/Trap to the GY?"); if (c != null) await x.D.ToGrave(c, x.Self); }
                if (i == 2) await x.D.ToGrave(x.My.Deck[0], x.Self);
            };
            s.Run = async x =>
            {
                int i = x.Self.Var("rnmode");
                var l = i == 0 ? x.OppMonsters().ToList() : i == 1 ? x.Spells(x.Opp).ToList() : x.Their.Grave.ToList();
                foreach (var c in l) await x.D.ToDeck(c);
                x.D.Shuffle(x.Opp);
            };
        }
        Spell("Rainbow Path", async x =>
        {
            x.D.AttackNegated = true;
            var l = x.My.Deck.Where(c => c.Name is "Rainbow Dragon" or "Rainbow Dark Dragon").ToList();
            if (l.Count > 0 && await x.Ask("Add Rainbow Dragon or Rainbow Dark Dragon from your Deck?")) await x.Search(c => c.Name is "Rainbow Dragon" or "Rainbow Dark Dragon", "Add which?");
        }, x => AttackOnMe(x) && x.My.SpellsOnField.Any(c => c.FaceUp && CrystalBeast(c)),
        async x => { var c = await x.Pick(x.My.SpellsOnField.Where(c => c.FaceUp && CrystalBeast(c)), "Send which Crystal Beast to the GY?"); if (c != null) await x.D.ToGrave(c, x.Self); });
        {
            var s = Spell("Rainbow Veil", async x => { if (x.T0 is { } t && t.Valid() && t.FaceUp) await x.D.Equip(x.Self, t); },
                x => x.Targetable(x.D.FaceUpMonsters).Any(), async x => await x.Target(x.D.FaceUpMonsters, "Equip to which monster?"));
            s.Forbid = (x, q, c) => q == Q.Negate && x.D.Phase == Phase.Battle && c != null && c.Loc == Loc.Monster && x.Self.EquippedTo is { } m && c.Controller != m.Controller
                && c.Var("veilbattle") == x.D.Turn;
            s.On = async (x, e) =>
            {
                if (e.Kind == EvKind.AttackDeclared && x.Self.EquippedTo is { } m && (e.Card == m || e.Other == m))
                { var o = e.Card == m ? e.Other : e.Card; if (o != null) { o.Vars["veilbattle"] = x.D.Turn; x.D.Seq++; } }
            };
        }
        {
            var s = S("Rallis the Star Bird");
            s.CalcAtk = (x, c, o, atk) => c == x.Self && o != null ? atk + 200 * x.D.Level(o) : atk;
            s.On = async (x, e) =>
            {
                if (e.Kind != EvKind.AfterDamage || e.Card != x.Self || x.Self.Loc != Loc.Monster) return;
                await x.D.Banish(x.Self, x.Self);
                if (x.Self.Loc != Loc.Banished) return;
                var d = x.D; int me = x.Me; var self = x.Self; bool done = false;
                d.Watch(int.MaxValue, async ev =>
                {
                    if (done || ev.Kind != EvKind.BattleStart || ev.Player != me) return;
                    done = true;
                    if (self.Loc == Loc.Banished && d.CanSpecialSummon(self, me, by: self, ignore: true)) await d.SpecialSummon(self, me, false, by: self, ignore: true);
                });
            };
            s.Note = "Its ATK gain applies during damage calculation.";
        }
        S("Rare Metal Dragon").NormalSummonOk = x => false;
        {
            var s = Spell("Rare Metalmorph", async x =>
            {
                if (x.T0 is not { Loc: Loc.Monster } t || !x.Self.OnField) return;
                Tie(x, t); x.Self.Vars["stay"] = 1;
            }, x => x.Targetable(x.D.FaceUpMonsters.Where(m => x.IsRace(m, "Machine"))).Any(), async x => await x.Target(x.D.FaceUpMonsters.Where(m => x.IsRace(m, "Machine")), "Target which Machine?"));
            s.Stats = (x, c, st) => { if (x.Self.Var("stay") == 1 && c == TiedMonster(x)) st.Add(500, 0); };
            s.On = async (x, e) =>
            {
                if (!x.Self.OnField || !x.Self.FaceUp || TiedMonster(x) is not { } t) return;
                if (e.Kind == EvKind.LeftField && e.Card == t) { await x.D.Destroy(x.Self, x.Self); return; }
                if (e.Kind == EvKind.Activated && e.Link is { } l && l.Card.Data.IsSpell && l.X.T.Contains(t) && x.Self.Var("metalused") == 0)
                { x.Self.Vars["metalused"] = 1; await x.D.Negate(l, destroy: false); }
            };
        }
        Spell("Rare Value", async x =>
        {
            var l = x.My.SpellsOnField.Where(c => c.FaceUp && CrystalBeast(c)).ToList();
            var c = await x.D.PickOne(x.Opp, l, "Choose 1 Crystal Beast card for your opponent to send to the GY", x.Self);
            if (c == null) return;
            await x.D.ToGrave(c, x.Self);
            if (c.Loc == Loc.Grave) await x.D.Draw(x.Me, 2);
        }, x => x.My.SpellsOnField.Count(c => c.FaceUp && CrystalBeast(c)) >= 2);
        {
            var s = Spell("Raregold Armor", async x => { if (x.T0 is { } t && t.Valid() && t.FaceUp) await x.D.Equip(x.Self, t); },
                x => x.Targetable(x.D.FaceUpMonsters).Any(), async x => await x.Target(x.D.FaceUpMonsters, "Equip to which monster?"));
            s.Forbid = (x, q, c) => q == Q.BeAttacked && x.Self.EquippedTo is { } m && m.Controller == x.Me && c != null && c != m && c.Controller == x.Me && x.D.Querying?.Controller == x.Opp;
        }
        {
            var s = S("Raviel, Lord of Phantasms");
            s.SummonOk = (x, by, k) => by == x.Self;
            s.ProcCan = x => x.D.Tributable(x.Me).Count(m => m.FaceUp ? x.IsRace(m, "Fiend") : m.Data.Race == "Fiend") >= 3;
            s.Proc = async x =>
            {
                foreach (var t in await x.PickN(x.D.Tributable(x.Me).Where(m => m.FaceUp ? x.IsRace(m, "Fiend") : m.Data.Race == "Fiend"), 3, 3, "Tribute 3 Fiends")) await x.D.Tribute(t, x.Self);
                await x.D.SpecialSummon(x.Self, x.Me, await x.D.ChoosePosition(x.Me, x.Self), by: x.Self);
            };
            s.On = async (x, e) =>
            {
                if (e.Kind == EvKind.Summoned && e.Player == x.Opp && e.Summoned(SummonKind.Normal, SummonKind.Tribute) && Up(x) && x.D.FreeMonsterZones(x.Me) > 0)
                {
                    var t = x.D.MakeToken("Phantasm Token", x.Me, 1000, 1000, "DARK", "Fiend", 1);
                    if (await x.D.SpecialSummon(t, x.Me, await x.D.ChoosePosition(x.Me, t), by: x.Self)) t.Vars["noattack"] = int.MaxValue;
                }
            };
            s.OncePerTurn = true;
            s.Can = x => Up(x) && x.D.Tributable(x.Me).Any(m => m != x.Self);
            s.Cost = async x =>
            {
                var t = await x.Pick(x.D.Tributable(x.Me).Where(m => m != x.Self), "Tribute 1 monster");
                if (t == null) return;
                x.Self.Vars["ravielgain"] = Math.Max(0, (t.AsMonster ?? t.Data).Atk);
                await x.D.Tribute(t, x.Self);
            };
            s.Run = async x => { if (Up(x)) x.D.Boost(x.Self, x.Self.Var("ravielgain")); };
        }
        {
            var s = Spell("Re-Fusion", async x =>
            {
                if (x.T0 is not { Loc: Loc.Grave } t || !x.Self.OnField) return;
                if (!await x.D.SpecialSummon(t, x.Me, await x.D.ChoosePosition(x.Me, t), by: x.Self)) return;
                await x.D.Equip(x.Self, t);
                x.D.Flags[$"refusion{x.Self.Uid}"] = t.Uid + 1;
            }, x => x.My.Lp > 800 && x.Targetable(x.My.Grave.Where(c => c.Data.IsFusion && x.D.CanSpecialSummon(c, x.Me, by: x.Self))).Any(),
            async x => { x.D.Pay(x.Me, 800, x.Self); await x.Target(x.My.Grave.Where(c => c.Data.IsFusion && x.D.CanSpecialSummon(c, x.Me, by: x.Self)), "Special Summon which Fusion Monster?"); });
            s.On = async (x, e) =>
            {
                if (e.Is(EvKind.Destroyed, x.Self) && x.D.Flags.GetValueOrDefault($"refusion{x.Self.Uid}") is > 0 and var u && x.D.CardByUid(u - 1) is { Loc: Loc.Monster } m)
                    await x.D.Banish(m, x.Self);
            };
        }
        {
            var s = Spell("Reaper Scythe - Dreadscythe", async x => { if (x.T0 is { } t && t.Valid() && t.FaceUp) await x.D.Equip(x.Self, t); },
                x => x.Targetable(x.D.FaceUpMonsters.Where(m => Scripts.NameOf(m) == "Guardian Dreadscythe")).Any(), async x => await x.Target(x.D.FaceUpMonsters.Where(m => Scripts.NameOf(m) == "Guardian Dreadscythe"), "Equip to which Guardian Dreadscythe?"));
            s.EquipOk = (x, m) => Scripts.NameOf(m) == "Guardian Dreadscythe";
            s.Stats = (x, c, st) => { if (c == x.Self.EquippedTo) st.Add(500 * x.D.Players.Sum(p => p.Grave.Count(g => g.Data.IsMonster)), 0); };
        }
        Spell("Rebellion", async x =>
        {
            if (x.T0 is not { Loc: Loc.Monster } t || !await x.D.TakeControl(t, x.Me)) return;
            var d = x.D; int me = x.Me, turn = d.Turn; bool done = false;
            d.Linger(turn, (q, c, p) => q == Q.Attack && c != null && c != t && c.Controller == me);
            d.Watch(turn, async ev => { if (!done && ev.Kind == EvKind.BattleEnd) { done = true; if (t.Loc == Loc.Monster && t.Controller == me) await d.TakeControl(t, t.Owner); } });
            d.Later(turn, Phase.End, async () => { if (t.Loc == Loc.Monster && t.Controller == me) await d.TakeControl(t, t.Owner); });
        }, x => Hopt(x) && x.D.Phase == Phase.Battle && x.D.FreeMonsterZones(x.Me) > 0 && x.Targetable(x.OppMonsters()).Any(),
        async x => { UseHopt(x); await x.Target(x.OppMonsters(), "Take control of which monster?"); });
        {
            var s = Spell("Rebirth Judgment", async x => x.Self.Vars["stay"] = 1);
            s.AddFx(f =>
            {
                f.Label = "Declare a Type for the GYs"; f.InSpellZone = true;
                f.Can = x => x.Self.FaceUp && Hopt(x);
                f.Cost = async x => UseHopt(x);
                f.Run = async x =>
                {
                    var races = x.D.Cards.Where(c => c.Data.IsMonster).Select(c => c.Data.Race).Where(r => !string.IsNullOrEmpty(r)).Distinct().OrderBy(r => r).ToList();
                    int i = await x.D.ChooseText(x.Me, "Rebirth Judgment: declare a Type", races, x.Self);
                    x.D.RebirthRace = races[i]; x.D.Flags["rebirthturn"] = x.D.Turn; x.D.Seq++;
                };
            });
            s.Note = "Cards that check a GY monster's Type through the game rules see the declared Type.";
        }
        S("Reborn Zombie").Forbid = (x, q, c) => q == Q.DestroyByBattle && c == x.Self && x.Self.Attack && x.My.Hand.Count == 0;
        {
            var s = Spell("Recycle", async x => x.Self.Vars["stay"] = 1);
            s.On = async (x, e) =>
            {
                if (e.Kind != EvKind.Standby || e.Player != x.Me || !x.Self.OnField || !x.Self.FaceUp || x.My.Lp <= 300 || !x.My.Grave.Any(c => !c.Data.IsMonster)) return;
                if (!await x.Ask("Pay 300 LP to return a Spell/Trap from your GY to the bottom of your Deck?")) return;
                x.D.Pay(x.Me, 300, x.Self);
                var c = await x.Pick(x.My.Grave.Where(c => !c.Data.IsMonster), "Return which card?");
                if (c != null) await x.D.ToDeck(c, top: false);
            };
        }
        S("Red Gadget").On = async (x, e) =>
        {
            if (e.Is(EvKind.Summoned, x.Self) && e.Arg != (int)SummonKind.Flip && x.My.Deck.Any(c => c.Name == "Yellow Gadget") && await x.Ask("Add Yellow Gadget from your Deck?"))
                await x.Search(c => c.Name == "Yellow Gadget", "Add Yellow Gadget");
        };
        {
            var s = S("Red-Eyes Black Dragon Sword");
            s.SummonOk = (x, by, k) => by?.Name == "The Claw of Hermos";
            s.On = async (x, e) =>
            {
                if (!e.Is(EvKind.Summoned, x.Self) || x.My.FreeSpellZones == 0) return;
                var t = await x.Pick(x.Targetable(x.D.FaceUpMonsters.Where(m => m != x.Self)), "Equip Red-Eyes Black Dragon Sword to which monster?");
                if (t == null) return;
                x.Self.ResetFieldState();
                x.D.Place(x.Self, x.Me, Loc.Spell); x.Self.FaceUp = true;
                await x.D.Equip(x.Self, t);
                x.Self.Vars["asspell"] = 1;
            };
            s.Stats = (x, c, st) =>
            {
                if (c != x.Self.EquippedTo || x.Self.Loc != Loc.Spell) return;
                int n = x.D.FaceUpMonsters.Count(m => x.IsRace(m, "Dragon")) + x.D.Players.Sum(p => p.Grave.Count(g => g.Data.IsMonster && g.Data.Race == "Dragon"));
                st.Add(1000 + 500 * n, 500 * n);
            };
        }
        Spell("Red-Eyes Burn", async x =>
        {
            if (x.T0 is not { } t) return;
            int atk = Math.Max(0, t.Data.Atk);
            await x.D.Damage(x.Me, atk, x.Self); await x.D.Damage(x.Opp, atk, x.Self);
        }, x => Hopt(x) && x.Ev is { Kind: EvKind.Destroyed, Card: { } c } && c.In("Red-Eyes") && c.Name != "Red-Eyes B. Chick" && c.Data.IsMonster
            && x.D.LeftFieldLog.LastOrDefault(l => l.c == c) is { c: not null } l && l.controller == x.Me && l.faceUp && l.to != Loc.Monster,
        async x => { UseHopt(x); x.T.Add(x.Ev!.Card!); });
        {
            var s = S("Red-Eyes Darkness Dragon");
            s.SummonOk = (x, by, k) => by == x.Self;
            s.ProcCan = x => x.D.Tributable(x.Me).Any(m => Scripts.NameOf(m) == "Red-Eyes B. Dragon");
            s.Proc = async x =>
            {
                var t = await x.Pick(x.D.Tributable(x.Me).Where(m => Scripts.NameOf(m) == "Red-Eyes B. Dragon"), "Tribute Red-Eyes B. Dragon");
                if (t == null) return;
                await x.D.Tribute(t, x.Self);
                await x.D.SpecialSummon(x.Self, x.Me, await x.D.ChoosePosition(x.Me, x.Self), by: x.Self);
            };
            s.Stats = (x, c, st) => { if (c == x.Self) st.Add(300 * x.My.Grave.Count(g => g.Data.IsMonster && g.Data.Race == "Dragon"), 0); };
        }
        {
            var s = S("Red-Eyes Darkness Metal Dragon");
            s.ProcCan = x => !x.D.TurnFlags.Contains($"redmd{x.Me}") && x.MyMonsters().Any(m => m.FaceUp && x.IsRace(m, "Dragon"));
            s.Proc = async x =>
            {
                var t = await x.Pick(x.MyMonsters().Where(m => m.FaceUp && x.IsRace(m, "Dragon")), "Banish 1 face-up Dragon you control");
                if (t == null) return;
                x.D.TurnFlags.Add($"redmd{x.Me}");
                await x.D.Banish(t, x.Self);
                await x.D.SpecialSummon(x.Self, x.Me, await x.D.ChoosePosition(x.Me, x.Self), by: x.Self);
            };
            s.Can = x => Up(x) && Hopt(x) && x.D.FreeMonsterZones(x.Me) > 0 && x.My.Hand.Concat(x.My.Grave).Any(c => c.Data.IsMonster && c.Data.Race == "Dragon" && c.Name != "Red-Eyes Darkness Metal Dragon" && x.D.CanSpecialSummon(c, x.Me, by: x.Self));
            s.Cost = async x => UseHopt(x);
            s.Run = async x => await SummonPick(x, x.My.Hand.Concat(x.My.Grave).Where(c => c.Data.IsMonster && c.Data.Race == "Dragon" && c.Name != "Red-Eyes Darkness Metal Dragon"), "Special Summon which Dragon?");
        }
        Spell("Red-Eyes Spirit", async x => { if (x.T0 is { Loc: Loc.Grave } t) await x.D.SpecialSummon(t, x.Me, await x.D.ChoosePosition(x.Me, t), by: x.Self); },
            x => x.D.FreeMonsterZones(x.Me) > 0 && x.Targetable(x.My.Grave.Where(c => c.Data.IsMonster && c.In("Red-Eyes") && x.D.CanSpecialSummon(c, x.Me, by: x.Self))).Any(),
            async x => await x.Target(x.My.Grave.Where(c => c.Data.IsMonster && c.In("Red-Eyes") && x.D.CanSpecialSummon(c, x.Me, by: x.Self)), "Special Summon which Red-Eyes monster?"));
        Spell("Red-Eyes Transmigration", async x => await RitualSummon(x, m => m.Name == "Lord of the Red", exact: false, graveMat: c => c.Data.IsMonster && c.In("Red-Eyes") && c.Name != "Red-Eyes B. Chick"),
            x => RitualTargets(x, m => m.Name == "Lord of the Red", exact: false, graveMat: c => c.Data.IsMonster && c.In("Red-Eyes") && c.Name != "Red-Eyes B. Chick").Any());
        S("Red-Eyes Wyvern").On = async (x, e) =>
        {
            if (e.Kind != EvKind.EndPhase || e.Player != x.Me || x.Self.Loc != Loc.Grave || x.My.NormalSummoned) return;
            var l = x.My.Grave.Where(c => c != x.Self && c.Data.IsMonster && c.In("Red-Eyes") && c.Name != "Red-Eyes B. Chick" && x.D.CanSpecialSummon(c, x.Me, by: x.Self)).ToList();
            if (l.Count == 0 || !await x.Ask("Banish Red-Eyes Wyvern to Special Summon a Red-Eyes monster from your GY?")) return;
            await x.D.Banish(x.Self, x.Self);
            await SummonPick(x, l, "Special Summon which Red-Eyes monster?");
        };
        Sure("Possessed Dark Soul", "Pot of Avarice", "Pyramid Turtle", "R - Righteous Justice", "Rainbow Life", "Rare Metal Dragon", "Reaper Scythe - Dreadscythe", "Reborn Zombie", "Red Gadget",
            "Red-Eyes Darkness Dragon", "Red-Eyes Spirit", "Proto-Cyber Dragon", "Raregold Armor", "Prickle Fairy", "Puppet Master");
    }
}
