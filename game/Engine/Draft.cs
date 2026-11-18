namespace PoC.Engine;

/// Pack Duel draft (PLAN_PACK_DUEL.md): two rival decks are dealt into ten packs of eight (4 + 4), five per player.
/// Each round both players open a pack at the same time, keep 4 and get the other player's 4 leftovers; then one
/// shared bonus pack of Fusion / Ritual kits (each takes one, copies allowed). The result is two ~40-card decks.
/// Deterministic from (deck A, deck B, seed), so a LAN draft only sends the picks.
public sealed class Draft
{
    public const int Rounds = 5, Keep = 4, PackCount = 10;
    public readonly record struct Slot(int Card, int Src);   // Src: 0 = deck A, 1 = deck B

    public readonly string[] Source;
    public readonly List<Slot>[][] Packs;                    // [player][round]
    public readonly List<List<int>> Kits = new();            // the bonus pack; every kit the same size
    public readonly List<Slot>[] Kept = { new(), new() }, Got = { new(), new() };
    public readonly int[] Kit = { -1, -1 };
    public int Round;                                        // 0-4 packs, 5 bonus, 6 done
    public bool Done => Round > Rounds;
    public bool Bonus => Round == Rounds;

    static CardDb Db => CardDb.Shared;
    static string N(int idx) => Db.Get(idx).Name;

    public Draft(string a, string b, ulong seed)
    {
        Source = new[] { a, b };
        var rng = new Rng(seed);
        var packs = Enumerable.Range(0, PackCount).Select(_ => new List<Slot>()).ToArray();
        var kits = new List<List<int>>[2];
        for (int s = 0; s < 2; s++)
        {
            var cards = Prepare(Source[s], out kits[s]);
            rng.Shuffle(cards);
            // copies next to each other, then round-robin: two copies never land in the same pack
            var order = cards.GroupBy(N).SelectMany(g => g).ToList();
            for (int i = 0; i < order.Count; i++) packs[i % PackCount].Add(new Slot(order[i], s));
        }
        Unclash(packs);
        rng.Shuffle(packs);
        foreach (var p in packs) rng.Shuffle(p);
        Packs = new[] { packs.Take(Rounds).ToArray(), packs.Skip(Rounds).Take(Rounds).ToArray() };

        // bonus pack: up to 2 kits from each deck, topped up to 4 from whichever has more
        foreach (var k in kits) rng.Shuffle(k);
        Kits.AddRange(kits[0].Take(2)); Kits.AddRange(kits[1].Take(2));
        Kits.AddRange(kits[0].Skip(2).Concat(kits[1].Skip(2)).Take(4 - Kits.Count));
        int size = Kits.Count == 0 ? 0 : Kits.Max(k => k.Count);
        foreach (var k in Kits)
        {
            // pad with the kit's smallest monster (a Fusion material, a Ritual's tribute fodder)
            var pad = k.Select(Db.Get).Where(c => c.IsMonster && !c.IsFusion).OrderBy(c => c.Level).FirstOrDefault()?.Idx ?? k[^1];
            while (k.Count < size) k.Add(pad);
        }
    }

