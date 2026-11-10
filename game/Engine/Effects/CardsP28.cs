namespace PoC.Engine;

// The three Egyptian God cards (2026-11-09): the original games listed them as blank 0/0 cards; the remake gives them
// their current official text (CardDb.Gods) and plays it in full.
public static partial class Scripts
{
    static void CardsP28(CardDb db)
    {
        // Shared by all three: 3 Tributes, never Set, the Normal Summon can't be answered (Script.SummonLock).
        Script God(string name)
        {
            var s = S(name);
            s.TributesNeeded = x => 3;
            s.NoSet = true;
            s.SummonLock = true;
            return s;
        }

        // "Once per turn, during the End Phase, if this card was Special Summoned: Send it to the GY."
        async Flow LeaveIfSpecial(Ctx x, Ev e)
        {
            if (e.Kind != EvKind.EndPhase || !Up(x) || x.Self.Summoned is not (SummonKind.Special or SummonKind.Fusion or SummonKind.Ritual)
                || x.Self.Var("godend") == x.D.Turn) return;
            x.Self.Vars["godend"] = x.D.Turn;
            await x.D.ToGrave(x.Self, x.Self);
        }

        {
            var s = God("Obelisk the Tormentor");
            s.Forbid = (x, q, c) => c == x.Self && Up(x) && (q == Q.Target || (q == Q.Attack && x.Self.Var("obeliskfx") == x.D.Turn));
            s.On = LeaveIfSpecial;
            // You can Tribute 2 monsters; destroy all monsters your opponent controls. It cannot attack this turn
            // (and, by ruling, the effect can't be used once it has attacked).
            s.Can = x => Up(x) && x.Self.Attacks == 0 && x.MyMonsters().Count(m => m != x.Self && !x.D.Forbidden(Q.Tribute, m)) >= 2;
            s.Cost = async x =>
            {
                foreach (var t in await x.PickN(x.MyMonsters().Where(m => m != x.Self && !x.D.Forbidden(Q.Tribute, m)), 2, 2, "Tribute 2 monsters"))
                    await x.D.Tribute(t, x.Self);
                x.Self.Vars["obeliskfx"] = x.D.Turn;
            };
            s.Run = async x => await x.D.Destroy(x.OppMonsters().ToList(), x.Self);
        }

        {
            var s = God("Slifer the Sky Dragon");
            s.Stats = (x, c, st) => { if (c == x.Self) { int n = 1000 * x.My.Hand.Count; st.Add(n, n); } };
            s.On = async (x, e) =>
            {
                await LeaveIfSpecial(x, e);
                if (!Up(x) || e.Kind != EvKind.Summoned || e.Arg == (int)SummonKind.Flip) return;
                foreach (var m in (e.Cards ?? new List<Card> { e.Card! }).Where(m => m.Controller == x.Opp && m.Valid() && m.FaceUp && m.Attack).ToList())
                {
                    int before = x.D.Atk(m);
                    m.Vars["modatk"] = m.Var("modatk") - 2000;
                    x.D.Seq++;
                    if (before > 0 && x.D.Atk(m) == 0) await x.D.Destroy(m, x.Self);
                }
            };
        }

        {
            var s = God("The Winged Dragon of Ra");
            s.SummonOk = (x, by, k) => false;   // "Cannot be Special Summoned" (Sphere Mode / Immortal Phoenix ignore it)
            s.On = async (x, e) =>
            {
                // "When this card is Normal Summoned: You can pay LP so that you only have 100 left; this card gains
                // ATK/DEF equal to the amount of LP paid."
                if (!e.Is(EvKind.Summoned, x.Self) || !e.Summoned(SummonKind.Normal, SummonKind.Tribute) || x.My.Lp <= 100) return;
                int pay = x.My.Lp - 100;
                if (!await x.Ask($"Pay {pay} LP so The Winged Dragon of Ra gains {pay} ATK/DEF?")) return;
                x.D.Pay(x.Me, pay, x.Self);
                x.Self.Vars["modatk"] = x.Self.Var("modatk") + pay;
                x.Self.Vars["moddef"] = x.Self.Var("moddef") + pay;
                x.D.Seq++;
            };
            // "You can pay 1000 LP, then target 1 monster on the field; destroy that target."
            s.Can = x => Up(x) && x.My.Lp > 1000 && x.Targetable(x.AllMonsters()).Any();
            s.Cost = async x => { x.D.Pay(x.Me, 1000, x.Self); await x.Target(x.AllMonsters(), "Destroy which monster?"); };
            s.Run = async x => { if (x.T0 is { } t && t.Valid()) await x.D.Destroy(t, x.Self); };
        }
    }
}
