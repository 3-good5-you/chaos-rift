using Godot;
using PoC.Engine;

namespace PoC.Game.UI;

/// Signature card effects in real duels (locked in the FX Lab, 2026-11-13; the effects live in
/// DuelField.FxLab.Batch6.cs and DuelField.SignatureFx.cs). Play hooks them in where the plain effect would have played:
/// summons (the three Gods with their tributes taken by lightning, Dark Magician, Blue-Eyes, Red-Eyes, the Envoys,
/// Jinzo), cards destroyed by a card's effect (Feather Duster, MST, Dark Hole, Raigeki, Heavy Storm, Trap Hole, Ring of
/// Destruction, Torrential Tribute, Mirror Force, Time Wizard), and moments inside a card's resolution, before the
/// cards move (Change of Heart, Brain Control, Monster Reborn, Pot of Greed, Magic Cylinder, Negate Attack, Swords of
/// Revealing Light), plus every banished card (phase out).
public partial class DuelField
{
    static readonly HashSet<string> GodNames = new() { "Obelisk the Tormentor", "Slifer the Sky Dragon", "The Winged Dragon of Ra" };

    /// A signature effect the replay waits for before its next step (Play awaits it after Flair).
    Task? sigTask;
    int godTributes;   // tributes of a God already struck in this batch (their strikes follow one another)
    readonly HashSet<int> swept = new();   // this batch: Harpie's Feather Duster cards whose sweep already played

    /// Summons with their own arrival (the Gods are added in SigArrive).
    static readonly Dictionary<string, Func<DuelField, View, St, Task>> SummonFx = new()
    {
        ["Dark Magician"] = (f, v, at) => f.DarkMagicianFx(v, at),
        ["Blue-Eyes White Dragon"] = (f, v, at) => f.BlueEyesOn(v, at),
        ["Red-Eyes B. Dragon"] = (f, v, at) => f.RedEyesOn(v, at),
        ["Black Luster Soldier - Envoy of the Beginning"] = (f, v, at) => f.ChaosOn(v, at),
        ["Chaos Emperor Dragon - Envoy of the End"] = (f, v, at) => f.ChaosOn(v, at),
        ["Jinzo"] = (f, v, at) => f.JinzoOn(v, at),
    };

    readonly HashSet<int> sigShown = new();   // summons already shown by a resolution's own effect (Monster Reborn)

    /// A summon that plays its own signature arrival, landing face-up (not one Monster Reborn already brought in).
    bool SigSummon(Step s) => s.Ev is { Kind: EvKind.Summoned, Card: { } c } && s.Cards[c.Uid].Up && !sigShown.Contains(c.Uid)
        && (GodNames.Contains(c.Name) || SummonFx.ContainsKey(c.Name));

    /// The card a summon's tributes go to is a God: the tribute is struck and taken by lightning. True when it played.
    bool GodTribute(View tribute, (SummonKind Kind, int Uid, St At) o)
    {
        if (o.Kind != SummonKind.Tribute || !views.TryGetValue(o.Uid, out var god) || !GodNames.Contains(god.Card.Name)) return false;
        var (col, pale) = GodColours(god.Card.Name);
        TributeTaken(tribute, col, pale, 0);
        godTributes++;
        // no wait: the tribute is its own sheet from the strike on, so its real card goes to the Graveyard unseen
        return true;
    }

    static (Color Col, Color Pale) GodColours(string name) => name switch
    {
        "Obelisk the Tormentor" => (ObBlue, ObPale),
        "Slifer the Sky Dragon" => (SlRed, SlPale),
        _ => (RaGold, RaPale),
    };

    /// The summoned card has just landed (Sync): hide it, and play its arrival, which burns it in at the end.
    void SigArrive(View v, St at)
    {
        v.Root.Visible = false; v.Stat.Visible = false;
        arriving.Add(v.Card.Uid);
        sigTask = Arrive(v, SummonFx.TryGetValue(v.Card.Name, out var fx) ? fx(this, v, at) : GodArrive(v, at));
        if (OS.IsDebugBuild()) GD.Print($"sig: {v.Card.Name} arrives");
    }

