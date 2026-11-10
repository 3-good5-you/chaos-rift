using Godot;
using PoC.Engine;

namespace PoC.Game.UI;

/// The effects picked in the VFX Lab (remake/samples/vfx-lab.html, then the in-game `--vfxlab`): battle destroy =
/// MD crack + shatter, effect destroy = burn to embers, attack / defense placement = light swords / light shield,
/// chain link = a gold number on the card's corner, negated = glitch, blocked attack = hit-stop + shield,
/// fusion = souls swirl and the monster rebuilds on the field, tribute = souls to the summon, effect trigger = gold rays,
/// then the rest of the duel's events (12-35, numbered as in the lab). Sprites in assets/vfx (remake/tools/vfx_sprites.py),
/// Master Duel textures in assets/vfx/md.
public partial class DuelField
{
    static readonly Color FxGold = new(1f, 0.85f, 0.54f), FxWarm = new(1f, 0.945f, 0.78f), FxIce = new(0.62f, 0.83f, 1f), FxEmber = new(1f, 0.7f, 0.28f);
    static readonly Color FxViolet = new(0.79f, 0.64f, 1f), FxGreen = new(0.56f, 0.94f, 0.72f), FxPink = new(1f, 0.6f, 0.82f), FxRed = new(1f, 0.42f, 0.36f);
    static Texture2D VfxTex(string name) => Tex($"res://assets/vfx/{name}.png");
    static CanvasItemMaterial? addMat;
    static CanvasItemMaterial AddMat => addMat ??= new CanvasItemMaterial { BlendMode = CanvasItemMaterial.BlendModeEnum.Add };

    /// Fade in over the first `fin` of k, out over the last `fout`.
    static float Env(float k, float fin = 0.12f, float fout = 0.4f) => Mathf.Clamp(k / fin, 0, 1) * (k > 1 - fout ? (1 - k) / fout : 1);

    static StandardMaterial3D Additive(Texture2D tex, Color c, bool billboard = false) => new()
    {
        ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded, Transparency = BaseMaterial3D.TransparencyEnum.Alpha, BlendMode = BaseMaterial3D.BlendModeEnum.Add,
        AlbedoTexture = tex, AlbedoColor = c, CullMode = BaseMaterial3D.CullModeEnum.Disabled, DisableReceiveShadows = true,
        BillboardMode = billboard ? BaseMaterial3D.BillboardModeEnum.Enabled : BaseMaterial3D.BillboardModeEnum.Disabled,
    };

    // ---------------------------------------------------------------- building blocks

    /// A texture lying on the table (rings, card frames, circles) that grows from s0 to s1 and fades.
    void Decal(string tex, Color tint, Vector3 at, Vector2 size, float s0, float s1, double dur, float yaw = 0, float spin = 0, float alpha = 1, float y = 0.04f)
    {
        var m = Additive(VfxTex(tex), new Color(tint, 0));
        var q = new MeshInstance3D { Mesh = new QuadMesh { Size = size }, MaterialOverride = m, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off };
        world.AddChild(q);
        q.Position = new Vector3(at.X, y, at.Z);
        q.Rotation = new Vector3(-Mathf.Pi / 2, yaw, 0);
        q.Scale = Vector3.One * s0;
        var tw = CreateTween().SetParallel();
        tw.TweenProperty(q, "scale", Vector3.One * s1, dur).SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
        tw.TweenMethod(Callable.From<float>(k => m.AlbedoColor = new Color(tint, alpha * Env(k))), 0f, 1f, dur);
        if (spin != 0) tw.TweenProperty(q, "rotation:y", yaw + spin, dur);
        tw.Chain().TweenCallback(Callable.From(q.QueueFree));
    }

    /// A texture over the screen at a world point (flashes, glints, rays): additive, grows and fades.
    void Glint(string tex, Color tint, Vector3 at, float size, float s0, float s1, double dur, float spin = 0, float alpha = 1) =>
        GlintAt(tex, tint, cam.UnprojectPosition(at), size, s0, s1, dur, spin, alpha);

