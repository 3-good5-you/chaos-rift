using Godot;

namespace PoC.Game.UI;

/// Screen change. Lives on its own layer under the root, so it survives the scene change; it also hides the load
/// pause of heavy scenes (the duel field). Styles (samples 2026-11-08, `-- --transition=NAME` to try one):
/// curtain = the old slanted dark curtain with gold edges;
/// tear    = the old screen tears along a rift and the two halves slide apart, the new screen behind;
/// sweep   = the rift splits into two glowing lines that race out to the edges, the new screen between them;
/// open    = a rift opens down the middle and widens like a window until the new screen fills it.
/// A Rift screen tears along the rift already drawn on it (playtest 2026-11-12: no second, new tear).
public static class Transition
{
    const float Skew = RivalPanels.Skew;
    const double In = 0.3, Out = 0.38;
    static bool busy;
    public static string Style = OS.GetCmdlineUserArgs().FirstOrDefault(a => a.StartsWith("--transition="))?[13..] ?? "tear";

    /// Sound played as the screen changes (a UiSfxLab spec), null = silent. The tear's rip (2026-11-09, sfxforge rift_tear).
    public static string? Sound = Style == "tear" ? "gen/G_rift_tear" : null;

    public static void Go(Node from, string scene)
    {
        if (!busy && Sound != null && !OS.GetCmdlineUserArgs().Any(a => a.StartsWith("--shot")))
        {
            var root = from.GetTree().Root;
            var pa = new AudioStreamPlayer { Bus = "Sound" }; var pb = new AudioStreamPlayer { Bus = "Sound" };   // follows the Sound volume in Options
            root.AddChild(pa); root.AddChild(pb);
            UiSfxLab.PlaySpec(root, pa, pb, Sound);
            root.GetTree().CreateTimer(3).Timeout += () => { pa.QueueFree(); pb.QueueFree(); };
        }
        if (Style == "curtain") Run(from.GetTree(), scene);
        else Rift(from.GetTree(), scene, Style, from is RiftMenu rm ? rm.RiftLine : null);
    }

    const string RiftCode = @"shader_type canvas_item;
uniform sampler2D old_tex : filter_linear;
uniform float p = 0.0;
uniform int mode = 0;   // 0 tear, 1 sweep, 2 open
uniform float top = -1.0;   // the old screen's rift (RiftMenu Top / Bottom); < 0 = none, tear down the middle
uniform float bottom = 0.5;
float h21(vec2 q) { vec3 r = fract(vec3(q.xyx) * 0.1031); r += dot(r, r.yzx + 33.33); return fract((r.x + r.y) * r.z); }
float n1(float x) { float i = floor(x); float f = fract(x); f = f*f*(3.0-2.0*f); return mix(h21(vec2(i, 1.7)), h21(vec2(i + 1.0, 1.7)), f); }
float jag(float y) { return (n1(y * 7.0 + 3.0) - 0.5) * 0.03 + (n1(y * 31.0) - 0.5) * 0.008; }
void fragment() {
  vec2 uv = UV;
  bool own = top >= 0.0;   // the same line RiftMenu.CrackFn draws
  float c = own ? mix(top, bottom, uv.y) + (n1(uv.y * 7.0 + 3.0) - 0.5) * 0.022 : 0.5 + jag(uv.y) * 1.4;
  // first the rift draws itself top to bottom on the old screen, then it acts
  float drawn = own ? 1.0 : smoothstep(0.0, 0.22, p);
  float q = own ? p : smoothstep(0.22, 1.0, p);   // a rift already there: no drawing it first
  float line = exp(-abs(uv.x - c) * 420.0) * step(uv.y, drawn * 1.08) * (1.0 - q);
  vec4 col = vec4(0.0);
  float edge = 1.0;
  if (mode == 0) {            // tear: halves slide apart, a hot rim on each torn edge
    float s = q * q * 0.62;
    if (uv.x < c - s) { col = texture(old_tex, uv + vec2(s, 0.0)); edge = (c - s) - uv.x; }
    else if (uv.x > c + s) { col = texture(old_tex, uv - vec2(s, 0.0)); edge = uv.x - (c + s); }
    float d = min(abs(uv.x - (c - s)), abs(uv.x - (c + s)));
    col.rgb += vec3(1.0, 0.95, 1.0) * exp(-d * 260.0) * (1.0 - q) * 1.5 * step(0.001, q);
    col.a = max(col.a, exp(-d * 260.0) * (1.0 - q) * step(0.001, q));
  } else if (mode == 1) {     // sweep: the rift travels right to left
    float x = mix(0.5, -0.25, q) + jag(uv.y) * 2.0;
    float xr = 1.0 - x + jag(uv.y + 0.37) * 2.0;
    if (uv.x < x || uv.x > xr) col = texture(old_tex, uv);
    float d = min(abs(uv.x - x), abs(uv.x - xr)) + (q < 0.001 ? 10.0 : 0.0);
    vec3 glow = vec3(1.0) * exp(-d * 300.0) * 1.6 + vec3(0.7, 0.72, 0.85) * exp(-d * 30.0) * 0.5;
    col.rgb = col.rgb + glow;
    col.a = max(col.a, clamp(exp(-d * 30.0) * 0.8, 0.0, 1.0));
  } else {                    // open: a window widens from the rift
    float w = q * q * 0.8;
    float d = abs(uv.x - c) - w;
    if (d > 0.0) col = texture(old_tex, uv);
    col.rgb = col.rgb * (1.0 - exp(-max(d, 0.0) * 25.0) * 0.6 * step(0.0, d)) + vec3(1.0, 0.96, 1.0) * exp(-abs(d) * 220.0) * 1.4;
    col.a = max(col.a, exp(-abs(d) * 220.0) * (1.0 - q * 0.6) * step(0.001, q));
  }
  col.rgb += vec3(1.0, 0.96, 1.0) * line * 1.6;
  COLOR = col;
}";

