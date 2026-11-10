using Godot;

namespace PoC.Game.UI;

/// Update check (2026-11-10): once per launch the main menu asks itch.io which version is up on this platform's
/// channel (butler's public "wharf/latest" endpoint, the --userversion of the last push). If it is newer than this
/// build's version (project.godot application/config/version), the menu offers UPDATE, which opens the download page.
/// ponytail: no in-place download: itch serves files only to signed-in users or its own app (which updates by itself).
/// A silent auto-update would need the installer on a public host (e.g. a public GitHub release).
public static class Updater
{
    public const string Page = "https://3good5you.itch.io/chaos-rift";
    const string Latest = "https://itch.io/api/1/x/wharf/latest?target=3good5you/chaos-rift&channel_name=";
    static bool asked;

    public static string Current => ProjectSettings.GetSetting("application/config/version").AsString();

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
        var channel = OS.GetName() == "macOS" ? "mac" : "windows-setup";
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
