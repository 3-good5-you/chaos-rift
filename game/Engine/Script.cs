namespace PoC.Engine;

public enum EvKind
{
    Summoned,        // Card, Summon kind in Arg (SummonKind)
    Set,             // monster Set face-down: Card
    Flipped,         // Card turned face-up (flip effects): Card
    Destroyed,       // Card, Other = destroyer (battle opponent or effect card), Battle flag
    ToGrave,         // Card arrived in a Graveyard; From = previous location
    LeftField,       // Card left the field (any destination); From
    ToHand,          // Card returned/added to hand; From
    AttackDeclared,  // Card = attacker, Other = target (null = direct)
    DamageCalc,      // Card = attacker, Other = defender (window before damage calculation)
    BattleDamage,    // Card = monster that dealt it, Player = damaged player, Amount
    Damage,          // any LP damage: Player, Amount, Battle flag, Other = source card
    Gain,            // LP gained: Player, Amount
    Activated,       // Link = the chain link just added
    Standby, EndPhase, DrawPhase, BattleStart, BattleEnd, MainStart, // Player = turn player
    Drew,            // Player, Amount, Arg = 1 when during the Draw Phase
    Discarded,       // Player, Cards, Other = effect source
    Equipped,        // Card = equip, Other = monster
    ControlChanged,  // Card, Player = new controller
    PositionChanged, // Card
    BattleDestroyedMonster, // Card = the monster that won, Other = the destroyed monster
    AfterDamage,     // end of a battle's damage step: Card = attacker, Other = defender
    Revealed,        // cards shown to a player (Cards, Player = who sees them); presentation only
    Coin,            // coin toss: Player = tosser, Other = source card, Amount = coins, Arg = heads bitmask, Call = 1 heads / 0 tails / -1 none; presentation only
    Die,             // die roll: Player = roller, Other = source card, Amount = result 1-6; presentation only
    Negated,         // a chain link's activation was negated: Card, Player = who activated it; presentation only
    Triggered,       // never raised by the engine: the field's own step for Duel.OnTriggered
    Banished,        // Card was banished; From = where it was
    AfterCalc,       // after damage calculation, before anything is destroyed: Card = attacker, Other = defender
    Placed,          // a monster card was placed face-up in a Spell & Trap Zone as a Continuous Spell: Card, Player
    DamageStart,     // start of the Damage Step, before a face-down defender is flipped: Card = attacker, Other = defender
    Resolved,        // a chain link finished resolving: Link, Card, Player (Chain Burst: "immediately after it resolves")
    Shuffled,        // Player's Deck was shuffled mid-duel; presentation only
}

public sealed class Ev
{
    public EvKind Kind;
    public Card? Card, Other;
    public int Player = -1, Amount, Arg, Call = -1;
    public bool Battle;
    public bool Cost;            // Discarded: discarded as a cost (not "by a card effect")
    public Loc From;
    public Link? Link;
    public List<Card>? Cards;
    public override string ToString() => $"{Kind} {Card} {Other} p{Player} {Amount}";
}

