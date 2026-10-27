using System.Collections;
using System.Collections.Specialized;
using System.Globalization;
using System.Windows.Input;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.VisualTree;
using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;

namespace TKCode.Arc;

public partial class ConnectedScatter
{
    internal static readonly StyledProperty<double> NarrowWidthProperty =
        AvaloniaProperty.Register<ConnectedScatter, double>(nameof(NarrowWidth), 540);

    internal double NarrowWidth { get => GetValue(NarrowWidthProperty); set => SetValue(NarrowWidthProperty, value); }

    private enum Arrival
    {
        None,
        DrawIn,
        Playback,
        Glide
    }

    private enum PlayState
    {
        Play,
        Pause,
        Replay
    }

    private ConnectedScatterSplitPanel? _header;
    private SegmentedControl? _seriesSwitch;
    private ConnectedScatterReadoutStrip? _readouts;
    private ConnectedScatterSplitPanel? _chartHeader;
    private TextBlock? _description;
    private TextBlock? _yTitle;
    private ConnectedScatterLegend? _legend;
    private ConnectedScatterPlot? _plot;
    private TextBlock? _xTitle;
    private Button? _playButton;
    private ArcGlyph? _playGlyph;
    private TextBlock? _playText;
    private ConnectedScatterScrubber? _scrubber;
    private ConnectedScatterDataTable? _table;
    private AnalyticsAnnouncer? _announcer;

    private readonly List<ScatterSeries> _series = new();
    private readonly ConnectedScatterTimeline _timeline = new();
    private readonly LiveLoop _loop;
    private INotifyCollectionChanged? _observedSource;
    private ScatterAxis? _observedX;
    private ScatterAxis? _observedY;
    private ScatterSeries? _shown;
    private ViewportVisibility? _visibility;
    private bool _seriesDirty = true;
    private bool _syncing;
    private bool _positioned;
    private bool _awaitingAppear;
    private bool _drawIn;
    private Arrival _arrival;
    private double _stepClock;
    private double _pointerU;
    private int _settled = -1;
    private PlayState? _buttonState;
    private ScatterReadoutState _lastReadout = ScatterReadoutState.Empty;
    private string _lastReadoutKey = string.Empty;
    private int _count;

    // Cached numbers of the shown series (target values; the plot animates the displayed ones).
    private string[] _labels = [];
    private double[] _x = [];
    private double[] _y = [];
    private ScatterPlotOptions _options = new();

    static ConnectedScatter()
    {
        FocusableProperty.OverrideDefaultValue<ConnectedScatter>(false);
    }

    /// <summary>Creates the block.</summary>
    public ConnectedScatter()
    {
        Series.CollectionChanged += (_, _) => OnSeriesChanged();
        _loop = new LiveLoop(this, Advance) { MinimumLevel = ArcMotionLevel.Off };
    }

    /// <summary>The chart (tests).</summary>
    internal ConnectedScatterPlot? Plot => _plot;

    /// <summary>The scrubber (tests).</summary>
    internal ConnectedScatterScrubber? Scrubber => _scrubber;

    /// <summary>The readout strip (tests).</summary>
    internal ConnectedScatterReadoutStrip? Readouts => _readouts;

    /// <summary>The legend (tests).</summary>
    internal ConnectedScatterLegend? Legend => _legend;

    /// <summary>The live region (tests).</summary>
    internal AnalyticsAnnouncer? Announcer => _announcer;

    /// <summary>The screen reader table (tests).</summary>
    internal ConnectedScatterDataTable? Table => _table;

    /// <summary>The play button (tests).</summary>
    internal Button? PlayButton => _playButton;

    /// <summary>The play button's text (tests).</summary>
    internal TextBlock? PlayButtonText => _playText;

    /// <summary>The head's motion model (tests).</summary>
    internal ConnectedScatterTimeline Timeline => _timeline;

    /// <summary>True while the first run is on its way (tests).</summary>
    internal bool IsDrawingIn => _drawIn;

    /// <summary>True until the block has been visible and the first run has started (tests).</summary>
    internal bool IsAwaitingAppear => _awaitingAppear;

    /// <summary>The series in effect (<see cref="SeriesSource"/> or <see cref="Series"/>).</summary>
    public IReadOnlyList<ScatterSeries> EffectiveSeries
    {
        get
        {
            EnsureSeries();
            return _series;
        }
    }

    /// <summary>The series shown now.</summary>
    public ScatterSeries? ShownSeries
    {
        get
        {
            EnsureSeries();
            return _shown;
        }
    }

    /// <summary>The month the head rests on, or −1 before the first run has finished.</summary>
    public int SettledIndex => _settled;

    /// <summary>Selects the series whose <see cref="ScatterSeries.EffectiveKey"/> is <paramref name="key"/>; false when none matches.</summary>
    public bool SelectSeries(string key)
    {
        var match = EffectiveSeries.FirstOrDefault(s => s.EffectiveKey == key);
        if (match is null)
        {
            return false;
        }
        SelectedSeriesKey = match.EffectiveKey;
        return true;
    }

