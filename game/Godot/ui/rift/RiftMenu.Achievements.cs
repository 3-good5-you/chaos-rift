using Godot;
using PoC.Engine;

namespace PoC.Game.UI;

/// Achievement pieces shared by the Achievements screen and every Rift menu (the unlock pop-up). Menu Lab picks
/// 2026-11-16: the pop-up slides in at the top with the card medal and the prize.
public abstract partial class RiftMenu
{
    protected static string PrizeName(Achievements.Def a) => a.Kind switch
    {
        Achievements.Prize.Back => Lang.Cosmetic(Shop.Backs.First(b => b.Key == a.Key).Name).ToUpperInvariant() + "  " + Lang.Tr("CARD BACK"),
        Achievements.Prize.Mat => Lang.Cosmetic(Shop.Mats.First(b => b.Key == a.Key).Name).ToUpperInvariant() + "  " + Lang.Tr("PLAYMAT"),
        Achievements.Prize.Frame => Lang.Cosmetic(Shop.Frames.First(b => b.Key == a.Key).Name).ToUpperInvariant() + "  " + Lang.Tr("ICON FRAME"),
        _ => Lang.F("{0:N0} DP", a.Dp),
    };

    /// The prize drawn into a box of height `h` centred on `c`: a standing card back, a playmat, your picture in the
    /// frame, or a gold DP coin. `lit` false = washed out (not earned yet).
    protected void PrizeArt(Control host, Vector2 c, float h, Achievements.Def a, bool lit, bool shadow = true)
    {
        bool light = OnLight((host.IsInsideTree() ? host.GlobalPosition : host.Position) + c);
        var dim = lit ? Colors.White : new Color(0.62f, 0.62f, 0.66f, 0.85f);
        Color sh = light ? new Color(0.1f, 0.1f, 0.15f) : new Color(0.3f, 0.3f, 0.4f);
        switch (a.Kind)
        {
            case Achievements.Prize.Back:
            {
                var sz = new Vector2(h * 0.686f, h);
                if (shadow) Shadow(host, c - sz / 2, sz, light ? 0.3f : 0.5f, sh, new Vector2(0, h * 0.04f));
                Pic(host, UI.Cosmetics.Back(a.Key, h > 200 ? 0 : 256), c - sz / 2, sz).Modulate = dim;
                break;
            }
            case Achievements.Prize.Mat:
            {
                var sz = new Vector2(h * 1.6f, h);
                if (shadow) Shadow(host, c - sz / 2, sz, light ? 0.3f : 0.5f, sh, new Vector2(0, h * 0.04f));
                var p = Pic(host, Tex($"res://assets/mats/{a.Key}.jpg"), c - sz / 2, sz);
                p.StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered;
                p.Material = Kit.RoundedMat(1.6f, 0.06f);
                p.Modulate = dim;
                break;
            }
            case Achievements.Prize.Frame:
            {
                var f = UI.Cosmetics.Framed(Avatar, a.Key, h);
                f.Position = c - new Vector2(h, h) / 2; f.Modulate = dim;
                host.AddChild(f);
                break;
            }
            default:
            {
                var r = new Rect2(c - new Vector2(h, h) * 0.4f, new Vector2(h, h) * 0.8f);
                Plate(host, r, Box(lit ? GoldC : GoldC with { A = 0.4f }, GoldLight, Math.Max(2, h * 0.03f), 999, shadow ? 12 : 0, new Color(0.3f, 0.2f, 0.05f, 0.3f), new Vector2(0, 4)));
                Fit(host, Lang.Tr("{0:N0} DP").Replace("{0:N0}", "").Trim(), Title, Math.Max(12, (int)(h * 0.22f)), 8, Ink, r.Grow(-r.Size.X * 0.08f));   // the short price word (DP / PD / نقطة): the full Arabic "DP" is too long for the coin
                break;
            }
        }
    }

