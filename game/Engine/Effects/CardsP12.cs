namespace PoC.Engine;

// Official cards, batch 12 (Foolish Burial .. Goblin Zombie).
public static partial class Scripts
{
    // Fossil Fusion materials: "in your GY" / "in your opponent's GY" are read against the player Fusion Summoning.
    static Mat Rock(bool yourGy = false) => Cond(yourGy ? "1 Rock monster in your GY" : "1 Rock monster", c => c.Data.IsMonster && c.Data.Race == "Rock" && (!yourGy || (c.Loc == Loc.Grave && c.Owner == FusingPlayer)));
    static Mat LevelMat(string label, Func<int, bool> lv, bool oppGy = false) =>
        Cond(label, c => c.Data.IsMonster && lv(c.Data.Level) && (!oppGy || (c.Loc == Loc.Grave && c.Owner != FusingPlayer)));

    /// "Must first be Special Summoned with Fossil Fusion"; "can make up to N attacks on monsters".
    static Script Fossil(string name, List<Mat> mats, int attacksOnMonsters = 1)
    {
        var s = S(name);
        s.Materials = mats;
        s.SummonOk = (x, by, k) => (k == SummonKind.Fusion && by?.Name == "Fossil Fusion") || x.D.Flags.ContainsKey($"proper{x.Self.Uid}");
        if (attacksOnMonsters > 1)
        {
            s.ExtraAttacks = x => attacksOnMonsters - 1;
            s.Forbid = (x, q, c) => q == Q.DirectAttack && c == x.Self && x.Self.Attacks > 0;
        }
        return s;
    }

    /// A Fossil monster's "banish this card from your GY" effect (once per turn).
    static void FossilGrave(Script s, string label, Func<Ctx, bool> can, Func<Ctx, Flow> run, Func<Ctx, Flow>? target = null)
    {
        s.AddFx(f =>
        {
            f.Label = label; f.FromGrave = true;
            f.Can = x => Hopt(x) && can(x);
            f.Cost = async x => { UseHopt(x); await x.D.Banish(x.Self, x.Self); if (target != null) await target(x); };
            f.Run = run;
        });
    }

    static void BurnOnKill(Script s, int dmg)
    {
        var on = s.On;
        s.On = async (x, e) =>
        {
            if (on != null) await on(x, e);
            if (e.Kind == EvKind.BattleDestroyedMonster && e.Card == x.Self && e.Other!.Controller == x.Opp && Up(x) && await x.Ask($"Inflict {dmg} damage to your opponent?")) await x.D.Damage(x.Opp, dmg, x.Self);
        };
    }

