using TKCode.Arc.Motion;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.TextFormatting;

namespace TKCode.Arc;

/// <summary>
/// The one surface that draws a <see cref="RaceBarChart"/>'s bars, axis and legend. It is also the chart's focusable group:
/// keys bubble to the chart, and its peer speaks the period, the leader and the reading. Text is cached as
/// <see cref="TextLayout"/> per row and rebuilt only when the displayed text changes, so a frame allocates nothing while
/// the visible digits stand still.
/// </summary>
internal sealed class RaceBarStage : Control, IChartsBAccessible, Avalonia.Rendering.ICustomHitTest
{
    private const double NameGap = 8;
    private const double ValueGap = 8;
    private const double InsidePadding = 8;
    private const double LegendRowHeight = 20;
    private const double LegendBottom = 10;
    private const double LegendItemGap = 16;
    private const double BottomPadding = 4;
    private const double TipGap = 12;
    private const int MaxTickLabels = 64;

    private readonly Dictionary<double, TextLayout> _tickLabels = new();
    private readonly List<LegendEntry> _legend = new();
    private RaceBarChart? _chart;
    private ChartsBTooltip? _tooltip;
    private ChartsBAutomationPeer? _peer;
    private RowText[] _rows = [];
    private TextLayout? _empty;
    private double _nameColumn;
    private double _valueColumn;
    private double _legendHeight;
    private double _legendWidth = -1;
    private double _textWidth = -1;
    private bool _textReady;
    private int _tipItem = -1;
    private int _tipRank = -1;
    private int _tipFrame = -1;
    private string? _tipValue;
    private double _tipBarEnd;

    private sealed class RowText
    {
        public TextLayout? Name;
        public TextLayout? NameOnBar;
        public TextLayout? Value;
        public string? ValueString;
        public long ValueKey = long.MinValue;
        public bool NameInside;
    }

    private sealed class LegendEntry
    {
        public required TextLayout Text;
        public required int Hue;
        public double X;
        public double Y;
    }

    public RaceBarStage()
    {
        FocusAdorner = null;
        ClipToBounds = false;
        AutomationProperties.SetLiveSetting(this, AutomationLiveSetting.Polite);
    }

    bool Avalonia.Rendering.ICustomHitTest.HitTest(Point point) => new Rect(Bounds.Size).Contains(point);

    internal void Attach(RaceBarChart chart)
    {
        _chart = chart;
        _tooltip = new ChartsBTooltip(this, ArcMotionPresets.Snappy, InvalidateVisual) { MinWidth = 170 };
        OnDataChanged();
    }

    private bool Narrow => Bounds.Width > 0 && Bounds.Width < (_chart?.NarrowWidth ?? 400);

    private double Pitch => (_chart?.BarHeight ?? 28) + (_chart?.BarGap ?? 6);

    private double PlotTop => _legendHeight + (_chart?.AxisHeight ?? 26);

    private double PlotLeft => Narrow ? 0 : _nameColumn;

    /// <summary>Pixels from zero to the right end of the axis.</summary>
    internal double PlotWidth => Math.Max(40, Bounds.Width - PlotLeft - _valueColumn);

    internal bool NameIsInside(int item) => item >= 0 && item < _rows.Length && _rows[item].NameInside;

    internal bool IsNarrow => Narrow;

    /// <summary>The legend's labels in order, for tests.</summary>
    internal int LegendCount => _legend.Count;

    internal double BarWidthOf(int item)
    {
        var engine = _chart!.Engine;
        var ratio = engine.Domain > 0 ? engine.Shown[item] / engine.Domain : 0;
        return Math.Max(0, Math.Min(1, ratio)) * PlotWidth;
    }

    /// <summary>The bar's color.</summary>
    internal Color ColorOf(int item) => HueColor(_chart!.Engine.Hue[item]);

    private Color HueColor(int hue)
    {
        var p = _chart!.Palette;
        return hue >= 0 ? p.Series(hue) : ChartsBMath.MixIn(p.TextMuted, 0.55, p.Surface);
    }

    // ---- change notifications -----------------------------------------------------------------------------------------

