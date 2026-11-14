using Godot;
using PoC.Engine;

namespace PoC.Game.UI;

/// Your Decks (Chaos Rift sample A, locked 2026-11-08): between the main menu and the Deck Builder. Your decks as
/// cases (cover card on two card backs) in the light, plus NEW DECK; IMPORT bottom left. The chosen deck opens up in
/// the dark: cover, name, IN USE, Main / Side / Fusion counts, ban-list legality, EDIT DECK and USE / COPY / RENAME /
/// EXPORT / DELETE. Pointing at a deck shows it on the right; clicking it opens the Deck Builder.
public partial class DeckSelect : RiftMenu
{
    static Deck? remembered;
    Deck? sel;
    Control list = null!, detail = null!;

    protected override void OnBack()
    {
        if (toast != null) { CloseToast(); return; }
        Transition.Go(this, "res://ui/MainMenu.tscn");
    }

    protected override void Build()
    {
        Demo.Ensure();
        Rift(0.56f, 0.5f);
        Back();
        Heading("YOUR DECKS", new Vector2(80, 60), 46);
        list = new Control { Size = new Vector2(W, H), MouseFilter = MouseFilterEnum.Ignore };
        detail = new Control { Size = new Vector2(W, H), MouseFilter = MouseFilterEnum.Ignore };
        AddChild(list); AddChild(detail);
        Session.Decks.RemoveAll(d => d != Session.Playing && d.Main.Count + d.Extra.Count + d.Side.Count == 0);   // left by an old bug (NEW DECK saved before any card)
        sel = remembered != null && Session.Decks.Contains(remembered) ? remembered : Session.Playing ?? Session.Decks.FirstOrDefault();
        var imp = Put(this, "IMPORT DECK FILE", Title, 18, GoldDeep, new Vector2(110, H - 96));
        Click(imp, Import, h => imp.AddThemeColorOverride("font_color", h ? Ink : GoldDeep));
        Put(this, ".ydc (the original game)  ·  .ydk (YGOPro / EDOPro)", Body, 16, Graphite, new Vector2(110, H - 62));
        Refresh();
    }

    void Refresh()
    {
        foreach (var c in list.GetChildren()) c.QueueFree();
        var decks = Session.Decks;
        const float cw = 150, gx = 100, gy = 110;
        float ch = cw * 1.458f;
        int cols = 3, rows = Math.Max(1, (int)((H - 400 + gy) / (ch + gy)));
        Action redraw = () => { };
        redraw = Paged(list, new Rect2(110, 230, W * 0.46f, H - 380), decks.Count + 1, cols, rows, new Vector2(cw + gx, ch + gy), (i, host, at) =>
        {
            if (i == decks.Count)
            {
                var slot = new ColorRect { Position = at, Size = new Vector2(cw, ch), Color = Ink * new Color(1, 1, 1, 0.05f), Material = Kit.RoundedMat(cw / ch, 0.06f) };
                host.AddChild(slot);
                Put(host, "+", Title, 60, Graphite, at + new Vector2(0, ch / 2 - 46), new Vector2(cw, 70), HorizontalAlignment.Center);
                var nl = Put(host, "NEW DECK", Title, 16, Graphite, at + new Vector2(-30, ch + 18), new Vector2(cw + 60, 24), HorizontalAlignment.Center);
                Click(slot, NewDeck, h => nl.AddThemeColorOverride("font_color", h ? GoldDeep : Graphite));
                return;
            }
            var d = decks[i];
            bool on = d == sel;
            Case(host, d, at, cw, on);
            var l = Put(host, d.Name.ToUpperInvariant(), Title, 15, on ? GoldDeep : Ink, at + new Vector2(-30, ch + 18), new Vector2(cw + 60, 44), HorizontalAlignment.Center);
            l.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            if (on) Pip(host, at + new Vector2(cw / 2, ch + 70), 8);
            if (d == Session.Playing) Put(host, "IN USE", Title, 12, GoldDeep, at + new Vector2(0, -40), new Vector2(cw, 18), HorizontalAlignment.Center);
            var hit = new Control { Position = at - new Vector2(10, 10), Size = new Vector2(cw + 30, ch + 20) };
            host.AddChild(hit);
            Click(hit, () => { sel = d; Edit(); }, h => { if (h && sel != d) { sel = d; redraw(); Detail(); } });
        }, new Vector2(110, H - 140));
        Detail();
    }

