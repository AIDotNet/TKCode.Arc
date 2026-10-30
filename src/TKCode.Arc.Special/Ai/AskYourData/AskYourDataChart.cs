using System.Globalization;
using Avalonia;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using TKCode.Arc.Motion;

namespace TKCode.Arc;

/// <summary>
/// The answer of an <see cref="AskYourData"/> drawn as bars or lines in one self-drawn control: gridlines with value ticks
/// on the left, category labels under the plot that thin out to keep 46px apart, stacked bars (series that add up) or
/// grouped bars (rates), or one line per series. A readout follows the pointer, a tap or the arrow keys: the category's
/// column lights up and a tooltip lists each series (and the total when they add up), flipping to whichever side of the
/// column has room.
/// <para>
/// Bars rise from the baseline in 320ms with at most 180ms of stagger across the categories; lines draw from the left in
/// 460ms. Code-driven (one <see cref="MotionValue{T}"/> timeline) because the bars and paths are coordinates of the drawn
/// plot, which a ControlTheme transition cannot reach. The layout (text, tick positions, line geometry) is cached per size
/// and result; a frame of the build only redraws. Reduced motion and Off: drawn whole.
/// </para>
/// </summary>
internal sealed class AskYourDataChart : Control
{
    /// <summary>The result drawn.</summary>
    public static readonly StyledProperty<AskYourDataResult?> ResultProperty =
        AvaloniaProperty.Register<AskYourDataChart, AskYourDataResult?>(nameof(Result));

    /// <summary>Bars or lines (a table is a separate view).</summary>
    public static readonly StyledProperty<AskYourDataChartKind> KindProperty =
        AvaloniaProperty.Register<AskYourDataChart, AskYourDataChartKind>(nameof(Kind));

    /// <summary>The words of the readout.</summary>
    public static readonly StyledProperty<AskYourDataLabels> LabelsProperty =
        AvaloniaProperty.Register<AskYourDataChart, AskYourDataLabels>(nameof(Labels), AskYourDataLabels.Default);

    /// <summary>The palette for series without a color of their own (theme: Series1).</summary>
    public static readonly StyledProperty<IBrush?> Series1BrushProperty =
        AvaloniaProperty.Register<AskYourDataChart, IBrush?>(nameof(Series1Brush));

    /// <summary>Theme: Series2.</summary>
    public static readonly StyledProperty<IBrush?> Series2BrushProperty =
        AvaloniaProperty.Register<AskYourDataChart, IBrush?>(nameof(Series2Brush));

    /// <summary>Theme: Series3.</summary>
    public static readonly StyledProperty<IBrush?> Series3BrushProperty =
        AvaloniaProperty.Register<AskYourDataChart, IBrush?>(nameof(Series3Brush));

    /// <summary>Theme: Series4.</summary>
    public static readonly StyledProperty<IBrush?> Series4BrushProperty =
        AvaloniaProperty.Register<AskYourDataChart, IBrush?>(nameof(Series4Brush));

    /// <summary>Gridlines (theme: BorderSubtle).</summary>
    public static readonly StyledProperty<IBrush?> GridBrushProperty =
        AvaloniaProperty.Register<AskYourDataChart, IBrush?>(nameof(GridBrush));

    /// <summary>Tick and category labels (theme: TextMuted).</summary>
    public static readonly StyledProperty<IBrush?> AxisBrushProperty =
        AvaloniaProperty.Register<AskYourDataChart, IBrush?>(nameof(AxisBrush));

    /// <summary>The active column (theme: SurfaceMuted).</summary>
    public static readonly StyledProperty<IBrush?> HighlightBrushProperty =
        AvaloniaProperty.Register<AskYourDataChart, IBrush?>(nameof(HighlightBrush));

    /// <summary>The tooltip's fill (theme: SurfaceRaised).</summary>
    public static readonly StyledProperty<IBrush?> TooltipBackgroundProperty =
        AvaloniaProperty.Register<AskYourDataChart, IBrush?>(nameof(TooltipBackground));

