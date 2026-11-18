using Godot;
using PoC.Engine;

namespace PoC.Game.UI;

/// The NEW mark on a newly won card (Unlock Lab pick 2026-11-16: LIFT + REVERSE SWELL + CRIMSON TAB). Reveal: the
/// face-down card lifts toward you and glows, turns over and drops back (the sound's hit on the turn), then a big NEW
/// band sits across it for a moment and collapses into the crimson corner tab. The tab stays on the card in the
/// Card List / Deck Builder until you look at the card.
public static class NewMark
{
    public static readonly Color Crimson = new(1f, 0.23f, 0.29f);
    const double Hit = 0.5, Hold = 2.2;   // Hit: the turn, on the reverse swell's thud
    static Font? bold;
    static Font Bold => bold ??= Fonts.Get(new[] { "Bahnschrift", "Segoe UI" }, 700, true);

    static void Play(Node owner, string name, float db)
    {
        var p = new AudioStreamPlayer { Bus = "Sound", VolumeDb = db, Stream = GD.Load<AudioStream>($"res://assets/audio/gen/{name}.wav") };
        owner.AddChild(p); p.Play();
        p.Finished += p.QueueFree;
    }

    /// Reveals a face-down card (`face` shows the back now, `art` after the turn); centre pivot on `card`.
    public static void Reveal(Control card, TextureRect face, Texture2D art)
    {
        Play(card, "U_reverse_swell", -2);
        var host = card.GetParent<Control>();
        var sb = new StyleBoxFlat { BgColor = Crimson with { A = 0.9f }, ShadowColor = Crimson with { A = 0.7f }, ShadowSize = (int)(card.Size.Y * 0.11f) };
        sb.SetCornerRadiusAll(6);
        var aura = new Panel { Position = card.Position, Size = card.Size, PivotOffset = card.Size / 2, Modulate = new Color(1, 1, 1, 0), MouseFilter = Control.MouseFilterEnum.Ignore };
        aura.AddThemeStyleboxOverride("panel", sb);
        host.AddChild(aura);
        host.MoveChild(aura, card.GetIndex());   // behind the card
        float y0 = card.Position.Y, lift = card.Size.Y * 0.06f;
        var t = card.CreateTween().SetParallel();
        foreach (var c in new Control[] { card, aura })
        {
            t.TweenProperty(c, "scale", new Vector2(1.08f, 1.08f), Hit - 0.05).SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.Out);
            t.TweenProperty(c, "position:y", y0 - lift, Hit - 0.05).SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.Out);
        }
        t.TweenProperty(aura, "modulate:a", 1f, Hit - 0.05);
        var f = card.CreateTween();
        f.TweenInterval(Hit - 0.06);
        f.TweenProperty(card, "scale:x", 0f, 0.06);
        f.TweenCallback(Callable.From(() => face.Texture = art));
        f.TweenProperty(card, "scale:x", 1.08f, 0.06);
        f.TweenInterval(0.02);
        f.TweenCallback(Callable.From(() =>
        {
            var d = card.CreateTween().SetParallel();
            d.TweenProperty(card, "scale", Vector2.One, 0.3).SetTrans(Tween.TransitionType.Expo).SetEase(Tween.EaseType.Out);
            d.TweenProperty(card, "position:y", y0, 0.3).SetTrans(Tween.TransitionType.Expo).SetEase(Tween.EaseType.Out);
            d.TweenProperty(aura, "position:y", y0, 0.3).SetTrans(Tween.TransitionType.Expo).SetEase(Tween.EaseType.Out);
            d.TweenProperty(aura, "scale", Vector2.One, 0.3);
            d.TweenProperty(aura, "modulate:a", 0f, 0.8).SetDelay(0.1);
            d.Chain().TweenCallback(Callable.From(aura.QueueFree));
            Sheen(card);
        }));
        f.TweenInterval(0.06);
        f.TweenCallback(Callable.From(() => Across(card)));
    }

    /// The big NEW across the card: opens out from the middle, holds, collapses into the corner tab.
    static void Across(Control card)
    {
        float w = card.Size.X, h = card.Size.Y, bh = Math.Max(40, h * 0.215f);
        var band = new Control { Position = new Vector2(-w * 0.1f, h * 0.42f - bh / 2), Size = new Vector2(w * 1.2f, bh), MouseFilter = Control.MouseFilterEnum.Ignore };
        band.PivotOffset = band.Size / 2;
        card.AddChild(band);
        Strip(band, Vector2.Zero, band.Size, new Color(0, 0, 0, 0.8f));
        Strip(band, Vector2.Zero, new Vector2(band.Size.X, 2), Crimson);
        Strip(band, new Vector2(0, bh - 2), new Vector2(band.Size.X, 2), Crimson);
        var l = Word(band, Colors.White, band.Size, bh * 0.67f);
        l.AddThemeConstantOverride("outline_size", (int)(bh * 0.14f)); l.AddThemeColorOverride("font_outline_color", Crimson with { A = 0.45f });
        var clip = new Control { Size = band.Size, ClipContents = true, MouseFilter = Control.MouseFilterEnum.Ignore };
        var glint = new ColorRect { Size = new Vector2(bh * 0.45f, bh), Position = new Vector2(-bh, 0), Color = new Color(1, 1, 1, 0.22f), MouseFilter = Control.MouseFilterEnum.Ignore };
        band.AddChild(clip); clip.AddChild(glint);
        band.Scale = new Vector2(0.05f, 1); band.Modulate = new Color(1, 1, 1, 0);
        var t = card.CreateTween().SetParallel();
        t.TweenProperty(band, "scale:x", 1f, 0.3).SetTrans(Tween.TransitionType.Expo).SetEase(Tween.EaseType.Out);
        t.TweenProperty(band, "modulate:a", 1f, 0.12);
        t.TweenProperty(glint, "position:x", band.Size.X + bh, 0.45).SetDelay(0.35).SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.InOut);
        var c = card.CreateTween();
        c.TweenInterval(Hold);
        c.TweenProperty(band, "position", new Vector2(w - band.Size.X / 2 - w * 0.1f, h * 0.045f - bh / 2), 0.28).SetTrans(Tween.TransitionType.Expo).SetEase(Tween.EaseType.In);
        c.Parallel().TweenProperty(band, "scale", new Vector2(0.22f, 0.3f), 0.28).SetTrans(Tween.TransitionType.Expo).SetEase(Tween.EaseType.In);
        c.Parallel().TweenProperty(band, "modulate:a", 0f, 0.28).SetTrans(Tween.TransitionType.Expo).SetEase(Tween.EaseType.In);
        c.TweenCallback(Callable.From(() => { band.QueueFree(); Tag(card, card.Size, fresh: true); }));
    }

    /// The crimson tab hanging over the top-right edge of a card of `size` drawn in `host`; a shine runs over it now
    /// and then. `seen` set: hovering `host` clears the tab and calls it (once).
    public static Control Tag(Control host, Vector2 size, bool fresh = false, Action? seen = null)
    {
        float k = Math.Clamp(size.Y / 400f, 0.55f, 1f), tw = 70 * k, th = 26 * k, sk = 8 * k;
        var root = new Control { Position = new Vector2(size.X - tw - 6 * k, -th * 0.35f), MouseFilter = Control.MouseFilterEnum.Ignore };
        host.AddChild(root);
        root.AddChild(new Polygon2D { Polygon = new[] { new Vector2(sk, 0), new Vector2(tw, 0), new Vector2(tw - sk, th), new Vector2(0, th) },
            VertexColors = new[] { Crimson.Lightened(0.2f), Crimson.Lightened(0.2f), Crimson.Darkened(0.35f), Crimson.Darkened(0.35f) } });
        var clip = new Control { Size = new Vector2(tw, th), ClipContents = true, MouseFilter = Control.MouseFilterEnum.Ignore };
        var shine = new Polygon2D { Polygon = new[] { new Vector2(sk, 0), new Vector2(sk + 10 * k, 0), new Vector2(10 * k, th), new Vector2(0, th) }, Color = new Color(1, 1, 1, 0.55f), Position = new Vector2(-20 * k, 0) };
        root.AddChild(clip); clip.AddChild(shine);
        var s = root.CreateTween().SetLoops();
        s.TweenInterval(2.0 + GD.Randf() * 0.8);   // the tabs in a grid don't all shine at once
        s.TweenProperty(shine, "position:x", tw + 4, 0.4);
        s.TweenCallback(Callable.From(() => shine.Position = new Vector2(-20 * k, 0)));
        Word(root, Colors.White, new Vector2(tw, th), 16 * k);
        if (fresh)
        {
            root.Modulate = new Color(1, 1, 1, 0); root.Scale = new Vector2(1.6f, 1.6f);
            var t = root.CreateTween().SetParallel();
            t.TweenProperty(root, "modulate:a", 1f, 0.15);
            t.TweenProperty(root, "scale", Vector2.One, 0.25).SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
        }
        if (seen != null)
        {
            bool gone = false;
            host.MouseEntered += () =>
            {
                if (gone || !GodotObject.IsInstanceValid(root)) return;
                gone = true;
                Play(root.GetParent(), "U_seen", -10);
                root.CreateTween().TweenProperty(root, "modulate:a", 0f, 0.2);
                seen();
            };
        }
        return root;
    }

    static Label Word(Control host, Color c, Vector2 box, float size)
    {
        var l = Kit.Put(host, "NEW", Bold, (int)size, c, Vector2.Zero, box, HorizontalAlignment.Center);
        l.VerticalAlignment = VerticalAlignment.Center;
        l.MouseFilter = Control.MouseFilterEnum.Ignore;
        return l;
    }

    /// A horizontal strip whose ends fade out.
    static void Strip(Control host, Vector2 pos, Vector2 size, Color c)
    {
        var g = new Gradient { Offsets = new[] { 0f, 0.18f, 0.82f, 1f }, Colors = new[] { c with { A = 0 }, c, c, c with { A = 0 } } };
        host.AddChild(new TextureRect { Texture = new GradientTexture2D { Gradient = g, Width = 256, Height = 4 }, Position = pos, Size = size,
            StretchMode = TextureRect.StretchModeEnum.Scale, ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, MouseFilter = Control.MouseFilterEnum.Ignore });
    }

    static Shader? sheen;
    static void Sheen(Control card)
    {
        var o = new Control { Size = card.Size, ClipContents = true, MouseFilter = Control.MouseFilterEnum.Ignore };
        card.AddChild(o);
        var m = new ShaderMaterial { Shader = sheen ??= new Shader { Code = @"shader_type canvas_item;
render_mode blend_add;
uniform float p = -1.0;
void fragment() { float d = (UV.x * 0.8 + UV.y * 0.5) / 1.3 - p; COLOR = vec4(vec3(1.0), exp(-d * d * 220.0) * 0.45); }" } };
        o.AddChild(new ColorRect { Size = card.Size, Material = m, MouseFilter = Control.MouseFilterEnum.Ignore });
        var t = o.CreateTween();
        t.TweenInterval(0.12);
        t.TweenMethod(Callable.From<float>(v => m.SetShaderParameter("p", v)), -0.3f, 1.3f, 0.5).SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.InOut);
        t.TweenCallback(Callable.From(o.QueueFree));
    }
}
