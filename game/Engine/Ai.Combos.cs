using System.Text.RegularExpressions;

namespace PoC.Engine;

/// Multi-card plans the one-card rules miss (2026-11-16 card scan: Ojama Delta Hurricane!!, XYZ / VWXYZ, Harpie Lady
/// Phoenix Formation never or almost never played). Read from card text, so any deck with the pieces benefits.
public sealed partial class Ai
{
    /// The monster names a contact fusion ("You do not use Polymerization") is made of.
    static string[] ContactMaterials(Card f) =>
        f.Data.Desc.Contains("do not use \"Polymerization\"") ? Regex.Matches(f.Data.Desc.Split('\n')[0], "\"([^\"]+)\"").Select(m => m.Groups[1].Value).ToArray() : Array.Empty<string>();

    IEnumerable<string> FieldNames(Duel d) => Me(d).MonstersOnField.Where(m => m.FaceUp).Select(Scripts.NameOf);
    static string AsNamed(Card c) => Scripts.Get(c.Data) is { TreatedAs: { } a, TreatedAlways: true } ? a : c.Name;   // Cyber Harpie in hand is a Harpie Lady

    /// Contact fusions in our Extra Deck whose every material is on our field, in our hand, or is `extra`.
    IEnumerable<(Card fusion, string[] mats)> LiveContacts(Duel d, Card? extra = null)
    {
        var have = FieldNames(d).Concat(Me(d).Hand.Select(h => h.Name)).Append(extra?.Name ?? "").ToHashSet();
        foreach (var f in Me(d).Extra)
            if (ContactMaterials(f) is { Length: > 0 } mats && mats.All(have.Contains)) yield return (f, mats);
    }

    /// Summoning `c` brings a held plan one step closer: a contact fusion all of whose pieces we hold, Ojama Delta
    /// Hurricane!! with the three Ojamas at hand, Harpie Lady Phoenix Formation with three Harpies at hand.
    bool ComboPiece(Duel d, Card c)
    {
        var field = FieldNames(d).ToList();
        if (field.Contains(AsNamed(c))) return false;
        if (LiveContacts(d, c).Any(x => x.mats.Contains(c.Name))) return true;
        var hand = Me(d).Hand;
        var both = field.Concat(hand.Select(AsNamed)).ToList();
        if (hand.Any(h => h.Name == "Ojama Delta Hurricane!!") && c.Name is "Ojama Green" or "Ojama Yellow" or "Ojama Black"
            && new[] { "Ojama Green", "Ojama Yellow", "Ojama Black" }.All(both.Contains)) return true;
        if (hand.Any(h => h.Name == "Harpie Lady Phoenix Formation") && AsNamed(c) is "Harpie Lady" or "Harpie Lady Sisters"
            && both.Count(n => n is "Harpie Lady" or "Harpie Lady Sisters") >= 3) return true;
        return false;
    }

    bool HoldsDelta(Duel d) => Me(d).Hand.Any(h => h.Name == "Ojama Delta Hurricane!!")
        && new[] { "Ojama Green", "Ojama Yellow", "Ojama Black" }.All(n => FieldNames(d).Concat(Me(d).Hand.Select(h => h.Name)).Contains(n));

    /// A union monster (Y-Dragon Head) stays a monster while it is a piece of a contact fusion we can make.
    bool KeepForContact(Duel d, Card c) => LiveContacts(d).Any(x => x.mats.Contains(c.Name));

    /// Making contact fusion `f` would eat the pieces of a bigger one we can make instead (XY-Dragon Cannon when Z-Metal
    /// Tank is at hand for XYZ-Dragon Cannon).
    bool Spoils(Duel d, Card f)
    {
        var mats = ContactMaterials(f);
        return mats.Length > 0 && LiveContacts(d).Any(x => x.fusion.Name != f.Name && x.mats.Length > mats.Length && mats.All(x.mats.Contains));
    }

    /// Shield & Sword: every face-up monster's ATK and DEF swap for the turn. Worth it when one of ours then beats
    /// everything they have, and didn't before (or now by far more).
    bool SwapWins(Duel d)
    {
        if (d.Phase != Phase.Main1 || d.Turn <= 1) return false;
        var mine = Me(d).MonstersOnField.Where(m => m.FaceUp && !m.Defense && d.CanAttack(m)).ToList();
        if (mine.Count == 0) return false;
        var theirs = Opp(d).MonstersOnField.ToList();
        int GuardAfter(Card t) => !t.FaceUp ? Guard(d, t) : t.Defense ? d.Atk(t) : d.Def(t);
        int before = mine.Max(d.Atk) - theirs.Select(t => Guard(d, t)).DefaultIfEmpty(0).Max();
        int after = mine.Max(d.Def) - theirs.Select(GuardAfter).DefaultIfEmpty(0).Max();
        return after > 0 && (before <= 0 || after >= before + 500);
    }
}
