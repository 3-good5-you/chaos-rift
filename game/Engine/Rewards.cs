namespace PoC.Engine;

/// Card rewards as decoded from the three exes (remake/data/progression.json → rewards), plus remake rules:
/// - cards already owned 3 times (the deck limit) are left out of the draw while other cards remain;
/// - new first: the draw is weighted by copies owned (none 20, one 3, two 1) instead of uniform;
/// - pity: after PityAfter rewards in a row with no new card, the next reward holds one (while any is left);
/// - rewards scale with the opponent's difficulty level (For), EXPERT also guarantees new cards.
public static class Rewards
{
    public const int UsefulCopies = 3;
    public const int PityAfter = 4;

    /// Cards won and how many of them must be new, by difficulty level (OpponentPick.Level 1..7).
    /// Single duel: EASY 1, NORMAL 2, HARD 2, EXPERT 3 (1 new). Match: 3 / 4 / 5 / 6 (2 new).
    public static (int Count, int New) For(int level, bool match)
    {
        int tier = level <= 2 ? 0 : level <= 4 ? 1 : level == 5 ? 2 : 3;
        return match ? (3 + tier, tier == 3 ? 2 : 0) : (new[] { 1, 2, 2, 3 }[tier], tier == 3 ? 1 : 0);
    }

    static int Weight(int copies) => copies switch { 0 => 20, 1 => 3, _ => 1 };

    static int Bit(string rival) => rival switch { "yugi" => 1, "kaiba" => 2, "joey" => 4, "jaden" => CardDb.JadenBit, _ => CardDb.Expansion };

    /// Cards a rival's rewards can give (card_pack.bin bit for that game).
    public static List<CardData> Pool(string rival) =>
        CardDb.Shared.ByIdx.Values.Where(d => (d.PackMask & Bit(rival)) != 0).OrderBy(d => d.Idx).ToList();

    /// Draw `count` distinct cards from the rival's pool and grant them. Each result says whether the card
    /// was new (not owned before).
    public static List<(CardData Card, bool New)> Roll(Profile p, string rival, int count, bool match, Rng rng, int mustBeNew = 0) =>
        Roll(p, rival, Pool(rival), count, match, rng, mustBeNew);

    /// The same draw from any card pool (Tournament prizes: the beaten duelist's cards plus the expansion).
    public static List<(CardData Card, bool New)> Roll(Profile p, string rival, List<CardData> pool, int count, bool match, Rng rng, int mustBeNew = 0)
    {
        var open = pool.Where(d => p.Count(d.Idx) < UsefulCopies).ToList();
        if (open.Count >= count) pool = open;

        // A draw with no unowned card is redrawn while attempts remain (more attempts late in the collection).
        int c = p.CollectionPercent;
        int attempts = 1 + (c >= 80 ? 1 : 0) + (rival != "yugi" && c >= 90 ? 1 : 0);
        var draw = new List<CardData>();
        for (int a = 0; a < attempts; a++)
        {
            draw = Distinct(pool, count, p, rng);
            if (draw.Any(d => p.Count(d.Idx) == 0)) break;
        }
        // Guaranteed new cards (EXPERT, or pity after a dry run) replace owned ones from the end, while any are left.
        int need = Math.Min(draw.Count, Math.Max(mustBeNew, p.DryRuns >= PityAfter ? 1 : 0));
        var unowned = pool.Where(d => p.Count(d.Idx) == 0 && !draw.Contains(d)).ToList();
        for (int k = draw.Count - 1; k >= 0 && draw.Count(d => p.Count(d.Idx) == 0) < need && unowned.Count > 0; k--)
        {
            if (p.Count(draw[k].Idx) == 0) continue;
            int u = rng.Int(unowned.Count);
            draw[k] = unowned[u];
            unowned.RemoveAt(u);
        }
        p.DryRuns = draw.Any(d => p.Count(d.Idx) == 0) ? 0 : p.DryRuns + 1;

        var result = draw.Select(d => (d, p.Count(d.Idx) == 0)).ToList();
        foreach (var d in draw)
        {
            p.Owned[d.Idx] = Math.Min(255, p.Count(d.Idx) + 1);
            p.Fresh.Add(d.Idx);
        }
        var all = CardDb.Shared.ByIdx.Values.Where(d => (d.PackMask & 7) != 0).ToList();   // the original PoC collection (rival choice reads it)
        p.CollectionPercent = all.Count(d => p.Count(d.Idx) > 0) * 100 / all.Count;
        if (rival == "joey") Gauge(p, result.Any(r => r.Item2), match, rng);
        return result;
    }

    /// Joey's bonus gauge. What the bonus flag grants was never traced: it is tracked, nothing more.
    static void Gauge(Profile p, bool anyNew, bool match, Rng rng)
    {
        // ponytail: "while the flag is set the next reward resets the gauge to 0" read as one reset that clears the flag.
        if (p.JoeyBonus) { p.JoeyBonus = false; p.JoeyGauge = 0; return; }
        p.JoeyGauge += match ? rng.Int(8) + 3 : rng.Int(10) + 1;
        if (p.JoeyGauge <= 19) return;
        if (anyNew) p.JoeyGauge = 17;
        else { p.JoeyGauge = 0; p.JoeyBonus = true; }
    }

    /// `count` distinct cards, each drawn by weight (copies owned) from what is left.
    static List<CardData> Distinct(List<CardData> pool, int count, Profile p, Rng rng)
    {
        var left = pool.ToList();
        var pick = new List<CardData>();
        while (pick.Count < count && left.Count > 0)
        {
            int total = left.Sum(d => Weight(p.Count(d.Idx))), r = rng.Int(total), k = 0;
            while ((r -= Weight(p.Count(left[k].Idx))) >= 0) k++;
            pick.Add(left[k]);
            left.RemoveAt(k);
        }
        return pick;
    }
}
