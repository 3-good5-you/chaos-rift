using Godot;
using PoC.Engine;
using static PoC.Game.UI.ConceptKit;

namespace PoC.Game.UI;

/// Duel screen samples in the Chaos Rift style (2026-11-08), drawn as 2D mockups (the table in perspective).
/// `--screen=duel --variant=a|b|c`
/// a = Rift table: the table sits on the rift; the card you point at floats large in the light on the left with its
///     text; the rival's portrait and LP top right in the dark, yours bottom left; the phases run down the right edge.
/// b = Face-off: a real playmat; the rival towers behind the table in the dark; both LP as big numbers with a gold
///     gauge over and under the table; phases as six diamonds under the table; the card you point at pops up beside it.
/// c = Diagonal rift: the tear runs corner to corner across the whole field, your half in the light, theirs in the
///     dark; a minimal HUD in the corners.
public partial class RiftScreens
{
    // the table: plane x in [-1, 1] (left-right), z in [0, 1] (you -> rival)
    float tcx, tNear, tFar, tHalf, tK = 1.25f;

    Vector2 Proj(float x, float z)
    {
        float s = 1f / (1f + tK * z), s1 = 1f / (1f + tK);
        float t = (s - s1) / (1f - s1);            // 1 at the near edge, 0 at the far edge
        float y = tFar + (tNear - tFar) * t;
        return new Vector2(tcx + x * tHalf * (0.62f + 0.38f * t), y);
    }

    /// A flat textured quad on the table, split into strips so the perspective holds.
    Polygon2D Flat(Texture2D? tex, float x0, float z0, float x1, float z1, Color? tint = null, int strips = 1, bool rot = false)
    {
        var pts = new List<Vector2>(); var uvs = new List<Vector2>(); var polys = new Godot.Collections.Array();
        var size = tex != null ? new Vector2(tex.GetWidth(), tex.GetHeight()) : Vector2.One;
        for (int i = 0; i <= strips; i++)
        {
            float f = (float)i / strips, z = Mathf.Lerp(z0, z1, f);
            pts.Add(Proj(x0, z)); pts.Add(Proj(x1, z));
            // texture top = far edge (unless rotated: the rival's cards face them)
            float v = rot ? f : 1 - f;
            uvs.Add(new Vector2(rot ? 1 : 0, v) * size); uvs.Add(new Vector2(rot ? 0 : 1, v) * size);
        }
        for (int i = 0; i < strips; i++) polys.Add(new[] { 2 * i, 2 * i + 1, 2 * i + 3, 2 * i + 2 });
        var p = new Polygon2D { Polygon = pts.ToArray(), UV = uvs.ToArray(), Polygons = polys, Texture = tex, Color = tint ?? Colors.White, TextureFilter = TextureFilterEnum.LinearWithMipmaps };
        AddChild(p);
        return p;
    }

    /// A quad outline on the table (a zone).
    void Zone(float x0, float z0, float x1, float z1, Color c, float w = 1.5f)
    {
        var a = Proj(x0, z0); var b = Proj(x1, z0); var d = Proj(x1, z1); var e = Proj(x0, z1);
        AddChild(new Line2D { Points = new[] { a, b, d, e, a }, Width = w, DefaultColor = c, JointMode = Line2D.LineJointMode.Round });
    }

    static readonly float[] Cols = { -0.86f, -0.57f, -0.29f, 0f, 0.29f, 0.57f, 0.86f };
    const float CW = 0.105f, CD = 0.062f;   // half card size on the plane

    /// The 7 x 4 zone grid: Field/Fusion | 5 monsters / 5 spell-traps | GY/Deck, mirrored for the rival.
    void Zones(Color line, Color dim)
    {
        float[] rows = { 0.11f, 0.3f, 0.7f, 0.89f };
        foreach (var z in rows)
            for (int c = 0; c < 7; c++)
                Zone(Cols[c] - CW, z - CD, Cols[c] + CW, z + CD, c is 0 or 6 ? dim : line);
    }