    static void CardsP12(CardDb db)
    {
        Spell("Foolish Burial", async x => { var c = await x.Pick(x.My.Deck.Where(c => c.Data.IsMonster), "Send which monster from your Deck to the GY?"); if (c != null) await x.D.ToGrave(c, x.Self); x.D.Shuffle(x.Me); },
            x => x.My.Deck.Any(c => c.Data.IsMonster));
        {
            var s = Union("Fortune Chariot", (x, m) => x.IsRace(m, "Fairy"), effectsToo: true);
            s.AlsoIn = new[] { "Valkyrie" };
            s.Allow = (x, q, c) => q == Q.DirectAttack && c != null && x.Self.Var("union") == 1 && c == x.Self.EquippedTo;
            s.BattleDamage = (x, src, to, dmg) => x.Self.Var("union") == 1 && src == x.Self.EquippedTo && to == x.Opp && x.D.AttackTarget == null && x.Their.MonstersOnField.Any() ? dmg / 2 : dmg;
        }
        {
            var s = Fossil("Fossil Dragon Skullgar", new() { Rock(), LevelMat("1 Level 4 or lower monster in your opponent's GY", l => l <= 4, oppGy: true) });
            s.Allow = (x, q, c) => q == Q.Piercing && c == x.Self;
            FossilGrave(s, "Add Fossil Fusion", x => x.My.Deck.Any(c => c.Name == "Fossil Fusion"), x => x.Search(c => c.Name == "Fossil Fusion", "Add Fossil Fusion"));
        }
        {
            var s = Fossil("Fossil Dragon Skullgios", new() { Rock(), LevelMat("1 Level 7 or higher monster in your opponent's GY", l => l >= 7, oppGy: true) });
            s.Allow = (x, q, c) => q == Q.Piercing && c == x.Self;
            s.BattleDamage = (x, src, to, dmg) => src == x.Self && to == x.Opp && x.Self.Summoned == SummonKind.Fusion ? dmg * 2 : dmg;
            s.On = async (x, e) =>
            {
                if (e.Kind == EvKind.DamageCalc && (e.Card == x.Self || e.Other == x.Self) && Up(x))
                {
                    var o = e.Card == x.Self ? e.Other : e.Card;
                    if (o == null || o.Controller != x.Opp || !o.FaceUp || !await x.Ask($"Switch {o.Name}'s ATK and DEF until the end of the Damage Step?")) return;
                    int atk = x.Atk(o), def = x.Def(o);
                    x.D.Boost(o, def - atk, atk - def);
                    x.D.UntilBattleEnds(() => { o.TempAtk -= def - atk; o.TempDef -= atk - def; });
                }
            };
        }
        S("Fossil Dyna Pachycephalo").Forbid = (x, q, c) => q == Q.SpecialSummon && Up(x);
        S("Fossil Dyna Pachycephalo").On = async (x, e) =>
        {
            if (e.Is(EvKind.Flipped, x.Self)) await x.D.Destroy(x.AllMonsters().Where(m => m.Summoned is SummonKind.Special or SummonKind.Fusion or SummonKind.Ritual).ToList(), x.Self);
        };
        {
            var s = Spell("Fossil Excavation", async x =>
            {
                if (x.T0 is not { Loc: Loc.Grave } t || !await x.D.SpecialSummon(t, x.Me, await x.D.ChoosePosition(x.Me, t), by: x.Self)) return;
                Tie(x, t);
                x.D.Flags[$"neg{t.Uid}"] = 1;
            }, x => x.My.Hand.Any(c => c != x.Self) && x.D.FreeMonsterZones(x.Me) > 0 && x.Targetable(x.My.Grave.Where(c => c.Data.IsMonster && c.Data.Race == "Dinosaur")).Any(),
            async x =>
            {
                await x.D.Discard(x.Me, await x.PickN(x.My.Hand.Where(c => c != x.Self), 1, 1, "Discard 1 card"), x.Self);
                await x.Target(x.My.Grave.Where(c => c.Data.IsMonster && c.Data.Race == "Dinosaur"), "Special Summon which Dinosaur?");
            });
            s.On = async (x, e) =>
            {
                if (e.Is(EvKind.LeftField, x.Self) && TiedMonster(x) is { Loc: Loc.Monster } m) { x.D.Flags.Remove($"tie{x.Self.Uid}"); await x.D.Destroy(m, x.Self); }
                if (e.Kind == EvKind.LeftField && e.Card == TiedMonster(x) && x.Self.OnField) { x.D.Flags.Remove($"tie{x.Self.Uid}"); x.D.Flags.Remove($"neg{e.Card!.Uid}"); await x.D.Destroy(x.Self, x.Self); }
            };
        }
        {
            static IEnumerable<Card> Gys(Ctx x) => x.D.Players.SelectMany(p => p.Grave).Where(c => c.Data.IsMonster);
            var s = Spell("Fossil Fusion", async x =>
            {
                x.D.LastFusion = null;
                if (!await x.D.FusionSummon(x.Me, x.Self, pool: Gys(x), resultOk: c => c.In("Fossil"), matsTo: Loc.Banished) || x.D.LastFusion is not { } f) return;
                var used = Enumerable.Range(0, 5).Select(i => f.Var($"mat{i}")).Where(u => u > 0).Select(u => x.D.CardByUid(u - 1)).ToList();
                if (used.Any(c => c.Owner == x.Me) && used.Any(c => c.Owner == x.Opp))
                    x.D.Linger(int.MaxValue, (q, c, p) => q == Q.Target && c == f && f.Loc == Loc.Monster && x.D.Targeter?.Data.IsMonster == true);
            }, x => x.D.FusionOptions(x.Me, pool: Gys(x), resultOk: c => c.In("Fossil"), by: x.Self).Any());
            s.On = async (x, e) =>
            {
                if (e.Kind == EvKind.Destroyed && x.Self.Loc == Loc.Grave && e.Card!.Data.IsFusion && e.Card.In("Fossil") && x.D.LeftFieldLog.LastOrDefault(l => l.c == e.Card) is { c: not null } l
                    && l.controller == x.Me && l.faceUp && Hopt(x) && await x.Ask("Add Fossil Fusion from your GY to your hand?"))
                { UseHopt(x); await x.D.ToHand(x.Self, x.Self); }
            };
            s.Note = "If materials came from both GYs, monster effects can't target the Fusion Monster.";
        }
        {
            var s = Fossil("Fossil Machine Skull Buggy", new() { Rock(yourGy: true), LevelMat("1 Level 4 or lower monster", l => l <= 4) });
            BurnOnKill(s, 600);
            FossilGrave(s, "Add a Fossil Fusion monster", x => x.My.Deck.Any(c => c.Data.IsMonster && Mentions(c.Data, "Fossil Fusion")), x => x.Search(c => c.Data.IsMonster && Mentions(c.Data, "Fossil Fusion"), "Add which monster?"));
        }
        {
            var s = Fossil("Fossil Machine Skull Convoy", new() { Rock(yourGy: true), LevelMat("1 Level 7 or higher monster", l => l >= 7) }, attacksOnMonsters: 3);
            s.Stats = (x, c, st) => { if (c.Controller == x.Opp && x.Self.Summoned == SummonKind.Fusion && Up(x)) st.Add(-c.Data.Def, 0); };
            BurnOnKill(s, 1000);
        }
        {
            var s = Fossil("Fossil Machine Skull Wagon", new() { Rock(yourGy: true), LevelMat("1 Level 5 or 6 monster", l => l is 5 or 6) }, attacksOnMonsters: 2);
            BurnOnKill(s, 800);
            FossilGrave(s, "Destroy a Spell/Trap", x => x.Targetable(x.AllSpells()).Any(), async x => { if (x.T0 is { OnField: true } t) await x.D.Destroy(t, x.Self); },
                async x => await x.Target(x.AllSpells(), "Destroy which Spell/Trap?"));
        }
        {
            var s = Fossil("Fossil Warrior Skull Bone", new() { Rock(), LevelMat("1 Level 4 or lower monster", l => l <= 4) }, attacksOnMonsters: 2);
            FossilGrave(s, "Add Time Stream", x => x.My.Deck.Any(c => c.Name == "Time Stream"), x => x.Search(c => c.Name == "Time Stream", "Add Time Stream"));
        }
        {
            var s = Fossil("Fossil Warrior Skull King", new() { Rock(), LevelMat("1 Level 7 or higher monster", l => l >= 7) });
            s.Allow = (x, q, c) => c == x.Self && q is Q.Piercing or Q.DoubleAttack;
            s.Speed = 2; s.AnyTurn = true;
            s.Can = x => Up(x) && !x.Turn() && Hopt(x) && x.My.Hand.Count > 0 && x.D.FreeMonsterZones(x.Me) > 0 && x.Targetable(x.Their.Grave.Where(c => c.Data.IsMonster)).Any();
            s.Cost = async x => { UseHopt(x); await x.Target(x.Their.Grave.Where(c => c.Data.IsMonster), "Special Summon which monster from your opponent's GY?"); };
            s.Run = async x =>
            {
                if (x.My.Hand.Count == 0) return;
                await x.D.Discard(x.Me, await x.PickN(x.My.Hand, 1, 1, "Discard 1 card"), x.Self);
                if (x.T0 is { Loc: Loc.Grave } t) await x.D.SpecialSummon(t, x.Me, await x.D.ChoosePosition(x.Me, t), by: x.Self);
            };
        }
        {
            var s = Fossil("Fossil Warrior Skull Knight", new() { Rock(), LevelMat("1 Level 5 or 6 monster", l => l is 5 or 6) });
            s.Allow = (x, q, c) => q == Q.Piercing && c == x.Self;
            s.On = async (x, e) =>
            {
                if (e.Kind == EvKind.BattleDestroyedMonster && e.Card == x.Self && x.D.Attacker == x.Self && Up(x) && await x.Ask("Make a second attack in a row?"))
                { x.Self.Vars["maxatk"] = x.Self.Attacks + 1; x.Self.Vars["maxatkturn"] = x.D.Turn; }
            };
            FossilGrave(s, "Destroy a monster", x => x.Targetable(x.AllMonsters()).Any(), async x => { if (x.T0 is { } t && t.Valid()) await x.D.Destroy(t, x.Self); },
                async x => await x.Target(x.AllMonsters(), "Destroy which monster?"));
        }
        {
            var s = S("Freed the Brave Wanderer");
            s.OncePerTurn = true;
            s.Can = x => Up(x) && x.My.Grave.Count(c => c.Data.IsMonster && c.Data.Attribute == "LIGHT") >= 2 && x.Targetable(x.D.FaceUpMonsters.Where(m => x.Atk(m) > x.Atk(x.Self))).Any();
            s.Cost = async x =>
            {
                foreach (var c in await x.PickN(x.My.Grave.Where(c => c.Data.IsMonster && c.Data.Attribute == "LIGHT"), 2, 2, "Banish 2 LIGHT monsters from your GY")) await x.D.Banish(c, x.Self);
                await x.Target(x.D.FaceUpMonsters.Where(m => x.Atk(m) > x.Atk(x.Self)), "Destroy which monster with higher ATK?");
            };
            s.Run = async x => { if (x.T0 is { } t && t.Valid()) await x.D.Destroy(t, x.Self); };
        }
        {
            static bool Union(Card c) => c.Data.IsMonster && c.Data.Desc.Contains("Union");
            var s = S("Frontline Base");
            s.AddFx(f =>
            {
                f.Label = "Summon a Union monster"; f.OncePerTurn = true;
                f.Can = x => x.D.FreeMonsterZones(x.Me) > 0 && x.My.Hand.Any(c => Union(c) && c.Data.Level <= 4);
                f.Run = async x => await SummonPick(x, x.My.Hand.Where(c => Union(c) && c.Data.Level <= 4), "Special Summon which Union monster?");
            });
        }
        Spell("Fuh-Rin-Ka-Zan", async x =>
        {
            int i = await x.D.ChooseText(x.Me, "Fuh-Rin-Ka-Zan", new[] { "Destroy all monsters your opponent controls", "Destroy all Spells/Traps your opponent controls", "Discard 2 random cards from their hand", "Draw 2 cards" }, x.Self);
            if (i == 0) await x.D.Destroy(x.OppMonsters().ToList(), x.Self);
            else if (i == 1) await x.D.Destroy(x.Spells(x.Opp).ToList(), x.Self);
            else if (i == 2) await x.D.DiscardRandom(x.Opp, 2, x.Self);
            else await x.D.Draw(x.Me, 2);
        }, x => new[] { "WIND", "WATER", "FIRE", "EARTH" }.All(a => x.D.FaceUpMonsters.Any(m => x.IsAttr(m, a))));
        {
            var s = Spell("Fulfillment of the Contract", async x =>
            {
                if (x.T0 is not { Loc: Loc.Grave } t || !await x.D.SpecialSummon(t, x.Me, await x.D.ChoosePosition(x.Me, t), by: x.Self)) return;
                await x.D.Equip(x.Self, t);
                Tie(x, t);
            }, x => x.My.Lp > 800 && x.D.FreeMonsterZones(x.Me) > 0 && x.Targetable(x.My.Grave.Where(c => c.Data.IsRitual)).Any(),
            async x => { x.D.Pay(x.Me, 800); await x.Target(x.My.Grave.Where(c => c.Data.IsRitual), "Special Summon which Ritual Monster?"); });
            s.EquipOk = (x, m) => true;
            s.On = async (x, e) => { if (e.Is(EvKind.Destroyed, x.Self) && TiedMonster(x) is { Loc: Loc.Monster } m) { x.D.Flags.Remove($"tie{x.Self.Uid}"); await x.D.Banish(m, x.Self); } };
        }
        {
            var s = S("Full Moon Mirror");
            s.On = async (x, e) => { if (e.Kind == EvKind.Destroyed && e.From == Loc.Monster && x.Self.OnField && x.Self.FaceUp) AddCounter(x, x.Self, 1, "moon"); };
            s.AddFx(f =>
            {
                f.Label = "Use Full Moon Counters";
                f.Can = x => Hopt(x) && x.Self.Counters("moon") >= 1;
                f.Cost = async x =>
                {
                    UseHopt(x);
                    var opts = new List<string> { "1: Special Summon a Level 6 or lower Fiend/Fairy (hand or GY)" };
                    if (x.Self.Counters("moon") >= 3) opts.Add("3: Add a Level 10 DARK monster from your Deck");
                    if (x.Self.Counters("moon") >= 5) opts.Add("5: Special Summon a Level 10 monster (hand or GY)");
                    int i = await x.D.ChooseText(x.Me, "Full Moon Mirror", opts, x.Self);
                    x.N = i;
                    AddCounter(x, x.Self, -(i == 0 ? 1 : i == 1 ? 3 : 5), "moon");
                };
                f.Run = async x =>
                {
                    var hg = x.My.Hand.Concat(x.My.Grave);
                    if (x.N == 0) await SummonPick(x, hg.Where(c => c.Data.IsMonster && c.Data.Level <= 6 && c.Data.Race is "Fiend" or "Fairy"), "Special Summon which?");
                    else if (x.N == 1) await x.Search(c => c.Data.IsMonster && c.Data.Level == 10 && c.Data.Attribute == "DARK", "Add which Level 10 DARK monster?");
                    else await SummonPick(x, hg.Where(c => c.Data.IsMonster && c.Data.Level == 10), "Special Summon which Level 10 monster?");
                };
            });
        }
        {
            var s = S("Fushioh Richie");
            s.SummonOk = (x, by, k) => by == x.Self || x.D.Flags.ContainsKey($"proper{x.Self.Uid}");
            s.ProcFromDeck = true;
            s.ProcCan = x => x.Self.Loc is Loc.Hand or Loc.Deck && x.MyMonsters().Any(m => m.FaceUp && m.Name == "Great Dezard" && m.Var("dezard") >= 2);
            s.Proc = async x =>
            {
                var d = await x.Pick(x.MyMonsters().Where(m => m.FaceUp && m.Name == "Great Dezard" && m.Var("dezard") >= 2), "Tribute Great Dezard");
                if (d == null) return;
                bool deck = x.Self.Loc == Loc.Deck;
                await x.D.Tribute(d, x.Self);
                await x.D.SpecialSummon(x.Self, x.Me, await x.D.ChoosePosition(x.Me, x.Self), by: x.Self);
                if (deck) x.D.Shuffle(x.Me);
            };
            s.OncePerTurn = true;
            s.Can = x => Up(x);
            s.Run = async x => { if (Up(x)) await x.D.SetFaceDown(x.Self); };
            s.On = async (x, e) =>
            {
                if (e.Kind == EvKind.Activated && e.Link is { } l && !l.Card.Data.IsMonster && l.X.T.Contains(x.Self) && Up(x)) await x.D.Negate(l);
                if (e.Is(EvKind.Flipped, x.Self) && x.My.Grave.Any(c => c.Data.IsMonster && c.Data.Race == "Zombie") && await x.Ask("Special Summon a Zombie from your GY?"))
                    await SummonPick(x, x.Targetable(x.My.Grave.Where(c => c.Data.IsMonster && c.Data.Race == "Zombie")), "Special Summon which Zombie?");
            };
        }
        {
            var s = S("Fusilier Dragon, the Dual-Mode Beast");
            s.FreeTribute = (x, c, set) => c == x.Self && x.Self.Loc == Loc.Hand;
            s.OnFreeTribute = async x => x.Self.Vars["halved"] = 1;
            s.Base = (x, c, st) => { if (c == x.Self && x.Self.Var("halved") == 1) { st.BaseAtk /= 2; st.BaseDef /= 2; } };
        }
        S("Fusion Devourer").CalcAtk = (x, c, vs, atk) => vs == x.Self && c.Data.IsFusion && Up(x) ? 0 : atk;
        Spell("Fusion Guard", async x =>
        {
            await x.D.Negate(x.Ev!.Link!);
            var l = x.My.Extra.Where(c => c.Data.IsFusion).ToList();
            if (l.Count > 0) await x.D.ToGrave(l[x.D.Rng.Int(l.Count)], x.Self);
        }, x => x.My.Extra.Any() && x.Ev is { Kind: EvKind.Activated, Link: { } l } && l.Card != x.Self && System.Text.RegularExpressions.Regex.IsMatch(l.Card.Data.Desc, @"[Ii]nflict[^.]*damage"));
        Spell("Fusion Recovery", async x => { foreach (var t in x.T.Where(t => t.Loc == Loc.Grave).ToList()) await x.D.ToHand(t, x.Self); },
            x => x.Targetable(x.My.Grave.Where(c => c.Name == "Polymerization")).Any() && x.Targetable(x.My.Grave.Where(c => c.Var("fusionmat") == 1)).Any(),
            async x =>
            {
                await x.Target(x.My.Grave.Where(c => c.Name == "Polymerization"), "Target Polymerization in your GY");
                await x.Target(x.My.Grave.Where(c => c.Var("fusionmat") == 1), "Target a monster used as Fusion Material");
            });
        {
            var s = S("Fusion Weapon");
            s.EquipOk = (x, m) => m.Data.IsFusion && x.D.Level(m) <= 6;
            s.Stats = (x, c, st) => { if (c == x.Self.EquippedTo) st.Add(1500, 1500); };
        }
        {
            var s = Spell("Future Fusion", async x =>
            {
                x.Self.Vars["futurestart"] = x.D.Turn;
            });
            s.Run = async x => { x.Self.Vars["futurestart"] = x.D.Turn; x.Self.Vars["futurestandby"] = 0; };
            s.On = async (x, e) =>
            {
                if (e.Kind == EvKind.Standby && e.Player == x.Me && x.Self.OnField && x.Self.FaceUp && x.Self.Var("futurestart") > 0)
                {
                    int n = x.Self.Inc("futurestandby");
                    if (n == 1)
                    {
                        var f = await x.Pick(x.My.Extra.Where(f => f.Data.IsFusion), "Show which Fusion Monster?");
                        if (f == null) return;
                        x.D.Reveal(x.Opp, f);
                        x.Self.Vars["futureidx"] = f.Data.Idx + 1;
                        var mats = x.D.Recipes(f).FirstOrDefault() ?? new();
                        var used = new List<Card>();
                        foreach (var m in mats)
                        {
                            var c = await x.Pick(x.My.Deck.Where(c => !used.Contains(c) && m.Ok(c)), $"Send {m.Label} from your Deck to the GY");
                            if (c != null) used.Add(c);
                        }
                        foreach (var c in used) await x.D.ToGrave(c, x.Self);
                        x.D.Shuffle(x.Me);
                    }
                    else if (n == 2 && x.Self.Var("futureidx") > 0)
                    {
                        string name = x.D.Db.Get(x.Self.Var("futureidx") - 1).Name;
                        var f = x.My.Extra.FirstOrDefault(c => c.Name == name && x.D.CanSpecialSummon(c, x.Me, SummonKind.Fusion, x.Self));
                        if (f != null && await x.D.SpecialSummon(f, x.Me, await x.D.ChoosePosition(x.Me, f), kind: SummonKind.Fusion, by: x.Self)) Tie(x, f);
                    }
                }
                if (e.Is(EvKind.LeftField, x.Self) && TiedMonster(x) is { Loc: Loc.Monster } t) { x.D.Flags.Remove($"tie{x.Self.Uid}"); await x.D.Destroy(t, x.Self); }
                if (e.Kind == EvKind.Destroyed && e.Card == TiedMonster(x) && x.Self.OnField) { x.D.Flags.Remove($"tie{x.Self.Uid}"); await x.D.Destroy(x.Self, x.Self); }
            };
        }
        {
            var s = S("Gaia Plate the Earth Giant");
            s.ProcCan = x => x.My.Grave.Count(c => c.Data.IsMonster && c.Data.Race == "Rock") >= 2;
            s.Proc = async x =>
            {
                foreach (var c in await x.PickN(x.My.Grave.Where(c => c.Data.IsMonster && c.Data.Race == "Rock"), 2, 2, "Banish 2 Rock monsters from your GY")) await x.D.Banish(c, x.Self);
                await x.D.SpecialSummon(x.Self, x.Me, await x.D.ChoosePosition(x.Me, x.Self), by: x.Self);
            };
            s.CalcAtk = (x, c, vs, atk) => vs == x.Self && c.Controller != x.Me && Up(x) ? atk / 2 : atk;
            s.On = async (x, e) =>
            {
                if (e.Kind != EvKind.Standby || e.Player != x.Me || !Up(x)) return;
                var rocks = x.My.Grave.Where(c => c.Data.IsMonster && c.Data.Race == "Rock").ToList();
                if (rocks.Count > 0 && await x.Ask("Banish 1 Rock monster from your GY to keep Gaia Plate?")) { var c = await x.Pick(rocks, "Banish which Rock?"); if (c != null) { await x.D.Banish(c, x.Self); return; } }
                await x.D.ToGrave(x.Self, x.Self);
            };
            s.CalcDef = (x, c, vs, def) => vs == x.Self && c.Controller != x.Me && Up(x) ? def / 2 : def;
        }
        {
            var s = S("Gaia Soul the Combustible Collective");
            s.Allow = (x, q, c) => q == Q.Piercing && c == x.Self;
            s.OncePerTurn = true;
            s.Can = x => Up(x) && x.D.Tributable(x.Me).Any(m => m != x.Self && x.IsRace(m, "Pyro"));
            s.Cost = async x =>
            {
                var l = await x.PickN(x.D.Tributable(x.Me).Where(m => m != x.Self && x.IsRace(m, "Pyro")), 1, 2, "Tribute up to 2 Pyro monsters");
                foreach (var t in l) await x.D.Tribute(t, x.Self);
                x.N = l.Count;
            };
            s.Run = async x => { if (Up(x)) { x.Self.Vars["modatk"] = x.Self.Var("modatk") + 1000 * x.N; x.D.Seq++; } };
            s.On = async (x, e) => { if (e.Kind == EvKind.EndPhase && Up(x)) await x.D.Destroy(x.Self, x.Self); };
        }
        {
            var s = S("Gallis the Star Beast");
            s.AddFx(f =>
            {
                f.Label = "Reveal: mill the top card"; f.FromHand = true;
                f.Can = x => x.My.Deck.Count > 0;
                f.Cost = async x => x.D.Reveal(x.Opp, x.Self);
                f.Run = async x =>
                {
                    if (x.My.Deck.Count == 0) return;
                    var top = x.My.Deck[0];
                    await x.D.ToGrave(top, x.Self);
                    if (top.Data.IsMonster)
                    {
                        await x.D.Damage(x.Opp, 200 * top.Data.Level, x.Self);
                        if (x.Self.Loc == Loc.Hand) await x.D.SpecialSummon(x.Self, x.Me, await x.D.ChoosePosition(x.Me, x.Self), by: x.Self);
                    }
                    else await DestroyInHand(x, x.Self);
                };
            });
        }
        {
            var s = S("Gandora the Dragon of Destruction");
            s.SummonOk = (x, by, k) => false;
            s.Can = x => Up(x) && x.My.Lp > 1 && x.D.FieldCards.Any(c => c != x.Self);
            s.Cost = async x => x.D.Pay(x.Me, x.My.Lp / 2);
            s.Run = async x =>
            {
                var l = x.D.FieldCards.Where(c => c != x.Self).ToList();
                await x.D.Destroy(l, x.Self);
                int n = 0;
                foreach (var c in l.Where(c => !c.OnField)) { n++; if (c.Loc == Loc.Grave) await x.D.Banish(c, x.Self); }
                if (Up(x)) { x.Self.Vars["modatk"] = x.Self.Var("modatk") + 300 * n; x.D.Seq++; }
            };
            s.On = async (x, e) =>
            {
                if (e.Kind == EvKind.EndPhase && Up(x) && x.Self.SummonTurn == x.D.Turn && x.Self.Summoned is SummonKind.Normal or SummonKind.Tribute or SummonKind.Flip && x.Self.Var("gandoraturn") != x.D.Turn)
                { x.Self.Vars["gandoraturn"] = x.D.Turn; await x.D.ToGrave(x.Self, x.Self); }
            };
        }
        {
            var s = S("Gear Golem the Moving Fortress");
            s.OncePerTurn = true;
            s.Can = x => Up(x) && x.D.Phase == Phase.Main1 && x.My.Lp > 800;
            s.Cost = async x => x.D.Pay(x.Me, 800);
            s.Run = async x => { if (Up(x)) x.Self.Vars["direct"] = x.D.Turn; };
        }
        {
            var s = S("Gearfried the Swordmaster");
            s.SummonOk = (x, by, k) => by?.Name == "Release Restraint";
            s.On = async (x, e) =>
            {
                if (e.Kind == EvKind.Equipped && e.Other == x.Self && Up(x) && x.Targetable(x.OppMonsters()).Any())
                { var t = await x.Pick(x.Targetable(x.OppMonsters()), "Destroy which monster?"); if (t != null) await x.D.Destroy(t, x.Self); }
            };
        }
        {
            var s = S("Gemini Imps");
            s.AddFx(f =>
            {
                f.Label = "Negate a discard"; f.FromHand = true; f.Speed = 2; f.AnyTurn = true;
                f.Can = x => x.Ev is { Kind: EvKind.Activated, Link: { } l } && l.Player == x.Opp && DiscardsOnResolve(l.Card.Data.Desc);
                f.Cost = async x => await x.D.ToGrave(x.Self, x.Self);
                f.Run = async x =>
                {
                    var l = x.Ev!.Link!;
                    await x.D.Negate(l);
                    if (!l.Card.OnField) await x.D.Draw(x.Me, 1);
                };
            });
            s.Note = "Answers any opponent's card whose text mentions discarding.";
        }
        S("Gernia").On = async (x, e) =>
        {
            if (e.Is(EvKind.Destroyed, x.Self) && !e.Battle && e.Other?.Controller == x.Opp && x.Self.Loc == Loc.Grave && x.D.LeftFieldLog.LastOrDefault(l => l.c == x.Self) is { c: not null } l && l.faceUp && l.controller == x.Me)
                x.Self.Vars["gerniaturn"] = x.D.Turn;
            if (e.Kind == EvKind.Standby && e.Player == x.Me && x.Self.Loc == Loc.Grave && x.Self.Var("gerniaturn") > 0 && x.Self.Var("gerniaturn") < x.D.Turn)
            {
                x.Self.Vars["gerniaturn"] = 0;
                if (x.D.CanSpecialSummon(x.Self, x.Me, by: x.Self)) await x.D.SpecialSummon(x.Self, x.Me, await x.D.ChoosePosition(x.Me, x.Self), by: x.Self);
            }
        };
        S("Ghost Knight of Jackal").On = async (x, e) =>
        {
            if (e.Kind == EvKind.BattleDestroyedMonster && e.Card == x.Self && e.Other is { Loc: Loc.Grave } d && d.Owner == x.Opp && x.D.CanSpecialSummon(d, x.Me, by: x.Self)
                && await x.Ask($"Special Summon {d.Name} to your side in face-up Defense Position?"))
                await x.D.SpecialSummon(d, x.Me, true, by: x.Self);
        };
        {
            var s = S("Giant Axe Mummy");
            s.OncePerTurn = true;
            s.Can = x => Up(x);
            s.Run = async x => { if (Up(x)) await x.D.SetFaceDown(x.Self); };
            s.On = async (x, e) =>
            {
                if (e.Kind == EvKind.AfterDamage && e.Other == x.Self && x.D.TargetWasSet && x.Self.Valid() && e.Card is { } a && a.Valid() && x.Atk(a) < x.Def(x.Self)) await x.D.Destroy(a, x.Self);
            };
        }
        S("Giant Orc").On = async (x, e) =>
        {
            if (e.Kind == EvKind.BattleEnd && Up(x) && x.Self.Attacks > 0) { await x.D.ChangePosition(x.Self, true); x.Self.Vars["nochangepos"] = x.D.Turn + 2; }
        };
        {
            var s = S("Giant Rex");
            s.Forbid = (x, q, c) => q == Q.DirectAttack && c == x.Self;
            s.On = async (x, e) =>
            {
                if (e.Is(EvKind.Banished, x.Self) && Hopt(x) && x.D.CanSpecialSummon(x.Self, x.Self.Owner, by: x.Self) && await x.Ask("Special Summon Giant Rex?"))
                {
                    UseHopt(x);
                    int n = x.D.P(x.Self.Owner).Banished.Count(c => c != x.Self && c.Data.IsMonster && c.Data.Race == "Dinosaur");
                    if (await x.D.SpecialSummon(x.Self, x.Self.Owner, await x.D.ChoosePosition(x.Self.Owner, x.Self), by: x.Self)) { x.Self.Vars["modatk"] = 200 * n; x.D.Seq++; }
                }
            };
        }
        Spell("Gift of the Martyr", async x =>
        {
            var t = await x.Pick(x.MyMonsters(), "Which monster gains the ATK?");
            if (t != null) x.D.Boost(t, x.N);
        }, x => x.MyMonsters().Count() >= 2,
        async x => { var m = await x.Pick(x.MyMonsters(), "Send which monster to the GY?"); if (m != null) { x.N = m.FaceUp ? x.Atk(m) : m.Data.Atk; await x.D.ToGrave(m, x.Self); } });
        {
            var s = S("Gilford the Legend");
            s.SummonOk = (x, by, k) => false;
            s.On = async (x, e) =>
            {
                if (!e.Is(EvKind.Summoned, x.Self) || !e.Summoned(SummonKind.Normal, SummonKind.Tribute) || !x.My.Grave.Any(c => c.Data.IsSpell && c.Data.Is("Equip")) || !await x.Ask("Equip Equip Spells from your GY to your Warrior monsters?")) return;
                foreach (var eq in x.My.Grave.Where(c => c.Data.IsSpell && c.Data.Is("Equip")).ToList())
                {
                    if (x.My.FreeSpellZones == 0) break;
                    var hosts = x.MyMonsters().FaceUp().Where(m => x.IsRace(m, "Warrior") && (x.D.S(eq)?.EquipOk?.Invoke(x.D.X(eq), m) ?? true)).ToList();
                    if (hosts.Count == 0) continue;
                    var m = await x.Pick(hosts, $"Equip {eq.Name} to which Warrior?");
                    if (m != null) await x.D.Equip(eq, m);
                }
            };
        }
        {
            var s = S("Gilford the Lightning");
            s.TributeAny = (x, t) => true;
            s.TributeMax = 3;
            s.On = async (x, e) => { if (e.Is(EvKind.Summoned, x.Self) && e.Arg == (int)SummonKind.Tribute && x.D.LastTributes.Count == 3) await x.D.Destroy(x.OppMonsters().ToList(), x.Self); };
            s.Note = "Tributing a 3rd monster (offered after the first 2) summons it \"this way\".";
        }
        {
            var s = S("Gingerbread House");
            s.On = async (x, e) =>
            {
                if (e.Kind != EvKind.Standby || e.Player != x.Opp || !x.Self.OnField || !x.Self.FaceUp || !Hopt(x) || !x.OppMonsters().Any() || !await x.Ask("Gingerbread House: their monsters gain 600 ATK, then destroy those with 2500+?")) return;
                UseHopt(x);
                foreach (var m in x.OppMonsters().FaceUp()) { m.Vars["modatk"] = m.Var("modatk") + 600; }
                x.D.Seq++;
                var l = x.OppMonsters().Where(m => m.FaceUp && x.Atk(m) >= 2500).ToList();
                await x.D.Destroy(l, x.Self);
                await x.D.Gain(x.Me, 500 * l.Count(m => !m.OnField), x.Self);
            };
        }
        {
            var s = S("Glass Slippers");
            s.EquipOk = (x, m) => true;
            s.Stats = (x, c, st) => { if (c == x.Self.EquippedTo) st.Add(st.Race == "Fairy" ? 1000 : -1000, 0); };
            s.Forbid = (x, q, c) => q == Q.Attack && c != null && c == x.Self.EquippedTo && x.D.Race(c) != "Fairy";
            s.On = async (x, e) =>
            {
                if (e.Is(EvKind.Equipped, x.Self)) Tie(x, e.Other!);
                if (e.Kind == EvKind.Destroyed && e.Card == TiedMonster(x) && x.Self.Loc == Loc.Grave && Hopt(x) && x.My.FreeSpellZones > 0
                    && x.Targetable(x.MyMonsters().FaceUp().Where(m => m.In("Prinzessin"))).Any() && await x.Ask("Equip Glass Slippers to a Prinzessin?"))
                {
                    UseHopt(x);
                    var t = await x.Pick(x.Targetable(x.MyMonsters().FaceUp().Where(m => m.In("Prinzessin"))), "Equip to which Prinzessin?");
                    if (t != null) await x.D.Equip(x.Self, t);
                }
            };
        }
        {
            var s = S("Glife the Phantom Bird");
            s.AddFx(f =>
            {
                f.Label = "Add Golden Castle of Stromberg"; f.FromHand = true;
                f.Can = x => x.My.Deck.Any(c => c.Name == "Golden Castle of Stromberg");
                f.Cost = async x => await x.D.Discard(x.Me, new[] { x.Self }, x.Self);
                f.Run = x => x.Search(c => c.Name == "Golden Castle of Stromberg", "Add Golden Castle of Stromberg");
            });
            s.On = async (x, e) =>
            {
                if (e.Is(EvKind.Summoned, x.Self) && e.Arg != (int)SummonKind.Flip && Hopt(x) && x.Targetable(x.Their.Spells.Where(c => c != null).Select(c => c!)).Any() && await x.Ask("Destroy 1 card in your opponent's Spell & Trap Zone?"))
                { UseHopt(x); var t = await x.Pick(x.Targetable(x.Their.Spells.Where(c => c != null).Select(c => c!)), "Destroy which card?"); if (t != null) await x.D.Destroy(t, x.Self); }
            };
        }
        {
            var s = S("Goblin King");
            s.Forbid = (x, q, c) => q == Q.BeAttacked && c == x.Self && x.MyMonsters().Any(m => m != x.Self && m.FaceUp && x.IsRace(m, "Fiend"));
            s.Base = (x, c, st) => { if (c == x.Self) { int n = 1000 * x.D.FaceUpMonsters.Count(m => m != x.Self && m.Data.Race == "Fiend"); st.BaseAtk = n; st.BaseDef = n; } };
        }
        {
            var s = OnBattleDamage("Goblin Zombie", async (x, e) => { if (x.Their.Deck.Count > 0) await x.D.ToGrave(x.Their.Deck[0], x.Self); });
            var hit = s.On!;
            s.On = async (x, e) =>
            {
                await hit(x, e);
                if (e.Is(EvKind.ToGrave, x.Self) && e.From == Loc.Monster) await x.Search(c => c.Data.IsMonster && c.Data.Race == "Zombie" && c.Data.Def <= 1200, "Add a Zombie with 1200 or less DEF");
            };
        }
        Sure("Foolish Burial", "Fusion Weapon", "Fusion Recovery", "Goblin King", "Gear Golem the Moving Fortress", "Fuh-Rin-Ka-Zan");
    }
}