    /// <summary>The tooltip's edge (theme: Border).</summary>
    public static readonly StyledProperty<IBrush?> TooltipBorderBrushProperty =
        AvaloniaProperty.Register<AskYourDataChart, IBrush?>(nameof(TooltipBorderBrush));

    /// <summary>The readout's values (theme: Foreground).</summary>
    public static readonly StyledProperty<IBrush?> ForegroundProperty =
        AvaloniaProperty.Register<AskYourDataChart, IBrush?>(nameof(Foreground));

    /// <summary>The readout's labels (theme: TextSecondary).</summary>
    public static readonly StyledProperty<IBrush?> SecondaryBrushProperty =
        AvaloniaProperty.Register<AskYourDataChart, IBrush?>(nameof(SecondaryBrush));

    /// <summary>The ring around the line dots and between stacked segments (theme: Surface).</summary>
    public static readonly StyledProperty<IBrush?> SurfaceBrushProperty =
        AvaloniaProperty.Register<AskYourDataChart, IBrush?>(nameof(SurfaceBrush));

    /// <summary>The keyboard focus ring (theme: FocusRing).</summary>
    public static readonly StyledProperty<IBrush?> FocusBrushProperty =
        AvaloniaProperty.Register<AskYourDataChart, IBrush?>(nameof(FocusBrush));

    /// <summary>The font of the labels.</summary>
    public static readonly StyledProperty<FontFamily> FontFamilyProperty = TextElement.FontFamilyProperty.AddOwner<AskYourDataChart>();

    /// <summary>The size of the labels.</summary>
    public static readonly StyledProperty<double> FontSizeProperty =
        AvaloniaProperty.Register<AskYourDataChart, double>(nameof(FontSize), 11);

    /// <summary>The smallest gap between two category labels.</summary>
    internal const double LabelGap = 46;

    /// <summary>A bar's rise.</summary>
    internal static readonly TimeSpan BarRise = TimeSpan.FromMilliseconds(320);

    /// <summary>The stagger across all bars, at most.</summary>
    internal static readonly TimeSpan MaxStagger = TimeSpan.FromMilliseconds(180);

    /// <summary>A line's draw.</summary>
    internal static readonly TimeSpan LineDraw = TimeSpan.FromMilliseconds(460);

    private const double TopPad = 8;
    private const double BottomPad = 22;
    private const double GutterGap = 8;
    private const double TipPad = 10;
    private const double TipRow = 18;

    private static readonly Easing s_linear = new LinearEasing();
    private static readonly Easing s_rise = new SplineEasing(0.22, 1, 0.36, 1);

    private readonly List<(FormattedText Text, double Y)> _ticks = new();
    private readonly List<(FormattedText Text, double X)> _xLabels = new();
    private readonly List<StreamGeometry> _lines = new();
    private readonly List<Point[]> _points = new();
    private readonly List<IPen> _linePens = new();
    private readonly List<IBrush> _seriesBrushes = new();
    private readonly List<(FormattedText Label, FormattedText Value, IBrush Swatch)> _tipRows = new();
    private FormattedText? _tipTitle;
    private Size _tipSize;
    private MotionValue<double>? _build;
    private bool _layoutValid;
    private Size _layoutSize;
    private Rect _plot;
    private double _band;
    private double _top = 1;
    private int? _active;
    private IPen? _gridPen;
    private IPen? _focusPen;
    private IPen? _hairPen;
    private IPen? _ringPen;
    private IPen? _tipPen;

    static AskYourDataChart()
    {
        FocusableProperty.OverrideDefaultValue<AskYourDataChart>(true);
        FocusAdornerProperty.OverrideDefaultValue<AskYourDataChart>(null);
        AffectsRender<AskYourDataChart>(HighlightBrushProperty, TooltipBackgroundProperty);
    }

    /// <inheritdoc cref="ResultProperty"/>
    public AskYourDataResult? Result { get => GetValue(ResultProperty); set => SetValue(ResultProperty, value); }

    /// <inheritdoc cref="KindProperty"/>
    public AskYourDataChartKind Kind { get => GetValue(KindProperty); set => SetValue(KindProperty, value); }

