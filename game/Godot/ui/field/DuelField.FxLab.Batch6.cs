using Godot;
using PoC.Engine;

namespace PoC.Game.UI;

/// FX Lab "signature cards", round 10 (2026-11-13). Every new idea comes twice: on a darkened table and on the lit
/// table. Harpie's Feather Duster is now the duster from its card art (curved white handle with a gold ring, a fan of
/// long pointed feathers, a gold swish trail; the cards it hits are batted away in sparkles). Change of Heart C gets a
/// real heartbeat. The three Gods start fresh, three ideas each, all ending in a lightning strike with a brief slow
/// motion; every sound is timed so its loud part lands on what it belongs to (SndHit).
public partial class DuelField
{
    void Batch6Rows(Action<int, string, Func<int, Task>, string[]> row)
    {
        // all locked 2026-11-13 (Feather Duster A, Change of Heart C, Dark Magician A, Obelisk A, Slifer D, Ra A): no rows left
    }

    /// `name` timed so its loud part (`onset` s into the file) lands `hitIn` s from now (duel time).
    void SndHit(string name, double hitIn, double onset) => Snd(name, Math.Max(0, hitIn - onset));

    // sound onsets measured from the files (first moment at half the peak level)
    const double OnThunder = 0.08, OnKaminari = 0.14, OnDivine2 = 0.10, OnLand = 0.02;

    // ---------------------------------------------------------------- 75 Harpie's Feather Duster: a sweep of real feathers

    /// One real feather (feather_hq.png: a public-domain photo of a white feather, cut out; tip at the top), fading out
    /// over its life (t).
    const string LooseFeatherFx = @"void fragment() { vec4 c = texture(tex, UV); ALBEDO = c.rgb; ALPHA = c.a * amt * env(0.04, 0.3); }";
    const float FeatherAspect = 756f / 1024f;

    /// A feather flying a path: `path(t)` gives its position t seconds after it appears; it spins `spin` rad/s about
    /// the view, and rocks a little.
    void FlyingFeather(Func<float, Vector3> path, double life, float size, float spin)
    {
        var q = FxQuad(LooseFeatherFx, path(0), new Vector2(size * FeatherAspect, size), life, Colors.White, Bill, true, "feather_hq", ("amt", 1f));
        float ph = R(0, 6.28), rock = R(5, 9);
        q.CreateTween().TweenMethod(Callable.From<float>(k =>
        {
            float t = k * (float)life;
            q.Transform = new Transform3D(Bill * new Basis(Vector3.Back, ph + spin * t + Mathf.Sin(ph + t * rock) * 0.4f), path(t));
        }), 0f, 1f, life);
    }

    List<View> BackRowFoes()
    {
        var l = RivalBoard().Where(v => v.S.Loc == Loc.Spell).ToList();
        foreach (var (name, zone) in new[] { ("Mirror Force", 0), ("Trap Hole", 4) }) { var v = Of(1, name); Place(v, SpellZ(zone, 1, false)); l.Add(v); }
        return l.OrderBy(v => v.Root.GlobalPosition.X).ToList();
    }

