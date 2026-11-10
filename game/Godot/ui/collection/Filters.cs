using Godot;
using PoC.Engine;

namespace PoC.Game.UI;

/// Search (name or card text) and the simple filters (monster/spell/trap, attribute, level, type, and the
/// category: Normal / Effect / Fusion / Ritual, or a spell/trap's Continuous, Equip, ...) as one row of chips.
/// `Changed` fires on every change; `Match` tests a card.
public sealed class Filters
{
    public string Query = "";
    public Kind? Kind;
    public string? Attribute, Race, Frame;
    public int Level;                    // 0 = any
    public event Action? Changed;

    public bool Match(CardData d)
    {
        if (Query.Length > 0 && !d.Name.Contains(Query, StringComparison.OrdinalIgnoreCase) && !d.Desc.Contains(Query, StringComparison.OrdinalIgnoreCase)) return false;
        if (Kind != null && d.Kind != Kind) return false;
        if (Attribute != null && d.Attribute != Attribute) return false;
        if (Race != null && d.Race != Race) return false;
        if (Level > 0 && d.Level != Level) return false;
        if (Frame != null && d.Frame != Frame) return false;
        return true;
    }

    public static readonly string[] Races = CardDb.Shared.ByIdx.Values.Where(d => d.IsMonster && d.Race.Length > 0 && d.PackMask != 0)
        .Select(d => d.Race).Distinct().OrderBy(r => r).ToArray();

    /// Categories offered for a kind (all of them when no kind is chosen).
    static string[] Frames(Kind? k) => k switch
    {
        PoC.Engine.Kind.Monster => new[] { "Normal", "Effect", "Fusion", "Ritual" },
        PoC.Engine.Kind.Spell => new[] { "Normal", "Continuous", "Equip", "Field", "Quick-Play", "Ritual" },
        PoC.Engine.Kind.Trap => new[] { "Normal", "Continuous", "Counter" },
        _ => new[] { "Normal", "Effect", "Fusion", "Ritual", "Continuous", "Equip", "Field", "Quick-Play", "Counter" },
    };

    Control host = null!;
    LineEdit search = null!;
    readonly List<Panel> kinds = new();
    readonly List<Control> orbs = new();
    Panel lvChip = null!, typeChip = null!, catChip = null!;
    Panel? popup;

