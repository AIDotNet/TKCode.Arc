using System.Globalization;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.VisualTree;
using TKCode.Arc.DesignTokens;
using TKCode.Arc.Primitives;

namespace TKCode.Arc;

/// <summary>The words the open chart writes (all set by the block from its properties).</summary>
internal sealed class MetricExplorerTexts
{
    public string ThisPeriod { get; init; } = "This period";

    public string PreviousPeriod { get; init; } = "Previous period";

    public string Close { get; init; } = "Close chart";

    public string TabsLabel { get; init; } = "Metric";

    public string RangeLabel { get; init; } = "Chart range";

    public string ScrubName { get; init; } = "Scrub";

    public string ScrubFormat { get; init; } = "{0}: {1}. {2}: {3}";

    public string SummaryFormat { get; init; } = "{0} over the last {1} days, from {2} to {3}.";

    public string ScrubCompareFormat { get; init; } = "{0} vs {1}";

    public string Up { get; init; } = "up";

    public string Down { get; init; } = "down";

    public string Flat { get; init; } = "unchanged";

    /// <summary>The card button's spoken name: {0} title, {1} value, {2} up / down / unchanged, {3} change, {4} the range's card text.</summary>
    public string CardNameFormat { get; init; } = "{0}: {1}, {2} {3} over {4}. Open chart";

    public string PointsUnit { get; init; } = " points";
}

/// <summary>What the open chart shows.</summary>
internal sealed class MetricExplorerState
{
    public required MetricExplorerSeries[] Series { get; init; }

    public required ExplorerRange[] Ranges { get; init; }

    public int Metric { get; init; }

    public int Range { get; init; }

    public bool ShowPrevious { get; init; } = true;

    public required MetricExplorerTexts Texts { get; init; }
}

/// <summary>
/// The open chart's content: the tab strip and close button, the readout (metric name, big number, change with rolling
/// digits and its comparison), the range switch, the legend with the date span, the chart and the one-line description.
/// Every line of text swaps with the measured rise / blur; the readout switches to the inspected day while the chart is
/// scrubbed. Lays itself out in three forms: wide, narrow (under 640) and compact (440 and under).
/// </summary>
internal sealed class MetricExplorerPanel : Panel
{
    private readonly MetricExplorerTabs _tabs = new();
    private readonly MetricExplorerCloseButton _close = new();
    private readonly AnalyticsTextSwap _title = new();
    private readonly AnalyticsTextSwap _value = new();
    private readonly StackPanel _deltaRow = new() { Orientation = Orientation.Horizontal };
    private readonly AnalyticsDelta _delta = new();
    private readonly AnalyticsRollingNumber _change = new();
    private readonly AnalyticsTextSwap _compare = new();
    private readonly SegmentedControl _ranges = new();
    private readonly MetricExplorerLegendKey _key = new();
    private readonly AnalyticsTextSwap _span = new();
    private readonly MetricExplorerChart _chart = new();
    private readonly ArcContentSwap _description = new();
    private MetricExplorerGeometry _geometry = new();
    private MetricExplorerState? _state;
    private bool _syncing;
    private bool _hasState;
    private int _lastMetric = -1;
    private int _lastRange = -1;
    private bool _compact;
    private bool _stacked;
    private double _width;

    static MetricExplorerPanel()
    {
        FocusableProperty.OverrideDefaultValue<MetricExplorerPanel>(false);
    }

