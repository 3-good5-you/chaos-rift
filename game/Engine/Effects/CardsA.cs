namespace PoC.Engine;

// Layer 3, part A: hand-written effects (mostly the Yugi pool).
public static partial class Scripts
{
    /// Big Shield Gardna (face-down) negates a spell that targets only it and turns face-up Defense.
    internal static async Flow<bool> GardnaBlocks(Ctx x, Card t)
    {
        if (!x.Self.Data.IsSpell || t.Name != "Big Shield Gardna" || t.FaceUp || t.Loc != Loc.Monster) return false;
        await x.D.ChangePosition(t, true, true, flipEffect: false);
        return true;
    }

    static bool HasTrap(Ctx x) => x.AllSpells().Any(c => !c.FaceUp || c.Data.IsTrap);
    static IEnumerable<Card> SetOrFaceUp(Ctx x, bool traps) => x.AllSpells().Where(c => !c.FaceUp || (traps ? c.Data.IsTrap : c.Data.IsSpell));

    static void CardsA(CardDb db)
    {
        Spell("Graceful Charity", async x =>
        {
            await x.D.Draw(x.Me, 3);
            await x.D.Discard(x.Me, await x.PickN(x.My.Hand, 2, 2, "Discard 2 cards"), x.Self);
        });
        Spell("Card Destruction", async x =>
        {
            var n = new[] { x.D.P(0).Hand.Count, x.D.P(1).Hand.Count };
            for (int p = 0; p < 2; p++) await x.D.Discard(p, x.D.P(p).Hand.ToList(), x.Self);
            for (int p = 0; p < 2; p++) await x.D.Draw(p, n[p]);
        });

        // ---- battle traps
        Spell("Mirror Force", async x => await x.D.Destroy(x.OppMonsters().Where(m => m.Attack).ToList(), x.Self), AttackOnMe);
        Spell("Magic Cylinder", async x =>
        {
            var a = x.Ev!.Card!;
            x.D.AttackNegated = true;
            if (a.Valid()) await x.D.Damage(x.Opp, x.Atk(a), x.Self);
        }, AttackOnMe);
        Spell("Enchanted Javelin", async x => { if (x.Ev!.Card!.Valid()) await x.D.Gain(x.Me, x.Atk(x.Ev.Card), x.Self); }, AttackOnMe);
        Spell("Widespread Ruin", async x =>
        {
            var l = x.OppMonsters().Where(m => m.Attack).ToList();
            if (l.Count == 0) return;
            int max = l.Max(m => x.Atk(m));
            var t = await x.Pick(l.Where(m => x.Atk(m) == max), "Destroy which monster?");
            if (t != null) await x.D.Destroy(t, x.Self);
        }, AttackOnMe);
        Spell("Fissure", async x =>
        {
            var l = x.OppMonsters().FaceUp().ToList();
            if (l.Count == 0) return;
            int min = l.Min(m => x.Atk(m));
            var ties = l.Where(m => x.Atk(m) == min).OrderBy(m => m.Zone).ToList();
            var t = x.Classic ? ties[0] : await x.Pick(ties, "Destroy which monster?");
            if (t != null) await x.D.Destroy(t, x.Self);
        }, x => x.OppMonsters().FaceUp().Any());
        Spell("Two-Pronged Attack", async x =>
        {
            await x.D.Destroy(x.T.Where(c => c.Valid()).ToList(), x.Self);
        }, x => x.MyMonsters().Count() >= 2 && x.Targetable(x.OppMonsters()).Any(),
        async x => { await x.Target(x.MyMonsters(), "Destroy 2 of your monsters", 2); await x.Target(x.OppMonsters(), "Destroy 1 opponent monster"); });
        Spell("Dark Hole", async x => await x.D.Destroy(x.AllMonsters().ToList(), x.Self), x => x.AllMonsters().Any());
        Spell("Raigeki", async x => await x.D.Destroy(x.OppMonsters().ToList(), x.Self), x => x.OppMonsters().Any());

        // ---- negation traps keyed to a specific spell
        static bool OppActivates(Ctx x, string name) => x.Ev is { Kind: EvKind.Activated } e && e.Player == x.Opp && e.Card!.Name == name;
        Spell("White Hole", async x =>
        {
            // PoC: negates Dark Hole and destroys the Dark Hole player's monsters (printed text only protects yours).
            await x.D.Negate(x.Ev!.Link!);
            await x.D.Destroy(x.OppMonsters().ToList(), x.Self);
        }, x => OppActivates(x, "Dark Hole"));
        Spell("Anti Raigeki", async x => { await x.D.Negate(x.Ev!.Link!); await x.D.Destroy(x.OppMonsters().ToList(), x.Self); }, x => OppActivates(x, "Raigeki"));
        Spell("Call of the Grave", async x => await x.D.Negate(x.Ev!.Link!), x => OppActivates(x, "Monster Reborn"));
        Spell("Magic Jammer", async x => await x.D.Negate(x.Ev!.Link!),
            x => x.Ev is { Kind: EvKind.Activated } e && e.Player == x.Opp && e.Card!.Data.IsSpell && e.Link!.IsCardActivation && x.My.Hand.Count > 0,
            async x => await x.D.Discard(x.Me, await x.PickN(x.My.Hand, 1, 1, "Discard 1 card (cost)"), x.Self));

        Spell("De-Spell", async x =>
        {
            var t = x.T0;
            if (t == null || !t.OnField) return;
            if (t.Data.IsSpell) await x.D.Destroy(t, x.Self);
            else if (!t.FaceUp) x.D.Reveal(x.Me, t); // face-down Trap: revealed, stays
        }, x => x.Targetable(SetOrFaceUp(x, false).Where(c => c != x.Self)).Any(),
        async x => await x.Target(SetOrFaceUp(x, false).Where(c => c != x.Self), "Destroy which Spell card?"));
        Spell("Remove Trap", async x => { if (x.T0 is { OnField: true } t) await x.D.Destroy(t, x.Self); },
            x => x.AllSpells().Any(c => c.FaceUp && c.Data.IsTrap),
            async x => await x.Target(x.AllSpells().Where(c => c.FaceUp && c.Data.IsTrap), "Destroy which face-up Trap?"));

        Spell("Change of Heart", async x =>
        {
            var t = x.T0;
            if (t == null || !t.Valid() || t.Controller != x.Opp) return;
            if (x.Classic && await GardnaBlocks(x, t)) return;
            if (await x.D.TakeControl(t, x.Me))
                x.D.Later(x.D.Turn, Phase.End, async () => { if (t.Valid() && t.Controller == x.Me) await x.D.TakeControl(t, t.Owner); });
        }, x => x.Targetable(x.OppMonsters()).Any() && x.D.FreeMonsterZones(x.Me) > 0,
        async x => await x.Target(x.OppMonsters(), "Take control of which monster?"));

        Spell("Monster Reborn", async x =>
        {
            // Only monsters it can legally Special Summon (not Ra, not a Fusion that was never properly Summoned).
            var g = x.D.Players.SelectMany(p => p.Grave).Where(c => c.Data.IsMonster && x.D.CanSpecialSummon(c, x.Me, by: x.Self)).ToList();
            var t = await x.Pick(g, "Special Summon which monster?");
            if (t != null) await x.D.SpecialSummon(t, x.Me, await x.D.ChoosePosition(x.Me, t), by: x.Self);
        }, x => x.D.FreeMonsterZones(x.Me) > 0 && x.D.Players.Any(p => p.Grave.Any(c => c.Data.IsMonster && x.D.CanSpecialSummon(c, x.Me, by: x.Self)))
            && !(x.Classic && x.D.CountActive("Call of the Dark") > 0));

        static Script Pump(string name, int atk, int def) => Spell(name,
            async x => { if (x.T0 is { } t && t.Valid() && t.FaceUp) x.D.Boost(t, atk, def); },
            x => x.Targetable(x.D.FaceUpMonsters).Any(),
            async x => await x.Target(x.D.FaceUpMonsters, $"{name}: which monster?"));
        Pump("Reinforcements", 500, 0); Pump("Castle Walls", 0, 500);
        Pump("Rush Recklessly", 700, 0); Pump("The Reliable Guardian", 0, 700); Pump("Snake Fang", 0, -500);
        Spell("Waboku", async x => { x.D.TurnFlags.Add($"nobattledamage{x.Me}"); x.D.TurnFlags.Add($"nobattledestroy{x.Me}"); }, x => !x.Turn());
        Spell("Reverse Trap", async x => x.D.TurnFlags.Add("reverse"));
        Spell("Stop Defense", async x => { if (x.T0 is { } t && t.Valid() && t.Defense) await x.D.ChangePosition(t, false, true); },
            x => x.Targetable(x.OppMonsters().Where(m => m.Defense)).Any(),
            async x => await x.Target(x.OppMonsters().Where(m => m.Defense), "Change which monster to Attack Position?"));

        {
            var s = Spell("Swords of Revealing Light", async x =>
            {
                foreach (var m in x.OppMonsters().FaceDown().ToList()) await x.D.ChangePosition(m, m.Defense, true);   // flipped face-up: their FLIP effects activate
                x.Self.Vars["stay"] = 1;
                x.Self.Vars["turns"] = 0;
            });
            s.Forbid = (x, q, c) => q == Q.Attack && c != null && c.Controller == x.Opp;
            s.On = async (x, e) =>
            {
                if (e.Kind == EvKind.EndPhase && e.Player == x.Opp && x.Self.OnField && x.Self.Var("stay") == 1 && x.Self.Inc("turns") >= 3)
                    await x.D.Destroy(x.Self);
            };
        }
        {
            // House rule (user, 2026-11-02): face-up monsters only (the text and the official ruling allow a face-down one).
            var s = Spell("Spellbinding Circle", async x => { if (x.T0 is { } t && t.Valid()) x.Self.Vars["target"] = t.Uid + 1; },
                x => x.Targetable(x.OppMonsters().Where(m => m.FaceUp)).Any(),
                async x => await x.Target(x.OppMonsters().Where(m => m.FaceUp), "Bind which monster?"));
            s.Forbid = (x, q, c) => q is Q.Attack or Q.ChangePos && c != null && c.Uid + 1 == x.Self.Var("target");
            s.On = async (x, e) => { if (e.Kind == EvKind.LeftField && e.Card!.Uid + 1 == x.Self.Var("target") && x.Self.OnField) await x.D.Destroy(x.Self); };
        }
        {
            var s = Spell("Dragon Capture Jar", async x =>
            {
                foreach (var m in x.D.FaceUpMonsters.Where(m => x.IsRace(m, "Dragon") && m.Attack).ToList()) await x.D.ChangePosition(m, true);
            });
            s.Forbid = (x, q, c) => q == Q.ChangePos && c != null && c.FaceUp && x.D.Race(c) == "Dragon";
            s.On = async (x, e) =>
            {
                if (e.Kind == EvKind.Summoned && e.Card!.Valid() && e.Card.Attack && x.IsRace(e.Card, "Dragon")) await x.D.ChangePosition(e.Card, true);
            };
        }

        Flip("Man-Eater Bug", async x => { var t = await x.Pick(x.Targetable(x.AllMonsters()), "Destroy which monster?"); if (t != null) await x.D.Destroy(t, x.Self); });
        Flip("Hane-Hane", async x => { var t = await x.Pick(x.Targetable(x.AllMonsters()), "Return which monster to the hand?"); if (t != null) await x.D.ToHand(t, x.Self); });
        {
            var s = S("Wall of Illusion");
            s.On = async (x, e) =>
            {
                // Returns the attacker even when the Wall itself was destroyed in that battle (it is then in the Graveyard).
                if (e.Kind == EvKind.AfterDamage && e.Other == x.Self && (x.Self.Loc == Loc.Grave || (x.Self.Valid() && x.Self.FaceUp)) && e.Card!.Valid()) await x.D.ToHand(e.Card, x.Self);
            };
        }
        Flip("The Stern Mystic", async x => { var all = x.D.FieldCards.Where(c => !c.FaceUp).ToList(); x.D.Reveal(0, all); x.D.Reveal(1, all); });
        static Flow DestroyKind(Ctx x, bool traps) => Destroy1(x, SetOrFaceUp(x, traps), traps ? "Destroy which Trap card?" : "Destroy which Spell card?", c => traps ? c.Data.IsTrap : c.Data.IsSpell);
        Flip("Reaper of the Cards", x => DestroyKind(x, true));
        Flip("Trap Master", x => DestroyKind(x, true));
        Flip("Armed Ninja", x => DestroyKind(x, false));

        {
            var s = S("Kuriboh");
            s.FromHand = true; s.AnyTurn = true; s.Speed = 2;
            s.Can = x => x.Ev is { Kind: EvKind.DamageCalc } e && e.Player == x.Opp && (e.Other == null || e.Other.Controller == x.Me) && WouldTakeBattleDamage(x.D, x.Me);
            s.Cost = async x => await x.D.Discard(x.Me, new[] { x.Self }, x.Self);
            s.Run = async x => x.D.BattleFlags.Add($"nodamage{x.Me}");
        }
        S("Exodia the Forbidden One").Marker = true; // win check is in Duel.CheckWin
        foreach (var n in Duel.Substitutes) S(n).Marker = true; // fusion substitutes: Duel.FusionMaterials

        Spell("Polymerization", async x => await x.D.FusionSummon(x.Me, x.Self), x => x.D.FusionOptions(x.Me).Any());

        SelfBoost("Swamp Battleguard", x => 500 * x.MyMonsters().FaceUp().Count(m => m.Name == "Lava Battleguard"));
        SelfBoost("Lava Battleguard", x => 500 * x.MyMonsters().FaceUp().Count(m => m.Name == "Swamp Battleguard"));

        {
            var s = S("Time Wizard");
            s.OncePerTurn = true;
            s.Run = async x =>
            {
                if (await x.D.CallCoin(x.Me, x.Self)) await x.D.Destroy(x.OppMonsters().ToList(), x.Self);
                else
                {
                    var mine = x.MyMonsters().ToList();
                    int total = mine.Where(m => m.FaceUp).Sum(m => x.Atk(m));
                    await x.D.Destroy(mine, x.Self);
                    await x.D.Damage(x.Me, total / 2, x.Self);
                }
            };
        }
        Flip("Dragon Piper", async x =>
        {
            await x.D.Destroy(x.AllSpells().Where(c => c.FaceUp && c.Name == "Dragon Capture Jar").ToList(), x.Self);
            foreach (var m in x.D.FaceUpMonsters.Where(m => x.IsRace(m, "Dragon") && m.Defense).ToList()) await x.D.ChangePosition(m, false);
        });

        static Func<Ctx, Ev, Flow> LeftFieldToGrave(Func<Ctx, Flow> f) => (x, e) =>
            e.Is(EvKind.ToGrave, x.Self) && e.From == Loc.Monster ? f(x) : Flow.Done;
        S("Sangan").On = LeftFieldToGrave(x => x.Search(c => c.Data.IsMonster && c.Data.Atk <= 1500, "Add a monster with 1500 or less ATK"));
        S("Witch of the Black Forest").On = LeftFieldToGrave(x => x.Search(c => c.Data.IsMonster && c.Data.Def <= 1500, "Add a monster with 1500 or less DEF"));

        // ---- Petit Moth line: Cocoon of Evolution is equipped from the hand; the Moths are summoned by tributing the cocooned Petit Moth.
        {
            var s = S("Cocoon of Evolution");
            s.FromHand = true;
            s.Can = x => x.MyMonsters().FaceUp().Any(m => m.Name == "Petit Moth" && !x.D.EquipsOf(m).Any(e => e.Name == "Cocoon of Evolution")) && x.My.FreeSpellZones > 0;
            s.Run = async x =>
            {
                var t = await x.Pick(x.MyMonsters().FaceUp().Where(m => m.Name == "Petit Moth"), "Equip Cocoon to which Petit Moth?");
                if (t == null || x.Self.Loc != Loc.Hand) return;
                x.D.Place(x.Self, x.Me, Loc.Spell);
                await x.D.Equip(x.Self, t);
                x.Self.Vars["turns"] = 0;
            };
            s.Base = (x, c, st) => { if (x.Self.EquippedTo == c && x.Self.Loc == Loc.Spell) { st.BaseAtk = 0; st.BaseDef = 2000; } };
            s.On = async (x, e) => { if (e.Kind == EvKind.Standby && e.Player == x.Me && x.Self.EquippedTo != null) x.Self.Inc("turns"); };
        }
        static void Moth(string name, int turns)
        {
            var s = S(name);
            s.NoNormalSummon = true;
            s.ProcFromDeck = true;
            Card? Cocooned(Ctx x) => x.MyMonsters().FirstOrDefault(m => m.Name == "Petit Moth" && x.D.EquipsOf(m).Any(e => e.Name == "Cocoon of Evolution" && e.Var("turns") >= turns));
            s.ProcCan = x => x.Self.Loc is Loc.Hand or Loc.Deck && Cocooned(x) != null;
            s.Proc = async x =>
            {
                var m = Cocooned(x)!;
                await x.D.MoveTo(m, Loc.Grave);
                await x.D.SpecialSummon(x.Self, x.Me, await x.D.ChoosePosition(x.Me, x.Self));
                if (x.Self.Valid()) x.D.Shuffle(x.Me);
            };
        }
        Moth("Larvae Moth", 2); Moth("Great Moth", 4); Moth("Perfectly Ultimate Great Moth", 6);

        {
            var s = S("Harpie Lady Sisters");
            s.NoNormalSummon = true;
        }
        Spell("Elegant Egotist", async x =>
        {
            var l = x.My.Hand.Concat(x.My.Deck).Where(c => c.Name is "Harpie Lady" or "Harpie Lady Sisters");
            var t = await x.Pick(l, "Special Summon which Harpie?");
            if (t != null) { bool fromDeck = t.Loc == Loc.Deck; await x.D.SpecialSummon(t, x.Me, await x.D.ChoosePosition(x.Me, t), by: x.Self); if (fromDeck) x.D.Shuffle(x.Me); }
        }, x => x.D.FaceUpMonsters.Any(m => m.Name == "Harpie Lady") && x.D.FreeMonsterZones(x.Me) > 0);

        {
            var s = S("Castle of Dark Illusions");
            s.On = async (x, e) =>
            {
                if (e.Is(EvKind.Flipped, x.Self)) x.Self.Vars["castle"] = 1;
                else if (e.Kind == EvKind.Standby && e.Player == x.Me && x.OnFieldUp() && x.Self.Var("castle") == 1) x.Self.Vars["steps"] = Math.Min(4, x.Self.Var("steps") + 1);
            };
            s.Stats = (x, c, st) => { if (x.Self.Var("castle") == 1 && st.Race == "Zombie") st.Add(200 * (1 + x.Self.Var("steps")), 200 * (1 + x.Self.Var("steps"))); };
        }
        {
            var s = S("Catapult Turtle");
            s.Can = x => x.MyMonsters().Any(m => m != x.Self && !x.D.Forbidden(Q.Tribute, m));
            s.Cost = async x =>
            {
                var t = await x.Pick(x.MyMonsters().Where(m => m != x.Self && !x.D.Forbidden(Q.Tribute, m)), "Tribute which monster?");
                x.N = t == null ? 0 : x.Atk(t);
                if (t != null) await x.D.MoveTo(t, Loc.Grave);
            };
            s.Run = async x => await x.D.Damage(x.Opp, x.N / 2, x.Self);
        }
        {
            var s = S("Crass Clown");
            s.On = async (x, e) =>
            {
                bool toAtk = (e.Is(EvKind.PositionChanged, x.Self) && x.Self.Attack && x.Self.FaceUp && e.Arg == 0)
                    || (x.Classic && e.Is(EvKind.Summoned, x.Self) && e.Summoned(SummonKind.Flip));
                if (!toAtk) return;
                var t = await x.Pick(x.Targetable(x.OppMonsters()), "Return which monster to the hand?");
                if (t != null) await x.D.ToHand(t, x.Self);
            };
        }
        {
            var s = S("Pumpking the King of Ghosts");
            s.On = async (x, e) => { if (e.Kind == EvKind.Standby && e.Player == x.Me && x.OnFieldUp() && x.D.CountActive("Castle of Dark Illusions") > 0) x.Self.Vars["steps"] = Math.Min(3, x.Self.Var("steps") + 1); };
            s.Stats = (x, c, st) => { if (c == x.Self && x.D.CountActive("Castle of Dark Illusions") > 0) st.Add(100 * (1 + x.Self.Var("steps")), 100 * (1 + x.Self.Var("steps"))); };
        }
        Flip("Mask of Darkness", async x => { var t = await x.Pick(x.My.Grave.Where(c => c.Data.IsTrap), "Add which Trap to your hand?"); if (t != null) await x.D.ToHand(t, x.Self); });
        OnBattleDamage("White Magical Hat", async (x, e) => await x.D.DiscardRandom(x.Opp, 1, x.Self));
        OnBattleDamage("Masked Sorcerer", async (x, e) => await x.D.Draw(x.Me, 1));
        OnBattleDamage("The Bistro Butcher", async (x, e) => await x.D.Draw(x.Opp, 2));

        static Func<Ctx, Ev, Flow> ToDefense(Func<Ctx, Flow> f) => (x, e) => e.Is(EvKind.PositionChanged, x.Self) && e.Arg == 1 && x.Self.FaceUp ? f(x) : Flow.Done;
        S("Dream Clown").On = ToDefense(async x => { var t = await x.Pick(x.Targetable(x.OppMonsters()), "Destroy which monster?"); if (t != null) await x.D.Destroy(t, x.Self); });
        S("Tainted Wisdom").On = ToDefense(async x => x.D.Shuffle(x.Me));
        S("Yado Karu").On = ToDefense(async x =>
        {
            var l = await x.PickN(x.My.Hand, 0, x.My.Hand.Count, "Put cards from your hand on the bottom of your Deck (in order)");
            foreach (var c in l) await x.D.ToDeck(c, top: false);
        });

        Flip("Big Eye", async x =>
        {
            var top = x.My.Deck.Take(5).ToList();
            var order = await x.PickN(top, top.Count, top.Count, "Order the top cards (first pick = top)");
            for (int i = order.Count - 1; i >= 0; i--) { x.My.Deck.Remove(order[i]); x.My.Deck.Insert(0, order[i]); }
        });
        S("Penguin Knight").On = async (x, e) =>
        {
            if (e.Is(EvKind.ToGrave, x.Self) && e.From == Loc.Deck && e.Other != null && e.Other.Controller == x.Opp)
            {
                foreach (var c in x.My.Grave.ToList()) await x.D.ToDeck(c);
                x.D.Shuffle(x.Me);
            }
        };
        SelfBoost("Wodan the Resident of the Forest", x => 100 * x.D.FaceUpMonsters.Count(m => x.IsRace(m, "Plant")));
        {
            var s = S("The Little Swordsman of Aile");
            s.Can = x => x.MyMonsters().Any(m => m != x.Self && !x.D.Forbidden(Q.Tribute, m));
            s.Cost = async x => { var t = await x.Pick(x.MyMonsters().Where(m => m != x.Self && !x.D.Forbidden(Q.Tribute, m)), "Tribute which monster?"); if (t != null) await x.D.MoveTo(t, Loc.Grave); };
            s.Run = async x => { if (x.Self.Valid()) x.D.Boost(x.Self, 700); };
        }
        Flip("Princess of Tsurugi", async x => await x.D.Damage(x.Opp, 500 * x.Spells(x.Opp).Count(), x.Self));
        Spell("Dark-Piercing Light", async x => { foreach (var m in x.OppMonsters().FaceDown().ToList()) await x.D.Flip(m); }, x => x.OppMonsters().FaceDown().Any());

        {
            var s = S("Jirai Gumo");
            s.On = async (x, e) =>
            {
                if (e.Kind == EvKind.AttackDeclared && e.Card == x.Self && !await x.D.CallCoin(x.Me, x.Self)) x.D.Pay(x.Me, x.My.Lp - x.My.Lp / 2);
            };
        }
        SelfBoost("Shadow Ghoul", x => 100 * x.My.Grave.Count(c => c.Data.IsMonster));
        foreach (var n in new[] { "Sanga of the Thunder", "Kazejin", "Suijin" })
        {
            var s = S(n);
            s.On = async (x, e) =>
            {
                if (e.Kind == EvKind.AttackDeclared && e.Other == x.Self && x.Self.FaceUp && x.Self.Var("usedonce") == 0 && e.Card!.Valid()
                    && await x.Ask($"Use {n}: the attacker's ATK becomes 0?"))
                {
                    x.Self.Vars["usedonce"] = 1;
                    x.D.Boost(e.Card, -x.Atk(e.Card));
                }
            };
        }
        {
            var s = S("Gate Guardian");
            s.NoNormalSummon = true;
            string[] need = { "Sanga of the Thunder", "Kazejin", "Suijin" };
            s.ProcCan = x => x.Self.Loc == Loc.Hand && need.All(n => x.MyMonsters().Any(m => m.Name == n && !x.D.Forbidden(Q.Tribute, m)));
            s.Proc = async x =>
            {
                foreach (var n in need) await x.D.MoveTo(x.MyMonsters().First(m => m.Name == n), Loc.Grave);
                await x.D.SpecialSummon(x.Self, x.Me, await x.D.ChoosePosition(x.Me, x.Self));
            };
        }
        MetalForm("Metalzoa", "Zoa");
        MetalForm("Red-Eyes Black Metal Dragon", "Red-Eyes B. Dragon");

        S("Steel Scorpion").On = async (x, e) =>
        {
            if (e.Kind != EvKind.AttackDeclared || e.Other != x.Self || !e.Card!.Valid()) return;
            var a = e.Card;
            if (x.D.Race(a) == "Machine") return;
            int uid = a.Uid, due = x.D.Turn + 4;
            x.D.Later(due, Phase.End, async () => { var c = x.D.CardByUid(uid); if (c.Valid()) await x.D.Destroy(c, x.Self); });
        };

        S("Blast Juggler").On = async (x, e) =>
        {
            if (e.Kind != EvKind.Standby || e.Player != x.Me || !x.OnFieldUp()) return;
            if (!await x.Ask("Tribute Blast Juggler to destroy 2 face-up monsters with 1000 or less ATK?")) return;
            await x.D.MoveTo(x.Self, Loc.Grave);
            var l = await x.PickN(x.Targetable(x.D.FaceUpMonsters.Where(m => x.Atk(m) <= 1000)), 0, 2, "Destroy up to 2 monsters");
            await x.D.Destroy(l, x.Self);
        };
        {
            var s = S("Thunder Dragon");
            s.FromHand = true;
            s.Can = x => x.My.Deck.Any(c => c.Name == "Thunder Dragon");
            s.Cost = async x => await x.D.Discard(x.Me, new[] { x.Self }, x.Self);
            s.Run = async x =>
            {
                var l = await x.PickN(x.My.Deck.Where(c => c.Name == "Thunder Dragon"), 0, 2, "Add up to 2 Thunder Dragon");
                foreach (var c in l) await x.D.ToHand(c, x.Self);
                x.D.Shuffle(x.Me);
            };
        }
        Flip("Magician of Faith", async x => { var t = await x.Pick(x.My.Grave.Where(c => c.Data.IsSpell), "Add which Spell to your hand?"); if (t != null) await x.D.ToHand(t, x.Self); });
        {
            var s = S("Goddess of Whim");
            s.OncePerTurn = true;
            s.Run = async x => { if (!x.Self.Valid()) return; bool right = await x.D.CallCoin(x.Me, x.Self); int a = x.Atk(x.Self); x.D.Boost(x.Self, right ? a : -(a / 2)); };
        }
        static Func<Ctx, Ev, Flow> DiscardedByOpp(Func<Ctx, Flow> f) => (x, e) =>
            e.Is(EvKind.ToGrave, x.Self) && e.From == Loc.Hand && e.Other != null && e.Other.Controller == x.Opp ? f(x) : Flow.Done;
        S("Electric Snake").On = DiscardedByOpp(x => x.D.Draw(x.Me, 2));
        S("Minar").On = DiscardedByOpp(x => x.D.Damage(x.Opp, 1000, x.Self));
        S("Cockroach Knight").On = async (x, e) => { if (e.Is(EvKind.ToGrave, x.Self)) await x.D.ToDeck(x.Self); };
        S("Ameba").On = async (x, e) =>
        {
            if (e.Is(EvKind.ControlChanged, x.Self) && x.Self.Controller != x.Self.Owner && x.Self.FaceUp && x.Self.Var("done") == 0)
            { x.Self.Vars["done"] = 1; await x.D.Damage(x.Self.Controller, 2000, x.Self); }
        };
        S("Griggle").On = async (x, e) =>
        {
            if (e.Is(EvKind.ControlChanged, x.Self) && x.Self.Controller != x.Self.Owner && x.Self.FaceUp && x.Self.Var("done") == 0)
            { x.Self.Vars["done"] = 1; await x.D.Gain(x.Self.Owner, 3000, x.Self); }
        };
        SelfBoost("Maha Vailo", x => 500 * x.D.EquipsOf(x.Self).Count());
        OnSummon("Dragon Seeker", async x =>
        {
            var t = await x.Pick(x.Targetable(x.D.FaceUpMonsters.Where(m => x.IsRace(m, "Dragon"))), "Destroy which Dragon?");
            if (t != null) await x.D.Destroy(t, x.Self);
        }, normalOnly: true);
        {
            var s = S("Cannon Soldier");
            s.Can = x => x.MyMonsters().Any(m => !x.D.Forbidden(Q.Tribute, m));
            s.Cost = async x => { var t = await x.Pick(x.MyMonsters().Where(m => !x.D.Forbidden(Q.Tribute, m)), "Tribute which monster?"); if (t != null) await x.D.MoveTo(t, Loc.Grave); };
            s.Run = async x => await x.D.Damage(x.Opp, 500, x.Self);
        }
        SelfBoost("Muka Muka", x => 300 * x.My.Hand.Count, x => 300 * x.My.Hand.Count);
        {
            var s = S("Dark Elf");
            s.Forbid = (x, q, c) => q == Q.Attack && c == x.Self && x.My.Lp < 1000;
            s.On = async (x, e) => { if (e.Kind == EvKind.AttackDeclared && e.Card == x.Self) x.D.Pay(x.Me, 1000); };
        }
        S("Mushroom Man #2").On = async (x, e) =>
        {
            if (!x.OnFieldUp()) return;
            if (e.Kind == EvKind.Standby && e.Player == x.Me) await x.D.Damage(x.Me, 300, x.Self);
            else if (e.Kind == EvKind.EndPhase && e.Player == x.Me && x.My.Lp >= 500 && x.D.FreeMonsterZones(x.Opp) > 0
                && await x.Ask("Pay 500 LP to give control of Mushroom Man #2 to your opponent?"))
            {
                x.D.Pay(x.Me, 500);
                await x.D.TakeControl(x.Self, x.Opp);
            }
        };
        Flip("Needle Worm", async x => { foreach (var c in x.Their.Deck.Take(5).ToList()) await x.D.MoveTo(c, Loc.Grave, by: x.Self); });
        Flip("Weather Report", async x =>
        {
            var l = x.Spells(x.Opp).Where(c => c.FaceUp && c.Name == "Swords of Revealing Light").ToList();
            await x.D.Destroy(l, x.Self);
            if (l.Count > 0) x.D.Flags[$"twobattles{x.Me}"] = 1;
        });
        Flip("Morphing Jar", async x =>
        {
            for (int p = 0; p < 2; p++) await x.D.Discard(p, x.D.P(p).Hand.ToList(), x.Self);
            for (int p = 0; p < 2; p++) await x.D.Draw(p, Math.Min(5, x.D.P(p).Deck.Count + 0));
        });
        S("Electric Lizard").On = async (x, e) =>
        {
            if (e.Kind == EvKind.AttackDeclared && e.Other == x.Self && e.Card!.Valid() && x.D.Race(e.Card) != "Zombie") e.Card.Vars["noattack"] = x.D.Turn + 2;
        };
        Flip("Hiro's Shadow Scout", async x =>
        {
            int before = x.Their.Hand.Count;
            await x.D.Draw(x.Opp, 3);
            var drawn = x.Their.Hand.Skip(before).ToList();
            x.D.Reveal(x.Me, drawn);
            await x.D.Discard(x.Opp, drawn.Where(c => c.Data.IsSpell).ToList(), x.Self);
        });
        S("Invader of the Throne").On = async (x, e) =>
        {
            if (!e.Is(EvKind.Flipped, x.Self) || x.D.Phase == Phase.Battle) return;
            var t = await x.Pick(x.Targetable(x.OppMonsters()), "Switch control with which monster?");
            if (t == null) return;
            int me = x.Me, opp = x.Opp;
            // swap zones
            var pm = x.D.P(me); var po = x.D.P(opp);
            int za = x.Self.Zone, zb = t.Zone;
            pm.Monsters[za] = t; po.Monsters[zb] = x.Self;
            t.Zone = za; t.Controller = me; x.Self.Zone = zb; x.Self.Controller = opp;
            await x.D.Raise(new Ev { Kind = EvKind.ControlChanged, Card = t, Player = me });
            await x.D.Raise(new Ev { Kind = EvKind.ControlChanged, Card = x.Self, Player = opp });
        };
    }

