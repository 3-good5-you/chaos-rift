using System.Net;
using System.Net.Sockets;
using Godot;
using PoC.Engine;

namespace PoC.Game.UI;

/// LAN duel lobby (Chaos Rift look, 2026-11-09): you in the light (picture, name, deck cases), the network in the
/// dark: the duelists heard on it (click a name to join, or type an address), or, once connected, your opponent.
/// Both pick a deck and READY; the host deals the seed and the coin, then the coin toss.
/// Dev args: `--lan=host`, `--lan=join:<address>`, `--lanready` (ready as soon as connected).
public partial class LanLobby : RiftMenu
{
    enum Mode { Find, Hosting, Joining, Vs }
    Mode mode = Mode.Find;
    CancellationTokenSource? cts;
    readonly Dictionary<string, (string Name, double Seen)> heard = new();
    string? drawn;
    double clock;
    bool ready, theyReady, starting, autoReady;
    List<Deck> playable = new();

    Control right = null!, decks = null!, blips = null!;
    Control? go;
    Label note = null!, status = null!, back = null!, youReady = null!;
    LineEdit address = null!;
    float Rx => W * 0.8f;   // the middle of the dark side

    protected override string? MusicPath => Md("solo_gate");
    protected override int ItemCount => 1;

    protected override void Build()
    {
        Demo.Ensure();
        Rift(0.6f, 0.54f);
        back = T(this, "‹  MENU", Title, 18, new Vector2(62, 30), default, HorizontalAlignment.Left, 0.7f);
        Click(back, OnBack, h => back.Mat().P("lit", h ? 1f : 0f));
        Heading("LAN DUEL", new Vector2(80, 60), 46);

        // You, in the light.
        var me = Cosmetics.Framed(Avatar, P.Frame, 96);
        me.Position = new Vector2(110, 190);
        AddChild(me);
        Put(this, Lan.Name, Title, 36, Ink, new Vector2(232, 204));
        youReady = Put(this, "READY", Title, 18, GoldDeep, new Vector2(234, 254));
        youReady.Visible = false;
        Put(this, "YOUR DECK", Title, 20, Graphite, new Vector2(110, 340));
        decks = new Control { Size = new Vector2(W, H), MouseFilter = MouseFilterEnum.Ignore };
        right = new Control { Size = new Vector2(W, H), MouseFilter = MouseFilterEnum.Ignore };
        AddChild(decks); AddChild(right);
        note = Put(this, "", Body, 20, Graphite, new Vector2(112, H - 210), new Vector2(CrackX(H - 210) - 240, 56));
        note.AutowrapMode = TextServer.AutowrapMode.WordSmart;

        // SAVE REPLAY of the LAN duel that just ended (2026-11-09).
        if (Session.LastReplay is { } rep)
        {
            Control save = null!;
            save = Button(this, "SAVE REPLAY", new Vector2(190, H - 50), () =>
            {
                try { rep.Save(Session.ReplayDir); Session.LastReplay = null; save.Visible = false; Note("Replay saved. Watch it from REPLAYS on the main menu."); }
                catch (Exception e) when (e is IOException or UnauthorizedAccessException) { Note("Couldn't save the replay."); GD.PrintErr(e.Message); }
            }, 16);
        }

        foreach (var a in OS.GetCmdlineUserArgs())
        {
            if (a == "--lanready") autoReady = true;
            else if (a == "--lan=host") CallDeferred(nameof(Host));
            else if (a.StartsWith("--lan=join:")) { var to = a[11..]; Callable.From(() => Join(to)).CallDeferred(); }
        }

        if (Lan.On) Connected(fresh: false);
        else ShowFind();
    }

    // ---------------------------------------------------------------- pieces

    /// A line centred on the dark side.
    Label Dark(string s, Font f, int size, Color c, float y) => Put(right, s, f, size, c, new Vector2(Rx - 340, y), new Vector2(680, size * 1.5f), HorizontalAlignment.Center);

    void ClearRight()
    {
        foreach (var c in right.GetChildren()) c.QueueFree();
        blips = new Control { MouseFilter = MouseFilterEnum.Ignore };
    }

    // ---------------------------------------------------------------- find / host / join

