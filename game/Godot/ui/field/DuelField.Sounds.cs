using Godot;
using PoC.Engine;

namespace PoC.Game.UI;

/// The duel's sounds, as picked in the VFX Lab's sound rounds (2026-10-27). The effect code marks its moments with
/// Cue("name") — a swing, a hit, a shatter, each shake of the dice cup — and this table says what plays there, so
/// every sound lands on its animation. A cue's sound: "NAME", several joined with " + ", each optionally followed by
/// "^0.08" (pitch up 8% per repeat: the 2nd card drawn), "*0.6" (volume), ">0.31" (that many seconds after the cue)
/// and "@1.1" (pitch). {ATTR} = the acting monster's attribute. Names: SE_ = Master Duel (assets/audio/md/se),
/// C_ = cut from Master Duel files (game/tools/sfxcut.py), G_ = ours (game/tools/sfxforge.py), both in
/// assets/audio/gen; others the original PoC sounds (assets/audio/se).
public partial class DuelField
{
    const string Crack = "SE_CARDBREAK_01 + SE_CARD_BREAK>0.45";   // crack, then the break peaking on the shatter
    const string Attack = "SE_ATTACK_A_{ATTR}_DEFAULT_01";           // peaks ~0.31 s in; contact is 0.46 s after the wind-up

    static readonly Dictionary<string, string> CueSounds = new()
    {
        // Battle (lunge: wind-up 0.24 s, pause 0.08, dash 0.14 -> contact).
        ["crack"] = Crack,                                                    // 1, 13, 16: each destroyed card
        ["burn"] = "SE_CARDBREAK_02 + G_ember_tail>0.3",                      // 2
        ["zone_close"] = "SE_EV_CONVULSION_OF_NATURE_a + C_mat_rumble*0.6",   // Ground Collapse (UI Lab round 4 D): the quake peaks 0.30 s with the shards
        ["windup_block"] = Attack + " + SE_ATTACK_GUARD>0.31",                // 7: the guard peaks on contact
        ["windup_lock"] = Attack + " + SE_ATTACK_GUARD>0.31",                 // 12
        ["grind"] = "G_grind_scrape*0.5",
        ["grind_release"] = "SE_DISP_DEFENS",
        ["repel"] = "G_whoosh_repel",
        ["windup_meet"] = Attack + ">0.15",                                   // 13
        ["meet"] = "C_mat_hit_light",
        ["windup_direct"] = "SE_DIRECT_ATTACK_PLAYER>0.38",                   // 14
        ["windup_direct_rival"] = "SE_DIRECT_ATTACK_RIVAL>0.38",
        ["direct_impact"] = "C_smoke_blast*0.8",
        ["aim"] = "SE_DISP_ATTACK",                                           // 11
        ["block"] = "",
        // LP.
        ["lp_down_them"] = "SE_LP_COUNT_RIVAL",                               // 14
        ["lp_down_you"] = "SE_LP_COUNT_PLAYER + SE_DIRECT_ATTACK_RIVAL",      // 15
        ["lp_up"] = "SE_LP_RECOVERY_PLAYER + life_up",                        // 30
        ["banner_win"] = "SE_DUEL_WIN",                                       // 16
        ["banner_special_win"] = "SE_DUEL_SPECIAL_WIN",
        // Placement and summons.
        ["land"] = "SE_LAND_NORMAL_MIDDLE",                                   // 3, 4, 19
        ["swords_swing"] = "SE_DISP_ATTACK",                                  // 3: peaks as the swords cross
        ["shield"] = "SE_DISP_DEFENS",                                        // 4
        ["swirl_out"] = "SE_SMN_FUSION_01_01*0.8",                            // 8
        ["impact_fusion"] = "SE_LAND_FUSION_HIGH",
        ["soul_out"] = "C_absorb_soul_short*0.8",                             // 9
        ["impact_tribute"] = "SE_LAND_ADVANCE_HIGH",
        ["rebuild_special"] = "SE_CEMETARY_GOOUT + G_rebuild_violet*0.45",    // 17
        ["land_special"] = "SE_LAND_NORMAL_HIGH",
        ["ritual_circle"] = "SE_SMN_RITUAL_02_01 + G_ritual_drone*0.5",       // 18
        ["ritual_pillars"] = "G_pillar_zing*0.7",
        ["impact_ritual"] = "SE_LAND_RITUAL_HIGH",
        ["flip"] = "G_card_flip_snap>0.25",                                   // 19
        // Chains and effects.
        ["chain_link"] = "SE_DUELCHAIN_01",                                   // 5
        ["chain_resolve"] = "SE_DUELCHAIN_RESOLVE01",
        ["chain_end"] = "SE_CHAIN_END",
        ["negate"] = "G_glitch_static",                                       // 6
        ["rays"] = "SE_DUEL_ACTIVE_POSSIBLE",                                 // 10
        ["rays_spell"] = "SE_DUEL_ACTIVE_POSSIBLE",                           // 21
        ["rays_trap"] = "SE_DUEL_ACTIVE_POSSIBLE",
        ["flip_st"] = "SE_CARD_MOVE_03",
        ["cutin_in"] = "SE_DUEL_ACTIVE_POSSIBLE",                             // 35 (the same moment as the rays: plays once)
        // Card moves.
        ["card_slide"] = "SE_CARD_MOVE_04",                                   // 20
        ["card_place"] = "SE_CF_CARDIN_A + SE_LAND_MT_SET*0.7",
        ["draw"] = "SE_CARD_DRAW_01^0.08",                                    // 22: a step up per card in a row
        ["to_hand"] = "C_whoosh_gate*0.6",                                    // 24 (23: the Graveyard carries how the card went)
        ["discard_burn"] = "SE_CARDBREAK_02*0.8 + G_ember_tail>0.3",          // 25
        ["rebuild_gy"] = "SE_CEMETARY_ABSORB",
        ["stat_up"] = "G_stat_up_soft",                                       // 26
        ["stat_down"] = "G_stat_down_soft",
        ["thread"] = "G_thread_glide",                                        // 27
        ["thread_flare"] = "G_equip_clasp",
        ["turn"] = "SE_CARD_MOVE_03",                                         // 28
        ["control_out"] = "C_change*0.8",                                     // 29
        ["control_in"] = "SE_LAND_NORMAL_MIDDLE",
        // Turn and phase.
        ["phase_battle"] = "SE_PHASE_BATTLE*0.7 + C_mat_rumble*0.7",          // 31
        ["turn_wave"] = "SE_NEXT_TURN_PLAYER",                                // 32: peaks as the banner lands
        ["turn_wave_rival"] = "SE_NEXT_TURN_RIVAL",
        // Coin and die.
        ["coin_flip"] = "C_coin_toss_ticks",                                  // 33: flick, a tick each turn
        ["coin_land"] = "SE_COIN_DECIDE_02",
        ["coin_good"] = "SE_DR_REWARD",
        ["coin_bad"] = "SE_EV_TRANSMISSION_GEAR_LOSE",
        ["cup_shake"] = "C_dice_rattle_roll",                                 // 34: a click on each of the cup's turns
        ["cup_slam"] = "SE_LAND_MT_SET + SE_CARD_MOVE_04",
        ["cup_lift"] = "SE_CARD_MOVE_01",
        ["die_result"] = "SE_DR_DICEGET",
    };

