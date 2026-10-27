using System.Collections;
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
/// A line chart you play like a recording. The line is drawn from left to right as the clock runs, with the current
/// value next to its leading end and the time on the axis below. Events you mark on the clock show up as the line
/// reaches them: a dot lands on the line, a small card names the event above the chart, and its row in the list beside
/// the chart is highlighted, later adding what happened next. Around the chart sit a play button, a position slider,
/// a speed switch, and figures for the current time, value, comparison and best value so far.
/// <code>
/// &lt;arc:LineReplay Title="Release day" SeriesLabel="Release day" BaselineLabel="Typical Wednesday" ValueLabel="Active" /&gt;
/// </code>
/// </summary>
/// <remarks>
/// <para>
/// Data: <see cref="Points"/> on one clock in minutes from the start (not necessarily evenly spaced), and
/// <see cref="Moments"/> on the same clock. Leave <see cref="LineReplayPoint.Baseline"/> out of every point to hide the
/// comparison, its legend entry and the ratio. The value axis is fixed to the whole series.
/// </para>
/// <para>
/// Keys on the scrubber: Space, Enter or K plays and pauses (at the end it replays from the start); ←/→ step one point
/// (with Shift an hour), Page up / Page down an hour, Home / End jump to the start or the end. Enter or Space on a moment
/// moves the playhead to it.
/// </para>
/// </remarks>
[TemplatePart(PlayButtonPart, typeof(Button))]
[TemplatePart(PlayGlyphPart, typeof(ArcGlyph))]
[TemplatePart(PlayTextPart, typeof(TextBlock))]
[TemplatePart(StagePart, typeof(Panel))]
[TemplatePart(ScrubberPart, typeof(Panel))]
[TemplatePart(SpeedSwitchPart, typeof(SegmentedControl))]
[TemplatePart(ReadoutPart, typeof(Panel))]
[TemplatePart(MomentsPart, typeof(Panel))]
[TemplatePart(LiveRegionPart, typeof(Control))]
[PseudoClasses(StackedPseudoClass, NarrowPseudoClass, PlayingPseudoClass, NoBaselinePseudoClass, NoMomentsPseudoClass)]
public partial class LineReplay : TemplatedControl
{
    public const string PlayButtonPart = "PART_PlayButton";
    public const string PlayGlyphPart = "PART_PlayGlyph";
    public const string PlayTextPart = "PART_PlayText";
    public const string StagePart = "PART_Stage";
    public const string ScrubberPart = "PART_Scrubber";
    public const string SpeedSwitchPart = "PART_SpeedSwitch";
    public const string ReadoutPart = "PART_Readout";
    public const string MomentsPart = "PART_Moments";
    public const string LiveRegionPart = "PART_LiveRegion";

    /// <summary>Set while the block is narrower than 1000: the moments move under the chart.</summary>
    public const string StackedPseudoClass = ":stacked";

    /// <summary>Set while the block is narrower than 560: one column of moments, a 300px chart, the scrubber on its own row.</summary>
    public const string NarrowPseudoClass = ":narrow";

    /// <summary>Set while the replay plays.</summary>
    public const string PlayingPseudoClass = ":playing";

    /// <summary>Set while no point has a baseline: the comparison, its legend entry and the ratio are hidden.</summary>
    public const string NoBaselinePseudoClass = ":no-baseline";

    /// <summary>Set while there are no moments: the moments column is hidden.</summary>
    public const string NoMomentsPseudoClass = ":no-moments";

    #region Properties

    /// <summary>The data to play: one entry per sample with its clock time in minutes, its value and, if you have one, a typical value to compare with.</summary>
    public static readonly StyledProperty<IReadOnlyList<LineReplayPoint>?> PointsProperty =
        AvaloniaProperty.Register<LineReplay, IReadOnlyList<LineReplayPoint>?>(nameof(Points));

    /// <summary>The key moments as a list to follow (observed when it raises collection changes); it wins over <see cref="Moments"/>.</summary>
    public static readonly StyledProperty<IEnumerable?> MomentsSourceProperty =
        AvaloniaProperty.Register<LineReplay, IEnumerable?>(nameof(MomentsSource));

    /// <summary>The heading of the block; null hides it.</summary>
    public static readonly StyledProperty<string?> TitleProperty =
        AvaloniaProperty.Register<LineReplay, string?>(nameof(Title));

    /// <summary>A sentence under the heading saying what the line measures and over which day; null leaves it out.</summary>
    public static readonly StyledProperty<string?> DescriptionProperty =
        AvaloniaProperty.Register<LineReplay, string?>(nameof(Description));

    /// <summary>What the legend calls the line being played.</summary>
    public static readonly StyledProperty<string> SeriesLabelProperty =
        AvaloniaProperty.Register<LineReplay, string>(nameof(SeriesLabel), "Today");