    readonly HashSet<int> arriving = new();   // summons whose own arrival is still bringing them in (no label yet)

    async Task Arrive(View v, Task fx)
    {
        await fx;
        arriving.Remove(v.Card.Uid);
        if (v.S.Loc != Loc.None) { v.Root.Visible = true; UpdateStat(v, false); }
    }

    async Task GodArrive(View v, St at)
    {
        // the strikes on its tributes play out first
        await Wait(0.2);
        godTributes = 0;
        var (col, _) = GodColours(v.Card.Name);
        var g = new GodCtx { V = v, At = at, P = Grid3(at), Tribs = Array.Empty<View>() };
        g.Lift = DarkHold(col, 1.0);
        await Wait(0.5);
        await (v.Card.Name switch { "Obelisk the Tormentor" => ObeliskBody(g), "Slifer the Sky Dragon" => SliferBody(g), _ => RaBody(g) });
        PlacementFx(v, at.Def);
    }

    /// Cards destroyed by a card's effect, all at once: the destroying card's own effect over all of them.
    static readonly Dictionary<string, Func<DuelField, List<View>, Card, Task>> DestroyFx = new()
    {
        ["Harpie's Feather Duster"] = (f, row, _) => f.SweepAcross(row, 1),
        ["Mystical Space Typhoon"] = (f, row, _) => f.MstOn(row[0]),
        ["Dark Hole"] = (f, row, _) => f.DarkHoleOn(row),
        ["Raigeki"] = (f, row, _) => f.RaigekiOn(row),
        ["Heavy Storm"] = (f, row, _) => f.HeavyStormOn(row),
        ["Trap Hole"] = (f, row, _) => f.TrapHoleOn(row[0]),
        ["Ring of Destruction"] = (f, row, _) => f.RingOn(row[0]),
        ["Torrential Tribute"] = (f, row, _) => f.TorrentialOn(row),
        ["Time Wizard"] = (f, row, _) => f.TimeWizardOn(row),
        ["Mirror Force"] = (f, row, by) => f.views.TryGetValue(by.Uid, out var trap)
            ? f.MirrorForceOn(trap, f.lastAttack.A is { } a && f.views.TryGetValue(a.Uid, out var av) ? av : null, row) : Task.CompletedTask,
    };

    /// A card destroyed by one of DestroyFx's cards: the first one of this batch plays that effect over every card it
    /// destroys. True when the card is taken care of (now, or by the effect already on its way).
    bool DestroyedByCard(View v, Card by)
    {
        if (!DestroyFx.TryGetValue(by.Name, out var fx)) return false;
        if (!swept.Add(by.Uid)) return true;
        var row = destroying.Where(kv => kv.Value.By == by && views.TryGetValue(kv.Key, out var x) && x.Root.Visible && x.S.Loc is Loc.Spell or Loc.Field or Loc.Monster)
            .Select(kv => views[kv.Key]).ToList();
        if (!row.Contains(v)) row.Insert(0, v);
        sigTask = fx(this, row, by);
        if (OS.IsDebugBuild()) GD.Print($"sig: {by.Name} destroys {row.Count}");
        return true;
    }

    /// Cards a signature effect has turned into sheets (CardSheet): a batch's snapshots still show some of them on the
    /// field until their own move, and Sync would put them back there; they stay hidden until they really leave.
    readonly HashSet<int> sheeted = new();
    void KeepSheetedHidden(St[] cards)
    {
        foreach (var uid in sheeted.ToList())
        {
            if (!views.TryGetValue(uid, out var v)) { sheeted.Remove(uid); continue; }
            if (cards[uid].Loc is Loc.Monster or Loc.Spell or Loc.Field) { v.Root.Visible = false; v.Stat.Visible = false; }
            else
            {
                // it has left: the effect already showed it go, so it is put straight where it went (no flight from
                // its old zone, which showed the card again on its way), and shown there a moment later
                sheeted.Remove(uid);
                v.Tw?.Kill();
                Place(v, cards[uid]);
                v.Root.Visible = false; v.Stat.Visible = false;
                if (cards[uid].Loc != Loc.None) HideFor(v, 0.35);
            }
        }
        foreach (var uid in arriving)
            if (views.TryGetValue(uid, out var av)) { av.Root.Visible = false; av.Stat.Visible = false; }   // still arriving
    }

