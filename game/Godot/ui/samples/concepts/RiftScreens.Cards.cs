using Godot;
using PoC.Engine;
using static PoC.Game.UI.ConceptKit;

namespace PoC.Game.UI;

/// Card List samples. a = Binder: your cards in the light, the chosen one large in the dark with its text;
/// b = Spotlight: the chosen card stands ON the rift, its stats in the light, its text in the dark, a ribbon of cards below;
/// c = Archive: a narrow light strip of filters, the collection as a wide dark wall.
public partial class RiftScreens
{
    static readonly string[] Kinds = { "ALL", "MONSTERS", "SPELLS", "TRAPS" };

    void CardsS()
    {
        switch (variant)
        {
            case "b": CardsSpotlight(); break;
            case "c": CardsArchive(); break;
            case "old": CardsBinder(); break;
            default: CardsBinder2(); break;
        }
    }

    List<CardData> Pool(int n) => OwnedCards().Where(c => c.IsMonster).Take(n / 2).Concat(OwnedCards().Where(c => !c.IsMonster).Take(n - n / 2)).ToList();

    void Filters(Vector2 at, int size = 22, bool vertical = false)
    {
        float x = at.X, y = at.Y;
        for (int i = 0; i < Kinds.Length; i++)
        {
            var l = T(this, Kinds[i], Title, size, new Vector2(x, y), default, HorizontalAlignment.Left, i == 0 ? 1f : 0.55f);
            float w = Title.GetStringSize(Kinds[i], HorizontalAlignment.Left, -1, size).X;
            if (i == 0) { l.Mat().P("lit", 1f); Pip(vertical ? new Vector2(x - 20, y + size * 0.7f) : new Vector2(x + w / 2, y + size * 1.6f), 8); }
            if (vertical) y += size * 2.2f; else x += w + 40;
        }
    }

    void SearchLine(Vector2 at, float w, Color c)
    {
        C(this, "SEARCH", Thin, 20, c * new Color(1, 1, 1, 0.5f), at);
        Hair(at + new Vector2(0, 34), w, c * new Color(1, 1, 1, 0.3f));
    }

    void CardText(CardData d, Vector2 at, float w, Color main, Color sub)
    {
        C(this, d.Name.ToUpperInvariant(), Title, 34, main, at, new Vector2(w, 46));
        string type = d.IsMonster ? $"{d.Attribute}  ·  {d.Race}  ·  LEVEL {d.Level}" : $"{d.Kind} {d.Frame}".ToUpperInvariant();
        C(this, type.ToUpperInvariant(), Thin, 20, sub, at + new Vector2(0, 54));
        if (d.IsMonster) C(this, $"ATK {d.Atk}    DEF {d.Def}", Title, 26, new Color(1f, 0.85f, 0.5f) * (main == Ink ? GoldDeep : Colors.White), at + new Vector2(0, 92));
        var t = C(this, d.TcgDesc ?? d.Desc, Body, 21, sub, at + new Vector2(0, d.IsMonster ? 140 : 100), new Vector2(w, 260));
        t.AutowrapMode = TextServer.AutowrapMode.WordSmart; t.CustomMinimumSize = new Vector2(w, 0); t.Size = new Vector2(w, 260);
        C(this, $"OWNED ×{Session.Profile.Count(d.Idx)}", Thin, 20, sub, at + new Vector2(0, (d.IsMonster ? 140 : 100) + 270));
    }

    // a ---------------------------------------------------------------- binder
    void CardsBinder()
    {
        Rift(0.62f, 0.56f);
        Logo(new Vector2(W - 270, 90), 360);
        Heading("CARD LIST", new Vector2(80, 50));
        Filters(new Vector2(80, 160));
        SearchLine(new Vector2(700, 160), 300, Ink);
        var cards = Pool(15);
        float tw = 168, th = tw * 1.458f, gx = 26, gy = 30, x0 = 80, y0 = 250;
        int sel = 6;
        for (int i = 0; i < cards.Count; i++)
        {
            var at = new Vector2(x0 + i % 5 * (tw + gx), y0 + i / 5 * (th + gy));
            var c = Card(cards[i], at + new Vector2(tw, th) / 2, th, 0, true, i == sel ? 1.4f : 0.5f);
            if (i == sel) { c.Scale = new Vector2(1.07f, 1.07f); Pip(new Vector2(at.X + tw / 2, at.Y + th + 16), 9); }
        }
        var d = cards[sel];
        Card(d, new Vector2(W * 0.81f, H * 0.4f), H * 0.46f, 0.04f);
        CardText(d, new Vector2(W * 0.66f, H * 0.68f), W * 0.3f, Silver, Mist);
        C(this, $"{OwnedCards().Count} CARDS  ·  page 1 / 33", Thin, 20, Graphite, new Vector2(80, H - 60));
    }

