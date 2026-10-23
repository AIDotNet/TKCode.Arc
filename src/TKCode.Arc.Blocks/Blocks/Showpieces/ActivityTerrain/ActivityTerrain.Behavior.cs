using System.Globalization;
using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using AvaloniaButton = Avalonia.Controls.Button;

namespace TKCode.Arc;

public partial class ActivityTerrain
{
    internal static readonly IReadOnlyList<string> DefaultDayNames = ["Monday", "Tuesday", "Wednesday", "Thursday", "Friday", "Saturday", "Sunday"];
    internal static readonly IReadOnlyList<string> DefaultDayAbbreviations = ["Mon", "Tue", "Wed", "Thu", "Fri", "Sat", "Sun"];
    private static readonly CultureInfo s_culture = CultureInfo.GetCultureInfo("en-US");

    private SegmentedControl? _metricSwitch;
    private SegmentedControl? _viewSwitch;
    private ActivityTerrainBodyPanel? _body;
    private Panel? _plot;
    private ActivityTerrainCanvas? _canvas;
    private ShowpieceHoverCard? _card;
    private readonly ActivityTerrainCardView _cardView = new();
    private PresenceHost? _resetPresence;
    private AvaloniaButton? _reset;
    private Panel? _swatches;
    private ActivityTerrainStatsPanel? _stats;
    private ArcContentSwap? _summaryTitle;
    private ShowpieceNumber? _total;
    private ArcGlyph? _changeGlyph;
    private ShowpieceNumber? _change;
    private StackPanel? _peaks;
    private ArcContentSwap? _busiestDay;
    private ActivityTerrainCaption? _busiestCaption;
    private ShowpieceLiveRegion? _liveRegion;
    private ActivityTerrainDataTable? _dataTable;
    private TextBlock? _hint;

    private ActivityTerrainMetric? _metric;
    private ActivityTerrainSummary _summary = ActivityTerrainSummary.Empty;
    private bool _metricOptionsDirty = true;
    private bool _syncing;
    private bool _resetting;
    private bool _touched;
    private bool _primed;
    private double _containerWidth = double.NaN;

    /// <summary>The plot (tests).</summary>
    internal ActivityTerrainCanvas? Canvas => _canvas;

    /// <summary>The hover card (tests).</summary>
    internal ShowpieceHoverCard? Card => _card;

    /// <summary>The card's content (tests).</summary>
    internal ActivityTerrainCardView CardView => _cardView;

    /// <summary>The derived numbers of the shown metric (tests).</summary>
    internal ActivityTerrainSummary Summary => _summary;

    /// <summary>The metric being shown: <see cref="SelectedMetric"/> when it is one of <see cref="Metrics"/>, else by key, else the first.</summary>
    internal ActivityTerrainMetric? CurrentMetric => _metric;

    /// <summary>The live region (tests).</summary>
    internal ShowpieceLiveRegion? LiveRegion => _liveRegion;

    /// <summary>The table for screen readers (tests).</summary>
    internal ActivityTerrainDataTable? DataTable => _dataTable;

    internal IReadOnlyList<string> DayNameList => DayNames is { Count: >= ActivityTerrainMath.Days } names ? names : DefaultDayNames;

    internal IReadOnlyList<string> DayAbbreviationList =>
        DayAbbreviations is { Count: >= ActivityTerrainMath.Days } names ? names : DefaultDayAbbreviations;

    // Template -------------------------------------------------------------------------------------------------------

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        if (_metricSwitch is not null)
        {
            _metricSwitch.SelectionChanged -= OnMetricSwitchChanged;
        }
        if (_viewSwitch is not null)
        {
            _viewSwitch.SelectionChanged -= OnViewSwitchChanged;
        }
        if (_reset is not null)
        {
            _reset.Click -= OnResetClick;
        }
        if (_canvas is not null)
        {
            _canvas.Owner = null;
        }
        if (_card is not null)
        {
            _card.Content = null;
        }