    /// <inheritdoc cref="LabelsProperty"/>
    public AskYourDataLabels Labels { get => GetValue(LabelsProperty); set => SetValue(LabelsProperty, value); }

    /// <inheritdoc cref="Series1BrushProperty"/>
    public IBrush? Series1Brush { get => GetValue(Series1BrushProperty); set => SetValue(Series1BrushProperty, value); }

    /// <inheritdoc cref="Series2BrushProperty"/>
    public IBrush? Series2Brush { get => GetValue(Series2BrushProperty); set => SetValue(Series2BrushProperty, value); }

    /// <inheritdoc cref="Series3BrushProperty"/>
    public IBrush? Series3Brush { get => GetValue(Series3BrushProperty); set => SetValue(Series3BrushProperty, value); }

    /// <inheritdoc cref="Series4BrushProperty"/>
    public IBrush? Series4Brush { get => GetValue(Series4BrushProperty); set => SetValue(Series4BrushProperty, value); }

    /// <inheritdoc cref="GridBrushProperty"/>
    public IBrush? GridBrush { get => GetValue(GridBrushProperty); set => SetValue(GridBrushProperty, value); }

    /// <inheritdoc cref="AxisBrushProperty"/>
    public IBrush? AxisBrush { get => GetValue(AxisBrushProperty); set => SetValue(AxisBrushProperty, value); }

    /// <inheritdoc cref="HighlightBrushProperty"/>
    public IBrush? HighlightBrush { get => GetValue(HighlightBrushProperty); set => SetValue(HighlightBrushProperty, value); }

    /// <inheritdoc cref="TooltipBackgroundProperty"/>
    public IBrush? TooltipBackground { get => GetValue(TooltipBackgroundProperty); set => SetValue(TooltipBackgroundProperty, value); }

    /// <inheritdoc cref="TooltipBorderBrushProperty"/>
    public IBrush? TooltipBorderBrush { get => GetValue(TooltipBorderBrushProperty); set => SetValue(TooltipBorderBrushProperty, value); }

    /// <inheritdoc cref="ForegroundProperty"/>
    public IBrush? Foreground { get => GetValue(ForegroundProperty); set => SetValue(ForegroundProperty, value); }

    /// <inheritdoc cref="SecondaryBrushProperty"/>
    public IBrush? SecondaryBrush { get => GetValue(SecondaryBrushProperty); set => SetValue(SecondaryBrushProperty, value); }

    /// <inheritdoc cref="SurfaceBrushProperty"/>
    public IBrush? SurfaceBrush { get => GetValue(SurfaceBrushProperty); set => SetValue(SurfaceBrushProperty, value); }

    /// <inheritdoc cref="FocusBrushProperty"/>
    public IBrush? FocusBrush { get => GetValue(FocusBrushProperty); set => SetValue(FocusBrushProperty, value); }

    /// <inheritdoc cref="FontFamilyProperty"/>
    public FontFamily FontFamily { get => GetValue(FontFamilyProperty); set => SetValue(FontFamilyProperty, value); }

    /// <inheritdoc cref="FontSizeProperty"/>
    public double FontSize { get => GetValue(FontSizeProperty); set => SetValue(FontSizeProperty, value); }

    /// <summary>The category the readout shows, or null.</summary>
    internal int? ActiveIndex => _active;

    /// <summary>Raised when the readout moves or clears.</summary>
    internal event EventHandler? ActiveIndexChanged;

    /// <summary>The plot area (tests).</summary>
    internal Rect PlotRect => _plot;

    /// <summary>The value at the top of the axis (tests).</summary>
    internal double AxisTop => _top;

    /// <summary>Every how many categories a label is drawn (tests).</summary>
    internal int LabelStride { get; private set; } = 1;

    /// <summary>Where the build is, 0 to 1 (1 when drawn whole).</summary>
    internal double BuildProgress => _build is null ? 1 : Math.Clamp(_build.Value / BuildSeconds, 0, 1);

