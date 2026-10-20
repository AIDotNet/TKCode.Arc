using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>
/// Arc's bar chart: one measure across a run of days, weeks or months, with an average reference line and a
/// headline that reads the average at rest and the scrubbed bar while exploring. Bars grow in a left-to-right
/// wave the first time the chart is seen; a new range zooms the slots around the latest bar while bars that
/// keep their key morph, new ones grow beside them and leaving ones sink into the baseline.
/// <para>
/// Motion is code-driven (<see cref="MotionValue{T}"/> per bar) because bar heights, slots and the scale are
/// coordinates inside the self-drawn plot, which ControlTheme transitions cannot reach.
/// </para>
/// </summary>
/// <example><code>&lt;arc:BarChart Label="Active minutes" Period="Sep 15–21" Unit="min" ItemsSource="{Binding Week}" /&gt;</code></example>
[Avalonia.Controls.Metadata.TemplatePart(KindPart, typeof(ArcContentSwap))]
[Avalonia.Controls.Metadata.TemplatePart(ValuePart, typeof(RollingDigits))]
[Avalonia.Controls.Metadata.TemplatePart(ValueTextPart, typeof(ArcContentSwap))]
[Avalonia.Controls.Metadata.TemplatePart(WhenPart, typeof(ArcContentSwap))]
public class BarChart : ChartBase
{
    public const string KindPart = "PART_Kind";
    public const string ValuePart = "PART_Value";
    public const string ValueTextPart = "PART_ValueText";
    public const string WhenPart = "PART_When";

    private const double Top = 12;
    private const double Fill = 0.58;
    private static readonly SpringSpec Settle = SpringSpec.FromVisual(0.4, 0);
    private static readonly SpringSpec Grow = SpringSpec.FromVisual(0.42, 0.16);
    private static readonly SpringSpec Glide = SpringSpec.FromVisual(0.26, 0.12);
    private static readonly SpringSpec Collapse = SpringSpec.FromVisual(0.24, 0);

    /// <summary>The range on show, such as "Sep 15–21, 2026". It rests under the headline.</summary>
    public static readonly StyledProperty<string> PeriodProperty =
        AvaloniaProperty.Register<BarChart, string>(nameof(Period), string.Empty);

    /// <summary>Headline label at rest, above the average.</summary>
    public static readonly StyledProperty<string> AverageLabelProperty =
        AvaloniaProperty.Register<BarChart, string>(nameof(AverageLabel), "Daily average");

    /// <summary>Headline label while a bar is scrubbed.</summary>
    public static readonly StyledProperty<string> ValueLabelProperty =
        AvaloniaProperty.Register<BarChart, string>(nameof(ValueLabel), "Total");

    /// <summary>Draws the average as a dashed reference line that springs to each new range.</summary>
    public static readonly StyledProperty<bool> ShowAverageProperty =
        AvaloniaProperty.Register<BarChart, bool>(nameof(ShowAverage), true);

    /// <summary>Widest bar in pixels; wide containers gain spacing instead of thicker bars (chart token).</summary>
    internal static readonly StyledProperty<double> MaxBarWidthProperty =
        AvaloniaProperty.Register<BarChart, double>(nameof(MaxBarWidth), 28);

    public static new readonly StyledProperty<bool> ShowGridProperty = ChartBase.ShowGridProperty.AddOwner<BarChart>();
    public static new readonly StyledProperty<bool> ShowXAxisProperty = ChartBase.ShowXAxisProperty.AddOwner<BarChart>();
    public static new readonly StyledProperty<bool> ShowYAxisProperty = ChartBase.ShowYAxisProperty.AddOwner<BarChart>();
    public static new readonly StyledProperty<double?> YMaximumProperty = ChartBase.YMaximumProperty.AddOwner<BarChart>();
    public static new readonly StyledProperty<Func<double, string>?> TickFormatterProperty = ChartBase.TickFormatterProperty.AddOwner<BarChart>();

    /// <inheritdoc cref="PeriodProperty"/>
    public string Period { get => GetValue(PeriodProperty); set => SetValue(PeriodProperty, value); }

    /// <inheritdoc cref="AverageLabelProperty"/>
    public string AverageLabel { get => GetValue(AverageLabelProperty); set => SetValue(AverageLabelProperty, value); }

