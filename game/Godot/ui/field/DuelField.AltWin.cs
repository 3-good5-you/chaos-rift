using Godot;
using PoC.Engine;

namespace PoC.Game.UI;

/// Special wins besides Exodia (2026-11-09): Destiny Board, Exodius, Vennominaga, Flying Elephant, Last Turn,
/// Relay Soul, deck out. The cards that won rise from their zones to the middle, big; Destiny Board writes
/// F-I-N-A-L over its five cards one letter at a time; then the loser's field shatters, as after Exodia.
/// `-- --altwin=Destiny Board` (or any reason) plays one on the lab/demo board for screenshots.
public partial class DuelField
{
    static readonly string[] Letters = { "Destiny Board", "Spirit Message \"I\"", "Spirit Message \"N\"", "Spirit Message \"A\"", "Spirit Message \"L\"" };

    static bool IsAltWin(string reason) => reason is "Destiny Board" or "Exodius" or "Vennominaga" or "Flying Elephant" or "Last Turn" or "Relay Soul" or "deck out";

    /// The line under the cards: what happened, in the game's words.
    static string AltLine(string reason) => reason switch
    {
        "Destiny Board" => "THE MESSAGE IS COMPLETE",
        "Exodius" => "THE FIVE FORBIDDEN PIECES RETURN",
        "Vennominaga" => "THREE HYMNS OF THE SERPENT",
        "Flying Elephant" => "AN ELEPHANT TAKES FLIGHT",
        "Last Turn" => "THE LAST ONE STANDING",
        "Relay Soul" => "THE SOUL WAS NOT PASSED ON",
        "deck out" => "NO CARDS LEFT TO DRAW",
        _ => reason.ToUpperInvariant(),
    };

    static string AltCard(string reason) => reason == "Vennominaga" ? "Vennominaga the Deity of Poisonous Snakes" : reason;

