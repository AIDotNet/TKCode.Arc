// Ported from Arc's free plan-comparison block (MIT, Copyright (c) 2026 Elia Kuratli).
using System.Globalization;

namespace TKCode.Arc;

/// <summary>
/// The built-in text of <see cref="PlanComparison"/>. Replace the whole object to localize the block; sentences with a
/// number or a plan name are functions. Prices and counts use <see cref="Culture"/>.
/// </summary>
public sealed class PlanComparisonLabels
{
    /// <summary>Culture for prices and counts (default en-US).</summary>
    public CultureInfo Culture { get; init; } = SettingsFormat.DefaultCulture;

    /// <summary>The block's heading: "Room for the way you work".</summary>
    public string Title { get; init; } = "Room for the way you work";

    /// <summary>The sentence under the heading.</summary>
    public string Description { get; init; } = "Only the details that change between plans. Switch billing to see what you would pay.";

    /// <summary>Accessible name of the billing switch: "Billing period".</summary>
    public string BillingPeriod { get; init; } = "Billing period";

    /// <summary>The monthly segment: "Monthly".</summary>
    public string Monthly { get; init; } = "Monthly";

    /// <summary>The yearly segment: "Yearly".</summary>
    public string Yearly { get; init; } = "Yearly";

    /// <summary>The note under the billing switch while monthly prices show: "Billed month to month".</summary>
    public string MonthlyNote { get; init; } = "Billed month to month";

    /// <summary>
    /// The note under the billing switch while yearly prices show; receives the largest yearly saving of any plan in whole
    /// percent: 23 ⇒ "Billed yearly, save up to 23%" (0 ⇒ "Billed yearly").
    /// </summary>
    public Func<int, string> YearlyNote { get; init; } = percent => percent > 0 ? $"Billed yearly, save up to {percent}%" : "Billed yearly";

    /// <summary>The heading of the filter bar: "Compare plans".</summary>
    public string CompareTitle { get; init; } = "Compare plans";

    /// <summary>The count beside it: (shown, total) ⇒ "6 of 8 features".</summary>
    public Func<int, int, string> FeatureCount { get; init; } = (shown, total) => $"{shown} of {total} features";

    /// <summary>The label of the filter switch: "Show differences only".</summary>
    public string DifferencesOnly { get; init; } = "Show differences only";

    /// <summary>The header over the feature names: "What changes".</summary>
    public string FeatureHeader { get; init; } = "What changes";

    /// <summary>Accessible name of the matrix; receives the plan names: ["Team", "Studio"] ⇒ "Team and Studio plan comparison".</summary>
    public Func<IReadOnlyList<string>, string> TableName { get; init; } = names => names.Count switch
    {
        0 => "Plan comparison",
        1 => $"{names[0]} plan comparison",
        _ => $"{string.Join(", ", names.Take(names.Count - 1))} and {names[^1]} plan comparison"
    };

    /// <summary>Accessible name of a plan's column header: "Team" ⇒ "Team plan".</summary>
    public Func<string, string> PlanHeader { get; init; } = name => $"{name} plan";

    /// <summary>The text after each price: "per seat / month".</summary>
    public string PricePeriod { get; init; } = "per seat / month";

    /// <summary>A plan's select button: "Team" ⇒ "Select Team".</summary>
    public Func<string, string> Select { get; init; } = name => $"Select {name}";

    /// <summary>The select button of the chosen plan: "Selected".</summary>
    public string Selected { get; init; } = "Selected";

    /// <summary>Announced when a plan is chosen: "Team" ⇒ "Team selected".</summary>
    public Func<string, string> SelectedAnnouncement { get; init; } = name => $"{name} selected";

    /// <summary>A feature value that shows a check: "Included".</summary>
    public string Included { get; init; } = "Included";

    /// <summary>A feature value that is muted: "Not included".</summary>
    public string NotIncluded { get; init; } = "Not included";

    /// <summary>The footnote: "Prices in USD per seat. Yearly plans are billed annually.".</summary>
    public string Footnote { get; init; } = "Prices in USD per seat. Yearly plans are billed annually.";
}
