namespace PoC.Engine;

// Official cards, batch 13 (Goddess Bow .. Heart of the Underdog).
public static partial class Scripts
{
    /// "Cannot be Summoned unless you control a face-up X" (Normal and Special Summons).
    static void OnlyWith(Script s, string name)
    {
        bool Has(Ctx x) => x.My.FieldCards.Any(c => c.FaceUp && c.Name == name);
        s.NormalSummonOk = Has;
        s.SummonOk = (x, by, k) => Has(x);
    }

    /// "Once per turn: You can change this card to face-down Defense Position."
    static void FlipDown(Script s)
    {
        s.OncePerTurn = true;
        s.Can = x => Up(x);
        s.Run = async x => { if (Up(x)) await x.D.SetFaceDown(x.Self); };
    }

    static void CardsP13(CardDb db)
    {
        {
            var s = S("Goddess Bow");
            s.SummonOk = (x, by, k) => by?.Name == "The Claw of Hermos";
            s.On = async (x, e) =>
            {
                if (e.Is(EvKind.Summoned, x.Self) && e.Arg is (int)SummonKind.Special or (int)SummonKind.Fusion && x.My.FreeSpellZones > 0)
                {
                    var t = await x.Pick(x.Targetable(x.D.FaceUpMonsters.Where(m => m != x.Self)), "Equip Goddess Bow to which monster?");
                    if (t != null && x.Self.Valid()) { x.Self.ResetFieldState(); x.D.Place(x.Self, x.Me, Loc.Spell); await x.D.Equip(x.Self, t); x.Self.Vars["bow"] = 1; }
                }
                if (e.Kind == EvKind.Activated && e.Link is { } l && l.Player == x.Opp && x.D.Phase == Phase.Battle && x.Self.Var("bow") == 1 && x.Self.EquippedTo is { } m && x.Self.Var("bowturn") != x.D.Turn)
                {
                    x.Self.Vars["bowturn"] = x.D.Turn;
                    l.Negated = true;
                    m.Vars["maxatk"] = 2; m.Vars["maxatkturn"] = x.D.Turn;
                }
            };
        }
        {
            static bool AllValk(Ctx x) => x.MyMonsters().Any() && x.MyMonsters().All(m => m.FaceUp && m.In("Valkyrie"));
            var s = Spell("Goddess Skuld's Oracle", async x =>
            {
                if (AllValk(x) && x.My.Deck.Any(c => c.Name == "Goddess Verdande's Guidance") && await x.Ask("Add Goddess Verdande's Guidance from your Deck?")) await x.Search(c => c.Name == "Goddess Verdande's Guidance", "Add it");
            }, x => Hopt(x, "act"), async x => UseHopt(x, "act"));
            s.AddFx(f =>
            {
                f.Label = "Look at their top 3"; f.OncePerTurn = true;
                f.Can = x => x.Their.Deck.Count >= 3;
                f.Run = async x =>
                {
                    var top = x.Their.Deck.Take(3).ToList();
                    x.D.Reveal(x.Me, top);
                    var order = await x.PickN(top, 3, 3, "Put them back: pick in order, first = new top card");
                    for (int i = order.Count - 1; i >= 0; i--) { x.Their.Deck.Remove(order[i]); x.Their.Deck.Insert(0, order[i]); }
                    var d = x.D; int me = x.Me;
                    d.Linger(d.Turn, (q, c, p) => q is Q.NormalSummon or Q.SpecialSummon && c != null && (p == me || c.Controller == me) && c.Data.Race != "Fairy");
                };
            });
        }
        {
            var s = Spell("Goddess Urd's Verdict", async x => { });
            s.Run = null;
            s.Forbid = (x, q, c) => c != null && c.Controller == x.Me && c.Loc == Loc.Monster && c.FaceUp && c.In("Valkyrie")
                && ((q == Q.Target && x.D.QueryPlayer == x.Opp) || (q == Q.DestroyByEffect && x.D.Destroyer?.Controller == x.Opp));
            s.AddFx(f =>
            {
                f.Label = "Declare and reveal a Set card"; f.OncePerTurn = true;
                f.Can = x => x.Targetable(x.Their.FieldCards.Where(c => !c.FaceUp)).Any();
                f.Cost = async x => { x.N = (await DeclareName(x, "Declare 1 card name")).Idx; await x.Target(x.Their.FieldCards.Where(c => !c.FaceUp), "Reveal which Set card?"); };
                f.Run = async x =>
                {
                    if (x.T0 is not { OnField: true, FaceUp: false } t) return;
                    x.D.Reveal(x.Me, t); x.D.Reveal(x.Opp, t);
                    if (t.Name == x.D.Db.Get(x.N).Name) await x.D.Banish(t, x.Self);
                    else { var mine = await x.Pick(x.My.FieldCards, "Banish 1 card you control"); if (mine != null) await x.D.Banish(mine, x.Self); }
                };
            });
        }
        {
            static bool AllValk(Ctx x) => x.MyMonsters().Any() && x.MyMonsters().All(m => m.FaceUp && m.In("Valkyrie"));
            var s = Spell("Goddess Verdande's Guidance", async x =>
            {
                if (AllValk(x) && x.My.Deck.Any(c => c.Name == "Goddess Urd's Verdict") && await x.Ask("Add Goddess Urd's Verdict from your Deck?")) await x.Search(c => c.Name == "Goddess Urd's Verdict", "Add it");
            }, x => Hopt(x, "act"), async x => UseHopt(x, "act"));
            s.AddFx(f =>
            {
                f.Label = "Declare a card type"; f.OncePerTurn = true;
                f.Can = x => x.Their.Deck.Count > 0;
                f.Cost = async x => x.N = await x.D.ChooseText(x.Me, "Declare a type", new[] { "Monster", "Spell", "Trap" }, x.Self);
                f.Run = async x =>
                {
                    if (x.Their.Deck.Count == 0) return;
                    var top = x.Their.Deck[0];
                    x.D.Reveal(x.Me, top); x.D.Reveal(x.Opp, top);
                    bool hit = x.N switch { 0 => top.Data.IsMonster, 1 => top.Data.IsSpell, _ => top.Data.IsTrap };
                    if (hit && (top.Data.IsMonster ? x.D.FreeMonsterZones(x.Opp) > 0 : top.Data.Is("Field") ? x.Their.FieldZone == null : x.Their.FreeSpellZones > 0))
                    {
                        if (top.Data.IsMonster) await x.D.SpecialSummon(top, x.Opp, true, faceUp: false, by: x.Self);
                        else if (x.D.Place(top, x.Opp, top.Data.Is("Field") ? Loc.Field : Loc.Spell)) { top.FaceUp = false; top.SetTurn = x.D.Turn; await x.D.Raise(new Ev { Kind = EvKind.Set, Card = top, Player = x.Opp }); }
                    }
                    else await x.D.ToHand(top, x.Self);
                };
            });
            s.Note = "A declared Monster is Set face-down in Defense Position on their field.";
        }
        Spell("Gold Moon Coin", async x =>
        {
            foreach (var c in await x.PickN(x.My.Hand, 2, 2, "Give 2 cards from your hand to your opponent")) x.D.TakeToHand(c, x.Opp);
            await x.D.Draw(x.Me, 2);
        }, x => Hopt(x) && x.My.Hand.Count(c => c != x.Self) >= 2 && x.My.Deck.Count >= 2, async x => UseHopt(x));
        Spell("Gold Sarcophagus", async x =>
        {
            var c = await x.Pick(x.My.Deck, "Banish which card from your Deck?");
            x.D.Shuffle(x.Me);
            if (c == null) return;
            await x.D.Banish(c, x.Self);
            var d = x.D; int me = x.Me;
            int second = d.TurnPlayer == me ? d.Turn + 4 : d.Turn + 3;
            d.Later(second, Phase.Standby, async () => { if (c.Loc == Loc.Banished) await d.ToHand(c); });
        }, x => x.My.Deck.Count > 0);
        {
            static bool Mentions(CardData d) => d.IsMonster && Scripts.Mentions(d, "Golden Castle of Stromberg");
            var s = S("Golden Castle of Stromberg");
            s.On = async (x, e) =>
            {
                if (!x.Self.OnField || !x.Self.FaceUp) return;
                if (e.Kind == EvKind.Standby && e.Player == x.Me)
                {
                    if (x.My.Deck.Count >= 10) foreach (var c in x.My.Deck.Take(10).ToList()) { await x.D.Banish(c, x.Self); c.FaceUp = false; }
                    else await x.D.Destroy(x.Self, x.Self);
                }
                if (e.Kind == EvKind.AttackDeclared && e.Player == x.Opp && e.Card is { } a && a.Valid())
                {
                    int half = x.Atk(a) / 2;
                    await x.D.Destroy(a, x.Self);
                    if (!a.Valid()) await x.D.Damage(x.Opp, half, x.Self);
                }
            };
            s.AddFx(f =>
            {
                f.Label = "Summon from the Deck";
                f.Can = x => Hopt(x) && !x.My.NormalSummoned && x.D.FreeMonsterZones(x.Me) > 0 && x.My.Deck.Any(c => Mentions(c.Data));
                f.Cost = async x => { UseHopt(x); x.My.NormalSummoned = true; x.My.ExtraSummons = 0; };
                f.Run = async x => { await SummonPick(x, x.My.Deck.Where(c => Mentions(c.Data)), "Special Summon which monster?"); x.D.Shuffle(x.Me); };
            });
            s.Note = "Using its summon effect spends your Normal Summon for the turn (you can't Normal Summon/Set that turn).";
        }
        S("Golden Homunculus").Stats = (x, c, st) => { if (c == x.Self) st.Add(300 * x.My.Banished.Count, 300 * x.My.Banished.Count); };
        {
            var s = Spell("Golden Rule", async x =>
            {
                var placed = new List<string>();
                for (int i = 0; i < 2; i++)
                {
                    var c = await x.Pick(x.My.Deck.Where(c => c.Data.IsMonster && c.In("Crystal Beast") && !placed.Contains(c.Name)), "Place a Crystal Beast from your Deck as a Continuous Spell");
                    if (c == null || !await PlaceAsSpell(x, c)) break;
                    placed.Add(c.Name);
                }
                x.D.Shuffle(x.Me);
                var m = await x.Pick(x.My.Hand.Concat(x.My.Grave).Where(c => c.Data.IsMonster && c.In("Crystal Beast") && !placed.Contains(c.Name) && x.D.CanSpecialSummon(c, x.Me, by: x.Self)), "Special Summon which Crystal Beast?");
                if (m == null || !await x.D.SpecialSummon(m, x.Me, await x.D.ChoosePosition(x.Me, m), by: x.Self)) return;
                await x.D.Equip(x.Self, m);
                Tie(x, m);
            }, x => Hopt(x) && x.My.FreeSpellZones >= 3 && x.My.Deck.Where(c => c.Data.IsMonster && c.In("Crystal Beast")).Select(c => c.Name).Distinct().Count() >= 2, async x => UseHopt(x));
            s.AlsoIn = new[] { "Crystal" };
            s.EquipOk = (x, m) => true;
            s.On = async (x, e) => { if (e.Is(EvKind.LeftField, x.Self) && TiedMonster(x) is { Loc: Loc.Monster } m) { x.D.Flags.Remove($"tie{x.Self.Uid}"); await x.D.Destroy(m, x.Self); } };
        }
        {
            var s = S("Golem Sentry");
            FlipDown(s);
            s.On = async (x, e) =>
            {
                if (e.Is(EvKind.Summoned, x.Self) && e.Arg == (int)SummonKind.Flip && x.Targetable(x.OppMonsters()).Any())
                { var t = await x.Pick(x.Targetable(x.OppMonsters()), "Return which monster to the hand?"); if (t != null) await x.D.ToHand(t, x.Self); }
            };
        }
        Spell("Gorgon's Eye", async x => { var d = x.D; d.Linger(d.Turn, (q, c, p) => q == Q.Negate && c != null && c.Loc == Loc.Monster && c.Defense); });
        {
            var s = S("Gradius' Option");
            s.SummonOk = (x, by, k) => by == x.Self;
            s.ProcCan = x => x.MyMonsters().Any(m => m.FaceUp && m.Name == "Gradius");
            s.Proc = async x =>
            {
                var g = await x.Pick(x.MyMonsters().Where(m => m.FaceUp && m.Name == "Gradius"), "Select Gradius");
                if (g == null || !await x.D.SpecialSummon(x.Self, x.Me, await x.D.ChoosePosition(x.Me, x.Self), by: x.Self)) return;
                Tie(x, g);
            };
            s.Base = (x, c, st) => { if (c == x.Self && TiedMonster(x) is { Loc: Loc.Monster, FaceUp: true } g) { st.BaseAtk = x.Atk(g); st.BaseDef = x.Def(g); } };
            s.On = async (x, e) => { if (e.Kind == EvKind.LeftField && e.Card == TiedMonster(x) && Up(x)) { x.D.Flags.Remove($"tie{x.Self.Uid}"); await x.D.Destroy(x.Self, x.Self); } };
        }
        Spell("Grand Convergence", async x => { await x.D.Damage(x.Opp, 300, x.Self); await x.D.Destroy(x.AllMonsters().ToList(), x.Self); }, x => x.D.CountActive("Macro Cosmos", x.Me) > 0);
        S("Granmarg the Rock Monarch").On = async (x, e) =>
        {
            if (e.Is(EvKind.Summoned, x.Self) && e.Arg == (int)SummonKind.Tribute && x.Targetable(x.D.FieldCards.Where(c => !c.FaceUp)).Any())
            { var t = await x.Pick(x.Targetable(x.D.FieldCards.Where(c => !c.FaceUp)), "Destroy which Set card?"); if (t != null && !t.FaceUp) await x.D.Destroy(t, x.Self); }
        };
        OnBattleDeath("Grave Squirmer", async x => { var t = await x.Pick(x.Targetable(x.D.FieldCards), "Destroy which card?"); if (t != null) await x.D.Destroy(t, x.Self); });
        S("Gravekeeper's Assailant").On = async (x, e) =>
        {
            if (e.Kind == EvKind.AttackDeclared && e.Card == x.Self && x.D.CountActive("Necrovalley") > 0 && x.Targetable(x.OppMonsters().FaceUp()).Any() && await x.Ask("Change the battle position of 1 face-up opponent's monster?"))
            { var t = await x.Pick(x.Targetable(x.OppMonsters().FaceUp()), "Which monster?"); if (t != null) await x.D.ChangePosition(t, !t.Defense); }
        };
        {
            var s = S("Gravekeeper's Chief");
            s.Forbid = (x, q, c) => q is Q.NormalSummon or Q.SetMonster or Q.SpecialSummon or Q.FlipSummon && c != null && c != x.Self && c.Name == "Gravekeeper's Chief" && c.Controller == x.Me && Up(x);
            s.On = async (x, e) =>
            {
                if (e.Is(EvKind.Summoned, x.Self) && e.Arg == (int)SummonKind.Tribute && x.Targetable(x.My.Grave.Where(c => c.Data.IsMonster && c.In("Gravekeeper's"))).Any() && await x.Ask("Special Summon a Gravekeeper's monster from your GY?"))
                    await SummonPick(x, x.Targetable(x.My.Grave.Where(c => c.Data.IsMonster && c.In("Gravekeeper's"))), "Special Summon which?");
            };
        }
        S("Gravekeeper's Curse").On = async (x, e) => { if (e.Is(EvKind.Summoned, x.Self)) await x.D.Damage(x.Opp, 500, x.Self); };
        Flip("Gravekeeper's Guard", async x => { var t = await x.Pick(x.Targetable(x.OppMonsters()), "Return which monster to the hand?"); if (t != null) await x.D.ToHand(t, x.Self); });
        S("Gravekeeper's Spear Soldier").Allow = (x, q, c) => q == Q.Piercing && c == x.Self;
        {
            var s = S("Gravekeeper's Watcher");
            s.AddFx(f =>
            {
                f.Label = "Negate a discard"; f.FromHand = true; f.Speed = 2; f.AnyTurn = true;
                f.Can = x => x.Ev is { Kind: EvKind.Activated, Link: { } l } && l.Player == x.Opp && DiscardsOnResolve(l.Card.Data.Desc);
                f.Cost = async x => await x.D.ToGrave(x.Self, x.Self);
                f.Run = async x => await x.D.Negate(x.Ev!.Link!);
            });
        }
        {
            var s = S("Gravity Axe - Grarl");
            s.EquipOk = (x, m) => true;
            s.Stats = (x, c, st) => { if (c == x.Self.EquippedTo) st.Add(500, 0); };
            s.Forbid = (x, q, c) => q == Q.ChangePos && c != null && c.Controller == x.Opp;
        }
        {
            var s = S("Great Long Nose");
            s.SummonOk = (x, by, k) => false;
            s.On = async (x, e) =>
            {
                if (e.Is(EvKind.Flipped, x.Self)) x.Self.Vars["flipturn"] = x.D.Turn;
                if (e.Kind == EvKind.EndPhase && Up(x) && ((x.Self.SummonTurn == x.D.Turn && x.Self.Summoned is SummonKind.Normal or SummonKind.Tribute) || x.Self.Var("flipturn") == x.D.Turn))
                    await x.D.ToHand(x.Self, x.Self);
                if (e.Kind == EvKind.BattleDamage && e.Card == x.Self && e.Player == x.Opp)
                {
                    var d = x.D; int opp = x.Opp; bool done = false;
                    d.Watch(int.MaxValue, async ev => { if (!done && ev.Kind == EvKind.MainStart && ev.Player == opp) { done = true; d.TurnFlags.Add("nobattle"); } });
                }
            };
        }
        S("Green Gadget").On = async (x, e) =>
        {
            if (e.Is(EvKind.Summoned, x.Self) && e.Arg != (int)SummonKind.Flip && x.My.Deck.Any(c => c.Name == "Red Gadget") && await x.Ask("Add Red Gadget from your Deck?")) await x.Search(c => c.Name == "Red Gadget", "Add Red Gadget");
        };
        {
            var s = Spell("Grief Tablet", async x => { }, x => Hopt(x, "act"), async x => UseHopt(x, "act"));
            s.Run = null;
            s.AlsoIn = new[] { "Wedju" };
            s.EquipOk = (x, m) => true;
            s.Forbid = (x, q, c) => c != null && c == x.Self.EquippedTo && q is Q.Attack or Q.Tribute or Q.Negate;
            s.AddFx(f =>
            {
                f.Label = "Add a Wedju card"; f.OncePerTurn = true;
                f.Can = x => x.Self.EquippedTo?.Controller == x.Me && x.My.Deck.Any(c => c.In("Wedju") && c.Name != "Grief Tablet");
                f.Run = x => x.Search(c => c.In("Wedju") && c.Name != "Grief Tablet", "Add which Wedju card?");
            });
            s.On = async (x, e) =>
            {
                if (e.Is(EvKind.Equipped, x.Self)) Tie(x, e.Other!);
                if (e.Kind == EvKind.Destroyed && e.Card == TiedMonster(x) && x.Self.Loc == Loc.Grave && await x.Ask("Inflict 500 damage to your opponent (Grief Tablet)?")) { x.D.Flags.Remove($"tie{x.Self.Uid}"); await x.D.Damage(x.Opp, 500, x.Self); }
            };
        }
        Spell("Gryphon's Feather Duster", async x =>
        {
            var l = x.Spells(x.Me).Where(c => c != x.Self).ToList();
            await x.D.Destroy(l, x.Self);
            await x.D.Gain(x.Me, 500 * l.Count(c => !c.OnField), x.Self);
        }, x => x.Spells(x.Me).Any(c => c != x.Self));
        Spell("Guard Penalty", async x =>
        {
            var t = await x.Pick(x.AllMonsters(), "Select 1 monster");
            if (t == null) return;
            var d = x.D; int me = x.Me; bool done = false;
            d.Watch(d.Turn, async e => { if (!done && e.Kind == EvKind.PositionChanged && e.Card == t && t.Defense) { done = true; await d.Draw(me, 1); } });
        }, x => x.AllMonsters().Any());
        S("Guardian Angel Joan").On = async (x, e) => { if (KilledToGrave(x, e)) await x.D.Gain(x.Me, e.Other!.Data.Atk, x.Self); };
        {
            var s = S("Guardian Dreadscythe");
            s.SummonOk = (x, by, k) => by == x.Self;
            s.Forbid = (x, q, c) => q is Q.NormalSummon or Q.SpecialSummon && c != null && (c.Controller == x.Me || x.D.QueryPlayer == x.Me) && Up(x);
            s.AddFx(f =>
            {
                f.Label = "Special Summon"; f.FromHand = true; f.Speed = 2; f.AnyTurn = true;
                f.Can = x => x.Ev is { Kind: EvKind.Destroyed } e && e.Card!.Name == "Guardian Eatos" && e.Card.Loc == Loc.Grave && e.Card.Owner == x.Me && x.D.FreeMonsterZones(x.Me) > 0;
                f.Run = async x => { if (x.Self.Loc == Loc.Hand) await x.D.SpecialSummon(x.Self, x.Me, await x.D.ChoosePosition(x.Me, x.Self), by: x.Self); };
            });
            s.On = async (x, e) =>
            {
                if (e.Is(EvKind.Summoned, x.Self) && x.My.Deck.Any(c => c.Name == "Reaper Scythe - Dreadscythe") && x.My.FreeSpellZones > 0 && await x.Ask("Equip Reaper Scythe - Dreadscythe from your Deck?"))
                { var c = x.My.Deck.First(c => c.Name == "Reaper Scythe - Dreadscythe"); await x.D.Equip(c, x.Self); x.D.Shuffle(x.Me); }
                if (e.Is(EvKind.ToGrave, x.Self) && e.From == Loc.Monster && x.My.Hand.Count > 0)
                {
                    await x.D.ToGrave((await x.Pick(x.My.Hand, "Send 1 card from your hand to the GY"))!, x.Self);
                    if (x.Self.Loc == Loc.Grave && x.D.CanSpecialSummon(x.Self, x.Me, by: x.Self)) await x.D.SpecialSummon(x.Self, x.Me, await x.D.ChoosePosition(x.Me, x.Self), by: x.Self);
                }
            };
            s.Note = "While you control it you can't Normal or Special Summon (Setting is still allowed).";
        }
        {
            var s = S("Guardian Eatos");
            s.ProcCan = x => !x.My.Grave.Any(c => c.Data.IsMonster);
            s.Proc = async x => await x.D.SpecialSummon(x.Self, x.Me, await x.D.ChoosePosition(x.Me, x.Self), by: x.Self);
            s.Can = x => Up(x) && x.D.EquipsOf(x.Self).Any(e => e.Data.IsSpell && e.Controller == x.Me) && x.Targetable(x.Their.Grave.Where(c => c.Data.IsMonster)).Any();
            s.Cost = async x =>
            {
                var eq = await x.Pick(x.D.EquipsOf(x.Self).Where(e => e.Data.IsSpell && e.Controller == x.Me), "Send which Equip Spell to the GY?");
                if (eq != null) await x.D.ToGrave(eq, x.Self);
                x.T.AddRange(await x.PickN(x.Targetable(x.Their.Grave.Where(c => c.Data.IsMonster)), 1, 3, "Banish up to 3 monsters from your opponent's GY"));
            };
            s.Run = async x =>
            {
                int n = 0;
                foreach (var t in x.T.Where(t => t.Loc == Loc.Grave).ToList()) { await x.D.Banish(t, x.Self); n++; }
                if (Up(x)) x.D.Boost(x.Self, 500 * n);
            };
        }
        {
            var s = S("Guardian Elma");
            OnlyWith(s, "Butterfly Dagger - Elma");
            s.On = async (x, e) =>
            {
                if (e.Is(EvKind.Summoned, x.Self) && e.Arg != (int)SummonKind.Flip && x.My.FreeSpellZones > 0 && x.Targetable(x.My.Grave.Where(c => c.Data.IsSpell && c.Data.Is("Equip"))).Any() && await x.Ask("Equip an Equip Spell from your GY to Guardian Elma?"))
                { var c = await x.Pick(x.Targetable(x.My.Grave.Where(c => c.Data.IsSpell && c.Data.Is("Equip") && (x.D.S(c)?.EquipOk?.Invoke(x.D.X(c), x.Self) ?? true))), "Equip which?"); if (c != null) await x.D.Equip(c, x.Self); }
            };
        }
        {
            var s = S("Guardian Grarl");
            OnlyWith(s, "Gravity Axe - Grarl");
            s.ProcCan = x => x.My.Hand.Count == 1 && x.My.FieldCards.Any(c => c.FaceUp && c.Name == "Gravity Axe - Grarl");
            s.Proc = async x => await x.D.SpecialSummon(x.Self, x.Me, await x.D.ChoosePosition(x.Me, x.Self), by: x.Self);
        }
        {
            var s = S("Guardian Kay'est");
            OnlyWith(s, "Rod of Silence - Kay'est");
            s.Forbid = (x, q, c) => c == x.Self && q is Q.AffectedBySpell or Q.BeAttacked;
        }
        {
            var s = S("Guardian Sphinx");
            FlipDown(s);
            s.On = async (x, e) => { if (e.Is(EvKind.Summoned, x.Self) && e.Arg == (int)SummonKind.Flip) foreach (var m in x.OppMonsters().ToList()) await x.D.ToHand(m, x.Self); };
        }
        {
            var s = SelfBoost("Gyaku-Gire Panda", x => 500 * x.OppMonsters().Count());
            s.Allow = (x, q, c) => q == Q.Piercing && c == x.Self;
        }
        {
            var s = S("Gyroid");
            s.DestroyInstead = async (x, battle) =>
            {
                if (!battle || x.Self.Var("gyroturn") == x.D.Turn) return false;
                x.Self.Vars["gyroturn"] = x.D.Turn;
                return true;
            };
        }
        Spell("H - Heated Heart", async x =>
        {
            if (x.T0 is not { } t || !t.Valid() || !t.FaceUp) return;
            x.D.Boost(t, 500);
            t.Vars["pierce"] = x.D.Turn;
        }, x => x.Targetable(x.MyMonsters().FaceUp()).Any(), async x => await x.Target(x.MyMonsters().FaceUp(), "Which monster gains 500 ATK and piercing?"));
        Spell("Hallowed Life Barrier", async x =>
        {
            var d = x.D; int me = x.Me;
            d.Linger(d.Turn, (q, c, p) => q == Q.TakeDamage && p == me && (c == null || c.Controller != me));   // your own cards still hurt
            d.TurnFlags.Add($"nobattledestroy{me}");
        }, x => x.My.Hand.Any(c => c != x.Self), async x => await x.D.Discard(x.Me, await x.PickN(x.My.Hand.Where(c => c != x.Self), 1, 1, "Discard 1 card"), x.Self));
        S("Hallowed Life Barrier").Note = "This turn you take no damage and your monsters can't be destroyed by battle (per its rulings).";
        Spell("Hammer Shot", async x =>
        {
            var l = x.AllMonsters().Where(m => m.FaceUp && m.Attack).ToList();
            if (l.Count == 0) return;
            int top = l.Max(m => x.Atk(m));
            var t = await x.Pick(l.Where(m => x.Atk(m) == top), "Destroy which monster (highest ATK)?");
            if (t != null) await x.D.Destroy(t, x.Self);
        }, x => x.AllMonsters().Any(m => m.FaceUp && m.Attack));
        {
            var s = S("Hamon, Lord of Striking Thunder");
            s.SummonOk = (x, by, k) => by == x.Self;
            static IEnumerable<Card> Cont(Ctx x) => x.Spells(x.Me).Where(c => c.FaceUp && c.Data.IsSpell && c.Data.Is("Continuous"));
            s.ProcCan = x => Cont(x).Count() >= 3;
            s.Proc = async x =>
            {
                foreach (var c in await x.PickN(Cont(x), 3, 3, "Send 3 face-up Continuous Spells to the GY")) await x.D.ToGrave(c, x.Self);
                await x.D.SpecialSummon(x.Self, x.Me, await x.D.ChoosePosition(x.Me, x.Self), by: x.Self);
            };
            s.On = async (x, e) => { if (e.Kind == EvKind.BattleDestroyedMonster && e.Card == x.Self && e.Other!.Owner == x.Opp && e.Other.Loc == Loc.Grave) await x.D.Damage(x.Opp, 1000, x.Self); };
            s.Forbid = (x, q, c) => q == Q.BeAttacked && c != null && c != x.Self && c.Controller == x.Me && Up(x) && x.Self.Defense;
        }
        {
            var s = S("Hannibal Necromancer");
            s.On = async (x, e) => { if (e.Is(EvKind.Summoned, x.Self) && e.Summoned(SummonKind.Normal, SummonKind.Tribute) && x.Self.Counters("spell") < 1) AddCounter(x, x.Self, 1, "spell"); };
            s.Can = x => Up(x) && x.Self.Counters("spell") > 0 && x.Targetable(x.AllSpells().Where(c => c.FaceUp && c.Data.IsTrap)).Any();
            s.Cost = async x => { AddCounter(x, x.Self, -1, "spell"); await x.Target(x.AllSpells().Where(c => c.FaceUp && c.Data.IsTrap), "Destroy which face-up Trap?"); };
            s.Run = async x => { if (x.T0 is { OnField: true } t) await x.D.Destroy(t, x.Self); };
        }
        {
            static int Harpies(Ctx x) => x.MyMonsters().Count(m => m.FaceUp && (Scripts.NameOf(m) is "Harpie Lady" or "Harpie Lady Sisters" || Scripts.NameOf(m).StartsWith("Harpie Lady ")));
            Spell("Harpie Lady Phoenix Formation", async x =>
            {
                var l = x.T.Where(t => t.Valid()).ToList();
                await x.D.Destroy(l, x.Self);
                var dead = l.Where(m => !m.OnField).ToList();
                if (dead.Count > 0) await x.D.Damage(x.Opp, dead.Max(m => m.Data.Atk), x.Self);
            }, x => Harpies(x) >= 3 && !x.D.TurnFlags.Contains($"bp{x.Me}") && x.Targetable(x.OppMonsters()).Any(),
            async x =>
            {
                var d = x.D; int me = x.Me;
                d.TurnFlags.Add("nobattle");
                d.Linger(d.Turn, (q, c, p) => q == Q.SpecialSummon && c != null && (p == me || c.Controller == me) && c.Loc is Loc.Deck or Loc.Extra);   // from the Main Deck or Extra Deck (the hand is fine)
                var l = x.Targetable(x.OppMonsters()).ToList();
                x.T.AddRange(await x.PickN(l, Math.Min(l.Count, Harpies(x)), Math.Min(l.Count, Harpies(x)), "Target monsters to destroy"));
            });
        }
        {
            var s = Spell("Harpie's Feather Storm", async x =>
            {
                var d = x.D; int opp = x.Opp;
                d.Watch(d.Turn, async e => { if (e.Kind == EvKind.Activated && e.Link is { } l && l.Player == opp && l.Card.Data.IsMonster && !l.IsCardActivation) l.Negated = true; });
            }, x => x.MyMonsters().Any(m => m.FaceUp && x.IsAttr(m, "WIND") && x.IsRace(m, "Winged Beast")));
            s.HandTrap = x => x.MyMonsters().Any(m => m.FaceUp && m.In("Harpie"));
            s.On = async (x, e) =>
            {
                if (e.Is(EvKind.Destroyed, x.Self) && e.From == Loc.Spell && x.Self.Owner == x.Me && e.Other?.Controller == x.Opp && x.My.Deck.Concat(x.My.Grave).Any(c => c.Name == "Harpie's Feather Duster")
                    && await x.Ask("Add Harpie's Feather Duster from your Deck or GY?"))
                {
                    var c = await x.Pick(x.My.Deck.Concat(x.My.Grave).Where(c => c.Name == "Harpie's Feather Duster"), "Add which?");
                    bool deck = c?.Loc == Loc.Deck;
                    if (c != null) await x.D.ToHand(c, x.Self);
                    if (deck) x.D.Shuffle(x.Me);
                }
            };
        }
        Spell("Hate Buster", async x =>
        {
            var e = x.Ev!;
            var both = new[] { e.Card!, e.Other! }.Where(c => c.Valid()).ToList();
            int atk = e.Card!.Data.Atk;
            await x.D.Destroy(both, x.Self);
            if (!e.Card.Valid()) await x.D.Damage(x.Opp, atk, x.Self);
        }, x => x.Ev is { Kind: EvKind.AttackDeclared } e && e.Player == x.Opp && e.Other is { } t && t.Controller == x.Me && t.FaceUp && x.IsRace(t, "Fiend"));
        S("Heart of the Underdog").On = async (x, e) =>
        {
            if (e.Kind == EvKind.Drew && e.Arg == 1 && e.Player == x.Me && x.Self.OnField && x.Self.FaceUp && e.Cards!.Any(c => c.Data.IsNormalMonster) && await x.Ask("Reveal the Normal Monster you drew and draw 1 more card?"))
            { x.D.Reveal(x.Opp, e.Cards!.First(c => c.Data.IsNormalMonster)); await x.D.Draw(x.Me, 1); }
        };
        Sure("Golden Homunculus", "Gravekeeper's Spear Soldier", "Gravekeeper's Guard", "Gravity Axe - Grarl", "Guardian Angel Joan", "Gyaku-Gire Panda", "Grand Convergence", "Hammer Shot",
            "H - Heated Heart", "Gryphon's Feather Duster", "Grave Squirmer", "Green Gadget");
    }
}
