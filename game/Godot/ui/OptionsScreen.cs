using Godot;
using PoC.Engine;

namespace PoC.Game.UI;

/// Options (Chaos Rift sample B, locked 2026-11-08): each setting straddles the rift, its name in the light, its
/// choice in the dark. Your profile bottom left: click the name to rename, the picture or frame word to choose.
public partial class OptionsScreen : RiftMenu
{
    readonly List<(Label L, ColorRect A, ColorRect B, float Y)> rows = new();
    readonly List<Action> refresh = new();
    Control? picker;

    protected override void OnBack()
    {
        if (picker != null) { ClosePicker(); return; }
        Session.Save();
        Transition.Go(this, "res://ui/MainMenu.tscn");
    }
    protected override int ItemCount => rows.Count;

    protected override void Build()
    {
        Rift(0.53f, 0.47f);
        Back();
        Put(this, "OPTIONS", Title, 60, Ink, new Vector2(80, 70));
        Hair(this, new Vector2(82, 146), 200, GoldC);
        float y = 300;
        Row(ref y, "BAN LIST", DeckRules.Formats.Select(f => f.Name).ToArray(), () => Settings.Format, i => Settings.Format = i);
        Row(ref y, "DUEL SPEED", Settings.Speeds.Select(s => s.Name).ToArray(), () => Settings.Speed, i => Settings.Speed = i);
        Slider(ref y, "MUSIC", () => Settings.Music, v => { Settings.Music = v; Settings.Apply(false); });
        Slider(ref y, "SOUND", () => Settings.Sound, v => { Settings.Sound = v; Settings.Apply(false); });
        Row(ref y, "SCREEN", new[] { "FULLSCREEN", "WINDOW" }, () => Settings.Fullscreen ? 0 : 1, i => { Settings.Fullscreen = i == 0; Settings.Apply(); });
        Profile();
        Button(this, "DONE", new Vector2(W - 240, H - 100), OnBack);
        if (Demo.Arg("state") == "pictures") GetTree().CreateTimer(0.4).Timeout += Pictures;
        if (Demo.Arg("state") == "frames") GetTree().CreateTimer(0.4).Timeout += Frames;
    }

    /// The label in the light, right-aligned to the crack, with two diamonds that show on focus.
    Label Label(float y, string s)
    {
        float cx = CrackX(y + 14);
        var l = Put(this, s, Title, 30, Ink, new Vector2(cx - 760, y - 6), new Vector2(700, 44), HorizontalAlignment.Right);
        var a = Pip(this, new Vector2(cx - 30, y + 15), 10); var b = Pip(this, new Vector2(cx + 30, y + 15), 10);
        a.Visible = b.Visible = false;
        int k = rows.Count;
        rows.Add((l, a, b, y));
        var hit = new Control { Position = new Vector2(cx - 760, y - 20), Size = new Vector2(1400, 70) };
        AddChild(hit); MoveChild(hit, 1);
        hit.MouseFilter = MouseFilterEnum.Pass;
        hit.MouseEntered += () => SetFocus(k);
        return l;
    }

    void Row(ref float y, string name, string[] opts, Func<int> get, Action<int> set)
    {
        Label(y, name);
        float x = CrackX(y + 14) + 70;
        var setters = new List<Action<bool>>();
        for (int i = 0; i < opts.Length; i++)
        {
            int k = i;
            var (w, s) = Word(this, opts[i], new Vector2(x, y - 2), 26, get() == i, () => { set(k); UiSound(true); foreach (var f in refresh) f(); });
            setters.Add(s);
            x += w + 46;
        }
        refresh.Add(() => { for (int i = 0; i < setters.Count; i++) setters[i](get() == i); });
        y += 112;
    }

