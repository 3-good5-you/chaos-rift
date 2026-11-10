using Godot;
using PoC.Engine;

namespace PoC.Game.UI;

/// Mockups for the Duelist Points shop (not wired into the game).
/// `-- --demo --variant=a|b|c [--tab=cards|mats|backs] [--state=buy]`
/// a = SHOP tab on the Card List wall, b = a rival's stall, c = trade-in counter. Mock data only;
/// card backs are our own (game/tools/backforge.py).
public partial class ShopSamples : MenuBase
{
    float W, H, barY;
    protected override int ItemCount => 1;
    protected override void OnFocus(int old, int now) { }
    protected override void OnChoose(int i) { }

    const int Dp = 4850;
    static readonly Color Teal = new(0.35f, 0.9f, 0.85f);

    /// Mock price: what a card is worth in Duelist Points (limited cards cost the most).
    static int Price(CardData d)
    {
        int lim = DeckRules.Limit(d);
        if (lim <= 1) return lim == 0 ? 2400 : 1800;
        if (lim == 2) return 1200;
        int p = d.IsMonster ? (d.IsNormalMonster ? 100 + (d.Atk + d.Def) / 10 : 300 + d.Atk / 6) : 450;
        return Math.Max(100, p / 50 * 50);
    }

    /// Cards the rivals played against you that you don't own (mock: from a few of their decks).
    static List<CardData> Seen(string? rival = null)
    {
        var db = CardDb.Shared; var p = Session.Profile;
        var keys = new[] { "yugi/cpu03", "kaiba/kaiba_kk", "joey/cpu_011", "kaiba/yk_kkk", "yugi/cpukk" }.Where(k => rival == null || k.StartsWith(rival));
        return keys.SelectMany(k => db.Deck(k).Main).Distinct().Select(db.Get).Where(d => p.Count(d.Idx) == 0).DistinctBy(d => d.Name).OrderByDescending(Price).ToList();
    }

