using System.Diagnostics;
using PoC.Engine;

namespace PoC.Check;

/// Brain-vs-brain league: random CPU-deck pairs, each played twice with seats swapped, so deck and seat luck
/// cancel. Run: dotnet run --project Engine.Check -c Release -- league Plus Classic 500
static class League
{
    public static int Worlds = int.Parse(Environment.GetEnvironmentVariable("SEER_WORLDS") ?? "6");
    public static void Run(Brain a, Brain b, int pairs) => _ =
        Run($"{a}", $"{b}", (seat, seed, x) => { var ai = new Ai(seat, seed, x ? b : a); ai.TimeBudgetMs = int.MaxValue; ai.Worlds = Worlds; return ai; }, pairs);

    /// CPU vs CPU by spec: a difficulty level ("3" = Ai.ForLevel, real time budgets) or a bare brain ("Classic",
    /// "Plus", "Seer"; no judge); a trailing "o" ("3o") = that CPU with the old fixed board score. LEAGUE_CHAOS=1: random effect-card decks (the whole card pool) instead of the
    /// CPU decks. Run: ... -- levels 3 Classic 200
    public static void Levels(string a, string b, int pairs)
    {
        if (Environment.GetEnvironmentVariable("JUDGE_LOG") == "1") Ai.Overruled = m => { lock (Console.Out) Console.WriteLine("  " + m); };
        _ = Run(a, b, (seat, seed, x) => Make(x ? b : a, seat, seed), pairs);
    }

    static readonly bool Chaos = Environment.GetEnvironmentVariable("LEAGUE_CHAOS") == "1";

    /// make(seat, seed, isB) builds one side's CPU.
    /// Returns a's win rate in percent.
    static double Run(string a, string b, Func<int, ulong, bool, Ai> make, int pairs, int? first = null)
    {
        var db = CardDb.Shared;
        Scripts.Get(db.Named("Pot of Greed"));   // build shared tables before going parallel
        new Ai(0, 0);
        var decks = db.DeckNames().ToList();
        int aWins = 0, bWins = 0, draws = 0, errors = 0;
        var thinks = new List<double>();
        var sw = Stopwatch.StartNew();
        int from = first ?? int.Parse(Environment.GetEnvironmentVariable("LEAGUE_FROM") ?? "0");
        Parallel.For(from, from + pairs, i =>
        {
            var rng = new Rng((ulong)(31000 + i));
            var setup = Chaos ? Bots.RandomSetup(rng, chaos: true) : new DuelSetup { Seed = rng.Next(), FirstPlayer = rng.Int(2) };
            if (Chaos) setup.Classic = true;
            else
                for (int p = 0; p < 2; p++)
                {
                    var (main, extra) = db.Deck(decks[rng.Int(decks.Count)]);
                    setup.Main[p].AddRange(main); setup.Extra[p].AddRange(extra);
                }
            foreach (bool swap in new[] { false, true })
            {
                var ais = new[] { make(0, (ulong)i * 2 + 1, swap), make(1, (ulong)i * 2 + 2, !swap) };
                int aSeat = swap ? 1 : 0;
                try
                {
                    var d = new Duel(setup);
                    int steps = 0;
                    while (!d.Over && steps++ < 4000)
                    {
                        var ai = ais[d.Pending!.Player];
                        long t0 = Stopwatch.GetTimestamp();
                        var cmd = ai.Decide(d);
                        if (ai.Brain == Brain.Seer || ai.JudgeWorlds > 0)
                        {
                            double ms = Stopwatch.GetElapsedTime(t0).TotalMilliseconds;
                            lock (thinks) { thinks.Add(ms); }
                            if (ms > 15000) Console.WriteLine($"slow {ms:0} ms: pair {i} swap {swap} log {d.Log.Count} {d.Pending}");
                        }
                        d.Submit(cmd);
                    }
                    if (!d.Over || d.Winner < 0) Interlocked.Increment(ref draws);
                    else if (d.Winner == aSeat) Interlocked.Increment(ref aWins);
                    else Interlocked.Increment(ref bWins);
                }
                catch (Exception e)
                {
                    if (Interlocked.Increment(ref errors) <= 3) Console.WriteLine($"error pair {i}: {e.InnerException?.Message ?? e.Message}");
                }
            }
        });
        int n = aWins + bWins;
        double rate = n == 0 ? 0 : 100.0 * aWins / n, margin = n == 0 ? 0 : 196 * Math.Sqrt(rate / 100 * (1 - rate / 100) / n);
        Console.WriteLine($"League {a} vs {b}: {a} won {aWins}, {b} won {bWins}, draws {draws}, errors {errors} " +
            $"-> {a} {rate:0.0}% ±{margin:0.0} ({sw.Elapsed.TotalSeconds:0}s)");
        if (thinks.Count > 0)
        {
            thinks.Sort();
            Console.WriteLine($"Seer/Judge decisions {thinks.Count}: median {thinks[thinks.Count / 2]:0} ms, 95% {thinks[thinks.Count * 95 / 100]:0} ms, max {thinks[^1]:0} ms (all cores busy)");
        }
        return rate;
    }

