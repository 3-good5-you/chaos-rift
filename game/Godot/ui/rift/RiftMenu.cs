using Godot;
using PoC.Engine;

namespace PoC.Game.UI;

/// Chaos Rift look (locked 2026-11-08), shared by the menu screens: the light/dark background torn by a glowing rift,
/// Perpetua Titling text that is graphite on the light side and silver on the dark side (gold when chosen), gold
/// diamonds as markers, hairlines instead of boxes, cards floating with soft shadows.
public abstract partial class RiftMenu : MenuBase
{
    // ---------------------------------------------------------------- shaders

    /// The crack: x = mix(top, bottom, y) in screen UV plus a jagged wobble. `crack_d` < 0 on the light side.
    public const string CrackFn = @"
uniform float top = 0.58;
uniform float bottom = 0.44;
uniform float aspect = 1.6;
float h21(vec2 p) { vec3 q = fract(vec3(p.xyx) * 0.1031); q += dot(q, q.yzx + 33.33); return fract((q.x + q.y) * q.z); }
float n1(float x) { float i = floor(x); float f = fract(x); f = f*f*(3.0-2.0*f); return mix(h21(vec2(i, 1.7)), h21(vec2(i + 1.0, 1.7)), f); }
float crack_d(vec2 suv, float t) {
  float base = mix(top, bottom, suv.y);
  float j = (n1(suv.y * 7.0 + 3.0) - 0.5) * 0.022 + (n1(suv.y * 31.0 + t * 0.5) - 0.5) * 0.005;
  return (suv.x - base - j) * aspect;
}
";

    public const string BgCode = @"shader_type canvas_item;
uniform vec4 light_a : source_color = vec4(0.985, 0.985, 0.98, 1.0); uniform vec4 light_b : source_color = vec4(0.88, 0.88, 0.87, 1.0);
uniform vec4 dark_a : source_color = vec4(0.025, 0.025, 0.03, 1.0); uniform vec4 dark_b : source_color = vec4(0.09, 0.09, 0.11, 1.0);
uniform vec4 halo_l : source_color = vec4(0.85, 0.75, 0.55, 1.0); uniform vec4 halo_d : source_color = vec4(0.7, 0.72, 0.8, 1.0);
uniform float rays = 0.0;
uniform float focus_y = -1.0;
uniform float draw = 1.0;     // intro: how far down the rift has torn
uniform float reveal = 1.0;   // intro: how far the light has flooded the left side
uniform float smoke = 1.0;    // dark-side smoke (0 = clean black void)
uniform float burst = 0.0;    // intro: the moment the rift tears
uniform sampler2D noise_tex : repeat_enable, filter_linear;
" + CrackFn + @"
float fbm(vec2 p) { float a = 0.5, s = 0.0; for (int i = 0; i < 4; i++) { s += a * texture(noise_tex, p).r; p *= 2.03; a *= 0.5; } return s; }
void fragment() {
  vec2 uv = UV; float t = TIME;
  float d = crack_d(uv, t);
  vec2 lp = uv - vec2(-0.05, -0.1);
  float ang = atan(lp.y, lp.x);
  float r = pow(abs(sin(ang * 16.0 + t * 0.04)), 24.0) * exp(-length(lp) * 1.4);
  vec3 light = mix(light_a.rgb, light_b.rgb, smoothstep(0.0, 1.2, length(lp))) + halo_l.rgb * r * rays * 0.25;
  float sm = fbm(uv * vec2(1.6, 1.1) + vec2(-t * 0.01, t * 0.004));
  vec3 dark = mix(dark_a.rgb, dark_b.rgb, smoothstep(0.25, 0.85, sm) * smoothstep(1.4, 0.2, length(uv - vec2(0.85, 0.35))) * smoke);
  dark = mix(vec3(0.008, 0.008, 0.012), dark, smoke);
  dark += step(0.998, h21(floor(uv * vec2(900.0, 560.0)))) * vec3(0.7) * (0.4 + 0.4 * sin(t + uv.x * 70.0));
  float lit_side = step(d, 0.0) * smoothstep(-reveal * 3.2 - 0.001, -reveal * 3.2 + 0.08, d);
  vec3 c = mix(dark, light, lit_side);
  c = mix(c, c * 0.8, smoothstep(-0.12, 0.0, d) * step(d, 0.0) * 0.5 * reveal);
  c += halo_d.rgb * exp(-max(d, 0.0) * 12.0) * 0.22 * step(0.0, d) * draw;
  float flare = focus_y < 0.0 ? 0.0 : exp(-pow((uv.y - focus_y) * 12.0, 2.0));
  float core = (1.0 + burst * 2.5) * exp(-abs(d) * max(420.0 - 200.0 * flare - 300.0 * burst, 60.0)) * (0.9 + 0.1 * n1(t * 6.0 + uv.y * 3.0));
  float halo = exp(-abs(d) * max(34.0 - 22.0 * burst, 10.0)) * (0.35 + flare * 0.6 + burst * 2.0) * (d < 0.0 ? 0.45 : 1.0);
  float torn = smoothstep(draw * 1.15, draw * 1.15 - 0.06, uv.y);
  float tip = exp(-pow((uv.y - draw * 1.15) * 30.0, 2.0)) * step(draw, 0.99) * step(0.001, draw);
  c += (vec3(1.0) * core * 1.3 + (d < 0.0 ? halo_l.rgb : halo_d.rgb) * halo * 0.5) * torn + vec3(1.0, 0.95, 1.0) * tip * exp(-abs(d) * 30.0) * 2.0;
  COLOR = vec4(c, 1.0);
}";

