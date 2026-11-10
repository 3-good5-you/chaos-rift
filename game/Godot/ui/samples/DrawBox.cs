using Godot;

namespace PoC.Game.UI;

/// A control that paints itself with a callback (arcs, rings, wedges) for the UI samples.
public partial class DrawBox : Control
{
    public System.Action<DrawBox>? Paint;
    public override void _Draw() => Paint?.Invoke(this);
}
