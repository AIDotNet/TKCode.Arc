namespace TKCode.Arc;

/// <summary>
/// Every word a <see cref="KpiDrilldown"/> writes or speaks besides the data, with English defaults. Placeholders are
/// numbered as described on each property. Assign a new instance to <see cref="KpiDrilldown.Labels"/> to translate the
/// block.
/// </summary>
public sealed class KpiDrilldownLabels
{
    /// <summary>The toggle that overlays the prior period.</summary>
    public string CompareText { get; set; } = "Compare";

    /// <summary>A range segment: {0} the days ("30 days").</summary>
    public string RangeFormat { get; set; } = "{0} days";

    /// <summary>A range segment's spoken name: {0} the days ("Last 30 days").</summary>
    public string RangeSpokenFormat { get; set; } = "Last {0} days";

    /// <summary>The range switch's group name.</summary>
    public string RangeGroupText { get; set; } = "Date range";

    /// <summary>The legend entry of the current period: {0} the days ("Last 30 days").</summary>
    public string CurrentPeriodFormat { get; set; } = "Last {0} days";

    /// <summary>The legend entry of the prior period: {0} the days ("Prior 30 days").</summary>
    public string PriorPeriodFormat { get; set; } = "Prior {0} days";

    /// <summary>The detail view's comparison line: {0} the prior value, {1} the days ("vs $178.3k in the prior 30 days").</summary>
    public string VersusFormat { get; set; } = "vs {0} in the prior {1} days";

    /// <summary>Spoken after a change: {0} the days ("against the prior 30 days").</summary>
    public string AgainstFormat { get; set; } = "against the prior {0} days";

    /// <summary>The tooltip's prior-period line: {0} the prior value ("$173.4k one period back").</summary>
    public string PeriodEarlierFormat { get; set; } = "{0} one period back";

    /// <summary>
    /// A card's spoken name: {0} the label, {1} the value, {2} the change in words ("up 3.3%"), {3} the days
    /// ("Annual run rate, $2.4M, up 3.3% against the prior 30 days. Show details").
    /// </summary>
    public string CardFormat { get; set; } = "{0}, {1}, {2} against the prior {3} days. Show details";

    /// <summary>The close button's spoken name.</summary>
    public string CloseText { get; set; } = "Return to overview";

    /// <summary>The breakdown heading.</summary>
    public string BreakdownText { get; set; } = "Split";

    /// <summary>The dimension switch's group name.</summary>
    public string BreakdownGroupText { get; set; } = "Break down by";

    /// <summary>The breakdown section's spoken name: {0} the metric, {1} the dimension ("Annual run rate by Tier").</summary>
    public string SectionFormat { get; set; } = "{0} by {1}";

    /// <summary>The chart's spoken summary: {0} the metric, {1} the days, {2} the first value, {3} the last value.</summary>
    public string ChartFormat { get; set; } = "{0} across the last {1} days, from {2} to {3}.";

    /// <summary>Spoken when the chart cursor moves by keyboard: {0} the date, {1} the value.</summary>
    public string CursorFormat { get; set; } = "{0}, {1}";

    /// <summary>The breakdown column headings after the dimension.</summary>
    public string ShareText { get; set; } = "Share";

    /// <inheritdoc cref="ShareText"/>
    public string ValueText { get; set; } = "Value";

    /// <inheritdoc cref="ShareText"/>
    public string ChangeText { get; set; } = "Change";

    /// <summary>Words for a change's direction ("up 3.3%", "down 0.2 pts", "unchanged").</summary>
    public string UpText { get; set; } = "up";

    /// <inheritdoc cref="UpText"/>
    public string DownText { get; set; } = "down";

    /// <inheritdoc cref="UpText"/>
    public string UnchangedText { get; set; } = "unchanged";
}
