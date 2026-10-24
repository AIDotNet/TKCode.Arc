using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Immutable;

namespace TKCode.Arc;

/// <summary>
/// A small map of a <see cref="WorkflowCanvas"/>: every step as a block (running and selected steps in the accent,
/// failed ones in red) and the part the canvas shows as a frame. Press to move the view there, drag to pan. Set
/// <see cref="Target"/> to the canvas and place the map over a corner, like <see cref="WorkflowZoomControls"/>. It is a
/// pointer shortcut: the canvas's own keys do the same, so the map takes no focus and screen readers skip it.
/// </summary>
public class WorkflowMinimap : TemplatedControl
{
    /// <summary>The canvas the map shows and moves.</summary>
    public static readonly StyledProperty<WorkflowCanvas?> TargetProperty =
        AvaloniaProperty.Register<WorkflowMinimap, WorkflowCanvas?>(nameof(Target));

    // Inputs from the theme.
    internal static readonly StyledProperty<IBrush?> NodeBrushProperty =
        AvaloniaProperty.Register<WorkflowMinimap, IBrush?>(nameof(NodeBrush));

    internal static readonly StyledProperty<IBrush?> AccentBrushProperty =
        AvaloniaProperty.Register<WorkflowMinimap, IBrush?>(nameof(AccentBrush));

    internal static readonly StyledProperty<IBrush?> DangerBrushProperty =
        AvaloniaProperty.Register<WorkflowMinimap, IBrush?>(nameof(DangerBrush));

    internal static readonly StyledProperty<IBrush?> ViewportFillProperty =
        AvaloniaProperty.Register<WorkflowMinimap, IBrush?>(nameof(ViewportFill));

    internal static readonly StyledProperty<BoxShadows> BoxShadowProperty =
        AvaloniaProperty.Register<WorkflowMinimap, BoxShadows>(nameof(BoxShadow));

    internal static readonly StyledProperty<double> NodeRadiusProperty =
        AvaloniaProperty.Register<WorkflowMinimap, double>(nameof(NodeRadius), 2);

    internal static readonly StyledProperty<double> ViewportRadiusProperty =
        AvaloniaProperty.Register<WorkflowMinimap, double>(nameof(ViewportRadius), 3);

    private WorkflowCanvas? _observed;
    private IPen? _borderPen;
    private IPen? _viewportPen;

    // While a drag lasts the map keeps its scale and place, so the frame follows the pointer instead of the map moving
    // under it.
    private (Rect World, double Scale, Point Offset)? _frozen;
    private Point _lastPoint;

    static WorkflowMinimap()
    {
        AffectsRender<WorkflowMinimap>(NodeBrushProperty, AccentBrushProperty, DangerBrushProperty, ViewportFillProperty,
            BoxShadowProperty, NodeRadiusProperty, ViewportRadiusProperty, BackgroundProperty, BorderBrushProperty,
            BorderThicknessProperty, CornerRadiusProperty, PaddingProperty);
        FocusableProperty.OverrideDefaultValue<WorkflowMinimap>(false);
        AutomationProperties.AccessibilityViewProperty.OverrideDefaultValue<WorkflowMinimap>(AccessibilityView.Raw);
    }

    /// <inheritdoc cref="TargetProperty"/>
    public WorkflowCanvas? Target
    {
        get => GetValue(TargetProperty);
        set => SetValue(TargetProperty, value);
    }

    internal IBrush? NodeBrush
    {
        get => GetValue(NodeBrushProperty);
        set => SetValue(NodeBrushProperty, value);
    }

    internal IBrush? AccentBrush
    {
        get => GetValue(AccentBrushProperty);
        set => SetValue(AccentBrushProperty, value);
    }

    internal IBrush? DangerBrush
    {
        get => GetValue(DangerBrushProperty);
        set => SetValue(DangerBrushProperty, value);
    }

    internal IBrush? ViewportFill
    {
        get => GetValue(ViewportFillProperty);
        set => SetValue(ViewportFillProperty, value);
    }

    internal BoxShadows BoxShadow
    {
        get => GetValue(BoxShadowProperty);
        set => SetValue(BoxShadowProperty, value);
    }

    internal double NodeRadius
    {
        get => GetValue(NodeRadiusProperty);
        set => SetValue(NodeRadiusProperty, value);
    }