    const string TwoToneCode = @"shader_type canvas_item;
uniform float lit = 0.0;
uniform float dim = 1.0;
" + CrackFn + @"
void fragment() {
  float a = texture(TEXTURE, UV).a * COLOR.a;
  float d = crack_d(SCREEN_UV, TIME);
  float y = smoothstep(0.3, 0.72, UV.y);
  vec3 graphite = mix(vec3(0.26, 0.26, 0.29), vec3(0.05, 0.05, 0.06), y);
  vec3 silver = mix(vec3(1.0), vec3(0.62, 0.64, 0.7), y);
  vec3 gold = d < 0.0 ? mix(vec3(0.74, 0.54, 0.18), vec3(0.5, 0.33, 0.08), y) : mix(vec3(1.0, 0.9, 0.58), vec3(0.7, 0.48, 0.16), y);
  COLOR = vec4(mix(d < 0.0 ? graphite : silver, gold, lit), a * mix(dim, 1.0, lit));
}";

    const string PipCode = "shader_type canvas_item; void fragment(){ vec2 p = UV - 0.5; float l = 0.5 + (-p.x - p.y) * 0.9; COLOR = vec4(mix(vec3(0.55, 0.36, 0.1), vec3(1.0, 0.92, 0.6), l), COLOR.a); }";

    const string ShadowCode = @"shader_type canvas_item;
uniform float strength; uniform vec4 col : source_color; uniform vec2 rect; uniform vec2 half_box; uniform float margin;
void fragment() {
  vec2 p = abs((UV - 0.5) * rect);
  float d = length(max(p - half_box * 0.94, 0.0));
  float k = exp(-pow(d / (margin * 0.42), 2.0)) * (1.0 - smoothstep(margin * 0.55, margin, d));
  COLOR = vec4(col.rgb, strength * k);
}";

    static Shader? bgShader, twoTone, pipShader, shadowShader;
    static Shader S(ref Shader? s, string code) => s ??= new Shader { Code = code };

    // ---------------------------------------------------------------- palette and fonts

    public static readonly Color Ink = new(0.07f, 0.07f, 0.08f), Silver = new(0.92f, 0.92f, 0.94f), GoldC = new(0.82f, 0.62f, 0.26f),
        GoldDeep = new(0.6f, 0.42f, 0.12f), GoldLight = new(1f, 0.85f, 0.5f), Graphite = new(0.2f, 0.2f, 0.23f), Mist = new(0.62f, 0.64f, 0.7f),
        Teal = new(0.55f, 0.85f, 0.9f), Violet = new(0.8f, 0.6f, 1f);

