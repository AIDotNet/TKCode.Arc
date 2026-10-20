using System.Globalization;
using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Input;
using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>
/// Arc's sparkline: a compact trend beside a value, for cards, table rows and KPI blocks. Scrub it with the
/// pointer or the arrow keys: the cursor springs between points, the stretch after it dims as if it has not
/// happened yet, and the headline and aside roll to the scrubbed point. The line draws once like a pen the first
/// time it is half visible; the fill follows the tip, then the end dot lands. New data morphs the line from the
/// shape on screen.
/// <para>
/// Motion is code-driven because the pen tip, cursor and morph are coordinates inside the self-drawn plot.
/// </para>
/// </summary>
/// <example><code>&lt;arc:Sparkline Label="Revenue" Value="$48.2K" Change="+4.1%" Tone="Success" ItemsSource="{Binding Revenue}" /&gt;</code></example>
[TemplatePart(LabelPart, typeof(ArcContentSwap))]
[TemplatePart(HeadlinePart, typeof(ArcContentSwap))]
[TemplatePart(AsidePart, typeof(ArcContentSwap))]
[PseudoClasses(ScrubbingPseudoClass, SuccessPseudoClass, WarningPseudoClass, DangerPseudoClass)]
public class Sparkline : ChartBase
{
    public const string LabelPart = "PART_Label";
    public const string HeadlinePart = "PART_Headline";
    public const string AsidePart = "PART_Aside";
    public const string ScrubbingPseudoClass = ":scrubbing";
    public const string SuccessPseudoClass = ":success";
    public const string WarningPseudoClass = ":warning";
    public const string DangerPseudoClass = ":danger";

    /// <summary>Space kept above the highest and below the lowest point; half the line width.</summary>
    private const double Inset = 5;
    private const double Cap = 1.25;
    private static readonly SpringSpec Smooth = SpringSpec.FromVisual(0.4, 0);
    private static readonly SpringSpec Snappy = SpringSpec.FromVisual(0.26, 0.12);

    /// <summary>The headline at rest, such as "$48.2K". Leave it null to show the scrubbed value beside the label instead.</summary>
    public static readonly StyledProperty<string?> ValueProperty =
        AvaloniaProperty.Register<Sparkline, string?>(nameof(Value));

    /// <summary>The aside at rest, such as "+4.1%", in the tone color.</summary>
    public static readonly StyledProperty<string?> ChangeProperty =
        AvaloniaProperty.Register<Sparkline, string?>(nameof(Change));

    /// <summary>Color of the line, its fill and the change.</summary>
    public static readonly StyledProperty<ChartTone> ToneProperty =
        AvaloniaProperty.Register<Sparkline, ChartTone>(nameof(Tone));

    /// <summary>Drawing width in Arc's SVG units; with <see cref="DrawingHeight"/> it sets the aspect ratio (the width follows the container).</summary>
    public static readonly StyledProperty<double> DrawingWidthProperty =
        AvaloniaProperty.Register<Sparkline, double>(nameof(DrawingWidth), 160);

    /// <summary>Drawing height in Arc's SVG units.</summary>
    public static readonly StyledProperty<double> DrawingHeightProperty =
        AvaloniaProperty.Register<Sparkline, double>(nameof(DrawingHeight), 52);

    /// <summary>One label per point, such as a date; it replaces the change while that point is scrubbed.</summary>
    public static readonly StyledProperty<IReadOnlyList<string>?> PointLabelsProperty =
        AvaloniaProperty.Register<Sparkline, IReadOnlyList<string>?>(nameof(PointLabels));

    /// <summary>A quiet wash of the tone under the line.</summary>
    public static readonly StyledProperty<bool> ShowAreaProperty =
        AvaloniaProperty.Register<Sparkline, bool>(nameof(ShowArea), true);

    /// <summary>Pointer and keyboard scrubbing. Turn it off in dense tables to drop the tab stop and pointer handling.</summary>
    public static readonly StyledProperty<bool> IsInteractiveProperty =
        AvaloniaProperty.Register<Sparkline, bool>(nameof(IsInteractive), true);

    /// <inheritdoc cref="ValueProperty"/>
    public string? Value { get => GetValue(ValueProperty); set => SetValue(ValueProperty, value); }

    /// <inheritdoc cref="ChangeProperty"/>
    public string? Change { get => GetValue(ChangeProperty); set => SetValue(ChangeProperty, value); }

    /// <inheritdoc cref="ToneProperty"/>
    public ChartTone Tone { get => GetValue(ToneProperty); set => SetValue(ToneProperty, value); }

