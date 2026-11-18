using Godot;
using PoC.Engine;

namespace PoC.Game.UI;

/// Gauntlet (Menu Lab pick 2026-11-16): the eight floors as steps climbing left to right, each with its rival and the
/// prize for beating it (Gauntlet.Floors: the whole prize, not added up). The next floor holds DUEL; top left, what
/// the run pays if it ends now. Shown when a run starts and between its duels. `-- --demo --floor=N` for screenshots.
public partial class GauntletScreen : RiftMenu
{
    protected override string? MusicPath => Md("solo_gate");
    protected override void OnBack() => Transition.Go(this, "res://ui/DuelModes.tscn");

    protected override void Build()
    {
        Demo.Ensure();
        if (Session.Run == null)
        {
            Session.Mode = GameMode.Gauntlet;
            Session.Run = Gauntlet.Climb(Tournament.Roster, new Rng(3));
            if (int.TryParse(Demo.Arg("floor", "0"), out var f)) for (int i = 0; i < f; i++) Session.Run.Record(true, 8000 - 900 * (i + 1));
        }
        var run = Session.Run;
        Rift(0.5f, 0.42f);
        Back("MODES");
        Put(this, "GAUNTLET", Title, 38, Ink, new Vector2(80, 58));
        Hair(this, new Vector2(82, 106), 140, GoldC);
        var (dp, cards) = run.Prize;
        Put(this, run.Index == 0 ? Lang.Tr("BEAT FLOOR 1 TO WIN YOUR FIRST PRIZE") : Lang.F("IF THE RUN ENDS NOW  ·  FLOOR {0} BEATEN", run.Index), Thin, 14, Graphite, new Vector2(84, 130));
        if (run.Index > 0) Put(this, Lang.F("{0:N0} DP", dp) + (cards > 0 ? (cards == 1 ? Lang.Tr("  +  1 card") : Lang.F("  +  {0} cards", cards)) : ""), Title, 18, GoldDeep, new Vector2(84, 152));
        Put(this, "You take the prize of the highest floor you beat. Climb higher, it grows.", Body, 17, Graphite, new Vector2(84, run.Index > 0 ? 190 : 158));
        Put(this, Lang.F("YOUR LP  {0:N0}", run.Lp), Title, 20, Graphite, new Vector2(84, run.Index > 0 ? 226 : 194));

        int n = run.Order.Length;
        float x0 = 90, stepW = (W - 180) / n, baseY = H - 70, rise = (H - 420) / n;
        for (int i = 0; i < n; i++)
        {
            var o = run.Order[i];
            var d = Tournament.Get(o.Rival);
            float x = x0 + i * stepW, top = baseY - (i + 1) * rise - 120;
            bool done = i < run.Index, now = i == run.Index && !run.Over;
            var c = new Vector2(x + stepW / 2, top);
            bool light = OnLight(c);
            Plate(this, new Rect2(x + 6, top + 70, stepW - 12, baseY - top - 70), now ? Box(light ? new Color(1f, 0.95f, 0.85f) : new Color(0.16f, 0.13f, 0.08f, 0.95f), GoldC, 2.5f, 10)
                : Box(light ? new Color(1, 1, 1, done ? 0.45f : 0.85f) : new Color(1, 1, 1, done ? 0.05f : 0.1f), new Color(0.5f, 0.5f, 0.5f, 0.15f), 1, 10));
            Face(this, c, now ? 92 : 78, o.Rival, now || done, done || now ? 1 : 0.6f);
            var ink = light ? Ink : Silver; var gold = light ? GoldDeep : GoldLight;
            Fit(this, d.Name, Title, 14, 10, now ? gold : ink, new Rect2(x + 8, top + 80, stepW - 16, 22));
            Fit(this, Lang.F("FLOOR {0}", i + 1) + "  ·  " + Lang.Tr(o.Tier), Thin, 11, 9, ink * new Color(1, 1, 1, 0.6f), new Rect2(x + 10, top + 104, stepW - 20, 18));
            var (fdp, fc) = Gauntlet.Floors[Math.Min(i, Gauntlet.Floors.Length - 1)];
            Fit(this, Lang.F("{0:N0} DP", fdp), Title, 16 + i * 2, 12, done ? ink * new Color(1, 1, 1, 0.45f) : gold, new Rect2(x + 8, top + 136, stepW - 16, 40));
            // A card every second floor (user 2026-11-17): the card line shows only where the count goes up (floors 2, 4, 6, 8);
            // the payout itself still counts every card earned below.
            if (fc > 0 && (i == 0 || Gauntlet.Floors[i - 1].Cards < fc)) Mid(this, fc == 1 ? Lang.Tr("+ 1 CARD") : Lang.F("+ {0} CARDS", fc), Title, 13 + fc, done ? ink * new Color(1, 1, 1, 0.45f) : gold, new Rect2(x, top + 182, stepW, 22));
            if (done) Tag(this, "✓", new Vector2(x + stepW / 2 + 40, top - 34), GoldC, Ink, 12);
            if (now) Button(this, "DUEL", new Vector2(x + stepW / 2, baseY - 40), () => Transition.Go(this, "res://ui/DuelSetup.tscn"), 22, primary: true);
        }
    }
}
