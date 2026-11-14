using Godot;
using PoC.Engine;

namespace PoC.Game.UI;

/// FX Lab "signature cards", round 4: the 19 cards that were waiting (2026-11-13). Three different ideas each, each
/// with its own sound; built on DuelField.FxKit3.cs (cards as sheets that bend, burn and build up; no crack decals,
/// no square particles).
public partial class DuelField
{
    void Batch2Rows(Action<int, string, Func<int, Task>, string[]> row)
    {
        row(73, "Mirror Force", MirrorForceFx, new[] {
            "Mirror wall: a pane of mirror light rises along YOUR front row, the attack hits it and comes back as one beam to each of their attackers",
            "Dome: a dome of mirror light closes over your side, the hit ripples over it, and a wave runs back over their side burning each attacker",
            "Shards: the barrier takes the hit and bursts into glass shards that fly back into each of their attackers" });
        row(74, "Negate Attack", NegateAttackFx, new[] {
            "Swallowed: a small vortex opens in front of your monster, the attack's charge is drawn into it and it snaps shut; the attacker drifts home",
            "Time stop: a pale wave crosses the field, the attacker freezes grey mid-charge, then slides back to its zone",
            "Bell: rings of light ring out where the hit would land, the attacker bounces off them with sparks" });
        row(75, "Harpie's Feather Duster", FeatherDusterFx, new[] {
            "Sweep: one great feather of light sweeps along their back row and each card it brushes bursts into feathers",
            "Feather fall: feathers of light drift down onto each of their cards and each turns to feathers where it lies",
            "Wingbeat: two wings of light beat once behind your card and the gust carries their cards off in flakes" });
        row(76, "Banished (removed from play)", BanishFx, new[] {
            "Rift: a thin black slit opens over the card and it is drawn edge-first into it; the slit shuts on a glint",
            "Fold away: the card folds in half and in half again into a point, which blinks out with a dark ring",
            "Phase out: the card goes cold and see-through, burns away in pale light, and a streak runs to the banished pile" });
        row(91, "Monster Reborn", MonsterRebornFx, new[] {
            "Ankh: an ankh of light rises over your Graveyard, light runs from it to the zone and the monster builds up there",
            "Pillar: a pillar of light comes down on the zone and the monster builds up inside it from the top",
            "Soul flame: a flame lifts out of the Graveyard, arcs to the zone and swells into the monster" });
        row(92, "Pot of Greed", PotOfGreedFx, new[] {
            "The pot: a glazed green pot appears over your card, tips, and two cards fly out of its mouth into your hand",
            "Green pull: two green lights leave the card for your Deck and pull its top two cards up into your hand",
            "Overflow: green light wells up out of the card and spills to your Deck, lifting two cards off it" });
        row(93, "Graceful Charity", GracefulCharityFx, new[] {
            "Angel's light: soft light falls on your Deck, three cards rise in it to your hand, two others float to the Graveyard as feathers",
            "Halo: a halo forms over your hand, gold feathers drift down; three cards come, two burn away white",
            "Gift: the Deck glows, three cards are drawn on ribbons of light, two leave your hand as motes to the Graveyard" });
        row(94, "Brain Control", BrainControlFx, new[] {
            "Waves: rings of violet thought pulse from your card to the monster, it takes their colour and crosses",
            "Strings: threads of light come down on the monster's corners and lift it across like a puppet",
            "Eye: an eye of light opens over the monster, looks, flashes, and the monster crosses" });
        row(95, "Magic Cylinder", MagicCylinderFx, new[] {
            "Twin cylinders: one swallows the attack in front of your monster, the other turns on their side and fires it back at them",
            "Portal rings: the attack goes into a ring of light and comes out of another above their side, straight at them",
            "Thrown back: the attack's charge is caught, swung once round your monster and hurled back as a comet" });
        row(96, "Torrential Tribute", TorrentialTributeFx, new[] {
            "Wave: a wall of water rolls across the whole table and washes every monster away",
            "Geysers: water bursts up under each monster and tosses it away in spray",
            "Flood: water rises over the table and every monster sinks under it" });
        row(97, "Solemn Judgment", SolemnJudgmentFx, new[] {
            "Verdict: half your LP drains as gold light up into the sky and comes down as one beam through the card",
            "Seal: a gold seal turns over the card and stamps down; the card burns away from the seal",
            "Bars: gold bars of judgment close round the card, tighten, and it goes out in white light" });
        row(98, "Summon: Blue-Eyes White Dragon", BlueEyesFx, new[] {
            "White lightning: white bolts ring the zone, the card builds up in a burst of pale blue wind",
            "Wing shadow: a great blue light sweeps over the table like a wing, then the card lands with a cold shock",
            "Breath: a column of white-blue light blasts up from the zone and the card forms in it" });
        row(99, "Summon: Dark Magician", DarkMagicianFx, new[] {
            "Circle: a dark magic circle opens, violet fire licks up round it, the card rises out of it",
            "Smoke: dark smoke gathers on the zone and clears off the card with a spark of light",
            "Runes: points of violet light orbit the zone, close in and condense into the card" });
        row(100, "Summon: Red-Eyes B. Dragon", RedEyesFx, new[] {
            "Black fire: a column of dark red fire stands on the zone and burns down to the card",
            "Eyes in the dark: the table dims, two red glints open over the zone, and a burst of fire shows the card",
            "Meteor: a ball of fire falls onto the zone, and the card stands in the smoke" });
        row(101, "Summon: Egyptian God (Obelisk)", EgyptianGodFx, new[] {
            "Divine pillar: the three tributes go up, the table dims, a huge gold pillar comes down and the card forms in it",
            "Rising light: gold light wells up out of the zone in rings, and the card rises from it with the ground shaking",
            "Sun: three soul lights meet over the zone, become a small sun, and it collapses into the card" });
        row(102, "Jinzo", JinzoFx, new[] {
            "Scan: a sweep of red light fans out from Jinzo over their back row; each trap it crosses fizzles dark with static",
            "Eye beams: two red beams trace each of their traps, which dim and lock",
            "Pulse: an electric ring bursts out of Jinzo; arcs jump onto their traps and they go dead" });
        row(103, "Time Wizard", TimeWizardFx, new[] {
            "Roulette: a clock face over Time Wizard, the hand whirls and stops; their monsters crumble to dust",
            "Years pass: clock rings ripple out over the table; their monsters fade, yellow, and fall to dust",
            "Sand: a small clock over each of their monsters runs fast and each one pours away as sand" });
        row(104, "Chaos summon (Envoy of the Beginning)", ChaosFx, new[] {
            "Spiral: a light and a dark orb rise from your Graveyard, spiral round the zone, meet in a flash and become the card",
            "Pillars: a white and a black pillar stand side by side, lean in and crash together into the card",
            "Balance: a disc of turning light and dark opens on the zone, and the card builds up from its middle" });
    }

    // ---------------------------------------------------------------- staging

    View MineAt(string name, int zone, bool up = true, bool def = false) { var v = Of(0, name); Place(v, Mon(v, zone, 0, up, def)); return v; }

    async Task<View> SetAndFlip(string name, Color tint, string sound)
    {
        var t = Of(0, name); Place(t, SpellZ(2, 0, false));
        await Wait(0.45);
        await Flip(t, tint, sound);
        return t;
    }

    /// A card leans toward `to` and holds there (an attack mid-charge).
    void Charge(View a, Vector3 to, double dur = 0.35, float reach = 0.4f)
    {
        var from = a.Root.Transform;
        Path(a, 0, dur, k => new Transform3D(from.Basis, from.Origin.Lerp(to, reach * Ease(k)) + Vector3.Up * 0.25f * Ease(k)));
    }

    void GoHome(View a, float dur = 0.4f) => Move(a, a.S, 0, dur, 0.3f);

    const string OrbFx = @"void fragment() {
  float r = length(UV * 2.0 - 1.0); float n = texture(noise, UV * 1.5 + vec2(seed, TIME * 2.0)).r;
  float g = exp(-r * r * 12.0) * 1.4 + exp(-r * 4.0) * 0.4 * (0.7 + 0.6 * n);
  ALBEDO = mix(col.rgb, vec3(1.0), exp(-r * r * 30.0)); ALPHA = clamp(g * amt, 0.0, 1.0) * smoothstep(1.0, 0.8, r);
}";

    /// A ball of energy flying a -> b along `path` (0..1 -> point), trailing embers; `then` at arrival.
    MeshInstance3D Orb(Func<float, Vector3> path, Color c, double dur, float size = 0.6f, Action? then = null, Tween.EaseType ease = Tween.EaseType.In)
    {
        var q = FxQuad(OrbFx, path(0), new Vector2(size, size), dur + 0.1, c, Bill, false, null, ("amt", 1f));
        var tw = q.CreateTween();
        float last = -1;
        tw.TweenMethod(Callable.From<float>(k =>
        {
            q.Position = path(k);
            if (k - last > 0.06f) { last = k; Ember(q.Position, c, new Vector3(R(-0.1, 0.1), R(0, 0.2), R(-0.1, 0.1)), 0.45, size * 0.18f, 0); }
        }), 0f, 1f, dur).SetTrans(Tween.TransitionType.Sine).SetEase(ease);
        if (then != null) tw.TweenCallback(Callable.From(then));
        return q;
    }

    static Func<float, Vector3> Line(Vector3 a, Vector3 b, float arc = 0) => k => a.Lerp(b, k) + Vector3.Up * Mathf.Sin(k * Mathf.Pi) * arc;

    Sheet NewSheet(Texture2D tex, Transform3D xf)
    {
        var m = Shade(SheetCode, ("tex", tex), ("noise", FxNoise), ("seed", R(0, 9)), ("card", new Vector2(CW, CH)));
        var mi = new MeshInstance3D { Mesh = new PlaneMesh { Size = new Vector2(CW, CH), SubdivideWidth = 16, SubdivideDepth = 22 }, MaterialOverride = m, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            CustomAabb = new Aabb(new Vector3(-4, -4, -4), new Vector3(8, 8, 8)) };
        world.AddChild(mi);
        mi.GlobalTransform = xf.TranslatedLocal(new Vector3(0, 0.006f, 0));
        liveSheets.Add(mi);
        return new Sheet { Mi = mi, M = m, Tex = tex };
    }

    /// The card builds up at `at` out of nothing (the burn run backward), its edge glowing, from `origin` or all over.
    async Task Materialise(View v, St at, Color edge, double dur, Vector2? origin = null, float edgeE = 5f, float embers = 1f)
    {
        Place(v, at);
        HideFor(v, dur + 0.02);
        var s = NewSheet(CardArt.Small(v.Card.Data), Target(v, at));
        s.Set("edge_col", edge); s.Set("edge_e", edgeE); s.Set("burn", 1f);
        if (origin is { } o) { s.Set("origin", o); s.Set("from_pt", 1f); }
        Anim(s.M, "burn", 1f, 0.0006f, dur, 0, Tween.TransitionType.Sine, Tween.EaseType.Out);
        for (int i = 0; i < (int)(18 * embers); i++) { double d = dur * i / (18 * embers); After(d, () => { if (IsInstanceValid(s.Mi)) Ember(s.World(new Vector2(R(0.05, 0.95), R(0.05, 0.95))), edge, new Vector3(0, R(0.2, 0.5), 0), 0.6, 0.035f, 0.4f); }); }
        await Wait(dur);
        s.Mi.QueueFree();
        UpdateStat(v, false);
    }

    /// A card burns away where it lies (its own sheet), from `origin` or all over.
    Sheet Burn(View v, Color edge, double dur, Vector2? origin = null, double delay = 0, float embers = 1f, float edgeE = 5f)
    {
        var s = CardSheet(v);
        BurnAway(s, edge, dur, origin, delay, embers, edgeE);
        return s;
    }

    // ---------------------------------------------------------------- 73 Mirror Force

    const string DomeCode = @"shader_type spatial;
