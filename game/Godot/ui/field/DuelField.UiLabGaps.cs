using Godot;
using PoC.Engine;

namespace PoC.Game.UI;

/// `--uilab`, round 8: samples for the "nice-to-have" rows of game/LAB_REPORT.md (Cyber Blader's mode, Chimeratech
/// Overdragon's attacks left, Crystal Beasts as Continuous Spells, Exodius's pieces, Stromberg's face-down banished
/// cards). Rounds 5-7 (U1-U10) are picked and built live in DuelField.Marks.cs. Picks go to
/// user://ui_lab_picks_round8.json and game/ui_picks_round8.txt.
public partial class DuelField
{
    PoC.Engine.DuelSetup GapLabSetup()
    {
        var db = CardDb.Shared;
        string[] you = { "Skilled Dark Magician", "Card Guard", "Cloudian - Nimbusman", "Clock Tower Prison", "Venom Swamp",
                         "Arcana Force XIV - Temperance", "Arcana Force I - The Magician", "Amazoness Chain Master",
                         "Crystal Beast Ruby Carbuncle", "Crystal Beast Sapphire Pegasus", "Exodius the Ultimate Forbidden Lord", "Exodia the Forbidden One",
                         "Left Arm of the Forbidden One", "Right Arm of the Forbidden One", "Left Leg of the Forbidden One", "Right Leg of the Forbidden One",
                         "Golden Castle of Stromberg" };
        string[] them = { "Vorse Raider", "Gaia The Fierce Knight", "Trap Hole", "Feral Imp", "Dark Hole", "Arcana Force VII - The Chariot" };
        var s = new PoC.Engine.DuelSetup { Seed = 1, FirstPlayer = 0, Shuffle = false, Classic = true };
        List<int> Deck(string[] top) => top.Concat(Enumerable.Range(0, 40 - top.Length).Select(i => Filler[i % Filler.Length])).Select(n => db.Named(n).Idx).ToList();
        s.Main[0].AddRange(Deck(you)); s.Main[1].AddRange(Deck(them));
        s.Extra[0].AddRange(new[] { "Cyber Blader", "Chimeratech Overdragon" }.Select(n => db.Named(n).Idx));
        return s;
    }

    // ---------------------------------------------------------------- pieces

    Panel Pane(Rect2 r, Color edge, int radius = 17, float glow = 0, Control? parent = null, float bg = 0.92f)
    {
        var p = TmpBox(r, Pill(new Color(0.03f, 0.03f, 0.05f, bg), edge, radius, 0, glow > 0 ? new Color(edge, glow) : (Color?)null, glow > 0 ? 12 : 0, 2));
        p.MouseFilter = MouseFilterEnum.Ignore;
        if (parent != null) p.Reparent(parent, false);
        return p;
    }

    TextureRect Pic(Control parent, Texture2D tex, Rect2 r)
    {
        var t = new TextureRect { Texture = tex, ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.Scale, Position = r.Position, Size = r.Size, MouseFilter = MouseFilterEnum.Ignore, TextureFilter = TextureFilterEnum.LinearWithMipmaps };
        parent.AddChild(t);
        return t;
    }
    /// A point on the card in its own frame on screen: fx -1..1 = its left..right, fz -1..1 = its top..bottom as its
    /// owner sees it (so a rival card's bottom-right is your top-left); beyond 1 = outside the card.
    Vector2 Pt(View v, float fx, float fz) => cam.UnprojectPosition(v.Root.GlobalTransform * new Vector3(fx * CW / 2, 0, fz * CH / 2));

    // ---------------------------------------------------------------- hover: a copy of the card details panel

    // The samples lay the details out themselves (same place, look and type sizes as the real panel), so each can
    // show its own arrangement; the lab's list steps aside while one shows.
    readonly List<Control> labHid = new();
    Panel dp = null!;
    float dy;

