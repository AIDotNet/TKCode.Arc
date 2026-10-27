using System.Collections;
using System.Collections.Specialized;
using System.Globalization;
using TKCode.Arc.Motion;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using Avalonia.Media;

namespace TKCode.Arc;

public partial class LineReplay
{
    /// <summary>Blocks at least this wide put the moments in a column beside the chart.</summary>
    internal const double SideBySideWidth = 1000;

    /// <summary>Blocks narrower than this use the phone layout.</summary>
    internal const double NarrowWidth = 560;

    internal static readonly double[] Speeds = [0.5, 1, 2, 4];

    private readonly LineReplayStage _stage;
    private readonly LineReplayScrubber _scrubber;
    private Button? _playButton;
    private ArcGlyph? _playGlyph;
    private TextBlock? _playText;
    private Panel? _stageHost;
    private Panel? _scrubberHost;
    private SegmentedControl? _speedSwitch;
    private LineReplayReadoutPanel? _readout;
    private LineReplayMomentsPanel? _momentsPanel;
    private LineChartsLiveRegion? _liveRegion;
    private TextBlock? _versusLabel;
    private LineReplayReadoutValue? _timeValue;
    private LineReplayReadoutValue? _valueValue;
    private LineReplayReadoutValue? _ratioValue;
    private LineReplayReadoutValue? _peakValue;
    private readonly List<LineReplayMomentButton> _momentButtons = new();

    private LineReplayData _data = LineReplayData.Empty;
    private INotifyCollectionChanged? _observedMoments;
    private readonly List<LineReplayMoment> _observedItems = new();
    private bool _syncing;
    private bool _rebuildQueued;
    private bool _positioned;
    private double _containerWidth = double.NaN;

    public LineReplay()
    {
        _stage = new LineReplayStage(this);
        _scrubber = new LineReplayScrubber(this);
        _motion = new MotionValue<double>(this, 0, MotionVectors.Double);
        _motion.Changed += OnPositionChanged;
        _loop = new LiveLoop(this, OnFrame) { MinimumLevel = ArcMotionLevel.Off, TracksViewport = false };
        Moments.CollectionChanged += (_, _) => OnDataChanged();
    }

    /// <summary>The chart (tests).</summary>
    internal LineReplayStage Stage => _stage;

    /// <summary>The scrubber (tests).</summary>
    internal LineReplayScrubber Scrubber => _scrubber;

    /// <summary>The prepared data (tests).</summary>
    internal LineReplayData Data => _data;

    /// <summary>The moment rows (tests).</summary>
    internal IReadOnlyList<LineReplayMomentButton> MomentButtons => _momentButtons;

    /// <summary>The live region (tests).</summary>
    internal LineChartsLiveRegion? LiveRegion => _liveRegion;

    /// <summary>The readout's values: time, value, ratio, peak (tests).</summary>
    internal (string Time, string Value, string Ratio, string Peak) ReadoutTexts =>
        (_timeValue?.Value ?? "", _valueValue?.Value ?? "", _ratioValue?.Value ?? "", _peakValue?.Value ?? "");

    // Template -------------------------------------------------------------------------------------------------------------

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        if (_playButton is not null)
        {
            _playButton.Click -= OnPlayClick;
        }
        if (_speedSwitch is not null)
        {
            _speedSwitch.SelectionChanged -= OnSpeedSwitchChanged;
        }
        _stageHost?.Children.Remove(_stage);
        _scrubberHost?.Children.Remove(_scrubber);
        _momentsPanel?.Children.Clear();

        _playButton = e.NameScope.Find<Button>(PlayButtonPart);
        _playGlyph = e.NameScope.Find<ArcGlyph>(PlayGlyphPart);
        _playText = e.NameScope.Find<TextBlock>(PlayTextPart);
        _stageHost = e.NameScope.Find<Panel>(StagePart);
        _scrubberHost = e.NameScope.Find<Panel>(ScrubberPart);
        _speedSwitch = e.NameScope.Find<SegmentedControl>(SpeedSwitchPart);
        _readout = e.NameScope.Find<LineReplayReadoutPanel>(ReadoutPart);
        _momentsPanel = e.NameScope.Find<LineReplayMomentsPanel>(MomentsPart);
        _liveRegion = e.NameScope.Find<LineChartsLiveRegion>(LiveRegionPart);
        _versusLabel = e.NameScope.Find<TextBlock>("PART_VersusLabel");
        _timeValue = e.NameScope.Find<LineReplayReadoutValue>("PART_TimeValue");
        _valueValue = e.NameScope.Find<LineReplayReadoutValue>("PART_ValueValue");
        _ratioValue = e.NameScope.Find<LineReplayReadoutValue>("PART_RatioValue");
        _peakValue = e.NameScope.Find<LineReplayReadoutValue>("PART_PeakValue");

