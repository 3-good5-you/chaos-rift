using Godot;
using PoC.Engine;

namespace PoC.Game.UI;

/// Replays (2026-11-09): every duel leaves Session.LastReplay; Results / the LAN lobby offer SAVE REPLAY, the
/// Replays screen lists them. Watching = Session.Watch set: the field re-runs the saved command log, both sides
/// answered from the file at a readable pace; no rewards, no CPU learning. LEAVE REPLAY in the duel menu.
public partial class DuelField
{
    Replay? Watch => Session.Watch;
    int watchAt;
    bool saveReplay, watchDev;
    string dressMat = "", dressBack = "", dressBack2 = "";
    string? dressMat2;

    sealed class ReplayEnded : Exception { }

    async Task<Command> Watched(Prompt p)
    {
        if (watchAt >= Watch!.Log.Count) throw new ReplayEnded();
        // The pace of a live duel (the beat the field gives a rival's move), whatever your duel speed setting.
        await Wait(p.Kind is Ask.Main or Ask.Battle ? 0.8 : 0.4);
        while (watchPaused) await ToSignal(GetTree().CreateTimer(0.1, true, false, true), SceneTreeTimer.SignalName.Timeout);
        return Watch.Log[watchAt++];
    }

    // ---------------------------------------------------------------- the replay bar (bottom right)

    static readonly double[] WatchSpeeds = { 1, 2, 4 };
    double watchMult = 1;
    bool watchPaused;
    readonly List<(Panel Chip, double Mult)> speedChips = new();
    Panel? pauseChip;

    /// REPLAY over PAUSE over 1x 2x 4x. Space pauses too. Pause holds before the next move (the current one finishes).
    void WatchBar()
    {
        pace = Settings.Speeds[0].Pace;   // NORMAL
        SetWatchSpeed(1);
        // Right side, under MENU and above the turn circle: nothing of the duel lives there.
        float x = W - 384, y = H * 0.23f;
        Put(hud, "REPLAY", Impact, 30, RivalPanels.Gold, new Vector2(x, y), new Vector2(360, 40), HorizontalAlignment.Center, 4);
        pauseChip = Chips.Make("PAUSE", new Vector2(x + 10, y + 50), new Vector2(340, 58), TogglePause, 22);
        hud.AddChild(pauseChip);
        for (int i = 0; i < WatchSpeeds.Length; i++)
        {
            double m = WatchSpeeds[i];
            var c = Chips.Make($"{m}x", new Vector2(x + 10 + i * 118, y + 122), new Vector2(104, 58), () => SetWatchSpeed(m), 22);
            hud.AddChild(c);
            speedChips.Add((c, m));
        }
        SetWatchSpeed(1);
    }

    void TogglePause()
    {
        watchPaused = !watchPaused;
        if (pauseChip != null) pauseChip.GetNode<Label>("Text").Text = watchPaused ? "PLAY" : "PAUSE";
    }

    void SetWatchSpeed(double m)
    {
        watchMult = m;
        Godot.Engine.TimeScale = m / pace;
        foreach (var (c, cm) in speedChips) c.Modulate = cm == m ? Colors.White : new Color(1, 1, 1, 0.45f);
    }

    /// The duel just ended (or was given up): keep it for SAVE REPLAY. Not for puzzles (their board is placed by
    /// hand after the setup, so the log alone can't rebuild it) or the labs.
    void KeepReplay(int winner, string reason)
    {
        Session.LastReplay = null;
        if (Watch != null || scenario != null || backToLab || Session.Puzzle != null) return;
        var r = Replay.Of(D, Me, winner, reason);
        r.You = Settings.PlayerName; r.Them = ThemName;
        r.Rival = Lan.On ? null : Rival.File;
        r.Mode = Lan.On ? "LAN" : Session.Tour != null ? "Tournament" : Session.Match ? $"Match {Session.MatchWins + Session.MatchLosses + 1}" : Session.Mode.ToString();
        (r.Mat, r.Mat2, r.Back, r.Back2) = (dressMat, dressMat2, dressBack, dressBack2);
        Session.LastReplay = r;
        if (saveReplay) { GD.Print($"replay: saved {r.Save(Session.ReplayDir)} ({r.Log.Count} commands, hash {r.Hash:x})"); GetTree().Quit(); }
    }

    /// The log ran out before the duel ended (a surrender or a hang-up): show how it ended, then back to the list.
    async Task EndOfReplay()
    {
        var r = Watch!;
        string them = r.Them.ToUpperInvariant();
        await EndTitle(r.Winner == 0 ? "VICTORY" : r.Winner == 1 ? "DEFEAT" : "DRAW", r.Winner == 1 ? Bad : RivalPanels.Gold, r.Winner != 1,
            r.Reason.ToUpperInvariant() + (r.Winner == 0 ? $"  ·  {them} DEFEATED" : r.Winner == 1 ? $"  ·  {them} WINS" : ""));
        await Wait(1.5);
        LeaveReplay();
    }

    void LeaveReplay()
    {
        Godot.Engine.TimeScale = 1;
        if (watchDev) { GD.Print($"replay: watched {watchAt}/{Watch!.Log.Count} commands, hash {D.Hash():x} {(D.Hash() == Watch.Hash ? "MATCHES" : "DIFFERS")}"); GetTree().Quit(); return; }
        Session.Watch = null;
        Transition.Go(this, "res://ui/Replays.tscn");
    }
}
