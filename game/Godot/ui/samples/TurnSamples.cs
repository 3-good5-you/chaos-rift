using Godot;
using PoC.Engine;

namespace PoC.Game.UI;

/// Mockups for "who goes first" on the dark ink sheet (not wired into the game).
/// `-- --demo --variant=a|b|c [--rivalfirst] [--rival=N]`
/// a = black lacquer coin with the Puzzle / Eye in cream ink, b = coin with your picture / the rival's face,
/// c = flat ink coin that flips in 2D and bursts into a splat on landing.
public partial class TurnSamples : MenuBase
{
    const string Face = @"shader_type spatial;
render_mode unshaded;
uniform bool flip = false;      // the back face is upside down after the half turn
uniform sampler2D tex : source_color, filter_linear_mipmap;
uniform float art_scale = 1.0;
uniform bool picture = false;   // false: emblem redrawn in cream ink; true: round picture
void fragment() {
  vec2 p = UV - 0.5;
  if (flip) p = -p;
  float r = length(p);
  if (r > 0.5) discard;
  vec3 ink = vec3(0.045, 0.04, 0.045), cream = vec3(0.9, 0.87, 0.8);
  vec3 c = ink + vec3(0.03) * smoothstep(0.5, 0.0, r);
  c = mix(c, cream, smoothstep(0.012, 0.0, abs(r - 0.46)) + smoothstep(0.006, 0.0, abs(r - 0.43)) * 0.6);
  if (picture) {
    float hole = 0.40;
    vec3 art = texture(tex, p / (hole * 2.0) + 0.5).rgb;
    c = mix(c, art, smoothstep(hole + 0.004, hole - 0.004, r));
  } else {
    vec4 art = texture(tex, p / art_scale + 0.5);
    float inside = step(max(abs(p.x / art_scale), abs(p.y / art_scale)), 0.5);
    float l = dot(art.rgb, vec3(0.3, 0.59, 0.11));
    vec3 e = mix(ink, cream, smoothstep(0.22, 0.5, l));
    c = mix(c, e, art.a * inside);
  }
  c += vec3(0.08) * exp(-pow((UV.x + UV.y - 0.7) * 6.0, 2.0));   // soft sheen
  ALBEDO = c;
}";

    /// Emblem in black ink on a cream disc (2D coin).
    const string Flat = @"shader_type canvas_item;
uniform sampler2D art : filter_linear;
uniform float art_scale = 0.75;
void fragment() {
  vec2 p = UV - 0.5;
  float r = length(p);
  vec3 ink = vec3(0.045, 0.04, 0.045), cream = vec3(0.9, 0.87, 0.8);
  vec4 a = texture(art, p / art_scale + 0.5);
  float inside = step(max(abs(p.x / art_scale), abs(p.y / art_scale)), 0.5);
  float l = dot(a.rgb, vec3(0.3, 0.59, 0.11));
  vec3 c = mix(cream, mix(ink, cream * 0.8, smoothstep(0.3, 0.75, l)), a.a * inside);
  c = mix(c, ink, smoothstep(0.012, 0.0, abs(r - 0.455)));
  COLOR = vec4(c, smoothstep(0.5, 0.49, r));
}";

    static readonly Vector2[] FaceAt = { new(0.49f, 0.38f), new(0.5f, 0.24f), new(0.43f, 0.22f), new(0.43f, 0.26f) };

    float W, H;
    bool youFirst;
    RivalPanels.Rival rival = null!;
    ShaderMaterial youSplat = null!, themSplat = null!, themWash = null!;
    Control youPic = null!;
    string variant = "a";

    protected override string? MusicPath => Md("solo_gate");
    protected override int ItemCount => 1;
    protected override void OnFocus(int old, int now) { }
    protected override void OnChoose(int i) { }
    protected override void OnBack() => GetTree().Quit();

    protected override void Build()
    {
        Demo.Ensure();
        Session.RollPicks();
        Session.Selected = int.Parse(Demo.Arg("rival", "1"));
        rival = Session.Rival;
        W = GetViewportRect().Size.X; H = GetViewportRect().Size.Y;
        youFirst = !OS.GetCmdlineUserArgs().Contains("--rivalfirst");
        variant = Demo.Arg("variant", "a");
        Ink.Sheet(this);
        Ink.Header(this, "WHO GOES FIRST", OnBack);
        Duelists();
        if (variant == "c") FlatCoin(); else Coin();
    }