    /// Every CPU deck's strength: win rate against random other CPU decks (Classic brain on both sides, both seats),
    /// next to the difficulty level the original games give it. Run: ... -- decks 120
    public static void Decks(int perDeck, Brain brain = Brain.Classic, string prefix = "")
    {
        var db = CardDb.Shared;
        Scripts.Get(db.Named("Pot of Greed"));
        new Ai(0, 0);
        var all = db.DeckNames().ToList();
        // prefix: "remastered" = the hand-built decks, "a,b,c" = those decks, else decks whose key starts with it.
        var keys = prefix == "remastered" ? db.RemasteredDeckNames().ToList()
            : prefix.Contains(',') ? prefix.Split(',').ToList() : all.Where(k => k.StartsWith(prefix)).ToList();
        var cache = all.Union(keys).ToDictionary(k => k, k => db.Deck(k));
        var wins = new int[keys.Count]; var games = new int[keys.Count];
        Parallel.For(0, keys.Count * perDeck, job =>
        {
            int k = job / perDeck, i = job % perDeck;
            var rng = new Rng((ulong)(job * 7919 + 11));
            string other;
            do other = all[rng.Int(all.Count)]; while (other == keys[k]);
            int seat = i % 2;
            var setup = new DuelSetup { Seed = rng.Next(), FirstPlayer = rng.Int(2) };
            setup.Main[seat].AddRange(cache[keys[k]].Main); setup.Extra[seat].AddRange(cache[keys[k]].Extra);
            setup.Main[1 - seat].AddRange(cache[other].Main); setup.Extra[1 - seat].AddRange(cache[other].Extra);
            try
            {
                var d = new Duel(setup);
                // The rated deck gets `brain`; with Seer the opponents get Classic+ (Seer on both sides is too slow).
                var foe = brain == Brain.Seer ? Brain.Plus : brain;
                var ais = new[] { new Ai(0, rng.Next(), seat == 0 ? brain : foe), new Ai(1, rng.Next(), seat == 1 ? brain : foe) };
                foreach (var ai in ais) { ai.TimeBudgetMs = 400; ai.Worlds = Worlds; }   // capped like the game, so stall duels finish
                int steps = 0;
                while (!d.Over && steps++ < 1500) d.Submit(ais[d.Pending!.Player].Decide(d));
                if (d.Winner < 0) return;
                Interlocked.Increment(ref games[k]);
                if (d.Winner == seat) Interlocked.Increment(ref wins[k]);
            }
            catch (Exception) { }
        });
        foreach (var k in Enumerable.Range(0, keys.Count).OrderByDescending(k => (double)wins[k] / Math.Max(1, games[k])))
            Console.WriteLine($"{keys[k],-22} {100.0 * wins[k] / Math.Max(1, games[k]),5:0.0}%  ({games[k]} duels)  level {OriginalLevel(keys[k])}");
    }

    /// The level the original opponent choice gives a deck (Progression), "-" for decks it never picks.
    static string OriginalLevel(string key)
    {
        var name = key[(key.IndexOf('/') + 1)..];
        if (key.StartsWith("yugi/")) return name switch { "cpujj" => "1", "cpuj" => "2", "cpuk" => "5", "cpukk" => "6", _ => "4" };
        if (key.StartsWith("kaiba/"))
            return name switch { "kaiba_j" => "1", "kaiba_jj" => "2", "kaiba_k" or "yk_k" => "5", "kaiba_kk" or "yk_kk" => "6", "kaiba_kkk" or "yk_kkk" => "7", "deck_k" => "-", _ => "4" };
        if (name.StartsWith("cpu_"))
        {
            int n = int.Parse(name[4..]);
            // the decoded original table (Progression now uses a re-ordered one)
            var lv = new[] { new[] { 0, 1, 2, 3, 4 }, new[] { 3, 4, 10, 11, 12 }, new[] { 10, 11, 12, 13, 14 }, new[] { 13, 14, 15, 20, 21 },
                new[] { 20, 21, 22, 23, 24 }, new[] { 23, 24, 25, 26, 27 }, new[] { 25, 26, 27, 28, 29 } };
            return string.Join("/", Enumerable.Range(1, 7).Where(l => lv[l - 1].Contains(n)));
        }
        return "-";
    }

