using Avalonia;

namespace TKCode.Arc;

public partial class ActivityTerrain
{
    /// <summary>The low end of the legend. Default "Less".</summary>
    public static readonly StyledProperty<string> LessTextProperty =
        AvaloniaProperty.Register<ActivityTerrain, string>(nameof(LessText), "Less");

    /// <summary>The high end of the legend. Default "More".</summary>
    public static readonly StyledProperty<string> MoreTextProperty =
        AvaloniaProperty.Register<ActivityTerrain, string>(nameof(MoreText), "More");

    /// <summary>The view switch's 3D option. Default "Terrain".</summary>
    public static readonly StyledProperty<string> PerspectiveTextProperty =
        AvaloniaProperty.Register<ActivityTerrain, string>(nameof(PerspectiveText), "Terrain");

    /// <summary>The view switch's flat option. Default "Heat map".</summary>
    public static readonly StyledProperty<string> TopViewTextProperty =
        AvaloniaProperty.Register<ActivityTerrain, string>(nameof(TopViewText), "Heat map");

    /// <summary>Names the metric switch for screen readers. Default "Metric".</summary>
    public static readonly StyledProperty<string> MetricSwitchLabelProperty =
        AvaloniaProperty.Register<ActivityTerrain, string>(nameof(MetricSwitchLabel), "Metric");

    /// <summary>Names the view switch for screen readers. Default "View".</summary>
    public static readonly StyledProperty<string> ViewSwitchLabelProperty =
        AvaloniaProperty.Register<ActivityTerrain, string>(nameof(ViewSwitchLabel), "View");

    /// <summary>The button that brings the camera back. Default "Reset view".</summary>
    public static readonly StyledProperty<string> ResetViewTextProperty =
        AvaloniaProperty.Register<ActivityTerrain, string>(nameof(ResetViewText), "Reset view");

    /// <summary>The hint under the plot for a mouse or a pen. Default "Drag to turn, pinch to zoom, click a bar to look closer."</summary>
    public static readonly StyledProperty<string> HintTextProperty =
        AvaloniaProperty.Register<ActivityTerrain, string>(nameof(HintText), "Drag to turn, pinch to zoom, click a bar to look closer.");

    /// <summary>The hint once the plot was touched. Default "Swipe sideways to turn, tap a bar to look closer."</summary>
    public static readonly StyledProperty<string> TouchHintTextProperty =
        AvaloniaProperty.Register<ActivityTerrain, string>(nameof(TouchHintText), "Swipe sideways to turn, tap a bar to look closer.");

    /// <summary>The keys of the plot, read by screen readers with it.</summary>
    public static readonly StyledProperty<string> KeyboardHintTextProperty =
        AvaloniaProperty.Register<ActivityTerrain, string>(nameof(KeyboardHintText),
            "Arrow keys move between hours and days, Home and End jump to the start and end of the day. Enter looks closer, Escape steps back. Shift with the arrow keys turns the view; plus and minus zoom.");

    /// <summary>The card's row for the same hour a week earlier. Default "Previous week".</summary>
    public static readonly StyledProperty<string> PreviousWeekTextProperty =
        AvaloniaProperty.Register<ActivityTerrain, string>(nameof(PreviousWeekText), "Previous week");

    /// <summary>The card's row for the change on the previous week. Default "Change".</summary>
    public static readonly StyledProperty<string> ChangeTextProperty =
        AvaloniaProperty.Register<ActivityTerrain, string>(nameof(ChangeText), "Change");

    /// <summary>The card's row for the hour's share of its day; {0} is the day's short name. Default "{0} share".</summary>
    public static readonly StyledProperty<string> ShareFormatProperty =
        AvaloniaProperty.Register<ActivityTerrain, string>(nameof(ShareFormat), "{0} share");

    /// <summary>An hour range; {0} and {1} are the start and the end ("09:00–10:00"). Default "{0}–{1}".</summary>
    public static readonly StyledProperty<string> HourRangeFormatProperty =
        AvaloniaProperty.Register<ActivityTerrain, string>(nameof(HourRangeFormat), "{0}–{1}");

    /// <summary>The summary's title when the metric has no <see cref="ActivityTerrainMetric.SummaryTitle"/>; {0} is the label. Default "{0} this week".</summary>
    public static readonly StyledProperty<string> SummaryTitleFormatProperty =
        AvaloniaProperty.Register<ActivityTerrain, string>(nameof(SummaryTitleFormat), "{0} this week");

    /// <summary>The words after the week's change. Default "from last week".</summary>
    public static readonly StyledProperty<string> ChangeSuffixTextProperty =
        AvaloniaProperty.Register<ActivityTerrain, string>(nameof(ChangeSuffixText), "from last week");

