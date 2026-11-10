namespace PoC.Engine;

// Official cards, batch 27 (White Veil .. Zombie Master).
public static partial class Scripts
{
    /// "Must first be Special Summoned (from your Extra Deck) by banishing the above cards you control" (X/Y/Z Cannons).
    static void BanishContact(Script s, params string[] mats)
    {
        s.SummonOk = (x, by, k) => by == x.Self && x.Self.Loc != Loc.Grave;
        s.ProcFromDeck = true;
        s.ProcCan = x => x.Self.Loc == Loc.Extra && mats.All(n => x.MyMonsters().Any(m => Scripts.NameOf(m) == n));
        s.Proc = async x =>
        {
            foreach (var n in mats) await x.D.Banish(x.MyMonsters().First(m => Scripts.NameOf(m) == n), x.Self);
            await x.D.SpecialSummon(x.Self, x.Me, await x.D.ChoosePosition(x.Me, x.Self), by: x.Self);
        };
    }

    /// Yubel line: indestructible by battle, no battle damage to you, burns an attacker before damage calculation.
    static void YubelBody(Script s)
    {
        s.Forbid = (x, q, c) => q == Q.DestroyByBattle && c == x.Self;
        s.Allow = (x, q, c) => q == Q.NoBattleDamageToController && c == x.Self;
    }

