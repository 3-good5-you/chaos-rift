using Godot;
using PoC.Engine;

namespace PoC.Game.UI;

/// Duel HUD design samples for the UI Lab (round 10, 2026-11-09), drawn over the live duel on a 2880x1800 canvas.
/// `Embed(key)`: lp1-3 life points, turn1-3 turn circle, prompt1-3 effect prompt, pause1-3 pause menu,
/// search1-3 card search. Nothing here covers the whole screen; the field stays visible behind every piece.
public partial class DuelUiSamples : RiftMenu
{
    string key = "";
    public static DuelUiSamples Embed(string key) => new() { key = key, MouseFilter = MouseFilterEnum.Ignore };
    protected override void Build() { }   // drawn in _Ready (never a screen of its own)

    static readonly Color Panel = new(0.035f, 0.035f, 0.05f, 0.93f), Crimson = new(0.9f, 0.34f, 0.3f), TheirC = new(0.55f, 0.8f, 1f);
    static readonly string[] Phases = { "DRAW", "STANDBY", "MAIN 1", "BATTLE", "MAIN 2", "END" };
    static readonly string[] Pool = { "Dark Magician", "Summoned Skull", "Blue-Eyes White Dragon", "Celtic Guardian", "Gemini Elf", "Mystical Elf", "Battle Ox", "Curse of Dragon", "Feral Imp" };
    const int Sel = 1, PhaseNow = 2, LpMe = 5300, LpThem = 6900;

    public override void _Ready()
    {
        Scale = Vector2.One * GetViewportRect().Size.X / 2880f;
        W = 2880; H = 1800; Top = Bottom = -1;
        Size = new Vector2(W, H);
        switch (key)
        {
            case "lp1": Medallion(0); Medallion(1); break;
            case "lp2": Blade(0); Blade(1); break;
            case "lp3": MiniCard(0, new Vector2(40, H - 470)); MiniCard(1, new Vector2(W - 40 - 290, 40)); break;
            case "lpf1": Open(0); Open(1); break;
            case "lpf2": Plate(0); Plate(1); break;
            case "lpf3": Classic(0); Classic(1); break;
            case "turn1": RiftDisc(); break;
            case "turn2": Dial(); break;
            case "turn3": Coin(); break;
            case "prompt1": Callout(); break;
            case "prompt2": HandBar(); break;
            case "prompt3": SidePrompt(); break;
            case "pause1": Drawer(); break;
            case "pause2": Compact(); break;
            case "pause3": DropDown(); break;
            case "search1": Tray(); break;
            case "search2": Column(); break;
            case "search3": Fan(); break;
        }
    }

    // ---------------------------------------------------------------- kit

    Panel Box(Control host, Rect2 r, float radius = 20, Color? bg = null, Color? edge = null)
    {
        var sb = new StyleBoxFlat { BgColor = bg ?? Panel, ShadowColor = new Color(0, 0, 0, 0.6f), ShadowSize = 34, ShadowOffset = new Vector2(0, 10) };
        sb.SetCornerRadiusAll((int)radius);
        if (edge is { } e) { sb.BorderColor = e; sb.SetBorderWidthAll(3); }
        var p = new Panel { Position = r.Position, Size = r.Size, MouseFilter = MouseFilterEnum.Ignore };
        p.AddThemeStyleboxOverride("panel", sb);
        host.AddChild(p);
        return p;
    }

    DrawBox Paint(Control host, Rect2 r, System.Action<DrawBox> paint)
    {
        var d = new DrawBox { Position = r.Position, Size = r.Size, MouseFilter = MouseFilterEnum.Ignore, Paint = paint };
        host.AddChild(d);
        return d;
    }

    static Label Wrap(Control host, string s, Font f, int size, Color c, Vector2 at, float w, HorizontalAlignment a = HorizontalAlignment.Left)
        => Kit.Put(host, s, f, size, c, at, new Vector2(w, size * 4), a, wrap: true);

