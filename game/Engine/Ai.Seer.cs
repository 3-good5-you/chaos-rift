namespace PoC.Engine;

/// Seer (EXPERT): look-ahead on the big decisions (Main phase actions, attacks, trap responses).
/// Each option is tried on Replay copies whose hidden cards are re-dealt (Duel.Redeal: no peeking), then both
/// players are played out by Classic+ until the end of the opponent's next turn; the end board is scored.
/// Same worlds for every option (common random numbers), best average wins. Everything else stays Classic+.
public sealed partial class Ai
{
    public int Worlds = 6;            // re-dealt worlds per decision
    public int TimeBudgetMs = 1500;   // safety cap: a decision takes at most this plus one simulation

    /// The CPU for a difficulty level (OpponentPick.Level 1..7): the strongest brain at every level (user 2026-10-30:
    /// the CPU is always as smart as it can be; levels differ by the rival's deck, Progression). Look-ahead (Seer) on
    /// the big decisions, every card play judged by simulation, the learned board value scoring both.
    public static Ai ForLevel(int player, ulong seed, int level) => new(player, seed, Brain.Seer) { Worlds = 8, JudgeWorlds = 4 };

    /// Simulations run side by side on this many threads (the game: every core but one; batch runs that already play
    /// duels in parallel set 1).
    public static int Threads = Math.Max(1, Environment.ProcessorCount - 1);
    static ParallelOptions Par => new() { MaxDegreeOfParallelism = Threads };

    int? Search(Duel d, Prompt p)
    {
        if (p.Kind is not (Ask.Main or Ask.Battle or Ask.Respond) || p.Options.Count < 2) return null;
        // The Classic+ pick is the default and also the tie-break, so the search can only move off it on evidence.
        var plus = new Ai(me, rng.Next(), Brain.Plus) { Legacy = Legacy }.CopyTurn(this);
        int baseline = plus.Decide(d).Picks[0];
        // Options that can't differ (two copies of one card in hand) are simulated once.
        var keys = p.Options.Select(o => Key(d, o)).ToList();
        // The rules every brain keeps (no second copy of a card already on this chain) hold for the search too.
        var reps = Enumerable.Range(0, p.Options.Count).Where(i => keys.IndexOf(keys[i]) == i && !Doubled(d, p.Options[i])).ToList();
        if (reps.Count < 2) return null;
        int rb = reps.First(i => keys[i] == keys[baseline]);
        var sum = new double[p.Options.Count];
        var lost = new int[p.Options.Count];   // worlds an option scored below the leader of that world
        var alive = new List<int>(reps);       // options still searched: one that loses every world by far is dropped
        var clock = System.Diagnostics.Stopwatch.StartNew();
        int horizon = d.Turn + (d.TurnPlayer == me ? 2 : 1);   // through the opponent's next turn
        int worlds = 0;
        for (int w = 0; w < Worlds && alive.Count > 1 && clock.ElapsedMilliseconds < TimeBudgetMs; w++)
        {
            ulong seed = rng.Next();
            var world = new double[p.Options.Count];
            var ok = new bool[p.Options.Count];
            bool cut = false;
            Parallel.ForEach(alive, Par, i =>
            {
                try
                {
                    var sim = d.Replay();
                    sim.Redeal(me, seed);
                    sim.Submit(new Command(me, new[] { i }));
                    var ais = new Ai[2];
                    ais[me] = new Ai(me, seed ^ 0x51, Brain.Plus).CopyTurn(this);
                    ais[1 - me] = new Ai(1 - me, seed ^ 0xA7, Brain.Plus) { Legacy = Legacy };
                    for (int steps = 0; !sim.Over && sim.Turn < horizon && steps < 600; steps++)
                    {
                        // A world cut short by the budget is dropped whole (a stalled rollout no longer runs on for seconds).
                        if ((steps & 15) == 15 && clock.ElapsedMilliseconds > TimeBudgetMs) { cut = true; return; }
                        sim.Submit(ais[sim.Pending!.Player].Decide(sim));
                    }
                    world[i] = Eval(sim);
                    ok[i] = true;
                }
                catch (Exception) { }   // an engine error in a simulation says nothing about the option
            });
            if (cut) break;
            if (!ok[rb]) break;   // no score for the Classic+ pick in this world: nothing to compare with
            worlds++;
            alive.RemoveAll(i => !ok[i]);
            double top = alive.Max(i => world[i]);
            foreach (int i in alive) { sum[i] += world[i]; if (world[i] < top) lost[i]++; }
            // An option behind in every world so far, by 2500 LP-equivalent on average, is not worth more simulations.
            if (worlds >= 3 && !NoPrune)
            {
                double lead = alive.Max(i => sum[i]);
                alive.RemoveAll(i => i != rb && lost[i] == worlds && (lead - sum[i]) / worlds > PruneGap);
            }
        }
        int best = baseline;
        if (worlds > 0 && alive.Contains(rb))
        {
            double bestScore = sum[rb] / worlds;
            foreach (int i in alive)
                if (sum[i] / worlds > bestScore + Margin) { best = i; bestScore = sum[i] / worlds; }
        }
        if (p.Kind == Ask.Main && Combo && worlds > 0 && alive.Contains(rb) && alive.Count > 1)
            best = Deep(d, alive.OrderByDescending(i => sum[i]).Take(ComboTop).Union(new[] { rb }).ToList(), rb, best, horizon);
        best = Sane(d, p, best, baseline);
        if (best != baseline) Overruled?.Invoke($"Seer {p.Options[best]} over {p.Options[baseline]} ({worlds} worlds, {clock.ElapsedMilliseconds} ms): " + string.Join(", ", alive.Select(i => $"{p.Options[i]} {sum[i] / worlds:0}")));
        // Keep the per-turn memory in step with what Classic+ would have recorded (stops ping-pong).
        if (best == baseline) tried.UnionWith(plus.tried);
        else Mark(p.Options[best]);
        return best;
    }

