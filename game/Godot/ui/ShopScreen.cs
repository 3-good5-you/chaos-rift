using Godot;
using PoC.Engine;
using Cosmetic = PoC.Engine.Shop.Cosmetic;

namespace PoC.Game.UI;

/// Shop (Chaos Rift, locked 2026-11-08). CARDS: a paged grid of every card for sale with its price, NEW for ones you
/// don't own, diamonds for copies you own; the chosen card large in the dark with BUY ×1 / ×3. CARD BACKS and FRAMES:
/// a grid in the light, the chosen one large in the dark. PLAYMATS: a price list in the light, the mat shown big.
/// DP top right. `-- --demo [--tab=cards|backs|mats|frames]`
public partial class ShopScreen : RiftMenu
{
    string tab = "cards";
    Control page = null!, detail = null!;
    Label dp = null!;
    CardBrowser? browser;
    readonly List<(string Key, Action<bool> Set)> tabs = new();

    protected override void OnBack() => Transition.Go(this, "res://ui/MainMenu.tscn");

    protected override void Build()
    {
        Demo.Ensure();
        Rift(0.62f, 0.56f);
        Back();
        Heading("SHOP", new Vector2(80, 60), 46);
        float x = 260;
        foreach (var (key, label) in new[] { ("cards", "CARDS"), ("backs", "CARD BACKS"), ("mats", "PLAYMATS"), ("frames", "FRAMES") })
        {
            var k = key;
            var (w, set) = Word(this, label, new Vector2(x, 78), 19, false, () => Show(k));
            tabs.Add((key, set));
            x += w + 30;
        }
        dp = Put(this, "", Title, 30, GoldLight, new Vector2(W - 380, 60), new Vector2(320, 40), HorizontalAlignment.Right);
        page = new Control { Size = new Vector2(W, H), MouseFilter = MouseFilterEnum.Ignore };
        AddChild(page);
        Show(Demo.Arg("tab", "cards"));
    }

    void Show(string t)
    {
        tab = t;
        foreach (var (k, set) in tabs) set(k == t);
        foreach (var c in page.GetChildren()) c.QueueFree();
        detail = new Control { Size = new Vector2(W, H), MouseFilter = MouseFilterEnum.Ignore };
        page.AddChild(detail);
        dp.Text = $"{P.Dp:N0} DP";
        switch (t)
        {
            case "backs": Grid(Cosmetic.Back); break;
            case "frames": Grid(Cosmetic.Frame); break;
            case "mats": Mats(); break;
            default: Cards(); break;
        }
    }

    void ClearDetail() { foreach (var c in detail.GetChildren()) c.QueueFree(); }

    /// Buy and refresh what changed.
    void Bought()
    {
        Session.Save();
        UiSound(true);
        Flash("BOUGHT");
        dp.Text = $"{P.Dp:N0} DP";
    }

    // ---------------------------------------------------------------- cards

    void Cards()
    {
        int Rank(CardData d) => P.Count(d.Idx) >= 3 ? 2 : P.Seen.Contains(d.Idx) ? 0 : 1;
        var all = Db.ByIdx.Values.Where(d => (d.PackMask != 0 || P.Seen.Contains(d.Idx) || P.Count(d.Idx) > 0) && Db.Canon(d.Idx) == d.Idx && d.Name.Length > 0 && !d.Name.EndsWith("Token"))
            .OrderBy(Rank).ThenByDescending(Shop.Price).ThenBy(d => d.Name).ToList();
        browser = new CardBrowser(this, new Rect2(80, 170, W * 0.5f, H - 210), 128, () => all, new[] { "FEATURED", "PRICE", "NAME", "ATK" });
        foreach (var n in GetChildren().OfType<Control>().TakeLast(2).ToList()) n.Reparent(page);
        browser.Decorate = (hit, d, size) =>
        {
            int own = P.Count(d.Idx);
            Put(hit, own >= 3 ? "OWNED ×3" : $"{Shop.Price(d):N0}", Thin, 15, own >= 3 ? Graphite * new Color(1, 1, 1, 0.5f) : Graphite, new Vector2(0, size.Y + 6), new Vector2(size.X, 20), HorizontalAlignment.Center);
            if (own == 0) Put(hit, "NEW", Title, 13, GoldDeep, new Vector2(2, -20));
            else for (int k = 0; k < Math.Min(3, own); k++) Pip(hit, new Vector2(8 + k * 13, -9), 6);
        };
        browser.Faded = d => P.Count(d.Idx) >= 3 || Shop.Price(d) > P.Dp;
        browser.Picked = CardDetail;
        browser.Refresh();
        var first = all.FirstOrDefault(d => P.Count(d.Idx) < 3 && Shop.Price(d) <= P.Dp) ?? all[0];
        browser.Selected = first; browser.BuildGrid();
        CardDetail(first);
    }

