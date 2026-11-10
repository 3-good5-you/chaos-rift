using Godot;
using PoC.Engine;
using static PoC.Game.UI.ConceptKit;

namespace PoC.Game.UI;

/// The main menu's DUEL tab (choose a rival, your deck, single duel or match), Chaos Rift samples 2026-11-08.
/// `--screen=rival --variant=a|b|c`
/// a = Line-up: the four rivals stand across the rift; the chosen one steps forward lit, the others wait in grey;
///     your deck and the mode along the bottom.
/// b = Spotlight: the rivals' names as a list in the light (like the main menu); the chosen rival large in the dark
///     with difficulty, deck and rewards; your deck bottom left.
/// c = Versus: you in the light with your deck under you (‹ › to switch), the rival in the dark (‹ › to switch, the
///     four as small medallions under them), VS on the rift, mode and DUEL in the middle.
public partial class RiftScreens
{
    int rsel = 1;

    void RivalS()
    {
        if (Session.Picks.Length != RivalPanels.All.Length) Session.RollPicks();
        switch (variant)
        {
            case "b": RivalSpotlight(); break;
            case "c": RivalVersus(); break;
            default: RivalLineup(); break;
        }
    }

    static OpponentPick PickOf(int i) => Session.Picks[i];
    string RewardLine(int i)
    {
        var (count, fresh) = Rewards.For(PickOf(i).Level, false);
        return $"WIN  {count} CARD{(count == 1 ? "" : "S")}{(fresh > 0 ? $" ({fresh} NEW)" : "")}  +  {Shop.Earn(PickOf(i).Level, true)} DP";
    }
    static PoC.Engine.Deck? MyDeck => Session.Playing ?? Session.Decks.FirstOrDefault();

    /// Your deck: the cover on two backs, name, ‹ › to switch.
    void DeckPick(Vector2 at, float w, bool light)
    {
        var d = MyDeck;
        float h = w * 1.458f;
        for (int k = 2; k >= 1; k--)
        {
            var b = Ink_(Cosmetics.Back(Session.Profile.Back, 256), at + new Vector2(k * 7, -k * 5), new Vector2(w, h));
            b.PivotOffset = new Vector2(w, h) / 2; b.Rotation = k * 0.04f;
        }
        if (d != null && d.CoverCard() >= 0) Card(Named(Db.Get(d.CoverCard()).Name), at + new Vector2(w, h) / 2, h, -0.02f, true, 0.7f);
        var ink = light ? Ink : Silver;
        C(this, "YOUR DECK", Thin, 14, ink * new Color(1, 1, 1, 0.55f), at + new Vector2(w + 24, 6));
        C(this, "‹", Title, 30, ink * new Color(1, 1, 1, 0.5f), at + new Vector2(w + 22, 28));
        C(this, (d?.Name ?? "NO DECK").ToUpperInvariant(), Title, 24, ink, at + new Vector2(w + 50, 34));
        C(this, "›", Title, 30, ink * new Color(1, 1, 1, 0.5f), at + new Vector2(w + 62 + Title.GetStringSize((d?.Name ?? "").ToUpperInvariant(), HorizontalAlignment.Left, -1, 24).X, 28));
        C(this, d != null ? $"{d.Main.Count} CARDS   ·   2004 ✓" : "", Thin, 15, ink * new Color(1, 1, 1, 0.55f), at + new Vector2(w + 24, 78));
    }

    void Modes(Vector2 at, bool light)
    {
        string[] m = { "SINGLE DUEL", "MATCH  (BEST OF 3)" };
        float x = at.X;
        for (int i = 0; i < 2; i++)
        {
            var c = i == 0 ? (light ? GoldDeep : GoldLight) : (light ? Ink : Silver) * new Color(1, 1, 1, 0.5f);
            C(this, m[i], Title, 20, c, new Vector2(x, at.Y));
            float w = Title.GetStringSize(m[i], HorizontalAlignment.Left, -1, 20).X;
            if (i == 0) Pip(new Vector2(x + w / 2, at.Y + 34), 8);
            x += w + 50;
        }
    }

