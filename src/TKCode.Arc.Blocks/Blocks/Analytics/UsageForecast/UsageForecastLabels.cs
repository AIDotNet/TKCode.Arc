namespace TKCode.Arc;

/// <summary>
/// Every word a <see cref="UsageForecast"/> writes or speaks besides your data, with English defaults. Placeholders are
/// numbered as described on each property. Assign a new instance to <see cref="UsageForecast.Labels"/> to translate the block.
/// </summary>
public sealed class UsageForecastLabels
{
    /// <summary>The metric switch's group name.</summary>
    public string MetricSwitchText { get; set; } = "Metric";

    /// <summary>The headline when the budget is already passed: {0} the day.</summary>
    public string PassedHeadlineFormat { get; set; } = "You went over budget on {0}";

    /// <summary>The line under it: {0} the percent above the budget, {1} the forecast, {2} the last day.</summary>
    public string PassedDetailFormat { get; set; } = "{0}% above budget now, trending to {1} by {2}.";

    /// <summary>The headline when the forecast reaches the budget: {0} the day.</summary>
    public string CrossHeadlineFormat { get; set; } = "On this pace you hit the budget on {0}";

    /// <summary>The line under it with a range: {0} the time left ("4 days"), {1} the earliest day, {2} the latest day.</summary>
    public string CrossDetailFormat { get; set; } = "{0} before the cycle ends, most likely {1}–{2}.";

    /// <summary>The line under it when the range is a single day: {0} the time left.</summary>
    public string CrossDetailPlainFormat { get; set; } = "{0} before the cycle ends.";

    /// <summary>A number of days: {0} the number.</summary>
    public string DaysFormat { get; set; } = "{0} days";

    /// <summary>One day.</summary>
    public string OneDayText { get; set; } = "1 day";

    /// <summary>The line under the headline when the budget is reached on the last day: {0} the earliest day.</summary>
    public string LastDayDetailFormat { get; set; } = "On the final day, possibly from {0}.";

    /// <summary>The same without an earlier day.</summary>
    public string LastDayPlainText { get; set; } = "On the final day of the cycle.";

    /// <summary>The headline when the forecast stays under the budget.</summary>
    public string UnderHeadline { get; set; } = "On this pace you finish under budget";

    /// <summary>The line under it: {0} the forecast, {1} the last day, {2} the percent of the budget.</summary>
    public string UnderDetailFormat { get; set; } = "Trending to {0} by {1}, {2}% of budget.";

    /// <summary>Appended when the top of the range reaches the budget: {0} the day.</summary>
    public string UnderRiskFormat { get; set; } = " A heavy week could tip it on {0}.";

    /// <summary>The first tile's label.</summary>
    public string UsedLabel { get; set; } = "Used to date";

    /// <summary>The second tile's label: {0} the last day.</summary>
    public string ForecastLabelFormat { get; set; } = "Forecast for {0}";

    /// <summary>The third tile's label.</summary>
    public string OverageLabel { get; set; } = "Overage";

    /// <summary>The first tile's small text: {0} the percent of the budget.</summary>
    public string UsedSmallFormat { get; set; } = "{0}% of budget";

    /// <summary>The second tile's small text: {0} the low end, {1} the high end.</summary>
    public string RangeSmallFormat { get; set; } = "Range {0}–{1}";

    /// <summary>The third tile's small text while there is no overage.</summary>
    public string UnderBudgetText { get; set; } = "Under budget";

    /// <summary>The label before the alert chips.</summary>
    public string NotifyAtText { get; set; } = "Notify at";

    /// <summary>The alert group's name.</summary>
    public string AlertGroupText { get; set; } = "Budget alerts";

    /// <summary>A chip's status when the threshold was crossed on day one: {0} the day.</summary>
    public string SinceFormat { get; set; } = "Since {0}";

    /// <summary>A chip's status for a threshold crossed on a measured day: {0} the day.</summary>
    public string ReachedFormat { get; set; } = "Hit {0}";