    async Task AltWinFinish(int side, string reason, int winner, List<Card>? given = null)
    {
        // The cards that won it, on the winner's field (or the loser's empty deck for a deck out).
        List<Card> cards = given ?? reason switch
        {
            "Destiny Board" => Letters.Select(n => D.Cards.FirstOrDefault(c => c.Name == n && c.Owner == winner && c.Loc is Loc.Spell or Loc.Monster)).OfType<Card>().ToList(),
            "Exodius" => D.Cards.Where(c => c.Owner == winner && (c.Name == "Exodius the Ultimate Forbidden Lord" && c.Loc == Loc.Monster || Duel.Exodia.Contains(c.Name) && c.Loc == Loc.Grave)).Take(6).ToList(),
            "deck out" => new(),
            _ => D.Cards.Where(c => c.Name == AltCard(reason) && c.Owner == winner).Take(1).ToList(),
        };
        if (cards.Count == 0 && reason != "deck out") cards = D.Cards.Where(c => c.Owner == winner && c.Loc == Loc.Monster && c.FaceUp).Take(1).ToList();

        var layer = new Control { Size = new Vector2(W, H), MouseFilter = MouseFilterEnum.Ignore, ZIndex = 40 };
        hud.AddChild(layer);
        var dim = new ColorRect { Size = new Vector2(W, H), Color = new Color(0, 0, 0, 0), MouseFilter = MouseFilterEnum.Ignore };
        layer.AddChild(dim);
        CreateTween().TweenProperty(dim, "color:a", 0.55f, 0.6);
        Cue("banner_special_win");

        bool destiny = reason == "Destiny Board";
        int n = Math.Max(1, cards.Count);
        float ch = destiny ? H * 0.34f : n == 1 ? H * 0.5f : H * 0.3f, cw = ch / CardH, gap = cw * 0.18f;
        float rowW = cards.Count * cw + (cards.Count - 1) * gap, y = H * 0.43f;
        var big = new List<TextureRect>();
        for (int i = 0; i < cards.Count; i++)
        {
            var c = cards[i];
            var at = new Vector2(W / 2 - rowW / 2 + i * (cw + gap) + cw / 2, y);
            Vector2 from = views.TryGetValue(c.Uid, out var v) ? cam.UnprojectPosition(v.Root.GlobalPosition) : at;
            if (v != null) { HideFor(v, 999); }
            var r = new TextureRect { Texture = CardArt.Large(c.Data), ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.Scale,
                Size = new Vector2(cw, ch), PivotOffset = new Vector2(cw, ch) / 2, Position = from - new Vector2(cw, ch) / 2, Scale = new Vector2(0.3f, 0.3f),
                TextureFilter = TextureFilterEnum.LinearWithMipmaps, MouseFilter = MouseFilterEnum.Ignore };
            layer.AddChild(r); big.Add(r);
            var tw = CreateTween().SetParallel();
            tw.TweenProperty(r, "position", at - new Vector2(cw, ch) / 2, 0.6).SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
            tw.TweenProperty(r, "scale", Vector2.One, 0.6).SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
            After(0.55, () => { Cue("thread_flare"); GlintAt("md/star4", FxGold, at, cw * 1.4f, 0.4f, 1.2f, 0.5); });
            await Wait(destiny ? 0.25 : 0.35);
        }
        await Wait(0.7);

        if (destiny)
        {
            // F I N A L, one letter over each card, each with a flare
            string word = "FINAL";
            for (int i = 0; i < big.Count && i < 5; i++)
            {
                var r = big[i];
                var l = Put(layer, word[i].ToString(), Impact, (int)(ch * 0.62f), RivalPanels.Gold, r.Position + new Vector2(0, ch * 0.1f), new Vector2(cw, ch * 0.8f), HorizontalAlignment.Center, 18);
                l.PivotOffset = new Vector2(cw, ch * 0.8f) / 2; l.Scale = new Vector2(2.2f, 2.2f); l.Modulate = new Color(1, 1, 1, 0);
                var tw = CreateTween().SetParallel();
                tw.TweenProperty(l, "scale", Vector2.One, 0.28).SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
                tw.TweenProperty(l, "modulate:a", 1f, 0.12);
                tw.TweenProperty(r, "modulate", new Color(0.55f, 0.5f, 0.45f), 0.3);
                Cue("thread_flare");
                GlintAt("md/star4", FxGold, r.Position + new Vector2(cw, ch) / 2, cw * 1.8f, 0.3f, 1.4f, 0.6);
                await Wait(0.45);
            }
            await Wait(0.3);
        }
        else if (reason == "deck out")
        {
            var l = Put(layer, "0", Impact, (int)(H * 0.4f), Bad, new Vector2(0, H * 0.18f), new Vector2(W, H * 0.45f), HorizontalAlignment.Center, 18);
            l.Modulate = new Color(1, 1, 1, 0);
            CreateTween().TweenProperty(l, "modulate:a", 1f, 0.4);
            Put(layer, "CARDS IN THE DECK", Cond, 40, new Color(1, 1, 1, 0.8f), new Vector2(0, H * 0.62f), new Vector2(W, 50), HorizontalAlignment.Center, 6);
            await Wait(1.0);
        }
        // the line under the cards, then a flash, then the loser's field breaks
        var line = Put(layer, AltLine(reason), Cond, 46, Colors.White, new Vector2(0, y + ch / 2 + 40), new Vector2(W, 60), HorizontalAlignment.Center, 8);
        line.Modulate = new Color(1, 1, 1, 0);
        CreateTween().TweenProperty(line, "modulate:a", 1f, 0.3);
        await Wait(1.3);
        var flash = new ColorRect { Size = new Vector2(W, H), Color = new Color(1, 0.95f, 0.85f, 0.85f), MouseFilter = MouseFilterEnum.Ignore };
        layer.AddChild(flash);
        CreateTween().TweenProperty(layer, "modulate:a", 0f, 0.6);
        await Wait(0.2);
        if (side is 0 or 1) await FieldShatters(1 - side);
        layer.QueueFree();
        await Wait(0.4);
    }
}