    /// <summary>What the legend calls the dashed comparison; the ratio's label reuses it ("Versus typical day").</summary>
    public static readonly StyledProperty<string> BaselineLabelProperty =
        AvaloniaProperty.Register<LineReplay, string>(nameof(BaselineLabel), "Typical day");

    /// <summary>The name of the measured quantity ("Active"), above the current value and in the slider's spoken value.</summary>
    public static readonly StyledProperty<string> ValueLabelProperty =
        AvaloniaProperty.Register<LineReplay, string>(nameof(ValueLabel), "Value");

    /// <summary>Writes a clock time (minutes) wherever one is shown or spoken; without one, times read like "9:40 am".</summary>
    public static readonly StyledProperty<Func<double, string>?> TimeFormatterProperty =
        AvaloniaProperty.Register<LineReplay, Func<double, string>?>(nameof(TimeFormatter));

    /// <summary>Writes the hour labels under the chart; without one, they read like "6 am".</summary>
    public static readonly StyledProperty<Func<double, string>?> TickFormatterProperty =
        AvaloniaProperty.Register<LineReplay, Func<double, string>?>(nameof(TickFormatter));

    /// <summary>Writes a value wherever one is shown or spoken; without one, values are whole numbers like "2,316".</summary>
    public static readonly StyledProperty<Func<double, string>?> ValueFormatterProperty =
        AvaloniaProperty.Register<LineReplay, Func<double, string>?>(nameof(ValueFormatter));

    /// <summary>The time from the first point to the last at normal speed (16 seconds by default).</summary>
    public static readonly StyledProperty<TimeSpan> DurationProperty =
        AvaloniaProperty.Register<LineReplay, TimeSpan>(nameof(Duration), TimeSpan.FromSeconds(16));

    /// <summary>The playback speed (two-way): 0.5, 1, 2 or 4.</summary>
    public static readonly StyledProperty<double> SpeedProperty =
        AvaloniaProperty.Register<LineReplay, double>(nameof(Speed), 1, defaultBindingMode: BindingMode.TwoWay,
            coerce: (_, v) => double.IsFinite(v) && v > 0 ? v : 1);

    /// <summary>Whether the chart first appears fully drawn (<see cref="LineReplayPosition.End"/>, the default) or empty (<see cref="LineReplayPosition.Start"/>).</summary>
    public static readonly StyledProperty<LineReplayPosition> DefaultPositionProperty =
        AvaloniaProperty.Register<LineReplay, LineReplayPosition>(nameof(DefaultPosition), LineReplayPosition.End);

    /// <summary>Starts a replay from the beginning once, as soon as 40% of the block has scrolled into view; nothing happens when motion is reduced.</summary>
    public static readonly StyledProperty<bool> AutoPlayProperty =
        AvaloniaProperty.Register<LineReplay, bool>(nameof(AutoPlay));

    /// <summary>The play button's text while paused partway.</summary>
    public static readonly StyledProperty<string> PlayTextProperty =
        AvaloniaProperty.Register<LineReplay, string>(nameof(PlayText), "Play");

    /// <summary>The play button's text while playing.</summary>
    public static readonly StyledProperty<string> PauseTextProperty =
        AvaloniaProperty.Register<LineReplay, string>(nameof(PauseText), "Pause");

    /// <summary>The play button's text at the end.</summary>
    public static readonly StyledProperty<string> ReplayTextProperty =
        AvaloniaProperty.Register<LineReplay, string>(nameof(ReplayText), "Replay");

    /// <summary>The play button's screen reader name while paused partway.</summary>
    public static readonly StyledProperty<string> PlayLabelProperty =
        AvaloniaProperty.Register<LineReplay, string>(nameof(PlayLabel), "Play replay");

    /// <summary>The play button's screen reader name while playing.</summary>
    public static readonly StyledProperty<string> PauseLabelProperty =
        AvaloniaProperty.Register<LineReplay, string>(nameof(PauseLabel), "Pause replay");

    /// <summary>The play button's screen reader name at the end.</summary>
    public static readonly StyledProperty<string> ReplayLabelProperty =
        AvaloniaProperty.Register<LineReplay, string>(nameof(ReplayLabel), "Replay from the start");

    /// <summary>The readout's time label.</summary>
    public static readonly StyledProperty<string> TimeTextProperty =
        AvaloniaProperty.Register<LineReplay, string>(nameof(TimeText), "Time");

    /// <summary>The readout's ratio label; {0} is <see cref="BaselineLabel"/> with a lower-case first letter.</summary>
    public static readonly StyledProperty<string> VersusFormatProperty =
        AvaloniaProperty.Register<LineReplay, string>(nameof(VersusFormat), "Versus {0}");

