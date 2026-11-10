using System.Text.RegularExpressions;
using Godot;
using PoC.Engine;

namespace PoC.Game.UI;

/// Card pictures: HD scans (`res://cards/hd/<idx>.jpg`, 813x1185 from YGOPRODeck) everywhere; Small is a 400px copy
/// (`res://cards/small/`) for the field, hand and grids. The 200x290 game BMPs (remake/, dev only) when no scan exists.
// ponytail: the scans are kept as plain files (importer "keep": no 1,100-texture import step) and ship inside the
// game's pack, so builds read them the same way the editor does. Not in git (game/Godot/cards/ is ignored).
public static class CardArt
{
    const string Cards = "res://cards";
    static readonly string Remake = ProjectSettings.GlobalizePath("res://").TrimEnd('/') + "/../../remake";   // dev machines only
    static bool Exists(string p) => p.StartsWith("res://") ? Godot.FileAccess.FileExists(p) : File.Exists(p);
    static Dictionary<int, string>? files;
    static readonly Dictionary<string, Texture2D?> cache = new();
    static Texture2D? back;

    public static Texture2D Back => back ??= GD.Load<Texture2D>("res://assets/title/card_back.bmp");

    static string Hd(CardData d) => $"{Cards}/hd/{d.Idx}.jpg";

    public static Texture2D Small(CardData d) => SmallPath(d) is { } p ? Load(p) ?? Back : Back;

    static string? SmallPath(CardData d)
    {
        if (Exists(Hd(d))) return SmallOf(d);
        if (files == null) Index();
        return files!.TryGetValue(d.Idx, out var f) ? f : null;
    }

    // Grids (Card List, Deck Builder) ask for hundreds of pictures while scrolling: the files are decoded on worker
    // threads and turned into textures on the main thread a few milliseconds' worth per frame, so scrolling stays smooth.
    static readonly System.Collections.Concurrent.ConcurrentQueue<(string path, Image? img, Action<Texture2D> done)> decoded = new();
    static bool pumping;

    public static void SmallAsync(CardData d, Action<Texture2D> done)
    {
        if (files == null) Index();   // built once, here on the main thread, before workers read it
        var known = Exists(Hd(d)) ? SmallOf(d) : files!.GetValueOrDefault(d.Idx);
        if (known != null && cache.TryGetValue(known, out var hit)) { done(hit ?? Back); return; }
        if (!pumping) { pumping = true; ((SceneTree)Godot.Engine.GetMainLoop()).ProcessFrame += Pump; }
        Task.Run(() =>
        {
            Image? img = null;
            try
            {
                var p = SmallPath(d);
                if (p != null) { img = Image.LoadFromFile(p); img.GenerateMipmaps(); }
                decoded.Enqueue((p ?? "", img, done));
            }
            catch (Exception) { decoded.Enqueue(("", null, done)); }
        });
    }

    static void Pump()
    {
        ulong start = Time.GetTicksUsec();
        while (Time.GetTicksUsec() - start < 4000 && decoded.TryDequeue(out var job))
        {
            if (job.img == null) { job.done(Back); continue; }
            if (!cache.TryGetValue(job.path, out var t) || t == null) cache[job.path] = t = ImageTexture.CreateFromImage(job.img);
            job.done(t);
        }
    }

    public static Texture2D Large(CardData d) => Load(Hd(d)) ?? Small(d);

    /// The 400px copy (decoding 1000 full scans for a grid would be slow and heavy); the full scan if there is none.
    static string SmallOf(CardData d) => Exists($"{Cards}/small/{d.Idx}.jpg") ? $"{Cards}/small/{d.Idx}.jpg" : Hd(d);

    static readonly Dictionary<int, Texture2D> pictures = new();

    /// The picture window of the card (square crop), for deck covers and profile pictures. A real texture
    /// (not an atlas) so shaders that round it see 0..1 UVs.
    public static Texture2D Picture(CardData d)
    {
        if (pictures.TryGetValue(d.Idx, out var cached)) return cached;
        var t = Large(d);
        float w = t.GetWidth(), h = t.GetHeight();
        // HD scans carry a wider border than the 200x290 game pictures.
        var r = w > 400 ? new Rect2(w * 0.08f, h * 0.155f, w * 0.845f, w * 0.845f) : new Rect2(w * 0.13f, h * 0.2f, w * 0.74f, w * 0.74f);
        var img = t.GetImage().GetRegion(new Rect2I((Vector2I)r.Position, (Vector2I)r.Size));
        img.GenerateMipmaps();
        return pictures[d.Idx] = ImageTexture.CreateFromImage(img);
    }

    static Texture2D? Load(string path)
    {
        if (cache.TryGetValue(path, out var t)) return t;
        if (!Exists(path)) return cache[path] = null;
        var img = Image.LoadFromFile(path);
        img.GenerateMipmaps();
        return cache[path] = ImageTexture.CreateFromImage(img);
    }

    static void Index()
    {
        files = new();
        var head = new Regex(@"^//\s*(\d{4}):\[");
        foreach (var game in new[] { "yugi", "kaiba", "joey" })
        {
            var dir = $"{Remake}/extracted/{game}/card";
            if (!Directory.Exists(dir)) continue;
            var have = Directory.GetFiles(dir).Select(Path.GetFileName).ToHashSet(StringComparer.OrdinalIgnoreCase);
            var lines = File.ReadAllLines($"{dir}/list_card.txt");
            for (int i = 0; i + 1 < lines.Length; i++)
            {
                var m = head.Match(lines[i]);
                if (!m.Success) continue;
                int idx = int.Parse(m.Groups[1].Value);
                var f = lines[i + 1].Trim();
                if (!files.ContainsKey(idx) && have.Contains(f) && !f.Equals("card_ura.bmp", StringComparison.OrdinalIgnoreCase)) files[idx] = $"{dir}/{f}";
            }
        }
    }
}
