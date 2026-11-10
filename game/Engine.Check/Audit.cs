using System.Text.RegularExpressions;
using PoC.Engine;

namespace PoC.Check;

/// `audit [filter]`: every official card's activations tried on two staged boards (bare: nothing to hit; rich: both
/// sides have monsters, Set cards and GY cards). Each offered option is played out with fixed answers and the board
/// is compared before resolution and after. Flags: NOOP (activated, resolved, changed nothing = a missing activation
/// requirement), CRASH, and cards with nothing offered on either board (fine for triggers; listed for review).
static class Audit
{
    const string Neo = "Neo the Magic Swordsman", Elf = "Mystical Elf", Pot = "Pot of Greed";

    enum Where { Hand, Field, Set, Grave }

    public static int Run(string filter)
    {
        var db = CardDb.Shared;
        var cards = db.ByIdx.Values.Where(d => d.Modern && !d.IsNormalMonster && Scripts.IsWired(d)
                && d.Name.Contains(filter, StringComparison.OrdinalIgnoreCase))
            .GroupBy(d => d.Name).Select(g => g.First()).OrderBy(d => d.Name).ToList();
        int noop = 0, crash = 0, silent = 0;
        foreach (var d in cards)
        {
            var places = d.IsMonster ? (d.IsFusion ? new[] { Where.Field, Where.Grave } : new[] { Where.Hand, Where.Field, Where.Grave })
                : d.IsTrap ? new[] { Where.Set, Where.Grave } : new[] { Where.Hand, Where.Grave, Where.Set };
            bool any = false;
            var notes = new List<string>();
            foreach (var w in places)
                foreach (bool rich in new[] { false, true })
                    foreach (int turn in new[] { 3, 2 })   // 3: our Main Phase; 2: the opponent's turn (Pot of Greed, Summon, attacks)
                    {
                        if (w == Where.Set && d.IsSpell && !d.Is("Quick-Play")) continue;
                        Duel Mk() => Stage(d, w, rich, turn);
                        List<(int step, int opt, string label)> wins;
                        try { wins = Windows(Mk, d, turn).Take(8).ToList(); }
                        catch (Exception e) { notes.Add($"CRASH stage {w}/{(rich ? "rich" : "bare")}/t{turn}: {e.GetBaseException().Message}"); crash++; continue; }
                        foreach (var (step, opt, label) in wins)
                        {
                            any = true;
                            string tag = $"{w}/{(rich ? "rich" : "bare")}/{(turn == 3 ? "main" : "opp turn")} {label}";
                            try
                            {
                                var r = Try(Mk, d, turn, step, opt);
                                if (r != null) { notes.Add(r + " " + tag); noop++; }
                                else if (Debug) notes.Add("ok " + tag);
                            }
                            catch (Exception e)
                            {
                                crash++;
                                notes.Add($"CRASH {tag}: {e.GetBaseException().Message} @ {(e.GetBaseException().StackTrace ?? "").Split((char)10).FirstOrDefault()?.Trim()}");
                            }
                        }
                    }
            bool hasAct = Scripts.Get(d) is { } sc && (sc.Run != null || sc.Proc != null || sc.IgnRun != null) && sc.On == null;
            if (!any && hasAct) { silent++; notes.Add("NEVER OFFERED (has an activated effect, no trigger)"); }
            if (notes.Count > 0)
            {
                Console.WriteLine($"{d.Name}{(Scripts.Get(d)?.Sure == true ? " [sure]" : "")}");
                foreach (var n in notes.Distinct()) Console.WriteLine("  " + n);
            }
        }
        Console.WriteLine($"audit: {cards.Count} cards, NOOP {noop}, CRASH {crash}, never offered {silent}");
        return crash == 0 ? 0 : 1;
    }

    static bool Debug => Environment.GetEnvironmentVariable("AUDIT_DEBUG") == "1";
    static bool Stop(Duel x, int turn, bool after) => x.Over || x.Pending == null || (turn == 2 ? x.Turn > 2 : after && x.Pending is { Kind: Ask.Main, Player: 0 } && x.Chain.Count == 0);

    /// Every prompt where we may use the card: our Main Phase (turn 3, first prompt) or our response windows on their turn.
    static IEnumerable<(int, int, string)> Windows(Func<Duel> mk, CardData d, int turn)
    {
        var x = mk();
        var done = new HashSet<string>();
        for (int step = 0; step < 400 && !Stop(x, turn, step > 0); step++)
        {
            var p = x.Pending!;
            if (Debug && step == 0) Console.WriteLine($"    t{turn} G0[{string.Join(",", x.P(0).Grave.Select(c => c.Name))}] {p}");
            if (p.Player == 0 && (turn == 3 ? step == 0 && p.Kind == Ask.Main : p.Kind == Ask.Respond))
                for (int i = 0; i < p.Options.Count; i++)
                    if (p.Options[i].Card?.Data == d && p.Options[i].Act is Act.Activate or Act.Special)
                        yield return (step, i, $"{p.Options[i].Act}{(p.Options[i].Text is { } t ? " " + t : "")}{(turn == 2 ? $" @ {x.Phase} '{p.Text}'" : "")}");
            if (turn == 3) yield break;
            x.Submit(Policy(x, p, done));
        }
    }

