using Godot;
using PoC.Engine;

namespace PoC.Game.UI;

/// Deck Builder (Chaos Rift sample A, locked 2026-11-08), opened from Your Decks. Your cards in the light (search,
/// kind, FILTERS), the deck in the dark as three labelled zones like the original game: MAIN DECK, SIDE DECK, then
/// FUSION DECK. Click a card of yours to add it (Fusion monsters go to the Fusion Deck); right-click to put it in the
/// Side Deck. Click a card in the deck to take it out; right-click moves it between Main and Side. Diamonds under your
/// cards count the copies already in the deck. Adding a card offers what it needs (Fusion materials, Ritual Spell).
/// Edits are a working copy until SAVE. `-- --demo`.
public partial class DeckBuilder : RiftMenu
{
    /// The deck to edit (set by Your Decks); null = the deck in use.
    public static Deck? Editing;

    /// A deck for one game mode only (Pack Duel, Limited Duel): built from `Pool` (a pretend collection with the
    /// mode's rule applied), never saved or exported; USE THIS DECK puts it in Session.ModeDeck and goes to `Next`.
    public sealed record TempBuild(string Name, string Rule, Profile Pool, string Next, List<List<int>>? Packs = null);
    public static TempBuild? Temp;
    static new Profile P => Temp?.Pool ?? Session.Profile;
    Deck source = null!, deck = null!;
    CardBrowser browser = null!;
    Control zones = null!;
    Label hoverLine = null!, legal = null!;
    LineEdit nameEdit = null!;
    TextureRect cover = null!;
    float dx, dw;
    Label dirtyLabel = null!;
    int justAdded = -1;

    bool Dirty => !deck.SameAs(source);

    protected override void OnBack()
    {
        if (toast != null) { CloseToast(); return; }
        if (!Dirty || Temp != null) { Leave(); return; }
        Ask("Unsaved changes.", ("SAVE", () => { if (Save()) Leave(); }), ("DISCARD", Leave));
    }

    void Leave() => Transition.Go(this, Temp != null ? "res://ui/DuelModes.tscn" : "res://ui/DeckSelect.tscn");

