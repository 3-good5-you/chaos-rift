using Godot;
using PoC.Engine;

namespace PoC.Game.UI;

/// FX Lab "signature cards", round 6 (2026-11-13). Locked in round 5 (code in Batch3, rows gone): Heavy Storm (no rain),
/// Trap Hole, Ring of Destruction, Magic Cylinder (mouth opened at the front, no beam from inside), Blue-Eyes, Jinzo A
/// (set cards stay hidden). Dropped by the user: Graceful Charity, Solemn Judgment (no effect for them).
public partial class DuelField
{
    void Batch4Rows(Action<int, string, Func<int, Task>, string[]> row)
    {
        row(73, "Mirror Force", MirrorForce6, new[] {
            "Curved barrier: a curved wall of mirror light (no frame) bends round your side, the hit flares white on it, and a fan of beams goes back into every attacker at once",
            "Burst back: the barrier takes the hit, then bursts outward as a wave of mirror light that runs over their side burning every attacker it reaches" });
        row(74, "Negate Attack", NegateAttack6, new[] {
            "Vortex: a swirl opens in front of the attacker as it charges, swallows its charge, the attacker is thrown back to its zone and the swirl closes into your trap",
            "Rewind: the attacker's charge runs backward in streaks of light and it returns to its zone; their monsters dim for a moment: no more attacks",
            "Drained: the attacker's power is pulled out of it in streams into your trap, which flares; the attacker sinks back to its zone, dull" });
        row(75, "Harpie's Feather Duster", FeatherDuster6, new[] {
            "Wing sweep: one great wing of light sweeps along their back row; each card it passes bursts into feathers",
            "Wingbeat: two great wings open behind your card and beat once; the gust carries their cards off as feathers",
            "Wing from above: a wing rises out of your card and fans down over their back row, turning each card to feathers as it covers it" });
        row(82, "Change of Heart", ChangeOfHeart6, new[] { "Solid wings, fixed: the wings the right way up (the Master Duel texture is stored upside down), cleaner shading, then the heart and the crossing" });
        row(91, "Monster Reborn", MonsterReborn6, new[] { "Particles: the monster comes apart into particles from the bottom up in your Graveyard, they stream to the zone, and it builds back from the top down" });
        row(96, "Torrential Tribute", Torrential6, new[] {
            "Falls: water crashes down from above onto every monster zone at once; the monsters are driven down and gone in the spray",
            "Two tides: a wall of water comes in from each player's edge; they meet in the middle in a great splash and the monsters are gone",
            "Water serpent: a stream of water leaps out of the trap and winds over the table, striking each monster in turn" });
        row(99, "Summon: Dark Magician", DarkMagician6, new[] { "Circle with impact: the table dims, the circle opens with violet fire, dark lightning drops into it, a burst bends the table, then the card burns in with a violet flame edge" });
        row(100, "Summon: Red-Eyes B. Dragon", RedEyes6, new[] { "Red lightning: exactly the Blue-Eyes summon in red: bolts ring the zone, then the card builds up with red lightning crawling over it" });
        if (OS.GetCmdlineUserArgs().Contains("--labfixes"))   // locked looks with late fixes, for filming only
        {
            row(903, "check Negate", NegateAttack6, new[] { "x", "rewind" });
            row(904, "check Torrential", Torrential6, new[] { "x", "two tides" });
        }
        row(101, "Summon: Egyptian God (Obelisk)", Obelisk6, new[] { "Descent, fixed: the slow motion no longer sticks (two hit-stops at once left the game slowed); quicker; thunder, impact and rumble sounds" });
    }

    // ---------------------------------------------------------------- 73 Mirror Force

