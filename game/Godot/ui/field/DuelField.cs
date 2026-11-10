using Godot;
using PoC.Engine;

namespace PoC.Game.UI;

/// The duel field: a lit 3D table with the cards as 3D objects (Master Duel style). Card details sit on
/// the left, both duelists' profile, LP and the phase steps on the right (DuelField.Hud.cs).
/// Runs a real engine duel: every engine event is snapshotted and the snapshots replay as animations.
/// You play your side (DuelField.Input.cs); the original CPU AI (Engine/Ai.cs) plays the rival.
/// Args: `--mat=arena|rival|dm|redeyes|blueeyes`, `--seed=N`, `--turn=N` (fast-forward), `--speed=X`, `--autoplay` (bot plays your side), `--selftest`.
public partial class DuelField : MenuBase
{
    /// Where a card is, as the field draws it. Side 0 = you (near), 1 = rival (far).
    /// Ready (Battle Phase, turn player's monsters): 0 none, 1 can attack, 2 already attacked.
    /// Ctr: its counters ("spell=3"); Lv / LvWas: current and printed Level (DuelField.Marks.cs).
    public readonly record struct St(Loc Loc, int Index, int Count, bool Up, bool Def, int Side, int Atk, int Df, int Ready = 0, int Counter = 0, string Ctr = "", int Lv = 0, int LvWas = 0);

    sealed class Step
    {
        public Ev? Ev;
        public Phase Phase;
        public int Tp, Turn, Chain;
        public int[] Lp = new int[2];
        public St[] Cards = null!;
        public int Seq;   // Duel.Seq when taken (Duel.OnTriggered inserts before later steps)
        public int[] Blocked = new int[2];   // per screen side: Monster Zones that can't be used (bit = zone; Ground Collapse, Dimensionhole)
        public Ev? Cause;                // a Triggered step: the event its card answered
        public List<Card>? Group;        // the first of several triggers on one event: all of them, shown as one chain
        public bool InGroup;
        public Card? Thief;              // a card leaving the hand at random: the card whose effect pulls it (Thief's pull)
        public bool Together;            // a discard with the next one (Card Destruction): both hands burn at once
        public bool NoDraw, ToEnd;       // the Draw Phase without its draw; the turn skips to its End Phase
        public int Chaos = -1;           // Chaos Duel: the rule in force
    }

    sealed class View
    {
        public Card Card = null!;
        public Node3D Root = null!;
        public StandardMaterial3D Face = null!, BackMat = null!;
        public MeshInstance3D Aura = null!;
        public ShaderMaterial ShineMat = null!;
        public Node3D Stat = null!;               // ATK / DEF pair: the current position's number lit, the other muted
        public Label3D StatAtk = null!, StatDef = null!, Badge = null!, Count = null!;
        public Tween? Tw;
        public St S;
        public bool Front;
    }

    /// A playmat: frame colours per side and optional card art under each half (or one art for the whole mat).
    public sealed record Playmat(Color You, Color Them, Color Stone, string? ArtYou, string? ArtThem, bool Full, string? Tex = null, string? TexThem = null);

    // Table geometry (world units). Cards lie on the XZ plane, +Z toward you.
    const float CW = 0.6f, CH = 0.87f, PX = 0.9f, PZ = 1.0f, MatW = 7.3f, MatD = 4.9f;

    float W, H;
    Duel D = null!;
    RivalPanels.Rival Rival = null!;
    Playmat Pm = null!;
    Camera3D cam = null!;
    Node3D world = null!;
    Font Impact = null!, Cond = null!, Ui = null!;
    static readonly string[] PhaseNames = { "DRAW", "STANDBY", "MAIN 1", "BATTLE", "MAIN 2", "END" };
    static readonly string[] PhaseShort = { "DP", "SP", "M1", "BP", "M2", "EP" };

    readonly Dictionary<int, View> views = new();
    readonly List<Step> steps = new();
    readonly List<Step> carried = new();   // a batch's trailing moves to the Graveyard, held for the next batch (Loop)
    readonly HashSet<int> crackedEarly = new();   // battle-destroyed cards already shattering with their opponent (Play)
    (int A, int B) mutualPair = (-1, -1);             // the two monsters of an equal-ATK battle (Flair: DamageCalc)
    readonly Dictionary<int, (bool Battle, Card? By)> destroying = new();   // this batch's destroyed cards -> by battle, by what
    // This batch's tributes and fusion materials (they leave for a summon queued later in the batch) -> that summon and where it lands.
    readonly Dictionary<int, (SummonKind Kind, int Uid, St At)> offered = new();
    readonly HashSet<int> blockShown = new();   // attackers whose blocked attack already played its shield
    readonly int[] shownLp = new int[2];
    int shownTp = -1, shownTurn = -1, hovered = -1, shownChain;
    Phase shownPhase;
    double pace = Settings.Pace;
    Rng bot = null!;
    Ai cpu = null!;     // the CPU (Engine/Ai.cs) plays the rival; its brain follows the pick's difficulty level
    /// Your engine seat. The field always draws you near (view side 0): engine seats map to view sides by `Side`.
    /// 0 vs the CPU; a LAN guest is seat 1 (`--seat=1` tries it against the CPU).
    int Me;
    int Side(int seat) => seat < 0 ? seat : seat ^ Me;
    int actionsTurn = -1, actions;
    bool thinking;   // the CPU is reading D on a worker thread: the field asks D nothing that touches its caches
    Vector3 camHome;
    readonly List<MeshInstance3D> crystals = new();
    readonly List<Node3D>[] banishMarks = { new(), new() };
    readonly MeshInstance3D[,] blockMarks = new MeshInstance3D[2, 5];   // a closed Monster Zone (Step.Blocked)

    // Master Duel style: a Normal track, Keycard once an ace hits the field, Climax once someone is low on LP.
    // EXPERT picks play the Ex 02 set. Some duels instead play the rival's own PoC theme, start to end (never mixed).
    const float ClassicChance = 0.25f;
    static bool Expert => (Session.Tour != null || (Session.Selected >= 0 && Session.Selected < Session.Picks.Length)) && Session.Pick.Tier == "EXPERT";
    // One Master Duel set per duel: its normal, keycard and climax tracks belong together.
    readonly int trackSet = GD.RandRange(1, 13);
    string DuelTrack(string stage) => Expert ? Md($"duel_ex_02_{stage}") : $"res://assets/audio/md/duel_{stage}_{trackSet:00}.mp3";
    readonly bool classic = GD.Randf() < ClassicChance;
    int musicStage; // 0 normal, 1 keycard, 2 climax; only ever goes up
    protected override string? MusicPath => classic
        ? $"res://assets/audio/duel_{(Session.Rival.File is var f && f is "yugi" or "kaiba" or "joey" ? f : "joey")}.wav"   // the PoC themes; the others borrow Joey's
        : DuelTrack("normal");
    void Escalate(int stage)
    {
        // A classic duel keeps the rival's theme until the climax, which takes over there too.
        if ((classic && stage < 2) || stage <= musicStage) return;
        musicStage = stage;
        SetMusic(DuelTrack(stage == 1 ? "keycard" : "climax"));
    }
    protected override int ItemCount => 1;
    protected override void OnFocus(int old, int now) { }
    protected override void OnChoose(int i) { }

    /// What lies around a forged mat's table, far below it: 0 lava sea, 1 water, 2 clouds, 3 stone tiles, 4 circuitry, 5 cosmos.
    /// Deep = the depths, Glow = the mat's accent (same as matforge.py's corner colour).
    static readonly Dictionary<string, (int Kind, Color Deep, Color Glow)> Surrounds = new()
    {
        ["magma"] = (0, new Color("2a0a04"), new Color("ff6a14")), ["tide"] = (1, new Color("06243a"), new Color("7ad8ff")),
        ["storm"] = (2, new Color("1c0f30"), new Color("d070ff")), ["skyway"] = (2, new Color("2a4a48"), new Color("8affb0")),
        ["radiant"] = (3, new Color("3a2c14"), new Color("ffe08a")), ["ruins"] = (3, new Color("2a2016"), new Color("ffb050")),
        ["pharaoh"] = (3, new Color("3a2a10"), new Color("ffcf70")), ["datastream"] = (4, new Color("041a24"), new Color("3ae0ff")),
        ["cup_arena"] = (4, new Color("1a0a2a"), new Color("c070ff")), ["steel"] = (4, new Color("14181e"), new Color("e8f0ff")),
        ["twin_vortex"] = (5, new Color("2a1206"), new Color("ff9a30")), ["starlane"] = (5, new Color("0a1030"), new Color("9ab8ff")),
        ["art_dark_magician"] = (5, new Color("1a0a30"), new Color("c07aff")), ["art_blue_eyes"] = (5, new Color("061830"), new Color("8ad0ff")),
        ["art_red_eyes"] = (5, new Color("2a0606"), new Color("ff5a3a")),
        ["obsidian"] = (2, new Color("0a0908"), new Color("e8c070")), ["ivory"] = (2, new Color("3a3630"), new Color("fff2d0")),
        ["eclipse"] = (5, new Color("05040c"), new Color("c8d2ff")), ["crimson"] = (0, new Color("1a0406"), new Color("f0c060")),
    };

    /// The world under a plain (not forged) mat (2026-11-09: they were a bare grey gradient): the arena's gold-seamed
    /// stone floor, the rival's colours as a nebula, the art mats as their monster's sky.
    (int Kind, Color Deep, Color Glow) PlainWorld => matKey switch
    {
        "rival" => (5, Rival.Color.Darkened(0.85f), Rival.Color.Lightened(0.35f)),
        "dm" => Surrounds["art_dark_magician"], "redeyes" => Surrounds["art_red_eyes"], "blueeyes" => Surrounds["art_blue_eyes"],
        _ => (3, new Color("15131a"), new Color("e8c070")),
    };

    /// A CPU rival's own playmat: their half of the table and their half of the world (2026-11-09).
    static string RivalMat(string file) => file switch
    {
        "yugi" => "art_dark_magician", "kaiba" => "art_blue_eyes", "joey" => "art_red_eyes", "jaden" => "twin_vortex", _ => "storm",
    };

    /// Card backs per owner: your equipped back on your cards, the rival's on theirs (classic for the CPU).
    readonly Texture2D[] backs = new Texture2D[2];

    Playmat MatNamed(string name) => name switch
    {
        "rival" => new(RivalPanels.Gold, Rival.Color.Lightened(0.3f), new Color(0.07f, 0.075f, 0.09f), null, Rival.Card, false),
        "dm" => new(RivalPanels.Gold, new Color(0.75f, 0.5f, 1f), new Color(0.08f, 0.05f, 0.12f), "46986414", "46986414", true),
        "redeyes" => new(RivalPanels.Gold, new Color(1f, 0.4f, 0.3f), new Color(0.1f, 0.045f, 0.04f), "74677422", "74677422", true),
        "blueeyes" => new(RivalPanels.Gold, new Color(0.5f, 0.8f, 1f), new Color(0.04f, 0.07f, 0.11f), "89631139", "89631139", true),
        // Forged mats (game/tools/matforge.py, assets/mats/<style>.jpg): the texture is the whole mat.
        _ when name.StartsWith("forge:") => new(RivalPanels.Gold, new Color(0.62f, 0.8f, 1f), new Color(0.055f, 0.07f, 0.095f), null, null, false, name[6..]),
        // Default (LAN): neutral stone, gold for you, cool silver for the opponent.
        _ => new(RivalPanels.Gold, new Color(0.62f, 0.8f, 1f), new Color(0.055f, 0.07f, 0.095f), null, null, false),
    };

    // ---------------------------------------------------------------- build

