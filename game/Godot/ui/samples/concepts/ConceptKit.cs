using Godot;
using PoC.Engine;

namespace PoC.Game.UI;

/// Shared bits for the main-menu concept round (2026-11-08, "back to the drawing board"): 3D stage in a SubViewport,
/// card quads, shader rects, menu item descriptions and the card each item shows off.
public static class ConceptKit
{
    public static readonly string[] Blurb =
    {
        "Challenge Yugi, Kaiba, Joey or Jaden", "Eight duelists. One crown.", "Duel a friend near or far",
        "Build the deck that answers", "Every card you have won", "Spend your DP on packs and cosmetics",
        "Sound, screen and profile", "Leave the realm",
    };
    /// The card each menu item calls up (by name).
    public static readonly string[] Featured =
    {
        "Dark Magician", "Exodia the Forbidden One", "Blue-Eyes White Dragon", "Black Luster Soldier - Envoy of the Beginning",
        "Dark Magician Girl", "Kuriboh", "Time Wizard", "Chaos Emperor Dragon - Envoy of the End",
    };
    public static readonly string[] Ring =
    {
        "Dark Magician", "Blue-Eyes White Dragon", "Red-Eyes B. Dragon", "Exodia the Forbidden One", "Summoned Skull",
        "Dark Magician Girl", "Black Luster Soldier - Envoy of the Beginning", "Chaos Emperor Dragon - Envoy of the End",
        "Elemental HERO Neos", "Kuriboh", "Time Wizard", "Obelisk the Tormentor", "Slifer the Sky Dragon",
        "The Winged Dragon of Ra", "Magician of Black Chaos", "Elemental HERO Flame Wingman",
    };
    public static readonly Color Cream = new(0.93f, 0.9f, 0.82f), Gold = new(1f, 0.8f, 0.42f);

    public static CardData Card(string name) => CardDb.Shared.Named(name);
    public static Texture2D Full(string name) => CardArt.Large(Card(name));
    public static Texture2D Art(string name) => CardArt.Picture(Card(name));
    public static Texture2D Tex(string path) => GD.Load<Texture2D>(path);

    /// A full-screen 3D stage behind the 2D UI. Returns the 3D root and its camera.
    public static (Node3D Root, Camera3D Cam, SubViewport Vp) Stage(Control host)
    {
        var box = new SubViewportContainer { Stretch = false, MouseFilter = Control.MouseFilterEnum.Ignore };
        box.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        box.Size = host.GetViewportRect().Size;
        var vp = new SubViewport { Size = (Vector2I)host.GetViewportRect().Size, OwnWorld3D = true, Msaa3D = Viewport.Msaa.Msaa4X, TransparentBg = false, HandleInputLocally = false };
        box.AddChild(vp);
        host.AddChild(box);
        var root = new Node3D();
        vp.AddChild(root);
        var cam = new Camera3D { Current = true, Fov = 42 };
        root.AddChild(cam);
        return (root, cam, vp);
    }

    /// A card as a thin double-sided quad (front = the scan, back = our card back).
    public static Node3D CardQuad(Texture2D front, float h = 1.46f, float emission = 0.25f)
    {
        var n = new Node3D();
        float w = h * 0.686f;
        MeshInstance3D Face(Texture2D t, bool back)
        {
            var m = new StandardMaterial3D { AlbedoTexture = t, EmissionEnabled = true, Emission = Colors.White, EmissionTexture = t, EmissionEnergyMultiplier = emission,
                Roughness = 0.35f, Metallic = 0.1f, TextureFilter = BaseMaterial3D.TextureFilterEnum.LinearWithMipmapsAnisotropic };
            var mi = new MeshInstance3D { Mesh = new QuadMesh { Size = new Vector2(w, h) }, MaterialOverride = m };
            if (back) mi.RotationDegrees = new Vector3(0, 180, 0);
            return mi;
        }
        n.AddChild(Face(front, false));
        n.AddChild(Face(Tex("res://assets/title/card_back.bmp"), true));
        return n;
    }

    public static ColorRect ShaderRect(Control host, string code, Vector2 pos, Vector2 size)
    {
        var r = new ColorRect { Position = pos, Size = size, MouseFilter = Control.MouseFilterEnum.Ignore, Material = new ShaderMaterial { Shader = new Shader { Code = code } } };
        host.AddChild(r);
        return r;
    }


    public static Label Say(Control host, string s, Font f, int size, Color c, Vector2 pos, Vector2 box = default, HorizontalAlignment a = HorizontalAlignment.Left)
        => Kit.Put(host, s, f, size, c, pos, box, a);

    public static Font Sys(string[] names, int weight = 400, bool italic = false, int stretch = 100) =>
        new SystemFont { FontNames = names, FontWeight = weight, FontItalic = italic, FontStretch = stretch, Antialiasing = TextServer.FontAntialiasing.Gray, Oversampling = 2f };
}
