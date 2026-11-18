using Godot;
using PoC.Engine;

namespace PoC.Game.UI;

/// Duel, step 1 (Menu Lab pick 2026-11-16): every rival at once as head-and-shoulders cards, grouped by era (DUEL
/// MONSTERS, GX), each with its name and difficulty; pointing lifts a card with a gold ring, clicking goes to step 2.
/// All 21 tournament duelists are here (Session.Roster); LEGEND lists everyone with a level-7 deck (Modes.HasLegend).
public partial class RivalSelect : RiftMenu
{
    static readonly string[] DmEra = { "yugi", "kaiba", "joey", "mai", "pegasus", "marik", "bakura", "ishizu", "odion", "weevil", "rex", "mako", "keith" };
    bool leaving;

    protected override string? MusicPath => Md("solo_gate");
    protected override int ItemCount => Session.Roster.Length;

    protected override void Build()
    {
        Demo.Ensure();
        if (Session.Picks.Length != Session.Roster.Length) Session.RollPicks();
        Rift(0.9f, 0.86f);
        Back();
        string head = Session.Mode switch { GameMode.Legend => "LEGEND  ·  CHOOSE YOUR RIVAL", GameMode.Pack => "PACK DUEL  ·  CHOOSE YOUR RIVAL", GameMode.Limited => "LIMITED DUEL  ·  CHOOSE YOUR RIVAL", GameMode.Chaos => "CHAOS DUEL  ·  CHOOSE YOUR RIVAL", _ => "CHOOSE YOUR RIVAL" };
        Put(this, head, Title, 34, Ink, new Vector2(80, 58));
        Hair(this, new Vector2(82, 102), 140, GoldC);
        var all = Enumerable.Range(0, Session.Roster.Length).Where(i => Session.Mode != GameMode.Legend || Modes.HasLegend(Session.Roster[i].File)).ToList();
        const int cols = 8;
        float gx = 20, cw = (CrackX(H) - 84 - 60 - (cols - 1) * gx) / cols, ch = cw * 1.35f, gy = 20, y = 140;
        foreach (var (era, idx) in new[] { ("DUEL MONSTERS", all.Where(i => DmEra.Contains(Session.Roster[i].File)).ToList()), ("GX", all.Where(i => !DmEra.Contains(Session.Roster[i].File)).ToList()) })
        {
            if (idx.Count == 0) continue;
            Put(this, era, Thin, 14, Graphite, new Vector2(84, y));
            y += 28;
            for (int k = 0; k < idx.Count; k++)
            {
                int i = idx[k];
                var r = Session.Roster[i];
                var pick = Session.Mode == GameMode.Legend ? Modes.Legend(r.File) : Session.Picks[i];
                var at = new Vector2(84 + k % cols * (cw + gx), y + k / cols * (ch + gy));
                var root = new Control { Position = at, Size = new Vector2(cw, ch), PivotOffset = new Vector2(cw, ch) / 2 };
                AddChild(root);
                var pic = Pic(root, RivalPanels.Bust(r), Vector2.Zero, root.Size);
                pic.Material = Kit.RoundedMat(cw / ch, 0.05f);
                Plate(root, new Rect2(0, ch - 52, cw, 52), new StyleBoxFlat { BgColor = new Color(0.04f, 0.04f, 0.05f, 0.82f), CornerRadiusBottomLeft = (int)(cw * 0.05f), CornerRadiusBottomRight = (int)(cw * 0.05f), AntiAliasing = true });
                Fit(root, r.Name.ToUpperInvariant(), Title, 16, 11, Silver, new Rect2(6, ch - 50, cw - 12, 28));
                Mid(root, pick.Tier, Title, 10, Session.TierColor(pick.Level), new Rect2(0, ch - 26, cw, 18));
                var ring = Plate(root, new Rect2(-3, -3, cw + 6, ch + 6), Box(new Color(0, 0, 0, 0), GoldC, 3, (int)(cw * 0.06f)));
                ring.Visible = i == Session.Selected;
                Click(root, () => OnChoose(i), h =>
                {
                    if (h) UiSound(false);
                    ring.Visible = h || i == Session.Selected;
                    Ink_.Only(this, root).TweenProperty(root, "scale", h ? new Vector2(1.06f, 1.06f) : Vector2.One, 0.14).SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
                    root.ZIndex = h ? 2 : 0;
                });
            }
            y += (idx.Count + cols - 1) / cols * (ch + gy) + 14;
        }
    }

    protected override void OnFocus(int old, int now) { }

    protected override void OnChoose(int i)
    {
        if (leaving || i < 0 || i >= Session.Roster.Length) return;
        if (Session.Mode == GameMode.Legend && !Modes.HasLegend(Session.Roster[i].File)) return;
        leaving = true;
        UiSound(true);
        Session.Selected = i;
        Session.Tour = null;
        Transition.Go(this, "res://ui/DuelSetup.tscn");
    }

    protected override void OnBack() => Transition.Go(this, Session.ModeDeck != null ? "res://ui/DeckBuilder.tscn" : "res://ui/DuelModes.tscn");
}
