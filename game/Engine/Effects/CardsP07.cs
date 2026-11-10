namespace PoC.Engine;

// Official cards, batch 7 (Damage = Reptile .. Demise, King of Armageddon).
public static partial class Scripts
{
    /// "Look at / declare a card name": the names of every card in this duel (both Decks).
    static async Flow<CardData> DeclareName(Ctx x, string text, Func<CardData, bool>? ok = null)
    {
        var names = x.D.Cards.Where(c => !c.IsToken && (ok?.Invoke(c.Data) ?? true)).Select(c => c.Data).GroupBy(d => d.Name).Select(g => g.First()).OrderBy(d => d.Name).ToList();
        return names[await x.D.ChooseText(x.Me, text, names.Select(d => d.Name).ToList(), x.Self)];
    }

    static bool DarkScorpion(Card c) => c.In("Dark Scorpion") || c.Name == "Don Zaloog";

    /// Dark Scorpions: "When this card inflicts Battle Damage to your opponent, you can activate 1 of these effects".
    static void ScorpionHit(string name, string[] labels, Func<Ctx, int, Flow> run)
    {
        OnBattleDamage(name, async (x, e) =>
        {
            int i = await x.D.ChooseText(x.Me, $"{name}: activate an effect?", labels.Append("No").ToList(), x.Self);
            if (i < labels.Length) await run(x, i);
        });
    }

