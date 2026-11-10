namespace PoC.Engine;

/// Per-rival progress the original games kept (streak, Joey's level). Persisted by the game layer.
public sealed class RivalRecord
{
    public bool LastWon;        // last_result_kind == 1
    public int Streak;          // length of the current win/lose streak
    public int Level = 1;       // Joey's ladder level (1..7); Yugi/Kaiba: level of the last duel (remake ratchet)
}

public sealed class Profile
{
    public int CollectionPercent;           // owned / obtainable * 100
    public bool CarriedOver;                // owns cards from an earlier game (Kaiba pool 1, Joey FUL decks)
    public readonly Dictionary<string, RivalRecord> Rivals = new();
    public readonly Dictionary<int, int> Owned = new();   // card idx -> copies owned (cap 255)
    public readonly HashSet<int> Fresh = new();           // NEW flag on rewarded cards
    public int JoeyGauge;                                  // Joey's bonus gauge, 0..19
    public bool JoeyBonus;                                 // set when the gauge overflowed on a draw with no new card
    public int DryRuns;                                    // rewards in a row with no new card (remake pity rule)
    public int Dp;                                         // Duelist Points (Shop.cs)
    public readonly HashSet<string> Starters = new();      // rivals whose starter deck you have (picked, or won by beating them)
    public readonly HashSet<string> Solved = new();        // Challenges solved (puzzle ids)
    public readonly HashSet<int> Seen = new();            // cards a rival revealed against you: the shop sells these
    public readonly HashSet<string> Mats = new(), Backs = new(), Frames = new();   // bought cosmetics (free ones not listed)
    public string Mat = Shop.DefaultMat, Back = Shop.DefaultBack, Frame = Shop.DefaultFrame;  // equipped
    Value? brain; public string BrainFrom = "";   // the CPU's board value as trained by your duels (Value.cs), and the shipped model it grew from
    /// Your copy of the CPU's board value. A new shipped model (retrained) replaces a copy grown from an older one.
    public Value Brain
    {
        get { if (brain == null || BrainFrom != Value.ShippedId) { brain = new Value(Value.Trained.W, Value.Trained.Uses); BrainFrom = Value.ShippedId; } return brain; }
        set => brain = value;
    }
    public int Count(int idx) => Owned.GetValueOrDefault(idx);
    public RivalRecord Of(string rival) => Rivals.TryGetValue(rival, out var r) ? r : Rivals[rival] = new RivalRecord();
}

/// The opponent deck a duel against a rival will use, and its difficulty on one 1..7 scale.
public sealed record OpponentPick(string Rival, string DeckKey, int Level)
{
    public string Tier => Level switch { <= 2 => "EASY", <= 4 => "NORMAL", 5 => "HARD", _ => "EXPERT" };
}

/// Automatic opponent choice, as decoded from the three exes (remake/data/progression.json).
/// Yugi and Kaiba roll a deck from streak + collection; Joey uses a level ladder.
public static class Progression
{
    /// The difficulty level still comes from the original rules (streak, collection, Joey's ladder); the deck is the
    /// rival's remake deck for that level (user 2026-10-30: the original CPU decks are replaced; levels reflect card
    /// access and deck sophistication, the CPU brain is the same at every level).
    public static OpponentPick Pick(string rival, Profile p, Rng rng)
    {
        var pick = rival switch
        {
            "yugi" => Ratchet(Yugi(p, rng), p),
            "kaiba" => Ratchet(Kaiba(p, rng), p),
            "joey" => Joey(p, rng),
            "jaden" => new OpponentPick("jaden", "", Math.Clamp(p.Of("jaden").Level, 1, 7)),   // remake rival: Joey's ladder rules
            _ => throw new ArgumentException(rival),
        };
        // Yugi's Exodia decks (v1.0), a third of the time at EASY (their measured strength vs the starter decks, 7-20%):
        // level 1 the Exodia Necross deck, level 2 the instant-win one.
        if (rival == "yugi" && pick.Level is 1 or 2 && rng.Int(3) == 0) return pick with { DeckKey = pick.Level == 1 ? "yugi/x_exodia_gy" : "yugi/x_exodia_insta" };
        return pick with { DeckKey = $"{rival}/x_lv{pick.Level}" };
    }

