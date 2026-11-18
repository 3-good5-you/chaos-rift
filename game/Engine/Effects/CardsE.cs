namespace PoC.Engine;

// PoC cards that had no script and played as blanks (2026-11-16 deck audit: Engine.Check "no blank effect cards").
public static partial class Scripts
{
    /// The opponent's monster Relinquished / Thousand-Eyes Restrict holds as an Equip Card.
    static Card? Absorbed(Ctx x) => x.D.EquipsOf(x.Self).FirstOrDefault(e => e.Var("absorbed") == x.Self.Uid + 1);

    /// "This monster can take on the ATK and DEF of 1 opponent's monster on the field (a face-down monster results in an
    /// ATK and DEF of 0). Treat the selected monster as an Equip Spell Card ... only once per turn ... only 1 monster at a
    /// time." Relinquished also shields itself with it in battle and shares the battle damage it takes.
    static Script Absorber(string name)
    {
        var s = S(name);
        s.OncePerTurn = true;
        s.Note = "The absorbed monster sits in your Spell & Trap Zone as an Equip Card; this card's ATK and DEF become its (0 if it was face-down).";
        s.Can = x => x.Self.Loc == Loc.Monster && x.Self.FaceUp && Absorbed(x) == null && x.My.FreeSpellZones > 0 && x.Targetable(x.OppMonsters()).Any();
        s.Cost = async x => await x.Target(x.OppMonsters(), $"{name}: absorb which monster?");
        s.Run = async x =>
        {
            if (x.T0 is not { } t || !t.Valid() || t.Loc != Loc.Monster || !x.Self.Valid() || Absorbed(x) != null || x.My.FreeSpellZones == 0) return;
            bool up = t.FaceUp;
            if (!x.D.Place(t, x.Me, Loc.Spell)) return;
            t.FaceUp = true;
            t.Vars["absorbed"] = x.Self.Uid + 1;
            t.Vars["absorbedup"] = up ? 1 : 0;
            await x.D.Equip(t, x.Self);
        };
        s.Base = (x, c, st) =>
        {
            if (c != x.Self || Absorbed(x) is not { } e) return;
            bool up = e.Var("absorbedup") == 1;
            st.BaseAtk = up ? e.Data.Atk : 0;
            st.BaseDef = up ? e.Data.Def : 0;
        };
        return s;
    }

    static void CardsE(CardDb db)
    {
        {
            var s = Absorber("Relinquished");
            // "When your opponent's monster attacks this monster with an ATK higher than Relinquished, the equipped monster
            // is destroyed instead"; "damage you receive from the attacking monster ... is also inflicted on your opponent".
            s.DestroyInstead = async (x, battle) =>
            {
                if (!battle || Absorbed(x) is not { } e || x.D.Attacker is not { } a || a.Controller != x.Opp || x.D.AttackTarget != x.Self) return false;
                await x.D.Destroy(e, x.Self);
                return true;
            };
            s.On = async (x, e) =>
            {
                if (e.Kind == EvKind.BattleDamage && e.Player == x.Me && e.Amount > 0 && x.Self.Loc == Loc.Monster && Absorbed(x) != null
                    && e.Card is { } a && a.Controller == x.Opp && x.D.AttackTarget == x.Self)
                    await x.D.Damage(x.Opp, e.Amount, x.Self);
            };
        }
        {
            var s = Absorber("Thousand-Eyes Restrict");
            // "As long as this card remains face-up on the field, other monsters cannot change their positions or attack."
            s.Forbid = (x, q, c) => q is Q.Attack or Q.ChangePos && c != null && c != x.Self && x.Self.Loc == Loc.Monster && x.Self.FaceUp;
        }
        {
            var s = S("Cyber Harpie");   // "This card is treated as "Harpie Lady"."
            s.TreatedAs = "Harpie Lady";
            s.TreatedAlways = true;
        }
        {
            // Continuous Trap: their face-up monster loses 700 ATK and cannot change position; this card goes with it.
            var s = Spell("Shadow Spell", async x =>
            {
                x.Self.Vars["stay"] = 1;
                if (x.T0 is { } t && t.Valid()) x.Self.Vars["target"] = t.Uid + 1;
            },
            x => x.Targetable(x.OppMonsters().Where(m => m.FaceUp)).Any(),
            async x => await x.Target(x.OppMonsters().Where(m => m.FaceUp), "Shadow Spell: which monster?"));
            static bool Holds(Ctx x, Card? c) => c != null && x.Self.OnField && x.Self.FaceUp && c.Uid + 1 == x.Self.Var("target");
            s.Stats = (x, c, st) => { if (Holds(x, c)) st.Add(-700, 0); };
            s.Forbid = (x, q, c) => q == Q.ChangePos && Holds(x, c);
            s.On = async (x, e) => { if (e.Kind == EvKind.LeftField && e.Card!.Uid + 1 == x.Self.Var("target") && x.Self.OnField) await x.D.Destroy(x.Self); };
        }
    }
}
