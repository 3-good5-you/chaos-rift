using Godot;
using PoC.Engine;

namespace PoC.Game.UI;

/// FX Lab "signature cards", round 4 (2026-11-13): all 27 cards in one batch (user: "make all of them into one
/// batch"). The eight from batch 1 come back as the picked idea, refined on the new kit (DuelField.FxKit3.cs: cards
/// bend and burn as one sheet, staged twisters, boiling explosions, real lightning hits, no reused defeat crack);
/// Trap Hole gets three new ideas. The other 19 cards are in DuelField.FxLab.Batch2.cs.
public partial class DuelField
{
    void CardFxLabEvents(Action<string, string, (string, Func<Task>)[]> add)
    {
        const string G = "SIGNATURE CARDS · ROUND 8 · PICK ONE PER ROW";
        void Row(int n, string name, Func<int, Task> run, params string[] opts) => add(G, $"{n}  {name}", opts.Select((o, i) => (o, (Func<Task>)(() => run(i + 1)))).ToArray());
        // round 4 rows (this file and Batch2) are locked or replaced; round 5 lists only what is still open (Batch3)
        Batch6Rows((n, name, run, opts) => Row(n, name, run, opts));
        if (OS.GetCmdlineUserArgs().Contains("--labfixes")) Batch4Rows((n, name, run, opts) => { if (n >= 900) Row(n, name, run, opts); });
    }

    // ---------------------------------------------------------------- staging

    Vector3 ZonePos(Loc loc, int zone, int side) => Grid3(new St(loc, zone, 0, true, false, side, 0, 0));

    /// Your card played face-up in your middle Spell & Trap zone, with its glow. `sound` = the card's own sound.
    async Task<View> PlayCard(string name, Color tint, bool faceDown = false, string? sound = null)
    {
        var card = Of(0, name); Place(card, SpellZ(2, 0, !faceDown));
        await Wait(0.5);
        if (!faceDown) { GoldRays(card, tint); if (sound != null) Snd(sound); else Cue("flip_st"); await Wait(0.45); }
        return card;
    }

    async Task Flip(View trap, Color tint, string? sound = null)
    {
        Move(trap, trap.S with { Up = true }, 0, 0.35f, 0.3f);
        await Wait(0.4); GoldRays(trap, tint); if (sound != null) Snd(sound); else Cue("flip_st"); await Wait(0.4);
    }

    List<View> TheirMonsters() => RivalBoard().Where(v => v.S.Loc == Loc.Monster).ToList();

    /// A sword of light standing point-down on `tip`. `h` = its height. Lives `life` seconds.
    MeshInstance3D Sword(Vector3 tip, float h, double life, Color? blade = null, float lean = 0)
    {
        var basis = Bill * new Basis(Vector3.Back, lean);
        var q = FxQuad(SwordFx, tip + basis.Y * h / 2, new Vector2(h * 0.34f, h), life, blade ?? new Color(1f, 0.95f, 0.78f), basis, true, null, ("col2", new Color(1f, 0.72f, 0.22f)));
        // its light, behind it; it goes where the sword goes
        var glowM = FxMat(PillarFx, new Color(1f, 0.85f, 0.5f), false, null, ("amt", 0.14f), ("t", 0.5f));
        q.AddChild(new MeshInstance3D { Mesh = new QuadMesh { Size = new Vector2(h * 0.5f, h * 1.05f) }, Position = new Vector3(0, 0, -0.02f), MaterialOverride = glowM, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off });
        return q;
    }