    /// CPU side-decking on vs off: the same best-of-3 matches (Classic+ both sides), CPU deck from those with a side
    /// list. Reports the CPU's win rate in duels 2-3 and in matches. Run: ... -- sidetest 600
    public static void SideTest(int matches)
    {
        var db = CardDb.Shared;
        Scripts.Get(db.Named("Pot of Greed"));
        new Ai(0, 0);
        var all = db.DeckNames().ToList();
        var sided = all.Concat(db.RemasteredDeckNames()).Where(k => db.DeckSide(k).Count > 0).ToList();
        var cache = all.Union(sided).ToDictionary(k => k, k => (deck: db.Deck(k), side: db.DeckSide(k)));
        var later = new int[2]; var laterN = new int[2]; var won = new int[2]; int swaps = 0;
        Parallel.For(0, matches, i =>
        {
            var rng = new Rng((ulong)(55000 + i));
            string cpuKey = sided[rng.Int(sided.Count)], youKey = all[rng.Int(all.Count)];
            ulong seed = rng.Next();
            for (int on = 0; on < 2; on++)
            {
                var r = new Rng(seed);
                var cpu = new Deck("cpu", cache[cpuKey].deck.Main, cache[cpuKey].deck.Extra);
                cpu.Side.AddRange(cache[cpuKey].side);
                var seen = new CpuSide.Seen();
                int cw = 0, yw = 0, first = r.Int(2);
                for (int g = 0; g < 3 && cw < 2 && yw < 2; g++)
                {
                    var setup = new DuelSetup { Seed = r.Next(), FirstPlayer = first };
                    setup.Main[0].AddRange(cache[youKey].deck.Main); setup.Extra[0].AddRange(cache[youKey].deck.Extra);
                    setup.Main[1].AddRange(cpu.Main); setup.Extra[1].AddRange(cpu.Extra);
                    int winner = -1;
                    try
                    {
                        var d = new Duel(setup);
                        var ais = new[] { new Ai(0, r.Next(), Brain.Plus), new Ai(1, r.Next(), Brain.Plus) };
                        int steps = 0;
                        while (!d.Over && steps++ < 4000) d.Submit(ais[d.Pending!.Player].Decide(d));
                        winner = d.Winner;
                        CpuSide.Observe(seen, d, 0);
                    }
                    catch (Exception) { }
                    if (g > 0 && winner >= 0) { Interlocked.Increment(ref laterN[on]); if (winner == 1) Interlocked.Increment(ref later[on]); }
                    if (winner == 1) cw++; else if (winner == 0) yw++;
                    if (winner >= 0) first = 1 - winner;   // loser goes first
                    if (on == 1) Interlocked.Add(ref swaps, CpuSide.Swap(cpu, seen).Count);
                }
                if (cw >= 2) Interlocked.Increment(ref won[on]);
            }
        });
        for (int on = 0; on < 2; on++)
            Console.WriteLine($"CPU siding {(on == 1 ? "on " : "off")}: duels 2-3 won {100.0 * later[on] / Math.Max(1, laterN[on]):0.0}% of {laterN[on]}, matches won {100.0 * won[on] / matches:0.0}%");
        Console.WriteLine($"swaps made: {swaps}");
    }

