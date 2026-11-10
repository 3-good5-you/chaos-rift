using Godot;
using PoC.Engine;
using static PoC.Game.UI.ConceptKit;

namespace PoC.Game.UI;

/// Tournament samples, round 2 (2026-11-08): a mirrored bracket (user's reference): four seats on the light side,
/// four on the dark side, the final and the champion in the middle on the rift. Three ways to show the duelists.
/// `--screen=tour --variant=a|b|c [--state=new|mid|won|out]`
/// a = Nameplates: each seat is a picture tile with the name written on the bracket line; your path and the current
///     match drawn in gold. The prize ladder under the champion.
/// b = Portrait cards: the eight duelists as card-shaped portraits at the outer edges; the current match grows into a
///     big VS in the middle.
/// c = Medallions: round pictures on the lines, beaten ones crossed out; the trophy and the prize ladder in the middle.
public partial class RiftScreens
{
    Tournament tour = null!;

    /// Proposed prizes by where you finish (paid once, when you go out or win). Champion wins big.
    static readonly (string Place, string Prize)[] Ladder =
    {
        ("CHAMPION", "1,500 DP  ·  6 CARDS (3 NEW)  ·  THE FINALIST'S ACE CARD  ·  CHAMPION CARD BACK"),
        ("RUNNER-UP", "600 DP  ·  4 CARDS (1 NEW)"),
        ("SEMIFINAL", "250 DP  ·  2 CARDS"),
        ("QUARTERFINAL", "50 DP"),
    };

    void TourS()
    {
        tour = new Tournament(new Rng(7));
        var st = Demo.Arg("state", "mid");
        if (st is "mid" or "won") { tour.PlayOthers(); tour.Record(true); }
        if (st == "won") { tour.PlayOthers(); tour.Record(true); tour.PlayOthers(); tour.Record(true); }
        if (st == "out") { tour.PlayOthers(); tour.Record(false); }
        switch (variant)
        {
            case "b": TourCards(); break;
            case "c": TourMedals(); break;
            default: TourPlates(); break;
        }
    }

    string TName(string k) => k == Tournament.You ? Settings.PlayerName.ToUpperInvariant() : Tournament.Get(k).Name;
    static Texture2D? TPic(string k, bool portrait = false)
    {
        if (k == Tournament.You) return Settings.Avatar;
        var r = RivalPanels.Of(Tournament.Get(k));
        return portrait && RivalPanels.HasPortrait(r) ? RivalPanels.Portrait(r) : RivalPanels.Art(r);
    }
    bool Lost(int round, int match, string who) => tour.Winners[round][match] is { } w && w != who;
    /// Out at any round (for drawing a seat).
    bool Out(string k)
    {
        for (int r = 0; r < 3; r++)
            for (int m = 0; m < tour.Winners[r].Length; m++)
            {
                var (a, b) = tour.Pair(r, m);
                if ((a == k || b == k) && Lost(r, m, k)) return true;
            }
        return false;
    }
    bool InCurrent(string? k) => k != null && tour.Foe != null && (k == Tournament.You || k == tour.Foe.Key);
    string TStatus() => tour.Champion is { } c ? (c == Tournament.You ? "YOU ARE THE CHAMPION" : $"{TName(c)} WINS")
        : tour.YouOut ? "KNOCKED OUT" : Tournament.RoundNames[tour.Round];
    int YourPlace() => tour.Champion == Tournament.You ? 0 : tour.YouOut ? tour.Round switch { 0 => 3, 1 => 2, _ => 1 } : -1;

