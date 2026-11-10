namespace PoC.Engine;

// Layer 3, part C: hand-written effects (Kaiba pool, second half / early Joey pool).
public static partial class Scripts
{
    static readonly string[] Races =
    {
        "Aqua", "Beast", "Beast-Warrior", "Dinosaur", "Dragon", "Fairy", "Fiend", "Fish", "Insect", "Machine", "Plant",
        "Pyro", "Reptile", "Rock", "Sea Serpent", "Spellcaster", "Thunder", "Warrior", "Winged Beast", "Zombie",
    };
    static readonly string[] AttrList = { "LIGHT", "DARK", "WATER", "FIRE", "EARTH", "WIND" };

    static bool IsToon(Card c) => c.Name.StartsWith("Toon ") || c.Name is "Manga Ryu-Ran" or "Blue-Eyes Toon Dragon";

    static void Toon(string name)
    {
        var s = S(name);
        bool World(Ctx x) => x.D.CountActive("Toon World", x.Me) > 0;
        s.Forbid = (x, q, c) =>
            c == x.Self && (
                (q is Q.NormalSummon or Q.SetMonster && !World(x)) ||
                (q == Q.Attack && (x.Self.SummonTurn == x.D.Turn || x.My.Lp <= 500)) ||
                (q == Q.DirectAttack && x.OppMonsters().FaceUp().Any(IsToon)))
            || (q == Q.BeAttacked && c != null && x.D.Querying == x.Self && x.OppMonsters().FaceUp().Any(IsToon) && !IsToon(c));
        s.Allow = (x, q, c) => q == Q.DirectAttack && c == x.Self;
        s.On = async (x, e) =>
        {
            if (e.Kind == EvKind.AttackDeclared && e.Card == x.Self) x.D.Pay(x.Me, 500);
            else if (e.Kind == EvKind.LeftField && e.Card!.Name == "Toon World" && e.Card.Owner == x.Me && x.Self.Valid()) await x.D.Destroy(x.Self);
        };
    }

    /// Banish a card from a Graveyard, honouring Kycoo and Spirit Elimination.
    internal static async Flow BanishFromGrave(Ctx x, Card c)
    {
        if (c.Loc != Loc.Grave) return;
        if (x.D.TurnFlags.Contains($"spiritelim{c.Owner}") && c.Data.IsMonster)
        {
            var sub = await x.D.PickOne(c.Owner, x.D.P(c.Owner).MonstersOnField, "Spirit Elimination: banish which monster instead?", x.Self);
            if (sub != null) { await x.D.Banish(sub, x.Self); return; }
        }
        await x.D.Banish(c, x.Self);
    }

    internal static bool CanBanishGrave(Ctx x) => x.D.CountActive("Kycoo the Ghost Destroyer", x.Opp) == 0;

