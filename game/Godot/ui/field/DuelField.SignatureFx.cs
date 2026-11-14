using Godot;
using PoC.Engine;

namespace PoC.Game.UI;

/// The locked signature effects of FX Lab rounds 4 to 7 (2026-11-13), on real cards: each core takes the cards it
/// acts on, so the lab (its staging, then the core) and real duels (DuelField.Signature.cs hooks) play the same thing.
/// The lab originals they came from: Swords cage, MST cyclone, Dark Hole (lighter dim), Raigeki (Batch1); Banish C
/// phase-out, Pot of Greed B, Brain Control A, Time Wizard C, Chaos summon A (Batch2); Heavy Storm, Trap Hole, Ring of
/// Destruction, Magic Cylinder, Blue-Eyes, Jinzo A (Batch3); Negate Attack B, Monster Reborn A, Torrential Tribute B,
/// Red-Eyes (Batch4); Mirror Force A (Batch5). In duels the replay itself shows LP changes, card moves and flips.
public partial class DuelField
{
    // ---------------------------------------------------------------- Swords of Revealing Light: the cage

    /// Seven swords land on an arc round `side`'s monster row, a chain of light runs between them, bars rise: a cage.
    async Task SwordsOn(int side)
    {
        float rowZ = ZonePos(Loc.Monster, 2, side).Z, outward = side == 1 ? 1 : -1;
        var gold = new Color(1f, 0.88f, 0.6f);
        var pts = new List<Vector3>();
        for (int i = 0; i < 7; i++)
        {
            float a = Mathf.Pi * (0.06f + i * 0.88f / 6f);
            pts.Add(new Vector3(Mathf.Cos(a) * 2.95f, 0, rowZ - 0.1f * outward + Mathf.Sin(a) * 1.05f * outward));
        }
        for (int i = 0; i < 7; i++) SwordDrop(pts[i], 1.45f, i * 0.09, 1.9 - i * 0.09, pts[i].X * -0.04f, 0.65f);
        await Wait(0.75);
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
        for (int i = 0; i + 1 < pts.Count; i++)
            for (int b = 1; b <= 2; b++)
            {
                var at = pts[i].Lerp(pts[i + 1], b / 3f);
                double d = (i * 2 + b) * 0.025;
                After(d, () => FxStand(PillarFx, at, new Vector2(0.14f, 1.0f), 0.95, gold, false, null, ("amt", 0.55f)));
            }
        await Wait(0.3);
        foreach (var v in views.Values.Where(v => v.Root.Visible && v.S is { Loc: Loc.Monster } s && s.Side == side))
            FxQuad(GroundGlowFx, v.Root.GlobalPosition with { Y = 0.02f }, new Vector2(1.1f, 1.1f), 0.6, gold);
        await Wait(1.0);
    }

    // ---------------------------------------------------------------- Mystical Space Typhoon: the tight cyclone

    async Task MstOn(View target)
    {
        var at = target.Root.GlobalPosition with { Y = 0 }; var wind = new Color(0.75f, 0.92f, 1f); var dust = new Color(0.62f, 0.72f, 0.8f, 0.55f);
        Snd("SE_EV_CYCLONE");   // with the twister (no longer at activation)
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
        Flakes(s, 46, uv => { var p = s.World(uv) - at; var flat = p with { Y = 0 }; var tan = Vector3.Up.Cross(flat).Normalized(); return tan * R(1.2, 2.0) + Vector3.Up * R(1.4, 2.4) + flat.Normalized() * 0.3f; }, 0.85, 0.25, wind);
        BurnAway(s, new Color(0.85f, 0.95f, 1f), 0.3, null, 0, 0.3f, 2f, 1.2f);
        Snd("SE_CARD_ST_BREAK");
        await Wait(0.9);
    }

    // ---------------------------------------------------------------- Dark Hole: the black hole

