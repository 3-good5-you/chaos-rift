using Godot;
using PoC.Engine;

namespace PoC.Game.UI;

/// Duel, step 1 (Chaos Rift sample B, locked 2026-11-08): the rivals' names as a list in the light; the focused
/// rival stands large in the dark with their difficulty once, top right. Clicking a name goes straight to step 2.
public partial class RivalSelect : RiftMenu
{
    readonly List<Label> names = new();
    TextureRect portrait = null!;
    Label tier = null!, dots = null!;
    bool leaving;

    protected override string? MusicPath => Md("solo_gate");
    protected override int ItemCount => RivalPanels.All.Length;

    protected override void Build()
    {
        Demo.Ensure();
        if (Session.Picks.Length != RivalPanels.All.Length) Session.RollPicks();
        Rift(0.46f, 0.4f);
        Back();
        string head = Session.Mode switch { GameMode.Legend => "LEGEND  ·  CHOOSE YOUR RIVAL", GameMode.Pack => "PACK DUEL  ·  CHOOSE YOUR RIVAL", GameMode.Limited => "LIMITED DUEL  ·  CHOOSE YOUR RIVAL", GameMode.Chaos => "CHAOS DUEL  ·  CHOOSE YOUR RIVAL", _ => "CHOOSE YOUR RIVAL" };
        Put(this, head, Title, 34, Ink, new Vector2(80, 58));
        Hair(this, new Vector2(82, 102), 140, GoldC);
        portrait = Pic(this, null, Vector2.Zero, Vector2.One);
        tier = Put(this, "", Title, 22, Colors.White, new Vector2(W - 380, 70), new Vector2(320, 30), HorizontalAlignment.Right);
        dots = Put(this, "", Body, 18, Colors.White, new Vector2(W - 380, 104), new Vector2(320, 24), HorizontalAlignment.Right);
        float y = H * 0.3f;
        for (int i = 0; i < RivalPanels.All.Length; i++)
        {
            var l = Put(this, RivalPanels.All[i].Name, Title, 44, Ink * new Color(1, 1, 1, 0.55f), new Vector2(120, y), new Vector2(560, 80));
            l.PivotOffset = new Vector2(0, 40);
            Hook(l, i);
            l.MouseDefaultCursorShape = CursorShape.PointingHand;
            names.Add(l);
            y += 110;
        }
        SetFocus(Math.Clamp(Session.Selected, 0, RivalPanels.All.Length - 1));
    }

    ColorRect? pip;

    protected override void OnFocus(int old, int now)
    {
        if (leaving || now < 0) return;
        for (int i = 0; i < names.Count; i++)
        {
            bool on = i == now;
            names[i].AddThemeColorOverride("font_color", on ? GoldDeep : Ink * new Color(1, 1, 1, 0.55f));
            Ink_.Only(this, names[i]).TweenProperty(names[i], "scale", on ? new Vector2(1.35f, 1.35f) : Vector2.One, 0.2).SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
        }
        pip ??= Pip(this, Vector2.Zero, 14);
        pip.Position = names[now].Position + new Vector2(-38, 34);
        var r = RivalPanels.All[now];
        var tex = RivalPanels.Portrait(r);
        float ph = H * 0.84f, pw = ph * tex.GetWidth() / tex.GetHeight();
        portrait.Texture = tex;
        portrait.Size = new Vector2(pw, ph);
        var home = new Vector2(W * 0.68f - pw / 2, H - ph);
        portrait.Position = home + new Vector2(30, 0);
        portrait.Modulate = new Color(1, 1, 1, 0);
        var tw = Ink_.Only(this, portrait).SetParallel();
        tw.TweenProperty(portrait, "position", home, 0.3).SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
        tw.TweenProperty(portrait, "modulate:a", 1f, 0.25);
        var pick = Session.Mode == GameMode.Legend ? Modes.Legend(r.File) : Session.Picks[now];
        var c = Session.TierColor(pick.Level);
        tier.Text = pick.Tier; tier.AddThemeColorOverride("font_color", c);
        dots.Text = new string('●', pick.Level); dots.AddThemeColorOverride("font_color", c);
    }

    protected override void OnChoose(int i)
    {
        if (leaving) return;
        leaving = true;
        Session.Selected = i;
        Session.Tour = null;
        Transition.Go(this, "res://ui/DuelSetup.tscn");
    }

    protected override void OnBack() => Transition.Go(this, Session.ModeDeck != null ? "res://ui/DeckBuilder.tscn" : "res://ui/DuelModes.tscn");
}