    /// The same at a screen point (HUD panels, the middle of the screen).
    void GlintAt(string tex, Color tint, Vector2 p, float size, float s0, float s1, double dur, float spin = 0, float alpha = 1)
    {
        var r = new TextureRect
        {
            Texture = VfxTex(tex), ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.Scale,
            Size = new Vector2(size, size), Position = p - new Vector2(size, size) / 2, PivotOffset = new Vector2(size, size) / 2,
            Material = AddMat, MouseFilter = MouseFilterEnum.Ignore, Scale = Vector2.One * s0, SelfModulate = new Color(tint, 0),
        };
        hud.AddChild(r);
        var tw = CreateTween().SetParallel();
        tw.TweenProperty(r, "scale", Vector2.One * s1, dur).SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.Out);
        tw.TweenMethod(Callable.From<float>(k => r.SelfModulate = new Color(tint, alpha * Env(k, 0.1f, 0.6f))), 0f, 1f, dur);
        if (spin != 0) tw.TweenProperty(r, "rotation", spin, dur);
        tw.Chain().TweenCallback(Callable.From(r.QueueFree));
    }

    /// MD card-shaped light frame bursting off a card on the table.
    void FrameBurst(View v, Color tint, float s0 = 0.75f, float s1 = 1.25f, double dur = 0.52, float alpha = 1)
    {
        var at = Target(v, v.S).Origin;
        Decal("md/cardimp", tint, at, new Vector2(CW * 1.75f, CH * 1.6f), s0, s1, dur, v.S.Loc == Loc.Monster && v.S.Def ? Mathf.Pi / 2 : 0, alpha: alpha);
    }

    /// The soft MD ring under a card as it lands.
    void SoftLand(View v, Color tint, string cue = "land")
    {
        Cue(cue);
        Decal("md/softring", tint, Target(v, v.S).Origin, new Vector2(2.6f, 2.6f), 0.35f, 1.15f, 0.48, alpha: 0.9f);
        Shake(0.03f);
    }

    /// A big arrival: card frame, spiked ring and glare.
    void SummonImpact(View v, Color tint, Color ring, string cue)
    {
        var at = Target(v, v.S).Origin;
        Cue(cue);
        Shake(0.1f);
        FrameBurst(v, tint, 0.75f, 1.4f, 0.56);
        Decal("md/spikering", ring, at, new Vector2(3.3f, 3.3f), 0.3f, 1.25f, 0.52);
        Glint("md/glare", tint, at + Vector3.Up * 0.2f, 460, 0.4f, 1f, 0.34, alpha: 0.8f);
    }

    /// A glowing mote that flies from a to b along an arc (souls, sparks of light).
    void Mote(Vector3 a, Vector3 b, Color c, double dur, float size, float arc)
    {
        var m = Additive(SoftDot, c, billboard: true);
        var q = new MeshInstance3D { Mesh = new QuadMesh { Size = new Vector2(size, size) }, MaterialOverride = m, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off };
        world.AddChild(q);
        var mid = (a + b) / 2 + new Vector3((float)GD.RandRange(-0.6, 0.6), arc, (float)GD.RandRange(-0.3, 0.3));
        var tw = CreateTween();
        tw.TweenMethod(Callable.From<float>(t =>
        {
            float e = t < 0.5f ? 2 * t * t : 1 - Mathf.Pow(-2 * t + 2, 2) / 2, u = 1 - e;
            q.Position = u * u * a + 2 * u * e * mid + e * e * b;
            m.AlbedoColor = new Color(c, Mathf.Min(1, (1 - t) * 5));
        }), 0f, 1f, dur);
        tw.TweenCallback(Callable.From(q.QueueFree));
    }
    static GradientTexture2D SoftDot => softDot ??= new GradientTexture2D
    {
        Width = 64, Height = 64, Fill = GradientTexture2D.FillEnum.Radial, FillFrom = new Vector2(0.5f, 0.5f), FillTo = new Vector2(1f, 0.5f),
        Gradient = new Gradient { Offsets = new[] { 0f, 0.35f, 1f }, Colors = new[] { Colors.White, new Color(1, 1, 1, 0.45f), new Color(1, 1, 1, 0) } },
    };

    /// MD shard fragments bursting from a point and falling (chain snap).
    void Shards(Vector3 at, Color c, int n)
    {
        var grad = new Gradient(); grad.SetColor(0, c); grad.SetColor(1, new Color(c, 0));
        var mat = Additive(VfxTex("md/shards"), Colors.White);
        mat.BillboardMode = BaseMaterial3D.BillboardModeEnum.Particles; mat.VertexColorUseAsAlbedo = true;
        var p = new GpuParticles3D
        {
            Position = at, Amount = n, Lifetime = 0.95, OneShot = true, Explosiveness = 1,
            ProcessMaterial = new ParticleProcessMaterial
            {
                Direction = new Vector3(0, 1, 0), Spread = 80, InitialVelocityMin = 0.8f, InitialVelocityMax = 2.2f, Gravity = new Vector3(0, -6f, 0),
                AngleMin = 0, AngleMax = 360, AngularVelocityMin = -300, AngularVelocityMax = 300, ScaleMin = 0.6f, ScaleMax = 1.2f, ColorRamp = new GradientTexture1D { Gradient = grad },
            },
            DrawPass1 = new QuadMesh { Size = new Vector2(0.2f, 0.2f), Material = mat },
        };
        world.AddChild(p);
        p.Emitting = true;
        After(1.6, p.QueueFree);
    }

    /// Dark MD smoke puffs lifting off a point (normal blending, so it darkens).
    void Smoke(Vector3 at, int n, Color c)
    {
        for (int i = 0; i < n; i++)
        {
            var m = new StandardMaterial3D { ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded, Transparency = BaseMaterial3D.TransparencyEnum.Alpha, AlbedoTexture = VfxTex("md/smoke"), AlbedoColor = new Color(c, 0), BillboardMode = BaseMaterial3D.BillboardModeEnum.Enabled, DisableReceiveShadows = true };
            float s = (float)GD.RandRange(0.6, 0.9);
            var q = new MeshInstance3D { Mesh = new QuadMesh { Size = new Vector2(s, s) }, MaterialOverride = m, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off };
            world.AddChild(q);
            var from = at + new Vector3((float)GD.RandRange(-0.3, 0.3), 0.15f, (float)GD.RandRange(-0.3, 0.3));
            q.Position = from;
            double d = GD.RandRange(0.8, 1.1);
            var tw = CreateTween().SetParallel();
            tw.TweenProperty(q, "position", from + new Vector3((float)GD.RandRange(-0.3, 0.3), 0.7f, 0), d).SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.Out);
            tw.TweenProperty(q, "scale", Vector3.One * 1.6f, d);
            tw.TweenMethod(Callable.From<float>(k => m.AlbedoColor = new Color(c, c.A * Env(k, 0.15f, 0.6f))), 0f, 1f, d);
            tw.Chain().TweenCallback(Callable.From(q.QueueFree));
        }
    }

    /// A copy of the card at its current pose, for effects that play where it stood while the real card moves on unseen.
    Node3D Ghost(View v, out StandardMaterial3D face)
    {
        var g = new Node3D(); world.AddChild(g);
        g.GlobalTransform = v.Root.GlobalTransform;
        face = (StandardMaterial3D)v.Face.Duplicate();
        g.AddChild(new MeshInstance3D { Mesh = new QuadMesh { Size = new Vector2(CW, CH) }, RotationDegrees = new Vector3(-90, 0, 0), Position = new Vector3(0, 0.0045f, 0), MaterialOverride = face });
        g.AddChild(new MeshInstance3D { Mesh = new QuadMesh { Size = new Vector2(CW, CH) }, RotationDegrees = new Vector3(90, 0, 0), Position = new Vector3(0, -0.0045f, 0), MaterialOverride = v.BackMat });
        return g;
    }

    /// Hide the real card for a moment (its own move to the Graveyard plays unseen behind an effect).
    void HideFor(View v, double s)
    {
        v.Root.Visible = false; v.Stat.Visible = false;
        After(s, () => { v.Root.Visible = v.S.Loc != Loc.None; if (v.Root.Visible) UpdateStat(v, false); });
    }

    // ---------------------------------------------------------------- 1. destroyed by battle: MD crack, orange

    /// An orange MD crack spreads over the card from where it was hit, the card heats up, then breaks into
    /// shards with embers. Plays on a copy; the real card goes to the Graveyard hidden.
    async void CrackBreak(View v, Vector3 from)
    {
        var g = Ghost(v, out var face);
        Cue("crack");
        HideFor(v, 1.3);
        var cm = CrackOn(g, from, FxEmber * 1.3f);
        var tw = CreateTween().SetParallel();
        // Heat: the card glows orange from within and trembles, the crack brightening, for about half a second.
        face.ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded;
        tw.TweenProperty(face, "albedo_color", new Color(1f, 0.62f, 0.38f), 0.45).SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.In);
        tw.TweenProperty(face, "emission", new Color(0.9f, 0.32f, 0.05f), 0.45).SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.In);
        tw.TweenProperty(cm, "albedo_color", new Color(1f, 0.8f, 0.45f) * 2.2f, 0.45);
        var home = g.Position;
        var shake = CreateTween();
        for (int i = 0; i < 8; i++) shake.TweenProperty(g, "position", home + new Vector3(i % 2 == 0 ? 0.012f : -0.012f, 0.01f * i, 0), 0.06);
        Glint("md/glare", FxEmber, g.GlobalPosition + Vector3.Up * 0.1f, 200, 0.6f, 1f, 0.55, alpha: 0.55f);
        Shake(0.03f);
        await Wait(0.55);
        Cue("shatter");
        Shatter3D(g, face, from);
        var at = g.GlobalPosition;
        Glint("md/glare", FxEmber, at + Vector3.Up * 0.2f, 260, 0.4f, 1f, 0.35, alpha: 0.7f);
        Burst(at, FxEmber, 22, 1.4f);
        Flash(at, FxEmber, 5);
        Shake(0.07f);
    }

    /// The MD radial crack spreading over a card (or its copy) from the point nearest `from`; returns its material.
    StandardMaterial3D CrackOn(Node3D card, Vector3 from, Color tint)
    {
        var local = card.ToLocal(from);
        float u = Mathf.Clamp(local.X / CW + 0.5f, 0.2f, 0.8f), w = Mathf.Clamp(local.Z / CH + 0.5f, 0.15f, 0.85f);
        var cm = Additive(VfxTex("md/crack"), tint);
        cm.TextureRepeat = false;
        card.AddChild(new MeshInstance3D { Mesh = new QuadMesh { Size = new Vector2(CW, CH) }, RotationDegrees = new Vector3(-90, 0, 0), Position = new Vector3(0, 0.006f, 0), MaterialOverride = cm, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off });
        // The crack texture spans ~1.7 card widths; it grows out of the hit point.
        CreateTween().TweenMethod(Callable.From<float>(k =>
        {
            float sx = 1 / (1.7f * k), sy = 1 / (1.16f * k);
            cm.Uv1Scale = new Vector3(sx, sy, 1); cm.Uv1Offset = new Vector3(0.5f - u * sx, 0.5f - w * sy, 0);
        }), 0.15f, 1f, 0.22).SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
        return cm;
    }

    /// Break a card copy into jittered triangles that fly away from `from`, tumble and fade.
    void Shatter3D(Node3D g, StandardMaterial3D face, Vector3 from)
    {
        const int cols = 7, rows = 9;
        var rng = new RandomNumberGenerator();
        var pts = new Vector2[rows + 1, cols + 1];
        for (int r = 0; r <= rows; r++) for (int c = 0; c <= cols; c++)
            pts[r, c] = new Vector2(c / (float)cols + (c % cols == 0 ? 0 : rng.RandfRange(-0.04f, 0.04f)), r / (float)rows + (r % rows == 0 ? 0 : rng.RandfRange(-0.035f, 0.035f)));
        var mat = (StandardMaterial3D)face.Duplicate();
        mat.Transparency = BaseMaterial3D.TransparencyEnum.Alpha; mat.CullMode = BaseMaterial3D.CullModeEnum.Disabled;
        var away = g.GlobalPosition - from; away.Y = 0; away = away.LengthSquared() < 0.01f ? Vector3.Forward : away.Normalized();
        var basis = g.GlobalTransform.Basis;
        foreach (var n in g.GetChildren()) ((Node3D)n).Visible = false;
        for (int r = 0; r < rows; r++) for (int c = 0; c < cols; c++)
        {
            var q = new[] { pts[r, c], pts[r, c + 1], pts[r + 1, c + 1], pts[r + 1, c] };
            var tris = rng.Randf() < 0.5f ? new[] { new[] { q[0], q[1], q[2] }, new[] { q[0], q[2], q[3] } } : new[] { new[] { q[0], q[1], q[3] }, new[] { q[1], q[2], q[3] } };
            foreach (var t in tris)
            {
                var cen = (t[0] + t[1] + t[2]) / 3;
                Vector3 L(Vector2 uv) => new((uv.X - 0.5f) * CW, 0.006f, (uv.Y - 0.5f) * CH);
                var st = new SurfaceTool(); st.Begin(Mesh.PrimitiveType.Triangles);
                foreach (var uv in t) { st.SetNormal(Vector3.Up); st.SetUV(uv); st.AddVertex(L(uv) - L(cen)); }
                var piece = new MeshInstance3D { Mesh = st.Commit(), MaterialOverride = mat, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off };
                world.AddChild(piece);
                var start = g.GlobalTransform * L(cen);
                piece.GlobalTransform = new Transform3D(basis, start);
                var outDir = (basis * L(cen)).Normalized() * 0.6f + away;
                var peak = start + outDir * rng.RandfRange(0.2f, 0.4f) + Vector3.Up * rng.RandfRange(0.25f, 0.6f);
                var end = start + outDir * rng.RandfRange(0.45f, 0.85f) + Vector3.Up * 0.02f;
                var spin = new Vector3(rng.RandfRange(-6, 6), rng.RandfRange(-6, 6), rng.RandfRange(-6, 6));
                double d = rng.RandfRange(0.7f, 0.95f);
                var tw = CreateTween().SetParallel();
                tw.TweenMethod(Callable.From<float>(k =>
                {
                    var p = start.Lerp(peak, Mathf.Min(1, k * 2)).Lerp(end, Mathf.Max(0, k * 2 - 1));
                    piece.GlobalTransform = new Transform3D(basis.Rotated(spin.Normalized(), spin.Length() * k), p);
                }), 0f, 1f, d);
                tw.TweenMethod(Callable.From<float>(k => { mat.AlbedoColor = new Color(mat.AlbedoColor, 1 - k * k); mat.Emission = new Color(0.9f, 0.32f, 0.05f) * (1 - k); }), 0f, 1f, d);
                tw.Chain().TweenCallback(Callable.From(piece.QueueFree));
            }
        }
        After(1.1, g.QueueFree);
    }

    // ---------------------------------------------------------------- 2. destroyed by card effect: burn to embers

    void EffectBurn(View v, string cue = "burn")
    {
        Cue(cue);
        var at = v.Root.GlobalPosition;
        Glint("md/glare", new Color(1f, 0.69f, 0.44f), at + Vector3.Up * 0.2f, 420, 0.4f, 1f, 0.42, alpha: 0.8f);
        After(0.35, () => Smoke(at, 5, new Color(0.29f, 0.25f, 0.28f, 0.42f)));
        Disintegrate(v);
    }

    // ---------------------------------------------------------------- 3 / 4. placed in attack / defense

    /// A card placed on the field: the light swords clash over an attacker, the light shield pops over a defender.
    void PlacementFx(View v, bool def)
    {
        if (def) ShieldPop(v); else SwordClash(v);
    }

    const string SweepShader = @"shader_type canvas_item;