/// Questions the engine asks the field: "is X forbidden?" / "is X allowed?"
public enum Q
{
    Attack, DirectAttack, ChangePos, NormalSummon, SetMonster, SpecialSummon, FlipSummon,
    Activate,          // activating this card (or its effect)
    SetSpell,          // setting this spell/trap from hand
    Negate,            // this card's effects are negated
    Target,            // this card cannot be targeted by card effects
    Tribute,           // this card cannot be tributed
    BeAttacked,        // this card cannot be chosen as an attack target
    AffectedBySpell,   // unaffected by spells
    TakeDamage,        // Card = null, use Player via Ctx: damage to that player becomes 0
    MustBeAttacked,    // Allow: attacks must target this card
    AttackFromDefense, // Allow: may attack in Defense Position (still using ATK)
    Piercing,          // Allow: piercing battle damage
    DoubleAttack,      // Allow: can attack twice
    NoBattleDamageToController, // Allow: controller takes no battle damage from this card's battles
    NoHandLimit,       // Allow: no hand-size limit for that player
    SetFaceDown,       // Forbid: monsters cannot be Set face-down (they go face-up Defense)
    Banish,            // cards sent to the Graveyard are banished instead (Allow, global)
    TakeEffectDamage,  // Forbid (Card = null, player via QueryPlayer): that player takes no effect damage
    GainIsDamage,
    AffectedByTrap,
    BanishAny,         // Allow: this card going to the GY from anywhere is banished instead (Chaos Distill)    // Forbid: this card is unaffected by Trap effects (targeting, destruction, moving, stat changes)      // Allow (player via QueryPlayer): that player's LP gains become damage instead (Bad Reaction to Simochi)
    AffectedByMonster, // Forbid: unaffected by this monster effect (Duel.Affecting = the monster)
    ChangeControl,     // Forbid: control of this card cannot switch
    DestroyByBattle,   // Forbid: this card cannot be destroyed by battle
    DestroyByEffect,   // Forbid: this card cannot be destroyed by card effects (Duel.Destroyer = the effect's card)
}

/// One Fusion Material: a label for prompts, the test, and the listed name (named materials can be substituted).
public sealed record Mat(string Label, Func<Card, bool> Ok, string? Name = null, bool More = false);   // More: "1+" (one or more)

public sealed class Stat
{
    public int BaseAtk, BaseDef, Atk, Def, Level;
    public string Race, Attr;
    public Stat(Card c)
    {
        var d = c.AsMonster ?? c.Data;   // a Trap Monster uses its monster stats
        BaseAtk = d.Atk; BaseDef = d.Def; Level = d.Level;
        Race = d.Race; Attr = d.Attribute;
    }
    public void Add(int atk, int def) { Atk += atk; Def += def; }
}

public sealed class Link
{
    public Card Card = null!;
    public Script S = null!;
    public int Player;
    public Ctx X = null!;
    public int Number;
    public bool Negated;
    public int Speed;
    public bool FromHand;        // was activated from hand (spell) - for "activated" checks
    public bool IsCardActivation; // spell/trap card activation (vs monster effect)
    public override string ToString() => $"#{Number} {Card} p{Player}";
}

/// Context handed to every script delegate.
public sealed class Ctx
{
    public Duel D = null!;
    public Card Self = null!;
    public Link? Link;
    public Ev? Ev;                     // event being responded to / triggering this
    public List<Card> T = new();       // targets chosen at activation
    public int N;                      // a value chosen at activation (attribute, type, die...)
    public int Me => Self.Controller;
    public int Opp => 1 - Self.Controller;
    public Player My => D.P(Me);
    public Player Their => D.P(Opp);
    public Card? T0 => T.Count > 0 ? T[0] : null;
    public bool Classic => D.Classic;
}

/// Behaviour of one card (by Konami id). Every delegate is optional.
public sealed class Script
{
    public int KonamiId;
    public string Name = "";

    // Activation: Spell/Trap card activation, or a monster's activated effect.
    public Func<Ctx, bool>? Can;       // extra activation condition (event timing, board needs)
    public Func<Ctx, Flow>? Cost;      // costs and target selection, at activation time
    public Func<Ctx, Flow>? Run;       // resolution
    public int Speed;                  // 0 = from card type
    public bool FromHand;              // monster effect activated from the hand (Thunder Dragon, Kuriboh)
    public bool FromGrave;             // monster effect activated from the Graveyard
    public bool OncePerTurn;
    public bool AnyTurn;               // monster quick effect usable on the opponent's turn

    // A face-up Spell/Trap's extra ignition action (Magical Labyrinth, Metalmorph target...).
    public Func<Ctx, bool>? IgnCan;
    public Func<Ctx, Flow>? IgnRun;

