using System.Text.RegularExpressions;

namespace PoC.Engine;

/// Card families for the card lab: official (pool) cards bundled by archetype and by naming each other in their text,
/// so cards that depend on each other (Exodia Necross and the Exodia pieces, the Elemental HEROes and their Fusions)
/// are tested together.
public static class Families
{
    static Dictionary<string, string>? of;
    static Dictionary<string, List<CardData>>? members;
    static Dictionary<string, List<string>>? mentions;

    /// Archetype labels from the card database that are really one family here.
    static string Norm(string a) => a switch
    {
        "roid" => "Roid",
        "Advanced Crystal Beast" => "Crystal Beast",
        "Chrysalis" => "Neo-Spacian",
        "Fan-Made Cards" or "Knight" or "Fairy" or "Dice" or "Anti" or "Mask" or "Charmer" or "The Agent" or "Book of" => "",
        _ => a,
    };

    /// The card's family ("" = none: it stands alone).
    public static string Of(CardData d) { Build(); return of!.GetValueOrDefault(d.Name, ""); }

    /// The other cards that matter when testing this one: its family, the cards its text names, and the cards naming it.
    public static List<CardData> Related(CardData d)
    {
        Build();
        var db = CardDb.Shared;
        var l = new List<CardData>();
        void Add(CardData c) { if (c.Name != d.Name && !l.Any(x => x.Name == c.Name)) l.Add(c); }
        foreach (var n in mentions!.GetValueOrDefault(d.Name, new())) if (db.ByName.TryGetValue(n, out var c)) Add(c[0]);
        foreach (var (n, ms) in mentions!) if (ms.Contains(d.Name) && db.ByName.TryGetValue(n, out var c)) Add(c[0]);
        if (Of(d) is { Length: > 0 } f) foreach (var c in members![f]) Add(c);
        return l;
    }

    static void Build()
    {
        if (of != null) return;
        var db = CardDb.Shared;
        var pool = db.ByIdx.Values.Where(d => d.Modern && d.Name.Length > 0).GroupBy(d => d.Name).Select(g => g.First()).ToList();
        var names = pool.Select(d => d.Name).ToHashSet();
        var archs = pool.Select(d => Norm(d.Archetype)).Where(a => a.Length > 0).ToHashSet();
        mentions = pool.ToDictionary(d => d.Name, d => Regex.Matches(d.Desc, "\"([^\"]+)\"").Select(m => m.Groups[1].Value).Where(n => n != d.Name).Distinct().ToList());
        var named = mentions.Values.SelectMany(x => x).GroupBy(n => n).ToDictionary(g => g.Key, g => g.Count());

        var parent = new Dictionary<string, string>();
        string Find(string x) { while (parent.TryGetValue(x, out var p) && p != x) x = parent[x] = parent.GetValueOrDefault(p, p); return x; }
        void Union(string a, string b) { parent.TryAdd(a, a); parent.TryAdd(b, b); var (ra, rb) = (Find(a), Find(b)); if (ra != rb) parent[ra] = rb; }
        foreach (var d in pool) parent.TryAdd(d.Name, d.Name);

        foreach (var d in pool)
        {
            var own = Norm(d.Archetype);
            if (own.Length > 0) Union(d.Name, "#" + own);
            foreach (var q in mentions[d.Name])
            {
                // Another official card it names: same family, unless that card is a hub many cards name.
                if (names.Contains(q) && named[q] <= 6) Union(d.Name, q);
                // A card or archetype it names that belongs to a family (Exodia Necross -> "Exodia the Forbidden One").
                else if (own.Length == 0 && archs.FirstOrDefault(a => q.Contains(a, StringComparison.Ordinal)) is { } a) Union(d.Name, "#" + a);
            }
        }

        var groups = pool.GroupBy(d => Find(d.Name)).ToList();
        of = new(); members = new();
        foreach (var g in groups)
        {
            var root = g.Key;
            var tags = parent.Keys.Where(k => k.StartsWith('#') && Find(k) == root).Select(k => k[1..]).ToList();
            string fam = tags.Count > 0 ? tags.OrderByDescending(t => g.Count(d => Norm(d.Archetype) == t)).ThenBy(t => t).First()
                : g.Count() > 1 ? g.OrderByDescending(d => g.Count(x => mentions[x.Name].Contains(d.Name))).ThenBy(d => d.Name).First().Name : "";
            if (fam.Length == 0) continue;
            members[fam] = g.OrderBy(d => d.Name).ToList();
            foreach (var d in g) of[d.Name] = fam;
        }
    }
}
