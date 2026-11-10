using Godot;
using PoC.Engine;

namespace PoC.Game.UI;

/// Fixed duels that stop at one moment, for screenshots and manual checks (`--scenario=NAME`):
/// respond — Kaiba summons Gemini Elf into your set Trap Hole: the chain prompt.
/// search  — Sangan is destroyed in battle: its deck search.
/// boost   — Feral Imp with Sword of Dark Destruction (ATK up, DEF down), Celtic Guardian with Axe of
///           Despair, Yami weakening Kaiba's Fairy; your Main Phase.
/// target  — boost, then Battle Phase: pick what Celtic Guardian attacks.
/// barrel  — Barrel Dragon on your field, Kaiba has a set monster: click it to toss its 3 coins.
/// timewizard — Time Wizard on your field: its effect asks you to call the coin.
/// dice    — Celtic Guardian on your field, Graceful Dice in hand: activate it to roll the dice cup.
/// mutual  — your Celtic Guardian attacks theirs (equal ATK): both destroyed.
/// mutualtrap — the same with a set Call Of The Haunted on their side (a window after the first destruction): response windows split the battle's steps.
/// exodia  — four pieces in hand; Kaiba ends his turn and your draw brings the fifth: the Exodia finish.
/// fusion  — Flame Manipulator and Masaki on your field, Polymerization activated: the Fusion plays live.
/// Test hooks: `--point=Card Name` moves the mouse onto that card, `--click=Card Name` clicks it once your
/// first prompt is up, `--chip=LABEL` presses the dialog button with that label when it appears.
public partial class DuelField
{
    string? scenario, pointName, clickName, chipName;
    Func<Opt, bool>? liveMove;   // a scenario's last move, made once the field is watching (Loop)

    static readonly string[] Filler = { "Mystical Elf", "Silver Fang", "Battle Ox", "Mystical Elf", "Silver Fang" };

    PoC.Engine.DuelSetup ScenarioSetup()
    {
        string[] you, them;
        var stage = StageFor(scenario);
        switch (scenario)
        {
            case "respond": you = new[] { "Trap Hole", "Mystical Elf", "Silver Fang", "Battle Ox", "Celtic Guardian" }; them = new[] { "Gemini Elf" }; break;
            case "barrel": you = new[] { "Mystical Elf", "Silver Fang", "Barrel Dragon", "Battle Ox", "Celtic Guardian" }; them = new[] { "Gemini Elf" }; break;
            case "timewizard": you = new[] { "Time Wizard", "Mystical Elf", "Silver Fang", "Battle Ox", "Celtic Guardian" }; them = new[] { "Gemini Elf" }; break;
            case "dice": you = new[] { "Celtic Guardian", "Graceful Dice", "Mystical Elf", "Silver Fang", "Battle Ox" }; them = new[] { "Gemini Elf" }; break;
            case "search": you = new[] { "Sangan", "Trap Hole", "Battle Ox", "Mystical Elf", "Feral Imp" }; them = new[] { "Gemini Elf" }; break;
            case "mutualtrap": you = new[] { "Celtic Guardian", "Mystical Elf", "Silver Fang", "Battle Ox", "Feral Imp" }; them = new[] { "Celtic Guardian", "Call Of The Haunted" }; break;
            case "mutual": you = new[] { "Celtic Guardian", "Mystical Elf", "Silver Fang", "Battle Ox", "Feral Imp" }; them = new[] { "Celtic Guardian" }; break;
            case "exodia": you = new[] { "Exodia the Forbidden One", "Left Arm of the Forbidden One", "Right Arm of the Forbidden One", "Left Leg of the Forbidden One", "Celtic Guardian", "Mystical Elf", "Right Leg of the Forbidden One" }; them = new[] { "Gemini Elf", "Battle Ox" }; break;
            case "fusion": you = new[] { "Flame Manipulator", "Masaki the Legendary Swordsman", "Polymerization", "Mystical Elf", "Silver Fang" }; them = new[] { "Gemini Elf" }; break;
            case not null when stage != null: you = stage.You; them = stage.Them; break;
            default: you = new[] { "Feral Imp", "Sword of Dark Destruction", "Yami", "Celtic Guardian", "Axe of Despair", "Mystical Elf" }; them = new[] { "Shining Abyss" }; break;
        }
        var db = CardDb.Shared;
        var s = new PoC.Engine.DuelSetup { Seed = 1, FirstPlayer = 0, Shuffle = false, Classic = true };
        List<int> Deck(string[] top) => top.Concat(Enumerable.Range(0, 40 - top.Length).Select(i => Filler[i % Filler.Length])).Select(n => db.Named(n).Idx).ToList();
        s.Main[0].AddRange(Deck(you));
        s.Main[1].AddRange(Deck(them));
        if (scenario == "fusion") s.Extra[0].Add(db.Named("Flame Swordsman").Idx);
        foreach (var x in stage?.Extra ?? Array.Empty<string>()) s.Extra[0].Add(db.Named(x).Idx);
        return s;
    }

