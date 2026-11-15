using Godot;
using PoC.Engine;

namespace PoC.Game.UI;

/// Game language (2026-11-14). English is the source: every Label / Button whose text is exactly an English key in
/// i18n/<code>.json shows the translation (Godot auto-translate), so screens need no code changes. Strings built at
/// run time ($"TURN {n}") stay English until they go through Tr(). Card text: i18n/cards.<code>.json, { idx: text },
/// English where a card is missing. Card names stay English.
public static class Lang
{
    public static readonly (string Code, string Name)[] All = { ("en", "ENGLISH"), ("es", "ESPAÑOL"), ("ar", "العربية") };

    public static string Code { get; private set; } = "en";
    public static int Index => Math.Max(0, Array.FindIndex(All, l => l.Code == Code));

    static readonly Dictionary<string, Translation> ui = new();
    static Dictionary<int, string> cards = new();

    /// Switch language; unknown codes fall back to English.
    public static void Set(string code)
    {
        if (!All.Any(l => l.Code == code)) code = "en";
        Code = code;
        var duel = code == "en" ? null : Read($"res://i18n/duel.{code}.json");
        if (code != "en" && !ui.ContainsKey(code) && Read($"res://i18n/{code}.json") is { } d)
        {
            var t = new Translation { Locale = code };
            foreach (var (k, v) in d) t.AddMessage(k, v);
            // the engine's own fixed text (challenge names and goals, Limited rules) shows on plain labels too
            foreach (var (k, v) in duel ?? new()) if (!k.Contains("{0}") && !d.ContainsKey(k)) t.AddMessage(k, v);
            // card names (user 2026-11-14): any label showing a name, as written or in capitals, shows the translated
            // one; the card art keeps its printed English name, the engine keeps using the English one
            foreach (var (k, v) in Read($"res://i18n/names.{code}.json") ?? new())
            {
                if (!d.ContainsKey(k)) t.AddMessage(k, v);
                var up = k.ToUpperInvariant();
                if (!d.ContainsKey(up)) t.AddMessage(up, v.ToUpper(System.Globalization.CultureInfo.GetCultureInfo(code)));
            }
            TranslationServer.AddTranslation(t);
            ui[code] = t;
        }
        cards = code == "en" ? new() : (Read($"res://i18n/cards.{code}.json") ?? new()).Where(kv => int.TryParse(kv.Key, out _)).ToDictionary(kv => int.Parse(kv.Key), kv => kv.Value);
        LoadDuel(duel);
        // Arabic would mirror every Control (Godot flips layout for RTL locales), but screens place things by absolute
        // position: project.godot pins internationalization/rendering/root_node_layout_direction to left-to-right.
        // Arabic words still shape and read right-to-left inside their labels.
        TranslationServer.SetLocale(code);
        Fonts.Joined(code == "ar");
    }

    /// A card's text in the game language (the TCG wording in English).
    public static string Desc(CardData d) => Card(d) ?? d.TcgDesc ?? d.Desc;

    /// The translated card text, or null (English, or not translated yet).
    public static string? Card(CardData d) => cards.GetValueOrDefault(d.Idx);

    /// A shop cosmetic's name. Ones named after a card ("Dark Magician" playmat) have their own "cosmetic:" keys, so
    /// the cosmetic is translated without renaming the card itself (card names stay English).
    public static string Cosmetic(string name) => Tr("cosmetic:" + name) is var t && t != "cosmetic:" + name ? t : Tr(name);

    /// A run-time string through the table (for text built in code, not set straight on a Label).
    public static string Tr(string s) => TranslationServer.Translate(s);

    /// A run-time string with values: the English template ("PAGE {0} / {1}") is the key, the values go in after.
    /// A translation with a broken placeholder falls back to the English template rather than crash the screen.
    /// Text values go through the table too, so a rival's name ("KAIBA") inside a line comes out in the game language.
    public static string F(string template, params object?[] args)
    {
        args = args.Select(a => a is string s ? Tr(s) : a).ToArray();
        try { return string.Format(Tr(template), args); }
        catch (FormatException) { return string.Format(template, args); }
    }

    // ---------------------------------------------------------------- duel prompts

    /// Engine prompts ("Add which monster to your hand?", $"Equip {name} to which monster?") are built in English and
    /// read by the CPU, so they are translated only where the duel shows them. i18n/duel.<code>.json holds the engine's
    /// templates (game/tools/i18n/export_prompts.py), holes as {0}: exact text first, then the templates with holes,
    /// longest first, as patterns; what fills a hole (a card name, a number) is carried over as it is.
    static Dictionary<string, string> duelExact = new();
    static List<(System.Text.RegularExpressions.Regex Re, string To)> duelHoles = new();
    static readonly Dictionary<string, string> duelSeen = new();

    static void LoadDuel(Dictionary<string, string>? d)
    {
        duelSeen.Clear();
        duelExact = d?.Where(kv => !kv.Key.Contains("{0}")).ToDictionary(kv => kv.Key, kv => kv.Value) ?? new();
        duelHoles = d?.Where(kv => kv.Key.Contains("{0}")).OrderByDescending(kv => kv.Key.Length)
            .Select(kv => (new System.Text.RegularExpressions.Regex("^" + System.Text.RegularExpressions.Regex.Replace(
                System.Text.RegularExpressions.Regex.Escape(kv.Key), @"\\\{(\d+)}", "(?<p$1>.+?)") + "$",
                System.Text.RegularExpressions.RegexOptions.Singleline), kv.Value)).ToList() ?? new();
    }

    /// A duel prompt or choice in the game language; English when no template fits.
    public static string Duel(string? s)
    {
        if (string.IsNullOrEmpty(s) || Code == "en") return s ?? "";
        if (duelSeen.TryGetValue(s, out var hit)) return hit;
        string r = duelExact.TryGetValue(s, out var t) ? t : Tr(s);
        if (r == s)
            foreach (var (re, to) in duelHoles)
                if (re.Match(s) is { Success: true } m)
                {
                    r = System.Text.RegularExpressions.Regex.Replace(to, @"\{(\d+)\}", h => m.Groups["p" + h.Groups[1].Value] is { Success: true } g ? Tr(g.Value) : h.Value);   // a card name in a hole: its translated name
                    break;
                }
        return duelSeen[s] = r;
    }

    static Dictionary<string, string>? Read(string path)
    {
        if (!Godot.FileAccess.FileExists(path)) return null;
        try { return System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, string>>(Godot.FileAccess.GetFileAsString(path)); }
        catch (System.Text.Json.JsonException e) { GD.PushWarning($"{path}: {e.Message}"); return null; }
    }
}