    /// Combo look-ahead (Main phase): the few best first actions are tried with every follow-up action of the same
    /// Main phase, so a play that only pays off with the right next play (a search then the card it fetched, a tribute
    /// fodder then the big summon, an equip then the attack) is scored by its best line, not by what Classic+ would
    /// happen to do next. Fresh worlds, shared by every line (common random numbers); its own time budget.
    int Deep(Duel d, List<int> firsts, int rb, int fallback, int horizon)
    {
        var clock = System.Diagnostics.Stopwatch.StartNew();
        // line totals per first action, keyed by the follow-up ("" = no Main-phase follow-up: the turn moved on)
        var lines = firsts.ToDictionary(i => i, _ => new Dictionary<string, (double sum, int n)>());
        int worlds = 0;
        for (int w = 0; w < ComboWorlds && clock.ElapsedMilliseconds < ComboBudgetMs; w++)
        {
            ulong seed = rng.Next();
            // Same seeds for every line: the prefix (first action, then whatever Classic+ answers with) replays exactly.
            (Duel sim, Ai[] ais) Prefix(int i)
            {
                var sim = d.Replay();
                sim.Redeal(me, seed);
                sim.Submit(new Command(me, new[] { i }));
                var ais = new Ai[2];
                ais[me] = new Ai(me, seed ^ 0x51, Brain.Plus).CopyTurn(this);
                ais[1 - me] = new Ai(1 - me, seed ^ 0xA7, Brain.Plus) { Legacy = Legacy };
                for (int steps = 0; !sim.Over && sim.Turn == d.Turn && steps < 200; steps++)
                {
                    if (sim.Pending is { Kind: Ask.Main } q && q.Player == me) break;
                    sim.Submit(ais[sim.Pending!.Player].Decide(sim));
                }
                return (sim, ais);
            }
            double Finish(Duel sim, Ai[] ais)
            {
                for (int steps = 0; !sim.Over && sim.Turn < horizon && steps < 600; steps++)
                {
                    if ((steps & 15) == 15 && clock.ElapsedMilliseconds > ComboBudgetMs) throw new TimeoutException();
                    sim.Submit(ais[sim.Pending!.Player].Decide(sim));
                }
                return Eval(sim);
            }
            // Every (first, follow-up) line of this world.
            var jobs = new List<(int i, int j, string key)>();
            foreach (int i in firsts)
            {
                try
                {
                    var (sim, _) = Prefix(i);
                    if (sim.Over || sim.Pending is not { Kind: Ask.Main } q || q.Player != me || q.Options.Count < 2) { jobs.Add((i, -1, "")); continue; }
                    var seen = new HashSet<string>();
                    for (int j = 0; j < q.Options.Count; j++)
                        if (!Doubled(sim, q.Options[j]) && seen.Add(Key(sim, q.Options[j]))) jobs.Add((i, j, Key(sim, q.Options[j])));
                }
                catch (Exception) { }
            }
            var score = new double?[jobs.Count];
            bool cut = false;
            Parallel.For(0, jobs.Count, Par, k =>
            {
                try
                {
                    var (sim, ais) = Prefix(jobs[k].i);
                    if (jobs[k].j >= 0) sim.Submit(new Command(me, new[] { jobs[k].j }));
                    score[k] = Finish(sim, ais);
                }
                catch (TimeoutException) { cut = true; }
                catch (Exception) { }
            });
            if (cut) break;   // a world cut short is dropped whole
            worlds++;
            for (int k = 0; k < jobs.Count; k++)
                if (score[k] is double v)
                {
                    var t = lines[jobs[k].i];
                    var (s0, n0) = t.GetValueOrDefault(jobs[k].key);
                    t[jobs[k].key] = (s0 + v, n0 + 1);
                }
        }
        if (worlds == 0) return fallback;
        // A first action is worth its best line (a line must have a score in every world).
        double Worth(int i) => lines[i].Values.Where(x => x.n == worlds).Select(x => x.sum / worlds).DefaultIfEmpty(double.NegativeInfinity).Max();
        int best = rb;
        double bestScore = Worth(rb);
        if (double.IsNegativeInfinity(bestScore)) return fallback;
        foreach (int i in firsts)
            if (Worth(i) > bestScore + Margin) { best = i; bestScore = Worth(i); }
        return best;
    }