    /// <inheritdoc cref="ValueLabelProperty"/>
    public string ValueLabel { get => GetValue(ValueLabelProperty); set => SetValue(ValueLabelProperty, value); }

    /// <inheritdoc cref="ShowAverageProperty"/>
    public bool ShowAverage { get => GetValue(ShowAverageProperty); set => SetValue(ShowAverageProperty, value); }

    /// <inheritdoc cref="ChartBase.ShowGridProperty"/>
    public bool ShowGrid { get => GetValue(ShowGridProperty); set => SetValue(ShowGridProperty, value); }

    /// <inheritdoc cref="ChartBase.ShowXAxisProperty"/>
    public bool ShowXAxis { get => GetValue(ShowXAxisProperty); set => SetValue(ShowXAxisProperty, value); }

    /// <inheritdoc cref="ChartBase.ShowYAxisProperty"/>
    public bool ShowYAxis { get => GetValue(ShowYAxisProperty); set => SetValue(ShowYAxisProperty, value); }

    /// <inheritdoc cref="ChartBase.YMaximumProperty"/>
    public double? YMaximum { get => GetValue(YMaximumProperty); set => SetValue(YMaximumProperty, value); }

    /// <summary>Formats value-axis ticks. Defaults to the value format (Arc formats ticks like values).</summary>
    public Func<double, string>? TickFormatter { get => GetValue(TickFormatterProperty); set => SetValue(TickFormatterProperty, value); }

    internal double MaxBarWidth => GetValue(MaxBarWidthProperty);

    /// <summary>The average of all values (the resting headline and the reference line).</summary>
    public double Average => _average;

    private sealed class Bar
    {
        public required string Key { get; init; }
        public required MotionValue<double> Amount { get; init; }
        public required MotionValue<double> Place { get; init; }
        public required MotionValue<double> Highlight { get; init; }
        public int Index { get; set; }
        public double Value { get; set; }
        public bool Leaving { get; set; }
        public bool Grown { get; set; }
        public double FirstDelay { get; set; }
        public StreamGeometry? Geometry { get; set; }
        public Rect GeometryKey { get; set; }
    }

    private readonly Dictionary<string, Bar> _bars = new();
    private readonly List<Bar> _order = new();
    private ChartValueAxis? _axis;
    private MotionValue<double>? _unit;
    private MotionValue<double>? _mean;
    private MotionValue<double>? _meanShown;
    private MotionValue<double>? _cursor;
    private MotionValue<double>? _scrub;
    private double _average;
    private bool _meanStarted;
    private bool _wasScrubbing;
    private int _lastActive = -1;
    private ArcContentSwap? _kind;
    private RollingDigits? _value;
    private ArcContentSwap? _valueText;
    private ArcContentSwap? _when;
    private readonly Dictionary<string, double> _fitting = new();
    private double _fittingWidth = -1;
    private ChartData? _fittingData;

    static BarChart()
    {
        PlotHeightProperty.OverrideDefaultValue<BarChart>(176);
        CategoryLabelProperty.OverrideDefaultValue<BarChart>("Day");
        GutterWidthProperty.OverrideDefaultValue<BarChart>(52);
    }

    private protected override double RevealThreshold => 0.35;

    private protected override string DefaultValueFormat => "#,##0";

    private protected override string DefaultTick(double value) => FormatValue(value);

    private ChartValueAxis Axis => _axis ??= new ChartValueAxis(this, InvalidatePlots);

    private MotionValue<double> UnitShare => _unit ??= CreateValue(1, OnLayoutFrame);

    private MotionValue<double> Mean => _mean ??= CreateValue(0, InvalidatePlots);

    private MotionValue<double> MeanShown => _meanShown ??= CreateValue(0, InvalidatePlots);

    private MotionValue<double> Crosshair => _cursor ??= CreateValue(0, InvalidatePlots);

    private MotionValue<double> Scrub => _scrub ??= CreateValue(0, InvalidatePlots);

    private MotionValue<double> CreateValue(double initial, Action changed)
    {
        var value = new MotionValue<double>(this, initial, MotionVectors.Double);
        value.Changed += _ => changed();
        return value;
    }

