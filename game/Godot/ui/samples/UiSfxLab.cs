using Godot;

namespace PoC.Game.UI;

/// Sound samples for the Chaos Rift menus (2026-11-08). Each row is a UI event; click an option to hear it, click ✓ to
/// pick it. Picks go to game/ui_sfx_picks.txt. The transition rows have SEE: the real screen change with that sound.
/// A spec is "file" or "file + file>0.12" (second sound after 0.12 s), files under assets/audio.
public partial class UiSfxLab : RiftMenu
{
    static readonly (string Event, string[] Options)[] Rows =
    {
        ("SCREEN CHANGE (RIFT TEAR)", new[] { "gen/C_whoosh_gate", "md/se/SE_MENU_SLIDE_02 + gen/G_glitch_static>0.05", "md/se/SE_EXCLUSION_GOOUT", "md/se/SE_CF_BOOKOUT + gen/C_whoosh_gate>0.08", "md/se/SE_PHASE_WINDOW_OPEN" }),
        ("LOADING INTRO: THE RIFT TEARS", new[] { "md/se/SE_CARD_FINISH_BREAK_01", "md/se/SE_CARD_FINISH_BREAK_02", "md/se/SE_DUEL_LIVE_THUNDER", "md/se/SE_CARD_FINISH_BREAK_03 + gen/G_glitch_static>0.0" }),
        ("LOADING INTRO: LOGO MOVES", new[] { "gen/C_whoosh_gate", "gen/G_thread_glide", "md/se/SE_MENU_SLIDE_04", "gen/G_whoosh_repel" }),
        ("HOVER A MENU ITEM", new[] { "md/se/SE_MENU_SELECT_01", "md/se/SE_MENU_SELECT_02", "md/se/SE_DUEL_SELECT", "md/se/SE_CARDVIEW_01" }),
        ("CHOOSE / CONFIRM", new[] { "md/se/SE_MENU_DECIDE", "md/se/SE_MENU_S_DECIDE_01", "md/se/SE_DUEL_DECIDE", "md/se/SE_CARDVIEW_APPLY_01" }),
        ("BACK", new[] { "md/se/SE_MENU_CANCEL", "md/se/SE_DUEL_CANCEL", "md/se/SE_PHASE_WINDOW_CLOSE", "md/se/SE_STRUCTURE_CARD_CLOSE" }),
        ("SWITCH TAB", new[] { "md/se/SE_MENU_SLIDE_01", "md/se/SE_MENU_SLIDE_03", "md/se/SE_CARDVIEW_02", "md/se/SE_MENU_OVERLAP" }),
        ("TURN A PAGE", new[] { "md/se/SE_CF_CARDIN_A", "md/se/SE_CF_CARDIN_B", "gen/G_card_flip_snap", "md/se/SE_MENU_SLIDE_02" }),
        ("ADD A CARD TO THE DECK", new[] { "md/se/SE_LAND_MT_SET", "md/se/SE_SMN_CMN_CARD_01", "gen/G_card_flip_snap", "md/se/SE_CF_CARDIN_A" }),
        ("TAKE A CARD OUT", new[] { "md/se/SE_CEMETERY_CARD", "md/se/SE_CF_BOOKOUT", "md/se/SE_EXCLUSION_GOOUT", "md/se/SE_SMN_CMN_CARD_02" }),
        ("BUY", new[] { "md/se/SE_ITEMCOIN_DECIDE", "md/se/SE_SYS_VERIFY_KIRAKIRA_01", "md/se/SE_STRUCTURE_CARD_GET", "md/se/SE_ITEMCOIN_THROW + md/se/SE_SYS_VERIFY_KIRAKIRA_01>0.25" }),
        ("SAVE / EQUIP", new[] { "md/se/SE_SYS_VERIFY", "md/se/SE_SYS_VERIFY_02", "md/se/SE_SYS_VERIFY_03", "md/se/SE_CARDVIEW_APPLY_02" }),
        ("NOT ALLOWED", new[] { "md/se/SE_EFFECT_INVALID", "md/se/SE_ATTACK_GUARD", "md/se/SE_MENU_CANCEL", "gen/G_grind_scrape" }),
        ("TOURNAMENT: YOU MOVE UP", new[] { "md/se/SE_LEVEL_UP", "md/se/SE_SYS_VERIFY_KIRAKIRA_01", "md/se/SE_NEXT_TURN_PLAYER", "gen/G_pillar_zing" }),
    };

    static readonly Dictionary<int, int> picks = new();
    AudioStreamPlayer a = null!, b = null!;
    readonly List<List<Label>> marks = new();

    protected override string? MusicPath => null;
    protected override void OnBack() => GetTree().Quit();

