using System.Collections;
using System.Windows.Input;
using Avalonia;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Interactivity;
using Avalonia.Metadata;

namespace TKCode.Arc;

/// <summary>
/// Connected scatter: two measures that move together over time, drawn as one path. Each month is a point, joined in
/// time order by a smooth curve with a small arrow on every segment long enough to hold one; a head point marks the
/// current month and a scrubber under the chart moves it along the real curve. A dashed target line (y = k · x) splits the
/// plot and the healthy side is tinted. A segment switch changes the series (point by point morph), the four readouts
/// above count with the head, and Play walks the head through the months at a steady pace. The first time the block is on
/// screen the head runs from the first month to the last and leaves its trail behind.
/// <code>
/// &lt;arc:ConnectedScatter Title="Unit economics by channel" TargetRatio="3"&gt;
///     &lt;arc:ConnectedScatter.XAxis&gt;&lt;arc:ScatterAxis Name="CAC" Title="Acquisition cost" Minimum="300" Maximum="1900" Step="200" /&gt;&lt;/arc:ConnectedScatter.XAxis&gt;
///     &lt;arc:ScatterSeries Name="Field sales"&gt;
///         &lt;arc:ScatterPoint Label="Jul 2024" X="1150" Y="3600" /&gt;
///     &lt;/arc:ScatterSeries&gt;
/// &lt;/arc:ConnectedScatter&gt;
/// </code>
/// </summary>
/// <remarks>
/// Keys: the scrubber is one tab stop; ← / ↓ go one month back, → / ↑ one on, PageUp / PageDown three on / back, Home and End
/// the ends (a position between two months steps to the next whole month in the direction pressed). The series switch moves
/// with the arrow keys and selects as it moves. Play, Pause and Replay are a button. The chart itself takes no focus; its data
/// is also a table for screen readers and every settled month is announced.
/// </remarks>
[TemplatePart(HeaderPart, typeof(Panel))]
[TemplatePart(SeriesSwitchPart, typeof(SegmentedControl))]
[TemplatePart(DescriptionPart, typeof(TextBlock))]
[TemplatePart(ReadoutsPart, typeof(Control))]
[TemplatePart(ChartHeaderPart, typeof(Panel))]
[TemplatePart(YTitlePart, typeof(TextBlock))]
[TemplatePart(LegendPart, typeof(Control))]
[TemplatePart(PlotPart, typeof(Control))]
[TemplatePart(XTitlePart, typeof(TextBlock))]
[TemplatePart(TransportPart, typeof(Grid))]
[TemplatePart(PlayButtonPart, typeof(Button))]
[TemplatePart(PlayGlyphPart, typeof(ArcGlyph))]
[TemplatePart(PlayTextPart, typeof(TextBlock))]
[TemplatePart(ScrubberPart, typeof(Control))]
[TemplatePart(TablePart, typeof(Control))]
[TemplatePart(AnnouncerPart, typeof(Control))]
[PseudoClasses(PlayingPseudoClass, NarrowPseudoClass, CompactPseudoClass, EmptyPseudoClass)]
public partial class ConnectedScatter : TemplatedControl
{
    public const string HeaderPart = "PART_Header";
    public const string SeriesSwitchPart = "PART_SeriesSwitch";
    public const string DescriptionPart = "PART_Description";
    public const string ReadoutsPart = "PART_Readouts";
    public const string ChartHeaderPart = "PART_ChartHeader";
    public const string YTitlePart = "PART_YTitle";
    public const string LegendPart = "PART_Legend";
    public const string PlotPart = "PART_Plot";
    public const string XTitlePart = "PART_XTitle";
    public const string TransportPart = "PART_Transport";
    public const string PlayButtonPart = "PART_PlayButton";
    public const string PlayGlyphPart = "PART_PlayGlyph";
    public const string PlayTextPart = "PART_PlayText";
    public const string ScrubberPart = "PART_Scrubber";
    public const string TablePart = "PART_Table";
    public const string AnnouncerPart = "PART_Announcer";

