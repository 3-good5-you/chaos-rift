namespace PoC.Engine;

// Official cards, batch 6 (Cyber Angel Idaten .. D.D. Warrior Lady).
public static partial class Scripts
{
    /// A Trap Card Special Summoned as a monster ("This card is also still a Trap").
    internal static async Flow<bool> TrapMonster(Ctx x, int atk, int def, string attr, string race, int level, bool? defense = null)
    {
        var c = x.Self;
        if (!c.OnField || !x.D.CanSpecialSummon(c, x.Me, by: c)) return false;
        var asMon = new CardData { Name = c.Name, Idx = c.Data.Idx, KonamiId = c.Data.KonamiId, Passcode = c.Data.Passcode, Kind = Kind.Monster, Frame = "Effect",
            Atk = atk, Def = def, Attribute = attr, Race = race, Level = level, Desc = c.Data.Desc, Modern = true };
        return await x.D.SpecialSummon(c, x.Me, defense ?? await x.D.ChoosePosition(x.Me, c), by: c, asMonster: asMon);
    }

    /// Cyberdark: "If this card is Normal Summoned: Target 1 Level 3 or lower Dragon monster in your GY; equip it to this card.
    /// Gains ATK equal to its original ATK. If this card would be destroyed by battle, destroy that monster instead."
    static Card? CyberdarkEquip(Ctx x) => x.D.EquipsOf(x.Self).FirstOrDefault(e => e.Var("cyberdark") == x.Self.Uid + 1);
    static async Flow EquipFromGrave(Ctx x, Card dragon)
    {
        if (!x.Self.Valid() || x.My.FreeSpellZones == 0 || dragon.Loc != Loc.Grave) return;
        await x.D.Equip(dragon, x.Self);
        if (dragon.OnField) dragon.Vars["cyberdark"] = x.Self.Uid + 1;
    }
    static Script Cyberdark(string name, int maxLevel = 3, bool onSpecial = false)
    {
        var s = S(name);
        s.Stats = (x, c, st) => { if (c == x.Self && CyberdarkEquip(x) is { } e) st.Add(e.Data.Atk, 0); };
        s.DestroyInstead = async (x, battle) => { if (!battle || CyberdarkEquip(x) is not { } e) return false; await x.D.Destroy(e, x.Self); return true; };
        s.On = async (x, e) =>
        {
            bool fires = onSpecial ? e.Is(EvKind.Summoned, x.Self) && e.Arg is (int)SummonKind.Special or (int)SummonKind.Fusion : e.Is(EvKind.Summoned, x.Self) && e.Summoned(SummonKind.Normal, SummonKind.Tribute);
            if (!fires) return;
            var l = x.Targetable(x.My.Grave.Where(c => c.Data.IsMonster && c.Data.Race == "Dragon" && c.Data.Level <= maxLevel)).ToList();
            if (l.Count == 0) return;
            var d = await x.Pick(l, "Equip which Dragon from your GY?");
            if (d != null) await EquipFromGrave(x, d);
        };
        return s;
    }

    static bool ListsCyberDragon(Card m) => Scripts.NameOf(m) == "Cyber Dragon" || (m.Data.IsFusion && m.Data.Desc.Contains("\"Cyber Dragon\""));