    /// Mirrored bracket geometry: seat i (0-7) and the slots of later rounds. Left half = seats 0-3 (light side).
    (List<List<Vector2>> Cols, Vector2 Champ) Mirror(float outerX, float stepX, float top, float area, float champY)
    {
        var cols = new List<List<Vector2>>();
        for (int r = 0; r < 3; r++)
        {
            int n = 8 >> r, half = n / 2;
            var col = new List<Vector2>();
            for (int i = 0; i < n; i++)
            {
                bool right = i >= half; int j = right ? i - half : i;
                float y = top + (j + 0.5f) * area / half;
                float x = right ? W - outerX - r * stepX : outerX + r * stepX;
                col.Add(new Vector2(x, y));
            }
            cols.Add(col);
        }
        return (cols, new Vector2(W / 2, champY));
    }

    /// The bracket lines between rounds; gold where a winner climbed, the current match glowing.
    void BracketLines(List<List<Vector2>> cols, Vector2 champ, float inset)
    {
        for (int r = 0; r < 3; r++)
            for (int m = 0; m < cols[r].Count / 2; m++)
            {
                var (pa, pb) = tour.Pair(r, m);
                var win = tour.Winners[r][m];
                bool current = r == tour.Round && (InCurrent(pa) || InCurrent(pb));
                Vector2 to = r == 2 ? champ : cols[r + 1][m];
                foreach (var (k, who) in new[] { (2 * m, pa), (2 * m + 1, pb) })
                {
                    var from = cols[r][k];
                    float dir = from.X < W / 2 ? 1 : -1;
                    var a = from + new Vector2(dir * inset, 0);
                    float mx = r == 2 ? to.X - dir * 30 : (a.X + to.X - dir * inset) / 2;
                    bool lit = win != null && win == who;
                    var col = lit ? GoldC : current ? GoldC * new Color(1, 1, 1, 0.55f) : InkFor(a) * new Color(1, 1, 1, 0.25f);
                    var pts = r == 2 ? new[] { a, new Vector2(mx, a.Y), new Vector2(mx, to.Y) } : new[] { a, new Vector2(mx, a.Y), new Vector2(mx, to.Y), to - new Vector2(dir * inset, 0) };
                    AddChild(new Line2D { Points = pts, Width = lit ? 3.5f : current ? 2.5f : 1.5f, DefaultColor = col, JointMode = Line2D.LineJointMode.Round });
                }
            }
    }

    static readonly (string Place, string Dp, string Extra)[] Prize4 =
    {
        ("CHAMPION", "1,500", "6 CARDS + ACE + CARD BACK"), ("RUNNER-UP", "600", "4 CARDS"), ("SEMIFINAL", "250", "2 CARDS"), ("QUARTERFINAL", "50", ""),
    };

    /// Four prize columns: the place, its DP big, one short line. Champion in gold, your current place marked.
    void Prizes(Vector2 centre, float w, bool dark)
    {
        int place = YourPlace();
        float colW = 230, x0 = centre.X - 2 * colW;
        for (int i = 0; i < 4; i++)
        {
            float cx = x0 + (i + 0.5f) * colW;
            bool champ = i == 0, mine = i == place;
            var at = new Vector2(cx, centre.Y);
            var lit = champ || mine;
            T(this, Prize4[i].Place, Title, 15, new Vector2(cx - 115, centre.Y - 32), new Vector2(230, 22), HorizontalAlignment.Center, 0.9f).Mat().P("lit", lit ? 1f : 0f);
            var dp = T(this, Prize4[i].Dp + " DP", Title, champ ? 34 : 26, new Vector2(cx - 115, centre.Y - 6), new Vector2(230, 44), HorizontalAlignment.Center);
            if (lit) dp.Mat().P("lit", 1f);
            if (mine) Pip(new Vector2(cx, centre.Y + 50), 9);
        }
    }

    void TourHeader()
    {
        C(this, "‹  MENU", Title, 18, Graphite, new Vector2(62, 30));
        C(this, "TOURNAMENT", Title, 40, Ink, new Vector2(80, 58));
        Hair(new Vector2(82, 108), 160, GoldC);
        C(this, TStatus(), Title, 22, GoldLight, new Vector2(W - 680, 66), new Vector2(600, 30), HorizontalAlignment.Right);
        if (tour.Foe != null) C(this, $"NEXT: VS {tour.Foe.Name}", Thin, 17, Mist, new Vector2(W - 680, 102), new Vector2(600, 24), HorizontalAlignment.Right);
    }

