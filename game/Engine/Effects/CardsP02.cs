namespace PoC.Engine;

// Official cards, batch 2 (Ancient Gear Golem .. Battle Fusion).
public static partial class Scripts
{
    /// "If this card attacks, your opponent cannot activate any Spell/Trap Cards until the end of the Damage Step."
    internal static bool LocksDuringAttack(Ctx x, Q q, Card? c) => q == Q.Activate && c != null && c.Controller == x.Opp && !c.Data.IsMonster && x.D.Attacker == x.Self;

    /// Is there a subset of `lv` adding up to exactly `target`?
    internal static bool SubsetSum(IEnumerable<int> lv, int target)
    {
        if (target < 0) return false;
        var ok = new bool[target + 1]; ok[0] = true;
        foreach (var l in lv) for (int t = target; t >= l; t--) ok[t] |= ok[t - l];
        return ok[target];
    }

    /// Pick cards one by one until their Levels add up to exactly `target` (each pick must still leave a way to finish).
    internal static async Flow<List<Card>> PickLevels(Ctx x, List<Card> from, int target, string text)
    {
        var got = new List<Card>();
        int left = target;
        while (left > 0)
        {
            var c = await x.Pick(from.Except(got).Where(g => x.D.Level(g) <= left && SubsetSum(from.Except(got).Where(o => o != g).Select(o => x.D.Level(o)), left - x.D.Level(g))), $"{text} ({left} Levels to go)");
            if (c == null) break;
            got.Add(c); left -= x.D.Level(c);
        }
        return got;
    }

    // ---- Arcana Force: "When this card is Summoned: toss a coin" (Light Barrier: you choose the result instead)
    internal static bool Heads(Ctx x) => Up(x) && x.Self.Var("arcana") == 1;
    internal static bool Tails(Ctx x) => Up(x) && x.Self.Var("arcana") == 2;

    internal static async Flow ArcanaCoin(Ctx x)
    {
        bool heads;
        if (x.MyMonsters().Any() && x.D.CountActive("Light Barrier", x.Me) > 0)
            heads = await x.D.ChooseText(x.Me, $"{x.Self.Name}: Light Barrier - choose the coin result", new[] { "Heads", "Tails" }, x.Self) == 0;
        else heads = x.D.TossCoins(x.Me, 1, x.Self) == 1;
        if (x.Self.Valid()) { x.Self.Vars["arcana"] = heads ? 1 : 2; x.D.Seq++; }
        if (x.Self.Valid() && x.Self.Name == "Arcana Force VII - The Chariot" && !heads) await x.D.TakeControl(x.Self, x.Opp);   // Tails is applied at once
    }

    static Script Arcana(string name, Func<Ctx, Ev, Flow>? more = null, bool specialOnly = false)
    {
        var s = S(name);
        s.On = async (x, e) =>
        {
            if (e.Is(EvKind.Summoned, x.Self) && (!specialOnly || e.Arg is (int)SummonKind.Special)) { await ArcanaCoin(x); return; }
            if (more != null) await more(x, e);
        };
        return s;
    }

    /// Union monster: once per turn, equip this card to a monster you control or unequip it to Special Summon it
    /// (face-up Attack); one Union per monster; destroyed instead of the equipped monster by battle (and by effects).
    internal static Script Union(string name, Func<Ctx, Card, bool> ok, bool effectsToo = false)
    {
        var s = S(name);
        static bool Free(Ctx x) => x.D.Flags.GetValueOrDefault($"union{x.Self.Uid}") != x.D.Turn;
        IEnumerable<Card> Hosts(Ctx x) => x.MyMonsters().FaceUp().Where(m => m != x.Self && ok(x, m) && !x.D.EquipsOf(m).Any(e => e.Var("union") == 1));
        s.BattleShield = (x, m) => x.Self.Var("union") == 1 && x.Self.EquippedTo == m;
        if (effectsToo) s.EffectShield = (x, m) => x.Self.Var("union") == 1 && x.Self.EquippedTo == m;
        s.AddFx(f =>
        {
            f.Label = "Equip (Union)";
            f.Can = x => Up(x) && Free(x) && x.My.FreeSpellZones > 0 && x.Targetable(Hosts(x)).Any();
            f.Cost = async x => { x.D.Flags[$"union{x.Self.Uid}"] = x.D.Turn; await x.Target(Hosts(x), $"Equip {name} to which monster?"); };
            f.Run = async x =>
            {
                if (x.T0 is not { } t || !t.Valid() || !t.FaceUp || !Up(x) || x.My.FreeSpellZones == 0) return;
                x.Self.ResetFieldState();
                x.D.Place(x.Self, x.Me, Loc.Spell);
                await x.D.Equip(x.Self, t);
                x.Self.Vars["union"] = 1;
            };
        });
        s.AddFx(f =>
        {
            f.Label = "Unequip (Union)"; f.InSpellZone = true;
            f.Can = x => x.Self.Var("union") == 1 && x.Self.EquippedTo != null && Free(x) && x.D.FreeMonsterZones(x.Me) > 0;
            f.Cost = async x => x.D.Flags[$"union{x.Self.Uid}"] = x.D.Turn;
            f.Run = async x => { if (x.Self.Loc == Loc.Spell) await x.D.SpecialSummon(x.Self, x.Me, false, by: x.Self); };
        });
        return s;
    }

