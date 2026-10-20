using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;
using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Avalonia.Controls;

namespace TKCode.Arc;

/// <summary>
/// The optional trend line of a <see cref="MetricCard"/>: a 2.5px round-capped line over a 9% wash of its color,
/// ending in a 7px dot. It draws itself in once when it first comes into view (slow at both ends, like a pen) and
/// crossfades to new data. Geometry is built when the data or size changes, never per frame.
/// </summary>
internal sealed class MetricCardSparkline : Control
{
    public static readonly StyledProperty<IList<double>?> ValuesProperty =
        AvaloniaProperty.Register<MetricCardSparkline, IList<double>?>(nameof(Values));

    public static readonly StyledProperty<IBrush?> StrokeProperty =
        AvaloniaProperty.Register<MetricCardSparkline, IBrush?>(nameof(Stroke));

    public IList<double>? Values { get => GetValue(ValuesProperty); set => SetValue(ValuesProperty, value); }
    public IBrush? Stroke { get => GetValue(StrokeProperty); set => SetValue(StrokeProperty, value); }

    private const double LineWidth = 2.5;
    private const double Dot = 7;
    private const double Inset = 4;

    private sealed class Shape
    {
        public StreamGeometry? Line;
        public StreamGeometry? Area;
        public Point End;
    }

    private readonly MotionValue<double> _reveal;
    private readonly MotionValue<double> _swap;
    private Shape? _current;
    private Shape? _previous;
    private Size _builtFor;
    private IList<double>? _builtFrom;
    private Pen? _pen;
    private Pen? _dotPen;
    private IBrush? _penBrush;
    private IBrush? _wash;
    private ViewportVisibility? _visibility;
    private bool _revealed;

    static MetricCardSparkline()
    {
        AffectsRender<MetricCardSparkline>(StrokeProperty);
        IsHitTestVisibleProperty.OverrideDefaultValue<MetricCardSparkline>(false);
    }

    public MetricCardSparkline()
    {
        _reveal = new MotionValue<double>(this, 0, MotionVectors.Double);
        _reveal.Changed += _ => InvalidateVisual();
        _swap = new MotionValue<double>(this, 1, MotionVectors.Double);
        _swap.Changed += _ => InvalidateVisual();
    }

    internal double RevealProgress => _reveal.Value;

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        if (_revealed)
        {
            return;
        }
        if (!ArcMotion.IsFull(this))
        {
            Reveal(animate: false);
            return;
        }
        _visibility = new ViewportVisibility(this, 0.5);
        _visibility.VisibilityChanged += (_, visible) =>
        {
            if (visible)
            {
                Reveal(animate: true);
            }
        };
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _visibility?.Dispose();
        _visibility = null;
        if (!_revealed)
        {
            _reveal.Jump(0);
        }
    }

    private void Reveal(bool animate)
    {
        _revealed = true;
        _visibility?.Dispose();
        _visibility = null;
        if (animate && IsLoaded)
        {
            _reveal.Jump(0);
            _ = _reveal.AnimateTo(1, MotionSpec.Tween(TimeSpan.FromSeconds(0.9), ArcEasings.InOut));
        }
        else
        {
            _reveal.Jump(1);
        }
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ValuesProperty && _current is not null)
        {
            var level = ArcMotion.GetEffectiveLevel(this);
            _previous = level == ArcMotionLevel.Off || !_revealed ? null : _current;
            _current = null;
            _builtFrom = null;
            if (_previous is not null)
            {
                _swap.Jump(0);
                _ = _swap.AnimateTo(1, MotionSpec.Tween(level == ArcMotionLevel.Full ? ArcMotionPresets.Standard : ArcMotionPresets.Instant, ArcEasings.Standard));
            }
            InvalidateVisual();
        }
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        if (finalSize != _builtFor)
        {
            _current = null;
            _previous = null;
        }
        return base.ArrangeOverride(finalSize);
    }

    private Shape? Build()
    {
        var values = Values;
        var size = Bounds.Size;
        if (values is null || values.Count < 2 || size.Width <= 0 || size.Height <= 0)
        {
            return null;
        }
        if (_current is not null && ReferenceEquals(_builtFrom, values) && _builtFor == size)
        {
            return _current;
        }
        var min = double.MaxValue;
        var max = double.MinValue;
        foreach (var v in values)
        {
            min = Math.Min(min, v);
            max = Math.Max(max, v);
        }
        var range = max - min < 1e-9 ? 1 : max - min;
        var width = size.Width - 2 * Inset;
        var height = size.Height - 2 * Inset;
        Point At(int i) => new(Inset + width * i / (values.Count - 1), Inset + height * (1 - (values[i] - min) / range));
        var line = new StreamGeometry();
        using (var ctx = line.Open())
        {
            ctx.BeginFigure(At(0), false);
            for (var i = 1; i < values.Count; i++)
            {
                ctx.LineTo(At(i));
            }
            ctx.EndFigure(false);
        }
        var area = new StreamGeometry();
        using (var ctx = area.Open())
        {
            ctx.BeginFigure(new Point(Inset, size.Height), true);
            for (var i = 0; i < values.Count; i++)
            {
                ctx.LineTo(At(i));
            }
            ctx.LineTo(new Point(Inset + width, size.Height));
            ctx.EndFigure(true);
        }
        _builtFor = size;
        _builtFrom = values;
        _current = new Shape { Line = line, Area = area, End = At(values.Count - 1) };
        return _current;
    }

    public override void Render(DrawingContext context)
    {
        var stroke = Stroke;
        var shape = Build();
        if (stroke is null || shape is null)
        {
            return;
        }
        if (!ReferenceEquals(stroke, _penBrush) || _pen is null)
        {
            _penBrush = stroke;
            _pen = new Pen(stroke, LineWidth, lineCap: PenLineCap.Round, lineJoin: PenLineJoin.Round);
            _dotPen = new Pen(stroke, Dot, lineCap: PenLineCap.Round);
            var color = stroke is ISolidColorBrush solid ? solid.Color : Colors.Gray;
            _wash = new ImmutableSolidColorBrush(Color.FromArgb((byte)(color.A * 0.09), color.R, color.G, color.B));
        }
        var reveal = Math.Clamp(_reveal.Value, 0, 1);
        if (reveal <= 0)
        {
            return;
        }
        var swap = Math.Clamp(_swap.Value, 0, 1);
        using (context.PushClip(new Rect(-Dot, -Dot, (Bounds.Width + 2 * Dot) * reveal, Bounds.Height + 2 * Dot)))
        {
            if (_previous is not null && swap < 1)
            {
                using (context.PushOpacity(1 - swap))
                {
                    DrawShape(context, _previous);
                }
            }
            using (context.PushOpacity(_previous is null ? 1 : swap))
            {
                DrawShape(context, shape);
            }
        }
        if (swap >= 1)
        {
            _previous = null;
        }
    }

    private void DrawShape(DrawingContext context, Shape shape)
    {
        context.DrawGeometry(_wash, null, shape.Area!);
        context.DrawGeometry(null, _pen, shape.Line!);
        // A zero-length round-capped stroke is a true circle in the line's color.
        context.DrawLine(_dotPen!, shape.End, shape.End);
    }
}