    /// A pill button: filled gold for the main answer, an outline for the other.
    void Pill(Control host, string s, Vector2 centre, bool main, int size = 30, float w = 0)
    {
        float tw = Title.GetStringSize(s, HorizontalAlignment.Left, -1, size).X;
        w = Math.Max(w, tw + 80);
        float h = size * 2.3f;
        var sb = new StyleBoxFlat { BgColor = main ? GoldC : new Color(0, 0, 0, 0.35f), BorderColor = main ? GoldLight : GoldC };
        sb.SetCornerRadiusAll((int)(h / 2)); sb.SetBorderWidthAll(3);
        var p = new Panel { Position = centre - new Vector2(w / 2, h / 2), Size = new Vector2(w, h), MouseFilter = MouseFilterEnum.Ignore };
        p.AddThemeStyleboxOverride("panel", sb);
        host.AddChild(p);
        Put(p, s, Title, size, main ? Ink : GoldLight, new Vector2(0, (h - size * 1.4f) / 2), new Vector2(w, size * 1.4f), HorizontalAlignment.Center);
    }

    string Who(int side) => side == 0 ? Settings.PlayerName.ToUpperInvariant() : Session.Rival.Name.ToUpperInvariant();
    Color SideC(int side) => side == 0 ? GoldC : TheirC;
    Color SideLight(int side) => side == 0 ? GoldLight : TheirC;