    /// <summary>The title of the peak hours. Default "Peak hours".</summary>
    public static readonly StyledProperty<string> PeakHoursTitleProperty =
        AvaloniaProperty.Register<ActivityTerrain, string>(nameof(PeakHoursTitle), "Peak hours");

    /// <summary>The title of the busiest day. Default "Busiest day".</summary>
    public static readonly StyledProperty<string> BusiestDayTitleProperty =
        AvaloniaProperty.Register<ActivityTerrain, string>(nameof(BusiestDayTitle), "Busiest day");

    /// <summary>
    /// The busiest day's line: {0} is its total, {1} the unit, {2} the week's working-hours share (both numbers roll).
    /// Default "{0} {1}, {2} during working hours".
    /// </summary>
    public static readonly StyledProperty<string> BusiestDayFormatProperty =
        AvaloniaProperty.Register<ActivityTerrain, string>(nameof(BusiestDayFormat), "{0} {1}, {2} during working hours");

    /// <summary>Names the summary for screen readers. Default "Summary of the week".</summary>
    public static readonly StyledProperty<string> SummaryLabelProperty =
        AvaloniaProperty.Register<ActivityTerrain, string>(nameof(SummaryLabel), "Summary of the week");

    /// <summary>Shown on the plot when every value is zero. Default "No data".</summary>
    public static readonly StyledProperty<string> NoDataTextProperty =
        AvaloniaProperty.Register<ActivityTerrain, string>(nameof(NoDataText), "No data");

    /// <summary>
    /// A peak hour button's name for screen readers: {0} the day, {1} the hour, {2} the value, {3} the unit. Default
    /// "{0} {1}, {2} {3}. Show this hour up close".
    /// </summary>
    public static readonly StyledProperty<string> PeakButtonFormatProperty =
        AvaloniaProperty.Register<ActivityTerrain, string>(nameof(PeakButtonFormat), "{0} {1}, {2} {3}. Show this hour up close");

    /// <summary>Announced for a highlighted hour: {0} the day, {1} the hours, {2} the value, {3} the unit. Default "{0}, {1}: {2} {3}.".</summary>
    public static readonly StyledProperty<string> HoverAnnouncementFormatProperty =
        AvaloniaProperty.Register<ActivityTerrain, string>(nameof(HoverAnnouncementFormat), "{0}, {1}: {2} {3}.");

    /// <summary>
    /// Announced for a highlighted hour with a previous week: as <see cref="HoverAnnouncementFormat"/> plus {4}, the
    /// change. Default "{0}, {1}: {2} {3}, {4} on the previous week.".
    /// </summary>
    public static readonly StyledProperty<string> ChangeAnnouncementFormatProperty =
        AvaloniaProperty.Register<ActivityTerrain, string>(nameof(ChangeAnnouncementFormat), "{0}, {1}: {2} {3}, {4} on the previous week.");

    /// <summary>Announced when the camera flies to an hour: {0} the day, {1} the hours. Default "Looking closer at {0}, {1}.".</summary>
    public static readonly StyledProperty<string> FocusAnnouncementFormatProperty =
        AvaloniaProperty.Register<ActivityTerrain, string>(nameof(FocusAnnouncementFormat), "Looking closer at {0}, {1}.");

    /// <summary>Announced when the camera returns to the whole week. Default "Back to the whole week.".</summary>
    public static readonly StyledProperty<string> WeekAnnouncementTextProperty =
        AvaloniaProperty.Register<ActivityTerrain, string>(nameof(WeekAnnouncementText), "Back to the whole week.");

    /// <summary>Announced after Reset view. Default "View reset.".</summary>
    public static readonly StyledProperty<string> ResetAnnouncementTextProperty =
        AvaloniaProperty.Register<ActivityTerrain, string>(nameof(ResetAnnouncementText), "View reset.");

    /// <summary>Announced when the heat map shows. Default "Heat map: color shows how busy each hour was.".</summary>
    public static readonly StyledProperty<string> TopViewAnnouncementTextProperty =
        AvaloniaProperty.Register<ActivityTerrain, string>(nameof(TopViewAnnouncementText), "Heat map: color shows how busy each hour was.");

    /// <summary>Announced when the terrain shows again. Default "Terrain: bar height shows how busy each hour was.".</summary>
    public static readonly StyledProperty<string> PerspectiveAnnouncementTextProperty =
        AvaloniaProperty.Register<ActivityTerrain, string>(nameof(PerspectiveAnnouncementText), "Terrain: bar height shows how busy each hour was.");

