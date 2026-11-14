using Godot;
using PoC.Engine;

namespace PoC.Game.UI;

/// Deck Builder (Chaos Rift sample A, locked 2026-11-08), opened from Your Decks. Your cards in the light (search,
/// kind, FILTERS), the deck in the dark as three labelled zones like the original game: MAIN DECK, SIDE DECK, then
/// FUSION DECK. Hover any card to see it in the preview (left). Left-click adds a copy (Fusion monsters go to the
/// Fusion Deck), right-click takes one out, dragging moves a card between your cards, the Main Deck and the Side Deck.
/// Diamonds under your cards count the copies already in the deck. Adding a card offers what it needs (Fusion materials, Ritual Spell).
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
    Label legal = null!;
    Control preview = null!;
    CardData? shown;
    const float PreviewW = 360;
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
        Rift(0.668f, 0.653f);
        source = Temp != null ? new Deck(Temp.Name) : Editing ?? Session.Playing ?? Session.Decks.FirstOrDefault() ?? AddNew();
        if (Temp != null && Session.ModeDeck != null) { source = Session.ModeDeck; }   // rebuilding: start from the deck you made
        deck = source.Copy();
        Back(Temp != null ? "MODES" : "DECKS");
        Put(this, "DECK BUILDER", Title, 40, Ink, new Vector2(60, 60));
        Hair(this, new Vector2(62, 114), 160, GoldC);

        dx = W * 0.68f; dw = W - dx - 40;   // the deck: a narrow column on the right
        // anything dropped on the light side goes back to your cards
        var leftDrop = new Control { Size = new Vector2(dx - 30, H), MouseFilter = MouseFilterEnum.Pass };
        AddChild(leftDrop);
        Wire(leftDrop, 'c', null);
        preview = new Control { Position = new Vector2(60, 140), Size = new Vector2(PreviewW, 10), MouseFilter = MouseFilterEnum.Ignore };
        AddChild(preview);

        var owned = Db.ByIdx.Values.Where(d => P.Count(d.Idx) > 0 && d.Name.Length > 0).ToList();
        // the preview has the whole left column; your cards take the middle, up to the rift
        float bx = 60 + PreviewW + 36;
        browser = new CardBrowser(this, new Rect2(bx, 146, dx - 70 - bx, H - 176), 96, () => owned, new[] { "NAME", "ATK", "DEF", "LEVEL" })
        { Compact = true, GroupKinds = true, ReleaseClick = true };
        Wire(browser.Grid, 'c', null);
        browser.Decorate = (hit, d, size) =>
        {
            int n = deck.Copies(d.Idx);
            for (int k = 0; k < n; k++) Pip(hit, new Vector2(size.X / 2 - (n - 1) * 8 + k * 16, size.Y + 10), 7);
            Wire(hit, 'c', d);
        };
        browser.Faded = d => !DeckRules.CanAdd(deck, d, P) && !DeckRules.CanAdd(deck, d, P, side: true);
        browser.Picked = d => Add(d, false);
        browser.Right = TakeOut;
        browser.Hovered = ShowCard;
        browser.Refresh();

        var coverHit = new Control { Position = new Vector2(dx, 128), Size = new Vector2(64, 64), TooltipText = "Choose the cover card" };
        AddChild(coverHit);
        cover = Pic(coverHit, null, Vector2.Zero, new Vector2(64, 64));
        cover.Material = Kit.RoundedMat(1, 0.14f);
        Click(coverHit, CoverPick, h => cover.SelfModulate = h ? new Color(1.15f, 1.15f, 1.15f) : Colors.White);
        Put(this, "COVER", Thin, 12, Mist, new Vector2(dx, 196), new Vector2(64, 16), HorizontalAlignment.Center);
        nameEdit = new LineEdit { Text = deck.Name, Position = new Vector2(dx + 80, 128), Size = new Vector2(dw - 80, 60), Flat = true, MaxLength = 24, SelectAllOnFocus = true, TooltipText = "Click to rename" };
        nameEdit.AddThemeFontOverride("font", Title); nameEdit.AddThemeFontSizeOverride("font_size", 30);
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
            Put(this, Temp.Rule, Title, 14, GoldLight, new Vector2(dx + 82, 208), new Vector2(dw - 82, 22));
            Put(this, "FOR THIS DUEL ONLY  ·  NOT SAVED", Thin, 13, Mist, new Vector2(dx, 100));
        }
        legal = Put(this, "", Thin, 15, Mist, new Vector2(dx + 82, 208), new Vector2(dw - 82, 22));
        legal.Visible = Temp == null;
        legal.ClipText = true;
        dirtyLabel = Put(this, "", Title, 14, GoldLight, new Vector2(dx, 100));
        if (Temp != null) Button(this, "USE THIS DECK", new Vector2(W - 170, 76), UseTemp, 22);
        else
        {
            Button(this, "SAVE", new Vector2(W - 110, 76), () => Save(), 22);
            Button(this, "REVERT", new Vector2(W - 270, 76), () => { deck = source.Copy(); nameEdit.Text = deck.Name; Changed(); }, 18);
        }
        Hair(this, new Vector2(dx, 238), dw, Silver * new Color(1, 1, 1, 0.12f));
        zones = new Control { Size = new Vector2(W, H), MouseFilter = MouseFilterEnum.Ignore };
        AddChild(zones);
        Changed();
        if (deck.CoverCard() is >= 0 and var cv0) ShowCard(Db.Get(cv0)); else if (owned.Count > 0) ShowCard(owned[0]);
    }

    /// Pack / Limited: the deck must be legal against the pool; then it is played, never saved.
    void UseTemp()
    {
        var why = DeckRules.Problem(deck, P);
        if (why != null) { Ask(why); return; }
        Session.ModeDeck = deck.Copy();
        Transition.Go(this, Temp!.Next);
    }

    static Deck AddNew() => new("New Deck");   // joins Session.Decks on SAVE, never before

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

    /// Right-click on one of your cards: one copy leaves the deck (Main / Fusion first, then the Side Deck).
    void TakeOut(CardData d)
    {
        if (!DeckRules.Remove(deck, d) && !DeckRules.Remove(deck, d, true)) return;
        UiSound(false); Changed();
    }

    /// The card preview (left): the card, its name, type line, ATK / DEF and its text with the keywords marked. It
    /// stays on the last card you pointed at.
    void ShowCard(CardData d)
    {
        if (shown == d) return;
        shown = d;
        Preview(preview, d, PreviewW);
    }

    // ---------------------------------------------------------------- drag and drop

    /// Make `c` part of the drag-and-drop: it accepts drops for `zone` ('c' your cards, 'm' Main, 's' Side, 'x'
    /// Fusion), and with a card it can be picked up from there.
    void Wire(Control c, char zone, CardData? d) => c.SetDragForwarding(
        Callable.From<Vector2, Variant>(_ =>
        {
            if (d == null) return default;
            var ghost = new Control();
            Pic(ghost, CardArt.Small(d), new Vector2(-45, -66), new Vector2(90, 131)).Modulate = new Color(1, 1, 1, 0.85f);
            c.SetDragPreview(ghost);
            return $"{zone}{d.Idx}";
        }),
        Callable.From<Vector2, Variant, bool>((_, data) => data.VariantType == Variant.Type.String && Group(data.AsString()[0]) != Group(zone)),
        Callable.From<Vector2, Variant>((_, data) => Drop(data.AsString(), zone)));

    static char Group(char zone) => zone == 'x' ? 'm' : zone;   // Main and Fusion are one place: the card knows which

    void Drop(string data, char to)
    {
        char from = Group(data[0]); to = Group(to);
        var d = Db.Get(int.Parse(data[1..]));
        if (from == 'c') { Add(d, to == 's'); return; }
        bool ok = to == 'c' ? DeckRules.Remove(deck, d, from == 's') : DeckRules.Move(deck, d, to == 's');
        if (!ok) { Ask(to == 's' ? $"The Side Deck holds {DeckRules.MaxSide} cards." : $"The Main Deck holds {DeckRules.MaxMain} cards."); return; }
        if (to != 'c') justAdded = d.Idx;
        UiSound(false); Changed();
    }

    // ---------------------------------------------------------------- the deck: MAIN, SIDE, FUSION

    static List<CardData> Sorted(IEnumerable<int> ids) => ids.Select(CardDb.Shared.Get).OrderBy(c => c.Kind).ThenByDescending(c => c.Level).ThenByDescending(c => c.Atk).ThenBy(c => c.Name).ToList();

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
        float mw = 52, mh, mg = 6; int cols;
        for (; ; mw -= 2)
        {
            mh = mw * 1.458f; cols = (int)((dw + mg) / (mw + mg));
            int rows = Math.Max(1, (Math.Max(main.Count, DeckRules.MinMain) + cols - 1) / cols) + (DeckRules.MaxSide + cols - 1) / cols + Math.Max(1, (ex.Count + cols - 1) / cols);
            if (mw <= 30 || 256 + 3 * 46 + 24 + 2 * 18 + rows * (mh + mg) <= H - 16) break;
        }
        float y = 256;
        void Zone(string name, string count, Color tint, string? kinds = null)
        {
            // the Monster / Spell / Trap counts get their own line under the header (beside it they ran off the column)
            if (kinds != null) Put(zones, kinds, Title, 14, Silver * new Color(1, 1, 1, 0.85f), new Vector2(dx + 22, y + 42));
            Pip(zones, new Vector2(dx + 6, y + 12), 9);
            Put(zones, name, Title, 20, tint, new Vector2(dx + 22, y));
            Put(zones, count, Title, 16, Silver * new Color(1, 1, 1, 0.85f), new Vector2(dx + 22 + Title.GetStringSize(name, HorizontalAlignment.Left, -1, Z(20)).X + 24, y + 3));
            Hair(zones, new Vector2(dx, y + 34), dw, tint * new Color(1, 1, 1, 0.35f));
            y += kinds != null ? 70 : 46;
        }
        void Tiles(List<CardData> cards, int slots, Color tint, char zone)
        {
            bool isSide = zone == 's';
            int n = Math.Max(cards.Count, slots);
            float zh = Math.Max(1, (n + cols - 1) / cols) * (mh + mg);
            var bed = new Control { Position = new Vector2(dx - 10, y - 70), Size = new Vector2(dw + 20, zh + 82), MouseFilter = MouseFilterEnum.Pass };
            zones.AddChild(bed);
            Wire(bed, zone, null);
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
                Click(t, () => Add(d, isSide),
                    h => { t.Scale = h ? new Vector2(1.08f, 1.08f) : Vector2.One; t.ZIndex = h ? 1 : 0; if (h) ShowCard(d); },
                    () => { DeckRules.Remove(deck, d, isSide); UiSound(false); Changed(); }, release: true);
                Wire(t, zone, d);
            }
            y += zh + 18;
        }
        Zone("MAIN DECK", $"{main.Count} / {DeckRules.MinMain}–{DeckRules.MaxMain}", Silver, $"MONSTER {main.Count(c => c.IsMonster)}  SPELL {main.Count(c => c.IsSpell)}  TRAP {main.Count(c => c.IsTrap)}");
        Tiles(main, DeckRules.MinMain, Silver, 'm');
        Zone("SIDE DECK", $"{side.Count} / {DeckRules.MaxSide}", Teal);
        Tiles(side, DeckRules.MaxSide, Teal, 's');
        Zone("FUSION DECK", $"{ex.Count}", Violet);
        Tiles(ex, 0, Violet, 'x');
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
