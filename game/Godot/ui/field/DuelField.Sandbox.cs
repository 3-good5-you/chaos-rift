using Godot;
using PoC.Engine;

namespace PoC.Game.UI;

/// The FX Lab's SANDBOX (key "sandbox" or "sandbox:Card Name"): a live duel on your turn 3, Main Phase 1, with both
/// hands empty and both fields clear. The SANDBOX panel (right) puts any card anywhere for either side whenever you have
/// a Main or Battle Phase choice up, changes LP, and lets you play the rival's side yourself (hands open) or leave it
/// to the CPU. Cards added this way break the duel's replay log: lab only.
public partial class DuelField
{
    bool sandbox, hotseat;
    Panel? sbPanel;
    Label sbPicked = null!, sbStatus = null!;
    CardData? sbCard;
    int sbSide;   // 0 you, 1 rival (screen sides)
    readonly List<Button> sbSideButtons = new(), sbRivalButtons = new();

    bool Mine(int player) => player == Me || hotseat;

    /// Called by ScenarioPlay once the duel stands at your turn 3: empty both hands, add the card being tested.
    void SandboxStart()
    {
        sandbox = true;
        foreach (var pl in D.Players)
        {
            foreach (var c in pl.Hand.ToList()) { pl.Hand.Remove(c); c.Loc = Loc.Deck; pl.Deck.Add(c); }
        }
        var parts = scenario!.Split(':', 2);
        if (parts.Length == 2)
        {
            var d = CardDb.Shared.Named(parts[1]);
            bool extra = d.IsMonster && (d.IsFusion || d.IsRitual || (Scripts.Get(d)?.NoNormalSummon ?? false));
            SandboxPut(d, 0, d.IsTrap ? "SPELL/TRAP SET" : extra ? "ATTACK" : "HAND");
            // Its family / the cards it names wait in your Deck (Fusions in the Fusion Deck): "from your hand or Deck"
            // effects (Armed Dragon LV3 -> LV5) have something to fetch, as in a real deck built around it.
            var pl = D.P(Me);
            foreach (var r in Families.Related(d))
            {
                var c = D.NewCard(r, Me);
                if (r.IsFusion) { c.Loc = Loc.Extra; pl.Extra.Add(c); } else { c.Loc = Loc.Deck; pl.Deck.Add(c); }
            }
        }
        D.RefreshPrompt();
    }

    static readonly string[] SbPlaces = { "HAND", "ATTACK", "DEFENSE", "SET MONSTER", "SPELL/TRAP SET", "SPELL/TRAP FACE-UP", "FIELD ZONE", "GRAVEYARD", "BANISHED", "DECK TOP", "FUSION DECK" };

    /// A new copy of the card, straight into place. False when there is no room.
    bool SandboxPut(CardData d, int side, string where)
    {
        int p = side == 0 ? Me : 1 - Me;
        var pl = D.P(p);
        var c = D.NewCard(d, p);
        switch (where)
        {
            case "HAND": c.Loc = Loc.Hand; pl.Hand.Add(c); break;
            case "GRAVEYARD": c.Loc = Loc.Grave; c.FaceUp = true; pl.Grave.Add(c); break;
            case "BANISHED": c.Loc = Loc.Banished; c.FaceUp = true; pl.Banished.Add(c); break;
            case "DECK TOP": c.Loc = Loc.Deck; pl.Deck.Insert(0, c); break;
            case "FUSION DECK": c.Loc = Loc.Extra; pl.Extra.Add(c); break;
            default:
                var loc = where switch { "FIELD ZONE" => Loc.Field, "SPELL/TRAP SET" or "SPELL/TRAP FACE-UP" => Loc.Spell, _ => Loc.Monster };
                if (loc == Loc.Field && pl.FieldZone != null) return false;
                if (!D.Place(c, p, loc)) return false;
                c.FaceUp = where is not ("SET MONSTER" or "SPELL/TRAP SET");
                c.Defense = where is "DEFENSE" or "SET MONSTER";
                c.SetTurn = c.SummonTurn = -5;
                if (loc == Loc.Monster && c.FaceUp) c.Summoned = SummonKind.Special;
                if (loc == Loc.Spell && c.FaceUp) c.Vars["stay"] = 1;   // a face-up Spell that lasts (Swords, Steelcage...) counts as active
                break;
        }
        return true;
    }

