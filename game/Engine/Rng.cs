namespace PoC.Engine;

/// Seeded, platform-independent RNG (SplitMix64). All duel randomness goes through Duel.Rng,
/// so a duel is reproducible from its seed + command log.
public sealed class Rng
{
    ulong s;
    public Rng(ulong seed) => s = seed;
    public ulong State => s;
    public void Reseed(ulong seed) => s = seed;

    public ulong Next()
    {
        ulong z = s += 0x9E3779B97F4A7C15UL;
        z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
        z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
        return z ^ (z >> 31);
    }

    /// Uniform int in [0, n).
    public int Int(int n) => n <= 0 ? 0 : (int)(Next() % (ulong)n);
    public bool Coin() => (Next() & 1) == 0;
    public int Die() => Int(6) + 1;

    public void Shuffle<T>(IList<T> l)
    {
        for (int i = l.Count - 1; i > 0; i--)
        {
            int j = Int(i + 1);
            (l[i], l[j]) = (l[j], l[i]);
        }
    }
}
