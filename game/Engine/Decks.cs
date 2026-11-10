namespace PoC.Engine;

/// A player's deck: card idx lists for the main deck, the Fusion Deck and the Side Deck (cards to swap in
/// between the duels of a match), plus the cover card shown on its case.
public sealed class Deck
{
    public string Name;
    public int Cover = -1;   // card idx; -1 = pick automatically
    public readonly List<int> Main = new(), Extra = new(), Side = new();
    public Deck(string name, IEnumerable<int>? main = null, IEnumerable<int>? extra = null)
    {
        Name = name;
        if (main != null) Main.AddRange(main);
        if (extra != null) Extra.AddRange(extra);
    }
    public Deck Copy(string? name = null) { var d = new Deck(name ?? Name, Main, Extra) { Cover = Cover }; d.Side.AddRange(Side); return d; }
    public bool SameAs(Deck o) => Name == o.Name && Cover == o.Cover && Main.Order().SequenceEqual(o.Main.Order()) && Extra.Order().SequenceEqual(o.Extra.Order()) && Side.Order().SequenceEqual(o.Side.Order());
    IEnumerable<int> All => Main.Concat(Extra).Concat(Side);
    public int Copies(int idx) => All.Count(x => x == idx);
    /// Copies of a card by name: alternate-art printings (two Blue-Eyes, two Polymerization) count together.
    public int CopiesNamed(string name) => All.Count(i => CardDb.Shared.Get(i).Name == name);

    /// The cover card: the chosen one if still in the deck, else the strongest monster.
    public int CoverCard()
    {
        if (Cover >= 0 && Copies(Cover) > 0) return Cover;
        var db = CardDb.Shared;
        var all = Main.Concat(Extra).Select(db.Get).ToList();
        return all.OrderByDescending(c => c.IsMonster).ThenByDescending(c => c.Atk).FirstOrDefault()?.Idx ?? -1;
    }
}

/// Deck files. `.ydc` is the original games' binary deck (u8 version 1, 3 pad bytes, u32 checksum, then main,
/// fusion and side, each u16 count + u16 PoC card ids; id 2000 is Kaiba's Blue-Eyes). `.ydk` is the YGOPro/EDOPro
/// text deck (passcodes under #main / #extra / !side), so decks also travel to and from other simulators.
public static class DeckFile
{
    // ponytail: the .ydc checksum algorithm isn't decoded; we write 0 and ignore it on read.
    // The remake reads its own files fine; whether the original exe accepts them is untested.
    public static byte[] ToYdc(Deck d)
    {
        var o = new List<byte> { 1, 0, 0, 0, 0, 0, 0, 0 };
        foreach (var part in new[] { d.Main, d.Extra, d.Side })
        {
            o.AddRange(BitConverter.GetBytes((ushort)part.Count));
            foreach (var i in part) o.AddRange(BitConverter.GetBytes((ushort)CardDb.Shared.Get(i).KonamiId));
        }
        return o.ToArray();
    }

    public static string ToYdk(Deck d)
    {
        string P(IEnumerable<int> l) => string.Concat(l.Select(i => CardDb.Shared.Get(i).RealPasscode + "\n"));
        return $"#created by Chaos Rift\n#main\n{P(d.Main)}#extra\n{P(d.Extra)}!side\n{P(d.Side)}";
    }

    /// Read either format (by content: .ydk is text). Cards the game doesn't know are skipped and counted.
    public static (Deck Deck, int Unknown) Read(byte[] b, string name)
    {
        var db = CardDb.Shared;
        var deck = new Deck(name);
        int unknown = 0;
        void Put(CardData? c, int part)
        {
            if (c == null) { unknown++; return; }
            int i = db.Canon(c.Idx);
            (part == 2 ? deck.Side : c.IsFusion ? deck.Extra : deck.Main).Add(i);   // Fusions land in the Fusion Deck whatever the file says
        }
        if (b.Length >= 8 && b[0] == 1)   // pad bytes are junk in the originals (Yugi's is fc 1a 00)
        {
            int o = 8;
            for (int part = 0; part < 3 && o + 2 <= b.Length; part++)
            {
                int n = BitConverter.ToUInt16(b, o); o += 2;
                for (int k = 0; k < n && o + 2 <= b.Length; k++, o += 2)
                {
                    int id = BitConverter.ToUInt16(b, o);
                    Put(db.ByKonami.TryGetValue(id == 2000 ? 0 : id, out var l) ? l[0] : null, part);
                }
            }
            return (deck, unknown);
        }
        var byPass = db.ByIdx.Values.Where(c => c.RealPasscode > 0).GroupBy(c => c.RealPasscode).ToDictionary(g => g.Key, g => g.First());
        int sec = 0;
        foreach (var raw in System.Text.Encoding.UTF8.GetString(b).Split('\n'))
        {
            var line = raw.Trim();
            if (line.StartsWith("#main")) sec = 0;
            else if (line.StartsWith("#extra")) sec = 1;
            else if (line.StartsWith("!side")) sec = 2;
            else if (int.TryParse(line, out var pc)) Put(byPass.GetValueOrDefault(pc), sec);
        }
        return (deck, unknown);
    }
}

