using System.Text.Json;

namespace PoC.Engine;

/// The original CPU opponent (remake/data/ai.json, decoded from yugi_pc.exe), answering engine prompts.
/// A rule script with no look-ahead: each turn walks fixed stages (first-use cards, summon/effects/spells,
/// positions, battle if profitable, Main 2, set traps), responds with the first set trap whose approval rule
/// passes, and picks targets/tributes by fixed priorities.
public enum Brain { Classic, Plus, Seer }

public sealed partial class Ai
{
    readonly int me;
    public readonly Brain Brain;
    readonly Rng rng;
    int turn = -1;
    readonly HashSet<string> tried = new();   // actions taken this turn (stops ping-pong, e.g. position changes)

    static Lists? lists;
    sealed class Lists
    {
        public string[] First = Array.Empty<string>(), Spells = Array.Empty<string>(), Equips = Array.Empty<string>(),
            MonsterEffects = Array.Empty<string>(), HighValue = Array.Empty<string>(), Always = Array.Empty<string>();
    }

    public Ai(int player, ulong seed, Brain brain = Brain.Classic)
    {
        me = player;
        Brain = brain;
        rng = new Rng(seed);
        lists ??= Load();
    }

    static Lists Load()
    {
        using var s = typeof(Ai).Assembly.GetManifestResourceStream("ai.json") ?? throw new FileNotFoundException("ai.json");
        using var d = JsonDocument.Parse(s);
        var l = d.RootElement.GetProperty("lists");
        string[] Names(string prefix) => l.EnumerateObject().First(p => p.Name.StartsWith(prefix)).Value.EnumerateArray().Select(x => x.GetProperty("name").GetString()!).ToArray();
        return new Lists
        {
            First = Names("stage1_first_use"), Spells = Names("spell_activation_order"), Equips = Names("equip_order"),
            MonsterEffects = Names("monster_effects"), HighValue = Names("high_value"),
            Always = d.RootElement.GetProperty("responses").EnumerateObject().First(p => p.Name.StartsWith("approval_rules")).Value.GetProperty("always").EnumerateArray().Select(x => x.GetString()!).ToArray(),
        };
    }

    // ---------------------------------------------------------------- entry

    public Command Decide(Duel d)
    {
        var p = d.Pending ?? throw new InvalidOperationException("No prompt");
        if (d.Turn != turn) { turn = d.Turn; tried.Clear(); gains.Clear(); }
        // An open field is attacked (every brain; playtest 2026-11-09: the look-ahead talked itself out of free direct
        // attacks and the duel stalled). Once the rules have nothing left to play before battle, they decide.
        if ((Brain == Brain.Seer || JudgeWorlds > 0) && !Opp(d).MonstersOnField.Any() && (p.Kind == Ask.Battle || p.Kind == Ask.Main && d.Phase == Phase.Main1))
        {
            // Only asking: the rules' per-turn memory must not keep a play this look did not make (playtest 2026-11-12:
            // the summon / trap was marked done here, then never made - the CPU sat on its hand facing an empty field).
            var keep = tried.ToList();
            var open = Rule(d, p);
            if (p.Options[open[0]] is { Act: Act.ToBattle } or { Act: Act.Attack, Target: null }) return new Command(me, open);
            tried.Clear(); tried.UnionWith(keep);
        }
        if (Brain == Brain.Seer && Search(d, p) is int best && !SummonsExodia(p, best)) return new Command(me, new[] { SetFlips(p, best) });
        if (JudgeWorlds > 0 && Judge(d, p) is int[] judged && !(judged.Length == 1 && SummonsExodia(p, judged[0]))) return new Command(me, judged);
        return new Command(me, Rule(d, p));
    }

    /// The rule brain's answer (Classic / Classic+), no simulation.
    int[] Rule(Duel d, Prompt p)
    {
        int pick = p.Kind switch
        {
            Ask.Main => Main(d, p),
            Ask.Battle => Battle(d, p),
            Ask.Respond => Respond(d, p),
            Ask.Position => Position(d, p),
            Ask.YesNo => IsRaPay(p) && !RaPays(d, Others(d, p.Source!)) ? 1 : 0,
            Ask.Pick => -1,
            _ => 0,
        };
        if (p.Kind == Ask.Pick) return PickCards(d, p);
        if (pick < 0 || pick >= p.Options.Count) pick = p.Options.Count - 1;
        return new[] { SetFlips(p, pick) };
    }