    /// How hard each of Joey's ladder decks is for a new player: CPU win rate against the two starter decks
    /// (you = Classic+, a stand-in for a human; Joey = the original brain; 2004 list applied). Run: ... -- ladder 300
    public static void Ladder(int perDeck)
    {
        var db = CardDb.Shared;
        Scripts.Get(db.Named("Pot of Greed"));
        new Ai(0, 0);
        var keys = db.DeckNames().Where(k => k.StartsWith("joey/cpu_")).ToList();
        var starters = DeckRules.Starters.Select(s => db.Deck(s.Key)).ToList();
        var wins = new int[keys.Count]; var games = new int[keys.Count];
        Parallel.For(0, keys.Count * perDeck, job =>
        {
            int k = job / perDeck, i = job % perDeck;
            var rng = new Rng((ulong)(job * 104729 + 3));
            var you = starters[i % 2];
            var (m, x) = db.Deck(keys[k]);
            var setup = new DuelSetup { Seed = rng.Next(), FirstPlayer = (i / 2) % 2 };
            setup.Main[0].AddRange(you.Main); setup.Extra[0].AddRange(you.Extra);
            setup.Main[1].AddRange(DeckRules.ForCpu(m, "joey")); setup.Extra[1].AddRange(x);
            try
            {
                var d = new Duel(setup);
                var ais = new[] { new Ai(0, rng.Next(), Brain.Plus), new Ai(1, rng.Next()) };
                int steps = 0;
                while (!d.Over && steps++ < 4000) d.Submit(ais[d.Pending!.Player].Decide(d));
                if (d.Winner < 0) return;
                Interlocked.Increment(ref games[k]);
                if (d.Winner == 1) Interlocked.Increment(ref wins[k]);
            }
            catch (Exception) { }
        });
        foreach (var k in Enumerable.Range(0, keys.Count).OrderBy(k => (double)wins[k] / Math.Max(1, games[k])))
            Console.WriteLine($"{keys[k],-16} beats a starter deck {100.0 * wins[k] / Math.Max(1, games[k]),5:0.0}%  ({games[k]})  original level {OriginalLevel(keys[k])}");
    }

    /// Rival deck ladder (remastered_decks.json, rival/x_lvN): each deck's win rate against the three starter decks,
    /// the same CPU on both sides (spec as in `levels`, default Classic+) so only the decks differ. Each rival's column
    /// should climb with the level. Run: ... -- rivals 200 [spec]
    public static void Rivals(int perDeck, string spec = "Plus")
    {
        var db = CardDb.Shared;
        Scripts.Get(db.Named("Pot of Greed"));
        new Ai(0, 0);
        var keys = db.RemasteredDeckNames().Where(k => Environment.GetEnvironmentVariable("RIVALS") is not { } f || k.Contains(f)).ToList();
        var use = Environment.GetEnvironmentVariable("RIVALS_USE") == "1" ? new Dictionary<string, int>() : null;   // what the rival's deck actually did
        var starters = DeckRules.Starters.Select(s => db.Deck(s.Key)).ToList();
        var wins = new int[keys.Count]; var games = new int[keys.Count];
        Parallel.For(0, keys.Count * perDeck, job =>
        {
            int k = job / perDeck, i = job % perDeck;
            var rng = new Rng((ulong)(job * 104729 + 11));
            var you = starters[i % starters.Count];
            var (m, x) = db.Deck(keys[k]);
            var setup = new DuelSetup { Seed = rng.Next(), FirstPlayer = (i / 3) % 2 };
            setup.Main[0].AddRange(you.Main); setup.Extra[0].AddRange(you.Extra);
            setup.Main[1].AddRange(m); setup.Extra[1].AddRange(x);
            try
            {
                var d = new Duel(setup);
                if (use != null)
                    d.OnEvent += e =>
                    {
                        string? key = e.Kind switch { EvKind.Summoned when e.Card!.Controller == 1 => $"summon {(SummonKind)e.Arg} {e.Card.Name}", EvKind.Activated when e.Link!.Player == 1 => "use " + e.Link.Card.Name, _ => null };
                        if (key != null) lock (use) use[key] = use.GetValueOrDefault(key) + 1;
                    };
                var ais = new[] { Make(spec, 0, rng.Next()), Make(spec, 1, rng.Next()) };
                for (int steps = 0; !d.Over && steps < 4000; steps++) d.Submit(ais[d.Pending!.Player].Decide(d));
                if (use != null) lock (use) { use["turns"] = use.GetValueOrDefault("turns") + d.Turn; use[$"won{d.Winner}"] = use.GetValueOrDefault($"won{d.Winner}") + 1; }
                if (d.Winner < 0) return;
                Interlocked.Increment(ref games[k]);
                if (d.Winner == 1) Interlocked.Increment(ref wins[k]);
            }
            catch (Exception) { }
        });
        Console.WriteLine("level   yugi   kaiba  joey   jaden  (% wins vs the starter decks)");
        for (int l = 1; l <= 7; l++)
            Console.WriteLine($"  {l}  " + string.Join("", new[] { "yugi", "kaiba", "joey", "jaden" }.Select(r => { int k = keys.IndexOf($"{r}/x_lv{l}"); return k < 0 ? "    -  " : $"{100.0 * wins[k] / Math.Max(1, games[k]),6:0.0} "; })));
        if (use != null) foreach (var (k, v) in use.OrderByDescending(kv => kv.Value)) Console.WriteLine($"    {v,5} {k}");
        foreach (var k in keys.Where(k => !k.Contains("/x_lv"))) Console.WriteLine($"  {k}: {100.0 * wins[keys.IndexOf(k)] / Math.Max(1, games[keys.IndexOf(k)]):0.0}%");
    }

