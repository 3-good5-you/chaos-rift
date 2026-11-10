using PoC.Engine;

namespace PoC.Check;

/// `godduels N [seer]`: the rare God decks (Progression.GodDecks) played by the CPU against the rival HARD/EXPERT
/// decks, CPU on both sides. Reports per God deck how often each God hit the field and each God effect was used,
/// the win rate, and every engine exception. Default brain = Classic+ with one judged world (fast); `seer` = the game's
/// CPU (Ai.ForLevel), much slower.
static class GodDuels
{
    public sealed class Stats
    {
        public int Duels, Wins, Errors, Unfinished;
        public readonly Dictionary<string, int> Count = new();
        public void Add(string k, int n = 1) { lock (Count) Count[k] = Count.GetValueOrDefault(k) + n; }
    }

    static readonly string[] Foes = new[] { "yugi", "kaiba", "joey", "jaden" }.SelectMany(r => new[] { 5, 6, 7 }.Select(l => $"{r}/x_lv{l}")).ToArray();

    public static Dictionary<string, Stats> Play(int n, bool seer, ulong seed = 1, Action<string>? fail = null)
    {
        var db = CardDb.Shared;
        var all = Progression.GodDecks.ToDictionary(k => k, _ => new Stats());
        Scripts.Get(db.Named("Pot of Greed")); _ = new Ai(0, 1);   // build shared tables before going parallel
        Parallel.For(0, n * Progression.GodDecks.Length, new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount }, j =>
        {
            string key = Progression.GodDecks[j % Progression.GodDecks.Length];
            var st = all[key];
            var rng = new Rng(seed * 1000003 + (ulong)j);
            string foe = Foes[rng.Int(Foes.Length)];
            int god = rng.Int(2);   // the God deck's seat
            var s = new DuelSetup { Seed = rng.Next(), FirstPlayer = rng.Int(2), Classic = false };
            var (gm, ge) = db.Deck(key); var (fm, fe) = db.Deck(foe);
            s.Main[god].AddRange(DeckRules.ForCpu(gm)); s.Extra[god].AddRange(ge);
            s.Main[1 - god].AddRange(DeckRules.ForCpu(fm)); s.Extra[1 - god].AddRange(fe);
            Duel? d = null;
            try
            {
                d = new Duel(s);
                Ai Cpu(int p) => seer ? Ai.ForLevel(p, rng.Next(), 7) : new Ai(p, rng.Next(), Brain.Plus) { JudgeWorlds = 1 };
                var ai = new[] { Cpu(0), Cpu(1) };
                d.OnEvent += e =>
                {
                    if (e.Kind == EvKind.Summoned && e.Card is { } c && c.Controller == god && c.Data.Race == "Divine-Beast") st.Add($"on field: {c.Name}");
                    if (e.Kind == EvKind.Activated && e.Link?.Card is { } a && a.Controller == god && a.Data.Race == "Divine-Beast") st.Add($"effect: {a.Name}");
                    if (e.Kind == EvKind.Destroyed && e.Other is { } by && by.Controller == god && by.Data.Race == "Divine-Beast" && e.Card?.Controller != god) st.Add($"kills: {by.Name}{(e.Battle ? " (battle)" : " (effect)")}");
                };
                d.OnTriggered += (c, _, e) =>
                {
                    if (c.Controller != god || c.Data.Race != "Divine-Beast") return;
                    if (c.Name == "Slifer the Sky Dragon" && e.Kind == EvKind.Summoned) st.Add("Slifer: -2000 on an opponent's summon");
                    if (e.Kind == EvKind.EndPhase && c.Loc != Loc.Monster) st.Add($"end phase: {c.Name} sent back");
                };
                for (int i = 0; i < 4000 && !d.Over; i++)
                {
                    var p = d.Pending!;
                    var cmd = ai[p.Player].Decide(d);
                    if (p.Player == god && p.Kind == Ask.YesNo && p.Source?.Name == "The Winged Dragon of Ra" && p.Text.StartsWith("Pay"))
                        st.Add(cmd.Picks[0] == 0 ? "Ra: paid LP down to 100" : "Ra: kept its LP");
                    d.Submit(cmd);
                }
                lock (st) { if (d.Over) { st.Duels++; if (d.Winner == god) st.Wins++; } else st.Unfinished++; }
            }
            catch (Exception e)
            {
                lock (st) st.Errors++;
                fail?.Invoke($"{key} vs {foe} (duel {j}, turn {d?.Turn}): {e.InnerException?.Message ?? e.Message}\n{e.InnerException?.StackTrace ?? e.StackTrace}");
            }
        });
        return all;
    }

    public static int Run(int n, bool seer)
    {
        int errors = 0;
        var clock = System.Diagnostics.Stopwatch.StartNew();
        var all = Play(n, seer, fail: m => { Interlocked.Increment(ref errors); Console.WriteLine("FAIL " + m); });
        foreach (var (key, st) in all)
        {
            Console.WriteLine($"{key}: {st.Duels} duels, won {100.0 * st.Wins / Math.Max(1, st.Duels):0}%, unfinished {st.Unfinished}, errors {st.Errors}");
            foreach (var (k, v) in st.Count.OrderBy(x => x.Key)) Console.WriteLine($"  {k,-58} {v,5}  ({(double)v / Math.Max(1, st.Duels):0.00}/duel)");
        }
        Console.WriteLine($"godduels: {n} per deck, {(seer ? "Seer" : "Classic+ judged")} brain, {clock.Elapsed.TotalSeconds:0}s, {errors} errors");
        return errors == 0 ? 0 : 1;
    }
}