    void FieldCard(string name, int col, float z, bool rival = false, bool faceDown = false, bool defense = false, Color? tint = null)
    {
        var d = Named(name);
        var tex = faceDown ? Cosmetics.Back(rival ? "07_fire" : Session.Profile.Back, 256) : CardArt.Small(d);
        float cw = CW * 0.92f, cd = CD * 0.98f;
        if (defense) (cw, cd) = (cd * 1.55f, cw * 0.62f);
        Flat(tex, Cols[col] - cw, z - cd, Cols[col] + cw, z + cd, tint, 4, rival);
    }

    void SampleField(bool rivalTint = false)
    {
        FieldCard("Dark Magician", 3, 0.3f);
        FieldCard("Celtic Guardian", 2, 0.3f);
        FieldCard("Kuriboh", 4, 0.3f, faceDown: true, defense: true);
        FieldCard("Mirror Force", 2, 0.11f, faceDown: true);
        FieldCard("Yami", 0, 0.11f);
        FieldCard("Blue-Eyes White Dragon", 3, 0.7f, rival: true);
        FieldCard("Battle Ox", 1, 0.7f, rival: true, faceDown: true, defense: true);
        FieldCard("Trap Hole", 4, 0.89f, rival: true, faceDown: true);
        // decks and graveyards
        Flat(Cosmetics.Back(Session.Profile.Back, 256), Cols[6] - CW * 0.92f, 0.11f - CD, Cols[6] + CW * 0.92f, 0.11f + CD, null, 2);
        Flat(Cosmetics.Back("07_fire", 256), Cols[0] - CW * 0.92f, 0.89f - CD, Cols[0] + CW * 0.92f, 0.89f + CD, null, 2, true);
        Flat(CardArt.Small(Named("Mystical Elf")), Cols[6] - CW * 0.92f, 0.3f - CD, Cols[6] + CW * 0.92f, 0.3f + CD, new Color(1, 1, 1, 0.85f), 2);
    }

    void Hand(Vector2 centre, float h, float spread = 120)
    {
        string[] hand = { "Monster Reborn", "Gaia The Fierce Knight", "Pot of Greed", "Swords of Revealing Light", "Summoned Skull" };
        for (int i = 0; i < hand.Length; i++)
        {
            float k = i - (hand.Length - 1) / 2f;
            var c = Card(Named(hand[i]), centre + new Vector2(k * spread, Math.Abs(k) * 10 - (i == 1 ? 40 : 0)), h, k * 0.06f, true, 0.8f);
        }
    }

    string[] Phases = { "DRAW", "STANDBY", "MAIN 1", "BATTLE", "MAIN 2", "END" };

    void DuelS()
    {
        switch (variant)
        {
            case "b": DuelFaceOff(); break;
            case "c": DuelDiagonal(); break;
            default: DuelRiftTable(); break;
        }
    }

