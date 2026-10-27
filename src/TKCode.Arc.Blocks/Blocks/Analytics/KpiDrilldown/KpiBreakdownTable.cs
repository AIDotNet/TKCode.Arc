using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using TKCode.Arc.DesignTokens;

namespace TKCode.Arc;

/// <summary>
/// The detail view's breakdown table (notes 3.4): a heading row (dimension, Share, Value, Change; 12px TextMuted) and one
/// 38.6px row per member under a 1px BorderSubtle rule: the name (14 Medium) cross-fades when the dimension changes (out
/// 80ms, then in 240ms), the 6px share bar springs to its share (no stagger), the percent, value and change roll digit by
/// digit, the change tinted Success or Danger. The name takes 30%, Value 96 and Change 88 on the right; the share column
/// takes the rest (bar, then a 44px percent and 12 of air). Compact hides the share column (Value 76, Change 68).
/// </summary>
internal sealed class KpiBreakdownTable : Control
{
    internal const double HeaderHeight = 29.8;
    internal const double RowHeight = 38.6;
    private const double CellGap = 12;

    private readonly AnalyticsPalette _palette = new();
    private readonly AnalyticsTextSwap _dimension = Swap(12, false);
    private readonly TextBlock _share = Heading();
    private readonly TextBlock _value = Heading();
    private readonly TextBlock _change = Heading();
    private readonly List<Row> _rows = new();
    private bool _compact;

    public KpiBreakdownTable()
    {
        foreach (var part in new Control[] { _dimension, _share, _value, _change })
        {
            Add(part);
        }
        ArcTokens.Bind(_dimension, AnalyticsTextSwap.ForegroundProperty, ArcDesignTokenKind.TextMuted);
        _value.TextAlignment = TextAlignment.Right;
        _change.TextAlignment = TextAlignment.Right;
        ActualThemeVariantChanged += (_, _) => RefreshPalette();
        ResourcesChanged += (_, _) => RefreshPalette();
    }

    /// <summary>Value column width (96; compact 76).</summary>
    public static readonly StyledProperty<double> ValueWidthProperty =
        AvaloniaProperty.Register<KpiBreakdownTable, double>(nameof(ValueWidth), 96);

    /// <summary>Change column width (88; compact 68).</summary>
    public static readonly StyledProperty<double> ChangeWidthProperty =
        AvaloniaProperty.Register<KpiBreakdownTable, double>(nameof(ChangeWidth), 88);

    /// <summary>The percent's width in the share column (44).</summary>
    public static readonly StyledProperty<double> PercentWidthProperty =
        AvaloniaProperty.Register<KpiBreakdownTable, double>(nameof(PercentWidth), 44);

    static KpiBreakdownTable()
    {
        AffectsMeasure<KpiBreakdownTable>(ValueWidthProperty, ChangeWidthProperty, PercentWidthProperty);
    }

    public double ValueWidth { get => GetValue(ValueWidthProperty); set => SetValue(ValueWidthProperty, value); }

    public double ChangeWidth { get => GetValue(ChangeWidthProperty); set => SetValue(ChangeWidthProperty, value); }

    public double PercentWidth { get => GetValue(PercentWidthProperty); set => SetValue(PercentWidthProperty, value); }

    /// <summary>Compact: no share column.</summary>
    public bool IsCompact
    {
        get => _compact;
        set
        {
            if (_compact == value)
            {
                return;
            }
            _compact = value;
            InvalidateMeasure();
        }
    }

    /// <summary>Rows shown (tests).</summary>
    internal int RowCount => _rows.Count;

    /// <summary>A row's parts (tests).</summary>
    internal (AnalyticsTextSwap Name, AnalyticsShareBar Bar, AnalyticsRollingNumber Percent, AnalyticsRollingNumber Value, AnalyticsRollingNumber Change) RowParts(int i)
    {
        var r = _rows[i];
        return (r.Name, r.Bar, r.Percent, r.Value, r.Change);
    }

    /// <summary>The dimension heading (tests).</summary>
    internal AnalyticsTextSwap DimensionHeading => _dimension;

    /// <summary>Sets the headings.</summary>
    public void SetHeadings(string dimension, string share, string value, string change, bool animate)
    {
        SetSwap(_dimension, dimension, animate);
        _share.Text = share;
        _value.Text = value;
        _change.Text = change;
    }