    void ShowFind()
    {
        mode = Mode.Find;
        ready = theyReady = false;
        ClearRight();
        Dark("DUELISTS ON YOUR NETWORK", Thin, 18, Mist, 150);
        status = Dark("", Title, 26, Mist * new Color(1, 1, 1, 0.7f), 250);
        right.AddChild(blips);
        Dark("OR TYPE AN ADDRESS", Thin, 15, Mist, H - 330);
        address = new LineEdit { PlaceholderText = "ADDRESS", Position = new Vector2(Rx - 220, H - 296), Size = new Vector2(440, 54), Flat = true, Alignment = HorizontalAlignment.Center };
        address.AddThemeFontOverride("font", Title); address.AddThemeFontSizeOverride("font_size", 26);
        address.AddThemeColorOverride("font_color", Silver); address.AddThemeColorOverride("caret_color", Silver);
        address.AddThemeColorOverride("font_placeholder_color", Mist * new Color(1, 1, 1, 0.45f));
        address.TextSubmitted += t => Join(t);
        right.AddChild(address);
        Hair(right, new Vector2(Rx - 220, H - 240), 440, Silver * new Color(1, 1, 1, 0.3f));
        Button(right, "JOIN", new Vector2(Rx, H - 180), () => Join(address.Text), 24);
        SetGo("HOST DUEL");
        Listen();
        Decks();
        drawn = null;
    }

    void Listen()
    {
        cts?.Cancel();
        cts = new CancellationTokenSource();
        _ = Beacon.Listen((name, addr) => heard[addr] = (name, clock), cts.Token);
    }

    /// The duelists heard on the network: a name to click, the address under it.
    void DrawBlips()
    {
        foreach (var c in blips.GetChildren()) c.QueueFree();
        int i = 0;
        foreach (var (addr, (name, _)) in heard.OrderBy(h => h.Key).Take(5))
        {
            float y = 230 + i * 84;
            var l = Put(blips, name.ToUpperInvariant(), Title, 34, Silver, new Vector2(Rx - 340, y), new Vector2(680, 48), HorizontalAlignment.Center);
            Put(blips, addr, Thin, 14, Mist, new Vector2(Rx - 340, y + 48), new Vector2(680, 20), HorizontalAlignment.Center);
            var target = addr;
            Click(l, () => { UiSound(true); Join(target); }, h => { if (h) UiSound(false); l.AddThemeColorOverride("font_color", h ? GoldLight : Silver); });
            i++;
        }
        if (mode == Mode.Find) status.Text = i == 0 ? "SEARCHING…" : "";
    }

    void Go()
    {
        switch (mode)
        {
            case Mode.Find: Host(); break;
            case Mode.Hosting or Mode.Joining: Cancel(); break;
            case Mode.Vs: SetReady(!ready); break;
        }
    }

    async void Host()
    {
        if (mode != Mode.Find || !Decks()) return;
        mode = Mode.Hosting;
        cts?.Cancel();
        cts = new CancellationTokenSource();
        ClearRight();
        Dark("WAITING FOR A DUELIST", Title, 36, Silver, H * 0.34f);
        Dark("They will see your name in their list, or can type", Body, 20, Mist, H * 0.34f + 76);
        Dark(string.Join("   ·   ", LocalAddresses()), Title, 26, GoldLight, H * 0.34f + 116);
        SetGo("CANCEL");
        var token = cts.Token;
        _ = Beacon.Announce(Settings.PlayerName, token);
        try { Lan.Link = await LanLink.Host(token); }
        catch (OperationCanceledException) { return; }
        catch (SocketException e) { Note($"Can't host: {e.Message}"); ShowFind(); return; }
        cts.Cancel();   // stop announcing
        Connected(fresh: true);
    }

    async void Join(string to)
    {
        to = to.Trim();
        if (mode != Mode.Find || to.Length == 0 || !Decks()) return;
        int port = LanLink.Port;
        int colon = to.LastIndexOf(':');
        if (colon > 0 && int.TryParse(to[(colon + 1)..], out var p)) { port = p; to = to[..colon]; }
        mode = Mode.Joining;
        cts?.Cancel();
        cts = new CancellationTokenSource(TimeSpan.FromSeconds(8));
        status.Text = $"CONNECTING TO {to}…";
        SetGo("CANCEL");
        try { Lan.Link = await LanLink.Join(to, cts.Token, port); }
        catch (Exception e) when (e is OperationCanceledException or SocketException)
        {
            if (mode != Mode.Joining) return;   // cancelled by hand
            ShowFind();
            Note($"No duel found at {to}.");
            return;
        }
        Connected(fresh: true);
    }