    /// The rift styles: snapshot the old screen, swap the scene at once (it draws underneath), and animate the
    /// snapshot away along the rift.
    static async void Rift(SceneTree tree, string scene, string style, Vector2? line)
    {
        if (busy) return;
        busy = true;
        var size = tree.Root.GetVisibleRect().Size;
        var old = ImageTexture.CreateFromImage(tree.Root.GetTexture().GetImage());
        var layer = new CanvasLayer { Layer = 100 };
        tree.Root.AddChild(layer);
        var block = new Control { Size = size, MouseFilter = Control.MouseFilterEnum.Stop };
        layer.AddChild(block);
        var mat = new ShaderMaterial { Shader = new Shader { Code = RiftCode } };
        mat.SetShaderParameter("old_tex", old);
        int mode = style switch { "sweep" => 1, "open" => 2, _ => 0 };
        mat.SetShaderParameter("mode", mode);
        if (line is { } rl) { mat.SetShaderParameter("top", rl.X); mat.SetShaderParameter("bottom", rl.Y); }
        var rect = new ColorRect { Size = size, Material = mat, MouseFilter = Control.MouseFilterEnum.Ignore };
        layer.AddChild(rect);
        tree.ChangeSceneToFile(scene);
        // let the new scene build and settle for a few frames behind the snapshot (its first frames hitch)
        for (int i = 0; i < 4; i++) await tree.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
        var tw = rect.CreateTween();
        var set = Callable.From<float>(v => mat.SetShaderParameter("p", v));
        if (line != null) tw.TweenMethod(set, 0f, 1f, 0.6).SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.In);
        else tw.TweenMethod(set, 0f, 1f, 0.8).SetTrans(Tween.TransitionType.Linear);
        await rect.ToSignal(tw, Tween.SignalName.Finished);
        layer.QueueFree();
        busy = false;
    }

    static async void Run(SceneTree tree, string scene)
    {
        if (busy) return;
        busy = true;
        var size = tree.Root.GetVisibleRect().Size;
        float W = size.X, H = size.Y;
        var layer = new CanvasLayer { Layer = 100 };
        tree.Root.AddChild(layer);
        var block = new Control { Size = size, MouseFilter = Control.MouseFilterEnum.Stop };
        layer.AddChild(block);

        var curtain = new Node2D { Position = new Vector2(W + 10, 0) };
        var poly = new[] { new Vector2(Skew, 0), new Vector2(W + 2 * Skew, 0), new Vector2(W + Skew, H), new Vector2(0, H) };
        curtain.AddChild(new Polygon2D { Polygon = poly, Color = new Color(0.035f, 0.032f, 0.045f) });
        curtain.AddChild(new Polygon2D
        {
            Polygon = poly,
            VertexColors = new[] { new Color(1f, 0.82f, 0.4f, 0f), new Color(1f, 0.82f, 0.4f, 0.07f), new Color(1f, 0.82f, 0.4f, 0f), new Color(1f, 0.82f, 0.4f, 0.07f) },
        });
        foreach (var (a, b) in new[] { (poly[3], poly[0]), (poly[2], poly[1]) })
            curtain.AddChild(new Line2D { Points = new[] { a, b }, Width = 4, DefaultColor = RivalPanels.Gold });
        layer.AddChild(curtain);

        var tw = curtain.CreateTween();
        tw.TweenProperty(curtain, "position:x", -Skew, In).SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.In);
        await curtain.ToSignal(tw, Tween.SignalName.Finished);

        tree.ChangeSceneToFile(scene);
        await tree.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
        await tree.ToSignal(tree, SceneTree.SignalName.ProcessFrame);

        var tw2 = curtain.CreateTween();
        tw2.TweenProperty(curtain, "position:x", -W - 2 * Skew - 10, Out).SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
        await curtain.ToSignal(tw2, Tween.SignalName.Finished);
        layer.QueueFree();
        busy = false;
    }
}
