using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using TKCode.Arc.Motion;

namespace TKCode.Arc;

/// <summary>
/// The chart of an <see cref="AiInsightCard"/>: the metric as a line over a soft area, the period before as a dashed line,
/// and a band over the stretch the insight (or the selected cause) talks about, inside which the line takes the change's
/// color. It draws itself in one pass from cached geometry; the band glides between stretches on a spring and the line
/// draws in from the left the first time it shows (full motion only). Pointer or arrow keys pick a point, reported by
/// <see cref="HoverChanged"/> so the card can show its readout.
/// </summary>
internal sealed class AiInsightCardChart : Control
{
    public static readonly StyledProperty<IBrush?> LineBrushProperty =
        AvaloniaProperty.Register<AiInsightCardChart, IBrush?>(nameof(LineBrush));

    public static readonly StyledProperty<IBrush?> AreaBrushProperty =
        AvaloniaProperty.Register<AiInsightCardChart, IBrush?>(nameof(AreaBrush));

    public static readonly StyledProperty<IBrush?> ComparisonBrushProperty =
        AvaloniaProperty.Register<AiInsightCardChart, IBrush?>(nameof(ComparisonBrush));

    public static readonly StyledProperty<IBrush?> GridBrushProperty =
        AvaloniaProperty.Register<AiInsightCardChart, IBrush?>(nameof(GridBrush));

    public static readonly StyledProperty<IBrush?> BandBrushProperty =
        AvaloniaProperty.Register<AiInsightCardChart, IBrush?>(nameof(BandBrush));

    public static readonly StyledProperty<IBrush?> ChangeBrushProperty =
        AvaloniaProperty.Register<AiInsightCardChart, IBrush?>(nameof(ChangeBrush));

    public static readonly StyledProperty<IBrush?> DotBrushProperty =
        AvaloniaProperty.Register<AiInsightCardChart, IBrush?>(nameof(DotBrush));

    public static readonly StyledProperty<IBrush?> FocusBrushProperty =
        AvaloniaProperty.Register<AiInsightCardChart, IBrush?>(nameof(FocusBrush));

    public static readonly StyledProperty<double> LineThicknessProperty =
        AvaloniaProperty.Register<AiInsightCardChart, double>(nameof(LineThickness), 2);

    public static readonly StyledProperty<double> AreaOpacityProperty =
        AvaloniaProperty.Register<AiInsightCardChart, double>(nameof(AreaOpacity), 0.5);

    public static readonly StyledProperty<double> DotRadiusProperty =
        AvaloniaProperty.Register<AiInsightCardChart, double>(nameof(DotRadius), 4);

    private IReadOnlyList<double> _points = [];
    private IReadOnlyList<double>? _comparison;
    private Geometry? _line;
    private Geometry? _area;
    private Geometry? _comparisonLine;
    private Size _builtFor;
    private bool _dirty = true;
    private double _min;
    private double _max;
    private Pen? _linePen;
    private Pen? _changePen;
    private Pen? _comparisonPen;
    private Pen? _gridPen;
    private Pen? _guidePen;
    private Pen? _focusPen;
    private MotionValue<Point>? _band;
    private MotionValue<double>? _reveal;
    private Point _bandValue = new(-1, -1);
    private double _revealValue = 1;
    private bool _revealed;
    private int _hover = -1;

    static AiInsightCardChart()
    {
        FocusableProperty.OverrideDefaultValue<AiInsightCardChart>(true);
        ClipToBoundsProperty.OverrideDefaultValue<AiInsightCardChart>(false);
        AffectsRender<AiInsightCardChart>(LineBrushProperty, AreaBrushProperty, ComparisonBrushProperty, GridBrushProperty,
            BandBrushProperty, ChangeBrushProperty, DotBrushProperty, FocusBrushProperty, AreaOpacityProperty, DotRadiusProperty);
    }

    public IBrush? LineBrush { get => GetValue(LineBrushProperty); set => SetValue(LineBrushProperty, value); }

    public IBrush? AreaBrush { get => GetValue(AreaBrushProperty); set => SetValue(AreaBrushProperty, value); }

    public IBrush? ComparisonBrush { get => GetValue(ComparisonBrushProperty); set => SetValue(ComparisonBrushProperty, value); }