    const string CurvedMirrorCode = @"shader_type spatial;
render_mode unshaded, blend_add, cull_disabled, depth_draw_never, shadows_disabled;
uniform vec4 col : source_color; uniform float amt = 0.0; uniform float flare = 0.0; uniform sampler2D noise : repeat_enable, filter_linear_mipmap;
varying vec3 lp;
void vertex() { lp = VERTEX; }
void fragment() {
  float f = pow(1.0 - abs(dot(normalize(NORMAL), normalize(VIEW))), 1.6);
  float glint = exp(-pow((UV.x * 1.4 + UV.y * 0.6 - fract(TIME * 0.4) * 2.4 + 0.2) / 0.03, 2.0));
  float sheen = pow(texture(noise, vec2(UV.x * 2.0 + TIME * 0.05, UV.y * 0.7)).r, 2.5);
  float rise = smoothstep(amt * 1.1 - 0.1, amt * 1.1, 1.0 - UV.y);
  float hit = exp(-pow(length((UV - vec2(0.5, 0.55)) * vec2(2.5, 1.0)) / (0.25 + flare * 0.5), 2.0)) * flare;
  float edges = smoothstep(0.0, 0.12, UV.x) * smoothstep(1.0, 0.88, UV.x) * smoothstep(0.0, 0.1, 1.0 - UV.y) * smoothstep(1.0, 0.75, 1.0 - UV.y);
  ALBEDO = mix(col.rgb, vec3(1.0), clamp(glint + hit, 0.0, 1.0));
  ALPHA = clamp((0.08 + f * 0.45 + sheen * 0.15 + glint * 0.6 + hit) * edges, 0.0, 1.0) * (1.0 - rise) * smoothstep(0.15, -0.55, lp.z);   // the half facing them only
}";

