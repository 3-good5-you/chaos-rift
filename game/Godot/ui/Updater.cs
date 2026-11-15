using Godot;

namespace PoC.Game.UI;

/// Update check (2026-11-10): once per launch the main menu asks itch.io which version is up on this platform's
/// channel (butler's public "wharf/latest" endpoint, the --userversion of the last push). If it is newer than this
/// build's version (project.godot application/config/version), the menu offers UPDATE.
/// UPDATE (2026-11-14): on Windows, when the game was installed with Setup.exe (uninstall.exe beside it), the game
/// downloads the new Setup.exe itself (itch hands out a signed link to a free project's file without a sign-in: POST
/// <page>/file/<upload id>), then starts it with --update=<install folder> and quits; Setup waits for the game to
/// close, installs over it and starts it again. Anywhere else (Mac, the portable zip) and on any failure, UPDATE opens
/// the download page as before.
public static class Updater
{
    public const string Page = "https://3good5you.itch.io/chaos-rift";
    const string Latest = "https://itch.io/api/1/x/wharf/latest?target=3good5you/chaos-rift&channel_name=";
    static bool asked;

    public static string Current => ProjectSettings.GetSetting("application/config/version").AsString();

    const long SetupUpload = 20408561;   // the windows-setup channel's upload on itch (stable across butler pushes)

    static string GameDir => System.IO.Path.GetDirectoryName(OS.GetExecutablePath())!;

    /// The game can update itself here: Windows, an exported build installed by Setup.exe.
    public static bool CanInstall => OS.GetName() == "Windows" && !OS.HasFeature("editor") && System.IO.File.Exists(System.IO.Path.Combine(GameDir, "uninstall.exe"));

    /// Downloads the new installer (reporting "x / y MB"), then starts it and quits the game. `failed` on any error.
    public static void Install(Node host, Action<string> progress, Action failed)
    {
        var path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "ChaosRift-Update-Setup.exe");
        var ask = new HttpRequest { Timeout = 20 };
        host.AddChild(ask);
        ask.RequestCompleted += (result, code, headers, body) =>
        {
            ask.QueueFree();
            var json = result == (long)HttpRequest.Result.Success && code == 200 ? Json.ParseString(System.Text.Encoding.UTF8.GetString(body)) : default;
            if (json.VariantType != Variant.Type.Dictionary || !json.AsGodotDictionary().TryGetValue("url", out var url)) { failed(); return; }
            var get = new HttpRequest { DownloadFile = path, DownloadChunkSize = 1 << 20 };
            host.AddChild(get);
            var tick = new Godot.Timer { WaitTime = 0.25, Autostart = true };
            host.AddChild(tick);
            tick.Timeout += () => { long n = get.GetDownloadedBytes(), all = get.GetBodySize(); progress(all > 0 ? Lang.F("Downloading the update...  {0} / {1} MB", n >> 20, all >> 20) : "Downloading the update..."); };
            get.RequestCompleted += (r2, c2, h2, b2) =>
            {
                tick.QueueFree(); get.QueueFree();
                if (r2 != (long)HttpRequest.Result.Success || c2 != 200 || new System.IO.FileInfo(path).Length < 1 << 20) { failed(); return; }
                progress("Installing: the game restarts when it is done.");
                if (OS.CreateProcess(path, new[] { "--update=" + GameDir }) <= 0) { failed(); return; }
                host.GetTree().Quit();
            };
            if (get.Request(url.AsString()) != Error.Ok) { tick.QueueFree(); get.QueueFree(); failed(); }
        };
        if (ask.Request($"{Page}/file/{SetupUpload}?source=game_download", null, Godot.HttpClient.Method.Post) != Error.Ok) { ask.QueueFree(); failed(); }
    }

    /// Calls `found(version)` on the main thread when a newer build is up. Quiet on any failure (offline, draft page).
    public static void Check(Node host, Action<string> found)
    {
        if (OS.GetCmdlineUserArgs().FirstOrDefault(a => a.StartsWith("--fakeupdate=")) is { } fake) { asked = true; found(fake[13..]); return; }   // dev: see the prompt
        if (asked || OS.HasFeature("editor") || OS.GetCmdlineUserArgs().Any(a => a.StartsWith("--"))) return;
        asked = true;
        var http = new HttpRequest { Timeout = 8 };
        host.AddChild(http);
        http.RequestCompleted += (result, code, headers, body) =>
        {
            http.QueueFree();
            if (result != (long)HttpRequest.Result.Success || code != 200) return;
            var json = Json.ParseString(System.Text.Encoding.UTF8.GetString(body));
            if (json.VariantType != Variant.Type.Dictionary || !json.AsGodotDictionary().TryGetValue("latest", out var v)) return;
            var latest = v.AsString();
            if (Newer(latest, Current)) found(latest);
        };
        var channel = OS.GetName() switch { "macOS" => "mac", "Linux" => "linux", _ => "windows-setup" };
        if (http.Request(Latest + channel) != Error.Ok) http.QueueFree();
    }

    /// "1.0.2" vs "1.0.1": dotted numbers, missing parts count as 0; anything unreadable is never newer.
    public static bool Newer(string a, string b)
    {
        int[]? P(string s) { var p = s.Trim().Split('.'); var n = new int[p.Length]; for (int i = 0; i < p.Length; i++) if (!int.TryParse(p[i], out n[i])) return null; return n; }
        if (P(a) is not { } x || P(b) is not { } y) return false;
        for (int i = 0; i < Math.Max(x.Length, y.Length); i++)
        {
            int l = i < x.Length ? x[i] : 0, r = i < y.Length ? y[i] : 0;
            if (l != r) return l > r;
        }
        return false;
    }
}