    (Card? A, Card? T) lastAttack;   // the attack being made (Mirror Force, Magic Cylinder, Negate Attack)
    void SigAttack(Ev e) => lastAttack = (e.Card, e.Other);

    int byShown = -1;   // the resolving card whose own moment already played (it plays once per resolution)

    /// Before a step's cards move: a resolving card's own moment, and banished cards phasing out. Returns whether a
    /// control change was shown (no plain flare then) and what to do once the cards have moved.
    async Task<(bool Shown, Action? Then)> SigBefore(Step s)
    {
        var e = s.Ev; var by = s.By;
        if (by == null) byShown = -1;
        View? V(Card? c) => c != null && views.TryGetValue(c.Uid, out var v) ? v : null;
        // a banished card phases out where it was, and a cold orb takes it to its owner's pile
        if (e is { Kind: EvKind.Banished, Card: { } bc } && V(bc) is { } bv && bv.S.Loc is not (Loc.Banished or Loc.None or Loc.Deck or Loc.Extra)
            && (bv.Root.Visible || bv.S.Loc == Loc.Grave))
        {
            bv.Root.Visible = true;   // a card under the top of the Graveyard pile phases out too
            int pile = s.Cards[bc.Uid].Side;
            if (OS.IsDebugBuild()) GD.Print($"sig: {bc.Name} banished (phase out)");
            await BanishOn(bv, pile);
            return (false, null);   // it is a sheet now: KeepSheetedHidden puts the card on the banished pile
        }
        if (by == null) return (false, null);
        // control changes
        if (e is { Kind: EvKind.ControlChanged, Card: { } cc } && V(cc) is { } cv)
        {
            if (by.Name == "Change of Heart") { var heart = await HeartBeats(cv); return (true, () => sigTask = HeartCarry(cv, heart, 0.9)); }
            if (by.Name == "Brain Control" && V(by) is { } bcv) { if (OS.IsDebugBuild()) GD.Print("sig: Brain Control"); await BrainControlOn(bcv, cv); return (true, () => sigTask = BrainControlEnd(cv)); }
        }
        // Monster Reborn: the monster comes apart in the Graveyard and builds back on its zone (no other arrival)
        if (by.Name == "Monster Reborn" && e is { Kind: EvKind.Summoned, Card: { } rc } && V(rc) is { } rv && rv.S.Loc == Loc.Grave)
        {
            sigShown.Add(rc.Uid); if (OS.IsDebugBuild()) GD.Print("sig: Monster Reborn");
            await RebornOn(rv, s.Cards[rc.Uid]);
            return (false, null);
        }
        if (byShown == by.Uid) return (false, null);
        if (OS.IsDebugBuild() && by.Name is "Pot of Greed" or "Magic Cylinder" or "Negate Attack" or "Swords of Revealing Light") GD.Print($"sig: {by.Name} at {e?.Kind}");
        switch (by.Name)
        {
            case "Pot of Greed" when e?.Kind == EvKind.Drew && V(by) is { } pv:
                byShown = by.Uid; await PotOn(pv, Side(e.Player)); break;
            case "Magic Cylinder" when e?.Kind == EvKind.Damage && V(lastAttack.A) is { } av:
                byShown = by.Uid;
                var guard = V(lastAttack.T)?.Root.GlobalPosition ?? ZonePos(Loc.Monster, 2, 1 - av.S.Side);
                await CylinderOn(av, guard, Side(e.Player)); break;
            case "Negate Attack" when V(lastAttack.A) is { } nv:
                byShown = by.Uid; await NegateOn(nv, V(lastAttack.T)?.Root.GlobalPosition, nv.S.Side); break;
            case "Swords of Revealing Light" when V(by) is { } sv:
                byShown = by.Uid; await SwordsOn(1 - sv.S.Side); break;
        }
        return (false, null);
    }

