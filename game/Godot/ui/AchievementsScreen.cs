using Godot;
using PoC.Engine;

namespace PoC.Game.UI;

/// Achievements (Menu Lab pick 2026-11-16): one list in the light, finished ones first with CLAIM in the row and a
/// gold bar on their left edge; pointing at a row shows its prize large in the dark (card back, playmat, icon frame or
/// DP) with what it takes and CLAIM. Goals and prizes: Engine/Achievements.cs.
public partial class AchievementsScreen : RiftMenu
{
    Control body = null!, spot = null!;
    Achievements.Def? focus;

    protected override string? MusicPath => Md("menu_02");
    protected override void OnBack() => Transition.Go(this, "res://ui/MainMenu.tscn");
    int Decks => Session.Decks.Count;

    protected override void Build()
    {
        Demo.Ensure();
        Rift(0.6f, 0.55f);
        Back();
        HeadTabs(RecordTabs, 0, new Vector2(80, 54));
        body = new Control { Size = new Vector2(W, H), MouseFilter = MouseFilterEnum.Ignore };
        spot = new Control { Size = new Vector2(W, H), MouseFilter = MouseFilterEnum.Ignore };
        AddChild(body); AddChild(spot);
        Fill();
    }

    void Claim(Achievements.Def a)
    {
        if (!Achievements.Claim(a, P, Decks)) return;
        Session.Save();
        Flash(a.Kind == Achievements.Prize.Dp ? Lang.F("+{0} DP", a.Dp) : PrizeName(a));
        Fill();
    }

    void Fill()
    {
        foreach (var c in body.GetChildren()) c.QueueFree();
        var all = Achievements.All;
        int done = all.Count(a => Achievements.Done(a, P, Decks));
        float lw = CrackX(H) - 84 - 80, rowH = 88;
        Put(body, Lang.F("{0} OF {1} DONE", done, all.Length), Thin, 15, Graphite, new Vector2(84, 136));
        Bar(body, new Vector2(84, 164), 260, done / (float)all.Length, 6);
        // ready to claim first, then in progress, then the ones already claimed
        var list = all.OrderBy(a => Achievements.Ready(a, P, Decks) ? 0 : Achievements.Claimed(a, P) ? 2 : 1).ToList();
        int rows = (int)((H - 280) / rowH);
        Paged(body, new Rect2(84, 200, lw, rows * rowH), list.Count, 1, rows, new Vector2(0, rowH), (i, host, at) =>
        {
            var a = list[i];
            bool isDone = Achievements.Done(a, P, Decks), ready = Achievements.Ready(a, P, Decks), claimed = Achievements.Claimed(a, P);
            float h = rowH - 8, cy = h / 2;
            var bg = Plate(host, new Rect2(at, new Vector2(lw, h)), Box(new Color(1, 1, 1, 0), default, 0, 10));
            if (ready) Plate(host, new Rect2(at + new Vector2(0, 10), new Vector2(4, h - 20)), Box(GoldC, default, 0, 99));
            Hair(host, at + new Vector2(0, rowH - 4), lw, Ink * new Color(1, 1, 1, 0.07f), 1);
            Medal(host, at + new Vector2(46, cy), 60, a, isDone);
            Put(host, a.Name, Title, 16, isDone ? GoldDeep : Ink, at + new Vector2(92, cy - 28));
            var desc = Put(host, a.Desc, Body, 16, Graphite, at + new Vector2(92, cy + 2), new Vector2(lw - 420, 24));
            desc.ClipText = true;
            float px = lw - 310;
            if (ready) Button(host, "CLAIM", at + new Vector2(px + 90, cy), () => Claim(a), 13, primary: true);
            else if (claimed) Mid(host, "✓  " + Lang.Tr("YOURS"), Title, 12, GoldDeep, new Rect2(at + new Vector2(px, cy - 12), new Vector2(180, 24)));
            else
            {
                Bar(host, at + new Vector2(px, cy - 6), 180, Achievements.Progress(a, P, Decks) / (float)a.Goal, 6);
                Mid(host, Progress(a), Title, 11, Graphite, new Rect2(at + new Vector2(px, cy + 4), new Vector2(180, 20)));
            }
            PrizeArt(host, at + new Vector2(lw - 56, cy), 62, a, isDone, false);
            bg.MouseFilter = MouseFilterEnum.Pass;
            Click(bg, () => { if (ready) Claim(a); }, hv => { bg.AddThemeStyleboxOverride("panel", Box(hv ? new Color(0, 0, 0, 0.035f) : new Color(1, 1, 1, 0), default, 0, 10)); if (hv) Focus(a); });
        }, new Vector2(84 + lw - 260, H - 60));
        Focus(focus ?? list[0]);
    }

    string Progress(Achievements.Def a) => a.Goal == 1 ? Lang.Tr(Achievements.Done(a, P, Decks) ? "DONE" : "NOT YET") : $"{Achievements.Progress(a, P, Decks)} / {a.Goal}";

    /// A rounded progress bar (light side).
    void Bar(Control host, Vector2 at, float w, float frac, float h)
    {
        Plate(host, new Rect2(at, new Vector2(w, h)), Box(new Color(0, 0, 0, 0.1f), default, 0, 99));
        if (frac > 0) Plate(host, new Rect2(at, new Vector2(Math.Max(h, w * Math.Min(1, frac)), h)), Box(GoldDeep, default, 0, 99));
    }

    /// The prize of the row you point at, large in the dark.
    void Focus(Achievements.Def a)
    {
        focus = a;
        foreach (var c in spot.GetChildren()) c.QueueFree();
        bool isDone = Achievements.Done(a, P, Decks);
        float cx = (CrackX(H * 0.5f) + W) / 2 + 20, y = H * 0.66f;
        PrizeArt(spot, new Vector2(cx, H * 0.4f), a.Kind == Achievements.Prize.Mat ? 260 : a.Kind == Achievements.Prize.Frame ? 240 : 360, a, isDone);
        Mid(spot, PrizeName(a), Title, 24, isDone ? GoldLight : Silver, new Rect2(cx - 330, y, 660, 34));
        if (a.Kind != Achievements.Prize.Dp) Mid(spot, Lang.F("+ {0:N0} DP", a.Dp), Title, 16, Mist, new Rect2(cx - 300, y + 38, 600, 24));
        Mid(spot, a.Desc, Body, 19, Mist, new Rect2(cx - 330, y + 74, 660, 26));
        if (Achievements.Ready(a, P, Decks)) Button(spot, "CLAIM", new Vector2(cx, y + 146), () => Claim(a), 22, primary: true);
        else if (Achievements.Claimed(a, P)) Mid(spot, "✓  " + Lang.Tr("YOURS"), Title, 18, GoldLight, new Rect2(cx - 180, y + 124, 360, 28));
        else
        {
            var track = Plate(spot, new Rect2(cx - 180, y + 124, 360, 8), Box(new Color(1, 1, 1, 0.1f), default, 0, 99));
            float f = Achievements.Progress(a, P, Decks) / (float)a.Goal;
            if (f > 0) Plate(spot, new Rect2(cx - 180, y + 124, Math.Max(8, 360 * f), 8), Box(GoldLight, default, 0, 99));
            Mid(spot, Progress(a), Title, 14, Mist, new Rect2(cx - 180, y + 140, 360, 22));
        }
    }

    protected override void OnFocus(int old, int now) { }
    protected override void OnChoose(int i) { }
}
