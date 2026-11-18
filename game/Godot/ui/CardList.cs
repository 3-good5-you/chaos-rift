using Godot;
using PoC.Engine;
using Cosmetic = PoC.Engine.Shop.Cosmetic;

namespace PoC.Game.UI;

/// COLLECTION (2026-11-17: the Card List and the Shop in one screen). Tabs as one pill: CARDS / CARD BACKS / PLAYMATS /
/// FRAMES. CARDS: every card you own or can buy as a paged grid in the light (search, kind, FILTERS, sort incl. PRICE),
/// the chosen card large in the dark with its text, what you own, and BUY right there. Cosmetics: a grid of all of
/// them (unowned faded with the price), the chosen one large: EQUIP if yours, BUY if not. DP top right.
/// `-- --demo [--tab=cards|backs|mats|frames] [--state=filters]`
public partial class CardList : RiftMenu
{
    static readonly (string Key, string Label)[] Tabs = { ("cards", "CARDS"), ("backs", "CARD BACKS"), ("mats", "PLAYMATS"), ("frames", "FRAMES") };
    string tab = "cards";
    Control page = null!, bar = null!;
    CardBrowser? browser;
    Control? detail;
    Label dp = null!;
    List<CardData> cards = new();

    protected override void OnBack() => Transition.Go(this, "res://ui/MainMenu.tscn");

    protected override void Build()
    {
        Demo.Ensure();
        Rift(0.62f, 0.56f);
        // the shop sells every collectable card (Shop.ForSale) plus what you own
        cards = Db.ByIdx.Values.Where(d => (d.PackMask != 0 || P.Seen.Contains(d.Idx) || P.Count(d.Idx) > 0) && Db.Canon(d.Idx) == d.Idx && d.Name.Length > 0 && !d.Name.EndsWith("Token"))
            .OrderBy(d => d.Kind).ThenBy(d => d.IsFusion).ThenBy(d => d.Name).ToList();
        Back();
        bar = new Control { MouseFilter = MouseFilterEnum.Ignore };
        AddChild(bar);
        dp = Put(this, "", Title, 30, GoldLight, new Vector2(W - 380, 60), new Vector2(320, 40), HorizontalAlignment.Right);
        page = new Control { Size = new Vector2(W, H), MouseFilter = MouseFilterEnum.Ignore };
        AddChild(page);
        Show(Demo.Arg("tab", "cards"));
    }

    void Show(string t)
    {
        tab = t;
        foreach (var c in bar.GetChildren()) c.QueueFree();
        int on = Math.Max(0, Array.FindIndex(Tabs, x => x.Key == t));
        Pill(bar, Tabs.Select(x => x.Label).ToArray(), on, new Vector2(80, 54), i => Show(Tabs[i].Key), CrackX(80) - 140, 17);
        foreach (var c in page.GetChildren()) c.QueueFree();
        browser = null; detail = null;
        Money();
        if (t == "cards") Cards(); else Cosmetics(t switch { "backs" => Cosmetic.Back, "mats" => Cosmetic.Mat, _ => Cosmetic.Frame });
    }

    void Money() => dp.Text = Lang.F("{0:N0} DP", P.Dp);

    void Bought()
    {
        Session.Save();
        UiSound(true);
        Flash("BOUGHT");
        Money();
    }

    // ---------------------------------------------------------------- cards

    void Cards()
    {
        var area = new Rect2(80, 170, W * 0.5f, H - 210);
        // the browser adds itself to the screen; keep it inside the page so tab switches clear it
        browser = new CardBrowser(this, area, 160, () => cards, new[] { "NAME", "PRICE", "ATK", "DEF", "LEVEL" });
        foreach (var n in GetChildren().OfType<Control>().TakeLast(2).ToList()) n.Reparent(page);
        browser.Decorate = (hit, d, size) =>
        {
            int own = P.Count(d.Idx);
            if (P.Fresh.Contains(d.Idx)) NewMark.Tag(hit, size, seen: () => { if (P.Fresh.Remove(d.Idx)) Session.Save(); });
            for (int k = 0; k < Math.Min(3, own); k++) Pip(hit, new Vector2(size.X - 10 - k * 13, -8), 6);
        };
        browser.Picked = Detail;
        browser.Faded = d => P.Count(d.Idx) == 0;
        if (Demo.Arg("state") == "filters") { browser.Q.Kind = Kind.Monster; browser.Q.Attribute = "DARK"; browser.Q.Race = "Dragon"; browser.FiltersOpen = true; }
        browser.Refresh();
        var first = cards.FirstOrDefault(c => P.Count(c.Idx) > 0 && browser.Q.Match(c)) ?? cards[0];
        browser.Selected = first; browser.BuildGrid();
        Detail(first);
        Put(page, Lang.F("OWNED {0} / {1}", cards.Count(c => P.Count(c.Idx) > 0), cards.Count), Thin, 18, Graphite, new Vector2(80, H - 40));
    }

