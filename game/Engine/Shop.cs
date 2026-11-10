namespace PoC.Engine;

/// The Duelist Points shop: cards you have seen a rival play (unowned or under 3 copies), playmats and card backs.
/// DP come from duels (<see cref="Earn"/>). Cosmetics (mat, card back, icon frame) are owned and equipped separately:
/// any mat with any back and any frame.
public static class Shop
{
    public enum Cosmetic { Mat, Back, Frame }

    /// A playmat, card back or icon frame. Key = its file (assets/mats/<key>.jpg, assets/backs/<key>.png,
    /// assets/frames/<key>.png); price 0 = free from the start.
    public sealed record Item(string Key, string Name, int Price, Cosmetic Kind);

    public const string DefaultMat = "arena", DefaultBack = "classic", DefaultFrame = "classic";

    // ponytail: every cosmetic is bought with DP; milestone unlocks (beat all rivals on EXPERT...) come with milestones.
    public static readonly Item[] Mats =
    {
        new(DefaultMat, "Arena", 0, Cosmetic.Mat),
        new("magma", "Magma", 3000, Cosmetic.Mat), new("tide", "Tide", 3000, Cosmetic.Mat), new("storm", "Storm", 3000, Cosmetic.Mat), new("skyway", "Skyway", 3000, Cosmetic.Mat),
        new("radiant", "Radiant", 3000, Cosmetic.Mat), new("ruins", "Ruins", 2500, Cosmetic.Mat), new("pharaoh", "Pharaoh", 4000, Cosmetic.Mat), new("datastream", "Datastream", 2500, Cosmetic.Mat),
        new("cup_arena", "Cup Arena", 3500, Cosmetic.Mat), new("steel", "Steel", 2500, Cosmetic.Mat), new("twin_vortex", "Twin Vortex", 3500, Cosmetic.Mat), new("starlane", "Starlane", 3500, Cosmetic.Mat),
        new("art_dark_magician", "Dark Magician", 5000, Cosmetic.Mat), new("art_blue_eyes", "Blue-Eyes", 5000, Cosmetic.Mat), new("art_red_eyes", "Red-Eyes", 5000, Cosmetic.Mat),
        // Prize mats (price -1, never sold): a tournament win gives the next one you don't have (PrizeMat).
        new("obsidian", "Obsidian", -1, Cosmetic.Mat), new("ivory", "Ivory", -1, Cosmetic.Mat), new("eclipse", "Eclipse", -1, Cosmetic.Mat), new("crimson", "Crimson", -1, Cosmetic.Mat),
    };

    public static readonly Item[] Backs =
    {
        new(DefaultBack, "Classic", 0, Cosmetic.Back),
        new("01_vortex", "Vortex", 2000, Cosmetic.Back), new("02_silver_grid", "Silver Grid", 2000, Cosmetic.Back), new("03_light", "Light", 3000, Cosmetic.Back), new("04_dark", "Dark", 3000, Cosmetic.Back),
        new("05_earth", "Earth", 3000, Cosmetic.Back), new("06_water", "Water", 3000, Cosmetic.Back), new("07_fire", "Fire", 3000, Cosmetic.Back), new("08_wind", "Wind", 3000, Cosmetic.Back),
        new("09_circuit_blue", "Circuit", 2500, Cosmetic.Back), new("10_circuit_black", "Black Circuit", 2500, Cosmetic.Back), new("11_hyperspace", "Hyperspace", 3000, Cosmetic.Back),
        new("12_millennium", "Millennium", 4000, Cosmetic.Back), new("13_hex_cup", "Hex Cup", 3500, Cosmetic.Back),
        new("14_art_dark_magician", "Dark Magician", 4000, Cosmetic.Back), new("15_art_blue_eyes", "Blue-Eyes", 4000, Cosmetic.Back), new("16_art_red_eyes", "Red-Eyes", 4000, Cosmetic.Back),
        // v1.0: the player's own designs (art-source/card-backs), fitted to the card shape with a gold rim.
        new("17_crimson_sigil", "Crimson Sigil", 3500, Cosmetic.Back), new("18_ink_pharaoh", "Ink Pharaoh", 3500, Cosmetic.Back), new("19_neon_pharaoh", "Neon Pharaoh", 3500, Cosmetic.Back),
        new("20_millennium_puzzle", "Millennium Puzzle", 3500, Cosmetic.Back), new("21_sacred_geometry", "Sacred Geometry", 3500, Cosmetic.Back), new("22_red_eyes_fury", "Red-Eyes Fury", 3500, Cosmetic.Back),
        new("23_pharaoh_relic", "Pharaoh's Relic", 3500, Cosmetic.Back), new("24_slifer_scroll", "Slifer Scroll", 3500, Cosmetic.Back), new("25_golden_pharaoh", "Golden Pharaoh", 3500, Cosmetic.Back),
        new("26_yellow_puzzle", "Yellow Puzzle", 3500, Cosmetic.Back), new("27_spirit_duel", "Spirit Duel", 3500, Cosmetic.Back), new("28_neon_dragons", "Neon Dragons", 3500, Cosmetic.Back),
        new("29_gods_tapestry", "Gods Tapestry", 3500, Cosmetic.Back), new("30_chaos_vortex", "Chaos Vortex", 3500, Cosmetic.Back),
    };