    public MetricExplorerPanel()
    {
        _title.FontSize = 14;
        _title.LineHeight = 15.4;
        _value.FontSize = 28;
        _value.LineHeight = 33.6;
        _value.FontWeight = FontWeight.Medium;
        _value.IsTabular = true;
        _value.Swap = AnalyticsSwapStyle.RiseLate;
        _title.Swap = AnalyticsSwapStyle.RiseLate;
        _compare.Swap = AnalyticsSwapStyle.RiseLate;
        _compare.FontSize = 14;
        _compare.LineHeight = 19.6;
        _compare.IsTabular = true;
        _span.FontSize = 12;
        _span.LineHeight = 16.8;
        _span.IsTabular = true;
        _span.Swap = AnalyticsSwapStyle.RiseLate;
        _span.TextAlignment = TextAlignment.Right;
        _delta.IconSize = 14;
        _delta.IconStroke = 1.75;
        _delta.IsArrowRotating = true;
        _change.FontSize = 14;
        _change.FontWeight = FontWeight.Medium;
        _change.Bind(TemplatedControl.ForegroundProperty, _delta.GetObservable(TemplatedControl.ForegroundProperty));
        _compare.Margin = new Thickness(8, 0, 0, 0);
        _delta.VerticalAlignment = VerticalAlignment.Center;
        _change.VerticalAlignment = VerticalAlignment.Center;
        _compare.VerticalAlignment = VerticalAlignment.Center;
        _deltaRow.Children.Add(_delta);
        _deltaRow.Children.Add(_change);
        _deltaRow.Children.Add(_compare);
        _description.Mode = ContentSwapMode.BlurSwap;
        _description.HorizontalContentAlignment = HorizontalAlignment.Left;
        _description.VerticalContentAlignment = VerticalAlignment.Top;
        _description.MeasuresExiting = false;
        AnalyticsSegmented.SetSpring(_ranges, TKCode.Arc.Motion.ArcSpring.Smooth);
        _ranges.HorizontalAlignment = HorizontalAlignment.Left;

        foreach (var child in new Control[] { _tabs, _close, _title, _value, _deltaRow, _ranges, _key, _span, _chart, _description })
        {
            Children.Add(child);
        }

        _tabs.SelectionChanged += i => MetricChosen?.Invoke(i);
        _close.Invoked += () => CloseRequested?.Invoke();
        _ranges.SelectionChanged += OnRangeSwitchChanged;
        _chart.ScrubChanged += OnChartScrub;
        AttachedToVisualTree += (_, _) => BindTokens();
    }

    /// <summary>Raised when the person picks a metric tab.</summary>
    public event Action<int>? MetricChosen;

    /// <summary>Raised when the person picks a range.</summary>
    public event Action<int>? RangeChosen;

    /// <summary>Raised by the close button and by Escape.</summary>
    public event Action? CloseRequested;

    /// <summary>Raised when the inspected day changes (null: cleared).</summary>
    public event Action<int?>? ScrubChanged;

    /// <summary>The chart (tests).</summary>
    internal MetricExplorerChart Chart => _chart;

    /// <summary>The tab strip (tests).</summary>
    internal MetricExplorerTabs Tabs => _tabs;

    /// <summary>The close button (tests).</summary>
    internal MetricExplorerCloseButton CloseButton => _close;

    /// <summary>The range switch (tests).</summary>
    internal SegmentedControl RangeSwitch => _ranges;

    /// <summary>The readout parts (tests).</summary>
    internal (AnalyticsTextSwap Title, AnalyticsTextSwap Value, AnalyticsDelta Delta, AnalyticsRollingNumber Change, AnalyticsTextSwap Compare, AnalyticsTextSwap Span) Readout =>
        (_title, _value, _delta, _change, _compare, _span);

    /// <summary>The description swap (tests).</summary>
    internal ArcContentSwap Description => _description;

    /// <summary>The shared geometry.</summary>
    public MetricExplorerGeometry Geometry
    {
        get => _geometry;
        set
        {
            _geometry = value;
            _chart.Geometry = value;
            InvalidateMeasure();
        }
    }

    // Colours and fonts follow the theme through the token resources (bound once the panel is attached).
    private void BindTokens()
    {
        ArcTokens.Bind(_title, AnalyticsTextSwap.FontFamilyProperty, ArcDesignTokenKind.DisplayFontFamily);
        ArcTokens.Bind(_title, AnalyticsTextSwap.ForegroundProperty, ArcDesignTokenKind.TextSecondary);
        ArcTokens.Bind(_value, AnalyticsTextSwap.FontFamilyProperty, ArcDesignTokenKind.BodyFontFamily);
        ArcTokens.Bind(_value, AnalyticsTextSwap.ForegroundProperty, ArcDesignTokenKind.Foreground);
        ArcTokens.Bind(_compare, AnalyticsTextSwap.FontFamilyProperty, ArcDesignTokenKind.BodyFontFamily);
        ArcTokens.Bind(_compare, AnalyticsTextSwap.ForegroundProperty, ArcDesignTokenKind.TextMuted);
        ArcTokens.Bind(_span, AnalyticsTextSwap.FontFamilyProperty, ArcDesignTokenKind.BodyFontFamily);
        ArcTokens.Bind(_span, AnalyticsTextSwap.ForegroundProperty, ArcDesignTokenKind.TextMuted);
        ArcTokens.Bind(_change, AnalyticsRollingNumber.FontFamilyProperty, ArcDesignTokenKind.BodyFontFamily);
    }