    /// Special Summoned from the Deck by tributing `from` while it is equipped with `equip` (Metalzoa, Wall Shadow...).
    internal static void MetalForm(string name, string from, string equip = "Metalmorph")
    {
        var s = S(name);
        s.NoNormalSummon = true;
        s.ProcFromDeck = true;
        Card? Base(Ctx x) => x.MyMonsters().FirstOrDefault(m => m.Name == from && m.FaceUp && x.D.EquipsOf(m).Any(e => e.Name == equip));
        s.ProcCan = x => x.Self.Loc == Loc.Deck && Base(x) != null;
        s.Proc = async x =>
        {
            await x.D.MoveTo(Base(x)!, Loc.Grave);
            await x.D.SpecialSummon(x.Self, x.Me, await x.D.ChoosePosition(x.Me, x.Self));
            x.D.Shuffle(x.Me);
        };
    }

    internal static async Flow Destroy1(Ctx x, IEnumerable<Card> cands, string text, Func<Card, bool> really)
    {
        var t = await x.Pick(x.Targetable(cands.Where(c => c != x.Self)), text);
        if (t == null) return;
        if (!t.FaceUp)   // flipped face-up for both players to see before anything happens to it
        {
            t.FaceUp = true;
            x.D.Reveal(x.Me, t); x.D.Reveal(x.Opp, t);
            if (!really(t)) { t.FaceUp = false; return; }   // the wrong kind goes back face-down
        }
        if (really(t)) await x.D.Destroy(t, x.Self);
    }

    internal static bool WouldTakeBattleDamage(Duel d, int p)
    {
        var a = d.Attacker; var t = d.AttackTarget;
        if (a == null) return false;
        if (a.Controller == p) return t != null && !t.Defense && d.Atk(t) > d.BattleAtk(a) || t != null && t.Defense && d.Def(t) > d.BattleAtk(a);
        if (t == null) return d.BattleAtk(a) > 0;
        return t.Defense ? d.Allowed(Q.Piercing, a) && d.BattleAtk(a) > d.Def(t) : d.BattleAtk(a) > d.Atk(t);
    }
}
