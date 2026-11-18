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
uniform float dark = 0.0;   // 1 = always the dark-side colours (text on a dark bar over the rift)
" + CrackFn + @"
void fragment() {
  float a = texture(TEXTURE, UV).a * COLOR.a;
  float d = dark > 0.5 ? 1.0 : crack_d(SCREEN_UV, TIME);
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
    // was the Light cut (300): too thin to read at caption sizes (playtest 2026-11-12)
    public static Font Thin => thin ??= Fonts.Spaced(Sys(new[] { "Perpetua Titling MT", "Castellar" }, 700), 4);
    // FX lab pick 2026-11-12: body text in the heavy cut, small text 15% bigger (Z). Card text keeps the regular cut
    // (BodyRegular) so its bold keywords still stand out.
    public static Font Body => body ??= Sys(new[] { "Gill Sans MT", "Segoe UI" }, 700);
    static Font? bodyRegular;
    public static Font BodyRegular => bodyRegular ??= Sys(new[] { "Gill Sans MT", "Segoe UI" }, 400);
    /// The size small menu text is drawn at: up to 22 it grows 15%; headings stay. Put applies it, so anything that
    /// measures a label must measure at Z(size) too.
    public static int Z(int size) => size <= 22 ? (int)MathF.Round(size * 1.15f) : size;

    // ---------------------------------------------------------------- screen

    protected float W, H;
    protected float Top = 0.58f, Bottom = 0.44f;
    /// Where this screen's rift runs (x at the top and bottom edge, 0..1); the transition tears along it.
    public Vector2? RiftLine => bg == null ? null : new Vector2(Top, Bottom);
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
        var l = Kit.Put(host, s, f, Z(size), c, pos, box, a);
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
        float w = Title.GetStringSize(Lang.Tr(s), HorizontalAlignment.Left, -1, Z(size)).X;
        Hair(this, new Vector2(at.X, at.Y + size * 1.25f), w * 0.6f, GoldC with { A = 0.8f });
        Pip(this, new Vector2(at.X + w * 0.6f + 10, at.Y + size * 1.25f), 9);
    }

    /// Tabs as one pill (the Single / Match toggle picked in the Menu Lab, 2026-11-16): equal halves, the open one ink with
    /// gold text and a soft shadow. The text shrinks until the pill fits `maxW` (Spanish / Arabic run longer).
    /// Returns the pill's size.
    protected Vector2 Pill(Control host, string[] labels, int on, Vector2 at, Action<int> pick, float maxW, int size = 18)
    {
        int fs = size;
        float Seg(int s) => labels.Max(l => Title.GetStringSize(Lang.Tr(l), HorizontalAlignment.Left, -1, Z(s)).X) + 56;
        while (fs > 11 && Seg(fs) * labels.Length > maxW) fs--;
        float seg = Seg(fs), h = MathF.Round(Z(size) * 2.6f), pw = seg * labels.Length;
        var track = new Panel { Position = at, Size = new Vector2(pw, h), MouseFilter = MouseFilterEnum.Ignore };
        track.AddThemeStyleboxOverride("panel", Box(new Color(0, 0, 0, 0.06f), Ink * new Color(1, 1, 1, 0.14f), 1.5f, 99));
        host.AddChild(track);
        var knob = new Panel { Position = at + new Vector2(on * seg + 4, 4), Size = new Vector2(seg - 8, h - 8), MouseFilter = MouseFilterEnum.Ignore };
        knob.AddThemeStyleboxOverride("panel", Box(Ink, default, 0, 99, 10, new Color(0, 0, 0, 0.25f), new Vector2(0, 3)));
        host.AddChild(knob);
        for (int i = 0; i < labels.Length; i++)
        {
            int k = i;
            var dim = Ink * new Color(1, 1, 1, 0.6f);
            var l = Put(host, labels[i], Title, fs, i == on ? GoldLight : dim, at + new Vector2(i * seg, Z(fs) * 0.12f), new Vector2(seg, h), HorizontalAlignment.Center);
            l.VerticalAlignment = VerticalAlignment.Center;
            Click(l, () => { if (k == on) return; UiSound(false); pick(k); }, hv => { if (k != on) l.AddThemeColorOverride("font_color", hv ? Ink : dim); });
        }
        return new Vector2(pw, h);
    }

    /// Two screens sharing one main-menu entry (2026-11-17: ACHIEVEMENTS | REPLAYS): a pill in place of the heading.
    protected void HeadTabs((string Label, string Scene)[] tabs, int on, Vector2 at) =>
        Pill(this, tabs.Select(t => t.Label).ToArray(), on, at, i => Transition.Go(this, tabs[i].Scene), CrackX(at.Y) - at.X - 60, 20);

    protected static readonly (string, string)[] RecordTabs = { ("ACHIEVEMENTS", "res://ui/Achievements.tscn"), ("REPLAYS", "res://ui/Replays.tscn") };

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

    static Font? bodyBold;
    public static Font BodyBold => bodyBold ??= Sys(new[] { "Gill Sans MT", "Segoe UI" }, 700);

    /// A card's text with its keywords marked (LabText.Card), shrunk from `size` until it fits the box. `light` = the
    /// darker inks for the light side of the rift.
    protected static RichTextLabel CardText(Control host, CardData d, Vector2 pos, Vector2 box, int size, bool light, int min = 12)
    {
        string text = Lang.Desc(d);
        while (size > min && BodyRegular.GetMultilineStringSize(text, HorizontalAlignment.Left, box.X, size).Y * 1.12f > box.Y) size--;
        var rt = new RichTextLabel { BbcodeEnabled = true, Text = LabText.Card(text, light), Position = pos, Size = box, ScrollActive = false, MouseFilter = MouseFilterEnum.Ignore };
        rt.AddThemeFontOverride("normal_font", BodyRegular); rt.AddThemeFontOverride("bold_font", BodyBold);
        rt.AddThemeFontSizeOverride("normal_font_size", size); rt.AddThemeFontSizeOverride("bold_font_size", size);
        rt.AddThemeColorOverride("default_color", light ? Ink : Silver * new Color(1, 1, 1, 0.9f));
        host.AddChild(rt);
        return rt;
    }

    /// The card preview column on the light side (Deck Builder, Side Deck): the card, its name, type line, ATK / DEF
    /// and its text with the keywords marked, down to the bottom of the screen.
    protected void Preview(Control host, CardData d, float pw)
    {
        foreach (var c in host.GetChildren()) c.QueueFree();
        float ph = pw / 0.686f, y = ph + 16;
        Card(host, d, new Vector2(pw, ph) / 2, ph, 0, 0.8f, large: true);
        float nh = Title.GetMultilineStringSize(d.Name.ToUpperInvariant(), HorizontalAlignment.Left, pw, 22).Y;
        Kit.Put(host, d.Name.ToUpperInvariant(), Title, 22, Ink, new Vector2(0, y), new Vector2(pw, nh), wrap: true);
        y += nh + 4;
        string line = d.IsMonster ? $"{Lang.Tr(d.Attribute)} · {Lang.Tr(d.Race)} / {Lang.Tr(d.Frame)} · ★{d.Level}" : $"{Lang.Tr(d.Kind.ToString())} · {Lang.Tr(d.Frame)}";
        Put(host, line, BodyBold, 17, Graphite, new Vector2(0, y), new Vector2(pw, 24)).ClipText = true;
        y += 26;
        if (d.IsMonster) { Put(host, $"{Lang.Tr("ATK")} {d.AtkText}    {Lang.Tr("DEF")} {d.DefText}", BodyBold, 20, Ink, new Vector2(0, y)); y += 32; }
        Hair(host, new Vector2(0, y), pw, GoldDeep with { A = 0.6f });
        y += 10;
        CardText(host, d, new Vector2(0, y), new Vector2(pw, H - 44 - host.Position.Y - y), 19, light: true);
    }

    /// Make any control clickable: hover callback, left click, optional right click.
    protected static void Click(Control c, Action click, Action<bool>? hover = null, Action? right = null, bool release = false)
    {
        c.MouseFilter = MouseFilterEnum.Pass;   // Pass: the mouse wheel still reaches the page underneath
        c.MouseDefaultCursorShape = CursorShape.PointingHand;
        if (hover != null) { c.MouseEntered += () => hover(true); c.MouseExited += () => hover(false); }
        if (right != null) c.SetMeta("rmb", true);   // MenuBase._Input: this right click is not "back"
        Vector2 down = default; bool armed = false;
        c.GuiInput += e =>
        {
            if (e is not InputEventMouseButton mb) return;
            if (mb.ButtonIndex == MouseButton.Right) { if (mb.Pressed) right?.Invoke(); return; }
            if (mb.ButtonIndex != MouseButton.Left) return;
            if (!release) { if (mb.Pressed) click(); return; }
            // `release`: draggable things click when the button comes up without having moved (a drag is not a click)
            if (mb.Pressed) { down = mb.GlobalPosition; armed = true; }
            else if (armed) { armed = false; if (down.DistanceTo(mb.GlobalPosition) < 10) click(); }
        };
    }

    /// A word you can click: dim, gold + diamond when on. Returns its width and a setter.
    protected (float W, Action<bool> Set) Word(Control host, string s, Vector2 at, int size, bool on, Action click, Font? f = null)
    {
        f ??= Title;
        float w = f.GetStringSize(Lang.Tr(s), HorizontalAlignment.Left, -1, Z(size)).X;
        var l = T(host, s, f, size, at, default, HorizontalAlignment.Left, 0.55f);
        var pip = Pip(host, new Vector2(at.X + w / 2, at.Y + size * 1.55f), 7);
        void Set(bool v) { l.Mat().P("lit", v ? 1f : 0f); pip.Visible = v; }
        Set(on);
        Click(l, click, h => { if (!pip.Visible) l.Mat().P("dim", h ? 0.9f : 0.55f); });
        return (w, Set);
    }

    /// A button (Menu Lab pick 2026-11-16, replacing the bare word with a gold line under it: players didn't see it was
    /// a button): a soft capsule with a gold arrow badge on the right. On the light side a white capsule with an ink
    /// rim, on the dark side a smoked one; the main action of a screen (`primary`, by default any button of size 28
    /// and up) is solid: ink with gold text on the light side, gold with ink text on the dark side.
    protected Control Button(Control host, string s, Vector2 centre, Action click, int size = 26, bool? primary = null)
    {
        bool main = primary ?? size >= 28;
        bool dark = false;
        for (Node? n = host; n != null; n = n.GetParent()) if (n.HasMeta("dark")) { dark = true; break; }
        var at = (host.IsInsideTree() ? host.GlobalPosition : host.Position) + centre;
        bool light = !dark && OnLight(at);
        int fs = Z(Math.Min(size, 26));
        float tw = Title.GetStringSize(Lang.Tr(s), HorizontalAlignment.Left, -1, fs).X;
        float h = MathF.Round(fs * 2.1f + (main ? 6 : 0)), w = tw + h * 2.0f;
        var root = new Control { Position = centre - new Vector2(w, h) / 2, Size = new Vector2(w, h), PivotOffset = new Vector2(w, h) / 2 };
        host.AddChild(root);
        bool hov = false;
        var face = new Control { Size = root.Size, MouseFilter = MouseFilterEnum.Ignore };
        root.AddChild(face);
        face.Draw += () => Capsule(face, w, h, light, main, hov);
        var l = Put(root, s, Title, Math.Min(size, 26), Colors.White, new Vector2(-h * 0.4f, Z(Math.Min(size, 26)) * 0.12f), new Vector2(w, h), HorizontalAlignment.Center);
        l.VerticalAlignment = VerticalAlignment.Center;
        void Paint() => l.AddThemeColorOverride("font_color", main ? (light && !hov ? GoldLight : Ink) : hov ? (light ? GoldDeep : GoldLight) : light ? Ink : Silver);
        Paint();
        Click(root, () =>
        {
            UiSound(true);
            var t = CreateTween();
            t.TweenProperty(root, "scale", new Vector2(0.96f, 0.96f), 0.06);
            t.TweenProperty(root, "scale", Vector2.One, 0.1);
            t.TweenCallback(Callable.From(click));
        }, on =>
        {
            if (on) UiSound(false);
            hov = on; Paint(); face.QueueRedraw();
            Ink_.Only(this, root).TweenProperty(root, "scale", on ? new Vector2(1.03f, 1.03f) : Vector2.One, 0.14).SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
        });
        return root;
    }

    /// Lays buttons out left to right from `start` (each button's top-left), wrapping to a new row below when the next one
    /// would pass `maxX`: translated labels run longer, so fixed button positions overlapped (2026-11-17).
    protected static void Flow(Vector2 start, float maxX, params Control[] buttons)
    {
        var at = start;
        foreach (var b in buttons)
        {
            if (at.X > start.X && at.X + b.Size.X > maxX) at = new Vector2(start.X, at.Y + b.Size.Y + 14);
            b.Position = at;
            at.X += b.Size.X + 18;
        }
    }

    /// A rounded box (soft shadow optional) for capsules and panels.
    public static StyleBoxFlat Box(Color bg, Color edge, float border, int radius, float shadow = 0, Color sh = default, Vector2 shOff = default)
    {
        var b = new StyleBoxFlat { BgColor = bg, BorderColor = edge, AntiAliasing = true, CornerDetail = 8, ShadowSize = (int)shadow, ShadowColor = sh, ShadowOffset = shOff };
        b.SetBorderWidthAll((int)border); b.SetCornerRadiusAll(radius);
        return b;
    }

    /// The capsule face of Button: body, rim, gold badge with a chevron (it slides a little on hover).
    static void Capsule(Control c, float w, float h, bool light, bool main, bool hov)
    {
        Color gold = light ? GoldDeep : GoldLight;
        Color fill = main ? (light ? (hov ? GoldC : Ink) : hov ? GoldLight : GoldC) : hov ? GoldC with { A = light ? 0.18f : 0.22f } : light ? new Color(1, 1, 1, 0.65f) : new Color(0, 0, 0, 0.4f);
        var rim = main ? GoldC : (light ? Ink : Silver) with { A = hov ? 0.6f : 0.3f };
        c.DrawStyleBox(Box(fill, rim, 1.5f, (int)(h / 2), hov ? 14 : 6, new Color(0, 0, 0, light ? 0.12f : 0.4f), new Vector2(0, 3)), new Rect2(0, 0, w, h));
        var cc = new Vector2(w - h / 2, h / 2);
        c.DrawCircle(cc, h * 0.34f, main ? (light && !hov ? GoldC : Ink) : hov ? GoldC : gold);
        float a = h * 0.1f, dx = hov ? 2 : 0;
        var arrow = main ? (light && !hov ? Ink : GoldLight) : hov ? Ink : Colors.White;
        c.DrawPolyline(new[] { cc + new Vector2(-a * 0.6f + dx, -a), cc + new Vector2(a * 0.6f + dx, 0), cc + new Vector2(-a * 0.6f + dx, a) }, arrow, 2.5f, true);
    }

    /// A name box across the bottom (rename a deck, name a replay): Enter or OK keeps a non-empty name, Esc / CANCEL drops it.
    protected void NamePrompt(string title, string start, Action<string> done)
    {
        var box = new Control { Size = new Vector2(W, 170), Position = new Vector2(0, H - 170), ZIndex = 40 };
        box.SetMeta("dark", true);
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
        Darken(box);
        e.GrabFocus();
    }

    /// Back link, top left: "‹  NAME".
    protected void Back(string to = "MENU")
    {
        var l = T(this, "‹  " + Lang.Tr(to), Title, 18, new Vector2(62, 30), default, HorizontalAlignment.Left, 0.7f);
        Click(l, OnBack, h => l.Mat().P("lit", h ? 1f : 0f));
        // on top of whatever the screen adds after it: a full-size layer over it (the Deck Builder's drop area)
        // swallowed its clicks (playtest 2026-11-12)
        Callable.From(() => { if (IsInstanceValid(l)) MoveChild(l, -1); }).CallDeferred();
    }

    /// Pages of items (no scroll bars anywhere): `draw(i, host, at)` places item i at `at`. The mouse wheel or
    /// "‹ PAGE n / m ›" turn pages; `label` is where that line goes. Returns a redraw action (keeps the page).
    protected Action Paged(Control parent, Rect2 area, int count, int cols, int rows, Vector2 cell, Action<int, Control, Vector2> draw, Vector2 label)
    {
        int per = Math.Max(1, cols * rows), page = 0;
        var root = new Control { Position = area.Position, Size = area.Size, MouseFilter = MouseFilterEnum.Stop };
        parent.AddChild(root);
        Action redraw = null!;
        void Turn(int d)
        {
            int pages = Math.Max(1, (count + per - 1) / per);
            int np = Math.Clamp(page + d, 0, pages - 1);
            if (np != page) { page = np; redraw(); UiSound(false); }
        }
        var setPage = Pager(parent, label, Turn, !OnLight(label));
        redraw = () =>
        {
            foreach (var c in root.GetChildren()) c.QueueFree();
            int pages = Math.Max(1, (count + per - 1) / per);
            page = Math.Clamp(page, 0, pages - 1);
            setPage(page, pages);
            for (int k = 0; k < per && page * per + k < count; k++)
                draw(page * per + k, root, new Vector2(k % cols * cell.X, k / cols * cell.Y));
        };
        root.GuiInput += e => { if (e is InputEventMouseButton { Pressed: true } mb && mb.ButtonIndex is MouseButton.WheelDown or MouseButton.WheelUp) Turn(mb.ButtonIndex == MouseButton.WheelDown ? 1 : -1); };
        redraw();
        return redraw;
    }

    /// "‹  PAGE 1 / 3  ›" (260 wide): bold text between two big gold arrows you click; an arrow fades at the first /
    /// last page; hidden when there is only one page. Returns the setter (page, pages).
    protected static Action<int, int> Pager(Control host, Vector2 at, Action<int> turn, bool dark)
    {
        var root = new Control { Position = at, Size = new Vector2(260, 30), MouseFilter = MouseFilterEnum.Ignore };
        host.AddChild(root);
        var gold = dark ? GoldLight : GoldDeep;
        // the right arrow is the next page in every language (user 2026-11-17). Arabic letters sit lower in the line
        // than Latin capitals, so there the arrows sit lower too, level with the word's middle
        float ay = Lang.Code == "ar" ? 3 : -3;
        var left = Put(root, "‹", Title, 34, gold, new Vector2(0, ay), new Vector2(44, 48), HorizontalAlignment.Center);
        var text = Put(root, "", Title, 17, dark ? Silver : Ink, new Vector2(0, 2), new Vector2(260, 24), HorizontalAlignment.Center);
        var right = Put(root, "›", Title, 34, gold, new Vector2(216, ay), new Vector2(44, 48), HorizontalAlignment.Center);
        left.TextDirection = right.TextDirection = TextDirection.Ltr;   // Arabic mirrors ‹ › (they pointed inward)
        var (prev, next) = (left, right);
        Click(prev, () => turn(-1)); Click(next, () => turn(1));
        return (page, pages) =>
        {
            root.Visible = pages > 1;
            text.Text = Lang.F("PAGE {0} / {1}", page + 1, pages);
            // the arrows hug the text (PÁGINA is longer than PAGE), centred on the same spot
            float tw = Math.Max(90, text.GetMinimumSize().X);
            left.Position = new Vector2(130 - tw / 2 - 48, left.Position.Y); right.Position = new Vector2(130 + tw / 2 + 4, right.Position.Y);
            prev.Modulate = new Color(1, 1, 1, page > 0 ? 1 : 0.25f); next.Modulate = new Color(1, 1, 1, page < pages - 1 ? 1 : 0.25f);
        };
    }

    /// A one-line message with up to two choices, centred at the bottom of the screen (replaces the old toasts).
    protected Control? toast;
    protected Label? toastText;   // the message line of the current toast
    protected bool toastKeep;     // a toast with no choices that stays (a download in progress)
    protected void Ask(string text, params (string Label, Action Do)[] choices)
    {
        CloseToast();
        toast = new Control { Size = new Vector2(W, 150), Position = new Vector2(0, H - 150), ZIndex = 40 };
        toast.SetMeta("dark", true);
        AddChild(toast);
        toast.AddChild(new ColorRect { Size = toast.Size, Color = new Color(0.02f, 0.02f, 0.03f, 0.92f), MouseFilter = MouseFilterEnum.Stop });
        Hair(toast, Vector2.Zero, W, GoldC with { A = 0.6f });
        toastText = Put(toast, text, Body, 24, Silver, new Vector2(80, 30), new Vector2(W - 160, 36), HorizontalAlignment.Center);
        float x = W / 2 - (choices.Length - 1) * 160;
        foreach (var (label, act) in choices) { Button(toast, label, new Vector2(x, 100), act, 22); x += 320; }
        Darken(toast);
        var mine = toast;
        if (choices.Length == 0) GetTree().CreateTimer(1.6).Timeout += () => { if (IsInstanceValid(this) && !toastKeep && toast == mine) CloseToast(); };
    }
    protected void CloseToast() { toast?.QueueFree(); toast = null; toastKeep = false; }

    /// Two-tone text inside a dark bar (toasts, name prompts) keeps the dark-side colours wherever the rift runs.
    static void Darken(Node n)
    {
        if (n is CanvasItem { Material: ShaderMaterial m } && m.Shader?.Code?.Contains("uniform float dark") == true) m.SetShaderParameter("dark", 1f);
        foreach (var c in n.GetChildren()) Darken(c);
    }

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
