using Godot;
using PoC.Engine;
using Cosmetic = PoC.Engine.Shop.Cosmetic;

namespace PoC.Game.UI;

/// Card List (Chaos Rift sample A, locked 2026-11-08). CARDS: every card you own or can earn as a paged grid in the
/// light (search, kind, FILTERS: attribute / category / type), the chosen card large in the dark with its text.
/// CARD BACKS / PLAYMATS: what you own, click one to equip it; the rest are in the Shop.
/// `-- --demo [--tab=cards|backs|mats] [--state=filters]`
public partial class CardList : RiftMenu
{
    string tab = "cards";
    Control page = null!;
    CardBrowser? browser;
    Control? detail;
    readonly List<(string Key, Action<bool> Set)> tabs = new();
    List<CardData> cards = new();

    protected override void OnBack() => Transition.Go(this, "res://ui/MainMenu.tscn");

    protected override void Build()
    {
        Demo.Ensure();
        Rift(0.62f, 0.56f);
        cards = Db.ByIdx.Values.Where(d => (d.PackMask != 0 || P.Count(d.Idx) > 0) && Db.Canon(d.Idx) == d.Idx && d.Name.Length > 0).OrderBy(d => d.Kind).ThenBy(d => d.IsFusion).ThenBy(d => d.Name).ToList();
        Back();
        Heading("CARD LIST", new Vector2(80, 60), 46);
        float x = 420;
        foreach (var (key, label) in new[] { ("cards", "CARDS"), ("backs", "CARD BACKS"), ("mats", "PLAYMATS") })
        {
            var k = key;
            var (w, set) = Word(this, label, new Vector2(x, 78), 20, false, () => Show(k));
            tabs.Add((key, set));
            x += w + 40;
        }
        page = new Control { Size = new Vector2(W, H), MouseFilter = MouseFilterEnum.Ignore };
        AddChild(page);
        Show(Demo.Arg("tab", "cards"));
    }

    void Show(string t)
    {
        tab = t;
        foreach (var (k, set) in tabs) set(k == t);
        foreach (var c in page.GetChildren()) c.QueueFree();
        browser = null; detail = null;
        if (t == "cards") Cards(); else Cosmetics(t == "backs" ? Cosmetic.Back : Cosmetic.Mat);
    }

    // ---------------------------------------------------------------- cards

    void Cards()
    {
        var area = new Rect2(80, 170, W * 0.5f, H - 210);
        // the browser adds itself to the screen; keep it inside the page so tab switches clear it
        browser = new CardBrowser(this, area, 160, () => cards.Where(c => !c.Name.EndsWith("Token")).ToList());
        foreach (var n in GetChildren().OfType<Control>().TakeLast(2).ToList()) n.Reparent(page);
        browser.Decorate = (hit, d, size) =>
        {
            int own = P.Count(d.Idx);
            if (P.Fresh.Contains(d.Idx)) Put(hit, "NEW", Title, 13, GoldDeep, new Vector2(2, -20));
            for (int k = 0; k < Math.Min(3, own); k++) Pip(hit, new Vector2(size.X - 10 - k * 13, -8), 6);
        };
        browser.Picked = Detail;
        browser.Faded = d => P.Count(d.Idx) == 0;
        if (Demo.Arg("state") == "filters") { browser.Q.Kind = Kind.Monster; browser.Q.Attribute = "DARK"; browser.Q.Race = "Dragon"; browser.FiltersOpen = true; }
        browser.Refresh();
        var first = cards.FirstOrDefault(c => P.Count(c.Idx) > 0 && browser.Q.Match(c)) ?? cards[0];
        browser.Selected = first; browser.BuildGrid();
        Detail(first);
        Put(page, $"OWNED {cards.Count(c => P.Count(c.Idx) > 0)} / {cards.Count}", Thin, 18, Graphite, new Vector2(80, H - 40));
    }

    void Detail(CardData d)
    {
        detail?.QueueFree();
        detail = new Control { Size = new Vector2(W, H), MouseFilter = MouseFilterEnum.Ignore };
        page.AddChild(detail);
        if (P.Fresh.Remove(d.Idx)) Session.Save();
        Card(detail, d, new Vector2(W * 0.81f, H * 0.36f), H * 0.46f, 0.04f, 1f, large: true);
        float x = W * 0.66f, w = W * 0.3f, y = H * 0.64f;
        int ns = 30; while (ns > 18 && Title.GetStringSize(d.Name.ToUpperInvariant(), HorizontalAlignment.Left, -1, Z(ns)).X > w) ns -= 2;
        Put(detail, d.Name.ToUpperInvariant(), Title, ns, Silver, new Vector2(x, y + (30 - ns) / 2f), new Vector2(w, 44));
        string type = d.IsMonster ? $"{d.Attribute}  ·  {d.Race}  ·  LEVEL {d.Level}" : $"{d.Frame} {d.Kind}";
        Put(detail, type.ToUpperInvariant(), Thin, 18, Mist, new Vector2(x, y + 50));
        float ty = y + 86;
        if (d.IsMonster) { Put(detail, $"ATK {d.AtkText}    DEF {d.DefText}", Title, 24, GoldLight, new Vector2(x, ty)); ty += 44; }
        CardText(detail, d, new Vector2(x, ty), new Vector2(w, H - ty - 70), 20, light: false, min: 14);
        int own = P.Count(d.Idx);
        int inDecks = Session.Decks.Count(k => k.Copies(d.Idx) > 0);
        Put(detail, own > 0 ? $"OWNED ×{own}   ·   IN {inDecks} DECK{(inDecks == 1 ? "" : "S")}" : "NOT OWNED YET", Thin, 18, own > 0 ? Silver : Mist, new Vector2(x, H - 50));
    }