    private void OnLayoutFrame() => InvalidatePlots();

    private protected override void OnTemplateApplied(INameScope scope)
    {
        _kind = scope.Find<ArcContentSwap>(KindPart);
        _value = scope.Find<RollingDigits>(ValuePart);
        _valueText = scope.Find<ArcContentSwap>(ValueTextPart);
        _when = scope.Find<ArcContentSwap>(WhenPart);
        UpdateHeadline(1, 1);
    }

    #region Data

    private protected override void OnDataChanged(ChartData previous, ChartData current)
    {
        var values = current.Series.Length > 0 ? current.Series[0].Values : [];
        var count = Math.Max(current.Count, 1);
        var last = current.Count - 1;
        double sum = 0, peak = 0;
        foreach (var value in values)
        {
            sum += value;
            peak = Math.Max(peak, value);
        }
        _average = values.Length > 0 ? sum / values.Length : 0;
        var full = IsMotionFull;
        var changed = !previous.IsEmpty || _bars.Count > 0;
        if (changed && !SameKeys(previous, current))
        {
            SetActive(null);
        }

        // Bars that join an existing chart grow outward from the ones that stayed; the first reveal runs left to right.
        var incoming = new List<int>();
        for (var i = 0; i < current.Count; i++)
        {
            if (!_bars.ContainsKey(current.Keys[i]))
            {
                incoming.Add(i);
            }
        }
        incoming.Sort((a, b) => (last - a).CompareTo(last - b));
        var freshStep = Math.Min(ArcMotionPresets.StaggerItem.TotalSeconds, 0.32 / Math.Max(1, incoming.Count));
        var revealStep = Math.Min(ArcMotionPresets.StaggerItem.TotalSeconds, 0.36 / count);
        var seen = new HashSet<string>(StringComparer.Ordinal);
        _order.Clear();
        for (var i = 0; i < current.Count; i++)
        {
            var key = current.Keys[i];
            seen.Add(key);
            var place = last - i;
            if (!_bars.TryGetValue(key, out var bar))
            {
                bar = new Bar
                {
                    Key = key,
                    Amount = CreateValue(0, InvalidatePlots),
                    Place = CreateValue(place, InvalidatePlots),
                    Highlight = CreateValue(0, InvalidatePlots),
                    FirstDelay = changed && IsRevealed ? incoming.IndexOf(i) * freshStep : i * revealStep
                };
                _bars[key] = bar;
            }
            else if (bar.Leaving)
            {
                bar.Leaving = false;
            }
            bar.Index = i;
            bar.Value = i < values.Length ? values[i] : 0;
            if (Math.Abs(bar.Place.Target - place) > 1e-9)
            {
                _ = bar.Place.AnimateTo(place, full ? MotionSpec.Spring(Settle) : MotionSpec.Instant);
            }
            _order.Add(bar);
            if (IsRevealed || MotionLevel != ArcMotionLevel.Full)
            {
                GrowBar(bar);
            }
        }
        var leaving = new List<Bar>();
        foreach (var bar in _bars.Values)
        {
            if (!seen.Contains(bar.Key) && !bar.Leaving)
            {
                bar.Leaving = true;
                leaving.Add(bar);
            }
        }
        foreach (var bar in leaving)
        {
            _ = SinkBar(bar);
        }

        var ticks = ChartTicks.FromZero(YMaximum ?? peak);
        Axis.SetTicks(ticks, FormatTick, changed && full);
        var unit = 1.0 / count;
        if (!changed || !full)
        {
            UnitShare.Jump(unit);
        }
        else
        {
            _ = UnitShare.AnimateTo(unit, MotionSpec.Spring(Settle));
        }
        if (_meanStarted || MotionLevel != ArcMotionLevel.Full)
        {
            _ = Mean.AnimateTo(_average, full ? MotionSpec.Spring(Settle) : MotionSpec.Instant);
        }
        _fittingData = null;
        if (changed && !previous.IsEmpty)
        {
            Announcement = $"{Period}. {AverageLabel} {FormatWithUnit(Math.Round(_average))}.";
        }
        UpdateHeadline(1, 1);
    }

