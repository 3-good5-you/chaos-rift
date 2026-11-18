using System.IO;
using Godot;

namespace PoC.Game.UI;

/// Update check (2026-11-10): once per launch the main menu asks itch.io which version is up on this platform's
/// channel (butler's public "wharf/latest" endpoint, the --userversion of the last push). If it is newer than this
/// build's version, the menu offers UPDATE.
/// UPDATE (2026-11-14): on Windows, when the game was installed with Setup.exe (uninstall.exe beside it), the game
/// downloads the new Setup.exe itself (itch hands out a signed link to a free project's file without a sign-in: POST
/// <page>/file/<upload id>), then starts it with --update=<install folder> and quits; Setup waits for the game to
/// close, installs over it and starts it again.
/// Small updates (2026-11-15): package.py --base also makes a patch per platform holding only what changed since a
/// base release, pushed to the "<platform>-patch" channel with --userversion "<version> base <base version>". When
/// one is up for this install's base it is taken instead of the full game: Windows runs it like Setup (it installs
/// over the folder without wiping it); Mac and Linux unpack it over the game here, then restart. The patch's
/// resources are patch.pck, loaded at startup (LoadPatch); patch.json beside it says which version this now is.
/// Anywhere the game cannot update itself, and on any failure, UPDATE opens the download page.
public static class Updater
{
    public const string Page = "https://3good5you.itch.io/chaos-rift";
    const string Latest = "https://itch.io/api/1/x/wharf/latest?target=3good5you/chaos-rift&channel_name=";
    static bool asked;

    const long SetupUpload = 20408561;   // the windows-setup channel's upload on itch (stable across butler pushes)
    /// The patch channels' uploads on itch (`butler status 3good5you/chaos-rift` lists them; created 2026-11-15 with a
    /// README placeholder at --userversion 0, which never parses as a patch). A 0 channel is never asked.
    static readonly Dictionary<string, long> PatchUpload = new() { ["windows-patch"] = 20571353, ["mac-patch"] = 20571355, ["linux-patch"] = 20571357 };

    static bool Mac => OS.GetName() == "macOS";
    static bool Windows => OS.GetName() == "Windows";
    static string GameDir => Path.GetDirectoryName(OS.GetExecutablePath())!;
    /// Where the game's .pck lies (patch.pck and patch.json go beside it): Chaos Rift.app/Contents/Resources on a Mac.
    static string PackDir => Mac ? Path.GetFullPath(Path.Combine(GameDir, "..", "Resources")) : GameDir;
    /// What a patch's file names are relative to: the .app on a Mac, the game folder elsewhere.
    static string PatchRoot => Mac ? Path.GetFullPath(Path.Combine(GameDir, "..", "..")) : GameDir;

    /// The version of the main .pck (project.godot application/config/version at export).
    public static string Base => ProjectSettings.GetSetting("application/config/version").AsString();
    static string? patched;
    public static string Current => patched ?? Base;

    /// Startup, before any scene past the loading screen: the installed patch's resources over the main pack's.
    public static void LoadPatch()
    {
        string json = Path.Combine(PackDir, "patch.json"), pck = Path.Combine(PackDir, "patch.pck");
        if (OS.HasFeature("editor") || !File.Exists(json)) return;
        var d = Json.ParseString(File.ReadAllText(json));
        if (d.VariantType != Variant.Type.Dictionary || d.AsGodotDictionary().GetValueOrDefault("base").AsString() != Base) return;   // left by an older install
        if (File.Exists(pck) && !ProjectSettings.LoadResourcePack(pck)) { GD.PrintErr("update: could not load " + pck); return; }
        patched = d.AsGodotDictionary()["version"].AsString();
    }

    static string? patch;   // the patch channel to take, when the check found one that fits this install

    /// The game can update itself here: a patch fits this install, or Windows with Setup.exe's install.
    public static bool CanInstall => !OS.HasFeature("editor") && (patch != null || Windows && File.Exists(Path.Combine(GameDir, "uninstall.exe")));

