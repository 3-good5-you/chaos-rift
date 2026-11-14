using Godot;
using PoC.Engine;

namespace PoC.Game.UI;

/// FX Lab "signature cards", round 7 (2026-11-13). Locked in round 6: Negate Attack B (rewind, now without the
/// lingering sparks), Monster Reborn A, Torrential Tribute B (mist instead of sparks). Wings rebuilt: mirrored
/// left-right (the narrow root at the body), and they flap with the feathers trailing (FxKit3 MakeWing / Flap).
/// The other two God cards join Obelisk.
public partial class DuelField
{
    void Batch5Rows(Action<int, string, Func<int, Task>, string[]> row)
    {
        row(73, "Mirror Force", MirrorForce7, new[] {
            "Prism barrier: the curved mirror, now with a rainbow edge running over it; the hit sends back lances of light, one per attacker, each leaving a fading trail",
            "Reflection: the attack sinks into the mirror, the mirror flashes, and the same attack comes back out many times over, one into each attacker" });
        row(75, "Harpie's Feather Duster", FeatherDuster7, new[] { "Wing sweep, flying: the great wing flaps as it crosses their back row, its feathers bending with each beat; each card it passes bursts into feathers" });
        row(82, "Change of Heart", ChangeOfHeart7, new[] { "Wings, right way round: the narrow roots meet behind the monster, the wings open out and beat twice, feathers trailing; the heart; it crosses" });
        row(99, "Summon: Dark Magician", DarkMagician7, new[] { "Circle with impact, more: the circle, violet fire, three dark bolts, a slow-motion beat on the burst, a shock ring and the card burning in" });
        row(100, "Summon: Red-Eyes B. Dragon", RedEyes6, new[] { "Red lightning (no eyes): the Blue-Eyes summon in red" });
        row(101, "Summon: Obelisk the Tormentor", Obelisk7, new[] { "Descent, with weight: blue souls rise, the table goes deep blue, the colossal strike lands in slow motion, then the card" });
        row(105, "Summon: Slifer the Sky Dragon", Slifer7, new[] {
            "Coils: three red souls rise; a long serpent of red lightning coils down out of the sky round the zone and strikes it; slow-motion beat; the card",
            "Thunderhead: the sky goes red-black, red bolts fall faster and faster round the zone, then one red strike and the card" });
        row(106, "Summon: The Winged Dragon of Ra", Ra7, new[] {
            "Sun: three gold souls rise into a sun over the zone; it swells, burns white, drops onto the zone and bursts in gold fire; slow-motion beat; the card",
            "Phoenix: gold wings of fire open over the zone and beat once, the fire pours down onto the zone, and the card burns in" });
    }

    // ---------------------------------------------------------------- locked fixes

    /// Negate Attack B, locked: the rewind leaves faint afterimages that go with the card (no sparks left behind).
    async Task NegateRewind(View a, Transform3D home, Color blue)
    {
        Snd("SE_DUEL_TIME_COUNT_ZERO");
        for (int i = 0; i < 5; i++)
        {
            double d = i * 0.07;
            After(d, () =>
            {
                var g = NewSheet(a.Face.AlbedoTexture, a.Root.GlobalTransform);
                g.Set("glow_col", blue); g.Set("glow", 0.8f); g.Set("darken", 0.4f);
                g.Set("burn", 0.001f); Anim(g.M, "burn", 0.2f, 1f, 0.3);
                After(0.32, () => { if (IsInstanceValid(g.Mi)) g.Mi.QueueFree(); });
            });
        }
        Fly(a, home, 0.45f, Tween.TransitionType.Sine, Tween.EaseType.InOut);
        await Wait(0.5);
    }

    // ---------------------------------------------------------------- 73 Mirror Force

