using Godot;
using PoC.Engine;
using static PoC.Game.UI.ConceptKit;

namespace PoC.Game.UI;

/// Deck Builder samples. a = Across the rift: your cards in the light, the deck in the dark (drag across the tear);
/// b = Deck list: the deck as named strips in three columns (monsters / spells / traps) in the dark, the collection as
/// a list in the light; c = Focus: the card you consider stands on the rift with ADD / REMOVE, the deck as a strip on
/// top, your collection as a ribbon below.
public partial class RiftScreens
{
    void DeckS()
    {
        switch (variant)
        {
            case "b": DeckList(); break;
            case "c": DeckFocus(); break;
            case "old": DeckAcross(); break;
            default: DeckAcross2(); break;
        }
    }

    static PoC.Engine.Deck TheDeck => Session.Playing ?? Session.Decks.First();

    static IEnumerable<CardData> Sorted(IEnumerable<int> ids) => ids.Select(CardDb.Shared.Get).OrderBy(c => c.Kind).ThenByDescending(c => c.Level).ThenBy(c => c.Name);

    void Counts(Vector2 at, Color c, Color dim)
    {
        var d = TheDeck;
        var main = d.Main.Select(CardDb.Shared.Get).ToList();
        string[] parts = { $"{d.Main.Count}", "/40", $"   MON {main.Count(x => x.IsMonster)}   SPELL {main.Count(x => x.IsSpell)}   TRAP {main.Count(x => x.IsTrap)}   EX {d.Extra.Count}   SIDE {d.Side.Count}" };
        C(this, parts[0], Title, 48, c, at);
        C(this, parts[1], Thin, 24, dim, at + new Vector2(Title.GetStringSize(parts[0], HorizontalAlignment.Left, -1, 48).X + 6, 22));
        C(this, parts[2], Thin, 20, dim, at + new Vector2(150, 26));
    }

    // a ---------------------------------------------------------------- across the rift
    void DeckAcross()
    {
        Rift(0.45f, 0.39f);
        Logo(new Vector2(W - 230, 70), 280);
        Heading("DECK BUILDER", new Vector2(70, 40), 46);
        Filters(new Vector2(70, 130), 18);
        var cards = OwnedCards().Take(16).ToList();
        float tw = 132, th = tw * 1.458f, gx = 22, gy = 24, x0 = 70, y0 = 210;
        for (int i = 0; i < cards.Count; i++)
        {
            var at = new Vector2(x0 + i % 4 * (tw + gx), y0 + i / 4 * (th + gy));
            var c = Card(cards[i], at + new Vector2(tw, th) / 2, th, 0, true, 0.6f);
            int inDeck = TheDeck.Copies(cards[i].Idx);
            for (int k = 0; k < inDeck; k++) Pip(new Vector2(at.X + tw / 2 - (inDeck - 1) * 9 + k * 18, at.Y + th + 10), 7);
        }
        // the deck in the dark: name, counts, 40 slots, fusion + side rows
        float dx = W * 0.5f;
        C(this, TheDeck.Name.ToUpperInvariant(), Title, 40, Silver, new Vector2(dx, 160));
        Counts(new Vector2(dx, 214), Silver, Mist);
        var main = Sorted(TheDeck.Main).ToList();
        float mw = 80, mh = mw * 1.458f, mg = 8;
        for (int i = 0; i < Math.Max(40, main.Count); i++)
        {
            var at = new Vector2(dx + i % 10 * (mw + mg), 300 + i / 10 * (mh + mg));
            if (i < main.Count) Ink_(CardArt.Small(main[i]), at, new Vector2(mw, mh));
            else Hair(at, mw, Silver * new Color(1, 1, 1, 0.15f));
        }
        float fy = 300 + 4 * (mh + mg) + 30;
        C(this, "FUSION", Thin, 18, Mist, new Vector2(dx, fy));
        C(this, "SIDE", Thin, 18, Mist, new Vector2(dx + 5 * (mw + mg) + 10, fy));
        var ex = Sorted(TheDeck.Extra).ToList();
        for (int i = 0; i < 4; i++)
        {
            var at = new Vector2(dx + i * (mw + mg), fy + 30);
            if (i < ex.Count) Ink_(CardArt.Small(ex[i]), at, new Vector2(mw, mh)); else Hair(at + new Vector2(0, mh), mw, Silver * new Color(1, 1, 1, 0.15f));
        }
        for (int i = 0; i < 5; i++) Hair(new Vector2(dx + (5 + i) * (mw + mg) + 10, fy + 30 + mh), mw, Silver * new Color(1, 1, 1, 0.15f));
        Pip(new Vector2(CrackX(H * 0.5f), H * 0.5f), 14);
        focusables.Add(Button("SAVE", new Vector2(W - 380, H - 60), 24));
        focusables.Add(Button("DECKS", new Vector2(W - 160, H - 60), 24));
    }

