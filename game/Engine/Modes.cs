namespace PoC.Engine;

/// The duel modes of the DUEL tab (2026-11-08). Regular = rival + single duel or match (the original game).
public enum GameMode { Regular, Gauntlet, Chaos, Challenges, Pack, Limited, Legend }

public static class Modes
{
    public sealed record Info(GameMode Mode, string Name, string Line, string Reward, bool Ready);

    public static readonly Info[] All =
    {
        new(GameMode.Regular, "REGULAR", "Choose a rival. A single duel or a best-of-three match.", "Cards and DP by the rival's level", true),
        new(GameMode.Gauntlet, "GAUNTLET", "Eight rivals in a row, easiest first, with one deck. Your LP carries over.", "The higher you climb, the bigger the prize: up to 2,000 DP + 4 cards", true),
        new(GameMode.Pack, "PACK DUEL", "Draft a deck from two rival decks, pack by pack, against the CPU.", "Keep 1 of 3 cards from your deck (2 of 6 for a match)", true),
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
        return new(rival, roll % 12 == 0 ? Progression.OwnGod(rival) ?? Progression.GodDecks[roll / 12 % Progression.GodDecks.Length] : $"{rival}/x_lv7", 7);
    }
    /// Who can be faced in Legend: rivals with a level-7 deck (2026-11-16: all 21 duelists).
    public static bool HasLegend(string rival) => Progression.HasLadder(rival) && Progression.LadderLevel(rival, 7) == 7;
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
}

/// A Gauntlet run: rivals in a row (easiest first), one deck, your LP carries from duel to duel. Since 2026-11-16 it
/// is eight floors drawn from the tournament roster (Menu Lab pick): the run pays once, when it ends, the prize of the
/// highest floor you beat (Floors; not added up): DP rises every floor, cards go up by one every second floor.
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

    /// The prize for beating floor i+1 (the whole prize, not added to the floors below).
    public static readonly (int Dp, int Cards)[] Floors = { (100, 0), (150, 1), (250, 1), (400, 2), (600, 2), (850, 3), (1200, 3), (2000, 4) };
    /// What the run pays if it ends now: the prize of the highest floor beaten.
    public (int Dp, int Cards) Prize => Index == 0 ? (0, 0) : Floors[Math.Min(Index, Floors.Length) - 1];

    /// Eight duelists from `roster`, spread from the easiest to the hardest (ties shuffled by `rng`), each with their
    /// tournament deck.
    public static Gauntlet Climb(IEnumerable<Duelist> roster, Rng rng)
    {
        var all = roster.Select(d => (d, k: rng.Int(1000))).OrderBy(x => x.d.Level).ThenBy(x => x.k).Select(x => x.d).ToList();
        int n = Math.Min(Floors.Length, all.Count);
        var pick = Enumerable.Range(0, n).Select(i => all[n <= 1 ? 0 : (int)Math.Round(i * (all.Count - 1) / (double)(n - 1))]).Distinct().ToList();
        return new Gauntlet(pick.Select(d => new OpponentPick(d.Key, d.Deck, d.Level)));
    }
}