    static void CardsC(CardDb db)
    {
        foreach (var n in new[] { "Manga Ryu-Ran", "Toon Mermaid", "Toon Summoned Skull", "Blue-Eyes Toon Dragon" }) Toon(n);
        {
            var s = Flip("Dark-Eyes Illusionist", async x =>
            {
                var t = await x.Pick(x.Targetable(x.OppMonsters()), "Which monster cannot attack?");
                if (t != null) x.Self.Vars["target"] = t.Uid + 1;
            });
            s.Forbid = (x, q, c) => q == Q.Attack && c != null && c.Uid + 1 == x.Self.Var("target");
        }
        static Script EquipOnAttack(string name)
        {
            var s = S(name);
            s.AlwaysActive = true;
            s.On = async (x, e) =>
            {
                if (e.Kind == EvKind.AttackDeclared && e.Other == x.Self && !x.Self.FaceUp && x.Self.Valid() && e.Card!.Valid() && x.My.FreeSpellZones > 0)
                {
                    var a = e.Card;
                    x.D.Place(x.Self, x.Me, Loc.Spell);
                    x.Self.FaceUp = true;
                    await x.D.Equip(x.Self, a);
                    x.Self.Vars["equipturn"] = x.D.Turn;
                }
                else if (e.Kind == EvKind.Standby && e.Player == x.Opp && x.Self.Loc == Loc.Spell && x.Self.EquippedTo is { } m)
                {
                    if (name == "Kiseitai") await x.D.Gain(x.Me, x.Atk(m) / 2, x.Self);
                    else if (x.Self.Var("equipturn") < x.D.Turn)
                    {
                        int atk = x.Atk(m);
                        await x.D.Destroy(new[] { m, x.Self }, x.Self);
                        await x.D.Damage(x.Opp, atk, x.Self);
                    }
                }
            };
            return s;
        }
        EquipOnAttack("Blast Sphere");
        EquipOnAttack("Kiseitai");
        S("Lord of D.").Forbid = (x, q, c) => q == Q.Target && c != null && c.Loc == Loc.Monster && c.FaceUp && x.D.Race(c) == "Dragon";
        static void TributeToAttack(Script s)
        {
            s.Forbid = (x, q, c) => q == Q.Attack && c == x.Self && !x.MyMonsters().Any(m => m != x.Self && !x.D.Forbidden(Q.Tribute, m));
            var prev = s.On;
            s.On = async (x, e) =>
            {
                if (e.Kind == EvKind.AttackDeclared && e.Card == x.Self)
                {
                    var t = await x.Pick(x.MyMonsters().Where(m => m != x.Self && !x.D.Forbidden(Q.Tribute, m)), "Tribute 1 monster to attack");
                    if (t != null) await x.D.MoveTo(t, Loc.Grave);
                }
                if (prev != null) await prev(x, e);
            };
        }
        TributeToAttack(S("Panther Warrior"));
        // Amplifier (equipped): Jinzo leaves its controller's Traps alone.
        S("Jinzo").Forbid = (x, q, c) => c != null && c.Data.IsTrap && (q == Q.Activate || (q == Q.Negate && c.FaceUp))
            && !(c.Controller == x.Me && x.D.EquipsOf(x.Self).Any(e => e.FaceUp && e.Name == "Amplifier"));
        SelfBoost("Dark Magician Girl", x => 300 * x.D.Players.Sum(p => p.Grave.Count(c => c.Name is "Dark Magician" or "Magician of Black Chaos")));
        {
            var s = SelfBoost("Insect Queen", x => 200 * x.D.FaceUpMonsters.Count(m => x.IsRace(m, "Insect")));
            s.On = async (x, e) =>
            {
                if (e.Kind == EvKind.BattleDestroyedMonster && e.Card == x.Self)
                {
                    int me = x.Me;
                    x.D.Later(x.D.Turn, Phase.End, async () =>
                    {
                        if (x.D.FreeMonsterZones(me) == 0) return;
                        var tok = x.D.MakeToken("Insect Monster Token", me, 100, 100, "EARTH", "Insect");
                        await x.D.SpecialSummon(tok, me, await x.D.ChoosePosition(me, tok));
                    });
                }
            };
            TributeToAttack(s);
        }
        {
            var s = S("Parasite Paracide");
            s.On = async (x, e) =>
            {
                if (e.Is(EvKind.Flipped, x.Self) && x.Self.Var("parasite") == 0)
                {
                    // Shuffled face-up into the Deck of the flipping player's opponent.
                    int target = x.Opp;
                    await x.D.MoveTo(x.Self, Loc.Deck);
                    x.D.P(x.Self.Owner).Deck.Remove(x.Self);
                    x.D.P(target).Deck.Insert(0, x.Self);
                    x.D.Shuffle(target);
                    x.Self.Vars["parasite"] = 1;
                }
                else if (e.Kind == EvKind.Drew && e.Cards!.Contains(x.Self) && x.Self.Var("parasite") == 1 && x.Self.Loc == Loc.Hand)
                {
                    int p = e.Player;
                    if (await x.D.SpecialSummon(x.Self, p, true, by: x.Self)) x.Self.Vars["parasite"] = 2;
                    await x.D.Damage(p, 1000, x.Self);
                }
            };
            s.Base = (x, c, st) => { if (x.Self.Var("parasite") == 2 && x.Self.Valid() && c.Controller == x.Self.Controller) st.Race = "Insect"; };
        }
        {
            var s = OnSummon("Total Defense Shogun", async x => { if (x.Self.Attack) await x.D.ChangePosition(x.Self, true); }, normalOnly: true, includeFlip: true);
            s.Allow = (x, q, c) => q == Q.AttackFromDefense && c == x.Self;
        }
        SelfBoost("Buster Blader", x => 500 * (x.OppMonsters().FaceUp().Count(m => x.IsRace(m, "Dragon")) + x.Their.Grave.Count(c => c.Data.Race == "Dragon")));
        {
            var s = S("Valkyrion the Magna Warrior");
            s.NoNormalSummon = true;
            string[] parts = { "Alpha The Magnet Warrior", "Beta The Magnet Warrior", "Gamma The Magnet Warrior" };
            IEnumerable<Card> Pool(Ctx x) => x.My.Hand.Concat(x.MyMonsters());
            s.ProcCan = x => x.Self.Loc == Loc.Hand && parts.All(n => Pool(x).Any(c => c.Name == n)) && (x.D.FreeMonsterZones(x.Me) > 0 || x.MyMonsters().Any(m => parts.Contains(m.Name)));
            s.Proc = async x =>
            {
                foreach (var n in parts) { var c = await x.Pick(Pool(x).Where(c => c.Name == n), $"Tribute {n}"); if (c != null) await x.D.MoveTo(c, Loc.Grave); }
                await x.D.SpecialSummon(x.Self, x.Me, await x.D.ChoosePosition(x.Me, x.Self));
            };
            s.Can = x => parts.All(n => x.My.Grave.Any(c => c.Name == n)) && x.D.FreeMonsterZones(x.Me) >= 2;
            s.Cost = async x => await x.D.MoveTo(x.Self, Loc.Grave);
            s.Run = async x =>
            {
                foreach (var n in parts)
                    if (x.My.Grave.FirstOrDefault(c => c.Name == n) is { } c) await x.D.SpecialSummon(c, x.Me, await x.D.ChoosePosition(x.Me, c), by: x.Self);
            };
        }
        {
            var s = Spell("Magical Hats", async x =>
            {
                var m = await x.Pick(x.MyMonsters(), "Hide which monster?");
                var extras = await x.PickN(x.My.Deck.Where(c => !c.Data.IsMonster), 2, 2, "Pick 2 non-monster cards from your Deck");
                foreach (var c in extras)
                {
                    if (!x.D.Place(c, x.Me, Loc.Monster)) break;
                    c.FaceUp = false; c.Defense = true; c.Vars["hat"] = 1; c.SetTurn = x.D.Turn;
                }
                if (m != null && m.Valid()) { m.FaceUp = false; m.Defense = true; }
                x.D.Shuffle(x.Me);
                x.D.Flags[$"hats{x.Self.Uid}"] = x.D.Turn;
            }, x => !x.Turn() && x.D.Phase == Phase.Battle && x.MyMonsters().Any() && x.My.Deck.Count(c => !c.Data.IsMonster) >= 2 && x.D.FreeMonsterZones(x.Me) >= 2);
            s.On = async (x, e) =>
            {
                if (e.Kind == EvKind.BattleEnd && x.D.Flags.GetValueOrDefault($"hats{x.Self.Uid}") == x.D.Turn)
                    await x.D.Destroy(x.D.AllMonsters.Where(c => c.Var("hat") == 1).ToList(), x.Self);
            };
        }
        Spell("Time Machine", async x =>
        {
            var c = x.Ev!.Card!;
            if (c.Loc != Loc.Grave) return;
            bool def = x.D.Flags.GetValueOrDefault($"lastdef{c.Uid}") == 1;
            await x.D.SpecialSummon(c, c.Owner, def, by: x.Self);
        }, x => x.Ev is { Kind: EvKind.Destroyed, Battle: true } e && e.Card!.Loc == Loc.Grave && e.Card.Data.IsMonster && x.D.FreeMonsterZones(e.Card.Owner) > 0);
        Spell("Toon World", async x => { }, x => x.My.Lp > 1000, async x => x.D.Pay(x.Me, 1000)).Run = null;
        {
            var s = S("7 Completed");
            s.EquipOk = (x, m) => x.D.Race(m) == "Machine";
            s.Cost = async x =>
            {
                await x.Target(x.D.EquipTargets(x.Self), "Equip 7 Completed to which Machine?");
                x.N = await x.D.ChooseText(x.Me, "7 Completed", new[] { "+700 ATK", "+700 DEF" }, x.Self);
            };
            s.Run = async x => { if (x.T0 is { } t && t.Valid() && t.FaceUp) { await x.D.Equip(x.Self, t); x.Self.Vars["def"] = x.N; } };
            s.Stats = (x, c, st) => { if (x.Self.EquippedTo == c) st.Add(x.Self.Var("def") == 0 ? 700 : 0, x.Self.Var("def") == 1 ? 700 : 0); };
        }
        Spell("Lightforce Sword", async x =>
        {
            var h = x.Their.Hand;
            if (h.Count == 0) return;
            var c = h[x.D.Rng.Int(h.Count)];
            await x.D.Banish(c, x.Self);
            c.FaceUp = false;
            int opp = x.Opp;
            int next = x.D.TurnPlayer == opp ? x.D.Turn + 2 : x.D.Turn + 1;
            x.D.Later(next + 6, Phase.Standby, async () => { if (c.Loc == Loc.Banished) await x.D.ToHand(c); });
        }, x => x.Their.Hand.Count > 0);
        Spell("The Flute of Summoning Dragon", async x =>
        {
            foreach (var c in await x.PickN(x.My.Hand.Where(c => c.Data.IsMonster && c.Data.Race == "Dragon"), 0, Math.Min(2, x.D.FreeMonsterZones(x.Me)), "Special Summon up to 2 Dragons"))
                await x.D.SpecialSummon(c, x.Me, await x.D.ChoosePosition(x.Me, c), by: x.Self);
        }, x => x.D.FaceUpMonsters.Any(m => m.Name == "Lord of D.") && x.D.FreeMonsterZones(x.Me) > 0);
        Spell("Shield & Sword", async x => { foreach (var m in x.D.FaceUpMonsters) m.Vars["swapturn"] = x.D.Turn; }, x => x.D.FaceUpMonsters.Any());
        Spell("Chain Destruction", async x =>
        {
            var m = x.Ev!.Card!;
            var p = x.D.P(m.Controller);
            foreach (var c in p.Hand.Concat(p.Deck).Where(c => c.Name == m.Name).ToList()) await x.D.MoveTo(c, Loc.Grave, by: x.Self);
            x.D.Shuffle(m.Controller);
        }, x => x.Ev is { Kind: EvKind.Summoned } e && e.Card!.Valid() && e.Card.FaceUp && x.Atk(e.Card) <= 2000);
        Spell("Magic-Arm Shield", async x =>
        {
            var t = await x.Pick(x.Targetable(x.OppMonsters().FaceUp().Where(m => m != x.D.Attacker)), "Take control of which monster?");
            if (t == null || !await x.D.TakeControl(t, x.Me)) return;
            x.D.AttackTarget = t;
            x.D.Later(x.D.Turn, Phase.End, async () => { if (t.Valid() && t.Controller != t.Owner) await x.D.TakeControl(t, t.Owner); });
        }, x => AttackOnMe(x) && x.OppMonsters().FaceUp().Any(m => m != x.D.Attacker) && x.D.FreeMonsterZones(x.Me) > 0);
        S("Ultimate Offering").Marker = true; // Duel.CanNormalSummon / NormalSummon
        Spell("Soul Release", async x =>
        {
            foreach (var c in await x.PickN(x.D.Players.SelectMany(p => p.Grave), 1, 5, "Banish up to 5 cards from the Graveyards")) await BanishFromGrave(x, c);
        }, x => x.D.Players.Any(p => p.Grave.Count > 0) && CanBanishGrave(x));
        Continuous("Magic Thorn").On = async (x, e) =>
        {
            if (e.Kind == EvKind.Discarded && e.Player == x.Opp && e.Other != null) await x.D.Damage(x.Opp, 500 * e.Amount, x.Self);
        };
        Spell("Fusion Sage", x => x.Search(c => c.Name == "Polymerization", "Add Polymerization to your hand"), x => x.My.Deck.Any(c => c.Name == "Polymerization"));
        {
            var s = Spell("Last Will", async x => x.D.Flags[$"lastwill{x.Me}"] = x.D.Turn);
            s.On = async (x, e) =>
            {
                if (e.Kind == EvKind.ToGrave && e.From == Loc.Monster && e.Card!.Owner == x.Me && x.Self.Loc == Loc.Grave
                    && x.D.Flags.GetValueOrDefault($"lastwill{x.Me}") == x.D.Turn)
                {
                    x.D.Flags[$"lastwill{x.Me}"] = 0;
                    await x.Recruit(c => c.Data.Atk <= 1500, "Last Will: Special Summon a monster with 1500 or less ATK");
                }
            };
        }
        Spell("Gryphon Wing", async x => { await x.D.Negate(x.Ev!.Link!); await x.D.Destroy(x.Spells(x.Opp).ToList(), x.Self); },
            x => OppActivated(x, l => l.Card.Name == "Harpie's Feather Duster"));
        Spell("Time Seal", async x => x.D.Flags[$"skipdraw{x.Opp}"] = 1);
        {
            var s = Spell("Graverobber", async x =>
            {
                var t = await x.Pick(x.Their.Grave.Where(c => c.Data.IsSpell), "Take which Spell?");
                if (t == null) return;
                x.D.TakeToHand(t, x.Me);
                x.D.Flags[$"robbed{t.Uid}"] = x.D.Turn;
                x.D.Later(x.D.Turn, Phase.End, async () => { if (t.Loc == Loc.Hand && t.Controller != t.Owner) await x.D.ToGrave(t); });
            }, x => x.Their.Grave.Any(c => c.Data.IsSpell));
            s.On = async (x, e) =>
            {
                if (e.Kind == EvKind.Activated && e.Card!.Owner != e.Player && x.D.Flags.GetValueOrDefault($"robbed{e.Card.Uid}") == x.D.Turn && e.Link!.FromHand)
                    await x.D.Damage(e.Player, 2000, x.Self);
            };
        }
        Spell("Gift of The Mystical Elf", async x => await x.D.Gain(x.Me, 300 * x.AllMonsters().Count(), x.Self));
        Continuous("The Eye of Truth").On = async (x, e) =>
        {
            if (e.Kind == EvKind.Standby && e.Player == x.Opp)
            {
                x.D.Reveal(x.Me, x.Their.Hand);
                if (x.Their.Hand.Any(c => c.Data.IsSpell)) await x.D.Gain(x.Opp, 1000, x.Self);
            }
        };
        Spell("Dust Tornado", async x =>
        {
            if (x.T0 is { OnField: true } t) await x.D.Destroy(t, x.Self);
            var set = await x.Pick(x.My.Hand.Where(c => !c.Data.IsMonster && !c.Data.Is("Field")), "Set a Spell/Trap from your hand?", optional: true);
            if (set != null && x.D.Place(set, x.Me, Loc.Spell)) { set.FaceUp = false; set.SetTurn = x.D.Turn; await x.D.Raise(new Ev { Kind = EvKind.Set, Card = set, Player = x.Me }); }
        }, x => x.Targetable(x.Spells(x.Opp)).Any(), async x => await x.Target(x.Spells(x.Opp), "Destroy which Spell/Trap?"));
        Spell("Solomon's Lawbook", async x => x.D.Flags[$"skipstandby{x.Me}"] = 1);
        Spell("Earthshaker", async x =>
        {
            int a = await x.D.ChooseText(x.Me, "Earthshaker: first Attribute", AttrList, x.Self);
            var rest = AttrList.Where((_, i) => i != a).ToArray();
            int b = await x.D.ChooseText(x.Me, "Earthshaker: second Attribute", rest, x.Self);
            var pair = new[] { AttrList[a], rest[b] };
            int pick = await x.D.ChooseText(x.Opp, "Earthshaker: destroy which Attribute?", pair, x.Self);
            await x.D.Destroy(x.D.FaceUpMonsters.Where(m => x.IsAttr(m, pair[pick])).ToList(), x.Self);
        });
        {
            var s = Continuous("Mirror Wall");
            s.Stats = (x, c, st) => { if (c.Controller == x.Opp && x.D.Attacker == c) st.Atk -= (st.BaseAtk + st.Atk) / 2; };
            s.On = async (x, e) =>
            {
                if (e.Kind == EvKind.Standby && e.Player == x.Me && x.Self.OnField)
                {
                    if (x.My.Lp > 2000 && await x.Ask("Pay 2000 LP to keep Mirror Wall?")) x.D.Pay(x.Me, 2000);
                    else await x.D.Destroy(x.Self);
                }
            };
        }
        S("Banisher of the Light").Allow = (x, q, c) => q == Q.Banish;
        SelfBoost("Flash Assailant", x => -400 * x.My.Hand.Count, x => -400 * x.My.Hand.Count);
        {
            var s = S("Karate Man");
            s.OncePerTurn = true;
            s.Run = async x =>
            {
                if (!x.Self.Valid()) return;
                x.Self.Vars["karate"] = x.D.Turn;
                var me = x.Self;
                x.D.Later(x.D.Turn, Phase.End, async () => { if (me.Valid()) await x.D.Destroy(me, me); });
            };
            s.Base = (x, c, st) => { if (c == x.Self && x.Self.Var("karate") == x.D.Turn) st.BaseAtk *= 2; };
        }
        MyStandby("Dark Zebra", async x =>
        {
            if (x.My.FieldCards.Count() == 1 && x.Self.Attack) { await x.D.ChangePosition(x.Self, true); x.Self.Vars["nochangepos"] = x.D.Turn; }
        });
        S("Spear Cretin").On = async (x, e) =>
        {
            if (e.Is(EvKind.Flipped, x.Self)) x.D.Flags[$"cretin{x.Self.Uid}"] = 1;
            else if (e.Is(EvKind.ToGrave, x.Self) && x.D.Flags.GetValueOrDefault($"cretin{x.Self.Uid}") == 1)
            {
                x.D.Flags[$"cretin{x.Self.Uid}"] = 0;
                foreach (var p in new[] { x.Self.Owner, 1 - x.Self.Owner })
                {
                    var t = await x.D.PickOne(p, x.D.P(p).Grave.Where(c => c.Data.IsMonster), "Spear Cretin: Special Summon 1 monster", x.Self, optional: true);
                    if (t == null) continue;
                    bool set = await x.D.ChooseText(p, "Position", new[] { "Attack Position", "Face-down Defense" }, x.Self) == 1;
                    await x.D.SpecialSummon(t, p, set, faceUp: !set, by: x.Self);
                }
            }
        };
        {
            var s = OnSummon("Boar Soldier", async x => await x.D.Destroy(x.Self, x.Self), normalOnly: true);
            s.Stats = (x, c, st) => { if (c == x.Self && x.OppMonsters().Any()) st.Add(-1000, 0); };
        }
        S("Ceremonial Bell").Marker = true;   // hands revealed: presentation
        S("Respect Play").Marker = true;      // hands revealed on own turn: presentation
        Continuous("Kotodama").On = async (x, e) =>
        {
            if (e.Kind == EvKind.Summoned && e.Card!.Valid() && x.D.FaceUpMonsters.Any(m => m != e.Card && m.Name == e.Card.Name)) await x.D.Destroy(e.Card, x.Self);
        };
        {
            var s = Continuous("Messenger of Peace");
            s.Forbid = (x, q, c) => q == Q.Attack && c != null && x.Atk(c) >= 1500;
            s.On = async (x, e) =>
            {
                if (e.Kind == EvKind.Standby && e.Player == x.Me && x.Self.OnField)
                {
                    if (x.My.Lp > 100 && await x.Ask("Pay 100 LP to keep Messenger of Peace?")) x.D.Pay(x.Me, 100);
                    else await x.D.Destroy(x.Self);
                }
            };
        }
        static Script DestroyedByOpp(string name, Func<Card, bool> kind) => Spell(name,
            async x => { if (x.T0 is { OnField: true } t) await x.D.Destroy(t, x.Self); },
            x => x.Ev is { Kind: EvKind.Destroyed } e && e.Card!.Owner == x.Me && kind(e.Card) && e.Other != null && e.Other.Controller == x.Opp && x.Targetable(x.AllSpells().Where(c => c != x.Self)).Any(),
            async x => await x.Target(x.AllSpells().Where(c => c != x.Self), "Destroy which Spell/Trap?"));
        DestroyedByOpp("Gust", c => c.Data.IsSpell);
        DestroyedByOpp("Driving Snow", c => c.Data.IsTrap);
        Spell("Armored Glass", async x => x.D.Linger(x.D.Turn, (q, c, p) => q == Q.Negate && c != null && (c.EquippedTo != null || c.Data.Is("Equip"))),
            x => x.Ev is { Kind: EvKind.Equipped });
        Spell("World Suppression", async x => { var f = x.Ev!.Card!; x.D.Linger(x.D.Turn, (q, c, p) => q == Q.Negate && c == f); },
            x => AnyActivated(x, l => l.Card.Data.Is("Field") && l.IsCardActivation));
        Spell("Mystic Probe", async x => x.D.Linger(x.D.Turn, (q, c, p) => q == Q.Negate && c != null && c.Data.IsSpell && c.Data.Is("Continuous")),
            x => AnyActivated(x, l => l.Card.Data.IsSpell && l.Card.Data.Is("Continuous")));
        Spell("Metal Detector", async x => x.D.Linger(x.D.Turn, (q, c, p) => q == Q.Negate && c != null && c.Data.IsTrap && c.Data.Is("Continuous")),
            x => AnyActivated(x, l => l.Card.Data.IsTrap && l.Card.Data.Is("Continuous") && l.Card != x.Self));
        static bool IDamaged(Ctx x) => x.Ev is { Kind: EvKind.Damage } e && e.Player == x.Me;
        Spell("Numinous Healer", async x => await x.D.Gain(x.Me, 1000 + 500 * x.My.Grave.Count(c => c.Name == "Numinous Healer"), x.Self), IDamaged);
        Spell("Attack and Receive", async x => await x.D.Damage(x.Opp, 700 + 300 * x.My.Grave.Count(c => c.Name == "Attack and Receive"), x.Self), IDamaged);
        {
            var s = Spell("Appropriate", async x => { }, x => x.Ev is { Kind: EvKind.Drew, Arg: 0 } e && e.Player == x.Opp);
            s.Run = null;
            s.On = async (x, e) => { if (e.Kind == EvKind.Drew && e.Arg == 0 && e.Player == x.Opp && x.Self.OnField) await x.D.Draw(x.Me, 2); };
        }
        {
            var s = Spell("Forced Requisition", async x => { }, x => x.Ev is { Kind: EvKind.Discarded } e && e.Player == x.Me);
            s.Run = null;
            s.On = async (x, e) =>
            {
                if (e.Kind == EvKind.Discarded && e.Player == x.Me && x.Self.OnField && e.Other != x.Self)
                    await x.D.Discard(x.Opp, await x.D.Pick(x.Opp, x.Their.Hand, e.Amount, e.Amount, $"Forced Requisition: discard {e.Amount}", x.Self), x.Self);
            };
        }
        {
            var s = S("DNA Surgery");
            s.Cost = async x => x.N = await x.D.ChooseText(x.Me, "DNA Surgery: declare a Type", Races, x.Self);
            s.Run = async x => x.Self.Vars["race"] = x.N + 1;
            s.Base = (x, c, st) => { if (x.Self.Var("race") > 0) st.Race = Races[x.Self.Var("race") - 1]; };
        }
        {
            var s = S("The Regulation of Tribe");
            s.Cost = async x => x.N = await x.D.ChooseText(x.Me, "The Regulation of Tribe: declare a Type", Races, x.Self);
            s.Run = async x => x.Self.Vars["race"] = x.N + 1;
            s.Forbid = (x, q, c) => q == Q.Attack && c != null && x.Self.Var("race") > 0 && x.D.Race(c) == Races[x.Self.Var("race") - 1];
            s.On = async (x, e) =>
            {
                if (e.Kind == EvKind.Standby && e.Player == x.Me && x.Self.OnField)
                {
                    var t = await x.Pick(x.MyMonsters().Where(m => !x.D.Forbidden(Q.Tribute, m)), "Tribute 1 monster to keep The Regulation of Tribe", optional: true);
                    if (t != null) await x.D.MoveTo(t, Loc.Grave); else await x.D.Destroy(x.Self);
                }
            };
        }
        Spell("Backup Soldier", async x =>
        {
            foreach (var c in await x.PickN(x.My.Grave.Where(c => c.Data.IsNormalMonster && c.Data.Atk <= 1500), 1, 3, "Add up to 3 monsters to your hand")) await x.D.ToHand(c, x.Self);
        }, x => x.My.Grave.Count(c => c.Data.IsMonster) >= 5 && x.My.Grave.Any(c => c.Data.IsNormalMonster && c.Data.Atk <= 1500));
        Spell("Major Riot", async x =>
        {
            var counts = new[] { x.D.P(0).MonstersOnField.Count(), x.D.P(1).MonstersOnField.Count() };
            foreach (var m in x.AllMonsters().ToList()) await x.D.ToHand(m, x.Self);
            foreach (var p in new[] { x.Me, x.Opp })
            {
                var l = await x.D.Pick(p, x.D.P(p).Hand.Where(c => c.Data.IsMonster && !c.Data.IsFusion && !c.Data.IsRitual && (!x.Classic || c.Data.Level <= 4)), 0, counts[p], "Major Riot: Special Summon monsters face-down", x.Self);
                foreach (var c in l) await x.D.SpecialSummon(c, p, true, faceUp: false, by: x.Self);
            }
        }, x => x.Ev is { Kind: EvKind.ToHand, From: Loc.Monster } e && e.Card!.Owner == x.Me);
        Spell("Ceasefire", async x =>
        {
            foreach (var m in x.AllMonsters().FaceDown().ToList()) await x.D.ChangePosition(m, m.Defense, true, flipEffect: false);
            await x.D.Damage(x.Opp, 500 * x.AllMonsters().Count(m => m.Data.IsEffect), x.Self);
        });
        Continuous("Light of Intervention").Forbid = (x, q, c) => q == Q.SetFaceDown;
        {
            var s = Continuous("Inspection");
            s.On = async (x, e) =>
            {
                if (e.Kind == EvKind.Standby && e.Player == x.Opp && x.Self.OnField && x.My.Lp > 500 && x.Their.Hand.Count > 0
                    && await x.Ask("Pay 500 LP to look at a random card in your opponent's hand?"))
                {
                    x.D.Pay(x.Me, 500);
                    x.D.Reveal(x.Me, x.Their.Hand[x.D.Rng.Int(x.Their.Hand.Count)]);
                }
            };
        }
        {
            var s = S("Prohibition");
            var names = db.ByIdx.Values.Select(c => c.Name).Distinct().OrderBy(n => n).ToArray();
            s.Cost = async x => x.N = await x.D.ChooseText(x.Me, "Prohibition: declare a card name", names, x.Self);
            s.Run = async x => x.Self.Vars["name"] = x.N + 1;
            s.Forbid = (x, q, c) => c != null && !c.OnField && x.Self.Var("name") > 0 && c.Name == names[x.Self.Var("name") - 1]
                && q is Q.Activate or Q.NormalSummon or Q.SetMonster or Q.SetSpell or Q.SpecialSummon;
        }
        Flip("Morphing Jar #2", async x =>
        {
            var lost = new[] { 0, 0 };
            foreach (var m in x.AllMonsters().ToList()) { lost[m.Owner]++; await x.D.ToDeck(m); }
            for (int p = 0; p < 2; p++) x.D.Shuffle(p);
            foreach (var p in new[] { x.Me, x.Opp })
            {
                int found = 0;
                var pl = x.D.P(p);
                while (found < lost[p] && pl.Deck.Count > 0)
                {
                    var c = pl.Deck[0];
                    x.D.Reveal(0, c); x.D.Reveal(1, c);
                    if (c.Data.IsMonster && c.Data.Level <= 4 && !c.Data.IsFusion && !c.Data.IsRitual && x.D.FreeMonsterZones(p) > 0)
                    { found++; await x.D.SpecialSummon(c, p, true, faceUp: false, by: x.Self); }
                    else await x.D.MoveTo(c, Loc.Grave, by: x.Self);
                }
            }
        });
        Continuous("Aqua Chorus").Stats = (x, c, st) => { if (x.D.FaceUpMonsters.Any(m => m != c && m.Name == c.Name)) st.Add(500, 500); };
        {
            var s = Spell("Sebek's Blessing", async x => await x.D.Gain(x.Me, x.Ev!.Amount, x.Self),
                x => x.Ev is { Kind: EvKind.Damage, Battle: true } e && e.Player == x.Opp && e.Other?.Controller == x.Me && x.D.AttackTarget == null);
        }
        Spell("Riryoku", async x =>
        {
            if (x.T.Count < 2) return;
            var (a, b) = (x.T[0], x.T[1]);
            if (!a.Valid() || !b.Valid() || !a.FaceUp || !b.FaceUp) return;
            int half = x.Atk(a) / 2;
            x.D.Boost(a, -half); x.D.Boost(b, half);
        }, x => x.Turn() && x.D.FaceUpMonsters.Count() >= 2,
        async x => { await x.Target(x.D.FaceUpMonsters, "Halve the ATK of which monster?"); await x.Target(x.D.FaceUpMonsters.Where(m => !x.T.Contains(m)), "Give the ATK to which monster?"); });
        S("Sword of Dragon's Soul").On = async (x, e) =>
        {
            if (e.Kind == EvKind.AfterDamage && x.Self.EquippedTo is { } m && (e.Card == m || e.Other == m))
            {
                var foe = e.Card == m ? e.Other : e.Card;
                if (foe != null && foe.Valid() && x.D.Race(foe) == "Dragon") x.Self.Vars["dragon"] = foe.Uid + 1;
            }
            else if (e.Kind == EvKind.BattleEnd && x.Self.Var("dragon") > 0)
            {
                var foe = x.D.CardByUid(x.Self.Var("dragon") - 1);
                x.Self.Vars["dragon"] = 0;
                if (foe.Valid()) await x.D.Destroy(foe, x.Self);
            }
        };
        Spell("Seal of the Ancients", async x => x.D.Reveal(x.Me, x.Their.FieldCards.Where(c => !c.FaceUp)),
            x => x.My.Lp > 1000 && x.Their.FieldCards.Any(c => !c.FaceUp), async x => x.D.Pay(x.Me, 1000));
        {
            var s = S("Big Shield Gardna");
            s.AlwaysActive = true;
            s.On = async (x, e) =>
            {
                if (e.Kind == EvKind.Activated && !x.Self.FaceUp && x.Self.Valid() && e.Card!.Data.IsSpell && e.Link!.X.T.Count == 1 && e.Link.X.T0 == x.Self)
                {
                    e.Link.Negated = true;
                    await x.D.ChangePosition(x.Self, true, true, flipEffect: false);
                }
                else if (e.Kind == EvKind.AfterDamage && e.Other == x.Self && x.Self.Valid() && x.Self.FaceUp && x.Self.Defense)
                    await x.D.ChangePosition(x.Self, false);
            };
        }
        Spell("Graceful Dice", async x => { int r = x.D.Die(x.Me, x.Self); foreach (var m in x.MyMonsters().FaceUp()) x.D.Boost(m, 100 * r, 100 * r); }, x => x.MyMonsters().FaceUp().Any());
        Spell("Skull Dice", async x => { int r = x.D.Die(x.Me, x.Self); foreach (var m in x.OppMonsters().FaceUp()) x.D.Boost(m, -100 * r, -100 * r); }, x => x.OppMonsters().FaceUp().Any());
        Spell("Dark Magic Curtain", async x =>
        {
            var t = await x.Pick(x.My.Deck.Where(c => c.Name == "Dark Magician"), "Special Summon Dark Magician");
            if (t != null) await x.D.SpecialSummon(t, x.Me, await x.D.ChoosePosition(x.Me, t), by: x.Self);
            x.D.Shuffle(x.Me);
            int me = x.Me, turn = x.D.Turn;
            x.D.Linger(turn, (q, c, p) => q is Q.NormalSummon or Q.SetMonster or Q.SpecialSummon && (c?.Controller ?? p) == me);
            x.My.NormalSummoned = true;
        }, x => x.D.FreeMonsterZones(x.Me) > 0 && !x.My.NormalSummoned && x.My.Deck.Any(c => c.Name == "Dark Magician"), PayHalf);
        Spell("Mystic Box", async x =>
        {
            if (x.T0 is { } t && t.Valid()) await x.D.Destroy(t, x.Self);
            var g = await x.Pick(x.MyMonsters(), "Give which of your monsters to your opponent?");
            if (g != null) await x.D.TakeControl(g, x.Opp);
        }, x => x.MyMonsters().Any() && x.Targetable(x.OppMonsters()).Any(), async x => await x.Target(x.OppMonsters(), "Destroy which monster?"));
        {
            var s = Spell("Nightmare's Steelcage", async x => x.Self.Vars["stay"] = 1);
            s.Forbid = (x, q, c) => q == Q.Attack && x.Self.Var("stay") == 1;
            s.On = async (x, e) => { if (e.Kind == EvKind.EndPhase && e.Player == x.Opp && x.Self.OnField && x.Self.Var("stay") == 1 && x.Self.Inc("turns") >= 2) await x.D.Destroy(x.Self); };
        }
        {
            var s = S("Rocket Warrior");
            s.Allow = (x, q, c) => q == Q.NoBattleDamageToController && c == x.Self && x.Turn();
            s.On = async (x, e) => { if (e.Kind == EvKind.AttackDeclared && e.Card == x.Self && e.Other is { } t && t.Valid()) x.D.Boost(t, -500); };
        }
        Spell("Multiply", async x =>
        {
            while (x.D.FreeMonsterZones(x.Me) > 0)
            {
                var tok = x.D.MakeToken("Kuriboh Token", x.Me, 300, 200, "DARK", "Fiend");
                if (!await x.D.SpecialSummon(tok, x.Me, true, by: x.Self)) break;
                tok.Vars["notribute"] = 1;
            }
        }, x => x.MyMonsters().FaceUp().Any(m => m.Name == "Kuriboh"),
        async x => { var k = await x.Pick(x.MyMonsters().FaceUp().Where(m => m.Name == "Kuriboh"), "Tribute Kuriboh"); if (k != null) await x.D.MoveTo(k, Loc.Grave); });
        OnSummon("Berfomet", x => x.Search(c => c.Name == "Gazelle the King of Mythical Beasts", "Add Gazelle to your hand?", optional: true), normalOnly: true);
        {
            var s = S("Beast of Gilfer");
            s.On = async (x, e) =>
            {
                if (!e.Is(EvKind.ToGrave, x.Self) || e.From != Loc.Monster || x.My.FreeSpellZones == 0 || !x.D.FaceUpMonsters.Any()) return;
                var t = await x.Pick(x.Targetable(x.D.FaceUpMonsters), "Equip Beast of Gilfer to which monster?", optional: true);
                if (t == null) return;
                x.D.Place(x.Self, x.Me, Loc.Spell);
                await x.D.Equip(x.Self, t);
            };
            s.Stats = (x, c, st) => { if (x.Self.Loc == Loc.Spell && x.Self.EquippedTo == c) st.Add(-500, 0); };
        }
        {
            var s = S("Copycat");
            s.On = async (x, e) =>
            {
                if (!e.Is(EvKind.Summoned, x.Self)) return;
                var t = await x.Pick(x.OppMonsters().FaceUp(), "Copy which monster's original ATK/DEF?", optional: true);
                if (t != null) { x.Self.Vars["copyatk"] = t.Data.Atk + 1; x.Self.Vars["copydef"] = t.Data.Def; }
            };
            s.Base = (x, c, st) => { if (c == x.Self && x.Self.Var("copyatk") > 0) { st.BaseAtk = x.Self.Var("copyatk") - 1; st.BaseDef = x.Self.Var("copydef"); } };
        }
        Spell("Makiu", async x =>
        {
            x.D.TurnFlags.Add("nobattle");
            if (x.T0 is not { } m || !m.Valid()) return;
            int atk = x.Atk(m);
            await x.D.Destroy(x.OppMonsters().Where(o => x.Def(o) < atk).ToList(), x.Self);
        }, x => x.MyMonsters().FaceUp().Any(m => m.Name == "Summoned Skull" || x.IsRace(m, "Thunder")),
        async x => await x.Target(x.MyMonsters().FaceUp().Where(m => m.Name == "Summoned Skull" || x.IsRace(m, "Thunder")), "Use which monster?"));
        S("Kinetic Soldier").Stats = (x, c, st) =>
        {
            if (c != x.Self) return;
            var foe = x.D.Attacker == c ? x.D.AttackTarget : x.D.AttackTarget == c ? x.D.Attacker : null;
            if (foe != null && foe.FaceUp && x.D.Race(foe) == "Warrior") st.Add(2000, 2000);
        };
        static void Weakener(Script s)
        {
            var prev = s.On;
            s.On = async (x, e) =>
            {
                if (e.Is(EvKind.Destroyed, x.Self) && e.Battle && e.Other is { } k && k.Valid()) { k.Vars["modatk"] = k.Var("modatk") - 500; k.Vars["moddef"] = k.Var("moddef") - 500; }
                if (prev != null) await prev(x, e);
            };
        }
        Weakener(Flip("Slate Warrior", async x => { x.Self.Vars["modatk"] = x.Self.Var("modatk") + 500; x.Self.Vars["moddef"] = x.Self.Var("moddef") + 500; }));
        Weakener(Flip("Electromagnetic Bagworm", async x =>
        {
            var t = await x.Pick(x.Targetable(x.OppMonsters().Where(m => m.FaceUp && x.IsRace(m, "Machine"))), "Take control of which Machine?");
            if (t == null || !await x.D.TakeControl(t, x.Me)) return;
            int me = x.Me;
            int next = x.D.TurnPlayer == me ? x.D.Turn + 2 : x.D.Turn + 1;
            x.D.Later(next, Phase.End, async () => { if (t.Valid() && t.Controller != t.Owner) await x.D.TakeControl(t, t.Owner); });
        }));
        Weakener(Flip("Rigras Leever", async x =>
        {
            var a = await x.Pick(x.Their.Hand, "Discard which card from your opponent's hand?");
            var b = await x.D.PickOne(x.Opp, x.My.Hand, "Discard which card from your opponent's hand?", x.Self);
            if (a != null) await x.D.Discard(x.Opp, new[] { a }, x.Self);
            if (b != null) await x.D.Discard(x.Me, new[] { b }, x.Self);
        }));
        Spell("Michizure", async x => { if (x.T0 is { } t && t.Valid()) await x.D.Destroy(t, x.Self); },
            x => x.Ev is { Kind: EvKind.ToGrave, From: Loc.Monster } e && e.Card!.Owner == x.Me && x.Targetable(x.AllMonsters()).Any(),
            async x => await x.Target(x.AllMonsters(), "Destroy which monster?"));
        {
            var s = Spell("Minor Goblin Official", async x => { }, x => x.Their.Lp <= 3000);
            s.Run = null;
            s.On = async (x, e) => { if (e.Kind == EvKind.Standby && e.Player == x.Opp && x.Self.OnField) await x.D.Damage(x.Opp, 500, x.Self); };
        }
    }
}
