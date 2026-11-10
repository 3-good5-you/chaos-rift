namespace PoC.Engine;

// Official cards, batch 18 (Mirror Force Dragon .. No Entry!!).
public static partial class Scripts
{
    /// Aqua Dolphin / Marine Dolphin: discard 1; look at the opponent's hand and pick a monster; ATK check.
    static void DolphinScan(Script s, bool penalty)
    {
        s.OncePerTurn = true;
        s.Can = x => Up(x) && x.My.Hand.Count > 0;
        s.Cost = async x => await x.D.Discard(x.Me, await x.PickN(x.My.Hand, 1, 1, "Discard 1 card"), x.Self);
        s.Run = async x =>
        {
            x.D.Reveal(x.Me, x.Their.Hand);
            var t = await x.Pick(x.Their.Hand.Where(c => c.Data.IsMonster), "Choose 1 monster in your opponent's hand");
            if (t == null) return;
            if (x.MyMonsters().Any(m => m.FaceUp && x.Atk(m) >= t.Data.Atk)) { await DestroyInHand(x, t); await x.D.Damage(x.Opp, 500, x.Self); }
            else if (penalty) await x.D.Damage(x.Me, 500, x.Self);
        };
    }

    /// Glow Moss / Twinkle Moss: a player draws 1 and shows it; Monster ends the Battle Phase, Spell may turn the attack direct, Trap sets it to Defense.
    static void MossDraw(Script s, bool oppDraws)
    {
        s.On = async (x, e) =>
        {
            if (e.Kind != EvKind.AttackDeclared || (e.Card != x.Self && e.Other != x.Self) || !Up(x)) return;
            int p = oppDraws ? x.Opp : x.Me;
            if (x.D.P(p).Deck.Count == 0) return;
            await x.D.Draw(p, 1);
            var c = x.D.P(p).Hand.Last();
            x.D.Reveal(x.Me, c); x.D.Reveal(x.Opp, c);
            if (c.Data.IsMonster) x.D.TurnFlags.Add("endbattle");
            else if (c.Data.IsSpell && e.Card == x.Self && x.D.AttackTarget != null && await x.Ask("Change it to a direct attack?")) x.D.AttackTarget = null;
            else if (c.Data.IsTrap && x.Self.Attack) await x.D.ChangePosition(x.Self, true);
        };
    }

    /// Mystic Swordsman LV2 / Ninja Grandmaster Sasuke: "At the start of the Damage Step, if this card attacks a [face-down / face-up] Defense Position monster: Destroy that monster."
    static void StartDamageKill(Script s, bool faceDown)
    {
        var on = s.On;
        s.On = async (x, e) =>
        {
            if (on != null) await on(x, e);
            if (e.Kind == EvKind.DamageStart && e.Card == x.Self && e.Other is { Defense: true } t && t.FaceUp != faceDown && Up(x)) await x.D.Destroy(t, x.Self);
        };
    }

