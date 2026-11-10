using Godot;
using PoC.Engine;

namespace PoC.Game.UI;

/// Phase control, Master Duel style: a round phase button at the right-middle showing the turn and phase,
/// plus a centre callout on each change. While you can act in Main/Battle the button pulses: left click goes
/// to the next phase, right click lists the phases you can go to now.
public partial class DuelField
{
    Label phaseTurn = null!, phaseName = null!;
    Panel phaseCircle = null!;
    Panel? phaseMenu;
    Tween? pulse;
    int lastTurn = -1;
    Prompt? phasePrompt;
    Color phaseColor = RivalPanels.Gold;

    static readonly Act[] PhaseActs = { Act.ToBattle, Act.ToMain2, Act.EndTurn };
    static string PhaseActName(Act a) => a switch { Act.ToBattle => "BATTLE PHASE", Act.ToMain2 => "MAIN PHASE 2", _ => "END TURN" };

    static StyleBoxFlat Pill(Color bg, Color border, int radius, float skew = 0, Color? glow = null, int glowSize = 0, int bw = 1) => new()
    {
        BgColor = bg, BorderColor = border,
        BorderWidthTop = bw, BorderWidthBottom = bw, BorderWidthLeft = bw, BorderWidthRight = bw,
        CornerRadiusTopLeft = radius, CornerRadiusTopRight = radius, CornerRadiusBottomLeft = radius, CornerRadiusBottomRight = radius,
        CornerDetail = 12, AntiAliasing = true, Skew = new Vector2(skew, 0),
        ShadowColor = glow ?? new Color(0, 0, 0, 0), ShadowSize = glowSize,
    };

    void BuildPhase()
    {
        const float D = 150;
        var c = new Vector2(W - 24 - D, H / 2 - D / 2);
        phaseCircle = Box(hud, new Rect2(c, new Vector2(D, D)), Pill(new Color(0.03f, 0.03f, 0.045f, 0.92f), RivalPanels.Gold, (int)(D / 2), 0, null, 0, 3));
        phaseCircle.PivotOffset = new Vector2(D / 2, D / 2);
        phaseCircle.MouseFilter = MouseFilterEnum.Stop;
        phaseCircle.GuiInput += PhaseInput;
        phaseTurn = Put(phaseCircle, "", Cond, 19, new Color(1, 1, 1, 0.6f), new Vector2(0, 38), new Vector2(D, 24), HorizontalAlignment.Center);
        phaseName = Put(phaseCircle, "", Impact, 32, Colors.White, new Vector2(0, 62), new Vector2(D, 44), HorizontalAlignment.Center, 6);
    }

    void ShowPhase(Phase p, int side, int turn, bool callout = true)
    {
        var c = side == 0 ? Pm.You : Pm.Them;
        phaseColor = c;
        int now = (int)p;
        bool newTurn = turn != lastTurn;
        lastTurn = turn;
        phaseTurn.Text = "TURN " + turn;
        phaseTurn.AddThemeColorOverride("font_color", c);
        phaseName.Text = PhaseNames[now];
        phaseCircle.AddThemeStyleboxOverride("panel", Pill(new Color(0.03f, 0.03f, 0.045f, 0.92f), c, 75, 0, new Color(c, 0.4f), 18, 3));
        phaseCircle.Scale = new Vector2(1.12f, 1.12f);
        CreateTween().TweenProperty(phaseCircle, "scale", Vector2.One, 0.3).SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
        // The phase's name lies on the turn player's half of the field (Battle in ember, as its zone wave).
        if (!newTurn && callout) FieldTitle(PhaseNames[now] + " PHASE", side, p == Phase.Battle ? FxEmber : side == 0 ? RivalPanels.Gold : Pm.Them, 110);
        for (int i = 0; i < 2; i++)
        {
            var pc = i == 0 ? Pm.You : Pm.Them;
            profile[i].AddThemeStyleboxOverride("panel", Glass(pc * new Color(1, 1, 1, i == side ? 0.95f : 0.3f), i == side ? 0.35f : 0));
        }
    }

    /// Master Duel style: the new phase's name flashes across the middle of the field.
    Label? callout;
    Tween? calloutTw;

    void Callout(string text, Color c)
    {
        // Phases can change faster than a callout fades: drop the old one (and its tween) first.
        calloutTw?.Kill();
        callout?.QueueFree();
        var l = callout = Put(hud, text, Impact, 82, c, new Vector2(0, H / 2 - 60), new Vector2(W, 110), HorizontalAlignment.Center, 14);
        l.PivotOffset = new Vector2(W / 2, 55);
        l.Scale = new Vector2(1.25f, 1.25f);
        l.Modulate = new Color(1, 1, 1, 0);
        var tw = calloutTw = CreateTween().SetParallel();
        tw.TweenProperty(l, "modulate:a", 1f, 0.12);
        tw.TweenProperty(l, "scale", Vector2.One, 0.3).SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
        tw.Chain().TweenInterval(0.35);
        tw.Chain().TweenProperty(l, "modulate:a", 0f, 0.3);
        tw.Parallel().TweenProperty(l, "scale", new Vector2(0.92f, 0.92f), 0.3);
        tw.Chain().TweenCallback(Callable.From(() => { if (callout == l) callout = null; l.QueueFree(); }));
    }

    // ---- your phase changes

    /// Next phase in order: Battle, then Main 2, then end of turn (whichever the engine offers first).
    static int NextPhase(Prompt p)
    {
        foreach (var a in PhaseActs)
        {
            int i = p.Options.FindIndex(o => o.Act == a);
            if (i >= 0) return i;
        }
        return -1;
    }

    void PhaseInput(InputEvent e)
    {
        if (e is not InputEventMouseButton { Pressed: true } mb || phasePrompt == null) return;
        if (mb.ButtonIndex == MouseButton.Left) PhaseLeft();
        else if (mb.ButtonIndex == MouseButton.Right) OpenMenu(phasePrompt);
    }

    void PhaseLeft()
    {
        if (phasePrompt == null) return;
        CancelAttack();
        int i = NextPhase(phasePrompt);
        if (i >= 0) Answer(i);
    }

    void OpenMenu(Prompt p)
    {
        CloseMenu();
        var opts = Enumerable.Range(0, p.Options.Count).Where(i => PhaseActs.Contains(p.Options[i].Act)).ToList();
        float h = 54 + opts.Count * 54;
        phaseMenu = Box(hud, new Rect2(phaseCircle.Position + new Vector2(-300, phaseCircle.Size.Y / 2 - h / 2), new Vector2(280, h)), Glass(phaseColor * new Color(1, 1, 1, 0.8f), 0.25f));
        Put(phaseMenu, "GO TO", Cond, 20, new Color(1, 1, 1, 0.6f), new Vector2(0, 12), new Vector2(280, 26), HorizontalAlignment.Center);
        for (int k = 0; k < opts.Count; k++)
        {
            int idx = opts[k];
            phaseMenu.AddChild(Chips.Make(PhaseActName(p.Options[idx].Act), new Vector2(20, 46 + k * 54), new Vector2(240, 44), () => Answer(idx), 20));
        }
        phaseMenu.Modulate = new Color(1, 1, 1, 0);
        CreateTween().TweenProperty(phaseMenu, "modulate:a", 1f, 0.15);
    }

    void CloseMenu()
    {
        phaseMenu?.QueueFree();
        phaseMenu = null;
    }
}
