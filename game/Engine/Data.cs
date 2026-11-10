using System.Text.Json;

namespace PoC.Engine;

public enum Kind { Monster, Spell, Trap }

public sealed class CardData
{
    public int Idx, KonamiId, Passcode, Level, Atk, Def;
    public int YgoId;   // YGOPRODeck id = real passcode; set for the PoC cards whose card_pass.bin entry is blank (deck files)
    public int RealPasscode => Passcode > 0 ? Passcode : YgoId;
    public string Name = "", Frame = "", Attribute = "", Race = "", Desc = "";
    public Kind Kind;
    public int PackMask; // 1 Yugi, 2 Kaiba, 4 Joey (obtainable pools), 8 Jaden (GX expansion cards), 16 every expansion card
    public string? TcgDesc; // YGOPRODeck text, matching the HD scan (display only; the engine follows Desc)
    // Anime-pool cards (pool_cards.json, game/tools/pool_import.py): official current text and rulings, no PoC quirks.
    public bool Modern;
    public string[] Rulings = Array.Empty<string>();
    public string[] Series = Array.Empty<string>();
    public string Archetype = "";
    public bool StatsUnknown;   // printed ATK/DEF "?" (Slifer, Ra): 0 on the field before effects
    public string AtkText => StatsUnknown || Atk < 0 ? "?" : Atk.ToString();
    public string DefText => StatsUnknown || Def < 0 ? "?" : Def.ToString();   // the archetype the card database files it under (card lab families)

    /// Archetype membership by the official rule: the archetype's name is part of the card's name (case matters:
    /// "HERO" covers Elemental/Destiny/Evil HERO, not "Hero Kid").
    public bool In(string archetype) => Name.Contains(archetype, StringComparison.Ordinal);

    public bool IsMonster => Kind == Kind.Monster;
    public bool IsSpell => Kind == Kind.Spell;
    public bool IsTrap => Kind == Kind.Trap;
    public bool IsFusion => IsMonster && Frame == "Fusion";
    public bool IsRitual => IsMonster && Frame == "Ritual";
    public bool IsEffect => IsMonster && Frame == "Effect";
    public bool IsNormalMonster => IsMonster && Frame == "Normal";
    // Spell/Trap subtype lives in Frame: Normal, Continuous, Equip, Field, Quick-Play, Ritual, Counter.
    public bool Is(string frame) => Frame == frame;
    public override string ToString() => Name;
}

public sealed record Fusion(int Result, int A, int B);
public sealed record Ritual(int Monster, int Spell, int Levels);

public sealed class EffectInfo
{
    public int KonamiId;
    public string Name = "", Trig = "", Does = "";
    public string? Quirk, Cond, Cost;
    public string Src = "";      // "code" = decoded from the exe, "text" = written from the card text, "table"
    public bool Probable;        // its exe routine was only probably located
}

// All card data, loaded once from the embedded JSON (remake/data).
public sealed class CardDb
{
    public const int JadenBit = 8, Expansion = 16;   // PackMask bits of the official expansion cards
    public readonly Dictionary<int, CardData> ByIdx = new();
    public readonly Dictionary<int, List<CardData>> ByKonami = new();
    public readonly Dictionary<string, List<CardData>> ByName = new(StringComparer.OrdinalIgnoreCase);
    public readonly Dictionary<int, EffectInfo> Effects = new();
    public readonly List<Fusion> Fusions = new();
    public readonly List<Ritual> Rituals = new();
    public readonly Dictionary<string, Dictionary<int, int>> Limits = new(); // game -> konami -> max
    public readonly Dictionary<int, Dictionary<string, int>> AttributeBoosts = new();
    public readonly Dictionary<string, JsonElement> EffectValues = new(); // "konami" -> values

    static CardDb? shared;
    public static CardDb Shared => shared ??= new CardDb();

    static JsonDocument Res(string name)
    {
        var s = typeof(CardDb).Assembly.GetManifestResourceStream(name) ?? throw new FileNotFoundException(name);
        return JsonDocument.Parse(s);
    }

