using Godot;
using PoC.Engine;

namespace PoC.Game.UI;

/// FX Lab "signature cards", round 5 (2026-11-13). Locked in round 4 (code stays in Batch1/Batch2, rows gone):
/// Swords cage (big ring removed), MST cyclone, Dark Hole (lighter dim), Banished C phase out, Raigeki, Pot of Greed B,
/// Brain Control A, Time Wizard C, Chaos A. Everything still open is here, in one batch.
public partial class DuelField
{
    void Batch3Rows(Action<int, string, Func<int, Task>, string[]> row)
    {
        row(73, "Mirror Force", MirrorForce5, new[] {
            "Mirror, refined: a pane of true mirror rises in front of your monsters, the attack hits it in a ripple, and one bright beam goes back into each attacker, which burns from where it is hit",
            "Split: the attack's charge bounces off the mirror and breaks into rays that fan across their row",
            "Sheet: the mirror throws the whole hit back as one wide sheet of light that sweeps their row" });
        row(74, "Negate Attack", NegateAttack5, new[] {
            "Drawn in: a small vortex in front of your monster swallows the attack and snaps shut; a cool band sweeps the table: the Battle Phase is over",
            "Absorbing shield: the attack sinks into a disc of light, which ripples, glows and fades; the band sweeps, battle over",
            "Drained: light streams out of the attacker into your trap; the attacker dims and goes home; the band sweeps, battle over" });
        row(75, "Harpie's Feather Duster", FeatherDuster5, new[] {
            "Feather blade: one great feather of light glides along their back row and each card it passes turns to feathers",
            "Flurry: a gust of feathers swirls across their back row and carries each card off as feathers",
            "Dusting: a single feather drops onto each card in turn, brushes over it, and the card comes apart in feathers" });
        row(82, "Change of Heart", ChangeOfHeart5, new[] {
            "Solid wings: two smaller solid wings (Master Duel wing shape), white-gold and violet, unfold behind it and beat once; the heart above; it crosses",
            "Heart only: no wings; a soft two-coloured heart beats over it and draws it across on a ribbon of light" });
        row(84, "Heavy Storm", HeavyStorm5, new[] { "Twisters, cards pulled in: as each funnel reaches a card, the card is dragged to its foot, spirals up inside it with the funnel and comes apart near the top" });
        row(88, "Trap Hole", TrapHole5, new[] { "Drop: the ground gives way in a burst of dust, the monster tips and drops in darkening, the pit closes; quick" });
        row(90, "Ring of Destruction", RingOfDestruction5, new[] { "Ring, finer fireball: the same ring; the blast is many small boiling puffs instead of a few big ones" });
        row(91, "Monster Reborn", MonsterReborn5, new[] { "Flame: the monster burns out in your Graveyard, the flame leaps to the zone, and the monster burns back in there" });
        row(93, "Graceful Charity", GracefulCharity5, new[] {
            "Gold ring: a ring of gold light rises off your Deck; three cards rise through it to your hand; two leave your hand as falling gold petals",
            "From above: a beam of white light comes down; three cards fall out of it into your hand; two rise up into it and are gone",
            "Spread: a halo turns over your Deck and three cards fan off it in an arc; two cards leave with gold sparkle for the Graveyard" });
        row(95, "Magic Cylinder", MagicCylinder5, new[] { "Twin cylinders, refined: lacquered violet cylinders with gold stars and rune bands; one opens in front of your monster and swallows the attack, the other appears beside the attacker and fires it into their LP" });
        row(96, "Torrential Tribute", Torrential5, new[] {
            "Burst: water bursts out of the trap and splits into jets that hit every monster",
            "Downpour: the sky goes dark and a hard rain pours on every monster; they wash away in the spray",
            "Whirlpool: the whole table turns into a whirlpool; the monsters circle it and go under in the middle" });
        row(97, "Solemn Judgment", Solemn5, new[] {
            "Gavel: a great gold gavel comes down on the card; the shock rings out and the card burns away",
            "Halo of judgment: gold rings close round the card one by one, crush in, and it flares white and is gone",
            "Price paid: gold light drains from your LP to the trap, which throws a lance of gold through the card" });
        row(98, "Summon: Blue-Eyes White Dragon", BlueEyes5, new[] { "White lightning, refined: bolts ring the zone, then the card builds up with lightning crawling over it and a blue-white burning edge" });
        row(99, "Summon: Dark Magician", DarkMagician5, new[] { "Circle, refined: the magic circle, violet fire with no hard base, then the card burns in from its foot with a violet flame edge" });
        row(100, "Summon: Red-Eyes B. Dragon", RedEyes5, new[] { "Red lightning, dark, eyes: red bolts ring the zone, the table goes dark, two red eyes open, then the card burns in with a red edge" });
        row(101, "Summon: Egyptian God (Obelisk)", Obelisk5, new[] {
            "Descent: blue souls of the three tributes rise, the table goes deep blue, a colossal blue strike lands, the whole table bends and the card burns in",
            "Roar: a blue pillar with a turning hieroglyph ring; a blast of wind runs out over the field (every card shudders); the card forms from the top",
            "Storm of the god: blue bolts hammer round the zone faster and faster, then one huge strike and the card" });
        row(102, "Jinzo", Jinzo5, new[] {
            "Static: a red psychic wave rings out from Jinzo; each trap it reaches fills with red static and goes dead",
            "Override: a red lock sigil forms over each trap in turn and the trap greys out under it",
            "Eye flash: the table darkens, Jinzo's eyes flash red, red lines run to every trap and they glitch out" });
    }

    // ---------------------------------------------------------------- shared bits