    protected override void Build()
    {
        W = GetViewportRect().Size.X; H = GetViewportRect().Size.Y;
        Impact = Sys(new[] { "Impact", "Arial Black" }, 800, italic: true);
        Cond = Sys(new[] { "Bahnschrift", "Impact" }, 700, stretch: 75);
        Ui = Sys(new[] { "Bahnschrift", "Segoe UI" }, 500);

        if (Session.Picks.Length == 0) Session.RollPicks();
        if (Session.Selected < 0) Session.Selected = 1;
        Rival = Session.Rival;

        ulong seed = (ulong)Session.Rng.Int(1 << 30);
        int turn = 0;
        // Your equipped playmat and card back (Card List); args override them for screenshots.
        string mat = Session.Profile.Mat == Shop.DefaultMat ? "arena" : "forge:" + Session.Profile.Mat;
        string back = Session.Profile.Back, back2 = Shop.DefaultBack;
        string? mat2 = null;   // the rival's own forged mat (online); their half of the table and of the world
        foreach (var a in OS.GetCmdlineUserArgs())
        {
            if (a.StartsWith("--seed=")) seed = ulong.Parse(a[7..]);
            else if (a.StartsWith("--turn=")) turn = int.Parse(a[7..]);
            else if (a.StartsWith("--mat=")) mat = a[6..];
            else if (a.StartsWith("--mat2=forge:")) mat2 = a[13..];
            else if (a.StartsWith("--back=")) back = a[7..];
            else if (a.StartsWith("--back2=")) back2 = a[8..];
            else if (a == "--autoplay") autoplay = true;
            else if (a == "--savereplay") saveReplay = true;   // dev: save the replay the moment the duel ends, then quit
            else if (a.StartsWith("--watch=")) { Session.Watch = Replay.Load(a[8..]); watchDev = true; }   // dev: play back a replay file
            else if (a.StartsWith("--seat=")) Me = int.Parse(a[7..]) & 1;
            else if (a == "--selftest") selftest = true;
            else if (a == "--vfxlab") scenario = "vfxlab";
            else if (a == "--uilab") { scenario = "vfxlab"; uiLab = true; }
            else if (a == "--fxlab") { scenario = "vfxlab"; fxLab = true; }
            else if (a == "--cardlab") { scenario = "vfxlab"; fxLab = cardLab = true; }
            else if (a == "--trycheck") { TryCheck(); return; }
            else if (a.StartsWith("--poolcheck")) { TryCheck(a.Length > 12 ? a[12..] : ""); return; }
            else if (a.StartsWith("--labsweep")) { LabSweep(a.Length > 11 ? a[11..] : ""); return; }
            else if (a.StartsWith("--scenario=")) scenario = a[11..];
            else if (a.StartsWith("--puzzle=")) { Session.Puzzle = Puzzles.Get(a[9..]); Session.Mode = GameMode.Challenges; Session.Selected = 1; }
            else if (a == "--chaos") Session.Mode = GameMode.Chaos;
            else if (a.StartsWith("--point=")) pointName = a[8..];
            else if (a.StartsWith("--click=")) clickName = a[8..];
            else if (a.StartsWith("--chip=")) chipName = a[7..];
            else if (a.StartsWith("--gapdemo=")) GapDemo(a[10..]);   // DuelField.Marks.cs
            else if (a.StartsWith("--overlay=")) { var o = a[10..]; GetTree().CreateTimer(2.5).Timeout += () => Overlay(o); }
            else if (a.StartsWith("--aicheck=")) { AiCheck(int.Parse(a[10..])); return; }
            else if (a == "--matview") matView = true;
            else if (a.StartsWith("--vsview=")) vsView = a[9..];
            else if (a.StartsWith("--speed=")) pace = 1 / double.Parse(a[8..], System.Globalization.CultureInfo.InvariantCulture);
        }
        // The FX Lab asked for one of its bug scenarios live (DuelField.FxLab.cs): play it, then come back.
        if (labLaunch != null) { scenario = labLaunch; labLaunch = null; fxLab = cardLab = uiLab = false; backToLab = true; }
        Godot.Engine.TimeScale = 1 / pace;
        // LAN: your seat from the lobby; the opponent's equipped mat half and card back.
        if (Lan.On) { Me = Lan.Seat; mat2 = Lan.Them!.Mat == Shop.DefaultMat ? null : Lan.Them.Mat; back2 = Lan.Them.Back; }
        if (Watch is { } wr) { Me = wr.Seat; (mat, mat2, back, back2) = (wr.Mat, wr.Mat2, wr.Back, wr.Back2); }
        matKey = uiLab && labMat != null ? labMat : mat;
        Pm = MatNamed(matKey);
        if (!Lan.On && Watch == null && mat2 == null) mat2 = uiLab && labRivalMat != null ? labRivalMat : RivalMat(Rival.File);
        if (mat2 != null && Surrounds.TryGetValue(mat2, out var rw)) Pm = Pm with { TexThem = mat2, Them = rw.Glow.Lightened(0.15f) };   // their zone lines in their world's colour
        (dressMat, dressMat2, dressBack, dressBack2) = (matKey, mat2, back, back2);   // a saved replay dresses the table the same
        backs[0] = Cosmetics.Back(back); backs[1] = Cosmetics.Back(back2);   // ponytail: only forged vs forged mixes; default/art mats ignore the rival's

        var db = CardDb.Shared;
        var setup = new PoC.Engine.DuelSetup { Seed = seed, FirstPlayer = Session.YouFirst ? Me : 1 - Me, Classic = Session.Classic };
        // Your deck from DuelSetup; a fixed decks.json deck when the field is started on its own (dev/screenshots).
        var (m0, e0) = (Session.ModeDeck ?? Session.MatchDeck ?? Session.Playing) is { } mine ? (mine.Main.ToList(), mine.Extra.ToList()) : db.Deck("yugi/cpu01");
        var (m1, e1) = Session.RivalDeck is { } rd ? (rd.Main.ToList(), rd.Extra.ToList()) : db.Deck(Session.Pick.DeckKey);
        m1 = DeckRules.ForCpu(m1);   // 2004 list: the rival plays legal too; 2003: as the original decks were
        setup.Main[Me].AddRange(m0); setup.Extra[Me].AddRange(e0);
        setup.Main[1 - Me].AddRange(m1); setup.Extra[1 - Me].AddRange(e1);
        if (Session.Rule != null) setup.StartingLp = Session.Rule.Lp;
        if (Session.Mode == GameMode.Chaos) setup.Chaos = true;
        if (Session.Run != null) { setup.Lp = new int[2]; setup.Lp[Me] = Session.Run.Lp; setup.Lp[1 - Me] = 8000; }
        if (scenario != null) setup = scenario == "vfxlab" ? LabSetup() : ScenarioSetup();
        if (Lan.On) setup = Lan.Setup(Session.Playing!);
        if (Session.Puzzle is { } pz && scenario == null && !Lan.On && Watch == null) setup = Puzzles.Setup(pz);
        if (Watch != null) setup = Watch.Setup;
        D = new Duel(setup);
        if (Session.Puzzle is { } pz2 && scenario == null && !Lan.On && Watch == null) Puzzles.Prepare(D, pz2);
        bot = new Rng(seed * 31 + 7);
        cpu = Ai.ForLevel(1 - Me, seed * 17 + 3, Session.Pick.Level);
        if (!Lan.On) { cpu.Board = Session.Profile.Brain; Ai.Used = cpu.Board.Used; }   // the CPU learns from your duels
        if (scenario != null && scenario != "vfxlab") ScenarioPlay();
        while (!D.Over && D.Turn < turn) D.Submit(D.Pending!.Player != Me ? cpu.Decide(D) : Bot(D.Pending!));
        D.OnEvent += e => steps.Add(Snap(e));
        D.OnTriggered += Triggered;

        GetViewport().Msaa3D = Viewport.Msaa.Msaa4X;
        // Smooth over sharp: the table renders at 77% and FSR upscales it, and the duel holds a steady 60 FPS
        // (an even split of 120/144 Hz screens) instead of wandering between 60 and 90, which reads as jitter.
        GetViewport().Scaling3DMode = Viewport.Scaling3DModeEnum.Fsr;
        GetViewport().Scaling3DScale = 0.77f;
        Godot.Engine.MaxFps = 60;
        BuildWorld();
        if (matView) { MatView(); return; }
        if (vsView != null) { VsView(); return; }
        BuildHud();
        if (Watch != null) WatchBar();
        BuildInput();
        BuildMenuButton();
        if (sandbox) SandboxPanel();
        if (backToLab)
        {
            hud.AddChild(Chips.Make("BACK TO LAB", new Vector2(W - 196 - 12 - 200, 196), new Vector2(200, 46), () => GetTree().ReloadCurrentScene(), 18));   // beside MENU
            if (!sandbox && StageFor(scenario) is { } st)   // what you're checking, on the right under the chip
            {
                var box = Box(hud, new Rect2(W - 420, 256, 396, 250), Glass(RivalPanels.Gold * new Color(1, 1, 1, 0.5f)));   // under MENU, above the phase circle
                var l = Put(box, $"{st.Title.ToUpperInvariant()}\n{st.Todo}\n\nSHOULD: {st.Expect}", Ui, 17, Colors.White, new Vector2(14, 10), new Vector2(368, 230));
                FitWrap(l, new Vector2(368, 230), 17, 11);
            }
        }

        // Opening: everything starts in the decks and the first snapshot deals the hands.
        var now = Snap(null);
        for (int i = 0; i < 2; i++) { shownLp[i] = D.P(Side(i)).Lp; SetLp(i, shownLp[i], false); }
        foreach (var c in D.Cards)
        {
            var v = ViewOf(c.Uid);
            var s = now.Cards[c.Uid];
            if (turn == 0 && scenario == null && s.Loc == Loc.Hand) s = new St(Loc.Deck, 0, 40, false, false, s.Side, 0, 0);
            Place(v, s);
        }
        if (scenario == "vfxlab") { VfxLab(); return; }
        steps.Add(now);
        Loop();
    }

    bool matView;
    string matKey = "arena";

    /// `--matview`: the bare mat from straight above (no cards, no HUD), for shop previews of a playmat.
    void MatView()
    {
        cam.Projection = Camera3D.ProjectionType.Orthogonal;
        cam.HOffset = 0;
        cam.Position = new Vector3(0, 12, 0);
        cam.RotationDegrees = new Vector3(-90, 0, 0);
        cam.Size = MatD + 0.9f;
    }

    // ---------------------------------------------------------------- 3D world

    void BuildWorld()
    {
        world = new Node3D();
        AddChild(world);
        (int Kind, Color Deep, Color Glow)? sur = Pm.Tex != null && Surrounds.TryGetValue(Pm.Tex, out var su) ? su : PlainWorld;   // your world, under your half
        var them = Pm.TexThem != null && Surrounds.TryGetValue(Pm.TexThem, out var st2) ? st2 : sur;   // the rival's world, under theirs
        var env = new Godot.Environment
        {
            BackgroundMode = Godot.Environment.BGMode.Sky,
            Sky = new Sky { SkyMaterial = sur is { } sk && them is { } tk ? Shade(SkyShader, ("top", tk.Deep.Darkened(0.6f)), ("low", sk.Deep.Lerp(tk.Deep, 0.5f)), ("accent", tk.Glow), ("detail", uiLab && labSky == 0 ? 0f : 1f))
                : Shade(SkyShader, ("top", Pm.Them.Darkened(0.85f)), ("low", Pm.Stone * 0.6f), ("accent", Pm.Them)) },
            AmbientLightSource = Godot.Environment.AmbientSource.Color, AmbientLightColor = Pm.Stone.Lightened(0.35f), AmbientLightEnergy = 0.55f,
            TonemapMode = Godot.Environment.ToneMapper.Filmic, TonemapExposure = 1.05f,
            GlowEnabled = true, GlowIntensity = 0.75f, GlowBloom = 0.08f, GlowHdrThreshold = 0.9f,
            FogEnabled = true, FogLightColor = Pm.Stone.Lightened(0.15f), FogDensity = 0.02f,
        };
        if (sur is { } sf)
        {
            // The depths below the table fade into the mat's own haze.
            env.FogLightColor = sf.Deep.Lerp(them!.Value.Deep, 0.5f); env.FogDensity = 0.012f;
            env.FogHeight = -1.0f; env.FogHeightDensity = 0.12f;
        }
        world.AddChild(new WorldEnvironment { Environment = env });

        cam = new Camera3D { Fov = 33, Position = new Vector3(0, 11.07f, 6.67f), HOffset = -0.18f };
        world.AddChild(cam);
        cam.LookAt(new Vector3(0, 0, 0.95f));
        camHome = cam.Position;

        var sun = new DirectionalLight3D { LightEnergy = 1.1f, ShadowEnabled = true, ShadowBlur = 2.5f, LightColor = new Color(1f, 0.95f, 0.88f), DirectionalShadowMaxDistance = 25 };
        world.AddChild(sun);
        sun.RotationDegrees = new Vector3(-62, 28, 0);
        // Side rim lights: each half glows in its owner's colour.
        world.AddChild(new OmniLight3D { Position = new Vector3(0, 2.2f, -3.6f), LightColor = Pm.Them, LightEnergy = 1.4f, OmniRange = 7 });
        world.AddChild(new OmniLight3D { Position = new Vector3(0, 2.2f, 3.6f), LightColor = Pm.You, LightEnergy = 0.7f, OmniRange = 6.5f });

        // Floor around the mat (a forged mat brings its own surroundings far below), then the mat.
        if (sur == null)
            world.AddChild(new MeshInstance3D { Mesh = new PlaneMesh { Size = new Vector2(80, 80) }, Position = new Vector3(0, -0.06f, 0), MaterialOverride = Shade(FloorShader, ("tint", Pm.Stone), ("accent", Pm.Them)) });
        var mat = Shade(MatShader, ("stone", Pm.Stone), ("col_you", Pm.You), ("col_them", Pm.Them), ("size", new Vector2(MatW, MatD)), ("full", Pm.Full ? 1f : 0f));
        if (Pm.ArtYou != null) { mat.SetShaderParameter("art_you", Tex($"res://assets/art/art-{Pm.ArtYou}.jpg")); mat.SetShaderParameter("has_you", 1f); }
        if (Pm.ArtThem != null) { mat.SetShaderParameter("art_them", Tex($"res://assets/art/art-{Pm.ArtThem}.jpg")); mat.SetShaderParameter("has_them", 1f); }
        if (Pm.Tex != null && Cosmetics.ResImage($"res://assets/mats/{Pm.Tex}.jpg") is { } mi)
        {
            mi.GenerateMipmaps();
            var tex = ImageTexture.CreateFromImage(mi);
            mat.SetShaderParameter("mat_tex", tex); mat.SetShaderParameter("mat_them", tex); mat.SetShaderParameter("has_mat", 1f); mat.SetShaderParameter("has_mat_them", 1f);
        }
        // The rival's half shows their own mat (mats are 180-degree symmetric, so their half reads upright for them).
        if (Pm.TexThem != null && Cosmetics.ResImage($"res://assets/mats/{Pm.TexThem}.jpg") is { } mt)
        {
            mt.GenerateMipmaps();
            mat.SetShaderParameter("mat_them", ImageTexture.CreateFromImage(mt)); mat.SetShaderParameter("has_mat_them", 1f);
        }
        world.AddChild(new MeshInstance3D { Mesh = Slab(MatW, MatD, 0.08f, MatCorner), Position = new Vector3(0, -0.04f, 0), MaterialOverride = mat });

        foreach (var z in Zones())
        {
            var c = z.Side == 0 ? Pm.You : Pm.Them;
            bool mon = z.Loc == Loc.Monster;
            var size = new Vector2(CW + 0.12f, CH + 0.12f);
            var frame = new MeshInstance3D
            {
                Mesh = new QuadMesh { Size = size }, RotationDegrees = new Vector3(-90, 0, 0), CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
                MaterialOverride = Shade(FrameShader, ("col", c), ("size", size), ("strength", mon ? 0.85f : 0.5f)),
            };
            frame.Position = Grid3(z) + new Vector3(0, z.Loc == Loc.Banished ? -0.056f : 0.003f, 0);
            world.AddChild(frame);
            if (z.Loc == Loc.Banished) { frame.Visible = false; banishMarks[z.Side].Add(frame); }
            if (mon)
            {
                // Cracked ground (UI Lab round 4): the zone darkens, a red crack lies over it, embers glow in the cracks.
                var bm = new MeshInstance3D { Mesh = new QuadMesh { Size = size }, RotationDegrees = new Vector3(-90, 0, 0), CastShadow = GeometryInstance3D.ShadowCastingSetting.Off, Visible = false,
                    MaterialOverride = new StandardMaterial3D { ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded, Transparency = BaseMaterial3D.TransparencyEnum.Alpha, AlbedoColor = new Color(0.02f, 0.01f, 0.01f, 0.72f), DisableReceiveShadows = true },
                    Position = Grid3(z) + new Vector3(0, 0.005f, 0) };
                var cm = Additive(VfxTex("md/crack"), new Color(1f, 0.3f, 0.2f) * 1.6f);
                bm.AddChild(new MeshInstance3D { Name = "crack", Mesh = new QuadMesh { Size = new Vector2(CW * 1.25f, CH * 1.05f) }, Position = new Vector3(0, 0, 0.002f), MaterialOverride = cm, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off });
                var glow = bm.CreateTween().SetLoops();
                glow.TweenProperty(cm, "albedo_color:a", 1f, 0.9).SetTrans(Tween.TransitionType.Sine);
                glow.TweenProperty(cm, "albedo_color:a", 0.45f, 0.9).SetTrans(Tween.TransitionType.Sine);
                world.AddChild(bm);
                blockMarks[z.Side, z.Index] = bm;
            }
            var label = ZoneLabel(z.Loc);
            if (label == "") continue;
            var l = new Label3D { Text = label, Font = Cond, FontSize = 44, PixelSize = 0.0035f, Modulate = c * new Color(1, 1, 1, 0.45f), OutlineSize = 0, Shaded = false, DoubleSided = false };
            l.RotationDegrees = new Vector3(-90, z.Side == 1 ? 180 : 0, 0);
            l.Position = Grid3(z) + new Vector3(0, z.Loc == Loc.Banished ? -0.055f : 0.004f, 0);
            world.AddChild(l);
            if (z.Loc == Loc.Banished) { l.Visible = false; banishMarks[z.Side].Add(l); }
        }

        if (sur is { } s)
        {
            // A forged mat is the whole table: one slab, same edge as the mat, floating over the mat's own world (see Surrounds).
            var dark = new StandardMaterial3D { AlbedoColor = s.Deep.Lerp(Pm.Stone, 0.4f).Lightened(0.12f), Metallic = 0.8f, Roughness = 0.4f };
            world.AddChild(new MeshInstance3D { Mesh = Slab(MatW, MatD, 0.3f, MatCorner), Position = new Vector3(0, -0.23f, 0), MaterialOverride = dark });
            world.AddChild(new MeshInstance3D { Mesh = new PlaneMesh { Size = new Vector2(140, 140) }, Position = new Vector3(0, -4.2f, 0), MaterialOverride = Shade(SurroundShader, ("kind", s.Kind), ("deep", s.Deep), ("glow", s.Glow), ("kind2", them!.Value.Kind), ("deep2", them.Value.Deep), ("glow2", them.Value.Glow), ("split", 1f), ("seam", uiLab && labSeam >= 0 ? labSeam : 2)), CastShadow = GeometryInstance3D.ShadowCastingSetting.Off });
            // A drifting haze layer between the table and the depths: parallax when the camera moves.
            world.AddChild(new MeshInstance3D { Mesh = new PlaneMesh { Size = new Vector2(140, 140) }, Position = new Vector3(0, -2.0f, 0), MaterialOverride = Shade(HazeShader, ("glow", s.Glow), ("glow2", them!.Value.Glow)), CastShadow = GeometryInstance3D.ShadowCastingSetting.Off });
        }
        else
        {
            // The default table: a rounded metal slab with a gold edge, corner pylons, the rune circle on the floor.
            world.AddChild(new MeshInstance3D { Mesh = Slab(MatW + 0.5f, MatD + 0.5f, 0.3f, MatCorner + 0.25f), Position = new Vector3(0, -0.23f, 0), MaterialOverride = new StandardMaterial3D { AlbedoColor = Pm.Stone.Lightened(0.08f), Metallic = 0.85f, Roughness = 0.32f } });
            var trim = new StandardMaterial3D { AlbedoColor = RivalPanels.Gold, EmissionEnabled = true, Emission = RivalPanels.Gold, EmissionEnergyMultiplier = 1.6f, Metallic = 1, Roughness = 0.2f };
            world.AddChild(new MeshInstance3D { Mesh = Slab(MatW + 0.56f, MatD + 0.56f, 0.03f, MatCorner + 0.28f), Position = new Vector3(0, -0.0955f, 0), MaterialOverride = trim, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off });
            var metal = new StandardMaterial3D { AlbedoColor = Pm.Stone.Lightened(0.15f), Metallic = 0.9f, Roughness = 0.3f };
            foreach (var sx in new[] { -1, 1 })
            {
                var c = Pm.Them;
                var at = new Vector3(sx * (MatW / 2 + 0.75f), 0, -(MatD / 2 + 0.45f));
                world.AddChild(new MeshInstance3D { Mesh = new CylinderMesh { TopRadius = 0.1f, BottomRadius = 0.18f, Height = 0.9f, RadialSegments = 24 }, Position = at + new Vector3(0, 0.3f, 0), MaterialOverride = metal });
                var crystal = new MeshInstance3D
                {
                    Mesh = new SphereMesh { Radius = 0.1f, Height = 0.42f, RadialSegments = 6, Rings = 3 }, Position = at + new Vector3(0, 1.0f, 0), CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
                    MaterialOverride = new StandardMaterial3D { AlbedoColor = c, EmissionEnabled = true, Emission = c, EmissionEnergyMultiplier = 2.5f },
                };
                world.AddChild(crystal);
                crystals.Add(crystal);
                world.AddChild(new OmniLight3D { Position = at + new Vector3(0, 1.0f, 0), LightColor = c, LightEnergy = 0.9f, OmniRange = 2.6f });
            }
            world.AddChild(new MeshInstance3D { Mesh = new QuadMesh { Size = new Vector2(15, 15) }, RotationDegrees = new Vector3(-90, 0, 0), Position = new Vector3(0, -0.05f, 0), MaterialOverride = Shade(RuneShader, ("you", Pm.You), ("them", Pm.Them)), CastShadow = GeometryInstance3D.ShadowCastingSetting.Off });
        }

        if (uiLab && labShards == 1) { Shards(sur!.Value, 1); Shards(them!.Value, -1); }   // off: UI Lab round 11 picked sky detail only
        // Drifting motes for life.
        if (!matView)
        {
            if (uiLab && labMotes == 1)   // motes off (UI Lab round 10 pick); the lab can still show them
            {
                world.AddChild(Motes(sur!.Value.Glow, new Vector3(0, 0.6f, 1.6f)));
                world.AddChild(Motes(them!.Value.Glow, new Vector3(0, 0.6f, -1.6f)));
            }
        }
    }