    /// <summary>
    /// Announced on a metric switch: {0} the label, {1} the week's total, {2} the busiest hour, {3} its value. Default
    /// "Showing {0}: {1} this week, busiest at {2} with {3}.".
    /// </summary>
    public static readonly StyledProperty<string> MetricAnnouncementFormatProperty =
        AvaloniaProperty.Register<ActivityTerrain, string>(nameof(MetricAnnouncementFormat), "Showing {0}: {1} this week, busiest at {2} with {3}.");

    /// <summary>
    /// The plot's name for screen readers: {0} the label, {1} the total, {2} the change (or nothing), {3} the busiest hour,
    /// {4} the busiest day. Default "{0} by hour for the week: {1} in total{2}. Busiest hour {3}, busiest day {4}. Every hour is in the table that follows.".
    /// </summary>
    public static readonly StyledProperty<string> ChartSummaryFormatProperty =
        AvaloniaProperty.Register<ActivityTerrain, string>(nameof(ChartSummaryFormat),
            "{0} by hour for the week: {1} in total{2}. Busiest hour {3}, busiest day {4}. Every hour is in the table that follows.");

    /// <summary>The caption of the table read by screen readers; {0} is the label. Default "{0} per hour, Monday to Sunday".</summary>
    public static readonly StyledProperty<string> TableCaptionFormatProperty =
        AvaloniaProperty.Register<ActivityTerrain, string>(nameof(TableCaptionFormat), "{0} per hour, Monday to Sunday");

    /// <inheritdoc cref="LessTextProperty"/>
    public string LessText { get => GetValue(LessTextProperty); set => SetValue(LessTextProperty, value); }

    /// <inheritdoc cref="MoreTextProperty"/>
    public string MoreText { get => GetValue(MoreTextProperty); set => SetValue(MoreTextProperty, value); }

    /// <inheritdoc cref="PerspectiveTextProperty"/>
    public string PerspectiveText { get => GetValue(PerspectiveTextProperty); set => SetValue(PerspectiveTextProperty, value); }

    /// <inheritdoc cref="TopViewTextProperty"/>
    public string TopViewText { get => GetValue(TopViewTextProperty); set => SetValue(TopViewTextProperty, value); }

    /// <inheritdoc cref="MetricSwitchLabelProperty"/>
    public string MetricSwitchLabel { get => GetValue(MetricSwitchLabelProperty); set => SetValue(MetricSwitchLabelProperty, value); }

    /// <inheritdoc cref="ViewSwitchLabelProperty"/>
    public string ViewSwitchLabel { get => GetValue(ViewSwitchLabelProperty); set => SetValue(ViewSwitchLabelProperty, value); }

    /// <inheritdoc cref="ResetViewTextProperty"/>
    public string ResetViewText { get => GetValue(ResetViewTextProperty); set => SetValue(ResetViewTextProperty, value); }

    /// <inheritdoc cref="HintTextProperty"/>
    public string HintText { get => GetValue(HintTextProperty); set => SetValue(HintTextProperty, value); }

    /// <inheritdoc cref="TouchHintTextProperty"/>
    public string TouchHintText { get => GetValue(TouchHintTextProperty); set => SetValue(TouchHintTextProperty, value); }

    /// <inheritdoc cref="KeyboardHintTextProperty"/>
    public string KeyboardHintText { get => GetValue(KeyboardHintTextProperty); set => SetValue(KeyboardHintTextProperty, value); }

    /// <inheritdoc cref="PreviousWeekTextProperty"/>
    public string PreviousWeekText { get => GetValue(PreviousWeekTextProperty); set => SetValue(PreviousWeekTextProperty, value); }

    /// <inheritdoc cref="ChangeTextProperty"/>
    public string ChangeText { get => GetValue(ChangeTextProperty); set => SetValue(ChangeTextProperty, value); }

    /// <inheritdoc cref="ShareFormatProperty"/>
    public string ShareFormat { get => GetValue(ShareFormatProperty); set => SetValue(ShareFormatProperty, value); }

    /// <inheritdoc cref="HourRangeFormatProperty"/>
    public string HourRangeFormat { get => GetValue(HourRangeFormatProperty); set => SetValue(HourRangeFormatProperty, value); }

    /// <inheritdoc cref="SummaryTitleFormatProperty"/>
    public string SummaryTitleFormat { get => GetValue(SummaryTitleFormatProperty); set => SetValue(SummaryTitleFormatProperty, value); }

    /// <inheritdoc cref="ChangeSuffixTextProperty"/>
    public string ChangeSuffixText { get => GetValue(ChangeSuffixTextProperty); set => SetValue(ChangeSuffixTextProperty, value); }

