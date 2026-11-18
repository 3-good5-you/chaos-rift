using Godot;
using PoC.Engine;

namespace PoC.Game.UI;

/// Row 2: the tournament as a left-to-right bracket (the user's reference: eight names on the left, lines join to the
/// trophy on the right) with YOUR ROAD on the dark side (round 1 pick B, refined). The round names sit over their
/// columns; your plate is gold with a YOU tag inside it, your next rival's plate says NEXT. The state link bottom
/// left cycles new / semi / final / won / out.
public partial class MenuLab
{
    static int tourState = 1;
    static readonly string[] TourStates = { "QUARTERFINAL", "SEMIFINAL", "FINAL", "CHAMPION", "KNOCKED OUT" };
    static readonly Dictionary<int, Tournament> tours = new();
    Tournament tour = null!;

    static Tournament MakeTour(int state)
    {
        if (tours.TryGetValue(state, out var done)) return done;
        var t = new Tournament(new Rng(7));
        for (int r = 0; r < Math.Min(state, 3); r++) { t.PlayOthers(); t.Record(true); }
        if (state == 4) { t.PlayOthers(); t.Record(false); }
        return tours[state] = t;
    }

    bool Lost(string k)
    {
        for (int r = 0; r < 3; r++)
            for (int m = 0; m < tour.Winners[r].Length; m++)
            {
                var (a, b) = tour.Pair(r, m);
                if ((a == k || b == k) && tour.Winners[r][m] is { } w && w != k) return true;
            }
        return false;
    }

    /// The furthest column a duelist reached (0 = first round).
    int Depth(string k)
    {
        int d = 0;
        for (int r = 0; r < 3; r++) if (tour.Winners[r].Contains(k)) d = r + 1;
        return d;
    }

    string Status => tour.Champion is { } c ? (c == Tournament.You ? "YOU ARE THE CHAMPION" : Lang.F("{0} WINS", NameOf(c))) : tour.YouOut ? "KNOCKED OUT" : Tournament.RoundNames[tour.Round];

    void Tour(int v)
    {
        tour = MakeTour(tourState);
        Session.Tour = tour;
        Rift(0.67f, 0.61f);
        Head("TOURNAMENT");
        var st = Put(this, Lang.Tr("LAB STATE:") + " " + TourStates[tourState] + "  ›", Thin, 13, Graphite, new Vector2(84, H - 44));
        Click(st, () => { tourState = (tourState + 1) % TourStates.Length; Play(lastVar); });
        Bracket(new Rect2(84, 150, CrackX(H) - 84 - 60, H - 150 - 70), false);
        if (v == 0) Road(new Rect2(CrackX(0) + 70, 70, W - CrackX(0) - 70 - 60, H - 130));
        else NextMatch(new Rect2(CrackX(H * 0.5f) + 40, 70, W - CrackX(H * 0.5f) - 100, H - 130));
    }