    /// Build the row at `pos` inside `parent` (wrapping to a second row past maxW); returns its height.
    public float Build(Control parent, Vector2 pos, float searchW = 300, bool compact = false, float maxW = 1e9f)
    {
        kinds.Clear(); orbs.Clear();   // built again when its screen rebuilds (the Shop after a purchase): keeps its state
        host = new Control { Position = pos, Size = new Vector2(10, 56), MouseFilter = Control.MouseFilterEnum.Ignore };
        parent.AddChild(host);
        float x = 0, h = 52;

        search = new LineEdit { Position = new Vector2(0, 2), Size = new Vector2(searchW, h - 4), PlaceholderText = compact ? "Search" : "Search name or text", TooltipText = "Searches card names and card text", ClearButtonEnabled = true, CaretBlink = true };
        var sb = Kit.Glass(new Color(1, 1, 1, 0.14f), 0, 14);
        sb.ContentMarginLeft = 20; sb.ContentMarginRight = 12; sb.ShadowSize = 0;
        var sf = (StyleBoxFlat)sb.Duplicate(); sf.BorderColor = Kit.Accent * new Color(1, 1, 1, 0.7f);
        search.AddThemeStyleboxOverride("normal", sb); search.AddThemeStyleboxOverride("focus", sf);
        search.AddThemeFontOverride("font", Kit.Ui); search.AddThemeFontSizeOverride("font_size", 22);
        search.AddThemeColorOverride("font_placeholder_color", new Color(1, 1, 1, 0.3f));
        search.Text = Query;
        search.TextChanged += t => { Query = t.Trim(); Changed?.Invoke(); };
        host.AddChild(search);
        x += searchW + 18;

        string[] names = compact ? new[] { "ALL", "MON", "SPELL", "TRAP" } : new[] { "ALL", "MONSTER", "SPELL", "TRAP" };
        float[] widths = compact ? new float[] { 64, 76, 86, 78 } : new float[] { 80, 128, 100, 92 };
        for (int i = 0; i < 4; i++)
        {
            int k = i;
            var c = Chips.Make(names[i], new Vector2(x, 2), new Vector2(widths[i], h - 4), () => SetKind(k == 0 ? null : (Kind)(k - 1)), 20);
            host.AddChild(c); kinds.Add(c);
            x += widths[i] + 8;
        }
        x += 14;
        float orb = compact ? h - 10 : h - 4, lvW = compact ? 92 : 112, typeW = compact ? 130 : 170, catW = compact ? 140 : 170;
        float y = 2, rowW = x;
        if (x + 6 * (orb + 2) + 16 + lvW + typeW + catW + 16 > maxW) { rowW = x; x = 0; y = h + 10; }

        foreach (var a in Kit.Attributes)
        {
            var at = a;
            var o = new Control { Position = new Vector2(x, y + (h - 4 - orb) / 2), Size = new Vector2(orb, orb), MouseFilter = Control.MouseFilterEnum.Stop, PivotOffset = new Vector2(orb, orb) / 2, TooltipText = a };
            o.Draw += () => Kit.Orb(o, o.Size / 2, o.Size.X * 0.4f, at, Attribute == null || Attribute == at ? 1f : 0.3f);
            o.GuiInput += e => { if (e is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left }) SetAttr(Attribute == at ? null : at); };
            o.MouseEntered += () => o.CreateTween().TweenProperty(o, "scale", new Vector2(1.15f, 1.15f), 0.12);
            o.MouseExited += () => o.CreateTween().TweenProperty(o, "scale", Vector2.One, 0.12);
            host.AddChild(o); orbs.Add(o);
            x += orb + 2;
        }
        x += 16;

