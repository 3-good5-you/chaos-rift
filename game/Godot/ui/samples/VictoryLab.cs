using Godot;
using PoC.Engine;

namespace PoC.Game.UI;

/// Victory Lab (2026-11-09): every end-of-duel and unlock moment in one place. Pick a row on the left, play its
/// options, pick one and leave a comment (saved to user://victory_lab_picks_round2.json and game/victory_picks_round2.txt).
/// The samples are live: the prize buttons deal the reward cards, click any card to see it large; other buttons
/// replay. Keys: up/down rows, 1-9 options, R replay, H hide the panel.
/// Run: res://ui/samples/VictoryLab.tscn -- --unlockall   (`--labplay=row:option` and `--labhide` for screenshots)
public partial class VictoryLab : Control
{
    static readonly (string Name, (string Label, string Key)[] Vars)[] Rows =
    {
        // Round 1 picked and built into Results (2026-11-09): split win/lose, podium, road, tiles, deck unlock.
        // Legend: no special screen (the crack was rejected). Round 2: the new playmat.
        ("1  New playmat won", new[] { ("Unrolls behind a gold edge", "mat2"), ("Two halves meet at the rift", "mat3"), ("Top and bottom halves meet (like the field)", "mat6"), ("Face down, flips over", "mat4"), ("Drawn in gold, then fills in", "mat5") }),
    };

    sealed class Pick { public int Choice { get; set; } = -1; public string Note { get; set; } = ""; }
    static Dictionary<string, Pick> picks = new();
    static int sel, lastVar;
    static bool loaded, played;

    Control? stage;
    VBoxContainer panel = null!;
    readonly List<Button> rowButtons = new(), varButtons = new(), pickButtons = new();
    Label title = null!, saved = null!;
    LineEdit note = null!;
    const string File = "user://victory_lab_picks_round2.json";
    static string Text => ProjectSettings.GlobalizePath("res://").TrimEnd('/') + "/../victory_picks_round2.txt";

