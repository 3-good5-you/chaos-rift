namespace PoC.Engine;

// Card movement and LP primitives. Every state change that scripts can cause goes through here.
public sealed partial class Duel
{
    void Detach(Card c)
    {
        var p = P(c.Controller);
        var o = P(c.Owner);
        switch (c.Loc)
        {
            case Loc.Deck: if (!o.Deck.Remove(c)) P(1 - c.Owner).Deck.Remove(c); break;
            case Loc.Hand: if (!p.Hand.Remove(c)) { Players[0].Hand.Remove(c); Players[1].Hand.Remove(c); } break;
            case Loc.Grave: if (!o.Grave.Remove(c)) P(1 - c.Owner).Grave.Remove(c); break;
            case Loc.Banished: if (!o.Banished.Remove(c)) P(1 - c.Owner).Banished.Remove(c); break;
            case Loc.Extra: o.Extra.Remove(c); break;
            case Loc.Monster: if (p.Monsters[c.Zone] == c) p.Monsters[c.Zone] = null; break;
            case Loc.Spell: if (p.Spells[c.Zone] == c) p.Spells[c.Zone] = null; break;
            case Loc.Field: if (p.FieldZone == c) p.FieldZone = null; break;
        }
        c.Loc = Loc.None;
        c.Zone = -1;
    }

    /// Move a card. Handles leaving-the-field cleanup, tokens, Fusion monsters returning to the
    /// Fusion Deck and grave-to-banish replacement. Raises LeftField / ToGrave / ToHand.
    public async Flow MoveTo(Card c, Loc dest, bool top = true, Card? by = null, bool battle = false)
    {
        if (paying == 0 && Unaffected(c, by)) return;   // an "unaffected by Traps" monster stays put (costs still work)
        if (c.Loc == Loc.Grave && by != null && (paying == 0 || dest == Loc.Banished) && Necrovalley() && !TurnFlags.Contains("necroexempt")
            && !P(c.Owner).MonstersOnField.Any(m => m.FaceUp && m.Name == "Gravekeeper's Chief" && !Negated(m))) return;   // Necrovalley: GY cards stay put (Gravekeeper's Chief: not its controller's GY)
        var from = c.Loc;
        bool wasOnField = c.OnField;
        int controllerBefore = c.Controller, levelBefore = c.Loc == Loc.Monster ? Level(c) : 0;
        bool faceUpBefore = c.FaceUp;
        var varsBefore = wasOnField ? new Dictionary<string, int>(c.Vars) : null;
        var equips = wasOnField && c.Loc == Loc.Monster ? EquipsOf(c).ToList() : new();
        if (dest == Loc.Grave && ((wasOnField && Allowed(Q.Banish, c)) || Allowed(Q.BanishAny, c))) dest = Loc.Banished;
        if (wasOnField && c.FaceUp && c.Loc == Loc.Monster && S(c)?.LeaveToBanish == true && !Negated(c)) dest = Loc.Banished;   // Dark Magician of Chaos
        if (wasOnField && c.Loc == Loc.Monster && c.Var("leavebanish") == 1) dest = Loc.Banished;   // Living Fossil: "banish it when it leaves the field"
        if (dest == Loc.Grave && S(c)?.GraveToBanish is { } gb && gb(X(c), c.Loc, battle, paying == 0 && by != null)) dest = Loc.Banished;
        if (c.Data.IsFusion && dest is Loc.Deck or Loc.Hand) dest = Loc.Extra;
        Detach(c);
        if (wasOnField) { c.ResetFieldState(); c.Controller = c.Owner; }
        if (from == Loc.Grave) { c.Vars.Remove("fusionmat"); c.Vars.Remove("freetribute"); }   // left the GY: no longer "used as Fusion Material", a fresh copy
        if (dest is Loc.Deck or Loc.Hand or Loc.Extra) Flags.Remove($"proper{c.Uid}");   // a new proper Summon is needed
        if (dest == Loc.Grave) c.Vars["gyturn"] = Turn;
        if (from == Loc.Grave) Flags.Remove($"neg{c.Uid}");
        if (wasOnField) LeftFieldLog.Add((c, controllerBefore, levelBefore, dest, Turn, faceUpBefore, varsBefore!));
        if (dest is Loc.Deck or Loc.Hand or Loc.Extra) c.FaceUp = false;
        if (dest is Loc.Grave or Loc.Banished) c.FaceUp = true;
        if (c.IsToken && dest != Loc.Monster) { c.Loc = Loc.None; }
        else
        {
            var o = P(c.Owner);
            c.Loc = dest;
            switch (dest)
            {
                case Loc.Deck: if (top) o.Deck.Insert(0, c); else o.Deck.Add(c); break;
                case Loc.Hand: o.Hand.Add(c); break;
                case Loc.Grave: o.Grave.Add(c); break;
                case Loc.Banished: o.Banished.Add(c); break;
                case Loc.Extra: o.Extra.Add(c); break;
                default: throw new ArgumentException("Use Place() for field zones");
            }
        }
        if (wasOnField)
        {
            await Raise(new Ev { Kind = EvKind.LeftField, Card = c, From = from, Other = by, Battle = battle, Player = c.Owner });
            foreach (var e in equips) if (e.OnField) await MoveTo(e, Loc.Grave);
        }
        if (c.Loc == Loc.Grave && by != null && by.Controller != c.Owner) Flags[$"sentbyopp{c.Uid}"] = Turn;
        if (c.Loc == Loc.Grave) await Raise(new Ev { Kind = EvKind.ToGrave, Card = c, From = from, Other = by, Battle = battle, Player = c.Owner });
        if (c.Loc == Loc.Hand && from != Loc.Deck) await Raise(new Ev { Kind = EvKind.ToHand, Card = c, From = from, Other = by, Player = c.Owner });
        if (c.Loc == Loc.Banished) { c.Vars["banishturn"] = Turn; await Raise(new Ev { Kind = EvKind.Banished, Card = c, From = from, Other = by, Player = c.Owner }); }
        CheckWin();
    }

