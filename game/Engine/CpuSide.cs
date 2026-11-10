namespace PoC.Engine;

/// CPU side-decking between the duels of a match (remake rule). After each duel the CPU notes what you revealed
/// (spells, traps, big monsters, how many monsters) and swaps up to MaxSwaps situational cards between its Main
/// and Side Deck, e.g. Dust Tornado in for Trap Hole against a trap-heavy deck. Only situational cards ever move,
/// so the deck's engine (Dark Magician, Lord of D. + Flute...) is never touched.
public static class CpuSide
{
    public const int MaxSwaps = 5;

    /// What the opponent showed over the match so far.
    public sealed class Seen { public int Spells, Traps, Big, Monsters, Duels; }

    enum Role { AntiTrap, Backrow, AntiSpell, AntiBig, AntiSwarm }

    static readonly Dictionary<string, Role> Roles = new()
    {
        ["Jinzo"] = Role.AntiTrap, ["Royal Decree"] = Role.AntiTrap, ["Seven Tools of the Bandit"] = Role.AntiTrap,
        ["Mystical Space Typhoon"] = Role.Backrow, ["Dust Tornado"] = Role.Backrow, ["Heavy Storm"] = Role.Backrow,
        ["Remove Trap"] = Role.Backrow, ["De-Spell"] = Role.Backrow, ["Giant Trunade"] = Role.Backrow,
        ["Magic Jammer"] = Role.AntiSpell, ["Magic Drain"] = Role.AntiSpell, ["Imperial Order"] = Role.AntiSpell,
        ["Trap Hole"] = Role.AntiBig, ["Crush Card"] = Role.AntiBig, ["Fissure"] = Role.AntiBig, ["Spellbinding Circle"] = Role.AntiBig,
        ["Ring of Destruction"] = Role.AntiBig, ["Nobleman of Crossout"] = Role.AntiBig, ["Magic Cylinder"] = Role.AntiBig,
        ["Widespread Ruin"] = Role.AntiBig, ["Kunai with Chain"] = Role.AntiBig, ["Gravity Bind"] = Role.AntiBig,
        ["Torrential Tribute"] = Role.AntiSwarm, ["Dark Hole"] = Role.AntiSwarm, ["Mirror Force"] = Role.AntiSwarm, ["Ceasefire"] = Role.AntiSwarm,
    };

    /// Note the cards `player` revealed in a finished duel: everything of theirs face-up, in the Graveyard or banished.
    public static void Observe(Seen s, Duel d, int player)
    {
        foreach (var c in d.Cards.Where(c => c.Owner == player && !c.IsToken && (c.Loc is Loc.Grave or Loc.Banished || (c.OnField && c.FaceUp))))
        {
            if (c.Data.IsSpell) s.Spells++;
            else if (c.Data.IsTrap) s.Traps++;
            else { s.Monsters++; if (c.Data.Atk >= 1900) s.Big++; }
        }
        s.Duels++;
    }

    /// How useful a situational card looks against what was seen (1 = as good as usual); null = not situational.
    static double? Value(CardData c, Seen s)
    {
        if (!Roles.TryGetValue(c.Name, out var r)) return null;
        double n = Math.Max(1, s.Duels);
        double v = r switch
        {
            Role.AntiTrap => s.Traps / n / 2.5,
            Role.Backrow => (s.Traps + 0.25 * s.Spells) / n / 3,
            Role.AntiSpell => s.Spells / n / 4,
            Role.AntiBig => s.Big / n / 2,
            _ => s.Monsters / n / 6,
        };
        return Math.Clamp(v, 0.25, 2);
    }

    /// Swap situational cards between deck.Main and deck.Side (in place; card counts and limits can't change).
    /// Returns the swaps (out, in).
    public static List<(CardData Out, CardData In)> Swap(Deck deck, Seen s)
    {
        var db = CardDb.Shared;
        var done = new List<(CardData, CardData)>();
        if (s.Duels == 0) return done;
        while (done.Count < MaxSwaps)
        {
            var outs = deck.Main.Distinct().Select(db.Get).Where(c => Value(c, s) != null).OrderBy(c => Value(c, s)).ToList();
            var ins = deck.Side.Distinct().Select(db.Get).Where(c => Value(c, s) != null).OrderByDescending(c => Value(c, s)).ToList();
            var swap = ins.SelectMany(i => outs.Select(o => (o, i)))
                .Where(x => Value(x.i, s) > Value(x.o, s) + 0.4)
                .Select(x => ((CardData, CardData)?)x).FirstOrDefault();
            if (swap is not { } sw) break;
            deck.Main.Remove(sw.Item1.Idx); deck.Side.Add(sw.Item1.Idx);
            deck.Side.Remove(sw.Item2.Idx); deck.Main.Add(sw.Item2.Idx);
            done.Add(sw);
        }
        return done;
    }
}