        _metricSwitch = e.NameScope.Find<SegmentedControl>(MetricSwitchPart);
        _viewSwitch = e.NameScope.Find<SegmentedControl>(ViewSwitchPart);
        _body = e.NameScope.Find<ActivityTerrainBodyPanel>(BodyPart);
        _plot = e.NameScope.Find<Panel>(PlotPart);
        _canvas = e.NameScope.Find<ActivityTerrainCanvas>(CanvasPart);
        _card = e.NameScope.Find<ShowpieceHoverCard>(CardPart);
        _resetPresence = e.NameScope.Find<PresenceHost>(ResetPresencePart);
        _reset = e.NameScope.Find<AvaloniaButton>(ResetPart);
        _swatches = e.NameScope.Find<Panel>(SwatchesPart);
        _stats = e.NameScope.Find<ActivityTerrainStatsPanel>(StatsPart);
        _summaryTitle = e.NameScope.Find<ArcContentSwap>(SummaryTitlePart);
        _total = e.NameScope.Find<ShowpieceNumber>(TotalPart);
        _changeGlyph = e.NameScope.Find<ArcGlyph>(ChangeGlyphPart);
        _change = e.NameScope.Find<ShowpieceNumber>(ChangePart);
        _peaks = e.NameScope.Find<StackPanel>(PeaksPart);
        _busiestDay = e.NameScope.Find<ArcContentSwap>(BusiestDayPart);
        _busiestCaption = e.NameScope.Find<ActivityTerrainCaption>(BusiestCaptionPart);
        _liveRegion = e.NameScope.Find<ShowpieceLiveRegion>(LiveRegionPart);
        _dataTable = e.NameScope.Find<ActivityTerrainDataTable>(DataTablePart);
        _hint = e.NameScope.Find<TextBlock>(HintPart);

