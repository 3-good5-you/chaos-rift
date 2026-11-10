using System.Text.RegularExpressions;

namespace PoC.Engine;

// Official cards, batch 15 (Inferno Hammer .. Last Resort).
public static partial class Scripts
{
    /// Instant Neo Space: the equipped Neos Fusion skips its End Phase return to the Extra Deck.
    static bool InstantNeoSpace(Ctx x) => x.D.EquipsOf(x.Self).Any(e => e.FaceUp && e.Name == "Instant Neo Space" && !x.D.Negated(e));

    /// "a Spell Card that would destroy a Spell/Trap Card(s)": its text destroys Spells/Traps.
    static bool DestroysSpells(CardData d) => Regex.IsMatch(d.Desc, @"destroy[^.]*(Spell|Trap)|(Spell|Trap)[^.]*destroy", RegexOptions.IgnoreCase);

    static readonly string[] Kuris = { "Kuribah", "Kuribee", "Kuriboo", "Kuribeh", "Kuriboh" };

    /// Kuribah / Kuribeh: Tribute this card and 1 each of the other four from hand and/or field.
    static IEnumerable<Card> KuriPool(Ctx x) => x.My.Hand.Concat(x.MyMonsters().Where(m => m.FaceUp));
    static bool KuriSet(Ctx x) => Up(x) && Kuris.Where(n => n != x.Self.Name).All(n => KuriPool(x).Any(c => c.Name == n && !x.D.Forbidden(Q.Tribute, c)));
    static async Flow TributeKuris(Ctx x)
    {
        await x.D.Tribute(x.Self, x.Self);
        foreach (var n in Kuris.Where(n => n != x.Self.Name))
            if (KuriPool(x).FirstOrDefault(c => c.Name == n) is { } c) { if (c.Loc == Loc.Hand) await x.D.ToGrave(c, x.Self); else await x.D.Tribute(c, x.Self); }
    }

    /// "When this card, or another Kuriboh monster, you control is destroyed by battle".
    static bool KuriDied(Ctx x, Ev e) => e.Kind == EvKind.Destroyed && e.Battle && e.Card!.In("Kuriboh") && (e.Card == x.Self || Up(x))
        && x.D.LeftFieldLog.LastOrDefault(l => l.c == e.Card) is { c: not null } l && l.controller == x.Me;

