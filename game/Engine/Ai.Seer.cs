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

    int? Search(Duel d, Prompt p)
    {
        if (p.Kind is not (Ask.Main or Ask.Battle or Ask.Respond) || p.Options.Count < 2) return null;
        // The Classic+ pick is the default and also the tie-break, so the search can only move off it on evidence.
        var plus = new Ai(me, rng.Next(), Brain.Plus).CopyTurn(this);
        int baseline = plus.Decide(d).Picks[0];
        // Options that can't differ (two copies of one card in hand) are simulated once.
        var keys = p.Options.Select(o => Key(d, o)).ToList();
        // The rules every brain keeps (no second copy of a card already on this chain) hold for the search too.
        var reps = Enumerable.Range(0, p.Options.Count).Where(i => keys.IndexOf(keys[i]) == i && !Doubled(d, p.Options[i])).ToList();
        if (reps.Count < 2) return null;
        var sum = new double[p.Options.Count];
        var n = new int[p.Options.Count];
        var world = new double[p.Options.Count];
        var ok = new bool[p.Options.Count];
        var clock = System.Diagnostics.Stopwatch.StartNew();
        int horizon = d.Turn + (d.TurnPlayer == me ? 2 : 1);   // through the opponent's next turn
        int worlds = 0;
        for (int w = 0; w < Worlds; w++)
        {
            ulong seed = rng.Next();
            bool timeUp = false;
            foreach (int i in reps)
            {
                // A world cut short by the budget is dropped whole; with none complete the Classic+ pick stands.
                if (clock.ElapsedMilliseconds > TimeBudgetMs) { timeUp = true; break; }
                ok[i] = false;
                try
                {
                    var sim = d.Replay();
                    sim.Redeal(me, seed);
                    sim.Submit(new Command(me, new[] { i }));
                    var ais = new Ai[2];
                    ais[me] = new Ai(me, seed ^ 0x51, Brain.Plus).CopyTurn(this);
                    ais[1 - me] = new Ai(1 - me, seed ^ 0xA7, Brain.Plus);
                    int steps = 0;
                    while (!sim.Over && sim.Turn < horizon && steps++ < 600) sim.Submit(ais[sim.Pending!.Player].Decide(sim));
                    world[i] = Eval(sim);
                    ok[i] = true;
                }
                catch (Exception) { }   // an engine error in a simulation says nothing about the option
            }
            if (timeUp) break;
            worlds++;
            foreach (int i in reps) if (ok[i]) { sum[i] += world[i]; n[i]++; }
        }
        int rb = reps.First(i => keys[i] == keys[baseline]);
        int best = baseline;
        if (n[rb] == worlds && worlds > 0)
        {
            double bestScore = sum[rb] / worlds;
            foreach (int i in reps)
                if (n[i] == worlds && sum[i] / worlds > bestScore + Margin) { best = i; bestScore = sum[i] / worlds; }
        }
        best = Sane(d, p, best, baseline);
        if (best != baseline) Overruled?.Invoke($"Seer {p.Options[best]} over {p.Options[baseline]} ({worlds} worlds): " + string.Join(", ", reps.Select(i => $"{p.Options[i]} {(n[i] == worlds && worlds > 0 ? (sum[i] / worlds).ToString("0") : "failed")}")));
        // Keep the per-turn memory in step with what Classic+ would have recorded (stops ping-pong).
        if (best == baseline) tried.UnionWith(plus.tried);
        else Mark(p.Options[best]);
        return best;
    }

    const double Margin = 50;   // an option must beat the Classic+ pick by this much (LP-equivalent) to replace it

    Ai CopyTurn(Ai from)
    {
        turn = from.turn;
        tried.UnionWith(from.tried);
        return this;
    }

    /// The learned board value (Value.cs) the look-ahead scores with; null = the old fixed score below.
    public Value? Board = Value.Trained;

    /// Board score from this AI's side, in LP-equivalent points.
    double Eval(Duel d)
    {
        if (d.Over) return d.Winner == me ? 100000 : d.Winner < 0 ? 0 : -100000;
        if (Board != null) return Board.Points(d, me);
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
