using Godot;
using PoC.Engine;

namespace PoC.Game.UI;

/// LAN duel (ui/Lan.cs): lockstep over the command log. Your answers go out as they are made, the opponent's
/// arrive as `cmd` messages and are submitted to this PC's copy of the duel. At every turn start both sides send
/// a state hash; a mismatch, a nonsensical answer or a hang-up ends the duel.
public partial class DuelField
{
    string ThemName => Lan.On ? Lan.TheirName : Rival.Name;

    sealed class LanEnded : Exception
    {
        public readonly bool Desync;
        public LanEnded(string why, bool desync) : base(why) => Desync = desync;
    }

    bool lanOver, remoteWait;
    int hashedTurn = -1;
    readonly Dictionary<int, ulong> ourHash = new(), theirHash = new();

    /// The opponent's next answer (hashes that arrive meanwhile are checked).
    async Task<Command> Remote()
    {
        remoteWait = true;
        while (true)
        {
            var m = await Lan.Link!.Read();
            if (m == null || m.T == "bye") throw new LanEnded("OPPONENT LEFT", false);
            if (m.T == "hash") { theirHash[m.Turn] = m.Hash; Compare(m.Turn); }
            else if (m.T == "cmd") { remoteWait = false; return new Command(m.P, m.Picks ?? Array.Empty<int>()); }
        }
    }

    void SendHash()
    {
        if (D.Turn == hashedTurn) return;
        hashedTurn = D.Turn;
        ourHash[D.Turn] = D.Hash();
        Lan.Link!.Send(new LanLink.Msg { T = "hash", Turn = D.Turn, Hash = ourHash[D.Turn] });
        Compare(D.Turn);
    }

    void Compare(int turn)
    {
        if (ourHash.TryGetValue(turn, out var a) && theirHash.TryGetValue(turn, out var b) && a != b) throw new LanEnded("OUT OF SYNC", true);
    }

    /// Hang-ups while it is your move (while waiting on the opponent, Remote notices them itself).
    void LanWatch()
    {
        if (Lan.On && !lanOver && !remoteWait && Lan.Link!.Closed) LanOver("OPPONENT LEFT", false);
    }

    /// The duel can't go on: say why, keep the logs of a desync for debugging, back to the lobby.
    async void LanOver(string why, bool desync)
    {
        if (lanOver) return;
        lanOver = true;
        if (desync)
        {
            var path = ProjectSettings.GlobalizePath($"user://lan_desync_{DateTime.Now:yyyyMMdd_HHmmss}.txt");
            var s = D.Setup;
            System.IO.File.WriteAllText(path, $"seat {Me} seed {s.Seed} first {s.FirstPlayer}\nmain0 {string.Join(",", s.Main[0])}\nextra0 {string.Join(",", s.Extra[0])}\n" +
                $"main1 {string.Join(",", s.Main[1])}\nextra1 {string.Join(",", s.Extra[1])}\nlog {string.Join(" ", D.Log)}\nstate {D.StateHash()}\n");
            GD.PrintErr($"LAN desync, logs in {path}");
        }
        if (autoplay) GD.Print($"lan: ended, {why}");
        Lan.LastWinner = desync ? -2 : 0;   // the one who stays wins
        if (!desync) KeepReplay(0, "Opponent left");
        Lan.Close();
        await Banner(why, Pm.Them, desync ? "THE TWO DUELS DRIFTED APART" : "YOU WIN BY DEFAULT");
        await Wait(2);
        Transition.Go(this, "res://ui/LanLobby.tscn");
    }
}
