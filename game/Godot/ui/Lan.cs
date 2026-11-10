using Godot;
using PoC.Engine;

namespace PoC.Game.UI;

/// A LAN duel in progress (Engine/LanLink.cs): the link, your seat (host 0, guest 1), the opponent's profile and
/// deck, and the setup the host dealt. Lives across scenes: lobby → coin toss → field → lobby (rematch).
public static class Lan
{
    public sealed record Peer(string Name, int Pic, string Mat, string Back, string Frame, string List, string Build);

    public static LanLink? Link;
    public static bool On => Link != null;
    public static int Seat => Link?.IsHost == false ? 1 : 0;
    public static Peer? Them;
    public static int[] TheirMain = Array.Empty<int>(), TheirExtra = Array.Empty<int>();
    public static ulong Seed;
    public static int First;                 // engine seat that goes first
    public static int LastWinner = -2;       // last LAN duel, 0 you / 1 them / -1 draw / -2 none

    public static string Name => Settings.PlayerName.ToUpperInvariant();
    public static string TheirName => Them?.Name.ToUpperInvariant() ?? "OPPONENT";
    public static Texture2D? TheirAvatar => Them != null && Settings.PictureChoices.Contains(Them.Pic) && Settings.PictureCard(Them.Pic) is { } c ? CardArt.Picture(c) : null;

    /// The opponent's icon frame (classic before they greet).
    public static string TheirFrame => Them?.Frame ?? Shop.DefaultFrame;

    public static LanLink.Msg Hello() => new()
    {
        T = "hello", Name = Settings.PlayerName, Pic = Settings.Picture, Mat = Session.Profile.Mat, Back = Session.Profile.Back, Frame = Session.Profile.Frame,
        List = DeckRules.LimitGame, Build = LanLink.Build,
    };

    public static void Greet(LanLink.Msg m) => Them = new Peer(m.Name ?? "Opponent", m.Pic, m.Mat ?? Shop.DefaultMat, m.Back ?? Shop.DefaultBack, m.Frame ?? Shop.DefaultFrame, m.List ?? "joey", m.Build ?? "");

    /// Same engine build and card data, or the two duels could drift apart.
    public static bool SameBuild => Them?.Build == LanLink.Build;

    /// The host's ban list rules the duel.
    public static string List => Link?.IsHost == false && Them != null ? Them.List : DeckRules.LimitGame;

    public static PoC.Engine.DuelSetup Setup(Deck mine)
    {
        var s = new PoC.Engine.DuelSetup { Seed = Seed, FirstPlayer = First, Classic = Session.Classic };
        s.Main[Seat].AddRange(mine.Main); s.Extra[Seat].AddRange(mine.Extra);
        s.Main[1 - Seat].AddRange(TheirMain); s.Extra[1 - Seat].AddRange(TheirExtra);
        return s;
    }

    /// The opponent's deck as sent: real card ids, 40-80 main cards, Fusions only in the Fusion Deck.
    public static bool Sane(int[] main, int[] extra) =>
        main.Length is >= 40 and <= 80 && extra.Length <= 15 && main.All(CardDb.Shared.ByIdx.ContainsKey) && extra.All(i => CardDb.Shared.ByIdx.TryGetValue(i, out var d) && d.IsFusion);

    public static void Close()
    {
        Link?.Dispose();
        Link = null; Them = null; LastWinner = -2;
    }
}