    // ---------------------------------------------------------------- test scenarios (`--scenario=sig:Card Name`)

    /// The board for each signature card: your turn 3, the card in your hand, what it needs on the field.
    void SigScenario(string card)
    {
        Do(Is(Act.EndTurn)); Do(Is(Act.EndTurn));   // to your turn 3
        switch (card)
        {
            case "Harpie's Feather Duster":
                foreach (var n in new[] { "Pot of Greed", "Dark Hole", "Swords of Revealing Light" }) SandboxPut(CardDb.Shared.Named(n), 1, "SPELL/TRAP SET");
                liveMove = Is(Act.Activate, card); break;
            case "Change of Heart" or "Brain Control":
                SandboxPut(CardDb.Shared.Named("Gaia The Fierce Knight"), 1, "ATTACK");
                liveMove = Is(Act.Activate, card); break;
            case "Mystical Space Typhoon":
                SandboxPut(CardDb.Shared.Named("Swords of Revealing Light"), 1, "SPELL/TRAP SET");
                liveMove = Is(Act.Activate, card); break;
            case "Heavy Storm":
                foreach (var n in new[] { "Pot of Greed", "Dark Hole" }) SandboxPut(CardDb.Shared.Named(n), 1, "SPELL/TRAP SET");
                SandboxPut(CardDb.Shared.Named("Mirror Force"), 0, "SPELL/TRAP SET");
                liveMove = Is(Act.Activate, card); break;
            case "Dark Hole" or "Raigeki" or "Swords of Revealing Light":
                foreach (var n in new[] { "Gaia The Fierce Knight", "Battle Ox" }) SandboxPut(CardDb.Shared.Named(n), 1, "ATTACK");
                SandboxPut(CardDb.Shared.Named("Mystical Elf"), 1, "SET MONSTER");
                if (card == "Dark Hole") SandboxPut(CardDb.Shared.Named("Celtic Guardian"), 0, "ATTACK");
                liveMove = Is(Act.Activate, card); break;
            case "Pot of Greed":
                liveMove = Is(Act.Activate, card); break;
            case "Monster Reborn":
                SandboxPut(CardDb.Shared.Named("Dark Magician"), 0, "GRAVEYARD");
                liveMove = Is(Act.Activate, card); break;
            case "Black Luster Soldier - Envoy of the Beginning":
                foreach (var n in new[] { "Mystical Elf", "Feral Imp" }) SandboxPut(CardDb.Shared.Named(n), 0, "GRAVEYARD");   // a LIGHT and a DARK to banish
                liveMove = o => o.Card?.Name == card && o.Act is Act.Special or Act.Summon; break;
            case "Ring of Destruction" or "Torrential Tribute":
                SandboxPut(CardDb.Shared.Named("Gaia The Fierce Knight"), 1, "ATTACK");
                SandboxPut(CardDb.Shared.Named(card), 0, "SPELL/TRAP SET");
                SandboxPut(CardDb.Shared.Named("Celtic Guardian"), 0, "HAND");
                trapScenario = card;
                liveMove = card == "Torrential Tribute" ? Is(Act.Summon, "Celtic Guardian") : o => o.Card?.Name == card; break;
            case "Time Wizard":
                foreach (var n in new[] { "Gaia The Fierce Knight", "Battle Ox" }) SandboxPut(CardDb.Shared.Named(n), 1, "ATTACK");
                liveMove = Is(Act.Summon, card); liveMove2 = Is(Act.Activate, card); break;
            case "Jinzo":
                foreach (var n in new[] { "Celtic Guardian" }) SandboxPut(CardDb.Shared.Named(n), 0, "ATTACK");
                foreach (var n in new[] { "Mirror Force", "Pot of Greed" }) SandboxPut(CardDb.Shared.Named(n), 1, "SPELL/TRAP SET");
                liveMove = Is(Act.Summon, card); break;
            case "Trap Hole":
                // the rival's turn plays live with one strong monster in hand: it summons it into your set Trap Hole
                SandboxPut(CardDb.Shared.Named(card), 0, "SPELL/TRAP SET");
                var rh = D.P(1 - Me);
                foreach (var c in rh.Hand.ToList()) { rh.Hand.Remove(c); c.Loc = Loc.Deck; rh.Deck.Add(c); }
                SandboxPut(CardDb.Shared.Named("Gemini Elf"), 1, "HAND");
                trapScenario = card;
                liveMove = Is(Act.EndTurn); break;
            case "Mirror Force" or "Magic Cylinder" or "Negate Attack":
                // the rival attacks into your set trap (Trap Hole: the rival summons into it); you answer with it
                SandboxPut(CardDb.Shared.Named(card), 0, "SPELL/TRAP SET");
                SandboxPut(CardDb.Shared.Named("Celtic Guardian"), 0, "ATTACK");
                foreach (var n in new[] { "Gaia The Fierce Knight", "Battle Ox" }) SandboxPut(CardDb.Shared.Named(n), 1, "ATTACK");
                trapScenario = card;
                liveMove = Is(Act.EndTurn); break;
            default:   // a summon: tributes on your field
                foreach (var n in new[] { "Celtic Guardian", "Feral Imp", "Mystical Elf" }) SandboxPut(CardDb.Shared.Named(n), 0, "ATTACK");
                liveMove = Is(Act.Summon, card); break;
        }
        liveAuto = true;
        D.RefreshPrompt();
    }