    static Font? title, thin, body;
    public static Font Title => title ??= Fonts.Spaced(Sys(new[] { "Perpetua Titling MT", "Castellar" }, 700), 6);
    public static Font Thin => thin ??= Fonts.Spaced(Sys(new[] { "Perpetua Titling MT", "Castellar" }, 300), 5);
    public static Font Body => body ??= Sys(new[] { "Gill Sans MT", "Segoe UI" }, 400);

    // ---------------------------------------------------------------- screen

    protected float W, H;
    protected float Top = 0.58f, Bottom = 0.44f;
    protected ShaderMaterial bg = null!;
    protected override string? MusicPath => Md("menu_02");
    protected override void OnFocus(int old, int now) { }
    protected override void OnChoose(int i) { }

    /// The rift background; the crack runs from x = top*W (top edge) to bottom*W (bottom edge).
    protected void Rift(float top, float bottom)
    {
        W = GetViewportRect().Size.X; H = GetViewportRect().Size.Y;
        Top = top; Bottom = bottom;
        var noise = new NoiseTexture2D { Width = 512, Height = 512, Seamless = true, Noise = new FastNoiseLite { Frequency = 0.01f } };
        var r = new ColorRect { Size = new Vector2(W, H), MouseFilter = MouseFilterEnum.Ignore };
        bg = new ShaderMaterial { Shader = S(ref bgShader, BgCode) }.P("aspect", W / H).P("noise_tex", noise).P("top", top).P("bottom", bottom);
        r.Material = bg;
        AddChild(r);
    }

    protected float CrackX(float yPx) => Mathf.Lerp(Top, Bottom, yPx / H) * W;
    protected bool OnLight(Vector2 p) => p.X < CrackX(p.Y);

    /// Graphite on the light side, silver on the dark side; `lit` 1 = gold.
    protected ShaderMaterial TwoTone(float dim = 1f) => new ShaderMaterial { Shader = S(ref twoTone, TwoToneCode) }.P("aspect", W / H).P("top", Top).P("bottom", Bottom).P("dim", dim);

    protected static Label Put(Control host, string s, Font f, int size, Color c, Vector2 pos, Vector2 box = default, HorizontalAlignment a = HorizontalAlignment.Left)
    {
        var l = Kit.Put(host, s, f, size, c, pos, box, a);
        l.MouseFilter = MouseFilterEnum.Ignore;
        return l;
    }

    /// A label in the two-tone ink.
    protected Label T(Control host, string s, Font f, int size, Vector2 pos, Vector2 box = default, HorizontalAlignment a = HorizontalAlignment.Left, float dim = 1f)
    {
        var l = Put(host, s, f, size, Colors.White, pos, box, a);
        l.Material = TwoTone(dim);
        return l;
    }

    protected Color InkAt(Vector2 p) => OnLight(p) ? Ink : Silver;
    protected Color GoldAt(Vector2 p) => OnLight(p) ? GoldDeep : GoldLight;

    protected static ColorRect Hair(Control host, Vector2 at, float w, Color c, float t = 1.5f)
    {
        var r = new ColorRect { Position = at, Size = new Vector2(w, t), Color = c, MouseFilter = MouseFilterEnum.Ignore };
        host.AddChild(r);
        return r;
    }

    /// The gold chiselled diamond.
    protected static ColorRect Pip(Control host, Vector2 centre, float s)
    {
        var p = new ColorRect { Size = new Vector2(s, s), Position = centre - new Vector2(s, s) / 2, PivotOffset = new Vector2(s, s) / 2, Rotation = Mathf.Pi / 4, MouseFilter = MouseFilterEnum.Ignore,
            Material = new ShaderMaterial { Shader = S(ref pipShader, PipCode) } };
        host.AddChild(p);
        return p;
    }

    /// Screen title with a gold hairline and a diamond under it.
    protected void Heading(string s, Vector2 at, int size = 48)
    {
        T(this, s, Title, size, at);
        float w = Title.GetStringSize(s, HorizontalAlignment.Left, -1, size).X;
        Hair(this, new Vector2(at.X, at.Y + size * 1.25f), w * 0.6f, GoldC with { A = 0.8f });
        Pip(this, new Vector2(at.X + w * 0.6f + 10, at.Y + size * 1.25f), 9);
    }

