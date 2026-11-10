using Godot;

namespace PoC.Game.UI;

/// Every font the game asks for by system name comes through here (2026-11-09). The picked look is the Office fonts
/// (Perpetua Titling, Gill Sans, Castellar); where they are not installed (most PCs without Office, every Mac) the
/// bundled free look-alikes take over: Cinzel, Cabin, Cinzel Decorative (assets/fonts, SIL OFL). Windows fonts
/// (Bahnschrift, Segoe UI, Impact) likewise fall back to Barlow / Anton. Font lab: `-- --fontlab` (1 / 2 switch
/// titles / text between the installed font and the bundled one, 3 = pretend nothing is installed, like a Mac).
public static partial class Fonts
{
    public static readonly string[] TitleOpts = { "office", "cinzel" };
    public static readonly string[] BodyOpts = { "office", "cabin" };
    public static int Title, Body;     // 0 = the Office font when installed (picked 2026-11-09), 1 = always the bundled one
    public static bool ForceBundled;   // lab key 3 / --macfonts: act as if no system font is installed

    static readonly Dictionary<string, FontFile> files = new();

    static Fonts()
    {
        foreach (var a in OS.GetCmdlineUserArgs())
        {
            if (a.StartsWith("--titlefont=")) Title = Math.Max(0, Array.IndexOf(TitleOpts, a[12..]));
            else if (a.StartsWith("--bodyfont=")) Body = Math.Max(0, Array.IndexOf(BodyOpts, a[11..]));
            else if (a == "--macfonts") ForceBundled = true;
        }
    }

    static FontFile File(string name)
    {
        if (files.TryGetValue(name, out var f)) return f;
        f = GD.Load<FontFile>($"res://assets/fonts/{name}");
        f.Antialiasing = TextServer.FontAntialiasing.Gray;
        f.Oversampling = 2f;
        return files[name] = f;
    }

    /// A variable font at a weight (Cinzel, Cabin), or a fixed file; `slant` fakes italic for Impact's stand-in.
    static Font Var(string file, int weight = 0, float slant = 0)
    {
        var v = new FontVariation { BaseFont = File(file) };
        if (weight > 0) v.VariationOpentype = new Godot.Collections.Dictionary { { TextServerManager.GetPrimaryInterface().NameToTag("wght"), weight } };
        if (slant != 0) v.VariationTransform = new Transform2D(new Vector2(1, 0), new Vector2(-slant, 1), Vector2.Zero);
        return v;
    }

    /// Letter spacing on a font. A variable font's weight lives on its FontVariation, and a FontVariation wrapped in
    /// another one loses it, so spacing goes on that same variation.
    public static Font Spaced(Font f, int glyph)
    {
        var v = f as FontVariation ?? new FontVariation { BaseFont = f };
        v.SpacingGlyph = glyph;
        return v;
    }

    static bool Installed(string name) => !ForceBundled && OS.GetSystemFontPath(name) != "";