    static void CardsP02(CardDb db)
    {
        {
            var s = S("Ancient Gear Golem");
            s.SummonOk = (x, by, k) => false;
            s.Forbid = LocksDuringAttack;
            s.Allow = (x, q, c) => q == Q.Piercing && c == x.Self;
        }
        S("Ancient Gear Soldier").Forbid = LocksDuringAttack;
        {
            var s = S("Ancient Gear Tank");
            s.EquipOk = (x, m) => m.In("Ancient Gear");
            s.Stats = (x, c, st) => { if (c == x.Self.EquippedTo) st.Add(600, 0); };
            s.On = async (x, e) => { if (e.Is(EvKind.Destroyed, x.Self) && x.Self.Loc == Loc.Grave && e.From is Loc.Spell) await x.D.Damage(x.Opp, 600, x.Self); };
        }
        Spell("Ancient Gear Workshop", async x => { if (x.T0 is { Loc: Loc.Grave } t) await x.D.ToHand(t, x.Self); },
            x => x.Targetable(x.My.Grave.Where(c => c.Data.IsMonster && c.In("Ancient Gear"))).Any(),
            async x => await x.Target(x.My.Grave.Where(c => c.Data.IsMonster && c.In("Ancient Gear")), "Add which Ancient Gear monster to your hand?"));
        {
            var s = S("Anti-Aircraft Flower");
            s.Can = x => x.MyMonsters().Any(m => m.FaceUp && x.IsRace(m, "Insect") && !x.D.Forbidden(Q.Tribute, m));
            s.Cost = async x => { var t = await x.Pick(x.MyMonsters().Where(m => m.FaceUp && x.IsRace(m, "Insect") && !x.D.Forbidden(Q.Tribute, m)), "Tribute which Insect?"); if (t != null) await x.D.Tribute(t, x.Self); };
            s.Run = async x => await x.D.Damage(x.Opp, 800, x.Self);
        }
        {
            var s = Spell("Anti-Magic Arrows", async x =>
            {
                var d = x.D;
                d.Linger(d.Turn, (q, c, p) => q == Q.Activate && c != null && !c.Data.IsMonster);
            }, x => x.Ev is { Kind: EvKind.BattleStart });
            s.NoResponse = true;
        }
        {
            static int Valks(Ctx x) => x.MyMonsters().FaceUp().Count(m => m.In("Valkyrie"));
            var s = Spell("Apple of Enlightenment", async x =>
            {
                foreach (var t in x.T.Where(t => t.Loc == Loc.Grave).ToList()) await x.D.Banish(t, x.Self);
            }, x => Valks(x) > 0 && x.Targetable(x.Their.Grave).Any(),
            async x => x.T.AddRange(await x.PickN(x.Targetable(x.Their.Grave), 1, Valks(x), $"Banish up to {Valks(x)} cards from your opponent's GY")));
            s.On = async (x, e) =>
            {
                // This Set card left the field because of an opponent's effect: draw (Valkyries + 1).
                if (e.Is(EvKind.LeftField, x.Self) && e.Other is { } by && by.Controller == x.Opp && x.Self.Loc is Loc.Grave or Loc.Banished
                    && x.D.LeftFieldLog.LastOrDefault(l => l.c == x.Self) is { c: not null } l && !l.faceUp && l.controller == x.Self.Owner
                    && await x.Ask($"Draw {Valks(x) + 1} card(s) (Apple of Enlightenment)?"))
                    await x.D.Draw(x.Me, Valks(x) + 1);
            };
            s.Note = "The draw is applied when you say yes (no chain).";
        }
        Spell("Arcana Call", async x =>
        {
            if (x.T.Count < 2 || x.T[0] is not { } mine || !mine.Valid() || x.T[1].Loc != Loc.Grave) return;
            var gone = x.T[1];
            await x.D.Banish(gone, x.Self);
            mine.Vars["copyidx"] = gone.Data.Idx + 1; mine.Vars["copyturn"] = x.D.Turn; x.D.Seq++;
        }, x => x.Targetable(x.MyMonsters().FaceUp().Where(m => m.In("Arcana Force"))).Any() && x.D.Players.Any(p => p.Grave.Any(c => c.Data.IsMonster && c.In("Arcana Force"))),
        async x =>
        {
            await x.Target(x.MyMonsters().FaceUp().Where(m => m.In("Arcana Force")), "Select your Arcana Force monster");
            await x.Target(x.D.Players.SelectMany(p => p.Grave).Where(c => c.Data.IsMonster && c.In("Arcana Force")), "Banish which Arcana Force monster (its effect is lent)?");
        });
        {
            var s = Arcana("Arcana Force 0 - The Fool", async (x, e) =>
            {
                if (e.Kind == EvKind.Activated && e.Link is { } l && l.X.T.Contains(x.Self) && l.Card != x.Self
                    && ((Heads(x) && l.Player == x.Me) || (Tails(x) && l.Player == x.Opp)))
                    await x.D.Negate(l);
            });
            s.Forbid = (x, q, c) => c == x.Self && (q == Q.DestroyByBattle || (q == Q.ChangePos && !x.Self.Defense));
        }
        foreach (var n in new[] { "Arcana Force EX - The Dark Ruler", "Arcana Force EX - The Light Ruler" })
        {
            bool dark = n.Contains("Dark");
            var s = Arcana(n, async (x, e) =>
            {
                if (dark)
                {
                    if (e.Kind == EvKind.BattleEnd && Heads(x) && x.Self.Attacks >= 2)
                    { await x.D.ChangePosition(x.Self, true); x.Self.Vars["nochangepos"] = x.D.Turn + 2; }
                    if (e.Is(EvKind.Destroyed, x.Self) && x.D.LeftFieldLog.LastOrDefault(l => l.c == x.Self).vars?.GetValueOrDefault("arcana") == 2)
                        await x.D.Destroy(x.D.FieldCards.ToList(), x.Self);
                }
                else
                {
                    if (e.Kind == EvKind.BattleDestroyedMonster && e.Card == x.Self && Heads(x) && e.Other!.Loc == Loc.Grave && x.My.Grave.Any() && await x.Ask("Add 1 card from your GY to your hand?"))
                    { var c = await x.Pick(x.Targetable(x.My.Grave), "Add which card?"); if (c != null) await x.D.ToHand(c, x.Self); }
                    if (e.Kind == EvKind.Activated && e.Link is { } l && Tails(x) && l.X.T.Contains(x.Self) && l.Card != x.Self && (!l.Card.Data.IsMonster || !l.IsCardActivation))
                    {
                        x.Self.Vars["modatk"] = x.Self.Var("modatk") - 1000; x.D.Seq++;
                        await x.D.Negate(l);
                    }
                }
            }, specialOnly: true);
            s.SummonOk = (x, by, k) => by == x.Self;
            s.ProcCan = x => x.MyMonsters().Count() >= 3;
            s.Proc = async x =>
            {
                var l = await x.PickN(x.MyMonsters(), 3, 3, "Send 3 monsters you control to the GY");
                foreach (var m in l) await x.D.ToGrave(m, x.Self);
                await x.D.SpecialSummon(x.Self, x.Me, await x.D.ChoosePosition(x.Me, x.Self), by: x.Self);
            };
            if (dark) s.Allow = (x, q, c) => q == Q.DoubleAttack && c == x.Self && Heads(x);
        }
        Arcana("Arcana Force I - The Magician", async (x, e) =>
        {
            if (e.Kind != EvKind.Activated || e.Link is not { IsCardActivation: true } l || !l.Card.Data.IsSpell) return;
            if (Heads(x)) SetBaseAtk(x, x.Self, 2 * x.Self.Data.Atk);
            else if (Tails(x)) await x.D.Gain(x.Opp, 500, x.Self);
        });
        Arcana("Arcana Force III - The Empress", async (x, e) =>
        {
            bool oppSummon = e.Player == x.Opp && (e.Summoned(SummonKind.Normal, SummonKind.Tribute) || (e.Kind == EvKind.Set && e.Card!.Summoned == SummonKind.Set));
            if (!oppSummon) return;
            if (Heads(x) && x.My.Hand.Any(c => c.Data.IsMonster && c.In("Arcana Force") && x.D.CanSpecialSummon(c, x.Me, by: x.Self)) && await x.Ask("Special Summon an Arcana Force monster from your hand?"))
                await SummonPick(x, x.My.Hand.Where(c => c.Data.IsMonster && c.In("Arcana Force")), "Special Summon which?");
            else if (Tails(x) && x.My.Hand.Count > 0)
                await x.D.ToGrave((await x.Pick(x.My.Hand, "Send 1 card from your hand to the GY"))!, x.Self);
        });
        Arcana("Arcana Force IV - The Emperor").Stats = (x, c, st) =>
        {
            if (c.Controller == x.Me && c.In("Arcana Force")) st.Add(Heads(x) ? 500 : Tails(x) ? -500 : 0, 0);
        };
        {
            var s = Arcana("Arcana Force VI - The Lovers");
            s.TributeWeight = (x, t, forCard) => t == x.Self && Heads(x) && forCard.In("Arcana Force") ? 2 : 1;
            s.Forbid = (x, q, c) => Tails(x) && q is Q.NormalSummon or Q.SetMonster && c != null && c.Controller == x.Me && c.In("Arcana Force") && x.D.TributesFor(c) > 0;
        }
        Arcana("Arcana Force VII - The Chariot", async (x, e) =>
        {
            if (e.Kind == EvKind.BattleDestroyedMonster && e.Card == x.Self && Heads(x) && e.Other is { Loc: Loc.Grave } d && d.Owner == x.Opp
                && x.D.CanSpecialSummon(d, x.Me, by: x.Self) && await x.Ask($"Special Summon {d.Name} to your side of the field?"))
                await x.D.SpecialSummon(d, x.Me, await x.D.ChoosePosition(x.Me, d), by: x.Self);
        });
        {
            var s = S("Arcana Force XII - The Hangman");
            s.On = async (x, e) =>
            {
                if (!e.Is(EvKind.Summoned, x.Self)) return;
                await ArcanaCoin(x);
                var side = x.Self.Var("arcana") == 1 ? x.Me : x.Opp;
                var t = await x.Pick(x.D.P(side).MonstersOnField, side == x.Me ? "Destroy 1 monster you control" : "Destroy 1 monster your opponent controls");
                if (t == null) return;
                int atk = t.Data.Atk;
                await x.D.Destroy(t, x.Self);
                if (t.Loc != Loc.Monster) await x.D.Damage(side, atk, x.Self);
            };
            s.AddFx(f =>
            {
                f.Label = "Summon an Arcana Force"; f.FromHand = true; f.Speed = 2; f.AnyTurn = true;
                f.Can = x => x.Ev is { Kind: EvKind.Activated } && Hopt(x) && x.My.Hand.Any(c => c.Data.IsMonster && c.In("Arcana Force") && x.D.CanSpecialSummon(c, x.Me, by: x.Self));
                f.Cost = async x => { UseHopt(x); x.D.Reveal(x.Opp, x.Self); };
                f.Run = async x => await SummonPick(x, x.My.Hand.Where(c => c.Data.IsMonster && c.In("Arcana Force")), "Special Summon which Arcana Force (Defense)?", defense: true);
            });
            s.Note = "Its coin is tossed when it is Summoned and applied at once (Heads: your monster, Tails: theirs).";
        }
        {
            var s = Arcana("Arcana Force XIV - Temperance");
            s.BattleDamage = (x, src, to, dmg) => (Heads(x) && to == x.Me) || (Tails(x) && to == x.Opp) ? dmg / 2 : dmg;
            s.AddFx(f =>
            {
                f.Label = "No battle damage"; f.FromHand = true; f.Speed = 2; f.AnyTurn = true;
                f.Can = x => x.Ev is { Kind: EvKind.DamageCalc } && WouldTakeBattleDamage(x.D, x.Me);
                f.Cost = async x => await x.D.Discard(x.Me, new[] { x.Self }, x.Self);
                f.Run = async x => x.D.BattleFlags.Add($"nodamage{x.Me}");
            });
        }
        {
            var s = Arcana("Arcana Force XV - The Fiend", async (x, e) =>
            {
                if (e.Kind != EvKind.AttackDeclared || (e.Card != x.Self && e.Other != x.Self)) return;
                if (Heads(x) && x.AllMonsters().Any() && await x.Ask("Destroy 1 monster on the field (500 damage to its controller)?"))
                {
                    var t = await x.Pick(x.Targetable(x.AllMonsters()), "Destroy which monster?");
                    if (t == null) return;
                    int who = t.Controller;
                    await x.D.Destroy(t, x.Self);
                    if (t.Loc != Loc.Monster) await x.D.Damage(who, 500, x.Self);
                }
                else if (Tails(x)) await x.D.Destroy(x.AllMonsters().ToList(), x.Self);
            });
            s.AddFx(f =>
            {
                f.Label = "Add Light Barrier"; f.FromHand = true;
                f.Can = x => x.My.Deck.Concat(x.My.Grave).Any(c => c.Name == "Light Barrier");
                f.Cost = async x => await x.D.Discard(x.Me, new[] { x.Self }, x.Self);
                f.Run = async x =>
                {
                    var c = await x.Pick(x.My.Deck.Concat(x.My.Grave).Where(c => c.Name == "Light Barrier"), "Add which Light Barrier?");
                    bool deck = c?.Loc == Loc.Deck;
                    if (c != null) await x.D.ToHand(c, x.Self);
                    if (deck) x.D.Shuffle(x.Me);
                };
            });
        }
        Arcana("Arcana Force XVIII - The Moon", async (x, e) =>
        {
            if (e.Kind == EvKind.Standby && e.Player == x.Me && Heads(x) && x.D.FreeMonsterZones(x.Me) > 0 && await x.Ask("Special Summon a Moon Token?"))
                await x.D.SpecialSummon(x.D.MakeToken("Moon Token", x.Me, 0, 0, "LIGHT", "Fairy", 1), x.Me, await x.D.ChoosePosition(x.Me, x.Self), by: x.Self);
            if (e.Kind == EvKind.EndPhase && e.Player == x.Me && Tails(x) && x.MyMonsters().Any())
            {
                var t = await x.Pick(x.MyMonsters(), "Give control of 1 monster you control to your opponent");
                if (t != null) await x.D.TakeControl(t, x.Opp);
            }
        });
        Arcana("Arcana Force XXI - The World", async (x, e) =>
        {
            if (e.Kind == EvKind.EndPhase && e.Player == x.Me && Heads(x) && x.MyMonsters().Count() >= 2 && await x.Ask("Send 2 monsters you control to the GY to skip your opponent's next turn?"))
            {
                foreach (var m in await x.PickN(x.MyMonsters(), 2, 2, "Send 2 monsters to the GY")) await x.D.ToGrave(m, x.Self);
                x.D.Flags[$"skipturn{x.Opp}"] = 1;
            }
            if (e.Kind == EvKind.DrawPhase && e.Player == x.Opp && Tails(x) && x.Their.Grave.Count > 0) await x.D.ToHand(x.Their.Grave[^1], x.Self);
        });
        {
            var s = S("Archfiend's Oath");
            s.AddFx(f =>
            {
                f.Label = "Pay 500, declare a card"; f.OncePerTurn = true;
                f.Can = x => x.My.Lp > 500 && x.My.Deck.Count > 0;
                f.Cost = async x =>
                {
                    x.D.Pay(x.Me, 500);
                    var names = x.My.Deck.Select(c => c.Data).GroupBy(d => d.Name).Select(g => g.First()).OrderBy(d => d.Name).ToList();
                    int i = await x.D.ChooseText(x.Me, "Declare 1 card name", names.Select(d => d.Name).Append("(a card not in your Deck)").ToList(), x.Self);
                    x.N = i < names.Count ? names[i].Idx : -1;
                };
                f.Run = async x =>
                {
                    if (x.My.Deck.Count == 0) return;
                    var top = x.My.Deck[0];
                    x.D.Reveal(x.Me, top); x.D.Reveal(x.Opp, top);
                    if (x.N >= 0 && top.Name == x.D.Db.Get(x.N).Name) await x.D.ToHand(top, x.Self); else await x.D.ToGrave(top, x.Self);
                };
            });
            s.Note = "Declaring offers the card names in your Deck (any other name could never be excavated).";
        }
        {
            var s = S("Archlord Zerato");
            static bool Sanctuary(Ctx x, bool mine) => x.D.Players.Where(p => !mine || p.Index == x.Me).Any(p => p.FieldZone is { FaceUp: true } f && Scripts.NameOf(f) == "The Sanctuary in the Sky");
            s.SummonOk = (x, by, k) => by == x.Self;
            s.ProcCan = x => Sanctuary(x, false) && x.MyMonsters().Any(m => m.FaceUp && m.Name == "Warrior of Zera");
            s.Proc = async x =>
            {
                var z = await x.Pick(x.MyMonsters().Where(m => m.FaceUp && m.Name == "Warrior of Zera"), "Tribute Warrior of Zera");
                if (z == null) return;
                await x.D.Tribute(z, x.Self);
                await x.D.SpecialSummon(x.Self, x.Me, await x.D.ChoosePosition(x.Me, x.Self), by: x.Self);
            };
            s.Can = x => Sanctuary(x, true) && x.My.Hand.Any(c => c.Data.IsMonster && c.Data.Attribute == "LIGHT");
            s.Cost = async x => await x.D.Discard(x.Me, await x.PickN(x.My.Hand.Where(c => c.Data.IsMonster && c.Data.Attribute == "LIGHT"), 1, 1, "Discard 1 LIGHT monster"), x.Self);
            s.Run = async x => { if (Sanctuary(x, true)) await x.D.Destroy(x.OppMonsters().ToList(), x.Self); };
        }
        {
            var s = S("Armed Changer");
            s.EquipOk = (x, m) => true;
            s.Can = x => x.My.Hand.Any(c => c != x.Self && c.Data.Is("Equip") && c.Data.IsSpell) && x.D.EquipTargets(x.Self).Any();
            s.Cost = async x =>
            {
                await x.D.ToGrave((await x.Pick(x.My.Hand.Where(c => c != x.Self && c.Data.Is("Equip") && c.Data.IsSpell), "Send 1 Equip Spell from your hand to the GY"))!, x.Self);
                var t = await x.Pick(x.D.EquipTargets(x.Self), "Equip Armed Changer to which monster?");
                if (t != null) x.T.Add(t);
            };
            s.On = async (x, e) =>
            {
                if (e.Kind != EvKind.BattleDestroyedMonster || x.Self.EquippedTo is not { } m || e.Card != m) return;
                int atk = x.Atk(m);
                var l = x.My.Grave.Where(c => c.Data.IsMonster && c.Data.Atk <= atk).ToList();
                if (l.Count > 0 && await x.Ask($"Add a monster with {atk} or less ATK from your GY to your hand?"))
                { var c = await x.Pick(l, "Add which monster?"); if (c != null) await x.D.ToHand(c, x.Self); }
            };
        }
        {
            var s = S("Armed Dragon LV10");
            s.SummonOk = (x, by, k) => by == x.Self;
            s.ProcCan = x => x.MyMonsters().Any(m => m.FaceUp && m.Name == "Armed Dragon LV7" && !x.D.Forbidden(Q.Tribute, m));
            s.Proc = async x =>
            {
                var t = await x.Pick(x.MyMonsters().Where(m => m.FaceUp && m.Name == "Armed Dragon LV7"), "Tribute Armed Dragon LV7");
                if (t == null) return;
                await x.D.Tribute(t, x.Self);
                await x.D.SpecialSummon(x.Self, x.Me, await x.D.ChoosePosition(x.Me, x.Self), by: x.Self);
            };
            s.Can = x => Up(x) && x.My.Hand.Count > 0 && x.OppMonsters().FaceUp().Any();   // nothing to destroy = can't activate
            s.Cost = async x => await x.D.ToGrave((await x.Pick(x.My.Hand, "Send 1 card from your hand to the GY"))!, x.Self);
            s.Run = async x => await x.D.Destroy(x.OppMonsters().FaceUp().ToList(), x.Self);
        }
        {
            var s = S("Armed Dragon LV10 White");
            static List<Card> Dragons(Ctx x) => x.MyMonsters().Where(m => m.FaceUp && m.In("Armed Dragon")).Concat(x.My.Grave.Where(c => c.Data.IsMonster && c.In("Armed Dragon"))).ToList();
            s.SummonOk = (x, by, k) => by == x.Self;
            s.Forbid = (x, q, c) => q == Q.TakeEffectDamage && Up(x) && x.D.QueryPlayer == x.Me;
            s.AddFx(f =>
            {
                f.Label = "Special Summon"; f.FromHand = true;
                f.Can = x => Hopt(x) && x.D.FreeMonsterZones(x.Me) + x.MyMonsters().Count(m => m.In("Armed Dragon")) > 0 && SubsetSum(Dragons(x).Select(c => x.D.Level(c)), 10);
                f.Cost = async x =>
                {
                    UseHopt(x);
                    foreach (var c in await PickLevels(x, Dragons(x), 10, "Banish Armed Dragon monsters (field / GY)")) await x.D.Banish(c, x.Self);
                };
                f.Run = async x =>
                {
                    if (x.Self.Loc != Loc.Hand || !await x.D.SpecialSummon(x.Self, x.Me, await x.D.ChoosePosition(x.Me, x.Self), by: x.Self)) return;
                    if (x.My.Deck.Any(c => c.Name == "White Veil") && await x.Ask("Add White Veil from your Deck to your hand?")) await x.Search(c => c.Name == "White Veil", "Add White Veil");
                };
            });
            s.On = async (x, e) =>
            {
                if (e.Kind == EvKind.DamageStart && e.Card == x.Self && Up(x) && x.D.FieldCards.Any() && await x.Ask("Destroy 1 card on the field (Armed Dragon LV10 White)?"))
                { var t = await x.Pick(x.D.FieldCards, "Destroy which card?"); if (t != null) await x.D.Destroy(t, x.Self); }
            };
        }
        MyStandby("Armed Dragon LV3", async x =>
        {
            var l = x.My.Hand.Concat(x.My.Deck).Where(c => c.Name == "Armed Dragon LV5").ToList();
            if (l.Count == 0 || !await x.Ask("Send Armed Dragon LV3 to the GY to Special Summon Armed Dragon LV5?")) return;
            await x.D.ToGrave(x.Self, x.Self);
            await SummonPick(x, l, "Special Summon Armed Dragon LV5");
            x.D.Shuffle(x.Me);
        });
        {
            var s = S("Armed Dragon LV5");
            s.Can = x => Up(x) && x.My.Hand.Any(h => h.Data.IsMonster && x.Targetable(x.OppMonsters()).Any(m => x.Atk(m) <= h.Data.Atk));
            s.Cost = async x =>
            {
                var h = await x.Pick(x.My.Hand.Where(h => h.Data.IsMonster && x.Targetable(x.OppMonsters()).Any(m => x.Atk(m) <= h.Data.Atk)), "Send 1 monster from your hand to the GY");
                if (h == null) return;
                await x.D.ToGrave(h, x.Self);
                x.N = h.Data.Atk;
                await x.Target(x.OppMonsters().Where(m => x.Atk(m) <= x.N), $"Destroy which monster (ATK {x.N} or less)?");
            };
            s.Run = async x => { if (x.T0 is { } t && t.Valid() && x.Atk(t) <= x.N) await x.D.Destroy(t, x.Self); };
            s.On = async (x, e) =>
            {
                if (e.Kind == EvKind.BattleDestroyedMonster && e.Card == x.Self) x.Self.Vars["killturn"] = x.D.Turn;
                if (e.Kind == EvKind.EndPhase && Up(x) && x.Self.Var("killturn") == x.D.Turn)
                {
                    var l = x.My.Hand.Concat(x.My.Deck).Where(c => c.Name == "Armed Dragon LV7").ToList();
                    if (l.Count == 0 || !await x.Ask("Send Armed Dragon LV5 to the GY to Special Summon Armed Dragon LV7?")) return;
                    await x.D.ToGrave(x.Self, x.Self);
                    await SummonPick(x, l, "Special Summon Armed Dragon LV7");
                    x.D.Shuffle(x.Me);
                }
            };
        }
        {
            var s = S("Armed Dragon LV7");
            s.SummonOk = (x, by, k) => by?.Name == "Armed Dragon LV5";
            s.Can = x => Up(x) && x.My.Hand.Any(h => h.Data.IsMonster) && x.OppMonsters().Any();
            s.Cost = async x =>
            {
                var h = await x.Pick(x.My.Hand.Where(h => h.Data.IsMonster), "Send 1 monster from your hand to the GY");
                if (h != null) { await x.D.ToGrave(h, x.Self); x.N = h.Data.Atk; }
            };
            s.Run = async x => await x.D.Destroy(x.OppMonsters().Where(m => (m.FaceUp ? x.Atk(m) : m.Data.Atk) <= x.N).ToList(), x.Self);   // face-down ones too (their printed ATK)
        }
        S("Armed Samurai - Ben Kei").ExtraAttacks = x => x.D.EquipsOf(x.Self).Count();
        {
            var s = Union("Armor Breaker", (x, m) => x.IsRace(m, "Warrior"));
            s.On = async (x, e) =>
            {
                if (e.Kind == EvKind.BattleDamage && x.Self.Var("union") == 1 && e.Card == x.Self.EquippedTo && e.Player == x.Opp && x.D.FieldCards.Any())
                { var t = await x.Pick(x.D.FieldCards, "Destroy 1 card on the field (Armor Breaker)"); if (t != null) await x.D.Destroy(t, x.Self); }
            };
        }
        {
            var s = Union("Armored Cybern", (x, m) => Scripts.NameOf(m) == "Cyber Dragon" || (m.Data.IsFusion && m.Data.Desc.Contains("\"Cyber Dragon\"")), effectsToo: true);
            s.AddFx(f =>
            {
                f.Label = "Destroy a monster"; f.InSpellZone = true; f.OncePerTurn = true;
                f.Can = x => x.Self.Var("union") == 1 && x.Self.EquippedTo is { } m && x.Atk(m) >= 1000 && x.Targetable(x.D.FaceUpMonsters).Any();
                f.Cost = async x => await x.Target(x.D.FaceUpMonsters, "Destroy which face-up monster?");
                f.Run = async x =>
                {
                    if (x.Self.EquippedTo is not { } m || x.Atk(m) < 1000) return;
                    m.Vars["modatk"] = m.Var("modatk") - 1000; x.D.Seq++;
                    if (x.T0 is { } t && t.Valid()) await x.D.Destroy(t, x.Self);
                };
            });
        }
        {
            var s = S("Assault Armor");
            s.EquipOk = (x, m) => m.Controller == x.Me && x.IsRace(m, "Warrior") && x.MyMonsters().Count() == 1;
            s.Stats = (x, c, st) => { if (c == x.Self.EquippedTo) st.Add(300, 0); };
            s.AddFx(f =>
            {
                f.Label = "Attack twice this turn";
                f.Can = x => x.D.Phase == Phase.Main1 && x.Self.EquippedTo != null;
                f.Cost = async x => { x.T.Add(x.Self.EquippedTo!); await x.D.ToGrave(x.Self, x.Self); };
                f.Run = async x => { if (x.T0 is { } m && m.Valid()) { m.Vars["maxatk"] = 2; m.Vars["maxatkturn"] = x.D.Turn; } };
            });
        }
        {
            var s = S("Asura Priest");
            s.SummonOk = (x, by, k) => false;
            s.AttackEach = true;
            s.On = async (x, e) =>
            {
                if (e.Is(EvKind.Flipped, x.Self)) x.Self.Vars["flipturn"] = x.D.Turn;
                if (e.Kind == EvKind.EndPhase && Up(x) && ((x.Self.SummonTurn == x.D.Turn && x.Self.Summoned is SummonKind.Normal or SummonKind.Tribute) || x.Self.Var("flipturn") == x.D.Turn))
                    await x.D.ToHand(x.Self, x.Self);
            };
        }
        S("Atomic Firefly").On = async (x, e) =>
        {
            // Face-up when the battle began (not a Set card that was attacked), destroyed by that battle.
            if (e.Is(EvKind.Destroyed, x.Self) && e.Battle && e.Other is { } by && !(x.D.AttackTarget == x.Self && x.D.TargetWasSet))
                await x.D.Damage(by.Controller, 1000, x.Self);
        };
        Spell("Attack Guidance Armor", async x =>
        {
            if (x.Ev?.Card is not { } a || !a.Valid()) return;
            if (x.N == 0) { await x.D.Destroy(a, x.Self); return; }
            if (x.T0 is { } t && t.Valid() && x.D.Attacker == a)
            {
                x.D.AttackTarget = t;
                if (t.Controller == a.Controller) x.D.BattleFlags.Add("redirectself");
            }
        }, x => x.Ev is { Kind: EvKind.AttackDeclared } && Hopt(x),
        async x =>
        {
            UseHopt(x);
            var a = x.Ev!.Card!;
            var others = x.AllMonsters().Where(m => m != a).ToList();
            x.N = others.Count == 0 ? 0 : await x.D.ChooseText(x.Me, "Attack Guidance Armor", new[] { "Destroy the attacking monster", "Change the attack target" }, x.Self);
            if (x.N == 1) await x.Target(others, "Which monster becomes the attack target?");
        });
        Spell("Attack Reflector Unit", async x =>
        {
            await SummonPick(x, x.My.Hand.Concat(x.My.Deck).Where(c => c.Name == "Cyber Barrier Dragon"), "Special Summon Cyber Barrier Dragon");
            x.D.Shuffle(x.Me);
        }, x => x.MyMonsters().Any(m => Scripts.NameOf(m) == "Cyber Dragon" && !x.D.Forbidden(Q.Tribute, m)) && x.My.Hand.Concat(x.My.Deck).Any(c => c.Name == "Cyber Barrier Dragon"),
        async x => { var t = await x.Pick(x.MyMonsters().Where(m => Scripts.NameOf(m) == "Cyber Dragon"), "Tribute Cyber Dragon"); if (t != null) await x.D.Tribute(t, x.Self); });
        Spell("Ayers Rock Sunrise", async x =>
        {
            if (x.T0 is not { Loc: Loc.Grave } t || !await x.D.SpecialSummon(t, x.Me, await x.D.ChoosePosition(x.Me, t), by: x.Self)) return;
            int n = x.My.Grave.Count(c => c.Data.IsMonster && c.Data.Race is "Beast" or "Plant" or "Winged Beast");
            foreach (var m in x.OppMonsters().FaceUp().ToList()) x.D.Boost(m, -200 * n);
        }, x => Hopt(x) && x.D.FreeMonsterZones(x.Me) > 0 && x.Targetable(x.My.Grave.Where(c => c.Data.IsMonster && c.Data.Race == "Beast")).Any(),
        async x => { UseHopt(x); await x.Target(x.My.Grave.Where(c => c.Data.IsMonster && c.Data.Race == "Beast"), "Special Summon which Beast?"); });
        foreach (var n in new[] { "B.E.S. Crystal Core", "B.E.S. Tetran" })
        {
            var s = S(n);
            s.Forbid = (x, q, c) => q == Q.DestroyByBattle && c == x.Self;
            s.On = async (x, e) =>
            {
                if (e.Is(EvKind.Summoned, x.Self) && e.Summoned(SummonKind.Normal, SummonKind.Tribute)) AddCounter(x, x.Self, 3);
                if (e.Kind == EvKind.AfterDamage && (e.Card == x.Self || e.Other == x.Self) && Up(x))
                {
                    if (x.Self.Counters() > 0) AddCounter(x, x.Self, -1); else await x.D.Destroy(x.Self, x.Self);
                }
            };
            s.OncePerTurn = true;
            if (n.Contains("Core"))
            {
                s.Can = x => Up(x) && x.OppMonsters().Any(m => m.FaceUp && m.Attack);
                s.Run = async x => { var t = await x.Pick(x.OppMonsters().Where(m => m.FaceUp && m.Attack), "Change which monster to Defense Position?"); if (t != null) await x.D.ChangePosition(t, true); };
            }
            else
            {
                s.Can = x => Up(x) && x.Self.Counters() > 0 && x.AllSpells().Any();
                s.Cost = async x => AddCounter(x, x.Self, -1);
                s.Run = async x => { var t = await x.Pick(x.AllSpells(), "Destroy which Spell/Trap?"); if (t != null) await x.D.Destroy(t, x.Self); };
            }
        }
        S("Babycerasaurus").On = async (x, e) =>
        {
            if (e.Is(EvKind.Destroyed, x.Self) && !e.Battle && e.Other != null && x.Self.Loc == Loc.Grave)
            {
                await SummonPick(x, x.My.Deck.Where(c => c.Data.IsMonster && c.Data.Race == "Dinosaur" && c.Data.Level <= 4), "Special Summon a Level 4 or lower Dinosaur");
                x.D.Shuffle(x.Me);
            }
        };
        S("Backfire").On = async (x, e) =>
        {
            if (e.Kind == EvKind.Destroyed && e.Card!.Loc == Loc.Grave && x.Self.OnField && x.Self.FaceUp && e.From == Loc.Monster
                && x.D.LeftFieldLog.LastOrDefault(l => l.c == e.Card) is { c: not null } l && l.controller == x.Me && l.faceUp && e.Card.Data.Attribute == "FIRE")
                await x.D.Damage(x.Opp, 500, x.Self);
        };
        S("Bad Reaction to Simochi").Allow = (x, q, c) => q == Q.GainIsDamage && x.D.QueryPlayer == x.Opp;
        {
            var s = MyStandby("Balloon Lizard", async x => AddCounter(x, x.Self));
            var standby = s.On!;
            s.On = async (x, e) =>
            {
                await standby(x, e);
                if (e.Is(EvKind.Destroyed, x.Self) && e.Other is { } by && x.D.LeftFieldLog.LastOrDefault(l => l.c == x.Self).vars is { } v && v.GetValueOrDefault("ctr:") is > 0 and var n)
                    await x.D.Damage(by.Controller, 400 * n, x.Self);
            };
        }
        Spell("Battle Fusion", async x =>
        {
            var e = x.Ev!;
            var mine = new[] { e.Card, e.Other }.FirstOrDefault(m => m != null && m.Controller == x.Me && m.Data.IsFusion);
            var theirs = new[] { e.Card, e.Other }.FirstOrDefault(m => m != null && m.Controller == x.Opp);
            if (mine == null || theirs == null || !mine.Valid() || !mine.FaceUp || !theirs.Valid()) return;
            int gain = x.Atk(theirs);
            x.D.Boost(mine, gain);
            x.D.UntilBattleEnds(() => mine.TempAtk -= gain);
        }, x => Hopt(x) && x.Ev is { Kind: EvKind.AttackDeclared } e && e.Other != null
            && new[] { e.Card, e.Other }.Any(m => m!.Controller == x.Me && m.FaceUp && m.Data.IsFusion) && new[] { e.Card, e.Other }.Any(m => m!.Controller == x.Opp),
        async x => UseHopt(x));
    }
}
