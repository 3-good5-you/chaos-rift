namespace PoC.Engine;

// Official cards, batch 10 (Elemental HERO Aqua Neos .. Enraged Battle Ox).
public static partial class Scripts
{
    static bool EHero(Card c) => c.Data.IsMonster && c.In("Elemental HERO");

    /// "When this card destroys a monster by battle and sends it to the GY" (the monster's ATK in the GY = its printed ATK).
    static bool KilledToGrave(Ctx x, Ev e) => e.Kind == EvKind.BattleDestroyedMonster && e.Card == x.Self && e.Other!.Loc == Loc.Grave && Up(x);

    /// A Contact Fusion's End Phase return, then something more (Chaos / Magma / Storm Neos).
    static void ContactThen(Script s, Func<Ctx, Flow> after)
    {
        s.On = async (x, e) =>
        {
            if (e.Kind != EvKind.EndPhase || !Up(x) || (x.D.CountActive("Neo Space", x.Me) > 0 || InstantNeoSpace(x))) return;
            await x.D.MoveTo(x.Self, Loc.Extra, by: x.Self);
            if (x.Self.Loc == Loc.Extra) await after(x);
        };
    }

    static async Flow DestroyRandomHand(Ctx x)
    {
        if (x.Their.Hand.Count == 0) return;
        var c = x.Their.Hand[x.D.Rng.Int(x.Their.Hand.Count)];
        x.D.Reveal(x.Me, c);
        await DestroyInHand(x, c);
    }

