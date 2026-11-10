using Godot;
using PoC.Engine;

namespace PoC.Game.UI;

/// Tournament (Chaos Rift sample B, locked 2026-11-08): a mirrored 8-seat bracket, four seats in the light, four in
/// the dark, every duelist a round picture; the current match large in the middle (you VS them) with DUEL; the prize
/// ladder (by finishing place, Tournament.Ladder) along the bottom. `-- --demo --state=new|mid|won|out`.
public partial class TournamentScreen : RiftMenu
{
    Tournament tour = null!;

    protected override string? MusicPath => Md("solo_gate");
    protected override void OnBack() => Transition.Go(this, "res://ui/MainMenu.tscn");

    protected override void Build()
    {
        Demo.Ensure();
        Stage();
        Session.Tour ??= new Tournament(new Rng((ulong)Session.Rng.Int(1 << 30)));
        tour = Session.Tour;
        Rift(0.5f, 0.5f);
        Back();
        Put(this, "TOURNAMENT", Title, 40, Ink, new Vector2(80, 58));
        Hair(this, new Vector2(82, 108), 160, GoldC);
        string status = tour.Champion is { } c ? (c == Tournament.You ? "YOU ARE THE CHAMPION" : $"{Name(c)} WINS")
            : tour.YouOut ? "KNOCKED OUT" : Tournament.RoundNames[tour.Round];
        Put(this, status, Title, 22, GoldLight, new Vector2(W - 680, 66), new Vector2(600, 30), HorizontalAlignment.Right);
        if (tour.Foe != null) Put(this, $"NEXT: VS {tour.Foe.Name}", Thin, 17, Mist, new Vector2(W - 680, 102), new Vector2(600, 24), HorizontalAlignment.Right);

        const float d0 = 104;
        var (cols, champ) = Mirror(60 + d0 / 2, 210, 170, H * 0.62f, H * 0.42f);
        Lines(cols, champ, d0 / 2 + 8);
        for (int i = 0; i < 8; i++) Seat(cols[0][i], tour.Seats[i], InCurrent(tour.Seats[i]) ? d0 * 1.08f : d0, true);
        for (int r = 1; r < 3; r++)
            for (int i = 0; i < cols[r].Count; i++)
            {
                var who = tour.Winners[r - 1][i];
                if (who == null) Pip(this, cols[r][i], 8); else Seat(cols[r][i], who, 56, false);
            }

        // the current match, large, in the middle; or the champion
        if (tour.Foe is { } foe)
        {
            var you = UI.Cosmetics.Framed(Avatar, P.Frame, 150);
            you.Position = new Vector2(W / 2 - 260, champ.Y - 75); AddChild(you);
            Round(new Vector2(W / 2 + 185, champ.Y), 150, foe.Key, true);
            T(this, "VS", Title, 54, new Vector2(W / 2 - 100, champ.Y - 36), new Vector2(200, 70), HorizontalAlignment.Center);
            Put(this, Settings.PlayerName.ToUpperInvariant(), Title, 18, Ink, new Vector2(W / 2 - 335, champ.Y + 84), new Vector2(300, 24), HorizontalAlignment.Center);
            Put(this, foe.Name, Title, 18, Silver, new Vector2(W / 2 + 35, champ.Y + 84), new Vector2(300, 24), HorizontalAlignment.Center);
            Button(this, "DUEL", new Vector2(W / 2, champ.Y + 160), () => Transition.Go(this, "res://ui/DuelSetup.tscn"), 28);
        }
        else
        {
            if (tour.Champion is { } ch)
            {
                if (ch == Tournament.You) { var y = UI.Cosmetics.Framed(Avatar, P.Frame, 170); y.Position = champ - new Vector2(85, 85); AddChild(y); }
                else Round(champ, 170, ch, true);
                T(this, Name(ch), Title, 26, new Vector2(W / 2 - 300, champ.Y + 96), new Vector2(600, 34), HorizontalAlignment.Center).Mat().P("lit", 1f);
            }
            Button(this, "NEW TOURNAMENT", new Vector2(W / 2, champ.Y + 180), () => { Session.Tour = new Tournament(new Rng((ulong)Session.Rng.Int(1 << 30))); Transition.Go(this, "res://ui/Tournament.tscn"); }, 24);
        }
        T(this, "CHAMPION", Title, 20, new Vector2(W / 2 - 200, 170), new Vector2(400, 28), HorizontalAlignment.Center).Mat().P("lit", 1f);
        Pip(this, new Vector2(W / 2, 214), 20);
        Prizes(new Vector2(W / 2, H - 130));
    }

