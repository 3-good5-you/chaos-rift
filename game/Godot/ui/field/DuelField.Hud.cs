using Godot;
using PoC.Engine;

namespace PoC.Game.UI;

/// 2D overlay: card details (left), profiles + LP and the phase steps (right), prompts, toasts, banners.
public partial class DuelField
{
    Control hud = null!;
    TextureRect detailArt = null!;
    Label detailName = null!, detailType = null!, detailStars = null!, detailStats = null!, detailDesc = null!, detailHint = null!;
    RichTextLabel detailRich = null!;   // the text as shown, keywords marked; detailDesc stays (invisible) as the measure
    readonly Panel[] profile = new Panel[2];
    readonly Label[] lpText = new Label[2];
    readonly ColorRect[] lpFill = new ColorRect[2], lpTrail = new ColorRect[2];
    const float BarW = 212, ProfW = 380, ProfH = 150, DetailW = 380;
    ShaderMaterial vignette = null!;
    Panel detail = null!;
    ColorRect detailRule = null!;
    Panel prompt = null!, toast = null!;
    Label promptTitle = null!, toastText = null!, toastSub = null!;
    Control promptChips = null!;
    int promptToken, toastToken;

    static StyleBoxFlat Glass(Color edge, float glow = 0) => new()
    {
        BgColor = new Color(0.025f, 0.028f, 0.04f, 0.8f), BorderColor = edge,
        BorderWidthTop = 2, BorderWidthBottom = 1, BorderWidthLeft = 1, BorderWidthRight = 1,
        CornerRadiusTopLeft = 18, CornerRadiusTopRight = 18, CornerRadiusBottomLeft = 18, CornerRadiusBottomRight = 18,
        CornerDetail = 8, AntiAliasing = true,
        ShadowColor = glow > 0 ? new Color(edge, glow) : new Color(0, 0, 0, 0.45f), ShadowSize = glow > 0 ? 22 : 18,
    };

    Panel Box(Control parent, Rect2 r, StyleBox style)
    {
        var p = new Panel { Position = r.Position, Size = r.Size, MouseFilter = MouseFilterEnum.Ignore };
        p.AddThemeStyleboxOverride("panel", style);
        parent.AddChild(p);
        return p;
    }

    Label Put(Control parent, string text, Font f, int size, Color c, Vector2 pos, Vector2 box, HorizontalAlignment align = HorizontalAlignment.Left, int outline = 0)
    {
        var l = Text(text, f, size, c, outline, new Color(0, 0, 0, 0.85f));
        l.Position = pos; l.Size = box; l.HorizontalAlignment = align;
        l.SetMeta("box", box.X);   // the label grows to a long text's width; FitText shrinks it back into this
        parent.AddChild(l);
        return l;
    }