    static void CardsP10(CardDb db)
    {
        {
            var s = Contact("Elemental HERO Aqua Neos");
            s.OncePerTurn = true;
            s.Can = x => Up(x) && x.My.Hand.Count > 0 && x.Their.Hand.Count > 0;
            s.Cost = async x => await x.D.Discard(x.Me, await x.PickN(x.My.Hand, 1, 1, "Discard 1 card"), x.Self);
            s.Run = DestroyRandomHand;
        }
        S("Elemental HERO Bladedge").Allow = (x, q, c) => q == Q.Piercing && c == x.Self;
        {
            var s = S("Elemental HERO Bubbleman");
            s.ProcCan = x => x.My.Hand.Count == 1 && x.My.Hand[0] == x.Self;
            s.Proc = async x => await x.D.SpecialSummon(x.Self, x.Me, await x.D.ChoosePosition(x.Me, x.Self), by: x.Self);
            static bool Alone(Ctx x) => x.My.Hand.Count == 0 && x.My.FieldCards.All(c => c == x.Self);
            s.On = async (x, e) => { if (e.Is(EvKind.Summoned, x.Self) && Alone(x) && await x.Ask("Draw 2 cards (Bubbleman)?") && Alone(x)) await x.D.Draw(x.Me, 2); };
        }
        {
            var s = Contact("Elemental HERO Chaos Neos");
            ContactThen(s, async x => { foreach (var m in x.D.FaceUpMonsters.ToList()) await x.D.SetFaceDown(m); });
            s.OncePerTurn = true;
            s.Can = x => Up(x) && x.D.Phase == Phase.Main1;
            s.Run = async x =>
            {
                int h = x.D.TossCoins(x.Me, 3, x.Self);
                if (h == 3) await x.D.Destroy(x.OppMonsters().ToList(), x.Self);
                else if (h == 2) { var d = x.D; int opp = x.Opp; d.Linger(d.Turn, (q, c, p) => q == Q.Negate && c != null && c.Loc == Loc.Monster && c.FaceUp && c.Controller == opp); }
                else if (h == 1) foreach (var m in x.MyMonsters().ToList()) await x.D.ToHand(m, x.Self);
            };
            s.Note = "2 Heads negates your opponent's face-up monsters for the turn as a lasting rule (Linger), which reaches monsters that come out later too.";
        }
        {
            var s = S("Elemental HERO Clay Guardian");
            s.Materials = new() { Cond("1 \"Elemental HERO\" monster", EHero), Cond("1 Warrior monster", c => c.Data.IsMonster && c.Data.Race == "Warrior") };
            s.Forbid = (x, q, c) => q is Q.DestroyByBattle or Q.DestroyByEffect && c != null && c != x.Self && c.Controller == x.Me && c.FaceUp && c.In("Elemental HERO") && Up(x);
            s.On = async (x, e) =>
            {
                if (!e.Is(EvKind.Summoned, x.Self) || e.Arg is (int)SummonKind.Normal or (int)SummonKind.Tribute or (int)SummonKind.Flip || !Hopt(x) || !await x.Ask("Special Summon an Elemental HERO from your Deck (Clay Guardian)?")) return;
                UseHopt(x);
                await SummonPick(x, x.My.Deck.Where(EHero), "Special Summon which Elemental HERO?");
                x.D.Shuffle(x.Me);
                if (x.Their.FieldCards.Any() && await x.Ask("Inflict 400 damage for each card your opponent controls?")) await x.D.Damage(x.Opp, 400 * x.Their.FieldCards.Count(), x.Self);
                var d = x.D; int me = x.Me;
                d.Linger(d.Turn, (q, c, p) => q == Q.SpecialSummon && c != null && c.Loc == Loc.Extra && (p == me || c.Owner == me) && !c.In("HERO"));
            };
        }
        {
            var s = Contact("Elemental HERO Dark Neos");
            static Card? Held(Ctx x) => x.D.Flags.GetValueOrDefault($"darkneos{x.Self.Uid}") is > 0 and var u && x.D.CardByUid(u - 1) is { Loc: Loc.Monster } c ? c : null;
            s.Forbid = (x, q, c) => q == Q.Negate && c != null && c == Held(x) && Up(x);
            s.Can = x => Up(x) && Held(x) == null && x.Targetable(x.D.FaceUpMonsters.Where(m => m.Data.IsEffect && m != x.Self)).Any();
            s.Cost = async x => await x.Target(x.D.FaceUpMonsters.Where(m => m.Data.IsEffect && m != x.Self), "Negate which Effect Monster's effect?");
            s.Run = async x => { if (x.T0 is { } t && t.Valid() && Up(x)) x.D.Flags[$"darkneos{x.Self.Uid}"] = t.Uid + 1; };
        }
        {
            var s = S("Elemental HERO Darkbright");
            s.Allow = (x, q, c) => q == Q.Piercing && c == x.Self;
            s.On = async (x, e) =>
            {
                if (e.Kind == EvKind.AfterDamage && e.Card == x.Self && Up(x) && x.Self.Attack) await x.D.ChangePosition(x.Self, true);
                if (e.Is(EvKind.Destroyed, x.Self) && x.Targetable(x.OppMonsters()).Any())
                { var t = await x.Pick(x.Targetable(x.OppMonsters()), "Destroy which monster (Darkbright)?"); if (t != null) await x.D.Destroy(t, x.Self); }
            };
        }
        {
            var s = S("Elemental HERO Divine Neos");
            static bool NeosLike(Card c) => c.In("Neos") || c.In("Neo Space");
            static bool Any5(Card c) => c.Data.IsMonster && (NeosLike(c) || c.In("Neo-Spacian") || c.In("HERO"));
            s.Materials = new() { Cond("1 \"Neos\" or \"Neo Space\" monster", c => c.Data.IsMonster && NeosLike(c)), Cond("1 \"Neo-Spacian\" monster", c => c.Data.IsMonster && c.In("Neo-Spacian")),
                Cond("1 \"HERO\" monster", c => c.Data.IsMonster && c.In("HERO")), Cond("1 Neos / Neo-Spacian / HERO monster", Any5), Cond("1 Neos / Neo-Spacian / HERO monster", Any5) };
            s.MustFusion = true;
            s.OncePerTurn = true;
            s.Can = x => Up(x) && x.My.Grave.Any(Any5);
            s.Cost = async x => { var c = await x.Pick(x.My.Grave.Where(Any5), "Banish which monster from your GY?"); if (c != null) { x.N = c.Data.Idx + 1; await x.D.Banish(c, x.Self); } };
            s.Run = async x =>
            {
                if (!Up(x)) return;
                x.Self.Vars["modatk"] = x.Self.Var("modatk") + 500;
                if (x.N > 0) { x.Self.Vars["copyidx"] = x.N; x.Self.Vars["copyturn"] = x.D.Turn; }
                x.D.Seq++;
            };
            s.Note = "Until the End Phase it plays as the banished monster's card script (that card's effects replace its own for the turn).";
        }
        {
            var s = S("Elemental HERO Electrum");
            var attrs = new[] { "LIGHT", "WIND", "WATER", "FIRE", "EARTH" };
            s.Stats = (x, c, st) => { if (c == x.Self) st.Add(300 * x.OppMonsters().Count(m => m.FaceUp && attrs.Contains(x.D.Attr(m))), 0); };
            s.On = async (x, e) =>
            {
                if (e.Is(EvKind.Summoned, x.Self) && e.Arg == (int)SummonKind.Fusion)
                    foreach (var p in new[] { x.Me, x.Opp })
                    {
                        foreach (var c in x.D.P(p).Banished.ToList()) await x.D.ToDeck(c);
                        x.D.Shuffle(p);
                    }
            };
            s.Note = "Its extra Attributes (WIND, WATER, FIRE, EARTH) count for its ATK gain; elsewhere it reads as LIGHT.";
        }
        S("Elemental HERO Flame Wingman").On = async (x, e) => { if (KilledToGrave(x, e)) await x.D.Damage(x.Opp, e.Other!.Data.Atk, x.Self); };
        {
            var s = Contact("Elemental HERO Flare Neos");
            s.Stats = (x, c, st) => { if (c == x.Self) st.Add(400 * x.AllSpells().Count(), 0); };
        }
        {
            var s = Contact("Elemental HERO Glow Neos");
            s.OncePerTurn = true;
            s.Can = x => Up(x) && x.D.Phase == Phase.Main1 && x.Targetable(x.Their.FieldCards.Where(c => c.FaceUp)).Any();
            s.Cost = async x => await x.Target(x.Their.FieldCards.Where(c => c.FaceUp), "Destroy which face-up card?");
            s.Run = async x =>
            {
                if (x.T0 is not { OnField: true } t) return;
                bool mon = t.Loc == Loc.Monster, spell = !mon && t.Data.IsSpell;
                await x.D.Destroy(t, x.Self);
                if (t.OnField || !Up(x)) return;
                if (mon) x.Self.Vars["noattack"] = x.D.Turn;
                else if (spell) x.Self.Vars["direct"] = x.D.Turn;
                else await x.D.ChangePosition(x.Self, true);
            };
        }
        {
            var s = Contact("Elemental HERO Grand Neos");
            s.OncePerTurn = true;
            s.Can = x => Up(x) && x.Targetable(x.OppMonsters()).Any();
            s.Cost = async x => await x.Target(x.OppMonsters(), "Return which monster to the hand?");
            s.Run = async x => { if (x.T0 is { } t && t.Valid()) await x.D.ToHand(t, x.Self); };
        }
        {
            var s = Contact("Elemental HERO Magma Neos");
            s.Stats = (x, c, st) => { if (c == x.Self) st.Add(400 * x.D.FieldCards.Count(), 0); };
            ContactThen(s, async x => { foreach (var c in x.D.FieldCards.ToList()) await x.D.ToHand(c, x.Self); });
        }
        {
            var s = Contact("Elemental HERO Marine Neos", returns: false);
            s.OncePerTurn = true;
            s.Can = x => Up(x) && x.Their.Hand.Count > 0;
            s.Run = DestroyRandomHand;
        }
        S("Elemental HERO Mariner").Allow = (x, q, c) => q == Q.DirectAttack && c == x.Self && x.Spells(x.Me).Any(s => !s.FaceUp);
        S("Elemental HERO Necroid Shaman").On = async (x, e) =>
        {
            if (!e.Is(EvKind.Summoned, x.Self) || e.Arg is (int)SummonKind.Normal or (int)SummonKind.Tribute or (int)SummonKind.Flip) return;
            var t = await x.Pick(x.Targetable(x.OppMonsters()), "Destroy which monster?");
            if (t == null) return;
            await x.D.Destroy(t, x.Self);
            var l = x.Their.Grave.Where(c => c.Data.IsMonster && x.D.CanSpecialSummon(c, x.Opp, by: x.Self)).ToList();
            if (l.Count > 0) { var r = await x.Pick(l, "Special Summon which monster to your opponent's field?"); if (r != null) await x.D.SpecialSummon(r, x.Opp, await x.D.ChoosePosition(x.Me, r), by: x.Self); }
        };
        S("Elemental HERO Necroshade").FreeTribute = (x, c, set) => !set && x.Self.Loc == Loc.Grave && c.In("Elemental HERO") && c.Data.Level >= 5;
        {
            var s = S("Elemental HERO Neo Bubbleman");
            s.TreatedAs = "Elemental HERO Bubbleman";
            s.SummonOk = (x, by, k) => by == x.Self;
            s.ProcCan = x => x.MyMonsters().Any(m => m.FaceUp && Scripts.NameOf(m) == "Elemental HERO Bubbleman") && x.My.Hand.Any(c => c.Name == "Metamorphosis");
            s.Proc = async x =>
            {
                var b = await x.Pick(x.MyMonsters().Where(m => m.FaceUp && Scripts.NameOf(m) == "Elemental HERO Bubbleman"), "Send Elemental HERO Bubbleman to the GY");
                var m = x.My.Hand.First(c => c.Name == "Metamorphosis");
                if (b == null) return;
                await x.D.ToGrave(b, x.Self); await x.D.ToGrave(m, x.Self);
                await x.D.SpecialSummon(x.Self, x.Me, await x.D.ChoosePosition(x.Me, x.Self), by: x.Self);
            };
            s.On = async (x, e) =>
            {
                if (e.Kind != EvKind.AfterDamage || !Up(x)) return;
                var o = e.Card == x.Self ? e.Other : e.Other == x.Self ? e.Card : null;
                if (o != null && o.Controller == x.Opp && o.Valid()) await x.D.Destroy(o, x.Self);
            };
        }
        S("Elemental HERO Phoenix Enforcer").Forbid = (x, q, c) => q == Q.DestroyByBattle && c == x.Self;
        {
            var s = S("Elemental HERO Plasma Vice");
            s.Allow = (x, q, c) => q == Q.Piercing && c == x.Self;
            s.Can = x => Up(x) && x.My.Hand.Count > 0 && x.Targetable(x.OppMonsters().Where(m => m.Attack)).Any();
            s.Cost = async x =>
            {
                await x.D.Discard(x.Me, await x.PickN(x.My.Hand, 1, 1, "Discard 1 card"), x.Self);
                await x.Target(x.OppMonsters().Where(m => m.Attack), "Destroy which Attack Position monster?");
            };
            s.Run = async x => { if (x.T0 is { } t && t.Valid()) await x.D.Destroy(t, x.Self); };
        }
        {
            var s = S("Elemental HERO Prisma");
            static IEnumerable<string> Named(Card f) => System.Text.RegularExpressions.Regex.Matches(f.Data.Desc.Split('\n')[0], "\"([^\"]+)\"").Select(m => m.Groups[1].Value);
            s.OncePerTurn = true;
            s.Can = x => Up(x) && x.My.Extra.Any(f => f.Data.IsFusion && Named(f).Any(n => x.My.Deck.Any(c => string.Equals(c.Name, n, StringComparison.OrdinalIgnoreCase))));
            s.Run = async x =>
            {
                var f = await x.Pick(x.My.Extra.Where(f => f.Data.IsFusion && Named(f).Any(n => x.My.Deck.Any(c => string.Equals(c.Name, n, StringComparison.OrdinalIgnoreCase)))), "Reveal which Fusion Monster?");
                if (f == null) return;
                x.D.Reveal(x.Opp, f);
                var m = await x.Pick(x.My.Deck.Where(c => Named(f).Any(n => string.Equals(c.Name, n, StringComparison.OrdinalIgnoreCase))), "Send which listed material from your Deck to the GY?");
                if (m == null) return;
                await x.D.ToGrave(m, x.Self);
                x.D.Shuffle(x.Me);
                if (!Up(x)) return;
                x.Self.TempName = m.Name; x.D.Seq++;
                var me = x.Self; var d = x.D;
                d.Later(d.Turn, Phase.End, async () => me.TempName = null);
            };
        }
        {
            var s = S("Elemental HERO Rampart Blaster");
            s.Allow = (x, q, c) => q == Q.AttackFromDefense && c == x.Self && x.Self.FaceUp && !x.OppMonsters().Any();
            s.CalcAtk = (x, c, vs, atk) => c == x.Self && x.D.Attacker == x.Self && x.Self.Defense ? atk / 2 : atk;
        }
        {
            var s = SelfBoost("Elemental HERO Shining Flare Wingman", x => 300 * x.My.Grave.Count(c => c.In("Elemental HERO")));
            s.On = async (x, e) => { if (KilledToGrave(x, e)) await x.D.Damage(x.Opp, e.Other!.Data.Atk, x.Self); };
        }
        {
            var s = SelfBoost("Elemental HERO Shining Phoenix Enforcer", x => 300 * x.My.Grave.Count(c => c.In("Elemental HERO")));
            s.Forbid = (x, q, c) => q == Q.DestroyByBattle && c == x.Self;
        }
        S("Elemental HERO Steam Healer").On = async (x, e) => { if (KilledToGrave(x, e)) await x.D.Gain(x.Me, e.Other!.Data.Atk, x.Self); };
        {
            var s = Contact("Elemental HERO Storm Neos");
            ContactThen(s, async x =>
            {
                foreach (var c in x.D.FieldCards.ToList()) await x.D.ToDeck(c);
                x.D.Shuffle(x.Me); x.D.Shuffle(x.Opp);
            });
            s.OncePerTurn = true;
            s.Can = x => Up(x) && x.AllSpells().Any();
            s.Run = async x => await x.D.Destroy(x.AllSpells().ToList(), x.Self);
        }
        {
            var s = S("Elemental HERO Tempest");
            s.Can = x => Up(x) && x.My.FieldCards.Any(c => c != x.Self) && x.Targetable(x.MyMonsters()).Any();
            s.Cost = async x =>
            {
                var c = await x.Pick(x.My.FieldCards.Where(c => c != x.Self), "Send 1 other card you control to the GY");
                if (c != null) await x.D.ToGrave(c, x.Self);
                await x.Target(x.MyMonsters(), "Which monster can't be destroyed by battle?");
            };
            s.Run = async x => { if (x.T0 is { } t && t.Valid()) x.D.Flags[$"tempest{x.Self.Uid}_{t.Uid}"] = 1; };
            s.Forbid = (x, q, c) => q == Q.DestroyByBattle && c != null && x.D.Flags.ContainsKey($"tempest{x.Self.Uid}_{c.Uid}") && Up(x);
        }
        {
            var s = S("Elemental HERO Thunder Giant");
            s.OncePerTurn = true;
            s.Can = x => Up(x) && x.My.Hand.Count > 0 && x.Targetable(x.D.FaceUpMonsters.Where(m => m.Data.Atk < x.Atk(x.Self))).Any();
            s.Cost = async x =>
            {
                await x.D.Discard(x.Me, await x.PickN(x.My.Hand, 1, 1, "Discard 1 card"), x.Self);
                await x.Target(x.D.FaceUpMonsters.Where(m => m.Data.Atk < x.Atk(x.Self)), "Destroy which monster (lower original ATK)?");
            };
            s.Run = async x => { if (x.T0 is { } t && t.Valid() && t.FaceUp) await x.D.Destroy(t, x.Self); };
        }
        {
            var s = S("Elemental HERO Wild Wingman");
            s.Can = x => Up(x) && x.My.Hand.Count > 0 && x.Targetable(x.AllSpells()).Any();
            s.Cost = async x => { await x.D.Discard(x.Me, await x.PickN(x.My.Hand, 1, 1, "Discard 1 card"), x.Self); await x.Target(x.AllSpells(), "Destroy which Spell/Trap?"); };
            s.Run = async x => { if (x.T0 is { OnField: true } t) await x.D.Destroy(t, x.Self); };
        }
        S("Elemental HERO Wildedge").AttackEach = true;
        S("Elemental HERO Wildheart").Forbid = (x, q, c) => q == Q.AffectedByTrap && c == x.Self;
        Spell("Elemental Recharge", async x => await x.D.Gain(x.Me, 1000 * x.MyMonsters().Count(m => m.FaceUp && m.In("Elemental HERO")), x.Self),
            x => x.MyMonsters().Any(m => m.FaceUp && m.In("Elemental HERO")));
        Spell("Emblem of Dragon Destroyer", async x =>
        {
            var c = await x.Pick(x.My.Deck.Concat(x.My.Grave).Where(c => c.Name == "Buster Blader"), "Add Buster Blader");
            bool deck = c?.Loc == Loc.Deck;
            if (c != null) await x.D.ToHand(c, x.Self);
            if (deck) x.D.Shuffle(x.Me);
        }, x => x.My.Deck.Concat(x.My.Grave).Any(c => c.Name == "Buster Blader"));
        Spell("Embodiment of Apophis", async x => await TrapMonster(x, 1600, 1800, "EARTH", "Reptile", 4),
            x => x.D.Phase is Phase.Main1 or Phase.Main2 && x.D.FreeMonsterZones(x.Me) > 0);
        S("Embodiment of Apophis").Note = "Becomes a Normal Monster (Reptile/EARTH/Level 4/1600/1800) that is also still a Trap.";
        Spell("Emergency Provisions", async x => await x.D.Gain(x.Me, 1000 * x.N, x.Self), x => x.Spells(x.Me).Any(c => c != x.Self),
            async x =>
            {
                var l = await x.PickN(x.Spells(x.Me).Where(c => c != x.Self), 1, 5, "Send which Spell/Trap Cards you control to the GY?");
                foreach (var c in l) await x.D.ToGrave(c, x.Self);
                x.N = l.Count;
            });
        S("Emes the Infinity").On = async (x, e) => { if (KilledToGrave(x, e)) { x.Self.Vars["modatk"] = x.Self.Var("modatk") + 700; x.D.Seq++; } };
        S("Emissary of the Afterlife").On = async (x, e) =>
        {
            if (!e.Is(EvKind.ToGrave, x.Self) || e.From is not (Loc.Monster or Loc.Spell)) return;
            foreach (var p in new[] { x.Me, x.Opp })
            {
                var c = await x.D.PickOne(p, x.D.P(p).Deck.Where(c => c.Data.IsNormalMonster && c.Data.Level <= 3), "Add 1 Level 3 or lower Normal Monster from your Deck", x.Self);
                if (c != null) { await x.D.ToHand(c, x.Self); x.D.Shuffle(p); }
            }
        };
        {
            static bool EN(Card c) => c.Data.IsMonster && (c.In("Elemental HERO") || c.In("Neo-Spacian"));
            var s = Spell("EN Shuffle", async x =>
            {
                if (x.T0 is not { } t || !t.Valid()) return;
                string name = t.Name;
                await x.D.ToDeck(t, shuffle: true);
                if (t.Loc != Loc.Deck) return;
                await SummonPick(x, x.My.Deck.Where(c => EN(c) && c.Name != name), "Special Summon which Elemental HERO / Neo-Spacian?");
                x.D.Shuffle(x.Me);
            }, x => Hopt(x, "1") && x.MyMonsters().Any(m => m.FaceUp && EN(m)), async x => { UseHopt(x, "1"); var t = await x.Pick(x.MyMonsters().Where(m => m.FaceUp && EN(m)), "Shuffle which monster into the Deck?"); if (t != null) x.T.Add(t); });
            s.GraveFx = new Script
            {
                Speed = 1,
                Can = x => Hopt(x, "2") && (x.My.Grave.Any(c => c.Name == "Elemental HERO Neos") || (x.My.Grave.Any(c => c.Data.IsMonster && c.In("Elemental HERO")) && x.My.Grave.Any(c => c.Data.IsMonster && c.In("Neo-Spacian")))),
                Cost = async x => { UseHopt(x, "2"); await x.D.Banish(x.Self, x.Self); },
                Run = async x =>
                {
                    bool neos = x.My.Grave.Any(c => c.Name == "Elemental HERO Neos");
                    bool pair = x.My.Grave.Any(c => c.Data.IsMonster && c.In("Elemental HERO") && c.Name != "Elemental HERO Neos") && x.My.Grave.Any(c => c.Data.IsMonster && c.In("Neo-Spacian"));
                    var back = new List<Card>();
                    if (neos && (!pair || await x.D.ChooseText(x.Me, "EN Shuffle", new[] { "Shuffle Elemental HERO Neos", "Shuffle 1 Elemental HERO and 1 Neo-Spacian" }, x.Self) == 0))
                        back.Add(x.My.Grave.First(c => c.Name == "Elemental HERO Neos"));
                    else
                    {
                        var h = await x.Pick(x.My.Grave.Where(c => c.Data.IsMonster && c.In("Elemental HERO")), "Shuffle which Elemental HERO?");
                        var n = await x.Pick(x.My.Grave.Where(c => c.Data.IsMonster && c.In("Neo-Spacian")), "Shuffle which Neo-Spacian?");
                        if (h != null) back.Add(h); if (n != null) back.Add(n);
                    }
                    foreach (var c in back) await x.D.ToDeck(c);
                    x.D.Shuffle(x.Me);
                    await x.D.Draw(x.Me, 1);
                },
            };
        }
        Spell("Enchanting Fitting Room", async x =>
        {
            var top = x.My.Deck.Take(4).ToList();
            x.D.Reveal(x.Me, top); x.D.Reveal(x.Opp, top);
            foreach (var c in top.Where(c => c.Data.IsNormalMonster && c.Data.Level <= 3))
                if (x.D.CanSpecialSummon(c, x.Me, by: x.Self)) await x.D.SpecialSummon(c, x.Me, await x.D.ChoosePosition(x.Me, c), by: x.Self);
            x.D.Shuffle(x.Me);
        }, x => x.My.Lp > 800 && x.My.Deck.Count >= 4, async x => x.D.Pay(x.Me, 800));
        Spell("End of the World", async x => await RitualSummon(x, m => m.Name is "Ruin, Queen of Oblivion" or "Demise, King of Armageddon", exact: true),
            x => RitualTargets(x, m => m.Name is "Ruin, Queen of Oblivion" or "Demise, King of Armageddon", true).Any());
        Spell("Enemy Controller", async x =>
        {
            if (x.T0 is not { } t || !t.Valid() || !t.FaceUp) return;
            if (x.N == 0) { await x.D.ChangePosition(t, !t.Defense); return; }
            if (await x.D.TakeControl(t, x.Me))
                x.D.Later(x.D.Turn, Phase.End, async () => { if (t.Valid() && t.Controller == x.Me) await x.D.TakeControl(t, t.Owner); });
        }, x => x.Targetable(x.OppMonsters().FaceUp()).Any(),
        async x =>
        {
            bool canTake = x.D.Tributable(x.Me).Any();
            x.N = canTake ? await x.D.ChooseText(x.Me, "Enemy Controller", new[] { "Change a monster's battle position", "Tribute 1 monster to take control of one" }, x.Self) : 0;
            if (x.N == 1) { var t = await x.Pick(x.D.Tributable(x.Me), "Tribute 1 monster"); if (t != null) await x.D.Tribute(t, x.Self); }
            await x.Target(x.OppMonsters().FaceUp(), x.N == 0 ? "Change which monster's battle position?" : "Take control of which monster?");
        });
        Spell("Energy Drain", async x => { if (x.T0 is { } t && t.Valid() && t.FaceUp) x.D.Boost(t, 200 * x.Their.Hand.Count, 200 * x.Their.Hand.Count); },
            x => x.Targetable(x.MyMonsters().FaceUp()).Any(), async x => await x.Target(x.MyMonsters().FaceUp(), "Which monster gains ATK/DEF?"));
        S("Enervating Mist").HandLimit = (x, p) => p == x.Opp ? 5 : -1;
        S("Enraged Battle Ox").Allow = (x, q, c) => q == Q.Piercing && c != null && c.Controller == x.Me && x.D.Race(c) is "Beast" or "Beast-Warrior" or "Winged Beast";
        Sure("Elemental HERO Bladedge", "Elemental HERO Phoenix Enforcer", "Elemental HERO Shining Phoenix Enforcer", "Elemental Recharge", "Emblem of Dragon Destroyer",
            "Emes the Infinity", "Energy Drain", "Enervating Mist", "Enraged Battle Ox", "Elemental HERO Flare Neos", "Elemental HERO Wildedge", "Elemental HERO Mariner");
    }
}
