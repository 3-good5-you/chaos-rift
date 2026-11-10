using System.Text.RegularExpressions;

namespace PoC.Engine;

// Official cards, batch 9 (Dimension Fusion Destruction .. Elemental HERO Air Neos).
public static partial class Scripts
{
    /// Contact Fusion: "Must first be Special Summoned (from your Extra Deck) by returning the above cards you control to the
    /// Deck (You do not use Polymerization)"; Neos Fusions return to the Extra Deck during the End Phase (unless Neo Space).
    internal static Script Contact(string name, bool returns = true)
    {
        var s = S(name);
        var names = Regex.Matches(CardDb.Shared.Named(name).Desc.Split('\n')[0], "\"([^\"]+)\"").Select(m => m.Groups[1].Value).ToList();
        IEnumerable<Card> Mats(Ctx x, string n) => x.MyMonsters().Where(m => m.FaceUp && string.Equals(Scripts.NameOf(m), n, StringComparison.OrdinalIgnoreCase));
        s.SummonOk = (x, by, k) => by == x.Self;
        s.ProcFromDeck = true;
        s.ProcCan = x => x.Self.Loc == Loc.Extra && names.All(n => Mats(x, n).Any()) && names.Distinct(StringComparer.OrdinalIgnoreCase).Count() == names.Count;
        s.Proc = async x =>
        {
            var used = new List<Card>();
            foreach (var n in names) { var m = await x.Pick(Mats(x, n).Except(used), $"Return {n} to the Deck"); if (m == null) return; used.Add(m); }
            foreach (var m in used) await x.D.MoveTo(m, Loc.Deck, by: x.Self);
            x.D.Shuffle(x.Me);
            await x.D.SpecialSummon(x.Self, x.Me, await x.D.ChoosePosition(x.Me, x.Self), by: x.Self);
        };
        if (returns)
            s.On = async (x, e) => { if (e.Kind == EvKind.EndPhase && Up(x) && x.D.CountActive("Neo Space", x.Me) == 0 && !InstantNeoSpace(x)) await x.D.MoveTo(x.Self, Loc.Extra, by: x.Self); };
        s.Note = "Contact Fusion: Special Summon it from the Extra Deck (on your field) by shuffling the listed monsters you control into the Deck.";
        return s;
    }

    /// Destroy a card in the hand (it goes to the GY and counts as destroyed).
    internal static async Flow DestroyInHand(Ctx x, Card c)
    {
        if (c.Loc != Loc.Hand) return;
        await x.D.MoveTo(c, Loc.Grave, by: x.Self);
        await x.D.Raise(new Ev { Kind = EvKind.Destroyed, Card = c, Other = x.Self, From = Loc.Hand, Player = c.Owner });
    }

    static bool AttrOnField(Ctx x, string attr) => x.D.FaceUpMonsters.Any(m => x.IsAttr(m, attr));

