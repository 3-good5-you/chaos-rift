using Godot;

namespace PoC.Game.UI;

/// Shared plumbing for menu screens: menu entries, keyboard/mouse focus, sounds, and a screenshot
/// mode (`-- --shot=path.png [--hover=N] [--delay=S]`) used to capture previews.
public abstract partial class MenuBase : Control
{
    public static readonly string[] Items = { "Duel", "Tournament", "LAN Duel", "Deck Builder", "Card List", "Shop", "Options", "Quit" };

    protected int Focus = -1;

    /// Number of focusable entries (defaults to the main menu items).
    protected virtual int ItemCount => Items.Length;

    /// Esc / back button. Nothing by default.
    protected virtual void OnBack() { }

    /// True on screens redone in the ink look: the shared chips, panels and card tiles draw in cream ink (Kit.InkTheme).
    protected virtual bool Inked => false;

    /// Background music; null for none. Master Duel OST in `assets/audio/md/`.
    protected virtual string? MusicPath => Md("menu_01");

    /// A Master Duel track; with a count, a random one of `<kind>_01..count`.
    protected static string Md(string kind, int count = 0) =>
        $"res://assets/audio/md/{kind}{(count > 0 ? $"_{GD.RandRange(1, count):00}" : "")}.mp3";
    AudioStreamPlayer? music, sfx;
    AudioStream? cursorSnd, decideSnd;
    string? shotPath;
    int shotHover = -1;
    double shotDelay = 3.2, clock;
    bool shotDone;
    static bool wentTo;

    public override void _Ready()
    {
        SetAnchorsPreset(LayoutPreset.FullRect);
        foreach (var a in OS.GetCmdlineUserArgs())
        {
            if (a.StartsWith("--shot=")) shotPath = a[7..];
            else if (a.StartsWith("--hover=")) shotHover = int.Parse(a[8..]);
            else if (a.StartsWith("--goto=") && !wentTo) { wentTo = true; var to = a[7..]; GetTree().CreateTimer(1.5).Timeout += () => Transition.Go(this, to); } // transition test (once: the next screen reads the same args)
            else if (a.StartsWith("--delay=")) shotDelay = double.Parse(a[8..], System.Globalization.CultureInfo.InvariantCulture);
        }
        Session.Load();
        Kit.InkTheme = Inked;
        Settings.Apply(window: shotPath == null);
        if (shotPath == null)
        {
            music = Music(this, MusicPath);
            sfx = new AudioStreamPlayer { VolumeDb = -6, Bus = "Sound" };
            AddChild(sfx);
            cursorSnd = GD.Load<AudioStream>("res://assets/audio/md/se/SE_MENU_SELECT_01.wav");
            decideSnd = GD.Load<AudioStream>("res://assets/audio/md/se/SE_MENU_DECIDE.wav");
        }
        Build();
        Fonts.Lab(this);
    }

    /// True while capturing a screenshot (`--shot`): no audio.
    protected bool Shooting => shotPath != null;

    static AudioStreamPlayer? shared;
    static string? playing;
    static bool looping = true;
    static bool MenuTrack(string? p) => p != null && (p.Contains("/menu_") || p.Contains("/solo_gate"));

    /// One music player for the whole game (on the tree root), so changing screens never restarts the song:
    /// a menu screen keeps whatever menu track is already playing; other tracks (duel) swap in; null stops it.
    static AudioStreamPlayer? Music(Node from, string? path)
    {
        if (shared == null || !IsInstanceValid(shared))
        {
            shared = new AudioStreamPlayer { VolumeDb = -3, Bus = "Music" };
            shared.Finished += () => { if (looping) shared.Play(); };
            from.GetTree().Root.CallDeferred(Node.MethodName.AddChild, shared);
            playing = null;
        }
        if (path == null) { shared.Stop(); playing = null; return shared; }
        if (path == playing || (MenuTrack(path) && MenuTrack(playing))) return shared;
        Swap(path, true);
        return shared;
    }

    static void Swap(string path, bool loop)
    {
        playing = path; looping = loop;
        shared!.VolumeDb = -3;   // a screen left mid-fade must not leave the music quiet
        shared!.Stream = GD.Load<AudioStream>(path);
        if (shared.IsInsideTree()) shared.Play(); else shared.Autoplay = true;
    }

