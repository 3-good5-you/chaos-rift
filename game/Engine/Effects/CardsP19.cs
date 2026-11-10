namespace PoC.Engine;

// Official cards, batch 19 (Non Aggression Area .. Plasma Warrior Eitom).
public static partial class Scripts
{
    /// Ojama King / Ojama Knight: "Select up to N of your opponent's Monster Zones. The selected zones cannot be used."
    static void LockOppZones(Script s, int n)
    {
        s.On = async (x, e) =>
        {
            bool on = (e.Is(EvKind.Summoned, x.Self) || e.Is(EvKind.Flipped, x.Self)) && Up(x);
            bool off = (e.Kind == EvKind.LeftField && e.Card == x.Self) || (e.Kind == EvKind.PositionChanged && e.Card == x.Self && !x.Self.FaceUp);
            string key = $"ojama{x.Self.Uid}";
            if (off && x.D.Flags.GetValueOrDefault(key) is var mask && mask != 0)
            {
                int p = x.D.Flags.GetValueOrDefault(key + "p");
                x.D.Flags[$"blocked{p}"] = x.D.Flags.GetValueOrDefault($"blocked{p}") & ~mask;
                x.D.Flags.Remove(key); x.D.Seq++;
            }
            if (!on) return;
            int picked = 0;
            for (int i = 0; i < n; i++)
            {
                var opts = new List<Opt>();
                for (int z = 0; z < 5; z++)
                    if (x.D.P(x.Opp).Monsters[z] == null && (x.D.Flags.GetValueOrDefault($"blocked{x.Opp}") & (1 << z)) == 0)
                        opts.Add(new Opt(Act.Option, Arg: x.Opp * 8 + z, Text: $"Opponent's zone {z + 1}"));
                if (opts.Count == 0) break;
                opts.Add(new Opt(Act.Option, Arg: -1, Text: "Stop"));
                var o = await x.D.Choose(x.Me, Ask.Choose, opts, $"{x.Self.Name}: block which zone?", x.Self);
                if (o.Arg < 0) break;
                int bit = 1 << (o.Arg % 8);
                x.D.Flags[$"blocked{x.Opp}"] = x.D.Flags.GetValueOrDefault($"blocked{x.Opp}") | bit;
                picked |= bit;
            }
            x.D.Flags[key] = picked; x.D.Flags[key + "p"] = x.Opp; x.D.Seq++;
        };
    }

    static string[] OjamaTrio = { "Ojama Green", "Ojama Yellow", "Ojama Black" };