    /// A card caught by the feathers: it becomes a sheet of its own picture at once (the real card can move on unseen);
    /// at `delay` it is torn off the table, carried along with the feathers (+X) tumbling, then torn apart into pieces
    /// of its own picture and a few more feathers, all flying on with the stream.
    void CaughtInFeathers(View v, float speed, double delay)
    {
        var s = CardSheet(v); var x0 = s.Mi.GlobalTransform;
        After(delay, () =>
        {
            Snd("SE_CARD_ST_BREAK");
            var axis = new Vector3(R(-0.3, 0.3), 0.3f, 1f).Normalized();
            s.Set("flutter", 1.4f);
            const double carry = 0.35;
            var tw = s.Mi.CreateTween();
            tw.TweenMethod(Callable.From<float>(k =>
            {
                s.Mi.GlobalTransform = new Transform3D(new Basis(axis, -k * 4f) * x0.Basis, x0.Origin + new Vector3(speed * 0.55f * k * (float)carry * 2.5f, Mathf.Sin(k * 2.4f) * 0.6f, -0.15f * k));
                s.Set("curl", k * 0.7f);
            }), 0f, 1f, carry).SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.Out);
            tw.TweenCallback(Callable.From(() =>
            {
                var p = s.Mi.GlobalPosition;
                Flakes(s, 28, uv => new Vector3(speed * R(0.5, 0.9), R(0.2, 0.9), R(-0.5, 0.3)), 0.9, 0.08, null, 0.17f, 0.6f);
                for (int i = 0; i < 5; i++)
                {
                    var o = p + new Vector3(R(-0.2, 0.2), R(-0.1, 0.2), R(-0.2, 0.2)); float vx = speed * R(0.5, 0.8), vy = R(0.3, 0.9);
                    FlyingFeather(t => o + new Vector3(vx * t, vy * t - 0.4f * t * t, 0), R(1.0, 1.5), R(0.22, 0.32), R(-4, 4));
                }
                s.Mi.QueueFree();
                Snd("SE_CARD_FINISH_BREAK_02");
            }));
        });
    }

    /// LOCKED 2026-11-13: style 1 (gust) is Harpie's Feather Duster. The sweep: feathers blow in from the left end of the field and sweep across their back row to the right end;
    /// every card the front reaches is caught and torn apart. `style`: 1 = gust (a low, fast, dense stream along the
    /// row), 2 = wave (the feathers rise into a wave that rolls over and comes down across the row), 3 = whirl (a
    /// spinning tube of feathers that drills along the row).
    async Task FeatherSweep(int style)
    {
        var row = BackRowFoes();
        await PlayCard("Harpie's Feather Duster", FxGreen);
        await SweepAcross(row, style);
    }

    /// The sweep over `row` (cards on the field, left to right): see FeatherSweep. In duels: Harpie's Feather Duster
    /// (style 1, the locked gust) over the cards it destroys.
    async Task SweepAcross(List<View> row, int style)
    {
        row = row.OrderBy(v => v.Root.GlobalPosition.X).ToList();
        float rowY = row[0].Root.GlobalPosition.Y + 0.15f, rowZ = row[0].Root.GlobalPosition.Z;
        const float X0 = -4.6f, X1 = 4.8f;
        double dur = style == 2 ? 1.25 : style == 3 ? 1.35 : 1.0;
        float speed = (X1 - X0) / (float)dur;
        // the wind, then the duster's own sound with its swell in the middle of the sweep
        Snd("C_GALE");
        SndHit("SE_EV_HARPIESFEATHER_DUSTER", dur * 0.5, 0.55);
        Snd("SE_ATTACK_A_WIND_SPECIAL_01", dur * 0.3);
        int n = style == 1 ? 150 : 130;
        for (int i = 0; i < n; i++)
        {
            double d = dur * i / n * 0.95;
            float frontX = X0 + speed * (float)d;   // where the front is when this feather appears
            // most ride the front; four in ten are left behind by it, slow down and drift, so a long stream of
            // feathers trails across the field behind the front
            bool trail = i % 5 < 2;
            float size = R(0.26, 0.48), life = trail ? R(1.5, 2.2) : R(0.9, 1.4);
            float y0 = rowY + R(-0.05, 0.35), z0 = rowZ + R(-0.45, 0.45), vx = speed * (trail ? R(0.12, 0.4) : R(0.85, 1.25));
            Func<float, Vector3> path = style switch
            {
                // gust: straight along the row, low, a little flutter
                1 => t => new Vector3(frontX + vx * t, y0 + Mathf.Sin(t * 7f + i) * 0.06f + 0.15f * t, z0 + Mathf.Sin(t * 5f + i * 0.7f) * 0.06f),
                // wave: each feather rises up the face of the wave and rolls over forward and down
                2 => t => { float u = Mathf.Min(1, t / 0.7f); return new Vector3(frontX - 0.6f + vx * t, rowY + 1.3f * Mathf.Sin(u * Mathf.Pi) * R01(i) + 0.05f, z0 - 0.3f * Mathf.Sin(u * Mathf.Pi)); },
                // whirl: round a tube along the row while travelling, the tube widening behind the front
                _ => t => { float a = i * 2.39f + t * 11f, r = 0.25f + 0.35f * Mathf.Min(1, t * 2); return new Vector3(frontX + vx * t, rowY + 0.35f + Mathf.Cos(a) * r, rowZ + Mathf.Sin(a) * r); },
            };
            float spin = R(-6, 6);
            After(d, () => FlyingFeather(path, life, size, spin));
        }
        foreach (var v in row)
        {
            double hit = (v.Root.GlobalPosition.X - X0) / speed;
            CaughtInFeathers(v, speed, hit);
        }
        await Wait(dur + 1.6);
    }

    static float R01(int i) => 0.6f + 0.4f * Mathf.Abs(Mathf.Sin(i * 12.9898f));   // a steady per-feather 0.6..1 (paths are re-evaluated every frame)

    // ---------------------------------------------------------------- locked (rows left the lab; kept for wiring)

    static readonly Color DevilCol = new(0.42f, 0.22f, 0.7f), DevilHi = new(0.82f, 0.62f, 1f);

    /// LOCKED 2026-11-13: Change of Heart C, "Heartbeat", with the lub-dub sound on every beat (lab: their monster
    /// comes to your side).
    async Task ChangeOfHeartLocked()
    {
        var foes = TheirMonsters(); var t = foes[1];
        await PlayCard("Change of Heart", FxPink);
        var heart = await HeartBeats(t);
        Move(t, Mon(t, 3, 0), 0, 0.8f, 0.25f);
        await HeartCarry(t, heart, 0.8);
        await Wait(1.3);
    }

    /// Change of Heart, part one: a dark heart over the monster beats three times (lub-dub), turning from violet to
    /// warm pink. Returns the heart (it lives on through HeartCarry).
    async Task<MeshInstance3D> HeartBeats(View t)
    {
        var tp = t.Root.GlobalPosition;
        var heart = FxQuad(SoftHeartFx, tp + Vector3.Up * 1.0f, new Vector2(1.25f, 1.25f), 3.2, DevilCol, Bill, true, null, ("col2", DevilHi), ("amt", 0f));
        var hm = (ShaderMaterial)heart.MaterialOverride; hm.RenderPriority = 2;
        await Wait(0.35);
        for (int b = 0; b < 3; b++)
        {
            float k = (b + 1) / 3f;
            Snd("C_HEARTBEAT");   // lub on the big swell, dub 0.2 s later on the second, smaller swell
            var tw = heart.CreateTween();
            tw.TweenProperty(heart, "scale", Vector3.One * 1.25f, 0.07).SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.Out);
            tw.TweenProperty(heart, "scale", Vector3.One * 1.04f, 0.12).SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.Out);
            tw.TweenProperty(heart, "scale", Vector3.One * 1.14f, 0.06).SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.Out);
            tw.TweenProperty(heart, "scale", Vector3.One, 0.2).SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.Out);
            hm.SetShaderParameter("col", DevilCol.Lerp(HeartPink, k)); hm.SetShaderParameter("col2", DevilHi.Lerp(HeartWarm, k));
            FxQuad(RingFx, tp with { Y = 0.04f }, new Vector2(1.0f + b * 0.3f, 1.0f + b * 0.3f), 0.45, DevilHi.Lerp(HeartPink, k));
            FxQuad(GroundGlowFx, tp with { Y = 0.02f }, new Vector2(1.1f, 1.1f), 0.4, DevilCol.Lerp(HeartPink, k));
            Shake(0.02f + b * 0.015f);
            await Wait(0.62 - b * 0.08);
        }
        Snd("C_HEART_LIGHT");
        return heart;
    }

    static readonly Color HeartPink = new(1f, 0.5f, 0.72f), HeartWarm = new(1f, 0.78f, 0.86f);

    /// Change of Heart, part two: while the monster slides over to its new side (moved by the caller), the heart
    /// rides above it trailing pink embers; a ring where it lands.
    async Task HeartCarry(View t, MeshInstance3D heart, double dur)
    {
        Snd("C_FEATHER_SWISH");
        var tw2 = CreateTween();
        tw2.TweenMethod(Callable.From<float>(k =>
        {
            var p = t.Root.GlobalPosition;
            if (IsInstanceValid(heart)) heart.Position = p + Vector3.Up * 0.95f;
            if (GD.Randf() < 0.7f) Ember(p + new Vector3(R(-0.25, 0.25), 0.05f, R(-0.3, 0.3)), HeartPink, new Vector3(0, R(0.1, 0.3), 0), 0.8, 0.045f, 0.2f);
        }), 0f, 1f, dur);
        await Wait(dur);
        var land = t.Root.GlobalPosition;
        FxQuad(RingFx, land with { Y = 0.04f }, new Vector2(1.6f, 1.6f), 0.5, HeartPink); FxQuad(GroundGlowFx, land with { Y = 0.02f }, new Vector2(1.3f, 1.3f), 0.6, HeartPink); Cue("land");
    }

    /// LOCKED 2026-11-13: Dark Magician summon A, circle with impact, dark magic hit; no darkening, no slow motion.
    async Task DarkMagicianLocked() { var v = Of(0, "Dark Magician"); await DarkMagicianFx(v, Mon(v, 2, 0)); }

    /// Dark Magician arriving at `at` (in duels: hidden as it lands; this plays and burns it in).
    async Task DarkMagicianFx(View v, St at)
    {
        var p = Grid3(at);
        var violet = new Color(0.7f, 0.4f, 1f); var pale = new Color(0.9f, 0.75f, 1f);
        Snd("SE_MONSTER_CUTIN_DARK");
        FxQuad(CircleFx, p + Vector3.Up * 0.02f, new Vector2(2.8f, 2.8f), 2.6, violet, Flat, false, null, ("amt", 2f));
        for (int i = 0; i < 12; i++) { float a = i * Mathf.Tau / 12; var q = p + new Vector3(Mathf.Cos(a) * 1.2f, -0.05f, Mathf.Sin(a) * 1.2f); double d = 0.2 + i * 0.03; After(d, () => FxStand(SoftFireFx, q, new Vector2(0.6f, 1.05f), 1.8, violet, false, null, ("col2", pale), ("amt", 1.1f))); }
        await Wait(0.65);
        for (int i = 0; i < 3; i++) { double d = i * 0.13; After(d, () => { LightningHit(p + new Vector3(R(-0.35, 0.35), 0, R(-0.3, 0.3)), violet, 0.8f, dim: false); Snd("SE_DUEL_LIVE_THUNDER"); }); }
        await Wait(0.45);
        Warp(p + Vector3.Up * 0.05f, 7f, 0.8, 2.3f); Shake(0.27f); ScreenFlash(violet, 0.35f, 0.4);
        Snd("SE_ATTACK_A_DARK_SPECIAL_02");
        FxQuad(RingFx, p with { Y = 0.05f }, new Vector2(3.2f, 3.2f), 0.6, pale);
        await BuildIn(v, at, violet, 0.7, new Vector2(0.5f, 1f), 7f, (s, k) => { if (GD.Randf() < 0.4f) Crawl(s, pale); }, 1.3f);
        FxQuad(CoreGlowFx, p + Vector3.Up * 0.2f, new Vector2(1.4f, 1.4f), 0.35, violet, Bill);
        Cue("land");
        await Wait(0.6);
    }

    // ---------------------------------------------------------------- scene light (dark table without dimming the effects)

    sealed class SceneLights { public Godot.Environment? Env; public float Amb, Bg; public readonly List<(Light3D L, float E)> Ls = new(); }
    SceneLights? sceneLights;

    /// The table's own lights (sun, ambient, sky, the two rim lights), read once.
    SceneLights Lights()
    {
        if (sceneLights != null) return sceneLights;
        var s = new SceneLights();
        foreach (var n in world.GetChildren())
            if (n is WorldEnvironment we) { s.Env = we.Environment; s.Amb = s.Env.AmbientLightEnergy; s.Bg = s.Env.BackgroundEnergyMultiplier; }
            else if (n is DirectionalLight3D d) s.Ls.Add((d, d.LightEnergy));
            else if (n is OmniLight3D o && o.OmniRange >= 6) s.Ls.Add((o, o.LightEnergy));
        return sceneLights = s;
    }

    /// How dark the table is: 0 = normal, 1 = dark, below 0 = lit up brighter than normal (a flash of the strike).
    /// Only the lights change, so lightning and glows stay at full brightness against the dark.
    void SetDark(float k)
    {
        var s = Lights(); float m = 1 - 0.85f * k;
        if (s.Env != null) { s.Env.AmbientLightEnergy = s.Amb * m; s.Env.BackgroundEnergyMultiplier = s.Bg * (1 - 0.75f * k); }
        foreach (var (l, e) in s.Ls) l.LightEnergy = e * m;
        RenderingServer.GlobalShaderParameterSet("table_dim", k);   // the mat, zone frames and sky (they light themselves)
    }

    /// The table darkens smoothly (lights down over `fadeIn`, a soft tinted vignette) and stays dark until the returned
    /// Lift() is called: then the strike lights the table up brighter for a moment and it settles back to normal.
    /// Real time throughout, so a slow motion never stretches it.
    Action DarkHold(Color tint, double fadeIn = 1.0)
    {
        var vm = Shade(VignetteCode, ("col", tint.Darkened(0.8f)), ("a", 0f));
        var vig = new ColorRect { Size = new Vector2(W, H), Material = vm, MouseFilter = MouseFilterEnum.Ignore };
        hud.AddChild(vig); hud.MoveChild(vig, 0);
        float k = 0;
        var tw = CreateTween().SetIgnoreTimeScale(true);
        tw.TweenMethod(Callable.From<float>(v => { k = v; SetDark(v); vm.SetShaderParameter("a", v * 0.55f); }), 0f, 1f, fadeIn).SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.InOut);
        bool lifted = false;
        return () =>
        {
            if (lifted) return; lifted = true;
            tw.Kill();
            var t2 = CreateTween().SetIgnoreTimeScale(true);
            t2.TweenMethod(Callable.From<float>(v => { SetDark(v); vm.SetShaderParameter("a", Mathf.Max(0, v) * 0.55f); }), k, -0.7f, 0.06);   // the strike lights it all up
            t2.TweenMethod(Callable.From<float>(v => { SetDark(v); vm.SetShaderParameter("a", Mathf.Max(0, v) * 0.55f); }), -0.7f, 0f, 0.7).SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
            t2.TweenCallback(Callable.From(() => { SetDark(0); vig.QueueFree(); }));
        };
    }

    const string VignetteCode = "shader_type canvas_item;\nuniform vec4 col : source_color; uniform float a = 0.0;\n" +
        "void fragment() { float d = length((UV - 0.5) * vec2(1.0, 0.8)); COLOR = vec4(col.rgb, a * smoothstep(0.3, 0.62, d)); }";

    sealed class GodCtx { public View V = null!; public St At; public Vector3 P; public View[] Tribs = null!; public Action Lift = () => { }; }

    // ---------------------------------------------------------------- lightning (the old bolts, improved)

    /// A bolt with its crack (the old LightningHit look, improved): the sound starts now and the bolt lands on the
    /// crack; a second, thinner bolt restrikes beside it a moment later (real lightning flickers), and a few short
    /// arcs fork off the top of the channel. Full speed.
    void Bolt(Vector3 at, Color c, float power, float flash, string sound = "SE_DUEL_LIVE_THUNDER", double onset = OnThunder)
    {
        Snd(sound);
        After(Math.Max(0, onset - 0.12), () =>
        {
            LightningHit(at, c, power, dim: false, flash: flash);
            After(0.12, () => Forks(at, c, power));
            After(0.21, () => LightningHit(at + new Vector3(R(-0.12, 0.12), 0, R(-0.08, 0.08)), c.Lerp(Colors.White, 0.4f), power * 0.45f, dim: false, flash: 0f));
        });
    }

    /// Short arcs splitting off a bolt's channel, high up, reaching out sideways and down.
    void Forks(Vector3 at, Color c, float power)
    {
        for (int i = 0; i < 3; i++)
        {
            float h = R(1.6, 3.6) * Mathf.Min(1.2f, 0.6f + power * 0.3f);
            var a = at + Vector3.Up * h; var side = Bill.X * (i % 2 == 0 ? 1 : -1);
            Jag(a, a + side * R(0.5, 1.1) + Vector3.Down * R(0.4, 1.0), c.Lerp(Colors.White, 0.3f), 4, 0.16, 0.4f * Mathf.Min(1.5f, power), 0.12f);
        }
    }

    /// A lightning line drawn from `a` to `b` in short jagged pieces, `steps` of them, each living `life`.
    void Jag(Vector3 a, Vector3 b, Color c, int steps, double life, float width = 0.5f, float jitter = 0.18f)
    {
        var prev = a;
        for (int i = 1; i <= steps; i++)
        {
            var nxt = a.Lerp(b, i / (float)steps) + (i < steps ? new Vector3(R(-1, 1), R(-1, 1), R(-1, 1)) * jitter : Vector3.Zero);
            Arc(prev, nxt, c, width, life); prev = nxt;
        }
    }

    // ---------------------------------------------------------------- the old storm cloud, improved

    /// The storm cloud (the old puffs, improved): more puffs in two tones, layered dark under pale, drifting slowly;
    /// returns a function that sets off lightning inside it (the puffs near it light up, a flash, arcs).
    Action StormCloud(Vector3 at, float width, double life)
    {
        for (int i = 0; i < 26; i++)
        {
            double d = i * 0.035;
            bool low = i % 3 == 0;
            var p = at + new Vector3(R(-width / 2, width / 2), low ? R(-0.35, -0.1) : R(-0.15, 0.3), R(-0.6, 0.6));
            var col = low ? new Color(0.22f, 0.24f, 0.32f, 0.9f) : new Color(0.46f, 0.5f, 0.62f, 0.85f);
            After(d, () => Puffs(p, 1, col, R(1.6, 2.4), 0.03f, 0.35f));
        }
        return () =>
        {
            var c = at + new Vector3(R(-width * 0.4, width * 0.4), R(-0.2, 0.2), R(-0.3, 0.3));
            Jag(c, c + new Vector3(R(-0.9, 0.9), R(-0.4, 0.1), 0), ObPale, 3, 0.12, 0.9f, 0.14f);
            Flash(c, ObBlue, 12f);
            FxQuad(CoreGlowFx, c, new Vector2(1.8f, 1.1f), 0.14, ObBlue, Bill);
        };
    }

    // ---------------------------------------------------------------- the whole-field shock wave

    /// The whole screen bends: a ring races out from the impact over the entire field, pushing the picture outward
    /// with a colour fringe on its edge; a pull toward the centre at the start.
    const string ScreenShockCode = @"shader_type canvas_item;