        lvChip = Chips.Make("LEVEL", new Vector2(x, y), new Vector2(lvW, h - 4), () => Pick(lvChip, Enumerable.Range(1, 12).Select(i => i.ToString()).ToArray(), 6, s => SetLevel(s == null ? 0 : int.Parse(s))), 20);
        host.AddChild(lvChip);
        x += lvChip.Size.X + 8;
        typeChip = Chips.Make("TYPE", new Vector2(x, y), new Vector2(typeW, h - 4), () => Pick(typeChip, Races.Select(r => r.ToUpperInvariant()).ToArray(), 4, s => SetRace(s == null ? null : Races.First(r => r.ToUpperInvariant() == s))), 20);
        typeChip.GetNode<Label>("Text").ClipText = true;
        host.AddChild(typeChip);
        x += typeChip.Size.X + 8;
        catChip = Chips.Make("CATEGORY", new Vector2(x, y), new Vector2(catW, h - 4), () => Pick(catChip, Frames(Kind).Select(f => f.ToUpperInvariant()).ToArray(), 4, s => SetFrame(s == null ? null : Frames(null).Concat(Frames(PoC.Engine.Kind.Spell)).First(f => f.ToUpperInvariant() == s))), 20);
        catChip.GetNode<Label>("Text").ClipText = true;
        host.AddChild(catChip);
        x += catChip.Size.X;
        host.Size = new Vector2(Math.Max(x, rowW), y + h);
        Sync();
        return host.Size.Y;
    }

    public void SetKind(Kind? k)
    {
        Kind = k;
        if (k is PoC.Engine.Kind.Spell or PoC.Engine.Kind.Trap) { Attribute = null; Race = null; Level = 0; }
        if (Frame != null && !Frames(k).Contains(Frame)) Frame = null;
        Sync(); Changed?.Invoke();
    }
    /// Effect / Fusion pick monsters; Equip, Field, ... pick spells; Counter picks traps.
    public void SetFrame(string? f)
    {
        Frame = f;
        if (f != null && Kind == null)
            Kind = Frames(PoC.Engine.Kind.Monster).Contains(f) && f != "Normal" && f != "Ritual" ? PoC.Engine.Kind.Monster
                 : f == "Counter" ? PoC.Engine.Kind.Trap : f is "Equip" or "Field" or "Quick-Play" ? PoC.Engine.Kind.Spell : null;
        Sync(); Changed?.Invoke();
    }
    public void SetAttr(string? a) { Attribute = a; if (a != null) ToMonster(); Sync(); Changed?.Invoke(); }
    void ToMonster() { Kind = PoC.Engine.Kind.Monster; if (Frame != null && !Frames(Kind).Contains(Frame)) Frame = null; }
    public void SetLevel(int l) { Level = l; if (l > 0) ToMonster(); Sync(); Changed?.Invoke(); }
    public void SetRace(string? r) { Race = r; if (r != null) ToMonster(); Sync(); Changed?.Invoke(); }
    public void SetQuery(string q) { Query = q; search.Text = q; Changed?.Invoke(); }

    void Sync()
    {
        for (int i = 0; i < kinds.Count; i++) Chips.SetOn(kinds[i], i == 0 ? Kind == null : Kind == (Kind)(i - 1));
        foreach (var o in orbs) o.QueueRedraw();
        lvChip.GetNode<Label>("Text").Text = Level > 0 ? $"LV {Level}" : "LEVEL";
        Chips.SetOn(lvChip, Level > 0);
        typeChip.GetNode<Label>("Text").Text = Race?.ToUpperInvariant() ?? "TYPE";
        Chips.SetOn(typeChip, Race != null);
        catChip.GetNode<Label>("Text").Text = Frame?.ToUpperInvariant() ?? "CATEGORY";
        Chips.SetOn(catChip, Frame != null);
        bool spellTrap = Kind is PoC.Engine.Kind.Spell or PoC.Engine.Kind.Trap;
        foreach (var c in orbs.Cast<Control>().Append(lvChip).Append(typeChip)) c.CreateTween().TweenProperty(c, "modulate:a", spellTrap ? 0.35f : 1f, 0.15);
    }

    /// Small glass popup of option chips under `anchor`; picking the lit one (or ANY) clears.
    void Pick(Control anchor, string[] options, int cols, Action<string?> chosen)
    {
        Close();
        float cw = cols >= 6 ? 64 : 168, ch = 44, gap = 8;
        int rows = (options.Length + 1 + cols - 1) / cols;
        var size = new Vector2(cols * cw + (cols - 1) * gap + 32, rows * ch + (rows - 1) * gap + 32);
        var root = anchor.GetTree().CurrentScene as Control ?? host;
        var at = anchor.GlobalPosition + new Vector2(0, anchor.Size.Y + 10);
        at.X = Math.Min(at.X, root.Size.X - size.X - 24);
        // Full-screen catcher closes the popup on an outside click.
        var catcher = new Panel { Size = root.Size, MouseFilter = Control.MouseFilterEnum.Stop, ZIndex = 50 };
        catcher.AddThemeStyleboxOverride("panel", new StyleBoxEmpty());
        catcher.GuiInput += e => { if (e is InputEventMouseButton { Pressed: true }) Close(); };
        root.AddChild(catcher);
        popup = Kit.Box(catcher, new Rect2(at, size), Kit.Glass(Kit.Accent * new Color(1, 1, 1, 0.7f), 0.2f));
        popup.MouseFilter = Control.MouseFilterEnum.Stop;
        var all = options.Prepend("ANY").ToArray();
        for (int i = 0; i < all.Length; i++)
        {
            var s = all[i];
            var c = Chips.Make(s, new Vector2(16 + i % cols * (cw + gap), 16 + i / cols * (ch + gap)), new Vector2(cw, ch), () => { Close(); chosen(s == "ANY" ? null : s); }, cw < 100 ? 20 : 17);
            popup.AddChild(c);
        }
        popup.Modulate = new Color(1, 1, 1, 0); popup.Position += new Vector2(0, -8);
        var tw = popup.CreateTween().SetParallel();
        tw.TweenProperty(popup, "modulate:a", 1f, 0.15);
        tw.TweenProperty(popup, "position:y", at.Y, 0.2).SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
    }

    void Close()
    {
        popup?.GetParent()?.QueueFree();
        popup = null;
    }
}