    /// Guard rails on the look-ahead (playtest 2026-11-12). Its simulated worlds talked it out of plain good plays:
    /// it ended turns with a monster or a Trap still in hand, held a 2500 ATK monster back from monsters it beats,
    /// turned a 2600 DEF wall to its 100 ATK, and hid an attacker in Defense. The look-ahead may pick a different
    /// play, never "do nothing" over the rules' summon / set / attack, and never those two position changes.
    int Sane(Duel d, Prompt p, int pick, int rule)
    {
        if (pick == rule || pick < 0 || pick >= p.Options.Count) return pick;
        var o = p.Options[pick];
        if (o.Act is Act.EndTurn or Act.ToMain2)
        {
            if (p.Options[rule].Act is Act.Summon or Act.Set or Act.SetSpell or Act.Special or Act.FlipSummon or Act.Attack or Act.ToBattle) return rule;
            // Ending the turn instead of a card the rules would play is the look-ahead's call, but not when it also
            // throws away a Battle Phase the rules' count says pays (two 2500s left idle facing three Set cards).
            int toBattle = Find(p, x => x.Act == Act.ToBattle);
            if (p.Kind == Ask.Main && d.Phase == Phase.Main1 && toBattle >= 0
                && Score(d, Me(d).MonstersOnField.Where(m => m.FaceUp && !m.Defense && d.CanAttack(m)).Select(d.Atk).ToList()) > 0) return toBattle;
        }
        if (o is { Act: Act.ChangePos, Card: { FaceUp: true } c })
        {
            if (c.Defense && WantsDefense(d, d.Atk(c), d.Def(c))) return rule;
            if (!c.Defense && d.Phase == Phase.Main1 && d.Turn > 1 && d.CanAttack(c) && Opp(d).MonstersOnField.Any(t => d.Atk(c) > Guard(d, t))) return rule;
        }
        return pick;
    }

    static bool IsFlip(Card c) => c.Data.Desc.StartsWith("FLIP");
    /// Exodia pieces are a win condition in hand, never field material (every brain).
    static bool IsExodia(Card c) => Duel.Exodia.Contains(c.Name);
    /// The look-ahead brains see a 1000 ATK body; the pieces win in hand, so their Summons are vetoed (the rule brain decides).
    static bool SummonsExodia(Prompt p, int i) => p.Kind == Ask.Main && i >= 0 && i < p.Options.Count && p.Options[i] is { Act: Act.Summon or Act.Set, Card: { } c } && IsExodia(c);

    /// A FLIP monster Normal Summoned face-up throws its effect away: Set it instead (every brain).
    static int SetFlips(Prompt p, int i)
    {
        if (p.Kind != Ask.Main || p.Options[i] is not { Act: Act.Summon, Card: { } c } || !IsFlip(c)) return i;
        int set = Find(p, o => o.Act == Act.Set && o.Card == c);
        return set >= 0 ? set : i;
    }

    // ---------------------------------------------------------------- helpers

    Player Me(Duel d) => d.P(me);
    Player Opp(Duel d) => d.P(1 - me);
    static int Find(Prompt p, Func<Opt, bool> f) => p.Options.FindIndex(o => f(o));
    int Take(Prompt p, int i, string key) { if (i >= 0) tried.Add(key); return i; }
    bool Done(string key) => tried.Contains(key);

    /// What the CPU believes a monster's guard is. Face-down DEF is underestimated early (original quirk).
    int Guard(Duel d, Card c)
    {
        if (!c.FaceUp) return d.Turn < 15 ? c.Data.Def * (d.Turn + 5) / 20 : c.Data.Def;
        return c.Defense ? d.Def(c) : d.Atk(c);
    }

    int OppBestAtk(Duel d) => Opp(d).MonstersOnField.Where(c => c.FaceUp).Select(d.Atk).DefaultIfEmpty(0).Max();
    int MyBest(Duel d) => Me(d).MonstersOnField.Where(c => c.FaceUp).Select(c => Math.Max(d.Atk(c), d.Def(c))).DefaultIfEmpty(0).Max();

    /// The original board score: simulate the CPU's whole Battle Phase. Attackers in ascending ATK; each attacks
    /// directly when nothing is left, else the target it beats by the most (ties: higher DEF). Returns damage dealt
    /// plus 1 per destroyed monster (tie-break).
    int Score(Duel d, IEnumerable<int> attackers)
    {
        var targets = Opp(d).MonstersOnField.Select(c => (atk: c.FaceUp ? d.Atk(c) : 0, guard: Guard(d, c), def: !c.FaceUp || c.Defense)).ToList();
        int dmg = 0, kills = 0;
        foreach (var a in attackers.OrderBy(x => x))
        {
            if (targets.Count == 0) { dmg += a; continue; }
            var beaten = targets.Where(t => a > t.guard).OrderByDescending(t => a - t.guard).ThenByDescending(t => t.guard).ToList();
            if (beaten.Count == 0) continue;
            var best = beaten[0];
            if (!best.def) dmg += a - best.atk;
            targets.Remove(best);
            kills++;
        }
        return dmg * 10 + kills;
    }

    /// The original position comparator: true = Defense.
    bool WantsDefense(Duel d, int atk, int def)
    {
        int oppBest = OppBestAtk(d);
        // A wall (or a weak FLIP monster) left in Attack only feeds the opponent's next attack (every brain).
        if (def > atk && atk < 1500) return true;
        if (!Opp(d).MonstersOnField.Any()) return false;
        if (def > atk && def > 1500 && def >= MyBest(d)) return true;
        if (atk >= def && oppBest - atk >= 1000) return true;
        return false;
    }