    protected override void Build()
    {
        Demo.Ensure();
        W = GetViewportRect().Size.X; H = GetViewportRect().Size.Y;
        AddChild(new ColorRect
        {
            AnchorRight = 1, AnchorBottom = 1, MouseFilter = MouseFilterEnum.Ignore,
            Material = Mat(@"shader_type canvas_item;
void fragment() {
  vec2 p = (UV - vec2(0.3, 0.2)) * vec2(1.78, 1.0);
  COLOR = vec4(mix(vec3(0.075, 0.06, 0.045), vec3(0.018, 0.018, 0.026), smoothstep(0.0, 1.2, length(p))), 1.0);
}"),
        });
        barY = H - 120;
        switch (Demo.Arg("variant", "a"))
        {
            case "b": Stall(); break;
            case "c": Counter(); break;
            default: Wall(); break;
        }
    }

    // ---- shared pieces

    void BottomBar(string title)
    {
        AddChild(new ColorRect { Position = new Vector2(0, barY), Size = new Vector2(W, 120), Material = Mat(RivalPanels.Bar), MouseFilter = MouseFilterEnum.Ignore });
        Kit.Put(this, title, Kit.Cond, 50, Colors.White, new Vector2(64, barY + 29));
        AddChild(new ColorRect { Position = new Vector2(68, barY + 91), Size = new Vector2(110, 3), Color = RivalPanels.Gold });
        AddChild(Chips.Make("BACK", new Vector2(W - 264, barY + 28), new Vector2(200, 64), () => { }));
    }

    /// DP balance pill.
    void Balance(Vector2 at, int dp, float scale = 1)
    {
        var b = Kit.Box(this, new Rect2(at, new Vector2(300, 64) * scale), RivalPanels.ChipStyle(new Color(0.1f, 0.08f, 0.03f, 0.9f), RivalPanels.Gold, 0.35f));
        Kit.Put(b, "DP", Kit.Cond, (int)(24 * scale), RivalPanels.Gold * new Color(1, 1, 1, 0.75f), new Vector2(22, 17) * scale);
        Kit.Put(b, dp.ToString("N0"), Kit.Impact, (int)(38 * scale), RivalPanels.Gold, new Vector2(70, 8) * scale, new Vector2(210, 50) * scale, HorizontalAlignment.Right);
    }

    /// A card with a price tag under it (dimmed when you can't afford it).
    void PricedCard(Control parent, CardData d, Vector2 pos, Vector2 size)
    {
        int price = Price(d);
        var t = new TextureRect { Texture = CardArt.Small(d), ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.Scale, Position = pos, Size = size, Material = Kit.RoundedMat(size.X / size.Y), TextureFilter = TextureFilterEnum.LinearWithMipmaps };
        parent.AddChild(t);
        bool can = price <= Dp;
        if (!can) t.Modulate = new Color(0.55f, 0.55f, 0.6f);
        var tag = new Control { Position = pos + new Vector2(size.X / 2 - 62, size.Y - 22), Size = new Vector2(124, 38) };
        tag.Draw += () => Kit.Badge(tag, new Rect2(Vector2.Zero, tag.Size), can ? new Color(0.08f, 0.06f, 0.02f, 0.92f) : new Color(0.1f, 0.1f, 0.12f, 0.92f), $"{price:N0} DP", Kit.Cond, 22, can ? RivalPanels.Gold : new Color(1, 1, 1, 0.4f));
        parent.AddChild(tag);
    }

    void BuyPanel(CardData d, string seenLine)
    {
        var dim = new ColorRect { Size = new Vector2(W, H), Color = new Color(0, 0, 0, 0.78f), ZIndex = 20 };
        AddChild(dim);
        float bh = H - 200, bw = bh / 1.458f;
        dim.AddChild(new TextureRect { Texture = CardArt.Large(d), ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.Scale, Material = Kit.RoundedMat(1 / 1.458f, 0.02f), Position = new Vector2(W / 2 - bw - 60, 100), Size = new Vector2(bw, bh), TextureFilter = TextureFilterEnum.LinearWithMipmaps });
        float pw = Math.Min(760, W / 2 - 110), iw = pw - 88;
        var panel = Kit.Box(dim, new Rect2(W / 2 + 10, 100, pw, bh), Kit.Glass(RivalPanels.Gold * new Color(1, 1, 1, 0.5f)));
        var name = Kit.Put(panel, d.Name, Kit.Impact, 58, Colors.White, new Vector2(44, 36), new Vector2(iw, 72), outline: 8, wrap: true);
        float y = 36 + name.GetLineCount() * name.GetLineHeight() + 4;
        Kit.Put(panel, d.IsMonster ? $"{d.Attribute}  ·  {d.Race.ToUpperInvariant()} / {d.Frame.ToUpperInvariant()}" : $"{d.Kind.ToString().ToUpperInvariant()}  ·  {d.Frame.ToUpperInvariant()}", Kit.Ui, 22, RivalPanels.Gold, new Vector2(44, y)); y += 38;
        if (d.IsMonster) { Kit.Put(panel, $"ATK {d.Atk}   DEF {d.Def}", Kit.Impact, 40, Colors.White, new Vector2(44, y), outline: 6); y += 60; }
        var desc = Kit.Put(panel, d.Desc, Kit.Ui, 20, new Color(1, 1, 1, 0.85f), new Vector2(44, y), new Vector2(iw, 40), wrap: true);
        y += desc.GetLineCount() * desc.GetLineHeight() + 20;
        Kit.Put(panel, seenLine, Kit.Cond, 24, Teal, new Vector2(44, y)); y += 60;
        int price = Price(d);
        Kit.Put(panel, $"{price:N0} DP", Kit.Impact, 54, RivalPanels.Gold, new Vector2(44, y), outline: 6);
        Kit.Put(panel, $"YOU HAVE {Dp:N0}  →  {Dp - price:N0} AFTER", Kit.Cond, 22, new Color(1, 1, 1, 0.55f), new Vector2(44, y + 70));
        var buy = Chips.Make("BUY", new Vector2(pw - 284, y + 6), new Vector2(240, 72), () => { }, 30);
        Chips.SetLit(buy, true);
        panel.AddChild(buy);
        panel.Size = new Vector2(pw, Math.Min(bh, y + 140));
    }

    string Tab => Demo.Arg("tab", "cards");

    /// CARDS / PLAYMATS / CARD BACKS chips.
    void Tabs(Vector2 at)
    {
        var names = new[] { ("cards", "CARDS"), ("mats", "PLAYMATS"), ("backs", "CARD BACKS") };
        float x = at.X;
        foreach (var (key, label) in names)
        {
            float w = label.Length * 13 + 60;
            var c = Chips.Make(label, new Vector2(x, at.Y), new Vector2(w, 56), () => { });
            if (key == Tab) Chips.SetOn(c, true);
            AddChild(c);
            x += w + 14;
        }
    }

    static Texture2D? Shot(string file)
    {
        var img = Image.LoadFromFile(ProjectSettings.GlobalizePath("res://") + "../samples_shots/" + file);
        return img == null ? null : ImageTexture.CreateFromImage(img);
    }

    const float MatAspect = 7.3f / 4.9f;

    /// The playmat cut out of a top-down `--matview` render (DuelField: mat 7.3 x 4.9 in an orthographic view 5.8 high).
    static Texture2D? MatOnly(string file)
    {
        if (Shot(file) is not { } t) return null;
        float vh = 5.8f, vw = vh * t.GetWidth() / t.GetHeight();
        var size = new Vector2(t.GetWidth() * 7.3f / vw, t.GetHeight() * 4.9f / vh);
        return new AtlasTexture { Atlas = t, Region = new Rect2((new Vector2(t.GetWidth(), t.GetHeight()) - size) / 2, size) };
    }

    /// Price pill (gold) or how-to-unlock pill (teal) under a cosmetic.
    void Tag(Vector2 centre, string text, bool gold)
    {
        float w = Math.Max(110, text.Length * 11 + 30);
        var tag = new Control { Position = centre - new Vector2(w / 2, 19), Size = new Vector2(w, 38) };
        tag.Draw += () => Kit.Badge(tag, new Rect2(Vector2.Zero, tag.Size), gold ? new Color(0.08f, 0.06f, 0.02f, 0.92f) : new Color(0.1f, 0.1f, 0.12f, 0.92f), text, Kit.Cond, 21, gold ? RivalPanels.Gold : Teal);
        AddChild(tag);
    }

    /// Playmats (previewed with the real field) or card backs, in `area`.
    void Cosmetics(Rect2 area)
    {
        if (Tab == "mats")
        {
            var mats = new[]
            {
                ("ARENA", "mat_arena.png", "EQUIPPED"), ("MAGMA", "mats_forge/top_magma.png", "3,000 DP"), ("TIDE", "mats_forge/top_tide.png", "3,000 DP"),
                ("PHARAOH", "mats_forge/top_pharaoh.png", "BEAT ALL RIVALS ON EXPERT"), ("DATASTREAM", "mats_forge/top_datastream.png", "2,500 DP"), ("BLUE-EYES", "mats_forge/top_art_blue_eyes.png", "5,000 DP"),
            };
            int cols = 3; float gap = 34, w = (area.Size.X - gap * (cols - 1)) / cols, h = w / MatAspect;
            if (2 * (h + 120) > area.Size.Y) { h = area.Size.Y / 2 - 120; w = h * MatAspect; }
            for (int i = 0; i < mats.Length; i++)
            {
                var (name, file, state) = mats[i];
                var pos = area.Position + new Vector2(i % cols * (w + gap), i / cols * (h + 120));
                var t = new TextureRect { Texture = MatOnly(file), ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.Scale, Position = pos, Size = new Vector2(w, h), Material = Kit.RoundedMat(w / h, 0.03f), TextureFilter = TextureFilterEnum.LinearWithMipmaps };
                if (state.StartsWith("BEAT")) t.Modulate = new Color(0.45f, 0.45f, 0.5f);
                AddChild(t);
                if (state == "EQUIPPED")
                    Kit.Box(this, new Rect2(pos - new Vector2(4, 4), new Vector2(w + 8, h + 8)), new StyleBoxFlat { DrawCenter = false, BorderColor = Teal, BorderWidthTop = 3, BorderWidthBottom = 3, BorderWidthLeft = 3, BorderWidthRight = 3, CornerRadiusTopLeft = 14, CornerRadiusTopRight = 14, CornerRadiusBottomLeft = 14, CornerRadiusBottomRight = 14, AntiAliasing = true });
                Kit.Put(this, name, Kit.Cond, 28, Colors.White, pos + new Vector2(4, h + 12));
                Tag(pos + new Vector2(Math.Max(110, state.Length * 11 + 30) / 2, h + 78), state, state.EndsWith("DP"));
            }
            return;
        }
        // Our own backs (game/tools/backforge.py, renders in samples_shots/backs_forge).
        var backs = new[]
        {
            ("VORTEX", "01_vortex", "EQUIPPED"), ("SILVER GRID", "02_silver_grid", "2,000 DP"), ("LIGHT", "03_light", "3,000 DP"),
            ("DARK", "04_dark", "3,000 DP"), ("FIRE", "07_fire", "3,000 DP"), ("CIRCUIT", "09_circuit_blue", "2,500 DP"),
            ("MILLENNIUM", "12_millennium", "BEAT ALL RIVALS ON EXPERT"), ("DARK MAGICIAN", "14_art_dark_magician", "BEAT YUGI ON HARD"),
        };
        int bcols = 8;
        var size = new Vector2(190, 277);
        float g = (area.Size.X - bcols * size.X) / (bcols - 1);
        if (g < 20) { size *= (area.Size.X - 20 * (bcols - 1)) / bcols / size.X; g = 20; }
        for (int i = 0; i < backs.Length; i++)
        {
            var (name, file, state) = backs[i];
            var pos = area.Position + new Vector2(i % bcols * (size.X + g), i / bcols * (size.Y + 150));
            AddChild(new TextureRect { Texture = Shot("backs_forge/" + file + ".png"), ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.Scale, Position = pos, Size = size, Material = Kit.RoundedMat(size.X / size.Y), TextureFilter = TextureFilterEnum.LinearWithMipmaps });
            Kit.Put(this, name, Kit.Cond, 22, Colors.White, pos + new Vector2(-10, size.Y + 12), new Vector2(size.X + 20, 60), HorizontalAlignment.Center, wrap: true);
            Tag(pos + new Vector2(size.X / 2, size.Y + 96), state.Length > 16 ? state[..16] + "…" : state, state.EndsWith("DP"));
        }
    }

    // ---- A: SHOP tab on the Card List wall

    void Wall()
    {
        var p = Session.Profile;
        var seen = Seen().Select(d => d.Name).ToHashSet();
        var cards = CardDb.Shared.ByIdx.Values.Where(d => d.PackMask != 0 && p.Count(d.Idx) == 0).DistinctBy(d => d.Name)
            .OrderByDescending(d => seen.Contains(d.Name)).ThenBy(d => d.Kind).ThenBy(d => d.Name).Take(60).ToList();
        var tile = new Vector2(150, 219); float gap = 18, x0 = 40, y0 = 40;
        int cols = (int)((W - 80 + gap) / (tile.X + gap));
        if (Tab != "cards") { Cosmetics(new Rect2(80, 70, W - 160, barY - 120)); cards.Clear(); }
        for (int i = 0; i < cards.Count; i++)
        {
            var pos = new Vector2(x0 + i % cols * (tile.X + gap), y0 + i / cols * (tile.Y + 34));
            if (pos.Y + tile.Y > barY - 20) break;
            var d = cards[i];
            if (seen.Contains(d.Name)) PricedCard(this, d, pos, tile);
            else AddChild(new CardTile(d, tile) { Position = pos, ShowCount = false });
        }
        BottomBar("CARD LIST");
        AddChild(Chips.Make("ALL", new Vector2(330, barY + 32), new Vector2(110, 56), () => { }));
        Tabs(new Vector2(460, barY + 32));
        Balance(new Vector2(W - 600, barY + 28), Dp);
        if (Demo.Arg("state") == "buy") BuyPanel(Seen().First(d => Price(d) <= Dp && d.IsMonster), "SEEN: KAIBA PLAYED IT  ·  3 DUELS AGO");
    }

    // ---- B: a rival's stall

    void Stall()
    {
        var r = RivalPanels.All[1];
        var tex = GD.Load<Texture2D>($"res://assets/rivals/{r.File}_happy.png");
        float rh = barY + 20, rw = rh * tex.GetWidth() / tex.GetHeight();
        AddChild(new ColorRect { Position = new Vector2(W - rw - 120, 0), Size = new Vector2(rw + 120, barY), Color = r.Color * new Color(1, 1, 1, 0.12f) });
        AddChild(new TextureRect { Texture = tex, ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.KeepAspect, Position = new Vector2(W - rw - 40, barY - rh), Size = new Vector2(rw, rh) });
        float cx = 70;
        Kit.Put(this, "KAIBA'S CARDS", Kit.Impact, 84, Colors.White, new Vector2(cx, 34), outline: 10);
        Kit.Put(this, "CARDS KAIBA PLAYED AGAINST YOU. TRADE DUELIST POINTS FOR THEM.", Kit.Cond, 26, new Color(1, 1, 1, 0.6f), new Vector2(cx + 4, 140));
        var seen = Seen("kaiba");
        var size = new Vector2(196, 286); float gap = 30;
        int perRow = (int)((W - rw - 200 + gap) / (size.X + gap));
        Tabs(new Vector2(cx, 196));
        if (Tab != "cards") { Cosmetics(new Rect2(cx, 300, W - rw - 260, barY - 340)); seen.Clear(); }
        for (int i = 0; i < Math.Min(seen.Count, perRow * 2); i++)
            PricedCard(this, seen[i], new Vector2(cx + i % perRow * (size.X + gap), 290 + i / perRow * (size.Y + 50)), size);
        BottomBar("SHOP");
        for (int i = 0; i < 3; i++)
        {
            var c = Chips.Make(RivalPanels.All[i].Name, new Vector2(300 + i * 170, barY + 32), new Vector2(156, 56), () => { });
            if (i == 1) Chips.SetOn(c, true);
            AddChild(c);
        }
        Balance(new Vector2(W - 600, barY + 28), Dp);
        if (Demo.Arg("state") == "buy" && seen.Count > 0) BuyPanel(seen.First(d => Price(d) <= Dp && d.IsMonster), "KAIBA PLAYED IT  ·  3 DUELS AGO");
    }

    // ---- C: trade-in counter

    void Counter()
    {
        var db = CardDb.Shared;
        Kit.Put(this, "DUELIST POINTS", Kit.Impact, 84, Colors.White, new Vector2(70, 34), outline: 10);
        // Left: spare copies (beyond 3) turning into DP.
        var left = Kit.Box(this, new Rect2(60, 170, 560, barY - 210), Kit.Glass(Teal * new Color(1, 1, 1, 0.5f)));
        Kit.Put(left, "SPARE COPIES", Kit.Cond, 34, Teal, new Vector2(34, 26));
        Kit.Put(left, "COPIES OVER 3 TURN INTO DP AUTOMATICALLY", Kit.Cond, 20, new Color(1, 1, 1, 0.5f), new Vector2(34, 70));
        var spares = new[] { ("Kuriboh", 2), ("Giant Germ", 1), ("Battle Ox", 3), ("Trap Hole", 1), ("Mystical Elf", 2) };
        float y = 120;
        foreach (var (n, k) in spares)
        {
            var d = db.Named(n);
            left.AddChild(new TextureRect { Texture = CardArt.Small(d), ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.Scale, Position = new Vector2(34, y), Size = new Vector2(62, 90), Material = Kit.RoundedMat(62f / 90) });
            Kit.Put(left, d.Name, Kit.Cond, 26, Colors.White, new Vector2(116, y + 12), new Vector2(280, 34));
            Kit.Put(left, $"×{k} SPARE", Kit.Cond, 20, new Color(1, 1, 1, 0.5f), new Vector2(116, y + 48));
            Kit.Put(left, $"+{k * 50} DP", Kit.Impact, 32, Teal, new Vector2(330, y + 24), new Vector2(190, 44), HorizontalAlignment.Right);
            y += 112;
        }
        Balance(new Vector2(660, 190), Dp, 1.4f);
        Kit.Put(this, "+50 PER WIN  ·  +200 EXPERT  ·  +20 PER LOSS", Kit.Cond, 22, new Color(1, 1, 1, 0.5f), new Vector2(664, 300));
        var seen = Seen();
        Tabs(new Vector2(660, 360));
        if (Tab != "cards") { Cosmetics(new Rect2(660, 460, W - 720, barY - 500)); seen.Clear(); }
        var size = new Vector2(176, 257); float gap = 26;
        int perRow = (int)((W - 720 + gap) / (size.X + gap));
        for (int i = 0; i < Math.Min(seen.Count, perRow * 2); i++)
            PricedCard(this, seen[i], new Vector2(660 + i % perRow * (size.X + gap), 430 + i / perRow * (size.Y + 52)), size);
        BottomBar("SHOP");
        if (Demo.Arg("state") == "buy" && seen.Count > 0) BuyPanel(seen.First(d => Price(d) <= Dp && d.IsMonster), "SEEN: JOEY PLAYED IT  ·  LAST DUEL");
    }
}
