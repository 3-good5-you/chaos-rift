namespace PoC.Engine;

// Official cards, batch 16 (Last Turn .. Magician's Circle).
public static partial class Scripts
{
    static bool IsLV(Card c) => c.Data.IsMonster && System.Text.RegularExpressions.Regex.IsMatch(c.Name, @" LV\d+$");
    static string LVBase(Card c) => System.Text.RegularExpressions.Regex.Replace(c.Name, @" LV\d+$", "");
    static int LVOf(Card c) => int.Parse(System.Text.RegularExpressions.Regex.Match(c.Name, @"LV(\d+)$").Groups[1].Value);

    /// Legendary Knights: summoned only by Legend of Heart; when Special Summoned, banish 1 face-up Spell/Trap.
    static Script LegendaryKnight(string name)
    {
        var s = S(name);
        s.SummonOk = (x, by, k) => by?.Name == "Legend of Heart";
        s.On = async (x, e) =>
        {
            if (!e.Is(EvKind.Summoned, x.Self) || !x.Targetable(x.AllSpells().Where(c => c.FaceUp)).Any() || !await x.Ask("Banish 1 face-up Spell/Trap on the field?")) return;
            var t = await x.Pick(x.Targetable(x.AllSpells().Where(c => c.FaceUp)), "Banish which Spell/Trap?");
            if (t != null) await x.D.Banish(t, x.Self);
        };
        return s;
    }

    static async Flow SetFromGrave(Ctx x, Card t, bool actNow)
    {
        if (t.Loc != Loc.Grave || (t.Data.Is("Field") ? x.My.FieldZone != null : x.My.FreeSpellZones == 0)) return;
        if (!x.D.Place(t, x.Me, t.Data.Is("Field") ? Loc.Field : Loc.Spell)) return;
        t.FaceUp = false; t.SetTurn = actNow ? -1 : x.D.Turn;
        await x.D.Raise(new Ev { Kind = EvKind.Set, Card = t, Player = x.Me });
    }