    // b ---------------------------------------------------------------- spotlight on the rift
    void CardsSpotlight()
    {
        Rift(0.52f, 0.48f);
        Logo(new Vector2(W - 270, 90), 360);
        Heading("CARD LIST", new Vector2(80, 50));
        var cards = Pool(13);
        int si = cards.FindIndex(c => c.IsMonster && c.Atk >= 2000); if (si < 0) si = 6;
        var d = cards[si];
        Card(d, new Vector2(W * 0.5f, H * 0.43f), H * 0.58f, 0, true, 1.2f);
        // stats in the light (right-aligned to the card), text in the dark
        float lx = W * 0.5f - H * 0.58f * 0.343f - 60;
        C(this, d.Name.ToUpperInvariant(), Title, 40, Ink, new Vector2(lx - 640, H * 0.2f), new Vector2(640, 110), HorizontalAlignment.Right).AutowrapMode = TextServer.AutowrapMode.WordSmart;
        C(this, d.IsMonster ? $"{d.Attribute} · {d.Race}".ToUpperInvariant() : $"{d.Frame} {d.Kind}".ToUpperInvariant(), Thin, 22, Graphite, new Vector2(lx - 640, H * 0.2f + 120), new Vector2(640, 30), HorizontalAlignment.Right);
        if (d.IsMonster)
        {
            C(this, $"LEVEL {d.Level}", Title, 28, Ink, new Vector2(lx - 640, H * 0.2f + 170), new Vector2(640, 40), HorizontalAlignment.Right);
            C(this, $"{d.Atk}", Title, 64, GoldDeep, new Vector2(lx - 640, H * 0.2f + 230), new Vector2(640, 80), HorizontalAlignment.Right);
            C(this, "ATTACK", Thin, 18, Graphite, new Vector2(lx - 640, H * 0.2f + 312), new Vector2(640, 26), HorizontalAlignment.Right);
            C(this, $"{d.Def}", Title, 64, Ink, new Vector2(lx - 640, H * 0.2f + 350), new Vector2(640, 80), HorizontalAlignment.Right);
            C(this, "DEFENSE", Thin, 18, Graphite, new Vector2(lx - 640, H * 0.2f + 432), new Vector2(640, 26), HorizontalAlignment.Right);
        }
        float rx = W * 0.5f + H * 0.58f * 0.343f + 60;
        var t = C(this, d.TcgDesc ?? d.Desc, Body, 23, Mist, new Vector2(rx, H * 0.28f), new Vector2(W - rx - 90, 400));
        t.AutowrapMode = TextServer.AutowrapMode.WordSmart; t.CustomMinimumSize = new Vector2(W - rx - 90, 0); t.Size = new Vector2(W - rx - 90, 400);
        C(this, $"OWNED ×{Session.Profile.Count(d.Idx)}   ·   IN DECKS ×1", Thin, 22, Silver, new Vector2(rx, H * 0.28f + 300));
        // ribbon of cards along the bottom, the chosen one lifted
        float rh = 150, rw = rh * 0.686f, gap = 18;
        float total = cards.Count * (rw + gap) - gap, x0 = (W - total) / 2;
        for (int i = 0; i < cards.Count; i++)
        {
            var c = Card(cards[i], new Vector2(x0 + i * (rw + gap) + rw / 2, H - 120 - (i == si ? 18 : 0)), rh, 0, true, 0.6f);
            if (i != si) c.Modulate = new Color(1, 1, 1, 0.7f);
        }
        Pip(new Vector2(x0 + si * (rw + gap) + rw / 2, H - 22), 9);
        Filters(new Vector2(80, 170), 20);
    }

    // c ---------------------------------------------------------------- archive: a narrow light strip, a wide dark wall
    void CardsArchive()
    {
        Rift(0.24f, 0.18f);
        Logo(new Vector2(W - 250, 84), 320);
        C(this, "CARD", Title, 50, Ink, new Vector2(60, 50));
        C(this, "LIST", Title, 50, Ink, new Vector2(60, 110));
        Hair(new Vector2(62, 182), 120, GoldC);
        Filters(new Vector2(80, 260), 22, vertical: true);
        float y = 480;
        foreach (var a in new[] { "DARK", "LIGHT", "EARTH", "WATER", "FIRE", "WIND" })
        {
            C(this, a, Thin, 18, Graphite * new Color(1, 1, 1, 0.75f), new Vector2(80, y)); y += 38;
        }
        C(this, "LEVEL  1 – 12", Thin, 18, Graphite, new Vector2(80, y + 20));
        SearchLine(new Vector2(80, y + 80), 220, Ink);
        var cards = Pool(21);
        float x0 = W * 0.27f, tw = 168, th = tw * 1.458f, gx = 34, gy = 46, y0 = 230;
        int sel = 9;
        for (int i = 0; i < cards.Count; i++)
        {
            var at = new Vector2(x0 + i % 7 * (tw + gx), y0 + i / 7 * (th + gy));
            var c = Card(cards[i], at + new Vector2(tw, th) / 2, th, 0, true, i == sel ? 1.3f : 0.7f);
            if (i == sel) { c.Scale = new Vector2(1.12f, 1.12f); c.ZIndex = 1; }
            else c.Modulate = new Color(0.85f, 0.85f, 0.88f);
        }
        var d = cards[sel];
        C(this, d.Name.ToUpperInvariant(), Title, 30, Silver, new Vector2(x0, H - 92));
        C(this, d.IsMonster ? $"ATK {d.Atk}  ·  DEF {d.Def}  ·  OWNED ×{Session.Profile.Count(d.Idx)}" : $"OWNED ×{Session.Profile.Count(d.Idx)}", Thin, 22, new Color(1f, 0.85f, 0.5f), new Vector2(x0, H - 50));
        C(this, "ENTER  ·  READ THE CARD", Thin, 18, Mist, new Vector2(W - 400, H - 50), new Vector2(330, 26), HorizontalAlignment.Right);
    }
}
