using Godot;
using PoC.Engine;

namespace PoC.Game.UI;

/// Die roll (EvKind.Die): a lacquered dice cup shakes over the field, slams down and lifts to show the die
/// lying on the engine's result, then a gold badge and the outcome text spell it out.
public partial class DuelField
{
    const float DieSize = 0.7f;
    static readonly Color Ivory = new(0.94f, 0.91f, 0.83f), Ink = new(0.08f, 0.08f, 0.1f), AceRed = new(0.8f, 0.08f, 0.1f);

    // A subdivided unit box pulled into a rounded cube; pips are drawn per face (1 opposite 6, 2-5, 3-4).
    const string DieShader = @"shader_type spatial;
render_mode unshaded;
uniform vec3 body; uniform vec3 pip; uniform vec3 ace;
uniform float round_r = 0.16;
varying vec3 lp; varying vec3 ln;
void vertex() {
  lp = VERTEX; ln = NORMAL;
  vec3 h = vec3(.5 - round_r);
  vec3 c = clamp(VERTEX, -h, h);
  vec3 n = normalize(VERTEX - c);
  VERTEX = c + n * round_r;
  NORMAL = n;
}
float pips(vec2 p, int v) {
  float d = 1e3;
  if (v % 2 == 1) d = min(d, length(p));
  if (v >= 2) { d = min(d, length(p - vec2(-.25, -.25))); d = min(d, length(p - vec2(.25, .25))); }
  if (v >= 4) { d = min(d, length(p - vec2(-.25, .25))); d = min(d, length(p - vec2(.25, -.25))); }
  if (v == 6) { d = min(d, length(p - vec2(-.25, 0))); d = min(d, length(p - vec2(.25, 0))); }
  return d;
}
void fragment() {
  vec3 a = abs(ln); int v; vec2 p;
  if (a.z > .5) { v = ln.z < 0. ? 1 : 6; p = lp.xy; }
  else if (a.x > .5) { v = ln.x > 0. ? 2 : 5; p = lp.zy; }
  else { v = ln.y > 0. ? 3 : 4; p = lp.xz; }
  float r = v == 1 ? .15 : .085;
  float inPip = 1. - smoothstep(r - .015, r, pips(p, v));
  float shade = .5 + .5 * max(dot(NORMAL, normalize(vec3(-.3, .6, .8))), 0.);
  ALBEDO = mix(body * shade, (v == 1 ? ace : pip) * (.6 + .4 * shade), inPip);
}";

    const string ShadowShader = @"shader_type spatial;
render_mode unshaded, cull_disabled;
uniform float a = .5;
void fragment() { ALBEDO = vec3(0); ALPHA = a * (1. - smoothstep(.1, .5, length(UV - .5))); }";

    /// The die's own normal for face r (matches the shader).
    static Vector3 FaceNormal(int r) => r switch
    {
        1 => Vector3.Forward, 6 => Vector3.Back, 2 => Vector3.Right, 5 => Vector3.Left, 3 => Vector3.Up, _ => Vector3.Down,
    };

    /// Rotation taking unit axis a onto unit axis b.
    static Basis Turn(Vector3 a, Vector3 b)
    {
        if (a.IsEqualApprox(b)) return Basis.Identity;
        if (a.IsEqualApprox(-b)) return new Basis(Mathf.Abs(a.Y) < 0.9f ? Vector3.Up : Vector3.Right, Mathf.Pi);
        return new Basis(a.Cross(b).Normalized(), a.AngleTo(b));
    }