    public IBrush? GridBrush { get => GetValue(GridBrushProperty); set => SetValue(GridBrushProperty, value); }

    public IBrush? BandBrush { get => GetValue(BandBrushProperty); set => SetValue(BandBrushProperty, value); }

    public IBrush? ChangeBrush { get => GetValue(ChangeBrushProperty); set => SetValue(ChangeBrushProperty, value); }

    public IBrush? DotBrush { get => GetValue(DotBrushProperty); set => SetValue(DotBrushProperty, value); }

    public IBrush? FocusBrush { get => GetValue(FocusBrushProperty); set => SetValue(FocusBrushProperty, value); }

    public double LineThickness { get => GetValue(LineThicknessProperty); set => SetValue(LineThicknessProperty, value); }

    public double AreaOpacity { get => GetValue(AreaOpacityProperty); set => SetValue(AreaOpacityProperty, value); }

    public double DotRadius { get => GetValue(DotRadiusProperty); set => SetValue(DotRadiusProperty, value); }

    /// <summary>Raised with the picked point's index (−1 when none is picked).</summary>
    public event Action<int>? HoverChanged;

    /// <summary>The picked point; −1 when none is.</summary>
    public int HoverIndex => _hover;

    /// <summary>The band's current start and end in point units (−1 when there is none); follows the spring.</summary>
    internal Point BandValue => _bandValue;

    /// <summary>How much of the line is drawn, 0 to 1.</summary>
    internal double Reveal => _revealValue;

    public int Count => _points.Count;

    public void SetData(IReadOnlyList<double>? points, IReadOnlyList<double>? comparison)
    {
        _points = points ?? [];
        _comparison = comparison is { Count: > 1 } ? comparison : null;
        _dirty = true;
        TryReveal();
        if (_hover >= _points.Count)
        {
            SetHover(-1);
        }
        InvalidateVisual();
    }

    /// <summary>Moves the band to <paramref name="range"/> (null hides it), on a spring when <paramref name="animate"/>.</summary>
    public void SetBand(AiInsightRange? range, bool animate)
    {
        var target = range is null || _points.Count == 0
            ? new Point(-1, -1)
            : new Point(Math.Clamp(Math.Min(range.Start, range.End), 0, _points.Count - 1),
                Math.Clamp(Math.Max(range.Start, range.End), 0, _points.Count - 1));
        // A band that appears or disappears does not fly in from nowhere.
        if (!animate || target.X < 0 || _bandValue.X < 0)
        {
            _band?.Jump(target);
            _bandValue = target;
            InvalidateVisual();
            return;
        }
        _band ??= CreateBand();
        _ = _band.AnimateTo(target, MotionSpec.Spring(ArcSpring.Snappy));
    }

    private MotionValue<Point> CreateBand()
    {
        var band = new MotionValue<Point>(this, _bandValue, MotionVectors.Point);
        band.Changed += value =>
        {
            _bandValue = value;
            InvalidateVisual();
        };
        return band;
    }