    /// <summary>Whether the tooltip sits right of its column (false: flipped left).</summary>
    internal bool TooltipOnRight { get; private set; } = true;

    /// <summary>The value format of the result.</summary>
    internal Func<double, string> Format => Result?.FormatValue ?? AskYourDataFormat.Value;

    private bool HasData => Result is { HasData: true };

    private bool StackedBars => Kind == AskYourDataChartKind.Bar && Result is { Stacked: true };

    private double BuildSeconds => Kind == AskYourDataChartKind.Line
        ? LineDraw.TotalSeconds
        : BarRise.TotalSeconds + Stagger * Math.Max(0, (Result?.Categories.Count ?? 1) - 1);

    private double Stagger
    {
        get
        {
            var n = Result?.Categories.Count ?? 1;
            return n <= 1 ? 0 : Math.Min(0.03, MaxStagger.TotalSeconds / (n - 1));
        }
    }

    /// <summary>Plays the build from nothing (bars rise, lines draw); drawn whole without full motion.</summary>
    public void Replay()
    {
        _build ??= CreateBuild();
        var total = BuildSeconds;
        if (!IsLoaded || !ArcMotion.IsFull(this))
        {
            _build.Jump(total);
            InvalidateVisual();
            return;
        }
        _build.Jump(0);
        _ = _build.AnimateTo(total, MotionSpec.Tween(TimeSpan.FromSeconds(total), s_linear));
        InvalidateVisual();
    }

    private MotionValue<double> CreateBuild()
    {
        var value = new MotionValue<double>(this, BuildSeconds, MotionVectors.Double);
        value.Changed += _ => InvalidateVisual();
        return value;
    }

    /// <summary>Moves the readout to a category (null clears it).</summary>
    internal void SetActive(int? index)
    {
        if (index is { } i && (!HasData || i < 0 || i >= Result!.Categories.Count))
        {
            index = null;
        }
        if (_active == index)
        {
            return;
        }
        _active = index;
        BuildTooltip();
        InvalidateVisual();
        ActiveIndexChanged?.Invoke(this, EventArgs.Empty);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        var p = change.Property;
        if (p == ResultProperty || p == KindProperty)
        {
            _active = null;
            _layoutValid = false;
            InvalidateArrange();
            InvalidateVisual();
        }
        else if (p == FontFamilyProperty || p == FontSizeProperty || p == AxisBrushProperty || p == GridBrushProperty
                 || p == Series1BrushProperty || p == Series2BrushProperty || p == Series3BrushProperty || p == Series4BrushProperty
                 || p == ForegroundProperty || p == SecondaryBrushProperty || p == TooltipBorderBrushProperty || p == LabelsProperty
                 || p == SurfaceBrushProperty || p == FocusBrushProperty)
        {
            _layoutValid = false;
            InvalidateArrange();
            InvalidateVisual();
        }
    }

    protected override Size MeasureOverride(Size availableSize) => default;

    protected override Size ArrangeOverride(Size finalSize)
    {
        if (!_layoutValid || _layoutSize != finalSize)
        {
            BuildLayout(finalSize);
        }
        return finalSize;
    }

    /// <summary>The brush of a series: its own color, or the palette in order.</summary>
    internal IBrush SeriesBrush(int index)
    {
        if (Result is { } result && index < result.Series.Count && result.Series[index].Color is { } color)
        {
            return new ImmutableSolidColorBrush(color);
        }
        return (index % 4) switch
        {
            0 => Series1Brush,
            1 => Series2Brush,
            2 => Series3Brush,
            _ => Series4Brush
        } ?? Brushes.Gray;
    }

    private FormattedText Text(string text, IBrush? brush, FontWeight weight = FontWeight.Normal) =>
        new(text, CultureInfo.CurrentCulture, FlowDirection.LeftToRight, new Typeface(FontFamily, FontStyle.Normal, weight), FontSize,
            brush ?? Brushes.Gray);