    void LabCover(bool hide)
    {
        if (!hide) { foreach (var c in labHid) c.Visible = true; labHid.Clear(); return; }
        foreach (var c in hud.GetChildren().OfType<Control>())
            if ((c is ScrollContainer || c is VBoxContainer) && c.Position.X < 40 && c.Visible) { c.Visible = false; labHid.Add(c); }
    }

    async Task Done(double s) { await Hold(s); LabCover(false); }

    float DpMax => H - ProfH - 72;

    /// Starts the details panel for `d`: full card art, or only a strip of the picture (`strip` = its height).
    void DpStart(CardData d, float strip = 0)
    {
        LabCover(true);
        dp = TmpBox(new Rect2(24, 24, DetailW, 100), Glass(RivalPanels.Gold * new Color(1, 1, 1, 0.45f)));
        dp.ClipContents = true; dp.MouseFilter = MouseFilterEnum.Ignore;
        float aw = DetailW - 40;
        var tex = CardArt.Large(d);
        if (strip <= 0) { Pic(dp, tex, new Rect2(20, 20, aw, aw * CardH)); dy = 20 + aw * CardH + 12; return; }
        // the picture window of the scan (x 12-88 %, y 18-70 %), cut to `strip` high around its middle
        var sz = tex.GetSize();
        float rw = sz.X * 0.754f, rh = rw * strip / aw;
        Pic(dp, new AtlasTexture { Atlas = tex, Region = new Rect2(sz.X * 0.123f, sz.Y * 0.44f - rh / 2, rw, rh) }, new Rect2(20, 20, aw, strip));
        dy = 20 + strip + 12;
    }
    Label DpLine(string t, Font f, int size, Color c, float h, int outline = 0)
    {
        var l = Put(dp, t, f, size, c, new Vector2(20, dy), new Vector2(DetailW - 40, h), HorizontalAlignment.Left, outline);
        dy += h;
        return l;
    }
    static float Measure(string t, int size, float w) => Kit.Ui.GetMultilineStringSize(t, HorizontalAlignment.Left, w, size).Y * 1.3f + 2;
    /// Wrapped text at the cursor; returns its label (`box`: the rect it took, for a frame around it).
    Label DpPara(string t, int size, Color c, float pad = 0)
    {
        float w = DetailW - 40 - pad * 2, h = Measure(t, size, w);
        var l = Put(dp, t, Ui, size, c, new Vector2(20 + pad, dy + pad), new Vector2(w, h));
        l.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        l.ClipText = true;
        l.Size = new Vector2(w, h);   // Put grew it to the one-line width
        dy += h + pad * 2;
        return l;
    }
    /// Name, type line, stars and ATK/DEF as the real panel shows them.
    void DpHead(CardData d, string? name = null, int? atk = null, int? def = null, Action? starRow = null)
    {
        FitText(DpLine(name ?? d.Name, Cond, 32, Colors.White, 40), 32);
        DpLine(d.IsMonster ? $"{d.Attribute}  ·  {d.Race.ToUpperInvariant()} / {d.Frame.ToUpperInvariant()}" : $"{d.Kind.ToString().ToUpperInvariant()}  ·  {d.Frame.ToUpperInvariant()}", Ui, 17, RivalPanels.Gold, 24);
        if (!d.IsMonster) return;
        if (starRow != null) starRow(); else DpLine(new string('★', Math.Max(0, d.Level)), Ui, 18, new Color(1f, 0.7f, 0.25f), 24);
        DpLine($"ATK {atk ?? d.Atk}    DEF {def ?? d.Def}", Impact, 30, Colors.White, 40, 6);
    }
    void DpRule() { dp.AddChild(new ColorRect { Position = new Vector2(20, dy + 4), Size = new Vector2(DetailW - 40, 1), Color = RivalPanels.Gold * new Color(1, 1, 1, 0.4f), MouseFilter = MouseFilterEnum.Ignore }); dy += 12; }
    void DpEnd() => dp.Size = new Vector2(DetailW, Math.Min(dy + 18, DpMax));

    /// The real panel's whole look for a plain card (counters / coin rows go in between by the caller).
    void DpCard(CardData d) { DpStart(d); DpHead(d); DpRule(); DpPara(d.Desc, 16, new Color(1, 1, 1, 0.82f)); DpEnd(); }

