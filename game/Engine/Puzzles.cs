namespace PoC.Engine;

/// Challenges (2026-11-08): set boards to win this turn. You start on your turn 3 at the Main Phase with exactly the
/// hand and field given; the rival's field is laid and its hand is empty; if your turn ends without a win, you lose
/// (DuelSetup.TurnLimit). Each puzzle has a known solution, checked by Engine.Check.
public static class Puzzles
{
    /// A card on a field: name, set face-down, in defense.
    public sealed record On(string Name, bool Set = false, bool Def = false);

    public sealed record Puzzle(string Id, string Name, string Goal, int TheirLp, string[] Hand, On[] Mine, On[] Theirs,
        string[]? Grave = null, string[]? Extra = null, string[]? TheirGrave = null, int YourLp = 8000, int Dp = 200,
        int Turns = 1, string[]? Draws = null, string[]? TheirDraws = null)
    {
        /// "WIN THIS TURN" / "WIN WITHIN 3 TURNS" (your turns; the rival plays its own in between).
        public string Limit => Turns == 1 ? "WIN THIS TURN" : $"WIN WITHIN {Turns} TURNS";
    }

    /// Rising difficulty. Every solution wins against any answer the rival can give (Engine.Check walks all of them),
    /// and the obvious line loses. Goals never name face-down cards (2026-11-09: the player finds out like in a duel).
    public static readonly Puzzle[] All =
    {
        new("c01", "THE KINDEST CUT", "One card waits face-down. You have only 2000 LP.", 3100,
            new[] { "Shrink", "Hinotama" },
            new[] { new On("Summoned Skull"), new On("Gemini Elf"), new On("Celtic Guardian") },
            new[] { new On("Magic Cylinder", Set: true) }, YourLp: 2000),
        new("c02", "CATAPULT", "A face-down card guards them. Your LP is lower than theirs.", 2400,
            new[] { "Megamorph", "Axe of Despair" },
            new[] { new On("Catapult Turtle"), new On("Gemini Elf") },
            new[] { new On("Mystical Elf", Def: true), new On("Mirror Force", Set: true) }, YourLp: 2000),
        new("c03", "SILENCE", "Two cards wait face-down. Neither may be touched.", 2200,
            new[] { "Jinzo", "Ookazi" },
            new[] { new On("Celtic Guardian"), new On("Battle Ox") },
            new[] { new On("Gemini Elf"), new On("Mirror Force", Set: true), new On("Magic Cylinder", Set: true) }),
        new("c04", "THE PRICE OF POWER", "Three spells. Only the right two win.", 7900,
            new[] { "Monster Reborn", "Premature Burial", "Megamorph" },
            new[] { new On("Gemini Elf"), new On("Celtic Guardian") }, Array.Empty<On>(),
            Grave: new[] { "Blue-Eyes White Dragon" }),
        new("c05", "TURNABOUT", "Your monsters are built for defense. Their dragon is not.", 3300,
            new[] { "Shield & Sword" },
            new[] { new On("Big Shield Gardna"), new On("Mystical Elf"), new On("Celtic Guardian") },
            new[] { new On("Blue-Eyes White Dragon") }),
        new("c06", "STRENGTH IN NUMBERS", "One monster on your field. It will need friends.", 4700,
            new[] { "Scapegoat", "United We Stand", "Axe of Despair" },
            new[] { new On("Celtic Guardian") }, new[] { new On("Battle Ox") }),
        new("c07", "CANNON FODDER", "A face-down card guards them. Count your damage.", 2300,
            new[] { "Change of Heart", "Ookazi" },
            new[] { new On("Cannon Soldier"), new On("Celtic Guardian") },
            new[] { new On("Gemini Elf"), new On("Battle Ox"), new On("Mirror Force", Set: true) }),
        new("c08", "BORROWED DRAGON", "Their dragon, a face-down card, your 2900 LP.", 3000,
            new[] { "Change of Heart", "Megamorph" },
            new[] { new On("Catapult Turtle") },
            new[] { new On("Blue-Eyes White Dragon"), new On("Mirror Force", Set: true) }, YourLp: 2900, Dp: 300),
        // ---- more than one turn (2026-11-12): `Draws` are your next draws in order, the rival draws `TheirDraws` and
        // plays its turns for real. The solutions win whatever the rival does on its turns (Engine.Check walks them).
        new("m01", "SLOW BURN", "Their monster is too strong to fight. Your next draw is Ookazi.", 3300,
            new[] { "Scapegoat" },
            new[] { new On("Cannon Soldier") },
            new[] { new On("Summoned Skull") }, YourLp: 2600, Dp: 300,
            Turns: 2, Draws: new[] { "Ookazi" }, TheirDraws: new[] { "Polymerization" }),
        new("m02", "SECOND FLIGHT", "A wall and a face-down card. Your next draw is Premature Burial.", 3000,
            new[] { "Monster Reborn" },
            new[] { new On("Catapult Turtle", Def: true) },
            new[] { new On("Labyrinth Wall", Def: true), new On("Mirror Force", Set: true) },
            Grave: new[] { "Blue-Eyes White Dragon" }, YourLp: 1000, Dp: 300,
            Turns: 2, Draws: new[] { "Premature Burial" }, TheirDraws: new[] { "Polymerization" }),
        new("m03", "THREE LIGHTS", "Their dragon rules the field. Your next draws: Axe of Despair, then Gemini Elf.", 5400,
            new[] { "Swords of Revealing Light", "Battle Ox", "Summoned Skull" },
            Array.Empty<On>(),
            new[] { new On("Blue-Eyes White Dragon") }, YourLp: 1500, Dp: 400,
            Turns: 3, Draws: new[] { "Axe of Despair", "Gemini Elf" }, TheirDraws: new[] { "Polymerization", "Black Luster Ritual" }),
    };