    // b ---------------------------------------------------------------- deck list in named strips
    void DeckList()
    {
        Rift(0.36f, 0.3f);
        Logo(new Vector2(W - 230, 70), 280);
        Heading("DECK BUILDER", new Vector2(60, 40), 42);
        // the chosen card + the collection as a list in the light
        var cards = OwnedCards().Where(c => c.IsMonster).Take(10).ToList();
        var pick = cards[2];
        Card(pick, new Vector2(170, 330), 270, -0.03f);
        C(this, pick.Name.ToUpperInvariant(), Title, 22, Ink, new Vector2(290, 230), new Vector2(330, 60)).AutowrapMode = TextServer.AutowrapMode.WordSmart;
        C(this, $"ATK {pick.Atk}  DEF {pick.Def}", Thin, 18, GoldDeep, new Vector2(290, 300));
        focusables.Add(Button("ADD", new Vector2(350, 380), 22));
        float y = 500;
        for (int i = 0; i < cards.Count; i++)
        {
            bool on = i == 2;
            C(this, cards[i].Name, on ? Title : Body, on ? 18 : 20, on ? GoldDeep : Ink, new Vector2(70, y), new Vector2(400, 28)).ClipText = true;
            C(this, $"{cards[i].Atk}", Thin, 16, Graphite, new Vector2(CrackX(y) - 200, y + 3), new Vector2(80, 24), HorizontalAlignment.Right);
            int own = Math.Min(3, Session.Profile.Count(cards[i].Idx));
            for (int k = 0; k < own; k++) Pip(new Vector2(CrackX(y) - 100 + k * 16, y + 14), 7);
            if (on) Pip(new Vector2(52, y + 14), 9);
            y += 58;
        }
        // the deck: three columns of strips (small art + name + count)
        C(this, TheDeck.Name.ToUpperInvariant(), Title, 40, Silver, new Vector2(W * 0.36f, 150));
        Counts(new Vector2(W * 0.36f, 206), Silver, Mist);
        var groups = new[] { ("MONSTERS", TheDeck.Main.Select(CardDb.Shared.Get).Where(c => c.IsMonster)), ("SPELLS", TheDeck.Main.Select(CardDb.Shared.Get).Where(c => c.IsSpell)), ("TRAPS", TheDeck.Main.Select(CardDb.Shared.Get).Where(c => c.IsTrap)) };
        float colW = (W * 0.6f) / 3, cx = W * 0.36f;
        foreach (var (name, list) in groups)
        {
            var g = list.GroupBy(c => c.Name).OrderByDescending(x => x.First().Level).ThenBy(x => x.Key).ToList();
            C(this, $"{name}  {list.Count()}", Title, 20, new Color(1f, 0.85f, 0.5f), new Vector2(cx, 300));
            Hair(new Vector2(cx, 332), colW - 40, Silver * new Color(1, 1, 1, 0.2f));
            float sy = 350;
            foreach (var grp in g.Take(13))
            {
                var c = grp.First();
                var art = Ink_(CardArt.Picture(c), new Vector2(cx, sy), new Vector2(44, 44));
                art.Material = Kit.RoundedMat(1, 0.12f);
                C(this, c.Name, Body, 19, Silver, new Vector2(cx + 58, sy + 9), new Vector2(colW - 140, 28)).ClipText = true;
                C(this, $"×{grp.Count()}", Thin, 20, Mist, new Vector2(cx + colW - 80, sy + 9), new Vector2(36, 28), HorizontalAlignment.Right);
                sy += 54;
            }
            cx += colW;
        }
        focusables.Add(Button("SAVE", new Vector2(W - 380, H - 60), 24));
        focusables.Add(Button("DECKS", new Vector2(W - 160, H - 60), 24));
    }