        _stageHost?.Children.Add(_stage);
        _scrubberHost?.Children.Add(_scrubber);
        if (_playButton is not null)
        {
            _playButton.Click += OnPlayClick;
        }
        UpdateSpeedOptions();
        if (_speedSwitch is not null)
        {
            _speedSwitch.SelectionChanged += OnSpeedSwitchChanged;
        }
        _containerWidth = double.NaN;
        UpdateLabels();
        Rebuild();
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        if (double.IsFinite(availableSize.Width))
        {
            ApplyContainerWidth(availableSize.Width);
        }
        return base.MeasureOverride(availableSize);
    }

    /// <summary>The block's own width picks the layout: moments beside the chart from 1000, the phone layout below 560.</summary>
    private void ApplyContainerWidth(double width)
    {
        if (width == _containerWidth)
        {
            return;
        }
        _containerWidth = width;
        var stacked = width < SideBySideWidth;
        var narrow = width < NarrowWidth;
        PseudoClasses.Set(StackedPseudoClass, stacked);
        PseudoClasses.Set(NarrowPseudoClass, narrow);
        if (_momentsPanel is not null)
        {
            _momentsPanel.Columns = stacked && !narrow ? 2 : 1;
        }
        if (_readout is not null)
        {
            _readout.IsNarrow = narrow;
        }
        _stage.Height = narrow ? 300 : 380;
        _stage.SetNarrow(narrow);
    }

    // Property changes -----------------------------------------------------------------------------------------------------

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        var property = change.Property;
        if (property == PointsProperty || property == MomentsSourceProperty)
        {
            OnDataChanged();
        }
        else if (property == SpeedProperty)
        {
            UpdateSpeedOptions();
        }
        else if (property == DefaultPositionProperty)
        {
            if (!_playedOnce && !_isPlaying)
            {
                _positioned = false;
                PlaceDefault();
            }
        }
        else if (property == TimeFormatterProperty || property == TickFormatterProperty || property == ValueFormatterProperty ||
                 property == RiseFormatProperty || property == DipFormatProperty || property == PeakFormatProperty)
        {
            UpdateMomentTexts();
            UpdateSamples();
            UpdateFrame(_motion.Value, animate: false);
        }
        else if (property == PlayTextProperty || property == PauseTextProperty || property == ReplayTextProperty ||
                 property == PlayLabelProperty || property == PauseLabelProperty || property == ReplayLabelProperty)
        {
            UpdatePlayButton();
        }
        else if (property == BaselineLabelProperty || property == VersusFormatProperty || property == SpeedLabelProperty ||
                 property == ValueLabelProperty)
        {
            UpdateLabels();
            ReportPosition();
        }
    }

    private void UpdateLabels()
    {
        if (_versusLabel is not null)
        {
            _versusLabel.Text = string.Format(CultureInfo.CurrentCulture, VersusFormat, LowerFirst(BaselineLabel));
        }
        if (_speedSwitch is not null)
        {
            AutomationProperties.SetName(_speedSwitch, SpeedLabel);
        }
    }

    private static string LowerFirst(string text) =>
        text.Length > 0 ? char.ToLower(text[0], CultureInfo.CurrentCulture) + text[1..] : text;

    private void UpdateSpeedOptions()
    {
        if (_speedSwitch is null)
        {
            return;
        }
        _syncing = true;
        try
        {
            _speedSwitch.ItemsSource ??= Speeds.Select(s => new SegmentedOption(s, s.ToString("0.#", CultureInfo.CurrentCulture) + "×"))
                .ToArray();
            var index = Array.IndexOf(Speeds, Speed);
            _speedSwitch.SelectedIndex = index >= 0 ? index : -1;
        }
        finally
        {
            _syncing = false;
        }
    }

    private void OnSpeedSwitchChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (!_syncing && _speedSwitch is { SelectedIndex: >= 0 } s && s.SelectedIndex < Speeds.Length)
        {
            SetCurrentValue(SpeedProperty, Speeds[s.SelectedIndex]);
        }
    }

    private void OnPlayClick(object? sender, RoutedEventArgs e) => TogglePlay();

    // Data -----------------------------------------------------------------------------------------------------------------

    private void OnDataChanged()
    {
        if (_rebuildQueued)
        {
            return;
        }
        // Several edits in a row (filling the moments one by one) rebuild once.
        _rebuildQueued = true;
        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            if (_rebuildQueued)
            {
                Rebuild();
            }
        });
    }

    /// <summary>Rebuilds the prepared data and every part from it (also called by tests to skip the dispatcher).</summary>
    internal void Rebuild()
    {
        _rebuildQueued = false;
        ObserveMoments();
        var moments = MomentsSource is { } source ? source.OfType<LineReplayMoment>().ToList() : Moments.ToList();
        foreach (var item in _observedItems)
        {
            item.PropertyChanged -= OnMomentPropertyChanged;
        }
        _observedItems.Clear();
        _observedItems.AddRange(moments);
        foreach (var item in _observedItems)
        {
            item.PropertyChanged += OnMomentPropertyChanged;
        }

        var hadData = _data.Count > 0;
        _data = new LineReplayData(Points ?? [], moments);
        PseudoClasses.Set(NoBaselinePseudoClass, !_data.HasBaseline);
        PseudoClasses.Set(NoMomentsPseudoClass, _data.Moments.Length == 0);
        _stage.SetData(_data);
        _scrubber.SetData(_data);
        BuildMomentButtons();
        UpdateSamples();
        if (!hadData || !_positioned)
        {
            _positioned = false;
            PlaceDefault();
        }
        else
        {
            var clamped = Math.Clamp(_motion.Value, _data.Start, _data.End);
            _motion.Jump(clamped);
            UpdateFrame(clamped, animate: false);
        }
    }

    private void ObserveMoments()
    {
        var observed = MomentsSource as INotifyCollectionChanged;
        if (ReferenceEquals(observed, _observedMoments))
        {
            return;
        }
        if (_observedMoments is not null)
        {
            _observedMoments.CollectionChanged -= OnMomentsSourceChanged;
        }
        _observedMoments = observed;
        if (observed is not null)
        {
            observed.CollectionChanged += OnMomentsSourceChanged;
        }
    }

    private void OnMomentsSourceChanged(object? sender, NotifyCollectionChangedEventArgs e) => OnDataChanged();

    private void OnMomentPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e) => OnDataChanged();

    /// <summary>Puts the playhead where it rests before the first play (no animation).</summary>
    private void PlaceDefault()
    {
        if (_data.Count == 0)
        {
            return;
        }
        _positioned = true;
        var position = DefaultPosition == LineReplayPosition.End ? _data.End : _data.Start;
        _motion.Jump(position);
        UpdateFrame(position, animate: false);
    }

    private void BuildMomentButtons()
    {
        foreach (var button in _momentButtons)
        {
            button.Click -= OnMomentClick;
        }
        _momentButtons.Clear();
        _momentsPanel?.Children.Clear();
        foreach (var info in _data.Moments)
        {
            var button = new LineReplayMomentButton(info)
            {
                Glyph = GlyphOf(info.Moment.Kind),
                Title = info.Moment.Title,
                Detail = info.Moment.Detail,
            };
            button.Click += OnMomentClick;
            _momentButtons.Add(button);
            _momentsPanel?.Children.Add(button);
        }
        UpdateMomentTexts();
    }

    private void UpdateMomentTexts()
    {
        foreach (var button in _momentButtons)
        {
            var info = button.Info;
            button.TimeText = FormatTimeText(info.Time);
            button.EffectText = EffectText(info);
        }
    }

    internal string EffectText(LineReplayMomentInfo info)
    {
        if (!double.IsFinite(info.Effect))
        {
            return "";
        }
        if (info.IsIncident)
        {
            var percent = Math.Round(info.Effect * 100, MidpointRounding.AwayFromZero);
            var signed = (percent < 0 ? LineChartsFormat.Minus : "+") + Math.Abs(percent).ToString("0", LineChartsFormat.Culture) + "%";
            return string.Format(CultureInfo.CurrentCulture, DipFormat, signed);
        }
        return string.Format(CultureInfo.CurrentCulture, RiseFormat, FormatValueText(Math.Max(0, info.Effect)));
    }

    internal static Geometry GlyphOf(LineReplayMomentKind kind) => kind switch
    {
        LineReplayMomentKind.Press => LineChartsGlyphs.Newspaper,
        LineReplayMomentKind.Email => ArcIcons.Mail,
        LineReplayMomentKind.Incident => ArcIcons.TriangleAlert,
        LineReplayMomentKind.Milestone => ArcIcons.Trophy,
        _ => LineChartsGlyphs.Rocket,
    };

    private void OnMomentClick(object? sender, RoutedEventArgs e)
    {
        if (sender is LineReplayMomentButton button)
        {
            SeekToMoment(button.Info.Moment);
        }
    }

    // Texts ----------------------------------------------------------------------------------------------------------------

    internal string FormatTimeText(double minutes) =>
        TimeFormatter is { } formatter ? formatter(minutes) : LineChartsFormat.Clock(minutes);

    internal string FormatTickText(double minutes) =>
        TickFormatter is { } formatter ? formatter(minutes) : LineChartsFormat.Hour(minutes);

    internal string FormatValueText(double value) =>
        !double.IsFinite(value) ? "–" : ValueFormatter is { } formatter ? formatter(value) : LineChartsFormat.Count(value);

    private static string Ratio(double value, double baseline) =>
        double.IsFinite(baseline) && baseline > 0 ? (value / baseline).ToString("0.0", LineChartsFormat.Culture) + "×" : "–";

    private string PeakReadout(double position)
    {
        var (peak, at) = _data.PeakAt(position);
        return double.IsFinite(peak)
            ? string.Format(CultureInfo.CurrentCulture, PeakFormat, FormatValueText(peak), FormatTimeText(at))
            : "–";
    }

    /// <summary>The scrubber's value text: "9:40 am, 2,316 on site".</summary>
    internal string ValueText(double position) =>
        $"{FormatTimeText(position)}, {FormatValueText(_data.ValueAt(position))} {ValueLabel.ToLower(CultureInfo.CurrentCulture)}";

    private void UpdateSamples()
    {
        if (_data.Count == 0)
        {
            return;
        }
        // Each value is sized for its widest form, so playback never moves the readout's columns.
        var span = _data.End - _data.Start;
        var timeSample = Enumerable.Range(0, 49).Select(i => FormatTimeText(_data.Start + span * i / 48))
            .OrderByDescending(t => t.Length).First();
        var max = _data.PeakValues.Length > 0 ? _data.PeakValues[^1] : 0;
        if (_timeValue is not null)
        {
            _timeValue.Sample = timeSample;
        }
        if (_valueValue is not null)
        {
            _valueValue.Sample = FormatValueText(max);
        }
        if (_ratioValue is not null)
        {
            _ratioValue.Sample = "88.8×";
        }
        if (_peakValue is not null)
        {
            _peakValue.Sample = string.Format(CultureInfo.CurrentCulture, PeakFormat, FormatValueText(max), timeSample);
        }
    }

    private void UpdateReadout(double position)
    {
        _timeValue?.SetValue(FormatTimeText(position));
        var value = _data.ValueAt(position);
        _valueValue?.SetValue(FormatValueText(value));
        _ratioValue?.SetValue(Ratio(value, _data.BaselineAt(position)));
        _peakValue?.SetValue(PeakReadout(position));
    }

    private void UpdateMomentStates(double position)
    {
        var current = -1;
        for (var i = 0; i < _data.Moments.Length; i++)
        {
            if (_data.Moments[i].Time <= position + 1e-6)
            {
                current = i;
            }
        }
        foreach (var button in _momentButtons)
        {
            var info = button.Info;
            var reached = info.Time <= position + 1e-6;
            button.SetState(reached, info.Index == current, position >= Math.Min(_data.End, info.Time + info.Window) - 1e-6 && reached);
        }
    }

    private void UpdatePlayButton()
    {
        var atEnd = _data.Count > 0 && _motion.Value >= _data.End - 1e-6;
        var (glyph, text, label) = _isPlaying ? (ArcIcons.Pause, PauseText, PauseLabel)
            : atEnd ? (ArcIcons.RotateCcw, ReplayText, ReplayLabel)
            : (ArcIcons.Play, PlayText, PlayLabel);
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
        PseudoClasses.Set(PlayingPseudoClass, _isPlaying);
    }
}