    /// <summary>Moves the head to month <paramref name="index"/> (clamped): on the Smooth spring, or at once without animation or at Reduced / Off motion.</summary>
    public void GoToMonth(int index, bool animate = true)
    {
        if (_count == 0)
        {
            return;
        }
        CancelDrawIn();
        StopPlayback(announce: false);
        MoveHeadTo(Math.Clamp(index, 0, _count - 1), animate);
    }

    /// <summary>Starts playback from the head (from the first month when it is at the last one).</summary>
    public void Play()
    {
        if (_count < 2 || IsPlaying)
        {
            return;
        }
        CancelDrawIn();
        if (_timeline.U >= _count - 1 - 1e-6)
        {
            _timeline.Jump(0);
            PushPosition();
        }
        IsPlaying = true;
        PseudoClasses.Set(PlayingPseudoClass, true);
        _plot?.SetEmphasis(true);
        _stepClock = 0;
        _arrival = Arrival.Playback;
        _timeline.StartLinear(_count - 1, Math.Max(0.001, PlaybackMonthDuration.TotalSeconds));
        UpdatePlayButton();
        _loop.Start();
    }

    /// <summary>Pauses playback where the head is (between two months when it is).</summary>
    public void Pause() => StopPlayback(announce: true);

    /// <summary>Plays again from the first month.</summary>
    public void Replay()
    {
        if (_count < 2)
        {
            return;
        }
        CancelDrawIn();
        StopPlayback(announce: false);
        _timeline.Jump(0);
        PushPosition();
        Play();
    }

    /// <summary>Runs the first-appearance draw-in again: the head goes back to the first month and runs to the last (Full motion; otherwise it jumps to the end).</summary>
    public void ReplayDrawIn()
    {
        if (_count < 2)
        {
            return;
        }
        StopPlayback(announce: false);
        _plot?.SetReveal(false, false);
        if (!ArcMotion.IsFull(this))
        {
            _plot?.SetReveal(true, false);
            MoveHeadTo(_count - 1, animate: false);
            return;
        }
        _awaitingAppear = false;
        _timeline.Jump(0);
        PushPosition();
        StartDrawIn();
    }

    /// <summary>A command that toggles playback like the button does: pause while playing, replay at the last month, otherwise play.</summary>
    public ICommand PlayPauseCommand => _playPauseCommand ??= new ScatterPlayPauseCommand(this);

    private ICommand? _playPauseCommand;

    internal void TogglePlayback()
    {
        if (IsPlaying)
        {
            Pause();
        }
        else if (_count > 1 && _timeline.U >= _count - 1 - 1e-6 && !_drawIn)
        {
            Replay();
        }
        else
        {
            Play();
        }
    }

    /// <summary>Moves the keyboard focus to the scrubber.</summary>
    public void FocusScrubber() => _scrubber?.Focus(NavigationMethod.Tab);

    #region Template

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        if (_seriesSwitch is not null)
        {
            _seriesSwitch.SelectionChanged -= OnSwitchChanged;
        }
        if (_playButton is not null)
        {
            _playButton.Click -= OnPlayClick;
        }
        if (_plot is not null)
        {
            _plot.PointInvoked -= OnPointInvoked;
            _plot.DisplayChanged -= OnDisplayChanged;
        }
        if (_scrubber is not null)
        {
            _scrubber.ScrubStarted -= OnScrubStarted;
            _scrubber.ScrubMoved -= OnScrubMoved;
            _scrubber.ScrubEnded -= OnScrubEnded;
        }

        _header = e.NameScope.Find<ConnectedScatterSplitPanel>(HeaderPart);
        _seriesSwitch = e.NameScope.Find<SegmentedControl>(SeriesSwitchPart);
        _readouts = e.NameScope.Find<ConnectedScatterReadoutStrip>(ReadoutsPart);
        _chartHeader = e.NameScope.Find<ConnectedScatterSplitPanel>(ChartHeaderPart);
        _description = e.NameScope.Find<TextBlock>(DescriptionPart);
        _yTitle = e.NameScope.Find<TextBlock>(YTitlePart);
        _legend = e.NameScope.Find<ConnectedScatterLegend>(LegendPart);
        _plot = e.NameScope.Find<ConnectedScatterPlot>(PlotPart);
        _xTitle = e.NameScope.Find<TextBlock>(XTitlePart);
        _playButton = e.NameScope.Find<Button>(PlayButtonPart);
        _playGlyph = e.NameScope.Find<ArcGlyph>(PlayGlyphPart);
        _playText = e.NameScope.Find<TextBlock>(PlayTextPart);
        _scrubber = e.NameScope.Find<ConnectedScatterScrubber>(ScrubberPart);
        _table = e.NameScope.Find<ConnectedScatterDataTable>(TablePart);
        _announcer = e.NameScope.Find<AnalyticsAnnouncer>(AnnouncerPart);