    static void CardsP15(CardDb db)
    {
        S("Inferno Hammer").On = async (x, e) =>
        {
            if (KilledToGrave(x, e) && x.Targetable(x.OppMonsters().FaceUp()).Any() && await x.Ask("Flip 1 face-up monster your opponent controls face-down?"))
            { var t = await x.Pick(x.Targetable(x.OppMonsters().FaceUp()), "Flip which monster face-down?"); if (t != null) await x.D.SetFaceDown(t); }
        };
        Spell("Inferno Reckless Summon", async x =>
        {
            var m = x.Ev!.Card!;
            foreach (var c in x.My.Hand.Concat(x.My.Deck).Concat(x.My.Grave).Where(c => c.Name == m.Name).ToList())
                if (x.D.CanSpecialSummon(c, x.Me, by: x.Self)) await x.D.SpecialSummon(c, x.Me, false, by: x.Self);
            x.D.Shuffle(x.Me);
            var mine = x.OppMonsters().FaceUp().ToList();
            if (mine.Count == 0) return;
            var pick = await x.D.PickOne(x.Opp, mine, "Special Summon copies of which monster?", x.Self);
            if (pick == null) return;
            foreach (var c in x.Their.Hand.Concat(x.Their.Deck).Concat(x.Their.Grave).Where(c => c.Name == pick.Name).ToList())
                if (x.D.CanSpecialSummon(c, x.Opp, by: x.Self)) await x.D.SpecialSummon(c, x.Opp, await x.D.ChoosePosition(x.Opp, c), by: x.Self);
            x.D.Shuffle(x.Opp);
        }, x => x.Ev is { Kind: EvKind.Summoned } e && e.Player == x.Me && e.Arg is (int)SummonKind.Special or (int)SummonKind.Fusion or (int)SummonKind.Ritual
            && e.Card is { Loc: Loc.Monster, FaceUp: true } m && x.Atk(m) <= 1500 && x.OppMonsters().Any(o => o.FaceUp));
        Spell("Inferno Tempest", async x =>
        {
            foreach (var p in x.D.Players)
                foreach (var c in p.Deck.Concat(p.Grave).Where(c => c.Data.IsMonster).ToList()) await x.D.Banish(c, x.Self);
        }, x => x.Ev is { Kind: EvKind.BattleDamage } e && e.Player == x.Me && e.Amount >= 3000);
        {
            var s = S("Injection Fairy Lily");
            s.Speed = 2; s.AnyTurn = true;
            s.Can = x => Up(x) && x.My.Lp > 2000 && x.Ev is { Kind: EvKind.DamageCalc } e && (e.Card == x.Self || e.Other == x.Self) && x.Self.Var("lilycalc") != x.D.Seq;
            s.Cost = async x => { x.Self.Vars["lilycalc"] = x.D.Seq; x.D.Pay(x.Me, 2000, x.Self); };
            s.Run = async x => { if (Up(x)) x.Self.Vars["calcatk"] = 3000; };
            s.CalcAtk = (x, c, o, atk) => c == x.Self && x.Self.Var("calcatk") > 0 ? atk + x.Self.Var("calcatk") : atk;
            s.On = async (x, e) => { if (e.Kind == EvKind.AfterDamage && (e.Card == x.Self || e.Other == x.Self)) x.Self.Vars["calcatk"] = 0; };
        }
        {
            var s = S("Insect Princess");
            s.On = async (x, e) =>
            {
                if (e.Kind == EvKind.BattleDestroyedMonster && e.Card == x.Self && Up(x) && e.Other!.Data.Race == "Insect") { x.Self.Vars["modatk"] = x.Self.Var("modatk") + 500; x.D.Seq++; }
                if (Up(x)) foreach (var m in x.OppMonsters().Where(m => m.FaceUp && m.Defense && x.IsRace(m, "Insect")).ToList()) await x.D.ChangePosition(m, false);
            };
            s.Note = "Opponent's face-up Insects are switched to Attack Position whenever anything happens on the field.";
        }
        {
            static IEnumerable<Card> Fusions(Ctx x) => x.My.Extra.Where(c => c.Data.IsFusion && c.Data.Level <= 5 && x.D.S(c)?.MustFusion != true && x.D.CanSpecialSummon(c, x.Me, SummonKind.Fusion, x.Self));
            Spell("Instant Fusion", async x =>
            {
                var c = await x.Pick(Fusions(x), "Special Summon which Fusion Monster?");
                if (c == null || !await x.D.SpecialSummon(c, x.Me, await x.D.ChoosePosition(x.Me, c), kind: SummonKind.Fusion, by: x.Self)) return;
                c.Vars["noattack"] = int.MaxValue;
                var d = x.D; var me = c;
                d.Later(d.Turn, Phase.End, async () => { if (me.Loc == Loc.Monster) await d.Destroy(me); });
            }, x => Hopt(x) && x.My.Lp > 1000 && Fusions(x).Any(), async x => { UseHopt(x); x.D.Pay(x.Me, 1000, x.Self); });
        }
        {
            static bool NeosFusion(Card m) => m.Data.IsFusion && Mentions(m.Data, "Elemental HERO Neos");
            var s = Spell("Instant Neo Space", async x => { if (x.T0 is { } t && t.Valid() && t.FaceUp) await x.D.Equip(x.Self, t); },
                x => x.Targetable(x.MyMonsters().FaceUp().Where(NeosFusion)).Any(),
                async x => await x.Target(x.MyMonsters().FaceUp().Where(NeosFusion), "Equip to which Neos Fusion?"));
            s.EquipOk = (x, m) => NeosFusion(m);
            s.On = async (x, e) =>
            {
                if (e.Kind == EvKind.LeftField && e.Card == x.Self.EquippedTo && x.Self.OnField) x.Self.Vars["neosleft"] = 1;
                if (e.Kind == EvKind.ToGrave && e.Card == x.Self && x.Self.Var("neosleft") == 1)
                {
                    x.Self.Vars["neosleft"] = 0;
                    var l = x.My.Hand.Concat(x.My.Deck).Concat(x.My.Grave).Where(c => c.Name == "Elemental HERO Neos" && x.D.CanSpecialSummon(c, x.Me, by: x.Self)).ToList();
                    if (l.Count > 0 && await x.Ask("Special Summon Elemental HERO Neos from your hand, Deck, or GY?")) { await SummonPick(x, l, "Special Summon Elemental HERO Neos"); x.D.Shuffle(x.Me); }
                }
            };
        }
        Spell("Interdimensional Matter Transporter", async x =>
        {
            if (x.T0 is not { Loc: Loc.Monster } t) return;
            await x.D.Banish(t, x.Self);
            if (t.Loc != Loc.Banished) return;
            var d = x.D; int me = t.Controller == -1 ? x.Me : x.Me;
            d.Later(d.Turn, Phase.End, async () => { if (t.Loc == Loc.Banished && d.CanSpecialSummon(t, t.Owner, ignore: true)) await d.SpecialSummon(t, t.Owner, false, by: null, ignore: true); });
        }, x => x.Targetable(x.MyMonsters().FaceUp()).Any(), async x => await x.Target(x.MyMonsters().FaceUp(), "Banish which monster until the End Phase?"));
        {
            var s = S("Invincible Demise Lord");
            static bool AnyBattleDeath(Ctx x) => x.D.LeftFieldLog.Any(l => l.turn == x.D.Turn && x.D.Flags.ContainsKey($"battledead{l.c.Owner}_{l.c.Uid}"));
            static bool LordDied(Ctx x) => x.D.LeftFieldLog.Any(l => l.turn == x.D.Turn && l.c.Name == "Invincible Demise Lord" && x.D.Flags.GetValueOrDefault($"battledead{l.c.Owner}_{l.c.Uid}") == x.D.Turn);
            s.On = async (x, e) =>
            {
                if (e.Kind != EvKind.EndPhase || x.Self.Loc is not (Loc.Hand or Loc.Grave) || !Hopt(x, "1") || !AnyBattleDeath(x) || !x.D.CanSpecialSummon(x.Self, x.Me, by: x.Self)) return;
                if (!await x.Ask("Special Summon Invincible Demise Lord?")) return;
                UseHopt(x, "1");
                bool lord = LordDied(x);
                if (await x.D.SpecialSummon(x.Self, x.Me, await x.D.ChoosePosition(x.Me, x.Self), by: x.Self) && lord) { x.Self.Vars["lordbuff"] = 1; x.D.Seq++; }
            };
            s.Base = (x, c, st) => { if (c == x.Self && x.Self.Var("lordbuff") == 1) st.BaseAtk = 3000; };
            s.Forbid = (x, q, c) => q == Q.DestroyByEffect && c == x.Self && x.Self.Var("lordbuff") == 1;
            s.OncePerTurn = false;
            s.Can = x => Up(x) && Hopt(x, "2") && x.My.Deck.Any(c => c.Name == "Slash Draw");
            s.Run = async x =>
            {
                UseHopt(x, "2");
                int n = Math.Min(x.My.Deck.Count, x.Their.FieldCards.Count() + 1);
                var l = new List<Card>();   // one at a time, top first; the last pick must be a Slash Draw if none was taken yet
                for (int i = 0; i < n; i++)
                {
                    bool mustSlash = i == n - 1 && !l.Any(c => c.Name == "Slash Draw");
                    var c = await x.Pick(x.My.Deck.Where(c => !l.Contains(c) && (!mustSlash || c.Name == "Slash Draw")), $"Reveal card {i + 1}/{n} for the top of your Deck (first = top){(mustSlash ? ": a Slash Draw" : "")}");
                    if (c == null) break;
                    l.Add(c);
                }
                x.D.Reveal(x.Opp, l);
                foreach (var c in l) x.My.Deck.Remove(c);
                x.D.Shuffle(x.Me);
                x.My.Deck.InsertRange(0, l);
                x.D.Seq++;
            };
        }
        {
            var s = S("Ipiria");
            s.On = async (x, e) => { if (e.Is(EvKind.Summoned, x.Self) && Hopt(x)) { UseHopt(x); await x.D.Draw(x.Me, 1); } };
        }
        {
            static bool Castle(Ctx x) => x.D.Players.Any(p => p.FieldZone is { FaceUp: true } f && f.Name == "Golden Castle of Stromberg");
            var s = Spell("Iron Cage", async x =>
            {
                var pool = x.MyMonsters().Concat(Castle(x) ? x.OppMonsters() : Enumerable.Empty<Card>()).ToList();
                var t = await x.Pick(pool, "Send which monster to the GY?");
                if (t == null) return;
                await x.D.ToGrave(t, x.Self);
                if (t.Loc == Loc.Grave) Tie(x, t);
                x.Self.Vars["stay"] = 1;
            }, x => Hopt(x) && (x.MyMonsters().Any() || (Castle(x) && x.OppMonsters().Any())), async x => UseHopt(x));
            s.On = async (x, e) =>
            {
                if (e.Kind != EvKind.Standby || e.Player != x.Me || !x.Self.OnField || !x.Self.FaceUp || x.Self.Var("stay") != 1) return;
                if (TiedMonster(x) is not { Loc: Loc.Grave } t || !x.D.CanSpecialSummon(t, x.Me, by: x.Self) || !x.Targetable(new[] { t }).Any()) return;
                if (!await x.Ask($"Destroy Iron Cage to Special Summon {t.Name}?")) return;
                await x.D.Destroy(x.Self, x.Self);
                if (!x.Self.OnField && t.Loc == Loc.Grave) await x.D.SpecialSummon(t, x.Me, await x.D.ChoosePosition(x.Me, t), by: x.Self);
            };
        }
        {
            static bool Castle(Ctx x) => x.D.Players.Any(p => p.FieldZone is { FaceUp: true } f && f.Name == "Golden Castle of Stromberg");
            var s = S("Iron Hans");
            s.Stats = (x, c, st) => { if (c == x.Self && Castle(x)) st.Add(1000 * x.MyMonsters().Count(m => m.FaceUp && m.Name == "Iron Knight"), 0); };
            s.On = async (x, e) =>
            {
                if (!e.Is(EvKind.Summoned, x.Self) || !Hopt(x) || !x.My.Deck.Any(c => c.Name == "Iron Knight") || !await x.Ask("Special Summon Iron Knight from your Deck?")) return;
                UseHopt(x);
                await SummonPick(x, x.My.Deck.Where(c => c.Name == "Iron Knight"), "Special Summon Iron Knight");
                x.D.Shuffle(x.Me);
                if (!Castle(x)) { var d = x.D; int me = x.Me; d.Linger(d.Turn, (q, c, p) => q == Q.SpecialSummon && c != null && c.Loc == Loc.Extra && (p == me || c.Owner == me)); }
            };
        }
        {
            static bool Castle(Ctx x) => x.D.Players.Any(p => p.FieldZone is { FaceUp: true } f && f.Name == "Golden Castle of Stromberg");
            var s = S("Iron Knight");
            s.Stats = (x, c, st) => { if (c == x.Self && x.MyMonsters().Any(m => m.FaceUp && m.Name == "Iron Hans")) st.Add(-1000, 0); };
            s.On = async (x, e) =>
            {
                bool battle = e.Is(EvKind.Destroyed, x.Self) && e.Battle;
                bool effect = e.Kind == EvKind.ToGrave && e.Card == x.Self && e.From == Loc.Monster && !x.D.Flags.ContainsKey($"battledead{x.Self.Owner}_{x.Self.Uid}") && e.Other != null;
                if (!(battle || effect) || !Hopt(x)) return;
                var l = x.My.Deck.Where(c => c.Name == "Iron Hans" || (Castle(x) && c.Data.IsMonster && c.Data.Race == "Warrior")).ToList();
                if (l.Count == 0 || !await x.Ask("Add a card from your Deck to your hand?")) return;
                UseHopt(x);
                var c = await x.Pick(l, Castle(x) ? "Add Iron Hans or a Warrior" : "Add Iron Hans");
                if (c != null) { await x.D.ToHand(c, x.Self); x.D.Reveal(x.Opp, c); }
                x.D.Shuffle(x.Me);
            };
        }
        {
            var s = S("Jetroid");
            s.On = async (x, e) =>
            {
                if (e.Kind != EvKind.AttackDeclared || e.Other != x.Self || !Up(x)) return;
                x.D.TurnFlags.Add($"traphand{x.Me}");
            };
            s.Note = "When it is chosen as an attack target, you can activate 1 Trap from your hand (the window stays open for the rest of the turn).";
        }
        {
            var s = S("Jinzo - Lord");
            s.SummonOk = (x, by, k) => by == x.Self;
            s.ProcCan = x => x.MyMonsters().Any(m => m.FaceUp && Scripts.NameOf(m) == "Jinzo");
            s.Proc = async x =>
            {
                var j = await x.Pick(x.MyMonsters().Where(m => m.FaceUp && Scripts.NameOf(m) == "Jinzo"), "Send which Jinzo to the GY?");
                if (j == null) return;
                await x.D.ToGrave(j, x.Self);
                await x.D.SpecialSummon(x.Self, x.Me, await x.D.ChoosePosition(x.Me, x.Self), by: x.Self);
            };
            s.Forbid = (x, q, c) => Up(x) && c != null && c.Data.IsTrap && q is Q.Activate or Q.Negate;
            s.OncePerTurn = true;
            s.Can = x => Up(x) && x.AllSpells().Any(c => c.FaceUp && c.Data.IsTrap);
            s.Run = async x =>
            {
                var l = x.AllSpells().Where(c => c.FaceUp && c.Data.IsTrap).ToList();
                await x.D.Destroy(l, x.Self);
                int n = l.Count(c => !c.OnField);
                if (n > 0) await x.D.Damage(x.Opp, 300 * n, x.Self);
            };
        }
        {
            var s = DirectAttacker("Jinzo - Returner");
            s.On = async (x, e) =>
            {
                if (e.Kind != EvKind.ToGrave || e.Card != x.Self) return;
                var l = x.My.Grave.Where(c => c.Name == "Jinzo" && x.D.CanSpecialSummon(c, x.Me, by: x.Self)).ToList();
                if (l.Count == 0 || !await x.Ask("Special Summon Jinzo from your GY?")) return;
                var j = await x.Pick(l, "Special Summon Jinzo");
                if (j == null || !await x.D.SpecialSummon(j, x.Me, await x.D.ChoosePosition(x.Me, j), by: x.Self)) return;
                var d = x.D; int me = x.Me; bool done = false;
                d.Watch(int.MaxValue, async ev => { if (!done && ev.Kind == EvKind.EndPhase && ev.Player == me) { done = true; if (j.Loc == Loc.Monster) await d.Destroy(j); } });
            };
        }
        Spell("Judgment of Anubis", async x =>
        {
            await x.D.Negate(x.Ev!.Link!);
            var l = x.OppMonsters().FaceUp().ToList();
            if (l.Count == 0 || !await x.Ask("Destroy 1 face-up monster your opponent controls and inflict damage equal to its ATK?")) return;
            var t = await x.Pick(l, "Destroy which monster?");
            if (t == null) return;
            int atk = x.Atk(t);
            await x.D.Destroy(t, x.Self);
            if (!t.OnField) await x.D.Damage(x.Opp, atk, x.Self);
        }, x => x.Ev is { Kind: EvKind.Activated, Link: { IsCardActivation: true } l } && l.Player == x.Opp && l.Card.Data.IsSpell && DestroysSpells(l.Card.Data) && x.My.Hand.Any(c => c != x.Self),
        async x => await x.D.Discard(x.Me, await x.PickN(x.My.Hand.Where(c => c != x.Self), 1, 1, "Discard 1 card"), x.Self));
        {
            var s = S("Juragedo");
            s.AddFx(f =>
            {
                f.Label = "Special Summon (Battle Step)"; f.FromHand = true; f.Speed = 2; f.AnyTurn = true;
                f.Can = x => Hopt(x) && x.D.Phase == Phase.Battle && x.D.CanSpecialSummon(x.Self, x.Me, by: x.Self);
                f.Cost = async x => UseHopt(x);
                f.Run = async x => { if (x.Self.Loc == Loc.Hand && await x.D.SpecialSummon(x.Self, x.Me, await x.D.ChoosePosition(x.Me, x.Self), by: x.Self)) await x.D.Gain(x.Me, 1000, x.Self); };
            });
            s.AddFx(f =>
            {
                f.Label = "Tribute: +1000 ATK"; f.Speed = 2; f.AnyTurn = true;
                f.Can = x => Up(x) && !x.D.Forbidden(Q.Tribute, x.Self) && x.MyMonsters().Any(m => m != x.Self && m.FaceUp);
                f.Cost = async x => { await x.D.Tribute(x.Self, x.Self); await x.Target(x.MyMonsters().FaceUp(), "Which monster gains 1000 ATK?"); };
                f.Run = async x =>
                {
                    if (x.T0 is not { Loc: Loc.Monster, FaceUp: true } t) return;
                    t.Vars["modatk"] = t.Var("modatk") + 1000; x.D.Seq++;
                    var d = x.D; d.Later(d.Turn + 1, Phase.End, async () => { t.Vars["modatk"] = t.Var("modatk") - 1000; d.Seq++; });
                };
            });
            s.Note = "The Battle Step is treated as any time in the Battle Phase.";
        }
        Spell("Jurassic Impact", async x =>
        {
            var l = x.D.AllMonsters.ToList();
            await x.D.Destroy(l, x.Self);
            int n = l.Count(c => !c.OnField);
            if (n > 0) { await x.D.Damage(x.Me, 1000 * n, x.Self); await x.D.Damage(x.Opp, 1000 * n, x.Self); }
            var d = x.D; d.Linger(d.Turn + 1, (q, c, p) => q is Q.NormalSummon or Q.SpecialSummon);
        }, x => x.MyMonsters().Count(m => m.FaceUp && x.IsRace(m, "Dinosaur")) >= 2 && x.My.Lp < x.Their.Lp);
        S("Jurassic World").Stats = (x, c, st) => { if (c.FaceUp && c.Loc == Loc.Monster && x.IsRace(c, "Dinosaur")) st.Add(300, 300); };
        Spell("Justi-Break", async x =>
            await x.D.Destroy(x.D.AllMonsters.Where(m => !(m.FaceUp && m.Attack && m.Data.IsNormalMonster)).ToList(), x.Self),
            x => x.Ev is { Kind: EvKind.AttackDeclared, Other: { FaceUp: true } t } e && e.Player == x.Opp && t.Controller == x.Me && t.Data.IsNormalMonster);
        S("KA-2 Des Scissors").On = async (x, e) => { if (KilledToGrave(x, e)) await x.D.Damage(x.Opp, 500 * e.Other!.Data.Level, x.Self); };
        {
            var s = S("Kaibaman");
            s.Can = x => Up(x) && !x.D.Forbidden(Q.Tribute, x.Self) && x.My.Hand.Any(c => c.Name == "Blue-Eyes White Dragon" && x.D.CanSpecialSummon(c, x.Me, by: x.Self, ignoreZones: true));
            s.Cost = async x => await x.D.Tribute(x.Self, x.Self);
            s.Run = async x => await SummonPick(x, x.My.Hand.Where(c => c.Name == "Blue-Eyes White Dragon"), "Special Summon Blue-Eyes White Dragon");
        }
        {
            var s = S("Kaiser Glider");
            s.Forbid = (x, q, c) => q == Q.DestroyByBattle && c == x.Self && new[] { x.D.Attacker, x.D.AttackTarget }.FirstOrDefault(m => m != null && m != x.Self) is { } o && x.Atk(o) == x.Atk(x.Self);
            s.On = async (x, e) =>
            {
                if (e.Is(EvKind.Destroyed, x.Self) && x.Self.Loc == Loc.Grave && x.Targetable(x.D.AllMonsters).Any())
                { var t = await x.Pick(x.Targetable(x.D.AllMonsters), "Return which monster to the hand?"); if (t != null) await x.D.ToHand(t, x.Self); }
            };
        }
        S("Kaiser Sea Horse").TributeWeight = (x, t, forCard) => forCard.Data.Attribute == "LIGHT" ? 2 : 1;
        {
            var s = S("Kaitoptera");
            s.Forbid = (x, q, c) => q == Q.BeAttacked && c == x.Self && Up(x) && x.D.Querying is { } a && a.Controller == x.Opp && x.D.Attr(a) != "WIND"
                && x.OppMonsters().Count(m => m.FaceUp && x.D.Attr(m) != "WIND") >= 2;
            s.AddFx(f =>
            {
                f.Label = "Add Polymerization";
                f.Can = x => Up(x) && Hopt(x, "1") && x.My.Deck.Any(c => c.Name == "Polymerization");
                f.Cost = async x => UseHopt(x, "1");
                f.Run = async x => await x.Search(c => c.Name == "Polymerization", "Add Polymerization");
            });
            s.On = async (x, e) =>
            {
                if (e.Kind != EvKind.Banished || e.Card != x.Self || !Hopt(x, "2") || !x.D.CanSpecialSummon(x.Self, x.Me, by: x.Self) || !await x.Ask("Special Summon Kaitoptera?")) return;
                UseHopt(x, "2");
                if (!await x.D.SpecialSummon(x.Self, x.Me, await x.D.ChoosePosition(x.Me, x.Self), by: x.Self)) return;
                var l = x.My.Grave.Where(c => c.Name == "Polymerization").ToList();
                if (l.Count > 0 && await x.Ask("Add Polymerization from your GY to your hand?")) await x.D.ToHand(l[0], x.Self);
            };
        }
        S("Kelbek").On = async (x, e) =>
        {
            if (e.Kind == EvKind.AfterDamage && e.Other == x.Self && e.Card is { Loc: Loc.Monster } a && a.Controller == x.Opp) await x.D.ToHand(a, x.Self);
        };
        OnBattleDeath("Keldo", async x =>
        {
            var l = x.Targetable(x.Their.Grave).ToList();
            if (l.Count < 2) return;
            foreach (var c in await x.PickN(l, 2, 2, "Return which 2 cards to your opponent's Deck?")) await x.D.ToDeck(c);
            x.D.Shuffle(x.Opp);
        });
        Spell("Kid Guard", async x =>
        {
            x.D.AttackNegated = true;
            await x.Search(c => EHero(c), "Add an Elemental HERO monster");
        }, x => AttackOnMe(x) && x.D.Tributable(x.Me).Any(m => m.Name == "Hero Kid"),
        async x => { var k = await x.Pick(x.D.Tributable(x.Me).Where(m => m.Name == "Hero Kid"), "Tribute Hero Kid"); if (k != null) await x.D.Tribute(k, x.Self); });
        {
            var s = S("King Dragun");
            s.Forbid = (x, q, c) => q == Q.Target && Up(x) && c != null && c.Loc == Loc.Monster && c.FaceUp && x.IsRace(c, "Dragon") && x.D.Targeter is { } by && by.Controller == x.Opp;
            s.OncePerTurn = true;
            s.Can = x => Up(x) && x.My.Hand.Any(c => c.Data.IsMonster && c.Data.Race == "Dragon" && x.D.CanSpecialSummon(c, x.Me, by: x.Self));
            s.Run = async x => await SummonPick(x, x.My.Hand.Where(c => c.Data.IsMonster && c.Data.Race == "Dragon"), "Special Summon which Dragon?");
        }
        {
            var s = S("King of the Skull Servants");
            s.Base = (x, c, st) => { if (c == x.Self) st.BaseAtk = 1000 * x.My.Grave.Count(g => g.Name is "King of the Skull Servants" or "Skull Servant" || Scripts.NameOf(g) == "Skull Servant"); };
            s.On = async (x, e) =>
            {
                if (!e.Is(EvKind.Destroyed, x.Self) || !e.Battle || x.Self.Loc != Loc.Grave) return;
                var l = x.My.Grave.Where(c => c != x.Self && (c.Name == "King of the Skull Servants" || Scripts.NameOf(c) == "Skull Servant")).ToList();
                if (l.Count == 0 || !x.D.CanSpecialSummon(x.Self, x.Me, by: x.Self) || !await x.Ask("Banish a Skull Servant to Special Summon this card?")) return;
                var b = await x.Pick(l, "Banish which?");
                if (b == null) return;
                await x.D.Banish(b, x.Self);
                await x.D.SpecialSummon(x.Self, x.Me, await x.D.ChoosePosition(x.Me, x.Self), by: x.Self);
            };
        }
        {
            var s = S("King of the Swamp");
            s.Substitute = true;
            s.AddFx(f =>
            {
                f.Label = "Add Polymerization"; f.FromHand = true;
                f.Can = x => x.Self.Loc == Loc.Hand && x.My.Deck.Any(c => c.Name == "Polymerization");
                f.Cost = async x => await x.D.Discard(x.Me, new[] { x.Self }, x.Self);
                f.Run = async x => await x.Search(c => c.Name == "Polymerization", "Add Polymerization");
            });
        }
        S("King's Knight").On = async (x, e) =>
        {
            if (e.Is(EvKind.Summoned, x.Self) && e.Summoned(SummonKind.Normal, SummonKind.Tribute) && x.MyMonsters().Any(m => m.FaceUp && m.Name == "Queen's Knight")
                && x.My.Deck.Any(c => c.Name == "Jack's Knight") && await x.Ask("Special Summon Jack's Knight from your Deck?"))
            { await SummonPick(x, x.My.Deck.Where(c => c.Name == "Jack's Knight"), "Special Summon Jack's Knight"); x.D.Shuffle(x.Me); }
        };
        {
            var s = Union("Kiryu", (x, m) => Scripts.NameOf(m) == "Dark Blade");
            s.Stats = (x, c, st) => { if (x.Self.Var("union") == 1 && c == x.Self.EquippedTo) st.Add(900, 900); };
            s.EffectShield = null;
            s.AddFx(f =>
            {
                f.Label = "Tribute: direct attack"; f.InSpellZone = true;
                f.Can = x => x.Self.Var("union") == 1 && x.Self.EquippedTo is { } m && x.D.TurnPlayer == x.Me && x.D.Phase is Phase.Main1;
                f.Cost = async x => { x.T.Add(x.Self.EquippedTo!); await x.D.Tribute(x.Self, x.Self); };
                f.Run = async x => { if (x.T0 is { Loc: Loc.Monster } m) m.Vars["direct"] = x.D.Turn; };
            });
        }
        S("Kishido Spirit").Forbid = (x, q, c) => q == Q.DestroyByBattle && x.Self.FaceUp && c != null && c.Controller == x.Me && x.D.Attacker is { } a && x.D.AttackTarget is { } t && x.Atk(a) == x.Atk(t);
        {
            var s = S("Knight of Twin Swords");
            s.ExtraAttacks = x => 1;
            s.Note = "It can attack 2 times, but only on monsters.";
            s.Forbid = (x, q, c) => q == Q.DirectAttack && c == x.Self && x.Self.Attacks > 0;
            s.AddFx(f =>
            {
                f.Label = "Special Summon (die card)"; f.FromHand = true;
                f.Can = x => Hopt(x, "1") && x.D.CanSpecialSummon(x.Self, x.Me, by: x.Self) && x.My.FieldCards.Any(c => c.FaceUp && Regex.IsMatch(c.Data.Desc, "roll a (six-sided )?die", RegexOptions.IgnoreCase));
                f.Cost = async x => UseHopt(x, "1");
                f.Run = async x => { if (x.Self.Loc == Loc.Hand) await x.D.SpecialSummon(x.Self, x.Me, await x.D.ChoosePosition(x.Me, x.Self), by: x.Self); };
            });
            s.AddFx(f =>
            {
                f.Label = "Roll 3 dice";
                f.Can = x => Up(x) && Hopt(x, "2");
                f.Cost = async x => UseHopt(x, "2");
                f.Run = async x =>
                {
                    var r = new[] { x.D.Die(x.Me, x.Self), x.D.Die(x.Me, x.Self), x.D.Die(x.Me, x.Self) };
                    if (Up(x)) x.D.Boost(x.Self, 350 * r.Sum());
                    if (r.Distinct().Count() < 3) await SummonPick(x, x.My.Hand.Where(c => c.Data.IsMonster), "Special Summon 1 monster from your hand", optional: true);
                };
            });
        }
        Spell("Knight's Title", async x => { await SummonPick(x, x.My.Hand.Concat(x.My.Deck).Concat(x.My.Grave).Where(c => c.Name == "Dark Magician Knight"), "Special Summon Dark Magician Knight", ignore: true); x.D.Shuffle(x.Me); },
            x => x.D.Tributable(x.Me).Any(m => m.FaceUp && Scripts.NameOf(m) == "Dark Magician") && x.My.Hand.Concat(x.My.Deck).Concat(x.My.Grave).Any(c => c.Name == "Dark Magician Knight"),
            async x => { var t = await x.Pick(x.D.Tributable(x.Me).Where(m => m.FaceUp && Scripts.NameOf(m) == "Dark Magician"), "Tribute Dark Magician"); if (t != null) await x.D.Tribute(t, x.Self); });
        {
            var s = SelfBoost("Kuribabylon", x => 300 * (x.MyMonsters().Count(m => m.FaceUp && m.In("Kuriboh")) + x.My.Grave.Count(c => c.Data.IsMonster && c.In("Kuriboh"))),
                                              x => 300 * (x.MyMonsters().Count(m => m.FaceUp && m.In("Kuriboh")) + x.My.Grave.Count(c => c.Data.IsMonster && c.In("Kuriboh"))));
            s.AddFx(f =>
            {
                f.Label = "Special Summon from hand"; f.FromHand = true;
                f.Can = x => Hopt(x) && x.My.Grave.Count(c => c.Data.IsMonster) > x.Their.Grave.Count(c => c.Data.IsMonster) && x.D.CanSpecialSummon(x.Self, x.Me, by: x.Self);
                f.Cost = async x => UseHopt(x);
                f.Run = async x => { if (x.Self.Loc == Loc.Hand) await x.D.SpecialSummon(x.Self, x.Me, await x.D.ChoosePosition(x.Me, x.Self), by: x.Self); };
            });
            s.AddFx(f =>
            {
                f.Label = "Return: summon 5 Kuribohs";
                f.Can = x => Up(x) && Hopt(x) && x.D.TurnPlayer == x.Me && x.D.Phase is Phase.Main1 or Phase.Battle or Phase.Main2
                    && x.D.FreeMonsterZones(x.Me) + 1 >= 5 && Kuris.All(n => x.My.Hand.Concat(x.My.Grave).Any(c => c.Name == n && x.D.CanSpecialSummon(c, x.Me, by: x.Self, ignoreZones: true)));
                f.Cost = async x => UseHopt(x);
                f.Run = async x =>
                {
                    if (!Up(x)) return;
                    await x.D.ToHand(x.Self, x.Self);
                    if (x.Self.Loc != Loc.Hand || x.D.FreeMonsterZones(x.Me) < 5) return;
                    var l = Kuris.Select(n => x.My.Hand.Concat(x.My.Grave).FirstOrDefault(c => c.Name == n && x.D.CanSpecialSummon(c, x.Me, by: x.Self))).ToList();
                    if (l.Any(c => c == null)) return;
                    foreach (var c in l) await x.D.SpecialSummon(c!, x.Me, false, by: x.Self);
                };
            });
        }
        {
            var s = S("Kuribah");
            s.On = async (x, e) =>
            {
                if (!KuriDied(x, e) || !Hopt(x)) return;
                var l = x.My.Deck.Where(c => c.Data.IsMonster && c.Data.Atk == 300 && c.Data.Def == 200 && c.Name != "Kuribah" && x.D.CanSpecialSummon(c, x.Me, by: x.Self)).ToList();
                if (l.Count == 0 || !await x.Ask("Special Summon a 300 ATK / 200 DEF monster from your Deck?")) return;
                UseHopt(x);
                await SummonPick(x, l, "Special Summon which?");
                x.D.Shuffle(x.Me);
            };
            s.AddFx(f =>
            {
                f.Label = "Tribute 5: Kuribabylon";
                f.Can = x => KuriSet(x) && x.My.Hand.Concat(x.My.Deck).Concat(x.My.Grave).Any(c => c.Name == "Kuribabylon");
                f.Cost = TributeKuris;
                f.Run = async x => { await SummonPick(x, x.My.Hand.Concat(x.My.Deck).Concat(x.My.Grave).Where(c => c.Name == "Kuribabylon"), "Special Summon Kuribabylon"); x.D.Shuffle(x.Me); };
            });
        }
        {
            var s = S("Kuribee");
            s.On = async (x, e) =>
            {
                if (KuriDied(x, e) && Hopt(x, "1") && x.My.Deck.Any(c => !c.Data.IsMonster && Mentions(c.Data, "Kuriboh")) && await x.Ask("Add a Spell/Trap that lists Kuriboh?"))
                { UseHopt(x, "1"); await x.Search(c => !c.Data.IsMonster && Mentions(c.Data, "Kuriboh"), "Add which Spell/Trap?"); return; }
                if (e.Kind == EvKind.AttackDeclared && e.Player == x.Opp && Up(x) && Hopt(x, "2") && x.MyMonsters().Any(m => m != x.Self && m.FaceUp && m.In("Kuriboh"))
                    && await x.Ask("Make your other monsters' ATK 0 to negate the attack?"))
                {
                    UseHopt(x, "2");
                    foreach (var m in x.MyMonsters().Where(m => m != x.Self && m.FaceUp)) x.D.Boost(m, -x.Atk(m));
                    x.D.AttackNegated = true;
                }
            };
        }
        {
            var s = S("Kuribeh");
            s.AddFx(f =>
            {
                f.Label = "Discard: +1500 ATK"; f.FromHand = true; f.Speed = 2; f.AnyTurn = true;
                f.Can = x => x.Self.Loc == Loc.Hand && x.Targetable(x.MyMonsters().FaceUp().Where(m => m.In("Kuriboh"))).Any();
                f.Cost = async x => { await x.D.Discard(x.Me, new[] { x.Self }, x.Self); await x.Target(x.MyMonsters().FaceUp().Where(m => m.In("Kuriboh")), "Which Kuriboh gains 1500 ATK?"); };
                f.Run = async x => { if (x.T0 is { Loc: Loc.Monster, FaceUp: true } t) { t.Vars["modatk"] = t.Var("modatk") + 1500; x.D.Seq++; } };
            });
            s.AddFx(f =>
            {
                f.Label = "Tribute 5: Kuribandit";
                f.Can = x => KuriSet(x) && x.My.Deck.Concat(x.My.Grave).Any(c => c.Name == "Kuribandit");
                f.Cost = TributeKuris;
                f.Run = async x =>
                {
                    var c = await x.Pick(x.My.Deck.Concat(x.My.Grave).Where(c => c.Name == "Kuribandit"), "Add Kuribandit");
                    if (c != null) { bool deck = c.Loc == Loc.Deck; await x.D.ToHand(c, x.Self); if (deck) x.D.Shuffle(x.Me); }
                    if (x.My.Hand.Any(c => c.Data.IsMonster && c.Data.Race == "Fiend") && await x.Ask("Normal Summon 1 Fiend monster?")) x.My.ExtraSummons++;
                };
            });
            s.Note = "The Normal Summon afterwards is given as one extra Normal Summon to use right away.";
        }
        S("Kuribandit").On = async (x, e) =>
        {
            if (e.Kind != EvKind.EndPhase || !Up(x) || x.Self.SummonTurn != x.D.Turn || x.Self.Summoned is not (SummonKind.Normal or SummonKind.Tribute) || x.My.Deck.Count < 5 || x.D.Forbidden(Q.Tribute, x.Self)) return;
            if (!await x.Ask("Tribute Kuribandit to excavate the top 5 cards?")) return;
            await x.D.Tribute(x.Self, x.Self);
            var top = x.My.Deck.Take(5).ToList();
            x.D.Reveal(x.Me, top); x.D.Reveal(x.Opp, top);
            var pick = await x.Pick(top.Where(c => !c.Data.IsMonster), "Add 1 excavated Spell/Trap to your hand?", optional: true);
            if (pick != null) await x.D.ToHand(pick, x.Self);
            foreach (var c in top.Where(c => c != pick)) await x.D.ToGrave(c, x.Self);
        };
        {
            var s = S("Kuriboo");
            s.AddFx(f =>
            {
                f.Label = "Discard: add a Kuriboh"; f.FromHand = true; f.Speed = 2; f.AnyTurn = true;
                f.Can = x => x.Ev is { Kind: EvKind.AttackDeclared } e && e.Player == x.Opp && x.My.Deck.Any(c => c.Data.IsMonster && c.In("Kuriboh") && c.Name != "Kuriboo");
                f.Cost = async x => await x.D.Discard(x.Me, new[] { x.Self }, x.Self);
                f.Run = async x => await x.Search(c => c.Data.IsMonster && c.In("Kuriboh") && c.Name != "Kuriboo", "Add which Kuriboh monster?");
            });
            s.AddFx(f =>
            {
                f.Label = "Discard a Trap: -1500 ATK"; f.Speed = 2; f.AnyTurn = true;
                f.Can = x => Up(x) && Hopt(x) && x.My.Hand.Any(c => c.Data.IsTrap) && x.Targetable(x.OppMonsters().FaceUp()).Any();
                f.Cost = async x => { UseHopt(x); await x.D.Discard(x.Me, await x.PickN(x.My.Hand.Where(c => c.Data.IsTrap), 1, 1, "Discard 1 Trap"), x.Self); await x.Target(x.OppMonsters().FaceUp(), "Which monster loses 1500 ATK?"); };
                f.Run = async x => { if (x.T0 is { Loc: Loc.Monster, FaceUp: true } t) x.D.Boost(t, -1500); };
            });
        }
        {
            var s = S("Lady Ninja Yae");
            s.Can = x => Up(x) && x.My.Hand.Any(c => c.Data.IsMonster && c.Data.Attribute == "WIND") && x.Spells(x.Opp).Any();
            s.Cost = async x => await x.D.Discard(x.Me, await x.PickN(x.My.Hand.Where(c => c.Data.IsMonster && c.Data.Attribute == "WIND"), 1, 1, "Discard 1 WIND monster"), x.Self);
            s.Run = async x => { foreach (var c in x.Spells(x.Opp).ToList()) await x.D.ToHand(c, x.Self); };
        }
        Spell("Last Resort", async x =>
        {
            bool had = x.Their.FieldZone is { FaceUp: true };
            var f = await x.Pick(x.My.Deck.Where(c => c.Name == "Ancient City - Rainbow Ruins"), "Activate Ancient City - Rainbow Ruins?", optional: true);
            if (f != null)
            {
                if (x.My.FieldZone is { } old) await x.D.ToGrave(old, x.Self);
                x.D.Place(f, x.Me, Loc.Field); f.FaceUp = true; x.D.Seq++;
                x.D.Shuffle(x.Me);
            }
            if (had && await x.D.YesNo(x.Opp, "Draw 1 card?", x.Self)) await x.D.Draw(x.Opp, 1);
        }, x => AttackOnMe(x));
        Sure("Inferno Hammer", "Inferno Tempest", "Ipiria", "Jurassic World", "Justi-Break", "KA-2 Des Scissors", "Kaibaman", "Kaiser Glider", "Kaiser Sea Horse", "Keldo",
            "King's Knight", "Knight's Title", "Lady Ninja Yae", "Kishido Spirit", "Kelbek", "Jinzo - Returner");
    }
}