    /// A sword falls onto `tip`, lands with a soft ring of light and a spit of sparks (no crack), stays `stay` s and
    /// lifts away as motes.
    void SwordDrop(Vector3 tip, float h, double delay, double stay, float lean = 0, float power = 1f)
    {
        After(delay, () =>
        {
            var sw = Sword(tip, h, 0.2 + stay + 0.45, null, lean);
            var land = sw.Transform; sw.Transform = land.Translated(Vector3.Up * 5f);
            var gold = new Color(1f, 0.86f, 0.55f);
            var tw = sw.CreateTween();
            tw.TweenProperty(sw, "transform", land, 0.16).SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.In);
            tw.TweenCallback(Callable.From(() =>
            {
                Snd("C_DART");   // the blade biting into the table
                FxQuad(RingFx, tip + Vector3.Up * 0.03f, new Vector2(1.3f, 1.3f) * power, 0.4, gold);
                FxQuad(GroundGlowFx, tip + Vector3.Up * 0.02f, new Vector2(0.9f, 0.9f) * power, 0.5, gold);
                Warp(tip + Vector3.Up * 0.04f, 1.6f * power, 0.35, 0.8f * power);
                Streaks(tip + Vector3.Up * 0.05f, new Color(1f, 0.92f, 0.7f), (int)(8 * power), 2.2f);
                Shake(0.025f * power);
            }));
            tw.TweenInterval(Math.Max(0.05, stay));
            tw.TweenCallback(Callable.From(() => { for (int i = 0; i < 10; i++) Ember(tip + Vector3.Up * R(0.1, h), new Color(1f, 0.9f, 0.65f), new Vector3(R(-0.2, 0.2), R(0.6, 1.2), R(-0.2, 0.2)), R(0.6, 1.0), 0.035f, 0.4f); }));
            tw.TweenProperty(sw, "transform", land.Translated(Vector3.Up * 0.9f).ScaledLocal(new Vector3(0.2f, 1.25f, 1)), 0.35).SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.In);
        });
    }

    // ---------------------------------------------------------------- 70 Swords of Revealing Light

    async Task SwordsFx(int style)
    {
        var foes = TheirMonsters();
        var hid = Of(1, "Battle Ox"); Place(hid, Mon(hid, 0, 1, up: false, def: true));
        await PlayCard("Swords of Revealing Light", FxGreen, sound: "SE_EV_GOFUKEN");
        float rowZ = ZonePos(Loc.Monster, 2, 1).Z;
        var gold = new Color(1f, 0.88f, 0.6f);
        // seven swords on an arc round their monster row, landing one after another
        var pts = new List<Vector3>();
        for (int i = 0; i < 7; i++)
        {
            float a = Mathf.Pi * (0.06f + i * 0.88f / 6f);
            pts.Add(new Vector3(Mathf.Cos(a) * 2.95f, 0, rowZ - 0.1f + Mathf.Sin(a) * 1.05f));
        }
        for (int i = 0; i < 7; i++) SwordDrop(pts[i], 1.45f, i * 0.09, 1.9 - i * 0.09, pts[i].X * -0.04f, 0.65f);
        await Wait(0.75);
        // the chain of light runs from sword to sword
        for (int i = 0; i + 1 < pts.Count; i++)
        {
            int k = i;
            After(i * 0.06, () =>
            {
                var a = pts[k] + Vector3.Up * 0.35f; var b = pts[k + 1] + Vector3.Up * 0.35f;
                var l = LightLine(a, b, gold, 0.1); After(1.25 - k * 0.06, () => { if (IsInstanceValid(l)) l.QueueFree(); });
                Ember(b, gold, Vector3.Up * 0.3f, 0.6, 0.06f, 0.2f);
            });
        }
        Snd("SE_TDY_CHAINGET_CHAIN_01", 0.1);
        await Wait(0.42);
        // bars of light rise between the swords: the cage
        for (int i = 0; i + 1 < pts.Count; i++)
            for (int b = 1; b <= 2; b++)
            {
                var at = pts[i].Lerp(pts[i + 1], b / 3f);
                double d = (i * 2 + b) * 0.025;
                After(d, () => FxStand(PillarFx, at, new Vector2(0.14f, 1.0f), 0.95, gold, false, null, ("amt", 0.55f)));
            }
        await Wait(0.3);
        Move(hid, hid.S with { Up = true }, 0, 0.4f, 0.3f);   // their face-down monsters are revealed
        foreach (var v in foes.Append(hid)) FxQuad(GroundGlowFx, v.Root.GlobalPosition with { Y = 0.02f }, new Vector2(1.1f, 1.1f), 0.6, gold);
        await Wait(2.0);
    }

    // ---------------------------------------------------------------- 71 Mystical Space Typhoon

    async Task MstFx(int style)
    {
        var target = RivalBoard().First(v => v.S.Loc == Loc.Spell);
        await PlayCard("Mystical Space Typhoon", FxGreen, sound: "SE_EV_CYCLONE");
        var at = target.Root.GlobalPosition with { Y = 0 }; var wind = new Color(0.75f, 0.92f, 1f); var dust = new Color(0.62f, 0.72f, 0.8f, 0.55f);
        var tw = Twister(at, 1.9f, 0.5f, wind, dust);
        TwistLife(tw, 0.32, 0.75, 0.45, dust);
        var s = CardSheet(target);
        Anim(s.M, "flutter", 0f, 1f, 0.3);
        var from = s.Mi.Transform;
        s.Mi.CreateTween().TweenMethod(Callable.From<float>(k =>
        {
            float e = k * k;
            s.Mi.Transform = new Transform3D(new Basis(Vector3.Up, e * 16f) * new Basis(Vector3.Right, -0.9f * Mathf.Min(1, k * 1.6f)) * from.Basis, from.Origin + Vector3.Up * (0.15f + 0.95f * e)).ScaledLocal(Vector3.One * (1 - 0.35f * e));
        }), 0f, 1f, 0.75).SetDelay(0.18);
        await Wait(0.65);
        // it comes apart in the funnel: flakes of its picture carried round and up
        Flakes(s, 46, uv => { var p = s.World(uv) - at; var flat = p with { Y = 0 }; var tan = Vector3.Up.Cross(flat).Normalized(); return tan * R(1.2, 2.0) + Vector3.Up * R(1.4, 2.4) + flat.Normalized() * 0.3f; }, 0.85, 0.25, wind);
        BurnAway(s, new Color(0.85f, 0.95f, 1f), 0.3, null, 0, 0.3f, 2f, 1.2f);
        Snd("SE_CARD_ST_BREAK");
        await Wait(1.6);
    }

    // ---------------------------------------------------------------- 72 Dark Hole

    async Task DarkHoleFx(int style)
    {
        var all = TheirMonsters();
        foreach (var (name, zone) in new[] { ("Dark Magician", 1), ("Celtic Guardian", 3) }) { var v = Of(0, name); Place(v, Mon(v, zone, 0)); all.Add(v); }
        await PlayCard("Dark Hole", FxGreen, sound: "SE_EV_BLACKHOLE");
        var deep = new Color(0.5f, 0.28f, 1f); var hot = new Color(1f, 0.8f, 0.95f);
        var c = new Vector3(0, 1.35f, 0);
        FieldLines(0f, 0.5);
        ScreenDim(0.2f, 0.5, 1.6, 0.6);
        Warp(c, 6.5f, 2.5, 1.2f, lens: true, Bill);
        FxQuad(VortexFx, c, new Vector2(3.0f, 3.0f), 2.5, deep * new Color(0.55f, 0.55f, 0.55f), Bill * new Basis(Vector3.Right, 1.15f), false, null, ("col2", hot), ("amt", 3.5f));
        DarkBall(c, 0.42f, hot, 2.5);
        for (int i = 0; i < 30; i++) { float ang = i * 0.9f, rad = 2.6f + (i % 4) * 0.5f; var a = new Vector3(Mathf.Cos(ang) * rad, 0.1f, Mathf.Sin(ang) * rad * 0.7f); var col = i % 3 == 0 ? Colors.White : hot; After(0.15 + i * 0.045, () => SwirlMote(a, c, col, 0.9, 0.9f, 1.3f, 0.06f)); }
        // each monster stretched and wound into it, the edge nearest the star first
        var sheets = all.Select(CardSheet).ToList();
        for (int i = 0; i < sheets.Count; i++)
        {
            var s = sheets[i];
            s.Set("pull_to", s.Local(c)); s.Set("twist", 2.6f); s.Set("lag", 0.85f); s.Set("glow_col", deep);
            double d = 0.45 + i * 0.09;
            Anim(s.M, "pull", 0f, 1f, 1.25, d, Tween.TransitionType.Quad, Tween.EaseType.In);
            Anim(s.M, "glow", 0f, 0.3f, 0.9, d + 0.45);
            Anim(s.M, "darken", 0f, 0.55f, 1.0, d + 0.2);
            After(d + 1.27, () => { if (IsInstanceValid(s.Mi)) s.Mi.QueueFree(); });
        }
        await Wait(2.2);
        Warp(c, 6f, 0.45, 1.4f, false, Bill); Flash(c, hot, 6f); FxQuad(CoreGlowFx, c, new Vector2(2.6f, 2.6f), 0.4, hot, Bill); ScreenFlash(hot, 0.3f, 0.3);
        Shake(0.12f); HitStop(0.07);
        Streaks(c, hot, 22, 4.5f, 0.5f, Vector3.Zero, 1.5f);
        FieldLines(1f, 0.8);
        await Wait(1.6);
    }

    // ---------------------------------------------------------------- 80 Raigeki

    async Task RaigekiFx(int style)
    {
        var foes = TheirMonsters().OrderBy(v => v.Root.GlobalPosition.X).ToList();
        await PlayCard("Raigeki", FxGreen, sound: "SE_EV_RAIGEKI");
        var volt = new Color(0.82f, 0.9f, 1f);
        for (int i = 0; i < foes.Count; i++)
        {
            var v = foes[i]; var p = v.Root.GlobalPosition with { Y = 0 };
            double lead = LightningHit(p, volt, 1f, dim: i == 0);
            After(lead, () =>
            {
                var s = CardSheet(v);
                s.Set("glow_col", Colors.White); Anim(s.M, "glow", 2.6f, 0f, 0.35);
                Anim(s.M, "darken", 0f, 0.45f, 0.3);
                BurnAway(s, new Color(0.6f, 0.82f, 1f), 0.7, new Vector2(R(0.4, 0.6), R(0.4, 0.6)), 0.12, 1.2f, 6f, 0.9f);
            });
            if (i > 0) Snd("SE_DUEL_LIVE_THUNDER", lead);
            await Wait(0.2);
        }
        await Wait(2.0);
    }

    // ---------------------------------------------------------------- 82 Change of Heart

    async Task ChangeOfHeartFx(int style)
    {
        var foes = TheirMonsters(); var t = foes[1];
        await PlayCard("Change of Heart", FxGreen, sound: "SE_EV_EFFECT_VEILER");
        var p = t.Root.GlobalPosition; var light = new Color(1f, 0.93f, 0.72f); var dark = new Color(0.62f, 0.4f, 1f);
        var goal = Mon(t, 3, 0);
        var root = p + new Vector3(0, 0.45f, -0.5f);
        LightWing(root - Bill.X * 0.06f, -1, 1.25f, light, 0.55, 1.5);
        LightWing(root + Bill.X * 0.06f, 1, 1.25f, dark, 0.55, 1.5);
        FxQuad(SoftHeartFx, p + new Vector3(0, 0.9f, -0.75f), new Vector2(0.95f, 0.95f), 2.0, new Color(1f, 0.95f, 0.85f), Bill, true, null, ("col2", new Color(0.32f, 0.12f, 0.55f)), ("amt", 2.5f));
        Snd("SE_MONSTER_CUTIN_LIGHT", 0.3);
        await Wait(1.55);
        for (int i = 0; i < 8; i++) Ember(root + new Vector3(R(-0.8, 0.8), R(-0.2, 0.6), 0), i % 2 == 0 ? light : dark, new Vector3(R(-0.3, 0.3), R(-0.4, 0.1), R(-0.2, 0.2)), R(0.8, 1.2), 0.045f, -0.3f);
        Move(t, goal, 0, 0.6f, 0.9f); await Wait(0.62);
        var land = t.Root.GlobalPosition;
        FxQuad(RingFx, land with { Y = 0.04f }, new Vector2(2.0f, 2.0f), 0.5, FxPink); FxQuad(GroundGlowFx, land with { Y = 0.02f }, new Vector2(1.3f, 1.3f), 0.6, FxPink); Cue("land");
        await Wait(1.6);
    }

    // ---------------------------------------------------------------- 84 Heavy Storm

    async Task HeavyStormFx(int style)
    {
        var rows = RivalBoard().Where(v => v.S.Loc == Loc.Spell).ToList();
        foreach (var (name, zone) in new[] { ("Trap Hole", 0), ("Mirror Force", 4) }) { var v = Of(0, name); Place(v, SpellZ(zone, 0, false)); rows.Add(v); }
        var card = await PlayCard("Heavy Storm", FxGreen, sound: "SE_EV_HEAVY_STORM");
        rows.Add(card);
        var wind = new Color(0.72f, 0.82f, 1f); var dust = new Color(0.5f, 0.56f, 0.66f, 0.6f);
        Vignette(new Color(0.02f, 0.03f, 0.07f), 3.2);
        const double form = 0.5, run = 1.5, end = 0.6;
        const float x0 = 2.75f;
        foreach (int side in new[] { 0, 1 })
        {
            float z = ZonePos(Loc.Spell, 2, side).Z, dir = side == 0 ? 1 : -1;
            var tw = Twister(new Vector3(-x0 * dir, 0, z), side == 0 ? 2.3f : 1.7f, side == 0 ? 0.7f : 0.58f, wind, dust);
            TwistLife(tw, form, run, end, dust);
            tw.Root.CreateTween().TweenProperty(tw.Root, "position:x", x0 * dir, run).SetDelay(form * 0.8).SetTrans(Tween.TransitionType.Linear);
        }
        // a light slanting rain over the whole table
        for (int k = 0; k < 10; k++) { float x = -3.2f + k * 0.7f; After(0.2 + k * 0.15, () => Streaks(new Vector3(x, 2.6f, R(-2.0, 2.0)), wind, 10, 4.5f, 5f, new Vector3(0.6f, -1f, 0), 0.3f)); }
        foreach (var v in rows)
        {
            float dir = v.S.Side == 0 ? 1 : -1; float x = v.Root.GlobalPosition.X;
            double when = form * 0.8 + (x * dir + x0) / (2 * x0) * run - 0.15;
            After(when, () =>
            {
                var s = CardSheet(v); var from = s.Mi.Transform;
                Anim(s.M, "flutter", 0f, 1.2f, 0.2);
                s.Mi.CreateTween().TweenMethod(Callable.From<float>(k =>
                {
                    float e = k * k;
                    s.Mi.Transform = new Transform3D(new Basis(Vector3.Up, e * 12f * dir) * new Basis(Vector3.Right, -1.0f * Mathf.Min(1, k * 2f)) * from.Basis, from.Origin + new Vector3(dir * 0.5f * e, 0.2f + 1.3f * e, 0)).ScaledLocal(Vector3.One * (1 - 0.3f * e));
                }), 0f, 1f, 0.5);
                After(0.4, () =>
                {
                    Flakes(s, 34, uv => new Vector3(dir * R(1.0, 1.8), R(1.2, 2.2), R(-0.4, 0.4)), 0.8, 0.2, wind);
                    BurnAway(s, new Color(0.85f, 0.92f, 1f), 0.25, null, 0, 0.2f, 1.5f, 1f);
                });
            });
        }
        await Wait(form + run + end + 1.0);
    }

    // ---------------------------------------------------------------- 88 Trap Hole

    const string PitOpenCode = @"shader_type spatial;