    // ---------------------------------------------------------------- the events

    void UiLabGaps()
    {
        void Add(string name, params (string, Func<Task>)[] vars) => labEvents.Add(("NICE-TO-HAVE · ROUND 8", name, vars));

        /// A card's text with one sentence marked by the gold bar (the kept-coin look), in the details copy.
        void MarkedText(CardData d, string sentence)
        {
            int i = d.Desc.IndexOf(sentence, StringComparison.Ordinal);
            string before = i > 0 ? d.Desc[..i].TrimEnd() : d.Desc, after = i >= 0 ? d.Desc[(i + sentence.Length)..].TrimStart() : "";
            DpPara(before, 16, new Color(1, 1, 1, 0.82f));
            if (i < 0) return;
            dy += 4;
            var l = DpPara(sentence, 16, Colors.White, 8);
            var bg = new Panel { Position = l.Position - new Vector2(8, 6), Size = l.Size + new Vector2(16, 10), MouseFilter = MouseFilterEnum.Ignore };
            bg.AddThemeStyleboxOverride("panel", Pill(new Color(RivalPanels.Gold, 0.08f), new Color(0, 0, 0, 0), 4, 0, null, 0, 0));
            dp.AddChild(bg); dp.MoveChild(bg, l.GetIndex());
            dp.AddChild(new ColorRect { Position = l.Position - new Vector2(8, 6), Size = new Vector2(4, l.Size.Y + 10), Color = RivalPanels.Gold, MouseFilter = MouseFilterEnum.Ignore });
            if (after.Length > 0) { dy += 4; DpPara(after, 16, new Color(1, 1, 1, 0.82f)); }
        }
        /// A row under ATK/DEF in the details copy: a value on the left, words beside it.
        void DpRow(string value, string words, Color c)
        {
            var row = Pane(new Rect2(20, dy + 4, DetailW - 40, 44), c, 12, 0, dp, 0.5f);
            Put(row, value, Impact, 24, c, new Vector2(10, 4), new Vector2(60, 36), HorizontalAlignment.Center, 4).VerticalAlignment = VerticalAlignment.Center;
            Put(row, words, Ui, 17, Colors.White, new Vector2(74, 9), new Vector2(DetailW - 120, 26));
            dy += 56;
        }
        /// The square middle of a card's picture.
        Texture2D Crop(CardData d)
        {
            var t = CardArt.Large(d); var z = t.GetSize();
            return new AtlasTexture { Atlas = t, Region = new Rect2(z.X * 0.16f, z.Y * 0.2f, z.X * 0.68f, z.X * 0.68f) };
        }
        /// A gold / coloured ring round a corner of the card holding a number (the mode, the pieces...).
        void CornerBadge(View v, float fx, float fz, string text, Color c)
        {
            var at = Pt(v, fx, fz);
            var b = Pane(new Rect2(at - new Vector2(15, 15), new Vector2(30, 30)), c, 15, 0.4f);
            Put(b, text, Impact, 18, c, Vector2.Zero, new Vector2(30, 30), HorizontalAlignment.Center, 3).VerticalAlignment = VerticalAlignment.Center;
        }

        // ---- N1 Cyber Blader: which of its three modes is on (the rival controls 2 monsters: double ATK)
        async Task<View> Blader()
        {
            var v = PutMon("Cyber Blader", 2, 0);
            PutMon("Vorse Raider", 1, 1); PutMon("Gaia The Fierce Knight", 3, 1);
            await Wait(0.5);
            return v;
        }
        const string bladerOn = "While your opponent controls only 2 monsters, double this card's ATK.";
        Add("1  Cyber Blader: active mode",
            ("A: Hover only: gold bar beside the active sentence", async () =>
            {
                var v = await Blader();
                var d = v.Card.Data; DpStart(d); DpHead(d); DpRule(); MarkedText(d, bladerOn); DpEnd();
                await Done(8);
            }),
            ("B: A + the mode number on the card's top-right", async () =>
            {
                var v = await Blader();
                CornerBadge(v, 0.72f, -0.8f, "2", RivalPanels.Gold);
                var d = v.Card.Data; DpStart(d); DpHead(d); DpRule(); MarkedText(d, bladerOn); DpEnd();
                await Done(8);
            }));

        // ---- N2 Chimeratech Overdragon: attacks left this Battle Phase
        async Task<View> Overdragon(int left)
        {
            var v = C("Chimeratech Overdragon");
            Place(v, Mon(v, 2, 0) with { Atk = 2400, Df = 2400, Counter = left });   // the built turn-count badge
            PutMon("Vorse Raider", 1, 1); PutMon("Gaia The Fierce Knight", 3, 1);
            await Wait(0.5);
            return v;
        }
        Add("2  Chimeratech Overdragon: attacks left",
            ("A: The blue round badge (as the turn counts) = attacks left", async () => { await Overdragon(2); await Hold(8); }),
            ("B: \"2 ATTACKS LEFT\" under the ATK / DEF", async () =>
            {
                var v = await Overdragon(0);
                var at = Pt(v, 0, 1.55f);
                uiTemp.Add(Put(hud, "2 ATTACKS LEFT", Cond, 18, CountBlue, at - new Vector2(80, 12), new Vector2(160, 24), HorizontalAlignment.Center, 5));
                await Hold(8);
            }),
            ("C: Hover only: a row in the details", async () =>
            {
                var v = await Overdragon(0);
                var d = v.Card.Data; DpStart(d); DpHead(d, atk: 2400, def: 2400); DpRow("2 / 3", "attacks left this Battle Phase", CountBlue); DpRule(); DpPara(d.Desc, 15, new Color(1, 1, 1, 0.82f)); DpEnd();
                await Done(8);
            }));

        // ---- N3 Crystal Beasts kept in the Spell & Trap Zone as Continuous Spells
        async Task<List<View>> Crystals()
        {
            var l = new List<View> { Put("Crystal Beast Ruby Carbuncle", SpellZ(1, 0, true)), Put("Crystal Beast Sapphire Pegasus", SpellZ(3, 0, true)) };
            await Wait(0.5);
            return l;
        }
        Color spellGreen = new(0.3f, 0.85f, 0.6f);
        Add("3  Crystal Beast in the Spell & Trap Zone",
            ("A: \"CONT. SPELL\" tag across the card's bottom", async () =>
            {
                foreach (var v in await Crystals())
                {
                    var at = Pt(v, 0, 0.72f);
                    var p = Pane(new Rect2(at - new Vector2(46, 11), new Vector2(92, 22)), spellGreen, 11);
                    Put(p, "CONT. SPELL", Cond, 15, spellGreen, Vector2.Zero, new Vector2(92, 22), HorizontalAlignment.Center).VerticalAlignment = VerticalAlignment.Center;
                }
                await Hold(8);
            }),
            ("B: Spell-green edge round the card", async () =>
            {
                foreach (var v in await Crystals())
                {
                    var q = new[] { Pt(v, -1, -1), Pt(v, 1, -1), Pt(v, 1, 1), Pt(v, -1, 1) };
                    foreach (var (w, a) in new[] { (12f, 0.2f), (6f, 0.4f), (2.5f, 1f) })
                        Tmp(new Line2D { Points = q, Closed = true, Width = w, DefaultColor = new Color(spellGreen, a), JointMode = Line2D.LineJointMode.Round, Antialiased = true });
                }
                await Hold(8);
            }),
            ("C: Hover only: the details type line says it", async () =>
            {
                var d = (await Crystals())[0].Card.Data;
                DpStart(d);
                FitText(DpLine(d.Name, Cond, 32, Colors.White, 40), 32);
                DpLine("CONTINUOUS SPELL  ·  in the Spell & Trap Zone", Ui, 17, spellGreen, 24);
                DpLine($"(monster: {d.Attribute} · {d.Race.ToUpperInvariant()})", Ui, 15, new Color(1, 1, 1, 0.45f), 24);
                DpRule(); DpPara(d.Desc, 15, new Color(1, 1, 1, 0.82f)); DpEnd();
                await Done(8);
            }));

        // ---- N4 Exodius: Forbidden One pieces in your Graveyard (n / 5)
        string[] pieces = { "Exodia the Forbidden One", "Left Arm of the Forbidden One", "Right Arm of the Forbidden One", "Left Leg of the Forbidden One", "Right Leg of the Forbidden One" };
        const int sent = 3;
        async Task<View> Exodius()
        {
            var v = C("Exodius the Ultimate Forbidden Lord");
            Place(v, Mon(v, 2, 0) with { Atk = 3000 });
            for (int i = 0; i < sent; i++) Put(pieces[i], GyAt(0) with { Index = i, Count = sent });
            await Wait(0.5);
            return v;
        }
        Add("4  Exodius: pieces in the Graveyard",
            ("A: \"3/5\" on a gold badge, bottom-right", async () => { var v = await Exodius(); CornerBadge(v, 0.7f, 0.74f, $"{sent}/5", RivalPanels.Gold); await Hold(8); }),
            ("B: Five pips under the ATK / DEF (lit = in the GY)", async () =>
            {
                var v = await Exodius();
                var at = Pt(v, 0, 1.55f);
                for (int i = 0; i < 5; i++)
                {
                    var pip = TmpBox(new Rect2(at + new Vector2((i - 2) * 18 - 7, -7), new Vector2(14, 14)), Pill(i < sent ? RivalPanels.Gold : new Color(0, 0, 0, 0.5f), RivalPanels.Gold, 7, 0, null, 0, 2));
                    pip.MouseFilter = MouseFilterEnum.Ignore;
                }
                await Hold(8);
            }),
            ("C: Hover only: the five pieces in the details, sent ones lit", async () =>
            {
                var d = (await Exodius()).Card.Data;
                DpStart(d); DpHead(d, atk: 3000);
                float s = (DetailW - 40 - 4 * 8) / 5;
                for (int i = 0; i < 5; i++)
                {
                    var t = Pic(dp, Crop(CardDb.Shared.Named(pieces[i])), new Rect2(20 + i * (s + 8), dy + 4, s, s));
                    if (i >= sent) t.Modulate = new Color(0.3f, 0.3f, 0.3f);
                }
                dy += s + 8;
                DpLine($"{sent} / 5 FORBIDDEN ONE IN YOUR GRAVEYARD", Cond, 18, RivalPanels.Gold, 26);
                DpRule(); DpPara(d.Desc, 13, new Color(1, 1, 1, 0.82f)); DpEnd();   // its long text fits at 13
                await Done(8);
            }));

        // ---- N5 Golden Castle of Stromberg: cards banished face-down show their back
        async Task Stromberg()
        {
            Place(C("Golden Castle of Stromberg"), new St(Loc.Field, 0, 0, true, false, 0, 0, 0));
            Put("Vorse Raider", new St(Loc.Banished, 0, 2, false, false, 0, 0, 0));
            Put("Gaia The Fierce Knight", new St(Loc.Banished, 1, 2, false, false, 0, 0, 0));
            await Wait(0.5);
        }
        Add("5  Golden Castle of Stromberg: face-down banished",
            ("A: The banished pile shows the card back", async () => { await Stromberg(); await Hold(8); }),
            ("B: A + a small \"2 FACE-DOWN\" chip on the pile", async () =>
            {
                await Stromberg();
                var at = Pt(C("Gaia The Fierce Knight"), 0, 1.25f);
                var p = Pane(new Rect2(at - new Vector2(52, 11), new Vector2(104, 22)), new Color(1, 1, 1, 0.35f), 11);
                Put(p, "2 FACE-DOWN", Cond, 15, Colors.White, Vector2.Zero, new Vector2(104, 22), HorizontalAlignment.Center).VerticalAlignment = VerticalAlignment.Center;
                await Hold(8);
            }));
    }
}