    /// Answer prompts until one has an option matching f (passing response windows on the way), then take it.
    void Do(Func<Opt, bool> f)
    {
        while (true)
        {
            var p = D.Pending ?? throw new InvalidOperationException("scenario: the duel ended");
            int i = p.Options.FindIndex(o => f(o));
            if (i >= 0) { D.Submit(new Command(p.Player, new[] { i })); return; }
            if (p.Kind == Ask.Respond) { D.Submit(new Command(p.Player, new[] { p.Options.Count - 1 })); continue; }
            // A card's own question on the way (an End Phase effect, a hand-size discard): the plainest answer.
            if (p.Kind == Ask.Pick) { D.Submit(new Command(p.Player, Enumerable.Range(0, p.Min).ToArray())); continue; }
            if (p.Kind is Ask.YesNo or Ask.Choose or Ask.Position) { D.Submit(new Command(p.Player, new[] { p.Kind == Ask.YesNo ? 1 : 0 })); continue; }
            throw new InvalidOperationException($"scenario {scenario}: no matching option in {p}");
        }
    }

    static Func<Opt, bool> Is(Act a, string? card = null, string? target = null) =>
        o => o.Act == a && (card == null || o.Card?.Name == card) && (target == null || o.Target?.Name == target);

    void ScenarioPlay()
    {
        if (StageFor(scenario) is { } stage)
        {
            stage.Lay(this);
            Do(Is(Act.EndTurn));                       // your turn 1
            if (!stage.CpuLive) Do(Is(Act.EndTurn));   // the rival's turn 2, skipped: you start on turn 3
            if (scenario!.StartsWith("sandbox")) SandboxStart();
            return;
        }
        switch (scenario)
        {
            case "respond":
                Do(Is(Act.SetSpell, "Trap Hole")); Do(Is(Act.EndTurn));
                Do(Is(Act.Summon, "Gemini Elf"));               // stops at your chain prompt
                break;
            case "barrel":
                Do(Is(Act.Summon, "Mystical Elf")); Do(Is(Act.EndTurn));
                Do(Is(Act.Set, "Gemini Elf")); Do(Is(Act.EndTurn));
                Do(Is(Act.Summon, "Silver Fang")); Do(Is(Act.EndTurn));
                Do(Is(Act.EndTurn));
                Do(Is(Act.Summon, "Barrel Dragon"));             // tributes the two monsters
                while (D.Pending is { Kind: Ask.Pick or Ask.Respond } r) D.Submit(new Command(r.Player, r.Kind == Ask.Pick ? Enumerable.Range(0, r.Min).ToArray() : new[] { r.Options.Count - 1 }));
                break;                                            // your Main Phase with Barrel Dragon out
            case "timewizard":
                Do(Is(Act.Summon, "Time Wizard")); Do(Is(Act.EndTurn));
                Do(Is(Act.Summon, "Gemini Elf")); Do(Is(Act.EndTurn));
                while (D.Pending is { Kind: Ask.Respond } r) D.Submit(new Command(r.Player, new[] { r.Options.Count - 1 }));
                break;
            case "dice":
                Do(Is(Act.Summon, "Celtic Guardian"));
                while (D.Pending is { Kind: Ask.Respond } r) D.Submit(new Command(r.Player, new[] { r.Options.Count - 1 }));
                break;                                            // your Main Phase: click Graceful Dice to roll
            case "mutual" or "mutualtrap":
                Do(Is(Act.Summon, "Celtic Guardian")); Do(Is(Act.EndTurn));
                Do(Is(Act.Summon, "Celtic Guardian")); if (scenario == "mutualtrap") Do(Is(Act.SetSpell, "Call Of The Haunted")); Do(Is(Act.EndTurn));
                Do(Is(Act.ToBattle)); liveMove = Is(Act.Attack, "Celtic Guardian", "Celtic Guardian");   // the battle plays live
                break;
            case "exodia":
                Do(Is(Act.Summon, "Celtic Guardian")); Do(Is(Act.EndTurn));
                Do(Is(Act.Summon, "Gemini Elf"));               // Kaiba plays on live; your next draw is the last piece
                break;
            case "fusion":
                Do(Is(Act.Summon, "Flame Manipulator")); Do(Is(Act.EndTurn));
                Do(Is(Act.Summon, "Gemini Elf")); Do(Is(Act.EndTurn));
                Do(Is(Act.Summon, "Masaki the Legendary Swordsman"));
                Do(Is(Act.Activate, "Polymerization"));          // stops at the Fusion prompts: they and the summon play live
                break;
            case "search":
                Do(Is(Act.Summon, "Sangan")); Do(Is(Act.EndTurn));
                Do(Is(Act.Summon, "Gemini Elf")); Do(Is(Act.ToBattle)); Do(Is(Act.Attack, "Gemini Elf", "Sangan"));
                while (D.Pending is { Kind: Ask.Respond } r) D.Submit(new Command(r.Player, new[] { r.Options.Count - 1 }));
                break;                                            // stops at Sangan's search
            default:
                Do(Is(Act.Summon, "Feral Imp"));
                Do(Is(Act.Activate, "Sword of Dark Destruction"));
                Do(Is(Act.Activate, "Yami")); Do(Is(Act.EndTurn));
                Do(Is(Act.Summon, "Shining Abyss")); Do(Is(Act.EndTurn));
                Do(Is(Act.Summon, "Celtic Guardian"));
                Do(Is(Act.Activate, "Axe of Despair"));
                if (D.Pending is { Kind: Ask.Pick } pk) D.Submit(new Command(0, new[] { pk.Options.FindIndex(o => o.Card?.Name == "Celtic Guardian") }));
                if (scenario == "target") Do(Is(Act.ToBattle));
                while (D.Pending is { Kind: Ask.Respond } r) D.Submit(new Command(r.Player, new[] { r.Options.Count - 1 }));
                break;
        }
    }

