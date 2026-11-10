using Godot;
using PoC.Engine;

namespace PoC.Game.UI;

/// Mockups for the Online screen in the ink style (not wired). Online = room codes through a relay (PLAN_ONLINE.md);
/// LAN stays as the second tab. `-- --demo --variant=a|b|c [--state=find|vs]`
/// a = code card: your profile left, CREATE ROOM shows a big code, JOIN box + recent opponents right.
/// b = two splats: HOST and JOIN as two cream ink splats, deck strip and relay status under them.
/// c = rooms + quick match: open rooms list (friends' public rooms), a big QUICK MATCH brush, code box.
/// state vs = connected: both profiles facing, decks, READY (same for every variant, for reference).
public partial class OnlineSamples : MenuBase
{
    float W, H, top;
    static readonly Color Dim = Ink.Cream * new Color(1, 1, 1, 0.5f);
    static readonly (string Name, int Pic, string List, int Ping)[] Rooms =
        { ("MARIK", 5, "2004", 38), ("TEA", 4, "2004", 61), ("BAKURA", 6, "2003", 92), ("DUKE", 2, "2004", 120), ("REBECCA", 7, "2004", 44) };

    protected override string? MusicPath => Md("menu_02");
    protected override int ItemCount => 1;
    protected override void OnFocus(int old, int now) { }
    protected override void OnChoose(int i) { }
    protected override void OnBack() => GetTree().Quit();

    protected override void Build()
    {
        Demo.Ensure();
        W = GetViewportRect().Size.X; H = GetViewportRect().Size.Y;
        Ink.Sheet(this);
        var (after, bottom) = Ink.Header(this, "DUEL ONLINE", OnBack, logoW: 320);
        // ONLINE / LAN tabs beside the title.
        float tx = after + 40;
        Action<bool> online = _ => { }, lan = _ => { };
        tx += Ink.TabWord(this, "ONLINE", new Vector2(tx, bottom / 2 - 34), () => { }, out online) + 20;
        Ink.TabWord(this, "LAN", new Vector2(tx, bottom / 2 - 34), () => { }, out lan);
        online(true);
        top = bottom + 40;
        if (Demo.Arg("state") == "vs") { Versus(); return; }
        switch (Demo.Arg("variant", "a"))
        {
            case "b": Splats(); break;
            case "c": RoomList(); break;
            default: CodeCard(); break;
        }
    }

    // ---------------------------------------------------------------- shared

    static Texture2D? PicOf(int i) => Settings.PictureCard(i) is { } c ? CardArt.Picture(c) : null;

    Control Profile(Vector2 centre, float size, string name, Texture2D? pic, string frame, Color nameC)
    {
        var f = Cosmetics.Framed(pic, frame, size);
        f.Position = centre - new Vector2(size, size) / 2;
        AddChild(f);
        Kit.Put(this, name, Ink.Display, 40, nameC, centre + new Vector2(-260, size / 2 + 6), new Vector2(520, 50), HorizontalAlignment.Center);
        return f;
    }

    void Splat(Vector2 centre, Vector2 size, int seed, Color c, double delay = 0.15)
    {
        var sp = Ink.Pic(GD.Load<Texture2D>("res://assets/title/ink_splash.png"), centre - size / 2, size, this);
        sp.StretchMode = TextureRect.StretchModeEnum.Scale;
        var m = Ink.BleedMat(c, 0f, seed);
        sp.Material = m;
        GetTree().CreateTimer(delay).Timeout += () => Ink.To(this, m, "progress", 1.1f, 0.7);
    }

    /// Code entry: an ink line with the typed code, brush underline.
    LineEdit CodeBox(Vector2 at, float w, string placeholder)
    {
        var e = new LineEdit { Position = at, Size = new Vector2(w, 64), PlaceholderText = placeholder, Flat = true, MaxLength = 12, Alignment = HorizontalAlignment.Center };
        e.AddThemeFontOverride("font", Ink.Display); e.AddThemeFontSizeOverride("font_size", 38);
        e.AddThemeColorOverride("font_color", Ink.Cream);
        e.AddThemeColorOverride("font_placeholder_color", Ink.Cream * new Color(1, 1, 1, 0.25f));
        AddChild(e);
        Ink.Brush(at + new Vector2(-10, 60), new Vector2(w + 20, 12), Dim, this);
        return e;
    }

