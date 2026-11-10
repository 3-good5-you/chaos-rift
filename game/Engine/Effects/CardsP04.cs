using System.Text.RegularExpressions;

namespace PoC.Engine;

// Official cards, batch 4 (Call of the Earthbound .. Clear Phantom).
public static partial class Scripts
{
    /// Official Ritual Summon: a Ritual Monster from your hand that `mon` accepts, Tributing monsters from your hand or field
    /// whose total Levels equal (exact) or reach its Level; `graveMat` adds GY monsters that are banished instead (Chaos Form).
    internal static IEnumerable<Card> RitualTargets(Ctx x, Func<Card, bool> mon, bool exact, Func<Card, bool>? graveMat = null) =>
        x.My.Hand.Where(m => m.Data.IsRitual && mon(m) && x.D.CanSpecialSummon(m, x.Me, SummonKind.Ritual, x.Self, ignoreZones: true)
            && (exact ? SubsetSum(RitualMats(x, m, graveMat).Select(c => x.D.Level(c)), x.D.Level(m)) : RitualMats(x, m, graveMat).Sum(c => x.D.Level(c)) >= x.D.Level(m))
            && (x.D.FreeMonsterZones(x.Me) > 0 || RitualMats(x, m, graveMat).Any(c => c.Loc == Loc.Monster)));

    static List<Card> RitualMats(Ctx x, Card m, Func<Card, bool>? graveMat) =>
        x.My.Hand.Where(h => h != m && h.Data.IsMonster).Concat(x.D.Tributable(x.Me)).Concat(graveMat == null ? Enumerable.Empty<Card>() : x.My.Grave.Where(graveMat)).ToList();

    internal static async Flow<bool> RitualSummon(Ctx x, Func<Card, bool> mon, bool exact, Func<Card, bool>? graveMat = null)
    {
        var m = await x.Pick(RitualTargets(x, mon, exact, graveMat), "Ritual Summon which monster?");
        if (m == null) return false;
        var mats = RitualMats(x, m, graveMat);
        int lv = x.D.Level(m);
        List<Card> used;
        if (exact) used = await PickLevels(x, mats, lv, $"Tribute for {m.Name}");
        else
        {
            used = new();
            while (used.Sum(c => x.D.Level(c)) < lv)
            {
                var t = await x.Pick(mats.Except(used), $"Tribute for {m.Name} ({used.Sum(c => x.D.Level(c))}/{lv} Levels)");
                if (t == null) return false;
                used.Add(t);
            }
        }
        foreach (var c in used) { if (c.Loc == Loc.Grave) await x.D.Banish(c, x.Self); else await x.D.Tribute(c, x.Self); }
        return await x.D.SpecialSummon(m, x.Me, await x.D.ChoosePosition(x.Me, m), kind: SummonKind.Ritual, by: x.Self);
    }

    /// Chrysalis: Tribute this card while "Neo Space" is on the field: Special Summon its Neo-Spacian from your hand or Deck.
    static void Chrysalis(string name, string into)
    {
        var s = S(name);
        s.Can = x => Up(x) && x.D.CountActive("Neo Space") > 0 && x.My.Hand.Concat(x.My.Deck).Any(c => c.Name == into);
        s.Cost = async x => await x.D.Tribute(x.Self, x.Self);
        s.Run = async x => { await SummonPick(x, x.My.Hand.Concat(x.My.Deck).Where(c => c.Name == into), $"Special Summon {into}"); x.D.Shuffle(x.Me); };
    }

    /// "Mentions" a card: its text names it in quotes.
    internal static bool Mentions(CardData d, string name) => d.Desc.Contains('"' + name + '"');