    void SandboxPanel()
    {
        // Bottom right, under the phase circle (the field and the right column's panels stay clear); scrolls when the
        // search results make it taller than the room.
        float w = 380, x = W - w - 24, y = H / 2 + 75 + 16, h = H - y - 24 - 46 - 10;
        var toggle = Chips.Make("SANDBOX", new Vector2(W - 24 - 180, H - 24 - 46), new Vector2(180, 46), () => sbPanel!.Visible = !sbPanel.Visible, 18);
        hud.AddChild(toggle);
        sbPanel = Box(hud, new Rect2(x, y, w, h), Glass(RivalPanels.Gold * new Color(1, 1, 1, 0.6f)));
        sbPanel.MouseFilter = MouseFilterEnum.Stop;
        var scroll = new ScrollContainer { Position = new Vector2(10, 8), Size = new Vector2(w - 20, h - 16), HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
        sbPanel.AddChild(scroll);
        var col = new VBoxContainer { CustomMinimumSize = new Vector2(w - 34, 0) };
        col.AddThemeConstantOverride("separation", 4);
        scroll.AddChild(col);
        w -= 6;
        Button B(Control parent, string text, Action press, float bw = 0)
        {
            var b = new Button { Text = text, CustomMinimumSize = new Vector2(bw, 30), FocusMode = FocusModeEnum.None, ClipText = true, TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis };
            b.AddThemeFontOverride("font", Ui); b.AddThemeFontSizeOverride("font_size", 14);
            b.AddThemeStyleboxOverride("normal", Pill(new Color(0.04f, 0.06f, 0.1f, 0.88f), new Color(RivalPanels.Gold, 0.3f), 8));
            b.AddThemeStyleboxOverride("hover", Pill(new Color(0.09f, 0.11f, 0.17f, 0.95f), RivalPanels.Gold, 8));
            b.AddThemeStyleboxOverride("pressed", Pill(new Color(0.16f, 0.13f, 0.05f, 0.95f), RivalPanels.Gold, 8));
            b.Pressed += press;
            parent.AddChild(b);
            return b;
        }
        HBoxContainer Row() { var r = new HBoxContainer(); r.AddThemeConstantOverride("separation", 5); col.AddChild(r); return r; }
        col.AddChild(Text("ADD A CARD", Cond, 20, RivalPanels.Gold, 4));
        var search = new LineEdit { PlaceholderText = "Search a card name...", CustomMinimumSize = new Vector2(w - 28, 34) };
        search.AddThemeFontOverride("font", Ui); search.AddThemeFontSizeOverride("font_size", 16);
        col.AddChild(search);
        var results = new VBoxContainer(); results.AddThemeConstantOverride("separation", 2);
        col.AddChild(results);
        var names = CardDb.Shared.ByIdx.Values.Where(d => d.Name.Length > 0).Select(d => d.Name).Distinct().OrderBy(n => n).ToList();
        search.TextChanged += t =>
        {
            foreach (var n in results.GetChildren()) n.QueueFree();
            if (t.Trim().Length < 2) return;
            foreach (var n in names.Where(n => n.Contains(t.Trim(), StringComparison.OrdinalIgnoreCase)).Take(8))
                B(results, n, () => { sbCard = CardDb.Shared.Named(n); sbPicked.Text = n; Focus(new Card(-1, sbCard, 0)); }, w - 28);
        };
        // The card under test's family and the cards it names: one click picks them (no searching).
        if (scenario?.Split(':', 2) is [_, var tested] && Families.Related(CardDb.Shared.Named(tested)) is { Count: > 0 } rel)
        {
            col.AddChild(Text("WORKS WITH", Cond, 16, RivalPanels.Gold, 4));
            var rg = new GridContainer { Columns = 2 }; rg.AddThemeConstantOverride("h_separation", 4); rg.AddThemeConstantOverride("v_separation", 3);
            col.AddChild(rg);
            foreach (var r in rel.Take(30)) { var rd = r; B(rg, rd.Name, () => { sbCard = rd; sbPicked.Text = rd.Name; Focus(new Card(-1, rd, 0)); }, (w - 38) / 2); }
        }
        sbPicked = Text("(pick a card)", Ui, 17, Colors.White); col.AddChild(sbPicked);
        var sides = Row();
        sides.AddChild(Text("FOR", Cond, 16, RivalPanels.Gold, 4));
        foreach (var (label, s) in new[] { ("YOU", 0), ("RIVAL", 1) }) { int k = s; sbSideButtons.Add(B(sides, label, () => { sbSide = k; SbMarks(); }, 90)); }
        var grid = new GridContainer { Columns = 2 }; grid.AddThemeConstantOverride("h_separation", 4); grid.AddThemeConstantOverride("v_separation", 3);
        col.AddChild(grid);
        foreach (var place in SbPlaces) { var pl = place; B(grid, pl, () => SandboxAdd(pl), (w - 38) / 2); }
        col.AddChild(Text("LIFE POINTS", Cond, 16, RivalPanels.Gold, 4));
        foreach (var (label, side) in new[] { ("YOU", 0), ("RIVAL", 1) })
        {
            var r = Row(); r.AddChild(Text(label, Ui, 14, Colors.White));
            foreach (var amt in new[] { -1000, -100, 100, 1000 }) { int a = amt, sd = side; B(r, (a > 0 ? "+" : "") + a, () => SandboxLp(sd, a), 66); }
        }
        var rv = Row(); rv.AddChild(Text("RIVAL PLAYED BY", Cond, 16, RivalPanels.Gold, 4));
        foreach (var (label, hs) in new[] { ("CPU", false), ("ME", true) }) { bool me2 = hs; sbRivalButtons.Add(B(rv, label, () => { hotseat = me2; SbMarks(); steps.Add(Snap(null)); Sync(Snap(null).Cards, null); }, 70)); }
        sbStatus = Text("", Ui, 15, new Color(1, 1, 1, 0.7f)); col.AddChild(sbStatus);
        sbStatus.AutowrapMode = TextServer.AutowrapMode.WordSmart; sbStatus.CustomMinimumSize = new Vector2(w - 28, 40);
        SbMarks();
    }

    void SbMarks()
    {
        for (int i = 0; i < sbSideButtons.Count; i++) sbSideButtons[i].Modulate = i == sbSide ? RivalPanels.Gold : new Color(1, 1, 1, 0.6f);
        for (int i = 0; i < sbRivalButtons.Count; i++) sbRivalButtons[i].Modulate = (i == 1) == hotseat ? RivalPanels.Gold : new Color(1, 1, 1, 0.6f);
    }

    /// Cards and LP change only while a Main / Battle choice of yours is open (the duel waits there, nothing runs).
    bool SbReady()
    {
        if (ask is { Kind: Ask.Main or Ask.Battle } && D.Pending == ask) return true;
        sbStatus.Text = "Wait for a Main or Battle Phase choice of yours (the duel must be waiting on you).";
        return false;
    }

    void SandboxAdd(string where)
    {
        if (sbCard == null) { sbStatus.Text = "Pick a card first (search above)."; return; }
        if (!SbReady()) return;
        if (!SandboxPut(sbCard, sbSide, where)) { sbStatus.Text = "No free zone there."; return; }
        sbStatus.Text = $"{sbCard.Name} → {(sbSide == 0 ? "your" : "the rival's")} {where.ToLowerInvariant()}";
        SbRefresh();
    }

    void SandboxLp(int side, int amount)
    {
        if (!SbReady()) return;
        var pl = D.P(side == 0 ? Me : 1 - Me);
        pl.Lp = Math.Max(1, pl.Lp + amount);
        SetLp(side, pl.Lp, true); shownLp[side] = pl.Lp;
        sbStatus.Text = $"{(side == 0 ? "Your" : "Rival")} LP: {pl.Lp}";
    }

    /// Show the new cards where they are and recount what you can do now.
    void SbRefresh()
    {
        var snap = Snap(null);
        foreach (var c in D.Cards) { var v = ViewOf(c.Uid); if (v.S.Loc == Loc.None && snap.Cards[c.Uid].Loc != Loc.None) Place(v, new St(Loc.Deck, 0, 1, false, false, snap.Cards[c.Uid].Side, 0, 0)); }
        Sync(snap.Cards, null);
        D.RefreshPrompt();
        if (ask == null) return;
        acts.Clear(); actSel.Clear(); pileActs.Clear();
        CollectActions(ask);
        RefreshHints();
    }
}
