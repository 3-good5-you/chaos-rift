using System.Text;
using System.Text.Json;

namespace PoC.Engine;

/// The save file: profile, decks and options as readable JSON. Cards are stored by idx, not konami id:
/// alternate-art printings (two Blue-Eyes, two Polymerization) share a konami id, so only idx says which one you own.
public static class SaveData
{
    public const int Version = 1;

    /// Player options (the game layer maps them to its settings).
    public sealed class Options
    {
        public string Name = "Player", BanList = "joey";
        public int Picture, Speed;
        public float Music, Sound;
        public bool Fullscreen;
    }

    /// Everything the save holds. Playing = index into Decks, -1 for none.
    public sealed class State
    {
        public Profile Profile = new();
        public List<Deck> Decks = new();
        public int Playing = -1;
        public Options Options = new();
    }

    // The file's shape.
    sealed class FileDto
    {
        public int Version;
        public ProfileDto Profile = new();
        public List<DeckDto> Decks = new();
        public int Playing = -1;
        public Options Options = new();
    }
    sealed class ProfileDto
    {
        public Dictionary<int, int> Owned = new();
        public List<int> Fresh = new();
        public int CollectionPercent;
        public bool CarriedOver;
        public Dictionary<string, RivalRecord> Rivals = new();
        public int JoeyGauge;
        public bool JoeyBonus;
        public int DryRuns;     // added after v1 shipped: older files load it as 0
        // Shop (added after v1 too): older files load 0 DP, nothing seen or bought, the default mat and back.
        public int Dp;
        public List<int> Seen = new();
        public List<string> Mats = new(), Backs = new(), Frames = new();
        public string Mat = Shop.DefaultMat, Back = Shop.DefaultBack, Frame = Shop.DefaultFrame;
        // CPU learning (added later): older files start from the shipped model.
        public string? Brain;
        public List<string>? Starters;   // added later: null = older file (Session works it out from the deck names)
        public List<string>? Solved;     // Challenges (added 2026-11-08)
        public string BrainFrom = "";
    }
    sealed class DeckDto
    {
        public string Name = "Deck";
        public int Cover = -1;
        public List<int> Main = new(), Fusion = new(), Side = new();
    }

    static readonly JsonSerializerOptions Json = new() { IncludeFields = true, WriteIndented = true };

    public static string ToJson(State s)
    {
        var p = s.Profile;
        var f = new FileDto
        {
            Version = Version,
            Profile = new ProfileDto
            {
                Owned = new(p.Owned), Fresh = p.Fresh.Order().ToList(), CollectionPercent = p.CollectionPercent,
                CarriedOver = p.CarriedOver, Rivals = new(p.Rivals), JoeyGauge = p.JoeyGauge, JoeyBonus = p.JoeyBonus,
                DryRuns = p.DryRuns,
                Dp = p.Dp, Seen = p.Seen.Order().ToList(), Mats = p.Mats.Order().ToList(), Backs = p.Backs.Order().ToList(), Frames = p.Frames.Order().ToList(), Mat = p.Mat, Back = p.Back, Frame = p.Frame,
                Brain = p.Brain.ToJson(), BrainFrom = p.BrainFrom, Starters = p.Starters.Order().ToList(), Solved = p.Solved.Order().ToList(),
            },
            Decks = s.Decks.Select(d => new DeckDto { Name = d.Name, Cover = d.Cover, Main = d.Main.ToList(), Fusion = d.Extra.ToList(), Side = d.Side.ToList() }).ToList(),
            Playing = s.Playing,
            Options = s.Options,
        };
        return JsonSerializer.Serialize(f, Json);
    }

