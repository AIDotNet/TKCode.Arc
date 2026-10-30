using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using TKCode.Arc.Motion;

namespace TKCode.Arc;

/// <summary>
/// The vertical stack of an <see cref="AiWorkflow"/>'s steps, which also draws the rail behind them: one 1px hairline
/// through every mark (the marks sit on Surface so the line passes behind them). A connector the run has passed is
/// filled with the accent. While the run moves on, a 5px accent dot glides down the next connector in 240ms and the
/// line fills behind it. Reduced motion: no dot and no growth (the connector fills at once). Off: the same.
/// <para>
/// Drawn in code because the connectors join marks that live in different rows and in the branch's path list, and the
/// dot's progress is one <see cref="MotionValue{T}"/>; a ControlTheme cannot express either.
/// </para>
/// </summary>
internal sealed class AiWorkflowRail : Control
{
    public static readonly StyledProperty<IBrush?> LineBrushProperty =
        AvaloniaProperty.Register<AiWorkflowRail, IBrush?>(nameof(LineBrush));

    public static readonly StyledProperty<IBrush?> FillBrushProperty =
        AvaloniaProperty.Register<AiWorkflowRail, IBrush?>(nameof(FillBrush));

    private readonly List<(double Y, AiWorkflowStepRow Row)> _marks = [];
    private MotionValue<double>? _travel;
    private string? _travelTo;
    private Pen? _linePen;
    private Pen? _fillPen;

    static AiWorkflowRail()
    {
        AffectsRender<AiWorkflowRail>(LineBrushProperty, FillBrushProperty);
    }

    public AiWorkflowRail()
    {
        Children.CollectionChanged += (_, e) =>
        {
            if (e.OldItems is not null)
            {
                foreach (Control old in e.OldItems)
                {
                    LogicalChildren.Remove(old);
                    VisualChildren.Remove(old);
                }
            }
            if (e.NewItems is not null)
            {
                foreach (Control added in e.NewItems)
                {
                    LogicalChildren.Add(added);
                    VisualChildren.Add(added);
                }
            }
            InvalidateMeasure();
        };
    }

    /// <summary>The stacked children: row shells and the branch's path list.</summary>
    public Avalonia.Controls.Controls Children { get; } = [];

    public IBrush? LineBrush { get => GetValue(LineBrushProperty); set => SetValue(LineBrushProperty, value); }

    public IBrush? FillBrush { get => GetValue(FillBrushProperty); set => SetValue(FillBrushProperty, value); }

    /// <summary>The rows on the rail, in order (the owner gives the visible ones).</summary>
    public Func<IEnumerable<AiWorkflowStepRow>>? Rows { get; set; }

    /// <summary>Whether a row's incoming connector is filled (the run reached it).</summary>
    public Func<AiWorkflowStepRow, bool>? IsReached { get; set; }

    /// <summary>The dot's progress down the connector it travels (tests).</summary>
    internal double TravelProgress => _travel?.Value ?? 0;

    /// <summary>The step the dot travels into, or null.</summary>
    internal string? TravelingTo => _travelTo;

    /// <summary>The centers of the marks, last drawn (tests).</summary>
    internal IReadOnlyList<(double Y, AiWorkflowStepRow Row)> Marks => _marks;

    /// <summary>Starts the dot down the connector into <paramref name="stepId"/>.</summary>
    public void Travel(string stepId, TimeSpan duration)
    {
        _travelTo = stepId;
        if (_travel is null)
        {
            _travel = new MotionValue<double>(this, 0, MotionVectors.Double);
            _travel.Changed += _ => InvalidateVisual();
        }
        _travel.Jump(0);
        if (IsLoaded && ArcMotion.GetEffectiveLevel(this) == ArcMotionLevel.Full)
        {
            _ = _travel.AnimateTo(1, MotionSpec.Tween(duration, ArcEasings.InOut));
        }
        else
        {
            _travel.Jump(1);
        }
        InvalidateVisual();
    }

    /// <summary>Ends the travel (the step started, or the run stopped).</summary>
    public void EndTravel()
    {
        _travelTo = null;
        _travel?.Jump(0);
        InvalidateVisual();
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        double width = 0;
        double height = 0;
        foreach (var child in Children)
        {
            child.Measure(availableSize.WithHeight(double.PositiveInfinity));
            width = Math.Max(width, child.DesiredSize.Width);
            height += child.DesiredSize.Height;
        }
        return new Size(width, height);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        double y = 0;
        foreach (var child in Children)
        {
            var h = child.DesiredSize.Height;
            child.Arrange(new Rect(0, y, finalSize.Width, h));
            y += h;
        }
        InvalidateVisual();
        return finalSize;
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == LineBrushProperty)
        {
            _linePen = null;
        }
        else if (change.Property == FillBrushProperty)
        {
            _fillPen = null;
        }
    }

    private void CollectMarks()
    {
        _marks.Clear();
        if (Rows is null)
        {
            return;
        }
        foreach (var row in Rows())
        {
            if (!row.IsEffectivelyVisible || row.Bounds.Height <= 0.5)
            {
                continue;
            }
            if (row.TranslatePoint(new Point(0, row.MarkCenterY), this) is { } p)
            {
                _marks.Add((p.Y, row));
            }
        }
    }

    public override void Render(DrawingContext context)
    {
        CollectMarks();
        if (_marks.Count < 2)
        {
            return;
        }
        var x = AiWorkflowMetrics.MarkSize / 2;
        _linePen ??= new Pen(LineBrush ?? Brushes.Gray, AiWorkflowMetrics.RailWidth);
        _fillPen ??= new Pen(FillBrush ?? Brushes.DodgerBlue, AiWorkflowMetrics.RailWidth);
        var half = AiWorkflowMetrics.MarkSize / 2;
        for (var i = 1; i < _marks.Count; i++)
        {
            var top = _marks[i - 1].Y;
            var bottom = _marks[i].Y;
            if (bottom <= top)
            {
                continue;
            }
            context.DrawLine(_linePen, new Point(x, top), new Point(x, bottom));
            var row = _marks[i].Row;
            if (_travelTo is not null && row.Step.Id == _travelTo)
            {
                var p = Math.Clamp(_travel?.Value ?? 1, 0, 1);
                var start = top + half;
                var end = bottom - half;
                var at = start + (end - start) * p;
                context.DrawLine(_fillPen, new Point(x, top), new Point(x, at));
                if (ArcMotion.GetEffectiveLevel(this) == ArcMotionLevel.Full && p < 1)
                {
                    var r = AiWorkflowMetrics.DotSize / 2;
                    context.DrawEllipse(FillBrush ?? Brushes.DodgerBlue, null, new Point(x, at), r, r);
                }
            }
            else if (IsReached?.Invoke(row) == true)
            {
                context.DrawLine(_fillPen, new Point(x, top), new Point(x, bottom));
            }
        }
    }
}