    public static Font Get(string[] names, int weight = 400, bool italic = false, int stretch = 100)
    {
        string first = names[0];
        Font? f = first switch
        {
            "Perpetua Titling MT" when Title == 1 || !Installed(first) => Var("Cinzel.ttf", weight >= 600 ? 700 : 400),
            "Gill Sans MT" when Body == 1 || !Installed(first) => Var("Cabin.ttf", Math.Max(400, weight)),
            "Castellar" when Title == 1 || !Installed(first) => File("CinzelDecorative-Regular.ttf"),
            "Bahnschrift" when !Installed("Bahnschrift") => File(stretch <= 80 ? (weight >= 700 ? "BarlowCondensed-Bold.ttf" : "BarlowCondensed-SemiBold.ttf")
                : stretch <= 90 ? "BarlowSemiCondensed-Medium.ttf" : weight >= 700 ? "Barlow-Bold.ttf" : weight >= 500 ? "Barlow-Medium.ttf" : "Barlow-Regular.ttf"),
            "Segoe UI" when !Installed("Segoe UI") => File(weight >= 700 ? "Barlow-Bold.ttf" : weight >= 500 ? "Barlow-Medium.ttf" : "Barlow-Regular.ttf"),
            "Impact" when !Installed("Impact") => Var("Anton-Regular.ttf", 0, italic ? 0.18f : 0),
            _ => null,
        };
        if (f != null) return f;
        // Still a system font: add the Mac names for the Japanese and symbol faces.
        var list = first switch
        {
            "Yu Gothic UI" => names.Concat(new[] { "Hiragino Sans", "Hiragino Kaku Gothic ProN" }).ToArray(),
            "Segoe UI Symbol" => names.Concat(new[] { "Apple Symbols", "Arial Unicode MS" }).ToArray(),
            _ => names,
        };
        return new SystemFont { FontNames = list, FontWeight = weight, FontItalic = italic, FontStretch = stretch, Antialiasing = TextServer.FontAntialiasing.Gray, Oversampling = 2f };
    }

    // ---------------------------------------------------------------- font lab overlay

    /// Small panel top centre; 1 / 2 / 3 change a font and restart the game on the same screen.
    public static void Lab(Control screen)
    {
        if (!OS.GetCmdlineUserArgs().Contains("--fontlab")) return;
        var layer = new CanvasLayer { Layer = 120 };
        screen.AddChild(layer);
        var bg = new ColorRect { Color = new Color(0, 0, 0, 0.82f), Position = new Vector2(screen.Size.X / 2 - 520, 6), Size = new Vector2(1040, 62), MouseFilter = Control.MouseFilterEnum.Ignore };
        layer.AddChild(bg);
        var l = new Label
        {
            Text = $"FONT LAB   [1] TITLES: {(Title == 0 ? "PERPETUA (IF INSTALLED)" : "CINZEL")}   [2] TEXT: {(Body == 0 ? "GILL SANS (IF INSTALLED)" : "CABIN")}   [3] HUD: {(ForceBundled ? "MAC (BUNDLED)" : "WINDOWS")}",
            Position = new Vector2(16, 14), Size = new Vector2(1010, 36), HorizontalAlignment = HorizontalAlignment.Center,
        };
        l.AddThemeFontSizeOverride("font_size", 24);
        l.AddThemeColorOverride("font_color", new Color(1f, 0.85f, 0.5f));
        bg.AddChild(l);
        var keys = new LabKeys { Screen = screen };
        layer.AddChild(keys);
    }

    sealed partial class LabKeys : Node
    {
        public Control Screen = null!;
        public override void _UnhandledInput(InputEvent e)
        {
            if (e is not InputEventKey { Pressed: true, Echo: false } k) return;
            int t = Title, b = Body; bool m = ForceBundled;
            if (k.Keycode == Key.Key1) t = (t + 1) % TitleOpts.Length;
            else if (k.Keycode == Key.Key2) b = (b + 1) % BodyOpts.Length;
            else if (k.Keycode == Key.Key3) m = !m;
            else return;
            GetViewport().SetInputAsHandled();
            // Fonts are cached all over the UI: restart with the new choice, opening the screen we are on.
            var args = OS.GetCmdlineArgs().Where(a => !a.StartsWith("--titlefont=") && !a.StartsWith("--bodyfont=") && a != "--macfonts" && !a.StartsWith("res://")).ToList();
            int sep = args.IndexOf("--");
            if (sep < 0) { args.Add("--"); sep = args.Count - 1; }
            args.Insert(sep, Screen.SceneFilePath);
            args.AddRange(new[] { $"--titlefont={TitleOpts[t]}", $"--bodyfont={BodyOpts[b]}" });
            if (m) args.Add("--macfonts");
            OS.SetRestartOnExit(true, args.ToArray());
            GetTree().Quit();
        }
    }
}
