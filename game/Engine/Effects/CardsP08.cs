namespace PoC.Engine;

// Official cards, batch 8 (Demiurge Ema .. Dimension Fusion).
public static partial class Scripts
{
    static bool DHero(Card c) => c.Data.IsMonster && c.In("Destiny HERO");

    /// "Must be Special Summoned (from your hand) by Tributing N monsters (that satisfy `ok` as a group)".
    internal static void TributeProc(Script s, int n, Func<Ctx, List<Card>, bool>? ok = null)
    {
        s.SummonOk = (x, by, k) => by == x.Self;
        s.ProcCan = x => x.D.Tributable(x.Me).Count() >= n && (ok == null || ok(x, x.D.Tributable(x.Me).ToList()));
        s.Proc = async x =>
        {
            var l = await x.PickN(x.D.Tributable(x.Me), n, n, $"Tribute {n} monsters");
            if (ok != null && !ok(x, l)) return;
            foreach (var t in l) await x.D.Tribute(t, x.Self);
            await x.D.SpecialSummon(x.Self, x.Me, await x.D.ChoosePosition(x.Me, x.Self), by: x.Self);
        };
    }

    /// Destroyed by battle and sent to the GY since your last Standby Phase (the turn before this one, or this one).
    static bool BattleDiedSinceStandby(Ctx x) => x.Self.Loc == Loc.Grave && x.D.Flags.GetValueOrDefault($"battledead{x.Self.Owner}_{x.Self.Uid}") >= x.D.Turn - 1;