    public override void _Ready()
    {
        SetAnchorsPreset(LayoutPreset.FullRect);
        Session.Load();
        if (!loaded) { Load(); loaded = true; }
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

    // ---------------------------------------------------------------- panel

    Button Btn(Control parent, string text, Action press, float w, float h = 34)
    {
        var b = new Button { Text = text, Alignment = HorizontalAlignment.Left, CustomMinimumSize = new Vector2(w, h), FocusMode = FocusModeEnum.None, ClipText = true };
        b.AddThemeFontSizeOverride("font_size", 16);
        StyleBoxFlat Sb(Color bg, Color edge) { var s = new StyleBoxFlat { BgColor = bg, BorderColor = edge, ContentMarginLeft = 10 }; s.SetBorderWidthAll(1); s.SetCornerRadiusAll(8); return s; }
        b.AddThemeStyleboxOverride("normal", Sb(new Color(0.04f, 0.05f, 0.08f, 0.9f), new Color(RivalPanels.Gold, 0.3f)));
        b.AddThemeStyleboxOverride("hover", Sb(new Color(0.09f, 0.1f, 0.15f, 0.95f), RivalPanels.Gold));
        b.AddThemeStyleboxOverride("pressed", Sb(new Color(0.16f, 0.13f, 0.05f, 0.95f), RivalPanels.Gold));
        b.Pressed += press;
        parent.AddChild(b);
        return b;
    }

    Label Lbl(Control parent, string text, int size, Color c)
    {
        var l = new Label { Text = text, AutowrapMode = TextServer.AutowrapMode.WordSmart, CustomMinimumSize = new Vector2(330, 0) };
        l.AddThemeFontSizeOverride("font_size", size); l.AddThemeColorOverride("font_color", c);
        parent.AddChild(l);
        return l;
    }

    void BuildPanel()
    {
        var bg = new PanelContainer { Position = new Vector2(16, 16), ZIndex = 100 };
        var sb = new StyleBoxFlat { BgColor = new Color(0.02f, 0.02f, 0.03f, 0.88f), ContentMarginLeft = 12, ContentMarginRight = 12, ContentMarginTop = 12, ContentMarginBottom = 12 };
        sb.SetCornerRadiusAll(12);
        bg.AddThemeStyleboxOverride("panel", sb);
        AddChild(bg);
        panel = new VBoxContainer(); panel.AddThemeConstantOverride("separation", 4);
        bg.AddChild(panel);
        Lbl(panel, "VICTORY LAB", 20, RivalPanels.Gold);
        for (int i = 0; i < Rows.Length; i++) { int k = i; rowButtons.Add(Btn(panel, "", () => { Select(k); Play(0); }, 340)); }
        panel.AddChild(new HSeparator());
        title = Lbl(panel, "", 16, RivalPanels.Gold);
        for (int v = 0; v < 4; v++) { int k = v; varButtons.Add(Btn(panel, "", () => Play(k), 340)); }
        var row = new HBoxContainer(); row.AddThemeConstantOverride("separation", 4); panel.AddChild(row);
        Lbl(row, "PICK", 16, RivalPanels.Gold).CustomMinimumSize = new Vector2(44, 0);
        for (int v = -1; v < 4; v++) { int k = v; pickButtons.Add(Btn(row, v < 0 ? "none" : ((char)('A' + v)).ToString(), () => SetPick(k), v < 0 ? 60 : 34)); }
        note = new LineEdit { PlaceholderText = "Comment: what to keep or change...", CustomMinimumSize = new Vector2(340, 34) };
        note.TextChanged += t => { Cur().Note = t; Save(); };
        note.TextSubmitted += _ => note.ReleaseFocus();
        panel.AddChild(note);
        var tools = new HBoxContainer(); tools.AddThemeConstantOverride("separation", 4); panel.AddChild(tools);
        Btn(tools, "Replay (R)", () => Play(lastVar), 110);
        Btn(tools, "Hide (H)", () => bg.Visible = false, 100);
        Btn(tools, "Copy all", () => { DisplayServer.ClipboardSet(Summary()); saved!.Text = "Copied"; }, 100);
        saved = Lbl(panel, "", 13, new Color(1, 1, 1, 0.55f));
    }

    Pick Cur() => picks.TryGetValue(Rows[sel].Name, out var p) ? p : picks[Rows[sel].Name] = new Pick();

    void Select(int i)
    {
        sel = Math.Clamp(i, 0, Rows.Length - 1);
        for (int r = 0; r < Rows.Length; r++)
        {
            var p = picks.TryGetValue(Rows[r].Name, out var x) ? x : null;
            rowButtons[r].Text = Rows[r].Name + (p is { Choice: >= 0 } ? $"   ✓ {(char)('A' + p.Choice)}" : "") + (p is { Note.Length: > 0 } ? "  ✎" : "");
            rowButtons[r].Modulate = r == sel ? Colors.White : new Color(1, 1, 1, 0.65f);
        }
        var vars = Rows[sel].Vars;
        title.Text = Rows[sel].Name.ToUpperInvariant();
        for (int v = 0; v < varButtons.Count; v++)
        {
            varButtons[v].Visible = v < vars.Length;
            if (v < vars.Length) varButtons[v].Text = $"{(char)('A' + v)}   {vars[v].Label}";
        }
        for (int b = 0; b < pickButtons.Count; b++)
        {
            pickButtons[b].Visible = b - 1 < vars.Length;
            pickButtons[b].Modulate = Cur().Choice == b - 1 ? RivalPanels.Gold : new Color(1, 1, 1, 0.6f);
        }
        note.SetBlockSignals(true); note.Text = Cur().Note; note.SetBlockSignals(false);
    }

    void SetPick(int v) { Cur().Choice = v; Save(); Select(sel); }

    // ---------------------------------------------------------------- the sample on stage

    void Play(int v)
    {
        var vars = Rows[sel].Vars;
        if (v < 0 || v >= vars.Length) return;
        lastVar = v;
        stage?.QueueFree();
        var key = vars[v].Key;
        if (key == "unlock")
        {
            // the real screen; its buttons come back here
            var k = Array.FindIndex(DeckRules.Starters, s => s.Key.StartsWith("kaiba/"));
            var (m, x) = CardDb.Shared.Deck(DeckRules.Starters[k].Key);
            Session.LastStarter = new Deck(DeckRules.Starters[k].Name, m, x);
            Session.Selected = 1;
            Session.AfterUnlock = "res://ui/samples/VictoryLab.tscn";
            stage = (Control)GD.Load<PackedScene>("res://ui/DeckUnlock.tscn").Instantiate();
        }
        else stage = new VictorySamples { Show = key, Replay = () => Play(lastVar) };
        AddChild(stage);
        MoveChild(stage, 0);   // under the panel
    }

    public override void _UnhandledInput(InputEvent e)
    {
        if (e is not InputEventKey { Pressed: true, Echo: false } k || note.HasFocus()) return;
        if (k.Keycode == Key.Up) Select(sel - 1);
        else if (k.Keycode == Key.Down) Select(sel + 1);
        else if (k.Keycode == Key.R) Play(lastVar);
        else if (k.Keycode == Key.H) { var p = panel.GetParent<Control>(); p.Visible = !p.Visible; }
        else if (k.Keycode is >= Key.Key1 and <= Key.Key9) Play((int)(k.Keycode - Key.Key1));
    }

    // ---------------------------------------------------------------- picks

    void Load()
    {
        if (!Godot.FileAccess.FileExists(File)) return;
        using var f = Godot.FileAccess.Open(File, Godot.FileAccess.ModeFlags.Read);
        picks = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, Pick>>(f.GetAsText()) ?? new();
    }

    void Save()
    {
        using (var f = Godot.FileAccess.Open(File, Godot.FileAccess.ModeFlags.Write)) f.StoreString(System.Text.Json.JsonSerializer.Serialize(picks));
        using (var t = Godot.FileAccess.Open(Text, Godot.FileAccess.ModeFlags.Write)) t?.StoreString(Summary());
        saved.Text = "Saved to game/victory_picks_round2.txt";
    }

    static string Summary() => string.Join("\n", Rows.Select(r =>
    {
        var p = picks.TryGetValue(r.Name, out var x) ? x : new Pick();
        string pick = p.Choice >= 0 && p.Choice < r.Vars.Length ? $"{(char)('A' + p.Choice)} — {r.Vars[p.Choice].Label}" : "(none)";
        return $"{r.Name}: {pick}{(p.Note.Length > 0 ? $"   // {p.Note}" : "")}";
    }));
}