render_mode blend_add;
uniform float t = 0.0;
void fragment() {
  vec4 c = texture(TEXTURE, UV);
  float band = exp(-pow((UV.y - (t * 1.6 - 0.3)) * 7.0, 2.0));
  COLOR = vec4(c.rgb + band * c.a * 0.8, c.a) * COLOR;
}";

    /// One light sprite (sword or shield) on the HUD with a light sweep running down it.
    TextureRect LightSprite(Control parent, string tex, Vector2 size, Vector2 pivot, out ShaderMaterial sweep)
    {
        sweep = Shade(SweepShader, ("t", 0f));
        var r = new TextureRect { Texture = VfxTex(tex), ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.Scale, Size = size, PivotOffset = pivot, Position = -pivot, Material = sweep, MouseFilter = MouseFilterEnum.Ignore };
        parent.AddChild(r);
        return r;
    }

    /// Two gold light-glass swords swing in and clash over the card (MD flash and star where they cross).
    async void SwordClash(View v)
    {
        var at = v.Root.GlobalPosition + Vector3.Up * 0.15f;
        Cue("swords_swing");
        var root = new Control { Position = cam.UnprojectPosition(at), MouseFilter = MouseFilterEnum.Ignore, Modulate = new Color(1, 1, 1, 0) };
        hud.AddChild(root);
        // sword.png: 214x500, blade L = 420 px from the tip (y 40); the swords cross 38% down the blade.
        float k = 150f / 420f;
        var swords = new List<(TextureRect R, ShaderMaterial M, float S)>();
        foreach (float s in new[] { 1f, -1f })
        {
            var r = LightSprite(root, "sword", new Vector2(214, 500) * k, new Vector2(107, 40 + 0.38f * 420) * k, out var m);
            r.Rotation = s * 1.52f;
            swords.Add((r, m, s));
        }
        var tw = CreateTween().SetParallel();
        tw.TweenProperty(root, "modulate:a", 1f, 0.08);
        foreach (var (r, m, s) in swords)
        {
            tw.TweenProperty(r, "rotation", s * 0.62f, 0.23).SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.In);
            tw.TweenMethod(Callable.From<float>(t => m.SetShaderParameter("t", t)), 0f, 1f, 1.05);
        }
        await Wait(0.23);
        Cue("swords_clash");
        Glint("md/flash", Colors.White, at, 240, 0.3f, 1.1f, 0.4);
        Glint("md/star4", FxWarm, at, 170, 0.2f, 1f, 0.36, spin: 0.4f);
        Burst(at, FxGold, 10, 1.4f);
        Shake(0.035f);
        await Wait(0.55);
        var end = CreateTween().SetParallel();
        end.TweenProperty(root, "modulate:a", 0f, 0.28);
        end.TweenProperty(root, "position:y", root.Position.Y - 16, 0.28);
        end.Chain().TweenCallback(Callable.From(root.QueueFree));
    }

    /// The blue light-glass shield pops over the card with an MD flash, a light sweep and a corner glint.
    async void ShieldPop(View v)
    {
        var at = v.Root.GlobalPosition + Vector3.Up * 0.12f;
        Cue("shield");
        var root = new Control { Position = cam.UnprojectPosition(at), MouseFilter = MouseFilterEnum.Ignore, Modulate = new Color(1, 1, 1, 0), Scale = Vector2.One * 1.5f };
        hud.AddChild(root);
        float k = 120f / 483f;   // shield.png 399x483: about four-fifths of a card's height
        LightSprite(root, "shield", new Vector2(399, 483) * k, new Vector2(399, 483) * k / 2, out var m);
        var tw = CreateTween().SetParallel();
        tw.TweenProperty(root, "modulate:a", 1f, 0.12);
        tw.TweenProperty(root, "scale", Vector2.One, 0.15).SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
        tw.TweenMethod(Callable.From<float>(t => m.SetShaderParameter("t", t)), 0f, 1f, 1.0);
        await Wait(0.12);
        Glint("md/flash", new Color(0.75f, 0.89f, 1f), at, 210, 0.3f, 1f, 0.36);
        await Wait(0.33);
        Glint("md/star4", new Color(0.9f, 0.96f, 1f), at + new Vector3(0.18f, 0.2f, -0.18f), 110, 0.2f, 1f, 0.36);
        await Wait(0.3);
        var end = CreateTween().SetParallel();
        end.TweenProperty(root, "modulate:a", 0f, 0.28);
        end.TweenProperty(root, "position:y", root.Position.Y - 14, 0.28);
        end.Chain().TweenCallback(Callable.From(root.QueueFree));
    }

    // ---------------------------------------------------------------- 5 / 6. chain links and negated links

    /// The open chain, bottom to top. Each link shows a small gold number on its card's corner (the Badge)
    /// until it resolves; there is no chain drawn between them.
    readonly List<View> chainFx = new();

    /// An activation joins the chain: a gold card frame pulses (the number is the Badge).
    void ChainLinkFx(View v)
    {
        FrameBurst(v, FxGold, 0.95f, 1.25f, 0.48);
        chainFx.Add(v);
        Cue("chain_link", chainFx.Count - 1);
    }

    /// The top link resolves: a glint where its number was, and the number goes.
    void ChainResolve()
    {
        if (chainFx.Count == 0) return;
        var v = chainFx[^1];
        chainFx.RemoveAt(chainFx.Count - 1);
        Cue(chainFx.Count == 0 ? "chain_end" : "chain_resolve", chainFx.Count);
        Glint("md/star4", FxGold, v.Badge.GlobalPosition, 150, 0.2f, 1f, 0.34);
        v.Badge.Visible = false;
    }

    void ChainClear() => chainFx.Clear();

    /// A negated link: the card jitters and flickers like a broken signal and settles dimmed; its number goes.
    async void NegateGlitch(View v)
    {
        Cue("negate");
        v.Badge.Visible = false;
        await Glitch(v, 0.6);
    }

    /// The card jitters and flickers like a broken signal, then settles dimmed (negated).
    async Task Glitch(View v, double dur)
    {
        var home = v.Root.Position;
        for (double t = 0; t < dur; t += 0.04)
        {
            v.Root.Position = home + new Vector3((float)GD.RandRange(-0.07, 0.07), 0, (float)GD.RandRange(-0.03, 0.03));
            v.Root.Visible = GD.Randf() > 0.2f;
            v.Face.AlbedoColor = GD.Randf() > 0.5f ? new Color(1f, 0.5f, 0.5f) : new Color(0.5f, 0.9f, 1f);
            await Wait(0.04);
        }
        v.Root.Position = home; v.Root.Visible = true; v.Face.AlbedoColor = new Color(0.4f, 0.4f, 0.46f);
    }

    // ---------------------------------------------------------------- 7. attack blocked: hit-stop + shield

    /// The defender holds: everything freezes for an instant, the camera punches in and the light shield takes the hit.
    void BlockHit(View t, Vector3 at)
    {
        Cue("block");
        HitStop(0.16);
        CamPunch(at, 0.4f);
        ShieldPop(t);
        FrameBurst(t, FxIce, 1f, 1.3f, 0.45);
        Shake(0.1f);
    }

    /// Everything freezes for a moment of real time.
    void HitStop(double s)
    {
        double was = Godot.Engine.TimeScale;
        Godot.Engine.TimeScale = 0.03;
        GetTree().CreateTimer(s, true, false, true).Timeout += () => Godot.Engine.TimeScale = was;
    }

    /// The camera pushes toward a point for an instant and settles back.
    void CamPunch(Vector3 at, float amount = 0.35f, double dur = 0.28)
    {
        var dir = (at - camHome).Normalized();
        var tw = CreateTween();
        tw.TweenProperty(cam, "position", camHome + dir * amount, dur * 0.3).SetTrans(Tween.TransitionType.Expo).SetEase(Tween.EaseType.Out);
        tw.TweenProperty(cam, "position", camHome, dur * 0.7).SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
    }

    /// Sparks spraying off a contact point for a while (clashes, grinding).
    void GrindSparks(Vector3 at, double dur, int amount = 60, float speed = 1f)
    {
        var grad = new Gradient { Offsets = new[] { 0f, 0.4f, 1f }, Colors = new[] { Colors.White, new Color(1f, 0.88f, 0.55f), new Color(0.75f, 0.89f, 1f, 0) } };
        var p = new GpuParticles3D
        {
            Position = at, Amount = amount, Lifetime = 0.45, OneShot = false, Explosiveness = 0,
            ProcessMaterial = new ParticleProcessMaterial
            {
                Direction = new Vector3(0, 1, 0), Spread = 70, InitialVelocityMin = 1.5f * speed, InitialVelocityMax = 3.2f * speed, Gravity = new Vector3(0, -7f, 0),
                ScaleMin = 0.3f, ScaleMax = 0.7f, ColorRamp = new GradientTexture1D { Gradient = grad },
            },
            DrawPass1 = new QuadMesh { Size = new Vector2(0.07f, 0.07f), Material = Glowing(3) },
        };
        world.AddChild(p);
        p.Emitting = true;
        After(dur, () => p.Emitting = false);
        After(dur + 0.6, p.QueueFree);
    }

    // ---------------------------------------------------------------- 8. fusion: souls swirl, rebuilt on the field

    /// A fusion material dissolves into light and its motes swirl round the summon spot and sink into it.
    void SwirlOut(View v, Vector3 centre)
    {
        var from = v.Root.GlobalPosition;
        Cue("swirl_out");
        Disintegrate(v, FxWarm);
        for (int i = 0; i < 14; i++)
        {
            var a = from + new Vector3((float)GD.RandRange(-0.25, 0.25), 0.12f, (float)GD.RandRange(-0.3, 0.3));
            var col = i % 3 == 0 ? Colors.White : FxGold;
            After(0.25 + i * 0.035, () => SwirlMote(a, centre, col, 1.2, 1.0f, 1.2f, (float)GD.RandRange(0.09, 0.15)));
        }
    }

    /// The card builds itself out of embers where it rests (the burn, run backwards); the real card shows once it is whole.
    async Task Rebuild(View v, Color edge, double dur = 0.75, string cue = "rebuild")
    {
        HideFor(v, dur);
        Cue(cue);
        var g = new Node3D(); world.AddChild(g); g.GlobalTransform = Target(v, v.S);
        var m = Shade(DissolveShader, ("tex", v.S.Up || v.S.Loc == Loc.Grave ? CardArt.Small(v.Card.Data) : backs[Side(v.Card.Owner)]), ("edge", edge), ("t", 1f));
        g.AddChild(new MeshInstance3D { Mesh = new QuadMesh { Size = new Vector2(CW, CH) }, RotationDegrees = new Vector3(-90, 0, 0), Position = new Vector3(0, 0.0045f, 0), MaterialOverride = m, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off });
        Burst(g.GlobalPosition, edge, 18, 1.2f);
        CreateTween().TweenMethod(Callable.From<float>(t => m.SetShaderParameter("t", t)), 1f, 0f, dur).SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.Out);
        await Wait(dur);
        g.QueueFree();
    }

    // ---------------------------------------------------------------- 11. attack declared: aim line

    const string AimShader = @"shader_type spatial;