    // ---------------------------------------------------------------- pieces

    string Name(string k) => k == Tournament.You ? Settings.PlayerName.ToUpperInvariant() : Tournament.Get(k).Name;
    bool Lost(int r, int m, string who) => tour.Winners[r][m] is { } w && w != who;
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

    /// A round picture: gold ring for the current match, faded when out.
    void Round(Vector2 at, float d, string key, bool big)
    {
        var c = new Control { Position = at - new Vector2(d, d) / 2, Size = new Vector2(d, d), MouseFilter = MouseFilterEnum.Ignore };
        AddChild(c);
        if (key == Tournament.You) { c.AddChild(UI.Cosmetics.Framed(Avatar, P.Frame, d)); return; }
        bool now = InCurrent(key);
        c.AddChild(new ColorRect { Size = c.Size, Color = now || big ? GoldC : InkAt(at) * new Color(1, 1, 1, 0.25f), Material = Kit.RoundedMat(1, 0.5f), MouseFilter = MouseFilterEnum.Ignore });
        var p = Pic(c, RivalPanels.Art(RivalPanels.Of(Tournament.Get(key))), new Vector2(4, 4), c.Size - new Vector2(8, 8));
        p.Material = Kit.RoundedMat(1, 0.5f);
        if (!big && Out(key)) p.Modulate = new Color(0.5f, 0.5f, 0.55f, 0.4f);
    }

    void Seat(Vector2 at, string key, float d, bool named)
    {
        Round(at, d, key, false);
        if (!named) return;
        bool beaten = Out(key), you = key == Tournament.You, now = InCurrent(key);
        var col = beaten ? InkAt(at) * new Color(1, 1, 1, 0.35f) : now || you ? GoldAt(at) : InkAt(at);
        Put(this, Name(key), Title, 14, col, new Vector2(at.X - 90, at.Y + d / 2 + 6), new Vector2(180, 20), HorizontalAlignment.Center);
    }

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
                col.Add(new Vector2(right ? W - outerX - r * stepX : outerX + r * stepX, top + (j + 0.5f) * area / half));
            }
            cols.Add(col);
        }
        return (cols, new Vector2(W / 2, champY));
    }

    void Lines(List<List<Vector2>> cols, Vector2 champ, float inset)
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
                    var col = lit ? GoldC : current ? GoldC * new Color(1, 1, 1, 0.55f) : InkAt(a) * new Color(1, 1, 1, 0.25f);
                    var pts = r == 2 ? new[] { a, new Vector2(mx, a.Y), new Vector2(mx, to.Y) } : new[] { a, new Vector2(mx, a.Y), new Vector2(mx, to.Y), to - new Vector2(dir * inset, 0) };
                    AddChild(new Line2D { Points = pts, Width = lit ? 3.5f : current ? 2.5f : 1.5f, DefaultColor = col, JointMode = Line2D.LineJointMode.Round });
                }
            }
    }

    /// Four prize columns: place and DP. Champion in gold; your finishing place marked.
    void Prizes(Vector2 centre)
    {
        int place = tour.Place ?? -1;
        float colW = 230, x0 = centre.X - 2 * colW;
        for (int i = 0; i < 4; i++)
        {
            float cx = x0 + (i + 0.5f) * colW;
            bool lit = i == 0 || i == place;
            T(this, Tournament.PlaceNames[i], Title, 15, new Vector2(cx - 115, centre.Y - 32), new Vector2(230, 22), HorizontalAlignment.Center, 0.9f).Mat().P("lit", lit ? 1f : 0f);
            var dp = T(this, $"{Tournament.Ladder[i].Dp:N0} DP", Title, i == 0 ? 34 : 26, new Vector2(cx - 115, centre.Y - 6), new Vector2(230, 44), HorizontalAlignment.Center);
            if (lit) dp.Mat().P("lit", 1f);
            if (i == place) Pip(this, new Vector2(cx, centre.Y + 50), 9);
        }
    }

    /// Screenshots: `--demo --state=new|mid|won|out`.
    static void Stage()
    {
        if (!OS.GetCmdlineUserArgs().Contains("--demo")) return;
        var state = OS.GetCmdlineUserArgs().FirstOrDefault(a => a.StartsWith("--state="))?[8..] ?? "new";
        var t = new Tournament(new Rng(7));
        if (state is "mid" or "won") { t.PlayOthers(); t.Record(true); }
        if (state == "won") { t.PlayOthers(); t.Record(true); t.PlayOthers(); t.Record(true); }
        if (state == "out") { t.PlayOthers(); t.Record(false); }
        Session.Tour = t;
    }
}