    // "...each monster you control" is not a take-control card: drop the phrase before keyword checks (every brain;
    // United We Stand / Mage Power were equipped onto the opponent's monster).
    string? Does(Duel d, Card c) => d.Db.Effects.TryGetValue(c.Data.KonamiId, out var e) ? e.Does.ToLowerInvariant().Replace("you control", "").Replace("they control", "") : null;

    // ---------------------------------------------------------------- Main Phase

    int Main(Duel d, Prompt p)
    {
        bool main1 = d.Phase == Phase.Main1;
        int i;
        // Stage 1: first-use cards (heal, burn, spell/trap removal) when their condition holds.
        foreach (var name in lists!.First)
        {
            i = Find(p, o => o.Act == Act.Activate && o.Card!.Name == name && !Done("a" + o.Card.Uid) && FirstUseOk(d, o.Card));
            if (i >= 0) return Take(p, i, "a" + p.Options[i].Card!.Uid);
        }
        // Stage 2: summon, monster effects, spells, then normal summon / set.
        i = SummonChoice(d, p);
        if (i >= 0) return Take(p, i, "s");
        i = GodEffect(d, p);
        if (i >= 0) return i;
        // Special Summon procedures (Spirit of Flames, Dark Necrofear, Gate Guardian...): the original held these in
        // hand forever (every brain).
        i = Find(p, o => o.Act == Act.Special && !Done("x" + o.Card!.Uid) && o.Card.Data.Atk >= 1200);
        if (i >= 0) return Take(p, i, "x" + p.Options[i].Card!.Uid);
        foreach (var name in lists.MonsterEffects)
        {
            i = Find(p, o => o.Act == Act.Activate && o.Card!.OnField && o.Card.Name == name && !Done("e" + o.Card.Uid));
            if (i >= 0) return Take(p, i, "e" + p.Options[i].Card!.Uid);
        }
        i = SpellChoice(d, p);
        if (i >= 0) return i;
        // Stage 3: positions and flips (Classic+: first switch everything to attack when that is lethal).
        if (Brain != Brain.Classic && main1 && (i = LethalPush(d, p)) >= 0) return i;
        i = PositionChoice(d, p);
        if (i >= 0) return i;
        // Stage 4: battle when the simulation finds a profit.
        if (main1)
        {
            int toBattle = Find(p, o => o.Act == Act.ToBattle);
            var attackers = Me(d).MonstersOnField.Where(c => c.FaceUp && !c.Defense).Select(d.Atk).ToList();
            if (toBattle >= 0 && attackers.Count > 0 && Score(d, attackers) > 0) return toBattle;
        }
        // Stage 6: set traps (and quick-play spells) before ending the turn.
        i = Find(p, o => o.Act == Act.SetSpell && (o.Card!.Data.IsTrap || o.Card.Data.Is("Quick-Play")) && !Done("t" + o.Card.Uid));
        if (i >= 0) return Take(p, i, "t" + p.Options[i].Card!.Uid);
        return Find(p, o => o.Act == Act.EndTurn);
    }

    bool FirstUseOk(Duel d, Card c)
    {
        var does = Does(d, c) ?? "";
        int mine = Me(d).Lp, theirs = Opp(d).Lp;
        if (does.Contains("gain") || does.Contains("recover")) return mine < 8000 && mine <= theirs + 1000;
        if (does.Contains("damage")) return theirs < 1000 || mine >= theirs + 500;
        if (does.Contains("destroy")) return Opp(d).SpellsOnField.Any();
        return true;
    }

    int SummonChoice(Duel d, Prompt p)
    {
        if (Done("s")) return -1;
        var opp = Opp(d).MonstersOnField.ToList();
        int oppBest = OppBestAtk(d);
        var current = Me(d).MonstersOnField.Where(c => c.FaceUp && !c.Defense).Select(d.Atk).ToList();
        int baseScore = Score(d, current);
        // Tribute summons: score the board with the new monster replacing its tributes; keep the best.
        int best = -1, bestScore = int.MinValue;
        for (int k = 0; k < p.Options.Count; k++)
        {
            var o = p.Options[k];
            if (o.Act != Act.Summon || o.Card!.Data.Level < 5) continue;
            int need = d.TributesFor(o.Card);   // the Gods take 3
            // a God on the field is never one of the Tributes (playtest 2026-11-12: Ra went to the GY for Sphere Mode / Summoned Skull)
            if (need > Me(d).MonstersOnField.Count(m => !IsGod(m))) continue;
            var rest = Me(d).MonstersOnField.OrderByDescending(TributeValue(d)).Take(Math.Max(0, Me(d).MonstersOnField.Count() - need))
                .Where(c => c.FaceUp && !c.Defense).Select(d.Atk).ToList();
            int atk = SummonAtk(d, o.Card, rest);
            if (IsGod(o.Card) && GodWorth(d, o.Card, atk)) return k;
            var keep = rest.Append(atk).ToList();
            int sc = Score(d, keep) + opp.Count(m => atk > Guard(d, m));
            if (sc > bestScore) { bestScore = sc; best = k; }
        }
        if (best >= 0 && bestScore >= baseScore) return best;
        // Flip monsters (set) when the opponent has monsters.
        if (opp.Count > 0)
        {
            int i = Find(p, o => o.Act == Act.Set && IsFlip(o.Card!));
            if (i >= 0) return i;
        }
        // Highest relevant stat that at least matches the opponent's best, else any level 1-4.
        var cands = Enumerable.Range(0, p.Options.Count).Where(k => p.Options[k].Act is Act.Summon or Act.Set && p.Options[k].Card!.Data.Level <= 4 && !IsExodia(p.Options[k].Card!))
            .OrderByDescending(k => Math.Max(p.Options[k].Card!.Data.Atk, p.Options[k].Card!.Data.Def)).ToList();
        foreach (var k in cands)
        {
            var c = p.Options[k].Card!;
            int stat = Math.Max(c.Data.Atk, c.Data.Def);
            if (stat < oppBest) continue;
            return Pos(p, k, WantsDefense(d, c.Data.Atk, c.Data.Def));
        }
        if (cands.Count > 0)
        {
            var c = p.Options[cands[0]].Card!;
            return Pos(p, cands[0], WantsDefense(d, c.Data.Atk, c.Data.Def) || opp.Count > 0);
        }
        return -1;
    }