    protected override void Build()
    {
        Rift(0.5f, 0.5f);
        // the lab must be heard even when Options has Sound at 0
        int sb = AudioServer.GetBusIndex("Sound");
        if (sb >= 0) { AudioServer.SetBusMute(sb, false); AudioServer.SetBusVolumeDb(sb, 0); }
        a = new AudioStreamPlayer { Bus = "Sound" }; b = new AudioStreamPlayer { Bus = "Sound" };
        AddChild(a); AddChild(b);
        Put(this, "SOUND SAMPLES", Title, 34, Ink, new Vector2(70, 30));
        Put(this, "CLICK A LETTER TO HEAR IT  ·  ✓ TO PICK", Thin, 15, Graphite, new Vector2(72, 78));
        float y = 130, rowH = (H - 170) / Rows.Length;
        for (int r = 0; r < Rows.Length; r++)
        {
            int ri = r;
            var (ev, opts) = Rows[r];
            Put(this, ev, Title, 17, Ink, new Vector2(70, y + 4), new Vector2(CrackX(y) - 120, 26), HorizontalAlignment.Right);
            float x = CrackX(y) + 40;
            var row = new List<Label>();
            for (int o = 0; o < opts.Length; o++)
            {
                int oi = o;
                var l = Put(this, ((char)('A' + o)).ToString(), Title, 24, Silver, new Vector2(x, y));
                l.TooltipText = opts[o];
                Click(l, () => Play(opts[oi]), h => l.AddThemeColorOverride("font_color", h ? GoldLight : picks.GetValueOrDefault(ri, -1) == oi ? GoldLight : Silver));
                var ok = Put(this, "✓", Body, 30, Mist * new Color(1, 1, 1, 0.5f), new Vector2(x + 30, y - 4));
                Click(ok, () => { picks[ri] = oi; Refresh(); SavePicks(); Play(opts[oi]); });
                row.Add(ok);
                x += 100;
            }
            if (r == 0)
            {
                var see = Put(this, "SEE", Title, 18, GoldLight, new Vector2(x + 20, y + 4));
                Click(see, () => { Transition.Sound = opts[picks.GetValueOrDefault(0, 0)]; Transition.Go(this, "res://ui/samples/UiSfxLab.tscn"); });
            }
            marks.Add(row);
            y += rowH;
        }
        foreach (var kv in LoadPicks()) picks[kv.Key] = kv.Value;
        Refresh();
    }

    void Refresh()
    {
        for (int r = 0; r < marks.Count; r++)
            for (int o = 0; o < marks[r].Count; o++)
                marks[r][o].AddThemeColorOverride("font_color", picks.GetValueOrDefault(r, -1) == o ? GoldLight : Mist * new Color(1, 1, 1, 0.5f));
    }

    /// Play a spec: "x" or "x + y>delay".
    public static void PlaySpec(Node owner, AudioStreamPlayer a, AudioStreamPlayer b, string spec)
    {
        var parts = spec.Split('+', StringSplitOptions.TrimEntries);
        a.Stream = GD.Load<AudioStream>($"res://assets/audio/{parts[0]}.wav"); a.Play();
        if (parts.Length < 2) return;
        var (file, delay) = parts[1].Contains('>') ? (parts[1].Split('>')[0], double.Parse(parts[1].Split('>')[1], System.Globalization.CultureInfo.InvariantCulture)) : (parts[1], 0.0);
        b.Stream = GD.Load<AudioStream>($"res://assets/audio/{file}.wav");
        if (delay <= 0) b.Play(); else owner.GetTree().CreateTimer(delay).Timeout += () => b.Play();
    }

    void Play(string spec) => PlaySpec(this, a, b, spec);

    static string PicksPath => ProjectSettings.GlobalizePath("res://") + "../ui_sfx_picks.txt";

    static void SavePicks()
    {
        var lines = Rows.Select((r, i) => $"{i + 1}  {r.Event}: {(picks.TryGetValue(i, out var o) ? $"{(char)('A' + o)}  ({r.Options[o]})" : "(none)")}");
        System.IO.File.WriteAllLines(PicksPath, lines);
    }

    static Dictionary<int, int> LoadPicks()
    {
        var d = new Dictionary<int, int>();
        if (!System.IO.File.Exists(PicksPath)) return d;
        foreach (var line in System.IO.File.ReadAllLines(PicksPath))
        {
            var p = line.Split(':');
            if (p.Length < 2 || !int.TryParse(p[0].Split(' ')[0], out var n)) continue;
            var t = p[1].Trim();
            if (t.Length > 0 && t[0] is >= 'A' and <= 'E') d[n - 1] = t[0] - 'A';
        }
        return d;
    }
}