    /// Put a card into a field zone of `player`. zone -1 = first free.
    public bool Place(Card c, int player, Loc loc, int zone = -1)
    {
        var p = P(player);
        if (loc == Loc.Field)
        {
            if (p.FieldZone != null) return false;
            Detach(c);
            p.FieldZone = c; c.Loc = Loc.Field; c.Zone = 0; c.Controller = player;
            return true;
        }
        var arr = loc == Loc.Monster ? p.Monsters : p.Spells;
        if (zone < 0) zone = Array.IndexOf(arr, null);
        if (zone < 0 || arr[zone] != null) return false;
        // Blocked zones: Monster Zones in bits 0-4 (Ground Collapse), Spell & Trap Zones in bits 8-12 (Seismic Shockwave).
        int sh = loc == Loc.Monster ? 0 : 8;
        if (Flags.TryGetValue($"blocked{player}", out var mask) && (mask & (1 << (zone + sh))) != 0)
        {
            zone = Enumerable.Range(0, 5).FirstOrDefault(z => arr[z] == null && (mask & (1 << (z + sh))) == 0, -1);
            if (zone < 0) return false;
        }
        Detach(c);
        arr[zone] = c; c.Loc = loc; c.Zone = zone; c.Controller = player;
        return true;
    }

    public int FreeMonsterZones(int player)
    {
        var p = P(player);
        Flags.TryGetValue($"blocked{player}", out var mask);
        return Enumerable.Range(0, 5).Count(z => p.Monsters[z] == null && (mask & (1 << z)) == 0);
    }

    public IEnumerable<Card> EquipsOf(Card m) => FieldCards.Where(e => e.EquippedTo == m);

    // ---- destruction

    public Flow Destroy(Card c, Card? by = null) => Destroy(new[] { c }, by);
    public Card? Destroyer;
    public Card? Targeter;    // the card choosing targets right now ("cannot be targeted by monster effects")
    internal int paying;      // inside a Cost delegate: discards there are costs, not "by a card effect"

    /// Unaffected by this card's effect (Trap effects: Q.AffectedByTrap; Spell effects: Q.AffectedBySpell).
    public bool Necrovalley() => Sources().Any(s => s.c.Name == "Necrovalley" && s.c.Loc == Loc.Field);

    /// The card whose effect is being checked by Unaffected (Q.AffectedByMonster / AffectedBySpell scripts read it).
    public Card? Affecting;
    public bool Unaffected(Card c, Card? by) => by != null && by != c && c.Loc == Loc.Monster && (Affecting = by) != null
        && ((by.Data.IsTrap && Forbidden(Q.AffectedByTrap, c)) || (by.Data.IsMonster && by.AsMonster == null && Forbidden(Q.AffectedByMonster, c)) || (by.Data.IsSpell && !by.IsSpellLike && c.Data.Modern && Forbidden(Q.AffectedBySpell, c)));   // PoC cards keep their original handling   // the card whose effect is destroying (for Q.DestroyByEffect: "cannot be destroyed by Spell effects")

