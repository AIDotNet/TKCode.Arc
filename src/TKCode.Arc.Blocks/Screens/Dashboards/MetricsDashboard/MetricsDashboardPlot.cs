using Avalonia;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using TKCode.Arc.Motion;

namespace TKCode.Arc;

/// <summary>What the plot draws for one metric over one range: the series plus the texts that go with it.</summary>
internal sealed class MetricsDashboardScene
{
    public MetricsDashboardScene(MetricsDashboardSeries series, string[] axisLabels, string[] xLabels, Func<int, string> title,
        Func<double, string> value, string previousFormat)
    {
        Series = series;
        AxisLabels = axisLabels;
        XLabels = xLabels;
        Title = title;
        Value = value;
        PreviousFormat = previousFormat;
    }

    /// <summary>The data.</summary>
    public MetricsDashboardSeries Series { get; }

    /// <summary>The four y labels, bottom to top.</summary>
    public string[] AxisLabels { get; }

    /// <summary>The x label of every point ("Oct 18"); only every <c>LabelStep</c>-th is drawn.</summary>
    public string[] XLabels { get; }

    /// <summary>The tooltip's first line for a point ("Oct 18", "Week of Oct 13").</summary>
    public Func<int, string> Title { get; }

    /// <summary>Value text for the tooltip.</summary>
    public Func<double, string> Value { get; }

    /// <summary>The tooltip's previous-period line, {0} = the formatted value.</summary>
    public string PreviousFormat { get; }

    // Geometry built for one plot rectangle, kept until the rectangle changes.
    internal Rect GeometryRect;
    internal StreamGeometry? Line;
    internal StreamGeometry? Area;
    internal StreamGeometry? PreviousLine;
    internal Point[] Points = Array.Empty<Point>();
}

/// <summary>
/// The dashboard's chart, drawn in one <see cref="Render"/>: the y labels (right aligned, 10 left of the plot), four
/// gridlines (the bottom one in Border, the others BorderSubtle), the area (accent 7%, down to the plot's bottom), the
/// previous period (TextMuted 1.25, dashed 3 / 3) and the line (accent 1.75), both on the harmonic monotone curve, the
/// x labels in a 24px row, and while a point is inspected a 1px BorderStrong rule, a 9px dot and the tooltip (all
/// instant, as measured). Range and metric changes cross-fade the whole picture (240ms (.22, 1, .36, 1)); axis labels
/// switch at once. The previous period fades in and out on Smooth.
/// </summary>
/// <remarks>
/// Why code-driven: the cross-fade holds two complete pictures at once and the compare fade is one stroke inside the same
/// drawing; neither is an element a ControlTheme transition could target.
/// </remarks>
internal sealed class MetricsDashboardPlot : Control
{
    public static readonly StyledProperty<double> PlotHeightProperty =
        AvaloniaProperty.Register<MetricsDashboardPlot, double>(nameof(PlotHeight), 200);

    public static readonly StyledProperty<double> XAxisHeightProperty =
        AvaloniaProperty.Register<MetricsDashboardPlot, double>(nameof(XAxisHeight), 24);

    public static readonly StyledProperty<double> YAxisGapProperty =
        AvaloniaProperty.Register<MetricsDashboardPlot, double>(nameof(YAxisGap), 10);

    public static readonly StyledProperty<double> LineThicknessProperty =
        AvaloniaProperty.Register<MetricsDashboardPlot, double>(nameof(LineThickness), 1.75);

    public static readonly StyledProperty<double> PreviousLineThicknessProperty =
        AvaloniaProperty.Register<MetricsDashboardPlot, double>(nameof(PreviousLineThickness), 1.25);

    public static readonly StyledProperty<double> PreviousDashProperty =
        AvaloniaProperty.Register<MetricsDashboardPlot, double>(nameof(PreviousDash), 3);

    public static readonly StyledProperty<double> AreaOpacityProperty =
        AvaloniaProperty.Register<MetricsDashboardPlot, double>(nameof(AreaOpacity), 0.07);

    public static readonly StyledProperty<double> DotDiameterProperty =
        AvaloniaProperty.Register<MetricsDashboardPlot, double>(nameof(DotDiameter), 9);

    public static readonly StyledProperty<int> MaxLabelsProperty =
        AvaloniaProperty.Register<MetricsDashboardPlot, int>(nameof(MaxLabels), 6);