    /// Train the board value (Value.cs) on CPU-vs-CPU duels: half CPU decks, half whole-pool decks. 10% of duels are
    /// held out to report how often the model calls the eventual winner. Writes Engine/ai_value.json.
    /// Run: ... -- train 3000 [spec]   (spec as in `levels`: who plays the training duels)
    public static Value Train(int duels, string spec = "Plus", bool write = true, int seedBase = 77000)
    {
        var db = CardDb.Shared;
        Scripts.Get(db.Named("Pot of Greed"));
        new Ai(0, 0);
        _ = Value.Trained;
        // The original CPU decks plus the remake's (rival ladders, Jaden, Exodia, tournament duelists: the expansion cards).
        var decks = db.DeckNames().Concat(db.RemasteredDeckNames()).ToList();
        var fit = new List<(double[] f, double y)>[duels];
        var sw = Stopwatch.StartNew();
        // Card payoffs carry over at half weight, so rare cards keep what earlier runs saw.
        var uses = new Value(new double[Value.Names.Length], Value.Trained.Uses.ToDictionary(kv => kv.Key, kv => new[] { kv.Value[0] / 2, kv.Value[1] / 2 }));
        Ai.Used = uses.Used;
        Parallel.For(0, duels, i =>
        {
            var rng = new Rng((ulong)(seedBase + i));
            var setup = i % 2 == 1 ? Bots.RandomSetup(rng, chaos: true) : new DuelSetup { Seed = rng.Next(), FirstPlayer = rng.Int(2) };
            setup.Classic = true;
            if (i % 2 == 0)
                for (int p = 0; p < 2; p++) { var (m, x) = db.Deck(decks[rng.Int(decks.Count)]); setup.Main[p].AddRange(m); setup.Extra[p].AddRange(x); }
            fit[i] = new();
            try
            {
                var d = new Duel(setup);
                var ais = new[] { Make(spec, 0, rng.Next()), Make(spec, 1, rng.Next()) };
                for (int k = 0; k < 4000 && !d.Over; k++) d.Submit(ais[d.Pending!.Player].Decide(d));
                fit[i] = Value.Positions(d);
            }
            catch (Exception) { }
        });
        var train = fit.Where((_, i) => i % 10 != 0).SelectMany(x => x).ToList();
        var test = fit.Where((_, i) => i % 10 == 0).SelectMany(x => x).ToList();
        Ai.Used = null;
        var v = new Value(new double[Value.Names.Length], uses.Uses).Fit(train, stay: 1, steps: 15);
        double Acc(Value m) => test.Count(t => (t.f.Select((f, k) => f * m.W[k]).Sum() > 0) == (t.y > 0.5)) * 100.0 / Math.Max(1, test.Count);
        Console.WriteLine($"Trained on {train.Count} positions ({sw.Elapsed.TotalSeconds:0}s). Held-out winner called: learned {Acc(v):0.0}%, shipped {Acc(Value.Trained):0.0}%");
        for (int k = 0; k < Value.Names.Length; k++) Console.WriteLine($"  {Value.Names[k],-9} {v.W[k],8:0.000}");
        Console.WriteLine($"Card payoffs recorded for {v.Uses.Count} Spells/Traps; most valuable to hold:");
        foreach (var (k, u) in v.Uses.Where(kv => kv.Value[1] >= 5).OrderByDescending(kv => kv.Value[0] / kv.Value[1]).Take(12))
            Console.WriteLine($"  {k,-28} avg {u[0] / u[1],6:0}  ({u[1]} uses)");
        if (!write) return v;
        File.WriteAllText(Path.Combine("Engine", "ai_value.json"), v.ToJson());
        Console.WriteLine("wrote Engine/ai_value.json (rebuild to ship it)");
        return v;
    }

