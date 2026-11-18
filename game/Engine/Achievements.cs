namespace PoC.Engine;

/// Achievements (Menu Lab pick 2026-11-16): goals with progress; a finished one is claimed for DP and, for most, a
/// cosmetic from the shop list (card back, playmat, icon frame). Progress comes from Profile.Stats (counters the game
/// bumps when a duel ends) or from what the profile already holds (collection, decks, puzzles).
public static class Achievements
{
    public enum Prize { Dp, Back, Mat, Frame }

    public sealed record Def(string Id, string Group, string Name, string Desc, int Goal, int Dp, string Card, Prize Kind, string Key, Func<Profile, int, int> Have);

    public static readonly string[] Groups = { "DUELS", "RIVALS", "COLLECTION", "MODES", "CHALLENGES" };

    static int S(Profile p, string k) => p.Stats.GetValueOrDefault(k);
    static int Keys(Profile p, string prefix) => p.Stats.Keys.Count(k => k.StartsWith(prefix));
    static int Races(Profile p, string race) => p.Owned.Count(kv => kv.Value > 0 && CardDb.Shared.ByIdx.TryGetValue(kv.Key, out var c) && c.IsMonster && c.Race == race);
    static int Copies(Profile p, string name) => CardDb.Shared.ByName.TryGetValue(name, out var l) ? p.Count(CardDb.Shared.Canon(l[0].Idx)) : 0;

    /// `Have(profile, decks)`: decks = how many decks you have built.
    public static readonly Def[] All =
    {
        new("first_blood", "DUELS", "FIRST BLOOD", "Win your first duel", 1, 100, "Celtic Guardian", Prize.Dp, "", (p, _) => S(p, "wins")),
        new("duelist", "DUELS", "DUELIST", "Win 25 duels", 25, 200, "Dark Magician", Prize.Back, "ach_duelist", (p, _) => S(p, "wins")),
        new("veteran", "DUELS", "VETERAN", "Win 100 duels", 100, 500, "Buster Blader", Prize.Mat, "ach_veteran", (p, _) => S(p, "wins")),
        new("untouched", "DUELS", "UNTOUCHED", "Win a duel without losing any LP", 1, 300, "Mirror Force", Prize.Frame, "ach_untouched", (p, _) => S(p, "untouched")),
        new("comeback", "DUELS", "COMEBACK", "Win a duel with under 1,000 LP left", 1, 300, "Monster Reborn", Prize.Frame, "ach_comeback", (p, _) => S(p, "comeback")),
        new("forbidden", "DUELS", "FORBIDDEN ONE", "Win by gathering Exodia", 1, 500, "Exodia the Forbidden One", Prize.Back, "ach_forbidden", (p, _) => S(p, "exodia")),
        new("matches", "DUELS", "MATCH MAKER", "Win 10 matches", 10, 400, "Summoned Skull", Prize.Frame, "ach_matches", (p, _) => S(p, "matches")),
        new("king", "RIVALS", "KING OF GAMES", "Beat Yugi at EXPERT", 1, 300, "Dark Magician Girl", Prize.Back, "ach_king", (p, _) => S(p, "expert:yugi")),
        new("all_comers", "RIVALS", "ALL COMERS", "Beat every rival once", Tournament.Roster.Length, 800, "Thousand-Eyes Restrict", Prize.Mat, "ach_all_comers", (p, _) => Keys(p, "beat:")),
        new("grudge", "RIVALS", "GRUDGE", "Beat Kaiba 7 times in a row", 7, 300, "Blue-Eyes White Dragon", Prize.Back, "ach_grudge", (p, _) => p.Rivals.TryGetValue("kaiba", out var r) && r.LastWon ? r.Streak : 0),
        new("half_way", "COLLECTION", "HALF WAY", "Own half of all cards", 50, 500, "Pot of Greed", Prize.Mat, "ach_half_way", (p, _) => p.CollectionPercent),
        new("dragon_hoard", "COLLECTION", "DRAGON HOARD", "Own 30 different Dragons", 30, 300, "Red-Eyes B. Dragon", Prize.Back, "ach_dragon_hoard", (p, _) => Races(p, "Dragon")),
        new("triple_white", "COLLECTION", "TRIPLE WHITE", "Own 3 Blue-Eyes White Dragon", 3, 300, "Blue-Eyes White Dragon", Prize.Mat, "ach_triple_white", (p, _) => Copies(p, "Blue-Eyes White Dragon")),
        new("decksmith", "COLLECTION", "DECKSMITH", "Build 10 decks", 10, 200, "Card Destruction", Prize.Frame, "ach_decksmith", (_, d) => d),
        new("champion", "MODES", "CHAMPION", "Win a tournament", 1, 1000, "Gate Guardian", Prize.Mat, "ach_champion", (p, _) => S(p, "champion")),
        // 2026-11-17: the 4 tournament playmats moved here from the champion's podium (a win no longer gives one)
        new("cups_3", "MODES", "CUP REGULAR", "Win 3 tournaments", 3, 600, "Gaia the Dragon Champion", Prize.Mat, "obsidian", (p, _) => S(p, "champion")),
        new("cups_5", "MODES", "CUP HUNTER", "Win 5 tournaments", 5, 800, "Red-Eyes Black Metal Dragon", Prize.Mat, "ivory", (p, _) => S(p, "champion")),
        new("cups_10", "MODES", "GRAND CHAMPION", "Win 10 tournaments", 10, 1200, "Dark Paladin", Prize.Mat, "eclipse", (p, _) => S(p, "champion")),
        new("cups_20", "MODES", "LEGEND OF THE CUP", "Win 20 tournaments", 20, 2000, "Blue-Eyes Ultimate Dragon", Prize.Mat, "crimson", (p, _) => S(p, "champion")),
        new("gauntlet", "MODES", "RUN THE GAUNTLET", "Clear a gauntlet", 1, 500, "Black Luster Soldier", Prize.Back, "ach_gauntlet", (p, _) => S(p, "gauntlet")),
        new("legend", "MODES", "LEGEND SLAYER", "Win LEGEND against 4 rivals", 4, 600, "Dark Magician", Prize.Frame, "ach_legend", (p, _) => Keys(p, "legend:")),
        new("sealed", "MODES", "SEALED", "Win a pack duel match", 1, 300, "Monster Reborn", Prize.Dp, "", (p, _) => S(p, "pack")),
        new("chaos", "MODES", "CHAOS RIDER", "Win 5 chaos duels", 5, 300, "Chaos Emperor Dragon - Envoy of the End", Prize.Back, "ach_chaos", (p, _) => S(p, "chaos")),
        new("puzzler", "CHALLENGES", "PUZZLER", "Solve a challenge", 1, 200, "Swords of Revealing Light", Prize.Dp, "", (p, _) => p.Solved.Count),
        new("puzzle_master", "CHALLENGES", "PUZZLE MASTER", "Solve every challenge", Puzzles.All.Length, 800, "Jinzo", Prize.Frame, "ach_puzzle_master", (p, _) => p.Solved.Count),
    };