    private static bool SameKeys(ChartData a, ChartData b)
    {
        if (a.Count != b.Count)
        {
            return false;
        }
        for (var i = 0; i < a.Count; i++)
        {
            if (a.Keys[i] != b.Keys[i])
            {
                return false;
            }
        }
        return true;
    }

    private void GrowBar(Bar bar)
    {
        if (MotionLevel != ArcMotionLevel.Full)
        {
            bar.Amount.Jump(bar.Value);
            bar.Grown = true;
            return;
        }
        var first = !bar.Grown;
        bar.Grown = true;
        _ = bar.Amount.AnimateTo(bar.Value, first
            ? MotionSpec.Spring(Grow, TimeSpan.FromSeconds(bar.FirstDelay))
            : MotionSpec.Spring(Settle));
    }

    /// <summary>A leaving bar sinks into the baseline, faster than a bar grows, while the zoom carries it away.</summary>
    private async Task SinkBar(Bar bar)
    {
        var done = await bar.Amount.AnimateTo(0, MotionLevel == ArcMotionLevel.Full ? MotionSpec.Spring(Collapse) : MotionSpec.Instant);
        if (bar.Leaving && (done || bar.Amount.Value <= 0.0001))
        {
            _bars.Remove(bar.Key);
            InvalidatePlots();
        }
    }