    /// Overnight training: rounds of `Train` (fresh duels each round, played by the current best model) until `hours`
    /// pass. A round's model replaces the best only if it wins a judge-2-worlds head-to-head (150 pairs, >= 51%); accepted
    /// models go to Engine/ai_value.json. Every round is logged to Engine/train_log.txt. Run: ... -- trainloop 6
    public static void TrainLoop(double hours, int duels = 1200)
    {
        string log = Path.Combine("Engine", "train_log.txt");
        void Log(string s) { Console.WriteLine(s); File.AppendAllText(log, $"{DateTime.Now:yyyy-MM-dd HH:mm} {s}{Environment.NewLine}"); }
        var end = DateTime.Now.AddHours(hours);
        if (OperatingSystem.IsWindows()) SetThreadExecutionState(0x80000001);   // ES_CONTINUOUS | ES_SYSTEM_REQUIRED: no idle sleep while training
        Log($"training loop start: {hours} h, {duels} duels a round");
        for (int round = 1; DateTime.Now < end; round++)
        {
            var sw = Stopwatch.StartNew();
            var cand = Train(duels, "J1", write: false, seedBase: 1_000_000 * round);
            var best = Value.Trained;
            double rate = Run($"round {round}", "best", (seat, seed, isB) => { var ai = Make("J2", seat, seed); ai.Board = isB ? best : cand; return ai; }, 150, 500_000 + 1000 * round);
            bool keep = rate >= 51;
            if (keep) { Value.Trained = cand; File.WriteAllText(Path.Combine("Engine", "ai_value.json"), cand.ToJson()); }
            var top = cand.Uses.Where(kv => kv.Value[1] >= 5).OrderByDescending(kv => kv.Value[0] / kv.Value[1]).Take(5).Select(kv => kv.Key);
            Log($"round {round}: new model vs best {rate:0.0}% -> {(keep ? "KEPT" : "dropped")} ({sw.Elapsed.TotalMinutes:0} min); weights {string.Join(" ", cand.W.Select(w => w.ToString("0.000")))}; top holds: {string.Join(", ", top)}");
        }
        Log("training loop done");
    }

    /// The shipped board value (embedded ai_value.json) against another model file, same judging CPU on both sides.
    /// Run: ... -- valuevs other.json 300
    public static void ValueVs(string file, int pairs)
    {
        var other = Value.FromJson(File.ReadAllText(file));
        var shipped = Value.Trained;
        _ = Run("shipped", Path.GetFileName(file), (seat, seed, isB) => { var ai = Make("J2", seat, seed); ai.Board = isB ? other : shipped; return ai; }, pairs, 900_000);
    }

    [System.Runtime.InteropServices.DllImport("kernel32.dll")] static extern uint SetThreadExecutionState(uint flags);

    static Ai Make(string spec, int seat, ulong seed)
    {
        bool old = spec.EndsWith("o");
        var s = old ? spec[..^1] : spec;
        // "J2" = Classic+ with the judge on 2 worlds (cheap: training and deck tests); a number = Ai.ForLevel.
        var ai = s.StartsWith("J") ? new Ai(seat, seed, Brain.Plus) { JudgeWorlds = int.Parse(s[1..]) }
            : int.TryParse(s, out int lv) ? Ai.ForLevel(seat, seed, lv) : new Ai(seat, seed, Enum.Parse<Brain>(s));
        if (old) ai.Board = null;
        return ai;
    }

    public static int Level = int.Parse(Environment.GetEnvironmentVariable("SEER_LEVEL") ?? "7");

    /// Real-game thinking time of the CPU at a level (one duel at a time, default time budget).
    public static void SeerBench()
    {
        var db = CardDb.Shared;
        var decks = db.DeckNames().ToList();
        var rng = new Rng(99);
        var rows = new List<(double ms, int opts, int log, Ask kind)>();
        for (int g = 0; g < 25; g++)
        {
            var setup = new DuelSetup { Seed = rng.Next(), FirstPlayer = rng.Int(2) };
            for (int p = 0; p < 2; p++) { var (m, x) = db.Deck(decks[rng.Int(decks.Count)]); setup.Main[p].AddRange(m); setup.Extra[p].AddRange(x); }
            var d = new Duel(setup);
            var ais = new[] { new Ai(0, 1), Ai.ForLevel(1, 2, Level) };
            int steps = 0;
            while (!d.Over && steps++ < 4000)
            {
                var pr = d.Pending!;
                long t0 = Stopwatch.GetTimestamp();
                var cmd = ais[pr.Player].Decide(d);
                if (pr.Player == 1 && pr.Kind is Ask.Main or Ask.Battle or Ask.Respond && pr.Options.Count > 1)
                    rows.Add((Stopwatch.GetElapsedTime(t0).TotalMilliseconds, pr.Options.Count, d.Log.Count, pr.Kind));
                d.Submit(cmd);
            }
        }
        rows.Sort((a, b) => a.ms.CompareTo(b.ms));
        Console.WriteLine($"searched {rows.Count}: median {rows[rows.Count / 2].ms:0} ms, 90% {rows[rows.Count * 9 / 10].ms:0}, max {rows[^1].ms:0}");
        foreach (var r in rows.TakeLast(6)) Console.WriteLine($"  {r.ms:0} ms  {r.kind} {r.opts} options, log {r.log}");
    }

