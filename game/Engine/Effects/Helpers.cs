namespace PoC.Engine;

/// Small vocabulary the card scripts are written in.
public static class X
{
    public static IEnumerable<Card> MyMonsters(this Ctx x) => x.My.MonstersOnField;
    public static IEnumerable<Card> OppMonsters(this Ctx x) => x.Their.MonstersOnField;
    public static IEnumerable<Card> AllMonsters(this Ctx x) => x.D.AllMonsters;
    public static IEnumerable<Card> FaceUp(this IEnumerable<Card> l) => l.Where(c => c.FaceUp);
    public static IEnumerable<Card> FaceDown(this IEnumerable<Card> l) => l.Where(c => !c.FaceUp);
    public static IEnumerable<Card> Spells(this Ctx x, int p) => x.D.P(p).SpellsOnField;
    public static IEnumerable<Card> AllSpells(this Ctx x) => x.D.Players.SelectMany(p => p.SpellsOnField);

    /// Cards that effects may target (Lord of D. etc.).
    public static IEnumerable<Card> Targetable(this Ctx x, IEnumerable<Card> l) { x.D.Targeter = x.Self; return l.Where(c => !x.D.Forbidden(Q.Target, c, x.Me) && !(c.Data.IsMonster && c.Loc == Loc.Monster && x.Self.Data.IsSpell && x.D.Forbidden(Q.AffectedBySpell, c))
        && !(c.Loc == Loc.Monster && x.Self.Data.IsTrap && x.D.Forbidden(Q.AffectedByTrap, c)) && !(c.Loc == Loc.Monster && x.Self.Data.IsMonster && x.D.Unaffected(c, x.Self))).ToList(); }

    public static Flow<Card?> Pick(this Ctx x, IEnumerable<Card> l, string text, bool optional = false) => x.D.PickOne(x.Me, l, text, x.Self, optional);
    public static Flow<List<Card>> PickN(this Ctx x, IEnumerable<Card> l, int min, int max, string text) => x.D.Pick(x.Me, l, min, max, text, x.Self);
    public static Flow<bool> Ask(this Ctx x, string text) => x.D.YesNo(x.Me, text, x.Self);

    /// Pick a target now (at activation) and store it in x.T.
    public static async Flow Target(this Ctx x, IEnumerable<Card> l, string text, int n = 1)
    {
        var t = await x.D.Pick(x.Me, x.Targetable(l), n, n, text, x.Self);
        x.T.AddRange(t);
    }

    public static int Atk(this Ctx x, Card c) => x.D.Atk(c);
    public static int Def(this Ctx x, Card c) => x.D.Def(c);
    public static bool Valid(this Card c) => c.Loc == Loc.Monster;
    public static bool Is(this Ev e, EvKind k, Card c) => e.Kind == k && e.Card == c;
    public static bool IsRace(this Ctx x, Card c, string race) => x.D.Race(c) == race;
    public static bool IsAttr(this Ctx x, Card c, string a) => x.D.Attr(c) == a;
    public static bool Named(this Card c, string name) => c.Name == name;
    public static bool Turn(this Ctx x) => x.D.TurnPlayer == x.Me;
    public static bool Summoned(this Ev e, params SummonKind[] k) => e.Kind == EvKind.Summoned && k.Contains((SummonKind)e.Arg);
    public static bool IsNormalOrFlip(this Ev e) => e.Summoned(SummonKind.Normal, SummonKind.Tribute, SummonKind.Flip);

    /// Monsters in a deck search that the PoC recruiters may fetch (no Ritual/Fusion).
    public static IEnumerable<Card> DeckMonsters(this Ctx x, Func<Card, bool> f) => x.My.Deck.Where(c => c.Data.IsMonster && f(c));

    public static async Flow Search(this Ctx x, Func<Card, bool> f, string text, bool optional = false)
    {
        var c = await x.Pick(x.My.Deck.Where(f), text, optional);
        if (c != null) await x.D.ToHand(c, x.Self);
        x.D.Shuffle(x.Me);
    }

    public static async Flow Recruit(this Ctx x, Func<Card, bool> f, string text, bool? defense = null, bool faceDownAllowed = false)
    {
        var c = await x.Pick(x.My.Deck.Where(c => c.Data.IsMonster && !c.Data.IsRitual && !c.Data.IsFusion && f(c)), text, optional: true);
        if (c != null)
        {
            bool def = defense ?? await x.D.ChoosePosition(x.Me, c);
            await x.D.SpecialSummon(c, x.Me, def, faceUp: !(def && faceDownAllowed), by: x.Self);
        }
        x.D.Shuffle(x.Me);
    }

    public static bool InGrave(this Ctx x) => x.Self.Loc == Loc.Grave;
    public static bool OnFieldUp(this Ctx x) => x.Self.Loc == Loc.Monster && x.Self.FaceUp;
}
