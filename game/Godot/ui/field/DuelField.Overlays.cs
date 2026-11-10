using Godot;
using PoC.Engine;

namespace PoC.Game.UI;

/// Dev: `-- --overlay=<name>` shows one of the duel's overlays on demand (2.5 s in), for screenshots and reviews.
/// `--overlay=list` prints the names.
public partial class DuelField
{
    static readonly string[] OverlayNames =
    {
        "turn", "phase", "phasemenu", "actionchip", "toast", "yesno", "choose", "respond", "pickfield", "pickhidden",
        "attack", "pile", "discard", "coincall", "coin", "dice", "boost", "links", "lp", "burn", "shatter", "shield",
        "swords", "spark", "rays", "menu", "win", "effectlong", "chooselong", "sheetlong",
    };

    void Overlay(string name)
    {
        if (name == "list") { GD.Print(string.Join(" ", OverlayNames)); return; }
        var mine = views.Values.Where(v => v.S.Side == 0 && v.S.Loc == Loc.Monster).Select(v => v).ToList();
        var theirs = views.Values.Where(v => v.S.Side == 1 && v.S.Loc == Loc.Monster).ToList();
        var anyMon = mine.Concat(theirs).FirstOrDefault() ?? views.Values.First(v => v.S.Loc == Loc.Hand);
        var hand = views.Values.Where(v => v.S.Side == 0 && v.S.Loc == Loc.Hand).OrderBy(v => v.S.Index).ToList();
        var grave = D.Cards.Where(c => c.Data.IsMonster).Take(6).ToList();
        var card = hand.FirstOrDefault()?.Card ?? anyMon.Card;
        var chips = new List<(string, System.Action, bool)>();
        System.Action none = () => { };
        switch (name)
        {
            case "turn": _ = Banner("YOUR TURN", RivalPanels.Gold, "TURN 3"); break;
            case "win": _ = Banner("YOU WIN", RivalPanels.Gold, ""); break;
            case "phase": ShowPhase(Phase.Battle, 0, 3); break;
            case "phasemenu": PhaseLeft(); break;
            case "actionchip": if (hand.Count > 0) GetViewport().WarpMouse(cam.UnprojectPosition(hand[0].Root.GlobalPosition)); break;
            case "toast": Toast($"{Settings.PlayerName} · {card.Name}", "CHAIN 1"); break;
            // The longest real prompt texts, for text-fit checks.
            case "effectlong": EffectSheet(new Prompt { Player = Me, Kind = Ask.YesNo, Source = card, Text = "Send Armed Dragon LV3 to the GY to Special Summon Armed Dragon LV5?", Options = { new(Act.Option, Arg: 1, Text: "Yes"), new(Act.Option, Arg: 0, Text: "No") } }); break;
            case "chooselong": EffectSheet(new Prompt { Player = Me, Kind = Ask.Choose, Source = card, Text = "Return a Continuous Spell Card from your Graveyard to the top of your Deck, or keep it there?", Options = { new(Act.Option, Arg: 0, Text: "Return it to the top of the Deck"), new(Act.Option, Arg: 1, Text: "Special Summon 1 Armed Dragon LV5 from your hand or Deck, then destroy 1 monster your opponent controls"), new(Act.Option, Arg: 2, Text: "Keep it") } }); break;
            case "sheetlong": ShowSheet("UFO TURTLE: SPECIAL SUMMON 1 FIRE MONSTER WITH 1500 OR LESS ATK FROM YOUR DECK IN FACE-UP ATTACK POSITION  ·  CHOOSE 1", grave, new() { ("CONFIRM", none, false) }); break;
            case "yesno": ShowSheet($"USE {card.Name.ToUpperInvariant()}'S EFFECT?", null, new() { ("YES", none, true), ("NO", none, true) }); break;
            case "choose": ShowSheet("CHOOSE AN EFFECT", null, new() { ("DESTROY 1 MONSTER", none, true), ("DRAW 1 CARD", none, true), ("GAIN 1000 LP", none, true) }); break;
            case "respond": ShowSheet("CHAIN TO SUMMON?", null, new() { ("ACTIVATE: TRAP HOLE", none, true), ("PASS", none, true) }); break;
            case "pickfield": ShowSheet("SELECT 1 MONSTER TO DESTROY  ·  CHOOSE 1", null, new(), top: true); foreach (var v in theirs) hints[v.Card.Uid] = Hint.Target; foreach (var v in views.Values) Aura(v); break;
            case "pickhidden": ShowSheet("SPECIAL SUMMON WHICH MONSTER?  ·  CHOOSE 1", grave, new() { ("CONFIRM", none, false) }); break;
            case "attack": ShowSheet($"{anyMon.Card.Name.ToUpperInvariant()}: CHOOSE A TARGET", null, new() { ("DIRECT ATTACK", none, true), ("CANCEL", none, true) }, top: true); break;
            case "pile": ShowSheet($"YOUR GRAVEYARD  ·  {grave.Count}", grave, new() { ("CLOSE", none, true) }); break;
            case "discard": ShowCards($"{Settings.PlayerName.ToUpperInvariant()} DISCARDED", grave.Take(4).ToList(), 3); break;
            case "position": PositionSheet(new Prompt { Kind = Ask.Position, Source = anyMon.Card, Text = $"Position for {anyMon.Card.Name}", Options = { new(Act.Option, anyMon.Card, Arg: 0, Text: "Attack Position"), new(Act.Option, anyMon.Card, Arg: 1, Text: "Defense Position") } }); break;
            case "zones": ZonePick(new Prompt { Kind = Ask.Choose, Source = card, Text = "Ground Collapse: block which zone?", Options = { new(Act.Option, Arg: 8 * Me + 1), new(Act.Option, Arg: 8 * Me + 4), new(Act.Option, Arg: 8 * (1 - Me) + 0), new(Act.Option, Arg: 8 * (1 - Me) + 3) } }); blockMarks[1, 2].Visible = true; break;
            case "coincall": CoinCallSheet(new Prompt { Kind = Ask.Choose, Source = card, Options = { new(Act.Option, Text: "Heads"), new(Act.Option, Text: "Tails") } }); break;
            case "coin": Coins(new Ev { Kind = EvKind.Coin, Player = Me, Other = card, Amount = 1, Arg = 1, Call = 1 }); break;
            case "dice": DiceCup(new Ev { Kind = EvKind.Die, Player = Me, Other = card, Amount = 5 }); break;
            case "boost": { var was = anyMon.S; Place(anyMon, was with { Atk = was.Atk + 500 }); StatChange(anyMon, was); break; }
            case "links": ShowLinks(anyMon.Card.Uid); break;
            case "lp": LpHit(1, 8000, 6500); break;
            case "burn": EffectBurn(anyMon); anyMon.Root.Visible = false; break;
            case "shatter": CrackBreak(anyMon, anyMon.Root.GlobalPosition + Vector3.Back); break;
            case "shield": ShieldPop(anyMon); break;
            case "swords": SwordClash(anyMon); break;
            case "spark": NegateGlitch(anyMon); break;
            case "rays": GoldRays(anyMon); break;
            case "menu": ToggleDuelMenu(); break;
            default: GD.PrintErr($"overlay: unknown '{name}' (try --overlay=list)"); break;
        }
    }
}
