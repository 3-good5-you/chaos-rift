using Godot;

namespace PoC.Game.UI;

/// Startup: a plain near-black screen (the same black the boot splash and the menu intro use) while the main menu
/// loads on a thread; the menu then plays the Chaos Rift intro itself (MainMenu.Intro).
public partial class Loading : Control
{
    const string Menu = "res://ui/MainMenu.tscn";

    public override void _Ready()
    {
        SetAnchorsPreset(LayoutPreset.FullRect);
        AddChild(new ColorRect { AnchorRight = 1, AnchorBottom = 1, Color = new Color(0.008f, 0.008f, 0.012f) });
        ResourceLoader.LoadThreadedRequest(Menu);
    }

    public override void _Process(double delta)
    {
        if (ResourceLoader.LoadThreadedGetStatus(Menu) != ResourceLoader.ThreadLoadStatus.Loaded) return;
        SetProcess(false);
        GetTree().ChangeSceneToPacked((PackedScene)ResourceLoader.LoadThreadedGet(Menu));
    }
}
