namespace TKCode.Arc;

/// <summary>How much thinking a model does before it answers. The order is the distance used to snap a level a model does not offer.</summary>
public enum ModelEffort
{
    /// <summary>Short replies, lowest latency.</summary>
    Low,

    /// <summary>The everyday balance.</summary>
    Medium,

    /// <summary>Works through harder problems.</summary>
    High,

    /// <summary>Long, careful reasoning.</summary>
    ExtraHigh,

    /// <summary>Exhaustive and slowest.</summary>
    Max
}

/// <summary>The English names and one-line descriptions of the <see cref="ModelEffort"/> levels (replaceable in each part's labels).</summary>
public static class ModelEffortNames
{
    /// <summary>The short name of <paramref name="effort"/>: Low, Medium, High, Extra high, Max.</summary>
    public static string Name(ModelEffort effort) => effort switch
    {
        ModelEffort.Low => "Low",
        ModelEffort.Medium => "Medium",
        ModelEffort.High => "High",
        ModelEffort.ExtraHigh => "Extra high",
        _ => "Max"
    };

    /// <summary>The one-line description shown under the name in the effort menu.</summary>
    public static string Description(ModelEffort effort) => effort switch
    {
        ModelEffort.Low => "Short replies, lowest latency",
        ModelEffort.Medium => "Everyday balance",
        ModelEffort.High => "Works through harder problems",
        ModelEffort.ExtraHigh => "Long, careful reasoning",
        _ => "Exhaustive, slowest"
    };
}

/// <summary>Chooses the effort a model can actually run at.</summary>
public static class ModelEffortSnap
{
    /// <summary>
    /// The level to use for a model that offers <paramref name="levels"/>: <paramref name="current"/> when the model
    /// offers it, otherwise the nearest offered level by <see cref="ModelEffort"/> order (the lower one when two are
    /// equally near, the cheaper choice). With no current level (null) it is <paramref name="defaultEffort"/> when
    /// offered, otherwise the middle level. A model without levels has no effort (null).
    /// </summary>
    /// <param name="current">The level chosen so far, or null.</param>
    /// <param name="levels">The levels the model offers, in any order; null or empty means the model has no effort.</param>
    /// <param name="defaultEffort">The model's own default level, or null.</param>
    public static ModelEffort? Snap(ModelEffort? current, IReadOnlyList<ModelEffort>? levels, ModelEffort? defaultEffort = null)
    {
        if (levels is null || levels.Count == 0)
        {
            return null;
        }
        var offered = levels.Distinct().OrderBy(static level => (int)level).ToArray();
        if (current is { } wanted)
        {
            var best = offered[0];
            var bestDistance = Math.Abs((int)best - (int)wanted);
            foreach (var level in offered)
            {
                var distance = Math.Abs((int)level - (int)wanted);
                // Ascending order: a strictly smaller distance wins, a tie keeps the lower level found first.
                if (distance < bestDistance)
                {
                    best = level;
                    bestDistance = distance;
                }
            }
            return best;
        }
        if (defaultEffort is { } preferred && offered.Contains(preferred))
        {
            return preferred;
        }
        return offered[offered.Length / 2];
    }
}
