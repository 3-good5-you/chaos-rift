using System.Text.Json;

namespace PoC.Engine;

/// Learned board value: how likely a side is to win from here, a logistic model over a few board features.
/// Trained on CPU-vs-CPU duels (Engine.Check `train`, weights in ai_value.json) and trained further on every duel you
/// finish (Learn; your copy of the weights lives in the save). Ai.Eval reads it, so this is what the CPU believes a
/// good board is: what an exposed low-ATK monster in Attack Position costs, what a trap still held is worth, what
/// lethal on board means, all priced by which boards went on to win.
public sealed class Value
{
    public static readonly string[] Names =
        { "turn", "lp", "hand", "exodia", "monsters", "atk", "def", "sets", "cont", "threat", "kills", "lethal", "lowdeck" };
    public readonly double[] W;
    /// Card name -> {sum of gains, times used}: what the CPU got each time it used that Spell/Trap (Judge records it).
    /// A card still in hand or Set is worth HoldShare of its usual payoff, so the CPU spends a card only when the
    /// moment beats that: Mirror Force waits for more than one small attacker, Raigeki for more than one monster.
    public readonly Dictionary<string, double[]> Uses = new();
    public const double HoldShare = 0.5;
    const int MinUses = 5;
    public Value(double[] w, Dictionary<string, double[]>? uses = null)
    {
        W = (double[])w.Clone();
        if (uses != null) lock (uses) foreach (var (k, v) in uses) Uses[k] = (double[])v.Clone();
    }

    static Value? trained;
    static string? shippedId;
    public static string ShippedId => shippedId ??= Trained.Id();
    /// The shipped weights (ai_value.json), or hand-set ones close to the old fixed score if the file is missing.
    public static Value Trained { get => trained ??= Load(); set { trained = value; shippedId = null; } }   // set: training runs only
    static Value Load()
    {
        using var s = typeof(Value).Assembly.GetManifestResourceStream("ai_value.json");
        if (s == null) return new Value(new[] { 0, 1, 0.4, 1.5, 0.5, 0.5, 0.5, 0.3, 0.4, 0, 0, 0, 0 });
        return FromJson(new StreamReader(s).ReadToEnd());
    }

    sealed class Dto { public Dictionary<string, double> Weights = new(); public Dictionary<string, double[]> Uses = new(); }
    static readonly JsonSerializerOptions Json = new() { IncludeFields = true, WriteIndented = true };
    public string ToJson()
    {
        lock (Uses)   // the CPU may be recording a use on its thread while the game saves
            return JsonSerializer.Serialize(new Dto
            {
                Weights = Names.Select((n, i) => (n, i)).ToDictionary(x => x.n, x => Math.Round(W[x.i], 4)),
                Uses = Uses.OrderBy(kv => kv.Key).ToDictionary(kv => kv.Key, kv => new[] { Math.Round(kv.Value[0]), kv.Value[1] }),
            }, Json);
    }
    public static Value FromJson(string json)
    {
        var d = JsonSerializer.Deserialize<Dto>(json, Json)!;
        return new Value(Names.Select(n => d.Weights.GetValueOrDefault(n)).ToArray(), d.Uses);
    }

    /// Fingerprint of the weights + payoffs (stable across runs), so a save knows which shipped model it grew from.
    public string Id() { ulong h = 14695981039346656037UL; foreach (char ch in ToJson()) h = (h ^ ch) * 1099511628211UL; return h.ToString("x"); }

    /// Train on one finished duel against you (its boards, both seats) with a small step. The payoffs of the cards the
    /// CPU used in it were already recorded as it played (Used).
    public Value LearnFrom(Duel d) => Fit(Positions(d), stay: 400, steps: 4);

    /// Record one use of a Spell/Trap and what it gained (LP-equivalent, before its own hold value).
    public void Used(string name, double gain)
    {
        lock (Uses)
        {
            var u = Uses.TryGetValue(name, out var x) ? x : Uses[name] = new double[2];
            u[0] += Math.Clamp(gain, -3000, 6000); u[1]++;
        }
    }

    /// What keeping this card (in hand or Set) is worth: a share of its usual payoff. Monsters and cards the CPU has
    /// seen used fewer than 5 times are worth nothing extra.
    public double Hold(Card c)
    {
        if (c.Data.IsMonster) return 0;
        lock (Uses) return Uses.TryGetValue(c.Name, out var u) && u[1] >= MinUses ? HoldShare * Math.Max(0, u[0] / u[1]) : 0;
    }

    /// Win-chance logit for `me`.
    public double Logit(Duel d, int me) => Features(d, me).Select((f, i) => f * W[i]).Sum();

    /// Logit in LP-equivalent points (the unit the CPU's margins use).
    public double Points(Duel d, int me) => Logit(d, me) / Math.Max(W[1], 0.05) * 1000 + Held(d, me) - Held(d, 1 - me);

    double Held(Duel d, int s) => d.P(s).Hand.Concat(d.P(s).Spells.Where(c => c != null && !c.FaceUp)!).Sum(c => Hold(c!));

    /// Board features from `me`'s side: each is mine minus the opponent's (so the model is the same for both seats),
    /// plus whose turn it is.
    public static double[] Features(Duel d, int me)
    {
        double[] a = Side(d, me), b = Side(d, 1 - me);
        var f = new double[Names.Length];
        f[0] = d.TurnPlayer == me ? 1 : -1;
        for (int i = 1; i < f.Length; i++) f[i] = a[i - 1] - b[i - 1];
        return f;
    }