    // ponytail: rank-style frames (bronze..master) are bought too; ranked play would award them instead.
    public static readonly Item[] Frames =
    {
        new(DefaultFrame, "Classic", 0, Cosmetic.Frame),
        new("bronze", "Bronze", 1000, Cosmetic.Frame), new("silver", "Silver", 1500, Cosmetic.Frame), new("gold", "Gold", 2000, Cosmetic.Frame),
        new("platinum", "Platinum", 2500, Cosmetic.Frame), new("diamond", "Diamond", 3000, Cosmetic.Frame), new("master", "Master", 4000, Cosmetic.Frame),
        new("light", "Light", 2500, Cosmetic.Frame), new("dark", "Dark", 2500, Cosmetic.Frame), new("earth", "Earth", 2500, Cosmetic.Frame),
        new("water", "Water", 2500, Cosmetic.Frame), new("fire", "Fire", 2500, Cosmetic.Frame), new("wind", "Wind", 2500, Cosmetic.Frame),
        new("millennium", "Millennium", 3500, Cosmetic.Frame), new("fusion", "Fusion", 3500, Cosmetic.Frame), new("rune", "Rune Circle", 3500, Cosmetic.Frame),
        new("chain", "Chain", 3000, Cosmetic.Frame), new("circuit", "Circuit", 3000, Cosmetic.Frame),
    };

    public static Item[] All(Cosmetic k) => k switch { Cosmetic.Mat => Mats, Cosmetic.Back => Backs, _ => Frames };
    static HashSet<string> Bought(Profile p, Cosmetic k) => k switch { Cosmetic.Mat => p.Mats, Cosmetic.Back => p.Backs, _ => p.Frames };
    public static string Equipped(Profile p, Cosmetic k) => k switch { Cosmetic.Mat => p.Mat, Cosmetic.Back => p.Back, _ => p.Frame };

    /// What a card costs: limited cards the most, then by strength.
    public static int Price(CardData d)
    {
        int lim = DeckRules.Limit(d);
        if (lim <= 1) return lim == 0 ? 2400 : 1800;
        if (lim == 2) return 1200;
        int p = d.IsMonster ? (d.IsNormalMonster ? 100 + (d.Atk + d.Def) / 10 : 300 + d.Atk / 6) : 450;
        return Math.Max(100, p / 50 * 50);
    }

    /// DP for a finished duel: a win pays by difficulty, a loss still pays a little.
    public static int Earn(int level, bool won) => !won ? 20 : level >= 6 ? 200 : level == 5 ? 100 : 50;

    /// After a duel: remember every card the rival revealed (face-up, in the Graveyard or banished), so the shop sells it.
    public static void Observe(Profile p, Duel d, int rival = 1)
    {
        foreach (var c in d.Cards.Where(c => c.Owner == rival && !c.IsToken && (c.Loc is Loc.Grave or Loc.Banished || (c.OnField && c.FaceUp))))
            p.Seen.Add(CardDb.Shared.Canon(c.Data.Idx));
    }

    /// Cards you can still buy (fewer than 3 copies): every collectable card (user rule 2026-10-29: seen or not).
    public static IEnumerable<CardData> ForSale(Profile p) =>
        CardDb.Shared.ByIdx.Values.Where(d => (d.PackMask != 0 || p.Seen.Contains(d.Idx)) && CardDb.Shared.Canon(d.Idx) == d.Idx && p.Count(d.Idx) < 3);

    public static bool Buy(Profile p, CardData d)
    {
        int price = Price(d);
        if (p.Count(d.Idx) >= 3 || p.Dp < price) return false;
        p.Dp -= price;
        p.Owned[d.Idx] = p.Count(d.Idx) + 1;
        p.Fresh.Add(d.Idx);
        return true;
    }

    /// A tournament win's playmat: the first prize mat you don't own yet (null once you have them all). Grants it.
    public static Item? PrizeMat(Profile p)
    {
        var it = Mats.FirstOrDefault(m => m.Price < 0 && !p.Mats.Contains(m.Key));
        if (it != null) p.Mats.Add(it.Key);
        return it;
    }

    public static bool Owns(Profile p, Item it) => it.Price == 0 || Bought(p, it.Kind).Contains(it.Key);

    public static bool Buy(Profile p, Item it)
    {
        if (Owns(p, it) || it.Price < 0 || p.Dp < it.Price) return false;   // prize mats are won, not bought
        p.Dp -= it.Price;
        Bought(p, it.Kind).Add(it.Key);
        return true;
    }

    /// Equip an owned mat, back or frame; false if you don't own it.
    public static bool Equip(Profile p, Item it)
    {
        if (!Owns(p, it)) return false;
        switch (it.Kind) { case Cosmetic.Mat: p.Mat = it.Key; break; case Cosmetic.Back: p.Back = it.Key; break; default: p.Frame = it.Key; break; }
        return true;
    }

    /// After loading: keep only known bought items; anything equipped but not owned falls back to the default.
    public static void Tidy(Profile p)
    {
        foreach (var k in Enum.GetValues<Cosmetic>()) Bought(p, k).RemoveWhere(key => All(k).All(i => i.Key != key));
        bool Ok(Cosmetic k, string key) => All(k).FirstOrDefault(i => i.Key == key) is { } it && Owns(p, it);
        if (!Ok(Cosmetic.Mat, p.Mat)) p.Mat = DefaultMat;
        if (!Ok(Cosmetic.Back, p.Back)) p.Back = DefaultBack;
        if (!Ok(Cosmetic.Frame, p.Frame)) p.Frame = DefaultFrame;
    }
}
