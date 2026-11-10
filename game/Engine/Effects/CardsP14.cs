namespace PoC.Engine;

// Official cards, batch 14 (Heavy Mech Support Platform .. Inferno Fire Blast).
public static partial class Scripts
{
    /// Spirit-style return: "returns to the owner's hand during the End Phase of the turn it is Normal Summoned or flipped face-up".
    static readonly HashSet<string> Spirits = new();
    static void ReturnsAtEnd(Script s, Func<Ctx, Ev, Flow>? more = null)
    {
        Spirits.Add(s.Name);
        s.SummonOk = (x, by, k) => false;
        s.On = async (x, e) =>
        {
            if (e.Is(EvKind.Flipped, x.Self)) x.Self.Vars["flipturn"] = x.D.Turn;
            if (e.Kind == EvKind.EndPhase && Up(x) && x.D.CountActive("Spiritual Energy Settle Machine") == 0 && ((x.Self.SummonTurn == x.D.Turn && x.Self.Summoned is SummonKind.Normal or SummonKind.Tribute) || x.Self.Var("flipturn") == x.D.Turn))
            { await x.D.ToHand(x.Self, x.Self); return; }
            if (more != null) await more(x, e);
        };
    }

    /// Horus-style LV up: "During the End Phase, if this card destroyed a monster by battle this turn: you can send it to the GY; Special Summon X from your hand or Deck".
    static void LevelUp(Script s, string next)
    {
        var on = s.On;
        s.On = async (x, e) =>
        {
            if (on != null) await on(x, e);
            if (e.Kind == EvKind.BattleDestroyedMonster && e.Card == x.Self) x.Self.Vars["killturn"] = x.D.Turn;
            if (e.Kind == EvKind.EndPhase && Up(x) && x.Self.Var("killturn") == x.D.Turn && x.My.Hand.Concat(x.My.Deck).Any(c => c.Name == next) && await x.Ask($"Send {x.Self.Name} to the GY to Special Summon {next}?"))
            {
                await x.D.ToGrave(x.Self, x.Self);
                await SummonPick(x, x.My.Hand.Concat(x.My.Deck).Where(c => c.Name == next), $"Special Summon {next}");
                x.D.Shuffle(x.Me);
            }
        };
    }

    static int BanishedMonsters(Ctx x) => x.D.Players.Sum(p => p.Banished.Count(c => c.Data.IsMonster));

    /// Helios: destroyed by battle and sent to the GY: Special Summon it during the End Phase, gaining `gain` ATK/DEF.
    static void HeliosRevive(Script s, int gain)
    {
        var on = s.On;
        s.On = async (x, e) =>
        {
            if (on != null) await on(x, e);
            if (e.Is(EvKind.Destroyed, x.Self) && e.Battle && x.Self.Loc == Loc.Grave) x.Self.Vars["heliosturn"] = x.D.Turn;
            if (e.Kind == EvKind.EndPhase && x.Self.Loc == Loc.Grave && x.Self.Var("heliosturn") == x.D.Turn && x.D.CanSpecialSummon(x.Self, x.Me, by: x.Self))
            {
                x.Self.Vars["heliosturn"] = 0;
                if (await x.D.SpecialSummon(x.Self, x.Me, await x.D.ChoosePosition(x.Me, x.Self), by: x.Self)) { x.Self.Vars["modatk"] = gain; x.Self.Vars["moddef"] = gain; x.D.Seq++; }
            }
        };
    }

