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

    /// Exodia is still on for this side: all five pieces in hand or deck (long-term plan, user 2026-11-16). An Exodia
    /// Necross deck wants the pieces in the Graveyard instead: no such plan there.
    static bool ExodiaLive(Player p) => Duel.Exodia.All(n => p.Hand.Any(c => c.Name == n) || p.Deck.Any(c => c.Name == n))
        && !p.Hand.Concat(p.Deck).Any(c => c.Name is "Exodia Necross" or "Contract with Exodia");
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
        if (!c.FaceUp)
        {
            // Classic+ doesn't look under the card: a Set monster is guessed at a typical Set DEF.
            if (Brain != Brain.Classic && !Legacy) return SetGuess;
            return d.Turn < 15 ? c.Data.Def * (d.Turn + 5) / 20 : c.Data.Def;
        }
        return c.Defense ? d.Def(c) : d.Atk(c);
    }

    const int SetGuess = 1200;   // Classic+: a Set monster's DEF, unseen (1000-1400 played alike in leagues; 1800 lost)
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
        // Exodia plan: stall. Every monster that can wall goes to Defense while the pieces come.
        if (!Legacy && Brain != Brain.Classic && ExodiaLive(Me(d)) && Opp(d).MonstersOnField.Any() && def >= atk) return true;
        // A wall that is also the best attacker on the field goes hunting in Main Phase 1 (user 2026-11-16: walls
        // summoned in Defense left the player's weaker monsters alive to tribute up): it beats one of theirs now and
        // nothing of theirs beats it.
        if (!Legacy && Brain != Brain.Classic && d.Phase == Phase.Main1 && d.Turn > 1 && atk >= 1000 && oppBest < atk
            && Opp(d).MonstersOnField.Any(m => atk > Guard(d, m))) return false;
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
        // Classic+: draws and searches first, so the summon is chosen from the fuller hand.
        if (Brain != Brain.Classic && !Legacy)
        {
            i = Find(p, o => o.Act == Act.Activate && o.Card is { Loc: Loc.Hand } c && !c.Data.IsMonster && !c.Data.IsTrap && !Done("a" + c.Uid)
                && Fills(c) && PlusSpellOk(d, c));
            if (i >= 0) return Take(p, i, "a" + p.Options[i].Card!.Uid);
        }
        // Stage 2: summon, monster effects, spells, then normal summon / set.
        i = SummonChoice(d, p);
        if (i >= 0) return Take(p, i, "s");
        i = GodEffect(d, p);
        if (i >= 0) return i;
        // Special Summon procedures (Spirit of Flames, Dark Necrofear, Gate Guardian...): the original held these in
        // hand forever (every brain).
        i = Find(p, o => o.Act == Act.Special && !Done("x" + o.Card!.Uid) && o.Card.Data.Atk >= 1200 && (Legacy || !Spoils(d, o.Card)));
        if (i >= 0) return Take(p, i, "x" + p.Options[i].Card!.Uid);
        foreach (var name in lists.MonsterEffects)
        {
            i = Find(p, o => o.Act == Act.Activate && o.Card!.OnField && o.Card.Name == name && !Done("e" + o.Card.Uid));
            if (i >= 0) return Take(p, i, "e" + p.Options[i].Card!.Uid);
        }
        // Classic+: every other monster effect is read from its text (2026-11-16 card audit: the expansion monsters
        // never used their effects, only the named list did).
        if (Brain != Brain.Classic && !Legacy)
        {
            i = Find(p, o => o.Act == Act.Activate && o.Card is { Data.IsMonster: true } c && !lists.MonsterEffects.Contains(c.Name) && !Done("e" + c.Uid) && MonsterFxOk(d, c));
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
            if (need > Me(d).MonstersOnField.Count(m => !IsGod(m)) + (d.TurnFlags.Contains($"soulexchange{me}") ? 1 : 0)) continue;
            var rest = Me(d).MonstersOnField.OrderByDescending(TributeValue(d)).Take(Math.Max(0, Me(d).MonstersOnField.Count() - need))
                .Where(c => c.FaceUp && !c.Defense).Select(d.Atk).ToList();
            int atk = SummonAtk(d, o.Card, rest);
            if (IsGod(o.Card) && GodWorth(d, o.Card, atk)) return k;
            var keep = rest.Append(atk).ToList();
            int sc = Score(d, keep) + opp.Count(m => atk > Guard(d, m));
            if (sc > bestScore) { bestScore = sc; best = k; }
        }
        if (best >= 0 && bestScore >= baseScore) return best;
        // A piece of a plan we hold (contact fusion, Ojama Delta Hurricane!!, Phoenix Formation) before the strongest stat.
        if (!Legacy)
        {
            int piece = Find(p, o => o.Act is Act.Summon && o.Card!.Data.Level <= 4 && ComboPiece(d, o.Card));
            if (piece >= 0) return Pos(p, piece, false);   // face-up: the plans count face-up monsters
        }
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
            if (Brain == Brain.Classic || Legacy ? GenericSpellOk(d, o.Card) : PlusSpellOk(d, o.Card))
            {
                Take(p, i, "a" + o.Card.Uid);
                if (o.Card.Name == "Double Summon") tried.Remove("s");   // the second Normal Summon it grants
                return i;
            }
        }
        return -1;
    }

    /// Classic+: most expansion cards have no effect summary, so the keyword rules above never fired them (E - Emergency
    /// Call, Double Summon, field spells, rituals sat in hand all duel, in the look-ahead's simulated turns too). Their
    /// own text and frame decide instead.
    bool PlusSpellOk(Duel d, Card c)
    {
        var txt = c.Data.Desc.ToLowerInvariant();
        Player mine = Me(d), opp = Opp(d);
        // Exodia plan: never throw the pieces in hand away (Card Destruction, Morphing Jar's kind), dig deeper.
        bool exodia = !Legacy && ExodiaLive(mine);
        if (exodia && mine.Hand.Any(IsExodia) && Has(txt, "discard their entire hand", "discard your entire hand", "discards their entire hand", "send all cards in their hand", "shuffle their hand", "entire hand into"))
            return false;
        if (GenericSpellOk(d, c)) return true;
        if (c.Name == "Double Summon") return Done("s") && mine.Hand.Any(h => h != c && h.Data.IsMonster && h.Data.Level <= 4 && !IsExodia(h)) && mine.FreeMonsterZones > 0;
        if (txt.Contains("pay ") && mine.Lp <= 2000) return false;
        if (c.Name == "Megamorph") return mine.Lp > opp.Lp && opp.MonstersOnField.Any(m => m.FaceUp);   // the target picker aims it at their best: halved
        if (txt.Contains("take control")) return opp.MonstersOnField.Any(m => m.FaceUp) && mine.FreeMonsterZones > 0;
        if (c.Data.Is("Field")) return mine.FieldZone is not { FaceUp: true };
        if (c.Data.Is("Ritual")) return mine.FreeMonsterZones > 0;
        // Soul Exchange (2026-11-16 card scan: never played, the battle-skip rule below held it): when one of their
        // monsters makes up the last Tribute a big monster in hand needs, Battle Phase or not.
        if (c.Name == "Soul Exchange") return SoulExchangeOk(d);
        // Old cards the scan found never played (2026-11-16).
        if (c.Name == "Shield & Sword") return SwapWins(d);
        if (c.Name == "Polymerization" && HoldsDelta(d)) return false;   // the Ojamas are for Ojama Delta Hurricane!!, not Ojama King
        if (c.Name == "Mesmeric Control") return opp.MonstersOnField.Any(m => m.Defense);   // their walls stay walls next turn
        // Wipes of their side (Ojama Delta Hurricane!!, Harpie Lady Phoenix Formation: only offered once the pieces are out).
        if (Has(txt, "destroy all cards on your opponent's side", "destroy all cards your opponent controls")) return opp.FieldCards.Any();
        if (txt.Contains("monsters your opponent controls") && Has(txt, "destroy those targets", "destroy them")) return opp.MonstersOnField.Any();
        if (txt.Contains("destroy that target") && txt.Contains("spell/trap")) return opp.SpellsOnField.Any();   // Stamping Destruction
        // A card that stops our own attacks (not a token that "cannot attack": Fiend's Sanctuary).
        if (Has(txt, "skip your battle phase", "monsters you control cannot attack", "you cannot declare an attack", "you cannot conduct your battle")) return false;
        if (txt.Contains("add all cards in your hand to your deck"))   // Reload: a hand with no pieces, for an Exodia deck
            return exodia && !mine.Hand.Any(IsExodia) && mine.Hand.Count >= 2;
        if (txt.Contains("without tributing"))   // Big Evolution Pill: a big monster of the named Type waits in hand
            return mine.Hand.Any(h => h.Data.IsMonster && h.Data.Level >= 5 && !string.IsNullOrEmpty(h.Data.Race) && txt.Contains(h.Data.Race.ToLowerInvariant()));
        if (txt.Contains("destroy") && txt.Contains("defense position monster")) return opp.MonstersOnField.Any(m => m.Defense);   // Shield Crush
        if (Has(txt, "destroy spell/trap", "destroy 1 spell/trap", "spell/trap cards equal")) return opp.SpellsOnField.Any();      // R - Righteous Justice
        if (txt.Contains("banish all other cards")) return mine.Hand.Count <= 1 && mine.FieldCards.Count() <= 1;   // Card of Sanctity
        if (Has(txt, "add them to your hand", "to your hand", "to the hand") && !txt.Contains("add all cards in your hand")) return true;   // searches, recoveries
        if (Has(txt, "special summon", "fusion summon", "ritual summon")) return mine.FreeMonsterZones > 0;
        if (txt.Contains("from your deck to the gy")) return true;
        if (txt.Contains("draw") && !Has(txt, "each player", "your opponent draws", "add all cards")) return mine.Deck.Count > (exodia ? 2 : 5);
        // Pumps for one of ours: on an attacker this Main Phase 1.
        if (Has(txt, "gains", "gain ") && txt.Contains("atk") && !txt.Contains("opponent controls"))
            return d.Phase == Phase.Main1 && d.Turn > 1 && mine.MonstersOnField.Any(m => m.FaceUp && !m.Defense && d.CanAttack(m));
        return c.Data.Is("Continuous") && !txt.Contains("opponent controls");
    }

    bool SoulExchangeOk(Duel d)
    {
        if (Done("s") || d.Phase != Phase.Main1 || !Opp(d).MonstersOnField.Any()) return false;
        int have = Me(d).MonstersOnField.Count(m => !IsGod(m));
        return Me(d).Hand.Any(h => h.Data.IsMonster && d.TributesFor(h) is var need && need >= 2 && have < need && have + 1 >= need);
    }

    /// A monster's own effect (Main Phase, on the field, in hand or in the GY), judged from its text: the costs it asks
    /// for against what it does. Unknown kinds of effect are left alone.
    bool MonsterFxOk(Duel d, Card c)
    {
        var t = c.Data.Desc.ToLowerInvariant();
        Player mine = Me(d), opp = Opp(d);
        int spare = mine.Hand.Count(h => h != c && !IsExodia(h));
        bool lethal = Has(t, "inflict 1000", "inflict 1,000") ? opp.Lp <= 1000 : Has(t, "inflict 500") && opp.Lp <= 500;
        // Costs first.
        if (Has(t, "discard 1 card", "discard 1 other card", "send 1 card from your hand") && spare == 0) return false;
        if (t.Contains("pay ") && mine.Lp <= 2000) return false;
        if (t.Contains("tribute this card") && !lethal) return false;   // Ancient Gear Cannon, Cyber Kirin: not as a Main Phase play
        if (Has(t, "tribute 1 monster", "tribute 1 other monster"))
        {
            int fodder = mine.MonstersOnField.Where(m => m != c && !IsGod(m)).Select(m => Math.Max(d.Atk(m), d.Def(m))).DefaultIfEmpty(int.MaxValue).Min();
            if (fodder > 1000 && !lethal) return false;   // Super Conductor Tyranno: tokens and small monsters only
        }
        if (t.Contains("cannot declare an attack the turn") && d.Phase == Phase.Main1 && d.Turn > 1 && c.OnField && d.CanAttack(c)
            && opp.MonstersOnField.Any(m => d.Atk(c) > Guard(d, m))) return false;   // it has a fight to win first
        // Then what it does.
        if (Has(t, "draw 1 card", "draw 2 cards", "excavate", "look at 1 face-down")) return true;   // Library, Diamond Dude, Patroid
        if (t.Contains("to your hand") && !t.Contains("return all")) return true;                     // searches (Destroyersaurus)
        if (t.Contains("damage to your opponent")) return true;                                       // burn (Dunker, Tyranno)
        if (t.Contains("destroy that target"))
            return Has(t, "spell/trap", "spell or trap") ? opp.SpellsOnField.Any()
                : t.Contains("defense position monster") ? opp.MonstersOnField.Any(m => m.Defense) : opp.MonstersOnField.Any(m => m.FaceUp && !m.Defense);
        if (Has(t, "equip that target to this card", "take control")) return opp.MonstersOnField.Any();
        if (t.Contains("take on the atk and def")) return opp.MonstersOnField.Any(m => m.FaceUp && d.Atk(m) > d.Atk(c));   // Relinquished: their best, face-up
        if (t.Contains("equip this card to that target")) return c.OnField && !KeepForContact(d, c);  // Union monsters (Y-Dragon Head), not while a contact fusion needs them
        if (t.Contains("gains 500 atk for each")) return c.OnField && d.Phase == Phase.Main1 && d.Turn > 1 && d.CanAttack(c) && mine.Deck.Count > 10;   // Card Trooper
        if (t.Contains("change this card to face-down")) return d.Phase == Phase.Main2 && opp.MonstersOnField.Count() >= 2;   // Guardian Sphinx: flips next turn
        if (t.Contains("special summon")) return mine.FreeMonsterZones > 0;
        return false;
    }

    /// A spell that only adds cards to the hand (Pot of Greed, searches, recoveries).
    static bool Fills(Card c)
    {
        var t = c.Data.Desc.ToLowerInvariant();
        return c.Data.Is("Normal") && (t.Contains("draw") || t.Contains("to your hand")) && !Has(t, "discard", "each player", "banish all", "add all cards", "destroy", "special summon");
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
        // Classic+: the opponent's card is about to destroy Spells/Traps: what of ours can still go off, goes off now
        // (a Set card lost to Heavy Storm did nothing).
        if (Brain != Brain.Classic && !Legacy && SpellsThreatened(d))
        {
            int save = Find(p, o => o.Act == Act.Activate && o.Card is { OnField: true, FaceUp: false } && !Doubled(d, o));
            if (save >= 0) return save;
        }
        // Classic+: monster quick effects (Necro Gardna from the GY...) answer by the same text rules as traps.
        if (Brain != Brain.Classic && !Legacy)
        {
            int q = Find(p, o => o.Act == Act.Activate && o.Card is { Data.IsMonster: true } mc && mc.Name != "Kuriboh" && !Doubled(d, o) && Approve(d, mc));
            if (q >= 0) return q;
        }
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
        if (d.WantsThisWindow(c) || Legacy) return d.WantsThisWindow(c);
        // Cards that may go off at any time (Threatening Roar, Hero Barrier, Embodiment of Apophis...) never "want" one
        // window more than another: their text names the moment.
        var txt = c.Data.Desc.ToLowerInvariant();
        if (Has(txt, "cannot declare an attack", "negate 1 attack", "negate the attack", "end the battle phase", "negate the next attack"))
            return incoming > 0 && d.Window?.Kind == EvKind.AttackDeclared && (d.AttackTarget is not { } tg ? incoming >= Math.Min(1000, mine.Lp) : incoming > Guard(d, tg));
        // A face-down flip stops the attack (Book of Moon): on an attacker that would win.
        if (txt.Contains("change that target to face-down"))
            return incoming > 0 && d.Window?.Kind == EvKind.AttackDeclared && (d.AttackTarget is not { } bt ? incoming >= Math.Min(1500, mine.Lp) : incoming > Guard(d, bt));
        // Shrink: halving their monster turns a lost fight into a won one.
        if (c.Name == "Shrink" && d.Window?.Kind == EvKind.AttackDeclared && d.Attacker is { } sa && d.AttackTarget is { FaceUp: true } st)
        {
            if (sa.Controller != me && st.Controller == me) return d.Atk(sa) >= Guard(d, st) && d.Atk(sa) / 2 < Guard(d, st);   // their attack on mine
            if (sa.Controller == me && !st.Defense) return d.Atk(sa) <= d.Atk(st) && d.Atk(sa) > d.Atk(st) / 2;               // mine on their Attack Position monster
            return false;
        }
        // Equip Traps for ours (Metalmorph): when one of ours fights.
        if (Has(txt, "equipped monster", "equipped with this card") && Has(txt, "atk +", "gains", "increases its atk"))
            return d.Window?.Kind == EvKind.AttackDeclared && (d.Attacker?.Controller == me || d.AttackTarget is { FaceUp: true } mt && mt.Controller == me);
        if (txt.Contains("special summon this card"))
            return txt.Contains("during your opponent's main phase")   // Cyber Shadow Gardna: a wall on their turn
                ? d.TurnPlayer != me && d.Phase == Phase.Main1 && mine.FreeMonsterZones > 0
                : d.TurnPlayer == me && d.Window?.Kind == EvKind.MainStart && mine.FreeMonsterZones > 0;
        if (txt.Contains("to your opponent's field"))   // Ojama Trio: fill their zones at the end of their turn
            return d.TurnPlayer != me && d.Window?.Kind == EvKind.EndPhase && opp.FreeMonsterZones >= 3;
        if (txt.Contains("special summon that target") && Has(txt, "in your gy", "in your graveyard"))   // Rite of Spirit
            return mine.FreeMonsterZones > 0 && (d.TurnPlayer != me ? d.Window?.Kind is EvKind.EndPhase or EvKind.AttackDeclared : d.Window?.Kind == EvKind.MainStart);
        if (txt.Contains("negate any monster effects your opponent activates"))   // Harpie's Feather Storm
            return d.Window is { Kind: EvKind.Activated, Link: { } fl } && fl.Player != me && fl.Card.Data.IsMonster;
        return false;
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
            case "Ring of Destruction" when !Legacy:   // the finisher, or a big attacker both sides can afford to take the damage of
                return opp.MonstersOnField.Any(m => m.FaceUp && d.Atk(m) >= opp.Lp && d.Atk(m) < mine.Lp)
                    || oppTurn && incoming >= Math.Max(1800, MyBest(d) + 1) && incoming < mine.Lp - 1000 && d.Atk(opp.MonstersOnField.Where(m => m.FaceUp).MaxBy(d.Atk)!) == incoming;
            case "Shadow Spell":   // -700 on their attacker turns a lost fight into a won one, or on their best at a direct attack
                if (d.Window?.Kind != EvKind.AttackDeclared || (d.Window.Card ?? d.Attacker) is not { } sh || sh.Controller == me
                    || d.Atk(sh) < opp.MonstersOnField.Where(m => m.FaceUp).Select(d.Atk).DefaultIfEmpty(0).Max()) return false;
                return d.AttackTarget is { } shT ? shT.Controller == me && (d.Atk(sh) >= Guard(d, shT) && d.Atk(sh) - 700 < Guard(d, shT) || incoming >= 1900) : incoming >= 1500;
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
    /// Legacy keeps the named list only; otherwise any card whose text stops an attack counts (user 2026-11-16: Mirror
    /// Force then a chained Negate Attack, the second card does nothing).
    bool ShieldSpent(Duel d, Card c) => Shield(c)
        && (d.Chain.Any(l => l.Player == me && Shield(l.Card)) || d.TurnFlags.Contains($"nobattledamage{me}"));

    bool Shield(Card c) => Shields.Contains(c.Name) || !Legacy && c.Data.IsTrap && Has(c.Data.Desc.ToLowerInvariant(),
        "negate the attack", "negate 1 attack", "negate an attack", "end the battle phase", "destroy all attack position",
        "destroy the attacking monster", "take no battle damage", "battle damage you take", "battle damage becomes 0");

    /// A card that only answers an attack, fired before any attack (user 2026-11-16: Waboku at the start of the
    /// opponent's turn, then no attack came). Kept until an attack is declared, unless a card on the chain is about to
    /// destroy it anyway. "Cannot declare an attack" cards (Threatening Roar) are meant to go off before attacks.
    bool TooEarly(Duel d, Card c) => !Legacy && Shield(c) && d.TurnPlayer != me && d.Attacker == null
        && d.Window?.Kind != EvKind.AttackDeclared && !c.Data.Desc.ToLowerInvariant().Contains("cannot declare an attack") && !SpellsThreatened(d);

    /// The opponent's card on the chain is about to destroy Spells/Traps.
    bool SpellsThreatened(Duel d) => d.Window is { Kind: EvKind.Activated, Link: { } l } && l.Player != me
        && Has(l.Card.Data.Desc.ToLowerInvariant(), "destroy all spell", "destroy 1 spell", "destroy all spells", "destroy spell", "destroy all set", "spell/trap", "spell or trap");

    bool Doubled(Duel d, Opt o) => o.Act == Act.Activate && o.Card is { } c && (ShieldSpent(d, c) || TooEarly(d, c) || d.Chain.Any(l => l.Player == me && l.Card.Name == c.Name)
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
        var list = order.ToList();
        // Soul Exchange: their best monster is the first Tribute (only one of theirs may go).
        if (cost && text.Contains("tribute") && d.TurnFlags.Contains($"soulexchange{me}")
            && list.Where(i => p.Options[i].Card!.Controller != me).OrderByDescending(i => Math.Max(d.Atk(p.Options[i].Card!), d.Def(p.Options[i].Card!))).FirstOrDefault(-1) is int theirs and >= 0)
        { list.Remove(theirs); list.Insert(0, theirs); }
        return (list, n);
    }
}