    #region State

    /// <summary>Shows <paramref name="state"/>; <paramref name="animate"/> lets the readout, the chart and the switches animate.</summary>
    public void Apply(MetricExplorerState state, bool animate)
    {
        var first = !_hasState;
        var metricChanged = state.Metric != _lastMetric;
        var rangeChanged = state.Range != _lastRange;
        _state = state;
        _hasState = true;
        animate &= !first;
        if (state.Series.Length == 0 || state.Ranges.Length == 0)
        {
            return;
        }
        var series = state.Series[Math.Clamp(state.Metric, 0, state.Series.Length - 1)];
        var range = state.Ranges[Math.Clamp(state.Range, 0, state.Ranges.Length - 1)];
        var texts = state.Texts;

        _syncing = true;
        var names = state.Series.Select(s => s.TabTitle).ToArray();
        var values = state.Series.Select(s => s.ValueText(s.Latest)).ToArray();
        var tabKey = string.Join("\u0001", names) + "\u0002" + texts.TabsLabel;
        var valueKey = string.Join("\u0001", values);
        if (tabKey != _tabKey)
        {
            _tabs.SetItems(names, values, state.Metric, texts.TabsLabel);
            _tabKey = tabKey;
            _valueKey = valueKey;
        }
        else
        {
            if (valueKey != _valueKey)
            {
                _tabs.SetValues(values);
                _valueKey = valueKey;
            }
            _tabs.Select(state.Metric);
        }
        var rangeKey = string.Join("\u0001", state.Ranges.Select(r => r.Label));
        if (rangeKey != _rangeKey)
        {
            _ranges.ItemsSource = state.Ranges.Select((r, i) => new SegmentedOption(i, r.Label)).ToList();
            _rangeKey = rangeKey;
        }
        var rangeIndex = Math.Clamp(state.Range, 0, state.Ranges.Length - 1);
        if (_ranges.SelectedIndex != rangeIndex)
        {
            _ranges.SelectedIndex = rangeIndex;
        }
        AutomationProperties.SetName(_ranges, texts.RangeLabel);
        AutomationProperties.SetName(_close, texts.Close);
        _syncing = false;

        _key.SetContent(texts.ThisPeriod, texts.PreviousPeriod, state.ShowPrevious);
        _chart.SetShowPrevious(state.ShowPrevious);
        _chart.SetTexts(texts.ScrubName, texts.ScrubFormat, texts.SummaryFormat);
        _chart.SetData(series, range.Days, animate && (metricChanged || rangeChanged));
        AutomationProperties.SetName(_tabs, texts.TabsLabel);

        _title.Text = series.Title;
        _span.Text = MetricExplorerMath.RangeText(series.End, range.Days);
        SetDescription(series.Description);
        RefreshReadout();
        _lastMetric = state.Metric;
        _lastRange = state.Range;
        InvalidateMeasure();
    }

    /// <summary>Selects tab <paramref name="index"/> without an event (the tab glides).</summary>
    public void SelectTab(int index) => _tabs.Select(index);

    /// <summary>Moves keyboard focus to the selected tab.</summary>
    public bool FocusTabs() => _tabs.Focus(NavigationMethod.Tab);

    /// <summary>Inspects day <paramref name="index"/> (null clears).</summary>
    public void SetScrub(int? index) => _chart.SetScrub(index, notify: false);

    private void SetDescription(string? text)
    {
        if (string.IsNullOrEmpty(text))
        {
            _description.Content = null;
            return;
        }
        if (_description.Content is TextBlock current && current.Text == text)
        {
            return;
        }
        var block = new TextBlock { Text = text, FontSize = 12, LineHeight = 16.8, TextWrapping = TextWrapping.Wrap };
        ArcTokens.Bind(block, TextBlock.ForegroundProperty, ArcDesignTokenKind.TextMuted);
        ArcTokens.Bind(block, TextBlock.FontFamilyProperty, ArcDesignTokenKind.BodyFontFamily);
        AutomationProperties.SetAccessibilityView(block, AccessibilityView.Content);
        _description.Content = block;
    }

