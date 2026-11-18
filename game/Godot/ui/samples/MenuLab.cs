using Godot;
using PoC.Engine;

namespace PoC.Game.UI;

/// Menu Lab (2026-11-16): samples for the menu screens that need a redo, all in the Chaos Rift look. Rows: button style
/// (used by every other sample once picked), tournament bracket, achievements, the TYPE filter, deck choice, duel type,
/// rivals for regular duels, gauntlet rewards. Pick a row on the left, play its options, pick one, leave a comment
/// (saved to user://menu_lab_picks_round4.json and game/menu_lab_picks.txt). Keys: up/down rows, 1-5 options, H hide panel.
/// Run: res://ui/samples/MenuLab.tscn -- --demo   (`--labplay=row:option`, `--labhide`)
public partial class MenuLab : RiftMenu
{
    // Round 4 (2026-11-17) built: champion prize B (Results.Podium). No open rows; add the next round's here.
    static readonly (string Name, string[] Vars)[] Rows = Array.Empty<(string, string[])>();

    sealed class Pick { public int Choice { get; set; } = -1; public string Note { get; set; } = ""; }
    static Dictionary<string, Pick> picks = new();
    static int sel, lastVar;
    static bool loaded, played;

    CanvasLayer layer = null!;
    VBoxContainer panel = null!;
    readonly List<Godot.Button> rowButtons = new(), varButtons = new(), pickButtons = new();
    Label rowTitle = null!, saved = null!;
    LineEdit note = null!;
    const string File = "user://menu_lab_picks_round4.json";
    static string TextFile => ProjectSettings.GlobalizePath("res://").TrimEnd('/') + "/../menu_lab_picks_round4.txt";

    protected override string? MusicPath => Md("solo_gate");
    protected override int ItemCount => 1;
    protected override void OnBack() { }

    /// The button style every sample draws with: the row 1 pick (or the option on show while row 1 is open).
    static int BtnStyle => 3;   // locked: arrow badge

    protected override void Build()
    {
        Demo.Ensure();
        if (Rows.Length == 0) { Flash("NO OPEN ROWS"); return; }
        if (Session.Picks.Length == 0) Session.RollPicks();
        if (!loaded) { Load(); loaded = true; }
        layer = new CanvasLayer { Layer = 50 };
        AddChild(layer);
        BuildPanel();
        Select(sel);
        bool hide = false;
        foreach (var a in OS.GetCmdlineUserArgs())
        {
            if (a == "--labhide") hide = true;
            if (a.StartsWith("--labplay=") && !played)
            {
                played = true;
                var p = a[10..].Split(':');
                sel = int.Parse(p[0]) - 1; Select(sel);
                lastVar = p.Length > 1 ? int.Parse(p[1]) - 1 : 0;
            }
        }
        Play(lastVar);
        if (hide) panel.GetParent<Control>().Visible = false;
    }

    public override void _UnhandledInput(InputEvent e)
    {
        if (e is not InputEventKey { Pressed: true, Echo: false } k || note.HasFocus()) return;
        if (k.Keycode == Key.H) { var p = panel.GetParent<Control>(); p.Visible = !p.Visible; }
        else if (k.Keycode == Key.Down) { Select(sel + 1); Play(0); }
        else if (k.Keycode == Key.Up) { Select(sel - 1); Play(0); }
        else if (k.Keycode is >= Key.Key1 and <= Key.Key5) Play((int)(k.Keycode - Key.Key1));
    }

    // ---------------------------------------------------------------- panel (same as the Victory Lab)

    Godot.Button PBtn(Control parent, string text, Action press, float w, float h = 32)
    {
        var b = new Godot.Button { Text = text, Alignment = HorizontalAlignment.Left, CustomMinimumSize = new Vector2(w, h), FocusMode = FocusModeEnum.None, ClipText = true };
        b.AddThemeFontSizeOverride("font_size", 15);
        StyleBoxFlat Sb(Color bg, Color edge) { var s = new StyleBoxFlat { BgColor = bg, BorderColor = edge, ContentMarginLeft = 10 }; s.SetBorderWidthAll(1); s.SetCornerRadiusAll(8); return s; }
        b.AddThemeStyleboxOverride("normal", Sb(new Color(0.04f, 0.05f, 0.08f, 0.9f), new Color(RivalPanels.Gold, 0.3f)));
        b.AddThemeStyleboxOverride("hover", Sb(new Color(0.09f, 0.1f, 0.15f, 0.95f), RivalPanels.Gold));
        b.AddThemeStyleboxOverride("pressed", Sb(new Color(0.16f, 0.13f, 0.05f, 0.95f), RivalPanels.Gold));
        b.Pressed += press;
        parent.AddChild(b);
        return b;
    }