    /// Plays the roll; returns how long the field should wait before the next step.
    double DiceCup(Ev e, bool words = true)
    {
        int r = Math.Clamp(e.Amount, 1, 6);
        if (e.Other != null) Focus(e.Other);
        var at = new Vector3(0.2f, 0, 0.15f);

        // The die, already lying on its result (face r up), hidden until the cup comes down over it.
        var dieRoot = new Node3D { Position = at + Vector3.Up * DieSize / 2, Visible = false };
        world.AddChild(dieRoot);
        var yaw = new Basis(Vector3.Up, 0.5f);
        var spin = new Node3D { Basis = yaw };
        dieRoot.AddChild(spin);
        var face = new Node3D { Basis = Turn(FaceNormal(r), Vector3.Up) };
        spin.AddChild(face);
        face.AddChild(new MeshInstance3D
        {
            Mesh = new BoxMesh { Size = Vector3.One, SubdivideWidth = 24, SubdivideHeight = 24, SubdivideDepth = 24 },
            Scale = Vector3.One * DieSize,
            MaterialOverride = Shade(DieShader, ("body", Ivory), ("pip", Ink), ("ace", AceRed)),
        });
        var shadow = new MeshInstance3D { Mesh = new PlaneMesh { Size = Vector2.One * 1.5f }, MaterialOverride = Shade(ShadowShader, ("a", 0.25f)), Position = at + Vector3.Up * 0.015f, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off };
        world.AddChild(shadow);

        // Lacquered cup, mouth down, gold bands.
        var cup = new Node3D { Position = at + new Vector3(0, 1.6f, 0), Scale = Vector3.One * 0.001f };
        world.AddChild(cup);
        var lacquer = new StandardMaterial3D { AlbedoColor = new Color(0.42f, 0.06f, 0.07f), Roughness = 0.25f, Metallic = 0.1f };
        var band = new StandardMaterial3D { AlbedoColor = RivalPanels.Gold, Roughness = 0.3f, Metallic = 0.8f };
        cup.AddChild(new MeshInstance3D { Mesh = new CylinderMesh { TopRadius = 0.42f, BottomRadius = 0.55f, Height = 0.85f, RadialSegments = 48 }, MaterialOverride = lacquer, Position = new Vector3(0, 0.425f, 0) });
        cup.AddChild(new MeshInstance3D { Mesh = new CylinderMesh { TopRadius = 0.565f, BottomRadius = 0.57f, Height = 0.07f, RadialSegments = 48 }, MaterialOverride = band, Position = new Vector3(0, 0.04f, 0) });
        cup.AddChild(new MeshInstance3D { Mesh = new CylinderMesh { TopRadius = 0.44f, BottomRadius = 0.445f, Height = 0.05f, RadialSegments = 48 }, MaterialOverride = band, Position = new Vector3(0, 0.83f, 0) });
        cup.AddChild(new MeshInstance3D { Mesh = new CylinderMesh { TopRadius = 0.2f, BottomRadius = 0.2f, Height = 0.02f, RadialSegments = 32 }, MaterialOverride = band, Position = new Vector3(0, 0.86f, 0) });

        var tw = CreateTween();
        Cue("cup_appear");
        tw.TweenProperty(cup, "scale", Vector3.One, 0.2).SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
        // Shake: side to side with a rattle, getting wilder.
        tw.TweenCallback(Callable.From(() => Cue("cup_shake")));
        tw.TweenMethod(Callable.From<float>(k =>
        {
            float amp = 0.15f + 0.3f * k;
            cup.Rotation = new Vector3(0.15f * Mathf.Sin(k * 31), 0, amp * Mathf.Sin(k * 47));
            cup.Position = at + new Vector3(0.12f * Mathf.Sin(k * 47), 1.6f + 0.08f * Mathf.Abs(Mathf.Sin(k * 23)), 0);
        }), 0f, 1f, 0.9);
        // Slam.
        tw.TweenProperty(cup, "rotation", Vector3.Zero, 0.06);
        tw.Parallel().TweenProperty(cup, "position", at, 0.12).SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.In);
        tw.TweenCallback(Callable.From(() => { dieRoot.Visible = true; Shake(0.06f); Shockwave(at, RivalPanels.Gold, 2f); Cue("cup_slam"); }));
        tw.TweenProperty(cup, "scale", new Vector3(1.08f, 0.9f, 1.08f), 0.06);
        tw.TweenProperty(cup, "scale", Vector3.One, 0.2).SetTrans(Tween.TransitionType.Elastic).SetEase(Tween.EaseType.Out);
        tw.TweenInterval(0.3);
        // Lift and tip back, away from the camera; the die rocks once as the cup leaves.
        tw.TweenCallback(Callable.From(() => Cue("cup_lift")));
        tw.TweenProperty(cup, "position", at + new Vector3(0.1f, 1.5f, -1.1f), 0.45).SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
        tw.Parallel().TweenProperty(cup, "rotation", new Vector3(-0.9f, 0, 0.15f), 0.45).SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
        tw.Parallel().TweenMethod(Callable.From<float>(k => spin.Basis = yaw * new Basis(Vector3.Right, 0.12f * Mathf.Sin(k * 18) * (1 - k))), 0f, 1f, 0.45);
        tw.TweenCallback(Callable.From(() => Cue("die_result")));
        tw.TweenProperty(cup, "scale", Vector3.One * 0.001f, 0.25);
        tw.TweenInterval(1.35);
        tw.TweenProperty(dieRoot, "scale", Vector3.One * 0.001f, 0.2);
        tw.Parallel().TweenProperty(shadow, "scale", Vector3.One * 0.001f, 0.2);
        tw.TweenCallback(Callable.From(() => { cup.QueueFree(); dieRoot.QueueFree(); shadow.QueueFree(); }));

        // The result in the coin's chip (no badge by the die, no words outside it).
        if (words) After(2.4, () => ResultChip($"{(e.Other?.Name ?? "DIE ROLL").ToUpperInvariant()}  ·  ROLLED {r}", RivalPanels.Gold, 1.6));
        return 3.6;
    }
}