    /// <summary>A chip's status for a threshold the forecast reaches: {0} the day.</summary>
    public string ExpectedFormat { get; set; } = "Due {0}";

    /// <summary>A chip's status for a threshold the cycle never reaches.</summary>
    public string NotExpectedText { get; set; } = "Not on track";

    /// <summary>A chip's spoken name: {0} the percent, {1} the status.</summary>
    public string ChipNameFormat { get; set; } = "{0}% of budget, {1}";

    /// <summary>The marker label of the present day.</summary>
    public string TodayText { get; set; } = "Today";

    /// <summary>The budget handle's word.</summary>
    public string BudgetText { get; set; } = "Budget";

    /// <summary>The legend's group name.</summary>
    public string LegendText { get; set; } = "Legend";

    /// <summary>Legend: the measured line.</summary>
    public string LegendMeasuredText { get; set; } = "Measured";

    /// <summary>Legend: the dashed forecast.</summary>
    public string LegendProjectedText { get; set; } = "Projected";

    /// <summary>Legend: the cone.</summary>
    public string LegendRangeText { get; set; } = "Expected range";

    /// <summary>Legend: the red part above the budget.</summary>
    public string LegendOverText { get; set; } = "Over budget";

    /// <summary>The inspect card's tag for a measured day.</summary>
    public string MeasuredTag { get; set; } = "Measured";

    /// <summary>The inspect card's tag for a forecast day.</summary>
    public string ProjectedTag { get; set; } = "Projected";

    /// <summary>The inspect card's line for a measured day: {0} the percent of the budget.</summary>
    public string PanelUsedFormat { get; set; } = "{0}% of budget";

    /// <summary>The inspect card's line for a forecast day: {0} low, {1} high (with units).</summary>
    public string PanelRangeFormat { get; set; } = "Range {0}–{1}";

    /// <summary>The hint read with the budget handle.</summary>
    public string DragHintText { get; set; } = "Drag the budget line or press the arrow keys.";

    /// <summary>The budget handle's name.</summary>
    public string BudgetHandleName { get; set; } = "Budget";

    /// <summary>The day inspector's name.</summary>
    public string InspectorName { get; set; } = "Day inspector";

    /// <summary>The spoken value of a measured day: {0} the day, {1} the value, {2} the percent of the budget.</summary>
    public string SpokenMeasuredFormat { get; set; } = "{0}: {1} used, {2}% of budget";

    /// <summary>The spoken value of a forecast day: {0} the day, {1} the value, {2} low, {3} high.</summary>
    public string SpokenProjectedFormat { get; set; } = "{0} projected: {1}, likely {2} to {3}";

    /// <summary>The chart's summary: {0} the metric, {1} used, {2} the last day, {3} the forecast, {4} the budget, {5} the headline.</summary>
    public string ChartSummaryFormat { get; set; } = "{0}: {1} used so far, {3} forecast by {2}, budget {4}. {5}";

    /// <summary>The data table's caption: {0} the metric, {1} the cycle ("Jun 1 to Jun 30").</summary>
    public string TableCaptionFormat { get; set; } = "{0}, {1}";

    /// <summary>The table's range cell for a measured day.</summary>
    public string TableMeasuredText { get; set; } = "Measured";

    /// <summary>The table's range cell for a forecast day: {0} low, {1} high.</summary>
    public string TableRangeFormat { get; set; } = "{0} to {1}";

    /// <summary>The table's marker after a forecast day's name.</summary>
    public string TableProjectedSuffix { get; set; } = " (projected)";

    /// <summary>The spoken change of the budget: {0} the budget.</summary>
    public string BudgetAnnouncementFormat { get; set; } = "Budget {0}.";

    /// <summary>The spoken change of a threshold: {0} the percent, {1} on or off.</summary>
    public string AlertAnnouncementFormat { get; set; } = "Alert at {0}% {1}.";

    /// <summary>The word for on.</summary>
    public string OnText { get; set; } = "on";

    /// <summary>The word for off.</summary>
    public string OffText { get; set; } = "off";
}