    Label PLbl(Control parent, string text, int size, Color c)
    {
        var l = new Label { Text = text, AutowrapMode = TextServer.AutowrapMode.WordSmart, CustomMinimumSize = new Vector2(300, 0) };
        l.AddThemeFontSizeOverride("font_size", size); l.AddThemeColorOverride("font_color", c);
        parent.AddChild(l);
        return l;
    }

    void BuildPanel()
    {
        var bg = new PanelContainer { Position = new Vector2(W - 340, 12) };
        var sb = new StyleBoxFlat { BgColor = new Color(0.02f, 0.02f, 0.03f, 0.9f), ContentMarginLeft = 10, ContentMarginRight = 10, ContentMarginTop = 10, ContentMarginBottom = 10 };
        sb.SetCornerRadiusAll(12);
        bg.AddThemeStyleboxOverride("panel", sb);
        layer.AddChild(bg);
        bg.Position = new Vector2(GetViewportRect().Size.X - 336, 12);
        panel = new VBoxContainer(); panel.AddThemeConstantOverride("separation", 3);
        bg.AddChild(panel);
        PLbl(panel, "MENU LAB   (H hides)", 17, RivalPanels.Gold);
        for (int i = 0; i < Rows.Length; i++) { int k = i; rowButtons.Add(PBtn(panel, "", () => { Select(k); Play(0); }, 310)); }
        panel.AddChild(new HSeparator());
        rowTitle = PLbl(panel, "", 14, RivalPanels.Gold);
        for (int v = 0; v < 5; v++) { int k = v; varButtons.Add(PBtn(panel, "", () => Play(k), 310)); }
        var row = new HBoxContainer(); row.AddThemeConstantOverride("separation", 3); panel.AddChild(row);
        PLbl(row, "PICK", 14, RivalPanels.Gold).CustomMinimumSize = new Vector2(38, 0);
        for (int v = -1; v < 5; v++) { int k = v; pickButtons.Add(PBtn(row, v < 0 ? "none" : ((char)('A' + v)).ToString(), () => SetPick(k), v < 0 ? 52 : 34)); }
        note = new LineEdit { PlaceholderText = "Comment: what to keep or change...", CustomMinimumSize = new Vector2(310, 32) };
        note.TextChanged += t => { Cur().Note = t; Save(); };
        note.TextSubmitted += _ => note.ReleaseFocus();
        panel.AddChild(note);
        var tools = new HBoxContainer(); tools.AddThemeConstantOverride("separation", 3); panel.AddChild(tools);
        PBtn(tools, "Replay", () => Play(lastVar), 100);
        PBtn(tools, "Copy all", () => { DisplayServer.ClipboardSet(Summary()); saved.Text = "Copied"; }, 100);
        saved = PLbl(panel, "", 12, new Color(1, 1, 1, 0.55f));
    }

    Pick Cur() => picks.TryGetValue(Rows[sel].Name, out var p) ? p : picks[Rows[sel].Name] = new Pick();

