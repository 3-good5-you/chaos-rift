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
        float y = 260;
        Row(ref y, "LANGUAGE", Lang.All.Select(l => l.Name).ToArray(), () => Lang.Index, i => { Lang.Set(Lang.All[i].Code); Session.Save(); Transition.Go(this, "res://ui/Options.tscn"); });
        Row(ref y, "DUEL SPEED", Settings.Speeds.Select(s => s.Name).ToArray(), () => Settings.Speed, i => Settings.Speed = i);
        Slider(ref y, "MUSIC", () => Settings.Music, v => { Settings.Music = v; Settings.Apply(false); });
        Slider(ref y, "SOUND", () => Settings.Sound, v => { Settings.Sound = v; Settings.Apply(false); });
        Row(ref y, "SCREEN", new[] { "FULLSCREEN", "WINDOW" }, () => Settings.Fullscreen ? 0 : 1, i => { Settings.Fullscreen = i == 0; Settings.Apply(); });
        Row(ref y, "SAVE FILE", new[] { "EXPORT", "IMPORT" }, () => -1, i => { if (i == 0) ExportSave(); else ImportSave(); });
        Profile();
        Button(this, "DONE", new Vector2(W - 240, H - 100), OnBack);
        Put(this, Lang.F("VERSION {0}", Updater.Current), Thin, 15, Mist, new Vector2(W - 420, 40), new Vector2(360, 24), HorizontalAlignment.Right);   // which build this is (QoL 2026-11-14)
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
        y += 100;
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
        y += 100;
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
            x += Title.GetStringSize(Lang.Tr(s), HorizontalAlignment.Left, -1, Z(17)).X + 34;
        }
    }

    /// A grid of round choices over a dark sheet; picking one applies it and closes.
    void Picker(string title, int n, float d, int cols, Func<int, Control> make, Func<int, bool> chosen, Action<int> pick, string? note = null)
    {
        ClosePicker();
        picker = new Control { Size = new Vector2(W, H), ZIndex = 20 };
        picker.SetMeta("dark", true);
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

    // ---------------------------------------------------------------- save file (2026-11-18)
    // EXPORT writes your progress (signed + scrambled, Engine/SaveData.cs Pack) where you choose: carry it to
    // another PC and IMPORT it there. A file edited by hand, or anything that isn't a save, is refused.

    /// The system's own open / save window.
    void FileWindow(bool save, Action<string> picked)
    {
        var d = new FileDialog
        {
            FileMode = save ? FileDialog.FileModeEnum.SaveFile : FileDialog.FileModeEnum.OpenFile,
            Access = FileDialog.AccessEnum.Filesystem, UseNativeDialog = true, Filters = new[] { "*.crsave ; " + Lang.Tr("Chaos Rift save") },
            CurrentFile = save ? $"ChaosRift-{string.Concat(Settings.PlayerName.Where(c => !Path.GetInvalidFileNameChars().Contains(c)))}-{DateTime.Now:yyyy-MM-dd}.crsave" : "",
            CurrentDir = OS.GetSystemDir(OS.SystemDir.Documents),
        };
        d.FileSelected += p => { d.QueueFree(); picked(p); };
        d.Canceled += d.QueueFree;
        AddChild(d);
        d.PopupCentered();
    }

    void ExportSave() => FileWindow(true, p =>
    {
        try { Session.Export(p); Sheet("SAVE EXPORTED", Lang.F("Saved to {0}. Copy it to your other PC and choose IMPORT there.", p), null); }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { Sheet("COULDN'T EXPORT", e.Message, null); }
    });

    void ImportSave() => FileWindow(false, p =>
    {
        SaveData.State s;
        try { s = SaveData.Import(p); }
        catch (SaveData.Changed) { Sheet("CAN'T IMPORT", "This save was changed outside the game, so it can't be loaded.", null); return; }
        catch (Exception e) when (e is IOException or InvalidDataException or UnauthorizedAccessException or System.Text.Json.JsonException)
        { Sheet("CAN'T IMPORT", "That file isn't a Chaos Rift save, or it's from a newer version of the game.", null); return; }
        var q = s.Profile;
        Sheet("REPLACE YOUR SAVE?",
            Lang.F("{0}: {1} DP, {2} cards, {3} decks.", s.Options.Name, q.Dp, q.Owned.Values.Sum(), s.Decks.Count) + "\n" +
            Lang.Tr("Your current progress will be replaced by this save."),
            () => { Session.Use(s); Transition.Go(this, "res://ui/MainMenu.tscn"); });
    });

    /// A message over a dark sheet; with `yes`, a REPLACE button too.
    void Sheet(string title, string text, Action? yes)
    {
        ClosePicker();
        picker = new Control { Size = new Vector2(W, H), ZIndex = 20 };
        picker.SetMeta("dark", true);
        picker.AddChild(new ColorRect { Size = picker.Size, Color = new Color(0.02f, 0.02f, 0.03f, 0.95f), MouseFilter = MouseFilterEnum.Stop });
        AddChild(picker);
        Put(picker, title, Title, 40, Silver, new Vector2(0, H * 0.32f), new Vector2(W, 60), HorizontalAlignment.Center);
        Hair(picker, new Vector2(W / 2 - 100, H * 0.32f + 62), 200, GoldC);
        var t = Put(picker, text, Body, 24, Mist, new Vector2(W / 2 - 520, H * 0.32f + 100), new Vector2(1040, 140), HorizontalAlignment.Center);
        t.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        if (yes == null) { Button(picker, "OK", new Vector2(W / 2, H * 0.72f), ClosePicker, 24); return; }
        Button(picker, "REPLACE", new Vector2(W / 2 - 160, H * 0.72f), () => { ClosePicker(); yes(); }, 24);
        Button(picker, "CANCEL", new Vector2(W / 2 + 160, H * 0.72f), ClosePicker, 24);
    }

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
            var c = new Control { Size = new Vector2(150, 150), TooltipText = Lang.Cosmetic(owned[i].Name) };
            c.AddChild(Cosmetics.Framed(Avatar, owned[i].Key, 150));
            return c;
        }, i => owned[i].Key == P.Frame, i => Shop.Equip(P, owned[i]), owned.Count < Shop.Frames.Length ? "MORE FRAMES IN THE SHOP" : null);
    }
}