    static double[] Side(Duel d, int s)
    {
        Player p = d.P(s), o = d.P(1 - s);
        var mons = p.MonstersOnField.ToList();
        var (dmg, kills) = Threat(d, p, o);
        return new double[]
        {
            Math.Min(p.Lp, 8000) / 1000.0,
            Math.Min(p.Hand.Count, 8),
            p.Hand.Count(c => Duel.Exodia.Contains(c.Name)),
            mons.Count,
            mons.Where(m => m.FaceUp && !m.Defense).Sum(d.Atk) / 1000.0,
            mons.Where(m => m.Defense).Sum(d.Def) / 1000.0,
            p.Spells.Count(c => c != null && !c.FaceUp),
            p.SpellsOnField.Count(c => c.FaceUp && c.EquippedTo == null && !c.Data.Is("Equip")),
            Math.Min(dmg, 8000) / 1000.0,
            kills,
            dmg >= o.Lp ? 1 : 0,
            p.Deck.Count < 3 ? 1 : 0,
        };
    }

    /// What `p`'s monsters could do in their next Battle Phase against `o`'s board, traps ignored: weakest first, each
    /// into the target it beats by most (an Attack Position target also takes the difference as damage), direct when
    /// the board is clear. This is where a low-ATK monster left in Attack Position shows up: as the opponent's damage.
    static (int dmg, int kills) Threat(Duel d, Player p, Player o)
    {
        var targets = o.MonstersOnField.Select(c => (atk: d.Atk(c), guard: c.Defense ? d.Def(c) : d.Atk(c), def: c.Defense)).ToList();
        int dmg = 0, kills = 0;
        foreach (int a in p.MonstersOnField.Select(d.Atk).OrderBy(x => x))
        {
            if (targets.Count == 0) { dmg += a; continue; }
            var beaten = targets.Where(t => a > t.guard).OrderByDescending(t => a - t.guard).ToList();
            if (beaten.Count == 0) continue;
            if (!beaten[0].def) dmg += a - beaten[0].atk;
            targets.Remove(beaten[0]);
            kills++;
        }
        return (dmg, kills);
    }

    /// Training samples from a finished duel: the board at every Main Phase prompt, from both seats, labelled with
    /// whether that seat went on to win. Draws teach nothing.
    public static List<(double[] f, double y)> Positions(Duel d)
    {
        var l = new List<(double[], double)>();
        if (!d.Over || d.Winner < 0) return l;
        try
        {
            var r = new Duel(d.Setup, null, d.Db);
            foreach (var c in d.Log)
            {
                if (r.Pending is { Kind: Ask.Main })
                    for (int s = 0; s < 2; s++) l.Add((Features(r, s), d.Winner == s ? 1 : 0));
                r.Submit(c);
            }
        }
        catch (Exception) { }   // a log that won't replay teaches what it got through
        return l;
    }

    /// New weights fitted to `data` (logistic regression, Newton steps), pulled toward this model's weights by
    /// `stay`: large = a small nudge (one of your duels), small = learn from scratch (training on thousands).
    public Value Fit(List<(double[] f, double y)> data, double stay, int steps = 12)
    {
        int n = W.Length;
        var w = (double[])W.Clone();
        for (int it = 0; it < steps && data.Count > 0; it++)
        {
            var g = new double[n];
            var h = new double[n, n];
            for (int i = 0; i < n; i++) { g[i] = stay * (w[i] - W[i]); h[i, i] = stay; }
            foreach (var (f, y) in data)
            {
                double z = 0;
                for (int i = 0; i < n; i++) z += w[i] * f[i];
                double pr = 1 / (1 + Math.Exp(-Math.Clamp(z, -30, 30))), q = pr * (1 - pr);
                for (int i = 0; i < n; i++)
                {
                    g[i] += (pr - y) * f[i];
                    for (int j = 0; j < n; j++) h[i, j] += q * f[i] * f[j];
                }
            }
            var step = Solve(h, g);
            for (int i = 0; i < n; i++) w[i] -= step[i];
        }
        return new Value(w, Uses);
    }

    /// Gaussian elimination with partial pivoting: x with h x = g.
    static double[] Solve(double[,] h, double[] g)
    {
        int n = g.Length;
        var a = (double[,])h.Clone();
        var x = (double[])g.Clone();
        for (int c = 0; c < n; c++)
        {
            int piv = Enumerable.Range(c, n - c).MaxBy(r => Math.Abs(a[r, c]));
            for (int k = 0; k < n; k++) (a[c, k], a[piv, k]) = (a[piv, k], a[c, k]);
            (x[c], x[piv]) = (x[piv], x[c]);
            if (Math.Abs(a[c, c]) < 1e-12) continue;
            for (int r = 0; r < n; r++)
            {
                if (r == c) continue;
                double m = a[r, c] / a[c, c];
                for (int k = c; k < n; k++) a[r, k] -= m * a[c, k];
                x[r] -= m * x[c];
            }
        }
        for (int i = 0; i < n; i++) x[i] = Math.Abs(a[i, i]) < 1e-12 ? 0 : x[i] / a[i, i];
        return x;
    }
}
