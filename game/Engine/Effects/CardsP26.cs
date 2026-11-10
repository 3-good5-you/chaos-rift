namespace PoC.Engine;

// Official cards, batch 26 (Valkyrie Erste .. White Night Queen).
public static partial class Scripts
{
    /// Vampire Lady / Vampire Lord: declare a card type; the opponent sends 1 card of that type from their Deck to the GY.
    static async Flow VampireMill(Ctx x)
    {
        int i = await x.D.ChooseText(x.Me, "Declare a card type", new[] { "Monster", "Spell", "Trap" }, x.Self);
        var l = x.Their.Deck.Where(c => i == 0 ? c.Data.IsMonster : i == 1 ? c.Data.IsSpell : c.Data.IsTrap).ToList();
        if (l.Count == 0) return;
        var c = await x.D.PickOne(x.Opp, l, "Send 1 card of the declared type from your Deck to the GY", x.Self);
        if (c != null) { await x.D.ToGrave(c, x.Self); x.D.Shuffle(x.Opp); }
    }

    static void VenomPlacer(Script s, int n, bool noAttack)
    {
        s.OncePerTurn = true;
        s.Can = x => Up(x) && x.OppMonsters().Any();
        s.Run = async x =>
        {
            var t = await x.Pick(x.OppMonsters(), $"Place {n} Venom Counter(s) on which monster?");
            if (t != null) AddCounter(x, t, n, "venom");
            if (noAttack) x.Self.Vars["noattack"] = x.D.Turn;
        };
    }

