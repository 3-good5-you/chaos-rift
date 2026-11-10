using System.Text.RegularExpressions;
using PoC.Engine;

namespace PoC.Check;

/// `pool [filter]`: every scripted official card (anime pool) played by the bots in duels built around it: 3 copies,
/// 2 of each card its text names, small normal monsters to fill, and its Fusion Monster in the Fusion Deck. Reports
/// crashes and cards whose script never ran. Smoke only: whether an effect is right is checked in the card lab.
static class PoolCheck
{
    public static int Run(string filter)
    {
        var db = CardDb.Shared;
        var pool = db.ByIdx.Values.Where(d => d.Modern && d.Name.Contains(filter, StringComparison.OrdinalIgnoreCase)).GroupBy(d => d.Name).Select(g => g.First()).OrderBy(d => d.Name).ToList();
        var effect = pool.Where(d => !d.IsNormalMonster).ToList();
        var wired = effect.Where(Scripts.IsWired).ToList();
        var fillers = db.ByIdx.Values.Where(c => c.IsNormalMonster && c.Level <= 4 && c.Atk is >= 1000 and <= 1800 && !c.Modern).Take(20).ToList();
        int crashes = 0;
        foreach (var d in wired)
        {
            // Cards it names, and Fusion Monsters it is a material for / that it summons.
            var named = Regex.Matches(d.Desc, "\"([^\"]+)\"").Select(m => m.Groups[1].Value).Distinct()
                .Where(n => db.ByName.ContainsKey(n)).Select(n => db.ByName[n][0]).Where(c => c.Name != d.Name).ToList();
            var fusions = db.ByIdx.Values.Where(f => f.IsFusion && (f == d || f.Desc.Contains('"' + d.Name + '"', StringComparison.OrdinalIgnoreCase))).Take(4).ToList();
            for (int seed = 1; seed <= 6; seed++)
            {
                var s = new DuelSetup { Seed = (ulong)(seed * 7919 + d.Idx), FirstPlayer = seed % 2, Classic = false };
                for (int p = 0; p < 2; p++)
                {
                    var main = new List<int>();
                    if (!d.IsFusion) main.AddRange(Enumerable.Repeat(d.Idx, 3));
                    foreach (var n in named.Where(n => !n.IsFusion).Take(8)) main.AddRange(Enumerable.Repeat(n.Idx, 2));
                    for (int i = 0; main.Count < 40; i++) main.Add(fillers[(i + seed + p) % fillers.Count].Idx);
                    s.Main[p].AddRange(main);
                    s.Extra[p].AddRange(fusions.Concat(named.Where(n => n.IsFusion)).Select(f => f.Idx).Distinct());
                }
                try
                {
                    var duel = new Duel(s);
                    var bot = new Rng((ulong)(seed * 31 + d.Idx));
                    for (int i = 0; i < 700 && !duel.Over; i++) duel.Submit(Bots.Decide(duel, bot));
                }
                catch (Exception e)
                {
                    crashes++;
                    Console.WriteLine($"CRASH {d.Name} (seed {seed}): {e.GetBaseException().Message}");
                    Console.WriteLine(string.Join("\n", (e.GetBaseException().StackTrace ?? "").Split('\n').Take(6)));
                    break;
                }
            }
        }
        var never = wired.Where(d => Scripts.Hits.GetValueOrDefault(d.Name) == 0 && Scripts.Get(d) is { } sc && (sc.Run != null || sc.On != null || sc.Proc != null || sc.IgnRun != null || sc.GraveFx != null)).ToList();
        Console.WriteLine($"pool: {pool.Count} official cards, {effect.Count} with effects, {wired.Count} scripted, {effect.Count - wired.Count} not yet");
        int sure = wired.Count(d => Scripts.Get(d)?.Sure == true);
        Console.WriteLine($"pool: {sure} auto-approved, {wired.Count - sure} go to the card lab");
        Console.WriteLine($"pool: {wired.Count - never.Count}/{wired.Count} scripted cards ran their code in bot duels; crashes: {crashes}");
        foreach (var d in never) Console.WriteLine("  never ran: " + d.Name);
        return crashes == 0 ? 0 : 1;
    }
}
