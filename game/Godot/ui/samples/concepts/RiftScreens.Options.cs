using Godot;
using PoC.Engine;
using static PoC.Game.UI.ConceptKit;

namespace PoC.Game.UI;

/// Options samples. a = profile in the light, settings in the dark; b = every row straddles the rift (label in the
/// light, control in the dark); c = your profile as a Duelist card in the light, settings beside it in the dark.
public partial class RiftScreens
{
    static readonly (string Label, int Kind, string[] Opts, int Pick, float Value)[] Rows =
    {
        ("BAN LIST", 0, new[] { "2003", "2004" }, 1, 0), ("DUEL SPEED", 0, new[] { "NORMAL", "FAST", "FASTER" }, 0, 0),
        ("MUSIC", 1, Array.Empty<string>(), 0, 0.7f), ("SOUND", 1, Array.Empty<string>(), 0, 0.85f), ("SCREEN", 0, new[] { "FULLSCREEN", "WINDOW" }, 0, 0),
    };

    void Options()
    {
        switch (variant)
        {
            case "b": OptionsStraddle(); break;
            case "c": OptionsDuelistCard(); break;
            default: OptionsSplit(); break;
        }
    }

    void ProfileBlock(Vector2 centre, float size)
    {
        var f = Cosmetics.Framed(Avatar, Session.Profile.Frame, size);
        f.Position = centre - new Vector2(size, size) / 2; AddChild(f);
        T(this, Settings.PlayerName.ToUpperInvariant(), Title, 46, new Vector2(centre.X - 300, centre.Y + size / 2 + 18), new Vector2(600, 60), HorizontalAlignment.Center);
        Hair(new Vector2(centre.X - 120, centre.Y + size / 2 + 88), 240, GoldC with { A = 0.7f });
        C(this, $"{Session.Profile.Dp:N0} DP   ·   DUELIST SINCE 2026", Body, 20, Graphite * new Color(1, 1, 1, 0.8f), new Vector2(centre.X - 300, centre.Y + size / 2 + 100), new Vector2(600, 30), HorizontalAlignment.Center);
    }

    // a ---------------------------------------------------------------- profile left, settings right
    void OptionsSplit()
    {
        Rift(0.5f, 0.42f);
        Heading("OPTIONS", new Vector2(80, 60));
        ProfileBlock(new Vector2(W * 0.22f, H * 0.42f), 260);
        // picture + frame pickers as small rows under the profile
        var db = CardDb.Shared;
        float y = H * 0.42f + 260 / 2 + 170;
        C(this, "PICTURE", Title, 18, Graphite, new Vector2(W * 0.22f - 300, y));
        for (int i = 0; i < 6; i++)
        {
            var pc = Settings.PictureCard(i);
            if (pc == null) continue;
            var f = Cosmetics.Framed(CardArt.Picture(pc), Shop.DefaultFrame, 64);
            f.Position = new Vector2(W * 0.22f - 300 + 140 + i * 72, y - 18); AddChild(f);
            if (i == Settings.Picture) Pip(new Vector2(f.Position.X + 32, y + 56), 8);
        }
        y += 90;
        C(this, "FRAME", Title, 18, Graphite, new Vector2(W * 0.22f - 300, y));
        string[] frames = { "classic", "gold", "millennium", "rune", "dark", "diamond" };
        for (int i = 0; i < frames.Length; i++)
        {
            var f = Cosmetics.Framed(Avatar, frames[i], 64);
            f.Position = new Vector2(W * 0.22f - 300 + 140 + i * 72, y - 18); AddChild(f);
            if (frames[i] == Session.Profile.Frame) Pip(new Vector2(f.Position.X + 32, y + 56), 8);
        }

        Logo(new Vector2(W - 300, 110), 420);
        float x0 = W * 0.56f, ry = 290;
        foreach (var (label, kind, opts, pick, val) in Rows)
        {
            int k = focusables.Count;
            var l = C(this, label, Title, 22, Mist, new Vector2(x0, ry));
            var lit = Hair(new Vector2(x0 - 40, ry + 14), 22, GoldC);
            lit.Visible = false;
            if (kind == 0) Segment(new Vector2(x0 + 280, ry - 2), opts, pick, true);
            else Slider(new Vector2(x0 + 280, ry + 16), 420, val, true);
            focusables.Add(on => { lit.Visible = on; l.AddThemeColorOverride("font_color", on ? new Color(1f, 0.85f, 0.5f) : Mist); });
            ry += 104;
        }
        focusables.Add(Button("DONE", new Vector2(W - 260, H - 110)));
    }