    static void CardsP26(CardDb db)
    {
        {
            var s = S("Valkyrie Erste");
            s.On = async (x, e) =>
            {
                if (e.Is(EvKind.Summoned, x.Self) && e.From == Loc.Hand && e.Other is { } by && by.Data.IsSpell && Hopt(x, "1") && x.Targetable(x.My.Grave.Where(c => c.Name == "Mischief of the Time Goddess")).Any() && await x.Ask("Add Mischief of the Time Goddess from your GY?"))
                { UseHopt(x, "1"); var c = await x.Pick(x.Targetable(x.My.Grave.Where(c => c.Name == "Mischief of the Time Goddess")), "Add which?"); if (c != null) await x.D.ToHand(c, x.Self); }
            };
            s.AddFx(f =>
            {
                f.Label = "Banish from the opponent's GY: copy its ATK"; f.Speed = 2; f.AnyTurn = true;
                f.Can = x => Up(x) && Hopt(x, "2") && x.MyMonsters().Any(m => m != x.Self && m.FaceUp && m.In("Valkyrie")) && x.Their.Grave.Any(c => c.Data.IsMonster);
                f.Cost = async x => UseHopt(x, "2");
                f.Run = async x =>
                {
                    var c = await x.Pick(x.Their.Grave.Where(c => c.Data.IsMonster), "Banish which monster from your opponent's GY?");
                    if (c == null) return;
                    await x.D.Banish(c, x.Self);
                    if (c.Loc == Loc.Banished && Up(x)) x.D.Boost(x.Self, Math.Max(0, c.Data.Atk) - x.Atk(x.Self));
                };
            });
        }
        S("Valkyrie Zweite").On = async (x, e) =>
        {
            if (e.Is(EvKind.Summoned, x.Self) && Hopt(x, "1") && x.Targetable(x.OppMonsters()).Any() && await x.Ask("Destroy 1 monster your opponent controls?"))
            { UseHopt(x, "1"); var t = await x.Pick(x.Targetable(x.OppMonsters()), "Destroy which monster?"); if (t != null) await x.D.Destroy(t, x.Self); }
            if (e.Kind == EvKind.AfterCalc && (e.Card == x.Self || e.Other == x.Self) && Hopt(x, "2") && x.Targetable(x.My.Grave.Where(c => c.Data.IsSpell && c.Data.Is("Continuous"))).Any() && await x.Ask("Add a Continuous Spell from your GY?"))
            { UseHopt(x, "2"); var t = await x.Pick(x.Targetable(x.My.Grave.Where(c => c.Data.IsSpell && c.Data.Is("Continuous"))), "Add which?"); if (t != null) await x.D.ToHand(t, x.Self); }
        };
        Spell("Valkyrie's Embrace", async x =>
        {
            if (x.T.Count < 2) return;
            var (m, o) = (x.T[0], x.T[1]);
            if (m is { Loc: Loc.Monster, FaceUp: true, Attack: true }) { await x.D.ChangePosition(m, true); if (m.Defense && o.Loc == Loc.Monster) await x.D.Banish(o, x.Self); }
        }, x => Hopt(x) && x.MyMonsters().Any() && x.MyMonsters().All(m => m.FaceUp && m.In("Valkyrie")) && x.Targetable(x.MyMonsters().Where(m => m.Attack)).Any() && x.Targetable(x.OppMonsters().FaceUp()).Any(),
        async x =>
        {
            UseHopt(x);
            var d = x.D; int me = x.Me; d.Linger(d.Turn, (q, c, p) => q == Q.SpecialSummon && p == me && c != null && !c.In("Valkyrie"));
            await x.Target(x.MyMonsters().Where(m => m.FaceUp && m.Attack && m.In("Valkyrie")), "Target your Valkyrie");
            var o = await x.Pick(x.Targetable(x.OppMonsters().FaceUp()), "Target 1 face-up monster your opponent controls");
            if (o != null) x.T.Add(o);
        });
        {
            var s = S("Vampire Genesis");
            s.SummonOk = (x, by, k) => by == x.Self;
            s.ProcCan = x => x.MyMonsters().Any(m => m.FaceUp && Scripts.NameOf(m) == "Vampire Lord");
            s.Proc = async x =>
            {
                var v = await x.Pick(x.MyMonsters().Where(m => m.FaceUp && Scripts.NameOf(m) == "Vampire Lord"), "Banish Vampire Lord");
                if (v == null) return;
                await x.D.Banish(v, x.Self);
                await x.D.SpecialSummon(x.Self, x.Me, await x.D.ChoosePosition(x.Me, x.Self), by: x.Self);
            };
            s.OncePerTurn = true;
            s.Can = x => Up(x) && x.D.FreeMonsterZones(x.Me) > 0 && x.My.Hand.Any(h => h.Data.IsMonster && h.Data.Race == "Zombie" && x.My.Grave.Any(g => g.Data.IsMonster && g.Data.Race == "Zombie" && g.Data.Level < h.Data.Level));
            s.Cost = async x =>
            {
                var h = await x.Pick(x.My.Hand.Where(h => h.Data.IsMonster && h.Data.Race == "Zombie" && x.My.Grave.Any(g => g.Data.IsMonster && g.Data.Race == "Zombie" && g.Data.Level < h.Data.Level)), "Discard which Zombie?");
                if (h == null) return;
                int lv = h.Data.Level;
                await x.D.Discard(x.Me, new[] { h }, x.Self);
                await x.Target(x.My.Grave.Where(g => g != h && g.Data.IsMonster && g.Data.Race == "Zombie" && g.Data.Level < lv && x.D.CanSpecialSummon(g, x.Me, by: x.Self)), "Special Summon which Zombie?");
            };
            s.Run = async x => { if (x.T0 is { Loc: Loc.Grave } t) await x.D.SpecialSummon(t, x.Me, await x.D.ChoosePosition(x.Me, t), by: x.Self); };
        }
        OnBattleDamage("Vampire Lady", async (x, e) => await VampireMill(x));
        {
            var s = OnBattleDamage("Vampire Lord", async (x, e) => await VampireMill(x));
            var on = s.On;
            s.On = async (x, e) =>
            {
                await on!(x, e);
                if (e.Is(EvKind.Destroyed, x.Self) && !e.Battle && x.Self.Loc == Loc.Grave && e.Other?.Controller == x.Opp && x.D.LeftFieldLog.LastOrDefault(l => l.c == x.Self) is { c: not null } l && l.controller == x.Me)
                {
                    var d = x.D; int me = x.Me; var self = x.Self; bool done = false;
                    d.Watch(int.MaxValue, async ev =>
                    {
                        if (done || ev.Kind != EvKind.Standby || ev.Player != me) return;
                        done = true;
                        if (self.Loc == Loc.Grave && d.CanSpecialSummon(self, me, by: self)) await d.SpecialSummon(self, me, await d.ChoosePosition(me, self), by: self);
                    });
                }
            };
        }
        OnBattleDeath("Vampire's Curse", async x =>
        {
            if (x.My.Lp <= 500 || !await x.Ask("Pay 500 LP to Special Summon Vampire's Curse in the next Standby Phase?")) return;
            x.D.Pay(x.Me, 500, x.Self);
            var d = x.D; int me = x.Me, turn = d.Turn; var self = x.Self; bool done = false;
            d.Watch(int.MaxValue, async ev =>
            {
                if (done || ev.Kind != EvKind.Standby || d.Turn != turn + 1) return;
                done = true;
                if (self.Loc == Loc.Grave && d.CanSpecialSummon(self, me, by: self) && await d.SpecialSummon(self, me, await d.ChoosePosition(me, self), by: self)) { self.Vars["modatk"] = self.Var("modatk") + 500; d.Seq++; }
            });
        });
        {
            var s = S("Van'Dalgyon the Dark Dragon Lord");
            s.On = async (x, e) =>
            {
                if (x.Self.Loc != Loc.Hand) return;
                if (e.Kind == EvKind.Negated && e.Player == x.Opp && x.D.Resolving is { } r && r.Player == x.Me && r.Card.Data.IsTrap && r.Card.Data.Is("Counter") && e.Card != null)
                    x.D.Flags[$"vandal{x.Me}"] = x.D.Flags.GetValueOrDefault($"vandal{x.Me}") | (e.Card.Data.IsSpell ? 1 : e.Card.Data.IsTrap ? 2 : 4);
                if (e.Kind == EvKind.Resolved && e.Link is { } l && l.Player == x.Me && l.Card.Data.IsTrap && l.Card.Data.Is("Counter") && x.D.Flags.GetValueOrDefault($"vandal{x.Me}") is var bits && bits != 0)
                {
                    x.D.Flags.Remove($"vandal{x.Me}");
                    if (x.D.Phase == Phase.Battle && x.D.Attacker != null && x.Ev?.Kind == EvKind.DamageCalc) return;
                    if (!x.D.CanSpecialSummon(x.Self, x.Me, by: x.Self) || !await x.Ask("Special Summon Van'Dalgyon the Dark Dragon Lord?")) return;
                    if (!await x.D.SpecialSummon(x.Self, x.Me, await x.D.ChoosePosition(x.Me, x.Self), by: x.Self)) return;
                    if ((bits & 1) != 0) await x.D.Damage(x.Opp, 1500, x.Self);
                    if ((bits & 2) != 0) { var t = await x.Pick(x.Targetable(x.Their.FieldCards), "Destroy which card?"); if (t != null) await x.D.Destroy(t, x.Self); }
                    if ((bits & 4) != 0) await SummonPick(x, x.Targetable(x.My.Grave.Where(c => c.Data.IsMonster)), "Special Summon a monster from your GY");
                }
            };
        }
        Spell("Vehicroid Connection Zone", async x =>
        {
            if (!await x.D.FusionSummon(x.Me, x.Self, resultOk: f => f.In("Vehicroid")) || x.D.LastFusion is not { Loc: Loc.Monster } f) return;
            var d = x.D; d.Linger(int.MaxValue, (q, c, p) => c == f && f.Loc == Loc.Monster && q is Q.DestroyByEffect or Q.Negate && f.Var("vczone") == 1 && !d.Flags.ContainsKey($"vczoff{f.Uid}"));
            f.Vars["vczone"] = 1;
        }, x => x.D.FusionOptions(x.Me, resultOk: f => f.In("Vehicroid"), by: x.Self).Any());
        {
            var s = S("Vennominaga the Deity of Poisonous Snakes");
            s.SummonOk = (x, by, k) => by?.Name is "Rise of the Snake Deity" || by == x.Self;
            s.Stats = (x, c, st) => { if (c == x.Self) st.Add(500 * x.My.Grave.Count(g => g.Data.IsMonster && g.Data.Race == "Reptile"), 0); };
            s.Forbid = (x, q, c) => c == x.Self && Up(x) && ((x.D.Affecting ?? x.D.Targeter) is { } by && by != x.Self && (!by.Data.IsMonster || !by.Data.IsNormalMonster)
                && q is Q.Target or Q.AffectedBySpell or Q.AffectedByTrap or Q.AffectedByMonster or Q.DestroyByEffect);
            s.On = async (x, e) =>
            {
                if (e.Is(EvKind.Destroyed, x.Self) && e.Battle && x.Self.Loc == Loc.Grave)
                {
                    var l = x.My.Grave.Where(c => c != x.Self && c.Data.IsMonster && c.Data.Race == "Reptile").ToList();
                    if (l.Count == 0 || !x.D.CanSpecialSummon(x.Self, x.Me, by: x.Self) || !await x.Ask("Banish a Reptile to Special Summon Vennominaga?")) return;
                    var b = await x.Pick(l, "Banish which Reptile?");
                    if (b == null) return;
                    await x.D.Banish(b, x.Self);
                    if (await x.D.SpecialSummon(x.Self, x.Me, await x.D.ChoosePosition(x.Me, x.Self), by: x.Self))
                    { x.D.Flags[$"hiss{x.Self.Uid}"] = x.D.Flags.GetValueOrDefault($"hiss{x.Self.Uid}") + 1; x.D.Seq++; }
                }
                if (e.Kind == EvKind.BattleDamage && e.Card == x.Self && e.Player == x.Opp && x.D.Flags.GetValueOrDefault($"hiss{x.Self.Uid}") >= 3) x.D.Win(x.Me, "Vennominaga");
            };
            s.Note = "Its card text in the data is cut short: it gets a Hiss Counter each time it revives, and wins when it deals battle damage with 3.";
        }
        {
            var s = S("Vennominon the King of Poisonous Snakes");
            s.SummonOk = (x, by, k) => by == null || by == x.Self || !by.Data.IsMonster || by.Data.IsNormalMonster;
            s.Stats = (x, c, st) => { if (c == x.Self) st.Add(500 * x.My.Grave.Count(g => g.Data.IsMonster && g.Data.Race == "Reptile"), 0); };
            s.On = async (x, e) =>
            {
                if (!e.Is(EvKind.Destroyed, x.Self) || !e.Battle || x.Self.Loc != Loc.Grave) return;
                var l = x.My.Grave.Where(c => c != x.Self && c.Data.IsMonster && c.Data.Race == "Reptile").ToList();
                if (l.Count == 0 || !x.D.CanSpecialSummon(x.Self, x.Me, by: x.Self) || !await x.Ask("Banish a Reptile to Special Summon Vennominon?")) return;
                var b = await x.Pick(l, "Banish which Reptile?");
                if (b != null) { await x.D.Banish(b, x.Self); await x.D.SpecialSummon(x.Self, x.Me, await x.D.ChoosePosition(x.Me, x.Self), by: x.Self); }
            };
        }
        VenomPlacer(S("Venom Boa"), 2, true);
        VenomPlacer(S("Venom Serpent"), 1, false);
        VenomPlacer(S("Venom Snake"), 1, true);
        Spell("Venom Shot", async x =>
        {
            var c = await x.Pick(x.My.Deck.Where(c => c.Data.IsMonster && c.Data.Race == "Reptile"), "Send which Reptile to the GY?");
            x.D.Shuffle(x.Me);
            if (c != null) await x.D.ToGrave(c, x.Self);
            var t = await x.Pick(x.OppMonsters().FaceUp(), "Place 2 Venom Counters on which monster?");
            if (t != null) AddCounter(x, t, 2, "venom");
        }, x => x.MyMonsters().Any(m => m.FaceUp && (m.In("Venom") || Scripts.NameOf(m) is "Vennominaga the Deity of Poisonous Snakes" or "Vennominon the King of Poisonous Snakes")) && x.My.Deck.Any(c => c.Data.IsMonster && c.Data.Race == "Reptile") && x.OppMonsters().Any(m => m.FaceUp));
        {
            var s = S("Venom Swamp");
            static bool Immune(Card c) => c.In("Venom") || c.Name == "Vennominon the King of Poisonous Snakes";
            s.Stats = (x, c, st) => { if (x.Self.FaceUp && x.Self.Loc == Loc.Field && c.Loc == Loc.Monster && c.Counters("venom") > 0 && c.Name != "Vennominon the King of Poisonous Snakes") st.Add(-500 * c.Counters("venom"), 0); };
            s.On = async (x, e) =>
            {
                if (e.Kind != EvKind.EndPhase || !x.Self.FaceUp || x.Self.Loc != Loc.Field) return;
                foreach (var m in x.D.FaceUpMonsters.Where(m => !Immune(m)).ToList()) AddCounter(x, m, 1, "venom");
                await x.D.Destroy(x.D.FaceUpMonsters.Where(m => m.Counters("venom") > 0 && x.Atk(m) == 0 && m.Name != "Vennominon the King of Poisonous Snakes").ToList(), x.Self);
            };
        }
        {
            var s = Spell("Vicious Claw", async x => { if (x.T0 is { } t && t.Valid() && t.FaceUp) await x.D.Equip(x.Self, t); },
                x => !x.D.TurnFlags.Contains($"claw{x.Me}") && x.Targetable(x.D.FaceUpMonsters).Any(), async x => await x.Target(x.D.FaceUpMonsters, "Equip to which monster?"));
            s.Stats = (x, c, st) => { if (c == x.Self.EquippedTo) st.Add(300, 0); };
            s.Forbid = (x, q, c) =>
            {
                if (q != Q.DestroyByBattle || c == null || c != x.Self.EquippedTo || !x.Self.FaceUp || x.D.Phase != Phase.Battle) return false;
                x.D.Flags[$"clawsave{x.Self.Uid}"] = x.D.Turn;
                return true;
            };
            s.On = async (x, e) =>
            {
                if (e.Kind != EvKind.AfterDamage || x.D.Flags.GetValueOrDefault($"clawsave{x.Self.Uid}") != x.D.Turn || !x.Self.OnField) return;
                x.D.Flags.Remove($"clawsave{x.Self.Uid}");
                var m = x.Self.EquippedTo;
                var other = m == e.Card ? e.Other : e.Card;
                int owner = x.Self.Owner;
                await x.D.ToHand(x.Self, x.Self);
                x.D.TurnFlags.Add($"claw{owner}");
                var l = x.D.AllMonsters.Where(k => k != other).ToList();
                if (l.Count > 0) { var t = await x.Pick(l, "Destroy which monster?"); if (t != null) await x.D.Destroy(t, x.Self); }
                await x.D.Damage(x.Opp, 600, x.Self);
                if (x.D.FreeMonsterZones(x.Opp) > 0) { var tok = x.D.MakeToken("Evil Token", x.Opp, 2500, 2500, "DARK", "Fiend", 7); await x.D.SpecialSummon(tok, x.Opp, false, by: x.Self); }
            };
        }
        {
            var s = S("Victory Dragon");
            s.SummonOk = (x, by, k) => false;
            s.TributesNeeded = x => 3;
            s.TributeOk = (x, t) => t.FaceUp ? x.IsRace(t, "Dragon") : t.Data.Race == "Dragon";
            s.Note = "Its \"win the Match\" is a normal duel win here.";
        }
        Spell("Virus Cannon", async x =>
        {
            int n = x.Self.Var("virusn");
            var l = x.Their.Deck.Where(c => c.Data.IsSpell).ToList();
            int k = Math.Min(n, l.Count);
            if (k == 0) return;
            foreach (var c in await x.D.Pick(x.Opp, l, k, k, $"Send {k} Spell(s) from your Deck to the GY", x.Self)) await x.D.ToGrave(c, x.Self);
            x.D.Shuffle(x.Opp);
        }, x => x.D.Tributable(x.Me).Any(m => !m.IsToken),
        async x =>
        {
            var l = await x.PickN(x.D.Tributable(x.Me).Where(m => !m.IsToken), 1, x.D.Tributable(x.Me).Count(m => !m.IsToken), "Tribute how many monsters?");
            foreach (var t in l) await x.D.Tribute(t, x.Self);
            x.Self.Vars["virusn"] = l.Count;
        });
        OnBattleDeath("Volcanic Blaster", async x =>
        {
            var l = x.My.Deck.Where(c => c.Data.IsMonster && c.In("Volcanic")).ToList();
            if (l.Count == 0 || !await x.Ask("Place a Volcanic monster from your Deck on top of your Deck?")) return;
            var c = await x.Pick(l, "Which Volcanic monster?");
            x.D.Shuffle(x.Me);
            if (c != null) { x.My.Deck.Remove(c); x.My.Deck.Insert(0, c); x.D.Seq++; }
        });
        S("Volcanic Counter").On = async (x, e) =>
        {
            if (e.Kind != EvKind.BattleDamage || e.Player != x.Me || x.Self.Loc != Loc.Grave) return;
            int dmg = e.Amount;
            await x.D.Banish(x.Self, x.Self);
            if (x.My.Grave.Any(c => c.Data.IsMonster && c.Data.Attribute == "FIRE" && c.Name != "Volcanic Counter")) await x.D.Damage(x.Opp, dmg, x.Self);
        };
        {
            var s = S("Volcanic Doomfire");
            s.SummonOk = (x, by, k) => by == x.Self;
            s.ProcCan = x => x.My.SpellsOnField.Any(c => c.FaceUp && c.Name == "Tri-Blaze Accelerator");
            s.Proc = async x =>
            {
                await x.D.ToGrave(x.My.SpellsOnField.First(c => c.FaceUp && c.Name == "Tri-Blaze Accelerator"), x.Self);
                await x.D.SpecialSummon(x.Self, x.Me, await x.D.ChoosePosition(x.Me, x.Self), by: x.Self);
            };
            s.Allow = (x, q, c) => q == Q.MustBeAttacked && c == x.Self && Up(x);
            s.On = async (x, e) =>
            {
                if (e.Kind == EvKind.BattleStart && e.Player == x.Opp && Up(x)) foreach (var m in x.OppMonsters().Where(m => m.FaceUp && m.Attack)) m.Vars["mustattack"] = x.D.Turn;
                if (KilledToGrave(x, e))
                {
                    var l = x.OppMonsters().ToList();
                    await x.D.Destroy(l, x.Self);
                    int n = l.Count(m => !m.OnField);
                    if (n > 0) await x.D.Damage(x.Opp, 500 * n, x.Self);
                }
            };
        }
        Spell("Volcanic Eruption", async x => await x.D.Destroy(x.D.FieldCards.Where(c => c != x.Self).ToList(), x.Self),
            x => x.D.Phase == Phase.End && x.D.TurnPlayer == x.Me && x.My.FieldZone is { FaceUp: true } f && Scripts.NameOf(f) == "Jurassic World");
        {
            var s = S("Volcanic Hammerer");
            s.OncePerTurn = true;
            s.Can = x => Up(x) && x.My.Grave.Any(c => c.Data.IsMonster && c.In("Volcanic"));
            s.Run = async x => { x.Self.Vars["noattack"] = x.D.Turn; await x.D.Damage(x.Opp, 200 * x.My.Grave.Count(c => c.Data.IsMonster && c.In("Volcanic")), x.Self); };
        }
        {
            var s = S("Volcanic Queen");
            s.SummonOk = (x, by, k) => by == x.Self;
            s.ProcCan = x => x.D.Tributable(x.Opp).Any();
            s.Proc = async x =>
            {
                var t = await x.Pick(x.D.Tributable(x.Opp), "Tribute 1 monster your opponent controls");
                if (t == null) return;
                await x.D.Tribute(t, x.Self);
                await x.D.SpecialSummon(x.Self, x.Opp, false, by: x.Self);
                var d = x.D; int me = x.Me; d.Linger(d.Turn, (q, c, p) => q is Q.NormalSummon or Q.SetMonster && p == me);
            };
            s.OncePerTurn = true;
            s.Can = x => Up(x) && x.My.FieldCards.Any(c => c != x.Self);
            s.Cost = async x => { var c = await x.Pick(x.My.FieldCards.Where(c => c != x.Self), "Send which card to the GY?"); if (c != null) await x.D.ToGrave(c, x.Self); };
            s.Run = async x => await x.D.Damage(x.Opp, 1000, x.Self);
            s.On = async (x, e) =>
            {
                if (e.Kind != EvKind.EndPhase || e.Player != x.Me || !Up(x)) return;
                var l = x.D.Tributable(x.Me).Where(m => m != x.Self).ToList();
                if (l.Count > 0 && await x.Ask("Volcanic Queen: Tribute 1 other monster (otherwise take 1000 damage)?")) { var t = await x.Pick(l, "Tribute which monster?"); if (t != null) { await x.D.Tribute(t, x.Self); return; } }
                await x.D.Damage(x.Me, 1000, x.Self);
            };
        }
        S("Volcanic Scattershot").On = async (x, e) =>
        {
            if (e.Kind != EvKind.ToGrave || e.Card != x.Self) return;
            await x.D.Damage(x.Opp, 500, x.Self);
            if (e.Other is { } by && by.In("Blaze Accelerator") && x.My.Hand.Concat(x.My.Deck).Count(c => c.Name == "Volcanic Scattershot") >= 2 && await x.Ask("Send 2 Volcanic Scattershots to destroy all monsters your opponent controls?"))
            {
                foreach (var c in await x.PickN(x.My.Hand.Concat(x.My.Deck).Where(c => c.Name == "Volcanic Scattershot"), 2, 2, "Send 2 Volcanic Scattershots")) await x.D.ToGrave(c, x.Self);
                x.D.Shuffle(x.Me);
                await x.D.Destroy(x.OppMonsters().ToList(), x.Self);
            }
        };
        S("Volcanic Shell").AddFx(f =>
        {
            f.Label = "Pay 500: add Volcanic Shell"; f.FromGrave = true;
            f.Can = x => x.Self.Loc == Loc.Grave && Hopt(x) && x.My.Lp > 500 && x.My.Deck.Any(c => c.Name == "Volcanic Shell");
            f.Cost = async x => { UseHopt(x); x.D.Pay(x.Me, 500, x.Self); };
            f.Run = async x => { if (x.Self.Loc == Loc.Grave) await x.Search(c => c.Name == "Volcanic Shell", "Add Volcanic Shell"); };
        });
        {
            var s = S("Volcanic Slicer");
            s.OncePerTurn = true;
            s.Can = x => Up(x);
            s.Run = async x => { x.Self.Vars["noattack"] = x.D.Turn; await x.D.Damage(x.Opp, 500, x.Self); };
        }
        S("Vortex Trooper").On = async (x, e) =>
        {
            if (e.Is(EvKind.Summoned, x.Self) && e.Summoned(SummonKind.Normal, SummonKind.Tribute) && x.My.Hand.Count >= 2 && await x.Ask("Shuffle 2 cards from your hand into the Deck and draw 2?"))
            { foreach (var c in await x.PickN(x.My.Hand, 2, 2, "Shuffle which 2 cards?")) await x.D.ToDeck(c); x.D.Shuffle(x.Me); await x.D.Draw(x.Me, 2); }
            if (e.Is(EvKind.Destroyed, x.Self)) await x.D.Draw(x.Me, 1);
        };
        {
            static void ContactBanish(Script s, string a, string b)
            {
                s.SummonOk = (x, by, k) => by == x.Self;
                s.ProcFromDeck = true;
                s.ProcCan = x => x.Self.Loc == Loc.Extra && x.MyMonsters().Any(m => Scripts.NameOf(m) == a) && x.MyMonsters().Any(m => Scripts.NameOf(m) == b);
                s.Proc = async x =>
                {
                    await x.D.Banish(x.MyMonsters().First(m => Scripts.NameOf(m) == a), x.Self);
                    await x.D.Banish(x.MyMonsters().First(m => Scripts.NameOf(m) == b), x.Self);
                    await x.D.SpecialSummon(x.Self, x.Me, await x.D.ChoosePosition(x.Me, x.Self), by: x.Self);
                };
            }
            var vw = S("VW-Tiger Catapult");
            ContactBanish(vw, "V-Tiger Jet", "W-Wing Catapult");
            vw.Can = x => Up(x) && x.My.Hand.Count > 0 && x.Targetable(x.OppMonsters()).Any();
            vw.Cost = async x => { await x.D.Discard(x.Me, await x.PickN(x.My.Hand, 1, 1, "Discard 1 card"), x.Self); await x.Target(x.OppMonsters(), "Change which monster's position?"); };
            vw.Run = async x => { if (x.T0 is { Loc: Loc.Monster } t) await x.D.ChangePosition(t, !t.Defense, faceUp: true, flipEffect: false); };
            var vz = S("VWXYZ-Dragon Catapult Cannon");
            ContactBanish(vz, "VW-Tiger Catapult", "XYZ-Dragon Cannon");
            vz.OncePerTurn = true;
            vz.Can = x => Up(x) && x.Targetable(x.Their.FieldCards).Any();
            vz.Cost = async x => await x.Target(x.Their.FieldCards, "Banish which card?");
            vz.Run = async x => { if (x.T0 is { OnField: true } t) await x.D.Banish(t, x.Self); };
            vz.On = async (x, e) =>
            {
                if (e.Kind == EvKind.AttackDeclared && e.Card == x.Self && e.Other is { Loc: Loc.Monster } t && x.Targetable(new[] { t }).Any() && await x.Ask($"Change {t.Name}'s battle position?"))
                    await x.D.ChangePosition(t, !t.Defense, faceUp: true, flipEffect: false);
            };
        }
        {
            var s = Union("W-Wing Catapult", (x, m) => Scripts.NameOf(m) == "V-Tiger Jet", effectsToo: true);
            s.Stats = (x, c, st) => { if (x.Self.Var("union") == 1 && c == x.Self.EquippedTo) st.Add(400, 400); };
        }
        {
            var s = S("Wandering Mummy");
            FlipDown(s);
            s.Note = "The face-down monsters are not shuffled around afterwards.";
        }
        OnBattleDeath("Warrior Lady of the Wasteland", x => x.Recruit(c => c.Data.Race == "Warrior" && c.Data.Attribute == "EARTH" && c.Data.Atk is >= 0 and <= 1500, "Warrior Lady of the Wasteland: Special Summon an EARTH Warrior with 1500 or less ATK", false));
        S("Watapon").On = async (x, e) =>
        {
            if (e.Kind == EvKind.ToHand && e.Card == x.Self && e.From == Loc.Deck && e.Other != null && x.D.CanSpecialSummon(x.Self, x.Me, by: x.Self) && await x.Ask("Special Summon Watapon?"))
                await x.D.SpecialSummon(x.Self, x.Me, await x.D.ChoosePosition(x.Me, x.Self), by: x.Self);
        };
        {
            var s = S("Water Dragon");
            s.SummonOk = (x, by, k) => by?.Name == "Bonding - H2O";
            s.Stats = (x, c, st) => { if (Up(x) && c.Loc == Loc.Monster && c.FaceUp && (st.Attr == "FIRE" || st.Race == "Pyro")) st.Add(-1000000, 0); };
            s.On = async (x, e) =>
            {
                if (!e.Is(EvKind.Destroyed, x.Self) || x.Self.Loc != Loc.Grave || x.D.FreeMonsterZones(x.Me) < 3) return;
                var h = x.Targetable(x.My.Grave.Where(c => c.Name == "Hydrogeddon")).ToList();
                var o = x.Targetable(x.My.Grave.Where(c => c.Name == "Oxygeddon")).ToList();
                if (h.Count < 2 || o.Count < 1 || !await x.Ask("Special Summon 2 Hydrogeddon and 1 Oxygeddon from your GY?")) return;
                foreach (var c in h.Take(2).Append(o[0])) if (x.D.CanSpecialSummon(c, x.Me, by: x.Self)) await x.D.SpecialSummon(c, x.Me, await x.D.ChoosePosition(x.Me, c), by: x.Self);
            };
        }
        Spell("Water of Life", async x =>
        {
            if (x.T0 is not { Loc: Loc.Grave } t || !await x.D.SpecialSummon(t, x.Me, false, by: x.Self)) return;
            var d = x.D; int me = x.Me;
            d.Linger(d.Turn, (q, c, p) => q == Q.Activate && c != null && c.Data.IsMonster && c != t && (p == me || c.Controller == me));
        }, x => Hopt(x) && !x.MyMonsters().Any() && x.Targetable(x.My.Grave.Where(c => c.Data.IsMonster && x.D.CanSpecialSummon(c, x.Me, by: x.Self))).Any(),
        async x => { UseHopt(x); await x.Target(x.My.Grave.Where(c => c.Data.IsMonster && x.D.CanSpecialSummon(c, x.Me, by: x.Self)), "Special Summon which monster?"); });
        {
            var s = Spell("Weapon Change", async x => x.Self.Vars["stay"] = 1);
            s.On = async (x, e) =>
            {
                if (e.Kind != EvKind.Standby || e.Player != x.Me || !x.Self.OnField || !x.Self.FaceUp || x.My.Lp <= 700) return;
                var l = x.Targetable(x.MyMonsters().FaceUp().Where(m => x.D.Race(m) is "Warrior" or "Machine")).ToList();
                if (l.Count == 0 || !await x.Ask("Weapon Change: pay 700 LP to switch a monster's ATK and DEF?")) return;
                x.D.Pay(x.Me, 700, x.Self);
                var t = await x.Pick(l, "Switch which monster's ATK and DEF?");
                if (t == null) return;
                var d = x.D; int until = d.Turn + 1;
                t.Vars["swapturn"] = d.Turn; d.Seq++;
                d.Watch(until, async ev => { if (ev.Kind == EvKind.DrawPhase && d.Turn <= until && t.Loc == Loc.Monster) { t.Vars["swapturn"] = d.Turn; d.Seq++; } });
            };
            s.Note = "It switches the original ATK and DEF (so other changes still apply on top).";
        }
        S("Weathering Soldier").On = async (x, e) =>
        {
            if (e.Kind == EvKind.EndPhase && e.Player == x.Me && Up(x)) { x.Self.Vars["modatk"] = x.Self.Var("modatk") - 600; x.D.Seq++; }
            bool battle = e.Is(EvKind.Destroyed, x.Self) && e.Battle;
            bool effect = e.Kind == EvKind.ToGrave && e.Card == x.Self && e.From == Loc.Monster && e.Other != null && !e.Battle && !x.D.Flags.ContainsKey($"battledead{x.Self.Owner}_{x.Self.Uid}");
            if (!(battle || effect) || !Hopt(x)) return;
            var l = x.My.Deck.Where(c => c.Name == "Fossil Fusion" || (Mentions(c.Data, "Fossil Fusion") && c.Name != "Weathering Soldier")).ToList();
            if (l.Count == 0 || !await x.Ask("Add Fossil Fusion (or a card that lists it) from your Deck?")) return;
            UseHopt(x);
            await x.Search(c => l.Contains(c), "Add which card?");
        };
        S("Whirlwind Prodigy").TributeWeight = (x, t, forCard) => forCard.Data.Attribute == "WIND" ? 2 : 1;
        Spell("White Dragon Ritual", async x => await RitualSummon(x, m => m.Name == "Paladin of White Dragon", exact: false), x => RitualTargets(x, m => m.Name == "Paladin of White Dragon", exact: false).Any());
        {
            var s = S("White Knight Lord");
            s.AddFx(f =>
            {
                f.Label = "Banish 3: Special Summon"; f.FromHand = true;
                f.Can = x => x.Self.Loc == Loc.Hand && Hopt(x) && x.My.Grave.Count(c => c.Data.IsMonster) >= 3 && x.D.CanSpecialSummon(x.Self, x.Me, by: x.Self);
                f.Cost = async x => { UseHopt(x); foreach (var c in await x.PickN(x.My.Grave.Where(c => c.Data.IsMonster), 3, 3, "Banish 3 monsters from your GY")) await x.D.Banish(c, x.Self); };
                f.Run = async x =>
                {
                    if (x.Self.Loc != Loc.Hand || !await x.D.SpecialSummon(x.Self, x.Me, await x.D.ChoosePosition(x.Me, x.Self), by: x.Self)) return;
                    var mons = x.My.Hand.Concat(x.My.Deck).Where(c => c.Data.IsMonster && (c.In("White Knight") || c.In("White Night")) && x.D.CanSpecialSummon(c, x.Me, by: x.Self)).ToList();
                    bool veil = x.My.Deck.Any(c => c.Name == "White Veil");
                    var opts = new List<string>(); if (mons.Count > 0) opts.Add("Special Summon a White Knight/White Night monster"); if (veil) opts.Add("Add White Veil"); opts.Add("Nothing");
                    string pick = opts[await x.D.ChooseText(x.Me, "White Knight Lord", opts, x.Self)];
                    if (pick.StartsWith("Special")) await SummonPick(x, mons, "Special Summon which?");
                    else if (pick.StartsWith("Add")) await x.Search(c => c.Name == "White Veil", "Add White Veil");
                    x.D.Shuffle(x.Me);
                };
            });
            s.On = async (x, e) =>
            {
                if (e.Kind == EvKind.AttackDeclared && Up(x) && (e.Card == x.Self || e.Other == x.Self))
                { var o = e.Card == x.Self ? e.Other : e.Card; if (o is { Loc: Loc.Monster, FaceUp: true } && o.Controller == x.Opp) x.D.Boost(o, -x.Atk(o)); }
                if (e.Is(EvKind.Destroyed, x.Self) && e.Other?.Controller == x.Opp && x.Self.Owner == x.Me && x.D.LeftFieldLog.LastOrDefault(l => l.c == x.Self) is { c: not null } l && l.controller == x.Me)
                    await x.D.Damage(x.Opp, 1000, x.Self);
            };
            s.Note = "The opponent's monster's ATK becomes 0 until the end of the turn.";
        }
        MyStandby("White Magician Pikeru", async x => await x.D.Gain(x.Me, 400 * x.MyMonsters().Count(), x.Self));
        {
            var s = S("White Night Dragon");
            s.On = async (x, e) =>
            {
                if (!Up(x)) return;
                if (e.Kind == EvKind.Activated && e.Link is { IsCardActivation: true } l && !l.Card.Data.IsMonster && l.X.T.Contains(x.Self)) { await x.D.Negate(l); return; }
                if (e.Kind == EvKind.AttackDeclared && e.Player == x.Opp && e.Other is { FaceUp: true } t && t != x.Self && t.Controller == x.Me && x.My.SpellsOnField.Any()
                    && await x.Ask("White Night Dragon: send 1 Spell/Trap to change the attack target to it?"))
                {
                    var c = await x.Pick(x.My.SpellsOnField, "Send which Spell/Trap to the GY?");
                    if (c != null) { await x.D.ToGrave(c, x.Self); x.D.AttackTarget = x.Self; }
                }
            };
        }
        {
            var s = S("White Night Queen");
            s.SummonOk = (x, by, k) => false;
            s.OncePerTurn = true;
            s.Can = x => Up(x) && x.Targetable(x.D.FieldCards.Where(c => !c.FaceUp && c != x.Self)).Any();
            s.Cost = async x => await x.Target(x.D.FieldCards.Where(c => !c.FaceUp && c != x.Self), "Destroy which Set card?");
            s.Run = async x => { if (x.T0 is { OnField: true } t) await x.D.Destroy(t, x.Self); };
        }
        Sure("Valkyrie Zweite", "Vampire Lady", "Vampire Lord", "Venom Serpent", "Venom Snake", "Venom Boa", "Venom Shot", "Volcanic Blaster", "Volcanic Counter", "Volcanic Hammerer", "Volcanic Slicer", "Volcanic Shell",
            "Vortex Trooper", "Warrior Lady of the Wasteland", "Watapon", "Whirlwind Prodigy", "White Dragon Ritual", "White Magician Pikeru", "White Night Queen", "Victory Dragon", "Water of Life", "W-Wing Catapult");
    }
}
