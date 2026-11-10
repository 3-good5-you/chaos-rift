using Godot;
using PoC.Engine;

namespace PoC.Game.UI;

/// State shared between menu screens: the player profile and the opponent picks for this visit.
public static class Session
{
    public static Profile Profile = new();
    public static readonly Rng Rng = new((ulong)DateTime.Now.Ticks);

    public static OpponentPick[] Picks = Array.Empty<OpponentPick>();
    public static int Selected = -1;
    public static bool Match;            // false = single duel, true = match (best of 3)
    public static Tournament? Tour;      // a tournament in progress (Tournament screen); its duels use the current foe
    public static GameMode Mode;         // the DUEL tab's mode (Engine/Modes.cs)
    public static Gauntlet? Run;         // a Gauntlet in progress
    public static Modes.Rule? Rule;      // a Limited Duel's rule
    public static Deck? ModeDeck;        // a deck built for this mode only (Pack / Limited): never saved, never exported
    public static Puzzles.Puzzle? Puzzle;   // a Challenge being played
    public static int LastLp;            // your LP when the last duel ended (a Gauntlet carries it on)

    /// Back to a plain duel: no mode state left over.
    public static void ClearMode() { Mode = GameMode.Regular; Run = null; Rule = null; ModeDeck = null; Puzzle = null; }

    /// Who you are dueling: the tournament foe, or the rival picked on the rival select screen.
    public static RivalPanels.Rival Rival => Tour?.Foe is { } f ? RivalPanels.Of(f)
        : Run?.Current is { } g ? RivalPanels.All.First(r => r.File == g.Rival) : RivalPanels.All[Selected < 0 ? 0 : Selected];
    /// Their deck and CPU level.
    public static OpponentPick Pick => Tour?.Foe is { } f ? new OpponentPick(f.Key, f.Deck, f.Level)
        : Run?.Current is { } g ? g
        : Mode == GameMode.Legend ? Modes.Legend(RivalPanels.All[Selected < 0 ? 0 : Selected].File)
        : Picks[Selected < 0 ? 0 : Selected];
    public static int MatchWins, MatchLosses;   // running score of the current match

    /// The duel that just ended, for the results screen. Winner: 0 you, 1 rival, -1 draw.
    public sealed record DuelResult(int Winner, string Reason);
    public static DuelResult? LastDuel;
    public static Replay? LastReplay;    // the duel that just ended, until SAVE REPLAY or the next duel
    public static Replay? Watch;         // the replay the field is playing back (Replays screen)
    public static string ReplayDir => ProjectSettings.GlobalizePath("user://replays");
    public static string? AfterUnlock;
    public static Shop.Item? LastMat;    // the playmat the last result won (tournament champion), revealed after the results   // where the deck unlock goes on to (the Results button that led there)
    public static Deck? LastStarter;     // the rival's starter deck the last duel won you (first win against them)
    public static int LastDp;            // Duelist Points the last duel paid (Results shows them)
    public static bool Classic = true;   // PoC quirks on (the default; no toggle in the UI)

    public static readonly List<Deck> Decks = new();
    public static Deck? Playing;          // the deck chosen in DuelSetup
    public static Deck? MatchDeck;        // a match plays a copy of Playing; side-decking changes only the copy
    public static bool MatchOver => MatchWins >= 2 || MatchLosses >= 2;
    public static Deck? RivalDeck;        // in a match: the rival's deck after its side-decking (null = as listed)
    public static CpuSide.Seen RivalSeen = new();   // what you revealed so far this match

    /// Between the duels of a match the rival sides against what you revealed (Engine/CpuSide.cs).
    public static void RivalSides(Duel d)
    {
        if (RivalDeck == null)
        {
            var key = Pick.DeckKey;
            var (main, extra) = CardDb.Shared.Deck(key);
            RivalDeck = new Deck(key, main, extra);
            RivalDeck.Side.AddRange(CardDb.Shared.DeckSide(key));
        }
        CpuSide.Observe(RivalSeen, d, 0);
        CpuSide.Swap(RivalDeck, RivalSeen);
    }

