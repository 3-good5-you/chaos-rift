using Godot;
using PoC.Engine;

namespace PoC.Game.UI;

/// FX Lab "banish": the user kept the pile behind the Deck, off the table (pick D, 2026-10-29); the sample replays it.
public partial class DuelField
{
    async Task BanishNow()
    {
        var m = Of(0, "Dark Magician"); Place(m, Mon(m, 2, 0));
        var g = Of(0, "Summoned Skull"); Place(g, GyOf(0));
        var o = Of(0, "Celtic Guardian"); Place(o, new St(Loc.Banished, 0, 3, true, false, 0, 0, 0));
        foreach (var x in banishMarks[0]) x.Visible = true;
        await Wait(0.5);
        Move(m, new St(Loc.Banished, 1, 3, true, false, 0, 0, 0), 0, 0.45f, 0.4f); await Wait(0.6);
        Move(g, new St(Loc.Banished, 2, 3, true, false, 0, 0, 0), 0, 0.45f, 0.4f); await Wait(1.2);
    }
}