    /// The bracket in `area`: round names on top, eight seats on the left, winners further right, the cup last.
    /// `rings` = round pictures with the name beside them instead of name plates.
    void Bracket(Rect2 area, bool rings)
    {
        float top = area.Position.Y + 60, sp = (area.End.Y - top) / 8f, plateH = Math.Min(62, sp - 16);
        float gap = 70, w0 = 230, w1 = 215, w2 = 200;
        float[] colX = { area.Position.X, area.Position.X + w0 + gap, area.Position.X + w0 + w1 + 2 * gap };
        float[] colW = { w0, w1, w2 };
        float cupX = colX[2] + w2 + gap + 60;
        var ys = new List<float[]> { Enumerable.Range(0, 8).Select(i => top + (i + 0.5f) * sp).ToArray() };
        for (int r = 1; r < 4; r++) ys.Add(Enumerable.Range(0, 8 >> r).Select(m => (ys[r - 1][2 * m] + ys[r - 1][2 * m + 1]) / 2).ToArray());

        // round names over their columns: done ones ticked, the current one gold with a diamond under it
        int now = tour.Champion != null ? 3 : tour.Round;
        string[] heads = { "QUARTERFINAL", "SEMIFINAL", "FINAL", "CHAMPION" };
        for (int r = 0; r < 4; r++)
        {
            float cx = r == 3 ? cupX : colX[r] + colW[r] / 2;
            var col = r == now ? GoldDeep : r < now ? Graphite : Graphite * new Color(1, 1, 1, 0.45f);
            Mid(this, (r < now ? "✓  " : "") + heads[r], Title, r == now ? 16 : 14, col, new Rect2(cx - 120, area.Position.Y, 240, 26));
            if (r == now) Pip(this, new Vector2(cx, area.Position.Y + 36), 8);
        }

        // lines under the plates; your road gold, the live match pulsing
        for (int r = 0; r < 3; r++)
            for (int m = 0; m < (8 >> (r + 1)); m++)
            {
                var (pa, pb) = tour.Pair(r, m);
                var win = tour.Winners[r][m];
                bool live = r == tour.Round && tour.Champion == null && !tour.YouOut && (pa == Tournament.You || pb == Tournament.You);
                for (int k = 0; k < 2; k++)
                {
                    var who = k == 0 ? pa : pb;
                    var a = new Vector2(colX[r] + colW[r], ys[r][2 * m + k]);
                    var b = new Vector2(r == 2 ? cupX - 58 : colX[r + 1], ys[r + 1][m]);
                    float mx = a.X + gap / 2;
                    bool mine = who == Tournament.You && (win == Tournament.You || live);
                    bool lit = win != null && win == who;
                    var col = mine ? GoldC : lit ? Ink * new Color(1, 1, 1, 0.6f) : Ink * new Color(1, 1, 1, 0.16f);
                    var line = new Line2D { Points = new[] { a, new Vector2(mx, a.Y), new Vector2(mx, b.Y), b }, Width = mine ? 3.5f : lit ? 2 : 1.5f, DefaultColor = col, JointMode = Line2D.LineJointMode.Round };
                    AddChild(line);
                    if (live && who == Tournament.You) { var tw = line.CreateTween().SetLoops(); tw.TweenProperty(line, "modulate:a", 0.35f, 0.7); tw.TweenProperty(line, "modulate:a", 1f, 0.7); }
                }
            }

        void Seat(int r, int i, string? key)
        {
            float w = colW[r], h = r == 0 ? plateH : plateH * 0.9f, y = ys[r][i];
            var rect = new Rect2(colX[r], y - h / 2, w, h);
            if (key == null)
            {
                Plate(this, rect, Box(new Color(1, 1, 1, 0.3f), Ink * new Color(1, 1, 1, 0.14f), 1.5f, 99));
                Mid(this, "TO BE DECIDED", Thin, 11, Graphite * new Color(1, 1, 1, 0.45f), rect);
                return;
            }
            bool you = key == Tournament.You, beaten = Lost(key) && r == Depth(key), next = tour.Foe?.Key == key && r == tour.Round;
            if (!rings)
            {
                var s = you ? Box(new Color(1f, 0.96f, 0.86f), GoldC, 2.5f, 99, 14, GoldC with { A = 0.3f }, new Vector2(0, 3))
                    : next ? Box(Colors.White, Ink * new Color(1, 1, 1, 0.8f), 2, 99, 10, new Color(0, 0, 0, 0.15f), new Vector2(0, 3))
                    : Box(new Color(1, 1, 1, beaten ? 0.35f : 0.9f), new Color(0, 0, 0, 0.08f), 1, 99, beaten ? 0 : 6, new Color(0, 0, 0, 0.1f), new Vector2(0, 2));
                Plate(this, rect, s);
            }
            float d = rings ? h + 6 : h - 8;
            Face(this, new Vector2(rect.Position.X + h / 2, y), d, key, you || next, beaten ? 0.45f : 1);
            float tagW = 0;
            var nameBox = new Rect2(rect.Position.X + h + 6, y - 13, w - h - 14 - tagW, 26);
            var name = Put(this, NameOf(key), Title, r == 0 ? 15 : 14, you ? GoldDeep : Ink * new Color(1, 1, 1, beaten ? 0.4f : 1), nameBox.Position + new Vector2(0, 2), nameBox.Size);
            name.VerticalAlignment = VerticalAlignment.Center; name.ClipText = true;
            if (beaten) Hair(this, new Vector2(nameBox.Position.X, y + 1), Math.Min(nameBox.Size.X, Title.GetStringSize(NameOf(key), HorizontalAlignment.Left, -1, Z(r == 0 ? 15 : 14)).X), Ink * new Color(1, 1, 1, 0.4f), 1.5f);
            // small tags hanging on the plate's top edge, clear of the name
            if (you && r == Depth(key) && !tour.YouOut && tour.Champion == null) Tag(this, "YOU", new Vector2(rect.End.X - 34, rect.Position.Y - 1), GoldC, Ink, 10);
            if (next) Tag(this, "NEXT", new Vector2(rect.End.X - 38, rect.Position.Y - 1), Ink, GoldLight, 10);
        }
        for (int i = 0; i < 8; i++) Seat(0, i, tour.Seats[i]);
        for (int r = 1; r < 3; r++) for (int m = 0; m < (8 >> r); m++) Seat(r, m, tour.Winners[r - 1][m]);

        // the cup, with the champion's face on it once decided
        var cc = new Vector2(cupX, ys[3][0]);
        if (tour.Champion is { } ch) { Face(this, cc, 110, ch, true); Mid(this, NameOf(ch), Title, 15, GoldDeep, new Rect2(cc + new Vector2(-110, 62), new Vector2(220, 24))); }
        else Cup(this, cc, 118);
        Mid(this, Lang.F("{0:N0} DP", Tournament.Ladder[0].Dp), Title, 15, GoldDeep, new Rect2(cc + new Vector2(-110, 90), new Vector2(220, 24)));
    }


