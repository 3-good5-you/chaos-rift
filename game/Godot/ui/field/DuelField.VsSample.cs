using Godot;
using PoC.Engine;

namespace PoC.Game.UI;

/// LAN "connected" screen samples (not wired), shot inside the real 3D duel world: both players' forged mats meet
/// on the table, their worlds below, each player's framed icon hovers over their half as a medallion on a pillar of
/// light. `--vsview=h|i|j [--mat=forge:X --mat2=forge:Y]`
/// h = side-on: the table edge-on, you left, the rival right, the clash front between.
/// i = at the table: low behind your half, the rival's medallion towering over the far side.
/// j = high orbit: the whole arena from above at an angle, both pillars rising out of the worlds.
public partial class DuelField
{
    string? vsView;
    readonly List<(Node3D Node, float Y, float Phase)> bobbing = new();

    void VsView()
    {
        bool side = vsView == "h", table = vsView == "i";
        var youAt = new Vector3(0, 0, 1.25f);
        var themAt = new Vector3(0, 0, -1.25f);
        float height = table ? 1.5f : 1.9f;
        bool high = vsView == "j";
        Medallion(Settings.Avatar, Session.Profile.Frame == Shop.DefaultFrame ? "gold" : Session.Profile.Frame, youAt + new Vector3(table ? -1.45f : 0, table ? 1.75f : height, table ? 0.3f : 0), Pm.You, table ? 1.4f : high ? 2.3f : 1.7f);
        Medallion(Settings.PictureCard(5) is { } c ? CardArt.Picture(c) : null, "dark", themAt + new Vector3(table ? 0.6f : 0, table ? 2.4f : height, table ? -0.6f : 0), Pm.Them, table ? 2.3f : high ? 2.3f : 1.7f);

        cam.HOffset = 0;
        switch (vsView)
        {
            case "h": cam.Fov = 38; cam.Position = new Vector3(9.4f, 2.6f, 0); cam.LookAt(new Vector3(0, 1.05f, 0)); break;
            case "i": cam.Fov = 46; cam.Position = new Vector3(0.6f, 2.1f, 5.6f); cam.LookAt(new Vector3(0, 1.6f, -1.2f)); break;
            default: cam.Fov = 32; cam.Position = new Vector3(6.6f, 8.2f, 6.4f); cam.LookAt(new Vector3(0, 1.1f, 0)); break;
        }
        camHome = cam.Position;

        // Overlay: name plates and the VS splash (Master Duel style), the bottom bar.
        var hud = new Control { Size = new Vector2(W, H), MouseFilter = MouseFilterEnum.Ignore };
        AddChild(hud);
        hud.AddChild(new ColorRect { Size = new Vector2(W, H), MouseFilter = MouseFilterEnum.Ignore, Material = Mat(@"shader_type canvas_item;
void fragment() { vec2 p = (UV - 0.5) * vec2(1.6, 1.0); COLOR = vec4(0.0, 0.0, 0.0, smoothstep(0.45, 1.05, length(p)) * 0.75); }") });
        float barY = H - 120;
        Plate(hud, Settings.Avatar, Session.Profile.Frame == Shop.DefaultFrame ? "gold" : Session.Profile.Frame, Settings.PlayerName.ToUpperInvariant(),
            "YOUR DECK  ·  " + (Session.Playing?.Name ?? "DRAGON FURY").ToUpperInvariant(), Pm.You, side ? new Vector2(70, barY - 230) : new Vector2(70, barY - 230), false);
        Plate(hud, Settings.PictureCard(5) is { } c2 ? CardArt.Picture(c2) : null, "dark", "MARIK", "PICKING A DECK…", Pm.Them, new Vector2(W - 70 - 640, 60), true);
        var vs = Kit.Put(hud, "VS", Kit.Impact, 200, Colors.White, new Vector2(side ? W / 2 - 250 : table ? W * 0.27f - 250 : W * 0.24f - 250, side ? 70 : table ? 110 : 140), new Vector2(500, 260), HorizontalAlignment.Center, 22);
        vs.AddThemeColorOverride("font_outline_color", new Color(0.25f, 0.12f, 0.02f));
        hud.AddChild(new ColorRect { Position = new Vector2(0, barY), Size = new Vector2(W, 120), Material = Mat(RivalPanels.Bar), MouseFilter = MouseFilterEnum.Ignore });
        Kit.Put(hud, "LAN DUEL", Kit.Cond, 50, Colors.White, new Vector2(64, barY + 29));
        hud.AddChild(new ColorRect { Position = new Vector2(68, barY + 91), Size = new Vector2(110, 3), Color = RivalPanels.Gold });
        hud.AddChild(Chips.Make("LEAVE", new Vector2(W - 640, barY + 28), new Vector2(200, 64), () => { }));
        var go = Chips.Make("READY", new Vector2(W - 410, barY + 22), new Vector2(330, 76), () => { }, 30);
        Chips.SetOn(go, true);
        hud.AddChild(go);
    }

    /// The framed icon rendered to a texture and hung in the world as a glowing medallion on a pillar of light.
    void Medallion(Texture2D? pic, string frame, Vector3 at, Color col, float size)
    {
        var vp = new SubViewport { Size = new Vector2I(512, 512), TransparentBg = true, RenderTargetUpdateMode = SubViewport.UpdateMode.Always };
        vp.AddChild(Cosmetics.Framed(pic, frame, 512));
        AddChild(vp);
        var node = new Node3D { Position = at };
        world.AddChild(node);
        node.AddChild(new Sprite3D
        {
            Texture = vp.GetTexture(), PixelSize = size / 512f, Billboard = BaseMaterial3D.BillboardModeEnum.Enabled, Shaded = false,
            AlphaCut = SpriteBase3D.AlphaCutMode.Disabled, TextureFilter = BaseMaterial3D.TextureFilterEnum.LinearWithMipmaps, RenderPriority = 1,
        });
        // Halo behind it.
        node.AddChild(new MeshInstance3D
        {
            Mesh = new QuadMesh { Size = new Vector2(size * 2.4f, size * 2.4f) }, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            MaterialOverride = Shade(@"shader_type spatial;
render_mode unshaded, blend_add, depth_draw_never, cull_disabled;
uniform vec4 col : source_color;
void vertex() { MODELVIEW_MATRIX = VIEW_MATRIX * mat4(INV_VIEW_MATRIX[0], INV_VIEW_MATRIX[1], INV_VIEW_MATRIX[2], MODEL_MATRIX[3]); }
void fragment() {
  vec2 p = UV - 0.5; float r = length(p) * 2.0;
  float rays = pow(0.5 + 0.5 * sin(atan(p.y, p.x) * 14.0 + TIME * 0.8), 8.0) * smoothstep(1.0, 0.3, r) * 0.5;
  ALBEDO = col.rgb * (pow(max(0.0, 1.0 - r), 2.5) * 1.6 + rays);
}", ("col", col)),
        });
        bobbing.Add((node, at.Y, at.X + at.Z));
        // Pillar: from deep below the table up to the medallion.
        var pillar = new MeshInstance3D
        {
            Mesh = new CylinderMesh { TopRadius = 0.32f * size, BottomRadius = 0.5f * size, Height = at.Y + 8, RadialSegments = 48, CapTop = false, CapBottom = false },
            Position = new Vector3(at.X, (at.Y - 8) / 2, at.Z), CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            MaterialOverride = Shade(@"shader_type spatial;
render_mode unshaded, blend_add, depth_draw_never, cull_disabled;
uniform vec4 col : source_color;
void fragment() {
  float edge = pow(1.0 - abs(dot(NORMAL, VIEW)), 2.0);
  float streak = 0.6 + 0.4 * sin(UV.x * 40.0 + UV.y * 8.0 - TIME * 3.0);
  float fade = smoothstep(0.0, 0.25, UV.y) * smoothstep(1.0, 0.85, UV.y);
  ALBEDO = col.rgb * edge * streak * fade * 1.4;
}", ("col", col)),
        };
        world.AddChild(pillar);
        // Rune ring where the pillar pierces the mat.
        world.AddChild(new MeshInstance3D
        {
            Mesh = new QuadMesh { Size = new Vector2(size * 2.2f, size * 2.2f) }, RotationDegrees = new Vector3(-90, 0, 0), Position = new Vector3(at.X, 0.03f, at.Z),
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            MaterialOverride = Shade(@"shader_type spatial;
render_mode unshaded, blend_add, depth_draw_never, cull_disabled;
uniform vec4 col : source_color;
void fragment() {
  vec2 p = UV - 0.5; float r = length(p) * 2.0, a = atan(p.y, p.x);
  float ring = exp(-abs(r - 0.82) * 60.0) + exp(-abs(r - 0.62) * 80.0) * 0.7;
  ring += step(0.55, fract(a * 4.0 / 3.1416 + TIME * 0.15)) * exp(-abs(r - 0.72) * 140.0);
  ALBEDO = col.rgb * (ring * 1.5 + pow(max(0.0, 1.0 - r), 2.0) * 0.5);
}", ("col", col)),
        });
        world.AddChild(new OmniLight3D { Position = at, LightColor = col, LightEnergy = 3.5f, OmniRange = 5 });
    }

    void Plate(Control parent, Texture2D? pic, string frame, string name, string sub, Color col, Vector2 at, bool right)
    {
        // Text only: the medallion in the world is the icon. A coloured bar on the outer edge.
        var p = Kit.Box(parent, new Rect2(at + new Vector2(right ? 160 : 0, 30), new Vector2(480, 150)), Kit.Glass(col * new Color(1, 1, 1, 0.5f), 0.2f, 20));
        p.AddChild(new ColorRect { Position = new Vector2(right ? 480 - 10 : 0, 20), Size = new Vector2(10, 110), Color = col });
        var align = right ? HorizontalAlignment.Right : HorizontalAlignment.Left;
        Kit.Put(p, name, Kit.Impact, 72, Colors.White, new Vector2(28, 10), new Vector2(424, 94), align, 10);
        Kit.Put(p, sub, Kit.Cond, 26, col, new Vector2(28, 100), new Vector2(424, 34), align);
    }

    void VsAnimate()
    {
        float t = (float)Time.GetTicksMsec() / 1000f;
        foreach (var (n, y, ph) in bobbing) n.Position = n.Position with { Y = y + Mathf.Sin(t * 1.3f + ph) * 0.06f };
        cam.Position = camHome + new Vector3(Mathf.Sin(t * 0.15f) * 0.25f, 0, Mathf.Cos(t * 0.15f) * 0.1f);
    }
}
