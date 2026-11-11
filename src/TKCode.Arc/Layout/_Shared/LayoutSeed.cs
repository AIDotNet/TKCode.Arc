namespace TKCode.Arc;

/// <summary>
/// Repeatable "random" numbers for layout panels: a value depends only on a seed and a child's index, so the same children,
/// seed and grid always give the same layout (no <see cref="System.Random"/> during layout, nothing changes on resize).
/// </summary>
internal static class LayoutSeed
{
    /// <summary>A value in [0, 1) for <paramref name="index"/> under <paramref name="seed"/>; <paramref name="stream"/> picks an independent sequence.</summary>
    public static double Unit(int seed, int index, int stream = 0)
    {
        // SplitMix64 finalizer over (seed, stream, index): well spread even for consecutive indexes and seeds.
        var z = ((ulong)(uint)seed << 32) ^ ((ulong)(uint)stream << 24) ^ (uint)index;
        z += 0x9E3779B97F4A7C15UL;
        z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
        z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
        z ^= z >> 31;
        return (z >> 11) * (1.0 / (1UL << 53));
    }
}
