using Godot;
using PoC.Engine;

namespace PoC.Game.UI;

/// Card effects: destroyed by an effect = the card burns away into embers; a triggered effect announces itself
/// before its results play. The VFX Lab picks are in DuelField.Picked.cs.
public partial class DuelField
{
    // ---------------------------------------------------------------- triggered effects

    /// Duel.OnTriggered: put the "effect activated" step in front of the effect's own steps, if they haven't played yet.
    void Triggered(Card c, int seqBefore, Ev cause)
    {
        int i = steps.FindIndex(s => s.Seq > seqBefore);
        if (i < 0) return;   // already shown (the effect paused for a choice: EffectFlash announced it then)
        var at = i > 0 ? steps[i - 1] : Shown();
        steps.Insert(i, new Step { Ev = new Ev { Kind = EvKind.Triggered, Card = c, Player = c.Controller }, Phase = at.Phase, Tp = at.Tp, Turn = at.Turn, Chain = at.Chain, Lp = (int[])at.Lp.Clone(), Cards = at.Cards, Seq = seqBefore, Cause = cause });
    }

    /// The field as it is on screen now.
    Step Shown() => new()
    {
        Phase = shownPhase, Tp = shownTp, Turn = shownTurn, Lp = (int[])shownLp.Clone(),
        Cards = Enumerable.Range(0, D.Cards.Count).Select(u => ViewOf(u).S).ToArray(),
    };

    // ---------------------------------------------------------------- disintegrate

    const string DissolveShader = @"shader_type spatial;
render_mode unshaded, cull_back, depth_draw_always;
uniform sampler2D tex : source_color, filter_linear_mipmap;
uniform float t = 0.0;
uniform vec4 edge : source_color;
float h(vec2 p) { vec3 q = fract(vec3(p.xyx) * 0.1031); q += dot(q, q.yzx + 33.33); return fract((q.x + q.y) * q.z); }  // sin-free: sin() hashes break into blocks on Intel GPUs
float n(vec2 p) { vec2 i = floor(p), f = fract(p); f = f * f * (3.0 - 2.0 * f);
  return mix(mix(h(i), h(i + vec2(1, 0)), f.x), mix(h(i + vec2(0, 1)), h(i + vec2(1, 1)), f.x), f.y); }
void fragment() {
  vec4 c = texture(tex, UV);
  // burns from the top edge down, ragged
  float k = n(UV * vec2(5.0, 7.0)) * 0.55 + n(UV * vec2(17.0, 24.0)) * 0.25 + (1.0 - UV.y) * 0.2;
  float th = t * 1.25 - 0.1;
  if (k < th) discard;
  float e = 1.0 - smoothstep(th, th + 0.07, k);
  ALBEDO = mix(c.rgb, edge.rgb * 3.0, e);
}";

    /// The card burns away where it stood while the real card slips to the Graveyard unseen (edge: the burning rim's colour).
    void Disintegrate(View v, Color? edgeColor = null)
    {
        var ghost = new Node3D();
        world.AddChild(ghost);
        ghost.GlobalTransform = v.Root.GlobalTransform;
        var edge = edgeColor ?? new Color(1f, 0.55f, 0.2f);
        var front = Shade(DissolveShader, ("tex", CardArt.Small(v.Card.Data)), ("edge", edge));
        var back = Shade(DissolveShader, ("tex", backs[Side(v.Card.Owner)]), ("edge", edge));
        ghost.AddChild(new MeshInstance3D { Mesh = new QuadMesh { Size = new Vector2(CW, CH) }, RotationDegrees = new Vector3(-90, 0, 0), Position = new Vector3(0, 0.0045f, 0), MaterialOverride = front, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off });
        ghost.AddChild(new MeshInstance3D { Mesh = new QuadMesh { Size = new Vector2(CW, CH) }, RotationDegrees = new Vector3(90, 0, 0), Position = new Vector3(0, -0.0045f, 0), MaterialOverride = back, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off });
        // Embers lifting off the card while it burns.
        var grad = new Gradient { Offsets = new[] { 0f, 0.25f, 1f }, Colors = new[] { new Color(1f, 0.85f, 0.5f, 1), new Color(edge, 0.9f), new Color(0.35f, 0.3f, 0.35f, 0) } };
        var embers = new GpuParticles3D
        {
            Amount = 90, Lifetime = 0.8, Explosiveness = 0, OneShot = false,
            ProcessMaterial = new ParticleProcessMaterial
            {
                EmissionShape = ParticleProcessMaterial.EmissionShapeEnum.Box, EmissionBoxExtents = new Vector3(CW / 2, 0.01f, CH / 2),
                Direction = new Vector3(0, 1, 0), Spread = 25, InitialVelocityMin = 0.5f, InitialVelocityMax = 1.3f, Gravity = new Vector3(0.25f, 0.6f, -0.15f),
                TurbulenceEnabled = true, TurbulenceNoiseStrength = 0.6f, TurbulenceNoiseScale = 2.5f,
                ScaleMin = 0.3f, ScaleMax = 1f, ColorRamp = new GradientTexture1D { Gradient = grad },
            },
            DrawPass1 = new QuadMesh { Size = new Vector2(0.07f, 0.07f), Material = Glowing(1.4f) },
        };
        ghost.AddChild(embers);
        embers.Emitting = true;
        var tw = CreateTween();
        tw.TweenMethod(Callable.From<float>(x => { front.SetShaderParameter("t", x); back.SetShaderParameter("t", x); }), 0f, 1f, 0.55).SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.Out);
        tw.TweenCallback(Callable.From(() => embers.Emitting = false));
        tw.TweenInterval(0.85);
        tw.TweenCallback(Callable.From(ghost.QueueFree));
    }
}
