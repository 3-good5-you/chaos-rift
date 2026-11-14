using Godot;
using PoC.Engine;
using System;
using System.Collections.Generic;
using System.Linq;

namespace PoC.Game.UI;

/// Field Lab: duel field looks built from zero on real assets: scanned PBR floors, modelled boards and zone trays, glTF
/// props and HDRI lighting (CC0, Poly Haven; fetched by game/tools/fieldlab_assets.py into assets/fieldlab, loaded at
/// runtime). Each duelist brings their OWN half: floor, board, trays and scenery, so any theme can face any other.
/// Monster and spell/trap rows are identical in every theme (straight, column-aligned); pile zones vary per theme.
/// Keys: 1-6 = your half, Shift+1-6 = rival half, arrows = cycle (left/right yours, up/down rival), C = sample cards,
/// U = UI mock, P = pick (-> game/field_picks.txt), H = hide help text.
/// Screenshots: `-- --sample=N --rival=M --shot=path.png [--nocards] [--noui]`.
public partial class FieldLab : Node3D
{
    const int Monster = 0, Spell = 1, Field = 2, Deck = 3, Fusion = 4, Gy = 5, Banish = 6, Side = 7;
    static readonly string[] IconName = { "monster", "spell", "field", "deck", "fusion", "gy", "banish", "side" };
    const float BoardY = 0.02f;      // the row trays stand on the board, the pile trays on the floor
    static readonly Color Gold = new(1f, 0.82f, 0.4f);

    record struct Z(int Kind, float X, float Y, float Rot);

    /// One duelist's half of the field. Themes are written for the near side; P / Yaw turn them around for the rival.
    sealed class Half
    {
        public bool Me;
        public List<Z> Zones = new();
        public Vector3 P(float x, float y, float z) => Me ? new(x, y, z) : new(-x, y, -z);
        public float Yaw(float deg) => Me ? deg : deg + 180;
    }

    (string Name, string Note, Action Env, Action<Half> Build)[] themes = null!;
    int cur, rival = 1;
    bool cards = true, ui = true;
    string? shot;
    Node3D world = null!;
    Camera3D cam = null!;
    DirectionalLight3D sun = null!;
    Godot.Environment env = null!;
    Label caption = null!;
    Control hud = null!;
    string flash = "";
    readonly Half[] halves = { new() { Me = true }, new() { Me = false } };
    readonly List<(Light3D Light, float Energy, float Seed)> flicker = new();
    Shader flameShader = null!, moltenShader = null!;

    public override void _Ready()
    {
        themes = new (string, string, Action, Action<Half>)[]
        {
            ("CASTLE KEEP", "Monastery flagstones by firelight; slate board, iron trays.", CastleEnv, Castle),
            ("FROST PASS", "Fresh snow and boulders; ice-blue slate board, steel trays.", FrostEnv, Frost),
            ("FOREST SHRINE", "Mossy cobbles, roots and ferns; mossy rock board and trays.", ForestEnv, Forest),
            ("MARBLE HALL", "Cathedral marble and statues; white marble board, brass trays.", MarbleEnv, Marble),
            ("CALDERA", "Volcanic rock, a lava channel at the edge; rusted iron board and trays.", CalderaEnv, Caldera),
            ("SUNSET RUIN", "Red sandstone at dusk, old pottery; sandstone board and trays.", RuinEnv, Ruin),
        };
        foreach (var a in OS.GetCmdlineUserArgs())
        {
            if (a.StartsWith("--sample=")) cur = Math.Clamp(int.Parse(a[9..]) - 1, 0, themes.Length - 1);
            if (a.StartsWith("--rival=")) rival = Math.Clamp(int.Parse(a[8..]) - 1, 0, themes.Length - 1);
            if (a.StartsWith("--shot=")) shot = a[7..];
            if (a == "--nocards") cards = false;
            if (a == "--noui") ui = false;
        }
        flameShader = new Shader { Code = FlameCode };
        moltenShader = new Shader { Code = MoltenCode };

        cam = new Camera3D
        {
            Fov = 40,
            Attributes = new CameraAttributesPractical { DofBlurFarEnabled = true, DofBlurFarDistance = 27, DofBlurFarTransition = 10, DofBlurAmount = 0.05f },
        };
        AddChild(cam);

        env = new Godot.Environment
        {
            BackgroundMode = Godot.Environment.BGMode.Sky,
            TonemapMode = Godot.Environment.ToneMapper.Aces, TonemapWhite = 6,
            GlowEnabled = true, GlowIntensity = 0.6f, GlowBloom = 0.08f, GlowHdrThreshold = 1.1f,
            SsaoEnabled = true, SsaoRadius = 0.6f, SsaoIntensity = 2.2f, SsilEnabled = true,
            FogEnabled = true,
        };
        AddChild(new WorldEnvironment { Environment = env });
        sun = new DirectionalLight3D { ShadowEnabled = true, DirectionalShadowMaxDistance = 50, ShadowBlur = 2f, LightAngularDistance = 1.5f };
        AddChild(sun);
        world = new Node3D();
        AddChild(world);

        var layer = new CanvasLayer();
        AddChild(layer);
        layer.AddChild(new ColorRect      // soft vignette: pulls the eye to the play area
        {
            AnchorRight = 1, AnchorBottom = 1, MouseFilter = Control.MouseFilterEnum.Ignore,
            Material = new ShaderMaterial { Shader = new Shader { Code = "shader_type canvas_item; void fragment(){ COLOR = vec4(0.0, 0.0, 0.0, smoothstep(0.45, 1.0, length(UV - 0.5) * 1.25) * 0.55); }" } },
        });
        hud = new Control { MouseFilter = Control.MouseFilterEnum.Ignore };
        layer.AddChild(hud);
        caption = new Label { Visible = shot == null };
        caption.AddThemeFontSizeOverride("font_size", 15);
        caption.AddThemeColorOverride("font_outline_color", Colors.Black);
        caption.AddThemeConstantOverride("outline_size", 6);
        layer.AddChild(caption);

        Show();
        if (shot != null) GetTree().CreateTimer(1.6).Timeout += () =>
        {
            GetViewport().GetTexture().GetImage().SavePng(shot);
            GetTree().Quit();
        };
    }

    public override void _Process(double delta)
    {
        float t = Time.GetTicksMsec() / 1000f;
        foreach (var (l, e, s) in flicker)
            if (IsInstanceValid(l)) l.LightEnergy = e * (0.86f + 0.10f * MathF.Sin(t * 7 + s) * MathF.Sin(t * 4.3f + s * 2) + 0.04f * MathF.Sin(t * 17 + s));
    }