    /// Soft drop shadow with no visible edge.
    protected static ColorRect Shadow(Control host, Vector2 pos, Vector2 size, float strength, Color col, Vector2 offset = default)
    {
        float m = Math.Max(size.X, size.Y) * 0.3f;
        var sh = new ColorRect { Position = pos - new Vector2(m, m) + offset, Size = size + new Vector2(2 * m, 2 * m), MouseFilter = MouseFilterEnum.Ignore,
            Material = new ShaderMaterial { Shader = S(ref shadowShader, ShadowCode) }.P("strength", strength).P("col", col).P("rect", size + new Vector2(2 * m, 2 * m)).P("half_box", size / 2).P("margin", m) };
        host.AddChild(sh);
        return sh;
    }

    protected static TextureRect Pic(Control host, Texture2D? t, Vector2 pos, Vector2 size)
    {
        var r = new TextureRect { ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.Scale, MouseFilter = MouseFilterEnum.Ignore, Texture = t, TextureFilter = TextureFilterEnum.LinearWithMipmaps };
        host.AddChild(r);
        r.Position = pos; r.Size = size;   // after ExpandMode, or Godot clamps Size to the texture
        return r;
    }

    /// A card standing in the air with its soft shadow; root pivot in the middle.
    protected Control Card(Control host, CardData d, Vector2 centre, float h, float tilt = 0, float shadow = 1f, bool large = false)
    {
        float w = h * 0.686f;
        var root = new Control { Position = centre - new Vector2(w, h) / 2, Size = new Vector2(w, h), PivotOffset = new Vector2(w, h) / 2, Rotation = tilt, MouseFilter = MouseFilterEnum.Ignore };
        host.AddChild(root);
        bool light = OnLight(centre);
        if (shadow > 0) Shadow(root, Vector2.Zero, new Vector2(w, h), (light ? 0.3f : 0.45f) * shadow, light ? new Color(0.12f, 0.12f, 0.15f) : new Color(0.35f, 0.36f, 0.45f), light ? new Vector2(h * 0.03f, h * 0.05f) : Vector2.Zero);
        var pic = Pic(root, large || h > 300 ? CardArt.Large(d) : CardArt.Small(d), Vector2.Zero, new Vector2(w, h));
        if (!large && h <= 300) CardArt.SmallAsync(d, t => { if (IsInstanceValid(pic)) pic.Texture = t; });
        return root;
    }

    /// Make any control clickable: hover callback, left click, optional right click.
    protected static void Click(Control c, Action click, Action<bool>? hover = null, Action? right = null)
    {
        c.MouseFilter = MouseFilterEnum.Pass;   // Pass: the mouse wheel still reaches the page underneath
        c.MouseDefaultCursorShape = CursorShape.PointingHand;
        if (hover != null) { c.MouseEntered += () => hover(true); c.MouseExited += () => hover(false); }
        if (right != null) c.SetMeta("rmb", true);   // MenuBase._Input: this right click is not "back"
        c.GuiInput += e =>
        {
            if (e is not InputEventMouseButton { Pressed: true } mb) return;
            if (mb.ButtonIndex == MouseButton.Left) click();
            else if (mb.ButtonIndex == MouseButton.Right) right?.Invoke();
        };
    }

    /// A word you can click: dim, gold + diamond when on. Returns its width and a setter.
    protected (float W, Action<bool> Set) Word(Control host, string s, Vector2 at, int size, bool on, Action click, Font? f = null)
    {
        f ??= Title;
        float w = f.GetStringSize(s, HorizontalAlignment.Left, -1, size).X;
        var l = T(host, s, f, size, at, default, HorizontalAlignment.Left, 0.55f);
        var pip = Pip(host, new Vector2(at.X + w / 2, at.Y + size * 1.55f), 7);
        void Set(bool v) { l.Mat().P("lit", v ? 1f : 0f); pip.Visible = v; }
        Set(on);
        Click(l, click, h => { if (!pip.Visible) l.Mat().P("dim", h ? 0.9f : 0.55f); });
        return (w, Set);
    }