    /// <inheritdoc cref="PeakHoursTitleProperty"/>
    public string PeakHoursTitle { get => GetValue(PeakHoursTitleProperty); set => SetValue(PeakHoursTitleProperty, value); }

    /// <inheritdoc cref="BusiestDayTitleProperty"/>
    public string BusiestDayTitle { get => GetValue(BusiestDayTitleProperty); set => SetValue(BusiestDayTitleProperty, value); }

    /// <inheritdoc cref="BusiestDayFormatProperty"/>
    public string BusiestDayFormat { get => GetValue(BusiestDayFormatProperty); set => SetValue(BusiestDayFormatProperty, value); }

    /// <inheritdoc cref="SummaryLabelProperty"/>
    public string SummaryLabel { get => GetValue(SummaryLabelProperty); set => SetValue(SummaryLabelProperty, value); }

    /// <inheritdoc cref="NoDataTextProperty"/>
    public string NoDataText { get => GetValue(NoDataTextProperty); set => SetValue(NoDataTextProperty, value); }

    /// <inheritdoc cref="PeakButtonFormatProperty"/>
    public string PeakButtonFormat { get => GetValue(PeakButtonFormatProperty); set => SetValue(PeakButtonFormatProperty, value); }

    /// <inheritdoc cref="HoverAnnouncementFormatProperty"/>
    public string HoverAnnouncementFormat { get => GetValue(HoverAnnouncementFormatProperty); set => SetValue(HoverAnnouncementFormatProperty, value); }

    /// <inheritdoc cref="ChangeAnnouncementFormatProperty"/>
    public string ChangeAnnouncementFormat { get => GetValue(ChangeAnnouncementFormatProperty); set => SetValue(ChangeAnnouncementFormatProperty, value); }

    /// <inheritdoc cref="FocusAnnouncementFormatProperty"/>
    public string FocusAnnouncementFormat { get => GetValue(FocusAnnouncementFormatProperty); set => SetValue(FocusAnnouncementFormatProperty, value); }

    /// <inheritdoc cref="WeekAnnouncementTextProperty"/>
    public string WeekAnnouncementText { get => GetValue(WeekAnnouncementTextProperty); set => SetValue(WeekAnnouncementTextProperty, value); }

    /// <inheritdoc cref="ResetAnnouncementTextProperty"/>
    public string ResetAnnouncementText { get => GetValue(ResetAnnouncementTextProperty); set => SetValue(ResetAnnouncementTextProperty, value); }

    /// <inheritdoc cref="TopViewAnnouncementTextProperty"/>
    public string TopViewAnnouncementText { get => GetValue(TopViewAnnouncementTextProperty); set => SetValue(TopViewAnnouncementTextProperty, value); }

    /// <inheritdoc cref="PerspectiveAnnouncementTextProperty"/>
    public string PerspectiveAnnouncementText { get => GetValue(PerspectiveAnnouncementTextProperty); set => SetValue(PerspectiveAnnouncementTextProperty, value); }

    /// <inheritdoc cref="MetricAnnouncementFormatProperty"/>
    public string MetricAnnouncementFormat { get => GetValue(MetricAnnouncementFormatProperty); set => SetValue(MetricAnnouncementFormatProperty, value); }

    /// <inheritdoc cref="ChartSummaryFormatProperty"/>
    public string ChartSummaryFormat { get => GetValue(ChartSummaryFormatProperty); set => SetValue(ChartSummaryFormatProperty, value); }

    /// <inheritdoc cref="TableCaptionFormatProperty"/>
    public string TableCaptionFormat { get => GetValue(TableCaptionFormatProperty); set => SetValue(TableCaptionFormatProperty, value); }

    private static bool IsTextProperty(AvaloniaProperty property) =>
        property == LessTextProperty || property == MoreTextProperty || property == PerspectiveTextProperty ||
        property == TopViewTextProperty || property == ResetViewTextProperty || property == HintTextProperty ||
        property == TouchHintTextProperty || property == KeyboardHintTextProperty || property == PreviousWeekTextProperty ||
        property == ChangeTextProperty || property == ShareFormatProperty || property == HourRangeFormatProperty ||
        property == SummaryTitleFormatProperty || property == ChangeSuffixTextProperty || property == PeakHoursTitleProperty ||
        property == BusiestDayTitleProperty || property == BusiestDayFormatProperty || property == SummaryLabelProperty ||
        property == NoDataTextProperty || property == PeakButtonFormatProperty || property == ChartSummaryFormatProperty ||
        property == TableCaptionFormatProperty || property == MetricSwitchLabelProperty || property == ViewSwitchLabelProperty;
}
