using Godot;
using PoC.Engine;

namespace PoC.Game.UI;

/// 3D effects on the table: summon landings, attacks, destruction, activation glow, camera shake.
public partial class DuelField
{
    void After(double s, Action a) => GetTree().CreateTimer(s).Timeout += a;

    void Shockwave(Vector3 at, Color c, float size)
    {
        var m = Shade(RingShader, ("col", c), ("fade", 1f));
        var q = new MeshInstance3D { Mesh = new QuadMesh { Size = Vector2.One }, RotationDegrees = new Vector3(-90, 0, 0), Position = new Vector3(at.X, 0.02f, at.Z), MaterialOverride = m, Scale = Vector3.One * 0.3f, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off };
        world.AddChild(q);
        var tw = CreateTween().SetParallel();
        tw.TweenProperty(q, "scale", Vector3.One * size, 0.6).SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
        tw.TweenMethod(Callable.From<float>(f => m.SetShaderParameter("fade", f)), 1f, 0f, 0.6);
        tw.Chain().TweenCallback(Callable.From(q.QueueFree));
    }

    void Flash(Vector3 at, Color c, float energy)
    {
        var l = new OmniLight3D { Position = at + new Vector3(0, 0.7f, 0), LightColor = c, OmniRange = 3.2f, LightEnergy = 0 };
        world.AddChild(l);
        var tw = CreateTween();
        tw.TweenProperty(l, "light_energy", energy, 0.05);
        tw.TweenProperty(l, "light_energy", 0f, 0.55).SetTrans(Tween.TransitionType.Expo).SetEase(Tween.EaseType.Out);
        tw.TweenCallback(Callable.From(l.QueueFree));
    }

    /// Round, soft-edged particles (a square quad reads as a hard little block on screen).
    static GradientTexture2D? softDot;
    static StandardMaterial3D Glowing(float brightness)
    {
        softDot ??= new GradientTexture2D
        {
            Width = 64, Height = 64, Fill = GradientTexture2D.FillEnum.Radial, FillFrom = new Vector2(0.5f, 0.5f), FillTo = new Vector2(1f, 0.5f),
            Gradient = new Gradient { Offsets = new[] { 0f, 0.35f, 1f }, Colors = new[] { Colors.White, new Color(1, 1, 1, 0.45f), new Color(1, 1, 1, 0) } },
        };
        return new StandardMaterial3D
        {
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded, Transparency = BaseMaterial3D.TransparencyEnum.Alpha, BlendMode = BaseMaterial3D.BlendModeEnum.Add,
            BillboardMode = BaseMaterial3D.BillboardModeEnum.Particles, VertexColorUseAsAlbedo = true, AlbedoColor = new Color(brightness, brightness, brightness),
            AlbedoTexture = softDot, DisableReceiveShadows = true,
        };
    }

    void Burst(Vector3 at, Color c, int n, float speed)
    {
        var grad = new Gradient();
        grad.SetColor(0, new Color(c.Lightened(0.4f), 1)); grad.SetColor(1, new Color(c, 0));
        var p = new GpuParticles3D
        {
            Position = at + new Vector3(0, 0.1f, 0), Amount = n, Lifetime = 0.9, OneShot = true, Explosiveness = 1,
            ProcessMaterial = new ParticleProcessMaterial
            {
                Direction = new Vector3(0, 1, 0), Spread = 75, InitialVelocityMin = speed * 0.3f, InitialVelocityMax = speed, Gravity = new Vector3(0, -3.5f, 0),
                DampingMin = 1, DampingMax = 2, ScaleMin = 0.5f, ScaleMax = 1.4f, ColorRamp = new GradientTexture1D { Gradient = grad },
            },
            DrawPass1 = new QuadMesh { Size = new Vector2(0.11f, 0.11f), Material = Glowing(3) },
        };
        world.AddChild(p);
        p.Emitting = true;
        GetTree().CreateTimer(1.6).Timeout += p.QueueFree;
    }

