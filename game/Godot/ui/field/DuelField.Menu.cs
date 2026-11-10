using Godot;
using PoC.Engine;

namespace PoC.Game.UI;

/// The in-duel menu (Esc or the MENU chip): resume, duel speed (applies at once), surrender.
/// The duel keeps running behind it on the rival's turn; on yours nothing happens until you act anyway.
public partial class DuelField
{
    Control? duelMenu;
    bool surrendered, audioChanged;
    int menuAudio = 1;   // MUSIC / SOUND rows: 1 = sliders (picked); the UI Lab also shows 0 = none, 2 = ten-step bars

    void BuildMenuButton() => hud.AddChild(Chips.Make("MENU", new Vector2(W - 196, 196), new Vector2(150, 46), ToggleDuelMenu, 20));

    void ToggleDuelMenu()
    {
        if (duelMenu != null) { CloseDuelMenu(); return; }
        if (surrendered) return;
        Sfx("SE_POPUP");
        duelMenu = new Control { Size = new Vector2(W, H), MouseFilter = MouseFilterEnum.Stop, ZIndex = 50 };
        var dim = new ColorRect { Size = duelMenu.Size, Color = new Color(0, 0, 0, 0.6f), MouseFilter = MouseFilterEnum.Stop };
        dim.GuiInput += e => { if (e is InputEventMouseButton { Pressed: true }) CloseDuelMenu(); };
        duelMenu.AddChild(dim);
        float w = 460, h = menuAudio > 0 ? 476 : 380;
        var panel = Box(duelMenu, new Rect2(W / 2 - w / 2, H / 2 - h / 2, w, h), Glass(RivalPanels.Gold * new Color(1, 1, 1, 0.8f), 0.25f));
        panel.MouseFilter = MouseFilterEnum.Stop;
        Put(panel, "DUEL MENU", Impact, 44, RivalPanels.Gold, new Vector2(0, 22), new Vector2(w, 56), HorizontalAlignment.Center, 8);
        float y = 106;
        Panel Chip(string text, System.Action act) { var c = Chips.Make(text, new Vector2(50, y), new Vector2(w - 100, 58), act, 24); panel.AddChild(c); y += 76; return c; }
        Chip("RESUME", CloseDuelMenu);
        Panel? speed = null;
        if (Watch == null) speed = Chip(SpeedText(), () =>
        {
            Settings.Speed = (Settings.Speed + 1) % Settings.Speeds.Length;
            pace = Settings.Pace;
            Godot.Engine.TimeScale = 1 / pace;
            Session.Save();
            speed!.GetNode<Label>("Text").Text = SpeedText();
        });
        if (menuAudio > 0)
        {
            Level(panel, ref y, w, "MUSIC", Settings.Music, v => Settings.Music = v);
            Level(panel, ref y, w, "SOUND", Settings.Sound, v => { Settings.Sound = v; UiSound("cursor"); });
            y += 8;
        }
        bool sure = false;
        Panel? quit = null;
        quit = Chip(Watch != null ? "LEAVE REPLAY" : Lan.On ? "LEAVE DUEL" : "SURRENDER", () =>
        {
            if (Watch != null) { LeaveReplay(); return; }
            if (!sure) { sure = true; quit!.GetNode<Label>("Text").Text = "CLICK AGAIN TO CONFIRM"; return; }
            Surrender();
        });
        hud.AddChild(duelMenu);
        duelMenu.Modulate = new Color(1, 1, 1, 0);
        CreateTween().TweenProperty(duelMenu, "modulate:a", 1f, 0.15);
    }

