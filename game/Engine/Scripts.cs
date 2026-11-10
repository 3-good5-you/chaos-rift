using System.Reflection;

namespace PoC.Engine;

/// Registry of card scripts, keyed by Konami id. Alt-art copies fall back to a same-name script.
/// Effects are registered in layers: Effects/DataDriven.cs (tables from effects.json),
/// Effects/Templates.cs (common patterns), Effects/Cards*.cs (hand-written).
public static partial class Scripts
{
    static Dictionary<int, Script>? byId;
    static Dictionary<string, Script>? byName;

    static void Build()
    {
        byId = new();
        byName = new(StringComparer.OrdinalIgnoreCase);
        var db = CardDb.Shared;
        // Every method named Register* in this partial class adds scripts, in declaration order of layers.
        DataDriven(db);
        Templates(db);
        foreach (var m in typeof(Scripts).GetMethods(BindingFlags.NonPublic | BindingFlags.Static).Where(m => m.Name.StartsWith("Cards")).OrderBy(m => m.Name))
        {
            wiring = m.Name.StartsWith("CardsP");   // official-card batches: every card they touch is wired in full
            m.Invoke(null, new object[] { db });
        }
        wiring = false;
        PoolRules(db);   // Effects/Pool.cs: what official text says without a script (materials, summon conditions)
        foreach (var s in byName!.Values.Distinct()) Instrument(s);
    }

    /// How often each card's script code ran (activation, trigger, procedure). For coverage checks.
    public static readonly Dictionary<string, int> Hits = new();

    static void Hit(Script s) { lock (Hits) Hits[s.Name] = Hits.GetValueOrDefault(s.Name) + 1; }

    static void Instrument(Script s)
    {
        if (s.Run is { } run) s.Run = x => { Hit(s); return run(x); };
        if (s.IgnRun is { } ign) s.IgnRun = x => { Hit(s); return ign(x); };
        if (s.Proc is { } proc) s.Proc = x => { Hit(s); return proc(x); };
        if (s.GraveFx is { } g) { g.Name = s.Name; g.KonamiId = s.KonamiId; Instrument(g); }
        foreach (var f in s.Fx) { f.Name = s.Name; Instrument(f); }
        // A trigger counts when it did something observable (raised an event, asked, paid, boosted).
        if (s.On is { } on) s.On = (x, e) => { int before = x.D.Seq; var f = on(x, e); if (!f.IsCompleted || x.D.Seq != before) Hit(s); return f; };
    }

    /// Get or create the script for a card name (all Konami ids carrying that name share it).
    internal static Script S(string name)
    {
        if (wiring) Wired.Add(name);
        if (byName!.TryGetValue(name, out var s)) return s;
        var db = CardDb.Shared;
        if (!db.ByName.TryGetValue(name, out var l)) throw new KeyNotFoundException("No card named " + name);
        s = new Script { KonamiId = l[0].KonamiId, Name = l[0].Name };
        byName[name] = s;
        foreach (var d in l) byId![d.KonamiId] = s;
        return s;
    }

    /// Official cards whose whole text the engine plays (written in an Effects/CardsP*.cs batch, or plain Fusion text).
    public static readonly HashSet<string> Wired = new(StringComparer.OrdinalIgnoreCase);
    static bool wiring;
    public static bool IsWired(CardData d) { if (byId == null) Build(); return Wired.Contains(d.Name); }

    public static Script? Get(CardData d)
    {
        if (byId == null) Build();
        if (byId!.TryGetValue(d.KonamiId, out var s)) return s;
        return byName!.TryGetValue(d.Name, out s) ? s : null;
    }

    /// Effect cards (effects.json) with no implemented script.
    public static List<EffectInfo> Missing()
    {
        if (byId == null) Build();
        return CardDb.Shared.Effects.Values.Where(e => !(byId!.TryGetValue(e.KonamiId, out var s) && s.Implemented)).ToList();
    }
}