    /// Replay to the window, use the card, play on: null = fine, "NOOP" = it resolved and changed nothing.
    static string? Try(Func<Duel> mk, CardData d, int turn, int step, int opt)
    {
        var x = mk();
        var done = new HashSet<string>();
        for (int i = 0; i < step; i++) x.Submit(Policy(x, x.Pending!, done));
        var o = x.Pending!.Options[opt];
        var self = o.Card!;
        string? atChain = null, resolved = null;
        x.OnEvent += e =>
        {
            if (e.Kind == EvKind.Activated && e.Link?.Card == self && atChain == null) atChain = Fp(x, self, d);
            if (e.Kind == EvKind.Resolved && e.Link?.Card == self && resolved == null) resolved = Fp(x, self, d);
        };
        x.Submit(new Command(0, new[] { opt }));
        for (int i = 0; i < 400 && !Stop(x, turn, true) && resolved == null; i++) x.Submit(Policy(x, x.Pending!, done));
        // Normal/Quick-Play Spells, Normal/Counter Traps, monster activated effects: resolving must change something.
        bool mustDo = o.Act == Act.Activate && (d.IsMonster || !(d.Is("Continuous") || d.Is("Field") || d.Is("Equip")));
        if (Debug) Console.WriteLine($"    {d.Name} t{turn} s{step} B {atChain} A {resolved}");
        return mustDo && atChain != null && atChain == resolved ? "NOOP" : null;
    }

    /// The opponent's turn: Pot of Greed, Normal Summon Neo, attack with everything; then end. Everything else: Answer.
    static Command Policy(Duel x, Prompt p, HashSet<string> done)
    {
        if (p.Player == 1 && p.Kind == Ask.Main)
        {
            int i = p.Options.FindIndex(o => o.Act == Act.Activate && o.Card?.Name == Pot);
            if (i < 0 || done.Contains("pot")) i = p.Options.FindIndex(o => o.Act == Act.Summon && o.Card?.Name == Neo && !done.Contains("sum"));
            if (i < 0) i = p.Options.FindIndex(o => o.Act == Act.ToBattle && !done.Contains("bp"));
            if (i >= 0)
            {
                done.Add(p.Options[i].Act switch { Act.Activate => "pot", Act.Summon => "sum", _ => "bp" });
                return new Command(1, new[] { i });
            }
        }
        if (p.Player == 1 && p.Kind == Ask.Battle)
        {
            int i = p.Options.FindIndex(o => o.Act == Act.Attack && o.Target != null);
            if (i < 0) i = p.Options.FindIndex(o => o.Act == Act.Attack);
            if (i >= 0) return new Command(1, new[] { i });
        }
        if (p.Kind == Ask.Position) { int i = p.Options.FindIndex(o => o.Text == "Attack Position"); return new Command(p.Player, new[] { Math.Max(0, i) }); }
        return Answer(p);
    }

    /// Fixed answers: the opponent passes / takes the first option; we take the first legal picks and say yes.
    static Command Answer(Prompt p)
    {
        switch (p.Kind)
        {
            case Ask.Respond: return new Command(p.Player, new[] { p.Options.Count - 1 });
            case Ask.YesNo: { int y = p.Options.FindIndex(o => o.Text == "Yes"); return new Command(p.Player, new[] { p.Player == 0 && y >= 0 ? y : Math.Max(0, p.Options.FindIndex(o => o.Text == "No")) }); }
            case Ask.Pick:
            {
                int n = Math.Min(p.Options.Count, Math.Max(p.Min, p.Player == 0 ? Math.Min(1, p.Max) : p.Min));
                return new Command(p.Player, Enumerable.Range(0, n).ToArray());
            }
            case Ask.Main:
            {
                int e = p.Options.FindIndex(o => o.Act == Act.EndTurn);
                return new Command(p.Player, new[] { e >= 0 ? e : p.Options.Count - 1 });
            }
            case Ask.Battle:
            {
                int e = p.Options.FindIndex(o => o.Act is Act.ToMain2 or Act.EndTurn);
                return new Command(p.Player, new[] { e >= 0 ? e : p.Options.Count - 1 });
            }
            default: return new Command(p.Player, new[] { 0 });
        }
    }