    private const double LabelSize = 12;
    private const double LabelLineHeight = 16.8;
    private const double XLabelTop = 7;

    private readonly AnalyticsPalette _palette = new();
    private readonly AnalyticsTextCache _cache = new(512);
    private readonly AnalyticsReadout _readout = new(AnalyticsReadoutStyle.Compact)
    {
        EnterDuration = TimeSpan.Zero,
        ExitDuration = TimeSpan.Zero
    };
    private readonly LiveLoop _loop;
    private MetricsDashboardScene? _scene;
    private MetricsDashboardScene? _leaving;
    private AnalyticsTween _fade = new(1);
    private AnalyticsSpringChannel _compare = new(0, 0.001, 0.01);
    private bool _isCompareEnabled;
    private int _activeIndex = -1;
    private double[] _tangents = Array.Empty<double>();

    static MetricsDashboardPlot()
    {
        AffectsMeasure<MetricsDashboardPlot>(PlotHeightProperty, XAxisHeightProperty);
        AffectsRender<MetricsDashboardPlot>(YAxisGapProperty, LineThicknessProperty, PreviousLineThicknessProperty, PreviousDashProperty,
            AreaOpacityProperty, DotDiameterProperty, MaxLabelsProperty);
        FocusableProperty.OverrideDefaultValue<MetricsDashboardPlot>(true);
        // The plot draws its own ring around the plot area; the window's default adorner would frame the axes too.
        FocusAdornerProperty.OverrideDefaultValue<MetricsDashboardPlot>(null);
        CursorProperty.OverrideDefaultValue<MetricsDashboardPlot>(new Cursor(StandardCursorType.Cross));
    }

    public MetricsDashboardPlot()
    {
        _loop = new LiveLoop(this, Advance) { MinimumLevel = ArcMotionLevel.Reduced };
        ActualThemeVariantChanged += (_, _) => RefreshPalette();
        ClipToBounds = false;
        // A theme style sets the window's adorner on focusable controls; the plot draws its own ring.
        FocusAdorner = null;
    }

    /// <summary>Raised when the inspected point changes (−1: none) and whether the keyboard did it.</summary>
    public event Action<int, bool>? InspectionChanged;

    public double PlotHeight { get => GetValue(PlotHeightProperty); set => SetValue(PlotHeightProperty, value); }

    public double XAxisHeight { get => GetValue(XAxisHeightProperty); set => SetValue(XAxisHeightProperty, value); }

    public double YAxisGap { get => GetValue(YAxisGapProperty); set => SetValue(YAxisGapProperty, value); }

    public double LineThickness { get => GetValue(LineThicknessProperty); set => SetValue(LineThicknessProperty, value); }

    public double PreviousLineThickness { get => GetValue(PreviousLineThicknessProperty); set => SetValue(PreviousLineThicknessProperty, value); }

    public double PreviousDash { get => GetValue(PreviousDashProperty); set => SetValue(PreviousDashProperty, value); }

    public double AreaOpacity { get => GetValue(AreaOpacityProperty); set => SetValue(AreaOpacityProperty, value); }

    public double DotDiameter { get => GetValue(DotDiameterProperty); set => SetValue(DotDiameterProperty, value); }

    public int MaxLabels { get => GetValue(MaxLabelsProperty); set => SetValue(MaxLabelsProperty, value); }

    /// <summary>The scene drawn now.</summary>
    public MetricsDashboardScene? Scene => _scene;

    /// <summary>The inspected point (−1: none).</summary>
    public int ActiveIndex => _activeIndex;

    /// <summary>The new picture's opacity during a cross-fade (1 at rest).</summary>
    internal double FadeProgress => _fade.Value;

    /// <summary>True while the old picture is still drawn.</summary>
    internal bool IsCrossFading => _leaving is not null;

    /// <summary>The previous period's opacity now.</summary>
    internal double CompareOpacity => Math.Clamp(_compare.Value, 0, 1);

    /// <summary>The tooltip (tests).</summary>
    internal AnalyticsReadout Readout => _readout;

    /// <summary>The plot area inside the control (no axes).</summary>
    internal Rect PlotRect
    {
        get
        {
            var left = AxisColumnWidth() + YAxisGap;
            return new Rect(left, 0, Math.Max(0, Bounds.Width - left), PlotHeight);
        }
    }