    static void CardsP27(CardDb db)
    {
        {
            var s = Spell("White Veil", async x => { if (x.T0 is { } t && t.Valid() && t.FaceUp) await x.D.Equip(x.Self, t); },
                x => x.Targetable(x.D.FaceUpMonsters).Any(), async x => await x.Target(x.D.FaceUpMonsters, "Equip to which monster?"));
            s.Forbid = (x, q, c) => x.Self.OnField && x.Self.EquippedTo is { } m && x.D.Phase == Phase.Battle && (x.D.Attacker == m || x.D.AttackTarget == m) && c != null
                && !c.Data.IsMonster && c.Controller == x.Opp && (q == Q.Activate || (q == Q.Negate && c.FaceUp && c.OnField));
            s.On = async (x, e) =>
            {
                if (e.Kind == EvKind.BattleDestroyedMonster && x.Self.OnField && e.Card == x.Self.EquippedTo && e.Other!.Owner == x.Opp && x.Spells(x.Opp).Any() && await x.Ask("Destroy all Spell/Trap Cards your opponent controls?"))
                    await x.D.Destroy(x.Spells(x.Opp).ToList(), x.Self);
                if (e.Kind == EvKind.LeftField && e.Card == x.Self && x.D.LeftFieldLog.LastOrDefault(l => l.c == x.Self) is { c: not null } lf && lf.faceUp)
                    x.D.Pay(lf.controller, 3000, x.Self);
            };
            s.Note = "Its card text in the data is cut short: when it leaves the field face-up, its controller loses 3000 LP.";
        }
        {
            var s = Spell("Wicked Canon", async x => { x.Self.Vars["stay"] = 1; x.Self.Vars["canonturn"] = x.D.Turn; });
            s.On = async (x, e) => { if (e.Kind == EvKind.AttackDeclared && e.Player == x.Opp && x.Self.OnField && x.Self.FaceUp) AddCounter(x, x.Self, 1, "doctrine"); };
            s.AddFx(f =>
            {
                f.Label = "Add a DARK Fiend, discard a Fiend"; f.InSpellZone = true;
                f.Can = x => x.Self.FaceUp && Hopt(x, "1") && x.Self.Var("canonturn") == x.D.Turn && x.D.TurnPlayer == x.Me && x.My.Deck.Any(c => c.Data.IsMonster && c.Data.Attribute == "DARK" && c.Data.Race == "Fiend");
                f.Cost = async x => UseHopt(x, "1");
                f.Run = async x =>
                {
                    await x.Search(c => c.Data.IsMonster && c.Data.Attribute == "DARK" && c.Data.Race == "Fiend", "Add which DARK Fiend?");
                    var l = x.My.Hand.Where(c => c.Data.IsMonster && c.Data.Race == "Fiend").ToList();
                    if (l.Count > 0) await x.D.Discard(x.Me, await x.PickN(l, 1, 1, "Discard 1 Fiend"), x.Self);
                };
            });
            s.AddFx(f =>
            {
                f.Label = "Send with 4 counters: Special Summon a Fiend"; f.InSpellZone = true;
                f.Can = x => x.Self.FaceUp && Hopt(x, "2") && x.Self.Counters("doctrine") >= 4 && x.My.Deck.Any(c => c.Data.IsMonster && c.Data.Race == "Fiend" && x.D.CanSpecialSummon(c, x.Me, by: x.Self));
                f.Cost = async x => { UseHopt(x, "2"); await x.D.ToGrave(x.Self, x.Self); };
                f.Run = async x =>
                {
                    await SummonPick(x, x.My.Deck.Where(c => c.Data.IsMonster && c.Data.Race == "Fiend"), "Special Summon which Fiend?");
                    if (x.My.Deck.Any(c => c.Name == "Super Polymerization") && await x.Ask("Add Super Polymerization from your Deck?")) await x.Search(c => c.Name == "Super Polymerization", "Add Super Polymerization");
                    else x.D.Shuffle(x.Me);
                };
            });
            s.Note = "Its card text in the data is cut short: the second effect can then add Super Polymerization.";
        }
        {
            var s = Spell("Wicked-Breaking Flamberge - Baou", async x => { if (x.T0 is { } t && t.Valid() && t.FaceUp) await x.D.Equip(x.Self, t); },
                x => x.My.Hand.Any(c => c != x.Self) && x.Targetable(x.D.FaceUpMonsters).Any(),
                async x => { await x.D.ToGrave((await x.PickN(x.My.Hand.Where(c => c != x.Self), 1, 1, "Send 1 card from your hand to the GY"))[0], x.Self); await x.Target(x.D.FaceUpMonsters, "Equip to which monster?"); });
            s.Stats = (x, c, st) => { if (c == x.Self.EquippedTo) st.Add(500, 0); };
            s.On = async (x, e) =>
            {
                if (e.Kind == EvKind.BattleDestroyedMonster && x.Self.OnField && e.Card == x.Self.EquippedTo && e.Other!.Controller != x.Self.EquippedTo!.Controller)
                { var d = x.D; var o = e.Other; d.Flags[$"neg{o.Uid}"] = 1; d.Later(d.Turn, Phase.End, async () => d.Flags.Remove($"neg{o.Uid}")); }
            };
        }
        Spell("Wild Fire", async x =>
        {
            var b = x.My.FieldCards.FirstOrDefault(c => c.FaceUp && c.In("Blaze Accelerator"));
            if (b == null) return;
            await x.D.Destroy(b, x.Self);
            await x.D.Destroy(x.D.AllMonsters.ToList(), x.Self);
            if (x.D.FreeMonsterZones(x.Me) > 0) { var t = x.D.MakeToken("Wild Fire Token", x.Me, 1000, 1000, "FIRE", "Pyro", 3); await x.D.SpecialSummon(t, x.Me, false, by: x.Self); }
            var d = x.D; int me = x.Me; d.Linger(d.Turn, (q, c, p) => q == Q.Attack && c != null && c.Controller == me);
        }, x => x.My.Lp > 500 && x.My.FieldCards.Any(c => c.FaceUp && c.In("Blaze Accelerator")), async x => x.D.Pay(x.Me, 500, x.Self));
        Spell("Wild Nature's Release", async x =>
        {
            if (x.T0 is not { Loc: Loc.Monster, FaceUp: true } t) return;
            x.D.Boost(t, x.Def(t));
            var d = x.D; d.Later(d.Turn, Phase.End, async () => { if (t.Loc == Loc.Monster) await d.Destroy(t); });
        }, x => x.Targetable(x.D.FaceUpMonsters.Where(m => x.D.Race(m) is "Beast" or "Beast-Warrior")).Any(),
        async x => await x.Target(x.D.FaceUpMonsters.Where(m => x.D.Race(m) is "Beast" or "Beast-Warrior"), "Target which Beast or Beast-Warrior?"));
        Spell("Wing Requital", async x => await x.D.Draw(x.Me, 2),
            x => Hopt(x) && x.My.Lp > 600 && x.MyMonsters().Any() && x.MyMonsters().All(m => m.FaceUp && x.IsRace(m, "Winged Beast")) && x.MyMonsters().Select(m => m.Data.Name).Distinct().Count() >= 2,
            async x => { UseHopt(x); x.D.Pay(x.Me, 600, x.Self); });
        S("Winged Kuriboh").On = async (x, e) => { if (e.Is(EvKind.Destroyed, x.Self) && x.Self.Loc == Loc.Grave && e.From == Loc.Monster) x.D.TurnFlags.Add($"nobattledamage{x.Me}"); };
        {
            var s = S("Winged Kuriboh LV10");
            s.SummonOk = (x, by, k) => by?.Name == "Transcendent Wings";
            s.Speed = 2; s.AnyTurn = true;
            s.Can = x => Up(x) && x.D.TurnPlayer == x.Opp && x.D.Phase == Phase.Battle && x.OppMonsters().Any(m => m.Attack) && !x.D.Forbidden(Q.Tribute, x.Self);
            s.Cost = async x => await x.D.Tribute(x.Self, x.Self);
            s.Run = async x =>
            {
                var l = x.OppMonsters().Where(m => m.Attack).ToList();
                await x.D.Destroy(l, x.Self);
                int dmg = l.Where(m => !m.OnField).Sum(m => Math.Max(0, (m.AsMonster ?? m.Data).Atk));
                if (dmg > 0) await x.D.Damage(x.Opp, dmg, x.Self);
            };
        }
        {
            var s = Spell("Wonder Garage", async x => { }, x => false);
            s.On = async (x, e) =>
            {
                if (!e.Is(EvKind.Destroyed, x.Self) || x.Self.Loc != Loc.Grave || x.D.LeftFieldLog.LastOrDefault(l => l.c == x.Self) is not { c: not null } l || l.faceUp) return;
                var c = x.My.Hand.Where(c => c.Data.IsMonster && c.Data.Race == "Machine" && c.Data.Level <= 4 && c.Name.Contains("roid") && x.D.CanSpecialSummon(c, x.Me, by: x.Self)).ToList();
                if (c.Count > 0) await SummonPick(x, c, "Special Summon a \"roid\" monster from your hand?", optional: true);
            };
            s.Note = "It has no activation: it works when your Set copy is destroyed.";
        }
        OnBattleDeath("Wroughtweiler", async x =>
        {
            var h = x.Targetable(x.My.Grave.Where(c => c.In("Elemental HERO") && c != x.Self)).ToList();
            var p = x.Targetable(x.My.Grave.Where(c => c.Name == "Polymerization")).ToList();
            if (h.Count == 0 || p.Count == 0) return;
            var a = await x.Pick(h, "Add which Elemental HERO card?");
            var b = await x.Pick(p, "Add which Polymerization?");
            if (a != null) await x.D.ToHand(a, x.Self);
            if (b != null) await x.D.ToHand(b, x.Self);
        });
        {
            var s = S("XY-Dragon Cannon");
            BanishContact(s, "X-Head Cannon", "Y-Dragon Head");
            s.Can = x => Up(x) && x.My.Hand.Count > 0 && x.Targetable(x.Spells(x.Opp).Where(c => c.FaceUp)).Any();
            s.Cost = async x => { await x.D.Discard(x.Me, await x.PickN(x.My.Hand, 1, 1, "Discard 1 card"), x.Self); await x.Target(x.Spells(x.Opp).Where(c => c.FaceUp), "Destroy which Spell/Trap?"); };
            s.Run = async x => { if (x.T0 is { OnField: true } t) await x.D.Destroy(t, x.Self); };
        }
        {
            var s = S("XYZ-Dragon Cannon");
            BanishContact(s, "X-Head Cannon", "Y-Dragon Head", "Z-Metal Tank");
            s.Can = x => Up(x) && x.My.Hand.Count > 0 && x.Targetable(x.Their.FieldCards).Any();
            s.Cost = async x => { await x.D.Discard(x.Me, await x.PickN(x.My.Hand, 1, 1, "Discard 1 card"), x.Self); await x.Target(x.Their.FieldCards, "Destroy which card?"); };
            s.Run = async x => { if (x.T0 is { OnField: true } t) await x.D.Destroy(t, x.Self); };
        }
        {
            var s = Union("Y-Dragon Head", (x, m) => Scripts.NameOf(m) == "X-Head Cannon", effectsToo: true);
            s.Stats = (x, c, st) => { if (x.Self.Var("union") == 1 && c == x.Self.EquippedTo) st.Add(400, 400); };
        }
        {
            var s = Union("Z-Metal Tank", (x, m) => Scripts.NameOf(m) is "X-Head Cannon" or "Y-Dragon Head", effectsToo: true);
            s.Stats = (x, c, st) => { if (x.Self.Var("union") == 1 && c == x.Self.EquippedTo) st.Add(600, 600); };
        }
        ReturnsAtEnd(S("Yamata Dragon"), async (x, e) =>
        {
            if (e.Kind == EvKind.BattleDamage && e.Card == x.Self && e.Player == x.Opp && x.My.Hand.Count < 5) await x.D.Draw(x.Me, 5 - x.My.Hand.Count);
        });
        ReturnsAtEnd(S("Yata-Garasu"), async (x, e) =>
        {
            if (e.Kind == EvKind.BattleDamage && e.Card == x.Self && e.Player == x.Opp) x.D.Flags[$"skipdraw{x.Opp}"] = 1;
        });
        S("Yellow Gadget").On = async (x, e) =>
        {
            if (e.Is(EvKind.Summoned, x.Self) && e.Arg != (int)SummonKind.Flip && x.My.Deck.Any(c => c.Name == "Green Gadget") && await x.Ask("Add Green Gadget from your Deck?"))
                await x.Search(c => c.Name == "Green Gadget", "Add Green Gadget");
        };
        S("Yowie").On = async (x, e) =>
        {
            if (!e.Is(EvKind.Summoned, x.Self) || x.D.Flags.ContainsKey($"yowie{x.Me}") || !await x.Ask("Yowie: make your opponent skip their next Draw Phase?")) return;
            x.D.Flags[$"yowie{x.Me}"] = 1;
            x.D.Flags[$"skipdraw{x.Opp}"] = 1;
            var d = x.D; int me = x.Me; int count = 0;
            d.Watch(d.Turn, async ev => { if (ev.Kind == EvKind.Summoned && ev.Player == me && ev.Arg is (int)SummonKind.Special or (int)SummonKind.Fusion or (int)SummonKind.Ritual) count++; });
            d.Linger(d.Turn, (q, c, p) => q == Q.SpecialSummon && p == me && count >= 1);
        };
        S("Yowie").Note = "It is played as \"Summoned by itself\" every time (the game does not check other cards Summoned with it).";
        {
            var s = S("Yubel");
            YubelBody(s);
            s.On = async (x, e) =>
            {
                if (e.Kind == EvKind.DamageCalc && e.Other == x.Self && Up(x) && x.Self.Attack && e.Card is { Loc: Loc.Monster } a && a.Controller == x.Opp) await x.D.Damage(x.Opp, x.Atk(a), x.Self);
                if (e.Kind == EvKind.EndPhase && e.Player == x.Me && Up(x))
                {
                    var l = x.D.Tributable(x.Me).Where(m => m != x.Self).ToList();
                    if (l.Count > 0 && await x.Ask("Yubel: Tribute 1 other monster (otherwise Yubel is destroyed)?")) { var t = await x.Pick(l, "Tribute which monster?"); if (t != null) { await x.D.Tribute(t, x.Self); return; } }
                    x.Self.Vars["selfkill"] = 1; x.D.Flags[$"yubelself{x.Self.Uid}"] = x.D.Turn;
                    await x.D.Destroy(x.Self, x.Self);
                    return;
                }
                if (e.Is(EvKind.Destroyed, x.Self) && x.D.Flags.GetValueOrDefault($"yubelself{x.Self.Uid}") != x.D.Turn)
                {
                    int o = x.Self.Owner;
                    var l = x.D.P(o).Hand.Concat(x.D.P(o).Deck).Concat(x.D.P(o).Grave).Where(c => c.Name == "Yubel - Terror Incarnate" && x.D.CanSpecialSummon(c, o, by: x.Self)).ToList();
                    if (l.Count > 0 && await x.D.YesNo(o, "Special Summon Yubel - Terror Incarnate?", x.Self))
                    { var c = await x.D.PickOne(o, l, "Special Summon Yubel - Terror Incarnate", x.Self); if (c != null) await x.D.SpecialSummon(c, o, await x.D.ChoosePosition(o, c), by: x.Self); x.D.Shuffle(o); }
                }
            };
        }
        {
            var s = S("Yubel - Terror Incarnate");
            YubelBody(s);
            s.SummonOk = (x, by, k) => by?.Name == "Yubel";
            s.On = async (x, e) =>
            {
                if (e.Kind == EvKind.DamageCalc && e.Other == x.Self && Up(x) && x.Self.Attack && e.Card is { Loc: Loc.Monster } a && a.Controller == x.Opp) await x.D.Damage(x.Opp, x.Atk(a), x.Self);
                if (e.Kind == EvKind.EndPhase && e.Player == x.Me && Up(x)) await x.D.Destroy(x.D.AllMonsters.Where(m => m != x.Self).ToList(), x.Self);
                if (e.Kind == EvKind.LeftField && e.Card == x.Self && x.D.LeftFieldLog.LastOrDefault(l => l.c == x.Self) is { c: not null } lf && lf.faceUp)
                {
                    var l = x.My.Hand.Concat(x.My.Deck).Concat(x.My.Grave).Where(c => c.Name == "Yubel - The Ultimate Nightmare" && x.D.CanSpecialSummon(c, x.Me, by: x.Self)).ToList();
                    if (l.Count > 0 && await x.Ask("Special Summon Yubel - The Ultimate Nightmare?")) { await SummonPick(x, l, "Special Summon Yubel - The Ultimate Nightmare"); x.D.Shuffle(x.Me); }
                }
            };
            s.Note = "Its card text in the data is cut short: when it leaves the field it can Special Summon Yubel - The Ultimate Nightmare.";
        }
        {
            var s = S("Yubel - The Ultimate Nightmare");
            YubelBody(s);
            s.SummonOk = (x, by, k) => by?.Name == "Yubel - Terror Incarnate";
            s.On = async (x, e) =>
            {
                if (e.Kind != EvKind.AfterDamage || !Up(x) || !x.Self.Attack) return;
                var o = e.Card == x.Self ? e.Other : e.Other == x.Self ? e.Card : null;
                if (o is { Loc: Loc.Monster } && o.Controller == x.Opp) { await x.D.Damage(x.Opp, x.Atk(o), x.Self); await x.D.Destroy(o, x.Self); }
            };
        }
        S("Zaborg the Thunder Monarch").On = async (x, e) =>
        {
            if (e.Is(EvKind.Summoned, x.Self) && e.Arg == (int)SummonKind.Tribute && x.Targetable(x.D.AllMonsters).Any())
            { var t = await x.Pick(x.Targetable(x.D.AllMonsters), "Destroy which monster?"); if (t != null) await x.D.Destroy(t, x.Self); }
        };
        Spell("Zero Gravity", async x => { foreach (var m in x.D.FaceUpMonsters.ToList()) await x.D.ChangePosition(m, !m.Defense); }, x => x.D.FaceUpMonsters.Any());
        {
            var s = Spell("Zoma the Spirit", async x => await TrapMonster(x, 1800, 500, "DARK", "Zombie", 4, defense: true));
            s.On = async (x, e) =>
            {
                if (e.Is(EvKind.Destroyed, x.Self) && e.Battle && e.Other is { } k && e.From == Loc.Monster)
                    await x.D.Damage(x.Opp, k.Loc == Loc.Monster ? x.Atk(k) : Math.Max(0, k.Data.Atk), x.Self);
            };
        }
        {
            var s = S("Zombie Master");
            s.OncePerTurn = true;
            s.Can = x => Up(x) && x.D.FreeMonsterZones(x.Me) > 0 && x.My.Hand.Any(c => c.Data.IsMonster) && x.Targetable(x.D.Players.SelectMany(p => p.Grave).Where(c => c.Data.IsMonster && c.Data.Race == "Zombie" && c.Data.Level <= 4)).Any();
            s.Cost = async x =>
            {
                await x.D.ToGrave((await x.PickN(x.My.Hand.Where(c => c.Data.IsMonster), 1, 1, "Send 1 monster from your hand to the GY"))[0], x.Self);
                await x.Target(x.D.Players.SelectMany(p => p.Grave).Where(c => c.Data.IsMonster && c.Data.Race == "Zombie" && c.Data.Level <= 4 && x.D.CanSpecialSummon(c, x.Me, by: x.Self)), "Special Summon which Zombie?");
            };
            s.Run = async x => { if (Up(x) && x.T0 is { Loc: Loc.Grave } t) await x.D.SpecialSummon(t, x.Me, await x.D.ChoosePosition(x.Me, t), by: x.Self); };
        }
        Sure("Wild Fire", "Wild Nature's Release", "Wing Requital", "Winged Kuriboh", "Winged Kuriboh LV10", "Wroughtweiler", "XY-Dragon Cannon", "XYZ-Dragon Cannon", "Y-Dragon Head", "Z-Metal Tank",
            "Yamata Dragon", "Yata-Garasu", "Yellow Gadget", "Zaborg the Thunder Monarch", "Zero Gravity", "Zoma the Spirit", "Zombie Master", "Wicked-Breaking Flamberge - Baou");
    }
}