    /// Throws on anything it can't trust: bad JSON, another version, unknown cards, a bad ban list.
    public static State FromJson(string json)
    {
        var f = JsonSerializer.Deserialize<FileDto>(json, Json) ?? throw new InvalidDataException("empty save");
        if (f.Version != Version) throw new InvalidDataException($"save version {f.Version}");
        var db = CardDb.Shared;
        void Known(IEnumerable<int> idx) { foreach (var i in idx) if (!db.ByIdx.ContainsKey(i)) throw new InvalidDataException($"unknown card {i}"); }
        var dp = f.Profile ?? throw new InvalidDataException("no profile");
        Known(dp.Owned.Keys); Known(dp.Fresh); Known(dp.Seen ?? new());
        var s = new State { Options = f.Options ?? throw new InvalidDataException("no options") };
        if (!DeckRules.Formats.Any(x => x.Key == s.Options.BanList)) throw new InvalidDataException($"ban list {s.Options.BanList}");
        var p = s.Profile;
        // Older saves may hold another game's printing of a card (Canon): its copies merge into the collection's one.
        foreach (var (k, v) in dp.Owned) p.Owned[db.Canon(k)] = Math.Clamp(p.Count(db.Canon(k)) + v, 0, 255);
        p.Fresh.UnionWith(dp.Fresh.Select(db.Canon));
        p.CollectionPercent = dp.CollectionPercent; p.CarriedOver = dp.CarriedOver;
        foreach (var (k, v) in dp.Rivals) p.Rivals[k] = v ?? new RivalRecord();
        p.JoeyGauge = dp.JoeyGauge; p.JoeyBonus = dp.JoeyBonus; p.DryRuns = Math.Max(0, dp.DryRuns);
        p.Dp = Math.Max(0, dp.Dp);
        p.Seen.UnionWith((dp.Seen ?? new()).Select(db.Canon));
        // Unknown cosmetics (renamed or removed) are dropped; an equipped one you don't own falls back to the default.
        p.Mats.UnionWith(dp.Mats ?? new()); p.Backs.UnionWith(dp.Backs ?? new()); p.Frames.UnionWith(dp.Frames ?? new());
        (p.Mat, p.Back, p.Frame) = (dp.Mat ?? "", dp.Back ?? "", dp.Frame ?? "");
        Shop.Tidy(p);
        if (dp.Starters != null) p.Starters.UnionWith(dp.Starters);
        if (dp.Solved != null) p.Solved.UnionWith(dp.Solved);
        else foreach (var (n, k) in DeckRules.Starters) if (f.Decks?.Any(d => d.Name == n) == true) p.Starters.Add(k.Split('/')[0]);
        // Weights are always the shipped ones: learning them from a handful of your duels drifted the CPU into never
        // attacking (playtest 2026-11-09). Only the card payoffs (Uses) carry over from the save.
        if (dp.Brain != null) { p.Brain = new Value(Value.Trained.W, Value.FromJson(dp.Brain).Uses); p.BrainFrom = dp.BrainFrom ?? ""; }
        foreach (var d in f.Decks ?? new())
        {
            Known(d.Main); Known(d.Fusion); Known(d.Side);
            var deck = new Deck(d.Name ?? "Deck", d.Main.Select(db.Canon), d.Fusion.Select(db.Canon)) { Cover = db.Canon(d.Cover) };
            deck.Side.AddRange(d.Side.Select(db.Canon));
            s.Decks.Add(deck);
        }
        s.Playing = f.Playing >= 0 && f.Playing < s.Decks.Count ? f.Playing : -1;
        return s;
    }

    /// Write to a temp file, flush it to disk, then replace the save, so a crash mid-write leaves the old save whole.
    public static void Write(string path, State s)
    {
        var tmp = path + ".tmp";
        using (var fs = new FileStream(tmp, FileMode.Create, FileAccess.Write))
        {
            fs.Write(Encoding.UTF8.GetBytes(ToJson(s)));
            fs.Flush(true);
        }
        File.Move(tmp, path, overwrite: true);
    }

    /// The saved state, or null for a new player. A file that can't be read is copied to `<path>.bad`
    /// (broken = true) and also gives null, so the game starts fresh.
    public static State? Read(string path, out bool broken)
    {
        broken = false;
        if (!File.Exists(path)) return null;
        try { return FromJson(File.ReadAllText(path)); }
        catch (Exception)
        {
            broken = true;
            try { File.Copy(path, path + ".bad", overwrite: true); } catch (IOException) { }
            return null;
        }
    }
}
