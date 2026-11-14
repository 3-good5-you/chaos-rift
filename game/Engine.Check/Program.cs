using PoC.Engine;

namespace PoC.Check;

// Assert-based engine checks. Run: dotnet run --project Engine.Check [-- duels N]
static class Program
{
    static int failures;

    static void Check(bool ok, string what)
    {
        if (!ok) { failures++; Console.WriteLine("FAIL " + what); }
    }

    static int Main(string[] args)
    {
        if (Environment.GetEnvironmentVariable("JUDGE_LOG") == "1") Ai.Overruled = m => { lock (Console.Out) Console.WriteLine("  " + m); };
        if (args.Length == 2 && args[0] == "one")
        {
            // Determinism probe: play one random duel and print its fingerprint.
            int i = int.Parse(args[1]);
            var setup = Bots.RandomSetup(new Rng((ulong)(1000 + i)), chaos: i % 2 == 1);
            var d = new Duel(setup);
            var bot = new Rng((ulong)(9000 + i));
            int steps = 0;
            while (!d.Over && steps++ < 4000) d.Submit(Bots.Decide(d, bot));
            Console.WriteLine($"{d.Log.Count} {d.Turn} {d.Winner} {d.StateHash().GetHashCode() ^ 0} {string.Join("", d.Log.Take(400)).Length}");
            Console.WriteLine(d.StateHash());
            return 0;
        }
        if (args.Length >= 1 && args[0] == "scenarios") { RemasteredChecks(); Scenarios.Run(Check); Console.WriteLine(failures == 0 ? "ALL CHECKS PASSED" : $"{failures} CHECK(S) FAILED"); return failures == 0 ? 0 : 1; }
        if (args.Length >= 1 && args[0] == "godduels") return GodDuels.Run(args.Length > 1 ? int.Parse(args[1]) : 50, args.Contains("seer"));
        if (args.Length >= 1 && args[0] == "puzzles") { PuzzleChecks(); return 0; }
        if (args.Length >= 1 && args[0] == "labtest") return LabTests.Run(args.Length > 1 ? args[1] : "");
        if (args.Length >= 1 && args[0] == "audit") return Audit.Run(args.Length > 1 ? args[1] : "");
        if (args.Length >= 1 && args[0] == "poolcheck") return PoolCheck.Run(args.Length > 1 ? args[1] : "");
        if (args.Length >= 1 && args[0] == "families")
        {
            var lab = CardDb.Shared.ByIdx.Values.Where(d => d.Modern && !d.IsNormalMonster && Scripts.IsWired(d) && Scripts.Get(d) is { Sure: false }).GroupBy(d => d.Name).Select(g => g.First()).ToList();
            foreach (var g in lab.GroupBy(Families.Of).OrderBy(g => g.Key == "" ? "~" : g.Key))
                Console.WriteLine($"{(g.Key == "" ? "(none)" : g.Key)} [{g.Count()}]: {string.Join(", ", g.Select(d => d.Name).OrderBy(n => n).Take(args.Length > 1 || g.Key == "" ? 9999 : 12))}");
            return 0;
        }
        if (args.Length >= 1 && args[0] == "league")
        {
            League.Run(Enum.Parse<Brain>(args[1]), Enum.Parse<Brain>(args[2]), args.Length > 3 ? int.Parse(args[3]) : 300);
            return 0;
        }
        if (args.Length >= 2 && args[0] == "pool")
        {
            // Every playable card (one printing per name): normal, or an effect the engine scripts. For deck building.
            var pdb = CardDb.Shared;
            var rows = pdb.ByIdx.Values.Where(c => pdb.Canon(c.Idx) == c.Idx).GroupBy(c => c.Name).Select(g => g.First())
                .Where(c => !pdb.Effects.ContainsKey(c.KonamiId) || Scripts.Get(c) is { Implemented: true })
                .OrderBy(c => c.Kind).ThenBy(c => c.Name)
                .Select(c => string.Join((char)9, c.Name, DeckRules.Limit(c, "joey"), c.Kind, c.Frame, c.Level, c.Atk, c.Def, c.Attribute, c.Race, pdb.Effects.TryGetValue(c.KonamiId, out var e) ? e.Does.Replace((char)10, (char)32) : "")).ToList();
            File.WriteAllLines(args[1], rows);
            Console.WriteLine($"{rows.Count()} playable cards -> {args[1]}");
            return 0;
        }
        if (args.Length >= 1 && args[0] == "trainloop") { League.TrainLoop(double.Parse(args[1], System.Globalization.CultureInfo.InvariantCulture)); return 0; }
        if (args.Length >= 1 && args[0] == "train") { League.Train(int.Parse(args[1]), args.Length > 2 ? args[2] : "Plus"); return 0; }
        if (args.Length >= 1 && args[0] == "levels") { League.Levels(args[1], args[2], args.Length > 3 ? int.Parse(args[3]) : 200); return 0; }
        if (args.Length >= 1 && args[0] == "decks") { League.Decks(args.Length > 1 ? int.Parse(args[1]) : 100, args.Length > 2 ? Enum.Parse<Brain>(args[2]) : Brain.Classic, args.Length > 3 ? args[3] : ""); return 0; }
        if (args.Length >= 3 && args[0] == "valuevs") { League.ValueVs(args[1], int.Parse(args[2])); return 0; }
        if (args.Length >= 1 && args[0] == "rivals") { League.Rivals(args.Length > 1 ? int.Parse(args[1]) : 200, args.Length > 2 ? args[2] : "Plus"); return 0; }
        if (args.Length >= 1 && args[0] == "ladder") { League.Ladder(args.Length > 1 ? int.Parse(args[1]) : 300); return 0; }
        if (args.Length >= 1 && args[0] == "sidetest") { League.SideTest(args.Length > 1 ? int.Parse(args[1]) : 600); return 0; }
        if (args.Length >= 1 && args[0] == "passive") { League.Passive(args.Length > 1 ? int.Parse(args[1]) : 40, args.Length > 2 ? args[2] : null); return 0; }
        if (args.Length >= 1 && args[0] == "seerbench") { League.SeerBench(); return 0; }
        if (args.Length >= 1 && args[0] == "bench") { League.Bench(); return 0; }
        if (args.Length >= 1 && args[0] == "replays")
        {
            ReplayChecks(args.Length > 1 ? int.Parse(args[1]) : 40);
            Console.WriteLine(failures == 0 ? "ALL CHECKS PASSED" : $"{failures} CHECK(S) FAILED");
            return failures == 0 ? 0 : 1;
        }
        if (args.Length >= 1 && args[0] == "lockstep")
        {
            LockstepChecks(args.Length > 1 ? int.Parse(args[1]) : 200);
            Console.WriteLine(failures == 0 ? "ALL CHECKS PASSED" : $"{failures} CHECK(S) FAILED");
            return failures == 0 ? 0 : 1;
        }
        if (args.Length >= 1 && args[0] == "ai")
        {
            AiDuels(args.Length > 1 ? int.Parse(args[1]) : 200);
            Console.WriteLine(failures == 0 ? "ALL CHECKS PASSED" : $"{failures} CHECK(S) FAILED");
            return failures == 0 ? 0 : 1;
        }
        var db = CardDb.Shared;
        int pc = db.ByIdx.Values.Count(d => d.Idx < 2000);   // the Gods (1108-1110) are PoC cards given modern text
        Check(pc == 1114, $"1114 PoC cards loaded (got {pc}), plus {db.ByIdx.Count - pc} anime-pool cards");
        Check(db.Effects.Count == 464, $"464 effects loaded (got {db.Effects.Count})");
        Check(db.Fusions.Count > 50 && db.Rituals.Count > 0, "fusion/ritual tables loaded");
        var bewd = db.Named("Blue-Eyes White Dragon");
        Check(bewd.Atk == 3000 && bewd.Def == 2500 && bewd.Level == 8, "BEWD stats");

        ProgressionChecks();
        RewardChecks();
        DeckChecks();
        SaveChecks();
        FlowChecks();
        RuleChecks();
        RedealChecks();
        BrainChecks();
        RemasteredChecks();
        TournamentChecks();
        DiceChecks();
        Scenarios.Run(Check);
        {
            var gods = GodDuels.Play(3, seer: false, seed: 9, fail: m => Console.WriteLine("FAIL god duel " + m));
            Check(gods.Values.All(s => s.Errors == 0), $"God decks: CPU duels run clean ({gods.Values.Sum(s => s.Errors)} errors)");
            Check(gods.Values.Sum(s => s.Count.Where(c => c.Key.StartsWith("on field:")).Sum(c => c.Value)) > 0, "God decks: the CPU summons its Gods");
        }
        int n = args.Length > 1 && args[0] == "duels" ? int.Parse(args[1]) : 300;
        RandomDuels(n);
        AiDuels(n / 3);
        LockstepChecks(n / 10);
        LogReplay.Run(Check);

        var missing = Scripts.Missing();
        Check(missing.Count == 0, $"every effects.json card has a script ({missing.Count} missing: {string.Join(", ", missing.Select(m => m.Name))})");
        var never = db.Effects.Values.Select(e => db.ByKonami.TryGetValue(e.KonamiId, out var l) ? l[0].Name : e.Name).Distinct()
            .Where(nm => Scripts.Hits.GetValueOrDefault(nm) == 0 && Scripts.Get(db.Named(nm)) is { Marker: false } sc && (sc.Run != null || sc.On != null || sc.Proc != null || sc.IgnRun != null))
            .OrderBy(nm => nm).ToList();
        Console.WriteLine($"Script code exercised in random duels: {db.Effects.Count - never.Count}/{db.Effects.Count}; never ran: {never.Count}");
        if (args.Contains("coverage")) foreach (var nm in never) Console.WriteLine("  never ran: " + nm);

        Console.WriteLine(failures == 0 ? "ALL CHECKS PASSED" : $"{failures} CHECK(S) FAILED");
        return failures == 0 ? 0 : 1;
    }