    /// A text button between two diamonds; hairlines above and below grow on hover.
    protected Control Button(Control host, string s, Vector2 centre, Action click, int size = 26)
    {
        float w = Title.GetStringSize(s, HorizontalAlignment.Left, -1, size).X;
        var root = new Control { Position = centre - new Vector2(w / 2 + 50, size), Size = new Vector2(w + 100, size * 2) };
        host.AddChild(root);
        var l = T(root, s, Title, size, new Vector2(10, size * 0.28f), new Vector2(w + 80, size * 1.45f), HorizontalAlignment.Center);
        Pip(root, new Vector2(14, size), 10); Pip(root, new Vector2(w + 86, size), 10);
        var top = Hair(root, new Vector2(30, 2), w + 40, GoldC with { A = 0.6f });
        var bot = Hair(root, new Vector2(30, size * 2 - 3), w + 40, GoldC with { A = 0.6f });
        foreach (var x in new Control[] { top, bot }) { x.PivotOffset = new Vector2(x.Size.X / 2, 0); x.Scale = new Vector2(0.3f, 1); }
        Click(root, () => { UiSound(true); click(); }, on =>
        {
            if (on) UiSound(false);
            l.Mat().P("lit", on ? 1f : 0f);
            foreach (var x in new Control[] { top, bot }) Ink_.Only(this, x).TweenProperty(x, "scale:x", on ? 1f : 0.3f, 0.25).SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
        });
        return root;
    }

    /// A name box across the bottom (rename a deck, name a replay): Enter or OK keeps a non-empty name, Esc / CANCEL drops it.
    protected void NamePrompt(string title, string start, Action<string> done)
    {
        var box = new Control { Size = new Vector2(W, 170), Position = new Vector2(0, H - 170), ZIndex = 40 };
        AddChild(box);
        box.AddChild(new ColorRect { Size = box.Size, Color = new Color(0.02f, 0.02f, 0.03f, 0.94f), MouseFilter = MouseFilterEnum.Stop });
        Hair(box, Vector2.Zero, W, GoldC with { A = 0.6f });
        Put(box, title, Thin, 16, Mist, new Vector2(W / 2 - 360, 18), new Vector2(720, 22), HorizontalAlignment.Center);
        var e = new LineEdit { Text = start, Position = new Vector2(W / 2 - 360, 46), Size = new Vector2(720, 60), MaxLength = 40, SelectAllOnFocus = true, Alignment = HorizontalAlignment.Center, Flat = true };
        e.AddThemeFontOverride("font", Title); e.AddThemeFontSizeOverride("font_size", 32); e.AddThemeColorOverride("font_color", Silver); e.AddThemeColorOverride("caret_color", Silver);
        box.AddChild(e);
        Hair(box, new Vector2(W / 2 - 360, 108), 720, Silver * new Color(1, 1, 1, 0.3f));
        void Ok() { var t = e.Text.Trim(); box.QueueFree(); if (t.Length > 0) done(t); }
        e.TextSubmitted += _ => Ok();
        e.GuiInput += ev => { if (ev.IsActionPressed("ui_cancel")) { box.QueueFree(); e.AcceptEvent(); } };
        Button(box, "OK", new Vector2(W / 2 + 470, 76), Ok, 22);
        Button(box, "CANCEL", new Vector2(W / 2 - 500, 76), () => box.QueueFree(), 18);
        e.GrabFocus();
    }

    /// Back link, top left: "‹  NAME".
    protected void Back(string to = "MENU")
    {
        var l = T(this, "‹  " + to, Title, 18, new Vector2(62, 30), default, HorizontalAlignment.Left, 0.7f);
        Click(l, OnBack, h => l.Mat().P("lit", h ? 1f : 0f));
    }