    const string PrismMirrorCode = @"shader_type spatial;
render_mode unshaded, blend_add, cull_disabled, depth_draw_never, shadows_disabled;
uniform vec4 col : source_color; uniform float amt = 0.0; uniform float flare = 0.0; uniform sampler2D noise : repeat_enable, filter_linear_mipmap;
varying vec3 lp;
void vertex() { lp = VERTEX; }
vec3 hue(float h) { return clamp(abs(mod(h * 6.0 + vec3(0.0, 4.0, 2.0), 6.0) - 3.0) - 1.0, 0.0, 1.0); }
void fragment() {
  float f = pow(1.0 - abs(dot(normalize(NORMAL), normalize(VIEW))), 1.6);
  float band = fract(UV.x * 1.5 + UV.y * 0.4 - TIME * 0.25);
  vec3 rainbow = hue(band) * smoothstep(0.0, 0.2, f);
  float glint = exp(-pow((UV.x * 1.4 + UV.y * 0.6 - fract(TIME * 0.4) * 2.4 + 0.2) / 0.03, 2.0));
  float hit = exp(-pow(length((UV - vec2(0.5, 0.55)) * vec2(2.5, 1.0)) / (0.25 + flare * 0.5), 2.0)) * flare;
  float edges = smoothstep(0.0, 0.12, UV.x) * smoothstep(1.0, 0.88, UV.x) * smoothstep(0.0, 0.1, 1.0 - UV.y) * smoothstep(1.0, 0.75, 1.0 - UV.y);
  float rise = smoothstep(amt * 1.1 - 0.1, amt * 1.1, 1.0 - UV.y);
  ALBEDO = mix(col.rgb * 0.7 + rainbow * 0.5, vec3(1.0), clamp(glint + hit, 0.0, 1.0));
  ALPHA = clamp((0.08 + f * 0.5 + glint * 0.6 + hit) * edges, 0.0, 1.0) * (1.0 - rise) * smoothstep(0.15, -0.55, lp.z);
}";

    async Task MirrorForce7(int style)
    {
        var foes = TheirMonsters();
        var mine = MineAt("Celtic Guardian", 2);
        var a = foes[1];
        var trap = Of(0, "Mirror Force"); Place(trap, SpellZ(2, 0, false));
        await Wait(0.4);
        Charge(a, mine.Root.GlobalPosition, 0.35, 0.35f);
        await Wait(0.3);
        await Flip(trap, FxPink, "SE_EV_MIRRORFORCE");
        var silver = new Color(0.78f, 0.9f, 1f); var fire = new Color(1f, 0.6f, 0.3f);
        var hitAt = mine.Root.GlobalPosition + new Vector3(0, 0.5f, -CH * 0.9f);
        var m = Shade(PrismMirrorCode, ("col", silver), ("noise", FxNoise), ("amt", 0f));
        var wall = new MeshInstance3D { Mesh = new CylinderMesh { TopRadius = 1, BottomRadius = 1, Height = 1, RadialSegments = 64, Rings = 1, CapTop = false, CapBottom = false }, MaterialOverride = m, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            Position = new Vector3(0, 0.5f, mine.Root.GlobalPosition.Z + 0.5f), Scale = new Vector3(3.0f, 1.0f, 1.35f) };
        world.AddChild(wall);
        Anim(m, "amt", 0f, 1f, 0.3, 0, Tween.TransitionType.Cubic, Tween.EaseType.Out);
        var atk = foes.Where(f => !f.S.Def).ToList();
        if (style == 1)
        {
            Orb(Line(a.Root.GlobalPosition + Vector3.Up * 0.2f, hitAt, 0.2f), fire, 0.32, 0.7f);
            await Wait(0.33);
            Anim(m, "flare", 1f, 0f, 0.6, 0, Tween.TransitionType.Expo, Tween.EaseType.Out);
            Warp(hitAt, 2.6f, 0.45, 1.6f, false, Bill); Flash(hitAt, silver, 10f); ScreenFlash(silver, 0.3f, 0.25); Shake(0.1f); HitStop(0.06);
            await Wait(0.08);
            foreach (var f in atk)
            {
                var to = f.Root.GlobalPosition + Vector3.Up * 0.1f;
                Orb(Line(hitAt, to, 0.1f), Colors.White, 0.16, 0.5f);
                Beam(hitAt, to, 0.35f, 0.7, silver);   // the trail it leaves, fading
                After(0.16, () => { Burn(f, new Color(0.75f, 0.9f, 1f), 0.55, new Vector2(0.5f, 0.9f), 0, 1f, 6f); Flash(to, silver, 6f); Streaks(to, Colors.White, 10, 2.2f); });
            }
        }
        else
        {
            var orb = Orb(Line(a.Root.GlobalPosition + Vector3.Up * 0.2f, hitAt, 0.2f), fire, 0.32, 0.7f);
            await Wait(0.32);
            orb.CreateTween().TweenProperty(orb, "scale", Vector3.One * 0.05f, 0.18);
            Warp(hitAt, 2f, 0.35, 1.2f, true, Bill);
            await Wait(0.25);
            Anim(m, "flare", 1.2f, 0f, 0.7, 0, Tween.TransitionType.Expo, Tween.EaseType.Out);
            Flash(hitAt, silver, 12f); ScreenFlash(Colors.White, 0.35f, 0.25); Shake(0.12f); HitStop(0.06);
            foreach (var f in atk)
            {
                var to = f.Root.GlobalPosition + Vector3.Up * 0.1f;
                for (int r = 0; r < 2; r++) { double d = r * 0.06; After(d, () => Orb(Line(hitAt, to, 0.15f), fire, 0.2, 0.6f)); }
                After(0.24, () => { Burn(f, new Color(1f, 0.75f, 0.45f), 0.5, new Vector2(0.5f, 0.9f), 0, 1f, 6f); Flash(to, fire, 6f); });
            }
        }
        Snd("SE_CARD_FINISH_BREAK_02", 0.15);
        Anim(m, "amt", 1f, 0f, 0.4, 0.6);
        After(1.1, () => wall.QueueFree());
        await Wait(0.4);
        GoHome(a);
        await Wait(1.6);
    }