render_mode unshaded, blend_mix, cull_disabled, depth_draw_never, shadows_disabled;
uniform float open = 0.0; uniform vec4 col : source_color; uniform sampler2D noise : repeat_enable, filter_linear_mipmap; uniform float seed = 0.0; uniform vec2 aspect = vec2(1.0, 1.0);
void fragment() {
  vec2 p = (UV * 2.0 - 1.0) * aspect; float r = length(p); float a = atan(p.y, p.x) / 6.28318;
  float rag = (texture(noise, vec2(cos(a * 6.28318), sin(a * 6.28318)) * 0.35 + seed).r - 0.5) * 0.1;
  float edge = open * (0.82 + rag);
  float inside = smoothstep(edge + 0.015, edge - 0.025, r);
  float wall = smoothstep(edge * 0.35, edge, r);
  float rim = exp(-pow((r - edge) / 0.022, 2.0)) * smoothstep(0.0, 0.08, open) * 0.7;
  float shade = texture(noise, vec2(a * 9.0, r * 3.0 + seed)).r;
  vec3 c = mix(vec3(0.0), vec3(0.09, 0.07, 0.06) * (0.6 + 0.8 * shade), wall * wall);
  c += col.rgb * pow(wall, 6.0) * 0.6;
  ALBEDO = c * inside + col.rgb * rim * 1.6; ALPHA = clamp(inside + rim * 0.9, 0.0, 1.0);
}";

    const string ShaftCode = @"shader_type spatial;
