using System.Diagnostics;

namespace PoC.Engine;

/// Judge: the rule brains know cards by name lists and by words in their text, so every card outside the lists is
/// a guess (a "destroy" spell fired into its own board, a revive aimed at the wrong monster, a trap never set off).
/// Before committing, the CPU plays its candidate answers out on re-dealt copies of the duel (Replay + Redeal: no
/// peeking) until the dust settles, and scores each board with Eval. The engine runs the real card scripts, so every
/// card in the pool is judged by what it actually does. The rule pick stays the default; another answer replaces it
/// only when it scores clearly better.
public sealed partial class Ai
{
    public int JudgeWorlds;            // re-dealt worlds per judged decision; 0 = rules only (Classic, simulations)
    public int JudgeBudgetMs = 900;    // safety cap per decision (a world cut short is dropped whole)
    const double JudgeMargin = 150;    // LP-equivalent an answer must win by to replace the rule pick
    readonly Dictionary<string, double> gains = new();   // card plays judged this turn, by option @ board
    public static Action<string>? Overruled;
    public static Action<string, double>? Used;     // a Spell/Trap the CPU used and what the judge saw it gain (Value.Uses)

    /// Report a chosen activation's gain, adding back the hold value the card gave up (so the record is the payoff).
    void Report(Opt o, double gain)
    {
        if (o is { Act: Act.Activate, Card: { } c } && !c.Data.IsMonster && !double.IsNaN(gain) && gain > double.MinValue)
            Used?.Invoke(c.Name, gain + (Board?.Hold(c) ?? 0));
    }           // diagnostics: a judged answer that replaced the rule pick

    int[]? Judge(Duel d, Prompt p)
    {
        switch (p.Kind)
        {
            case Ask.Main: return JudgeMain(d, p);
            case Ask.Battle or Ask.Respond or Ask.YesNo or Ask.Choose:
                return Best(d, p, Rule(d, p), Enumerable.Range(0, p.Options.Count).Where(i => !Doubled(d, p.Options[i])).Select(i => new[] { i }));
            case Ask.Pick:
                var (order, n) = PickOrder(d, p);
                return Best(d, p, order.Take(n).ToArray(), PickSets(p, order, n));
            default: return null;
        }
    }

    /// Main Phase: every card play (activation, Special Summon procedure, Normal Summon or Set, position change) is
    /// scored against doing nothing. The learned board value (Value.cs) prices positions: a low-ATK monster in Attack
    /// Position shows up as the opponent's damage next turn. The best play that pays is made; a play the rules chose that hurts is
    /// dropped and the rules asked again.
    int[] JudgeMain(Duel d, Prompt p)
    {
        var before = tried.ToList();
        int rule = Rule(d, p)[0];
        var plays = new List<int>();
        var seen = new HashSet<string>();
        for (int i = 0; i < p.Options.Count; i++)
            if (IsPlay(p.Options[i]) && seen.Add(Key(d, p.Options[i]))) plays.Add(i);
        if (plays.Count == 0) return new[] { rule };
        string board = "@" + d.StateHash();
        var todo = plays.Where(i => !gains.ContainsKey(Key(d, p.Options[i]) + board)).ToList();
        if (todo.Count > 0)
        {
            var sc = Play(d, todo.Select(i => (int[]?)new[] { i }).Prepend(null).ToList());
            if (sc == null || double.IsNaN(sc[0])) return new[] { rule };
            for (int k = 0; k < todo.Count; k++) gains[Key(d, p.Options[todo[k]]) + board] = sc[k + 1] - sc[0];
        }
        double Gain(int i) => gains.TryGetValue(Key(d, p.Options[i]) + board, out var g) && !double.IsNaN(g) ? g : double.MinValue;
        int best = plays.MaxBy(Gain);
        if (Gain(best) > JudgeMargin && (!IsPlay(p.Options[rule]) || Gain(best) > Gain(rule) + JudgeMargin))
        {
            tried.Clear(); tried.UnionWith(before);
            // Without the learned value the score can't see next turn's attacks: the summon takes the rules' position.
            if (Board == null && p.Options[best] is { Act: Act.Summon, Card: { } mon } && !IsExodia(mon)) best = SetFlips(p, Pos(p, best, WantsDefense(d, mon.Data.Atk, mon.Data.Def)));
            Mark(p.Options[best]);
            Overruled?.Invoke($"Main play {p.Options[best]} +{Gain(best):0} over {p.Options[rule]}");
            Report(p.Options[best], Gain(best));
            return new[] { best };
        }
        for (int guard = 0; IsPlay(p.Options[rule]) && Gain(rule) < -JudgeMargin && guard < 20; guard++)
        {
            int uid = p.Options[rule].Card!.Uid;
            Overruled?.Invoke($"Main veto {p.Options[rule]} {Gain(rule):0}");
            if (p.Options[rule].Act is Act.Summon or Act.Set) { tried.Add("s"); rule = Rule(d, p)[0]; continue; }
            if (p.Options[rule].Act is Act.ChangePos or Act.FlipSummon) { tried.Add("p" + uid); rule = Rule(d, p)[0]; continue; }
            tried.UnionWith(new[] { "a" + uid, "e" + uid, "x" + uid });
            rule = Rule(d, p)[0];
        }
        if (IsPlay(p.Options[rule])) Report(p.Options[rule], Gain(rule));
        return new[] { rule };
    }