    /// YOUR ROAD on the dark side, kept plain (round 3: the cards were too busy): one line of round pictures, the round
    /// name and one short line beside each; DUEL sits under the current one; only the champion step names a prize.
    void Road(Rect2 r)
    {
        float x = r.Position.X;
        Put(this, "YOUR ROAD", Thin, 15, Mist, new Vector2(x, r.Position.Y));
        Put(this, Status, Title, 28, GoldLight, new Vector2(x, r.Position.Y + 24));
        float y0 = r.Position.Y + 150, step = (r.End.Y - 60 - y0) / 3, fx = x + 40;
        int youDepth = Depth(Tournament.You);
        for (int i = 0; i < 3; i++)
        {
            bool lit = i < (tour.Champion == Tournament.You ? 3 : tour.Round) && !(tour.YouOut && i >= youDepth);
            AddChild(new Line2D { Points = new[] { new Vector2(fx, y0 + i * step + 44), new Vector2(fx, y0 + (i + 1) * step - 44) }, Width = lit ? 3 : 2, DefaultColor = lit ? GoldC : Silver * new Color(1, 1, 1, 0.18f) });
        }
        for (int i = 0; i < 4; i++)
        {
            var c = new Vector2(fx, y0 + i * step);
            string? foe = null;
            if (i < 3) for (int m = 0; m < tour.Winners[i].Length; m++) { var (a, b) = tour.Pair(i, m); if (a == Tournament.You) foe = b; if (b == Tournament.You) foe = a; }
            bool done = i < 3 && tour.Winners[i].Contains(Tournament.You), lost = i < 3 && tour.YouOut && i == youDepth,
                here = i < 3 && i == tour.Round && !tour.YouOut && tour.Champion == null, champ = i == 3 && tour.Champion == Tournament.You;
            float d = here ? 84 : 64;
            if (i == 3) Cup(this, c, 74);
            else if (foe != null) Face(this, c, d, foe, here, done || here || lost ? 1 : 0.5f);
            else { Plate(this, new Rect2(c - new Vector2(d, d) / 2, new Vector2(d, d)), Box(new Color(1, 1, 1, 0.03f), Silver * new Color(1, 1, 1, 0.22f), 1.5f, 99)); Mid(this, "?", Title, 22, Mist, new Rect2(c - new Vector2(d, d) / 2, new Vector2(d, d))); }
            float tx = fx + 66;
            string head = i == 3 ? "CHAMPION" : Tournament.RoundNames[i];
            Put(this, head, Title, here ? 19 : 16, here || champ ? GoldLight : Silver * new Color(1, 1, 1, done || lost ? 0.85f : 0.45f), new Vector2(tx, c.Y - 30));
            string sub = i == 3 ? Lang.F("{0:N0} DP  +  {1} cards", Tournament.Ladder[0].Dp, Tournament.Ladder[0].Cards)
                : done ? Lang.F("Beat {0}", NameOf(foe!)) : lost ? Lang.F("Lost to {0}", NameOf(foe!)) : foe != null ? Lang.F("vs {0}", NameOf(foe)) : "Rival not decided yet";
            Put(this, sub, Body, 18, i == 3 ? GoldLight : done ? GoldLight : lost ? new Color(1f, 0.5f, 0.5f) : here ? Silver : Mist * new Color(1, 1, 1, 0.7f), new Vector2(tx, c.Y + 2));
            if (here) Btn(this, "DUEL", new Vector2(tx + 90, c.Y + 76), () => Flash("DUEL"), primary: true, size: 18, width: 180);
        }
        if (tour.Foe == null) Btn(this, "NEW TOURNAMENT", new Vector2(r.GetCenter().X, r.End.Y), () => { tourState = 0; Play(lastVar); }, primary: true, size: 18);
    }