    // Equip Spells (and cards that become equips): legal target.
    public Func<Ctx, Card, bool>? EquipOk;

    // Continuous effects while this card is face-up on the field and not negated.
    public Action<Ctx, Card, Stat>? Base;   // changes base stats / race / attribute
    public Action<Ctx, Card, Stat>? Stats;  // ATK/DEF modifiers
    public Func<Ctx, Q, Card?, bool>? Forbid;
    public Func<Ctx, Q, Card?, bool>? Allow;
    public bool AlwaysActive;               // Base/Stats/Forbid apply also from hand/grave (rare)

    // Triggered / automatic behaviour. Called for every event while the card is in hand,
    // on the field or in the Graveyard; the script checks Self.Loc itself.
    public Func<Ctx, Ev, Flow>? On;

    // Summoning rules.
    public bool NoNormalSummon;              // cannot be Normal Summoned/Set
    public Func<Ctx, bool>? ProcCan;         // special summon procedure (from hand by default)
    public Func<Ctx, Flow>? Proc;
    public bool ProcFromDeck;
    public bool ProcFromGrave;               // its own Special Summon procedure also works from the GY (Plasma Warrior Eitom)
    public Func<Ctx, int>? TributesNeeded;   // override tribute count (Fiend Megacyber)

    // ---- official-text cards (anime pool)
    public List<Mat>? Materials;             // Fusion Materials, by name or by condition (null: the PoC fusion table)
    public bool MustFusion;                  // "Must be Fusion Summoned": revived only after a proper Fusion Summon, by an effect ignoring Summoning conditions
    public Func<Ctx, Card?, SummonKind, bool>? SummonOk; // Special Summon condition (the source card, the kind): "Must be Special Summoned with X"
    public bool Substitute;                  // may stand in for 1 Fusion Material listed by name
    public Func<Ctx, Card, Card?, int, int>? CalcAtk;    // during damage calculation only: (battling card, its opponent, ATK) -> ATK
    public Func<Ctx, Card, Card?, int, int>? CalcDef;    // ...the same for a defender's DEF (Gaia Plate the Earth Giant)
    public Func<Ctx, Card, bool>? BattleShield;          // equip: if the equipped monster would be destroyed by battle, destroy this instead
    public bool ShieldNoDamage;              // ...and its controller takes no battle damage from that battle
    public bool AttackEach;                  // can attack each of the opponent's monsters once per Battle Phase
    public Func<Ctx, Card, bool, bool>? FreeTribute;     // in the GY / on the field: this Normal Summon (or Set: bool) needs no Tributes
    public Func<Ctx, Flow>? OnFreeTribute;               // ...and what using it costs (Ancient Gear Castle: Tribute itself)
    public bool TreatedAlways;               // TreatedAs applies everywhere ("always treated as"), not only face-up on the field
    public Func<Ctx, Card, int>? LevelMod;   // continuous Level change for monsters in the hand and on the field (A Legendary Ocean)
    public Func<Ctx, Card, int, int, int>? BattleDamage;  // battle damage that `card` would inflict to player: (card, player, amount) -> amount
    public string[]? AlsoIn;                 // "always treated as an X card": archetypes besides its name's
    public bool NoResponse;                  // cards and effects cannot be activated in response to this activation
    public Func<Ctx, int>? ExtraAttacks;     // more attacks per Battle Phase (Armed Samurai - Ben Kei)
    public Func<Ctx, Card, bool>? EffectShield;          // equip: if the equipped monster would be destroyed by a card effect, destroy this instead
    public Func<Ctx, Card, Card, int>? TributeWeight;    // (tribute, monster being summoned) -> how many Tributes it counts as (The Lovers)
    public Action<Ctx, int, int, Card?>? Paid;           // a player paid LP (player, amount, the card it was paid for)
    public Func<Ctx, bool>? NormalSummonOk;  // its own Normal Summon condition, checked from the hand (Cave Dragon)
    public Func<Ctx, Card, int>? TributeWeightFor;       // on the monster being summoned: how many Tributes `t` counts as (Clear Vicious Knight)
    public Func<Ctx, Card, bool>? TributeAny;            // on the monster being summoned: more Tributes of this kind may be added (Cloudian - Nimbusman)
    public bool LeaveToBanish;               // "If this face-up card would leave the field, banish it instead"
    public Func<Ctx, Loc, bool, bool, bool>? GraveToBanish;   // (from, by battle, by a card effect) -> banished instead of going to the GY (Destiny HERO - Departed)
    public Func<Ctx, int, int>? HandLimit;   // (player) -> that player's hand size limit, or -1 for no change (Enervating Mist)
    public int TributeMax;                   // with TributeAny: at most this many Tributes in all (Fog King: 0 or 1)
    public bool NoSet;                       // cannot be Normal Set
    public bool SummonLock;                  // "This card's Normal Summon cannot be negated. When Normal Summoned, cards and effects cannot be activated" (Egyptian Gods)
    public Func<Ctx, bool>? HandTrap;        // this Trap may be activated from the hand when this holds (Harpie's Feather Storm)
    public Func<Ctx, Card, bool>? TributeOk;  // on the monster being Normal Summoned: which monsters may be Tributed for it
    public bool NotMaterial;                 // "Cannot be used as Fusion Material"
    public Func<Ctx, Card, bool, Flow<bool>>? GraveSave;     // in the GY: (monster about to be destroyed, battle?) -> true = this card was banished instead (Machine Angel Ritual)
    public Func<Duel, Card, IEnumerable<List<Mat>>>? MatRecipes;   // Fusion recipes built per duel (Pair Cycroid: 2 Machines with the same name)
    public Func<Ctx, bool, Flow<bool>>? DestroyInstead;  // would be destroyed (battle?): true = something else happened instead (Phantasm Counter)
    public Func<Ctx, Card, bool>? ExtraSummon;           // on the field: one more Normal Summon this turn for such a monster (Clear Cube)
    public bool ReflectBattleDamage;         // battle damage its controller would take from its battles goes to the opponent
    public Func<Ctx, bool>? ToSpellZone;     // destroyed face-up in a Monster Zone: may be placed face-up as a Continuous Spell instead
    public string? TreatedAs;                // its name while face-up on the field
    public bool Sure;                        // auto-approved: so simple it needs no card lab test (plain Fusion, stat Equip, draw/search/burn)
    public string? Note;                     // how the engine plays it, for the card lab page
    public Script? GraveFx;                  // a Spell/Trap's effect activated from the GY (its own Can/Cost/Run/Speed/OncePerTurn)
    // More activated effects of the same card, each its own Can/Cost/Run/Speed/OncePerTurn/AnyTurn and place: a monster's on
    // the field face-up (FromHand / FromGrave / InSpellZone move it), a Spell/Trap's face-up on the field (FromGrave: the GY).
    public readonly List<Script> Fx = new();
    public bool InSpellZone;                 // an Fx of a monster card placed in the Spell & Trap Zone (Crystal Beasts)
    public string? Label;                    // what the field's action chip calls this Fx ("USE EFFECT" when null)

    /// Add one more activated effect to this card.
    public Script AddFx(Action<Script> setup) { var f = new Script { KonamiId = KonamiId, Name = Name }; setup(f); Fx.Add(f); return f; }

    public bool Implemented => Materials != null || GraveFx != null || Fx.Count > 0 || MustFusion || SummonOk != null || CalcAtk != null || BattleShield != null || AttackEach || FreeTribute != null ||Can != null || Cost != null || Run != null || IgnRun != null || EquipOk != null || Base != null
        || Stats != null || Forbid != null || Allow != null || On != null || Proc != null || NoNormalSummon || TributesNeeded != null || Marker;
    public bool Marker;                      // behaviour lives in engine code (keyed by name)
}