    CardDb()
    {
        foreach (var e in Res("cards.json").RootElement.EnumerateArray())
        {
            var kind = e.GetProperty("kind").GetString() switch { "spell" => Kind.Spell, "trap" => Kind.Trap, _ => Kind.Monster };
            var c = new CardData
            {
                Idx = e.GetProperty("idx").GetInt32(),
                KonamiId = e.GetProperty("konami_id").GetInt32(),
                Passcode = Int(e, "passcode"),
                Name = e.GetProperty("name").GetString()!,
                Kind = kind,
                Frame = (e.TryGetProperty("frame", out var f) ? f : e.GetProperty("subtype")).GetString() ?? "",
                Attribute = Str(e, "attribute"),
                Race = Str(e, "race"),
                Level = Int(e, "level"),
                Atk = Int(e, "atk"),
                Def = Int(e, "def"),
                Desc = Str(e, "desc"),
                YgoId = Int(e, "ygoprodeck_id"),
            };
            if (e.TryGetProperty("pack_raw", out var p) && p.ValueKind == JsonValueKind.Object)
                c.PackMask = p.TryGetProperty("joey", out var j) ? j.GetInt32() : 0;
            ByIdx[c.Idx] = c;
            Add(ByKonami, c.KonamiId, c);
            Add(ByName, c.Name, c);
        }

        foreach (var e in Res("pool_cards.json").RootElement.EnumerateArray())
        {
            var c = new CardData
            {
                Idx = e.GetProperty("idx").GetInt32(), KonamiId = e.GetProperty("konami_id").GetInt32(), Passcode = Int(e, "passcode"),
                Name = Str(e, "name"), Frame = Str(e, "frame"), Attribute = Str(e, "attribute"), Race = Str(e, "race"),
                Level = Int(e, "level"), Atk = Int(e, "atk"), Def = Int(e, "def"), Desc = Str(e, "desc"), Modern = true,
                Kind = Str(e, "kind") switch { "spell" => Kind.Spell, "trap" => Kind.Trap, _ => Kind.Monster },
                Rulings = e.GetProperty("rulings").EnumerateArray().Select(x => x.GetString()!).ToArray(),
                Series = e.GetProperty("series").EnumerateArray().Select(x => x.GetString()!).ToArray(),
                Archetype = Str(e, "archetype"),
            };
            c.TcgDesc = c.Desc;
            // Expansion cards (v1.0): bit 16 = every official card (Shop, Tournament prizes), bit 8 = GX cards (Jaden's rewards).
            c.PackMask = Expansion | (c.Series.Contains("GX") ? JadenBit : 0);
            ByIdx[c.Idx] = c;
            Add(ByKonami, c.KonamiId, c);
            Add(ByName, c.Name, c);
        }

        // The three games each brought their own copy of some cards (Dark Magician x3, Blue-Eyes x2...): same name, same
        // art. The collection keeps one: the first obtainable printing gets every pack bit, the others none (Canon).
        foreach (var l in ByName.Values.Where(l => l.Count > 1 && l[0].Name.Length > 0))
        {
            var keep = l.FirstOrDefault(c => c.PackMask != 0) ?? l[0];
            foreach (var c in l) { canon[c.Idx] = keep.Idx; if (c != keep) { keep.PackMask |= c.PackMask; c.PackMask = 0; } }
        }

        foreach (var kv in Res("tcg_text.json").RootElement.EnumerateObject())
            if (ByIdx.TryGetValue(int.Parse(kv.Name), out var td)) td.TcgDesc = kv.Value.GetString();
        foreach (var (name, atk, text) in Gods) if (ByName.TryGetValue(name, out var gl)) foreach (var g in gl) MakeGod(g, atk, text);

        foreach (var kv in Res("effects.json").RootElement.GetProperty("cards").EnumerateObject())
        {
            var sp = kv.Name.IndexOf(' ');
            var id = int.Parse(kv.Name[..sp]);
            var v = kv.Value;
            Effects[id] = new EffectInfo
            {
                KonamiId = id, Name = kv.Name[(sp + 1)..], Trig = Str(v, "trig"), Does = Str(v, "does"),
                Quirk = Opt(v, "quirk"), Cond = Opt(v, "cond"), Cost = Opt(v, "cost"), Src = Str(v, "src"),
                Probable = v.TryGetProperty("code", out var code) && code.ToString().Contains("probable"),
            };
            if (v.TryGetProperty("values", out var vals)) EffectValues[id.ToString()] = vals.Clone();
        }

        var rt = Res("rule_tables.json").RootElement;
        // Fusion and ritual tables are identical in all three exes; Joey's is the superset.
        foreach (var f in rt.GetProperty("joey").GetProperty("fusion").EnumerateArray())
        {
            var ids = f.GetProperty("ids").EnumerateArray().Select(x => x.GetInt32()).ToArray();
            Fusions.Add(new Fusion(ids[0], ids[1], ids[2]));
        }
        foreach (var r in rt.GetProperty("joey").GetProperty("ritual").EnumerateArray())
        {
            var ids = r.GetProperty("ids").EnumerateArray().Select(x => x.GetInt32()).ToArray();
            Rituals.Add(new Ritual(ids[0], ids[1], r.GetProperty("tribute_levels").GetInt32()));
        }
        foreach (var g in rt.EnumerateObject())
        {
            var lim = Limits[g.Name] = new();
            foreach (var l in g.Value.GetProperty("limits").EnumerateArray())
                lim[l.GetProperty("id").GetInt32()] = l.GetProperty("max").GetInt32();
        }
        // Expansion cards on both lists, as the TCG Forbidden/Limited lists of the GX era (2005-2008) had them.
        foreach (var (name, max) in ExpansionLimits)
            if (ByName.TryGetValue(name, out var cl))
                foreach (var lim in Limits.Values) foreach (var c in cl.Where(c => c.Modern)) lim[c.KonamiId] = max;
        foreach (var b in rt.GetProperty("joey").GetProperty("attribute_boosts").EnumerateArray())
            AttributeBoosts[b.GetProperty("id").GetInt32()] =
                b.GetProperty("atk_by_attribute").EnumerateObject().ToDictionary(x => x.Name, x => x.Value.GetInt32());
    }