    static Shader? faceShader;
    /// Your picture, or the rival's face cut from their portrait (as the old profile does); round or square.
    TextureRect Face(Control host, int side, Rect2 r, bool round)
    {
        Texture2D? t; Vector4 region = new(0, 0, 1, 1);
        if (side == 1 && RivalPanels.HasPortrait(Session.Rival))
        {
            t = RivalPanels.Portrait(Session.Rival);
            region = new Vector4(0.25f, 0f, 0.5f, 0.5f * t.GetWidth() / t.GetHeight());
        }
        else t = side == 0 ? Avatar : RivalPanels.Art(Session.Rival);
        var pic = Pic(host, t, r.Position, r.Size);
        var m = new ShaderMaterial { Shader = faceShader ??= new Shader { Code = @"shader_type canvas_item;
uniform vec4 region; uniform float round_;
void fragment() {
  vec2 q = abs(UV - 0.5);
  float box = 1.0 - smoothstep(0.44, 0.45, max(q.x, q.y) - 0.0) * 0.0;
  float m = round_ > 0.5 ? 1.0 - smoothstep(0.49, 0.5, length(UV - 0.5)) : 1.0 - smoothstep(0.0, 0.01, length(max(q - 0.42, 0.0)) - 0.08);
  vec4 c = texture(TEXTURE, region.xy + UV * region.zw);
  COLOR = vec4(c.rgb, c.a * m);
}" } };
        m.SetShaderParameter("region", region); m.SetShaderParameter("round_", round ? 1f : 0f);
        pic.Material = m;
        return pic;
    }

    List<CardData> Cards() => Pool.Select(n => Db.Get(Db.Canon(Db.Named(n).Idx))).ToList();

    // ---------------------------------------------------------------- life points

    /// 1: a round portrait whose ring is the LP; name and number beside it on a soft shadow, no box.
    void Medallion(int side)
    {
        bool right = side == 1;
        float d = 230, lp = side == 0 ? LpMe : LpThem;
        var c = right ? new Vector2(W - 60 - d / 2, 60 + d / 2) : new Vector2(60 + d / 2, H - 60 - d / 2);
        var shadowAt = right ? new Vector2(c.X - 720, c.Y - 90) : new Vector2(c.X - 60, c.Y - 90);
        Shadow(this, shadowAt, new Vector2(780, 180), 0.85f, new Color(0, 0, 0));
        Paint(this, new Rect2(c - new Vector2(d / 2 + 24, d / 2 + 24), new Vector2(d + 48, d + 48)), b =>
        {
            var o = b.Size / 2;
            b.DrawArc(o, d / 2 + 12, 0, Mathf.Tau, 128, new Color(0, 0, 0, 0.7f), 16, true);
            b.DrawArc(o, d / 2 + 12, -Mathf.Pi / 2, -Mathf.Pi / 2 + Mathf.Tau * lp / 8000f, 128, SideLight(side), 10, true);
        });
        Face(this, side, new Rect2(c - new Vector2(d, d) / 2, new Vector2(d, d)), true);
        float tx = right ? c.X - d / 2 - 40 - 560 : c.X + d / 2 + 40;
        var al = right ? HorizontalAlignment.Right : HorizontalAlignment.Left;
        Put(this, Who(side), Title, 34, SideLight(side), new Vector2(tx, c.Y - 92), new Vector2(560, 44), al);
        Put(this, $"{lp:0}", Title, 104, Silver, new Vector2(tx, c.Y - 54), new Vector2(560, 130), al);
    }

    /// 2: a slanted blade along the screen edge, portrait at its outer end, LP as a bright line on its edge.
    void Blade(int side)
    {
        bool me = side == 0;
        float w = 860, h = 170, y = me ? H - h - 40 : 40, slant = 60;
        Vector2[] poly = me
            ? new[] { new Vector2(0, y), new Vector2(w, y), new Vector2(w - slant, y + h), new Vector2(0, y + h) }
            : new[] { new Vector2(W - w + slant, y), new Vector2(W, y), new Vector2(W, y + h), new Vector2(W - w, y + h) };
        Shadow(this, new Vector2(me ? 0 : W - w, y), new Vector2(w, h), 0.6f, new Color(0, 0, 0), new Vector2(0, 10));
        AddChild(new Polygon2D { Polygon = poly, Color = Panel });
        float lp = me ? LpMe : LpThem, f = lp / 8000f;
        var (e0, e1) = me ? (poly[0], poly[1]) : (poly[3], poly[2]);   // the edge facing the table
        AddChild(new Line2D { Points = new[] { e0, e1 }, Width = 3, DefaultColor = SideC(side) with { A = 0.35f } });
        AddChild(new Line2D { Points = me ? new[] { e0, e0.Lerp(e1, f) } : new[] { e1, e1.Lerp(e0, f) }, Width = 7, DefaultColor = SideLight(side) });
        float a = 130;
        var pr = new Rect2(me ? 22 : W - 22 - a, y + (h - a) / 2, a, a);
        Face(this, side, pr, false);
        float tx = me ? 22 + a + 30 : W - w + slant + 20, tw = w - a - 110;
        var al = me ? HorizontalAlignment.Left : HorizontalAlignment.Right;
        Put(this, Who(side), Title, 30, SideLight(side), new Vector2(tx, y + 18), new Vector2(tw, 40), al);
        Put(this, $"{lp:0}", Title, 88, Silver, new Vector2(tx, y + 48), new Vector2(tw, 110), al);
    }

    /// 3: a small standing card: square portrait, name, LP between two diamonds.
    void MiniCard(int side, Vector2 at)
    {
        float w = 290, h = 430, lp = side == 0 ? LpMe : LpThem;
        var p = Box(this, new Rect2(at, new Vector2(w, h)), 18, null, SideC(side));
        Face(p, side, new Rect2(16, 16, w - 32, w - 32), false);
        Put(p, Who(side), Title, 30, SideLight(side), new Vector2(0, w - 6), new Vector2(w, 40), HorizontalAlignment.Center);
        Put(p, $"{lp:0}", Title, 62, Silver, new Vector2(0, w + 36), new Vector2(w, 76), HorizontalAlignment.Center);
        Pip(p, new Vector2(30, w + 76), 13); Pip(p, new Vector2(w - 30, w + 76), 13);
    }

    // ---------------------------------------------------------------- life points with the profile frame (round 11)

    /// Your picture in your equipped frame; the rival's face in the classic frame (as the old profile box does).
    Control Framed(int side, float d)
    {
        var f = Cosmetics.Framed(side == 0 ? Avatar : null, side == 0 ? P.Frame : Shop.DefaultFrame, d);
        if (side == 1)
        {
            var pic = (TextureRect)f.GetChild(1);
            Texture2D t; Vector4 region = new(0, 0, 1, 1);
            if (RivalPanels.HasPortrait(Session.Rival)) { t = RivalPanels.Portrait(Session.Rival); region = new Vector4(0.25f, 0f, 0.5f, 0.5f * t.GetWidth() / t.GetHeight()); }
            else t = RivalPanels.Art(Session.Rival);
            var tmp = Face(this, 1, new Rect2(0, 0, 1, 1), true);   // borrow the face-crop material
            pic.Texture = t; pic.StretchMode = TextureRect.StretchModeEnum.Scale; pic.Material = tmp.Material;
            tmp.QueueFree();
        }
        return f;
    }

    /// The LP as a line with a diamond at the value.
    void LpLine(Control host, Vector2 at, float w, float f, int side, bool rightToLeft = false)
    {
        Hair(host, at, w, new Color(1, 1, 1, 0.14f), 6);
        Hair(host, new Vector2(rightToLeft ? at.X + w * (1 - f) : at.X, at.Y), w * f, SideLight(side), 6);
        Pip(host, new Vector2(rightToLeft ? at.X + w * (1 - f) : at.X + w * f, at.Y + 3), 18);
    }

    /// F1: the framed picture large, name and LP beside it on a soft shadow; no box.
    void Open(int side)
    {
        bool right = side == 1;
        float d = 250, lp = side == 0 ? LpMe : LpThem;
        var at = right ? new Vector2(W - 40 - d, 30) : new Vector2(40, H - 30 - d);
        Shadow(this, right ? new Vector2(at.X - 620, at.Y + 40) : new Vector2(at.X + 60, at.Y + 40), new Vector2(800, d - 80), 0.85f, new Color(0, 0, 0));
        var f = Framed(side, d); f.Position = at; AddChild(f);
        float tx = right ? at.X - 600 : at.X + d + 20;
        var al = right ? HorizontalAlignment.Right : HorizontalAlignment.Left;
        Put(this, Who(side), Title, 34, SideLight(side), new Vector2(tx, at.Y + 36), new Vector2(580, 44), al);
        Put(this, $"{lp:0}", Title, 100, Silver, new Vector2(tx, at.Y + 70), new Vector2(580, 120), al);
        LpLine(this, new Vector2(tx + (right ? 160 : 0), at.Y + 196), 420, lp / 8000f, side, right);
    }

    /// F2: a slim plate along the screen edge, the framed picture standing over its outer end.
    void Plate(int side)
    {
        bool right = side == 1;
        float d = 240, w = 760, h = 150, lp = side == 0 ? LpMe : LpThem;
        var r = right ? new Rect2(W - w - 30, 60, w, h) : new Rect2(30, H - h - 60, w, h);
        var p = Box(this, r, h / 2, null, SideC(side) with { A = 0.7f });
        var f = Framed(side, d); f.Position = right ? new Vector2(r.End.X - d + 30, r.Position.Y + h / 2 - d / 2) : new Vector2(r.Position.X - 30, r.Position.Y + h / 2 - d / 2); AddChild(f);
        float tx = right ? 60 : d + 10, tw = w - d - 70;
        var al = right ? HorizontalAlignment.Right : HorizontalAlignment.Left;
        Put(p, Who(side), Title, 28, SideLight(side), new Vector2(tx, 14), new Vector2(tw, 36), al);
        Put(p, $"{lp:0}", Title, 76, Silver, new Vector2(tx, 40), new Vector2(tw, 92), al);
        LpLine(p, new Vector2(tx, h - 18), tw, lp / 8000f, side, right);
    }

    /// F3: the old box and layout, set in the menus' type, the LP bar as a gold line with a diamond.
    void Classic(int side)
    {
        bool right = side == 1;
        float w = 600, h = 230, d = 216, lp = side == 0 ? LpMe : LpThem;
        var r = right ? new Rect2(W - w - 36, 36, w, h) : new Rect2(36, H - h - 36, w, h);
        var p = Box(this, r, 22, null, SideC(side) with { A = 0.6f });
        var f = Framed(side, d); f.Position = new Vector2(8, (h - d) / 2); p.AddChild(f);
        float tx = d + 24, tw = w - d - 56;
        Put(p, Who(side), Title, 32, SideLight(side), new Vector2(tx, 22), new Vector2(tw, 40));
        Put(p, "LP", Title, 22, GoldLight, new Vector2(tx, 110), new Vector2(60, 30));
        Put(p, $"{lp:0}", Title, 84, Silver, new Vector2(tx, 62), new Vector2(tw, 100), HorizontalAlignment.Right);
        LpLine(p, new Vector2(tx, 178), tw, lp / 8000f, side);
    }

    // ---------------------------------------------------------------- turn circle (right side, where the phase button is)

    static readonly Vector2 TurnC = new(2660, 900);
    const float R = 165;

    /// 1: the disc split by a thin rift: their half on top, yours below; the half whose turn it is glows.
    void RiftDisc()
    {
        var c = TurnC;
        Paint(this, new Rect2(c - new Vector2(R + 40, R + 40), new Vector2(2 * R + 80, 2 * R + 80)), b =>
        {
            var o = b.Size / 2;
            for (int i = 0; i < 5; i++) b.DrawArc(o, R + 8 + i * 5, 0, Mathf.Pi, 64, GoldC with { A = 0.12f - i * 0.02f }, 6, true);   // your half glows
            b.DrawCircle(o, R, new Color(0.03f, 0.03f, 0.045f, 0.96f));
            List<Vector2> Half(float a0) { var l = new List<Vector2> { o }; for (int i = 0; i <= 48; i++) l.Add(o + Vector2.FromAngle(a0 + Mathf.Pi * i / 48f) * R); return l; }
            b.DrawColoredPolygon(Half(Mathf.Pi).ToArray(), TheirC with { A = 0.12f });
            b.DrawColoredPolygon(Half(0).ToArray(), GoldC with { A = 0.28f });
            b.DrawArc(o, R, 0, Mathf.Pi, 64, GoldLight, 7, true);
            b.DrawArc(o, R, Mathf.Pi, Mathf.Tau, 64, TheirC with { A = 0.5f }, 4, true);
            var crack = new List<Vector2>(); var rng = new RandomNumberGenerator { Seed = 3 };
            for (int i = 0; i <= 14; i++) crack.Add(o + new Vector2(-R + 2 * R * i / 14f, i is 0 or 14 ? 0 : rng.RandfRange(-6, 6)));
            b.DrawPolyline(crack.ToArray(), Colors.White with { A = 0.85f }, 3, true);
        });
        Put(this, "TURN 4", Title, 28, Mist, new Vector2(c.X - R, c.Y - 92), new Vector2(2 * R, 36), HorizontalAlignment.Center);
        Put(this, Phases[PhaseNow], Title, 50, GoldLight, new Vector2(c.X - R, c.Y + 18), new Vector2(2 * R, 62), HorizontalAlignment.Center);
        Put(this, "YOUR TURN", Title, 20, GoldLight with { A = 0.8f }, new Vector2(c.X - R, c.Y + 86), new Vector2(2 * R, 28), HorizontalAlignment.Center);
    }

    /// 2: a dial: six diamonds round the rim, the passed ones joined by a gold arc; turn and phase in the middle.
    void Dial()
    {
        var c = TurnC;
        float a0 = Mathf.Pi * 0.75f, span = Mathf.Pi * 1.5f;
        Paint(this, new Rect2(c - new Vector2(R + 60, R + 60), new Vector2(2 * R + 120, 2 * R + 120)), b =>
        {
            var o = b.Size / 2;
            b.DrawCircle(o, R, new Color(0.03f, 0.03f, 0.045f, 0.96f));
            b.DrawArc(o, R, 0, Mathf.Tau, 128, GoldC with { A = 0.7f }, 4, true);
            b.DrawArc(o, R - 18, 0, Mathf.Tau, 128, GoldC with { A = 0.2f }, 2, true);
            b.DrawArc(o, R + 30, a0, a0 + span, 64, Mist with { A = 0.25f }, 3, true);
            b.DrawArc(o, R + 30, a0, a0 + span * PhaseNow / 5f, 64, GoldLight, 6, true);
        });
        for (int i = 0; i < 6; i++)
        {
            var pip = Pip(this, c + Vector2.FromAngle(a0 + span * i / 5f) * (R + 30), i == PhaseNow ? 30 : 16);
            if (i > PhaseNow) pip.Modulate = new Color(1, 1, 1, 0.4f);
        }
        Put(this, "TURN 4", Title, 28, Mist, new Vector2(c.X - R, c.Y - 80), new Vector2(2 * R, 36), HorizontalAlignment.Center);
        Put(this, Phases[PhaseNow], Title, 52, Silver, new Vector2(c.X - R, c.Y - 36), new Vector2(2 * R, 64), HorizontalAlignment.Center);
        Put(this, "NEXT · BATTLE", Title, 20, GoldLight, new Vector2(c.X - R, c.Y + 42), new Vector2(2 * R, 28), HorizontalAlignment.Center);
    }

    /// 3: a black lacquer coin with a gold rim (the coin from the toss), the phase on it; the turn on a gold tab above.
    void Coin()
    {
        var c = TurnC;
        Paint(this, new Rect2(c - new Vector2(R + 60, R + 60), new Vector2(2 * R + 120, 2 * R + 120)), b =>
        {
            var o = b.Size / 2;
            for (int i = 0; i < 8; i++) b.DrawArc(o, R + 10 + i * 6, 0, Mathf.Tau, 128, GoldC with { A = 0.12f - i * 0.014f }, 6, true);   // your-turn halo
            b.DrawCircle(o + new Vector2(0, 12), R, new Color(0, 0, 0, 0.5f));
            b.DrawCircle(o, R, new Color(0.7f, 0.5f, 0.18f));
            b.DrawCircle(o, R - 12, new Color(0.04f, 0.035f, 0.045f));
            b.DrawArc(o, R - 28, 0, Mathf.Tau, 128, GoldC with { A = 0.75f }, 3, true);
            b.DrawArc(o, R - 38, 0, Mathf.Tau, 128, GoldC with { A = 0.3f }, 1.5f, true);
            b.DrawArc(o, R - 6, -2.4f, -0.8f, 40, new Color(1, 0.95f, 0.8f, 0.7f), 4, true);   // rim light
        });
        var tab = Box(this, new Rect2(c.X - 95, c.Y - R - 54, 190, 54), 27, new Color(0.7f, 0.5f, 0.18f));
        Put(tab, "TURN 4", Title, 24, Ink, new Vector2(0, 11), new Vector2(190, 32), HorizontalAlignment.Center);
        Put(this, Phases[PhaseNow], Title, 50, GoldLight, new Vector2(c.X - R, c.Y - 34), new Vector2(2 * R, 62), HorizontalAlignment.Center);
        Pip(this, new Vector2(c.X, c.Y + 56), 12);
    }

    // ---------------------------------------------------------------- effect prompt

    const string Q = "ACTIVATE MIRROR FORCE?", Why = "Battle Ox attacks. Destroy all of their Attack Position monsters.";
    CardData Mirror => Db.Named("Mirror Force");

    /// 1: a callout beside the set card it comes from, a line pointing at it.
    void Callout()
    {
        var card = new Vector2(1300, 1135);   // your second Spell & Trap zone on screen
        var r = new Rect2(1460, 640, 980, 330);
        Paint(this, new Rect2(0, 0, W, H), b =>
        {
            b.DrawLine(card, new Vector2(r.Position.X + 40, r.End.Y), GoldLight, 3, true);
            b.DrawCircle(card, 10, GoldLight);
        });
        var p = Box(this, r, 24, null, GoldC);
        Card(p, Mirror, new Vector2(120, 165), 270, 0, 0.4f);
        Put(p, Q, Title, 38, Silver, new Vector2(240, 34), new Vector2(720, 50));
        Wrap(p, Why, Body, 28, Mist, new Vector2(242, 96), 690);
        Pill(p, "ACTIVATE", new Vector2(420, 262), true, 28, 300);
        Pill(p, "NOT NOW", new Vector2(760, 262), false, 28, 280);
    }

    /// 2: a bar just above your hand.
    void HandBar()
    {
        var p = Box(this, new Rect2(W / 2 - 900, 1330, 1800, 170), 85, null, GoldC);
        Card(p, Mirror, new Vector2(110, 85), 140, 0, 0.3f);
        Put(p, Q, Title, 38, Silver, new Vector2(190, 28), new Vector2(900, 50));
        Put(p, Why, Body, 26, Mist, new Vector2(192, 92), new Vector2(980, 36));
        Pill(p, "ACTIVATE", new Vector2(1340, 85), true, 28, 280);
        Pill(p, "NOT NOW", new Vector2(1640, 85), false, 28, 260);
    }

    /// 3: in the left column where the card detail sits: the card large, its text, the two answers under it.
    void SidePrompt()
    {
        var p = Box(this, new Rect2(40, 250, 600, 1180), 24, null, GoldC);
        Put(p, "CHAIN?", Title, 26, GoldLight, new Vector2(0, 30), new Vector2(600, 34), HorizontalAlignment.Center);
        Card(p, Mirror, new Vector2(300, 370), 560, 0, 0.6f, true);
        Wrap(p, Q, Title, 38, Silver, new Vector2(30, 680), 540, HorizontalAlignment.Center);
        Wrap(p, Why, Body, 26, Mist, new Vector2(50, 800), 500, HorizontalAlignment.Center);
        Pill(p, "ACTIVATE", new Vector2(300, 990), true, 30, 420);
        Pill(p, "NOT NOW", new Vector2(300, 1100), false, 30, 420);
    }

    // ---------------------------------------------------------------- pause

    void Volume(Control host, string name, Vector2 at, float w, float v)
    {
        Put(host, name, Title, 24, Mist, at, new Vector2(150, 32));
        float x0 = at.X + 160, tw = w - 160;
        Hair(host, new Vector2(x0, at.Y + 16), tw, Mist with { A = 0.3f }, 3);
        Hair(host, new Vector2(x0, at.Y + 14), tw * v, GoldC, 6);
        Pip(host, new Vector2(x0 + tw * v, at.Y + 17), 22);
    }

    void MenuRows(Control host, float x, float y, float w, float gap, int size, bool centred)
    {
        string[] rows = { "RESUME", "DUEL SPEED · FASTER", "CARD TEXT · SHORT" };
        var al = centred ? HorizontalAlignment.Center : HorizontalAlignment.Left;
        for (int i = 0; i < rows.Length; i++)
        {
            float yy = y + i * gap;
            Put(host, rows[i], Title, size, i == 0 ? GoldLight : Silver, new Vector2(x, yy), new Vector2(w, size * 1.4f), al);
            if (i == 0 && !centred) Pip(host, new Vector2(x - 34, yy + size * 0.7f), 15);
        }
        Volume(host, "MUSIC", new Vector2(x, y + 3 * gap), w, 0.69f);
        Volume(host, "SOUND", new Vector2(x, y + 3 * gap + 74), w, 0.47f);
        Hair(host, new Vector2(x, y + 3 * gap + 168), w, Mist with { A = 0.2f }, 2);
        Put(host, "SURRENDER", Title, size, Crimson, new Vector2(x, y + 3 * gap + 200), new Vector2(w, size * 1.4f), al);
    }

    /// 1: a drawer from the right edge; the field stays in view.
    void Drawer()
    {
        AddChild(new ColorRect { Size = new Vector2(W, H), Color = new Color(0, 0, 0, 0.3f), MouseFilter = MouseFilterEnum.Ignore });
        var p = Box(this, new Rect2(W - 760, -30, 800, H + 60), 0);
        Hair(p, Vector2.Zero, 4, GoldC).Size = new Vector2(4, H + 60);
        Put(p, "PAUSED", Title, 64, Silver, new Vector2(90, 150));
        Put(p, $"TURN 4  ·  MAIN 1  ·  VS {Session.Rival.Name}", Title, 22, Mist, new Vector2(92, 236));
        Hair(p, new Vector2(92, 280), 180, GoldC, 2);
        MenuRows(p, 130, 380, 560, 120, 40, false);
    }

    /// 2: a small card in the middle.
    void Compact()
    {
        AddChild(new ColorRect { Size = new Vector2(W, H), Color = new Color(0, 0, 0, 0.45f), MouseFilter = MouseFilterEnum.Ignore });
        float w = 800, h = 900;
        var p = Box(this, new Rect2(W / 2 - w / 2, H / 2 - h / 2 - 40, w, h), 30, null, GoldC);
        Put(p, "PAUSED", Title, 58, Silver, new Vector2(0, 54), new Vector2(w, 70), HorizontalAlignment.Center);
        Pip(p, new Vector2(w / 2, 142), 14);
        MenuRows(p, 90, 200, w - 180, 108, 36, true);
    }

    /// 3: a list dropping from the MENU button, top right; no dim.
    void DropDown()
    {
        var r = new Rect2(W - 760, 330, 700, 820);
        Paint(this, new Rect2(W - 200, 300, 50, 32), b => b.DrawColoredPolygon(new[] { new Vector2(0, 32), new Vector2(25, 0), new Vector2(50, 32) }, Panel));
        var p = Box(this, r, 22, null, GoldC);
        MenuRows(p, 100, 60, 520, 112, 36, false);
    }

    // ---------------------------------------------------------------- card search (never the whole screen)

    void Detail(Control host, CardData d, Vector2 at, float w, HorizontalAlignment a = HorizontalAlignment.Left)
    {
        Put(host, d.Name.ToUpperInvariant(), Title, 34, Silver, at, new Vector2(w, 46), a);
        Put(host, $"LEVEL {d.Level}  ·  ATK {d.Atk}  ·  DEF {d.Def}", Title, 22, GoldLight, at + new Vector2(0, 52), new Vector2(w, 30), a);
    }

    /// 1: a tray above your hand: one row of cards, the chosen one lifted; the field above stays clear.
    void Tray()
    {
        var cards = Cards();
        var p = Box(this, new Rect2(W / 2 - 1240, 960, 2480, 620), 28, null, GoldC);
        Put(p, "SPECIAL SUMMON WHICH MONSTER?", Title, 38, Silver, new Vector2(60, 36), new Vector2(1600, 50));
        Put(p, "FROM YOUR GRAVEYARD  ·  9 CARDS  ·  CHOOSE 1", Title, 22, GoldLight, new Vector2(62, 96), new Vector2(1400, 30));
        for (int i = 0; i < 7; i++)
        {
            bool sel = i == Sel;
            var c = new Vector2(190 + i * 245, 350 - (sel ? 30 : 0));
            Card(p, cards[i], c, sel ? 360 : 320, 0, sel ? 1f : 0.5f);
            if (sel) Pip(p, new Vector2(c.X, 575), 16);
        }
        Put(p, "›", Title, 70, GoldLight, new Vector2(1820, 290), new Vector2(50, 90));
        Hair(p, new Vector2(1900, 150), 3, Mist with { A = 0.2f }).Size = new Vector2(3, 420);
        Detail(p, cards[Sel], new Vector2(1950, 170), 500);
        Pill(p, "CONFIRM", new Vector2(2190, 410), true, 30, 380);
        Pill(p, "CANCEL", new Vector2(2190, 520), false, 28, 380);
    }

    /// 2: a column on the right: a 3-wide grid; the chosen card large with its text in the left detail column.
    void Column()
    {
        var cards = Cards();
        var p = Box(this, new Rect2(W - 900, 230, 860, 1340), 24, null, GoldC);
        Wrap(p, "SPECIAL SUMMON WHICH MONSTER?", Title, 34, Silver, new Vector2(50, 36), 760);
        Put(p, "GRAVEYARD  ·  CHOOSE 1", Title, 22, GoldLight, new Vector2(52, 130), new Vector2(700, 30));
        for (int i = 0; i < 9; i++)
        {
            bool sel = i == Sel;
            var c = new Vector2(165 + (i % 3) * 265, 360 + (i / 3) * 330);
            Card(p, cards[i], c, 300, 0, sel ? 1f : 0.4f);
            if (sel) Paint(p, new Rect2(c - new Vector2(113, 160), new Vector2(226, 320)), b => b.DrawRect(new Rect2(Vector2.Zero, b.Size), GoldLight, false, 6));
        }
        Pill(p, "CONFIRM", new Vector2(260, 1260), true, 28, 330);
        Pill(p, "CANCEL", new Vector2(620, 1260), false, 26, 280);
        var d = Box(this, new Rect2(40, 250, 600, 1180), 24);
        Card(d, cards[Sel], new Vector2(300, 370), 560, 0, 0.6f, true);
        Detail(d, cards[Sel], new Vector2(40, 680), 520, HorizontalAlignment.Center);
        Wrap(d, cards[Sel].TcgDesc ?? cards[Sel].Desc, Body, 26, Mist, new Vector2(50, 790), 500, HorizontalAlignment.Center);
    }

    /// 3: the cards fanned over your hand like a held hand; the chosen one rises. No panel.
    void Fan()
    {
        var cards = Cards();
        AddChild(new ColorRect { Size = new Vector2(W, H), Color = new Color(0, 0, 0, 0.4f), MouseFilter = MouseFilterEnum.Ignore });
        var pivot = new Vector2(W / 2, 3700);
        int n = cards.Count;
        for (int i = 0; i < n; i++)
        {
            bool sel = i == Sel;
            float a = (i - (n - 1) / 2f) * 0.06f;
            var c = pivot + Vector2.FromAngle(-Mathf.Pi / 2 + a) * (2420 + (sel ? 110 : 0));
            var card = Card(this, cards[i], c, sel ? 470 : 420, a, sel ? 1.3f : 0.7f);
            if (sel) card.ZIndex = 2;
        }
        Put(this, "SPECIAL SUMMON WHICH MONSTER?", Title, 44, Silver, new Vector2(0, 700), new Vector2(W, 56), HorizontalAlignment.Center);
        Put(this, "FROM YOUR GRAVEYARD  ·  CHOOSE 1", Title, 24, GoldLight, new Vector2(0, 768), new Vector2(W, 32), HorizontalAlignment.Center);
        Detail(this, cards[Sel], new Vector2(W / 2 - 600, 1580), 1200, HorizontalAlignment.Center);
        Pill(this, "CONFIRM", new Vector2(W / 2 - 200, 1730), true, 28, 300);
        Pill(this, "CANCEL", new Vector2(W / 2 + 200, 1730), false, 26, 260);
    }
}