    void CardDetail(CardData d)
    {
        ClearDetail();
        Card(detail, d, new Vector2(W * 0.81f, H * 0.4f), H * 0.48f, 0.04f, 1f, large: true);
        float x = W * 0.64f, w = W * 0.34f, y = H * 0.7f;
        int ns = 24; while (ns > 16 && Title.GetStringSize(d.Name.ToUpperInvariant(), HorizontalAlignment.Left, -1, Z(ns)).X > w) ns -= 2;
        Put(detail, d.Name.ToUpperInvariant(), Title, ns, Silver, new Vector2(x, y), new Vector2(w, 34), HorizontalAlignment.Center);
        // the card's text with the keywords marked, under the buy row
        CardText(detail, d, new Vector2(x, y + 176), new Vector2(w, H - y - 200), 17, light: OnLight(new Vector2(x + w / 2, y + 176)));
        int own = P.Count(d.Idx), price = Shop.Price(d);
        Put(detail, $"{price:N0} DP EACH   ·   OWNED ×{own}", Thin, 20, GoldLight, new Vector2(x, y + 44), new Vector2(w, 28), HorizontalAlignment.Center);
        if (own >= 3) { Put(detail, "YOU HAVE ALL THREE", Thin, 20, Mist, new Vector2(x, y + 110), new Vector2(w, 28), HorizontalAlignment.Center); return; }
        void Buy(int n)
        {
            int got = 0;
            for (int i = 0; i < n; i++) if (Shop.Buy(P, d)) got++;
            if (got == 0) { Ask("Not enough DP."); return; }
            Bought(); browser!.BuildGrid(); CardDetail(d);
        }
        if (price > P.Dp) { Put(detail, "NOT ENOUGH DP", Thin, 20, Mist, new Vector2(x, y + 110), new Vector2(w, 28), HorizontalAlignment.Center); return; }
        Button(detail, "BUY ×1", new Vector2(x + w * 0.3f, y + 130), () => Buy(1), 24);
        if (3 - own > 1 && price * (3 - own) <= P.Dp) Button(detail, $"BUY ×{3 - own}", new Vector2(x + w * 0.72f, y + 130), () => Buy(3 - own), 24);
    }

    // ---------------------------------------------------------------- card backs / frames: grid + big one

    void Grid(Cosmetic kind)
    {
        bool frame = kind == Cosmetic.Frame;
        var items = Shop.All(kind).Where(it => it.Price > 0).OrderBy(it => Shop.Owns(P, it)).ThenBy(it => it.Price).ToList();
        float tw = 140, th = frame ? 140 : tw * 1.458f, gx = 36, gy = 56;
        int cols = (int)((W * 0.5f + gx) / (tw + gx)), rows = (int)((H - 260 + gy) / (th + gy));
        Paged(page, new Rect2(90, 190, W * 0.52f, H - 250), items.Count, cols, rows, new Vector2(tw + gx, th + gy), (i, host, at) =>
        {
            var it = items[i];
            bool have = Shop.Owns(P, it);
            Control pic;
            if (frame) { pic = UI.Cosmetics.Framed(Avatar, it.Key, tw); pic.Position = at; host.AddChild(pic); }
            else { Shadow(host, at, new Vector2(tw, th), 0.22f, new Color(0.12f, 0.12f, 0.15f), new Vector2(6, 10)); pic = Pic(host, UI.Cosmetics.Back(it.Key, 256), at, new Vector2(tw, th)); }
            if (have) pic.Modulate = new Color(1, 1, 1, 0.45f);
            Put(host, have ? "OWNED" : $"{it.Price:N0}", Thin, 15, Graphite, at + new Vector2(-20, th + 8), new Vector2(tw + 40, 22), HorizontalAlignment.Center);
            var hit = new Control { Position = at, Size = new Vector2(tw, th), TooltipText = it.Name };
            host.AddChild(hit);
            pic.PivotOffset = new Vector2(tw, th) / 2;
            Click(hit, () => ItemDetail(it), h => pic.Scale = h ? new Vector2(1.04f, 1.04f) : Vector2.One);
        }, new Vector2(W * 0.52f - 210, H - 50));
        ItemDetail(items.FirstOrDefault(it => !Shop.Owns(P, it)) ?? items[0]);
    }

