using System.Text.RegularExpressions;

namespace PoC.Engine;

// Official-text cards (the anime pool, CardData.Modern): shared vocabulary, and the rules read straight off the text
// (Fusion Materials listed by name, "Must be Fusion Summoned", "Cannot be Normal Summoned/Set"). Card scripts live in
// Effects/CardsP*.cs, one file per group of cards, each checked against its text and rulings.
public static partial class Scripts
{
    /// The name a card goes by right now ("This card's name is treated as X while face-up on the field").
    public static string NameOf(Card c) => c.TempName is { } tn && c.Loc == Loc.Monster && c.FaceUp ? tn : Get(c.Data) is { TreatedAs: { } n } s && (s.TreatedAlways || (c.Loc == Loc.Monster && c.FaceUp)) ? n : c.Name;

    /// Archetype membership, by the official rule (the archetype's name is in the card's name, case matters).
    public static bool In(this Card c, string archetype) => NameOf(c).Contains(archetype, StringComparison.Ordinal) || (Get(c.Data)?.AlsoIn?.Contains(archetype) ?? false);

    // "You can only use this effect of X once per turn" / "You can only activate 1 X per turn": per player and card name.
    internal static bool Hopt(Ctx x, string key = "") => x.D.Flags.GetValueOrDefault($"hopt:{x.Self.Name}{key}:{x.Me}") != x.D.Turn;
    internal static void UseHopt(Ctx x, string key = "") => x.D.Flags[$"hopt:{x.Self.Name}{key}:{x.Me}"] = x.D.Turn;

    // Counters on a card (cleared when it leaves the field). kind "" = a plain counter.
    public static int Counters(this Card c, string kind = "") => c.Var("ctr:" + kind);
    internal static void AddCounter(Ctx x, Card c, int n = 1, string kind = "") { c.Vars["ctr:" + kind] = Math.Max(0, c.Counters(kind) + n); x.D.Seq++; }

    /// "Could make a player discard when it resolves" (Gravekeeper's Watcher, Gemini Imps): a discard in an effect, not a
    /// cost ("Discard 1 card; ...") and not a trigger on being discarded.
    public static bool DiscardsOnResolve(string desc) => desc.Split('.').Any(sentence =>
    {
        int at = sentence.IndexOf("discard", StringComparison.OrdinalIgnoreCase), semi = sentence.IndexOf(';');
        return at >= 0 && !(semi >= 0 && at < semi) && !Regex.IsMatch(sentence, @"(is|are) discarded", RegexOptions.IgnoreCase);
    });

    /// Mark cards whose whole effect is one plain engine action (no timing or ruling questions): no card lab test needed.
    internal static void Sure(params string[] names) { foreach (var n in names) S(n).Sure = true; }

    /// The rules written on official cards that need no script of their own.
    static void PoolRules(CardDb db)
    {
        foreach (var d in db.ByIdx.Values.Where(d => d.Modern && d.IsMonster).GroupBy(d => d.Name).Select(g => g.First()))
        {
            bool fusionOnly = d.Desc.Contains("Must be Fusion Summoned") || d.Desc.Contains("cannot be Special Summoned except by Fusion Summon");
            bool noNormal = Regex.IsMatch(d.Desc, @"(This card )?[Cc]annot be Normal Summoned( or |/)Set");
            var mats = d.IsFusion && !d.Desc.Contains("You do not use \"Polymerization\"") ? ParseMaterials(db, d.Desc) : null;
            if (!fusionOnly && !noNormal && mats == null) continue;
            var s = S(d.Name);
            s.MustFusion |= fusionOnly;
            s.NoNormalSummon |= noNormal;
            // "A Fusion Summon of this card can only be done with the above Fusion Materials": no substitutes.
            if (mats != null && System.Text.RegularExpressions.Regex.IsMatch(d.Desc, @"can only be (conducted|done) with the above Fusion Material"))
                mats = mats.Select(m => m with { Name = null }).ToList();
            s.Materials ??= mats;
            // Nothing but materials and "Must be Fusion Summoned": the rules above are the whole card.
            if (mats != null && Regex.Replace(string.Join("\n", d.Desc.Split('\n').Skip(1)), @"Must be Fusion Summoned and cannot be Special Summoned by other ways\.|\(This card is always treated as[^)]*\)|A Fusion Summon of this (card|monster) can only be (conducted|done) with the above Fusion Material( Monster)?s\.", "").Trim().Length == 0)
                { Wired.Add(d.Name); s.Sure = true; }
        }
    }

    /// First line of a Fusion Monster's text when every material is listed by name: "A" + "B" (+ 2 "C").
    static List<Mat>? ParseMaterials(CardDb db, string desc)
    {
        var line = desc.Split('\n')[0].Trim();
        var mats = new List<Mat>();
        foreach (var part in line.Split(" + "))
        {
            var m = Regex.Match(part.Trim(), "^(?:(\\d+) )?\"([^\"]+)\"$");
            if (!m.Success) return null;
            var name = m.Groups[2].Value;
            int n = m.Groups[1].Success ? int.Parse(m.Groups[1].Value) : 1;
            // Official names keep "HERO" in capitals; the older printed text writes "Hero".
            name = db.ByName.TryGetValue(name, out var l) ? l[0].Name : name;
            for (int i = 0; i < n; i++) mats.Add(Named(name));
        }
        return mats.Count >= 2 ? mats : null;
    }

    /// A Fusion Material listed by name (its "treated as" name counts; substitutes may stand in).
    internal static Mat Named(string name) => new(name, c => string.Equals(NameOf(c), name, StringComparison.OrdinalIgnoreCase), name);
    /// A Fusion Material by condition ("1 "Elemental HERO" monster"): no substitutes.
    internal static Mat Cond(string label, Func<Card, bool> ok) => new(label, ok);

    /// The player Fusion Summoning right now (materials "in your GY" / "in your opponent's GY").
    [ThreadStatic] public static int FusingPlayer;
}