    // ---------------------------------------------------------------- Egyptian Gods (every brain)

    static bool IsGod(Card c) => c.Data.Race == "Divine-Beast";
    static bool IsRaPay(Prompt p) => p.Kind == Ask.YesNo && p.Source?.Name == "The Winged Dragon of Ra" && p.Text.StartsWith("Pay");
    List<int> Others(Duel d, Card not) => Me(d).MonstersOnField.Where(m => m != not && m.FaceUp && !m.Defense).Select(d.Atk).ToList();

    /// The ATK a monster will have once Normal Summoned: printed ATK, except the Gods whose ATK is made on the field.
    int SummonAtk(Duel d, Card c, List<int> others) => c.Name switch
    {
        "Slifer the Sky Dragon" => 1000 * (Me(d).Hand.Count - 1),
        "The Winged Dragon of Ra" => RaPays(d, others) ? Me(d).Lp - 100 : 0,
        _ => c.Data.Atk,
    };

    /// Ra's "pay so you only have 100 LP": only for the kill this turn, or against an opponent with nothing left to
    /// answer it (no Spell/Trap set, at most one card in hand, nothing that stands up to Ra). At 100 LP any burn or
    /// direct hit ends the duel and Ra's own destroy effect (1000 LP) is gone, so a slow game is never worth it.
    bool RaPays(Duel d, List<int> others)
    {
        var opp = Opp(d);
        int pay = Me(d).Lp - 100;
        if (pay <= 0) return false;
        if (d.Phase == Phase.Main1 && d.Turn > 1 && Score(d, others.Append(pay)) / 10 >= opp.Lp) return true;
        return pay >= opp.Lp && opp.Hand.Count <= 1 && !opp.SpellsOnField.Any() && opp.MonstersOnField.All(m => Guard(d, m) < pay);
    }

    /// A God is worth its 3 Tributes: Obelisk always; Slifer with a hand to fuel it; Ra when it pays for the kill or
    /// can start shooting monsters (1000 LP each); Sphere Mode when it can call Ra.
    bool GodWorth(Duel d, Card c, int atk) => c.Name switch
    {
        "Obelisk the Tormentor" => true,
        "Slifer the Sky Dragon" => atk >= Math.Max(2000, OppBestAtk(d)),
        "The Winged Dragon of Ra" => atk > 0 || Me(d).Lp >= 3000 && RaShot(d),
        "The Winged Dragon of Ra - Sphere Mode" => !Me(d).MonstersOnField.Any(IsGod) && Me(d).Hand.Concat(Me(d).Deck).Any(x => x.Name == "The Winged Dragon of Ra"),
        _ => false,
    };

    /// An opponent's monster worth 1000 LP of Ra's destroy effect.
    bool RaShot(Duel d) => Opp(d).MonstersOnField.Any(m => !m.FaceUp || Math.Max(d.Atk(m), d.Def(m)) >= 1000);

    /// The Gods' Main Phase effects: Obelisk's wipe when it opens lethal or takes 2+ monsters it can't beat (or 3+);
    /// Ra's destroy while LP stays above 2000; Sphere Mode always (it becomes a 4000 ATK Ra).
    int GodEffect(Duel d, Prompt p)
    {
        for (int i = 0; i < p.Options.Count; i++)
        {
            if (p.Options[i] is not { Act: Act.Activate, Card: { OnField: true } c } || !IsGod(c)) continue;
            string key = $"g{c.Uid}/{Me(d).Lp}";   // Ra may shoot again once its LP was paid
            if (Done(key)) continue;
            bool go = c.Name switch
            {
                "Obelisk the Tormentor" => ObeliskOk(d, c),
                "The Winged Dragon of Ra" => Me(d).Lp > 2000 && RaShot(d),
                "The Winged Dragon of Ra - Sphere Mode" => true,
                _ => false,
            };
            if (go) return Take(p, i, key);
        }
        return -1;
    }