    // ---- the two duelists

    void Duelists()
    {
        // You, left: framed picture on a paper splat.
        const float A = 300;
        var c = new Vector2(W * 0.16f, H * 0.52f);
        var ys = Ink.Pic(GD.Load<Texture2D>("res://assets/title/ink_splash.png"), c - new Vector2(A, A) * 0.95f, new Vector2(A, A) * 1.9f, this);
        youSplat = Ink.BleedMat(Ink.Cream, 0.9f, 1); ys.Material = youSplat;
        youPic = Cosmetics.Framed(Settings.Avatar, Session.Profile.Frame, A);
        youPic.Position = c - new Vector2(A, A) / 2;
        AddChild(youPic);
        Kit.Put(this, Settings.PlayerName, Ink.Display, 90, Ink.Cream, new Vector2(c.X - 400, c.Y + A / 2 + 60), new Vector2(800, 120), HorizontalAlignment.Center);
        Kit.Put(this, (Session.Playing?.Name ?? "Deck").ToUpperInvariant(), Kit.Cond, 28, Ink.Cream * new Color(1, 1, 1, 0.5f), new Vector2(c.X - 400, c.Y + A / 2 + 180), new Vector2(800, 40), HorizontalAlignment.Center);

        // The rival, right: standing on a paper splat.
        var tex = RivalPanels.Portrait(rival);
        float ph = H * 0.62f, pw = ph * tex.GetWidth() / tex.GetHeight(), cx = W * 0.84f, bottom = H * 0.8f, s = ph * 1.1f;
        var ts = Ink.Pic(GD.Load<Texture2D>("res://assets/title/ink_splash.png"), new Vector2(cx - s / 2, bottom - ph * 0.55f - s / 2), new Vector2(s, s), this);
        themSplat = Ink.BleedMat(Ink.Cream, 0.9f, 2); ts.Material = themSplat;
        var por = Ink.Pic(tex, new Vector2(cx - pw / 2, bottom - ph), new Vector2(pw, ph), this);
        themWash = Ink.WashMat(0f); por.Material = themWash;
        Ink.Brush(new Vector2(cx - pw * 0.45f, bottom - 4), new Vector2(pw * 0.9f, 20), Ink.Cream * new Color(1, 1, 1, 0.5f), this);
        var n = rival.Name[..1] + rival.Name[1..].ToLowerInvariant();
        Kit.Put(this, n, Ink.Display, 90, Ink.Cream, new Vector2(cx - 400, bottom + 20), new Vector2(800, 120), HorizontalAlignment.Center);
    }

    /// After the toss: the winner's splat bleeds out, the other side sinks into the ink, the result is written.
    void Announce(float delay)
    {
        GetTree().CreateTimer(delay).Timeout += () =>
        {
            Ink.To(this, youFirst ? youSplat : themSplat, "progress", 1.4f, 0.7);
            Ink.To(this, youFirst ? themSplat : youSplat, "progress", 0f, 0.5);
            if (youFirst) Ink.To(this, themWash, "wash", 0.8f, 0.5);
            else CreateTween().TweenProperty(youPic, "modulate", new Color(0.45f, 0.43f, 0.42f), 0.5);
        };
        var who = youFirst ? "You" : rival.Name[..1] + rival.Name[1..].ToLowerInvariant();
        var line = Kit.Put(this, who, Ink.Display, 150, Ink.Cream, new Vector2(W / 2 - 600, H - 330), new Vector2(1200, 190), HorizontalAlignment.Center);
        var rest = Kit.Put(this, youFirst ? "GO FIRST" : "GOES FIRST", Kit.Cond, 64, Ink.Cream, new Vector2(W / 2 - 600, H - 150), new Vector2(1200, 80), HorizontalAlignment.Center);
        var rule = Ink.Brush(new Vector2(W / 2 - 260, H - 166), new Vector2(520, 14), Ink.Cream * new Color(1, 1, 1, 0.5f), this);
        foreach (var c in new Control[] { line, rest, rule })
        {
            c.Modulate = new Color(1, 1, 1, 0);
            var tw = CreateTween().SetParallel();
            tw.TweenProperty(c, "modulate:a", 1f, 0.35).SetDelay(delay + (c == line ? 0 : 0.25));
            var y = c.Position.Y;
            c.Position += new Vector2(0, 20);
            tw.TweenProperty(c, "position:y", y, 0.5).SetDelay(delay + (c == line ? 0 : 0.25)).SetTrans(Tween.TransitionType.Expo).SetEase(Tween.EaseType.Out);
        }
    }

