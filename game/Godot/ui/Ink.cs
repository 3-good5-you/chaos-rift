using Godot;
using PoC.Engine;

namespace PoC.Game.UI;

/// The ink/paper look shared by the menu screens after the main menu: dark ink sheet, cream paper splats that
/// bleed in, brush strokes in any colour, ink-drop difficulty, and the logo + title header.
public static class Ink
{
    public static readonly Color Cream = new(0.9f, 0.87f, 0.8f), Black = new(0.05f, 0.04f, 0.05f);

    /// Ink that bleeds in along ink_field: `progress` 0 = none, ~1.1 = full. tex_mix 1 shows the texture, 0 the ink colour.
    const string Bleed = @"shader_type canvas_item;
uniform sampler2D field : filter_linear, repeat_enable;
uniform float progress = 0.0;
uniform vec4 ink : source_color = vec4(0.05, 0.04, 0.05, 1.0);
uniform float tex_mix = 0.0;
uniform vec2 seed = vec2(0.0);
void fragment() {
  vec4 t = texture(TEXTURE, UV);
  float f = texture(field, UV * 0.55 + seed).r;
  float v = length((UV - 0.5) * 2.0) * 0.8 + f * 0.4;
  float thr = progress * 1.2;
  float m = smoothstep(thr + 0.02, thr - 0.02, v);
  float fringe = exp(-pow((v - thr) / 0.02, 2.0)) * 0.5;
  vec3 c = mix(ink.rgb, t.rgb, tex_mix) * (1.0 - fringe * (1.0 - tex_mix));
  COLOR = vec4(c, t.a * m * ink.a);
}";

    /// Picture washed toward one-colour ink (wash 1) or full colour (wash 0).
    const string WashCode = @"shader_type canvas_item;
uniform float wash = 0.0;
uniform vec4 ink : source_color = vec4(0.03, 0.03, 0.04, 1.0);
uniform vec4 paper : source_color = vec4(0.45, 0.42, 0.4, 1.0);
void fragment() {
  vec4 t = texture(TEXTURE, UV);
  float l = smoothstep(0.1, 0.95, dot(t.rgb, vec3(0.3, 0.59, 0.11)));
  COLOR = vec4(mix(t.rgb, mix(ink.rgb, paper.rgb, l), wash), t.a * COLOR.a);
}";

    const string Tint = @"shader_type canvas_item;
uniform vec4 tint : source_color;
void fragment() {
  // max of a few vertical taps: closes the thin highlight line through brush_ink.png
  float a = texture(TEXTURE, UV).a;
  for (int i = 1; i <= 3; i++) { float o = float(i) * 0.012; a = max(a, min(texture(TEXTURE, UV + vec2(0, o)).a, texture(TEXTURE, UV - vec2(0, o)).a)); }
  COLOR = vec4(tint.rgb, a * tint.a);
}";

    /// Castellar, the capitals of the "Chaos Ascendant" logo: display names and numerals (the blackletter read badly).
    public static Font Display => display ??= Fonts.Get(new[] { "Castellar", "Times New Roman" });
    static Font? display;

    static Texture2D? field;
    static Texture2D Field => field ??= GD.Load<Texture2D>("res://assets/title/ink_field.png");
    static Shader? bleed, wash, tint;

    static ShaderMaterial M(ref Shader? s, string code) => new() { Shader = s ??= new Shader { Code = code } };

    public static TextureRect Pic(Texture2D tex, Vector2 pos, Vector2 size, Node parent)
    {
        var r = new TextureRect { Texture = tex, ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered, Position = pos, Size = size, MouseFilter = Control.MouseFilterEnum.Ignore };
        parent.AddChild(r);
        return r;
    }

    /// The paper title card behind everything (the light screens).
    public static void Paper(Control host)
    {
        var bg = Pic(GD.Load<Texture2D>("res://assets/title/paper_bg.png"), Vector2.Zero, host.GetViewportRect().Size, host);
        bg.StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered;
    }