    static Duel Stage(CardData d, Where w, bool rich, int turn)
    {
        var db = CardDb.Shared;
        var named = Support(d);
        if (Debug && w == Where.Hand && rich) Console.WriteLine("  support: " + string.Join(", ", named.Select(n => $"{n.Name} ({n.Race})")));
        var fusions = db.ByIdx.Values.Where(f => f.IsFusion && (f.Desc.Contains('"' + d.Name + '"', StringComparison.OrdinalIgnoreCase)
            || Regex.Matches(d.Desc, "\"([^\"]+)\"").Any(m => m.Groups[1].Value == f.Name))).Take(4).ToList();
        var s = new DuelSetup { Seed = 5, Classic = false, Shuffle = false };
        // P0 hand: the card (unless it's a Fusion), Neo, Elf, Pot, one named card; then named cards and fillers in the Deck.
        var p0 = new List<int>();
        if (!d.IsFusion) p0.Add(d.Idx);
        p0.AddRange(new[] { Neo, Elf, Pot }.Select(n => db.Named(n).Idx));
        foreach (var n in named) p0.Add(n.Idx);
        p0.AddRange(new[] { Neo, Elf, Neo, Elf, Pot }.Select(n => db.Named(n).Idx));
        foreach (var n in named) p0.Add(n.Idx);
        foreach (var n in named) p0.Add(n.Idx);   // 3 of each: hand, field, GY / Deck
        while (p0.Count < 40) p0.Add(db.Named(p0.Count % 2 == 0 ? Neo : Elf).Idx);
        s.Main[0].AddRange(p0);
        s.Main[1].AddRange(Enumerable.Range(0, 40).Select(i => db.Named(i % 3 == 2 ? Pot : i % 2 == 0 ? Neo : Elf).Idx));
        s.Extra[0].AddRange(fusions.Select(f => f.Idx).Append(d.IsFusion ? d.Idx : -1).Where(i => i >= 0).Distinct());
        var x = new Duel(s);
        var me = x.P(0); var them = x.P(1);
        Card From(Player pl, string name) => pl.Hand.Concat(pl.Deck).Concat(pl.Extra).First(c => c.Name == name);
        void ToGy(Player pl, Card c) { pl.Hand.Remove(c); pl.Deck.Remove(c); pl.Extra.Remove(c); c.Loc = Loc.Grave; pl.Grave.Add(c); }
        var self = d.IsFusion ? me.Extra.First(c => c.Data == d) : From(me, d.Name);
        switch (w)
        {
            case Where.Field: me.Hand.Remove(self); me.Extra.Remove(self); x.Place(self, 0, Loc.Monster); self.FaceUp = true; self.Defense = false; self.SummonTurn = 0; break;
            case Where.Set: me.Hand.Remove(self); x.Place(self, 0, Loc.Spell); self.FaceUp = false; self.SetTurn = 0; break;
            case Where.Grave: ToGy(me, self); break;
        }
        if (rich)
        {
            foreach (var (n, set) in new[] { (Neo, false), (Neo, false), (Elf, true) })
            { var c = them.Deck.First(c => c.Name == n); them.Deck.Remove(c); x.Place(c, 1, Loc.Monster); c.FaceUp = !set; c.Defense = set; c.SummonTurn = 0; }
            for (int i = 0; i < 2; i++) { var c = them.Deck.First(c => c.Name == Pot); them.Deck.Remove(c); x.Place(c, 1, Loc.Spell); c.FaceUp = false; c.SetTurn = 0; }
            foreach (var n in new[] { Neo, Elf, Pot }) ToGy(them, them.Deck.First(c => c.Name == n));
            // Our field: up to 2 supporting monsters face-up (else Neo), a face-down Elf, and named Field / Continuous Spells face-up.
            var mons = named.Where(n => n.IsMonster && !n.IsFusion).Select(n => n.Name).Take(2).ToList();
            while (mons.Count < 2) mons.Add(Neo);
            foreach (var n in mons) if (me.Deck.FirstOrDefault(c => c.Name == n) is { } c) { me.Deck.Remove(c); x.Place(c, 0, Loc.Monster); c.FaceUp = true; c.SummonTurn = 0; }
            { var c = me.Deck.First(c => c.Name == Elf); me.Deck.Remove(c); x.Place(c, 0, Loc.Monster); c.FaceUp = false; c.Defense = true; c.SummonTurn = 0; }
            foreach (var n in named.Where(n => n.IsSpell && (n.Is("Field") || n.Is("Continuous"))).Take(2))
                if (me.Deck.FirstOrDefault(c => c.Name == n.Name) is { } c) { me.Deck.Remove(c); x.Place(c, 0, n.Is("Field") ? Loc.Field : Loc.Spell); c.FaceUp = true; }
            foreach (var n in new[] { Neo, Elf, Pot }) ToGy(me, me.Deck.First(c => c.Name == n));
            foreach (var n in named.Take(2)) if (me.Deck.FirstOrDefault(c => c.Name == n.Name) is { } c) ToGy(me, c);
        }
        // Pass to turn 2 (their Main Phase) or 3 (ours, our monsters can attack): options are then built from the staged board.
        for (int i = 0; i < 200 && !x.Over && x.Pending is { } p && !(p.Kind == Ask.Main && p.Player == (turn == 3 ? 0 : 1) && x.Turn == turn); i++)
            x.Submit(Answer(p));
        return x;
    }

