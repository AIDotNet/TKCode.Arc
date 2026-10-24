using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using TKCode.Arc.Gestures;
using TKCode.Arc.Motion;

namespace TKCode.Arc;

public partial class WorkflowCanvas
{
    /// <summary>Distance a press travels before it becomes a pan (5px, like the library's other drags).</summary>
    private const double PanThreshold = 5;

    /// <summary>
    /// How fitting, zooming, scrolling and arranging move: Arc's standard ease-out over 360ms (between its standard 240ms
    /// and considered 480ms). Not a spring: a spring spends its last third of a second moving by fractions of a pixel,
    /// and on the canvas each of those frames shows after everything looked still: a zoom draws the text at a slightly
    /// different size every frame, then sharpens it with a jolt as it stops, and cards, which sit on whole pixels, tick by
    /// one pixel at a time. The ease-out's tail is a few frames.
    /// </summary>
    private static readonly MotionSpec Glide = MotionSpec.Tween(TimeSpan.FromMilliseconds(360), ArcEasings.Standard);

    private readonly WheelRouter _wheel = new();
    private PanZoomTransform _view = PanZoomTransform.Identity;
    private PanZoomTransform _viewFrom;
    private PanZoomTransform _viewTo;
    private MotionValue<double>? _viewMotion;
    private bool _manualView;
    private bool _settingView;

    private IPointer? _pressPointer;
    private Point _pressPoint;
    private PanZoomTransform _pressView;
    private bool _panning;

    /// <summary>The current view (tests).</summary>
    internal PanZoomTransform View => _view;

    /// <summary>True while a fit, zoom or scroll is animating (tests).</summary>
    internal bool IsViewAnimating => _viewMotion is { IsAnimating: true };

    /// <summary>True while a drag pans the view (tests).</summary>
    internal bool IsPanning => _panning;

    /// <summary>The size of the area the workflow shows in (the canvas inside its border).</summary>
    internal Size ViewportSize => ViewportHost?.Bounds.Size ?? Bounds.Size;

    /// <summary>
    /// Fits the workflow again: per <see cref="AutoFit"/>, or as large as fits when it is <see cref="WorkflowAutoFit.None"/>.
    /// Animated when <paramref name="animate"/> (full motion). Fitting goes on after panning and zooming stopped it.
    /// </summary>
    public void FitToContent(bool animate = true)
    {
        var fit = AutoFit == WorkflowAutoFit.None ? WorkflowAutoFit.Fit : AutoFit;
        _manualView = AutoFit == WorkflowAutoFit.None;
        SetView(FitView(fit, ViewportSize), animate);
        InvalidateMeasure();
    }

    /// <summary>
    /// Zooms to <paramref name="zoom"/> (kept within <see cref="MinZoom"/> and <see cref="MaxZoom"/>), keeping the canvas
    /// point under <paramref name="anchor"/> (a point on the canvas control; null is the center of the view) where it is.
    /// </summary>
    public void ZoomTo(double zoom, Point? anchor = null, bool animate = true)
    {
        var size = ViewportSize;
        var at = anchor ?? new Point(size.Width / 2, size.Height / 2);
        _manualView = true;
        SetView(Target.ZoomTo(zoom, at, MinZoom, MaxZoom), animate);
    }

    /// <summary>Zooms in one step (×1.25) about the focused step or the center of the view.</summary>
    public void ZoomIn() => ZoomTo(Target.Zoom * ZoomStep, ZoomAnchor());

    /// <summary>Zooms out one step (÷1.25) about the focused step or the center of the view.</summary>
    public void ZoomOut() => ZoomTo(Target.Zoom / ZoomStep, ZoomAnchor());

    /// <summary>
    /// Pans just enough that <paramref name="node"/>'s card shows whole, with a margin of the canvas
    /// <see cref="Avalonia.Controls.Primitives.TemplatedControl.Padding"/>; nothing moves when it already shows.
    /// </summary>
    public void ScrollIntoView(WorkflowNode node, bool animate = true)
    {
        if (!_cards.ContainsKey(node))
        {
            return;
        }
        var target = Target.EnsureVisible(DisplayRect(node), ViewportSize, Padding);
        if (target != Target)
        {
            _manualView = true;
            SetView(target, animate);
        }
    }

    /// <summary>Where the view is going: the end of a running animation, or the view itself.</summary>
    private PanZoomTransform Target => IsViewAnimating ? _viewTo : _view;

    /// <summary>The view that fits the workflow into <paramref name="size"/> less the padding.</summary>
    private PanZoomTransform FitView(WorkflowAutoFit fit, Size size)
    {
        var content = ContentBounds();
        var max = fit == WorkflowAutoFit.ShrinkToFit ? Math.Min(1, MaxZoom) : MaxZoom;
        var viewport = new Size(size.Width, double.IsFinite(size.Height) ? size.Height : double.MaxValue);
        return PanZoomTransform.Fit(content, viewport, Padding, Math.Min(MinZoom, max), max);
    }

    /// <summary>Moves the view to <paramref name="view"/>, eased when <paramref name="animate"/> and motion is full.</summary>
    /// <remarks>
    /// Code-driven motion: the zoom and the pan move together through <see cref="PanZoomTransform.Interpolate"/> so the
    /// anchor of a zoom stays put; two independent transitions would let it drift.
    /// </remarks>
    private void SetView(PanZoomTransform view, bool animate)
    {
        if (!animate || !IsLoaded || !ArcMotion.IsFull(this))
        {
            StopViewAnimation();
            ApplyView(view);
            return;
        }
        _viewFrom = _view;
        _viewTo = view;
        if (_viewMotion is null)
        {
            _viewMotion = new MotionValue<double>(this, 0, MotionVectors.Double);
            _viewMotion.Changed += t => ApplyView(PanZoomTransform.Interpolate(_viewFrom, _viewTo, t));
        }
        _viewMotion.Jump(0);
        _ = _viewMotion.AnimateTo(1, Glide);
    }