        if (_canvas is not null)
        {
            _canvas.Owner = this;
            _canvas.SetViewMode(ViewMode, animate: false);
        }
        if (_card is not null)
        {
            _card.Placement = ShowpieceCardPlacement.Terrain;
            _card.PositionSpring = ShowpieceSprings.CardFollow;
            _card.FadeInDuration = ArcMotionPresets.Instant;
            _card.FadeOutDuration = ArcMotionPresets.Fast;
            _card.Content = _cardView;
        }
        if (_metricSwitch is not null)
        {
            _metricSwitch.SelectionChanged += OnMetricSwitchChanged;
        }
        if (_viewSwitch is not null)
        {
            _viewSwitch.SelectionChanged += OnViewSwitchChanged;
        }
        if (_reset is not null)
        {
            _reset.Click += OnResetClick;
        }
        _peakButtons.Clear();
        _metricOptionsDirty = true;
        _containerWidth = double.NaN;
        _primed = false;
        UpdateViewOptions();
        UpdateTexts();
        UpdateHint();
        Refresh(animate: false);
        UpdateCamera();
    }

    // Layout -----------------------------------------------------------------------------------------------------------

    protected override Size MeasureOverride(Size availableSize)
    {
        if (double.IsFinite(availableSize.Width))
        {
            ApplyContainerWidth(availableSize.Width - BorderThickness.Left - BorderThickness.Right);
        }
        return base.MeasureOverride(availableSize);
    }

    /// <summary>The block's own width picks the layout (spec 8): narrow below 560, wide from 880; the plot is 44% of it.</summary>
    private void ApplyContainerWidth(double width)
    {
        if (width == _containerWidth)
        {
            return;
        }
        _containerWidth = width;
        var narrow = width < ActivityTerrainMath.NarrowWidth;
        var wide = width >= ActivityTerrainMath.WideWidth;
        PseudoClasses.Set(NarrowPseudoClass, narrow);
        PseudoClasses.Set(WidePseudoClass, wide);
        if (_plot is not null)
        {
            _plot.Height = ActivityTerrainMath.PlotHeight(width);
        }
        if (_body is not null)
        {
            _body.IsWide = wide;
        }
        if (_stats is not null)
        {
            _stats.Layout = wide ? ActivityTerrainStatsLayout.Column : narrow ? ActivityTerrainStatsLayout.Narrow : ActivityTerrainStatsLayout.Row;
        }
        _canvas?.SetNarrow(narrow);
    }

    // Property changes ------------------------------------------------------------------------------------------------

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        var property = change.Property;
        if (property == SelectedMetricProperty)
        {
            if (!_syncing && SelectedMetric is { } metric)
            {
                SyncKey(metric);
            }
            Refresh(animate: true);
        }
        else if (property == SelectedMetricKeyProperty)
        {
            if (!_syncing)
            {
                var match = SelectedMetricKey is { } key ? Metrics.FirstOrDefault(m => m.EffectiveKey == key) : null;
                if (match is not null && !ReferenceEquals(match, SelectedMetric))
                {
                    SetCurrentValue(SelectedMetricProperty, match);
                }
            }
        }
        else if (property == ViewModeProperty)
        {
            OnViewModeChanged(change.GetNewValue<ActivityTerrainViewMode>());
        }
        else if (property == HighlightedCellProperty)
        {
            OnHighlightedChanged(change.GetNewValue<ActivityTerrainCell?>());
        }
        else if (property == FocusedCellProperty)
        {
            OnFocusedChanged(change.GetNewValue<ActivityTerrainCell?>());
        }
        else if (property == PeakCountProperty || property == WorkingHoursProperty || property == DayNamesProperty ||
                 property == DayAbbreviationsProperty)
        {
            _peakButtons.Clear();
            Refresh(animate: false);
        }
        else if (property == PerspectiveTextProperty || property == TopViewTextProperty)
        {
            UpdateViewOptions();
        }
        else if (property == HintTextProperty || property == TouchHintTextProperty)
        {
            UpdateHint();
        }
        if (IsTextProperty(property))
        {
            UpdateTexts();
            Refresh(animate: false);
        }
    }

    private void SyncKey(ActivityTerrainMetric metric)
    {
        _syncing = true;
        try
        {
            SetCurrentValue(SelectedMetricKeyProperty, metric.EffectiveKey);
        }
        finally
        {
            _syncing = false;
        }
    }

    // Refresh ----------------------------------------------------------------------------------------------------------

    private ActivityTerrainMetric? ResolveMetric()
    {
        if (SelectedMetric is { } selected && Metrics.Contains(selected))
        {
            return selected;
        }
        if (SelectedMetricKey is { } key && Metrics.FirstOrDefault(m => m.EffectiveKey == key) is { } byKey)
        {
            return byKey;
        }
        return Metrics.Count > 0 ? Metrics[0] : null;
    }

    /// <summary>Re-derives everything shown from the current metric; <paramref name="animate"/> rises the bars in a wave.</summary>
    private void Refresh(bool animate)
    {
        var metric = ResolveMetric();
        var switched = !ReferenceEquals(metric, _metric);
        _metric = metric;
        if (metric is not null && !ReferenceEquals(SelectedMetric, metric) && !_syncing)
        {
            _syncing = true;
            try
            {
                SetCurrentValue(SelectedMetricProperty, metric);
                SetCurrentValue(SelectedMetricKeyProperty, metric.EffectiveKey);
            }
            finally
            {
                _syncing = false;
            }
        }
        _summary = ActivityTerrainSummary.Create(metric?.Values, metric?.PreviousValues, PeakCount, WorkingHours);
        PseudoClasses.Set(EmptyPseudoClass, metric is null || _summary.IsEmpty);
        PseudoClasses.Set(NoComparisonPseudoClass, _summary.Change is null);
        if (_canvas is not null)
        {
            _canvas.SetData(_summary, metric?.Tone ?? ActivityTerrainTone.Accent, animate && _primed);
            _primed = metric is not null;
        }
        UpdateMetricOptions();
        UpdateStats();
        UpdateLegend();
        UpdateDataTable();
        UpdateCard();
        if (switched && animate && metric is not null && _canvas is not null)
        {
            Announce(Format(MetricAnnouncementFormat, LabelOf(metric), FormatValue(_summary.Total),
                $"{DayNameList[_summary.Peak.Day]} {HourText(_summary.Peak.Hour)}", FormatValue(_summary.ValueAt(_summary.Peak))));
        }
    }

    private void UpdateMetricOptions()
    {
        if (_metricSwitch is null)
        {
            return;
        }
        _syncing = true;
        try
        {
            if (_metricOptionsDirty)
            {
                _metricOptionsDirty = false;
                _metricSwitch.ItemsSource = Metrics.Select(m => new SegmentedOption(m, LabelOf(m))).ToList();
            }
            var index = _metric is null ? -1 : Metrics.IndexOf(_metric);
            if (_metricSwitch.SelectedIndex != index)
            {
                _metricSwitch.SelectedIndex = index;
            }
            _metricSwitch.IsVisible = Metrics.Count > 1;
            AutomationProperties.SetName(_metricSwitch, MetricSwitchLabel);
        }
        finally
        {
            _syncing = false;
        }
    }

    private void UpdateViewOptions()
    {
        if (_viewSwitch is null)
        {
            return;
        }
        _syncing = true;
        try
        {
            _viewSwitch.ItemsSource = new[]
            {
                new SegmentedOption(ActivityTerrainViewMode.Perspective, PerspectiveText),
                new SegmentedOption(ActivityTerrainViewMode.Top, TopViewText)
            };
            _viewSwitch.SelectedIndex = (int)ViewMode;
            AutomationProperties.SetName(_viewSwitch, ViewSwitchLabel);
        }
        finally
        {
            _syncing = false;
        }
    }

    private void OnMetricSwitchChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_syncing || _metricSwitch is null)
        {
            return;
        }
        var index = _metricSwitch.SelectedIndex;
        if (index >= 0 && index < Metrics.Count)
        {
            SetCurrentValue(SelectedMetricProperty, Metrics[index]);
        }
    }

    private void OnViewSwitchChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_syncing || _viewSwitch is null || _viewSwitch.SelectedIndex < 0)
        {
            return;
        }
        SetViewMode((ActivityTerrainViewMode)_viewSwitch.SelectedIndex);
    }

    private void UpdateTexts()
    {
        if (_reset is not null)
        {
            _reset.Content = ResetViewText;
            AutomationProperties.SetName(_reset, ResetViewText);
        }
        if (_stats is not null)
        {
            AutomationProperties.SetName(_stats, SummaryLabel);
        }
        if (_canvas is not null)
        {
            AutomationProperties.SetHelpText(_canvas, KeyboardHintText);
        }
    }

    // Stats --------------------------------------------------------------------------------------------------------------

    private string LabelOf(ActivityTerrainMetric metric) => metric.Label ?? metric.Key ?? string.Empty;

    private string UnitOf(ActivityTerrainMetric? metric) => metric?.Unit ?? string.Empty;

    private string FormatValue(double value) => _metric?.ValueFormat is { Length: > 0 } format
        ? value.ToString(format, s_culture)
        : ShowpieceFormat.Grouped(value);

    private Func<double, string>? ValueFormatter => _metric?.ValueFormat is { Length: > 0 } ? FormatValue : null;

    private static string Format(string format, params object?[] args)
    {
        try
        {
            return string.Format(s_culture, format, args);
        }
        catch (FormatException)
        {
            return format;
        }
    }

    private static string HourText(int hour) => hour.ToString("00", CultureInfo.InvariantCulture) + ":00";

    private string HourRange(int hour) => Format(HourRangeFormat, HourText(hour), HourText((hour + 1) % ActivityTerrainMath.Hours));

    /// <summary>+1 good news, −1 bad news, 0 flat or unknown.</summary>
    private int ToneOf(double? change)
    {
        if (change is not { } value || Math.Abs(value) < 0.0005)
        {
            return 0;
        }
        var higherIsBetter = _metric?.HigherIsBetter ?? true;
        return value > 0 == higherIsBetter ? 1 : -1;
    }

    private readonly List<ActivityTerrainPeakButton> _peakButtons = new();

    private void UpdateStats()
    {
        var metric = _metric;
        if (_summaryTitle is not null)
        {
            _summaryTitle.Content = metric?.SummaryTitle ?? (metric is null ? string.Empty : Format(SummaryTitleFormat, LabelOf(metric)));
        }
        if (_total is not null)
        {
            _total.Formatter = ValueFormatter;
            _total.Value = _summary.Total;
        }
        var change = _summary.Change;
        var tone = ToneOf(change);
        PseudoClasses.Set(ChangeGoodPseudoClass, tone > 0);
        PseudoClasses.Set(ChangeBadPseudoClass, tone < 0);
        if (_change is not null)
        {
            _change.Value = change ?? 0;
        }
        if (_changeGlyph is not null)
        {
            _changeGlyph.Data = change is < 0 ? ShowpieceIcons.ArrowDownRight : ArcIcons.ArrowUpRight;
        }
        if (_busiestDay is not null)
        {
            _busiestDay.Content = metric is null ? string.Empty : DayNameList[_summary.BusiestDay];
        }
        _busiestCaption?.Update(BusiestDayFormat, UnitOf(metric), _summary.DayTotals[_summary.BusiestDay], _summary.WorkingShare, ValueFormatter);
        UpdatePeaks();
    }

    private void UpdatePeaks()
    {
        if (_peaks is null)
        {
            return;
        }
        var peaks = _summary.Peaks;
        while (_peakButtons.Count < peaks.Length)
        {
            var button = new ActivityTerrainPeakButton();
            button.Click += OnPeakClick;
            _peakButtons.Add(button);
        }
        while (_peakButtons.Count > peaks.Length)
        {
            var last = _peakButtons[^1];
            last.Click -= OnPeakClick;
            _peakButtons.RemoveAt(_peakButtons.Count - 1);
        }
        if (_peaks.Children.Count != _peakButtons.Count || !_peaks.Children.SequenceEqual(_peakButtons))
        {
            _peaks.Children.Clear();
            foreach (var button in _peakButtons)
            {
                _peaks.Children.Add(button);
            }
        }
        var days = DayAbbreviationList;
        for (var i = 0; i < peaks.Length; i++)
        {
            var cell = peaks[i];
            var button = _peakButtons[i];
            button.Cell = cell;
            button.Label = $"{days[cell.Day]} {HourText(cell.Hour)}";
            button.Formatter = ValueFormatter;
            button.Value = _summary.ValueAt(cell);
            button.IsChecked = FocusedCell == cell;
            AutomationProperties.SetName(button, Format(PeakButtonFormat, DayNameList[cell.Day], HourText(cell.Hour),
                FormatValue(_summary.ValueAt(cell)), UnitOf(_metric)));
        }
    }

    private void OnPeakClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not ActivityTerrainPeakButton button)
        {
            return;
        }
        if (FocusedCell == button.Cell)
        {
            ShowWeek();
            return;
        }
        SetCurrentValue(HighlightedCellProperty, button.Cell);
        FlyTo(button.Cell);
    }

    private void UpdateLegend()
    {
        if (_swatches is null || TopLevel.GetTopLevel(this) is null)
        {
            return;
        }
        var low = ActivityTerrainPalette.Low(this);
        var tone = ActivityTerrainOklab.FromColor(ActivityTerrainPalette.ToneColor(this, _metric?.Tone ?? ActivityTerrainTone.Accent));
        var count = _swatches.Children.Count;
        for (var k = 0; k < count; k++)
        {
            if (_swatches.Children[k] is Border swatch)
            {
                swatch.Background = new ImmutableSolidColorBrush(ActivityTerrainPalette.At(low, tone, count > 1 ? k / (double)(count - 1) : 1));
            }
        }
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        UpdateLegend();
    }

    private void UpdateDataTable()
    {
        if (_dataTable is null)
        {
            return;
        }
        var metric = _metric;
        var unit = UnitOf(metric);
        var names = DayNameList;
        var rows = new string[ActivityTerrainMath.Days];
        var cells = new string[ActivityTerrainMath.Days][];
        for (var d = 0; d < ActivityTerrainMath.Days; d++)
        {
            rows[d] = names[d];
            var row = new string[ActivityTerrainMath.Hours];
            for (var h = 0; h < ActivityTerrainMath.Hours; h++)
            {
                var value = FormatValue(_summary.ValueAt(new ActivityTerrainCell(d, h)));
                row[h] = unit.Length > 0 ? $"{HourText(h)}: {value} {unit}" : $"{HourText(h)}: {value}";
            }
            cells[d] = row;
        }
        _dataTable.Update(metric is null ? string.Empty : Format(TableCaptionFormat, LabelOf(metric)), rows, cells);
    }

    /// <summary>The plot's name for screen readers: what the week looks like in one sentence.</summary>
    internal string? ChartSummary
    {
        get
        {
            if (_metric is not { } metric)
            {
                return null;
            }
            var change = _summary.Change is { } c ? $", {ShowpieceFormat.Percent(c, 1, signed: true)} on the previous week" : string.Empty;
            var peak = _summary.Peak;
            return Format(ChartSummaryFormat, LabelOf(metric), $"{FormatValue(_summary.Total)} {UnitOf(metric)}".TrimEnd(), change,
                $"{DayNameList[peak.Day]} {HourText(peak.Hour)}", DayNameList[_summary.BusiestDay]);
        }
    }

    private void Announce(string text) => _liveRegion?.Announce(text);

    // Card ---------------------------------------------------------------------------------------------------------------

    private ActivityTerrainCardInfo? BuildCardInfo(ActivityTerrainCell cell)
    {
        if (_metric is null || !cell.IsValid)
        {
            return null;
        }
        var value = _summary.ValueAt(cell);
        var previous = _summary.PreviousAt(cell);
        var change = _summary.ChangeAt(cell);
        var dayValues = new double[ActivityTerrainMath.Hours];
        Array.Copy(_summary.Values, cell.Day * ActivityTerrainMath.Hours, dayValues, 0, ActivityTerrainMath.Hours);
        return new ActivityTerrainCardInfo(
            DayNameList[cell.Day],
            HourRange(cell.Hour),
            FormatValue(value),
            UnitOf(_metric),
            previous is null ? null : PreviousWeekText,
            previous is { } p ? FormatValue(p) : null,
            change is null ? null : ChangeText,
            change is { } c ? ShowpieceFormat.Percent(c, 1, signed: true) : null,
            ToneOf(change),
            Format(ShareFormat, DayAbbreviationList[cell.Day]),
            ShowpieceFormat.Percent(_summary.ShareOfDay(cell), 1),
            dayValues,
            cell.Hour,
            _metric.Tone);
    }

    private void UpdateCard()
    {
        if (_card is null)
        {
            return;
        }
        var info = HighlightedCell is { } cell ? BuildCardInfo(cell) : null;
        if (info is not null)
        {
            _cardView.Update(info);
        }
        var open = info is not null && _canvas is { IsDragging: false } && _canvas.CardAnchor is not null;
        if (open)
        {
            _card.ShowAt(_canvas!.CardAnchor!.Value);
        }
        else
        {
            _card.IsOpen = false;
        }
    }

    internal void OnCanvasAnchorChanged(Point? anchor)
    {
        if (_card is null)
        {
            return;
        }
        if (anchor is { } point)
        {
            if (_card.IsOpen)
            {
                _card.Anchor = point;
            }
            else if (HighlightedCell is not null)
            {
                UpdateCard();
            }
        }
        else if (_card.IsOpen)
        {
            _card.IsOpen = false;
        }
    }

    // Camera -------------------------------------------------------------------------------------------------------------

    internal void OnCanvasCameraChanged() => UpdateCamera();

    private void UpdateCamera()
    {
        if (_canvas is null)
        {
            return;
        }
        Yaw = _canvas.YawDegrees;
        Pitch = _canvas.PitchDegrees;
        Zoom = _canvas.OrbitCamera.Zoom;
        IsViewDefault = FocusedCell is null && _canvas.IsCameraAtRest;
        if (_resetPresence is not null)
        {
            _resetPresence.IsPresent = !IsViewDefault;
        }
    }

    private void OnViewModeChanged(ActivityTerrainViewMode mode)
    {
        PseudoClasses.Set(TopViewPseudoClass, mode == ActivityTerrainViewMode.Top);
        if (_viewSwitch is not null && _viewSwitch.SelectedIndex != (int)mode)
        {
            _syncing = true;
            try
            {
                _viewSwitch.SelectedIndex = (int)mode;
            }
            finally
            {
                _syncing = false;
            }
        }
        if (_canvas is not null)
        {
            _canvas.SetViewMode(mode, animate: IsLoaded);
            if (FocusedCell is { } focused)
            {
                // The flown-in target follows the bar's new height (flat in the heat map).
                _canvas.FlyTo(focused);
            }
        }
        UpdateCamera();
        Announce(mode == ActivityTerrainViewMode.Top ? TopViewAnnouncementText : PerspectiveAnnouncementText);
        RaiseEvent(new ActivityTerrainViewModeChangedEventArgs(ViewModeChangedEvent, mode));
    }

    // Highlight and focus -----------------------------------------------------------------------------------------------

    private void OnHighlightedChanged(ActivityTerrainCell? cell)
    {
        if (cell is { IsValid: false })
        {
            SetCurrentValue(HighlightedCellProperty, null);
            return;
        }
        _canvas?.SetHighlighted(cell);
        UpdateCard();
        if (cell is { } c && _metric is not null)
        {
            var value = FormatValue(_summary.ValueAt(c));
            var change = _summary.ChangeAt(c);
            Announce(change is { } delta
                ? Format(ChangeAnnouncementFormat, DayNameList[c.Day], HourRange(c.Hour), value, UnitOf(_metric),
                    ShowpieceFormat.Percent(delta, 1, signed: true))
                : Format(HoverAnnouncementFormat, DayNameList[c.Day], HourRange(c.Hour), value, UnitOf(_metric)));
        }
        RaiseEvent(new ActivityTerrainCellEventArgs(CellHoveredEvent, cell, cell is { } h ? _summary.ValueAt(h) : null));
    }

    private void OnFocusedChanged(ActivityTerrainCell? cell)
    {
        if (cell is { IsValid: false })
        {
            SetCurrentValue(FocusedCellProperty, null);
            return;
        }
        _canvas?.SetFocused(cell);
        if (cell is { } c)
        {
            _canvas?.FlyTo(c);
            Announce(Format(FocusAnnouncementFormat, DayNameList[c.Day], HourRange(c.Hour)));
        }
        else if (!_resetting)
        {
            _canvas?.ShowWeek();
            Announce(WeekAnnouncementText);
        }
        foreach (var button in _peakButtons)
        {
            button.IsChecked = cell == button.Cell;
        }
        UpdateCamera();
        RaiseEvent(new ActivityTerrainCellEventArgs(CellFocusedEvent, cell, cell is { } f ? _summary.ValueAt(f) : null));
    }

    // Public commands ------------------------------------------------------------------------------------------------

    /// <summary>Flies the camera to an hour (zoom at least 1.5); the same hour again keeps the camera where it is.</summary>
    public void FlyTo(ActivityTerrainCell cell)
    {
        if (cell.IsValid)
        {
            SetCurrentValue(FocusedCellProperty, cell);
        }
    }

    /// <summary>Brings the camera back to the whole week (zoom 1, default target); the turn and tilt stay.</summary>
    public void ShowWeek() => SetCurrentValue(FocusedCellProperty, null);

    /// <summary>Brings turn, tilt, zoom and target back to the starting view and leaves the focused hour.</summary>
    public void ResetView()
    {
        _resetting = true;
        try
        {
            SetCurrentValue(FocusedCellProperty, null);
        }
        finally
        {
            _resetting = false;
        }
        _canvas?.ResetCamera();
        UpdateCamera();
        Announce(ResetAnnouncementText);
        RaiseEvent(new RoutedEventArgs(ViewResetEvent));
    }

    /// <summary>Switches between the terrain and the heat map.</summary>
    public void SetViewMode(ActivityTerrainViewMode mode) => SetCurrentValue(ViewModeProperty, mode);

    /// <summary>Plays the first reveal again: the camera swings down from far above while the bars grow back to front.</summary>
    public void ReplayIntro() => _canvas?.ReplayIntro();

    private void OnResetClick(object? sender, RoutedEventArgs e) => ResetView();

    // Input from the plot ---------------------------------------------------------------------------------------------

    internal void OnCanvasHover(ActivityTerrainCell? cell)
    {
        if (cell != HighlightedCell)
        {
            SetCurrentValue(HighlightedCellProperty, cell);
        }
    }

    internal void OnCanvasTap(ActivityTerrainCell? cell)
    {
        if (cell is { } c)
        {
            if (c != HighlightedCell)
            {
                SetCurrentValue(HighlightedCellProperty, c);
            }
            FlyTo(c);
        }
        else if (FocusedCell is not null)
        {
            ShowWeek();
        }
    }

    /// <summary>The plot's keyboard model (spec 7); false leaves the key to the stage (or the page).</summary>
    internal bool HandleCanvasKey(KeyEventArgs e)
    {
        if (e.KeyModifiers.HasFlag(KeyModifiers.Control) || e.KeyModifiers.HasFlag(KeyModifiers.Meta) ||
            e.KeyModifiers.HasFlag(KeyModifiers.Alt) || _canvas is null)
        {
            return false;
        }
        var shift = e.KeyModifiers.HasFlag(KeyModifiers.Shift);
        switch (e.Key)
        {
            case Key.OemPlus:
            case Key.Add:
                _canvas.ZoomCamera(ActivityTerrainMath.KeyZoomFactor);
                return true;
            case Key.OemMinus:
            case Key.Subtract:
                _canvas.ZoomCamera(1 / ActivityTerrainMath.KeyZoomFactor);
                return true;
            case Key.Left:
            case Key.Right:
            case Key.Up:
            case Key.Down:
                if (shift)
                {
                    if (ViewMode == ActivityTerrainViewMode.Perspective)
                    {
                        Orbit(e.Key);
                    }
                    return true;
                }
                MoveHighlight(e.Key);
                return true;
            case Key.Home:
            case Key.End:
            {
                if (_metric is null)
                {
                    return false;
                }
                var day = (HighlightedCell ?? _summary.Peak).Day;
                SetCurrentValue(HighlightedCellProperty, new ActivityTerrainCell(day, e.Key == Key.Home ? 0 : ActivityTerrainMath.Hours - 1));
                return true;
            }
            case Key.Enter:
            case Key.Space:
                if (HighlightedCell is { } highlighted)
                {
                    FlyTo(highlighted);
                    return true;
                }
                return false;
            case Key.Escape:
                if (FocusedCell is not null)
                {
                    ShowWeek();
                    return true;
                }
                if (HighlightedCell is not null)
                {
                    SetCurrentValue(HighlightedCellProperty, null);
                    return true;
                }
                return false;
        }
        return false;
    }

    private void Orbit(Key key)
    {
        switch (key)
        {
            case Key.Left:
                _canvas!.StepYaw(-ActivityTerrainMath.KeyYawStepDegrees);
                break;
            case Key.Right:
                _canvas!.StepYaw(ActivityTerrainMath.KeyYawStepDegrees);
                break;
            case Key.Up:
                _canvas!.StepPitch(ActivityTerrainMath.KeyPitchStepDegrees);
                break;
            default:
                _canvas!.StepPitch(-ActivityTerrainMath.KeyPitchStepDegrees);
                break;
        }
    }

    private void MoveHighlight(Key key)
    {
        if (_metric is null)
        {
            return;
        }
        if (HighlightedCell is not { } cell)
        {
            // The first arrow lands on the busiest hour without moving.
            SetCurrentValue(HighlightedCellProperty, _summary.Peak);
            return;
        }
        var day = cell.Day;
        var hour = cell.Hour;
        switch (key)
        {
            case Key.Left:
                hour = Math.Max(0, hour - 1);
                break;
            case Key.Right:
                hour = Math.Min(ActivityTerrainMath.Hours - 1, hour + 1);
                break;
            case Key.Up:
                day = Math.Max(0, day - 1);
                break;
            default:
                day = Math.Min(ActivityTerrainMath.Days - 1, day + 1);
                break;
        }
        SetCurrentValue(HighlightedCellProperty, new ActivityTerrainCell(day, hour));
    }

    // Hint ---------------------------------------------------------------------------------------------------------------

    // Seen on the way down (tunnel) so a press the plot handles still switches the hint.
    private void OnAnyPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        var touched = e.Pointer.Type == PointerType.Touch;
        if (touched != _touched)
        {
            _touched = touched;
            UpdateHint();
        }
    }

    private void UpdateHint()
    {
        if (_hint is not null)
        {
            _hint.Text = _touched ? TouchHintText : HintText;
        }
    }
}