    // b ---------------------------------------------------------------- rows straddling the rift
    void OptionsStraddle()
    {
        Rift(0.53f, 0.47f);
        Logo(new Vector2(W - 290, 105), 400);
        C(this, "OPTIONS", Title, 64, Ink, new Vector2(80, 54));
        Hair(new Vector2(82, 136), 200, GoldC);
        float ry = 330;
        foreach (var (label, kind, opts, pick, val) in Rows)
        {
            float cx = CrackX(ry + 14);
            var l = C(this, label, Title, 30, Ink, new Vector2(cx - 760, ry - 6), new Vector2(700, 44), HorizontalAlignment.Right);
            var lp = Pip(new Vector2(cx - 30, ry + 15), 10);
            var rp = Pip(new Vector2(cx + 30, ry + 15), 10);
            lp.Visible = rp.Visible = false;
            if (kind == 0) Segment(new Vector2(cx + 70, ry - 2), opts, pick, true, 26);
            else Slider(new Vector2(cx + 70, ry + 16), 460, val, true);
            focusables.Add(on => { lp.Visible = rp.Visible = on; l.AddThemeColorOverride("font_color", on ? GoldDeep : Ink); bg.P("focus_y", (ry + 14) / H); });
            ry += 112;
        }
        // profile bottom left, done bottom right
        var f = Cosmetics.Framed(Avatar, Session.Profile.Frame, 110);
        f.Position = new Vector2(80, H - 190); AddChild(f);
        C(this, Settings.PlayerName.ToUpperInvariant(), Title, 34, Ink, new Vector2(210, H - 172));
        C(this, "EDIT NAME   ·   PICTURE   ·   FRAME", Body, 19, Graphite * new Color(1, 1, 1, 0.7f), new Vector2(212, H - 120));
        focusables.Add(Button("DONE", new Vector2(W - 240, H - 110)));
    }

    // c ---------------------------------------------------------------- your profile as a Duelist card
    void OptionsDuelistCard()
    {
        Rift(0.56f, 0.5f);
        Logo(new Vector2(W - 290, 105), 400);
        C(this, "OPTIONS", Title, 60, Ink, new Vector2(80, 54));
        Hair(new Vector2(82, 130), 180, GoldC);
        // a card-shaped profile: gold frame, name bar, picture window, stats line
        float h = H * 0.68f, w = h * 0.686f;
        var c = new Vector2(W * 0.26f, H * 0.56f);
        var root = new Control { Position = c - new Vector2(w, h) / 2, Size = new Vector2(w, h), PivotOffset = new Vector2(w, h) / 2, Rotation = -0.03f, MouseFilter = MouseFilterEnum.Ignore };
        AddChild(root);
        Shadow(root, Vector2.Zero, new Vector2(w, h), 0.32f, new Color(0.12f, 0.12f, 0.15f), new Vector2(18, 30));
        ShaderRect(root, @"shader_type canvas_item;
void fragment() {
  vec2 p = UV;
  float edge = min(min(p.x, 1.0 - p.x), min(p.y, 1.0 - p.y));
  vec3 body = mix(vec3(0.16, 0.14, 0.2), vec3(0.08, 0.07, 0.1), p.y);
  vec3 gold = mix(vec3(1.0, 0.88, 0.55), vec3(0.6, 0.42, 0.12), p.y);
  float rim = smoothstep(0.012, 0.0, abs(edge - 0.018));
  vec3 c = mix(body, gold, step(edge, 0.03) * 0.9 + rim);
  COLOR = vec4(c, 1.0);
}", Vector2.Zero, new Vector2(w, h));
        C(root, Settings.PlayerName.ToUpperInvariant(), Title, 34, new Color(1f, 0.92f, 0.7f), new Vector2(34, 30), new Vector2(w - 120, 46));
        Pip(new Vector2(w - 50, 54), 22, root);
        var pic = Ink_(Avatar!, new Vector2(w * 0.1f, 110), new Vector2(w * 0.8f, w * 0.8f), root);
        Hair(new Vector2(w * 0.1f, 110 + w * 0.8f + 4), w * 0.8f, new Color(1f, 0.85f, 0.5f), root, 2);
        C(root, "[DUELIST / CHAOS RIFT]", Body, 20, new Color(0.85f, 0.85f, 0.9f), new Vector2(34, 130 + w * 0.8f));
        C(root, "Wins 42  ·  Tournaments 3  ·  Cards 486", Body, 18, new Color(0.7f, 0.7f, 0.76f), new Vector2(34, 162 + w * 0.8f));
        C(root, $"DP {Session.Profile.Dp:N0}", Title, 26, new Color(1f, 0.88f, 0.55f), new Vector2(w - 230, h - 60), new Vector2(200, 36), HorizontalAlignment.Right);
        C(this, "PICTURE  ·  FRAME  ·  NAME", Body, 20, Graphite, new Vector2(c.X - 200, c.Y + h / 2 + 30), new Vector2(400, 30), HorizontalAlignment.Center);

        float x0 = W * 0.58f, ry = 330;
        foreach (var (label, kind, opts, pick, val) in Rows)
        {
            var l = C(this, label, Title, 20, Mist, new Vector2(x0, ry - 34));
            if (kind == 0) Segment(new Vector2(x0, ry), opts, pick, true, 28);
            else Slider(new Vector2(x0, ry + 20), 520, val, true);
            var mark = Hair(new Vector2(x0 - 34, ry - 22), 20, GoldC); mark.Visible = false;
            focusables.Add(on => { mark.Visible = on; l.AddThemeColorOverride("font_color", on ? new Color(1f, 0.85f, 0.5f) : Mist); });
            ry += 122;
        }
        focusables.Add(Button("DONE", new Vector2(W - 240, H - 110)));
    }
}