    async Flow<bool> GraveSave(Card c, bool battle)
    {
        foreach (var g in P(c.Controller).Grave.ToList())
            if (S(g)?.GraveSave is { } f && await f(X(g), c, battle)) return true;
        // ...or a face-up Field Spell that saves it (Toon Kingdom: banish the top card of the Deck instead).
        if (P(c.Controller).FieldZone is { FaceUp: true } fz && !Negated(fz) && S(fz)?.GraveSave is { } ff && await ff(X(fz), c, battle)) return true;
        return false;
    }

    public async Flow Destroy(IEnumerable<Card> list, Card? by = null, bool battle = false)
    {
        var l = list.Where(c => c.OnField).Distinct().ToList();
        foreach (var c in l)
        {
            if (!c.OnField) continue;
            Destroyer = by;
            bool kept = !battle && (Forbidden(Q.DestroyByEffect, c) || Unaffected(c, by));
            Destroyer = null;
            if (kept) continue;
            // Guard Counter: "if that card would ever be destroyed, remove 1 Guard Counter from it instead".
            if (c.Counters("guard") > 0) { c.Vars["ctr:guard"] = c.Counters("guard") - 1; Seq++; continue; }
            if (S(c)?.DestroyInstead is { } inst && c.FaceUp && !Negated(c) && await inst(X(c), battle)) continue;
            // A GY card banished instead (Machine Angel Ritual): "if a LIGHT monster you control would be destroyed".
            if (await GraveSave(c, battle)) continue;
            // An equip destroyed instead (Armored Cybern): "if the equipped monster would be destroyed by a card effect".
            if (!battle && c.Loc == Loc.Monster && EquipsOf(c).FirstOrDefault(e => e.FaceUp && !Negated(e) && S(e)?.EffectShield?.Invoke(X(e), c) == true) is { } sub)
            { await Destroy(sub, by); continue; }
            if (!battle && c.Data.IsTrap && c.Loc == Loc.Spell && TurnFlags.Contains($"faketrap{c.Controller}")
                && !(Classic && c.FaceUp && c.Data.Is("Normal")) && c.Name != "Fake Trap") continue;
            var loc = c.Loc;
            Flags[$"destroyed{c.Uid}"] = Turn;
            // Crystal Beasts: destroyed face-up in a Monster Zone, it may stay as a Continuous Spell instead of going to the GY.
            if (loc == Loc.Monster && c.FaceUp && S(c)?.ToSpellZone?.Invoke(X(c)) == true && P(c.Controller).FreeSpellZones > 0
                && await YesNo(c.Controller, $"Place {c.Name} in your Spell & Trap Zone as a Continuous Spell?", c))
            {
                int ctl = c.Controller;
                c.ResetFieldState();
                Place(c, ctl, Loc.Spell);
                c.FaceUp = true; c.Vars["asspell"] = 1;
                await Raise(new Ev { Kind = EvKind.Placed, Card = c, Player = ctl });
                await Raise(new Ev { Kind = EvKind.LeftField, Card = c, From = loc, Other = by, Battle = battle, Player = c.Owner });
                await Raise(new Ev { Kind = EvKind.Destroyed, Card = c, Other = by, Battle = battle, From = loc, Player = c.Owner });
                continue;
            }
            await MoveTo(c, Loc.Grave, by: by, battle: battle);
            await Raise(new Ev { Kind = EvKind.Destroyed, Card = c, Other = by, Battle = battle, From = loc, Player = c.Owner });
        }
    }

    /// Put a card into `player`'s hand without changing its owner (Graverobber).
    public void TakeToHand(Card c, int player)
    {
        Detach(c);
        c.Loc = Loc.Hand; c.Controller = player; c.FaceUp = false;
        P(player).Hand.Add(c);
    }

    public async Flow ToGrave(Card c, Card? by = null) => await MoveTo(c, Loc.Grave, by: by);
    public async Flow Banish(Card c, Card? by = null) { if (c.Loc != Loc.Banished) await MoveTo(c, Loc.Banished, by: by); }
    public async Flow ToHand(Card c, Card? by = null) => await MoveTo(c, Loc.Hand, by: by);
    public async Flow ToDeck(Card c, bool top = true, bool shuffle = false)
    {
        await MoveTo(c, Loc.Deck, top);
        if (shuffle) Shuffle(c.Owner);
    }