    // a ---------------------------------------------------------------- rift table
    void DuelRiftTable()
    {
        Rift(0.62f, 0.54f);
        tcx = W * 0.6f; tNear = H * 0.84f; tFar = H * 0.2f; tHalf = W * 0.31f;
        // the table: dark glass, the half nearest you lit, gold zone lines
        var glass = Flat(null, -1.08f, -0.04f, 1.08f, 1.04f, new Color(0.04f, 0.04f, 0.06f, 0.55f), 8);
        Flat(null, -1.08f, -0.04f, 1.08f, 0.5f, new Color(0.95f, 0.9f, 0.8f, 0.08f), 4);
        Zone(-1.08f, -0.04f, 1.08f, 1.04f, GoldC * new Color(1, 1, 1, 0.7f), 2.5f);
        AddChild(new Line2D { Points = new[] { Proj(-1.08f, 0.5f), Proj(1.08f, 0.5f) }, Width = 2, DefaultColor = Silver * new Color(1, 1, 1, 0.5f) });
        Zones(Silver * new Color(1, 1, 1, 0.28f), Silver * new Color(1, 1, 1, 0.15f));
        SampleField();
        Hand(new Vector2(W * 0.6f, H - 30), 170, 100);
        // the card you point at, in the light
        var d = Named("Dark Magician");
        Card(d, new Vector2(200, H * 0.36f), H * 0.42f, -0.03f);
        C(this, d.Name.ToUpperInvariant(), Title, 26, Ink, new Vector2(60, H * 0.6f));
        C(this, "DARK  ·  SPELLCASTER  ·  LV 7", Thin, 15, Graphite, new Vector2(60, H * 0.6f + 40));
        C(this, "ATK 2500   DEF 2100", Title, 22, GoldDeep, new Vector2(60, H * 0.6f + 70));
        var t = C(this, d.TcgDesc ?? d.Desc, Body, 17, Graphite, new Vector2(60, H * 0.6f + 110), new Vector2(300, 120));
        t.AutowrapMode = TextServer.AutowrapMode.WordSmart; t.CustomMinimumSize = new Vector2(300, 0); t.Size = new Vector2(300, 120);
        // LP: rival top right, you bottom left
        LpBlock(new Vector2(W - 420, 40), "KAIBA", RivalPanels.Portrait(RivalPanels.All[1]), 6200, false);
        LpBlock(new Vector2(60, H - 150), Settings.PlayerName.ToUpperInvariant(), Settings.Avatar, 7500, true);
        // phases down the right edge
        float py = H * 0.3f;
        for (int i = 0; i < Phases.Length; i++)
        {
            bool on = i == 3;
            C(this, Phases[i], Title, on ? 22 : 16, on ? GoldLight : Mist * new Color(1, 1, 1, 0.55f), new Vector2(W - 210, py), new Vector2(170, 30), HorizontalAlignment.Right);
            if (on) Pip(new Vector2(W - 30, py + 14), 10);
            py += 54;
        }
        C(this, "TURN 4", Thin, 16, Mist, new Vector2(W - 210, py + 10), new Vector2(170, 22), HorizontalAlignment.Right);
    }

    void LpBlock(Vector2 at, string name, Texture2D? pic, int lp, bool light)
    {
        var ink = light ? Ink : Silver; var gold = light ? GoldDeep : GoldLight;
        var f = Cosmetics.Framed(pic, light ? Session.Profile.Frame : Shop.DefaultFrame, 96);
        f.Position = at; AddChild(f);
        C(this, name, Title, 20, ink, at + new Vector2(112, 6));
        C(this, $"{lp}", Title, 52, ink, at + new Vector2(110, 30));
        C(this, "LP", Thin, 16, gold, at + new Vector2(112 + Title.GetStringSize($"{lp}", HorizontalAlignment.Left, -1, 52).X + 8, 62));
        Hair(at + new Vector2(112, 98), 220, ink * new Color(1, 1, 1, 0.2f));
        Hair(at + new Vector2(112, 97), 220 * lp / 8000f, GoldC, null, 3);
    }