render_mode unshaded, blend_add, cull_disabled, depth_draw_never, shadows_disabled;
uniform float open = 0.0; uniform vec4 col : source_color; uniform vec4 col2 : source_color; uniform sampler2D noise : repeat_enable, filter_linear_mipmap; uniform float seed = 0.0; uniform vec2 aspect = vec2(1.0, 1.0);
void fragment() {
  vec2 p = (UV * 2.0 - 1.0) * aspect; float r = length(p); float a = atan(p.y, p.x);
  float edge = open * 0.8;
  float inside = smoothstep(edge + 0.02, edge - 0.03, r);
  float depth = 0.12 / (r + 0.03);
  float rings = pow(0.5 + 0.5 * sin(depth * 6.0 - TIME * 9.0), 6.0);
  float streak = pow(texture(noise, vec2(a * 0.9 + seed, depth * 0.08 - TIME * 0.6)).r, 3.0);
  float far = exp(-r * r * 40.0 / max(open, 0.05));
  vec3 c = mix(col.rgb, col2.rgb, smoothstep(0.0, edge, r));
  float v = (rings * 0.55 + streak * 0.8) * smoothstep(0.0, 0.25 * edge, r) + far * 1.4;
  float rim = exp(-pow((r - edge) / 0.03, 2.0)) * smoothstep(0.0, 0.1, open);
  ALBEDO = c + vec3(1.0) * far * 0.5 + col2.rgb * rim; ALPHA = clamp(v * inside * 0.85 + rim * 1.2, 0.0, 1.0);
}";

    MeshInstance3D ZoneQuad(string code, Vector3 at, Vector2 size, params (string, Variant)[] u)
    {
        var m = Shade(code, u.Concat(new (string, Variant)[] { ("noise", FxNoise), ("seed", R(0, 9)), ("aspect", new Vector2(1f, size.Y / size.X)) }).ToArray());
        var q = new MeshInstance3D { Mesh = new QuadMesh { Size = size }, MaterialOverride = m, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off };
        world.AddChild(q); q.Transform = new Transform3D(Flat, at);
        return q;
    }

    async Task TrapHoleFx(int style)
    {
        var trap = Of(0, "Trap Hole"); Place(trap, SpellZ(2, 0, false));
        var m = Of(1, "Gemini Elf"); Place(m, HandOf(1, 0, 1, false));
        await Wait(0.5);
        Move(m, Mon(m, 2, 1), 0, 0.45f, 0.5f);
        await Wait(0.55);
        await Flip(trap, FxPink, "SE_EV_CONVULSION_OF_NATURE_a");
        var p = m.Root.GlobalPosition with { Y = 0.004f };
        var zone = new Vector2(CW * 1.45f, CH * 1.25f);
        var s = CardSheet(m); var from = s.Mi.Transform;
        if (style == 1)
        {
            var amber = new Color(1f, 0.6f, 0.3f);
            var pit = ZoneQuad(PitOpenCode, p, zone * 1.15f, ("col", amber));
            var pm = (ShaderMaterial)pit.MaterialOverride;
            Anim(pm, "open", 0f, 1f, 0.35, 0, Tween.TransitionType.Back, Tween.EaseType.Out);
            Shake(0.04f); Snd("SE_CARD_FINISH_BREAK_02", 0.05);
            // it tips over the edge and drops in, darkening as it goes down
            s.Mi.CreateTween().TweenMethod(Callable.From<float>(k =>
            {
                float f = Mathf.Max(0, k - 0.15f) / 0.85f, e = f * f;
                s.Mi.Transform = new Transform3D(new Basis(Vector3.Right, e * 0.9f) * new Basis(Vector3.Up, e * 0.6f) * from.Basis, from.Origin + Vector3.Up * 0.04f * Mathf.Sin(Mathf.Min(1, k / 0.15f) * Mathf.Pi)).ScaledLocal(Vector3.One * (1 - 0.82f * e));
                s.Set("darken", e * 0.95f);
            }), 0f, 1f, 0.75).SetDelay(0.2);
            for (int i = 0; i < 6; i++) Ember(p + new Vector3(R(-0.25, 0.25), 0.02f, R(-0.3, 0.3)), amber, new Vector3(0, -0.2f, 0), 0.6, 0.03f, -0.5f);
            await Wait(0.98);
            s.Mi.QueueFree();
            Puffs(p, 4, new Color(0.35f, 0.3f, 0.27f, 0.55f), 0.55f, 0.5f, 0.2f); Snd("SE_CARD_FINISH_BREAK_03");
            Anim(pm, "open", 1f, 0f, 0.3, 0.25, Tween.TransitionType.Cubic, Tween.EaseType.In);
            After(0.6, () => { if (IsInstanceValid(pit)) pit.QueueFree(); });
        }
        else if (style == 2)
        {
            FxQuad(CircleFx, p + Vector3.Up * 0.006f, zone * 1.35f, 1.8, FxPink);
            Snd("SE_EV_CONVULSION_OF_NATURE_b", 0.1);
            await Wait(0.35);
            var shaft = ZoneQuad(ShaftCode, p, zone * 1.1f, ("col", new Color(1f, 0.85f, 0.95f)), ("col2", new Color(0.85f, 0.35f, 0.75f)));
            var sm = (ShaderMaterial)shaft.MaterialOverride;
            Anim(sm, "open", 0f, 1f, 0.3, 0, Tween.TransitionType.Back, Tween.EaseType.Out);
            s.Set("glow_col", FxPink);
            // it falls away down the shaft: smaller, turning, lit pink, gone into the bright point
            s.Mi.CreateTween().TweenMethod(Callable.From<float>(k =>
            {
                float e = 1 - Mathf.Pow(1 - k, 2.2f);
                s.Mi.Transform = new Transform3D(new Basis(Vector3.Up, e * 2.4f) * from.Basis, from.Origin + Vector3.Up * 0.003f).ScaledLocal(Vector3.One * Mathf.Max(0.01f, 1 - e));
                s.Set("glow", e * 1.4f);
            }), 0f, 1f, 0.8).SetDelay(0.15);
            for (int i = 0; i < 10; i++) After(0.15 + i * 0.06, () => Ember(p + new Vector3(R(-0.3, 0.3), 0.01f, R(-0.35, 0.35)), FxPink, Vector3.Up * R(0.3, 0.8), 0.7, 0.03f, 0.5f));
            await Wait(1.0);
            s.Mi.QueueFree();
            FxQuad(CoreGlowFx, p + Vector3.Up * 0.05f, new Vector2(1.0f, 1.0f), 0.3, FxPink, Bill);
            Anim(sm, "open", 1f, 0f, 0.3, 0.1, Tween.TransitionType.Cubic, Tween.EaseType.In);
            After(0.45, () => { if (IsInstanceValid(shaft)) shaft.QueueFree(); });
        }
        else
        {
            var grit = new Color(0.62f, 0.52f, 0.4f);
            var pit = ZoneQuad(PitOpenCode, p, zone * 1.05f, ("col", new Color(0.35f, 0.28f, 0.22f)));
            var pm = (ShaderMaterial)pit.MaterialOverride;
            Anim(pm, "open", 0f, 0.9f, 0.12);
            Snd("SE_CARD_FINISH_BREAK_03"); Shake(0.12f); HitStop(0.04);
            // the ground drops: the card goes straight down with it
            s.Mi.CreateTween().TweenMethod(Callable.From<float>(k => { s.Mi.Transform = new Transform3D(new Basis(Vector3.Forward, k * 0.25f) * from.Basis, from.Origin).ScaledLocal(Vector3.One * (1 - 0.7f * k * k)); s.Set("darken", k * 0.9f); s.Set("crumple", k * 0.25f); }), 0f, 1f, 0.32).SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.In);
            // a burst of dust round the rim, heavy grit falling back
            for (int i = 0; i < 10; i++) { float a = i * Mathf.Tau / 10; var rim = p + new Vector3(Mathf.Cos(a) * zone.X * 0.5f, 0.02f, Mathf.Sin(a) * zone.Y * 0.5f); After(R(0, 0.06), () => Puffs(rim, 1, new Color(0.42f, 0.36f, 0.3f, 0.75f), 0.75f, 0.55f, 0.08f)); }
            Streaks(p + Vector3.Up * 0.05f, grit, 22, 2.4f, 7f, Vector3.Up, 0.7f);
            await Wait(0.34);
            s.Mi.QueueFree();
            After(0.35, () => Puffs(p, 5, new Color(0.38f, 0.33f, 0.28f, 0.5f), 0.9f, 0.25f, 0.3f));
            Anim(pm, "open", 0.9f, 0f, 1.4, 0.6);
            After(2.1, () => { if (IsInstanceValid(pit)) pit.QueueFree(); });
            await Wait(0.6);
        }
        Gone(m);
        await Wait(1.7);
    }

    // ---------------------------------------------------------------- 90 Ring of Destruction

    /// The ring: a steel band with a brass rail, eight charges with a light each. Returns its root, the lights and
    /// where each charge sits on it.
    (Node3D Ring, List<StandardMaterial3D> Lights, List<Vector3> Local) RingProp(Vector3 at)
    {
        var ring = new Node3D { Position = at, Scale = Vector3.One * 1.18f };
        world.AddChild(ring);
        var steel = new StandardMaterial3D { AlbedoColor = new Color(0.62f, 0.6f, 0.6f), Metallic = 0.55f, Roughness = 0.3f, EmissionEnabled = true, Emission = new Color(0.1f, 0.1f, 0.12f) };
        var dark = new StandardMaterial3D { AlbedoColor = new Color(0.24f, 0.3f, 0.2f), Metallic = 0.2f, Roughness = 0.6f };
        var brass = new StandardMaterial3D { AlbedoColor = new Color(0.95f, 0.72f, 0.28f), Metallic = 0.5f, Roughness = 0.3f, EmissionEnabled = true, Emission = new Color(0.22f, 0.15f, 0.04f) };
        ring.AddChild(new MeshInstance3D { Mesh = new TorusMesh { InnerRadius = 0.52f, OuterRadius = 0.62f, Rings = 64, RingSegments = 12 }, MaterialOverride = steel });
        ring.AddChild(new MeshInstance3D { Mesh = new TorusMesh { InnerRadius = 0.555f, OuterRadius = 0.585f, Rings = 64, RingSegments = 8 }, Position = new Vector3(0, 0.05f, 0), MaterialOverride = brass });
        var lights = new List<StandardMaterial3D>(); var local = new List<Vector3>();
        for (int i = 0; i < 8; i++)
        {
            float a = i * Mathf.Tau / 8; var p = new Vector3(Mathf.Cos(a) * 0.57f, 0, Mathf.Sin(a) * 0.57f);
            var charge = new Node3D { Position = p, Rotation = new Vector3(0, -a, 0) };
            ring.AddChild(charge);
            charge.AddChild(new MeshInstance3D { Mesh = new CapsuleMesh { Radius = 0.075f, Height = 0.26f, RadialSegments = 12, Rings = 4 }, RotationDegrees = new Vector3(90, 0, 0), MaterialOverride = dark });
            charge.AddChild(new MeshInstance3D { Mesh = new CylinderMesh { TopRadius = 0.082f, BottomRadius = 0.082f, Height = 0.04f, RadialSegments = 12 }, RotationDegrees = new Vector3(90, 0, 0), MaterialOverride = brass });
            var lm = Lit(new Color(1f, 0.15f, 0.1f), 0.4f); lights.Add(lm); local.Add(p);
            charge.AddChild(new MeshInstance3D { Mesh = new SphereMesh { Radius = 0.03f, Height = 0.06f, RadialSegments = 10, Rings = 5 }, Position = new Vector3(0, 0.085f, 0), MaterialOverride = lm });
        }
        return (ring, lights, local);
    }

    async Task RingOfDestructionFx(int style)
    {
        var foes = TheirMonsters(); var t = foes[1];
        var trap = Of(0, "Ring of Destruction"); Place(trap, SpellZ(2, 0, false));
        await Wait(0.5);
        await Flip(trap, FxPink, "SE_TDY_CHAINGET_CHAIN_01");
        var p = t.Root.GlobalPosition; int atk = t.Card.Data.Atk;
        var (ring, lights, _) = RingProp(p + Vector3.Up * 2.4f);
        var red = new Color(1f, 0.15f, 0.1f);
        void Light(int i, bool on) => lights[i].AlbedoColor = on ? new Color(red.R * 3f, red.G * 3f, red.B * 3f) : new Color(red.R * 0.4f, red.G * 0.4f, red.B * 0.4f);
        var tw = ring.CreateTween().SetParallel();
        tw.TweenProperty(ring, "position:y", p.Y + 0.16f, 0.4).SetTrans(Tween.TransitionType.Bounce).SetEase(Tween.EaseType.Out);
        tw.TweenProperty(ring, "rotation:y", 2.2f, 0.4);
        await Wait(0.42);
        Cue("land"); Shake(0.04f); Streaks(p + Vector3.Up * 0.15f, new Color(1f, 0.8f, 0.5f), 8, 1.6f);
        // it tightens on the card, and the lights run round it faster and faster
        ring.CreateTween().TweenProperty(ring, "scale", new Vector3(0.95f, 1.18f, 0.95f), 0.3).SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
        double gap = 0.11; int at = 0;
        for (int step = 0; step < 22; step++)
        {
            for (int i = 0; i < 8; i++) Light(i, i == at % 8);
            Snd("SE_DUEL_TIME_COUNT");
            await Wait(gap); gap = Math.Max(0.028, gap * 0.9); at++;
        }
        for (int i = 0; i < 8; i++) Light(i, true);
        FxQuad(CoreGlowFx, p + Vector3.Up * 0.2f, new Vector2(1.6f, 1.6f), 0.15, red, Bill);
        await Wait(0.12);
        Snd("SE_ATTACK_A_FIRE_SPECIAL_02");
        ring.QueueFree();
        var s = CardSheet(t);
        BurnAway(s, new Color(1f, 0.5f, 0.12f), 0.35, null, 0, 0.4f, 6f);
        Explosion(p, 1.05f);
        LpHit(0, 8000, 8000 - atk); LpHit(1, 8000, 8000 - atk); Vignette(new Color(0.5f, 0.08f, 0.02f), 0.8);
        await Wait(2.3);
    }
}