    void Drops(Vector2 at, int level, bool centre = true)
    {
        var tier = Session.TierColor(level);
        string word = new OpponentPick("", "", level).Tier;
        string dots = new string('●', level) + new string('○', 7 - level);
        C(this, dots, Body, 16, tier, at - new Vector2(centre ? 150 : 0, 0), new Vector2(300, 22), centre ? HorizontalAlignment.Center : HorizontalAlignment.Left);
        C(this, word, Title, 15, tier, at + new Vector2(centre ? -150 : 0, 24), new Vector2(300, 22), centre ? HorizontalAlignment.Center : HorizontalAlignment.Left);
    }

    // a ---------------------------------------------------------------- line-up
    void RivalLineup()
    {
        Rift(0.5f, 0.5f);
        C(this, "‹  MENU", Title, 18, Graphite, new Vector2(62, 30));
        C(this, "CHOOSE YOUR RIVAL", Title, 40, Ink, new Vector2(80, 58));
        Hair(new Vector2(82, 108), 180, GoldC);
        int n = RivalPanels.All.Length;
        float colW = (W - 200) / n;
        for (int i = 0; i < n; i++)
        {
            var r = RivalPanels.All[i];
            bool on = i == rsel;
            var tex = RivalPanels.Portrait(r);
            float ph = H * (on ? 0.56f : 0.48f), pw = ph * tex.GetWidth() / tex.GetHeight();
            float cx = 100 + colW * (i + 0.5f);
            var at = new Vector2(cx - pw / 2, H * 0.66f - ph);
            bool light = OnLight(new Vector2(cx, H / 2));
            if (on) AddChild(new ColorRect { Position = new Vector2(cx - 260, H * 0.16f), Size = new Vector2(520, H * 0.6f), MouseFilter = MouseFilterEnum.Ignore,
                Material = new ShaderMaterial { Shader = new Shader { Code = "shader_type canvas_item; uniform vec4 c : source_color; void fragment(){ vec2 p = (UV - 0.5) * vec2(1.6, 1.0); COLOR = vec4(c.rgb, exp(-dot(p, p) * 4.0) * 0.4); }" } }.P("c", light ? new Color(1f, 0.85f, 0.55f) : new Color(0.7f, 0.72f, 0.9f)) });
            var p = Ink_(tex, at, new Vector2(pw, ph));
            if (!on) p.Material = new ShaderMaterial { Shader = new Shader { Code = "shader_type canvas_item; void fragment(){ vec4 a = texture(TEXTURE, UV); float l = dot(a.rgb, vec3(0.3,0.59,0.11)); COLOR = vec4(vec3(l) * 0.85, a.a * 0.55); }" } };
            var ink = light ? Ink : Silver;
            C(this, r.Name, Title, on ? 40 : 28, on ? (light ? GoldDeep : GoldLight) : ink * new Color(1, 1, 1, 0.6f), new Vector2(cx - 200, H * 0.67f), new Vector2(400, 50), HorizontalAlignment.Center);
            Drops(new Vector2(cx, H * 0.67f + 56), PickOf(i).Level);
            if (on) C(this, RewardLine(i), Thin, 16, light ? GoldDeep : GoldLight, new Vector2(cx - 250, H * 0.67f + 108), new Vector2(500, 22), HorizontalAlignment.Center);
        }
        // bottom: your deck, mode, DUEL
        DeckPick(new Vector2(80, H - 180), 80, true);
        Modes(new Vector2(W * 0.55f, H - 92), false);
        focusables.Add(Button("DUEL", new Vector2(W - 170, H - 80), 30));
    }

    // b ---------------------------------------------------------------- spotlight
    void RivalSpotlight()
    {
        if (Demo.Arg("state") == "setup") { RivalSetup(); return; }
        if (Demo.Arg("state") == "modes") { RivalModes(); return; }
        if (Demo.Arg("state") == "challenges") { Challenges(); return; }
        Rift(0.46f, 0.4f);
        C(this, "‹  MENU", Title, 18, Graphite, new Vector2(62, 30));
        C(this, "CHOOSE YOUR RIVAL", Title, 34, Ink, new Vector2(80, 58));
        Hair(new Vector2(82, 102), 140, GoldC);
        float y = H * 0.3f;
        for (int i = 0; i < RivalPanels.All.Length; i++)
        {
            bool on = i == rsel;
            var r = RivalPanels.All[i];
            C(this, r.Name, Title, on ? 64 : 44, on ? GoldDeep : Ink * new Color(1, 1, 1, 0.55f), new Vector2(120, y));
            if (on) Pip(new Vector2(88, y + 38), 14);
            y += on ? 120 : 92;
        }
        // the chosen rival, large in the dark: name and difficulty once, nothing else
        var rv = RivalPanels.All[rsel];
        var tex = RivalPanels.Portrait(rv);
        float ph = H * 0.84f, pw = ph * tex.GetWidth() / tex.GetHeight();
        Ink_(tex, new Vector2(W * 0.68f - pw / 2, H - ph), new Vector2(pw, ph));
        var tier = Session.TierColor(PickOf(rsel).Level);
        C(this, PickOf(rsel).Tier, Title, 22, tier, new Vector2(W - 380, 70), new Vector2(320, 30), HorizontalAlignment.Right);
        C(this, new string('●', PickOf(rsel).Level), Body, 18, tier, new Vector2(W - 380, 104), new Vector2(320, 24), HorizontalAlignment.Right);
    }