    async Task MirrorForce6(int style)
    {
        var foes = TheirMonsters();
        var mine = MineAt("Celtic Guardian", 2);
        var a = foes[1];
        var trap = Of(0, "Mirror Force"); Place(trap, SpellZ(2, 0, false));
        await Wait(0.4);
        Charge(a, mine.Root.GlobalPosition, 0.35, 0.35f);
        await Wait(0.3);
        await Flip(trap, FxPink, "SE_EV_MIRRORFORCE");
        var silver = new Color(0.75f, 0.88f, 1f);
        var hitAt = mine.Root.GlobalPosition + new Vector3(0, 0.5f, -CH * 0.9f);
        // a curved wall: part of a cylinder bending round your monsters
        var m = Shade(CurvedMirrorCode, ("col", silver), ("noise", FxNoise), ("amt", 0f));
        var wall = new MeshInstance3D { Mesh = new CylinderMesh { TopRadius = 1, BottomRadius = 1, Height = 1, RadialSegments = 64, Rings = 1, CapTop = false, CapBottom = false }, MaterialOverride = m, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            Position = new Vector3(0, 0.5f, mine.Root.GlobalPosition.Z + 0.5f), Scale = new Vector3(3.0f, 1.0f, 1.35f) };
        world.AddChild(wall);
        Anim(m, "amt", 0f, 1f, 0.3, 0, Tween.TransitionType.Cubic, Tween.EaseType.Out);
        Orb(Line(a.Root.GlobalPosition + Vector3.Up * 0.2f, hitAt, 0.2f), new Color(1f, 0.6f, 0.3f), 0.32, 0.7f);
        await Wait(0.33);
        Anim(m, "flare", 1f, 0f, 0.6, 0, Tween.TransitionType.Expo, Tween.EaseType.Out);
        Warp(hitAt, 2.6f, 0.45, 1.6f, false, Bill); Flash(hitAt, silver, 10f); ScreenFlash(silver, 0.3f, 0.25); Shake(0.1f); HitStop(0.06);
        Streaks(hitAt, Colors.White, 18, 3.2f);
        await Wait(0.1);
        var atk = foes.Where(f => !f.S.Def).ToList();
        if (style == 1)
            foreach (var f in atk)
            {
                var to = f.Root.GlobalPosition + Vector3.Up * 0.1f;
                Beam(hitAt, to, 0.7f, 0.5, silver); Beam(hitAt, to, 0.25f, 0.4, Colors.White);
                After(0.08, () => { Burn(f, new Color(0.75f, 0.9f, 1f), 0.55, new Vector2(0.5f, 0.9f), 0, 1f, 6f); Flash(to, silver, 6f); Streaks(to, Colors.White, 10, 2.2f); });
            }
        else
        {
            var front = FxQuad(BeamFx, new Vector3(0, 0.1f, hitAt.Z), new Vector2(7f, 0.8f), 0.7, silver, Flat, false, null, ("amt", 1.4f));
            front.CreateTween().TweenProperty(front, "position:z", -2.5f, 0.55).SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.Out);
            Warp(new Vector3(0, 0.05f, hitAt.Z - 1.4f), 6f, 0.6, 1.4f);
            foreach (var f in atk) { double d = Mathf.Abs(hitAt.Z - f.Root.GlobalPosition.Z) / 2.6 * 0.55; After(d, () => { Burn(f, new Color(0.75f, 0.9f, 1f), 0.5, new Vector2(0.5f, 1f), 0, 1f, 6f); Streaks(f.Root.GlobalPosition, Colors.White, 8, 2f); }); }
        }
        Snd("SE_CARD_FINISH_BREAK_02", 0.1);
        Anim(m, "amt", 1f, 0f, 0.4, 0.6);
        After(1.1, () => wall.QueueFree());
        await Wait(0.4);
        GoHome(a);
        await Wait(1.6);
    }

    // ---------------------------------------------------------------- 74 Negate Attack

    async Task NegateAttack6(int style)
    {
        var foes = TheirMonsters();
        var mine = MineAt("Celtic Guardian", 2);
        var a = foes[1];
        var trap = Of(0, "Negate Attack"); Place(trap, SpellZ(2, 0, false));
        await Wait(0.4);
        var home = a.Root.GlobalTransform;
        Charge(a, mine.Root.GlobalPosition, 0.35, 0.3f);
        await Wait(0.3);
        await Flip(trap, FxPink, "SE_EV_INFINITE_IMPERMANENCE");
        var ap = a.Root.GlobalPosition; var tp = trap.Root.GlobalPosition + Vector3.Up * 0.1f;
        var blue = new Color(0.55f, 0.75f, 1f); var fire = new Color(1f, 0.6f, 0.3f);
        if (style == 1)
        {
            var at = ap + (mine.Root.GlobalPosition - ap).Normalized() * 0.5f + Vector3.Up * 0.25f;
            FxQuad(VortexFx, at, new Vector2(1.2f, 1.2f), 1.1, blue * new Color(0.6f, 0.6f, 0.6f), Bill, false, null, ("col2", Colors.White), ("amt", 6f));
            FxQuad(CoreFx, at, new Vector2(0.3f, 0.3f), 1.1, blue, Bill, true);
            Warp(at, 1.6f, 1.0, 1.2f, true, Bill);
            SoulStream(ap + Vector3.Up * 0.15f, at, 12, fire, 0.4, 0.2f, 0.02);
            await Wait(0.5);
            Fly(a, home, 0.3f, Tween.TransitionType.Back, Tween.EaseType.Out);   // thrown back
            Shake(0.06f);
            await Wait(0.3);
            Orb(Line(at, tp, 0.6f), blue, 0.4, 0.5f, () => FrameBurst(trap, blue, 0.9f, 1.25f, 0.4));
            await Wait(0.5);
        }
        else if (style == 2)
        {
            await NegateRewind(a, home, blue);
            foreach (var f in foes) { f.Face.AlbedoColor = new Color(0.6f, 0.65f, 0.75f); var ff = f; After(0.7, () => ff.Face.AlbedoColor = Colors.White); }
            ScreenFlash(blue, 0.15f, 0.4);
            await Wait(0.6);
        }
        else
        {
            SoulStream(ap + Vector3.Up * 0.1f, tp, 24, fire, 0.7, 0.7f, 0.025);
            a.Face.AlbedoColor = new Color(0.55f, 0.55f, 0.6f);
            await Wait(0.85);
            FrameBurst(trap, fire, 0.9f, 1.3f, 0.45); Snd("SE_CARD_ST_BREAK");
            Fly(a, home, 0.4f, Tween.TransitionType.Sine, Tween.EaseType.Out);
            await Wait(0.6);
            a.Face.AlbedoColor = Colors.White;
        }
        Place(a, a.S);
        await Wait(1.6);
    }

    // ---------------------------------------------------------------- 75 Harpie's Feather Duster

    async Task FeatherDuster6(int style)
    {
        var board = RivalBoard(); var rows = board.Where(v => v.S.Loc == Loc.Spell).ToList();
        foreach (var (name, zone) in new[] { ("Mirror Force", 0), ("Trap Hole", 4) }) { var v = Of(1, name); Place(v, SpellZ(zone, 1, false)); rows.Add(v); }
        var card = await PlayCard("Harpie's Feather Duster", FxGreen, sound: "SE_EV_HARPIESFEATHER_DUSTER");
        var gold = new Color(1f, 0.92f, 0.72f); var hi = Colors.White; float z = ZonePos(Loc.Spell, 2, 1).Z;
        void ToFeathers(View v, Vector3 drift)
        {
            var s = CardSheet(v);
            BurnAway(s, Colors.White, 0.35, new Vector2(0.5f, 0.5f), 0, 0.3f, 4f);
            var p = s.Mi.GlobalPosition;
            for (int i = 0; i < 8; i++) Feather(p + new Vector3(R(-0.25, 0.25), 0.1f, R(-0.3, 0.3)), p + drift + new Vector3(R(-0.6, 0.6), R(0.6, 1.4), R(-0.5, 0.3)), i % 2 == 0 ? gold : Colors.White, R(0.9, 1.4), 0.4f);
        }
        if (style == 1)
        {
            // one wing, held out flat-ish, swept along the row like a hand brushing a table
            var pivot = new Node3D(); world.AddChild(pivot);
            pivot.GlobalTransform = new Transform3D(Bill * new Basis(Vector3.Right, -0.5f), new Vector3(-4.2f, 0.55f, z));
            var m = FxMat(SolidWingFx, gold, true, "md2/wingR", ("col2", hi), ("amt", 0f));
            pivot.AddChild(new MeshInstance3D { Mesh = new QuadMesh { Size = new Vector2(2.2f, 2.2f) }, MaterialOverride = m, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off, Position = new Vector3(0.45f, 0.1f, 0) });
            var tw = pivot.CreateTween();
            tw.TweenMethod(Callable.From<float>(e => m.SetShaderParameter("amt", e)), 0f, 1f, 0.2);
            tw.Parallel().TweenProperty(pivot, "position:x", 3.6f, 1.1).SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.InOut);
            tw.TweenMethod(Callable.From<float>(e => m.SetShaderParameter("amt", 1 - e)), 0f, 1f, 0.3);
            tw.TweenCallback(Callable.From(pivot.QueueFree));
            Snd("SE_ATTACK_A_WIND_SPECIAL_01", 0.1);
            foreach (var v in rows) { float x = v.Root.GlobalPosition.X; double d = 1.1 * Mathf.Acos(1 - 2 * Mathf.Clamp((x - 0.3f + 4.2f) / 7.8f, 0, 1)) / Mathf.Pi; After(d, () => ToFeathers(v, new Vector3(1.0f, 0, 0))); }
            await Wait(1.6);
        }
        else if (style == 2)
        {
            var root = card.Root.GlobalPosition + new Vector3(0, 0.6f, -0.5f);
            SolidWing(root - Bill.X * 0.05f, -1, 1.9f, gold, hi, 0.4, 0.8);
            SolidWing(root + Bill.X * 0.05f, 1, 1.9f, gold, hi, 0.4, 0.8);
            await Wait(0.62);   // the downstroke
            Snd("SE_ATTACK_A_WIND_SPECIAL_01");
            Warp(card.Root.GlobalPosition + new Vector3(0, 0.3f, -1.2f), 6f, 0.6, 1.1f);
            for (int i = 0; i < 30; i++) { double d = i * 0.02; After(d, () => { var from = card.Root.GlobalPosition + new Vector3(R(-2, 2), R(0.3, 0.9), -0.6f); Feather(from, from + new Vector3(R(-0.6, 0.6), R(0.2, 0.8), -3.5f), i % 2 == 0 ? gold : Colors.White, R(0.8, 1.2), R(0.3, 0.45)); }); }
            foreach (var v in rows) { double d = 0.25 + R(0, 0.15); After(d, () => ToFeathers(v, new Vector3(0, 0, -1.2f))); }
            await Wait(1.5);
        }
        else
        {
            // a wing rises out of your card and lays itself over their back row, left to right
            var pivot = new Node3D(); world.AddChild(pivot);
            pivot.GlobalTransform = new Transform3D(Bill * new Basis(Vector3.Right, -0.6f), new Vector3(1.6f, 0.3f, z + 0.2f));   // rooted beside their back row, fanning over it
            var m = FxMat(SolidWingFx, gold, true, "md2/wingL", ("col2", hi), ("amt", 0f));
            var q = new MeshInstance3D { Mesh = new QuadMesh { Size = new Vector2(3.4f, 3.4f) }, MaterialOverride = m, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off, Position = new Vector3(-1.1f, 0.5f, 0) };
            pivot.AddChild(q);
            pivot.Rotation = new Vector3(0, 0, -1.4f); pivot.Scale = Vector3.One * 0.3f;
            var tw = pivot.CreateTween();
            tw.TweenMethod(Callable.From<float>(e => m.SetShaderParameter("amt", e)), 0f, 1f, 0.3);
            tw.Parallel().TweenProperty(pivot, "scale", Vector3.One, 0.4).SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
            tw.TweenProperty(pivot, "rotation:z", 0.5f, 0.9).SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.InOut);   // fans over the row
            tw.TweenMethod(Callable.From<float>(e => m.SetShaderParameter("amt", 1 - e)), 0f, 1f, 0.35);
            tw.TweenCallback(Callable.From(pivot.QueueFree));
            Snd("SE_ATTACK_A_WIND_DEFAULT_01", 0.4);
            foreach (var v in rows) { float x = v.Root.GlobalPosition.X; double d = 0.4 + (x + 2.8f) / 5.6f * 0.85; After(d, () => ToFeathers(v, new Vector3(0.6f, 0, 0))); }
            await Wait(1.8);
        }
        await Wait(1.0);
    }

    // ---------------------------------------------------------------- 82 Change of Heart

    Task ChangeOfHeart6(int style) => ChangeOfHeart5(1);   // same staging; the wing itself was fixed (SolidWing / SolidWingFx)

    // ---------------------------------------------------------------- 91 Monster Reborn

    async Task MonsterReborn6(int style)
    {
        var dead = Of(0, "Dark Magician"); Place(dead, GyAt(0));
        await PlayCard("Monster Reborn", FxGreen, sound: "SE_EV_MONSTER_REBORN");
        var gy = GyPos(0); var at = Mon(dead, 2, 0); var zone = Grid3(at);
        var gold = new Color(1f, 0.9f, 0.65f);
        // it comes apart from the bottom up in the Graveyard
        var ghost = NewSheet(CardArt.Small(dead.Card.Data), dead.Root.GlobalTransform);
        dead.Root.Visible = false;
        BurnAway(ghost, gold, 0.75, new Vector2(0.5f, 1.05f), 0, 0.4f, 3f);
        Flakes(ghost, 40, uv => new Vector3(R(-0.15, 0.15), R(0.6, 1.1), R(-0.15, 0.15)), 0.9, 0.7, gold, 0.07f, -0.2f);
        // the particles stream over to the zone
        for (int i = 0; i < 36; i++) { double d = 0.35 + i * 0.022; After(d, () => Mote(gy + new Vector3(R(-0.25, 0.25), 0.5f, R(-0.3, 0.3)), zone + new Vector3(R(-0.25, 0.25), 0.25f, R(-0.35, 0.35)), i % 3 == 0 ? Colors.White : gold, 0.6, R(0.05, 0.09), 0.7f)); }
        await Wait(1.0);
        // and it builds back from the top down
        await BuildIn(dead, at, gold, 0.75, new Vector2(0.5f, -0.05f), 3.5f, null, 1.2f);
        FxQuad(GroundGlowFx, zone with { Y = 0.02f }, new Vector2(1.3f, 1.3f), 0.5, gold);
        Cue("land");
        await Wait(1.5);
    }

    // ---------------------------------------------------------------- 96 Torrential Tribute

    async Task Torrential6(int style)
    {
        var foes = TheirMonsters();
        var mine = new[] { MineAt("Celtic Guardian", 1), MineAt("Dark Magician", 3) };
        var all = foes.Concat(mine).ToList();
        var trap = Of(0, "Torrential Tribute"); Place(trap, SpellZ(2, 0, false));
        var sum = Of(1, "Battle Ox"); Place(sum, HandOf(1, 0, 1, false));
        await Wait(0.4);
        Move(sum, Mon(sum, 0, 1), 0, 0.45f, 0.5f); all.Add(sum);
        await Wait(0.55);
        await Flip(trap, FxPink, "SE_EV_FORBIDDEN_DROPLET");
        var water = new Color(0.3f, 0.6f, 0.95f); var foam = new Color(0.88f, 0.96f, 1f);
        void Hit(View v, Vector3 push, bool mist = false)
        {
            var s = CardSheet(v); var from = s.Mi.Transform;
            s.Set("glow_col", water); Anim(s.M, "glow", 0f, 0.4f, 0.2); Anim(s.M, "darken", 0f, 0.5f, 0.4);
            s.Mi.CreateTween().TweenMethod(Callable.From<float>(k => s.Mi.Transform = new Transform3D(new Basis(Vector3.Right, -k * 0.8f) * from.Basis, from.Origin + push * k)), 0f, 1f, 0.45);
            if (!mist) Streaks(from.Origin + Vector3.Up * 0.1f, foam, 16, 2.8f, 7f, Vector3.Up, 0.6f);
            Puffs(from.Origin + Vector3.Up * 0.15f, mist ? 3 : 2, new Color(0.85f, 0.93f, 1f, 0.5f), 0.7f, 0.5f, 0.2f);
            BurnAway(s, foam, 0.35, null, 0.15, 0.3f, 2f);
        }
        if (style == 1)
        {
            foreach (var v in all)
            {
                var p = v.Root.GlobalPosition with { Y = 0 };
                FxStand(PillarFx, p + Vector3.Up * 0.0f, new Vector2(0.95f, 4.5f), 0.55, foam, false, null, ("amt", 1.1f));
                FxStand(SoftFireFx, p, new Vector2(1.1f, 0.9f), 0.6, water, false, null, ("col2", foam), ("amt", 1f));   // the splash at its foot
            }
            Snd("SE_EV_CONVULSION_OF_NATURE_b", 0.05);
            await Wait(0.22);
            Shake(0.1f);
            foreach (var v in all) Hit(v, Vector3.Down * 0.15f);
            await Wait(1.2);
        }
        else if (style == 2)
        {
            var near = FxQuad(TideFx, new Vector3(0, 0.08f, 1.25f), new Vector2(7.4f, 2.5f), 0.9, water, Flat, true);
            var far = FxQuad(TideFx, new Vector3(0, 0.08f, -1.25f), new Vector2(7.4f, 2.5f), 0.9, water, new Basis(Vector3.Up, Mathf.Pi) * Flat, true);
            Snd("SE_SOLO_WIND_01");
            await Wait(0.7);
            var mid = new Vector3(0, 0.1f, 0);
            for (int i = 0; i < 16; i++) Puffs(mid + new Vector3(R(-3.2, 3.2), 0, R(-0.4, 0.4)), 1, new Color(0.88f, 0.95f, 1f, 0.55f), 1.2f, 1.1f, 0.25f);   // mist where they meet
            Warp(mid, 6f, 0.5, 1.4f); Shake(0.12f); Snd("SE_EV_CONVULSION_OF_NATURE_b");
            foreach (var v in all) Hit(v, new Vector3(0, 0.1f, -Mathf.Sign(v.Root.GlobalPosition.Z) * 0.3f), mist: true);
            await Wait(1.4);
        }
        else
        {
            var tp = trap.Root.GlobalPosition + Vector3.Up * 0.2f;
            var order = all.OrderBy(v => v.Root.GlobalPosition.Z * 10 + v.Root.GlobalPosition.X * (v.Root.GlobalPosition.Z > 0 ? 1 : -1)).ToList();
            var pts = new List<Vector3> { tp };
            pts.AddRange(order.Select(v => v.Root.GlobalPosition + Vector3.Up * 0.15f));
            float segs = pts.Count - 1; double dur = 0.28 * segs;
            Vector3 P(float k) { float f = k * segs; int i = Mathf.Min((int)f, pts.Count - 2); float u = f - i; return pts[i].Lerp(pts[i + 1], u) + Vector3.Up * Mathf.Sin(u * Mathf.Pi) * 0.7f; }
            // the head and its trail
            for (int j = 0; j < 10; j++) { double d = j * 0.025; float sz = 0.75f - j * 0.05f; After(d, () => Orb(P, j == 0 ? foam : water, dur, sz, null, Tween.EaseType.InOut)); }
            for (int i = 0; i < order.Count; i++) { var v = order[i]; double d = dur * (i + 1) / segs; After(d, () => Hit(v, Vector3.Down * 0.1f)); }
            Snd("SE_SOLO_WIND_01");
            await Wait(dur + 1.0);
        }
        await Wait(0.8);
    }

    // ---------------------------------------------------------------- 99-101 summons

    async Task DarkMagician6(int style)
    {
        var v = Of(0, "Dark Magician"); var at = Mon(v, 2, 0); var p = Grid3(at);
        var violet = new Color(0.7f, 0.4f, 1f); var pale = new Color(0.9f, 0.75f, 1f);
        Snd("SE_MONSTER_CUTIN_DARK");
        ScreenDim(0.4f, 0.25, 1.4, 0.5);
        FxQuad(CircleFx, p + Vector3.Up * 0.02f, new Vector2(2.6f, 2.6f), 2.4, violet, Flat, false, null, ("amt", 2f));
        for (int i = 0; i < 12; i++) { float a = i * Mathf.Tau / 12; var q = p + new Vector3(Mathf.Cos(a) * 1.15f, -0.05f, Mathf.Sin(a) * 1.15f); double d = 0.25 + i * 0.03; After(d, () => FxStand(SoftFireFx, q, new Vector2(0.55f, 0.95f), 1.6, violet, false, null, ("col2", pale), ("amt", 1f))); }
        await Wait(0.7);
        for (int i = 0; i < 3; i++) { double d = i * 0.12; After(d, () => LightningHit(p + new Vector3(R(-0.3, 0.3), 0, R(-0.3, 0.3)), violet, 0.7f, dim: false)); }
        Snd("SE_DUEL_LIVE_THUNDER", 0.1);
        await Wait(0.4);
        Warp(p + Vector3.Up * 0.05f, 6f, 0.6, 2.2f); Shake(0.18f); HitStop(0.05); ScreenFlash(violet, 0.3f, 0.3);
        Snd("SE_CARD_FINISH_BREAK_03");
        await BuildIn(v, at, violet, 0.7, new Vector2(0.5f, 1f), 7f, (s, k) => { if (GD.Randf() < 0.35f) Crawl(s, pale); }, 1.3f);
        FxQuad(CoreGlowFx, p + Vector3.Up * 0.2f, new Vector2(1.3f, 1.3f), 0.35, violet, Bill);
        Cue("land");
        await Wait(1.5);
    }

    async Task RedEyes6(int style)
    {
        var v = Of(0, "Red-Eyes B. Dragon"); var at = Mon(v, 2, 0); var p = Grid3(at);
        var red = new Color(1f, 0.32f, 0.22f); var hot = new Color(1f, 0.6f, 0.45f);
        Snd("SE_MONSTER_CUTIN_FIRE");
        for (int i = 0; i < 4; i++) { float a = i * Mathf.Tau / 4 + 0.4f; var q = p + new Vector3(Mathf.Cos(a) * 0.9f, 0, Mathf.Sin(a) * 0.8f); double d = i * 0.12; After(d, () => LightningHit(q, red, 0.55f, dim: false)); }
        await Wait(0.6);
        ScreenFlash(red, 0.25f, 0.3);
        await BuildIn(v, at, hot, 0.75, new Vector2(0.5f, 0.5f), 7f, (s, k) => { if (GD.Randf() < 0.7f) Crawl(s, red); s.Set("glow_col", red); s.Set("glow", (1 - k) * 0.6f); });
        Warp(p + Vector3.Up * 0.05f, 4f, 0.5, 1.6f); Shake(0.1f); FxQuad(GroundGlowFx, p with { Y = 0.02f }, new Vector2(1.6f, 1.6f), 0.5, red);
        Cue("land");
        await Wait(1.5);
    }

    async Task Obelisk6(int style)
    {
        var tribs = new[] { MineAt("Celtic Guardian", 1), MineAt("Feral Imp", 2), MineAt("Curse of Dragon", 3) };
        await Wait(0.4);
        var v = Of(0, "Obelisk the Tormentor"); var at = Mon(v, 2, 0); var p = Grid3(at);
        var blue = new Color(0.35f, 0.6f, 1f); var pale = new Color(0.75f, 0.88f, 1f);
        Snd("SE_MONSTER_CUTIN_DIVINE");
        foreach (var t in tribs) { var s = CardSheet(t); BurnAway(s, blue, 0.45, null, 0, 0.6f, 5f); SoulStream(t.Root.GlobalPosition, p + new Vector3(0, 2.5f, -1f), 8, blue, 0.6, 0.6f, 0.03); }
        Snd("SE_SUMMON_ADVANCE", 0.1);
        await Wait(0.6);
        ScreenDim(0.55f, 0.25, 1.0, 0.5); Vignette(new Color(0.05f, 0.12f, 0.4f), 1.6);
        Snd("SE_DUEL_LIVE_THUNDER", 0.05);
        await Wait(0.3);
        LightningHit(p, pale, 2.0f, dim: false);
        Snd("SE_EV_RAIGEKI", 0.05);
        await Wait(0.12);
        FieldLines(0f, 0.15);
        Warp(p + Vector3.Up * 0.05f, 9f, 0.7, 3f); Shake(0.35f); ScreenFlash(blue, 0.45f, 0.35);
        Snd("SE_CARD_FINISH_BREAK_03", 0.02);
        for (int i = 0; i < 12; i++) Puffs(p + new Vector3(Mathf.Cos(i * 0.52f) * 1.2f, 0.05f, Mathf.Sin(i * 0.52f) * 1.0f), 1, new Color(0.4f, 0.45f, 0.55f, 0.6f), 0.9f, 0.4f, 0.1f);
        await BuildIn(v, at, pale, 0.6, new Vector2(0.5f, 0.5f), 8f, (s, k) => { if (GD.Randf() < 0.6f) Crawl(s, pale); });
        FieldLines(1f, 0.5);
        Snd("SE_DUEL_SUMMON_COUNT_05");
        Cue("land");
        await Wait(1.3);
    }
}
