namespace PoC.Engine;

// Official cards, batch 21 (Reflect Bounder .. Sasuke Samurai).
public static partial class Scripts
{
    /// Roll Out! / Union hosts: the monster types or names a Union monster's text equips to.
    static bool UnionHostOk(Ctx x, Card union, Card host)
    {
        var names = System.Text.RegularExpressions.Regex.Matches(union.Data.Desc, "\"([^\"]+)\"").Select(m => m.Groups[1].Value).Where(n => n != union.Name).ToList();
        if (names.Count > 0) return names.Contains(Scripts.NameOf(host));
        var race = System.Text.RegularExpressions.Regex.Match(union.Data.Desc, @"equip (?:it|this card) to (?:a|an|1) (?:face-up )?([A-Z][\w-]+?)(?:-Type)? monster");
        return !race.Success || x.IsRace(host, race.Groups[1].Value);
    }

    static void CardsP21(CardDb db)
    {
        {
            var s = S("Reflect Bounder");
            s.On = async (x, e) =>
            {
                if (e.Kind == EvKind.DamageStart && e.Other == x.Self) x.Self.Vars["bounder"] = Up(x) && x.Self.Attack ? x.D.Seq + 1 : 0;
                if (e.Kind == EvKind.DamageCalc && e.Other == x.Self && x.Self.Var("bounder") > 0 && e.Card is { Loc: Loc.Monster } a && a.Controller == x.Opp && Up(x))
                { x.Self.Vars["bounder"] = -1; await x.D.Damage(x.Opp, x.Atk(a), x.Self); }
                if (e.Kind == EvKind.AfterCalc && e.Other == x.Self && x.Self.Var("bounder") == -1) { x.Self.Vars["bounder"] = 0; if (x.Self.Loc == Loc.Monster) await x.D.Destroy(x.Self, x.Self); }
            };
        }
        OnBattleDeath("Regenerating Rose", async x =>
        {
            if (x.D.FreeMonsterZones(x.Me) == 0 || !await x.Ask("Special Summon 2 Regenerating Rose Tokens?")) return;
            for (int i = 0; i < 2 && x.D.FreeMonsterZones(x.Me) > 0; i++)
            { var t = x.D.MakeToken("Regenerating Rose Token", x.Me, 1200, 1200, "DARK", "Plant", 3); await x.D.SpecialSummon(t, x.Me, await x.D.ChoosePosition(x.Me, t), by: x.Self); }
        });
        S("Reign-Beaux, Overlord of Dark World").On = async (x, e) =>
        {
            if (e.Kind != EvKind.Discarded || e.Cost || e.Cards?.Contains(x.Self) != true || e.Other?.Controller != x.Opp || x.Self.Loc != Loc.Grave || !x.D.CanSpecialSummon(x.Self, x.Me, by: x.Self)) return;
            if (!await x.D.SpecialSummon(x.Self, x.Me, await x.D.ChoosePosition(x.Me, x.Self), by: x.Self)) return;
            int i = await x.D.ChooseText(x.Me, "Reign-Beaux: destroy", new[] { "All monsters your opponent controls", "All Spell/Trap Cards your opponent controls" }, x.Self);
            await x.D.Destroy(i == 0 ? x.OppMonsters().ToList() : x.Spells(x.Opp).ToList(), x.Self);
        };
        Spell("Reinforcement of the Army", x => x.Search(c => c.Data.IsMonster && c.Data.Race == "Warrior" && c.Data.Level <= 4, "Add which Warrior?"),
            x => x.My.Deck.Any(c => c.Data.IsMonster && c.Data.Race == "Warrior" && c.Data.Level <= 4));
        {
            var s = Spell("Relay Soul", async x =>
            {
                var c = await x.Pick(x.My.Hand.Where(c => c.Data.IsMonster && x.D.CanSpecialSummon(c, x.Me, by: x.Self)), "Special Summon which monster?");
                if (c == null || !await x.D.SpecialSummon(c, x.Me, await x.D.ChoosePosition(x.Me, c), by: x.Self)) return;
                var d = x.D; int me = x.Me; bool done = false;
                d.Linger(int.MaxValue, (q, k, p) => !done && q == Q.TakeDamage && p == me && c.Loc == Loc.Monster && c.FaceUp && c.Controller == me);
                d.Watch(int.MaxValue, async ev => { if (!done && ev.Kind == EvKind.LeftField && ev.Card == c) { done = true; d.Win(1 - me, "Relay Soul"); } });
            }, x => x.D.FreeMonsterZones(x.Me) > 0 && x.My.Hand.Any(c => c.Data.IsMonster && x.D.CanSpecialSummon(c, x.Me, by: x.Self)));
        }
        Spell("Release Restraint", async x => { await SummonPick(x, x.My.Hand.Concat(x.My.Deck).Where(c => c.Name == "Gearfried the Swordmaster"), "Special Summon Gearfried the Swordmaster"); x.D.Shuffle(x.Me); },
            x => x.D.Tributable(x.Me).Any(m => m.FaceUp && Scripts.NameOf(m) == "Gearfried the Iron Knight") && x.My.Hand.Concat(x.My.Deck).Any(c => c.Name == "Gearfried the Swordmaster"),
            async x => { var t = await x.Pick(x.D.Tributable(x.Me).Where(m => m.FaceUp && Scripts.NameOf(m) == "Gearfried the Iron Knight"), "Tribute Gearfried the Iron Knight"); if (t != null) await x.D.Tribute(t, x.Self); });
        Spell("Relieve Monster", async x =>
        {
            if (x.T0 is not { Loc: Loc.Monster } t) return;
            await x.D.ToHand(t, x.Self);
            await SummonPick(x, x.My.Hand.Where(c => c.Data.IsMonster && c.Data.Level == 4 && c != t), "Special Summon a Level 4 monster from your hand");
        }, x => AttackOnMe(x) && x.Targetable(x.MyMonsters()).Any(), async x => await x.Target(x.MyMonsters(), "Return which monster to your hand?"));
        Spell("Reload", async x =>
        {
            var l = x.My.Hand.Where(c => c != x.Self).ToList();
            foreach (var c in l) await x.D.ToDeck(c);
            x.D.Shuffle(x.Me);
            if (l.Count > 0) await x.D.Draw(x.Me, l.Count);
        });
        {
            var s = S("Rescue Cat");
            s.Can = x => Up(x) && Hopt(x) && x.D.FreeMonsterZones(x.Me) + 1 >= 2 && x.My.Deck.Count(c => c.Data.IsMonster && c.Data.Race == "Beast" && c.Data.Level <= 3) >= 2;
            s.Cost = async x => { UseHopt(x); await x.D.ToGrave(x.Self, x.Self); };
            s.Run = async x =>
            {
                var l = x.My.Deck.Where(c => c.Data.IsMonster && c.Data.Race == "Beast" && c.Data.Level <= 3 && x.D.CanSpecialSummon(c, x.Me, by: x.Self, ignoreZones: true)).ToList();
                if (l.Count < 2 || x.D.FreeMonsterZones(x.Me) < 2) return;
                var d = x.D;
                foreach (var c in await x.PickN(l, 2, 2, "Special Summon which 2 Beasts?"))
                    if (await d.SpecialSummon(c, x.Me, await d.ChoosePosition(x.Me, c), by: x.Self))
                    { d.Flags[$"neg{c.Uid}"] = 1; d.Later(d.Turn, Phase.End, async () => { d.Flags.Remove($"neg{c.Uid}"); if (c.Loc == Loc.Monster) await d.Destroy(c); }); }
                d.Shuffle(x.Me);
            };
        }
        S("Rescueroid").On = async (x, e) =>
        {
            if (!Up(x) || e.Kind != EvKind.Destroyed || !e.Battle || e.Card == x.Self || e.Card!.Loc != Loc.Grave || !e.Card.Name.Contains("roid")) return;
            if (x.D.LeftFieldLog.LastOrDefault(l => l.c == e.Card) is { c: not null } l && l.controller == x.Me && await x.Ask($"Return {e.Card.Name} to its owner's hand?")) await x.D.ToHand(e.Card, x.Self);
        };
        Spell("Return from the Different Dimension", async x =>
        {
            var d = x.D; var list = new List<Card>();
            foreach (var c in x.My.Banished.Where(c => c.Data.IsMonster && c.FaceUp).ToList())
            {
                if (d.FreeMonsterZones(x.Me) == 0) break;
                if (d.CanSpecialSummon(c, x.Me, by: x.Self) && await d.SpecialSummon(c, x.Me, await d.ChoosePosition(x.Me, c), by: x.Self)) list.Add(c);
            }
            d.Later(d.Turn, Phase.End, async () => { foreach (var c in list.Where(c => c.Loc == Loc.Monster)) await d.Banish(c); });
        }, x => x.My.Lp > 1 && x.D.FreeMonsterZones(x.Me) > 0 && x.My.Banished.Any(c => c.Data.IsMonster && c.FaceUp && x.D.CanSpecialSummon(c, x.Me, by: x.Self)), async x => x.D.Pay(x.Me, x.My.Lp / 2, x.Self));
        Spell("Return Soul", async x =>
        {
            var l = x.D.Players.SelectMany(p => p.Grave).Where(c => c.Data.IsMonster && x.D.Flags.GetValueOrDefault($"destroyed{c.Uid}") == x.D.Turn).ToList();
            if (l.Count == 0) return;
            foreach (var c in await x.PickN(l, 0, Math.Min(3, l.Count), "Return up to 3 monsters to their owners' Decks")) { await x.D.ToDeck(c); x.D.Shuffle(c.Owner); }
        }, x => x.D.Phase == Phase.End);
        S("Return Zombie").On = async (x, e) =>
        {
            if (e.Kind == EvKind.Standby && e.Player == x.Me && x.Self.Loc == Loc.Grave && x.My.Hand.Count == 0 && x.My.Lp > 500 && await x.Ask("Pay 500 LP to add Return Zombie to your hand?"))
            { x.D.Pay(x.Me, 500, x.Self); if (x.My.Hand.Count == 0) await x.D.ToHand(x.Self, x.Self); }
        };
        Spell("Reversal of Fate", async x => { if (x.T0 is { Loc: Loc.Monster } t && t.Var("arcana") is 1 or 2) { t.Vars["arcana"] = 3 - t.Var("arcana"); x.D.Seq++; } },
            x => x.MyMonsters().Any(m => m.FaceUp && m.In("Arcana Force") && m.Var("arcana") is 1 or 2),
            async x => await x.Target(x.MyMonsters().FaceUp().Where(m => m.In("Arcana Force") && m.Var("arcana") is 1 or 2), "Select which Arcana Force monster?"));
        Spell("Reversal Quiz", async x =>
        {
            foreach (var c in x.My.Hand.ToList()) await x.D.ToGrave(c, x.Self);
            foreach (var c in x.My.FieldCards.Where(c => c != x.Self).ToList()) await x.D.ToGrave(c, x.Self);
            if (x.My.Deck.Count == 0) return;
            int call = await x.D.ChooseText(x.Me, "Reversal Quiz: call the top card of your Deck", new[] { "Monster", "Spell", "Trap" }, x.Self);
            var top = x.My.Deck[0];
            x.D.Reveal(x.Me, top); x.D.Reveal(x.Opp, top);
            if (call == (top.Data.IsMonster ? 0 : top.Data.IsSpell ? 1 : 2)) { (x.My.Lp, x.Their.Lp) = (x.Their.Lp, x.My.Lp); x.D.Seq++; }
        });
        Spell("Reverse of Neos", async x =>
        {
            var n = await x.Pick(x.My.Deck.Where(c => c.Name == "Elemental HERO Neos" && x.D.CanSpecialSummon(c, x.Me, by: x.Self)), "Special Summon Elemental HERO Neos");
            x.D.Shuffle(x.Me);
            if (n == null || !await x.D.SpecialSummon(n, x.Me, false, by: x.Self)) return;
            n.Vars["modatk"] = n.Var("modatk") + 1000; x.D.Seq++;
            var d = x.D; d.Later(d.Turn, Phase.End, async () => { if (n.Loc == Loc.Monster) await d.Destroy(n); });
        }, x => x.Ev is { Kind: EvKind.Destroyed, Card: { } c } && c.Data.IsFusion && c.In("Neos") && x.D.LeftFieldLog.LastOrDefault(l => l.c == c) is { c: not null } l && l.controller == x.Me && l.faceUp
            && x.D.FreeMonsterZones(x.Me) > 0 && x.My.Deck.Any(c => c.Name == "Elemental HERO Neos"));
        {
            static bool Listed(Ctx x, Card f, Card m) => x.D.Recipes(f).Any(r => r.Any(mat => mat.Ok(m)));
            static IEnumerable<Card> Fusions(Ctx x) => x.My.Extra.Where(f => f.Data.IsFusion && x.My.Hand.Any(m => m.Data.IsMonster && Listed(x, f, m) && x.D.CanSpecialSummon(m, x.Me, by: x.Self)));
            Spell("Revoke Fusion", async x =>
            {
                var f = await x.Pick(Fusions(x), "Send which Fusion Monster to the GY?");
                if (f == null) return;
                await x.D.ToGrave(f, x.Self);
                var m = await x.Pick(x.My.Hand.Where(m => m.Data.IsMonster && Listed(x, f, m) && x.D.CanSpecialSummon(m, x.Me, by: x.Self)), "Special Summon which material?");
                if (m == null || !await x.D.SpecialSummon(m, x.Me, await x.D.ChoosePosition(x.Me, m), by: x.Self)) return;
                var d = x.D; d.Later(d.Turn, Phase.End, async () => { if (m.Loc == Loc.Monster) await d.ToGrave(m); });
            }, x => x.My.Hand.Any(c => c.Name == "Polymerization") && x.D.FreeMonsterZones(x.Me) > 0 && Fusions(x).Any(),
            async x => await x.D.Discard(x.Me, new[] { x.My.Hand.First(c => c.Name == "Polymerization") }, x.Self));
        }
        {
            var s = Spell("Ride of the Valkyries", async x =>
            {
                var used = new HashSet<string>();
                while (x.D.FreeMonsterZones(x.Me) > 0)
                {
                    var c = await x.Pick(x.My.Hand.Where(c => c.Data.IsMonster && c.In("Valkyrie") && !used.Contains(c.Name) && x.D.CanSpecialSummon(c, x.Me, by: x.Self)), "Special Summon a Valkyrie? (cancel to stop)", optional: true);
                    if (c == null) break;
                    if (await x.D.SpecialSummon(c, x.Me, await x.D.ChoosePosition(x.Me, c), by: x.Self)) used.Add(c.Name);
                }
                var d = x.D; int me = x.Me, turn = d.Turn;
                if (used.Count >= 3)
                {
                    d.TurnFlags.Add($"nobattledamage{me}");
                    d.Watch(turn + 1, async ev => { if (ev.Kind == EvKind.DrawPhase && d.Turn == turn + 1) d.TurnFlags.Add($"nobattledamage{me}"); });
                }
                d.Later(turn, Phase.End, async () => { foreach (var m in d.P(me).MonstersOnField.ToList()) await d.ToDeck(m); d.Shuffle(me); });
            }, x => x.D.FreeMonsterZones(x.Me) > 0 && x.My.Hand.Any(c => c.Data.IsMonster && c.In("Valkyrie")));
            s.GraveFx = new Script
            {
                Can = x => Hopt(x, "gy") && x.My.Deck.Any(c => c.Name == "Mischief of the Time Goddess"),
                Cost = async x => { UseHopt(x, "gy"); await x.D.Banish(x.Self, x.Self); },
                Run = async x => await x.Search(c => c.Name == "Mischief of the Time Goddess", "Add Mischief of the Time Goddess"),
            };
        }
        Spell("Ring of Defense", async x =>
        {
            var link = x.Ev!.Link!; var d = x.D; int me = x.Me;
            d.Linger(d.Turn, (q, c, p) => q == Q.TakeEffectDamage && d.Resolving == link);
        }, x => x.Ev is { Kind: EvKind.Activated, Link: { } l } && l.Card.Data.IsTrap && System.Text.RegularExpressions.Regex.IsMatch(l.Card.Data.Desc, @"inflict[^.]*damage|damage[^.]*inflict|take[^.]*damage", System.Text.RegularExpressions.RegexOptions.IgnoreCase));
        Spell("Rite of Spirit", async x =>
        {
            if (x.T0 is not { Loc: Loc.Grave } t) return;
            x.D.TurnFlags.Add("necroexempt");
            await x.D.SpecialSummon(t, x.Me, await x.D.ChoosePosition(x.Me, t), by: x.Self);
            x.D.TurnFlags.Remove("necroexempt");
        }, x => { x.D.TurnFlags.Add("necroexempt"); var ok = x.D.FreeMonsterZones(x.Me) > 0 && x.Targetable(x.My.Grave.Where(c => c.Data.IsMonster && c.In("Gravekeeper's") && x.D.CanSpecialSummon(c, x.Me, by: x.Self))).Any(); x.D.TurnFlags.Remove("necroexempt"); return ok; },
        async x => { x.D.TurnFlags.Add("necroexempt"); await x.Target(x.My.Grave.Where(c => c.Data.IsMonster && c.In("Gravekeeper's")), "Special Summon which Gravekeeper's?"); x.D.TurnFlags.Remove("necroexempt"); });
        {
            var s = Spell("Ritual of the Matador", async x => await RitualSummon(x, m => m.Name == "Matador Archfiend", exact: false),
                x => RitualTargets(x, m => m.Name == "Matador Archfiend", exact: false).Any());
            s.GraveFx = new Script
            {
                Can = x => Hopt(x, "gy") && x.My.Hand.Any(c => c.Data.IsMonster && !c.Data.IsRitual && c.In("Archfiend") && x.D.CanSpecialSummon(c, x.Me, by: x.Self)),
                Cost = async x => { UseHopt(x, "gy"); await x.D.Banish(x.Self, x.Self); },
                Run = async x => await SummonPick(x, x.My.Hand.Where(c => c.Data.IsMonster && !c.Data.IsRitual && c.In("Archfiend")), "Special Summon which Archfiend?"),
            };
        }
        {
            var s = S("Ritual Sanctuary");
            s.AddFx(f =>
            {
                f.Label = "Discard a Spell: add a LIGHT Ritual card";
                f.Can = x => x.Self.FaceUp && Hopt(x, "1") && x.My.Hand.Any(c => c.Data.IsSpell) && x.My.Deck.Any(c => (c.Data.IsRitual && c.Data.Attribute == "LIGHT") || (c.Data.IsSpell && c.Data.Is("Ritual")));
                f.Cost = async x => { UseHopt(x, "1"); await x.D.Discard(x.Me, await x.PickN(x.My.Hand.Where(c => c.Data.IsSpell), 1, 1, "Discard 1 Spell"), x.Self); };
                f.Run = async x => await x.Search(c => (c.Data.IsRitual && c.Data.Attribute == "LIGHT") || (c.Data.IsSpell && c.Data.Is("Ritual")), "Add which card?");
            });
            s.AddFx(f =>
            {
                f.Label = "Shuffle Spells: revive a LIGHT Fairy";
                f.Can = x => x.Self.FaceUp && Hopt(x, "2") && x.D.FreeMonsterZones(x.Me) > 0 && x.My.Grave.Any(c => c.Data.IsMonster && c.Data.Attribute == "LIGHT" && c.Data.Race == "Fairy" && c.Data.Level >= 1 && c.Data.Level <= x.My.Grave.Count(g => g.Data.IsSpell));
                f.Cost = async x =>
                {
                    UseHopt(x, "2");
                    var t = await x.Pick(x.Targetable(x.My.Grave.Where(c => c.Data.IsMonster && c.Data.Attribute == "LIGHT" && c.Data.Race == "Fairy" && c.Data.Level <= x.My.Grave.Count(g => g.Data.IsSpell))), "Special Summon which LIGHT Fairy?");
                    if (t == null) return;
                    x.T.Add(t);
                    foreach (var c in await x.PickN(x.My.Grave.Where(c => c.Data.IsSpell), t.Data.Level, t.Data.Level, $"Shuffle {t.Data.Level} Spells into the Deck")) await x.D.ToDeck(c);
                    x.D.Shuffle(x.Me);
                };
                f.Run = async x => { if (x.T0 is { Loc: Loc.Grave } t) await x.D.SpecialSummon(t, x.Me, await x.D.ChoosePosition(x.Me, t), by: x.Self); };
            });
        }
        {
            var s = Spell("Ritual Weapon", async x => { if (x.T0 is { } t && t.Valid() && t.FaceUp) await x.D.Equip(x.Self, t); },
                x => x.Targetable(x.D.FaceUpMonsters.Where(m => m.Data.IsRitual && x.D.Level(m) <= 6)).Any(), async x => await x.Target(x.D.FaceUpMonsters.Where(m => m.Data.IsRitual && x.D.Level(m) <= 6), "Equip to which Ritual Monster?"));
            s.EquipOk = (x, m) => m.Data.IsRitual && x.D.Level(m) <= 6;
            s.Stats = (x, c, st) => { if (c == x.Self.EquippedTo) st.Add(1500, 1500); };
        }
        {
            var s = Spell("Rivalry of Warlords", async x => x.Self.Vars["stay"] = 1);
            static string? Kept(Ctx x, int p) => x.D.P(p).MonstersOnField.Where(m => m.FaceUp).Select(m => x.D.Race(m)).Distinct() is var l && l.Count() == 1 ? l.First() : null;
            s.Forbid = (x, q, c) => x.Self.OnField && x.Self.FaceUp && q is Q.NormalSummon or Q.SpecialSummon or Q.FlipSummon && c != null
                && (x.D.QueryPlayer >= 0 ? x.D.QueryPlayer : c.Controller) is var p && Kept(x, p) is { } r && (c.AsMonster ?? c.Data).Race != r;
            s.On = async (x, e) =>
            {
                if (!x.Self.OnField || !x.Self.FaceUp) return;
                foreach (var p in new[] { x.Me, x.Opp })
                {
                    var races = x.D.P(p).MonstersOnField.Where(m => m.FaceUp).Select(m => x.D.Race(m)).Distinct().ToList();
                    if (races.Count < 2) continue;
                    int i = await x.D.ChooseText(p, "Rivalry of Warlords: keep which Type?", races, x.Self);
                    foreach (var m in x.D.P(p).MonstersOnField.Where(m => m.FaceUp && x.D.Race(m) != races[i]).ToList()) await x.D.ToGrave(m, x.Self);
                }
            };
        }
        Spell("Rock Bombardment", async x =>
        {
            var c = await x.Pick(x.My.Deck.Where(c => c.Data.IsMonster && c.Data.Race == "Rock"), "Send which Rock monster to the GY?");
            x.D.Shuffle(x.Me);
            if (c != null) { await x.D.ToGrave(c, x.Self); await x.D.Damage(x.Opp, 500, x.Self); }
        }, x => x.My.Deck.Any(c => c.Data.IsMonster && c.Data.Race == "Rock"));
        {
            var s = S("Rocket Hermos Cannon");
            s.SummonOk = (x, by, k) => by?.Name == "The Claw of Hermos";
            s.On = async (x, e) =>
            {
                if (!e.Is(EvKind.Summoned, x.Self) || x.My.FreeSpellZones == 0) return;
                var t = await x.Pick(x.Targetable(x.D.FaceUpMonsters.Where(m => m != x.Self)), "Equip Rocket Hermos Cannon to which monster?");
                if (t == null) return;
                x.Self.ResetFieldState();
                x.D.Place(x.Self, x.Me, Loc.Spell); x.Self.FaceUp = true;
                await x.D.Equip(x.Self, t);
                x.Self.Vars["asspell"] = 1;
            };
            s.Allow = (x, q, c) => q is Q.DoubleAttack or Q.Piercing && x.Self.Loc == Loc.Spell && c != null && c == x.Self.EquippedTo;
        }
        {
            var s = Spell("Rod of Silence - Kay'est", async x => { if (x.T0 is { } t && t.Valid() && t.FaceUp) await x.D.Equip(x.Self, t); },
                x => x.Targetable(x.D.FaceUpMonsters).Any(), async x => await x.Target(x.D.FaceUpMonsters, "Equip to which monster?"));
            s.Stats = (x, c, st) => { if (c == x.Self.EquippedTo) st.Add(0, 500); };
            s.On = async (x, e) =>
            {
                if (e.Kind == EvKind.Activated && x.Self.OnField && x.Self.FaceUp && x.Self.EquippedTo is { } m && e.Link is { } l && l.Card != x.Self && l.Card.Data.IsSpell && l.X.T.Contains(m))
                    await x.D.Negate(l);
            };
        }
        S("Rod of the Mind's Eye").BattleDamage = (x, src, to, dmg) => src == x.Self.EquippedTo && x.Self.OnField && to == x.Opp && dmg > 0 ? 1000 : dmg;
        Spell("Rod of the Mind's Eye", async x => { if (x.T0 is { } t && t.Valid() && t.FaceUp) await x.D.Equip(x.Self, t); },
            x => x.Targetable(x.D.FaceUpMonsters).Any(), async x => await x.Target(x.D.FaceUpMonsters, "Equip to which monster?"));
        Spell("Roll Out!", async x =>
        {
            if (x.T0 is not { Loc: Loc.Grave } u || x.My.FreeSpellZones == 0) return;
            var host = await x.Pick(x.MyMonsters().FaceUp().Where(m => UnionHostOk(x, u, m) && !x.D.EquipsOf(m).Any(e => e.Var("union") == 1)), $"Equip {u.Name} to which monster?");
            if (host == null) return;
            x.D.Place(u, x.Me, Loc.Spell); u.FaceUp = true;
            await x.D.Equip(u, host);
            u.Vars["union"] = 1;
        }, x => x.My.FreeSpellZones > 1 && x.Targetable(x.My.Grave.Where(c => c.Data.IsMonster && c.Data.Desc.Contains("Union") && x.MyMonsters().Any(m => m.FaceUp && UnionHostOk(x, c, m)))).Any(),
        async x => await x.Target(x.My.Grave.Where(c => c.Data.IsMonster && c.Data.Desc.Contains("Union") && x.MyMonsters().Any(m => m.FaceUp && UnionHostOk(x, c, m))), "Target which Union monster?"));
        S("Roll Out!").Note = "The monster it equips to is read from the Union monster's text (its named monster or Type).";
        Spell("Rope of Life", async x =>
        {
            if (x.T0 is not { Loc: Loc.Grave } t || !await x.D.SpecialSummon(t, x.Me, await x.D.ChoosePosition(x.Me, t), by: x.Self)) return;
            t.Vars["modatk"] = t.Var("modatk") + 800; x.D.Seq++;
        }, x => x.Ev is { Kind: EvKind.Destroyed, Battle: true, Card: { } c } && c.Loc == Loc.Grave && c.Owner == x.Me && x.My.Hand.Any(h => h != x.Self) && x.D.FreeMonsterZones(x.Me) > 0 && x.D.CanSpecialSummon(c, x.Me, by: x.Self),
        async x => { await x.D.Discard(x.Me, x.My.Hand.Where(h => h != x.Self).ToList(), x.Self); x.T.Add(x.Ev!.Card!); });
        Spell("Roulette Spider", async x =>
        {
            if (x.D.Attacker is not { Loc: Loc.Monster } a) return;
            int r = x.D.Die(x.Me, x.Self);
            switch (r)
            {
                case 1: x.D.Pay(x.Me, x.My.Lp / 2, x.Self); break;
                case 2: x.D.AttackTarget = null; break;
                case 3: { var t = await x.Pick(x.MyMonsters(), "Change the attack target to which of your monsters?"); if (t != null) x.D.AttackTarget = t; break; }
                case 4:
                    {
                        var t = await x.Pick(x.OppMonsters().Where(m => m != a), "Change the attack target to which of your opponent's monsters?");
                        if (t != null) x.D.AttackTarget = t;
                        break;
                    }
                case 5: x.D.AttackNegated = true; await x.D.Damage(x.Opp, x.Atk(a), x.Self); break;
                case 6: await x.D.Destroy(a, x.Self); break;
            }
        }, AttackOnMe);
        S("Roulette Spider").Note = "Result 4 makes the attacker battle one of its own side's monsters.";
        {
            var s = S("Royal Magical Library");
            s.On = async (x, e) => { if (e.Kind == EvKind.Resolved && Up(x) && e.Link is { IsCardActivation: true } l && l.Card.Data.IsSpell && x.Self.Counters("spell") < 3) AddCounter(x, x.Self, 1, "spell"); };
            s.Can = x => Up(x) && x.Self.Counters("spell") >= 3;
            s.Cost = async x => AddCounter(x, x.Self, -3, "spell");
            s.Run = async x => await x.D.Draw(x.Me, 1);
        }
        {
            static bool Royal(Card c) => c.Data.IsMonster && Mentions(c.Data, "Queen's Knight") && Mentions(c.Data, "King's Knight") && Mentions(c.Data, "Jack's Knight");
            string[] knights = { "Queen's Knight", "King's Knight", "Jack's Knight" };
            IEnumerable<Card> Pool(Ctx x) => x.My.Hand.Concat(x.MyMonsters().Where(m => m.FaceUp));
            Spell("Royal Straight", async x =>
            {
                var l = x.My.Hand.Concat(x.My.Deck).Concat(x.My.Extra).Concat(x.My.Grave).Where(c => Royal(c) && x.D.CanSpecialSummon(c, x.Me, by: x.Self)).ToList();
                await SummonPick(x, l, "Special Summon which monster?");
                x.D.Shuffle(x.Me);
            }, x => knights.All(n => Pool(x).Any(c => Scripts.NameOf(c) == n)) && x.My.Hand.Concat(x.My.Deck).Concat(x.My.Extra).Concat(x.My.Grave).Any(c => Royal(c) && x.D.CanSpecialSummon(c, x.Me, by: x.Self, ignoreZones: true)),
            async x => { foreach (var n in knights) { var c = await x.Pick(Pool(x).Where(c => Scripts.NameOf(c) == n), $"Send {n} to the GY"); if (c != null) await x.D.ToGrave(c, x.Self); } });
        }
        {
            var s = S("Royal Straight Slasher");
            s.SummonOk = (x, by, k) => by?.Name == "Royal Straight";
            s.OncePerTurn = false;
            static List<Card>? Five(Ctx x) { var pool = x.My.Hand.Concat(x.My.Deck).Where(c => c.Data.IsMonster).ToList(); return Enumerable.Range(1, 5).All(l => pool.Any(c => c.Data.Level == l)) ? pool : null; }
            s.Can = x => Up(x) && Hopt(x, "1") && new[] { "Queen's Knight", "King's Knight", "Jack's Knight" }.All(n => x.My.Grave.Any(c => c.Name == n)) && Five(x) != null;
            s.Cost = async x => UseHopt(x, "1");
            s.Run = async x =>
            {
                var pool = Five(x);
                if (pool == null) return;
                var sent = new List<Card>();
                for (int l = 1; l <= 5; l++) { var c = await x.Pick(pool.Where(c => c.Data.Level == l && c.Loc is Loc.Hand or Loc.Deck), $"Send a Level {l} monster to the GY"); if (c != null) sent.Add(c); }
                foreach (var c in sent) await x.D.ToGrave(c, x.Self);
                x.D.Shuffle(x.Me);
                if (sent.Count == 5 && sent.All(c => c.Loc == Loc.Grave)) await x.D.Destroy(x.Their.FieldCards.ToList(), x.Self);
            };
            var on = s.On;
            s.On = async (x, e) =>
            {
                if (!e.Is(EvKind.Destroyed, x.Self) || !e.Battle || !Hopt(x, "2")) return;
                var l = x.Targetable(x.My.Grave.Where(c => c != x.Self && c.Data.IsMonster && c.Data.Attribute == "LIGHT" && c.Data.Race == "Warrior" && x.D.CanSpecialSummon(c, x.Me, by: x.Self))).ToList();
                int max = Math.Min(3, Math.Min(l.Count, x.D.FreeMonsterZones(x.Me)));
                if (max == 0 || !await x.Ask("Special Summon up to 3 LIGHT Warriors from your GY?")) return;
                UseHopt(x, "2");
                foreach (var c in await x.PickN(l, 1, max, $"Special Summon up to {max}")) await x.D.SpecialSummon(c, x.Me, await x.D.ChoosePosition(x.Me, c), by: x.Self);
            };
        }
        Spell("Royal Tribute", async x =>
        {
            foreach (var p in new[] { x.Me, x.Opp })
            {
                var l = x.D.P(p).Hand.Where(c => c.Data.IsMonster).ToList();
                if (l.Count > 0) await x.D.Discard(p, l, x.Self);
            }
        }, x => x.My.FieldZone is { FaceUp: true, Name: "Necrovalley" });
        Spell("Ruthless Denial", async x =>
        {
            var m = await x.Pick(x.MyMonsters(), "Send which of your monsters to the GY?");
            if (m == null) return;
            await x.D.ToGrave(m, x.Self);
            if (x.Their.Hand.Count > 0) await x.D.ToGrave(x.Their.Hand[x.D.Rng.Int(x.Their.Hand.Count)], x.Self);
        }, x => x.MyMonsters().Any());
        S("Ryu Kokki").On = async (x, e) =>
        {
            if (e.Kind != EvKind.AfterDamage || !Up(x)) return;
            var o = e.Card == x.Self ? e.Other : e.Other == x.Self ? e.Card : null;
            if (o is { Loc: Loc.Monster } && x.D.Race(o) is "Warrior" or "Spellcaster") await x.D.Destroy(o, x.Self);
        };
        {
            var s = Spell("Sabatiel - The Philosopher's Stone", async x => await x.Search(c => c.Data.IsSpell && (c.In("Polymerization") || c.In("Fusion")), "Add which Polymerization/Fusion Spell?"),
                x => x.My.Grave.Any(c => c.Data.IsMonster && c.In("Winged Kuriboh")) && x.My.Lp > 1 && x.My.Deck.Any(c => c.Data.IsSpell && (c.In("Polymerization") || c.In("Fusion"))),
                async x => x.D.Pay(x.Me, x.My.Lp / 2, x.Self));
            s.GraveFx = new Script
            {
                Can = x => x.My.Grave.Count(c => c.Name == x.Self.Name) >= 3 && x.Targetable(x.D.FaceUpMonsters).Any(),
                Cost = async x =>
                {
                    foreach (var c in x.My.Grave.Where(c => c.Name == x.Self.Name).Take(3).ToList()) await x.D.Banish(c, x.Self);
                    await x.Target(x.D.FaceUpMonsters, "Which monster gains ATK?");
                },
                Run = async x => { if (x.T0 is { Loc: Loc.Monster, FaceUp: true } t && x.D.FaceUpMonsters.Any()) x.D.Boost(t, x.D.FaceUpMonsters.Max(m => x.Atk(m))); },
            };
        }
        Spell("Sage's Stone", async x => { await SummonPick(x, x.My.Hand.Concat(x.My.Deck).Where(c => c.Name == "Dark Magician"), "Special Summon Dark Magician"); x.D.Shuffle(x.Me); },
            x => x.MyMonsters().Any(m => m.FaceUp && Scripts.NameOf(m) == "Dark Magician Girl") && x.My.Hand.Concat(x.My.Deck).Any(c => c.Name == "Dark Magician"));
        Spell("Sakuretsu Armor", async x => { if (x.D.Attacker is { Loc: Loc.Monster } a) await x.D.Destroy(a, x.Self); }, AttackOnMe);
        Spell("Salvage", async x => { foreach (var t in x.T.Where(t => t.Loc == Loc.Grave).ToList()) await x.D.ToHand(t, x.Self); },
            x => x.Targetable(x.My.Grave.Where(c => c.Data.IsMonster && c.Data.Attribute == "WATER" && c.Data.Atk is >= 0 and <= 1500)).Count() >= 2,
            async x => x.T.AddRange(await x.PickN(x.Targetable(x.My.Grave.Where(c => c.Data.IsMonster && c.Data.Attribute == "WATER" && c.Data.Atk is >= 0 and <= 1500)), 2, 2, "Target 2 WATER monsters")));
        S("Samsara Lotus").On = async (x, e) =>
        {
            if (e.Kind == EvKind.Standby && e.Player == x.Me && Up(x)) await x.D.Damage(x.Me, 1000, x.Self);
            if (e.Kind == EvKind.EndPhase && e.Player == x.Me && x.Self.Loc == Loc.Grave && !x.My.SpellsOnField.Any() && x.D.CanSpecialSummon(x.Self, x.Me, by: x.Self) && await x.Ask("Special Summon Samsara Lotus from your GY?"))
                await x.D.SpecialSummon(x.Self, x.Me, false, by: x.Self);
        };
        {
            var s = S("Sand Gambler");
            s.OncePerTurn = true;
            s.Can = x => Up(x) && x.D.TurnPlayer == x.Me && x.D.Phase is Phase.Main1 or Phase.Main2;
            s.Run = async x =>
            {
                int h = x.D.TossCoins(x.Me, 3, x.Self);
                if (h == 3) await x.D.Destroy(x.OppMonsters().ToList(), x.Self);
                else if (h == 0) await x.D.Destroy(x.MyMonsters().ToList(), x.Self);
            };
        }
        StartDamageKill(S("Sasuke Samurai"), faceDown: true);
        Sure("Regenerating Rose", "Reinforcement of the Army", "Release Restraint", "Reload", "Return Zombie", "Reversal of Fate", "Ritual Weapon", "Rock Bombardment", "Rod of the Mind's Eye", "Royal Magical Library",
            "Royal Tribute", "Ruthless Denial", "Ryu Kokki", "Sage's Stone", "Sakuretsu Armor", "Salvage", "Samsara Lotus", "Sand Gambler", "Sasuke Samurai", "Relieve Monster");
    }
}
