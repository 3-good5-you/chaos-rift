using Godot;
using PoC.Engine;
using static PoC.Game.UI.ConceptKit;

namespace PoC.Game.UI;

/// Shop samples. a = Altar: a grid of card backs in the light, the chosen one floating large in the dark with BUY;
/// b = Carousel: single cards for sale ride across the rift, the centre one large; c = Boutique: playmats listed in
/// the light like a price list, the chosen mat shown big in the dark.
public partial class RiftScreens
{
    static readonly string[] ShopTabs = { "CARDS", "CARD BACKS", "PLAYMATS", "FRAMES" };

    void ShopS()
    {
        switch (variant)
        {
            case "b": ShopCarousel(); break;
            case "c": ShopBoutique(); break;
            case "d": ShopCardsGrid(); break;
            case "e": ShopCardsList(); break;
            default: ShopAltar(); break;
        }
    }

    void ShopHeader(int tab, float x, float y, bool tabsOnLight = true)
    {
        Heading("SHOP", new Vector2(x, y));
        float tx = x;
        for (int i = 0; i < ShopTabs.Length; i++)
        {
            float w = Title.GetStringSize(ShopTabs[i], HorizontalAlignment.Left, -1, 19).X;
            var at = new Vector2(tx, y + 104);
            var l = T(this, ShopTabs[i], Title, 19, at, default, HorizontalAlignment.Left, i == tab ? 1f : 0.5f);
            if (i == tab) { l.Mat().P("lit", 1f); Pip(new Vector2(tx + w / 2, at.Y + 36), 8); }
            tx += w + 30;
        }
    }

    void Dp(Vector2 at, Color c) => C(this, $"{Session.Profile.Dp:N0} DP", Title, 30, c, at, new Vector2(300, 40), HorizontalAlignment.Right);

    // a ---------------------------------------------------------------- altar: grid in the light, the chosen back in the dark
    void ShopAltar()
    {
        Rift(0.56f, 0.5f);
        Logo(new Vector2(W - 270, 90), 360);
        ShopHeader(1, 80, 50);
        var backs = global::PoC.Engine.Shop.Backs.Skip(10).Take(12).ToArray();
        float tw = 150, th = tw * 1.458f, gx = 40, gy = 56, x0 = 90, y0 = 270;
        int sel = 4;
        for (int i = 0; i < backs.Length; i++)
        {
            var at = new Vector2(x0 + i % 4 * (tw + gx), y0 + i / 4 * (th + gy));
            Shadow(this, at, new Vector2(tw, th), 0.22f, new Color(0.12f, 0.12f, 0.15f), new Vector2(6, 10));
            var r = Ink_(Cosmetics.Back(backs[i].Key, 256), at, new Vector2(tw, th));
            if (i == sel) Pip(new Vector2(at.X + tw / 2, at.Y + th + 20), 9);
            else r.Modulate = new Color(1, 1, 1, 0.85f);
            C(this, $"{backs[i].Price:N0}", Thin, 18, Graphite, new Vector2(at.X, at.Y + th + 6), new Vector2(tw, 24), HorizontalAlignment.Center);
        }
        var it = backs[sel];
        float h = H * 0.56f, w = h * 0.686f;
        var c = new Vector2(W * 0.77f, H * 0.52f);
        var root = new Control { Position = c - new Vector2(w, h) / 2, Size = new Vector2(w, h), PivotOffset = new Vector2(w, h) / 2, Rotation = 0.05f };
        AddChild(root);
        Shadow(root, Vector2.Zero, new Vector2(w, h), 0.5f, new Color(0.35f, 0.36f, 0.45f));
        Ink_(Cosmetics.Back(it.Key), Vector2.Zero, new Vector2(w, h), root);
        C(this, it.Name.ToUpperInvariant(), Title, 40, Silver, new Vector2(c.X - 400, c.Y + h / 2 + 40), new Vector2(800, 50), HorizontalAlignment.Center);
        C(this, $"{it.Price:N0} DP", Thin, 28, new Color(1f, 0.85f, 0.5f), new Vector2(c.X - 200, c.Y + h / 2 + 96), new Vector2(400, 36), HorizontalAlignment.Center);
        focusables.Add(Button("BUY", new Vector2(c.X, H - 70)));
        Dp(new Vector2(W - 380, 190), Silver);
    }