    /// The room code, big, on a cream brush (ink letters).
    void CodeStamp(Vector2 centre, string code, float w)
    {
        Ink.Brush(centre - new Vector2(w / 2, 52), new Vector2(w, 104), Ink.Cream, this);
        Kit.Put(this, code, Ink.Display, 56, Ink.Black, centre - new Vector2(w / 2, 36), new Vector2(w, 70), HorizontalAlignment.Center);
    }

    void Status(string text, Vector2 at, Color dot)
    {
        AddChild(new ColorRect { Position = at + new Vector2(0, 11), Size = new Vector2(12, 12), Color = dot, Material = Kit.RoundedMat(1, 0.5f) });
        Kit.Put(this, text, Kit.Ui, 20, Dim, at + new Vector2(22, 0));
    }

    string DeckName => (Session.Playing ?? Session.Decks.FirstOrDefault())?.Name.ToUpperInvariant() ?? "NO DECK";

    // ---------------------------------------------------------------- a: code card

    void CodeCard()
    {
        float colW = W / 3;
        // You.
        Profile(new Vector2(colW / 2, top + 200), 240, Settings.PlayerName.ToUpperInvariant(), Settings.Avatar, Session.Profile.Frame, Ink.Cream);
        Kit.Put(this, "DECK", Kit.Cond, 22, Dim, new Vector2(60, top + 420), new Vector2(colW - 120, 30), HorizontalAlignment.Center);
        Kit.Put(this, DeckName + "  ›", Kit.Cond, 34, Ink.Cream, new Vector2(60, top + 450), new Vector2(colW - 120, 44), HorizontalAlignment.Center);
        Kit.Put(this, "2004 LIST", Kit.Cond, 22, Dim, new Vector2(60, top + 500), new Vector2(colW - 120, 30), HorizontalAlignment.Center);

        // Host: room code.
        float hx = colW + colW / 2;
        Kit.Put(this, "HOST", Kit.Cond, 36, Ink.Cream, new Vector2(colW, top), new Vector2(colW, 44), HorizontalAlignment.Center);
        CodeStamp(new Vector2(hx, top + 170), "RED-EYES 4821", colW - 80);
        Kit.Put(this, "Send this code to your friend.", Kit.Ui, 22, Dim, new Vector2(colW, top + 240), new Vector2(colW, 30), HorizontalAlignment.Center);
        AddChild(Chips.Make("COPY CODE", new Vector2(hx - 150, top + 300), new Vector2(300, 64), () => { }, 24));
        Status("Waiting for a duelist…", new Vector2(hx - 120, top + 400), new Color(1f, 0.75f, 0.3f));

        // Join.
        float jx = 2 * colW + 40, jw = colW - 100;
        Kit.Put(this, "JOIN", Kit.Cond, 36, Ink.Cream, new Vector2(2 * colW, top), new Vector2(colW, 44), HorizontalAlignment.Center);
        CodeBox(new Vector2(jx, top + 130), jw, "ENTER CODE");
        AddChild(Chips.Make("JOIN", new Vector2(jx + jw / 2 - 150, top + 230), new Vector2(300, 64), () => { }, 24));
        Kit.Put(this, "RECENT", Kit.Cond, 22, Dim, new Vector2(jx, top + 350));
        for (int i = 0; i < 3; i++)
        {
            var r = Rooms[i];
            var f = Cosmetics.Framed(PicOf(r.Pic), Shop.DefaultFrame, 64);
            f.Position = new Vector2(jx, top + 390 + i * 80);
            AddChild(f);
            Kit.Put(this, r.Name, Kit.Cond, 30, Ink.Cream, new Vector2(jx + 80, top + 398 + i * 80));
            Kit.Put(this, i == 0 ? "ONLINE · REMATCH" : "OFFLINE", Kit.Ui, 18, i == 0 ? new Color(0.5f, 0.9f, 0.55f) : Dim, new Vector2(jx + 80, top + 432 + i * 80));
        }
        Ink.Brush(new Vector2(colW - 4, top + 40), new Vector2(8, H - top - 200), Ink.Cream * new Color(1, 1, 1, 0.12f), this);
        Ink.Brush(new Vector2(2 * colW - 4, top + 40), new Vector2(8, H - top - 200), Ink.Cream * new Color(1, 1, 1, 0.12f), this);
        Status("Relay connected · 38 ms", new Vector2(60, H - 90), new Color(0.5f, 0.9f, 0.55f));
    }