    /// Remake rule (Yugi, Kaiba): the original re-rolls every duel, so beating a HARD deck often fell back to NORMAL.
    /// On a win streak the rival never drops below the level you last beat, and every 3rd win in a row goes one higher.
    static OpponentPick Ratchet(OpponentPick pick, Profile p)
    {
        var r = p.Of(pick.Rival);
        if (!r.LastWon || r.Level < 5) return pick;
        int top = pick.Rival == "yugi" ? 6 : 7;
        int floor = Math.Min(top, r.Level + (r.Streak % 3 == 0 ? 1 : 0));
        if (pick.Level >= floor) return pick;
        if (pick.Rival == "yugi") return pick with { DeckKey = floor == 5 ? "yugi/cpuk" : "yugi/cpukk", Level = floor };
        return pick with { DeckKey = $"kaiba/{(p.CarriedOver ? KaibaPool1 : KaibaPool0)[floor + 5]}", Level = floor };   // slots 10-12 = levels 5-7
    }

    // ---- Yugi (0x434d00): default cpu01..04; j/jj easier after losses, k/kk harder after wins.
    static OpponentPick Yugi(Profile p, Rng rng)
    {
        var r = p.Of("yugi");
        int c = p.CollectionPercent;
        int lose = r.LastWon ? 0 : r.Streak, win = r.LastWon ? r.Streak : 0;
        bool Roll(int n) => rng.Int(n) == 0;
        string? s = null;
        if (c < 45)
        {
            if (lose >= 2 && Roll(2)) s = "jj";
            else if (win > 2 && Roll(win > 4 ? 3 : 4)) s = "k";
        }
        else if (c < 60)
        {
            if (lose >= 2 && Roll(3)) s = rng.Int(2) == 0 ? "j" : "jj";
            else if (win > 2 && Roll(win > 4 ? 2 : 3)) s = rng.Int(2) == 0 ? "k" : "kk";
        }
        else if (c < 90)
        {
            if (lose >= 3 && Roll(3)) s = "j";
            else if (win > 1 && Roll(win > 3 ? 2 : 3)) s = rng.Int(2) == 0 ? "k" : "kk";
        }
        else if (Roll(3)) s = rng.Int(2) == 0 ? "k" : "kk";
        s ??= $"0{rng.Int(4) + 1}";
        int level = s switch { "jj" => 1, "j" => 2, "k" => 5, "kk" => 6, _ => 4 };
        return new OpponentPick("yugi", $"yugi/cpu{s}", level);
    }

    // ---- Kaiba (0x438210): 13 slots, 0 J .. 1 JJ (easy), 2..9 normal, 10 K, 11 KK, 12 KKK.
    static readonly string[] KaibaPool0 = { "kaiba_j", "kaiba_jj", "kaiba_gensaku", "kaiba_equip", "kaiba_hand", "kaiba_fusion", "kaiba_deck", "kaiba_blueeyes", "kaiba_inifu", "kaiba_dragon", "kaiba_k", "kaiba_kk", "kaiba_kkk" };
    static readonly string[] KaibaPool1 = { "kaiba_deck", "kaiba_inifu", "yk_bokori", "yk_kongo", "yk_counter", "kaiba_fusion", "kaiba_blueeyes", "kaiba_equip", "kaiba_kkk", "kaiba_kk", "yk_k", "yk_kk", "yk_kkk" };