    void Slider(ref float y, string name, Func<float> get, Action<float> set)
    {
        Label(y, name);
        float x = CrackX(y + 14) + 70, w = 460, ly = y + 16;
        Hair(this, new Vector2(x, ly), w, Silver * new Color(1, 1, 1, 0.25f));
        var fill = Hair(this, new Vector2(x, ly - 0.5f), w * get(), GoldC, 2.5f);
        var knob = Pip(this, new Vector2(x + w * get(), ly + 0.5f), 16);
        var val = Put(this, $"{(int)(get() * 100)}", Thin, 24, Silver, new Vector2(x + w + 24, ly - 16));
        var hit = new Control { Position = new Vector2(x - 10, ly - 22), Size = new Vector2(w + 20, 44), MouseDefaultCursorShape = CursorShape.PointingHand };
        AddChild(hit);
        void At(float px)
        {
            float v = Mathf.Clamp((px - 10) / w, 0, 1);
            set(v);
            fill.Size = new Vector2(w * v, 2.5f); knob.Position = new Vector2(x + w * v - 8, ly + 0.5f - 8); val.Text = $"{(int)(v * 100)}";
        }
        hit.GuiInput += e =>
        {
            if (e is InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: true } mb) At(mb.Position.X);
            else if (e is InputEventMouseMotion { ButtonMask: MouseButtonMask.Left } mm) At(mm.Position.X);
        };
        y += 112;
    }

    protected override void OnFocus(int old, int now)
    {
        for (int i = 0; i < rows.Count; i++)
        {
            var (l, a, b, y) = rows[i];
            bool on = i == now;
            a.Visible = b.Visible = on;
            l.AddThemeColorOverride("font_color", on ? GoldDeep : Ink);
            if (on) bg.P("focus_y", (y + 14) / H);
        }
    }

    // ---------------------------------------------------------------- profile

    Control? profile;

    void Profile()
    {
        profile?.QueueFree();
        profile = new Control { Position = new Vector2(80, H - 200), Size = new Vector2(800, 130) };
        AddChild(profile);
        var pic = Cosmetics.Framed(Avatar, P.Frame, 110);
        profile.AddChild(pic);
        var hit = new Control { Size = new Vector2(110, 110), TooltipText = "Change picture" };
        profile.AddChild(hit);
        Click(hit, Pictures);
        var name = new LineEdit { Text = Settings.PlayerName, Position = new Vector2(124, 8), Size = new Vector2(520, 56), Flat = true, MaxLength = Settings.NameMax, SelectAllOnFocus = true, PlaceholderText = "Your name", TooltipText = "Click to rename" };
        name.AddThemeFontOverride("font", Title); name.AddThemeFontSizeOverride("font_size", 34);
        name.AddThemeColorOverride("font_color", Ink); name.AddThemeColorOverride("caret_color", Ink);
        name.AddThemeColorOverride("selection_color", GoldC with { A = 0.35f });
        name.TextChanged += t => { if (t.Trim().Length > 0) Settings.PlayerName = t.Trim(); };
        name.TextSubmitted += _ => name.ReleaseFocus();
        name.FocusExited += () => { name.Text = Settings.PlayerName; Session.Save(); };
        profile.AddChild(name);
        float x = 132;
        foreach (var (s, act) in new (string, Action)[] { ("RENAME", () => name.GrabFocus()), ("PICTURE", Pictures), ("FRAME", Frames) })
        {
            var l = Put(profile, s, Title, 17, Graphite, new Vector2(x, 72));
            Click(l, act, h => l.AddThemeColorOverride("font_color", h ? GoldDeep : Graphite));
            x += Title.GetStringSize(s, HorizontalAlignment.Left, -1, Z(17)).X + 34;
        }
    }

    /// A grid of round choices over a dark sheet; picking one applies it and closes.
    void Picker(string title, int n, float d, int cols, Func<int, Control> make, Func<int, bool> chosen, Action<int> pick, string? note = null)
    {
        ClosePicker();
        picker = new Control { Size = new Vector2(W, H), ZIndex = 20 };
        var shade = new ColorRect { Size = picker.Size, Color = new Color(0.02f, 0.02f, 0.03f, 0.95f), MouseFilter = MouseFilterEnum.Stop };
        shade.GuiInput += e => { if (e is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Right }) ClosePicker(); };
        shade.SetMeta("rmb", true); picker.AddChild(shade);
        AddChild(picker);
        Put(picker, title, Title, 40, Silver, new Vector2(0, 110), new Vector2(W, 60), HorizontalAlignment.Center);
        Hair(picker, new Vector2(W / 2 - 100, 172), 200, GoldC);
        const float gap = 26;
        cols = Math.Min(cols, n);
        float x0 = (W - (cols * d + (cols - 1) * gap)) / 2, y0 = 240;
        for (int k = 0; k < n; k++)
        {
            int i = k;
            var cell = make(i);
            cell.Position = new Vector2(x0 + k % cols * (d + gap), y0 + k / cols * (d + gap + 20));
            cell.PivotOffset = new Vector2(d, d) / 2;
            picker.AddChild(cell);
            if (chosen(i)) Pip(picker, cell.Position + new Vector2(d / 2, d + 12), 9);
            Click(cell, () => { pick(i); Session.Save(); ClosePicker(); Profile(); }, h => Ink_.Only(this, cell).TweenProperty(cell, "scale", h ? new Vector2(1.08f, 1.08f) : Vector2.One, 0.12));
        }
        if (note != null) Put(picker, note, Thin, 20, Mist, new Vector2(0, H - 150), new Vector2(W, 30), HorizontalAlignment.Center);
        Button(picker, "BACK", new Vector2(W / 2, H - 90), ClosePicker, 22);
    }

    void ClosePicker() { picker?.QueueFree(); picker = null; }

    void Pictures()
    {
        var ch = Settings.PictureChoices.ToList();
        Picker("PICTURE", ch.Count, 110, 8, i =>
        {
            var c = new Control { Size = new Vector2(110, 110), TooltipText = Settings.Pictures[ch[i]] };
            Pic(c, CardArt.Picture(Settings.PictureCard(ch[i])!), new Vector2(5, 5), new Vector2(100, 100)).Material = Kit.RoundedMat(1, 0.5f);
            return c;
        }, i => ch[i] == Settings.Picture, i => Settings.Picture = ch[i]);
    }

    void Frames()
    {
        var owned = Shop.Frames.Where(f => Shop.Owns(P, f)).ToList();
        Picker("FRAME", owned.Count, 150, 6, i =>
        {
            var c = new Control { Size = new Vector2(150, 150), TooltipText = owned[i].Name };
            c.AddChild(Cosmetics.Framed(Avatar, owned[i].Key, 150));
            return c;
        }, i => owned[i].Key == P.Frame, i => Shop.Equip(P, owned[i]), owned.Count < Shop.Frames.Length ? "MORE FRAMES IN THE SHOP" : null);
    }
}