    protected override void Build()
    {
        Demo.Ensure();
        Rift(0.45f, 0.39f);
        source = Temp != null ? new Deck(Temp.Name) : Editing ?? Session.Playing ?? Session.Decks.FirstOrDefault() ?? AddNew();
        if (Temp != null && Session.ModeDeck != null) { source = Session.ModeDeck; }   // rebuilding: start from the deck you made
        deck = source.Copy();
        Back(Temp != null ? "MODES" : "DECKS");
        Put(this, "DECK BUILDER", Title, 40, Ink, new Vector2(60, 60));
        Hair(this, new Vector2(62, 114), 160, GoldC);

        var owned = Db.ByIdx.Values.Where(d => P.Count(d.Idx) > 0 && d.Name.Length > 0).OrderBy(d => d.Kind).ThenBy(d => d.IsFusion).ThenBy(d => d.Name).ToList();
        browser = new CardBrowser(this, new Rect2(60, 150, W * 0.36f, H - 180), 118, () => owned, new[] { "NAME", "ATK", "DEF", "LEVEL" });
        browser.Decorate = (hit, d, size) =>
        {
            int n = deck.Copies(d.Idx);
            for (int k = 0; k < n; k++) Pip(hit, new Vector2(size.X / 2 - (n - 1) * 8 + k * 16, size.Y + 10), 7);
        };
        browser.Faded = d => !DeckRules.CanAdd(deck, d, P) && !DeckRules.CanAdd(deck, d, P, side: true);
        browser.Picked = d => Add(d, false);
        browser.Right = d => Add(d, true);
        browser.Hovered = ShowHover;
        browser.Refresh();

        dx = W * 0.47f; dw = W - dx - 40;
        var coverHit = new Control { Position = new Vector2(dx, 128), Size = new Vector2(64, 64), TooltipText = "Choose the cover card" };
        AddChild(coverHit);
        cover = Pic(coverHit, null, Vector2.Zero, new Vector2(64, 64));
        cover.Material = Kit.RoundedMat(1, 0.14f);
        Click(coverHit, CoverPick, h => cover.SelfModulate = h ? new Color(1.15f, 1.15f, 1.15f) : Colors.White);
        Put(this, "COVER", Thin, 12, Mist, new Vector2(dx, 196), new Vector2(64, 16), HorizontalAlignment.Center);
        nameEdit = new LineEdit { Text = deck.Name, Position = new Vector2(dx + 80, 128), Size = new Vector2(dw - 420, 60), Flat = true, MaxLength = 24, SelectAllOnFocus = true, TooltipText = "Click to rename" };
        nameEdit.AddThemeFontOverride("font", Title); nameEdit.AddThemeFontSizeOverride("font_size", 36);
        nameEdit.AddThemeColorOverride("font_color", Silver); nameEdit.AddThemeColorOverride("caret_color", Silver);
        nameEdit.TextChanged += t => { if (t.Trim().Length > 0) deck.Name = t.Trim(); };
        nameEdit.TextSubmitted += _ => nameEdit.ReleaseFocus();
        AddChild(nameEdit);
        if (Temp == null)
        {
            var ren = Put(this, "✎ RENAME", Title, 14, Mist, new Vector2(dx + 82, 184));
            Click(ren, () => nameEdit.GrabFocus(), h => ren.AddThemeColorOverride("font_color", h ? GoldLight : Mist));
        }
        else
        {
            nameEdit.Editable = false;
            Put(this, Temp.Rule, Title, 14, GoldLight, new Vector2(dx + 82, 186), new Vector2(dw - 420, 22));
            Put(this, "FOR THIS DUEL ONLY  ·  NOT SAVED", Thin, 13, Mist, new Vector2(W - 400, 118), new Vector2(360, 20), HorizontalAlignment.Right);
        }
        legal = Put(this, "", Thin, 16, Mist, new Vector2(dx + 250, 186), new Vector2(dw - 590, 24));
        legal.Visible = Temp == null;
        legal.ClipText = true;
        dirtyLabel = Put(this, "", Title, 14, GoldLight, new Vector2(W - 400, 118), new Vector2(360, 20), HorizontalAlignment.Right);
        if (Temp != null) Button(this, "USE THIS DECK", new Vector2(W - 190, 180), UseTemp, 22);
        else
        {
            Button(this, "SAVE", new Vector2(W - 120, 180), () => Save(), 22);
            Button(this, "REVERT", new Vector2(W - 290, 180), () => { deck = source.Copy(); nameEdit.Text = deck.Name; Changed(); }, 18);
        }
        Hair(this, new Vector2(dx, 226), dw, Silver * new Color(1, 1, 1, 0.12f));
        Put(this, "CLICK: ADD   ·   RIGHT-CLICK: SIDE DECK", Thin, 13, Graphite * new Color(1, 1, 1, 0.6f), new Vector2(60, H - 34));
        zones = new Control { Size = new Vector2(W, H), MouseFilter = MouseFilterEnum.Ignore };
        AddChild(zones);
        hoverLine = Put(this, "", Body, 18, Mist, new Vector2(dx, H - 72), new Vector2(dw, 50));
        hoverLine.AutowrapMode = TextServer.AutowrapMode.WordSmart; hoverLine.ClipText = true;
        Changed();
    }

    /// Pack / Limited: the deck must be legal against the pool; then it is played, never saved.
    void UseTemp()
    {
        var why = DeckRules.Problem(deck, P);
        if (why != null) { Ask(why); return; }
        Session.ModeDeck = deck.Copy();
        Transition.Go(this, Temp!.Next);
    }

    static Deck AddNew() { var d = new Deck("New Deck"); Session.Decks.Add(d); return d; }

    // ---------------------------------------------------------------- edits

    void Add(CardData d, bool side)
    {
        if (!DeckRules.Add(deck, d, P, side)) { Ask(Why(d, side)); return; }
        justAdded = d.Idx;
        UiSound(false);
        Changed();
        if (!side) OfferMaterials(d);
    }

    string Why(CardData d, bool side)
    {
        if (deck.Copies(d.Idx) >= P.Count(d.Idx)) return $"You have {P.Count(d.Idx)} {d.Name} and they're all in the deck.";
        if (deck.CopiesNamed(d.Name) >= DeckRules.Limit(d)) return DeckRules.Limit(d) == 0 ? $"{d.Name} is forbidden." : $"{d.Name}: {DeckRules.Limit(d)} copies at most.";
        return side ? $"The Side Deck holds {DeckRules.MaxSide} cards." : $"The Main Deck holds {DeckRules.MaxMain} cards.";
    }