    async Task DarkHoleOn(List<View> all)
    {
        var deep = new Color(0.5f, 0.28f, 1f); var hot = new Color(1f, 0.8f, 0.95f);
        var c = new Vector3(0, 1.35f, 0);
        Snd("SE_EV_BLACKHOLE");   // as the hole opens (no longer at activation)
        FieldLines(0f, 0.5);
        ScreenDim(0.2f, 0.5, 1.6, 0.6);
        Warp(c, 6.5f, 2.5, 1.2f, lens: true, Bill);
        FxQuad(VortexFx, c, new Vector2(3.0f, 3.0f), 2.5, deep * new Color(0.55f, 0.55f, 0.55f), Bill * new Basis(Vector3.Right, 1.15f), false, null, ("col2", hot), ("amt", 3.5f));
        DarkBall(c, 0.42f, hot, 2.5);
        for (int i = 0; i < 30; i++) { float ang = i * 0.9f, rad = 2.6f + (i % 4) * 0.5f; var a = new Vector3(Mathf.Cos(ang) * rad, 0.1f, Mathf.Sin(ang) * rad * 0.7f); var col = i % 3 == 0 ? Colors.White : hot; After(0.15 + i * 0.045, () => SwirlMote(a, c, col, 0.9, 0.9f, 1.3f, 0.06f)); }
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
        Snd("SE_ATTACK_A_DARK_SPECIAL_02");   // the collapse
        Warp(c, 6f, 0.45, 1.4f, false, Bill); Flash(c, hot, 6f); FxQuad(CoreGlowFx, c, new Vector2(2.6f, 2.6f), 0.4, hot, Bill); ScreenFlash(hot, 0.3f, 0.3);
        Shake(0.12f); HitStop(0.07);
        Streaks(c, hot, 22, 4.5f, 0.5f, Vector3.Zero, 1.5f);
        FieldLines(1f, 0.8);
        await Wait(0.8);
    }

    // ---------------------------------------------------------------- Raigeki: a bolt on each

    async Task RaigekiOn(List<View> foes)
    {
        foes = foes.OrderBy(v => v.Root.GlobalPosition.X).ToList();
        var volt = new Color(1f, 0.55f, 0.16f);   // the card art: white-hot bolts with orange edges on deep blue
        var deep = new Color(0.16f, 0.2f, 0.75f);
        Vignette(new Color(0.03f, 0.04f, 0.2f), 2.2);
        Snd("SE_EV_RAIGEKI");
        var sheets = foes.Select(CardSheet).ToList();   // every card becomes its sheet now (the real ones can move on unseen)
        for (int i = 0; i < foes.Count; i++)
        {
            var v = foes[i]; var p = v.Root.GlobalPosition with { Y = 0 }; var s = sheets[i];
            double lead = LightningHit(p, volt, 1f, dim: false, stop: false);
            After(lead, () =>
            {
                s.Set("glow_col", Colors.White); Anim(s.M, "glow", 2.6f, 0f, 0.35);
                Anim(s.M, "darken", 0f, 0.45f, 0.3);
                FxQuad(GroundGlowFx, p with { Y = 0.02f }, new Vector2(2.2f, 2.2f), 0.6, deep);
                BurnAway(s, new Color(1f, 0.62f, 0.2f), 0.7, new Vector2(R(0.4, 0.6), R(0.4, 0.6)), 0.12, 1.2f, 6f, 0.9f);
            });
            Snd("SE_DUEL_LIVE_THUNDER", Math.Max(0, lead - OnThunder));   // each bolt's crack on its flash
            await Wait(0.38);   // clearly one after another
        }
        await Wait(1.0);
    }

    // ---------------------------------------------------------------- Heavy Storm: a twister along each back row