    public void Shuffle(int player) => Rng.Shuffle(P(player).Deck);

    /// Test hook (Engine.Check log replay): swap a copy of `name` from the Deck into the hand,
    /// putting a hand card that is not `name` back in its place. Not reachable from commands.
    /// `cost` ranks hand cards to give up (lowest first; negative = keep).
    internal bool ConjureToHand(int player, string name, Func<Card, int> cost)
    {
        var p = P(player);
        if (p.Hand.Any(c => c.Name == name)) return true;
        int di = p.Deck.FindIndex(c => c.Name == name);
        var victim = p.Hand.Where(c => cost(c) >= 0).OrderBy(cost).FirstOrDefault();
        if (di < 0 || victim == null) return false;
        int hi = p.Hand.IndexOf(victim);
        var (d, h) = (p.Deck[di], p.Hand[hi]);
        p.Deck[di] = h; h.Loc = Loc.Deck;
        p.Hand[hi] = d; d.Loc = Loc.Hand; d.Controller = player;
        return true;
    }

    public async Flow Discard(int player, IEnumerable<Card> list, Card? by = null, bool random = false)
    {
        var l = list.ToList();
        foreach (var c in l) { Flags[$"discarded{c.Uid}"] = Turn; await MoveTo(c, Loc.Grave, by: by); }   // Super Rejuvenation counts these
        if (l.Count > 0) await Raise(new Ev { Kind = EvKind.Discarded, Player = player, Cards = l, Amount = l.Count, Other = by, Arg = random ? 1 : 0, Cost = paying > 0 });   // Arg 1: picked at random
    }

    public async Flow DiscardRandom(int player, int n, Card? by = null)
    {
        var h = P(player).Hand;
        var l = new List<Card>();
        for (int i = 0; i < n && h.Count > l.Count; i++)
        {
            var rest = h.Except(l).ToList();
            l.Add(rest[Rng.Int(rest.Count)]);
        }
        await Discard(player, l, by, random: true);
    }

    // ---- draw / LP

    public async Flow Draw(int player, int n, bool drawPhase = false)
    {
        var p = P(player);
        var drawn = new List<Card>();
        for (int i = 0; i < n; i++)
        {
            if (p.Deck.Count == 0) throw new DuelOver(1 - player, "deck out");
            var c = p.Deck[0];
            Detach(c);
            c.Loc = Loc.Hand;
            c.Controller = player;
            p.Hand.Add(c);
            drawn.Add(c);
        }
        CheckWin();
        if (n > 0) await Raise(new Ev { Kind = EvKind.Drew, Player = player, Amount = n, Arg = drawPhase ? 1 : 0, Cards = drawn });
    }

    public Link? PainLink; public int PainPlayer;
    public async Flow Damage(int player, int amount, Card? src = null, bool battle = false)
    {
        if (amount <= 0) return;
        if (Forbidden(Q.TakeDamage, src, player)) return;
        if (!battle && Forbidden(Q.TakeEffectDamage, null, player)) return;
        if (!battle && PainLink != null && Resolving == PainLink && player == PainPlayer) player = 1 - player;   // Spell of Pain
        if (TurnFlags.Contains($"rainbowlife{player}")) { await Gain(player, amount, src); return; }   // Rainbow Life
        P(player).Lp = Math.Max(0, P(player).Lp - amount); // LP stops at 0 (the original kept it unsigned)
        await Raise(new Ev { Kind = EvKind.Damage, Player = player, Amount = amount, Battle = battle, Other = src });
    }

    public async Flow Gain(int player, int amount, Card? src = null)
    {
        if (amount <= 0) return;
        if (Allowed(Q.GainIsDamage, null, player)) { await Damage(player, amount, src); return; }
        P(player).Lp += amount;
        await Raise(new Ev { Kind = EvKind.Gain, Player = player, Amount = amount, Other = src });
    }

