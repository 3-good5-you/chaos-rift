using Godot;
using PoC.Engine;
using static PoC.Game.UI.ConceptKit;

namespace PoC.Game.UI;

/// Concept 1 "SHADOW REALM": a real 3D void. Sixteen legendary cards orbit a pillar of chaos light in violet fog;
/// the menu is a column of engraved words on the left, and each item summons its card to the front of the ring.
/// `-- --demo [--screen=menu|rivals] [--hover=N]`
public partial class ShadowRealm : MenuBase
{
    float W, H;
    Node3D ring = null!, featured = null!, root = null!;
    readonly List<Node3D> cards = new();
    readonly List<(Label L, ColorRect Line)> items = new();
    Label blurb = null!;
    readonly List<Node3D> rivals = new();
    readonly List<Label> rivalNames = new();
    SpotLight3D spot = null!;
    bool rivalScreen;
    double t;
    Font display = null!, small = null!;

    protected override int ItemCount => rivalScreen ? 4 : Items.Length;
    protected override string? MusicPath => Md("menu_01");
    protected override void OnChoose(int i) { }
    protected override void OnBack() => GetTree().Quit();

    protected override void Build()
    {
        Demo.Ensure();
        W = GetViewportRect().Size.X; H = GetViewportRect().Size.Y;
        rivalScreen = Demo.Arg("screen") == "rivals";
        display = Ink.Display; small = Sys(new[] { "Bahnschrift", "Segoe UI" }, 400, stretch: 87);
        var (r, cam, _) = Stage(this);
        root = r;
        root.AddChild(new WorldEnvironment
        {
            Environment = new Godot.Environment
            {
                BackgroundMode = Godot.Environment.BGMode.Color, BackgroundColor = new Color(0.02f, 0.008f, 0.035f),
                AmbientLightSource = Godot.Environment.AmbientSource.Color, AmbientLightColor = new Color(0.3f, 0.22f, 0.45f), AmbientLightEnergy = 0.25f,
                TonemapMode = Godot.Environment.ToneMapper.Filmic, TonemapExposure = 1.1f,
                GlowEnabled = true, GlowIntensity = 0.9f, GlowBloom = 0.04f, GlowHdrThreshold = 1.2f, GlowBlendMode = Godot.Environment.GlowBlendModeEnum.Screen,
                VolumetricFogEnabled = true, VolumetricFogDensity = 0.012f, VolumetricFogAlbedo = new Color(0.4f, 0.3f, 0.6f),
                VolumetricFogEmission = new Color(0.02f, 0.005f, 0.04f), VolumetricFogEmissionEnergy = 0.5f, VolumetricFogLength = 40,
            },
        });
        cam.Position = new Vector3(0, 0.5f, 7.5f);
        cam.LookAt(new Vector3(0.6f, 0.2f, 0));

        // Chaos light: a white-violet pillar with a gold core, lights above and below.
        var pillarAt = rivalScreen ? new Vector3(0, 0, -4) : new Vector3(2.9f, 0, -1.8f);
        var pillar = new MeshInstance3D
        {
            Mesh = new CylinderMesh { TopRadius = 0.025f, BottomRadius = 0.025f, Height = 30 },
            MaterialOverride = new StandardMaterial3D { ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded, AlbedoColor = new Color(1.3f, 1.0f, 2.2f), Transparency = BaseMaterial3D.TransparencyEnum.Alpha },
            Position = pillarAt, Visible = !rivalScreen,
        };
        root.AddChild(pillar);
        root.AddChild(new OmniLight3D { Position = pillarAt + new Vector3(0, 0.5f, 0.8f), LightColor = new Color(0.75f, 0.55f, 1f), LightEnergy = 4, OmniRange = 9, LightVolumetricFogEnergy = 1.2f });
        root.AddChild(new OmniLight3D { Position = pillarAt + new Vector3(0, -2.5f, 1), LightColor = Gold, LightEnergy = 2, OmniRange = 6, LightVolumetricFogEnergy = 0.8f });
        Motes(pillarAt);

        if (rivalScreen) { Rivals(cam); return; }

        ring = new Node3D { Position = pillarAt, RotationDegrees = new Vector3(12, 0, -8) };
        root.AddChild(ring);
        for (int i = 0; i < Ring.Length; i++)
        {
            var c = CardQuad(Full(Ring[i]), 1.3f, 0.06f);
            float a = i * Mathf.Tau / Ring.Length;
            c.Position = new Vector3(Mathf.Cos(a) * 3.6f, Mathf.Sin(i * 1.7f) * 0.35f, Mathf.Sin(a) * 3.6f);
            c.Rotation = new Vector3(0, -a + Mathf.Pi / 2, 0);
            ring.AddChild(c);
            cards.Add(c);
        }
        featured = new Node3D { Position = pillarAt + new Vector3(-0.2f, 0.25f, 2.6f) };
        root.AddChild(featured);

        // 2D: logo, menu, profile.
        var logo = Tex("res://assets/title/logo_dark.png");
        Ink.Pic(logo, new Vector2(70, 50), new Vector2(520, 520f * logo.GetHeight() / logo.GetWidth()), this);
        float y = 340;
        for (int i = 0; i < Items.Length; i++)
        {
            int k = i;
            var line = new ColorRect { Position = new Vector2(80, y + 62), Size = new Vector2(0, 2), Color = Gold, MouseFilter = MouseFilterEnum.Ignore };
            AddChild(line);
            var l = Say(this, Items[i].ToUpperInvariant(), display, 46, Cream * new Color(1, 1, 1, 0.5f), new Vector2(80, y), new Vector2(560, 64));
            l.MouseFilter = MouseFilterEnum.Stop;
            l.MouseEntered += () => SetFocus(k);
            items.Add((l, line));
            y += 76;
        }
        blurb = Say(this, "", small, 22, Cream * new Color(1, 1, 1, 0.6f), new Vector2(82, y + 14), new Vector2(600, 30));
        Profile();
        SetFocus(0);
    }