    bool ObeliskOk(Duel d, Card ob)
    {
        var opp = Opp(d).MonstersOnField.ToList();
        if (opp.Count == 0) return false;
        int left = Me(d).MonstersOnField.Where(m => m != ob).OrderBy(TributeValue(d)).Skip(2)
            .Where(m => m.FaceUp && !m.Defense && d.CanAttack(m)).Sum(d.Atk);
        if (d.Phase == Phase.Main1 && d.Turn > 1 && left >= Opp(d).Lp) return true;   // the wipe opens a lethal attack
        return opp.Count >= 3 || opp.Count >= 2 && opp.Max(m => Guard(d, m)) >= d.Atk(ob);
    }

    /// Prefer Set (face-down Defense) over Summon for the same card when the comparator wants Defense.
    static int Pos(Prompt p, int k, bool defense)
    {
        var c = p.Options[k].Card;
        int i = Find(p, o => o.Card == c && o.Act == (defense ? Act.Set : Act.Summon));
        return i >= 0 ? i : k;
    }

    int SpellChoice(Duel d, Prompt p)
    {
        // Equips onto our monsters, in the original order.
        if (Me(d).MonstersOnField.Any(c => c.FaceUp))
            foreach (var name in lists!.Equips)
            {
                int i = Find(p, o => o.Act == Act.Activate && o.Card!.Name == name && !Done("a" + o.Card.Uid));
                if (i >= 0) return Take(p, i, "a" + p.Options[i].Card!.Uid);
            }
        foreach (var name in lists!.Spells)
        {
            int i = Find(p, o => o.Act == Act.Activate && o.Card!.Name == name && !o.Card.Data.IsMonster && !Done("a" + o.Card.Uid));
            if (i >= 0 && !SkipRoll(d)) return Take(p, i, "a" + p.Options[i].Card!.Uid);
        }
        // ponytail: the original spell planner (0x576740) is not decoded; other spells use their effect text.
        for (int i = 0; i < p.Options.Count; i++)
        {
            var o = p.Options[i];
            if (o.Act != Act.Activate || o.Card!.Data.IsMonster || o.Card.Data.IsTrap || Done("a" + o.Card.Uid)) continue;
            if (GenericSpellOk(d, o.Card)) return Take(p, i, "a" + o.Card.Uid);
        }
        return -1;
    }

    /// 1 in 8 "skip this activation", only while the opponent controls Magic Jammer.
    bool SkipRoll(Duel d) => Opp(d).SpellsOnField.Any(c => c.Name == "Magic Jammer") && rng.Int(8) == 0;

    bool GenericSpellOk(Duel d, Card c)
    {
        var does = Does(d, c);
        if (does == null) return false;
        var opp = Opp(d); var mine = Me(d);
        // Equip Spells outside the original list: boosts go on our face-up monster, weakeners on theirs (every brain).
        if (c.Data.Is("Equip") && !does.Contains("control"))
            return does.Contains("gain") ? mine.MonstersOnField.Any(m => m.FaceUp) : does.Contains("lose") || does.Contains("cannot") ? opp.MonstersOnField.Any(m => m.FaceUp) : false;
        if (does.Contains("destroy") && does.Contains("monster"))
            return opp.MonstersOnField.Count() > mine.MonstersOnField.Count() || OppBestAtk(d) > MyBest(d);
        if (does.Contains("destroy")) return opp.SpellsOnField.Any();
        if (does.Contains("control")) return opp.MonstersOnField.Any();
        if (does.Contains("special summon")) return mine.FreeMonsterZones > 0;
        if (does.Contains("damage")) return true;
        return false;
    }

    /// Classic+: a Defense monster that could switch to attack turns this Battle Phase into lethal.
    int LethalPush(Duel d, Prompt p)
    {
        var now = Me(d).MonstersOnField.Where(c => c.FaceUp && !c.Defense).Select(d.Atk).ToList();
        var extra = p.Options.Where(o => o.Card != null && !Done("p" + o.Card.Uid) &&
            (o.Act == Act.FlipSummon || (o.Act == Act.ChangePos && o.Card.FaceUp && o.Card.Defense))).ToList();
        if (extra.Count == 0 || Score(d, now) / 10 >= Opp(d).Lp) return -1;
        var all = now.Concat(extra.Select(o => o.Card!.FaceUp ? d.Atk(o.Card) : o.Card.Data.Atk)).ToList();
        if (Score(d, all) / 10 < Opp(d).Lp) return -1;
        return Take(p, p.Options.IndexOf(extra[0]), "p" + extra[0].Card!.Uid);
    }

    int PositionChoice(Duel d, Prompt p)
    {
        for (int i = 0; i < p.Options.Count; i++)
        {
            var o = p.Options[i];
            var c = o.Card;
            if (c == null || Done("p" + c.Uid)) continue;
            if (o.Act == Act.ChangePos && c.FaceUp)
            {
                bool def = WantsDefense(d, d.Atk(c), d.Def(c));
                if (def != c.Defense) return Take(p, i, "p" + c.Uid);
            }
            if (o.Act == Act.FlipSummon && !WantsDefense(d, c.Data.Atk, c.Data.Def)) return Take(p, i, "p" + c.Uid);
        }
        return -1;
    }