    /// <summary>Lays out the scale, the labels and the line paths for one size and result.</summary>
    private void BuildLayout(Size size)
    {
        _layoutValid = true;
        _layoutSize = size;
        _ticks.Clear();
        _xLabels.Clear();
        _lines.Clear();
        _points.Clear();
        _linePens.Clear();
        _seriesBrushes.Clear();
        _ringPen = SurfaceBrush is { } ring ? new ImmutablePen(ring.ToImmutable(), 2) : null;
        _tipPen = TooltipBorderBrush is { } tip ? new ImmutablePen(tip.ToImmutable(), 1) : null;
        _gridPen = GridBrush is { } grid ? new ImmutablePen(grid.ToImmutable(), 1) : null;
        _focusPen = FocusBrush is { } focus ? new ImmutablePen(focus.ToImmutable(), 2) : null;
        _hairPen = AxisBrush is { } axis ? new ImmutablePen(axis.ToImmutable(), 1, new ImmutableDashStyle([3, 3], 0)) : null;
        if (Result is not { HasData: true } result || size.Width <= 0 || size.Height <= 0)
        {
            _plot = default;
            return;
        }
        var format = Format;
        var (top, step) = AskYourDataFormat.NiceScale(AskYourDataFormat.DataMaximum(result, StackedBars));
        _top = top;
        var tickTexts = new List<(FormattedText, double)>();
        var gutter = 0.0;
        for (var v = 0.0; v <= top + step / 2; v += step)
        {
            var text = Text(format(v), AxisBrush);
            gutter = Math.Max(gutter, text.Width);
            tickTexts.Add((text, v));
        }
        _plot = new Rect(gutter + GutterGap, TopPad, Math.Max(1, size.Width - gutter - GutterGap - 2),
            Math.Max(1, size.Height - TopPad - BottomPad));
        foreach (var (text, v) in tickTexts)
        {
            _ticks.Add((text, YOf(v)));
        }
        var n = result.Categories.Count;
        _band = _plot.Width / n;
        LabelStride = AskYourDataFormat.LabelStride(_band, LabelGap);
        for (var i = 0; i < n; i += LabelStride)
        {
            _xLabels.Add((Text(result.Categories[i], AxisBrush), CenterOf(i)));
        }
        for (var s = 0; s < result.Series.Count; s++)
        {
            _seriesBrushes.Add(SeriesBrush(s));
            var points = new Point[n];
            var geometry = new StreamGeometry();
            using (var ctx = geometry.Open())
            {
                for (var i = 0; i < n; i++)
                {
                    points[i] = new Point(CenterOf(i), YOf(AskYourDataFormat.ValueAt(result.Series[s], i)));
                    if (i == 0)
                    {
                        ctx.BeginFigure(points[i], false);
                    }
                    else
                    {
                        ctx.LineTo(points[i]);
                    }
                }
                ctx.EndFigure(false);
            }
            _points.Add(points);
            _lines.Add(geometry);
            _linePens.Add(new ImmutablePen(_seriesBrushes[s].ToImmutable(), 2, lineCap: PenLineCap.Round, lineJoin: PenLineJoin.Round));
        }
        BuildTooltip();
    }

    private double YOf(double value) => _plot.Bottom - Math.Clamp(value / _top, 0, 1) * _plot.Height;

    private double CenterOf(int index) => _plot.Left + _band * (index + 0.5);

    private void BuildTooltip()
    {
        _tipRows.Clear();
        _tipTitle = null;
        if (_active is not { } i || Result is not { HasData: true } result || i >= result.Categories.Count)
        {
            return;
        }
        var format = Format;
        _tipTitle = Text(result.Categories[i], Foreground, FontWeight.SemiBold);
        var width = _tipTitle.Width;
        for (var s = 0; s < result.Series.Count; s++)
        {
            var label = Text(result.Series[s].Label, SecondaryBrush);
            var value = Text(format(AskYourDataFormat.ValueAt(result.Series[s], i)), Foreground, FontWeight.Medium);
            _tipRows.Add((label, value, s < _seriesBrushes.Count ? _seriesBrushes[s] : SeriesBrush(s)));
            width = Math.Max(width, 14 + label.Width + 16 + value.Width);
        }
        if (result.Stacked && result.Series.Count > 1)
        {
            var label = Text(Labels.Total, SecondaryBrush);
            var value = Text(format(AskYourDataFormat.TotalAt(result, i)), Foreground, FontWeight.SemiBold);
            _tipRows.Add((label, value, Brushes.Transparent));
            width = Math.Max(width, 14 + label.Width + 16 + value.Width);
        }
        _tipSize = new Size(width + TipPad * 2, TipPad * 2 + TipRow * (_tipRows.Count + 1));
    }

