using System.Text.RegularExpressions;
using PoC.Engine;

namespace PoC.Check;

/// Replays the recorded original duels (remake/data/duel_logs, yugi_pc.exe command logs) against the engine.
/// The logs hold actions, not deck order, so each player's deck is stacked in first-use order (padded with a
/// vanilla filler), the engine is driven from the logged actions, and the outcome is compared: the sequence of
/// Life Point losses (op 0x43) and the winner (op 0x04).
static class LogReplay
{
    record Rec(int Line, int P, int Op, int A, int B, int C);

    enum K { Summon, Set, Flip, SetST, Activate, Battle, Attack, Material, ChangePos }

    sealed class Action
    {
        public K Kind; public int P; public int Turn; public string Card = ""; public string? Target; public bool Direct; public bool Done;
        public int Zone;
        public override string ToString() => $"T{Turn} P{P} {Kind} {Card}{(Kind == K.Attack ? " -> " + (Direct ? "direct" : Target) : "")}";
    }

    static string Filler = "";

    static string Dir()
    {
        var d = Environment.GetEnvironmentVariable("POC_ROOT") ?? AppContext.BaseDirectory;   // POC_ROOT: when built outside the repo
        while (d != null && !Directory.Exists(Path.Combine(d, "remake", "data", "duel_logs"))) d = Path.GetDirectoryName(d);
        return d == null ? "" : Path.Combine(d, "remake", "data", "duel_logs");
    }

    public static void Run(Action<bool, string> check)
    {
        var dir = Dir();
        if (dir == "") { check(false, "duel_logs folder not found"); return; }
        foreach (var f in Directory.GetFiles(dir, "*.txt").OrderBy(x => x))
        {
            try { Replay(f, check); }
            catch (Exception e) { check(false, $"{Path.GetFileName(f)}: replay crashed: {e.InnerException?.Message ?? e.Message}"); }
        }
    }