    void Select(int i)
    {
        sel = Math.Clamp(i, 0, Rows.Length - 1);
        for (int r = 0; r < Rows.Length; r++)
        {
            var p = picks.TryGetValue(Rows[r].Name, out var x) ? x : null;
            rowButtons[r].Text = Rows[r].Name + (p is { Choice: >= 0 } ? $"   ✓ {(char)('A' + p.Choice)}" : "") + (p is { Note.Length: > 0 } ? "  ✎" : "");
            rowButtons[r].Modulate = r == sel ? Colors.White : new Color(1, 1, 1, 0.6f);
        }
        var vars = Rows[sel].Vars;
        rowTitle.Text = Rows[sel].Name.ToUpperInvariant();
        for (int v = 0; v < varButtons.Count; v++)
        {
            varButtons[v].Visible = v < vars.Length;
            if (v < vars.Length) varButtons[v].Text = $"{(char)('A' + v)}   {vars[v]}";
        }
        for (int b = 0; b < pickButtons.Count; b++)
        {
            pickButtons[b].Visible = b - 1 < vars.Length;
            pickButtons[b].Modulate = Cur().Choice == b - 1 ? RivalPanels.Gold : new Color(1, 1, 1, 0.6f);
        }
        note.SetBlockSignals(true); note.Text = Cur().Note; note.SetBlockSignals(false);
    }

    void SetPick(int v) { Cur().Choice = v; Save(); Select(sel); }

    void Load()
    {
        try
        {
            if (!Godot.FileAccess.FileExists(File)) return;
            using var f = Godot.FileAccess.Open(File, Godot.FileAccess.ModeFlags.Read);
            picks = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, Pick>>(f.GetAsText()) ?? new();
        }
        catch { picks = new(); }
    }

    void Save()
    {
        using (var f = Godot.FileAccess.Open(File, Godot.FileAccess.ModeFlags.Write)) f?.StoreString(System.Text.Json.JsonSerializer.Serialize(picks));
        try { System.IO.File.WriteAllText(TextFile, Summary()); } catch { }
        saved.Text = "Saved to game/menu_lab_picks_round4.txt";
    }

    static string Summary()
    {
        var sb = new System.Text.StringBuilder("MENU LAB PICKS\n");
        foreach (var (name, vars) in Rows)
        {
            var p = picks.TryGetValue(name, out var x) ? x : null;
            sb.Append($"{name}: {(p is { Choice: >= 0 } ? $"{(char)('A' + p.Choice)} ({vars[p.Choice]})" : "-")}");
            if (p is { Note.Length: > 0 }) sb.Append($"  // {p.Note}");
            sb.Append('\n');
        }
        return sb.ToString();
    }

    // ---------------------------------------------------------------- stage

    /// Wipe the last sample (keeps the panel layer and MenuBase's audio) and draw option v of the open row.
    void Play(int v)
    {
        var vars = Rows[sel].Vars;
        if (v < 0 || v >= vars.Length) return;
        lastVar = v;
        foreach (var c in GetChildren()) if (c != layer && c is not AudioStreamPlayer) { RemoveChild(c); c.QueueFree(); }
        switch (sel)
        {
        }
        Select(sel);
    }

    // ---------------------------------------------------------------- shared pieces for the samples



    /// A card-like panel that suits its side of the rift: white paper on the light side, smoked glass on the dark.
    protected Panel Sheet(Control host, Rect2 r, bool on = false, int radius = 14)
    {
        bool light = OnLight(host.GlobalPosition + r.GetCenter());
        var s = light
            ? Box(on ? new Color(1f, 0.97f, 0.9f) : new Color(1, 1, 1, 0.85f), on ? GoldC : new Color(0, 0, 0, 0.1f), on ? 2.5f : 1, radius, 18, new Color(0.1f, 0.1f, 0.15f, 0.16f), new Vector2(0, 6))
            : Box(on ? new Color(0.16f, 0.13f, 0.08f, 0.95f) : new Color(0.1f, 0.1f, 0.13f, 0.82f), on ? GoldLight : new Color(1, 1, 1, 0.1f), on ? 2.5f : 1, radius, 18, new Color(0, 0, 0, 0.4f), new Vector2(0, 6));
        return Plate(host, r, s);
    }



    static Vector2[] Octagon(float x, float y, float w, float h, float cut) => new[]
    {
        new Vector2(x + cut, y), new Vector2(x + w - cut, y), new Vector2(x + w, y + cut), new Vector2(x + w, y + h - cut),
        new Vector2(x + w - cut, y + h), new Vector2(x + cut, y + h), new Vector2(x, y + h - cut), new Vector2(x, y + cut), new Vector2(x + cut, y),
    };

