using Godot;

namespace PoC.Game.UI;

/// `--uilab`, round 11 (2026-11-09; picked: sky detail only, old LP box, playmat worlds kept). Round 10 picks, now built in: the soft dark gap between the two worlds, no
/// motes; the turn circle, effect prompt, pause menu and card search stay as they are. This round: the plain mats'
/// worlds (they were a bare gradient), detail round the table (drifting shards, a sky with wisps and stars), and
/// life points that keep the profile frame. Rows 1-2 reload the field with the option; row 3 draws over it.
/// H hides the lab panel. Picks go to user://ui_lab_picks_round11.json and game/ui_picks_round11.txt.
public partial class DuelField
{
    static string? labMat, labRivalMat;              // playmat overrides (survive the reload)
    static int labSeam = -1, labMotes = -1;          // kept for the lab; the picks are the defaults
    static int labSky = -1, labShards = -1;          // 0 = off
    readonly List<Control> labHidden = new();
    ScrollContainer labScroll = null!;
    VBoxContainer labBox = null!;

    void UiLabRift()
    {
        void Add(string name, params (string, Func<Task>)[] vars) => labEvents.Add(("DUEL LOOK · ROUND 11", name, vars));

        Func<Task> Look(System.Action set) => () => { set(); labReturn = labEvents[labSel].Name; GetTree().ReloadCurrentScene(); return Task.CompletedTask; };
        (string, Func<Task>) Mine(string label, string mat) => (label, Look(() => labMat = mat));
        void Hide(params Control[] cs) { foreach (var c in cs) if (c.Visible) { c.Visible = false; labHidden.Add(c); } }
        (string, Func<Task>) New(string label, string key) => (label, () =>
        {
            var n = DuelUiSamples.Embed(key);
            hud.AddChild(n); hud.MoveChild(n, labScroll.GetIndex()); uiTemp.Add(n);
            Hide(profile[0], profile[1]);
            return Task.CompletedTask;
        });

        Add("1  Your playmat and its world",
            Mine("Default arena (NEW world: gold-seamed stone)", "arena"), Mine("Rival's colours (NEW world: nebula)", "rival"),
            Mine("Dark Magician art (NEW world)", "dm"), Mine("Obsidian", "forge:obsidian"), Mine("Ivory", "forge:ivory"),
            Mine("Eclipse", "forge:eclipse"), Mine("Crimson", "forge:crimson"), Mine("Magma", "forge:magma"),
            Mine("Steel", "forge:steel"), Mine("Storm", "forge:storm"));
        Add("2  Detail round the table",
            ("Drifting shards + sky with wisps and stars", Look(() => { labShards = 1; labSky = -1; })),
            ("Sky detail only", Look(() => { labShards = 0; labSky = -1; })),
            ("Shards only", Look(() => { labShards = 1; labSky = 0; })),
            ("Neither (round 10)", Look(() => { labShards = 0; labSky = 0; })));
        Add("4  Special wins (watch only)",
            ("Destiny Board: F-I-N-A-L", async () =>
            {
                var cs = Letters.Select((n, i) => Put(n, SpellZ(i, 0, true)).Card).ToList();
                await Wait(0.5); await AltWinFinish(0, "Destiny Board", 0, cs);
            }),
            ("Exodius", async () => { var v = PutMon("Exodius the Ultimate Forbidden Lord", 2, 0); await Wait(0.5); await AltWinFinish(0, "Exodius", 0, new() { v.Card }); }),
            ("Vennominaga", async () => { var v = PutMon("Vennominaga the Deity of Poisonous Snakes", 2, 0); await Wait(0.5); await AltWinFinish(0, "Vennominaga", 0, new() { v.Card }); }),
            ("Flying Elephant", async () => { var v = PutMon("Flying Elephant", 2, 0); await Wait(0.5); await AltWinFinish(0, "Flying Elephant", 0, new() { v.Card }); }),
            ("Deck out", async () => await AltWinFinish(0, "deck out", 0, new())));
        Add("3  Life points, with your frame",
            ("Old: as now", () => Task.CompletedTask),
            New("Frame large, no box", "lpf1"), New("Slim plate, frame standing over its end", "lpf2"), New("Old box, menu type, gold LP line", "lpf3"));
    }
}
