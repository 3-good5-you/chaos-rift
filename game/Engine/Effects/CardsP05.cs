namespace PoC.Engine;

// Official cards, batch 5 (Clear Rage Golem .. Cyber Angel Dakini).
public static partial class Scripts
{
    // ---- Clear World: each player gets the effects of the Attributes of the face-up monsters they control.
    static bool MentionsClearWorld(CardData d) => d.IsMonster && Mentions(d, "Clear World");
    static bool ClearImmune(Duel d, int p) => d.P(p).MonstersOnField.Any(m => m.FaceUp && !d.Negated(m) && m.Data.Desc.Contains("You are unaffected by the effects of \"Clear World\""));
    /// Clear Wall (with a Level 7+ "Clear World" monster) makes its controller's opponent receive every effect.
    static bool ClearForced(Duel d, int p) => d.P(1 - p).SpellsOnField.Any(s => s.FaceUp && s.Name == "Clear Wall" && !d.Negated(s))
        && d.P(1 - p).MonstersOnField.Any(m => m.FaceUp && MentionsClearWorld(m.Data) && d.Level(m) >= 7);
    internal static bool ClearHits(Duel d, int p, string attr) => !ClearImmune(d, p)
        && (ClearForced(d, p) || d.P(p).MonstersOnField.Any(m => m.FaceUp && d.Attr(m) == attr));

    /// Crystal Beast (non-Advanced): destroyed face-up in a Monster Zone, it may stay as a Continuous Spell.
    static Script CrystalBeast(string name) { var s = S(name); s.ToSpellZone = x => true; return s; }
    static int BeastsInSpellZone(Ctx x) => x.My.Spells.Count(c => c != null && c.FaceUp && c.In("Crystal Beast"));

    static void Cloudian(Script s, bool battleProof = true)
    {
        if (battleProof) s.Forbid = (x, q, c) => q == Q.DestroyByBattle && c == x.Self;
        var more = s.On;
        s.On = async (x, e) =>
        {
            if (Up(x) && x.Self.Defense) { await x.D.Destroy(x.Self, x.Self); return; }
            if (more != null) await more(x, e);
        };
    }

    /// The Neo-Spacian a Chrysalis monster names in its text.
    static string? ChrysalisInto(Card c) => System.Text.RegularExpressions.Regex.Match(c.Data.Desc, "\"(Neo-Spacian [^\"]+)\"") is { Success: true } m ? m.Groups[1].Value : null;