    static readonly (string, int)[] ExpansionLimits =
    {
        ("Magical Scientist", 0), ("Butterfly Dagger - Elma", 0), ("Dimension Fusion", 0), ("Metamorphosis", 0), ("Exchange of the Spirit", 0),
        ("Last Turn", 0), ("Return from the Different Dimension", 0), ("Chaos Emperor Dragon - Envoy of the End", 0), ("Black Luster Soldier - Envoy of the Beginning", 0),
        ("Dark Magician of Chaos", 1), ("Card Trooper", 1), ("Destiny HERO - Disk Commander", 1), ("Gold Sarcophagus", 1), ("Destiny HERO - Malicious", 1),
        ("Overload Fusion", 1), ("Future Fusion", 1), ("Monster Gate", 1), ("Burial from a Different Dimension", 1), ("Treeborn Frog", 1), ("Mind Control", 1),
        ("Rescue Cat", 1), ("Brain Control", 1), ("Volcanic Shell", 1), ("Cyber Valley", 1),
    };

    /// The Egyptian Gods: blank 0/0 cards in the original games, given their current official text (their HD art prints it).
    /// ATK -1 = "?" (0 on the field before effects).
    static readonly (string Name, int Atk, string Text)[] Gods =
    {
        ("Obelisk the Tormentor", 4000, "Requires 3 Tributes to Normal Summon (cannot be Normal Set). This card's Normal Summon cannot be negated. When Normal Summoned, cards and effects cannot be activated. Neither player can target this card with card effects. Once per turn, during the End Phase, if this card was Special Summoned: Send it to the GY. You can Tribute 2 monsters; destroy all monsters your opponent controls. This card cannot declare an attack the turn this effect is activated."),
        ("Slifer the Sky Dragon", -1, "Requires 3 Tributes to Normal Summon (cannot be Normal Set). This card's Normal Summon cannot be negated. When Normal Summoned, cards and effects cannot be activated. Once per turn, during the End Phase, if this card was Special Summoned: Send it to the GY. Gains 1000 ATK/DEF for each card in your hand. If a monster(s) is Normal or Special Summoned to your opponent's field in Attack Position: That monster(s) loses 2000 ATK, then if its ATK has been reduced to 0 as a result, destroy it."),
        ("The Winged Dragon of Ra", -1, "Cannot be Special Summoned. Requires 3 Tributes to Normal Summon (cannot be Normal Set). This card's Normal Summon cannot be negated. When Normal Summoned, other cards and effects cannot be activated. When this card is Normal Summoned: You can pay LP so that you only have 100 left; this card gains ATK/DEF equal to the amount of LP paid. You can pay 1000 LP, then target 1 monster on the field; destroy that target."),
    };