    // c ---------------------------------------------------------------- focus on one card
    void DeckFocus()
    {
        Rift(0.52f, 0.48f);
        Logo(new Vector2(W - 230, 70), 280);
        C(this, "DECK BUILDER", Title, 36, Ink, new Vector2(60, 40));
        Hair(new Vector2(62, 90), 140, GoldC);
        // the deck as a strip along the top
        var main = Sorted(TheDeck.Main).ToList();
        float sh = 92, sw = sh * 0.686f, sg = 4, sx = 60;
        for (int i = 0; i < main.Count && sx + sw < W - 60; i++, sx += sw + sg) Ink_(CardArt.Small(main[i]), new Vector2(sx, 130), new Vector2(sw, sh));
        Counts(new Vector2(60, 238), Ink, Graphite);
        C(this, TheDeck.Name.ToUpperInvariant(), Title, 30, Silver, new Vector2(W - 560, 246), new Vector2(500, 40), HorizontalAlignment.Right);
        // the card in focus, on the rift
        var owned = OwnedCards().Take(15).ToList();
        var d = owned.FirstOrDefault(c => c.Name == "Dark Magician") ?? owned[7];
        Card(d, new Vector2(W / 2, H * 0.56f), H * 0.42f, 0, true, 1.2f);
        float half = H * 0.42f * 0.343f;
        C(this, d.Name.ToUpperInvariant(), Title, 32, Ink, new Vector2(W / 2 - half - 660, H * 0.44f), new Vector2(600, 80), HorizontalAlignment.Right).AutowrapMode = TextServer.AutowrapMode.WordSmart;
        C(this, $"IN DECK ×{TheDeck.Copies(d.Idx)}   ·   OWNED ×{Session.Profile.Count(d.Idx)}", Thin, 20, Graphite, new Vector2(W / 2 - half - 660, H * 0.44f + 90), new Vector2(600, 30), HorizontalAlignment.Right);
        focusables.Add(Button("ADD", new Vector2(W / 2 - half - 150, H * 0.62f), 28));
        focusables.Add(Button("REMOVE", new Vector2(W / 2 + half + 170, H * 0.62f), 28));
        var t = C(this, d.TcgDesc ?? d.Desc, Body, 20, Mist, new Vector2(W / 2 + half + 60, H * 0.42f), new Vector2(560, 160));
        t.AutowrapMode = TextServer.AutowrapMode.WordSmart; t.CustomMinimumSize = new Vector2(560, 0); t.Size = new Vector2(560, 160);
        // collection ribbon
        float rh = 140, rw = rh * 0.686f, gap = 16, total = owned.Count * (rw + gap) - gap, x0 = (W - total) / 2;
        for (int i = 0; i < owned.Count; i++)
        {
            var c = Card(owned[i], new Vector2(x0 + i * (rw + gap) + rw / 2, H - 100), rh, 0, true, 0.5f);
            if (owned[i] != d) c.Modulate = new Color(1, 1, 1, 0.65f);
        }
    }
}