    public override void _UnhandledInput(InputEvent e)
    {
        if (e is not InputEventKey { Pressed: true, Echo: false } k) return;
        flash = "";
        int n = themes.Length;
        if (k.Keycode >= Key.Key1 && k.Keycode < Key.Key1 + n) { if (k.ShiftPressed) rival = (int)(k.Keycode - Key.Key1); else cur = (int)(k.Keycode - Key.Key1); }
        else if (k.Keycode == Key.Right) cur = (cur + 1) % n;
        else if (k.Keycode == Key.Left) cur = (cur + n - 1) % n;
        else if (k.Keycode == Key.Up) rival = (rival + 1) % n;
        else if (k.Keycode == Key.Down) rival = (rival + n - 1) % n;
        else if (k.Keycode == Key.C) cards = !cards;
        else if (k.Keycode == Key.U) ui = !ui;
        else if (k.Keycode == Key.H) { caption.Visible = !caption.Visible; return; }
        else if (k.Keycode == Key.Escape) { GetTree().Quit(); return; }
        else if (k.Keycode == Key.P)
        {
            var path = ProjectSettings.GlobalizePath("res://") + "../field_picks.txt";
            System.IO.File.AppendAllText(path, $"{DateTime.Now:yyyy-MM-dd HH:mm}  pick mine {cur + 1} {themes[cur].Name} / rival {rival + 1} {themes[rival].Name}\n");
            flash = "   ** PICKED, saved to game/field_picks.txt **";
        }
        else return;
        Show();
    }

    void Show()
    {
        foreach (var c in world.GetChildren()) c.QueueFree();
        foreach (var c in hud.GetChildren()) c.QueueFree();
        flicker.Clear();

        // with the UI on, the field sits right of the card preview and above the hand
        cam.Position = ui ? new(-1.25f, 17.0f, 12.4f) : new(0, 15.2f, 10.2f);
        cam.LookAt(ui ? new Vector3(-1.25f, 0, 2.1f) : new Vector3(0, 0, 0.75f));

        themes[cur].Env();
        themes[cur].Build(halves[0]);
        themes[rival].Build(halves[1]);
        if (cards) Cards();
        if (ui) Ui();
        var vp = GetViewport().GetVisibleRect().Size;
        caption.Position = new(ui ? vp.X * 0.215f : 24, vp.Y - 46);
        caption.Text = $"YOU {cur + 1} {themes[cur].Name}   vs   RIVAL {rival + 1} {themes[rival].Name}{flash}\n1-6 your half   Shift+1-6 rival half   arrows cycle   C cards   U ui   P pick   H hide   Esc quit";
    }

    // ---------------------------------------------------------------- zones

    /// Rows are fixed: 5 monster over 5 spell/trap, straight, 1.8 apart (a sideways card is 1.45 wide). `piles` picks how
    /// the six pile zones are arranged at the two ends.
    static List<Z> Layout(int piles)
    {
        var l = new List<Z>();
        void A(int k, float x, float z, float r = 0) => l.Add(new(k, x, z, r));
        for (int i = -2; i <= 2; i++) A(Monster, i * 1.8f, 1.45f);
        for (int i = -2; i <= 2; i++) A(Spell, i * 1.8f, 3.4f);
        switch (piles)
        {
            case 0:   // triangle at each end
                A(Field, -5.9f, 1.45f); A(Fusion, -5.9f, 3.4f); A(Side, -7.5f, 2.42f);
                A(Gy, 5.9f, 1.45f); A(Deck, 5.9f, 3.4f); A(Banish, 7.5f, 2.42f);
                break;
            case 1:   // the rows simply continue
                A(Field, -5.9f, 1.45f); A(Fusion, -5.9f, 3.4f); A(Side, -7.5f, 3.4f);
                A(Gy, 5.9f, 1.45f); A(Banish, 7.5f, 1.45f); A(Deck, 5.9f, 3.4f);
                break;
            default:  // stepped out and back like wing feathers
                A(Field, -5.8f, 1.45f, -0.3f); A(Fusion, -6.7f, 3.2f, -0.3f); A(Side, -7.6f, 4.95f, -0.3f);
                A(Gy, 5.8f, 1.45f, 0.3f); A(Deck, 6.7f, 3.2f, 0.3f); A(Banish, 7.6f, 4.95f, 0.3f);
                break;
        }
        return l;
    }

    Node3D Pivot(Z z)
    {
        var n = new Node3D { Position = new(z.X, z.Kind < Field ? BoardY : 0, z.Y), Rotation = new(0, -z.Rot, 0) };
        world.AddChild(n);
        return n;
    }

    /// Everything a half is made of: its floor, the board the two rows are set into, a kerb along the halfway line, one
    /// tray per zone and an engraved icon in each bed saying what the zone is for (a card on the zone covers it).
    void Ground(Half h, int piles, Material floor, Material board, Material trim, Material rim, Material bed, Color line, float radius, int segs, bool kerb = true)
    {
        h.Zones = Layout(piles).Select(z => h.Me ? z : new Z(z.Kind, -z.X, -z.Y, z.Rot + MathF.PI)).ToList();
        world.AddChild(new MeshInstance3D { Mesh = new PlaneMesh { Size = new(140, 55) }, Position = h.P(0, 0, 27.5f), MaterialOverride = floor });

        var glow = new StandardMaterial3D { AlbedoColor = line * 0.35f, EmissionEnabled = true, Emission = line, EmissionEnergyMultiplier = 1.3f, Roughness = 0.4f };
        var b = PlateMesh(4.72f, 1.96f, radius + 0.06f, segs, 0.16f, 0.045f, BoardY, 0.03f);
        var bp = new Node3D { Position = h.P(0, 0, 2.425f) };
        world.AddChild(bp);
        bp.AddChild(new MeshInstance3D { Mesh = b[0], MaterialOverride = trim });
        bp.AddChild(new MeshInstance3D { Mesh = b[1], MaterialOverride = glow, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off });
        bp.AddChild(new MeshInstance3D { Mesh = b[2], MaterialOverride = board });
        if (kerb) world.AddChild(new MeshInstance3D { Mesh = new BoxMesh { Size = new(19, 0.05f, 0.2f) }, Position = h.P(0, 0.025f, 0.1f), MaterialOverride = trim });

        var row = PlateMesh(0.56f, 0.78f, radius, segs, 0.085f, 0.022f, 0.006f, 0.014f);
        var tray = PlateMesh(0.56f, 0.78f, radius, segs, 0.12f, 0.09f, 0.012f, 0.014f);
        foreach (var z in h.Zones)
        {
            var p = Pivot(z);
            var m = z.Kind >= Deck ? tray : row;
            var lineMat = z.Kind == Field ? new StandardMaterial3D { AlbedoColor = Gold * 0.35f, EmissionEnabled = true, Emission = Gold, EmissionEnergyMultiplier = 1.3f } : glow;
            p.AddChild(new MeshInstance3D { Mesh = m[0], MaterialOverride = rim });
            p.AddChild(new MeshInstance3D { Mesh = m[1], MaterialOverride = lineMat, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off });
            p.AddChild(new MeshInstance3D { Mesh = m[2], MaterialOverride = bed });
            if (Img($"icons/{IconName[z.Kind]}.png") is { } icon)
                p.AddChild(new MeshInstance3D
                {
                    Mesh = new QuadMesh { Size = new(0.66f, 0.66f) }, Position = new(0, 0.024f, 0), RotationDegrees = new(-90, 0, 0),
                    CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
                    MaterialOverride = new StandardMaterial3D
                    {
                        AlbedoTexture = icon, AlbedoColor = new Color(line.Lerp(Colors.White, 0.5f), 0.34f), Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
                        Roughness = 0.35f, Metallic = 0.6f, TextureFilter = BaseMaterial3D.TextureFilterEnum.LinearWithMipmapsAnisotropic,
                    },
                });
        }
    }