    public override void Render(DrawingContext context)
    {
        var bounds = new Rect(Bounds.Size);
        // The whole plot takes the pointer, not only the painted bars.
        context.FillRectangle(Brushes.Transparent, bounds);
        if (Result is not { HasData: true } result || !_layoutValid || _plot.Width <= 1)
        {
            return;
        }
        var n = result.Categories.Count;
        var scale = TopLevel.GetTopLevel(this)?.RenderScaling ?? 1;
        if (_active is { } active && HighlightBrush is { } highlight)
        {
            var x = _plot.Left + _band * active;
            context.DrawRectangle(highlight, null, new Rect(x + 1, _plot.Top - 4, Math.Max(0, _band - 2), _plot.Height + 4), 6, 6);
        }
        foreach (var (text, y) in _ticks)
        {
            // Hairlines on device pixels, so they stay crisp at any scale.
            var snapped = (Math.Round(y * scale) + 0.5) / scale;
            if (_gridPen is not null)
            {
                context.DrawLine(_gridPen, new Point(_plot.Left, snapped), new Point(_plot.Right, snapped));
            }
            context.DrawText(text, new Point(_plot.Left - GutterGap - text.Width, y - text.Height / 2));
        }
        foreach (var (text, x) in _xLabels)
        {
            var left = Math.Clamp(x - text.Width / 2, 0, Math.Max(0, bounds.Width - text.Width));
            context.DrawText(text, new Point(left, _plot.Bottom + 6));
        }
        var t = _build?.Value ?? BuildSeconds;
        if (Kind == AskYourDataChartKind.Line)
        {
            RenderLines(context, result, t);
        }
        else
        {
            RenderBars(context, result, n, t);
        }
        if (_active is { } index && _tipTitle is not null)
        {
            RenderTooltip(context, index, bounds);
        }
        if (_focusPen is not null && Classes.Contains(":focus-visible"))
        {
            context.DrawRectangle(null, _focusPen, bounds.Deflate(1), 10, 10);
        }
    }

    private void RenderBars(DrawingContext context, AskYourDataResult result, int n, double t)
    {
        var stagger = Stagger;
        var rise = BarRise.TotalSeconds;
        var stacked = result.Stacked;
        var seriesCount = result.Series.Count;
        var groupWidth = stacked ? Math.Min(_band * 0.6, 40) : Math.Min(_band * 0.76, 18 * seriesCount + 2 * (seriesCount - 1));
        var gap = stacked ? 0 : Math.Min(2, groupWidth / seriesCount * 0.2);
        var barWidth = stacked ? groupWidth : (groupWidth - gap * (seriesCount - 1)) / seriesCount;
        for (var i = 0; i < n; i++)
        {
            var p = Math.Clamp((t - i * stagger) / rise, 0, 1);
            if (p <= 0)
            {
                continue;
            }
            var eased = s_rise.Ease(p);
            var left = CenterOf(i) - groupWidth / 2;
            var baseY = _plot.Bottom;
            for (var s = 0; s < seriesCount; s++)
            {
                var value = AskYourDataFormat.ValueAt(result.Series[s], i);
                var h = Math.Clamp(value / _top, 0, 1) * _plot.Height * eased;
                if (h <= 0.1)
                {
                    continue;
                }
                var brush = _seriesBrushes[s];
                if (stacked)
                {
                    // A 1px seam of the surface between stacked segments.
                    var seam = h > 3 && baseY < _plot.Bottom ? 1 : 0;
                    context.DrawRectangle(brush, null, new Rect(left, baseY - h, barWidth, Math.Max(0, h - seam)), 2, 2);
                    baseY -= h;
                }
                else
                {
                    var x = left + s * (barWidth + gap);
                    context.DrawRectangle(brush, null, new Rect(x, _plot.Bottom - h, barWidth, h), 2, 2);
                }
            }
        }
    }