    /// Step 2 after choosing a rival: the rival small in the dark; in the light, the duel type and your deck.
    void RivalSetup()
    {
        Rift(0.6f, 0.54f);
        C(this, "‹  RIVALS", Title, 18, Graphite, new Vector2(62, 30));
        var rv = RivalPanels.All[rsel];
        // the rival, on the dark side
        var tex = RivalPanels.Portrait(rv);
        float ph = H * 0.7f, pw = ph * tex.GetWidth() / tex.GetHeight();
        Ink_(tex, new Vector2(W * 0.8f - pw / 2, H - ph), new Vector2(pw, ph));
        C(this, rv.Name, Title, 54, Silver, new Vector2(W * 0.8f - 300, 70), new Vector2(600, 70), HorizontalAlignment.Center);
        C(this, PickOf(rsel).Tier, Title, 18, Session.TierColor(PickOf(rsel).Level), new Vector2(W * 0.8f - 300, 142), new Vector2(600, 24), HorizontalAlignment.Center);
        // 1 duel type
        C(this, "1   DUEL TYPE", Title, 20, Graphite, new Vector2(110, 120));
        string[] modes = { "SINGLE DUEL", "MATCH" }; string[] sub = { "One duel", "Best of three, side deck between duels" };
        for (int i = 0; i < 2; i++)
        {
            bool on = i == 0;
            var at = new Vector2(110 + i * 420, 170);
            C(this, modes[i], Title, on ? 40 : 34, on ? GoldDeep : Ink * new Color(1, 1, 1, 0.5f), at);
            C(this, sub[i], Body, 22, Graphite * new Color(1, 1, 1, on ? 0.9f : 0.5f), at + new Vector2(2, 56));
            if (on) Hair(at + new Vector2(0, 50), Title.GetStringSize(modes[i], HorizontalAlignment.Left, -1, 40).X, GoldC, null, 2.5f);
        }
        // 2 your deck: the cases in a row
        C(this, "2   YOUR DECK", Title, 20, Graphite, new Vector2(110, 330));
        var decks = Session.Decks.ToList();
        const float cw = 150; float ch = cw * 1.458f;
        for (int i = 0; i < decks.Count && i < 4; i++)
        {
            var d = decks[i];
            bool on = d == MyDeck;
            var at = new Vector2(130 + i * (cw + 90), 400);
            for (int k = 2; k >= 1; k--)
            {
                var b = Ink_(Cosmetics.Back(Session.Profile.Back, 256), at + new Vector2(k * 8, -k * 6), new Vector2(cw, ch));
                b.PivotOffset = new Vector2(cw, ch) / 2; b.Rotation = k * 0.04f;
            }
            if (d.CoverCard() >= 0) { var c = Card(Db.Get(d.CoverCard()), at + new Vector2(cw, ch) / 2, ch, -0.02f, true, on ? 1.3f : 0.6f); if (on) c.Scale = new Vector2(1.06f, 1.06f); }
            C(this, d.Name.ToUpperInvariant(), Title, 15, on ? GoldDeep : Ink, at + new Vector2(-30, ch + 20), new Vector2(cw + 60, 22), HorizontalAlignment.Center);
            if (on) Pip(at + new Vector2(cw / 2, ch + 56), 8);
        }
        focusables.Add(Button("START DUEL", new Vector2(W * 0.4f, H - 100), 32));
    }