    async Task HeavyStormOn(List<View> rows)
    {
        var wind = new Color(0.72f, 0.82f, 1f); var dust = new Color(0.5f, 0.56f, 0.66f, 0.6f);
        Vignette(new Color(0.02f, 0.03f, 0.07f), 3.2);
        Snd("SE_EV_HEAVY_STORM"); Snd("C_GALE", 0.3); Snd("SE_ATTACK_A_WIND_SPECIAL_01", 0.5);
        const double form = 0.5, run = 1.6, end = 0.6;
        float x0 = Mathf.Abs(ZonePos(Loc.Spell, 4, 0).X) + CW * 0.6f;   // just past the outer Spell & Trap zones: never over the Deck or the side zones
        var twisters = new Twist?[2];
        foreach (int side in rows.Select(v => v.S.Side).Distinct())
        {
            float z = ZonePos(Loc.Spell, 2, side).Z, dir = side == 0 ? 1 : -1;
            var tw = Twister(new Vector3(-x0 * dir, 0, z), side == 0 ? 2.3f : 1.7f, side == 0 ? 0.7f : 0.58f, wind, dust);
            TwistLife(tw, form, run, end, dust);
            tw.Root.CreateTween().TweenProperty(tw.Root, "position:x", x0 * dir, run).SetDelay(form * 0.8).SetTrans(Tween.TransitionType.Linear);
            twisters[side] = tw;
        }
        foreach (var v in rows)
        {
            int side = v.S.Side; float dir = side == 0 ? 1 : -1; float x = v.Root.GlobalPosition.X;
            var tw = twisters[side]!;
            float h = side == 0 ? 2.3f : 1.7f;
            var s = CardSheet(v); var from = s.Mi.GlobalTransform;
            double when = form * 0.8 + (x * dir + x0) / (2 * x0) * run - 0.35;   // a little before the funnel arrives: it starts pulling
            After(when, () =>
            {
                if (!IsInstanceValid(tw.Root)) return;
                Anim(s.M, "flutter", 0f, 1.3f, 0.2);
                float spin0 = R(0, 6.28f);
                s.Mi.CreateTween().TweenMethod(Callable.From<float>(k =>
                {
                    if (!IsInstanceValid(tw.Root)) return;
                    var axis = tw.Root.GlobalPosition;
                    float pullIn = Mathf.Min(1, k / 0.3f), up = Mathf.Max(0, (k - 0.25f) / 0.75f);
                    float rad = Mathf.Lerp(0.15f, 0.5f, up), a = spin0 + up * up * 14f * dir;
                    var orbit = axis + new Vector3(Mathf.Cos(a) * rad, 0.12f + up * h * 0.75f, Mathf.Sin(a) * rad * 0.8f);
                    var pos = from.Origin.Lerp(orbit, Ease(pullIn));
                    s.Mi.GlobalTransform = new Transform3D(new Basis(Vector3.Up, a * 1.3f) * new Basis(Vector3.Right, -1.2f * pullIn) * from.Basis, pos).ScaledLocal(Vector3.One * (1 - 0.45f * up));
                }), 0f, 1f, 0.95);
                After(0.8, () =>
                {
                    Snd("SE_CARD_ST_BREAK");
                    Flakes(s, 30, uv => { var c = s.World(uv) - tw.Root.GlobalPosition; var flat = c with { Y = 0 }; return Vector3.Up.Cross(flat).Normalized() * R(1.2, 2.0) * dir + Vector3.Up * R(1.0, 1.8); }, 0.7, 0.15, wind);
                    BurnAway(s, new Color(0.85f, 0.92f, 1f), 0.2, null, 0, 0.2f, 1.5f, 1f);
                });
            });
        }
        await Wait(form + run + end + 0.4);
    }

    // ---------------------------------------------------------------- Trap Hole: the ground gives way

    async Task TrapHoleOn(View m)
    {
        var p = m.Root.GlobalPosition with { Y = 0.004f };
        var zone = new Vector2(CW * 1.45f, CH * 1.25f);
        var s = CardSheet(m); var from = s.Mi.Transform;
        var pit = ZoneQuad(PitOpenCode, p, zone * 1.08f, ("col", new Color(0.55f, 0.38f, 0.24f)));
        var pm = (ShaderMaterial)pit.MaterialOverride;
        Anim(pm, "open", 0f, 0.95f, 0.12, 0, Tween.TransitionType.Cubic, Tween.EaseType.Out);
        Snd("SE_CARD_FINISH_BREAK_03"); Shake(0.1f); HitStop(0.03);
        for (int i = 0; i < 10; i++) { float a = i * Mathf.Tau / 10; var rim = p + new Vector3(Mathf.Cos(a) * zone.X * 0.5f, 0.02f, Mathf.Sin(a) * zone.Y * 0.5f); After(R(0, 0.05), () => Puffs(rim, 1, new Color(0.42f, 0.36f, 0.3f, 0.7f), 0.65f, 0.5f, 0.06f)); }
        Streaks(p + Vector3.Up * 0.05f, new Color(0.62f, 0.52f, 0.4f), 18, 2.2f, 7f, Vector3.Up, 0.7f);
        s.Mi.CreateTween().TweenMethod(Callable.From<float>(k =>
        {
            float f = Mathf.Max(0, k - 0.12f) / 0.88f, e = f * f;
            s.Mi.Transform = new Transform3D(new Basis(Vector3.Right, e * 0.9f) * new Basis(Vector3.Up, e * 0.5f) * from.Basis, from.Origin).ScaledLocal(Vector3.One * (1 - 0.85f * e));
            s.Set("darken", e);
        }), 0f, 1f, 0.42).SetDelay(0.04);
        await Wait(0.47);
        s.Mi.QueueFree();
        Puffs(p, 3, new Color(0.38f, 0.33f, 0.28f, 0.5f), 0.6f, 0.35f, 0.2f);
        Anim(pm, "open", 0.95f, 0f, 0.25, 0.1, Tween.TransitionType.Cubic, Tween.EaseType.In);
        After(0.4, () => { if (IsInstanceValid(pit)) pit.QueueFree(); });
        await Wait(0.5);
    }