    /// A volume row: drag or click the slider (menuAudio 1) or click one of ten steps (2); the left step also mutes.
    void Level(Control panel, ref float y, float w, string name, float v, Action<float> set)
    {
        var row = new Control { Position = new Vector2(50, y), Size = new Vector2(w - 100, 48), MouseFilter = MouseFilterEnum.Stop };
        panel.AddChild(row);
        y += 62;
        Put(row, name, Cond, 22, new Color(1, 1, 1, 0.8f), new Vector2(0, 10), new Vector2(100, 28));
        var pct = Put(row, "", Cond, 20, RivalPanels.Gold, new Vector2(row.Size.X - 56, 11), new Vector2(56, 28), HorizontalAlignment.Right);
        float x0 = 100, tw = row.Size.X - 100 - 70;
        System.Action<float> show;
        if (menuAudio == 1)
        {
            row.AddChild(new ColorRect { Position = new Vector2(x0, 21), Size = new Vector2(tw, 6), Color = new Color(1, 1, 1, 0.15f), MouseFilter = MouseFilterEnum.Ignore });
            var fill = new ColorRect { Position = new Vector2(x0, 21), Size = new Vector2(0, 6), Color = RivalPanels.Gold, MouseFilter = MouseFilterEnum.Ignore };
            var knob = new Panel { Size = new Vector2(24, 24), Position = new Vector2(x0 - 12, 12), MouseFilter = MouseFilterEnum.Ignore };
            knob.AddThemeStyleboxOverride("panel", Pill(new Color(1f, 0.93f, 0.7f), RivalPanels.Gold, 12, 0, new Color(RivalPanels.Gold, 0.5f), 8, 2));
            row.AddChild(fill); row.AddChild(knob);
            show = k => { fill.Size = new Vector2(tw * k, 6); knob.Position = new Vector2(x0 + tw * k - 12, 12); };
        }
        else
        {
            var steps = new List<Panel>();
            float sw = tw / 10;
            for (int i = 0; i < 10; i++)
            {
                var s = new Panel { Position = new Vector2(x0 + i * sw, 12), Size = new Vector2(sw - 5, 24), MouseFilter = MouseFilterEnum.Ignore };
                row.AddChild(s); steps.Add(s);
            }
            show = k =>
            {
                for (int i = 0; i < 10; i++) steps[i].AddThemeStyleboxOverride("panel", Pill(i < Mathf.RoundToInt(k * 10) ? RivalPanels.Gold : new Color(1, 1, 1, 0.08f), new Color(RivalPanels.Gold, 0.4f), 5));
            };
        }
        void Apply(float k)
        {
            k = Mathf.Clamp(k, 0, 1);
            if (menuAudio == 2) k = Mathf.Ceil(k * 10) / 10;
            show(k);
            pct.Text = k <= 0.001f ? "OFF" : $"{Mathf.RoundToInt(k * 100)}%";
            if (Mathf.IsEqualApprox(k, v)) return;
            v = k; set(k); Settings.Apply(false); audioChanged = true;
        }
        float At(float x) => menuAudio == 2 && x < x0 + tw / 10 && v <= 0.1f && v > 0 ? 0 : (x - x0) / tw;   // the first step again: off
        row.GuiInput += e =>
        {
            if (e is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left } b) Apply(At(b.Position.X));
            else if (menuAudio == 1 && e is InputEventMouseMotion m && (m.ButtonMask & MouseButtonMask.Left) != 0) Apply(At(m.Position.X));
        };
        show(v);
        pct.Text = v <= 0.001f ? "OFF" : $"{Mathf.RoundToInt(v * 100)}%";
    }

    static string SpeedText() => $"DUEL SPEED: {Settings.Speeds[Settings.Speed].Name}";

    void CloseDuelMenu()
    {
        duelMenu?.QueueFree();
        duelMenu = null;
        if (audioChanged) { audioChanged = false; Session.Save(); }
    }

    /// Give up the duel: the rival wins it (a LAN opponent is told you left). Ends whatever the duel loop was waiting on.
    void Surrender()
    {
        if (surrendered) return;
        surrendered = true;
        KeepReplay(1, "Surrender");   // before a LAN hang-up below forgets who the opponent was
        (surrenderLan, surrenderThem) = (Lan.On, ThemName);   // hanging up below clears both
        CloseDuelMenu();
        answer?.TrySetCanceled();   // the duel loop also checks `surrendered` between steps and after the rival's move
        // Waiting on a LAN opponent's move: tell them and hang up, which ends that wait.
        if (Lan.On && remoteWait) { Lan.Link?.Send(new LanLink.Msg { T = "bye" }); Lan.Close(); }
    }

    bool surrenderLan;
    string surrenderThem = "";

    async Task EndBySurrender()
    {
        bool lan = surrenderLan;
        if (lan)
        {
            lanOver = true;
            if (Lan.Link is { Closed: false } link) link.Send(new LanLink.Msg { T = "bye" });
            Lan.Close();
            Lan.LastWinner = 1;
        }
        await FieldShatters(0);   // giving up: your monsters break, as an LP loss would
        Sfx("SE_DUEL_LOSE");
        await EndTitle("DEFEAT", Bad, false, $"SURRENDER  ·  {surrenderThem.ToUpperInvariant()} WINS");
        await Wait(1.5);
        if (backToLab) { GetTree().ReloadCurrentScene(); return; }
        if (lan) { Transition.Go(this, "res://ui/LanLobby.tscn"); return; }
        Shop.Observe(Session.Profile, D);
        bool more = Session.FinishDuel(1, "Surrender");
        if (more) Session.RivalSides(D);
        Transition.Go(this, more ? "res://ui/SideDeck.tscn" : "res://ui/Results.tscn");
    }
}
