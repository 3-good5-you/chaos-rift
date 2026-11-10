using Godot;
using PoC.Engine;

namespace PoC.Game.UI;

/// `--uilab`, round 4: Ground Collapse, picked 2026-10-29 (closed zone = B cracked ground, zone pick = A red boxes)
/// and now built live. Its sound, picked after: D, the earthquake. The live version plays here.
public partial class DuelField
{
    void UiLabZones()
    {
        void Add(string name, params (string, Func<Task>)[] vars) => labEvents.Add(("BATTLE UI · ROUND 4", name, vars));
        // Ground Collapse closes two zones; here one of theirs and one of yours.
        (int Side, int Z)[] closing = { (1, 1), (0, 3) };

        async Task Collapse()
        {
            PutMon("Vorse Raider", 2, 1); PutMon("Celtic Guardian", 2, 0);
            var s = Put("Ground Collapse", SpellZ(2, 0, true)); await Wait(0.3);
            GoldRays(s, FxRed); await Wait(0.7);
        }
        async Task Close()
        {
            foreach (var (s, z) in closing) { blockMarks[s, z].Visible = true; ZoneCollapse(blockMarks[s, z]); await Wait(1.4); }
        }
        async Task Done(double hold)
        {
            await Hold(hold);
            foreach (var bm in blockMarks) bm.Visible = false;
        }

        Add("1  Closed Monster Zone: sound",
            ("Live: earthquake (Convulsion of Nature) + rumble  ★", async () => { await Collapse(); await Close(); await Done(2.5); }));

        // Activation: each plays a monster, a Spell and a Trap activating, twice.
        async Task Activations()
        {
            var vs = new[] { PutMon("Celtic Guardian", 2, 0), Put("Ground Collapse", SpellZ(1, 0, true)), Put("Trap Hole", SpellZ(3, 1, true)) };
            Color?[] tints = { null, FxGreen, FxPink };
            await Wait(0.4);
            for (int k = 0; k < 2; k++) for (int i = 0; i < vs.Length; i++) { GoldRays(vs[i], tints[i]); await Wait(1.3); }
            await Hold(0.3);
        }
        Add("2  Effect activation (no lift)",
            ("Live: rays + star flash + light sweep, no lift  ★", async () => await Activations()));
    }
}