    /// The Battle Phase is over: a cool band of light sweeps the table from their side to yours.
    void BattleEnds()
    {
        var band = FxQuad(BeamFx, new Vector3(0, 0.06f, -2.4f), new Vector2(7.4f, 0.55f), 0.9, new Color(0.6f, 0.8f, 1f), Flat, false, null, ("amt", 0.9f));
        band.CreateTween().TweenProperty(band, "position:z", 2.4f, 0.8).SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.InOut);
        Vignette(new Color(0.25f, 0.35f, 0.55f), 0.9);
        Snd("SE_DUEL_TIME_COUNT_ZERO", 0.1);
    }

    /// A copy of a card that stays where it is (the real one hidden for `dur`), for looks laid over a card that stays.
    Sheet Overlay(View v, double dur)
    {
        var s = NewSheet(v.S.Up ? v.Face.AlbedoTexture : backs[Side(v.Card.Owner)], v.Root.GlobalTransform);
        v.Root.Visible = false;
        After(dur, () => { if (IsInstanceValid(s.Mi)) s.Mi.QueueFree(); v.Root.Visible = true; });
        return s;
    }

    /// The card builds up out of nothing like Materialise, calling `tick(sheet, k)` as it goes.
    async Task BuildIn(View v, St at, Color edge, double dur, Vector2? origin, float edgeE, Action<Sheet, float>? tick = null, float embers = 1f)
    {
        Place(v, at);
        HideFor(v, dur + 0.02);
        var s = NewSheet(CardArt.Small(v.Card.Data), Target(v, at));
        s.Set("edge_col", edge); s.Set("edge_e", edgeE); s.Set("burn", 1f);
        if (origin is { } o) { s.Set("origin", o); s.Set("from_pt", 1f); }
        Anim(s.M, "burn", 1f, 0.0006f, dur, 0, Tween.TransitionType.Sine, Tween.EaseType.Out);
        int n = (int)(dur / 0.05);
        for (int i = 0; i < n; i++)
        {
            float k = i / (float)n;
            After(dur * k, () =>
            {
                if (!IsInstanceValid(s.Mi)) return;
                tick?.Invoke(s, k);
                if (GD.Randf() < embers) Ember(s.World(new Vector2(R(0.05, 0.95), R(0.05, 0.95))), edge, new Vector3(0, R(0.2, 0.5), 0), 0.6, 0.035f, 0.4f);
            });
        }
        await Wait(dur);
        s.Mi.QueueFree();
        UpdateStat(v, false);
    }

    /// Lightning crawling over a sheet: a short arc between two random points of it.
    void Crawl(Sheet s, Color c)
    {
        var a = s.World(new Vector2(R(0.05, 0.95), R(0.05, 0.95))) + Vector3.Up * 0.02f; var b = s.World(new Vector2(R(0.05, 0.95), R(0.05, 0.95))) + Vector3.Up * 0.02f;
        Arc(a, b, c, 0.3f, 0.12);
    }

    // ---------------------------------------------------------------- 73 Mirror Force

    const string MirrorFx = @"void fragment() {
  vec2 p = UV * 2.0 - 1.0;
  float rise = step(1.0 - UV.y, amt * 1.02);
  float sky = mix(0.25, 0.6, UV.y) + 0.25 * texture(noise, vec2(UV.x * 0.6 + TIME * 0.05, UV.y * 0.3)).r;
  float glint = exp(-pow((UV.x + UV.y * 0.5 - fract(TIME * 0.35) * 2.0 + 0.3) / 0.035, 2.0)) * 0.9;
  float glint2 = exp(-pow((UV.x + UV.y * 0.5 - fract(TIME * 0.35 + 0.15) * 2.0 + 0.3) / 0.012, 2.0)) * 0.6;
  vec2 e = abs(p); float frame = exp(-pow((max(e.x * 1.0, e.y) - 0.96) / 0.02, 2.0));
  float d = length((UV - vec2(0.5, 0.45)) * vec2(3.0, 1.0)); float rip = exp(-pow((d - col2.a * 2.0) / 0.06, 2.0)) * (1.0 - col2.a) * step(0.01, col2.a);
  ALBEDO = mix(col.rgb * sky, vec3(1.0), clamp(glint + glint2 + frame * 0.5 + rip, 0.0, 1.0));
  ALPHA = clamp(0.12 + sky * 0.18 + glint * 0.7 + glint2 * 0.6 + frame * 0.45 + rip, 0.0, 1.0) * rise * smoothstep(1.0, 0.98, e.x) * (1.0 - smoothstep(0.85, 1.0, t));
}";

    async Task MirrorForce5(int style)
    {
        var foes = TheirMonsters();
        var mine = MineAt("Celtic Guardian", 2);
        var a = foes[1];
        var trap = Of(0, "Mirror Force"); Place(trap, SpellZ(2, 0, false));
        await Wait(0.4);
        Charge(a, mine.Root.GlobalPosition, 0.35, 0.35f);
        await Wait(0.3);
        await Flip(trap, FxPink, "SE_EV_MIRRORFORCE");
        var silver = new Color(0.8f, 0.9f, 1f);
        float frontZ = mine.Root.GlobalPosition.Z - CH * 0.75f;
        var hitAt = new Vector3(0, 0.5f, frontZ);
        var pane = FxQuad(MirrorFx, new Vector3(0, 0.6f, frontZ), new Vector2(5.4f, 1.3f), 2.0, silver, new Basis(Vector3.Right, -0.7f), true, null, ("amt", 0f), ("col2", new Color(1, 1, 1, 0)));
        var pm = (ShaderMaterial)pane.MaterialOverride;
        Anim(pm, "amt", 0f, 1f, 0.25, 0, Tween.TransitionType.Cubic, Tween.EaseType.Out);
        Orb(Line(a.Root.GlobalPosition + Vector3.Up * 0.2f, hitAt, 0.2f), new Color(1f, 0.6f, 0.3f), 0.32, 0.7f);
        await Wait(0.32);
        CreateTween().TweenMethod(Callable.From<float>(k => pm.SetShaderParameter("col2", new Color(1, 1, 1, k))), 0.01f, 1f, 0.5);
        Warp(hitAt, 2.4f, 0.4, 1.4f, false, Bill); Flash(hitAt, silver, 9f); ScreenFlash(silver, 0.25f, 0.2); Shake(0.08f); HitStop(0.05);
        Streaks(hitAt, Colors.White, 16, 3f);
        await Wait(0.12);
        var atk = foes.Where(f => !f.S.Def).ToList();
        if (style == 1)
            foreach (var f in atk)
            {
                var to = f.Root.GlobalPosition + Vector3.Up * 0.1f;
                Beam(hitAt, to, 0.55f, 0.55, silver); Beam(hitAt, to, 0.2f, 0.4, Colors.White);
                After(0.1, () => { Burn(f, new Color(0.75f, 0.9f, 1f), 0.55, new Vector2(0.5f, 0.9f), 0, 1f, 6f); Flash(to, silver, 5f); Streaks(to, Colors.White, 8, 2f); });
            }
        else if (style == 2)
        {
            Orb(Line(hitAt, hitAt + new Vector3(0, 0.3f, -0.6f)), Colors.White, 0.12, 0.6f);
            await Wait(0.12);
            foreach (var f in atk)
            {
                var to = f.Root.GlobalPosition + Vector3.Up * 0.1f;
                for (int r = 0; r < 3; r++) { var off = new Vector3(R(-0.25, 0.25), 0, R(-0.2, 0.2)); Orb(Line(hitAt + new Vector3(0, 0.3f, -0.6f), to + off, 0.25f), silver, 0.22 + r * 0.04, 0.3f); }
                After(0.24, () => Burn(f, new Color(0.75f, 0.9f, 1f), 0.5, new Vector2(0.5f, 0.9f), 0, 1f, 6f));
            }
        }
        else
        {
            var sheet = FxQuad(BeamFx, new Vector3(0, 0.25f, frontZ), new Vector2(6.5f, 0.9f), 0.6, silver, Flat, false, null, ("amt", 1.3f));
            sheet.CreateTween().TweenProperty(sheet, "position:z", -1.6f, 0.4).SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.Out);
            foreach (var f in atk) { double d = 0.1 + (frontZ - f.Root.GlobalPosition.Z) * 0.12; After(d, () => Burn(f, new Color(0.75f, 0.9f, 1f), 0.5, new Vector2(0.5f, 1f), 0, 1f, 6f)); }
        }
        Snd("SE_CARD_FINISH_BREAK_02", 0.12);
        await Wait(0.4);
        GoHome(a);
        await Wait(1.6);
    }

    // ---------------------------------------------------------------- 74 Negate Attack

    async Task NegateAttack5(int style)
    {
        var foes = TheirMonsters();
        var mine = MineAt("Celtic Guardian", 2);
        var a = foes[1];
        var trap = Of(0, "Negate Attack"); Place(trap, SpellZ(2, 0, false));
        await Wait(0.4);
        Charge(a, mine.Root.GlobalPosition, 0.35, 0.35f);
        await Wait(0.3);
        await Flip(trap, FxPink, "SE_EV_INFINITE_IMPERMANENCE");
        var mp = mine.Root.GlobalPosition; var ap = a.Root.GlobalPosition;
        var front = mp + new Vector3(0, 0.45f, -CH * 0.8f);
        var blue = new Color(0.6f, 0.8f, 1f); var fire = new Color(1f, 0.6f, 0.3f);
        if (style == 1)
        {
            var orb = Orb(Line(ap + Vector3.Up * 0.2f, front, 0.1f), fire, 0.5, 0.65f, null, Tween.EaseType.Out);
            FxQuad(VortexFx, front, new Vector2(1.3f, 1.3f), 1.0, blue * new Color(0.6f, 0.6f, 0.6f), Bill, false, null, ("col2", Colors.White), ("amt", 5f));
            FxQuad(CoreFx, front, new Vector2(0.35f, 0.35f), 1.0, blue, Bill, true);
            Warp(front, 1.6f, 1.0, 1.2f, true, Bill);
            await Wait(0.5);
            orb.CreateTween().TweenProperty(orb, "scale", Vector3.One * 0.05f, 0.2).SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.In);
            await Wait(0.45);
            FxQuad(CoreGlowFx, front, new Vector2(1.0f, 1.0f), 0.25, blue, Bill); Snd("SE_CARD_ST_BREAK");
        }
        else if (style == 2)
        {
            var disc = FxQuad(HexFx, front, new Vector2(1.5f, 1.5f), 1.4, blue, Bill, false, null, ("amt", 1f), ("cells", new Vector2(7f, 7f)));
            FxQuad(RingFx, front, new Vector2(1.6f, 1.6f), 0.4, Colors.White, Bill);
            var orb = Orb(Line(ap + Vector3.Up * 0.2f, front, 0.1f), fire, 0.35, 0.65f);
            await Wait(0.35);
            orb.CreateTween().TweenProperty(orb, "scale", Vector3.One * 0.05f, 0.35);
            for (int i = 0; i < 3; i++) { int k = i; After(i * 0.12, () => FxQuad(RingFx, front, new Vector2(0.6f + k * 0.35f, 0.6f + k * 0.35f), 0.4, blue, Bill)); }
            FxQuad(CoreGlowFx, front, new Vector2(1.2f, 1.2f), 0.6, blue, Bill); Snd("SE_CARD_ST_BREAK");
            await Wait(0.6);
        }
        else
        {
            var tp = trap.Root.GlobalPosition + Vector3.Up * 0.1f;
            SoulStream(ap + Vector3.Up * 0.1f, tp, 22, fire, 0.7, 0.8f, 0.025);
            var s = Overlay(a, 1.2);
            s.Set("glow_col", new Color(0.3f, 0.35f, 0.45f)); Anim(s.M, "darken", 0f, 0.55f, 0.7);
            await Wait(0.9);
            FrameBurst(trap, blue, 0.9f, 1.25f, 0.45);
            await Wait(0.3);
        }
        GoHome(a, 0.5f);
        await Wait(0.3);
        BattleEnds();
        await Wait(1.6);
    }

    // ---------------------------------------------------------------- 75 Harpie's Feather Duster

    async Task FeatherDuster5(int style)
    {
        var board = RivalBoard(); var rows = board.Where(v => v.S.Loc == Loc.Spell).ToList();
        foreach (var (name, zone) in new[] { ("Mirror Force", 0), ("Trap Hole", 4) }) { var v = Of(1, name); Place(v, SpellZ(zone, 1, false)); rows.Add(v); }
        await PlayCard("Harpie's Feather Duster", FxGreen, sound: "SE_EV_HARPIESFEATHER_DUSTER");
        var gold = new Color(1f, 0.92f, 0.7f); float z = ZonePos(Loc.Spell, 2, 1).Z;
        void ToFeathers(View v, Vector3 drift)
        {
            var s = CardSheet(v);
            BurnAway(s, Colors.White, 0.4, new Vector2(0.5f, 0.5f), 0, 0.3f, 4f);
            var p = s.Mi.GlobalPosition;
            for (int i = 0; i < 7; i++) Feather(p + new Vector3(R(-0.25, 0.25), 0.1f, R(-0.3, 0.3)), p + drift + new Vector3(R(-0.6, 0.6), R(0.6, 1.4), R(-0.5, 0.3)), i % 2 == 0 ? gold : Colors.White, R(0.9, 1.4), 0.4f);
        }
        if (style == 1)
        {
            var f = FxQuad(FeatherFx, new Vector3(-3.8f, 0.3f, z + 0.1f), new Vector2(0.9f, 2.4f), 1.4, gold, new Basis(Vector3.Up, -Mathf.Pi / 2) * new Basis(Vector3.Right, -0.9f), false, null, ("col2", Colors.White));
            f.CreateTween().TweenProperty(f, "position:x", 3.8f, 1.15).SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.InOut);
            Snd("SE_ATTACK_A_WIND_DEFAULT_01", 0.1);
            foreach (var v in rows) { float x = v.Root.GlobalPosition.X; double d = 1.15 * Mathf.Acos(1 - 2 * Mathf.Clamp((x + 3.8f) / 7.6f, 0, 1)) / Mathf.Pi; After(d, () => ToFeathers(v, new Vector3(1.0f, 0, 0))); }
            await Wait(1.5);
        }
        else if (style == 2)
        {
            Snd("SE_ATTACK_A_WIND_SPECIAL_01");
            for (int i = 0; i < 40; i++)
            {
                double d = i * 0.03; float zz = z + R(-0.5, 0.5);
                After(d, () => { var from = new Vector3(-4f, R(0.2, 1.0), zz); Feather(from, from + new Vector3(8f, R(-0.2, 0.6), R(-0.4, 0.4)), i % 3 == 0 ? gold : Colors.White, R(1.0, 1.4), R(0.3, 0.5)); });
            }
            foreach (var v in rows) { double d = 0.2 + (v.Root.GlobalPosition.X + 4f) / 8f * 1.0; After(d, () => ToFeathers(v, new Vector3(1.5f, 0, 0))); }
            await Wait(1.6);
        }
        else
        {
            foreach (var v in rows.OrderBy(v => v.Root.GlobalPosition.X))
            {
                var p = v.Root.GlobalPosition;
                Feather(p + new Vector3(0, 1.4f, -0.3f), p + new Vector3(0, 0.08f, 0), gold, 0.45, 0.7f);
                await Wait(0.4);
                var swipe = FxQuad(FeatherFx, p + new Vector3(-0.3f, 0.12f, 0), new Vector2(0.4f, 1.0f), 0.3, gold, new Basis(Vector3.Up, -Mathf.Pi / 2) * Flat, false, null, ("col2", Colors.White));
                swipe.CreateTween().TweenProperty(swipe, "position:x", p.X + 0.3f, 0.25);
                ToFeathers(v, Vector3.Zero);
                Snd("SE_ATTACK_A_WIND_DEFAULT_01");
                await Wait(0.12);
            }
            await Wait(0.6);
        }
        await Wait(1.0);
    }

    // ---------------------------------------------------------------- 82 Change of Heart

    async Task ChangeOfHeart5(int style)
    {
        var foes = TheirMonsters(); var t = foes[1];
        await PlayCard("Change of Heart", FxGreen, sound: "SE_EV_EFFECT_VEILER");
        var p = t.Root.GlobalPosition; var goal = Mon(t, 3, 0);
        var heartAt = p + new Vector3(0, 1.05f, -0.95f);
        if (style == 1)
        {
            var root = p + new Vector3(0, 0.55f, -0.6f);
            SolidWing(root - Bill.X * 0.05f, -1, 1.5f, new Color(1f, 0.93f, 0.78f), Colors.White, 0.5, 1.4);
            SolidWing(root + Bill.X * 0.05f, 1, 1.5f, new Color(0.42f, 0.24f, 0.7f), new Color(0.85f, 0.7f, 1f), 0.5, 1.4);
            FxQuad(SoftHeartFx, heartAt, new Vector2(0.75f, 0.75f), 1.9, new Color(1f, 0.95f, 0.85f), Bill, true, null, ("col2", new Color(0.32f, 0.12f, 0.55f)), ("amt", 2.5f));
            Snd("SE_MONSTER_CUTIN_LIGHT", 0.3);
            await Wait(1.55);
            Move(t, goal, 0, 0.6f, 0.9f); await Wait(0.62);
        }
        else
        {
            var h = FxQuad(SoftHeartFx, heartAt, new Vector2(1.0f, 1.0f), 2.4, new Color(1f, 0.95f, 0.85f), Bill, true, null, ("col2", new Color(0.32f, 0.12f, 0.55f)), ("amt", 3f));
            for (int i = 0; i < 3; i++) Snd("SE_DUEL_TIME_COUNT_ZERO", 0.15 + i * 0.6);
            await Wait(1.3);
            var to = Target(t, goal).Origin + Vector3.Up * 0.1f;
            h.CreateTween().TweenProperty(h, "position", to + new Vector3(0, 0.6f, -0.4f), 0.75).SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.InOut);
            for (int i = 0; i < 14; i++) { double d = i * 0.05; After(d, () => Ember(t.Root.GlobalPosition + Vector3.Up * 0.15f, i % 2 == 0 ? new Color(1f, 0.85f, 0.95f) : new Color(0.7f, 0.5f, 1f), Vector3.Up * 0.2f, 0.8, 0.05f, 0)); }
            Move(t, goal, 0, 0.75f, 0.7f); await Wait(0.78);
        }
        var land = t.Root.GlobalPosition;
        FxQuad(RingFx, land with { Y = 0.04f }, new Vector2(1.6f, 1.6f), 0.5, FxPink); FxQuad(GroundGlowFx, land with { Y = 0.02f }, new Vector2(1.3f, 1.3f), 0.6, FxPink); Cue("land");
        await Wait(1.5);
    }

    // ---------------------------------------------------------------- 84 Heavy Storm

    async Task HeavyStorm5(int style)
    {
        var rows = RivalBoard().Where(v => v.S.Loc == Loc.Spell).ToList();
        foreach (var (name, zone) in new[] { ("Trap Hole", 0), ("Mirror Force", 4) }) { var v = Of(0, name); Place(v, SpellZ(zone, 0, false)); rows.Add(v); }
        var card = await PlayCard("Heavy Storm", FxGreen, sound: "SE_EV_HEAVY_STORM");
        rows.Add(card);
        var wind = new Color(0.72f, 0.82f, 1f); var dust = new Color(0.5f, 0.56f, 0.66f, 0.6f);
        Vignette(new Color(0.02f, 0.03f, 0.07f), 3.2);
        const double form = 0.5, run = 1.6, end = 0.6;
        const float x0 = 2.75f;
        var twisters = new Twist[2];
        foreach (int side in new[] { 0, 1 })
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
            var tw = twisters[side];
            float h = side == 0 ? 2.3f : 1.7f;
            double when = form * 0.8 + (x * dir + x0) / (2 * x0) * run - 0.35;   // a little before the funnel arrives: it starts pulling
            After(when, () =>
            {
                if (!IsInstanceValid(tw.Root)) return;
                var s = CardSheet(v); var from = s.Mi.GlobalTransform;
                Anim(s.M, "flutter", 0f, 1.3f, 0.2);
                float spin0 = R(0, 6.28f);
                s.Mi.CreateTween().TweenMethod(Callable.From<float>(k =>
                {
                    if (!IsInstanceValid(tw.Root)) return;
                    var axis = tw.Root.GlobalPosition;
                    // first dragged over the table to the funnel's foot, then round and up inside it
                    float pullIn = Mathf.Min(1, k / 0.3f), up = Mathf.Max(0, (k - 0.25f) / 0.75f);
                    float rad = Mathf.Lerp(0.15f, 0.5f, up), a = spin0 + up * up * 14f * dir;
                    var orbit = axis + new Vector3(Mathf.Cos(a) * rad, 0.12f + up * h * 0.75f, Mathf.Sin(a) * rad * 0.8f);
                    var pos = from.Origin.Lerp(orbit, Ease(pullIn));
                    s.Mi.GlobalTransform = new Transform3D(new Basis(Vector3.Up, a * 1.3f) * new Basis(Vector3.Right, -1.2f * pullIn) * from.Basis, pos).ScaledLocal(Vector3.One * (1 - 0.45f * up));
                }), 0f, 1f, 0.95);
                After(0.8, () =>
                {
                    Flakes(s, 30, uv => { var c = s.World(uv) - tw.Root.GlobalPosition; var flat = c with { Y = 0 }; return Vector3.Up.Cross(flat).Normalized() * R(1.2, 2.0) * dir + Vector3.Up * R(1.0, 1.8); }, 0.7, 0.15, wind);
                    BurnAway(s, new Color(0.85f, 0.92f, 1f), 0.2, null, 0, 0.2f, 1.5f, 1f);
                });
            });
        }
        await Wait(form + run + end + 1.0);
    }

    // ---------------------------------------------------------------- 88 Trap Hole

    async Task TrapHole5(int style)
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
        var pit = ZoneQuad(PitOpenCode, p, zone * 1.08f, ("col", new Color(0.55f, 0.38f, 0.24f)));
        var pm = (ShaderMaterial)pit.MaterialOverride;
        Anim(pm, "open", 0f, 0.95f, 0.12, 0, Tween.TransitionType.Cubic, Tween.EaseType.Out);
        Snd("SE_CARD_FINISH_BREAK_03"); Shake(0.1f); HitStop(0.03);
        for (int i = 0; i < 10; i++) { float a = i * Mathf.Tau / 10; var rim = p + new Vector3(Mathf.Cos(a) * zone.X * 0.5f, 0.02f, Mathf.Sin(a) * zone.Y * 0.5f); After(R(0, 0.05), () => Puffs(rim, 1, new Color(0.42f, 0.36f, 0.3f, 0.7f), 0.65f, 0.5f, 0.06f)); }
        Streaks(p + Vector3.Up * 0.05f, new Color(0.62f, 0.52f, 0.4f), 18, 2.2f, 7f, Vector3.Up, 0.7f);
        // a beat on the edge, then it tips and drops in, going dark
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
        Gone(m);
        await Wait(1.6);
    }

    // ---------------------------------------------------------------- 90 Ring of Destruction

    Task RingOfDestruction5(int style) => RingOfDestructionFx(1);   // the blast itself got finer (Explosion, FxKit3)

    // ---------------------------------------------------------------- 91 Monster Reborn

    async Task MonsterReborn5(int style)
    {
        var dead = Of(0, "Dark Magician"); Place(dead, GyAt(0));
        await PlayCard("Monster Reborn", FxGreen, sound: "SE_EV_MONSTER_REBORN");
        var gy = GyPos(0); var at = Mon(dead, 2, 0); var zone = Grid3(at);
        var flame = new Color(1f, 0.62f, 0.25f); var hot = new Color(1f, 0.92f, 0.6f);
        // it burns out in the Graveyard
        var ghost = NewSheet(CardArt.Small(dead.Card.Data), dead.Root.GlobalTransform);
        dead.Root.Visible = false;
        FxStand(SoftFireFx, gy, new Vector2(0.9f, 1.3f), 1.1, flame, false, null, ("col2", hot), ("amt", 1.2f));
        BurnAway(ghost, flame, 0.8, new Vector2(0.5f, 1f), 0.1, 1f, 6f);
        await Wait(0.75);
        // the flame leaps over to the zone
        Orb(Line(gy + Vector3.Up * 0.5f, zone + Vector3.Up * 0.3f, 1.0f), hot, 0.45, 0.6f, null, Tween.EaseType.InOut);
        await Wait(0.45);
        FxStand(SoftFireFx, zone, new Vector2(1.0f, 1.4f), 1.2, flame, false, null, ("col2", hot), ("amt", 1.2f));
        FxQuad(GroundGlowFx, zone with { Y = 0.02f }, new Vector2(1.5f, 1.5f), 1.1, flame);
        await BuildIn(dead, at, flame, 0.8, new Vector2(0.5f, 1f), 6f);
        Cue("land");
        await Wait(1.5);
    }

    // ---------------------------------------------------------------- 93 Graceful Charity

    async Task GracefulCharity5(int style)
    {
        var hand = DealHand(0, 3);
        await PlayCard("Graceful Charity", FxGreen, sound: "SE_EV_GHOSTRICK_ANGEL");
        var deck = Grid3(DeckOf(0)); var gy = GyPos(0); var gold = new Color(1f, 0.85f, 0.5f); var soft = new Color(1f, 0.96f, 0.85f);
        if (style == 1)
        {
            var ring = FxQuad(RingFx, deck + Vector3.Up * 0.05f, new Vector2(1.1f, 1.1f), 1.6, gold, Flat);
            ring.CreateTween().TweenProperty(ring, "position:y", 0.9f, 1.4).SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.Out);
            FxQuad(GroundGlowFx, deck with { Y = 0.05f }, new Vector2(1.2f, 1.2f), 1.4, gold);
            await Wait(0.3);
        }
        else if (style == 2)
        {
            var sky = deck + new Vector3(-1.2f, 0, 0.6f);
            FxStand(PillarFx, sky, new Vector2(1.2f, 6f), 2.4, soft, false, null, ("amt", 0.7f));
            await Wait(0.3);
        }
        else
        {
            FxQuad(RingFx, deck + new Vector3(0, 0.5f, -0.2f), new Vector2(0.9f, 0.4f), 1.4, gold, Bill);
            await Wait(0.2);
        }
        await Draws(0, hand, 3, style == 3 ? 0.1 : 0.2);
        foreach (var v in hand.TakeLast(3)) FrameBurst(v, gold, 0.95f, 1.15f, 0.35);
        await Wait(0.4);
        foreach (var v in hand.Take(2).ToList())
        {
            var from = v.Root.GlobalPosition;
            if (style == 1)
            {
                var s = CardSheet(v);
                Flakes(s, 24, uv => new Vector3(R(-0.3, 0.3), R(0.2, 0.5), R(-0.6, -0.2)), 1.3, 0.3, gold, 0.12f, -0.3f);
                BurnAway(s, gold, 0.5, null, 0, 0.3f, 4f);
            }
            else if (style == 2)
            {
                var s = CardSheet(v); var f0 = s.Mi.GlobalTransform;
                s.Mi.CreateTween().TweenMethod(Callable.From<float>(k => s.Mi.GlobalTransform = f0.Translated(Vector3.Up * 2.5f * k * k)), 0f, 1f, 0.6);
                BurnAway(s, Colors.White, 0.4, null, 0.3, 0.4f, 4f);
            }
            else
            {
                for (int i = 0; i < 10; i++) { double d = i * 0.03; After(d, () => Ember(from + new Vector3(R(-0.2, 0.2), 0.1f, R(-0.2, 0.2)), gold, (gy - from) * 1.4f + Vector3.Up * 0.8f, 0.9, 0.05f, -1f)); }
                HideFor(v, 0.9); Place(v, GyAt(0));
                After(0.85, () => FxQuad(GroundGlowFx, gy with { Y = 0.05f }, new Vector2(1f, 1f), 0.4, gold));
            }
            await Wait(0.15);
        }
        await Wait(1.4);
    }

    // ---------------------------------------------------------------- 95 Magic Cylinder

    const string CylBodyCode = @"shader_type spatial;