    /// A source deck ready to deal: at most 2 copies, Ritual pieces moved to kits, refilled from the side deck.
    /// `kits`: its Fusion kits (Fusion + materials + Polymerization) and Ritual kits (monster + spell + fodder).
    static List<int> Prepare(string key, out List<List<int>> kits)
    {
        var (main0, extra) = Db.Deck(key);
        var main = main0.Select(Db.Canon).ToList();
        var side = Db.DeckSide(key).Select(Db.Canon).ToList();
        kits = new();
        bool Is(int konami, int idx) => Db.ByKonami.TryGetValue(konami, out var l) && l.Any(c => c.Name == N(idx));
        var ritualNames = new HashSet<string>();
        foreach (var r in Db.Rituals)
        {
            int mon = main.FirstOrDefault(i => Is(r.Monster, i), -1);
            if (mon < 0 || !Db.ByKonami.TryGetValue(r.Spell, out var sp)) continue;
            int spell = Db.Canon(sp[0].Idx);
            ritualNames.Add(N(mon)); ritualNames.Add(N(spell));
            var fodder = main.Select(Db.Get).Where(c => c.IsMonster && c.Frame != "Ritual" && c.Level <= 4).OrderBy(c => c.Level).FirstOrDefault();
            var kit = new List<int> { mon, spell };
            if (fodder != null) kit.Add(fodder.Idx);
            kits.Add(kit);
        }
        main.RemoveAll(i => ritualNames.Contains(N(i)));
        int Count(string n) => main.Count(i => N(i) == n);
        var cut = new List<int>();
        for (int i = main.Count - 1; i >= 0; i--)
            if (Count(N(main[i])) > 2) { cut.Add(main[i]); main.RemoveAt(i); }
        bool Fits(int i, int max) { var c = Db.Get(i); return !c.IsFusion && !ritualNames.Contains(c.Name) && Count(c.Name) < max; }
        foreach (var i in side) { if (main.Count >= DeckRules.MinMain) break; if (Fits(i, 2)) main.Add(i); }
        // still short (small side deck): a third copy, as few as needed; cut copies first, then the side deck
        foreach (var i in cut.Concat(side)) { if (main.Count >= DeckRules.MinMain) break; if (Fits(i, 3)) main.Add(i); }
        foreach (var f in extra.Select(Db.Canon).Distinct())
        {
            var mats = DeckRules.Materials(Db.Get(f));
            if (mats.Count == 0) continue;
            var kit = new List<int> { f };
            kit.AddRange(mats.Select(m => Db.Canon(m.Idx)));
            if (!mats.Any(m => m.Name == "Polymerization")) kit.Add(Db.Canon(DeckRules.Polymerization.Idx));
            kits.Add(kit);
        }
        return main;
    }

    /// Where one pack holds a card twice (deck A and deck B share it), swap the B copy with a B card of another pack.
    static void Unclash(List<Slot>[] packs)
    {
        for (int k = 0; k < packs.Length; k++)
            for (int a = 0; a < packs[k].Count; a++)
            {
                var s = packs[k][a];
                if (s.Src != 1 || packs[k].Count(x => N(x.Card) == N(s.Card)) < 2) continue;
                for (int j = 0; j < packs.Length; j++)
                {
                    if (j == k || packs[j].Any(x => N(x.Card) == N(s.Card))) continue;
                    int b = packs[j].FindIndex(x => x.Src == 1 && !packs[k].Any(y => N(y.Card) == N(x.Card)));
                    if (b < 0) continue;
                    (packs[k][a], packs[j][b]) = (packs[j][b], packs[k][a]);
                    break;
                }
            }
    }

    /// This round's pack for a player.
    public List<Slot> Pack(int p) => Packs[p][Round];

    /// Both players' picks for the round: positions in their own pack (Keep of them). Leftovers go across.
    public void Pick(int[] keep0, int[] keep1)
    {
        if (Bonus || Done) throw new InvalidOperationException("no pack round");
        var keep = new[] { keep0, keep1 };
        for (int p = 0; p < 2; p++)
        {
            var pack = Pack(p);
            int need = Math.Min(Keep, pack.Count);
            if (keep[p].Distinct().Count() != need || keep[p].Any(i => i < 0 || i >= pack.Count)) throw new ArgumentException($"player {p}: pick {need} of the pack");
        }
        for (int p = 0; p < 2; p++)
        {
            var pack = Pack(p);
            for (int i = 0; i < pack.Count; i++) (keep[p].Contains(i) ? Kept[p] : Got[1 - p]).Add(pack[i]);
        }
        Round++;
        if (Bonus && Kits.Count == 0) Round++;
    }

    /// The bonus round: each player's kit (the same one is fine).
    public void PickKit(int k0, int k1)
    {
        if (!Bonus) throw new InvalidOperationException("not the bonus round");
        if (k0 < 0 || k0 >= Kits.Count || k1 < 0 || k1 >= Kits.Count) throw new ArgumentException("kit");
        (Kit[0], Kit[1]) = (k0, k1);
        Round++;
    }

    /// A player's drafted cards so far (kept + received + kit).
    public IEnumerable<int> Cards(int p)
    {
        var drafted = Kept[p].Concat(Got[p]).Select(s => s.Card).ToList();
        if (Kit[p] < 0) return drafted;
        var adds = KitAdds(p, Kit[p]);
        return drafted.Concat(Kits[Kit[p]].Where((_, j) => adds[j]));
    }