    static void CardsP18(CardDb db)
    {
        {
            var s = S("Mirror Force Dragon");
            s.SummonOk = (x, by, k) => by?.Name == "The Fang of Critias";
            s.On = async (x, e) =>
            {
                if (!Up(x) || !x.Their.FieldCards.Any()) return;
                bool hit = (e.Kind == EvKind.AttackDeclared && e.Player == x.Opp && e.Other?.Controller == x.Me)
                    || (e.Kind == EvKind.Activated && e.Link is { } l && l.Player == x.Opp && l.X.T.Any(t => t.Loc == Loc.Monster && t.Controller == x.Me));
                if (hit && await x.Ask("Destroy all cards your opponent controls?")) await x.D.Destroy(x.Their.FieldCards.ToList(), x.Self);
            };
        }
        Spell("Mirror Gate", async x =>
        {
            if (x.D.Attacker is not { Loc: Loc.Monster } a || x.D.AttackTarget is not { Loc: Loc.Monster } t || t.Controller != x.Me || x.D.AttackNegated) return;
            int me = x.Me, opp = x.Opp;
            var l = new List<Card>();
            if (x.D.FreeMonsterZones(me) == 0 && x.D.FreeMonsterZones(opp) == 0)
            {   // a straight swap: each takes the other's zone
                x.D.P(opp).Monsters[a.Zone] = null; x.D.P(me).Monsters[t.Zone] = null;
                (a.Zone, t.Zone) = (t.Zone, a.Zone);
                x.D.P(me).Monsters[a.Zone] = a; x.D.P(opp).Monsters[t.Zone] = t;
                a.Controller = me; t.Controller = opp; x.D.Seq++;
            }
            else { await x.D.TakeControl(a, me); await x.D.TakeControl(t, opp); }
            x.D.BattleFlags.Add("swapped");
            var d = x.D;
            d.Later(d.Turn, Phase.End, async () =>
            {
                if (a.Loc == Loc.Monster && a.Controller == me) await d.TakeControl(a, opp);
                if (t.Loc == Loc.Monster && t.Controller == opp) await d.TakeControl(t, me);
            });
        }, x => x.Ev is { Kind: EvKind.AttackDeclared, Other: { FaceUp: true } t } e && e.Player == x.Opp && t.Controller == x.Me && t.In("Elemental HERO"));
        {
            var s = Spell("Mischief of the Time Goddess", async x =>
            {
                var d = x.D; int me = x.Me; var self = x.Self;
                bool done = false;
                d.Watch(d.Turn, async ev =>
                {
                    if (done || ev.Kind != EvKind.BattleEnd || ev.Player != me) return;
                    done = true;
                    var mons = d.P(me).MonstersOnField.ToList();
                    if (mons.Count == 0 || !mons.All(m => m.FaceUp && m.In("Valkyrie"))) return;
                    if (self.Loc == Loc.Grave || self.OnField) { if (self.OnField) await d.ToGrave(self, self); }
                    d.Flags["timeskip"] = me + 1;
                    d.Flags[$"mischief{me}"] = d.Turn + 2;
                    d.TurnFlags.Add("toend");
                });
            }, x => x.D.Chain.Count == 0 && x.D.Flags.GetValueOrDefault($"mischief{x.Me}") < x.D.Turn);
            s.NoResponse = true;
            s.Note = "Activate it during your turn; at the end of your Battle Phase (all your monsters are Valkyries) your opponent's turn is skipped and your next turn starts at the Battle Phase.";
        }
        Spell("Misfortune", async x =>
        {
            if (x.T0 is { } t) await x.D.Damage(x.Opp, Math.Max(0, t.Data.Atk) / 2, x.Self);
            var d = x.D; int me = x.Me;
            d.Linger(d.Turn, (q, c, p) => q == Q.Attack && c != null && c.Controller == me);
        }, x => x.Targetable(x.OppMonsters().FaceUp()).Any(), async x => await x.Target(x.OppMonsters().FaceUp(), "Select which monster?"));
        Spell("Mispolymerization", async x => { foreach (var m in x.D.FaceUpMonsters.Where(m => m.Data.IsFusion).ToList()) await x.D.MoveTo(m, Loc.Extra, by: x.Self); },
            x => x.Ev is { Kind: EvKind.Summoned, Card: { } c } && c.Data.IsFusion && c.Loc == Loc.Monster);
        {
            var s = Spell("Mist Body", async x => { if (x.T0 is { } t && t.Valid() && t.FaceUp) await x.D.Equip(x.Self, t); },
                x => x.Targetable(x.D.FaceUpMonsters).Any(), async x => await x.Target(x.D.FaceUpMonsters, "Equip to which monster?"));
            s.Forbid = (x, q, c) => q == Q.DestroyByBattle && c != null && c == x.Self.EquippedTo;
        }
        FlipDown(S("Moai Interceptor Cannons"));
        S("Mobius the Frost Monarch").On = async (x, e) =>
        {
            if (!e.Is(EvKind.Summoned, x.Self) || e.Arg != (int)SummonKind.Tribute || !x.Targetable(x.AllSpells()).Any() || !await x.Ask("Destroy up to 2 Spell/Trap Cards?")) return;
            await x.D.Destroy(await x.PickN(x.Targetable(x.AllSpells()), 1, Math.Min(2, x.Targetable(x.AllSpells()).Count()), "Destroy which Spell/Traps?"), x.Self);
        };
        S("Mokey Mokey King").On = async (x, e) =>
        {
            if (e.Kind != EvKind.LeftField || e.Card != x.Self || !x.My.Grave.Any(c => c.Name == "Mokey Mokey") || !await x.Ask("Special Summon as many Mokey Mokey as possible from your GY?")) return;
            foreach (var c in x.My.Grave.Where(c => c.Name == "Mokey Mokey").ToList())
                if (x.D.CanSpecialSummon(c, x.Me, by: x.Self)) await x.D.SpecialSummon(c, x.Me, await x.D.ChoosePosition(x.Me, c), by: x.Self);
        };
        {
            var s = Spell("Mokey Mokey Smackdown", async x => x.Self.Vars["stay"] = 1);
            s.On = async (x, e) =>
            {
                if (!x.Self.OnField || !x.Self.FaceUp || e.Kind != EvKind.Destroyed || !e.Card!.Data.IsMonster || e.From != Loc.Monster) return;
                if (x.D.LeftFieldLog.LastOrDefault(l => l.c == e.Card) is not { c: not null } l || l.controller != x.Me || (e.Card.AsMonster ?? e.Card.Data).Race != "Fairy") return;
                foreach (var m in x.MyMonsters().Where(m => m.FaceUp && m.Name == "Mokey Mokey").ToList()) x.D.Boost(m, 3000 - x.Atk(m));
            };
            s.Note = "It needs a face-up Mokey Mokey on your field after the Fairy is destroyed.";
        }
        S("Molten Zombie").On = async (x, e) => { if (e.Is(EvKind.Summoned, x.Self) && e.From == Loc.Grave) await x.D.Draw(x.Me, 1); };
        Spell("Monster Gate", async x =>
        {
            var dig = new List<Card>();
            Card? hit = null;
            foreach (var c in x.My.Deck.ToList())
            {
                dig.Add(c);
                if (c.Data.IsMonster && !c.Data.IsFusion && x.D.S(c) is not { SummonOk: not null } && x.D.S(c) is not { MustFusion: true } && !c.Data.IsRitual) { hit = c; break; }
            }
            x.D.Reveal(x.Me, dig); x.D.Reveal(x.Opp, dig);
            if (hit != null && x.D.CanSpecialSummon(hit, x.Me, by: x.Self)) await x.D.SpecialSummon(hit, x.Me, await x.D.ChoosePosition(x.Me, hit), by: x.Self);
            foreach (var c in dig.Where(c => c != hit && c.Loc == Loc.Deck)) await x.D.ToGrave(c, x.Self);
        }, x => x.D.Tributable(x.Me).Any(), async x => { var t = await x.Pick(x.D.Tributable(x.Me), "Tribute 1 monster"); if (t != null) await x.D.Tribute(t, x.Self); });
        Spell("Monster Reincarnation", async x => { if (x.T0 is { Loc: Loc.Grave } t) await x.D.ToHand(t, x.Self); },
            x => x.My.Hand.Any(c => c != x.Self) && x.Targetable(x.My.Grave.Where(c => c.Data.IsMonster)).Any(),
            async x => { await x.D.Discard(x.Me, await x.PickN(x.My.Hand.Where(c => c != x.Self), 1, 1, "Discard 1 card"), x.Self); await x.Target(x.My.Grave.Where(c => c.Data.IsMonster), "Add which monster?"); });
        {
            var s = Spell("Morale Boost", async x => x.Self.Vars["stay"] = 1);
            s.On = async (x, e) =>
            {
                if (!x.Self.OnField || !x.Self.FaceUp) return;
                if (e.Kind == EvKind.Equipped && e.Card is { } eq && eq.Data.IsSpell && eq.Data.Is("Equip")) await x.D.Gain(eq.Controller, 1000, x.Self);
                if (e.Kind == EvKind.LeftField && e.Card is { } c && c != x.Self && c.Data.IsSpell && c.Data.Is("Equip") && e.From == Loc.Spell
                    && x.D.LeftFieldLog.LastOrDefault(l => l.c == c) is { c: not null } l && l.faceUp) await x.D.Damage(l.controller, 1000, x.Self);
            };
        }
        {
            var s = S("Mound of the Bound Creator");
            static bool Big(Ctx x, Card? c) => c != null && c.Loc == Loc.Monster && c.FaceUp && x.D.Level(c) >= 10;
            s.Forbid = (x, q, c) => x.Self.FaceUp && x.Self.Loc == Loc.Field && q is Q.Target or Q.DestroyByEffect && Big(x, c);
            s.On = async (x, e) =>
            {
                if (x.Self.FaceUp && x.Self.Loc == Loc.Field && e.Kind == EvKind.BattleDestroyedMonster && x.D.Level(e.Card!) >= 10 && e.Other!.Loc == Loc.Grave
                    && x.D.LeftFieldLog.LastOrDefault(l => l.c == e.Other) is { c: not null } l)
                    await x.D.Damage(l.controller, 1000, x.Self);
                if (e.Is(EvKind.Destroyed, x.Self) && !e.Battle && x.Self.Loc == Loc.Grave && x.My.Deck.Any(c => c.Data.IsMonster && c.Data.Attribute == "DIVINE") && await x.Ask("Add a DIVINE monster from your Deck?"))
                    await x.Search(c => c.Data.IsMonster && c.Data.Attribute == "DIVINE", "Add which DIVINE monster?");
            };
        }
        SelfBoost("Mudora", x => 200 * x.My.Grave.Count(c => c.Data.IsMonster && c.Data.Race == "Fairy"));
        Spell("Muko", async x =>
        {
            var link = x.Ev!.Link!; var d = x.D; var self = x.Self;
            d.Watch(d.Turn, async ev =>
            {
                if (ev.Kind != EvKind.Drew || ev.Arg == 1 || d.Resolving != link || ev.Amount <= 0) return;
                var hand = d.P(ev.Player).Hand;
                var drawn = hand.Skip(Math.Max(0, hand.Count - ev.Amount)).ToList();
                d.Reveal(1 - ev.Player, drawn);
                await d.Discard(ev.Player, drawn, self);
            });
        }, x => x.Ev is { Kind: EvKind.Activated, Link: { } l } && l.Card != x.Self && System.Text.RegularExpressions.Regex.IsMatch(l.Card.Data.Desc, @"\bdraws? \d|\bdraws? (a|1|2|3|cards?)\b", System.Text.RegularExpressions.RegexOptions.IgnoreCase));
        S("Muko").Note = "It answers any card whose text draws cards; the cards drawn by that card are discarded.";
        Spell("Multiple Destruction", async x =>
        {
            int n = 0;
            foreach (var p in x.D.Players)
                foreach (var c in p.Hand.ToList()) { await x.D.ToDeck(c, top: false); n++; }
            x.D.Pay(x.Me, 300 * Math.Max(1, n), x.Self);
            await x.D.Draw(x.Me, 5); await x.D.Draw(x.Opp, 5);
        }, x => Hopt(x) && x.My.Hand.Count(c => c != x.Self) >= 3 && x.Their.Hand.Count >= 3, async x => UseHopt(x));
        Spell("Multiplication of Ants", async x =>
        {
            for (int i = 0; i < 2 && x.D.FreeMonsterZones(x.Me) > 0; i++)
            {
                var t = x.D.MakeToken("Army Ant Token", x.Me, 500, 1200, "EARTH", "Insect", 4);
                if (await x.D.SpecialSummon(t, x.Me, await x.D.ChoosePosition(x.Me, t), by: x.Self)) t.Vars["notribute"] = 1;
            }
        }, x => x.D.Tributable(x.Me).Any(m => m.FaceUp ? x.IsRace(m, "Insect") : m.Data.Race == "Insect"),
        async x => { var t = await x.Pick(x.D.Tributable(x.Me).Where(m => m.FaceUp ? x.IsRace(m, "Insect") : m.Data.Race == "Insect"), "Tribute 1 Insect"); if (t != null) await x.D.Tribute(t, x.Self); });
        Spell("Mustering of the Dark Scorpions", async x =>
        {
            var used = new HashSet<string>();
            while (x.D.FreeMonsterZones(x.Me) > 0)
            {
                var l = x.My.Hand.Where(c => c.Data.IsMonster && c.In("Dark Scorpion") && !used.Contains(c.Name) && x.D.CanSpecialSummon(c, x.Me, by: x.Self)).ToList();
                var c = await x.Pick(l, "Special Summon a Dark Scorpion? (cancel to stop)", optional: true);
                if (c == null) break;
                used.Add(c.Name);
                await x.D.SpecialSummon(c, x.Me, await x.D.ChoosePosition(x.Me, c), by: x.Self);
            }
        }, x => x.MyMonsters().Any(m => m.FaceUp && m.Name == "Don Zaloog") && x.My.Hand.Any(c => c.Data.IsMonster && c.In("Dark Scorpion")));
        Spell("My Body as a Shield", async x => await x.D.Negate(x.Ev!.Link!),
            x => x.My.Lp > 1500 && x.Ev is { Kind: EvKind.Activated, Link: { } l } && l.Player == x.Opp && System.Text.RegularExpressions.Regex.IsMatch(l.Card.Data.Desc, @"destroy[^.]*monster", System.Text.RegularExpressions.RegexOptions.IgnoreCase),
            async x => x.D.Pay(x.Me, 1500, x.Self));
        S("My Body as a Shield").Note = "It answers any opponent's card whose text destroys a monster.";
        {
            var s = S("Mystic Swordsman LV2");
            LevelUp(s, "Mystic Swordsman LV4");
            StartDamageKill(s, faceDown: true);
        }
        Spell("Mystik Wok", async x =>
        {
            if (x.T.FirstOrDefault() is not { } t) return;
            int atk = x.Self.Var("wokatk"), def = x.Self.Var("wokdef");
            int i = await x.D.ChooseText(x.Me, "Gain LP equal to its...", new[] { $"ATK ({atk})", $"DEF ({def})" }, x.Self);
            await x.D.Gain(x.Me, i == 0 ? atk : def, x.Self);
        }, x => x.D.Tributable(x.Me).Any(),
        async x =>
        {
            var t = await x.Pick(x.D.Tributable(x.Me), "Tribute 1 monster");
            if (t == null) return;
            x.Self.Vars["wokatk"] = x.Atk(t); x.Self.Vars["wokdef"] = x.Def(t);
            x.T.Add(t);
            await x.D.Tribute(t, x.Self);
        });
        {
            var s = Spell("Natural Disaster", async x => x.Self.Vars["stay"] = 1);
            s.On = async (x, e) =>
            {
                if (!x.Self.OnField || !x.Self.FaceUp || e.Kind != EvKind.Destroyed || e.Battle || e.Card!.Loc != Loc.Grave || e.Other is not { } by || !by.In("Cloudian") || !by.Data.IsMonster) return;
                if (x.D.LeftFieldLog.LastOrDefault(l => l.c == e.Card) is { c: not null } l && l.controller == x.Opp) await x.D.Damage(x.Opp, 500, x.Self);
            };
        }
        {
            var s = Spell("Necklace of Command", async x => { if (x.T0 is { } t && t.Valid() && t.FaceUp) await x.D.Equip(x.Self, t); },
                x => x.Targetable(x.MyMonsters().FaceUp()).Any(), async x => await x.Target(x.MyMonsters().FaceUp(), "Equip to which monster?"));
            s.On = async (x, e) =>
            {
                if (e.Kind == EvKind.Destroyed && e.Battle && e.Card == x.Self.EquippedTo && x.Self.OnField) x.D.Flags[$"necklace{x.Self.Uid}"] = x.D.Turn;
                if (e.Kind == EvKind.ToGrave && e.Card == x.Self && x.D.Flags.GetValueOrDefault($"necklace{x.Self.Uid}") == x.D.Turn)
                {
                    x.D.Flags.Remove($"necklace{x.Self.Uid}");
                    int i = await x.D.ChooseText(x.Me, "Necklace of Command", new[] { "Draw 1 card", "Discard 1 random card from your opponent's hand", "Nothing" }, x.Self);
                    if (i == 0) await x.D.Draw(x.Me, 1);
                    if (i == 1 && x.Their.Hand.Count > 0) await x.D.DiscardRandom(x.Opp, 1, x.Self);
                }
            };
        }
        {
            var s = S("Necro Gardna");
            s.AddFx(f =>
            {
                f.Label = "Negate the next attack"; f.FromGrave = true; f.Speed = 2; f.AnyTurn = true;
                f.Can = x => x.D.TurnPlayer == x.Opp && x.D.Phase is not (Phase.Main2 or Phase.End);
                f.Cost = async x => await x.D.Banish(x.Self, x.Self);
                f.Run = async x =>
                {
                    if (x.D.Attacker is { } a && a.Controller == x.Opp && x.D.Phase == Phase.Battle && x.Ev is { Kind: EvKind.AttackDeclared }) { x.D.AttackNegated = true; return; }
                    var d = x.D; int opp = x.Opp; bool done = false;
                    d.Watch(d.Turn, async e => { if (!done && e.Kind == EvKind.AttackDeclared && e.Player == opp) { done = true; d.AttackNegated = true; } });
                };
            });
        }
        {
            var s = S("Necrovalley");
            s.Stats = (x, c, st) => { if (c.Loc == Loc.Monster && c.FaceUp && c.In("Gravekeeper's")) st.Add(500, 500); };
            s.Forbid = (x, q, c) => q == Q.SpecialSummon && x.Self.FaceUp && x.Self.Loc == Loc.Field && c != null && c.Loc == Loc.Grave && !x.D.TurnFlags.Contains("necroexempt");
            s.Note = "Cards in either GY cannot be moved by card effects or banished (effects that would do so leave them where they are).";
        }
        S("Neko Mane King").On = async (x, e) =>
        {
            if (e.Kind == EvKind.ToGrave && e.Card == x.Self && x.D.TurnPlayer == x.Opp && e.Other is { } by && by.Controller == x.Opp && x.Self.Owner == x.Me)
            { x.D.TurnFlags.Add("toend"); x.D.TurnFlags.Add("endbattle"); }
        };
        {
            static bool NeosLike(Card c) => Scripts.NameOf(c) == "Elemental HERO Neos" || (c.Data.IsFusion && Mentions(c.Data, "Elemental HERO Neos"));
            S("Neo Space").Stats = (x, c, st) => { if (c.Loc == Loc.Monster && c.FaceUp && NeosLike(c)) st.Add(500, 0); };
        }
        {
            var s = S("Neo-Spacian Air Hummingbird");
            s.OncePerTurn = true;
            s.Can = x => Up(x) && x.Their.Hand.Count > 0;
            s.Run = async x => await x.D.Gain(x.Me, 500 * x.Their.Hand.Count, x.Self);
        }
        DolphinScan(S("Neo-Spacian Aqua Dolphin"), penalty: true);
        {
            var s = S("Neo-Spacian Dark Panther");
            s.OncePerTurn = true;
            s.Can = x => Up(x) && x.Targetable(x.OppMonsters().FaceUp()).Any();
            s.Cost = async x => await x.Target(x.OppMonsters().FaceUp(), "Copy which monster?");
            s.Run = async x =>
            {
                if (x.T0 is not { Loc: Loc.Monster, FaceUp: true } t || !Up(x)) return;
                var self = x.Self; var d = x.D;
                self.TempName = t.Data.Name;
                self.Vars["copyidx"] = t.Data.Idx + 1; self.Vars["copyturn"] = d.Turn; d.Seq++;
                d.Later(d.Turn, Phase.End, async () => { self.TempName = null; self.Vars["copyidx"] = 0; d.Seq++; });
            };
        }
        SelfBoost("Neo-Spacian Flare Scarab", x => 400 * x.Their.SpellsOnField.Count());
        MossDraw(S("Neo-Spacian Glow Moss"), oppDraws: true);
        S("Neo-Spacian Grand Mole").On = async (x, e) =>
        {
            if (e.Kind != EvKind.DamageStart || !Up(x)) return;
            var o = e.Card == x.Self ? e.Other : e.Other == x.Self ? e.Card : null;
            if (o == null || o.Controller != x.Opp || !await x.Ask("Return both monsters to the hand?")) return;
            await x.D.ToHand(o, x.Self);
            await x.D.ToHand(x.Self, x.Self);
        };
        {
            var s = S("Neo-Spacian Marine Dolphin");
            s.SummonOk = (x, by, k) => by?.Name == "NEX";
            DolphinScan(s, penalty: false);
        }
        {
            var s = S("Neo-Spacian Twinkle Moss");
            s.SummonOk = (x, by, k) => by?.Name == "NEX";
            MossDraw(s, oppDraws: false);
        }
        {
            var s = Spell("Neos Force", async x => { if (x.T0 is { } t && t.Valid() && t.FaceUp) await x.D.Equip(x.Self, t); },
                x => x.Targetable(x.D.FaceUpMonsters.Where(m => Scripts.NameOf(m) == "Elemental HERO Neos")).Any(),
                async x => await x.Target(x.D.FaceUpMonsters.Where(m => Scripts.NameOf(m) == "Elemental HERO Neos"), "Equip to which Neos?"));
            s.EquipOk = (x, m) => Scripts.NameOf(m) == "Elemental HERO Neos";
            s.Stats = (x, c, st) => { if (c == x.Self.EquippedTo) st.Add(800, 0); };
            s.On = async (x, e) =>
            {
                if (!x.Self.OnField || !x.Self.FaceUp) return;
                if (e.Kind == EvKind.BattleDestroyedMonster && e.Card == x.Self.EquippedTo && e.Other!.Loc == Loc.Grave) await x.D.Damage(x.Opp, Math.Max(0, e.Other.Data.Atk), x.Self);
                if (e.Kind == EvKind.EndPhase) { await x.D.ToDeck(x.Self); x.D.Shuffle(x.Self.Owner); }
            };
        }
        {
            var s = S("Neos Wiseman");
            s.SummonOk = (x, by, k) => by == x.Self;
            s.ProcCan = x => x.MyMonsters().Any(m => m.FaceUp && Scripts.NameOf(m) == "Elemental HERO Neos") && x.MyMonsters().Any(m => m.FaceUp && Scripts.NameOf(m) == "Yubel");
            s.Proc = async x =>
            {
                var n = x.MyMonsters().First(m => m.FaceUp && Scripts.NameOf(m) == "Elemental HERO Neos");
                var y = x.MyMonsters().First(m => m.FaceUp && Scripts.NameOf(m) == "Yubel");
                await x.D.ToGrave(n, x.Self); await x.D.ToGrave(y, x.Self);
                await x.D.SpecialSummon(x.Self, x.Me, await x.D.ChoosePosition(x.Me, x.Self), by: x.Self);
            };
            s.Forbid = (x, q, c) => q == Q.DestroyByEffect && c == x.Self;
            s.On = async (x, e) =>
            {
                if (e.Kind != EvKind.AfterDamage || !Up(x)) return;
                var o = e.Card == x.Self ? e.Other : e.Other == x.Self ? e.Card : null;
                if (o == null || (o.Loc == Loc.Monster && o.Controller != x.Opp)) return;
                int atk = o.Loc == Loc.Monster ? x.Atk(o) : Math.Max(0, o.Data.Atk), def = o.Loc == Loc.Monster ? x.Def(o) : Math.Max(0, o.Data.Def);
                await x.D.Damage(x.Opp, atk, x.Self);
                await x.D.Gain(x.Me, def, x.Self);
            };
        }
        OnBattleDeath("Newdoria", async x =>
        {
            var t = await x.Pick(x.Targetable(x.D.AllMonsters), "Destroy which monster?");
            if (t != null) await x.D.Destroy(t, x.Self);
        });
        {
            static bool Same(Card f, string name) => f.Data.Level == 4 && (f.Name == name || Mentions(f.Data, name) && f.Data.Desc.Contains("also treated as"));
            Spell("NEX", async x =>
            {
                if (x.T.FirstOrDefault() is not { } sent) return;
                await SummonPick(x, x.My.Extra.Where(f => Same(f, sent.Name)), "Special Summon which monster?");
            }, x => x.MyMonsters().Any(m => m.FaceUp && m.In("Neo-Spacian") && x.My.Extra.Any(f => Same(f, m.Name) && x.D.CanSpecialSummon(f, x.Me, by: x.Self, ignoreZones: true))),
            async x =>
            {
                var m = await x.Pick(x.MyMonsters().Where(m => m.FaceUp && m.In("Neo-Spacian") && x.My.Extra.Any(f => Same(f, m.Name))), "Send which Neo-Spacian to the GY?");
                if (m == null) return;
                x.T.Add(m);
                await x.D.ToGrave(m, x.Self);
            });
        }
        {
            var s = S("Nightmare Penguin");
            s.Stats = (x, c, st) => { if (Up(x) && c.Controller == x.Me && c.Loc == Loc.Monster && c.FaceUp && x.IsAttr(c, "WATER")) st.Add(200, 0); };
            s.On = async (x, e) =>
            {
                if (e.Is(EvKind.Flipped, x.Self) && x.Targetable(x.Their.FieldCards).Any())
                { var t = await x.Pick(x.Targetable(x.Their.FieldCards), "Return which card to the hand?"); if (t != null) await x.D.ToHand(t, x.Self); }
            };
        }
        {
            var s = Spell("Nightmare Wheel", async x =>
            {
                if (x.T0 is not { Loc: Loc.Monster } t || !x.Self.OnField) return;
                Tie(x, t); x.Self.Vars["stay"] = 1;
            }, x => x.Targetable(x.OppMonsters()).Any(), async x => await x.Target(x.OppMonsters(), "Target which monster?"));
            s.Forbid = (x, q, c) => q is Q.Attack or Q.ChangePos && x.Self.OnField && x.Self.FaceUp && c != null && c == TiedMonster(x);
            s.On = async (x, e) =>
            {
                if (!x.Self.OnField || !x.Self.FaceUp || TiedMonster(x) is not { } t) return;
                if (e.Kind == EvKind.LeftField && e.Card == t) { await x.D.Destroy(x.Self, x.Self); return; }
                if (e.Kind == EvKind.Standby && e.Player == x.Me && t.Loc == Loc.Monster) await x.D.Damage(x.Opp, 500, x.Self);
            };
        }
        StartDamageKill(S("Ninja Grandmaster Sasuke"), faceDown: false);
        Spell("No Entry!!", async x => { foreach (var m in x.D.AllMonsters.Where(m => m.FaceUp && m.Attack).ToList()) await x.D.ChangePosition(m, true); }, x => x.D.AllMonsters.Any(m => m.FaceUp && m.Attack));
        Sure("Misfortune", "Mispolymerization", "Mist Body", "Moai Interceptor Cannons", "Mobius the Frost Monarch", "Mokey Mokey King", "Molten Zombie", "Monster Reincarnation", "Mudora",
            "Multiplication of Ants", "Mustering of the Dark Scorpions", "Neo-Spacian Air Hummingbird", "Neo-Spacian Flare Scarab", "Newdoria", "Nightmare Penguin", "Ninja Grandmaster Sasuke", "No Entry!!", "Mystic Swordsman LV2");
    }
}
