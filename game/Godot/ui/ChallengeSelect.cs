using Godot;
using PoC.Engine;

namespace PoC.Game.UI;

/// Challenges (2026-11-08): the puzzles as a numbered list in the light (a gold diamond = solved); the focused one in
/// the dark: name, goal, their LP, your hand, the reward, START. A puzzle starts straight on the field (no coin toss).
public partial class ChallengeSelect : RiftMenu
{
    readonly List<Label> names = new();
    Control detail = null!;
    int sel;

    protected override string? MusicPath => Md("solo_gate");
    protected override int ItemCount => Puzzles.All.Length;
    protected override void OnBack() => Transition.Go(this, "res://ui/DuelModes.tscn");

    protected override void Build()
    {
        Demo.Ensure();
        Session.Mode = GameMode.Challenges;
        Rift(0.5f, 0.44f);
        Back("MODES");
        Put(this, "CHALLENGES", Title, 40, Ink, new Vector2(80, 58));
        Hair(this, new Vector2(82, 108), 160, GoldC);
        Put(this, $"SOLVED {Puzzles.All.Count(z => P.Solved.Contains(z.Id))} / {Puzzles.All.Length}", Thin, 18, GoldDeep, new Vector2(84, 124));
        float y = H * 0.2f;
        for (int i = 0; i < Puzzles.All.Length; i++)
        {
            var z = Puzzles.All[i];
            var l = Put(this, $"{i + 1:00}   {z.Name}", Title, 30, Ink * new Color(1, 1, 1, 0.6f), new Vector2(130, y), new Vector2(640, 46));
            Hook(l, i);
            l.MouseDefaultCursorShape = CursorShape.PointingHand;
            if (P.Solved.Contains(z.Id)) Pip(this, new Vector2(104, y + 22), 10);
            names.Add(l);
            y += 80;
        }
        detail = new Control { Size = new Vector2(W, H), MouseFilter = MouseFilterEnum.Ignore };
        AddChild(detail);
        int first = Array.FindIndex(Puzzles.All, z => !P.Solved.Contains(z.Id));
        SetFocus(first < 0 ? 0 : first);
    }

    protected override void OnFocus(int old, int now)
    {
        if (now < 0) return;
        sel = now;
        for (int i = 0; i < names.Count; i++) names[i].AddThemeColorOverride("font_color", i == now ? GoldDeep : Ink * new Color(1, 1, 1, 0.6f));
        foreach (var c in detail.GetChildren()) c.QueueFree();
        var z = Puzzles.All[now];
        float x = W * 0.56f, w = W - x - 80;
        Put(detail, z.Name, Title, 52, Silver, new Vector2(x, H * 0.16f), new Vector2(w, 70));
        Put(detail, z.Goal, Body, 26, Mist, new Vector2(x, H * 0.16f + 84), new Vector2(w, 40));
        Put(detail, z.Limit, Title, 18, GoldLight, new Vector2(x, H * 0.16f + 140));
        Put(detail, "THEIR LP", Thin, 15, Mist, new Vector2(x, H * 0.16f + 196));
        Put(detail, $"{z.TheirLp:N0}", Title, 44, Silver, new Vector2(x, H * 0.16f + 218));
        Put(detail, "YOUR HAND", Thin, 15, Mist, new Vector2(x, H * 0.16f + 300));
        float cx = x;
        foreach (var n in z.Hand)
        {
            var d = Db.Get(Db.Canon(Db.Named(n).Idx));
            Card(detail, d, new Vector2(cx + 60, H * 0.16f + 420), 170, 0, 0.8f);
            cx += 134;
        }
        bool solved = P.Solved.Contains(z.Id);
        Put(detail, solved ? "SOLVED" : $"REWARD  {z.Dp} DP", Title, 18, GoldLight, new Vector2(x, H * 0.16f + 530));
        Button(detail, solved ? "PLAY AGAIN" : "START", new Vector2(x + 180, H - 100), () => Choose(sel), 28);
    }

    protected override void OnChoose(int i)
    {
        Session.Puzzle = Puzzles.All[i];
        Session.Match = false;
        Session.Selected = 1;   // Kaiba keeps the other side of the table
        Session.ModeDeck = null;
        Transition.Go(this, "res://ui/field/DuelField.tscn");
    }
}