    // ---- a / b: 3D coin

    static Texture2D RivalFace(RivalPanels.Rival r)
    {
        int k = Array.FindIndex(RivalPanels.All, x => x.File == r.File);
        var img = RivalPanels.Portrait(r).GetImage();
        if (k < 0) return ImageTexture.CreateFromImage(img);
        int s = (int)(img.GetWidth() * 0.46f);
        var c = FaceAt[k] * new Vector2(img.GetWidth(), img.GetHeight());
        if (img.IsCompressed()) img.Decompress();
        return ImageTexture.CreateFromImage(img.GetRegion(new Rect2I((int)c.X - s / 2, Math.Max(0, (int)c.Y - s / 2), s, s)));
    }

    void Coin()
    {
        var holder = new SubViewportContainer { Position = new Vector2(W / 2 - 500, 120), Size = new Vector2(1000, H * 0.62f), Stretch = true, MouseFilter = MouseFilterEnum.Ignore };
        var vp = new SubViewport { TransparentBg = true, Msaa3D = Viewport.Msaa.Msaa4X, OwnWorld3D = true };
        holder.AddChild(vp);
        AddChild(holder);
        var world = new Node3D();
        vp.AddChild(world);
        world.AddChild(new WorldEnvironment
        {
            Environment = new Godot.Environment
            {
                BackgroundMode = Godot.Environment.BGMode.Color, BackgroundColor = new Color(0, 0, 0, 0),
                AmbientLightSource = Godot.Environment.AmbientSource.Color, AmbientLightColor = new Color(0.95f, 0.92f, 0.86f), AmbientLightEnergy = 1.1f,
            },
        });
        world.AddChild(new DirectionalLight3D { RotationDegrees = new Vector3(-35, -25, 0), LightEnergy = 1.0f });
        world.AddChild(new OmniLight3D { Position = new Vector3(1.5f, 2, 3), LightColor = new Color(1f, 0.95f, 0.85f), LightEnergy = 1.4f, OmniRange = 8 });
        var cam = new Camera3D { Position = new Vector3(0, 1.2f, 6.5f), Fov = 40 };
        world.AddChild(cam);
        cam.LookAt(new Vector3(0, 0.6f, 0));

        var coin = new Node3D { Position = new Vector3(0, 0.6f, 0) };
        world.AddChild(coin);
        var rim = new StandardMaterial3D { AlbedoColor = new Color(0.86f, 0.83f, 0.76f), Metallic = 0.1f, Roughness = 0.45f };
        coin.AddChild(new MeshInstance3D { Mesh = new CylinderMesh { TopRadius = 1.2f, BottomRadius = 1.2f, Height = 0.14f, RadialSegments = 96 }, MaterialOverride = rim, RotationDegrees = new Vector3(90, 0, 0) });
        ShaderMaterial FaceMat(Texture2D t, float scale, bool picture)
        {
            var m = new ShaderMaterial { Shader = new Shader { Code = Face } };
            m.SetShaderParameter("tex", t); m.SetShaderParameter("art_scale", scale); m.SetShaderParameter("picture", picture);
            return m;
        }
        bool pics = variant == "b";
        var front = pics ? FaceMat(Settings.Avatar ?? Tex("res://assets/coin/puzzle.png"), 1, true) : FaceMat(Tex("res://assets/coin/puzzle.png"), 0.66f, false);
        var back = pics ? FaceMat(RivalFace(rival), 1, true) : FaceMat(Tex("res://assets/coin/eye.png"), 0.74f, false);
        back.SetShaderParameter("flip", true);
        var faceMesh = new QuadMesh { Size = new Vector2(2.4f, 2.4f) };
        coin.AddChild(new MeshInstance3D { Mesh = faceMesh, MaterialOverride = front, Position = new Vector3(0, 0, 0.071f) });
        coin.AddChild(new MeshInstance3D { Mesh = faceMesh, MaterialOverride = back, Position = new Vector3(0, 0, -0.071f), RotationDegrees = new Vector3(0, 180, 0) });

        // Toss: rise toward the camera, spin, fall back and settle on the winner's side.
        float turns = 9 + (youFirst ? 0 : 0.5f);
        var tw = CreateTween();
        tw.TweenInterval(0.6);
        tw.TweenMethod(Callable.From<float>(k =>
        {
            float h = 4f * k * (1 - k);
            coin.Position = new Vector3(0, 0.6f + h * 1.1f, -h * 2.5f);
            coin.Rotation = new Vector3(Mathf.Tau * turns * Mathf.SmoothStep(0, 1, k), 0, 0.25f * Mathf.Sin(k * 6));
        }), 0f, 1f, 2.2).SetTrans(Tween.TransitionType.Linear);
        tw.TweenProperty(coin, "scale", new Vector3(1.1f, 1.1f, 1.1f), 0.12);
        tw.TweenProperty(coin, "scale", Vector3.One, 0.3).SetTrans(Tween.TransitionType.Elastic).SetEase(Tween.EaseType.Out);
        Announce(2.9f);
    }