    // ---------------------------------------------------------------- Battle Phase

    int Battle(Duel d, Prompt p)
    {
        // Attack executor: weakest attacker first; direct if possible, else the target it beats by the most.
        var attackers = p.Options.Where(o => o.Act == Act.Attack).Select(o => o.Card!).Distinct().OrderBy(d.Atk);
        foreach (var a in attackers)
        {
            int direct = Find(p, o => o.Act == Act.Attack && o.Card == a && o.Target == null);
            if (direct >= 0) return direct;
            int atk = d.Atk(a), best = -1, margin = 0, guard = -1;
            for (int i = 0; i < p.Options.Count; i++)
            {
                var o = p.Options[i];
                if (o.Act != Act.Attack || o.Card != a || o.Target == null) continue;
                int g = Guard(d, o.Target);
                if (atk <= g) continue;
                if (atk - g > margin || (atk - g == margin && g > guard)) { best = i; margin = atk - g; guard = g; }
            }
            if (best >= 0) return best;
        }
        // No way out of the Battle Phase while a monster must attack (Berserk Gorilla, Staunch Defender): attack with it.
        int end = Find(p, o => o.Act == Act.ToMain2);
        return end >= 0 ? end : Find(p, o => o.Act == Act.Attack);
    }

    // ---------------------------------------------------------------- responses

    int Respond(Duel d, Prompt p)
    {
        // Hand trap path: Kuriboh when about to take battle damage.
        int kuri = Find(p, o => o.Card?.Name == "Kuriboh" && o.Card.Loc == Loc.Hand);
        if (kuri >= 0 && d.Attacker != null && d.Attacker.Controller != me) return kuri;
        // First face-down Spell/Trap in zone order whose approval passes.
        foreach (var c in Me(d).Spells)
        {
            if (c == null || c.FaceUp) continue;
            int i = Find(p, o => o.Act == Act.Activate && o.Card == c);
            if (i < 0) continue;
            if (!c.Data.IsTrap && !c.Data.Is("Quick-Play")) continue;
            // A second copy of a card already on this chain hits the same thing twice (two Spellbinding Circles on one
            // monster): every brain.
            if (Doubled(d, p.Options[i])) continue;
            if (Approve(d, c)) return i;
        }
        return p.Options.Count - 1; // Pass
    }

    bool Approve(Duel d, Card c)
    {
        if (lists!.Always.Contains(c.Name)) return true;
        var opp = Opp(d); var mine = Me(d);
        var attacker = d.Window?.Kind == EvKind.AttackDeclared ? d.Window.Card : d.Attacker;
        int incoming = attacker != null && attacker.Controller != me ? d.Atk(attacker) : 0;
        int oppMonsters = opp.MonstersOnField.Count(), myMonsters = mine.MonstersOnField.Count();
        if (Brain != Brain.Classic && PlusApprove(d, c, incoming) is bool plus) return plus;
        switch (c.Name)
        {
            case "Two-Pronged Attack":
                return opp.MonstersOnField.Any(m => m.FaceUp && d.Atk(m) > MyBest(d));
            case "Spellbinding Circle":   // never a second circle on a monster one already binds (every brain)
                return incoming > 0 && attacker != null && !Bound(d, attacker)   // the best of the monsters still free to attack (not one already bound or spent)
                    && incoming >= opp.MonstersOnField.Where(m => m == attacker || d.CanAttack(m) && !Bound(d, m)).Select(d.Atk).DefaultIfEmpty(0).Max();
            case "Ring of Destruction":
                return opp.MonstersOnField.Any(m => m.FaceUp && d.Atk(m) > opp.Lp && d.Atk(m) < mine.Lp);
            case "Reinforcements":
                return d.AttackTarget is { } t && t.Controller == me && attacker != null && d.Atk(attacker) - d.Atk(t) is > 0 and < 500;
            case "Just Desserts":
                return 500 * oppMonsters >= opp.Lp || rng.Int(3) + 1 < oppMonsters;
            case "Magic Thorn":
                return mine.Hand.Any(h => h.Name is "Card Destruction" or "Delinquent Duo");
            case "Mirror Force" or "Waboku":
                return incoming > 0 && (incoming >= mine.Lp || (myMonsters == 0 && oppMonsters >= 2) || mine.Lp < opp.Lp);
            case "Gift of The Mystical Elf":
                return oppMonsters + myMonsters >= 3;
            case "Ceasefire":
                {
                    int Eff(Player pl) => pl.MonstersOnField.Count(m => m.Data.IsEffect);
                    return 500 * (Eff(opp) + Eff(mine)) >= opp.Lp || Eff(opp) - Eff(mine) >= 2;
                }
            case "Aqua Chorus":
                return mine.MonstersOnField.Where(m => m.FaceUp).GroupBy(m => m.Name).Any(g => g.Count() > 1);
            case "Magic Cylinder":
                return incoming > 0 && (incoming * 2 >= opp.Lp || incoming >= mine.Lp || oppMonsters == 1);
            case "Torrential Tribute":
                return oppMonsters > 0;
        }
        // The original never fires the other ~60 traps (Acid Trap Hole, Magic Jammer...): a set card that just sits
        // there. Every brain uses the Classic+ rule for those (user: the CPU must not hold cards for nothing).
        if (Brain == Brain.Classic) return PlusApprove(d, c, incoming) is true;
        // A trap no rule knows (new cards): fire it at the event it answers (Trap Hole on a summon), not never.
        return d.WantsThisWindow(c);
    }