    void ItemDetail(Shop.Item it)
    {
        ClearDetail();
        var kind = it.Kind;
        bool mat = kind == Cosmetic.Mat, frame = kind == Cosmetic.Frame, have = Shop.Owns(P, it);
        float w = mat ? W * 0.32f : frame ? H * 0.36f : H * 0.36f, h = mat ? w / UI.Cosmetics.MatAspect : frame ? w : w * 1.458f;
        var c = new Vector2(W * 0.8f, mat ? H * 0.42f : H * 0.44f);
        if (frame)
        {
            var f = UI.Cosmetics.Framed(Avatar, it.Key, w);
            f.Position = c - new Vector2(w, h) / 2; detail.AddChild(f);
        }
        else
        {
            Shadow(detail, c - new Vector2(w, h) / 2, new Vector2(w, h), 0.5f, new Color(0.35f, 0.36f, 0.45f));
            var pic = Pic(detail, mat ? UI.Cosmetics.MatPreview(it.Key) : UI.Cosmetics.Back(it.Key), c - new Vector2(w, h) / 2, new Vector2(w, h));
            if (mat) pic.Material = Kit.RoundedMat(w / h, 0.03f);
            else pic.Rotation = 0.04f;
        }
        float y = c.Y + h / 2 + 36;
        Put(detail, it.Name.ToUpperInvariant(), Title, 34, Silver, new Vector2(c.X - 400, y), new Vector2(800, 46), HorizontalAlignment.Center);
        if (have) { Put(detail, mat || kind == Cosmetic.Back ? "OWNED  ·  EQUIP IT IN THE CARD LIST" : "OWNED  ·  EQUIP IT IN OPTIONS", Thin, 18, Mist, new Vector2(c.X - 400, y + 56), new Vector2(800, 28), HorizontalAlignment.Center); return; }
        Put(detail, $"{it.Price:N0} DP", Thin, 26, GoldLight, new Vector2(c.X - 200, y + 54), new Vector2(400, 34), HorizontalAlignment.Center);
        if (it.Price > P.Dp) { Put(detail, "NOT ENOUGH DP", Thin, 18, Mist, new Vector2(c.X - 200, y + 100), new Vector2(400, 28), HorizontalAlignment.Center); return; }
        Button(detail, "BUY", new Vector2(c.X, y + 130), () => { if (Shop.Buy(P, it)) { Bought(); Show(tab); ItemDetail(it); } });
    }

    // ---------------------------------------------------------------- playmats: a price list + the mat big

    void Mats()
    {
        var mats = Shop.Mats.Where(it => it.Price > 0).OrderBy(it => Shop.Owns(P, it)).ThenBy(it => it.Price).ToList();
        int rows = (int)((H - 260) / 64);
        Paged(page, new Rect2(60, 190, W * 0.45f, H - 250), mats.Count, 1, rows, new Vector2(0, 64), (i, host, at) =>
        {
            var it = mats[i];
            bool have = Shop.Owns(P, it);
            float y = at.Y, priceX = W * 0.36f;
            var l = Put(host, it.Name.ToUpperInvariant(), Thin, 24, have ? Graphite * new Color(1, 1, 1, 0.5f) : Ink, new Vector2(20, y));
            float lx = 20 + Thin.GetStringSize(l.Text, HorizontalAlignment.Left, -1, 24).X + 16;
            for (float dx = lx; dx < priceX - 10; dx += 14) Hair(host, new Vector2(dx, y + 22), 3, Ink * new Color(1, 1, 1, 0.25f), 2);
            Put(host, have ? "OWNED" : $"{it.Price:N0}", Thin, 22, Graphite, new Vector2(priceX, y + 2), new Vector2(130, 30), HorizontalAlignment.Right);
            var hit = new Control { Position = new Vector2(0, y - 10), Size = new Vector2(priceX + 140, 50) };
            host.AddChild(hit);
            Click(hit, () => ItemDetail(it), h => l.AddThemeColorOverride("font_color", h ? GoldDeep : have ? Graphite * new Color(1, 1, 1, 0.5f) : Ink));
        }, new Vector2(W * 0.45f - 240, H - 50));
        ItemDetail(mats.FirstOrDefault(it => !Shop.Owns(P, it)) ?? mats[0]);
    }
}