    static OpponentPick Kaiba(Profile p, Rng rng)
    {
        var r = p.Of("kaiba");
        int c = p.CollectionPercent;
        int lose = r.LastWon ? 0 : r.Streak, win = r.LastWon ? r.Streak : 0;
        bool Roll(int n) => rng.Int(n) == 0;
        int Either(int a, int b) => rng.Int(2) == 0 ? a : b;
        int slot = -1;
        if (c < 40)
        {
            if (lose >= 2 && Roll(2)) slot = 1;
            else if (win > 5 && Roll(3)) slot = 10;
            else if (win > 3 && Roll(4)) slot = 10;
        }
        else if (c < 65)
        {
            if (lose >= 3 && Roll(3)) slot = Either(0, 1);
            else if (win > 5 && Roll(2)) slot = Either(11, 12);
            else if (win > 3 && Roll(3)) slot = Either(10, 11);
        }
        else if (c < 85)
        {
            if (lose >= 3 && Roll(3)) slot = 1;
            else if (win > 4 && Roll(2)) slot = Either(11, 12);
            else if (win > 2 && Roll(3)) slot = Either(11, 12);
        }
        else slot = 2 + rng.Int(p.CarriedOver ? 11 : 10);
        if (slot < 0) slot = 2 + rng.Int(8);
        var pool = p.CarriedOver ? KaibaPool1 : KaibaPool0;
        int level = slot switch { 0 => 1, 1 => 2, 10 => 5, 11 => 6, 12 => 7, _ => 4 };
        return new OpponentPick("kaiba", $"kaiba/{pool[slot]}", level);
    }

    // ---- Joey (0x44bbe0, ladder 0x446df0): CPU_nnn per level, FUL_* when cards were carried over.
    // Remake order: the original table ({0-4}, {3,4,10-12}, {10-14}, {13-15,20,21}, {20-24}, {23-27}, {25-29}) got
    // no harder (level-1 decks beat a starter deck ~70% of the time, some level-7 decks ~45%). Same decks, same
    // overlapping windows of 5, sorted by how often each beats a starter deck (Engine.Check `ladder`, 2004 list).
    static readonly int[][] JoeyTable =
    {
        new[] { 24, 10, 26, 29, 2 }, new[] { 29, 2, 13, 22, 12 }, new[] { 13, 22, 12, 25, 23 }, new[] { 25, 23, 27, 4, 14 },
        new[] { 4, 14, 1, 3, 20 }, new[] { 3, 20, 0, 15, 11 }, new[] { 0, 15, 11, 21, 28 },
    };
    static readonly string[][] JoeyFul =
    {
        new[] { "0ge1", "0umi", "0val" }, new[] { "1dec", "1red", "1too", "1ins", "1uij", "1umi", "1val" },
        new[] { "1fus", "1gam", "1ge1", "1ge2", "1hik", "1mix" }, new[] { "2dec", "2val", "2too", "2ge1", "2hik", "2ins", "2uij" },
        new[] { "2fus", "2gam", "2ge2", "2mix", "2red", "2umi" }, new[] { "3all", "3dec", "3yam", "3fus", "3too", "3hnd" },
        new[] { "3all", "3dec", "3exo", "3fus", "3grv", "3hnd" },
    };

    static OpponentPick Joey(Profile p, Rng rng)
    {
        int level = Math.Clamp(p.Of("joey").Level, 1, 7);
        string key;
        if (p.CarriedOver)
        {
            var row = JoeyFul[Math.Clamp(level + 2, 3, 9) - 3];
            key = $"joey/ful_{row[rng.Int(row.Length)]}";
        }
        else key = $"joey/cpu_{JoeyTable[level - 1][rng.Int(5)]:000}";
        return new OpponentPick("joey", key, level);
    }

    static readonly int[] Up = { 0, 3, 4, 5, 5, 5, 6, 6, 7 };
    static readonly int[] Down = { 0, 0, 3, 4, 4, 4, 4, 3, 2, 2 };

    /// Record a duel result against a rival (streak; Joey's level ladder).
    public static void Record(Profile p, string rival, bool won, int level = 0)
    {
        var r = p.Of(rival);
        if (r.LastWon == won && r.Streak > 0) r.Streak = Math.Min(255, r.Streak + 1); else r.Streak = 1;
        r.LastWon = won;
        if (rival is not ("joey" or "jaden")) { if (level > 0) r.Level = level; return; }
        int co = p.CarriedOver && rival == "joey" ? 1 : 0;
        int u = Math.Max(1, r.Level + 2 * co);
        if (won && u < (co == 1 ? 9 : 7) && u < Up.Length && r.Streak >= Up[u]) { r.Level++; r.Streak = 0; }
        else if (!won && u > 2 * co + 1 && u < Down.Length && r.Streak >= Down[u]) { r.Level--; r.Streak = 0; }
        r.Level = Math.Clamp(r.Level, 1, 7);
    }
}