    /// <summary>Set while the head is being played through the months.</summary>
    public const string PlayingPseudoClass = ":playing";

    /// <summary>Set while the block is narrower than 540: paddings 16, radius from the panel scale.</summary>
    public const string NarrowPseudoClass = ":narrow";

    /// <summary>Set while the content is narrower than 480: a 300px chart, the transport in one column, no notes.</summary>
    public const string CompactPseudoClass = ":compact";

    /// <summary>Set while there is no series to show.</summary>
    public const string EmptyPseudoClass = ":empty";

    #region Properties

    /// <summary>The heading; null leaves it out.</summary>
    public static readonly StyledProperty<string?> TitleProperty =
        AvaloniaProperty.Register<ConnectedScatter, string?>(nameof(Title));

    /// <summary>The line under the heading; null writes one from the axis names and the first and last month.</summary>
    public static readonly StyledProperty<string?> DescriptionProperty =
        AvaloniaProperty.Register<ConnectedScatter, string?>(nameof(Description));

    /// <summary>The series as a list you own (followed when it raises collection changes); it wins over <see cref="Series"/>.</summary>
    public static readonly StyledProperty<IEnumerable?> SeriesSourceProperty =
        AvaloniaProperty.Register<ConnectedScatter, IEnumerable?>(nameof(SeriesSource));