    static void MakeGod(CardData g, int atk, string text)
    {
        g.Frame = "Effect"; g.Attribute = "DIVINE"; g.Race = "Divine-Beast"; g.Level = 10;
        g.Atk = g.Def = Math.Max(0, atk); g.StatsUnknown = atk < 0;
        g.Desc = g.TcgDesc = text; g.Modern = true; g.Archetype = "Egyptian God";
        g.PackMask |= Expansion;   // collectable (the Shop), like the expansion cards
    }

    static void Add<K>(Dictionary<K, List<CardData>> d, K k, CardData c) where K : notnull
    {
        if (!d.TryGetValue(k, out var l)) d[k] = l = new();
        l.Add(c);
    }
    static string Str(JsonElement e, string n) => e.TryGetProperty(n, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString()! : "";
    static string? Opt(JsonElement e, string n) => e.TryGetProperty(n, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;
    static int Int(JsonElement e, string n) => e.TryGetProperty(n, out var v) && v.ValueKind == JsonValueKind.Number ? v.GetInt32() : 0;

    public CardData Get(int idx) => ByIdx[idx];
    readonly Dictionary<int, int> canon = new();
    /// The collection's printing of a card (one per name): owned copies, rewards and the shop all use this idx.
    public int Canon(int idx) => canon.TryGetValue(idx, out var k) ? k : idx;
    public CardData Named(string name) => ByName.TryGetValue(name, out var l) ? l[0] : throw new KeyNotFoundException(name);

    /// Deck from decks.json ("yugi/cpu01") or remastered_decks.json ("yugi/x_lv3"): main and fusion idx lists.
    public (List<int> Main, List<int> Extra) Deck(string key)
    {
        if (Remastered(key))
        {
            using var r = Res("remastered_decks.json");
            var x = r.RootElement.GetProperty(key);
            List<int> N(string s) => x.GetProperty(s).EnumerateObject().SelectMany(c => Enumerable.Repeat(Named(c.Name).Idx, c.Value.GetInt32())).ToList();
            return (N("main"), N("fusion"));
        }
        using var d = Res("decks.json");
        var e = d.RootElement.GetProperty(key);
        List<int> L(string s) => e.TryGetProperty(s, out var a) ? a.EnumerateArray().Select(x => x.GetProperty("idx").GetInt32()).ToList() : new();
        return (L("main"), L("fusion"));
    }

    /// Remake-authored decks (remastered_decks.json): rival ladders `rival/x_*` and tournament duelists `tour/*`.
    static bool Remastered(string key) => key.Contains("/x_") || key.StartsWith("tour/");

    /// A CPU deck's side deck (empty for most decks).
    public List<int> DeckSide(string key)
    {
        using var d = Res(Remastered(key) ? "remastered_decks.json" : "decks.json");
        var e = d.RootElement.GetProperty(key);
        if (!e.TryGetProperty("side", out var a)) return new();
        return a.ValueKind == JsonValueKind.Object
            ? a.EnumerateObject().SelectMany(c => Enumerable.Repeat(Named(c.Name).Idx, c.Value.GetInt32())).ToList()
            : a.EnumerateArray().Select(x => x.GetProperty("idx").GetInt32()).ToList();
    }

    /// The remake's hand-built EXPERT decks (remastered_decks.json).
    public IEnumerable<string> RemasteredDeckNames()
    {
        using var d = Res("remastered_decks.json");
        return d.RootElement.EnumerateObject().Select(p => p.Name).Where(n => !n.StartsWith('_')).ToList();
    }

    /// The original CPU decks (decks.json).
    public IEnumerable<string> DeckNames()
    {
        using var d = Res("decks.json");
        return d.RootElement.EnumerateObject().Select(p => p.Name).ToList();
    }
}