    /// Step 2 with every duel mode: a short list of names, the chosen one's one-line rule under the list.
    void RivalModes()
    {
        Rift(0.6f, 0.54f);
        C(this, "‹  RIVALS", Title, 18, Graphite, new Vector2(62, 30));
        var rv = RivalPanels.All[rsel];
        var tex = RivalPanels.Portrait(rv);
        float ph = H * 0.7f, pw = ph * tex.GetWidth() / tex.GetHeight();
        Ink_(tex, new Vector2(W * 0.8f - pw / 2, H - ph), new Vector2(pw, ph));
        C(this, rv.Name, Title, 54, Silver, new Vector2(W * 0.8f - 300, 70), new Vector2(600, 70), HorizontalAlignment.Center);
        C(this, "1   DUEL TYPE", Title, 20, Graphite, new Vector2(110, 110));
        (string N, string D)[] modes =
        {
            ("SINGLE DUEL", "One duel."), ("MATCH", "Best of three, side deck between duels."),
            ("GAUNTLET", "All four rivals in a row, one deck, your LP carries over."),
            ("CHAOS DUEL", "Every few turns the rift adds a new rule for both players."),
            ("LIMITED DUEL", "Today: Normal monsters only, 4000 LP."),
            ("LEGEND", "Their strongest deck at full strength. Rare reward."),
        };
        int sel = 2;
        for (int i = 0; i < modes.Length; i++)
        {
            bool on = i == sel;
            var at = new Vector2(110 + i % 3 * 300, 158 + i / 3 * 62);
            C(this, modes[i].N, Title, on ? 28 : 24, on ? GoldDeep : Ink * new Color(1, 1, 1, 0.5f), at);
            if (on) Hair(at + new Vector2(0, 38), Title.GetStringSize(modes[i].N, HorizontalAlignment.Left, -1, 28).X, GoldC, null, 2.5f);
        }
        C(this, modes[sel].D, Body, 22, Graphite, new Vector2(112, 300));
        C(this, "2   YOUR DECK", Title, 20, Graphite, new Vector2(110, 370));
        const float cw = 150; float ch = cw * 1.458f;
        var decks = Session.Decks.ToList();
        for (int i = 0; i < decks.Count && i < 4; i++)
        {
            var d = decks[i]; bool on = d == MyDeck;
            var at = new Vector2(130 + i * (cw + 90), 440);
            for (int k = 2; k >= 1; k--) { var b = Ink_(Cosmetics.Back(Session.Profile.Back, 256), at + new Vector2(k * 8, -k * 6), new Vector2(cw, ch)); b.PivotOffset = new Vector2(cw, ch) / 2; b.Rotation = k * 0.04f; }
            if (d.CoverCard() >= 0) Card(Db.Get(d.CoverCard()), at + new Vector2(cw, ch) / 2, ch, -0.02f, true, on ? 1.3f : 0.6f);
            C(this, d.Name.ToUpperInvariant(), Title, 15, on ? GoldDeep : Ink, at + new Vector2(-30, ch + 20), new Vector2(cw + 60, 22), HorizontalAlignment.Center);
        }
        focusables.Add(Button("START DUEL", new Vector2(W * 0.4f, H - 90), 32));
    }

    /// CHALLENGES (a new main-menu entry): puzzles and sealed deck, the modes that need their own screens.
    void Challenges()
    {
        Rift(0.5f, 0.5f);
        C(this, "‹  MENU", Title, 18, Graphite, new Vector2(62, 30));
        C(this, "CHALLENGES", Title, 40, Ink, new Vector2(80, 58));
        Hair(new Vector2(82, 108), 160, GoldC);
        // left: puzzles
        Card(Named("Dark Magician"), new Vector2(W * 0.25f, H * 0.42f), H * 0.4f, -0.04f);
        C(this, "PUZZLES", Title, 46, Ink, new Vector2(W * 0.25f - 300, H * 0.66f), new Vector2(600, 60), HorizontalAlignment.Center);
        C(this, "A set board. Win this turn with the cards you are given.", Body, 20, Graphite, new Vector2(W * 0.25f - 300, H * 0.66f + 64), new Vector2(600, 28), HorizontalAlignment.Center);
        C(this, "SOLVED 7 / 30", Thin, 18, GoldDeep, new Vector2(W * 0.25f - 300, H * 0.66f + 104), new Vector2(600, 24), HorizontalAlignment.Center);
        // right: sealed
        for (int k = 0; k < 3; k++) { var b = Ink_(Cosmetics.Back("30_chaos_vortex", 256), new Vector2(W * 0.75f - 90 + (k - 1) * 70, H * 0.42f - 130), new Vector2(180, 262)); b.PivotOffset = new Vector2(90, 131); b.Rotation = (k - 1) * 0.12f; }
        C(this, "SEALED DECK", Title, 46, Silver, new Vector2(W * 0.75f - 300, H * 0.66f), new Vector2(600, 60), HorizontalAlignment.Center);
        C(this, "Open 6 packs, build a 40-card deck from them, duel the rivals.", Body, 20, Mist, new Vector2(W * 0.75f - 300, H * 0.66f + 64), new Vector2(600, 28), HorizontalAlignment.Center);
        C(this, "BEST RUN  3 WINS", Thin, 18, GoldLight, new Vector2(W * 0.75f - 300, H * 0.66f + 104), new Vector2(600, 24), HorizontalAlignment.Center);
    }