    /// <summary>Shows the rows of a breakdown; <paramref name="animate"/> rolls numbers, springs bars and cross-fades names.</summary>
    public void SetRows(IReadOnlyList<KpiBreakdownLine> lines, KpiMetric metric, bool animate)
    {
        while (_rows.Count > lines.Count)
        {
            var last = _rows[^1];
            _rows.RemoveAt(_rows.Count - 1);
            last.Remove(this);
        }
        while (_rows.Count < lines.Count)
        {
            var row = new Row();
            row.Add(this);
            _rows.Add(row);
        }
        var valueFormat = metric.NumberFormat;
        var changeFormat = metric.ChangeFormat;
        var percent = AnalyticsNumberFormat.Percent(0);
        for (var i = 0; i < lines.Count; i++)
        {
            var line = lines[i];
            var row = _rows[i];
            var fresh = !animate || !row.HasData;
            SetSwap(row.Name, line.Label, !fresh);
            row.Bar.Value = line.Share;
            Roll(row.Percent, percent, line.Share * 100, fresh);
            Roll(row.Value, valueFormat, double.IsFinite(line.Value) ? line.Value : 0, fresh);
            row.Change.IsVisible = line.Change is not null;
            if (line.Change is { } change)
            {
                Roll(row.Change, changeFormat, change, fresh);
                var tone = AnalyticsFormat.Tone(change, metric.IsLowerBetter, changeFormat.Decimals);
                row.SetTone(tone);
            }
            var spoken = $"{line.Label}, {percent.Format(line.Share * 100)}, {metric.FormatValue(line.Value)}";
            if (line.Change is { } c)
            {
                spoken += ", " + changeFormat.Format(c);
            }
            AutomationProperties.SetName(row.Name, spoken);
            row.HasData = true;
        }
        InvalidateMeasure();
    }

    private static void Roll(AnalyticsRollingNumber number, AnalyticsNumberFormat format, double amount, bool jump)
    {
        number.IsInstant = jump;
        number.Format = format;
        number.Amount = amount;
        number.IsInstant = false;
    }

    private static void SetSwap(AnalyticsTextSwap swap, string text, bool animate)
    {
        if (!animate)
        {
            swap.Swap = AnalyticsSwapStyle.Instant;
        }
        swap.Text = text;
        swap.Swap = AnalyticsSwapStyle.Sequential;
    }

    private void Add(Control control)
    {
        VisualChildren.Add(control);
        LogicalChildren.Add(control);
    }

    private void Drop(Control control)
    {
        VisualChildren.Remove(control);
        LogicalChildren.Remove(control);
    }

    private void RefreshPalette()
    {
        if (_palette.IsReady)
        {
            _palette.Refresh(this);
        }
        InvalidateVisual();
    }

    private (double Name, double Share, double Value, double Change) Columns(double width)
    {
        var name = _compact ? Math.Max(0, width - ValueWidth - ChangeWidth) : Math.Round(width * 0.3);
        var share = _compact ? 0 : Math.Max(0, width - name - ValueWidth - ChangeWidth);
        return (name, share, ValueWidth, ChangeWidth);
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var width = double.IsFinite(availableSize.Width) ? availableSize.Width : 600;
        var (name, share, value, change) = Columns(width);
        var cell = new Size(double.PositiveInfinity, double.PositiveInfinity);
        _dimension.Measure(new Size(Math.Max(0, name - CellGap), 16.8));
        _share.Measure(cell);
        _value.Measure(new Size(value, 16.8));
        _change.Measure(new Size(change, 16.8));
        _share.IsVisible = !_compact;
        foreach (var row in _rows)
        {
            row.Name.Measure(new Size(Math.Max(0, name - CellGap), 19.6));
            row.Bar.IsVisible = !_compact;
            row.Percent.IsVisible = !_compact;
            row.Bar.Measure(new Size(Math.Max(0, share - PercentWidth - CellGap), 6));
            row.Percent.Measure(cell);
            row.Value.Measure(cell);
            row.Change.Measure(cell);
        }
        return new Size(width, HeaderHeight + _rows.Count * RowHeight);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var (name, share, value, change) = Columns(finalSize.Width);
        var shareX = name;
        var valueRight = name + share + value;
        var changeRight = finalSize.Width;
        _dimension.Arrange(new Rect(0, 4, Math.Max(0, name - CellGap), 16.8));
        _share.Arrange(new Rect(shareX, 4, Math.Max(0, share), 16.8));
        _value.Arrange(new Rect(valueRight - value, 4, value, 16.8));
        _change.Arrange(new Rect(changeRight - change, 4, change, 16.8));
        var y = HeaderHeight;
        foreach (var row in _rows)
        {
            var textTop = y + 1 + 9;
            var mid = textTop + 19.6 / 2;
            row.Name.Arrange(new Rect(0, textTop, Math.Max(0, name - CellGap), 19.6));
            var barWidth = Math.Max(0, share - PercentWidth - CellGap);
            row.Bar.Arrange(new Rect(shareX, mid - 3 + 0.5, barWidth, 6));
            var p = row.Percent.DesiredSize;
            row.Percent.Arrange(new Rect(shareX + barWidth + PercentWidth - p.Width, mid - p.Height / 2, p.Width, p.Height));
            var v = row.Value.DesiredSize;
            row.Value.Arrange(new Rect(valueRight - v.Width, mid - v.Height / 2, v.Width, v.Height));
            var c = row.Change.DesiredSize;
            row.Change.Arrange(new Rect(changeRight - c.Width, mid - c.Height / 2, c.Width, c.Height));
            y += RowHeight;
        }
        return finalSize;
    }

