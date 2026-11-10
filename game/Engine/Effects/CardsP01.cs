namespace PoC.Engine;

// Official cards, batch 1 (8-Claws Scorpion .. Ancient Gear Fist), each written against its current text and rulings.
public static partial class Scripts
{
    // ---- shared by the official-card batches

    /// Face-up in a Monster Zone (a monster's continuous effects apply only there, not as a Continuous Spell).
    internal static bool Up(Ctx x) => x.Self.Loc == Loc.Monster && x.Self.FaceUp;

    /// The open window is the opponent activating a card (not an effect): its chain link.
    internal static Link? OppCardActivation(Ctx x, Func<Card, bool> kind) =>
        x.Ev is { Kind: EvKind.Activated, Link: { IsCardActivation: true } l } && l.Player == x.Opp && kind(l.Card) ? l : null;

    /// Pick one of `l` and Special Summon it (position of your choice). False when nothing was summoned.
    internal static async Flow<bool> SummonPick(Ctx x, IEnumerable<Card> l, string text, bool optional = false, bool? defense = null, bool ignore = false)
    {
        var c = await x.Pick(l.Where(c => x.D.CanSpecialSummon(c, x.Me, by: x.Self, ignore: ignore)), text, optional);
        if (c == null) return false;
        return await x.D.SpecialSummon(c, x.Me, defense ?? await x.D.ChoosePosition(x.Me, c), by: x.Self, ignore: ignore);
    }

    /// "Original ATK becomes N" until the end of this turn.
    internal static void SetBaseAtk(Ctx x, Card c, int atk) { c.Vars["baseatk"] = atk + 1; c.Vars["baseatkturn"] = x.D.Turn; x.D.Seq++; }

    /// Put a monster card face-up in your Spell & Trap Zone, treated as a Continuous Spell (Crystal Beasts).
    internal static async Flow<bool> PlaceAsSpell(Ctx x, Card c)
    {
        if (x.My.FreeSpellZones == 0) return false;
        if (c.OnField) c.ResetFieldState();
        if (!x.D.Place(c, x.Me, Loc.Spell)) return false;
        c.FaceUp = true; c.Vars["asspell"] = 1;
        await x.D.Raise(new Ev { Kind = EvKind.Placed, Card = c, Player = x.Me });
        return true;
    }

    static bool AdvancedDark(Ctx x) => x.D.Players.Any(p => p.FieldZone is { FaceUp: true } f && f.Name == "Advanced Dark");

    /// "Advanced Crystal Beast": sent to the GY without "Advanced Dark" in a Field Zone; destroyed face-up in a Monster
    /// Zone, it may stay as a Continuous Spell instead.
    static Script AdvancedCrystalBeast(string name, Func<Ctx, Ev, Flow>? more = null)
    {
        var s = S(name);
        s.ToSpellZone = x => true;
        s.On = async (x, e) =>
        {
            if (Up(x) && !AdvancedDark(x)) { await x.D.ToGrave(x.Self, x.Self); return; }
            if (more != null) await more(x, e);
        };
        return s;
    }

    static List<string>? allRaces;
    static List<string> AllRaces => allRaces ??= CardDb.Shared.ByIdx.Values.Where(d => d.IsMonster && d.Race.Length > 0).Select(d => d.Race).Distinct().OrderBy(r => r).ToList();
    static readonly string[] AllAttributes = { "DARK", "DIVINE", "EARTH", "FIRE", "LIGHT", "WATER", "WIND" };

