using Godot;
using PoC.Engine;

namespace PoC.Game.UI;

/// Mockups for rival select in the main menu's ink/paper language (not wired into the game).
/// `-- --demo --variant=a|b|c|d`
/// a = ink columns (focused column floods with ink), b = spotlight + ink list, c = dark ink with paper splats,
/// d = paper slips with hanko seals.
public partial class RivalSamples : MenuBase
{
    /// Ink that bleeds in along ink_field: `progress` 0 = none, 1 = full. tex_mix 1 shows the texture, 0 the ink colour.
    const string Bleed = @"shader_type canvas_item;
uniform sampler2D field : filter_linear, repeat_enable;
uniform float progress = 0.0;
uniform vec4 ink : source_color = vec4(0.05, 0.04, 0.05, 1.0);
uniform float tex_mix = 0.0;
uniform vec2 seed = vec2(0.0);
uniform float from_bottom = 0.0;
void fragment() {
  vec4 t = texture(TEXTURE, UV);
  float f = texture(field, UV * 0.55 + seed).r;
  float d = mix(length((UV - 0.5) * 2.0) * 0.8, 1.0 - UV.y, from_bottom);
  float v = d + f * 0.4;
  float thr = progress * 1.2;
  float m = smoothstep(thr + 0.02, thr - 0.02, v);
  float fringe = exp(-pow((v - thr) / 0.02, 2.0)) * 0.5;
  vec3 c = mix(ink.rgb, t.rgb, tex_mix) * (1.0 - fringe * (1.0 - tex_mix));
  COLOR = vec4(c, t.a * m * ink.a);
}";

    /// Portrait washed toward one-colour ink (wash 1) or full colour (wash 0).
    const string Wash = @"shader_type canvas_item;
uniform float wash = 0.0;
uniform vec4 ink : source_color = vec4(0.08, 0.07, 0.08, 1.0);
uniform vec4 paper : source_color = vec4(0.86, 0.82, 0.74, 1.0);
void fragment() {
  vec4 t = texture(TEXTURE, UV);
  float l = smoothstep(0.1, 0.95, dot(t.rgb, vec3(0.3, 0.59, 0.11)));
  COLOR = vec4(mix(t.rgb, mix(ink.rgb, paper.rgb, l), wash), t.a * COLOR.a);
}";

    static readonly Color InkCol = new(0.05f, 0.04f, 0.05f), Cream = new(0.9f, 0.87f, 0.8f), Seal = new(0.72f, 0.1f, 0.08f);
    /// Face centre (fraction of the portrait) for the round medallions.
    static readonly Vector2[] Face = { new(0.49f, 0.38f), new(0.5f, 0.24f), new(0.43f, 0.22f), new(0.43f, 0.26f) };

    float W, H;
    string variant = "a";
    Texture2D field = null!;
    readonly List<Action<bool>> lights = new();
    Action<int>? onFocus;

    protected override string? MusicPath => Md("solo_gate");
    protected override int ItemCount => RivalPanels.All.Length;
    protected override void OnFocus(int old, int now)
    {
        if (old >= 0 && old < lights.Count) lights[old](false);
        if (now >= 0 && now < lights.Count) lights[now](true);
        if (now >= 0) onFocus?.Invoke(now);
    }
    protected override void OnChoose(int i) { }
    protected override void OnBack() => GetTree().Quit();

    protected override void Build()
    {
        Session.RollPicks();
        W = GetViewportRect().Size.X; H = GetViewportRect().Size.Y;
        field = Tex("res://assets/title/ink_field.png");
        variant = Demo.Arg("variant", "a");
        if (variant == "c") Dark(); else Paper();
        switch (variant)
        {
            case "b": Spotlight(); break;
            case "c": Splats(); break;
            case "d": Slips(); break;
            default: Columns(); break;
        }
        GetTree().CreateTimer(1.2).Timeout += () => { if (Focus < 0) SetFocus(0); };
    }

    // ---- shared pieces

    void Paper()
    {
        var bg = Pic("res://assets/title/paper_bg.png", Vector2.Zero, new Vector2(W, H), this);
        bg.StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered;
    }