    /// Cards that make it usable: the cards its text names, its family, and vanilla monsters of the Types/Attributes it mentions.
    static List<CardData> Support(CardData d)
    {
        var db = CardDb.Shared;
        var l = Regex.Matches(d.Desc, "\"([^\"]+)\"").Select(m => m.Groups[1].Value).Distinct()
            .Where(n => db.ByName.ContainsKey(n)).Select(n => db.ByName[n][0]).ToList();
        l.AddRange(Families.Related(d).Where(r => r.Modern || r.IsMonster).Take(6));
        foreach (var race in new[] { "Beast-Warrior", "Winged Beast", "Sea Serpent", "Dragon", "Spellcaster", "Zombie", "Warrior", "Beast", "Fiend", "Fairy", "Insect", "Dinosaur", "Reptile", "Fish", "Aqua", "Pyro", "Thunder", "Rock", "Plant", "Machine" })
            if (Regex.IsMatch(d.Desc, race + "(-Type)? (monster|Fusion|Normal|Effect)"))
            {
                l.AddRange(db.ByIdx.Values.Where(c => c.IsNormalMonster && c.Race == race && c.Level <= 4 && !c.Modern).Take(1));
                l.AddRange(db.ByIdx.Values.Where(c => c.IsNormalMonster && c.Race == race && c.Level >= 5 && !c.Modern).Take(1));
                break;
            }
        foreach (var attr in new[] { "LIGHT", "DARK", "WATER", "FIRE", "EARTH", "WIND" })
            if (Regex.IsMatch(d.Desc, attr + " monster"))
                l.AddRange(db.ByIdx.Values.Where(c => c.IsNormalMonster && c.Attribute == attr && c.Level <= 4 && !c.Modern).Take(1));
        return l.Where(c => c.Name != d.Name && !c.IsFusion).GroupBy(c => c.Name).Select(g => g.First()).Take(8).ToList();
    }

    static string Fp(Duel x, Card self, CardData d)
    {
        var sb = new System.Text.StringBuilder();
        string C(Card c) => c == self && !d.IsMonster ? "" : $"{c.Name}:{(c.FaceUp ? 1 : 0)}{(c.Defense ? 1 : 0)}{x.Atk(c)}/{x.Def(c)}/{c.Controller}/{string.Join(",", c.Vars.Where(v => !v.Key.StartsWith("hopt")).Select(v => v.Key + "=" + v.Value))}";
        for (int p = 0; p < 2; p++)
        {
            var pl = x.P(p);
            sb.Append($"|{pl.Lp}|X{pl.ExtraSummons}|K{string.Join(",", pl.Deck.Select(c => c.Uid))}|H{string.Join(",", pl.Hand.Where(c => c != self).Select(c => c.Name).OrderBy(n => n))}|D{pl.Deck.Count}|E{pl.Extra.Count}");
            sb.Append($"|G{string.Join(",", pl.Grave.Where(c => c != self).Select(c => c.Name).OrderBy(n => n))}|B{string.Join(",", pl.Banished.Where(c => c != self).Select(c => c.Name))}");
            foreach (var c in pl.Monsters) sb.Append(c == null ? "|-" : "|" + C(c));
            foreach (var c in pl.Spells) sb.Append(c == null ? "|-" : "|" + C(c));
            sb.Append("|F" + (pl.FieldZone is { } f ? C(f) : "-"));
        }
        sb.Append("|T" + string.Join(",", x.TurnFlags.OrderBy(t => t)) + "|BF" + string.Join(",", x.BattleFlags.OrderBy(t => t)) + "|AN" + x.AttackNegated);
        sb.Append("|LV" + string.Join(",", x.P(0).Hand.Concat(x.P(1).Hand).Where(c => c.Data.IsMonster).Select(c => x.Level(c))));
        sb.Append($"|S{self.Loc}{self.EquippedTo?.Uid}:{string.Join(",", self.Vars.Where(v => !v.Key.StartsWith("hopt") && !v.Key.StartsWith("used")).Select(v => v.Key + "=" + v.Value))}|W{x.LingerCount}");
        sb.Append("|L" + string.Join(",", x.Flags.Where(f => !f.Key.StartsWith("hopt") && !f.Key.StartsWith("paid")).OrderBy(f => f.Key).Select(f => f.Key + "=" + f.Value)));
        return sb.ToString();
    }
}
