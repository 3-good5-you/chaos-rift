using Godot;
using PoC.Engine;

namespace PoC.Game.UI;

/// A playmat won (2026-11-09, Victory Lab pick C): after the results, the mat's two halves meet on the centre line
/// like the duel field (the rival's half drops from above, yours rises from below), a flash along the seam, then
/// NEW PLAYMAT and its name. EQUIP puts it on your table; CONTINUE goes where the Results button pointed.
/// `-- --demo[=obsidian]` shows it without a tournament (screenshots).
public partial class MatUnlock : RiftMenu
{
    Shop.Item mat = null!;
    bool leaving;

    protected override string? MusicPath => null;
    protected override void OnBack() => Next();

    protected override void Build()
    {
        foreach (var a in OS.GetCmdlineUserArgs())
            if (a.StartsWith("--demo"))
            {
                var key = a.Contains('=') ? a[(a.IndexOf('=') + 1)..] : "obsidian";
                Session.LastMat = Shop.Mats.FirstOrDefault(m => m.Key == key);
            }
        if (Session.LastMat == null) { Next(); return; }
        mat = Session.LastMat;
        Session.LastMat = null;   // shown once
        // a deck won in the same result still gets its reveal after this one
        if (Session.LastStarter != null) { var after = Session.AfterUnlock; Session.AfterUnlock = "res://ui/DeckUnlock.tscn"; next = after; }

        Rift(0.5f, 0.5f);
        var tex = GD.Load<Texture2D>($"res://assets/mats/{mat.Key}.jpg");
        float w = W * 0.56f, h = w * tex.GetHeight() / tex.GetWidth();
        var at = new Vector2(W / 2 - w / 2, H * 0.12f);
        var shadow = Shadow(this, at, new Vector2(w, h), 0.7f, new Color(0, 0, 0), new Vector2(0, 30));
        Appear(shadow, 0.3);
        Control Half(bool top, float fromY)
        {
            var clip = new Control { Position = new Vector2(at.X, fromY), Size = new Vector2(w, h / 2), ClipContents = true, MouseFilter = MouseFilterEnum.Ignore };
            AddChild(clip);
            Pic(clip, tex, new Vector2(0, top ? 0 : -h / 2), new Vector2(w, h));
            return clip;
        }
        var upper = Half(true, at.Y - H * 0.7f);
        var lower = Half(false, at.Y + h / 2 + H * 0.7f);
        var tw = CreateTween().SetParallel();
        tw.TweenProperty(upper, "position:y", at.Y, 0.8).SetDelay(0.3).SetTrans(Tween.TransitionType.Expo).SetEase(Tween.EaseType.Out);
        tw.TweenProperty(lower, "position:y", at.Y + h / 2, 0.8).SetDelay(0.3).SetTrans(Tween.TransitionType.Expo).SetEase(Tween.EaseType.Out);
        var seam = new ColorRect { Position = new Vector2(at.X, at.Y + h / 2 - 3), Size = new Vector2(w, 6), Color = Colors.White, MouseFilter = MouseFilterEnum.Ignore, Modulate = new Color(1, 1, 1, 0) };
        AddChild(seam);
        var st = CreateTween();
        st.TweenInterval(1.0);
        st.TweenCallback(Callable.From(() => Sound("res://assets/audio/md/se/SE_CARD_FINISH_BREAK_01.wav", -6)));
        st.TweenProperty(seam, "modulate:a", 1f, 0.05);
        st.TweenProperty(seam, "modulate:a", 0f, 0.6);
        GetTree().CreateTimer(1.2).Timeout += () =>
            CreateTween().TweenMethod(Callable.From<float>(v => bg.P("burst", v)), 1f, 0f, 1.4).SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);

        var head = T(this, "NEW PLAYMAT", Title, 64, new Vector2(0, at.Y + h + 40), new Vector2(W, 80), HorizontalAlignment.Center);
        head.Mat().P("lit", 1f);
        Appear(head, 1.2);
        Appear(T(this, $"{mat.Name.ToUpperInvariant()}  ·  CHAMPION PRIZE", Title, 28, new Vector2(0, at.Y + h + 124), new Vector2(W, 36), HorizontalAlignment.Center), 1.4);
        Appear(Button(this, "EQUIP", new Vector2(W / 2 - 200, H - 70), Equip, 28), 1.6);
        Appear(Button(this, "CONTINUE", new Vector2(W / 2 + 200, H - 70), Next, 24), 1.6);
    }

    string? next;

    void Equip()
    {
        Shop.Equip(P, mat);
        Session.Save();
        Flash("ON YOUR TABLE FROM THE NEXT DUEL");
        GetTree().CreateTimer(0.9).Timeout += Next;
    }

    void Next()
    {
        if (leaving) return;
        leaving = true;
        // never back to Results (it would pay the duel's rewards again)
        var to = Session.AfterUnlock ?? "res://ui/MainMenu.tscn";
        Session.AfterUnlock = next;
        Transition.Go(this, to);
    }

    void Appear(Control c, double delay)
    {
        c.Modulate = new Color(1, 1, 1, 0);
        CreateTween().TweenProperty(c, "modulate:a", 1f, 0.4).SetDelay(delay);
    }

    void Sound(string path, float db)
    {
        if (Shooting) return;
        var p = new AudioStreamPlayer { Stream = GD.Load<AudioStream>(path), VolumeDb = db, Bus = "Sound", Autoplay = true };
        p.Finished += p.QueueFree;
        AddChild(p);
    }
}
