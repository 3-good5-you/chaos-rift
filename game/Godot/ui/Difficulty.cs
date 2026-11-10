using Godot;
using PoC.Engine;

namespace PoC.Game.UI;

/// A rival's automatic difficulty: seven slanted level pips and the tier word, drawn in the strip
/// under the rival's panel.
public static class Difficulty
{
    public const float Width = 7 * 27 + 12 + 110;
    static Font? font;

    static Polygon2D Pip(float x, float y, Color c) => new()
    {
        Polygon = new[] { new Vector2(x + 4, y), new Vector2(x + 26, y), new Vector2(x + 22, y + 14), new Vector2(x, y + 14) }, Color = c,
    };

    /// Build at `pos` (top-left) in `host`; pips fade in one by one after `delay`.
    public static Node2D Build(Node host, Vector2 pos, OpponentPick pick, Node owner, float delay)
    {
        font ??= Fonts.Get(new[] { "Bahnschrift", "Segoe UI" }, 700, stretch: 87);
        var col = Session.TierColor(pick.Level);
        var root = new Node2D { Position = pos };
        for (int i = 0; i < 7; i++)
        {
            var pip = Pip(i * 27, 0, i < pick.Level ? col : new Color(1, 1, 1, 0.14f));
            pip.Modulate = new Color(1, 1, 1, 0);
            root.AddChild(pip);
            owner.CreateTween().TweenProperty(pip, "modulate:a", 1f, 0.2).SetDelay(delay + i * 0.05);
        }
        var l = new Label { Text = pick.Tier, Position = new Vector2(7 * 27 + 12, -9) };
        l.AddThemeFontOverride("font", font);
        l.AddThemeFontSizeOverride("font_size", 24);
        l.AddThemeColorOverride("font_color", col);
        root.AddChild(l);
        host.AddChild(root);
        return root;
    }
}
