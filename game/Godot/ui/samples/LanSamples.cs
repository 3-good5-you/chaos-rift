using Godot;
using PoC.Engine;

namespace PoC.Game.UI;

/// Mockups for the LAN duel lobby (not wired). `-- --demo --variant=a|b|c --state=find|vs`
/// find = looking for a duelist (hosts heard on the network, join by IP, host), vs = connected, picking decks.
/// a = Versus podiums, b = Card halves (profile cards as big foil panels), c = Room list.
public partial class LanSamples : MenuBase
{
    float W, H, barY;
    protected override int ItemCount => 1;
    protected override void OnFocus(int old, int now) { }
    protected override void OnChoose(int i) { }

    static readonly Color Cool = new(0.62f, 0.8f, 1f);
    record Host(string Name, int Pic, string List, string Addr);
    static readonly Host[] Hosts =
    {
        new("MARIK", 5, "2004", "192.168.1.24"), new("TEA", 4, "2004", "192.168.1.31"), new("BAKURA", 6, "2003", "192.168.1.40"),
    };
    static Host Them => Hosts[0];
    bool vs;
    string You => Settings.PlayerName.ToUpperInvariant() == "PLAYER" ? "ATEM" : Settings.PlayerName.ToUpperInvariant();

    protected override void Build()
    {
        Demo.Ensure();
        W = GetViewportRect().Size.X; H = GetViewportRect().Size.Y;
        vs = Demo.Arg("state", "find") == "vs";
        AddChild(new ColorRect
        {
            AnchorRight = 1, AnchorBottom = 1, MouseFilter = MouseFilterEnum.Ignore,
            Material = Mat(@"shader_type canvas_item;
uniform vec4 cool : source_color;
void fragment() {
  vec2 p = (UV - vec2(0.5, 0.45)) * vec2(1.78, 1.0);
  vec3 c = mix(vec3(0.07, 0.056, 0.04), vec3(0.016, 0.017, 0.024), smoothstep(0.0, 1.1, length(p)));
  c = mix(c, cool.rgb * 0.12, smoothstep(0.55, 1.0, UV.x) * 0.7);
  COLOR = vec4(c, 1.0);
}", ("cool", Cool)),
        });
        barY = H - 120;
        switch (Demo.Arg("variant", "a"))
        {
            case "b": Halves(); break;
            case "c": Rooms(); break;
            case "d": vs = true; Clash(); break;
            case "e": vs = true; Arena(); break;
            case "f": vs = true; Shadow(); break;
            case "g": vs = true; Storm(); break;
            default: Podiums(); break;
        }
    }

    // ---- shared pieces

    void BottomBar(string title, string action)
    {
        AddChild(new ColorRect { Position = new Vector2(0, barY), Size = new Vector2(W, 120), Material = Mat(RivalPanels.Bar), MouseFilter = MouseFilterEnum.Ignore });
        Kit.Put(this, title, Kit.Cond, 50, Colors.White, new Vector2(64, barY + 29));
        AddChild(new ColorRect { Position = new Vector2(68, barY + 91), Size = new Vector2(110, 3), Color = RivalPanels.Gold });
        AddChild(Chips.Make(vs ? "LEAVE" : "BACK", new Vector2(W - 640, barY + 28), new Vector2(200, 64), () => { }));
        var go = Chips.Make(action, new Vector2(W - 410, barY + 22), new Vector2(330, 76), () => { }, 30);
        Chips.SetOn(go, vs);
        AddChild(go);
    }

    static Texture2D? Pic(int i) => Settings.PictureCard(i) is { } c ? CardArt.Picture(c) : null;
    static Texture2D? Art(int i) => Pic(i);

    Control Avatar(int pic, float size, Vector2 at, Control? parent = null)
    {
        var f = Cosmetics.Framed(Pic(pic), Shop.DefaultFrame, size);
        f.Position = at;
        (parent ?? this).AddChild(f);
        return f;
    }

    /// Your deck case (cover + name); the opponent's is a closed case showing their card back.
    Panel DeckCase(Control parent, Vector2 at, float w, bool hidden, int which = 0)
    {
        float h = w * 1.42f;
        var tile = Kit.Box(parent, new Rect2(at, new Vector2(w, h)), Kit.Glass(hidden ? Cool * new Color(1, 1, 1, 0.5f) : RivalPanels.Gold, hidden ? 0 : 0.35f, 12));
        var deck = Session.Decks.Count > which ? Session.Decks[which] : null;
        Texture2D? t = hidden ? Cosmetics.Back(Hosts[0].Pic % 2 == 0 ? "01_vortex" : Shop.DefaultBack, 360) : deck != null ? CardArt.Picture(CardDb.Shared.Get(deck.CoverCard())) : null;
        tile.AddChild(new TextureRect { Texture = t, ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered, Position = new Vector2(10, 10), Size = new Vector2(w - 20, w - 20), Material = Kit.RoundedMat(1, 0.05f), MouseFilter = MouseFilterEnum.Ignore });
        Kit.Put(tile, hidden ? "DECK HIDDEN" : (deck?.Name ?? "DECK").ToUpperInvariant(), Kit.Ui, 18, hidden ? Cool : Colors.White, new Vector2(6, w), new Vector2(w - 12, h - w - 6), HorizontalAlignment.Center, wrap: true).VerticalAlignment = VerticalAlignment.Center;
        return tile;
    }

    Panel Status(Control parent, string text, bool ok, Vector2 at)
    {
        var c = ok ? new Color(0.45f, 0.95f, 0.6f) : new Color(1, 1, 1, 0.55f);
        var b = Kit.Box(parent, new Rect2(at, new Vector2(220, 46)), RivalPanels.ChipStyle(new Color(c, 0.1f), c * new Color(1, 1, 1, 0.6f), 0));
        Kit.Put(b, text, Kit.Cond, 24, c, Vector2.Zero, b.Size, HorizontalAlignment.Center).VerticalAlignment = VerticalAlignment.Center;
        return b;
    }

    /// Hosts heard on the network: avatar, name, ban list, address, JOIN.
    void HostRows(Control parent, Vector2 at, float w, float rowH = 96)
    {
        for (int i = 0; i < Hosts.Length; i++)
        {
            var h = Hosts[i];
            var row = Kit.Box(parent, new Rect2(at + new Vector2(0, i * (rowH + 12)), new Vector2(w, rowH)), Kit.Glass(new Color(1, 1, 1, i == 0 ? 0.3f : 0.12f), 0, 14));
            Avatar(h.Pic, rowH - 16, new Vector2(10, 8), row);
            Kit.Put(row, h.Name, Kit.Impact, 38, Colors.White, new Vector2(rowH + 12, 10));
            Kit.Put(row, $"BAN LIST {h.List}   ·   {h.Addr}", Kit.Ui, 18, new Color(1, 1, 1, 0.5f), new Vector2(rowH + 14, 58));
            var chip = Chips.Make("JOIN", new Vector2(w - 150, (rowH - 56) / 2), new Vector2(130, 56), () => { });
            if (i == 0) Chips.SetLit(chip, true);
            row.AddChild(chip);
        }
    }

    /// "Or type an address" box.
    void IpBox(Control parent, Vector2 at, float w)
    {
        Kit.Put(parent, "OR JOIN BY ADDRESS", Kit.Cond, 24, RivalPanels.Gold, at);
        var box = Kit.Box(parent, new Rect2(at + new Vector2(0, 36), new Vector2(w - 150, 58)), Kit.Glass(new Color(1, 1, 1, 0.18f), 0, 12));
        Kit.Put(box, "192.168.1.", Kit.Ui, 26, new Color(1, 1, 1, 0.75f), new Vector2(18, 12));
        parent.AddChild(Chips.Make("JOIN", at + new Vector2(w - 136, 37), new Vector2(130, 56), () => { }));
    }

    /// Radar rings with a sweep, for "searching".
    ColorRect Radar(Control parent, Rect2 r)
    {
        var c = new ColorRect
        {
            Position = r.Position, Size = r.Size, MouseFilter = MouseFilterEnum.Ignore,
            Material = Mat(@"shader_type canvas_item;
uniform vec4 col : source_color;
void fragment() {
  vec2 p = UV - 0.5; float r = length(p) * 2.0;
  float f = fract(r * 4.0); float rings = (1.0 - smoothstep(0.0, 0.025, min(f, 1.0 - f))) * step(r, 1.0);
  float a = mod(atan(p.y, p.x) - TIME * 1.4, 6.2832);
  float sweep = smoothstep(1.2, 0.0, a) * step(r, 1.0) * 0.35;
  float cross = (smoothstep(0.004, 0.0, abs(p.x)) + smoothstep(0.004, 0.0, abs(p.y))) * step(r, 1.0) * 0.25;
  COLOR = vec4(col.rgb, (rings * 0.35 + sweep + cross) * (1.0 - r * 0.4));
}", ("col", Cool)),
        };
        parent.AddChild(c);
        return c;
    }

    // ---- a: Versus podiums

    void Podiums()
    {
        float mid = W / 2;
        // You, left.
        Avatar(Settings.Picture, 280, new Vector2(150, 140));
        Kit.Put(this, You, Kit.Impact, 96, Colors.White, new Vector2(140, 440), outline: 10);
        Kit.Put(this, "YOUR DECK", Kit.Cond, 26, RivalPanels.Gold, new Vector2(150, 580));
        DeckCase(this, new Vector2(150, 618), 170, false);
        for (int i = 1; i < Math.Min(4, Session.Decks.Count); i++)
        {
            var t = DeckCase(this, new Vector2(150 + i * 186, 638), 150, false, i);
            t.Modulate = new Color(0.6f, 0.6f, 0.62f);
        }
        if (vs) Status(this, "READY", true, new Vector2(470, 260));

        // VS line.
        AddChild(new ColorRect { Position = new Vector2(mid - 2, 80), Size = new Vector2(4, barY - 160), Color = RivalPanels.Gold * new Color(1, 1, 1, 0.35f), Rotation = 0.06f });
        var v = Kit.Put(this, "VS", Kit.Impact, 120, RivalPanels.Gold, new Vector2(mid - 90, barY / 2 - 90), new Vector2(180, 150), HorizontalAlignment.Center, 12);

        float rx = mid + 110, rw = W - rx - 110;
        if (vs)
        {
            Avatar(Them.Pic, 280, new Vector2(W - 150 - 280, 140));
            Kit.Put(this, Them.Name, Kit.Impact, 96, Cool, new Vector2(rx, 440), new Vector2(rw + 40, 120), HorizontalAlignment.Right, 10);
            Kit.Put(this, "THEIR DECK", Kit.Cond, 26, Cool, new Vector2(rx, 580), new Vector2(rw + 40, 30), HorizontalAlignment.Right);
            DeckCase(this, new Vector2(W - 150 - 170, 618), 170, true);
            Status(this, "PICKING A DECK…", false, new Vector2(W - 150 - 280 - 250, 260));
            Kit.Put(this, $"CONNECTED  ·  {Them.Addr}  ·  BAN LIST 2004", Kit.Ui, 20, new Color(1, 1, 1, 0.45f), new Vector2(rx, 860), new Vector2(rw + 40, 30), HorizontalAlignment.Right);
            BottomBar("LAN DUEL", "READY");
            return;
        }
        Radar(this, new Rect2(W - 560, 20, 520, 520)).Modulate = new Color(1, 1, 1, 0.5f);
        Kit.Put(this, "DUELISTS ON YOUR NETWORK", Kit.Cond, 30, Cool, new Vector2(rx, 110));
        HostRows(this, new Vector2(rx, 160), rw);
        IpBox(this, new Vector2(rx, 540), rw);
        Kit.Put(this, "Or host: your PC waits and shows up in their list.", Kit.Ui, 20, new Color(1, 1, 1, 0.45f), new Vector2(rx, 680));
        BottomBar("LAN DUEL", "HOST DUEL");
    }

    // ---- b: Card halves

    void Halves()
    {
        float pw = W * 0.47f, ph = barY + 10;
        Panel Half(Texture2D? art, Color tint, bool right)
        {
            var p = new Panel { Size = new Vector2(pw, ph), Position = new Vector2(right ? W - pw + 20 : -20, 0), ClipContents = true, MouseFilter = MouseFilterEnum.Ignore };
            p.AddThemeStyleboxOverride("panel", new StyleBoxFlat { BgColor = new Color(0.03f, 0.03f, 0.045f), Skew = new Vector2(right ? 0.12f : -0.12f, 0) });
            if (art != null)
                p.AddChild(new TextureRect { Texture = art, ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered, Size = new Vector2(pw, ph), Material = Mat(MainMenu.Foil, ("tint", tint), ("phase", right ? 0.5f : 0f)), MouseFilter = MouseFilterEnum.Ignore });
            p.AddChild(new ColorRect { Size = new Vector2(pw, ph), MouseFilter = MouseFilterEnum.Ignore, Material = Mat(@"shader_type canvas_item;
void fragment() { COLOR = vec4(0.0, 0.0, 0.0, smoothstep(0.35, 1.0, UV.y) * 0.85); }") });
            AddChild(p);
            return p;
        }
        Half(Art(Settings.Picture), RivalPanels.Gold, false);
        Kit.Put(this, You, Kit.Impact, 110, Colors.White, new Vector2(80, barY - 330), outline: 12);
        Kit.Put(this, "YOUR DECK", Kit.Cond, 26, RivalPanels.Gold, new Vector2(86, barY - 190));
        Kit.Put(this, (Session.Decks.FirstOrDefault()?.Name ?? "DECK").ToUpperInvariant(), Kit.Ui, 34, Colors.White, new Vector2(86, barY - 156));
        AddChild(Chips.Make("CHANGE DECK", new Vector2(86, barY - 96), new Vector2(240, 56), () => { }, 22));
        if (vs)
        {
            Half(Art(Them.Pic), Cool, true);
            Kit.Put(this, Them.Name, Kit.Impact, 110, Colors.White, new Vector2(W - 80 - 900, barY - 330), new Vector2(900, 140), HorizontalAlignment.Right, 12);
            Kit.Put(this, "PICKING A DECK…", Kit.Cond, 30, Cool, new Vector2(W - 86 - 600, barY - 170), new Vector2(600, 40), HorizontalAlignment.Right);
            Kit.Put(this, $"{Them.Addr}  ·  BAN LIST 2004", Kit.Ui, 20, new Color(1, 1, 1, 0.5f), new Vector2(W - 86 - 600, barY - 120), new Vector2(600, 30), HorizontalAlignment.Right);
            Kit.Put(this, "VS", Kit.Impact, 150, RivalPanels.Gold, new Vector2(W / 2 - 120, barY / 2 - 110), new Vector2(240, 200), HorizontalAlignment.Center, 14);
            BottomBar("LAN DUEL", "READY");
            return;
        }
        // Searching: a radar where the rival's half will be, hosts as blips with names.
        var radar = new Rect2(W - pw / 2 - 380, barY / 2 - 380, 760, 760);
        Radar(this, radar);
        var spots = new[] { new Vector2(-0.22f, -0.2f), new Vector2(0.25f, 0.05f), new Vector2(-0.08f, 0.3f) };
        for (int i = 0; i < Hosts.Length; i++)
        {
            var at = radar.GetCenter() + spots[i] * radar.Size.X;
            var a = Avatar(Hosts[i].Pic, 110, at - new Vector2(55, 55));
            if (i == 0) a.Scale = new Vector2(1.15f, 1.15f);
            Kit.Put(this, Hosts[i].Name, Kit.Impact, 32, i == 0 ? RivalPanels.Gold : Colors.White, at + new Vector2(-150, 64), new Vector2(300, 40), HorizontalAlignment.Center, 8);
        }
        Kit.Put(this, "CLICK A DUELIST TO JOIN", Kit.Cond, 28, Cool, new Vector2(W - pw, barY - 160), new Vector2(pw - 60, 34), HorizontalAlignment.Right);
        var box = Kit.Box(this, new Rect2(W - 560, barY - 110, 360, 56), Kit.Glass(new Color(1, 1, 1, 0.18f), 0, 12));
        Kit.Put(box, "Address…", Kit.Ui, 24, new Color(1, 1, 1, 0.45f), new Vector2(18, 12));
        AddChild(Chips.Make("JOIN", new Vector2(W - 186, barY - 110), new Vector2(130, 56), () => { }));
        BottomBar("LAN DUEL", "HOST DUEL");
    }

    // ---- c: Room list

    void Rooms()
    {
        // Left column: you, your deck, host / address.
        var me = Kit.Box(this, new Rect2(64, 64, 520, barY - 128), Kit.Glass(RivalPanels.Gold * new Color(1, 1, 1, 0.5f), 0.15f));
        Avatar(Settings.Picture, 200, new Vector2(160, 30), me);
        Kit.Put(me, You, Kit.Impact, 64, Colors.White, new Vector2(0, 240), new Vector2(520, 80), HorizontalAlignment.Center, 8);
        Kit.Put(me, "YOUR DECK", Kit.Cond, 24, RivalPanels.Gold, new Vector2(40, 340));
        DeckCase(me, new Vector2(40, 380), 150, false);
        Kit.Put(me, $"{Session.Decks.FirstOrDefault()?.Main.Count ?? 40} CARDS\nBAN LIST 2004  ✓", Kit.Ui, 22, new Color(1, 1, 1, 0.7f), new Vector2(214, 400));
        me.AddChild(Chips.Make("CHANGE", new Vector2(214, 490), new Vector2(180, 54), () => { }, 22));
        if (!vs) IpBox(me, new Vector2(40, 650), 440);

        float x = 640, w = W - x - 64;
        if (vs)
        {
            // Connected: a match card with both duelists.
            var card = Kit.Box(this, new Rect2(x, 64, w, barY - 128), Kit.Glass(Cool * new Color(1, 1, 1, 0.5f), 0.12f));
            Kit.Put(card, "ROOM  ·  " + Them.Addr, Kit.Cond, 28, Cool, new Vector2(40, 30));
            for (int s = 0; s < 2; s++)
            {
                float cx = s == 0 ? w * 0.25f : w * 0.75f;
                Avatar(s == 0 ? Settings.Picture : Them.Pic, 220, new Vector2(cx - 110, 120), card);
                Kit.Put(card, s == 0 ? You : Them.Name, Kit.Impact, 60, s == 0 ? Colors.White : Cool, new Vector2(cx - 300, 360), new Vector2(600, 80), HorizontalAlignment.Center, 8);
                Status(card, s == 0 ? "READY" : "PICKING A DECK…", s == 0, new Vector2(cx - 110, 460));
            }
            Kit.Put(card, "VS", Kit.Impact, 110, RivalPanels.Gold, new Vector2(w / 2 - 100, 170), new Vector2(200, 140), HorizontalAlignment.Center, 12);
            Kit.Put(card, "Host's ban list: 2004.  Decks stay hidden until the duel.", Kit.Ui, 22, new Color(1, 1, 1, 0.5f), new Vector2(0, barY - 230), new Vector2(w, 30), HorizontalAlignment.Center);
            BottomBar("LAN DUEL", "READY");
            return;
        }
        Kit.Put(this, "OPEN DUELS ON YOUR NETWORK", Kit.Cond, 30, Cool, new Vector2(x, 70));
        Kit.Put(this, "searching…", Kit.Ui, 22, new Color(1, 1, 1, 0.4f), new Vector2(x + w - 140, 76));
        HostRows(this, new Vector2(x, 120), w, 110);
        var empty = Kit.Box(this, new Rect2(x, 120 + 3 * 122, w, 110), Kit.Glass(new Color(1, 1, 1, 0.06f), 0, 14));
        Kit.Put(empty, "No one else? HOST DUEL and your friend will see you here.", Kit.Ui, 22, new Color(1, 1, 1, 0.4f), Vector2.Zero, empty.Size, HorizontalAlignment.Center).VerticalAlignment = VerticalAlignment.Center;
        BottomBar("LAN DUEL", "HOST DUEL");
    }
}