    void Detail(CardData d)
    {
        detail?.QueueFree();
        detail = new Control { Size = new Vector2(W, H), MouseFilter = MouseFilterEnum.Ignore };
        page.AddChild(detail);
        if (P.Fresh.Remove(d.Idx)) Session.Save();
        Card(detail, d, new Vector2(W * 0.81f, H * 0.34f), H * 0.44f, 0.04f, 1f, large: true);
        float x = W * 0.66f, w = W * 0.3f, y = H * 0.6f;
        int ns = 30; while (ns > 18 && Title.GetStringSize(Lang.Tr(d.Name.ToUpperInvariant()), HorizontalAlignment.Left, -1, Z(ns)).X > w) ns -= 2;
        Put(detail, d.Name.ToUpperInvariant(), Title, ns, Silver, new Vector2(x, y + (30 - ns) / 2f), new Vector2(w, 44));
        string type = d.IsMonster ? $"{Lang.Tr(d.Attribute)}  ·  {Lang.Tr(d.Race)}  ·  {Lang.Tr("LEVEL")} {d.Level}" : $"{Lang.Tr(d.Frame)} {Lang.Tr(d.Kind.ToString())}";
        Put(detail, type.ToUpperInvariant(), Thin, 18, Mist, new Vector2(x, y + 50));
        float ty = y + 86;
        if (d.IsMonster) { Put(detail, $"{Lang.Tr("ATK")} {d.AtkText}    {Lang.Tr("DEF")} {d.DefText}", Title, 24, GoldLight, new Vector2(x, ty)); ty += 44; }
        CardText(detail, d, new Vector2(x, ty), new Vector2(w, H - ty - 160), 20, light: false, min: 14);
        int own = P.Count(d.Idx), price = Shop.Price(d);
        string owned = own > 0 ? Lang.F("OWNED ×{0}", own) : Lang.Tr("NOT OWNED YET");   // no "in N decks" (user 2026-11-17)
        Fit(detail, owned, Thin, 18, 12, own > 0 ? Silver : Mist, new Rect2(x, H - 150, w * 0.64f, 26)).HorizontalAlignment = HorizontalAlignment.Left;
        // buying, right here (was the Shop's CARDS tab): the price on the owned line, the buttons under it
        bool sold = d.PackMask != 0 || P.Seen.Contains(d.Idx);
        if (!sold) return;
        if (own >= 3) { Put(detail, "YOU HAVE ALL THREE", Thin, 18, Mist, new Vector2(x, H - 96)); return; }
        Fit(detail, Lang.F("{0:N0} DP", price), Title, 22, 14, GoldLight, new Rect2(x + w * 0.66f, H - 156, W - 24 - x - w * 0.66f, 36)).HorizontalAlignment = HorizontalAlignment.Right;
        if (price > P.Dp) { Put(detail, "NOT ENOUGH DP", Thin, 18, Mist, new Vector2(x, H - 96)); return; }
        // one BUY, one copy per press (user 2026-11-17: two buttons were confusing)
        var buy = Button(detail, "BUY", new Vector2(x + 100, H - 71), () =>
        {
            if (!Shop.Buy(P, d)) { Ask("Not enough DP."); return; }
            Bought(); browser!.BuildGrid(); Detail(d);
        }, 20);
        buy.Position = new Vector2(x, H - 108);
    }

    // ---------------------------------------------------------------- card backs / playmats / frames: equip or buy