    // a ---------------------------------------------------------------- nameplates
    void TourPlates()
    {
        Rift(0.5f, 0.5f);
        TourHeader();
        float plateW = 250, tile = 56;
        var (cols, champ) = Mirror(70, 250, 170, H * 0.62f, H * 0.42f);
        BracketLines(cols, champ, plateW);
        void Plate(Vector2 at, string? key, float scale = 1)
        {
            bool left = at.X < W / 2, you = key == Tournament.You, now = InCurrent(key);
            float tw = tile * scale;
            var ink = InkFor(at);
            if (key == null) { Hair(new Vector2(left ? at.X : at.X - plateW, at.Y), plateW, ink * new Color(1, 1, 1, 0.18f)); return; }
            bool beaten = Out(key);
            // the picture tile at the outer end, the name sitting on the line
            var picAt = new Vector2(left ? at.X : at.X - tw, at.Y - tw / 2);
            if (you) { var f = Cosmetics.Framed(Settings.Avatar, Session.Profile.Frame, tw + 10); f.Position = picAt - new Vector2(5, 5); AddChild(f); }
            else
            {
                Shadow(this, picAt, new Vector2(tw, tw), beaten ? 0.1f : 0.3f, OnLight(at) ? new Color(0.1f, 0.1f, 0.12f) : new Color(0.35f, 0.36f, 0.45f));
                var p = Ink_(TPic(key)!, picAt, new Vector2(tw, tw)); p.Material = Kit.RoundedMat(1, 0.12f);
                if (beaten) p.Modulate = new Color(0.55f, 0.55f, 0.6f, 0.45f);
            }
            var nameCol = beaten ? ink * new Color(1, 1, 1, 0.35f) : you || now ? (OnLight(at) ? GoldDeep : GoldLight) : ink;
            float nx = left ? at.X + tw + 14 : at.X - plateW;
            C(this, TName(key), Title, (int)(19 * Math.Min(scale, 1.1f)), nameCol, new Vector2(nx, at.Y - 28), new Vector2(plateW - tw - 14, 26), left ? HorizontalAlignment.Left : HorizontalAlignment.Right).ClipText = true;
            if (!you && !beaten) C(this, new string('●', Tournament.Get(key).Level), Body, 10, Session.TierColor(Tournament.Get(key).Level), new Vector2(nx, at.Y + 6), new Vector2(plateW - tw - 14, 14), left ? HorizontalAlignment.Left : HorizontalAlignment.Right);
            Hair(new Vector2(left ? at.X + tw + 6 : at.X - plateW, at.Y), plateW - tw - 6, beaten ? ink * new Color(1, 1, 1, 0.15f) : you || now ? GoldC : ink * new Color(1, 1, 1, 0.4f), null, you || now ? 2.5f : 1.5f);
        }
        for (int i = 0; i < 8; i++) Plate(cols[0][i], tour.Seats[i]);
        for (int r = 1; r < 3; r++)
            for (int i = 0; i < cols[r].Count; i++) Plate(cols[r][i], tour.Winners[r - 1][i], 1 + r * 0.1f);
        // champion seat on the rift
        Pip(champ, 26);
        T(this, "CHAMPION", Title, 22, new Vector2(W / 2 - 200, champ.Y - 80), new Vector2(400, 30), HorizontalAlignment.Center).Mat().P("lit", 1f);
        Prizes(new Vector2(W / 2, H - 190), 1300, false);
        focusables.Add(Button(tour.Foe != null ? "DUEL" : "NEW TOURNAMENT", new Vector2(W / 2, champ.Y + 150), 28));
    }