    void Cancel()
    {
        cts?.Cancel();
        ShowFind();
    }

    static IEnumerable<string> LocalAddresses()
    {
        try
        {
            var l = Dns.GetHostAddresses(Dns.GetHostName()).Where(a => a.AddressFamily == AddressFamily.InterNetwork && !IPAddress.IsLoopback(a)).Select(a => a.ToString()).ToList();
            return l.Count > 0 ? l : new[] { "127.0.0.1" };
        }
        catch (SocketException) { return new[] { "127.0.0.1" }; }
    }

    // ---------------------------------------------------------------- connected

    void Connected(bool fresh)
    {
        mode = Mode.Vs;
        cts?.Cancel();
        heard.Clear();
        ready = theyReady = starting = false;
        if (fresh) { Lan.Them = null; Lan.Link!.Send(Lan.Hello()); }
        ShowThem();
        SetGo("READY");
        if (!fresh && autoReady) SetReady(true);
        Note(Lan.LastWinner switch { 0 => "You won the last duel.", 1 => "You lost the last duel.", -1 => "The last duel was a draw.", _ => "" });
    }

    /// Your opponent, in the dark (once their hello arrived).
    void ShowThem()
    {
        ClearRight();
        Decks();
        if (Lan.Them == null) { status = Dark("CONNECTING…", Title, 36, Mist, H * 0.4f); return; }
        Dark(Lan.TheirName, Title, 54, Silver, 70);
        status = Dark("", Title, 20, Mist, 146);
        float size = Math.Min(420, H * 0.4f);
        var them = Cosmetics.Framed(Lan.TheirAvatar, Lan.TheirFrame, size);
        them.Position = new Vector2(Rx - size / 2, 230);
        right.AddChild(them);
        var list = DeckRules.Formats.FirstOrDefault(f => f.Key == Lan.List).Name ?? "?";
        Dark($"{Lan.Link!.Peer}   ·   BAN LIST {list}{(Lan.Link.IsHost ? "" : " (HOST'S)")}", Thin, 15, Mist, 230 + size + 40);
        float vy = H * 0.42f;
        T(right, "VS", Title, 64, new Vector2(CrackX(vy) - 100, vy - 44), new Vector2(200, 88), HorizontalAlignment.Center);
        TheirStatus();
    }

    void TheirStatus() { if (status != null && Lan.Them != null) { status.Text = theyReady ? "READY" : "PICKING A DECK…"; status.AddThemeColorOverride("font_color", theyReady ? GoldLight : Mist); } }

    /// Decks legal under the host's ban list; false (with a note) when there are none.
    bool Decks()
    {
        var list = Lan.On ? Lan.List : DeckRules.LimitGame;
        playable = Session.Decks.Where(d => DeckRules.IsLegal(d, Session.Profile, list)).ToList();
        if (Session.Playing == null || !playable.Contains(Session.Playing)) Session.Playing = playable.FirstOrDefault();
        ShowDeck();
        if (playable.Count == 0) Note("No deck is legal under this ban list. Build one in the Deck Builder.");
        return playable.Count > 0;
    }

    /// Your deck cases (as on the Duel screen); locked once you are READY.
    void ShowDeck()
    {
        foreach (var c in decks.GetChildren()) c.QueueFree();
        youReady.Visible = ready;
        if (playable.Count == 0) { Put(decks, "NO LEGAL DECK", Title, 26, Ink * new Color(1, 1, 1, 0.5f), new Vector2(110, 400)); return; }
        const float cw = 150, gx = 90; float ch = cw * 1.458f;
        int cols = (int)((CrackX(600) - 200) / (cw + gx));
        Paged(decks, new Rect2(130, 420, cols * (cw + gx), ch + 120), playable.Count, cols, 1, new Vector2(cw + gx, 0), (i, host, at) =>
        {
            var d = playable[i];
            bool on = d == Session.Playing;
            for (int k = 2; k >= 1; k--)
            {
                var b = Pic(host, Cosmetics.Back(P.Back, 256), at + new Vector2(k * 8, -k * 6), new Vector2(cw, ch));
                b.PivotOffset = new Vector2(cw, ch) / 2; b.Rotation = k * 0.04f;
            }
            if (d.CoverCard() >= 0) { var c = Card(host, Db.Get(d.CoverCard()), at + new Vector2(cw, ch) / 2, ch, -0.02f, on ? 1.3f : 0.6f); if (on) c.Scale = new Vector2(1.06f, 1.06f); }
            Put(host, d.Name.ToUpperInvariant(), Title, 15, on ? GoldDeep : Ink, at + new Vector2(-30, ch + 20), new Vector2(cw + 60, 22), HorizontalAlignment.Center);
            if (on) Pip(host, at + new Vector2(cw / 2, ch + 56), 8);
            if (ready) { host.Modulate = new Color(1, 1, 1, 0.6f); return; }
            var hit = new Control { Position = at - new Vector2(10, 10), Size = new Vector2(cw + 30, ch + 20) };
            host.AddChild(hit);
            Click(hit, () => { Session.Playing = d; UiSound(false); ShowDeck(); });
        }, new Vector2(130, 420 + ch + 90));
    }

    void SetReady(bool on)
    {
        if (on && (Lan.Them == null || Session.Playing == null)) return;
        ready = on;
        SetGo(on ? "NOT READY" : "READY");
        ShowDeck();
        Lan.Link!.Send(on ? new LanLink.Msg { T = "deck", Main = Session.Playing!.Main.ToArray(), Extra = Session.Playing.Extra.ToArray() } : new LanLink.Msg { T = "wait" });
        Deal();
    }

    /// Both ready: the host deals the seed and the coin; the guest waits for them.
    void Deal()
    {
        if (!ready || !theyReady || !Lan.Link!.IsHost || starting) return;
        Lan.Seed = Session.Rng.Next();
        Lan.First = Session.Rng.Int(2);
        Lan.Link.Send(new LanLink.Msg { T = "setup", Seed = Lan.Seed, First = Lan.First });
        Start();
    }

    void Start()
    {
        starting = true;   // from here the field reads the link
        // Transition ignores a new change while the curtain that brought us here is still running.
        GetTree().CreateTimer(Math.Max(0.05, 1.0 - clock)).Timeout += () => Transition.Go(this, "res://ui/TurnOrder.tscn");
    }

    void Pump()
    {
        var link = Lan.Link!;
        while (!starting && link.TryRead(out var m))
        {
            switch (m.T)
            {
                case "hello":
                    Lan.Greet(m);
                    if (!Lan.SameBuild) { Drop("Your opponent runs a different version of the game."); return; }
                    ShowThem();
                    if (autoReady) SetReady(true);
                    break;
                case "deck":
                    if (!Lan.Sane(m.Main ?? Array.Empty<int>(), m.Extra ?? Array.Empty<int>())) { Drop("Your opponent's deck could not be read."); return; }
                    (Lan.TheirMain, Lan.TheirExtra, theyReady) = (m.Main!, m.Extra!, true);
                    TheirStatus();
                    Deal();
                    break;
                case "wait": theyReady = false; TheirStatus(); break;
                case "setup":
                    if (link.IsHost) break;
                    (Lan.Seed, Lan.First) = (m.Seed, m.First);
                    Start();
                    return;
                case "bye": Drop("Your opponent left."); return;
            }
        }
        if (!starting && link.Closed) Drop("Your opponent left.");
    }

    void Drop(string why)
    {
        Lan.Close();
        ShowFind();
        Note(why);
    }

    void Note(string s) => note.Text = s;

    /// The one main button under your decks (HOST DUEL / CANCEL / READY / NOT READY) and the back link's word.
    void SetGo(string text)
    {
        back.Text = mode == Mode.Vs ? "‹  LEAVE" : "‹  MENU";
        go?.QueueFree();
        go = Button(this, text, new Vector2(W * 0.3f, H - 100), Go, 32);
    }

    protected override void OnBack()
    {
        if (mode == Mode.Vs) { Drop(""); return; }
        if (mode is Mode.Hosting or Mode.Joining) { Cancel(); return; }
        cts?.Cancel();
        Transition.Go(this, "res://ui/MainMenu.tscn");
    }

    public override void _Process(double delta)
    {
        base._Process(delta);
        clock += delta;
        if (Lan.On && mode == Mode.Vs) Pump();
        if (mode != Mode.Find) return;
        // Hosts that went quiet for 4 s leave the radar.
        foreach (var k in heard.Where(h => clock - h.Value.Seen > 4).Select(h => h.Key).ToList()) heard.Remove(k);
        var sig = string.Join(",", heard.Keys.OrderBy(k => k));
        if (sig != drawn) { drawn = sig; DrawBlips(); }
    }

    public override void _ExitTree() => cts?.Cancel();
}