    /// Paying LP as a cost (not damage: never prevented).
    public void Pay(int player, int amount, Card? src = null)
    {
        if (paying > 0 && src != null && src.Data.IsSpell && CountActive("Spell Economics", player) > 0) return;   // Spell Economics: no LP costs for Spells
        Seq++;
        Flags[$"paid{player}:{Turn}"] = Flags.GetValueOrDefault($"paid{player}:{Turn}") + amount;   // Life Absorbing Machine
        P(player).Lp = Math.Max(0, P(player).Lp - amount);
        OnEvent?.Invoke(new Ev { Kind = EvKind.Damage, Player = player, Amount = amount, Arg = 1 });
        foreach (var (sc, s) in Sources().ToList()) s.Paid?.Invoke(X(sc), player, amount, src);   // Battle-Scarred
        CheckWin();
    }

    // ---- summons / positions / control

    /// `kind`/`by`: how and by which card (Summoning conditions of official cards); `ignore`: "ignoring its Summoning conditions".
    public bool CanSpecialSummon(Card c, int player, SummonKind kind = SummonKind.Special, Card? by = null, bool ignore = false, bool ignoreZones = false) =>
        (ignoreZones || FreeMonsterZones(player) > 0) && !Forbidden(Q.SpecialSummon, c, player) && SummonConditionOk(c, kind, by, ignore)
        && !(kind == SummonKind.Fusion && CountActive("Non-Fusion Area") > 0);

    /// Official Summoning conditions. "Must be Fusion Summoned" / "Must be Special Summoned with X" monsters: only that way;
    /// once properly Summoned, an effect that ignores Summoning conditions may bring them back (not Monster Reborn).
    bool SummonConditionOk(Card c, SummonKind kind, Card? by, bool ignore)
    {
        var s = S(c);
        if (s == null || (!s.MustFusion && s.SummonOk == null)) return true;
        // Ignoring Summoning conditions: from the hand, Deck or Extra Deck; from the GY / banished only once properly Summoned,
        // unless it could be Normal Summoned ("Cannot be Special Summoned" monsters like Ancient Gear Golem).
        if (ignore) return c.Loc is Loc.Hand or Loc.Deck or Loc.Extra || Flags.ContainsKey($"proper{c.Uid}")
            || (!s.MustFusion && !s.NoNormalSummon && !c.Data.IsFusion && !c.Data.IsRitual);
        if (s.MustFusion) return kind == SummonKind.Fusion && c.Loc == Loc.Extra;
        return s.SummonOk!(X(c), by, kind);
    }

    public async Flow<bool> SpecialSummon(Card c, int player, bool defense = false, bool faceUp = true, SummonKind kind = SummonKind.Special, Card? by = null, bool ignore = false, CardData? asMonster = null)
    {
        if (!CanSpecialSummon(c, player, kind, by, ignore)) return false;
        if (!ignore && S(c) is { } sc && (sc.MustFusion || sc.SummonOk != null)) Flags[$"proper{c.Uid}"] = 1;
        TurnFlags.Add($"ss{player}");   // "you cannot Special Summon during the turn you activate this"
        var from = c.Loc;
        if (from is Loc.Monster or Loc.Spell) c.ResetFieldState();
        if (!Place(c, player, Loc.Monster)) return false;
        c.FaceUp = faceUp;
        c.Defense = defense || !faceUp;
        c.Summoned = kind;
        c.SummonTurn = Turn;
        c.SetTurn = faceUp ? -1 : Turn;
        c.Vars["from"] = (int)from;
        if (asMonster != null) c.AsMonster = asMonster;   // a Trap Monster
        c.SummonedBy = by?.Name ?? "";
        await Raise(new Ev { Kind = faceUp ? EvKind.Summoned : EvKind.Set, Card = c, Arg = (int)kind, Player = player, From = from, Other = by });
        return true;
    }

    public Card MakeToken(string name, int player, int atk, int def, string attr = "EARTH", string race = "Beast", int level = 1)
    {
        var d = Db.ByName.TryGetValue(name, out var l) ? l[0] : new CardData { Name = name, Kind = Kind.Monster, Frame = "Normal", Atk = atk, Def = def, Attribute = attr, Race = race, Level = level, Idx = -1 };
        var c = NewCard(d, player);
        c.IsToken = true;
        return c;
    }

    public async Flow ChangePosition(Card c, bool defense, bool? faceUp = null, bool flipEffect = true)
    {
        if (c.Loc != Loc.Monster) return;
        bool wasDown = !c.FaceUp;
        c.Defense = defense;
        if (faceUp.HasValue) c.FaceUp = faceUp.Value;
        await Raise(new Ev { Kind = EvKind.PositionChanged, Card = c, Player = c.Controller, Arg = defense ? 1 : 0 });
        if (wasDown && c.FaceUp && flipEffect) await Flipped(c);
    }