    void OfferMaterials(CardData d)
    {
        var missing = DeckRules.MissingMaterials(deck, d);
        if (missing.Count == 0) return;
        var names = string.Join(", ", missing.GroupBy(m => m.Name).Select(g => g.Count() > 1 ? $"{g.Key} ×{g.Count()}" : g.Key));
        bool any = missing.Any(m => Db.ByName[m.Name].Any(c => DeckRules.CanAdd(deck, c, P)));
        if (any) Ask($"{d.Name} needs {names}.", ("ADD", () => { CloseToast(); var f = DeckRules.AddMaterials(deck, d, P); Changed(); if (f.Count > 0) Ask($"Not in your cards: {string.Join(", ", f.Select(x => x.Name).Distinct())}."); }), ("SKIP", CloseToast));
        else Ask($"{d.Name} needs {names}. Not in your cards.");
    }

    bool Save(bool force = false)
    {
        var bare = DeckRules.FusionsMissingMaterials(deck);
        if (bare.Count > 0 && !force)
        {
            Ask($"Materials missing for {string.Join(", ", bare.Select(f => f.Name))}.",
                ("ADD", () => { CloseToast(); foreach (var f in bare) DeckRules.AddMaterials(deck, f, P); Changed(); }),
                ("SAVE ANYWAY", () => { CloseToast(); Save(true); }));
            return false;
        }
        source.Name = deck.Name; source.Cover = deck.Cover;
        source.Main.Clear(); source.Main.AddRange(deck.Main);
        source.Extra.Clear(); source.Extra.AddRange(deck.Extra);
        source.Side.Clear(); source.Side.AddRange(deck.Side);
        if (!Session.Decks.Contains(source)) Session.Decks.Add(source);
        Session.Playing ??= source;
        Session.Save();
        Flash("SAVED");
        dirtyLabel.Text = "";
        return true;
    }

    void ShowHover(CardData d)
    {
        string stats = d.IsMonster ? $"  ·  ATK {d.Atk} / DEF {d.Def}" : "";
        hoverLine.Text = $"{d.Name}{stats}  —  {(d.TcgDesc ?? d.Desc).Replace('\n', ' ')}";
    }

    // ---------------------------------------------------------------- the deck: MAIN, SIDE, FUSION

    static List<CardData> Sorted(IEnumerable<int> ids) => ids.Select(CardDb.Shared.Get).OrderBy(c => c.Kind).ThenByDescending(c => c.Level).ThenBy(c => c.Name).ToList();