    /// [rim, line, bed] for a rounded / chamfered rectangle with half size (hx, hy) measured at the bed edge. The owner
    /// line is inlaid in the top of the rim, away from the card.
    static ArrayMesh[] PlateMesh(float hx, float hy, float radius, int segs, float rimW, float rimH, float bedY, float lineW)
    {
        Vector2[] Loop(float o)     // outline pushed out by o
        {
            float r = MathF.Max(radius + o, 0.002f), cx = hx + o - r, cy = hy + o - r;
            var pts = new List<Vector2>();
            for (int c = 0; c < 4; c++)
                for (int i = 0; i <= segs; i++)
                {
                    float a = (c + i / (float)segs) * MathF.PI / 2;
                    pts.Add(new((c is 0 or 3 ? cx : -cx) + r * MathF.Cos(a), (c < 2 ? cy : -cy) + r * MathF.Sin(a)));
                }
            return pts.ToArray();
        }
        static void Tri(SurfaceTool st, Vector3 a, Vector3 b, Vector3 c)
        {
            var n = (c - a).Cross(b - a);
            if (n.Y < 0) { (b, c) = (c, b); n = -n; }
            st.SetNormal(n.Normalized());
            st.AddVertex(a); st.AddVertex(b); st.AddVertex(c);
        }
        static ArrayMesh Sweep(bool cap, params (Vector2[] Pts, float Y)[] rings)
        {
            var st = new SurfaceTool();
            st.Begin(Mesh.PrimitiveType.Triangles);
            Vector3 V(Vector2 p, float y) => new(p.X, y, p.Y);
            for (int k = 0; k + 1 < rings.Length; k++)
            {
                var (a, ya) = rings[k]; var (b, yb) = rings[k + 1];
                for (int i = 0; i < a.Length; i++)
                {
                    int j = (i + 1) % a.Length;
                    Tri(st, V(a[i], ya), V(a[j], ya), V(b[j], yb));
                    Tri(st, V(a[i], ya), V(b[j], yb), V(b[i], yb));
                }
            }
            if (cap)
            {
                var (a, ya) = rings[^1];
                for (int i = 0; i < a.Length; i++) Tri(st, new(0, ya, 0), V(a[i], ya), V(a[(i + 1) % a.Length], ya));
            }
            return st.Commit();
        }
        float mid = rimW * 0.5f;    // the line runs along the middle of the rim top
        var outer = Sweep(false, (Loop(rimW), 0f), (Loop(rimW - 0.014f), rimH), (Loop(mid + lineW / 2), rimH));
        var line = Sweep(false, (Loop(mid + lineW / 2), rimH), (Loop(mid - lineW / 2), rimH));
        var inner = Sweep(false, (Loop(mid - lineW / 2), rimH), (Loop(0.014f), rimH), (Loop(0), bedY));
        var st2 = new SurfaceTool();
        st2.Begin(Mesh.PrimitiveType.Triangles);
        st2.AppendFrom(outer, 0, Transform3D.Identity); st2.AppendFrom(inner, 0, Transform3D.Identity);
        return new[] { st2.Commit(), line, Sweep(true, (Loop(0), bedY)) };
    }

    // ---------------------------------------------------------------- sample cards + UI mock

    static readonly string[] Wanted =
    {
        "Dark Magician", "Blue-Eyes White Dragon", "Summoned Skull", "Celtic Guardian", "Red-Eyes B. Dragon", "Mystical Space Typhoon",
        "Mirror Force", "Monster Reborn", "Curse of Dragon", "Gaia The Fierce Knight", "Swords of Revealing Light", "Trap Hole",
    };
    List<CardData>? deck;

    CardData Card(int i)
    {
        if (deck == null)
        {
            var all = CardDb.Shared.ByIdx.Values;
            deck = Wanted.Select(n => all.FirstOrDefault(c => c.Name == n)).Where(c => c != null).Select(c => c!).ToList();
            if (deck.Count < 6) deck = all.Take(12).ToList();
        }
        return deck[i % deck.Count];
    }

    // a few cards so the scale and the free space can be judged; most zones stay empty on purpose
    void Cards()
    {
        var back = GD.Load<Texture2D>("res://assets/backs/01_vortex.png");
        void Put(Z z, Texture2D tex, bool sideways = false, float pile = 0)
        {
            var p = Pivot(z);
            if (pile > 0)
                p.AddChild(new MeshInstance3D
                {
                    Mesh = new BoxMesh { Size = new(1.0f, pile, 1.45f) }, Position = new(0, 0.015f + pile / 2, 0),
                    MaterialOverride = new StandardMaterial3D { AlbedoColor = new(0.70f, 0.66f, 0.58f), Roughness = 0.8f },
                });
            p.AddChild(new MeshInstance3D
            {
                Mesh = new BoxMesh { Size = new(1.0f, 0.012f, 1.45f) }, Position = new(0, 0.03f + pile, 0), RotationDegrees = new(0, sideways ? 90 : 0, 0),
                MaterialOverride = new StandardMaterial3D { AlbedoColor = new(0.1f, 0.08f, 0.06f) },
            });
            p.AddChild(new MeshInstance3D
            {
                Mesh = new QuadMesh { Size = new(1.0f, 1.45f) }, Position = new(0, 0.038f + pile, 0), RotationDegrees = new(-90, sideways ? 90 : 0, 0),
                MaterialOverride = new StandardMaterial3D { AlbedoTexture = tex, Roughness = 0.35f, TextureFilter = BaseMaterial3D.TextureFilterEnum.LinearWithMipmapsAnisotropic },
                CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            });
        }
        Z Of(Half h, int kind, int n = 0) => h.Zones.Where(z => z.Kind == kind).ElementAt(n);
        Texture2D Art(int i) => CardArt.Small(Card(i));
        var (me, them) = (halves[0], halves[1]);

        Put(Of(me, Monster, 1), Art(0)); Put(Of(me, Monster, 2), Art(3), sideways: true); Put(Of(me, Monster, 3), back, sideways: true);
        Put(Of(me, Spell, 0), back); Put(Of(me, Spell, 3), back);
        Put(Of(me, Deck), back, pile: 0.30f); Put(Of(me, Fusion), back, pile: 0.10f); Put(Of(me, Side), back, pile: 0.12f); Put(Of(me, Gy), Art(7), pile: 0.04f);
        Put(Of(them, Monster, 2), Art(1)); Put(Of(them, Monster, 0), back, sideways: true); Put(Of(them, Spell, 2), back);
        Put(Of(them, Deck), back, pile: 0.26f); Put(Of(them, Gy), Art(11), pile: 0.03f);
    }