    /// Is the game's CPU passive? It plays `duels` duels against the Classic rules and every turn it could attack an
    /// empty field is counted, with how many of those it let go by (playtest 2026-11-09: "never goes to battle").
    /// Run: ... -- passive 40 [brain.json]   (a brain file = the weights from a save, to compare with the shipped ones)
    public static void Passive(int duels, string? brainFile)
    {
        var db = CardDb.Shared;
        Scripts.Get(db.Named("Pot of Greed"));
        new Ai(0, 0);
        var board = brainFile != null ? Value.FromJson(File.ReadAllText(brainFile)) : Value.Trained;
        var decks = db.DeckNames().ToList();
        int open = 0, missed = 0, tributeSets = 0, wins = 0, turns = 0, unfinished = 0;
        // playtest 2026-11-12 habits: walls flipped to Attack, a winning attack not made, an attacker hidden in Defense,
        // a turn ended with a monster / Trap still playable
        int wallFlips = 0, beats = 0, beatsMissed = 0, hides = 0, idleMon = 0, idleTrap = 0, myTurns = 0;
        // PASSIVE_TRACE=1: one duel at a time, and each turn that let a free attack go is printed with what the CPU saw.
        bool trace = Environment.GetEnvironmentVariable("PASSIVE_TRACE") != null;
        var lines = new List<string>();
        if (trace) Ai.Overruled = l => lines.Add("      " + l);
        Parallel.For(0, duels, new ParallelOptions { MaxDegreeOfParallelism = trace ? 1 : -1 }, g =>
        {
            var rng = new Rng((ulong)(4200 + g));
            var setup = new DuelSetup { Seed = rng.Next(), FirstPlayer = rng.Int(2) };
            for (int p = 0; p < 2; p++) { var (m, x) = db.Deck(decks[rng.Int(decks.Count)]); setup.Main[p].AddRange(m); setup.Extra[p].AddRange(x); }
            var d = new Duel(setup);
            var cpu = Ai.ForLevel(1, rng.Next(), Level);
            cpu.Board = board; cpu.TimeBudgetMs = cpu.JudgeBudgetMs = int.MaxValue;
            var ais = new[] { new Ai(0, rng.Next()), cpu };
            int turn = -1; bool couldAttack = false, attacked = false, couldBeat = false;
            void Close()
            {
                if (couldAttack) { Interlocked.Increment(ref open); if (!attacked) { Interlocked.Increment(ref missed); if (trace) { Console.WriteLine($"--- duel {g} turn {turn}: free attack not taken"); foreach (var l in lines) Console.WriteLine(l); } } }
                if (couldBeat) { Interlocked.Increment(ref beats); if (!attacked) { Interlocked.Increment(ref beatsMissed); if (trace) { Console.WriteLine($"--- duel {g} turn {turn}: winning attack not made"); foreach (var l in lines) Console.WriteLine(l); } } }
                couldAttack = attacked = couldBeat = false; lines.Clear();
            }
            int steps = 0;
            while (!d.Over && steps++ < 4000)
            {
                var pr = d.Pending!;
                if (d.Turn != turn) { Close(); turn = d.Turn; }
                var cmd = ais[pr.Player].Decide(d);
                if (pr.Player == 1 && d.TurnPlayer == 1 && cmd.Picks.Length > 0)
                {
                    var o = pr.Options[cmd.Picks[0]];
                    if (trace) lines.Add($"  [{d.Phase}] {pr.Kind} LP {d.P(1).Lp}/{d.P(0).Lp} mine [{string.Join(", ", d.P(1).MonstersOnField.Select(m => $"{m.Name} {d.Atk(m)}/{d.Def(m)}{(m.Defense ? " DEF" : "")}{(m.FaceUp ? "" : " down")}"))}] sets {d.P(1).SpellsOnField.Count()} | theirs: sets {d.P(0).SpellsOnField.Count()} hand {d.P(0).Hand.Count} -> {o}   of [{string.Join(" | ", pr.Options)}]");
                    if (pr.Kind == Ask.Main && d.Phase == Phase.Main1 && pr.Options.Any(x => x.Act == Act.ToBattle) && !d.P(0).MonstersOnField.Any()
                        && d.P(1).MonstersOnField.Any(m => !m.Defense && d.CanAttack(m) && d.Atk(m) > 0)) couldAttack = true;
                    if (o.Act == Act.Attack) attacked = true;
                    bool Beats(Card m) => d.P(0).MonstersOnField.Any(t => t.FaceUp && d.Atk(m) > (t.Defense ? d.Def(t) : d.Atk(t)));
                    if (pr.Kind == Ask.Main && d.Phase == Phase.Main1 && pr.Options.Any(x => x.Act == Act.ToBattle)
                        && d.P(1).MonstersOnField.Any(m => m.FaceUp && !m.Defense && d.CanAttack(m) && Beats(m))) couldBeat = true;
                    if (pr.Kind == Ask.Main && o.Act == Act.ChangePos && o.Card is { FaceUp: true } pc)
                    {
                        if (pc.Defense && d.Def(pc) > d.Atk(pc) && d.Atk(pc) < 1500 && !Beats(pc) && d.P(0).MonstersOnField.Any()) { Interlocked.Increment(ref wallFlips); if (trace) Console.WriteLine($"--- duel {g} turn {turn}: wall flipped to Attack: {pc.Name} {d.Atk(pc)}/{d.Def(pc)}"); }
                        if (!pc.Defense && d.Phase == Phase.Main1 && d.CanAttack(pc) && Beats(pc)) { Interlocked.Increment(ref hides); if (trace) Console.WriteLine($"--- duel {g} turn {turn}: attacker hidden in Defense: {pc.Name} {d.Atk(pc)}"); }
                    }
                    if (pr.Kind == Ask.Main && o.Act == Act.EndTurn)
                    {
                        Interlocked.Increment(ref myTurns);
                        var me1 = d.P(1);
                        if (pr.Options.Any(x => x.Act is Act.Summon or Act.Set && x.Card!.Data.Level <= 4 && !Duel.Exodia.Contains(x.Card.Name))) { Interlocked.Increment(ref idleMon); if (trace) { Console.WriteLine($"--- duel {g} turn {turn}: ended with a monster to play"); foreach (var l in lines) Console.WriteLine(l); } }
                        if (pr.Options.Any(x => x.Act == Act.SetSpell && x.Card!.Data.IsTrap)) Interlocked.Increment(ref idleTrap);
                    }
                    if (pr.Kind == Ask.Main && o.Act == Act.Set && o.Card!.Data.Level >= 5) Interlocked.Increment(ref tributeSets);
                }
                d.Submit(cmd);
            }
            Close();
            Interlocked.Add(ref turns, d.Turn);
            if (!d.Over) Interlocked.Increment(ref unfinished); else if (d.Winner == 1) Interlocked.Increment(ref wins);
        });
        Console.WriteLine($"passive ({(brainFile == null ? "shipped" : Path.GetFileName(brainFile))}): {duels} duels, CPU won {wins}, unfinished {unfinished}, {turns / (double)duels:0.0} turns a duel; " +
            $"free attacks on an empty field {open}, not taken {missed}; tribute Sets {tributeSets}");
        Console.WriteLine($"habits: CPU turns {myTurns}; walls flipped to Attack {wallFlips}; turns with a winning attack {beats}, none made {beatsMissed}; attackers hidden in Defense {hides}; " +
            $"turns ended with a monster to play {idleMon}, with a Trap to set {idleTrap}");
    }