    void Cosmetics(Cosmetic kind)
    {
        var all = Shop.All(kind).OrderByDescending(it => Shop.Owns(P, it)).ThenBy(it => !Shop.Owns(P, it) && !Shop.Sold(it)).ThenBy(it => it.Price).ToList();   // unowned prizes after what's for sale
        bool mat = kind == Cosmetic.Mat, frame = kind == Cosmetic.Frame;
        float tw = mat ? 300 : frame ? 130 : 150, th = mat ? tw / UI.Cosmetics.MatAspect : frame ? tw : tw * 1.458f, gx = mat ? 30 : 34, gy = 54;
        int cols = (int)((W * 0.52f + gx) / (tw + gx));
        var eq = Shop.Equipped(P, kind);
        int rows = Math.Max(1, (int)((H - 260 + gy) / (th + gy)));
        Paged(page, new Rect2(90, 200, W * 0.53f, H - 260), all.Count, cols, rows, new Vector2(tw + gx, th + gy), (i, host, at) =>
        {
            var it = all[i];
            bool have = Shop.Owns(P, it), on = it.Key == eq;
            Control pic;
            if (frame) { pic = UI.Cosmetics.Framed(Avatar, it.Key, tw); pic.Position = at; host.AddChild(pic); }
            else
            {
                Shadow(host, at, new Vector2(tw, th), 0.22f, new Color(0.12f, 0.12f, 0.15f), new Vector2(6, 10));
                var tex = Pic(host, mat ? UI.Cosmetics.MatPreview(it.Key) : UI.Cosmetics.Back(it.Key, 256), at, new Vector2(tw, th));
                if (mat) tex.Material = Kit.RoundedMat(tw / th, 0.04f);
                pic = tex;
            }
            if (!have) pic.Modulate = new Color(1, 1, 1, 0.4f);
            string label = have || !Shop.Sold(it) ? Lang.Cosmetic(it.Name).ToUpperInvariant() : Lang.F("{0:N0} DP", it.Price);
            Fit(host, label, Thin, 15, 10, on ? GoldDeep : have ? Graphite : GoldDeep, new Rect2(at + new Vector2(-gx / 2 + 2, th + 8), new Vector2(tw + gx - 4, 22)));
            if (on) Pip(host, at + new Vector2(tw / 2, -12), 10);
            var hit = new Control { Position = at, Size = new Vector2(tw, th), TooltipText = Lang.Cosmetic(it.Name) };
            host.AddChild(hit);
            pic.PivotOffset = new Vector2(tw, th) / 2;
            Click(hit, () => Preview(it, kind), h => pic.Scale = h ? new Vector2(1.04f, 1.04f) : Vector2.One);
        }, new Vector2(W * 0.53f - 210, H - 50));
        Preview(all.First(it => it.Key == eq), kind);
    }

    void Preview(Shop.Item it, Cosmetic kind)
    {
        detail?.QueueFree();
        detail = new Control { Size = new Vector2(W, H), MouseFilter = MouseFilterEnum.Ignore };
        page.AddChild(detail);
        bool mat = kind == Cosmetic.Mat, frame = kind == Cosmetic.Frame, have = Shop.Owns(P, it), on = Shop.Equipped(P, kind) == it.Key;
        float w = mat ? W * 0.3f : frame ? H * 0.34f : H * 0.4f, h = mat ? w / UI.Cosmetics.MatAspect : frame ? w : w * 1.458f;
        var c = new Vector2(W * 0.81f, H * 0.42f);
        if (frame) { var f = UI.Cosmetics.Framed(Avatar, it.Key, w); f.Position = c - new Vector2(w, h) / 2; detail.AddChild(f); }
        else
        {
            Shadow(detail, c - new Vector2(w, h) / 2, new Vector2(w, h), 0.5f, new Color(0.35f, 0.36f, 0.45f));
            var pic = Pic(detail, mat ? UI.Cosmetics.MatPreview(it.Key) : UI.Cosmetics.Back(it.Key), c - new Vector2(w, h) / 2, new Vector2(w, h));
            if (mat) pic.Material = Kit.RoundedMat(w / h, 0.03f);
        }
        float bw = (W - CrackX(c.Y + h / 2) - 60) * 2;   // the text box, centred on the picture, kept in the dark
        var box = new Rect2(c.X - bw / 2, c.Y + h / 2 + 30, bw, 46);
        Fit(detail, Lang.Cosmetic(it.Name).ToUpperInvariant(), Title, 34, 18, Silver, box);
        var below = new Rect2(box.Position + new Vector2(0, 60), new Vector2(bw, 30));
        if (on) Fit(detail, "EQUIPPED", Title, 20, 12, GoldLight, below);
        else if (have) Button(detail, "EQUIP", new Vector2(c.X, below.Position.Y + 20), () => { if (Shop.Equip(P, it)) { Session.Save(); Show(tab); Preview(it, kind); } });
        // prizes are earned, never sold: say where from
        else if (Shop.EarnedBy(it) is { } a) Fit(detail, Lang.F("ACHIEVEMENT: {0}", Lang.Tr(a.Name)), Thin, 20, 12, Mist, below);
        else if (it.Price < 0) Fit(detail, "WIN A TOURNAMENT TO EARN IT", Thin, 20, 12, Mist, below);
        else
        {
            Fit(detail, Lang.F("{0:N0} DP", it.Price), Thin, 26, 14, GoldLight, below);
            if (it.Price > P.Dp) Fit(detail, "NOT ENOUGH DP", Thin, 18, 12, Mist, new Rect2(below.Position + new Vector2(0, 46), below.Size));
            else Button(detail, "BUY", new Vector2(c.X, below.Position.Y + 84), () => { if (Shop.Buy(P, it)) { Bought(); Show(tab); Preview(it, kind); } });
        }
    }
}