    /// Broken rock drifting around the table on one side (side 1 = yours, toward the camera), lit at the rim in that
    /// world's colour, so the space round the table is not an empty gradient. Fixed seed: the same in every duel.
    void Shards((int Kind, Color Deep, Color Glow) w, int side)
    {
        var rng = new RandomNumberGenerator { Seed = side > 0 ? 71u : 72u };
        var mat = Shade(ShardShader, ("deep", w.Deep), ("glow", w.Glow));
        Mesh[] shapes = { new PrismMesh { Size = Vector3.One }, new BoxMesh { Size = new Vector3(1, 0.5f, 0.8f) }, new SphereMesh { Radius = 0.5f, Height = 0.8f, RadialSegments = 5, Rings = 2 } };
        for (int i = 0; i < 30; i++)
        {
            // your side: in front of the table, where the camera sees it; theirs: out toward the horizon
            float x = rng.RandfRange(-13, 13), z = side > 0 ? rng.RandfRange(2.6f, 7.5f) : -rng.RandfRange(2.6f, 12f);
            if (Mathf.Abs(x) < 4.8f && Mathf.Abs(z) < 3.2f) x = Mathf.Sign(x == 0 ? 1 : x) * rng.RandfRange(5.2f, 13f);   // never under or over the table
            float s = rng.RandfRange(0.1f, 0.42f) * (Mathf.Abs(z) > 7 ? 1.5f : 1f);
            var m = new MeshInstance3D { Mesh = shapes[rng.RandiRange(0, 2)], MaterialOverride = mat, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
                Position = new Vector3(x, rng.RandfRange(-3.2f, -0.9f), z), Scale = new Vector3(s, s * rng.RandfRange(0.6f, 1.4f), s),
                Rotation = new Vector3(rng.RandfRange(0, Mathf.Tau), rng.RandfRange(0, Mathf.Tau), rng.RandfRange(0, Mathf.Tau)) };
            m.SetInstanceShaderParameter("phase", rng.Randf() * 50f);
            world.AddChild(m);
        }
    }

    const string ShardShader = @"shader_type spatial;
render_mode cull_disabled, unshaded;
uniform vec4 deep : source_color;
uniform vec4 glow : source_color;
instance uniform float phase = 0.0;
void vertex() {
  float t = TIME * 0.25 + phase;
  VERTEX.y += sin(t) * 0.15;   // a slow bob, each shard on its own beat
}
void fragment() {
  float rim = pow(1.0 - abs(dot(NORMAL, VIEW)), 3.0);
  float facet = 0.5 + 0.5 * dot(NORMAL, normalize(vec3(0.3, 0.8, 0.5)));   // flat facets, faintly shaded
  ALBEDO = deep.rgb * (0.25 + 0.35 * facet) + glow.rgb * rim * 0.9;
}";

    GpuParticles3D Motes(Color c, Vector3 at)
    {
        var grad = new Gradient();
        grad.SetColor(0, new Color(c, 0)); grad.SetColor(1, new Color(c, 0));
        grad.AddPoint(0.3f, new Color(c, 0.9f)); grad.AddPoint(0.7f, new Color(c, 0.6f));
        return new GpuParticles3D
        {
            Position = at, Amount = 70, Lifetime = 7, Preprocess = 7,
            ProcessMaterial = new ParticleProcessMaterial
            {
                EmissionShape = ParticleProcessMaterial.EmissionShapeEnum.Box, EmissionBoxExtents = new Vector3(4.2f, 0.6f, 1.8f),
                Direction = new Vector3(0, 1, 0), Spread = 40, InitialVelocityMin = 0.04f, InitialVelocityMax = 0.16f, Gravity = new Vector3(0, 0.015f, 0),
                ScaleMin = 0.4f, ScaleMax = 1.2f, ColorRamp = new GradientTexture1D { Gradient = grad },
            },
            DrawPass1 = new QuadMesh { Size = new Vector2(0.07f, 0.07f), Material = Glowing(2) },
        };
    }

    /// Mat corner radius; matches the rounded edge painted by matforge.py (CORNER) and the gold line in MatShader.
    const float MatCorner = 0.47f;

    /// A rounded-corner slab (w x d, h tall, centred on the origin): top face and sides, no bottom.
    static ArrayMesh Slab(float w, float d, float h, float r, int seg = 10)
    {
        var ring = new List<Vector3>();
        float hx = w / 2 - r, hz = d / 2 - r;
        foreach (var (cx, cz, a0) in new[] { (hx, hz, 0f), (-hx, hz, 90f), (-hx, -hz, 180f), (hx, -hz, 270f) })
            for (int i = 0; i <= seg; i++)
            {
                float a = Mathf.DegToRad(a0 + 90f * i / seg);
                ring.Add(new Vector3(cx + r * Mathf.Cos(a), 0, cz + r * Mathf.Sin(a)));
            }
        var st = new SurfaceTool();
        st.Begin(Mesh.PrimitiveType.Triangles);
        var up = new Vector3(0, h / 2, 0);
        for (int i = 0; i < ring.Count; i++)
        {
            var a = ring[i]; var b = ring[(i + 1) % ring.Count];
            st.SetNormal(Vector3.Up);
            st.AddVertex(up); st.AddVertex(a + up); st.AddVertex(b + up);
            var n = new Vector3(b.Z - a.Z, 0, a.X - b.X).Normalized();
            st.SetNormal(n);
            st.AddVertex(a + up); st.AddVertex(b - up); st.AddVertex(b + up);
            st.AddVertex(a + up); st.AddVertex(a - up); st.AddVertex(b - up);
        }
        return st.Commit();
    }

    static ShaderMaterial Shade(string code, params (string, Variant)[] u) => MenuBase.Mat(code, u);

    Vector3 Grid3(St s)
    {
        var g = Grid(s);
        return new Vector3(g.X * PX, 0, g.Y * PZ);
    }

    /// Zone position in pitches, origin at the board centre, +y toward you.
    /// Columns: Field/Fusion -3, zones -2..2, Graveyard/Deck 3; Banished sits off the table behind the deck (the rival's mirror to the other side).
    static Vector2 Grid(St s)
    {
        var v = s.Loc switch
        {
            Loc.Monster => new Vector2(s.Index - 2, 0.62f),
            Loc.Spell => new Vector2(s.Index - 2, 1.62f),
            Loc.Field => new Vector2(-3, 0.62f),
            Loc.Grave => new Vector2(3, 0.62f),
            Loc.Banished => new Vector2(3, (MatD / 2 + 0.75f) / PZ), // off the table, behind the deck
            Loc.Deck => new Vector2(3, 1.62f),
            _ => new Vector2(-3, 1.62f), // Extra (Fusion Deck)
        };
        return s.Side == 1 ? -v : v;
    }

    static IEnumerable<St> Zones()
    {
        for (int side = 0; side < 2; side++)
        {
            for (int i = 0; i < 5; i++) yield return new St(Loc.Monster, i, 0, false, false, side, 0, 0);
            for (int i = 0; i < 5; i++) yield return new St(Loc.Spell, i, 0, false, false, side, 0, 0);
            foreach (var l in new[] { Loc.Field, Loc.Grave, Loc.Deck, Loc.Extra, Loc.Banished })
                yield return new St(l, 0, 0, false, false, side, 0, 0);
        }
    }

    static string ZoneLabel(Loc l) => l switch { Loc.Field => "FIELD", Loc.Grave => "GRAVE", Loc.Deck => "DECK", Loc.Extra => "FUSION", Loc.Banished => "BANISH", _ => "" };

    /// Where a card in state s rests (world transform). Hands are laid out in camera space.
    Transform3D Target(View v, St s)
    {
        var faceUp = new Basis(Vector3.Forward, s.Up ? 0 : Mathf.Pi);
        if (s.Loc == Loc.Hand)
        {
            int i = s.Side == 1 ? s.Count - 1 - s.Index : s.Index;
            float mid = (s.Count - 1) / 2f;
            if (s.Side == 0)
            {
                float step = s.Count <= 1 ? 0 : Math.Min(0.64f, 3.6f / (s.Count - 1));
                float a = (i - mid) * -0.045f;
                bool hot = v.Card.Uid == hovered;
                var local = new Vector3((i - mid) * step, -1.8f + Math.Abs(i - mid) * -0.025f + (hot ? 0.32f : 0), -7f + (hot ? 0.4f : 0) + i * 0.002f);
                return new Transform3D(cam.Basis * new Basis(Vector3.Right, Mathf.Pi / 2) * new Basis(Vector3.Up, a), cam.Transform * local);
            }
            float rs = s.Count <= 1 ? 0 : Math.Min(0.55f, 3.2f / (s.Count - 1));
            var pos = new Vector3((i - mid) * rs, 0.55f + Math.Abs(i - mid) * -0.03f, -2.95f);
            var face = s.Up ? Basis.Identity : new Basis(Vector3.Forward, Mathf.Pi);   // Ceremonial Bell / hotseat: their hand shows its faces
            return new Transform3D(new Basis(Vector3.Right, 1.0f) * new Basis(Vector3.Up, (i - mid) * 0.04f) * face, pos);
        }
        var p = Grid3(s);
        float yaw = s.Side == 1 ? Mathf.Pi : 0;
        if (s.Loc == Loc.Monster && s.Def) yaw += Mathf.Pi / 2;
        float y = s.Loc == Loc.Banished ? -0.045f : 0.012f;
        if (s.Loc is Loc.Deck or Loc.Grave or Loc.Banished or Loc.Extra)
        {
            int depth = s.Loc is Loc.Deck or Loc.Extra ? s.Count - 1 - s.Index : s.Index;
            y += Math.Min(depth * 0.0065f, 0.3f);
        }
        return new Transform3D(new Basis(Vector3.Up, yaw) * faceUp, p + new Vector3(0, y, 0));
    }