    // ---------------------------------------------------------------- 75 Harpie's Feather Duster

    async Task FeatherDuster7(int style)
    {
        var board = RivalBoard(); var rows = board.Where(v => v.S.Loc == Loc.Spell).ToList();
        foreach (var (name, zone) in new[] { ("Mirror Force", 0), ("Trap Hole", 4) }) { var v = Of(1, name); Place(v, SpellZ(zone, 1, false)); rows.Add(v); }
        await PlayCard("Harpie's Feather Duster", FxGreen, sound: "SE_EV_HARPIESFEATHER_DUSTER");
        var gold = new Color(1f, 0.92f, 0.72f); float z = ZonePos(Loc.Spell, 2, 1).Z;
        // a right wing, root leading, flying left to right low over their back row
        var w = MakeWing(new Vector3(-4.3f, 0.75f, z + 0.15f), 1, 2.0f, gold, Colors.White, Bill * new Basis(Vector3.Right, -0.35f));
        CreateTween().TweenMethod(Callable.From<float>(e => w.M.SetShaderParameter("amt", e)), 0f, 1f, 0.2);
        Flap(w, 1.3, 3f, 0.4f, 0.1f);
        var tw = w.Pivot.CreateTween();
        tw.TweenProperty(w.Pivot, "position:x", 3.4f, 1.3).SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.InOut);
        tw.TweenMethod(Callable.From<float>(e => w.M.SetShaderParameter("amt", 1 - e)), 0f, 1f, 0.3);
        tw.TweenCallback(Callable.From(w.Pivot.QueueFree));
        Snd("SE_ATTACK_A_WIND_SPECIAL_01", 0.1);
        foreach (var v in rows)
        {
            float x = v.Root.GlobalPosition.X;
            double d = 1.3 * Mathf.Acos(1 - 2 * Mathf.Clamp((x - 0.6f + 4.3f) / 7.7f, 0, 1)) / Mathf.Pi;   // when the wing's middle is over it
            After(d, () =>
            {
                var s = CardSheet(v);
                BurnAway(s, Colors.White, 0.35, new Vector2(0.5f, 0.5f), 0, 0.3f, 4f);
                var p = s.Mi.GlobalPosition;
                for (int i = 0; i < 8; i++) Feather(p + new Vector3(R(-0.25, 0.25), 0.1f, R(-0.3, 0.3)), p + new Vector3(R(0.4, 1.4), R(0.6, 1.4), R(-0.5, 0.3)), i % 2 == 0 ? gold : Colors.White, R(0.9, 1.4), 0.4f);
            });
        }
        await Wait(2.6);
    }

    // ---------------------------------------------------------------- 82 Change of Heart

    async Task ChangeOfHeart7(int style)
    {
        var foes = TheirMonsters(); var t = foes[1];
        await PlayCard("Change of Heart", FxGreen, sound: "SE_EV_EFFECT_VEILER");
        var p = t.Root.GlobalPosition; var goal = Mon(t, 3, 0);
        var root = p + new Vector3(0, 0.45f, -0.55f);
        SolidWing(root - Bill.X * 0.04f, -1, 1.3f, new Color(1f, 0.93f, 0.78f), Colors.White, 0.45, 1.3);
        SolidWing(root + Bill.X * 0.04f, 1, 1.3f, new Color(0.45f, 0.26f, 0.75f), new Color(0.85f, 0.7f, 1f), 0.45, 1.3);
        FxQuad(SoftHeartFx, p + new Vector3(0, 1.05f, -0.95f), new Vector2(0.8f, 0.8f), 1.9, new Color(1f, 0.95f, 0.85f), Bill, true, null, ("col2", new Color(0.32f, 0.12f, 0.55f)), ("amt", 2.5f));
        Snd("SE_MONSTER_CUTIN_LIGHT", 0.3);
        await Wait(1.75);
        Move(t, goal, 0, 0.6f, 0.9f); await Wait(0.62);
        var land = t.Root.GlobalPosition;
        FxQuad(RingFx, land with { Y = 0.04f }, new Vector2(1.6f, 1.6f), 0.5, FxPink); FxQuad(GroundGlowFx, land with { Y = 0.02f }, new Vector2(1.3f, 1.3f), 0.6, FxPink); Cue("land");
        await Wait(1.5);
    }

    // ---------------------------------------------------------------- summons

    /// The heavy moment of a big summon: everything slows to a quarter for a beat, the table bends, the camera shakes.
    void SlowBeat(Vector3 at, Color c, float power = 1f)
    {
        HitStop(0.45 * power, 0.25);
        Warp(at + Vector3.Up * 0.05f, 8f * power, 0.8, 2.6f * power); Shake(0.3f * power); ScreenFlash(c, 0.4f, 0.4);
        Snd("SE_CARD_FINISH_BREAK_03");
    }

    async Task DarkMagician7(int style)
    {
        var v = Of(0, "Dark Magician"); var at = Mon(v, 2, 0); var p = Grid3(at);
        var violet = new Color(0.7f, 0.4f, 1f); var pale = new Color(0.9f, 0.75f, 1f);
        Snd("SE_MONSTER_CUTIN_DARK");
        ScreenDim(0.45f, 0.25, 1.5, 0.5);
        FxQuad(CircleFx, p + Vector3.Up * 0.02f, new Vector2(2.8f, 2.8f), 2.6, violet, Flat, false, null, ("amt", 2f));
        for (int i = 0; i < 12; i++) { float a = i * Mathf.Tau / 12; var q = p + new Vector3(Mathf.Cos(a) * 1.2f, -0.05f, Mathf.Sin(a) * 1.2f); double d = 0.2 + i * 0.03; After(d, () => FxStand(SoftFireFx, q, new Vector2(0.6f, 1.05f), 1.8, violet, false, null, ("col2", pale), ("amt", 1.1f))); }
        await Wait(0.65);
        for (int i = 0; i < 3; i++) { double d = i * 0.13; After(d, () => { LightningHit(p + new Vector3(R(-0.35, 0.35), 0, R(-0.3, 0.3)), violet, 0.8f, dim: false); Snd("SE_DUEL_LIVE_THUNDER"); }); }
        await Wait(0.45);
        SlowBeat(p, violet, 0.9f);
        FxQuad(RingFx, p with { Y = 0.05f }, new Vector2(3.2f, 3.2f), 0.6, pale);
        await BuildIn(v, at, violet, 0.7, new Vector2(0.5f, 1f), 7f, (s, k) => { if (GD.Randf() < 0.4f) Crawl(s, pale); }, 1.3f);
        FxQuad(CoreGlowFx, p + Vector3.Up * 0.2f, new Vector2(1.4f, 1.4f), 0.35, violet, Bill);
        Cue("land");
        await Wait(1.5);
    }

    /// The God summons share one frame: three souls rise, the sky takes the God's colour, its own arrival, the slow beat,
    /// and the card burning in. `arrive` plays the God's own part and returns when it lands.
    async Task GodSummon(string[] names, Color col, Color pale, string cut, Func<Vector3, Task> arrive)
    {
        var tribs = new[] { MineAt("Celtic Guardian", 1), MineAt("Feral Imp", 2), MineAt("Curse of Dragon", 3) };
        await Wait(0.4);
        var v = names.Select(n => views.Values.FirstOrDefault(x => x.Card.Owner == 0 && x.Card.Name == n)).FirstOrDefault(x => x != null) ?? Of(0, "Obelisk the Tormentor");
        var at = Mon(v, 2, 0); var p = Grid3(at);
        Snd(cut);
        foreach (var t in tribs) { var s = CardSheet(t); BurnAway(s, col, 0.45, null, 0, 0.6f, 5f); SoulStream(t.Root.GlobalPosition, p + new Vector3(0, 2.5f, -1f), 8, col, 0.6, 0.6f, 0.03); }
        Snd("SE_SUMMON_ADVANCE", 0.1);
        await Wait(0.6);
        ScreenDim(0.55f, 0.25, 1.2, 0.5); Vignette(col.Darkened(0.75f), 1.8);
        await arrive(p);
        FieldLines(0f, 0.15);
        SlowBeat(p, col, 1.1f);
        for (int i = 0; i < 12; i++) Puffs(p + new Vector3(Mathf.Cos(i * 0.52f) * 1.2f, 0.05f, Mathf.Sin(i * 0.52f) * 1.0f), 1, new Color(0.4f, 0.42f, 0.5f, 0.6f), 0.9f, 0.4f, 0.1f);
        await BuildIn(v, at, pale, 0.6, new Vector2(0.5f, 0.5f), 8f, (s, k) => { if (GD.Randf() < 0.6f) Crawl(s, pale); });
        FieldLines(1f, 0.5);
        Snd("SE_DUEL_SUMMON_COUNT_05");
        Cue("land");
        await Wait(1.3);
    }

    Task Obelisk7(int style) => GodSummon(new[] { "Obelisk the Tormentor" }, new Color(0.35f, 0.6f, 1f), new Color(0.75f, 0.88f, 1f), "SE_MONSTER_CUTIN_DIVINE", async p =>
    {
        Snd("SE_DUEL_LIVE_THUNDER");
        await Wait(0.3);
        LightningHit(p, new Color(0.75f, 0.88f, 1f), 2.0f, dim: false);
        Snd("SE_EV_RAIGEKI", 0.05);
        await Wait(0.12);
    });

    Task Slifer7(int style) => GodSummon(new[] { "Slifer the Sky Dragon" }, new Color(1f, 0.25f, 0.2f), new Color(1f, 0.7f, 0.6f), "SE_MONSTER_CUTIN_DIVINE", async p =>
    {
        var red = new Color(1f, 0.3f, 0.22f);
        if (style == 1)
        {
            // a serpent of red lightning coils down out of the sky round the zone
            Vector3 Coil(float k) { float a = k * 3.2f * Mathf.Tau, r = Mathf.Lerp(1.6f, 0.15f, k); return p + new Vector3(Mathf.Cos(a) * r, Mathf.Lerp(4.2f, 0.2f, k * k), Mathf.Sin(a) * r * 0.8f); }
            for (int j = 0; j < 14; j++) { double d = j * 0.03; float sz = 0.8f - j * 0.045f; After(d, () => Orb(Coil, j == 0 ? Colors.White : red, 1.1, sz, null, Tween.EaseType.In)); }
            for (int i = 0; i < 6; i++) { double d = 0.15 + i * 0.15; After(d, () => { var k = (float)(d / 1.1); Arc(Coil(k), Coil(Mathf.Min(1, k + 0.08f)), red, 0.5f, 0.2); }); }
            Snd("SE_DUEL_LIVE_THUNDER", 0.2); Snd("SE_ATTACK_A_WIND_SPECIAL_01", 0.3);
            await Wait(1.1);
            LightningHit(p, new Color(1f, 0.6f, 0.5f), 1.8f, dim: false);
            Snd("SE_EV_RAIGEKI", 0.05);
            await Wait(0.12);
        }
        else
        {
            ScreenDim(0.35f, 0.1, 1.2, 0.3);
            for (int i = 0; i < 9; i++)
            {
                float a = R(0, 6.28); var q = p + new Vector3(Mathf.Cos(a) * R(0.8, 1.6), 0, Mathf.Sin(a) * R(0.7, 1.3));
                LightningHit(q, i % 2 == 0 ? red : new Color(1f, 0.6f, 0.5f), 0.5f, dim: false);
                if (i % 3 == 0) Snd("SE_DUEL_LIVE_THUNDER");
                await Wait(0.22 - i * 0.018);
            }
            LightningHit(p, new Color(1f, 0.7f, 0.6f), 2.0f, dim: false);
            Snd("SE_EV_RAIGEKI", 0.05);
            await Wait(0.12);
        }
    });

    Task Ra7(int style) => GodSummon(new[] { "The Winged Dragon of Ra" }, new Color(1f, 0.75f, 0.25f), new Color(1f, 0.92f, 0.65f), "SE_MONSTER_CUTIN_DIVINE", async p =>
    {
        var gold = new Color(1f, 0.78f, 0.3f);
        if (style == 1)
        {
            var sunAt = p + new Vector3(0, 1.4f, -0.9f);
            var sun = FxQuad(OrbFx, sunAt, new Vector2(0.6f, 0.6f), 1.6, gold, Bill, false, null, ("amt", 1.6f));
            var halo = FxQuad(FireFx, sunAt, new Vector2(1.6f, 1.6f), 1.6, gold, Bill, false, null, ("col2", Colors.White), ("amt", 0.0f));
            sun.CreateTween().TweenProperty(sun, "scale", Vector3.One * 3.2f, 1.0).SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.In);
            for (int i = 0; i < 16; i++) { double d = i * 0.05; After(d, () => { var dir = new Vector3(R(-1, 1), R(-1, 1), 0).Normalized(); Ember(sunAt + Bill * dir * 1.2f, gold, -(Bill * dir) * 1.2f, 0.6, 0.06f, 0); }); }
            Snd("SE_ATTACK_A_FIRE_SPECIAL_02", 0.2);
            await Wait(1.0);
            ScreenFlash(Colors.White, 0.3f, 0.2);
            sun.CreateTween().TweenProperty(sun, "position", p + Vector3.Up * 0.15f, 0.2).SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.In);
            await Wait(0.2);
            sun.QueueFree(); halo.QueueFree();
            Explosion(p, 1.2f, gold);
            Snd("SE_ATTACK_A_FIRE_SPECIAL_02");
        }
        else
        {
            var root = p + new Vector3(0, 0.9f, -0.9f);
            SolidWing(root - Bill.X * 0.05f, -1, 2.2f, new Color(1f, 0.78f, 0.35f), new Color(1f, 0.97f, 0.85f), 0.4, 0.9, 1.5f);
            SolidWing(root + Bill.X * 0.05f, 1, 2.2f, new Color(1f, 0.78f, 0.35f), new Color(1f, 0.97f, 0.85f), 0.4, 0.9, 1.5f);
            for (int i = 0; i < 24; i++) { double d = 0.2 + i * 0.03; After(d, () => Ember(root + new Vector3(R(-1.6, 1.6), R(-0.2, 0.6), 0), gold, new Vector3(0, R(-0.2, 0.4), 0), 0.9, 0.06f, 0.4f)); }
            Snd("SE_ATTACK_A_WIND_SPECIAL_01", 0.3);
            await Wait(1.0);
            for (int i = 0; i < 3; i++) FxStand(SoftFireFx, p + new Vector3((i - 1) * 0.3f, 0, 0), new Vector2(1.4f, 2.6f), 1.0, gold, false, null, ("col2", Colors.White), ("amt", 1.3f));
            Snd("SE_ATTACK_A_FIRE_SPECIAL_02");
            await Wait(0.3);
        }
    });
}