    // ---------------------------------------------------------------- b: two splats

    void Splats()
    {
        float y = top + (H - top - 260) / 2;
        var l = new Vector2(W * 0.3f, y); var r = new Vector2(W * 0.7f, y);
        Splat(l, new Vector2(620, 620), 3, Ink.Cream);
        Splat(r, new Vector2(620, 620), 8, Ink.Cream * new Color(1, 1, 1, 0.16f), 0.3);
        Kit.Put(this, "HOST", Ink.Display, 80, Ink.Black, l - new Vector2(300, 150), new Vector2(600, 100), HorizontalAlignment.Center);
        Kit.Put(this, "your code", Kit.Cond, 26, Ink.Black * new Color(1, 1, 1, 0.6f), l - new Vector2(300, 40), new Vector2(600, 36), HorizontalAlignment.Center);
        Kit.Put(this, "KURIBOH 0715", Ink.Display, 36, Ink.Black, l - new Vector2(300, -8), new Vector2(600, 56), HorizontalAlignment.Center);
        Kit.Put(this, "click to copy", Kit.Ui, 18, Ink.Black * new Color(1, 1, 1, 0.5f), l + new Vector2(-300, 74), new Vector2(600, 26), HorizontalAlignment.Center);

        Kit.Put(this, "JOIN", Ink.Display, 80, Ink.Cream, r - new Vector2(300, 150), new Vector2(600, 100), HorizontalAlignment.Center);
        CodeBox(r - new Vector2(200, 20), 400, "ENTER CODE");
        Kit.Put(this, "or", Kit.Cond, 34, Dim, new Vector2(W / 2 - 40, y - 24), new Vector2(80, 44), HorizontalAlignment.Center);

        // Deck strip + status.
        float by = H - 210;
        Ink.Brush(new Vector2(-40, by - 10), new Vector2(W + 80, 130), Ink.Cream * new Color(1, 1, 1, 0.07f), this);
        var f = Cosmetics.Framed(Settings.Avatar, Session.Profile.Frame, 96);
        f.Position = new Vector2(70, by + 6); AddChild(f);
        Kit.Put(this, Settings.PlayerName.ToUpperInvariant(), Kit.Cond, 34, Ink.Cream, new Vector2(184, by + 14));
        Kit.Put(this, "DECK  " + DeckName + "   ·   2004 LIST", Kit.Cond, 24, Dim, new Vector2(184, by + 58));
        Status("Relay connected · 38 ms", new Vector2(W - 420, by + 40), new Color(0.5f, 0.9f, 0.55f));
    }

    // ---------------------------------------------------------------- c: rooms + quick match