    public bool Combo = true;
    public int ComboTop = 3, ComboWorlds = 4, ComboBudgetMs = 1500;

    const double PruneGap = 2500;
    const double Margin = 50;   // an option must beat the Classic+ pick by this much (LP-equivalent) to replace it

    /// The CPU as it was before 2026-11-15 (rule brain without the text rules, search without pruning): A/B runs only.
    public bool Legacy, NoPrune;

    Ai CopyTurn(Ai from)
    {
        Legacy = from.Legacy;
        turn = from.turn;
        tried.UnionWith(from.tried);
        return this;
    }

    /// Progress toward a win that isn't LP (user 2026-11-16: long-term plans). The learned value sees too few Exodia or
    /// Destiny Board duels to price them, so the steps are set by hand: each piece in hand is worth more than the last,
    /// each Spirit Message on the board brings the win a turn nearer. Both sides are scored, so the look-ahead also
    /// goes after the opponent's plan (destroying their Destiny Board, making them discard pieces).
    static double Plan(Duel d, int s)
    {
        var p = d.P(s);
        double v = 0;
        if (ExodiaLive(p)) v += ExodiaStep[Math.Min(4, p.Hand.Count(IsExodia))];
        if (p.SpellsOnField.Any(c => c.FaceUp && c.Name == "Destiny Board"))
            v += BoardStep[Math.Min(3, p.FieldCards.Count(c => c.Name.StartsWith("Spirit Message")))];
        return v;
    }
    static readonly double[] ExodiaStep = { 0, 150, 600, 1600, 4000 };   // by pieces in hand
    static readonly double[] BoardStep = { 800, 1800, 3500, 6000 };      // Destiny Board up, by Spirit Messages placed

    /// The learned board value (Value.cs) the look-ahead scores with; null = the old fixed score below.
    public Value? Board = Value.Trained;

    /// Board score from this AI's side, in LP-equivalent points.
    double Eval(Duel d)
    {
        if (d.Over) return d.Winner == me ? 100000 : d.Winner < 0 ? 0 : -100000;
        if (Board != null) return Board.Points(d, me) + (Legacy ? 0 : Plan(d, me) - Plan(d, 1 - me));
        Player a = Me(d), b = Opp(d);
        double Side(Player pl)
        {
            double v = Math.Min(pl.Lp, 8000) + 400 * pl.Hand.Count + 1500 * pl.Hand.Count(IsExodia);
            foreach (var m in pl.MonstersOnField) v += 500 + 0.5 * Math.Max(d.Atk(m), d.Def(m));
            v += 300 * pl.SpellsOnField.Count(c => !c.FaceUp);
            // A face-up Continuous/Field card keeps working (worth a card in hand); an equip's worth is in its monster.
            v += pl.SpellsOnField.Where(c => c.FaceUp).Sum(c => c.EquippedTo != null || c.Data.Is("Equip") ? 150 : 400);
            return v;
        }
        return Side(a) - Side(b);
    }
}
