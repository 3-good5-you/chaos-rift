namespace PoC.Engine;

// Official cards, batch 17 (Magician's Valkyria .. Mirage Tube).
public static partial class Scripts
{
    /// "Monsters your opponent controls cannot target [these] for attacks, except this one."
    static void ProtectsOthers(Script s, Func<Ctx, Card, bool> which) =>
        s.Forbid = (x, q, c) => q == Q.BeAttacked && Up(x) && c != null && c != x.Self && c.Controller == x.Me && which(x, c)
            && x.D.Querying is { } a && a.Controller == x.Opp;

    /// Pick one card for each listed material from `pool` (each used once).
    static async Flow<List<Card>?> PickMats(Ctx x, List<Mat> mats, List<Card> pool)
    {
        var used = new List<Card>();
        foreach (var m in mats)
        {
            var l = pool.Where(c => !used.Contains(c) && m.Ok(c)).ToList();
            var c = await x.Pick(l, $"Material: {m.Label}");
            if (c == null) return null;
            used.Add(c);
        }
        return used;
    }

    static bool HasMats(List<Mat> mats, List<Card> pool, int i = 0, HashSet<Card>? used = null)
    {
        used ??= new();
        if (i == mats.Count) return true;
        foreach (var c in pool.Where(c => !used.Contains(c) && mats[i].Ok(c)))
        {
            used.Add(c);
            if (HasMats(mats, pool, i + 1, used)) return true;
            used.Remove(c);
        }
        return false;
    }