    static void CardsP06(CardDb db)
    {
        S("Cyber Angel Idaten").On = async (x, e) =>
        {
            if (e.Is(EvKind.Summoned, x.Self) && e.Arg == (int)SummonKind.Ritual && x.My.Deck.Concat(x.My.Grave).Any(c => c.Data.IsSpell && c.Data.Is("Ritual")) && await x.Ask("Add 1 Ritual Spell from your Deck or GY?"))
            {
                var c = await x.Pick(x.My.Deck.Concat(x.My.Grave).Where(c => c.Data.IsSpell && c.Data.Is("Ritual")), "Add which Ritual Spell?");
                bool deck = c?.Loc == Loc.Deck;
                if (c != null) await x.D.ToHand(c, x.Self);
                if (deck) x.D.Shuffle(x.Me);
            }
            if (e.Is(EvKind.ToGrave, x.Self) && e.From is Loc.Monster or Loc.Hand && x.D.Flags.GetValueOrDefault($"tributed{x.Self.Uid}") == x.D.Turn && x.MyMonsters().Any(m => m.FaceUp && m.Data.IsRitual)
                && await x.Ask("Your Ritual Monsters gain 1000 ATK/DEF?"))
                foreach (var m in x.MyMonsters().Where(m => m.FaceUp && m.Data.IsRitual)) { m.Vars["modatk"] = m.Var("modatk") + 1000; m.Vars["moddef"] = m.Var("moddef") + 1000; }
        };
        {
            var s = S("Cyber Barrier Dragon");
            s.SummonOk = (x, by, k) => by?.Name == "Attack Reflector Unit";
            s.On = async (x, e) =>
            {
                if (e.Kind == EvKind.AttackDeclared && e.Player == x.Opp && Up(x) && x.Self.Attack && x.Self.Var("barrierturn") != x.D.Turn)
                { x.Self.Vars["barrierturn"] = x.D.Turn; x.D.AttackNegated = true; }
            };
        }
        {
            var s = S("Cyber Blader");
            int Opp(Ctx x) => x.OppMonsters().Count();
            s.Forbid = (x, q, c) => (q == Q.DestroyByBattle && c == x.Self && Opp(x) == 1)
                || (q == Q.Negate && c != null && c.Controller == x.Opp && Opp(x) == 3 && (!c.Data.IsMonster || c.Data.IsEffect));
            s.Stats = (x, c, st) => { if (c == x.Self && Opp(x) == 2) st.Add(st.BaseAtk + st.Atk, 0); };
        }
        {
            var s = S("Cyber Dragon");
            s.ProcCan = x => !x.MyMonsters().Any() && x.OppMonsters().Any();
            s.Proc = async x => await x.D.SpecialSummon(x.Self, x.Me, await x.D.ChoosePosition(x.Me, x.Self), by: x.Self);
        }
        S("Cyber End Dragon").Allow = (x, q, c) => q == Q.Piercing && c == x.Self;
        S("Cyber Esper").On = async (x, e) => { if (e.Kind == EvKind.Drew && e.Player == x.Opp && Up(x) && x.Self.Attack) x.D.Reveal(x.Me, e.Cards!); };
        {
            var s = S("Cyber Gymnast");
            s.OncePerTurn = true;
            s.Can = x => Up(x) && x.My.Hand.Count > 0 && x.Targetable(x.OppMonsters().Where(m => m.FaceUp && m.Attack)).Any();
            s.Cost = async x =>
            {
                await x.D.Discard(x.Me, await x.PickN(x.My.Hand, 1, 1, "Discard 1 card"), x.Self);
                await x.Target(x.OppMonsters().Where(m => m.FaceUp && m.Attack), "Destroy which face-up Attack Position monster?");
            };
            s.Run = async x => { if (x.T0 is { } t && t.Valid()) await x.D.Destroy(t, x.Self); };
        }
        {
            var s = S("Cyber Kirin");
            s.Speed = 2; s.AnyTurn = true;
            s.Can = x => Up(x);
            s.Cost = async x => await x.D.Tribute(x.Self, x.Self);
            s.Run = async x => { var d = x.D; int me = x.Me; d.Linger(d.Turn, (q, c, p) => q == Q.TakeEffectDamage && p == me); };
            s.Note = "Used as a Quick Effect (in response to burn), the way it is played; tell me if it should only be used in your Main Phase.";
        }
        {
            var s = S("Cyber Laser Dragon");
            s.SummonOk = (x, by, k) => by?.Name == "Photon Generator Unit";
            s.OncePerTurn = true;
            s.Can = x => Up(x) && x.AllMonsters().Any(m => m != x.Self && m.FaceUp && (x.Atk(m) >= x.Atk(x.Self) || x.Def(m) >= x.Atk(x.Self)));
            s.Run = async x =>
            {
                var t = await x.Pick(x.AllMonsters().Where(m => m != x.Self && m.FaceUp && (x.Atk(m) >= x.Atk(x.Self) || x.Def(m) >= x.Atk(x.Self))), "Destroy which monster?");
                if (t != null) await x.D.Destroy(t, x.Self);
            };
        }
        {
            var s = S("Cyber Ogre");
            static Card? Ogre(Ctx x) => x.Ev is { Kind: EvKind.AttackDeclared or EvKind.DamageCalc } e
                ? new[] { e.Card, e.Other }.FirstOrDefault(m => m != null && m.Controller == x.Me && m.FaceUp && Scripts.NameOf(m) == "Cyber Ogre") : null;
            s.AddFx(f =>
            {
                f.Label = "Negate the battle"; f.FromHand = true; f.Speed = 2; f.AnyTurn = true;
                f.Can = x => Ogre(x) != null;
                f.Cost = async x => { x.T.Add(Ogre(x)!); await x.D.Discard(x.Me, new[] { x.Self }, x.Self); };
                f.Run = async x =>
                {
                    if (x.T0 is not { } o || !o.Valid()) return;
                    x.D.AttackNegated = true;
                    o.Vars["modatk"] = o.Var("modatk") + 2000; x.D.Seq++;
                    var d = x.D; int turn = d.Turn; bool done = false;
                    d.Watch(int.MaxValue, async ev =>
                    {
                        if (done || ev.Kind != EvKind.AfterDamage || (ev.Card != o && ev.Other != o)) return;
                        done = true;
                        if (o.Loc == Loc.Monster) o.Vars["modatk"] = o.Var("modatk") - 2000;
                    });
                };
            });
        }
        S("Cyber Ogre 2").CalcAtk = (x, c, vs, atk) => c == x.Self && x.D.Attacker == x.Self && vs != null ? atk + x.Atk(vs) / 2 : atk;
        S("Cyber Ouroboros").On = async (x, e) =>
        {
            if (e.Is(EvKind.Banished, x.Self) && x.My.Hand.Count > 0 && await x.Ask("Send 1 card from your hand to the GY to draw 1 card?"))
            {
                await x.D.ToGrave((await x.Pick(x.My.Hand, "Send which card to the GY?"))!, x.Self);
                await x.D.Draw(x.Me, 1);
            }
        };
        S("Cyber Petit Angel").On = async (x, e) =>
        {
            static bool Ok(Card c) => (c.Data.IsMonster && c.In("Cyber Angel")) || c.Name == "Machine Angel Ritual";
            if (e.Is(EvKind.Summoned, x.Self) && Hopt(x) && x.My.Deck.Any(Ok) && await x.Ask("Add a Cyber Angel monster or Machine Angel Ritual from your Deck?"))
            { UseHopt(x); await x.Search(Ok, "Add which card?"); }
        };
        S("Cyber Phoenix").On = async (x, e) =>
        {
            if (e.Kind == EvKind.Activated && e.Link is { } l && !l.Card.Data.IsMonster && l.X.T.Count == 1 && l.X.T0 is { Loc: Loc.Monster, FaceUp: true } t
                && t.Controller == x.Me && x.IsRace(t, "Machine") && Up(x) && x.Self.Attack)
                l.Negated = true;
            if (e.Is(EvKind.Destroyed, x.Self) && e.Battle && x.Self.Loc == Loc.Grave && await x.Ask("Draw 1 card (Cyber Phoenix)?")) await x.D.Draw(x.Me, 1);
        };
        S("Cyber Prima").On = async (x, e) => { if (e.Is(EvKind.Summoned, x.Self) && e.Arg == (int)SummonKind.Tribute) await x.D.Destroy(x.AllSpells().Where(c => c.FaceUp && c.Data.IsSpell).ToList(), x.Self); };
        S("Cyber Raider").On = async (x, e) =>
        {
            if (!e.Is(EvKind.Summoned, x.Self)) return;
            var eq = x.Targetable(x.D.FieldCards.Where(c => c.FaceUp && c.EquippedTo != null)).ToList();
            if (eq.Count == 0) return;
            int mode = await x.D.ChooseText(x.Me, "Cyber Raider", new[] { "Destroy 1 Equip Card", "Equip 1 Equip Card to Cyber Raider" }, x.Self);
            var t = await x.Pick(eq, mode == 0 ? "Destroy which Equip Card?" : "Equip which Equip Card to Cyber Raider?");
            if (t == null) return;
            if (mode == 0) { await x.D.Destroy(t, x.Self); return; }
            if (!x.Self.Valid()) return;
            if (t.Controller != x.Me) { if (x.My.FreeSpellZones == 0) return; t.EquippedTo = null; x.D.Place(t, x.Me, Loc.Spell); t.FaceUp = true; }
            await x.D.Equip(t, x.Self);
        };
        {
            var s = Spell("Cyber Shadow Gardna", async x => { await TrapMonster(x, 0, 0, "EARTH", "Machine", 4); },
                x => x.D.TurnPlayer == x.Opp && x.D.Phase is Phase.Main1 or Phase.Main2 && x.D.FreeMonsterZones(x.Me) > 0);
            s.On = async (x, e) =>
            {
                if (x.Self.Loc != Loc.Monster) return;
                if (e.Kind == EvKind.AttackDeclared && e.Other == x.Self && e.Player == x.Opp && e.Card is { } a)
                {
                    x.Self.Vars["baseatk"] = x.Atk(a) + 1; x.Self.Vars["baseatkturn"] = x.D.Turn;
                    x.Self.Vars["basedef"] = x.Def(a) + 1; x.Self.Vars["basedefturn"] = x.D.Turn; x.D.Seq++;
                }
                if (e.Kind == EvKind.EndPhase && e.Player == x.Opp && x.Self.FaceUp && x.My.FreeSpellZones > 0)
                {
                    x.Self.ResetFieldState();
                    x.D.Place(x.Self, x.Me, Loc.Spell);
                    x.Self.FaceUp = false; x.Self.SetTurn = x.D.Turn;
                    await x.D.Raise(new Ev { Kind = EvKind.Set, Card = x.Self, Player = x.Me });
                }
            };
        }
        S("Cyber Summon Blaster").On = async (x, e) =>
        {
            if (e.Kind == EvKind.Summoned && e.Arg is (int)SummonKind.Special or (int)SummonKind.Fusion or (int)SummonKind.Ritual && e.Card!.Valid() && x.IsRace(e.Card, "Machine") && x.Self.OnField && x.Self.FaceUp)
                await x.D.Damage(x.Opp, 300, x.Self);
        };
        S("Cyber Tutu").Allow = (x, q, c) => q == Q.DirectAttack && c == x.Self && x.OppMonsters().Any() && x.OppMonsters().All(m => m.FaceUp && x.Atk(m) > x.Atk(x.Self));
        S("Cyber Twin Dragon").Allow = (x, q, c) => q == Q.DoubleAttack && c == x.Self;
        {
            var s = S("Cyber Valley");
            s.On = async (x, e) =>
            {
                if (e.Kind == EvKind.AttackDeclared && e.Other == x.Self && e.Player == x.Opp && Up(x) && await x.Ask("Banish Cyber Valley to draw 1 card and end the Battle Phase?"))
                {
                    await x.D.Banish(x.Self, x.Self);
                    await x.D.Draw(x.Me, 1);
                    x.D.AttackNegated = true; x.D.TurnFlags.Add("endbattle");
                }
            };
            s.AddFx(f =>
            {
                f.Label = "Banish 2, draw 2";
                f.Can = x => Up(x) && x.Targetable(x.MyMonsters().FaceUp().Where(m => m != x.Self)).Any();
                f.Cost = async x => await x.Target(x.MyMonsters().FaceUp().Where(m => m != x.Self), "Banish which face-up monster with Cyber Valley?");
                f.Run = async x =>
                {
                    if (x.T0 is { } t && t.Valid()) await x.D.Banish(t, x.Self);
                    if (x.Self.Valid()) await x.D.Banish(x.Self, x.Self);
                    await x.D.Draw(x.Me, 2);
                };
            });
            s.AddFx(f =>
            {
                f.Label = "GY card to the Deck top";
                f.Can = x => Up(x) && x.My.Hand.Count > 0 && x.Targetable(x.My.Grave).Any();
                f.Cost = async x => await x.Target(x.My.Grave, "Place which GY card on top of your Deck?");
                f.Run = async x =>
                {
                    if (x.Self.Valid()) await x.D.Banish(x.Self, x.Self);
                    var h = await x.Pick(x.My.Hand, "Banish 1 card from your hand");
                    if (h != null) await x.D.Banish(h, x.Self);
                    if (x.T0 is { Loc: Loc.Grave } t) await x.D.ToDeck(t, top: true);
                };
            });
        }
        {
            var s = Cyberdark("Cyberdark Dragon", maxLevel: 99, onSpecial: true);
            var stats = s.Stats!;
            s.Stats = (x, c, st) => { stats(x, c, st); if (c == x.Self) st.Add(100 * x.My.Grave.Count(g => g.Data.IsMonster), 0); };
        }
        {
            var s = Cyberdark("Cyberdark Edge");
            s.Allow = (x, q, c) => q == Q.DirectAttack && c == x.Self;
            s.CalcAtk = (x, c, vs, atk) => c == x.Self && x.D.Attacker == x.Self && vs == null && x.Their.MonstersOnField.Any() ? atk / 2 : atk;
        }
        Cyberdark("Cyberdark Horn").Allow = (x, q, c) => q == Q.Piercing && c == x.Self;
        {
            var s = Cyberdark("Cyberdark Keel");
            var on = s.On!;
            s.On = async (x, e) => { await on(x, e); if (e.Kind == EvKind.BattleDestroyedMonster && e.Card == x.Self && e.Other!.Controller == x.Opp) await x.D.Damage(x.Opp, 300, x.Self); };
        }
        {
            static IEnumerable<Card> Pool(Ctx x) => x.My.Hand.Concat(x.My.Grave).Concat(x.My.MonstersOnField).Concat(x.My.Spells.Where(c => c != null && c.Data.IsMonster).Select(c => c!));
            Spell("Cyberdark Impact!", async x => await x.D.FusionSummon(x.Me, x.Self, pool: Pool(x), resultOk: c => c.Name == "Cyberdark Dragon", matsTo: Loc.Deck),
                x => x.D.FusionOptions(x.Me, pool: Pool(x), resultOk: c => c.Name == "Cyberdark Dragon").Any());
        }
        Spell("Cybernetic Fusion Support", async x => x.D.TurnFlags.Add($"fusionsupport{x.Me}"), x => Hopt(x), async x => { UseHopt(x); x.D.Pay(x.Me, x.My.Lp / 2); });
        {
            var s = S("Cybernetic Hidden Technology");
            s.AddFx(f =>
            {
                f.Label = "Destroy the attacker"; f.Speed = 2;
                f.Can = x => AttackOnMe(x) && x.MyMonsters().Any(m => m.FaceUp && ListsCyberDragon(m));
                f.Cost = async x =>
                {
                    var m = await x.Pick(x.MyMonsters().Where(m => m.FaceUp && ListsCyberDragon(m)), "Send which Cyber Dragon (or its Fusion) to the GY?");
                    if (m != null) await x.D.ToGrave(m, x.Self);
                    x.T.Add(x.Ev!.Card!);
                };
                f.Run = async x => { if (x.T0 is { } a && a.Valid()) await x.D.Destroy(a, x.Self); };
            });
        }
        Spell("Cybernetic Zone", async x =>
        {
            if (x.T0 is not { } m || !m.Valid() || !m.FaceUp) return;
            int owner = m.Owner;
            await x.D.Banish(m, x.Self);
            if (m.Loc != Loc.Banished) return;
            var d = x.D;
            int nextStandby = d.TurnPlayer == owner ? d.Turn + 2 : d.Turn + 1;
            d.Later(d.Turn, Phase.End, async () =>
            {
                if (m.Loc != Loc.Banished || d.FreeMonsterZones(owner) == 0 || !d.Place(m, owner, Loc.Monster)) return;
                m.FaceUp = true; m.Defense = false;
                m.Vars["modatk"] = d.Atk(m);   // double its ATK
                d.Seq++;
                d.Later(nextStandby, Phase.Standby, async () => { if (m.Loc == Loc.Monster) await d.Destroy(m); });
            });
        }, x => x.Targetable(x.MyMonsters().FaceUp().Where(m => m.Data.IsFusion && x.IsRace(m, "Machine"))).Any(),
        async x => await x.Target(x.MyMonsters().FaceUp().Where(m => m.Data.IsFusion && x.IsRace(m, "Machine")), "Banish which Machine Fusion Monster until the End Phase?"));
        {
            var s = S("Cyclone Boomerang");
            s.EquipOk = (x, m) => Scripts.NameOf(m) == "Elemental HERO Wildheart";
            s.Stats = (x, c, st) => { if (c == x.Self.EquippedTo) st.Add(500, 0); };
            s.On = async (x, e) =>
            {
                if (e.Is(EvKind.Equipped, x.Self)) Tie(x, e.Other!);
                if (e.Kind == EvKind.Destroyed && !e.Battle && e.Card == TiedMonster(x) && e.Card!.Loc == Loc.Grave && x.Self.Loc == Loc.Grave)
                {
                    x.D.Flags.Remove($"tie{x.Self.Uid}");
                    var l = x.AllSpells().ToList();
                    await x.D.Destroy(l, x.Self);
                    await x.D.Damage(x.Opp, 100 * l.Count(c => !c.OnField), x.Self);
                }
            };
        }
        {
            var s = Spell("D - Burst", async x =>
            {
                if (x.T0 is not { OnField: true } t) return;
                await x.D.Destroy(t, x.Self);
                if (t.OnField) return;
                await x.D.Draw(x.Me, 1);
                var l = x.My.Hand.Concat(x.My.Grave).Concat(x.My.Banished).Where(c => c.Data.IsMonster && c.In("Destiny HERO")).ToList();
                if (x.MyMonsters().Any(m => m.FaceUp && m.In("Destiny HERO")) && l.Count > 0 && await x.Ask("Special Summon a Destiny HERO from your hand, GY or banishment?"))
                    await SummonPick(x, l, "Special Summon which Destiny HERO?");
            }, x => Hopt(x) && x.Targetable(x.Spells(x.Me).Where(c => c.FaceUp && c.Data.IsSpell && c != x.Self)).Any(),
            async x => { UseHopt(x); await x.Target(x.Spells(x.Me).Where(c => c.FaceUp && c.Data.IsSpell && c != x.Self), "Destroy which face-up Spell you control?"); });
            s.On = async (x, e) =>
            {
                if (e.Kind == EvKind.AfterDamage && x.Self.Loc == Loc.Grave && e.Card is { } a && a.Controller == x.Me && a.Valid()
                    && (x.D.EquipsOf(a).Any() || Scripts.NameOf(a) == "Destiny HERO - Dogma") && await x.Ask($"Banish D - Burst so {a.Name} can attack again?"))
                {
                    await x.D.Banish(x.Self, x.Self);
                    a.Vars["maxatk"] = a.Attacks + 1; a.Vars["maxatkturn"] = x.D.Turn;
                }
            };
        }
        {
            var s = Spell("D - Chain", async x =>
            {
                if (x.T0 is not { } t || !t.Valid() || !t.FaceUp) return;
                await x.D.Equip(x.Self, t);
                x.Self.Vars["stay"] = 1;
            }, x => x.Targetable(x.MyMonsters().FaceUp().Where(m => m.In("Destiny HERO"))).Any(),
            async x => await x.Target(x.MyMonsters().FaceUp().Where(m => m.In("Destiny HERO")), "Equip D - Chain to which Destiny HERO?"));
            s.Stats = (x, c, st) => { if (c == x.Self.EquippedTo) st.Add(500, 0); };
            s.On = async (x, e) => { if (e.Kind == EvKind.BattleDestroyedMonster && e.Card == x.Self.EquippedTo && e.Other!.Controller == x.Opp && e.Other.Loc == Loc.Grave) await x.D.Damage(x.Opp, 500, x.Self); };
        }
        Spell("D - Counter", async x => { if (x.Ev?.Card is { } a && a.Valid()) await x.D.Destroy(a, x.Self); },
            x => x.Ev is { Kind: EvKind.AttackDeclared } e && e.Player == x.Opp && e.Other is { } t && t.Controller == x.Me && t.FaceUp && t.In("Destiny HERO"));
        {
            static bool Plasma(Ctx x) => x.MyMonsters().Any(m => m.FaceUp && Scripts.NameOf(m) == "Destiny HERO - Plasma");
            var s = Spell("D - Force", async x =>
            {
                x.Self.Vars["stay"] = 1;
                if (x.My.Deck.Concat(x.My.Grave).Any(c => c.Name == "Destiny HERO - Plasma") && await x.Ask("Add Destiny HERO - Plasma from your Deck or GY?"))
                {
                    var c = await x.Pick(x.My.Deck.Concat(x.My.Grave).Where(c => c.Name == "Destiny HERO - Plasma"), "Add which?");
                    bool deck = c?.Loc == Loc.Deck;
                    if (c != null) await x.D.ToHand(c, x.Self);
                    if (deck) x.D.Shuffle(x.Me);
                }
            });
            s.On = async (x, e) => { if (e.Kind == EvKind.DrawPhase && e.Player == x.Me && Plasma(x)) x.D.TurnFlags.Add($"skipdraw{x.Me}"); };
            s.Forbid = (x, q, c) => Plasma(x) && c != null && (
                (q == Q.Target && c.Controller == x.Me && x.D.QueryPlayer == x.Opp)
                || (q == Q.DestroyByEffect && Scripts.NameOf(c) == "Destiny HERO - Plasma" && c.Controller == x.Me && x.D.Destroyer?.Controller == x.Opp));
            s.Allow = (x, q, c) => q == Q.DoubleAttack && Plasma(x) && c != null && c.Controller == x.Me && Scripts.NameOf(c) == "Destiny HERO - Plasma";
            s.Stats = (x, c, st) => { if (Plasma(x) && c.Controller == x.Me && Scripts.NameOf(c) == "Destiny HERO - Plasma") st.Add(100 * x.D.Players.Sum(p => p.Grave.Count(g => g.Data.IsMonster)), 0); };
        }
        {
            var s = Spell("D - Formation", async x => x.Self.Vars["stay"] = 1);
            s.Run = null;
            s.On = async (x, e) =>
            {
                if (!x.Self.OnField || !x.Self.FaceUp) return;
                if (e.Kind == EvKind.Destroyed && e.From == Loc.Monster && e.Card!.In("Destiny HERO") && x.D.LeftFieldLog.LastOrDefault(l => l.c == e.Card) is { c: not null } l && l.controller == x.Me && l.faceUp)
                    AddCounter(x, x.Self, 1, "d");
                if (e.Kind == EvKind.Summoned && e.Player == x.Me && e.Arg != (int)SummonKind.Flip && x.D.Phase is Phase.Main1 or Phase.Main2 && x.Self.Counters("d") >= 2)
                {
                    string name = e.Card!.Name;
                    var l2 = x.My.Deck.Concat(x.My.Grave).Where(c => c.Name == name).ToList();
                    if (l2.Count == 0 || !await x.Ask($"Send D - Formation to the GY to add up to 2 \"{name}\"?")) return;
                    await x.D.ToGrave(x.Self, x.Self);
                    foreach (var c in await x.PickN(l2, 1, 2, $"Add up to 2 {name}")) await x.D.ToHand(c, x.Self);
                    x.D.Shuffle(x.Me);
                }
            };
        }
        Spell("D - Fortune", async x => { x.D.AttackNegated = true; x.D.TurnFlags.Add("endbattle"); },
            x => x.Ev is { Kind: EvKind.AttackDeclared, Other: null } e && e.Player == x.Opp && x.My.Grave.Any(c => c.Data.IsMonster && c.In("Destiny HERO")),
            async x => { var c = await x.Pick(x.My.Grave.Where(c => c.Data.IsMonster && c.In("Destiny HERO")), "Banish 1 Destiny HERO from your GY"); if (c != null) await x.D.Banish(c, x.Self); });
        {
            var s = Spell("D - Shield", async x =>
            {
                if (x.T0 is not { } t || !t.Valid() || !t.FaceUp) return;
                await x.D.ChangePosition(t, true);
                await x.D.Equip(x.Self, t);
                x.Self.Vars["stay"] = 1;
            }, x => x.Ev is { Kind: EvKind.AttackDeclared } e && e.Player == x.Opp && e.Other is { } t && t.Controller == x.Me && t.FaceUp && t.Attack && t.In("Destiny HERO"),
            async x => x.T.Add(x.Ev!.Other!));
            s.Forbid = (x, q, c) => q == Q.DestroyByBattle && c != null && c == x.Self.EquippedTo;
        }
        Spell("D - Spirit", async x =>
        {
            if (x.MyMonsters().Any(m => m.FaceUp && m.In("Destiny HERO"))) return;
            await SummonPick(x, x.My.Hand.Where(c => c.Data.IsMonster && c.In("Destiny HERO") && c.Data.Level <= 4), "Special Summon which Destiny HERO?");
        }, x => !x.MyMonsters().Any(m => m.FaceUp && m.In("Destiny HERO")) && x.My.Hand.Any(c => c.Data.IsMonster && c.In("Destiny HERO") && c.Data.Level <= 4));
        Spell("D - Time", async x =>
        {
            int lv = x.Ev!.Card!.Data.Level;
            await x.Search(c => c.Data.IsMonster && c.In("Destiny HERO") && c.Data.Level <= lv, $"Add a Destiny HERO (Level {lv} or lower)");
        }, x => x.Ev is { Kind: EvKind.LeftField } e && e.From == Loc.Monster && e.Card!.In("Elemental HERO")
            && x.D.LeftFieldLog.LastOrDefault(l => l.c == e.Card) is { c: not null } l && l.controller == x.Me && l.faceUp
            && x.My.Deck.Any(c => c.Data.IsMonster && c.In("Destiny HERO") && c.Data.Level <= e.Card.Data.Level));
        SelfBoost("D.3.S. Frog", x => 500 * x.My.Grave.Count(c => c.Name == "Treeborn Frog"));
        S("D.D. Crazy Beast").On = async (x, e) => { if (e.Kind == EvKind.BattleDestroyedMonster && e.Card == x.Self && e.Other!.Loc == Loc.Grave) await x.D.Banish(e.Other, x.Self); };
        Spell("D.D. Designator", async x =>
        {
            string name = x.D.Db.Get(x.N).Name;
            x.D.Reveal(x.Me, x.Their.Hand);
            var hit = x.Their.Hand.FirstOrDefault(c => c.Name == name);
            if (hit != null) await x.D.Banish(hit, x.Self);
            else if (x.My.Hand.Count > 0) await x.D.Banish(x.My.Hand[x.D.Rng.Int(x.My.Hand.Count)], x.Self);
        }, x => x.Their.Hand.Count > 0, async x =>
        {
            var names = x.D.Cards.Where(c => !c.IsToken).Select(c => c.Data).GroupBy(d => d.Name).Select(g => g.First()).OrderBy(d => d.Name).ToList();
            x.N = names[await x.D.ChooseText(x.Me, "Declare 1 card name", names.Select(d => d.Name).ToList(), x.Self)].Idx;
        });
        S("D.D. Designator").Note = "You declare from the names of every card in this duel (both Decks).";
        S("D.D. Scout Plane").On = async (x, e) =>
        {
            if (e.Kind == EvKind.EndPhase && x.Self.Loc == Loc.Banished && x.Self.Var("banishturn") == x.D.Turn && x.Self.Var("scoutturn") != x.D.Turn && x.D.CanSpecialSummon(x.Self, x.Me, by: x.Self))
            { x.Self.Vars["scoutturn"] = x.D.Turn; await x.D.SpecialSummon(x.Self, x.Me, false, by: x.Self); }
        };
        S("D.D. Warrior Lady").On = async (x, e) =>
        {
            if (e.Kind != EvKind.AfterCalc || (e.Card != x.Self && e.Other != x.Self)) return;
            var other = e.Card == x.Self ? e.Other : e.Card;
            if (other == null || other.Controller == x.Me || !other.Valid() || !await x.Ask($"Banish {other.Name} and D.D. Warrior Lady?")) return;
            await x.D.Banish(other, x.Self);
            if (x.Self.Valid()) await x.D.Banish(x.Self, x.Self);
        };
    }
}