    static void CardsP19(CardDb db)
    {
        Spell("Non Aggression Area", async x =>
        {
            var d = x.D; int opp = x.Opp;
            d.Linger(d.Turn + 1, (q, c, p) => q is Q.NormalSummon or Q.SetMonster or Q.SpecialSummon && p == opp && d.TurnPlayer == opp);
        }, x => x.D.TurnPlayer == x.Me && x.D.Phase == Phase.Standby && x.My.Hand.Any(c => c != x.Self),
        async x => await x.D.Discard(x.Me, await x.PickN(x.My.Hand.Where(c => c != x.Self), 1, 1, "Discard 1 card"), x.Self));
        Spell("Non-Fusion Area", async x => x.Self.Vars["stay"] = 1);
        OnBattleDamage("Nubian Guard", async (x, e) =>
        {
            var l = x.My.Grave.Where(c => c.Data.IsSpell && c.Data.Is("Continuous")).ToList();
            if (l.Count == 0 || !await x.Ask("Return a Continuous Spell from your GY to the top of your Deck?")) return;
            var c = await x.Pick(l, "Return which?");
            if (c != null) await x.D.ToDeck(c, top: true);
        });
        Spell("Nutrient Z", async x => await x.D.Gain(x.Me, 4000, x.Self), x =>
        {
            if (x.Ev is not { Kind: EvKind.DamageCalc, Card: { } a } e || a.Controller != x.Opp) return false;
            var t = e.Other;
            int atk = x.D.CalcAtk(a, t, x.Atk(a));
            int dmg = t == null ? atk : t.Attack ? atk - x.D.CalcAtk(t, a, x.Atk(t)) : (x.D.Allowed(Q.Piercing, a) || a.Var("pierce") == x.D.Turn) ? atk - x.Def(t) : 0;
            return dmg >= 2000;
        });
        Spell("O - Oversoul", async x => { if (x.T0 is { Loc: Loc.Grave } t) await x.D.SpecialSummon(t, x.Me, await x.D.ChoosePosition(x.Me, t), by: x.Self); },
            x => x.D.FreeMonsterZones(x.Me) > 0 && x.Targetable(x.My.Grave.Where(c => c.Data.IsNormalMonster && c.In("Elemental HERO") && x.D.CanSpecialSummon(c, x.Me, by: x.Self))).Any(),
            async x => await x.Target(x.My.Grave.Where(c => c.Data.IsNormalMonster && c.In("Elemental HERO") && x.D.CanSpecialSummon(c, x.Me, by: x.Self)), "Special Summon which Elemental HERO?"));
        {
            var s = Spell("Ocean of Regeneration", async x => x.Self.Vars["stay"] = 1);
            static IEnumerable<Card> Ok(Ctx x) => x.My.Grave.Where(c => c.Data.IsMonster && c.Data.Attribute == "WATER" && c.Data.Atk is >= 0 and <= 1000 && x.D.CanSpecialSummon(c, x.Me, by: x.Self));
            s.AddFx(f =>
            {
                f.Label = "Special Summon a WATER monster"; f.InSpellZone = true;
                f.Can = x => x.Self.FaceUp && Hopt(x) && x.Targetable(Ok(x)).Any();
                f.Cost = async x => { UseHopt(x); await x.Target(Ok(x), "Special Summon which WATER monster?"); };
                f.Run = async x =>
                {
                    if (x.T0 is not { Loc: Loc.Grave } t || !await x.D.SpecialSummon(t, x.Me, await x.D.ChoosePosition(x.Me, t), by: x.Self)) return;
                    var d = x.D; d.Later(d.Turn, Phase.End, async () => { if (t.Loc == Loc.Monster) await d.Destroy(t); });
                };
            });
        }
        Spell("Ojama Delta Hurricane!!", async x => await x.D.Destroy(x.Their.FieldCards.ToList(), x.Self),
            x => OjamaTrio.All(n => x.MyMonsters().Any(m => m.FaceUp && Scripts.NameOf(m) == n)));
        LockOppZones(S("Ojama King"), 3);
        LockOppZones(S("Ojama Knight"), 2);
        Spell("Ojama Trio", async x =>
        {
            var d = x.D; var toks = new List<Card>();
            for (int i = 0; i < 3 && d.FreeMonsterZones(x.Opp) > 0; i++)
            {
                var t = d.MakeToken("Ojama Token", x.Opp, 0, 1000, "LIGHT", "Beast", 2);
                if (await d.SpecialSummon(t, x.Opp, true, by: x.Self)) { t.Vars["notribute"] = 1; toks.Add(t); }
            }
            d.Watch(int.MaxValue, async e =>
            {
                if (e.Kind == EvKind.Destroyed && e.Card != null && toks.Contains(e.Card) && d.LeftFieldLog.LastOrDefault(l => l.c == e.Card) is { c: not null } l)
                    await d.Damage(l.controller, 300, null);
            });
        }, x => x.D.FreeMonsterZones(x.Opp) >= 3);
        {
            var s = Spell("Ojamagic", async x => { });
            s.On = async (x, e) =>
            {
                if (e.Kind != EvKind.ToGrave || e.Card != x.Self || e.From is not (Loc.Hand or Loc.Spell)) return;
                foreach (var n in OjamaTrio)
                    if (x.My.Deck.FirstOrDefault(c => c.Name == n) is { } c) { await x.D.ToHand(c, x.Self); x.D.Reveal(x.Opp, c); }
                x.D.Shuffle(x.Me);
            };
        }
        Spell("Ojamandala", async x =>
        {
            if (x.D.FreeMonsterZones(x.Me) < 3) return;
            foreach (var n in OjamaTrio)
                if (x.My.Grave.FirstOrDefault(c => c.Name == n && x.D.CanSpecialSummon(c, x.Me, by: x.Self)) is { } c) await x.D.SpecialSummon(c, x.Me, await x.D.ChoosePosition(x.Me, c), by: x.Self);
        }, x => x.My.Lp > 1000 && x.D.FreeMonsterZones(x.Me) >= 3 && OjamaTrio.All(n => x.My.Grave.Any(c => c.Name == n)), async x => x.D.Pay(x.Me, 1000, x.Self));
        Spell("Ojamuscle", async x =>
        {
            if (x.T0 is not { Loc: Loc.Monster, FaceUp: true } k) return;
            var l = x.D.AllMonsters.Where(m => m != k && m.FaceUp && m.In("Ojama")).ToList();
            await x.D.Destroy(l, x.Self);
            int n = l.Count(m => !m.OnField);
            if (k.Loc == Loc.Monster && n > 0) { k.Vars["modatk"] = k.Var("modatk") + 1000 * n; x.D.Seq++; }
        }, x => x.Targetable(x.D.FaceUpMonsters.Where(m => Scripts.NameOf(m) == "Ojama King")).Any(),
        async x => await x.Target(x.D.FaceUpMonsters.Where(m => Scripts.NameOf(m) == "Ojama King"), "Select which Ojama King?"));
        Flip("Old Vindictive Magician", async x =>
        {
            var t = await x.Pick(x.Targetable(x.OppMonsters()), "Destroy which monster?");
            if (t != null) await x.D.Destroy(t, x.Self);
        });
        {
            var s = Spell("Opti-Camouflage Armor", async x => { if (x.T0 is { } t && t.Valid() && t.FaceUp) await x.D.Equip(x.Self, t); },
                x => x.Targetable(x.D.FaceUpMonsters.Where(m => x.D.Level(m) == 1)).Any(), async x => await x.Target(x.D.FaceUpMonsters.Where(m => x.D.Level(m) == 1), "Equip to which Level 1 monster?"));
            s.EquipOk = (x, m) => x.D.Level(m) == 1;
            s.Allow = (x, q, c) => q == Q.DirectAttack && c != null && c == x.Self.EquippedTo;
        }
        {
            var s = S("Orca Mega-Fortress of Darkness");
            s.AddFx(f =>
            {
                f.Label = "Tribute Torpedo Fish: destroy a monster";
                f.Can = x => Up(x) && x.D.Tributable(x.Me).Any(m => m.Name == "Torpedo Fish") && x.Targetable(x.D.AllMonsters.Where(m => m.Name != "Torpedo Fish" || x.D.Tributable(x.Me).Count(t => t.Name == "Torpedo Fish") > 1)).Any();
                f.Cost = async x => { var t = await x.Pick(x.D.Tributable(x.Me).Where(m => m.Name == "Torpedo Fish"), "Tribute Torpedo Fish"); if (t != null) await x.D.Tribute(t, x.Self); };
                f.Run = async x => { var t = await x.Pick(x.D.AllMonsters, "Destroy which monster?"); if (t != null) await x.D.Destroy(t, x.Self); };
            });
            s.AddFx(f =>
            {
                f.Label = "Tribute Cannonball Spear Shellfish: destroy a Spell/Trap";
                f.Can = x => Up(x) && x.D.Tributable(x.Me).Any(m => m.Name == "Cannonball Spear Shellfish") && x.AllSpells().Any();
                f.Cost = async x => { var t = await x.Pick(x.D.Tributable(x.Me).Where(m => m.Name == "Cannonball Spear Shellfish"), "Tribute Cannonball Spear Shellfish"); if (t != null) await x.D.Tribute(t, x.Self); };
                f.Run = async x => { var t = await x.Pick(x.AllSpells(), "Destroy which Spell/Trap?"); if (t != null) await x.D.Destroy(t, x.Self); };
            });
        }
        {
            var s = Spell("Ordeal of a Traveler", async x => x.Self.Vars["stay"] = 1);
            s.On = async (x, e) =>
            {
                if (!x.Self.OnField || !x.Self.FaceUp || e.Kind != EvKind.AttackDeclared || e.Player != x.Opp || x.My.Hand.Count == 0 || e.Card is not { Loc: Loc.Monster } a) return;
                var c = x.My.Hand[x.D.Rng.Int(x.My.Hand.Count)];
                int call = await x.D.ChooseText(x.Opp, "Ordeal of a Traveler: call the type of the random card", new[] { "Monster", "Spell", "Trap" }, x.Self);
                x.D.Reveal(x.Opp, c);
                int real = c.Data.IsMonster ? 0 : c.Data.IsSpell ? 1 : 2;
                if (call != real) await x.D.ToHand(a, x.Self);
            };
        }
        {
            var s = S("Orgoth the Relentless");
            s.OncePerTurn = true;
            s.Can = x => Up(x) && x.D.TurnPlayer == x.Me && x.D.Phase is Phase.Main1 or Phase.Main2;
            s.Run = async x =>
            {
                var r = new[] { x.D.Die(x.Me, x.Self), x.D.Die(x.Me, x.Self), x.D.Die(x.Me, x.Self) };
                if (!Up(x)) return;
                var self = x.Self; var d = x.D; int sum = 100 * r.Sum();
                self.Vars["modatk"] = self.Var("modatk") + sum; self.Vars["moddef"] = self.Var("moddef") + sum; d.Seq++;
                int until = d.Turn + 1;
                d.Later(until, Phase.End, async () => { self.Vars["modatk"] = self.Var("modatk") - sum; self.Vars["moddef"] = self.Var("moddef") - sum; d.Seq++; });
                var pairs = r.GroupBy(v => v).Where(g => g.Count() >= 2).Select(g => g.Key).ToList();
                bool all = r.Distinct().Count() == 1;
                bool Has(int a, int b) => all || pairs.Any(v => v == a || v == b);
                if (pairs.Count == 0) return;
                if (Has(1, 2)) { self.Vars["orgothsafe"] = until; d.Linger(until, (q, c, p) => q is Q.DestroyByBattle or Q.DestroyByEffect && c == self && self.Var("orgothsafe") >= d.Turn); }
                if (Has(3, 4)) await d.Draw(x.Me, 2);
                if (Has(5, 6)) self.Vars["direct"] = d.Turn;
            };
        }
        {
            var s = S("Orichalcos Shunoros");
            s.NormalSummonOk = x => false;
            s.Base = (x, c, st) => { if (c == x.Self) st.BaseAtk = 1000 * x.OppMonsters().Count(); };
            s.Forbid = (x, q, c) => q == Q.DestroyByEffect && Up(x) && c != null && c.Loc == Loc.Monster && c.FaceUp && c.Data.IsNormalMonster && x.D.Level(c) == 4;
            s.On = async (x, e) =>
            {
                if (x.Self.Loc != Loc.Hand || !e.Is(EvKind.Destroyed, e.Card) || !e.Battle || !e.Card!.Data.IsNormalMonster) return;
                if (x.D.LeftFieldLog.LastOrDefault(l => l.c == e.Card) is not { c: not null } l || l.controller != x.Me || !x.D.CanSpecialSummon(x.Self, x.Me, by: x.Self)) return;
                if (await x.Ask("Special Summon Orichalcos Shunoros from your hand?")) await x.D.SpecialSummon(x.Self, x.Me, await x.D.ChoosePosition(x.Me, x.Self), by: x.Self);
            };
        }
        {
            var s = Spell("Orichalcos Sword of Sealing", async x => { if (x.T0 is { } t && t.Valid() && t.FaceUp) await x.D.Equip(x.Self, t); },
                x => x.Targetable(x.D.FaceUpMonsters).Any(), async x => await x.Target(x.D.FaceUpMonsters, "Equip to which monster?"));
            s.Forbid = (x, q, c) => q == Q.Negate && c != null && c == x.Self.EquippedTo;
            static Card? Holder(Ctx x) => x.D.Flags.GetValueOrDefault($"sealgrant{x.Self.Uid}") is > 0 and var u && x.D.CardByUid(u - 1) is { Loc: Loc.Monster, FaceUp: true } m
                && m.Controller == x.Me && x.D.Flags.GetValueOrDefault($"sealuntil{x.Self.Uid}") >= x.D.Turn ? m : null;
            s.AddFx(f =>
            {
                f.Label = "Grant the destroy effect"; f.InSpellZone = true;
                f.Can = x => x.Self.FaceUp && Hopt(x) && x.My.FieldZone != null && x.Targetable(x.MyMonsters().FaceUp().Where(m => m.Data.IsMonster && !m.Data.IsNormalMonster)).Any();
                f.Cost = async x => { UseHopt(x); await x.Target(x.MyMonsters().FaceUp().Where(m => !m.Data.IsNormalMonster), "Which Effect Monster gains the effect?"); };
                f.Run = async x =>
                {
                    if (x.T0 is not { Loc: Loc.Monster } t) return;
                    x.D.Flags[$"sealgrant{x.Self.Uid}"] = t.Uid + 1;
                    x.D.Flags[$"sealuntil{x.Self.Uid}"] = x.D.TurnPlayer == x.Me ? x.D.Turn + 1 : x.D.Turn;
                };
            });
            s.AddFx(f =>
            {
                f.Label = "Granted: discard, destroy a card"; f.InSpellZone = true; f.Speed = 2; f.AnyTurn = true;
                f.Can = x => x.Self.FaceUp && Holder(x) is { } m && x.D.Flags.GetValueOrDefault($"sealused{m.Uid}") != x.D.Turn && x.My.Hand.Count > 0 && x.Targetable(x.D.FieldCards.Where(c => c.FaceUp)).Any();
                f.Cost = async x =>
                {
                    x.D.Flags[$"sealused{Holder(x)!.Uid}"] = x.D.Turn;
                    await x.D.ToGrave((await x.PickN(x.My.Hand, 1, 1, "Send 1 card from your hand to the GY"))[0], x.Self);
                    await x.Target(x.D.FieldCards.Where(c => c.FaceUp), "Destroy which card?");
                };
                f.Run = async x => { if (x.T0 is { OnField: true } t) await x.D.Destroy(t, Holder(x) ?? x.Self); };
            });
            s.Note = "The granted Quick Effect is used from the Sword's own menu while that monster is face-up on your field.";
        }
        {
            var s = S("Otohime");
            ReturnsAtEnd(s, async (x, e) =>
            {
                if ((e.Is(EvKind.Summoned, x.Self) && e.Summoned(SummonKind.Normal, SummonKind.Tribute, SummonKind.Flip)) && x.Targetable(x.OppMonsters().FaceUp()).Any() && await x.Ask("Change the battle position of 1 face-up monster your opponent controls?"))
                { var t = await x.Pick(x.Targetable(x.OppMonsters().FaceUp()), "Which monster?"); if (t != null) await x.D.ChangePosition(t, !t.Defense); }
            });
        }
        S("Outstanding Dog Marron").On = async (x, e) => { if (e.Kind == EvKind.ToGrave && e.Card == x.Self && x.Self.Loc == Loc.Grave && x.Self.Owner == x.Me) { await x.D.ToDeck(x.Self); x.D.Shuffle(x.Me); } };
        {
            static IEnumerable<Card> Pull(Ctx x, int lv) => x.My.Deck.Where(c => c.Data.IsMonster && c.In("Destiny HERO") && c.Data.Level <= lv / 2 && x.D.CanSpecialSummon(c, x.Me, by: x.Self));
            Spell("Over Destiny", async x =>
            {
                if (x.T0 is not { Loc: Loc.Grave } t) return;
                var c = await x.Pick(Pull(x, t.Data.Level), "Special Summon which Destiny HERO?");
                x.D.Shuffle(x.Me);
                if (c == null || !await x.D.SpecialSummon(c, x.Me, await x.D.ChoosePosition(x.Me, c), by: x.Self)) return;
                var d = x.D; d.Later(d.Turn, Phase.End, async () => { if (c.Loc == Loc.Monster) await d.Destroy(c); });
            }, x => x.D.FreeMonsterZones(x.Me) > 0 && x.Targetable(x.My.Grave.Where(c => c.Data.IsMonster && c.In("Destiny HERO") && Pull(x, c.Data.Level).Any())).Any(),
            async x => await x.Target(x.My.Grave.Where(c => c.Data.IsMonster && c.In("Destiny HERO") && Pull(x, c.Data.Level).Any()), "Target which Destiny HERO?"));
        }
        {
            var s = Spell("Over Limit", async x =>
            {
                var l = x.D.Players.SelectMany(p => p.Grave).Where(c => c.Data.IsNormalMonster && c.Data.Atk is >= 0 and <= 1000 && x.D.Flags.GetValueOrDefault($"battledead{c.Owner}_{c.Uid}") == x.D.Turn).ToList();
                foreach (var c in l)
                    if (x.D.CanSpecialSummon(c, x.Me, by: x.Self)) await x.D.SpecialSummon(c, x.Me, await x.D.ChoosePosition(x.Me, c), by: x.Self);
            }, x => x.My.Lp > 500 && x.D.FreeMonsterZones(x.Me) > 0 && x.D.Players.SelectMany(p => p.Grave).Any(c => c.Data.IsNormalMonster && c.Data.Atk is >= 0 and <= 1000 && x.D.Flags.GetValueOrDefault($"battledead{c.Owner}_{c.Uid}") == x.D.Turn),
            async x => x.D.Pay(x.Me, 500, x.Self));
            s.Note = "Judgement call: all the monsters are Special Summoned to your field.";
        }
        Spell("Overload Fusion", async x => await x.D.FusionSummon(x.Me, x.Self, banishMats: true, pool: x.MyMonsters().Concat(x.My.Grave.Where(c => c.Data.IsMonster)), resultOk: f => f.Data.Attribute == "DARK" && f.Data.Race == "Machine"),
            x => x.D.FusionOptions(x.Me, pool: x.MyMonsters().Concat(x.My.Grave.Where(c => c.Data.IsMonster)), resultOk: f => f.Data.Attribute == "DARK" && f.Data.Race == "Machine", by: x.Self).Any());
        Spell("Owner's Seal", async x => { foreach (var m in x.D.AllMonsters.Where(m => m.Controller != m.Owner).ToList()) await x.D.TakeControl(m, m.Owner); }, x => x.D.AllMonsters.Any(m => m.Controller != m.Owner));
        S("Oxygeddon").On = async (x, e) =>
        {
            if (e.Is(EvKind.Destroyed, x.Self) && e.Battle && x.Self.Loc == Loc.Grave && e.Other is { } o && x.D.Race(o) == "Pyro")
            { await x.D.Damage(x.Me, 800, x.Self); await x.D.Damage(x.Opp, 800, x.Self); }
        };
        {
            var s = DirectAttacker("Pair Cycroid");
            s.MatRecipes = (d, res) => d.Cards.Where(c => c.Owner == res.Owner && c.Data.IsMonster && c.Data.Race == "Machine").Select(c => c.Name).Distinct()
                .Select(n => new List<Mat> { Cond($"1 \"{n}\"", c => c.Data.IsMonster && c.Data.Race == "Machine" && Scripts.NameOf(c) == n), Cond($"1 \"{n}\"", c => c.Data.IsMonster && c.Data.Race == "Machine" && Scripts.NameOf(c) == n) }).ToList();
        }
        {
            var s = S("Paladin of White Dragon");
            StartDamageKill(s, faceDown: true);
            s.Can = x => Up(x) && !x.D.Forbidden(Q.Tribute, x.Self) && x.My.Hand.Concat(x.My.Deck).Any(c => c.Name == "Blue-Eyes White Dragon" && x.D.CanSpecialSummon(c, x.Me, by: x.Self, ignoreZones: true));
            s.Cost = async x => await x.D.Tribute(x.Self, x.Self);
            s.Run = async x =>
            {
                await SummonPick(x, x.My.Hand.Concat(x.My.Deck).Where(c => c.Name == "Blue-Eyes White Dragon"), "Special Summon Blue-Eyes White Dragon");
                x.D.Shuffle(x.Me);
                var d = x.D; int me = x.Me;
                d.Linger(d.Turn, (q, c, p) => q == Q.Attack && c != null && c.Controller == me && Scripts.NameOf(c) == "Blue-Eyes White Dragon");
            };
        }
        {
            var s = S("Pandemonium");
            s.On = async (x, e) =>
            {
                if (!x.Self.FaceUp || x.Self.Loc != Loc.Field || e.Kind != EvKind.Destroyed || e.Battle || !e.Card!.In("Archfiend") || e.Card.Loc != Loc.Grave || !e.Card.Data.IsMonster) return;
                if (x.D.LeftFieldLog.LastOrDefault(l => l.c == e.Card) is not { c: not null } l) return;
                int p = l.controller, lv = l.level > 0 ? l.level : e.Card.Data.Level;
                var pick = x.D.P(p).Deck.Where(c => c.Data.IsMonster && c.In("Archfiend") && c.Data.Level < lv).ToList();
                if (pick.Count == 0 || !await x.D.YesNo(p, "Pandemonium: add a lower-Level Archfiend from your Deck?", x.Self)) return;
                var c = await x.D.PickOne(p, pick, "Add which Archfiend?", x.Self);
                if (c != null) { await x.D.ToHand(c, x.Self); x.D.Reveal(1 - p, c); }
                x.D.Shuffle(p);
            };
        }
        S("Pandemonium Watchbear").Forbid = (x, q, c) => q == Q.DestroyByEffect && Up(x) && c != null && c.Name == "Pandemonium" && c.Controller == x.Me && x.D.Destroyer is { } by && by.Controller == x.Opp;
        {
            var s = S("Patroid");
            s.OncePerTurn = true;
            s.Can = x => Up(x) && x.Their.FieldCards.Any(c => !c.FaceUp);
            s.Run = async x => { var c = await x.Pick(x.Their.FieldCards.Where(c => !c.FaceUp), "Look at which face-down card?"); if (c != null) x.D.Reveal(x.Me, c); };
        }
        Spell("Pegasus Wing", async x =>
        {
            foreach (var m in x.T.Where(m => m.Loc == Loc.Monster)) { m.Vars["direct"] = x.D.Turn; m.Vars["halfdirect"] = x.D.Turn; }
        }, x => x.My.Grave.Any(c => c.Data.IsMonster && c.Data.Desc.Contains("Union")) && x.Targetable(x.MyMonsters().FaceUp().Where(m => m.In("Valkyrie"))).Any(),
        async x => x.T.AddRange(await x.PickN(x.Targetable(x.MyMonsters().FaceUp().Where(m => m.In("Valkyrie"))), 1, x.Targetable(x.MyMonsters().FaceUp().Where(m => m.In("Valkyrie"))).Count(), "Target which Valkyries?")));
        Spell("Penalty Game!", async x =>
        {
            int i = await x.D.ChooseText(x.Me, "Penalty Game!", new[] { "They cannot draw in their next Draw Phase", "They cannot activate Spells/Traps this turn" }, x.Self);
            if (i == 0) x.D.Flags[$"skipdraw{x.Opp}"] = 1;
            else { var d = x.D; int opp = x.Opp; d.Linger(d.Turn, (q, c, p) => q == Q.Activate && c != null && !c.Data.IsMonster && (p == opp || c.Controller == opp)); }
        }, x => x.Their.Hand.Count == 4);
        {
            static bool Penguin(Card m) => m.In("Penguin");
            var s = Spell("Penguin Sword", async x => { if (x.T0 is { } t && t.Valid() && t.FaceUp) await x.D.Equip(x.Self, t); },
                x => x.Targetable(x.D.FaceUpMonsters.Where(Penguin)).Any(), async x => await x.Target(x.D.FaceUpMonsters.Where(Penguin), "Equip to which Penguin?"));
            s.EquipOk = (x, m) => Penguin(m);
            s.Stats = (x, c, st) => { if (c == x.Self.EquippedTo) st.Add(800, 0); };
            s.On = async (x, e) =>
            {
                if (!x.Self.OnField || !x.Self.FaceUp) return;
                if (e.Kind == EvKind.BattleDamage && e.Card == x.Self.EquippedTo && e.Player == x.Opp && x.Their.FieldCards.Any() && await x.Ask("Return 1 card your opponent controls to the hand?"))
                { var t = await x.Pick(x.Targetable(x.Their.FieldCards), "Return which card?"); if (t != null) await x.D.ToHand(t, x.Self); }
                if (e.Kind is EvKind.ToHand or EvKind.Banished && e.Card is { } c && e.From is Loc.Monster or Loc.Spell or Loc.Field && x.D.LeftFieldLog.LastOrDefault(l => l.c == c) is { c: not null } l
                    && l.controller == x.Opp && l.faceUp && e.Other is { } by && by.In("Penguin") && Hopt(x))
                {
                    UseHopt(x);
                    var d = x.D; string n = c.Data.Name; int until = d.Turn + 1;
                    d.Linger(until, (q, k, p) => q == Q.Negate && k != null && k.Data.Name == n);
                }
            };
        }
        {
            var s = DirectAttacker("Penguin Torpedo");
            s.On = async (x, e) =>
            {
                if (e.Kind == EvKind.BattleDamage && e.Card == x.Self && e.Player == x.Opp && Hopt(x) && x.Targetable(x.OppMonsters().Where(m => x.D.Level(m) <= 6)).Any() && await x.Ask("Take control of a Level 6 or lower monster until the End Phase?"))
                {
                    UseHopt(x);
                    var t = await x.Pick(x.Targetable(x.OppMonsters().Where(m => x.D.Level(m) <= 6)), "Take control of which monster?");
                    if (t != null && await x.D.TakeControl(t, x.Me))
                    {
                        var d = x.D; int me = x.Me, turn = d.Turn;
                        d.Linger(turn, (q, c, p) => c == t && t.Controller == me && q is Q.Negate or Q.Attack);
                        d.Later(turn, Phase.End, async () => { if (t.Loc == Loc.Monster && t.Controller == me) await d.TakeControl(t, t.Owner); });
                    }
                }
                if (e.Kind == EvKind.AfterDamage && e.Card == x.Self && x.Self.Loc == Loc.Monster) await x.D.Destroy(x.Self, x.Self);
            };
        }
        SelfBoost("Perfect Machine King", x => 500 * x.D.FaceUpMonsters.Count(m => m != x.Self && x.IsRace(m, "Machine")));
        Spell("Phantasmal Martyrs", async x =>
        {
            foreach (var c in x.My.Hand.ToList()) await x.D.ToGrave(c, x.Self);
            for (int i = 0; i < 3 && x.D.FreeMonsterZones(x.Me) > 0; i++)
            {
                var t = x.D.MakeToken("Phantasmal Martyr Token", x.Me, 0, 0, "DARK", "Fiend", 1);
                await x.D.SpecialSummon(t, x.Me, false, by: x.Self);
            }
        }, x => x.My.Hand.Count(c => c != x.Self) >= 2 && x.D.FreeMonsterZones(x.Me) >= 3 && x.MyMonsters().Any(m => m.FaceUp && Scripts.NameOf(m) is "Uria, Lord of Searing Flames" or "Hamon, Lord of Striking Thunder"));
        {
            var s = S("Phantom of Chaos");
            s.BattleDamage = (x, src, to, dmg) => Up(x) && to == x.Opp && (x.D.Attacker == x.Self || x.D.AttackTarget == x.Self) ? 0 : dmg;
            s.OncePerTurn = true;
            s.Can = x => Up(x) && x.Targetable(x.My.Grave.Where(c => c.Data.IsMonster && !c.Data.IsNormalMonster)).Any();
            s.Cost = async x => await x.Target(x.My.Grave.Where(c => c.Data.IsMonster && !c.Data.IsNormalMonster), "Banish which Effect Monster?");
            s.Run = async x =>
            {
                if (x.T0 is not { Loc: Loc.Grave } t || !Up(x)) return;
                await x.D.Banish(t, x.Self);
                if (t.Loc != Loc.Banished || !Up(x)) return;
                var self = x.Self; var d = x.D;
                self.TempName = t.Data.Name;
                SetBaseAtk(x, self, Math.Max(0, t.Data.Atk));
                self.Vars["copyidx"] = t.Data.Idx + 1; self.Vars["copyturn"] = d.Turn; d.Seq++;
                d.Later(d.Turn, Phase.End, async () => { self.TempName = null; self.Vars["copyidx"] = 0; self.Vars["baseatkturn"] = 0; d.Seq++; });
            };
        }
        Spell("Photon Generator Unit", async x => { await SummonPick(x, x.My.Hand.Concat(x.My.Deck).Concat(x.My.Grave).Where(c => c.Name == "Cyber Laser Dragon"), "Special Summon Cyber Laser Dragon"); x.D.Shuffle(x.Me); },
            x => x.D.Tributable(x.Me).Count(m => m.FaceUp && Scripts.NameOf(m) == "Cyber Dragon") >= 2 && x.My.Hand.Concat(x.My.Deck).Concat(x.My.Grave).Any(c => c.Name == "Cyber Laser Dragon"),
            async x => { foreach (var t in await x.PickN(x.D.Tributable(x.Me).Where(m => m.FaceUp && Scripts.NameOf(m) == "Cyber Dragon"), 2, 2, "Tribute 2 Cyber Dragons")) await x.D.Tribute(t, x.Self); });
        Spell("Pikeru's Circle of Enchantment", async x => { var d = x.D; int me = x.Me; d.Linger(d.Turn, (q, c, p) => q == Q.TakeEffectDamage && p == me); });
        S("Pinch Hopper").On = async (x, e) =>
        {
            if (e.Kind == EvKind.ToGrave && e.Card == x.Self && e.From == Loc.Monster && x.Self.Owner == x.Me && x.My.Hand.Any(c => c.Data.IsMonster && c.Data.Race == "Insect"))
                await SummonPick(x, x.My.Hand.Where(c => c.Data.IsMonster && c.Data.Race == "Insect"), "Special Summon an Insect from your hand?", optional: true);
        };
        S("Pitch-Black Warwolf").Forbid = (x, q, c) => q == Q.Activate && Up(x) && x.D.Phase == Phase.Battle && c != null && c.Data.IsTrap && c.Controller == x.Opp;
        {
            var s = Union("Pitch-Dark Dragon", (x, m) => Scripts.NameOf(m) == "Dark Blade");
            s.Stats = (x, c, st) => { if (x.Self.Var("union") == 1 && c == x.Self.EquippedTo) st.Add(400, 400); };
            s.Allow = (x, q, c) => q == Q.Piercing && x.Self.Var("union") == 1 && c != null && c == x.Self.EquippedTo;
        }
        {
            var s = S("Plasma Warrior Eitom");
            s.AlsoIn = new[] { "Magna Warrior" };
            s.SummonOk = (x, by, k) => by == x.Self;
            s.ProcFromGrave = true;
            s.ProcCan = x => !x.D.TurnFlags.Contains($"eitom{x.Me}") && x.D.Tributable(x.Me).Any(m => x.D.Level(m) >= 7);
            s.Proc = async x =>
            {
                var t = await x.Pick(x.D.Tributable(x.Me).Where(m => x.D.Level(m) >= 7), "Tribute 1 Level 7 or higher monster");
                if (t == null) return;
                x.D.TurnFlags.Add($"eitom{x.Me}");
                await x.D.Tribute(t, x.Self);
                await x.D.SpecialSummon(x.Self, x.Me, await x.D.ChoosePosition(x.Me, x.Self), by: x.Self);
            };
            s.OncePerTurn = true;
            s.Can = x => Up(x) && x.My.Deck.Any(c => c.Data.IsMonster && (c.In("Magnet Warrior") || c.In("Magna Warrior")) && c.Name != "Plasma Warrior Eitom");
            s.Run = async x =>
            {
                var c = await x.Pick(x.My.Deck.Where(c => c.Data.IsMonster && (c.In("Magnet Warrior") || c.In("Magna Warrior")) && c.Name != "Plasma Warrior Eitom"), "Send which monster to the GY?");
                x.D.Shuffle(x.Me);
                if (c != null) await x.D.ToGrave(c, x.Self);
                if (Up(x)) { SetBaseAtk(x, x.Self, 1500); x.Self.Vars["direct"] = x.D.Turn; }
            };
        }
        Sure("Non-Fusion Area", "O - Oversoul", "Ojama Delta Hurricane!!", "Ojamandala", "Old Vindictive Magician", "Opti-Camouflage Armor", "Owner's Seal", "Perfect Machine King", "Phantasmal Martyrs",
            "Photon Generator Unit", "Pikeru's Circle of Enchantment", "Pinch Hopper", "Pitch-Black Warwolf", "Outstanding Dog Marron", "Oxygeddon", "Nubian Guard");
    }
}
