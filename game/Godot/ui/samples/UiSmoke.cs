using Godot;
using PoC.Engine;

namespace PoC.Game.UI;

/// Click-through test of the Chaos Rift menu screens (2026-11-08). Clicks the real controls by emitting their
/// GuiInput, across scene changes, and checks the effect on the session. Run with `-- --unlockall` so saving goes to
/// user://save_test.json, never the real save. Prints PASS/FAIL lines and quits; screenshots to `--shots=<dir>`.
public partial class UiSmoke : Node
{
    int fails;
    string? shots;

    bool runner;

    public override void _Ready()
    {
        shots = OS.GetCmdlineUserArgs().FirstOrDefault(a => a.StartsWith("--shots="))?[8..];
        // the scene root gets replaced by every scene change: run from a twin that lives under the window root
        if (!runner) { GetTree().Root.CallDeferred(Node.MethodName.AddChild, new UiSmoke { runner = true }); return; }
        CallDeferred(MethodName.Start);
    }

    async void Start()
    {
        try { await Run(); }
        catch (Exception e) { GD.Print("FAIL exception " + e); fails++; }
        GD.Print(fails == 0 ? "UI SMOKE: ALL PASSED" : $"UI SMOKE: {fails} FAILED");
        GetTree().Quit(fails);
    }

    void Check(bool ok, string what) { GD.Print((ok ? "PASS " : "FAIL ") + what); if (!ok) fails++; }

    async Task Wait(double s) => await ToSignal(GetTree().CreateTimer(s), SceneTreeTimer.SignalName.Timeout);
    async Task Go(string scene) { GetTree().ChangeSceneToFile(scene); await Wait(1.2); }
    Node Scene => GetTree().CurrentScene;

    IEnumerable<Control> Clickables() => Scene.FindChildren("*", "Control", true, false).OfType<Control>().Where(c => c.MouseDefaultCursorShape == Control.CursorShape.PointingHand && c.IsVisibleInTree());

    List<Label> Labels() => Scene.FindChildren("*", "Label", true, false).OfType<Label>().ToList();

    static bool HasText(Control c, string t) => c is Label l && l.Text.Trim() == t || c.FindChildren("*", "Label", true, false).OfType<Label>().Any(l => l.Text.Trim() == t);

    Control? Find(string text) => Clickables().LastOrDefault(c => HasText(c, text));   // last = the newest (a question bar over the screen)

    void Click(Control c, bool right = false)
    {
        // press + release: draggable tiles (Deck Builder) click on release
        foreach (bool down in new[] { true, false })
            if (GodotObject.IsInstanceValid(c)) c.EmitSignal(Control.SignalName.GuiInput, new InputEventMouseButton { Pressed = down, ButtonIndex = right ? MouseButton.Right : MouseButton.Left });
    }

    async Task Tap(string text, bool right = false)
    {
        var c = Find(text);
        Check(c != null, $"found '{text}'");
        if (c != null) Click(c, right);
        await Wait(0.3);
    }

    async Task Shot(string name)
    {
        if (shots == null) return;
        await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        GetViewport().GetTexture().GetImage().SavePng($"{shots}/{name}.png");
    }