    /// The achievement's medal: its card's art in a circle, a gold ring when done, washed out while locked.
    protected void Medal(Control host, Vector2 c, float d, Achievements.Def a, bool done)
    {
        var root = new Control { Position = c - new Vector2(d, d) / 2, Size = new Vector2(d, d), MouseFilter = MouseFilterEnum.Ignore };
        host.AddChild(root);
        bool light = OnLight((host.IsInsideTree() ? host.GlobalPosition : host.Position) + c);
        root.AddChild(new ColorRect { Size = root.Size, Color = done ? GoldC : (light ? Ink : Silver) * new Color(1, 1, 1, 0.2f), Material = Kit.RoundedMat(1, 0.5f), MouseFilter = MouseFilterEnum.Ignore });
        var art = Db.ByName.TryGetValue(a.Card, out var l) ? l[0] : Db.Named("Kuriboh");
        var p = Pic(root, CardArt.Picture(art), new Vector2(3, 3), root.Size - new Vector2(6, 6));
        p.Material = Kit.RoundedMat(1, 0.5f);
        if (!done) p.Modulate = new Color(0.5f, 0.5f, 0.55f, 0.6f);
    }

    /// After every Rift screen is built: achievements finished since the last look slide in at the top, one by one.
    protected override void AfterBuild()
    {
        if (Shooting || Demo.On) return;
        var fresh = Achievements.TellNew(P, Session.Decks.Count);
        if (fresh.Count == 0) return;
        Session.Save();
        for (int i = 0; i < fresh.Count; i++) Unlocked(fresh[i], PopupAt + i * 4.4);
    }

    /// When the first unlock pop-up slides in (a screen with its own reveal waits for it to finish).
    protected virtual double PopupAt => 0.6;

    /// The unlock pop-up: a dark card slides down, the medal flips in, the prize fades in; then it leaves.
    void Unlocked(Achievements.Def a, double delay)
    {
        float w = 680, h = 132;
        var card = new Control { Position = new Vector2(W / 2 - w / 2, -h - 40), Size = new Vector2(w, h), ZIndex = 90, MouseFilter = MouseFilterEnum.Ignore };
        card.SetMeta("dark", true);
        AddChild(card);
        Plate(card, new Rect2(Vector2.Zero, card.Size), Box(new Color(0.06f, 0.06f, 0.08f, 0.97f), GoldC, 2, 18, 30, new Color(0, 0, 0, 0.45f), new Vector2(0, 10)));
        var medal = new Control { Position = new Vector2(66, h / 2), Size = Vector2.Zero };
        card.AddChild(medal);
        Medal(medal, Vector2.Zero, 96, a, true);
        medal.Scale = new Vector2(0, 1);
        Put(card, "ACHIEVEMENT UNLOCKED", Thin, 13, GoldLight, new Vector2(128, 20));
        Put(card, a.Name, Title, 26, Silver, new Vector2(128, 42));
        Put(card, (a.Kind == Achievements.Prize.Dp ? "" : PrizeName(a) + "   ·   ") + Lang.F("+{0} DP", a.Dp) + "   ·   " + Lang.Tr("CLAIM IT IN ACHIEVEMENTS"), Title, 12, GoldLight, new Vector2(128, 88), new Vector2(w - 250, 22)).ClipText = true;
        var prize = new Control { Position = new Vector2(w - 70, h / 2), Size = Vector2.Zero, Modulate = new Color(1, 1, 1, 0) };
        card.AddChild(prize);
        PrizeArt(prize, Vector2.Zero, 100, a, true, false);
        var tw = CreateTween();
        tw.TweenInterval(delay);
        tw.TweenProperty(card, "position:y", 40f, 0.45).SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
        tw.TweenProperty(medal, "scale:x", 1f, 0.35).SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
        tw.TweenProperty(prize, "modulate:a", 1f, 0.3);
        tw.TweenInterval(3.4);
        tw.TweenProperty(card, "position:y", -h - 40, 0.35).SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.In);
        tw.TweenCallback(Callable.From(card.QueueFree));
    }
}
