using Godot;
using PoC.Engine;

namespace PoC.Game.UI;

/// Small pieces from the Menu Lab (2026-11-16) shared by the rebuilt screens: centred text, rounded plates, round
/// rival faces, capsule tags, the gold cup.
public abstract partial class RiftMenu
{
    /// Text centred in a box. Perpetua's capitals sit high in their line box, so they are nudged down a little.
    protected Label Mid(Control host, string s, Font f, int size, Color c, Rect2 r)
    {
        var l = Put(host, s, f, size, c, r.Position + new Vector2(0, Z(size) * 0.12f), r.Size, HorizontalAlignment.Center);
        l.VerticalAlignment = VerticalAlignment.Center;
        return l;
    }

    /// Centred text shrunk to fit the box. Perpetua carries fixed letter spacing, so a long name may never fit by
    /// shrinking: then it switches to the bold body font, which is far narrower.
    protected Label Fit(Control host, string s, Font f, int size, int min, Color c, Rect2 r)
    {
        int start = size;
        while (size > min && f.GetStringSize(Lang.Tr(s), HorizontalAlignment.Left, -1, Z(size)).X > r.Size.X) size--;
        if (f.GetStringSize(Lang.Tr(s), HorizontalAlignment.Left, -1, Z(size)).X > r.Size.X)
        {
            f = BodyBold; size = start;
            while (size > 10 && f.GetStringSize(Lang.Tr(s), HorizontalAlignment.Left, -1, Z(size)).X > r.Size.X) size--;
        }
        var l = Mid(host, s, f, size, c, r);
        l.ClipText = true;
        return l;
    }

    /// A rounded panel.
    protected static Panel Plate(Control host, Rect2 r, StyleBoxFlat s)
    {
        var p = new Panel { Position = r.Position, Size = r.Size, MouseFilter = MouseFilterEnum.Ignore };
        p.AddThemeStyleboxOverride("panel", s);
        host.AddChild(p);
        return p;
    }

    protected static void Diamond(CanvasItem c, Vector2 at, float r, Color col) =>
        c.DrawColoredPolygon(new[] { at + new Vector2(0, -r), at + new Vector2(r, 0), at + new Vector2(0, r), at + new Vector2(-r, 0) }, col);

    /// A duelist's round face with a ring (gold = in focus); `you` = your framed picture.
    protected Control Face(Control host, Vector2 centre, float d, string key, bool ring, float fade = 1)
    {
        var c = new Control { Position = centre - new Vector2(d, d) / 2, Size = new Vector2(d, d), MouseFilter = MouseFilterEnum.Ignore };
        host.AddChild(c);
        if (key == Tournament.You) { c.AddChild(UI.Cosmetics.Framed(Avatar, P.Frame, d)); return c; }
        bool light = OnLight((host.IsInsideTree() ? host.GlobalPosition : host.Position) + centre);
        c.AddChild(new ColorRect { Size = c.Size, Color = ring ? GoldC : (light ? Ink : Silver) * new Color(1, 1, 1, 0.25f), Material = Kit.RoundedMat(1, 0.5f), MouseFilter = MouseFilterEnum.Ignore });
        var p = Pic(c, RivalPanels.FaceTex(RivalPanels.Of(Tournament.Get(key))), new Vector2(3, 3), c.Size - new Vector2(6, 6));
        p.Material = Kit.RoundedMat(1, 0.5f);
        if (fade < 1) p.Modulate = new Color(0.55f, 0.55f, 0.6f, fade);
        return c;
    }

    /// A small capsule label ("YOU", "NEXT").
    protected void Tag(Control host, string text, Vector2 centre, Color bg, Color fg, int size = 12)
    {
        float w = Title.GetStringSize(Lang.Tr(text), HorizontalAlignment.Left, -1, Z(size)).X + 22, h = Z(size) + 12;
        Plate(host, new Rect2(centre - new Vector2(w, h) / 2, new Vector2(w, h)), Box(bg, default, 0, 99));
        Mid(host, text, Title, size, fg, new Rect2(centre - new Vector2(w, h) / 2, new Vector2(w, h)));
    }

    /// A gold cup: bowl, handles, stem and base.
    protected static void Cup(Control host, Vector2 centre, float size)
    {
        var c = new Control { Position = centre - new Vector2(size, size) / 2, Size = new Vector2(size, size), MouseFilter = MouseFilterEnum.Ignore };
        host.AddChild(c);
        c.Draw += () =>
        {
            float s = size;
            var bowl = new List<Vector2> { new(s * 0.22f, s * 0.12f), new(s * 0.78f, s * 0.12f) };
            for (int i = 0; i <= 16; i++) { float a = i / 16f * Mathf.Pi; bowl.Add(new Vector2(s * 0.5f + Mathf.Cos(a) * s * 0.28f, s * 0.3f + Mathf.Sin(a) * s * 0.28f)); }
            c.DrawArc(new Vector2(s * 0.22f, s * 0.32f), s * 0.13f, Mathf.Pi * 0.5f, Mathf.Pi * 1.5f, 20, GoldDeep, s * 0.04f, true);
            c.DrawArc(new Vector2(s * 0.78f, s * 0.32f), s * 0.13f, -Mathf.Pi * 0.5f, Mathf.Pi * 0.5f, 20, GoldDeep, s * 0.04f, true);
            c.DrawColoredPolygon(bowl.ToArray(), GoldC);
            c.DrawRect(new Rect2(s * 0.27f, s * 0.12f, s * 0.46f, s * 0.04f), GoldLight);
            c.DrawColoredPolygon(new[] { new Vector2(s * 0.45f, s * 0.57f), new Vector2(s * 0.55f, s * 0.57f), new Vector2(s * 0.53f, s * 0.74f), new Vector2(s * 0.47f, s * 0.74f) }, GoldDeep);
            c.DrawStyleBox(Box(GoldC, default, 0, 3), new Rect2(s * 0.32f, s * 0.74f, s * 0.36f, s * 0.07f));
            c.DrawStyleBox(Box(GoldDeep, default, 0, 3), new Rect2(s * 0.26f, s * 0.81f, s * 0.48f, s * 0.09f));
            Diamond(c, new Vector2(s * 0.5f, s * 0.33f), s * 0.07f, GoldLight);
        };
    }
}