    void Changed()
    {
        browser.BuildGrid();
        if (dirtyLabel != null) dirtyLabel.Text = Dirty && Temp == null ? "UNSAVED CHANGES" : "";
        foreach (var c in zones.GetChildren()) c.QueueFree();
        int cv = deck.CoverCard();
        cover.Texture = cv >= 0 ? CardArt.Picture(Db.Get(cv)) : null;
        var probs = DeckRules.Formats.Select(f => (f.Name, Pr: DeckRules.Problem(deck, P, f.Key))).ToList();
        legal.Text = string.Join("    ", probs.Select(p => $"{p.Name} {(p.Pr == null ? "✓" : "✕")}")) + (probs.FirstOrDefault(p => p.Pr != null).Pr is { } why ? $"    ·    {why}" : "");
        legal.AddThemeColorOverride("font_color", probs.All(p => p.Pr == null) ? Mist : new Color(1f, 0.6f, 0.55f));

        var main = Sorted(deck.Main); var side = Sorted(deck.Side); var ex = Sorted(deck.Extra);
        // tiles shrink a little for big decks so all three zones stay on screen
        float mw = main.Count <= 45 ? 60 : 60 * MathF.Sqrt(45f / main.Count), mh = mw * 1.458f, mg = 6;
        int cols = (int)((dw + mg) / (mw + mg));
        float y = 246;
        void Zone(string name, string count, Color tint)
        {
            Pip(zones, new Vector2(dx + 6, y + 12), 9);
            Put(zones, name, Title, 20, tint, new Vector2(dx + 22, y));
            Put(zones, count, Thin, 18, Mist, new Vector2(dx + 22 + Title.GetStringSize(name, HorizontalAlignment.Left, -1, 20).X + 20, y + 2));
            Hair(zones, new Vector2(dx, y + 34), dw, tint * new Color(1, 1, 1, 0.35f));
            y += 46;
        }
        void Tiles(List<CardData> cards, int slots, Color tint, bool isSide)
        {
            int n = Math.Max(cards.Count, slots);
            for (int i = 0; i < n; i++)
            {
                var at = new Vector2(dx + i % cols * (mw + mg), y + i / cols * (mh + mg));
                if (i >= cards.Count)
                {
                    zones.AddChild(new ColorRect { Position = at, Size = new Vector2(mw, mh), Color = tint * new Color(1, 1, 1, 0.06f), MouseFilter = MouseFilterEnum.Ignore, Material = Kit.RoundedMat(mw / mh, 0.08f) });
                    continue;
                }
                var d = cards[i];
                var t = Pic(zones, CardArt.Small(d), at, new Vector2(mw, mh));
                t.PivotOffset = new Vector2(mw, mh) / 2;
                t.TooltipText = d.Name;
                bool last = d.Idx == justAdded && (i + 1 == cards.Count || cards[i + 1].Idx != d.Idx);
                if (last) { t.Scale = new Vector2(1.3f, 1.3f); CreateTween().TweenProperty(t, "scale", Vector2.One, 0.3).SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out); }
                Click(t, () => { DeckRules.Remove(deck, d, isSide); UiSound(false); Changed(); },
                    h => { t.Scale = h ? new Vector2(1.08f, 1.08f) : Vector2.One; t.ZIndex = h ? 1 : 0; if (h) ShowHover(d); },
                    () => { if (DeckRules.Move(deck, d, !isSide)) { justAdded = d.Idx; Changed(); } else Ask(isSide ? $"The Main Deck holds {DeckRules.MaxMain} cards." : $"The Side Deck holds {DeckRules.MaxSide} cards."); });
            }
            y += Math.Max(1, (n + cols - 1) / cols) * (mh + mg) + 18;
        }
        Zone("MAIN DECK", $"{main.Count} / {DeckRules.MinMain}–{DeckRules.MaxMain}     MONSTER {main.Count(c => c.IsMonster)}   SPELL {main.Count(c => c.IsSpell)}   TRAP {main.Count(c => c.IsTrap)}", Silver);
        Tiles(main, DeckRules.MinMain, Silver, false);
        Zone("SIDE DECK", $"{side.Count} / {DeckRules.MaxSide}", Teal);
        Tiles(side, DeckRules.MaxSide, Teal, true);
        Zone("FUSION DECK", $"{ex.Count}", Violet);
        Tiles(ex, 0, Violet, false);
        justAdded = -1;
    }

    /// Pick the cover from the deck's own cards.
    void CoverPick()
    {
        var ids = deck.Main.Concat(deck.Extra).Distinct().Select(Db.Get).OrderBy(c => c.Kind).ThenByDescending(c => c.Level).ToList();
        if (ids.Count == 0) return;
        CloseToast();
        toast = new Control { Size = new Vector2(W, H), ZIndex = 40 };
        AddChild(toast);
        toast.AddChild(new ColorRect { Size = toast.Size, Color = new Color(0.02f, 0.02f, 0.03f, 0.95f), MouseFilter = MouseFilterEnum.Stop });
        Put(toast, "COVER CARD", Title, 38, Silver, new Vector2(0, 70), new Vector2(W, 50), HorizontalAlignment.Center);
        Hair(toast, new Vector2(W / 2 - 100, 128), 200, GoldC);
        var sc = new ScrollContainer { Position = new Vector2(120, 170), Size = new Vector2(W - 240, H - 300), HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
        toast.AddChild(sc);
        const float tw = 110, gap = 20; float th = tw * 1.458f;
        int cols = (int)((W - 240 + gap) / (tw + gap));
        var holder = new Control { CustomMinimumSize = new Vector2(W - 260, ((ids.Count + cols - 1) / cols) * (th + gap) + 20) };
        sc.AddChild(holder);
        int now = deck.CoverCard();
        for (int i = 0; i < ids.Count; i++)
        {
            var d = ids[i];
            var at = new Vector2(i % cols * (tw + gap), i / cols * (th + gap) + 10);
            var t = Pic(holder, CardArt.Small(d), at, new Vector2(tw, th));
            t.PivotOffset = new Vector2(tw, th) / 2;
            if (d.Idx == now) Pip(holder, at + new Vector2(tw / 2, -8), 9);
            Click(t, () => { deck.Cover = d.Idx; CloseToast(); Changed(); }, h => t.Scale = h ? new Vector2(1.06f, 1.06f) : Vector2.One);
        }
        Button(toast, "BACK", new Vector2(W / 2, H - 70), CloseToast, 22);
    }
}