    /// Swap the background music (e.g. win/lose jingle). Ignored while shooting.
    protected void SetMusic(string path, bool loop = true)
    {
        if (music == null) return;
        if (shared == null || !shared.Playing) { Swap(path, loop); return; }
        // dip and rise instead of a hard cut (the duel's keycard / climax tracks, the win and lose jingles)
        var tw = CreateTween();
        tw.TweenProperty(shared, "volume_db", -40f, 0.7).SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.In);
        tw.TweenCallback(Callable.From(() => { Swap(path, loop); shared.VolumeDb = -40; }));
        tw.TweenProperty(shared, "volume_db", -3f, 0.9).SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.Out);
    }

    /// Build the scene graph and start the intro animation.
    protected abstract void Build();

    /// Visual update when the focused item changes (old may be -1).
    protected abstract void OnFocus(int old, int now);

    /// Visual feedback when an item is chosen. No screens exist behind the items yet.
    protected abstract void OnChoose(int index);

    public void SetFocus(int i)
    {
        if (i == Focus) return;
        int old = Focus;
        Focus = i;
        OnFocus(old, i);
        Play(cursorSnd);
    }

    public void Choose(int i)
    {
        SetFocus(i);
        Play(decideSnd);
        OnChoose(i);
    }

    /// The cursor tick or the decide sound, for screens with their own buttons.
    protected void UiSound(bool decide) => Play(decide ? decideSnd : cursorSnd);

    void Play(AudioStream? s)
    {
        if (sfx == null || s == null) return;
        sfx.Stream = s;
        sfx.Play();
    }

    /// Right click = back (the same as Esc), unless what is under the cursor has a right click of its own ("rmb").
    public override void _Input(InputEvent e)
    {
        if (e is not InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Right }) return;
        for (Node? n = GetViewport().GuiGetHoveredControl(); n != null; n = n.GetParent()) if (n.HasMeta("rmb")) return;
        GetViewport().SetInputAsHandled();
        Input.ParseInputEvent(new InputEventAction { Action = "ui_cancel", Pressed = true });
    }

    public override void _UnhandledInput(InputEvent e)
    {
        if (e.IsActionPressed("ui_cancel")) { OnBack(); return; }
        if (e.IsActionPressed("ui_down") || e.IsActionPressed("ui_right")) SetFocus(Focus < 0 ? 0 : (Focus + 1) % ItemCount);
        else if (e.IsActionPressed("ui_up") || e.IsActionPressed("ui_left")) SetFocus(Focus < 0 ? 0 : (Focus + ItemCount - 1) % ItemCount);
        else if (e.IsActionPressed("ui_accept") && Focus >= 0) Choose(Focus);
    }

    public override void _Process(double delta)
    {
        clock += delta;
        if (shotPath == null || shotDone) return;
        if (shotHover >= 0 && clock > shotDelay - 0.9 && Focus != shotHover) SetFocus(shotHover);
        if (clock < shotDelay) return;
        shotDone = true;
        Capture();
    }

    async void Capture()
    {
        // Let two full frames finish so the readback holds one complete frame.
        await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        GetViewport().GetTexture().GetImage().SavePng(shotPath);
        GetTree().Quit();
    }

    /// Wire a control so hovering focuses item i and clicking chooses it.
    protected void Hook(Control c, int i)
    {
        c.MouseFilter = MouseFilterEnum.Stop;
        c.MouseEntered += () => { if (shotPath == null) SetFocus(i); }; // screenshots ignore the real cursor
        c.GuiInput += e => { if (e is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left }) Choose(i); };
    }

    // ---- small helpers

    protected static Texture2D Tex(string path) => GD.Load<Texture2D>(path);

    protected static Font Sys(string[] names, int weight = 400, bool italic = false, int stretch = 100) => Fonts.Get(names, weight, italic, stretch);

    protected static Label Text(string s, Font f, int size, Color c, int outline = 0, Color? outlineColor = null)
    {
        var l = new Label { Text = s };
        l.AddThemeFontOverride("font", f);
        l.AddThemeFontSizeOverride("font_size", size);
        l.AddThemeColorOverride("font_color", c);
        if (outline > 0)
        {
            l.AddThemeConstantOverride("outline_size", outline);
            l.AddThemeColorOverride("font_outline_color", outlineColor ?? Colors.Black);
        }
        return l;
    }

    static readonly Dictionary<string, Shader> shaders = new();
    protected static ShaderMaterial Mat(string code, params (string, Variant)[] uniforms)
    {
        // one Shader per source text: compiling it again for every material froze the duel's start (2 per card)
        if (!shaders.TryGetValue(code, out var sh)) shaders[code] = sh = new Shader { Code = code };
        var m = new ShaderMaterial { Shader = sh };
        foreach (var (k, v) in uniforms) m.SetShaderParameter(k, v);
        return m;
    }
}