    // b ---------------------------------------------------------------- portrait cards + VS in the middle
    void TourCards()
    {
        Rift(0.5f, 0.5f);
        TourHeader();
        float d0 = 104;
        var (cols, champ) = Mirror(60 + d0 / 2, 210, 170, H * 0.62f, H * 0.42f);
        BracketLines(cols, champ, d0 / 2 + 8);
        for (int i = 0; i < 8; i++)
        {
            string k = tour.Seats[i];
            var at = cols[0][i];
            bool beaten = Out(k), you = k == Tournament.You, now = InCurrent(k);
            float d = now ? d0 * 1.08f : d0;
            var c = new Control { Position = at - new Vector2(d, d) / 2, Size = new Vector2(d, d) };
            AddChild(c);
            if (you) c.AddChild(Cosmetics.Framed(Settings.Avatar, Session.Profile.Frame, d));
            else
            {
                c.AddChild(new ColorRect { Size = c.Size, Color = now ? GoldC : InkFor(at) * new Color(1, 1, 1, 0.25f), Material = Kit.RoundedMat(1, 0.5f) });
                var p = Ink_(TPic(k)!, new Vector2(4, 4), c.Size - new Vector2(8, 8), c); p.Material = Kit.RoundedMat(1, 0.5f);
                if (beaten) p.Modulate = new Color(0.5f, 0.5f, 0.55f, 0.4f);
            }
            C(this, TName(k), Title, 14, beaten ? InkFor(at) * new Color(1, 1, 1, 0.35f) : now || you ? (OnLight(at) ? GoldDeep : GoldLight) : InkFor(at), new Vector2(at.X - 90, at.Y + d / 2 + 6), new Vector2(180, 20), HorizontalAlignment.Center);
        }
        for (int r = 1; r < 3; r++)
            for (int i = 0; i < cols[r].Count; i++)
            {
                var who = tour.Winners[r - 1][i];
                var at = cols[r][i];
                if (who == null) { Pip(at, 8); continue; }
                var c = new Control { Position = at - new Vector2(28, 28), Size = new Vector2(56, 56) };
                AddChild(c);
                if (who == Tournament.You) c.AddChild(Cosmetics.Framed(Settings.Avatar, Session.Profile.Frame, 56));
                else { var p = Ink_(TPic(who)!, Vector2.Zero, new Vector2(56, 56), c); p.Material = Kit.RoundedMat(1, 0.5f); if (Out(who)) p.Modulate = new Color(0.5f, 0.5f, 0.55f, 0.45f); }
            }
        // the current match, big, in the middle
        if (tour.Foe is { } foe)
        {
            var you = Cosmetics.Framed(Settings.Avatar, Session.Profile.Frame, 150);
            you.Position = new Vector2(W / 2 - 260, champ.Y - 75); AddChild(you);
            var r = RivalPanels.Of(foe);
            var fp = Ink_(RivalPanels.Art(r), new Vector2(W / 2 + 110, champ.Y - 75), new Vector2(150, 150));
            fp.Material = Kit.RoundedMat(1, 0.5f);
            var vs = C(this, "VS", Title, 54, Colors.White, new Vector2(W / 2 - 100, champ.Y - 36), new Vector2(200, 70), HorizontalAlignment.Center);
            vs.Material = TwoTone();
            C(this, Settings.PlayerName.ToUpperInvariant(), Title, 18, Ink, new Vector2(W / 2 - 335, champ.Y + 84), new Vector2(300, 24), HorizontalAlignment.Center);
            C(this, foe.Name, Title, 18, Silver, new Vector2(W / 2 + 35, champ.Y + 84), new Vector2(300, 24), HorizontalAlignment.Center);
            focusables.Add(Button("DUEL", new Vector2(W / 2, champ.Y + 160), 28));
        }
        T(this, "CHAMPION", Title, 20, new Vector2(W / 2 - 200, 170), new Vector2(400, 28), HorizontalAlignment.Center).Mat().P("lit", 1f);
        Pip(new Vector2(W / 2, 214), 20);
        Prizes(new Vector2(W / 2, H - 130), 1300, false);
    }

