namespace TKCode.Arc;

/// <summary>The routing of a whole sequence for one top-k and capacity: one <see cref="MixtureOfExpertsRoute"/> per token.</summary>
internal sealed class MixtureOfExpertsPlan
{
    public MixtureOfExpertsPlan(MixtureOfExpertsRoute[] routes, int experts, int k, int capacity)
    {
        Routes = routes;
        ExpertCount = experts;
        K = k;
        Capacity = capacity;
        Empty = new int[experts];
    }

    /// <summary>One route per token, in order.</summary>
    public MixtureOfExpertsRoute[] Routes { get; }

    public int ExpertCount { get; }

    public int K { get; }

    /// <summary>Tokens an expert takes before it is full; <see cref="int.MaxValue"/> when nothing is ever dropped.</summary>
    public int Capacity { get; }

    public bool IsUnlimited => Capacity == int.MaxValue;

    /// <summary>All zero loads (before the first token).</summary>
    public int[] Empty { get; }

    /// <summary>The loads once the first <paramref name="count"/> tokens are routed.</summary>
    public IReadOnlyList<int> LoadsAfter(int count) => count <= 0 || Routes.Length == 0 ? Empty : Routes[Math.Min(count, Routes.Length) - 1].Loads;

    /// <summary>The number of expert assignments once the first <paramref name="count"/> tokens are routed.</summary>
    public int RoutesAfter(int count) => count <= 0 || Routes.Length == 0 ? 0 : Routes[Math.Min(count, Routes.Length) - 1].Routes;

    /// <summary>The load balance once the first <paramref name="count"/> tokens are routed (null before any route).</summary>
    public double? BalanceAfter(int count) => count <= 0 || Routes.Length == 0 ? null : Routes[Math.Min(count, Routes.Length) - 1].LoadBalance;

    /// <summary>The fill of an expert's capacity bar for a load: load / capacity, or load / the largest load when unlimited.</summary>
    public double CapacityFill(int load, IReadOnlyList<int> loads)
    {
        if (load <= 0)
        {
            return 0;
        }
        if (!IsUnlimited)
        {
            return Math.Clamp(load / (double)Capacity, 0, 1);
        }
        var max = 0;
        for (var i = 0; i < loads.Count; i++)
        {
            max = Math.Max(max, loads[i]);
        }
        return max <= 0 ? 0 : load / (double)max;
    }
}

/// <summary>
/// The arithmetic of a sparse mixture-of-experts layer, as pure functions: gate scores normalized to sum to 1, the top k
/// (ties to the lower expert index), weights renormalized within the choice, capacity = ⌈factor × tokens × k / experts⌉,
/// tokens loaded in order (a full expert skips the token; a token whose every choice is full passes through), load
/// balance = (E − max/mean) / (E − 1), and stable example scores for tokens that bring none.
/// </summary>
internal static class MixtureOfExpertsPlanner
{
    /// <summary>The fewest experts a layer can show.</summary>
    public const int MinExperts = 2;

    /// <summary>The most experts a layer can show.</summary>
    public const int MaxExperts = 12;

    /// <summary>A fraction as a whole percentage, rounded half up (each value on its own, so a pair may add to 101).</summary>
    public static int Percent(double fraction) => (int)Math.Round(fraction * 100, MidpointRounding.AwayFromZero);

    /// <summary>
    /// Scores normalized to sum to 1 over <paramref name="experts"/> entries: negative, NaN and missing entries count as
    /// 0, extra entries are ignored, and an all-zero list becomes uniform.
    /// </summary>
    public static double[] Normalize(IReadOnlyList<double> raw, int experts)
    {
        var p = new double[experts];
        var sum = 0.0;
        for (var i = 0; i < experts; i++)
        {
            var v = i < raw.Count ? raw[i] : 0;
            p[i] = double.IsFinite(v) && v > 0 ? v : 0;
            sum += p[i];
        }
        for (var i = 0; i < experts; i++)
        {
            p[i] = sum > 0 ? p[i] / sum : 1.0 / experts;
        }
        return p;
    }

    /// <summary>
    /// Stable example scores for a token without any, derived from its text so the same text always gets the same
    /// scores: the 32-bit FNV-1a hash of the text (UTF-16 code units) seeds a mulberry32 generator; its first draw picks
    /// the favourite expert and its second the runner-up among the rest; then every expert draws a raw score of 3 to 8,
    /// the favourite 40 to 65 and the runner-up 15 to 35. The result is normalized like given scores.
    /// </summary>
    public static double[] Synthesize(string text, int experts)
    {
        var random = new Mulberry32(Fnv1a(text));
        var first = Math.Min(experts - 1, (int)(random.Next() * experts));
        var second = (first + 1 + Math.Min(experts - 2, (int)(random.Next() * (experts - 1)))) % experts;
        var raw = new double[experts];
        for (var i = 0; i < experts; i++)
        {
            raw[i] = 3 + 5 * random.Next();
        }
        raw[first] = 40 + 25 * random.Next();
        raw[second] = 15 + 20 * random.Next();
        return Normalize(raw, experts);
    }