/// Deck rules: a 40-80 card main deck, a Side Deck of up to 15, at most 3 copies across all of it (fewer on
/// the limit list), Fusion monsters in the Fusion Deck (or the Side Deck). A player can't use more copies than they own.
public static class DeckRules
{
    public const int MinMain = 40, MaxMain = 80, MaxSide = 15, MaxCopies = 3;

    /// Limit lists by format: the 2003 list (Kaiba's game) and the 2004 list (Joey's game).
    // ponytail: Master Duel's list needs a source; one more line here once it exists in rule_tables.json.
    public static readonly (string Name, string Key)[] Formats = { ("2003", "kaiba"), ("2004", "joey") };

    /// Format in force for duels: a CardDb.Limits key. 2004 by default; a settings choice later.
    public static string LimitGame = "joey";

    /// The two original starter decks (inferred from the data: kaiba/deck_k, joey/deck_j).
    // The player decks each original game ships (`deck.ydc` in its install folder; Kaiba's and Joey's match deck_k/deck_j).
    /// One per rival (the deck key starts with the rival file); Jaden's is the remake's own (remastered_decks.json).
    public static readonly (string Name, string Key)[] Starters = { ("Yugi Starter", "yugi/deck_y"), ("Kaiba Starter", "kaiba/deck_k"), ("Joey Starter", "joey/deck_j"), ("Jaden Starter", "jaden/x_starter") };

    /// A CPU deck for duels under `list`: under the 2003 list the CPU keeps its decks as they were (the originals
    /// ignore limits); under 2004 every forbidden or over-limit copy is swapped for a legal staple, so the CPU
    /// plays by the same list as you.
    public static List<int> ForCpu(IEnumerable<int> main, string? list = null)
    {
        list ??= LimitGame;
        var deck = main.ToList();
        if (list == "kaiba") return deck;
        var db = CardDb.Shared;
        var fillers = new[] { "Fissure", "Trap Hole", "Mystical Space Typhoon", "Dust Tornado", "Nobleman of Crossout", "Waboku" }.Select(db.Named).ToList();
        int Named(string n) => deck.Count(i => db.Get(i).Name == n);
        for (int k = 0; k < deck.Count; k++)
        {
            var c = db.Get(deck[k]);
            if (Named(c.Name) <= Limit(c, list)) continue;
            var f = fillers.FirstOrDefault(x => Named(x.Name) < Limit(x, list));
            if (f == null) { deck.RemoveAt(k--); continue; }
            deck[k] = f.Idx;
        }
        return deck;
    }

    /// Copies a limit list allows: 0 forbidden, 1 limited, 2 semi-limited, otherwise 3.
    public static int Limit(CardData d, string? list = null) =>
        CardDb.Shared.Limits[list ?? LimitGame].TryGetValue(d.KonamiId, out var m) ? m : MaxCopies;

    /// Copies of this printing a deck may hold: the limit, capped by what the player owns.
    public static int Allowed(CardData d, Profile p) => Math.Min(Limit(d), p.Count(d.Idx));

    public static bool CanAdd(Deck deck, CardData d, Profile p, bool side = false) =>
        deck.Copies(d.Idx) < p.Count(d.Idx) && deck.CopiesNamed(d.Name) < Limit(d)
        && (side ? deck.Side.Count < MaxSide : d.IsFusion || deck.Main.Count < MaxMain);