    // ---------------------------------------------------------------- Ring of Destruction: the ring and the blast

    /// The ring drops onto the monster and tightens, its lights run faster and faster, then the blast. `lp` (lab
    /// only): show both players' LP drop by its ATK (in duels the replay does).
    async Task RingOn(View t, bool lp = false)
    {
        var p = t.Root.GlobalPosition; int atk = t.S.Atk > 0 ? t.S.Atk : t.Card.Data.Atk;
        var (ring, lights, _) = RingProp(p + Vector3.Up * 2.4f);
        var red = new Color(1f, 0.15f, 0.1f);
        void Light(int i, bool on) => lights[i].AlbedoColor = on ? new Color(red.R * 3f, red.G * 3f, red.B * 3f) : new Color(red.R * 0.4f, red.G * 0.4f, red.B * 0.4f);
        var tw = ring.CreateTween().SetParallel();
        tw.TweenProperty(ring, "position:y", p.Y + 0.16f, 0.4).SetTrans(Tween.TransitionType.Bounce).SetEase(Tween.EaseType.Out);
        tw.TweenProperty(ring, "rotation:y", 2.2f, 0.4);
        await Wait(0.42);
        Cue("land"); Shake(0.04f); Streaks(p + Vector3.Up * 0.15f, new Color(1f, 0.8f, 0.5f), 8, 1.6f);
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
        if (lp) { LpHit(0, 8000, 8000 - atk); LpHit(1, 8000, 8000 - atk); }
        Vignette(new Color(0.5f, 0.08f, 0.02f), 0.8);
        await Wait(0.9);
    }

    // ---------------------------------------------------------------- Torrential Tribute: two tides

    async Task TorrentialOn(List<View> all)
    {
        var water = new Color(0.3f, 0.6f, 0.95f); var foam = new Color(0.88f, 0.96f, 1f);
        var sheets = all.ToDictionary(v => v, CardSheet);
        FxQuad(TideFx, new Vector3(0, 0.08f, 1.25f), new Vector2(7.4f, 2.5f), 0.9, water, Flat, true);
        FxQuad(TideFx, new Vector3(0, 0.08f, -1.25f), new Vector2(7.4f, 2.5f), 0.9, water, new Basis(Vector3.Up, Mathf.Pi) * Flat, true);
        Snd("SE_SOLO_WIND_01");
        await Wait(0.7);
        var mid = new Vector3(0, 0.1f, 0);
        for (int i = 0; i < 16; i++) Puffs(mid + new Vector3(R(-3.2, 3.2), 0, R(-0.4, 0.4)), 1, new Color(0.88f, 0.95f, 1f, 0.55f), 1.2f, 1.1f, 0.25f);   // mist where they meet
        Warp(mid, 6f, 0.5, 1.4f); Shake(0.12f); Snd("SE_EV_CONVULSION_OF_NATURE_b");
        foreach (var (v, s) in sheets)
        {
            var from = s.Mi.Transform; var push = new Vector3(0, 0.1f, -Mathf.Sign(from.Origin.Z) * 0.3f);
            s.Set("glow_col", water); Anim(s.M, "glow", 0f, 0.4f, 0.2); Anim(s.M, "darken", 0f, 0.5f, 0.4);
            s.Mi.CreateTween().TweenMethod(Callable.From<float>(k => s.Mi.Transform = new Transform3D(new Basis(Vector3.Right, -k * 0.8f) * from.Basis, from.Origin + push * k)), 0f, 1f, 0.45);
            Puffs(from.Origin + Vector3.Up * 0.15f, 3, new Color(0.85f, 0.93f, 1f, 0.5f), 0.7f, 0.5f, 0.2f);
            BurnAway(s, foam, 0.35, null, 0.15, 0.3f, 2f);
        }
        await Wait(1.0);
    }

    // ---------------------------------------------------------------- Mirror Force: the prism barrier