    void Dark() => AddChild(new ColorRect
    {
        AnchorRight = 1, AnchorBottom = 1, MouseFilter = MouseFilterEnum.Ignore,
        Material = Mat(@"shader_type canvas_item;
uniform sampler2D field : filter_linear;
void fragment() {
  float f = texture(field, UV).r;
  vec3 c = mix(vec3(0.035, 0.03, 0.04), vec3(0.09, 0.08, 0.09), f * f);
  c *= 1.0 - 0.45 * length((UV - 0.5) * vec2(1.3, 1.0));
  COLOR = vec4(c, 1.0);
}", ("field", field)),
    });

    TextureRect Pic(string path, Vector2 pos, Vector2 size, Node parent) => Pic(Tex(path), pos, size, parent);

    static TextureRect Pic(Texture2D tex, Vector2 pos, Vector2 size, Node parent)
    {
        var r = new TextureRect { Texture = tex, ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered, Position = pos, Size = size, MouseFilter = MouseFilterEnum.Ignore };
        parent.AddChild(r);
        return r;
    }

    ShaderMaterial BleedMat(Color ink, float progress, int seed, bool tex = false, bool bottom = false) =>
        Mat(Bleed, ("field", field), ("progress", progress), ("ink", ink), ("tex_mix", tex ? 1f : 0f), ("seed", new Vector2(seed * 0.37f, seed * 0.21f)), ("from_bottom", bottom ? 1f : 0f));

    void Anim(ShaderMaterial m, string p, float to, double t = 0.45) =>
        CreateTween().TweenMethod(Callable.From<float>(v => m.SetShaderParameter(p, v)), (float)m.GetShaderParameter(p), to, t).SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);

