namespace TKCode.Arc;

/// <summary>
/// Every word a <see cref="HorizonChart"/> writes or speaks besides the data, with English defaults. Placeholders are
/// numbered as described on each property. Assign a new instance to <see cref="HorizonChart.Labels"/> to translate the block.
/// </summary>
public sealed class HorizonChartLabels
{
    /// <summary>The small label beside the sort switch.</summary>
    public string SortText { get; set; } = "Sort";

    /// <summary>The small label beside the band switch.</summary>
    public string BandsText { get; set; } = "Bands";

    /// <summary>The sort switch's group name.</summary>
    public string SortGroupText { get; set; } = "Sort rows";

    /// <summary>The band switch's group name.</summary>
    public string BandsGroupText { get; set; } = "Band count";

    /// <summary>The sort switch's name button.</summary>
    public string NameText { get; set; } = "Name";

    /// <summary>The sort switch's peak button (largest swing first).</summary>
    public string PeakText { get; set; } = "Peak";

    /// <summary>The sort switch's latest button (last value first).</summary>
    public string LatestText { get; set; } = "Latest";

    /// <summary>The line under the date while no day is picked.</summary>
    public string LatestDayText { get; set; } = "Most recent day";

    /// <summary>The line under the date while the crosshair is on a day.</summary>
    public string SelectedDayText { get; set; } = "Picked day";

    /// <summary>The scale's label for the side under the baseline.</summary>
    public string BelowText { get; set; } = "Under";

    /// <summary>The scale's label for the side over the baseline.</summary>
    public string AboveText { get; set; } = "Over";

    /// <summary>What the values are measured against, in the caption and the spoken texts ("winter forecast").</summary>
    public string BaselineText { get; set; } = "baseline";

    /// <summary>The caption for two or more bands: {0} the band count, {1} the size of one band, {2} the baseline.</summary>
    public string CaptionFormat { get; set; } = "Rows fold into {0} bands of {1}. Darker color means a larger gap from the {2}.";

    /// <summary>The caption for one band: {0} the extent, {1} the baseline.</summary>
    public string CaptionOneBandFormat { get; set; } = "With a single band each row is an ordinary area chart up to {0} from the {1}.";

    /// <summary>The colour scale's spoken description: {0} the under end, {1} the over end, {2} the baseline, {3} the bands per side.</summary>
    public string ScaleDescriptionFormat { get; set; } = "Color scale from {0} under the {2} to {1} over it, {3} per side.";

    /// <summary>One band, in the scale's description.</summary>
    public string OneBandText { get; set; } = "one band";

    /// <summary>Several bands, in the scale's description: {0} the count.</summary>
    public string BandCountFormat { get; set; } = "{0} bands";

    /// <summary>The chart's name: {0} the title, {1} the number of rows.</summary>
    public string ChartNameFormat { get; set; } = "{0}. {1} rows";

    /// <summary>The chart's help text (the keys).</summary>
    public string KeyboardHint { get; set; } = "Arrow keys move the crosshair a day, Shift plus an arrow moves a week, Home and End go to the ends, Escape clears it.";

    /// <summary>Said when the crosshair moves by key: {0} the day, {1} the top row, {2} its value, {3} the bottom row, {4} its value.</summary>
    public string DayAnnouncementFormat { get; set; } = "{0}. Top {1} {2}, bottom {3} {4}.";

    /// <summary>Said when the rows are sorted by name.</summary>
    public string SortedByNameText { get; set; } = "Sorted by name";

    /// <summary>Said when the rows are sorted by their largest gap.</summary>
    public string SortedByPeakText { get; set; } = "Sorted by largest gap";

    /// <summary>Said when the rows are sorted by their latest value.</summary>
    public string SortedByLatestText { get; set; } = "Sorted by latest value";

    /// <summary>Said when the band count changes: {0} the count, {1} the size of one band.</summary>
    public string BandsAnnouncementFormat { get; set; } = "{0} bands of {1}";

    /// <summary>Said when there is a single band: {0} the extent.</summary>
    public string OneBandAnnouncementFormat { get; set; } = "One band of {0}";

    /// <summary>The screen reader table's caption: {0} the title, {1} the baseline.</summary>
    public string TableCaptionFormat { get; set; } = "{0}, values against the {1}";

    /// <summary>The table's latest column.</summary>
    public string LatestColumnText { get; set; } = "Latest";

    /// <summary>The table's highest column.</summary>
    public string HighestColumnText { get; set; } = "Highest";

    /// <summary>The table's lowest column.</summary>
    public string LowestColumnText { get; set; } = "Lowest";

    /// <summary>A table cell: {0} the column, {1} the value, {2} the day.</summary>
    public string TableCellFormat { get; set; } = "{0}: {1} on {2}";

    /// <summary>Shown when there are no rows.</summary>
    public string EmptyText { get; set; } = "No rows to show.";
}