        if (_seriesSwitch is not null)
        {
            _seriesSwitch.SelectionChanged += OnSwitchChanged;
        }
        if (_playButton is not null)
        {
            _playButton.Click += OnPlayClick;
        }
        if (_plot is not null)
        {
            _plot.PointInvoked += OnPointInvoked;
            _plot.DisplayChanged += OnDisplayChanged;
        }
        if (_scrubber is not null)
        {
            _scrubber.ScrubStarted += OnScrubStarted;
            _scrubber.ScrubMoved += OnScrubMoved;
            _scrubber.ScrubEnded += OnScrubEnded;
            _scrubber.KeyHandler = HandleScrubberKey;
            _scrubber.ValueRequested = index => GoToMonth(index, animate: false);
        }
        _seriesDirty = true;
        _positioned = false;
        _buttonState = null;
        Refresh(animate: false, announce: false);
        UpdateBreakpoints(Bounds.Width);
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        InitializePosition();
        if (_awaitingAppear)
        {
            WatchVisibility();
        }
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _visibility?.Dispose();
        _visibility = null;
    }

    protected override void OnSizeChanged(SizeChangedEventArgs e)
    {
        base.OnSizeChanged(e);
        UpdateBreakpoints(e.NewSize.Width);
    }

    private void UpdateBreakpoints(double width)
    {
        if (width <= 0)
        {
            return;
        }
        var content = ContentWidth(width);
        var breakpoints = new ScatterBreakpoints(content);
        PseudoClasses.Set(NarrowPseudoClass, width < NarrowWidth);
        PseudoClasses.Set(CompactPseudoClass, breakpoints.IsCompact);
        if (_scrubber is not null && _count > 0)
        {
            _scrubber.SetMonths(ShownPoints(), breakpoints.HasQuarterLabels);
        }
    }

    private double ContentWidth(double width)
    {
        var padding = Padding;
        var border = BorderThickness;
        return Math.Max(0, width - padding.Left - padding.Right - border.Left - border.Right);
    }

    private IReadOnlyList<ScatterPoint> ShownPoints() => _shown?.Points ?? (IReadOnlyList<ScatterPoint>)[];

    #endregion

    #region Property changes

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        var property = change.Property;
        if (property == SeriesSourceProperty)
        {
            ObserveSource(change.GetNewValue<IEnumerable?>());
            OnSeriesChanged();
        }
        else if (property == SelectedSeriesKeyProperty)
        {
            if (!_syncing)
            {
                var before = _shown;
                Refresh(animate: true, announce: true);
                if (!ReferenceEquals(before, _shown))
                {
                    RaiseSeriesChanged();
                }
            }
        }
        else if (property == XAxisProperty || property == YAxisProperty)
        {
            ObserveAxes();
            Refresh(animate: false, announce: false);
        }
        else if (property == TargetRatioProperty || property == ValueFormatterProperty || property == RatioFormatterProperty ||
                 property == MonthNameProperty || property == RatioNameProperty || property == RatioNameFormatProperty ||
                 property == MonthOfFormatProperty || property == SinceFormatProperty || property == SameAsFormatProperty ||
                 property == AboveTargetFormatProperty || property == BelowTargetFormatProperty || property == TargetLabelFormatProperty ||
                 property == TargetLegendFormatProperty || property == ZoneLabelFormatProperty || property == ZoneLegendTextProperty ||
                 property == SeriesLegendFormatProperty || property == DescriptionFormatProperty || property == DescriptionProperty ||
                 property == TableCaptionFormatProperty || property == MonthAnnouncementFormatProperty ||
                 property == AboveAnnouncementFormatProperty || property == BelowAnnouncementFormatProperty || property == TitleProperty ||
                 property == ScrubberLabelProperty || property == SeriesSwitchLabelProperty)
        {
            Refresh(animate: false, announce: false);
        }
        else if (property == PlayTextProperty || property == PauseTextProperty || property == ReplayTextProperty ||
                 property == PlayLabelProperty || property == PauseLabelProperty || property == ReplayLabelProperty)
        {
            _buttonState = null;
            UpdatePlayButton();
        }
        else if (property == SelectedIndexProperty)
        {
            if (!_syncing && _positioned && change.GetNewValue<int>() >= 0)
            {
                GoToMonth(change.GetNewValue<int>());
            }
        }
        else if (property == NarrowWidthProperty)
        {
            UpdateBreakpoints(Bounds.Width);
        }
    }

    private void ObserveSource(IEnumerable? source)
    {
        if (_observedSource is not null)
        {
            _observedSource.CollectionChanged -= OnSourceCollectionChanged;
        }
        _observedSource = source as INotifyCollectionChanged;
        if (_observedSource is not null)
        {
            _observedSource.CollectionChanged += OnSourceCollectionChanged;
        }
    }

    private void OnSourceCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e) => OnSeriesChanged();

    private void ObserveAxes()
    {
        if (_observedX is not null)
        {
            _observedX.Changed -= OnAxisChanged;
        }
        if (_observedY is not null)
        {
            _observedY.Changed -= OnAxisChanged;
        }
        _observedX = XAxis;
        _observedY = YAxis;
        if (_observedX is not null)
        {
            _observedX.Changed += OnAxisChanged;
        }
        if (_observedY is not null)
        {
            _observedY.Changed += OnAxisChanged;
        }
    }

    private void OnAxisChanged(object? sender, EventArgs e) => Refresh(animate: false, announce: false);

    private void OnSeriesChanged()
    {
        _seriesDirty = true;
        Refresh(animate: false, announce: false);
    }

    private void OnSeriesDataChanged(object? sender, EventArgs e)
    {
        Refresh(animate: false, announce: false);
    }

    private void EnsureSeries()
    {
        if (!_seriesDirty)
        {
            ResolveShown();
            return;
        }
        _seriesDirty = false;
        foreach (var series in _series)
        {
            series.Changed -= OnSeriesDataChanged;
        }
        _series.Clear();
        IEnumerable source = SeriesSource ?? Series;
        foreach (var item in source)
        {
            if (item is ScatterSeries series)
            {
                _series.Add(series);
                series.Changed += OnSeriesDataChanged;
            }
        }
        ResolveShown();
    }

    // The series shown: the one with the selected key, or the first.
    private void ResolveShown()
    {
        var key = SelectedSeriesKey;
        _shown = _series.FirstOrDefault(s => s.EffectiveKey == key) ?? _series.FirstOrDefault();
    }

    private void RaiseSeriesChanged()
    {
        var index = _shown is null ? -1 : _series.IndexOf(_shown);
        RaiseEvent(new ScatterSeriesChangedEventArgs(SeriesChangedEvent, _shown, index) { Source = this });
    }

    #endregion

    #region Data to the parts

    private string AxisName(ScatterAxis? axis, string fallback) => string.IsNullOrEmpty(axis?.Name) ? fallback : axis!.Name;

    private string AxisTitle(ScatterAxis? axis, string fallback) => string.IsNullOrEmpty(axis?.Title) ? AxisName(axis, fallback) : axis!.Title!;

    private string FormatValue(double value) => (ValueFormatter ?? ConnectedScatterEngine.DefaultValue)(value);

    private string FormatRatio(double ratio) => (RatioFormatter ?? ConnectedScatterEngine.DefaultRatio)(ratio);

    // "3x" for the target in notes and labels: the ratio format, but a whole number loses its ".0" unless the app set a format.
    private string FormatTarget(double ratio)
    {
        if (RatioFormatter is { } custom)
        {
            return custom(ratio);
        }
        return ratio.ToString("0.##", AnalyticsFormat.DefaultCulture) + "x";
    }

    private string EffectiveRatioName() =>
        RatioName ?? string.Format(CultureInfo.CurrentCulture, RatioNameFormat, AxisName(YAxis, "Y"), AxisName(XAxis, "X"));

    private bool HasTarget => !double.IsNaN(TargetRatio) && TargetRatio > 0;

    /// <summary>Rebuilds every part from the properties and the series (a data, axis, format or text change).</summary>
    private void Refresh(bool animate, bool announce)
    {
        EnsureSeries();
        if (_plot is null)
        {
            return;
        }
        BuildData();
        PseudoClasses.Set(EmptyPseudoClass, _count == 0);
        BuildOptions();
        _plot.SetOptions(_options);
        var notes = new List<ScatterNoteSource>();
        if (_shown is not null)
        {
            foreach (var note in _shown.Notes)
            {
                notes.Add(new ScatterNoteSource(note.Index, note.Text, note.Side));
            }
        }
        var morph = animate && ArcMotion.IsFull(this);
        _plot.SetSeries(_labels, _x, _y, notes, morph);
        UpdateSwitch();
        UpdateTexts();
        UpdateTable();
        if (_count == 0)
        {
            _positioned = false;
            _timeline.Jump(0);
            _settled = -1;
        }
        else if (_positioned)
        {
            ClampPosition();
        }
        InitializePosition();
        UpdateBreakpoints(Bounds.Width);
        if (_positioned)
        {
            _lastReadoutKey = string.Empty;
            PushPosition();
            var index = _settled >= 0 ? _settled : ConnectedScatterEngine.MonthOf(_timeline.U, _count);
            _scrubber?.Report(index, MonthAnnouncement(index));
            if (announce && _announcer is not null && _shown is not null)
            {
                _announcer.Announce(string.Format(CultureInfo.CurrentCulture, SeriesAnnouncementFormat, _shown.Name, MonthAnnouncement(index)));
            }
        }
        else
        {
            _readouts?.SetState(ScatterReadoutState.Empty);
        }
    }

    private void BuildData()
    {
        var points = _shown?.Points;
        _count = points?.Count ?? 0;
        _labels = new string[_count];
        _x = new double[_count];
        _y = new double[_count];
        for (var i = 0; i < _count; i++)
        {
            var point = points![i];
            _labels[i] = point.Label;
            _x[i] = point.X;
            _y[i] = point.Y;
        }
    }

    private void BuildOptions()
    {
        var lowX = double.PositiveInfinity;
        var highX = double.NegativeInfinity;
        var lowY = double.PositiveInfinity;
        var highY = double.NegativeInfinity;
        foreach (var series in _series)
        {
            foreach (var point in series.Points)
            {
                lowX = Math.Min(lowX, point.X);
                highX = Math.Max(highX, point.X);
                lowY = Math.Min(lowY, point.Y);
                highY = Math.Max(highY, point.Y);
            }
        }
        var xAxis = XAxis;
        var yAxis = YAxis;
        var x = ConnectedScatterEngine.ResolveAxis(xAxis?.Minimum ?? double.NaN, xAxis?.Maximum ?? double.NaN, xAxis?.Step ?? double.NaN, lowX, highX);
        var y = ConnectedScatterEngine.ResolveAxis(yAxis?.Minimum ?? double.NaN, yAxis?.Maximum ?? double.NaN, yAxis?.Step ?? double.NaN, lowY, highY);
        var target = HasTarget ? TargetRatio : double.NaN;
        var xName = AxisName(xAxis, "X");
        var yName = AxisName(yAxis, "Y");
        var targetText = HasTarget ? FormatTarget(TargetRatio) : string.Empty;
        _options = new ScatterPlotOptions
        {
            XMin = x.Min, XMax = x.Max, XStep = x.Step,
            YMin = y.Min, YMax = y.Max, YStep = y.Step,
            TargetRatio = target,
            XName = xName,
            YName = yName,
            RatioName = EffectiveRatioName(),
            TargetLabel = HasTarget ? string.Format(CultureInfo.CurrentCulture, TargetLabelFormat, targetText, xName) : string.Empty,
            ZoneLabel = HasTarget ? string.Format(CultureInfo.CurrentCulture, ZoneLabelFormat, targetText) : string.Empty,
            Value = FormatValue,
            Ratio = FormatRatio,
            XTick = xAxis?.TickFormatter,
            YTick = yAxis?.TickFormatter
        };
    }

    private void UpdateSwitch()
    {
        if (_seriesSwitch is null)
        {
            return;
        }
        _syncing = true;
        try
        {
            _seriesSwitch.ItemsSource = _series.Select(s => new SegmentedOption(s, s.Name)).ToList();
            _seriesSwitch.IsVisible = _series.Count > 1;
            AutomationProperties.SetName(_seriesSwitch, SeriesSwitchLabel);
            var index = _shown is null ? -1 : _series.IndexOf(_shown);
            if (_seriesSwitch.SelectedIndex != index)
            {
                _seriesSwitch.SelectedIndex = index;
            }
        }
        finally
        {
            _syncing = false;
        }
    }

    private void UpdateTexts()
    {
        var xAxis = XAxis;
        var yAxis = YAxis;
        if (_description is not null)
        {
            var description = EffectiveDescription;
            _description.Text = description;
            _description.IsVisible = !string.IsNullOrEmpty(description);
        }
        if (_yTitle is not null)
        {
            _yTitle.Text = yAxis?.Title;
            _yTitle.IsVisible = !string.IsNullOrEmpty(yAxis?.Title);
        }
        if (_xTitle is not null)
        {
            _xTitle.Text = xAxis?.Title;
            _xTitle.IsVisible = !string.IsNullOrEmpty(xAxis?.Title);
        }
        if (_legend is not null)
        {
            var xName = AxisName(xAxis, "X");
            var yName = AxisName(yAxis, "Y");
            var targetText = HasTarget ? FormatTarget(TargetRatio) : string.Empty;
            _legend.SetItems(string.Format(CultureInfo.CurrentCulture, SeriesLegendFormat, _shown?.Name ?? string.Empty),
                HasTarget ? string.Format(CultureInfo.CurrentCulture, TargetLegendFormat, targetText, xName, yName) : string.Empty,
                ZoneLegendText, HasTarget);
        }
        if (_scrubber is not null)
        {
            _scrubber.SliderName = ScrubberLabel;
        }
        UpdatePlayButton();
        AutomationProperties.SetName(this, Title ?? string.Empty);
    }

    /// <summary>The line under the heading: <see cref="Description"/>, or one written from the axes and the first and last month.</summary>
    public string? EffectiveDescription
    {
        get
        {
            if (Description is { } description)
            {
                return description;
            }
            if (_count == 0)
            {
                return null;
            }
            var sentence = string.Format(CultureInfo.CurrentCulture, DescriptionFormat, LowerFirst(AxisTitle(YAxis, "Y")),
                LowerFirst(AxisTitle(XAxis, "X")), _labels[0], _labels[_count - 1]);
            return sentence.Length > 0 ? char.ToUpper(sentence[0], CultureInfo.CurrentCulture) + sentence[1..] : sentence;
        }
    }

    // Axis titles are written as names ("Customer acquisition cost"); inside a sentence they start in lower case unless they are an acronym.
    private static string LowerFirst(string text) =>
        text.Length > 1 && char.IsUpper(text[0]) && !char.IsUpper(text[1]) ? char.ToLower(text[0], CultureInfo.CurrentCulture) + text[1..] : text;

    private void UpdateTable()
    {
        if (_table is null)
        {
            return;
        }
        var caption = string.Format(CultureInfo.CurrentCulture, TableCaptionFormat, _shown?.Name ?? string.Empty, AxisTitle(XAxis, "X"), AxisTitle(YAxis, "Y"));
        var xName = AxisName(XAxis, "X");
        var yName = AxisName(YAxis, "Y");
        var ratioName = EffectiveRatioName();
        var rows = new string[_count][];
        for (var i = 0; i < _count; i++)
        {
            rows[i] =
            [
                $"{xName}: {FormatValue(_x[i])}",
                $"{yName}: {FormatValue(_y[i])}",
                $"{ratioName}: {FormatRatio(ConnectedScatterEngine.Ratio(_x[i], _y[i]))}"
            ];
        }
        _table.Update(caption, _labels.ToArray(), rows);
    }

    private string MonthAnnouncement(int index)
    {
        if (index < 0 || index >= _count)
        {
            return string.Empty;
        }
        var ratio = ConnectedScatterEngine.Ratio(_x[index], _y[index]);
        var status = string.Empty;
        if (HasTarget)
        {
            var healthy = ConnectedScatterEngine.IsHealthy(_x[index], _y[index], TargetRatio);
            status = string.Format(CultureInfo.CurrentCulture, healthy ? AboveAnnouncementFormat : BelowAnnouncementFormat, FormatTarget(TargetRatio));
        }
        return string.Format(CultureInfo.CurrentCulture, MonthAnnouncementFormat, _labels[index], AxisName(XAxis, "X"), FormatValue(_x[index]),
            AxisName(YAxis, "Y"), FormatValue(_y[index]), FormatRatio(ratio), status);
    }

    #endregion

    #region Position and readouts

    private void InitializePosition()
    {
        if (_positioned || _count == 0 || _plot is null || !this.IsAttachedToVisualTree())
        {
            return;
        }
        _positioned = true;
        var requested = SelectedIndex;
        if (PlayOnFirstAppear && requested < 0 && ArcMotion.IsFull(this) && _count > 1)
        {
            _awaitingAppear = true;
            _timeline.Jump(0);
            _plot.SetReveal(false, false);
            PushPosition();
            _scrubber?.Report(0, MonthAnnouncement(0));
            WatchVisibility();
            return;
        }
        var index = requested >= 0 ? Math.Min(requested, _count - 1) : _count - 1;
        _timeline.Jump(index);
        _plot.SetReveal(true, false);
        PushPosition();
        Settle(index, raise: false);
    }

    private void ClampPosition()
    {
        if (_timeline.U > _count - 1)
        {
            _timeline.Jump(_count - 1);
        }
        if (_settled >= _count)
        {
            _settled = _count - 1;
        }
    }

    private void WatchVisibility()
    {
        _visibility?.Dispose();
        if (!_awaitingAppear || !this.IsAttachedToVisualTree())
        {
            return;
        }
        _visibility = new ViewportVisibility(this, 0.25, once: true);
        _visibility.VisibilityChanged += OnVisible;
    }

    private void OnVisible(object? sender, bool visible)
    {
        if (visible && _awaitingAppear)
        {
            StartDrawIn();
        }
    }

    private void StartDrawIn()
    {
        _awaitingAppear = false;
        _visibility?.Dispose();
        _visibility = null;
        if (_count < 2)
        {
            return;
        }
        _drawIn = true;
        _arrival = Arrival.DrawIn;
        _timeline.StartTween(0, _count - 1, Math.Max(0.001, DrawInDuration.TotalSeconds), ArcEasings.InOut);
        _loop.Start();
    }

    // A key, a press or the play button while the first run is going: it ends where it is and the layers appear.
    private void CancelDrawIn()
    {
        if (_awaitingAppear)
        {
            _awaitingAppear = false;
            _visibility?.Dispose();
            _visibility = null;
            _plot?.SetReveal(true, false);
        }
        if (_drawIn)
        {
            _drawIn = false;
            _timeline.Stop();
            _arrival = Arrival.None;
            _plot?.SetReveal(true, true);
        }
    }

    private bool Advance(double dt)
    {
        // The page's own frame loop caps a frame at 40ms, so a long frame advances a short step instead of jumping.
        dt = Math.Min(dt, 0.05);
        bool moving;
        if (IsPlaying && !ArcMotion.IsFull(this))
        {
            moving = StepPlayback(dt);
        }
        else
        {
            moving = _timeline.Advance(dt);
        }
        PushPosition();
        if (_timeline.TakeArrived())
        {
            OnArrived();
        }
        return moving || IsPlaying || _timeline.IsMoving;
    }

    // Reduced and Off motion: playback jumps a month every four month-durations (measured 960ms).
    private bool StepPlayback(double dt)
    {
        _stepClock += dt;
        var period = Math.Max(0.001, PlaybackMonthDuration.TotalSeconds) * 4;
        if (_stepClock < period)
        {
            return true;
        }
        _stepClock = 0;
        var next = Math.Floor(_timeline.U + 1e-9) + 1;
        if (next >= _count - 1)
        {
            _timeline.Jump(_count - 1);
            PushPosition();
            OnArrived();
            return false;
        }
        _timeline.Jump(next);
        return true;
    }

    private void OnArrived()
    {
        var arrival = _arrival;
        _arrival = Arrival.None;
        switch (arrival)
        {
            case Arrival.DrawIn:
                _drawIn = false;
                _plot?.SetReveal(true, true);
                Settle(_count - 1, raise: false);
                break;
            case Arrival.Playback:
                IsPlaying = false;
                PseudoClasses.Set(PlayingPseudoClass, false);
                _plot?.SetEmphasis(false);
                Settle(_count - 1, raise: true);
                break;
            case Arrival.Glide:
                Settle(ConnectedScatterEngine.MonthOf(_timeline.U, _count), raise: true);
                break;
        }
    }

    private void PushPosition()
    {
        var u = _timeline.U;
        _plot?.SetPosition(u);
        _scrubber?.SetPosition(u);
        Position = u;
        UpdateReadouts();
        UpdatePlayButton();
    }

    private void OnDisplayChanged()
    {
        _lastReadoutKey = string.Empty;
        UpdateReadouts();
    }

    // The four readouts follow the head: values interpolated between the neighbouring months of the displayed points.
    private void UpdateReadouts()
    {
        if (_readouts is null || _plot is null || _count == 0 || !_positioned)
        {
            return;
        }
        var u = _timeline.U;
        var (x, y) = _plot.ValuesAt(u);
        var month = ConnectedScatterEngine.MonthOf(u, _count);
        var xText = FormatValue(x);
        var yText = FormatValue(y);
        var ratio = ConnectedScatterEngine.Ratio(x, y);
        var ratioText = FormatRatio(ratio);
        var healthy = !HasTarget || ConnectedScatterEngine.IsHealthy(x, y, TargetRatio);
        var key = string.Concat(month.ToString(CultureInfo.InvariantCulture), "|", xText, "|", yText, "|", ratioText, "|", healthy ? "1" : "0");
        if (key == _lastReadoutKey)
        {
            return;
        }
        _lastReadoutKey = key;
        var first = _labels[0];
        var state = new ScatterReadoutState(MonthName, _labels[month],
            string.Format(CultureInfo.CurrentCulture, MonthOfFormat, month + 1, _count),
            AxisName(XAxis, "X"), xText, SinceNote(x - _x[0], first),
            AxisName(YAxis, "Y"), yText, SinceNote(y - _y[0], first),
            EffectiveRatioName(), ratioText,
            HasTarget ? string.Format(CultureInfo.CurrentCulture, healthy ? AboveTargetFormat : BelowTargetFormat, FormatTarget(TargetRatio)) : string.Empty,
            healthy, HasTarget);
        _lastReadout = state;
        _readouts.SetState(state);
        AutomationProperties.SetName(_readouts, $"{state.MonthName} {state.MonthValue}, {state.XName} {state.XValue}, {state.YName} {state.YValue}, {state.RatioName} {state.RatioValue}");
    }

    private string SinceNote(double difference, string firstLabel)
    {
        var abs = FormatValue(Math.Abs(difference));
        if (abs == FormatValue(0))
        {
            return string.Format(CultureInfo.CurrentCulture, SameAsFormat, firstLabel);
        }
        return string.Format(CultureInfo.CurrentCulture, SinceFormat, ConnectedScatterEngine.SignedDifference(difference, FormatValue), firstLabel);
    }

    /// <summary>The readout texts now (tests).</summary>
    internal ScatterReadoutState CurrentReadout => _lastReadout;

    // The head settled on a month: the property, the scrubber's value, the announcement, the event and the command.
    private void Settle(int index, bool raise)
    {
        _settled = index;
        _syncing = true;
        try
        {
            SetCurrentValue(SelectedIndexProperty, index);
        }
        finally
        {
            _syncing = false;
        }
        var text = MonthAnnouncement(index);
        _scrubber?.Report(index, text);
        _lastReadoutKey = string.Empty;
        UpdateReadouts();
        UpdatePlayButton();
        if (!raise)
        {
            return;
        }
        _announcer?.Announce(text);
        var point = _shown is not null && index < _shown.Points.Count ? _shown.Points[index] : null;
        var args = new ScatterMonthChangedEventArgs(MonthChangedEvent, index, _shown, point) { Source = this };
        RaiseEvent(args);
        if (MonthChangedCommand is { } command && command.CanExecute(args))
        {
            command.Execute(args);
        }
    }

    // A spring to a month (keys, taps, the end of a drag), or a jump when motion is not Full.
    private void MoveHeadTo(int index, bool animate)
    {
        if (animate && ArcMotion.IsFull(this) && this.IsAttachedToVisualTree())
        {
            _arrival = Arrival.Glide;
            _timeline.GlideTo(index, AnalyticsSprings.Smooth);
            if (!_timeline.IsMoving)
            {
                _timeline.TakeArrived();
                _arrival = Arrival.None;
                PushPosition();
                Settle(index, raise: true);
                return;
            }
            _loop.Start();
            return;
        }
        _arrival = Arrival.None;
        _timeline.Jump(index);
        PushPosition();
        Settle(index, raise: true);
    }

    private void StopPlayback(bool announce)
    {
        if (!IsPlaying)
        {
            return;
        }
        IsPlaying = false;
        PseudoClasses.Set(PlayingPseudoClass, false);
        _plot?.SetEmphasis(false);
        _timeline.Stop();
        _arrival = Arrival.None;
        // The head stays where it stopped (between two months); the readout names the nearest month.
        var index = ConnectedScatterEngine.MonthOf(_timeline.U, _count);
        _settled = index;
        _syncing = true;
        try
        {
            SetCurrentValue(SelectedIndexProperty, index);
        }
        finally
        {
            _syncing = false;
        }
        var text = MonthAnnouncement(index);
        _scrubber?.Report(index, text);
        UpdatePlayButton();
        if (announce)
        {
            _announcer?.Announce(text);
        }
    }

    #endregion

    #region Input

    private void OnSwitchChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_syncing || _seriesSwitch is null)
        {
            return;
        }
        if (_seriesSwitch.SelectedIndex is var index and >= 0 && index < _series.Count)
        {
            SelectedSeriesKey = _series[index].EffectiveKey;
        }
    }

    private void OnPlayClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e) => TogglePlayback();

    private void OnPointInvoked(int index) => GoToMonth(index);

    private void OnScrubStarted(double u)
    {
        CancelDrawIn();
        StopPlayback(announce: false);
        _pointerU = u;
        _plot?.SetEmphasis(true);
        _arrival = Arrival.None;
        FollowPointer(u);
    }

    private void OnScrubMoved(double u)
    {
        _pointerU = u;
        FollowPointer(u);
    }

    private void FollowPointer(double u)
    {
        if (ArcMotion.IsFull(this))
        {
            _timeline.Follow(u, AnalyticsSprings.Smooth);
            _loop.Start();
        }
        else
        {
            _timeline.Jump(u);
        }
        PushPosition();
    }

    private void OnScrubEnded()
    {
        _plot?.SetEmphasis(false);
        var month = ConnectedScatterEngine.MonthOf(_pointerU, _count);
        _timeline.EndFollow();
        MoveHeadTo(month, animate: true);
    }

    private bool HandleScrubberKey(KeyEventArgs e)
    {
        if (e.KeyModifiers != KeyModifiers.None || _count == 0)
        {
            return false;
        }
        if (ConnectedScatterEngine.KeyTarget(e.Key, _timeline.U, _count) is not { } target)
        {
            return false;
        }
        CancelDrawIn();
        StopPlayback(announce: false);
        MoveHeadTo(target, animate: true);
        return true;
    }

    #endregion

    #region Play button

    private void UpdatePlayButton()
    {
        var atEnd = _count > 1 && _timeline.U >= _count - 1 - 1e-6 && !_drawIn && !_awaitingAppear;
        var state = IsPlaying ? PlayState.Pause : atEnd ? PlayState.Replay : PlayState.Play;
        if (_buttonState == state)
        {
            return;
        }
        _buttonState = state;
        var (glyph, text, label) = state switch
        {
            PlayState.Pause => (ArcIcons.Pause, PauseText, PauseLabel),
            PlayState.Replay => (ArcIcons.RotateCcw, ReplayText, ReplayLabel),
            _ => (ArcIcons.Play, PlayText, PlayLabel)
        };
        if (_playGlyph is not null)
        {
            _playGlyph.Data = glyph;
        }
        if (_playText is not null)
        {
            _playText.Text = text;
        }
        if (_playButton is not null)
        {
            AutomationProperties.SetName(_playButton, label);
        }
    }

    #endregion

    protected override AutomationPeer OnCreateAutomationPeer() => new ConnectedScatterPeer(this);
}

/// <summary>The block for assistive technology: a group named by its title.</summary>
internal sealed class ConnectedScatterPeer(ConnectedScatter owner) : ControlAutomationPeer(owner)
{
    protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Group;

    protected override bool IsContentElementCore() => true;

    protected override bool IsControlElementCore() => true;
}

/// <summary>The play / pause / replay toggle as a command (always executable).</summary>
internal sealed class ScatterPlayPauseCommand(ConnectedScatter owner) : ICommand
{
#pragma warning disable CS0067
    public event EventHandler? CanExecuteChanged;
#pragma warning restore CS0067

    public bool CanExecute(object? parameter) => true;

    public void Execute(object? parameter) => owner.TogglePlayback();
}