    void RoomList()
    {
        float lw = W * 0.55f, x = 70;
        Kit.Put(this, "OPEN ROOMS", Kit.Cond, 30, Ink.Cream, new Vector2(x, top));
        Kit.Put(this, "FORMAT", Kit.Cond, 20, Dim, new Vector2(x + lw - 330, top + 8));
        Kit.Put(this, "PING", Kit.Cond, 20, Dim, new Vector2(x + lw - 190, top + 8));
        for (int i = 0; i < Rooms.Length; i++)
        {
            var r = Rooms[i];
            float y = top + 60 + i * 104;
            if (i == 0) Ink.Brush(new Vector2(x - 30, y - 6), new Vector2(lw + 40, 96), Ink.Cream * new Color(1, 1, 1, 0.09f), this);
            var f = Cosmetics.Framed(PicOf(r.Pic), Shop.DefaultFrame, 84);
            f.Position = new Vector2(x, y); AddChild(f);
            Kit.Put(this, r.Name, Kit.Cond, 36, Ink.Cream, new Vector2(x + 104, y + 10));
            Kit.Put(this, $"{r.Name.ToLowerInvariant()}'s room", Kit.Ui, 18, Dim, new Vector2(x + 106, y + 52));
            Kit.Put(this, r.List, Kit.Cond, 28, Ink.Cream, new Vector2(x + lw - 330, y + 22));
            Kit.Put(this, $"{r.Ping} ms", Kit.Cond, 28, r.Ping < 80 ? new Color(0.5f, 0.9f, 0.55f) : new Color(1f, 0.75f, 0.3f), new Vector2(x + lw - 190, y + 22));
        }
        float rx = W * 0.66f, rw = W - rx - 70;
        // Quick match: a big cream brush with ink letters.
        Ink.Brush(new Vector2(rx - 20, top), new Vector2(rw + 40, 150), Ink.Cream, this);
        Kit.Put(this, "QUICK MATCH", Ink.Display, 54, Ink.Black, new Vector2(rx, top + 34), new Vector2(rw, 70), HorizontalAlignment.Center);
        Kit.Put(this, "HOST A ROOM", Kit.Cond, 30, Ink.Cream, new Vector2(rx, top + 200), new Vector2(rw, 40), HorizontalAlignment.Center);
        Ink.Brush(new Vector2(rx + rw / 2 - 110, top + 240), new Vector2(220, 10), Dim, this);
        Kit.Put(this, "JOIN WITH A CODE", Kit.Cond, 24, Dim, new Vector2(rx, top + 310), new Vector2(rw, 30), HorizontalAlignment.Center);
        CodeBox(new Vector2(rx + 40, top + 350), rw - 80, "CODE");
        var f2 = Cosmetics.Framed(Settings.Avatar, Session.Profile.Frame, 120);
        f2.Position = new Vector2(rx + 10, H - 300); AddChild(f2);
        Kit.Put(this, Settings.PlayerName.ToUpperInvariant(), Kit.Cond, 36, Ink.Cream, new Vector2(rx + 150, H - 280));
        Kit.Put(this, "DECK  " + DeckName, Kit.Cond, 24, Dim, new Vector2(rx + 150, H - 234));
        Status("Relay connected · 5 rooms open", new Vector2(70, H - 90), new Color(0.5f, 0.9f, 0.55f));
    }

    // ---------------------------------------------------------------- connected

    void Versus()
    {
        float y = top + 220;
        var me = new Vector2(W * 0.27f, y); var them = new Vector2(W * 0.73f, y);
        Splat(me, new Vector2(520, 520), 2, Ink.Cream * new Color(1, 1, 1, 0.14f));
        Splat(them, new Vector2(520, 520), 7, Ink.Cream * new Color(1, 1, 1, 0.14f), 0.3);
        Profile(me, 300, Settings.PlayerName.ToUpperInvariant(), Settings.Avatar, Session.Profile.Frame, Ink.Cream);
        Profile(them, 300, "MARIK", PicOf(5), Shop.DefaultFrame, Ink.Cream);
        Kit.Put(this, "VS", Ink.Display, 120, Ink.Cream, new Vector2(W / 2 - 150, y - 80), new Vector2(300, 150), HorizontalAlignment.Center);
        foreach (var (c, deck, ready) in new[] { (me, DeckName, true), (them, "LAVA SURGE", false) })
        {
            Kit.Put(this, "DECK  " + deck, Kit.Cond, 28, Dim, c + new Vector2(-260, 240), new Vector2(520, 40), HorizontalAlignment.Center);
            if (ready)
            {
                Ink.Brush(c + new Vector2(-130, 300), new Vector2(260, 76), Ink.Cream, this);
                Kit.Put(this, "READY", Kit.Cond, 40, Ink.Black, c + new Vector2(-130, 312), new Vector2(260, 50), HorizontalAlignment.Center);
            }
            else Kit.Put(this, "choosing a deck…", Kit.Ui, 24, Dim, c + new Vector2(-260, 318), new Vector2(520, 36), HorizontalAlignment.Center);
        }
        Status("Room RED-EYES 4821 · 38 ms · 2004 list · Single duel", new Vector2(70, H - 90), new Color(0.5f, 0.9f, 0.55f));
        AddChild(Chips.Make("CHANGE DECK", new Vector2(W - 420, H - 130), new Vector2(340, 80), () => { }, 28));
    }
}