    /// How long rebuilding a duel from its log takes (the cost of one look-ahead branch).
    public static void Bench()
    {
        var db = CardDb.Shared;
        var decks = db.DeckNames().ToList();
        var rng = new Rng(77);
        var times = new List<(int cmds, double ms)>();
        for (int i = 0; i < 20; i++)
        {
            var setup = new DuelSetup { Seed = rng.Next(), FirstPlayer = rng.Int(2) };
            for (int p = 0; p < 2; p++) { var (m, x) = db.Deck(decks[rng.Int(decks.Count)]); setup.Main[p].AddRange(m); setup.Extra[p].AddRange(x); }
            var d = new Duel(setup);
            var ais = new[] { new Ai(0, 1), new Ai(1, 2) };
            int steps = 0;
            while (!d.Over && steps++ < 4000) d.Submit(ais[d.Pending!.Player].Decide(d));
            d.Replay();
            var sw = Stopwatch.StartNew();
            for (int k = 0; k < 20; k++) d.Replay();
            times.Add((d.Log.Count, sw.Elapsed.TotalMilliseconds / 20));
        }
        Console.WriteLine($"Replay: avg {times.Average(t => t.cmds):0} commands, avg {times.Average(t => t.ms):0.00} ms, worst {times.Max(t => t.ms):0.00} ms");
    }
}