    /// A deck case: the cover card standing on two fanned card backs.
    void Case(Control host, Deck d, Vector2 at, float w, bool on)
    {
        float h = w * 1.458f;
        for (int k = 2; k >= 1; k--)
        {
            var b = Pic(host, UI.Cosmetics.Back(P.Back, 256), at + new Vector2(k * 9, -k * 6), new Vector2(w, h));
            b.PivotOffset = new Vector2(w, h) / 2; b.Rotation = k * 0.04f; b.Modulate = new Color(1, 1, 1, 0.9f);
        }
        int cover = d.CoverCard();
        var c = cover >= 0 ? Card(host, Db.Get(cover), at + new Vector2(w, h) / 2, h, -0.02f, on ? 1.3f : 0.6f) : null;
        if (c != null && on) c.Scale = new Vector2(1.06f, 1.06f);
    }

    void Detail()
    {
        foreach (var c in detail.GetChildren()) c.QueueFree();
        if (sel == null) { Put(detail, "NO DECKS YET", Title, 30, Silver, new Vector2(W * 0.6f, H * 0.45f)); return; }
        var d = sel;
        float rx = W * 0.57f;
        int cover = d.CoverCard();
        if (cover >= 0) Card(detail, Db.Get(cover), new Vector2(rx + 150, H * 0.42f), 400, 0.04f, 1f, large: true);
        float tx = rx + 340, y = H * 0.12f;
        Put(detail, d.Name.ToUpperInvariant(), Title, 38, Silver, new Vector2(tx, y), new Vector2(W - tx - 60, 50)).ClipText = true;
        if (d == Session.Playing) Put(detail, "IN USE", Title, 16, GoldLight, new Vector2(tx + 2, y + 56));
        var main = d.Main.Select(Db.Get).ToList();
        y += 104;
        foreach (var (k, v) in new[] { ("MAIN", $"{d.Main.Count}"), ("SIDE", $"{d.Side.Count}"), ("FUSION", $"{d.Extra.Count}"),
            ("MONSTER · SPELL · TRAP", $"{main.Count(c => c.IsMonster)} · {main.Count(c => c.IsSpell)} · {main.Count(c => c.IsTrap)}") })
        {
            Put(detail, k, Thin, 15, Mist, new Vector2(tx, y)); Put(detail, v, Title, 20, Silver, new Vector2(tx, y + 22)); y += 60;
        }
        Put(detail, "BAN LIST", Thin, 15, Mist, new Vector2(tx, y));
        float lx = tx;
        foreach (var (name, key) in DeckRules.Formats)
        {
            var pr = DeckRules.Problem(d, P, key);
            var l = Put(detail, $"{name} {(pr == null ? "✓" : "✕")}", Title, 20, pr == null ? Silver : new Color(1f, 0.55f, 0.5f), new Vector2(lx, y + 22));
            if (pr != null) l.TooltipText = pr;
            lx += Title.GetStringSize(l.Text, HorizontalAlignment.Left, -1, Z(20)).X + 34;
        }
        var why = DeckRules.Problem(d, P);
        if (why != null) Put(detail, why, Body, 17, new Color(1f, 0.6f, 0.55f), new Vector2(tx, y + 56), new Vector2(W - tx - 60, 24)).ClipText = true;

        // the main action, then the others as a column of clear buttons
        Button(detail, "EDIT DECK", new Vector2(rx + 150, H * 0.74f), Edit, 30);
        var acts = new List<(string, Action)>();
        if (d != Session.Playing) acts.Add(("USE THIS DECK", Use));
        acts.AddRange(new (string, Action)[] { ("COPY", CopyDeck), ("RENAME", Rename), ("EXPORT", Export), ("DELETE", Delete) });
        float bx = tx + 40, by = H * 0.6f;
        foreach (var (s2, act) in acts) { Button(detail, s2, new Vector2(bx + 120, by), act, 19); by += 62; }
    }

    // ---------------------------------------------------------------- actions

    void Edit()
    {
        remembered = sel;
        DeckBuilder.Editing = sel;
        DeckBuilder.Temp = null;
        Transition.Go(this, "res://ui/DeckBuilder.tscn");
    }

    void NewDeck()
    {
        // not in Session.Decks until the builder's SAVE: backing out leaves nothing behind
        remembered = sel;
        DeckBuilder.Editing = new Deck(UniqueName("New Deck"));
        DeckBuilder.Temp = null;
        Transition.Go(this, "res://ui/DeckBuilder.tscn");
    }