    /// Add a card to the Side Deck, or to the part of the deck it belongs in (Fusion monsters go to the Fusion Deck).
    public static bool Add(Deck deck, CardData d, Profile p, bool side = false)
    {
        if (!CanAdd(deck, d, p, side)) return false;
        (side ? deck.Side : d.IsFusion ? deck.Extra : deck.Main).Add(d.Idx);
        return true;
    }

    public static bool Remove(Deck deck, CardData d, bool side = false) => (side ? deck.Side : d.IsFusion ? deck.Extra : deck.Main).Remove(d.Idx);

    /// Side-decking between the duels of a match: one card out of the main (or Fusion) Deck for one Side Deck
    /// card, so both keep their size. A Fusion monster only trades with a Fusion monster.
    public static bool Swap(Deck deck, CardData out_, CardData in_)
    {
        if (out_.IsFusion != in_.IsFusion) return false;
        var part = out_.IsFusion ? deck.Extra : deck.Main;
        if (!part.Contains(out_.Idx) || !deck.Side.Contains(in_.Idx)) return false;
        part.Remove(out_.Idx); deck.Side.Remove(in_.Idx);
        part.Add(in_.Idx); deck.Side.Add(out_.Idx);
        return true;
    }

    /// Move one copy between the Side Deck and the main/Fusion Deck.
    public static bool Move(Deck deck, CardData d, bool toSide)
    {
        if (toSide ? deck.Side.Count >= MaxSide : !d.IsFusion && deck.Main.Count >= MaxMain) return false;
        if (!Remove(deck, d, !toSide)) return false;
        (toSide ? deck.Side : d.IsFusion ? deck.Extra : deck.Main).Add(d.Idx);
        return true;
    }

    public static bool IsLegal(Deck deck, Profile p, string? list = null) => Problem(deck, p, list) == null;

    /// Why the deck can't be used under a limit list (short, for the UI), or null when it is legal.
    public static string? Problem(Deck deck, Profile p, string? list = null)
    {
        var db = CardDb.Shared;
        foreach (var i in deck.Main.Concat(deck.Extra).Concat(deck.Side).Distinct())
        {
            var d = db.Get(i);
            if (d.IsFusion ? deck.Main.Contains(i) : deck.Extra.Contains(i)) return d.IsFusion ? $"{d.Name}: Fusion Deck only" : $"{d.Name}: not a Fusion";
            if (deck.Copies(i) > p.Count(i)) return $"Not enough {d.Name}";
            int lim = Limit(d, list);
            if (lim == 0) return $"{d.Name} is forbidden";
            if (deck.CopiesNamed(d.Name) > lim) return $"Too many {d.Name}";
        }
        if (deck.Main.Count > MaxMain) return $"Main Deck over {MaxMain}";
        if (deck.Side.Count > MaxSide) return $"Side Deck over {MaxSide}";
        return deck.Main.Count < MinMain ? $"{MinMain - deck.Main.Count} more card{(MinMain - deck.Main.Count == 1 ? "" : "s")} needed" : null;
    }

    public static CardData Polymerization => CardDb.Shared.Named("Polymerization");

    /// What a Fusion monster is made from (first recipe), plus Polymerization. Empty if it has no recipe.
    public static List<CardData> Materials(CardData fusion)
    {
        var db = CardDb.Shared;
        var f = db.Fusions.FirstOrDefault(r => db.ByKonami.TryGetValue(r.Result, out var l) && l.Any(c => c.Name == fusion.Name));
        if (f == null)
        {
            // Newer Fusions (anime pool): their first text line lists the materials; named cards there are the recipe.
            var line = fusion.Desc.Split('\n')[0];
            var named = System.Text.RegularExpressions.Regex.Matches(line, "\"([^\"]+)\"").Select(m => m.Groups[1].Value)
                .Where(n => n != fusion.Name && db.ByName.ContainsKey(n)).Select(db.Named).ToList();
            // no Polymerization here: these decks also fuse with Miracle Fusion, Fusion Gate, Power Bond...
            return named;
        }
        CardData M(int konami) => db.ByKonami[konami][0];
        // Blue-Eyes Ultimate Dragon is listed as BEWD + BEWD; it takes three.
        var mats = f.A == f.B && fusion.Name == "Blue-Eyes Ultimate Dragon" ? new List<CardData> { M(f.A), M(f.A), M(f.A) } : new List<CardData> { M(f.A), M(f.B) };
        mats.Add(Polymerization);
        return mats;
    }