    void Shake(float amount)
    {
        var tw = CreateTween();
        var rng = new RandomNumberGenerator();
        for (int i = 0; i < 7; i++)
        {
            float k = amount * (1 - i / 7f);
            tw.TweenProperty(cam, "position", camHome + new Vector3(rng.RandfRange(-k, k), rng.RandfRange(-k, k), rng.RandfRange(-k, k)), 0.035);
        }
        tw.TweenProperty(cam, "position", camHome, 0.06);
    }

    /// Where a direct attack lands: just past the defender's back row.
    static Vector3 DirectPoint(int attacker) => new(0, 0.3f, attacker == 0 ? -3.0f : 3.0f);

    /// The attacker charges its target (or the rival). grind: the defender holds (DEF too high), so the
    /// attacker grinds against it in a stream of sparks, then is thrown back.
    /// onHit replaces the small flash at the moment of impact; repel throws the attacker back afterwards.
    void Lunge(View? a, View? t, int side, bool grind = false, Action<Vector3>? onHit = null, bool repel = false, bool quiet = false, string? windup = null)
    {
        if (a == null) return;
        void C(string n) { if (!quiet) Cue(n, 0, a.Card); }
        // The wind-up sound says what kind of attack it is (blocked, locked, mutual, direct); a plain attack has none.
        C(windup ?? (t == null ? (side == 0 ? "windup_direct" : "windup_direct_rival") : grind ? "windup_lock" : repel ? "windup_block" : "windup"));
        a.Tw?.Kill();
        var home = Target(a, a.S);
        var start = home.Origin;
        var dest = t != null ? t.Root.GlobalPosition : DirectPoint(side);
        var hit = start.Lerp(dest, 0.82f) + Vector3.Up * 0.2f;
        var back = start + Vector3.Up * 0.6f + (start - dest).Normalized() * 0.2f;
        a.Stat.Visible = false;
        a.Tw = CreateTween();
        a.Tw.TweenProperty(a.Root, "position", back, 0.24).SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.Out);
        a.Tw.TweenInterval(0.08);
        a.Tw.TweenProperty(a.Root, "position", hit, 0.14).SetTrans(Tween.TransitionType.Expo).SetEase(Tween.EaseType.In);
        if (grind && t != null)
        {
            a.Tw.TweenCallback(Callable.From(() => { C("grind"); Flash(hit, new Color(1f, 0.9f, 0.75f), 4); GrindSparks(hit, 0.34); }));
            a.Tw.TweenProperty(a.Root, "position", hit + (dest - start).Normalized() * 0.05f, 0.34);
            a.Tw.TweenCallback(Callable.From(() => { C("grind_release"); C("repel"); Glint("md/flash", new Color(0.87f, 0.95f, 1f), hit, 420, 0.3f, 1.1f, 0.38); FrameBurst(t, FxIce, 0.95f, 1.25f, 0.46); Flash(hit, FxIce, 7); Shake(0.09f); }));
            a.Tw.TweenProperty(a.Root, "position", start + (start - dest).Normalized() * 0.4f + Vector3.Up * 0.05f, 0.3).SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
        }
        else
        {
            a.Tw.TweenCallback(Callable.From(() =>
            {
                C(t == null ? "direct_hit" : "hit");
                if (onHit != null) { onHit(hit); return; }
                Glint("md/flash", new Color(1f, 0.95f, 0.85f), hit, 240, 0.3f, 1f, 0.28);
                Shake(0.06f);
                if (t != null) Jolt(t, (dest - start).Normalized());
            }));
            if (repel) a.Tw.TweenCallback(Callable.From(() => C("repel")));
            if (repel) a.Tw.TweenProperty(a.Root, "position", start + (start - dest).Normalized() * 0.4f + Vector3.Up * 0.05f, 0.3).SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
        }
        a.Tw.TweenProperty(a.Root, "position", start, 0.42).SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
        a.Tw.TweenCallback(Callable.From(() => { a.Root.Transform = Target(a, a.S); UpdateStat(a, false); }));
    }

    void Jolt(View t, Vector3 dir)
    {
        if (t.Tw != null && t.Tw.IsRunning()) return;   // never cut a move short (a face-down defender turning over)
        var home = Target(t, t.S);
        t.Tw = CreateTween();
        t.Tw.TweenProperty(t.Root, "position", home.Origin + dir * 0.18f + Vector3.Up * 0.08f, 0.06);
        t.Tw.TweenProperty(t.Root, "position", home.Origin, 0.3).SetTrans(Tween.TransitionType.Elastic).SetEase(Tween.EaseType.Out);
    }

    /// Light sweep across the card on the table.
    void Shine(View? v)
    {
        if (v == null) return;
        var tw = CreateTween();
        tw.TweenInterval(0.15);
        tw.TweenMethod(Callable.From<float>(t => v.ShineMat.SetShaderParameter("t", t)), 0f, 1f, 0.55);
        tw.TweenMethod(Callable.From<float>(t => v.ShineMat.SetShaderParameter("t", t)), 0f, 1f, 0.45);
        tw.TweenCallback(Callable.From(() => v.ShineMat.SetShaderParameter("t", -1f)));
    }

    /// A triggered effect that asks for a choice (search, target...) shows its source like an activation.
    async Task EffectFlash(Card c, int player)
    {
        if (c.Uid == lastRays && Time.GetTicksMsec() - lastRaysAt < 4000) return;
        if (views.TryGetValue(c.Uid, out var v)) GoldRays(v);
        Focus(c);
        await Wait(1.0);
    }

    // ---- links (equips, continuous boosts): the gold thread while either card is hovered

    readonly List<Node> linkFx = new();
    readonly HashSet<int> linked = new();

    void ShowLinks(int uid)
    {
        foreach (var n in linkFx) n.QueueFree();
        linkFx.Clear();
        linked.Clear();
        if (uid >= 0)
        {
            var c = views[uid].Card;
            var others = D.Cards.Where(x => x.OnField && x.EquippedTo == c).ToList();
            if (c.EquippedTo is { OnField: true } m) others.Add(m);
            others.AddRange(D.TiedTo(c));
            // Continuous stat changes (Field Spells, Bladefly...): the card and every monster it is boosting or weakening.
            if (!thinking) others.AddRange(D.AffectedBy(c));
            if (!thinking && c.Loc == Loc.Monster) others.AddRange(D.FieldCards.Where(src => src != c && src.EquippedTo != c && D.AffectedBy(src).Contains(c)));
            others = others.Distinct().ToList();
            var from = Target(views[uid], views[uid].S).Origin;
            foreach (var o in others)
            {
                linked.Add(o.Uid);
                var to = Target(views[o.Uid], views[o.Uid].S).Origin;
                if (new Vector2(to.X - from.X, to.Z - from.Z).Length() < 0.05f) continue;
                linkFx.Add(LightLine(from, to, FxGold, 0.2));   // the equip's gold thread
            }
            if (linked.Count > 0) linked.Add(uid);
        }
        RefreshHints();
    }

    static readonly string CoinFace = TurnOrder.Face
        .Replace("shader_type spatial;", "shader_type spatial;\nrender_mode unshaded;")
        .Replace("ALBEDO = mix(gold, art.rgb, art.a * inside);", "ALBEDO = mix(gold, art.rgb, art.a * inside) * 0.82;");

    /// Coin toss: 3D coins (Millennium Puzzle = heads, Eye = tails) flip up over the field and land on the
    /// engine's results, then the outcome is spelled out.
    void Coins(Ev e, bool words = true)
    {
        int n = Math.Max(1, e.Amount);
        int heads = System.Numerics.BitOperations.PopCount((uint)e.Arg);
        var col = e.Player == Me ? Pm.You : Pm.Them;
        if (e.Other != null) Focus(e.Other);
        for (int i = 0; i < n; i++) { int k = i; After(k * 0.15, () => Cue("coin_flip", k)); After(k * 0.15 + 1.45, () => Cue("coin_land", k)); }
        var center = new Vector3(0, 2.2f, 0.9f);
        // Unlit: each coin faces the camera at a slightly different angle, and lit metal made the one catching
        // the sun's highlight blow out (left bright, right dim). The face keeps its own gold shading.
        var rim = new StandardMaterial3D { AlbedoColor = new Color(0.62f, 0.44f, 0.14f), ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded };
        ShaderMaterial FaceMat(string img, float scale) => Shade(CoinFace, ("tex", Tex($"res://assets/coin/{img}.png")), ("art_scale", scale));
        var faceMesh = new QuadMesh { Size = new Vector2(2.4f, 2.4f) };
        var coins = new List<Node3D>();
        for (int i = 0; i < n; i++)
        {
            bool h = (e.Arg & (1 << i)) != 0;
            var holder = new Node3D { Position = center + new Vector3((i - (n - 1) / 2f) * 1.05f, 0, 0) };
            world.AddChild(holder);
            holder.LookAt(cam.GlobalPosition, Vector3.Up);
            var coin = new Node3D { Scale = Vector3.One * 0.001f };
            holder.AddChild(coin);
            coin.AddChild(new MeshInstance3D { Mesh = new CylinderMesh { TopRadius = 1.2f, BottomRadius = 1.2f, Height = 0.14f, RadialSegments = 64 }, MaterialOverride = rim, RotationDegrees = new Vector3(90, 0, 0) });
            coin.AddChild(new MeshInstance3D { Mesh = faceMesh, MaterialOverride = FaceMat("puzzle", 0.72f), Position = new Vector3(0, 0, 0.071f) });
            coin.AddChild(new MeshInstance3D { Mesh = faceMesh, MaterialOverride = FaceMat("eye", 0.86f), Position = new Vector3(0, 0, -0.071f), RotationDegrees = new Vector3(0, 180, 0) });
            coins.Add(holder);
            // After LookAt the -Z side (Eye) faces the camera; half a turn more shows the Puzzle.
            float turns = 5 + i + (h ? 0.5f : 0f);
            var tw = CreateTween();
            tw.TweenInterval(i * 0.15);
            tw.TweenProperty(coin, "scale", Vector3.One * 0.36f, 0.15);
            tw.TweenMethod(Callable.From<float>(k =>
            {
                float lift = 4f * k * (1 - k);
                coin.Position = new Vector3(0, lift * 1.1f, 0);
                coin.Rotation = new Vector3(Mathf.Tau * turns * Mathf.SmoothStep(0, 1, k), 0, 0);
            }), 0f, 1f, 1.3);
            tw.TweenProperty(coin, "scale", Vector3.One * 0.42f, 0.08);
            tw.TweenProperty(coin, "scale", Vector3.One * 0.36f, 0.25).SetTrans(Tween.TransitionType.Elastic).SetEase(Tween.EaseType.Out);
            tw.TweenInterval(1.0 + (n - 1 - i) * 0.15);
            tw.TweenProperty(coin, "scale", Vector3.One * 0.001f, 0.2);
            tw.TweenCallback(Callable.From(holder.QueueFree));   // the coin's own tween frees it (never mid-flip)
        }
        // Once every coin has landed, one chip: whose call and whether it was right, or whether the effect worked;
        // green when it goes your way.
        bool good = e.Call >= 0 ? ((e.Arg & 1) != 0) == (e.Call == 1) : n == 1 ? heads == 1 : heads * 2 > n;
        bool mine = e.Player == Me;
        string text = e.Call >= 0 ? $"{(mine ? "YOU" : ThemName.ToUpperInvariant())} CALLED {(good ? "RIGHT" : "WRONG")}" : good ? "EFFECT SUCCEEDS" : "EFFECT FAILS";
        if (words) After(1.7 + n * 0.15, () =>
        {
            Cue(good ? "coin_good" : "coin_bad");
            ResultChip(text, good == mine ? Good : Bad);
        });
    }

    readonly List<View> badged = new();

    void Badge(View? v, int link)
    {
        if (v == null) return;
        v.Badge.Text = link.ToString();
        v.Badge.Visible = true;
        v.Badge.Scale = Vector3.One * 0.01f;
        CreateTween().TweenProperty(v.Badge, "scale", Vector3.One, 0.3).SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
        badged.Add(v);
    }

    void ClearBadges()
    {
        foreach (var v in badged) v.Badge.Visible = false;
        badged.Clear();
    }
}