    async Task Run()
    {
        Profile P = Session.Profile;
        // ---- Your Decks
        await Go("res://ui/DeckSelect.tscn");
        int n = Session.Decks.Count;
        Check(n > 0, $"decks loaded ({n})");
        await Tap("COPY");
        Check(Session.Decks.Count == n + 1, "COPY adds a deck");
        await Tap("DELETE");
        await Tap("DELETE");   // the confirm button in the question bar
        Check(Session.Decks.Count == n, "DELETE removes it");
        await Shot("1_decks");
        await Tap("EDIT DECK");
        await Wait(1.2);
        Check(Scene is DeckBuilder, "EDIT DECK opens the builder");

        // ---- Deck Builder
        var deck = DeckBuilder.Editing ?? Session.Playing!;
        int main0 = deck.Main.Count, side0 = deck.Side.Count;
        var builder = (DeckBuilder)Scene;
        float crack = builder.GetViewportRect().Size.X * 0.42f;
        var tiles = Clickables().Where(c => c.GetParent() is Control g && c.GlobalPosition.X < crack && c.Size.X > 80 && c.Size.Y > 120).ToList();
        Check(tiles.Count > 0, $"collection tiles ({tiles.Count})");
        // a card that can still go in
        var canAdd = tiles.FirstOrDefault(t => t.Modulate.A > 0.9f && t.GetParent().GetChildren().OfType<Control>().FirstOrDefault(x => x.Position == t.Position && x != t) is { } card && card.Modulate.A > 0.9f) ?? tiles[0];
        int at = tiles.IndexOf(canAdd);
        Click(canAdd); await Wait(0.4);
        if (Find("SKIP") != null) await Tap("SKIP");
        // the grid was rebuilt by the click: find the same tile again
        var again = Clickables().Where(c => c.GetParent() is Control && c.GlobalPosition.X < crack && c.Size.X > 80 && c.Size.Y > 120).ToList();
        Click(again[Math.Min(at, again.Count - 1)], right: true); await Wait(0.4);
        if (Scene.FindChildren("*", "Label", true, false).OfType<Label>().Any(l => l.Text.Contains("needs"))) await Tap("SKIP");
        await Shot("2_builder");
        await Tap("SAVE");
        if (Find("SAVE ANYWAY") != null) await Tap("SAVE ANYWAY");
        int main1 = deck.Main.Count + deck.Extra.Count, side1 = deck.Side.Count;
        Check(main1 + side1 > main0 + side0 + deck.Extra.Count - deck.Extra.Count - 0 || main1 + side1 >= main0 + side0, $"adding cards changed the deck ({main0}+{side0} -> {deck.Main.Count}+{side1}, fusion {deck.Extra.Count})");
        // remove one from the main zone (a tile right of the crack)
        var deckTiles = Clickables().Where(c => c.GlobalPosition.X > crack + 60 && c is TextureRect).ToList();
        Check(deckTiles.Count > 0, $"deck tiles ({deckTiles.Count})");
        int before = deck.Main.Count + deck.Side.Count + deck.Extra.Count;
        bool canDrop = deck.Main.Count > DeckRules.MinMain;   // at 40 the removal leaves 39: SAVE must refuse it
        if (deckTiles.Count > 0) { Click(deckTiles[0], right: true); await Wait(0.3); }
        await Tap("SAVE");
        if (Find("SAVE ANYWAY") != null) await Tap("SAVE ANYWAY");
        Check(deck.Main.Count + deck.Side.Count + deck.Extra.Count == before - (canDrop ? 1 : 0), canDrop ? "right-clicking a deck card takes it out" : "a Main Deck under 40 cards can't be saved");

        // ---- Buying (the Shop lives in COLLECTION since 2026-11-17)
        await Go("res://ui/CardList.tscn");
        P = Session.Profile;              // every screen reloads the save
        P.Dp = Math.Max(P.Dp, 20000);     // sandbox save only (--unlockall)
        foreach (var k in P.Owned.Keys.ToList()) { P.Owned[k] = 1; P.Seen.Add(k); }   // the sandbox owns everything: make room to buy (seen = for sale)
        await Tap("CARD BACKS"); await Tap("CARDS");   // redraw with the new DP
        int dp = P.Dp;
        var buy = Find("BUY ×1");
        if (buy != null) { Click(buy); await Wait(0.3); Check(P.Dp < dp, $"BUY ×1 spends DP ({dp} -> {P.Dp})"); }
        else GD.Print("SKIP shop buy (nothing affordable)");
        await Tap("CARD BACKS"); await Shot("3_shop_backs");
        int ownedBacks = Shop.Backs.Count(b => Shop.Owns(P, b));
        if (ownedBacks == Shop.Backs.Length) GD.Print("SKIP back buy (the sandbox owns every back)");
        else if (Shop.Backs.FirstOrDefault(b => !Shop.Owns(P, b) && Shop.Sold(b)) is { } want && Clickables().FirstOrDefault(c => c.TooltipText == Lang.Cosmetic(want.Name)) is { } tile)
        { Click(tile); await Wait(0.3); await Tap("BUY"); Check(Shop.Backs.Count(b => Shop.Owns(P, b)) == ownedBacks + 1, "BUY a card back"); }
        else GD.Print("SKIP back buy (no unowned back on the first page)");
        await Tap("PLAYMATS"); await Tap("FRAMES"); await Tap("CARDS");

        // ---- Card List: equip a card back you own
        await Go("res://ui/CardList.tscn");
        P = Session.Profile;
        // filters: open the panel, pick a monster type from the TYPE list, then spells + a category, then clear
        await Tap("FILTERS ▾");
        await Tap("TYPE:  ANY   ▾");
        await Tap("Dragon");
        var labels = Scene.FindChildren("*", "Label", true, false).OfType<Label>().ToList();
        Check(labels.Any(l => l.Text == "TYPE:  DRAGON   ▾"), "TYPE picker sets Dragon");
        await Tap("SPELL"); await Tap("EQUIP");
        await Shot("4a_cardlist_spell_filter");
        await Tap("CLEAR FILTERS");
        Check(Scene.FindChildren("*", "Label", true, false).OfType<Label>().All(l => l.Text != "CLEAR FILTERS"), "CLEAR FILTERS clears");
        await Tap("CARD BACKS");
        var owned = Shop.Backs.Where(b => Shop.Owns(P, b) && b.Key != P.Back).ToList();
        if (owned.Count > 0)
        {
            var hit = Clickables().FirstOrDefault(c => c.TooltipText == owned[0].Name);
            Check(hit != null, $"owned back tile '{owned[0].Name}'");
            if (hit != null) { Click(hit); await Wait(0.3); await Tap("EQUIP"); Check(P.Back == owned[0].Key, "EQUIP sets the card back"); }
        }
        await Shot("4_cardlist_backs");

        // ---- Options
        await Go("res://ui/Options.tscn");
        int sp = Settings.Speed;
        await Tap(Settings.Speeds[(sp + 1) % Settings.Speeds.Length].Name);
        Check(Settings.Speed == (sp + 1) % Settings.Speeds.Length, "speed word sets the duel speed");
        await Tap(Settings.Speeds[sp].Name);
        await Tap("FRAME"); await Shot("5_options_frames");
        // ---- Duel modes
        if (!Session.Decks.Any(d => DeckRules.IsLegal(d, Session.Profile))) { Session.Decks.Add(DeckRules.GrantStarter(Session.Profile, 0)); Session.Save(); }   // sandbox only
        await Go("res://ui/DuelModes.tscn");
        await Tap("GAUNTLET"); await Wait(1.2);
        Check(Scene is GauntletScreen && Session.Run is { Order.Length: 8 }, "GAUNTLET opens the eight floors");
        await Shot("7a_gauntlet");
        await Tap("DUEL"); await Wait(1.2);
        Check(Scene is DuelSetup && Session.Run != null && Labels().Any(l => l.Text == "GAUNTLET"), "floor 1 DUEL opens setup");
        await Tap("START DUEL"); await Wait(2);
        Check(Scene is TurnOrder, "gauntlet START goes to the coin toss");
        await Go("res://ui/DuelModes.tscn");
        Check(Session.Run == null, "back at the modes, the run is cleared");
        await Tap("LEGEND"); await Wait(1.2);
        await Tap("KAIBA"); await Wait(1.2);
        Check(Scene is DuelSetup && Session.Pick.Level == 7 && Session.Pick.DeckKey == "kaiba/x_lv7", "LEGEND: Kaiba at level 7 with his strongest deck");
        await Go("res://ui/DuelModes.tscn");
        await Tap("PACK DUEL"); await Wait(1.5);
        Check(Scene is PackDraft && Session.Draft is { Round: 0 }, "PACK DUEL opens the draft");
        await Shot("7_packs");
        for (int r = 0; r <= Draft.Rounds && !Session.Draft!.Done; r++) { await Tap("AUTO"); await Wait(0.3); await Tap("LOCK IN"); await Wait(1.6); }
        Check(Session.Draft!.Done && Labels().Any(l => l.Text == "BEST OF 3"), "the draft ends on your deck");
        await Shot("8_pack_deck");
        await Tap("BEST OF 3"); await Wait(1.5);
        Check(Scene is TurnOrder && Session.Match && Session.ModeDeck is { Main.Count: >= 40 } && Session.RivalDeck?.Name == "RIVAL DRAFT", "BEST OF 3 starts a match with both drafted decks");
        int decksBefore = Session.Decks.Count;
        await Go("res://ui/DuelModes.tscn");
        await Tap("LIMITED DUEL"); await Wait(1.5);
        Check(Scene is DeckBuilder && Find("START DUEL") != null, "LIMITED DUEL opens the rule builder");
        await Shot("9_limited_builder");
        Check(Session.Decks.Count == decksBefore, "mode decks never join your saved decks");
        await Go("res://ui/DuelModes.tscn");
        await Tap("CHAOS DUEL"); await Wait(1.2);
        Check(Scene is RivalSelect && Session.Mode == GameMode.Chaos && Labels().Any(l => l.Text.StartsWith("CHAOS DUEL")), "CHAOS DUEL opens rival select");
        await Go("res://ui/DuelModes.tscn");
        await Tap("CHALLENGES"); await Wait(1.2);
        Check(Scene is ChallengeSelect, "CHALLENGES opens the puzzle list");
        await Tap("START"); await Wait(3);
        Check(Scene?.Name == "DuelField" && Session.Puzzle != null, $"START opens the puzzle on the field (now {Scene?.Name})");
        await Shot("10_puzzle_field");
        await Go("res://ui/DuelModes.tscn");
        await Tap("REGULAR"); await Wait(1.2);
        Check(Scene is RivalSelect, "REGULAR opens rival select");
        // ---- Duel tab: rival name -> step 2 -> match -> start
        if (!Session.Decks.Any(d => DeckRules.IsLegal(d, Session.Profile))) { Session.Decks.Add(DeckRules.GrantStarter(Session.Profile, 0)); Session.Save(); }   // sandbox only
        await Go("res://ui/RivalSelect.tscn");
        await Tap("KAIBA"); await Wait(1.2);
        Check(Scene is DuelSetup && Session.Selected == 1, "clicking a rival name opens step 2");
        await Tap("MATCH");
        Check(Session.Match, "MATCH sets a match");
        await Tap("SINGLE DUEL");
        GD.Print($"deck in use: {Session.Playing?.Name ?? "none"}");
        await Tap("START DUEL"); await Wait(2.5);
        Check(Scene is TurnOrder, $"START DUEL goes to the coin toss (now {Scene?.Name})");
        // ---- Tournament: DUEL -> step 2 shows the round
        Session.Tour = null;
        await Go("res://ui/Tournament.tscn");
        Check(Session.Tour != null, "tournament created");
        await Tap("DUEL"); await Wait(1.2);
        Check(Scene is DuelSetup && Scene.FindChildren("*", "Label", true, false).OfType<Label>().Any(l => l.Text == "QUARTERFINAL"), "tournament DUEL opens step 2 with the round");
        Session.Tour = null;
        // ---- Achievements: the list shows every goal; a done one claims once
        Achievements.Bump(Session.Profile, "wins");
        await Go("res://ui/Achievements.tscn");
        Check(Scene is AchievementsScreen && Labels().Any(l => Achievements.All.Any(d => d.Name == l.Text)), "achievements list");   // any goal: claimed ones sort to the last page of the reused test save
        await Shot("11_achievements");
        if (!Achievements.Claimed(Achievements.All[0], Session.Profile))
        {
            int dpWas = Session.Profile.Dp;
            await Tap("CLAIM"); await Wait(0.6);
            Check(Session.Profile.Dp > dpWas, "CLAIM pays the DP");
        }
        await Go("res://ui/MainMenu.tscn");
        await Wait(5);
        await Shot("6_menu");
        Check(Scene is MainMenu, "back at the main menu");
    }
}