    /// Pages of items (no scroll bars anywhere): `draw(i, host, at)` places item i at `at`. The mouse wheel or
    /// "‹ PAGE n / m ›" turn pages; `label` is where that line goes. Returns a redraw action (keeps the page).
    protected Action Paged(Control parent, Rect2 area, int count, int cols, int rows, Vector2 cell, Action<int, Control, Vector2> draw, Vector2 label)
    {
        int per = Math.Max(1, cols * rows), page = 0;
        var root = new Control { Position = area.Position, Size = area.Size, MouseFilter = MouseFilterEnum.Stop };
        parent.AddChild(root);
        var lab = Put(parent, "", Thin, 16, InkAt(label) * new Color(1, 1, 1, 0.6f), label, new Vector2(300, 22));
        Action redraw = null!;
        void Turn(int d)
        {
            int pages = Math.Max(1, (count + per - 1) / per);
            int np = Math.Clamp(page + d, 0, pages - 1);
            if (np != page) { page = np; redraw(); UiSound(false); }
        }
        redraw = () =>
        {
            foreach (var c in root.GetChildren()) c.QueueFree();
            int pages = Math.Max(1, (count + per - 1) / per);
            page = Math.Clamp(page, 0, pages - 1);
            lab.Text = pages > 1 ? $"‹  PAGE {page + 1} / {pages}  ›" : "";
            for (int k = 0; k < per && page * per + k < count; k++)
                draw(page * per + k, root, new Vector2(k % cols * cell.X, k / cols * cell.Y));
        };
        root.GuiInput += e => { if (e is InputEventMouseButton { Pressed: true } mb && mb.ButtonIndex is MouseButton.WheelDown or MouseButton.WheelUp) Turn(mb.ButtonIndex == MouseButton.WheelDown ? 1 : -1); };
        Click(lab, () => Turn(1), null, () => Turn(-1));
        redraw();
        return redraw;
    }

    /// A one-line message with up to two choices, centred at the bottom of the screen (replaces the old toasts).
    protected Control? toast;
    protected void Ask(string text, params (string Label, Action Do)[] choices)
    {
        CloseToast();
        toast = new Control { Size = new Vector2(W, 150), Position = new Vector2(0, H - 150), ZIndex = 40 };
        AddChild(toast);
        toast.AddChild(new ColorRect { Size = toast.Size, Color = new Color(0.02f, 0.02f, 0.03f, 0.92f), MouseFilter = MouseFilterEnum.Stop });
        Hair(toast, Vector2.Zero, W, GoldC with { A = 0.6f });
        Put(toast, text, Body, 24, Silver, new Vector2(80, 30), new Vector2(W - 160, 36), HorizontalAlignment.Center);
        float x = W / 2 - (choices.Length - 1) * 160;
        foreach (var (label, act) in choices) { Button(toast, label, new Vector2(x, 100), act, 22); x += 320; }
        if (choices.Length == 0) GetTree().CreateTimer(1.6).Timeout += CloseToast;
    }
    protected void CloseToast() { toast?.QueueFree(); toast = null; }

    /// Small fading word at the bottom ("SAVED").
    protected void Flash(string s)
    {
        var l = Put(this, s, Title, 28, GoldLight, new Vector2(0, H - 90), new Vector2(W, 40), HorizontalAlignment.Center);
        l.ZIndex = 41;
        var tw = CreateTween(); tw.TweenInterval(0.8); tw.TweenProperty(l, "modulate:a", 0f, 0.5); tw.TweenCallback(Callable.From(l.QueueFree));
    }

    protected static Texture2D? Avatar => Settings.Avatar;
    protected static Profile P => Session.Profile;
    protected static CardDb Db => CardDb.Shared;
}

public static class RiftExt
{
    public static T P<T>(this T m, string k, Variant v) where T : ShaderMaterial { m.SetShaderParameter(k, v); return m; }
    public static ShaderMaterial Mat(this CanvasItem c) => (ShaderMaterial)c.Material;
}

/// Tween helper name kept short for the kit (Ink.Only lives in the old ink kit).
static class Ink_
{
    public static Tween Only(Node owner, GodotObject target, string key = "tw") => global::PoC.Game.UI.Ink.Only(owner, target, key);
}