    /// A scenario's live move answers its own follow-up questions (tribute picks, a target, a position).
    bool liveAuto;
    string? trapScenario;
    Func<Opt, bool>? liveMove2;   // a scenario's second live move (Time Wizard: summon it, then use it)   // a trap scenario: the rival's turn plays live, and your trap answers when it can
    Command AutoAnswer(Prompt p) => new(p.Player, p.Kind switch
    {
        Ask.Respond when trapScenario != null && p.Options.FindIndex(o => o.Card?.Name == trapScenario) is >= 0 and var k => new[] { k },
        Ask.Respond => new[] { p.Options.Count - 1 },
        Ask.Pick => Enumerable.Range(0, Math.Max(1, p.Min)).ToArray(),
        _ => new[] { 0 },
    });

    // ---------------------------------------------------------------- `--sigcheck`: is the board what the duel says?

    static readonly bool SigCheckOn = OS.GetCmdlineUserArgs().Contains("--sigcheck");
    readonly List<MeshInstance3D> liveSheets = new();   // every card sheet made (CardSheet, NewSheet): none may linger

    St[]? shownCards;   // the snapshot on screen now (the step Play last synced)
    string shownStep = "";

    /// After a batch has played and its effects settled: every card shows what the last played step says (place, side,
    /// zone, face, position), cards on the field are visible and sit on their zone, cards in a pile sit on it, no card
    /// sheet is left on screen, and no card came back to a zone it had left (WatchCards). Prints each problem.
    void VerifyBoard(string when)
    {
        int bad = 0;
        void Bad(string m) { bad++; GD.Print($"sigcheck PROBLEM {when}: {m}"); }
        foreach (var v in views.Values)
        {
            if (shownCards == null || v.Card.Uid >= shownCards.Length) continue;
            var want = shownCards[v.Card.Uid]; var s = v.S; var name = v.Card.Name;
            bool field = want.Loc is Loc.Monster or Loc.Spell or Loc.Field;
            if (s.Loc != want.Loc || s.Side != want.Side || field && (s.Index != want.Index || s.Up != want.Up || s.Def != want.Def))
            { Bad($"{name}: shown {s.Loc}/{s.Side}/z{s.Index}/up{s.Up}/def{s.Def}, step says {want.Loc}/{want.Side}/z{want.Index}/up{want.Up}/def{want.Def}"); continue; }
            if (field)
            {
                if (!v.Root.Visible) Bad($"{name}: on the field ({want.Loc} {want.Index}) but hidden");
                float d = v.Root.Transform.Origin.DistanceTo(Target(v, s).Origin);
                if (d > 0.05f) Bad($"{name}: {d:0.00} away from its zone");
                if (!v.Root.Transform.Basis.IsEqualApprox(Target(v, s).Basis)) Bad($"{name}: turned wrong on its zone");
            }
            else if (want.Loc is Loc.Grave or Loc.Banished && v.Root.Visible && v.Root.Transform.Origin.DistanceTo(Target(v, s).Origin) > 0.2f)
                Bad($"{name}: in {want.Loc} but drawn {v.Root.Transform.Origin.DistanceTo(Target(v, s).Origin):0.00} away from the pile");
        }
        foreach (var w in watchHits) Bad(w);
        watchHits.Clear(); watched.Clear(); hitOnce.Clear(); everTaken.Clear();
        if (!watching) { watching = true; WatchCards(); }
        liveSheets.RemoveAll(m => !IsInstanceValid(m));
        foreach (var m in liveSheets.Where(m => m.Visible)) Bad($"a card sheet is still on screen at {m.GlobalPosition}");
        GD.Print($"sigcheck {when}: {bad} problems");
    }