    static void CardsP05(CardDb db)
    {
        {
            var s = S("Clear Rage Golem");
            s.On = async (x, e) =>
            {
                if (e.Is(EvKind.Summoned, x.Self) && e.Summoned(SummonKind.Normal, SummonKind.Tribute, SummonKind.Special, SummonKind.Fusion, SummonKind.Ritual)
                    && Hopt(x) && x.D.TurnPlayer == x.Me && x.D.Phase is Phase.Main1 or Phase.Battle && await x.Ask("This turn, your monsters that mention Clear World can attack directly?"))
                {
                    UseHopt(x);
                    x.D.Flags[$"cleardirect{x.Me}"] = x.D.Turn;
                }
                if (e.Kind == EvKind.BattleDamage && e.Card == x.Self && e.Player == x.Opp && x.Their.Hand.Count > 0 && await x.Ask("Inflict 300 damage for each card in your opponent's hand?"))
                    await x.D.Damage(x.Opp, 300 * x.Their.Hand.Count, x.Self);
            };
            s.Allow = (x, q, c) => q == Q.DirectAttack && c != null && c.Controller == x.Me && x.D.Flags.GetValueOrDefault($"cleardirect{x.Me}") == x.D.Turn && MentionsClearWorld(c.Data);
            s.Note = "The direct-attack effect lasts the turn while Clear Rage Golem is on the field.";
        }
        {
            var s = S("Clear Vice Dragon");
            s.CalcAtk = (x, c, vs, atk) => c == x.Self && x.D.Attacker == x.Self && vs != null && vs.Controller == x.Opp ? 2 * x.Atk(vs) : atk;
            s.DestroyInstead = async (x, battle) =>
            {
                if (battle || x.D.Destroyer is not { } by || by.Controller != x.Opp || x.My.Hand.Count == 0 || !await x.Ask("Discard 1 card instead of Clear Vice Dragon being destroyed?")) return false;
                await x.D.Discard(x.Me, await x.PickN(x.My.Hand, 1, 1, "Discard 1 card"), x.Self);
                return true;
            };
        }
        {
            var s = S("Clear Vicious Knight");
            s.TributeWeightFor = (x, t) => t.FaceUp && MentionsClearWorld(t.Data) ? 2 : 1;
            s.Stats = (x, c, st) => { if (c == x.Self && x.OppMonsters().FaceUp().Any()) st.Add(x.OppMonsters().FaceUp().Max(m => x.D.StatOf(m).BaseAtk), 0); };
            s.Forbid = (x, q, c) => q == Q.Activate && c != null && c.Controller == x.Opp && c.Loc == Loc.Monster && c.Summoned is SummonKind.Special or SummonKind.Fusion or SummonKind.Ritual
                && x.Self.Summoned == SummonKind.Tribute && Up(x) && x.Atk(c) < x.Atk(x.Self);
        }
        {
            var s = S("Clear Wall");
            s.Forbid = (x, q, c) => q == Q.DestroyByBattle && c != null && c.Controller == x.Me && MentionsClearWorld(c.Data);
            s.BattleDamage = (x, src, to, dmg) => to == x.Me && new[] { x.D.Attacker, x.D.AttackTarget }.Any(m => m != null && m.Controller == x.Me && MentionsClearWorld(m.Data)) ? 0 : dmg;
            s.AddFx(f =>
            {
                f.Label = "Add a Clear World monster";
                f.Can = x => Hopt(x) && x.D.CountActive("Clear World") > 0 && x.My.Deck.Any(c => MentionsClearWorld(c.Data));
                f.Cost = async x => UseHopt(x);
                f.Run = x => x.Search(c => MentionsClearWorld(c.Data), "Add which monster?");
            });
        }
        {
            var s = S("Clear World");
            s.Note = "LIGHT (hand revealed) has no effect yet: the duel shows hands only with Ceremonial Bell / the sandbox. WIND is paid as the Spell is activated.";
            s.Forbid = (x, q, c) => q == Q.Attack && c != null && ClearHits(x.D, c.Controller, "DARK") && x.D.P(c.Controller).MonstersOnField.Count() >= 2;
            s.On = async (x, e) =>
            {
                if (!x.Self.OnField || !x.Self.FaceUp) return;
                if (e.Kind == EvKind.EndPhase && e.Player == x.Me) await UpkeepOrDestroy(x, 500, "Clear World");
                if (!x.Self.OnField) return;
                if (e.Kind == EvKind.Standby && ClearHits(x.D, e.Player, "EARTH"))
                {
                    var defs = x.D.P(e.Player).MonstersOnField.Where(m => m.FaceUp && m.Defense).ToList();
                    if (defs.Count > 0) { var t = await x.D.PickOne(e.Player, defs, "Clear World (EARTH): destroy 1 face-up Defense Position monster you control", x.Self); if (t != null) await x.D.Destroy(t, x.Self); }
                }
                if (e.Kind == EvKind.EndPhase && ClearHits(x.D, e.Player, "WATER") && x.D.P(e.Player).Hand.Count > 0)
                    await x.D.Discard(e.Player, await x.D.Pick(e.Player, x.D.P(e.Player).Hand, 1, 1, "Clear World (WATER): discard 1 card", x.Self), x.Self);
                if (e.Kind == EvKind.EndPhase && ClearHits(x.D, e.Player, "FIRE")) await x.D.Damage(e.Player, 1000, x.Self);
                if (e.Kind == EvKind.Activated && e.Link is { IsCardActivation: true } l && l.Card.Data.IsSpell && ClearHits(x.D, l.Player, "WIND")) x.D.Pay(l.Player, 500);
            };
        }
        {
            var s = S("Clock Tower Prison");
            s.BattleDamage = (x, src, to, dmg) => to == x.Me && x.Self.Counters("clock") >= 4 ? 0 : dmg;
            s.On = async (x, e) =>
            {
                if (e.Kind == EvKind.Standby && e.Player == x.Opp && x.Self.OnField && x.Self.FaceUp) AddCounter(x, x.Self, 1, "clock");
                if (e.Is(EvKind.Destroyed, x.Self) && x.Self.Loc == Loc.Grave && x.D.LeftFieldLog.LastOrDefault(l => l.c == x.Self).vars?.GetValueOrDefault("ctr:clock") >= 4)
                { await SummonPick(x, x.My.Hand.Concat(x.My.Deck).Where(c => c.Name == "Destiny HERO - Dreadmaster"), "Special Summon Destiny HERO - Dreadmaster"); x.D.Shuffle(x.Me); }
            };
        }
        {
            var s = Spell("Clockwork Night", async x => { }, x => Hopt(x, "act"), async x => UseHopt(x, "act"));
            s.Run = null;
            s.Base = (x, c, st) => st.Race = "Machine";
            s.Stats = (x, c, st) => { if (st.Race != "Machine") return; if (c.Controller == x.Me) st.Add(500, 500); else st.Add(-500, -500); };
            s.GraveFx = new Script
            {
                Speed = 1,
                Can = x => Hopt(x) && x.My.Hand.Count > 0 && x.My.Deck.Any(c => c.Data.IsMonster && c.Data.Attribute == "EARTH" && c.Data.Race == "Machine"),
                Cost = async x => { UseHopt(x); await x.D.Banish(x.Self, x.Self); await x.D.Discard(x.Me, await x.PickN(x.My.Hand, 1, 1, "Discard 1 card"), x.Self); },
                Run = x => x.Search(c => c.Data.IsMonster && c.Data.Attribute == "EARTH" && c.Data.Race == "Machine", "Add which EARTH Machine?"),
            };
        }
        Spell("Cloning", async x =>
        {
            if (x.T0 is not { } t || !t.Valid() || !t.FaceUp) return;
            var tok = x.D.MakeToken("Clone Token", x.Me, t.Data.Atk, t.Data.Def, t.Data.Attribute, t.Data.Race, t.Data.Level);
            if (!await x.D.SpecialSummon(tok, x.Me, await x.D.ChoosePosition(x.Me, tok), by: x.Self)) return;
            var d = x.D;
            d.Watch(int.MaxValue, async e => { if (e.Kind == EvKind.Destroyed && e.Card == t && t.Loc == Loc.Grave && tok.Loc == Loc.Monster) await d.Destroy(tok); });
        }, x => x.Ev is { Kind: EvKind.Summoned } e && e.Player == x.Opp && e.Summoned(SummonKind.Normal, SummonKind.Tribute, SummonKind.Flip) && e.Card!.Valid() && e.Card.Data.Level > 0 && x.D.FreeMonsterZones(x.Me) > 0,
        async x => x.T.Add(x.Ev!.Card!));
        {
            var s = S("Cloudian - Eye of the Typhoon");
            s.On = async (x, e) =>
            {
                if (e.Kind == EvKind.AttackDeclared && e.Card == x.Self)
                    foreach (var m in x.D.FaceUpMonsters.Where(m => !m.In("Cloudian")).ToList()) await x.D.ChangePosition(m, !m.Defense);
            };
            Cloudian(s);
        }
        {
            var s = S("Cloudian - Ghost Fog");
            s.SummonOk = (x, by, k) => false;
            s.BattleDamage = (x, src, to, dmg) => x.D.Attacker == x.Self || x.D.AttackTarget == x.Self ? 0 : dmg;
            s.On = async (x, e) =>
            {
                if (!e.Is(EvKind.Destroyed, x.Self) || !e.Battle || e.Other is not { } by) return;
                int n = x.D.Level(by);
                for (int i = 0; i < n && x.D.FaceUpMonsters.Any(); i++)
                {
                    var t = await x.Pick(x.D.FaceUpMonsters, $"Place a Fog Counter ({i + 1}/{n})");
                    if (t != null) AddCounter(x, t, 1, "fog");
                }
            };
        }
        {
            var s = S("Cloudian - Nimbusman");
            s.TributeAny = (x, t) => x.IsAttr(t, "WATER") || (!t.FaceUp && t.Data.Attribute == "WATER");
            s.Stats = (x, c, st) => { if (c == x.Self) st.Add(500 * x.D.FieldCards.Sum(f => f.Counters("fog")), 0); };
            s.On = async (x, e) => { if (e.Is(EvKind.Summoned, x.Self) && e.Arg == (int)SummonKind.Tribute) AddCounter(x, x.Self, x.D.LastTributes.Count(t => t.Data.Attribute == "WATER"), "fog"); };
            Cloudian(s);
        }
        S("Cloudian - Poison Cloud").On = async (x, e) =>
        {
            if (e.Is(EvKind.Destroyed, x.Self) && e.Battle && x.Self.Loc == Loc.Grave && e.Other is { } by && !(x.D.AttackTarget == x.Self && x.D.TargetWasSet) && by.Valid())
            {
                await x.D.Destroy(by, x.Self);
                if (by.Loc != Loc.Monster) await x.D.Damage(x.Opp, 800, x.Self);
            }
        };
        S("Cloudian - Sheep Cloud").On = async (x, e) =>
        {
            if (!e.Is(EvKind.Destroyed, x.Self) || !e.Battle || x.Self.Loc != Loc.Grave) return;
            for (int i = 0; i < 2 && x.D.FreeMonsterZones(x.Me) > 0; i++)
            {
                var t = x.D.MakeToken("Cloudian Token", x.Me, 0, 0, "WATER", "Fairy", 1);
                if (await x.D.SpecialSummon(t, x.Me, true, by: x.Self)) t.Vars["notribute"] = 1;
            }
        };
        S("Cloudian - Sheep Cloud").Note = "Its Tokens can't be Tributed at all here (the \"except for a Cloudian\" exception isn't applied).";
        Spell("Cocoon Party", async x =>
        {
            int n = x.My.Grave.Where(c => c.Data.IsMonster && c.In("Neo-Spacian")).Select(c => c.Name).Distinct().Count();
            for (int i = 0; i < n; i++)
                if (!await SummonPick(x, x.My.Deck.Where(c => c.Data.IsMonster && c.In("Chrysalis")), $"Special Summon a Chrysalis monster ({i + 1}/{n})", optional: true)) break;
            x.D.Shuffle(x.Me);
        }, x => x.My.Grave.Any(c => c.Data.IsMonster && c.In("Neo-Spacian")) && x.My.Deck.Any(c => c.Data.IsMonster && c.In("Chrysalis")) && x.D.FreeMonsterZones(x.Me) > 0);
        Spell("Cocoon Veil", async x =>
        {
            var d = x.D;
            d.Linger(d.Turn, (q, c, p) => q == Q.TakeEffectDamage);
            var into = x.T0 != null ? ChrysalisInto(x.T0) : null;
            if (into == null) return;
            await SummonPick(x, x.My.Hand.Concat(x.My.Deck).Concat(x.D.Players.SelectMany(p => p.Grave)).Where(c => c.Name == into), $"Special Summon {into}");
            x.D.Shuffle(x.Me);
        }, x => x.MyMonsters().Any(m => m.FaceUp && m.In("Chrysalis") && ChrysalisInto(m) is { } n && x.My.Hand.Concat(x.My.Deck).Concat(x.D.Players.SelectMany(p => p.Grave)).Any(c => c.Name == n)),
        async x =>
        {
            var t = await x.Pick(x.MyMonsters().Where(m => m.FaceUp && m.In("Chrysalis") && ChrysalisInto(m) != null), "Tribute 1 face-up Chrysalis monster");
            if (t == null) return;
            x.T.Add(t); x.N = t.Data.Idx;
            await x.D.Tribute(t, x.Self);
        });
        S("Coffin Seller").On = async (x, e) =>
        {
            if (e.Kind == EvKind.ToGrave && e.Card!.Data.IsMonster && e.Card.Owner == x.Opp && x.Self.OnField && x.Self.FaceUp) await x.D.Damage(x.Opp, 300, x.Self);
        };
        {
            var s = S("Comic Hand");
            s.EquipOk = (x, m) => m.Controller == x.Opp;
            s.Can = x => x.D.CountActive("Toon World", x.Me) > 0 && x.D.FreeMonsterZones(x.Me) > 0 && x.D.EquipTargets(x.Self).Any();
            s.On = async (x, e) =>
            {
                if (e.Is(EvKind.Equipped, x.Self) && e.Other is { } m && m.Controller == x.Opp) await x.D.TakeControl(m, x.Me);
                if (x.Self.OnField && x.Self.FaceUp && x.D.CountActive("Toon World") == 0 && e.Kind != EvKind.Activated) await x.D.Destroy(x.Self, x.Self);
            };
            s.Allow = (x, q, c) => q == Q.DirectAttack && c == x.Self.EquippedTo && !x.OppMonsters().Any(m => m.FaceUp && m.In("Toon"));
            s.Note = "The equipped monster attacks directly while your opponent has no Toon monster; other Toon rules are not applied to it.";
        }
        {
            var s = S("Command Knight");
            s.Stats = (x, c, st) => { if (c.Controller == x.Me && st.Race == "Warrior") st.Add(400, 0); };
            s.Forbid = (x, q, c) => q == Q.BeAttacked && c == x.Self && x.MyMonsters().Any(m => m != x.Self);
        }
        {
            var s = Spell("Common Soul", async x =>
            {
                if (x.T0 is not { } t || !t.Valid() || !t.FaceUp) return;
                var n = await x.Pick(x.My.Hand.Where(c => c.Data.IsMonster && c.In("Neo-Spacian") && x.D.CanSpecialSummon(c, t.Controller, by: x.Self)), "Special Summon which Neo-Spacian?");
                if (n == null || !await x.D.SpecialSummon(n, t.Controller, await x.D.ChoosePosition(x.Me, n), by: x.Self)) return;
                x.Self.Vars["stay"] = 1;
                Tie(x, n);
                x.D.Flags[$"soul{x.Self.Uid}"] = t.Uid + 1;
            }, x => x.Targetable(x.D.FaceUpMonsters).Any() && x.My.Hand.Any(c => c.Data.IsMonster && c.In("Neo-Spacian")),
            async x => await x.Target(x.D.FaceUpMonsters, "Select 1 face-up monster"));
            s.Stats = (x, c, st) =>
            {
                if (x.D.Flags.GetValueOrDefault($"soul{x.Self.Uid}") == c.Uid + 1 && TiedMonster(x) is { Loc: Loc.Monster, FaceUp: true } n) st.Add(x.D.StatOf(n).BaseAtk + x.D.StatOf(n).Atk, 0);
            };
            s.On = async (x, e) => { if (e.Is(EvKind.LeftField, x.Self) && TiedMonster(x) is { Loc: Loc.Monster } n) { x.D.Flags.Remove($"tie{x.Self.Uid}"); await x.D.ToHand(n, x.Self); } };
        }
        Spell("Compulsory Evacuation Device", async x => { if (x.T0 is { } t && t.Valid()) await x.D.ToHand(t, x.Self); },
            x => x.Targetable(x.AllMonsters()).Any(), async x => await x.Target(x.AllMonsters(), "Return which monster to the hand?"));
        Spell("Contact", async x =>
        {
            var chrys = x.MyMonsters().Where(m => m.FaceUp && m.In("Chrysalis")).ToList();
            var names = chrys.Select(ChrysalisInto).Where(n => n != null).ToHashSet();
            foreach (var c in chrys) await x.D.ToGrave(c, x.Self);
            await SummonPick(x, x.My.Hand.Concat(x.My.Deck).Where(c => names.Contains(c.Name)), "Special Summon which monster?");
            x.D.Shuffle(x.Me);
        }, x => x.MyMonsters().Any(m => m.FaceUp && m.In("Chrysalis")));
        {
            static List<string> NeosMats(Card f) => System.Text.RegularExpressions.Regex.Matches(f.Data.Desc.Split('\n')[0], "\"([^\"]+)\"").Select(m => m.Groups[1].Value).ToList();
            Spell("Contact Out", async x =>
            {
                if (x.T0 is not { } f || !f.Valid()) return;
                var names = NeosMats(f);
                await x.D.MoveTo(f, Loc.Extra);
                var mats = names.Select(n => x.My.Deck.FirstOrDefault(c => string.Equals(c.Name, n, StringComparison.OrdinalIgnoreCase))).ToList();
                if (mats.All(m => m != null) && mats.Distinct().Count() == mats.Count && x.D.FreeMonsterZones(x.Me) >= mats.Count && await x.Ask("Special Summon its Fusion Materials from your Deck?"))
                    foreach (var m in mats) await x.D.SpecialSummon(m!, x.Me, await x.D.ChoosePosition(x.Me, m!), by: x.Self);
                x.D.Shuffle(x.Me);
            }, x => x.Targetable(x.MyMonsters().Where(m => m.FaceUp && m.Data.IsFusion && m.In("Neos"))).Any(),
            async x => await x.Target(x.MyMonsters().Where(m => m.FaceUp && m.Data.IsFusion && m.In("Neos")), "Return which Neos Fusion Monster?"));
        }
        Spell("Contract with Exodia", async x => await SummonPick(x, x.My.Hand.Where(c => c.Name == "Exodia Necross"), "Special Summon Exodia Necross"),
            x => Duel.Exodia.All(n => x.My.Grave.Any(c => c.Name == n)) && x.My.Hand.Any(c => c.Name == "Exodia Necross"));
        Spell("Convert Contact", async x =>
        {
            var h = await x.Pick(x.My.Hand.Where(c => c.Data.IsMonster && c.In("Neo-Spacian")), "Send 1 Neo-Spacian from your hand to the GY");
            var d = await x.Pick(x.My.Deck.Where(c => c.Data.IsMonster && c.In("Neo-Spacian")), "Send 1 Neo-Spacian from your Deck to the GY");
            if (h == null || d == null) return;
            await x.D.ToGrave(h, x.Self); await x.D.ToGrave(d, x.Self);
            x.D.Shuffle(x.Me);
            await x.D.Draw(x.Me, 2);
        }, x => !x.MyMonsters().Any() && x.My.Hand.Any(c => c.Data.IsMonster && c.In("Neo-Spacian")) && x.My.Deck.Any(c => c.Data.IsMonster && c.In("Neo-Spacian")));
        Spell("Cost Down", async x =>
        {
            foreach (var c in x.My.Hand.Where(c => c.Data.IsMonster)) { c.Vars["lvdown"] = 2; c.Vars["lvdownturn"] = x.D.Turn; }
        }, x => x.My.Hand.Any(c => c != x.Self), async x => await x.D.Discard(x.Me, await x.PickN(x.My.Hand.Where(c => c != x.Self), 1, 1, "Discard 1 card"), x.Self));
        Spell("Counter Gem", async x =>
        {
            foreach (var c in x.My.Spells.Where(c => c != null && c != x.Self).Select(c => c!).ToList()) await x.D.ToGrave(c, x.Self);
            foreach (var c in x.My.Grave.Where(c => c.Data.IsMonster && c.In("Crystal Beast")).ToList())
                if (!await PlaceAsSpell(x, c)) break;
            var d = x.D; int me = x.Me;
            d.Later(d.Turn, Phase.End, async () => await d.Destroy(d.P(me).FieldCards.Where(c => c.FaceUp && c.In("Crystal Beast")).ToList()));
        });
        Spell("Creature Swap", async x =>
        {
            var mine = await x.Pick(x.MyMonsters(), "Choose 1 monster you control to swap");
            var theirs = await x.D.PickOne(x.Opp, x.OppMonsters(), "Choose 1 monster you control to swap", x.Self);
            if (mine == null || theirs == null) return;
            int zm = mine.Zone, zt = theirs.Zone;
            x.D.P(x.Me).Monsters[zm] = theirs; x.D.P(x.Opp).Monsters[zt] = mine;
            (mine.Zone, theirs.Zone) = (zt, zm);
            mine.Controller = x.Opp; theirs.Controller = x.Me;
            mine.Vars["nochangepos"] = x.D.Turn; theirs.Vars["nochangepos"] = x.D.Turn;
            await x.D.Raise(new Ev { Kind = EvKind.ControlChanged, Card = mine, Player = x.Opp, Arg = x.Me });
            await x.D.Raise(new Ev { Kind = EvKind.ControlChanged, Card = theirs, Player = x.Me, Arg = x.Opp });
        }, x => x.MyMonsters().Any() && x.OppMonsters().Any());
        Spell("Cross Counter", async x =>
        {
            var e = x.Ev!;
            if (e.Card is not { } a) return;
            x.D.BattleFlags.Add($"double{x.Opp}");
            var d = x.D;
            d.Watch(d.Turn, async ev => { if (ev.Kind == EvKind.AfterDamage && ev.Card == a && a.Valid() && a.Var("crosscounter") != d.Turn) { a.Vars["crosscounter"] = d.Turn; await d.Destroy(a); } });
        }, x => x.Ev is { Kind: EvKind.AttackDeclared } e && e.Player == x.Opp && e.Other is { } t && t.Controller == x.Me && t.Defense && (!t.FaceUp || x.Def(t) > x.Atk(e.Card!)));
        S("Cross Counter").Note = "Activated on the attack (its DEF is checked then; a face-down defender may turn out lower): your opponent's battle damage that battle is doubled, then the attacker is destroyed.";
        {
            var s = S("Cross Porter");
            s.Can = x => Up(x) && x.MyMonsters().Any() && x.My.Hand.Any(c => c.Data.IsMonster && c.In("Neo-Spacian"));
            s.Cost = async x => { var m = await x.Pick(x.MyMonsters(), "Send 1 monster you control to the GY"); if (m != null) await x.D.ToGrave(m, x.Self); };
            s.Run = async x => await SummonPick(x, x.My.Hand.Where(c => c.Data.IsMonster && c.In("Neo-Spacian")), "Special Summon which Neo-Spacian?");
            s.On = async (x, e) =>
            {
                if (e.Is(EvKind.ToGrave, x.Self) && x.My.Deck.Any(c => c.Data.IsMonster && c.In("Neo-Spacian")) && await x.Ask("Add a Neo-Spacian from your Deck to your hand?"))
                    await x.Search(c => c.Data.IsMonster && c.In("Neo-Spacian"), "Add which Neo-Spacian?");
            };
        }
        Spell("Crystal Beacon", async x =>
        {
            if (BeastsInSpellZone(x) < 2) return;
            await SummonPick(x, x.My.Deck.Where(c => c.Data.IsMonster && c.In("Crystal Beast")), "Special Summon which Crystal Beast?");
            x.D.Shuffle(x.Me);
        }, x => BeastsInSpellZone(x) >= 2 && x.My.Deck.Any(c => c.Data.IsMonster && c.In("Crystal Beast")) && x.D.FreeMonsterZones(x.Me) > 0);
        CrystalBeast("Crystal Beast Amber Mammoth").On = async (x, e) =>
        {
            if (e.Kind == EvKind.AttackDeclared && e.Player == x.Opp && Up(x) && e.Other is { } t && t != x.Self && t.Controller == x.Me && t.FaceUp && t.In("Crystal Beast")
                && await x.Ask("Make Amber Mammoth the attack target instead?"))
                x.D.AttackTarget = x.Self;
        };
        {
            var s = CrystalBeast("Crystal Beast Amethyst Cat");
            s.Allow = (x, q, c) => q == Q.DirectAttack && c == x.Self && Up(x);
            s.BattleDamage = (x, src, to, dmg) => src == x.Self && to == x.Opp && x.D.AttackTarget == null && x.Their.MonstersOnField.Any() ? dmg / 2 : dmg;
        }
        {
            var s = CrystalBeast("Crystal Beast Cobalt Eagle");
            s.OncePerTurn = true;
            s.Can = x => Up(x) && x.Targetable(x.My.FieldCards.Where(c => c.FaceUp && c.In("Crystal Beast"))).Any();
            s.Cost = async x => await x.Target(x.My.FieldCards.Where(c => c.FaceUp && c.In("Crystal Beast")), "Return which Crystal Beast card to the top of your Deck?");
            s.Run = async x => { if (x.T0 is { OnField: true } t) await x.D.ToDeck(t, top: true); };
        }
        CrystalBeast("Crystal Beast Ruby Carbuncle").On = async (x, e) =>
        {
            if (!e.Is(EvKind.Summoned, x.Self) || e.Arg is not ((int)SummonKind.Special or (int)SummonKind.Fusion or (int)SummonKind.Ritual)) return;
            var l = x.My.Spells.Where(c => c != null && c.Data.IsMonster && c.In("Crystal Beast")).Select(c => c!).ToList();
            if (l.Count == 0 || !await x.Ask("Special Summon the Crystal Beast cards in your Spell & Trap Zones?")) return;
            foreach (var c in l) if (x.D.CanSpecialSummon(c, x.Me, by: x.Self)) await x.D.SpecialSummon(c, x.Me, await x.D.ChoosePosition(x.Me, c), by: x.Self);
        };
        CrystalBeast("Crystal Beast Sapphire Pegasus").On = async (x, e) =>
        {
            if (!e.Is(EvKind.Summoned, x.Self) || x.My.FreeSpellZones == 0) return;
            var l = x.My.Hand.Concat(x.My.Deck).Concat(x.My.Grave).Where(c => c.Data.IsMonster && c.In("Crystal Beast")).ToList();
            if (l.Count == 0 || !await x.Ask("Place a Crystal Beast in your Spell & Trap Zone as a Continuous Spell?")) return;
            var c = await x.Pick(l, "Place which Crystal Beast?");
            bool deck = c?.Loc == Loc.Deck;
            if (c != null) await PlaceAsSpell(x, c);
            if (deck) x.D.Shuffle(x.Me);
        };
        CrystalBeast("Crystal Beast Topaz Tiger").CalcAtk = (x, c, vs, atk) => c == x.Self && x.D.Attacker == x.Self && vs != null ? atk + 400 : atk;
        Spell("Crystal Pair", async x =>
        {
            var c = await x.Pick(x.My.Deck.Where(c => c.Data.IsMonster && c.In("Crystal Beast")), "Place which Crystal Beast as a Continuous Spell?");
            if (c != null) await PlaceAsSpell(x, c);
            x.D.Shuffle(x.Me);
            x.D.TurnFlags.Add($"nobattledamage{x.Me}");
        }, x => x.Ev is { Kind: EvKind.Destroyed, Battle: true } e && e.Card!.Loc == Loc.Grave && e.Card.In("Crystal Beast")
            && x.D.LeftFieldLog.LastOrDefault(l => l.c == e.Card) is { c: not null } l && l.controller == x.Me && x.My.Deck.Any(c => c.Data.IsMonster && c.In("Crystal Beast")));
        {
            var s = S("Crystal Release");
            s.EquipOk = (x, m) => m.In("Crystal Beast");
            s.Stats = (x, c, st) => { if (c == x.Self.EquippedTo) st.Add(800, 0); };
            s.On = async (x, e) =>
            {
                if (e.Is(EvKind.LeftField, x.Self) && x.Self.Loc == Loc.Grave && x.My.FreeSpellZones > 0 && x.My.Deck.Any(c => c.Data.IsMonster && c.In("Crystal Beast"))
                    && await x.Ask("Place a Crystal Beast from your Deck as a Continuous Spell?"))
                { var c = await x.Pick(x.My.Deck.Where(c => c.Data.IsMonster && c.In("Crystal Beast")), "Place which?"); if (c != null) await PlaceAsSpell(x, c); x.D.Shuffle(x.Me); }
            };
        }
        {
            var s = S("Crystal Tree");
            s.On = async (x, e) => { if (e.Kind == EvKind.Placed && e.Card!.In("Crystal Beast") && x.Self.OnField && x.Self.FaceUp) AddCounter(x, x.Self, 1, "crystal"); };
            s.AddFx(f =>
            {
                f.Label = "Place Crystal Beasts";
                f.Can = x => x.Self.Counters("crystal") > 0;
                f.Cost = async x => { x.N = x.Self.Counters("crystal"); await x.D.ToGrave(x.Self, x.Self); };
                f.Run = async x =>
                {
                    for (int i = 0; i < x.N && x.My.FreeSpellZones > 0; i++)
                    {
                        var c = await x.Pick(x.My.Deck.Where(c => c.Data.IsMonster && c.In("Crystal Beast")), $"Place a Crystal Beast ({i + 1}/{x.N})");
                        if (c == null) break;
                        await PlaceAsSpell(x, c);
                    }
                    x.D.Shuffle(x.Me);
                };
            });
        }
        Spell("Cup of Ace", async x => await x.D.Draw(x.D.TossCoins(x.Me, 1, x.Self) == 1 ? x.Me : x.Opp, 2),
            x => x.My.Deck.Count >= 2 || x.Their.Deck.Count >= 2);
        Spell("Curse of Anubis", async x =>
        {
            foreach (var m in x.AllMonsters().Where(m => m.FaceUp && m.Data.IsEffect).ToList())
            {
                if (!m.Defense) await x.D.ChangePosition(m, true);
                m.Vars["basedef"] = 1; m.Vars["basedefturn"] = x.D.Turn; m.Vars["nochangepos"] = x.D.Turn;
            }
        }, x => x.AllMonsters().Any(m => m.FaceUp && m.Data.IsEffect));
        {
            var s = Spell("Cursed Seal of the Forbidden Spell", async x =>
            {
                var l = x.Ev!.Link!;
                await x.D.Negate(l);
                string name = l.Card.Name; int opp = l.Player; var d = x.D;
                d.Linger(int.MaxValue, (q, c, p) => q == Q.Activate && c != null && c.Name == name && c.Controller == opp);
            }, x => x.Ev is { Kind: EvKind.Activated, Link: { IsCardActivation: true } l } && l.Card.Data.IsSpell && l.Player == x.Opp && x.My.Hand.Any(c => c.Data.IsSpell),
            async x => await x.D.Discard(x.Me, await x.PickN(x.My.Hand.Where(c => c.Data.IsSpell), 1, 1, "Discard 1 Spell"), x.Self));
        }
        {
            var s = S("Cyber Angel Benten");
            s.On = async (x, e) =>
            {
                if (e.Kind == EvKind.BattleDestroyedMonster && e.Card == x.Self && e.Other!.Loc == Loc.Grave) await x.D.Damage(x.Opp, e.Other.Data.Def, x.Self);
                if (e.Is(EvKind.ToGrave, x.Self) && x.D.Flags.GetValueOrDefault($"tributed{x.Self.Uid}") == x.D.Turn && e.From is Loc.Monster or Loc.Hand
                    && x.My.Deck.Any(c => c.Data.IsMonster && c.Data.Attribute == "LIGHT" && c.Data.Race == "Fairy") && await x.Ask("Add a LIGHT Fairy monster from your Deck?"))
                    await x.Search(c => c.Data.IsMonster && c.Data.Attribute == "LIGHT" && c.Data.Race == "Fairy", "Add which LIGHT Fairy?");
            };
        }
        {
            var s = S("Cyber Angel Dakini");
            s.Allow = (x, q, c) => q == Q.Piercing && c != null && c.Controller == x.Me && c.Data.IsRitual;
            s.On = async (x, e) =>
            {
                if (e.Is(EvKind.Summoned, x.Self) && e.Arg == (int)SummonKind.Ritual && x.OppMonsters().Any() && await x.Ask("Make your opponent send 1 monster they control to the GY?"))
                { var t = await x.D.PickOne(x.Opp, x.OppMonsters(), "Send 1 monster you control to the GY", x.Self); if (t != null) await x.D.ToGrave(t, x.Self); }
                if (e.Kind == EvKind.EndPhase && e.Player == x.Me && Up(x) && x.Self.Var("dakiniturn") != x.D.Turn)
                {
                    var l = x.Targetable(x.My.Grave.Where(c => c.Data.IsRitual || c.Name == "Machine Angel Ritual")).ToList();
                    if (l.Count == 0 || !await x.Ask("Add a Ritual Monster or Machine Angel Ritual from your GY?")) return;
                    x.Self.Vars["dakiniturn"] = x.D.Turn;
                    var c = await x.Pick(l, "Add which?");
                    if (c != null) await x.D.ToHand(c, x.Self);
                }
            };
        }
    }
}
