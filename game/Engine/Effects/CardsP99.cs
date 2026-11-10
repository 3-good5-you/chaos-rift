namespace PoC.Engine;

// Official cards auto-approved (user, 2026-10-30): their whole effect is one plain engine action with no timing or ruling
// question (stat boosts, piercing, draw/search/burn, simple destroy/banish), so they skip the card lab. Plain Fusion
// Monsters are approved in Effects/Pool.cs. Later batches mark their own with Sure(...).
public static partial class Scripts
{
    static void CardsP99(CardDb db)
    {
        Sure("Airknight Parshath", "Amazoness Paladin", "Amazoness Swords Woman", "Ancient Gear Golem", "Ancient Gear Soldier", "Ancient Gear Tank",
            "Ancient Gear Workshop", "Bowganian", "Burden of the Mighty", "Book of Moon", "Bonfire", "Burst Stream of Destruction",
            "Chaos End", "Chaos Greed", "Chaos Necromancer", "Cemetary Bomb", "Command Knight", "Compulsory Evacuation Device", "Chthonian Alliance",
            "Cup of Ace", "Cyber End Dragon", "Cyber Twin Dragon", "D.3.S. Frog", "Dark Driceratops", "Dark Factory of Mass Production",
            "Dark Mirror Force", "Dark World Dealings", "Darkness Destroyer", "Des Koala", "Destiny Draw", "Dimension Fusion", "Dimensional Prison",
            "Double Summon", "Draining Shield", "Dragon Master Knight", "Dragon's Rage", "E - Emergency Call", "Earthquake", "Ebon Magician Curran");
    }
}