    void Use() { Session.Playing = sel; Session.Save(); Flash("IN USE"); Refresh(); }

    void CopyDeck()
    {
        var c = sel!.Copy(UniqueName(sel.Name + " copy"));
        Session.Decks.Insert(Session.Decks.IndexOf(sel) + 1, c);
        Session.Save(); sel = c; Refresh();
    }

    string UniqueName(string n)
    {
        string s = n; int k = 2;
        while (Session.Decks.Any(d => d.Name == s)) s = $"{n} {k++}";
        return s;
    }

    void Rename()
    {
        CloseToast();
        toast = new Control { Size = new Vector2(W, 150), Position = new Vector2(0, H - 150), ZIndex = 40 };
        AddChild(toast);
        toast.AddChild(new ColorRect { Size = toast.Size, Color = new Color(0.02f, 0.02f, 0.03f, 0.94f), MouseFilter = MouseFilterEnum.Stop });
        Hair(toast, Vector2.Zero, W, GoldC with { A = 0.6f });
        var e = new LineEdit { Text = sel!.Name, Position = new Vector2(W / 2 - 360, 30), Size = new Vector2(720, 60), MaxLength = 24, SelectAllOnFocus = true, Alignment = HorizontalAlignment.Center, Flat = true };
        e.AddThemeFontOverride("font", Title); e.AddThemeFontSizeOverride("font_size", 34); e.AddThemeColorOverride("font_color", Silver); e.AddThemeColorOverride("caret_color", Silver);
        toast.AddChild(e);
        Hair(toast, new Vector2(W / 2 - 360, 92), 720, Silver * new Color(1, 1, 1, 0.3f));
        void Done() { if (e.Text.Trim().Length > 0) { sel.Name = e.Text.Trim(); Session.Save(); } CloseToast(); Refresh(); }
        e.TextSubmitted += _ => Done();
        Button(toast, "OK", new Vector2(W / 2 + 460, 60), Done, 22);
        e.GrabFocus();
    }

    void Delete()
    {
        var d = sel!;
        Ask($"Delete {d.Name}?", ("DELETE", () =>
        {
            CloseToast();
            Session.Decks.Remove(d);
            if (Session.Playing == d) Session.Playing = Session.Decks.FirstOrDefault();
            Session.Save();
            sel = Session.Playing ?? Session.Decks.FirstOrDefault();
            Refresh();
        }), ("KEEP", CloseToast));
    }

    // ---------------------------------------------------------------- deck files

    static readonly string[] DeckFilters = { "*.ydc ; Power of Chaos deck", "*.ydk ; YGOPro / EDOPro deck" };

    FileDialog FileBox(FileDialog.FileModeEnum mode)
    {
        var fd = new FileDialog { FileMode = mode, Access = FileDialog.AccessEnum.Filesystem, Filters = DeckFilters, UseNativeDialog = true };
        fd.CurrentDir = OS.GetSystemDir(OS.SystemDir.Documents);
        fd.Canceled += fd.QueueFree;
        AddChild(fd);
        fd.PopupCentered(new Vector2I(900, 600));
        return fd;
    }

    void Import() => FileBox(FileDialog.FileModeEnum.OpenFile).FileSelected += path =>
    {
        var (d, unknown) = DeckFile.Read(System.IO.File.ReadAllBytes(path), System.IO.Path.GetFileNameWithoutExtension(path));
        if (d.Main.Count + d.Extra.Count + d.Side.Count == 0) { Ask("No cards in that file."); return; }
        d.Name = UniqueName(d.Name);
        Session.Decks.Add(d); Session.Save(); sel = d; Refresh();
        // Cards you don't own show in the deck's ban-list line until you get them.
        Ask(unknown > 0 ? $"Imported {d.Name}. {unknown} card{(unknown == 1 ? "" : "s")} in the file aren't in this game and were skipped." : $"Imported {d.Name}.");
    };

    void Export()
    {
        var d = sel!;
        var fd = FileBox(FileDialog.FileModeEnum.SaveFile);
        fd.CurrentFile = d.Name + ".ydc";
        fd.FileSelected += path =>
        {
            if (path.EndsWith(".ydk", StringComparison.OrdinalIgnoreCase)) System.IO.File.WriteAllText(path, DeckFile.ToYdk(d));
            else System.IO.File.WriteAllBytes(path, DeckFile.ToYdc(d));
            Flash("EXPORTED");
        };
    }
}