    public override void Render(DrawingContext context)
    {
        if (!_palette.IsReady)
        {
            _palette.Refresh(this);
        }
        var pen = _palette.Pen(_palette.BorderSubtle, 1, cap: PenLineCap.Flat);
        var y = HeaderHeight;
        for (var i = 0; i < _rows.Count; i++)
        {
            context.DrawLine(pen, new Point(0, y + 0.5), new Point(Bounds.Width, y + 0.5));
            y += RowHeight;
        }
    }

    private static TextBlock Heading()
    {
        var text = new TextBlock { LineHeight = 16.8, VerticalAlignment = VerticalAlignment.Top };
        ArcTokens.Bind(text, TextBlock.FontSizeProperty, ArcDesignTokenKind.TypeXS);
        ArcTokens.Bind(text, TextBlock.ForegroundProperty, ArcDesignTokenKind.TextMuted);
        return text;
    }

    private static AnalyticsTextSwap Swap(double size, bool medium)
    {
        var swap = new AnalyticsTextSwap
        {
            FontSize = size,
            LineHeight = size > 12 ? 19.6 : 16.8,
            Swap = AnalyticsSwapStyle.Sequential
        };
        if (medium)
        {
            ArcTokens.Bind(swap, AnalyticsTextSwap.FontWeightProperty, ArcDesignTokenKind.WeightMedium);
        }
        return swap;
    }

    private static AnalyticsRollingNumber Number(double size, ArcDesignTokenKind color, bool medium)
    {
        var number = new AnalyticsRollingNumber { FontSize = size };
        ArcTokens.Bind(number, AnalyticsRollingNumber.ForegroundProperty, color);
        ArcTokens.Bind(number, AnalyticsRollingNumber.FontFamilyProperty, ArcDesignTokenKind.BodyFontFamily);
        if (medium)
        {
            ArcTokens.Bind(number, AnalyticsRollingNumber.FontWeightProperty, ArcDesignTokenKind.WeightMedium);
        }
        return number;
    }

    private sealed class Row
    {
        private IDisposable? _toneBinding;
        private AnalyticsTone? _tone;

        public Row()
        {
            ArcTokens.Bind(Name, AnalyticsTextSwap.ForegroundProperty, ArcDesignTokenKind.Foreground);
        }

        public AnalyticsTextSwap Name { get; } = Swap(14, true);

        public AnalyticsShareBar Bar { get; } = new() { Height = 6 };

        public AnalyticsRollingNumber Percent { get; } = Number(12, ArcDesignTokenKind.TextMuted, false);

        public AnalyticsRollingNumber Value { get; } = Number(14, ArcDesignTokenKind.Foreground, false);

        public AnalyticsRollingNumber Change { get; } = Number(14, ArcDesignTokenKind.TextMuted, false);

        public bool HasData { get; set; }

        public void Add(KpiBreakdownTable table)
        {
            table.Add(Name);
            table.Add(Bar);
            table.Add(Percent);
            table.Add(Value);
            table.Add(Change);
        }

        public void Remove(KpiBreakdownTable table)
        {
            table.Drop(Name);
            table.Drop(Bar);
            table.Drop(Percent);
            table.Drop(Value);
            table.Drop(Change);
            _toneBinding?.Dispose();
        }

        public void SetTone(AnalyticsTone tone)
        {
            if (_tone == tone)
            {
                return;
            }
            _tone = tone;
            _toneBinding?.Dispose();
            var kind = tone switch
            {
                AnalyticsTone.Good => ArcDesignTokenKind.Success,
                AnalyticsTone.Bad => ArcDesignTokenKind.Danger,
                _ => ArcDesignTokenKind.TextMuted
            };
            _toneBinding = ArcTokens.Bind(Change, AnalyticsRollingNumber.ForegroundProperty, kind);
        }
    }
}