    internal double ViewportRadius
    {
        get => GetValue(ViewportRadiusProperty);
        set => SetValue(ViewportRadiusProperty, value);
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        Observe(Target);
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        Observe(null);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == TargetProperty)
        {
            if (VisualRoot is not null)
            {
                Observe(Target);
            }
            InvalidateVisual();
        }
        else if (change.Property == BorderBrushProperty || change.Property == BorderThicknessProperty)
        {
            _borderPen = null;
        }
        else if (change.Property == AccentBrushProperty)
        {
            _viewportPen = null;
        }
    }

    private void Observe(WorkflowCanvas? canvas)
    {
        if (ReferenceEquals(_observed, canvas))
        {
            return;
        }
        if (_observed is not null)
        {
            _observed.SceneChanged -= InvalidateVisual;
        }
        _observed = canvas;
        if (canvas is not null)
        {
            canvas.SceneChanged += InvalidateVisual;
        }
    }

    /// <summary>
    /// What the map shows and how: the world rectangle (every card and the view, so the frame is always on the map),
    /// the scale and the screen point of the world's top-left corner.
    /// </summary>
    internal (Rect World, double Scale, Point Offset)? Mapping()
    {
        if (_frozen is { } frozen)
        {
            return frozen;
        }
        if (Target is not { } canvas || canvas.ViewportSize is { Width: <= 0 } or { Height: <= 0 })
        {
            return null;
        }
        var world = canvas.View.VisibleWorld(canvas.ViewportSize);
        foreach (var node in canvas.Graph?.Nodes ?? [])
        {
            world = world.Union(canvas.DisplayRect(node));
        }
        var inner = new Rect(Bounds.Size).Deflate(Padding);
        if (inner.Width <= 0 || inner.Height <= 0 || world.Width <= 0 || world.Height <= 0)
        {
            return null;
        }
        var scale = Math.Min(inner.Width / world.Width, inner.Height / world.Height);
        var offset = new Point(inner.X + (inner.Width - world.Width * scale) / 2, inner.Y + (inner.Height - world.Height * scale) / 2);
        return (world, scale, offset);
    }

    /// <summary>The map point of a world point.</summary>
    private static Point ToMap((Rect World, double Scale, Point Offset) m, Point world)
        => new(m.Offset.X + (world.X - m.World.X) * m.Scale, m.Offset.Y + (world.Y - m.World.Y) * m.Scale);

    private static Rect ToMap((Rect World, double Scale, Point Offset) m, Rect world)
        => new(ToMap(m, world.TopLeft), world.Size * m.Scale);

    /// <summary>The world point at a map point.</summary>
    internal static Point ToWorld((Rect World, double Scale, Point Offset) m, Point map)
        => new(m.World.X + (map.X - m.Offset.X) / m.Scale, m.World.Y + (map.Y - m.Offset.Y) / m.Scale);

    public override void Render(DrawingContext context)
    {
        var box = new Rect(Bounds.Size);
        _borderPen ??= BorderBrush is { } border && BorderThickness.Left > 0
            ? new ImmutablePen(border.ToImmutable(), BorderThickness.Left)
            : null;
        var radius = CornerRadius.TopLeft;
        context.DrawRectangle(Background, _borderPen, new RoundedRect(box.Deflate(BorderThickness.Left / 2), radius), BoxShadow);
        if (Target is not { } canvas || Mapping() is not { } m)
        {
            return;
        }
        using var clip = context.PushClip(new RoundedRect(box.Deflate(BorderThickness.Left), Math.Max(0, radius - BorderThickness.Left)));
        var frame = ToMap(m, canvas.View.VisibleWorld(canvas.ViewportSize));
        if (ViewportFill is { } fill)
        {
            context.DrawRectangle(fill, null, frame, ViewportRadius, ViewportRadius);
        }
        var selected = canvas.SelectedNodes;
        foreach (var node in canvas.Graph?.Nodes ?? [])
        {
            var state = canvas.CardFor(node)?.DisplayState ?? node.State;
            var brush = state == WorkflowNodeState.Failed ? DangerBrush
                : state == WorkflowNodeState.Running || selected.Contains(node) ? AccentBrush
                : NodeBrush;
            var rect = ToMap(m, canvas.DisplayRect(node));
            // Very small maps still show every step.
            rect = new Rect(rect.X, rect.Y, Math.Max(rect.Width, 2), Math.Max(rect.Height, 2));
            using (context.PushOpacity(state == WorkflowNodeState.Skipped ? 0.5 : 1))
            {
                context.DrawRectangle(brush, null, rect, NodeRadius, NodeRadius);
            }
        }
        _viewportPen ??= AccentBrush is { } accent ? new ImmutablePen(accent.ToImmutable(), 1) : null;
        context.DrawRectangle(null, _viewportPen, frame.Deflate(0.5), ViewportRadius, ViewportRadius);
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed || Target is not { } canvas || Mapping() is not { } m)
        {
            return;
        }
        var point = e.GetPosition(this);
        // A press outside the frame moves the view there; inside, the frame is picked up where it was pressed.
        if (!ToMap(m, canvas.View.VisibleWorld(canvas.ViewportSize)).Contains(point))
        {
            canvas.CenterOn(ToWorld(m, point));
        }
        _frozen = m;
        _lastPoint = point;
        e.Pointer.Capture(this);
        e.Handled = true;
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        if (_frozen is not { } m || Target is not { } canvas)
        {
            return;
        }
        var point = e.GetPosition(this);
        var world = canvas.View.VisibleWorld(canvas.ViewportSize).Center + (point - _lastPoint) / m.Scale;
        _lastPoint = point;
        canvas.CenterOn(world);
        e.Handled = true;
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        if (_frozen is not null)
        {
            Release(e.Pointer);
            e.Handled = true;
        }
    }

    protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e)
    {
        base.OnPointerCaptureLost(e);
        _frozen = null;
        InvalidateVisual();
    }

    private void Release(IPointer pointer)
    {
        _frozen = null;
        pointer.Capture(null);
        InvalidateVisual();
    }
}