render_mode cull_disabled;
void fragment() {
  vec2 uv = UV * vec2(10.0, 4.0); vec2 cell = fract(uv) - 0.5; vec2 id = floor(uv);
  float off = mod(id.y, 2.0) * 0.5; cell = fract(uv + vec2(off, 0.0)) - 0.5;
  float a = atan(cell.y, cell.x); float r = length(cell);
  float star = smoothstep(0.02, 0.0, r - (0.12 + 0.07 * cos(a * 5.0)));
  float band = step(0.47, UV.y) * step(UV.y, 0.53) + step(0.08, UV.y) * step(UV.y, 0.11) + step(0.89, UV.y) * step(UV.y, 0.92);
  float rune = step(0.47, UV.y) * step(UV.y, 0.53) * step(0.55, fract(UV.x * 36.0)) * 0.0;
  vec3 lac = mix(vec3(0.22, 0.07, 0.4), vec3(0.4, 0.15, 0.65), UV.y);
  float gold = clamp(star + band, 0.0, 1.0);
  ALBEDO = mix(lac, vec3(1.0, 0.76, 0.3), gold);
  METALLIC = mix(0.25, 0.9, gold); ROUGHNESS = mix(0.18, 0.25, gold);
  EMISSION = vec3(1.0, 0.75, 0.3) * star * 0.35;
}";

    /// A lacquered violet cylinder with gold stars and bands, its mouth (with a turning swirl in it) toward `face`.
    Node3D Cylinder2(Vector3 at, Vector3 face, float size)
    {
        var root = new Node3D { Position = at };
        world.AddChild(root);
        root.LookAt(at + face, Vector3.Up);
        var body = new Node3D { RotationDegrees = new Vector3(90, 0, 0), Scale = Vector3.One * size };
        root.AddChild(body);
        var brass = new StandardMaterial3D { AlbedoColor = new Color(1f, 0.75f, 0.3f), Metallic = 0.9f, Roughness = 0.2f };
        var inner = new StandardMaterial3D { AlbedoColor = new Color(0.04f, 0.02f, 0.07f), CullMode = BaseMaterial3D.CullModeEnum.Disabled };
        body.AddChild(new MeshInstance3D { Mesh = new CylinderMesh { TopRadius = 0.3f, BottomRadius = 0.3f, Height = 0.8f, RadialSegments = 48, Rings = 4, CapTop = true, CapBottom = false }, MaterialOverride = Shade(CylBodyCode) });
        body.AddChild(new MeshInstance3D { Mesh = new CylinderMesh { TopRadius = 0.275f, BottomRadius = 0.275f, Height = 0.79f, RadialSegments = 48, CapTop = false, CapBottom = false, FlipFaces = true }, MaterialOverride = inner });
        body.AddChild(new MeshInstance3D { Mesh = new TorusMesh { InnerRadius = 0.27f, OuterRadius = 0.34f, Rings = 48, RingSegments = 12 }, Position = new Vector3(0, -0.41f, 0), MaterialOverride = brass });   // the lip at the mouth
        body.AddChild(new MeshInstance3D { Mesh = new TorusMesh { InnerRadius = 0.28f, OuterRadius = 0.325f, Rings = 48, RingSegments = 10 }, Position = new Vector3(0, 0.38f, 0), MaterialOverride = brass });
        var sm = Shade(FxAdd.Replace(", depth_test_disabled", "") + VortexFx, ("col", new Color(0.5f, 0.3f, 0.9f)), ("noise", FxNoise), ("col2", new Color(1f, 0.8f, 1f)), ("amt", 4f), ("seed", R(0, 9)));
        var swirl = new MeshInstance3D { Mesh = new QuadMesh { Size = new Vector2(0.5f * size, 0.5f * size) }, MaterialOverride = sm, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off, Position = new Vector3(0, 0, -0.33f * size) };
        root.AddChild(swirl); Run(swirl, sm, 3.0);
        root.Scale = Vector3.One * 0.01f;
        root.CreateTween().TweenProperty(root, "scale", Vector3.One, 0.3).SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
        return root;
    }

    async Task MagicCylinder5(int style)
    {
        var foes = TheirMonsters(); var a = foes[0]; int atk = a.Card.Data.Atk;
        var mine = MineAt("Celtic Guardian", 1);
        var trap = Of(0, "Magic Cylinder"); Place(trap, SpellZ(2, 0, false));
        await Wait(0.4);
        Charge(a, mine.Root.GlobalPosition, 0.35, 0.25f);
        await Wait(0.3);
        await Flip(trap, FxPink, "SE_EV_MAGIC_CYLINDER");
        var mp = mine.Root.GlobalPosition; var ap = a.Root.GlobalPosition; var fire = new Color(1f, 0.55f, 0.25f);
        var toward = (ap - mp) with { Y = 0 };
        var mouth = mp + toward.Normalized() * 0.55f + Vector3.Up * 0.45f;
        var them = new Vector3(2.9f, 0.5f, -2.8f);                        // their LP, top right
        var c2At = ap + new Vector3(0.95f, 0.5f, 0.1f);
        var c1 = Cylinder2(mouth, -toward, 0.85f);
        var c2 = Cylinder2(c2At, them - c2At, 0.85f);
        await Wait(0.35);
        var lip = mouth + toward.Normalized() * 0.4f;
        Orb(Line(ap + Vector3.Up * 0.2f, lip, 0.1f), fire, 0.35, 0.7f, () => { FxQuad(CoreGlowFx, lip, new Vector2(0.8f, 0.8f), 0.2, fire, Bill); Snd("SE_CARD_ST_BREAK"); });
        await Wait(0.6);
        var muzzle = c2At + (them - c2At).Normalized() * 0.42f;   // just in front of its mouth
        FxQuad(CoreGlowFx, muzzle, new Vector2(0.8f, 0.8f), 0.25, fire, Bill);
        Orb(Line(muzzle, them, 0.15f), fire, 0.28, 0.9f, () => { Explosion(them with { Y = 0.25f }, 0.45f); LpHit(1, 8000, 8000 - atk); });
        await Wait(0.7);
        foreach (var c in new[] { c1, c2 }) { var tw = c.CreateTween(); tw.TweenProperty(c, "scale", Vector3.One * 0.01f, 0.25).SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.In); tw.TweenCallback(Callable.From(c.QueueFree)); }
        GoHome(a);
        await Wait(1.5);
    }

    // ---------------------------------------------------------------- 96 Torrential Tribute

    async Task Torrential5(int style)
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
        var water = new Color(0.3f, 0.6f, 0.9f); var foam = new Color(0.88f, 0.96f, 1f);
        void Wash(View v, Vector3 push, double delay)
        {
            After(delay, () =>
            {
                var s = CardSheet(v); var from = s.Mi.Transform;
                s.Set("glow_col", water); Anim(s.M, "glow", 0f, 0.35f, 0.3); Anim(s.M, "flutter", 0f, 0.8f, 0.2);
                s.Mi.CreateTween().TweenMethod(Callable.From<float>(k => s.Mi.Transform = new Transform3D(new Basis(Vector3.Right, -k * 1.2f) * from.Basis, from.Origin + push * k + Vector3.Up * 0.3f * Mathf.Sin(k * Mathf.Pi))), 0f, 1f, 0.55);
                Streaks(from.Origin + Vector3.Up * 0.1f, foam, 14, 2.6f, 6f, Vector3.Up, 0.6f);
                Puffs(from.Origin + Vector3.Up * 0.1f, 2, new Color(0.85f, 0.93f, 1f, 0.45f), 0.6f, 0.5f, 0.2f);
                Flakes(s, 16, uv => push * 1.4f + Vector3.Up * R(0.3, 0.9), 0.6, 0.2, foam, 0.1f, 2f);
                BurnAway(s, foam, 0.35, null, 0.25, 0.2f, 2f);
            });
        }
        var tp = trap.Root.GlobalPosition + Vector3.Up * 0.15f;
        if (style == 1)
        {
            FxStand(PillarFx, tp, new Vector2(0.9f, 1.8f), 0.6, foam, false, null, ("amt", 1f));
            Streaks(tp, foam, 26, 3.8f, 6f, Vector3.Up, 0.4f);
            await Wait(0.25);
            foreach (var v in all)
            {
                var to = v.Root.GlobalPosition + Vector3.Up * 0.1f;
                Beam(tp + Vector3.Up * 0.4f, to, 0.4f, 0.5, water); Beam(tp + Vector3.Up * 0.4f, to, 0.15f, 0.4, foam);
                Wash(v, (to - tp).Normalized() with { Y = 0 } * 0.9f, 0.2);
            }
            await Wait(1.2);
        }
        else if (style == 2)
        {
            ScreenDim(0.45f, 0.3, 1.4, 0.5);
            for (int k = 0; k < 28; k++) { double d = 0.2 + k * 0.05; After(d, () => Streaks(new Vector3(R(-3.4, 3.4), 3.2f, R(-2.2, 2.2)), foam, 14, 6f, 9f, new Vector3(0.15f, -1f, 0), 0.08f)); }
            for (int k = 0; k < 14; k++) { double d = 0.5 + k * 0.08; After(d, () => FxQuad(RingFx, new Vector3(R(-3, 3), 0.03f, R(-2, 2)), new Vector2(0.5f, 0.5f), 0.4, foam)); }
            Snd("SE_SOLO_WIND_01", 0.2);
            foreach (var v in all) Wash(v, new Vector3(R(-0.3, 0.3), 0, R(-0.3, 0.3)), 0.8 + R(0, 0.35));
            await Wait(1.9);
        }
        else
        {
            var c = new Vector3(0, 0.04f, 0);
            var pool = FxQuad(VortexFx, c, new Vector2(7.2f, 5.0f), 2.4, water * new Color(0.7f, 0.7f, 0.7f), Flat, false, null, ("col2", foam), ("amt", 2.5f));
            FxQuad(CoreFx, c + Vector3.Up * 0.005f, new Vector2(0.7f, 0.5f), 2.4, water, Flat, true);
            Snd("SE_EV_CONVULSION_OF_NATURE_b", 0.1);
            for (int i = 0; i < all.Count; i++)
            {
                var v = all[i]; double d = 0.4 + i * 0.07;
                After(d, () =>
                {
                    var s = CardSheet(v); var from = s.Mi.GlobalTransform; var off = from.Origin - c;
                    s.Set("glow_col", water); Anim(s.M, "glow", 0f, 0.4f, 0.8); Anim(s.M, "darken", 0f, 0.7f, 1.2);
                    s.Mi.CreateTween().TweenMethod(Callable.From<float>(k =>
                    {
                        float e = k * k, a = e * 7f;
                        var p = c + new Vector3(off.X * Mathf.Cos(a) - off.Z * Mathf.Sin(a), 0.02f, off.X * Mathf.Sin(a) + off.Z * Mathf.Cos(a)) * (1 - e);
                        s.Mi.GlobalTransform = new Transform3D(new Basis(Vector3.Up, a) * from.Basis, p).ScaledLocal(Vector3.One * (1 - 0.85f * e));
                    }), 0f, 1f, 1.3);
                    After(1.3, () => { if (IsInstanceValid(s.Mi)) s.Mi.QueueFree(); });
                });
            }
            await Wait(2.1);
            Streaks(c + Vector3.Up * 0.1f, foam, 20, 3f, 6f, Vector3.Up, 0.5f);
        }
        await Wait(1.0);
    }

    // ---------------------------------------------------------------- 97 Solemn Judgment

    /// A gavel: gold head with darker bands, a handle.
    Node3D Gavel(Vector3 at, float size)
    {
        var root = new Node3D { Position = at, Scale = Vector3.One * size };
        world.AddChild(root);
        var gold = new StandardMaterial3D { AlbedoColor = new Color(1f, 0.8f, 0.35f), Metallic = 0.85f, Roughness = 0.22f, EmissionEnabled = true, Emission = new Color(0.25f, 0.17f, 0.04f) };
        var dark = new StandardMaterial3D { AlbedoColor = new Color(0.45f, 0.28f, 0.08f), Metallic = 0.6f, Roughness = 0.35f };
        root.AddChild(new MeshInstance3D { Mesh = new CylinderMesh { TopRadius = 0.22f, BottomRadius = 0.22f, Height = 0.75f, RadialSegments = 32 }, RotationDegrees = new Vector3(0, 0, 90), MaterialOverride = gold });
        foreach (float x in new[] { -0.3f, 0.3f }) root.AddChild(new MeshInstance3D { Mesh = new CylinderMesh { TopRadius = 0.235f, BottomRadius = 0.235f, Height = 0.06f, RadialSegments = 32 }, RotationDegrees = new Vector3(0, 0, 90), Position = new Vector3(x, 0, 0), MaterialOverride = dark });
        root.AddChild(new MeshInstance3D { Mesh = new CylinderMesh { TopRadius = 0.05f, BottomRadius = 0.06f, Height = 1.1f, RadialSegments = 16 }, Position = new Vector3(0, 0.62f, 0), MaterialOverride = dark });
        return root;
    }

    async Task Solemn5(int style)
    {
        var trap = Of(0, "Solemn Judgment"); Place(trap, SpellZ(2, 0, false));
        var m = Of(1, "Summoned Skull"); Place(m, HandOf(1, 0, 1, false));
        await Wait(0.4);
        Move(m, Mon(m, 2, 1), 0, 0.45f, 0.5f);
        await Wait(0.55);
        await Flip(trap, FxPink, "SE_EV_SOLEMNJUDGMENT");
        LpHit(0, 8000, 4000);
        var p = m.Root.GlobalPosition; var gold = new Color(1f, 0.85f, 0.45f);
        if (style == 1)
        {
            var g = Gavel(p + new Vector3(0.9f, 1.2f, 0.2f), 0.9f);
            g.Rotation = new Vector3(0, 0, -1.1f);
            var tw = g.CreateTween();
            tw.TweenProperty(g, "rotation:z", -1.5f, 0.3).SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.Out);   // raised
            tw.TweenProperty(g, "rotation:z", 0.35f, 0.14).SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.In);   // down
            tw.Parallel().TweenProperty(g, "position", p + new Vector3(0.05f, 0.3f, 0.1f), 0.14).SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.In);
            await Wait(0.45);
            Shake(0.16f); HitStop(0.06); Snd("SE_CARD_FINISH_BREAK_02"); ScreenFlash(gold, 0.25f, 0.2);
            for (int i = 0; i < 3; i++) { int k = i; After(i * 0.1, () => FxQuad(RingFx, p with { Y = 0.03f }, new Vector2(1.0f + k * 0.5f, 1.0f + k * 0.5f), 0.45, gold)); }
            Warp(p + Vector3.Up * 0.05f, 2.6f, 0.4, 1.4f);
            Burn(m, gold, 0.6, new Vector2(0.5f, 0.5f), 0.05, 1f, 6f);
            var t2 = g.CreateTween(); t2.TweenInterval(0.35); t2.TweenProperty(g, "scale", Vector3.One * 0.01f, 0.3).SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.In); t2.TweenCallback(Callable.From(g.QueueFree));
        }
        else if (style == 2)
        {
            for (int i = 0; i < 4; i++)
            {
                int k = i;
                After(i * 0.15, () =>
                {
                    var r = FxQuad(RingFx, p + Vector3.Up * (0.2f + k * 0.2f), new Vector2(1.6f, 1.6f), 0.9 - k * 0.1, gold, Flat);
                    r.CreateTween().TweenProperty(r, "scale", Vector3.One * 0.45f, 0.5).SetDelay(0.2).SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.In);
                    Snd("SE_DUEL_TIME_COUNT");
                });
            }
            await Wait(0.95);
            ScreenFlash(Colors.White, 0.3f, 0.25); Flash(p + Vector3.Up * 0.4f, gold, 9f); HitStop(0.05);
            var s = CardSheet(m); s.Set("glow_col", Colors.White); Anim(s.M, "glow", 0f, 3f, 0.2);
            BurnAway(s, Colors.White, 0.4, null, 0.15, 0.6f, 4f);
        }
        else
        {
            var lp = new Vector3(-3.0f, 0.3f, 2.4f); var tp = trap.Root.GlobalPosition + Vector3.Up * 0.2f;
            SoulStream(lp, tp, 20, gold, 0.6, 0.6f, 0.02);
            await Wait(0.75);
            FrameBurst(trap, gold, 0.9f, 1.3f, 0.4);
            Beam(tp, p + Vector3.Up * 0.1f, 0.5f, 0.45, gold); Beam(tp, p + Vector3.Up * 0.1f, 0.18f, 0.35, Colors.White);
            await Wait(0.12);
            Shake(0.1f); HitStop(0.04);
            Burn(m, gold, 0.55, new Vector2(0.5f, 1f), 0, 1f, 6f);
        }
        await Wait(1.8);
    }

    // ---------------------------------------------------------------- 98-101 summons

    async Task BlueEyes5(int style)
    {
        var v = Of(0, "Blue-Eyes White Dragon"); var at = Mon(v, 2, 0); var p = Grid3(at);
        var ice = new Color(0.65f, 0.85f, 1f);
        Snd("SE_MONSTER_CUTIN_LIGHT");
        for (int i = 0; i < 4; i++) { float a = i * Mathf.Tau / 4 + 0.4f; var q = p + new Vector3(Mathf.Cos(a) * 0.9f, 0, Mathf.Sin(a) * 0.8f); double d = i * 0.12; After(d, () => LightningHit(q, ice, 0.55f, dim: false)); }
        await Wait(0.6);
        ScreenFlash(ice, 0.25f, 0.3);
        await BuildIn(v, at, new Color(0.7f, 0.88f, 1f), 0.75, new Vector2(0.5f, 0.5f), 7f, (s, k) => { if (GD.Randf() < 0.7f) Crawl(s, ice); s.Set("glow_col", ice); s.Set("glow", (1 - k) * 0.6f); });
        Warp(p + Vector3.Up * 0.05f, 4f, 0.5, 1.6f); Shake(0.1f); FxQuad(GroundGlowFx, p with { Y = 0.02f }, new Vector2(1.6f, 1.6f), 0.5, ice);
        Cue("land");
        await Wait(1.5);
    }

    async Task DarkMagician5(int style)
    {
        var v = Of(0, "Dark Magician"); var at = Mon(v, 2, 0); var p = Grid3(at);
        var violet = new Color(0.7f, 0.4f, 1f);
        Snd("SE_MONSTER_CUTIN_DARK");
        FxQuad(CircleFx, p + Vector3.Up * 0.02f, new Vector2(2.2f, 2.2f), 2.2, violet, Flat, false, null, ("amt", 1.5f));
        for (int i = 0; i < 10; i++) { float a = i * Mathf.Tau / 10; var q = p + new Vector3(Mathf.Cos(a) * 0.95f, -0.05f, Mathf.Sin(a) * 0.95f); double d = 0.3 + i * 0.035; After(d, () => FxStand(SoftFireFx, q, new Vector2(0.5f, 0.8f), 1.4, violet, false, null, ("col2", new Color(1f, 0.85f, 1f)), ("amt", 1f))); }
        await Wait(0.7);
        await BuildIn(v, at, violet, 0.8, new Vector2(0.5f, 1f), 6f, null, 1.2f);
        FxQuad(CoreGlowFx, p + Vector3.Up * 0.2f, new Vector2(1.0f, 1.0f), 0.35, violet, Bill);
        Cue("land");
        await Wait(1.5);
    }

    async Task RedEyes5(int style)
    {
        var v = Of(0, "Red-Eyes B. Dragon"); var at = Mon(v, 2, 0); var p = Grid3(at);
        var red = new Color(1f, 0.3f, 0.2f);
        Snd("SE_MONSTER_CUTIN_FIRE");
        for (int i = 0; i < 4; i++) { float a = i * Mathf.Tau / 4 + 0.4f; var q = p + new Vector3(Mathf.Cos(a) * 0.9f, 0, Mathf.Sin(a) * 0.8f); double d = i * 0.12; After(d, () => LightningHit(q, red, 0.55f, dim: false)); }
        await Wait(0.6);
        ScreenDim(0.55f, 0.2, 0.9, 0.4);
        await Wait(0.3);
        foreach (int s in new[] { -1, 1 }) FxQuad(CoreGlowFx, p + new Vector3(s * 0.22f, 0.9f, -0.5f), new Vector2(0.35f, 0.35f), 0.8, red, Bill);
        Snd("SE_CARD_ST_BREAK", 0.1);
        await Wait(0.55);
        await BuildIn(v, at, red, 0.7, new Vector2(0.5f, 0.5f), 7f, (s, k) => { if (GD.Randf() < 0.4f) Crawl(s, red); });
        FxQuad(GroundGlowFx, p with { Y = 0.02f }, new Vector2(1.6f, 1.6f), 0.5, red); Shake(0.08f);
        Cue("land");
        await Wait(1.5);
    }

    async Task Obelisk5(int style)
    {
        var tribs = new[] { MineAt("Celtic Guardian", 1), MineAt("Feral Imp", 2), MineAt("Curse of Dragon", 3) };
        await Wait(0.4);
        var v = Of(0, "Obelisk the Tormentor"); var at = Mon(v, 2, 0); var p = Grid3(at);
        var blue = new Color(0.35f, 0.6f, 1f); var pale = new Color(0.75f, 0.88f, 1f);
        Snd("SE_MONSTER_CUTIN_DIVINE");
        // the three tributes go up as blue souls
        foreach (var t in tribs) { var s = CardSheet(t); BurnAway(s, blue, 0.5, null, 0, 0.6f, 5f); SoulStream(t.Root.GlobalPosition, p + new Vector3(0, 2.5f, -1f), 8, blue, 0.7, 0.6f, 0.03); }
        await Wait(0.75);
        if (style == 1)
        {
            ScreenDim(0.55f, 0.3, 1.3, 0.6); Vignette(new Color(0.05f, 0.12f, 0.4f), 2.0);
            await Wait(0.4);
            LightningHit(p, pale, 2.0f, dim: false);
            await Wait(0.12);
            FieldLines(0f, 0.15);
            Warp(p + Vector3.Up * 0.05f, 9f, 0.8, 3f); Shake(0.35f); HitStop(0.08); ScreenFlash(blue, 0.45f, 0.4);
            for (int i = 0; i < 12; i++) Puffs(p + new Vector3(Mathf.Cos(i * 0.52f) * 1.2f, 0.05f, Mathf.Sin(i * 0.52f) * 1.0f), 1, new Color(0.4f, 0.45f, 0.55f, 0.6f), 0.9f, 0.4f, 0.1f);
            await BuildIn(v, at, pale, 0.7, new Vector2(0.5f, 0.5f), 8f, (s, k) => { if (GD.Randf() < 0.6f) Crawl(s, pale); });
            FieldLines(1f, 0.6);
        }
        else if (style == 2)
        {
            FxStand(PillarFx, p, new Vector2(2.2f, 7f), 2.0, blue, false, null, ("amt", 1.4f));
            FxStand(PillarFx, p, new Vector2(0.7f, 7f), 1.8, pale, false, null, ("amt", 1f));
            var ring = FxQuad(CircleFx, p + Vector3.Up * 0.03f, new Vector2(3.0f, 3.0f), 2.2, blue, Flat, false, null, ("amt", 0.8f));
            ring.CreateTween().TweenProperty(ring, "position:y", 1.6f, 2.0).SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.InOut);
            await Wait(0.6);
            // the blast of wind: every card on the field shudders
            FxQuad(RingFx, p with { Y = 0.05f }, new Vector2(4f, 4f), 0.7, pale); Warp(p + Vector3.Up * 0.05f, 8f, 0.7, 1.6f); Shake(0.25f);
            foreach (var c in views.Values.Where(x => x.Root.Visible && x.S.Loc is Loc.Monster or Loc.Spell).ToList()) Jolt(c, (c.Root.GlobalPosition - p).Normalized() * 0.25f);
            Snd("SE_ATTACK_A_WIND_SPECIAL_01");
            await BuildIn(v, at, pale, 0.8, new Vector2(0.5f, 0f), 7f);
        }
        else
        {
            for (int i = 0; i < 9; i++)
            {
                float a = R(0, 6.28); var q = p + new Vector3(Mathf.Cos(a) * R(0.8, 1.5), 0, Mathf.Sin(a) * R(0.7, 1.2));
                LightningHit(q, i % 2 == 0 ? blue : pale, 0.5f, dim: false);
                await Wait(0.24 - i * 0.02);
            }
            ScreenDim(0.4f, 0.1, 0.3, 0.3);
            await Wait(0.2);
            LightningHit(p, pale, 2.2f, dim: false);
            await Wait(0.12);
            Warp(p + Vector3.Up * 0.05f, 8f, 0.7, 2.6f); Shake(0.3f); HitStop(0.08);
            await BuildIn(v, at, pale, 0.55, new Vector2(0.5f, 0.5f), 8f, (s, k) => { if (GD.Randf() < 0.6f) Crawl(s, pale); });
        }
        Cue("land");
        await Wait(1.6);
    }

    // ---------------------------------------------------------------- 102 Jinzo

    const string LockFx = @"void fragment() {
  vec2 p = UV * 2.0 - 1.0; float r = length(p); float a = atan(p.y, p.x);
  float ring = exp(-pow((r - 0.8) / 0.03, 2.0)) + exp(-pow((r - 0.62) / 0.02, 2.0));
  float ticks = step(0.66, r) * step(r, 0.76) * step(0.6, fract(a / 6.28318 * 16.0 + TIME * 0.3));
  vec2 q = abs(p); float bar = step(q.x, 0.06) * step(q.y, 0.5) + step(q.y, 0.06) * step(q.x, 0.5);
  float v = ring + ticks * 0.6 + bar * 0.9;
  float draw = step(fract(a / 6.28318 + 0.5), smoothstep(0.0, 0.25, t) * 1.01);
  ALBEDO = mix(col.rgb, vec3(1.0), clamp(bar * 0.3 + ring * 0.2, 0.0, 1.0)); ALPHA = clamp(v, 0.0, 1.0) * max(draw, bar * step(0.25, t)) * (1.0 - smoothstep(0.75, 1.0, t));
}";

    async Task Jinzo5(int style)
    {
        var board = RivalBoard(); var traps = board.Where(v => v.S.Loc == Loc.Spell).ToList();
        foreach (var (name, zone) in new[] { ("Mirror Force", 0), ("Trap Hole", 2) }) { var tv = Of(1, name); Place(tv, SpellZ(zone, 1, false)); traps.Add(tv); }
        var j = Of(0, "Jinzo"); var at = Mon(j, 2, 0);
        Snd("SE_EV_IMPERIAL_ORDER");
        await BuildIn(j, at, new Color(1f, 0.3f, 0.3f), 0.45, null, 4f, null, 0.5f);
        var jp = j.Root.GlobalPosition; var red = new Color(1f, 0.22f, 0.2f);
        void Dead(View tv, double after)
        {
            After(after, () =>
            {
                var s = Overlay(tv, 1.6);
                s.Set("glitch_col", red); Anim(s.M, "glitch", 0f, 1f, 0.15); Anim(s.M, "glitch", 1f, 0.15f, 0.9, 0.5);
                Anim(s.M, "darken", 0f, 0.55f, 1.0, 0.4);
                After(1.6, () => { tv.Face.AlbedoColor = new Color(0.45f, 0.43f, 0.48f); });
                Snd("SE_SOLO_GATE078_ELECTRIC");
            });
        }
        if (style == 1)
        {
            for (int i = 0; i < 3; i++) { int k = i; After(i * 0.18, () => { FxQuad(RingFx, jp with { Y = 0.05f }, new Vector2(1.6f, 1.6f), 0.5, red); Warp(jp + Vector3.Up * 0.05f, 7f, 0.9, 0.9f); }); }
            FxQuad(GroundGlowFx, jp with { Y = 0.03f }, new Vector2(1.4f, 1.4f), 0.8, red);
            foreach (var tv in traps) Dead(tv, (jp - tv.Root.GlobalPosition).Length() / 4.5 * 0.9);
            await Wait(1.6);
        }
        else if (style == 2)
        {
            foreach (var tv in traps.OrderBy(x => x.Root.GlobalPosition.X))
            {
                FxQuad(LockFx, tv.Root.GlobalPosition + new Vector3(0, 0.15f, 0), new Vector2(0.75f, 0.75f), 1.4, red, Flat);
                Dead(tv, 0.3);
                await Wait(0.25);
            }
            await Wait(1.0);
        }
        else
        {
            ScreenDim(0.5f, 0.2, 1.0, 0.4);
            await Wait(0.25);
            var eyes = jp + new Vector3(0, 0.55f, -0.35f);
            foreach (int s in new[] { -1, 1 }) FxQuad(CoreGlowFx, eyes + new Vector3(s * 0.08f, 0, 0), new Vector2(0.4f, 0.4f), 0.5, red, Bill);
            ScreenFlash(red, 0.2f, 0.2);
            await Wait(0.2);
            foreach (var tv in traps) { var tp = tv.Root.GlobalPosition + Vector3.Up * 0.03f; Beam(eyes, tp, 0.08f, 0.6, red); Dead(tv, 0.15); }
            await Wait(1.4);
        }
        await Wait(1.0);
    }
}
