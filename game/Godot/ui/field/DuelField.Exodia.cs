using Godot;
using PoC.Engine;

namespace PoC.Game.UI;

/// The original Exodia finish (FX Lab pick 2026-10-29: "Giant assembles" with the original game's Exodia as the final
/// form). Since the Master Duel version was locked (DuelField.ExodiaMd.cs) this plays only when its frames are missing.
/// The screen darkens, each piece burns out of the hand and lands big on screen in its place in the body (head, arms,
/// legs); the five flare and fade while the original game's Exodia (face and both arms) rises out of them, charges a
/// light ball between its hands and fires it at the loser (up the table, or straight at you).
public partial class DuelField
{
    // The original game's Exodia art (remake/extracted_png/yugi/y/duel/exodia): face and both arms in three poses
    // (2 = small, far away; 1 = charging; 3 = thrusting), the light ball.
    static readonly Dictionary<string, Texture2D> exoTex = new();
    static Texture2D ExoTex(string n) => exoTex.TryGetValue(n, out var t) ? t
        : exoTex[n] = ImageTexture.CreateFromImage(ResImage($"res://assets/exodia/{n}.png"));

    /// In a duel: the winner's five pieces, then the loser's field shatters (the caller shows the banner).
    async Task ExodiaFinish(int side)
    {
        var pieces = Duel.Exodia.Select(n => D.P(D.Winner).Hand.First(c => c.Name == n)).Select(c => views[c.Uid]).ToList();
        if (ExodiaNativeInDuels ? MdNativeThere : MdFilmThere) await ExodiaFinalMd(pieces, ExodiaNativeInDuels); else await ExodiaFinal(side, pieces);
        await FieldShatters(1 - side);
        await Wait(0.6);
    }

    /// 1. The pieces burn out of the hand and land big in a body shape: head in the middle, arms out, legs below.
    /// Returns the big cards and the body's centre once they have settled.
    async Task<(List<TextureRect> big, Vector2 c)> ExodiaAssemble(List<View> p, Func<Texture2D, Vector2, Vector2, Color, TextureRect> pic)
    {
        float ch = H * 0.36f, cw = ch / CardH;
        var c = new Vector2(W / 2, H * 0.42f);
        // Facing you, its left limbs are on your right.
        var slots = new[] { (c + new Vector2(0, -ch * 0.18f), 0f), (c + new Vector2(cw * 1.15f, -ch * 0.05f), 0.18f), (c + new Vector2(-cw * 1.15f, -ch * 0.05f), -0.18f),
                            (c + new Vector2(cw * 0.55f, ch * 0.82f), 0.06f), (c + new Vector2(-cw * 0.55f, ch * 0.82f), -0.06f) };
        var big = new List<TextureRect>();
        for (int i = 0; i < p.Count; i++)
        {
            var v = p[i];
            var from = cam.UnprojectPosition(v.Root.GlobalPosition);
            Disintegrate(v, FxGold); HideFor(v, 999); v.S = v.S with { Loc = Loc.None };
            Cue("discard_burn");
            var r = pic(CardArt.Large(v.Card.Data), new Vector2(cw, ch), from - new Vector2(cw, ch) / 2, new Color(1, 1, 1, 0));
            r.Scale = new Vector2(0.35f, 0.35f);
            big.Add(r);
            var (at, rot) = slots[i];
            var tw = CreateTween().SetParallel();
            tw.TweenProperty(r, "modulate:a", 1f, 0.35).SetDelay(0.3);
            tw.TweenProperty(r, "position", at - new Vector2(cw, ch) / 2, 0.6).SetDelay(0.3).SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
            tw.TweenProperty(r, "scale", Vector2.One, 0.6).SetDelay(0.3).SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
            tw.TweenProperty(r, "rotation", rot, 0.6).SetDelay(0.3);
            int k = i;
            After(0.9, () => { Cue("thread_flare"); GlintAt("md/star4", FxGold, slots[k].Item1, 380, 0.4f, 1.3f, 0.5); });
            await Wait(0.45);
        }
        await Wait(0.8);
        return (big, c);
    }

