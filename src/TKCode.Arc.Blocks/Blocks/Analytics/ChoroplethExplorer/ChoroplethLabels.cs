namespace TKCode.Arc;

/// <summary>
/// Every word a <see cref="ChoroplethExplorer"/> writes or speaks besides the data, with English defaults. Placeholders are
/// numbered as described on each property; nouns come from the area ("states", "county"), metric words from the metric
/// ("revenue"). Assign a new instance to <see cref="ChoroplethExplorer.Labels"/> to translate the block.
/// </summary>
public sealed class ChoroplethLabels
{
    /// <summary>The generated title: {0} the first metric's label, {1} the singular region noun ("Revenue by state").</summary>
    public string TitleFormat { get; set; } = "{0} by {1}";

    /// <summary>A count of regions: {0} the number, {1} the noun in the right number ("51 states", "1 state").</summary>
    public string CountFormat { get; set; } = "{0} {1}";

    /// <summary>The note over the list: {0} the metric noun ("Ranked by revenue").</summary>
    public string RankedByFormat { get; set; } = "Ranked by {0}";

    /// <summary>A rank among all: {0} the ordinal, {1} how many ("8th of 51").</summary>
    public string RankFormat { get; set; } = "{0} of {1}";

    /// <summary>The hover card's hint for a region with subregions: {0} the count of subregions ("Select to see 64 counties").</summary>
    public string OpenHintFormat { get; set; } = "Select to see {0}";

    /// <summary>The hover card's hint for a region without subregions.</summary>
    public string ZoomHintText { get; set; } = "Select to zoom in";

    /// <summary>The button that zooms back out: {0} the plural region noun ("All states").</summary>
    public string BackFormat { get; set; } = "All {0}";

    /// <summary>The button in the empty panel: {0} the plural region noun ("Back to all states").</summary>
    public string BackToAllFormat { get; set; } = "Back to all {0}";

    /// <summary>The empty panel's heading: {0} the singular subregion noun, {1} the region ("No county detail for Texas").</summary>
    public string NoDetailTitleFormat { get; set; } = "No {0} detail for {1}";

    /// <summary>The empty panel's last sentence: {0} the region, {1} its rank ("3rd of 51 states"), {2} the metric noun, {3} the value.</summary>
    public string NoDetailFormat { get; set; } = "{0} ranks {1} by {2}, at {3}.";

    /// <summary>The legend step of subregions without a value.</summary>
    public string NoDataText { get; set; } = "No data";

    /// <summary>The first legend step's name: {0} the count ("10 states"), {1} its upper edge.</summary>
    public string LegendBelowFormat { get; set; } = "{0} below {1}";

    /// <summary>A middle legend step's name: {0} the count, {1} its lower edge, {2} its upper edge ("10 states $120K to $230K").</summary>
    public string LegendRangeFormat { get; set; } = "{0} {1} to {2}";

    /// <summary>The last legend step's name: {0} the count, {1} its lower edge ("10 states $1.6M and above").</summary>
    public string LegendAboveFormat { get; set; } = "{0} {1} and above";

    /// <summary>The no-data step's name: {0} the count ("9 counties with no data").</summary>
    public string LegendNoDataFormat { get; set; } = "{0} with no data";

    /// <summary>A list row's name: {0} the ordinal, {1} the name, {2} the value ("1st, California, $9.8M").</summary>
    public string RowFormat { get; set; } = "{0}, {1}, {2}";

    /// <summary>Added to the name of a row that opens subregions: {0} their count ("Open 58 counties").</summary>
    public string RowOpenFormat { get; set; } = "Open {0}";

    /// <summary>Added to the name of a row without subregions.</summary>
    public string RowZoomText { get; set; } = "Zoom in";

    /// <summary>Said when a region opens: {0} the region, {1} the subregion count, {2} the metric noun.</summary>
    public string ZoomInFormat { get; set; } = "{0}, {1} ranked by {2}. Press Escape to zoom out.";

    /// <summary>Said when a region without subregions opens: {0} the region, {1} the singular subregion noun.</summary>
    public string ZoomInEmptyFormat { get; set; } = "{0}. No {1} detail. Press Escape to zoom out.";

    /// <summary>Said when the map zooms out: {0} the plural region noun, {1} the metric noun.</summary>
    public string ZoomOutFormat { get; set; } = "Showing all {0}, ranked by {1}.";

    /// <summary>Said when a subregion is pinned: {0} its name, {1} its value, {2} its rank ("2nd of 58").</summary>
    public string PinnedFormat { get; set; } = "{0}: {1}, {2}.";

    /// <summary>Said when the metric changes: {0} the metric noun, {1} the first region, {2} its value.</summary>
    public string MetricAnnouncementFormat { get; set; } = "Ranked by {0}. {1} leads at {2}.";

    /// <summary>Said when the area changes: {0} the area, {1} the region count ("13 provinces"), {2} the metric noun.</summary>
    public string AreaAnnouncementFormat { get; set; } = "{0}, {1} ranked by {2}.";

    /// <summary>The map's accessible name: {0} the area or opened region, {1} the metric noun.</summary>
    public string MapNameFormat { get; set; } = "Map of {0} shaded by {1}.";

    /// <summary>The map's help text for screen readers.</summary>
    public string MapHelpText { get; set; } = "The ranked list has the same data. Select a region in the list or on the map to zoom in.";

    /// <summary>The accessible name of the metric switch.</summary>
    public string MetricSwitchName { get; set; } = "Metric";

    /// <summary>The accessible name of the area switch.</summary>
    public string AreaSwitchName { get; set; } = "Map";

    /// <summary>The accessible name of the legend.</summary>
    public string LegendName { get; set; } = "Legend";

    /// <summary>The word read for a minus sign ("minus 13.6%").</summary>
    public string MinusWord { get; set; } = "minus";
}
