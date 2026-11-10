using Godot;
using PoC.Engine;

namespace PoC.Game.UI;

/// DUEL tab (2026-11-08): the duel modes as a list in the light; the focused one's rule and reward in the dark.
/// REGULAR opens rival select; PACK DUEL and LIMITED DUEL open a deck builder with the mode's rule applied (that deck
/// is for this duel only: not saved, not exported); GAUNTLET goes to setup with the four rivals in order; LEGEND opens
/// rival select for a full-strength rival. Modes not built yet show "COMING NEXT".
public partial class DuelModes : RiftMenu
{
    readonly List<Label> names = new();
    Label title = null!, line = null!, reward = null!, extra = null!;
    ColorRect? pip;

    protected override string? MusicPath => Md("solo_gate");
    protected override int ItemCount => Modes.All.Length;
    protected override void OnBack() => Transition.Go(this, "res://ui/MainMenu.tscn");

    protected override void Build()
    {
        Demo.Ensure();
        Session.Tour = null;
        Session.ClearMode();
        DeckBuilder.Temp = null;
        if (Session.Picks.Length != RivalPanels.All.Length) Session.RollPicks();
        Rift(0.5f, 0.44f);
        Back();
        Put(this, "DUEL", Title, 40, Ink, new Vector2(80, 58));
        Hair(this, new Vector2(82, 108), 100, GoldC);
        float y = H * 0.2f;
        for (int i = 0; i < Modes.All.Length; i++)
        {
            var m = Modes.All[i];
            var l = Put(this, m.Name, Title, 40, Ink * new Color(1, 1, 1, m.Ready ? 0.6f : 0.25f), new Vector2(130, y), new Vector2(620, 60));
            l.PivotOffset = new Vector2(0, 30);
            Hook(l, i);
            l.MouseDefaultCursorShape = m.Ready ? CursorShape.PointingHand : CursorShape.Arrow;
            names.Add(l);
            y += 92;
        }
        float x = W * 0.58f, w = W - x - 80;
        title = Put(this, "", Title, 60, Silver, new Vector2(x, H * 0.28f), new Vector2(w, 80));
        line = Put(this, "", Body, 26, Mist, new Vector2(x, H * 0.28f + 100), new Vector2(w, 80));
        line.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        extra = Put(this, "", Title, 20, Silver, new Vector2(x, H * 0.28f + 200), new Vector2(w, 60));
        extra.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        reward = Put(this, "", Title, 20, GoldLight, new Vector2(x, H * 0.28f + 290), new Vector2(w, 30));
        SetFocus(0);
    }

    protected override void OnFocus(int old, int now)
    {
        if (now < 0) return;
        for (int i = 0; i < names.Count; i++)
        {
            bool on = i == now, ready = Modes.All[i].Ready;
            names[i].AddThemeColorOverride("font_color", on ? (ready ? GoldDeep : Graphite) : Ink * new Color(1, 1, 1, ready ? 0.6f : 0.25f));
            Ink_.Only(this, names[i]).TweenProperty(names[i], "scale", on ? new Vector2(1.18f, 1.18f) : Vector2.One, 0.18);
        }
        pip ??= Pip(this, Vector2.Zero, 12);
        pip.Position = names[now].Position + new Vector2(-34, 24);
        var m = Modes.All[now];
        title.Text = m.Name;
        line.Text = m.Line;
        extra.Text = m.Mode switch
        {
            GameMode.Limited => "TODAY:  " + Modes.Today(DateTime.Today).Name + "\n" + Modes.Today(DateTime.Today).Text,
            GameMode.Gauntlet => string.Join("   ›   ", new Gauntlet(Session.Picks).Order.Select(p => RivalPanels.All.First(r => r.File == p.Rival).Name)),
            _ => "",
        };
        extra.AddThemeFontOverride("font", m.Mode == GameMode.Limited ? Body : Title);
        extra.AddThemeFontSizeOverride("font_size", m.Mode == GameMode.Limited ? 22 : 20);
        reward.Text = m.Ready ? m.Reward.ToUpperInvariant() : "COMING NEXT";
        reward.AddThemeColorOverride("font_color", m.Ready ? GoldLight : Mist);
        reward.Position = new Vector2(reward.Position.X, H * 0.28f + (extra.Text.Length > 0 ? 300 : 200));
    }

    protected override void OnChoose(int i)
    {
        var m = Modes.All[i];
        if (!m.Ready) return;
        Session.Mode = m.Mode;
        switch (m.Mode)
        {
            case GameMode.Challenges:
                Transition.Go(this, "res://ui/ChallengeSelect.tscn"); break;
            case GameMode.Regular:
            case GameMode.Legend:
            case GameMode.Chaos:
                Transition.Go(this, "res://ui/RivalSelect.tscn"); break;
            case GameMode.Gauntlet:
                Session.Run = new Gauntlet(Session.Picks);
                Session.Match = false;
                Transition.Go(this, "res://ui/DuelSetup.tscn"); break;
            case GameMode.Limited:
            {
                var rule = Session.Rule = Modes.Today(DateTime.Today);
                // your own cards that the rule allows, at most as many copies as the rule allows
                var pool = new Profile();
                foreach (var (idx, n) in Session.Profile.Owned)
                    if (n > 0 && rule.Allowed(Db.Get(idx))) pool.Owned[idx] = Math.Min(n, Modes.MaxCopies(rule));
                DeckBuilder.Temp = new DeckBuilder.TempBuild("LIMITED DECK", rule.Name, pool, "res://ui/RivalSelect.tscn");
                Transition.Go(this, "res://ui/DeckBuilder.tscn"); break;
            }
            case GameMode.Pack:
            {
                var packs = Modes.OpenPacks(Session.Rng);
                DeckBuilder.Temp = new DeckBuilder.TempBuild("PACK DECK", "BUILT FROM YOUR SIX PACKS ONLY", Modes.PoolProfile(packs.SelectMany(p => p)), "res://ui/RivalSelect.tscn", packs);
                Transition.Go(this, "res://ui/PackOpen.tscn"); break;
            }
        }
    }
}