    static void CardsP09(CardDb db)
    {
        {
            var s = Spell("Dimension Fusion Destruction", async x =>
            {
                var pool = x.My.Hand.Concat(x.MyMonsters()).Concat(x.My.Grave).Where(c => c.Data.IsMonster);
                x.D.LastFusion = null;
                // Banish the listed materials, then Special Summon (not a Fusion Summon) ignoring its Summoning conditions.
                var opts = x.D.FusionOptions(x.Me, pool: pool, resultOk: c => c.In("Phantasm")).ToList();
                if (opts.Count == 0) return;
                var res = await x.Pick(opts.Select(o => o.result), "Which Phantasm Fusion Monster?");
                if (res == null) return;
                var needs = opts.First(o => o.result == res).mats;
                foreach (var n in needs) { var m = await x.Pick(pool.Where(c => n.Ok(c) && c.Loc != Loc.Banished), $"Banish {n.Label}"); if (m != null) await x.D.Banish(m, x.Self); }
                if (await x.D.SpecialSummon(res, x.Me, await x.D.ChoosePosition(x.Me, res), by: x.Self, ignore: true)) res.Vars["nodmg"] = 1;
            }, x => x.D.FusionOptions(x.Me, pool: x.My.Hand.Concat(x.MyMonsters()).Concat(x.My.Grave).Where(c => c.Data.IsMonster), resultOk: c => c.In("Phantasm")).Any());
            // "If you control a Sacred Beast, your opponent cannot activate cards or effects in response to this card's activation."
            s.Cost = async x => { if (x.MyMonsters().Any(m => m.FaceUp && m.Name is "Uria, Lord of Searing Flames" or "Hamon, Lord of Striking Thunder" or "Raviel, Lord of Phantasms")) x.Self.Vars["noresp"] = x.D.Turn; };
            s.Note = "You take no battle damage from attacks involving the summoned monster.";
        }
        S("Dimensional Fissure").Allow = (x, q, c) => q == Q.BanishAny && c != null && c.Data.IsMonster;
        Spell("Dimensional Prison", async x => { if (x.T0 is { } a && a.Valid()) await x.D.Banish(a, x.Self); }, AttackOnMe, async x => x.T.Add(x.Ev!.Card!));
        S("Disc Fighter").On = async (x, e) =>
        {
            if (e.Kind == EvKind.DamageCalc && e.Card == x.Self && e.Other is { } t && t.Valid() && t.Defense && x.Def(t) >= 2000)
            { await x.D.Destroy(t, x.Self); x.D.BattleFlags.Add("nodamagecalc"); }
        };
        {
            var s = S("Divine Serpent Geh");
            s.SummonOk = (x, by, k) => by == x.Self;
            s.Forbid = (x, q, c) => q == Q.Target && c == x.Self;
            s.AddFx(f =>
            {
                f.Label = "Special Summon (pay half LP)"; f.FromHand = true; f.Speed = 2; f.AnyTurn = true;
                f.Can = x => x.Ev is { Kind: EvKind.Destroyed } e && (e.Battle ? x.D.Attacker?.Controller == x.Opp : e.Other?.Controller == x.Opp)
                    && x.D.LeftFieldLog.LastOrDefault(l => l.c == e.Card) is { c: not null } l && l.controller == x.Me && x.D.FreeMonsterZones(x.Me) > 0;
                f.Cost = async x => x.D.Pay(x.Me, x.My.Lp / 2);
                f.Run = async x => { if (x.Self.Loc == Loc.Hand) await x.D.SpecialSummon(x.Self, x.Me, await x.D.ChoosePosition(x.Me, x.Self), by: x.Self); };
            });
            s.On = async (x, e) =>
            {
                if (e.Kind == EvKind.DamageCalc && e.Card == x.Self && e.Other is { } t && t.Valid())
                {
                    x.D.Flags[$"neg{t.Uid}"] = 1; t.Vars["baseatk"] = t.Data.Atk / 2 + 1; t.Vars["baseatkturn"] = x.D.Turn;
                    x.D.UntilBattleEnds(() => { x.D.Flags.Remove($"neg{t.Uid}"); t.Vars.Remove("baseatkturn"); });
                }
                if (e.Kind == EvKind.DamageCalc && (e.Card == x.Self || e.Other == x.Self) && Up(x) && x.D.FaceUpMonsters.Any() && await x.Ask("Make Divine Serpent Geh's ATK the highest original ATK on the field?"))
                {
                    int top = x.D.FaceUpMonsters.Max(m => m.Data.Atk);
                    x.Self.Vars["baseatk"] = top + 1; x.Self.Vars["baseatkturn"] = x.D.Turn; x.D.Seq++;
                }
            };
            s.Note = "Its \"ATK becomes the highest original ATK\" is offered once per battle at damage calculation (no chain).";
        }
        {
            var s = S("Divine Sword - Phoenix Blade");
            s.EquipOk = (x, m) => x.IsRace(m, "Warrior");
            s.Stats = (x, c, st) => { if (c == x.Self.EquippedTo) st.Add(300, 0); };
            s.GraveFx = new Script
            {
                Speed = 1,
                Can = x => x.My.Grave.Count(c => c.Data.IsMonster && c.Data.Race == "Warrior") >= 2,
                Cost = async x => { foreach (var c in await x.PickN(x.My.Grave.Where(c => c.Data.IsMonster && c.Data.Race == "Warrior"), 2, 2, "Banish 2 Warrior monsters from your GY")) await x.D.Banish(c, x.Self); },
                Run = async x => { if (x.Self.Loc == Loc.Grave) await x.D.ToHand(x.Self, x.Self); },
            };
        }
        Spell("Divine Wrath", async x =>
        {
            var l = x.Ev!.Link!;
            await x.D.Negate(l, destroy: false);
            if (l.Card.Loc == Loc.Monster) await x.D.Destroy(l.Card, x.Self);
        }, x => x.Ev is { Kind: EvKind.Activated, Link: { } l } && l.Card.Data.IsMonster && !l.IsCardActivation && x.My.Hand.Any(c => c != x.Self),
        async x => await x.D.Discard(x.Me, await x.PickN(x.My.Hand.Where(c => c != x.Self), 1, 1, "Discard 1 card"), x.Self));
        Spell("Doble Passe", async x =>
        {
            if (x.Ev?.Other is not { } mine || !mine.Valid()) return;
            int atk = x.Atk(mine);
            x.D.AttackTarget = null;
            x.D.Flags["forceddirect"] = 1;
            var d = x.D;
            d.UntilBattleEnds(() => d.Flags.Remove("forceddirect"));
            await d.Damage(x.Opp, atk, x.Self);
            mine.Vars["direct"] = d.Turn + 1;
        }, x => x.Ev is { Kind: EvKind.AttackDeclared } e && e.Player == x.Opp && e.Other is { } t && t.Controller == x.Me && t.FaceUp && t.Attack);
        {
            var s = Spell("Doctor D", async x =>
            {
                var c = await x.Pick(x.My.Grave.Where(DHero), "Add or Special Summon which Destiny HERO?");
                if (c == null) return;
                bool ss = x.D.CanSpecialSummon(c, x.Me, by: x.Self) && await x.D.ChooseText(x.Me, $"{c.Name}:", new[] { "Add to your hand", "Special Summon it" }, x.Self) == 1;
                if (ss) await x.D.SpecialSummon(c, x.Me, await x.D.ChoosePosition(x.Me, c), by: x.Self); else await x.D.ToHand(c, x.Self);
            }, x => x.My.Grave.Count(DHero) >= 2,
            async x => { var b = await x.Pick(x.My.Grave.Where(DHero), "Banish 1 Destiny HERO from your GY"); if (b != null) await x.D.Banish(b, x.Self); });
            s.GraveFx = new Script
            {
                Speed = 1,
                Can = x => x.Targetable(x.MyMonsters().FaceUp().Where(m => m.In("Destiny HERO"))).Count() >= 2,
                Cost = async x => { await x.D.Banish(x.Self, x.Self); x.T.AddRange(await x.PickN(x.Targetable(x.MyMonsters().FaceUp().Where(m => m.In("Destiny HERO"))), 2, 2, "Target 2 Destiny HERO monsters you control")); },
                Run = async x =>
                {
                    if (x.T.Count < 2 || !x.T.All(t => t.Valid() && t.FaceUp)) return;
                    var a = await x.Pick(x.T, "Which one's ATK becomes the other's?");
                    if (a == null) return;
                    var b = x.T.First(t => t != a);
                    x.D.Boost(a, x.Atk(b) - x.Atk(a));
                },
            };
        }
        {
            var s = Spell("Doll Hammer", async x =>
            {
                if (x.T0 is not { } t || !t.Valid()) return;
                await x.D.Destroy(t, x.Self);
                if (t.Loc == Loc.Monster) return;
                await x.D.Draw(x.Me, 2);
                if (x.OppMonsters().Any() && await x.Ask("Change the battle position of 1 monster your opponent controls?"))
                { var m = await x.Pick(x.OppMonsters(), "Which monster?"); if (m != null) await x.D.ChangePosition(m, !m.Defense, m.FaceUp ? null : true); }
            }, x => Hopt(x, "1") && x.Targetable(x.MyMonsters()).Any(), async x => { UseHopt(x, "1"); await x.Target(x.MyMonsters(), "Destroy which monster you control?"); });
            s.On = async (x, e) =>
            {
                if (e.Kind == EvKind.Summoned && e.From == Loc.Grave && e.Player == x.Me && x.Self.Loc == Loc.Grave && Hopt(x, "2") && x.D.Phase != Phase.Battle && await x.Ask("Add Doll Hammer to your hand?"))
                { UseHopt(x, "2"); await x.D.ToHand(x.Self, x.Self); }
            };
        }
        OnBattleDamage("Don Zaloog", async (x, e) =>
        {
            int i = await x.D.ChooseText(x.Me, "Don Zaloog: activate an effect?", new[] { "Discard 1 random card from their hand", "Send the top 2 cards of their Deck to the GY", "No" }, x.Self);
            if (i == 0 && x.Their.Hand.Count > 0) await x.D.DiscardRandom(x.Opp, 1, x.Self);
            if (i == 1) foreach (var c in x.Their.Deck.Take(2).ToList()) await x.D.ToGrave(c, x.Self);
        });
        {
            var s = S("Doom Virus Dragon");
            s.SummonOk = (x, by, k) => by?.Name == "The Fang of Critias";
            s.On = async (x, e) =>
            {
                if (!e.Is(EvKind.Summoned, x.Self) || e.Arg == (int)SummonKind.Flip) return;
                x.D.Reveal(x.Me, x.Their.Hand);
                await x.D.Destroy(x.OppMonsters().Where(m => x.Atk(m) >= 1500 || (!m.FaceUp && m.Data.Atk >= 1500)).ToList(), x.Self);
                foreach (var c in x.Their.Hand.Where(c => c.Data.IsMonster && c.Data.Atk >= 1500).ToList()) await DestroyInHand(x, c);
                var d = x.D; int opp = x.Opp, turns = 0, me = x.Me; var self = x.Self;
                d.Watch(int.MaxValue, async ev =>
                {
                    if (turns >= 3) return;
                    if (ev.Kind == EvKind.EndPhase && ev.Player == opp) turns++;
                    if (ev.Kind == EvKind.Drew && ev.Player == opp)
                    {
                        d.Reveal(me, ev.Cards!);
                        foreach (var c in ev.Cards!.Where(c => c.Loc == Loc.Hand && c.Data.IsMonster && c.Data.Atk >= 1500).ToList()) await DestroyInHand(d.X(self), c);
                    }
                });
            };
        }
        {
            var s = S("Doomcaliber Knight");
            s.SummonOk = (x, by, k) => false;
            s.Speed = 2; s.AnyTurn = true;
            s.Can = x => Up(x) && x.Ev is { Kind: EvKind.Activated, Link: { } l } && l.Card.Data.IsMonster && !l.IsCardActivation && l.Card != x.Self;
            s.Cost = async x => await x.D.Tribute(x.Self, x.Self);
            s.Run = async x => { var l = x.Ev!.Link!; await x.D.Negate(l, destroy: false); if (l.Card.Loc == Loc.Monster) await x.D.Destroy(l.Card, x.Self); };
        }
        S("Double Coston").TributeWeight = (x, t, forCard) => t == x.Self && (forCard.Loc == Loc.Hand ? forCard.Data.Attribute : x.D.Attr(forCard)) == "DARK" ? 2 : 1;
        Spell("Double Fusion", async x =>
        {
            if (!await x.D.FusionSummon(x.Me, x.Self)) return;
            if (x.D.FusionOptions(x.Me).Any() && await x.Ask("Fusion Summon 1 more Fusion Monster?")) await x.D.FusionSummon(x.Me, x.Self);
        }, x => x.My.Lp > 500 && x.D.FusionOptions(x.Me).Any(), async x => x.D.Pay(x.Me, 500));
        Spell("Double Magical Arm Bind", async x =>
        {
            var d = x.D; int me = x.Me;
            foreach (var t in x.T.Where(t => t.Valid() && t.FaceUp).ToList())
                if (await d.TakeControl(t, me))
                {
                    int end = d.TurnPlayer == me ? d.Turn : d.Turn + 1;
                    d.Later(end, Phase.End, async () => { if (t.Valid() && t.Controller == me) await d.TakeControl(t, t.Owner); });
                }
        }, x => x.D.Tributable(x.Me).Count() >= 2 && x.Targetable(x.OppMonsters().FaceUp()).Count() >= 2,
        async x =>
        {
            foreach (var t in await x.PickN(x.D.Tributable(x.Me), 2, 2, "Tribute 2 monsters")) await x.D.Tribute(t, x.Self);
            x.T.AddRange(await x.PickN(x.Targetable(x.OppMonsters().FaceUp()), 2, 2, "Take control of which 2 monsters?"));
        });
        {
            static bool NegatesTraps(Card c) => Regex.IsMatch(c.Data.Desc, @"([Nn]egate[^.]*Trap|Trap Cards cannot be activated|cannot activate Trap)") || c.Name is "Jinzo" or "Royal Decree" or "Fiend Skull Dragon" or "The Dragon's Bead";
            Spell("Double Snare", async x =>
            {
                var t = await x.Pick(x.D.FieldCards.Where(c => c.FaceUp && NegatesTraps(c)), "Destroy which card that negates Traps?");
                if (t != null) await x.D.Destroy(t, x.Self);
            }, x => x.D.FieldCards.Any(c => c.FaceUp && NegatesTraps(c)));
        }
        Spell("Double Spell", async x =>
        {
            if (x.T0 is not { Loc: Loc.Grave } t) return;
            bool field = t.Data.Is("Field");
            if (field ? x.My.FieldZone != null : x.My.FreeSpellZones == 0) return;
            x.D.Place(t, x.Me, field ? Loc.Field : Loc.Spell);
            t.FaceUp = true;
            var tx = x.D.X(t);
            var ts = x.D.S(t);
            if (ts?.Cost != null) await ts.Cost(tx);
            if (ts?.Run != null) await ts.Run(tx);
            else if (t.Data.Is("Equip") && x.D.EquipTargets(t).Any()) { var m = await x.Pick(x.D.EquipTargets(t), $"Equip {t.Name} to which monster?"); if (m != null) await x.D.Equip(t, m); }
            bool stays = t.Data.Is("Continuous") || t.Data.Is("Field") || (t.Data.Is("Equip") && t.EquippedTo != null) || t.Var("stay") == 1;
            if (t.OnField && !stays) await x.D.ToGrave(t, x.Self);
        }, x => x.My.Hand.Any(c => c != x.Self && c.Data.IsSpell) && x.Targetable(x.Their.Grave.Where(c => c.Data.IsSpell)).Any(),
        async x =>
        {
            await x.D.Discard(x.Me, await x.PickN(x.My.Hand.Where(c => c != x.Self && c.Data.IsSpell), 1, 1, "Discard 1 Spell"), x.Self);
            await x.Target(x.Their.Grave.Where(c => c.Data.IsSpell), "Use which Spell from your opponent's GY?");
        });
        S("Double Spell").Note = "The Spell's own conditions are not re-checked; its targets are chosen when Double Spell resolves.";
        Spell("Double Summon", async x => x.My.ExtraSummons = Math.Max(x.My.ExtraSummons, 1));
        {
            var s = S("Dragon Master Knight");
            s.Materials = new() { Named("Black Luster Soldier"), Named("Blue-Eyes Ultimate Dragon") };
            s.Stats = (x, c, st) => { if (c == x.Self) st.Add(500 * x.MyMonsters().Count(m => m != x.Self && m.FaceUp && x.IsRace(m, "Dragon")), 0); };
        }
        {
            var s = S("Dragon Nails");
            s.EquipOk = (x, m) => x.IsAttr(m, "DARK");
            s.Stats = (x, c, st) => { if (c == x.Self.EquippedTo) st.Add(600, 0); };
            s.Forbid = (x, q, c) => q == Q.DestroyByEffect && c != null && c == x.Self.EquippedTo && x.D.Destroyer?.Controller == x.Opp;
            s.On = async (x, e) =>
            {
                if (e.Is(EvKind.Equipped, x.Self)) Tie(x, e.Other!);
                if (e.Kind == EvKind.Destroyed && e.Battle && e.Card == TiedMonster(x) && e.Card!.Loc == Loc.Grave && x.Self.Loc == Loc.Grave && Hopt(x)
                    && x.D.CanSpecialSummon(e.Card, x.Me, by: x.Self) && x.My.FreeSpellZones > 0 && await x.Ask($"Special Summon {e.Card.Name} and re-equip Dragon Nails?"))
                {
                    UseHopt(x);
                    var m = e.Card;
                    if (!await x.D.SpecialSummon(m, x.Me, await x.D.ChoosePosition(x.Me, m), by: x.Self)) return;
                    await x.D.Equip(x.Self, m);
                    x.Self.Vars["banishleave"] = 1;
                }
            };
            s.GraveToBanish = (x, from, battle, eff) => from == Loc.Spell && x.Self.Var("banishleave") == 1;
        }
        Spell("Dragon's Gunfire", async x =>
        {
            if (x.N == 0) await x.D.Damage(x.Opp, 800, x.Self);
            else if (x.T0 is { } t && t.Valid() && x.Def(t) <= 800) await x.D.Destroy(t, x.Self);
        }, x => x.MyMonsters().Any(m => m.FaceUp && x.IsRace(m, "Dragon")),
        async x =>
        {
            var l = x.Targetable(x.AllMonsters().Where(m => m.FaceUp && x.Def(m) <= 800)).ToList();
            x.N = l.Count == 0 ? 0 : await x.D.ChooseText(x.Me, "Dragon's Gunfire", new[] { "Inflict 800 damage", "Destroy a monster with 800 or less DEF" }, x.Self);
            if (x.N == 1) await x.Target(l, "Destroy which monster?");
        });
        {
            static IEnumerable<Card> Pool(Ctx x) => x.MyMonsters().Concat(x.My.Grave.Where(c => c.Data.IsMonster));
            Spell("Dragon's Mirror", async x => await x.D.FusionSummon(x.Me, x.Self, pool: Pool(x), resultOk: c => c.Data.Race == "Dragon", matsTo: Loc.Banished),
                x => x.D.FusionOptions(x.Me, pool: Pool(x), resultOk: c => c.Data.Race == "Dragon").Any());
        }
        S("Dragon's Rage").Allow = (x, q, c) => q == Q.Piercing && c != null && c.Controller == x.Me && x.IsRace(c, "Dragon");
        {
            var s = S("Dragonroid");
            s.AddFx(f =>
            {
                f.Label = "Become a Dragon (in the GY)"; f.FromGrave = true; f.OncePerTurn = true;
                f.Run = async x => { x.Self.Vars["dragonturn"] = x.D.Turn; x.D.Seq++; };
            });
            s.AddFx(f =>
            {
                f.Label = "Discard: an effect"; f.FromHand = true;
                f.Can = x => Hopt(x);
                f.Cost = async x =>
                {
                    UseHopt(x);
                    await x.D.Discard(x.Me, new[] { x.Self }, x.Self);
                    x.N = x.My.Deck.Any(c => c.Data.IsMonster && c.In("roid") && c.Data.Attribute != "WIND")
                        ? await x.D.ChooseText(x.Me, "Dragonroid", new[] { "Add a non-WIND \"roid\" monster", "Fusion effects can't be negated this turn" }, x.Self) : 1;
                };
                f.Run = async x =>
                {
                    if (x.N == 0) await x.Search(c => c.Data.IsMonster && c.In("roid") && c.Data.Attribute != "WIND", "Add which \"roid\" monster?");
                    else x.D.TurnFlags.Add($"fusionlock{x.Me}");
                };
            });
            s.Note = "As a Dragon in the GY it counts for Dragon materials only through cards that ask the engine its Type there (few do). The no-negate choice has nothing to stop in the current card pool.";
        }
        Spell("Draining Shield", async x =>
        {
            if (x.T0 is not { } a || !a.Valid()) return;
            x.D.AttackNegated = true;
            await x.D.Gain(x.Me, x.Atk(a), x.Self);
        }, AttackOnMe, async x => x.T.Add(x.Ev!.Card!));
        Spell("Dramatic Rescue", async x =>
        {
            var l = x.Ev!.Link!;
            var t = l.X.T.FirstOrDefault(c => c.Valid() && c.In("Amazoness"));
            if (t == null) return;
            await x.D.ToHand(t, x.Self);
            await SummonPick(x, x.My.Hand.Where(c => c != t && c.Data.IsMonster), "Special Summon 1 other monster from your hand");
        }, x => x.Ev is { Kind: EvKind.Activated, Link: { } l } && l.X.T.Any(t => t.Loc == Loc.Monster && t.FaceUp && t.In("Amazoness")));
        S("Drillago").Allow = (x, q, c) => q == Q.DirectAttack && c == x.Self && !x.Their.SpellsOnField.Any() && x.OppMonsters().Any() && x.OppMonsters().All(m => m.FaceUp && x.Atk(m) >= 1600);
        S("Drillroid").On = async (x, e) =>
        {
            if (e.Kind == EvKind.DamageCalc && e.Card == x.Self && e.Other is { } t && t.Valid() && t.Defense) await x.D.Destroy(t, x.Self);
        };
        Spell("Drop Off", async x =>
        {
            var drawn = x.Ev!.Cards!.Where(c => c.Loc == Loc.Hand).ToList();
            if (drawn.Count > 0) await x.D.Discard(x.Opp, drawn.Take(1).ToList(), x.Self);
        }, x => x.Ev is { Kind: EvKind.Drew, Arg: 1 } e && e.Player == x.Opp);
        Flip("Dummy Golem", async x =>
        {
            var t = await x.D.PickOne(x.Opp, x.OppMonsters().Where(m => m != x.Self), "Choose 1 monster you control to switch with Dummy Golem", x.Self);
            if (t == null || !x.Self.Valid()) return;
            int zs = x.Self.Zone, zt = t.Zone;
            x.My.Monsters[zs] = t; x.Their.Monsters[zt] = x.Self;
            (x.Self.Zone, t.Zone) = (zt, zs);
            int me = x.Me; x.Self.Controller = x.Opp; t.Controller = me;
            await x.D.Raise(new Ev { Kind = EvKind.ControlChanged, Card = t, Player = me, Arg = 1 - me });
            await x.D.Raise(new Ev { Kind = EvKind.ControlChanged, Card = x.Self, Player = 1 - me, Arg = me });
        });
        {
            var s = S("Dyna Base");
            s.AddFx(f =>
            {
                f.Label = "Special Summon"; f.FromHand = true;
                f.Can = x => Hopt(x, "1") && x.My.Lp < x.Their.Lp && x.D.FreeMonsterZones(x.Me) > 0;
                f.Cost = async x => UseHopt(x, "1");
                f.Run = async x => { if (x.Self.Loc == Loc.Hand) await x.D.SpecialSummon(x.Self, x.Me, await x.D.ChoosePosition(x.Me, x.Self), by: x.Self); };
            });
            s.AddFx(f =>
            {
                f.Label = "Fusion Summon";
                f.Can = x => Hopt(x, "2") && x.D.FusionOptions(x.Me).Any(o => o.mats.Any(m => m.Ok(x.Self)));
                f.Cost = async x => UseHopt(x, "2");
                f.Run = async x => await x.D.FusionSummon(x.Me, x.Self);
            });
            s.Note = "Its Fusion Summon must use Dyna Base: choose it as one of the materials.";
        }
        {
            var s = S("Dyna Tank");
            s.Materials = new() { Cond("1 Machine monster", c => c.Data.IsMonster && c.Data.Race == "Machine"), Cond("1 Dinosaur monster", c => c.Data.IsMonster && c.Data.Race == "Dinosaur") };
            s.Stats = (x, c, st) =>
            {
                if (c != x.Self) return;
                var dino = Enumerable.Range(0, 3).Select(i => x.Self.Var($"mat{i}")).Where(u => u > 0).Select(u => x.D.CardByUid(u - 1)).FirstOrDefault(m => m.Data.Race == "Dinosaur");
                st.BaseAtk = dino?.Data.Atk ?? 0;
            };
            s.AddFx(f =>
            {
                f.Label = "Redirect the effect"; f.Speed = 2; f.AnyTurn = true;
                f.Can = x => Hopt(x, "1") && x.Ev is { Kind: EvKind.Activated, Link: { } l } && l.X.T.Count == 1 && l.X.T0 == x.Self
                    && x.D.FieldCards.Any(c => c != x.Self && c.Loc == x.Self.Loc);
                f.Cost = async x => { UseHopt(x, "1"); await x.Target(x.D.FieldCards.Where(c => c != x.Self && c.Loc == x.Self.Loc), "Which card becomes the new target?"); };
                f.Run = async x => { if (x.Ev!.Link is { } l && x.T0 is { } nt && nt.OnField) { l.X.T.Clear(); l.X.T.Add(nt); } };
            });
            s.On = async (x, e) =>
            {
                if (e.Is(EvKind.Destroyed, x.Self) && e.Other?.Controller == x.Opp && x.Self.Owner == x.Me && Hopt(x, "2") && x.My.Grave.Any(c => c.Data.IsMonster && c.Data.Race == "Dinosaur")
                    && await x.Ask("Special Summon a Dinosaur from your GY?"))
                { UseHopt(x, "2"); await SummonPick(x, x.My.Grave.Where(c => c.Data.IsMonster && c.Data.Race == "Dinosaur"), "Special Summon which Dinosaur?"); }
            };
        }
        Spell("E - Emergency Call", x => x.Search(c => c.Data.IsMonster && c.In("Elemental HERO"), "Add which Elemental HERO?"), x => x.My.Deck.Any(c => c.Data.IsMonster && c.In("Elemental HERO")));
        Spell("Earthquake", async x => { foreach (var m in x.D.FaceUpMonsters.Where(m => m.Attack).ToList()) await x.D.ChangePosition(m, true); }, x => x.D.FaceUpMonsters.Any(m => m.Attack));
        MyStandby("Ebon Magician Curran", async x => { if (x.OppMonsters().Any()) await x.D.Damage(x.Opp, 300 * x.OppMonsters().Count(), x.Self); });
        S("Ectoplasmer").On = async (x, e) =>
        {
            if (e.Kind != EvKind.EndPhase || !x.Self.OnField || !x.Self.FaceUp) return;
            int tp = e.Player;
            var l = x.D.P(tp).MonstersOnField.Where(m => m.FaceUp && !x.D.Forbidden(Q.Tribute, m)).ToList();
            if (l.Count == 0) return;
            var t = await x.D.PickOne(tp, l, "Ectoplasmer: Tribute 1 face-up monster", x.Self);
            if (t == null) return;
            int half = t.Data.Atk / 2;
            await x.D.Tribute(t, x.Self);
            await x.D.Damage(1 - tp, half, x.Self);
        };
        Spell("Edge Hammer", async x =>
        {
            if (x.T0 is not { } t || !t.Valid()) return;
            int atk = t.Data.Atk;
            await x.D.Destroy(t, x.Self);
            if (t.Loc != Loc.Monster) await x.D.Damage(x.Opp, atk, x.Self);
        }, x => x.MyMonsters().Any(m => m.Name == "Elemental HERO Bladedge" && !x.D.Forbidden(Q.Tribute, m)) && x.Targetable(x.OppMonsters()).Any(),
        async x =>
        {
            var b = await x.Pick(x.MyMonsters().Where(m => m.Name == "Elemental HERO Bladedge"), "Tribute Elemental HERO Bladedge");
            if (b != null) await x.D.Tribute(b, x.Self);
            await x.Target(x.OppMonsters(), "Destroy which monster?");
        });
        {
            var s = S("Electromagnetic Turtle");
            s.AddFx(f =>
            {
                f.Label = "End the Battle Phase"; f.FromGrave = true; f.Speed = 2; f.AnyTurn = true;
                f.Can = x => x.D.TurnPlayer == x.Opp && x.D.Phase == Phase.Battle && x.D.Flags.GetValueOrDefault($"turtle{x.Me}") == 0;
                f.Cost = async x => { x.D.Flags[$"turtle{x.Me}"] = 1; await x.D.Banish(x.Self, x.Self); };
                f.Run = async x => { x.D.AttackNegated = true; x.D.TurnFlags.Add("endbattle"); };
            });
        }
        static async Flow AgainInARow(Ctx x)
        {
            if (await x.Ask($"{x.Self.Name}: attack once again in a row?")) { x.Self.Vars["maxatk"] = x.Self.Attacks + 1; x.Self.Vars["maxatkturn"] = x.D.Turn; }
        }
        {
            var s = SelfBoost("Element Dragon", x => AttrOnField(x, "FIRE") ? 500 : 0);
            s.On = async (x, e) => { if (e.Kind == EvKind.BattleDestroyedMonster && e.Card == x.Self && e.Other!.Controller == x.Opp && Up(x) && AttrOnField(x, "WIND")) await AgainInARow(x); };
        }
        {
            var s = S("Element Magician");
            s.On = async (x, e) => { if (e.Kind == EvKind.BattleDestroyedMonster && e.Card == x.Self && e.Other!.Controller == x.Opp && Up(x) && AttrOnField(x, "WIND")) await AgainInARow(x); };
            s.Note = "WATER (control can't switch) is applied by cards that take control asking it; most current ones do.";
        }
        {
            var s = SelfBoost("Element Saurus", x => AttrOnField(x, "FIRE") ? 500 : 0);
            s.On = async (x, e) => { if (e.Kind == EvKind.BattleDestroyedMonster && e.Card == x.Self && e.Other!.Data.IsEffect && AttrOnField(x, "EARTH")) x.D.Flags[$"neg{e.Other.Uid}"] = 1; };
        }
        {
            var s = Spell("Elemental Absorber", async x =>
            {
                var c = await x.Pick(x.My.Hand.Where(c => c.Data.IsMonster), "Banish 1 monster from your hand");
                if (c == null) return;
                await x.D.Banish(c, x.Self);
                x.Self.Vars["absorb"] = Array.IndexOf(AllAttributes, c.Data.Attribute) + 1;
            }, x => x.My.Hand.Any(c => c.Data.IsMonster));
            s.Forbid = (x, q, c) => q == Q.Attack && c != null && c.Controller == x.Opp && x.Self.Var("absorb") > 0 && x.D.Attr(c) == AllAttributes[x.Self.Var("absorb") - 1];
        }
        {
            static bool Burst(Ctx x) => new[] { "WIND", "WATER", "FIRE", "EARTH" }.All(a => x.D.Tributable(x.Me).Any(m => (m.FaceUp ? x.D.Attr(m) : m.Data.Attribute) == a));
            Spell("Elemental Burst", async x => await x.D.Destroy(x.Their.FieldCards.ToList(), x.Self), x => Burst(x) && x.Their.FieldCards.Any(),
            async x =>
            {
                foreach (var a in new[] { "WIND", "WATER", "FIRE", "EARTH" })
                {
                    var t = await x.Pick(x.D.Tributable(x.Me).Where(m => (m.FaceUp ? x.D.Attr(m) : m.Data.Attribute) == a), $"Tribute 1 {a} monster");
                    if (t != null) await x.D.Tribute(t, x.Self);
                }
            });
        }
        {
            var s = Contact("Elemental HERO Air Neos");
            s.Stats = (x, c, st) => { if (c == x.Self && x.My.Lp < x.Their.Lp) st.Add(x.Their.Lp - x.My.Lp, 0); };
        }
    }
}