    /// <summary>Shows a new picture; with <paramref name="animate"/> (and Full motion) the old one cross-fades out.</summary>
    public void SetScene(MetricsDashboardScene scene, bool animate)
    {
        var level = ArcMotion.GetEffectiveLevel(this);
        if (animate && _scene is not null && IsLoaded && level == ArcMotionLevel.Full)
        {
            _leaving = _scene;
            _fade.Jump(0);
            _fade.Start(1, ArcMotionPresets.Standard, AnalyticsEasings.Standard);
            _loop.Start();
        }
        else
        {
            _leaving = null;
            _fade.Jump(1);
        }
        _scene = scene;
        if (_activeIndex >= scene.Series.Count)
        {
            SetActive(scene.Series.Count - 1, false);
        }
        else if (_activeIndex >= 0)
        {
            UpdateReadout();
        }
        InvalidateMeasure();
        InvalidateVisual();
    }

    /// <summary>Shows or hides the previous period (Smooth fade at Full motion, at once otherwise).</summary>
    public void SetCompare(bool enabled, bool animate)
    {
        _isCompareEnabled = enabled;
        var target = enabled ? 1 : 0;
        if (animate && IsLoaded && ArcMotion.IsFull(this))
        {
            _compare.SpringTo(target, AnalyticsSprings.Smooth);
            _loop.Start();
        }
        else
        {
            _compare.Jump(target);
        }
        if (_activeIndex >= 0)
        {
            UpdateReadout();
        }
        InvalidateVisual();
    }

    /// <summary>Inspects a point (−1 clears).</summary>
    public void Inspect(int index, bool byKeyboard = false)
    {
        var count = _scene?.Series.Count ?? 0;
        SetActive(index < 0 || count == 0 ? -1 : Math.Min(index, count - 1), byKeyboard);
    }

    private void SetActive(int index, bool byKeyboard)
    {
        if (index == _activeIndex)
        {
            return;
        }
        _activeIndex = index;
        UpdateReadout();
        InvalidateVisual();
        InspectionChanged?.Invoke(index, byKeyboard);
    }

    /// <summary>The tooltip's text for a point: title, value and (with Compare on) the previous value.</summary>
    public string? DescribePoint(int index)
    {
        if (_scene is not { } scene || index < 0 || index >= scene.Series.Count)
        {
            return null;
        }
        var text = $"{scene.Title(index)}, {scene.Value(scene.Series.Values[index])}";
        if (_isCompareEnabled && scene.Series.Previous is { } previous)
        {
            text += ", " + string.Format(scene.PreviousFormat, scene.Value(previous[index]));
        }
        return text;
    }

    private void UpdateReadout()
    {
        if (_scene is not { } scene || _activeIndex < 0 || _activeIndex >= scene.Series.Count)
        {
            _readout.Clear();
            return;
        }
        var i = _activeIndex;
        IReadOnlyList<AnalyticsReadoutRow>? rows = null;
        if (_isCompareEnabled && scene.Series.Previous is { } previous)
        {
            rows = [new AnalyticsReadoutRow(string.Format(scene.PreviousFormat, scene.Value(previous[i])))];
        }
        _readout.SetContent(new AnalyticsReadoutContent(scene.Title(i), scene.Value(scene.Series.Values[i]), rows), ArcMotionLevel.Off);
    }

    private bool Advance(double dt)
    {
        var fading = _fade.Advance(dt);
        if (!fading)
        {
            _leaving = null;
        }
        var comparing = _compare.Step(dt);
        InvalidateVisual();
        return fading || comparing;
    }

    // Layout -----------------------------------------------------------------------------------------------------------

    protected override Size MeasureOverride(Size availableSize)
    {
        var width = double.IsFinite(availableSize.Width) ? availableSize.Width : 600;
        return new Size(width, PlotHeight + XAxisHeight);
    }

    private Typeface LabelFace => new(_palette.BodyFont);

    private double AxisColumnWidth()
    {
        if (_scene is null || !_palette.IsReady)
        {
            return 0;
        }
        double width = 0;
        foreach (var label in _scene.AxisLabels)
        {
            width = Math.Max(width, _cache.Get(label, LabelFace, LabelSize, true).Width);
        }
        return Math.Ceiling(width * 10) / 10;
    }

