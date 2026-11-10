namespace PoC.Engine;

public enum Loc { None, Deck, Hand, Monster, Spell, Field, Grave, Banished, Extra }
public enum Phase { Draw, Standby, Main1, Battle, Main2, End }
public enum SummonKind { None, Normal, Tribute, Set, Flip, Special, Fusion, Ritual }

public sealed class Card
{
    public readonly int Uid;
    public readonly CardData Data;
    public readonly int Owner;
    public int Controller;
    public Loc Loc;
    public int Zone = -1;
    public bool FaceUp;
    public bool Defense;

    // Per-stay state (cleared when the card leaves the field).
    public SummonKind Summoned;
    public int SummonTurn = -1;
    public int SetTurn = -1;
    public bool PositionChangedThisTurn;
    public int Attacks;
    public Card? EquippedTo;
    public int TempAtk, TempDef;                 // until end of turn
    public readonly Dictionary<string, int> Vars = new(); // per-card script counters/flags
    public bool IsToken;
    public string SummonedBy = "";
    public CardData? AsMonster;
    public string? TempName;                     // a name it takes for a while (Elemental HERO Prisma); cleared when it leaves the field                  // a Trap Card Special Summoned as a monster (its monster stats); null otherwise               // name of the card whose effect Special Summoned it ("" = none / procedure)

    public Card(int uid, CardData data, int owner) { Uid = uid; Data = data; Owner = owner; Controller = owner; }

    public bool OnField => Loc is Loc.Monster or Loc.Spell or Loc.Field;
    public bool IsMonster => Data.IsMonster && !IsSpellLike;
    // A monster card used as an equip (Beast of Gilfer, Sword Hunter's trophies) sits in a S/T zone.
    public bool IsSpellLike => Loc == Loc.Spell && Data.IsMonster;
    public bool Attack => !Defense;
    public string Name => Data.Name;
    public int Var(string k) => Vars.TryGetValue(k, out var v) ? v : 0;
    public int Inc(string k) => Vars[k] = Var(k) + 1;

    internal void ResetFieldState()
    {
        Summoned = SummonKind.None; SummonTurn = -1; SetTurn = -1; PositionChangedThisTurn = false; Attacks = 0;
        EquippedTo = null; TempAtk = TempDef = 0; Vars.Clear(); FaceUp = false; Defense = false; SummonedBy = ""; AsMonster = null; TempName = null;
    }
    public override string ToString() => $"{Data.Name}#{Uid}";
}

public sealed class Player
{
    public readonly int Index;
    public int Lp = 8000;
    public readonly List<Card> Deck = new();   // index 0 = top
    public readonly List<Card> Hand = new();
    public readonly Card?[] Monsters = new Card?[5];
    public readonly Card?[] Spells = new Card?[5];
    public Card? FieldZone;
    public readonly List<Card> Grave = new();  // last = top
    public readonly List<Card> Banished = new();
    public readonly List<Card> Extra = new();  // Fusion Deck
    public bool NormalSummoned;
    public int ExtraSummons;                   // Ultimate Offering
    public Player(int i) => Index = i;

    public IEnumerable<Card> MonstersOnField => Monsters.Where(c => c != null)!;
    public IEnumerable<Card> SpellsOnField => Spells.Where(c => c != null).Concat(FieldZone != null ? new[] { FieldZone } : Array.Empty<Card>())!;
    public IEnumerable<Card> FieldCards => MonstersOnField.Concat(SpellsOnField);
    public int FreeMonsterZones => Monsters.Count(c => c == null);
    public int FreeSpellZones => Spells.Count(c => c == null);
}

// ---- prompts and commands ----

public enum Ask { Main, Battle, Respond, Pick, YesNo, Choose, Position }

public enum Act
{
    Summon, Set, FlipSummon, ChangePos, Activate, SetSpell, Special, // main phase
    ToBattle, ToMain2, EndTurn,                                       // phase moves
    Attack, Pass,                                                      // battle / responses
    Card, Option,                                                      // picks
}

/// One selectable option. Card/Target point at card instances; Arg is a small int (zone, enum, value).
public sealed record Opt(Act Act, Card? Card = null, Card? Target = null, int Arg = 0, string? Text = null)
{
    public override string ToString() => Act switch
    {
        Act.Attack => $"Attack {Card} -> {(Target == null ? "direct" : Target.ToString())}",
        Act.Option => Text ?? $"Option {Arg}",
        _ => $"{Act} {Card}{(Target != null ? " -> " + Target : "")}{(Text != null ? " " + Text : "")}",
    };
}

public sealed class Prompt
{
    public int Player;
    public Ask Kind;
    public string Text = "";
    public List<Opt> Options = new();
    public int Min = 1, Max = 1;
    public Card? Source;   // card asking (for UI)
    public bool CancelOk;  // an empty answer backs out (a tribute summon you changed your mind about)
    public override string ToString() => $"P{Player} {Kind} '{Text}' [{Min}-{Max}] {string.Join(" | ", Options.Select((o, i) => $"{i}:{o}"))}";
}

/// A player's answer: indexes into Prompt.Options. Every duel input is one of these.
public readonly record struct Command(int Player, int[] Picks)
{
    public override string ToString() => $"{Player}:{string.Join(",", Picks)}";
    public static Command Parse(string s)
    {
        var p = s.Split(':');
        return new Command(int.Parse(p[0]), p[1].Length == 0 ? Array.Empty<int>() : p[1].Split(',').Select(int.Parse).ToArray());
    }
}

public sealed class DuelSetup
{
    public ulong Seed = 1;
    public bool Classic = true;            // keep the PoC quirks (effects.json "quirk")
    public int StartingLp = 8000;
    public int[]? Lp;
    public bool Chaos;
    public int TurnLimit;                  // Challenges: when this turn ends without a winner, player 0 loses (0 = no limit)                     // Chaos Duel: every third turn the rift sets a new rule for both players (Duel.ChaosRule)                      // per-player starting LP (a Gauntlet carries yours over); null = StartingLp
    public int FirstPlayer = 0;
    public bool Shuffle = true;            // false = decks keep their listed order (index 0 = top); for tests/puzzles
    public List<int>[] Main = { new(), new() }; // card idx lists
    public List<int>[] Extra = { new(), new() };
}

public sealed class DuelOver : Exception
{
    public readonly int Winner; // -1 = draw
    public readonly string Reason;
    public DuelOver(int winner, string reason) : base(reason) { Winner = winner; Reason = reason; }
}
