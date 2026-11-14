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

    /// Six packs of nine from the collectable cards: per pack 5 monsters (one of them higher-level), 2 spells, 2 traps.
    /// The packs share two themes (a monster Type or Attribute each, playtest 2026-11-12: fully random packs gave piles
    /// that could not make a deck): three of a pack's four low monsters and most high ones come from its theme, one
    /// spell backs the theme when one exists, and the rest are cards that work on their own (no card named in their
    /// text, no Ritual pieces, nothing that can't be Normal Summoned). One low monster per pack stays fully random.
    /// Returns the card idx list (duplicates possible).
    public static List<List<int>> OpenPacks(Rng rng, int packs = 6)
    {
        var db = CardDb.Shared;
        static bool Alone(CardData c) => !c.Desc.Contains('"') && c.Frame != "Ritual" && !c.Desc.Contains("Cannot be Normal Summoned", StringComparison.OrdinalIgnoreCase);
        var pool = db.ByIdx.Values.Where(c => c.PackMask != 0 && db.Canon(c.Idx) == c.Idx && c.Name.Length > 0 && !c.Name.EndsWith("Token") && !c.IsFusion).ToList();
        var low = pool.Where(c => c.IsMonster && c.Level <= 4 && Alone(c)).ToList();
        var high = pool.Where(c => c.IsMonster && c.Level is >= 5 and <= 8 && Alone(c)).ToList();
        var spells = pool.Where(c => c.IsSpell && Alone(c)).ToList();
        var traps = pool.Where(c => c.IsTrap && Alone(c)).ToList();
        // themes: any Type or Attribute with enough low monsters to fill packs
        var themes = low.GroupBy(c => c.Race).Concat(low.GroupBy(c => c.Attribute)).Where(g => g.Key.Length > 1 && char.IsLetter(g.Key[0]) && g.Count() >= 12).Select(g => g.Key).ToList();
        string t1 = themes[rng.Int(themes.Count)], t2 = themes[rng.Int(themes.Count)];
        static bool In(CardData c, string theme) => c.Race == theme || c.Attribute == theme;
        static bool Backs(CardData c, string theme) => c.Desc.Contains(theme, StringComparison.Ordinal);
        int Pick(List<CardData> l) => l[rng.Int(l.Count)].Idx;
        int Themed(List<CardData> l, Func<CardData, bool> f) { var t = l.Where(f).ToList(); return Pick(t.Count > 0 ? t : l); }
        var all = new List<List<int>>();
        for (int p = 0; p < packs; p++)
        {
            string theme = p % 2 == 0 ? t1 : t2;
            var pack = new List<int>();
            for (int k = 0; k < 3; k++) pack.Add(Themed(low, c => In(c, theme)));
            pack.Add(Pick(low));
            pack.Add(rng.Int(4) == 0 ? Pick(high) : Themed(high, c => In(c, theme)));
            pack.Add(Themed(spells, c => Backs(c, theme))); pack.Add(Pick(spells));
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