    /// Die rolls: 1-6, every face shows up, and each roll raises a Die event carrying the result and source.
    static void DiceChecks()
    {
        var d = new Duel(Bots.RandomSetup(new Rng(77), chaos: false));
        var evs = new List<Ev>();
        d.OnEvent += e => { if (e.Kind == EvKind.Die) evs.Add(e); };
        var src = d.P(0).Deck[0];
        var rolls = Enumerable.Range(0, 300).Select(_ => d.Die(0, src)).ToList();
        Check(rolls.All(r => r is >= 1 and <= 6) && rolls.Distinct().Count() == 6, "dice: results 1-6, all faces");
        Check(evs.Count == 300 && evs.Select(e => e.Amount).SequenceEqual(rolls) && evs.All(e => e.Player == 0 && e.Other == src), "dice: one Die event per roll with result and source");
    }

    /// The hand-built EXPERT decks: 40 cards, legal under the 2004 list, every Fusion has its materials, effects scripted.
    /// Tournament mode: every roster deck exists, a bracket runs to a champion whether you win or lose, prizes draw.
    static void TournamentChecks()
    {
        var db = CardDb.Shared;
        Check(Tournament.Roster.All(d => db.RemasteredDeckNames().Contains(d.Deck)) && Tournament.Roster.All(d => db.ByName.ContainsKey(d.Card)), "tournament: every duelist's deck and signature card exist");
        foreach (bool win in new[] { true, false })
        {
            var t = new Tournament(new Rng(win ? 5u : 6u));
            Check(t.Seats.Length == 8 && t.Seats[0] == Tournament.You && t.Seats.Distinct().Count() == 8, "tournament: 8 distinct seats, you first");
            int rounds = 0;
            while (t.Foe is { } foe && rounds++ < 3) { t.PlayOthers(); t.Record(win); }
            Check(t.Round == 3 && t.Champion != null && (t.Champion == Tournament.You) == win, $"tournament ({(win ? "win all" : "lose first")}): a champion ({t.Champion}), rounds {rounds}");
            Check(t.Log.Count == 7, $"tournament: 7 matches logged ({t.Log.Count})");
        }
        var p = new Profile();
        { // game modes
          var packs = Modes.OpenPacks(new Rng(3)); Check(packs.Count == 6 && packs.All(k => k.Count == 9), "six packs of nine");
          for (ulong seed = 1; seed <= 40; seed++)
          {
              // themed packs: one Type or Attribute covers a good share of the monsters, and no card needs a named partner
              var cards = Modes.OpenPacks(new Rng(seed)).SelectMany(k => k).Select(CardDb.Shared.Get).ToList();
              var mons = cards.Where(c => c.IsMonster).ToList();
              int top = mons.GroupBy(c => c.Race).Concat(mons.GroupBy(c => c.Attribute)).Max(g => g.Count());
              Check(mons.Count == 30 && top >= 9 && cards.All(c => !c.Desc.Contains('"') && !c.IsFusion), $"pack pool {seed} is themed ({top} of {mons.Count} monsters share a Type / Attribute)");
          }
          var pp = Modes.PoolProfile(packs.SelectMany(k => k)); var pd = new Deck("pack");
          foreach (var i in packs.SelectMany(k => k).Distinct()) while (DeckRules.Add(pd, db.Get(i), pp)) { }
          Check(pd.Main.Count >= 40 && pd.Main.All(i => packs.Any(k => k.Contains(i))), $"a pack deck is built only from the packs ({pd.Main.Count})");
          var g = new Gauntlet(new[] { new OpponentPick("kaiba", "k", 6), new OpponentPick("joey", "j", 2) });
          Check(g.Current!.Rival == "joey", "gauntlet starts with the easiest rival");
          g.Record(true, 3100); Check(g.Lp == 3100 && g.Current!.Rival == "kaiba", "gauntlet carries LP to the next rival");
          g.Record(true, 500); Check(g.Cleared && g.Over, "gauntlet cleared");
          var ds = new DuelSetup { Lp = new[] { 2500, 8000 } }; var m = Bots.RandomSetup(new Rng(5), false); m.Lp = new[] { 2500, 8000 };
          var gd = new Duel(m); Check(gd.P(0).Lp == 2500 && gd.P(1).Lp == 8000, "per-player starting LP");
          { // Chaos Duel: rules appear on turn 3 and the duel still plays out; the same seed gives the same duel
            var rules = new HashSet<int>(); int done = 0;
            for (int k = 0; k < 6; k++)
            {
                var cs = Bots.RandomSetup(new Rng((ulong)(70 + k)), false); cs.Chaos = true;
                var cd = new Duel(cs); var bot = new Rng((ulong)(90 + k)); int steps = 0;
                while (!cd.Over && steps++ < 4000) { if (cd.Turn >= 3) rules.Add(cd.ChaosRule); cd.Submit(Bots.Decide(cd, bot)); }
                if (cd.Over) done++;
            }
            Check(done == 6 && rules.Count >= 2 && !rules.Contains(-1), $"chaos duels finish with rules in force ({string.Join(",", rules)})");
            var c1 = Bots.RandomSetup(new Rng(71), false); c1.Chaos = true; var c2 = Bots.RandomSetup(new Rng(71), false); c2.Chaos = true;
            var d1 = new Duel(c1); var d2 = new Duel(c2); var b1 = new Rng(5); var b2 = new Rng(5);
            for (int k = 0; k < 400 && !d1.Over; k++) { d1.Submit(Bots.Decide(d1, b1)); d2.Submit(Bots.Decide(d2, b2)); }
            Check(d1.StateHash() == d2.StateHash(), "chaos duels are deterministic (LAN-safe)"); }
          PuzzleChecks();
          Check(Modes.LimitedRules.All(r => r.Lp > 0) && Modes.Today(new DateTime(2026, 11, 8)) != null, "limited rule of the day"); }
        { var t1 = new Tournament(new Rng(7)); t1.PlayOthers(); t1.Record(false); Check(t1.Place == 3, "out in the quarterfinal = place 3");
          var t2 = new Tournament(new Rng(7)); for (int r = 0; r < 3; r++) { t2.PlayOthers(); t2.Record(true); } Check(t2.Place == 0 && Tournament.Ladder[0].Dp > Tournament.Ladder[1].Dp, "winning it all = champion, pays the most");
          var t3 = new Tournament(new Rng(7)); t3.PlayOthers(); t3.Record(true); Check(t3.Place == null, "still in after a win"); t3.PlayOthers(); t3.Record(false); Check(t3.Place == 2, "out in the semifinal = place 2"); }
        var got = Rewards.Roll(p, "tournament", Tournament.PrizePool(Tournament.Get("pegasus")), 5, true, new Rng(3), 2);
        Check(got.Count == 5 && got.All(g => g.New), "tournament: final prize draws 5 new cards");
    }