    /// The curved prism mirror rises in front of `trap`'s side, the attack (from `attacker`) hits it, and a lance of
    /// light goes back into each of `atk`, burning it.
    async Task MirrorForceOn(View trap, View? attacker, List<View> atk)
    {
        int side = trap.S.Side; float rowZ = ZonePos(Loc.Monster, 2, side).Z, toward = side == 0 ? -1 : 1;
        var silver = new Color(0.78f, 0.9f, 1f); var fire = new Color(1f, 0.6f, 0.3f);
        var hitAt = new Vector3(0, 0.5f, rowZ + toward * CH * 0.9f);
        var m = Shade(PrismMirrorCode, ("col", silver), ("noise", FxNoise), ("amt", 0f));
        var wall = new MeshInstance3D { Mesh = new CylinderMesh { TopRadius = 1, BottomRadius = 1, Height = 1, RadialSegments = 64, Rings = 1, CapTop = false, CapBottom = false }, MaterialOverride = m, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            Position = new Vector3(0, 0.5f, rowZ - toward * 0.5f), Scale = new Vector3(3.0f, 1.0f, 1.35f), Rotation = new Vector3(0, side == 0 ? 0 : Mathf.Pi, 0) };
        world.AddChild(wall);
        Anim(m, "amt", 0f, 1f, 0.3, 0, Tween.TransitionType.Cubic, Tween.EaseType.Out);
        var sheets = atk.ToDictionary(v => v, v => (Pos: v.Root.GlobalPosition, S: CardSheet(v)));
        if (attacker != null) Orb(Line((sheets.TryGetValue(attacker, out var aa) ? aa.Pos : attacker.Root.GlobalPosition) + Vector3.Up * 0.2f, hitAt, 0.2f), fire, 0.32, 0.7f);
        await Wait(0.33);
        Anim(m, "flare", 1f, 0f, 0.6, 0, Tween.TransitionType.Expo, Tween.EaseType.Out);
        Warp(hitAt, 2.6f, 0.45, 1.6f, false, Bill); Flash(hitAt, silver, 10f); ScreenFlash(silver, 0.3f, 0.25); Shake(0.1f); HitStop(0.06);
        await Wait(0.08);
        foreach (var (_, (pos, s)) in sheets)
        {
            var to = pos + Vector3.Up * 0.1f;
            Orb(Line(hitAt, to, 0.1f), Colors.White, 0.16, 0.5f);
            Beam(hitAt, to, 0.35f, 0.7, silver);
            After(0.16, () => { s.Set("edge_col", new Color(0.75f, 0.9f, 1f)); BurnAway(s, new Color(0.75f, 0.9f, 1f), 0.55, new Vector2(0.5f, 0.9f), 0, 1f, 6f); Flash(to, silver, 6f); Streaks(to, Colors.White, 10, 2.2f); });
        }
        Snd("SE_CARD_FINISH_BREAK_02", 0.15);
        Anim(m, "amt", 1f, 0f, 0.4, 0.6);
        After(1.1, () => wall.QueueFree());
        await Wait(1.0);
    }

    // ---------------------------------------------------------------- Negate Attack: the rewind

    /// The attacker's charge runs backward (afterimages) and it is home again; `side`'s monsters dim for a moment: no
    /// more attacks. `target` = what it was charging at (null: straight ahead).
    async Task NegateOn(View attacker, Vector3? target, int side)
    {
        var blue = new Color(0.55f, 0.75f, 1f);
        var home = attacker.Root.GlobalTransform;
        Charge(attacker, target ?? attacker.Root.GlobalPosition + new Vector3(0, 0, side == 1 ? 1.5f : -1.5f), 0.3, 0.3f);
        await Wait(0.3);
        await NegateRewind(attacker, home, blue);
        foreach (var f in views.Values.Where(v => v.Root.Visible && v.S is { Loc: Loc.Monster } s && s.Side == side))
        { f.Face.AlbedoColor = new Color(0.6f, 0.65f, 0.75f); var ff = f; After(0.7, () => ff.Face.AlbedoColor = Colors.White); }
        ScreenFlash(blue, 0.15f, 0.4);
        await Wait(0.6);
        Place(attacker, attacker.S);
    }

    // ---------------------------------------------------------------- Monster Reborn: particles

