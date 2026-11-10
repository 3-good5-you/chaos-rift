namespace PoC.Engine;

/// A tournament duelist: who they are, their deck (remastered_decks.json), the difficulty shown (from the deck's measured
/// win rate vs the starter decks, `rivals` mode: 80%+ = 6 ... under 40% = 2), and their signature card.
public sealed record Duelist(string Key, string Name, string Deck, int Level, string Card, string Color);

/// Tournament mode (v1.0): 8 duelists in a single-elimination bracket, you plus 7 drawn from the roster. Your matches
/// are played on the duel field; the others are played out by the engine (fast CPU) when a round starts.
/// Kept in memory only: quitting the game mid-tournament ends it (like a match).
public sealed class Tournament
{
    public static readonly Duelist[] Roster =
    {
        new("yugi", "YUGI", "yugi/x_lv6", 6, "Dark Magician", "7a2fb3"),
        new("kaiba", "KAIBA", "kaiba/x_lv6", 6, "Blue-Eyes White Dragon", "2669d9"),
        new("joey", "JOEY", "joey/x_lv6", 6, "Red-Eyes B. Dragon", "d94026"),
        new("jaden", "JADEN", "jaden/x_lv5", 5, "Elemental HERO Flame Wingman", "e68c1a"),
        new("mai", "MAI", "tour/mai", 5, "Harpie Lady Sisters", "c0469a"),
        new("pegasus", "PEGASUS", "tour/pegasus", 2, "Thousand-Eyes Restrict", "b81f3c"),
        new("marik", "MARIK", "tour/marik", 6, "Lava Golem", "8a4fd1"),
        new("bakura", "BAKURA", "tour/bakura", 5, "Dark Necrofear", "5b6a7d"),
        new("ishizu", "ISHIZU", "tour/ishizu", 5, "Guardian Sphinx", "d9b44a"),
        new("odion", "ODION", "tour/odion", 2, "Embodiment of Apophis", "a1773a"),
        new("weevil", "WEEVIL", "tour/weevil", 3, "Insect Queen", "4c9a3a"),
        new("rex", "REX", "tour/rex", 6, "Two-Headed King Rex", "9a5a2a"),
        new("mako", "MAKO", "tour/mako", 4, "Fortress Whale", "2a8ab8"),
        new("keith", "BANDIT KEITH", "tour/keith", 6, "Barrel Dragon", "6b6b6b"),
        new("zane", "ZANE", "tour/zane", 4, "Cyber End Dragon", "3aa7c9"),
        new("chazz", "CHAZZ", "tour/chazz", 6, "Armed Dragon LV7", "2f2f3a"),
        new("alexis", "ALEXIS", "tour/alexis", 4, "Cyber Blader", "d6a3c9"),
        new("bastion", "BASTION", "tour/bastion", 6, "Water Dragon", "3a6fd6"),
        new("syrus", "SYRUS", "tour/syrus", 5, "Super Vehicroid Jumbo Drill", "5fa3e0"),
        new("aster", "ASTER", "tour/aster", 3, "Destiny HERO - Dogma", "c9c9d6"),
        new("crowler", "CROWLER", "tour/crowler", 4, "Ancient Gear Golem", "8f7a4a"),
    };

    public const string You = "you";
    public const int Size = 8;

    /// Seats in bracket order (seat 0 = you). Match m of round r pairs seats Of(r, m); winners move up.
    public readonly string[] Seats;
    public readonly List<string?[]> Winners = new() { new string?[4], new string?[2], new string?[1] };
    public readonly List<string> Log = new();   // "MARIK def. MAI (LP 4300)" lines, latest last
    public int Round;          // 0 quarterfinal, 1 semifinal, 2 final, 3 = finished
    public bool YouOut;
    readonly Rng rng;

    public Tournament(Rng rng, IEnumerable<string>? pick = null)
    {
        this.rng = rng;
        var pool = (pick ?? Roster.Select(d => d.Key)).ToList();
        var seats = new List<string> { You };
        while (seats.Count < Size && pool.Count > 0) { int i = rng.Int(pool.Count); seats.Add(pool[i]); pool.RemoveAt(i); }
        Seats = seats.ToArray();
        // Rare God decks: a HARD-or-better duelist brings an Egyptian God deck 1 tournament in 20 (rolled once, here).
        foreach (var k in Seats.Skip(1)) if (Get(k).Level >= 5 && rng.Int(Progression.GodOdds) == 0) gods[k] = Progression.GodDeck(rng);
    }

    readonly Dictionary<string, string> gods = new();
    /// A seat's duelist with the deck they brought to this tournament.
    public Duelist Seat(string key) => gods.TryGetValue(key, out var g) ? Get(key) with { Deck = g } : Get(key);

    public static Duelist Get(string key) => Roster.First(d => d.Key == key);
    public static readonly string[] RoundNames = { "QUARTERFINAL", "SEMIFINAL", "FINAL" };