    View ViewOf(int uid)
    {
        if (views.TryGetValue(uid, out var v)) return v;
        v = new View { Card = D.CardByUid(uid) };
        v.Root = new Node3D();
        // Thin slab for thickness, the face on top and the back underneath.
        v.Root.AddChild(new MeshInstance3D { Mesh = new BoxMesh { Size = new Vector3(CW, 0.008f, CH) }, MaterialOverride = new StandardMaterial3D { AlbedoColor = new Color(0.12f, 0.08f, 0.04f), Roughness = 0.6f } });
        v.Face = new StandardMaterial3D { AlbedoTexture = backs[Side(v.Card.Owner)], Roughness = 0.5f, DisableFog = true, TextureFilter = BaseMaterial3D.TextureFilterEnum.LinearWithMipmapsAnisotropic, EmissionEnabled = true, Emission = Colors.Black };
        v.Root.AddChild(new MeshInstance3D { Mesh = new QuadMesh { Size = new Vector2(CW, CH) }, RotationDegrees = new Vector3(-90, 0, 0), Position = new Vector3(0, 0.0045f, 0), MaterialOverride = v.Face });
        v.Root.AddChild(new MeshInstance3D
        {
            Mesh = new QuadMesh { Size = new Vector2(CW, CH) }, RotationDegrees = new Vector3(90, 0, 0), Position = new Vector3(0, -0.0045f, 0),
            MaterialOverride = v.BackMat = new StandardMaterial3D { AlbedoTexture = backs[Side(v.Card.Owner)], Roughness = 0.5f, DisableFog = true, TextureFilter = BaseMaterial3D.TextureFilterEnum.LinearWithMipmapsAnisotropic },
        });
        v.ShineMat = Shade(ShineShader, ("t", -1f));
        v.Root.AddChild(new MeshInstance3D { Mesh = new QuadMesh { Size = new Vector2(CW, CH) }, RotationDegrees = new Vector3(-90, 0, 0), Position = new Vector3(0, 0.0052f, 0), MaterialOverride = v.ShineMat, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off });
        var auraSize = new Vector2(CW + 0.4f, CH + 0.4f);
        v.Aura = new MeshInstance3D
        {
            Mesh = new QuadMesh { Size = auraSize }, RotationDegrees = new Vector3(-90, 0, 0), Position = new Vector3(0, -0.006f, 0), Visible = false, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            MaterialOverride = Shade(AuraShader, ("col", v.Card.Controller == Me ? Pm.You : Pm.Them), ("size", auraSize), ("card", new Vector2(CW, CH))),
        };
        v.Root.AddChild(v.Aura);
        world.AddChild(v.Root);

        Label3D StatText(HorizontalAlignment align, Vector2 offset) => new()
        {
            Font = Cond, FontSize = 30, PixelSize = 0.0042f, Billboard = BaseMaterial3D.BillboardModeEnum.Enabled, NoDepthTest = true,
            OutlineSize = 14, OutlineModulate = new Color(0, 0, 0, 0.9f), RenderPriority = 2, OutlineRenderPriority = 1, HorizontalAlignment = align, Offset = offset,
        };
        v.Stat = new Node3D { Visible = false };
        v.StatAtk = StatText(HorizontalAlignment.Right, new Vector2(-8, 0));
        v.StatDef = StatText(HorizontalAlignment.Left, new Vector2(8, 0));
        var slash = StatText(HorizontalAlignment.Center, Vector2.Zero);
        slash.Text = "/"; slash.Modulate = new Color(1, 1, 1, 0.45f);
        v.Stat.AddChild(v.StatAtk); v.Stat.AddChild(v.StatDef); v.Stat.AddChild(slash);
        v.Badge = new Label3D { Font = Impact, FontSize = 52, PixelSize = 0.004f, Billboard = BaseMaterial3D.BillboardModeEnum.Enabled, NoDepthTest = true, OutlineSize = 7, Modulate = new Color(1f, 0.97f, 0.9f), OutlineModulate = new Color(0.45f, 0.3f, 0.05f, 1), Visible = false, RenderPriority = 3, OutlineRenderPriority = 2 };
        v.Badge.AddChild(new Sprite3D { Texture = VfxTex("badge"), PixelSize = 0.0024f, Billboard = BaseMaterial3D.BillboardModeEnum.Enabled, NoDepthTest = true, RenderPriority = 1 });
        v.Count = new Label3D { Font = Impact, FontSize = 32, PixelSize = 0.004f, Billboard = BaseMaterial3D.BillboardModeEnum.Enabled, NoDepthTest = true, OutlineSize = 8, Modulate = new Color(0.55f, 0.9f, 1f), OutlineModulate = new Color(0, 0.08f, 0.15f, 1), Visible = false, RenderPriority = 2, OutlineRenderPriority = 1 };
        world.AddChild(v.Stat); world.AddChild(v.Badge); world.AddChild(v.Count);
        return views[uid] = v;
    }

    public override void _Process(double delta)
    {
        base._Process(delta);
        if (cam == null || matView) return;
        if (vsView != null) { VsAnimate(); return; }
        foreach (var v in views.Values)
        {
            var p = v.Root.GlobalPosition;
            // ATK / DEF small, just under the card (its near edge; a Defense Position card's is its long side)
            float under = (v.S.Def ? CW : CH) / 2 + 0.1f;
            if (v.Stat.Visible) v.Stat.Position = p + new Vector3(0, 0.12f, v.S.Side == 0 ? under : -under);
            if (v.Badge.Visible) v.Badge.Position = p + new Vector3(CW * 0.42f, 0.12f, -CH * 0.42f);   // the card's far right corner
            PlaceMarks(v);   // counters, turn count, Level (DuelField.Marks.cs)
        }
        LanWatch();
        Hover();
        UpdateActionChip();
        float t = (float)Time.GetTicksMsec() / 1000f;
        for (int i = 0; i < crystals.Count; i++)
        {
            crystals[i].RotationDegrees = new Vector3(0, t * 40 + i * 30, 0);
            crystals[i].Position = new Vector3(crystals[i].Position.X, 1.0f + Mathf.Sin(t * 1.6f + i) * 0.05f, crystals[i].Position.Z);
        }
    }

    void Hover()
    {
        var m = GetViewport().GetMousePosition();
        int best = -1;
        float bestZ = -1;
        bool overSheet = sheet != null && sheet.GetGlobalRect().HasPoint(m);   // a card list (Monster Reborn...) over the field
        foreach (var v in views.Values)
        {
            if (overSheet || !v.Root.Visible || v.S.Loc == Loc.None) continue;
            var t = v.Root.GlobalTransform;
            var poly = new[] { new Vector3(-CW / 2, 0, -CH / 2), new Vector3(CW / 2, 0, -CH / 2), new Vector3(CW / 2, 0, CH / 2), new Vector3(-CW / 2, 0, CH / 2) }
                .Select(c => cam.UnprojectPosition(t * c)).ToArray();
            if (!Geometry2D.IsPointInPolygon(m, poly)) continue;
            // Hand above field above piles; the top card of a pile wins.
            float z = v.S.Loc == Loc.Hand ? 100 + v.S.Index : v.S.Loc is Loc.Monster or Loc.Spell or Loc.Field ? 50
                : 1 + (v.S.Loc is Loc.Deck or Loc.Extra ? v.S.Count - v.S.Index : v.S.Index) / 100f;
            if (z > bestZ) { bestZ = z; best = v.Card.Uid; }
        }
        if (best == hovered) return;
        int old = hovered;
        hovered = best;
        foreach (var uid in new[] { old, best })
            if (uid >= 0 && views[uid].S.Loc == Loc.Hand && views[uid].S.Side == 0) Move(views[uid], views[uid].S, 0, 0.16f, 0);
        ShowLinks(best);
        if (best >= 0 && acts.ContainsKey(best)) UiSound("cursor");
        // S.Up (the rules' state), not Front (the animation's): a face-down card whose view was left turned up by a
        // reveal or a cut-short flip showed its text on hover (playtest 2026-11-09).
        if (best >= 0 && (views[best].S.Up || (views[best].S.Side == 0 && views[best].S.Loc is Loc.Monster or Loc.Spell or Loc.Field))) Focus(views[best].Card);
    }

    // ---------------------------------------------------------------- moving cards

    /// Hand cards face the camera and are drawn unlit so they keep their true colours.
    static void Lit(View v, bool lit)
    {
        var m = lit ? BaseMaterial3D.ShadingModeEnum.PerPixel : BaseMaterial3D.ShadingModeEnum.Unshaded;
        v.Face.ShadingMode = m; v.BackMat.ShadingMode = m;
        // Hand cards float above the table: their shadows were hard dark rectangles on the floor.
        foreach (var g in v.Root.GetChildren().OfType<GeometryInstance3D>())
            g.CastShadow = lit && g != v.Aura && g.MaterialOverride != v.ShineMat ? GeometryInstance3D.ShadowCastingSetting.On : GeometryInstance3D.ShadowCastingSetting.Off;
    }

    void Place(View v, St s)
    {
        Lit(v, s.Loc != Loc.Hand);
        v.Tw?.Kill();
        v.S = s;
        v.Root.Transform = Target(v, s);
        v.Root.Visible = s.Loc != Loc.None;
        SetFace(v, s.Up);
        UpdateStat(v, false);
    }

    void Move(View v, St s, float delay, float dur, float lift)
    {
        v.S = s;
        if (s.Loc == Loc.None) { v.Root.Visible = false; v.Stat.Visible = false; return; }
        v.Root.Visible = true;
        var from = v.Root.Transform;
        var to = Target(v, s);
        var q0 = from.Basis.GetRotationQuaternion();
        var q1 = to.Basis.GetRotationQuaternion();
        bool flip = s.Up != v.Front;
        if (flip) lift = Math.Max(lift, 0.45f);
        v.Stat.Visible = false; v.Count.Visible = false;
        // A card already in flight continues at once (restarting with a delay would freeze it mid-air).
        if (v.Tw != null && v.Tw.IsRunning()) delay = 0;
        v.Tw?.Kill();
        v.Tw = CreateTween();
        v.Tw.TweenInterval(delay);
        v.Tw.TweenCallback(Callable.From(() => Lit(v, s.Loc != Loc.Hand)));
        v.Tw.TweenMethod(Callable.From<float>(t =>
        {
            float e = t < 0.5f ? 4 * t * t * t : 1 - Mathf.Pow(-2 * t + 2, 3) / 2;
            v.Root.Transform = new Transform3D(new Basis(q0.Slerp(q1, e)), from.Origin.Lerp(to.Origin, e) + Vector3.Up * Mathf.Sin(Mathf.Pi * t) * lift);
            if (flip && t >= 0.5f && v.Front != s.Up) SetFace(v, s.Up);
        }), 0f, 1f, dur);
        v.Tw.TweenCallback(Callable.From(() => { SetFace(v, s.Up); UpdateStat(v, false); }));
    }

    void SetFace(View v, bool front)
    {
        v.Front = front;
        v.Face.AlbedoTexture = CardArt.Small(v.Card.Data);
        // The face quad always shows the card; face-down cards are simply turned over.
    }

    void UpdateStat(View v, bool pulse)
    {
        var s = v.S;
        // Battle Phase: a pulsing aura on monsters that can attack, attacked ones dimmed.
        Aura(v);
        v.Face.AlbedoColor = s.Ready == 2 ? new Color(0.42f, 0.42f, 0.48f) : Colors.White;
        v.Count.Visible = s.Counter > 0 && s.Loc is Loc.Monster or Loc.Spell or Loc.Field;
        v.Count.Text = s.Counter.ToString();
        UpdateMarks(v);
        v.Stat.Visible = s.Loc == Loc.Monster && (s.Up || s.Side == 0) && !crackedEarly.Contains(v.Card.Uid);
        if (!v.Stat.Visible) return;
        var d = v.Card.Data;
        v.StatAtk.Text = s.Atk.ToString();
        v.StatDef.Text = s.Df.ToString();
        // Lit number = current position (boosted green, weakened red); the other is muted.
        Color Lit(int now, int printed) => s.Ready == 2 ? new Color(0.6f, 0.6f, 0.65f) : now > printed ? new Color(0.45f, 0.75f, 1f) : now < printed ? new Color(1f, 0.5f, 0.45f) : Colors.White;
        // The muted number keeps its colour (boosted blue / weakened red), just dimmed.
        static Color Dim(Color c) => new(c.R * 0.75f, c.G * 0.75f, c.B * 0.8f, 0.55f);
        v.StatAtk.Modulate = s.Def ? Dim(Lit(s.Atk, d.Atk)) : Lit(s.Atk, d.Atk);
        v.StatDef.Modulate = s.Def ? Lit(s.Df, d.Def) : Dim(Lit(s.Df, d.Def));
        v.StatAtk.FontSize = s.Def ? 25 : 32;
        v.StatDef.FontSize = s.Def ? 32 : 25;
        if (!pulse) return;
        v.Stat.Scale = new Vector3(1.6f, 1.6f, 1.6f);
        CreateTween().TweenProperty(v.Stat, "scale", Vector3.One, 0.4).SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
    }

    int Sync(St[] st, Ev? ev)
    {
        int k = 0;
        for (int uid = 0; uid < st.Length; uid++)
        {
            var s = st[uid];
            var v = ViewOf(uid);
            if (v.S == s) continue;
            if (v.S with { Atk = 0, Df = 0, Ready = 0, Counter = 0 } == s with { Atk = 0, Df = 0, Ready = 0, Counter = 0 }) { var was = v.S; bool pulse = s.Atk != was.Atk || s.Df != was.Df; if (s.Counter > 0 && s.Counter != was.Counter) Sfx("SE_CARD_COUNTER"); v.S = s; UpdateStat(v, pulse); if (pulse && s.Loc == Loc.Monster && s.Up) StatChange(v, was); continue; }
            bool drop = ev?.Card?.Uid == uid && ev.Kind is EvKind.Summoned or EvKind.Set;
            bool draw = v.S.Loc == Loc.Deck && s.Loc == Loc.Hand;
            // Back to the hand from the field flies high; a monster changing sides arcs across; a flipped monster pops up.
            bool home = ev?.Card?.Uid == uid && ev.Kind == EvKind.ToHand && v.S.Loc is Loc.Monster or Loc.Spell or Loc.Field;
            bool swap = ev?.Card?.Uid == uid && ev.Kind == EvKind.ControlChanged;
            bool pop = ev?.Card?.Uid == uid && ev.Kind == EvKind.Flipped;
            // Cards shifting within the same hand or pile just slide; real moves are staggered.
            if (v.S.Loc == s.Loc && v.S.Side == s.Side && s.Loc is Loc.Hand or Loc.Deck or Loc.Grave or Loc.Extra or Loc.Banished) { Move(v, s, 0, 0.3f, 0); continue; }
            Move(v, s, k++ * 0.12f, drop ? 0.5f : draw ? 0.55f : swap ? 0.6f : home || pop ? 0.5f : 0.45f, drop ? 1.1f : swap ? 0.9f : pop ? 0.8f : home ? 0.6f : draw ? 0.5f : 0.3f);
        }
        return k;
    }

    // ---------------------------------------------------------------- engine loop