    /// The monster comes apart into particles from the bottom up where it lies (its Graveyard), they stream to `at`,
    /// and it builds back from the top down there.
    async Task RebornOn(View dead, St at)
    {
        var gy = dead.Root.GlobalPosition; var zone = Grid3(at);
        var gold = new Color(1f, 0.9f, 0.65f);
        var ghost = NewSheet(CardArt.Small(dead.Card.Data), dead.Root.GlobalTransform);
        dead.Root.Visible = false;
        BurnAway(ghost, gold, 0.75, new Vector2(0.5f, 1.05f), 0, 0.4f, 3f);
        Flakes(ghost, 40, uv => new Vector3(R(-0.15, 0.15), R(0.6, 1.1), R(-0.15, 0.15)), 0.9, 0.7, gold, 0.07f, -0.2f);
        for (int i = 0; i < 36; i++) { double d = 0.35 + i * 0.022; After(d, () => Mote(gy + new Vector3(R(-0.25, 0.25), 0.5f, R(-0.3, 0.3)), zone + new Vector3(R(-0.25, 0.25), 0.25f, R(-0.35, 0.35)), i % 3 == 0 ? Colors.White : gold, 0.6, R(0.05, 0.09), 0.7f)); }
        await Wait(1.0);
        await BuildIn(dead, at, gold, 0.75, new Vector2(0.5f, -0.05f), 3.5f, null, 1.2f);
        FxQuad(GroundGlowFx, zone with { Y = 0.02f }, new Vector2(1.3f, 1.3f), 0.5, gold);
        Cue("land");
        await Wait(0.4);
    }

    // ---------------------------------------------------------------- Pot of Greed: into the Deck

    /// Two green orbs leave the card for `side`'s Deck (the draws follow).
    async Task PotOn(View card, int side)
    {
        var p = card.Root.GlobalPosition; var green = new Color(0.45f, 1f, 0.55f);
        var deck = Grid3(DeckOf(side)) + Vector3.Up * 0.2f;
        for (int i = 0; i < 2; i++) Orb(Line(p + Vector3.Up * 0.2f, deck + Vector3.Up * 0.05f * i, 0.6f + i * 0.3f), green, 0.4 + i * 0.08, 0.45f, () => FxQuad(RingFx, deck with { Y = 0.05f }, new Vector2(1f, 1f), 0.35, green));
        await Wait(0.5);
    }

    // ---------------------------------------------------------------- Brain Control: violet orbs

    /// Violet orbs fly from the card into the monster, which takes on a violet glow (BrainControlEnd clears it).
    async Task BrainControlOn(View card, View t)
    {
        var p = t.Root.GlobalPosition; var violet = new Color(0.75f, 0.5f, 1f);
        var from = card.Root.GlobalPosition;
        for (int i = 0; i < 6; i++)
        {
            double d = i * 0.12;
            After(d, () => Orb(Line(from + Vector3.Up * 0.2f, p + Vector3.Up * 0.1f, 0.6f), violet, 0.45, 0.35f, () => FxQuad(RingFx, p with { Y = 0.03f }, new Vector2(1.3f, 1.3f), 0.4, violet)));
        }
        await Wait(0.9);
        t.Face.EmissionEnabled = true; t.Face.Emission = violet * 0.35f;
    }

    async Task BrainControlEnd(View t)
    {
        await Wait(0.62);
        t.Face.Emission = Colors.Black;
        FxQuad(GroundGlowFx, t.Root.GlobalPosition with { Y = 0.02f }, new Vector2(1.3f, 1.3f), 0.6, new Color(0.75f, 0.5f, 1f)); Cue("land");
    }

    // ---------------------------------------------------------------- Magic Cylinder: twin cylinders