    /// One button face in style `st`: 0 gold plaque, 1 chamfered frame, 2 banner between rules, 3 arrow badge.
    static void DrawBtn(Control c, int st, float w, float h, bool light, bool primary, bool hov)
    {
        Color gold = light ? GoldDeep : GoldLight;
        Color fill = primary ? (light ? (hov ? GoldC : Ink) : hov ? GoldLight : GoldC)
            : hov ? GoldC with { A = light ? 0.18f : 0.22f } : light ? new Color(1, 1, 1, 0.6f) : new Color(0, 0, 0, 0.4f);
        Color edge = primary ? GoldC : gold with { A = hov ? 1f : 0.75f };
        switch (st)
        {
            case 0:
                c.DrawStyleBox(Box(fill, edge, primary ? 2 : 1.5f, 5, hov ? 16 : 6, new Color(0, 0, 0, light ? 0.12f : 0.4f), new Vector2(0, 3)), new Rect2(0, 0, w, h));
                Diamond(c, new Vector2(0, h / 2), hov ? 7 : 5.5f, GoldC);
                Diamond(c, new Vector2(w, h / 2), hov ? 7 : 5.5f, GoldC);
                break;
            case 1:
            {
                float cut = h * 0.3f;
                c.DrawColoredPolygon(Octagon(0, 0, w, h, cut)[..8], fill);
                c.DrawPolyline(Octagon(0, 0, w, h, cut), edge, primary ? 2 : 1.5f, true);
                c.DrawPolyline(Octagon(5, 5, w - 10, h - 10, cut - 2), edge with { A = 0.35f }, 1, true);
                break;
            }
            case 2:
            {
                if (primary || hov) c.DrawRect(new Rect2(0, 7, w, h - 14), primary ? fill : GoldC with { A = 0.16f });
                foreach (float y in new[] { 4f, h - 4 })
                {
                    float inset = hov ? 0 : w * 0.06f;
                    c.DrawLine(new Vector2(inset, y), new Vector2(w - inset, y), edge, 1.5f, true);
                    Diamond(c, new Vector2(inset, y), 4.5f, GoldC);
                    Diamond(c, new Vector2(w - inset, y), 4.5f, GoldC);
                }
                break;
            }
            default:
            {
                var bgc = primary ? fill : light ? new Color(1, 1, 1, 0.65f) : new Color(0, 0, 0, 0.4f);
                var rim = primary ? GoldC : (light ? Ink : Silver) with { A = hov ? 0.6f : 0.3f };
                c.DrawStyleBox(Box(bgc, rim, 1.5f, (int)(h / 2), hov ? 14 : 6, new Color(0, 0, 0, light ? 0.12f : 0.4f), new Vector2(0, 3)), new Rect2(0, 0, w, h));
                var cc = new Vector2(w - h / 2, h / 2);
                c.DrawCircle(cc, h * 0.34f, primary ? (light && !hov ? GoldC : Ink) : hov ? GoldC : gold);
                float a = h * 0.1f, dx = hov ? 2 : 0;
                var arrow = primary ? (light && !hov ? Ink : GoldLight) : hov ? Ink : Colors.White;
                c.DrawPolyline(new[] { cc + new Vector2(-a * 0.6f + dx, -a), cc + new Vector2(a * 0.6f + dx, 0), cc + new Vector2(-a * 0.6f + dx, a) }, arrow, 2.5f, true);
                break;
            }
        }
    }