    void Motes(Vector3 at)
    {
        var p = new GpuParticles3D
        {
            Amount = 400, Lifetime = 9, Position = at + new Vector3(-1.5f, -3, 0), Preprocess = 9,
            DrawPass1 = new QuadMesh { Size = new Vector2(0.02f, 0.02f), Material = new StandardMaterial3D { ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded, AlbedoColor = new Color(1.8f, 1.4f, 2.6f), BillboardMode = BaseMaterial3D.BillboardModeEnum.Enabled, Transparency = BaseMaterial3D.TransparencyEnum.Alpha, BlendMode = BaseMaterial3D.BlendModeEnum.Add } },
            ProcessMaterial = new ParticleProcessMaterial
            {
                EmissionShape = ParticleProcessMaterial.EmissionShapeEnum.Box, EmissionBoxExtents = new Vector3(7, 0.5f, 4), Direction = Vector3.Up, Spread = 25,
                InitialVelocityMin = 0.2f, InitialVelocityMax = 0.6f, Gravity = Vector3.Zero, TurbulenceEnabled = true, TurbulenceNoiseStrength = 0.6f,
                ScaleMin = 0.5f, ScaleMax = 1.6f,
            },
        };
        root.AddChild(p);
    }

    void Profile()
    {
        var f = Cosmetics.Framed(Settings.Avatar, Session.Profile.Frame, 92);
        f.Position = new Vector2(W - 400, H - 140);
        AddChild(f);
        Say(this, Settings.PlayerName.ToUpperInvariant(), display, 30, Cream, new Vector2(W - 296, H - 128));
        Say(this, $"{Session.Profile.Dp:N0} DP", small, 20, Gold, new Vector2(W - 294, H - 86));
    }

    protected override void OnFocus(int old, int now)
    {
        if (rivalScreen) { FocusRival(now); return; }
        for (int i = 0; i < items.Count; i++)
        {
            bool on = i == now;
            var (l, line) = items[i];
            l.AddThemeColorOverride("font_color", on ? Gold : Cream * new Color(1, 1, 1, 0.5f));
            CreateTween().TweenProperty(l, "position:x", on ? 110f : 80f, 0.25).SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
            CreateTween().TweenProperty(line, "size:x", on ? Kit.Cond.GetStringSize(l.Text, HorizontalAlignment.Left, -1, 46).X * 1.25f + 30 : 0f, 0.3);
        }
        blurb.Text = Blurb[now];
        // The item's card rises out of the fog in front of the pillar.
        foreach (var c in featured.GetChildren()) c.QueueFree();
        var card = CardQuad(Full(Featured[now]), 2.3f, 0.12f);
        card.Position = new Vector3(0, -2.5f, -1);
        card.RotationDegrees = new Vector3(0, -25, 0);
        featured.AddChild(card);
        var tw = CreateTween().SetParallel();
        tw.TweenProperty(card, "position", Vector3.Zero, 0.7).SetTrans(Tween.TransitionType.Expo).SetEase(Tween.EaseType.Out);
        tw.TweenProperty(card, "rotation_degrees", new Vector3(0, -12, 0), 0.9).SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
    }