    /// One cylinder opens in front of the defender (`guard`, toward the attacker) and swallows the attack; the other
    /// appears beside the attacker and fires it at `hitSide`'s LP. `lp` (lab only): show the LP drop.
    async Task CylinderOn(View attacker, Vector3 guard, int hitSide, int lp = -1)
    {
        var ap = attacker.Root.GlobalPosition; var fire = new Color(1f, 0.55f, 0.25f);
        var toward = (ap - guard) with { Y = 0 };
        var mouth = guard + toward.Normalized() * 0.55f + Vector3.Up * 0.45f;
        var them = hitSide == 1 ? new Vector3(2.9f, 0.5f, -2.8f) : new Vector3(-2.9f, 0.5f, 2.8f);   // the LP corner
        var c2At = ap + new Vector3(0.95f, 0.5f, 0.1f);
        var c1 = Cylinder2(mouth, -toward, 0.85f);
        var c2 = Cylinder2(c2At, them - c2At, 0.85f);
        await Wait(0.35);
        var lip = mouth + toward.Normalized() * 0.4f;
        Orb(Line(ap + Vector3.Up * 0.2f, lip, 0.1f), fire, 0.35, 0.7f, () => { FxQuad(CoreGlowFx, lip, new Vector2(0.8f, 0.8f), 0.2, fire, Bill); Snd("SE_CARD_ST_BREAK"); });
        await Wait(0.6);
        var muzzle = c2At + (them - c2At).Normalized() * 0.42f;
        FxQuad(CoreGlowFx, muzzle, new Vector2(0.8f, 0.8f), 0.25, fire, Bill);
        Orb(Line(muzzle, them, 0.15f), fire, 0.28, 0.9f, () => { Explosion(them with { Y = 0.25f }, 0.45f); if (lp >= 0) LpHit(hitSide, 8000, 8000 - lp); });
        await Wait(0.45);
        foreach (var c in new[] { c1, c2 }) { var tw = c.CreateTween(); tw.TweenProperty(c, "scale", Vector3.One * 0.01f, 0.25).SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.In); tw.TweenCallback(Callable.From(c.QueueFree)); }
    }

    // ---------------------------------------------------------------- summons: Blue-Eyes, Red-Eyes, Chaos, Jinzo

    /// White (Blue-Eyes) or red (Red-Eyes) lightning rings the zone, then the card builds up with lightning crawling
    /// over it.
    async Task DragonOn(View v, St at, Color c, Color edge, string cut)
    {
        var p = Grid3(at);
        Snd(cut);
        for (int i = 0; i < 4; i++) { float a = i * Mathf.Tau / 4 + 0.4f; var q = p + new Vector3(Mathf.Cos(a) * 0.9f, 0, Mathf.Sin(a) * 0.8f); double d = i * 0.12; After(d, () => LightningHit(q, c, 0.55f, dim: false)); }
        await Wait(0.6);
        ScreenFlash(c, 0.25f, 0.3);
        await BuildIn(v, at, edge, 0.75, new Vector2(0.5f, 0.5f), 7f, (s, k) => { if (GD.Randf() < 0.7f) Crawl(s, c); s.Set("glow_col", c); s.Set("glow", (1 - k) * 0.6f); });
        Warp(p + Vector3.Up * 0.05f, 4f, 0.5, 1.6f); Shake(0.1f); FxQuad(GroundGlowFx, p with { Y = 0.02f }, new Vector2(1.6f, 1.6f), 0.5, c);
        Cue("land");
        await Wait(0.4);
    }

    Task BlueEyesOn(View v, St at) => DragonOn(v, at, new Color(0.65f, 0.85f, 1f), new Color(0.7f, 0.88f, 1f), "SE_MONSTER_CUTIN_LIGHT");
    Task RedEyesOn(View v, St at) => DragonOn(v, at, new Color(1f, 0.32f, 0.22f), new Color(1f, 0.6f, 0.45f), "SE_MONSTER_CUTIN_FIRE");

    /// A light orb and a dark orb rise from `side`'s Graveyard, circle the zone closing in, and collide: the card.
    async Task ChaosOn(View v, St at)
    {
        var p = Grid3(at);
        var light = new Color(1f, 0.95f, 0.8f); var dark = new Color(0.55f, 0.3f, 1f); var gy = GyPos(at.Side);
        Snd("SE_EV_ULTIMATE_SLAYER");
        var c = p + Vector3.Up * 0.5f;
        Orb(k => { float a = k * 3f * Mathf.Tau, rad = 1.2f * (1 - k * k); return k < 0.25f ? gy.Lerp(c, k * 4) + Vector3.Up * 0.3f : c + new Vector3(Mathf.Cos(a) * rad, 0.2f, Mathf.Sin(a) * rad * 0.8f); }, light, 1.4, 0.55f, null, Tween.EaseType.InOut);
        Orb(k => { float a = k * 3f * Mathf.Tau + Mathf.Pi, rad = 1.2f * (1 - k * k); return k < 0.25f ? gy.Lerp(c, k * 4) + Vector3.Up * 0.3f : c + new Vector3(Mathf.Cos(a) * rad, 0.2f, Mathf.Sin(a) * rad * 0.8f); }, dark, 1.4, 0.55f, null, Tween.EaseType.InOut);
        await Wait(1.4);
        ScreenFlash(Colors.White, 0.45f, 0.35); Warp(c, 4f, 0.5, 2f, false, Bill); HitStop(0.06); Shake(0.14f);
        FxQuad(CoreGlowFx, c, new Vector2(2.2f, 2.2f), 0.4, light, Bill);
        await Materialise(v, at, light, 0.35, new Vector2(0.5f, 0.5f), 6f);
        Cue("land");
        await Wait(0.4);
    }

    /// Jinzo builds in red, then red pulses run out from it over the table; each Spell & Trap card the pulse reaches
    /// glitches red and goes dull for a moment (set cards stay set: nothing about them is shown).
    async Task JinzoOn(View j, St at)
    {
        var traps = views.Values.Where(v => v.Root.Visible && v.S.Loc == Loc.Spell && (!v.S.Up || v.Card.Data.IsTrap)).ToList();
        Snd("SE_EV_IMPERIAL_ORDER");
        await BuildIn(j, at, new Color(1f, 0.3f, 0.3f), 0.45, null, 4f, null, 0.5f);
        var jp = j.Root.GlobalPosition; var red = new Color(1f, 0.22f, 0.2f);
        for (int i = 0; i < 3; i++) { After(i * 0.18, () => { FxQuad(RingFx, jp with { Y = 0.05f }, new Vector2(1.6f, 1.6f), 0.5, red); Warp(jp + Vector3.Up * 0.05f, 7f, 0.9, 0.9f); }); }
        FxQuad(GroundGlowFx, jp with { Y = 0.03f }, new Vector2(1.4f, 1.4f), 0.8, red);
        foreach (var tv in traps)
            After((jp - tv.Root.GlobalPosition).Length() / 4.5 * 0.9, () =>
            {
                var s = Overlay(tv, 1.6);
                s.Set("glitch_col", red); Anim(s.M, "glitch", 0f, 1f, 0.15); Anim(s.M, "glitch", 1f, 0.15f, 0.9, 0.5);
                Anim(s.M, "darken", 0f, 0.55f, 1.0, 0.4);
                Snd("SE_SOLO_GATE078_ELECTRIC");
            });
        await Wait(1.6);
    }

    // ---------------------------------------------------------------- Time Wizard: the sand clocks

    /// A small clock over each card runs fast backward and each one crumbles to dust.
    async Task TimeWizardOn(List<View> victims)
    {
        var gold = new Color(1f, 0.85f, 0.5f); var dust = new Color(0.75f, 0.68f, 0.55f);
        Snd("SE_EV_TRANSMISSION_GEAR");
        foreach (var v in victims)
        {
            var at = v.Root.GlobalPosition + new Vector3(0, 0.5f, -0.35f);
            var c = FxQuad(ClockFx, at, new Vector2(0.6f, 0.6f), 1.3, gold, Bill, false, null, ("amt", 0f));
            Anim((ShaderMaterial)c.MaterialOverride, "amt", 0f, -3f, 1.0, 0.1, Tween.TransitionType.Quad, Tween.EaseType.In);
            var s = CardSheet(v); var pos = v.Root.GlobalPosition;
            After(0.9, () =>
            {
                s.Set("glow_col", new Color(0.6f, 0.45f, 0.2f)); Anim(s.M, "glow", 0f, 0.3f, 0.3); Anim(s.M, "darken", 0f, 0.35f, 0.4);
                Flakes(s, 30, uv => new Vector3(R(-0.2, 0.2), R(-0.1, 0.3), R(-0.2, 0.2)), 0.9, 0.5, null, 0.1f, 2.5f);
                BurnAway(s, dust, 0.6, new Vector2(0.5f, 0f), 0.2, 0.3f, 1.5f);
                After(0.5, () => Puffs(pos, 2, new Color(0.6f, 0.55f, 0.45f, 0.45f), 0.7f, 0.3f, 0.25f));
            });
        }
        for (int i = 0; i < 8; i++) Snd("SE_DUEL_TIME_COUNT", 0.15 + i * 0.1);
        await Wait(2.0);
    }

    // ---------------------------------------------------------------- Banished: phase out

    /// The card glows cold and burns away from the top, then a cold orb carries it to its owner's banished pile.
    async Task BanishOn(View v, int pileSide)
    {
        var p = v.Root.GlobalPosition; var cold = new Color(0.7f, 0.8f, 1f);
        var s = CardSheet(v);
        s.Set("glow_col", cold); Anim(s.M, "glow", 0f, 1.1f, 0.35); Anim(s.M, "darken", 0f, 0.5f, 0.35);
        BurnAway(s, new Color(0.75f, 0.85f, 1f), 0.65, new Vector2(0.5f, 0f), 0.35, 0.8f, 4f, 0.2f);
        await Wait(0.9);
        var pile = Grid3(new St(Loc.Banished, 0, 0, true, false, pileSide, 0, 0)) + Vector3.Up * 0.2f;
        Orb(Line(p + Vector3.Up * 0.2f, pile, 0.5f), cold, 0.35, 0.4f, () => FxQuad(RingFx, pile with { Y = 0.03f }, new Vector2(1.2f, 1.2f), 0.4, cold));
        await Wait(0.4);
    }
}