    // Input ------------------------------------------------------------------------------------------------------------

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        if (e.Pointer.Type == PointerType.Touch && !e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            return;
        }
        Track(e.GetPosition(this));
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        Track(e.GetPosition(this));
    }

    private void Track(Point point)
    {
        if (_scene is not { } scene || scene.Series.Count == 0)
        {
            return;
        }
        var plot = PlotRect;
        if (point.Y < plot.Top || point.Y > plot.Bottom || point.X < plot.Left - 0.5 || point.X > plot.Right + 0.5)
        {
            SetActive(-1, false);
            return;
        }
        var scale = new AnalyticsPointScale(scene.Series.Count, plot.Left, plot.Right);
        SetActive(scale.NearestIndex(point.X), false);
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        SetActive(-1, false);
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Handled || _scene is not { } scene)
        {
            return;
        }
        if (e.Key == Key.Escape && _activeIndex >= 0)
        {
            SetActive(-1, true);
            e.Handled = true;
            return;
        }
        if (AnalyticsKeyboard.Step(e.Key, _activeIndex, scene.Series.Count, vertical: false) is { } next)
        {
            SetActive(next, true);
            e.Handled = true;
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
        SetActive(-1, false);
        InvalidateVisual();
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new PlotPeer(this);

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        RefreshPalette();
    }

    private void RefreshPalette()
    {
        if (VisualRoot is null)
        {
            return;
        }
        _palette.Refresh(this);
        _cache.Clear();
        InvalidateMeasure();
        InvalidateVisual();
    }

    // Drawing ----------------------------------------------------------------------------------------------------------

    public override void Render(DrawingContext context)
    {
        if (!_palette.IsReady)
        {
            _palette.Refresh(this);
        }
        var plot = PlotRect;
        // The control is only hit where it paints: cover the whole plot for the crosshair.
        context.FillRectangle(Brushes.Transparent, new Rect(Bounds.Size));
        if (_scene is not { } scene || plot.Width <= 0)
        {
            return;
        }

        DrawGrid(context, plot);
        if (_leaving is { } leaving)
        {
            using (context.PushOpacity(1 - Math.Clamp(_fade.Value, 0, 1)))
            {
                DrawSeries(context, leaving, plot);
            }
            using (context.PushOpacity(Math.Clamp(_fade.Value, 0, 1)))
            {
                DrawSeries(context, scene, plot);
            }
        }
        else
        {
            DrawSeries(context, scene, plot);
        }
        DrawAxisLabels(context, scene, plot);
        DrawInspection(context, scene, plot);
        if (AnalyticsFocus.IsVisible(this))
        {
            AnalyticsFocus.Draw(context, _palette, plot);
        }
    }

    private void DrawGrid(DrawingContext context, Rect plot)
    {
        var subtle = _palette.Pen(_palette.BorderSubtle, 1, cap: PenLineCap.Flat);
        var edge = _palette.Pen(_palette.Border, 1, cap: PenLineCap.Flat);
        for (var k = 0; k < 4; k++)
        {
            // Arc's rows sit at 0, 1/3, 2/3 and the last pixel row of the plot.
            var y = k == 3 ? plot.Bottom - 0.5 : Math.Floor(plot.Top + plot.Height * k / 3) + 0.5;
            context.DrawLine(k == 3 ? edge : subtle, new Point(plot.Left, y), new Point(plot.Right, y));
        }
    }

    private void EnsureGeometry(MetricsDashboardScene scene, Rect plot)
    {
        if (scene.Line is not null && scene.GeometryRect == plot)
        {
            return;
        }
        var series = scene.Series;
        var n = series.Count;
        scene.GeometryRect = plot;
        var ticks = series.Ticks;
        var y = new AnalyticsLinearScale(ticks.Min, ticks.Max, plot.Bottom, plot.Top);
        var x = new AnalyticsPointScale(n, plot.Left, plot.Right);
        var points = new Point[n];
        for (var i = 0; i < n; i++)
        {
            points[i] = new Point(x.Map(i), y.Map(series.Values[i]));
        }
        scene.Points = points;
        scene.Line = AnalyticsGeometry.Line(points, AnalyticsCurve.MonotoneHarmonic);
        scene.Area = AnalyticsGeometry.Area(points, plot.Bottom, AnalyticsCurve.MonotoneHarmonic);
        if (series.Previous is { } previous)
        {
            var before = new Point[n];
            for (var i = 0; i < n; i++)
            {
                before[i] = new Point(x.Map(i), y.Map(previous[i]));
            }
            scene.PreviousLine = AnalyticsGeometry.Line(before, AnalyticsCurve.MonotoneHarmonic);
        }
        else
        {
            scene.PreviousLine = null;
        }
    }

    private void DrawSeries(DrawingContext context, MetricsDashboardScene scene, Rect plot)
    {
        if (scene.Series.Count == 0)
        {
            return;
        }
        EnsureGeometry(scene, plot);
        context.DrawGeometry(_palette.Brush(_palette.Accent, AreaOpacity), null, scene.Area!);
        var compare = Math.Clamp(_compare.Value, 0, 1);
        if (scene.PreviousLine is { } previous && compare > 0.001)
        {
            using (context.PushOpacity(compare))
            {
                context.DrawGeometry(null, _palette.Pen(_palette.TextMuted, PreviousLineThickness, dash: PreviousDash, gap: PreviousDash, cap: PenLineCap.Flat),
                    previous);
            }
        }
        context.DrawGeometry(null, _palette.Pen(_palette.Accent, LineThickness), scene.Line!);
    }

    private void DrawAxisLabels(DrawingContext context, MetricsDashboardScene scene, Rect plot)
    {
        var face = LabelFace;
        var muted = _palette.Brush(_palette.TextMuted);
        var right = plot.Left - YAxisGap;
        // Arc: a 4-label column stretched over the plot's height + 12 (6 above, 6 below), labels spread end to end.
        var top = plot.Top - 6;
        var pitch = (plot.Height + 12 - LabelLineHeight) / 3;
        for (var k = 0; k < 4 && k < scene.AxisLabels.Length; k++)
        {
            var label = _cache.Get(scene.AxisLabels[k], face, LabelSize, true);
            var rowTop = top + (3 - k) * pitch;
            label.Draw(context, muted, new Point(right - label.Width, rowTop + (LabelLineHeight - label.Height) / 2));
        }

        var n = scene.Series.Count;
        if (n == 0)
        {
            return;
        }
        var step = MetricsDashboardEngine.LabelStep(n, MaxLabels);
        var x = new AnalyticsPointScale(n, plot.Left, plot.Right);
        var labelTop = plot.Bottom + XLabelTop;
        for (var i = 0; i < n; i += step)
        {
            var label = _cache.Get(scene.XLabels[i], face, LabelSize, true);
            var px = x.Map(i);
            var left = i == 0 ? px : i == n - 1 ? px - label.Width : px - label.Width / 2;
            label.Draw(context, muted, new Point(left, labelTop + (LabelLineHeight - label.Height) / 2));
        }
    }

    private void DrawInspection(DrawingContext context, MetricsDashboardScene scene, Rect plot)
    {
        var i = _activeIndex;
        if (i < 0 || i >= scene.Series.Count)
        {
            return;
        }
        EnsureGeometry(scene, plot);
        var point = scene.Points[i];
        // Arc: a 1px rule centred on the point (translateX(−0.5)).
        var ruleX = point.X;
        context.DrawLine(_palette.Pen(_palette.BorderStrong, 1, cap: PenLineCap.Flat), new Point(ruleX, plot.Top), new Point(ruleX, plot.Bottom));
        var radius = (DotDiameter - 2) / 2;
        context.DrawEllipse(_palette.Brush(_palette.Surface), _palette.Pen(_palette.Accent, 2), point, radius, radius);

        if (_readout.Content is null)
        {
            UpdateReadoutContentOnly();
        }
        var size = _readout.Measure(_cache, _palette);
        var left = AnalyticsReadout.CenteredX(point.X, size.Width, plot.Left, plot.Right);
        var topY = AnalyticsReadout.OppositeHalfY(point.Y, plot.Top, plot.Bottom, size.Height);
        if (!_readout.IsShown || _readout.Position != new Point(left, topY))
        {
            _readout.Show(new Point(left, topY), ArcMotionLevel.Off);
        }
        _readout.Draw(context, _cache, _palette);
    }

    private void UpdateReadoutContentOnly()
    {
        if (_scene is { } scene && _activeIndex >= 0 && _activeIndex < scene.Series.Count)
        {
            _readout.SetContent(new AnalyticsReadoutContent(scene.Title(_activeIndex), scene.Value(scene.Series.Values[_activeIndex])),
                ArcMotionLevel.Off);
        }
    }

    private sealed class PlotPeer(MetricsDashboardPlot owner) : ControlAutomationPeer(owner)
    {
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Image;

        protected override bool IsControlElementCore() => true;

        protected override bool IsContentElementCore() => true;
    }
}
