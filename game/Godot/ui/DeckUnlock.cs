using Godot;
using PoC.Engine;

namespace PoC.Game.UI;

/// First win against a rival (2026-11-09): their starter deck is yours. After the results (rewards paid there): a sealed
/// stack of your card backs on the rift shakes, the seal bursts, the deck's best cards fan out and flip, then the
/// deck's name is written in. USE THIS DECK sets it as your duel deck; CONTINUE goes where the Results button pointed.
/// `-- --demo[=kaiba]` shows it without a duel (screenshots).
public partial class DeckUnlock : RiftMenu
{
    Deck deck = null!;
    readonly List<Control> fanned = new();
    bool shown;

    protected override string? MusicPath => null;   // the win jingle has played; the reveal has its own sounds
    protected override void OnBack() => Next();

    protected override void Build()
    {
        foreach (var a in OS.GetCmdlineUserArgs())
            if (a.StartsWith("--demo"))
            {
                var key = a.Contains('=') ? a[(a.IndexOf('=') + 1)..] : "kaiba";
                int k = Array.FindIndex(DeckRules.Starters, s => s.Key.StartsWith(key + "/"));
                var (m, x) = Db.Deck(DeckRules.Starters[Math.Max(0, k)].Key);
                Session.LastStarter = new Deck(DeckRules.Starters[Math.Max(0, k)].Name, m, x);
                Session.Selected = Math.Max(0, Array.FindIndex(RivalPanels.All, r => r.File == key));
            }
        if (Session.LastStarter == null) { Next(); return; }
        deck = Session.LastStarter;
        Session.LastStarter = null;   // shown once: no second reveal, no second reward
        Rift(0.5f, 0.5f);
        bg.P("focus_y", 0.5f);
        var rival = RivalPanels.All[Math.Clamp(Session.Selected, 0, RivalPanels.All.Length - 1)];
        T(this, $"{rival.Name} DEFEATED", Thin, 26, new Vector2(0, 70), new Vector2(W, 34), HorizontalAlignment.Center);
        Stack();
    }

    /// The deck's best cards to show: its cover (or ace) first, then the strongest monsters, then a spell or two.
    List<CardData> Showcase()
    {
        var cards = deck.Main.Distinct().Select(Db.Get).ToList();
        var mons = cards.Where(c => c.IsMonster).OrderByDescending(c => c.Atk + c.Level * 50).ToList();
        var rest = cards.Where(c => !c.IsMonster).Take(2);
        var list = mons.Take(6).Concat(rest).Take(7).ToList();
        if (deck.Cover >= 0 && Db.Get(deck.Cover) is { } cv) { list.Remove(cv); list.Insert(0, cv); }
        // the ace in the middle of the fan
        var ace = list[0]; list.RemoveAt(0); list.Insert(list.Count / 2, ace);
        return list;
    }

    void Stack()
    {
        var back = Cosmetics.Back(P.Back, 512);
        float h = 420, w = h * 0.686f;
        var c = new Vector2(W / 2, H * 0.47f);
        var pile = new Control { Position = c - new Vector2(w, h) / 2, Size = new Vector2(w, h), PivotOffset = new Vector2(w, h) / 2, MouseFilter = MouseFilterEnum.Ignore };
        AddChild(pile);
        Shadow(pile, Vector2.Zero, new Vector2(w, h), 0.6f, new Color(0, 0, 0), new Vector2(0, 30));
        for (int i = 5; i >= 0; i--) Pic(pile, back, new Vector2(i * 3, -i * 5), new Vector2(w, h));
        // a gold seal band across the stack
        var band = new ColorRect { Position = new Vector2(-10, h / 2 - 18), Size = new Vector2(w + 20, 36), Color = GoldC, MouseFilter = MouseFilterEnum.Ignore };
        pile.AddChild(band);
        Pip(pile, new Vector2(w / 2, h / 2), 44);
        pile.Scale = new Vector2(0.6f, 0.6f); pile.Modulate = new Color(1, 1, 1, 0);
        var tw = CreateTween();
        tw.TweenProperty(pile, "modulate:a", 1f, 0.4);
        tw.Parallel().TweenProperty(pile, "scale", Vector2.One, 0.6).SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
        // it trembles, then the seal bursts
        tw.TweenInterval(0.5);
        for (int i = 0; i < 8; i++) tw.TweenProperty(pile, "rotation", (i % 2 == 0 ? 1 : -1) * 0.012f * (i + 2), 0.05);
        tw.TweenCallback(Callable.From(() =>
        {
            Sound("res://assets/audio/md/se/SE_CARD_FINISH_BREAK_01.wav", -3);
            CreateTween().TweenMethod(Callable.From<float>(v => bg.P("burst", v)), 1f, 0f, 1.2).SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
            var flash = new ColorRect { Size = new Vector2(W, H), Color = new Color(1, 0.95f, 0.85f, 0.9f), MouseFilter = MouseFilterEnum.Ignore };
            AddChild(flash);
            CreateTween().TweenProperty(flash, "modulate:a", 0f, 0.5);
            pile.QueueFree();
            Fan();
        }));
    }

