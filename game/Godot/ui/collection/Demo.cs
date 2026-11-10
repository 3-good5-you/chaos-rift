using Godot;
using PoC.Engine;

namespace PoC.Game.UI;

/// Screenshot/demo data (`-- --demo`): Kaiba's starter, a seeded spread of reward cards (every card with
/// an HD scan among them), a few NEW, and two decks. Only fills an empty profile.
public static class Demo
{
    public static bool On => OS.GetCmdlineUserArgs().Contains("--demo");

    public static string Arg(string key, string def = "")
    {
        foreach (var a in OS.GetCmdlineUserArgs()) if (a.StartsWith($"--{key}=")) return a[(key.Length + 3)..];
        return def;
    }

    public static void Ensure()
    {
        if (!On || !Session.NeedsStarter) return;
        var p = Session.Profile;
        var db = CardDb.Shared;
        var starter = DeckRules.GrantStarter(p, 1);
        var rng = new Rng(11);
        var pool = db.ByIdx.Values.Where(d => d.PackMask != 0).OrderBy(d => d.Idx).ToList();
        for (int i = 0; i < 220; i++) { var d = pool[rng.Int(pool.Count)]; p.Owned[d.Idx] = Math.Min(3, p.Count(d.Idx) + 1); }
        var hd = Directory.GetFiles(ProjectSettings.GlobalizePath("res://") + "../../remake/samples/img", "hd-*.jpg")
            .Select(f => int.Parse(Path.GetFileNameWithoutExtension(f)[3..])).ToHashSet();
        foreach (var d in db.ByIdx.Values.Where(d => hd.Contains(d.Passcode))) p.Owned[d.Idx] = Math.Max(p.Count(d.Idx), 2);
        var owned = p.Owned.Keys.OrderBy(k => k).ToList();
        for (int i = 0; i < 14; i++) p.Fresh.Add(owned[rng.Int(owned.Count)]);
        p.Fresh.Add(db.Named("Blue-Eyes White Dragon").Idx);

        var dragon = starter.Copy("Dragon Fury");
        foreach (var n in new[] { "Blue-Eyes White Dragon", "Blue-Eyes White Dragon", "Blue-Eyes Ultimate Dragon" })
        {
            var c = db.Named(n);
            p.Owned[c.Idx] = Math.Max(p.Count(c.Idx), 2);
            DeckRules.Add(dragon, c, p);
        }
        dragon.Cover = db.Named("Blue-Eyes White Dragon").Idx;
        Session.Decks.Add(dragon);
        Session.Decks.Add(starter);
        Session.Playing = dragon;

        // Shop: some DP, cards seen from a few rival decks, a mat and two backs bought, tide + water equipped.
        p.Dp = 4850;
        foreach (var k in new[] { "yugi/cpu03", "kaiba/kaiba_kk", "joey/cpu_011", "kaiba/yk_kkk" })
            foreach (var i in db.Deck(k).Main) p.Seen.Add(i);
        p.Mats.Add("tide"); p.Backs.Add("06_water"); p.Backs.Add("11_hyperspace");
        p.Frames.Add("millennium"); p.Frames.Add("water");
        p.Mat = "tide"; p.Back = "06_water"; p.Frame = "millennium";
    }
}