    async void Loop()
    {
        try
        {
            Sfx("SE_DUEL_START");
            await Wait(0.5);
            while (true)
            {
                var batch = carried.Concat(steps).ToList();
                steps.Clear(); carried.Clear();
                // A card going to the Graveyard can open a response window (a tribute, a destroyed defender), and a Fusion
                // asks for its position after its materials are gone: the batch then ends before the summon or destruction
                // that explains the move. Hold the trailing moves until the next batch so they play as tribute / fusion /
                // destruction instead of a plain move. Not when the duel is back at an open Main/Battle prompt: the action
                // is over (a resolved Spell going to the Graveyard, a boost) and must show now, not after your next move.
                if (!D.Over && D.Pending?.Kind is not (Ask.Main or Ask.Battle))
                {
                    // (each batch ends with a plain snapshot taken after the command: look past those)
                    int cut = batch.Count;
                    while (cut > 0 && (batch[cut - 1].Ev == null || batch[cut - 1].Ev!.Kind is EvKind.LeftField or EvKind.ToGrave)) cut--;
                    if (batch.Skip(cut).Any(x => x.Ev != null))
                    {
                        carried.AddRange(batch.Skip(cut));
                        batch.RemoveRange(cut, batch.Count - cut);
                    }
                }
                // A destroyed card leaves the field a step before its Destroyed event: its effect plays where it stood.
                destroying.Clear();
                foreach (var s in batch) if (s.Ev is { Kind: EvKind.Destroyed, Card: { } dc }) destroying[dc.Uid] = (s.Ev.Battle, s.Ev.Other);
                offered.Clear();
                for (int i = 0; i < batch.Count; i++)
                    if (batch[i].Ev is { Card: { } oc } oe && (oe.Kind == EvKind.LeftField || oe.Kind == EvKind.ToGrave && oe.From == Loc.Hand) && !destroying.ContainsKey(oc.Uid) && !offered.ContainsKey(oc.Uid))
                        for (int j = i + 1; j < batch.Count; j++)
                            if (batch[j].Ev is { Kind: EvKind.Summoned, Card: { } sum } se)
                            {
                                if ((SummonKind)se.Arg is SummonKind.Tribute or SummonKind.Fusion) offered[oc.Uid] = ((SummonKind)se.Arg, sum.Uid, batch[j].Cards[sum.Uid]);
                                break;
                            }
                // Random discards are pulled out by the card that caused them; a hand thrown away by one card burns at once.
                foreach (var d in batch.Where(x => x.Ev is { Kind: EvKind.Discarded, Arg: 1, Other: not null }))
                    foreach (var x in batch.Where(x => x.Ev is { Kind: EvKind.ToGrave, From: Loc.Hand } && d.Ev!.Cards!.Contains(x.Ev.Card!))) x.Thief = d.Ev!.Other;
                for (int i = 0; i + 1 < batch.Count; i++)
                    if (batch[i].Ev is { Kind: EvKind.ToGrave, From: Loc.Hand } a && batch[i + 1].Ev is { Kind: EvKind.ToGrave, From: Loc.Hand } b && a.Other == b.Other && batch[i].Thief == null)
                        batch[i].Together = true;
                // Several cards answering one event (a Standby Phase with a Graveyard full of them): one chain, numbered.
                foreach (var g in batch.Where(x => x.Ev?.Kind == EvKind.Triggered && x.Cause != null).GroupBy(x => x.Cause).Where(g => g.Count() > 1))
                {
                    var l = g.ToList();
                    l[0].Group = l.Select(x => x.Ev!.Card!).ToList();
                    foreach (var x in l) x.InGroup = true;
                }
                foreach (var s in batch) { if (surrendered) throw new OperationCanceledException(); await Play(s); }
                if (D.Over) break;
                var p = D.Pending!;
                if (p.Source != null && p.Kind is Ask.Pick or Ask.YesNo or Ask.Choose) await EffectFlash(p.Source, Side(p.Player));
                Command cmd;
                // Response windows the field doesn't think worth asking about pass for you (Worth).
                if (Watch != null) cmd = await Watched(p);
                else if (Mine(p.Player) && liveMove != null && p.Options.FindIndex(o => liveMove(o)) is >= 0 and var lm) { cmd = new Command(Me, new[] { lm }); liveMove = null; }
                else if (Mine(p.Player) && !autoplay && p.Kind == Ask.Respond && !Worth(p))
                    cmd = new Command(p.Player, new[] { p.Options.FindIndex(o => o.Act == Act.Pass) });
                else if (Mine(p.Player) && !autoplay) cmd = await Human(p);   // the sandbox's ME mode answers for the rival too
                else
                {
                    // A beat before each rival move so you can follow what it does.
                    if (p.Player != Me && p.Kind is Ask.Main or Ask.Battle) await Wait(0.8);
                    // Seer can think for a second or so: off the main thread so the field keeps animating (it only reads D).
                    thinking = true;
                    try { cmd = p.Player != Me ? (Lan.On ? await Remote() : await Task.Run(() => cpu.Decide(D))) : Bot(p); }
                    finally { thinking = false; }
                    if (p.Player == Me && p.Kind is Ask.Respond or Ask.YesNo or Ask.Choose or Ask.Pick) await AskYou(p, cmd);
                }
                if (surrendered) throw new OperationCanceledException();
                if (lanOver) return;
                if (Lan.On && p.Player == Me) Lan.Link!.Send(new LanLink.Msg { T = "cmd", P = cmd.Player, Picks = cmd.Picks });
                if (Lan.On && p.Player != Me)
                {
                    try { D.Submit(cmd); }
                    catch (ArgumentException) { throw new LanEnded("OUT OF SYNC", true); }   // their answer doesn't fit our prompt
                }
                else D.Submit(cmd);
                if (Lan.On) SendHash();
                steps.Add(Snap(null));
            }
            int w = Side(D.Winner);
            if (selftest) GD.Print($"selftest: duel over, winner {w} ({D.EndReason}), turn {D.Turn}, {D.Log.Count} commands");
            if (Lan.On) lanOver = true;   // a hang-up after the last command no longer matters
            // LP down to 0: the loser's field shatters before the banner.
            if (w is 0 or 1 && D.P(D.Winner ^ 1).Lp <= 0) await FieldShatters(1 - w);
            if (D.EndReason == "Exodia") await ExodiaFinish(w);
            else if (IsAltWin(D.EndReason)) await AltWinFinish(w, D.EndReason, D.Winner);   // Destiny Board, Exodius, deck out... (DuelField.AltWin.cs)
            if (w == 0) Cue(D.P(D.Winner ^ 1).Lp <= 0 ? "banner_win" : "banner_special_win"); else Sfx("SE_DUEL_LOSE");   // special: Exodia, deck out...
            // VICTORY slams in / DEFEAT crashes down; the subtitle names a special end (Exodia, deck out...).
            string them = ThemName.ToUpperInvariant(), why = D.EndReason == "LP reached 0" ? "" : D.EndReason.ToUpperInvariant();
            await EndTitle(w == 0 ? "VICTORY" : w == 1 ? "DEFEAT" : "DRAW", w == 1 ? Bad : RivalPanels.Gold, w != 1,
                why.Length > 0 ? why : w == 0 ? $"{them} DEFEATED" : w == 1 ? $"{them} WINS" : "");
            await Wait(2);
            if (selftest || backToLab) { GetTree().ReloadCurrentScene(); return; }
            if (Watch != null) { LeaveReplay(); return; }
            KeepReplay(w, D.EndReason);
            // LAN: no rewards (two PCs would farm them); back to the lobby for a rematch.
            if (Lan.On) { if (autoplay) GD.Print($"lan: duel over, seat {Me}, winner {w} ({D.EndReason}), turn {D.Turn}, {D.Log.Count} commands, hash {D.Hash():x}"); Lan.LastWinner = w; Transition.Go(this, "res://ui/LanLobby.tscn"); return; }
            // Mid-match: side-decking before the next duel; results only once the match (or single duel) is over.
            Shop.Observe(Session.Profile, D);   // the rival's revealed cards go on sale
            Session.LastLp = D.P(Me).Lp;
            bool more = Session.FinishDuel(w, D.EndReason);
            if (more) Session.RivalSides(D);
            Transition.Go(this, more ? "res://ui/SideDeck.tscn" : "res://ui/Results.tscn");   // a rival's deck won: Results shows its reveal after the rewards
        }
        catch (Exception) when (surrendered) { await EndBySurrender(); }
        catch (LanEnded x) { LanOver(x.Message, x.Desync); }
        catch (ReplayEnded) { await EndOfReplay(); }
        catch (Exception e) { GD.PrintErr(e.ToString()); }
    }

    async Task Wait(double s) => await ToSignal(GetTree().CreateTimer(s), SceneTreeTimer.SignalName.Timeout);

    // Duel speed scales the whole clock (waits, tweens, timers alike), so faster speeds never overlap animations.
    public override void _ExitTree() { Godot.Engine.TimeScale = 1; Godot.Engine.MaxFps = 0; base._ExitTree(); }

    readonly HashSet<View> groupBadged = new();   // numbered triggers still to apply (Flair, EvKind.Triggered)
    bool handsOpen;   // Ceremonial Bell face-up, Respect Play on the rival's turn, or the sandbox's ME mode: both hands shown

    int shownChaos = -1;
    bool puzzleShown;
    Label? chaosLine;

    void ChaosLine(string text)
    {
        chaosLine ??= Put(hud, "", Cond, 22, new Color(0.8f, 0.68f, 1f), new Vector2(0, 8), new Vector2(W, 30), HorizontalAlignment.Center, 6);
        chaosLine.Text = "CHAOS RULE:  " + text.ToUpperInvariant();
    }

    Step Snap(Ev? e)
    {
        handsOpen = hotseat || D.CountActive("Ceremonial Bell") > 0 || (D.CountActive("Respect Play") > 0 && D.TurnPlayer != Me);
        for (int p = 0; p < 2; p++) handShown[p] = HandShown(p);
        var s = new Step { Ev = e, Phase = D.Phase, Tp = Side(D.TurnPlayer), Turn = D.Turn, Chain = D.Chain.Count, Cards = new St[D.Cards.Count], Seq = D.Seq };
        s.NoDraw = e?.Kind == EvKind.DrawPhase && (D.TurnFlags.Contains($"skipdraw{D.TurnPlayer}") || D.Flags.GetValueOrDefault($"skipdraw{D.TurnPlayer}") > 0);
        s.ToEnd = D.TurnFlags.Contains("toend");
        s.Chaos = D.ChaosRule;
        s.Lp[0] = D.P(Me).Lp; s.Lp[1] = D.P(1 - Me).Lp;
        for (int p = 0; p < 2; p++) s.Blocked[Side(p)] = D.Flags.GetValueOrDefault($"blocked{p}");
        foreach (var c in D.Cards) s.Cards[c.Uid] = StateOf(c);
        return s;
    }

    St StateOf(Card c)
    {
        if (c.OnField)
        {
            bool mon = c.Loc == Loc.Monster;
            int ready = 0;
            if (mon && c.FaceUp && D.Phase == Phase.Battle && c.Controller == D.TurnPlayer)
            {
                ready = c.Attacks > 0 && !D.CanAttack(c) ? 2 : c.Defense ? 0 : 1;   // a second attack (double attackers) keeps the aura
                // While the engine waits for a battle choice it knows exactly who may attack.
                if (ready == 1 && D.Pending is { Kind: Ask.Battle } bp && !bp.Options.Any(o => o.Act == Act.Attack && o.Card == c)) ready = 0;
            }
            return new St(c.Loc, c.Loc == Loc.Field ? 0 : c.Zone, 0, c.FaceUp, c.Defense, Side(c.Controller), mon ? D.Atk(c) : 0, mon ? D.Def(c) : 0, ready, CounterOf(c),
                CountersOf(c), mon && c.FaceUp ? D.Level(c) : 0, (c.AsMonster ?? c.Data).Level);
        }
        foreach (var p in D.Players)
        {
            var l = c.Loc switch { Loc.Hand => p.Hand, Loc.Deck => p.Deck, Loc.Grave => p.Grave, Loc.Banished => p.Banished, Loc.Extra => p.Extra, _ => null };
            if (l == null) break;
            int i = l.IndexOf(c);
            if (i >= 0) return new St(c.Loc, i, l.Count, c.Loc is Loc.Grave or Loc.Banished || (c.Loc == Loc.Hand && (p.Index == Me || handsOpen || handShown[p.Index])), false, Side(p.Index), 0, 0);
        }
        return new St(Loc.None, 0, 0, false, false, Side(c.Owner), 0, 0);
    }

    // Cards that last a set number of turns show the turns left; cards that count up (Castle of Dark Illusions,
    // Stim-Pack, Germ Infection, Cocoon of Evolution...) show their count.
    static readonly Dictionary<string, int> Durations = new() { ["Swords of Revealing Light"] = 3, ["Nightmare's Steelcage"] = 2, ["Ekibyo Drakmord"] = 2 };
    static int CounterOf(Card c)
    {
        if (!c.FaceUp) return 0;
        if (c.Name == "Future Fusion") return c.Var("futurestart") > 0 ? Math.Max(0, 2 - c.Var("futurestandby")) : 0;   // its Standby Phases to go
        if (Durations.TryGetValue(c.Name, out var n)) return c.Var("stay") == 1 || c.EquippedTo != null ? Math.Max(0, n - c.Var("turns")) : 0;
        return c.Var("steps") + c.Var("turns");
    }