render_mode unshaded, blend_add, cull_disabled, depth_draw_never, shadows_disabled;
uniform vec4 col : source_color; uniform float amt = 0.0; uniform vec3 hit = vec3(0.0, 0.5, -1.0); uniform float rip = 0.0; uniform sampler2D noise : repeat_enable, filter_linear_mipmap;
varying vec3 lp;
void vertex() { lp = VERTEX; }
void fragment() {
  float f = pow(1.0 - abs(dot(normalize(NORMAL), normalize(VIEW))), 2.2);
  float sheen = pow(texture(noise, vec2(lp.x * 0.4 + TIME * 0.1, lp.y * 0.6)).r, 3.0);
  float d = distance(normalize(lp), normalize(hit));
  float ring = exp(-pow((d - rip * 2.2) / 0.08, 2.0)) * (1.0 - rip) * step(0.001, rip);
  float bottom = smoothstep(0.0, 0.08, lp.y);
  ALBEDO = mix(col.rgb, vec3(1.0), ring); ALPHA = clamp((f * 0.55 + sheen * 0.25 + ring * 1.5) * amt * bottom, 0.0, 1.0);
}";

    async Task MirrorForceFx(int style)
    {
        var foes = TheirMonsters();
        var mine = MineAt("Celtic Guardian", 2);
        var a = foes[1];
        var trap = Of(0, "Mirror Force"); Place(trap, SpellZ(2, 0, false));
        await Wait(0.4);
        Charge(a, mine.Root.GlobalPosition, 0.35, 0.35f);
        await Wait(0.3);
        await Flip(trap, FxPink, "SE_EV_MIRRORFORCE");
        var silver = new Color(0.78f, 0.9f, 1f);
        float frontZ = mine.Root.GlobalPosition.Z - CH * 0.75f;
        var ap = a.Root.GlobalPosition;
        // the attack's charge flies at your side
        var hitAt = new Vector3(0, 0.45f, frontZ);
        Orb(Line(ap + Vector3.Up * 0.2f, hitAt, 0.2f), new Color(1f, 0.6f, 0.3f), 0.3, 0.7f);
        if (style == 1)
        {
            var pane = FxQuad(HexFx, new Vector3(0, 0.55f, frontZ), new Vector2(5.6f, 1.3f), 1.9, silver, new Basis(Vector3.Right, -0.75f), false, null, ("amt", 1f), ("cells", new Vector2(22f, 4f)));
            pane.Scale = new Vector3(1, 0.05f, 1);
            pane.CreateTween().TweenProperty(pane, "scale", Vector3.One, 0.25).SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
            await Wait(0.32);
            Warp(hitAt, 2.2f, 0.4, 1.4f, false, Bill); Flash(hitAt, silver, 8f); ScreenFlash(silver, 0.25f, 0.2); Shake(0.08f); HitStop(0.05);
            Streaks(hitAt, Colors.White, 14, 3f);
            await Wait(0.12);
            foreach (var f in foes.Where(f => !f.S.Def))
            {
                var to = f.Root.GlobalPosition + Vector3.Up * 0.1f;
                Beam(hitAt, to, 0.35f, 0.5, silver);
                After(0.12, () => Burn(f, new Color(0.75f, 0.9f, 1f), 0.55, new Vector2(0.5f, 0.85f), 0, 1f, 6f));
            }
            Snd("SE_CARD_FINISH_BREAK_02", 0.12);
        }
        else if (style == 2)
        {
            var m = Shade(DomeCode, ("col", silver), ("noise", FxNoise), ("amt", 0f));
            var dome = new MeshInstance3D { Mesh = new SphereMesh { Radius = 1, Height = 2, RadialSegments = 64, Rings = 32, IsHemisphere = true }, MaterialOverride = m, Position = new Vector3(0, 0, 1.45f), Scale = new Vector3(3.3f, 1.1f, 1.25f), CastShadow = GeometryInstance3D.ShadowCastingSetting.Off };
            world.AddChild(dome);
            Anim(m, "amt", 0f, 1f, 0.25);
            await Wait(0.3);
            m.SetShaderParameter("hit", (dome.GlobalTransform.AffineInverse() * hitAt));
            Anim(m, "rip", 0.001f, 1f, 0.6, 0, Tween.TransitionType.Sine, Tween.EaseType.Out);
            Flash(hitAt, silver, 7f); Shake(0.07f); HitStop(0.05); Streaks(hitAt, Colors.White, 10, 2.6f);
            await Wait(0.2);
            // the wave runs back over their side
            var front = FxQuad(BeamFx, new Vector3(0, 0.08f, frontZ), new Vector2(7f, 0.5f), 0.7, silver, Flat, false, null, ("amt", 1.2f));
            front.CreateTween().TweenProperty(front, "position:z", -2.4f, 0.6).SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.Out);
            Warp(new Vector3(0, 0.05f, frontZ - 1.2f), 5f, 0.6, 1.2f);
            foreach (var f in foes.Where(f => !f.S.Def)) { double d = 0.1 + Mathf.Abs(f.Root.GlobalPosition.Z - frontZ) * 0.25; After(d, () => Burn(f, new Color(0.75f, 0.9f, 1f), 0.5, new Vector2(0.5f, 0.95f), 0, 1f, 6f)); }
            Anim(m, "amt", 1f, 0f, 0.5, 0.5);
            After(1.1, () => dome.QueueFree());
            Snd("SE_CARD_FINISH_BREAK_02", 0.2);
        }
        else
        {
            var pane = FxQuad(HexFx, new Vector3(0, 0.55f, frontZ), new Vector2(4.6f, 1.2f), 0.42, silver, new Basis(Vector3.Right, -0.75f), false, null, ("amt", 1f), ("cells", new Vector2(18f, 4f)));
            await Wait(0.32);
            Flash(hitAt, silver, 8f); Shake(0.09f); HitStop(0.06); Snd("SE_CARD_FINISH_BREAK_03");
            foreach (var f in foes.Where(f => !f.S.Def))
            {
                var to = f.Root.GlobalPosition + Vector3.Up * 0.2f;
                GlassBreak(new Vector3(f.Root.GlobalPosition.X * 0.6f, 0.55f, frontZ), 1.4f, 0.9f, silver, to, 6, 3);
                After(0.35, () => { Burn(f, new Color(0.8f, 0.92f, 1f), 0.45, new Vector2(0.5f, 0.9f), 0, 1f, 6f); Streaks(to, Colors.White, 8, 2.2f); });
            }
        }
        await Wait(0.4);
        GoHome(a);
        await Wait(1.6);
    }

    // ---------------------------------------------------------------- 74 Negate Attack

    async Task NegateAttackFx(int style)
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
        var front = mp + new Vector3(0, 0.4f, -CH * 0.8f);
        var blue = new Color(0.6f, 0.8f, 1f);
        if (style == 1)
        {
            var orb = Orb(Line(ap + Vector3.Up * 0.2f, front, 0.1f), new Color(1f, 0.6f, 0.3f), 0.55, 0.65f, null, Tween.EaseType.Out);
            var vx = FxQuad(VortexFx, front, new Vector2(1.3f, 1.3f), 1.0, blue * new Color(0.6f, 0.6f, 0.6f), Bill, false, null, ("col2", Colors.White), ("amt", 5f));
            FxQuad(CoreFx, front, new Vector2(0.35f, 0.35f), 1.0, blue, Bill, true);
            Warp(front, 1.6f, 1.0, 1.2f, true, Bill);
            await Wait(0.55);
            orb.CreateTween().TweenProperty(orb, "scale", Vector3.One * 0.05f, 0.2).SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.In);
            await Wait(0.4);
            FxQuad(CoreGlowFx, front, new Vector2(1.0f, 1.0f), 0.25, blue, Bill); Warp(front, 2f, 0.3, 1.2f, false, Bill); Snd("SE_CARD_ST_BREAK");
        }
        else if (style == 2)
        {
            ScreenFlash(new Color(0.7f, 0.8f, 1f), 0.3f, 0.4); Vignette(new Color(0.35f, 0.45f, 0.6f), 1.4);
            Warp(mp + Vector3.Up * 0.05f, 7f, 0.6, 1.0f);
            for (int i = 0; i < 3; i++) { int k = i; After(i * 0.1, () => FxQuad(ClockFx, front, new Vector2(0.9f + k * 0.5f, 0.9f + k * 0.5f), 1.2, blue, Bill, false, null, ("amt", 0f))); }
            await Wait(0.2);
            var s = CardSheet(a);   // frozen: a grey copy holds still mid-charge
            s.Set("darken", 0.35f); s.Set("glow_col", new Color(0.55f, 0.65f, 0.8f)); Anim(s.M, "glow", 0f, 0.5f, 0.2);
            Snd("SE_DUEL_TIME_COUNT_ZERO");
            await Wait(0.9);
            s.Mi.QueueFree();
            var st = Mon(a, a.Card.Uid == a.Card.Uid ? 2 : 2, 1);
            Place(a, st); a.Root.GlobalTransform = s.Mi.GlobalTransform;
            Anim(s.M, "glow", 0.5f, 0f, 0.1);
        }
        else
        {
            for (int i = 0; i < 4; i++) { int k = i; After(i * 0.12, () => { FxQuad(RingFx, front, new Vector2(1.0f + k * 0.35f, 1.0f + k * 0.35f), 0.5, k % 2 == 0 ? Colors.White : blue, Bill); Snd("SE_DUEL_TIME_COUNT", 0); }); }
            await Wait(0.15);
            var from = a.Root.Transform;
            Path(a, 0, 0.18, k => new Transform3D(from.Basis, from.Origin.Lerp(front, 0.55f * k)));
            await Wait(0.18);
            Flash(front, blue, 7f); Streaks(front, Colors.White, 14, 2.8f); Shake(0.07f); HitStop(0.05);
        }
        await Wait(0.2);
        GoHome(a, 0.5f);
        await Wait(1.6);
    }

    // ---------------------------------------------------------------- 75 Harpie's Feather Duster

    async Task FeatherDusterFx(int style)
    {
        var board = RivalBoard(); var rows = board.Where(v => v.S.Loc == Loc.Spell).ToList();
        foreach (var (name, zone) in new[] { ("Mirror Force", 0), ("Trap Hole", 4) }) { var v = Of(1, name); Place(v, SpellZ(zone, 1, false)); rows.Add(v); }
        var card = await PlayCard("Harpie's Feather Duster", FxGreen, sound: "SE_EV_HARPIESFEATHER_DUSTER");
        var gold = new Color(1f, 0.92f, 0.7f); float z = ZonePos(Loc.Spell, 2, 1).Z;
        void ToFeathers(View v, Vector3 drift)
        {
            var s = CardSheet(v);
            BurnAway(s, Colors.White, 0.4, new Vector2(0.5f, 0.5f), 0, 0.3f, 4f);
            var p = s.Mi.GlobalPosition;
            for (int i = 0; i < 6; i++) Feather(p + new Vector3(R(-0.25, 0.25), 0.1f, R(-0.3, 0.3)), p + drift + new Vector3(R(-0.6, 0.6), R(0.6, 1.4), R(-0.5, 0.3)), i % 2 == 0 ? gold : Colors.White, R(0.9, 1.4), 0.4f);
        }
        if (style == 1)
        {
            var f = FxQuad(LightFeatherFx, new Vector3(-3.6f, 0.25f, z), new Vector2(1.0f, 3.0f), 1.3, gold, new Basis(Vector3.Up, -Mathf.Pi / 2) * Flat, false, null, ("amt", 1.3f));
            f.CreateTween().TweenProperty(f, "position:x", 3.6f, 1.1).SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.InOut);
            Snd("SE_ATTACK_A_WIND_DEFAULT_01", 0.1);
            foreach (var v in rows.OrderBy(v => v.Root.GlobalPosition.X)) { float x = v.Root.GlobalPosition.X; double d = 0.55 * (1 - Mathf.Cos((x + 3.6f) / 7.2f * Mathf.Pi)) * 1.0; After(d, () => ToFeathers(v, new Vector3(1.2f, 0, 0))); }
            await Wait(1.4);
        }
        else if (style == 2)
        {
            foreach (var v in rows)
            {
                var p = v.Root.GlobalPosition; double d = R(0, 0.3);
                for (int i = 0; i < 3; i++) Feather(p + new Vector3(R(-0.4, 0.4), 2.2f + i * 0.4f, R(-0.2, 0.2)), p + Vector3.Up * 0.1f, i == 0 ? gold : Colors.White, 0.8 + i * 0.1 + d, 0.55f);
                After(0.85 + d, () => { FxQuad(GroundGlowFx, p with { Y = 0.02f }, new Vector2(1f, 1f), 0.5, gold); ToFeathers(v, Vector3.Zero); });
            }
            await Wait(1.5);
        }
        else
        {
            var root = card.Root.GlobalPosition + new Vector3(0, 0.5f, -0.4f);
            LightWing(root, -1, 1.2f, gold, 0.35, 0.5);
            LightWing(root, 1, 1.2f, gold, 0.35, 0.5);
            await Wait(0.55);
            Snd("SE_ATTACK_A_WIND_SPECIAL_01");
            for (int g = 0; g < 6; g++) { int gg = g; After(g * 0.05, () => FxQuad(GustFx, new Vector3(R(-2, 2), 0.2f + gg * 0.1f, 0), new Vector2(0.8f, 5.5f), 0.7, Colors.White, new Basis(Vector3.Up, 0) * Flat, false, null, ("amt", 0.7f))); }
            foreach (var v in rows)
            {
                double d = 0.3 + R(0, 0.12);
                After(d, () =>
                {
                    var s = CardSheet(v); var from = s.Mi.Transform; Anim(s.M, "flutter", 0f, 1.3f, 0.15);
                    s.Mi.CreateTween().TweenMethod(Callable.From<float>(k => s.Mi.Transform = new Transform3D(new Basis(Vector3.Right, -k * 2.5f) * from.Basis, from.Origin + new Vector3(0, 0.8f * k, -1.4f * k))), 0f, 1f, 0.45);
                    After(0.25, () => { Flakes(s, 28, uv => new Vector3(R(-0.4, 0.4), R(0.6, 1.2), R(-2.4, -1.4)), 0.7, 0.15, gold); BurnAway(s, Colors.White, 0.2, null, 0, 0.1f, 2f); });
                });
            }
            await Wait(1.3);
        }
        await Wait(1.2);
    }

    // ---------------------------------------------------------------- 76 Banished

    const string SlitFx = @"void fragment() {
  vec2 p = UV * 2.0 - 1.0; float w = amt * 0.16 * (1.0 - p.y * p.y);
  float n = texture(noise, vec2(p.y * 2.0 + seed, TIME * 0.6)).r;
  float d = abs(p.x) - w * (0.8 + 0.4 * n);
  float inside = smoothstep(0.01, -0.01, d);
  float rim = exp(-pow(d / 0.02, 2.0)) * step(0.001, amt);
  float halo = exp(-max(d, 0.0) * 14.0) * 0.4 * step(0.001, amt);
  ALBEDO = mix(vec3(0.0), col.rgb, clamp(rim * 2.0 + halo, 0.0, 1.0)); ALPHA = clamp(inside + rim + halo, 0.0, 1.0) * smoothstep(1.0, 0.85, abs(p.y));
}";

    async Task BanishFx(int style)
    {
        var foes = TheirMonsters(); var v = foes[1];
        await PlayCard("Mystical Space Typhoon", FxGreen, sound: "SE_EV_CROSSOUT_DESIGNATOR");
        var p = v.Root.GlobalPosition; var cold = new Color(0.7f, 0.8f, 1f);
        var s = CardSheet(v); var from = s.Mi.Transform;
        if (style == 1)
        {
            var at = p + new Vector3(0, 0.6f, -0.2f);
            var slit = FxQuad(SlitFx, at, new Vector2(1.2f, 1.6f), 1.5, new Color(0.75f, 0.6f, 1f), Bill, true, null, ("amt", 0f));
            var sm = (ShaderMaterial)slit.MaterialOverride;
            Anim(sm, "amt", 0f, 1f, 0.3, 0, Tween.TransitionType.Back, Tween.EaseType.Out);
            await Wait(0.3);
            s.Mi.CreateTween().TweenMethod(Callable.From<float>(k =>
            {
                float e = k * k;
                s.Mi.GlobalTransform = new Transform3D(Bill * new Basis(Vector3.Right, -Mathf.Pi / 2 * (1 - e)), from.Origin.Lerp(at, e)).ScaledLocal(new Vector3(Mathf.Max(0.02f, 1 - e), 1, 1 - 0.3f * e));
            }), 0f, 1f, 0.55);
            await Wait(0.6);
            s.Mi.QueueFree();
            Anim(sm, "amt", 1f, 0f, 0.18, 0.05, Tween.TransitionType.Cubic, Tween.EaseType.In);
            After(0.22, () => { FxQuad(CoreGlowFx, at, new Vector2(0.8f, 0.8f), 0.3, Colors.White, Bill); Snd("SE_CARD_ST_BREAK"); });
        }
        else if (style == 2)
        {
            var tw = s.Mi.CreateTween();
            tw.TweenMethod(Callable.From<float>(k => s.Mi.Transform = from * new Transform3D(new Basis(Vector3.Right, k * 0.4f), new Vector3(0, 0.15f * k, 0)).ScaledLocal(new Vector3(1, 1, 1 - 0.5f * k))), 0f, 1f, 0.22).SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.In);
            tw.TweenMethod(Callable.From<float>(k => s.Mi.Transform = from * new Transform3D(new Basis(Vector3.Forward, k * 0.4f) * new Basis(Vector3.Right, 0.4f), new Vector3(0, 0.15f, 0)).ScaledLocal(new Vector3(1 - 0.5f * k, 1, 0.5f))), 0f, 1f, 0.22).SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.In);
            tw.TweenMethod(Callable.From<float>(k => s.Mi.Transform = from * new Transform3D(new Basis(Vector3.Up, k * 3f), new Vector3(0, 0.15f + 0.2f * k, 0)).ScaledLocal(new Vector3(0.5f, 1, 0.5f) * (1 - k))), 0f, 1f, 0.3).SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.In);
            s.Set("glow_col", new Color(0.6f, 0.5f, 1f)); Anim(s.M, "glow", 0f, 1.2f, 0.7);
            Snd("SE_CARD_FLIP_02", 0.2);
            await Wait(0.76);
            s.Mi.QueueFree();
            var at = p + Vector3.Up * 0.35f;
            FxQuad(CoreGlowFx, at, new Vector2(0.7f, 0.7f), 0.3, Colors.White, Bill);
            FxQuad(RingFx, p with { Y = 0.03f }, new Vector2(1.6f, 1.6f), 0.45, new Color(0.5f, 0.35f, 0.9f));
            Warp(at, 1.4f, 0.35, 1.2f, false, Bill);
        }
        else
        {
            s.Set("glow_col", cold); Anim(s.M, "glow", 0f, 1.1f, 0.35); Anim(s.M, "darken", 0f, 0.5f, 0.35);
            BurnAway(s, new Color(0.75f, 0.85f, 1f), 0.65, new Vector2(0.5f, 0f), 0.35, 0.8f, 4f, 0.2f);
            await Wait(0.9);
            var pile = Grid3(new St(Loc.Banished, 0, 0, true, false, 1, 0, 0)) + Vector3.Up * 0.2f;
            Orb(Line(p + Vector3.Up * 0.2f, pile, 0.5f), cold, 0.35, 0.4f, () => FxQuad(RingFx, pile with { Y = 0.03f }, new Vector2(1.2f, 1.2f), 0.4, cold));
        }
        await Wait(1.6);
    }

    // ---------------------------------------------------------------- 91 Monster Reborn

    async Task MonsterRebornFx(int style)
    {
        var dead = Of(0, "Dark Magician"); Place(dead, GyAt(0));
        await PlayCard("Monster Reborn", FxGreen, sound: "SE_EV_MONSTER_REBORN");
        var gy = GyPos(0); var at = Mon(dead, 2, 0); var zone = Grid3(at);
        var gold = new Color(1f, 0.88f, 0.6f);
        if (style == 1)
        {
            var ank = FxQuad(AnkhFx, gy + new Vector3(0, 0.6f, -0.3f), new Vector2(0.9f, 1.25f), 2.0, gold, Bill);
            ank.CreateTween().TweenProperty(ank, "position:y", 1.0f, 2.0).SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.Out);
            await Wait(0.5);
            SoulStream(gy + Vector3.Up * 0.7f, zone, 18, gold, 0.7, 0.7f, 0.025);
            await Wait(0.6);
            await Materialise(dead, at, gold, 0.6, new Vector2(0.5f, 0.5f));
        }
        else if (style == 2)
        {
            FxStand(PillarFx, zone, new Vector2(1.2f, 6f), 1.6, gold, false, null, ("amt", 1.1f));
            FxQuad(GroundGlowFx, zone with { Y = 0.02f }, new Vector2(1.8f, 1.8f), 1.6, gold);
            ScreenDim(0.25f, 0.2, 0.9, 0.4);
            await Wait(0.35);
            await Materialise(dead, at, Colors.White, 0.85, new Vector2(0.5f, 0f), 6f);
            FxQuad(RingFx, zone with { Y = 0.03f }, new Vector2(2f, 2f), 0.5, gold);
        }
        else
        {
            var flame = FxQuad(FireFx, gy + Vector3.Up * 0.3f, new Vector2(0.6f, 0.9f), 1.0, gold, Bill, false, null, ("col2", Colors.White), ("amt", 1.2f));
            await Wait(0.3);
            Orb(Line(gy + Vector3.Up * 0.4f, zone + Vector3.Up * 0.2f, 1.2f), gold, 0.6, 0.8f, null, Tween.EaseType.InOut);
            await Wait(0.6);
            FxQuad(CoreGlowFx, zone + Vector3.Up * 0.2f, new Vector2(1.6f, 1.6f), 0.4, gold, Bill);
            await Materialise(dead, at, gold, 0.5, new Vector2(0.5f, 0.5f));
        }
        Cue("land");
        await Wait(1.5);
    }

    // ---------------------------------------------------------------- 92 Pot of Greed

    /// A pot turned on a lathe: glazed green body, gold rim.
    Node3D Pot(Vector3 at, float size)
    {
        var root = new Node3D { Position = at, Scale = Vector3.One * size };
        world.AddChild(root);
        (float R, float Y)[] prof = { (0f, 0f), (0.15f, 0f), (0.21f, 0.05f), (0.27f, 0.18f), (0.28f, 0.3f), (0.23f, 0.43f), (0.13f, 0.52f), (0.11f, 0.57f), (0.15f, 0.62f), (0.14f, 0.65f), (0.1f, 0.63f), (0.08f, 0.5f) };
        var st = new SurfaceTool(); st.Begin(Mesh.PrimitiveType.Triangles);
        const int seg = 40;
        for (int i = 0; i + 1 < prof.Length; i++)
            for (int j = 0; j < seg; j++)
            {
                float a0 = j * Mathf.Tau / seg, a1 = (j + 1) * Mathf.Tau / seg;
                Vector3 P(int k, float a) => new(prof[k].R * Mathf.Cos(a), prof[k].Y, prof[k].R * Mathf.Sin(a));
                var q = new[] { P(i, a0), P(i, a1), P(i + 1, a1), P(i + 1, a0) };
                foreach (int k in new[] { 0, 2, 1, 0, 3, 2 }) st.AddVertex(q[k]);
            }
        st.GenerateNormals();
        var glaze = new StandardMaterial3D { AlbedoColor = new Color(0.12f, 0.5f, 0.22f), Metallic = 0.15f, Roughness = 0.18f, ClearcoatEnabled = true, Clearcoat = 1f, EmissionEnabled = true, Emission = new Color(0.02f, 0.12f, 0.04f), CullMode = BaseMaterial3D.CullModeEnum.Disabled };
        root.AddChild(new MeshInstance3D { Mesh = st.Commit(), MaterialOverride = glaze });
        root.AddChild(new MeshInstance3D { Mesh = new TorusMesh { InnerRadius = 0.13f, OuterRadius = 0.16f, Rings = 40, RingSegments = 10 }, Position = new Vector3(0, 0.635f, 0), MaterialOverride = new StandardMaterial3D { AlbedoColor = new Color(1f, 0.78f, 0.3f), Metallic = 0.8f, Roughness = 0.25f } });
        root.AddChild(new MeshInstance3D { Mesh = new TorusMesh { InnerRadius = 0.265f, OuterRadius = 0.29f, Rings = 48, RingSegments = 8 }, Position = new Vector3(0, 0.3f, 0), MaterialOverride = new StandardMaterial3D { AlbedoColor = new Color(1f, 0.78f, 0.3f), Metallic = 0.8f, Roughness = 0.25f } });
        return root;
    }

    async Task PotOfGreedFx(int style)
    {
        var hand = DealHand(0, 3);
        var card = await PlayCard("Pot of Greed", FxGreen, sound: "SE_EV_GOLD_SARCOPHAGUS");
        var p = card.Root.GlobalPosition; var green = new Color(0.45f, 1f, 0.55f);
        if (style == 1)
        {
            var pot = Pot(p + new Vector3(0, 0.3f, -0.15f), 0.01f);
            FxQuad(GroundGlowFx, p with { Y = 0.02f }, new Vector2(1.4f, 1.4f), 1.6, green);
            var tw = pot.CreateTween();
            tw.TweenProperty(pot, "scale", Vector3.One * 1.5f, 0.35).SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
            tw.Parallel().TweenProperty(pot, "rotation:y", 3.0f, 0.6);
            tw.TweenProperty(pot, "rotation:x", 1.1f, 0.25).SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);   // it tips toward you
            tw.TweenInterval(0.6);
            tw.TweenProperty(pot, "scale", Vector3.One * 0.01f, 0.25).SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.In);
            tw.TweenCallback(Callable.From(pot.QueueFree));
            await Wait(0.65);
            Streaks(pot.GlobalPosition + new Vector3(0, 0.2f, 0.4f), green, 10, 2f, 1.5f, Vector3.Back);
            await Draws(0, hand, 2, 0.12);
        }
        else if (style == 2)
        {
            var deck = Grid3(DeckOf(0)) + Vector3.Up * 0.2f;
            for (int i = 0; i < 2; i++) Orb(Line(p + Vector3.Up * 0.2f, deck + Vector3.Up * 0.05f * i, 0.6f + i * 0.3f), green, 0.4 + i * 0.08, 0.45f, () => FxQuad(RingFx, deck with { Y = 0.05f }, new Vector2(1f, 1f), 0.35, green));
            await Wait(0.5);
            await Draws(0, hand, 2, 0.15);
            foreach (var v in hand.TakeLast(2)) FrameBurst(v, green, 0.9f, 1.15f, 0.4);
        }
        else
        {
            for (int i = 0; i < 3; i++) { int k = i; After(i * 0.15, () => FxQuad(RingFx, p with { Y = 0.03f }, new Vector2(1.0f + k * 0.35f, 1.0f + k * 0.35f), 0.5, green)); }
            FxStand(FireFx, p, new Vector2(0.9f, 0.9f), 0.9, green, false, null, ("col2", Colors.White), ("amt", 0.8f));
            var deck = Grid3(DeckOf(0));
            Beam(p + Vector3.Up * 0.05f, deck + Vector3.Up * 0.05f, 0.4f, 0.7, green);
            await Wait(0.45);
            FxQuad(GroundGlowFx, deck with { Y = 0.05f }, new Vector2(1.3f, 1.3f), 0.6, green);
            await Draws(0, hand, 2, 0.15);
        }
        await Wait(1.2);
    }

    // ---------------------------------------------------------------- 93 Graceful Charity

    async Task GracefulCharityFx(int style)
    {
        var hand = DealHand(0, 3);
        var card = await PlayCard("Graceful Charity", FxGreen, sound: "SE_EV_GHOSTRICK_ANGEL");
        var deck = Grid3(DeckOf(0)); var gy = GyPos(0); var soft = new Color(1f, 0.96f, 0.85f);
        if (style == 1)
        {
            FxStand(PillarFx, deck, new Vector2(1.0f, 5.5f), 1.6, soft, false, null, ("amt", 0.6f));
            for (int i = 0; i < 8; i++) Ember(deck + new Vector3(R(-0.3, 0.3), R(1.5, 3.0), R(-0.2, 0.2)), soft, new Vector3(0, -R(0.4, 0.8), 0), 1.2, 0.04f, 0);
            await Wait(0.4);
        }
        else if (style == 2)
        {
            var handC = hand[1].Root.GlobalPosition + new Vector3(0, 0.6f, -0.3f);
            FxQuad(RingFx, handC, new Vector2(1.2f, 0.5f), 1.4, new Color(1f, 0.85f, 0.45f), Bill);
            for (int i = 0; i < 6; i++) Feather(handC + new Vector3(R(-0.8, 0.8), R(0.6, 1.0), 0), handC + new Vector3(R(-1.2, 1.2), -0.6f, 0), new Color(1f, 0.85f, 0.5f), R(1.2, 1.6), 0.4f);
            await Wait(0.3);
        }
        else
        {
            FxQuad(GroundGlowFx, deck with { Y = 0.05f }, new Vector2(1.5f, 1.5f), 1.2, soft);
            await Wait(0.2);
        }
        await Draws(0, hand, 3, 0.16);
        if (style == 3) foreach (var v in hand.TakeLast(3)) { var ll = LightLine(deck + Vector3.Up * 0.1f, v.Root.GlobalPosition, soft, 0.2); After(0.6, () => { if (IsInstanceValid(ll)) ll.QueueFree(); }); }
        await Wait(0.4);
        // two cards leave the hand for the Graveyard
        foreach (var v in hand.Take(2).ToList())
        {
            var from = v.Root.GlobalPosition;
            if (style == 1) { for (int i = 0; i < 4; i++) Feather(from, gy + Vector3.Up * 0.2f + new Vector3(R(-0.3, 0.3), 0, R(-0.3, 0.3)), soft, R(0.8, 1.1), 0.35f); HideFor(v, 1.0); Place(v, GyAt(0)); }
            else if (style == 2) { var s = CardSheet(v); BurnAway(s, Colors.White, 0.5, null, 0, 1f, 5f); }
            else { SoulStream(from, gy, 10, soft, 0.6, 0.5f, 0.02); HideFor(v, 0.8); Place(v, GyAt(0)); }
            await Wait(0.12);
        }
        await Wait(1.4);
    }

    // ---------------------------------------------------------------- 94 Brain Control

    const string EyeFx = @"void fragment() {
  vec2 p = UV * 2.0 - 1.0;
  float lid = abs(p.y) - (1.0 - p.x * p.x) * 0.48 * amt;
  float eye = smoothstep(0.02, -0.02, lid) * step(abs(p.x), 1.0);
  float outline = exp(-pow(lid / 0.035, 2.0)) * step(abs(p.x), 1.0);
  float r = length(p * vec2(1.0, 1.05));
  float iris = smoothstep(0.34, 0.3, r) * eye; float ring = exp(-pow((r - 0.3) / 0.03, 2.0)) * eye;
  float pupil = smoothstep(0.13, 0.1, length(p * vec2(1.8, 1.0))) * eye;
  float rays = pow(texture(noise, vec2(atan(p.y, p.x) * 1.5 + seed, r * 2.0 - TIME)).r, 2.0) * iris;
  ALBEDO = mix(col.rgb, vec3(1.0), clamp(outline * 0.5 + ring, 0.0, 1.0));
  ALPHA = clamp(outline + ring + iris * 0.35 + rays * 0.6 + eye * 0.06 - pupil * 0.5, 0.0, 1.0) * env(0.12, 0.25);
}";

    async Task BrainControlFx(int style)
    {
        var foes = TheirMonsters(); var t = foes[1];
        var card = await PlayCard("Brain Control", FxGreen, sound: "SE_EV_DARKRULER_NOMORE");
        var p = t.Root.GlobalPosition; var violet = new Color(0.75f, 0.5f, 1f); var goal = Mon(t, 3, 0);
        if (style == 1)
        {
            var from = card.Root.GlobalPosition;
            for (int i = 0; i < 6; i++)
            {
                double d = i * 0.12;
                After(d, () => Orb(Line(from + Vector3.Up * 0.2f, p + Vector3.Up * 0.1f, 0.6f), violet, 0.45, 0.35f, () => FxQuad(RingFx, p with { Y = 0.03f }, new Vector2(1.3f, 1.3f), 0.4, violet)));
            }
            await Wait(0.9);
            t.Face.EmissionEnabled = true; t.Face.Emission = violet * 0.35f;
        }
        else if (style == 2)
        {
            var corners = new[] { new Vector3(-CW * 0.45f, 0, -CH * 0.45f), new Vector3(CW * 0.45f, 0, -CH * 0.45f), new Vector3(-CW * 0.45f, 0, CH * 0.45f), new Vector3(CW * 0.45f, 0, CH * 0.45f) };
            var lines = new List<MeshInstance3D>();
            foreach (var c in corners) lines.Add(LightLine(p + c + new Vector3(0, 3.5f, -0.6f), p + c + Vector3.Up * 0.02f, violet, 0.3));
            await Wait(0.45);
            Snd("SE_TDY_CHAINGET_CHAIN_01");
            var from = t.Root.Transform; var to = Target(t, goal);
            Path(t, 0, 0.9, k => new Transform3D(from.Basis.Slerp(to.Basis, k), from.Origin.Lerp(to.Origin, Ease(k)) + Vector3.Up * Mathf.Sin(k * Mathf.Pi) * 0.9f + Vector3.Up * Mathf.Sin(k * 18f) * 0.03f));
            for (int i = 0; i < 18; i++) { double d = i * 0.05; After(d, () => { int j = 0; foreach (var l in lines) { if (IsInstanceValid(l)) l.QueueFree(); j++; } lines.Clear(); var cp = t.Root.GlobalTransform; foreach (var c in corners) { var a = cp * c; var l2 = LightLine(a + new Vector3(0, 3.5f, -0.6f), a, violet, 0.01); lines.Add(l2); } }); }
            await Wait(0.95);
            foreach (var l in lines) if (IsInstanceValid(l)) l.QueueFree();
            Place(t, goal);
            await Wait(0.4);
            goto done;
        }
        else
        {
            var eye = FxQuad(EyeFx, p + new Vector3(0, 0.7f, -0.5f), new Vector2(1.5f, 0.9f), 1.6, violet, Bill, false, null, ("amt", 0f));
            Anim((ShaderMaterial)eye.MaterialOverride, "amt", 0f, 1f, 0.4, 0.1, Tween.TransitionType.Back, Tween.EaseType.Out);
            await Wait(0.9);
            FxQuad(CoreGlowFx, p + new Vector3(0, 0.7f, -0.5f), new Vector2(1.4f, 1.4f), 0.3, violet, Bill); ScreenFlash(violet, 0.2f, 0.25);
            FrameBurst(t, violet, 0.9f, 1.3f);
            await Wait(0.4);
        }
        Move(t, goal, 0, 0.6f, 0.9f); await Wait(0.62);
        t.Face.Emission = Colors.Black;
    done:
        FxQuad(GroundGlowFx, t.Root.GlobalPosition with { Y = 0.02f }, new Vector2(1.3f, 1.3f), 0.6, violet); Cue("land");
        await Wait(1.5);
    }

    // ---------------------------------------------------------------- 95 Magic Cylinder

    /// A brass-rimmed cylinder, lying with its mouth toward `face`.
    Node3D Cylinder(Vector3 at, Vector3 face, float size)
    {
        var root = new Node3D { Position = at };
        world.AddChild(root);
        root.LookAt(at + face, Vector3.Up);
        var body = new Node3D { RotationDegrees = new Vector3(90, 0, 0), Scale = Vector3.One * size };
        root.AddChild(body);
        var dark = new StandardMaterial3D { AlbedoColor = new Color(0.08f, 0.05f, 0.12f), Metallic = 0.4f, Roughness = 0.4f, CullMode = BaseMaterial3D.CullModeEnum.Disabled };
        var brass = new StandardMaterial3D { AlbedoColor = new Color(1f, 0.75f, 0.3f), Metallic = 0.85f, Roughness = 0.22f };
        var violet = new StandardMaterial3D { AlbedoColor = new Color(0.35f, 0.15f, 0.55f), Metallic = 0.3f, Roughness = 0.3f };
        body.AddChild(new MeshInstance3D { Mesh = new CylinderMesh { TopRadius = 0.3f, BottomRadius = 0.3f, Height = 0.8f, RadialSegments = 40, CapTop = false, CapBottom = true }, MaterialOverride = violet });
        body.AddChild(new MeshInstance3D { Mesh = new CylinderMesh { TopRadius = 0.28f, BottomRadius = 0.28f, Height = 0.79f, RadialSegments = 40, CapTop = false, CapBottom = false, FlipFaces = true }, MaterialOverride = dark });
        foreach (float y in new[] { 0.4f, -0.4f, 0.12f }) body.AddChild(new MeshInstance3D { Mesh = new TorusMesh { InnerRadius = 0.28f, OuterRadius = 0.34f, Rings = 40, RingSegments = 10 }, Position = new Vector3(0, y, 0), MaterialOverride = brass });
        root.Scale = Vector3.One * 0.01f;
        root.CreateTween().TweenProperty(root, "scale", Vector3.One, 0.3).SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
        return root;
    }

    async Task MagicCylinderFx(int style)
    {
        var foes = TheirMonsters(); var a = foes[0]; int atk = a.Card.Data.Atk;
        var mine = MineAt("Celtic Guardian", 2);
        var trap = Of(0, "Magic Cylinder"); Place(trap, SpellZ(2, 0, false));
        await Wait(0.4);
        Charge(a, mine.Root.GlobalPosition, 0.35, 0.3f);
        await Wait(0.3);
        await Flip(trap, FxPink, "SE_EV_MAGIC_CYLINDER");
        var mp = mine.Root.GlobalPosition; var ap = a.Root.GlobalPosition; var fire = new Color(1f, 0.55f, 0.25f); var violet = new Color(0.7f, 0.45f, 1f);
        var mouth = mp + new Vector3(0, 0.45f, -CH * 0.75f);
        var them = new Vector3(0, 0.6f, -2.6f);
        if (style == 1)
        {
            var c1 = Cylinder(mouth, (ap - mouth) with { Y = 0 }, 0.9f);
            var c2At = new Vector3(2.6f, 0.5f, -0.4f); var c2 = Cylinder(c2At, them - c2At, 0.9f);
            await Wait(0.35);
            Orb(Line(ap + Vector3.Up * 0.2f, mouth, 0.1f), fire, 0.35, 0.7f, () => { FxQuad(CoreGlowFx, mouth, new Vector2(0.8f, 0.8f), 0.2, violet, Bill); Snd("SE_CARD_ST_BREAK"); });
            await Wait(0.65);
            Orb(Line(c2At, them, 0.2f), fire, 0.3, 0.9f, () => { Explosion(them with { Y = 0.2f }, 0.5f); LpHit(1, 8000, 8000 - atk); });
            Beam(c2At, them, 0.5f, 0.35, fire);
            await Wait(0.6);
            foreach (var c in new[] { c1, c2 }) { var tw = c.CreateTween(); tw.TweenProperty(c, "scale", Vector3.One * 0.01f, 0.25).SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.In); tw.TweenCallback(Callable.From(c.QueueFree)); }
        }
        else if (style == 2)
        {
            var r1 = FxQuad(RingFx, mouth, new Vector2(1.2f, 1.2f), 1.2, violet, Bill);
            var exitAt = them + new Vector3(0, 0.6f, 0.9f);
            await Wait(0.25);
            FxQuad(VortexFx, mouth, new Vector2(1.1f, 1.1f), 0.9, violet, Bill, false, null, ("col2", Colors.White), ("amt", 5f));
            Orb(Line(ap + Vector3.Up * 0.2f, mouth, 0.1f), fire, 0.35, 0.7f);
            await Wait(0.45);
            FxQuad(VortexFx, exitAt, new Vector2(1.1f, 1.1f), 0.8, violet, Bill, false, null, ("col2", Colors.White), ("amt", -5f));
            await Wait(0.2);
            Orb(Line(exitAt, them with { Y = 0.2f }, 0f), fire, 0.2, 0.9f, () => { Explosion(them with { Y = 0.2f }, 0.5f); LpHit(1, 8000, 8000 - atk); });
            await Wait(0.5);
        }
        else
        {
            var mpc = mp + Vector3.Up * 0.5f;
            Orb(Line(ap + Vector3.Up * 0.2f, mouth, 0.1f), fire, 0.3, 0.7f);
            await Wait(0.3);
            FxQuad(RingFx, mouth, new Vector2(1f, 1f), 0.3, violet, Bill);
            // caught, swung once round your monster, thrown back
            Orb(k => mpc + new Vector3(Mathf.Sin(k * Mathf.Tau) * 0.9f, 0.1f * Mathf.Sin(k * Mathf.Pi), -Mathf.Cos(k * Mathf.Tau) * 0.7f), fire, 0.5, 0.75f, null, Tween.EaseType.InOut);
            await Wait(0.5);
            Orb(Line(mouth, them with { Y = 0.2f }, 0.3f), new Color(1f, 0.75f, 0.4f), 0.25, 1.0f, () => { Explosion(them with { Y = 0.2f }, 0.55f); LpHit(1, 8000, 8000 - atk); });
            for (int i = 0; i < 6; i++) { double d = i * 0.04; After(d, () => Streaks(mouth.Lerp(them, (float)(d / 0.25)), fire, 4, 1.4f, 0f)); }
            await Wait(0.5);
        }
        GoHome(a);
        await Wait(1.5);
    }

    // ---------------------------------------------------------------- 96 Torrential Tribute

    const string FloodFx = @"void fragment() {
  vec2 q = UV * vec2(1.5, 1.0);
  float n = texture(noise, q * 0.8 + vec2(TIME * 0.04, TIME * 0.025)).r; float n2 = texture(noise, q * 1.3 - vec2(TIME * 0.03, -TIME * 0.05) + 0.4).r;
  float caust = pow(1.0 - abs(n - n2), 30.0) * 0.6;
  vec2 e = abs(UV - 0.5) * 2.0; float edge = 1.0 - smoothstep(0.7, 1.0, max(e.x, e.y * 1.05) + (n - 0.5) * 0.25);
  float front = smoothstep(0.0, 0.25, amt * 1.25 - (1.0 - UV.y) * 0.25 - (n - 0.5) * 0.2);
  ALBEDO = mix(col.rgb * (0.55 + 0.35 * n2), vec3(0.8, 0.95, 1.0), caust * 0.8);
  ALPHA = clamp((0.32 + caust * 0.5) * edge * front * amt, 0.0, 1.0);
}";

    /// The front of a tide crossing the table (toward -z as t runs): a foam line, clear water behind it thinning out.
    const string TideFx = @"void fragment() {
  float y = 1.0 - UV.y;
  float n = texture(noise, vec2(UV.x * 1.2 + seed, TIME * 0.3)).r; float n2 = texture(noise, vec2(UV.x * 3.0, y * 2.0 - TIME * 1.5)).r;
  float front = t * 1.3 - 0.1 + (n - 0.5) * 0.08;
  float d = front - y;
  float foam = exp(-pow(d / 0.025, 2.0)) * (0.7 + 0.6 * n2);
  float body = smoothstep(0.0, 0.02, d) * (1.0 - smoothstep(0.0, 0.45, d)) * (0.35 + 0.4 * pow(n2, 2.0));
  float sides = smoothstep(0.0, 0.08, UV.x) * smoothstep(1.0, 0.92, UV.x);
  ALBEDO = mix(col.rgb, vec3(0.92, 0.98, 1.0), clamp(foam, 0.0, 1.0)); ALPHA = clamp(foam + body, 0.0, 1.0) * sides * (1.0 - smoothstep(0.85, 1.0, t));
}";

    async Task TorrentialTributeFx(int style)
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
        var water = new Color(0.25f, 0.55f, 0.85f); var foam = new Color(0.85f, 0.95f, 1f);
        if (style == 1)
        {
            FxQuad(TideFx, new Vector3(0, 0.08f, 0), new Vector2(7.4f, 5.0f), 1.6, water, Flat, true);
            Snd("SE_SOLO_WIND_01", 0.1);
            foreach (var v in all)
            {
                float z = v.Root.GlobalPosition.Z; double d = (2.8f - z) / 5.6f * 1.2 + 0.2;   // the wave runs from your side to theirs
                After(d, () =>
                {
                    var s = CardSheet(v); var from = s.Mi.Transform;
                    s.Set("darken", 0.3f); Anim(s.M, "flutter", 0f, 0.8f, 0.2);
                    s.Mi.CreateTween().TweenMethod(Callable.From<float>(k => s.Mi.Transform = new Transform3D(new Basis(Vector3.Right, -k * 1.5f) * from.Basis, from.Origin + new Vector3(0, 0.3f * Mathf.Sin(k * Mathf.Pi), -1.6f * k))), 0f, 1f, 0.5);
                    BurnAway(s, foam, 0.4, null, 0.15, 0.3f, 2f);
                    Streaks(from.Origin + Vector3.Up * 0.1f, foam, 10, 2.4f, 5f, new Vector3(0, 1, -0.6f), 0.5f);
                });
            }
            await Wait(1.8);
        }
        else if (style == 2)
        {
            foreach (var v in all)
            {
                var p = v.Root.GlobalPosition with { Y = 0 }; double d = R(0, 0.35);
                After(d, () =>
                {
                    FxStand(PillarFx, p, new Vector2(0.8f, 2.6f), 0.7, foam, false, null, ("amt", 1f));
                    FxStand(FireFx, p, new Vector2(0.9f, 1.6f), 0.7, water, false, null, ("col2", foam), ("amt", 1f));
                    Streaks(p + Vector3.Up * 0.3f, foam, 18, 3.4f, 6f, Vector3.Up, 0.4f);
                    After(0.3, () => Puffs(p + Vector3.Up * 0.4f, 2, new Color(0.85f, 0.92f, 1f, 0.45f), 0.8f, 0.6f, 0.3f));
                    var s = CardSheet(v); var from = s.Mi.Transform;
                    s.Mi.CreateTween().TweenMethod(Callable.From<float>(k => s.Mi.Transform = new Transform3D(new Basis(new Vector3(1, 0.3f, 0.2f).Normalized(), k * 7f) * from.Basis, from.Origin + Vector3.Up * (2.4f * k - 1.6f * k * k))), 0f, 1f, 0.75);
                    BurnAway(s, foam, 0.35, null, 0.4, 0.3f, 2f);
                });
            }
            Snd("SE_EV_CONVULSION_OF_NATURE_b", 0.1);
            await Wait(1.4);
        }
        else
        {
            var q = FxQuad(FloodFx, new Vector3(0, 0.05f, 0), new Vector2(7.6f, 5.2f), 2.4, water, Flat, true, null, ("amt", 0f));
            var qm = (ShaderMaterial)q.MaterialOverride;
            Anim(qm, "amt", 0f, 1f, 0.8);
            Anim(qm, "amt", 1f, 0f, 0.6, 1.7);
            foreach (var v in all)
            {
                double d = 0.3 + R(0, 0.3);
                After(d, () =>
                {
                    var s = CardSheet(v);
                    s.Set("glow_col", water); Anim(s.M, "glow", 0f, 0.4f, 0.8); Anim(s.M, "darken", 0f, 0.8f, 1.0); Anim(s.M, "sink", 0f, 0.4f, 1.0);
                    var from = s.Mi.Transform;
                    s.Mi.CreateTween().TweenMethod(Callable.From<float>(k => s.Mi.Transform = from.ScaledLocal(Vector3.One * (1 - 0.3f * k))), 0f, 1f, 1.0);
                    for (int i = 0; i < 5; i++) Ember(from.Origin + new Vector3(R(-0.25, 0.25), 0.05f, R(-0.3, 0.3)), foam, Vector3.Up * R(0.2, 0.5), 0.8, 0.04f, 0.3f);
                    BurnAway(s, water, 0.4, null, 1.0, 0.2f, 1.5f);
                });
            }
            await Wait(2.4);
        }
        await Wait(1.0);
    }

    // ---------------------------------------------------------------- 97 Solemn Judgment

    async Task SolemnJudgmentFx(int style)
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
            var lp = new Vector3(-3.0f, 0.3f, 2.4f);
            SoulStream(lp, p + new Vector3(0, 3.5f, -0.5f), 20, gold, 0.6, 1.4f, 0.02);
            ScreenDim(0.3f, 0.25, 0.7, 0.4);
            await Wait(0.75);
            FxStand(PillarFx, p, new Vector2(0.9f, 6f), 0.7, gold, false, null, ("amt", 1.3f));
            FxStand(PillarFx, p, new Vector2(0.3f, 6f), 0.5, Colors.White, false, null, ("amt", 1f));
            ScreenFlash(gold, 0.35f, 0.25); Shake(0.12f); HitStop(0.05);
            FxQuad(RingFx, p with { Y = 0.03f }, new Vector2(1.8f, 1.8f), 0.45, gold);
            Burn(m, gold, 0.6, new Vector2(0.5f, 0.5f), 0.05, 1.2f, 6f);
        }
        else if (style == 2)
        {
            var seal = FxQuad(CircleFx, p + new Vector3(0, 1.2f, 0), new Vector2(1.6f, 1.6f), 1.4, gold, Flat, false, null, ("amt", 3f));
            var tw = seal.CreateTween();
            tw.TweenInterval(0.5);
            tw.TweenProperty(seal, "position:y", 0.03f, 0.14).SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.In);
            await Wait(0.66);
            Shake(0.12f); HitStop(0.05); Snd("SE_CARD_FINISH_BREAK_02");
            Warp(p + Vector3.Up * 0.04f, 2.4f, 0.4, 1.3f);
            Streaks(p + Vector3.Up * 0.05f, gold, 16, 2.6f);
            Burn(m, gold, 0.7, new Vector2(0.5f, 0.5f), 0.1, 1f, 6f);
        }
        else
        {
            var bars = new List<MeshInstance3D>();
            for (int i = 0; i < 10; i++)
            {
                float a = i * Mathf.Tau / 10; var off = new Vector3(Mathf.Cos(a) * 0.75f, 0, Mathf.Sin(a) * 0.65f);
                var b = FxStand(PillarFx, p + off * 1.6f, new Vector2(0.09f, 1.3f), 1.5, gold, false, null, ("amt", 1.3f));
                b.CreateTween().TweenProperty(b, "position", b.Position - off * 0.6f, 0.6).SetDelay(0.2).SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.In);
            }
            await Wait(0.85);
            ScreenFlash(Colors.White, 0.3f, 0.25); Flash(p + Vector3.Up * 0.4f, gold, 9f); HitStop(0.05);
            var s = CardSheet(m); s.Set("glow_col", Colors.White); Anim(s.M, "glow", 0f, 3f, 0.25);
            BurnAway(s, Colors.White, 0.4, null, 0.2, 0.6f, 4f);
        }
        await Wait(1.8);
    }

    // ---------------------------------------------------------------- 98-101 summons

    async Task BlueEyesFx(int style)
    {
        var v = Of(0, "Blue-Eyes White Dragon"); var at = Mon(v, 2, 0); var p = Grid3(at);
        var ice = new Color(0.65f, 0.85f, 1f);
        Snd("SE_MONSTER_CUTIN_LIGHT");
        if (style == 1)
        {
            for (int i = 0; i < 4; i++) { float a = i * Mathf.Tau / 4 + 0.4f; var q = p + new Vector3(Mathf.Cos(a) * 0.9f, 0, Mathf.Sin(a) * 0.8f); double d = i * 0.12; After(d, () => LightningHit(q, ice, 0.55f, dim: false)); }
            await Wait(0.6);
            ScreenFlash(ice, 0.3f, 0.3);
            for (int g = 0; g < 8; g++) { float a = g * Mathf.Tau / 8; Streaks(p + Vector3.Up * 0.1f, ice, 3, 3f, 0f, new Vector3(Mathf.Cos(a), 0.2f, Mathf.Sin(a)), 0.2f); }
            await Materialise(v, at, ice, 0.45, new Vector2(0.5f, 0.5f), 6f);
            Warp(p + Vector3.Up * 0.05f, 4f, 0.5, 1.6f); Shake(0.1f);
        }
        else if (style == 2)
        {
            var sweep = FxQuad(GustFx, new Vector3(-5f, 0.5f, 0), new Vector2(3.5f, 6f), 1.0, ice, new Basis(Vector3.Up, 0) * Flat, false, null, ("amt", 1f));
            sweep.CreateTween().TweenProperty(sweep, "position:x", 5f, 0.9).SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.InOut);
            ScreenDim(0.35f, 0.3, 0.4, 0.4);
            Snd("SE_ATTACK_A_WIND_SPECIAL_01", 0.1);
            await Wait(0.8);
            Place(v, at); var tf = Target(v, at); v.Root.GlobalTransform = tf.Translated(Vector3.Up * 2.5f);
            Fly(v, tf, 0.25f, Tween.TransitionType.Quad, Tween.EaseType.In);
            await Wait(0.25);
            FxQuad(RingFx, p with { Y = 0.03f }, new Vector2(3.2f, 3.2f), 0.6, ice); FxQuad(GroundGlowFx, p with { Y = 0.02f }, new Vector2(2.2f, 2.2f), 0.7, ice);
            Warp(p + Vector3.Up * 0.05f, 4.5f, 0.5, 2f); Shake(0.16f); HitStop(0.06);
            for (int i = 0; i < 16; i++) Ember(p + Vector3.Up * 0.05f, Colors.White, new Vector3(R(-1, 1), R(0, 0.3), R(-1, 1)) * 2f, 0.9, 0.04f, 0);
        }
        else
        {
            FxStand(PillarFx, p, new Vector2(1.4f, 6.5f), 1.3, ice, false, null, ("amt", 1.4f));
            FxStand(PillarFx, p, new Vector2(0.5f, 6.5f), 1.1, Colors.White, false, null, ("amt", 1f));
            Warp(p + Vector3.Up * 0.05f, 3f, 1.0, 1.2f);
            ScreenFlash(ice, 0.2f, 0.4); Shake(0.08f);
            await Wait(0.3);
            await Materialise(v, at, Colors.White, 0.7, new Vector2(0.5f, 1f), 6f);
        }
        Cue("land");
        await Wait(1.5);
    }

    async Task DarkMagicianFx(int style)
    {
        var v = Of(0, "Dark Magician"); var at = Mon(v, 2, 0); var p = Grid3(at);
        var violet = new Color(0.7f, 0.4f, 1f);
        Snd("SE_MONSTER_CUTIN_DARK");
        if (style == 1)
        {
            FxQuad(CircleFx, p + Vector3.Up * 0.02f, new Vector2(2.2f, 2.2f), 1.9, violet, Flat, false, null, ("amt", 1.5f));
            for (int i = 0; i < 8; i++) { float a = i * Mathf.Tau / 8; var q = p + new Vector3(Mathf.Cos(a) * 0.95f, 0, Mathf.Sin(a) * 0.95f); double d = 0.3 + i * 0.04; After(d, () => FxStand(FireFx, q, new Vector2(0.45f, 0.7f), 1.1, violet, false, null, ("col2", new Color(1f, 0.8f, 1f)), ("amt", 1f))); }
            await Wait(0.7);
            Place(v, at); var tf = Target(v, at); v.Root.GlobalTransform = tf.Translated(Vector3.Down * 0.6f).ScaledLocal(Vector3.One * 0.7f);
            Fly(v, tf, 0.5f, Tween.TransitionType.Back, Tween.EaseType.Out);
            FxQuad(CoreGlowFx, p + Vector3.Up * 0.2f, new Vector2(1.4f, 1.4f), 0.4, violet, Bill);
        }
        else if (style == 2)
        {
            Puffs(p, 8, new Color(0.18f, 0.08f, 0.28f, 0.85f), 1.1f, 0.25f, 0.4f);
            await Wait(0.3);
            Puffs(p, 6, new Color(0.25f, 0.12f, 0.38f, 0.7f), 1.3f, 0.6f, 0.5f);
            Place(v, at); HideFor(v, 0.55);
            await Wait(0.55);
            var tip = p + new Vector3(CW * 0.3f, 0.6f, -CH * 0.3f);
            FxQuad(CoreGlowFx, tip, new Vector2(0.6f, 0.6f), 0.4, new Color(1f, 0.85f, 1f), Bill); Streaks(tip, violet, 10, 2f);
            Snd("SE_CARD_ST_BREAK");
        }
        else
        {
            for (int i = 0; i < 16; i++)
            {
                float a = i * Mathf.Tau / 16; var from = p + new Vector3(Mathf.Cos(a) * 1.6f, 0.3f + (i % 3) * 0.2f, Mathf.Sin(a) * 1.3f);
                double d = i * 0.03; After(d, () => SwirlMote(from, p + Vector3.Up * 0.15f, i % 2 == 0 ? violet : Colors.White, 0.8, 1.2f, 1.2f, 0.13f));
            }
            await Wait(0.8);
            await Materialise(v, at, violet, 0.45, new Vector2(0.5f, 0.5f), 5f);
        }
        Cue("land");
        await Wait(1.5);
    }

    async Task RedEyesFx(int style)
    {
        var v = Of(0, "Red-Eyes B. Dragon"); var at = Mon(v, 2, 0); var p = Grid3(at);
        var red = new Color(1f, 0.25f, 0.12f);
        Snd("SE_MONSTER_CUTIN_FIRE");
        if (style == 1)
        {
            for (int i = 0; i < 3; i++) FxStand(FireFx, p + new Vector3((i - 1) * 0.25f, 0, 0), new Vector2(1.2f, 2.4f), 1.4, new Color(0.35f, 0.05f, 0.05f), false, null, ("col2", red), ("amt", 1.2f));
            FxQuad(GroundGlowFx, p with { Y = 0.02f }, new Vector2(1.8f, 1.8f), 1.4, red);
            for (int i = 0; i < 14; i++) Ember(p + new Vector3(R(-0.4, 0.4), 0.1f, R(-0.3, 0.3)), red, new Vector3(R(-0.2, 0.2), R(0.6, 1.4), R(-0.2, 0.2)), R(0.8, 1.3), 0.04f, 0.5f);
            await Wait(0.8);
            await Materialise(v, at, red, 0.5, new Vector2(0.5f, 1f), 6f);
        }
        else if (style == 2)
        {
            ScreenDim(0.55f, 0.25, 0.6, 0.3);
            await Wait(0.35);
            foreach (int s in new[] { -1, 1 }) FxQuad(CoreGlowFx, p + new Vector3(s * 0.22f, 0.9f, -0.5f), new Vector2(0.35f, 0.35f), 0.6, red, Bill);
            Snd("SE_CARD_ST_BREAK", 0.1);
            await Wait(0.55);
            Place(v, at);
            Explosion(p, 0.7f, red);
        }
        else
        {
            var sky = p + new Vector3(-2.5f, 4.5f, -2.5f);
            Orb(Line(sky, p + Vector3.Up * 0.2f), new Color(1f, 0.55f, 0.2f), 0.45, 1.0f);
            await Wait(0.45);
            Place(v, at);
            Explosion(p, 0.9f);
        }
        Cue("land");
        await Wait(1.6);
    }

    async Task EgyptianGodFx(int style)
    {
        var tribs = new[] { MineAt("Celtic Guardian", 1), MineAt("Feral Imp", 2), MineAt("Curse of Dragon", 3) };
        await Wait(0.4);
        var v = Of(0, "Obelisk the Tormentor"); var at = Mon(v, 2, 0); var p = Grid3(at);
        var gold = new Color(1f, 0.82f, 0.4f);
        Snd("SE_MONSTER_CUTIN_DIVINE");
        if (style == 3)
        {
            var sun = p + new Vector3(0, 1.1f, -0.6f);
            foreach (var t in tribs) { Orb(Line(t.Root.GlobalPosition + Vector3.Up * 0.1f, sun, 0.4f), gold, 0.6, 0.5f); var s = CardSheet(t); BurnAway(s, gold, 0.35, null, 0, 0.5f, 5f); }
            await Wait(0.6);
            var core = FxQuad(OrbFx, sun, new Vector2(1.6f, 1.6f), 1.0, gold, Bill, false, null, ("amt", 1.5f));
            FxQuad(RingFx, sun, new Vector2(2.4f, 2.4f), 0.6, gold, Bill);
            await Wait(0.6);
            core.CreateTween().TweenProperty(core, "position", p + Vector3.Up * 0.1f, 0.25).SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.In);
            await Wait(0.25);
            Place(v, at);
        }
        else
        {
            foreach (var t in tribs) SoulsOut(t, p);
            foreach (var t in tribs) { t.S = t.S with { Loc = Loc.None }; HideFor(t, 0); }
            await Wait(0.9);
            if (style == 1)
            {
                ScreenDim(0.5f, 0.3, 1.0, 0.5);
                await Wait(0.3);
                FxStand(PillarFx, p, new Vector2(2.4f, 7f), 1.6, gold, false, null, ("amt", 1.4f));
                FxStand(PillarFx, p, new Vector2(0.8f, 7f), 1.3, Colors.White, false, null, ("amt", 1f));
                FxQuad(CircleFx, p + Vector3.Up * 0.02f, new Vector2(3.2f, 3.2f), 1.8, gold, Flat, false, null, ("amt", 0.6f));
                Shake(0.15f); ScreenFlash(gold, 0.18f, 0.4);
                await Wait(0.3);
                await Materialise(v, at, Colors.White, 0.7, new Vector2(0.5f, 0f), 6f);
            }
            else
            {
                for (int i = 0; i < 5; i++) { int k = i; After(i * 0.18, () => { FxQuad(RingFx, p with { Y = 0.03f }, new Vector2(1.2f + k * 0.45f, 1.2f + k * 0.45f), 0.6, gold); Shake(0.05f); }); }
                FxQuad(GroundGlowFx, p with { Y = 0.02f }, new Vector2(2.6f, 2.6f), 1.6, gold);
                for (int i = 0; i < 20; i++) Ember(p + new Vector3(R(-0.8, 0.8), 0.05f, R(-0.7, 0.7)), gold, Vector3.Up * R(0.6, 1.4), R(0.9, 1.4), 0.045f, 0.5f);
                await Wait(0.6);
                Place(v, at); var tf = Target(v, at); v.Root.GlobalTransform = tf.Translated(Vector3.Down * 0.5f);
                Fly(v, tf, 0.7f, Tween.TransitionType.Sine, Tween.EaseType.Out);
                Shake(0.12f);
                await Wait(0.7);
            }
        }
        Warp(p + Vector3.Up * 0.05f, 5f, 0.6, 2.2f); Shake(0.2f); HitStop(0.07); Cue("land");
        await Wait(1.6);
    }

    // ---------------------------------------------------------------- 102 Jinzo

    const string FanFx = @"void fragment() {
  vec2 p = vec2(UV.x * 2.0 - 1.0, 1.0 - UV.y); float r = length(p); float a = atan(p.x, p.y);
  float sweep = (t * 2.0 - 1.0) * 1.1;
  float beam = smoothstep(0.3, 0.0, sweep - a) * step(a, sweep);
  float lead = exp(-pow((a - sweep) / 0.025, 2.0));
  float rings = 0.55 + 0.45 * sin(r * 45.0 - TIME * 12.0);
  ALBEDO = mix(col.rgb, vec3(1.0), lead * 0.5);
  ALPHA = clamp((beam * 0.3 * rings + lead) * smoothstep(1.0, 0.75, r) * smoothstep(0.0, 0.05, r), 0.0, 1.0) * (1.0 - smoothstep(0.85, 1.0, t));
}";

    async Task JinzoFx(int style)
    {
        var board = RivalBoard(); var traps = board.Where(v => v.S.Loc == Loc.Spell).ToList();
        foreach (var (name, zone) in new[] { ("Mirror Force", 0), ("Trap Hole", 2) }) { var tv = Of(1, name); Place(tv, SpellZ(zone, 1, false)); traps.Add(tv); }
        var j = Of(0, "Jinzo"); var at = Mon(j, 2, 0);
        Snd("SE_EV_IMPERIAL_ORDER");
        await Materialise(j, at, new Color(1f, 0.3f, 0.3f), 0.4, null, 4f, 0.5f);
        var jp = j.Root.GlobalPosition; var red = new Color(1f, 0.2f, 0.18f);
        void Kill(View tv)
        {
            // the trap flickers with red static and goes dead (grey)
            for (int i = 0; i < 7; i++) { int k = i; After(k * 0.055, () => { tv.Face.AlbedoColor = k % 2 == 0 ? new Color(0.45f, 0.4f, 0.45f) : Colors.White; tv.Face.Emission = k % 2 == 0 ? Colors.Black : red * 0.5f; }); }
            After(0.42, () => { tv.Face.AlbedoColor = new Color(0.38f, 0.36f, 0.4f); tv.Face.Emission = Colors.Black; });
            Arc(tv.Root.GlobalPosition + new Vector3(-0.2f, 0.05f, 0), tv.Root.GlobalPosition + new Vector3(0.2f, 0.05f, 0.1f), red, 0.25f, 0.25);
        }
        if (style == 1)
        {
            float far = jp.Z - ZonePos(Loc.Spell, 2, 1).Z + 0.8f;
            FxQuad(FanFx, new Vector3(jp.X, 0.03f, jp.Z - far / 2), new Vector2(far * 1.9f, far), 1.2, red);
            Snd("SE_DUEL_LIVE_THUNDER", 0.2);
            foreach (var tv in traps) { float a = Mathf.Atan2(tv.Root.GlobalPosition.X - jp.X, jp.Z - tv.Root.GlobalPosition.Z); double d = ((a / 1.1f) + 1) / 2 * 1.2; After(d, () => { Kill(tv); Streaks(tv.Root.GlobalPosition + Vector3.Up * 0.05f, red, 6, 1.4f); }); }
            await Wait(1.4);
        }
        else if (style == 2)
        {
            var eye = jp + new Vector3(0, 0.6f, -0.2f);
            foreach (var tv in traps.OrderBy(x => x.Root.GlobalPosition.X))
            {
                var tp = tv.Root.GlobalPosition + Vector3.Up * 0.03f;
                foreach (int s in new[] { -1, 1 }) Beam(eye + new Vector3(s * 0.08f, 0, 0), tp, 0.12f, 0.35, red);
                FxQuad(GroundGlowFx, tp, new Vector2(1f, 1f), 0.4, red);
                Kill(tv); Snd("SE_SOLO_GATE078_ELECTRIC");
                await Wait(0.25);
            }
        }
        else
        {
            for (int i = 0; i < 3; i++) { int k = i; After(i * 0.1, () => FxQuad(RingFx, jp with { Y = 0.04f }, new Vector2(1.6f + k * 0.9f, 1.6f + k * 0.9f), 0.45, new Color(1f, 0.45f, 0.4f))); }
            for (int i = 0; i < 6; i++) { float a = i * Mathf.Tau / 6; Arc(jp + Vector3.Up * 0.1f, jp + new Vector3(Mathf.Cos(a) * 1.2f, 0.05f, Mathf.Sin(a) * 1.0f), red, 0.4f, 0.3); }
            Warp(jp + Vector3.Up * 0.05f, 4.5f, 0.6, 1.2f);
            Snd("SE_DUEL_LIVE_THUNDER");
            foreach (var tv in traps) { double d = (jp - tv.Root.GlobalPosition).Length() / 4.5 * 0.7; After(d, () => { var tp = tv.Root.GlobalPosition; Arc(tp + new Vector3(R(-0.4, 0.4), 0.8f, 0), tp + Vector3.Up * 0.03f, new Color(1f, 0.5f, 0.5f), 0.6f, 0.3); Kill(tv); Streaks(tp, Colors.White, 8, 1.8f); }); }
            await Wait(1.0);
        }
        await Wait(1.4);
    }

    // ---------------------------------------------------------------- 103 Time Wizard

    async Task TimeWizardFx(int style)
    {
        var foes = TheirMonsters();
        var w = MineAt("Time Wizard", 2);
        Snd("SE_EV_TRANSMISSION_GEAR");
        var wp = w.Root.GlobalPosition; var gold = new Color(1f, 0.85f, 0.5f); var dust = new Color(0.75f, 0.68f, 0.55f);
        void Crumble(View v, double d)
        {
            After(d, () =>
            {
                var s = CardSheet(v);
                s.Set("glow_col", new Color(0.6f, 0.45f, 0.2f)); Anim(s.M, "glow", 0f, 0.3f, 0.3); Anim(s.M, "darken", 0f, 0.35f, 0.4);
                Flakes(s, 30, uv => new Vector3(R(-0.2, 0.2), R(-0.1, 0.3), R(-0.2, 0.2)), 0.9, 0.5, null, 0.1f, 2.5f);
                BurnAway(s, dust, 0.6, new Vector2(0.5f, 0f), 0.2, 0.3f, 1.5f);
                After(0.5, () => Puffs(v.Root.GlobalPosition, 2, new Color(0.6f, 0.55f, 0.45f, 0.45f), 0.7f, 0.3f, 0.25f));
            });
        }
        if (style == 1)
        {
            var at = wp + new Vector3(0, 0.9f, -0.6f);
            var clock = FxQuad(ClockFx, at, new Vector2(1.4f, 1.4f), 2.2, gold, Bill, false, null, ("amt", 0f));
            Anim((ShaderMaterial)clock.MaterialOverride, "amt", 0f, 4.25f, 1.5, 0.2, Tween.TransitionType.Cubic, Tween.EaseType.Out);
            for (int i = 0; i < 10; i++) Snd("SE_DUEL_TIME_COUNT", 0.25 + i * (0.05 + i * 0.012));
            await Wait(1.75);
            FxQuad(CoreGlowFx, at, new Vector2(1.6f, 1.6f), 0.35, gold, Bill); Snd("SE_CF_GET_NORMAL");
            await Wait(0.2);
            foreach (var v in foes) Crumble(v, R(0, 0.2));
            await Wait(1.2);
        }
        else if (style == 2)
        {
            for (int i = 0; i < 4; i++) { int k = i; After(i * 0.25, () => FxQuad(ClockFx, wp with { Y = 0.03f }, new Vector2(1.4f + k * 0.8f, 1.4f + k * 0.8f), 0.9, gold * new Color(1, 1, 1, 0.6f), Flat, false, null, ("amt", k * 0.3f))); }
            Snd("SE_DUEL_TIME_COUNT_ZERO", 0.3);
            await Wait(0.6);
            foreach (var v in foes) { v.Face.AlbedoColor = new Color(1f, 0.85f, 0.6f); Crumble(v, 0.4 + R(0, 0.2)); }
            await Wait(1.6);
        }
        else
        {
            foreach (var v in foes)
            {
                var at = v.Root.GlobalPosition + new Vector3(0, 0.5f, -0.35f);
                var c = FxQuad(ClockFx, at, new Vector2(0.6f, 0.6f), 1.3, gold, Bill, false, null, ("amt", 0f));
                Anim((ShaderMaterial)c.MaterialOverride, "amt", 0f, -3f, 1.0, 0.1, Tween.TransitionType.Quad, Tween.EaseType.In);
                Crumble(v, 0.9);
            }
            for (int i = 0; i < 8; i++) Snd("SE_DUEL_TIME_COUNT", 0.15 + i * 0.1);
            await Wait(2.0);
        }
        await Wait(1.0);
    }

    // ---------------------------------------------------------------- 104 Chaos summon

    const string YinYangFx = @"void fragment() {
  vec2 p = UV * 2.0 - 1.0; float r = length(p); float a = atan(p.y, p.x) + TIME * 2.0;
  vec2 q = vec2(cos(a), sin(a)) * r;
  float side = step(0.0, q.y);
  float s1 = smoothstep(0.52, 0.48, length(q - vec2(0.5, 0.0))); float s2 = smoothstep(0.52, 0.48, length(q + vec2(0.5, 0.0)));
  float light = clamp(side + s1 - s2, 0.0, 1.0);
  float disc = smoothstep(1.0, 0.94, r) * amt;
  float rim = exp(-pow((r - 0.97) / 0.03, 2.0)) * amt;
  float hole = smoothstep(0.0, 0.25, r - (1.0 - amt) * 1.2);
  ALBEDO = mix(vec3(0.08, 0.03, 0.14), vec3(1.0, 0.95, 0.85), light) + col.rgb * rim;
  ALPHA = clamp(disc * 0.45 * smoothstep(1.0, 0.6, r) + rim, 0.0, 1.0) * hole * env(0.1, 0.25);
}";

    async Task ChaosFx(int style)
    {
        var l = Of(0, "Celtic Guardian"); Place(l, GyAt(0)); var d0 = Of(0, "Feral Imp"); Place(d0, GyAt(0));
        var v = Of(0, "Black Luster Soldier - Envoy of the Beginning"); var at = Mon(v, 2, 0); var p = Grid3(at);
        var light = new Color(1f, 0.95f, 0.8f); var dark = new Color(0.55f, 0.3f, 1f); var gy = GyPos(0);
        Snd("SE_EV_ULTIMATE_SLAYER");
        if (style == 1)
        {
            var c = p + Vector3.Up * 0.5f;
            Orb(k => { float a = k * 3f * Mathf.Tau, rad = 1.2f * (1 - k * k); return k < 0.25f ? gy.Lerp(c, k * 4) + Vector3.Up * 0.3f : c + new Vector3(Mathf.Cos(a) * rad, 0.2f, Mathf.Sin(a) * rad * 0.8f); }, light, 1.4, 0.55f, null, Tween.EaseType.InOut);
            Orb(k => { float a = k * 3f * Mathf.Tau + Mathf.Pi, rad = 1.2f * (1 - k * k); return k < 0.25f ? gy.Lerp(c, k * 4) + Vector3.Up * 0.3f : c + new Vector3(Mathf.Cos(a) * rad, 0.2f, Mathf.Sin(a) * rad * 0.8f); }, dark, 1.4, 0.55f, null, Tween.EaseType.InOut);
            await Wait(1.4);
            ScreenFlash(Colors.White, 0.45f, 0.35); Warp(c, 4f, 0.5, 2f, false, Bill); HitStop(0.06); Shake(0.14f);
            FxQuad(CoreGlowFx, c, new Vector2(2.2f, 2.2f), 0.4, light, Bill);
            await Materialise(v, at, light, 0.35, new Vector2(0.5f, 0.5f), 6f);
        }
        else if (style == 2)
        {
            var pl = FxStand(PillarFx, p + new Vector3(-1.4f, 0, 0), new Vector2(0.9f, 5f), 1.2, light, false, null, ("amt", 1.2f));
            var pd = FxStand(PillarFx, p + new Vector3(1.4f, 0, 0), new Vector2(0.9f, 5f), 1.2, dark, false, null, ("amt", 1.4f));
            foreach (var (q, s) in new[] { (pl, 1f), (pd, -1f) }) q.CreateTween().TweenProperty(q, "position:x", q.Position.X + s * 1.4f, 0.5).SetDelay(0.5).SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.In);
            await Wait(1.0);
            ScreenFlash(Colors.White, 0.5f, 0.3); Warp(p + Vector3.Up * 0.3f, 5f, 0.5, 2.4f); Shake(0.18f); HitStop(0.06);
            Streaks(p + Vector3.Up * 0.3f, light, 14, 4f); Streaks(p + Vector3.Up * 0.3f, dark, 14, 4f);
            await Materialise(v, at, Colors.White, 0.3, null, 6f);
        }
        else
        {
            var disc = FxQuad(YinYangFx, p + Vector3.Up * 0.02f, new Vector2(1.9f, 1.9f), 2.0, new Color(0.9f, 0.8f, 1f), Flat, true, null, ("amt", 0f));
            Anim((ShaderMaterial)disc.MaterialOverride, "amt", 0f, 1f, 0.5, 0, Tween.TransitionType.Back, Tween.EaseType.Out);
            await Wait(0.8);
            await Materialise(v, at, new Color(0.9f, 0.8f, 1f), 0.6, new Vector2(0.5f, 0.5f), 5f);
        }
        Cue("land");
        await Wait(1.5);
    }
}