    static StyleBoxFlat Glass(Color edge, float bg = 0.82f, int radius = 10) => new()
    {
        BgColor = new Color(0.025f, 0.028f, 0.04f, bg), BorderColor = edge,
        BorderWidthLeft = 1, BorderWidthRight = 1, BorderWidthTop = 1, BorderWidthBottom = 1,
        CornerRadiusTopLeft = radius, CornerRadiusTopRight = radius, CornerRadiusBottomLeft = radius, CornerRadiusBottomRight = radius,
        ShadowColor = new Color(0, 0, 0, 0.45f), ShadowSize = 14,
    };

    static Panel Box(Control parent, Rect2 r, StyleBox style)
    {
        var p = new Panel { Position = r.Position, Size = r.Size, MouseFilter = Control.MouseFilterEnum.Ignore };
        p.AddThemeStyleboxOverride("panel", style);
        parent.AddChild(p);
        return p;
    }

    static TextureRect Pic(Control parent, Texture2D tex, Vector2 pos, Vector2 size)
    {
        var t = new TextureRect { Texture = tex, ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.Scale, MouseFilter = Control.MouseFilterEnum.Ignore };
        parent.AddChild(t);
        t.Position = pos; t.Size = size;
        return t;
    }

    static Label Text(Control parent, string text, int size, Color c, Vector2 at, Vector2 box, HorizontalAlignment align = HorizontalAlignment.Left)
    {
        var l = new Label { Text = text, Position = at, Size = box, HorizontalAlignment = align, VerticalAlignment = VerticalAlignment.Center, ClipText = true };
        l.AddThemeFontSizeOverride("font_size", size);
        l.AddThemeColorOverride("font_color", c);
        parent.AddChild(l);
        return l;
    }

    /// Static mock of the duel HUD in the game's current glass-and-gold style, laid out on a 1600x900 sheet:
    /// card preview left, hands top and bottom, phase changer and both life totals right.
    void Ui()
    {
        var vp = GetViewport().GetVisibleRect().Size;
        hud.Scale = Vector2.One * (vp.X / 1600f);
        float H = vp.Y / hud.Scale.X;       // sheet height: 900 at 16:9, taller on 16:10
        var goldEdge = Gold * new Color(1, 1, 1, 0.45f);
        var back = GD.Load<Texture2D>("res://assets/backs/01_vortex.png");
        const int focus = 3;                // the hand card under the cursor: raised, and shown in the preview
        var sel = Card(4 + focus);

        // card preview
        var pv = Box(hud, new Rect2(20, 20, 292, H - 40), Glass(goldEdge));
        Pic(pv, CardArt.Large(sel), new(16, 16), new(260, 379));
        Text(pv, sel.Name, 21, Gold, new(16, 404), new(260, 30));
        Text(pv, sel.IsMonster ? $"{sel.Attribute}  /  {sel.Race}  /  Level {sel.Level}" : $"{sel.Frame} {(sel.IsSpell ? "Spell" : "Trap")}", 13, new Color(1, 1, 1, 0.6f), new(16, 434), new(260, 20));
        if (sel.IsMonster) Text(pv, $"ATK {sel.AtkText}     DEF {sel.DefText}", 17, Colors.White, new(16, 458), new(260, 26));
        pv.AddChild(new ColorRect { Position = new(16, 492), Size = new(260, 1), Color = goldEdge });
        var desc = new RichTextLabel { BbcodeEnabled = true, Text = LabText.Card(sel.TcgDesc ?? sel.Desc), Position = new(16, 502), Size = new(260, H - 556), ScrollActive = false, MouseFilter = Control.MouseFilterEnum.Ignore };
        desc.AddThemeFontSizeOverride("normal_font_size", 14); desc.AddThemeFontSizeOverride("bold_font_size", 14);
        desc.AddThemeColorOverride("default_color", new Color(1, 1, 1, 0.82f));
        pv.AddChild(desc);

        // life totals
        void Life(float y, string name, int lp, Color c)
        {
            var p = Box(hud, new Rect2(1356, y, 224, 104), Glass(c * new Color(1, 1, 1, 0.45f)));
            Text(p, name, 14, c, new(16, 8), new(192, 22));
            Text(p, lp.ToString(), 38, Colors.White, new(16, 28), new(192, 46));
            p.AddChild(new ColorRect { Position = new(16, 82), Size = new(192, 8), Color = new Color(0.1f, 0.1f, 0.13f) });
            p.AddChild(new ColorRect { Position = new(16, 82), Size = new(192 * lp / 8000f, 8), Color = c });
        }
        Life(20, "RIVAL", 6200, new Color(0.55f, 0.72f, 1f));
        Life(H - 124, "YOU", 8000, Gold);

        // phase changer: the six phases in order, the current one lit, plus the end-turn button
        var ph = Box(hud, new Rect2(1496, (H - 430) / 2, 84, 430), Glass(goldEdge));
        Text(ph, "TURN 3", 12, new Color(1, 1, 1, 0.55f), new(0, 8), new(84, 18), HorizontalAlignment.Center);
        string[] names = { "DP", "SP", "M1", "BP", "M2", "EP" };
        for (int i = 0; i < 6; i++)
        {
            bool now = i == 2, done = i < 2;
            var chip = Box(ph, new Rect2(12, 34 + i * 54, 60, 46), new StyleBoxFlat
            {
                BgColor = now ? Gold : new Color(1, 1, 1, done ? 0.03f : 0.07f), BorderColor = now ? Gold : Gold * new Color(1, 1, 1, done ? 0.15f : 0.4f),
                BorderWidthLeft = 1, BorderWidthRight = 1, BorderWidthTop = 1, BorderWidthBottom = 1,
                CornerRadiusTopLeft = 8, CornerRadiusTopRight = 8, CornerRadiusBottomLeft = 8, CornerRadiusBottomRight = 8,
                ShadowColor = now ? new Color(Gold, 0.5f) : Colors.Transparent, ShadowSize = now ? 12 : 0,
            });
            Text(chip, names[i], 19, now ? new Color(0.08f, 0.06f, 0.02f) : new Color(1, 1, 1, done ? 0.3f : 0.85f), Vector2.Zero, new(60, 46), HorizontalAlignment.Center);
        }
        var end = Box(ph, new Rect2(12, 366, 60, 50), Glass(new Color(1f, 0.45f, 0.35f, 0.7f), 0.5f, 8));
        Text(end, "END\nTURN", 12, new Color(1f, 0.7f, 0.62f), Vector2.Zero, new(60, 50), HorizontalAlignment.Center);

        // hands: the rival's face down along the top, yours fanned along the bottom
        const float cx = 904;
        for (int i = 0; i < 5; i++)
        {
            var r = Pic(hud, back, new(cx - 36 + (i - 2) * 58, -56 - MathF.Abs(i - 2) * 3), new(72, 104));
            r.PivotOffset = new(36, 52); r.RotationDegrees = 180 + (i - 2) * -3;
        }
        for (int i = 0; i < 6; i++)
        {
            bool hot = i == focus;
            float w = hot ? 150 : 124, h = w * 1.4575f, t = i - 2.5f;
            var c = Pic(hud, CardArt.Small(Card(4 + i)), new(cx - w / 2 + t * 104, H - (hot ? 262 : 168) + t * t * 3.5f - (h - 181)), new(w, h));
            c.PivotOffset = new(w / 2, h); c.RotationDegrees = hot ? 0 : t * 3.2f; c.ZIndex = hot ? 2 : 0;
            if (hot)
            {
                var ring = new Panel { Position = new(-3, -3), Size = new(w + 6, h + 6), MouseFilter = Control.MouseFilterEnum.Ignore };
                ring.AddThemeStyleboxOverride("panel", new StyleBoxFlat
                {
                    DrawCenter = false, BorderColor = Gold, BorderWidthLeft = 3, BorderWidthRight = 3, BorderWidthTop = 3, BorderWidthBottom = 3,
                    CornerRadiusTopLeft = 7, CornerRadiusTopRight = 7, CornerRadiusBottomLeft = 7, CornerRadiusBottomRight = 7,
                });
                c.AddChild(ring);
            }
        }
    }