    async Task Play(Step s)
    {
        bool settle = LineDone(s);   // a raised hand stays up through its effect's steps (before shownTp/Phase move on)
        if (s.Tp != shownTp || s.Turn != shownTurn)
        {
            shownTp = s.Tp; shownTurn = s.Turn; shownPhase = s.Phase;
            ShowPhase(s.Phase, s.Tp, s.Turn);
            ZoneWave(s.Tp, FxGold, spells: true, cue: s.Tp == 0 ? "turn_wave" : "turn_wave_rival");   // the turn player's side wakes up
            await Wait(0.15);
            // The words ride in with the wave, lying on the turn player's half of the field.
            FieldTitle(s.Tp == 0 ? "YOUR TURN" : $"{ThemName.ToUpperInvariant()}'S TURN", s.Tp, s.Tp == 0 ? RivalPanels.Gold : Pm.Them, s.Tp == 0 ? 150 : 130);
            await Wait(1.2);
        }
        else if (s.Phase != shownPhase)
        {
            shownPhase = s.Phase;
            AimClear();
            if (s.Phase == Phase.Battle) Cue("phase_battle");
            else if (PhaseSounds.TryGetValue(s.Phase, out var ps)) Sfx(s.Tp == 0 ? $"SE_PHASE_{ps}" : $"SE_PHASE_OPP_{ps}");
            ShowPhase(s.Phase, s.Tp, s.Turn);
            if (s.Phase == Phase.Battle) ZoneWave(s.Tp, FxEmber, spells: false, 0.7, cue: "battle_wave");
            await Wait(0.35);
            if (s.Phase == Phase.End && s.ToEnd) { SkipWords("END PHASE", s.Tp); await Wait(0.9); }
        }
        if (s.NoDraw) { SkipWords("NO DRAW", s.Tp); await Wait(0.9); }
        if (Session.Puzzle is { } pz && !puzzleShown && scenario == null)
        {
            puzzleShown = true;
            ChaosLine($"{pz.Name}  ·  WIN THIS TURN");
            chaosLine!.Text = $"CHALLENGE:  {pz.Name}  ·  WIN THIS TURN  ·  {pz.Goal.ToUpperInvariant()}";
        }
        if (s.Chaos != shownChaos && s.Chaos >= 0)
        {
            // Chaos Duel: the rift announces the new rule, then it stays as a line at the top of the screen
            shownChaos = s.Chaos;
            var (cn, ct) = Duel.ChaosRules[s.Chaos];
            await Banner(cn, new Color(0.75f, 0.6f, 1f), "CHAOS RULE  ·  " + ct.ToUpperInvariant());
            ChaosLine(cn + "  ·  " + ct);
        }
        if (handLine != null && settle) await SettleHand(s);   // a revealed hand: its picked card leaves from the line, the rest go back
        // Cards whose leaving plays as an effect where they stood; the real card moves to the Graveyard unseen.
        View? veiled = null, discarded = null;
        double extra = 0;
        if (s.Ev?.Kind is EvKind.LeftField or EvKind.BattleEnd) AimClear();
        // The battle is over: a monster shattered early with its opponent but spared after all (Waboku, a shield) comes back.
        if (s.Ev?.Kind is EvKind.AfterDamage or EvKind.BattleEnd)
            foreach (var u in crackedEarly.Where(u => s.Cards[u].Loc == Loc.Monster).ToList())
            {
                crackedEarly.Remove(u);
                if (views.TryGetValue(u, out var sv)) { sv.Root.Visible = true; UpdateStat(sv, false); }
            }
        if (s.Ev is { Kind: EvKind.LeftField or EvKind.ToGrave, Card: { } lc } && views.TryGetValue(lc.Uid, out var lv))
        {
            // Destroyed comes after the card has left; a battle loss says so on the leave itself (a chain answering the move
            // to the Graveyard can push Destroyed batches later).
            if (s.Ev.Kind == EvKind.LeftField && s.Ev.Battle && !destroying.ContainsKey(lc.Uid)) destroying[lc.Uid] = (true, s.Ev.Other);
            if (s.Ev.Kind == EvKind.LeftField && destroying.TryGetValue(lc.Uid, out var how))
            {
                Vector3 From((bool Battle, Card? By) h, View v) => h.By != null && views.TryGetValue(h.By.Uid, out var bv) ? bv.Root.GlobalPosition : v.Root.GlobalPosition + Vector3.Back;
                if (crackedEarly.Remove(lc.Uid)) { }   // already shattered with the monster it fought: its move plays hidden
                else if (how.Battle)
                {
                    CrackBreak(lv, From(how, lv));
                    // Monsters that destroyed each other break together, not one after the other (a response window can
                    // put the second one batches later: it stays hidden until its own move).
                    int other = mutualPair.A == lc.Uid ? mutualPair.B : mutualPair.B == lc.Uid ? mutualPair.A : -1;
                    if (other >= 0 && views.TryGetValue(other, out var ov) && ov.S.Loc == Loc.Monster && crackedEarly.Add(other))
                        CrackBreak(ov, lv.Root.GlobalPosition);   // hidden until its own LeftField (HideFor skips crackedEarly)
                }
                else EffectBurn(lv);
                veiled = lv;
            }
            else if (offered.Remove(lc.Uid, out var o))
            {
                var to = Target(ViewOf(o.Uid), o.At).Origin;
                if (o.Kind == SummonKind.Fusion) SwirlOut(lv, to); else SoulsOut(lv, to);
                veiled = lv; extra = 0.9;
            }
            else if (s.Ev.Kind == EvKind.ToGrave && s.Ev.From == Loc.Hand && s.Thief is { } thief && views.TryGetValue(thief.Uid, out var tv) && tv.Root.Visible && tv.S.Loc is Loc.Monster or Loc.Spell or Loc.Field)
            {
                // Thief's pull: the effect's card glows, a stream reaches the card in the hand and yanks it out, face-up.
                GoldRays(tv);
                await Wait(0.5);
                SoulStream(tv.Root.GlobalPosition + Vector3.Up * 0.1f, lv.Root.GlobalPosition, 14, FxViolet, 0.6, 0.6f);
                await Wait(0.6);
                Fly(lv, new Transform3D(tv.Root.GlobalTransform.Basis, tv.Root.GlobalPosition + Vector3.Up * 0.35f), 0.45f, Tween.TransitionType.Back, Tween.EaseType.In);
                Cue("control_out");
                await Wait(0.5);
                Fly(lv, Stand(tv.Root.GlobalPosition + Vector3.Up * 0.6f, 0.9f, faceUp: true), 0.25f);
                FrameBurst(tv, FxViolet, 0.9f, 1.3f);
                await Wait(0.8);
                extra = 0.45;   // then it drops into the Graveyard (Sync)
            }
            else if (s.Ev.Kind == EvKind.ToGrave && s.Ev.From == Loc.Hand)
            {
                DiscardFx(lv, Target(lv, s.Cards[lc.Uid]).Origin);
                discarded = lv; extra = s.Together ? 0 : 0.6;
            }
        }
        // A monster changing sides: its frame flares where it stood before it arcs across.
        if (s.Ev is { Kind: EvKind.ControlChanged, Card: { } cc } && views.TryGetValue(cc.Uid, out var ccv)) { Cue("control_out"); FrameBurst(ccv, FxEmber, 0.95f, 1.2f); }
        int moved = Sync(s.Cards, s.Ev);
        if (veiled != null) HideFor(veiled, 1.1);
        // A discard rebuilds in the Graveyard once its embers arrive (the restore and the rebuild's own hide fall on the same frame).
        if (discarded != null) { var dv = discarded; HideFor(dv, 1.0); After(1.0, () => _ = Rebuild(dv, FxEmber, 0.5, "rebuild_gy")); }
        // Card moves: each card drawn a step higher (the stagger of Sync); a card going to the Graveyard carries the sound
        // of how it went (its destruction, discard, tribute); set cards and summons sound in Flair.
        if (moved > 0)
            switch (s.Ev?.Kind)
            {
                case EvKind.Drew: for (int i = 0; i < moved; i++) { int k = i; After(k * 0.12, () => Cue("draw", k)); } break;
                case EvKind.ToHand: Cue("to_hand"); break;
                case EvKind.Summoned or EvKind.Activated or EvKind.LeftField or EvKind.Set or EvKind.ToGrave or EvKind.Discarded
                    or EvKind.Flipped or EvKind.ControlChanged or EvKind.PositionChanged: break;
                default: Sfx("SE_CARD_MOVE_01"); break;
            }
        // Chain links resolving, then the chain closing.
        if (s.Chain < shownChain) for (int i = s.Chain; i < shownChain; i++) ChainResolve();
        shownChain = s.Chain;
        for (int side = 0; side < 2; side++)
            for (int z = 0; z < 5; z++)
            {
                var bm = blockMarks[side, z];
                bool on = (s.Blocked[side] & (1 << z)) != 0;
                if (on == bm.Visible) continue;
                bm.Visible = on;
                if (on) ZoneCollapse(bm);
                else ZoneLight(bm.GlobalPosition, FxGold);   // it opens again
            }
        for (int side = 0; side < 2; side++)
        {
            bool any = s.Cards.Any(c => c.Loc == Loc.Banished && c.Side == side);
            foreach (var m in banishMarks[side]) m.Visible = any;
        }
        for (int i = 0; i < 2; i++)
            if (s.Lp[i] != shownLp[i])
            {
                LpHit(i, shownLp[i], s.Lp[i]);
                shownLp[i] = s.Lp[i];
                if (s.Lp[i] <= 2000) Escalate(2);
            }
        if (s.Chain == 0 && groupBadged.Count == 0) { ClearBadges(); ChainClear(); }
        double wait = s.Ev == null ? 0 : Flair(s.Ev, s);
        await Wait(s.Together ? 0.05 : Math.Max(wait, moved > 0 ? 0.45 + (moved - 1) * 0.1 : 0.04) + extra);
    }

    string? lastReveal;

    double Flair(Ev e, Step s)
    {
        if (e.Kind != EvKind.Revealed) lastReveal = null;
        View? V(Card? c) => c != null && views.TryGetValue(c.Uid, out var v) ? v : null;
        switch (e.Kind)
        {
            case EvKind.Summoned:
                {
                    var kind = (SummonKind)e.Arg;
                    bool def = s.Cards[e.Card!.Uid].Def;
                    if (V(e.Card) is { } sv)
                    {
                        // The landing (fusion: rebuilt out of the swirl; tribute: the souls' impact; special: rebuilt from embers;
                        // ritual: circle and pillars, then the drop), then the light swords or shield.
                        switch (kind)
                        {
                            case SummonKind.Fusion:
                                HideFor(sv, 0.6); After(0.5, () => { _ = Rebuild(sv, FxWarm, cue: "rebuild_fusion"); After(0.8, () => SummonImpact(sv, FxWarm, FxGold, "impact_fusion")); });
                                After(1.5, () => PlacementFx(sv, def));
                                break;
                            case SummonKind.Special:
                                _ = Rebuild(sv, FxViolet.Lerp(Colors.White, 0.3f), cue: "rebuild_special");
                                After(0.75, () => SoftLand(sv, FxViolet, "land_special")); After(0.95, () => PlacementFx(sv, def));
                                break;
                            case SummonKind.Ritual:
                                RitualCircle(Target(sv, sv.S).Origin);
                                HideFor(sv, 0.7); After(0.7, () => Drop(sv, sv.S));
                                After(1.15, () => SummonImpact(sv, FxIce, FxIce, "impact_ritual")); After(1.4, () => PlacementFx(sv, def));
                                break;
                            case SummonKind.Tribute:
                                After(0.45, () => SummonImpact(sv, FxWarm, FxGold, "impact_tribute")); After(0.7, () => PlacementFx(sv, def));
                                break;
                            default:   // Normal, Flip (it pops up and turns over)
                                After(0.45, () => SoftLand(sv, def ? FxIce : FxGold)); After(0.7, () => PlacementFx(sv, def));
                                break;
                        }
                    }
                    if (kind is SummonKind.Fusion or SummonKind.Ritual || s.Cards[e.Card.Uid].Atk >= 2500) Escalate(1);
                    Focus(e.Card);
                    return kind switch { SummonKind.Fusion => 2.1, SummonKind.Ritual => 2.2, SummonKind.Special => 1.8, _ => 1.55 };
                }
            case EvKind.Flipped:
                Cue("flip");
                Focus(e.Card);   // it pops up and turns over (Sync); a Flip Summon lands with its swords next (Summoned)
                return 0.6;
            case EvKind.Set:
                if (V(e.Card) is not { } setv) return 0.4;
                if (e.Card!.Data.IsMonster) { After(0.45, () => SoftLand(setv, FxIce)); After(0.7, () => PlacementFx(setv, true)); return 1.4; }
                Cue("card_slide"); After(0.45, () => { Cue("card_place"); ZoneLight(Target(setv, setv.S).Origin, FxIce); });   // a Spell/Trap: its zone lights up
                return 0.9;
            case EvKind.PositionChanged:
                if (V(e.Card) is { S: { Loc: Loc.Monster, Up: true } } pv) { Cue("turn"); After(0.4, () => PlacementFx(pv, pv.S.Def)); return 1.0; }
                return 0;
            case EvKind.ControlChanged:
                if (V(e.Card) is { } cv) After(0.6, () => { Cue("control_in"); FrameBurst(cv, FxGold, 0.9f, 1.3f); });
                return 1.0;
            case EvKind.AttackDeclared:
                {
                    var a = V(e.Card); var t = V(e.Other); int side = Side(e.Card!.Controller);
                    if (a != null) AimShow(a, t != null ? Target(t, t.S).Origin : DirectPoint(side));
                    Focus(e.Card);
                    return 0.7;
                }
            case EvKind.DamageCalc when e.Card is { } atkCard:
                {
                    var a = V(atkCard); var t = V(e.Other); int side = Side(atkCard.Controller);
                    int atk = s.Cards[atkCard.Uid].Atk;
                    // What a face-up defender shows: DEF higher = blocked (hit-stop and the light shield); DEF equal = both
                    // survive (locked blades); equal ATK = both destroyed (they meet in the middle).
                    var ds = e.Other != null ? s.Cards[e.Other.Uid] : default;
                    bool shown = t != null && ds is { Loc: Loc.Monster, Up: true };
                    bool blocked = shown && ds.Def && ds.Df > atk, locked = shown && ds.Def && ds.Df == atk, mutual = shown && !ds.Def && ds.Atk == atk && atk > 0;
                    if (blocked || locked) blockShown.Add(atkCard.Uid);
                    mutualPair = mutual ? (atkCard.Uid, e.Other!.Uid) : (-1, -1);
                    crackedEarly.Clear();
                    After(0.46, AimClear);
                    if (blocked) Lunge(a, t, side, onHit: h => BlockHit(t!, h), repel: true);
                    else if (locked) Lunge(a, t, side, grind: true);
                    else if (mutual && a != null) MeetInMiddle(a, t!, side);
                    else if (t == null) Lunge(a, null, side, onHit: _ => DirectHit(side));
                    else Lunge(a, t, side);
                    return locked ? 1.5 : 1.0;
                }
            case EvKind.AfterCalc when e.Card is { } ca && e.Other is { } ct:
                {
                    // Damage-Step responses (Rush Recklessly...) can change who loses after the clash was shown: the real pair.
                    St x = s.Cards[ca.Uid], y = s.Cards[ct.Uid];
                    bool both = x.Loc == Loc.Monster && y is { Loc: Loc.Monster, Up: true, Def: false } && x.Atk == y.Atk && x.Atk > 0;
                    mutualPair = both ? (ca.Uid, ct.Uid) : (-1, -1);
                    return 0;
                }
            case EvKind.Destroyed:
                return e.Battle ? 0.55 : 0.6;   // the shatter / burn started when the card left the field (Play)
            case EvKind.Negated:
                if (V(e.Card) is { } nv) NegateGlitch(nv);
                return 1.3;
            case EvKind.BattleStart when e.Arg > 0:
                {
                    // A second Battle Phase (Weather Report): the phase doesn't change, so say it and wake the attackers.
                    int side = Side(e.Player);
                    Cue("phase_battle");
                    FieldTitle($"BATTLE PHASE {e.Arg + 1}", side, FxEmber, 110);
                    ZoneWave(side, FxEmber, spells: false, 0.7, cue: "battle_wave");
                    return 1.4;
                }
            case EvKind.Triggered when s.Group != null:
                {
                    // All of them glow at once with their numbers; each number goes as that card's effect applies.
                    // Graveyard cards fan out above the pile so each number can be seen, then settle back.
                    int k = 0, fan = 0;
                    foreach (var gc in s.Group)
                    {
                        if (V(gc) is not { } gv) continue;
                        if (gv.S.Loc == Loc.Grave)
                        {
                            var home = Target(gv, gv.S); var st = gv.S;
                            var to = home.Origin + new Vector3(-Mathf.Sign(home.Origin.X) * 0.55f * ++fan, 0.25f + fan * 0.01f, 0);
                            gv.Root.Visible = true; gv.Tw?.Kill();
                            gv.Tw = CreateTween();
                            gv.Tw.TweenProperty(gv.Root, "position", to, 0.3).SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
                            After(1.5, () => { if (gv.S == st) Move(gv, st, 0, 0.3f, 0.1f); });
                        }
                        GoldRays(gv); Badge(gv, ++k); groupBadged.Add(gv);
                    }
                    Focus(e.Card);
                    After(1.4, () => { if (V(e.Card) is { } hv) { hv.Badge.Visible = false; groupBadged.Remove(hv); } });   // the first one applies now
                    return 1.4;
                }
            case EvKind.Triggered when s.InGroup:
                if (V(e.Card) is { } gm) { FrameBurst(gm, FxGold, 0.98f, 1.12f, 0.45, alpha: 0.7f); gm.Badge.Visible = false; groupBadged.Remove(gm); }
                return 0.35;
            case EvKind.Triggered:
                if (e.Card!.Uid == lastRays && Time.GetTicksMsec() - lastRaysAt < 4000) return 0;
                if (V(e.Card) is { } tv) GoldRays(tv);
                Focus(e.Card);
                return Math.Max(1.1, ShowActivation(V(e.Card), e.Card));
            case EvKind.AfterDamage when e.Other is { } def && e.Card is { } atk:
                {
                    // The defender held. A face-down one only shows it now (it was flipped during the attack): its shield.
                    AimClear();
                    var d = s.Cards[def.Uid]; var a = s.Cards[atk.Uid];
                    if (blockShown.Remove(atk.Uid) || !(d.Loc == Loc.Monster && d.Def && a.Loc == Loc.Monster && a.Atk <= d.Df) || V(def) is not { } dv) return 0;
                    ShieldPop(dv); FrameBurst(dv, FxIce, 1f, 1.3f, 0.45);
                    return 0.6;
                }
            case EvKind.Damage or EvKind.Gain:
                {
                    bool you = e.Player == Me;
                    if (e.Kind == EvKind.Gain) LpGlints(Side(e.Player));
                    else if (you) Vignette(FxRed, 0.8);   // your LP going down: the screen's edges pulse red
                }
                return 0.75;
            case EvKind.Activated:
                if (CardSounds.TryGetValue(e.Card!.Name, out var cs)) Sfx(cs);   // Master Duel's own sound for this card
                if (!e.Card.Data.IsMonster) Cue("flip_st");
                // Glow: Spells green, Traps pink, monster effects gold.
                if (V(e.Card) is { } av) { GoldRays(av, e.Card.Data.IsSpell ? FxGreen : e.Card.Data.IsTrap ? FxPink : null); if (s.Chain > 0) ChainLinkFx(av); }
                if (s.Chain > 0) Badge(V(e.Card), s.Chain);
                Focus(e.Card);
                return ShowActivation(V(e.Card), e.Card);   // it glows in place
            case EvKind.Coin:
                Coins(e);
                return 2.9;
            case EvKind.Die:
                return DiceCup(e);
            case EvKind.Discarded:
                return 0.3;   // no words: the cards burnt away when they left the hand (Play)
            case EvKind.Revealed when e.Cards is { Count: > 0 } shown:
                {
                    // Revealed names who sees the cards; effects shown to both players raise it twice in a row.
                    var key = string.Join(",", shown.Select(c => c.Uid));
                    if (key == lastReveal) return 0;
                    lastReveal = key;
                    bool mine = Side(shown[0].Owner) == 0;
                    string title = $"{(mine ? "YOUR" : ThemName.ToUpperInvariant() + "'S")} CARDS  ·  REVEALED";
                    var vs = shown.Select(V).OfType<View>().ToList();
                    if (vs.Count == shown.Count && vs.All(v => v.S.Loc == Loc.Hand && v.S.Side == 1 && v.S.Up) && handShown[1 - Me]) return 0;   // already kept face-up (Mind on Air)
                    // A whole hand rises in a line (and stays for a pick from it); field cards flip up and lift where they are.
                    if (vs.Count == shown.Count && vs.All(v => v.S.Loc == Loc.Hand && v.S.Side == vs[0].S.Side)) { RaiseHand(vs, vs[0].S.Side, title); return 1.8; }
                    return LiftReveal(vs, title, mine ? Pm.You : Pm.Them);
                }
            case EvKind.Equipped:
                if (V(e.Card) is { } ev && V(e.Other) is { } mv) { EquipThread(ev, mv); return 1.2; }
                return 0.45;
        }
        return 0;
    }