    static bool IsPlay(Opt o) => o.Act is Act.Activate or Act.Special or Act.Summon or Act.Set or Act.ChangePos or Act.FlipSummon;

    /// Pick answers worth trying besides the rule's: each other card swapped in for the rule's last choice, the
    /// rule order at the smallest and largest allowed size, and the order reversed (the other side's cards).
    static IEnumerable<int[]> PickSets(Prompt p, List<int> order, int n)
    {
        var head = order.Take(n - 1);
        foreach (int x in order.Skip(n).Take(8)) yield return head.Append(x).ToArray();
        if (p.Min < n) yield return order.Take(p.Min).ToArray();
        if (p.Max > n) yield return order.Take(p.Max).ToArray();
        if (n > 1) yield return Enumerable.Reverse(order).Take(n).ToArray();
    }

    /// Simulate the rule answer and the alternatives; the best average replaces the rule answer only by JudgeMargin.
    int[] Best(Duel d, Prompt p, int[] rule, IEnumerable<int[]> alts)
    {
        string SetKey(int[] s) => string.Join(";", s.Select(i => Key(d, p.Options[i])).Order());
        var cands = new List<int[]> { rule };
        var seen = new HashSet<string> { SetKey(rule) };
        foreach (var a in alts) if (seen.Add(SetKey(a))) cands.Add(a);
        if (cands.Count < 2) return rule;
        var sc = Play(d, cands.Select(c => (int[]?)c).ToList());
        if (sc == null || double.IsNaN(sc[0])) return rule;
        int best = 0;
        for (int i = 1; i < cands.Count; i++) if (sc[i] > sc[best] + (best == 0 ? JudgeMargin : 0)) best = i;
        if (best > 0) Overruled?.Invoke($"{p.Kind} '{p.Text}' {Show(cands[best])} {sc[best] - sc[0]:+0} over {Show(rule)}");
        string Show(int[] s) => "[" + string.Join(", ", s.Select(i => p.Options[i])) + "]";
        int pass = cands.FindIndex(c => c.Length == 1 && p.Options[c[0]].Act == Act.Pass);
        if (p.Kind == Ask.Respond && pass >= 0 && cands[best].Length == 1) Report(p.Options[cands[best][0]], sc[best] - sc[pass]);
        return cands[best];
    }

    /// Average Eval per candidate answer over re-dealt worlds (the same worlds for each: common random numbers).
    /// A null candidate leaves the board as it is. NaN = that answer broke the engine.
    double[]? Play(Duel d, List<int[]?> cands)
    {
        var sum = new double[cands.Count];
        var bad = new bool[cands.Count];
        var clock = Stopwatch.StartNew();
        int worlds = 0;
        for (int w = 0; w < JudgeWorlds; w++)
        {
            ulong seed = rng.Next();
            var s = new double[cands.Count];
            for (int i = 0; i < cands.Count; i++)
            {
                if (clock.ElapsedMilliseconds > JudgeBudgetMs) return Average();
                try
                {
                    var sim = d.Replay();
                    sim.Redeal(me, seed);
                    if (cands[i] is { } c) { sim.Submit(new Command(me, c)); Settle(sim, seed); }
                    s[i] = Eval(sim);
                }
                catch (Exception e) { bad[i] = true; Overruled?.Invoke($"simulation failed: {e.InnerException?.Message ?? e.Message}"); }
            }
            worlds++;
            for (int i = 0; i < s.Length; i++) sum[i] += s[i];
        }
        return Average();
        double[]? Average() => worlds == 0 ? null : sum.Select((v, i) => bad[i] ? double.NaN : v / worlds).ToArray();
    }

    /// Play on (Classic+ both sides) until someone is back at an open Main/Battle prompt or the turn ends.
    void Settle(Duel sim, ulong seed)
    {
        var ais = new Ai[2];
        ais[me] = new Ai(me, seed ^ 0x51, Brain.Plus).CopyTurn(this);
        ais[1 - me] = new Ai(1 - me, seed ^ 0xA7, Brain.Plus);
        int t = sim.Turn;
        for (int steps = 0; !sim.Over && sim.Turn == t && sim.Pending!.Kind is not (Ask.Main or Ask.Battle) && steps < 300; steps++)
            sim.Submit(ais[sim.Pending.Player].Decide(sim));
    }

    /// Options that can't differ in outcome (two copies of one card in hand) share a key.
    static string Key(Duel d, Opt o) => $"{o.Act}|{o.Card?.Name}|{o.Card?.Loc}|{o.Card?.Controller}|{o.Card?.FaceUp}|{o.Card?.Defense}|{(o.Card is { OnField: true } c ? d.Atk(c) : 0)}" +
        $"|{o.Target?.Name}|{o.Target?.Controller}|{(o.Target is { OnField: true } t ? d.Atk(t) : 0)}|{o.Target?.FaceUp}|{o.Arg}|{o.Text}";

    /// Record a chosen Main Phase answer the way the rules would have (stops ping-pong).
    void Mark(Opt o)
    {
        if (o.Card is not { } c) return;
        tried.Add(o.Act switch
        {
            Act.Summon or Act.Set => "s",
            Act.ChangePos or Act.FlipSummon => "p" + c.Uid,
            Act.SetSpell => "t" + c.Uid,
            Act.Special => "x" + c.Uid,
            _ => "a" + c.Uid,
        });
    }
}