    /// Logo top left, "CHOOSE YOUR RIVAL" beside it, BACK bottom left.
    void Header(bool dark)
    {
        var ink = dark ? Cream : InkCol;
        var logo = Tex(dark ? "res://assets/title/logo_dark.png" : "res://assets/title/logo_light.png");
        float lw = 420, lh = lw * logo.GetHeight() / logo.GetWidth();
        Pic(logo, new Vector2(50, 30), new Vector2(lw, lh), this);
        var t = Kit.Put(this, "CHOOSE YOUR RIVAL", Kit.Cond, 64, ink, new Vector2(lw + 110, 30 + lh / 2 - 44));
        Brush(new Vector2(lw + 100, 30 + lh / 2 + 34), new Vector2(520, 16), ink * new Color(1, 1, 1, 0.55f), this);
        var back = Kit.Put(this, "‹  BACK", Kit.Cond, 40, ink * new Color(1, 1, 1, 0.75f), new Vector2(W - 230, 60));
        back.MouseFilter = MouseFilterEnum.Stop;
        back.GuiInput += e => { if (e is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left }) OnBack(); };
    }

    /// Seven brush dabs (filled up to the level) and the tier word; on paper the tier colours are deepened.
    Control Pips(Control parent, Vector2 pos, OpponentPick pick, bool dark, float scale = 1f)
    {
        var root = new Control { Position = pos, Scale = new Vector2(scale, scale), MouseFilter = MouseFilterEnum.Ignore };
        var tier = dark ? Session.TierColor(pick.Level) : Session.TierColor(pick.Level).Darkened(0.4f);
        var empty = (dark ? Cream : InkCol) * new Color(1, 1, 1, 0.16f);
        for (int i = 0; i < 7; i++)
        {
            Brush(new Vector2(i * 34, (i % 2) * 2), new Vector2(30, 16), i < pick.Level ? tier : empty, root);
        }
        Kit.Put(root, pick.Tier, Kit.Cond, 30, tier, new Vector2(7 * 34 + 14, -12));
        parent.AddChild(root);
        return root;
    }

    /// The ink brush stroke in any colour (the texture is black, so modulate alone can't lighten it).
    TextureRect Brush(Vector2 pos, Vector2 size, Color c, Node parent, string path = "res://assets/title/brush_ink.png")
    {
        var b = Pic(path, pos, size, parent);
        b.StretchMode = TextureRect.StretchModeEnum.Scale;
        b.Material = Mat(@"shader_type canvas_item;
uniform vec4 tint : source_color;
void fragment() { COLOR = vec4(tint.rgb, texture(TEXTURE, UV).a * tint.a); }", ("tint", c));
        return b;
    }

    /// Thick ink ring around a round medallion.
    static void Ring(Vector2 pos, float size, Control parent)
    {
        var r = new Panel { Position = pos, Size = new Vector2(size, size), MouseFilter = MouseFilterEnum.Ignore };
        int w = 10, rad = (int)(size / 2);
        r.AddThemeStyleboxOverride("panel", new StyleBoxFlat { DrawCenter = false, BorderColor = InkCol, BorderWidthBottom = w, BorderWidthTop = w, BorderWidthLeft = w, BorderWidthRight = w, CornerRadiusTopLeft = rad, CornerRadiusTopRight = rad, CornerRadiusBottomLeft = rad, CornerRadiusBottomRight = rad, CornerDetail = 24, AntiAliasing = true });
        parent.AddChild(r);
    }

    /// Red hanko seal: a rounded square, tilted, cream text.
    static Panel Hanko(Control parent, string top, string big, Vector2 pos, float size)
    {
        var p = new Panel { Position = pos, Size = new Vector2(size, size), PivotOffset = new Vector2(size, size) / 2, Rotation = -0.12f, MouseFilter = MouseFilterEnum.Ignore };
        p.AddThemeStyleboxOverride("panel", new StyleBoxFlat
        {
            BgColor = Seal, BorderColor = new Color(0.95f, 0.85f, 0.75f, 0.85f), BorderWidthBottom = 3, BorderWidthTop = 3, BorderWidthLeft = 3, BorderWidthRight = 3,
            CornerRadiusTopLeft = 14, CornerRadiusTopRight = 14, CornerRadiusBottomLeft = 14, CornerRadiusBottomRight = 14, ExpandMarginLeft = 0, AntiAliasing = true,
        });
        Kit.Put(p, top, Kit.Cond, (int)(size * 0.2f), Cream, new Vector2(0, size * 0.08f), new Vector2(size, size * 0.25f), HorizontalAlignment.Center);
        Kit.Put(p, big, Kit.Impact, (int)(size * 0.5f), Cream, new Vector2(0, size * 0.28f), new Vector2(size, size * 0.6f), HorizontalAlignment.Center);
        parent.AddChild(p);
        return p;
    }

    /// Round crop of the rival's face.
    static Texture2D FaceTex(int i)
    {
        var t = RivalPanels.Portrait(RivalPanels.All[i]);
        float s = t.GetWidth() * 0.46f;
        var c = Face[i] * new Vector2(t.GetWidth(), t.GetHeight());
        return new AtlasTexture { Atlas = t, Region = new Rect2(c.X - s / 2, Math.Max(0, c.Y - s / 2), s, s) };
    }

    static Font Gothic => gothic ??= new SystemFont { FontNames = new[] { "Old English Text MT" }, Antialiasing = TextServer.FontAntialiasing.Gray, Oversampling = 2f };
    static Font? gothic;

    static string Title(string n) => n[..1] + n[1..].ToLowerInvariant();

    // ---- a: ink columns

    void Columns()
    {
        Header(false);
        int n = RivalPanels.All.Length;
        float x0 = 90, top = 270, colW = (W - x0 * 2) / n, floodH = H - top - 300;
        for (int i = 0; i < n; i++)
        {
            int k = i;
            var r = RivalPanels.All[i];
            var col = new Control { Position = new Vector2(x0 + i * colW, top), Size = new Vector2(colW, H - top - 60) };
            AddChild(col);
            var flood = new ColorRect { Position = new Vector2(-colW * 0.2f, -60), Size = new Vector2(colW * 1.4f, floodH + 100), MouseFilter = MouseFilterEnum.Ignore };
            var fm = BleedMat(r.Color.Darkened(0.82f), 0.3f, i + 1);
            flood.Material = fm;
            col.AddChild(flood);
            var tex = RivalPanels.Portrait(r);
            float ph = floodH * 0.98f, pw = ph * tex.GetWidth() / tex.GetHeight();
            var por = Pic(tex, new Vector2((colW - pw) / 2, floodH - ph + 20), new Vector2(pw, ph), col);
            var wm = Mat(Wash, ("wash", 0.9f));
            por.Material = wm;
            var name = Kit.Put(col, r.Name, Kit.Cond, 76, InkCol, new Vector2(0, floodH + 34), new Vector2(colW, 90), HorizontalAlignment.Center);
            name.PivotOffset = new Vector2(colW / 2, 45);
            var pips = Pips(col, new Vector2(colW / 2 - 200, floodH + 150), Session.Picks[i], false);
            pips.Modulate = new Color(1, 1, 1, 0.55f);
            Hook(col, i);
            lights.Add(on =>
            {
                Anim(fm, "progress", on ? 0.72f : 0.3f, on ? 0.7 : 0.35);
                Anim(wm, "wash", on ? 0f : 0.9f, 0.4);
                var tw = CreateTween().SetParallel();
                tw.TweenProperty(name, "scale", on ? new Vector2(1.12f, 1.12f) : Vector2.One, 0.3).SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
                tw.TweenProperty(pips, "modulate:a", on ? 1f : 0.55f, 0.25);
                tw.TweenProperty(por, "position:y", floodH - ph + (on ? -10 : 20), 0.4).SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
            });
        }
    }

    // ---- b: spotlight + ink list

    void Spotlight()
    {
        Header(false);
        var stage = new Control { Size = new Vector2(W, H), MouseFilter = MouseFilterEnum.Ignore };
        AddChild(stage);
        float lx = W * 0.64f, rowH = 205, ly = 290;
        for (int i = 0; i < RivalPanels.All.Length; i++)
        {
            var r = RivalPanels.All[i];
            var row = new Control { Position = new Vector2(lx, ly + i * rowH), Size = new Vector2(W - lx - 80, rowH - 20) };
            AddChild(row);
            var hi = Pic("res://assets/title/brush_ink.png", new Vector2(60, 20), new Vector2(row.Size.X - 20, 100), row);
            hi.StretchMode = TextureRect.StretchModeEnum.Scale; hi.Scale = new Vector2(0, 1);
            const float A = 160;
            var face = new Panel { Position = new Vector2(0, (row.Size.Y - A) / 2), Size = new Vector2(A, A), ClipChildren = ClipChildrenMode.AndDraw, MouseFilter = MouseFilterEnum.Ignore };
            face.AddThemeStyleboxOverride("panel", new StyleBoxFlat { BgColor = Cream, CornerRadiusTopLeft = 80, CornerRadiusTopRight = 80, CornerRadiusBottomLeft = 80, CornerRadiusBottomRight = 80, CornerDetail = 16, AntiAliasing = true });
            var fm = Mat(Wash, ("wash", 0.85f));
            Pic(FaceTex(i), Vector2.Zero, new Vector2(A, A), face).Material = fm;
            row.AddChild(face);
            Ring(face.Position - new Vector2(10, 10), A + 20, row);
            var name = Kit.Put(row, r.Name, Kit.Cond, 60, InkCol, new Vector2(A + 60, 26), new Vector2(400, 80));
            var pips = Pips(row, new Vector2(A + 64, 122), Session.Picks[i], false, 0.8f);
            Hook(row, i);
            lights.Add(on =>
            {
                var tw = CreateTween().SetParallel();
                tw.TweenProperty(hi, "scale:x", on ? 1f : 0f, on ? 0.28 : 0.18).SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
                tw.TweenProperty(name, "theme_override_colors/font_color", on ? Cream : InkCol, 0.18);
                tw.TweenProperty(row, "position:x", lx + (on ? -24 : 0), 0.25).SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
                Anim(fm, "wash", on ? 0f : 0.85f, 0.3);
                pips.Visible = !on;
            });
        }
        onFocus = i => Stage(stage, i);
    }

    /// The focused rival large on the left: an ink splat bleeds in, the portrait bleeds out of it.
    void Stage(Control stage, int i)
    {
        foreach (var c in stage.GetChildren()) { var old = (CanvasItem)c; var tw = CreateTween(); tw.TweenProperty(old, "modulate:a", 0f, 0.2); tw.TweenCallback(Callable.From(old.QueueFree)); }
        var r = RivalPanels.All[i];
        var root = new Control { Size = stage.Size, MouseFilter = MouseFilterEnum.Ignore };
        stage.AddChild(root);
        float sz = H * 0.92f;
        var splat = Pic("res://assets/title/ink_splash.png", new Vector2(W * 0.42f - sz / 2, H * 0.56f - sz / 2), new Vector2(sz, sz), root);
        var sm = BleedMat(r.Color.Darkened(0.78f), 0f, i + 3, tex: false);
        splat.Material = sm;
        Anim(sm, "progress", 1.1f, 0.8);
        var tex = RivalPanels.Portrait(r);
        float ph = H * 0.74f, pw = ph * tex.GetWidth() / tex.GetHeight();
        var por = Pic(tex, new Vector2(W * 0.44f - pw / 2, H - ph + 10), new Vector2(pw, ph), root);
        var pm = BleedMat(Colors.White, 0f, i + 7, tex: true, bottom: true);
        por.Material = pm;
        CreateTween().TweenInterval(0.12);
        Anim(pm, "progress", 1.15f, 0.9);
        var name = Kit.Put(root, Title(r.Name), Gothic, 170, InkCol, new Vector2(80, H * 0.34f), new Vector2(700, 200));
        name.Modulate = new Color(1, 1, 1, 0);
        CreateTween().TweenProperty(name, "modulate:a", 1f, 0.4).SetDelay(0.2);
        var pick = Session.Picks[i];
        Pips(root, new Vector2(96, H * 0.34f + 210), pick, false);
        Kit.Put(root, $"LEVEL {pick.Level}", Kit.Cond, 34, InkCol * new Color(1, 1, 1, 0.6f), new Vector2(96, H * 0.34f + 250));
        // Signature card in an ink ring.
        const float A = 230;
        var card = new Panel { Position = new Vector2(110, H * 0.34f + 330), Size = new Vector2(A, A), ClipChildren = ClipChildrenMode.AndDraw, MouseFilter = MouseFilterEnum.Ignore };
        card.AddThemeStyleboxOverride("panel", new StyleBoxFlat { BgColor = Cream, CornerRadiusTopLeft = 115, CornerRadiusTopRight = 115, CornerRadiusBottomLeft = 115, CornerRadiusBottomRight = 115, CornerDetail = 16, AntiAliasing = true });
        var art = Pic(RivalPanels.Art(r), Vector2.Zero, new Vector2(A, A), card); art.StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered;
        root.AddChild(card);
        Ring(card.Position - new Vector2(12, 12), A + 24, root);
    }

    // ---- c: dark ink, paper splats

    void Splats()
    {
        Header(true);
        int n = RivalPanels.All.Length;
        float colW = (W - 160) / n, baseY = H - 260;
        for (int i = 0; i < n; i++)
        {
            var r = RivalPanels.All[i];
            var col = new Control { Position = new Vector2(80 + i * colW, 260), Size = new Vector2(colW, baseY - 260 + 200) };
            AddChild(col);
            float sz = colW * 1.5f;
            var splat = Pic("res://assets/title/ink_splash.png", new Vector2((colW - sz) / 2, col.Size.Y * 0.42f - sz / 2), new Vector2(sz, sz), col);
            splat.PivotOffset = new Vector2(sz, sz) / 2;
            var sm = BleedMat(Cream, 0.55f, i + 2);
            splat.Material = sm;
            var tex = RivalPanels.Portrait(r);
            float ph = (baseY - 260) * 0.98f, pw = ph * tex.GetWidth() / tex.GetHeight();
            var por = Pic(tex, new Vector2((colW - pw) / 2, baseY - 260 - ph), new Vector2(pw, ph), col);
            var wm = Mat(Wash, ("wash", 0.75f), ("ink", new Color(0.03f, 0.03f, 0.04f)), ("paper", new Color(0.45f, 0.42f, 0.4f)));
            por.Material = wm;
            Brush(new Vector2(colW * 0.1f, baseY - 262), new Vector2(colW * 0.8f, 20), Cream * new Color(1, 1, 1, 0.5f), col);
            var name = Kit.Put(col, r.Name, Kit.Cond, 72, Cream * new Color(1, 1, 1, 0.6f), new Vector2(0, baseY - 230), new Vector2(colW, 90), HorizontalAlignment.Center);
            var pips = InkBar(col, colW / 2, baseY - 120, Session.Picks[i]);
            Hook(col, i);
            lights.Add(on =>
            {
                Anim(sm, "progress", on ? 1.1f : 0.55f, on ? 0.6 : 0.3);
                Anim(wm, "wash", on ? 0f : 0.75f, 0.35);
                var tw = CreateTween().SetParallel();
                tw.TweenProperty(name, "theme_override_colors/font_color", on ? Cream : Cream * new Color(1, 1, 1, 0.6f), 0.2);
                tw.TweenProperty(pips, "modulate:a", on ? 1f : 0.5f, 0.2);
            });
            pips.Modulate = new Color(1, 1, 1, 0.5f);
        }
    }

    /// Difficulty under a rival on the dark sheet, `--bar=stroke|drops|word`.
    Control InkBar(Control parent, float cx, float y, OpponentPick pick)
    {
        var root = new Control { Position = new Vector2(cx, y), MouseFilter = MouseFilterEnum.Ignore };
        parent.AddChild(root);
        var tier = Session.TierColor(pick.Level);
        var faint = Cream * new Color(1, 1, 1, 0.13f);
        switch (Demo.Arg("bar", "stroke"))
        {
            case "drops":
                // Seven ink drops, the level's worth in the tier colour.
                var rng = new RandomNumberGenerator { Seed = 5 };
                for (int i = 0; i < 7; i++)
                {
                    float s = 40 + rng.Randf() * 8;
                    var d = Brush(new Vector2(-170 + i * 44 - s / 2, -s / 2 + (i % 2) * 4), new Vector2(s, s), i < pick.Level ? tier : faint, root, "res://assets/title/ink_splash.png");
                    d.PivotOffset = new Vector2(s, s) / 2; d.Rotation = rng.Randf() * 6.28f;
                }
                Kit.Put(root, pick.Tier, Kit.Cond, 34, tier, new Vector2(-200, 30), new Vector2(400, 44), HorizontalAlignment.Center);
                break;
            case "word":
                // The tier written on a brush stroke of its colour, like the menu's focus stroke.
                Brush(new Vector2(-170, -34), new Vector2(340, 72), tier, root);
                Kit.Put(root, pick.Tier, Kit.Cond, 44, new Color(0.06f, 0.05f, 0.06f), new Vector2(-170, -24), new Vector2(340, 60), HorizontalAlignment.Center);
                Kit.Put(root, $"LEVEL {pick.Level}", Kit.Cond, 26, Cream * new Color(1, 1, 1, 0.5f), new Vector2(-170, 40), new Vector2(340, 34), HorizontalAlignment.Center);
                break;
            default:
                // One long stroke, inked in up to the level.
                const float bw = 380, bh = 30;
                Brush(new Vector2(-bw / 2, -bh / 2), new Vector2(bw, bh), faint, root);
                var clip = new Control { Position = new Vector2(-bw / 2, -bh), Size = new Vector2(bw * pick.Level / 7f, bh * 2), ClipContents = true, MouseFilter = MouseFilterEnum.Ignore };
                root.AddChild(clip);
                Brush(new Vector2(0, bh / 2), new Vector2(bw, bh), tier, clip);
                Kit.Put(root, $"{pick.Tier}  ·  LV {pick.Level}", Kit.Cond, 32, tier, new Vector2(-200, 26), new Vector2(400, 44), HorizontalAlignment.Center);
                break;
        }
        return root;
    }

    // ---- d: paper slips with hanko seals

    void Slips()
    {
        Header(false);
        int n = RivalPanels.All.Length;
        float gap = 50, cw = (W - 220 - gap * (n - 1)) / n, ch = H - 470, top = 290;
        for (int i = 0; i < n; i++)
        {
            var r = RivalPanels.All[i];
            float rot = (i % 2 == 0 ? -1 : 1) * 0.025f;
            var home = new Vector2(110 + i * (cw + gap), top);
            var slip = new Panel { Position = home, Size = new Vector2(cw, ch), PivotOffset = new Vector2(cw / 2, ch), Rotation = rot, ClipChildren = ClipChildrenMode.AndDraw };
            slip.AddThemeStyleboxOverride("panel", new StyleBoxFlat
            {
                BgColor = new Color(0.95f, 0.93f, 0.88f), CornerRadiusTopLeft = 22, CornerRadiusTopRight = 22, CornerRadiusBottomLeft = 22, CornerRadiusBottomRight = 22, CornerDetail = 10, AntiAliasing = true,
            });
            AddChild(slip);
            var shadow = new Panel { Position = home + new Vector2(10, 14), Size = slip.Size, PivotOffset = slip.PivotOffset, Rotation = rot, MouseFilter = MouseFilterEnum.Ignore };
            shadow.AddThemeStyleboxOverride("panel", new StyleBoxFlat { BgColor = new Color(0, 0, 0, 0.18f), CornerRadiusTopLeft = 22, CornerRadiusTopRight = 22, CornerRadiusBottomLeft = 22, CornerRadiusBottomRight = 22 });
            AddChild(shadow); MoveChild(shadow, slip.GetIndex());
            var flood = new ColorRect { Size = slip.Size, MouseFilter = MouseFilterEnum.Ignore };
            var fm = BleedMat(r.Color.Darkened(0.55f), 0.2f, i + 5, bottom: true);
            flood.Material = fm;
            slip.AddChild(flood);
            var tex = RivalPanels.Portrait(r);
            float ph = ch * 0.8f, pw = ph * tex.GetWidth() / tex.GetHeight();
            Pic(tex, new Vector2((cw - pw) / 2, ch - ph), new Vector2(pw, ph), slip);
            var name = Kit.Put(slip, Title(r.Name), Gothic, 92, InkCol, new Vector2(30, 14), new Vector2(cw - 60, 120));
            var frame = new Panel { Size = slip.Size, MouseFilter = MouseFilterEnum.Ignore };
            frame.AddThemeStyleboxOverride("panel", new StyleBoxFlat
            {
                DrawCenter = false, BorderColor = InkCol, BorderWidthBottom = 5, BorderWidthTop = 5, BorderWidthLeft = 5, BorderWidthRight = 5,
                CornerRadiusTopLeft = 22, CornerRadiusTopRight = 22, CornerRadiusBottomLeft = 22, CornerRadiusBottomRight = 22, CornerDetail = 10, AntiAliasing = true,
            });
            slip.AddChild(frame);
            var pick = Session.Picks[i];
            var seal = Hanko(slip, pick.Tier, pick.Level.ToString(), new Vector2(cw - 150, ch - 160), 120);
            Hook(slip, i);
            lights.Add(on =>
            {
                Anim(fm, "progress", on ? 1.1f : 0.2f, on ? 0.7 : 0.35);
                var tw = CreateTween().SetParallel();
                foreach (var c in new Control[] { slip, shadow })
                {
                    tw.TweenProperty(c, "position:y", home.Y + (on ? -36 : 0), 0.35).SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
                    tw.TweenProperty(c, "rotation", on ? 0f : rot, 0.35);
                    tw.TweenProperty(c, "scale", on ? new Vector2(1.04f, 1.04f) : Vector2.One, 0.35).SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
                }
                tw.TweenProperty(name, "theme_override_colors/font_color", on ? Cream : InkCol, 0.3);
                tw.TweenProperty(seal, "rotation", on ? -0.2f : -0.12f, 0.3);
            });
        }
    }
}
