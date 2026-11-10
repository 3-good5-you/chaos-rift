namespace PoC.Engine;

// Official cards, batch 3 (Battle of Sleeping Spirits .. Byroad Sacrifice).
public static partial class Scripts
{
    /// Dark World: "If this card is discarded to the GY by a card effect" (not as a cost, not the hand-size limit).
    internal static bool DiscardedByEffect(Ctx x, Ev e) => e.Kind == EvKind.Discarded && !e.Cost && e.Other != null && e.Cards!.Contains(x.Self) && x.Self.Loc == Loc.Grave;

    /// Remember which monster an equip/continuous card is tied to (cleared fields can't hold it once the card moves).
    internal static Card? TiedMonster(Ctx x) => x.D.Flags.GetValueOrDefault($"tie{x.Self.Uid}") is > 0 and var u ? x.D.CardByUid(u - 1) : null;
    internal static void Tie(Ctx x, Card m) => x.D.Flags[$"tie{x.Self.Uid}"] = m.Uid + 1;

    static void CardsP03(CardDb db)
    {
        Spell("Battle of Sleeping Spirits", async x =>
        {
            foreach (var p in new[] { x.Me, x.Opp })
            {
                var l = x.D.BattleDestroyedThisTurn(p).Where(c => x.D.CanSpecialSummon(c, p, by: x.Self)).ToList();
                if (l.Count == 0) continue;
                var c = await x.D.PickOne(p, l, "Special Summon 1 monster destroyed by battle this turn (Attack Position)?", x.Self, optional: true);
                if (c == null || !await x.D.SpecialSummon(c, p, false, by: x.Self)) continue;
                if (p == x.Me && c.Name == "Elemental HERO Neos") c.Vars["doubleturn"] = x.D.Turn;
            }
        }, x => Hopt(x) && (x.D.BattleDestroyedThisTurn(x.Me).Any() || x.D.BattleDestroyedThisTurn(x.Opp).Any()), async x => UseHopt(x));
        // Elemental HERO Neos summoned by Battle of Sleeping Spirits: double ATK when it battles an opponent's monster this turn.
        S("Elemental HERO Neos").CalcAtk = (x, c, vs, atk) => c == x.Self && x.Self.Var("doubleturn") == x.D.Turn && vs != null && vs.Controller != x.Me ? atk * 2 : atk;
        {
            var s = Spell("Battle-Scarred", async x => { if (x.T0 is { } t && t.Valid()) { Tie(x, t); x.Self.Vars["stay"] = 1; } },
                x => x.Targetable(x.MyMonsters().FaceUp().Where(m => m.In("Archfiend"))).Any(),
                async x => await x.Target(x.MyMonsters().FaceUp().Where(m => m.In("Archfiend")), "Select 1 Archfiend monster you control"));
            s.Paid = (x, p, amount, src) => { if (p == x.Me && src != null && src == TiedMonster(x) && x.D.Phase == Phase.Standby) x.D.Pay(x.Opp, amount); };
            s.On = async (x, e) =>
            {
                if (e.Is(EvKind.LeftField, x.Self) && TiedMonster(x) is { OnField: true } m) { x.D.Flags.Remove($"tie{x.Self.Uid}"); await x.D.Destroy(m, x.Self); }
                if (e.Kind == EvKind.LeftField && e.Card == TiedMonster(x) && x.Self.OnField) { x.D.Flags.Remove($"tie{x.Self.Uid}"); await x.D.Destroy(x.Self, x.Self); }
            };
        }
        Spell("Beast Soul Swap", async x =>
        {
            if (x.T0 is not { } t || !t.Valid()) return;
            int lv = x.D.Level(t);
            await x.D.ToHand(t, x.Self);
            if (t.Loc == Loc.Hand) await SummonPick(x, x.My.Hand.Where(c => c.Data.IsMonster && c.Data.Race == "Beast" && c.Data.Level == lv), $"Special Summon a Level {lv} Beast from your hand");
        }, x => x.Targetable(x.MyMonsters().FaceUp().Where(m => x.IsRace(m, "Beast"))).Any(),
        async x => await x.Target(x.MyMonsters().FaceUp().Where(m => x.IsRace(m, "Beast")), "Return which Beast to the hand?"));
        Spell("Beckoning Light", async x =>
        {
            var hand = x.My.Hand.ToList();
            await x.D.Discard(x.Me, hand, x.Self);
            int n = hand.Count(c => c.Loc == Loc.Grave);
            var l = x.My.Grave.Where(c => c.Data.IsMonster && c.Data.Attribute == "LIGHT").ToList();
            foreach (var c in await x.PickN(l, Math.Min(n, l.Count), Math.Min(n, l.Count), $"Add {Math.Min(n, l.Count)} LIGHT monster(s) to your hand")) await x.D.ToHand(c, x.Self);
        }, x => x.My.Hand.Any(c => c != x.Self));
        S("Beiige, Vanguard of Dark World").On = async (x, e) => { if (DiscardedByEffect(x, e) && x.D.CanSpecialSummon(x.Self, x.Me, by: x.Self)) await x.D.SpecialSummon(x.Self, x.Me, await x.D.ChoosePosition(x.Me, x.Self), by: x.Self); };
        {
            var s = S("Berserk Dragon");
            s.SummonOk = (x, by, k) => by?.Name == "A Deal with Dark Ruler";
            s.AttackEach = true;
            s.On = async (x, e) => { if (e.Kind == EvKind.EndPhase && e.Player == x.Me && Up(x)) { x.Self.Vars["modatk"] = x.Self.Var("modatk") - 500; x.D.Seq++; } };
        }
        S("Berserk Gorilla").Note = "While it can attack, your Battle Phase can't end until it has attacked.";
        S("Berserk Gorilla").On = async (x, e) =>
        {
            if (!Up(x)) return;
            if (x.Self.Defense) { await x.D.Destroy(x.Self, x.Self); return; }
            if (x.Self.Var("mustattack") != x.D.Turn) x.Self.Vars["mustattack"] = x.D.Turn;   // must attack if able
        };
        Spell("Berserker Soul", async x =>
        {
            for (int i = 0; i < 8 && x.My.Deck.Count > 0; i++)
            {
                var top = x.My.Deck[0];
                x.D.Reveal(x.Me, top); x.D.Reveal(x.Opp, top);
                if (!top.Data.IsMonster) break;   // a non-monster stays on top of the Deck
                await x.D.ToGrave(top, x.Self);
                await x.D.Damage(x.Opp, 500, x.Self);
            }
        }, x => Hopt(x) && x.Ev is { Kind: EvKind.Damage, Battle: true } e && e.Player == x.Opp && e.Amount <= 1500 && e.Other is { } a && a.Controller == x.Me
            && x.D.Attacker == a && x.D.AttackTarget == null && x.My.Hand.Any(c => c != x.Self),
        async x => { UseHopt(x); await x.D.Discard(x.Me, x.My.Hand.Where(c => c != x.Self).ToList(), x.Self); });
        {
            var s = S("Big Bang Shot");
            s.EquipOk = (x, m) => true;
            s.Stats = (x, c, st) => { if (c == x.Self.EquippedTo) st.Add(400, 0); };
            s.Allow = (x, q, c) => q == Q.Piercing && c == x.Self.EquippedTo;
            s.On = async (x, e) =>
            {
                if (e.Is(EvKind.Equipped, x.Self)) Tie(x, e.Other!);
                if (e.Is(EvKind.LeftField, x.Self) && TiedMonster(x) is { OnField: true } m) { x.D.Flags.Remove($"tie{x.Self.Uid}"); await x.D.Banish(m, x.Self); }
            };
        }
        Spell("Big Burn", async x =>
        {
            foreach (var c in x.D.Players.SelectMany(p => p.Grave).Where(c => c.Data.IsMonster).ToList()) await x.D.Banish(c, x.Self);
        }, x => x.Ev is { Kind: EvKind.Activated, Link: { } l } && l.Player == x.Opp && l.X.T.Any(t => t.Loc == Loc.Grave && t.Data.IsMonster));
        {
            var s = Spell("Big Evolution Pill", async x => { x.Self.Vars["stay"] = 1; x.Self.Vars["oppends"] = 0; },
                x => x.MyMonsters().Any(m => x.IsRace(m, "Dinosaur") && !x.D.Forbidden(Q.Tribute, m)),
                async x => { var t = await x.Pick(x.MyMonsters().Where(m => x.IsRace(m, "Dinosaur") && !x.D.Forbidden(Q.Tribute, m)), "Tribute 1 Dinosaur"); if (t != null) await x.D.Tribute(t, x.Self); });
            s.FreeTribute = (x, c, set) => !set && c.Data.Race == "Dinosaur" && c.Data.Level >= 5 && x.Self.OnField && x.Self.Var("stay") == 1;
            s.OnFreeTribute = async x => { };
            s.On = async (x, e) =>
            {
                if (e.Kind == EvKind.EndPhase && e.Player == x.Opp && x.Self.OnField && x.Self.Var("stay") == 1 && x.Self.Inc("oppends") >= 3) await x.D.Destroy(x.Self, x.Self);
            };
        }
        Spell("Big Wave Small Wave", async x =>
        {
            var l = x.MyMonsters().FaceUp().Where(m => x.IsAttr(m, "WATER")).ToList();
            await x.D.Destroy(l, x.Self);
            int n = l.Count(m => m.Loc != Loc.Monster);
            for (int i = 0; i < n; i++)
            {
                var w = x.My.Hand.Where(c => c.Data.IsMonster && c.Data.Attribute == "WATER" && x.D.CanSpecialSummon(c, x.Me, by: x.Self)).ToList();
                if (w.Count == 0 || !await x.Ask("Special Summon a WATER monster from your hand?")) break;
                await SummonPick(x, w, "Special Summon which WATER monster?");
            }
        }, x => x.MyMonsters().FaceUp().Any(m => x.IsAttr(m, "WATER")));
        {
            var s = S("Black Luster Soldier - Envoy of the Beginning");
            s.SummonOk = (x, by, k) => by == x.Self || x.D.Flags.ContainsKey($"proper{x.Self.Uid}");
            static IEnumerable<Card> Of(Ctx x, string a) => x.My.Grave.Where(c => c.Data.IsMonster && c.Data.Attribute == a);
            s.ProcCan = x => Of(x, "LIGHT").Any() && Of(x, "DARK").Any();
            s.Proc = async x =>
            {
                var l = await x.Pick(Of(x, "LIGHT"), "Banish 1 LIGHT monster from your GY");
                var d = await x.Pick(Of(x, "DARK"), "Banish 1 DARK monster from your GY");
                if (l == null || d == null) return;
                await x.D.Banish(l, x.Self); await x.D.Banish(d, x.Self);
                await x.D.SpecialSummon(x.Self, x.Me, await x.D.ChoosePosition(x.Me, x.Self), by: x.Self);
            };
            s.Can = x => Up(x) && x.Self.Var("blsturn") != x.D.Turn && x.Self.Attacks == 0 && x.Targetable(x.AllMonsters().Where(m => m != x.Self)).Any();
            s.Cost = async x => { x.Self.Vars["blsturn"] = x.D.Turn; x.Self.Vars["noattack"] = x.D.Turn; await x.Target(x.AllMonsters().Where(m => m != x.Self), "Banish which monster?"); };
            s.Run = async x => { if (x.T0 is { } t && t.Valid()) await x.D.Banish(t, x.Self); };
            s.On = async (x, e) =>
            {
                if (e.Kind == EvKind.BattleDestroyedMonster && e.Card == x.Self && x.D.Attacker == x.Self && Up(x) && x.Self.Var("blsturn") != x.D.Turn
                    && await x.Ask("Make a second attack in a row (Black Luster Soldier)?"))
                { x.Self.Vars["blsturn"] = x.D.Turn; x.Self.Vars["maxatk"] = x.Self.Attacks + 1; x.Self.Vars["maxatkturn"] = x.D.Turn; }
            };
        }
        S("Black Stego").On = async (x, e) => { if (e.Kind == EvKind.AttackDeclared && e.Other == x.Self && Up(x) && x.Self.Attack) await x.D.ChangePosition(x.Self, true); };
        S("Black Tyranno").Allow = (x, q, c) => q == Q.DirectAttack && c == x.Self && !x.Their.SpellsOnField.Any() && x.OppMonsters().All(m => m.Defense);
        {
            var s = SelfBoost("Blade Knight", x => x.My.Hand.Count <= 1 ? 400 : 0);
            s.On = async (x, e) =>
            {
                if (e.Kind == EvKind.BattleDestroyedMonster && e.Card == x.Self && !x.MyMonsters().Any(m => m != x.Self) && e.Other!.Data.Desc.StartsWith("FLIP"))
                    x.D.Flags[$"neg{e.Other.Uid}"] = 1;
            };
        }
        {
            var s = S("Blaze Accelerator");
            s.AddFx(f =>
            {
                f.Label = "Destroy a monster";
                f.Can = x => x.Targetable(x.OppMonsters()).Any() && x.My.Hand.Any(c => c.Data.IsMonster && c.Data.Race == "Pyro" && c.Data.Atk <= 500);
                f.Cost = async x => await x.Target(x.OppMonsters(), "Destroy which monster?");
                f.Run = async x =>
                {
                    var d = x.D; int me = x.Me;
                    d.Linger(d.Turn, (q, c, p) => q == Q.Attack && c != null && c.Controller == me);
                    var p = await x.Pick(x.My.Hand.Where(c => c.Data.IsMonster && c.Data.Race == "Pyro" && c.Data.Atk <= 500), "Send 1 Pyro monster (500 or less ATK) to the GY");
                    if (p == null) return;
                    await x.D.ToGrave(p, x.Self);
                    if (x.T0 is { } t && t.Valid()) await x.D.Destroy(t, x.Self);
                };
            });
        }
        {
            var s = S("Blockman");
            s.On = async (x, e) => { if (Up(x) && x.D.TurnPlayer == x.Me && x.Self.Var("lastup") != x.D.Turn) { x.Self.Vars["lastup"] = x.D.Turn; x.Self.Inc("upturns"); } };
            s.Can = x => Up(x);
            s.Cost = async x => { x.N = Math.Max(1, x.Self.Var("upturns")); await x.D.Tribute(x.Self, x.Self); };
            s.Run = async x =>
            {
                for (int i = 0; i < x.N && x.D.FreeMonsterZones(x.Me) > 0; i++)
                {
                    var t = x.D.MakeToken("Block Token", x.Me, 1000, 1500, "EARTH", "Rock", 4);
                    if (await x.D.SpecialSummon(t, x.Me, true, by: x.Self)) t.Vars["noattack"] = int.MaxValue;
                }
            };
        }
        {
            var s = S("Blue Flame Swordsman");
            s.AddFx(f =>
            {
                f.Label = "Give 600 ATK"; f.Speed = 2; f.AnyTurn = true; f.OncePerTurn = true;
                f.Can = x => x.D.Phase == Phase.Battle && x.Atk(x.Self) >= 600 && x.Targetable(x.MyMonsters().FaceUp().Where(m => m != x.Self && x.IsRace(m, "Warrior"))).Any();
                f.Cost = async x => await x.Target(x.MyMonsters().FaceUp().Where(m => m != x.Self && x.IsRace(m, "Warrior")), "Which Warrior gains 600 ATK?");
                f.Run = async x =>
                {
                    if (!Up(x) || x.Atk(x.Self) < 600) return;
                    x.Self.Vars["modatk"] = x.Self.Var("modatk") - 600;
                    if (x.T0 is { } t && t.Valid() && t.FaceUp) t.Vars["modatk"] = t.Var("modatk") + 600;
                    x.D.Seq++;
                };
            });
            s.On = async (x, e) =>
            {
                if (!e.Is(EvKind.Destroyed, x.Self) || x.Self.Loc != Loc.Grave || e.Other is not { } by || by.Controller == x.Me) return;   // destroyed by your opponent's card
                var l = x.My.Grave.Where(c => c != x.Self && c.Data.IsMonster && c.Data.Attribute == "FIRE" && c.Data.Race == "Warrior").ToList();
                if (l.Count == 0 || !await x.Ask("Banish Blue Flame Swordsman to Special Summon a FIRE Warrior from your GY?")) return;
                await x.D.Banish(x.Self, x.Self);
                await SummonPick(x, l, "Special Summon which FIRE Warrior?");
            };
        }
        {
            var s = S("Blue-Eyes Tyrant Dragon");
            s.Materials = new() { Named("Blue-Eyes White Dragon"), Cond("1 Dragon monster", c => c.Data.IsMonster && c.Data.Race == "Dragon") };
            s.SummonOk = (x, by, k) => k == SummonKind.Fusion || by == x.Self;
            static IEnumerable<Card> Hosts(Ctx x) => x.MyMonsters().Where(m => m.FaceUp && Scripts.NameOf(m) == "Blue-Eyes White Dragon" && x.D.EquipsOf(m).Any(e => e.Data.IsFusion));
            s.ProcFromDeck = true;
            s.ProcCan = x => x.Self.Loc == Loc.Extra && Hosts(x).Any();
            s.Proc = async x =>
            {
                var t = await x.Pick(Hosts(x), "Tribute Blue-Eyes White Dragon (equipped with a Fusion Monster)");
                if (t == null) return;
                await x.D.Tribute(t, x.Self);
                await x.D.SpecialSummon(x.Self, x.Me, await x.D.ChoosePosition(x.Me, x.Self), by: x.Self);
            };
            s.Forbid = (x, q, c) => q == Q.AffectedByTrap && c == x.Self;
            s.AttackEach = true;
            s.On = async (x, e) =>
            {
                if (e.Kind == EvKind.AfterDamage && (e.Card == x.Self || e.Other == x.Self) && Up(x) && x.Self.Var("tyrantturn") != x.D.Turn
                    && x.My.FreeSpellZones > 0 && x.My.Grave.Any(c => c.Data.IsTrap) && await x.Ask("Set 1 Trap from your GY?"))
                {
                    x.Self.Vars["tyrantturn"] = x.D.Turn;
                    var t = await x.Pick(x.Targetable(x.My.Grave.Where(c => c.Data.IsTrap)), "Set which Trap?");
                    if (t != null && x.D.Place(t, x.Me, Loc.Spell)) { t.FaceUp = false; t.SetTurn = x.D.Turn; await x.D.Raise(new Ev { Kind = EvKind.Set, Card = t, Player = x.Me }); }
                }
            };
        }
        Spell("Bonding - H2O", async x =>
        {
            await SummonPick(x, x.My.Hand.Concat(x.My.Deck).Concat(x.My.Grave).Where(c => c.Name == "Water Dragon"), "Special Summon Water Dragon");
            x.D.Shuffle(x.Me);
        }, x => x.MyMonsters().Count(m => Scripts.NameOf(m) == "Hydrogeddon") >= 2 && x.MyMonsters().Any(m => Scripts.NameOf(m) == "Oxygeddon")
            && x.My.Hand.Concat(x.My.Deck).Concat(x.My.Grave).Any(c => c.Name == "Water Dragon"),
        async x =>
        {
            foreach (var t in await x.PickN(x.MyMonsters().Where(m => Scripts.NameOf(m) == "Hydrogeddon"), 2, 2, "Tribute 2 Hydrogeddon")) await x.D.Tribute(t, x.Self);
            var o = await x.Pick(x.MyMonsters().Where(m => Scripts.NameOf(m) == "Oxygeddon"), "Tribute 1 Oxygeddon");
            if (o != null) await x.D.Tribute(o, x.Self);
        });
        Spell("Bonfire", x => x.Search(c => c.Data.IsMonster && c.Data.Race == "Pyro" && c.Data.Level <= 4, "Add a Level 4 or lower Pyro"),
            x => Hopt(x) && x.My.Deck.Any(c => c.Data.IsMonster && c.Data.Race == "Pyro" && c.Data.Level <= 4), async x => UseHopt(x));
        Spell("Book of Life", async x =>
        {
            if (x.T.Count < 2) return;
            if (x.T[0].Loc == Loc.Grave) await x.D.SpecialSummon(x.T[0], x.Me, await x.D.ChoosePosition(x.Me, x.T[0]), by: x.Self);
            if (x.T[1].Loc == Loc.Grave) await x.D.Banish(x.T[1], x.Self);
        }, x => x.D.FreeMonsterZones(x.Me) > 0 && x.Targetable(x.My.Grave.Where(c => c.Data.IsMonster && c.Data.Race == "Zombie")).Any() && x.Targetable(x.Their.Grave.Where(c => c.Data.IsMonster)).Any(),
        async x =>
        {
            await x.Target(x.My.Grave.Where(c => c.Data.IsMonster && c.Data.Race == "Zombie"), "Special Summon which Zombie?");
            await x.Target(x.Their.Grave.Where(c => c.Data.IsMonster), "Banish which monster from your opponent's GY?");
        });
        Spell("Book of Moon", async x => { if (x.T0 is { } t && t.Valid() && t.FaceUp) await x.D.SetFaceDown(t); },
            x => x.Targetable(x.D.FaceUpMonsters.Where(m => !m.IsToken)).Any(),
            async x => await x.Target(x.D.FaceUpMonsters.Where(m => !m.IsToken), "Change which monster to face-down Defense Position?"));
        Spell("Book of Taiyou", async x =>
        {
            var t = await x.Pick(x.AllMonsters().Where(m => !m.FaceUp), "Flip which face-down monster face-up?");
            if (t != null) await x.D.ChangePosition(t, false, true);
        }, x => x.AllMonsters().Any(m => !m.FaceUp));
        {
            var s = S("Boss Rush");
            static bool Boss(Card c) => c.In("B.E.S.") || c.Name == "Big Core";
            s.Forbid = (x, q, c) => q is Q.NormalSummon or Q.SetMonster && c != null && c.Controller == x.Me;
            s.On = async (x, e) =>
            {
                if (e.Kind == EvKind.Destroyed && Boss(e.Card!) && e.Card!.Loc == Loc.Grave) x.Self.Vars["bossturn"] = x.D.Turn;
                if (e.Kind == EvKind.EndPhase && x.Self.Var("bossturn") == x.D.Turn && x.My.Deck.Any(c => c.Data.IsMonster && Boss(c)) && await x.Ask("Special Summon a B.E.S. monster or Big Core from your Deck?"))
                {
                    await SummonPick(x, x.My.Deck.Where(c => c.Data.IsMonster && Boss(c)), "Special Summon which?");
                    x.D.Shuffle(x.Me);
                }
            };
        }
        Spell("Bottomless Trap Hole", async x =>
        {
            foreach (var m in Summoned1500(x).ToList()) { await x.D.Destroy(m, x.Self); if (m.Loc == Loc.Grave) await x.D.Banish(m, x.Self); }
        }, x => Summoned1500(x).Any());
        static IEnumerable<Card> Summoned1500(Ctx x) => x.Ev is { Kind: EvKind.Summoned } e && e.Player == x.Opp && e.Card!.Valid() && e.Card.FaceUp && x.Atk(e.Card) >= 1500 ? new[] { e.Card } : Array.Empty<Card>();
        MyStandby("Bowganian", async x => await x.D.Damage(x.Opp, 600, x.Self));
        {
            static bool Normalable(Ctx x, Card m) => !m.IsToken && !m.Data.IsFusion && !m.Data.IsRitual && x.D.S(m)?.NoNormalSummon != true;
            Spell("Brain Control", async x =>
            {
                if (x.T0 is not { } t || !t.Valid() || !t.FaceUp) return;
                if (await x.D.TakeControl(t, x.Me))
                    x.D.Later(x.D.Turn, Phase.End, async () => { if (t.Valid() && t.Controller == x.Me) await x.D.TakeControl(t, t.Owner); });
            }, x => x.My.Lp > 800 && x.D.FreeMonsterZones(x.Me) > 0 && x.Targetable(x.OppMonsters().FaceUp().Where(m => Normalable(x, m))).Any(),
            async x => { x.D.Pay(x.Me, 800); await x.Target(x.OppMonsters().FaceUp().Where(m => Normalable(x, m)), "Take control of which monster?"); });
        }
        {
            static IEnumerable<Card> Seals(Ctx x) => x.My.Deck.Concat(x.My.Grave).Where(c => c.Name == "Break the Seal");
            static async Flow PlaceSeal(Ctx x)
            {
                var c = await x.Pick(Seals(x), "Place which Break the Seal face-up?");
                bool deck = c?.Loc == Loc.Deck;
                if (c != null && x.D.Place(c, x.Me, Loc.Spell)) { c.FaceUp = true; c.Vars["stay"] = 1; x.D.Seq++; }
                if (deck) x.D.Shuffle(x.Me);
            }
            var s = Spell("Break the Seal", async x =>
            {
                if (Hopt(x, "1") && Seals(x).Any() && x.My.FreeSpellZones > 0 && await x.Ask("Also place 1 Break the Seal from your Deck or GY face-up?")) { UseHopt(x, "1"); await PlaceSeal(x); }
            });
            s.AddFx(f =>
            {
                f.Label = "Place another Break the Seal";
                f.Can = x => Hopt(x, "1") && Seals(x).Any() && x.My.FreeSpellZones > 0;
                f.Cost = async x => UseHopt(x, "1");
                f.Run = PlaceSeal;
            });
            s.AddFx(f =>
            {
                f.Label = "Add a Forbidden One";
                f.Can = x => Hopt(x, "2") && x.Spells(x.Me).Count(c => c.FaceUp && c.Name == "Break the Seal") >= 2 && x.My.Deck.Any(c => c.Data.IsMonster && c.In("Forbidden One"));
                f.Cost = async x =>
                {
                    UseHopt(x, "2");
                    var other = await x.Pick(x.Spells(x.Me).Where(c => c.FaceUp && c.Name == "Break the Seal" && c != x.Self), "Send which other Break the Seal to the GY?");
                    await x.D.ToGrave(x.Self, x.Self);
                    if (other != null) await x.D.ToGrave(other, x.Self);
                };
                f.Run = x => x.Search(c => c.Data.IsMonster && c.In("Forbidden One"), "Add which Forbidden One?");
            });
            s.On = async (x, e) =>
            {
                if (!e.Is(EvKind.Destroyed, x.Self) || !Hopt(x, "3")) return;
                var pieces = x.My.Hand.Where(c => c.Data.IsMonster && c.In("Forbidden One")).ToList();
                if (pieces.Count == 0 || !x.Their.FieldCards.Any() || !await x.Ask("Reveal Forbidden One monsters to return your opponent's cards to the hand?")) return;
                UseHopt(x, "3");
                var shown = await x.PickN(pieces, 1, Math.Min(5, pieces.Count), "Reveal up to 5 Forbidden One monsters");
                x.D.Reveal(x.Opp, shown);
                foreach (var c in await x.PickN(x.Their.FieldCards, Math.Min(shown.Count, x.Their.FieldCards.Count()), Math.Min(shown.Count, x.Their.FieldCards.Count()), $"Return {shown.Count} of your opponent's cards to the hand"))
                    await x.D.ToHand(c, x.Self);
            };
        }
        {
            var s = S("Breaker the Magical Warrior");
            s.Stats = (x, c, st) => { if (c == x.Self) st.Add(300 * x.Self.Counters("spell"), 0); };
            s.On = async (x, e) => { if (e.Is(EvKind.Summoned, x.Self) && e.Summoned(SummonKind.Normal, SummonKind.Tribute) && x.Self.Counters("spell") < 1) AddCounter(x, x.Self, 1, "spell"); };
            s.Can = x => Up(x) && x.Self.Counters("spell") > 0 && x.Targetable(x.AllSpells()).Any();
            s.Cost = async x => { AddCounter(x, x.Self, -1, "spell"); await x.Target(x.AllSpells(), "Destroy which Spell/Trap?"); };
            s.Run = async x => { if (x.T0 is { OnField: true } t) await x.D.Destroy(t, x.Self); };
        }
        S("Broww, Huntsman of Dark World").On = async (x, e) =>
        {
            if (!DiscardedByEffect(x, e)) return;
            bool byOpp = e.Other!.Controller != x.Me && e.Player == x.Me;
            await x.D.Draw(x.Me, byOpp ? 2 : 1);
        };
        {
            var s = Spell("Brutal Potion", async x =>
            {
                if (x.T0 is not { } t || !t.Valid()) return;
                await x.D.Equip(x.Self, t);
                x.Self.Vars["stay"] = 1;
            }, x => x.Targetable(x.MyMonsters().FaceUp()).Any(), async x => await x.Target(x.MyMonsters().FaceUp(), "Equip Brutal Potion to which monster?"));
            s.On = async (x, e) =>
            {
                if (e.Kind == EvKind.Damage && !e.Battle && e.Arg == 0 && e.Player == x.Opp && e.Other is { } src && src.Controller == x.Me
                    && x.Self.EquippedTo is { } m && x.Self.Var("potionturn") != x.D.Turn)
                { x.Self.Vars["potionturn"] = x.D.Turn; x.D.Boost(m, 1000); }
            };
        }
        {
            var s = S("Bubble Blaster");
            s.EquipOk = (x, m) => Scripts.NameOf(m) == "Elemental HERO Bubbleman";
            s.Stats = (x, c, st) => { if (c == x.Self.EquippedTo) st.Add(800, 0); };
            s.BattleShield = (x, m) => m == x.Self.EquippedTo;
            s.ShieldNoDamage = true;
        }
        Spell("Bubble Illusion", async x => x.D.TurnFlags.Add($"traphand{x.Me}"), x => x.MyMonsters().FaceUp().Any(m => Scripts.NameOf(m) == "Elemental HERO Bubbleman"));
        Spell("Bubble Shuffle", async x =>
        {
            if (x.T.Count < 2) return;
            var (b, o) = (x.T[0], x.T[1]);
            foreach (var t in new[] { b, o }) if (t.Valid() && t.FaceUp && t.Attack) await x.D.ChangePosition(t, true);
            if (!b.Valid()) return;
            await x.D.Tribute(b, x.Self);
            await SummonPick(x, x.My.Hand.Where(c => c.Data.IsMonster && c.In("Elemental HERO")), "Special Summon an Elemental HERO from your hand");
        }, x => x.Targetable(x.MyMonsters().Where(m => m.FaceUp && m.Attack && Scripts.NameOf(m) == "Elemental HERO Bubbleman")).Any() && x.Targetable(x.OppMonsters().Where(m => m.FaceUp && m.Attack)).Any(),
        async x =>
        {
            await x.Target(x.MyMonsters().Where(m => m.FaceUp && m.Attack && Scripts.NameOf(m) == "Elemental HERO Bubbleman"), "Target your Elemental HERO Bubbleman");
            await x.Target(x.OppMonsters().Where(m => m.FaceUp && m.Attack), "Target 1 face-up Attack Position monster your opponent controls");
        });
        S("Burden of the Mighty").Stats = (x, c, st) => { if (c.Controller == x.Opp) st.Add(-100 * x.D.Level(c), 0); };
        Spell("Burial from a Different Dimension", async x =>
        {
            foreach (var t in x.T.Where(t => t.Loc == Loc.Banished).ToList()) await x.D.MoveTo(t, Loc.Grave, by: x.Self);
        }, x => x.D.Players.Any(p => p.Banished.Any(c => c.Data.IsMonster && c.FaceUp)),
        async x => x.T.AddRange(await x.PickN(x.Targetable(x.D.Players.SelectMany(p => p.Banished).Where(c => c.Data.IsMonster && c.FaceUp)), 1, 3, "Return up to 3 banished monsters to the GY")));
        S("Burning Algae").On = async (x, e) => { if (e.Is(EvKind.ToGrave, x.Self)) await x.D.Gain(x.Opp, 1000, x.Self); };
        Spell("Burst Breath", async x =>
        {
            await x.D.Destroy(x.D.FaceUpMonsters.Where(m => x.Def(m) <= x.N).ToList(), x.Self);
        }, x => x.MyMonsters().Any(m => x.IsRace(m, "Dragon") && !x.D.Forbidden(Q.Tribute, m)),
        async x =>
        {
            var t = await x.Pick(x.MyMonsters().Where(m => x.IsRace(m, "Dragon") && !x.D.Forbidden(Q.Tribute, m)), "Tribute 1 Dragon");
            if (t == null) return;
            x.N = t.FaceUp ? x.Atk(t) : 0;
            await x.D.Tribute(t, x.Self);
        });
        Spell("Burst Return", async x =>
        {
            foreach (var m in x.D.FaceUpMonsters.Where(m => m.In("Elemental HERO") && Scripts.NameOf(m) != "Elemental HERO Burstinatrix").ToList()) await x.D.ToHand(m, x.Self);
        }, x => x.MyMonsters().FaceUp().Any(m => Scripts.NameOf(m) == "Elemental HERO Burstinatrix") && x.D.FaceUpMonsters.Any(m => m.In("Elemental HERO") && Scripts.NameOf(m) != "Elemental HERO Burstinatrix"));
        Spell("Burst Stream of Destruction", async x =>
        {
            await x.D.Destroy(x.OppMonsters().ToList(), x.Self);
            int me = x.Me;   // also ones Summoned later this turn
            x.D.Linger(x.D.Turn, (q, c, p) => q == Q.Attack && c != null && c.Controller == me && Scripts.NameOf(c) == "Blue-Eyes White Dragon");
        }, x => x.MyMonsters().FaceUp().Any(m => Scripts.NameOf(m) == "Blue-Eyes White Dragon"));
        {
            var s = S("Butterfly Dagger - Elma");
            s.EquipOk = (x, m) => true;
            s.Stats = (x, c, st) => { if (c == x.Self.EquippedTo) st.Add(300, 0); };
            s.On = async (x, e) => { if (e.Is(EvKind.Destroyed, x.Self) && e.From == Loc.Spell && x.Self.Loc == Loc.Grave && await x.Ask("Return Butterfly Dagger - Elma to your hand?")) await x.D.ToHand(x.Self, x.Self); };
        }
        Spell("Byroad Sacrifice", async x => await SummonPick(x, x.My.Hand.Where(c => c.Name == "Cyber Ogre"), "Special Summon Cyber Ogre"),
            x => x.Ev is { Kind: EvKind.Destroyed, Battle: true } e && x.D.LeftFieldLog.LastOrDefault(l => l.c == e.Card) is { c: not null } l && l.controller == x.Me && x.My.Hand.Any(c => c.Name == "Cyber Ogre"));
    }
}