    /// Classic+ trap rules: the original approves ~40 of the 101 traps and never fires the rest (Magic Jammer,
    /// Solemn Judgment, Crush Card...). Null = keep the original rule for this card.
    bool? PlusApprove(Duel d, Card c, int incoming)
    {
        var opp = Opp(d); var mine = Me(d);
        var w = d.Window;
        bool oppTurn = d.TurnPlayer != me;
        var link = w?.Kind == EvKind.Activated && w.Link?.Player != me ? w.Link : null;   // opponent's card on the chain
        var summoned = w?.Kind == EvKind.Summoned && w.Card?.Controller != me ? w.Card : null;
        bool bigSummon = summoned != null && d.Atk(summoned) >= Math.Max(1800, MyBest(d));
        bool strongCard = link != null && (lists!.HighValue.Contains(link.Card.Name) || Has(Does(d, link.Card), "destroy", "control", "damage"));
        int oppAttackers = opp.MonstersOnField.Count(m => m.FaceUp && !m.Defense);
        switch (c.Name)
        {
            case "Magic Jammer" or "Magic Drain" or "Riryoku Field":
                return link != null && link.IsCardActivation && !link.Card.Data.IsTrap && strongCard;
            case "Solemn Judgment":
                return (link != null && link.IsCardActivation && strongCard) || (bigSummon && mine.Lp > 3000);
            case "Seven Tools of the Bandit":
                return link != null && link.IsCardActivation && link.Card.Data.IsTrap;
            case "Horn of Heaven":
                return bigSummon;
            case "Mirror Force":   // "beats my best" means nothing with no monsters: a lone small attacker isn't worth it
                return incoming > 0 && (oppAttackers >= 2 || MyBest(d) > 0 && incoming >= MyBest(d) || incoming >= mine.Lp);
            case "Magic Cylinder":
                return incoming > 0 && (incoming >= 1500 || incoming * 2 >= opp.Lp || incoming >= mine.Lp);
            case "Negate Attack" or "Kunai with Chain" or "Destruction Punch" or "Mask of Weakness" or "Dark Spirit of the Silent":
                return incoming > 0;
            case "Crush Card":
                return opp.MonstersOnField.Any(m => m.FaceUp && d.Atk(m) >= 1500);
            case "Acid Trap Hole":
                return opp.MonstersOnField.Any(m => !m.FaceUp);
            case "Dust Tornado":
                return opp.SpellsOnField.Any() && (w?.Kind == EvKind.EndPhase || opp.SpellsOnField.Any(s => s.FaceUp));
            case "Time Seal" or "Lightforce Sword":
                return oppTurn && w?.Kind == EvKind.EndPhase && opp.Hand.Count > 0;
            case "Windstorm of Etaqua":
                return oppTurn && w?.Kind == EvKind.BattleStart && opp.MonstersOnField.Any(m => m.FaceUp && !m.Defense && d.Atk(m) > MyBest(d));
            case "Chain Destruction":
                return summoned != null;
            case "Time Machine":
                return w?.Card?.Owner == me;
            case "Michizure" or "Shift" or "Fairy's Hand Mirror" or "Gust" or "Driving Snow" or "Gryphon Wing"
                or "White Hole" or "Anti Raigeki" or "Call of the Grave" or "Fake Trap":
                return true;   // only offered when their own trigger condition holds
        }
        return null;
    }

    static bool Has(string? s, params string[] w) => s != null && w.Any(s.Contains);

    /// A second copy of a card already on this chain, or a lock on an attacker one already holds.
    static readonly HashSet<string> Shields = new() { "Mirror Force", "Waboku", "Negate Attack", "Magic Cylinder", "Sakuretsu Armor", "Draining Shield", "Widespread Ruin", "Threatening Roar", "Hallowed Life Barrier" };

    /// One answer per attack (playtest 2026-11-12: Waboku and Mirror Force fired on the same attack). A second shield
    /// on a chain that already has one, or in a turn Waboku already covers, is kept for the next turn.
    bool ShieldSpent(Duel d, Card c) => Shields.Contains(c.Name)
        && (d.Chain.Any(l => l.Player == me && Shields.Contains(l.Card.Name)) || d.TurnFlags.Contains($"nobattledamage{me}"));