    private void OnRangeSwitchChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_syncing || _ranges.SelectedIndex < 0)
        {
            return;
        }
        RangeChosen?.Invoke(_ranges.SelectedIndex);
    }

    private void OnChartScrub(int? index)
    {
        RefreshReadout();
        ScrubChanged?.Invoke(index);
    }

    /// <summary>The readout: the range summary, or the inspected day against the day one period earlier.</summary>
    private void RefreshReadout()
    {
        if (_state is not { } state || state.Series.Length == 0)
        {
            return;
        }
        var series = state.Series[Math.Clamp(state.Metric, 0, state.Series.Length - 1)];
        var range = state.Ranges[Math.Clamp(state.Range, 0, state.Ranges.Length - 1)];
        var days = range.Days;
        double now;
        double before;
        string compare;
        if (_chart.ScrubIndex is { } index)
        {
            var k = days - 1 - index;
            now = series.At(k);
            before = series.At(k + days);
            compare = string.Format(CultureInfo.CurrentCulture, state.Texts.ScrubCompareFormat,
                series.DateAt(k).ToString("MMM d", AnalyticsFormat.DefaultCulture),
                series.DateAt(k + days).ToString("MMM d", AnalyticsFormat.DefaultCulture));
        }
        else
        {
            now = series.Latest;
            before = series.At(days);
            compare = range.CompareText;
        }
        // The big number changes without travelling while the person scrubs; metric and range switches rise.
        var instant = _chart.ScrubIndex is not null || _wasScrubbing;
        _wasScrubbing = _chart.ScrubIndex is not null;
        SetText(_value, series.ValueText(now), instant);
        SetText(_compare, compare, instant);
        var change = series.Change(now, before);
        var (direction, tone) = series.Trend(change);
        _delta.Direction = direction;
        _delta.Tone = tone;
        _delta.UpText = state.Texts.Up;
        _delta.DownText = state.Texts.Down;
        _delta.FlatText = state.Texts.Flat;
        _change.Format = MetricExplorerMath.ChangeFormat(series.ChangeMode);
        _change.Amount = double.IsFinite(change) ? change : 0;
        _change.IsVisible = double.IsFinite(change);
        AutomationProperties.SetName(_deltaRow, $"{_delta.SpokenText}{(double.IsFinite(change) ? " " + _change.FullText : string.Empty)} {compare}");
        AutomationProperties.SetName(_value, series.ValueText(now));
    }

    private bool _wasScrubbing;
    private string _tabKey = string.Empty;
    private string _valueKey = string.Empty;
    private string _rangeKey = string.Empty;

    private static void SetText(AnalyticsTextSwap swap, string text, bool instant)
    {
        if (swap.Text == text)
        {
            return;
        }
        if (instant)
        {
            var style = swap.Swap;
            swap.Swap = AnalyticsSwapStyle.Instant;
            swap.Text = text;
            swap.Swap = style;
        }
        else
        {
            swap.Text = text;
        }
    }

    #endregion

    #region Layout

    // Row heights measured on the wide layout: title 15.4, value 33.6, change 19.6, gaps 4.
    private const double TitleLine = 15.4;
    private const double ChangeLine = 19.6;

    protected override Size MeasureOverride(Size availableSize)
    {
        var width = double.IsInfinity(availableSize.Width) ? 760 : availableSize.Width;
        _width = width;
        var g = _geometry;
        var pad = g.PaddingFor(width);
        _compact = g.IsCompact(width);
        _stacked = g.IsNarrow(width);
        var inner = Math.Max(0, width - pad.Left - pad.Right);
        _tabs.ShowValues = !_stacked;
        _tabs.IsStretched = _compact;
        _value.FontSize = _compact ? 22 : 28;
        _value.LineHeight = _compact ? 26.4 : 33.6;

        var y = pad.Top;
        var closeSize = new Size(32, 32);
        _close.Measure(closeSize);
        var tabWidth = _compact ? inner : Math.Max(0, inner - 32 - 12);
        _tabs.Measure(new Size(tabWidth, double.PositiveInfinity));
        y += _compact ? _tabs.DesiredSize.Height : g.TabRowHeight;
        y += g.RowGap;

        _title.Measure(new Size(inner, double.PositiveInfinity));
        _value.Measure(new Size(inner, double.PositiveInfinity));
        _deltaRow.Measure(new Size(inner, double.PositiveInfinity));
        _ranges.Measure(new Size(inner, double.PositiveInfinity));
        var left = TitleLine + 4 + _value.LineHeight + 4 + ChangeLine;
        var readout = _stacked ? left + g.StackedGap + _ranges.DesiredSize.Height : Math.Max(left, _ranges.DesiredSize.Height);
        y += readout + g.LegendTopGap;

        _key.Measure(new Size(inner, double.PositiveInfinity));
        _span.Measure(new Size(inner, double.PositiveInfinity));
        y += LegendRowCount(inner) == 1 ? 16.8 : 16.8 + 4 + 16.8;
        y += g.LegendBottomGap;

        _chart.Measure(new Size(inner, double.PositiveInfinity));
        y += _chart.DesiredSize.Height;

        var description = _description.Content is null ? 0 : g.DescriptionGap;
        _description.Measure(new Size(inner, double.PositiveInfinity));
        y += description + (_description.Content is null ? 0 : _description.DesiredSize.Height);
        y += pad.Bottom;
        return new Size(width, Math.Ceiling(y * 10) / 10);
    }

    private int LegendRowCount(double inner) => _key.DesiredSize.Width + 16 + SpanWidth() <= inner ? 1 : 2;

    private double SpanWidth() => _span.DesiredSize.Width;

    protected override Size ArrangeOverride(Size finalSize)
    {
        var g = _geometry;
        var width = finalSize.Width;
        var pad = g.PaddingFor(width);
        var inner = Math.Max(0, width - pad.Left - pad.Right);
        var x = pad.Left;
        var y = pad.Top;

        double tabsHeight;
        if (_compact)
        {
            tabsHeight = _tabs.DesiredSize.Height;
            _tabs.Arrange(new Rect(x, y, inner, tabsHeight));
        }
        else
        {
            tabsHeight = g.TabRowHeight;
            _tabs.Arrange(new Rect(x, y, Math.Max(0, inner - 32 - 12), tabsHeight));
            _close.Arrange(new Rect(x + inner - 32, y, 32, 32));
        }
        y += tabsHeight + g.RowGap;

        var left = TitleLine + 4 + _value.LineHeight + 4 + ChangeLine;
        _title.Arrange(new Rect(x, y, inner, TitleLine));
        _value.Arrange(new Rect(x, y + TitleLine + 4, inner, _value.LineHeight));
        _deltaRow.Arrange(new Rect(x, y + TitleLine + 4 + _value.LineHeight + 4, inner, ChangeLine));
        var rangeSize = _ranges.DesiredSize;
        if (_compact)
        {
            // The close button sits on the readout's first line, pulled out over the padding (−8 / −6).
            _close.Arrange(new Rect(x + inner + 6 - 32 + 0, y - 8, 32, 32));
        }
        double readout;
        if (_stacked)
        {
            _ranges.Arrange(new Rect(x, y + left + g.StackedGap, rangeSize.Width, rangeSize.Height));
            readout = left + g.StackedGap + rangeSize.Height;
        }
        else
        {
            _ranges.Arrange(new Rect(x + inner - rangeSize.Width, y, rangeSize.Width, rangeSize.Height));
            readout = Math.Max(left, rangeSize.Height);
        }
        y += readout + g.LegendTopGap;

        var keySize = _key.DesiredSize;
        var spanWidth = _span.DesiredSize.Width;
        if (LegendRowCount(inner) == 1)
        {
            _key.Arrange(new Rect(x, y, keySize.Width, 16.8));
            _span.Arrange(new Rect(x + inner - Math.Max(spanWidth, 0), y, Math.Max(spanWidth, 0), 16.8));
            y += 16.8;
        }
        else
        {
            _key.Arrange(new Rect(x, y, keySize.Width, 16.8));
            y += 16.8 + 4;
            _span.Arrange(new Rect(x + inner - spanWidth, y, spanWidth, 16.8));
            y += 16.8;
        }
        y += g.LegendBottomGap;

        var chartHeight = _chart.DesiredSize.Height;
        _chart.Arrange(new Rect(x, y, inner, chartHeight));
        y += chartHeight;

        if (_description.Content is not null)
        {
            y += g.DescriptionGap;
            _description.Arrange(new Rect(x, y, inner, _description.DesiredSize.Height));
        }
        else
        {
            _description.Arrange(new Rect(x, y, inner, 0));
        }
        return finalSize;
    }

    #endregion

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        // Escape reaches here only when the chart had nothing to clear: it closes the chart.
        if (!e.Handled && e.Key == Key.Escape && e.KeyModifiers == KeyModifiers.None)
        {
            e.Handled = true;
            CloseRequested?.Invoke();
        }
    }
}