    static void CardsP14(CardDb db)
    {
        {
            var s = Union("Heavy Mech Support Platform", (x, m) => x.IsRace(m, "Machine"), effectsToo: true);
            s.Stats = (x, c, st) => { if (x.Self.Var("union") == 1 && c == x.Self.EquippedTo) st.Add(500, 500); };
        }
        S("Helios - The Primordial Sun").Base = (x, c, st) => { if (c == x.Self) { st.BaseAtk = 100 * BanishedMonsters(x); st.BaseDef = st.BaseAtk; } };
        {
            var s = S("Helios Duo Megistus");
            s.Base = (x, c, st) => { if (c == x.Self) { st.BaseAtk = 200 * BanishedMonsters(x); st.BaseDef = st.BaseAtk; } };
            s.ProcCan = x => x.D.Tributable(x.Me).Any(m => m.Name == "Helios - The Primordial Sun");
            s.Proc = async x => { var t = await x.Pick(x.D.Tributable(x.Me).Where(m => m.Name == "Helios - The Primordial Sun"), "Tribute Helios - The Primordial Sun"); if (t != null) { await x.D.Tribute(t, x.Self); await x.D.SpecialSummon(x.Self, x.Me, await x.D.ChoosePosition(x.Me, x.Self), by: x.Self); } };
            HeliosRevive(s, 300);
        }
        {
            var s = S("Helios Trice Megistus");
            s.Base = (x, c, st) => { if (c == x.Self) { st.BaseAtk = 300 * BanishedMonsters(x); st.BaseDef = st.BaseAtk; } };
            s.ProcCan = x => x.D.Tributable(x.Me).Any(m => m.Name == "Helios Duo Megistus");
            s.Proc = async x => { var t = await x.Pick(x.D.Tributable(x.Me).Where(m => m.Name == "Helios Duo Megistus"), "Tribute Helios Duo Megistus"); if (t != null) { await x.D.Tribute(t, x.Self); await x.D.SpecialSummon(x.Self, x.Me, await x.D.ChoosePosition(x.Me, x.Self), by: x.Self); } };
            s.ExtraAttacks = x => x.OppMonsters().Any() ? 1 : 0;
            HeliosRevive(s, 500);
        }
        {
            var s = S("Helpoemer");
            s.SummonOk = (x, by, k) => x.Self.Loc != Loc.Grave;
            s.On = async (x, e) =>
            {
                if (e.Kind == EvKind.BattleEnd && e.Player == x.Opp && x.Self.Loc == Loc.Grave && x.D.Flags.ContainsKey($"battledead{x.Self.Owner}_{x.Self.Uid}") && x.Their.Hand.Count > 0)
                    await x.D.DiscardRandom(x.Opp, 1, x.Self);
            };
        }
        Spell("Hero Barrier", async x =>
        {
            if (!x.MyMonsters().Any(m => m.FaceUp && m.In("Elemental HERO"))) return;
            if (x.D.Attacker is { } a && a.Controller == x.Opp) { x.D.AttackNegated = true; return; }
            var d = x.D; int opp = x.Opp; bool done = false;
            d.Watch(d.Turn, async e => { if (!done && e.Kind == EvKind.AttackDeclared && e.Player == opp) { done = true; d.AttackNegated = true; } });
        }, x => x.MyMonsters().Any(m => m.FaceUp && m.In("Elemental HERO")));
        Spell("Hero Blast", async x =>
        {
            if (x.T0 is not { Loc: Loc.Grave } t) return;
            int atk = t.Data.Atk;
            await x.D.ToHand(t, x.Self);
            var l = x.OppMonsters().Where(m => m.FaceUp && x.Atk(m) <= atk).ToList();
            if (l.Count > 0) { var k = await x.Pick(l, "Destroy which monster?"); if (k != null) await x.D.Destroy(k, x.Self); }
        }, x => x.Targetable(x.My.Grave.Where(c => c.Data.IsNormalMonster && c.In("Elemental HERO"))).Any(),
        async x => await x.Target(x.My.Grave.Where(c => c.Data.IsNormalMonster && c.In("Elemental HERO")), "Add which Elemental HERO Normal Monster?"));
        Spell("Hero Counterattack", async x =>
        {
            if (x.My.Hand.Count == 0) return;
            var c = x.My.Hand[x.D.Rng.Int(x.My.Hand.Count)];
            x.D.Reveal(x.Opp, c); x.D.Reveal(x.Me, c);
            if (!(c.Data.IsMonster && c.In("Elemental HERO"))) return;
            var t = await x.Pick(x.OppMonsters(), "Destroy which monster your opponent controls?");
            if (t != null) await x.D.Destroy(t, x.Self);
            if (x.D.CanSpecialSummon(c, x.Me, by: x.Self)) await x.D.SpecialSummon(c, x.Me, await x.D.ChoosePosition(x.Me, c), by: x.Self);
        }, x => x.Ev is { Kind: EvKind.Destroyed, Battle: true } e && e.Card!.In("Elemental HERO") && x.D.LeftFieldLog.LastOrDefault(l => l.c == e.Card) is { c: not null } l && l.controller == x.Me
            && x.My.Hand.Any(c => c.Data.IsMonster && c.In("Elemental HERO")));
        {
            string[] four = { "H - Heated Heart", "E - Emergency Call", "R - Righteous Justice", "O - Oversoul" };
            Spell("Hero Flash!!", async x =>
            {
                await SummonPick(x, x.My.Deck.Where(c => c.Data.IsNormalMonster && c.In("Elemental HERO")), "Special Summon an Elemental HERO Normal Monster");
                x.D.Shuffle(x.Me);
                foreach (var m in x.MyMonsters().Where(m => m.FaceUp && m.Data.IsNormalMonster && m.In("Elemental HERO"))) m.Vars["direct"] = x.D.Turn;
            }, x => four.All(n => x.My.Grave.Any(c => c.Name == n)),
            async x => { foreach (var n in four) await x.D.Banish(x.My.Grave.First(c => c.Name == n), x.Self); });
        }
        Spell("Hero Heart", async x =>
        {
            if (x.T0 is not { } t || !t.Valid() || !t.FaceUp) return;
            x.D.Boost(t, -(x.Atk(t) - x.Atk(t) / 2));
            t.Vars["maxatk"] = 2; t.Vars["maxatkturn"] = x.D.Turn;
        }, x => x.Targetable(x.MyMonsters().FaceUp().Where(m => m.In("Elemental HERO"))).Any(),
        async x => await x.Target(x.MyMonsters().FaceUp().Where(m => m.In("Elemental HERO")), "Which Elemental HERO attacks twice?"));
        S("Hero Kid").On = async (x, e) =>
        {
            if (!e.Is(EvKind.Summoned, x.Self) || e.Arg is not ((int)SummonKind.Special or (int)SummonKind.Fusion or (int)SummonKind.Ritual)) return;
            var l = x.My.Deck.Where(c => c.Name == "Hero Kid").ToList();
            if (l.Count == 0 || !await x.Ask("Special Summon Hero Kids from your Deck?")) return;
            foreach (var c in await x.PickN(l, 1, Math.Min(l.Count, x.D.FreeMonsterZones(x.Me)), "Special Summon how many Hero Kids?"))
                await x.D.SpecialSummon(c, x.Me, await x.D.ChoosePosition(x.Me, c), by: x.Self);
            x.D.Shuffle(x.Me);
        };
        Spell("Hero Mask", async x =>
        {
            if (x.T0 is not { } t || !t.Valid() || !t.FaceUp) return;
            var c = await x.Pick(x.My.Deck.Where(c => c.Data.IsMonster && c.In("Elemental HERO") && c.Name != Scripts.NameOf(t)), "Send which Elemental HERO from your Deck to the GY?");
            x.D.Shuffle(x.Me);
            if (c == null) return;
            await x.D.ToGrave(c, x.Self);
            if (c.Loc == Loc.Grave && t.Valid()) { t.TempName = c.Name; x.D.Seq++; var d = x.D; d.Later(d.Turn, Phase.End, async () => t.TempName = null); }
        }, x => x.Targetable(x.MyMonsters().FaceUp()).Any() && x.My.Deck.Any(c => c.Data.IsMonster && c.In("Elemental HERO")),
        async x => await x.Target(x.MyMonsters().FaceUp(), "Which monster takes the name?"));
        {
            var s = S("Hero Medal");
            s.Can = x => false;
            s.On = async (x, e) =>
            {
                if (e.Is(EvKind.Destroyed, x.Self) && x.Self.Loc == Loc.Grave && e.Other?.Controller == x.Opp && x.D.LeftFieldLog.LastOrDefault(l => l.c == x.Self) is { c: not null } l && !l.faceUp)
                { await x.D.ToDeck(x.Self, shuffle: true); await x.D.Draw(x.Me, 1); }
            };
            s.Note = "It has no activation of its own: it works when your Set copy is destroyed by your opponent's card effect.";
        }
        {
            var s = Spell("Hero Ring", async x =>
            {
                if (x.T0 is not { } t || !t.Valid() || !t.FaceUp) return;
                await x.D.Equip(x.Self, t);
                x.Self.Vars["stay"] = 1;
            }, x => x.Targetable(x.D.FaceUpMonsters.Where(m => x.IsRace(m, "Warrior") && x.Atk(m) <= 1500)).Any(),
            async x => await x.Target(x.D.FaceUpMonsters.Where(m => x.IsRace(m, "Warrior") && x.Atk(m) <= 1500), "Equip Hero Ring to which Warrior?"));
            s.Forbid = (x, q, c) => q == Q.BeAttacked && c != null && c == x.Self.EquippedTo && x.D.Querying is { } a && a.Controller != c.Controller && x.Atk(a) >= 1900;
        }
        Spell("Hero Signal", async x => { await SummonPick(x, x.My.Hand.Concat(x.My.Deck).Where(c => EHero(c) && c.Data.Level <= 4), "Special Summon a Level 4 or lower Elemental HERO"); x.D.Shuffle(x.Me); },
            x => x.Ev is { Kind: EvKind.Destroyed, Battle: true } e && e.Card!.Loc == Loc.Grave && x.D.LeftFieldLog.LastOrDefault(l => l.c == e.Card) is { c: not null } l && l.controller == x.Me
                && x.My.Hand.Concat(x.My.Deck).Any(c => EHero(c) && c.Data.Level <= 4));
        Spell("Hero Spirit", async x => x.D.Flags[$"battleshield{x.Me}"] = x.D.Turn,
            x => x.D.Phase == Phase.Battle && x.My.Grave.Any(c => c.In("Elemental HERO") && x.D.Flags.GetValueOrDefault($"battledead{x.Me}_{c.Uid}") == x.D.Turn));
        {
            var s = Spell("Hero's Rule 1: Five Freedoms", async x => { foreach (var t in x.T.Where(t => t.Loc == Loc.Grave).ToList()) await x.D.Banish(t, x.Self); },
                x => x.D.Players.Any(p => p.Grave.Any()),
                async x => x.T.AddRange(await x.PickN(x.Targetable(x.D.Players.SelectMany(p => p.Grave)), 1, 5, "Banish up to 5 cards from the GYs")));
            s.GraveFx = new Script
            {
                Speed = 1,   // no "(Quick Effect)": your Main Phase
                Can = x => Hopt(x) && x.Self.Var("gyturn") != x.D.Turn && x.D.FreeMonsterZones(x.Me) > 0 && x.My.Banished.Any(c => EHero(c) && c.FaceUp),
                Cost = async x => { UseHopt(x); await x.D.Banish(x.Self, x.Self); await x.Target(x.My.Banished.Where(c => EHero(c) && c.FaceUp), "Special Summon which banished Elemental HERO?"); },
                Run = async x => { if (x.T0 is { Loc: Loc.Banished } t) await x.D.SpecialSummon(t, x.Me, await x.D.ChoosePosition(x.Me, t), by: x.Self, ignore: true); },
            };
        }
        Spell("Hero's Rule 2", async x => await x.D.Negate(x.Ev!.Link!),
            x => x.Ev is { Kind: EvKind.Activated, Link: { } l } && l.Card != x.Self && (!l.Card.Data.IsMonster || !l.IsCardActivation) && l.X.T.Any(t => t.Loc == Loc.Grave));
        {
            var s = S("Hexe Trude");
            static bool Castle(Ctx x) => x.D.Players.Any(p => p.FieldZone is { FaceUp: true } f && f.Name == "Golden Castle of Stromberg");
            s.FreeTribute = (x, c, set) => c == x.Self && x.Self.Loc == Loc.Hand && !set && Castle(x);
            s.OnFreeTribute = async x => { };
            s.OncePerTurn = true;
            s.Can = x => Up(x) && Castle(x) && x.Targetable(x.D.FieldCards.Where(c => c != x.Self)).Any();
            s.Cost = async x => await x.Target(x.D.FieldCards.Where(c => c != x.Self), "Destroy which card?");
            s.Run = async x =>
            {
                if (x.T0 is not { OnField: true } t) return;
                await x.D.Destroy(t, x.Self);
                if (!t.OnField && Up(x)) { x.Self.Vars["maxatk"] = 2; x.Self.Vars["maxatkturn"] = x.D.Turn; x.Self.Vars["nodirect"] = x.D.Turn; }
            };
            s.On = async (x, e) =>
            {
                if (e.Kind == EvKind.BattleDestroyedMonster && e.Card == x.Self && x.Targetable(x.MyMonsters().FaceUp()).Any() && await x.Ask("Give 1 face-up monster you control 400 ATK?"))
                { var t = await x.Pick(x.Targetable(x.MyMonsters().FaceUp()), "Which monster?"); if (t != null) { t.Vars["modatk"] = t.Var("modatk") + 400; x.D.Seq++; } }
            };
        }
        Spell("Hidden Soldiers", async x => await SummonPick(x, x.My.Hand.Where(c => c.Data.IsMonster && c.Data.Attribute == "DARK" && c.Data.Level <= 4), "Special Summon a Level 4 or lower DARK monster"),
            x => x.Ev is { Kind: EvKind.Summoned } e && e.Player == x.Opp && e.IsNormalOrFlip() && x.My.Hand.Any(c => c.Data.IsMonster && c.Data.Attribute == "DARK" && c.Data.Level <= 4) && x.D.FreeMonsterZones(x.Me) > 0);
        Spell("Hieroglyph Lithograph", async x => x.D.Flags[$"handlimit{x.Me}"] = 7, x => x.My.Lp > 1000, async x => x.D.Pay(x.Me, 1000));
        ReturnsAtEnd(S("Hino-Kagu-Tsuchi"), async (x, e) =>
        {
            if (e.Kind == EvKind.BattleDamage && e.Card == x.Self && e.Player == x.Opp)
            {
                var d = x.D; int opp = x.Opp; bool done = false; var self = x.Self;
                d.Watch(int.MaxValue, async ev => { if (!done && ev.Kind == EvKind.DrawPhase && ev.Player == opp) { done = true; await d.Discard(opp, d.P(opp).Hand.ToList(), self); } });
            }
        });
        {
            var s = S("Holding Arms");
            static Card? Held(Ctx x) => TiedMonster(x) is { Loc: Loc.Monster, FaceUp: true } m ? m : null;
            s.On = async (x, e) =>
            {
                if (e.Is(EvKind.Summoned, x.Self) && e.Arg != (int)SummonKind.Flip && x.Targetable(x.OppMonsters().FaceUp()).Any())
                { var t = await x.Pick(x.Targetable(x.OppMonsters().FaceUp()), "Target 1 face-up monster your opponent controls"); if (t != null) Tie(x, t); }
            };
            s.Forbid = (x, q, c) => Up(x) && c != null && ((c == Held(x) && q is Q.Attack or Q.Negate) || (c == x.Self && Held(x) != null && q is Q.DestroyByBattle or Q.DestroyByEffect));
        }
        {
            var s = S("Holding Legs");
            s.On = async (x, e) => { if (e.Is(EvKind.Summoned, x.Self)) foreach (var c in x.AllSpells().Where(c => !c.FaceUp).ToList()) await x.D.ToHand(c, x.Self); };
            s.AddFx(f =>
            {
                f.Label = "Lock a Set card"; f.FromGrave = true;
                f.Can = x => x.Targetable(x.Spells(x.Opp).Where(c => !c.FaceUp)).Any();
                f.Cost = async x => { await x.D.Banish(x.Self, x.Self); await x.Target(x.Spells(x.Opp).Where(c => !c.FaceUp), "Which Set card can't be activated?"); };
                f.Run = async x => { if (x.T0 is { OnField: true, FaceUp: false } t) t.Vars["noactuntil"] = x.D.TurnPlayer == x.Me ? x.D.Turn + 1 : x.D.Turn + 2; };
            });
        }
        {
            var s = S("Honest");
            s.Can = x => Up(x);
            s.Run = async x => { if (Up(x)) await x.D.ToHand(x.Self, x.Self); };
            s.AddFx(f =>
            {
                f.Label = "LIGHT monster gains ATK"; f.FromHand = true; f.Speed = 2; f.AnyTurn = true;
                f.Can = x => x.Ev is { Kind: EvKind.DamageCalc } e && new[] { e.Card, e.Other }.Any(m => m != null && m.Controller == x.Me && m.FaceUp && x.IsAttr(m, "LIGHT"))
                    && new[] { e.Card, e.Other }.Any(m => m != null && m.Controller == x.Opp);
                f.Cost = async x => await x.D.ToGrave(x.Self, x.Self);
                f.Run = async x =>
                {
                    var e = x.Ev!;
                    var mine = new[] { e.Card, e.Other }.FirstOrDefault(m => m != null && m.Controller == x.Me);
                    var theirs = new[] { e.Card, e.Other }.FirstOrDefault(m => m != null && m.Controller == x.Opp);
                    if (mine != null && theirs != null && mine.Valid() && theirs.Valid()) x.D.Boost(mine, x.Atk(theirs));
                };
            });
        }
        {
            static bool Neos(Ctx x) => x.MyMonsters().Any(m => m.FaceUp && (Scripts.NameOf(m) == "Elemental HERO Neos" || (m.Data.IsFusion && m.In("Neos"))));
            static bool NeosFusion(Ctx x) => x.MyMonsters().Any(m => m.FaceUp && m.Data.IsFusion && m.In("Neos"));
            static bool HeroMat(Card f) => System.Text.RegularExpressions.Regex.IsMatch(f.Data.Desc.Split('\n')[0], "HERO");
            static IEnumerable<Card> Pool(Ctx x) => x.My.Hand.Concat(x.My.Deck).Concat(x.MyMonsters()).Where(c => c.Data.IsMonster);
            var s = Spell("Hope for the Future", async x => await x.D.FusionSummon(x.Me, x.Self, pool: Pool(x), resultOk: HeroMat),
                x => Hopt(x, "1") && Neos(x) && x.My.Hand.Any(c => c != x.Self) && x.D.FusionOptions(x.Me, pool: Pool(x), resultOk: HeroMat).Any(),
                async x => { UseHopt(x, "1"); await x.D.Discard(x.Me, await x.PickN(x.My.Hand.Where(c => c != x.Self), 1, 1, "Discard 1 card"), x.Self); });
            s.GraveFx = new Script
            {
                Speed = 2,
                Can = x => Hopt(x, "2") && NeosFusion(x) && x.Targetable(x.Their.FieldCards.Where(c => c.FaceUp)).Any(),
                Cost = async x => { UseHopt(x, "2"); await x.D.Banish(x.Self, x.Self); await x.Target(x.Their.FieldCards.Where(c => c.FaceUp), "Negate which card's effects this turn?"); },
                Run = async x => { if (x.T0 is { OnField: true } t) { x.D.Flags[$"neg{t.Uid}"] = 1; var d = x.D; d.Later(d.Turn, Phase.End, async () => d.Flags.Remove($"neg{t.Uid}")); } },
            };
        }
        {
            var s = S("Horned Saurus");
            s.Materials = new() { Named("Kaitoptera"), Cond("1 Dinosaur or Dragon monster", c => c.Data.IsMonster && c.Data.Race is "Dinosaur" or "Dragon") };
            s.On = async (x, e) =>
            {
                if (!e.Is(EvKind.Summoned, x.Self)) return;
                if (e.Arg is (int)SummonKind.Special or (int)SummonKind.Fusion or (int)SummonKind.Ritual) x.Self.Vars["direct"] = x.D.Turn;
                if (e.Arg == (int)SummonKind.Fusion && Hopt(x, "1") && x.My.Deck.Concat(x.My.Grave).Any(c => c.Data.IsSpell && c.Data.Is("Field")) && await x.Ask("Place a Field Spell from your Deck or GY face-up in your Field Zone?"))
                {
                    UseHopt(x, "1");
                    var f = await x.Pick(x.My.Deck.Concat(x.My.Grave).Where(c => c.Data.IsSpell && c.Data.Is("Field")), "Place which Field Spell?");
                    if (f == null) return;
                    if (x.My.FieldZone is { } old) await x.D.ToGrave(old, x.Self);
                    bool deck = f.Loc == Loc.Deck;
                    x.D.Place(f, x.Me, Loc.Field); f.FaceUp = true; x.D.Seq++;
                    if (deck) x.D.Shuffle(x.Me);
                }
            };
            s.AddFx(f =>
            {
                f.Label = "Normal Summon a Dinosaur/Dragon";
                f.Can = x => Hopt(x, "2") && x.My.Hand.Any(c => c.Data.IsMonster && c.Data.Race is "Dinosaur" or "Dragon");
                f.Cost = async x => UseHopt(x, "2");
                f.Run = async x => x.My.ExtraSummons++;
            });
            s.Note = "Its Main Phase effect grants one more Normal Summon this turn (use it right away on a Dinosaur or Dragon).";
        }
        {
            var s = S("Horus the Black Flame Dragon LV4");
            s.Forbid = (x, q, c) => q == Q.ChangeControl && c == x.Self;
            LevelUp(s, "Horus the Black Flame Dragon LV6");
        }
        {
            var s = S("Horus the Black Flame Dragon LV6");
            s.Forbid = (x, q, c) => q == Q.AffectedBySpell && c == x.Self;
            LevelUp(s, "Horus the Black Flame Dragon LV8");
        }
        {
            var s = S("Horus the Black Flame Dragon LV8");
            s.SummonOk = (x, by, k) => by?.Name == "Horus the Black Flame Dragon LV6";
            s.Speed = 2; s.AnyTurn = true;
            s.Can = x => Up(x) && x.Ev is { Kind: EvKind.Activated, Link: { IsCardActivation: true } l } && l.Card.Data.IsSpell;
            s.Run = async x => { if (Up(x)) await x.D.Negate(x.Ev!.Link!); };
        }
        {
            string[] three = { "People Running About", "Oppressed People", "United Resistance" };
            Spell("Huge Revolution", async x =>
            {
                foreach (var c in x.Their.Hand.ToList()) await x.D.ToGrave(c, x.Self);
                await x.D.Destroy(x.Their.FieldCards.ToList(), x.Self);
            }, x => x.Turn() && x.D.Phase is Phase.Main1 or Phase.Main2 && three.All(n => x.My.FieldCards.Any(c => c.FaceUp && c.Name == n)));
        }
        S("Human-Wave Tactics").On = async (x, e) =>
        {
            if (e.Kind != EvKind.EndPhase || !x.Self.OnField || !x.Self.FaceUp) return;
            int n = x.My.Grave.Count(c => c.Data.IsNormalMonster && c.Data.Level <= 2 && x.D.Flags.GetValueOrDefault($"battledead{x.Me}_{c.Uid}") == x.D.Turn);
            for (int i = 0; i < n; i++)
                if (!await SummonPick(x, x.My.Deck.Where(c => c.Data.IsNormalMonster && c.Data.Level <= 2), "Special Summon a Level 2 or lower Normal Monster")) break;
            if (n > 0) x.D.Shuffle(x.Me);
        };
        Spell("Hunting Instinct", async x => await SummonPick(x, x.My.Hand.Where(c => c.Data.IsMonster && c.Data.Race == "Dinosaur"), "Special Summon which Dinosaur?"),
            x => x.Ev is { Kind: EvKind.Summoned } e && e.Player == x.Opp && e.Arg is (int)SummonKind.Special or (int)SummonKind.Fusion or (int)SummonKind.Ritual && x.My.Hand.Any(c => c.Data.IsMonster && c.Data.Race == "Dinosaur") && x.D.FreeMonsterZones(x.Me) > 0);
        S("Hydrogeddon").On = async (x, e) =>
        {
            if (e.Kind == EvKind.BattleDestroyedMonster && e.Card == x.Self && e.Other!.Owner == x.Opp && e.Other.Loc == Loc.Grave && Up(x) && x.My.Deck.Any(c => c.Name == "Hydrogeddon") && await x.Ask("Special Summon Hydrogeddon from your Deck?"))
            { await SummonPick(x, x.My.Deck.Where(c => c.Name == "Hydrogeddon"), "Special Summon Hydrogeddon"); x.D.Shuffle(x.Me); }
        };
        S("Hyper Hammerhead").On = async (x, e) =>
        {
            if (e.Kind != EvKind.AfterDamage || !Up(x)) return;
            var o = e.Card == x.Self ? e.Other : e.Other == x.Self ? e.Card : null;
            if (o != null && o.Controller == x.Opp && o.Valid()) await x.D.ToHand(o, x.Self);
        };
        Spell("Icarus Attack", async x => await x.D.Destroy(x.T.Where(t => t.OnField).ToList(), x.Self),
            x => x.D.Tributable(x.Me).Any(m => m.FaceUp ? x.IsRace(m, "Winged Beast") : m.Data.Race == "Winged Beast") && x.Targetable(x.D.FieldCards.Where(c => c != x.Self)).Count() >= 2,
            async x =>
            {
                var t = await x.Pick(x.D.Tributable(x.Me).Where(m => m.FaceUp ? x.IsRace(m, "Winged Beast") : m.Data.Race == "Winged Beast"), "Tribute 1 Winged Beast");
                if (t != null) await x.D.Tribute(t, x.Self);
                x.T.AddRange(await x.PickN(x.Targetable(x.D.FieldCards.Where(c => c != x.Self)), 2, 2, "Destroy which 2 cards?"));
            });
        {
            var s = SelfBoost("Ice Knight", x => 400 * x.MyMonsters().Count(m => m.FaceUp && x.IsRace(m, "Aqua")));
            s.OncePerTurn = true;
            s.Can = x => Up(x) && !x.D.TurnFlags.Contains($"iceknight{x.Me}");
            s.Run = async x =>
            {
                x.D.TurnFlags.Add($"iceknight{x.Me}");
                var d = x.D; int me = x.Me;
                d.Linger(d.Turn, (q, c, p) => q is Q.NormalSummon or Q.SpecialSummon && c != null && (p == me || c.Controller == me) && (c.Loc == Loc.Hand ? c.Data.Attribute : d.Attr(c)) != "WATER");
            };
            s.ExtraSummon = (x, c) => x.D.TurnFlags.Contains($"iceknight{x.Me}") && c.Data.Attribute == "WATER" && c.Controller == x.Me;
        }
        Spell("Illusion Gate", async x =>
        {
            x.D.Flags[$"illusiongate{x.Me}"] = 1;
            await x.D.Destroy(x.OppMonsters().ToList(), x.Self);
            var l = x.Their.Grave.Where(c => c.Data.IsMonster && x.D.CanSpecialSummon(c, x.Me, by: x.Self, ignore: true)).ToList();
            if (l.Count > 0 && await x.Ask("Special Summon 1 monster from your opponent's GY to your field?")) await SummonPick(x, l, "Special Summon which?", ignore: true);
        }, x => x.D.Flags.GetValueOrDefault($"illusiongate{x.Me}") == 0 && x.My.Lp > 1, async x => x.D.Pay(x.Me, x.My.Lp / 2));
        {
            var s = S("Inaba White Rabbit");
            ReturnsAtEnd(s);
            s.Allow = (x, q, c) => q == Q.DirectAttack && c == x.Self;
        }
        S("Indomitable Fighter Lei Lei").On = async (x, e) =>
        {
            if (e.Kind == EvKind.BattleEnd && Up(x) && x.Self.Attacks > 0) { await x.D.ChangePosition(x.Self, true); x.Self.Vars["nochangepos"] = x.D.Turn + 2; }
        };
        S("Infernal Dragon").On = async (x, e) =>
        {
            if (e.Kind == EvKind.EndPhase && Up(x) && x.Self.Attacks > 0 && x.Self.Var("infernalturn") != x.D.Turn) { x.Self.Vars["infernalturn"] = x.D.Turn; await x.D.Destroy(x.Self, x.Self); return; }
            if (e.Is(EvKind.Destroyed, x.Self) && x.Self.Loc == Loc.Grave && e.From == Loc.Monster && x.D.Tributable(x.Me).Any() && await x.Ask("Tribute 1 monster to Special Summon Infernal Dragon from the GY?"))
            {
                var t = await x.Pick(x.D.Tributable(x.Me), "Tribute 1 monster");
                if (t != null) { await x.D.Tribute(t, x.Self); await x.D.SpecialSummon(x.Self, x.Me, await x.D.ChoosePosition(x.Me, x.Self), by: x.Self); }
            }
        };
        {
            var s = S("Infernal Flame Emperor");
            s.SummonOk = (x, by, k) => false;
            s.On = async (x, e) =>
            {
                if (!e.Is(EvKind.Summoned, x.Self) || e.Arg != (int)SummonKind.Tribute) return;
                int max = Math.Min(5, x.AllSpells().Count());
                var fire = x.My.Grave.Where(c => c.Data.IsMonster && c.Data.Attribute == "FIRE").ToList();
                if (max == 0 || fire.Count == 0 || !await x.Ask("Banish FIRE monsters from your GY to destroy that many Spells/Traps?")) return;
                var l = await x.PickN(fire, 1, Math.Min(max, fire.Count), $"Banish up to {Math.Min(max, fire.Count)} FIRE monsters");
                foreach (var c in l) await x.D.Banish(c, x.Self);
                var st = x.AllSpells().ToList();
                await x.D.Destroy(await x.PickN(st, Math.Min(l.Count, st.Count), Math.Min(l.Count, st.Count), $"Destroy {l.Count} Spell/Trap card(s)"), x.Self);
            };
        }
        {
            var s = S("Infernal Incinerator");
            s.TributesNeeded = x => 1;
            s.TributeOk = (x, t) => t.FaceUp && x.Atk(t) >= 2000;
            s.Stats = (x, c, st) => { if (c == x.Self) st.Add(200 * x.OppMonsters().Count() - 500 * x.MyMonsters().Count(m => m != x.Self), 0); };
            s.On = async (x, e) =>
            {
                if ((e.Is(EvKind.Summoned, x.Self) && e.Summoned(SummonKind.Normal, SummonKind.Tribute)) || (e.Is(EvKind.Set, x.Self) && x.Self.Summoned == SummonKind.Set))
                    if (x.My.Hand.Count > 0) await x.D.Discard(x.Me, x.My.Hand.ToList(), x.Self);
            };
            s.Note = "Its Normal Summon discards your other cards right after the Tribute (part of summoning it).";
        }
        {
            var s = S("Infernalqueen Archfiend");
            s.On = async (x, e) =>
            {
                if (!Up(x)) return;
                if (e.Kind == EvKind.Standby && e.Player == x.Me && x.D.CountActive("Pandemonium") == 0) { if (x.My.Lp < 500) await x.D.Destroy(x.Self); else x.D.Pay(x.Me, 500, x.Self); }   // can't pay the mandatory 500: destroyed
                if (e.Kind == EvKind.Standby)
                {
                    var t = await x.Pick(x.D.FaceUpMonsters.Where(m => m.In("Archfiend")), "Which Archfiend gains 1000 ATK until the End Phase?");
                    if (t != null) x.D.Boost(t, 1000);
                }
                if (e.Kind == EvKind.Activated && e.Link is { } l && l.Player == x.Opp && l.X.T.Contains(x.Self) && x.D.Die(x.Me, x.Self) is 2 or 5) await x.D.Negate(l);
            };
            s.Note = "Its die is rolled when it is targeted (not when the effect resolves).";
        }
        Spell("Inferno Fire Blast", async x =>
        {
            if (x.T0 is not { } t || !t.Valid()) return;
            await x.D.Damage(x.Opp, t.Data.Atk, x.Self);
            t.Vars["noattack"] = x.D.Turn;
        }, x => x.Targetable(x.MyMonsters().FaceUp().Where(m => Scripts.NameOf(m) == "Red-Eyes B. Dragon")).Any(),
        async x => await x.Target(x.MyMonsters().FaceUp().Where(m => Scripts.NameOf(m) == "Red-Eyes B. Dragon"), "Target Red-Eyes B. Dragon"));
        Sure("Helios - The Primordial Sun", "Hero Blast", "Hidden Soldiers", "Hieroglyph Lithograph", "Hydrogeddon", "Hyper Hammerhead", "Icarus Attack", "Inferno Fire Blast", "Hero Signal", "Hunting Instinct");
    }
}
