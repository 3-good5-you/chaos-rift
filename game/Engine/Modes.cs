namespace PoC.Engine;

/// The duel modes of the DUEL tab (2026-11-08). Regular = rival + single duel or match (the original game).
public enum GameMode { Regular, Gauntlet, Chaos, Challenges, Pack, Limited, Legend }

public static class Modes
{
    public sealed record Info(GameMode Mode, string Name, string Line, string Reward, bool Ready);

    public static readonly Info[] All =
    {
        new(GameMode.Regular, "REGULAR", "Choose a rival. A single duel or a best-of-three match.", "Cards and DP by the rival's level", true),
        new(GameMode.Gauntlet, "GAUNTLET", "All four rivals in a row with one deck. Your LP carries over.", "Clear it: 1,000 DP + 4 cards", true),
        new(GameMode.Pack, "PACK DUEL", "Open six packs and build a 40-card deck from them alone.", "Keep 3 cards from your packs", true),
        new(GameMode.Limited, "LIMITED DUEL", "Today's rule decides what your deck may hold.", "Normal rewards + 150 DP", true),
        new(GameMode.Legend, "LEGEND", "A rival at full strength with their strongest deck.", "600 DP + 3 cards (2 new)", true),
        new(GameMode.Chaos, "CHAOS DUEL", "Every third turn the rift sets a new rule for both players.", "Normal rewards + 200 DP", true),
        new(GameMode.Challenges, "CHALLENGES", "A set board: win this turn with the cards you are given.", "200 DP for each one solved", true),
    };

    public static Info Of(GameMode m) => All.First(i => i.Mode == m);

    // ---------------------------------------------------------------- Legend

    /// The rival at the top level with their strongest remake deck; on a rare day (1 in 12, per rival, the same all
    /// day so every screen agrees) an Egyptian God deck instead.
    public static OpponentPick Legend(string rival, DateTime? day = null)
    {
        var d = day ?? DateTime.Today;
        int roll = d.Year * 400 + d.DayOfYear + rival.Sum(ch => ch) * 7;
        return new(rival, roll % 12 == 0 ? Progression.GodDecks[roll / 12 % Progression.GodDecks.Length] : $"{rival}/x_lv7", 7);
    }
    public static readonly (int Dp, int Cards, int New) LegendPrize = (600, 3, 2);

    // ---------------------------------------------------------------- Limited Duel

    public sealed record Rule(string Name, string Text, Func<CardData, bool> Allowed, int Lp);

    public static readonly Rule[] LimitedRules =
    {
        new("NORMAL MONSTERS ONLY", "Your monsters must be Normal monsters. Spells and traps are free.", c => !c.IsMonster || c.IsNormalMonster || c.IsFusion, 8000),
        new("NO TRAPS", "Monsters and spells only. Both players start at 6000 LP.", c => !c.IsTrap, 6000),
        new("LEVEL 4 AND BELOW", "No monster above level 4. No tributes, no fusions.", c => !c.IsMonster || (c.Level <= 4 && !c.IsFusion), 8000),
        new("SPEED DUEL", "Both players start at 4000 LP. Anything goes.", _ => true, 4000),
        new("ONE OF EACH", "Every card at most once. Both players start at 8000 LP.", _ => true, 8000),
    };

    /// The rule of the day (same for everyone on a given date).
    public static Rule Today(DateTime day) => LimitedRules[day.DayOfYear % LimitedRules.Length];
    /// Copies a Limited deck may hold of a card under `rule`.
    public static int MaxCopies(Rule rule) => rule.Name == "ONE OF EACH" ? 1 : DeckRules.MaxCopies;

    // ---------------------------------------------------------------- Pack Duel

    /// Six packs of nine from the collectable cards: per pack 5 monsters, 2 spells, 2 traps, one of them a
    /// higher-level monster. Returns the card idx list (duplicates possible).
    public static List<List<int>> OpenPacks(Rng rng, int packs = 6)
    {
        var db = CardDb.Shared;
        var pool = db.ByIdx.Values.Where(c => c.PackMask != 0 && db.Canon(c.Idx) == c.Idx && c.Name.Length > 0 && !c.Name.EndsWith("Token") && !c.IsFusion).ToList();
        var low = pool.Where(c => c.IsMonster && c.Level <= 4).ToList();
        var high = pool.Where(c => c.IsMonster && c.Level >= 5).ToList();
        var spells = pool.Where(c => c.IsSpell).ToList();
        var traps = pool.Where(c => c.IsTrap).ToList();
        var fusions = db.ByIdx.Values.Where(c => c.IsFusion && c.PackMask != 0 && db.Canon(c.Idx) == c.Idx).ToList();
        int Pick(List<CardData> l) => l[rng.Int(l.Count)].Idx;
        var all = new List<List<int>>();
        for (int p = 0; p < packs; p++)
        {
            var pack = new List<int>();
            for (int k = 0; k < 4; k++) pack.Add(Pick(low));
            pack.Add(Pick(rng.Int(6) == 0 && fusions.Count > 0 ? fusions : high));
            pack.Add(Pick(spells)); pack.Add(Pick(spells));
            pack.Add(Pick(traps)); pack.Add(Pick(traps));
            all.Add(pack);
        }
        return all;
    }

    /// A pretend collection for the temporary deck builder: exactly the cards in `cards`.
    public static Profile PoolProfile(IEnumerable<int> cards)
    {
        var p = new Profile();
        foreach (var i in cards) p.Owned[i] = p.Count(i) + 1;
        return p;
    }
}

/// A Gauntlet run: the four rivals in a row (easiest first), one deck, your LP carries from duel to duel.
public sealed class Gauntlet
{
    public readonly OpponentPick[] Order;
    public int Index, Lp = 8000;
    public bool Lost;
    public Gauntlet(IEnumerable<OpponentPick> picks) => Order = picks.OrderBy(p => p.Level).ToArray();
    public OpponentPick? Current => Lost || Index >= Order.Length ? null : Order[Index];
    public bool Cleared => !Lost && Index >= Order.Length;
    public bool Over => Lost || Cleared;

    /// Your duel ended with `lpLeft` (when you won).
    public void Record(bool won, int lpLeft)
    {
        if (!won) { Lost = true; return; }
        Index++;
        Lp = Math.Max(1, lpLeft);
    }

    public static readonly (int Dp, int Cards, int New) ClearPrize = (1000, 4, 2);
    public const int DpPerWin = 100;
}