    bool Doubled(Duel d, Opt o) => o.Act == Act.Activate && o.Card is { } c && (ShieldSpent(d, c) || d.Chain.Any(l => l.Player == me && l.Card.Name == c.Name)
        || c.Name == "Spellbinding Circle" && ((d.Window?.Kind == EvKind.AttackDeclared ? d.Window.Card : d.Attacker) is { } a && Bound(d, a)
            // nothing but Defense Position monsters to bind: a wasted card (every brain, look-ahead included)
            || Opp(d).MonstersOnField.Where(m => m.FaceUp).All(m => m.Defense)));

    /// A face-up lock (Spellbinding Circle, Invitation to a Dark Sleep...) already holds this monster.
    static bool Bound(Duel d, Card m) => d.FieldCards.Any(f => f.FaceUp && f.Var("target") == m.Uid + 1);

    // ---------------------------------------------------------------- picks and positions

    int Position(Duel d, Prompt p)
    {
        var c = p.Source ?? p.Options[0].Card;
        bool def = c != null && WantsDefense(d, c.Data.Atk, c.Data.Def);
        int i = Find(p, o => o.Arg == (def ? 1 : 0));
        return i >= 0 ? i : 0;
    }

    /// Tribute order: Sangan, Witch of the Black Forest, then lowest DEF + 2*ATK.
    /// A God is never Tribute fodder, and a monster on the field counts at the ATK / DEF it has there (playtest
    /// 2026-11-12: Ra's printed ATK is 0, so the CPU Tributed its 4000 ATK Ra for Summoned Skull).
    Func<Card, int> TributeValue(Duel d) => c => c.Name == "Sangan" ? -2 : c.Name == "Witch of the Black Forest" ? -1 : IsGod(c) ? 1_000_000
        : c.OnField && c.FaceUp ? Math.Max(c.Data.Def, d.Def(c)) + 2 * Math.Max(c.Data.Atk, d.Atk(c)) : c.Data.Def + 2 * c.Data.Atk;

    int[] PickCards(Duel d, Prompt p)
    {
        var (order, n) = PickOrder(d, p);
        return order.Take(n).ToArray();
    }

    /// Every option, most wanted first, and how many of them the rule takes.
    (List<int> order, int n) PickOrder(Duel d, Prompt p)
    {
        int n = Math.Max(p.Min, Math.Min(1, p.Max));
        string text = p.Text.ToLowerInvariant(), does = p.Source != null ? Does(d, p.Source) ?? "" : "";
        static bool Has(string s, params string[] w) => w.Any(s.Contains);
        // Costs (tributes, discards, materials) spend our least valuable cards; hostile effects hit the
        // opponent's best card; everything else (equips, boosts, searches, revives) goes to our best.
        bool cost = Has(text, "tribute", "discard", "send", "material", "cost");
        bool hostile = !cost && (p.Source == null || !lists!.Equips.Contains(p.Source.Name))
            && (Has(text, "destroy", "return", "banish", "remove", "control", "take") || Has(does, "destroy", "opponent", "control", "return"));
        int Value(Card c)
        {
            if (cost && text.Contains("tribute")) return TributeValue(d)(c);
            if (IsExodia(c) && c.Controller == me && !c.OnField) return 9000;   // never discarded for a cost
            if (c.Data.IsMonster) return c.OnField && c.FaceUp ? Math.Max(d.Atk(c), d.Def(c)) : Math.Max(c.Data.Atk, c.Data.Def);
            int hv = Array.IndexOf(lists!.HighValue, c.Name);
            return hv >= 0 ? 5000 - hv * 100 : c.FaceUp ? 1000 : 1500;
        }
        bool Preferred(Card c) => hostile ? c.Controller != me : c.Controller == me || !c.OnField;
        // Hostile picks: the attacking monster first (Spellbinding Circle on attack); a monster another copy of this card
        // already locks last.
        // A lock on a Defense Position monster stops nothing (playtest 2026-11-09: Spellbinding Circle on a wall).
        bool circle = p.Source?.Name == "Spellbinding Circle";
        int Aim(Card c) => !hostile ? 0 : circle && c.OnField && c.Defense ? -2 : c == d.Attacker && c.Controller != me ? 1
            : p.Source != null && d.FieldCards.Any(f => f != p.Source && f.Name == p.Source.Name && f.Var("target") == c.Uid + 1) ? -1 : 0;
        var order = Enumerable.Range(0, p.Options.Count).OrderByDescending(i => Preferred(p.Options[i].Card!)).ThenByDescending(i => Aim(p.Options[i].Card!));
        order = cost ? order.ThenBy(i => Value(p.Options[i].Card!)) : order.ThenByDescending(i => Value(p.Options[i].Card!));
        // "Up to N" hostile effects (Penguin Soldier, Kycoo...) take every opponent card they may, and none of ours
        // unless the pick is mandatory.
        // ponytail: never its own card; a stronger CPU would bounce its own monster to shake off a lock or a harmful
        // equip (Spellbinding Circle, Paralyzing Potion) when that is worth more than another bounce of yours.
        if (hostile) n = Math.Clamp(p.Options.Count(o => Preferred(o.Card!)), p.Min, p.Max);
        return (order.ToList(), n);
    }
}