    /// The dark ink sheet behind everything.
    public static void Sheet(Control host) => host.AddChild(new ColorRect
    {
        AnchorRight = 1, AnchorBottom = 1, MouseFilter = Control.MouseFilterEnum.Ignore,
        Material = new ShaderMaterial
        {
            Shader = new Shader
            {
                Code = @"shader_type canvas_item;
uniform sampler2D field : filter_linear;
void fragment() {
  float f = texture(field, UV).r;
  vec3 c = mix(vec3(0.035, 0.03, 0.04), vec3(0.09, 0.08, 0.09), f * f);
  c *= 1.0 - 0.45 * length((UV - 0.5) * vec2(1.3, 1.0));
  COLOR = vec4(c, 1.0);
}",
            },
        }.With("field", Field),
    });

    static ShaderMaterial With(this ShaderMaterial m, string k, Variant v) { m.SetShaderParameter(k, v); return m; }

    /// A bleed material: `colour` ink spreading to `progress`. Animate with <see cref="To"/>.
    public static ShaderMaterial BleedMat(Color colour, float progress, int seed, bool tex = false) =>
        M(ref bleed, Bleed).With("field", Field).With("progress", progress).With("ink", colour).With("tex_mix", tex ? 1f : 0f).With("seed", new Vector2(seed * 0.37f, seed * 0.21f));

    public static ShaderMaterial WashMat(float amount) => M(ref wash, WashCode).With("wash", amount);

