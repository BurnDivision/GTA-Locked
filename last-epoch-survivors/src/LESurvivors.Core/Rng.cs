namespace LESurvivors.Core;

/// <summary>Small deterministic RNG (xorshift64*). Seedable so runs and tests are reproducible.</summary>
public sealed class Rng
{
    private ulong _s;
    public Rng(ulong seed) { _s = seed == 0 ? 0x9E3779B97F4A7C15UL : seed; }
    public ulong NextU64() { _s ^= _s >> 12; _s ^= _s << 25; _s ^= _s >> 27; return _s * 2685821657736338717UL; }
    /// <summary>Uniform in [0,1).</summary>
    public double NextDouble() => (NextU64() >> 11) * (1.0 / (1UL << 53));
    public float NextFloat() => (float)NextDouble();
    /// <summary>Uniform int in [min, maxExclusive).</summary>
    public int Range(int min, int maxExclusive) => maxExclusive <= min ? min : min + (int)(NextDouble() * (maxExclusive - min));
    public float Range(float min, float max) => min + (float)NextDouble() * (max - min);
    public bool Chance(double p) => NextDouble() < p;

    public T Pick<T>(IReadOnlyList<T> list) => list[Range(0, list.Count)];

    /// <summary>Weighted pick; returns -1 when all weights are zero.</summary>
    public int PickWeighted(IReadOnlyList<double> weights)
    {
        double total = 0; foreach (var w in weights) total += Math.Max(0, w);
        if (total <= 0) return -1;
        double r = NextDouble() * total;
        for (int i = 0; i < weights.Count; i++) { r -= Math.Max(0, weights[i]); if (r < 0) return i; }
        return weights.Count - 1;
    }
}