    /// <summary>The 32-bit FNV-1a hash of <paramref name="text"/>'s UTF-16 code units.</summary>
    public static uint Fnv1a(string text)
    {
        var hash = 2166136261u;
        foreach (var c in text)
        {
            hash ^= c;
            hash *= 16777619u;
        }
        return hash;
    }

    /// <summary>The <paramref name="k"/> best experts, best first; equal scores go to the lower index.</summary>
    public static int[] TopK(IReadOnlyList<double> p, int k)
    {
        k = Math.Clamp(k, 0, p.Count);
        var order = new int[p.Count];
        for (var i = 0; i < order.Length; i++)
        {
            order[i] = i;
        }
        // A stable insertion sort by score, highest first: ties keep the lower index ahead.
        for (var i = 1; i < order.Length; i++)
        {
            var current = order[i];
            var j = i - 1;
            while (j >= 0 && p[order[j]] < p[current])
            {
                order[j + 1] = order[j];
                j--;
            }
            order[j + 1] = current;
        }
        return order[..k];
    }

    /// <summary>The chosen experts' weights within the choice: each score over the sum of the chosen scores.</summary>
    public static double[] Weights(IReadOnlyList<double> p, IReadOnlyList<int> chosen)
    {
        var sum = 0.0;
        foreach (var e in chosen)
        {
            sum += p[e];
        }
        var w = new double[chosen.Count];
        for (var i = 0; i < w.Length; i++)
        {
            w[i] = sum > 0 ? p[chosen[i]] / sum : 1.0 / w.Length;
        }
        return w;
    }

    /// <summary>
    /// The tokens one expert takes: ⌈factor × tokens × k / experts⌉, at least 1; <see cref="int.MaxValue"/> for an
    /// infinite factor (nothing is dropped).
    /// </summary>
    public static int Capacity(double factor, int tokens, int k, int experts)
    {
        if (double.IsPositiveInfinity(factor))
        {
            return int.MaxValue;
        }
        if (!(factor > 0) || experts <= 0)
        {
            factor = 1;
        }
        var raw = factor * tokens * k / Math.Max(1, experts);
        // A hair under the integer so 4.0000000001 from floating point stays 4.
        return Math.Max(1, (int)Math.Ceiling(raw - 1e-9));
    }

    /// <summary>
    /// (E − max/mean) / (E − 1) with mean = routes / E: 1 when every expert carries the same load, 0 when one expert
    /// carries everything; null while nothing has been routed.
    /// </summary>
    public static double? LoadBalance(IReadOnlyList<int> loads)
    {
        var e = loads.Count;
        var total = 0;
        var max = 0;
        for (var i = 0; i < e; i++)
        {
            total += loads[i];
            max = Math.Max(max, loads[i]);
        }
        if (total <= 0 || e < 2)
        {
            return null;
        }
        var mean = total / (double)e;
        return Math.Clamp((e - max / mean) / (e - 1), 0, 1);
    }

    /// <summary>
    /// Routes every token in order: normalize its scores (or synthesize them), take the top k, weigh them, and load the
    /// chosen experts best first; a chosen expert at capacity skips the token (it keeps its weight, the others are not
    /// raised), and a token with every choice full passes through without adding load.
    /// </summary>
    public static MixtureOfExpertsPlan Plan(IReadOnlyList<(string Text, IReadOnlyList<double>? Scores)> tokens, int experts, int k, double capacityFactor)
    {
        experts = Math.Clamp(experts, MinExperts, MaxExperts);
        k = Math.Clamp(k, 1, Math.Min(2, experts));
        var capacity = Capacity(capacityFactor, tokens.Count, k, experts);
        var loads = new int[experts];
        var routes = new MixtureOfExpertsRoute[tokens.Count];
        var total = 0;
        var dropped = new List<int>(2);
        for (var t = 0; t < tokens.Count; t++)
        {
            var (text, raw) = tokens[t];
            var p = raw is { Count: > 0 } ? Normalize(raw, experts) : Synthesize(text, experts);
            var chosen = TopK(p, k);
            var weights = Weights(p, chosen);
            dropped.Clear();
            foreach (var e in chosen)
            {
                if (loads[e] >= capacity)
                {
                    dropped.Add(e);
                }
                else
                {
                    loads[e]++;
                    total++;
                }
            }
            var snapshot = (int[])loads.Clone();
            routes[t] = new MixtureOfExpertsRoute(t, text, p, chosen, weights, dropped.ToArray(), snapshot, total, LoadBalance(snapshot));
        }
        return new MixtureOfExpertsPlan(routes, experts, k, capacity);
    }

    /// <summary>The mulberry32 generator: 32-bit state, doubles in [0, 1).</summary>
    private struct Mulberry32
    {
        private uint _state;

        public Mulberry32(uint seed) => _state = seed;

        public double Next()
        {
            _state += 0x6D2B79F5u;
            var t = _state;
            t = (t ^ (t >> 15)) * (t | 1u);
            t ^= t + (t ^ (t >> 7)) * (t | 61u);
            return (t ^ (t >> 14)) / 4294967296.0;
        }
    }
}