    // b ---------------------------------------------------------------- carousel across the rift
    void ShopCarousel()
    {
        Rift(0.53f, 0.47f);
        Logo(new Vector2(W - 270, 90), 360);
        ShopHeader(0, 80, 50);
        Dp(new Vector2(W - 380, 190), Silver);
        var db = CardDb.Shared;
        var forSale = new[] { "Summoned Skull", "Dark Magician Girl", "Black Luster Soldier - Envoy of the Beginning", "Red-Eyes B. Dragon", "Blue-Eyes White Dragon", "Jinzo", "Magician of Black Chaos" }
            .Where(n => db.ByName.ContainsKey(n)).Select(Named).ToList();
        int mid = 3;
        float cy = H * 0.55f;
        for (int k = -3; k <= 3; k++)
        {
            int i = mid + k; if (i < 0 || i >= forSale.Count) continue;
            int a = Math.Abs(k);
            float h = a == 0 ? H * 0.56f : H * (0.4f - a * 0.05f);
            float x = W / 2 + Math.Sign(k) * (a == 0 ? 0 : 200 + a * 230f) - (a == 0 ? 0 : 0);
            var card = Card(forSale[i], new Vector2(x, cy + a * 14), h, k * 0.05f);
            if (a > 0) card.Modulate = new Color(1, 1, 1, 1f - a * 0.22f);
        }
        // order: centre on top
        var centre = GetChildren().OfType<Control>().Last(c => c.Size.Y > H * 0.5f);
        MoveChild(centre, GetChildCount() - 1);
        var d = forSale[mid];
        T(this, d.Name.ToUpperInvariant(), Title, 38, new Vector2(W / 2 - 600, H - 160), new Vector2(1200, 50), HorizontalAlignment.Center);
        T(this, $"{(global::PoC.Engine.Shop.Price(d)):N0} DP   ·   OWNED ×{Session.Profile.Count(d.Idx)}", Thin, 24, new Vector2(W / 2 - 600, H - 108), new Vector2(1200, 34), HorizontalAlignment.Center, 0.8f);
        focusables.Add(Button("BUY", new Vector2(W / 2, H - 46), 26));
        Pip(new Vector2(W / 2 - 700, cy), 14); Pip(new Vector2(W / 2 + 700, cy), 14);
    }

    // c ---------------------------------------------------------------- boutique: playmat price list + big preview
    void ShopBoutique()
    {
        Rift(0.5f, 0.42f);
        Logo(new Vector2(W - 270, 90), 360);
        ShopHeader(2, 80, 50);
        var mats = global::PoC.Engine.Shop.Mats.Take(11).ToArray();
        float y = 270; int sel = 5;
        for (int i = 0; i < mats.Length; i++)
        {
            var row = new Vector2(80, y);
            bool on = i == sel;
            C(this, mats[i].Name.ToUpperInvariant(), on ? Title : Thin, 26, on ? GoldDeep : Ink, row);
            float lineX = 80 + Title.GetStringSize(mats[i].Name.ToUpperInvariant(), HorizontalAlignment.Left, -1, 26).X + 16;
            float priceX = CrackX(y) - 230;
            // dotted leader to the price, like a menu card
            for (float dx = lineX; dx < priceX - 10; dx += 14) Hair(new Vector2(dx, y + 22), 3, Ink * new Color(1, 1, 1, 0.3f), null, 2);
            C(this, $"{mats[i].Price:N0}", Thin, 24, on ? GoldDeep : Graphite, new Vector2(priceX, y + 2), new Vector2(120, 30), HorizontalAlignment.Right);
            if (on) Pip(new Vector2(60, y + 18), 10);
            y += 74;
        }
        var it = mats[sel];
        var tex = Cosmetics.MatPreview(it.Key);
        float pw = W * 0.42f, ph = tex != null ? pw * tex.GetHeight() / tex.GetWidth() : pw * 0.6f;
        var at = new Vector2(W * 0.54f, H * 0.25f);
        Shadow(this, at, new Vector2(pw, ph), 0.5f, new Color(0.35f, 0.36f, 0.45f));
        if (tex != null)
        {
            var r = Ink_(tex, at, new Vector2(pw, ph));
            r.Material = Kit.RoundedMat(pw / ph, 0.03f);
        }
        C(this, it.Name.ToUpperInvariant(), Title, 44, Silver, new Vector2(at.X, at.Y + ph + 40));
        C(this, "A playmat for your side of the field. Any mat goes with any card back.", Body, 22, Mist, new Vector2(at.X, at.Y + ph + 104));
        C(this, $"{it.Price:N0} DP", Thin, 32, new Color(1f, 0.85f, 0.5f), new Vector2(at.X + pw - 300, at.Y + ph + 44), new Vector2(300, 40), HorizontalAlignment.Right);
        focusables.Add(Button("BUY", new Vector2(at.X + pw - 110, at.Y + ph + 190)));
        focusables.Add(Button("PREVIEW", new Vector2(at.X + pw - 340, at.Y + ph + 190)));
        Dp(new Vector2(W - 380, 190), Silver);
    }
}
