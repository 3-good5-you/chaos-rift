using Godot;
using PoC.Engine;
using static PoC.Game.UI.ConceptKit;

namespace PoC.Game.UI;

/// Chaos Rift, round 3 (2026-11-08): monochrome colours (variant b) + the floating card (variant d), no hover text,
/// game renamed CHAOS RIFT. `-- --demo [--left=pair|deck|rival] [--logo=1|2|3] [--hover=N]`
/// left: pair = a light-side card answers the dark-side card for every item; deck = your deck's cover card and
/// profile; rival = your next rival washed into the light.
/// logo: 1 silver titling with hairlines, 2 the name split by a glowing crack, 3 CHAOS silver | RIFT gold.
public partial class RiftFinal : MenuBase
{
    static readonly Color LightA = new(0.985f, 0.985f, 0.98f), LightB = new(0.88f, 0.88f, 0.87f), DarkA = new(0.025f, 0.025f, 0.03f), DarkB = new(0.09f, 0.09f, 0.11f),
        HaloL = new(0.85f, 0.75f, 0.55f), HaloD = new(0.7f, 0.72f, 0.8f), InkC = new(0.06f, 0.06f, 0.07f), CreamC = new(0.95f, 0.95f, 0.96f), Gold2 = new(0.78f, 0.62f, 0.32f);
    const float Top = 0.58f, Bottom = 0.44f;

    /// Light-side / dark-side card per menu item.
    static readonly (string L, string D)[] Pairs =
    {
        ("Dark Magician", "Blue-Eyes White Dragon"), ("Black Luster Soldier - Envoy of the Beginning", "Chaos Emperor Dragon - Envoy of the End"),
        ("Red-Eyes B. Dragon", "Summoned Skull"), ("Dark Magician Girl", "Magician of Black Chaos"), ("Exodia the Forbidden One", "Obelisk the Tormentor"),
        ("Kuriboh", "Winged Kuriboh"), ("Time Wizard", "Thousand-Eyes Restrict"), ("Elemental HERO Neos", "Elemental HERO Flame Wingman"),
    };

    float W, H;
    string left = "pair";
    ShaderMaterial bg = null!;
    Font font = null!, small = null!;
    readonly List<(Label L, ShaderMaterial M, float Y)> items = new();
    ColorRect mark = null!;
    Control? cardR, cardL;
    Shader? inv;

    protected override string? MusicPath => Md("menu_01");
    protected override void OnChoose(int i) { }
    protected override void OnBack() => GetTree().Quit();

    ShaderMaterial InvMat(float dim = 1f) => new ShaderMaterial { Shader = inv ??= new Shader { Code = RiftVariants.Inv } }
        .P("aspect", W / H).P("ink", InkC).P("cream", CreamC).P("accent", Gold2).P("dim", dim);