    /// What a card depends on: a Fusion's materials; a Ritual monster's Ritual Spell (and back); otherwise every
    /// card its text names in quotes ("Dark Magician" for Dark Magic Attack, the LV4 for a LV6). Tokens are skipped.
    public static List<CardData> Needs(CardData d)
    {
        if (d.IsFusion) return Materials(d);
        var db = CardDb.Shared;
        bool Is(int konami, CardData c) => db.ByKonami.TryGetValue(konami, out var l) && l.Any(x => x.Name == c.Name);
        var names = db.Rituals.Where(r => Is(r.Monster, d)).SelectMany(r => db.ByKonami.GetValueOrDefault(r.Spell) ?? new())
            .Concat(db.Rituals.Where(r => Is(r.Spell, d)).SelectMany(r => db.ByKonami.GetValueOrDefault(r.Monster) ?? new()))
            .Select(c => c.Name)
            .Concat(System.Text.RegularExpressions.Regex.Matches(d.Desc, "\"([^\"]+)\"").Select(m => m.Groups[1].Value));
        return names.Distinct().Where(n => n != d.Name && !n.EndsWith("Token") && db.ByName.ContainsKey(n)).Select(db.Named).ToList();
    }

    /// What `card` needs that the deck lacks (by name, so any printing counts).
    public static List<CardData> MissingMaterials(Deck deck, CardData card)
    {
        var have = deck.Main.Concat(deck.Extra).Select(CardDb.Shared.Get).Select(c => c.Name).ToList();
        var missing = new List<CardData>();
        foreach (var m in Needs(card))
            if (!have.Remove(m.Name)) missing.Add(m);
        return missing;
    }

    /// Fusion Deck monsters whose materials (or Polymerization) aren't all in the main deck.
    public static List<CardData> FusionsMissingMaterials(Deck deck) =>
        deck.Extra.Distinct().Select(CardDb.Shared.Get).Where(f => MissingMaterials(deck, f).Count > 0).ToList();

    /// Add what a Fusion monster needs from the player's cards: one printing per missing material that
    /// the limits and the collection still allow. Returns the cards it could not add.
    public static List<CardData> AddMaterials(Deck deck, CardData fusion, Profile p)
    {
        var db = CardDb.Shared;
        var failed = new List<CardData>();
        foreach (var m in MissingMaterials(deck, fusion))
        {
            var printing = db.ByName[m.Name].FirstOrDefault(c => CanAdd(deck, c, p));
            if (printing == null) failed.Add(m); else Add(deck, printing, p);
        }
        return failed;
    }

    /// Give a new player a starter deck: its cards join the collection and it becomes their first deck.
    /// Cards the limit list forbids stay in the collection but out of the deck; the deck is topped back up
    /// to 40 with extra copies of its own Normal monsters (granted too).
    public static Deck GrantStarter(Profile p, int which)
    {
        var db = CardDb.Shared;
        var (name, key) = Starters[which];
        p.Starters.Add(key.Split('/')[0]);
        var (main, extra) = db.Deck(key);
        main = main.Select(db.Canon).ToList(); extra = extra.Select(db.Canon).ToList();
        foreach (var i in main.Concat(extra)) p.Owned[i] = Math.Min(255, p.Count(i) + 1);
        var deck = new Deck(name, main.Where(i => Limit(db.Get(i)) > 0), extra.Where(i => Limit(db.Get(i)) > 0));
        foreach (var i in main.Where(i => db.Get(i).IsNormalMonster).Distinct().ToList())
        {
            if (deck.Main.Count >= MinMain) break;
            if (deck.CopiesNamed(db.Get(i).Name) >= Limit(db.Get(i))) continue;
            p.Owned[i] = Math.Max(p.Count(i), deck.Copies(i) + 1);
            deck.Main.Add(i);
        }
        return deck;
    }

    /// First win against a rival: their starter deck, unless you already have it. Null otherwise.
    public static Deck? UnlockStarter(Profile p, string rival)
    {
        int k = Array.FindIndex(Starters, s => s.Key.StartsWith(rival + "/"));
        return k < 0 || p.Starters.Contains(rival) ? null : GrantStarter(p, k);
    }
}