    // ---------------------------------------------------------------- sound

    readonly List<AudioStreamPlayer> sfx = new();
    int sfxNext;

    /// A Master Duel sound (`SE_...`, `assets/audio/md/se/`) or an original PoC one (`assets/audio/se/<name>.wav`).
    void Sfx(string name) => PlayAudio(SfxPath(name));
    static string SfxPath(string name) => name.StartsWith("SE_") ? $"res://assets/audio/md/se/{name}.wav" : name.StartsWith("G_") || name.StartsWith("C_") ? $"res://assets/audio/gen/{name}.wav" : $"res://assets/audio/se/{name}.wav";
    // On the field the menu clicks are the duel's own select / decide sounds.
    void UiSound(string name) => Sfx(name == "cursor" ? "SE_DUEL_SELECT" : "SE_DUEL_DECIDE");

    static readonly Dictionary<Phase, string> PhaseSounds = new()
    {
        [Phase.Draw] = "DRAW", [Phase.Standby] = "STANDBY", [Phase.Main1] = "MAIN1", [Phase.Battle] = "BATTLE", [Phase.Main2] = "MAIN2", [Phase.End] = "END",
    };

    /// The sound of a card arriving on the field, by how it came and how big it is.

    /// Card-specific activation sounds Master Duel has for cards in this game.
    static readonly Dictionary<string, string> CardSounds = new()
    {
        ["Dark Hole"] = "SE_EV_BLACKHOLE", ["Mystical Space Typhoon"] = "SE_EV_CYCLONE", ["Monster Reborn"] = "SE_EV_MONSTER_REBORN",
    };

    /// A small pool of players so effects can overlap.
    void PlayAudio(string path, float pitch = 1, float vol = 1)
    {
        if (Shooting) return;
        if (sfx.Count == 0)
            for (int i = 0; i < 12; i++) { var a = new AudioStreamPlayer { VolumeDb = -4, Bus = "Sound" }; AddChild(a); sfx.Add(a); }
        var p = sfx[sfxNext++ % sfx.Count];
        p.Stream = GD.Load<AudioStream>(path);
        p.PitchScale = pitch;
        p.VolumeDb = -4 + Mathf.LinearToDb(vol);
        p.Play();
    }

    /// You're asked to chain when the rival does something (summons, attacks, activates), in the damage step
    /// (last-moment boosts, Kuriboh), or when one of your cards is waiting for exactly this moment.
    /// Other windows (phase changes, draws...) pass quietly. The card lab sweep applies the same rule.
    bool Worth(Prompt p) =>
        D.Window is { Kind: EvKind.Summoned or EvKind.AttackDeclared or EvKind.Activated } win && win.Player != p.Player
        || D.Window?.Kind == EvKind.DamageCalc
        || p.Options.Any(o => o.Card != null && D.WantsThisWindow(o.Card));

    // ---------------------------------------------------------------- demo bot

    /// Demo bot for your side under `--autoplay` / `--turn` fast-forward: summons its strongest monster, plays some spells, attacks when it wins.
    Command Bot(Prompt p)
    {
        var o = p.Options;
        List<int> Opts(Func<Opt, bool> f) => Enumerable.Range(0, o.Count).Where(i => f(o[i])).ToList();
        int Any(List<int> l) => l.Count == 0 ? -1 : l[bot.Int(l.Count)];
        int pick = -1;
        switch (p.Kind)
        {
            case Ask.Main:
                if (actionsTurn != D.Turn) { actionsTurn = D.Turn; actions = 0; }
                if (actions++ < 5)
                {
                    var sum = Opts(x => x.Act is Act.Summon or Act.Special).OrderByDescending(i => o[i].Card!.Data.Atk).ToList();
                    var act = Opts(x => x.Act == Act.Activate);
                    pick = act.Count > 0 && bot.Int(2) == 0 ? Any(act)
                        : sum.Count > 0 ? sum[0]
                        : bot.Int(3) != 0 ? Any(Opts(x => x.Act is Act.Activate or Act.SetSpell or Act.FlipSummon or Act.Set)) : -1;
                }
                if (pick < 0) pick = Any(Opts(x => x.Act == Act.ToBattle));
                if (pick < 0) pick = Any(Opts(x => x.Act == Act.EndTurn));
                break;
            case Ask.Battle:
                int Guard(Card t) => !t.FaceUp ? 1500 : t.Defense ? D.Def(t) : D.Atk(t);
                pick = Any(Opts(x => x.Act == Act.Attack && (x.Target == null || D.Atk(x.Card!) > Guard(x.Target))));
                if (pick < 0) pick = Any(Opts(x => x.Act == Act.ToMain2));
                if (pick < 0) pick = Any(Opts(x => x.Act == Act.Attack));   // a monster must attack
                break;
            case Ask.Respond:
                pick = bot.Int(2) == 0 ? bot.Int(o.Count - 1) : o.Count - 1;
                break;
            case Ask.YesNo:
                pick = 0;
                break;
            case Ask.Pick:
                {
                    int n = Math.Max(p.Min, Math.Min(1, p.Max));
                    return new Command(p.Player, Enumerable.Range(0, o.Count).OrderBy(_ => bot.Next()).Take(n).ToArray());
                }
        }
        return new Command(p.Player, new[] { pick < 0 ? bot.Int(o.Count) : pick });
    }

    // ---------------------------------------------------------------- shaders

    const string SkyShader = @"shader_type sky;
uniform vec4 top : source_color;
uniform vec4 low : source_color;
uniform vec4 accent : source_color;
uniform float detail = 1.0;
float h(vec2 p) { vec3 q = fract(vec3(p.xyx) * 0.1031); q += dot(q, q.yzx + 33.33); return fract((q.x + q.y) * q.z); }
float n(vec2 p) { vec2 i = floor(p), f = fract(p); f = f * f * (3.0 - 2.0 * f);
  return mix(mix(h(i), h(i + vec2(1, 0)), f.x), mix(h(i + vec2(0, 1)), h(i + vec2(1, 1)), f.x), f.y); }
float fbm(vec2 p) { float v = 0.0, a = 0.5; for (int i = 0; i < 5; i++) { v += a * n(p); p = p * 2.03 + 7.1; a *= 0.5; } return v; }
void sky() {
  float y = EYEDIR.y;
  vec3 c = mix(low.rgb, top.rgb, smoothstep(-0.3, 0.7, y));
  c += accent.rgb * 0.12 * exp(-abs(y - 0.05) * 6.0);
  if (detail > 0.5) {
    vec2 uv = vec2(atan(EYEDIR.x, EYEDIR.z) * 2.0, y * 5.0);
    float w = fbm(uv * vec2(1.0, 2.6) + vec2(TIME * 0.006, 0.0));
    float wisps = smoothstep(0.45, 0.8, w) * smoothstep(-0.2, 0.25, y);   // long drifting streaks of cloud
    c = mix(c, accent.rgb * 0.55 + top.rgb, wisps * 0.45);
    c += accent.rgb * pow(max(w - 0.55, 0.0) * 2.5, 2.0) * 0.5;
    vec2 g = uv * 60.0; vec2 id = floor(g); vec2 fp = fract(g) - 0.5;
    float star = step(0.985, h(id)) * (1.0 - smoothstep(0.0, 0.12, length(fp))) * smoothstep(0.0, 0.3, y);
    c += vec3(star) * (0.6 + 0.4 * sin(TIME * 1.7 + h(id + 3.0) * 40.0));
  }
  COLOR = c;
}";

    const string FloorShader = @"shader_type spatial;
uniform vec4 tint : source_color;
uniform vec4 accent : source_color;
void fragment() {
  vec2 p = (UV - 0.5) * 80.0;
  float r = length(p * vec2(0.6, 1.0));
  // continuous rings only (a square grid read as blocks around the table)
  float ring = exp(-abs(r - 6.0 - sin(TIME * 0.4) * 0.2) * 3.0) * 0.5;
  float ripples = (0.5 + 0.5 * sin(r * 2.2 - TIME * 0.35)) * exp(-r * 0.16);
  ALBEDO = tint.rgb * 0.18;
  EMISSION = accent.rgb * (ripples * 0.05 + ring * 0.25) * smoothstep(3.0, 6.0, r);
  ROUGHNESS = 0.3;
  METALLIC = 0.5;
}";

    /// The world far below a forged mat's table (see Surrounds for the kinds).
    const string SurroundShader = @"shader_type spatial;
render_mode unshaded;
uniform int kind = 0;
uniform vec4 deep : source_color;
uniform vec4 glow : source_color;
uniform int kind2 = 0;          // the rival's world, on their side, turned round like their mat
uniform vec4 deep2 : source_color;
uniform vec4 glow2 : source_color;
uniform float split = 0.0;      // 1: your world and the rival's meet at the rift under the centre line
uniform int seam = 2;           // the rift: 0 braided clash, 1 thin calm line, 2 soft dark gap (picked), 3 straight edge
float h(vec2 p) { vec3 q = fract(vec3(p.xyx) * 0.1031); q += dot(q, q.yzx + 33.33); return fract((q.x + q.y) * q.z); }  // sin-free: sin() hashes break into blocks on Intel GPUs
float n(vec2 p) { vec2 i = floor(p), f = fract(p); f = f * f * (3.0 - 2.0 * f);
  return mix(mix(h(i), h(i + vec2(1, 0)), f.x), mix(h(i + vec2(0, 1)), h(i + vec2(1, 1)), f.x), f.y); }
float fbm(vec2 p) { float v = 0.0, a = 0.5; for (int i = 0; i < 4; i++) { v += a * n(p); p = mat2(vec2(1.6, 1.2), vec2(-1.2, 1.6)) * p; a *= 0.5; } return v; }
vec3 world(int k, vec2 p, vec3 dc, vec3 gc, float t) {
  if (k == 0) {          // lava sea: dark crust, glowing cracks, molten pools breathing
    vec2 q = p * 0.3 + vec2(t * 0.03, t * 0.02);
    float f = fbm(q + fbm(q * 0.7 + t * 0.05) * 1.5);
    float crack = 1.0 - smoothstep(0.0, 0.025, abs(f - 0.5));
    float molten = smoothstep(0.55, 0.8, fbm(p * 0.15 - t * 0.04));
    return dc * 0.5 * f + gc * (crack * 1.4 + molten * 0.35) * (0.75 + 0.25 * sin(t * 1.3 + f * 9.0));
  } else if (k == 1) {   // dc water: caustic web drifting
    vec2 q = p * 0.5;
    float a = fbm(q + vec2(t * 0.15, 0.0)), b = fbm(q * 1.3 - vec2(0.0, t * 0.12) + 3.0);
    return dc * (0.6 + 0.6 * a) + gc * pow(1.0 - abs(a - b), 14.0) * 0.8;
  } else if (k == 2) {   // a sea of clouds, lightning inside now and then
    vec2 q = p * 0.1 + vec2(t * 0.02, 0.0);
    float f = fbm(q + fbm(q + t * 0.01));
    float flash = pow(max(0.0, sin(t * 0.9) * sin(t * 2.3 + 1.0)), 18.0);
    return mix(dc * 0.5, dc * 1.8 + gc * 0.15, smoothstep(0.3, 0.8, f)) + gc * flash * f * 1.6;
  } else if (k == 3) {   // great stone tiles, light running along the seams
    vec2 g = p * 0.5; vec2 id = floor(g); vec2 fp = fract(g) - 0.5;
    float tile = 1.0 - smoothstep(0.45, 0.48, max(abs(fp.x), abs(fp.y)));
    float run = exp(-abs(fract(length(id) * 0.08 - t * 0.12) - 0.5) * 12.0);
    return dc * (0.6 + 0.8 * fbm(p * 0.6)) * (0.7 + 0.6 * h(id)) * tile + gc * (1.0 - tile) * (0.1 + 0.8 * run);
  } else if (k == 4) {   // circuitry: traces with pulses travelling along them
    vec2 g = p * 0.6; vec2 id = floor(g); vec2 fp = fract(g);
    float r = h(id);
    float line = r < 0.5 ? 1.0 - smoothstep(0.02, 0.045, abs(fp.y - 0.5)) : 1.0 - smoothstep(0.02, 0.045, abs(fp.x - 0.5));
    float along = r < 0.5 ? id.x + fp.x : id.y + fp.y;
    float pulse = pow(fract(along * 0.06 - t * 0.25 + r * 7.0), 24.0);
    float node = (1.0 - smoothstep(0.08, 0.13, length(fp - 0.5))) * step(0.8, h(id + 7.0));
    return dc * 0.6 + gc * (line * (0.05 + 1.2 * pulse) + node * 0.35);
  }
  // cosmos: a slow nebula swirl with twinkling stars
  float r = length(p), a = atan(p.y, p.x);
  float neb = fbm(vec2(a * 3.0 + r * 0.06 - t * 0.04, r * 0.08));
  vec2 g = p * 1.5; vec2 id = floor(g); float s = h(id);
  vec2 fp = fract(g) - 0.5 - (vec2(h(id + 1.0), h(id + 2.0)) - 0.5) * 0.6;
  float star = step(0.94, s) * (1.0 - smoothstep(0.0, 0.07, length(fp))) * (0.6 + 0.4 * sin(t * 2.0 + s * 50.0));
  return dc * 0.8 * neb + gc * pow(neb, 3.0) * 0.9 + vec3(star);
}
// A world's light re-dyed in another world's colour (keeps its pattern, takes the other's hue).
vec3 dye(vec3 c, vec3 gc) { return gc * dot(c, vec3(0.3, 0.59, 0.11)) * 1.6; }
void fragment() {
  vec2 p = (UV - 0.5) * 140.0;   // p.y > 0: your side
  float t = TIME;
  vec3 mine = world(kind, p, deep.rgb, glow.rgb, t);
  if (split < 0.5) { ALBEDO = mine; }
  else {
  vec3 theirs = world(kind2, -p, deep2.rgb, glow2.rgb, t);
  // The clash front: a wavering line under the centre line where the two worlds push into each other.
  // (2.5 units toward the rival: this plane lies far below, so that is what sits under the table's centre line on screen)
  float wave = seam == 0 ? 1.0 : seam == 3 ? 0.0 : 0.25;   // calm seams barely waver: a horizontal split, not a storm
  float d = p.y + 2.5 + ((fbm(vec2(p.x * 0.07, t * 0.06)) - 0.5) * 9.0 + sin(p.x * 0.13 + t * 0.35) * 1.2) * wave;
  if (seam != 0) {
    vec3 c2 = mix(theirs, mine, smoothstep(-0.6, 0.6, d));
    if (seam == 1) { float l = exp(-abs(d) * 2.2); c2 = c2 * (1.0 - 0.5 * exp(-abs(d) * 0.5)) + mix(glow2.rgb, glow.rgb, 0.5) * l * 0.55; }
    else if (seam == 2) { c2 *= 1.0 - 0.9 * exp(-abs(d) * 0.35); }
    ALBEDO = c2;
  } else {
  // Near the front each world catches the other's colour, so the styles bleed into each other instead of butting.
  float bleed = (1.0 - smoothstep(0.0, 9.0, abs(d))) * 0.55;
  mine = mix(mine, dye(mine, glow2.rgb), bleed);
  theirs = mix(theirs, dye(theirs, glow.rgb), bleed);
  vec3 c = mix(theirs, mine, smoothstep(-1.2, 1.2, d));
  // The rift itself: dark, with braided strands of both colours running along it.
  float rift = exp(-abs(d) * 0.55);
  c *= 1.0 - 0.75 * rift;
  float strands = 0.0;
  for (int i = 0; i < 4; i++) {
    float fi = float(i);
    float off = (fi - 1.5) * 0.7 + sin(p.x * 0.27 + t * (0.6 + fi * 0.25) + fi * 2.0) * 0.7;
    strands += exp(-abs(d - off) * 7.0) * (0.35 + 0.65 * n(vec2(p.x * 0.18 - t * (0.8 + fi * 0.3), fi * 5.0)));
  }
  float which = smoothstep(0.35, 0.65, n(vec2(p.x * 0.09 + t * 0.3, 9.0)));
  vec3 seam_c = mix(glow2.rgb, glow.rgb, which);
  c += seam_c * strands * 0.6 + mix(glow2.rgb, glow.rgb, smoothstep(-2.0, 2.0, d)) * rift * 0.15;
  ALBEDO = c;
  }
  }
}";