    // ---------------------------------------------------------------- asset kit

    static readonly Dictionary<string, Texture2D?> texCache = new();
    static string Dir => ProjectSettings.GlobalizePath("res://assets/fieldlab/");

    static Texture2D? Img(string rel)
    {
        if (texCache.TryGetValue(rel, out var t)) return t;
        var p = Dir + rel;
        if (System.IO.File.Exists(p))
        {
            var img = Image.LoadFromFile(p);
            if (!rel.EndsWith(".hdr")) img.GenerateMipmaps();
            t = ImageTexture.CreateFromImage(img);
        }
        else GD.PushWarning($"Field Lab asset missing: {rel} (run game/tools/fieldlab_assets.py)");
        return texCache[rel] = t;
    }

    /// Scanned material, mapped in world space so every surface shares one texel density. `tile` = world units per repeat.
    static StandardMaterial3D Pbr(string id, float tile, Color? tint = null, float metal = 0, float rough = 1, float normal = 1)
    {
        var arm = Img($"{id}/arm.jpg");
        return new StandardMaterial3D
        {
            AlbedoTexture = Img($"{id}/diff.jpg"), AlbedoColor = tint ?? Colors.White,
            NormalEnabled = true, NormalTexture = Img($"{id}/nor.jpg"), NormalScale = normal,
            RoughnessTexture = arm, RoughnessTextureChannel = BaseMaterial3D.TextureChannel.Green, Roughness = rough,
            AOEnabled = true, AOTexture = arm, AOTextureChannel = BaseMaterial3D.TextureChannel.Red, Metallic = metal,
            Uv1Triplanar = true, Uv1WorldTriplanar = true, Uv1Scale = Vector3.One / tile,
            TextureFilter = BaseMaterial3D.TextureFilterEnum.LinearWithMipmapsAnisotropic,
        };
    }

    /// A Poly Haven model, scaled so its longest side is `size` units and its base sits on the floor at `pos`.
    Node3D? Model(string id, Vector3 pos, float yawDeg, float size)
    {
        var path = $"{Dir}{id}/{id}.gltf";
        if (!System.IO.File.Exists(path)) { GD.PushWarning($"Field Lab model missing: {id}"); return null; }
        var doc = new GltfDocument(); var st = new GltfState();
        if (doc.AppendFromFile(path, st) != Error.Ok) return null;
        if (doc.GenerateScene(st) is not Node3D n) return null;
        Aabb? box = null;
        void Walk(Node node, Transform3D t)
        {
            if (node is Node3D d && node != n) t *= d.Transform;
            if (node is MeshInstance3D { Mesh: not null } mi) { var a = t * mi.Mesh.GetAabb(); box = box is { } x ? x.Merge(a) : a; }
            foreach (var c in node.GetChildren()) Walk(c, t);
        }
        Walk(n, Transform3D.Identity);
        var bb = box ?? new Aabb(Vector3.Zero, Vector3.One);
        float k = size / MathF.Max(bb.Size.X, MathF.Max(bb.Size.Y, bb.Size.Z));
        var c0 = bb.GetCenter();
        n.Scale = Vector3.One * k; n.Position = -k * new Vector3(c0.X, bb.Position.Y, c0.Z);
        var holder = new Node3D { Position = pos, RotationDegrees = new(0, yawDeg, 0) };
        holder.AddChild(n);
        world.AddChild(holder);
        return holder;
    }

    void Sky(string hdri, float energy, float ambient, Color fog, float fogDensity)
    {
        env.Sky = new Sky { SkyMaterial = new PanoramaSkyMaterial { Panorama = Img($"{hdri}/{hdri}.hdr") } };
        env.BackgroundEnergyMultiplier = energy; env.AmbientLightSkyContribution = 1; env.AmbientLightEnergy = ambient;
        env.FogLightColor = fog; env.FogDensity = fogDensity;
    }