    static void RemasteredChecks()
    {
        var db = CardDb.Shared;
        var all = new Profile();
        foreach (var c in db.ByIdx.Values) all.Owned[c.Idx] = 3;
        var keys = db.RemasteredDeckNames().ToList();
        Check(new[] { "yugi", "kaiba", "joey", "jaden" }.All(r => Enumerable.Range(1, 7).All(l => keys.Contains($"{r}/x_lv{l}"))) && keys.Contains("yugi/x_exodia_insta") && keys.Contains("yugi/x_exodia_gy"), "rival decks: one per rival per level 1-7, plus Yugi's Exodia decks");
        foreach (var k in keys)
        {
            var (main, extra) = db.Deck(k);
            var deck = new Deck(k, main, extra);
            Check(main.Count == 40, $"{k}: 40 cards (got {main.Count})");
            Check(DeckRules.Problem(deck, all, "joey") is null, $"{k}: legal under 2004 ({DeckRules.Problem(deck, all, "joey")})");
            Check(DeckRules.FusionsMissingMaterials(deck).All(f => f.Modern), $"{k}: Fusion materials in the deck");   // CPU decks: the original fusion table only (newer Fusions are prompted in the deck builder)
            var unscripted = main.Select(db.Get).Where(c => db.Effects.ContainsKey(c.KonamiId) && Scripts.Get(c) is not { Implemented: true }).Select(c => c.Name).Distinct().ToList();
            Check(unscripted.Count == 0, $"{k}: every effect scripted ({string.Join(", ", unscripted)})");
            deck.Side.AddRange(db.DeckSide(k));
            Check(deck.Side.Count is > 0 and <= 15 && DeckRules.Problem(deck, all, "joey") is null, $"{k}: side deck legal ({DeckRules.Problem(deck, all, "joey")})");
            // The 2003 list forbids the Gods themselves (blank cards then); the CPU keeps its decks as-is under 2003 (ForCpu).
            if (k.StartsWith("god/"))
            {
                var noGods = new Deck(k, main.Where(i => db.Get(i).Race != "Divine-Beast"), extra);
                noGods.Side.AddRange(deck.Side);
                string? why = DeckRules.Problem(noGods, all, "kaiba");
                Check(why is null || why.Contains("more card"), $"{k}: legal under 2003 apart from the Gods ({why})");
                var over = deck.Main.Concat(deck.Side).Distinct().Select(db.Get).Where(c => new[] { "joey", "kaiba" }.Any(l => deck.CopiesNamed(c.Name) > DeckRules.Limit(c, l) && !(l == "kaiba" && c.Race == "Divine-Beast")))
                    .Select(c => $"{c.Name} x{deck.CopiesNamed(c.Name)} (2004 {DeckRules.Limit(c, "joey")}, 2003 {DeckRules.Limit(c, "kaiba")})").Distinct().ToList();
                Check(over.Count == 0 && DeckRules.ForCpu(main, "joey").SequenceEqual(main), $"{k}: within both limit lists ({string.Join("; ", over)})");
            }
        }
        // CPU decks under the ban list: 2004 makes them legal (same size), 2003 leaves them as the originals were.
        int fixedDecks = 0;
        foreach (var k in db.DeckNames())
        {
            var (m, _) = db.Deck(k);
            var legal = DeckRules.ForCpu(m, "joey");
            var d4 = new Deck(k, legal);
            var any = new Profile(); foreach (var i in legal) any.Owned[i] = 3;
            if (!legal.SequenceEqual(m)) fixedDecks++;
            Check(legal.Count == m.Count && (DeckRules.Problem(d4, any, "joey") is not { } x || x.Contains("more card")), $"{k}: legal under 2004 after ForCpu ({DeckRules.Problem(d4, any, "joey")})");
            Check(DeckRules.ForCpu(m, "kaiba").SequenceEqual(m), $"{k}: unchanged under 2003");
        }
        Console.WriteLine($"CPU decks changed for the 2004 list: {fixedDecks}");
        // CPU side-decking: against a trap-heavy deck with no big monsters, backrow removal comes in for anti-big cards,
        // and the Dark Magician engine never moves.
        var (ym, ye) = db.Deck("yugi/x_lv7");
        var yd = new Deck("y", ym, ye); yd.Side.AddRange(db.DeckSide("yugi/x_lv7"));
        var traps = new CpuSide.Seen { Traps = 8, Spells = 3, Big = 0, Monsters = 5, Duels = 1 };
        var sw = CpuSide.Swap(yd, traps);
        Check(sw.Count is > 0 and <= CpuSide.MaxSwaps && yd.Main.Count == 40 && sw.All(x => x.In.Name is "Mystical Space Typhoon" or "Dust Tornado" or "Heavy Storm" or "Royal Decree"),
            $"CPU side: trap-heavy opponent -> backrow removal in ({string.Join(", ", sw.Select(x => x.Out.Name + "->" + x.In.Name))})");
        Check(yd.Main.Count(i => db.Get(i).Name == "Dark Magician") == 3, "CPU side: engine cards stay");
        Check(CpuSide.Swap(new Deck("n", ym, ye) { }, new CpuSide.Seen()).Count == 0, "CPU side: nothing seen, nothing swapped");
    }

