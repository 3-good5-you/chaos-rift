using System.Text;
using System.Text.RegularExpressions;

namespace PoC.Game.UI;

/// Card lab text as BBCode: card text split into one line per effect (condition / cost / effect coloured the official
/// way: "condition: cost; effect"), rulings one per paragraph, and keywords highlighted everywhere.
public static class LabText
{
    const string Gold = "#e8c25a", Cond = "#7fd0ff", Cost = "#ffb46b", Name = "#ffe08a", Bad = "#ff8a7a", Good = "#9be37a", Aim = "#c9a2ff", Limit = "#ff9a8a", Dim = "#b9c0cc";

    static readonly (Regex Re, string Color)[] Keys =
    {
        (new(@"\b(Special Summon(?:ed|s|ing)?|Normal Summon(?:ed|s)?|Tribute Summon(?:ed|s)?|Fusion Summon(?:ed|s)?|Ritual Summon(?:ed|s)?|Flip Summon(?:ed|s)?|Set(?:s)?)\b"), Good),
        (new(@"\b([Dd]estroy(?:ed|s)?|[Bb]anish(?:ed|es)?|[Nn]egate(?:d|s)?|[Dd]iscard(?:ed|s)?|[Tt]ribute(?:d|s)?|send(?:s)? [^.;:]{0,40}? to the (?:GY|Graveyard))\b"), Bad),
        (new(@"\b([Tt]arget(?:s|ed)?)\b"), Aim),
        (new(@"\b(Draw Phase|Standby Phase|Main Phase(?: 1| 2)?|Battle Phase|Battle Step|Damage Step|damage calculation|End Phase|Quick Effect|FLIP|Trigger Effect|Ignition Effect|Continuous Effect)\b"), Cond),
        (new(@"\b([Oo]nce per (?:turn|Duel|battle|Chain)|[Cc]annot|can't|[Mm]ust|instead|[Uu]naffected|[Ii]gnoring (?:its|their) Summoning conditions)\b"), Gold),
    };

    // the same marks in darker inks, for text on the light side of the rift
    static readonly (string, string)[] OnLight = { (Gold, "#8a5a00"), (Cond, "#1565a8"), (Cost, "#b3541e"), (Name, "#7a4d00"), (Bad, "#b3261e"), (Good, "#2e7d32"), (Aim, "#6a3fb5") };

    /// Card text for the previews (menus and duel): keywords, quoted names and numbers highlighted, line breaks kept.
    public static string Card(string desc, bool light = false)
    {
        var s = Mark(desc.Replace("\r", ""));
        if (light) foreach (var (a, b) in OnLight) s = s.Replace(a, b);
        return s;
    }

    static string Esc(string s) => s.Replace("[", "[lb]");

    /// Highlight keywords, quoted card names and numbers in plain text.
    public static string Mark(string text)
    {
        var parts = Regex.Split(text, "(\"[^\"]*\")");   // quoted names stay untouched by the keyword pass
        var sb = new StringBuilder();
        foreach (var part in parts)
        {
            if (part.StartsWith('"') && part.Length > 1) { sb.Append($"[color={Name}]{Esc(part)}[/color]"); continue; }
            var s = Esc(part);
            foreach (var (re, color) in Keys) s = re.Replace(s, m => $"[b][color={color}]{m.Value}[/color][/b]");
            s = Regex.Replace(s, @"(?<![\w#=])(\d{2,5})(?![\w])", "[b]$1[/b]");
            sb.Append(s);
        }
        return sb.ToString();
    }

    /// The card text: material line dimmed, one numbered line per sentence/effect, ● options indented, limits flagged.
    public static string Effects(string desc)
    {
        var sb = new StringBuilder();
        int n = 0;
        foreach (var para in desc.Replace("\r", "").Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            bool materials = n == 0 && sb.Length == 0 && Regex.IsMatch(para, "^(\"[^\"]+\"|\\d+ [^.]*monster)") && !para.Contains(':') && !para.TrimEnd().EndsWith('.');
            if (materials) { sb.Append($"[color={Dim}]Materials: {Esc(para)}[/color]\n"); continue; }
            foreach (var chunk in para.Split('●'))
            {
                bool bullet = sb.Length > 0 && chunk != para.Split('●')[0];
                foreach (var sent in Sentences(chunk.Trim()))
                {
                    if (sent.Length == 0) continue;
                    if (bullet) { sb.Append($"      [color={Gold}]●[/color] {Psct(sent)}\n"); bullet = false; continue; }
                    if (Regex.IsMatch(sent, @"^(You can only (use|activate|Special Summon)|You can only control|Each player can only|This card can only be)", RegexOptions.IgnoreCase))
                    { sb.Append($"[color={Limit}][b]LIMIT[/b]  {Esc(sent)}[/color]\n"); continue; }
                    if (sent.StartsWith('(') && sent.EndsWith(')')) { sb.Append($"      [color={Dim}][i]{Esc(sent)}[/i][/color]\n"); continue; }
                    n++;
                    sb.Append($"[b][color={Gold}]{n}[/color][/b]  {Psct(sent)}\n");
                }
            }
        }
        return sb.ToString().TrimEnd();
    }

    /// "condition: cost; effect" coloured (only when the colon comes before the semicolon, outside quotes).
    static string Psct(string s)
    {
        int colon = Find(s, ':'), semi = Find(s, ';');
        if (colon < 0 && semi < 0) return Mark(s);
        var sb = new StringBuilder();
        int at = 0;
        if (colon >= 0 && (semi < 0 || colon < semi)) { sb.Append($"[color={Cond}]{Mark(s[..(colon + 1)])}[/color]"); at = colon + 1; }
        if (semi > at) { sb.Append($"[color={Cost}]{Mark(s[at..(semi + 1)])}[/color]"); at = semi + 1; }
        sb.Append(Mark(s[at..]));
        return sb.ToString();
    }

    static int Find(string s, char c)
    {
        bool q = false;
        for (int i = 0; i < s.Length; i++) { if (s[i] == '"') q = !q; else if (s[i] == c && !q) return i; }
        return -1;
    }

    /// Sentence split that keeps quoted names, parentheses and initials ("Red-Eyes B. Dragon", "D.D.") whole.
    static IEnumerable<string> Sentences(string text)
    {
        bool q = false; int depth = 0, start = 0;
        for (int i = 0; i < text.Length; i++)
        {
            char c = text[i];
            if (c == '"') q = !q;
            else if (c == '(') depth++;
            else if (c == ')') depth = Math.Max(0, depth - 1);
            else if (c == '.' && !q && depth == 0 && i + 2 < text.Length && text[i + 1] == ' ' && (char.IsUpper(text[i + 2]) || text[i + 2] == '('))
            {
                bool initial = i >= 1 && char.IsUpper(text[i - 1]) && (i < 2 || !char.IsLetter(text[i - 2]));
                if (initial) continue;
                yield return text[start..(i + 1)].Trim();
                start = i + 2;
            }
        }
        if (start < text.Length) yield return text[start..].Trim();
    }

    /// Rulings: "[Regarding ...]" lines become small headings, the rest are spaced bullets.
    public static string Rulings(string text)
    {
        var sb = new StringBuilder();
        foreach (var line in text.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            var l = line.Trim();
            if (l.StartsWith("[Regarding", StringComparison.OrdinalIgnoreCase))
                sb.Append($"[color={Gold}][i]{Esc(l.Trim('[', ']'))}[/i][/color]\n");
            else
                sb.Append($"[color={Gold}]•[/color]  {Mark(l)}\n\n");
        }
        return sb.ToString().TrimEnd();
    }
}