    /// A named moment in an effect: plays its sound. idx counts repeats (the 2nd card drawn); who supplies {ATTR}.
    void Cue(string name, int idx = 0, Card? who = null)
    {
        if (CueSounds.TryGetValue(name, out var spec) && spec != "") PlayCue(spec, idx, who);
    }

    readonly Dictionary<string, ulong> cuePlayed = new();

    void PlayCue(string spec, int idx, Card? who)
    {
        foreach (var part in spec.Split(" + "))
        {
            var s = part.Trim();
            float step = 0, vol = 1, k = 1; double delay = 0;
            static float F(string x) => float.Parse(x, System.Globalization.CultureInfo.InvariantCulture);
            int c = s.IndexOf('^'); if (c > 0) { step = F(s[(c + 1)..]); s = s[..c]; }
            c = s.IndexOf('*'); if (c > 0) { vol = F(s[(c + 1)..]); s = s[..c]; }
            c = s.IndexOf('>'); if (c > 0) { delay = F(s[(c + 1)..]); s = s[..c]; }
            c = s.IndexOf('@'); if (c > 0) { k = F(s[(c + 1)..]); s = s[..c]; }
            if (s.Contains("{ATTR}")) s = s.Replace("{ATTR}", Attr(who));
            // A touch of pitch variation on our own sounds so repeats (shards, ticks) never sound copied.
            float pitch = k * (1 + step * idx) * (s.StartsWith("G_") ? (float)GD.RandRange(0.98, 1.02) : 1f);
            var path = SfxPath(s);
            void Go()
            {
                // The same sound twice within 80 ms (two fusion materials, rays + cut-in) plays once: doubled it is harsh.
                ulong now = Time.GetTicksMsec();
                if (cuePlayed.TryGetValue(path, out var t) && now - t < 80) return;
                cuePlayed[path] = now;
                PlayAudio(path, pitch, vol);
            }
            if (delay > 0) After(delay, Go); else Go();
        }
    }

    /// The attribute as Master Duel names its attack sounds (DIVINE and unknown fall back to LIGHT / EARTH).
    static string Attr(Card? c) => c?.Data.Attribute.ToUpperInvariant() switch
    {
        "DARK" => "DARK", "LIGHT" or "DIVINE" => "LIGHT", "FIRE" => "FIRE", "WATER" => "WATER", "WIND" => "WIND", _ => "EARTH",
    };
}