    public void SetHover(int index)
    {
        index = _points.Count == 0 ? -1 : Math.Clamp(index, -1, _points.Count - 1);
        if (index == _hover)
        {
            return;
        }
        _hover = index;
        InvalidateVisual();
        HoverChanged?.Invoke(index);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == LineBrushProperty || change.Property == LineThicknessProperty)
        {
            _linePen = null;
            _guidePen = null;
        }
        else if (change.Property == ChangeBrushProperty)
        {
            _changePen = null;
        }
        else if (change.Property == ComparisonBrushProperty)
        {
            _comparisonPen = null;
        }
        else if (change.Property == GridBrushProperty)
        {
            _gridPen = null;
        }
        else if (change.Property == FocusBrushProperty)
        {
            _focusPen = null;
        }
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        TryReveal();
    }

    // The line draws in from the left once, the first time the chart is on screen with data (480ms); reduced motion and
    // Off show it whole.
    private void TryReveal()
    {
        if (_revealed || _points.Count < 2 || TopLevel.GetTopLevel(this) is null)
        {
            return;
        }
        _revealed = true;
        if (!ArcMotion.IsFull(this))
        {
            return;
        }
        _reveal = new MotionValue<double>(this, 0, MotionVectors.Double);
        _reveal.Changed += value =>
        {
            _revealValue = value;
            InvalidateVisual();
        };
        _revealValue = 0;
        _ = _reveal.AnimateTo(1, MotionSpec.Tween(ArcMotionPresets.Considered, ArcEasings.Standard));
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _reveal?.Jump(1);
        _revealValue = 1;
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        if (e.Pointer.Type == PointerType.Touch && !e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            return;
        }
        SetHover(IndexAt(e.GetPosition(this).X));
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        SetHover(IndexAt(e.GetPosition(this).X));
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        if (!IsKeyboardFocusWithin)
        {
            SetHover(-1);
        }
    }

    protected override void OnLostFocus(FocusChangedEventArgs e)
    {
        base.OnLostFocus(e);
        SetHover(-1);
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
        if (_points.Count == 0 || e.KeyModifiers != KeyModifiers.None)
        {
            return;
        }
        var last = _points.Count - 1;
        var current = _hover < 0 ? last : _hover;
        int? next = e.Key switch
        {
            Key.Left => _hover < 0 ? last : Math.Max(0, current - 1),
            Key.Right => _hover < 0 ? last : Math.Min(last, current + 1),
            Key.Home => 0,
            Key.End => last,
            Key.Escape when _hover >= 0 => -1,
            _ => null,
        };
        if (next is { } index)
        {
            SetHover(index);
            e.Handled = true;
        }
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new AiInsightCardChartPeer(this);

    private double Step(double width) => _points.Count > 1 ? width / (_points.Count - 1) : 0;

    private int IndexAt(double x)
    {
        if (_points.Count == 0)
        {
            return -1;
        }
        var step = Step(Bounds.Width);
        return step <= 0 ? 0 : Math.Clamp((int)Math.Round(x / step), 0, _points.Count - 1);
    }

    private Rect Plot => new(0, DotRadius + 2, Bounds.Width, Math.Max(0, Bounds.Height - 2 * (DotRadius + 2)));

    private double YOf(double value, Rect plot)
        => _max - _min <= 0 ? plot.Center.Y : plot.Bottom - (value - _min) / (_max - _min) * plot.Height;

    private void Build()
    {
        var size = Bounds.Size;
        if (!_dirty && size == _builtFor)
        {
            return;
        }
        _dirty = false;
        _builtFor = size;
        _line = _area = _comparisonLine = null;
        if (_points.Count < 2 || size.Width <= 0 || size.Height <= 0)
        {
            return;
        }
        _min = double.MaxValue;
        _max = double.MinValue;
        Extend(_points);
        if (_comparison is not null)
        {
            Extend(_comparison);
        }
        var pad = (_max - _min) * 0.12;
        _min -= pad;
        _max += pad;
        var plot = Plot;
        _line = Polyline(_points, plot, false);
        _area = Polyline(_points, plot, true);
        if (_comparison is not null)
        {
            _comparisonLine = Polyline(_comparison, plot, false);
        }
    }

    private void Extend(IReadOnlyList<double> values)
    {
        foreach (var v in values)
        {
            if (double.IsFinite(v))
            {
                _min = Math.Min(_min, v);
                _max = Math.Max(_max, v);
            }
        }
    }

    private Geometry Polyline(IReadOnlyList<double> values, Rect plot, bool closed)
    {
        var geometry = new StreamGeometry();
        using var context = geometry.Open();
        var count = Math.Min(values.Count, _points.Count);
        var step = Step(plot.Width);
        context.BeginFigure(new Point(0, YOf(values[0], plot)), closed);
        for (var i = 1; i < count; i++)
        {
            context.LineTo(new Point(i * step, YOf(values[i], plot)));
        }
        if (closed)
        {
            context.LineTo(new Point((count - 1) * step, plot.Bottom + DotRadius + 2));
            context.LineTo(new Point(0, plot.Bottom + DotRadius + 2));
        }
        context.EndFigure(closed);
        return geometry;
    }

    public override void Render(DrawingContext context)
    {
        var bounds = new Rect(Bounds.Size);
        context.FillRectangle(Brushes.Transparent, bounds);
        Build();
        var plot = Plot;
        _gridPen ??= new Pen(GridBrush, 1, new DashStyle([2, 3], 0));
        for (var i = 0; i < 3; i++)
        {
            var y = Math.Round(plot.Top + plot.Height * i / 2) + 0.5;
            context.DrawLine(_gridPen, new Point(0, y), new Point(bounds.Width, y));
        }
        if (_line is null)
        {
            DrawFocus(context, bounds);
            return;
        }
        var step = Step(plot.Width);
        Rect? band = null;
        if (_bandValue.X >= 0 && BandBrush is { } bandBrush)
        {
            var half = Math.Min(step / 2, 10);
            var left = Math.Max(0, _bandValue.X * step - half);
            var right = Math.Min(bounds.Width, _bandValue.Y * step + half);
            band = new Rect(left, 0, Math.Max(0, right - left), bounds.Height);
            context.FillRectangle(bandBrush, band.Value);
        }
        using (context.PushClip(new Rect(-DotRadius - 2, -DotRadius - 2, bounds.Width * _revealValue + DotRadius + 2, bounds.Height + 2 * DotRadius + 4)))
        {
            if (_comparisonLine is not null)
            {
                _comparisonPen ??= new Pen(ComparisonBrush, 1.5, new DashStyle([4, 4], 0), PenLineCap.Round);
                context.DrawGeometry(null, _comparisonPen, _comparisonLine);
            }
            if (AreaBrush is { } area)
            {
                using (context.PushOpacity(AreaOpacity))
                {
                    context.DrawGeometry(area, null, _area!);
                }
            }
            _linePen ??= new Pen(LineBrush, LineThickness, null, PenLineCap.Round, PenLineJoin.Round);
            context.DrawGeometry(null, _linePen, _line);
            if (band is { } b && ChangeBrush is not null)
            {
                _changePen ??= new Pen(ChangeBrush, LineThickness, null, PenLineCap.Round, PenLineJoin.Round);
                using (context.PushClip(b))
                {
                    context.DrawGeometry(null, _changePen, _line);
                }
            }
        }
        var last = _points.Count - 1;
        if (_revealValue >= 1)
        {
            DrawDot(context, last, plot, step);
        }
        if (_hover >= 0)
        {
            _guidePen ??= new Pen(LineBrush, 1, new DashStyle([2, 2], 0));
            var x = Math.Round(_hover * step) + 0.5;
            context.DrawLine(_guidePen, new Point(x, 0), new Point(x, bounds.Height));
            if (_hover != last || _revealValue < 1)
            {
                DrawDot(context, _hover, plot, step);
            }
        }
        DrawFocus(context, bounds);
    }

    private void DrawDot(DrawingContext context, int index, Rect plot, double step)
    {
        var inBand = _bandValue.X >= 0 && index >= _bandValue.X - 0.01 && index <= _bandValue.Y + 0.01;
        var pen = inBand && ChangeBrush is not null
            ? _changePen ??= new Pen(ChangeBrush, LineThickness, null, PenLineCap.Round, PenLineJoin.Round)
            : _linePen ??= new Pen(LineBrush, LineThickness, null, PenLineCap.Round, PenLineJoin.Round);
        var center = new Point(index * step, YOf(_points[index], plot));
        context.DrawEllipse(DotBrush, pen, center, DotRadius, DotRadius);
    }

    private void DrawFocus(DrawingContext context, Rect bounds)
    {
        if (!Classes.Contains(":focus-visible") || FocusBrush is null)
        {
            return;
        }
        _focusPen ??= new Pen(FocusBrush, 2);
        var radius = ArcTokens.GetCornerRadius(this, DesignTokens.ArcDesignTokenKind.RadiusStep6, new CornerRadius(6)).TopLeft;
        context.DrawRectangle(null, _focusPen, bounds.Inflate(4), radius, radius);
    }

    /// <summary>The chart reads as an image named by the card, with the picked point as its value.</summary>
    private sealed class AiInsightCardChartPeer(AiInsightCardChart owner) : ControlAutomationPeer(owner)
    {
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Image;

        protected override bool IsContentElementCore() => true;

        protected override bool IsControlElementCore() => true;

        protected override string? GetHelpTextCore() => AutomationProperties.GetHelpText(Owner);
    }
}