    /// <summary>The key of the series shown (two-way); null shows the first one. Changing it morphs the points.</summary>
    public static readonly StyledProperty<string?> SelectedSeriesKeyProperty =
        AvaloniaProperty.Register<ConnectedScatter, string?>(nameof(SelectedSeriesKey), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>The horizontal axis; null derives everything from the data.</summary>
    public static readonly StyledProperty<ScatterAxis?> XAxisProperty =
        AvaloniaProperty.Register<ConnectedScatter, ScatterAxis?>(nameof(XAxis));

    /// <summary>The vertical axis; null derives everything from the data.</summary>
    public static readonly StyledProperty<ScatterAxis?> YAxisProperty =
        AvaloniaProperty.Register<ConnectedScatter, ScatterAxis?>(nameof(YAxis));

    /// <summary>The target line's slope (y = ratio · x); the side above it is tinted as healthy. NaN leaves out the line, the zone and the status. Default 3.</summary>
    public static readonly StyledProperty<double> TargetRatioProperty =
        AvaloniaProperty.Register<ConnectedScatter, double>(nameof(TargetRatio), 3);

    /// <summary>Formats a value for the readouts, the card, the table and the announcements; null gives whole dollars ("$1,234").</summary>
    public static readonly StyledProperty<Func<double, string>?> ValueFormatterProperty =
        AvaloniaProperty.Register<ConnectedScatter, Func<double, string>?>(nameof(ValueFormatter));

    /// <summary>Formats the ratio; null gives one decimal and an x ("4.8x").</summary>
    public static readonly StyledProperty<Func<double, string>?> RatioFormatterProperty =
        AvaloniaProperty.Register<ConnectedScatter, Func<double, string>?>(nameof(RatioFormatter));

    /// <summary>
    /// The month the head rests on (two-way): −1 (default) means the last month, or, until the first draw-in ends, nothing yet.
    /// Setting it moves the head there on the Smooth spring.
    /// </summary>
    public static readonly StyledProperty<int> SelectedIndexProperty =
        AvaloniaProperty.Register<ConnectedScatter, int>(nameof(SelectedIndex), -1, defaultBindingMode: BindingMode.TwoWay);

    /// <summary>Whether the first time the block is on screen the head runs from the first month to the last (default true). Reduced and Off motion show the end at once.</summary>
    public static readonly StyledProperty<bool> PlayOnFirstAppearProperty =
        AvaloniaProperty.Register<ConnectedScatter, bool>(nameof(PlayOnFirstAppear), true);

    /// <summary>How long the first run takes, with the in-out easing (default 1.35s).</summary>
    public static readonly StyledProperty<TimeSpan> DrawInDurationProperty =
        AvaloniaProperty.Register<ConnectedScatter, TimeSpan>(nameof(DrawInDuration), TimeSpan.FromMilliseconds(1350));

    /// <summary>How long playback stays on one month (default 240ms, constant speed).</summary>
    public static readonly StyledProperty<TimeSpan> PlaybackMonthDurationProperty =
        AvaloniaProperty.Register<ConnectedScatter, TimeSpan>(nameof(PlaybackMonthDuration), TimeSpan.FromMilliseconds(240));

    /// <summary>The name of the month readout cell.</summary>
    public static readonly StyledProperty<string> MonthNameProperty =
        AvaloniaProperty.Register<ConnectedScatter, string>(nameof(MonthName), "Month");

    /// <summary>The ratio's name in the readouts and the card; null writes "{Y name} to {X name}".</summary>
    public static readonly StyledProperty<string?> RatioNameProperty =
        AvaloniaProperty.Register<ConnectedScatter, string?>(nameof(RatioName));

    /// <summary>The automatic ratio name; {0} the vertical axis's name, {1} the horizontal one's.</summary>
    public static readonly StyledProperty<string> RatioNameFormatProperty =
        AvaloniaProperty.Register<ConnectedScatter, string>(nameof(RatioNameFormat), "{0} to {1}");

    /// <summary>The month readout's note; {0} the month's number, {1} how many months.</summary>
    public static readonly StyledProperty<string> MonthOfFormatProperty =
        AvaloniaProperty.Register<ConnectedScatter, string>(nameof(MonthOfFormat), "{0} of {1}");

    /// <summary>A value readout's note; {0} the signed change, {1} the first month's name.</summary>
    public static readonly StyledProperty<string> SinceFormatProperty =
        AvaloniaProperty.Register<ConnectedScatter, string>(nameof(SinceFormat), "{0} since {1}");

    /// <summary>A value readout's note while it equals the first month; {0} the first month's name.</summary>
    public static readonly StyledProperty<string> SameAsFormatProperty =
        AvaloniaProperty.Register<ConnectedScatter, string>(nameof(SameAsFormat), "Same as {0}");

    /// <summary>The ratio readout's note above the target; {0} the target ("3x").</summary>
    public static readonly StyledProperty<string> AboveTargetFormatProperty =
        AvaloniaProperty.Register<ConnectedScatter, string>(nameof(AboveTargetFormat), "Above {0} target");

    /// <summary>The ratio readout's note below the target; {0} the target.</summary>
    public static readonly StyledProperty<string> BelowTargetFormatProperty =
        AvaloniaProperty.Register<ConnectedScatter, string>(nameof(BelowTargetFormat), "Below {0} target");

    /// <summary>The target line's label; {0} the target ("3x"), {1} the horizontal axis's name.</summary>
    public static readonly StyledProperty<string> TargetLabelFormatProperty =
        AvaloniaProperty.Register<ConnectedScatter, string>(nameof(TargetLabelFormat), "{0} {1}");

    /// <summary>The legend text of the target line; {0} the target, {1} the horizontal axis's name, {2} the vertical one's.</summary>
    public static readonly StyledProperty<string> TargetLegendFormatProperty =
        AvaloniaProperty.Register<ConnectedScatter, string>(nameof(TargetLegendFormat), "{2} = {0} {1}");

    /// <summary>The label in the healthy zone's corner; {0} the target.</summary>
    public static readonly StyledProperty<string> ZoneLabelFormatProperty =
        AvaloniaProperty.Register<ConnectedScatter, string>(nameof(ZoneLabelFormat), "Healthy, above {0}");

    /// <summary>The legend text of the healthy zone.</summary>
    public static readonly StyledProperty<string> ZoneLegendTextProperty =
        AvaloniaProperty.Register<ConnectedScatter, string>(nameof(ZoneLegendText), "Healthy zone");

    /// <summary>The legend text of the path; {0} the series' name.</summary>
    public static readonly StyledProperty<string> SeriesLegendFormatProperty =
        AvaloniaProperty.Register<ConnectedScatter, string>(nameof(SeriesLegendFormat), "{0}, in time order");

    /// <summary>The automatic description; {0} the vertical axis, {1} the horizontal axis, {2} the first month, {3} the last month.</summary>
    public static readonly StyledProperty<string> DescriptionFormatProperty =
        AvaloniaProperty.Register<ConnectedScatter, string>(nameof(DescriptionFormat), "{0} against {1}, {2} to {3}.");

    /// <summary>The play button's text before the last month.</summary>
    public static readonly StyledProperty<string> PlayTextProperty =
        AvaloniaProperty.Register<ConnectedScatter, string>(nameof(PlayText), "Play");

    /// <summary>The play button's text while playing.</summary>
    public static readonly StyledProperty<string> PauseTextProperty =
        AvaloniaProperty.Register<ConnectedScatter, string>(nameof(PauseText), "Pause");

    /// <summary>The play button's text at the last month.</summary>
    public static readonly StyledProperty<string> ReplayTextProperty =
        AvaloniaProperty.Register<ConnectedScatter, string>(nameof(ReplayText), "Replay");

    /// <summary>The play button's screen reader name before the last month.</summary>
    public static readonly StyledProperty<string> PlayLabelProperty =
        AvaloniaProperty.Register<ConnectedScatter, string>(nameof(PlayLabel), "Play month by month");

    /// <summary>The play button's screen reader name while playing.</summary>
    public static readonly StyledProperty<string> PauseLabelProperty =
        AvaloniaProperty.Register<ConnectedScatter, string>(nameof(PauseLabel), "Pause");

    /// <summary>The play button's screen reader name at the last month.</summary>
    public static readonly StyledProperty<string> ReplayLabelProperty =
        AvaloniaProperty.Register<ConnectedScatter, string>(nameof(ReplayLabel), "Replay from the first month");

    /// <summary>The screen reader name of the scrubber.</summary>
    public static readonly StyledProperty<string> ScrubberLabelProperty =
        AvaloniaProperty.Register<ConnectedScatter, string>(nameof(ScrubberLabel), "Month");

    /// <summary>The screen reader name of the series switch.</summary>
    public static readonly StyledProperty<string> SeriesSwitchLabelProperty =
        AvaloniaProperty.Register<ConnectedScatter, string>(nameof(SeriesSwitchLabel), "Series");

    /// <summary>The screen reader table's caption; {0} the series, {1} the horizontal axis's title, {2} the vertical one's.</summary>
    public static readonly StyledProperty<string> TableCaptionFormatProperty =
        AvaloniaProperty.Register<ConnectedScatter, string>(nameof(TableCaptionFormat), "{0}: {1} and {2} by month");

    /// <summary>
    /// What is said (and what the scrubber's value text is) for a month; {0} the month, {1} / {2} the horizontal axis's name and
    /// value, {3} / {4} the vertical axis's, {5} the ratio, {6} the status ("above the 3x target").
    /// </summary>
    public static readonly StyledProperty<string> MonthAnnouncementFormatProperty =
        AvaloniaProperty.Register<ConnectedScatter, string>(nameof(MonthAnnouncementFormat), "{0}: {1} {2}, {3} {4}, {5}, {6}.");

    /// <summary>Said when another series is chosen; {0} the series, {1} the month's announcement.</summary>
    public static readonly StyledProperty<string> SeriesAnnouncementFormatProperty =
        AvaloniaProperty.Register<ConnectedScatter, string>(nameof(SeriesAnnouncementFormat), "{0}. {1}");

    /// <summary>The status words of the announcement; {0} the target ("above the 3x target").</summary>
    public static readonly StyledProperty<string> AboveAnnouncementFormatProperty =
        AvaloniaProperty.Register<ConnectedScatter, string>(nameof(AboveAnnouncementFormat), "above the {0} target");

    /// <summary>The status words of the announcement; {0} the target.</summary>
    public static readonly StyledProperty<string> BelowAnnouncementFormatProperty =
        AvaloniaProperty.Register<ConnectedScatter, string>(nameof(BelowAnnouncementFormat), "below the {0} target");

    /// <summary>Run when the head settles on a month, with the <see cref="ScatterMonthChangedEventArgs"/> as the parameter.</summary>
    public static readonly StyledProperty<ICommand?> MonthChangedCommandProperty =
        AvaloniaProperty.Register<ConnectedScatter, ICommand?>(nameof(MonthChangedCommand));

    /// <summary>The head's continuous month position (0 to months − 1): fractions while it moves.</summary>
    public static readonly DirectProperty<ConnectedScatter, double> PositionProperty =
        AvaloniaProperty.RegisterDirect<ConnectedScatter, double>(nameof(Position), o => o.Position);

    /// <summary>True while playback runs.</summary>
    public static readonly DirectProperty<ConnectedScatter, bool> IsPlayingProperty =
        AvaloniaProperty.RegisterDirect<ConnectedScatter, bool>(nameof(IsPlaying), o => o.IsPlaying);

    #endregion

    #region Events

    /// <summary>Raised when the head settles on a month (released, a key, a tap, the end of playback), never while it moves.</summary>
    public static readonly RoutedEvent<ScatterMonthChangedEventArgs> MonthChangedEvent =
        RoutedEvent.Register<ConnectedScatter, ScatterMonthChangedEventArgs>(nameof(MonthChanged), RoutingStrategies.Bubble);

    /// <summary>Raised when another series is chosen.</summary>
    public static readonly RoutedEvent<ScatterSeriesChangedEventArgs> SeriesChangedEvent =
        RoutedEvent.Register<ConnectedScatter, ScatterSeriesChangedEventArgs>(nameof(SeriesChanged), RoutingStrategies.Bubble);

    /// <inheritdoc cref="MonthChangedEvent"/>
    public event EventHandler<ScatterMonthChangedEventArgs>? MonthChanged
    {
        add => AddHandler(MonthChangedEvent, value);
        remove => RemoveHandler(MonthChangedEvent, value);
    }

    /// <inheritdoc cref="SeriesChangedEvent"/>
    public event EventHandler<ScatterSeriesChangedEventArgs>? SeriesChanged
    {
        add => AddHandler(SeriesChangedEvent, value);
        remove => RemoveHandler(SeriesChangedEvent, value);
    }

    #endregion

    #region Accessors

    /// <inheritdoc cref="TitleProperty"/>
    public string? Title { get => GetValue(TitleProperty); set => SetValue(TitleProperty, value); }

    /// <inheritdoc cref="DescriptionProperty"/>
    public string? Description { get => GetValue(DescriptionProperty); set => SetValue(DescriptionProperty, value); }

    /// <inheritdoc cref="SeriesSourceProperty"/>
    public IEnumerable? SeriesSource { get => GetValue(SeriesSourceProperty); set => SetValue(SeriesSourceProperty, value); }

    /// <summary>The series the switch offers, in order (ignored while <see cref="SeriesSource"/> is set).</summary>
    [Content]
    public AvaloniaList<ScatterSeries> Series { get; } = new();

    /// <inheritdoc cref="SelectedSeriesKeyProperty"/>
    public string? SelectedSeriesKey { get => GetValue(SelectedSeriesKeyProperty); set => SetValue(SelectedSeriesKeyProperty, value); }

    /// <inheritdoc cref="XAxisProperty"/>
    public ScatterAxis? XAxis { get => GetValue(XAxisProperty); set => SetValue(XAxisProperty, value); }

    /// <inheritdoc cref="YAxisProperty"/>
    public ScatterAxis? YAxis { get => GetValue(YAxisProperty); set => SetValue(YAxisProperty, value); }

    /// <inheritdoc cref="TargetRatioProperty"/>
    public double TargetRatio { get => GetValue(TargetRatioProperty); set => SetValue(TargetRatioProperty, value); }

    /// <inheritdoc cref="ValueFormatterProperty"/>
    public Func<double, string>? ValueFormatter { get => GetValue(ValueFormatterProperty); set => SetValue(ValueFormatterProperty, value); }

    /// <inheritdoc cref="RatioFormatterProperty"/>
    public Func<double, string>? RatioFormatter { get => GetValue(RatioFormatterProperty); set => SetValue(RatioFormatterProperty, value); }

    /// <inheritdoc cref="SelectedIndexProperty"/>
    public int SelectedIndex { get => GetValue(SelectedIndexProperty); set => SetValue(SelectedIndexProperty, value); }

    /// <inheritdoc cref="PlayOnFirstAppearProperty"/>
    public bool PlayOnFirstAppear { get => GetValue(PlayOnFirstAppearProperty); set => SetValue(PlayOnFirstAppearProperty, value); }

    /// <inheritdoc cref="DrawInDurationProperty"/>
    public TimeSpan DrawInDuration { get => GetValue(DrawInDurationProperty); set => SetValue(DrawInDurationProperty, value); }

    /// <inheritdoc cref="PlaybackMonthDurationProperty"/>
    public TimeSpan PlaybackMonthDuration { get => GetValue(PlaybackMonthDurationProperty); set => SetValue(PlaybackMonthDurationProperty, value); }

    /// <inheritdoc cref="MonthNameProperty"/>
    public string MonthName { get => GetValue(MonthNameProperty); set => SetValue(MonthNameProperty, value); }

    /// <inheritdoc cref="RatioNameProperty"/>
    public string? RatioName { get => GetValue(RatioNameProperty); set => SetValue(RatioNameProperty, value); }

    /// <inheritdoc cref="RatioNameFormatProperty"/>
    public string RatioNameFormat { get => GetValue(RatioNameFormatProperty); set => SetValue(RatioNameFormatProperty, value); }

    /// <inheritdoc cref="MonthOfFormatProperty"/>
    public string MonthOfFormat { get => GetValue(MonthOfFormatProperty); set => SetValue(MonthOfFormatProperty, value); }

    /// <inheritdoc cref="SinceFormatProperty"/>
    public string SinceFormat { get => GetValue(SinceFormatProperty); set => SetValue(SinceFormatProperty, value); }

    /// <inheritdoc cref="SameAsFormatProperty"/>
    public string SameAsFormat { get => GetValue(SameAsFormatProperty); set => SetValue(SameAsFormatProperty, value); }

    /// <inheritdoc cref="AboveTargetFormatProperty"/>
    public string AboveTargetFormat { get => GetValue(AboveTargetFormatProperty); set => SetValue(AboveTargetFormatProperty, value); }

    /// <inheritdoc cref="BelowTargetFormatProperty"/>
    public string BelowTargetFormat { get => GetValue(BelowTargetFormatProperty); set => SetValue(BelowTargetFormatProperty, value); }

    /// <inheritdoc cref="TargetLabelFormatProperty"/>
    public string TargetLabelFormat { get => GetValue(TargetLabelFormatProperty); set => SetValue(TargetLabelFormatProperty, value); }

    /// <inheritdoc cref="TargetLegendFormatProperty"/>
    public string TargetLegendFormat { get => GetValue(TargetLegendFormatProperty); set => SetValue(TargetLegendFormatProperty, value); }

    /// <inheritdoc cref="ZoneLabelFormatProperty"/>
    public string ZoneLabelFormat { get => GetValue(ZoneLabelFormatProperty); set => SetValue(ZoneLabelFormatProperty, value); }

    /// <inheritdoc cref="ZoneLegendTextProperty"/>
    public string ZoneLegendText { get => GetValue(ZoneLegendTextProperty); set => SetValue(ZoneLegendTextProperty, value); }

    /// <inheritdoc cref="SeriesLegendFormatProperty"/>
    public string SeriesLegendFormat { get => GetValue(SeriesLegendFormatProperty); set => SetValue(SeriesLegendFormatProperty, value); }

    /// <inheritdoc cref="DescriptionFormatProperty"/>
    public string DescriptionFormat { get => GetValue(DescriptionFormatProperty); set => SetValue(DescriptionFormatProperty, value); }

    /// <inheritdoc cref="PlayTextProperty"/>
    public string PlayText { get => GetValue(PlayTextProperty); set => SetValue(PlayTextProperty, value); }

    /// <inheritdoc cref="PauseTextProperty"/>
    public string PauseText { get => GetValue(PauseTextProperty); set => SetValue(PauseTextProperty, value); }

    /// <inheritdoc cref="ReplayTextProperty"/>
    public string ReplayText { get => GetValue(ReplayTextProperty); set => SetValue(ReplayTextProperty, value); }

    /// <inheritdoc cref="PlayLabelProperty"/>
    public string PlayLabel { get => GetValue(PlayLabelProperty); set => SetValue(PlayLabelProperty, value); }

    /// <inheritdoc cref="PauseLabelProperty"/>
    public string PauseLabel { get => GetValue(PauseLabelProperty); set => SetValue(PauseLabelProperty, value); }

    /// <inheritdoc cref="ReplayLabelProperty"/>
    public string ReplayLabel { get => GetValue(ReplayLabelProperty); set => SetValue(ReplayLabelProperty, value); }

    /// <inheritdoc cref="ScrubberLabelProperty"/>
    public string ScrubberLabel { get => GetValue(ScrubberLabelProperty); set => SetValue(ScrubberLabelProperty, value); }

    /// <inheritdoc cref="SeriesSwitchLabelProperty"/>
    public string SeriesSwitchLabel { get => GetValue(SeriesSwitchLabelProperty); set => SetValue(SeriesSwitchLabelProperty, value); }

    /// <inheritdoc cref="TableCaptionFormatProperty"/>
    public string TableCaptionFormat { get => GetValue(TableCaptionFormatProperty); set => SetValue(TableCaptionFormatProperty, value); }

    /// <inheritdoc cref="MonthAnnouncementFormatProperty"/>
    public string MonthAnnouncementFormat { get => GetValue(MonthAnnouncementFormatProperty); set => SetValue(MonthAnnouncementFormatProperty, value); }

    /// <inheritdoc cref="SeriesAnnouncementFormatProperty"/>
    public string SeriesAnnouncementFormat { get => GetValue(SeriesAnnouncementFormatProperty); set => SetValue(SeriesAnnouncementFormatProperty, value); }

    /// <inheritdoc cref="AboveAnnouncementFormatProperty"/>
    public string AboveAnnouncementFormat { get => GetValue(AboveAnnouncementFormatProperty); set => SetValue(AboveAnnouncementFormatProperty, value); }

    /// <inheritdoc cref="BelowAnnouncementFormatProperty"/>
    public string BelowAnnouncementFormat { get => GetValue(BelowAnnouncementFormatProperty); set => SetValue(BelowAnnouncementFormatProperty, value); }

    /// <inheritdoc cref="MonthChangedCommandProperty"/>
    public ICommand? MonthChangedCommand { get => GetValue(MonthChangedCommandProperty); set => SetValue(MonthChangedCommandProperty, value); }

    /// <inheritdoc cref="PositionProperty"/>
    public double Position { get => _position; private set => SetAndRaise(PositionProperty, ref _position, value); }

    /// <inheritdoc cref="IsPlayingProperty"/>
    public bool IsPlaying { get => _isPlaying; private set => SetAndRaise(IsPlayingProperty, ref _isPlaying, value); }

    #endregion

    private double _position;
    private bool _isPlaying;
}