    void BuildHud()
    {
        hud = new Control { Size = new Vector2(W, H), MouseFilter = MouseFilterEnum.Ignore, ZIndex = 10 };
        AddChild(hud);
        vignette = Mat(@"shader_type canvas_item;
uniform vec4 col : source_color = vec4(1.0, 0.1, 0.05, 1.0);
uniform float amount = 0.0;
void fragment() { float r = length((UV - 0.5) * vec2(1.6, 1.0)); COLOR = vec4(col.rgb, smoothstep(0.45, 1.0, r) * amount); }");
        hud.AddChild(new ColorRect { Size = new Vector2(W, H), Material = vignette, MouseFilter = MouseFilterEnum.Ignore });

        BuildDetail();
        profile[1] = Profile(1, new Vector2(W - ProfW - 24, 24));
        profile[0] = Profile(0, new Vector2(24, H - ProfH - 24));
        BuildPhase();
        BuildPrompt();
        if (OS.GetCmdlineUserArgs().Contains("--nohud")) hud.Visible = false;   // dev: the bare field, for HUD design samples
    }

    // ---- card details (left)

    void BuildDetail()
    {
        detail = Box(hud, new Rect2(24, 24, DetailW, 200), Glass(RivalPanels.Gold * new Color(1, 1, 1, 0.45f)));
        detail.ClipContents = true;
        float artW = DetailW - 40, w = DetailW - 40;
        detailArt = new TextureRect { ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, Position = new Vector2(20, 20), Size = new Vector2(artW, artW * 1.458f), StretchMode = TextureRect.StretchModeEnum.Scale, TextureFilter = TextureFilterEnum.LinearWithMipmaps };
        detail.AddChild(detailArt);
        float y = 20 + artW * 1.458f + 12;
        detailName = Put(detail, "", Cond, 32, Colors.White, new Vector2(20, y), new Vector2(w, 40));
        detailName.ClipText = true;
        detailType = Put(detail, "", Ui, 17, RivalPanels.Gold, new Vector2(20, y + 40), new Vector2(w, 24));
        detailStars = Put(detail, "", Ui, 18, new Color(1f, 0.7f, 0.25f), new Vector2(20, y + 64), new Vector2(w, 24));
        detailStats = Put(detail, "", Impact, 30, Colors.White, new Vector2(20, y + 88), new Vector2(w, 40), outline: 6);
        detailRule = new ColorRect { Position = new Vector2(20, y + 132), Size = new Vector2(w, 1), Color = RivalPanels.Gold * new Color(1, 1, 1, 0.4f) };
        detail.AddChild(detailRule);
        detailDesc = Put(detail, "", Ui, 17, new Color(1, 1, 1, 0.82f), new Vector2(20, y + 142), new Vector2(w, 40));
        detailDesc.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        detailDesc.SelfModulate = Colors.Transparent;
        detailRich = new RichTextLabel { BbcodeEnabled = true, ScrollActive = false, MouseFilter = MouseFilterEnum.Ignore };
        // one font for normal and bold: the marks are colour only, so the lines break where the measured Label's do
        detailRich.AddThemeFontOverride("normal_font", Ui); detailRich.AddThemeFontOverride("bold_font", Ui);
        detailRich.AddThemeColorOverride("default_color", new Color(1, 1, 1, 0.82f));
        detailRich.AddThemeConstantOverride("line_separation", 3);
        detail.AddChild(detailRich);
        detailHint = Put(detail, "", Cond, 24, new Color(1, 1, 1, 0.25f), Vector2.Zero, Vector2.Zero);
        detail.Modulate = new Color(1, 1, 1, 0);
    }

    void Focus(Card? c)
    {
        if (c == null) return;
        var d = c.Data;
        detailHint.Visible = false;
        detailArt.Texture = CardArt.Large(d);
        detailName.Text = d.Name;
        if (d.IsMonster)
        {
            detailType.Text = $"{d.Attribute}  ·  {d.Race.ToUpperInvariant()} / {d.Frame.ToUpperInvariant()}";
            detailStars.Text = new string('★', Math.Max(0, d.Level));
            detailStats.Text = $"ATK {d.AtkText}    DEF {d.DefText}";
        }
        else
        {
            detailType.Text = $"{d.Kind.ToString().ToUpperInvariant()}  ·  {d.Frame.ToUpperInvariant()}";
            detailStars.Text = "";
            detailStats.Text = "";
        }
        // A kept coin's two lines and a borrowed name / copied effect are laid out apart (DuelField.Marks.cs).
        DetailExtraClear();
        detailDesc.Text = DetailText(c);
        // Monster stats take three lines; spells/traps move the rule and text up. The panel fits its content.
        float y = 20 + (DetailW - 40) * 1.458f + 12;
        float ruleY = (d.IsMonster ? y + 132 : y + 74) + BorrowRows(c, d.IsMonster ? y + 130 : y + 72);
        detailRule.Position = new Vector2(20, ruleY);
        detailDesc.Position = new Vector2(20, ruleY + 10);
        // Long official texts shrink to fit above your profile instead of running off the panel; long names shrink too.
        FitText(detailName, 32);
        float room = H - ProfH - 72 - (ruleY + 10) - 22;
        var font = detailDesc.GetThemeFont("font");
        int fs = 17;
        float Need(int size) => font.GetMultilineStringSize(detailDesc.Text, HorizontalAlignment.Left, DetailW - 40, size).Y * 1.4f + CoinNeed(c, size);   // label lines sit looser than the font measures
        while (fs > 11 && Need(fs) > room) fs--;
        detailDesc.AddThemeFontSizeOverride("font_size", fs);
        float descH = Math.Min(room, Need(fs)), coinH = CoinNeed(c, fs);
        detailDesc.Size = new Vector2(DetailW - 40, descH - coinH);
        detailRich.Text = LabText.Card(detailDesc.Text);
        detailRich.Position = detailDesc.Position; detailRich.Size = detailDesc.Size;
        detailRich.AddThemeFontSizeOverride("normal_font_size", fs); detailRich.AddThemeFontSizeOverride("bold_font_size", fs);
        CoinRows(c, ruleY + 10 + descH - coinH, fs);
        float h = Math.Min(H - ProfH - 72, ruleY + 10 + descH + 22);
        if (detail.Modulate.A < 0.99f) { detail.Size = new Vector2(DetailW, h); CreateTween().TweenProperty(detail, "modulate:a", 1f, 0.2); }
        else CreateTween().TweenProperty(detail, "size:y", h, 0.2).SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
        detailArt.Modulate = new Color(1, 1, 1, 0.3f);
        CreateTween().TweenProperty(detailArt, "modulate:a", 1f, 0.18);
    }

    // ---- profiles + LP (right)

    Panel Profile(int side, Vector2 at)
    {
        var c = side == 0 ? Pm.You : Pm.Them;
        var p = Box(hud, new Rect2(at, new Vector2(ProfW, ProfH)), Glass(c * new Color(1, 1, 1, 0.4f)));
        // The round picture in its icon frame: yours as equipped, the rival's (a face crop of the portrait) in the classic one.
        var framed = Cosmetics.Framed(side == 0 ? Settings.Avatar : Lan.On ? Lan.TheirAvatar : null, side == 0 ? Session.Profile.Frame : Lan.On ? Lan.TheirFrame : Shop.DefaultFrame, ProfH);
        framed.Position = new Vector2(2, 0);
        if (side == 1 && !Lan.On && Watch is not { Rival: null })   // a LAN replay: no rival portrait
        {
            var pic = (TextureRect)framed.GetChild(1);
            var t = RivalPanels.Portrait(Rival);
            pic.Texture = t; pic.StretchMode = TextureRect.StretchModeEnum.Scale;
            pic.Material = Mat(@"shader_type canvas_item;
uniform vec4 region;
void fragment() {
  float m = 1.0 - smoothstep(0.49, 0.5, length(UV - 0.5));
  vec4 c = texture(TEXTURE, region.xy + UV * region.zw);
  COLOR = vec4(c.rgb, c.a * m);
}", ("region", RivalPanels.Face(Rival)));
        }
        p.AddChild(framed);

        float x = 150;
        Put(p, side == 0 ? Settings.PlayerName.ToUpperInvariant() : ThemName, Impact, 34, c, new Vector2(x, 12), new Vector2(ProfW - x - 16, 44), outline: 6);
        Put(p, "LP", Cond, 22, RivalPanels.Gold, new Vector2(x + 2, 80), new Vector2(40, 28));
        lpText[side] = Put(p, "8000", Impact, 56, Colors.White, new Vector2(x + 20, 44), new Vector2(ProfW - x - 38, 74), HorizontalAlignment.Right, 8);
        var bar = Box(p, new Rect2(x, 122, BarW, 12), new StyleBoxFlat { BgColor = new Color(0.1f, 0.1f, 0.13f, 1), CornerRadiusTopLeft = 6, CornerRadiusTopRight = 6, CornerRadiusBottomLeft = 6, CornerRadiusBottomRight = 6 });
        bar.ClipChildren = ClipChildrenMode.AndDraw;
        lpTrail[side] = new ColorRect { Size = new Vector2(BarW, 12), Color = new Color(1f, 0.95f, 0.85f, 0.9f), MouseFilter = MouseFilterEnum.Ignore };
        lpFill[side] = new ColorRect
        {
            Size = new Vector2(BarW, 12), MouseFilter = MouseFilterEnum.Ignore,
            Material = Mat(@"shader_type canvas_item;
uniform vec4 col : source_color;
void fragment() { vec3 c = mix(col.rgb * 0.7, col.rgb * 1.25, UV.x) + vec3(0.25) * smoothstep(0.5, 0.0, UV.y); COLOR = vec4(c, 1.0); }", ("col", c)),
        };
        bar.AddChild(lpTrail[side]); bar.AddChild(lpFill[side]);
        return p;
    }

    void SetLp(int side, int lp, bool anim)
    {
        lpText[side].Text = lp.ToString();
        float w = BarW * Math.Clamp(lp / 8000f, 0f, 1f);
        lpFill[side].Size = new Vector2(w, 12);
        if (!anim) lpTrail[side].Size = new Vector2(w, 12);
    }

    void LpHit(int side, int from, int to)
    {
        Cue(to > from ? "lp_up" : side == 0 ? "lp_down_you" : "lp_down_them");
        float w = BarW * Math.Clamp(to / 8000f, 0f, 1f);
        var tw = CreateTween();
        tw.TweenMethod(Callable.From<int>(x => lpText[side].Text = x.ToString()), from, to, 0.7).SetDelay(0.1);
        CreateTween().TweenProperty(lpFill[side], "size:x", w, 0.3).SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
        CreateTween().TweenProperty(lpTrail[side], "size:x", w, 0.6).SetDelay(0.55).SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.InOut);
        bool gain = to > from;
        var col = gain ? new Color(0.5f, 1f, 0.6f) : new Color(1f, 0.38f, 0.3f);
        var at = profile[side].Position + new Vector2(side == 0 ? ProfW + 150 : 260, side == 0 ? ProfH / 2 : ProfH + 30);
        var l = Put(hud, (gain ? "+" : "−") + Math.Abs(to - from), Impact, 64, col, at - new Vector2(150, 40), new Vector2(300, 80), HorizontalAlignment.Center, 10);
        l.PivotOffset = new Vector2(150, 40);
        l.Scale = new Vector2(1.5f, 1.5f);
        var ft = CreateTween().SetParallel();
        ft.TweenProperty(l, "scale", Vector2.One, 0.3).SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
        ft.TweenProperty(l, "position:y", l.Position.Y + (side == 0 ? -70 : 60), 1.2).SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
        ft.TweenProperty(l, "modulate:a", 0f, 0.5).SetDelay(0.8);
        ft.Chain().TweenCallback(Callable.From(l.QueueFree));
        // Panel jolt, and a red edge pulse when you are hit.
        var home = profile[side].Position;
        var jt = CreateTween();
        for (int i = 0; i < 5; i++) jt.TweenProperty(profile[side], "position", home + new Vector2(i % 2 == 0 ? 8 : -8, 0) * (1 - i / 5f), 0.04);
        jt.TweenProperty(profile[side], "position", home, 0.04);
        if (side == 0 && !gain)
        {
            var vt = CreateTween();
            vt.TweenMethod(Callable.From<float>(a => vignette.SetShaderParameter("amount", a)), 0f, 0.6f, 0.08);
            vt.TweenMethod(Callable.From<float>(a => vignette.SetShaderParameter("amount", a)), 0.6f, 0f, 0.8);
        }
    }

    // ---- prompts, chain toast, banner

    void BuildPrompt()
    {
        prompt = Box(hud, new Rect2(W / 2 - 380, H * 0.4f, 760, 150), Glass(RivalPanels.Gold * new Color(1, 1, 1, 0.8f), 0.25f));
        prompt.Modulate = new Color(1, 1, 1, 0);
        promptTitle = Put(prompt, "", Cond, 30, Colors.White, new Vector2(0, 16), new Vector2(760, 40), HorizontalAlignment.Center);
        promptChips = new Control { Position = new Vector2(30, 76), Size = new Vector2(700, 50), MouseFilter = MouseFilterEnum.Ignore };
        prompt.AddChild(promptChips);

        toast = Box(hud, new Rect2(W / 2 - 330, 24, 660, 66), Glass(RivalPanels.Gold * new Color(1, 1, 1, 0.6f)));
        toast.Modulate = new Color(1, 1, 1, 0);
        toastText = Put(toast, "", Cond, 28, Colors.White, new Vector2(24, 14), new Vector2(480, 40));
        toastText.ClipText = true;
        toastSub = Put(toast, "", Impact, 28, RivalPanels.Gold, new Vector2(456, 12), new Vector2(180, 40), HorizontalAlignment.Right);
    }

    async Task AskYou(Prompt p, Command cmd)
    {
        promptToken++;
        foreach (var c in promptChips.GetChildren()) c.QueueFree();
        promptTitle.Text = p.Kind == Ask.Respond ? $"CHAIN TO {What(D.Window)}?" : p.Text.ToUpperInvariant();
        FitText(promptTitle, 30);
        var chips = new List<Panel>();
        int n = Math.Min(p.Options.Count, 4);
        float w = (700 - (n - 1) * 12) / n;
        for (int i = 0; i < n; i++)
        {
            var o = p.Options[i];
            var label = o.Act == Act.Pass ? "PASS" : o.Card?.Name ?? o.Text ?? o.ToString();
            var chip = Chips.Make(label, new Vector2(i * (w + 12), 0), new Vector2(w, 46), () => { }, 20);
            chip.MouseFilter = MouseFilterEnum.Ignore;
            promptChips.AddChild(chip);
            chips.Add(chip);
        }
        prompt.Scale = new Vector2(0.92f, 0.92f);
        prompt.PivotOffset = prompt.Size / 2;
        var tw = CreateTween().SetParallel();
        tw.TweenProperty(prompt, "modulate:a", 1f, 0.18);
        tw.TweenProperty(prompt, "scale", Vector2.One, 0.25).SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
        await Wait(1.0);
        foreach (var i in cmd.Picks) if (i < chips.Count) Chips.SetOn(chips[i], true);
        await Wait(0.5);
        CreateTween().TweenProperty(prompt, "modulate:a", 0f, 0.18);
        await Wait(0.2);
    }

    static string What(Ev? e) => e?.Kind switch
    {
        EvKind.Summoned => "THE SUMMON",
        EvKind.AttackDeclared => "THE ATTACK",
        EvKind.Activated => e.Card?.Name.ToUpperInvariant() ?? "THE ACTIVATION",
        EvKind.Set => "THE SET",
        EvKind.Destroyed => "THE DESTRUCTION",
        EvKind.Damage => "THE DAMAGE",
        EvKind.MainStart => "MAIN PHASE",
        EvKind.BattleStart => "BATTLE PHASE",
        EvKind.EndPhase => "END PHASE",
        EvKind.Standby => "STANDBY PHASE",
        EvKind.Drew => "THE DRAW",
        _ => "THIS",
    };

    /// Cards shown to both players for a moment (discards, reveals): a row of faces under a title.
    void ShowCards(string title, List<Card> cards, double secs)
    {
        const float cw = 120, ch = 175, gap = 12;
        int n = Math.Min(cards.Count, 8);
        float w = Math.Max(420, n * (cw + gap) + 36 - gap), h = ch + 82;
        var p = Box(hud, new Rect2(W / 2 - w / 2, H * 0.4f - h / 2, w, h), Glass(RivalPanels.Gold * new Color(1, 1, 1, 0.8f), 0.25f));
        Put(p, title, Cond, 24, Colors.White, new Vector2(16, 12), new Vector2(w - 32, 32), HorizontalAlignment.Center);
        float x0 = w / 2 - (n * (cw + gap) - gap) / 2;
        for (int i = 0; i < n; i++)
            p.AddChild(new TextureRect { Texture = CardArt.Small(cards[i].Data), ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.Scale, Size = new Vector2(cw, ch), Position = new Vector2(x0 + i * (cw + gap), 58), MouseFilter = MouseFilterEnum.Ignore });
        p.Modulate = new Color(1, 1, 1, 0);
        var tw = CreateTween();
        tw.TweenProperty(p, "modulate:a", 1f, 0.2);
        tw.TweenInterval(secs);
        tw.TweenProperty(p, "modulate:a", 0f, 0.3);
        tw.TweenCallback(Callable.From(p.QueueFree));
    }

    /// Shrinks a one-line label's font until its text fits its width (long card names, long prompts).
    static void FitText(Label l, int max, int min = 16)
    {
        var f = l.GetThemeFont("font");
        float w = l.HasMeta("box") ? (float)l.GetMeta("box") : l.Size.X;
        int size = max;
        while (size > min && f.GetStringSize(l.Text, HorizontalAlignment.Left, -1, size).X > w) size--;
        l.AddThemeFontSizeOverride("font_size", size);
        l.Size = new Vector2(w, l.Size.Y);   // back to its box (centred text centres again)
    }

    async void Toast(string text, string sub)
    {
        int token = ++toastToken;
        toastText.Text = text.ToUpperInvariant();
        FitText(toastText, 28);
        toastSub.Text = sub;
        toast.Position = new Vector2(toast.Position.X, 4);
        var tw = CreateTween().SetParallel();
        tw.TweenProperty(toast, "modulate:a", 1f, 0.2);
        tw.TweenProperty(toast, "position:y", 24f, 0.25).SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
        await Wait(1.6);
        if (token == toastToken) CreateTween().TweenProperty(toast, "modulate:a", 0f, 0.3);
    }

    /// Turn / result banner: a slanted band sweeping across the middle of the screen.
    async Task Banner(string text, Color color, string sub)
    {
        var band = new ColorRect
        {
            Size = new Vector2(W + 400, 190), Position = new Vector2(-200, H / 2 - 95), MouseFilter = MouseFilterEnum.Ignore, Rotation = -0.035f, PivotOffset = new Vector2((W + 400) / 2, 95),
            Material = Mat(@"shader_type canvas_item;
uniform vec4 col : source_color;
uniform float t = 0.0;
void fragment() {
  float edge = smoothstep(0.0, 0.25, UV.x) * smoothstep(1.0, 0.75, UV.x);
  float lines = exp(-abs(UV.y - 0.03) * 200.0) + exp(-abs(UV.y - 0.97) * 200.0);
  float sweep = exp(-pow((UV.x - t) * 6.0, 2.0)) * 0.35;
  vec3 c = mix(vec3(0.02, 0.02, 0.03), col.rgb * 0.35, 0.5 - abs(UV.y - 0.5)) + col.rgb * (lines + sweep);
  COLOR = vec4(c, (0.82 + lines) * edge);
}", ("col", color)),
        };
        band.Scale = new Vector2(1, 0);
        var l = Put(band, text, Impact, 104, color, new Vector2(-260, 18), new Vector2(W + 400, 130), HorizontalAlignment.Center, 14);
        var s = Put(band, sub, Cond, 26, new Color(1, 1, 1, 0.7f), new Vector2(0, 140), new Vector2(W + 400, 32), HorizontalAlignment.Center);
        hud.AddChild(band);
        var m = (ShaderMaterial)band.Material;
        var tw = CreateTween().SetParallel();
        tw.TweenProperty(band, "scale:y", 1f, 0.22).SetTrans(Tween.TransitionType.Expo).SetEase(Tween.EaseType.Out);
        tw.TweenProperty(l, "position:x", 0f, 0.45).SetTrans(Tween.TransitionType.Expo).SetEase(Tween.EaseType.Out);
        tw.TweenMethod(Callable.From<float>(x => m.SetShaderParameter("t", x)), -0.2f, 1.2f, 1.1);
        tw.Chain().TweenInterval(0.45);
        tw.Chain().TweenProperty(band, "scale:y", 0f, 0.2).SetTrans(Tween.TransitionType.Expo).SetEase(Tween.EaseType.In);
        tw.Parallel().TweenProperty(l, "position:x", 260f, 0.2).SetTrans(Tween.TransitionType.Expo).SetEase(Tween.EaseType.In);
        tw.Chain().TweenCallback(Callable.From(band.QueueFree));
        await Wait(1.4);
    }
}
