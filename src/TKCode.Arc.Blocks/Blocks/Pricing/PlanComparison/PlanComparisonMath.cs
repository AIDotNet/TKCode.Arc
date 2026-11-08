// Ported from Arc's free plan-comparison block (MIT, Copyright (c) 2026 Elia Kuratli).
namespace TKCode.Arc;

/// <summary>Arc's width tiers for the block (<c>data-size</c>), measured on the block's own content width.</summary>
internal enum PlanComparisonSize
{
    /// <summary>Wider than 720: the full layout with 32px gutters.</summary>
    Large,

    /// <summary>541–720: billing stacks under the heading, gutters tighten to 20px.</summary>
    Medium,

    /// <summary>341–540: the feature column goes, each name sits above its values.</summary>
    Small,

    /// <summary>340 and less: plan and value padding tighten to 12/16px.</summary>
    Tiny
}

/// <summary>The pure rules of <see cref="PlanComparison"/>, kept apart so tests can check them against Arc's source.</summary>
internal static class PlanComparisonMath
{
    /// <summary>Arc: <c>width &lt;= 340 ? "xs" : width &lt;= 540 ? "sm" : width &lt;= 720 ? "md" : "lg"</c>.</summary>
    public static PlanComparisonSize SizeFor(double width, double medium = 720, double small = 540, double tiny = 340)
    {
        if (width <= tiny)
        {
            return PlanComparisonSize.Tiny;
        }
        if (width <= small)
        {
            return PlanComparisonSize.Small;
        }
        return width <= medium ? PlanComparisonSize.Medium : PlanComparisonSize.Large;
    }

    /// <summary>Whether a feature row shows: everything, or only rows the plans do not share.</summary>
    public static bool IsRowShown(PlanComparisonFeature feature, bool differencesOnly) => !differencesOnly || !feature.IsShared;

    /// <summary>How many of <paramref name="features"/> show.</summary>
    public static int VisibleCount(IEnumerable<PlanComparisonFeature> features, bool differencesOnly)
        => features.Count(f => IsRowShown(f, differencesOnly));

    /// <summary>
    /// The largest yearly saving of any plan, in whole percent of its monthly price, rounded to the nearest
    /// (Team 13 → 10 saves 23%, Studio 27 → 22 saves 18.5%: "save up to 23%"). Plans without a monthly price or that
    /// cost more yearly count as 0.
    /// </summary>
    public static int MaxYearlySaving(IEnumerable<PlanComparisonPlan> plans)
    {
        var best = 0m;
        foreach (var plan in plans)
        {
            if (plan.MonthlyPrice > 0 && plan.YearlyPrice < plan.MonthlyPrice)
            {
                best = Math.Max(best, (plan.MonthlyPrice - plan.YearlyPrice) / plan.MonthlyPrice);
            }
        }
        return (int)Math.Round(best * 100m, MidpointRounding.AwayFromZero);
    }

    /// <summary>
    /// Digits after the point while a price counts from <paramref name="from"/> to <paramref name="to"/>: whole prices
    /// count in whole units (Arc's <c>Math.round</c>), prices with cents count in cents.
    /// </summary>
    public static int CountDecimals(decimal from, decimal to)
        => decimal.Truncate(from) == from && decimal.Truncate(to) == to ? 0 : 2;

    /// <summary>The value shown at <paramref name="progress"/> (eased, 0–1) of a count, rounded like Arc's.</summary>
    public static decimal CountValue(decimal from, decimal to, double progress, int decimals)
    {
        var value = (double)from + ((double)to - (double)from) * progress;
        return Math.Round((decimal)value, decimals, MidpointRounding.AwayFromZero);
    }
}