    public static Puzzle? Get(string id) => All.FirstOrDefault(p => p.Id == id);

    static readonly string[] Filler = { "Mystical Elf", "Silver Fang", "Battle Ox", "Feral Imp", "Celtic Guardian" };

    /// The duel setup: no shuffle, both decks hold every card the board needs (then filler), the turn limit set.
    public static DuelSetup Setup(Puzzle z)
    {
        var db = CardDb.Shared;
        var s = new DuelSetup { Seed = 1, FirstPlayer = 0, Shuffle = false, Classic = true, TurnLimit = 1 + 2 * z.Turns };   // your last turn: 3, 5, 7 ...
        List<int> Deck(IEnumerable<string> need) => need.Concat(Enumerable.Range(0, 40).Select(i => Filler[i % Filler.Length])).Take(Math.Max(40, need.Count())).Select(n => db.Named(n).Idx).ToList();
        s.Main[0].AddRange(Deck(z.Hand.Concat(z.Mine.Select(o => o.Name)).Concat(z.Grave ?? Array.Empty<string>()).Concat(z.Draws ?? Array.Empty<string>()).Where(n => !db.Named(n).IsFusion)));
        s.Main[1].AddRange(Deck(z.Theirs.Select(o => o.Name).Concat(z.TheirGrave ?? Array.Empty<string>()).Concat(z.TheirDraws ?? Array.Empty<string>())));
        foreach (var x in z.Extra ?? Array.Empty<string>()) s.Extra[0].Add(db.Named(x).Idx);
        return s;
    }

    /// Play turns 1 and 2 as empty turns, then lay the board on your turn 3 at its Main Phase.
    public static void Prepare(Duel d, Puzzle z)
    {
        Pass(d, () => d.Turn == 3 && d.Pending is { Player: 0, Kind: Ask.Main });
        var me = d.P(0); var them = d.P(1);
        foreach (var pl in new[] { me, them })
            foreach (var c in pl.Hand.ToList()) { pl.Hand.Remove(c); c.Loc = Loc.Deck; pl.Deck.Add(c); }
        Card Take(Player pl, string name) { var c = pl.Deck.Concat(pl.Extra).First(x => x.Name == name); pl.Deck.Remove(c); pl.Extra.Remove(c); return c; }
        foreach (var n in z.Hand) { var c = Take(me, n); c.Loc = Loc.Hand; c.FaceUp = false; me.Hand.Add(c); }
        void Lay(Player pl, int p, On o)
        {
            var c = Take(pl, o.Name);
            pl.Deck.Add(c);   // Place takes it from wherever it is
            d.Place(c, p, CardDb.Shared.Named(o.Name).IsMonster ? Loc.Monster : Loc.Spell);
            c.FaceUp = !o.Set; c.Defense = o.Def; c.SetTurn = -5; c.SummonTurn = -5;
        }
        foreach (var o in z.Mine) Lay(me, 0, o);
        foreach (var o in z.Theirs) Lay(them, 1, o);
        foreach (var n in z.Grave ?? Array.Empty<string>()) { var c = Take(me, n); c.Loc = Loc.Grave; c.FaceUp = true; me.Grave.Add(c); }
        foreach (var n in z.TheirGrave ?? Array.Empty<string>()) { var c = Take(them, n); c.Loc = Loc.Grave; c.FaceUp = true; them.Grave.Add(c); }
        // the next draws, in order, on top of each Deck
        void Top(Player pl, string[]? names)
        {
            for (int i = 0; i < (names?.Length ?? 0); i++)
            {
                var c = pl.Deck.Skip(i).First(x => x.Name == names![i]);
                pl.Deck.Remove(c); pl.Deck.Insert(i, c);
            }
        }
        Top(me, z.Draws); Top(them, z.TheirDraws);
        them.Lp = z.TheirLp; me.Lp = z.YourLp;
        me.NormalSummoned = false;
        d.RefreshPrompt();
    }

    /// Answer prompts plainly (end turns, pass responses) until `stop` holds.
    static void Pass(Duel d, Func<bool> stop)
    {
        for (int k = 0; k < 200 && !d.Over && !stop(); k++)
        {
            var p = d.Pending!;
            int i = p.Kind switch
            {
                Ask.Main or Ask.Battle => p.Options.FindIndex(o => o.Act == Act.EndTurn),
                Ask.Respond => p.Options.FindIndex(o => o.Act == Act.Pass),
                Ask.YesNo => 1,
                _ => 0,
            };
            if (i < 0) i = p.Options.Count - 1;
            d.Submit(new Command(p.Player, p.Kind == Ask.Pick ? Enumerable.Range(0, Math.Max(1, p.Min)).ToArray() : new[] { i }));
        }
    }
}