    void Sun(Vector3 deg, Color c, float energy) { sun.RotationDegrees = deg; sun.LightColor = c; sun.LightEnergy = energy; }

    OmniLight3D Glow(Vector3 pos, Color c, float energy, float range, bool shadow = false)
    {
        var l = new OmniLight3D { Position = pos, LightColor = c, LightEnergy = energy, OmniRange = range, OmniAttenuation = 1.4f, ShadowEnabled = shadow, ShadowBlur = 2 };
        world.AddChild(l);
        flicker.Add((l, energy, pos.X * 3.1f + pos.Z));
        return l;
    }

    /// The pool of light over the play area, so the board always reads whatever the scene's mood is.
    void KeyLight(Color c, float energy)
    {
        var l = new SpotLight3D { Position = new(0, 15, 3), LightColor = c, LightEnergy = energy, SpotAngle = 54, SpotRange = 36, SpotAngleAttenuation = 2.0f, ShadowEnabled = true, ShadowBlur = 2.5f };
        world.AddChild(l);
        l.LookAt(new Vector3(0, 0, 0.3f), Vector3.Forward);
    }

    /// Drifting glowing specks (embers, dust in a light shaft, snow). Kept off the rows so nothing crosses the cards.
    void Motes(Vector3 pos, Vector3 box, Color c, int amount, Vector3 drift, float size, float life = 5)
    {
        var mat = new StandardMaterial3D
        {
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded, BlendMode = BaseMaterial3D.BlendModeEnum.Add, AlbedoColor = c,
            BillboardMode = BaseMaterial3D.BillboardModeEnum.Particles, VertexColorUseAsAlbedo = true,
            AlbedoTexture = new GradientTexture2D
            {
                Width = 32, Height = 32, Fill = GradientTexture2D.FillEnum.Radial, FillFrom = new(0.5f, 0.5f), FillTo = new(0.5f, 0),
                Gradient = new Gradient { Colors = new[] { Colors.White, Colors.Black } },
            },
        };
        var fade = new Gradient { Offsets = new[] { 0f, 0.2f, 0.8f, 1f }, Colors = new[] { Colors.Black, Colors.White, Colors.White, Colors.Black } };
        world.AddChild(new GpuParticles3D
        {
            Position = pos, Amount = amount, Lifetime = life, Preprocess = life, VisibilityAabb = new Aabb(-box * 2 - Vector3.One * 6, box * 4 + Vector3.One * 12),
            DrawPass1 = new QuadMesh { Size = Vector2.One * size, Material = mat },
            ProcessMaterial = new ParticleProcessMaterial
            {
                EmissionShape = ParticleProcessMaterial.EmissionShapeEnum.Box, EmissionBoxExtents = box,
                Direction = drift.Normalized(), Spread = 25, InitialVelocityMin = drift.Length() * 0.5f, InitialVelocityMax = drift.Length() * 1.5f,
                Gravity = Vector3.Zero, ScaleMin = 0.5f, ScaleMax = 1.5f, TurbulenceEnabled = true, TurbulenceNoiseStrength = 0.6f, TurbulenceNoiseScale = 3,
                ColorRamp = new GradientTexture1D { Gradient = fade },
            },
        });
    }

    const string Noise = @"
float h1(vec2 p){ return fract(sin(dot(p, vec2(127.1, 311.7))) * 43758.5453); }
float vn(vec2 p){ vec2 i = floor(p), f = fract(p); f = f * f * f * (f * (f * 6.0 - 15.0) + 10.0); return mix(mix(h1(i), h1(i + vec2(1, 0)), f.x), mix(h1(i + vec2(0, 1)), h1(i + vec2(1, 1)), f.x), f.y); }
float fbm(vec2 p){ float a = 0.5, s = 0.0; for (int i = 0; i < 5; i++) { s += a * vn(p); p = mat2(vec2(0.8, -0.6), vec2(0.6, 0.8)) * p * 2.03 + 7.0; a *= 0.5; } return s; }
";

    // a living flame: noise streams upward through a tapering body every frame (no sprite frames)
    const string FlameCode = @"shader_type spatial; render_mode unshaded, blend_add, cull_disabled, depth_draw_never, shadows_disabled;
uniform float seed = 0.0; uniform float energy = 1.0;" + Noise + @"
void vertex(){
	mat4 mv = VIEW_MATRIX * mat4(INV_VIEW_MATRIX[0], INV_VIEW_MATRIX[1], INV_VIEW_MATRIX[2], MODEL_MATRIX[3]);
	MODELVIEW_MATRIX = mv; MODELVIEW_NORMAL_MATRIX = mat3(mv);
}
void fragment(){
	float y = 1.0 - UV.y, t = TIME * 1.5 + seed;
	float n1 = fbm(vec2(UV.x * 2.6 + seed, y * 2.0 - t));
	float n2 = fbm(vec2(UV.x * 5.5 - seed, y * 3.6 - t * 1.6));
	float x = UV.x - 0.5 + (n1 - 0.5) * 0.42 * y;
	float body = 1.0 - smoothstep(0.0, 0.40 * (1.0 - y * 0.9) + 0.02, abs(x));
	float f = clamp(body * (1.05 - y) * 1.5 + (n2 - 0.5) * 0.7 * body, 0.0, 1.0) * smoothstep(0.0, 0.06, y);
	vec3 c = mix(vec3(1.0, 0.22, 0.02), vec3(1.0, 0.70, 0.18), smoothstep(0.25, 0.65, f));
	c = mix(c, vec3(1.0, 0.95, 0.80), smoothstep(0.75, 1.0, f));
	ALBEDO = c * smoothstep(0.08, 0.45, f) * energy * 1.8;
}";

    // molten surface: two noise layers sliding against each other, so the crust drifts and breaks continuously
    const string MoltenCode = @"shader_type spatial; render_mode unshaded;
uniform float energy = 1.0; uniform bool round = false;
varying vec3 wp;" + Noise + @"
void vertex(){ wp = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz; }
void fragment(){
	vec2 p = wp.xz; float t = TIME * 0.22;
	float a = fbm(p * vec2(0.9, 2.2) + vec2(t, t * 0.3));
	float b = fbm(p * vec2(1.6, 3.1) - vec2(t * 1.4, -t * 0.5) + a * 1.2);
	float crust = smoothstep(0.42, 0.60, mix(a, b, 0.6));
	vec2 e = min(UV, 1.0 - UV);
	float edge = smoothstep(0.0, 0.30, (round ? (0.5 - length(UV - 0.5)) * 1.6 : min(e.x * 6.0, e.y)) + (b - 0.5) * 0.25);
	float pulse = 0.88 + 0.12 * sin(TIME * 1.3 + a * 12.0);
	vec3 c = mix(vec3(3.0, 1.0, 0.13), vec3(0.42, 0.05, 0.0), crust) * energy * pulse;
	ALBEDO = mix(vec3(0.015, 0.012, 0.01), c, edge * (1.0 - crust * 0.7));
}";