    static void CardsP17(CardDb db)
    {
        ProtectsOthers(S("Magician's Valkyria"), (x, c) => c.FaceUp && x.IsRace(c, "Spellcaster"));
        Spell("Magicians Unite", async x =>
        {
            if (x.T0 is not { Loc: Loc.Monster, FaceUp: true } t) return;
            x.D.Boost(t, 3000 - x.Atk(t));
            var d = x.D; int me = x.Me;
            d.Linger(d.Turn, (q, c, p) => q == Q.Attack && c != null && c != t && c.Controller == me && d.Race(c) == "Spellcaster");
        }, x => x.MyMonsters().Count(m => m.FaceUp && m.Attack && x.IsRace(m, "Spellcaster")) >= 2,
        async x => await x.Target(x.MyMonsters().Where(m => m.FaceUp && m.Attack && x.IsRace(m, "Spellcaster")), "Which Spellcaster's ATK becomes 3000?"));
        Spell("Magnet Circle LV2", async x => await SummonPick(x, x.My.Hand.Where(c => c.Data.IsMonster && c.Data.Race == "Machine" && c.Data.Level <= 2), "Special Summon which Machine?"),
            x => x.D.FreeMonsterZones(x.Me) > 0 && x.My.Hand.Any(c => c.Data.IsMonster && c.Data.Race == "Machine" && c.Data.Level <= 2));
        Spell("Magnet Force", async x =>
        {
            var d = x.D; int opp = x.Opp;
            d.Linger(d.Turn, (q, c, p) => q == Q.AffectedByMonster && c != null && c.Data.Race is "Machine" or "Rock" && d.Affecting is { } a && a != c && a.Controller == opp);
        });
        {
            static bool NoNormal(Card c) => c.Data.IsMonster && c.Data.Race is "Machine" or "Rock" && System.Text.RegularExpressions.Regex.IsMatch(c.Data.Desc, "cannot be Normal Summoned", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            Spell("Magnet Reverse", async x => { if (x.T0 is { Loc: Loc.Grave or Loc.Banished } t) await x.D.SpecialSummon(t, x.Me, await x.D.ChoosePosition(x.Me, t), by: x.Self); },
                x => x.Targetable(x.My.Grave.Concat(x.My.Banished.Where(c => c.FaceUp)).Where(c => NoNormal(c) && x.D.CanSpecialSummon(c, x.Me, by: x.Self))).Any(),
                async x => await x.Target(x.My.Grave.Concat(x.My.Banished.Where(c => c.FaceUp)).Where(c => NoNormal(c) && x.D.CanSpecialSummon(c, x.Me, by: x.Self)), "Special Summon which monster?"));
        }
        ReturnsAtEnd(S("Maharaghi"), async (x, e) =>
        {
            if (!(e.Is(EvKind.Summoned, x.Self) && e.Summoned(SummonKind.Normal, SummonKind.Tribute, SummonKind.Flip))) return;
            var d = x.D; int me = x.Me; var self = x.Self; bool done = false;
            d.Watch(int.MaxValue, async ev =>
            {
                if (done || ev.Kind != EvKind.DrawPhase || ev.Player != me || d.P(me).Deck.Count == 0) return;
                done = true;
                var top = d.P(me).Deck[0];
                d.Reveal(me, top);
                if (await d.ChooseText(me, $"Maharaghi: {top.Name}", new[] { "Keep it on top", "Put it on the bottom" }, self) == 1)
                { d.P(me).Deck.RemoveAt(0); d.P(me).Deck.Add(top); d.Seq++; }
            });
        });
        {
            var s = S("Maiden in Love");
            s.Allow = (x, q, c) => q == Q.MustBeAttacked && c == x.Self && Up(x);
            s.Forbid = (x, q, c) => q == Q.DestroyByBattle && c == x.Self;
            s.On = async (x, e) =>
            {
                if (e.Kind != EvKind.AfterDamage || !Up(x)) return;
                var o = e.Card == x.Self ? e.Other : e.Other == x.Self ? e.Card : null;
                if (o == null || o.Controller != x.Opp) return;
                var marked = x.OppMonsters().Where(m => m.Counters("maiden") > 0).ToList();
                var opts = new List<string> { "Place a Maiden Counter", "Take control of a monster with a Maiden Counter", "No" };
                int i = await x.D.ChooseText(x.Me, "Maiden in Love: activate an effect?", opts, x.Self);
                if (i == 0 && x.OppMonsters().Any(m => m.FaceUp)) { var t = await x.Pick(x.OppMonsters().FaceUp(), "Place a Maiden Counter on which monster?"); if (t != null) AddCounter(x, t, 1, "maiden"); }
                if (i == 1 && marked.Count > 0) { var t = await x.Pick(marked, "Take control of which monster?"); if (t != null) await x.D.TakeControl(t, x.Me); }
            };
        }
        S("Maiden of the Aqua").Note = "While no Field Spell is active, cards that check for \"Umi\" see it, but Umi's ATK/DEF changes do not apply.";
        {
            var s = S("Maju Garzett");
            s.On = async (x, e) => { if (e.Is(EvKind.Summoned, x.Self) && e.Arg == (int)SummonKind.Tribute) { x.Self.Vars["maju"] = x.D.LastTributes.Sum(t => Math.Max(0, t.Data.Atk)); x.D.Seq++; } };
            s.Base = (x, c, st) => { if (c == x.Self) st.BaseAtk = x.Self.Var("maju"); };
        }
        S("Makyura the Destructor").On = async (x, e) =>
        {
            if (e.Kind == EvKind.ToGrave && e.Card == x.Self && e.From == Loc.Monster && Hopt(x)) { UseHopt(x); x.D.TurnFlags.Add($"traphand{x.Me}"); }
        };
        Spell("Malevolent Catastrophe", async x => await x.D.Destroy(x.AllSpells().Where(c => c != x.Self).ToList(), x.Self), x => AttackOnMe(x) && x.AllSpells().Any(c => c != x.Self));
        Spell("Malfunction", async x =>
        {
            var l = x.Ev!.Link!;
            await x.D.Negate(l, destroy: false);
            if (l.Card.Loc is Loc.Spell or Loc.Field) { l.Card.FaceUp = false; l.Card.SetTurn = x.D.Turn; x.D.Seq++; }
        }, x => x.Ev is { Kind: EvKind.Activated, Link: { IsCardActivation: true } l } && l.Card.Data.IsTrap && l.Card != x.Self && x.My.Lp > 500,
        async x => x.D.Pay(x.Me, 500, x.Self));
        S("Malice Doll of Demise").On = async (x, e) =>
        {
            if (e.Kind != EvKind.ToGrave || e.Card != x.Self || e.From != Loc.Monster || e.Other is not { } by || !by.Data.IsSpell || !by.Data.Is("Continuous")) return;
            var d = x.D; int me = x.Me; var self = x.Self; bool done = false;
            d.Watch(int.MaxValue, async ev =>
            {
                if (done || ev.Kind != EvKind.Standby || ev.Player != me) return;
                done = true;
                if (self.Loc == Loc.Grave && d.CanSpecialSummon(self, me, by: self)) await d.SpecialSummon(self, me, await d.ChoosePosition(me, self), by: self);
            });
        };
        OnSummon("Manju of the Ten Thousand Hands", x => x.Search(c => c.Data.IsRitual || (c.Data.IsSpell && c.Data.Is("Ritual")), "Add a Ritual Monster or Ritual Spell?", optional: true), normalOnly: true, includeFlip: true);
        {
            var s = S("Marauding Captain");
            ProtectsOthers(s, (x, c) => c.FaceUp && x.IsRace(c, "Warrior"));
            s.On = async (x, e) =>
            {
                if (e.Is(EvKind.Summoned, x.Self) && e.Summoned(SummonKind.Normal, SummonKind.Tribute) && x.My.Hand.Any(c => c.Data.IsMonster && c.Data.Level <= 4 && x.D.CanSpecialSummon(c, x.Me, by: x.Self)))
                    await SummonPick(x, x.My.Hand.Where(c => c.Data.IsMonster && c.Data.Level <= 4), "Special Summon a Level 4 or lower monster?", optional: true);
            };
        }
        {
            var s = S("Marshmallon");
            s.Forbid = (x, q, c) => q == Q.DestroyByBattle && c == x.Self;
            s.On = async (x, e) => { if (e.Kind == EvKind.AfterCalc && e.Other == x.Self && x.D.TargetWasSet && e.Card != null) await x.D.Damage(e.Card.Controller, 1000, x.Self); };
        }
        {
            var s = Spell("Marshmallon Glasses", async x => x.Self.Vars["stay"] = 1);
            s.Forbid = (x, q, c) => q == Q.BeAttacked && x.Self.OnField && x.Self.FaceUp && c != null && c.Controller == x.Me && c.Name != "Marshmallon"
                && x.MyMonsters().Any(m => m.Name == "Marshmallon") && x.D.Querying is { } a && a.Controller == x.Opp;
        }
        {
            static bool Key(Card m) => Scripts.NameOf(m) is "Grand Tiki Elder" or "Melchid the Four-Face Beast";
            var s = S("Masked Beast Des Gardius");
            s.SummonOk = (x, by, k) => by == x.Self;
            s.ProcCan = x => x.D.Tributable(x.Me).Count() >= 2 && x.D.Tributable(x.Me).Any(Key);
            s.Proc = async x =>
            {
                var a = await x.Pick(x.D.Tributable(x.Me).Where(Key), "Tribute Grand Tiki Elder or Melchid the Four-Face Beast");
                var b = a == null ? null : await x.Pick(x.D.Tributable(x.Me).Where(m => m != a), "Tribute 1 more monster");
                if (a == null || b == null) return;
                await x.D.Tribute(a, x.Self); await x.D.Tribute(b, x.Self);
                await x.D.SpecialSummon(x.Self, x.Me, await x.D.ChoosePosition(x.Me, x.Self), by: x.Self);
            };
            s.On = async (x, e) =>
            {
                if (e.Kind != EvKind.ToGrave || e.Card != x.Self || e.From != Loc.Monster || x.My.FreeSpellZones == 0 || !x.D.AllMonsters.Any()) return;
                var mask = x.My.Deck.FirstOrDefault(c => c.Name == "The Mask of Remnants");
                if (mask == null) return;
                var t = await x.Pick(x.D.AllMonsters, "Equip The Mask of Remnants to which monster?");
                if (t == null) return;
                x.D.Place(mask, x.Me, Loc.Spell); mask.FaceUp = true;
                await x.D.Equip(mask, t);
                x.D.Shuffle(x.Me);
            };
        }
        OnBattleDeath("Masked Dragon", x => x.Recruit(c => c.Data.Race == "Dragon" && c.Data.Atk is >= 0 and <= 1500, "Masked Dragon: Special Summon a Dragon with 1500 or less ATK"));
        {
            var s = S("Mathematician");
            s.On = async (x, e) =>
            {
                if (e.Is(EvKind.Summoned, x.Self) && e.Summoned(SummonKind.Normal, SummonKind.Tribute) && x.My.Deck.Any(c => c.Data.IsMonster && c.Data.Level <= 4) && await x.Ask("Send a Level 4 or lower monster from your Deck to the GY?"))
                { var c = await x.Pick(x.My.Deck.Where(c => c.Data.IsMonster && c.Data.Level <= 4), "Send which monster?"); if (c != null) await x.D.ToGrave(c, x.Self); x.D.Shuffle(x.Me); }
                if (e.Is(EvKind.Destroyed, x.Self) && e.Battle && x.Self.Loc == Loc.Grave && await x.Ask("Draw 1 card?")) await x.D.Draw(x.Me, 1);
            };
        }
        {
            var s = S("Mausoleum of the Emperor");
            s.FreeTribute = (x, c, set) =>
            {
                int need = Math.Min(2, x.D.TributesFor(c));
                if (!x.Self.FaceUp || x.Self.Loc != Loc.Field || c.Controller != x.D.TurnPlayer || need == 0 || x.D.P(c.Controller).Lp <= 1000 * need || x.D.Phase is not (Phase.Main1 or Phase.Main2)) return false;
                x.D.Flags[$"mausoleum{c.Controller}"] = need;
                return true;
            };
            s.OnFreeTribute = async x => { int p = x.D.TurnPlayer; x.D.Pay(p, 1000 * x.D.Flags.GetValueOrDefault($"mausoleum{p}"), x.Self); };
            s.Note = "When you Normal Summon a monster that needs Tributes, the game offers to pay 1000 LP per Tribute (max. 2000) instead.";
        }
        S("Maximum Six").On = async (x, e) =>
        {
            if (e.Is(EvKind.Summoned, x.Self) && e.Arg == (int)SummonKind.Tribute) { int r = x.D.Die(x.Me, x.Self); if (Up(x)) { x.Self.Vars["modatk"] = x.Self.Var("modatk") + 200 * r; x.D.Seq++; } }
        };
        {
            var s = S("Mefist the Infernal General");
            s.Allow = (x, q, c) => q == Q.Piercing && c == x.Self;
            s.On = async (x, e) => { if (e.Kind == EvKind.BattleDamage && e.Card == x.Self && e.Player == x.Opp && x.Their.Hand.Count > 0) await x.D.DiscardRandom(x.Opp, 1, x.Self); };
        }
        {
            static IEnumerable<Card> Rocks(Ctx x) => x.My.Grave.Where(c => c.Data.IsMonster && c.Data.Race == "Rock");
            var s = S("Megarock Dragon");
            s.SummonOk = (x, by, k) => by == x.Self;
            s.ProcCan = x => Rocks(x).Any();
            s.Proc = async x =>
            {
                var l = await x.PickN(Rocks(x), 1, Rocks(x).Count(), "Banish how many Rock monsters?");
                foreach (var c in l) await x.D.Banish(c, x.Self);
                if (await x.D.SpecialSummon(x.Self, x.Me, await x.D.ChoosePosition(x.Me, x.Self), by: x.Self)) { x.Self.Vars["megarock"] = l.Count; x.D.Seq++; }
            };
            s.Base = (x, c, st) => { if (c == x.Self) { st.BaseAtk = 700 * x.Self.Var("megarock"); st.BaseDef = st.BaseAtk; } };
        }
        S("Mermaid Knight").ExtraAttacks = x => x.D.CountActive("Umi") > 0 ? 1 : 0;
        {
            var s = Spell("Metal Reflect Slime", async x => await TrapMonster(x, 0, 3000, "WATER", "Aqua", 10, defense: true));
            s.Forbid = (x, q, c) => q == Q.Attack && c == x.Self;
        }
        {
            var s = Union("Metallizing Parasite - Lunatite", (x, m) => true);
            s.Forbid = (x, q, c) => q == Q.AffectedBySpell && x.Self.Var("union") == 1 && c != null && c == x.Self.EquippedTo && (x.D.Affecting ?? x.D.Targeter) is { } by && by.Controller == x.Opp;
        }
        Spell("Metamorphosis", async x =>
        {
            int lv = x.Self.Var("metalv");
            await SummonPick(x, x.My.Extra.Where(c => c.Data.IsFusion && c.Data.Level == lv), "Special Summon which Fusion Monster?");
        }, x => x.D.Tributable(x.Me).Any(m => x.My.Extra.Any(c => c.Data.IsFusion && c.Data.Level == x.D.Level(m) && x.D.CanSpecialSummon(c, x.Me, by: x.Self, ignoreZones: true))),
        async x =>
        {
            var t = await x.Pick(x.D.Tributable(x.Me).Where(m => x.My.Extra.Any(c => c.Data.IsFusion && c.Data.Level == x.D.Level(m) && x.D.CanSpecialSummon(c, x.Me, by: x.Self, ignoreZones: true))), "Tribute which monster?");
            if (t == null) return;
            x.Self.Vars["metalv"] = x.D.Level(t);
            await x.D.Tribute(t, x.Self);
        });
        Spell("Meteor of Destruction", async x => await x.D.Damage(x.Opp, 1000, x.Self), x => x.Their.Lp > 3000);
        Spell("Meteorain", async x =>
        {
            var d = x.D; int me = x.Me, turn = x.D.Turn;
            foreach (var m in x.MyMonsters()) m.Vars["pierce"] = turn;
            d.Watch(turn, async ev => { if (ev.Kind == EvKind.AttackDeclared && ev.Card?.Controller == me && d.Turn == turn) ev.Card.Vars["pierce"] = turn; });
        });
        Spell("Micro Ray", async x => { if (x.T0 is { Loc: Loc.Monster, FaceUp: true } t) x.D.Boost(t, 0, -x.Def(t)); },
            x => x.Targetable(x.D.FaceUpMonsters).Any(), async x => await x.Target(x.D.FaceUpMonsters, "Which monster's DEF becomes 0?"));
        {
            var s = S("Mid Shield Gardna");
            FlipDown(s);
            s.On = async (x, e) =>
            {
                if (e.Kind != EvKind.Activated || e.Link is not { } l || !l.Card.Data.IsSpell || x.Self.Loc != Loc.Monster || x.Self.FaceUp) return;
                if (!l.X.T.Contains(x.Self) || l.X.T.Count != 1 || !await x.Ask("Mid Shield Gardna: negate that Spell?")) return;
                await x.D.ChangePosition(x.Self, true, faceUp: true);
                await x.D.Negate(l);
            };
        }
        Spell("Mind Control", async x =>
        {
            if (x.T0 is not { Loc: Loc.Monster } t || t.Controller != x.Opp || !await x.D.TakeControl(t, x.Me)) return;
            var d = x.D; int me = x.Me; int turn = d.Turn;
            t.Vars["noattack"] = turn;
            d.Linger(turn, (q, c, p) => q == Q.Tribute && c == t);
            d.Later(turn, Phase.End, async () => { if (t.Loc == Loc.Monster && t.Controller == me) await d.TakeControl(t, t.Owner); });
        }, x => x.D.FreeMonsterZones(x.Me) > 0 && x.Targetable(x.OppMonsters()).Any(), async x => await x.Target(x.OppMonsters(), "Take control of which monster?"));
        {
            var s = S("Mind on Air");
            s.On = async (x, e) => { if (e.Kind is EvKind.MainStart or EvKind.Drew && Up(x) && x.Their.Hand.Count > 0) x.D.Reveal(x.Me, x.Their.Hand); };
            s.Note = "Your opponent's hand is shown to you at each Main Phase and whenever they draw.";
        }
        OnBattleDeath("Mine Golem", async x => await x.D.Damage(x.Opp, 500, x.Self));
        Spell("Minefield Eruption", async x =>
        {
            var l = x.MyMonsters().Where(m => m.FaceUp && m.Name == "Mine Golem").ToList();
            if (l.Count > 0) await x.D.Damage(x.Opp, 1000 * l.Count, x.Self);
            await x.D.Destroy(x.MyMonsters().Where(m => m.FaceUp && m.Name == "Mine Golem").ToList(), x.Self);
        }, x => x.MyMonsters().Any(m => m.FaceUp && m.Name == "Mine Golem"));
        {
            static List<Card> Pool(Ctx x) => x.My.Hand.Concat(x.MyMonsters()).Concat(x.My.Grave).Where(c => c.Data.IsMonster && c != x.Self).ToList();
            static IEnumerable<Card> Results(Ctx x) => x.My.Extra.Where(f => f.Data.IsFusion && f.In("Elemental HERO") && Mentions(f.Data, "Elemental HERO Neos")
                && x.D.S(f)?.Materials is { } m && HasMats(m, Pool(x)) && x.D.CanSpecialSummon(f, x.Me, by: x.Self, ignore: true));
            Spell("Miracle Contact", async x =>
            {
                var f = await x.Pick(Results(x), "Special Summon which Fusion Monster?");
                if (f == null) return;
                var mats = await PickMats(x, x.D.S(f)!.Materials!, Pool(x));
                if (mats == null) return;
                foreach (var c in mats) await x.D.ToDeck(c);
                x.D.Shuffle(x.Me);
                await x.D.SpecialSummon(f, x.Me, await x.D.ChoosePosition(x.Me, f), by: x.Self, ignore: true);
            }, x => Results(x).Any());
        }
        {
            var s = S("Miracle Flipper");
            s.SummonOk = (x, by, k) => !x.D.P(x.Self.Controller).MonstersOnField.Any(m => m.FaceUp && m.Name == "Miracle Flipper");
            s.NormalSummonOk = x => !x.MyMonsters().Any(m => m.FaceUp && m.Name == "Miracle Flipper");
            ProtectsOthers(s, (x, c) => c.FaceUp);
            s.On = async (x, e) =>
            {
                if (e.Is(EvKind.Destroyed, x.Self) && e.Battle && x.Self.Loc == Loc.Grave)
                {
                    var d = x.D; var self = x.Self; int opp = x.Opp; bool done = false;
                    d.Watch(d.Turn, async ev => { if (!done && ev.Kind == EvKind.BattleEnd) { done = true; if (self.Loc == Loc.Grave && d.CanSpecialSummon(self, opp, by: self)) await d.SpecialSummon(self, opp, false, by: self); } });
                }
                if (e.Is(EvKind.Destroyed, x.Self) && !e.Battle && e.Other is { } by && !by.Data.IsMonster && x.OppMonsters().Any())
                { var t = await x.Pick(x.OppMonsters(), "Destroy which monster your opponent controls?"); if (t != null) await x.D.Destroy(t, x.Self); }
            };
        }
        Spell("Miracle Fusion", async x => await x.D.FusionSummon(x.Me, x.Self, banishMats: true, pool: x.MyMonsters().Concat(x.My.Grave.Where(c => c.Data.IsMonster)), resultOk: f => f.In("Elemental HERO")),
            x => x.D.FusionOptions(x.Me, pool: x.MyMonsters().Concat(x.My.Grave.Where(c => c.Data.IsMonster)), resultOk: f => f.In("Elemental HERO"), by: x.Self).Any());
        Spell("Miracle Kids", async x => { if (x.T0 is { Loc: Loc.Monster, FaceUp: true } t) x.D.Boost(t, -400 * x.My.Grave.Count(c => c.Name == "Hero Kid")); },
            x => x.Targetable(x.OppMonsters().FaceUp()).Any(), async x => await x.Target(x.OppMonsters().FaceUp(), "Which monster loses ATK?"));
        Spell("Miracle Rupture", async x =>
        {
            var c = await x.Pick(x.My.Deck.Where(c => c.Data.IsMonster && c.Data.Race == "Rock" && c.Data.Level <= 4), "Send which Rock monster to the GY?");
            x.D.Shuffle(x.Me);
            if (c == null) return;
            await x.D.ToGrave(c, x.Self);
            if (x.My.Grave.Any(g => g.Name == "Fossil Fusion")) await x.D.Draw(x.Me, 1);
        }, x => Hopt(x) && x.My.Deck.Any(c => c.Data.IsMonster && c.Data.Race == "Rock" && c.Data.Level <= 4), async x => UseHopt(x));
        S("Mirage Dragon").Forbid = (x, q, c) => q == Q.Activate && Up(x) && x.D.Phase == Phase.Battle && c != null && c.Data.IsTrap && c.Controller == x.Opp;
        {
            var s = S("Mirage Knight");
            s.SummonOk = (x, by, k) => by?.Name == "Dark Flare Knight";
            s.CalcAtk = (x, c, o, atk) => c == x.Self && o != null ? atk + Math.Max(0, (o.AsMonster ?? o.Data).Atk) : atk;
            s.On = async (x, e) =>
            {
                if (e.Kind == EvKind.AttackDeclared && (e.Card == x.Self || e.Other == x.Self)) x.Self.Vars["miragebattle"] = x.D.Turn;
                if (e.Kind == EvKind.EndPhase && Up(x) && x.Self.Var("miragebattle") == x.D.Turn) await x.D.Banish(x.Self, x.Self);
            };
        }
        {
            var s = Spell("Mirage of Nightmare", async x => x.Self.Vars["stay"] = 1);
            s.On = async (x, e) =>
            {
                if (e.Kind != EvKind.Standby || !x.Self.OnField || !x.Self.FaceUp) return;
                if (e.Player == x.Opp && x.My.Hand.Count < 4 && x.Self.Var("mirageturn") != x.D.Turn)
                {
                    x.Self.Vars["mirageturn"] = x.D.Turn;
                    int n = 4 - x.My.Hand.Count;
                    await x.D.Draw(x.Me, n);
                    x.Self.Vars["miragedrew"] = n;
                }
                else if (e.Player == x.Me && x.Self.Var("miragedrew") > 0)
                {
                    int n = x.Self.Var("miragedrew"); x.Self.Vars["miragedrew"] = 0;
                    await x.D.DiscardRandom(x.Me, Math.Min(n, x.My.Hand.Count), x.Self);
                }
            };
        }
        Spell("Mirage Tube", async x => await x.D.Damage(x.Opp, 1000, x.Self),
            x => x.Self.Loc == Loc.Spell && x.Ev is { Kind: EvKind.AttackDeclared, Other: { FaceUp: true } t } e && e.Player == x.Opp && t.Controller == x.Me);
        Sure("Magnet Circle LV2", "Malevolent Catastrophe", "Manju of the Ten Thousand Hands", "Marauding Captain", "Masked Dragon", "Mathematician", "Maximum Six", "Mefist the Infernal General",
            "Mermaid Knight", "Meteor of Destruction", "Micro Ray", "Mine Golem", "Minefield Eruption", "Miracle Kids", "Miracle Rupture", "Mirage Dragon", "Mirage Tube", "Magician's Valkyria", "Metal Reflect Slime");
    }
}