    /// Thin drifting haze between the dais and the depths, in each side's colour.
    const string HazeShader = @"shader_type spatial;
render_mode unshaded, blend_add, depth_draw_never, shadows_disabled;
uniform vec4 glow : source_color;
uniform vec4 glow2 : source_color;
float h(vec2 p) { vec3 q = fract(vec3(p.xyx) * 0.1031); q += dot(q, q.yzx + 33.33); return fract((q.x + q.y) * q.z); }  // sin-free: sin() hashes break into blocks on Intel GPUs
float n(vec2 p) { vec2 i = floor(p), f = fract(p); f = f * f * (3.0 - 2.0 * f);
  return mix(mix(h(i), h(i + vec2(1, 0)), f.x), mix(h(i + vec2(0, 1)), h(i + vec2(1, 1)), f.x), f.y); }
void fragment() {
  vec2 p = (UV - 0.5) * 140.0 * 0.12 + vec2(TIME * 0.03, TIME * 0.01);
  float m = n(p) * 0.6 + n(p * 2.1 + 5.0) * 0.4;
  ALBEDO = mix(glow2.rgb, glow.rgb, smoothstep(0.47, 0.53, UV.y + 0.03)) * smoothstep(0.5, 0.85, m) * 0.07;
}";

    /// Playmat: veined stone (or card art) per half, glowing gold border and centre line.
    const string MatShader = @"shader_type spatial;
uniform vec4 stone : source_color;
uniform vec4 col_you : source_color;
uniform vec4 col_them : source_color;
uniform sampler2D art_you : source_color, filter_linear_mipmap;
uniform sampler2D art_them : source_color, filter_linear_mipmap;
uniform float has_you = 0.0;
uniform float has_them = 0.0;
uniform float full = 0.0;
uniform sampler2D mat_tex : source_color, filter_linear_mipmap;
uniform sampler2D mat_them : source_color, filter_linear_mipmap;
uniform float has_mat = 0.0;
uniform float has_mat_them = 0.0;   // the rival's half has its own forged mat
uniform vec2 size = vec2(7.0, 4.8);
varying vec3 lp;
float h(vec2 p) { vec3 q = fract(vec3(p.xyx) * 0.1031); q += dot(q, q.yzx + 33.33); return fract((q.x + q.y) * q.z); }  // sin-free: sin() hashes break into blocks on Intel GPUs
float n(vec2 p) { vec2 i = floor(p), f = fract(p); f = f * f * (3.0 - 2.0 * f);
  return mix(mix(h(i), h(i + vec2(1, 0)), f.x), mix(h(i + vec2(0, 1)), h(i + vec2(1, 1)), f.x), f.y); }
float fbm(vec2 p) { float v = 0.0, a = 0.5; for (int i = 0; i < 5; i++) { v += a * n(p); p = mat2(vec2(1.6, 1.2), vec2(-1.2, 1.6)) * p; a *= 0.5; } return v; }
float rbox(vec2 p, vec2 b, float r) { vec2 q = abs(p) - b + r; return length(max(q, 0.0)) + min(max(q.x, q.y), 0.0) - r; }
void vertex() { lp = VERTEX; }
void fragment() {
  vec2 p = lp.xz;
  if (lp.y < 0.03) { ALBEDO = stone.rgb * 0.3; ROUGHNESS = 0.5; }
  else {
  bool you = p.y > 0.0;
  vec3 side = you ? col_you.rgb : col_them.rgb;
  float f = fbm(p * 1.4);
  float vein = 1.0 - smoothstep(0.0, 0.012, abs(fbm(p * 0.7 + 3.0) - 0.5));
  vec3 base = stone.rgb * (0.65 + 0.7 * f);
  // Card art: one image across the mat, or one per half (rival art turned to face them).
  vec2 uv = p / size + 0.5;
  vec2 auv = full > 0.5 ? vec2(uv.x, 0.12 + uv.y * 0.67) : (you ? vec2(uv.x, 0.18 + (uv.y - 0.5) * 2.0 * 0.48) : vec2(1.0 - uv.x, 0.18 + (0.5 - uv.y) * 2.0 * 0.48));
  vec3 art = you ? texture(art_you, auv).rgb : texture(art_them, auv).rgb;
  float has = you ? has_you : has_them;
  art = mix(vec3(dot(art, vec3(0.3, 0.59, 0.11))), art, 0.8);
  base = mix(base, art * (0.3 + 0.18 * f), has);
  // A forged mat covers the whole table (the rival's half is the same picture, seen from their side).
  float hm = you ? has_mat : has_mat_them;
  base = mix(base, (you ? texture(mat_tex, uv).rgb : texture(mat_them, uv).rgb) * 0.85, hm);   // forged mats are already dark under the zones
  has = max(has, hm);
  // Engraved seal under each half's monster row, turning slowly (art mats skip it).
  vec2 sp = p - vec2(0.0, you ? 1.05 : -1.05);
  float sr = length(sp);
  float ang = atan(sp.y, sp.x) + TIME * (you ? 0.05 : -0.05);
  float rings = exp(-abs(sr - 1.55) * 60.0) + exp(-abs(sr - 1.42) * 90.0) * 0.6 + exp(-abs(sr - 0.9) * 70.0) * 0.5;
  float ticks = step(0.92, fract(ang * 36.0 / 6.2831853)) * step(1.43, sr) * step(sr, 1.54);
  float star = exp(-abs(sr * cos(mod(ang, 1.0471976) - 0.5235988) - 0.78) * 50.0) * step(sr, 0.9);
  float seal = (rings + ticks * 0.8 + star * 0.5) * (1.0 - has);
  base += side * seal * 0.05;
  float e = rbox(p, size * 0.5 - 0.12, 0.35);
  base *= 0.6 + 0.4 * smoothstep(0.1, -1.4, e);
  float pulse = 0.6 + 0.4 * sin(TIME * 0.7 + p.x * 1.2 + p.y * 0.6);
  vec3 gold = vec3(1.0, 0.78, 0.4);
  float line = (1.0 - smoothstep(0.0, 0.02, abs(e + 0.05))) + 0.55 * (1.0 - smoothstep(0.0, 0.012, abs(e + 0.17)));
  float cl = (1.0 - smoothstep(0.0, 0.018, abs(p.y))) * step(abs(p.x), size.x * 0.5 - 0.3);
  float clGlow = exp(-abs(p.y) * 9.0) * 0.25;
  vec3 em = gold * line * 1.1 + side * seal * 0.22 * pulse + side * (vein * 0.07 * pulse * (1.0 - has * 0.6)) + mix(col_them.rgb, col_you.rgb, step(0.0, p.y)) * (cl * 1.6 + clGlow);
  ALBEDO = base;
  EMISSION = em;
  ROUGHNESS = 0.42 - 0.2 * vein;
  METALLIC = 0.25;
  }
}";

    /// Zone frame: rounded outline with soft glow and brighter corners.
    const string FrameShader = @"shader_type spatial;
render_mode unshaded, blend_add, depth_draw_never, cull_disabled, shadows_disabled;
uniform vec4 col : source_color;
uniform vec2 size;
uniform float strength = 1.0;
float rbox(vec2 p, vec2 b, float r) { vec2 q = abs(p) - b + r; return length(max(q, 0.0)) + min(max(q.x, q.y), 0.0) - r; }
void fragment() {
  vec2 p = (UV - 0.5) * size;
  float d = rbox(p, size * 0.5 - 0.04, 0.05);
  vec2 c = abs(p) - (size * 0.5 - 0.12);
  float corner = step(0.0, c.x) * step(0.0, c.y);
  float line = 1.0 - smoothstep(0.0, 0.01, abs(d));
  float glow = exp(-abs(d) * 40.0) * 0.18;
  float fill = step(d, 0.0) * 0.015;
  ALBEDO = col.rgb * (line * (0.9 + corner * 1.4) + glow + fill) * strength;
}";

    /// Light sweep across a card face (activation shine); t runs -1 (off) .. 0..1 (sweep).
    const string ShineShader = @"shader_type spatial;
render_mode unshaded, blend_add, depth_draw_never, cull_back, shadows_disabled;
uniform float t = -1.0;
void fragment() {
  float d = UV.x * 0.8 + UV.y * 0.6 - (t * 2.0 - 0.3);
  float band = exp(-d * d * 70.0) * step(0.0, t);
  vec2 e = min(UV, 1.0 - UV);
  float rim = exp(-min(e.x, e.y) * 40.0) * step(0.0, t) * (1.0 - t);
  ALBEDO = vec3(1.0, 0.95, 0.8) * (band * 1.6 + rim * 0.9);
}";

    /// Pulsing glow around a monster that can attack.
    const string AuraShader = @"shader_type spatial;
render_mode unshaded, blend_add, depth_draw_never, cull_disabled, shadows_disabled;
uniform vec4 col : source_color;
uniform vec2 size;
uniform vec2 card;
float rbox(vec2 p, vec2 b, float r) { vec2 q = abs(p) - b + r; return length(max(q, 0.0)) + min(max(q.x, q.y), 0.0) - r; }
void fragment() {
  vec2 p = (UV - 0.5) * size;
  float d = rbox(p, card * 0.5, 0.04);
  float pulse = 0.6 + 0.4 * sin(TIME * 4.0);
  float a = atan(p.y, p.x);
  float streak = 0.6 + 0.4 * sin(a * 12.0 - TIME * 3.0);
  // fade to zero before the quad edge so the additive glow never ends in a hard line
  float margin = min(size.x - card.x, size.y - card.y) * 0.5;
  float glow = exp(-max(d, 0.0) * 26.0) * step(0.0, d) * (1.0 - smoothstep(0.0, margin, d)) * pulse * streak;
  float edge = exp(-abs(d) * 70.0);
  ALBEDO = col.rgb * (glow * 1.1 + edge * 1.4);
}";

    /// Rune circle on the floor around the table, turning slowly.
    const string RuneShader = @"shader_type spatial;
render_mode unshaded, blend_add, depth_draw_never, shadows_disabled;
uniform vec4 you : source_color;
uniform vec4 them : source_color;
void fragment() {
  vec2 p = (UV - 0.5) * 15.0;
  float r = length(p);
  float a = atan(p.y, p.x);
  // full, unbroken loops; a slow pulse travels round them instead of dashed glyphs
  float rings = exp(-abs(r - 6.6) * 30.0) + exp(-abs(r - 6.2) * 40.0) * 0.6 + exp(-abs(r - 5.3) * 30.0) * 0.5;
  float travel = 0.75 + 0.25 * sin(a * 3.0 - TIME * 0.6);
  float band = exp(-abs(r - 6.4) * 9.0) * 0.25;
  vec3 c = mix(them.rgb, you.rgb, smoothstep(-2.0, 2.0, p.y));
  ALBEDO = c * (rings * travel + band) * 0.14 * smoothstep(4.2, 5.0, r) * (1.0 - smoothstep(7.2, 7.5, r));
}";

    const string RingShader = @"shader_type spatial;
render_mode unshaded, blend_add, depth_draw_never, cull_disabled, shadows_disabled;
uniform vec4 col : source_color;
uniform float fade = 1.0;
void fragment() {
  float r = length(UV - 0.5) * 2.0;
  float ring = exp(-abs(r - 0.85) * 22.0) + exp(-r * 4.0) * 0.4;
  ALBEDO = col.rgb * ring * fade * step(r, 1.0) * 2.0;
}";
}