    static void CardsP01(CardDb db)
    {
        {
            var s = S("8-Claws Scorpion");
            s.OncePerTurn = true;
            s.Note = "Its ATK is 2400 in damage calculation when the monster it attacks was face-down when the attack was declared.";
            s.Run = async x => { if (Up(x)) await x.D.SetFaceDown(x.Self); };
            s.CalcAtk = (x, c, vs, atk) => c == x.Self && x.D.Attacker == x.Self && x.D.TargetWasSet && vs != null ? 2400 : atk;
        }
        {
            var s = Spell("A Deal with Dark Ruler", async x =>
            {
                await SummonPick(x, x.My.Hand.Concat(x.My.Deck).Where(c => c.Name == "Berserk Dragon"), "Special Summon Berserk Dragon");
                x.D.Shuffle(x.Me);
            }, x => x.D.LeftFieldLog.Any(l => l.turn == x.D.Turn && l.controller == x.Me && l.level >= 8 && l.to == Loc.Grave && l.c.Data.IsMonster)
                && x.My.Hand.Concat(x.My.Deck).Any(c => c.Name == "Berserk Dragon" && x.D.CanSpecialSummon(c, x.Me, by: x.Self)));
            s.AlsoIn = new[] { "Archfiend" };
        }
        Spell("A Feather of the Phoenix", async x => { if (x.T0 is { Loc: Loc.Grave } t) await x.D.ToDeck(t, top: true); },
            x => x.My.Hand.Any(c => c != x.Self),   // the discarded card itself may be the target
            async x =>
            {
                await x.D.Discard(x.Me, await x.PickN(x.My.Hand.Where(c => c != x.Self), 1, 1, "Discard 1 card"), x.Self);
                await x.Target(x.My.Grave, "Return which card to the top of your Deck?");
            });
        Spell("A Hero Emerges", async x =>
        {
            if (x.My.Hand.Count == 0) return;
            var c = x.My.Hand[x.D.Rng.Int(x.My.Hand.Count)];   // the opponent's random choice
            x.D.Reveal(x.Opp, c); x.D.Reveal(x.Me, c);
            if (c.Data.IsMonster && x.D.CanSpecialSummon(c, x.Me, by: x.Self)) await x.D.SpecialSummon(c, x.Me, await x.D.ChoosePosition(x.Me, c), by: x.Self);
            else await x.D.ToGrave(c, x.Self);
        }, x => AttackOnMe(x) && x.My.Hand.Any(c => c.Data.IsMonster && c != x.Self));
        {
            var s = S("A Legendary Ocean");
            s.TreatedAs = "Umi"; s.TreatedAlways = true;
            s.Stats = (x, c, st) => { if (st.Attr == "WATER") st.Add(200, 200); };
            s.LevelMod = (x, c) => (c.Loc == Loc.Hand ? c.Data.Attribute : x.D.Attr(c)) == "WATER" ? -1 : 0;
            s.Note = "Its name is always \"Umi\" (cards that need Umi count it).";
        }
        Spell("A Rival Appears!", async x =>
        {
            var opp = await x.Pick(x.OppMonsters().FaceUp().Where(m => RivalMatch(x, m).Any()), "Select 1 face-up monster your opponent controls");
            if (opp != null) await SummonPick(x, RivalMatch(x, opp), $"Special Summon a Level {x.D.Level(opp)} monster from your hand");
        }, x => x.OppMonsters().FaceUp().Any(m => RivalMatch(x, m).Any()));
        static IEnumerable<Card> RivalMatch(Ctx x, Card m) => x.My.Hand.Where(h => h.Data.IsMonster && x.D.Level(h) == x.D.Level(m) && x.D.Level(m) > 0 && x.D.CanSpecialSummon(h, x.Me, by: x.Self));
        Spell("A Wingbeat of Giant Dragon", async x =>
        {
            var d = await x.Pick(x.MyMonsters().FaceUp().Where(m => x.IsRace(m, "Dragon") && x.D.Level(m) >= 5), "Return which Dragon to the hand?");
            if (d == null) return;
            await x.D.ToHand(d, x.Self);
            if (d.Loc is Loc.Hand or Loc.Extra) await x.D.Destroy(x.AllSpells().Where(c => c != x.Self).ToList(), x.Self);
        }, x => x.MyMonsters().FaceUp().Any(m => x.IsRace(m, "Dragon") && x.D.Level(m) >= 5));
        {
            var s = S("A-Team: Trap Disposal Unit");
            s.Speed = 2; s.AnyTurn = true;
            s.Can = x => Up(x) && OppCardActivation(x, c => c.Data.IsTrap) != null;
            s.Cost = async x => await x.D.Tribute(x.Self, x.Self);
            s.Run = async x => await x.D.Negate(x.Ev!.Link!);
        }
        OnBattleDeath("Abaki", async x => { await x.D.Damage(x.Me, 500, x.Self); await x.D.Damage(x.Opp, 500, x.Self); });
        Spell("Abyssal Designator", async x =>
        {
            string race = AllRaces[x.N / 100], attr = AllAttributes[x.N % 100];
            var l = x.Their.Hand.Concat(x.Their.Deck).Where(c => c.Data.IsMonster && c.Data.Race == race && c.Data.Attribute == attr).ToList();
            var c = await x.D.PickOne(x.Opp, l, $"Send 1 {attr} {race} monster from your hand or Deck to the GY", x.Self);
            if (c != null) { bool deck = c.Loc == Loc.Deck; await x.D.ToGrave(c, x.Self); if (deck) x.D.Shuffle(x.Opp); }
        }, x => x.My.Lp > 1000, async x =>
        {
            x.D.Pay(x.Me, 1000);
            int r = await x.D.ChooseText(x.Me, "Declare a Type", AllRaces, x.Self);
            int a = await x.D.ChooseText(x.Me, "Declare an Attribute", AllAttributes, x.Self);
            x.N = r * 100 + a;
        });

        // ---- Advanced Crystal Beasts
        {
            var s = AdvancedCrystalBeast("Advanced Crystal Beast Amber Mammoth", async (x, e) =>
            {
                if (e.Kind == EvKind.AttackDeclared && e.Player == x.Opp && e.Other is { Loc: Loc.Monster } t && t.Controller == x.Me && t.In("Advanced Crystal Beast")
                    && Up(x) && x.Self.Var("atknegturn") != x.D.Turn && await x.Ask("Negate that attack (Amber Mammoth)?"))
                { x.Self.Vars["atknegturn"] = x.D.Turn; x.D.AttackNegated = true; }
            });
            s.Speed = 2; s.AnyTurn = true; s.OncePerTurn = true;
            s.Can = x => Up(x) && x.Ev is { Kind: EvKind.Activated, Link: { } l } && l.Player == x.Opp
                && l.X.T.Any(t => t.Controller == x.Me && t.OnField && (t.In("Advanced Crystal Beast") || t.Name == "Advanced Dark"));
            s.Run = async x => await x.D.Negate(x.Ev!.Link!, destroy: false);
            s.Note = "Negating an attack on an Advanced Crystal Beast is applied at once when you say yes (no chain).";
        }
        {
            var s = AdvancedCrystalBeast("Advanced Crystal Beast Amethyst Cat");
            s.Allow = (x, q, c) => q == Q.DirectAttack && Up(x) && c != null && c.Controller == x.Me && c.In("Advanced Crystal Beast");
            s.BattleDamage = (x, src, to, dmg) => Up(x) && to == x.Opp && src.Controller == x.Me && src.In("Advanced Crystal Beast") && x.D.Attacker == src
                && x.D.AttackTarget == null && x.Their.MonstersOnField.Any() ? dmg / 2 : dmg;
        }
        {
            var s = AdvancedCrystalBeast("Advanced Crystal Beast Cobalt Eagle");
            static Flow DarkSearch(Ctx x) => x.Search(c => c.Name == "Advanced Dark", "Add Advanced Dark to your hand");
            s.AddFx(f => { f.Label = "Search Advanced Dark"; f.FromHand = true; f.Can = x => x.My.Deck.Any(c => c.Name == "Advanced Dark"); f.Cost = async x => await x.D.ToGrave(x.Self, x.Self); f.Run = DarkSearch; });
            s.AddFx(f => { f.Label = "Search Advanced Dark"; f.Can = x => x.My.Deck.Any(c => c.Name == "Advanced Dark"); f.Cost = async x => await x.D.ToGrave(x.Self, x.Self); f.Run = DarkSearch; });
            s.AddFx(f =>
            {
                f.Label = "Return a Crystal Beast"; f.OncePerTurn = true;
                f.Can = x => x.Targetable(x.My.FieldCards.Where(c => c.FaceUp && c.In("Advanced Crystal Beast"))).Any();
                f.Cost = async x => await x.Target(x.My.FieldCards.Where(c => c.FaceUp && c.In("Advanced Crystal Beast")), "Return which Advanced Crystal Beast card?");
                f.Run = async x =>
                {
                    if (x.T0 is not { OnField: true } t) return;
                    if (await x.D.ChooseText(x.Me, $"{t.Name}: where to?", new[] { "To the hand", "Top of the Deck" }, x.Self) == 0) await x.D.ToHand(t, x.Self);
                    else await x.D.ToDeck(t, top: true);
                };
            });
        }
        {
            var s = AdvancedCrystalBeast("Advanced Crystal Beast Emerald Tortoise");
            s.Speed = 2; s.AnyTurn = true; s.OncePerTurn = true;
            s.Can = x => Up(x) && x.Targetable(x.D.FaceUpMonsters).Any();
            s.Cost = async x => await x.Target(x.D.FaceUpMonsters, "Change the battle position of which monster?");
            s.Run = async x => { if (x.T0 is { } t && t.Valid() && t.FaceUp) await x.D.ChangePosition(t, !t.Defense); };
        }
        {
            var s = AdvancedCrystalBeast("Advanced Crystal Beast Ruby Carbuncle");
            s.AddFx(f =>
            {
                f.Label = "Special Summon"; f.InSpellZone = true;
                f.Can = x => x.Self.Var("asspell") == 1 && Hopt(x) && x.D.CanSpecialSummon(x.Self, x.Me, by: x.Self);
                f.Cost = async x => UseHopt(x);
                f.Run = async x =>
                {
                    if (!await x.D.SpecialSummon(x.Self, x.Me, await x.D.ChoosePosition(x.Me, x.Self), by: x.Self)) return;
                    var more = x.My.Spells.Where(c => c != null && c.Data.IsMonster && c.In("Advanced Crystal Beast")).Select(c => c!).ToList();
                    if (more.Count > 0 && await x.Ask("Also Special Summon the Advanced Crystal Beast cards in your Spell & Trap Zones?"))
                        foreach (var c in more) if (x.D.CanSpecialSummon(c, x.Me, by: x.Self)) await x.D.SpecialSummon(c, x.Me, await x.D.ChoosePosition(x.Me, c), by: x.Self);
                };
            });
        }
        AdvancedCrystalBeast("Advanced Crystal Beast Sapphire Pegasus", async (x, e) =>
        {
            if (!e.Is(EvKind.Summoned, x.Self) || x.My.FreeSpellZones == 0) return;
            var l = x.My.Banished.Concat(x.My.Hand).Concat(x.My.Deck).Concat(x.My.Grave).Where(c => c.Data.IsMonster && c.In("Advanced Crystal Beast")).ToList();
            if (l.Count == 0 || !await x.Ask("Place an Advanced Crystal Beast in your Spell & Trap Zone as a Continuous Spell?")) return;
            var c = await x.Pick(l, "Place which Advanced Crystal Beast?");
            bool deck = c?.Loc == Loc.Deck;
            if (c != null) await PlaceAsSpell(x, c);
            if (deck) x.D.Shuffle(x.Me);
        });
        AdvancedCrystalBeast("Advanced Crystal Beast Topaz Tiger").Stats = (x, c, st) =>
        {
            if (!Up(x)) return;
            if (c.Controller == x.Me && c.In("Advanced Crystal Beast")) st.Add(400, 400);
            else if (c.Controller == x.Opp) st.Add(-400, -400);
        };
        {
            var s = S("Advanced Dark");
            s.Base = (x, c, st) => { if (c.In("Crystal Beast")) st.Attr = "DARK"; };
            s.On = async (x, e) =>
            {
                // An "Ultimate Crystal" monster's attack: the attack target's effects are negated for the rest of that Battle Phase.
                if (e.Kind == EvKind.AttackDeclared && e.Card!.In("Ultimate Crystal") && e.Other is { } t && x.Self.OnField)
                {
                    x.D.Flags[$"neg{t.Uid}"] = 1;
                    x.D.Watch(x.D.Turn, async ev => { if (ev.Kind == EvKind.BattleEnd) x.D.Flags.Remove($"neg{t.Uid}"); });
                }
            };
            s.AddFx(f =>
            {
                f.Label = "No battle damage"; f.Speed = 2;
                f.Can = x => x.Ev is { Kind: EvKind.DamageCalc } e && new[] { e.Card, e.Other }.Any(m => m != null && m.Controller == x.Me && m.In("Crystal Beast"))
                    && WouldTakeBattleDamage(x.D, x.Me) && x.My.Deck.Any(c => c.Data.IsMonster && c.In("Crystal Beast"));
                f.Cost = async x => { var c = await x.Pick(x.My.Deck.Where(c => c.Data.IsMonster && c.In("Crystal Beast")), "Send which Crystal Beast to the GY?"); if (c != null) await x.D.ToGrave(c, x.Self); };
                f.Run = async x => x.D.BattleFlags.Add($"nodamage{x.Me}");
            });
            s.Note = "Crystal Beast monsters on the field become DARK (in the GY their Attribute is only read as printed).";
        }
        {
            var s = OnBattleDeath("Agido", async x =>
            {
                int r = x.D.Die(x.Me, x.Self);
                var l = x.My.Grave.Where(c => c.Data.IsMonster && x.IsRace(c, "Fairy") && (r == 6 ? c.Data.Level >= 6 : c.Data.Level == r)).ToList();
                if (l.Count > 0 && await x.Ask($"Rolled {r}: Special Summon a Fairy from your GY?")) await SummonPick(x, l, "Special Summon which Fairy?");
            });
        }
        {
            var s = OnBattleDamage("Airknight Parshath", async (x, e) => await x.D.Draw(x.Me, 1));
            s.Allow = (x, q, c) => q == Q.Piercing && c == x.Self;
        }
        Spell("Alchemy Cycle", async x =>
        {
            var hit = new HashSet<int>();
            foreach (var m in x.MyMonsters().FaceUp().ToList()) { SetBaseAtk(x, m, 0); hit.Add(m.Uid); }
            int me = x.Me, turn = x.D.Turn; var d = x.D;
            d.Watch(turn, async e => { if (e.Kind == EvKind.Destroyed && e.Battle && hit.Contains(e.Card!.Uid) && e.Card.Loc == Loc.Grave) await d.Draw(me, 1); });
        });
        {
            var s = Spell("ALERT!", async x =>
            {
                bool deck = x.MyMonsters().FaceUp().Any(m => m.Name == "Rescue-ACE Hydrant") && x.My.Deck.Any(c => c.Data.IsMonster && c.In("Rescue-ACE"))
                    && await x.D.ChooseText(x.Me, "ALERT!: add from where?", new[] { "Your GY", "Your Deck" }, x.Self) == 1;
                var c = await x.Pick((deck ? x.My.Deck : x.My.Grave).Where(c => c.Data.IsMonster && c.In("Rescue-ACE")), "Add which Rescue-ACE monster?");
                if (c != null) await x.D.ToHand(c, x.Self);
                if (deck) x.D.Shuffle(x.Me);
            }, x => Hopt(x) && x.My.Grave.Concat(x.MyMonsters().Any(m => m.FaceUp && m.Name == "Rescue-ACE Hydrant") ? x.My.Deck : Enumerable.Empty<Card>()).Any(c => c.Data.IsMonster && c.In("Rescue-ACE")),
            async x => UseHopt(x));
            s.AlsoIn = new[] { "Rescue-ACE" };
        }
        S("Amazoness Archers").Note = "The monsters it changes must attack this turn: their controller's Battle Phase can't end while one of them can still attack.";
        Spell("Amazoness Archers", async x =>
        {
            foreach (var m in x.OppMonsters().ToList())
            {
                if (m.Defense || !m.FaceUp) await x.D.ChangePosition(m, false, true, flipEffect: false);
                m.Vars["modatk"] = m.Var("modatk") - 500;
                m.Vars["mustattack"] = x.D.Turn;
            }
        }, x => AttackOnMe(x) && x.MyMonsters().FaceUp().Any(m => m.In("Amazoness")));
        MyStandby("Amazoness Blowpiper", async x =>
        {
            var t = await x.Pick(x.OppMonsters().FaceUp(), "Select 1 face-up monster your opponent controls (-500 ATK this turn)");
            if (t != null) x.D.Boost(t, -500);
        });
        {
            var s = Spell("Amazoness Call", async x =>
            {
                var c = await x.Pick(x.My.Deck.Where(c => c.In("Amazoness") && c.Name != "Amazoness Call"), "Take which Amazoness card?");
                if (c != null)
                {
                    if (await x.D.ChooseText(x.Me, $"{c.Name}: add it to your hand or send it to the GY?", new[] { "Add to hand", "Send to the GY" }, x.Self) == 0) await x.D.ToHand(c, x.Self);
                    else await x.D.ToGrave(c, x.Self);
                }
                x.D.Shuffle(x.Me);
            }, x => Hopt(x) && x.My.Deck.Any(c => c.In("Amazoness") && c.Name != "Amazoness Call"), async x => UseHopt(x));
            s.GraveFx = new Script
            {
                Speed = 1,   // "During your Main Phase"
                Can = x => x.Targetable(x.MyMonsters().FaceUp().Where(m => m.In("Amazoness"))).Any(),
                Cost = async x => { await x.D.Banish(x.Self, x.Self); await x.Target(x.MyMonsters().FaceUp().Where(m => m.In("Amazoness")), "Which Amazoness monster attacks every monster?"); },
                Run = async x =>
                {
                    if (x.T0 is not { } t || !t.Valid()) return;
                    t.Vars["attackeach"] = x.D.Turn;
                    int me = x.Me;   // also monsters Summoned later this turn
                    x.D.Linger(x.D.Turn, (q, c, p) => q == Q.Attack && c != null && c != t && c.Controller == me);
                },
            };
        }
        OnBattleDeath("Amazoness Chain Master", async x =>
        {
            if (x.My.Lp <= 1500 || !await x.Ask("Pay 1500 LP to look at your opponent's hand and take a monster?")) return;
            x.D.Pay(x.Me, 1500);
            x.D.Reveal(x.Me, x.Their.Hand);
            var c = await x.Pick(x.Their.Hand.Where(c => c.Data.IsMonster), "Add which monster to your hand?");
            if (c != null) x.D.TakeToHand(c, x.Me);
        });
        SelfBoost("Amazoness Paladin", x => 100 * x.MyMonsters().FaceUp().Count(m => m.In("Amazoness")));
        Spell("Amazoness Spellcaster", async x =>
        {
            if (x.T.Count < 2 || !x.T.All(t => t.Valid() && t.FaceUp)) return;
            int a = x.D.StatOf(x.T[0]).BaseAtk, b = x.D.StatOf(x.T[1]).BaseAtk;
            SetBaseAtk(x, x.T[0], b); SetBaseAtk(x, x.T[1], a);
        }, x => x.Targetable(x.MyMonsters().FaceUp().Where(m => m.In("Amazoness"))).Any() && x.Targetable(x.OppMonsters().FaceUp()).Any(),
        async x =>
        {
            await x.Target(x.MyMonsters().FaceUp().Where(m => m.In("Amazoness")), "Target your Amazoness monster");
            await x.Target(x.OppMonsters().FaceUp(), "Target 1 face-up monster your opponent controls");
        });
        S("Amazoness Swords Woman").ReflectBattleDamage = true;
        {
            var s = SelfBoost("Amazoness Tiger", x => 400 * x.MyMonsters().FaceUp().Count(m => m.In("Amazoness")));
            s.Forbid = (x, q, c) => c != null && c != x.Self && Up(x) && (
                (q is Q.NormalSummon or Q.SetMonster or Q.SpecialSummon or Q.FlipSummon && c.Name == "Amazoness Tiger" && c.Controller == x.Me)
                || (q == Q.BeAttacked && c.Controller == x.Me && c.FaceUp && c.In("Amazoness")));
        }
        {
            var s = S("Ambulance Rescueroid");
            s.On = async (x, e) =>
            {
                if (e.Kind == EvKind.Destroyed && e.Battle && e.Card != x.Self && e.Card!.Loc == Loc.Grave && e.From == Loc.Monster && Up(x)
                    && x.D.LeftFieldLog.LastOrDefault(l => l.c == e.Card) is { c: not null } lf && lf.controller == x.Me && x.Self.Var("rescueturn") != x.D.Turn
                    && x.D.CanSpecialSummon(e.Card, x.Me, by: x.Self) && await x.Ask($"Special Summon {e.Card.Name} in Defense Position?"))
                {
                    x.Self.Vars["rescueturn"] = x.D.Turn;
                    await x.D.SpecialSummon(e.Card, x.Me, true, by: x.Self);
                }
            };
        }
        S("Ambulanceroid").On = async (x, e) =>
        {
            if (e.Kind == EvKind.ToHand && e.From == Loc.Grave && e.Card!.Owner == x.Me && e.Card.Loc == Loc.Hand && e.Card.Data.IsMonster && e.Card.In("roid") && Up(x)
                && x.D.CanSpecialSummon(e.Card, x.Me, by: x.Self) && await x.Ask($"Special Summon {e.Card.Name}?"))
                await x.D.SpecialSummon(e.Card, x.Me, await x.D.ChoosePosition(x.Me, e.Card), by: x.Self);
        };
        {
            var s = S("Amplifier");
            s.EquipOk = (x, m) => Scripts.NameOf(m) == "Jinzo";
            s.On = async (x, e) =>
            {
                if (e.Is(EvKind.Equipped, x.Self)) x.D.Flags[$"amp{x.Self.Uid}"] = e.Other!.Uid + 1;
                if (e.Is(EvKind.LeftField, x.Self) && x.D.Flags.GetValueOrDefault($"amp{x.Self.Uid}") is > 0 and var u && x.D.CardByUid(u - 1) is { OnField: true } m)
                { x.D.Flags.Remove($"amp{x.Self.Uid}"); await x.D.Destroy(m, x.Self); }
            };
            s.Note = "\"Its activation and effect cannot be negated\" is not applied yet (no card here negates it).";
        }
        {
            var s = S("Amulet Dragon");
            s.Materials = new() { Named("Dark Magician"), Cond("1 Dragon monster", c => c.Data.IsMonster && c.Data.Race == "Dragon") };
            s.On = async (x, e) =>
            {
                if (e.Is(EvKind.Summoned, x.Self) && e.Arg is (int)SummonKind.Special or (int)SummonKind.Fusion)
                {
                    var spells = x.D.Players.SelectMany(p => p.Grave).Where(c => c.Data.IsSpell).ToList();
                    if (spells.Count == 0) return;
                    var l = await x.PickN(spells, 1, spells.Count, "Banish any number of Spells from the GYs (100 ATK each)");
                    int n = 0;
                    foreach (var c in l) { await x.D.Banish(c, x.Self); if (c.Loc == Loc.Banished) n++; }
                    if (x.Self.Valid()) x.Self.Vars["modatk"] = x.Self.Var("modatk") + 100 * n;
                }
                if (e.Is(EvKind.Destroyed, x.Self) && x.My.Grave.Any(c => c.Data.IsMonster && c.Data.Race == "Spellcaster") && await x.Ask("Special Summon a Spellcaster from your GY?"))
                    await SummonPick(x, x.My.Grave.Where(c => c.Data.IsMonster && c.Data.Race == "Spellcaster"), "Special Summon which Spellcaster?");
            };
        }
        {
            static int Beasts(Ctx x) => x.My.Spells.Count(c => c != null && c.FaceUp && c.In("Crystal Beast"));
            var s = S("Ancient City - Rainbow Ruins");
            s.Forbid = (x, q, c) => q == Q.DestroyByEffect && c == x.Self && Beasts(x) >= 1;
            s.BattleDamage = (x, src, to, dmg) =>
            {
                if (to != x.Me || Beasts(x) < 2 || x.Self.Var("halfturn") == x.D.Turn) return dmg;
                x.Self.Vars["halfturn"] = x.D.Turn;
                return dmg / 2;
            };
            s.AddFx(f =>
            {
                f.Label = "Negate a Spell/Trap"; f.Speed = 2;
                f.Can = x => Beasts(x) >= 3 && x.Ev is { Kind: EvKind.Activated, Link: { IsCardActivation: true } l } && !l.Card.Data.IsMonster && l.Card != x.Self
                    && x.MyMonsters().Any(m => m.FaceUp && m.In("Crystal Beast"));
                f.Cost = async x => { var c = await x.Pick(x.MyMonsters().Where(m => m.FaceUp && m.In("Crystal Beast")), "Send which Crystal Beast to the GY?"); if (c != null) await x.D.ToGrave(c, x.Self); };
                f.Run = async x => { if (Beasts(x) >= 3) await x.D.Negate(x.Ev!.Link!); };
            });
            s.AddFx(f => { f.Label = "Draw 1"; f.OncePerTurn = true; f.Can = x => Beasts(x) >= 4; f.Run = async x => { if (Beasts(x) >= 4) await x.D.Draw(x.Me, 1); }; });
            s.AddFx(f =>
            {
                f.Label = "Summon a Crystal Beast"; f.OncePerTurn = true;
                f.Can = x => Beasts(x) >= 5 && x.D.FreeMonsterZones(x.Me) > 0;
                f.Cost = async x => await x.Target(x.My.Spells.Where(c => c != null && c.FaceUp && c.Data.IsMonster && c.In("Crystal Beast")).Select(c => c!), "Special Summon which Crystal Beast?");
                f.Run = async x => { if (Beasts(x) >= 5 && x.T0 is { Loc: Loc.Spell } t) await x.D.SpecialSummon(t, x.Me, await x.D.ChoosePosition(x.Me, t), by: x.Self); };
            });
            s.Note = "Halving battle damage (2+) is applied automatically the first time you would take battle damage each turn.";
        }
        OnBattleDamage("Ancient Dragon", async (x, e) =>
        {
            if (e.Other == null && x.Self.Valid() && await x.Ask("Increase Ancient Dragon's Level by 1 and ATK by 500?"))
            { x.Self.Vars["modlevel"] = x.Self.Var("modlevel") + 1; x.Self.Vars["modatk"] = x.Self.Var("modatk") + 500; x.D.Seq++; }
        });
        {
            var s = S("Ancient Gear");
            s.ProcCan = x => x.MyMonsters().Any(m => m.FaceUp && m.In("Ancient Gear"));
            s.Proc = async x => await x.D.SpecialSummon(x.Self, x.Me, false, by: x.Self);
        }
        {
            var s = S("Ancient Gear Beast");
            s.SummonOk = (x, by, k) => false;
            s.Forbid = (x, q, c) => q == Q.Activate && c != null && c.Controller == x.Opp && !c.Data.IsMonster && x.D.Attacker == x.Self;
            s.On = async (x, e) => { if (e.Kind == EvKind.BattleDestroyedMonster && e.Card == x.Self && e.Other!.Controller != x.Me) x.D.Flags[$"neg{e.Other.Uid}"] = 1; };
        }
        {
            var s = S("Ancient Gear Cannon");
            s.Cost = async x => await x.D.Tribute(x.Self, x.Self);
            s.Run = async x =>
            {
                await x.D.Damage(x.Opp, 500, x.Self);
                var d = x.D;
                d.Linger(d.Turn, (q, c, p) => q == Q.Activate && c != null && c.Data.IsTrap && d.Phase == Phase.Battle);
            };
        }
        {
            var s = S("Ancient Gear Castle");
            s.Stats = (x, c, st) => { if (c.In("Ancient Gear")) st.Add(300, 0); };
            s.On = async (x, e) =>
            {
                if (x.Self.OnField && (e.Summoned(SummonKind.Normal, SummonKind.Tribute) || (e.Kind == EvKind.Set && e.Card!.Summoned == SummonKind.Set))) AddCounter(x, x.Self);
            };
            s.FreeTribute = (x, c, set) => !set && c.In("Ancient Gear") && x.Self.OnField && x.Self.Counters() >= x.D.TributesFor(c);
            s.OnFreeTribute = async x => await x.D.Tribute(x.Self, x.Self);
            s.Note = "Tributing it instead of monsters is offered when you Tribute Summon an Ancient Gear face-up.";
        }
        Spell("Ancient Gear Drill", async x =>
        {
            var c = await x.Pick(x.My.Deck.Where(c => c.Data.IsSpell && (c.Data.Is("Field") ? x.My.FieldZone == null : x.My.FreeSpellZones > 0)), "Set which Spell from your Deck?");
            if (c != null && x.D.Place(c, x.Me, c.Data.Is("Field") ? Loc.Field : Loc.Spell))
            {
                c.FaceUp = false; c.SetTurn = x.D.Turn; c.Vars["noactturn"] = x.D.Turn;
                await x.D.Raise(new Ev { Kind = EvKind.Set, Card = c, Player = x.Me });
            }
            x.D.Shuffle(x.Me);
        }, x => x.MyMonsters().FaceUp().Any(m => m.In("Ancient Gear")) && x.My.Hand.Any(c => c != x.Self) && x.My.Deck.Any(c => c.Data.IsSpell),
        async x => await x.D.Discard(x.Me, await x.PickN(x.My.Hand.Where(c => c != x.Self), 1, 1, "Discard 1 card"), x.Self));
        {
            var s = S("Ancient Gear Engineer");
            s.Forbid = (x, q, c) => q == Q.Activate && c != null && c.Controller == x.Opp && !c.Data.IsMonster && x.D.Attacker == x.Self;
            s.On = async (x, e) =>
            {
                if (e.Kind == EvKind.Activated && e.Link is { } l && l.Card.Data.IsTrap && l.X.T.Contains(x.Self) && Up(x)) await x.D.Negate(l);
                if (e.Kind == EvKind.AfterDamage && e.Card == x.Self && x.Self.Loc is Loc.Monster or Loc.Grave)
                {
                    var t = await x.Pick(x.Targetable(x.Spells(x.Opp)), "Destroy which Spell/Trap your opponent controls?");
                    if (t != null) await x.D.Destroy(t, x.Self);
                }
            };
        }
        Spell("Ancient Gear Explosive", async x =>
        {
            if (x.T0 is not { } t || !t.Valid()) return;
            int half = t.Data.Atk / 2;
            await x.D.Destroy(t, x.Self);
            if (t.Loc != Loc.Monster) await x.D.Damage(x.Opp, half, x.Self);
        }, x => x.Targetable(x.MyMonsters().FaceUp().Where(m => m.In("Ancient Gear"))).Any(),
        async x => await x.Target(x.MyMonsters().FaceUp().Where(m => m.In("Ancient Gear")), "Destroy which Ancient Gear monster?"));
        {
            static IEnumerable<Card> Gears(Ctx x) => x.My.Grave.Where(c => c.Data.IsMonster && c.In("Ancient Gear"));
            static bool Sums(List<int> lv, int target) { var ok = new bool[target + 1]; ok[0] = true; foreach (var l in lv) for (int t = target; t >= l; t--) ok[t] |= ok[t - l]; return ok[target]; }
            Spell("Ancient Gear Factory", async x =>
            {
                if (x.T0 is { Loc: Loc.Hand } m) m.Vars["notributeturn"] = x.D.Turn;
            }, x => x.My.Hand.Any(h => h.Data.IsMonster && h.In("Ancient Gear") && h.Data.Level >= 5 && Sums(Gears(x).Select(c => c.Data.Level).ToList(), 2 * h.Data.Level)),
            async x =>
            {
                var m = await x.Pick(x.My.Hand.Where(h => h.Data.IsMonster && h.In("Ancient Gear") && h.Data.Level >= 5 && Sums(Gears(x).Select(c => c.Data.Level).ToList(), 2 * h.Data.Level)), "Reveal which Ancient Gear?");
                if (m == null) return;
                x.D.Reveal(x.Opp, m); x.T.Add(m);
                int left = 2 * m.Data.Level;
                while (left > 0)
                {
                    var c = await x.Pick(Gears(x).Where(g => g.Data.Level <= left && Sums(Gears(x).Where(o => o != g).Select(o => o.Data.Level).ToList(), left - g.Data.Level)), $"Banish Ancient Gear monsters from your GY ({left} Levels to go)");
                    if (c == null) break;
                    left -= c.Data.Level;
                    await x.D.Banish(c, x.Self);
                }
            });
        }
        {
            var s = S("Ancient Gear Fist");
            s.EquipOk = (x, m) => m.In("Ancient Gear");
            s.On = async (x, e) =>
            {
                if (e.Kind != EvKind.AfterDamage || x.Self.EquippedTo is not { } m || !m.Valid()) return;
                var other = e.Card == m ? e.Other : e.Other == m ? e.Card : null;
                if (other != null && other.Valid()) await x.D.Destroy(other, x.Self);
            };
        }
    }
}
