// Ported from Arc's free plan-comparison block (MIT, Copyright (c) 2026 Elia Kuratli).
namespace TKCode.Arc;

/// <summary>
/// The sample offering a <see cref="PlanComparison"/> shows until it is given plans and features: a Team and a Studio
/// plan priced per seat and eight features, two of which both plans share. Each call returns new objects.
/// </summary>
public static class PlanComparisonSamples
{
    /// <summary>Team ($13 monthly, $10 yearly) and Studio ($27 monthly, $22 yearly).</summary>
    public static IReadOnlyList<PlanComparisonPlan> Plans() =>
    [
        new PlanComparisonPlan("Team", "For smaller teams", 13, 10),
        new PlanComparisonPlan("Studio", "For work across teams", 27, 22)
    ];

    /// <summary>Eight features with a Team and a Studio value each; "Active projects" and "Source exports" are shared.</summary>
    public static IReadOnlyList<PlanComparisonFeature> Features() =>
    [
        new PlanComparisonFeature("Active projects", true, "Unlimited", "Unlimited"),
        new PlanComparisonFeature("Shared workspaces", false, "1 workspace", "Unlimited"),
        new PlanComparisonFeature("Guest reviewers", false, "5 per project", "Unlimited"),
        new PlanComparisonFeature("Version history", false, "30 days", "Unlimited"),
        new PlanComparisonFeature("Approval flows", false, "Not included", "Included"),
        new PlanComparisonFeature("Custom roles", false, "Not included", "Included"),
        new PlanComparisonFeature("Source exports", true, "Included", "Included"),
        new PlanComparisonFeature("Support", false, "Email", "Priority email")
    ];
}
