using System.Text;
using System.Text.Json;

namespace PoC.Engine;

/// The save file: profile, decks and options as JSON, stored signed and scrambled (Pack) so it can't be read or
/// edited by hand. Cards are stored by idx, not konami id:
/// alternate-art printings (two Blue-Eyes, two Polymerization) share a konami id, so only idx says which one you own.
public static partial class SaveData
{
    public const int Version = 1;

    /// Player options (the game layer maps them to its settings).
    public sealed class Options
    {
        public string Name = "Player", BanList = "joey", Lang = "en";
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
        public Dictionary<string, int>? Stats;          // achievements (added 2026-11-16): null = older file
        public List<string>? Claimed, Told;
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
                Stats = new(p.Stats), Claimed = p.Claimed.Order().ToList(), Told = p.Told.Order().ToList(),
            },
            Decks = s.Decks.Select(d => new DeckDto { Name = d.Name, Cover = d.Cover, Main = d.Main.ToList(), Fusion = d.Extra.ToList(), Side = d.Side.ToList() }).ToList(),
            Playing = s.Playing,
            Options = s.Options,
        };
        return JsonSerializer.Serialize(f, Json);
    }

    /// Lightning Crash, Ojamandala, Ritual Sanctuary, Wicked Canon, Hope for the Future (removed 2026-11-16).
    static readonly HashSet<int> Removed = new() { 2876, 2924, 2960, 3053, 3158 };

    /// Throws on anything it can't trust: bad JSON, another version, unknown cards, a bad ban list.
    public static State FromJson(string json)
    {
        var f = JsonSerializer.Deserialize<FileDto>(json, Json) ?? throw new InvalidDataException("empty save");
        if (f.Version != Version) throw new InvalidDataException($"save version {f.Version}");
        var db = CardDb.Shared;
        void Known(IEnumerable<int> idx) { foreach (var i in idx) if (!db.ByIdx.ContainsKey(i)) throw new InvalidDataException($"unknown card {i}"); }
        var dp = f.Profile ?? throw new InvalidDataException("no profile");
        // Cards taken out of the game (no English HD scan): copies a save still holds are dropped, not rejected.
        foreach (var r in Removed) dp.Owned.Remove(r);
        dp.Fresh.RemoveAll(Removed.Contains); dp.Seen?.RemoveAll(Removed.Contains);
        foreach (var d in f.Decks ?? new())
        {
            d.Main.RemoveAll(Removed.Contains); d.Fusion.RemoveAll(Removed.Contains); d.Side.RemoveAll(Removed.Contains);
            if (Removed.Contains(d.Cover)) d.Cover = -1;
        }
        Known(dp.Owned.Keys); Known(dp.Fresh); Known(dp.Seen ?? new());
        var s = new State { Options = f.Options ?? throw new InvalidDataException("no options") };
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
        if (dp.Stats != null) foreach (var (k, v) in dp.Stats) p.Stats[k] = v;
        p.Claimed.UnionWith(dp.Claimed ?? new());
        if (dp.Told != null) p.Told.UnionWith(dp.Told);
        else Achievements.Quiet(p, s.Decks.Count);   // a save from before achievements: no pop-up storm on first launch
        return s;
    }

    /// Write to a temp file, flush it to disk, then replace the save, so a crash mid-write leaves the old save whole.
    /// The save it replaces is kept as `<path>.bak` (Read falls back to it).
    public static void Write(string path, State s)
    {
        var tmp = path + ".tmp";
        using (var fs = new FileStream(tmp, FileMode.Create, FileAccess.Write))
        {
            fs.Write(Encoding.ASCII.GetBytes(Pack(ToJson(s))));
            fs.Flush(true);
        }
        if (File.Exists(path)) File.Copy(path, path + ".bak", overwrite: true);
        File.Move(tmp, path, overwrite: true);
    }

    /// The saved state, or null for a new player. A file that can't be read (broken, or changed by hand) is copied
    /// to `<path>.bad` (broken = true); the last good save `<path>.bak` is used instead, else the game starts fresh.
    public static State? Read(string path, out bool broken)
    {
        broken = false;
        if (!File.Exists(path)) return null;
        try { return FromJson(Unpack(File.ReadAllText(path), plain: true)); }
        catch (Exception)
        {
            broken = true;
            try { File.Copy(path, path + ".bad", overwrite: true); } catch (IOException) { }
            try { return File.Exists(path + ".bak") ? FromJson(Unpack(File.ReadAllText(path + ".bak"), plain: true)) : null; }
            catch (Exception) { return null; }
        }
    }

    /// A save file someone picked to import: only a signed Chaos Rift save; throws Changed (edited outside the game)
    /// or InvalidDataException (not a save, another version, unknown cards) on anything else.
    public static State Import(string path)
    {
        var text = new FileInfo(path).Length <= 8 << 20 ? File.ReadAllText(path) : throw new InvalidDataException("too big for a save");
        return FromJson(Unpack(text, plain: false));
    }

    // ---------------------------------------------------------------- signed + scrambled file
    // "CHAOSRIFT-SAVE-1:<base64 of the gzipped JSON, XOR-masked>.<HMAC-SHA256 of those bytes>". The key lives in
    // the git-ignored SaveKey.local.cs, so the public source can't forge saves; builds without it use a dev key.
    // ponytail: the key ships inside the game, so a determined hacker can still dig it out; only a server stops that.

    const string Magic = "CHAOSRIFT-SAVE-1:";

    /// Thrown for a save whose signature doesn't match: changed outside the game (or by another build's key).
    public sealed class Changed() : IOException("this save was changed outside the game");

    public static string Pack(string json)
    {
        using var ms = new MemoryStream();
        using (var z = new System.IO.Compression.GZipStream(ms, System.IO.Compression.CompressionLevel.Optimal)) z.Write(Encoding.UTF8.GetBytes(json));
        var b = ms.ToArray();
        Mask(b);
        return Magic + Convert.ToBase64String(b) + "." + Convert.ToHexString(System.Security.Cryptography.HMACSHA256.HashData(Key, b));
    }

    /// The JSON inside a packed save. `plain` also lets an old readable save (2.0 and before) through.
    public static string Unpack(string text, bool plain)
    {
        text = text.Trim();
        if (!text.StartsWith(Magic, StringComparison.Ordinal))
            return plain && text.StartsWith('{') ? text : throw new InvalidDataException("not a Chaos Rift save");
        int dot = text.LastIndexOf('.');
        if (dot < Magic.Length) throw new InvalidDataException("not a Chaos Rift save");
        byte[] b, tag;
        try { (b, tag) = (Convert.FromBase64String(text[Magic.Length..dot]), Convert.FromHexString(text[(dot + 1)..])); }
        catch (FormatException) { throw new Changed(); }
        if (!System.Security.Cryptography.CryptographicOperations.FixedTimeEquals(tag, System.Security.Cryptography.HMACSHA256.HashData(Key, b))) throw new Changed();
        Mask(b);
        using var z = new System.IO.Compression.GZipStream(new MemoryStream(b), System.IO.Compression.CompressionMode.Decompress);
        using var r = new StreamReader(z, Encoding.UTF8);
        return r.ReadToEnd();
    }

    /// XOR with a key-derived stream (SHA-256 in counter mode): the file isn't plain base64 + gzip to anyone who
    /// recognises those. Applying it twice gives the bytes back.
    static void Mask(byte[] b)
    {
        var block = new byte[Key.Length + 4];
        Key.CopyTo(block, 0);
        for (int i = 0; i < b.Length; i += 32)
        {
            BitConverter.TryWriteBytes(block.AsSpan(Key.Length), i);
            var pad = System.Security.Cryptography.SHA256.HashData(block);
            for (int j = 0; j < 32 && i + j < b.Length; j++) b[i + j] ^= pad[j];
        }
    }
}

#if !SAVEKEY
public static partial class SaveData
{
    // Dev key for builds without SaveKey.local.cs (GitHub clones): their saves don't load in release builds.
    static readonly byte[] Key = Encoding.ASCII.GetBytes("chaos-rift-dev-save-key-not-secret");
}
#endif