    // c ---------------------------------------------------------------- medallions, trophy and prizes in the middle
    void TourMedals()
    {
        Rift(0.5f, 0.5f);
        TourHeader();
        float d = 84;
        var (cols, champ) = Mirror(90 + d / 2, 230, 170, H * 0.6f, H * 0.36f);
        BracketLines(cols, champ, d / 2 + 6);
        void Medal(Vector2 at, string? key, float size)
        {
            if (key == null) { Pip(at, 9); return; }
            bool beaten = Out(key), you = key == Tournament.You, now = InCurrent(key);
            var root = new Control { Position = at - new Vector2(size, size) / 2, Size = new Vector2(size, size), PivotOffset = new Vector2(size, size) / 2 };
            AddChild(root);
            if (you) root.AddChild(Cosmetics.Framed(Settings.Avatar, Session.Profile.Frame, size));
            else
            {
                root.AddChild(new ColorRect { Size = root.Size, Color = now ? GoldC : (OnLight(at) ? Ink : Silver) * new Color(1, 1, 1, 0.25f), Material = Kit.RoundedMat(1, 0.5f) });
                var p = Ink_(TPic(key)!, new Vector2(4, 4), root.Size - new Vector2(8, 8), root); p.Material = Kit.RoundedMat(1, 0.5f);
                if (beaten) p.Modulate = new Color(0.5f, 0.5f, 0.55f, 0.45f);
            }
            if (beaten) AddChild(new Line2D { Points = new[] { at + new Vector2(-size, size) * 0.35f, at + new Vector2(size, -size) * 0.35f }, Width = 3, DefaultColor = new Color(0.8f, 0.25f, 0.25f, 0.8f) });
            if (now && !you)
            {
                var tw = CreateTween().SetLoops();
                tw.TweenProperty(root, "scale", new Vector2(1.08f, 1.08f), 0.6); tw.TweenProperty(root, "scale", Vector2.One, 0.6);
            }
            bool left = at.X < W / 2;
            var col = beaten ? InkFor(at) * new Color(1, 1, 1, 0.35f) : now || you ? (OnLight(at) ? GoldDeep : GoldLight) : InkFor(at);
            C(this, TName(key), Title, 15, col, new Vector2(at.X - 100, at.Y + size / 2 + 6), new Vector2(200, 20), HorizontalAlignment.Center);
        }
        for (int i = 0; i < 8; i++) Medal(cols[0][i], tour.Seats[i], d);
        for (int r = 1; r < 3; r++) for (int i = 0; i < cols[r].Count; i++) Medal(cols[r][i], tour.Winners[r - 1][i], d - 10 + r * 8);
        // the trophy: a big gold diamond with rays on the rift
        var rays = new ColorRect { Position = champ - new Vector2(160, 160), Size = new Vector2(320, 320), MouseFilter = MouseFilterEnum.Ignore,
            Material = new ShaderMaterial { Shader = new Shader { Code = "shader_type canvas_item; void fragment(){ vec2 p = UV - 0.5; float a = atan(p.y, p.x); float r = length(p) * 2.0; float k = pow(abs(sin(a * 8.0 + TIME * 0.3)), 16.0) * smoothstep(1.0, 0.2, r); COLOR = vec4(1.0, 0.82, 0.45, (k * 0.35 + exp(-r * r * 6.0) * 0.4)); }" } } };
        AddChild(rays);
        Pip(champ, 46);
        T(this, tour.Champion is { } ch ? TName(ch) : "CHAMPION", Title, 24, new Vector2(W / 2 - 250, champ.Y + 50), new Vector2(500, 32), HorizontalAlignment.Center).Mat().P("lit", 1f);
        Prizes(new Vector2(W / 2, H - 170), 1300, true);
        if (tour.Foe != null) focusables.Add(Button("DUEL", new Vector2(W / 2, champ.Y + 140), 28));
    }
}