    /// Tween a float shader parameter.
    public static void To(Node owner, ShaderMaterial m, string p, float to, double t = 0.45) =>
        Only(owner, m, "tw_" + p).TweenMethod(Callable.From<float>(v => m.SetShaderParameter(p, v)), (float)m.GetShaderParameter(p), to, t).SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);

    /// A new tween on `owner` for `target`, after killing the last one started for the same target and key. Show and
    /// hide tweens last different times, so without this a quick hover off left the longer "show" running last and
    /// the highlight stuck on.
    public static Tween Only(Node owner, GodotObject target, string key = "tw")
    {
        if (target.HasMeta(key) && target.GetMeta(key).AsGodotObject() is Tween old && GodotObject.IsInstanceValid(old)) old.Kill();
        var tw = owner.CreateTween();
        target.SetMeta(key, tw);
        return tw;
    }

    /// The ink brush stroke (or any black-ink texture) in any colour.
    public static TextureRect Brush(Vector2 pos, Vector2 size, Color c, Node parent, string path = "res://assets/title/brush_ink.png")
    {
        var b = Pic(GD.Load<Texture2D>(path), pos, size, parent);
        b.StretchMode = TextureRect.StretchModeEnum.Scale;
        b.Material = M(ref tint, Tint).With("tint", c);
        return b;
    }

    /// Logo top left, the screen title beside it on a brush rule, BACK top right. `logoW` 420 for the roomy screens,
    /// smaller (`compact`) on the busy ones. Returns the x right after the title's rule and the header's bottom y.
    public static (float AfterTitle, float Bottom) Header(Control host, string title, Action back, bool onPaper = false, float logoW = 420)
    {
        float w = host.GetViewportRect().Size.X, k = logoW / 420;
        var ink = onPaper ? Black : Cream;
        var logo = GD.Load<Texture2D>(onPaper ? "res://assets/title/logo_light.png" : "res://assets/title/logo_dark.png");
        float lw = logoW, lh = lw * logo.GetHeight() / logo.GetWidth(), top = 30 * k;
        Pic(logo, new Vector2(50 * k, top), new Vector2(lw, lh), host);
        int ts = (int)(64 * Math.Max(0.75f, k));
        Kit.Put(host, title, Kit.Cond, ts, ink, new Vector2(lw + 110 * k, top + lh / 2 - ts * 0.69f));
        float tw = Kit.Cond.GetStringSize(title, HorizontalAlignment.Left, -1, ts).X + 40;
        Brush(new Vector2(lw + 100 * k, top + lh / 2 + ts * 0.53f), new Vector2(Math.Max(tw, 520 * k), 16 * Math.Max(0.75f, k)), ink * new Color(1, 1, 1, 0.55f), host);
        var dim = ink * new Color(1, 1, 1, 0.75f);
        var b = Kit.Put(host, "‹  BACK", Kit.Cond, 40, dim, new Vector2(w - 230, top + lh / 2 - 28));
        b.MouseFilter = Control.MouseFilterEnum.Stop; b.MouseDefaultCursorShape = Control.CursorShape.PointingHand;
        b.MouseEntered += () => b.AddThemeColorOverride("font_color", ink);
        b.MouseExited += () => b.AddThemeColorOverride("font_color", dim);
        b.GuiInput += e => { if (e is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left }) back(); };
        return (lw + 100 * k + Math.Max(tw, 520 * k), top + lh);
    }

    /// A header tab: a word that gets a cream brush stroke behind it (ink letters) while open. `lit` sets it;
    /// returns its width.
    public static float TabWord(Control host, string text, Vector2 pos, Action click, out Action<bool> lit)
    {
        float w = Kit.Cond.GetStringSize(text, HorizontalAlignment.Left, -1, 40).X + 40;
        var root = new Control { Position = pos, Size = new Vector2(w, 68), MouseFilter = Control.MouseFilterEnum.Stop, MouseDefaultCursorShape = Control.CursorShape.PointingHand };
        host.AddChild(root);
        var b = Brush(new Vector2(-18, 0), new Vector2(w + 36, 68), Cream, root);
        b.Scale = new Vector2(0, 1);
        var l = Kit.Put(root, text, Kit.Cond, 40, Cream, Vector2.Zero, root.Size, HorizontalAlignment.Center);
        l.VerticalAlignment = VerticalAlignment.Center;
        root.GuiInput += e => { if (e is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left }) click(); };
        bool was = false;
        lit = on =>
        {
            l.AddThemeColorOverride("font_color", on ? Black : Cream * new Color(1, 1, 1, 0.6f));
            if (on != was)
            {
                b.Visible = true;   // hidden while unlit: a zero-width brush still drew a 1 px line
                var tw = Only(root, b);
                tw.TweenProperty(b, "scale:x", on ? 1f : 0f, on ? 0.28 : 0.15).SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
                if (!on) tw.TweenCallback(Callable.From(() => b.Visible = false));
            }
            was = on;
        };
        b.Visible = false;
        lit(false);
        return w;
    }

    /// Seven ink drops centred on (cx, y), the level's worth in the tier colour, tier word under them.
    public static Control Drops(Control parent, float cx, float y, OpponentPick pick, bool onPaper = false)
    {
        var root = new Control { Position = new Vector2(cx, y), MouseFilter = Control.MouseFilterEnum.Ignore };
        parent.AddChild(root);
        var tier = onPaper ? Session.TierColor(pick.Level).Darkened(0.3f) : Session.TierColor(pick.Level);
        var empty = (onPaper ? Black : Cream) * new Color(1, 1, 1, 0.13f);
        var rng = new RandomNumberGenerator { Seed = 5 };
        for (int i = 0; i < 7; i++)
        {
            float s = 40 + rng.Randf() * 8;
            var d = Brush(new Vector2(-132 + i * 44 - s / 2, -s / 2 + (i % 2) * 4), new Vector2(s, s), i < pick.Level ? tier : empty, root, "res://assets/title/ink_splash.png");
            d.PivotOffset = new Vector2(s, s) / 2; d.Rotation = rng.Randf() * 6.28f;
        }
        Kit.Put(root, pick.Tier, Kit.Cond, 34, tier, new Vector2(-200, 30), new Vector2(400, 44), HorizontalAlignment.Center);
        return root;
    }
}