    /// <inheritdoc cref="DrawingWidthProperty"/>
    public double DrawingWidth { get => GetValue(DrawingWidthProperty); set => SetValue(DrawingWidthProperty, value); }

    /// <inheritdoc cref="DrawingHeightProperty"/>
    public double DrawingHeight { get => GetValue(DrawingHeightProperty); set => SetValue(DrawingHeightProperty, value); }

    /// <inheritdoc cref="PointLabelsProperty"/>
    public IReadOnlyList<string>? PointLabels { get => GetValue(PointLabelsProperty); set => SetValue(PointLabelsProperty, value); }

    /// <inheritdoc cref="ShowAreaProperty"/>
    public bool ShowArea { get => GetValue(ShowAreaProperty); set => SetValue(ShowAreaProperty, value); }

    /// <inheritdoc cref="IsInteractiveProperty"/>
    public bool IsInteractive { get => GetValue(IsInteractiveProperty); set => SetValue(IsInteractiveProperty, value); }

    private ChartLineMorph? _morph;
    private MotionValue<double>? _cursor;
    private MotionValue<double>? _scrub;
    private MotionValue<double>? _trail;
    private MotionValue<double>? _dot;
    private MotionValue<double>? _dotOpacity;
    private ArcContentSwap? _label;
    private ArcContentSwap? _headline;
    private ArcContentSwap? _aside;
    private bool _landed;
    private int? _lastIndex;
    private double _min;
    private double _max;
    private Point[] _points = [];
    private double[] _lengths = [];
    private StreamGeometry? _line;
    private StreamGeometry? _area;
    private bool _geometryDirty = true;
    private Size _geometrySize;

    static Sparkline()
    {
        CategoryLabelProperty.OverrideDefaultValue<Sparkline>("Point");
        AffectsPlot<Sparkline>(ToneProperty, ShowAreaProperty);
    }

    public Sparkline()
    {
        UpdatePseudoClasses();
    }

    private protected override double RevealThreshold => 0.5;

    /// <summary>One smooth stroke, slow at both ends like a pen (Arc: 840ms in-out).</summary>
    private protected override MotionSpec RevealSpec => PenStroke;

    private protected override string DefaultValueFormat => "#,##0.###";

    private ChartLineMorph Morph
    {
        get
        {
            if (_morph is null)
            {
                _morph = new ChartLineMorph(this);
                _morph.Changed += () =>
                {
                    _geometryDirty = true;
                    InvalidatePlots();
                };
            }
            return _morph;
        }
    }

    private MotionValue<double> Crosshair => _cursor ??= CreateValue(1);

    private MotionValue<double> Scrub => _scrub ??= CreateValue(0);

    private MotionValue<double> Trail => _trail ??= CreateValue(0);

    private MotionValue<double> Dot => _dot ??= CreateValue(0);

    private MotionValue<double> DotOpacity => _dotOpacity ??= CreateValue(0);

    private MotionValue<double> CreateValue(double initial)
    {
        var value = new MotionValue<double>(this, initial, MotionVectors.Double);
        value.Changed += _ => InvalidatePlots();
        return value;
    }

    private protected override void OnTemplateApplied(INameScope scope)
    {
        _label = scope.Find<ArcContentSwap>(LabelPart);
        _headline = scope.Find<ArcContentSwap>(HeadlinePart);
        _aside = scope.Find<ArcContentSwap>(AsidePart);
        if (MainPlot is { } plot)
        {
            plot.Focusable = IsInteractive;
            plot.IsHitTestVisible = IsInteractive;
        }
        UpdateCaption(1);
    }

    #region Data

    private double[] Values => Data.Series.Length > 0 ? Data.Series[0].Values : [];

    private int Last => Math.Max(0, Values.Length - 1);

    private protected override void OnDataChanged(ChartData previous, ChartData current)
    {
        var values = current.Series.Length > 0 ? current.Series[0].Values : [];
        // A single value draws a flat line.
        double[] safe = values.Length > 1 ? values : [values.Length > 0 ? values[0] : 0, values.Length > 0 ? values[0] : 0];
        _min = safe.Min();
        _max = safe.Max();
        var span = _max - _min;
        var unit = new double[safe.Length];
        for (var i = 0; i < safe.Length; i++)
        {
            unit[i] = span > 0 ? (safe[i] - _min) / span : 0.5;
        }
        var (xs, ys) = ChartCurves.SampleLine(unit, true, 128);
        for (var i = 0; i < ys.Length; i++)
        {
            ys[i] = Math.Clamp(ys[i], 0, 1);
        }
        // New data morphs the line from the shape on screen, on shared x positions.
        Morph.SetTargets(["line"], [xs], [ys], 161, IsMotionFull, Smooth);
        _geometryDirty = true;
        if (ActiveIndex is { } index && index > Last)
        {
            SetActive(null);
        }
        UpdateCaption(1);
        UpdateAutomation();
    }

