using Godot;
using PoC.Engine;

namespace PoC.Game.UI;

/// Player options and profile (name + picture). Kept in the save file (Session.Save).
public static class Settings
{
    public static readonly (string Name, double Pace)[] Speeds = { ("NORMAL", 1.4), ("FAST", 1.1), ("FASTER", 0.85) };

    public static float Music = 0.8f, Sound = 0.9f;   // 0..1
    public static int Speed;                         // index into Speeds
    public static bool Fullscreen = true;
    public static string PlayerName = "Player";
    public const int NameMax = 14;

    /// Profile pictures: the picture window of iconic cards.
    public static readonly string[] Pictures =
    {
        "Dark Magician", "Blue-Eyes White Dragon", "Red-Eyes B. Dragon", "Kuriboh", "Dark Magician Girl", "Summoned Skull",
        "Time Wizard", "Baby Dragon", "Celtic Guardian", "Gaia The Fierce Knight", "Harpie Lady", "Jinzo",
        "Exodia the Forbidden One", "Buster Blader", "Flame Swordsman", "Curse of Dragon", "Gearfried the Iron Knight", "Gemini Elf",
        "Axe Raider", "Mystical Elf", "Witty Phantom", "La Jinn the Mystical Genie of the Lamp", "Thousand Dragon", "Alligator's Sword",
    };
    public static int Picture = 3;                    // index into Pictures (Kuriboh)

    public static CardData? PictureCard(int i) =>
        CardDb.Shared.ByName.TryGetValue(Pictures[i], out var l) ? l.OrderByDescending(c => c.PackMask != 0).First() : null; // the collectable printing

    /// The available pictures (names missing from the card data are skipped).
    public static IEnumerable<int> PictureChoices => Enumerable.Range(0, Pictures.Length).Where(i => PictureCard(i) != null);

    public static Texture2D? Avatar => PictureCard(Picture) is { } c ? CardArt.Picture(c) : null;

    /// Ban list: index into DeckRules.Formats.
    public static int Format
    {
        get => Array.FindIndex(DeckRules.Formats, f => f.Key == DeckRules.LimitGame);
        set => DeckRules.LimitGame = DeckRules.Formats[value].Key;
    }

    public static double Pace => Speeds[Speed].Pace;

    /// The options as the save file holds them.
    public static SaveData.Options Saved
    {
        get => new() { Name = PlayerName, Picture = Picture, Music = Music, Sound = Sound, Speed = Speed, Fullscreen = Fullscreen, BanList = DeckRules.LimitGame };
        set
        {
            var n = value.Name.Trim();
            PlayerName = n.Length > 0 ? n[..Math.Min(n.Length, NameMax)] : "Player";
            Picture = PictureChoices.Contains(value.Picture) ? value.Picture : Picture;
            Music = Math.Clamp(value.Music, 0, 1); Sound = Math.Clamp(value.Sound, 0, 1);
            Speed = Math.Clamp(value.Speed, 0, Speeds.Length - 1);
            Fullscreen = value.Fullscreen;
            DeckRules.LimitGame = value.BanList;
        }
    }

    static bool ready;

    /// Audio buses "Music" and "Sound" (created once), their volumes, and the window mode.
    public static void Apply(bool window = true)
    {
        if (!ready)
        {
            ready = true;
            foreach (var bus in new[] { "Music", "Sound" })
            {
                AudioServer.AddBus();
                int i = AudioServer.BusCount - 1;
                AudioServer.SetBusName(i, bus);
                AudioServer.SetBusSend(i, "Master");
            }
        }
        Volume("Music", Music);
        Volume("Sound", Sound);
        if (window)
        {
            var want = Fullscreen ? DisplayServer.WindowMode.Fullscreen : DisplayServer.WindowMode.Windowed;
            if (DisplayServer.WindowGetMode() != want) DisplayServer.WindowSetMode(want);
        }
    }

    static void Volume(string bus, float v)
    {
        int i = AudioServer.GetBusIndex(bus);
        AudioServer.SetBusMute(i, v <= 0.001f);
        AudioServer.SetBusVolumeDb(i, Mathf.LinearToDb(v));
    }
}