    protected override void Build()
    {
        Demo.Ensure();
        W = GetViewportRect().Size.X; H = GetViewportRect().Size.Y;
        left = Demo.Arg("left", "pair");
        (font, menuSize) = Demo.Arg("font", "2") switch
        {
            "2" => (new FontVariation { BaseFont = Sys(new[] { "Perpetua Titling MT" }, 700), SpacingGlyph = 8 }, 30),
            "3" => (new FontVariation { BaseFont = Sys(new[] { "Copperplate Gothic Bold", "Copperplate Gothic" }, 700), SpacingGlyph = 6 }, 27),
            "4" => (new FontVariation { BaseFont = Sys(new[] { "Felix Titling" }, 400), SpacingGlyph = 8 }, 32),
            "5" => (new FontVariation { BaseFont = Sys(new[] { "Engravers MT" }, 400), SpacingGlyph = 4 }, 23),
            "0" => (new FontVariation { BaseFont = Sys(new[] { "Gill Sans MT", "Segoe UI" }, 300), SpacingGlyph = 12 }, 31),
            _ => (new FontVariation { BaseFont = Sys(new[] { "Castellar" }, 400), SpacingGlyph = 8 }, 30),
        };
        small = new FontVariation { BaseFont = Sys(new[] { "Gill Sans MT", "Segoe UI" }, 400), SpacingGlyph = 4 };
        var noise = new NoiseTexture2D { Width = 512, Height = 512, Seamless = true, Noise = new FastNoiseLite { Frequency = 0.01f } };
        bg = ShaderRect(this, RiftVariants.Bg, Vector2.Zero, new Vector2(W, H)).Mat().P("aspect", W / H).P("noise_tex", noise)
            .P("light_a", LightA).P("light_b", LightB).P("dark_a", DarkA).P("dark_b", DarkB).P("halo_l", HaloL).P("halo_d", HaloD).P("rays", 0f).P("specks", 0f);

        var logoArg = Demo.Arg("logo", "R0");
        if (logoArg.StartsWith('R')) LogoImage(logoArg);
        else if (left == "pair") Logo(new Vector2(Mathf.Lerp(Top, Bottom, 0.12f) * W, 50), 520, int.Parse(logoArg), split: true);
        else Logo(new Vector2(W - 470, 46), 540, int.Parse(logoArg), split: false);

        mark = new ColorRect { Size = new Vector2(220, 1.5f), MouseFilter = MouseFilterEnum.Ignore, Material = InvMat() };
        AddChild(mark);
        float y0 = left == "pair" ? 0.39f : 0.3f, step = left == "pair" ? 0.068f : 0.072f;
        for (int i = 0; i < Items.Length; i++)
        {
            int k = i;
            float y = y0 + i * step;
            float cx = Mathf.Lerp(Top, Bottom, y) * W;
            var l = Say(this, Items[i].ToUpperInvariant(), font, menuSize, Colors.White, new Vector2(cx - 400, y * H - menuSize * 0.75f), new Vector2(800, menuSize * 1.5f), HorizontalAlignment.Center);
            var m = MenuMat();
            l.Material = m; l.MouseFilter = MouseFilterEnum.Stop; l.MouseEntered += () => SetFocus(k);
            items.Add((l, m, y));
        }

        bool intro = OS.GetCmdlineUserArgs().Contains("--intro");
        if (left == "pair" && !intro) RandomPair();
        else if (left == "deck") DeckSide();
        else if (left == "rival") RivalSide();
        if (left != "deck") Profile(new Vector2(56, H - 112));
        SetFocus(0);
        if (OS.GetCmdlineUserArgs().Contains("--intro")) Intro();
    }

    // ---------------------------------------------------------------- loading intro