    private protected override void OnRevealCompleted()
    {
        _landed = true;
        var full = MotionLevel == ArcMotionLevel.Full;
        _ = DotOpacity.AnimateTo(1, full ? MotionSpec.Tween(ArcMotionPresets.Fast, ArcEasings.Standard) : MotionSpec.Instant);
        _ = Dot.AnimateTo(ActiveIndex is null ? 7 : 10, full ? MotionSpec.Spring(Snappy) : MotionSpec.Instant);
        UpdateTrail();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ValueProperty || change.Property == ChangeProperty || change.Property == PointLabelsProperty)
        {
            UpdateCaption(1);
            UpdateAutomation();
            RefreshText();
        }
        else if (change.Property == ToneProperty)
        {
            UpdatePseudoClasses();
        }
        else if (change.Property == IsInteractiveProperty)
        {
            if (MainPlot is { } plot)
            {
                plot.Focusable = IsInteractive;
                plot.IsHitTestVisible = IsInteractive;
            }
            if (!IsInteractive)
            {
                SetActive(null);
            }
        }
        else if (change.Property == DrawingWidthProperty || change.Property == DrawingHeightProperty)
        {
            InvalidatePlotLayout();
        }
    }

    private void UpdatePseudoClasses()
    {
        PseudoClasses.Set(SuccessPseudoClass, Tone == ChartTone.Success);
        PseudoClasses.Set(WarningPseudoClass, Tone == ChartTone.Warning);
        PseudoClasses.Set(DangerPseudoClass, Tone == ChartTone.Danger);
        PseudoClasses.Set(ScrubbingPseudoClass, ActiveIndex is not null);
    }

    #endregion

    #region Caption

    private string? PointLabel(int index)
    {
        if (PointLabels is { } labels)
        {
            return index < labels.Count ? labels[index] : null;
        }
        var data = Data;
        return index < data.Count && data.X[index] is not null and not int ? data.Labels[index] : null;
    }

    private string FormatPoint(int index)
    {
        var values = Values;
        return values.Length == 0 ? FormatValue(0) : FormatValue(values[Math.Min(index, values.Length - 1)], index);
    }

    /// <summary>Copy that holds a number enters from the side it moved toward.</summary>
    private void UpdateCaption(int direction)
    {
        var index = ActiveIndex;
        var scrubbing = index is not null;
        var at = index ?? Last;
        var point = FormatPoint(at);
        var when = PointLabel(at);
        if (_label is not null)
        {
            _label.Content = Label;
        }
        if (_headline is not null)
        {
            _headline.IsVisible = Value is not null;
            _headline.Direction = direction;
            _headline.Content = Value is null ? null : scrubbing ? point : Value;
        }
        if (_aside is not null)
        {
            _aside.Direction = scrubbing ? direction : 1;
            _aside.Content = index is null
                ? Change ?? string.Empty
                : Value is null
                    ? string.Join(" · ", new[] { point, when }.Where(s => !string.IsNullOrEmpty(s)))
                    : when ?? $"{at + 1} of {Last + 1}";
        }
    }

    private void UpdateAutomation()
    {
        var values = Values;
        var latest = Value ?? (values.Length > 0 ? FormatPoint(Last) : string.Empty);
        AutomationProperties.SetName(this, $"{Label}: {latest}. {Change ?? string.Empty}".Trim());
    }

    private protected override string BuildSummary()
    {
        var values = Values;
        return values.Length == 0 ? $"{Label}. {EmptyLabel}." : $"{Label}: {Value ?? FormatPoint(Last)}. {Change ?? string.Empty}".Trim();
    }

    internal override AutomationControlType GetPlotControlType(ChartPlot plot) => AutomationControlType.Slider;

    internal override string GetPlotAutomationName(ChartPlot plot) => $"{Label}, explore values";

    internal override string GetPlotValueText(ChartPlot plot)
    {
        var index = ActiveIndex;
        var at = index ?? Last;
        var when = PointLabel(at) ?? (index is null ? "Latest" : $"Point {at + 1} of {Last + 1}");
        return $"{when}: {FormatPoint(at)}";
    }

    #endregion

    #region Scrubbing

    private protected override void OnActiveIndexChanged(int? previous, int? current)
    {
        var last = Last;
        var to = current is { } index && last > 0 ? (double)Math.Min(index, last) / last : 1;
        var full = MotionLevel == ArcMotionLevel.Full;
        // The cursor springs to the scrubbed point and rides the line between points; leaving sends it home.
        _ = Crosshair.AnimateTo(to, full ? MotionSpec.Spring(Snappy) : MotionSpec.Instant);
        var quick = MotionLevel == ArcMotionLevel.Off ? MotionSpec.Instant : MotionSpec.Tween(ArcMotionPresets.Fast, ArcEasings.Standard);
        _ = Scrub.AnimateTo(current is null ? 0 : 1, quick);
        if (_landed)
        {
            _ = Dot.AnimateTo(current is null ? 7 : 10, full ? MotionSpec.Spring(Snappy) : MotionSpec.Instant);
        }
        UpdateTrail();
        var direction = current is null || _lastIndex is null ? 1 : Math.Sign(current.Value - _lastIndex.Value) is var s && s != 0 ? s : 1;
        _lastIndex = current;
        UpdatePseudoClasses();
        UpdateCaption(direction);
    }

    private void UpdateTrail()
    {
        var quick = MotionLevel == ArcMotionLevel.Off ? MotionSpec.Instant : MotionSpec.Tween(ArcMotionPresets.Fast, ArcEasings.Standard);
        _ = Trail.AnimateTo(ActiveIndex is not null && _landed ? 1 : 0, quick);
    }

    private int PointAt(ChartPlot plot, double x)
    {
        var width = plot.Bounds.Width;
        var last = Last;
        return width > 0 ? Math.Clamp((int)Math.Round(x / width * last), 0, last) : last;
    }

    internal override void OnPlotPointerPressed(ChartPlot plot, PointerPressedEventArgs e)
    {
        if (!IsInteractive || Data.IsEmpty)
        {
            return;
        }
        SetActive(PointAt(plot, e.GetPosition(plot).X));
    }

    internal override void OnPlotPointerMoved(ChartPlot plot, PointerEventArgs e)
    {
        if (!IsInteractive || Data.IsEmpty)
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
            SetActive(null);
        }
    }

    internal override void OnPlotPointerExited(ChartPlot plot, PointerEventArgs e) => SetActive(null);

    internal override void OnPlotPointerCaptureLost(ChartPlot plot, PointerCaptureLostEventArgs e) => SetActive(null);

    internal override void OnPlotKeyDown(ChartPlot plot, KeyEventArgs e)
    {
        if (!IsInteractive || Data.IsEmpty)
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
        var last = Last;
        var from = ActiveIndex ?? last;
        var page = Math.Max(1, (int)Math.Round((last + 1) / 6.0));
        int? next = e.Key switch
        {
            Key.Left or Key.Down => from - 1,
            Key.Right or Key.Up => from + 1,
            Key.PageDown => from - page,
            Key.PageUp => from + page,
            Key.Home => 0,
            Key.End => last,
            _ => null
        };
        if (next is { } index)
        {
            e.Handled = true;
            SetActive(Math.Clamp(index, 0, last));
        }
    }

    internal override void OnPlotFocusChanged(ChartPlot plot, bool focused, bool keyboard)
    {
        if (!focused)
        {
            SetActive(null);
        }
        plot.InvalidateVisual();
    }

    #endregion

    #region Rendering

    internal override double MeasurePlotHeight(ChartPlot plot, double width)
        => DrawingWidth > 0 ? width * DrawingHeight / DrawingWidth : DrawingHeight;

    internal override void OnPlotResized(ChartPlot plot, Size size) => _geometryDirty = true;

    private double ToY(double y, double height) => Inset + (1 - y) * (height - Inset * 2);

    private void RebuildGeometry(Size size)
    {
        _geometryDirty = false;
        _geometrySize = size;
        var morph = Morph;
        var grid = morph.Grid;
        if (grid.Length == 0)
        {
            _line = null;
            _area = null;
            return;
        }
        if (_points.Length != grid.Length)
        {
            _points = new Point[grid.Length];
            _lengths = new double[grid.Length];
        }
        var values = morph.Values(0);
        for (var k = 0; k < grid.Length; k++)
        {
            _points[k] = new Point(grid[k] * size.Width, ToY(values[k], size.Height));
            _lengths[k] = k == 0 ? 0 : _lengths[k - 1] + Distance(_points[k - 1], _points[k]);
        }
        _line = ChartGeometry.Line(_points);
        _area = ChartGeometry.Area(_points, size.Height);
    }

    private static double Distance(Point a, Point b)
    {
        var dx = b.X - a.X;
        var dy = b.Y - a.Y;
        return Math.Sqrt(dx * dx + dy * dy);
    }

    /// <summary>Where the pen is along the line once <paramref name="progress"/> of its length is drawn.</summary>
    private Point TipAt(double progress)
    {
        if (_points.Length == 0)
        {
            return default;
        }
        var total = _lengths[^1];
        var left = progress * total;
        for (var k = 1; k < _points.Length; k++)
        {
            if (_lengths[k] >= left)
            {
                var step = _lengths[k] - _lengths[k - 1];
                var t = step > 0 ? (left - _lengths[k - 1]) / step : 1;
                return new Point(_points[k - 1].X + (_points[k].X - _points[k - 1].X) * t, _points[k - 1].Y + (_points[k].Y - _points[k - 1].Y) * t);
            }
        }
        return _points[^1];
    }

    private protected override void RenderPlot(ChartPlot plot, DrawingContext context, Size size)
    {
        var palette = Palette;
        if (size.Width <= 0 || size.Height <= 0)
        {
            return;
        }
        if (_geometryDirty || _geometrySize != size)
        {
            RebuildGeometry(size);
        }
        var w = size.Width;
        var h = size.Height;
        var tone = palette.ToneColor(Tone);
        var progress = RevealProgress;
        var reveal = progress >= 1 ? w + 32 : TipAt(progress).X + 16;
        var at = Crosshair.Value;
        var cx = at * w;
        var cy = ToY(Morph.ValueAt(0, at), h);
        using var fade = context.PushOpacity(RevealFade);
        if (ShowArea && _area is not null)
        {
            using (context.PushClip(new Rect(-16, -16, Math.Max(0, reveal), h + 32)))
            {
                context.DrawGeometry(palette.Brush(Oklch.WithAlpha(palette.ToneTextColor(Tone), 0.09)), null, _area);
            }
        }
        context.DrawLine(palette.Pen(palette.Border, 1), new Point(0, h - 0.5), new Point(w, h - 0.5));
        if (_line is not null)
        {
            var stroke = StrokeWidth;
            var trail = Trail.Value;
            if (trail > 0.001)
            {
                // While scrubbing, the stretch after the cursor dims, as if the later points have not happened yet.
                using (context.PushClip(new Rect(cx + Cap, -16, w + 32, h + 32)))
                using (context.PushOpacity(trail))
                {
                    context.DrawGeometry(null, palette.Pen(palette.BorderStrong, stroke, 0, PenLineCap.Round), _line);
                }
            }
            if (progress > 0.001)
            {
                // Lit up to the cursor (the latest point at rest), and up to the pen tip while drawing.
                var right = progress >= 1 ? cx + Cap : Math.Min(cx + Cap, TipAt(progress).X);
                using (context.PushClip(new Rect(-16, -16, Math.Max(0, right + 16), h + 32)))
                {
                    context.DrawGeometry(null, palette.Pen(tone, stroke, 0, PenLineCap.Round), _line);
                }
                if (progress < 1)
                {
                    // The clip cuts the stroke; a round cap at the pen tip keeps it reading as a drawn line.
                    context.DrawEllipse(palette.Brush(tone), null, TipAt(progress), stroke / 2, stroke / 2);
                }
            }
        }
        var scrub = Scrub.Value;
        if (scrub > 0.001)
        {
            using (context.PushOpacity(scrub))
            {
                var x = Math.Round(cx) + 0.5;
                context.DrawLine(palette.Pen(palette.BorderStrong, 1), new Point(x, 0), new Point(x, h));
            }
        }
        // A round dot takes the line's color; it rests on the latest point and becomes the scrub handle.
        var dot = Dot.Value;
        var dotOpacity = DotOpacity.Value;
        if (dot > 0.01 && dotOpacity > 0.001)
        {
            using (context.PushOpacity(dotOpacity))
            {
                context.DrawEllipse(palette.Brush(tone), null, new Point(cx, cy), dot / 2, dot / 2);
            }
        }
        if (plot.IsFocusVisible)
        {
            context.DrawRectangle(null, palette.Pen(palette.FocusRing, 2), new RoundedRect(new Rect(size).Inflate(8), 9 + 8));
        }
    }

    #endregion
}
