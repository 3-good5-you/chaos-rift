namespace PoC.Engine;

// Official cards, batch 24 (Swift Gaia the Fierce Knight .. Time Magic Hammer).
public static partial class Scripts
{
    static readonly string[] Sarcophagi = { "The First Sarcophagus", "The Second Sarcophagus", "The Third Sarcophagus" };

    static void CardsP24(CardDb db)
    {
        {
            var s = S("Swift Gaia the Fierce Knight");
            s.FreeTribute = (x, c, set) => c == x.Self && x.Self.Loc == Loc.Hand && x.My.Hand.Count == 1;
            s.OnFreeTribute = async x => { };
        }
        Spell("Swing of Memories", async x =>
        {
            if (x.T0 is not { Loc: Loc.Grave } t || !await x.D.SpecialSummon(t, x.Me, await x.D.ChoosePosition(x.Me, t), by: x.Self)) return;
            var d = x.D; d.Later(d.Turn, Phase.End, async () => { if (t.Loc == Loc.Monster) await d.Destroy(t); });
        }, x => x.D.FreeMonsterZones(x.Me) > 0 && x.Targetable(x.My.Grave.Where(c => c.Data.IsNormalMonster && x.D.CanSpecialSummon(c, x.Me, by: x.Self))).Any(),
        async x => await x.Target(x.My.Grave.Where(c => c.Data.IsNormalMonster && x.D.CanSpecialSummon(c, x.Me, by: x.Self)), "Special Summon which Normal Monster?"));
        Spell("Synthesis Spell", async x => await RitualSummon(x, m => m.Name == "Lycanthrope", exact: false), x => RitualTargets(x, m => m.Name == "Lycanthrope", exact: false).Any());
        {
            var s = Spell("Synthetic Seraphim", async x => x.Self.Vars["stay"] = 1);
            s.On = async (x, e) =>
            {
                if (e.Kind == EvKind.Resolved && x.Self.OnField && x.Self.FaceUp && e.Link is { IsCardActivation: true } l && l.Card.Data.IsTrap && l.Card.Data.Is("Counter") && x.D.FreeMonsterZones(x.Me) > 0)
                { var t = x.D.MakeToken("Synthetic Seraphim Token", x.Me, 300, 300, "LIGHT", "Fairy", 1); await x.D.SpecialSummon(t, x.Me, await x.D.ChoosePosition(x.Me, t), by: x.Self); }
            };
        }
        Spell("System Down", async x => { foreach (var c in x.OppMonsters().Where(m => m.FaceUp ? x.IsRace(m, "Machine") : m.Data.Race == "Machine").Concat(x.Their.Grave.Where(c => c.Data.IsMonster && c.Data.Race == "Machine")).ToList()) await x.D.Banish(c, x.Self); },
            x => x.My.Lp > 1000 && (x.OppMonsters().Any(m => m.FaceUp ? x.IsRace(m, "Machine") : m.Data.Race == "Machine") || x.Their.Grave.Any(c => c.Data.IsMonster && c.Data.Race == "Machine")), async x => x.D.Pay(x.Me, 1000, x.Self));
        S("T.A.D.P.O.L.E.").On = async (x, e) =>
        {
            if (!e.Is(EvKind.Destroyed, x.Self) || !e.Battle || x.Self.Loc != Loc.Grave || x.Self.Owner != x.Me || !x.My.Deck.Any(c => c.Name == "T.A.D.P.O.L.E.") || !await x.Ask("Add T.A.D.P.O.L.E.s from your Deck?")) return;
            var l = x.My.Deck.Where(c => c.Name == "T.A.D.P.O.L.E.").ToList();
            foreach (var c in await x.PickN(l, 1, l.Count, "Add how many?")) await x.D.ToHand(c, x.Self);
            x.D.Shuffle(x.Me);
        };
        Spell("Tail Swipe", async x =>
        {
            if (x.T0 is not { Loc: Loc.Monster, FaceUp: true } t) return;
            int lv = x.D.Level(t);
            var l = x.OppMonsters().Where(m => !m.FaceUp || x.D.Level(m) < lv).ToList();
            if (l.Count > 0) foreach (var m in await x.PickN(l, 0, Math.Min(2, l.Count), "Return up to 2 monsters to the hand")) await x.D.ToHand(m, x.Self);
        }, x => x.Targetable(x.MyMonsters().FaceUp().Where(m => x.IsRace(m, "Dinosaur") && x.D.Level(m) >= 5)).Any(),
        async x => await x.Target(x.MyMonsters().FaceUp().Where(m => x.IsRace(m, "Dinosaur") && x.D.Level(m) >= 5), "Target which Dinosaur?"));
        Spell("Taunt", async x =>
        {
            var t = await x.Pick(x.MyMonsters(), "Select which monster?");
            if (t == null) return;
            x.D.Flags[$"staunch{x.Opp}"] = t.Uid + 1; x.D.Flags[$"staunchturn{x.Opp}"] = x.D.Turn;
        }, x => x.D.TurnPlayer == x.Opp && x.D.Phase == Phase.Main1 && x.MyMonsters().Any());
        S("Taunt").Note = "The selected monster must be face-up to be forced as the target.";
        {
            var s = Spell("Temple of the Kings", async x => x.Self.Vars["stay"] = 1);
            s.AddFx(f =>
            {
                f.Label = "Send with Serket: Special Summon"; f.InSpellZone = true;
                f.Can = x => x.Self.FaceUp && Hopt(x, "ss") && x.MyMonsters().Any(m => m.FaceUp && Scripts.NameOf(m) == "Mystical Beast of Serket")
                    && x.My.Hand.Concat(x.My.Deck).Concat(x.My.Extra.Where(c => c.Data.IsFusion)).Any(c => c.Data.IsMonster && x.D.CanSpecialSummon(c, x.Me, by: x.Self, ignoreZones: true));
                f.Cost = async x =>
                {
                    UseHopt(x, "ss");
                    var m = x.MyMonsters().First(m => m.FaceUp && Scripts.NameOf(m) == "Mystical Beast of Serket");
                    await x.D.ToGrave(m, x.Self); await x.D.ToGrave(x.Self, x.Self);
                };
                f.Run = async x => { await SummonPick(x, x.My.Hand.Concat(x.My.Deck).Concat(x.My.Extra.Where(c => c.Data.IsFusion)).Where(c => c.Data.IsMonster), "Special Summon which monster?"); x.D.Shuffle(x.Me); };
            });
        }
        Spell("Terraforming", x => x.Search(c => c.Data.IsSpell && c.Data.Is("Field"), "Add which Field Spell?"), x => x.My.Deck.Any(c => c.Data.IsSpell && c.Data.Is("Field")));
        {
            var s = S("Terrorking Archfiend");
            s.NormalSummonOk = x => x.MyMonsters().Any(m => m.FaceUp && m.In("Archfiend"));
            s.Forbid = (x, q, c) => q == Q.FlipSummon && c == x.Self && !x.MyMonsters().Any(m => m != x.Self && m.FaceUp && m.In("Archfiend"));
            s.On = async (x, e) =>
            {
                if (e.Kind == EvKind.Standby && e.Player == x.Me && Up(x) && x.D.CountActive("Pandemonium") == 0) { if (x.My.Lp > 800) x.D.Pay(x.Me, 800, x.Self); else await x.D.Destroy(x.Self, x.Self); }
                if (e.Kind == EvKind.Activated && Up(x) && e.Link is { } l && l.Player == x.Opp && l.X.T.Contains(x.Self) && x.D.Die(x.Me, x.Self) is 2 or 5) await x.D.Negate(l);
                if (e.Kind == EvKind.BattleDestroyedMonster && e.Card == x.Self) { var d = x.D; var o = e.Other!; d.Flags[$"neg{o.Uid}"] = 1; d.Later(d.Turn, Phase.End, async () => d.Flags.Remove($"neg{o.Uid}")); }
            };
            s.Note = "Its die is rolled when it is targeted (not when the effect resolves).";
        }
        {
            var s = Spell("The A. Forces", async x => x.Self.Vars["stay"] = 1);
            s.Stats = (x, c, st) => { if (x.Self.FaceUp && x.Self.OnField && c.Controller == x.Me && c.Loc == Loc.Monster && c.FaceUp && x.IsRace(c, "Warrior")) st.Add(200 * x.MyMonsters().Count(m => m.FaceUp && x.D.Race(m) is "Warrior" or "Spellcaster"), 0); };
        }
        S("The Agent of Wisdom - Mercury").On = async (x, e) =>
        {
            if (e.Kind == EvKind.EndPhase && e.Player == x.Opp) x.Self.Vars["mercury"] = Up(x) && x.My.Hand.Count == 0 ? x.D.Turn + 1 : 0;
            if (e.Kind == EvKind.Standby && e.Player == x.Me && Up(x) && x.Self.Var("mercury") == x.D.Turn) await x.D.Draw(x.Me, 1);
        };
        Spell("The Big March of Animals", async x =>
        {
            int n = x.MyMonsters().Count(m => m.FaceUp && x.IsRace(m, "Beast"));
            foreach (var m in x.MyMonsters().Where(m => m.FaceUp && x.IsRace(m, "Beast"))) x.D.Boost(m, 200 * n);
        }, x => x.MyMonsters().Any(m => m.FaceUp && x.IsRace(m, "Beast")));
        {
            var s = S("The Creator");
            s.SummonOk = (x, by, k) => x.Self.Loc != Loc.Grave;
            s.OncePerTurn = true;
            s.Can = x => Up(x) && x.My.Hand.Count > 0 && x.D.FreeMonsterZones(x.Me) > 0 && x.Targetable(x.My.Grave.Where(c => c.Data.IsMonster && x.D.CanSpecialSummon(c, x.Me, by: x.Self))).Any();
            s.Cost = async x => await x.Target(x.My.Grave.Where(c => c.Data.IsMonster && x.D.CanSpecialSummon(c, x.Me, by: x.Self)), "Special Summon which monster?");
            s.Run = async x =>
            {
                if (x.My.Hand.Count == 0) return;
                var h = await x.PickN(x.My.Hand, 1, 1, "Send 1 card from your hand to the GY");
                await x.D.ToGrave(h[0], x.Self);
                if (h[0].Loc == Loc.Grave && x.T0 is { Loc: Loc.Grave } t) await x.D.SpecialSummon(t, x.Me, await x.D.ChoosePosition(x.Me, t), by: x.Self);
            };
        }
        {
            var s = Spell("The Dragon's Bead", async x => x.Self.Vars["stay"] = 1);
            s.On = async (x, e) =>
            {
                if (e.Kind != EvKind.Activated || !x.Self.OnField || !x.Self.FaceUp || e.Link is not { } l || !l.Card.Data.IsTrap || l.Card == x.Self) return;
                if (l.X.T.Count != 1 || l.X.T[0] is not { Loc: Loc.Monster, FaceUp: true } t || !x.IsRace(t, "Dragon") || x.My.Hand.Count == 0) return;
                if (!await x.Ask($"The Dragon's Bead: discard 1 card to negate {l.Card.Name}?")) return;
                await x.D.Discard(x.Me, await x.PickN(x.My.Hand, 1, 1, "Discard 1 card"), x.Self);
                await x.D.Negate(l);
            };
        }
        {
            var s = S("The Duke of Demise");
            s.Materials = new() { Cond("1 Fiend or Zombie monster", c => c.Data.IsMonster && c.Data.Race is "Fiend" or "Zombie"), Cond("1 Fiend or Zombie monster", c => c.Data.IsMonster && c.Data.Race is "Fiend" or "Zombie") };
            s.Forbid = (x, q, c) => q == Q.DestroyByBattle && c == x.Self;
            s.On = async (x, e) => { if (e.Kind == EvKind.Standby && e.Player == x.Me && Up(x)) await UpkeepOrDestroy(x, 500, "The Duke of Demise"); };
            s.AddFx(f =>
            {
                f.Label = "One more Normal Summon";
                f.Can = x => Up(x) && Hopt(x, "1") && x.My.Hand.Any(c => c.Data.IsMonster);
                f.Cost = async x => UseHopt(x, "1");
                f.Run = async x => x.My.ExtraSummons++;
            });
            s.GraveFx = new Script
            {
                Can = x => Hopt(x, "2") && x.Targetable(x.My.Grave.Where(c => c != x.Self && c.Data.IsMonster && c.Data.Level >= 4 && c.Data.Race is "Fiend" or "Zombie")).Any(),
                Cost = async x => { UseHopt(x, "2"); await x.D.Banish(x.Self, x.Self); await x.Target(x.My.Grave.Where(c => c.Data.IsMonster && c.Data.Level >= 4 && c.Data.Race is "Fiend" or "Zombie"), "Add which monster?"); },
                Run = async x => { if (x.T0 is { Loc: Loc.Grave } t) await x.D.ToHand(t, x.Self); },
            };
        }
        {
            var s = S("The End of Anubis");
            s.Forbid = (x, q, c) => Up(x) && c != null && c.Loc == Loc.Grave && q is Q.Activate or Q.Target;
            s.Note = "Cards in the GYs cannot be targeted and GY effects cannot be activated while it is face-up.";
        }
        {
            var s = Spell("The Eye of Timaeus", async x =>
            {
                if (x.T0 is not { Loc: Loc.Monster, FaceUp: true } dm) return;
                await x.D.FusionSummon(x.Me, x.Self, pool: x.MyMonsters(), resultOk: f => Mentions(f.Data, Scripts.NameOf(dm)) || (Scripts.NameOf(dm) == "Dark Magician" && Mentions(f.Data, "Dark Magician")));
            }, x => Hopt(x) && x.Targetable(x.MyMonsters().FaceUp().Where(m => m.In("Dark Magician"))).Any(m => x.D.FusionOptions(x.Me, pool: x.MyMonsters(), resultOk: f => Mentions(f.Data, Scripts.NameOf(m)), by: x.Self).Any()),
            async x => { UseHopt(x); await x.Target(x.MyMonsters().FaceUp().Where(m => m.In("Dark Magician")), "Target which Dark Magician monster?"); });
            s.TreatedAs = "Legendary Dragon Timaeus"; s.TreatedAlways = true;
            s.Note = "The other materials come from your field.";
        }
        {
            var s = Spell("The First Sarcophagus", async x => x.Self.Vars["stay"] = 1);
            s.On = async (x, e) =>
            {
                // One Sarcophagus card leaves your field: the others on your field go to the GY.
                if (e.Kind == EvKind.LeftField && e.Card is { } gone && Sarcophagi.Contains(gone.Name) && x.D.LeftFieldLog.LastOrDefault(l => l.c == gone) is { c: not null } lf && lf.controller == x.Me
                    && (gone == x.Self || x.Self.OnField))
                {
                    foreach (var k in x.My.SpellsOnField.Where(c => Sarcophagi.Contains(c.Name)).ToList()) await x.D.ToGrave(k, x.Self);
                    return;
                }
                if (!x.Self.OnField || !x.Self.FaceUp) return;
                var mine = x.My.SpellsOnField.Where(c => c.FaceUp && Sarcophagi.Contains(c.Name)).ToList();
                if (e.Kind == EvKind.EndPhase && e.Player == x.Opp)
                {
                    string next = mine.Any(c => c.Name == "The Second Sarcophagus") ? "The Third Sarcophagus" : "The Second Sarcophagus";
                    if (mine.Any(c => c.Name == next) || x.My.FreeSpellZones == 0) return;
                    var c = x.My.Hand.Concat(x.My.Deck).FirstOrDefault(c => c.Name == next);
                    if (c == null) return;
                    bool deck = c.Loc == Loc.Deck;
                    if (x.D.Place(c, x.Me, Loc.Spell)) { c.FaceUp = true; c.Vars["stay"] = 1; x.D.Seq++; }
                    if (deck) x.D.Shuffle(x.Me);
                    mine = x.My.SpellsOnField.Where(c => c.FaceUp && Sarcophagi.Contains(c.Name)).ToList();
                    if (mine.Count == 3)
                    {
                        foreach (var k in mine) await x.D.ToGrave(k, x.Self);
                        await SummonPick(x, x.My.Hand.Concat(x.My.Deck).Where(c => c.Name == "Spirit of the Pharaoh"), "Special Summon Spirit of the Pharaoh");
                        x.D.Shuffle(x.Me);
                    }
                }
            };
        }
        foreach (var n in Sarcophagi)
        {
            var s = n == "The First Sarcophagus" ? S(n) : Spell(n, async x => { }, x => false);
            var on = s.On;
            s.On = async (x, e) =>
            {
                if (on != null) await on(x, e);
                if (e.Kind == EvKind.LeftField && Sarcophagi.Contains(e.Card!.Name) && e.Card != x.Self && x.Self.OnField && x.Self.FaceUp && x.D.LeftFieldLog.LastOrDefault(l => l.c == e.Card) is { c: not null } l && l.controller == x.Me && e.Card.Loc != Loc.Grave)
                    await x.D.ToGrave(x.Self, x.Self);
            };
            if (n != "The First Sarcophagus") s.Note = "It is placed only by The First Sarcophagus.";
        }
        Spell("The Flute of Summoning Kuriboh", async x =>
        {
            var c = await x.Pick(x.My.Deck.Where(c => c.Name is "Kuriboh" or "Winged Kuriboh"), "Take which card?");
            x.D.Shuffle(x.Me);
            if (c == null) return;
            if (x.D.CanSpecialSummon(c, x.Me, by: x.Self) && await x.D.ChooseText(x.Me, c.Name, new[] { "Add it to your hand", "Special Summon it" }, x.Self) == 1)
                await x.D.SpecialSummon(c, x.Me, await x.D.ChoosePosition(x.Me, c), by: x.Self);
            else { await x.D.ToHand(c, x.Self); x.D.Reveal(x.Opp, c); }
        }, x => x.My.Deck.Any(c => c.Name is "Kuriboh" or "Winged Kuriboh"));
        Spell("The Forces of Darkness", async x => { foreach (var t in x.T.Where(t => t.Loc == Loc.Grave).ToList()) await x.D.ToHand(t, x.Self); },
            x => x.Targetable(x.My.Grave.Where(c => c.Data.IsMonster && c.In("Dark World"))).Count() >= 2,
            async x => x.T.AddRange(await x.PickN(x.Targetable(x.My.Grave.Where(c => c.Data.IsMonster && c.In("Dark World"))), 2, 2, "Target 2 Dark World monsters")));
        Spell("The Grave of Enkindling", async x =>
        {
            foreach (int p in new[] { x.Me, x.Opp })
            {
                var l = x.D.P(p).Grave.Where(c => c.Data.IsMonster && x.D.CanSpecialSummon(c, p, by: x.Self)).ToList();
                if (l.Count == 0) continue;
                var c = await x.D.PickOne(p, l, "Special Summon 1 monster from your GY in Defense Position", x.Self);
                if (c != null && await x.D.SpecialSummon(c, p, true, by: x.Self)) { var m = c; var d = x.D; d.Linger(int.MaxValue, (q, k, pl) => q == Q.ChangePos && k == m && m.Loc == Loc.Monster); }
            }
        }, x => x.Ev is { Kind: EvKind.Destroyed, Battle: true, Card: { } c } && c.Loc == Loc.Grave && x.D.LeftFieldLog.LastOrDefault(l => l.c == c) is { c: not null } l && l.controller == x.Me);
        Spell("The Graveyard in the Fourth Dimension", async x => { foreach (var t in x.T.Where(t => t.Loc == Loc.Grave).ToList()) await x.D.ToDeck(t); x.D.Shuffle(x.Me); },
            x => x.Targetable(x.My.Grave.Where(IsLV)).Count() >= 2,
            async x => x.T.AddRange(await x.PickN(x.Targetable(x.My.Grave.Where(IsLV)), 2, 2, "Return which 2 LV monsters?")));
        S("The Kick Man").On = async (x, e) =>
        {
            if (!e.Is(EvKind.Summoned, x.Self) || e.Arg is not ((int)SummonKind.Special or (int)SummonKind.Fusion or (int)SummonKind.Ritual) || x.My.FreeSpellZones == 0) return;
            var l = x.My.Grave.Where(c => c.Data.IsSpell && c.Data.Is("Equip") && (x.D.S(c)?.EquipOk?.Invoke(x.D.X(c), x.Self) ?? true)).ToList();
            if (l.Count == 0 || !await x.Ask("Equip an Equip Spell from your GY to The Kick Man?")) return;
            var c = await x.Pick(l, "Equip which Equip Spell?");
            if (c == null || !x.D.Place(c, x.Me, Loc.Spell)) return;
            c.FaceUp = true;
            await x.D.Equip(c, x.Self);
        };
        {
            var s = Spell("The Mask of Remnants", async x => { await x.D.ToDeck(x.Self); x.D.Shuffle(x.Self.Owner); }, x => x.Self.Loc == Loc.Hand);
            s.On = async (x, e) => { if (e.Kind == EvKind.Equipped && e.Card == x.Self && e.Other is { Loc: Loc.Monster } m && m.Controller != x.Me) await x.D.TakeControl(m, x.Me); };
            s.Note = "From the hand it only shuffles itself into the Deck; Masked Beast Des Gardius equips it to take control.";
        }
        S("The Sanctuary in the Sky").BattleDamage = (x, src, to, dmg) => x.Self.FaceUp && x.Self.Loc == Loc.Field
            && new[] { x.D.Attacker, x.D.AttackTarget }.Any(m => m != null && m.Loc == Loc.Monster && m.Controller == to && x.IsRace(m, "Fairy")) ? 0 : dmg;
        {
            var s = Spell("The Seal of Orichalcos", async x =>
            {
                x.D.Flags[$"orichalcos{x.Me}"] = 1;
                await x.D.Destroy(x.MyMonsters().Where(m => m.Summoned is SummonKind.Special or SummonKind.Fusion or SummonKind.Ritual).ToList(), x.Self);
            }, x => x.D.Flags.GetValueOrDefault($"orichalcos{x.Me}") == 0);
            s.Stats = (x, c, st) => { if (x.Self.FaceUp && x.Self.Loc == Loc.Field && c.Controller == x.Me && c.Loc == Loc.Monster && c.FaceUp) st.Add(500, 0); };
            s.DestroyInstead = async (x, battle) => { if (battle || !Hopt(x, "safe")) return false; UseHopt(x, "safe"); return true; };
            s.Forbid = (x, q, c) => x.Self.FaceUp && x.Self.Loc == Loc.Field && c != null && (
                (q == Q.SpecialSummon && c.Loc == Loc.Extra && x.D.QueryPlayer == x.Me)
                || (q == Q.BeAttacked && c.Controller == x.Me && x.D.Querying?.Controller == x.Opp && x.MyMonsters().Count(m => m.FaceUp && m.Attack) >= 2
                    && x.MyMonsters().Where(m => m.FaceUp).Min(m => x.Atk(m)) == x.Atk(c) && c.FaceUp));
        }
        foreach (var n in new[] { "The Tricky" })
        {
            var s = S(n);
            s.ProcCan = x => x.My.Hand.Any(c => c != x.Self);
            s.Proc = async x =>
            {
                await x.D.Discard(x.Me, await x.PickN(x.My.Hand.Where(c => c != x.Self), 1, 1, "Discard 1 card"), x.Self);
                await x.D.SpecialSummon(x.Self, x.Me, await x.D.ChoosePosition(x.Me, x.Self), by: x.Self);
            };
        }
        S("The Trojan Horse").TributeWeight = (x, t, forCard) => forCard.Data.Attribute == "EARTH" ? 2 : 1;
        Spell("The Warrior Returning Alive", async x => { if (x.T0 is { Loc: Loc.Grave } t) await x.D.ToHand(t, x.Self); },
            x => x.Targetable(x.My.Grave.Where(c => c.Data.IsMonster && c.Data.Race == "Warrior")).Any(), async x => await x.Target(x.My.Grave.Where(c => c.Data.IsMonster && c.Data.Race == "Warrior"), "Add which Warrior?"));
        {
            var s = S("The Winged Dragon of Ra - Immortal Phoenix");
            s.SummonOk = (x, by, k) => by == x.Self;
            s.Forbid = (x, q, c) => c == x.Self && Up(x) && q is Q.AffectedBySpell or Q.AffectedByTrap or Q.AffectedByMonster or Q.Target or Q.DestroyByEffect or Q.ChangeControl;
            s.On = async (x, e) =>
            {
                if (e.Kind == EvKind.ToGrave && e.Card!.Name == "The Winged Dragon of Ra" && e.From == Loc.Monster && e.Card.Owner == x.Me && x.Self.Loc == Loc.Grave && x.D.CanSpecialSummon(x.Self, x.Me, by: x.Self))
                    await x.D.SpecialSummon(x.Self, x.Me, false, by: x.Self);
                if (e.Kind == EvKind.EndPhase && Up(x) && x.Self.Var("phoenixend") != x.D.Turn)
                {
                    x.Self.Vars["phoenixend"] = x.D.Turn;
                    await x.D.ToGrave(x.Self, x.Self);
                    if (x.Self.Loc == Loc.Grave) { await SummonPick(x, x.My.Hand.Concat(x.My.Deck).Where(c => c.Name == "The Winged Dragon of Ra - Sphere Mode"), "Special Summon The Winged Dragon of Ra - Sphere Mode", ignore: true); x.D.Shuffle(x.Me); }
                }
            };
            s.Can = x => Up(x) && x.My.Lp > 1000 && x.D.AllMonsters.Any(m => m != x.Self);
            s.Cost = async x => x.D.Pay(x.Me, 1000, x.Self);
            s.Run = async x => { var t = await x.Pick(x.D.AllMonsters.Where(m => m != x.Self), "Send which monster to the GY?"); if (t != null) await x.D.ToGrave(t, x.Self); };
        }
        {
            var s = S("The Winged Dragon of Ra - Sphere Mode");
            s.SummonOk = (x, by, k) => by?.Name == "The Winged Dragon of Ra - Immortal Phoenix";
            s.TributesNeeded = x => 3;
            s.NoSet = true;
            s.Forbid = (x, q, c) => c == x.Self && Up(x) && (q == Q.Attack || ((q == Q.BeAttacked || q == Q.Target) && (x.D.Querying ?? x.D.Targeter)?.Controller == x.Opp));
            s.Can = x => Up(x) && !x.D.Forbidden(Q.Tribute, x.Self) && x.My.Hand.Concat(x.My.Deck).Any(c => c.Name == "The Winged Dragon of Ra");
            s.Cost = async x => await x.D.Tribute(x.Self, x.Self);
            s.Run = async x =>
            {
                var c = await x.Pick(x.My.Hand.Concat(x.My.Deck).Where(c => c.Name == "The Winged Dragon of Ra" && x.D.CanSpecialSummon(c, x.Me, by: x.Self, ignore: true)), "Special Summon The Winged Dragon of Ra");
                x.D.Shuffle(x.Me);
                if (c != null && await x.D.SpecialSummon(c, x.Me, false, by: x.Self, ignore: true)) { SetBaseAtk(x, c, 4000); c.Vars["baseatkturn"] = int.MaxValue; c.Vars["basedef"] = 4001; c.Vars["basedefturn"] = int.MaxValue; }
            };
            s.Note = "Its Tributes come from your own field only (the game has no Tribute Summon to the opponent's side).";
        }
        SelfBoost("Theban Nightmare", x => x.My.Hand.Count == 0 && !x.My.Spells.Any(c => c != null) ? 1500 : 0);
        S("Thestalos the Firestorm Monarch").On = async (x, e) =>
        {
            if (!e.Is(EvKind.Summoned, x.Self) || e.Arg != (int)SummonKind.Tribute || x.Their.Hand.Count == 0) return;
            var c = x.Their.Hand[x.D.Rng.Int(x.Their.Hand.Count)];
            x.D.Reveal(x.Me, c);
            await x.D.Discard(x.Opp, new[] { c }, x.Self);
            if (c.Data.IsMonster) await x.D.Damage(x.Opp, 100 * c.Data.Level, x.Self);
        };
        Spell("Thousand Energy", async x =>
        {
            var l = x.MyMonsters().Where(m => m.FaceUp && m.Data.IsNormalMonster && !m.IsToken && m.Data.Level == 2).ToList();
            foreach (var m in l) { SetBaseAtk(x, m, Math.Max(0, m.Data.Atk) + 1000); m.Vars["basedef"] = Math.Max(0, m.Data.Def) + 1001; m.Vars["basedefturn"] = x.D.Turn; }
            var d = x.D; int me = x.Me;
            d.Later(d.Turn, Phase.End, async () => await d.Destroy(d.P(me).MonstersOnField.Where(m => m.Data.IsNormalMonster && m.Data.Level == 2).ToList()));
        }, x => x.MyMonsters().Any(m => m.FaceUp && m.Data.IsNormalMonster && m.Data.Level == 2));
        Spell("Threatening Roar", async x => { var d = x.D; int opp = x.Opp; d.Linger(d.Turn, (q, c, p) => q == Q.Attack && c != null && c.Controller == opp); });
        {
            var s = S("Thunder Ball");
            s.AddFx(f =>
            {
                f.Label = "Special Summon from hand"; f.FromHand = true; f.Speed = 2; f.AnyTurn = true;
                f.Can = x => Hopt(x, "1") && x.Ev is { Kind: EvKind.AfterDamage } && x.D.CanSpecialSummon(x.Self, x.Me, by: x.Self);
                f.Cost = async x => UseHopt(x, "1");
                f.Run = async x => { if (x.Self.Loc == Loc.Hand) await x.D.SpecialSummon(x.Self, x.Me, await x.D.ChoosePosition(x.Me, x.Self), by: x.Self); };
            });
            s.AddFx(f =>
            {
                f.Label = "Send a Trap: destroy its column";
                f.Can = x => Up(x) && Hopt(x, "2") && x.My.Hand.Concat(x.My.SpellsOnField.Where(c => !c.FaceUp)).Any(c => c.Data.IsTrap);
                f.Cost = async x => { UseHopt(x, "2"); var c = await x.Pick(x.My.Hand.Concat(x.My.SpellsOnField.Where(c => !c.FaceUp)).Where(c => c.Data.IsTrap), "Send which Trap to the GY?"); if (c != null) await x.D.ToGrave(c, x.Self); };
                f.Run = async x =>
                {
                    if (!Up(x)) return;
                    int z = x.Self.Zone;
                    var l = new[] { x.Their.Monsters[4 - z] }.Where(m => m != null).Cast<Card>().ToList();
                    await x.D.Destroy(l, x.Self);
                };
            });
            s.On = async (x, e) =>
            {
                if (e.Is(EvKind.Destroyed, x.Self) && e.From == Loc.Monster && x.Self.Loc == Loc.Grave && Hopt(x, "3") && x.My.Grave.Any() && await x.Ask("Thunder Ball: target a card in your GY and roll a die?"))
                {
                    UseHopt(x, "3");
                    var t = await x.Pick(x.Targetable(x.My.Grave), "Target which card?");
                    if (t != null && x.D.Die(x.Me, x.Self) == 6) await x.D.ToHand(t, x.Self);
                }
            };
            s.Note = "Its column is its own zone and the opponent's zone across from it.";
        }
        Spell("Thunder Crash", async x =>
        {
            var l = x.MyMonsters().ToList();
            await x.D.Destroy(l, x.Self);
            int n = l.Count(m => !m.OnField);
            if (n > 0) await x.D.Damage(x.Opp, 300 * n, x.Self);
        }, x => x.MyMonsters().Any());
        S("Thunder Nyan Nyan").On = async (x, e) => { if (Up(x) && x.MyMonsters().Any(m => m != x.Self && m.FaceUp && x.D.Attr(m) != "LIGHT")) await x.D.Destroy(x.Self, x.Self); };
        {
            static IEnumerable<Card> Pull(Ctx x, Card t) => x.My.Deck.Where(c => c.Data.IsMonster && c.Data.Race == x.D.Race(t) && c.Data.Attribute == x.D.Attr(t) && c.Data.Level == x.D.Level(t) && c.Name != Scripts.NameOf(t));
            Spell("Ties of the Brethren", async x =>
            {
                if (x.T0 is not { Loc: Loc.Monster, FaceUp: true } t) return;
                var d = x.D; int me = x.Me;
                var l = Pull(x, t).GroupBy(c => c.Name).Select(g => g.First()).ToList();
                if (l.Count >= 2 && d.FreeMonsterZones(me) >= 2)
                    foreach (var c in await x.PickN(l, 2, 2, "Special Summon 2 monsters with different names"))
                        await d.SpecialSummon(c, me, await d.ChoosePosition(me, c), by: x.Self);
                d.Shuffle(me);
                d.Linger(d.Turn, (q, c, p) => q == Q.SpecialSummon && p == me);
            }, x => x.My.Lp > 2000 && x.D.FreeMonsterZones(x.Me) >= 2 && x.Targetable(x.MyMonsters().FaceUp().Where(m => x.D.Level(m) <= 4 && Pull(x, m).Select(c => c.Name).Distinct().Count() >= 2)).Any(),
            async x => { x.D.Pay(x.Me, 2000, x.Self); x.D.TurnFlags.Add("nobattle"); await x.Target(x.MyMonsters().FaceUp().Where(m => x.D.Level(m) <= 4 && Pull(x, m).Select(c => c.Name).Distinct().Count() >= 2), "Target which monster?"); });
        }
        {
            string[] knights = { "Legendary Knight Timaeus", "Legendary Knight Critias", "Legendary Knight Hermos" };
            var s = S("Timaeus the Knight of Destiny");
            s.SummonOk = (x, by, k) => by == x.Self;
            s.ProcFromDeck = true;
            s.ProcCan = x => knights.All(n => x.MyMonsters().Any(m => Scripts.NameOf(m) == n));
            s.Proc = async x =>
            {
                foreach (var n in knights) { var m = x.MyMonsters().First(m => Scripts.NameOf(m) == n); await x.D.ToGrave(m, x.Self); }
                await x.D.SpecialSummon(x.Self, x.Me, await x.D.ChoosePosition(x.Me, x.Self), by: x.Self);
            };
            s.Forbid = (x, q, c) => c == x.Self && Up(x) && x.D.Affecting is { } a && a != x.Self && q is Q.AffectedByMonster or Q.AffectedBySpell or Q.AffectedByTrap;
            s.AddFx(f =>
            {
                f.Label = "ATK/DEF = highest ATK"; f.Speed = 2; f.AnyTurn = true;
                f.Can = x => Up(x) && x.Ev is { Kind: EvKind.DamageCalc } e && (e.Card == x.Self || e.Other == x.Self) && x.Self.Var("timaeuscalc") != x.D.Seq;
                f.Cost = async x => x.Self.Vars["timaeuscalc"] = x.D.Seq;
                f.Run = async x =>
                {
                    if (!Up(x)) return;
                    int top = x.D.FaceUpMonsters.Max(m => x.Atk(m));
                    SetBaseAtk(x, x.Self, top); x.Self.Vars["basedef"] = top + 1; x.Self.Vars["basedefturn"] = x.D.Turn;
                    x.Self.Vars["modatk"] = 0; x.Self.Vars["moddef"] = 0; x.Self.TempAtk = 0; x.Self.TempDef = 0; x.D.Seq++;
                };
            });
            s.On = async (x, e) =>
            {
                if (!e.Is(EvKind.Destroyed, x.Self) || !e.Battle) return;
                var l = x.My.Grave.Where(c => c.Data.IsMonster && c.In("Legendary Knight") && x.D.CanSpecialSummon(c, x.Me, by: x.Self, ignore: true)).GroupBy(c => c.Name).Select(g => g.First()).ToList();
                if (l.Count < 3 || x.D.FreeMonsterZones(x.Me) < 3 || !await x.Ask("Special Summon 3 Legendary Knights from your GY?")) return;
                foreach (var c in l.Take(3)) await x.D.SpecialSummon(c, x.Me, await x.D.ChoosePosition(x.Me, c), by: x.Self, ignore: true);
            };
            s.Note = "Its card text in the data is cut short; the last effect Special Summons the 3 Legendary Knights from your GY.";
        }
        {
            var s = S("Time Magic Hammer");
            s.SummonOk = (x, by, k) => by?.Name == "The Claw of Hermos";
            s.On = async (x, e) =>
            {
                if (e.Is(EvKind.Summoned, x.Self) && x.My.FreeSpellZones > 0)
                {
                    var t = await x.Pick(x.Targetable(x.D.FaceUpMonsters.Where(m => m != x.Self)), "Equip Time Magic Hammer to which monster?");
                    if (t == null) return;
                    x.Self.ResetFieldState();
                    x.D.Place(x.Self, x.Me, Loc.Spell); x.Self.FaceUp = true;
                    await x.D.Equip(x.Self, t);
                    x.Self.Vars["asspell"] = 1;
                    return;
                }
                if (e.Kind == EvKind.DamageStart && x.Self.Loc == Loc.Spell && x.Self.EquippedTo is { } m && (e.Card == m || e.Other == m))
                {
                    var o = e.Card == m ? e.Other : e.Card;
                    if (o == null || o.Controller == m.Controller || !await x.Ask($"Roll a die to banish {o.Name}?")) return;
                    int n = x.D.Die(x.Me, x.Self);
                    await x.D.Banish(o, x.Self);
                    if (o.Loc != Loc.Banished) return;
                    var d = x.D; int owner = o.Owner, back = d.Turn + n; bool done = false;
                    d.Watch(int.MaxValue, async ev =>
                    {
                        if (done || ev.Kind != EvKind.Standby || d.Turn < back) return;
                        done = true;
                        if (o.Loc == Loc.Banished && d.CanSpecialSummon(o, owner, ignore: true)) await d.SpecialSummon(o, owner, false, ignore: true);
                    });
                }
            };
        }
        Sure("Swift Gaia the Fierce Knight", "Swing of Memories", "Synthesis Spell", "System Down", "T.A.D.P.O.L.E.", "Terraforming", "The Big March of Animals", "The Forces of Darkness",
            "The Graveyard in the Fourth Dimension", "The Tricky", "The Trojan Horse", "The Warrior Returning Alive", "Theban Nightmare", "Thestalos the Firestorm Monarch", "Threatening Roar", "Thunder Crash", "Thunder Nyan Nyan", "The Sanctuary in the Sky");
    }
}