    private protected override void OnRevealed()
    {
        foreach (var bar in _order)
        {
            GrowBar(bar);
        }
        _meanStarted = true;
        var full = MotionLevel == ArcMotionLevel.Full;
        _ = Mean.AnimateTo(_average, full ? MotionSpec.Spring(Settle, TimeSpan.FromSeconds(0.12)) : MotionSpec.Instant);
        _ = MeanShown.AnimateTo(1, full
            ? MotionSpec.Tween(ArcMotionPresets.Standard, ArcEasings.Standard, TimeSpan.FromSeconds(0.12))
            : MotionSpec.Instant);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == PeriodProperty || change.Property == AverageLabelProperty || change.Property == ValueLabelProperty)
        {
            UpdateHeadline(1, 1);
            RefreshText();
        }
        else if (change.Property == ShowAverageProperty || change.Property == MaxBarWidthProperty)
        {
            InvalidatePlots();
        }
        else if (change.Property == YMaximumProperty)
        {
            OnDataChanged(Data, Data);
        }
    }

    private protected override void OnFormattingChanged()
    {
        _meanLabelValue = double.NaN;
        Axis.SetTicks(Axis.Ticks, FormatTick, false);
        UpdateHeadline(1, 1);
    }

    #endregion

    #region Headline and readout

    private void UpdateHeadline(int valueDirection, int indexDirection)
    {
        var data = Data;
        var index = ActiveIndex is { } i && i < data.Count ? i : (int?)null;
        var scrubbing = index is not null;
        var value = scrubbing ? data.Series[0].Values[index!.Value] : Math.Round(_average);
        if (_kind is not null)
        {
            _kind.Direction = 1;
            _kind.Content = scrubbing ? ValueLabel : AverageLabel;
        }
        var custom = ValueFormatter is not null || ValueFormat is not null;
        if (_value is not null)
        {
            _value.IsVisible = !custom;
            _value.Value = value;
        }
        if (_valueText is not null)
        {
            _valueText.IsVisible = custom;
            _valueText.Direction = valueDirection;
            _valueText.Content = FormatValue(value, index ?? -1);
        }
        if (_when is not null)
        {
            _when.Direction = scrubbing ? indexDirection : 1;
            _when.Content = scrubbing ? data.Labels[index!.Value] : Period;
        }
    }

    private protected override void OnActiveIndexChanged(int? previous, int? current)
    {
        var data = Data;
        var level = MotionLevel;
        var oldValue = previous is { } p && p < data.Count ? data.Series[0].Values[p] : Math.Round(_average);
        if (current is { } index && index < data.Count)
        {
            var place = data.Count - 1 - index;
            if (!_wasScrubbing || level != ArcMotionLevel.Full)
            {
                Crosshair.Jump(place);
            }
            else
            {
                _ = Crosshair.AnimateTo(place, MotionSpec.Spring(Glide));
            }
            _wasScrubbing = true;
            _ = Scrub.AnimateTo(1, Fade(ArcMotionPresets.Instant));
            var newValue = data.Series[0].Values[index];
            UpdateHeadline(newValue >= oldValue ? 1 : -1, _lastActive < 0 ? 1 : Math.Sign(index - _lastActive) is var s && s != 0 ? s : 1);
            _lastActive = index;
        }
        else
        {
            _wasScrubbing = false;
            _ = Scrub.AnimateTo(0, Fade(ArcMotionPresets.Instant));
            UpdateHeadline(Math.Round(_average) >= oldValue ? 1 : -1, 1);
            _lastActive = -1;
        }
        // The scrubbed bar takes the full accent while its neighbours step back.
        foreach (var bar in _order)
        {
            var to = current == bar.Index ? 1 : 0;
            if (Math.Abs(bar.Highlight.Target - to) > 1e-9)
            {
                _ = bar.Highlight.AnimateTo(to, Fade(ArcMotionPresets.Fast));
            }
        }
        InvalidatePlots();
    }

    private MotionSpec Fade(TimeSpan duration) => MotionLevel == ArcMotionLevel.Off
        ? MotionSpec.Instant
        : MotionSpec.Tween(duration, ArcEasings.Standard);

    private protected override string BuildSummary()
    {
        var data = Data;
        var suffix = string.IsNullOrEmpty(Unit) ? string.Empty : $" {Unit}";
        var text = $"{Label}, {Period}. {AverageLabel} {FormatValue(Math.Round(_average))}{suffix}.";
        if (data.IsEmpty)
        {
            return text;
        }
        var values = data.Series[0].Values;
        int high = 0, low = 0;
        for (var i = 1; i < values.Length; i++)
        {
            if (values[i] > values[high])
            {
                high = i;
            }
            if (values[i] < values[low])
            {
                low = i;
            }
        }
        text += $" Highest {data.Labels[high]}, {FormatValue(values[high], high)}{suffix}.";
        if (low != high)
        {
            text += $" Lowest {data.Labels[low]}, {FormatValue(values[low], low)}{suffix}.";
        }
        return text;
    }

    private protected override string BuildDataTable()
    {
        var data = Data;
        var builder = new System.Text.StringBuilder();
        builder.Append(CategoryLabel).Append('\t').Append(Label);
        for (var i = 0; i < data.Count; i++)
        {
            builder.Append('\n').Append(data.Labels[i]).Append('\t').Append(FormatWithUnit(data.Series[0].Values[i], i));
        }
        return builder.ToString();
    }

    internal override string GetPlotValueText(ChartPlot plot)
    {
        var data = Data;
        if (data.IsEmpty)
        {
            return "No data";
        }
        var index = ActiveIndex is { } i && i < data.Count ? i : data.Count - 1;
        return $"{data.Labels[index]}: {FormatWithUnit(data.Series[0].Values[index], index)}";
    }

    #endregion

    #region Input

    private double PlotWidth(double width) => Math.Max(0, width - (ShowYAxis ? GutterWidth : 0));

    internal override double MeasurePlotHeight(ChartPlot plot, double width) => PlotHeight + (ShowXAxis ? AxisRowHeight : 0);

    private int PointAt(ChartPlot plot, double x)
    {
        var data = Data;
        var last = data.Count - 1;
        var width = PlotWidth(plot.Bounds.Width);
        if (width <= 0)
        {
            return last;
        }
        var count = Math.Max(1, data.Count);
        var place = (int)Math.Floor((width - x) / width * count);
        return Math.Clamp(last - place, 0, last);
    }

    internal override void OnPlotPointerPressed(ChartPlot plot, PointerPressedEventArgs e)
    {
        if (Data.IsEmpty)
        {
            return;
        }
        if (e.Pointer.Type != PointerType.Mouse)
        {
            e.Pointer.Capture(plot);
        }
        SetActive(PointAt(plot, e.GetPosition(plot).X));
    }

    internal override void OnPlotPointerMoved(ChartPlot plot, PointerEventArgs e)
    {
        if (Data.IsEmpty)
        {
            return;
        }
        if (e.Pointer.Type == PointerType.Mouse || e.GetCurrentPoint(plot).Properties.IsLeftButtonPressed)
        {
            SetActive(PointAt(plot, e.GetPosition(plot).X));
        }
    }

    internal override void OnPlotPointerReleased(ChartPlot plot, PointerReleasedEventArgs e)
    {
        if (e.Pointer.Type != PointerType.Mouse)
        {
            e.Pointer.Capture(null);
            SetActive(null);
        }
    }

    internal override void OnPlotPointerExited(ChartPlot plot, PointerEventArgs e)
    {
        if (e.Pointer.Type == PointerType.Mouse)
        {
            SetActive(null);
        }
    }

    internal override void OnPlotPointerCaptureLost(ChartPlot plot, PointerCaptureLostEventArgs e)
    {
        if (e.Pointer.Type != PointerType.Mouse)
        {
            SetActive(null);
        }
    }

    internal override void OnPlotKeyDown(ChartPlot plot, KeyEventArgs e)
    {
        var data = Data;
        if (data.IsEmpty)
        {
            return;
        }
        if (e.Key == Key.Escape)
        {
            if (ActiveIndex is not null)
            {
                e.Handled = true;
                SetActive(null);
            }
            return;
        }
        if (ChartKeyboard.TryStep(e.Key, ActiveIndex, 0, data.Count - 1, Math.Min(7, data.Count), true, out var next))
        {
            e.Handled = true;
            SetActive(next);
        }
    }

    internal override void OnPlotFocusChanged(ChartPlot plot, bool focused, bool keyboard)
    {
        if (!focused)
        {
            SetActive(null);
        }
        else if (keyboard && !Data.IsEmpty)
        {
            SetActive(ActiveIndex ?? Data.Count - 1);
        }
        plot.InvalidateVisual();
    }

    #endregion

    #region Rendering

    private double _meanLabelValue = double.NaN;
    private string _meanLabel = string.Empty;

    /// <summary>"Avg 1,234", formatted only when the rounded average on screen changes.</summary>
    private string MeanLabel(double value)
    {
        if (value != _meanLabelValue)
        {
            _meanLabelValue = value;
            _meanLabel = $"Avg {FormatValue(value)}";
        }
        return _meanLabel;
    }

    private double CenterOf(double width, double place) => width - (place + 0.5) * width * UnitShare.Value;

    private protected override void RenderPlot(ChartPlot plot, DrawingContext context, Size size)
    {
        var palette = Palette;
        var data = Data;
        var width = PlotWidth(size.Width);
        var height = PlotHeight;
        if (width <= 0)
        {
            return;
        }
        var axis = Axis;
        var showMean = ShowAverage && !data.IsEmpty;
        var meanY = axis.Row(Mean.Value, Top, height);
        var meanOpacity = MeanShown.Value;
        axis.Render(context, palette, Texts, new Rect(0, 0, width, height), Top, ShowGrid, ShowYAxis, width + 10,
            showMean && meanOpacity > 0.5 ? meanY : double.NaN);

        var scrub = Scrub.Value;
        if (scrub > 0.001 && !data.IsEmpty)
        {
            var cx = Math.Round(CenterOf(width, Crosshair.Value)) + 0.5;
            using (context.PushOpacity(scrub))
            {
                context.DrawLine(palette.Pen(palette.BorderStrong, 1), new Point(cx, Top), new Point(cx, height));
            }
        }

        // Bars: a soft step of the accent; while scrubbing the rest step back and the scrubbed bar takes the full accent.
        var restShare = palette.IsDark ? 0.76 : 0.62;
        var dimShare = palette.IsDark ? 0.34 : 0.24;
        var rest = ChartPalette.Mix(palette.Accent, restShare, palette.Surface);
        var dim = ChartPalette.Mix(palette.Accent, dimShare, palette.Surface);
        var slot = width * UnitShare.Value;
        var barWidth = Math.Min(slot * Fill, MaxBarWidth);
        using (context.PushClip(new Rect(0, -1, width, height + 1)))
        {
            foreach (var bar in _bars.Values)
            {
                var amount = bar.Amount.Value;
                var top = axis.Max;
                var barHeight = top > 0 ? amount / top * (height - Top) : 0;
                if (barHeight <= 0.01 || barWidth <= 0)
                {
                    continue;
                }
                var center = CenterOf(width, bar.Place.Value);
                var key = new Rect(Math.Round(center - barWidth / 2, 2), height, Math.Round(barWidth, 2), Math.Round(barHeight, 2));
                if (bar.Geometry is null || bar.GeometryKey != key)
                {
                    bar.Geometry = ChartGeometry.Bar(key.X, key.Width, key.Height, height);
                    bar.GeometryKey = key;
                }
                var baseColor = ChartPalette.Lerp(rest, dim, ChartPalette.Step(scrub));
                var color = ChartPalette.Lerp(baseColor, palette.Accent, ChartPalette.Step(bar.Highlight.Value));
                context.DrawGeometry(palette.Brush(color), null, bar.Geometry);
            }
        }
        context.DrawLine(palette.Pen(palette.BorderStrong, 1), new Point(0, height - 0.5), new Point(width, height - 0.5));

        if (showMean && meanOpacity > 0.001)
        {
            using (context.PushOpacity(meanOpacity))
            {
                context.DrawLine(palette.Pen(palette.TextSecondary, 1, 2), new Point(0, meanY), new Point(width, meanY));
                if (ShowYAxis)
                {
                    var label = Texts.Get(palette, MeanLabel(Math.Round(Mean.Value)), palette.SizeXS, palette.Foreground, medium: true);
                    context.DrawText(label, new Point(width + 10, meanY - label.Height / 2));
                }
            }
        }

        if (ShowXAxis && !data.IsEmpty)
        {
            DrawAxisLabels(context, palette, data, width, height);
        }
        if (data.IsEmpty)
        {
            var message = Texts.Get(palette, EmptyLabel, palette.SizeSM, palette.TextMuted);
            context.DrawText(message, new Point((width - message.WidthIncludingTrailingWhitespace) / 2, (height - message.Height) / 2));
        }
    }

    /// <summary>Axis labels that fit once the range has settled: every label, else every second, third... counted back from the latest.</summary>
    private void DrawAxisLabels(DrawingContext context, ChartPalette palette, ChartData data, double width, double height)
    {
        if (!ReferenceEquals(_fittingData, data) || Math.Abs(_fittingWidth - width) > 0.5)
        {
            _fittingData = data;
            _fittingWidth = width;
            _fitting.Clear();
            var count = Math.Max(1, data.Count);
            var labels = new List<(string Key, double Size, double Natural, double Left)>();
            for (var i = data.Count - 1; i >= 0; i--)
            {
                if (data.AxisLabels[i] is not { Length: > 0 } text)
                {
                    continue;
                }
                var size = Texts.Get(palette, text, palette.SizeXS, palette.TextMuted).WidthIncludingTrailingWhitespace;
                var natural = width - (data.Count - 1 - i + 0.5) * (width / count) - size / 2;
                labels.Add((data.Keys[i], size, natural, Math.Clamp(natural, 0, Math.Max(0, width - size))));
            }
            for (var stride = 1; stride <= labels.Count; stride++)
            {
                var ok = true;
                (string Key, double Size, double Natural, double Left)? previous = null;
                for (var rank = 0; rank < labels.Count; rank += stride)
                {
                    var label = labels[rank];
                    if (previous is { } p && label.Left + label.Size + 10 > p.Left)
                    {
                        ok = false;
                        break;
                    }
                    previous = label;
                }
                if (ok || stride == labels.Count)
                {
                    for (var rank = 0; rank < labels.Count; rank += stride)
                    {
                        _fitting[labels[rank].Key] = labels[rank].Left - labels[rank].Natural;
                    }
                    break;
                }
            }
        }
        foreach (var bar in _order)
        {
            if (!_fitting.TryGetValue(bar.Key, out var shift) || data.AxisLabels[bar.Index] is not { Length: > 0 } text)
            {
                continue;
            }
            var label = Texts.Get(palette, text, palette.SizeXS, palette.TextMuted);
            var x = CenterOf(width, bar.Place.Value) - label.WidthIncludingTrailingWhitespace / 2 + shift;
            context.DrawText(label, new Point(Math.Round(x), height + 8));
        }
    }

    #endregion

    internal override AutomationControlType GetPlotControlType(ChartPlot plot) => AutomationControlType.Slider;
}
