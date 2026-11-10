using Godot;
using PoC.Engine;

namespace PoC.Game.UI;

/// Pack Duel: your six packs open one row at a time (each card flips from its back), then BUILD DECK opens the
/// deck builder limited to these cards. Leaving here ends the Pack Duel.
public partial class PackOpen : RiftMenu
{
    protected override string? MusicPath => Md("solo_gate");
    protected override void OnBack() => Transition.Go(this, "res://ui/DuelModes.tscn");

    protected override void Build()
    {
        Demo.Ensure();
        var t = DeckBuilder.Temp;
        if (t?.Packs == null)
        {
            var packs = Modes.OpenPacks(Session.Rng);
            t = DeckBuilder.Temp = new DeckBuilder.TempBuild("PACK DECK", "BUILT FROM YOUR SIX PACKS ONLY", Modes.PoolProfile(packs.SelectMany(p => p)), "res://ui/RivalSelect.tscn", packs);
            Session.Mode = GameMode.Pack;
        }
        Rift(0.82f, 0.76f);
        Back("MODES");
        Put(this, "PACK DUEL", Title, 40, Ink, new Vector2(80, 58));
        Hair(this, new Vector2(82, 108), 140, GoldC);
        Put(this, "Six packs, nine cards each. Build a 40-card deck from these alone.", Body, 22, Graphite, new Vector2(82, 124));
        float tw = 86, th = tw * 1.458f, gx = 16, gy = 18, x0 = 80, y0 = 180;
        var back = UI.Cosmetics.Back(P.Back, 256);
        for (int p = 0; p < t.Packs!.Count; p++)
            for (int k = 0; k < t.Packs[p].Count; k++)
            {
                var d = Db.Get(t.Packs[p][k]);
                var c = new Vector2(x0 + k * (tw + gx) + tw / 2, y0 + p * (th + gy) + th / 2);
                var card = Card(this, d, c, th, (k - 4) * 0.01f, 0.6f);
                var face = card.GetChildren().OfType<TextureRect>().Last();
                var real = face.Texture;
                face.Texture = back;
                card.Modulate = new Color(1, 1, 1, 0);
                double at = 0.3 + p * 0.45 + k * 0.05;
                var tw2 = CreateTween();
                tw2.TweenProperty(card, "modulate:a", 1f, 0.15).SetDelay(at);
                tw2.TweenProperty(card, "scale:x", 0f, 0.09);
                tw2.TweenCallback(Callable.From(() => face.Texture = real));
                tw2.TweenProperty(card, "scale:x", 1f, 0.09);
                card.ZIndex = p;
            }
        var monsters = t.Packs.SelectMany(x => x).Count(i => Db.Get(i).IsMonster);
        Put(this, "54 CARDS", Title, 30, Silver, new Vector2(W - 400, 220), new Vector2(340, 40), HorizontalAlignment.Right);
        Put(this, $"{monsters} MONSTERS", Thin, 18, Mist, new Vector2(W - 400, 270), new Vector2(340, 24), HorizontalAlignment.Right);
        Put(this, $"{54 - monsters} SPELLS & TRAPS", Thin, 18, Mist, new Vector2(W - 400, 298), new Vector2(340, 24), HorizontalAlignment.Right);
        Button(this, "BUILD DECK", new Vector2(W - 230, H - 100), () => Transition.Go(this, "res://ui/DeckBuilder.tscn"), 28);
    }
}
