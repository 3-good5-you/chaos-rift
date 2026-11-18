using Godot;

namespace PoC.Game.UI;

public abstract partial class RiftMenu
{
    /// The Deck Builder's page arrow, a SHARD (user pick 2026-11-17, over a torn chevron and a rift tear): a small dark
    /// splinter of the dark side, gold rim, a white rift crack along it. Its tips are clipped so it reads soft, not
    /// knife-sharp. w x h, pointing right (dir 1) or left (dir -1); `hov` brightens it.
    public static void DrawEdgeArrow(Control c, int dir, float w, float h, bool hov)
    {
        Vector2 P(float x, float y) => new(dir > 0 ? x : w - x, y);
        var shard = new[]
        {
            P(w * 0.08f, h * 0.02f), P(w * 0.2f, h * 0.02f), P(w * 0.58f, h * 0.28f), P(w * 0.96f, h * 0.46f), P(w * 0.96f, h * 0.54f),
            P(w * 0.56f, h * 0.74f), P(w * 0.18f, h * 0.98f), P(w * 0.06f, h * 0.98f), P(w * 0.38f, h * 0.6f), P(w * 0.32f, h * 0.5f), P(w * 0.4f, h * 0.38f),
        };
        c.DrawColoredPolygon(shard.Select(p => p + new Vector2(0, 3)).ToArray(), Ink with { A = 0.14f });
        c.DrawColoredPolygon(shard, Ink);
        c.DrawPolyline(shard.Append(shard[0]).ToArray(), hov ? GoldLight : GoldC, 1.5f, true);
        // the crack: a short zigzag from the back to the tip
        var crack = new[] { P(w * 0.2f, h * 0.1f), P(w * 0.38f, h * 0.24f), P(w * 0.34f, h * 0.3f), P(w * 0.6f, h * 0.4f), P(w * 0.56f, h * 0.45f), P(w * 0.88f, h * 0.5f) };
        c.DrawPolyline(crack, Colors.White with { A = hov ? 0.4f : 0.2f }, 4, true);
        c.DrawPolyline(crack, Colors.White with { A = hov ? 1f : 0.85f }, 1.2f, true);
    }
}