    /// Record a finished duel: streak / Joey level, the match score, and (in a match) the loser goes first
    /// next. True when the match goes on (side-decking before the next duel); false = results.
    public static bool FinishDuel(int winner, string reason)
    {
        LastDuel = new DuelResult(winner, reason);
        LastStarter = null;
        if (Tour != null) LastDp = 0;   // tournament prizes are paid once, by finishing place, in Results
        else if (Run != null) LastDp = winner == 0 ? Gauntlet.DpPerWin : 0;
        else if (Puzzle is { } z) { LastDp = winner == 0 && Profile.Solved.Add(z.Id) ? z.Dp : 0; }   // paid the first time only   // the Gauntlet's prize comes in Results
        else if (Mode != GameMode.Regular)
            LastDp = Shop.Earn(Pick.Level, winner == 0) + (winner == 0 ? Mode switch { GameMode.Limited => 150, GameMode.Chaos => 200, _ => 0 } : 0);
        else
        {
            Progression.Record(Profile, RivalPanels.All[Selected].File, winner == 0, Picks.Length > Selected ? Picks[Selected].Level : 0);
            LastDp = Shop.Earn(Picks.Length > Selected ? Picks[Selected].Level : 1, winner == 0);
            LastStarter = winner == 0 ? DeckRules.UnlockStarter(Profile, RivalPanels.All[Selected].File) : null;
            if (LastStarter != null) Decks.Add(LastStarter);
        }
        Profile.Dp += LastDp;
        Save();
        if (!Match) return false;
        if (winner == 0) MatchWins++;
        else if (winner == 1) MatchLosses++;
        if (winner >= 0) YouFirst = winner == 1;
        return !MatchOver;
    }
    public static bool NeedsStarter => Profile.Owned.Count == 0;
    public static bool YouFirst;         // coin flip result -> DuelSetup.FirstPlayer

    /// Roll each rival's opponent deck (auto difficulty), as the original did when a duel was set up.
    public static void RollPicks()
    {
        if (OS.GetCmdlineUserArgs().Contains("--demo"))
        {
            // Screenshot/demo data showing the range of levels.
            Picks = new[] { new OpponentPick("yugi", "yugi/cpu03", 4), new OpponentPick("kaiba", "kaiba/kaiba_kk", 6), new OpponentPick("joey", "joey/cpu_011", 2), new OpponentPick("jaden", "jaden/x_lv5", 5) };
            return;
        }
        Picks = RivalPanels.All.Select(r => Progression.Pick(r.File, Profile, Rng)).ToArray();
    }

    // The save file (Engine/SaveData.cs) in user://. `-- --demo` neither loads nor saves; screenshot runs
    // (`--shot=`) load but never save, so fake duels and demo data can't touch the real save.
    // `-- --unlockall` (testing): its own save_test.json, started from a copy of the real save, with every card x3,
    // every cosmetic and plenty of DP. Decks built there are kept between test runs; the real save is never touched.
    static bool UnlockAll => OS.GetCmdlineUserArgs().Contains("--unlockall");
    static string SavePath => ProjectSettings.GlobalizePath(UnlockAll ? "user://save_test.json" : "user://save.json");
    static bool loaded;
    static bool NoSave => Demo.On || OS.GetCmdlineUserArgs().Any(a => a.StartsWith("--shot="));

    /// Load the save once, at startup. Missing = new player; unreadable = kept as save.json.bad, start fresh.
    public static void Load()
    {
        if (loaded) return;
        loaded = true;
        if (Demo.On) return;
        var path = UnlockAll && !File.Exists(SavePath) ? ProjectSettings.GlobalizePath("user://save.json") : SavePath;
        var s = SaveData.Read(path, out bool broken);
        if (broken) GD.PushWarning($"Save file unreadable; kept as {SavePath}.bad, starting fresh.");
        if (s != null)
        {
            Profile = s.Profile;
            Decks.Clear(); Decks.AddRange(s.Decks);
            Playing = s.Playing >= 0 ? Decks[s.Playing] : null;
            Settings.Saved = s.Options;
        }
        if (UnlockAll) Unlock();
    }

    static void Unlock()
    {
        foreach (var d in CardDb.Shared.ByIdx.Values) Profile.Owned[d.Idx] = Math.Max(Profile.Count(d.Idx), 3);
        foreach (var i in Shop.Mats.Concat(Shop.Backs).Concat(Shop.Frames))
            (i.Kind switch { Shop.Cosmetic.Mat => Profile.Mats, Shop.Cosmetic.Back => Profile.Backs, _ => Profile.Frames }).Add(i.Key);
        Profile.Dp = Math.Max(Profile.Dp, 999_999);
    }

    public static void Save()
    {
        if (NoSave) return;
        try { SaveData.Write(SavePath, new SaveData.State { Profile = Profile, Decks = Decks.ToList(), Playing = Playing == null ? -1 : Decks.IndexOf(Playing), Options = Settings.Saved }); }
        catch (Exception e) { GD.PushError($"Save failed: {e.Message}"); }
    }

    public static Color TierColor(int level) => level switch
    {
        <= 2 => new Color(0.4f, 0.88f, 0.5f),
        <= 4 => new Color(0.4f, 0.72f, 1f),
        5 => new Color(1f, 0.62f, 0.22f),
        _ => new Color(1f, 0.28f, 0.32f),
    };

}