    /// Loading intro: a clean black void; the Yu-Gi-Oh! logo in the middle on a soft glow; "Chaos Rift" rises under it;
    /// the screen trembles, the rift tears down in a fraction of a second with sparks racing at its tip, hits with a
    /// white flash + shake + break sound, the light floods the left side, and the logo morphs to the top right.
    void Intro()
    {
        var bgRect = GetChildren().OfType<CanvasItem>().First(c => c.Material == bg);
        var menu = GetChildren().OfType<CanvasItem>().Where(c => c != logoPic && c != namePic && c != bgRect).ToList();
        foreach (var c in menu) c.Modulate = c.Modulate with { A = 0 };
        bg.P("draw", 0f).P("reveal", 0f).P("smoke", 0f);
        float lw = 1040, lh = lw * logoHome.Size.Y / logoHome.Size.X, nw = lw * 0.8f, nh = nw * nameHome.Size.Y / nameHome.Size.X;
        var logoMid = new Rect2(W / 2 - lw / 2, H * 0.42f - lh / 2 - 40, lw, lh);
        var nameMid = new Rect2(W / 2 - nw / 2, logoMid.Position.Y + lh * 0.86f, nw, nh);
        Place(logoPic, logoMid); Place(namePic, nameMid);
        logoPic.Modulate = new Color(1, 1, 1, 0); namePic.Modulate = new Color(1, 1, 1, 0);

        // soft glow behind the logo so it sits in light, not in murk
        var glow = ShaderRect(this, "shader_type canvas_item; void fragment(){ vec2 p = (UV - 0.5) * vec2(1.8, 1.0); float r = length(p); COLOR = vec4(0.62, 0.58, 0.75, exp(-r * r * 5.0) * 0.32); }",
            new Vector2(W / 2 - 900, logoMid.Position.Y + lh * 0.55f - 500), new Vector2(1800, 1000));
        MoveChild(glow, bgRect.GetIndex() + 1);
        glow.Modulate = new Color(1, 1, 1, 0);
        var flash = new ColorRect { Size = new Vector2(W, H), Color = new Color(1, 1, 1, 0), MouseFilter = MouseFilterEnum.Ignore };
        AddChild(flash);
        var sparks = new CpuParticles2D
        {
            Emitting = false, Amount = 140, Lifetime = 0.6, Explosiveness = 0, Direction = Vector2.Up, Spread = 180, Gravity = new Vector2(0, 900),
            InitialVelocityMin = 250, InitialVelocityMax = 650, ScaleAmountMin = 2, ScaleAmountMax = 5, Color = new Color(1f, 0.92f, 1f),
        };
        AddChild(sparks);
        var hit = new AudioStreamPlayer { Stream = GD.Load<AudioStream>("res://assets/audio/md/se/SE_CARD_FINISH_BREAK_01.wav"), VolumeDb = -2, Bus = "Sound" };
        var swoosh = new AudioStreamPlayer { Stream = GD.Load<AudioStream>("res://assets/audio/gen/C_whoosh_gate.wav"), VolumeDb = -6, Bus = "Sound" };
        AddChild(hit); AddChild(swoosh);
        Vector2 CrackAt(float y) => new(Mathf.Lerp(Top, Bottom, y) * W, y * H);
        void Shake(Tween t, float amp, float time, int n)
        {
            for (int k = 0; k < n; k++)
            {
                float a = amp * (1 - (float)k / n);
                t.TweenProperty(this, "position", new Vector2((float)GD.RandRange(-a, a), (float)GD.RandRange(-a, a)), time / n);
            }
            t.TweenProperty(this, "position", Vector2.Zero, time / n);
        }

        var tw = CreateTween();
        // 1 the logo, on its glow
        tw.TweenProperty(glow, "modulate:a", 1f, 0.8);
        tw.Parallel().TweenProperty(logoPic, "modulate:a", 1f, 0.8);
        // 2 the name rises in
        tw.TweenProperty(namePic, "modulate:a", 1f, 0.5);
        tw.Parallel().TweenProperty(namePic, "position:y", nameMid.Position.Y, 0.6).From(nameMid.Position.Y + 30).SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
        tw.TweenInterval(0.35);
        // 3 tremor: the void strains
        Shake(tw, 3, 0.4f, 10);
        // 4 the tear: fast, sparks racing at the tip
        tw.TweenCallback(Callable.From(() => { sparks.Emitting = true; if (!Shooting) hit.Play(); }));
        tw.TweenMethod(Callable.From<float>(v => { bg.P("draw", v); sparks.Position = CrackAt(Mathf.Min(v * 1.15f, 1f)); }), 0f, 1f, 0.22).SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.In);
        // 5 impact: white flash, burst on the crack, hard shake; the light floods in fast
        tw.TweenCallback(Callable.From(() => { sparks.Emitting = false; flash.Color = new Color(1, 1, 1, 0.85f); bg.P("smoke", 1f); }));
        tw.TweenProperty(flash, "color:a", 0f, 0.45).SetTrans(Tween.TransitionType.Expo).SetEase(Tween.EaseType.Out);
        tw.Parallel().TweenMethod(Callable.From<float>(v => bg.P("burst", v)), 1f, 0f, 0.9).SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
        tw.Parallel().TweenMethod(Callable.From<float>(v => bg.P("reveal", v)), 0f, 1f, 0.45).SetTrans(Tween.TransitionType.Expo).SetEase(Tween.EaseType.Out);
        tw.Parallel().TweenProperty(glow, "modulate:a", 0f, 0.4);
        tw.Parallel().TweenCallback(Callable.From(() => { var s2 = CreateTween(); Shake(s2, 14, 0.35f, 9); }));
        // 6 the logo morphs to the top right
        tw.TweenCallback(Callable.From(() => { if (!Shooting) swoosh.Play(); }));
        foreach (var (pic, home) in new[] { (logoPic, logoHome), (namePic, nameHome) })
        {
            tw.Parallel().TweenProperty(pic, "position", home.Position, 0.85).SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.InOut);
            tw.Parallel().TweenProperty(pic, "size", home.Size, 0.85).SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.InOut);
        }
        // 7 cards + menu
        tw.TweenCallback(Callable.From(RandomPair));
        for (int i = 0; i < menu.Count; i++)
            tw.Parallel().TweenProperty(menu[i], "modulate:a", 1f, 0.45).SetDelay(0.05 + i * 0.03);
    }

    int menuSize = 30;
    readonly ColorRect[] pips = new ColorRect[2];

    /// Menu words: graphite on the light side, the logo's silver on the dark side, gold when chosen.
    ShaderMaterial MenuMat() => new ShaderMaterial { Shader = menuShader ??= new Shader { Code = @"shader_type canvas_item;
uniform float lit = 0.0;
" + RiftVariants.CrackFn + @"
void fragment() {
  float a = texture(TEXTURE, UV).a * COLOR.a;
  float d = crack_d(SCREEN_UV, TIME);
  float y = smoothstep(0.3, 0.72, UV.y);
  vec3 graphite = mix(vec3(0.28, 0.28, 0.31), vec3(0.04, 0.04, 0.05), y);
  vec3 silver = mix(vec3(1.0), vec3(0.6, 0.62, 0.68), y) + vec3(0.1) * smoothstep(0.55, 0.6, UV.y) * smoothstep(0.7, 0.6, UV.y);
  vec3 gold = mix(vec3(1.0, 0.9, 0.58), vec3(0.66, 0.44, 0.14), y);
  vec3 c = mix(d < 0.0 ? graphite : silver, d < 0.0 ? vec3(0.6, 0.4, 0.1) * (1.1 - y * 0.4) : gold, lit);
  COLOR = vec4(c, a * mix(0.9, 1.0, lit));
}" } }.P("aspect", W / H);
    Shader? menuShader;

    /// The forged logo (game/tools/riftforge.py R0): Yu-Gi-Oh! logo + "Chaos Rift" in its own gothic chrome, as two
    /// pieces so the loading intro can morph them from the middle of the screen to the top right.
    TextureRect logoPic = null!, namePic = null!;
    Rect2 logoHome, nameHome;
    static TextureRect PicFile(string file, Control host)
    {
        var img = Image.LoadFromFile(ProjectSettings.GlobalizePath($"res://assets/title/rift/{file}.png"));
        img.GenerateMipmaps();
        var r = Ink.Pic(ImageTexture.CreateFromImage(img), Vector2.Zero, new Vector2(img.GetWidth(), img.GetHeight()), host);
        r.StretchMode = TextureRect.StretchModeEnum.Scale;
        r.TextureFilter = TextureFilterEnum.LinearWithMipmaps;
        return r;
    }
    void LogoImage(string key)
    {
        logoPic = PicFile("R0_logo", this);
        namePic = PicFile("R0_name", this);
        float lw = 560, lh = lw * logoPic.Size.Y / logoPic.Size.X;
        float nw = lw * 0.8f, nh = nw * namePic.Size.Y / namePic.Size.X;
        float cx = W - 360, top = 30;
        logoHome = new Rect2(cx - lw / 2, top, lw, lh);
        nameHome = new Rect2(cx - nw / 2, top + lh * 0.86f, nw, nh);
        Place(logoPic, logoHome); Place(namePic, nameHome);
    }
    static void Place(Control c, Rect2 r) { c.Position = r.Position; c.Size = r.Size; }

    /// Two cards drawn at random from the whole collection each time the menu opens; they stay put while you browse.
    void RandomPair()
    {
        var db = CardDb.Shared;
        var pool = db.ByIdx.Values.Where(c => db.Canon(c.Idx) == c.Idx && c.Name.Length > 0 && !c.Name.EndsWith("Token")).GroupBy(c => c.Name).Select(g => g.First()).ToList();
        var rng = new Random(int.TryParse(Demo.Arg("seed"), out var sd) ? sd : System.Environment.TickCount);
        var a = pool[rng.Next(pool.Count)]; var b = pool[rng.Next(pool.Count)];
        cardL = FloatCard(a.Name, new Vector2(W * 0.2f, H * 0.54f), H * 0.56f, -0.055f, true);
        cardR = FloatCard(b.Name, new Vector2(W * 0.8f, H * 0.6f), H * 0.56f, 0.055f, false);
        GD.Print($"menu cards: {a.Name} | {b.Name} (pool {pool.Count})");
    }

    void Profile(Vector2 at)
    {
        var f = Cosmetics.Framed(Settings.Avatar, Session.Profile.Frame, 70);
        f.Position = at; AddChild(f);
        Say(this, Settings.PlayerName.ToUpperInvariant(), font, 22, InkC, at + new Vector2(84, 10));
        Say(this, $"{Session.Profile.Dp:N0} DP", small, 17, InkC * new Color(1, 1, 1, 0.55f), at + new Vector2(85, 42));
    }

    // ---------------------------------------------------------------- logo

    /// The Yu-Gi-Oh! logo (kanji neutral) and CHAOS RIFT under it, centred on `top` (x = centre).
    void Logo(Vector2 top, float w, int style, bool split)
    {
        var src = Tex("res://assets/title/yugioh_logo.png");
        var crop = new AtlasTexture { Atlas = src, Region = new Rect2(40, 400, 3760, 1350) };
        float h = w * 1350 / 3760f;
        var pic = Ink.Pic(crop, new Vector2(top.X - w / 2, top.Y), new Vector2(w, h), this);
        pic.StretchMode = TextureRect.StretchModeEnum.Scale;
        // kanji: soft grey on the dark side, black ink on the light side (switches across the crack when split)
        pic.Material = new ShaderMaterial { Shader = new Shader { Code = @"shader_type canvas_item;
" + RiftVariants.CrackFn + @"
uniform bool split = false;
void fragment() {
  vec4 c = texture(TEXTURE, UV);
  float red = smoothstep(0.25, 0.5, c.r - max(c.g, c.b));
  float onLight = split ? step(crack_d(SCREEN_UV, TIME), 0.0) : 0.0;
  vec3 k = mix(vec3(0.42, 0.42, 0.46), vec3(0.1, 0.1, 0.11), onLight);
  COLOR = vec4(mix(c.rgb, k, red), c.a);
}" } }.P("aspect", W / H).P("split", split);

        float ny = top.Y + h + 4;
        var nameFont = new FontVariation { BaseFont = Sys(new[] { "Perpetua Titling MT", "Castellar" }, style == 2 ? 700 : 300), SpacingGlyph = style == 3 ? 26 : 20 };
        if (style == 3) nameFont = new FontVariation { BaseFont = Sys(new[] { "Gill Sans MT", "Segoe UI" }, 300), SpacingGlyph = 20 };
        int size = style == 2 ? 62 : 56;
        Label Word(string s, float x, float wBox, HorizontalAlignment a) => Say(this, s, nameFont, size, Colors.White, new Vector2(x, ny), new Vector2(wBox, size * 1.3f), a);
        if (split)
        {
            var l = Word("CHAOS RIFT", top.X - 450, 900, HorizontalAlignment.Center);
            l.Material = InvMat();
            Rules(top.X, ny + size * 1.35f, 0.5f, true);
            return;
        }
        if (style == 3)
        {
            var a = Word("CHAOS", top.X - 450, 435, HorizontalAlignment.Right);
            var b = Word("RIFT", top.X + 15, 435, HorizontalAlignment.Left);
            a.Material = Metal(new Color(1, 1, 1), new Color(0.6f, 0.62f, 0.68f));
            b.Material = Metal(new Color(1f, 0.9f, 0.6f), new Color(0.7f, 0.5f, 0.2f));
            Bolt(new Vector2(top.X, ny + 12), size * 0.95f);
            return;
        }
        var n = Word("CHAOS RIFT", top.X - 450, 900, HorizontalAlignment.Center);
        n.Material = Metal(new Color(1, 1, 1), new Color(0.58f, 0.6f, 0.66f));
        if (style == 2) CrackLine(top.X, ny + size * 0.56f, nameFont.GetStringSize("CHAOS RIFT", HorizontalAlignment.Left, -1, size).X + 40);
        if (style == 1) Rules(top.X, ny + size * 1.35f, 0.45f, false);
    }

    /// Chrome-ish vertical gradient with a slow shine.
    ShaderMaterial Metal(Color top, Color bottom) => new ShaderMaterial { Shader = new Shader { Code = @"shader_type canvas_item;
uniform vec4 a : source_color; uniform vec4 b : source_color;
void fragment() {
  float al = texture(TEXTURE, UV).a * COLOR.a;
  float y = UV.y;
  vec3 c = mix(a.rgb, b.rgb, smoothstep(0.25, 0.75, y)) + vec3(0.12) * smoothstep(0.62, 0.7, y) * smoothstep(0.8, 0.7, y);
  c += exp(-pow((SCREEN_UV.x - fract(TIME * 0.07) * 1.6 + 0.3) * 16.0, 2.0)) * 0.3;
  COLOR = vec4(c, al);
}" } }.P("a", top).P("b", bottom);

    /// The name torn by a glowing zigzag: halves pushed apart, light in the gap.
    ShaderMaterial Cracked() => new ShaderMaterial { Shader = new Shader { Code = @"shader_type canvas_item;
float zig(float x) { return 0.52 + (abs(fract(x * 7.0) - 0.5) - 0.25) * 0.28 + sin(x * 31.0) * 0.02; }
void fragment() {
  float z = zig(UV.x) * 0.5 + 0.27;
  float upper = step(UV.y, z);
  vec2 off = upper > 0.5 ? vec2(-0.002, -0.012) : vec2(0.002, 0.012);
  float al = texture(TEXTURE, UV + off).a * COLOR.a;
  float gap = smoothstep(0.012, 0.0, abs(UV.y - z));
  vec3 metal = mix(vec3(1.0), vec3(0.6, 0.62, 0.68), smoothstep(0.2, 0.8, UV.y));
  float inside = texture(TEXTURE, UV).a;
  vec3 glowc = vec3(0.9, 0.85, 1.0);
  COLOR = vec4(mix(metal, glowc * 1.4, gap * inside), max(al, gap * inside * 0.95));
}" } };

    /// A thin glowing zigzag tearing through the name.
    void CrackLine(float cx, float y, float w)
    {
        var rng = new RandomNumberGenerator { Seed = 11 };
        var pts = new List<Vector2>();
        for (float x = -w / 2; x <= w / 2; x += 26) pts.Add(new Vector2(cx + x, y + rng.RandfRange(-9, 9)));
        AddChild(new Line2D { Points = pts.ToArray(), Width = 9, DefaultColor = new Color(0.75f, 0.7f, 1f, 0.25f), JointMode = Line2D.LineJointMode.Sharp });
        AddChild(new Line2D { Points = pts.ToArray(), Width = 2.2f, DefaultColor = Colors.White, JointMode = Line2D.LineJointMode.Sharp,
            Gradient = new Gradient { Offsets = new[] { 0f, 0.15f, 0.85f, 1f }, Colors = new[] { new Color(1, 1, 1, 0), Colors.White, Colors.White, new Color(1, 1, 1, 0) } } });
    }

    /// Hairlines with small diamonds either side of the name.
    void Rules(float cx, float y, float alpha, bool split)
    {
        foreach (var dir in new[] { -1, 1 })
        {
            var line = new ColorRect { Position = new Vector2(dir < 0 ? cx - 330 : cx + 40, y), Size = new Vector2(290, 1.5f), Color = Colors.White * new Color(1, 1, 1, alpha), MouseFilter = MouseFilterEnum.Ignore };
            if (split) line.Material = InvMat(alpha * 1.6f); else line.Color = Gold2 with { A = 0.8f };
            AddChild(line);
        }
        var d = new ColorRect { Position = new Vector2(cx - 6, y - 5), Size = new Vector2(11, 11), Rotation = Mathf.Pi / 4, PivotOffset = new Vector2(5.5f, 5.5f), Color = Gold2, MouseFilter = MouseFilterEnum.Ignore };
        AddChild(d);
    }

    /// A small glowing lightning stroke between CHAOS and RIFT.
    void Bolt(Vector2 at, float h)
    {
        var pts = new[] { new Vector2(6, 0), new Vector2(-4, h * 0.45f), new Vector2(5, h * 0.5f), new Vector2(-6, h) };
        AddChild(new Line2D { Points = pts.Select(p => p + at).ToArray(), Width = 7, DefaultColor = new Color(0.8f, 0.75f, 1f, 0.35f), JointMode = Line2D.LineJointMode.Round });
        AddChild(new Line2D { Points = pts.Select(p => p + at).ToArray(), Width = 2.5f, DefaultColor = Colors.White, JointMode = Line2D.LineJointMode.Round });
    }

    // ---------------------------------------------------------------- the floating cards

    /// A card standing in the air: soft drop shadow (stronger on the light side), slight tilt, fade-rise in.
    Control FloatCard(string name, Vector2 centre, float h, float tilt, bool onLight)
    {
        float w = h * 0.686f;
        var root = new Control { Position = centre - new Vector2(w, h) / 2, Size = new Vector2(w, h), PivotOffset = new Vector2(w, h) / 2, Rotation = tilt, MouseFilter = MouseFilterEnum.Ignore };
        AddChild(root);
        float m = w * 0.45f;   // shadow margin around the card
        var off = onLight ? new Vector2(16, 30) : Vector2.Zero;
        var sh = ShaderRect(root, @"shader_type canvas_item;
uniform float strength = 0.35;
uniform vec4 col : source_color;
uniform vec2 rect;
uniform vec2 half_card;
uniform float margin;
void fragment() {
  vec2 p = abs((UV - 0.5) * rect);
  float d = length(max(p - half_card * 0.94, 0.0));
  float k = exp(-pow(d / (margin * 0.42), 2.0));
  k *= 1.0 - smoothstep(margin * 0.55, margin, d);
  COLOR = vec4(col.rgb, strength * k);
}", new Vector2(-m, -m) + off, new Vector2(w + 2 * m, h + 2 * m));
        sh.Mat().P("strength", onLight ? 0.32f : 0.5f).P("col", onLight ? new Color(0.12f, 0.12f, 0.15f) : HaloD * new Color(0.5f, 0.5f, 0.6f))
            .P("rect", new Vector2(w + 2 * m, h + 2 * m)).P("half_card", new Vector2(w, h) / 2).P("margin", m);
        var c = Ink.Pic(Full(name), Vector2.Zero, new Vector2(w, h), root);
        c.StretchMode = TextureRect.StretchModeEnum.Scale;
        root.Modulate = new Color(1, 1, 1, 0);
        var tw = CreateTween().SetParallel();
        tw.TweenProperty(root, "modulate:a", 1f, 0.35);
        tw.TweenProperty(root, "position:y", root.Position.Y - 12, 0.5).From(root.Position.Y + 22).SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
        return root;
    }

    protected override void OnFocus(int old, int now)
    {
        for (int i = 0; i < items.Count; i++) items[i].M.P("lit", i == now ? 1f : 0f);
        var (l, _, y) = items[now];
        bg.P("focus_y", y);
        mark.Visible = false;
        float w = font.GetStringSize(l.Text, HorizontalAlignment.Left, -1, menuSize).X;
        float cx = Mathf.Lerp(Top, Bottom, y) * W;
        for (int s = 0; s < 2; s++)
        {
            pips[s] ??= AddPip();
            var p = pips[s];
            float px = cx + (s == 0 ? -w / 2 - 30 : w / 2 + 18);
            p.Position = new Vector2(px, y * H - 6);
            p.Scale = Vector2.Zero;
            CreateTween().TweenProperty(p, "scale", Vector2.One, 0.25).SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
        }
        if (left == "pair") return;
        cardR?.QueueFree();
        cardR = FloatCard(Pairs[now].D, new Vector2(W * 0.8f, H * 0.62f), H * 0.56f, 0.055f, false);
    }

    /// A small gold diamond, like the dots on the logo's i and !.
    ColorRect AddPip()
    {
        var p = new ColorRect { Size = new Vector2(12, 12), PivotOffset = new Vector2(6, 6), Rotation = Mathf.Pi / 4, MouseFilter = MouseFilterEnum.Ignore,
            Material = new ShaderMaterial { Shader = new Shader { Code = "shader_type canvas_item; void fragment(){ vec2 p = UV - 0.5; float l = 0.5 + (-p.x - p.y) * 0.9; COLOR = vec4(mix(vec3(0.55, 0.36, 0.1), vec3(1.0, 0.92, 0.6), l), 1.0); }" } } };
        AddChild(p);
        return p;
    }

    // ---------------------------------------------------------------- left side: your deck

    void DeckSide()
    {
        var deck = Session.Playing ?? Session.Decks.FirstOrDefault();
        var cover = deck != null && deck.CoverCard() >= 0 ? CardDb.Shared.Get(deck.CoverCard()).Name : "Kuriboh";
        // the deck: two card backs under the cover card, fanned
        var c = new Vector2(W * 0.2f, H * 0.5f);
        float h = H * 0.46f, w = h * 0.686f;
        for (int i = 2; i >= 1; i--)
        {
            var back = Ink.Pic(Tex("res://assets/title/card_back.bmp"), c - new Vector2(w, h) / 2 + new Vector2(i * 16, i * 10), new Vector2(w, h), this);
            back.StretchMode = TextureRect.StretchModeEnum.Scale; back.PivotOffset = new Vector2(w, h) / 2; back.Rotation = -0.05f + i * 0.045f;
            back.Modulate = new Color(1, 1, 1, 0.92f);
        }
        FloatCard(cover, c, h, -0.05f, true);
        Say(this, "YOUR DECK", small, 16, InkC * new Color(1, 1, 1, 0.5f), new Vector2(c.X - 300, c.Y + h / 2 + 40), new Vector2(600, 24), HorizontalAlignment.Center);
        Say(this, (deck?.Name ?? "NO DECK").ToUpperInvariant(), font, 30, InkC, new Vector2(c.X - 300, c.Y + h / 2 + 66), new Vector2(600, 44), HorizontalAlignment.Center);
        var f = Cosmetics.Framed(Settings.Avatar, Session.Profile.Frame, 64);
        f.Position = new Vector2(c.X - 160, 70); AddChild(f);
        Say(this, Settings.PlayerName.ToUpperInvariant(), font, 22, InkC, new Vector2(c.X - 84, 76));
        Say(this, $"{Session.Profile.Dp:N0} DP   ·   {deck?.Main.Count ?? 0} CARDS", small, 16, InkC * new Color(1, 1, 1, 0.55f), new Vector2(c.X - 83, 108));
    }

    // ---------------------------------------------------------------- left side: next rival

    void RivalSide()
    {
        var tex = Tex("res://assets/rivals/kaiba.png");
        float h = H * 0.86f, w = h * tex.GetWidth() / tex.GetHeight();
        var r = Ink.Pic(tex, new Vector2(W * 0.2f - w / 2, H - h), new Vector2(w, h), this);
        r.StretchMode = TextureRect.StretchModeEnum.Scale;
        r.Material = new ShaderMaterial { Shader = new Shader { Code = @"shader_type canvas_item;
" + RiftVariants.CrackFn + @"
void fragment() {
  vec4 a = texture(TEXTURE, UV);
  float l = dot(a.rgb, vec3(0.3, 0.59, 0.11));
  vec3 c = mix(vec3(0.12, 0.12, 0.14), vec3(0.97), smoothstep(0.15, 0.9, l));
  float fade = smoothstep(1.0, 0.75, UV.y);
  float keep = smoothstep(0.0, -0.06, crack_d(SCREEN_UV, TIME));
  COLOR = vec4(c, a.a * fade * keep * 0.9);
}" } }.P("aspect", W / H);
        Say(this, "NEXT RIVAL", small, 16, InkC * new Color(1, 1, 1, 0.5f), new Vector2(70, 80));
        Say(this, "KAIBA", font, 46, InkC, new Vector2(66, 104));
        Ink.Drops(this, 200, 200, new OpponentPick("kaiba", "kaiba/x_lv6", 5), onPaper: true).Scale = new Vector2(0.6f, 0.6f);
    }
}