    // ---------------------------------------------------------------- rival select in the void

    static readonly string[] RivalKeys = { "yugi", "kaiba", "joey", "jaden" };

    void Rivals(Camera3D cam)
    {
        cam.Position = new Vector3(0, 0.6f, 7);
        cam.LookAt(new Vector3(0, 0.3f, 0));
        spot = new SpotLight3D { LightColor = new Color(1f, 0.9f, 0.75f), LightEnergy = 10, SpotRange = 14, SpotAngle = 16, LightVolumetricFogEnergy = 1.5f };
        root.AddChild(spot);
        for (int i = 0; i < 4; i++)
        {
            var tex = Tex($"res://assets/rivals/{RivalKeys[i]}.png");
            float h = 3.2f, w = h * tex.GetWidth() / tex.GetHeight();
            var n = new Node3D { Position = new Vector3((i - 1.5f) * 2.7f, -0.2f, -Mathf.Abs(i - 1.5f) * 0.6f) };
            n.AddChild(new MeshInstance3D
            {
                Mesh = new QuadMesh { Size = new Vector2(w, h) },
                MaterialOverride = new StandardMaterial3D { AlbedoTexture = tex, Transparency = BaseMaterial3D.TransparencyEnum.AlphaDepthPrePass, Roughness = 0.6f, TextureFilter = BaseMaterial3D.TextureFilterEnum.LinearWithMipmapsAnisotropic },
            });
            root.AddChild(n);
            rivals.Add(n);
            var l = Say(this, RivalKeys[i].ToUpperInvariant(), display, 52, Cream * new Color(1, 1, 1, 0.4f), new Vector2(W / 2 + (i - 1.5f) * 430 - 200, H - 150), new Vector2(400, 70), HorizontalAlignment.Center);
            int k = i; l.MouseFilter = MouseFilterEnum.Stop; l.MouseEntered += () => SetFocus(k);
            rivalNames.Add(l); l.ZIndex = 1;
        }
        ShaderRect(this, "shader_type canvas_item; void fragment(){ COLOR = vec4(0.01,0.0,0.02, smoothstep(0.0,1.0,UV.y)*0.95); }", new Vector2(0, H - 320), new Vector2(W, 320));
        var logo = Tex("res://assets/title/logo_dark.png");
        Ink.Pic(logo, new Vector2(60, 40), new Vector2(320, 320f * logo.GetHeight() / logo.GetWidth()), this);
        Say(this, "CHOOSE YOUR RIVAL", display, 40, Cream, new Vector2(420, 70));
        blurb = Say(this, "", small, 24, Cream * new Color(1, 1, 1, 0.65f), new Vector2(0, H - 70), new Vector2(W, 34), HorizontalAlignment.Center);
        blurb.ZIndex = 1;
        SetFocus(1);
    }

    static readonly string[] RivalLines = { "The King of Games. Hard · 6 decks", "Duel Monsters' strongest dragon tamer. Hard", "Never out of the duel. Normal", "Gotta get my game on! Normal" };

    void FocusRival(int now)
    {
        for (int i = 0; i < 4; i++)
        {
            bool on = i == now;
            CreateTween().TweenProperty(rivals[i], "position:z", on ? 1.2f : -Mathf.Abs(i - 1.5f) * 0.6f - 0.6f, 0.5).SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
            rivals[i].GetChild<MeshInstance3D>(0).Transparency = on ? 0f : 0.55f;
            rivalNames[i].AddThemeColorOverride("font_color", on ? Gold : Cream * new Color(1, 1, 1, 0.35f));
        }
        spot.Position = rivals[now].Position + new Vector3(0, 6, 3);
        spot.LookAt(rivals[now].Position + new Vector3(0, -0.5f, 1.2f));
        blurb.Text = RivalLines[now];
    }

    public override void _Process(double delta)
    {
        base._Process(delta);
        t += delta;
        if (ring != null) ring.RotateObjectLocal(Vector3.Up, (float)delta * 0.08f);
        if (featured != null) featured.Position = featured.Position with { Y = 0.25f + Mathf.Sin((float)t * 1.2f) * 0.06f };
        foreach (var (n, i) in rivals.Select((n, i) => (n, i))) n.Position = n.Position with { Y = -0.2f + Mathf.Sin((float)t * 0.9f + i) * 0.05f };
    }
}