render_mode unshaded, blend_add, depth_draw_never, cull_disabled, shadows_disabled;
uniform sampler2D tex : source_color, repeat_enable;
uniform vec4 col : source_color;
uniform float len = 1.0;
uniform float a = 1.0;
void fragment() {
  vec4 c = texture(tex, vec2(TIME * 1.6 - UV.x * len * 1.1, UV.y));   // chevrons point at the target and flow to it
  float ends = smoothstep(0.0, 0.06, UV.x) * smoothstep(1.0, 0.9, UV.x);
  ALBEDO = col.rgb * c.a * 1.8 * ends * a;
}";

    Node3D? aimFx;

    /// The attacker's aim: gold chevrons flowing along a lane from attacker to target over a soft ribbon.
    /// Stays until the attack lands or is stopped (AimClear).
    void AimShow(View a, Vector3 to)
    {
        AimClear();
        Cue("aim");
        var root = new Node3D(); world.AddChild(root); aimFx = root;
        var from = a.Root.GlobalPosition; from.Y = to.Y = 0.01f;   // just under the card faces: it runs on the table, never over a card's art
        var d = to - from; float len = d.Length();
        var basis = new Basis(Vector3.Up, Mathf.Atan2(-d.Z, d.X)) * new Basis(Vector3.Right, -Mathf.Pi / 2);
        var glow = Additive(VfxTex("md/streak"), new Color(FxGold, 0));
        var lane = Shade(AimShader, ("tex", VfxTex("md/chevron")), ("col", FxGold), ("len", len), ("a", 0f));
        foreach (var (mat, w) in new (Material, float)[] { (glow, 0.32f), (lane, 0.2f) })
        {
            var q = new MeshInstance3D { Mesh = new QuadMesh { Size = new Vector2(len, w) }, MaterialOverride = mat, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off };
            root.AddChild(q);
            q.GlobalTransform = new Transform3D(basis, from + d / 2);
        }
        var tw = CreateTween().SetParallel();
        tw.TweenProperty(glow, "albedo_color:a", 0.55f, 0.25);
        tw.TweenMethod(Callable.From<float>(k => lane.SetShaderParameter("a", k)), 0f, 1f, 0.25);
    }

    void AimClear()
    {
        if (aimFx == null) return;
        var r = aimFx; aimFx = null;
        if (!IsInstanceValid(r)) return;
        var tw = CreateTween();
        tw.TweenProperty(r, "scale", new Vector3(1, 0.01f, 1), 0.15);
        tw.TweenCallback(Callable.From(r.QueueFree));
    }

    // ---------------------------------------------------------------- 9. tribute: souls to the summon

    /// A tribute dissolves into light where it stood and its soul arcs over to the summon spot.
    void SoulsOut(View v, Vector3 to)
    {
        var from = v.Root.GlobalPosition;
        Cue("soul_out");
        Disintegrate(v, new Color(1f, 0.96f, 0.82f));
        After(0.25, () => SoulStream(from, to, 14, FxGold));
        After(1.15, () => Cue("souls_arrive"));
    }

    /// Soul motes flowing from a to b: the tribute look, used wherever light travels between two places.
    void SoulStream(Vector3 a, Vector3 b, int n, Color c, double dur = 0.9, float arc = 0.9f, double gap = 0.03)
    {
        for (int i = 0; i < n; i++)
        {
            var from = a + new Vector3((float)GD.RandRange(-0.25, 0.25), 0.15f, (float)GD.RandRange(-0.3, 0.3));
            var col = new[] { Colors.White, FxWarm, c }[i % 3];
            After(i * gap, () => Mote(from, b + Vector3.Up * 0.2f, col, dur, (float)GD.RandRange(0.09, 0.15), arc));
        }
    }

    /// A mote that flies out to a ring round `centre`, circles it while closing in, and sinks into it (the fusion swirl).
    void SwirlMote(Vector3 a, Vector3 centre, Color c, double dur, float radius, float turns, float size)
    {
        var m = Additive(SoftDot, c, billboard: true);
        var q = new MeshInstance3D { Mesh = new QuadMesh { Size = new Vector2(size, size) }, MaterialOverride = m, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off };
        world.AddChild(q);
        float a0 = Mathf.Atan2(a.Z - centre.Z, a.X - centre.X);
        var ring0 = centre + new Vector3(Mathf.Cos(a0) * radius, 0.4f, Mathf.Sin(a0) * radius);
        var tw = CreateTween();
        tw.TweenMethod(Callable.From<float>(t =>
        {
            if (t < 0.3f) { float e = t / 0.3f; e = e * e * (3 - 2 * e); q.Position = a.Lerp(ring0, e) + Vector3.Up * Mathf.Sin(Mathf.Pi * e) * 0.3f; }
            else
            {
                float k = (t - 0.3f) / 0.7f, ang = a0 + turns * Mathf.Tau * k, r = radius * Mathf.Pow(1 - k, 1.3f);
                q.Position = centre + new Vector3(Mathf.Cos(ang) * r, 0.4f * (1 - k) + 0.08f, Mathf.Sin(ang) * r);
            }
            m.AlbedoColor = new Color(c, Mathf.Min(1, (1 - t) * 6));
        }), 0f, 1f, dur);
        tw.TweenCallback(Callable.From(q.QueueFree));
    }

    // ---------------------------------------------------------------- 10. effect trigger: gold rays

    /// The card lifts a little, a gold MD frame glows round it, soft gold rays turn behind it, a four-point glint
    /// pops and the light sweep runs down the art.
    int lastRays = -1;
    ulong lastRaysAt;

    void GoldRays(View v, Color? tint = null)
    {
        lastRays = v.Card.Uid; lastRaysAt = Time.GetTicksMsec();
        var c = tint ?? FxGold;   // a Spell or Trap could use its own colour
        Cue(c == FxGreen ? "rays_spell" : c == FxPink ? "rays_trap" : "rays");
        // Rays and a star flash over the card, its frame glows and its face shines; no lift (user 2026-10-29).
        if (!v.Root.Visible && v.S.Loc == Loc.Grave) { v.Root.Visible = true; Place(v, v.S); }   // a Graveyard effect (Cockroach Knight) glows on the pile
        var at = Target(v, v.S).Origin;
        FrameBurst(v, c, 0.95f, 1.18f, 0.8, alpha: 0.8f);
        Glint("md/rays", c.Lerp(Colors.White, 0.35f), at + Vector3.Up * 0.15f, 520, 0.6f, 1.05f, 0.95, spin: 0.8f, alpha: 0.6f);
        After(0.14, () => Glint("md/star4", c.Lerp(Colors.White, 0.7f), at + Vector3.Up * 0.15f, 290, 0.2f, 1f, 0.42));
        Shine(v);
    }

    // ---------------------------------------------------------------- 13. both destroyed: meet in the middle

    /// Equal attackers charge each other and meet; the cracks follow as they leave the field.
    void MeetInMiddle(View a, View t, int side, Action<Vector3>? onMeet = null)
    {
        Lunge(t, a, 1 - side, onHit: _ => { }, quiet: true);
        Lunge(a, t, side, windup: "windup_meet", onHit: h => { Cue("meet"); Glint("md/burst", FxEmber, h, 360, 0.3f, 1.1f, 0.4); Shake(0.1f); onMeet?.Invoke(h); });
    }

    // ---------------------------------------------------------------- 14. direct attack: shockwave on their side

    void DirectHit(int attackerSide)
    {
        var p = DirectPoint(attackerSide);
        Cue("direct_impact");
        Decal("md/spikering", FxEmber, p, new Vector2(4.5f, 4.5f), 0.2f, 1.3f, 0.6);
        Decal("md/softring", FxWarm, p, new Vector2(6f, 6f), 0.2f, 1.2f, 0.7);
        Smoke(p, 6, new Color(0.5f, 0.45f, 0.45f, 0.4f));
        Shake(0.15f);
    }

    // ---------------------------------------------------------------- 15. LP loss: red screen edge

    /// The screen's edges pulse a colour (taking damage).
    void Vignette(Color c, double dur)
    {
        var m = Shade(@"shader_type canvas_item;
uniform vec4 col : source_color;
uniform float a = 0.0;
void fragment() { float d = length((UV - 0.5) * vec2(1.0, 0.8)); COLOR = vec4(col.rgb, a * smoothstep(0.3, 0.62, d)); }", ("col", c), ("a", 0f));
        var r = new ColorRect { Size = new Vector2(W, H), Material = m, MouseFilter = MouseFilterEnum.Ignore };
        hud.AddChild(r);
        var tw = CreateTween();
        tw.TweenMethod(Callable.From<float>(k => m.SetShaderParameter("a", Mathf.Sin(Mathf.Pi * k) * 0.85f)), 0f, 1f, dur);
        tw.TweenCallback(Callable.From(r.QueueFree));
    }

    // ---------------------------------------------------------------- 16. LP hits 0: the loser's field shatters

    async Task FieldShatters(int side)
    {
        foreach (var v in views.Values.Where(v => v.S.Loc == Loc.Monster && v.S.Side == side).ToList())
        {
            CrackBreak(v, v.Root.GlobalPosition + (side == 1 ? Vector3.Back : Vector3.Forward));
            v.S = v.S with { Loc = Loc.None };   // gone for good: the duel is over, nothing moves it to the Graveyard
            await Wait(0.12);
        }
        await Wait(0.6);
    }

    // ---------------------------------------------------------------- 18. ritual summon: circle and pillars

    /// Drop a card onto its zone from above, like a summon.
    void Drop(View v, St s) { Cue("drop"); Place(v, s); v.Root.Position += new Vector3(0, 1.6f, 0.8f); Move(v, s, 0, 0.5f, 0.3f); }

    void RitualCircle(Vector3 c)
    {
        Cue("ritual_circle");
        After(0.15, () => Cue("ritual_pillars"));
        Decal("md/magic", FxIce, c, new Vector2(3.2f, 3.2f), 0.5f, 1.05f, 1.8, spin: 2.4f);
        foreach (var d in new[] { new Vector3(-1, 0, -1), new Vector3(1, 0, -1), new Vector3(-1, 0, 1), new Vector3(1, 0, 1) })
        {
            var p = c + d + Vector3.Up * 0.5f;
            After(0.15, () => Glint("md/longstar", FxIce, p, 420, 0.2f, 1f, 0.8));
        }
    }

    // ---------------------------------------------------------------- 20 / 31 / 32. zones light up

    /// A Monster Zone closes (Ground Collapse): the crack spreads and the zone darkens, then on the quake's peak in the
    /// sound (`hit` s, zone_close) shards fly, the table shakes, and smoke rises with the rumble.
    void ZoneCollapse(MeshInstance3D bm, double hit = 0.3)
    {
        var at = bm.GlobalPosition;
        var dark = (StandardMaterial3D)bm.MaterialOverride;
        var crack = bm.GetNode<Node3D>("crack");
        Cue("zone_close");
        crack.Scale = Vector3.One * 0.25f;
        dark.AlbedoColor = new Color(dark.AlbedoColor, 0);
        var tw = CreateTween().SetParallel();
        tw.TweenProperty(crack, "scale", Vector3.One, hit).SetTrans(Tween.TransitionType.Expo).SetEase(Tween.EaseType.Out);
        tw.TweenProperty(dark, "albedo_color:a", 0.72f, hit);
        After(hit, () => { Shards(at, FxRed, 16); Flash(at, FxRed, 4); Shake(0.07f); ZoneLight(at, FxRed, 0.6); });
        After(hit + 0.12, () => Smoke(at, 4, new Color(0.25f, 0.2f, 0.2f, 0.5f)));
    }

    void ZoneLight(Vector3 at, Color c, double dur = 0.8) => Decal("md/zonesq", c, at, new Vector2(CW * 1.7f, CH * 1.45f), 1f, 1.04f, dur);

    /// A wave of lit zones across one side, left to right (Battle Phase: the monster row; turn start: both rows).
    void ZoneWave(int side, Color c, bool spells, double dur = 0.8, string cue = "zone_wave")
    {
        Cue(cue);
        for (int i = 0; i < 5; i++)
            foreach (var l in spells ? new[] { Loc.Monster, Loc.Spell } : new[] { Loc.Monster })
            {
                var p = Grid3(new St(l, i, 0, false, false, side, 0, 0));
                After(i * 0.07, () => ZoneLight(p, c, dur));
            }
    }

    // ---------------------------------------------------------------- 25. discard: embers drift to the GY

    /// The card burns away in the hand and its embers drift to the Graveyard (it rebuilds there: Rebuild).
    /// Call before the card moves; `gy` is where it will rest.
    void DiscardFx(View v, Vector3 gy)
    {
        var from = v.Root.GlobalPosition;
        EffectBurn(v, "discard_burn");
        After(0.3, () => Cue("ember_drift"));
        for (int i = 0; i < 10; i++)
        {
            var a = from + new Vector3((float)GD.RandRange(-0.2, 0.2), 0, 0);
            var c = i % 2 == 0 ? FxEmber : new Color(1f, 0.85f, 0.5f);
            After(0.3 + i * 0.05, () => Mote(a, gy + Vector3.Up * 0.1f, c, 0.7, 0.08f, 0.5f));
        }
    }

    // ---------------------------------------------------------------- 26. ATK/DEF change: glow and count

    /// The monster glows (gold up, red down) and its stat plate counts from the old values to the new ones.
    void StatChange(View v, St was)
    {
        var s = v.S;
        bool up = s.Atk - was.Atk + s.Df - was.Df >= 0;
        Cue(up ? "stat_up" : "stat_down");
        var tw = CreateTween();
        tw.TweenProperty(v.Face, "emission", up ? new Color(0.45f, 0.35f, 0.08f) : new Color(0.45f, 0.1f, 0.06f), 0.2);
        tw.TweenProperty(v.Face, "emission", Colors.Black, 0.6);
        if (s.Atk != was.Atk) CreateTween().TweenMethod(Callable.From<int>(x => v.StatAtk.Text = x.ToString()), was.Atk, s.Atk, 0.6).SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
        if (s.Df != was.Df) CreateTween().TweenMethod(Callable.From<int>(x => v.StatDef.Text = x.ToString()), was.Df, s.Df, 0.6).SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
    }

    // ---------------------------------------------------------------- 27. equip: gold thread

    /// A flat ribbon of light on the table from a to b, drawn out from a.
    MeshInstance3D LightLine(Vector3 a, Vector3 b, Color c, double grow = 0.25)
    {
        a.Y = b.Y = 0.2f;
        var d = b - a;
        var m = Additive(VfxTex("md/streak"), new Color(c * 1.4f, 0)); m.NoDepthTest = true;
        var q = new MeshInstance3D { Mesh = new QuadMesh { Size = new Vector2(d.Length(), 0.12f) }, MaterialOverride = m, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off };
        world.AddChild(q);
        q.Rotation = new Vector3(-Mathf.Pi / 2, Mathf.Atan2(-d.Z, d.X), 0);
        var tw = q.CreateTween().SetParallel();   // dies with the line: a hover thread freed mid-grow threw every frame (playtest log)
        tw.TweenProperty(m, "albedo_color:a", 1f, grow);
        tw.TweenMethod(Callable.From<float>(k => { q.Scale = new Vector3(k, 1, 1); q.Position = a + d * (k / 2); }), 0.01f, 1f, grow).SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
        return q;
    }

    /// The equip draws a gold thread to its monster, the monster's frame flares, the thread fades.
    /// Hovering either card later shows the same thread (ShowLinks).
    async void EquipThread(View e, View m)
    {
        Cue("thread");
        var l = LightLine(Target(e, e.S).Origin, Target(m, m.S).Origin, FxGold, 0.3);
        await Wait(0.4);
        Cue("thread_flare");
        FrameBurst(m, FxGold, 0.95f, 1.15f, 0.45);
        await Wait(0.5);
        var tw = CreateTween();
        tw.TweenProperty(l.MaterialOverride, "albedo_color:a", 0f, 0.3);
        tw.TweenCallback(Callable.From(l.QueueFree));
    }

    // ---------------------------------------------------------------- 30. LP gain: rising glints

    Vector2 PanelAt(int side) => profile[side].Position + profile[side].Size / 2;

    async void LpGlints(int side)
    {
        Cue("glints");
        for (int k = 0; k < 8; k++)
        {
            var p = PanelAt(side) + new Vector2((float)GD.RandRange(-150, 150), 20);
            var r = new TextureRect { Texture = VfxTex("md/star4"), ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.Scale, Size = new Vector2(60, 60), Position = p, Material = AddMat, MouseFilter = MouseFilterEnum.Ignore, SelfModulate = FxGreen };
            hud.AddChild(r);
            var tw = CreateTween().SetParallel();
            tw.TweenProperty(r, "position:y", p.Y - 160, 0.9);
            tw.TweenProperty(r, "self_modulate:a", 0f, 0.9);
            tw.Chain().TweenCallback(Callable.From(r.QueueFree));
            await Wait(0.06);
        }
    }

    // ---------------------------------------------------------------- 35. activation cut-in: side portrait

    /// The card's art slides in at the right edge below the phase button (nothing important sits there), small.
    void Portrait(Card c)
    {
        float w = 170, h = w * 1.458f;
        var p = new Panel { Size = new Vector2(w + 12, h + 12), Position = new Vector2(W + 20, H / 2 + 100), MouseFilter = MouseFilterEnum.Ignore };
        p.AddThemeStyleboxOverride("panel", Pill(new Color(0.03f, 0.04f, 0.07f, 0.95f), RivalPanels.Gold, 12, 0, new Color(RivalPanels.Gold, 0.45f), 16, 2));
        p.AddChild(new TextureRect { Texture = CardArt.Large(c.Data), ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.Scale, Size = new Vector2(w, h), Position = new Vector2(6, 6), MouseFilter = MouseFilterEnum.Ignore });
        hud.AddChild(p);
        Cue("cutin_in");
        After(1.26, () => Cue("cutin_out"));
        var tw = CreateTween();
        tw.TweenProperty(p, "position:x", W - w - 36, 0.26).SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
        tw.TweenInterval(1.0);
        tw.TweenProperty(p, "position:x", W + 20, 0.22).SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.In);
        tw.TweenCallback(Callable.From(p.QueueFree));
    }
}