uniform sampler2D scr : hint_screen_texture, filter_linear_mipmap, repeat_disable;
uniform vec2 c = vec2(0.5); uniform float t = 0.0; uniform float amt = 1.0; uniform float aspect = 1.78;
void fragment() {
  vec2 uv = SCREEN_UV; vec2 d = uv - c; d.x *= aspect; float r = length(d); vec2 dir = d / max(r, 0.0001);
  float rad = 0.05 + 1.5 * (1.0 - pow(1.0 - t, 2.2));
  float w = 0.05 + 0.1 * t;
  float ring = exp(-pow((r - rad) / w, 2.0)) * (1.0 - t);
  float pull = smoothstep(0.25, 0.0, t) * smoothstep(0.45, 0.0, r) * 0.6;
  vec2 off = dir * (ring * 0.05 - pull * 0.03) * amt; off.x /= aspect;
  vec3 col;
  col.r = texture(scr, uv - off * 1.15).r;
  col.g = texture(scr, uv - off).g;
  col.b = texture(scr, uv - off * 0.85).b;
  COLOR = vec4(col + ring * 0.08 * amt, 1.0);
}";

    void ScreenShock(Vector3 at, double dur, float amt = 1f)
    {
        var vp = GetViewport().GetVisibleRect().Size;
        var sp = cam.UnprojectPosition(at) / vp;
        var m = Shade(ScreenShockCode, ("c", sp), ("amt", amt), ("aspect", vp.X / vp.Y), ("t", 0f));
        var r = new ColorRect { Size = new Vector2(W, H), Material = m, MouseFilter = MouseFilterEnum.Ignore };
        hud.AddChild(r);
        var tw = r.CreateTween();
        tw.TweenMethod(Callable.From<float>(k => m.SetShaderParameter("t", k)), 0f, 1f, dur).SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.Out);
        tw.TweenCallback(Callable.From(r.QueueFree));
    }

    // ---------------------------------------------------------------- the Gods

    /// One tribute taken by lightning: the card becomes a sheet of its own picture at once (the real card can move on
    /// unseen); at `delay` a bolt strikes it, then it is drawn up into the bolt, thinning and burning away.
    void TributeTaken(View t, Color c, Color pale, double delay)
    {
        var at = t.Root.GlobalPosition;
        var s = CardSheet(t); var x0 = s.Mi.GlobalTransform;
        After(delay, () =>
        {
            Bolt(at, c, 0.75f, 0.25f);
            After(OnThunder + 0.05, () =>
            {
                for (int i = 0; i < 5; i++) { double d = i * 0.05; After(d, () => { if (IsInstanceValid(s.Mi)) Crawl(s, pale); }); }
                var tw = s.Mi.CreateTween();
                tw.TweenMethod(Callable.From<float>(k =>
                {
                    var b = new Basis(Vector3.Right, Mathf.Lerp(0, Mathf.Pi / 2 * 0.9f, Mathf.Min(1, k * 3))) * x0.Basis;
                    s.Mi.GlobalTransform = new Transform3D(b, x0.Origin + Vector3.Up * (0.3f * k + 2.4f * k * k)).ScaledLocal(new Vector3(1 - 0.75f * k, 1, 1 + 0.6f * k));
                }), 0f, 1f, 0.5).SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.In);
                BurnAway(s, pale, 0.5, new Vector2(0.5f, 1f), 0, 0.6f, 7f);
            });
        });
    }

    /// The opening, the same for all three: the tributes struck one by one and taken by the lightning; the table
    /// darkens smoothly (held until the big strike).
    async Task<GodCtx> GodOpening(string name, Color col, Color pale)
    {
        var g = new GodCtx { Tribs = new[] { MineAt("Celtic Guardian", 1), MineAt("Feral Imp", 2), MineAt("Curse of Dragon", 3) } };
        await Wait(0.4);
        g.V = views.Values.FirstOrDefault(x => x.Card.Owner == 0 && x.Card.Name == name) ?? Of(0, "Obelisk the Tormentor");
        g.At = Mon(g.V, 2, 0); g.P = Grid3(g.At);
        for (int i = 0; i < 3; i++) TributeTaken(g.Tribs[i], col, pale, i * 0.4);
        await Wait(3 * 0.4 + 0.4);
        g.Lift = DarkHold(col, 1.0);
        await Wait(0.5);
        return g;
    }

    /// Thunder round the zone in the dark: `n` bolts, each with its crack on its flash.
    async Task SmallThunder(GodCtx g, Color c, int n = 3, double gap = 0.32)
    {
        for (int i = 0; i < n; i++)
        {
            float a = R(0, 6.28); var q = g.P + new Vector3(Mathf.Cos(a) * R(0.9, 1.5), 0, Mathf.Sin(a) * R(0.7, 1.2));
            Bolt(q, c, 0.6f, 0.3f);
            await Wait(gap);
        }
    }


    // The slow motion happens DURING the shock wave (as in the early samples): the great bolt lands at full speed,
    // then time drops to Slow while the wave crosses the whole field and the card burns in, then snaps back.
    // ShockT = how long (duel time) the wave takes to cross; ShockT / Slow is how long it is watched in real time.
    const double Slow = 0.25, ShockT = 0.22;

    /// The great strike. At full speed: the crack and the deep boom, the bolt landing on the crack (with its forks and
    /// restrike). On the hit: the dark lifts in a flash of the strike's light and time slows; slowed down, the whole
    /// screen bends in a shock wave racing out over the field, rings race across the table, every card jolts as the
    /// wave reaches it, dust rolls out, the camera is pushed back; the card burns in from the strike; time snaps back
    /// with a heavy shake as the wave leaves the field.
    async Task BigStrike(GodCtx g, Color bolt, Color col, Color pale)
    {
        Snd("SE_SOLO_KAMINARI_01");
        Snd("SE_ATTACK_A_DIVINE_SPECIAL_02", OnKaminari - OnDivine2);
        await Wait(OnKaminari - 0.12);
        LightningHit(g.P, bolt, 2.6f, dim: false, flash: 0f);
        await Wait(0.12);
        // the hit
        g.Lift();
        ScreenFlash(Colors.White, 0.5f, 0.05);
        SlowMo(Slow, 1.5);
        After(0.02, () => Forks(g.P, bolt, 2.4f));
        After(0.03, () => LightningHit(g.P + new Vector3(R(-0.15, 0.15), 0, 0), Colors.White, 1.2f, dim: false, flash: 0f));
        CreateTween().SetIgnoreTimeScale(true).TweenProperty(cam, "position", camHome + (camHome - g.P).Normalized() * 0.3f, 0.5).SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
        ScreenShock(g.P, ShockT, 1.3f);
        Warp(g.P + Vector3.Up * 0.05f, 12f, ShockT, 3.2f);
        ShockRing(g.P, Colors.White, 0.1f, 10f, ShockT * 0.85);
        ShockRing(g.P, col, 0.3f, 8f, ShockT);
        TableJolt(g.P, 5.5f / (float)ShockT);
        for (int i = 0; i < 18; i++) { float a = i * Mathf.Tau / 18; var q = g.P + new Vector3(Mathf.Cos(a) * 1.2f, 0.05f, Mathf.Sin(a) * 1.0f); Puffs(q, 1, new Color(0.45f, 0.45f, 0.5f, 0.6f), 1.1f, 0.4f, 0.12f); }
        Streaks(g.P + Vector3.Up * 0.1f, pale, 36, 5f);
        FxQuad(GroundGlowFx, g.P with { Y = 0.02f }, new Vector2(3.4f, 3.4f), ShockT + 0.2, col);
        var build = BuildIn(g.V, g.At, pale, ShockT * 0.6, new Vector2(0.5f, 0.5f), 8f, (s, k) => { if (GD.Randf() < 0.6f) Crawl(s, pale); });
        await Wait(ShockT);
        await build;
        if (OS.IsDebugBuild()) GD.Print($"slowmo: card placed at {Time.GetTicksMsec() - slowT0} ms");
        // time comes back with a jolt
        SlowEnd(0.1);
        CreateTween().SetIgnoreTimeScale(true).TweenProperty(cam, "position", camHome, 0.3).SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
        After(0.05, () => Shake(0.4f));
        Snd("SE_LAND_ADVANCE_HIGH");
        Cue("land");
        await Wait(0.6);
    }

    /// A ring racing out over the table from `at`: from `r0` to `r1` across, thinning as it goes.
    void ShockRing(Vector3 at, Color c, float r0, float r1, double dur)
    {
        var q = FxQuad(RingFx, at with { Y = 0.05f }, new Vector2(1, 1), dur, c);
        q.Scale = Vector3.One * r0;
        q.CreateTween().TweenProperty(q, "scale", Vector3.One * r1, dur).SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
    }

    /// Every card on the table hops as the shock wave reaches it (`speed` units a duel second out from `at`).
    void TableJolt(Vector3 at, float speed)
    {
        foreach (var v in views.Values)
        {
            if (!v.Root.Visible || v.S.Loc is Loc.None or Loc.Hand || (v.Tw != null && v.Tw.IsRunning())) continue;
            var home = v.Root.Position; float dist = v.Root.GlobalPosition.DistanceTo(at);
            float h = Mathf.Clamp(0.22f - dist * 0.03f, 0.04f, 0.22f);
            var tw = v.Root.CreateTween(); tw.TweenInterval(dist / speed);
            tw.TweenProperty(v.Root, "position", home + Vector3.Up * h, 0.03).SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.Out);
            tw.TweenProperty(v.Root, "position", home, 0.1).SetTrans(Tween.TransitionType.Bounce).SetEase(Tween.EaseType.Out);
        }
    }

    static readonly Color ObBlue = new(0.35f, 0.6f, 1f), ObPale = new(0.75f, 0.88f, 1f);
    static readonly Color SlRed = new(1f, 0.28f, 0.2f), SlPale = new(1f, 0.7f, 0.6f);
    static readonly Color RaGold = new(1f, 0.78f, 0.3f), RaPale = new(1f, 0.93f, 0.68f);

    /// LOCKED 2026-11-13. Obelisk (A): the storm cloud gathers over their side as the table darkens, lightning going off inside it;
    /// thunder round the zone; the cloud flashes faster and faster; the great blue bolt.
    async Task ObeliskFinal(int style) => await ObeliskBody(await GodOpening("Obelisk the Tormentor", ObBlue, ObPale));

    async Task ObeliskBody(GodCtx g)
    {
        var inside = StormCloud(g.P + new Vector3(0, 1.7f, -0.35f), 2.8f, 4);
        Snd("SE_SOLO_GATE078_ELECTRIC", 0.3);
        for (int i = 0; i < 3; i++) After(0.35 + i * 0.2, inside);
        await Wait(0.9);
        await SmallThunder(g, ObBlue);
        for (int i = 0; i < 5; i++) After(i * 0.06, inside);
        await Wait(0.32);
        await BigStrike(g, ObPale, ObBlue, ObPale);
    }

    /// LOCKED 2026-11-13. Slifer (D): red rain: bolts fall round the zone faster and faster, each with its crack; the great red bolt.
    async Task SliferFinal(int style) => await SliferBody(await GodOpening("Slifer the Sky Dragon", SlRed, SlPale));

    async Task SliferBody(GodCtx g)
    {
        double t = 0;
        for (int i = 0; i < 9; i++) { float a = R(0, 6.28); var q = g.P + new Vector3(Mathf.Cos(a) * R(0.8, 1.6), 0, Mathf.Sin(a) * R(0.7, 1.3)); var c = i % 2 == 0 ? SlRed : SlPale; double d = t; After(d, () => Bolt(q, c, 0.5f, 0.2f)); t += 0.24 - i * 0.02; }
        await Wait(t + 0.2);
        await BigStrike(g, SlPale, SlRed, SlPale);
    }

    /// LOCKED 2026-11-13. Ra (A): the gold sun forms high over the zone, gold lightning crawling over it; gold thunder; the sun swells
    /// and fires its great bolt.
    async Task RaFinal(int style) => await RaBody(await GodOpening("The Winged Dragon of Ra", RaGold, RaPale));

    async Task RaBody(GodCtx g)
    {
        var at = g.P + new Vector3(0, 1.9f, -0.6f);
        var sun = FxQuad(OrbFx, at, new Vector2(0.4f, 0.4f), 3.6, RaGold, Bill, false, null, ("amt", 1.6f));
        sun.CreateTween().TweenProperty(sun, "scale", Vector3.One * 2.6f, 0.9).SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.Out);
        Snd("SE_SOLO_S_LIGHT");
        for (int i = 0; i < 6; i++)
        {
            double d = 0.35 + i * 0.12;
            After(d, () => { float a = R(0, 6.28); var c = at + Bill * new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0) * 0.55f; Jag(c, c + Bill * new Vector3(Mathf.Cos(a + 1.2f), Mathf.Sin(a + 1.2f), 0) * 0.5f, RaPale, 3, 0.14, 0.5f, 0.08f); });
        }
        await Wait(0.8);
        await SmallThunder(g, RaGold);
        // it swells, then fires
        sun.CreateTween().TweenProperty(sun, "scale", Vector3.One * 3.8f, 0.3).SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.In);
        for (int i = 0; i < 4; i++) { double d = i * 0.06; After(d, () => { float a = R(0, 6.28); var c = at + Bill * new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0) * 0.7f; Jag(c, c + Bill * new Vector3(Mathf.Cos(a + 1.2f), Mathf.Sin(a + 1.2f), 0) * 0.6f, RaPale, 3, 0.1, 0.6f, 0.08f); }); }
        await Wait(0.3);
        Jag(at, g.P + Vector3.Up * 0.3f, RaPale, 8, 0.3, 1.0f, 0.2f);
        sun.CreateTween().TweenProperty(sun, "scale", Vector3.One * 0.1f, 0.15);
        await BigStrike(g, RaPale, RaGold, RaPale);
    }
}