    private void RenderLines(DrawingContext context, AskYourDataResult result, double t)
    {
        var p = s_rise.Ease(Math.Clamp(t / LineDraw.TotalSeconds, 0, 1));
        var clip = new Rect(0, 0, _plot.Left + (_plot.Width + 8) * p, Bounds.Height);
        using (context.PushClip(clip))
        {
            for (var s = 0; s < _lines.Count; s++)
            {
                context.DrawGeometry(null, _linePens[s], _lines[s]);
            }
        }
        if (_active is { } i && p >= 1)
        {
            var x = CenterOf(i);
            if (_hairPen is not null)
            {
                context.DrawLine(_hairPen, new Point(x, _plot.Top), new Point(x, _plot.Bottom));
            }
            for (var s = 0; s < _points.Count; s++)
            {
                context.DrawEllipse(_seriesBrushes[s], _ringPen, _points[s][i], 4, 4);
            }
        }
    }

    private void RenderTooltip(DrawingContext context, int index, Rect bounds)
    {
        var center = CenterOf(index);
        var right = center + _band / 2 + 6;
        TooltipOnRight = right + _tipSize.Width <= bounds.Width;
        var x = TooltipOnRight ? right : Math.Max(0, center - _band / 2 - 6 - _tipSize.Width);
        var box = new Rect(x, _plot.Top, _tipSize.Width, _tipSize.Height);
        context.DrawRectangle(TooltipBackground, _tipPen, box, 10, 10);
        var y = box.Top + TipPad;
        context.DrawText(_tipTitle!, new Point(box.Left + TipPad, y + (TipRow - _tipTitle!.Height) / 2));
        foreach (var (label, value, swatch) in _tipRows)
        {
            y += TipRow;
            context.DrawRectangle(swatch, null, new Rect(box.Left + TipPad, y + TipRow / 2 - 4, 8, 8), 2, 2);
            context.DrawText(label, new Point(box.Left + TipPad + 14, y + (TipRow - label.Height) / 2));
            context.DrawText(value, new Point(box.Right - TipPad - value.Width, y + (TipRow - value.Height) / 2));
        }
    }

    private int IndexAt(Point point)
    {
        var n = Result?.Categories.Count ?? 0;
        if (n == 0 || _band <= 0)
        {
            return -1;
        }
        return Math.Clamp((int)Math.Floor((point.X - _plot.Left) / _band), 0, n - 1);
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        if (HasData)
        {
            SetActive(IndexAt(e.GetPosition(this)));
        }
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (HasData)
        {
            // A tap reads a column on touch; dragging across keeps reading while vertical scrolling passes through.
            SetActive(IndexAt(e.GetPosition(this)));
        }
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        if (!IsKeyboardFocusWithin)
        {
            SetActive(null);
        }
    }

    protected override void OnLostFocus(FocusChangedEventArgs e)
    {
        base.OnLostFocus(e);
        InvalidateVisual();
    }

    protected override void OnGotFocus(FocusChangedEventArgs e)
    {
        base.OnGotFocus(e);
        InvalidateVisual();
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Handled || !HasData)
        {
            return;
        }
        var n = Result!.Categories.Count;
        switch (e.Key)
        {
            case Key.Right:
                SetActive(_active is { } r ? Math.Min(n - 1, r + 1) : 0);
                e.Handled = true;
                break;
            case Key.Left:
                SetActive(_active is { } l ? Math.Max(0, l - 1) : n - 1);
                e.Handled = true;
                break;
            case Key.Home:
                SetActive(0);
                e.Handled = true;
                break;
            case Key.End:
                SetActive(n - 1);
                e.Handled = true;
                break;
            case Key.Escape when _active is not null:
                SetActive(null);
                e.Handled = true;
                break;
        }
    }
}