    // While cards play: a card seen on a field zone, then gone from it (hidden, or moved off), then seen on that same
    // zone again although the step on screen does not have it there: a card that "came back".
    readonly Dictionary<int, (string Key, string How)> watched = new();
    readonly List<string> watchHits = new();
    readonly HashSet<int> everTaken = new();   // cards an effect turned into sheets this batch (CardSheet)
    readonly HashSet<string> hitOnce = new();
    void Hit(int uid, string m) { if (hitOnce.Add($"{uid}|{m}")) watchHits.Add($"{m} (step: {shownStep})"); }
    bool watching;
    void WatchCards()
    {
        foreach (var v in views.Values)
        {
            int u0 = v.Card.Uid;
            if (v.Stat.Visible && !v.Root.Visible) Hit(u0, $"{v.Card.Name}: its ATK/DEF label shows while the card is hidden");
            if (everTaken.Contains(u0) && v.Root.Visible && v.Tw != null && v.Tw.IsRunning()) Hit(u0, $"{v.Card.Name}: seen moving after an effect already took it");
            string? key = null;
            bool near = v.S.Loc is Loc.Monster or Loc.Spell or Loc.Field && v.Root.Transform.Origin.DistanceTo(Target(v, v.S).Origin) < 0.1f;
            if (v.Root.Visible && near) key = $"{v.S.Loc} {v.S.Side}/{v.S.Index}";
            int uid = v.Card.Uid;
            if (!watched.TryGetValue(uid, out var e)) { if (key != null) watched[uid] = (key, ""); continue; }
            if (key == null) { if (e.How == "") watched[uid] = (e.Key, v.Root.Visible ? "moved off" : "hidden"); continue; }
            if (key != e.Key) { watched[uid] = (key, ""); continue; }
            if (e.How == "") continue;
            watched[uid] = (key, "");
            var want = shownCards != null && uid < shownCards.Length ? shownCards[uid] : default;
            if ($"{want.Loc} {want.Side}/{want.Index}" != key) watchHits.Add($"{v.Card.Name}: {e.How}, then back on {key} while the step on screen ({shownStep}) has it in {want.Loc}");
            else if (e.How == "hidden") GD.Print($"sigcheck HIDDEN-BACK {v.Card.Name} on {key} (step: {shownStep}){(sheeted.Contains(uid) ? " [taken by an effect]" : "")}");
        }
        GetTree().CreateTimer(0.1, true, false, true).Timeout += WatchCards;
    }
}