    static void CardsP07(CardDb db)
    {
        S("Damage = Reptile").On = async (x, e) =>
        {
            if (e.Kind != EvKind.BattleDamage || e.Player != x.Me || !x.Self.OnField || !x.Self.FaceUp || x.Self.Var("reptileturn") == x.D.Turn) return;
            if (!new[] { x.D.Attacker, x.D.AttackTarget }.Any(m => m != null && (m.FaceUp ? x.IsRace(m, "Reptile") : m.Data.Race == "Reptile"))) return;
            var l = x.My.Deck.Where(c => c.Data.IsMonster && c.Data.Race == "Reptile" && c.Data.Atk <= e.Amount).ToList();
            if (l.Count == 0 || !await x.Ask($"Special Summon a Reptile with {e.Amount} or less ATK from your Deck?")) return;
            x.Self.Vars["reptileturn"] = x.D.Turn;
            await SummonPick(x, l, "Special Summon which Reptile?");
            x.D.Shuffle(x.Me);
        };
        Spell("Damage Condenser", async x =>
        {
            int dmg = x.N;
            await SummonPick(x, x.My.Deck.Where(c => c.Data.IsMonster && c.Data.Atk <= dmg), $"Special Summon a monster with {dmg} or less ATK (Attack Position)", defense: false);
            x.D.Shuffle(x.Me);
        }, x => x.Ev is { Kind: EvKind.Damage, Battle: true } e && e.Player == x.Me && x.My.Hand.Any(c => c != x.Self),
        async x => { x.N = x.Ev!.Amount; await x.D.Discard(x.Me, await x.PickN(x.My.Hand.Where(c => c != x.Self), 1, 1, "Discard 1 card"), x.Self); });
        Spell("Damage Polarizer", async x =>
        {
            await x.D.Negate(x.Ev!.Link!);
            await x.D.Draw(x.Me, 1); await x.D.Draw(x.Opp, 1);
        }, x => x.Ev is { Kind: EvKind.Activated, Link: { } l } && l.Card != x.Self && System.Text.RegularExpressions.Regex.IsMatch(l.Card.Data.Desc, @"[Ii]nflict[^.]*damage"));
        S("Damage Polarizer").Note = "Counts as a damage effect: any card whose text says it inflicts damage.";
        S("Dandylion").On = async (x, e) =>
        {
            if (!e.Is(EvKind.ToGrave, x.Self)) return;
            var d = x.D;
            for (int i = 0; i < 2 && d.FreeMonsterZones(x.Me) > 0; i++)
            {
                var t = d.MakeToken("Fluff Token", x.Me, 0, 0, "WIND", "Plant", 1);
                if (await d.SpecialSummon(t, x.Me, true, by: x.Self)) { t.Vars["notribute"] = 1; d.Later(d.Turn, Phase.End, async () => t.Vars["notribute"] = 0); }
            }
        };
        S("Dangerous Machine Type-6").On = async (x, e) =>
        {
            if (e.Kind != EvKind.Standby || e.Player != x.Me || !x.Self.OnField || !x.Self.FaceUp) return;
            switch (x.D.Die(x.Me, x.Self))
            {
                case 1: if (x.My.Hand.Count > 0) await x.D.Discard(x.Me, await x.PickN(x.My.Hand, 1, 1, "Discard 1 card"), x.Self); break;
                case 2: if (x.Their.Hand.Count > 0) await x.D.Discard(x.Opp, await x.D.Pick(x.Opp, x.Their.Hand, 1, 1, "Discard 1 card", x.Self), x.Self); break;
                case 3: await x.D.Draw(x.Me, 1); break;
                case 4: await x.D.Draw(x.Opp, 1); break;
                case 5: { var t = await x.Pick(x.OppMonsters(), "Destroy 1 monster your opponent controls"); if (t != null) await x.D.Destroy(t, x.Self); break; }
                default: await x.D.Destroy(x.Self, x.Self); break;
            }
        };
        Spell("Dark Calling", async x => await x.D.FusionSummon(x.Me, x.Self, pool: x.My.Hand.Concat(x.My.Grave).Where(c => c.Data.IsMonster), resultOk: NeedsDarkFusion, matsTo: Loc.Banished),
            x => x.D.FusionOptions(x.Me, pool: x.My.Hand.Concat(x.My.Grave).Where(c => c.Data.IsMonster), resultOk: NeedsDarkFusion, by: x.Self).Any());
        {
            var s = S("Dark Catapulter");
            s.On = async (x, e) => { if (e.Kind == EvKind.Standby && e.Player == x.Me && Up(x) && x.Self.Defense) AddCounter(x, x.Self); };
            s.Can = x => Up(x) && x.Self.Counters() > 0 && x.My.Grave.Count >= x.Self.Counters() && x.AllSpells().Any();
            s.Cost = async x =>
            {
                x.N = x.Self.Counters();
                foreach (var c in await x.PickN(x.My.Grave, x.N, x.N, $"Banish {x.N} card(s) from your GY")) await x.D.Banish(c, x.Self);
            };
            s.Run = async x =>
            {
                var l = x.AllSpells().ToList();
                await x.D.Destroy(await x.PickN(l, Math.Min(x.N, l.Count), Math.Min(x.N, l.Count), $"Destroy {x.N} Spell/Trap card(s)"), x.Self);
                if (x.Self.Valid()) { x.Self.Vars["ctr:"] = 0; x.D.Seq++; }
            };
        }
        S("Dark City").CalcAtk = (x, c, vs, atk) => c == x.D.Attacker && c.In("Destiny HERO") && vs != null && atk < x.Atk(vs) ? atk + 1000 : atk;
        Spell("Dark Corridor", async x =>
        {
            var c = await x.Pick(x.My.Deck.Where(c => c.Data.IsMonster && c.In("Dark World")), "Add which Dark World monster?");
            if (c == null) return;
            await x.D.ToHand(c, x.Self); x.D.Shuffle(x.Me);
            await x.D.Discard(x.Me, await x.PickN(x.My.Hand, 1, 1, "Discard 1 card"), x.Self);
        }, x => Hopt(x) && x.My.Deck.Any(c => c.Data.IsMonster && c.In("Dark World")), async x => UseHopt(x));
        {
            var s = S("Dark Creator");
            s.ProcCan = x => x.D.Tributable(x.Me).Count(m => m.Data.Race is "Fiend" or "Fairy" || x.IsRace(m, "Fiend") || x.IsRace(m, "Fairy")) >= 2;
            s.Proc = async x =>
            {
                foreach (var t in await x.PickN(x.D.Tributable(x.Me).Where(m => x.IsRace(m, "Fiend") || x.IsRace(m, "Fairy")), 2, 2, "Tribute 2 Fiend and/or Fairy monsters")) await x.D.Tribute(t, x.Self);
                await x.D.SpecialSummon(x.Self, x.Me, await x.D.ChoosePosition(x.Me, x.Self), by: x.Self);
            };
            s.Forbid = (x, q, c) => (q == Q.DestroyByBattle && c == x.Self)
                || (c != null && c.IsToken && c.Name == "Dark Creator Token" && c.Var("creator") == x.Self.Uid + 1 && q is Q.DestroyByBattle or Q.DirectAttack);
            s.OncePerTurn = true;
            s.Can = x => Up(x) && x.D.FreeMonsterZones(x.Me) > 0;
            s.Run = async x =>
            {
                var d = x.D; int me = x.Me;
                while (d.FreeMonsterZones(me) > 0)
                {
                    var t = d.MakeToken("Dark Creator Token", me, 3000, 1000, "DARK", "Fiend", 10);
                    if (!await d.SpecialSummon(t, me, await d.ChoosePosition(me, t), by: x.Self)) break;
                    t.Vars["creator"] = x.Self.Uid + 1;
                }
                d.Linger(d.Turn, (q, c, p) => q == Q.SpecialSummon && (p == me || c?.Controller == me));
            };
            s.On = async (x, e) =>
            {
                if (e.Is(EvKind.Destroyed, x.Self)) await x.D.Destroy(x.D.AllMonsters.Where(m => m.IsToken && m.Var("creator") == x.Self.Uid + 1).ToList(), x.Self);
                if (e.Kind == EvKind.Destroyed && e.Battle && e.Card!.Data.IsMonster && Up(x)) await x.D.Damage(x.Opp, 700, x.Self);
            };
        }
        S("Dark Cure").On = async (x, e) =>
        {
            if (e.Kind == EvKind.Summoned && e.Player == x.Opp && e.Card!.Valid() && e.Card.FaceUp && x.Self.OnField && x.Self.FaceUp) await x.D.Gain(x.Opp, x.Atk(e.Card) / 2, x.Self);
        };
        Spell("Dark Designator", async x =>
        {
            var c = x.Their.Deck.FirstOrDefault(c => c.Name == x.D.Db.Get(x.N).Name);
            if (c != null) { await x.D.ToHand(c, x.Self); x.D.Shuffle(x.Opp); }
        }, null, async x => x.N = (await DeclareName(x, "Declare 1 Monster Card name", d => d.IsMonster)).Idx);
        S("Dark Designator").Note = "You declare from the monster names in this duel (both Decks).";
        {
            var s = Spell("Dark Dragon Ritual", async x => await RitualSummon(x, m => m.Name == "Paladin of Dark Dragon", exact: false),
                x => RitualTargets(x, m => m.Name == "Paladin of Dark Dragon", false).Any());
            s.GraveFx = new Script
            {
                Speed = 1,
                Can = x => x.Self.Var("gyturn") != x.D.Turn && x.My.Deck.Any(c => !c.Data.IsMonster && c.In("Red-Eyes")),
                Cost = async x => await x.D.Banish(x.Self, x.Self),
                Run = x => x.Search(c => !c.Data.IsMonster && c.In("Red-Eyes"), "Add which Red-Eyes Spell/Trap?"),
            };
        }
        S("Dark Driceratops").Allow = (x, q, c) => q == Q.Piercing && c == x.Self;
        {
            static bool Gate(Card c) => c.In("Gate Guardian");
            var s = Spell("Dark Element", async x =>
            {
                await SummonPick(x, x.My.Hand.Concat(x.My.Deck).Concat(x.My.Extra).Where(c => c.Data.IsMonster && Gate(c) && c.Data.Level >= 11), "Special Summon which Gate Guardian?", ignore: true);
                x.D.Shuffle(x.Me);
            }, x => Hopt(x, "1") && x.My.Grave.Any(c => c.Data.IsMonster && Gate(c)) && x.D.FreeMonsterZones(x.Me) > 0,
            async x => { UseHopt(x, "1"); x.D.Pay(x.Me, x.My.Lp / 2); });
            s.GraveFx = new Script
            {
                Speed = 1,
                Can = x => Hopt(x, "2") && x.My.Deck.Concat(x.My.Banished).Any(c => c.Name is "Sanga of the Thunder" or "Kazejin" or "Suijin"),
                Cost = async x => { UseHopt(x, "2"); await x.D.Banish(x.Self, x.Self); },
                Run = async x =>
                {
                    var c = await x.Pick(x.My.Deck.Concat(x.My.Banished).Where(c => c.Name is "Sanga of the Thunder" or "Kazejin" or "Suijin"), "Add which?");
                    if (c != null) { await x.D.ToHand(c, x.Self); x.D.Shuffle(x.Me); }
                },
            };
        }
        Spell("Dark Factory of Mass Production", async x => { foreach (var t in x.T.Where(t => t.Loc == Loc.Grave).ToList()) await x.D.ToHand(t, x.Self); },
            x => x.Targetable(x.My.Grave.Where(c => c.Data.IsNormalMonster)).Count() >= 2,
            async x => x.T.AddRange(await x.PickN(x.Targetable(x.My.Grave.Where(c => c.Data.IsNormalMonster)), 2, 2, "Target 2 Normal Monsters in your GY")));
        {
            var s = OnBattleDeath("Dark Flare Knight", async x => { await SummonPick(x, x.My.Hand.Concat(x.My.Deck).Where(c => c.Name == "Mirage Knight"), "Special Summon Mirage Knight"); x.D.Shuffle(x.Me); });
            s.Allow = (x, q, c) => q == Q.NoBattleDamageToController && c == x.Self;
        }
        Spell("Dark Fusion", async x =>
        {
            x.D.LastFusion = null;
            if (!await x.D.FusionSummon(x.Me, x.Self, resultOk: c => c.Data.Race == "Fiend") || x.D.LastFusion is not { } f) return;
            var d = x.D; int opp = x.Opp;
            d.Linger(d.Turn, (q, c, p) => q == Q.Target && c == f && p == opp);
        }, x => x.D.FusionOptions(x.Me, resultOk: c => c.Data.Race == "Fiend", by: x.Self).Any());
        {
            var s = S("Dark Guardian");
            s.AlsoIn = new[] { "Gate Guardian" };
            static IEnumerable<Card> Of(Ctx x, string n) => x.My.Hand.Concat(x.MyMonsters().FaceUp()).Concat(x.My.Grave).Concat(x.My.Banished).Where(c => c.Name == n);
            s.SummonOk = (x, by, k) => by == x.Self || by?.Name == "Dark Element";
            s.ProcCan = x => x.Self.Loc is Loc.Hand && new[] { "Sanga of the Thunder", "Kazejin", "Suijin" }.All(n => Of(x, n).Any());
            s.Proc = async x =>
            {
                foreach (var n in new[] { "Sanga of the Thunder", "Kazejin", "Suijin" })
                { var c = await x.Pick(Of(x, n), $"Shuffle {n} into the Deck"); if (c != null) await x.D.ToDeck(c); }
                x.D.Shuffle(x.Me);
                await x.D.SpecialSummon(x.Self, x.Me, await x.D.ChoosePosition(x.Me, x.Self), by: x.Self);
            };
            s.AddFx(f =>
            {
                f.Label = "Special Summon (from the GY)"; f.FromGrave = true;
                f.Can = x => new[] { "Sanga of the Thunder", "Kazejin", "Suijin" }.All(n => Of(x, n).Any()) && x.D.FreeMonsterZones(x.Me) > 0;
                f.Run = async x => await s.Proc!(x);
            });
            s.Forbid = (x, q, c) => c == x.Self && (q == Q.DestroyByBattle
                || (x.Self.SummonedBy == "Dark Element" && ((q == Q.Target && x.D.QueryPlayer == x.Opp) || (q == Q.DestroyByEffect && x.D.Destroyer is { } by && by != x.Self && (by.Data.IsMonster || by.Controller == x.Opp)))));
            s.Note = "Summoning it from the GY by shuffling the three is offered as an effect from the GY. Its Dark Element protection blocks targeting and destruction.";
        }
        Spell("Dark Illusion", async x => await x.D.Negate(x.Ev!.Link!),
            x => x.Ev is { Kind: EvKind.Activated, Link: { } l } && (!l.Card.Data.IsMonster || !l.IsCardActivation) && l.X.T.Any(t => t.Loc == Loc.Monster && t.FaceUp && x.IsAttr(t, "DARK")));
        OnSummon("Dark Jeroid", async x =>
        {
            var t = await x.Pick(x.Targetable(x.D.FaceUpMonsters), "Which face-up monster loses 800 ATK?");
            if (t != null) { t.Vars["modatk"] = t.Var("modatk") - 800; x.D.Seq++; }
        });
        {
            var s = S("Dark Magician Girl the Dragon Knight");
            s.Materials = new() { Named("Dark Magician Girl"), Cond("1 Dragon monster", c => c.Data.IsMonster && c.Data.Race == "Dragon") };
            s.Speed = 2; s.AnyTurn = true; s.OncePerTurn = true;
            s.Can = x => Up(x) && x.My.Hand.Count > 0 && x.Targetable(x.D.FieldCards.Where(c => c.FaceUp)).Any();
            s.Cost = async x =>
            {
                await x.D.ToGrave((await x.Pick(x.My.Hand, "Send 1 card from your hand to the GY"))!, x.Self);
                await x.Target(x.D.FieldCards.Where(c => c.FaceUp), "Destroy which face-up card?");
            };
            s.Run = async x => { if (x.T0 is { OnField: true } t) await x.D.Destroy(t, x.Self); };
        }
        {
            var s = S("Dark Magician Knight");
            s.SummonOk = (x, by, k) => by?.Name == "Knight's Title";
            s.On = async (x, e) =>
            {
                if (e.Is(EvKind.Summoned, x.Self) && e.Arg is (int)SummonKind.Special && x.Targetable(x.D.FieldCards).Any())
                { var t = await x.Pick(x.Targetable(x.D.FieldCards), "Destroy which card?"); if (t != null) await x.D.Destroy(t, x.Self); }
            };
        }
        {
            var s = S("Dark Magician of Chaos");
            s.LeaveToBanish = true;
            s.On = async (x, e) =>
            {
                if (e.Kind == EvKind.BattleDestroyedMonster && e.Card == x.Self && e.Other!.Owner == x.Opp && e.Other.Loc == Loc.Grave) await x.D.Banish(e.Other, x.Self);
                if (e.Kind == EvKind.EndPhase && Up(x) && x.Self.SummonTurn == x.D.Turn && x.Self.Summoned != SummonKind.Flip && Hopt(x)
                    && x.Targetable(x.My.Grave.Where(c => c.Data.IsSpell)).Any() && await x.Ask("Add 1 Spell from your GY to your hand?"))
                {
                    UseHopt(x);
                    var c = await x.Pick(x.Targetable(x.My.Grave.Where(c => c.Data.IsSpell)), "Add which Spell?");
                    if (c != null) await x.D.ToHand(c, x.Self);
                }
            };
        }
        Spell("Dark Mirror Force", async x => { foreach (var m in x.OppMonsters().Where(m => m.Defense).ToList()) await x.D.Banish(m, x.Self); }, x => AttackOnMe(x) && x.OppMonsters().Any(m => m.Defense));
        {
            var s = S("Dark Paladin");
            s.Speed = 2; s.AnyTurn = true;
            s.Can = x => Up(x) && x.My.Hand.Count > 0 && x.Ev is { Kind: EvKind.Activated, Link: { IsCardActivation: true } l } && l.Card.Data.IsSpell;
            s.Cost = async x => await x.D.Discard(x.Me, await x.PickN(x.My.Hand, 1, 1, "Discard 1 card"), x.Self);
            s.Run = async x => { if (Up(x)) await x.D.Negate(x.Ev!.Link!); };
            s.Stats = (x, c, st) => { if (c == x.Self) st.Add(500 * (x.D.FaceUpMonsters.Count(m => x.IsRace(m, "Dragon")) + x.D.Players.Sum(p => p.Grave.Count(g => g.Data.IsMonster && g.Data.Race == "Dragon"))), 0); };
        }
        Spell("Dark Renewal", async x =>
        {
            foreach (var t in x.T.Where(t => t.Valid()).ToList()) await x.D.ToGrave(t, x.Self);
            var l = x.My.Deck.Concat(x.My.Grave).Where(c => c.Data.IsMonster && c.Data.Attribute == "DARK" && c.Data.Race == "Spellcaster").ToList();
            if (l.Count > 0 && await x.Ask("Special Summon a DARK Spellcaster from your Deck or GY?")) { await SummonPick(x, l, "Special Summon which?"); x.D.Shuffle(x.Me); }
        }, x => x.Ev is { Kind: EvKind.Summoned } e && e.Player == x.Opp && e.Summoned(SummonKind.Normal, SummonKind.Tribute, SummonKind.Special, SummonKind.Fusion, SummonKind.Ritual)
            && e.Card!.Valid() && x.Targetable(x.MyMonsters().FaceUp().Where(m => x.IsRace(m, "Spellcaster"))).Any(),
        async x => { x.T.Add(x.Ev!.Card!); await x.Target(x.MyMonsters().FaceUp().Where(m => x.IsRace(m, "Spellcaster")), "Target 1 Spellcaster you control"); });
        S("Dark Sanctuary").On = async (x, e) =>
        {
            if (e.Kind == EvKind.AttackDeclared && e.Player == x.Opp && e.Card is { } a && x.Self.OnField && x.Self.FaceUp && x.D.TossCoins(x.Me, 1, x.Self) == 1)
            {
                int half = x.Atk(a) / 2;
                x.D.AttackNegated = true;
                await x.D.Damage(x.Opp, half, x.Self);
            }
        };
        S("Dark Sanctuary").Note = "With Destiny Board, each Spirit Message may be Special Summoned as a monster instead (offered when it would be placed).";
        ScorpionHit("Dark Scorpion - Chick the Yellow", new[] { "Return 1 card on the field to the hand", "Look at the top card of their Deck" }, async (x, i) =>
        {
            if (i == 0) { var t = await x.Pick(x.Targetable(x.D.FieldCards), "Return which card to the hand?"); if (t != null) await x.D.ToHand(t, x.Self); return; }
            if (x.Their.Deck.Count == 0) return;
            var top = x.Their.Deck[0];
            x.D.Reveal(x.Me, top);
            if (await x.D.ChooseText(x.Me, $"{top.Name}: return it to the top or the bottom?", new[] { "Top", "Bottom" }, x.Self) == 1) { x.Their.Deck.RemoveAt(0); x.Their.Deck.Add(top); }
        });
        ScorpionHit("Dark Scorpion - Gorg the Strong", new[] { "Return 1 opponent's monster to the top of the Deck", "Send the top card of their Deck to the GY" }, async (x, i) =>
        {
            if (i == 0) { var t = await x.Pick(x.Targetable(x.OppMonsters()), "Return which monster to the top of the Deck?"); if (t != null) await x.D.ToDeck(t, top: true); return; }
            if (x.Their.Deck.Count > 0) await x.D.ToGrave(x.Their.Deck[0], x.Self);
        });
        ScorpionHit("Dark Scorpion - Meanae the Thorn", new[] { "Add a Dark Scorpion card from your Deck", "Add a Dark Scorpion card from your GY" }, async (x, i) =>
        {
            if (i == 0) await x.Search(DarkScorpion, "Add which Dark Scorpion card?");
            else { var t = await x.Pick(x.Targetable(x.My.Grave.Where(DarkScorpion)), "Add which Dark Scorpion card?"); if (t != null) await x.D.ToHand(t, x.Self); }
        });
        {
            string[] five = { "Don Zaloog", "Dark Scorpion - Cliff the Trap Remover", "Dark Scorpion - Chick the Yellow", "Dark Scorpion - Gorg the Strong", "Dark Scorpion - Meanae the Thorn" };
            Spell("Dark Scorpion Combination", async x =>
            {
                foreach (var m in x.MyMonsters().Where(m => m.FaceUp && five.Contains(Scripts.NameOf(m))))
                { m.Vars["direct"] = x.D.Turn; m.Vars["fixeddmg"] = 400; m.Vars["fixeddmgturn"] = x.D.Turn; }
            }, x => five.All(n => x.MyMonsters().Any(m => m.FaceUp && Scripts.NameOf(m) == n)));
        }
        {
            var s = Spell("Dark Spell Regeneration", async x =>
            {
                if (x.T0 is not { Loc: Loc.Grave } t || x.My.FreeSpellZones == 0) return;
                if (x.D.Place(t, x.Me, t.Data.Is("Field") ? Loc.Field : Loc.Spell)) { t.FaceUp = false; t.SetTurn = x.D.Turn; await x.D.Raise(new Ev { Kind = EvKind.Set, Card = t, Player = x.Me }); }
            }, x => AttackOnMe(x) && x.My.FreeSpellZones > 0 && x.Targetable(x.Their.Grave.Where(c => c.Data.IsSpell)).Any(),
            async x => await x.Target(x.Their.Grave.Where(c => c.Data.IsSpell), "Set which Spell from your opponent's GY?"));
            s.GraveFx = new Script
            {
                Speed = 1,
                Can = x => x.My.Grave.Any(c => c.Name == "The Winged Dragon of Ra") && x.My.Hand.Concat(x.Spells(x.Me).Where(c => !c.FaceUp)).Any(c => c.Name == "Monster Reborn") && x.D.FreeMonsterZones(x.Me) > 0,
                Cost = async x =>
                {
                    await x.D.Banish(x.Self, x.Self);
                    var mr = await x.Pick(x.My.Hand.Concat(x.Spells(x.Me).Where(c => !c.FaceUp)).Where(c => c.Name == "Monster Reborn"), "Send Monster Reborn to the GY");
                    if (mr != null) await x.D.ToGrave(mr, x.Self);
                },
                Run = async x =>
                {
                    var ra = x.My.Grave.FirstOrDefault(c => c.Name == "The Winged Dragon of Ra");
                    if (ra == null || !await x.D.SpecialSummon(ra, x.Me, await x.D.ChoosePosition(x.Me, ra), by: x.Self, ignore: true)) return;
                    if (x.OppMonsters().Any() && await x.Ask("Send 1 monster your opponent controls to the GY?"))
                    { var t = await x.Pick(x.OppMonsters(), "Send which monster to the GY?"); if (t != null) await x.D.ToGrave(t, x.Self); }
                    var d = x.D;
                    d.Later(d.Turn, Phase.End, async () => { if (ra.Loc == Loc.Monster) await d.ToGrave(ra); });
                },
            };
            s.Note = "The Egyptian Gods are still placeholders (0/0, rules not decided), so Ra here is that placeholder.";
        }
        {
            var s = S("Dark Summoning Beast");
            static bool Lord(Card c) => c.Name is "Uria, Lord of Searing Flames" or "Hamon, Lord of Striking Thunder" or "Raviel, Lord of Phantasms";
            s.Can = x => Up(x) && Hopt(x) && x.My.Hand.Concat(x.My.Deck).Any(Lord);
            s.Cost = async x => { UseHopt(x); await x.D.Tribute(x.Self, x.Self); };
            s.Run = async x =>
            {
                await SummonPick(x, x.My.Hand.Concat(x.My.Deck).Where(Lord), "Special Summon which Sacred Beast?", ignore: true);
                x.D.Shuffle(x.Me);
                var d = x.D; int me = x.Me;
                d.Linger(d.Turn, (q, c, p) => q == Q.Attack && c != null && c.Controller == me);
            };
            s.AddFx(f =>
            {
                f.Label = "Add a Sacred Beast"; f.FromGrave = true;
                f.Can = x => x.My.Deck.Any(Lord);
                f.Cost = async x => await x.D.Banish(x.Self, x.Self);
                f.Run = x => x.Search(Lord, "Add which Sacred Beast?");
            });
        }
        Spell("Dark World Dealings", async x =>
        {
            await x.D.Draw(x.Me, 1); await x.D.Draw(x.Opp, 1);
            if (x.My.Hand.Count > 0) await x.D.Discard(x.Me, await x.PickN(x.My.Hand, 1, 1, "Discard 1 card"), x.Self);
            if (x.Their.Hand.Count > 0) await x.D.Discard(x.Opp, await x.D.Pick(x.Opp, x.Their.Hand, 1, 1, "Discard 1 card", x.Self), x.Self);
        }, x => x.My.Deck.Count > 0 && x.Their.Deck.Count > 0);
        Spell("Dark World Lightning", async x =>
        {
            if (x.T0 is not { OnField: true, FaceUp: false } t) return;
            await x.D.Destroy(t, x.Self);
            if (x.My.Hand.Count > 0) await x.D.Discard(x.Me, await x.PickN(x.My.Hand, 1, 1, "Discard 1 card"), x.Self);
        }, x => x.My.Hand.Any(c => c != x.Self) && x.Targetable(x.D.FieldCards.Where(c => !c.FaceUp)).Any(),
        async x => await x.Target(x.D.FieldCards.Where(c => !c.FaceUp), "Destroy which Set card?"));
        S("Darkbishop Archfiend").On = async (x, e) =>
        {
            if (e.Kind == EvKind.Standby && e.Player == x.Me && Up(x) && x.D.CountActive("Pandemonium") == 0) { if (x.My.Lp < 500) await x.D.Destroy(x.Self); else x.D.Pay(x.Me, 500, x.Self); }   // can't pay the mandatory 500: destroyed
            if (e.Kind == EvKind.Activated && e.Link is { } l && l.Player == x.Opp && Up(x) && l.X.T.Any(t => t.Controller == x.Me && t.Loc == Loc.Monster && t.FaceUp && t.In("Archfiend")))
            {
                int r = x.D.Die(x.Me, x.Self);
                if (r is 1 or 3 or 6) await x.D.Negate(l);
            }
        };
        S("Darkbishop Archfiend").Note = "Its die is rolled when your Archfiend is targeted (not when the effect resolves).";
        {
            var s = S("Darkness Destroyer");
            s.SummonOk = (x, by, k) => false;
            s.Allow = (x, q, c) => c == x.Self && q is Q.DoubleAttack or Q.Piercing;
        }
        {
            var s = S("Darkness Neosphere");
            s.SummonOk = (x, by, k) => by == x.Self;
            s.Forbid = (x, q, c) => q == Q.DestroyByBattle && c == x.Self;
            s.AddFx(f =>
            {
                f.Label = "Special Summon"; f.FromHand = true; f.Speed = 2; f.AnyTurn = true;
                f.Can = x => AttackOnMe(x) && x.MyMonsters().Any(m => m.FaceUp && x.IsRace(m, "Fiend")) && x.My.Hand.Any(c => c != x.Self && c.Data.IsMonster && c.Data.Race == "Fiend");
                f.Cost = async x =>
                {
                    var a = await x.Pick(x.MyMonsters().Where(m => m.FaceUp && x.IsRace(m, "Fiend")), "Send 1 Fiend you control to the GY");
                    var b = await x.Pick(x.My.Hand.Where(c => c != x.Self && c.Data.IsMonster && c.Data.Race == "Fiend"), "Send 1 Fiend from your hand to the GY");
                    if (a != null) await x.D.ToGrave(a, x.Self);
                    if (b != null) await x.D.ToGrave(b, x.Self);
                };
                f.Run = async x => { if (x.Self.Loc == Loc.Hand) await x.D.SpecialSummon(x.Self, x.Me, await x.D.ChoosePosition(x.Me, x.Self), by: x.Self); };
            });
            s.OncePerTurn = true;
            s.Can = x => Up(x) && x.Spells(x.Me).Any(c => c.FaceUp && c.Data.IsTrap);
            s.Run = async x => { foreach (var c in x.Spells(x.Me).Where(c => c.FaceUp && c.Data.IsTrap).ToList()) await x.D.ToHand(c, x.Self); };
        }
        S("Decoyroid").Forbid = (x, q, c) => q == Q.BeAttacked && c != null && c != x.Self && c.Controller == x.Me && c.FaceUp && Up(x);
        Spell("Dedication through Light and Darkness", async x =>
        {
            await SummonPick(x, x.My.Hand.Concat(x.My.Deck).Concat(x.My.Grave).Where(c => c.Name == "Dark Magician of Chaos"), "Special Summon Dark Magician of Chaos");
            x.D.Shuffle(x.Me);
        }, x => x.D.Tributable(x.Me).Any(m => Scripts.NameOf(m) == "Dark Magician") && x.My.Hand.Concat(x.My.Deck).Concat(x.My.Grave).Any(c => c.Name == "Dark Magician of Chaos"),
        async x => { var t = await x.Pick(x.D.Tributable(x.Me).Where(m => Scripts.NameOf(m) == "Dark Magician"), "Tribute 1 Dark Magician"); if (t != null) await x.D.Tribute(t, x.Self); });
        S("Deep Diver").On = async (x, e) =>
        {
            if (e.Kind == EvKind.BattleEnd && x.Self.Loc == Loc.Grave && x.D.BattleDestroyedThisTurn(x.Self.Owner).Contains(x.Self) && x.My.Deck.Any(c => c.Data.IsMonster))
            {
                var c = await x.Pick(x.My.Deck.Where(c => c.Data.IsMonster), "Place which monster on top of your Deck?");
                if (c == null) return;
                x.D.Reveal(x.Opp, c);
                x.My.Deck.Remove(c); x.My.Deck.Insert(0, c);
            }
        };
        Flip("Dekoichi the Battlechanted Locomotive", async x => await x.D.Draw(x.Me, 1 + x.MyMonsters().Count(m => m.FaceUp && m.Name == "Bokoichi the Freightening Car")));
        Spell("Demise of the Land", async x =>
        {
            var f = await x.Pick(x.My.Deck.Where(c => c.Data.Is("Field") && c.Data.IsSpell), "Activate which Field Spell from your Deck?");
            if (f == null) return;
            if (x.My.FieldZone is { } old) await x.D.ToGrave(old, x.Self);
            x.D.Place(f, x.Me, Loc.Field); f.FaceUp = true;
            x.D.Shuffle(x.Me);
            if (x.Their.FieldZone is { FaceUp: true } theirs) await x.D.Destroy(theirs, f);
            await x.D.Raise(new Ev { Kind = EvKind.Activated, Card = f, Player = x.Me });
        }, x => x.Ev is { Kind: EvKind.Summoned } e && e.Player == x.Opp && e.Arg is (int)SummonKind.Special or (int)SummonKind.Fusion or (int)SummonKind.Ritual && x.My.Deck.Any(c => c.Data.Is("Field") && c.Data.IsSpell));
        S("Demise of the Land").Note = "The Field Spell is placed face-up and applies; a Field Spell's own \"when activated\" effect is not run.";
        {
            var s = S("Demise, King of Armageddon");
            s.Can = x => Up(x) && x.My.Lp > 2000 && x.D.FieldCards.Any(c => c != x.Self);
            s.Cost = async x => x.D.Pay(x.Me, 2000);
            s.Run = async x => await x.D.Destroy(x.D.FieldCards.Where(c => c != x.Self).ToList(), x.Self);
        }
    }

    /// Fusion Monsters that "must be Special Summoned with Dark Fusion" (the Evil HEROes).
    static bool NeedsDarkFusion(Card c) => c.Data.Desc.Contains("\"Dark Fusion\"");
}
