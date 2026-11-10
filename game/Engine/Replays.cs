using System.Text.Json;

namespace PoC.Engine;

/// A saved duel. The engine is deterministic, so (Setup, Log) is the whole duel: the viewer re-runs it command by
/// command. The rest is only for the list and the table dressing. Cards are stored by idx like DuelSetup;
/// `Hash` (the duel's end state) tells the viewer whether this build still plays the duel the same way.
public sealed class Replay
{
    public const int Version = 1;
    public int V = Version;
    public string Name = "";            // the player's own name for it (asked on SAVE REPLAY); "" = "VS <them>"
    public string When = "";            // local time, "yyyy-MM-dd HH:mm"
    public string You = "", Them = "";
    public string? Rival;               // the CPU rival's file key (yugi, kaiba...); null = a LAN opponent
    public string Mode = "";            // shown in the list: "Duel", "Match 2", "LAN", "Tournament"...
    public int Seat;                    // which engine player was you
    public int Winner;                  // 0 you, 1 them, -1 draw (seen from you)
    public string Reason = "";
    public int Turns;
    public string Mat = "", Back = "", Back2 = "";
    public string? Mat2;
    public DuelSetup Setup = new();
    public List<Command> Log = new();
    public ulong Hash;

    static readonly JsonSerializerOptions Json = new() { IncludeFields = true, WriteIndented = false };

    /// Snapshot a finished (or surrendered) duel; the caller fills the names and dressing.
    public static Replay Of(Duel d, int seat, int winner, string reason) => new()
    {
        When = DateTime.Now.ToString("yyyy-MM-dd HH:mm"), Seat = seat, Winner = winner, Reason = reason, Turns = d.Turn,
        Setup = d.Setup, Log = d.Log.ToList(), Hash = d.Hash(),
    };

    public string Title => Name.Length > 0 ? Name : $"VS {Them}";

    public string Save(string dir)
    {
        Directory.CreateDirectory(dir);
        string path = Path.Combine(dir, $"{DateTime.Now:yyyyMMdd_HHmmss}_{Safe(Title)}.json");
        Write(path);
        return path;
    }

    /// Write (or rewrite, after a rename) via a temp file, so a crash can't leave half a replay.
    public void Write(string path)
    {
        File.WriteAllText(path + ".tmp", JsonSerializer.Serialize(this, Json));
        File.Move(path + ".tmp", path, true);
    }

    public static Replay? Load(string path)
    {
        try { return JsonSerializer.Deserialize<Replay>(File.ReadAllText(path), Json) is { V: <= Version } r ? r : null; }
        catch (Exception e) when (e is IOException or JsonException or NotSupportedException) { return null; }
    }

    /// Newest first; unreadable files are skipped.
    public static List<(string Path, Replay R)> List(string dir) => !Directory.Exists(dir) ? new()
        : Directory.GetFiles(dir, "*.json").OrderByDescending(f => f).Select(f => (f, Load(f)!)).Where(x => x.Item2 != null).ToList();

    /// Re-run the whole duel off-screen: does this build still reach the same end state?
    public bool Plays(CardDb? db = null)
    {
        try { return new Duel(Setup, Log, db).Hash() == Hash; }
        catch (Exception e) when (e is ArgumentException or InvalidOperationException) { return false; }
    }

    static string Safe(string s) => new(s.Select(c => char.IsLetterOrDigit(c) ? c : '_').Take(24).ToArray());
}