    void Molten(Vector3 pos, Vector2 size, float energy = 1, bool round = false)
    {
        var m = new ShaderMaterial { Shader = moltenShader };
        m.SetShaderParameter("energy", energy); m.SetShaderParameter("round", round);
        world.AddChild(new MeshInstance3D { Mesh = new PlaneMesh { Size = size }, Position = pos, MaterialOverride = m, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off });
    }

    /// A fire: a few overlapping flame sheets that always face the camera, its light and a thread of sparks.
    void Fire(Vector3 pos, float size, float light, float range)
    {
        for (int i = 0; i < 4; i++)
        {
            var m = new ShaderMaterial { Shader = flameShader };
            m.SetShaderParameter("seed", pos.X * 1.7f + i * 3.3f); m.SetShaderParameter("energy", i == 0 ? 1f : 0.7f);
            float w = size * (i == 0 ? 0.9f : 0.6f), h = size * (i == 0 ? 1.5f : 1.0f + 0.15f * i);
            var off = new Vector3((i - 1.5f) * 0.16f * size * (i == 0 ? 0 : 1), h * 0.28f, -h * 0.30f + (i % 2) * 0.1f * size);
            world.AddChild(new MeshInstance3D { Mesh = new QuadMesh { Size = new(w, h) }, Position = pos + off, MaterialOverride = m, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off });
        }
        Glow(pos + Vector3.Up * size * 0.8f, new(1.0f, 0.52f, 0.16f), light, range, true);
        Motes(pos + Vector3.Up * size * 0.6f, new(size * 0.2f, 0.1f, size * 0.2f), new(1.0f, 0.5f, 0.12f), 14, new(0, 1.2f, 0), 0.06f, 2.2f);
    }

    // ---------------------------------------------------------------- themes: Env = the mood of your side, Build = one half

    void CastleEnv()
    {
        Sky("castle_zavelstein_cellar", 0.3f, 0.7f, new(0.05f, 0.04f, 0.035f), 0.008f);
        Sun(new(-62, 28, 0), new(0.62f, 0.72f, 1.0f), 0.9f);
        KeyLight(new(1.0f, 0.88f, 0.72f), 26);
    }

    void Castle(Half h)
    {
        var iron = Pbr("metal_plate", 2.2f, new(0.62f, 0.60f, 0.60f), metal: 0.55f, rough: 0.8f);
        Ground(h, 0, Pbr("monastery_stone_floor", 6.5f, new(0.92f, 0.90f, 0.88f), normal: 1.4f),
            board: Pbr("slate_floor_02", 3.4f, new(0.85f, 0.88f, 1.0f), rough: 0.8f), trim: iron, rim: iron,
            bed: Pbr("metal_plate", 1.3f, new(0.42f, 0.44f, 0.54f), metal: 0.3f, rough: 0.7f), line: new(1.0f, 0.70f, 0.22f), radius: 0.10f, segs: 1);
        Model("stone_fire_pit", h.P(-7.9f, 0, 6.9f), h.Yaw(20), 3.0f);
        Molten(h.P(-7.9f, 0.25f, 6.9f), new(1.85f, 1.85f), 0.7f, round: true);
        Fire(h.P(-7.9f, 0.28f, 6.9f), 1.3f, 8, 13);
        Model("gothic_statue", h.P(8.3f, 0, 7.4f), h.Yaw(-150), 4.8f);
        Model("brass_candleholders", h.P(4.6f, 0, 7.3f), h.Yaw(30), 2.4f);
        Glow(h.P(4.6f, 1.3f, 7.3f), new(1.0f, 0.66f, 0.30f), 2.6f, 6);
        Model("brass_candleholders", h.P(-3.6f, 0, 7.6f), h.Yaw(200), 2.2f);
        Glow(h.P(-3.6f, 1.2f, 7.6f), new(1.0f, 0.66f, 0.30f), 2.2f, 6);
    }

    void FrostEnv()
    {
        Sky("snowy_forest", 0.6f, 0.9f, new(0.72f, 0.80f, 0.90f), 0.006f);
        Sun(new(-42, -38, 0), new(1.0f, 0.95f, 0.88f), 1.2f);
        KeyLight(new(0.9f, 0.95f, 1.0f), 5);
    }

    void Frost(Half h)
    {
        var steel = Pbr("metal_plate", 2.2f, new(0.62f, 0.70f, 0.80f), metal: 0.6f, rough: 0.7f);
        Ground(h, 1, Pbr("snow_02", 7f, new(0.86f, 0.90f, 0.96f), normal: 1.3f),
            board: Pbr("slate_floor_02", 3.4f, new(0.55f, 0.68f, 0.86f), rough: 0.55f), trim: steel, rim: steel,
            bed: Pbr("metal_plate", 1.3f, new(0.28f, 0.38f, 0.56f), metal: 0.35f, rough: 0.6f), line: new(0.25f, 0.95f, 0.85f), radius: 0.14f, segs: 6);
        Model("boulder_01", h.P(-8.3f, 0, 6.7f), h.Yaw(30), 3.2f);
        Model("namaqualand_boulder_02", h.P(8.5f, 0, 6.8f), h.Yaw(110), 3.0f);
        Model("coast_rocks_01", h.P(-7.5f, 0, 8.6f), h.Yaw(0), 7f);
        Model("coast_rocks_01", h.P(8.0f, 0, 8.9f), h.Yaw(170), 7f);
        Motes(h.P(0, 5, 7.5f), new(13, 3, 2.2f), new(0.9f, 0.95f, 1.0f), 90, new(0.25f, -0.9f, 0.1f), 0.07f, 7);
    }

    void ForestEnv()
    {
        Sky("mossy_forest", 0.5f, 0.85f, new(0.22f, 0.30f, 0.16f), 0.006f);
        Sun(new(-55, 40, 0), new(1.0f, 0.94f, 0.78f), 1.3f);
        KeyLight(new(1.0f, 0.97f, 0.85f), 5);
    }