    static void CardsP04(CardDb db)
    {
        Spell("Call of the Earthbound", async x =>
        {
            var t = await x.Pick(x.MyMonsters(), "Choose the target of the attack");
            if (t != null && x.D.Attacker is { } a && a.Valid()) x.D.AttackTarget = t;
        }, x => AttackOnMe(x) && x.MyMonsters().Any());
        {
            var s = S("Candelato, the Beast of Light");
            s.AlsoIn = new[] { "Phantom Beast" };
            s.Stats = (x, c, st) => { if (c == x.Self) st.Add(1000 * x.My.Hand.Count, 1000 * x.My.Hand.Count); };
            s.AddFx(f =>
            {
                f.Label = "Special Summon"; f.FromHand = true;
                f.Can = x => Hopt(x, "1") && x.My.Hand.Count(c => c != x.Self) >= 2 && x.D.CanSpecialSummon(x.Self, x.Me, by: x.Self);
                f.Cost = async x => { UseHopt(x, "1"); await x.D.Discard(x.Me, await x.PickN(x.My.Hand.Where(c => c != x.Self), 2, 2, "Discard 2 other cards"), x.Self); };
                f.Run = async x =>
                {
                    if (x.Self.Loc != Loc.Hand || !await x.D.SpecialSummon(x.Self, x.Me, await x.D.ChoosePosition(x.Me, x.Self), by: x.Self)) return;
                    if (x.My.Deck.Any(c => c.Data.Desc.Contains("coin")) && await x.Ask("Add a card with a coin-tossing effect from your Deck?"))
                        await x.Search(c => c.Data.Desc.Contains("coin"), "Add which card?");
                };
            });
            s.AddFx(f =>
            {
                f.Label = "Negate a search"; f.Speed = 2; f.AnyTurn = true;
                f.Can = x => Hopt(x, "2") && x.Ev is { Kind: EvKind.Activated, Link: { } l } && l.Card != x.Self && Regex.IsMatch(l.Card.Data.Desc, @"from (your|the|their) Deck to (your|the|their) hand");
                f.Cost = async x => UseHopt(x, "2");
                f.Run = async x => await x.D.Negate(x.Ev!.Link!, destroy: false);
            });
        }
        S("Cannonball Spear Shellfish").Forbid = (x, q, c) => q == Q.AffectedBySpell && c == x.Self && x.D.CountActive("Umi") > 0;
        {
            var s = S("Carboneddon");
            s.CalcAtk = (x, c, vs, atk) => c == x.Self && vs != null && x.IsAttr(vs, "FIRE") ? atk + 1000 : atk;
            s.AddFx(f =>
            {
                f.Label = "Summon a Dragon"; f.FromGrave = true;
                f.Can = x => Hopt(x) && x.D.FreeMonsterZones(x.Me) > 0 && x.My.Hand.Concat(x.My.Deck).Any(c => c.Data.IsNormalMonster && c.Data.Race == "Dragon" && c.Data.Level <= 7);
                f.Cost = async x => { UseHopt(x); await x.D.Banish(x.Self, x.Self); };
                f.Run = async x => { await SummonPick(x, x.My.Hand.Concat(x.My.Deck).Where(c => c.Data.IsNormalMonster && c.Data.Race == "Dragon" && c.Data.Level <= 7), "Special Summon which Dragon Normal Monster?", defense: true); x.D.Shuffle(x.Me); };
            });
        }
        {
            static async Flow Mill(Ctx x)
            {
                int max = Math.Min(3, x.My.Deck.Count);
                if (max == 0) return;
                int n = 1 + await x.D.ChooseText(x.Me, "Card Blocker: send how many cards from the top of your Deck?", Enumerable.Range(1, max).Select(i => i.ToString()).ToList(), x.Self);
                for (int i = 0; i < n && x.My.Deck.Count > 0; i++) await x.D.ToGrave(x.My.Deck[0], x.Self);
                x.D.Boost(x.Self, 0, 500 * n);
            }
            S("Card Blocker").On = async (x, e) =>
            {
                if (e.Is(EvKind.Summoned, x.Self) && Up(x) && x.Self.Attack) { await x.D.ChangePosition(x.Self, true); return; }
                if (e.Kind != EvKind.AttackDeclared || e.Player != x.Opp || !Up(x)) return;
                if (e.Other is { } t && t != x.Self && t.Controller == x.Me && t.FaceUp && await x.Ask($"Change the attack target to Card Blocker?"))
                    x.D.AttackTarget = x.Self;
                if (x.D.AttackTarget == x.Self && x.My.Deck.Count > 0 && await x.Ask("Send 1-3 cards from your Deck to the GY for +500 DEF each?")) await Mill(x);
            };
            S("Card Blocker").Note = "Its effects are offered at once when an attack is declared (no chain).";
        }
        {
            var s = S("Card Ejector");
            s.OncePerTurn = true;
            s.Can = x => Up(x) && x.Targetable(x.Their.Grave).Any();
            s.Cost = async x => await x.Target(x.Their.Grave, "Banish which card from your opponent's GY?");
            s.Run = async x => { if (x.T0 is { Loc: Loc.Grave } t) await x.D.Banish(t, x.Self); };
        }
        {
            var s = S("Card Guard");
            s.Stats = (x, c, st) => { if (c == x.Self) st.Add(300 * x.Self.Counters("guard"), 0); };
            s.On = async (x, e) => { if (e.Is(EvKind.Summoned, x.Self) && e.Summoned(SummonKind.Normal, SummonKind.Tribute, SummonKind.Special, SummonKind.Fusion, SummonKind.Ritual)) AddCounter(x, x.Self, 1, "guard"); };
            s.OncePerTurn = true;
            s.Can = x => Up(x) && x.Self.Counters("guard") > 0 && x.Targetable(x.My.FieldCards.Where(c => c.FaceUp && c != x.Self)).Any();
            s.Cost = async x => await x.Target(x.My.FieldCards.Where(c => c.FaceUp && c != x.Self), "Move a Guard Counter to which card?");
            s.Run = async x =>
            {
                if (x.Self.Counters("guard") == 0) return;
                AddCounter(x, x.Self, -1, "guard");
                if (x.T0 is { OnField: true } t) AddCounter(x, t, 1, "guard");
            };
            s.Note = "A card with a Guard Counter loses 1 counter instead of being destroyed.";
        }
        Spell("Card of Demise", async x =>
        {
            if (x.My.Hand.Count < 3) await x.D.Draw(x.Me, 3 - x.My.Hand.Count);
            var d = x.D; int me = x.Me, opp = x.Opp;
            d.Linger(d.Turn, (q, c, p) => q == Q.TakeDamage && p == opp);
            d.Later(d.Turn, Phase.End, async () => { foreach (var c in d.P(me).Hand.ToList()) await d.ToGrave(c); });
        }, x => Hopt(x) && !x.D.TurnFlags.Contains($"ss{x.Me}"),
        async x =>
        {
            UseHopt(x);
            var d = x.D; int me = x.Me;
            d.Linger(d.Turn, (q, c, p) => q == Q.SpecialSummon && c != null && (p == me || c.Controller == me));
        });
        Spell("Card of Sanctity", async x =>
        {
            foreach (var c in x.My.FieldCards.Where(c => c != x.Self).Concat(x.My.Hand).ToList()) await x.D.Banish(c, x.Self);
            if (x.My.Hand.Count < 2) await x.D.Draw(x.Me, 2 - x.My.Hand.Count);
        }, x => x.My.Hand.Any(c => c != x.Self) && x.My.FieldCards.Any(c => c != x.Self));
        {
            var s = S("Card Trooper");
            s.OncePerTurn = true;
            s.Can = x => Up(x) && x.My.Deck.Count > 0;
            s.Cost = async x =>
            {
                int max = Math.Min(3, x.My.Deck.Count);
                x.N = 1 + await x.D.ChooseText(x.Me, "Send how many cards from the top of your Deck to the GY?", Enumerable.Range(1, max).Select(i => i.ToString()).ToList(), x.Self);
                for (int i = 0; i < x.N && x.My.Deck.Count > 0; i++) await x.D.ToGrave(x.My.Deck[0], x.Self);
            };
            s.Run = async x => { if (Up(x)) x.D.Boost(x.Self, 500 * x.N); };
            s.On = async (x, e) =>
            {
                if (e.Is(EvKind.Destroyed, x.Self) && x.Self.Loc == Loc.Grave && x.Self.Owner == x.Me && x.D.LeftFieldLog.LastOrDefault(l => l.c == x.Self) is { c: not null } l && l.controller == x.Me)
                    await x.D.Draw(x.Me, 1);
            };
        }
        {
            var s = S("Castle Gate");
            s.Forbid = (x, q, c) => q == Q.DestroyByBattle && c == x.Self;
            s.OncePerTurn = true;
            s.Can = x => Up(x) && x.Self.Attack && x.D.Tributable(x.Me).Any(m => x.D.Level(m) <= 5 && m.Data.Atk > 0);
            s.Cost = async x =>
            {
                var t = await x.Pick(x.D.Tributable(x.Me).Where(m => x.D.Level(m) <= 5 && m.Data.Atk > 0), "Tribute 1 Level 5 or lower monster");
                if (t != null) { x.N = t.Data.Atk; await x.D.Tribute(t, x.Self); }
            };
            s.Run = async x => await x.D.Damage(x.Opp, x.N, x.Self);
        }
        {
            var s = S("Catnipped Kitty");
            s.Forbid = (x, q, c) => q == Q.BeAttacked && c == x.Self && x.MyMonsters().Any(m => m != x.Self && m.FaceUp && x.IsRace(m, "Beast"));
            s.OncePerTurn = true;
            s.Can = x => Up(x) && x.OppMonsters().Any();
            s.Run = async x =>
            {
                var t = await x.Pick(x.OppMonsters(), "Make which monster's DEF 0 this turn?");
                if (t != null) { t.Vars["basedef"] = 1; t.Vars["basedefturn"] = x.D.Turn; x.D.Seq++; }
            };
        }
        {
            var s = S("Cave Dragon");
            s.NormalSummonOk = x => !x.MyMonsters().Any();
            s.Forbid = (x, q, c) => q == Q.Attack && c == x.Self && !x.MyMonsters().Any(m => m != x.Self && m.FaceUp && x.IsRace(m, "Dragon"));
            s.Note = "Setting it is still allowed while you control a monster (the text only stops a Normal Summon).";
        }
        {
            var s = S("Celestial Sword - Eatos");
            s.AlsoIn = new[] { "Noble Arms" };
            s.EquipOk = (x, m) => true;
            s.Stats = (x, c, st) => { if (c == x.Self.EquippedTo) st.Add(500, 0); };
            s.On = async (x, e) =>
            {
                if (!e.Is(EvKind.LeftField, x.Self) || x.Self.Loc != Loc.Grave) return;
                var l = x.Targetable(x.MyMonsters().FaceUp().Where(m => Scripts.NameOf(m) == "Guardian Eatos")).ToList();
                if (l.Count == 0 || !await x.Ask("Give Guardian Eatos 500 ATK for each banished monster?")) return;
                var t = await x.Pick(l, "Target Guardian Eatos");
                if (t != null) { t.Vars["modatk"] = t.Var("modatk") + 500 * x.D.Players.Sum(p => p.Banished.Count(b => b.Data.IsMonster)); x.D.Seq++; }
            };
        }
        Spell("Cemetary Bomb", async x => await x.D.Damage(x.Opp, 100 * x.Their.Grave.Count, x.Self));
        S("Chain Burst").On = async (x, e) =>
        {
            if (e.Kind == EvKind.Resolved && e.Link is { IsCardActivation: true } l && l.Card.Data.IsTrap && l.Card != x.Self && x.Self.OnField) await x.D.Damage(l.Player, 1000, x.Self);
        };
        Spell("Chain Disappearance", async x =>
        {
            if (x.Ev?.Card is not { } m || !m.Valid() || !m.FaceUp) return;
            string name = m.Name; int who = m.Controller;
            await x.D.Banish(m, x.Self);
            foreach (var c in x.D.P(who).Hand.Concat(x.D.P(who).Deck).Where(c => c.Name == name).ToList()) await x.D.Banish(c, x.Self);
        }, x => x.Ev is { Kind: EvKind.Summoned } e && e.Player == x.Opp && e.Card!.Valid() && e.Card.FaceUp && x.Atk(e.Card) <= 1000);
        Spell("Chain Material", async x =>
        {
            x.D.TurnFlags.Add($"chainmaterial{x.Me}");
            var d = x.D; int me = x.Me;
            d.Linger(d.Turn, (q, c, p) => q == Q.Attack && c != null && c.Controller == me);
        }, x => x.MyMonsters().All(m => m.Attacks == 0));
        Spell("Change of Hero - Reflector Ray", async x => await x.D.Damage(x.Opp, 300 * x.Ev!.Card!.Data.Level, x.Self),
            x => x.Ev is { Kind: EvKind.Destroyed, Battle: true } e && e.Card!.Loc == Loc.Grave && e.Card.Data.IsFusion && e.Card.In("Elemental HERO")
                && x.D.LeftFieldLog.LastOrDefault(l => l.c == e.Card) is { c: not null } l && l.controller == x.Me);
        Spell("Chaos Burst", async x =>
        {
            if (x.T0 is not { } a || !a.Valid() || !a.FaceUp) return;
            await x.D.Destroy(a, x.Self);
            if (a.Loc != Loc.Monster) await x.D.Damage(x.Opp, 1000, x.Self);
        }, x => AttackOnMe(x) && x.D.Tributable(x.Me).Any(),
        async x =>
        {
            var t = await x.Pick(x.D.Tributable(x.Me), "Tribute 1 monster");
            if (t != null) await x.D.Tribute(t, x.Self);
            x.T.Add(x.Ev!.Card!);
        });
        {
            var s = S("Chaos Core");
            string[] lords = { "Uria, Lord of Searing Flames", "Hamon, Lord of Striking Thunder", "Raviel, Lord of Phantasms" };
            s.Speed = 2; s.AnyTurn = true;
            s.Can = x => Up(x) && Hopt(x) && ((x.Ev is { Kind: EvKind.Activated, Link: { } l } && l.X.T.Contains(x.Self))
                || (x.Ev is { Kind: EvKind.AttackDeclared } e && e.Other == x.Self && e.Player == x.Opp));
            s.Cost = async x => UseHopt(x);
            s.Run = async x =>
            {
                int n = 0;
                foreach (var lord in lords)
                {
                    var l = x.My.Hand.Concat(x.My.Deck).Where(c => c.Name == lord).ToList();
                    if (l.Count == 0 || !await x.Ask($"Send {lord} to the GY?")) continue;
                    bool deck = l[0].Loc == Loc.Deck;
                    await x.D.ToGrave(l[0], x.Self); n++;
                    if (deck) x.D.Shuffle(x.Me);
                }
                if (n > 0 && x.Self.Valid()) { AddCounter(x, x.Self, n, "phantasm"); x.D.TurnFlags.Add($"nobattledamage{x.Me}"); }
            };
            s.DestroyInstead = async (x, battle) =>
            {
                if (x.Self.Counters("phantasm") == 0 || !await x.Ask("Remove 1 Phantasm Counter from Chaos Core instead?")) return false;
                AddCounter(x, x.Self, -1, "phantasm");
                return true;
            };
        }
        {
            var s = S("Chaos Distill");
            s.Allow = (x, q, c) => q == Q.BanishAny && c != null && c.Owner == x.Me;
            s.AddFx(f =>
            {
                f.Label = "Add a Macro Cosmos card";
                f.Can = x => Hopt(x) && x.My.Deck.Any(c => c.Name != "Chaos Distill" && (c.Name == "Macro Cosmos" || Mentions(c.Data, "Macro Cosmos")));
                f.Cost = async x => UseHopt(x);
                f.Run = x => x.Search(c => c.Name != "Chaos Distill" && (c.Name == "Macro Cosmos" || Mentions(c.Data, "Macro Cosmos")), "Add which card?");
            });
            s.AddFx(f =>
            {
                f.Label = "Add a LIGHT ? ATK monster";
                f.Can = x => Hopt(x) && x.D.CountActive("Macro Cosmos") > 0 && x.My.Deck.Any(c => c.Data.IsMonster && c.Data.Attribute == "LIGHT" && c.Data.Atk < 0);
                f.Cost = async x => { UseHopt(x); await x.D.Banish(x.Self, x.Self); };
                f.Run = x => x.Search(c => c.Data.IsMonster && c.Data.Attribute == "LIGHT" && c.Data.Atk < 0, "Add which LIGHT monster with ? ATK?");
            });
        }
        {
            var s = S("Chaos Emperor Dragon - Envoy of the End");
            static IEnumerable<Card> Of(Ctx x, string a) => x.My.Grave.Where(c => c.Data.IsMonster && c.Data.Attribute == a);
            s.SummonOk = (x, by, k) => by == x.Self;
            s.ProcCan = x => Of(x, "LIGHT").Any() && Of(x, "DARK").Any();
            s.Proc = async x =>
            {
                var l = await x.Pick(Of(x, "LIGHT"), "Banish 1 LIGHT monster from your GY");
                var d = await x.Pick(Of(x, "DARK"), "Banish 1 DARK monster from your GY");
                if (l == null || d == null) return;
                await x.D.Banish(l, x.Self); await x.D.Banish(d, x.Self);
                await x.D.SpecialSummon(x.Self, x.Me, await x.D.ChoosePosition(x.Me, x.Self), by: x.Self);
            };
            s.OncePerTurn = true;
            s.Can = x => Up(x) && x.My.Lp > 1000 && !x.D.TurnFlags.Contains($"act{x.Me}");
            s.Cost = async x => x.D.Pay(x.Me, 1000);
            s.Run = async x =>
            {
                var d = x.D; int me = x.Me;
                d.Linger(d.Turn, (q, c, p) => q == Q.Activate && c != null && c.Controller == me);
                var all = d.Players.SelectMany(p => p.Hand.Concat(p.FieldCards)).ToList();
                foreach (var c in all) if (c.Loc != Loc.None) await d.ToGrave(c, x.Self);
                int n = all.Count(c => c.Loc == Loc.Grave && c.Owner == x.Opp);
                await d.Damage(x.Opp, 300 * n, x.Self);
            };
        }
        Spell("Chaos End", async x => await x.D.Destroy(x.AllMonsters().ToList(), x.Self), x => x.My.Banished.Count >= 7);
        Spell("Chaos Form", async x => await RitualSummon(x, m => m.In("Chaos") || m.In("Black Luster Soldier"), exact: true,
            c => c.Data.IsMonster && c.Name is "Blue-Eyes White Dragon" or "Dark Magician"),
            x => RitualTargets(x, m => m.In("Chaos") || m.In("Black Luster Soldier"), true, c => c.Data.IsMonster && c.Name is "Blue-Eyes White Dragon" or "Dark Magician").Any());
        Spell("Chaos Greed", async x => await x.D.Draw(x.Me, 2), x => x.My.Banished.Count >= 4 && x.My.Grave.Count == 0);
        SelfBoost("Chaos Necromancer", x => 300 * x.My.Grave.Count(c => c.Data.IsMonster));
        {
            var s = S("Chaosrider Gustaph");
            s.OncePerTurn = true;
            s.Can = x => Up(x) && x.My.Grave.Any(c => c.Data.IsSpell);
            s.Cost = async x =>
            {
                var l = await x.PickN(x.My.Grave.Where(c => c.Data.IsSpell), 1, 2, "Banish up to 2 Spells from your GY");
                foreach (var c in l) await x.D.Banish(c, x.Self);
                x.N = l.Count;
            };
            s.Run = async x =>
            {
                if (!Up(x)) return;
                var me = x.Self; int gain = 300 * x.N;
                me.Vars["modatk"] = me.Var("modatk") + gain; x.D.Seq++;
                x.D.Later(x.D.Turn + 1, Phase.End, async () => { if (me.Loc == Loc.Monster) me.Vars["modatk"] = me.Var("modatk") - gain; });
            };
        }
        {
            var s = S("Charm of Shabti");
            s.AddFx(f =>
            {
                f.Label = "Protect Gravekeeper's"; f.FromHand = true; f.Speed = 2; f.AnyTurn = true;
                f.Cost = async x => await x.D.Discard(x.Me, new[] { x.Self }, x.Self);
                f.Run = async x => { var d = x.D; int me = x.Me; d.Linger(d.Turn, (q, c, p) => q == Q.DestroyByBattle && c != null && c.Controller == me && c.In("Gravekeeper's")); };
            });
        }
        {
            var s = S("Chimeratech Fortress Dragon");
            s.NotMaterial = true;
            s.SummonOk = (x, by, k) => by == x.Self;
            s.ProcFromDeck = true;
            s.ProcCan = x => x.Self.Loc == Loc.Extra && x.AllMonsters().Any(m => m.FaceUp && Scripts.NameOf(m) == "Cyber Dragon") && x.AllMonsters().Count(m => m.FaceUp && x.IsRace(m, "Machine")) >= 2;
            s.Proc = async x =>
            {
                var cd = await x.Pick(x.AllMonsters().Where(m => m.FaceUp && Scripts.NameOf(m) == "Cyber Dragon"), "Send Cyber Dragon from either field to the GY");
                if (cd == null) return;
                var more = await x.PickN(x.AllMonsters().Where(m => m != cd && m.FaceUp && x.IsRace(m, "Machine")), 1, 5, "Send 1 or more Machine monsters from either field to the GY");
                var mats = more.Prepend(cd).ToList();
                foreach (var m in mats) await x.D.ToGrave(m, x.Self);
                if (await x.D.SpecialSummon(x.Self, x.Me, await x.D.ChoosePosition(x.Me, x.Self), by: x.Self)) x.Self.Vars["matcount"] = mats.Count;
            };
            s.Base = (x, c, st) => { if (c == x.Self) st.BaseAtk = 1000 * x.Self.Var("matcount"); };
        }
        {
            var s = S("Chimeratech Overdragon");
            s.Materials = new() { Named("Cyber Dragon"), Cond("1+ Machine monsters", c => c.Data.IsMonster && c.Data.Race == "Machine") with { More = true } };
            s.Base = (x, c, st) => { if (c == x.Self) { st.BaseAtk = 800 * x.Self.Var("matcount"); st.BaseDef = 800 * x.Self.Var("matcount"); } };
            s.ExtraAttacks = x => Math.Max(0, x.Self.Var("matcount") - 1);
            s.Forbid = (x, q, c) => q == Q.DirectAttack && c == x.Self && x.Self.Var("matcount") > 1 && x.Self.Attacks > 0;
            s.On = async (x, e) =>
            {
                if (e.Is(EvKind.Summoned, x.Self) && e.Arg == (int)SummonKind.Fusion)
                    foreach (var c in x.My.FieldCards.Where(c => c != x.Self).ToList()) await x.D.ToGrave(c, x.Self);
            };
            s.Note = "Its extra attacks are for monsters only: after attacking once it cannot attack directly.";
        }
        {
            var s = S("Chiron the Mage");
            s.OncePerTurn = true;
            s.Can = x => Up(x) && x.My.Hand.Any(c => c.Data.IsSpell) && x.Targetable(x.Spells(x.Opp)).Any();
            s.Cost = async x =>
            {
                await x.D.Discard(x.Me, await x.PickN(x.My.Hand.Where(c => c.Data.IsSpell), 1, 1, "Discard 1 Spell"), x.Self);
                await x.Target(x.Spells(x.Opp), "Destroy which Spell/Trap?");
            };
            s.Run = async x => { if (x.T0 is { OnField: true } t) await x.D.Destroy(t, x.Self); };
        }
        Chrysalis("Chrysalis Chicky", "Neo-Spacian Air Hummingbird");
        Chrysalis("Chrysalis Dolphin", "Neo-Spacian Aqua Dolphin");
        Chrysalis("Chrysalis Larva", "Neo-Spacian Flare Scarab");
        Chrysalis("Chrysalis Mole", "Neo-Spacian Grand Mole");
        Chrysalis("Chrysalis Pantail", "Neo-Spacian Dark Panther");
        Chrysalis("Chrysalis Pinny", "Neo-Spacian Glow Moss");
        {
            var s = S("Chthonian Alliance");
            s.EquipOk = (x, m) => true;
            s.Stats = (x, c, st) => { if (c == x.Self.EquippedTo) st.Add(800 * x.D.FaceUpMonsters.Count(m => m != c && Scripts.NameOf(m) == Scripts.NameOf(c)), 0); };
        }
        Spell("Chthonian Blast", async x =>
        {
            var l = x.D.FaceUpMonsters.ToList();
            if (l.Count == 0) return;
            int min = l.Min(m => x.Atk(m));
            var t = await x.Pick(l.Where(m => x.Atk(m) == min), "Destroy which monster (lowest ATK)?");
            if (t == null) return;
            int half = x.Atk(t) / 2;
            await x.D.Destroy(t, x.Self);
            if (t.Loc != Loc.Monster) { await x.D.Damage(x.Me, half, x.Self); await x.D.Damage(x.Opp, half, x.Self); }
        }, x => x.Ev is { Kind: EvKind.Destroyed } e && e.Card!.Loc == Loc.Grave && e.From == Loc.Monster
            && x.D.LeftFieldLog.LastOrDefault(l => l.c == e.Card) is { c: not null } l && l.controller == x.Me && l.faceUp && x.D.FaceUpMonsters.Any());
        Spell("Chthonian Polymer", async x =>
        {
            if (x.T0 is { } f && f.Valid()) await x.D.TakeControl(f, x.Me);
        }, x => x.Ev is { Kind: EvKind.Summoned } e && e.Arg == (int)SummonKind.Fusion && e.Player == x.Opp && e.Card!.Valid() && x.D.Tributable(x.Me).Any(),
        async x =>
        {
            var t = await x.Pick(x.D.Tributable(x.Me), "Tribute 1 monster");
            if (t != null) await x.D.Tribute(t, x.Self);
            x.T.Add(x.Ev!.Card!);
        });
        S("Chthonian Soldier").On = async (x, e) =>
        {
            if (e.Kind == EvKind.BattleDamage && e.Player == x.Me && (e.Card == x.Self || e.Other == x.Self)) x.D.Flags[$"chth{x.Self.Uid}"] = e.Amount;
            if (e.Is(EvKind.Destroyed, x.Self) && e.Battle && x.Self.Loc == Loc.Grave && e.Other is { } a && a == x.D.Attacker && a.Controller == x.Opp
                && x.D.Flags.GetValueOrDefault($"chth{x.Self.Uid}") is > 0 and var n)
            { x.D.Flags.Remove($"chth{x.Self.Uid}"); await x.D.Damage(x.Opp, n, x.Self); }
        };
        Spell("Clay Charge", async x =>
        {
            var e = x.Ev!;
            if (e.Other is not { } clay || !clay.Valid()) return;
            if (!clay.FaceUp) { x.D.Reveal(x.Opp, clay); }
            await x.D.Destroy(new[] { e.Card!, clay }.Where(c => c.Valid()).ToList(), x.Self);
            await x.D.Damage(x.Opp, 800, x.Self);
        }, x => x.Ev is { Kind: EvKind.AttackDeclared } e && e.Player == x.Opp && e.Other is { } t && t.Controller == x.Me && t.Name == "Elemental HERO Clayman");
        {
            static bool Clear(CardData d) => d.IsMonster && Mentions(d, "Clear World");
            var s = S("Clear Cube");
            s.ExtraSummon = (x, c) => Up(x) && c.Controller == x.Me && Clear(c.Data);
            s.On = async (x, e) =>
            {
                if (e.Is(EvKind.LeftField, x.Self) && e.Other is { } by && by.Controller == x.Opp && x.D.LeftFieldLog.LastOrDefault(l => l.c == x.Self) is { c: not null } l
                    && l.faceUp && l.controller == x.Self.Owner && x.My.Deck.Any(c => Clear(c.Data)) && await x.Ask("Special Summon a monster that mentions Clear World from your Deck?"))
                { await SummonPick(x, x.My.Deck.Where(c => Clear(c.Data)), "Special Summon which?"); x.D.Shuffle(x.Me); }
            };
            s.Note = "\"Unaffected by Clear World\" is applied by Clear World itself.";
        }
        {
            static bool Clearish(CardData d) => d.Name == "Clear World" || (!d.IsMonster && Mentions(d, "Clear World"));
            var s = S("Clear Phantom");
            s.AddFx(f =>
            {
                f.Label = "Add 2 Clear World cards"; f.FromHand = true;
                f.Can = x => Hopt(x) && x.My.Hand.Any(c => c != x.Self) && x.My.Deck.Count(c => Clearish(c.Data)) >= 2;
                f.Cost = async x =>
                {
                    UseHopt(x);
                    var other = await x.PickN(x.My.Hand.Where(c => c != x.Self), 1, 1, "Discard 1 other card");
                    await x.D.Discard(x.Me, other.Prepend(x.Self).ToList(), x.Self);
                };
                f.Run = async x =>
                {
                    foreach (var c in await x.PickN(x.My.Deck.Where(c => Clearish(c.Data)), 2, 2, "Add 2 cards (Clear World or Spells/Traps that mention it)")) await x.D.ToHand(c, x.Self);
                    x.D.Shuffle(x.Me);
                };
            });
            s.On = async (x, e) =>
            {
                if (!e.Is(EvKind.Destroyed, x.Self) || !x.Targetable(x.OppMonsters()).Any() || !await x.Ask("Destroy 1 monster your opponent controls (Clear Phantom)?")) return;
                var t = await x.Pick(x.Targetable(x.OppMonsters()), "Destroy which monster?");
                if (t == null) return;
                await x.D.Destroy(t, x.Self);
                if (t.Loc != Loc.Monster) foreach (var c in x.Their.Deck.Take(3).ToList()) await x.D.ToGrave(c, x.Self);
            };
        }
    }
}