    /// Which of a kit's cards a player would get: kit cards obey the 2-copy cap too (a card you already hold
    /// twice, or three times from a short deck, is left out). `known`: count only the cards you kept (what the
    /// draft screen may show; received cards stay hidden until drawn).
    public bool[] KitAdds(int p, int k, bool known = false)
    {
        var have = (known ? Kept[p] : Kept[p].Concat(Got[p])).GroupBy(s => N(s.Card)).ToDictionary(g => g.Key, g => g.Count());
        return Kits[k].Select(i =>
        {
            int n = have.GetValueOrDefault(N(i));
            if (n >= 2) return false;
            have[N(i)] = n + 1;
            return true;
        }).ToArray();
    }

    public Deck Deck(int p, string name) =>
        new(name, Cards(p).Where(i => !Db.Get(i).IsFusion), Cards(p).Where(i => Db.Get(i).IsFusion));

    // ---------------------------------------------------------------- picking for the AI (and AUTO)

    /// The 4 positions the AI keeps: card value, leaning towards the source deck it kept more of. Like a player it
    /// only knows its own picks; the cards it received stay hidden.
    // ponytail: one heuristic for every rival; draft difficulty levels later (PLAN_PACK_DUEL.md).
    public int[] Auto(int p)
    {
        var pack = Pack(p);
        var held = Kept[p].ToList();
        int lean = held.Count(s => s.Src == 0) - held.Count(s => s.Src == 1);
        var names = held.Select(s => N(s.Card)).ToHashSet();
        double Score(Slot s)
        {
            var c = Db.Get(s.Card);
            double v = c.IsMonster ? Math.Max(c.Atk, c.Def) / 600.0 + (c.IsNormalMonster ? 0 : 1.2) - (c.Level >= 7 ? 1.0 : c.Level >= 5 ? 0.4 : 0) : 3.0;
            v += (s.Src == 0 ? lean : -lean) * 0.15;
            if (Needs(c).Any(names.Contains) || held.Any(h => Needs(Db.Get(h.Card)).Contains(c.Name))) v += 1.5;   // completes something
            return v;
        }
        return Enumerable.Range(0, pack.Count).OrderByDescending(i => Score(pack[i])).Take(Math.Min(Keep, pack.Count)).ToArray();
    }

    /// The kit that fits the deck best: most of its cards among your picks, then the strongest monster.
    public int AutoKit(int p)
    {
        var names = Kept[p].Select(s => N(s.Card)).ToList();
        return Enumerable.Range(0, Kits.Count).OrderByDescending(k => Kits[k].Count(i => names.Contains(N(i))) * 2000 + Kits[k].Max(i => Db.Get(i).Atk)).First();
    }

    static readonly Dictionary<int, List<string>> needs = new();
    static List<string> Needs(CardData c) => needs.TryGetValue(c.Idx, out var l) ? l : needs[c.Idx] = DeckRules.Needs(c).Select(x => x.Name).ToList();

    // ---------------------------------------------------------------- choosing the two decks

    /// Difficulty tier of a remastered deck key (rival ladders by level; tournament duelists count as NORMAL).
    public static string Tier(string key) =>
        key.StartsWith("tour/") ? "NORMAL" : key.EndsWith("x_starter") ? "EASY"
        : int.TryParse(key[(key.LastIndexOf("lv") + 2)..], out var lv) ? lv <= 2 ? "EASY" : lv <= 4 ? "NORMAL" : lv == 5 ? "HARD" : "EXPERT" : "";

    /// Decks that can be drafted: rival ladders and tournament duelists (no Exodia, no Gods).
    public static List<string> Pool() => Db.RemasteredDeckNames().Where(k => Tier(k) != "" && !k.Contains("exodia")).ToList();

    /// Two different decks of one tier (random, or `tier`), preferring a pair that makes a bonus pack.
    public static Draft Random(Rng rng, string? tier = null)
    {
        var pool = Pool();
        Draft? best = null;
        for (int tries = 0; tries < 12; tries++)
        {
            var t = tier ?? Tier(pool[rng.Int(pool.Count)]);
            var same = pool.Where(k => Tier(k) == t).ToList();
            var a = same[rng.Int(same.Count)];
            var b = same.Where(k => k != a && k.Split('/')[0] != a.Split('/')[0]).DefaultIfEmpty(same.First(k => k != a)).ToList();
            var d = new Draft(a, b[rng.Int(b.Count)], rng.Next());
            if (d.Kits.Count >= 2) return d;
            best ??= d;
        }
        return best!;
    }
}