    // b ---------------------------------------------------------------- face-off
    void DuelFaceOff()
    {
        Rift(0.5f, 0.5f);
        tcx = W * 0.57f; tNear = H * 0.84f; tFar = H * 0.32f; tHalf = W * 0.33f;
        // the rival behind the table
        var tex = RivalPanels.Portrait(RivalPanels.All[1]);
        float ph = H * 0.62f, pw = ph * tex.GetWidth() / tex.GetHeight();
        var rp = Ink_(tex, new Vector2(W * 0.72f - pw / 2, -20), new Vector2(pw, ph));
        rp.Modulate = new Color(0.75f, 0.75f, 0.85f, 0.7f);
        rp.Position = new Vector2(W * 0.84f - pw / 2, -10);
        Flat(Cosmetics.MatPreview("radiant"), -1.06f, -0.04f, 1.06f, 1.04f, new Color(1, 1, 1, 0.92f), 16);
        Zone(-1.06f, -0.04f, 1.06f, 1.04f, GoldC, 2.5f);
        SampleField();
        Hand(new Vector2(W * 0.57f, H - 20), 160, 96);
        // LP: rival centred above the table, you under it
        void Lp(Vector2 c, string name, int lp, bool light)
        {
            var ink = light ? Ink : Silver;
            T(this, name, Title, 20, c + new Vector2(-400, -60), new Vector2(800, 26), HorizontalAlignment.Center);
            T(this, $"{lp}", Title, 58, c + new Vector2(-400, -36), new Vector2(800, 70), HorizontalAlignment.Center);
            Hair(c + new Vector2(-260, 40), 520, ink * new Color(1, 1, 1, 0.2f));
            Hair(c + new Vector2(-260 + 520 * (1 - lp / 8000f) / 2, 39), 520 * lp / 8000f, GoldC, null, 3);
        }
        Lp(new Vector2(W * 0.57f, H * 0.14f), "KAIBA", 6200, false);
        var f = Cosmetics.Framed(Settings.Avatar, Session.Profile.Frame, 120);
        f.Position = new Vector2(70, H - 190); AddChild(f);
        C(this, Settings.PlayerName.ToUpperInvariant(), Title, 22, Ink, new Vector2(200, H - 180));
        C(this, "7500", Title, 60, Ink, new Vector2(196, H - 150));
        C(this, "LP", Thin, 18, GoldDeep, new Vector2(350, H - 112));
        // phases: six diamonds under the table's far edge
        float px = W * 0.57f - 2.5f * 130;
        for (int i = 0; i < Phases.Length; i++)
        {
            var at = new Vector2(px + i * 130, H * 0.26f);
            bool on = i == 3;
            if (on) Pip(at, 14);
            else AddChild(new ColorRect { Position = at - new Vector2(4, 4), Size = new Vector2(8, 8), Rotation = Mathf.Pi / 4, PivotOffset = new Vector2(4, 4), Color = InkFor(at) * new Color(1, 1, 1, 0.4f) });
            C(this, Phases[i], Thin, 13, on ? (OnLight(at) ? GoldDeep : GoldLight) : InkFor(at) * new Color(1, 1, 1, 0.5f), at + new Vector2(-60, 12), new Vector2(120, 18), HorizontalAlignment.Center);
        }
        // the card you point at pops up beside the table
        var d = Named("Blue-Eyes White Dragon");
        Card(d, new Vector2(W * 0.1f, H * 0.38f), H * 0.36f, -0.04f);
        C(this, "ATK 3000  DEF 2500", Title, 18, GoldDeep, new Vector2(20, H * 0.6f), new Vector2(340, 26), HorizontalAlignment.Center);
    }

    // c ---------------------------------------------------------------- diagonal rift across the field
    void DuelDiagonal()
    {
        Rift(-0.15f, 1.15f);
        tcx = W * 0.5f; tNear = H * 0.9f; tFar = H * 0.12f; tHalf = W * 0.42f;
        Flat(null, -1.06f, -0.04f, 1.06f, 1.04f, new Color(0.5f, 0.5f, 0.55f, 0.08f), 8);
        Zone(-1.06f, -0.04f, 1.06f, 1.04f, GoldC * new Color(1, 1, 1, 0.8f), 2);
        Zones(Mist * new Color(1, 1, 1, 0.45f), Mist * new Color(1, 1, 1, 0.25f));
        SampleField();
        Hand(new Vector2(W * 0.5f, H + 30), 170, 100);
        // corners: you bottom left (light), rival top right (dark)
        C(this, Settings.PlayerName.ToUpperInvariant(), Title, 20, Ink, new Vector2(50, H - 160));
        C(this, "7500", Title, 72, Ink, new Vector2(44, H - 134));
        Hair(new Vector2(52, H - 44), 260, GoldC, null, 3);
        C(this, "KAIBA", Title, 20, Silver, new Vector2(W - 350, 40), new Vector2(300, 26), HorizontalAlignment.Right);
        C(this, "6200", Title, 72, Silver, new Vector2(W - 350, 64), new Vector2(300, 90), HorizontalAlignment.Right);
        Hair(new Vector2(W - 50 - 260 * 6200 / 8000f, 160), 260 * 6200 / 8000f, GoldC, null, 3);
        // phase as one word on the rift, the turn under it
        C(this, "BATTLE PHASE  ·  TURN 4", Title, 18, GoldLight, new Vector2(W - 650, 170), new Vector2(600, 26), HorizontalAlignment.Right);
        // card detail, top left in the light
        var d = Named("Celtic Guardian");
        Card(d, new Vector2(150, 230), 300, -0.03f);
        C(this, d.Name.ToUpperInvariant(), Title, 20, Silver, new Vector2(260, 100), new Vector2(400, 30));
        C(this, $"ATK {d.Atk}  DEF {d.Def}", Thin, 18, GoldLight, new Vector2(260, 134));
    }
}