    /// A button in the chosen style. `primary` = the one main action on a screen (DUEL, START DUEL, CLAIM).
    protected Control Btn(Control host, string text, Vector2 centre, Action click, bool primary = false, int size = 20, float width = 0)
    {
        int st = BtnStyle;
        bool light = OnLight(host.GlobalPosition + centre);
        int fs = Z(size);
        float tw = Title.GetStringSize(Lang.Tr(text), HorizontalAlignment.Left, -1, fs).X;
        float h = MathF.Round(fs * 2.3f + (primary ? 6 : 0)), w = Math.Max(width, tw + h * (st == 3 ? 2.0f : 1.5f));
        var root = new Control { Position = centre - new Vector2(w, h) / 2, Size = new Vector2(w, h), PivotOffset = new Vector2(w, h) / 2 };
        host.AddChild(root);
        bool hov = false;
        var face = new Control { Size = root.Size, MouseFilter = MouseFilterEnum.Ignore };
        root.AddChild(face);
        face.Draw += () => DrawBtn(face, st, w, h, light, primary, hov);
        Color gold = light ? GoldDeep : GoldLight, ink = light ? Ink : Silver;
        var l = Mid(root, text, Title, size, Colors.White, new Rect2(st == 3 ? -h * 0.4f : 0, 0, w, h));
        void Paint() => l.AddThemeColorOverride("font_color", primary ? (light && !hov ? GoldLight : Ink) : hov ? gold : ink);
        Paint();
        Click(root, () =>
        {
            UiSound(true);
            var t = CreateTween();
            t.TweenProperty(root, "scale", new Vector2(0.96f, 0.96f), 0.06);
            t.TweenProperty(root, "scale", Vector2.One, 0.1);
            t.TweenCallback(Callable.From(click));
        }, on =>
        {
            if (on) UiSound(false);
            hov = on; Paint(); face.QueueRedraw();
            Ink_.Only(this, root).TweenProperty(root, "scale", on ? new Vector2(1.03f, 1.03f) : Vector2.One, 0.14).SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
        });
        return root;
    }


    /// A rounded progress bar.
    protected void Bar(Control host, Vector2 at, float w, float frac, bool light, float h = 8, Color? fill = null)
    {
        Plate(host, new Rect2(at, new Vector2(w, h)), Box(light ? new Color(0, 0, 0, 0.1f) : new Color(1, 1, 1, 0.1f), default, 0, 99));
        if (frac > 0) Plate(host, new Rect2(at, new Vector2(Math.Max(h, w * Math.Min(1, frac)), h)), Box(fill ?? (light ? GoldDeep : GoldLight), default, 0, 99));
    }


    protected static string NameOf(string k) => k == Tournament.You ? Settings.PlayerName.ToUpperInvariant() : Tournament.Get(k).Name;


    /// Screen header used by most samples: back link, title, gold hairline.
    void Head(string title, bool light = true)
    {
        Back();
        Put(this, title, Title, 38, light ? Ink : Silver, new Vector2(80, 58));
        Hair(this, new Vector2(82, 106), 140, GoldC);
    }

    // ---------------------------------------------------------------- row 1: buttons

    void Buttons(int v)
    {
        Rift(0.5f, 0.5f);
        Head("BUTTONS");
        string[] desc = { "A thin gold frame with a diamond at each end, like a plaque. The main action is solid ink.",
            "Cut corners and a double rule, like a card frame. The main action is solid ink.",
            "The word between two gold rules with diamond ends. On hover the rules widen and the band fills.",
            "A soft capsule with a gold arrow badge, so it always reads as 'go'. The main action is solid ink." };
        Put(this, desc[v], Body, 21, Graphite, new Vector2(84, 140), new Vector2(W * 0.4f, 60)).AutowrapMode = TextServer.AutowrapMode.WordSmart;
        foreach (bool light in new[] { true, false })
        {
            float cx = light ? W * 0.25f : W * 0.75f;
            Put(this, light ? "ON THE LIGHT SIDE" : "ON THE DARK SIDE", Thin, 14, light ? Graphite : Mist, new Vector2(cx - 200, 260), new Vector2(400, 20), HorizontalAlignment.Center);
            Btn(this, "START DUEL", new Vector2(cx, 350), () => Flash("START DUEL"), primary: true, size: 26);
            Btn(this, "EDIT DECK", new Vector2(cx - 130, 470), () => Flash("EDIT DECK"));
            Btn(this, "COPY", new Vector2(cx + 120, 470), () => Flash("COPY"));
            Btn(this, "RENAME", new Vector2(cx - 130, 560), () => Flash("RENAME"), size: 17);
            Btn(this, "DELETE", new Vector2(cx + 120, 560), () => Flash("DELETE"), size: 17);
            Btn(this, "CLAIM 300 DP", new Vector2(cx, 670), () => Flash("CLAIMED"), primary: true, size: 18);
        }
        Put(this, "Hover and click them. The pick here is used by every other row.", Thin, 14, Graphite, new Vector2(84, H - 70));
    }
}