    static void CardsP08(CardDb db)
    {
        {
            var s = S("Demiurge Ema");
            static bool Big(Card c) => c.Data.IsMonster && c.Data.Atk >= 2400 && c.Data.Def == 1000;
            static IEnumerable<Card> Mats(Ctx x) => x.My.Hand.Where(c => c != x.Self).Concat(x.My.Deck).Concat(x.MyMonsters().FaceUp()).Where(Big);
            s.AddFx(f =>
            {
                f.Label = "Special Summon"; f.FromHand = true;
                f.Can = x => Hopt(x, "1") && Mats(x).Count() >= 4 && x.D.FreeMonsterZones(x.Me) + x.MyMonsters().Count(Big) > 0;
                f.Cost = async x => UseHopt(x, "1");
                f.Run = async x =>
                {
                    var l = await x.PickN(Mats(x), 4, 4, "Send 4 monsters (2400+ ATK, 1000 DEF) to the GY");
                    if (l.Count < 4) return;
                    foreach (var c in l) await x.D.ToGrave(c, x.Self);
                    x.D.Shuffle(x.Me);
                    if (x.Self.Loc == Loc.Hand) await x.D.SpecialSummon(x.Self, x.Me, await x.D.ChoosePosition(x.Me, x.Self), by: x.Self);
                };
            });
            s.AddFx(f =>
            {
                f.Label = "Destroy 2 Spells/Traps";
                f.Can = x => Hopt(x, "2") && x.Targetable(x.Spells(x.Me)).Any() && x.Targetable(x.Spells(x.Opp)).Any();
                f.Cost = async x => { UseHopt(x, "2"); await x.Target(x.Spells(x.Me), "Target 1 Spell/Trap on your field"); await x.Target(x.Spells(x.Opp), "Target 1 Spell/Trap on your opponent's field"); };
                f.Run = async x =>
                {
                    await x.D.Destroy(x.T.Where(t => t.OnField).ToList(), x.Self);
                    bool any = false;
                    foreach (var p in new[] { x.Me, x.Opp })
                        if (x.D.FreeMonsterZones(p) > 0 && await x.D.SpecialSummon(x.D.MakeToken("Homunculus Token", p, 800, 800, "LIGHT", "Fairy", 2), p, true, by: x.Self)) any = true;
                    if (any && Up(x)) { x.Self.Vars["modatk"] = x.Self.Var("modatk") + 1600; x.D.Seq++; }
                };
            });
        }
        S("Des Counterblow").On = async (x, e) =>
        {
            if (e.Kind == EvKind.BattleDamage && e.Other == null && x.D.AttackTarget == null && e.Card is { } a && a.Valid() && x.Self.OnField && x.Self.FaceUp) await x.D.Destroy(a, x.Self);
        };
        Spell("Des Croaking", async x => await x.D.Destroy(x.Their.FieldCards.ToList(), x.Self), x => x.MyMonsters().Count(m => m.FaceUp && Scripts.NameOf(m) == "Des Frog") >= 3);
        Flip("Des Feral Imp", async x => { var t = await x.Pick(x.Targetable(x.My.Grave), "Shuffle which GY card into your Deck?"); if (t != null) await x.D.ToDeck(t, shuffle: true); });
        S("Des Frog").On = async (x, e) =>
        {
            if (!e.Is(EvKind.Summoned, x.Self) || e.Arg != (int)SummonKind.Tribute) return;
            int n = x.My.Grave.Count(c => c.Name == "T.A.D.P.O.L.E.");
            for (int i = 0; i < n; i++)
                if (!await SummonPick(x, x.My.Hand.Concat(x.My.Deck).Where(c => c.Name == "Des Frog"), $"Special Summon a Des Frog ({i + 1}/{n})?", optional: true)) break;
            x.D.Shuffle(x.Me);
        };
        S("Des Kangaroo").On = async (x, e) =>
        {
            if (e.Kind == EvKind.AfterDamage && e.Other == x.Self && Up(x) && x.Self.Defense && e.Card is { } a && a.Valid() && x.Atk(a) < x.Def(x.Self)) await x.D.Destroy(a, x.Self);
        };
        Flip("Des Koala", async x => await x.D.Damage(x.Opp, 400 * x.Their.Hand.Count, x.Self));
        S("Des Volstgalph").On = async (x, e) =>
        {
            if (e.Kind == EvKind.BattleDestroyedMonster && e.Card == x.Self && e.Other!.Owner == x.Opp && e.Other.Loc == Loc.Grave) await x.D.Damage(x.Opp, 500, x.Self);
            if (e.Kind == EvKind.Resolved && e.Link is { IsCardActivation: true } l && l.Card.Data.IsSpell && (l.Card.Data.Is("Normal") || l.Card.Data.Is("Quick-Play")) && Up(x)) x.D.Boost(x.Self, 200);
        };
        S("Despair from the Dark").On = async (x, e) =>
        {
            if (e.Is(EvKind.ToGrave, x.Self) && e.From is Loc.Hand or Loc.Deck && e.Other is { } by && by.Controller == x.Opp && x.Self.Owner == x.Me && x.D.CanSpecialSummon(x.Self, x.Me, by: x.Self))
                await x.D.SpecialSummon(x.Self, x.Me, await x.D.ChoosePosition(x.Me, x.Self), by: x.Self);
        };
        {
            var s = S("Desrook Archfiend");
            s.On = async (x, e) =>
            {
                if (e.Kind == EvKind.Standby && e.Player == x.Me && Up(x) && x.D.CountActive("Pandemonium") == 0) { if (x.My.Lp < 500) await x.D.Destroy(x.Self); else x.D.Pay(x.Me, 500, x.Self); }   // can't pay the mandatory 500: destroyed
                if (e.Kind == EvKind.Activated && e.Link is { } l && l.Player == x.Opp && Up(x) && l.X.T.Contains(x.Self) && x.D.Die(x.Me, x.Self) == 3) await x.D.Negate(l);
            };
            s.AddFx(f =>
            {
                f.Label = "Revive Terrorking"; f.FromHand = true; f.Speed = 2; f.AnyTurn = true;
                f.Can = x => x.Ev is { Kind: EvKind.Destroyed } e && e.Card!.Name == "Terrorking Archfiend" && e.Card.Loc == Loc.Grave && e.Card.Owner == x.Me && x.D.CanSpecialSummon(e.Card, x.Me, by: x.Self);
                f.Cost = async x => { x.T.Add(x.Ev!.Card!); await x.D.ToGrave(x.Self, x.Self); };
                f.Run = async x => { if (x.T0 is { Loc: Loc.Grave } t) await x.D.SpecialSummon(t, x.Me, await x.D.ChoosePosition(x.Me, t), by: x.Self); };
            });
            s.Note = "Its die is rolled when it is targeted (not when the effect resolves).";
        }
        Spell("Destiny Draw", async x => await x.D.Draw(x.Me, 2), x => x.My.Hand.Any(c => c != x.Self && c.In("Destiny HERO")),
            async x => await x.D.Discard(x.Me, await x.PickN(x.My.Hand.Where(c => c != x.Self && c.In("Destiny HERO")), 1, 1, "Discard 1 Destiny HERO card"), x.Self));
        {
            var s = S("Destiny End Dragoon");
            s.OncePerTurn = true;
            s.Can = x => Up(x) && !x.D.TurnFlags.Contains($"bp{x.Me}") && x.Targetable(x.OppMonsters()).Any();
            s.Cost = async x => { x.D.TurnFlags.Add("nobattle"); await x.Target(x.OppMonsters(), "Destroy which monster?"); };
            s.Run = async x =>
            {
                if (x.T0 is not { } t || !t.Valid()) return;
                bool up = t.FaceUp; int atk = x.Atk(t);
                await x.D.Destroy(t, x.Self);
                if (up && t.Loc != Loc.Monster) await x.D.Damage(x.Opp, atk, x.Self);
            };
            s.On = async (x, e) =>
            {
                if (e.Kind == EvKind.Standby && e.Player == x.Me && x.Self.Loc == Loc.Grave && x.Self.Var("dragoonturn") != x.D.Turn && x.My.Grave.Any(c => c != x.Self && c.In("Destiny HERO"))
                    && x.D.CanSpecialSummon(x.Self, x.Me, by: x.Self, ignore: false) && await x.Ask("Banish a Destiny HERO card from your GY to Special Summon Destiny End Dragoon?"))
                {
                    x.Self.Vars["dragoonturn"] = x.D.Turn;
                    var c = await x.Pick(x.My.Grave.Where(c => c != x.Self && c.In("Destiny HERO")), "Banish which Destiny HERO card?");
                    if (c != null) { await x.D.Banish(c, x.Self); await x.D.SpecialSummon(x.Self, x.Me, await x.D.ChoosePosition(x.Me, x.Self), by: x.Self); }
                }
            };
        }
        {
            var s = S("Destiny HERO - Blade Master");
            s.AddFx(f =>
            {
                f.Label = "Destiny HEROes +800"; f.FromHand = true; f.Speed = 2; f.AnyTurn = true;
                f.Can = x => x.D.TurnPlayer == x.Opp && x.D.Phase == Phase.Battle && x.MyMonsters().Any(m => m.FaceUp && m.In("Destiny HERO"));
                f.Cost = async x => await x.D.Discard(x.Me, new[] { x.Self }, x.Self);
                f.Run = async x => { foreach (var m in x.MyMonsters().Where(m => m.FaceUp && m.In("Destiny HERO"))) x.D.Boost(m, 800); };
            });
        }
        {
            var s = S("Destiny HERO - Captain Tenacious");
            s.On = async (x, e) =>
            {
                if (e.Kind == EvKind.Destroyed && e.Battle && DHero(e.Card!) && e.Card!.Owner == x.Me && Up(x)) x.D.Flags[$"tenacious{x.Self.Uid}_{e.Card.Uid}"] = x.D.Turn;
                if (e.Kind == EvKind.Standby && e.Player == x.Me && Up(x) && x.Self.Var("tenturn") != x.D.Turn)
                {
                    var l = x.Targetable(x.My.Grave.Where(c => DHero(c) && x.D.Flags.GetValueOrDefault($"tenacious{x.Self.Uid}_{c.Uid}") >= x.D.Turn - 1)).ToList();
                    if (l.Count == 0 || !await x.Ask("Special Summon a Destiny HERO destroyed by battle since your last Standby Phase?")) return;
                    x.Self.Vars["tenturn"] = x.D.Turn;
                    await SummonPick(x, l, "Special Summon which?");
                }
            };
        }
        {
            var s = S("Destiny HERO - Celestial");
            s.On = async (x, e) =>
            {
                if (e.Kind == EvKind.AttackDeclared && e.Card == x.Self && x.Targetable(x.Spells(x.Opp).Where(c => c.FaceUp && c.Data.IsSpell)).Any() && await x.Ask("Destroy 1 face-up Spell your opponent controls (500 damage)?"))
                {
                    var t = await x.Pick(x.Targetable(x.Spells(x.Opp).Where(c => c.FaceUp && c.Data.IsSpell)), "Destroy which Spell?");
                    if (t == null) return;
                    await x.D.Destroy(t, x.Self);
                    if (!t.OnField) await x.D.Damage(x.Opp, 500, x.Self);
                }
            };
            s.AddFx(f =>
            {
                f.Label = "Banish 2, draw 2"; f.FromGrave = true;
                f.Can = x => Hopt(x) && x.My.Hand.Count == 0 && x.Self.Var("gyturn") != x.D.Turn && x.My.Grave.Any(c => c != x.Self && DHero(c));
                f.Cost = async x =>
                {
                    UseHopt(x);
                    var o = await x.Pick(x.My.Grave.Where(c => c != x.Self && DHero(c)), "Banish 1 Destiny HERO from your GY");
                    await x.D.Banish(x.Self, x.Self);
                    if (o != null) await x.D.Banish(o, x.Self);
                };
                f.Run = async x => await x.D.Draw(x.Me, 2);
            });
        }
        {
            var s = S("Destiny HERO - Dark Angel");
            s.AddFx(f =>
            {
                f.Label = "Summon to the opponent's field"; f.FromHand = true;
                f.Can = x => x.My.Grave.Count(DHero) >= 3 && x.D.FreeMonsterZones(x.Opp) > 0;
                f.Cost = async x =>
                {
                    await x.D.Discard(x.Me, new[] { x.Self }, x.Self);
                    await x.Target(x.My.Grave.Where(c => DHero(c) && c != x.Self), "Special Summon which Destiny HERO to your opponent's field?");
                };
                f.Run = async x =>
                {
                    if (x.T0 is not { Loc: Loc.Grave } t || !await x.D.SpecialSummon(t, x.Opp, true, by: x.Self)) return;
                    var d = x.D; int opp = x.Opp;
                    // While that monster is face-up on their field, the Spell effects they activate are negated and the Spell destroyed.
                    d.Watch(int.MaxValue, async e =>
                    {
                        if (e.Kind == EvKind.Activated && e.Link is { } l && l.Player == opp && l.Card.Data.IsSpell && t.Loc == Loc.Monster && t.FaceUp && t.Controller == opp) await d.Negate(l);   // negated, and the Spell destroyed
                    });
                };
            });
            s.On = async (x, e) =>
            {
                if (e.Kind == EvKind.Standby && e.Player == x.Me && x.Self.Loc == Loc.Grave && x.My.Grave.Any(c => c != x.Self && DHero(c)) && await x.Ask("Banish Dark Angel and a Destiny HERO: each player puts a Normal Spell from their Deck on top?"))
                {
                    var o = await x.Pick(x.My.Grave.Where(c => c != x.Self && DHero(c)), "Banish 1 Destiny HERO from your GY");
                    await x.D.Banish(x.Self, x.Self);
                    if (o != null) await x.D.Banish(o, x.Self);
                    foreach (var p in new[] { x.Me, x.Opp })
                    {
                        var c = await x.D.PickOne(p, x.D.P(p).Deck.Where(c => c.Data.IsSpell && c.Data.Is("Normal")), "Place 1 Normal Spell from your Deck on top of it", x.Self);
                        if (c != null) { x.D.Shuffle(p); x.D.P(p).Deck.Remove(c); x.D.P(p).Deck.Insert(0, c); }
                    }
                }
            };
            s.Note = "Its text here is shortened; it is played as the official card: while the monster it gives your opponent is face-up on their field, the Spells they activate are negated.";
        }
        {
            var s = S("Destiny HERO - Dasher");
            s.OncePerTurn = true;
            s.Can = x => Up(x) && x.D.Tributable(x.Me).Any(m => m != x.Self);
            s.Cost = async x => { var t = await x.Pick(x.D.Tributable(x.Me).Where(m => m != x.Self), "Tribute 1 other monster"); if (t != null) await x.D.Tribute(t, x.Self); };
            s.Run = async x => { if (Up(x)) x.D.Boost(x.Self, 1000); };
            s.On = async (x, e) =>
            {
                if (e.Kind == EvKind.BattleEnd && Up(x) && x.Self.Attacks > 0) await x.D.ChangePosition(x.Self, true);
                if (e.Kind == EvKind.Drew && e.Arg == 1 && e.Player == x.Me && x.Self.Loc == Loc.Grave && x.Self.Var("dasheronce") == 0
                    && e.Cards!.FirstOrDefault(c => c.Data.IsMonster && c.Loc == Loc.Hand) is { } m && x.D.CanSpecialSummon(m, x.Me, by: x.Self) && await x.Ask($"Reveal {m.Name} and Special Summon it (Destiny HERO - Dasher, once)?"))
                {
                    x.Self.Vars["dasheronce"] = 1;
                    x.D.Reveal(x.Opp, m);
                    await x.D.SpecialSummon(m, x.Me, await x.D.ChoosePosition(x.Me, m), by: x.Self);
                }
            };
        }
        S("Destiny HERO - Defender").On = async (x, e) => { if (e.Kind == EvKind.Standby && e.Player == x.Opp && Up(x) && x.Self.Defense) await x.D.Draw(x.Opp, 1); };
        {
            var s = S("Destiny HERO - Departed");
            s.GraveToBanish = (x, from, battle, byEffect) => (battle && from == Loc.Monster) || (byEffect && from is Loc.Hand or Loc.Deck);
            s.On = async (x, e) =>
            {
                if (e.Kind == EvKind.Standby && e.Player == x.Me && x.Self.Loc == Loc.Grave && x.D.CanSpecialSummon(x.Self, x.Opp, by: x.Self))
                    await x.D.SpecialSummon(x.Self, x.Opp, false, by: x.Self);
            };
        }
        {
            var s = S("Destiny HERO - Diamond Dude");
            s.OncePerTurn = true;
            s.Can = x => Up(x) && x.My.Deck.Count > 0;
            s.Run = async x =>
            {
                if (x.My.Deck.Count == 0) return;
                var top = x.My.Deck[0];
                x.D.Reveal(x.Me, top); x.D.Reveal(x.Opp, top);
                if (top.Data.IsSpell && top.Data.Is("Normal")) { await x.D.ToGrave(top, x.Self); top.Vars["ddude"] = x.D.Turn + 2; }
                else { x.My.Deck.RemoveAt(0); x.My.Deck.Add(top); }
            };
            s.Note = "The Normal Spell's effect can be activated from your GY during your next turn's Main Phase (its own conditions and costs apply).";
        }
        {
            var s = S("Destiny HERO - Disk Commander");
            s.SummonOk = (x, by, k) => !(x.Self.Loc == Loc.Grave && x.Self.Var("gyturn") == x.D.Turn);
            s.On = async (x, e) =>
            {
                if (e.Is(EvKind.Summoned, x.Self) && e.From == Loc.Grave && x.D.Flags.GetValueOrDefault($"diskcommander{x.Me}") == 0 && x.My.Deck.Count >= 2 && await x.Ask("Draw 2 cards (Disk Commander, once per Duel)?"))
                { x.D.Flags[$"diskcommander{x.Me}"] = 1; await x.D.Draw(x.Me, 2); }
            };
        }
        {
            var s = S("Destiny HERO - Dogma");
            TributeProc(s, 3, (x, l) => l.Any(DHero));
            s.On = async (x, e) =>
            {
                if (e.Is(EvKind.Summoned, x.Self) && e.Other == x.Self) x.Self.Vars["dogmaturn"] = x.D.Turn;
                if (e.Kind == EvKind.Standby && e.Player == x.Opp && Up(x) && x.Self.Var("dogmaturn") > 0 && x.Self.Var("dogmaturn") == x.D.Turn - 1)
                { x.Their.Lp = x.Their.Lp / 2; x.D.Seq++; x.Self.Vars["dogmaturn"] = 0; }
            };
        }
        {
            var s = S("Destiny HERO - Doom Lord");
            s.OncePerTurn = true;
            s.Can = x => Up(x) && x.Self.Attack && x.MyMonsters().All(m => m.Attacks == 0) && x.Targetable(x.OppMonsters()).Any();
            s.Cost = async x => await x.Target(x.OppMonsters(), "Banish which monster?");
            s.Run = async x =>
            {
                var d = x.D; int me = x.Me;
                d.Linger(d.Turn, (q, c, p) => q == Q.Attack && c != null && c.Controller == me);
                if (!Up(x) || !x.Self.Attack || x.T0 is not { } t || !t.Valid()) return;
                bool def = t.Defense, up = t.FaceUp; int owner = t.Owner;
                await d.Banish(t, x.Self);
                if (t.Loc != Loc.Banished) return;
                t.FaceUp = false;   // banished face-down
                int back = d.TurnPlayer == me ? d.Turn + 4 : d.Turn + 3;   // your 2nd Standby Phase after activation
                d.Later(back, Phase.Standby, async () =>
                {
                    if (t.Loc != Loc.Banished || d.FreeMonsterZones(owner) == 0 || !d.Place(t, owner, Loc.Monster)) return;
                    t.FaceUp = up; t.Defense = def; d.Seq++;
                });
            };
        }
        {
            var s = S("Destiny HERO - Double Dude");
            s.SummonOk = (x, by, k) => false;
            s.Allow = (x, q, c) => q == Q.DoubleAttack && c == x.Self;
            s.On = async (x, e) =>
            {
                if (e.Is(EvKind.Destroyed, x.Self)) x.Self.Vars["destroyedturn"] = x.D.Turn;
                if (e.Kind == EvKind.Standby && e.Player == x.Me && x.Self.Loc == Loc.Grave && x.Self.Var("destroyedturn") >= x.D.Turn - 1 && x.Self.Var("destroyedturn") > 0
                    && x.D.FreeMonsterZones(x.Me) >= 2 && await x.Ask("Special Summon 2 Double Dude Tokens?"))
                {
                    x.Self.Vars["destroyedturn"] = 0;
                    for (int i = 0; i < 2; i++) await x.D.SpecialSummon(x.D.MakeToken("Double Dude Token", x.Me, 1000, 1000, "DARK", "Warrior", 4), x.Me, await x.D.ChoosePosition(x.Me, x.Self), by: x.Self);
                }
            };
        }
        S("Destiny HERO - Dread Servant").On = async (x, e) =>
        {
            if (e.Is(EvKind.Summoned, x.Self) && e.Summoned(SummonKind.Normal, SummonKind.Tribute))
                foreach (var c in x.D.FieldCards.Where(c => c.FaceUp && c.Name == "Clock Tower Prison")) AddCounter(x, c, 1, "clock");
            if (e.Is(EvKind.Destroyed, x.Self) && e.Battle && x.Self.Loc == Loc.Grave && x.Targetable(x.Spells(x.Me)).Any() && await x.Ask("Destroy 1 Spell/Trap you control?"))
            { var t = await x.Pick(x.Targetable(x.Spells(x.Me)), "Destroy which?"); if (t != null) await x.D.Destroy(t, x.Self); }
        };
        {
            var s = S("Destiny HERO - Dreadmaster");
            s.Base = (x, c, st) =>
            {
                if (c != x.Self) return;
                int sum = x.MyMonsters().Where(m => m != x.Self && m.FaceUp && m.In("Destiny HERO")).Sum(m => x.D.StatOf(m).BaseAtk);
                st.BaseAtk = sum; st.BaseDef = sum;
            };
            s.On = async (x, e) =>
            {
                if (!e.Is(EvKind.Summoned, x.Self) || e.Arg != (int)SummonKind.Special) return;   // "After this card is Special Summoned"
                int me = x.Me; var d = x.D;
                if (x.Self.SummonedBy == "Clock Tower Prison")
                {
                    await d.Destroy(x.MyMonsters().Where(m => !m.In("Destiny HERO")).ToList(), x.Self);
                    for (int i = 0; i < 2; i++)
                        if (!await SummonPick(x, x.My.Grave.Where(DHero), $"Special Summon a Destiny HERO from your GY ({i + 1}/2)?", optional: true)) break;
                }
                d.Linger(d.Turn, (q, c, p) => q is Q.DestroyByBattle or Q.DestroyByEffect && c != null && c.Controller == me && c.In("Destiny HERO"));
                x.Self.Vars["dreadturn"] = d.Turn;
            };
            s.BattleDamage = (x, src, to, dmg) => to == x.Me && x.Self.Var("dreadturn") == x.D.Turn
                && new[] { x.D.Attacker, x.D.AttackTarget }.Any(m => m != null && m.Controller == x.Me && m.In("Destiny HERO")) ? 0 : dmg;
        }
        {
            var s = S("Destiny HERO - Dunker");
            s.Can = x => Up(x) && x.My.Hand.Any(c => c.In("Destiny HERO"));
            s.Cost = async x => await x.D.ToGrave((await x.Pick(x.My.Hand.Where(c => c.In("Destiny HERO")), "Send 1 Destiny HERO card from your hand to the GY"))!, x.Self);
            s.Run = async x => await x.D.Damage(x.Opp, 500, x.Self);
        }
        S("Destiny HERO - Fear Monger").On = async (x, e) =>
        {
            if (e.Kind == EvKind.Standby && e.Player == x.Me && BattleDiedSinceStandby(x))
                await SummonPick(x, x.Targetable(x.My.Grave.Where(c => DHero(c) && c.Name != "Destiny HERO - Fear Monger")), "Special Summon which Destiny HERO?");
        };
        {
            var s = S("Destiny HERO - Malicious");
            s.AddFx(f =>
            {
                f.Label = "Summon another Malicious"; f.FromGrave = true;
                f.Can = x => x.My.Deck.Any(c => c.Name == "Destiny HERO - Malicious") && x.D.FreeMonsterZones(x.Me) > 0;
                f.Cost = async x => await x.D.Banish(x.Self, x.Self);
                f.Run = async x => { await SummonPick(x, x.My.Deck.Where(c => c.Name == "Destiny HERO - Malicious"), "Special Summon Destiny HERO - Malicious"); x.D.Shuffle(x.Me); };
            });
        }
        {
            var s = S("Destiny HERO - Plasma");
            TributeProc(s, 3);
            static Card? Taken(Ctx x) => x.D.EquipsOf(x.Self).FirstOrDefault(e => e.Var("plasma") == x.Self.Uid + 1);
            s.Forbid = (x, q, c) => q == Q.Negate && c != null && c.Loc == Loc.Monster && c.FaceUp && c.Controller == x.Opp && Up(x);
            s.Stats = (x, c, st) => { if (c == x.Self && Taken(x) is { } e) st.Add(e.Data.Atk / 2, 0); };
            s.OncePerTurn = true;
            s.Can = x => Up(x) && Taken(x) == null && x.My.FreeSpellZones > 0 && x.Targetable(x.OppMonsters()).Any();
            s.Cost = async x => await x.Target(x.OppMonsters(), "Equip which monster to Plasma?");
            s.Run = async x =>
            {
                if (x.T0 is not { } t || !t.Valid() || !Up(x) || Taken(x) != null || x.My.FreeSpellZones == 0) return;
                t.ResetFieldState();
                x.D.Place(t, x.Me, Loc.Spell);
                await x.D.Equip(t, x.Self);
                t.Vars["plasma"] = x.Self.Uid + 1;
            };
        }
        Spell("Destiny Mirage", async x =>
        {
            var l = x.D.Players.SelectMany(p => p.Grave).Where(c => DHero(c) && x.D.Flags.GetValueOrDefault($"destroyed{c.Uid}") == x.D.Turn).ToList();
            foreach (var c in l) if (x.D.CanSpecialSummon(c, x.Me, by: x.Self)) await x.D.SpecialSummon(c, x.Me, await x.D.ChoosePosition(x.Me, c), by: x.Self);
        }, x => x.Ev is { Kind: EvKind.Destroyed, Battle: false } e && DHero(e.Card!) && e.Card!.Loc == Loc.Grave && e.Other is { } by && by.Controller == x.Opp
            && x.D.LeftFieldLog.LastOrDefault(l => l.c == e.Card) is { c: not null } l && l.controller == x.Me);
        Spell("Destiny Signal", async x => { await SummonPick(x, x.My.Hand.Concat(x.My.Deck).Where(c => DHero(c) && c.Data.Level <= 4), "Special Summon a Level 4 or lower Destiny HERO"); x.D.Shuffle(x.Me); },
            x => x.Ev is { Kind: EvKind.Destroyed, Battle: true } e && e.Card!.Loc == Loc.Grave && x.D.LeftFieldLog.LastOrDefault(l => l.c == e.Card) is { c: not null } l && l.controller == x.Me
                && x.My.Hand.Concat(x.My.Deck).Any(c => DHero(c) && c.Data.Level <= 4));
        {
            var s = S("Destroyersaurus");
            s.AddFx(f =>
            {
                f.Label = "Add Jurassic World"; f.FromHand = true;
                f.Can = x => x.My.Deck.Any(c => c.Name == "Jurassic World");
                f.Cost = async x => await x.D.Discard(x.Me, new[] { x.Self }, x.Self);
                f.Run = x => x.Search(c => c.Name == "Jurassic World", "Add Jurassic World");
            });
        }
        Spell("Destruction of Destiny", async x =>
        {
            var l = x.My.Deck.Take(3).ToList();
            foreach (var c in l) await x.D.ToGrave(c, x.Self);
            int n = l.Count(c => !c.Data.IsMonster && c.Loc == Loc.Grave);
            if (n > 0) await x.D.Damage(x.Me, 1000 * n, x.Self);
        }, x => x.My.Deck.Count >= 3);
        {
            var s = S("Diabound Kernel");
            s.On = async (x, e) => { if (e.Kind == EvKind.AttackDeclared && e.Card == x.Self && Up(x)) { x.Self.Vars["modatk"] = x.Self.Var("modatk") + 600; x.D.Seq++; } };
            s.Speed = 2; s.AnyTurn = true;
            s.Can = x => Up(x) && Hopt(x) && x.Targetable(x.OppMonsters().FaceUp()).Any();
            s.Cost = async x => { UseHopt(x); await x.Target(x.OppMonsters().FaceUp(), "Which monster loses ATK equal to Diabound Kernel's?"); };
            s.Run = async x =>
            {
                if (!Up(x)) return;
                if (x.T0 is { } t && t.Valid() && t.FaceUp) x.D.Boost(t, -x.Atk(x.Self));
                var me = x.Self; var d = x.D; int owner = me.Owner;
                await d.Banish(me, me);
                if (me.Loc != Loc.Banished) return;
                d.Later(d.Turn + 1, Phase.Standby, async () =>
                {
                    if (me.Loc == Loc.Banished && d.FreeMonsterZones(owner) > 0 && d.Place(me, owner, Loc.Monster)) { me.FaceUp = true; me.Defense = false; d.Seq++; }
                });
            };
        }
        Spell("Diamond-Dust Cyclone", async x =>
        {
            var t = await x.Pick(x.AllMonsters().Where(m => m.Counters("fog") >= 4), "Destroy which monster (4+ Fog Counters)?");
            if (t == null) return;
            int n = t.Counters("fog") / 4;
            await x.D.Destroy(t, x.Self);
            if (t.Loc != Loc.Monster) await x.D.Draw(x.Me, n);
        }, x => x.AllMonsters().Any(m => m.Counters("fog") >= 4));
        {
            var s = Spell("Dice Dungeon", async x =>
            {
                if (x.My.Deck.Any(c => c.Name == "Dimension Dice") && await x.Ask("Add Dimension Dice from your Deck?")) await x.Search(c => c.Name == "Dimension Dice", "Add Dimension Dice");
            });
            s.On = async (x, e) =>
            {
                if (e.Kind != EvKind.BattleStart || !x.Self.OnField || !x.Self.FaceUp || e.Player != x.Me || !await x.Ask("Dice Dungeon: each player rolls a die for their monsters?")) return;
                foreach (var p in new[] { x.Me, x.Opp })
                {
                    int r = x.D.Die(p, x.Self);
                    foreach (var m in x.D.P(p).MonstersOnField.Where(m => m.FaceUp).ToList())
                    {
                        int atk = x.Atk(m);
                        x.D.Boost(m, r switch { 1 => -1000, 2 => 1000, 3 => -500, 4 => 500, 5 => -atk / 2, _ => atk });
                    }
                }
            };
            s.Note = "Its Battle Phase roll is offered to the turn player whose field it is on.";
        }
        Flip("Dice Jar", async x =>
        {
            while (true)
            {
                int a = x.D.Die(x.Me, x.Self), b = x.D.Die(x.Opp, x.Self);
                if (a == b) continue;
                int loser = a < b ? x.Me : x.Opp, win = Math.Max(a, b);
                await x.D.Damage(loser, win == 6 ? 6000 : win * 500, x.Self);
                break;
            }
        });
        {
            var s = Spell("Different Dimension Capsule", async x =>
            {
                var c = await x.Pick(x.My.Deck, "Banish which card from your Deck face-down?");
                x.D.Shuffle(x.Me);
                if (c == null) return;
                await x.D.Banish(c, x.Self);
                c.FaceUp = false;
                x.Self.Vars["stay"] = 1;
                var me = x.Self; var d = x.D; int owner = x.Me;
                int second = d.TurnPlayer == owner ? d.Turn + 4 : d.Turn + 3;
                d.Later(second, Phase.Standby, async () =>
                {
                    if (!me.OnField) return;
                    await d.Destroy(me, me);
                    if (c.Loc == Loc.Banished) await d.ToHand(c, me);
                });
            }, x => x.My.Deck.Count > 0);
        }
        S("Different Dimension Dragon").Forbid = (x, q, c) => c == x.Self && (
            (q == Q.DestroyByEffect && x.D.Destroyer is { } by && !by.Data.IsMonster && !(x.D.Resolving is { } l && l.Card == by && l.X.T.Contains(x.Self)))
            || (q == Q.DestroyByBattle && new[] { x.D.Attacker, x.D.AttackTarget }.FirstOrDefault(m => m != null && m != x.Self) is { } o && x.Atk(o) <= 1900));
        Spell("Diffusion Wave-Motion", async x =>
        {
            if (x.T0 is not { } m || !m.Valid() || !m.FaceUp) return;
            m.Vars["attackeach"] = x.D.Turn; m.Vars["mustattack"] = x.D.Turn;
            int me = x.Me;   // also monsters Summoned later this turn
            x.D.Linger(x.D.Turn, (q, c, p) => q == Q.Attack && c != null && c != m && c.Controller == me);
            var d = x.D;
            d.Watch(d.Turn, async e => { if (e.Kind == EvKind.BattleDestroyedMonster && e.Card == m) d.Flags[$"neg{e.Other!.Uid}"] = 1; });
        }, x => x.OppMonsters().Any() && x.My.Lp > 1000 && x.Targetable(x.MyMonsters().FaceUp().Where(m => x.IsRace(m, "Spellcaster") && x.D.Level(m) >= 7)).Any(),
        async x => { x.D.Pay(x.Me, 1000); await x.Target(x.MyMonsters().FaceUp().Where(m => x.IsRace(m, "Spellcaster") && x.D.Level(m) >= 7), "Which Spellcaster attacks every monster?"); });
        {
            static bool Dice(Card c) => System.Text.RegularExpressions.Regex.IsMatch(c.Data.Desc, @"(roll|Roll)[^.]*di(e|ce)");
            Spell("Dimension Dice", async x =>
            {
                await SummonPick(x, x.My.Hand.Concat(x.My.Deck).Where(c => c.Data.IsMonster && Dice(c)), "Special Summon a monster with a die-roll effect");
                x.D.Shuffle(x.Me);
            }, x => x.My.FieldCards.Any(c => c.FaceUp && Dice(c)) && x.D.Tributable(x.Me).Any() && x.My.Hand.Concat(x.My.Deck).Any(c => c.Data.IsMonster && Dice(c)),
            async x => { var t = await x.Pick(x.D.Tributable(x.Me), "Tribute 1 monster"); if (t != null) await x.D.Tribute(t, x.Self); });
        }
        Spell("Dimension Distortion", async x => await SummonPick(x, x.My.Banished.Where(c => c.Data.IsMonster && c.FaceUp), "Special Summon which banished monster?"),
            x => x.My.Grave.Count == 0 && x.My.Banished.Any(c => c.Data.IsMonster && c.FaceUp) && x.D.FreeMonsterZones(x.Me) > 0);
        Spell("Dimension Explosion", async x =>
        {
            foreach (var p in new[] { x.Me, x.Opp })
            {
                var l = x.D.P(p).Banished.Where(c => c.Data.IsMonster && c.FaceUp && x.D.CanSpecialSummon(c, p, by: x.Self)).ToList();
                foreach (var c in await x.D.Pick(p, l, 0, Math.Min(2, x.D.FreeMonsterZones(p)), "Special Summon up to 2 of your banished monsters", x.Self))
                    await x.D.SpecialSummon(c, p, await x.D.ChoosePosition(p, c), by: x.Self);
            }
        }, x => x.MyMonsters().Any(m => m.FaceUp && m.Data.IsFusion) && x.D.Players.All(p => p.Banished.Any(c => c.Data.IsMonster && c.FaceUp)),
        async x => { var f = await x.Pick(x.MyMonsters().Where(m => m.FaceUp && m.Data.IsFusion), "Return which Fusion Monster to the Extra Deck?"); if (f != null) await x.D.MoveTo(f, Loc.Extra); });
        Spell("Dimension Fusion", async x =>
        {
            foreach (var p in new[] { x.Me, x.Opp })
                foreach (var c in x.D.P(p).Banished.Where(c => c.Data.IsMonster && c.FaceUp).ToList())
                {
                    if (x.D.FreeMonsterZones(p) == 0) break;
                    if (x.D.CanSpecialSummon(c, p, by: x.Self)) await x.D.SpecialSummon(c, p, await x.D.ChoosePosition(p, c), by: x.Self);
                }
        }, x => x.My.Lp > 2000 && x.D.Players.Any(p => p.Banished.Any(c => c.Data.IsMonster && c.FaceUp)), async x => x.D.Pay(x.Me, 2000));
    }
}