    // ---------------------------------------------------------------- card backs / playmats: equip what you own

    void Cosmetics(Cosmetic kind)
    {
        var all = Shop.All(kind).OrderByDescending(it => Shop.Owns(P, it)).ThenBy(it => it.Price).ToList();
        bool mat = kind == Cosmetic.Mat;
        float tw = mat ? 300 : 150, th = mat ? tw / UI.Cosmetics.MatAspect : tw * 1.458f, gx = mat ? 30 : 34, gy = 54;
        int cols = (int)((W * 0.52f + gx) / (tw + gx));
        var eq = Shop.Equipped(P, kind);
        int rows = Math.Max(1, (int)((H - 260 + gy) / (th + gy)));
        Paged(page, new Rect2(90, 200, W * 0.53f, H - 260), all.Count, cols, rows, new Vector2(tw + gx, th + gy), (i, host, at) =>
        {
            var it = all[i];
            bool have = Shop.Owns(P, it), on = it.Key == eq;
            Shadow(host, at, new Vector2(tw, th), 0.22f, new Color(0.12f, 0.12f, 0.15f), new Vector2(6, 10));
            var pic = Pic(host, mat ? UI.Cosmetics.MatPreview(it.Key) : UI.Cosmetics.Back(it.Key, 256), at, new Vector2(tw, th));
            if (mat) pic.Material = Kit.RoundedMat(tw / th, 0.04f);
            if (!have) pic.Modulate = new Color(1, 1, 1, 0.35f);
            Put(host, have ? it.Name.ToUpperInvariant() : "IN THE SHOP", Thin, 15, on ? GoldDeep : Graphite, at + new Vector2(-20, th + 8), new Vector2(tw + 40, 22), HorizontalAlignment.Center);
            if (on) Pip(host, at + new Vector2(tw / 2, -12), 10);
            var hit = new Control { Position = at, Size = new Vector2(tw, th), TooltipText = it.Name };
            host.AddChild(hit);
            Click(hit, () => Preview(it, kind), h => pic.SelfModulate = h ? new Color(1.08f, 1.08f, 1.08f) : Colors.White);
        }, new Vector2(W * 0.53f - 210, H - 50));
        Preview(all.First(it => it.Key == eq), kind);
    }

    void Preview(Shop.Item it, Cosmetic kind)
    {
        detail?.QueueFree();
        detail = new Control { Size = new Vector2(W, H), MouseFilter = MouseFilterEnum.Ignore };
        page.AddChild(detail);
        bool mat = kind == Cosmetic.Mat, have = Shop.Owns(P, it), on = Shop.Equipped(P, kind) == it.Key;
        float w = mat ? W * 0.3f : H * 0.4f, h = mat ? w / UI.Cosmetics.MatAspect : w * 1.458f;
        var c = new Vector2(W * 0.81f, H * 0.42f);
        Shadow(detail, c - new Vector2(w, h) / 2, new Vector2(w, h), 0.5f, new Color(0.35f, 0.36f, 0.45f));
        var pic = Pic(detail, mat ? UI.Cosmetics.MatPreview(it.Key) : UI.Cosmetics.Back(it.Key), c - new Vector2(w, h) / 2, new Vector2(w, h));
        if (mat) pic.Material = Kit.RoundedMat(w / h, 0.03f);
        Put(detail, it.Name.ToUpperInvariant(), Title, 34, Silver, new Vector2(c.X - 400, c.Y + h / 2 + 30), new Vector2(800, 46), HorizontalAlignment.Center);
        if (on) Put(detail, "EQUIPPED", Title, 20, GoldLight, new Vector2(c.X - 200, c.Y + h / 2 + 90), new Vector2(400, 30), HorizontalAlignment.Center);
        else if (have) Button(detail, "EQUIP", new Vector2(c.X, c.Y + h / 2 + 110), () => { if (Shop.Equip(P, it)) { Session.Save(); Show(tab); } });
        else Put(detail, it.Price < 0 ? "WIN A TOURNAMENT TO EARN IT" : $"{it.Price:N0} DP IN THE SHOP", Thin, 20, Mist, new Vector2(c.X - 300, c.Y + h / 2 + 90), new Vector2(600, 30), HorizontalAlignment.Center);
    }
}