    /// Test hooks, run when a prompt of yours appears.
    void TestHooks()
    {
        if (chipName != null && sheetLabels.FindIndex(l => l.Contains(chipName, StringComparison.OrdinalIgnoreCase)) is int ci and >= 0)
        {
            chipName = null;
            var press = sheetChips[ci];
            After(1.2, press);
        }
        if (pointName != null)
        {
            var name = pointName; pointName = null;
            After(0.6, () =>
            {
                var v = views.Values.FirstOrDefault(v => v.Card.Name == name && v.S.Loc is Loc.Hand or Loc.Monster or Loc.Spell or Loc.Field);
                if (v != null) GetViewport().WarpMouse(cam.UnprojectPosition(v.Root.GlobalPosition));
            });
        }
        if (clickName != null)
        {
            var name = clickName; clickName = null;
            After(1.0, () =>
            {
                var v = views.Values.FirstOrDefault(v => v.Card.Name == name && v.S.Loc is Loc.Hand or Loc.Monster or Loc.Spell or Loc.Field);
                if (v != null) { GetViewport().WarpMouse(cam.UnprojectPosition(v.Root.GlobalPosition)); CardLeft(v.Card.Uid); }
            });
        }
    }

    /// `--aicheck=N`: headless check of the CPU AI. N duels against the demo bot and N against itself, random
    /// decks.json decks; every duel must finish. Prints a summary and quits.
    void AiCheck(int n)
    {
        var db = CardDb.Shared;
        var decks = db.DeckNames().ToList();
        int done = 0, wins = 0, mirror = 0, errors = 0;
        for (int i = 0; i < n * 2; i++)
        {
            bool vsAi = i >= n;
            var r = new Rng((ulong)(5000 + i));
            var setup = new PoC.Engine.DuelSetup { Seed = r.Next(), FirstPlayer = r.Int(2) };
            for (int pl = 0; pl < 2; pl++) { var (m, e) = db.Deck(decks[r.Int(decks.Count)]); setup.Main[pl].AddRange(m); setup.Extra[pl].AddRange(e); }
            try
            {
                D = new Duel(setup);
                bot = new Rng((ulong)(7000 + i));
                var cpu = new Ai(1, (ulong)i);
                var other = new Ai(0, (ulong)i + 77);
                int steps = 0;
                while (!D.Over && steps++ < 4000) D.Submit(D.Pending!.Player == 1 ? cpu.Decide(D) : vsAi ? other.Decide(D) : Bot(D.Pending!));
                if (!D.Over) { errors++; GD.PrintErr($"aicheck: duel {i} did not finish (turn {D.Turn})"); continue; }
                if (vsAi) mirror++; else { done++; if (D.Winner == 1) wins++; }
            }
            catch (Exception e) { errors++; GD.PrintErr($"aicheck: duel {i} turn {D?.Turn}: {e.InnerException?.Message ?? e.Message}"); GD.PrintErr(e.InnerException?.StackTrace ?? e.StackTrace ?? ""); }
        }
        GD.Print($"aicheck: vs demo bot {done}/{n} finished, AI won {wins}; AI vs AI {mirror}/{n} finished; errors {errors}");
        GetTree().Quit(errors == 0 ? 0 : 1);
    }
}