    public static int Progress(Def a, Profile p, int decks) => Math.Clamp(a.Have(p, decks), 0, a.Goal);
    public static bool Done(Def a, Profile p, int decks) => a.Have(p, decks) >= a.Goal;
    public static bool Claimed(Def a, Profile p) => p.Claimed.Contains(a.Id);
    public static bool Ready(Def a, Profile p, int decks) => Done(a, p, decks) && !Claimed(a, p);

    /// Pay a finished achievement: its DP and its cosmetic (kept as owned; already owned = DP only). False if not ready.
    public static bool Claim(Def a, Profile p, int decks)
    {
        if (!Ready(a, p, decks)) return false;
        p.Claimed.Add(a.Id);
        p.Dp += a.Dp;
        switch (a.Kind)
        {
            case Prize.Back: p.Backs.Add(a.Key); break;
            case Prize.Mat: p.Mats.Add(a.Key); break;
            case Prize.Frame: p.Frames.Add(a.Key); break;
        }
        return true;
    }

    /// Finished ones the player hasn't been told about yet (the unlock pop-up); marks them told.
    public static List<Def> TellNew(Profile p, int decks)
    {
        var fresh = All.Where(a => Done(a, p, decks) && !p.Told.Contains(a.Id)).ToList();
        foreach (var a in fresh) p.Told.Add(a.Id);
        return fresh;
    }

    /// A save from before achievements: what is already done is told quietly (still to claim), so the first launch
    /// doesn't fire a stack of pop-ups.
    public static void Quiet(Profile p, int decks) { foreach (var a in All) if (Done(a, p, decks)) p.Told.Add(a.Id); }

    public static void Bump(Profile p, string key, int by = 1) => p.Stats[key] = S(p, key) + by;
    public static void Mark(Profile p, string key) => p.Stats[key] = 1;
}