    /// The two duelists of match m in round r (null while an earlier match is undecided).
    public (string? A, string? B) Pair(int r, int m) => r == 0 ? (Seats[2 * m], Seats[2 * m + 1]) : (Winners[r - 1][2 * m], Winners[r - 1][2 * m + 1]);

    /// Your opponent this round, or null when you're out or it's over.
    public Duelist? Foe
    {
        get
        {
            if (YouOut || Round > 2) return null;
            for (int m = 0; m < Winners[Round].Length; m++)
            {
                var (a, b) = Pair(Round, m);
                if (a == You && b != null) return Seat(b);
                if (b == You && a != null) return Seat(a);
            }
            return null;
        }
    }

    /// Play every other match of the current round on the engine (both sides CPU). `quick` = no look-ahead
    /// (fast enough to run while the bracket animates); ties are re-dueled up to twice, then decided by the higher level.
    public void PlayOthers(bool quick = true)
    {
        for (int m = 0; m < Winners[Round].Length; m++)
        {
            var (a, b) = Pair(Round, m);
            if (a == null || b == null || a == You || b == You || Winners[Round][m] != null) continue;
            Winners[Round][m] = Duel(Seat(a), Seat(b), quick);
        }
    }

    string Duel(Duelist a, Duelist b, bool quick)
    {
        var db = CardDb.Shared;
        for (int attempt = 0; attempt < 3; attempt++)
        {
            var s = new DuelSetup { Seed = rng.Next(), FirstPlayer = rng.Int(2), Classic = false };
            var (ma, ea) = db.Deck(a.Deck); var (mb, eb) = db.Deck(b.Deck);
            s.Main[0].AddRange(DeckRules.ForCpu(ma)); s.Extra[0].AddRange(ea);
            s.Main[1].AddRange(DeckRules.ForCpu(mb)); s.Extra[1].AddRange(eb);
            var d = new Duel(s);
            var ai = new[] { new Ai(0, rng.Next(), Brain.Plus) { JudgeWorlds = quick ? 0 : 1 }, new Ai(1, rng.Next(), Brain.Plus) { JudgeWorlds = quick ? 0 : 1 } };
            try { for (int i = 0; i < 3000 && !d.Over; i++) d.Submit(ai[d.Pending!.Player].Decide(d)); }
            catch (Exception) { continue; }
            if (d.Winner is 0 or 1)
            {
                var (w, l) = d.Winner == 0 ? (a, b) : (b, a);
                Log.Add($"{w.Name} def. {l.Name} (LP {Math.Max(0, d.P(d.Winner).Lp)})");
                return w.Key;
            }
        }
        var win = a.Level > b.Level || (a.Level == b.Level && rng.Coin()) ? a : b;
        Log.Add($"{win.Name} def. {(win == a ? b : a).Name} (time limit)");
        return win.Key;
    }

    /// Your duel this round ended (call after PlayOthers). Moves the bracket on; when you're knocked out the rest is
    /// played out at once so the bracket shows a champion.
    public void Record(bool youWon)
    {
        for (int m = 0; m < Winners[Round].Length; m++)
        {
            var (a, b) = Pair(Round, m);
            if (a != You && b != You) continue;
            var foe = a == You ? b! : a!;
            Winners[Round][m] = youWon ? You : foe;
            Log.Add(youWon ? $"YOU def. {Get(foe).Name}" : $"{Get(foe).Name} def. YOU");
        }
        if (!youWon) { YouOut = true; LostRound = Round; }
        Round++;
        while (YouOut && Round <= 2) { PlayOthers(); Round++; }
    }

    public string? Champion => Winners[2][0];

    /// Prizes by where you finish (2026-11-08: paid once, when you go out or win it; winning it all pays big).
    /// Index = place: 0 champion, 1 runner-up, 2 semifinal, 3 quarterfinal. The champion also gets the finalist's ace.
    public static readonly (int Dp, int Cards, int New)[] Ladder = { (1500, 6, 3), (600, 4, 1), (250, 2, 0), (50, 0, 0) };
    public static readonly string[] PlaceNames = { "CHAMPION", "RUNNER-UP", "SEMIFINAL", "QUARTERFINAL" };

    /// The round you lost (-1 = not out).
    public int LostRound = -1;

    /// Where you finished, once your tournament is over; null while you're still in it.
    public int? Place => Champion == You ? 0 : YouOut ? LostRound switch { 0 => 3, 1 => 2, _ => 1 } : null;

    /// Cards a tournament win can give: the beaten duelist's deck cards (collectable) plus the whole expansion.
    public static List<CardData> PrizePool(Duelist foe)
    {
        var db = CardDb.Shared;
        var (main, extra) = db.Deck(foe.Deck);
        var mine = main.Concat(extra).Select(i => db.Get(db.Canon(i))).Where(c => c.PackMask != 0).Distinct();
        return mine.Concat(Rewards.Pool("expansion")).Distinct().ToList();
    }
}