    /// <summary>The readout's peak label.</summary>
    public static readonly StyledProperty<string> PeakTextProperty =
        AvaloniaProperty.Register<LineReplay, string>(nameof(PeakText), "Peak so far");

    /// <summary>The peak value and when; {0} value, {1} time.</summary>
    public static readonly StyledProperty<string> PeakFormatProperty =
        AvaloniaProperty.Register<LineReplay, string>(nameof(PeakFormat), "{0} at {1}");

    /// <summary>The heading of the moments column.</summary>
    public static readonly StyledProperty<string> MomentsTitleProperty =
        AvaloniaProperty.Register<LineReplay, string>(nameof(MomentsTitle), "Key moments");

    /// <summary>A moment's effect, the rise over the next hour; {0} is the formatted rise.</summary>
    public static readonly StyledProperty<string> RiseFormatProperty =
        AvaloniaProperty.Register<LineReplay, string>(nameof(RiseFormat), "+{0} in the next hour");

    /// <summary>An incident's effect, the dip over 20 minutes; {0} is the signed percentage.</summary>
    public static readonly StyledProperty<string> DipFormatProperty =
        AvaloniaProperty.Register<LineReplay, string>(nameof(DipFormat), "{0} in 20 minutes");

    /// <summary>The scrubber's screen reader name.</summary>
    public static readonly StyledProperty<string> ScrubberLabelProperty =
        AvaloniaProperty.Register<LineReplay, string>(nameof(ScrubberLabel), "Replay position");

    /// <summary>The speed control's screen reader name.</summary>
    public static readonly StyledProperty<string> SpeedLabelProperty =
        AvaloniaProperty.Register<LineReplay, string>(nameof(SpeedLabel), "Playback speed");

    /// <summary>Announced when playback passes a moment; {0} time, {1} title.</summary>
    public static readonly StyledProperty<string> MomentAnnouncementFormatProperty =
        AvaloniaProperty.Register<LineReplay, string>(nameof(MomentAnnouncementFormat), "{0}. {1}.");

    /// <summary>Announced when playback is paused; {0} is the time.</summary>
    public static readonly StyledProperty<string> PausedAnnouncementFormatProperty =
        AvaloniaProperty.Register<LineReplay, string>(nameof(PausedAnnouncementFormat), "Paused at {0}.");

    /// <summary>Announced when the replay reaches the end; {0} peak value, {1} its time.</summary>
    public static readonly StyledProperty<string> FinishedAnnouncementFormatProperty =
        AvaloniaProperty.Register<LineReplay, string>(nameof(FinishedAnnouncementFormat), "Replay finished. Peak {0} at {1}.");

    /// <summary>Where the playhead is, in minutes on the replay clock.</summary>
    public static readonly DirectProperty<LineReplay, double> PositionProperty =
        AvaloniaProperty.RegisterDirect<LineReplay, double>(nameof(Position), o => o.Position);

    /// <summary>Whether the replay is playing.</summary>
    public static readonly DirectProperty<LineReplay, bool> IsPlayingProperty =
        AvaloniaProperty.RegisterDirect<LineReplay, bool>(nameof(IsPlaying), o => o.IsPlaying);

    #endregion

    #region Events

    /// <summary>Occurs as a playing replay crosses one of the moments; dragging the slider or jumping does not raise it.</summary>
    public static readonly RoutedEvent<LineReplayMomentEventArgs> MomentReachedEvent =
        RoutedEvent.Register<LineReplay, LineReplayMomentEventArgs>(nameof(MomentReached), RoutingStrategies.Bubble);

    /// <summary>Occurs as a playing replay crosses one of the moments; dragging the slider or jumping does not raise it.</summary>
    public event EventHandler<LineReplayMomentEventArgs>? MomentReached
    {
        add => AddHandler(MomentReachedEvent, value);
        remove => RemoveHandler(MomentReachedEvent, value);
    }

    #endregion

    private double _position;
    private bool _isPlaying;

    /// <summary>The key moments, when no <see cref="MomentsSource"/> is set.</summary>
    [Content]
    public AvaloniaList<LineReplayMoment> Moments { get; } = new();

    /// <inheritdoc cref="PointsProperty"/>
    public IReadOnlyList<LineReplayPoint>? Points { get => GetValue(PointsProperty); set => SetValue(PointsProperty, value); }

    /// <inheritdoc cref="MomentsSourceProperty"/>
    public IEnumerable? MomentsSource { get => GetValue(MomentsSourceProperty); set => SetValue(MomentsSourceProperty, value); }

    /// <inheritdoc cref="TitleProperty"/>
    public string? Title { get => GetValue(TitleProperty); set => SetValue(TitleProperty, value); }

    /// <inheritdoc cref="DescriptionProperty"/>
    public string? Description { get => GetValue(DescriptionProperty); set => SetValue(DescriptionProperty, value); }