    static void Replay(string file, Action<bool, string> check)
    {
        var db = CardDb.Shared;
        var re = new Regex(@"^\s*(\d+)\s+p(\d)\s+op\s+0x([0-9a-f]+)\s+a\s+0x([0-9a-f]+)\s+b\s+0x([0-9a-f]+)\s+c\s+0x([0-9a-f]+)", RegexOptions.IgnoreCase);
        var recs = File.ReadLines(file).Select(l => re.Match(l)).Where(m => m.Success)
            .Select(m => new Rec(int.Parse(m.Groups[1].Value), int.Parse(m.Groups[2].Value), Convert.ToInt32(m.Groups[3].Value, 16),
                Convert.ToInt32(m.Groups[4].Value, 16), Convert.ToInt32(m.Groups[5].Value, 16), Convert.ToInt32(m.Groups[6].Value, 16))).ToList();
        string K2N(int k) => db.ByKonami.TryGetValue(k & 0x7ff, out var l) ? l[0].Name : $"#{k & 0x7ff}";
        string I2N(int i) => db.ByIdx.TryGetValue(i, out var c) ? c.Name : $"idx{i}";

        // ---- parse actions
        var acts = new List<Action>();
        var logDamage = new List<(int p, int amount)>();
        var targets = new List<(string card, int turn, int p, int zone)>(); // 0x08 picks, keyed by the activating card
        string targetCard = "";
        var tributes = new List<(int turn, int p, int zone)>(); // 0x57 tribute zones
        var materials = new Queue<int>();                // 0x5a fusion material zones, in order
        var revived = new List<(int turn, string card)>();  // 0x75: monster special summoned from a Graveyard
        var positions = new Queue<bool>();               // special summon positions (0xe6 c=4: b>>8 == 3 is Defense)
        var fusions = new HashSet<string>();
        int winner = -2, turn = 0, first = recs.FirstOrDefault(r => r.Op == 0x01)?.P ?? 0;
        Action? lastAct = null;
        for (int i = 0; i < recs.Count; i++)
        {
            var r = recs[i];
            switch (r.Op)
            {
                case 0x01: turn++; break;
                case 0xc4:
                    {
                        bool set = (r.B >> 8) == 2;
                        acts.Add(lastAct = new Action { Kind = set ? K.Set : K.Summon, P = r.P, Turn = turn, Card = K2N(r.A) });
                        break;
                    }
                case 0xe6 when r.C == 4: positions.Enqueue((r.B >> 8) == 3); break;
                case 0xe6 when r.C == 3:
                    acts.Add(lastAct = new Action { Kind = K.Flip, P = r.P, Turn = turn, Card = K2N(r.A) });
                    break;
                case 0xc5:
                    // A Spell activated from the hand is logged as Set (0xc5) + activate (0x72).
                    if (i + 1 < recs.Count && recs[i + 1].Op == 0x72 && recs[i + 1].A == r.A) break;
                    acts.Add(lastAct = new Action { Kind = K.SetST, P = r.P, Turn = turn, Card = K2N(r.A) });
                    break;
                case 0x72:
                    targetCard = K2N(r.A);
                    if (i > 0 && recs[i - 1].Op == 0xad) break; // flip effect: automatic in the engine
                    if (db.ByKonami.TryGetValue(r.A & 0x7ff, out var cd) && cd[0].IsMonster && r.B != 0xb) break; // field monster triggers run by themselves; b=0xb = from the hand (Kuriboh)
                    acts.Add(lastAct = new Action { Kind = K.Activate, P = r.P, Turn = turn, Card = K2N(r.A) });
                    break;
                case 0x08: targets.Add((targetCard, turn, r.A, r.B >= 0x100 ? r.B >> 8 : r.B)); break; // monster zone << 8, or 5+ for S/T zones
                case 0x57: tributes.Add((turn, r.P, r.A)); break;
                case 0x75: revived.Add((turn, K2N(r.A))); break;
                case 0x7f when r.C == 1: acts.Add(new Action { Kind = K.ChangePos, P = r.P, Turn = turn, Zone = r.A, Card = $"zone {r.A}" }); break; // manual position change
                case 0x5a: materials.Enqueue(r.B); break;
                case 0xdc: case 0xe9: fusions.Add(K2N(r.A)); break;
                case 0x53: acts.Add(new Action { Kind = K.Battle, P = r.P, Turn = turn }); break;
                case 0x30:
                    {
                        int mine = r.P == 0 ? r.A : r.B, theirs = r.P == 0 ? r.B : r.A;
                        acts.Add(new Action { Kind = K.Attack, P = r.P, Turn = turn, Card = I2N(mine), Target = theirs == 0 ? null : I2N(theirs), Direct = theirs == 0 });
                        break;
                    }
                case 0x43: logDamage.Add((r.P, r.A)); break;
                case 0x04: winner = r.A; break;
            }
        }

        // Fusion materials come from the hand or field; add the recipe's materials as cards the player plays.
        foreach (var r in recs.Where(r => r.Op == 0xdc))
        {
            var res = K2N(r.A);
            var turnOf = recs.Where(x => x.Line <= r.Line).Count(x => x.Op == 0x01);
            var f = db.Fusions.FirstOrDefault(f => db.ByKonami.TryGetValue(f.Result, out var l) && l[0].Name == res);
            if (f != null)
                foreach (var m in new[] { f.A, f.B }.Distinct())
                    acts.Insert(acts.FindIndex(a => a.Turn > turnOf) is var ix and >= 0 ? ix : acts.Count,
                        new Action { Kind = K.Material, P = r.P, Turn = turnOf, Card = K2N(m), Done = true });
        }
        // A vanilla filler that this log never mentions.
        var mentioned = acts.Select(a => a.Card).Concat(acts.Select(a => a.Target ?? "")).ToHashSet();
        Filler = db.ByIdx.Values.First(c => c.IsNormalMonster && c.Level <= 4 && !mentioned.Contains(c.Name) && c.Name.StartsWith("Kuwagata")).Name;

        // ---- stack decks: cards each player plays from hand, in first-use order
        var setup = new DuelSetup { Seed = 5, Shuffle = false, Classic = true, FirstPlayer = first };
        for (int p = 0; p < 2; p++)
        {
            var used = acts.Where(a => a.P == p && a.Kind is K.Summon or K.Set or K.SetST or K.Material || (a.P == p && a.Kind == K.Activate && FromHand(a, acts))).Select(a => a.Card).ToList();
            var deck = used.Where(n => db.ByName.ContainsKey(n) && !db.Named(n).IsFusion).Select(n => db.Named(n).Idx).ToList();
            while (deck.Count < 40) deck.Add(db.Named(Filler).Idx);
            setup.Main[p].AddRange(deck);
            setup.Extra[p].AddRange(acts.Where(a => a.P == p && a.Kind == K.Attack).Select(a => a.Card).Concat(fusions).Distinct()
                .Where(n => db.ByName.ContainsKey(n) && db.Named(n).IsFusion).Select(n => db.Named(n).Idx));
        }

        var d = new Duel(setup);
        bool trace = Environment.GetEnvironmentVariable("LOGTRACE") is { } lt && file.Contains(lt);
        if (trace) d.OnTrace += t => Console.WriteLine($"     T{d.Turn} {t}");
        var engDamage = new List<(int p, int amount)>();
        d.OnEvent += e => { if (e.Kind == EvKind.Damage && e.Arg == 0) engDamage.Add((e.Player, e.Amount)); };
        int skipped = 0, steps = 0;

        // The logs do not record draws: before each answer, swap the cards the log is about to play
        // (this turn and the next) from the Deck into the hand in place of fillers.
        void Conjure()
        {
            foreach (var a in acts.Where(a => (!a.Done || a.Kind == K.Material) && a.Turn <= d.Turn + 1 && a.Turn >= d.Turn && (a.Kind is K.Summon or K.Set or K.SetST or K.Material || a.Kind == K.Activate && FromHand(a, acts))))
            {
                var now = acts.Where(x => !x.Done && x.P == a.P && x.Turn <= d.Turn).Select(x => x.Card).ToHashSet();
                var soon = acts.Where(x => !x.Done && x.P == a.P && x.Turn <= d.Turn + 2).Select(x => x.Card).ToHashSet();
                d.ConjureToHand(a.P, a.Card, c => c.Name == Filler ? 0 : now.Contains(c.Name) ? -1 : soon.Contains(c.Name) ? 2 : 1);
            }
        }

        Action? Next(int p, params K[] kinds) => acts.FirstOrDefault(a => !a.Done && a.Turn == d.Turn && a.P == p && kinds.Contains(a.Kind));
        bool AnyLeft(int p) => acts.Any(a => !a.Done && a.Turn == d.Turn && a.P == p);

        while (!d.Over && steps++ < 5000)
        {
            // Actions of turns the engine has already left are skipped (divergence).
            foreach (var a in acts.Where(a => !a.Done && a.Turn < d.Turn)) { a.Done = true; skipped++; }
            var pr = d.Pending!;
            var o = pr.Options;
            int pick = -1;
            switch (pr.Kind)
            {
                case Ask.Main:
                    {
                        var a = Next(pr.Player, K.Summon, K.Set, K.Flip, K.SetST, K.Activate, K.Battle, K.ChangePos);
                        while (a != null && pick < 0)
                        {
                            pick = a.Kind switch
                            {
                                K.Summon => o.FindIndex(x => x.Act == Act.Summon && x.Card!.Name == a.Card),
                                K.Set => o.FindIndex(x => x.Act == Act.Set && x.Card!.Name == a.Card),
                                K.Flip => o.FindIndex(x => x.Act == Act.FlipSummon && x.Card!.Name == a.Card),
                                K.SetST => o.FindIndex(x => x.Act == Act.SetSpell && x.Card!.Name == a.Card),
                                K.Activate => o.FindIndex(x => x.Act == Act.Activate && x.Card!.Name == a.Card),
                                K.Battle => o.FindIndex(x => x.Act == Act.ToBattle),
                                K.ChangePos => o.FindIndex(x => x.Act == Act.ChangePos && x.Card!.Zone == a.Zone),
                                _ => -1,
                            };
                            a.Done = true;
                            if (pick < 0) { skipped++; check(false, $"{Path.GetFileName(file)}: engine cannot do {a} ({pr})"); a = Next(pr.Player, K.Summon, K.Set, K.Flip, K.SetST, K.Activate, K.Battle, K.ChangePos); }
                        }
                        if (pick < 0) pick = o.FindIndex(x => x.Act == Act.EndTurn);
                        break;
                    }
                case Ask.Battle:
                    {
                        var a = Next(pr.Player, K.Attack, K.Activate);
                        if (a != null)
                        {
                            pick = a.Kind == K.Attack
                                ? o.FindIndex(x => x.Act == Act.Attack && x.Card!.Name == a.Card && (a.Direct ? x.Target == null : x.Target?.Name == a.Target))
                                : o.FindIndex(x => x.Act == Act.Activate && x.Card!.Name == a.Card);
                            a.Done = true;
                            if (pick < 0) { skipped++; check(false, $"{Path.GetFileName(file)}: engine cannot do {a} ({pr})"); }
                        }
                        if (pick < 0) pick = o.FindIndex(x => x.Act == Act.ToMain2);
                        break;
                    }
                case Ask.Respond:
                    {
                        var a = acts.FirstOrDefault(a => !a.Done && a.Turn == d.Turn && a.Kind == K.Activate && a.P == pr.Player && o.Any(x => x.Card?.Name == a.Card));
                        // Only answer with it if it is the next logged thing this player does.
                        var nextOfP = acts.FirstOrDefault(x => !x.Done && x.Turn == d.Turn && x.P == pr.Player);
                        // ...and everything logged before it (by either player) has happened.
                        bool inOrder = a != null && !acts.Take(acts.IndexOf(a)).Any(x => !x.Done && x.Turn == d.Turn && x.Kind != K.Material);
                        if (a != null && a == nextOfP && inOrder) { pick = o.FindIndex(x => x.Card?.Name == a.Card); a.Done = true; }
                        else pick = o.Count - 1;
                        break;
                    }
                case Ask.Pick:
                    {
                        var idx = Enumerable.Range(0, o.Count).ToList();
                        var chosen = new List<int>();
                        if (pr.Text.StartsWith("Discard")) idx = idx.OrderBy(i => o[i].Card!.Name == Filler ? 0 : 1).ToList();
                        else if (revived.FirstOrDefault(v => v.turn == d.Turn && o.Any(x => x.Card!.Loc == Loc.Grave && x.Card.Name == v.card)) is { card: not null } rv)
                        {
                            revived.Remove(rv);
                            chosen.Add(o.FindIndex(x => x.Card!.Loc == Loc.Grave && x.Card.Name == rv.card));
                        }
                        else if (pr.Text.StartsWith("Fusion Material") && materials.Count > 0)
                        {
                            int z = materials.Peek();
                            int k = o.FindIndex(x => x.Card!.Loc == Loc.Monster && x.Card.Zone == z);
                            if (k >= 0) { materials.Dequeue(); chosen.Add(k); }
                        }
                        else if (pr.Text.StartsWith("Tribute"))
                            foreach (var t in tributes.Where(t => t.turn == d.Turn && t.p == pr.Player).ToList())
                            {
                                if (chosen.Count >= pr.Max) break;
                                tributes.Remove(t);
                                int k = o.FindIndex(x => x.Card!.Loc == Loc.Monster && x.Card.Controller == pr.Player && x.Card.Zone == t.zone);
                                if (k >= 0 && !chosen.Contains(k)) chosen.Add(k);
                            }
                        else
                            foreach (var t in targets.Where(t => t.card == pr.Source?.Name && t.turn == d.Turn).ToList())
                            {
                                if (chosen.Count >= pr.Max) break;
                                targets.Remove(t);
                                int k = o.FindIndex(x => x.Card!.OnField && x.Card.Controller == t.p && (x.Card.Loc == Loc.Monster ? x.Card.Zone == t.zone : x.Card.Zone + 5 == t.zone));
                                if (k >= 0 && !chosen.Contains(k)) chosen.Add(k);
                            }
                        foreach (var i in idx) if (chosen.Count < pr.Min && !chosen.Contains(i)) chosen.Add(i);
                        Conjure();
                        d.Submit(new Command(pr.Player, chosen.ToArray()));
                        continue;
                    }
                case Ask.YesNo: pick = 0; break;
                case Ask.Position: pick = positions.Count > 0 && positions.Dequeue() ? 1 : 0; break;
                default: pick = 0; break;
            }
            Conjure();
            if (trace) Console.WriteLine($"   > T{d.Turn} {pr.Kind} P{pr.Player}: {o[pick]}");
            d.Submit(new Command(pr.Player, new[] { pick }));
        }

        var name = Path.GetFileName(file);
        int same = 0;
        while (same < Math.Min(logDamage.Count, engDamage.Count) && logDamage[same] == engDamage[same]) same++;
        bool all = same == logDamage.Count && engDamage.Count == logDamage.Count;
        Console.WriteLine($"Log {name}: {acts.Count} actions ({skipped} not reproducible), LP losses {same}/{logDamage.Count} identical in order"
            + (all ? "" : $" (first difference: log {Fmt(logDamage, same)} vs engine {Fmt(engDamage, same)})")
            + $", winner log P{winner} engine {(d.Over ? "P" + d.Winner : "none")}");
        if (!all && Environment.GetEnvironmentVariable("LOGDIFF") == "1")
        {
            Console.WriteLine("   log:    " + string.Join(" ", logDamage.Select(x => $"P{x.p}-{x.amount}")));
            Console.WriteLine("   engine: " + string.Join(" ", engDamage.Select(x => $"P{x.p}-{x.amount}")));
        }
        check(all, $"{name}: LP-loss sequence matches the original");
        if (winner >= 0) check(d.Over && d.Winner == winner, $"{name}: same winner");
    }

    static string Fmt(List<(int p, int amount)> l, int i) => i < l.Count ? $"P{l[i].p} -{l[i].amount}" : "end";

    // A logged activation came from the hand unless the same card was Set earlier by that player.
    static bool FromHand(Action a, List<Action> all)
    {
        int idx = all.IndexOf(a);
        int sets = all.Take(idx).Count(x => x.P == a.P && x.Kind == K.SetST && x.Card == a.Card);
        int prior = all.Take(idx).Count(x => x.P == a.P && x.Kind == K.Activate && x.Card == a.Card && !FromHandShallow(x, all));
        return sets <= prior;
    }

    static bool FromHandShallow(Action a, List<Action> all)
    {
        int idx = all.IndexOf(a);
        return !all.Take(idx).Any(x => x.P == a.P && x.Kind == K.SetST && x.Card == a.Card);
    }
}