    /// The animation itself (pieces in Duel.Exodia order: head, left arm, right arm, left leg, right leg). Leaves the
    /// pieces hidden and out of the hand; returns once the blast has hit.
    async Task ExodiaFinal(int side, List<View> p)
    {
        var temp = new List<Control>();
        T Add<T>(T n) where T : Control { hud.AddChild(n); temp.Add(n); return n; }
        TextureRect Pic(Texture2D t, Vector2 size, Vector2 pos, Color mod) => Add(new TextureRect { Texture = t, ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.Scale, Size = size, PivotOffset = size / 2, Position = pos, Modulate = mod, MouseFilter = MouseFilterEnum.Ignore, TextureFilter = TextureFilterEnum.LinearWithMipmaps });

        var dim = Add(new ColorRect { Size = new Vector2(W, H), Color = new Color(0, 0, 0, 0), MouseFilter = MouseFilterEnum.Ignore });
        CreateTween().TweenProperty(dim, "color:a", 0.8f, 0.7);
        await Wait(0.4);
        var (big, c) = await ExodiaAssemble(p, Pic);

        // 2. The five flare and fade; the original game's Exodia rises out of them, arms drawn back.
        Cue("ritual_circle");
        GlintAt("md/rays", FxGold, c, 1500, 0.3f, 1.2f, 2.0, spin: 0.5f, alpha: 0.7f);
        foreach (var r in big) { var t = CreateTween().SetParallel(); t.TweenProperty(r, "modulate", new Color(2f, 1.7f, 1f, 0), 0.9); t.TweenProperty(r, "scale", new Vector2(1.15f, 1.15f), 0.9); }
        float k1 = H / 600f;
        var o = new Vector2(W / 2, H * 0.47f);
        TextureRect Part(string n, Vector2 at, float alpha = 1)
        {
            var t = ExoTex(n); var sz = t.GetSize() * k1;
            return Pic(t, sz, o + at * k1 - sz / 2, new Color(1, 1, 1, alpha));
        }
        var near = new[] { Part("exo_face1", new(0, -125), 0), Part("exo_arm1", new(-245, 70), 0), Part("exo_arml1", new(245, 70), 0) };
        foreach (var n in near)
        {
            n.Scale = new Vector2(0.8f, 0.8f);
            var t = CreateTween().SetParallel();
            t.TweenProperty(n, "modulate:a", 1f, 0.8).SetDelay(0.3);
            t.TweenProperty(n, "scale", Vector2.One, 1.2).SetDelay(0.3).SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
        }
        await Wait(1.4);
        Shake(0.04f);

        // 3. A light ball grows between the hands.
        var ball = Part("exo_ball", new(0, 150)); ball.Scale = new Vector2(0.1f, 0.1f); ball.Material = AddMat;
        CreateTween().TweenProperty(ball, "scale", new Vector2(1.3f, 1.3f), 1.3).SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.In);
        for (int i = 0; i < 12; i++) { int q = i; After(q * 0.09, () => GlintAt("md/star4", FxGold, o + new Vector2(0, 150) * k1 + new Vector2((float)GD.RandRange(-260, 260), (float)GD.RandRange(-160, 160)), 120, 1f, 0.2f, 0.5)); }
        Cue("windup_direct");
        await Wait(1.4);

        // 4. The thrust: the ball fires at the loser.
        foreach (var n in near) n.Visible = false;
        var thrust = new[] { Part("exo_face3", new(0, -125)), Part("exo_arm3", new(-235, 100)), Part("exo_arml3", new(235, 100)) };
        foreach (var n in thrust) { n.Scale = new Vector2(1.08f, 1.08f); CreateTween().TweenProperty(n, "scale", Vector2.One, 0.2); }
        var shot = CreateTween().SetParallel();
        if (side == 0)   // into the rival's field, up the table
        {
            shot.TweenProperty(ball, "position:y", H * 0.08f - ball.Size.Y / 2, 0.35).SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.In);
            shot.TweenProperty(ball, "scale", new Vector2(0.7f, 0.7f), 0.35);
        }
        else shot.TweenProperty(ball, "scale", new Vector2(14f, 14f), 0.4).SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.In);   // straight at you
        Cue("direct_impact");
        await Wait(0.35);
        var white = Add(new ColorRect { Size = new Vector2(W, H), Color = new Color(1, 0.96f, 0.85f, 0), MouseFilter = MouseFilterEnum.Ignore });
        var wt = CreateTween(); wt.TweenProperty(white, "color:a", 1f, 0.08); wt.TweenProperty(white, "color:a", 0f, 0.9);
        Shake(0.15f);
        var hit = new Vector3(0, 0, side == 0 ? -1.2f : 1.2f);
        Flash(hit, FxGold, 14);
        Shockwave(hit, Colors.White, 12);
        ball.Visible = false;
        await Wait(0.15);
        foreach (var n in thrust) CreateTween().TweenProperty(n, "modulate:a", 0f, 0.8);
        CreateTween().TweenProperty(dim, "color:a", 0f, 0.8);
        After(1.2, () => { foreach (var n in temp) if (IsInstanceValid(n)) n.QueueFree(); });
    }
}