    // c ---------------------------------------------------------------- versus
    void RivalVersus()
    {
        Rift(0.5f, 0.5f);
        C(this, "‹  MENU", Title, 18, Graphite, new Vector2(62, 30));
        C(this, "DUEL", Title, 40, Ink, new Vector2(80, 58));
        Hair(new Vector2(82, 108), 100, GoldC);
        // you
        var you = Cosmetics.Framed(Settings.Avatar, Session.Profile.Frame, 260);
        you.Position = new Vector2(W * 0.25f - 130, H * 0.18f); AddChild(you);
        C(this, Settings.PlayerName.ToUpperInvariant(), Title, 40, Ink, new Vector2(W * 0.25f - 300, H * 0.18f + 276), new Vector2(600, 50), HorizontalAlignment.Center);
        DeckPick(new Vector2(W * 0.25f - 230, H * 0.62f), 110, true);
        // the rival
        var rv = RivalPanels.All[rsel];
        var tex = RivalPanels.Portrait(rv);
        float ph = H * 0.55f, pw = ph * tex.GetWidth() / tex.GetHeight();
        Ink_(tex, new Vector2(W * 0.75f - pw / 2, H * 0.12f), new Vector2(pw, ph));
        C(this, "‹", Title, 54, Silver * new Color(1, 1, 1, 0.6f), new Vector2(W * 0.75f - pw / 2 - 70, H * 0.36f));
        C(this, "›", Title, 54, Silver * new Color(1, 1, 1, 0.6f), new Vector2(W * 0.75f + pw / 2 + 40, H * 0.36f));
        C(this, rv.Name, Title, 44, Silver, new Vector2(W * 0.75f - 300, H * 0.12f + ph + 8), new Vector2(600, 56), HorizontalAlignment.Center);
        Drops(new Vector2(W * 0.75f, H * 0.12f + ph + 70), PickOf(rsel).Level);
        C(this, RewardLine(rsel), Thin, 16, GoldLight, new Vector2(W * 0.75f - 300, H * 0.12f + ph + 124), new Vector2(600, 22), HorizontalAlignment.Center);
        for (int i = 0; i < RivalPanels.All.Length; i++)
        {
            var c = new Vector2(W * 0.75f + (i - 1.5f) * 110, H - 76);
            var m = Ink_(RivalPanels.Art(RivalPanels.All[i]), c - new Vector2(40, 40), new Vector2(80, 80));
            m.StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered; m.Material = Kit.RoundedMat(1, 0.5f);
            if (i != rsel) m.Modulate = new Color(0.6f, 0.6f, 0.65f, 0.5f); else Pip(c + new Vector2(0, 52), 8);
        }
        var vs = C(this, "VS", Title, 110, Colors.White, new Vector2(W / 2 - 200, H * 0.3f), new Vector2(400, 140), HorizontalAlignment.Center);
        vs.Material = TwoTone();
        // mode + DUEL on the rift
        var m1 = C(this, "SINGLE DUEL", Title, 20, Colors.White, new Vector2(W / 2 - 300, H * 0.56f), new Vector2(600, 28), HorizontalAlignment.Center);
        m1.Material = TwoTone().P("lit", 1f);
        var m2 = C(this, "MATCH  (BEST OF 3)", Title, 18, Colors.White, new Vector2(W / 2 - 300, H * 0.6f), new Vector2(600, 28), HorizontalAlignment.Center);
        m2.Material = TwoTone().P("dim", 0.5f);
        focusables.Add(Button("DUEL", new Vector2(W / 2, H * 0.72f), 32));
    }
}
