namespace PoC.Engine;

// Layer 3, part B: hand-written effects (Kaiba pool, first half).
public static partial class Scripts
{
    static bool OppActivated(Ctx x, Func<Link, bool> f) => x.Ev is { Kind: EvKind.Activated, Link: { } l } && l.Player == x.Opp && f(l);
    static bool AnyActivated(Ctx x, Func<Link, bool> f) => x.Ev is { Kind: EvKind.Activated, Link: { } l } && f(l);
    static bool OppSummoned(Ctx x, bool special = true) => x.Ev is { Kind: EvKind.Summoned } e && e.Player == x.Opp && e.Card!.Valid() && (special || e.IsNormalOrFlip());

    static async Flow PayHalf(Ctx x) => x.D.Pay(x.Me, x.My.Lp / 2);

    static Script Continuous(string name) { var s = S(name); return s; }

    static void CardsB(CardDb db)
    {
        Spell("Eternal Rest", async x => await x.D.Destroy(x.AllMonsters().Where(m => x.D.EquipsOf(m).Any()).ToList(), x.Self),
            x => x.AllMonsters().Any(m => x.D.EquipsOf(m).Any()));
        {
            var s = S("Megamorph");
            s.EquipOk = (x, m) => true;
            s.Base = (x, c, st) =>
            {
                if (x.Self.EquippedTo != c) return;
                int mine = x.D.P(c.Controller).Lp, theirs = x.D.P(1 - c.Controller).Lp;
                if (mine < theirs) st.BaseAtk *= 2; else if (mine > theirs) st.BaseAtk /= 2;
            };
        }
        {
            var s = Spell("Metalmorph", async x =>
            {
                if (x.T0 is { } t && t.Valid() && t.FaceUp) { x.Self.Vars["stay"] = 1; await x.D.Equip(x.Self, t); }
            }, x => x.Targetable(x.D.FaceUpMonsters).Any(), async x => await x.Target(x.D.FaceUpMonsters, "Equip Metalmorph to which monster?"));
            s.Stats = (x, c, st) =>
            {
                if (x.Self.EquippedTo != c) return;
                st.Add(300, 300);
                if (x.D.Attacker == c && x.D.AttackTarget is { } t && t.FaceUp) st.Add(x.Atk(t) / 2, 0);
            };
        }
        {
            var s = Spell("Crush Card", async x =>
            {
                var opp = x.Their;
                await x.D.Destroy(x.OppMonsters().Where(m => (m.FaceUp ? x.Atk(m) : m.Data.Atk) >= 1500).ToList(), x.Self);
                foreach (var c in opp.Hand.Where(c => c.Data.IsMonster && c.Data.Atk >= 1500).ToList()) await x.D.MoveTo(c, Loc.Grave, by: x.Self);
                x.D.Flags[$"crush{x.Self.Uid}"] = x.D.Turn + 6;
                x.D.Flags[$"crushp{x.Self.Uid}"] = x.Opp;
            }, x => x.MyMonsters().Any(m => m.FaceUp && x.IsAttr(m, "DARK") && x.Atk(m) <= 1000),
            async x =>
            {
                var t = await x.Pick(x.MyMonsters().Where(m => m.FaceUp && x.IsAttr(m, "DARK") && x.Atk(m) <= 1000), "Tribute a DARK monster with 1000 or less ATK");
                if (t != null) await x.D.MoveTo(t, Loc.Grave);
            });
            s.On = async (x, e) =>
            {
                if (e.Kind != EvKind.Drew || x.D.Flags.GetValueOrDefault($"crush{x.Self.Uid}") < x.D.Turn) return;
                if (e.Player != x.D.Flags.GetValueOrDefault($"crushp{x.Self.Uid}")) return;
                foreach (var c in e.Cards!.Where(c => c.Loc == Loc.Hand && c.Data.IsMonster && c.Data.Atk >= 1500).ToList()) await x.D.MoveTo(c, Loc.Grave, by: x.Self);
            };
        }
        Spell("Harpie's Feather Duster", async x => await x.D.Destroy(x.Spells(x.Opp).ToList(), x.Self), x => x.Spells(x.Opp).Any());
        Spell("Acid Trap Hole", async x =>
        {
            if (x.T0 is not { } t || !t.Valid() || t.FaceUp) return;
            await x.D.Flip(t, triggerFlip: false);
            if (x.Def(t) <= 2000) await x.D.Destroy(t, x.Self); else await x.D.SetFaceDown(t);
        }, x => x.Targetable(x.AllMonsters().FaceDown()).Any(), async x => await x.Target(x.AllMonsters().FaceDown(), "Flip which face-down monster?"));
        {
            var s = Continuous("Goblin Fan");
            bool Hit(Ctx x, Card? c) => c != null && c.Valid() && c.Summoned == SummonKind.Flip && c.SummonTurn == x.D.Turn && c.Data.Level <= 2;
            s.Forbid = (x, q, c) => q == Q.Negate && Hit(x, c);
            s.On = async (x, e) => { if (e.Kind == EvKind.Summoned && e.Summoned(SummonKind.Flip) && Hit(x, e.Card)) await x.D.Destroy(e.Card!, x.Self); };
        }
        {
            string[] destroyers = { "Harpie's Feather Duster", "Heavy Storm", "Mystical Space Typhoon", "Dust Tornado", "Remove Trap", "Nobleman of Extermination", "Final Destiny", "Breaker the Magical Warrior" };
            Spell("Fake Trap", async x => x.D.TurnFlags.Add($"faketrap{x.Me}"),
                x => OppActivated(x, l => destroyers.Contains(l.Card.Name)) && x.Spells(x.Me).Any(c => c != x.Self && c.Data.IsTrap));
        }
        S("Jigen Bakudan").On = async (x, e) =>
        {
            if (e.Kind != EvKind.Standby || e.Player != x.Me || !x.OnFieldUp() || x.Self.Var("flipped") == 0) return;
            if (!await x.Ask("Tribute Jigen Bakudan: destroy your monsters and deal half their ATK?")) return;
            var mine = x.MyMonsters().ToList();
            int total = mine.Where(m => m.FaceUp && (x.Classic || m != x.Self)).Sum(m => x.Atk(m));
            await x.D.MoveTo(x.Self, Loc.Grave);
            await x.D.Destroy(mine.Where(m => m != x.Self).ToList(), x.Self);
            await x.D.Damage(x.Opp, total / 2, x.Self);
        };
        {
            var s = S("Barrel Dragon");
            s.OncePerTurn = true;
            s.Can = x => x.Targetable(x.OppMonsters()).Any();
            s.Cost = async x => await x.Target(x.OppMonsters(), "Target which monster?");
            s.Run = async x =>
            {
                int heads = x.D.TossCoins(x.Me, 3, x.Self);
                if (heads >= 2 && x.T0 is { } t && t.Valid()) await x.D.Destroy(t, x.Self);
            };
        }
        static Script RingOfDestruction(string name, bool skipDraw) => Spell(name, async x =>
        {
            if (x.T0 is not { } t || !t.Valid() || !t.FaceUp) return;
            int atk = x.Atk(t);
            await x.D.Destroy(t, x.Self);
            if (name == "Ring of Destruction") { await x.D.Damage(x.Me, atk, x.Self); await x.D.Damage(x.Opp, atk, x.Self); }
            if (skipDraw) x.D.Flags[$"skipdraw{x.Me}"] = 1;
        }, x => x.Targetable(x.D.FaceUpMonsters).Any(), async x => await x.Target(x.D.FaceUpMonsters, "Destroy which face-up monster?"));
        RingOfDestruction("Ring of Destruction", false);
        RingOfDestruction("Offerings to the Doomed", true);

        Spell("Negate Attack", async x => { x.D.AttackNegated = true; x.D.TurnFlags.Add("endbattle"); }, AttackOnMe);
        Spell("Mesmeric Control", async x => { int opp = x.Opp; x.D.Linger(x.D.Turn + 1, (q, c, p) => q == Q.ChangePos && c != null && c.Controller == opp); });
        Spell("Gravedigger Ghoul", async x =>
        {
            foreach (var c in await x.PickN(x.Their.Grave.Where(c => c.Data.IsMonster), 1, 2, "Banish up to 2 monsters")) await x.D.Banish(c, x.Self);
        }, x => x.Their.Grave.Any(c => c.Data.IsMonster) && !(x.Classic && x.D.CountActive("Kycoo the Ghost Destroyer", x.Opp) > 0) && x.D.CountActive("Kycoo the Ghost Destroyer", x.Opp) == 0);
        Spell("Tribute to The Doomed", async x => { if (x.T0 is { } t && t.Valid()) await x.D.Destroy(t, x.Self); },
            x => x.My.Hand.Count(c => c != x.Self) > 0 && x.Targetable(x.AllMonsters()).Any(),
            async x => { await x.D.Discard(x.Me, await x.PickN(x.My.Hand.Where(c => c != x.Self), 1, 1, "Discard 1 card (cost)"), x.Self); await x.Target(x.AllMonsters(), "Destroy which monster?"); });
        Spell("The Cheerful Coffin", async x => await x.D.Discard(x.Me, await x.PickN(x.My.Hand.Where(c => c.Data.IsMonster), 1, 3, "Discard up to 3 monsters"), x.Self),
            x => x.My.Hand.Any(c => c.Data.IsMonster));

        Spell("Solemn Judgment", async x =>
        {
            if (x.Ev!.Kind == EvKind.Activated) await x.D.Negate(x.Ev.Link!);
            else if (x.Ev.Card!.Valid()) await x.D.Destroy(x.Ev.Card, x.Self);
        }, x => OppActivated(x, l => l.IsCardActivation) || OppSummoned(x), PayHalf);
        Spell("Seven Tools of the Bandit", async x => await x.D.Negate(x.Ev!.Link!),
            x => AnyActivated(x, l => l.Card.Data.IsTrap && l.IsCardActivation && l.Card != x.Self) && x.My.Lp > 1000, async x => x.D.Pay(x.Me, 1000));
        Spell("Horn of Heaven", async x => { if (x.Ev!.Card!.Valid()) await x.D.Destroy(x.Ev.Card, x.Self); },
            x => OppSummoned(x) && x.MyMonsters().Any(m => !x.D.Forbidden(Q.Tribute, m)),
            async x => { var t = await x.Pick(x.MyMonsters().Where(m => !x.D.Forbidden(Q.Tribute, m)), "Tribute 1 monster (cost)"); if (t != null) await x.D.MoveTo(t, Loc.Grave); });
        Continuous("Royal Decree").Forbid = (x, q, c) => q == Q.Negate && c != null && c != x.Self && c.Data.IsTrap;
        Spell("Exile of the Wicked", async x => await x.D.Destroy(x.D.FaceUpMonsters.Where(m => x.IsRace(m, "Fiend")).ToList(), x.Self));
        S("Sword of Deep-Seated").On = async (x, e) => { if (e.Is(EvKind.ToGrave, x.Self) && e.From == Loc.Spell) await x.D.ToDeck(x.Self); };
        Spell("Block Attack", async x => { if (x.T0 is { } t && t.Valid() && t.Attack) await x.D.ChangePosition(t, true); },
            x => x.Targetable(x.OppMonsters().Where(m => m.Attack && m.FaceUp)).Any(),
            async x => await x.Target(x.OppMonsters().Where(m => m.Attack && m.FaceUp), "Change which monster to Defense?"));
        OnBattleDeath("The Unhappy Maiden", async x => x.D.TurnFlags.Add("endbattle"));
        Continuous("Robbin' Goblin").On = async (x, e) =>
        {
            if (e.Kind == EvKind.BattleDamage && e.Player == x.Opp && e.Card!.Controller == x.Me && x.Self.FaceUp) await x.D.DiscardRandom(x.Opp, 1, x.Self);
        };
        {
            var s = S("Germ Infection");
            s.EquipOk = (x, m) => x.D.Race(m) != "Machine";
            s.Stats = (x, c, st) => { if (x.Self.EquippedTo == c) st.Add(-300 * x.Self.Var("steps"), 0); };
            s.On = async (x, e) => { if (e.Kind == EvKind.Standby && x.Self.EquippedTo is { } m && e.Player == m.Controller) x.Self.Inc("steps"); };
        }
        {
            var s = S("Paralyzing Potion");
            s.EquipOk = (x, m) => x.D.Race(m) != "Machine";
            s.Forbid = (x, q, c) => q == Q.Attack && c == x.Self.EquippedTo;
        }
        {
            var s = S("Ring of Magnetism");
            s.EquipOk = (x, m) => m.Controller == x.Me;
            s.Allow = (x, q, c) => q == Q.MustBeAttacked && c == x.Self.EquippedTo;
        }
        Spell("Share the Pain", async x =>
        {
            var t = await x.D.PickOne(x.Opp, x.OppMonsters().Where(m => !x.D.Forbidden(Q.Tribute, m)), "Tribute 1 monster", x.Self);
            if (t != null) await x.D.MoveTo(t, Loc.Grave);
        }, x => x.MyMonsters().Any() && x.OppMonsters().Any(),
        async x => { var t = await x.Pick(x.MyMonsters().Where(m => !x.D.Forbidden(Q.Tribute, m)), "Tribute 1 monster (cost)"); if (t != null) await x.D.MoveTo(t, Loc.Grave); });
        {
            var s = S("Stim-Pack");
            s.Stats = (x, c, st) => { if (x.Self.EquippedTo == c) st.Add(700 - 200 * x.Self.Var("steps"), 0); };
            s.On = async (x, e) => { if (e.Kind == EvKind.Standby && x.Self.EquippedTo is { } m && e.Player == m.Controller) x.Self.Inc("steps"); };
        }
        Spell("Heavy Storm", async x =>
        {
            await x.D.Destroy(x.Spells(x.Opp).ToList(), x.Self);
            await x.D.Destroy(x.Spells(x.Me).Where(c => c != x.Self).ToList(), x.Self);
        }, x => x.AllSpells().Any(c => c != x.Self));
        Continuous("Gravekeeper's Servant").On = async (x, e) =>
        {
            if (e.Kind == EvKind.AttackDeclared && e.Player == x.Opp && x.Their.Deck.Count > 0) await x.D.MoveTo(x.Their.Deck[0], Loc.Grave, by: x.Self);
        };
        {
            var s = Spell("Curse of Fiend", async x =>
            {
                foreach (var m in x.AllMonsters().ToList())
                {
                    if (!m.FaceUp) await x.D.ChangePosition(m, false, true);
                    else await x.D.ChangePosition(m, !m.Defense);
                }
                x.D.Linger(x.D.Turn, (q, c, p) => q == Q.ChangePos);
            }, x => x.D.Phase == Phase.Standby && x.Turn() && x.AllMonsters().Any());
            s.Speed = 2;
        }
        Spell("Upstart Goblin", async x => { await x.D.Draw(x.Me, 1); await x.D.Gain(x.Opp, 1000, x.Self); });
        {
            var s = Continuous("Toll");
            s.Forbid = (x, q, c) => q == Q.Attack && c != null && x.D.P(c.Controller).Lp <= 500;
            s.On = async (x, e) => { if (e.Kind == EvKind.AttackDeclared) x.D.Pay(e.Player, 500); };
        }
        Spell("Final Destiny", async x => await x.D.Destroy(x.D.FieldCards.Where(c => c != x.Self).ToList(), x.Self),
            x => x.My.Hand.Count(c => c != x.Self) >= 5 && x.D.FieldCards.Any(c => c != x.Self),
            async x => await x.D.Discard(x.Me, await x.PickN(x.My.Hand.Where(c => c != x.Self), 5, 5, "Discard 5 cards (cost)"), x.Self));
        {
            var s = S("Snatch Steal");
            s.EquipOk = (x, m) => m.Controller == x.Opp;
            s.Can = x => x.D.FreeMonsterZones(x.Me) > 0;
            s.Run = async x =>
            {
                if (x.T0 is not { } t || !t.Valid() || !t.FaceUp || t.Controller != x.Opp) return;
                await x.D.Equip(x.Self, t);
                x.D.Flags[$"snatch{x.Self.Uid}"] = t.Uid + 1;
                await x.D.TakeControl(t, x.Me);
            };
            s.On = async (x, e) =>
            {
                if (e.Kind == EvKind.Standby && e.Player == x.Opp && x.Self.OnField && x.Self.EquippedTo != null) await x.D.Gain(x.Opp, 1000, x.Self);
                if (e.Is(EvKind.LeftField, x.Self) && x.D.Flags.GetValueOrDefault($"snatch{x.Self.Uid}") is > 0 and var u)
                {
                    x.D.Flags[$"snatch{x.Self.Uid}"] = 0;
                    var m = x.D.CardByUid(u - 1);
                    if (m.Valid() && m.Controller != m.Owner) await x.D.TakeControl(m, m.Owner);
                }
            };
        }
        Spell("Confiscation", async x =>
        {
            x.D.Reveal(x.Me, x.Their.Hand);
            var t = await x.Pick(x.Their.Hand, "Discard which card?");
            if (t != null) await x.D.Discard(x.Opp, new[] { t }, x.Self);
        }, x => x.My.Lp > 1000 && x.Their.Hand.Count > 0, async x => x.D.Pay(x.Me, 1000));
        Spell("Delinquent Duo", async x =>
        {
            await x.D.DiscardRandom(x.Opp, 1, x.Self);
            var t = await x.D.PickOne(x.Opp, x.Their.Hand, "Discard 1 card", x.Self);
            if (t != null) await x.D.Discard(x.Opp, new[] { t }, x.Self);
        }, x => x.My.Lp > 1000 && x.Their.Hand.Count > 0, async x => x.D.Pay(x.Me, 1000));
        Spell("Darkness Approaches", async x => { if (x.T0 is { } t && t.Valid() && t.FaceUp) { bool def = t.Defense; await x.D.SetFaceDown(t); t.Defense = def; } },
            x => x.My.Hand.Count(c => c != x.Self) >= 2 && DarknessTargets(x).Any(),
            async x =>
            {
                await x.D.Discard(x.Me, await x.PickN(x.My.Hand.Where(c => c != x.Self), 2, 2, "Discard 2 cards (cost)"), x.Self);
                await x.Target(DarknessTargets(x), "Flip which monster face-down?");
            });
        Spell("Fairy's Hand Mirror", async x =>
        {
            var l = x.Ev!.Link!;
            if (l.X.T0 is not { } old) return;
            var n = await x.Pick(x.Targetable(x.AllMonsters().Where(m => m != old && m.FaceUp == old.FaceUp)), "Redirect the Spell to which monster?");
            if (n != null) l.X.T[0] = n;
        }, x => OppActivated(x, l => l.Card.Data.IsSpell && l.X.T.Count == 1 && l.X.T0!.Loc == Loc.Monster) && x.AllMonsters().Count() > 1);
        Spell("Tailor of the Fickle", async x =>
        {
            var eq = await x.Pick(x.AllSpells().Where(c => c.EquippedTo != null), "Move which Equip card?");
            if (eq == null) return;
            var n = await x.Pick(x.D.EquipTargets(eq).Where(m => m != eq.EquippedTo), "Equip it to which monster?");
            if (n != null) { eq.EquippedTo = n; await x.D.Raise(new Ev { Kind = EvKind.Equipped, Card = eq, Other = n, Player = eq.Controller }); }
        }, x => x.AllSpells().Any(c => c.EquippedTo != null && x.D.EquipTargets(c).Any(m => m != c.EquippedTo)));
        Spell("The Forceful Sentry", async x =>
        {
            x.D.Reveal(x.Me, x.Their.Hand);
            var t = await x.Pick(x.Their.Hand, "Return which card to the Deck?");
            if (t != null) await x.D.ToDeck(t, shuffle: true);
        }, x => x.Their.Hand.Count > 0);
        S("Chain Energy").Marker = true; // Duel.CountActive("Chain Energy") charges every play/set
        Spell("Mystical Space Typhoon", async x =>
        {
            if (x.T0 is not { OnField: true } t) return;
            await x.D.Destroy(t, x.Self);
        }, x => x.Targetable(x.AllSpells().Where(c => c != x.Self)).Any(), async x => await x.Target(x.AllSpells().Where(c => c != x.Self), "Destroy which Spell/Trap?"));
        Spell("Giant Trunade", async x =>
        {
            foreach (var p in new[] { x.Opp, x.Me }) foreach (var c in x.Spells(p).Where(c => c != x.Self).ToList()) await x.D.ToHand(c, x.Self);
        }, x => x.AllSpells().Any(c => c != x.Self));
        Spell("Painful Choice", async x =>
        {
            var five = await x.PickN(x.My.Deck, 5, 5, "Pick 5 cards from your Deck");
            x.D.Reveal(x.Opp, five);
            var keep = await x.D.PickOne(x.Opp, five, "Pick 1 card for your opponent's hand", x.Self);
            foreach (var c in five) if (c == keep) await x.D.ToHand(c, x.Self); else await x.D.MoveTo(c, Loc.Grave, by: x.Self);
            x.D.Shuffle(x.Me);
        }, x => x.My.Deck.Count >= 5);
        Flip("Cyber Jar", async x =>
        {
            await x.D.Destroy(x.AllMonsters().ToList(), x.Self);
            foreach (var p in new[] { x.Me, x.Opp })
            {
                var pl = x.D.P(p);
                var top = pl.Deck.Take(5).ToList();
                x.D.Reveal(0, top); x.D.Reveal(1, top);
                foreach (var c in top)
                {
                    if (c.Data.IsMonster && c.Data.Level <= 4 && !c.Data.IsFusion && !c.Data.IsRitual && x.D.FreeMonsterZones(p) > 0)
                    {
                        bool set = await x.D.ChooseText(p, $"Cyber Jar: summon {c.Name} how?", new[] { "Attack Position", "Face-down Defense" }, x.Self) == 1;
                        await x.D.SpecialSummon(c, p, set, faceUp: !set, by: x.Self);
                    }
                    else await x.D.ToHand(c, x.Self);
                }
            }
        });
        OnBattleDeath("Giant Germ", async x =>
        {
            await x.D.Damage(x.Opp, 500, x.Self);
            var l = x.My.Deck.Where(c => c.Name == "Giant Germ").ToList();
            if (l.Count > 0 && await x.Ask("Special Summon Giant Germ copies from your Deck?"))
                foreach (var c in l) await x.D.SpecialSummon(c, x.Me, false, by: x.Self);
            x.D.Shuffle(x.Me);
        });
        OnBattleDeath("Nimble Momonga", async x =>
        {
            await x.D.Gain(x.Me, 1000, x.Self);
            var l = x.My.Deck.Where(c => c.Name == "Nimble Momonga").ToList();
            if (l.Count > 0 && await x.Ask("Special Summon Nimble Momonga copies face-down from your Deck?"))
                foreach (var c in l) await x.D.SpecialSummon(c, x.Me, true, faceUp: false, by: x.Self);
            x.D.Shuffle(x.Me);
        });
        OnSummon("Senju of the Thousand Hands", x => x.Search(c => c.Data.IsRitual, "Add a Ritual Monster to your hand?", optional: true), normalOnly: true, includeFlip: true);
        OnSummon("Sonic Bird", x => x.Search(c => c.Data.IsSpell && c.Data.Is("Ritual"), "Add a Ritual Spell to your hand?", optional: true), normalOnly: true, includeFlip: true);
        {
            var s = Continuous("Imperial Order");
            s.Forbid = (x, q, c) => q == Q.Negate && c != null && c.Data.IsSpell && c.Loc != Loc.Monster;
            s.On = async (x, e) =>
            {
                if (e.Kind == EvKind.Standby && e.Player == x.Me && x.Self.OnField)
                {
                    if (x.My.Lp > 700 && await x.Ask("Pay 700 LP to keep Imperial Order?")) x.D.Pay(x.Me, 700);
                    else await x.D.Destroy(x.Self);
                }
            };
        }
        Spell("Nobleman of Crossout", async x =>
        {
            if (x.T0 is not { } t || !t.Valid()) return;
            await x.D.Destroy(t, x.Self);
            await x.D.Banish(t, x.Self);
            x.D.Reveal(0, t); x.D.Reveal(1, t);
            if (db.Effects.TryGetValue(t.Data.KonamiId, out var ef) && ef.Trig.StartsWith("flip"))
            {
                for (int p = 0; p < 2; p++) { foreach (var c in x.D.P(p).Deck.Where(c => c.Name == t.Name).ToList()) await x.D.Banish(c, x.Self); x.D.Shuffle(p); }
            }
        }, x => x.Targetable(x.AllMonsters().FaceDown()).Any(), async x => await x.Target(x.AllMonsters().FaceDown(), "Destroy which face-down monster?"));
        Spell("Nobleman of Extermination", async x =>
        {
            if (x.T0 is not { OnField: true } t) return;
            await x.D.Destroy(t, x.Self);
            await x.D.Banish(t, x.Self);
            if (t.Data.IsTrap) for (int p = 0; p < 2; p++) { foreach (var c in x.D.P(p).Deck.Where(c => c.Name == t.Name).ToList()) await x.D.Banish(c, x.Self); x.D.Shuffle(p); }
        }, x => x.Targetable(x.AllSpells().Where(c => !c.FaceUp)).Any(), async x => await x.Target(x.AllSpells().Where(c => !c.FaceUp), "Destroy which face-down card?"));
        Spell("The Shallow Grave", async x =>
        {
            foreach (var p in new[] { x.Opp, x.Me })
            {
                var t = await x.D.PickOne(p, x.D.P(p).Grave.Where(c => c.Data.IsMonster), "Special Summon 1 monster face-down", x.Self);
                if (t != null) await x.D.SpecialSummon(t, p, true, faceUp: false, by: x.Self);
            }
        }, x => x.My.Grave.Any(c => c.Data.IsMonster) && (!x.Classic || x.Their.Grave.Any(c => c.Data.IsMonster)) && x.D.FreeMonsterZones(x.Me) > 0);
        static Script Burial(string name, bool payCost)
        {
            var s = Spell(name, async x =>
            {
                var t = await x.Pick(x.My.Grave.Where(c => c.Data.IsMonster), "Special Summon which monster?");
                if (t == null || !await x.D.SpecialSummon(t, x.Me, false, by: x.Self)) return;
                x.Self.Vars["stay"] = 1;
                x.D.Flags[$"burial{x.Self.Uid}"] = t.Uid + 1;
                if (name == "Premature Burial") await x.D.Equip(x.Self, t);
            }, x => (!payCost || x.My.Lp > 800) && x.D.FreeMonsterZones(x.Me) > 0 && x.My.Grave.Any(c => c.Data.IsMonster),
            payCost ? async x => x.D.Pay(x.Me, 800) : null);
            s.On = async (x, e) =>
            {
                int u = x.D.Flags.GetValueOrDefault($"burial{x.Self.Uid}") - 1;
                if (u < 0) return;
                var m = x.D.CardByUid(u);
                if (e.Is(EvKind.LeftField, x.Self))
                {
                    x.D.Flags[$"burial{x.Self.Uid}"] = 0;
                    if (m.Valid()) await x.D.Destroy(m, x.Self);
                }
                else if (e.Is(EvKind.LeftField, m) && x.Self.OnField)
                {
                    x.D.Flags[$"burial{x.Self.Uid}"] = 0;
                    await x.D.Destroy(x.Self);
                }
            };
            return s;
        }
        Burial("Premature Burial", true);
        Burial("Call Of The Haunted", false);
        Spell("Windstorm of Etaqua", async x =>
        {
            bool jar = x.D.CountActive("Dragon Capture Jar") > 0;
            foreach (var m in x.OppMonsters().FaceUp().ToList())
            {
                if (x.Classic && jar && m.Defense && x.IsRace(m, "Dragon")) continue;
                await x.D.ChangePosition(m, !m.Defense);
            }
        }, x => x.OppMonsters().FaceUp().Any());
        Continuous("Anti-Spell Fragrance").Forbid = (x, q, c) =>
            q == Q.Activate && c != null && c.Data.IsSpell && (c.Loc == Loc.Hand || (c.Loc is Loc.Spell or Loc.Field && !c.FaceUp && x.D.Turn - c.SetTurn < 2));
        Spell("Exchange", async x =>
        {
            x.D.Reveal(x.Me, x.Their.Hand); x.D.Reveal(x.Opp, x.My.Hand.Where(c => c != x.Self));
            var mine = await x.Pick(x.Their.Hand, "Take which card from your opponent's hand?");
            var theirs = await x.D.PickOne(x.Opp, x.My.Hand.Where(c => c != x.Self), "Take which card?", x.Self);
            if (mine != null) MoveHand(x.D, mine, x.Me);
            if (theirs != null) MoveHand(x.D, theirs, x.Opp);
        }, x => x.Their.Hand.Count > 0 && x.My.Hand.Count(c => c != x.Self) > 0);
        Spell("Thousand Knives", async x => { if (x.T0 is { } t && t.Valid()) await x.D.Destroy(t, x.Self); },
            x => x.MyMonsters().FaceUp().Any(m => m.Name == "Dark Magician") && x.Targetable(x.OppMonsters()).Any(),
            async x => await x.Target(x.OppMonsters(), "Destroy which monster?"));
        Spell("Soul Exchange", async x => { x.D.TurnFlags.Add($"soulexchange{x.Me}"); x.D.TurnFlags.Add("nobattle"); },
            x => x.OppMonsters().Any() && (!x.Classic || x.D.Phase == Phase.Main1)
                && (!x.Classic || x.My.Hand.Any(c => c.Data.IsMonster && c.Data.Level >= 5) || x.MyMonsters().Any(m => m.Name is "Catapult Turtle" or "The Little Swordsman of Aile" or "Cannon Soldier"))
                && x.D.CountActive("Mask of Restrict") == 0);
        {
            var s = S("Ancient Lamp");
            s.AlwaysActive = true;   // its redirect works while it is face-down (Duel.RaiseCore skips face-down cards otherwise)
            s.On = async (x, e) =>
            {
                if (e.Kind != EvKind.AttackDeclared || e.Other != x.Self || x.Self.FaceUp || !e.Card!.Valid()) return;
                var others = x.D.P(e.Card.Controller).MonstersOnField.Where(m => m != e.Card).ToList();
                if (others.Count == 0) return;
                var t = await x.Pick(others, "Ancient Lamp: redirect the attack to which monster?", optional: true);
                if (t == null) return;
                await x.D.Flip(x.Self, triggerFlip: false);   // using it reveals the Lamp
                x.D.AttackTarget = t; x.D.BattleFlags.Add("redirectself");
            };
            s.Can = x => x.My.Hand.Any(c => c.Name == "La Jinn the Mystical Genie of the Lamp") && x.D.FreeMonsterZones(x.Me) > 0;
            s.Run = async x =>
            {
                var t = await x.Pick(x.My.Hand.Where(c => c.Name == "La Jinn the Mystical Genie of the Lamp"), "Special Summon La Jinn");
                if (t != null) await x.D.SpecialSummon(t, x.Me, await x.D.ChoosePosition(x.Me, t), by: x.Self);
            };
        }
        Spell("Monster Recovery", async x =>
        {
            if (x.T0 is not { } t || !t.Valid()) return;
            int n = x.My.Hand.Count;
            await x.D.ToDeck(t);
            foreach (var c in x.My.Hand.ToList()) await x.D.ToDeck(c);
            x.D.Shuffle(x.Me);
            await x.D.Draw(x.Me, n);
        }, x => x.MyMonsters().Any(), async x => await x.Target(x.MyMonsters(), "Return which of your monsters?"));
        {
            var s = OnSummon("Invitation to a Dark Sleep", async x =>
            {
                var t = await x.Pick(x.Targetable(x.OppMonsters()), "Which monster cannot attack?");
                if (t != null) x.Self.Vars["target"] = t.Uid + 1;
            }, normalOnly: true);
            s.Forbid = (x, q, c) => q == Q.Attack && c != null && c.Uid + 1 == x.Self.Var("target");
        }
        S("The Fiend Megacyber").TributesNeeded = x => x.OppMonsters().Count() >= x.MyMonsters().Count() + 2 ? 0 : 2;
        {
            string[] letters = { "Spirit Message \"I\"", "Spirit Message \"N\"", "Spirit Message \"A\"", "Spirit Message \"L\"" };
            var s = Spell("Destiny Board", async x => { x.Self.Vars["stay"] = 1; });
            s.Run = null; // continuous trap: stays by type
            s.On = async (x, e) =>
            {
                if (!x.Self.OnField || !x.Self.FaceUp) return;
                if (e.Kind == EvKind.EndPhase && e.Player == x.Opp)
                {
                    var next = letters.FirstOrDefault(l => !Placed(x).Any(c => c.Name == l));
                    // Dark Sanctuary (official card): the Spirit Message may be Special Summoned as a monster instead.
                    bool sanctuary = x.D.CountActive("Dark Sanctuary", x.Me) > 0 && x.D.FreeMonsterZones(x.Me) > 0;
                    if (next == null || (x.My.FreeSpellZones == 0 && !sanctuary)) return;
                    var c = x.My.Hand.Concat(x.My.Deck).FirstOrDefault(c => c.Name == next);
                    if (c == null) return;
                    bool deck = c.Loc == Loc.Deck;
                    if (sanctuary && (x.My.FreeSpellZones == 0 || await x.Ask($"Special Summon {c.Name} as a monster (Dark Sanctuary)?")))
                    {
                        var mon = new CardData { Name = c.Name, Idx = c.Data.Idx, KonamiId = c.Data.KonamiId, Kind = Kind.Monster, Frame = "Normal", Attribute = "DARK", Race = "Fiend", Level = 1, Desc = c.Data.Desc };
                        await x.D.SpecialSummon(c, x.Me, false, by: x.Self, asMonster: mon);
                        c.Vars["sanctuary"] = 1;
                    }
                    else
                    {
                        x.D.Place(c, x.Me, Loc.Spell);
                        c.FaceUp = true;
                        await x.D.Raise(new Ev { Kind = EvKind.Set, Card = c, Player = x.Me });
                    }
                    if (deck) x.D.Shuffle(x.Me);
                    if (letters.All(l => Placed(x).Any(m => m.Name == l))) x.D.Win(x.Me, "Destiny Board");
                }
                else if (e.Kind == EvKind.LeftField && (e.Card == x.Self || letters.Contains(e.Card!.Name)) && e.Card!.Owner == x.Me)
                {
                    foreach (var c in x.Spells(x.Me).Concat(x.MyMonsters()).Where(c => c.Name == "Destiny Board" || letters.Contains(c.Name)).ToList()) await x.D.ToGrave(c);
                }
            };
            static IEnumerable<Card> Placed(Ctx x) => x.Spells(x.Me).Concat(x.MyMonsters()).Where(c => c.FaceUp);
            foreach (var l in letters)
            {
                var m = S(l); m.Can = x => false; m.Marker = true;
                // As a Dark Sanctuary monster: unaffected by card effects except Destiny Board, cannot be targeted for attacks.
                m.Forbid = (x, q, c) => c == x.Self && x.Self.Var("sanctuary") == 1 && x.Self.Loc == Loc.Monster
                    && (q is Q.BeAttacked or Q.Target or Q.AffectedByTrap or Q.AffectedBySpell || (q == Q.DestroyByEffect && x.D.Destroyer?.Name != "Destiny Board"));
            }
        }
        S("The Wicked Worm Beast").On = async (x, e) => { if (e.Kind == EvKind.EndPhase && e.Player == x.Me && x.OnFieldUp()) await x.D.ToHand(x.Self); };
        S("Mysterious Puppeteer").On = async (x, e) => { if (x.OnFieldUp() && e.Kind == EvKind.Summoned && e.Card != x.Self && e.IsNormalOrFlip())   /* "each additional monster": not itself */ await x.D.Gain(x.Me, 500, x.Self); };
        S("Dimensional Warrior").On = async (x, e) =>
        {
            if (e.Kind != EvKind.AfterDamage || (e.Card != x.Self && e.Other != x.Self) || e.Other == null) return;
            foreach (var c in new[] { e.Card!, e.Other }) if (c.Valid()) await x.D.Banish(c, x.Self);
        };
        MetalForm("Wall Shadow", "Labyrinth Wall", "Magical Labyrinth");
        S("Magical Labyrinth").EquipOk = (x, m) => m.Name == "Labyrinth Wall";
        SelfBoost("Harpie's Pet Dragon", x => 300 * x.D.FaceUpMonsters.Count(m => m.Name == "Harpie Lady"), x => 300 * x.D.FaceUpMonsters.Count(m => m.Name == "Harpie Lady"));
        SelfBoost("Machine King", x => 100 * x.D.FaceUpMonsters.Count(m => x.IsRace(m, "Machine")));
        {
            var s = S("Cyber-Stein");
            s.Can = x => x.My.Lp > 5000 && x.My.Extra.Any(c => c.Data.IsFusion) && x.D.FreeMonsterZones(x.Me) > 0;
            s.Cost = async x => x.D.Pay(x.Me, 5000);
            s.Run = async x => { var t = await x.Pick(x.My.Extra.Where(c => c.Data.IsFusion), "Special Summon which Fusion Monster?"); if (t != null) await x.D.SpecialSummon(t, x.Me, false, by: x.Self); };
        }
        S("The Immortal of Thunder").On = async (x, e) =>
        {
            if (e.Is(EvKind.Flipped, x.Self)) { x.D.Flags[$"immortal{x.Self.Uid}"] = 1; await x.D.Gain(x.Me, 3000, x.Self); }
            else if (e.Is(EvKind.ToGrave, x.Self) && e.From == Loc.Monster && x.D.Flags.GetValueOrDefault($"immortal{x.Self.Uid}") == 1)
            { x.D.Flags[$"immortal{x.Self.Uid}"] = 0; x.D.Pay(x.Self.Owner, 5000); }
        };
        MyStandby("Sinister Serpent", async x => { if (await x.Ask("Return Sinister Serpent to your hand?")) await x.D.ToHand(x.Self); }, x => x.InGrave());
        S("Insect Soldiers of the Sky").Stats = (x, c, st) =>
        {
            if (c == x.Self && x.D.Attacker == c && x.D.AttackTarget is { FaceUp: true } t && x.D.Attr(t) == "WIND") st.Add(1000, 0);
        };
        MyStandby("Patrol Robo", async x =>
        {
            var t = await x.Pick(x.Their.FieldCards.Where(c => !c.FaceUp), "Look at which face-down card?", optional: true);
            if (t != null) x.D.Reveal(x.Me, t);
        });
        Flip("Greenkappa", async x => await x.D.Destroy(await x.PickN(x.Targetable(x.AllSpells().Where(c => !c.FaceUp)), 0, 2, "Destroy up to 2 face-down Spell/Traps"), x.Self));
        Flip("Penguin Soldier", async x => { foreach (var c in await x.PickN(x.Targetable(x.AllMonsters().Where(m => m != x.Self)), 0, 2, "Return up to 2 monsters to the hand")) await x.D.ToHand(c, x.Self); });
        {
            var s = Spell("Kunai with Chain", async x =>
            {
                var a = x.Ev!.Card!;
                int choice = await x.D.ChooseText(x.Me, "Kunai with Chain", new[] { "Change the attacker to Defense Position", "Equip to one of your monsters (+500 ATK)" }, x.Self);
                if (choice == 0) { if (a.Valid()) await x.D.ChangePosition(a, true); return; }
                var t = await x.Pick(x.MyMonsters().FaceUp(), "Equip to which monster?");
                if (t != null) { x.Self.Vars["stay"] = 1; await x.D.Equip(x.Self, t); }
            }, AttackOnMe);
            s.Stats = (x, c, st) => { if (x.Self.EquippedTo == c) st.Add(500, 0); };
        }
    }

    static IEnumerable<Card> DarknessTargets(Ctx x) =>
        x.Targetable(x.D.FaceUpMonsters).Where(m => !(x.Classic && m.Data.KonamiId is >= 0x780 and <= 0x7cf));

    /// Move a hand card to another player's hand (Exchange, Graverobber).
    internal static void MoveHand(Duel d, Card c, int to)
    {
        d.P(0).Hand.Remove(c); d.P(1).Hand.Remove(c);
        d.P(to).Hand.Add(c);
        c.Controller = to;
    }
}