    /// Turn a face-down monster face-up (by attack or effect). Triggers its flip effect.
    public async Flow Flip(Card c, bool triggerFlip = true)
    {
        if (c.Loc != Loc.Monster || c.FaceUp) return;
        c.FaceUp = true;
        if (triggerFlip) await Flipped(c);
        else await Raise(new Ev { Kind = EvKind.PositionChanged, Card = c, Player = c.Controller });
    }

    internal async Flow Flipped(Card c)
    {
        c.Vars["flipped"] = 1;
        await Raise(new Ev { Kind = EvKind.Flipped, Card = c, Player = c.Controller });
    }

    public async Flow SetFaceDown(Card c)
    {
        if (c.Loc == Loc.Monster) { c.FaceUp = false; c.Defense = true; }
        else if (c.OnField) c.FaceUp = false;
        await Raise(new Ev { Kind = EvKind.PositionChanged, Card = c, Player = c.Controller });
    }

    public async Flow<bool> TakeControl(Card c, int player)
    {
        if (c.Loc != Loc.Monster || c.Controller == player || Forbidden(Q.ChangeControl, c)) return false;
        if (FreeMonsterZones(player) == 0) return false;
        var old = c.Controller;
        var zone = Array.IndexOf(P(player).Monsters, null);
        P(old).Monsters[c.Zone] = null;
        P(player).Monsters[zone] = c;
        c.Zone = zone;
        c.Controller = player;
        await Raise(new Ev { Kind = EvKind.ControlChanged, Card = c, Player = player, Arg = old });
        return true;
    }

    public async Flow Equip(Card equip, Card target)
    {
        if (!target.OnField || target.Loc != Loc.Monster) { await MoveTo(equip, Loc.Grave); return; }
        if (!equip.OnField)
        {
            if (!Place(equip, equip.Controller, Loc.Spell)) { await MoveTo(equip, Loc.Grave); return; }
        }
        equip.FaceUp = true;
        equip.EquippedTo = target;
        await Raise(new Ev { Kind = EvKind.Equipped, Card = equip, Other = target, Player = equip.Controller });
    }

    public void UntilEndOfTurn(Action undo) => endOfTurn.Add(undo);
    public void Boost(Card c, int atk, int def = 0) { c.TempAtk += atk; c.TempDef += def; Seq++; }

    /// Show cards to a player (presentation only; the engine already knows everything).
    public void Reveal(int player, params Card[] cs) => Reveal(player, (IEnumerable<Card>)cs);
    public void Reveal(int player, IEnumerable<Card> cs) => OnEvent?.Invoke(new Ev { Kind = EvKind.Revealed, Player = player, Cards = cs.ToList() });

    public bool Coin() => Rng.Coin();

    /// Toss n coins for an effect (no call); returns the number of heads. Raises a Coin event for the field.
    public int TossCoins(int player, int n, Card? src)
    {
        int mask = 0;
        for (int i = 0; i < n; i++) if (Coin()) mask |= 1 << i;
        Seq++;
        OnEvent?.Invoke(new Ev { Kind = EvKind.Coin, Player = player, Other = src, Amount = n, Arg = mask });
        return System.Numerics.BitOperations.PopCount((uint)mask);
    }

    /// "Toss a coin and call it": the player calls Heads or Tails, then the coin is tossed. True = called right.
    public async Flow<bool> CallCoin(int player, Card? src)
    {
        int call = await ChooseText(player, $"{src?.Name ?? "Coin toss"}: call it", new[] { "Heads", "Tails" }, src);
        bool heads = Coin();
        Seq++;
        OnEvent?.Invoke(new Ev { Kind = EvKind.Coin, Player = player, Other = src, Amount = 1, Arg = heads ? 1 : 0, Call = call == 0 ? 1 : 0 });
        return heads == (call == 0);
    }

    /// Roll a six-sided die for an effect; returns 1-6. Raises a Die event for the field.
    public int Die(int player, Card? src)
    {
        int r = Rng.Die();
        Seq++;
        OnEvent?.Invoke(new Ev { Kind = EvKind.Die, Player = player, Other = src, Amount = r });
        return r;
    }
}