    /// <inheritdoc cref="SeriesLabelProperty"/>
    public string SeriesLabel { get => GetValue(SeriesLabelProperty); set => SetValue(SeriesLabelProperty, value); }

    /// <inheritdoc cref="BaselineLabelProperty"/>
    public string BaselineLabel { get => GetValue(BaselineLabelProperty); set => SetValue(BaselineLabelProperty, value); }

    /// <inheritdoc cref="ValueLabelProperty"/>
    public string ValueLabel { get => GetValue(ValueLabelProperty); set => SetValue(ValueLabelProperty, value); }

    /// <inheritdoc cref="TimeFormatterProperty"/>
    public Func<double, string>? TimeFormatter { get => GetValue(TimeFormatterProperty); set => SetValue(TimeFormatterProperty, value); }

    /// <inheritdoc cref="TickFormatterProperty"/>
    public Func<double, string>? TickFormatter { get => GetValue(TickFormatterProperty); set => SetValue(TickFormatterProperty, value); }

    /// <inheritdoc cref="ValueFormatterProperty"/>
    public Func<double, string>? ValueFormatter { get => GetValue(ValueFormatterProperty); set => SetValue(ValueFormatterProperty, value); }

    /// <inheritdoc cref="DurationProperty"/>
    public TimeSpan Duration { get => GetValue(DurationProperty); set => SetValue(DurationProperty, value); }

    /// <inheritdoc cref="SpeedProperty"/>
    public double Speed { get => GetValue(SpeedProperty); set => SetValue(SpeedProperty, value); }

    /// <inheritdoc cref="DefaultPositionProperty"/>
    public LineReplayPosition DefaultPosition { get => GetValue(DefaultPositionProperty); set => SetValue(DefaultPositionProperty, value); }

    /// <inheritdoc cref="AutoPlayProperty"/>
    public bool AutoPlay { get => GetValue(AutoPlayProperty); set => SetValue(AutoPlayProperty, value); }

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

    /// <inheritdoc cref="TimeTextProperty"/>
    public string TimeText { get => GetValue(TimeTextProperty); set => SetValue(TimeTextProperty, value); }

    /// <inheritdoc cref="VersusFormatProperty"/>
    public string VersusFormat { get => GetValue(VersusFormatProperty); set => SetValue(VersusFormatProperty, value); }

    /// <inheritdoc cref="PeakTextProperty"/>
    public string PeakText { get => GetValue(PeakTextProperty); set => SetValue(PeakTextProperty, value); }

    /// <inheritdoc cref="PeakFormatProperty"/>
    public string PeakFormat { get => GetValue(PeakFormatProperty); set => SetValue(PeakFormatProperty, value); }

    /// <inheritdoc cref="MomentsTitleProperty"/>
    public string MomentsTitle { get => GetValue(MomentsTitleProperty); set => SetValue(MomentsTitleProperty, value); }

    /// <inheritdoc cref="RiseFormatProperty"/>
    public string RiseFormat { get => GetValue(RiseFormatProperty); set => SetValue(RiseFormatProperty, value); }

    /// <inheritdoc cref="DipFormatProperty"/>
    public string DipFormat { get => GetValue(DipFormatProperty); set => SetValue(DipFormatProperty, value); }

    /// <inheritdoc cref="ScrubberLabelProperty"/>
    public string ScrubberLabel { get => GetValue(ScrubberLabelProperty); set => SetValue(ScrubberLabelProperty, value); }

    /// <inheritdoc cref="SpeedLabelProperty"/>
    public string SpeedLabel { get => GetValue(SpeedLabelProperty); set => SetValue(SpeedLabelProperty, value); }

    /// <inheritdoc cref="MomentAnnouncementFormatProperty"/>
    public string MomentAnnouncementFormat
    {
        get => GetValue(MomentAnnouncementFormatProperty);
        set => SetValue(MomentAnnouncementFormatProperty, value);
    }

    /// <inheritdoc cref="PausedAnnouncementFormatProperty"/>
    public string PausedAnnouncementFormat
    {
        get => GetValue(PausedAnnouncementFormatProperty);
        set => SetValue(PausedAnnouncementFormatProperty, value);
    }

    /// <inheritdoc cref="FinishedAnnouncementFormatProperty"/>
    public string FinishedAnnouncementFormat
    {
        get => GetValue(FinishedAnnouncementFormatProperty);
        set => SetValue(FinishedAnnouncementFormatProperty, value);
    }

    /// <inheritdoc cref="PositionProperty"/>
    public double Position { get => _position; private set => SetAndRaise(PositionProperty, ref _position, value); }

    /// <inheritdoc cref="IsPlayingProperty"/>
    public bool IsPlaying { get => _isPlaying; private set => SetAndRaise(IsPlayingProperty, ref _isPlaying, value); }
}