    /// <summary>Stops a view animation where it is (input takes over).</summary>
    private void StopViewAnimation()
    {
        if (_viewMotion is { IsAnimating: true } motion)
        {
            motion.Jump(motion.Value);
        }
    }

    /// <summary>
    /// Raised when what a map of the canvas shows may have changed: the view, the cards' places or sizes, the run, the
    /// selection (<see cref="WorkflowMinimap"/> redraws; it reads the canvas when it renders).
    /// </summary>
    internal event Action? SceneChanged;

    /// <summary>Puts <paramref name="world"/> in the middle of the view at once, as input does (it stops fitting).</summary>
    internal void CenterOn(Point world)
    {
        var size = ViewportSize;
        var screen = _view.WorldToScreen(world);
        SetViewFromInput(_view.PanBy(new Point(size.Width / 2, size.Height / 2) - screen));
    }

    private void ApplyView(PanZoomTransform view)
    {
        SceneChanged?.Invoke();
        _view = view;
        RefreshCommands();
        UpdateRealized();
        if (_world is not null)
        {
            _world.RenderTransform = new MatrixTransform(view.Matrix);
        }
        if (_grid is not null)
        {
            _grid.View = view;
        }
        _settingView = true;
        try
        {
            SetCurrentValue(ZoomProperty, view.Zoom);
            SetCurrentValue(OffsetProperty, view.Origin);
        }
        finally
        {
            _settingView = false;
        }
    }

    /// <summary>Takes the view from input: stops fitting and any animation.</summary>
    private void SetViewFromInput(PanZoomTransform view)
    {
        _manualView = true;
        StopViewAnimation();
        ApplyView(view);
    }

    /// <summary>The focused step's center, or null (the center of the view).</summary>
    private Point? ZoomAnchor()
    {
        foreach (var (node, card) in _cards)
        {
            if (card.IsFocused)
            {
                return Target.WorldToScreen(DisplayRect(node).Center);
            }
        }
        return null;
    }

    // Input --------------------------------------------------------------------------------------------------------------

    private void InitializeInput()
    {
        AddHandler(InputElement.PointerTouchPadGestureMagnifyEvent, OnMagnify);
        GestureRecognizers.Add(new PinchGestureRecognizer());
        AddHandler(InputElement.PinchEvent, OnPinch);
        AddHandler(InputElement.PinchEndedEvent, OnPinchEnded);
        AddHandler(InputElement.DoubleTappedEvent, OnDoubleTapped);
    }

    private Point ViewPoint(PointerEventArgs e) => ViewportHost is { } host ? e.GetPosition(host) : e.GetPosition(this);

    protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
    {
        base.OnPointerWheelChanged(e);
        if (e.Handled || !IsPanZoomEnabled)
        {
            return;
        }
        _wheel.Mode = WheelMode;
        var action = _wheel.Route(e.Delta, e.KeyModifiers, TimeSpan.FromMilliseconds(e.Timestamp));
        switch (action.Kind)
        {
            case WheelActionKind.Pan:
                SetViewFromInput(_view.PanBy(action.Pan));
                break;
            case WheelActionKind.Zoom:
                SetViewFromInput(_view.ZoomBy(action.ZoomFactor, ViewPoint(e), MinZoom, MaxZoom));
                break;
            default:
                return;
        }
        e.Handled = true;
    }

    private void OnMagnify(object? sender, PointerDeltaEventArgs e)
    {
        if (!IsPanZoomEnabled)
        {
            return;
        }
        _wheel.Mode = WheelMode;
        var action = _wheel.RouteMagnify(e.Delta.X);
        if (action.Kind == WheelActionKind.Zoom)
        {
            SetViewFromInput(_view.ZoomBy(action.ZoomFactor, ViewPoint(e), MinZoom, MaxZoom));
            e.Handled = true;
        }
    }

    private void OnPinch(object? sender, PinchEventArgs e)
    {
        if (!IsPanZoomEnabled)
        {
            return;
        }
        _wheel.Mode = WheelMode;
        var action = _wheel.RoutePinch(e.Scale);
        if (action.Kind == WheelActionKind.Zoom)
        {
            EndPan();
            var origin = ViewportHost is { } host && this.TranslatePoint(e.ScaleOrigin, host) is { } p ? p : e.ScaleOrigin;
            SetViewFromInput(_view.ZoomBy(action.ZoomFactor, origin, MinZoom, MaxZoom));
            e.Handled = true;
        }
    }

    private void OnPinchEnded(object? sender, PinchEndedEventArgs e) => _wheel.EndPinch();

    /// <summary>A double click on a card opens its step; on the empty canvas it fits the workflow.</summary>
    private void OnDoubleTapped(object? sender, TappedEventArgs e)
    {
        // Two presses make a double click only when neither turned into a drag (a pan, a move, a connection).
        if (_dragsSincePress > 0 || FromCardContent(e.Source))
        {
            return;
        }
        if (CardOf(e.Source) is { Node: { } node })
        {
            RaiseEvent(new WorkflowNodeEventArgs(NodeActivatedEvent, node));
            e.Handled = true;
        }
        else if (IsPanZoomEnabled)
        {
            FitToContent();
            e.Handled = true;
        }
    }
}