    /// B: only the next match on the dark side: you VS them, their difficulty, DUEL, and one line on what winning is worth.
    void NextMatch(Rect2 r)
    {
        float cx = r.GetCenter().X, y = r.Position.Y + 60;
        Mid(this, tour.Foe != null ? "NEXT MATCH" : "RESULT", Thin, 15, Mist, new Rect2(r.Position.X, y, r.Size.X, 22));
        Mid(this, Status, Title, 32, GoldLight, new Rect2(r.Position.X, y + 28, r.Size.X, 44));
        y += 130;
        if (tour.Foe is { } foe)
        {
            var yb = UI.Cosmetics.Framed(Avatar, P.Frame, 140); yb.Position = new Vector2(cx - 200, y); AddChild(yb);
            Face(this, new Vector2(cx + 130, y + 70), 140, foe.Key, true);
            Mid(this, "VS", Title, 32, Silver, new Rect2(cx - 60, y + 40, 120, 60));
            Fit(this, Settings.PlayerName.ToUpperInvariant(), Title, 16, 11, Silver, new Rect2(cx - 230, y + 150, 200, 24));
            Fit(this, foe.Name, Title, 16, 11, Silver, new Rect2(cx + 30, y + 150, 200, 24));
            Mid(this, new OpponentPick(foe.Key, foe.Deck, foe.Level).Tier, Title, 13, Session.TierColor(foe.Level), new Rect2(cx + 30, y + 178, 200, 20));
            Btn(this, "DUEL", new Vector2(cx, r.End.Y - 20), () => Flash("DUEL"), primary: true, size: 26, width: 280);
            int next = Math.Min(2, tour.Round + 1);
            string win = tour.Round == 2 ? Lang.F("Win: champion  ·  {0:N0} DP", Tournament.Ladder[0].Dp) : Lang.F("Win: on to the {0}  ·  {1:N0} DP sure", Tournament.RoundNames[next], Tournament.Ladder[2 - tour.Round].Dp);
            Mid(this, win, Body, 18, Mist, new Rect2(r.Position.X, y + 230, r.Size.X, 26));
        }
        else Btn(this, "NEW TOURNAMENT", new Vector2(cx, y + 120), () => { tourState = 0; Play(lastVar); }, primary: true, size: 20);
    }
}