    static void CardsP16(CardDb db)
    {
        {
            var s = Spell("Last Turn", async x =>
            {
                var keep = await x.Pick(x.MyMonsters(), "Keep which monster?");
                if (keep == null) return;
                foreach (var c in x.D.FieldCards.Where(c => c != keep && c != x.Self).ToList()) await x.D.ToGrave(c, x.Self);
                foreach (var p in x.D.Players) foreach (var c in p.Hand.ToList()) await x.D.ToGrave(c, x.Self);
                var l = x.Their.Deck.Where(c => c.Data.IsMonster && !c.Data.IsFusion && x.D.S(c)?.SummonOk == null && x.D.CanSpecialSummon(c, x.Opp, by: x.Self)).ToList();
                var m = l.Count > 0 ? await x.D.PickOne(x.Opp, l, "Special Summon 1 monster from your Deck", x.Self) : null;
                if (m != null && await x.D.SpecialSummon(m, x.Opp, false, by: x.Self)) { x.D.Shuffle(x.Opp); if (keep.Loc == Loc.Monster) x.D.ForcedBattles.Add((m, keep)); }
                var d = x.D; int me = x.Me;
                d.Flags[$"lastturn{me}"] = d.Turn;
                d.Later(d.Turn, Phase.End, async () =>
                {
                    var a = d.P(me).MonstersOnField.Any(); var b = d.P(1 - me).MonstersOnField.Any();
                    if (a && !b) d.Win(me, "Last Turn");
                    else if (b && !a) d.Win(1 - me, "Last Turn");
                    else d.Win(-1, "Last Turn");
                });
            }, x => x.D.TurnPlayer == x.Opp && x.My.Lp <= 1000 && x.MyMonsters().Any());
            s.BattleDamage = (x, src, to, dmg) => x.D.Flags.GetValueOrDefault($"lastturn{x.Me}") == x.D.Turn && x.D.Flags.ContainsKey($"lastturn{x.Me}") ? 0 : dmg;
            s.Note = "The forced battle happens in your opponent's Battle Phase; the winner is decided in that turn's End Phase.";
        }
        {
            var s = S("Lava Golem");
            s.SummonOk = (x, by, k) => by == x.Self;
            s.ProcCan = x => x.D.Tributable(x.Opp).Count() >= 2;
            s.Proc = async x =>
            {
                var l = await x.PickN(x.D.Tributable(x.Opp), 2, 2, "Tribute 2 monsters your opponent controls");
                foreach (var t in l) await x.D.Tribute(t, x.Self);
                await x.D.SpecialSummon(x.Self, x.Opp, false, by: x.Self);
                var d = x.D; int me = x.Me;
                d.Linger(d.Turn, (q, c, p) => q is Q.NormalSummon or Q.SetMonster && p == me);
            };
            s.On = async (x, e) => { if (e.Kind == EvKind.Standby && e.Player == x.Me && Up(x)) await x.D.Damage(x.Me, 1000, x.Self); };
        }
        Spell("Left Arm Offering", async x =>
        {
            foreach (var c in x.My.Hand.ToList()) await x.D.Banish(c, x.Self);
            await x.Search(c => c.Data.IsSpell, "Add which Spell?");
            var d = x.D; int me = x.Me;
            d.Linger(d.Turn, (q, c, p) => q == Q.SetSpell && p == me);
        }, x => x.My.Hand.Count(c => c != x.Self) >= 2 && x.My.Deck.Any(c => c.Data.IsSpell));
        S("Legacy Hunter").On = async (x, e) =>
        {
            if (e.Kind == EvKind.AttackDeclared && e.Card == x.Self) x.D.Flags[$"legacy{x.Self.Uid}"] = e.Other is { FaceUp: false, Defense: true } t ? t.Uid + 1 : 0;
            if (KilledToGrave(x, e) && x.D.Flags.GetValueOrDefault($"legacy{x.Self.Uid}") == e.Other!.Uid + 1 && x.Their.Hand.Count > 0)
            {
                var c = x.Their.Hand[x.D.Rng.Int(x.Their.Hand.Count)];
                await x.D.ToDeck(c, shuffle: true);
            }
        };
        {
            static bool Dragon(Card c) => c.Data.IsSpell && c.In("Legendary Dragon");
            Spell("Legend of Heart", async x =>
            {
                var dragons = x.My.Hand.Concat(x.My.Grave).Where(Dragon).GroupBy(c => c.Name).Select(g => g.First()).ToList();
                int max = Math.Min(3, Math.Min(dragons.Count, x.D.FreeMonsterZones(x.Me)));
                if (max == 0) return;
                var ban = await x.PickN(dragons, 1, max, $"Banish up to {max} Legendary Dragon Spells with different names");
                foreach (var c in ban) await x.D.Banish(c, x.Self);
                var used = new HashSet<string>();
                for (int i = 0; i < ban.Count; i++)
                {
                    var l = x.My.Hand.Concat(x.My.Deck).Concat(x.My.Grave).Where(c => c.Data.IsMonster && c.In("Legendary Knight") && !used.Contains(c.Name) && x.D.CanSpecialSummon(c, x.Me, by: x.Self)).ToList();
                    var k = await x.Pick(l, "Special Summon which Legendary Knight?");
                    if (k == null) break;
                    used.Add(k.Name);
                    await x.D.SpecialSummon(k, x.Me, await x.D.ChoosePosition(x.Me, k), by: x.Self);
                }
                x.D.Shuffle(x.Me);
            }, x => Hopt(x) && x.My.Lp > 2000 && x.D.Tributable(x.Me).Any(m => m.FaceUp ? x.IsRace(m, "Warrior") : m.Data.Race == "Warrior") && x.My.Hand.Concat(x.My.Grave).Any(Dragon),
            async x =>
            {
                UseHopt(x); x.D.Pay(x.Me, 2000, x.Self);
                var t = await x.Pick(x.D.Tributable(x.Me).Where(m => m.FaceUp ? x.IsRace(m, "Warrior") : m.Data.Race == "Warrior"), "Tribute 1 Warrior");
                if (t != null) await x.D.Tribute(t, x.Self);
            });
        }
        MyStandby("Legendary Fiend", async x => { if (Up(x)) { x.Self.Vars["modatk"] = x.Self.Var("modatk") + 700; x.D.Seq++; } });
        {
            var s = LegendaryKnight("Legendary Knight Critias");
            var on = s.On;
            s.On = async (x, e) =>
            {
                await on!(x, e);
                if (e.Kind == EvKind.AttackDeclared && e.Other == x.Self && Up(x) && x.My.FreeSpellZones > 0 && x.Targetable(x.My.Grave.Where(c => c.Data.IsTrap)).Any() && await x.Ask("Set 1 Trap from your GY?"))
                { var t = await x.Pick(x.Targetable(x.My.Grave.Where(c => c.Data.IsTrap)), "Set which Trap?"); if (t != null) await SetFromGrave(x, t, true); }
            };
        }
        {
            var s = LegendaryKnight("Legendary Knight Hermos");
            var on = s.On;
            s.On = async (x, e) =>
            {
                await on!(x, e);
                if (e.Kind == EvKind.AttackDeclared && e.Other == x.Self && Up(x) && Hopt(x) && x.Targetable(x.My.Grave.Where(c => (c.Data.IsMonster && !c.Data.IsNormalMonster))).Any() && await x.Ask("Copy the name and effect of an Effect Monster in your GY?"))
                {
                    UseHopt(x);
                    var t = await x.Pick(x.Targetable(x.My.Grave.Where(c => (c.Data.IsMonster && !c.Data.IsNormalMonster))), "Copy which monster?");
                    if (t == null) return;
                    x.Self.Vars["copyidx"] = t.Data.Idx + 1;
                    int until = x.D.TurnPlayer == x.Me ? x.D.Turn + 2 : x.D.Turn + 1;
                    x.Self.Vars["copyturn"] = x.D.Turn;
                    var d = x.D; var self = x.Self;
                    d.Watch(until, async ev => { if (ev.Kind == EvKind.MainStart && self.Var("copyidx") > 0 && d.Turn <= until) self.Vars["copyturn"] = d.Turn; });
                    d.Later(until, Phase.End, async () => { self.Vars["copyidx"] = 0; self.Vars["copyturn"] = 0; });
                }
            };
            s.Note = "It copies the GY monster's name and effects until the End Phase of your next turn.";
        }
        {
            var s = LegendaryKnight("Legendary Knight Timaeus");
            var on = s.On;
            s.On = async (x, e) =>
            {
                await on!(x, e);
                if (e.Kind == EvKind.AttackDeclared && e.Other == x.Self && Up(x) && x.Targetable(x.My.Grave.Where(c => c.Data.IsSpell)).Any() && await x.Ask("Set 1 Spell from your GY?"))
                { var t = await x.Pick(x.Targetable(x.My.Grave.Where(c => c.Data.IsSpell)), "Set which Spell?"); if (t != null) await SetFromGrave(x, t, false); }
            };
        }
        {
            var s = S("Legion the Fiend Jester");
            s.AddFx(f =>
            {
                f.Label = "Extra Tribute Summon (Spellcaster)";
                f.Can = x => Up(x) && !x.D.TurnFlags.Contains($"legion{x.Me}") && x.My.Hand.Any(c => c.Data.IsMonster && c.Data.Race == "Spellcaster" && c.Data.Level >= 5);
                f.Cost = async x => x.D.TurnFlags.Add($"legion{x.Me}");
                f.Run = async x => x.My.ExtraSummons++;
            });
            s.On = async (x, e) =>
            {
                if (e.Kind == EvKind.ToGrave && e.Card == x.Self && e.From == Loc.Monster && Hopt(x))
                {
                    var l = x.My.Deck.Concat(x.My.Grave).Where(c => c.Data.IsNormalMonster && c.Data.Race == "Spellcaster").ToList();
                    if (l.Count == 0 || !await x.Ask("Add a Spellcaster Normal Monster from your Deck or GY?")) return;
                    UseHopt(x);
                    var c = await x.Pick(l, "Add which?");
                    if (c != null) { bool deck = c.Loc == Loc.Deck; await x.D.ToHand(c, x.Self); x.D.Reveal(x.Opp, c); if (deck) x.D.Shuffle(x.Me); }
                }
            };
            s.Note = "The extra Tribute Summon is given as one more Normal Summon: use it on a Spellcaster in Attack Position.";
        }
        {
            var s = S("Lekunga");
            s.Can = x => Up(x) && x.D.FreeMonsterZones(x.Me) > 0 && x.My.Grave.Count(c => c.Data.IsMonster && c.Data.Attribute == "WATER") >= 2;
            s.Cost = async x => { foreach (var c in await x.PickN(x.My.Grave.Where(c => c.Data.IsMonster && c.Data.Attribute == "WATER"), 2, 2, "Banish 2 WATER monsters")) await x.D.Banish(c, x.Self); };
            s.Run = async x => { var t = x.D.MakeToken("Lekunga Token", x.Me, 700, 700, "WATER", "Plant", 2); await x.D.SpecialSummon(t, x.Me, false, by: x.Self); };
        }
        Spell("Level Down!?", async x =>
        {
            if (x.T0 is not { Loc: Loc.Monster, FaceUp: true } t) return;
            int owner = t.Owner; string b = LVBase(t); int lv = LVOf(t);
            var l = x.D.P(owner).Grave.Where(c => IsLV(c) && LVBase(c) == b && LVOf(c) < lv).ToList();
            await x.D.ToDeck(t, shuffle: true);
            if (l.Count == 0) return;
            var m = await x.D.PickOne(owner, l, "Special Summon which?", x.Self);
            if (m != null && x.D.CanSpecialSummon(m, owner, by: x.Self, ignore: true)) await x.D.SpecialSummon(m, owner, await x.D.ChoosePosition(owner, m), by: x.Self, ignore: true);
        }, x => x.Targetable(x.D.FaceUpMonsters.Where(IsLV)).Any(), async x => await x.Target(x.D.FaceUpMonsters.Where(IsLV), "Target which LV monster?"));
        Spell("Level Modulation", async x =>
        {
            await x.D.Draw(x.Opp, 2);
            var l = x.My.Grave.Where(c => IsLV(c) && x.D.CanSpecialSummon(c, x.Me, by: x.Self, ignore: true)).ToList();
            var m = await x.Pick(l, "Special Summon which LV monster?");
            if (m == null || !await x.D.SpecialSummon(m, x.Me, await x.D.ChoosePosition(x.Me, m), by: x.Self, ignore: true)) return;
            m.Vars["noattack"] = x.D.Turn; m.Vars["noactturn"] = x.D.Turn;
            x.D.Flags[$"neg{m.Uid}"] = 1; var d = x.D; d.Later(d.Turn, Phase.End, async () => d.Flags.Remove($"neg{m.Uid}"));
        }, x => x.My.Grave.Any(c => IsLV(c) && x.D.CanSpecialSummon(c, x.Me, by: x.Self, ignore: true)));
        {
            static IEnumerable<Card> Next(Ctx x, Card lv) => x.My.Hand.Concat(x.My.Deck).Where(c => c.Data.IsMonster && Mentions(lv.Data, c.Name) && c.Name != lv.Name);
            Spell("Level Up!", async x =>
            {
                if (x.T.FirstOrDefault() is not { } sent) return;
                await SummonPick(x, Next(x, sent), "Special Summon which monster?", ignore: true);
                x.D.Shuffle(x.Me);
            }, x => x.MyMonsters().Any(m => m.FaceUp && IsLV(m) && Next(x, m).Any()),
            async x =>
            {
                var m = await x.Pick(x.MyMonsters().Where(m => m.FaceUp && IsLV(m) && Next(x, m).Any()), "Send which LV monster to the GY?");
                if (m == null) return;
                x.T.Add(m);
                await x.D.ToGrave(m, x.Self);
            });
        }
        {
            static bool Umi(Ctx x) => x.My.FieldCards.Any(c => c.FaceUp && Scripts.NameOf(c) == "Umi");
            var s = S("Levia-Dragon - Daedalus");
            s.Can = x => Up(x) && Umi(x);
            s.Cost = async x => { var u = await x.Pick(x.My.FieldCards.Where(c => c.FaceUp && Scripts.NameOf(c) == "Umi"), "Send which Umi to the GY?"); if (u != null) await x.D.ToGrave(u, x.Self); };
            s.Run = async x => await x.D.Destroy(x.D.FieldCards.Where(c => c != x.Self).ToList(), x.Self);
        }
        {
            var s = Spell("Life Absorbing Machine", async x => x.Self.Vars["stay"] = 1);
            s.On = async (x, e) =>
            {
                if (e.Kind != EvKind.Standby || e.Player != x.Me || !x.Self.OnField || !x.Self.FaceUp) return;
                int paid = x.D.Flags.GetValueOrDefault($"paid{x.Me}:{x.D.Turn - 2}");
                if (paid > 0) await x.D.Gain(x.Me, paid / 2, x.Self);
            };
        }
        {
            var s = Spell("Life Shaver", async x => x.Self.Vars["stay"] = 1, x => !x.My.SpellsOnField.Any(c => c != x.Self && c.FaceUp && c.Name == "Life Shaver"));
            s.On = async (x, e) => { if (e.Kind == EvKind.EndPhase && e.Player == x.Opp && x.Self.OnField && x.Self.FaceUp) AddCounter(x, x.Self); };
            s.AddFx(f =>
            {
                f.Label = "Send: opponent discards"; f.InSpellZone = true; f.Speed = 2; f.AnyTurn = true;
                f.Can = x => x.Self.FaceUp && x.Self.Counters() > 0 && x.D.Phase is Phase.Main1 or Phase.Battle or Phase.Main2;
                f.Cost = async x => { x.D.Flags[$"shaver{x.Self.Uid}"] = x.Self.Counters(); await x.D.ToGrave(x.Self, x.Self); };
                f.Run = async x =>
                {
                    int n = Math.Min(x.D.Flags.GetValueOrDefault($"shaver{x.Self.Uid}"), x.Their.Hand.Count);
                    if (n > 0) await x.D.Discard(x.Opp, await x.D.Pick(x.Opp, x.Their.Hand, n, n, $"Discard {n} card(s)", x.Self), x.Self);
                };
            });
        }
        {
            var s = S("Light Barrier");
            s.On = async (x, e) =>
            {
                if (!x.Self.OnField || !x.Self.FaceUp) return;
                if (e.Kind == EvKind.Standby && e.Player == x.Me)
                {
                    bool heads = x.D.TossCoins(x.Me, 1, x.Self) == 1;
                    if (!heads) x.D.Flags[$"neg{x.Self.Uid}"] = 1; else x.D.Flags.Remove($"neg{x.Self.Uid}");
                    x.D.Seq++;
                }
                if (e.Kind == EvKind.BattleDestroyedMonster && e.Card!.Controller == x.Me && e.Card.In("Arcana Force") && e.Other!.Owner == x.Opp && !x.D.Negated(x.Self))
                    await x.D.Gain(x.Me, Math.Max(0, e.Other.Data.Atk), x.Self);
            };
        }
        {
            static bool Ok(Card m) => m.Data.Attribute == "LIGHT" && m.Data.Race == "Warrior";
            var s = Spell("Light Laser", async x => { if (x.T0 is { } t && t.Valid() && t.FaceUp) await x.D.Equip(x.Self, t); },
                x => x.Targetable(x.D.FaceUpMonsters.Where(m => x.IsAttr(m, "LIGHT") && x.IsRace(m, "Warrior"))).Any(),
                async x => await x.Target(x.D.FaceUpMonsters.Where(m => x.IsAttr(m, "LIGHT") && x.IsRace(m, "Warrior")), "Equip to which LIGHT Warrior?"));
            s.EquipOk = (x, m) => x.IsAttr(m, "LIGHT") && x.IsRace(m, "Warrior");
            s.On = async (x, e) =>
            {
                if (e.Kind != EvKind.AfterDamage || !x.Self.OnField || x.Self.EquippedTo is not { } m) return;
                var o = e.Card == m ? e.Other : e.Other == m ? e.Card : null;
                if (o != null && o.Loc == Loc.Monster) await x.D.Banish(o, x.Self);
            };
        }
        Spell("Lightning Crash", async x =>
        {
            var c = await x.Pick(x.My.Deck.Where(c => c.Data.IsMonster && c.Data.Race == "Thunder"), "Add which Thunder monster?");
            x.D.Shuffle(x.Me);
            if (c == null) return;
            await x.D.ToHand(c, x.Self); x.D.Reveal(x.Opp, c);
            var d = x.D; int me = x.Me; string n = c.Name;
            d.Linger(d.Turn + 1, (q, k, p) => q is Q.NormalSummon or Q.SetMonster && p == me && k != null && k.Name == n);
        }, x => Hopt(x) && x.My.Deck.Any(c => c.Data.IsMonster && c.Data.Race == "Thunder"), async x => UseHopt(x));
        {
            var s = Spell("Limit Reverse", async x =>
            {
                if (x.T0 is not { Loc: Loc.Grave } t || !x.Self.OnField) return;
                if (await x.D.SpecialSummon(t, x.Me, false, by: x.Self)) { Tie(x, t); x.Self.Vars["stay"] = 1; }
            }, x => x.Targetable(x.My.Grave.Where(c => c.Data.IsMonster && c.Data.Atk is >= 0 and <= 1000 && x.D.CanSpecialSummon(c, x.Me, by: x.Self))).Any(),
            async x => await x.Target(x.My.Grave.Where(c => c.Data.IsMonster && c.Data.Atk is >= 0 and <= 1000 && x.D.CanSpecialSummon(c, x.Me, by: x.Self)), "Special Summon which monster?"));
            s.On = async (x, e) =>
            {
                var t = TiedMonster(x);
                if (e.Kind == EvKind.LeftField && e.Card == x.Self && t is { Loc: Loc.Monster }) { await x.D.Destroy(t, x.Self); return; }
                if (!x.Self.OnField || t == null) return;
                if (e.Kind == EvKind.PositionChanged && e.Card == t && t.Defense) { await x.D.Destroy(new[] { t, x.Self }, x.Self); return; }
                if (e.Kind == EvKind.LeftField && e.Card == t) await x.D.Destroy(x.Self, x.Self);
            };
        }
        {
            var s = Spell("Litmus Doom Ritual", async x => await RitualSummon(x, m => m.Name == "Litmus Doom Swordsman", exact: false),
                x => RitualTargets(x, m => m.Name == "Litmus Doom Swordsman", exact: false).Any());
            s.GraveFx = new Script
            {
                Can = x => Hopt(x, "gy") && x.Targetable(x.My.Grave.Where(c => c.Name == "Litmus Doom Swordsman")).Any(),
                Cost = async x => { UseHopt(x, "gy"); await x.Target(x.My.Grave.Where(c => c.Name == "Litmus Doom Swordsman"), "Shuffle which Litmus Doom Swordsman?"); },
                Run = async x =>
                {
                    if (x.T0 is not { Loc: Loc.Grave } t || x.Self.Loc != Loc.Grave) return;
                    await x.D.ToDeck(t); await x.D.ToDeck(x.Self); x.D.Shuffle(x.Me);
                    await x.D.Draw(x.Me, 1);
                },
            };
        }
        {
            var s = S("Litmus Doom Swordsman");
            s.Forbid = (x, q, c) => c == x.Self && (q == Q.DestroyByBattle || (q == Q.AffectedByTrap && Up(x)));
            s.Stats = (x, c, st) => { if (c == x.Self && x.D.FieldCards.Any(f => f.FaceUp && f.Data.IsTrap && f.Loc is Loc.Spell or Loc.Field)) st.Add(3000, 3000); };   // face-up Traps only (a Set card is not known to be a Trap)
            s.On = async (x, e) =>
            {
                if (!e.Is(EvKind.Destroyed, x.Self) || x.Self.Summoned != SummonKind.Ritual || e.Other?.Controller != x.Opp || x.My.FreeSpellZones == 0) return;
                var l = x.Targetable(x.D.Players.SelectMany(p => p.Grave).Where(c => c.Data.IsTrap)).ToList();
                if (l.Count == 0 || !await x.Ask("Set 1 Trap from either GY?")) return;
                var t = await x.Pick(l, "Set which Trap?");
                if (t != null) await SetFromGrave(x, t, false);
            };
        }
        S("Little-Winguard").On = async (x, e) =>
        {
            if (e.Kind == EvKind.EndPhase && e.Player == x.Me && Up(x) && x.Self.Var("wingturn") != x.D.Turn && await x.Ask("Change Little-Winguard's battle position?"))
            { x.Self.Vars["wingturn"] = x.D.Turn; await x.D.ChangePosition(x.Self, !x.Self.Defense); }
        };
        {
            var s = Spell("Living Fossil", async x =>
            {
                if (x.T0 is not { Loc: Loc.Grave } t || !x.Self.OnField) return;
                if (!await x.D.SpecialSummon(t, x.Me, await x.D.ChoosePosition(x.Me, t), by: x.Self)) return;
                t.Vars["leavebanish"] = 1;
                await x.D.Equip(x.Self, t);
            }, x => Hopt(x) && x.Targetable(x.My.Grave.Where(c => c.Data.IsMonster && c.Data.Level <= 4 && x.D.CanSpecialSummon(c, x.Me, by: x.Self))).Any(),
            async x => { UseHopt(x); await x.Target(x.My.Grave.Where(c => c.Data.IsMonster && c.Data.Level <= 4 && x.D.CanSpecialSummon(c, x.Me, by: x.Self)), "Special Summon which monster?"); });
            s.Stats = (x, c, st) => { if (c == x.Self.EquippedTo) st.Add(-1000, -1000); };
            s.Forbid = (x, q, c) => q == Q.Negate && c != null && c == x.Self.EquippedTo;
            s.On = async (x, e) =>
            {
                if (e.Kind == EvKind.Equipped && e.Card == x.Self) x.D.Flags[$"fossilhost{x.Self.Uid}"] = e.Other!.Uid + 1;
                if (e.Kind == EvKind.LeftField && e.Card == x.Self && x.D.Flags.GetValueOrDefault($"fossilhost{x.Self.Uid}") is > 0 and var u2 && x.D.CardByUid(u2 - 1) is { Loc: Loc.Monster } m2)
                { x.D.Flags.Remove($"fossilhost{x.Self.Uid}"); await x.D.Banish(m2, x.Self); }
            };
        }
        {
            var s = Spell("Loge's Flame", async x => x.Self.Vars["stay"] = 1);
            s.Forbid = (x, q, c) => q == Q.Attack && x.Self.FaceUp && x.Self.OnField && c != null && c.Controller == x.Opp && x.Atk(c) <= 2000 && x.MyMonsters().Any(m => m.FaceUp && m.In("Valkyrie"));
            s.On = async (x, e) =>
            {
                if (!e.Is(EvKind.Destroyed, x.Self) || e.Other?.Controller != x.Opp || x.D.LeftFieldLog.LastOrDefault(l => l.c == x.Self) is not { c: not null } l || l.controller != x.Me) return;
                var v = x.My.Hand.Concat(x.My.Deck).Where(c => c.Data.IsMonster && c.In("Valkyrie") && c.Data.Level >= 5 && x.D.CanSpecialSummon(c, x.Me, by: x.Self)).ToList();
                if (v.Count > 0 && await x.Ask("Special Summon a Level 5+ Valkyrie from your hand or Deck?")) { await SummonPick(x, v, "Special Summon which Valkyrie?"); x.D.Shuffle(x.Me); }
            };
        }
        {
            var s = S("Lord of the Red");
            static bool Act(Ctx x) => Up(x) && x.Ev is { Kind: EvKind.Activated, Link: { } l } && l.Card.Name != "Lord of the Red";
            s.AddFx(f =>
            {
                f.Label = "Destroy a monster"; f.Speed = 2; f.AnyTurn = true;
                f.Can = x => Act(x) && Hopt(x, "m") && x.Targetable(x.D.AllMonsters.Where(m => m != x.Self)).Any();
                f.Cost = async x => { UseHopt(x, "m"); await x.Target(x.D.AllMonsters.Where(m => m != x.Self), "Destroy which monster?"); };
                f.Run = async x => { if (x.T0 is { Loc: Loc.Monster } t) await x.D.Destroy(t, x.Self); };
            });
            s.AddFx(f =>
            {
                f.Label = "Destroy a Spell/Trap"; f.Speed = 2; f.AnyTurn = true;
                f.Can = x => Act(x) && Hopt(x, "s") && x.Targetable(x.AllSpells()).Any();
                f.Cost = async x => { UseHopt(x, "s"); await x.Target(x.AllSpells(), "Destroy which Spell/Trap?"); };
                f.Run = async x => { if (x.T0 is { OnField: true } t) await x.D.Destroy(t, x.Self); };
            });
        }
        OnBattleDeath("Lord Poison", async x =>
        {
            var l = x.Targetable(x.My.Grave.Where(c => c.Data.IsMonster && c.Data.Race == "Plant" && c.Name != "Lord Poison" && x.D.CanSpecialSummon(c, x.Me, by: x.Self))).ToList();
            if (l.Count > 0) await SummonPick(x, l, "Special Summon which Plant?");
        });
        Spell("Lucky Cloud", async x =>
        {
            var d = x.D; int me = x.Me; var self = x.Self;
            d.Later(d.Turn, Phase.End, async () =>
            {
                var names = d.SummonLog.Where(l => l.turn == d.Turn && l.player == me && l.name.Contains("Cloudian", StringComparison.Ordinal)).GroupBy(l => l.name);
                if (names.Any(g => g.Count() >= 2)) await d.Draw(me, 2);
            });
        });
        S("Lucky Cloud").Note = "It counts every Cloudian Summoned this turn, even ones no longer on the field.";
        Spell("Lullaby of Obedience", async x =>
        {
            var n = await DeclareName(x, "Declare 1 Monster Card name", d => d.IsMonster);
            var l = x.Their.Deck.Where(c => c.Name == n.Name).ToList();
            if (l.Count == 0) return;
            var c = l[0];
            x.D.Reveal(x.Me, c);
            int pick = await x.D.ChooseText(x.Opp, $"{c.Name}: your opponent gets it how?", new[] { "Added to their hand", "Special Summoned to their field" }, x.Self);
            if (pick == 1 && x.D.CanSpecialSummon(c, x.Me, by: x.Self, ignore: true)) await x.D.SpecialSummon(c, x.Me, false, by: x.Self, ignore: true);
            else { x.Their.Deck.Remove(c); c.Controller = x.Me; x.My.Hand.Add(c); c.Loc = Loc.Hand; x.D.Seq++; }
            x.D.Shuffle(x.Opp);
        }, x => x.My.Lp > 2000, async x => x.D.Pay(x.Me, 2000, x.Self));
        S("Lullaby of Obedience").Note = "The monster stays your opponent's card: it goes to their GY when it leaves your hand or field.";
        {
            var s = S("Lycanthrope");
            s.On = async (x, e) => { if (e.Kind == EvKind.BattleDamage && e.Card == x.Self && e.Player == x.Opp) await x.D.Damage(x.Opp, 200 * x.My.Grave.Count(c => c.Data.IsNormalMonster), x.Self); };
        }
        {
            var s = Spell("Machine Angel Ritual", async x => await RitualSummon(x, m => m.In("Cyber Angel"), exact: false),
                x => RitualTargets(x, m => m.In("Cyber Angel"), exact: false).Any());
            s.GraveSave = async (x, m, battle) =>
            {
                if (m.Controller != x.Me || !m.FaceUp || m.Loc != Loc.Monster || !x.IsAttr(m, "LIGHT")) return false;
                if (!await x.Ask($"Banish Machine Angel Ritual from your GY instead of {m.Name} being destroyed?")) return false;
                await x.D.Banish(x.Self, x.Self);
                return true;
            };
        }
        Spell("Machine Duplication", async x =>
        {
            if (x.T0 is not { Loc: Loc.Monster, FaceUp: true } t) return;
            var l = x.My.Deck.Where(c => c.Name == Scripts.NameOf(t) && x.D.CanSpecialSummon(c, x.Me, by: x.Self)).ToList();
            int max = Math.Min(2, Math.Min(l.Count, x.D.FreeMonsterZones(x.Me)));
            if (max == 0) return;
            foreach (var c in await x.PickN(l, 1, max, $"Special Summon up to {max}")) await x.D.SpecialSummon(c, x.Me, await x.D.ChoosePosition(x.Me, c), by: x.Self);
            x.D.Shuffle(x.Me);
        }, x => x.Targetable(x.MyMonsters().FaceUp().Where(m => x.IsRace(m, "Machine") && x.Atk(m) <= 500)).Any(),
        async x => await x.Target(x.MyMonsters().FaceUp().Where(m => x.IsRace(m, "Machine") && x.Atk(m) <= 500), "Target which Machine?"));
        {
            var s = Spell("Macro Cosmos", async x =>
            {
                x.Self.Vars["stay"] = 1;
                var l = x.My.Hand.Concat(x.My.Deck).Where(c => c.Name == "Helios - The Primordial Sun" && x.D.CanSpecialSummon(c, x.Me, by: x.Self)).ToList();
                if (l.Count > 0 && await x.Ask("Special Summon Helios - The Primordial Sun?")) { await SummonPick(x, l, "Special Summon Helios"); x.D.Shuffle(x.Me); }
            });
            s.Allow = (x, q, c) => q == Q.BanishAny && x.Self.OnField && x.Self.FaceUp;
        }
        OnBattleDeath("Mad Reloader", async x =>
        {
            if (x.My.Hand.Count < 2) return;
            foreach (var c in await x.PickN(x.My.Hand, 2, 2, "Send 2 cards from your hand to the GY")) await x.D.ToGrave(c, x.Self);
            await x.D.Draw(x.Me, 2);
        });
        {
            static bool Ok(Card m) => Scripts.NameOf(m) is "Dark Magician" or "Dark Magician Girl";
            var s = Spell("Magic Formula", async x => { if (x.T0 is { } t && t.Valid() && t.FaceUp) await x.D.Equip(x.Self, t); },
                x => x.Targetable(x.D.FaceUpMonsters.Where(Ok)).Any(), async x => await x.Target(x.D.FaceUpMonsters.Where(Ok), "Equip to which monster?"));
            s.EquipOk = (x, m) => Ok(m);
            s.Stats = (x, c, st) => { if (c == x.Self.EquippedTo) st.Add(700, 0); };
            s.On = async (x, e) => { if (e.Kind == EvKind.ToGrave && e.Card == x.Self && e.From is Loc.Spell) await x.D.Gain(x.Me, 1000, x.Self); };
        }
        Spell("Magic Reflector", async x => { if (x.T0 is { OnField: true, FaceUp: true } t) AddCounter(x, t, 1, "guard"); },
            x => x.Targetable(x.My.SpellsOnField.Where(c => c.FaceUp && c.Data.IsSpell && c != x.Self)).Any(),
            async x => await x.Target(x.My.SpellsOnField.Where(c => c.FaceUp && c.Data.IsSpell && c != x.Self), "Place a counter on which Spell?"));
        {
            var s = Spell("Magical Blast", async x => await x.D.Damage(x.Opp, 200 * x.MyMonsters().Count(m => m.FaceUp && x.IsRace(m, "Spellcaster")), x.Self),
                x => x.MyMonsters().Any(m => m.FaceUp && x.IsRace(m, "Spellcaster")));
            s.On = async (x, e) =>
            {
                if (e.Kind == EvKind.DrawPhase && e.Player == x.Me && x.Self.Loc == Loc.Grave && !x.D.TurnFlags.Contains($"skipdraw{x.Me}") && await x.Ask("Skip your draw to add Magical Blast to your hand?"))
                { x.D.TurnFlags.Add($"skipdraw{x.Me}"); await x.D.ToHand(x.Self, x.Self); }
            };
        }
        Spell("Magical Dimension", async x =>
        {
            if (x.T0 is not { Loc: Loc.Monster } t) return;
            await x.D.Tribute(t, x.Self);
            if (!await SummonPick(x, x.My.Hand.Where(c => c.Data.IsMonster && c.Data.Race == "Spellcaster"), "Special Summon which Spellcaster?")) return;
            if (x.D.AllMonsters.Any() && await x.Ask("Destroy 1 monster on the field?")) { var k = await x.Pick(x.D.AllMonsters, "Destroy which monster?"); if (k != null) await x.D.Destroy(k, x.Self); }
        }, x => x.MyMonsters().Any(m => m.FaceUp && x.IsRace(m, "Spellcaster")) && x.Targetable(x.D.Tributable(x.Me)).Any() && x.My.Hand.Any(c => c.Data.IsMonster && c.Data.Race == "Spellcaster"),
        async x => await x.Target(x.D.Tributable(x.Me), "Tribute which monster?"));
        Spell("Magical Mallet", async x =>
        {
            var l = await x.PickN(x.My.Hand.Where(c => c != x.Self), 0, x.My.Hand.Count(c => c != x.Self), "Shuffle which cards into your Deck?");
            foreach (var c in l) await x.D.ToDeck(c);
            x.D.Shuffle(x.Me);
            if (l.Count > 0) await x.D.Draw(x.Me, l.Count);
        });
        Flip("Magical Merchant", async x =>
        {
            var dig = new List<Card>();
            Card? hit = null;
            foreach (var c in x.My.Deck.ToList()) { dig.Add(c); if (!c.Data.IsMonster) { hit = c; break; } }
            x.D.Reveal(x.Me, dig); x.D.Reveal(x.Opp, dig);
            if (hit != null) await x.D.ToHand(hit, x.Self);
            foreach (var c in dig.Where(c => c != hit)) await x.D.ToGrave(c, x.Self);
        });
        {
            var s = S("Magical Scientist");
            s.Can = x => Up(x) && x.My.Lp > 1000 && x.My.Extra.Any(c => c.Data.IsFusion && c.Data.Level <= 6 && x.D.CanSpecialSummon(c, x.Me, by: x.Self, ignore: true));
            s.Cost = async x => x.D.Pay(x.Me, 1000, x.Self);
            s.Run = async x =>
            {
                var c = await x.Pick(x.My.Extra.Where(c => c.Data.IsFusion && c.Data.Level <= 6 && x.D.CanSpecialSummon(c, x.Me, by: x.Self, ignore: true)), "Special Summon which Fusion Monster?");
                if (c == null || !await x.D.SpecialSummon(c, x.Me, await x.D.ChoosePosition(x.Me, c), by: x.Self, ignore: true)) return;
                c.Vars["nodirect"] = x.D.Turn;   // it leaves at the end of this turn
                var d = x.D; d.Later(d.Turn, Phase.End, async () => { if (c.Loc == Loc.Monster) await d.MoveTo(c, Loc.Extra); });
            };
        }
        Spell("Magical Stone Excavation", async x => { if (x.T0 is { Loc: Loc.Grave } t) await x.D.ToHand(t, x.Self); },
            x => x.My.Hand.Count(c => c != x.Self) >= 2 && x.Targetable(x.My.Grave.Where(c => c.Data.IsSpell)).Any(),
            async x => { await x.D.Discard(x.Me, await x.PickN(x.My.Hand.Where(c => c != x.Self), 2, 2, "Discard 2 cards"), x.Self); await x.Target(x.My.Grave.Where(c => c.Data.IsSpell), "Add which Spell?"); });
        {
            var s = Spell("Magical Trick Mirror", async x => { if (x.T0 is { Loc: Loc.Grave } t) await SetFromGrave(x, t, false); },
                x => AttackOnMe(x) && x.My.FreeSpellZones > 0 && x.Targetable(x.Their.Grave.Where(c => c.Data.IsSpell)).Any(),
                async x => await x.Target(x.Their.Grave.Where(c => c.Data.IsSpell), "Set which Spell from your opponent's GY?"));
            s.GraveFx = new Script
            {
                Speed = 2, AnyTurn = true,
                Can = x => x.My.Hand.Concat(x.My.SpellsOnField.Where(c => !c.FaceUp)).Any(c => c.Name == "Monster Reborn") && x.My.Grave.Any(c => c.Name == "Obelisk the Tormentor" && x.D.CanSpecialSummon(c, x.Me, by: x.Self, ignore: true)),
                Cost = async x =>
                {
                    await x.D.Banish(x.Self, x.Self);
                    var r = await x.Pick(x.My.Hand.Concat(x.My.SpellsOnField.Where(c => !c.FaceUp)).Where(c => c.Name == "Monster Reborn"), "Send which Monster Reborn to the GY?");
                    if (r != null) await x.D.ToGrave(r, x.Self);
                },
                Run = async x =>
                {
                    var o = x.My.Grave.FirstOrDefault(c => c.Name == "Obelisk the Tormentor");
                    if (o == null || !await x.D.SpecialSummon(o, x.Me, true, by: x.Self, ignore: true)) return;
                    if (x.D.TurnPlayer == x.Opp) foreach (var m in x.OppMonsters().Where(m => m.FaceUp && m.Defense).ToList()) await x.D.ChangePosition(m, false);
                    if (x.D.TurnPlayer == x.Opp && x.OppMonsters().Any(m => m.Attack)) { foreach (var m in x.OppMonsters().Where(m => m.Attack)) m.Vars["mustattack"] = x.D.Turn; x.D.Flags[$"mustattackobelisk{x.Opp}"] = o.Uid + 1; }
                },
            };
            s.Note = "During your opponent's turn its GY effect switches their monsters to Attack Position and they must attack Obelisk this turn.";
        }
        Spell("Magician's Circle", async x =>
        {
            foreach (int p in new[] { x.Me, x.Opp })
            {
                var l = x.D.P(p).Deck.Where(c => c.Data.IsMonster && c.Data.Race == "Spellcaster" && c.Data.Atk is >= 0 and <= 2000 && x.D.CanSpecialSummon(c, p, by: x.Self)).ToList();
                if (l.Count == 0) continue;
                var c = await x.D.PickOne(p, l, "Special Summon 1 Spellcaster with 2000 or less ATK", x.Self);
                if (c != null) await x.D.SpecialSummon(c, p, false, by: x.Self);
                x.D.Shuffle(p);
            }
        }, x => x.Ev is { Kind: EvKind.AttackDeclared, Card: { } a } && x.IsRace(a, "Spellcaster") && x.My.Deck.Any(c => c.Data.IsMonster && c.Data.Race == "Spellcaster"));
        Sure("Lava Golem", "Legendary Fiend", "Lekunga", "Levia-Dragon - Daedalus", "Little-Winguard", "Lord Poison", "Lycanthrope", "Machine Duplication", "Mad Reloader", "Magic Formula",
            "Magical Mallet", "Magical Stone Excavation", "Magician's Circle", "Magical Merchant");
    }
}
