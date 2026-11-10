using Godot;
using PoC.Engine;

namespace PoC.Game.UI;

/// Replays (2026-11-09): the duels you saved (SAVE REPLAY on Results / the LAN lobby), newest first, as pages of rows
/// in the light; the chosen one in the dark: who, how it ended, WATCH and DELETE. A replay this build no longer
/// plays the same way (made before a card fix) says so instead of WATCH.
public partial class Replays : RiftMenu
{
    List<(string Path, Replay R)> all = new();
    Control list = null!, detail = null!;
    int sel;

    protected override string? MusicPath => Md("solo_gate");
    protected override void OnBack() => Transition.Go(this, "res://ui/MainMenu.tscn");

    protected override void Build()
    {
        Demo.Ensure();
        Session.Watch = null;
        Rift(0.56f, 0.5f);
        Back();
        Heading("REPLAYS", new Vector2(80, 60), 46);
        list = new Control { Size = new Vector2(W, H), MouseFilter = MouseFilterEnum.Ignore };
        detail = new Control { Size = new Vector2(W, H), MouseFilter = MouseFilterEnum.Ignore };
        AddChild(list); AddChild(detail);
        all = Replay.List(Session.ReplayDir);
        Refresh();
    }

    void Refresh()
    {
        foreach (var c in list.GetChildren()) c.QueueFree();
        foreach (var c in detail.GetChildren()) c.QueueFree();
        if (all.Count == 0)
        {
            Put(list, "NO REPLAYS YET", Title, 28, Graphite, new Vector2(110, 240));
            Put(list, "After a duel, press SAVE REPLAY on the results screen.", Body, 20, Graphite, new Vector2(110, 290));
            return;
        }
        sel = Math.Clamp(sel, 0, all.Count - 1);
        const float rowH = 84;
        int rows = Math.Max(1, (int)((H - 380) / rowH));
        Paged(list, new Rect2(110, 220, W * 0.42f, H - 340), all.Count, 1, rows, new Vector2(W * 0.42f, rowH), (i, host, at) =>
        {
            var r = all[i].R;
            bool on = i == sel;
            if (on) Pip(host, at + new Vector2(-24, 20), 10);
            var l = Put(host, r.Title.ToUpperInvariant(), Title, 24, on ? GoldDeep : Ink, at, new Vector2(W * 0.4f, 36));
            Put(host, $"{Result(r)}  ·  {r.Mode}  ·  {r.When}", Body, 17, Graphite, at + new Vector2(0, 38), new Vector2(W * 0.4f, 26));
            int k = i;
            Click(l, () => { sel = k; Refresh(); }, h => l.AddThemeColorOverride("font_color", h || k == sel ? GoldDeep : Ink));
        }, new Vector2(110, H - 100));
        Detail(all[sel]);
    }

    static string Result(Replay r) => r.Winner switch { 0 => "WON", 1 => "LOST", _ => "DRAW" };

    void Detail((string Path, Replay R) e)
    {
        var r = e.R;
        float x = W * 0.6f, w = W - x - 80, y = H * 0.16f;
        Put(detail, r.Title.ToUpperInvariant(), Title, 40, Silver, new Vector2(x, y - 70), new Vector2(w, 60));
        Put(detail, $"{r.You.ToUpperInvariant()}  VS  {r.Them.ToUpperInvariant()}", Title, 26, Mist, new Vector2(x, y + 10), new Vector2(w, 40));
        Put(detail, $"{Result(r)}  ·  {r.Reason}", Title, 22, r.Winner == 0 ? GoldLight : Mist, new Vector2(x, y + 76), new Vector2(w, 34));
        Put(detail, $"{r.Mode}  ·  {r.Turns} turns  ·  {r.When}", Body, 20, Mist, new Vector2(x, y + 120), new Vector2(w, 30));
        bool plays = r.Plays();
        if (plays) Button(detail, "WATCH", new Vector2(x + 140, H - 160), () => Watch(r), 30);
        else Put(detail, "Saved by an older version: cards have changed since, so it can't be played back.", Body, 20, Mist, new Vector2(x, H - 190), new Vector2(w, 60)).AutowrapMode = TextServer.AutowrapMode.WordSmart;
        Button(detail, "RENAME", new Vector2(x + (plays ? 640 : 360), H - 160), () => NamePrompt("RENAME THIS REPLAY", r.Title, name =>
        {
            r.Name = name;
            try { r.Write(e.Path); } catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { GD.PrintErr(ex.Message); }
            Refresh();
        }), 20);
        bool sure = false;
        Control del = null!;
        del = Button(detail, "DELETE", new Vector2(x + (plays ? 420 : 140), H - 160), () =>
        {
            if (!sure) { sure = true; del.GetChild<Label>(0).Text = "SURE?"; return; }
            if (OS.MoveToTrash(e.Path) == Error.Ok) { all.Remove(e); Refresh(); }   // the recycle bin, not gone for good
        }, 20);
    }

    void Watch(Replay r)
    {
        Session.ClearMode();
        Session.Tour = null;
        Session.Match = false;
        int i = Array.FindIndex(RivalPanels.All, x => x.File == r.Rival);
        Session.Selected = i < 0 ? 0 : i;
        Session.Watch = r;
        Transition.Go(this, "res://ui/field/DuelField.tscn");
    }
}