    /// Downloads the update (reporting "x / y MB"), installs it and restarts the game. `failed(why)` on any error.
    public static void Install(Node host, Action<string> progress, Action<string> failed)
    {
        bool unpack = patch != null && !Windows;   // Windows runs every update as a Setup window
        var path = Path.Combine(Path.GetTempPath(), unpack ? "ChaosRift-Update.zip" : "ChaosRift-Update-Setup.exe");
        var ask = new HttpRequest { Timeout = 30 };
        host.AddChild(ask);
        ask.RequestCompleted += (result, code, headers, body) =>
        {
            ask.QueueFree();
            var json = result == (long)HttpRequest.Result.Success && code == 200 ? Json.ParseString(System.Text.Encoding.UTF8.GetString(body)) : default;
            if (json.VariantType != Variant.Type.Dictionary || !json.AsGodotDictionary().TryGetValue("url", out var url))
            {
                // itch refuses links to hidden uploads (the patch channels are hidden on the page, 2026-11-15):
                // fall back to the full update where the game can install one, else the download page
                if (patch != null) { GD.PrintErr($"update: no link for {patch}, taking the full update"); patch = null; if (CanInstall) { Install(host, progress, failed); return; } }
                failed($"link: {(HttpRequest.Result)result} {code}"); return;
            }
            var get = new HttpRequest { DownloadFile = path, DownloadChunkSize = 1 << 20 };
            host.AddChild(get);
            var tick = new Godot.Timer { WaitTime = 0.25, Autostart = true };
            host.AddChild(tick);
            tick.Timeout += () => { long n = get.GetDownloadedBytes(), all = get.GetBodySize(); progress(all > 0 ? Lang.F("Downloading the update...  {0} / {1} MB", n >> 20, all >> 20) : "Downloading the update..."); };
            get.RequestCompleted += (r2, c2, h2, b2) =>
            {
                long all = get.GetBodySize(), got = File.Exists(path) ? new FileInfo(path).Length : 0;
                tick.QueueFree(); get.QueueFree();
                if (r2 != (long)HttpRequest.Result.Success || c2 != 200 || got == 0 || all > 0 && got != all) { failed($"download: {(HttpRequest.Result)r2} {c2}"); return; }
                progress("Installing: the game restarts when it is done.");
                if (!unpack)
                {
                    if (OS.CreateProcess(path, new[] { "--update=" + GameDir }) <= 0) { failed("could not start the installer"); return; }
                }
                else
                {
                    if (Unpack(path) is { } why) { failed(why); return; }
                    if (Mac) OS.CreateProcess("/usr/bin/open", new[] { "-n", PatchRoot }); else OS.CreateProcess(OS.GetExecutablePath(), System.Array.Empty<string>());
                }
                host.GetTree().Quit();
            };
            if (get.Request(url.AsString()) is var e2 and not Error.Ok) { tick.QueueFree(); get.QueueFree(); failed($"download: {e2}"); }
        };
        long upload = patch != null ? PatchUpload[patch] : SetupUpload;
        if (ask.Request($"{Page}/file/{upload}?source=game_download", null, Godot.HttpClient.Method.Post) is var e1 and not Error.Ok) { ask.QueueFree(); failed($"link: {e1}"); }
    }

    /// Mac / Linux: the patch's files over the game. Every file is written beside its target first, then all are
    /// renamed into place (the running game keeps the copies it has open), patch.json last; the .app is signed again.
    static string? Unpack(string zip)
    {
        var z = new ZipReader();
        if (z.Open(zip) != Error.Ok) return "patch: unreadable";
        var done = new List<string>();
        try
        {
            string root = PatchRoot.TrimEnd('/') + "/";
            foreach (var name in z.GetFiles().Where(n => !n.EndsWith("/")).OrderBy(n => n.EndsWith("patch.json")))
            {
                var to = Path.GetFullPath(Path.Combine(root, name));
                if (!to.StartsWith(root)) continue;   // never outside the game
                Directory.CreateDirectory(Path.GetDirectoryName(to)!);
                File.WriteAllBytes(to + ".new", z.ReadFile(name));
                done.Add(to);
                if (!OperatingSystem.IsWindows() && (name.StartsWith("Contents/MacOS/") || name.EndsWith(".x86_64")))
                    File.SetUnixFileMode(to + ".new", (UnixFileMode)0b111_101_101);   // rwxr-xr-x
            }
            foreach (var to in done) File.Move(to + ".new", to, true);
        }
        catch (Exception e) { foreach (var to in done) try { File.Delete(to + ".new"); } catch { } return "patch: " + e.Message; }
        finally { z.Close(); }
        if (Mac && OS.Execute("/usr/bin/codesign", new[] { "--force", "--deep", "--sign", "-", PatchRoot }) != 0) GD.PrintErr("update: codesign failed");
        return null;
    }

    /// Calls `found(version)` on the main thread when a newer build is up. Quiet on any failure (offline, draft page).
    public static void Check(Node host, Action<string> found)
    {
        if (OS.GetCmdlineUserArgs().FirstOrDefault(a => a.StartsWith("--fakeupdate=")) is { } fake) { asked = true; found(fake[13..]); return; }   // dev: see the prompt
        if (asked || OS.HasFeature("editor") || OS.GetCmdlineUserArgs().Any(a => a.StartsWith("--"))) return;
        asked = true;
        var plat = Mac ? "mac" : OS.GetName() == "Linux" ? "linux" : "windows";
        LatestOn(host, plat == "windows" ? "windows-setup" : plat, full => LatestOn(host, PatchUpload[plat + "-patch"] > 0 ? plat + "-patch" : null, small =>
        {
            string? best = full != null && Newer(full, Current) ? full : null;
            // "<version> base <base version>": only for the install it was made against, and only when it is the newest
            if (small?.Split(" base ") is [var v, var b] && b == Base && Newer(v, Current) && (best == null || !Newer(best, v))) { patch = plat + "-patch"; best = v; }
            if (best != null) found(best);
        }));
    }

    /// The --userversion of the channel's last push, or null (no channel, offline, unreadable).
    static void LatestOn(Node host, string? channel, Action<string?> got)
    {
        if (channel == null) { got(null); return; }
        var http = new HttpRequest { Timeout = 8 };
        host.AddChild(http);
        http.RequestCompleted += (result, code, headers, body) =>
        {
            http.QueueFree();
            var json = result == (long)HttpRequest.Result.Success && code == 200 ? Json.ParseString(System.Text.Encoding.UTF8.GetString(body)) : default;
            got(json.VariantType == Variant.Type.Dictionary && json.AsGodotDictionary().TryGetValue("latest", out var v) ? v.AsString() : null);
        };
        if (http.Request(Latest + channel) != Error.Ok) { http.QueueFree(); got(null); }
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