    void Fan()
    {
        var cards = Showcase();
        int n = cards.Count;
        var pivot = new Vector2(W / 2, H * 1.75f);
        float radius = H * 1.3f;
        for (int i = 0; i < n; i++)
        {
            float a = (i - (n - 1) / 2f) * 0.085f;
            bool ace = i == n / 2;
            var at = pivot + Vector2.FromAngle(-Mathf.Pi / 2 + a) * (radius + (ace ? 40 : 0));
            float h = ace ? 400 : 340;
            var card = Card(this, cards[i], new Vector2(W / 2, H * 0.47f), h, 0, 1f, ace);
            card.ZIndex = ace ? 2 : 1;
            var face = card.GetChildren().OfType<TextureRect>().Last();
            var real = face.Texture;
            face.Texture = Cosmetics.Back(P.Back, 512);
            card.Scale = new Vector2(0.5f, 0.5f);
            double d = 0.1 + Math.Abs(i - (n - 1) / 2f) * 0.07;
            var tw = CreateTween().SetParallel();
            tw.TweenProperty(card, "position", at - card.Size / 2, 0.7).SetDelay(d).SetTrans(Tween.TransitionType.Expo).SetEase(Tween.EaseType.Out);
            tw.TweenProperty(card, "rotation", a, 0.7).SetDelay(d).SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
            tw.TweenProperty(card, "scale", Vector2.One, 0.7).SetDelay(d).SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
            // flip face up, left to right
            var flip = CreateTween();
            flip.TweenInterval(1.0 + i * 0.12);
            flip.TweenProperty(card, "scale:x", 0f, 0.1);
            flip.TweenCallback(Callable.From(() => { face.Texture = real; Sound("res://assets/audio/md/se/SE_MENU_SELECT_01.wav", -10); }));
            flip.TweenProperty(card, "scale:x", 1f, 0.12);
            fanned.Add(card);
        }
        GetTree().CreateTimer(1.2 + n * 0.12 + 0.3).Timeout += Reveal;
    }

    void Reveal()
    {
        if (shown) return;
        shown = true;
        Sound("res://assets/audio/gen/C_whoosh_gate.wav", -6);
        float y = H * 0.67f;
        var head = T(this, "NEW DECK UNLOCKED", Title, 64, new Vector2(0, y), new Vector2(W, 80), HorizontalAlignment.Center);
        head.Mat().P("lit", 1f);
        var name = T(this, deck.Name.ToUpperInvariant(), Title, 40, new Vector2(0, y + 92), new Vector2(W, 50), HorizontalAlignment.Center);
        float nw = Title.GetStringSize(deck.Name.ToUpperInvariant(), HorizontalAlignment.Left, -1, 40).X;
        Pip(this, new Vector2(W / 2 - nw / 2 - 34, y + 118), 11); Pip(this, new Vector2(W / 2 + nw / 2 + 34, y + 118), 11);
        T(this, $"{deck.Main.Count} CARDS  ·  ADDED TO YOUR DECKS", Thin, 22, new Vector2(0, y + 154), new Vector2(W, 30), HorizontalAlignment.Center, 0.75f);
        var b1 = Button(this, "USE THIS DECK", new Vector2(W / 2 - 230, H - 110), Use, 28);
        var b2 = Button(this, "CONTINUE", new Vector2(W / 2 + 230, H - 110), Next, 28);
        foreach (var c in new Control[] { head, name, b1, b2 })
        {
            c.Modulate = new Color(1, 1, 1, 0);
            CreateTween().TweenProperty(c, "modulate:a", 1f, 0.4).SetDelay(c == head ? 0 : 0.3);
        }
    }

    void Use()
    {
        var mine = Session.Decks.FirstOrDefault(d => d.Name == deck.Name) ?? deck;
        Session.Playing = mine;
        Session.Save();
        Flash("IN USE FOR YOUR NEXT DUEL");
        GetTree().CreateTimer(0.9).Timeout += Next;
    }

    bool leaving;
    void Next()
    {
        if (leaving) return;
        leaving = true;
        // Rewards were paid on the Results screen before this: never back there (it would deal a duel's rewards again).
        var to = Session.AfterUnlock ?? "res://ui/MainMenu.tscn";
        Session.AfterUnlock = null;
        Transition.Go(this, to);
    }

    void Sound(string path, float db)
    {
        if (Shooting) return;
        var p = new AudioStreamPlayer { Stream = GD.Load<AudioStream>(path), VolumeDb = db, Bus = "Sound", Autoplay = true };
        p.Finished += p.QueueFree;
        AddChild(p);
    }
}