    /// LAN lockstep over real loopback sockets: each "PC" builds its duel from the decks and setup it received,
    /// its AI answers its own prompts and sends them, the other copy replays them; the hashes must agree after
    /// every command, through to the same winner.
    static void LockstepChecks(int n)
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(10));
        var hosting = LanLink.Host(cts.Token, 47890);
        using var guest = LanLink.Join("127.0.0.1", cts.Token, 47890).GetAwaiter().GetResult();
        using var host = hosting.GetAwaiter().GetResult();
        var links = new[] { host, guest };
        LanLink.Msg Read(LanLink l) => l.Read(cts.Token).GetAwaiter().GetResult() ?? throw new InvalidOperationException("link closed");
        int synced = 0, commands = 0;
        for (int i = 0; i < n; i++)
        {
            var s = Bots.RandomSetup(new Rng((ulong)(7700 + i)), chaos: i % 2 == 1);
            host.Send(new LanLink.Msg { T = "deck", Main = s.Main[0].ToArray(), Extra = s.Extra[0].ToArray() });
            guest.Send(new LanLink.Msg { T = "deck", Main = s.Main[1].ToArray(), Extra = s.Extra[1].ToArray() });
            host.Send(new LanLink.Msg { T = "setup", Seed = s.Seed, First = s.FirstPlayer });
            var (hd, gd) = (Read(guest), Read(host));
            var st = Read(guest);
            DuelSetup Make(int[] m0, int[] e0, int[] m1, int[] e1, ulong seed, int first)
            {
                var x = new DuelSetup { Seed = seed, FirstPlayer = first };
                x.Main[0].AddRange(m0); x.Extra[0].AddRange(e0); x.Main[1].AddRange(m1); x.Extra[1].AddRange(e1);
                return x;
            }
            var copies = new[]
            {
                new Duel(Make(s.Main[0].ToArray(), s.Extra[0].ToArray(), gd.Main!, gd.Extra!, s.Seed, s.FirstPlayer)),
                new Duel(Make(hd.Main!, hd.Extra!, s.Main[1].ToArray(), s.Extra[1].ToArray(), st.Seed, st.First)),
            };
            var ais = new[] { new Ai(0, (ulong)i * 2 + 1), new Ai(1, (ulong)i * 2 + 2) };
            bool ok = copies[0].Hash() == copies[1].Hash();
            for (int k = 0; ok && !copies[0].Over && k < 4000; k++)
            {
                int p = copies[0].Pending!.Player;
                var cmd = ais[p].Decide(copies[p]);
                copies[p].Submit(cmd);
                links[p].Send(new LanLink.Msg { T = "cmd", P = cmd.Player, Picks = cmd.Picks });
                var m = Read(links[1 - p]);
                copies[1 - p].Submit(new Command(m.P, m.Picks ?? Array.Empty<int>()));
                ok = copies[0].Hash() == copies[1].Hash();
                commands++;
            }
            ok &= copies[0].Winner == copies[1].Winner;
            Check(ok, $"lockstep {i}: both copies stay identical (turn {copies[0].Turn})");
            if (ok) synced++;
        }
        Console.WriteLine($"Lockstep: {synced}/{n} duels identical on both ends over TCP, {commands} commands sent");
    }

    /// Replays: a bot duel (some cut short like a surrender) saved to disk, loaded back and re-run reaches the same state.
    static void ReplayChecks(int n)
    {
        var dir = Path.Combine(Path.GetTempPath(), "chaosrift_replay_check");
        if (Directory.Exists(dir)) Directory.Delete(dir, true);
        for (int i = 0; i < n; i++)
        {
            var d = new Duel(Bots.RandomSetup(new Rng((ulong)(9100 + i)), chaos: i % 3 == 0));
            var ais = new[] { new Ai(0, (ulong)i + 5), new Ai(1, (ulong)i + 9) };
            int stop = i % 4 == 3 ? 60 : 4000;   // every 4th quits early
            for (int k = 0; !d.Over && k < stop; k++) d.Submit(ais[d.Pending!.Player].Decide(d));
            var r = Replay.Of(d, 0, d.Over ? d.Winner : 1, d.Over ? d.EndReason : "Surrender");
            r.You = "A"; r.Them = $"B{i}";
            var back = Replay.Load(r.Save(dir));
            Check(back != null && back.Log.Count == d.Log.Count && back.Plays(), $"replay {i}: saves, loads and replays ({d.Log.Count} commands)");
        }
        Check(Replay.List(dir).Count == n, "replay list sees every file");
        var bad = Replay.Load(Replay.List(dir)[0].Path)!;
        bad.Log.RemoveAt(bad.Log.Count / 2);
        Check(!bad.Plays(), "a broken replay is caught, not thrown");
        Directory.Delete(dir, true);
    }

    /// Your copy of the CPU's board value: a finished duel moves it a little, a CPU card use is recorded, and both
    /// survive save + load. A save made before CPU learning starts from the shipped model.
    static void BrainChecks()
    {
        var s = new SaveData.State();
        var p = s.Profile;
        var before = (double[])p.Brain.W.Clone();
        var d = new Duel(Bots.RandomSetup(new Rng(8300)));
        var ais = new[] { new Ai(0, 1, Brain.Plus), new Ai(1, 2, Brain.Plus) };
        for (int k = 0; k < 4000 && !d.Over; k++) d.Submit(ais[d.Pending!.Player].Decide(d));
        p.Brain = p.Brain.LearnFrom(d);
        p.Brain.Used("Mirror Force", 2500);
        double moved = p.Brain.W.Zip(before, (a, b) => Math.Abs(a - b)).Max();
        Check(d.Over && moved > 0 && moved < 0.2, $"brain: one duel nudges the weights (max change {moved:0.0000})");
        var json = SaveData.ToJson(s);
        var r = SaveData.FromJson(json).Profile;
        Check(r.Brain.W.SequenceEqual(Value.Trained.W) && r.Brain.Uses["Mirror Force"][1] == p.Brain.Uses["Mirror Force"][1], "brain: card payoffs saved and loaded, weights stay the shipped ones");
        var node = System.Text.Json.Nodes.JsonNode.Parse(json)!;
        node["Profile"]!.AsObject().Remove("Brain");
        var fresh = SaveData.FromJson(node.ToJsonString()).Profile;
        Check(fresh.Brain.W.SequenceEqual(Value.Trained.W), "brain: a save from before CPU learning starts from the shipped model");
    }

    /// Seer's re-deal: hidden cards move, nothing visible does, every card stays in exactly one place, play goes on.
    static void RedealChecks()
    {
        var db = CardDb.Shared;
        int moved = 0, ok = 0;
        for (int i = 0; i < 40; i++)
        {
            var rng = new Rng((ulong)(8100 + i));
            var setup = Bots.RandomSetup(rng);
            var d = new Duel(setup);
            var ais = new[] { new Ai(0, 1), new Ai(1, 2) };
            int stop = 10 + rng.Int(60);
            for (int k = 0; k < stop && !d.Over; k++) d.Submit(ais[d.Pending!.Player].Decide(d));
            if (d.Over) continue;
            var sim = d.Replay();
            string Visible(Duel x, int v) => string.Join(";", x.Cards.Where(c => c.Controller == v && c.Loc is Loc.Hand || c.FaceUp && c.OnField || c.Loc is Loc.Grave or Loc.Banished)
                .Select(c => $"{c.Uid}{c.Loc}{c.Zone}"));
            var before = Visible(sim, 1);
            var oppHidden = string.Join(",", sim.P(0).Hand.Concat(sim.P(0).Deck).Select(c => c.Uid));
            sim.Redeal(1, (ulong)i);
            bool places = sim.Cards.All(c => c.IsToken || c.Loc switch
            {
                Loc.Deck => sim.P(c.Owner).Deck.Count(x => x == c) == 1,
                Loc.Hand => sim.Players.Sum(p => p.Hand.Count(x => x == c)) == 1,
                Loc.Monster => sim.P(c.Controller).Monsters[c.Zone] == c,
                Loc.Spell => sim.P(c.Controller).Spells[c.Zone] == c,
                _ => true,
            });
            bool counts = sim.P(0).Hand.Count == d.P(0).Hand.Count && sim.P(0).Deck.Count == d.P(0).Deck.Count;
            Check(places && counts && Visible(sim, 1) == before, $"redeal {i}: cards stay in one place, counts and visible cards unchanged");
            if (string.Join(",", sim.P(0).Hand.Concat(sim.P(0).Deck).Select(c => c.Uid)) != oppHidden) moved++;
            try
            {
                var bots = new[] { new Ai(0, 3, Brain.Plus), new Ai(1, 4, Brain.Plus) };
                for (int k = 0; k < 300 && !sim.Over; k++) sim.Submit(bots[sim.Pending!.Player].Decide(sim));
                ok++;
            }
            catch (Exception e) { Check(false, $"redeal {i}: play continues ({e.InnerException?.Message ?? e.Message})"); }
        }
        Check(moved > 0, "redeal changes the hidden cards");
        Console.WriteLine($"Redeal: {ok} re-dealt duels played on, hidden cards changed in {moved}");
    }

    static void RewardChecks()
    {
        var rng = new Rng(5);
        Check(Rewards.Pool("yugi").Count == 155 && Rewards.Pool("kaiba").Count == 315 && Rewards.Pool("joey").Count == 350, "reward pool sizes 155/315/350");
        var p = new Profile();
        var r = Rewards.Roll(p, "yugi", 3, true, rng);
        Check(r.Count == 3 && r.Select(x => x.Card.Idx).Distinct().Count() == 3 && r.All(x => x.New && p.Count(x.Card.Idx) == 1 && (x.Card.PackMask & 1) != 0), "match reward: 3 distinct new yugi-pool cards");
        // 3-copy rule: with only two cards under 3 copies, single rewards always land on them.
        var pool = Rewards.Pool("kaiba");
        foreach (var d in pool) p.Owned[d.Idx] = 3;
        p.Owned[pool[7].Idx] = 1; p.Owned[pool[9].Idx] = 2;
        for (int i = 0; i < 50 && p.Count(pool[9].Idx) < 3; i++)
        {
            var one = Rewards.Roll(p, "kaiba", 1, false, rng)[0].Card.Idx;
            Check(one == pool[7].Idx || one == pool[9].Idx, "3-copy rule: reward only from cards owned < 3");
            if (p.Count(pool[7].Idx) >= 3 && p.Count(pool[9].Idx) >= 3) break;
        }
        foreach (var d in pool) p.Owned[d.Idx] = 255;
        Check(p.Count(Rewards.Roll(p, "kaiba", 1, false, rng)[0].Card.Idx) == 255, "owned count caps at 255 (all at 3+ falls back to the full pool)");
        // New first: with half the Yugi pool owned twice, a single reward is new far more often than the 50% of a uniform draw.
        var half = new Profile();
        var yp = Rewards.Pool("yugi");
        for (int i = 0; i < yp.Count; i += 2) half.Owned[yp[i].Idx] = 2;
        int fresh = 0;
        for (int i = 0; i < 400; i++)
        {
            var got = Rewards.Roll(half, "yugi", 1, false, rng)[0];
            if (got.New) { fresh++; half.Owned.Remove(got.Card.Idx); } else half.Owned[got.Card.Idx] = 2;
            half.DryRuns = 0;
        }
        Check(fresh > 400 * 85 / 100, $"new-first draw: a new card {fresh}/400 times with half the pool owned");
        // Pity: everything owned once but one card; after PityAfter dry rewards the next one is that card.
        var dry = new Profile();
        foreach (var d in yp) dry.Owned[d.Idx] = 1;
        dry.Owned.Remove(yp[5].Idx);
        dry.DryRuns = Rewards.PityAfter;
        var pity = Rewards.Roll(dry, "yugi", 1, false, rng)[0];
        Check(pity.New && pity.Card.Idx == yp[5].Idx && dry.DryRuns == 0, "pity: the reward after a dry run holds the one card still missing");
        // Difficulty-scaled rewards: EXPERT gives 3 (1 new) per duel and 6 (2 new) per match, even late in the collection.
        Check(Rewards.For(1, false) == (1, 0) && Rewards.For(4, false) == (2, 0) && Rewards.For(5, true) == (5, 0) && Rewards.For(7, false) == (3, 1) && Rewards.For(6, true) == (6, 2), "reward table by level");
        var late = new Profile();
        foreach (var d in yp) late.Owned[d.Idx] = 2;
        foreach (var d in yp.Take(10)) late.Owned.Remove(d.Idx);
        var ex = Rewards.Roll(late, "yugi", 6, true, rng, 2);
        Check(ex.Count == 6 && ex.Count(x => x.New) >= 2 && ex.Select(x => x.Card.Idx).Distinct().Count() == 6, "EXPERT match: 6 distinct cards, at least 2 new");
        var j = new Profile();
        for (int i = 0; i < 300; i++)
        {
            bool bonusBefore = j.JoeyBonus;
            var got = Rewards.Roll(j, "joey", i % 2 == 0 ? 1 : 3, i % 2 == 1, rng);
            Check(j.JoeyGauge is >= 0 and <= 19, "joey gauge stays 0..19");
            if (j.JoeyBonus && !bonusBefore) Check(j.JoeyGauge == 0 && !got.Any(x => x.New), "joey bonus only on a draw with no new card");
        }
    }

    static void SaveChecks()
    {
        var db = CardDb.Shared;
        var s = new SaveData.State();
        var p = s.Profile;
        var deck = DeckRules.GrantStarter(p, 2);
        var bewd = db.ByKonami[db.Named("Blue-Eyes White Dragon").KonamiId];
        foreach (var c in bewd) p.Owned[c.Idx] = 2;   // both printings
        p.Fresh.Add(bewd[^1].Idx);
        p.CollectionPercent = 37; p.CarriedOver = true; p.JoeyGauge = 12; p.JoeyBonus = true; p.DryRuns = 3;
        Progression.Record(p, "joey", true); Progression.Record(p, "kaiba", false); Progression.Record(p, "kaiba", false);
        p.Of("joey").Level = 4;
        deck.Cover = deck.Main[3];
        deck.Side.Add(bewd[^1].Idx);
        s.Decks.Add(new Deck("Empty")); s.Decks.Add(deck); s.Playing = 1;
        s.Options = new SaveData.Options { Name = "Seto", Picture = 5, Music = 0.25f, Sound = 0, Speed = 2, Fullscreen = false, BanList = "kaiba" };

        var json = SaveData.ToJson(s);
        var r = SaveData.FromJson(json);
        Check(SaveData.ToJson(r) == json, "save round-trips unchanged");
        Check(r.Profile.Owned.Count == p.Owned.Count && p.Owned.All(kv => r.Profile.Count(kv.Key) == kv.Value), "save: owned copies");
        Check(r.Profile.Fresh.SetEquals(p.Fresh) && r.Profile.JoeyGauge == 12 && r.Profile.JoeyBonus && r.Profile.CarriedOver && r.Profile.CollectionPercent == 37 && r.Profile.DryRuns == 3, "save: profile fields");
        var old = System.Text.RegularExpressions.Regex.Replace(json, @",\s*""DryRuns"": \d+", "");
        Check(!old.Contains("DryRuns") && SaveData.FromJson(old).Profile.DryRuns == 0, "save: a file from before DryRuns still loads");
        // Shop: DP, seen cards, bought cosmetics, what's equipped; a file from before the shop loads the defaults.
        var sp = new SaveData.State();
        sp.Profile.Dp = 5000; sp.Profile.Seen.Add(bewd[0].Idx);
        Check(Shop.Buy(sp.Profile, Shop.Mats.First(m => m.Key == "magma")) && Shop.Equip(sp.Profile, Shop.Mats.First(m => m.Key == "magma")), "shop: buy + equip a mat");
        Check(!Shop.Equip(sp.Profile, Shop.Backs.First(b => b.Key == "07_fire")), "shop: can't equip a back you don't own");
        sp.Profile.Frames.Add("fire"); sp.Profile.Frame = "fire";
        Check(Shop.Buy(sp.Profile, bewd[0]) && sp.Profile.Count(bewd[0].Idx) == 1 && sp.Profile.Dp == 5000 - 3000 - Shop.Price(bewd[0]), "shop: buy a seen card");
        sp.Profile.Dp += 10000;
        Check(Shop.Buy(sp.Profile, db.Named("Dark Magician")), "shop: unseen cards are for sale too");
        var sr = SaveData.FromJson(SaveData.ToJson(sp)).Profile;
        Check(sr.Dp == sp.Profile.Dp && sr.Seen.SetEquals(sp.Profile.Seen) && sr.Mats.Contains("magma") && sr.Mat == "magma" && sr.Back == Shop.DefaultBack && sr.Frame == "fire" && sr.Frames.Contains("fire"), "save: shop fields");
        var preShop = System.Text.RegularExpressions.Regex.Replace(json, @",\s*""(Dp|Seen|Mats|Backs|Frames|Mat|Back|Frame)"": (\[[^\]]*\]|""[^""]*""|\d+)", "");
        Check(!preShop.Contains("\"Mats\"") && SaveData.FromJson(preShop).Profile is { Dp: 0, Mat: Shop.DefaultMat, Back: Shop.DefaultBack, Frame: Shop.DefaultFrame } q && q.Seen.Count == 0, "save: a file from before the shop still loads");
        Check(r.Profile.Of("kaiba") is { LastWon: false, Streak: 2 } && r.Profile.Of("joey").Level == 4, "save: rival records");
        Check(r.Decks.Count == 2 && r.Decks[1].SameAs(deck) && r.Decks[1].Extra.SequenceEqual(deck.Extra) && r.Playing == 1, "save: decks + selected deck");
        Check(r.Options is { Name: "Seto", Picture: 5, Speed: 2, Fullscreen: false, BanList: "kaiba" } && r.Options.Music == 0.25f, "save: options");

        var dir = Path.Combine(Path.GetTempPath(), "poc_save_check");
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, "save.json");
        foreach (var f in Directory.GetFiles(dir)) File.Delete(f);
        Check(SaveData.Read(path, out bool broken) == null && !broken, "save: missing file = new player");
        SaveData.Write(path, s);
        Check(SaveData.Read(path, out broken) is { } back && !broken && SaveData.ToJson(back) == json && !File.Exists(path + ".tmp"), "save: write + read file");
        File.WriteAllText(path, json[..(json.Length / 2)]);
        Check(SaveData.Read(path, out broken) == null && broken && File.Exists(path + ".bad"), "save: broken file kept as .bad, starts fresh");
        File.WriteAllText(path, json.Replace("\"Version\": 1", "\"Version\": 9"));
        Check(SaveData.Read(path, out broken) == null && broken, "save: unknown version rejected");
    }

    static void DeckChecks()
    {
        var db = CardDb.Shared;
        for (int k = 0; k < DeckRules.Starters.Length; k++)
        {
            var p = new Profile();
            var d = DeckRules.GrantStarter(p, k);
            Check(DeckRules.IsLegal(d, p), $"starter {DeckRules.Starters[k].Name} is legal with only its own cards");
            Check(d.Main.Count >= DeckRules.MinMain && d.Main.All(i => DeckRules.Limit(db.Get(i)) > 0), "starter keeps 40+ cards without forbidden ones");
            var m = db.Get(d.Main[0]);
            DeckRules.Remove(d, m);
            Check(DeckRules.Add(d, m, p) && !DeckRules.Add(d, m, p), "can't add more copies than owned"); // starter owns exactly its deck
            while (d.Main.Count >= DeckRules.MinMain) d.Main.RemoveAt(0);
            Check(!DeckRules.IsLegal(d, p), "39-card main deck is illegal");
        }
        var q = new Profile();
        var bewd = db.Named("Blue-Eyes White Dragon");
        q.Owned[bewd.Idx] = 5;
        var e = new Deck("x");
        int n = 0; while (DeckRules.Add(e, bewd, q)) n++;
        Check(n == 3, "max 3 copies even when owning 5");
        var forbidden = db.ByIdx.Values.First(c => DeckRules.Limit(c) == 0);
        q.Owned[forbidden.Idx] = 3;
        Check(!DeckRules.Add(e, forbidden, q), $"forbidden card can't be added ({forbidden.Name})");
        var fusion = db.ByIdx.Values.First(c => c.IsFusion && DeckRules.Limit(c) == 3);
        q.Owned[fusion.Idx] = 1;
        Check(DeckRules.Add(e, fusion, q) && e.Extra.Contains(fusion.Idx) && !e.Main.Contains(fusion.Idx), "fusion monsters go to the Fusion Deck");

        // Alternate-art printings share the 3-copy limit.
        var alts = db.ByName["Blue-Eyes White Dragon"];
        var r = new Profile(); var a = new Deck("alt");
        foreach (var c in alts) r.Owned[c.Idx] = 2;
        int added = 0; foreach (var c in alts) { while (DeckRules.Add(a, c, r)) added++; }
        Check(alts.Count == 2 && added == 3, "both Blue-Eyes printings together max 3");

        // Fusion materials: Thousand Dragon = Baby Dragon + Time Wizard (+ Polymerization).
        var td = db.Named("Thousand Dragon");
        Check(string.Join(",", DeckRules.Materials(td).Select(c => c.Name)) == "Baby Dragon,Time Wizard,Polymerization", "Thousand Dragon materials");
        Check(DeckRules.Materials(db.Named("Blue-Eyes Ultimate Dragon")).Count(c => c.Name == "Blue-Eyes White Dragon") == 3, "Ultimate Dragon takes three Blue-Eyes");
        var f = new Deck("f"); var fp = new Profile();
        foreach (var c in DeckRules.Materials(td).Append(td)) fp.Owned[c.Idx] = 1;
        DeckRules.Add(f, td, fp);
        Check(DeckRules.FusionsMissingMaterials(f).Count == 1, "fusion without materials is flagged");
        // Quick-add for other dependent cards: Ritual monster <-> Ritual Spell, named cards in the text.
        Check(DeckRules.Needs(db.Named("Black Luster Soldier")).Any(c => c.Name == "Black Luster Ritual"), "Ritual monster needs its Ritual Spell");
        Check(DeckRules.Needs(db.Named("Black Luster Ritual")).Any(c => c.Name == "Black Luster Soldier"), "Ritual Spell needs its monster");
        Check(DeckRules.Needs(db.Named("Dark Magician Knight")).Any(c => c.Name == "Knight's Title"), "Dark Magician Knight needs Knight's Title");
        // Starter unlocks: the picked rival's deck never comes again; another rival's comes once.
        var up = new Profile();
        DeckRules.GrantStarter(up, 0);
        Check(DeckRules.UnlockStarter(up, "yugi") == null && DeckRules.UnlockStarter(up, "jaden") != null && DeckRules.UnlockStarter(up, "jaden") == null, "rival starter unlocks once, not the picked one");
        // Side Deck: max 15, shares the copy limit; main deck max 80.
        var sp = new Profile(); var sd = new Deck("s");
        foreach (var c in db.ByIdx.Values.Where(c => !c.IsFusion && DeckRules.Limit(c) == 3).Take(40)) sp.Owned[c.Idx] = 3;
        foreach (var i in sp.Owned.Keys.ToList()) for (int k = 0; k < 3; k++) DeckRules.Add(sd, db.Get(i), sp);
        Check(sd.Main.Count == DeckRules.MaxMain, "main deck stops at 80");
        var one = db.Get(sp.Owned.Keys.First());
        Check(!DeckRules.Add(sd, one, sp, side: true), "side copies count toward the 3-copy limit");
        DeckRules.Remove(sd, one);
        Check(DeckRules.Add(sd, one, sp, side: true) && sd.Side.Count == 1, "card goes to the Side Deck");
        Check(DeckRules.Move(sd, one, toSide: false) && sd.Side.Count == 0 && sd.Main.Count == 80, "move from side back to main");
        DeckRules.Remove(sd, one); DeckRules.Add(sd, one, sp, side: true);
        var outCard = db.Get(sd.Main[0]);
        Check(DeckRules.Swap(sd, outCard, one) && sd.Main.Count == 79 && sd.Side.Count == 1 && sd.Side[0] == outCard.Idx && sd.Main.Contains(one.Idx), "side swap is one-for-one");
        Check(!DeckRules.Swap(sd, db.Get(sd.Main[0]), td), "a Fusion only swaps with a Fusion");
        Check(DeckRules.AddMaterials(f, td, fp).Count == 0 && DeckRules.FusionsMissingMaterials(f).Count == 0 && f.Main.Count == 3, "materials added from the collection");

        // Fusion Deck holds as many as you like (no 3-card cap); copies of one Fusion still max 3.
        var fx = new Deck("fx"); var fxp = new Profile();
        var fusions = db.ByIdx.Values.Where(c => c.IsFusion && DeckRules.Limit(c) == 3 && db.Canon(c.Idx) == c.Idx).GroupBy(c => c.Name).Select(g => g.First()).Take(12).ToList();
        foreach (var c in fusions) { fxp.Owned[c.Idx] = 3; DeckRules.Add(fx, c, fxp); }
        Check(fx.Extra.Count == fusions.Count && fusions.Count == 12, "Fusion Deck takes 12 different Fusions");

        // Deck files: .ydc and .ydk round-trip; the shipped original deck.ydc files read back as the starter decks.
        var sd2 = DeckRules.GrantStarter(new Profile(), 1); sd2.Extra.AddRange(fusions.Select(c => c.Idx)); sd2.Side.Add(sd2.Main[0]);
        bool Same(Deck x, Deck y) => x.Main.Order().SequenceEqual(y.Main.Order()) && x.Extra.Order().SequenceEqual(y.Extra.Order()) && x.Side.Order().SequenceEqual(y.Side.Order());
        var (ydc, u1) = DeckFile.Read(DeckFile.ToYdc(sd2), "a");
        var (ydk, u2) = DeckFile.Read(System.Text.Encoding.UTF8.GetBytes(DeckFile.ToYdk(sd2)), "b");
        Check(u1 == 0 && Same(ydc, sd2), ".ydc export/import round-trips");
        Check(u2 == 0 && Same(ydk, sd2), ".ydk export/import round-trips");
        foreach (var (folder, key) in new[] { ("YUGI THE DESTINY", "yugi/deck_y"), ("KAIBA THE REVENGE", "kaiba/deck_k"), ("JOEY THE PASSION", "joey/deck_j") })
        {
            var path = Path.Combine(AppContext.BaseDirectory, "../../../../../Yu-Gi-Oh! Power of Chaos " + folder, "deck.ydc");
            if (!File.Exists(path)) continue;
            var (orig, u3) = DeckFile.Read(File.ReadAllBytes(path), folder);
            var (m, x) = db.Deck(key);
            Check(u3 == 0 && orig.Main.Count + orig.Extra.Count == m.Count + x.Count, $"original {folder} deck.ydc imports ({orig.Main.Count}+{orig.Extra.Count}, want {m.Count}+{x.Count})");
        }
    }

    /// Every Challenge is solvable by its known line (the rival answering with the AI), and doing nothing loses.
    static readonly Dictionary<string, string[]> Solutions = new()
    {
        ["c01"] = new[] { "Activate Shrink", "Pick Summoned Skull", "Activate Hinotama", "Battle", "Attack Summoned Skull", "Attack Celtic Guardian", "Attack Gemini Elf" },
        ["c02"] = new[] { "Activate Megamorph", "Pick Gemini Elf", "Activate Axe of Despair", "Pick Gemini Elf", "Activate Catapult Turtle", "Pick Gemini Elf" },
        ["c03"] = new[] { "Summon Jinzo", "Pick Celtic Guardian", "Battle", "Attack Jinzo>Gemini Elf", "Attack Battle Ox" },
        ["c04"] = new[] { "Activate Premature Burial", "Pick Blue-Eyes White Dragon", "Activate Megamorph", "Pick Blue-Eyes White Dragon", "Battle", "Attack Blue-Eyes White Dragon", "Attack Gemini Elf", "Attack Celtic Guardian" },
        ["c05"] = new[] { "Activate Shield & Sword", "Battle", "Attack Big Shield Gardna>Blue-Eyes White Dragon", "Attack Mystical Elf", "Attack Celtic Guardian" },
        ["c06"] = new[] { "Activate Scapegoat", "Activate United We Stand", "Pick Celtic Guardian", "Activate Axe of Despair", "Pick Celtic Guardian", "Battle", "Attack Celtic Guardian>Battle Ox" },
        ["c07"] = new[] { "Activate Change of Heart", "Pick Gemini Elf", "Activate Ookazi", "Activate Cannon Soldier", "Pick Gemini Elf", "Activate Cannon Soldier", "Pick Celtic Guardian", "Activate Cannon Soldier", "Pick Cannon Soldier" },
        ["c08"] = new[] { "Activate Change of Heart", "Pick Blue-Eyes White Dragon", "Activate Megamorph", "Pick Blue-Eyes White Dragon", "Activate Catapult Turtle", "Pick Blue-Eyes White Dragon" },
        // more than one turn: "End" ends your turn, "Attack X>*" = X attacks whatever it can (the first target, or directly)
        ["m01"] = new[] { "Activate Scapegoat", "Activate Cannon Soldier", "Pick Sheep Token", "Activate Cannon Soldier", "Pick Sheep Token", "Activate Cannon Soldier", "Pick Sheep Token", "Activate Cannon Soldier", "Pick Sheep Token", "Activate Cannon Soldier", "Pick Cannon Soldier", "End", "Activate Ookazi" },
        ["m02"] = new[] { "Activate Monster Reborn", "Pick Blue-Eyes White Dragon", "Activate Catapult Turtle", "Pick Blue-Eyes White Dragon", "End", "Activate Premature Burial", "Pick Blue-Eyes White Dragon", "Activate Catapult Turtle", "Pick Blue-Eyes White Dragon" },
        ["m03"] = new[] { "Activate Swords of Revealing Light", "Summon Battle Ox", "End", "Summon Summoned Skull", "Pick Battle Ox", "Activate Axe of Despair", "Pick Summoned Skull", "Battle", "Attack Summoned Skull>Blue-Eyes White Dragon", "End", "Summon Gemini Elf", "Battle", "Attack Summoned Skull>*", "Attack Gemini Elf>*" },
    };

    /// The tempting lines: each must lose against at least one answer of the rival.
    static readonly Dictionary<string, string[]> Fails = new()
    {
        ["c01"] = new[] { "Activate Hinotama", "Battle", "Attack Summoned Skull", "Attack Gemini Elf", "Attack Celtic Guardian" },
        ["c02"] = new[] { "Activate Axe of Despair", "Pick Gemini Elf", "Activate Catapult Turtle", "Pick Gemini Elf" },
        ["c03"] = new[] { "Activate Ookazi", "Battle", "Attack Battle Ox>Gemini Elf", "Attack Celtic Guardian" },
        ["c04"] = new[] { "Activate Monster Reborn", "Pick Blue-Eyes White Dragon", "Activate Megamorph", "Pick Blue-Eyes White Dragon", "Battle", "Attack Blue-Eyes White Dragon", "Attack Gemini Elf", "Attack Celtic Guardian" },
        ["c05"] = new[] { "Activate Shield & Sword", "Battle", "Attack Mystical Elf>Blue-Eyes White Dragon", "Attack Big Shield Gardna>Blue-Eyes White Dragon", "Attack Celtic Guardian" },
        ["c06"] = new[] { "Activate United We Stand", "Pick Celtic Guardian", "Activate Axe of Despair", "Pick Celtic Guardian", "Battle", "Attack Celtic Guardian>Battle Ox" },
        ["c07"] = new[] { "Activate Ookazi", "Activate Cannon Soldier", "Pick Celtic Guardian", "Activate Cannon Soldier", "Pick Cannon Soldier" },
        ["c08"] = new[] { "Activate Change of Heart", "Pick Blue-Eyes White Dragon", "Battle", "Attack Blue-Eyes White Dragon" },
        ["m01"] = new[] { "Activate Scapegoat", "Activate Cannon Soldier", "Pick Sheep Token", "Activate Cannon Soldier", "Pick Sheep Token", "Activate Cannon Soldier", "Pick Sheep Token", "Activate Cannon Soldier", "Pick Sheep Token", "End", "Activate Ookazi", "Activate Cannon Soldier", "Pick Cannon Soldier" },
        ["m02"] = new[] { "Activate Monster Reborn", "Pick Blue-Eyes White Dragon", "End", "Activate Catapult Turtle", "Pick Blue-Eyes White Dragon" },
        ["m03"] = new[] { "Activate Swords of Revealing Light", "Summon Battle Ox", "End", "Activate Axe of Despair", "Pick Battle Ox", "Battle", "Attack Battle Ox>Blue-Eyes White Dragon", "End", "Summon Gemini Elf", "Battle", "Attack Battle Ox>*", "Attack Gemini Elf>*" },
    };

    /// Play `steps` on puzzle z; the rival answers by `path` (index per decision with a real choice, 0 past its end).
    static (bool Won, List<int> Counts, string? Stuck) PlayPuzzle(Puzzles.Puzzle z, string[] steps, List<int> path)
    {
        var d = new Duel(Puzzles.Setup(z)); Puzzles.Prepare(d, z);
        var q = new Queue<string>(steps); var counts = new List<int>(); string? stuck = null;
        for (int guard = 0; !d.Over && guard < 400; guard++)
        {
            var p = d.Pending!;
            if (p.Player == 1)
            {
                if (p.Kind == Ask.Pick && p.Min > 1) { d.Submit(new Command(1, Enumerable.Range(0, p.Min).ToArray())); continue; }
                if (p.Options.Count <= 1) { d.Submit(new Command(1, new[] { 0 })); continue; }
                int k = counts.Count; counts.Add(p.Options.Count);
                d.Submit(new Command(1, new[] { k < path.Count ? path[k] : 0 }));
                continue;
            }
            int pick = -1;
            if (q.Count > 0)
            {
                var st = q.Peek(); var sp = st.Split(' ', 2); var (verb, rest) = (sp[0], sp.Length > 1 ? sp[1] : "");
                var (card, target) = rest.Contains('>') ? (rest.Split('>')[0], rest.Split('>')[1]) : (rest, (string?)null);
                pick = verb switch
                {
                    "Summon" => p.Kind == Ask.Main ? p.Options.FindIndex(o => o.Act == Act.Summon && o.Card?.Name == card) : -1,
                    "Activate" => p.Kind is Ask.Main or Ask.Battle or Ask.Respond ? p.Options.FindIndex(o => o.Act == Act.Activate && o.Card?.Name == card) : -1,
                    "Battle" => p.Kind == Ask.Main ? p.Options.FindIndex(o => o.Act == Act.ToBattle) : -1,
                    "Attack" => p.Kind == Ask.Battle ? p.Options.FindIndex(o => o.Act == Act.Attack && o.Card?.Name == card && (target == "*" || (target == null ? o.Target == null : o.Target?.Name == target))) : -1,
                    "End" => p.Kind is Ask.Main or Ask.Battle ? p.Options.FindIndex(o => o.Act == Act.EndTurn) : -1,
                    "Pick" => p.Kind == Ask.Pick ? p.Options.FindIndex(o => o.Card?.Name == card) : -1,
                    _ => -1,
                };
            }
            if (pick < 0 && q.Count > 0 && q.Peek() == "End" && p.Options.FindIndex(o => o.Act == Act.ToMain2) is >= 0 and var m2) { d.Submit(new Command(0, new[] { m2 })); continue; }   // the Battle Phase ends through Main Phase 2
            if (pick >= 0) { q.Dequeue(); d.Submit(new Command(0, new[] { pick })); continue; }
            if (p.Kind is Ask.Main or Ask.Battle && q.Count > 0) { stuck = $"'{q.Peek()}' at {p}"; break; }
            int i = p.Kind switch { Ask.Respond => p.Options.FindIndex(o => o.Act == Act.Pass), Ask.Main or Ask.Battle => p.Options.FindIndex(o => o.Act == Act.EndTurn), _ => 0 };
            d.Submit(new Command(0, p.Kind == Ask.Pick ? Enumerable.Range(0, Math.Max(1, p.Min)).ToArray() : new[] { Math.Max(0, i) }));
        }
        return (d.Over && d.Winner == 0, counts, stuck);
    }

    /// Every way the rival can answer `steps`: (runs, runs you won, first stuck message).
    static (int Runs, int Wins, string? Stuck) AllAnswers(Puzzles.Puzzle z, string[] steps)
    {
        var path = new List<int>(); int runs = 0, wins = 0; string? stuck = null;
        while (runs < 20000)
        {
            var (won, counts, st) = PlayPuzzle(z, steps, path);
            runs++; if (won) wins++; stuck ??= st;
            var full = Enumerable.Range(0, counts.Count).Select(k => k < path.Count ? path[k] : 0).ToList();
            int i = full.Count - 1;
            while (i >= 0 && full[i] + 1 >= counts[i]) i--;
            if (i < 0) break;
            path = full.Take(i).Append(full[i] + 1).ToList();
        }
        return (runs, wins, stuck);
    }

    static void PuzzleChecks()
    {
        Check(Puzzles.All.Select(z => z.Id).Distinct().Count() == Puzzles.All.Length && Puzzles.All.All(z => Solutions.ContainsKey(z.Id) && Fails.ContainsKey(z.Id)), "every puzzle has a solution and a tempting wrong line");
        foreach (var z in Puzzles.All)
        {
            var d = new Duel(Puzzles.Setup(z)); Puzzles.Prepare(d, z);
            Check(d.Turn == 3 && d.Pending is { Player: 0, Kind: Ask.Main } && d.P(1).Lp == z.TheirLp && d.P(0).Lp == z.YourLp && d.P(0).Hand.Count == z.Hand.Length, $"{z.Id} board laid");
            var (runs, wins, stuck) = AllAnswers(z, Solutions[z.Id]);
            Check(runs < 20000, $"{z.Id}: every rival answer walked ({runs} lines)");
            if (z.Turns > 1) Console.WriteLine($"puzzle {z.Id} ({z.Turns} turns): solution won {wins} of {runs} rival lines");
            Check(stuck == null && wins == runs, $"{z.Id} {z.Name}: the solution wins against every answer ({wins}/{runs}{(stuck != null ? ", stuck " + stuck : "")})");
            var (fr, fw, fs) = AllAnswers(z, Fails[z.Id]);
            Check(fw < fr, $"{z.Id}: the tempting line loses to some answer ({fw}/{fr} won{(fs != null ? ", stuck " + fs : "")})");
            var (er, ew, _) = AllAnswers(z, Array.Empty<string>());
            Check(ew == 0, $"{z.Id}: ending the turn loses");
        }
    }

    static void ProgressionChecks()
    {
        var decks = CardDb.Shared.DeckNames().Concat(CardDb.Shared.RemasteredDeckNames()).ToHashSet();
        int expertDecks = 0, godDecks = 0;
        var rng = new Rng(77);
        var levels = new HashSet<int>();
        for (int i = 0; i < 3000; i++)
        {
            var prof = new Profile { CollectionPercent = rng.Int(101), CarriedOver = rng.Int(2) == 0 };
            foreach (var r in new[] { "yugi", "kaiba", "joey", "jaden" })
            {
                var rec = prof.Of(r);
                rec.LastWon = rng.Int(2) == 0; rec.Streak = rng.Int(8); rec.Level = 1 + rng.Int(7);
                var pick = Progression.Pick(r, prof, rng);
                // one loss never drops the rival; a second loss in a row drops it one level at most (playtest 2026-11-12)
                if (r is "yugi" or "kaiba" && !rec.LastWon && rec.Streak > 0 && rec.Level >= 2 && !pick.DeckKey.StartsWith("god/") && !pick.DeckKey.Contains("exodia"))
                    Check(pick.Level >= Math.Min(r == "yugi" ? 6 : 7, rec.Level - (rec.Streak % 2 == 0 ? 1 : 0)), $"{r} after {rec.Streak} loss(es) at level {rec.Level}: level {pick.Level}");
                levels.Add(pick.Level);
                if (!decks.Contains(pick.DeckKey)) { Check(false, $"progression picked unknown deck {pick.DeckKey}"); return; }
                if (pick.DeckKey == $"{r}/x_lv{pick.Level}" || (r == "yugi" && pick.DeckKey.StartsWith("yugi/x_exodia") && pick.Level is 1 or 2)) expertDecks++;
                else if (Progression.GodDecks.Contains(pick.DeckKey) && pick.Level >= 5) godDecks++;
            }
        }
        Check(expertDecks + godDecks == 4 * 3000, "every pick brings the rival's deck for its level (Yugi: or an Exodia deck at EASY; HARD+: rarely a God deck)");
        Check(godDecks > 0 && godDecks < 4 * 3000 / 25, $"God decks are rare ({godDecks} of {4 * 3000} picks)");
        var hard = new Profile { CollectionPercent = 30 }; hard.Of("joey").Level = 6;
        var godRolls = Enumerable.Range(0, 4000).Select(_ => Progression.Pick("joey", hard, rng).DeckKey).Where(Progression.GodDecks.Contains).ToList();
        Check(godRolls.Count is > 120 and < 300 && Progression.GodDecks.All(godRolls.Contains), $"HARD+: about 1 duel in {Progression.GodOdds} brings a God deck, each of the four ({godRolls.Count}/4000)");
        Check(new Profile().Of("joey").Level == 1 && Enumerable.Range(0, 2000).All(_ => !Progression.GodDecks.Contains(Progression.Pick("joey", new Profile(), rng).DeckKey)), "EASY: never a God deck");
        var legendDays = Enumerable.Range(0, 366).Select(k => new DateTime(2026, 1, 1).AddDays(k)).ToList();
        int legendGods = legendDays.Count(day => Progression.GodDecks.Contains(Modes.Legend("kaiba", day).DeckKey));
        Check(legendGods is > 10 and < 60 && legendDays.All(day => Modes.Legend("kaiba", day) == Modes.Legend("kaiba", day)), $"Legend: a God deck on a rare day, the same all day ({legendGods}/366)");
        Check(levels.SetEquals(Enumerable.Range(1, 7)), $"difficulty levels 1..7 all reachable ({string.Join(",", levels.OrderBy(x => x))})");
        var p = new Profile();
        for (int i = 0; i < 3; i++) Progression.Record(p, "joey", true);
        Check(p.Of("joey").Level == 2, $"Joey ladder: 3 wins at level 1 -> level 2 (got {p.Of("joey").Level})");
        for (int i = 0; i < 3; i++) Progression.Record(p, "joey", false);
        Check(p.Of("joey").Level == 1, $"Joey ladder: 3 losses at level 2 -> level 1 (got {p.Of("joey").Level})");
        foreach (var r in new[] { "yugi", "kaiba" })
        {
            var q = new Profile { CollectionPercent = 30 };
            Progression.Record(q, r, true, 5);
            Check(Enumerable.Range(0, 200).All(_ => Progression.Pick(r, q, rng).Level >= 5), $"{r}: after beating HARD the next pick is HARD or harder");
            Progression.Record(q, r, true, 5); Progression.Record(q, r, true, 5);
            Check(Enumerable.Range(0, 200).All(_ => Progression.Pick(r, q, rng).Level >= 6), $"{r}: 3 wins in a row at HARD -> EXPERT");
            Progression.Record(q, r, false, 6);
            // playtest 2026-11-12: one loss keeps the level, the second in a row lowers it by one (no drop to EASY)
            Check(Enumerable.Range(0, 200).All(_ => Progression.Pick(r, q, rng).Level >= 6), $"{r}: one loss at EXPERT keeps EXPERT");
            Progression.Record(q, r, false, 6);
            Check(Enumerable.Range(0, 200).All(_ => Progression.Pick(r, q, rng).Level >= 5) && Enumerable.Range(0, 200).Any(_ => Progression.Pick(r, q, rng).Level < 6), $"{r}: two losses in a row -> one level down");
        }
    }

    static void FlowChecks()
    {
        // A duel with no decisions still reaches its first prompt and is reproducible.
        var setup = Bots.RandomSetup(new Rng(7));
        var d1 = new Duel(setup);
        var d2 = new Duel(setup);
        Check(d1.Pending != null && d1.StateHash() == d2.StateHash(), "same seed -> same opening state");
        Check(d1.P(0).Hand.Count == 6 && d1.P(1).Hand.Count == 5, "opening hands 5 + first player's turn-1 draw");
    }

    static void RuleChecks()
    {
        // Scripted mini-duel: decks of a single 1600-ATK normal monster vs. 1200.
        var a = CardDb.Shared.Named("Neo the Magic Swordsman").Idx;      // 1700
        var b = CardDb.Shared.Named("Mystical Elf").Idx;                  // 800/2000
        var setup = new DuelSetup { Seed = 3 };
        setup.Main[0].AddRange(Enumerable.Repeat(a, 40));
        setup.Main[1].AddRange(Enumerable.Repeat(b, 40));
        var d = new Duel(setup);
        // P0: summon Neo, end turn.
        Do(d, Act.Summon); Do(d, Act.EndTurn);
        Check(d.P(0).Monsters.Count(m => m != null) == 1, "Neo summoned");
        // P1: set Mystical Elf, end.
        Do(d, Act.Set); Do(d, Act.EndTurn);
        // P0: attack the set Elf: 1700 vs 2000 DEF -> 300 damage to P0, Elf flipped face-up.
        Do(d, Act.ToBattle); Do(d, Act.Attack);
        Check(d.P(0).Lp == 7700, $"attack into higher DEF deals 300 to attacker (LP {d.P(0).Lp})");
        var elf = d.P(1).MonstersOnField.Single();
        Check(elf.FaceUp, "face-down defender flipped by the attack");
        Check(!d.BattleOptions(0).Any(o => o.Act == Act.Attack), "one attack per monster");
        Do(d, Act.ToMain2); Do(d, Act.EndTurn);
        // P1: change Elf to attack and summon another, attack Neo: 800 vs 1700 -> 900 to P1.
        Do(d, Act.ChangePos);
        Do(d, Act.ToBattle);
        Do(d, Act.Attack);
        Check(d.P(1).Lp == 7100 && d.P(1).Grave.Count == 1, $"weaker attacker destroyed, 900 damage (LP {d.P(1).Lp})");
    }

    static void Do(Duel d, Act act)
    {
        var p = d.Pending!;
        int i = p.Options.FindIndex(o => o.Act == act);
        if (i < 0) throw new Exception($"No {act} in {p}");
        d.Submit(new Command(p.Player, new[] { i }));
        // Auto-pass any response windows the scripted test does not care about.
        while (d.Pending is { Kind: Ask.Respond } r) d.Submit(new Command(r.Player, new[] { r.Options.Count - 1 }));
    }

    /// The CPU AI (player 1) against the random bot and against itself: every duel must finish, and the AI
    /// should beat the random bot most of the time.
    static void AiDuels(int n)
    {
        int aiWins = 0, done = 0, mirrorDone = 0;
        for (int i = 0; i < n; i++)
        {
            foreach (bool mirror in new[] { false, true })
            {
                var rng = new Rng((ulong)(5000 + i));
                var setup = Bots.RandomSetup(rng);
                Duel? d = null;
                try
                {
                    d = new Duel(setup);
                    var cpu = new Ai(1, (ulong)i);
                    var other = new Ai(0, (ulong)i + 77);
                    var bot = new Rng((ulong)(7000 + i));
                    int steps = 0;
                    while (!d.Over && steps++ < 4000)
                        d.Submit(d.Pending!.Player == 1 ? cpu.Decide(d) : mirror ? other.Decide(d) : Bots.Decide(d, bot));
                    Check(d.Over, $"AI duel {i}{(mirror ? " (AI vs AI)" : "")} finished within 4000 decisions (turn {d.Turn})");
                    if (!d.Over) continue;
                    if (mirror) mirrorDone++;
                    else { done++; if (d.Winner == 1) aiWins++; }
                }
                catch (Exception e)
                {
                    failures++;
                    Console.WriteLine($"FAIL AI duel {i}{(mirror ? " (AI vs AI)" : "")} turn {d?.Turn}: {e.InnerException?.Message ?? e.Message}");
                    Console.WriteLine(e.InnerException?.StackTrace ?? e.StackTrace);
                }
            }
        }
        Console.WriteLine($"AI duels: vs random bot {done}/{n} finished, AI won {aiWins}; AI vs AI {mirrorDone}/{n} finished");
        Check(done == 0 || aiWins * 10 >= done * 7, $"AI beats the random bot at least 70% of the time ({aiWins}/{done})");
    }

    static void RandomDuels(int n)
    {
        int done = 0, totalCmds = 0, errors = 0;
        var wins = new int[3];
        for (int i = 0; i < n; i++)
        {
            var rng = new Rng((ulong)(1000 + i));
            var setup = Bots.RandomSetup(rng, chaos: i % 2 == 1);
            Duel? d = null;
            try
            {
                d = new Duel(setup);
                var bot = new Rng((ulong)(9000 + i));
                int steps = 0;
                while (!d.Over && steps++ < 4000) d.Submit(Bots.Decide(d, bot));
                Check(d.Over, $"duel {i} finished within 4000 decisions (turn {d.Turn})");
                Invariants(d, i);
                if (i % 25 == 0)
                {
                    var r = d.Replay();
                    Check(r.StateHash() == d.StateHash(), $"duel {i} replays to the same state");
                }
                wins[d.Winner + 1]++;
                totalCmds += d.Log.Count;
                done++;
            }
            catch (Exception e)
            {
                errors++;
                failures++;
                Console.WriteLine($"FAIL duel {i} (seed {setup.Seed}) turn {d?.Turn}: {e.InnerException?.Message ?? e.Message}");
                if (errors <= 3) Console.WriteLine(e.InnerException?.StackTrace ?? e.StackTrace);
            }
        }
        Console.WriteLine($"Random duels: {done}/{n} completed, avg {(done > 0 ? totalCmds / done : 0)} commands, P0 {wins[1]} P1 {wins[2]} draw {wins[0]}");
    }

    static void Invariants(Duel d, int i)
    {
        foreach (var p in d.Players)
        {
            var own = d.Cards.Where(c => c.Owner == p.Index && !c.IsToken).ToList();
            var placed = d.Players.SelectMany(q => q.Deck.Concat(q.Hand).Concat(q.Grave).Concat(q.Banished).Concat(q.Extra).Concat(q.FieldCards)).Where(c => c.Owner == p.Index && !c.IsToken).ToList();
            var dup = placed.GroupBy(c => c).Where(g => g.Count() > 1).Select(g => $"{g.Key} at {g.Key.Loc}");
            var lost = own.Except(placed).Select(c => $"{c} at {c.Loc}");
            Check(placed.Count == own.Count && placed.Distinct().Count() == own.Count, $"duel {i}: P{p.Index} cards conserved ({placed.Count}/{own.Count}) dup [{string.Join(", ", dup)}] lost [{string.Join(", ", lost)}]");
        }
        foreach (var c in d.Cards.Where(c => c.OnField))
            Check(c.Loc != Loc.Monster || d.P(c.Controller).Monsters[c.Zone] == c, $"duel {i}: {c} zone consistent");
    }
}