    void Forest(Half h)
    {
        var rock = Pbr("mossy_rock", 2.6f, new(0.85f, 0.85f, 0.80f), normal: 1.2f);
        Ground(h, 2, Pbr("mossy_cobblestone", 6f, normal: 1.3f),
            board: Pbr("sandstone_blocks_05", 3.2f, new(0.50f, 0.56f, 0.44f)), trim: rock, rim: rock,
            bed: Pbr("slate_floor_02", 1.6f, new(0.30f, 0.38f, 0.30f), rough: 0.8f), line: new(0.55f, 1.0f, 0.40f), radius: 0.20f, segs: 8);
        Model("tree_stump_01", h.P(-4.2f, 0, 7.2f), h.Yaw(40), 3.4f);
        Model("root_cluster_01", h.P(3.6f, 0, 7.4f), h.Yaw(200), 3.8f);
        Model("rock_moss_set_01", h.P(-6.5f, 0, 8.4f), h.Yaw(20), 6.5f);
        Model("rock_moss_set_01", h.P(6.0f, 0, 8.8f), h.Yaw(190), 6.5f);
        foreach (var (x, z, y) in new[] { (-1.2f, 6.6f, 0f), (6.4f, 7.2f, 70f), (-7.0f, 7.4f, 140f), (0.5f, 8.0f, 200f), (-2.4f, 8.6f, 300f), (8.9f, 7.6f, 20f) })
            Model("fern_02", h.P(x, 0, z), h.Yaw(y), 2.2f);
    }

    void MarbleEnv()
    {
        Sky("small_cathedral", 0.6f, 1.0f, new(0.30f, 0.28f, 0.25f), 0.003f);
        Sun(new(-58, -32, 0), new(1.0f, 0.95f, 0.85f), 1.0f);
        KeyLight(new(1.0f, 0.96f, 0.90f), 5);
    }

    void Marble(Half h)
    {
        var brass = Pbr("metal_plate", 2.2f, new(1.0f, 0.74f, 0.34f), metal: 0.85f, rough: 0.45f);
        Ground(h, 0, Pbr("marble_tiles", 16f, new(0.92f, 0.92f, 0.92f)),
            board: Pbr("marble_01", 5f, new(0.93f, 0.93f, 0.95f), rough: 0.35f), trim: brass, rim: brass,
            bed: Pbr("marble_01", 1.8f, new(0.20f, 0.20f, 0.29f), rough: 0.4f), line: new(1.0f, 0.82f, 0.45f), radius: 0.14f, segs: 2);
        Model("marble_bust_01", h.P(-8.2f, 0, 6.9f), h.Yaw(150), 3.4f);
        Model("marble_bust_01", h.P(8.2f, 0, 6.9f), h.Yaw(-150), 3.4f);
        Model("horse_statue_01", h.P(-4.2f, 0, 8.6f), h.Yaw(150), 5.0f);
        Model("horse_statue_01", h.P(4.2f, 0, 8.6f), h.Yaw(210), 5.0f);
        Model("brass_vase_01", h.P(0, 0, 7.2f), h.Yaw(0), 2.2f);
    }

    void CalderaEnv()
    {
        Sky("smelting_tower_interior", 0.2f, 0.55f, new(0.10f, 0.035f, 0.02f), 0.012f);
        Sun(new(-66, 12, 0), new(0.75f, 0.78f, 0.95f), 0.7f);
        KeyLight(new(1.0f, 0.90f, 0.80f), 24);
    }

    void Caldera(Half h)
    {
        var rust = Pbr("rusty_metal_02", 2.2f, new(0.85f, 0.80f, 0.78f), metal: 0.5f, rough: 0.85f);
        Ground(h, 1, Pbr("volcanic_rock_tiles", 6f, new(0.80f, 0.78f, 0.78f), normal: 1.5f),
            board: Pbr("rusty_metal_02", 4f, new(0.85f, 0.78f, 0.76f), metal: 0.35f, rough: 0.8f), trim: rust, rim: rust,
            bed: Pbr("slate_floor_02", 1.6f, new(0.50f, 0.52f, 0.62f), rough: 0.75f), line: new(1.0f, 0.50f, 0.10f), radius: 0.16f, segs: 1, kerb: false);
        Molten(h.P(0, 0.02f, 0.17f), new(26, 0.34f));                      // this side's half of the lava channel
        foreach (float x in new[] { -8f, -4f, 0f, 4f, 8f }) Glow(h.P(x, 0.7f, 0.2f), new(1.0f, 0.42f, 0.10f), 1.6f, 5);
        Model("namaqualand_boulder_02", h.P(-8.5f, 0, 6.8f), h.Yaw(60), 3.4f);
        Model("boulder_01", h.P(8.6f, 0, 6.7f), h.Yaw(250), 3.2f);
        Model("coast_rocks_01", h.P(-7.0f, 0, 8.8f), h.Yaw(40), 7f);
        Model("coast_rocks_01", h.P(7.5f, 0, 8.6f), h.Yaw(220), 7f);
        Motes(h.P(0, 0.7f, 0.2f), new(11, 0.3f, 0.12f), new(1.0f, 0.5f, 0.12f), 30, new(0, 0.9f, 0), 0.07f, 3);
    }

    void RuinEnv()
    {
        Sky("qwantani_sunset", 0.8f, 0.9f, new(0.85f, 0.55f, 0.38f), 0.004f);
        Sun(new(-30, 62, 0), new(1.0f, 0.74f, 0.48f), 1.5f);
        KeyLight(new(1.0f, 0.92f, 0.82f), 5);
    }

    void Ruin(Half h)
    {
        var stone = Pbr("sandstone_blocks_05", 2.4f, new(0.80f, 0.70f, 0.58f));
        Ground(h, 2, Pbr("red_sandstone_pavement", 6f, normal: 1.3f),
            board: Pbr("sandstone_blocks_05", 4.2f, new(1.0f, 0.93f, 0.82f)), trim: stone, rim: stone,
            bed: Pbr("slate_floor_02", 1.6f, new(0.24f, 0.32f, 0.42f), rough: 0.75f), line: new(0.25f, 0.95f, 1.0f), radius: 0.26f, segs: 8);
        Model("ceramic_vase_01", h.P(-4.4f, 0, 7.3f), h.Yaw(0), 2.8f);
        Model("antique_ceramic_vase_01", h.P(-3.0f, 0, 7.9f), h.Yaw(80), 2.0f);
        Model("namaqualand_boulder_02", h.P(0.4f, 0, 8.6f), h.Yaw(130), 3.6f);
        Model("brass_pot_01", h.P(3.5f, 0, 7.6f), h.Yaw(30), 1.8f);
        Model("coast_rocks_01", h.P(-6.5f, 0, 8.8f), h.Yaw(80), 6.5f);
        Model("ceramic_vase_01", h.P(6.6f, 0, 8.0f), h.Yaw(0), 3.2f);
    }
}