    // ---- c: flat ink coin

    void FlatCoin()
    {
        const float D = 340;
        var home = new Vector2(W / 2, H * 0.44f);
        // Splat that bursts out of the coin when it lands.
        var burst = Ink.Pic(GD.Load<Texture2D>("res://assets/title/ink_splash.png"), home - new Vector2(D, D) * 1.1f, new Vector2(D, D) * 2.2f, this);
        var bm = Ink.BleedMat(Ink.Cream, 0f, 5); burst.Material = bm;
        var coin = new Control { Size = new Vector2(D, D), Position = home - new Vector2(D, D) / 2, PivotOffset = new Vector2(D, D) / 2, MouseFilter = MouseFilterEnum.Ignore };
        AddChild(coin);
        ShaderMaterial Side(string img, float sc) => Mat(Flat, ("art", Tex($"res://assets/coin/{img}.png")), ("art_scale", sc));
        var face = new ColorRect { Size = coin.Size, Material = Side("puzzle", 0.62f), MouseFilter = MouseFilterEnum.Ignore };
        var backMat = Side("eye", 0.72f);
        var frontMat = (ShaderMaterial)face.Material;
        coin.AddChild(face);
        var shadow = new ColorRect { Size = new Vector2(D * 0.8f, 26), Position = home + new Vector2(-D * 0.4f, D / 2 + 30), Color = new Color(0, 0, 0, 0.5f), Material = Kit.RoundedMat(D * 0.8f / 26, 0.5f), MouseFilter = MouseFilterEnum.Ignore };
        AddChild(shadow); MoveChild(shadow, coin.GetIndex());

        // Flip: the coin's width follows cos(angle); past a half turn the other face shows.
        float turns = 7 + (youFirst ? 0 : 0.5f);
        var tw = CreateTween();
        tw.TweenInterval(0.6);
        tw.TweenMethod(Callable.From<float>(k =>
        {
            float a = Mathf.Tau * turns * Mathf.SmoothStep(0, 1, k), h = 4f * k * (1 - k);
            float cs = Mathf.Cos(a);
            coin.Scale = new Vector2(Math.Max(0.02f, Math.Abs(cs)), 1) * (1 + h * 0.35f);
            face.Material = cs >= 0 ? frontMat : backMat;
            coin.Position = home - new Vector2(D, D) / 2 - new Vector2(0, h * 260);
            shadow.Modulate = new Color(1, 1, 1, 1 - h * 0.7f);
        }), 0f, 1f, 2.2).SetTrans(Tween.TransitionType.Linear);
        tw.TweenCallback(Callable.From(() => { Ink.To(this, bm, "progress", 1.4f, 0.5); shadow.Visible = false; }));
        tw.TweenProperty(coin, "scale", new Vector2(1.12f, 1.12f), 0.1);
        tw.TweenProperty(coin, "scale", Vector2.One, 0.35).SetTrans(Tween.TransitionType.Elastic).SetEase(Tween.EaseType.Out);
        Announce(2.9f);
    }
}