    internal void OnDataChanged()
    {
        OnTextChanged();
        InvalidateMeasure();
    }

    internal void OnPaletteChanged() => OnTextChanged();

    internal void OnTextChanged()
    {
        ReleaseText();
        SyncTooltip(true);
        InvalidateVisual();
    }

    private void ReleaseText()
    {
        _textReady = false;
        _legendWidth = -1;
        foreach (var row in _rows)
        {
            row.Name?.Dispose();
            row.NameOnBar?.Dispose();
            row.Value?.Dispose();
            row.Name = row.NameOnBar = row.Value = null;
            row.ValueString = null;
            row.ValueKey = long.MinValue;
        }
        foreach (var label in _tickLabels.Values)
        {
            label.Dispose();
        }
        _tickLabels.Clear();
        foreach (var entry in _legend)
        {
            entry.Text.Dispose();
        }
        _legend.Clear();
        _empty?.Dispose();
        _empty = null;
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        ReleaseText();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == BoundsProperty && change.GetOldValue<Rect>().Width != Bounds.Width)
        {
            _chart?.OnPlotResized();
        }
    }

    // ---- text ---------------------------------------------------------------------------------------------------------

    private static TextLayout Layout(string text, ChartsBPalette p, double size, Color color, FontWeight weight = FontWeight.Normal,
                                     double maxWidth = double.PositiveInfinity, bool tabular = false, IBrush? brush = null)
        => new(text, p.Body(weight), size, brush ?? p.Brush(color), TextAlignment.Left, TextWrapping.NoWrap,
            double.IsInfinity(maxWidth) ? null : TextTrimming.CharacterEllipsis, textDecorations: null, FlowDirection.LeftToRight,
            maxWidth, double.PositiveInfinity, double.NaN, 0, 1, tabular ? DataGridPalette.TabularNumbers : null);

    // Sizes the columns: names as wide as they need up to the token, values as wide as the largest value in the data.
    private void EnsureText(double width)
    {
        var chart = _chart!;
        var engine = chart.Engine;
        var p = chart.Palette;
        if (_rows.Length != engine.ItemCount)
        {
            ReleaseText();
            _rows = new RowText[engine.ItemCount];
            for (var i = 0; i < _rows.Length; i++)
            {
                _rows[i] = new RowText();
            }
        }
        if (_textReady && Math.Abs(width - _textWidth) <= 0.5)
        {
            return;
        }
        _textReady = true;
        _textWidth = width;
        // Names are trimmed to the width, so they are laid out again for a new one.
        foreach (var row in _rows)
        {
            row.Name?.Dispose();
            row.NameOnBar?.Dispose();
            row.Name = row.NameOnBar = null;
        }
        var widest = 0.0;
        foreach (var item in engine.Items)
        {
            using var measure = Layout(item.Label, p, p.TypeSM, p.TextSecondary);
            widest = Math.Max(widest, measure.WidthIncludingTrailingWhitespace);
        }
        _nameColumn = Math.Ceiling(Math.Min(Math.Min(chart.NameColumnWidth, width * 0.34), widest) + NameGap);
        using (var largest = Layout(chart.FormatValue(engine.LargestValue()), p, p.TypeSM, p.Foreground, FontWeight.SemiBold, tabular: true))
        {
            _valueColumn = Math.Ceiling(largest.WidthIncludingTrailingWhitespace + ValueGap + 4);
        }
    }

    private RowText RowOf(int item, ChartsBPalette p)
    {
        var chart = _chart!;
        var row = _rows[item];
        var strong = item == chart.HighlightItem;
        var color = strong ? p.Foreground : p.TextSecondary;
        var weight = strong ? FontWeight.SemiBold : FontWeight.Normal;
        if (row.Name is null)
        {
            var maxWidth = Narrow ? Math.Max(40, Bounds.Width * 0.6) : Math.Max(20, _nameColumn - NameGap);
            row.Name = Layout(chart.Engine.Items[item].Label, p, p.TypeSM, color, weight, maxWidth);
        }
        UpdateValueText(item, row, p, strong);
        return row;
    }

    private void UpdateValueText(int item, RowText row, ChartsBPalette p, bool strong)
    {
        var chart = _chart!;
        var engine = chart.Engine;
        var value = engine.Shown[item];
        // While the race moves, only the leading digits are worth reading; on a whole period the value is exact.
        var position = engine.Position;
        var exact = Math.Abs(position - Math.Round(position)) < 1e-6;
        var key = KeyOf(value, exact, chart.ValueFormatter is null ? 3 : 4);
        if (key == row.ValueKey && row.Value is not null)
        {
            return;
        }
        row.ValueKey = key;
        var text = chart.FormatValue(value);
        if (text == row.ValueString && row.Value is not null)
        {
            return;
        }
        row.ValueString = text;
        row.Value?.Dispose();
        row.Value = Layout(text, p, p.TypeSM, strong ? p.Foreground : p.TextSecondary, strong ? FontWeight.SemiBold : FontWeight.Medium, tabular: true);
    }

    /// <summary>A number that changes when the leading digits of <paramref name="value"/> change (the exact bits on a whole period).</summary>
    internal static long KeyOf(double value, bool exact, int digits)
    {
        if (exact)
        {
            return BitConverter.DoubleToInt64Bits(value);
        }
        var magnitude = Math.Abs(value);
        if (magnitude < 1e-12)
        {
            return 0;
        }
        var exponent = (int)Math.Floor(Math.Log10(magnitude));
        var quantum = Math.Pow(10, exponent - digits + 1);
        return (long)Math.Round(value / quantum) * 64 + exponent + 32;
    }

    private TextLayout? NameOnBar(int item, RowText row, ChartsBPalette p)
    {
        if (row.NameOnBar is null)
        {
            var chart = _chart!;
            var strong = item == chart.HighlightItem;
            row.NameOnBar = Layout(chart.Engine.Items[item].Label, p, p.TypeSM, default, strong ? FontWeight.SemiBold : FontWeight.Medium,
                Math.Max(40, Bounds.Width * 0.6), brush: p.Brush(OnBarColor(ColorOf(item))));
        }
        return row.NameOnBar;
    }

    private static Color OnBarColor(Color bar)
    {
        static double Linear(byte channel)
        {
            var c = channel / 255.0;
            return c <= 0.04045 ? c / 12.92 : Math.Pow((c + 0.055) / 1.055, 2.4);
        }
        var luminance = 0.2126 * Linear(bar.R) + 0.7152 * Linear(bar.G) + 0.0722 * Linear(bar.B);
        return luminance > 0.36 ? Color.FromRgb(0x14, 0x14, 0x16) : Colors.White;
    }

    // ---- layout -------------------------------------------------------------------------------------------------------

    protected override Size MeasureOverride(Size availableSize)
    {
        var chart = _chart;
        var width = double.IsInfinity(availableSize.Width) ? 560 : availableSize.Width;
        if (chart is null)
        {
            return new Size(width, 160);
        }
        var engine = chart.Engine;
        EnsureText(width);
        EnsureLegend(width);
        var bars = Math.Max(1, Math.Min(chart.BarCount, Math.Max(engine.ItemCount, 1)));
        return new Size(width, _legendHeight + chart.AxisHeight + bars * Pitch - chart.BarGap + BottomPadding);
    }

    private void EnsureLegend(double width)
    {
        var chart = _chart!;
        var engine = chart.Engine;
        var p = chart.Palette;
        if (Math.Abs(width - _legendWidth) <= 0.5 && _legend.Count == engine.Groups.Length)
        {
            return;
        }
        foreach (var entry in _legend)
        {
            entry.Text.Dispose();
        }
        _legend.Clear();
        _legendWidth = width;
        double x = 0, y = 0;
        for (var g = 0; g < engine.Groups.Length; g++)
        {
            var text = Layout(engine.Groups[g].Label, p, p.TypeXS, p.TextSecondary);
            var itemWidth = 16 + text.WidthIncludingTrailingWhitespace;
            if (x > 0 && x + itemWidth > width)
            {
                x = 0;
                y += LegendRowHeight;
            }
            _legend.Add(new LegendEntry { Text = text, Hue = g < 4 ? g : -1, X = x, Y = y });
            x += itemWidth + LegendItemGap;
        }
        _legendHeight = _legend.Count == 0 ? 0 : y + LegendRowHeight + LegendBottom;
    }

    // ---- input --------------------------------------------------------------------------------------------------------

    private int PickRow(Point point)
    {
        var chart = _chart!;
        if (point.Y < PlotTop)
        {
            return -1;
        }
        var rank = (int)Math.Floor((point.Y - PlotTop) / Pitch);
        return rank >= 0 && rank < chart.ShownBars ? chart.Engine.Order[rank] : -1;
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        if (_chart is not null)
        {
            _chart.SetReading(PickRow(e.GetPosition(this)));
        }
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (_chart is not null)
        {
            _chart.SetReading(PickRow(e.GetPosition(this)));
        }
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        if (e.Pointer.Type == PointerType.Mouse)
        {
            _chart?.SetReading(-1);
        }
    }

    protected override void OnGotFocus(FocusChangedEventArgs e)
    {
        base.OnGotFocus(e);
        InvalidateVisual();
    }

    protected override void OnLostFocus(FocusChangedEventArgs e)
    {
        base.OnLostFocus(e);
        _chart?.SetReading(-1);
        InvalidateVisual();
    }

    // ---- tooltip & accessibility ----------------------------------------------------------------------------------------

    /// <summary>Follows the reading: the content is rebuilt only when what it says changes.</summary>
    internal void SyncTooltip(bool force)
    {
        var chart = _chart;
        var tooltip = _tooltip;
        if (chart is null || tooltip is null)
        {
            return;
        }
        var item = chart.ReadingItem;
        if (item < 0 || Bounds.Width <= 0)
        {
            if (tooltip.IsOpen)
            {
                tooltip.Hide();
                InvalidateVisual();
            }
            _tipItem = -1;
            return;
        }
        var engine = chart.Engine;
        var p = chart.Palette;
        if (_rows.Length != engine.ItemCount)
        {
            EnsureText(Bounds.Width);
        }
        var row = _rows[item];
        UpdateValueText(item, row, p, item == chart.HighlightItem);
        var rank = engine.Rank[item];
        var frame = chart.FrameIndex;
        var barEnd = BarWidthOf(item);
        var contentChanged = force || item != _tipItem || rank != _tipRank || frame != _tipFrame || row.ValueString != _tipValue;
        if (!contentChanged && Math.Abs(barEnd - _tipBarEnd) < 12)
        {
            return;
        }
        if (contentChanged)
        {
            var xs = p.TypeXS;
            var rows = new List<ChartsBTipRow>
            {
                new()
                {
                    Left = ChartsBText.Make(engine.Items[item].Label, p, xs, p.TextSecondary), Swatch = ColorOf(item),
                    Shape = ChartsBSwatchShape.Square, MinHeight = xs * 1.5
                },
                new()
                {
                    MarginTop = 2, MinHeight = p.TypeSM * 1.4,
                    Left = ChartsBText.Make(chart.CurrentPeriod, p, xs, p.TextMuted),
                    Right = ChartsBText.Make(row.ValueString ?? string.Empty, p, p.TypeSM, p.Foreground, FontWeight.Medium)
                },
                new()
                {
                    MinHeight = xs * 1.5,
                    Left = ChartsBText.Make("Rank", p, xs, p.TextMuted),
                    Right = ChartsBText.Make($"{rank + 1} of {engine.ItemCount}", p, xs, p.Foreground)
                }
            };
            tooltip.SetRows(rows);
        }
        _tipItem = item;
        _tipRank = rank;
        _tipFrame = frame;
        _tipValue = row.ValueString;
        _tipBarEnd = barEnd;

        var size = tooltip.Size;
        var ax = PlotLeft + barEnd;
        var ay = PlotTop + rank * Pitch;
        var left = ax + TipGap;
        if (left + size.Width > Bounds.Width)
        {
            left = ax - TipGap - size.Width;
        }
        var y = ay + (_chart!.BarHeight - size.Height) / 2;
        tooltip.Show(new Point(ChartsBMath.Clamp(left, 0, Math.Max(0, Bounds.Width - size.Width)), ChartsBMath.Clamp(y, 0, Math.Max(0, Bounds.Height - size.Height))));
        InvalidateVisual();
    }

    internal void Announce() => _peer?.Announce();

    protected override AutomationPeer OnCreateAutomationPeer() => _peer = new ChartsBAutomationPeer(this, this);

    string IChartsBAccessible.ChartRoleDescription => "race bar chart";

    string IChartsBAccessible.ChartName => _chart?.AccessibleName ?? string.Empty;

    string IChartsBAccessible.ChartHelp => _chart?.LiveText ?? string.Empty;

    // ---- drawing --------------------------------------------------------------------------------------------------------

    public override void Render(DrawingContext context)
    {
        var chart = _chart;
        var width = Bounds.Width;
        if (chart is null || width <= 0)
        {
            return;
        }
        var engine = chart.Engine;
        var p = chart.Palette;
        EnsureText(width);
        EnsureLegend(width);
        if (engine.ItemCount == 0 || engine.FrameCount == 0)
        {
            _empty ??= Layout(chart.EmptyLabel, p, p.TypeSM, p.TextMuted);
            _empty.Draw(context, new Point((width - _empty.WidthIncludingTrailingWhitespace) / 2, (Bounds.Height - _empty.Height) / 2));
            return;
        }
        DrawLegend(context, p);
        DrawAxis(context, chart, engine, p);
        DrawRows(context, chart, engine, p);
        _tooltip?.Draw(context, p);
        if (IsFocused && Classes.Contains(":focus-visible"))
        {
            context.DrawRectangle(null, p.Pen(p.FocusRing, 2), new RoundedRect(new Rect(Bounds.Size).Inflate(2), p.RadiusControl / 2));
        }
    }

    private void DrawLegend(DrawingContext context, ChartsBPalette p)
    {
        foreach (var entry in _legend)
        {
            ChartsBTooltip.DrawSwatch(context, p, new Rect(entry.X, entry.Y + (LegendRowHeight - 10) / 2, 10, 10), HueColor(entry.Hue), ChartsBSwatchShape.Square);
            entry.Text.Draw(context, new Point(entry.X + 16, entry.Y + (LegendRowHeight - entry.Text.Height) / 2));
        }
    }

    private void DrawAxis(DrawingContext context, RaceBarChart chart, RaceBarEngine engine, ChartsBPalette p)
    {
        var mix = ArcMotion.IsFull(this) ? engine.TickMix : 1;
        DrawTickSet(context, chart, engine, p, engine.TickStep, mix, mix < 1 ? engine.PreviousTickStep : 0, true);
        if (mix < 1 && engine.PreviousTickStep > 0)
        {
            DrawTickSet(context, chart, engine, p, engine.PreviousTickStep, 1 - mix, engine.TickStep, false);
        }
    }

    private void DrawTickSet(DrawingContext context, RaceBarChart chart, RaceBarEngine engine, ChartsBPalette p, double step, double opacity, double other, bool current)
    {
        if (step <= 0)
        {
            return;
        }
        var plotLeft = PlotLeft;
        var plotWidth = PlotWidth;
        var top = PlotTop;
        var bottom = Bounds.Height - BottomPadding;
        var domain = engine.Domain;
        for (var k = 0; k <= 64; k++)
        {
            var value = k * step;
            if (value > domain * (1 + 1e-9))
            {
                break;
            }
            var shared = other > 0 && Math.Abs(value / other - Math.Round(value / other)) < 1e-6;
            if (shared && !current)
            {
                continue;
            }
            var x = plotLeft + value / domain * plotWidth;
            // A tick coming in from the right edge fades in over the last stretch of the axis, so it never pops.
            var edge = ChartsBMath.Clamp((domain - value) / (domain * 0.05), 0, 1);
            var alpha = (shared ? 1 : opacity) * (k == 0 ? 1 : edge);
            if (alpha <= 0.01)
            {
                continue;
            }
            var label = TickLabel(chart, p, value);
            using (context.PushOpacity(alpha))
            {
                var pen = p.Pen(k == 0 ? p.BorderStrong : p.BorderSubtle, 1);
                var snapped = Math.Round(x) + 0.5;
                context.DrawLine(pen, new Point(snapped, top - 4), new Point(snapped, bottom));
                var lx = ChartsBMath.Clamp(x - label.WidthIncludingTrailingWhitespace / 2, 0, Math.Max(0, Bounds.Width - label.WidthIncludingTrailingWhitespace));
                label.Draw(context, new Point(lx, top - 6 - label.Height));
            }
        }
    }

    private TextLayout TickLabel(RaceBarChart chart, ChartsBPalette p, double value)
    {
        var key = Math.Round(value * 1e6) / 1e6;
        if (_tickLabels.TryGetValue(key, out var label))
        {
            return label;
        }
        if (_tickLabels.Count >= MaxTickLabels)
        {
            foreach (var old in _tickLabels.Values)
            {
                old.Dispose();
            }
            _tickLabels.Clear();
        }
        label = Layout(chart.FormatTick(key), p, p.TypeXS, p.TextMuted, tabular: true);
        _tickLabels[key] = label;
        return label;
    }

    private void DrawRows(DrawingContext context, RaceBarChart chart, RaceBarEngine engine, ChartsBPalette p)
    {
        var width = Bounds.Width;
        var pitch = Pitch;
        var barHeight = chart.BarHeight;
        var plotTop = PlotTop;
        var plotLeft = PlotLeft;
        var narrow = Narrow;
        var radius = chart.BarRadius;
        var bars = Math.Min(chart.BarCount, engine.ItemCount);
        using var clip = context.PushClip(new Rect(-2, plotTop - 2, width + 4, bars * pitch - chart.BarGap + 4));
        // Bottom rank first, so a bar on its way up slides over the ones it passes.
        var order = engine.Order;
        for (var r = order.Length - 1; r >= 0; r--)
        {
            var item = order[r];
            var opacity = engine.Presence[item] * engine.Dim[item];
            if (opacity <= 0.004)
            {
                continue;
            }
            var y = plotTop + engine.Row[item] * pitch;
            if (y >= plotTop + bars * pitch)
            {
                continue;
            }
            var barWidth = BarWidthOf(item);
            var color = ColorOf(item);
            using (context.PushOpacity(opacity))
            {
                if (barWidth >= 0.5)
                {
                    var end = Math.Min(radius, barWidth);
                    context.DrawRectangle(p.Brush(color), null, new RoundedRect(new Rect(plotLeft, y, barWidth, barHeight), 0, end, end, 0));
                }
                var row = RowOf(item, p);
                var name = row.Name!;
                var value = row.Value!;
                var valueY = y + (barHeight - value.Height) / 2;
                var nameY = y + (barHeight - name.Height) / 2;
                if (narrow)
                {
                    var nameWidth = name.WidthIncludingTrailingWhitespace;
                    // Hysteresis: a bar near the edge of fitting does not flicker its name in and out.
                    var fits = barWidth >= nameWidth + InsidePadding * 2 + (row.NameInside ? -4 : 4);
                    row.NameInside = fits;
                    if (fits)
                    {
                        NameOnBar(item, row, p)!.Draw(context, new Point(plotLeft + InsidePadding, nameY));
                        value.Draw(context, new Point(ClampValueX(plotLeft + barWidth + ValueGap, value, width), valueY));
                    }
                    else
                    {
                        var nx = plotLeft + barWidth + ValueGap;
                        name.Draw(context, new Point(nx, nameY));
                        value.Draw(context, new Point(ClampValueX(nx + nameWidth + ValueGap, value, width), valueY));
                    }
                }
                else
                {
                    name.Draw(context, new Point(plotLeft - NameGap - name.WidthIncludingTrailingWhitespace, nameY));
                    value.Draw(context, new Point(ClampValueX(plotLeft + barWidth + ValueGap, value, width), valueY));
                }
            }
        }
    }

    private static double ClampValueX(double x, TextLayout value, double width) => Math.Min(x, Math.Max(0, width - value.WidthIncludingTrailingWhitespace));
}
