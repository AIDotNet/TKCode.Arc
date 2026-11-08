using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Media.Immutable;
using TKCode.Arc.DesignTokens;
using TKCode.Arc.Gestures;
using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;

namespace TKCode.Arc;

/// <summary>
/// The scrolled page surface of a <see cref="PageViewer"/>: one self-drawn control with a virtual extent, so a document
/// of ten thousand pages has no visual tree to speak of (performance rule 1). <see cref="Render"/> draws only the visible
/// pages (shadow, white paper, bitmap, detail, layers) and takes pointer input for panning, zooming and the layers.
/// </summary>
internal sealed class PageViewerPresenter : Control, ILogicalScrollable
{
    /// <summary>The shadow under each page.</summary>
    public static readonly StyledProperty<BoxShadows> PageShadowProperty =
        AvaloniaProperty.Register<PageViewerPresenter, BoxShadows>(nameof(PageShadow));

    /// <summary>The ring of a page without a bitmap.</summary>
    public static readonly StyledProperty<IBrush?> PlaceholderInkProperty =
        AvaloniaProperty.Register<PageViewerPresenter, IBrush?>(nameof(PlaceholderInk));

    /// <summary>Gap between pages.</summary>
    public static readonly StyledProperty<double> PageSpacingProperty =
        AvaloniaProperty.Register<PageViewerPresenter, double>(nameof(PageSpacing));

    /// <summary>Canvas around the pages.</summary>
    public static readonly StyledProperty<double> CanvasPaddingProperty =
        AvaloniaProperty.Register<PageViewerPresenter, double>(nameof(CanvasPadding));

    /// <summary>Canvas around the pages in a narrow viewer.</summary>
    public static readonly StyledProperty<double> CanvasPaddingNarrowProperty =
        AvaloniaProperty.Register<PageViewerPresenter, double>(nameof(CanvasPaddingNarrow));

    /// <summary>Below this viewport width the narrow canvas padding applies.</summary>
    private const double NarrowWidth = 640;

    private const double PlaceholderRadius = 12;

    /// <summary>How near the edge of the viewport a layer's drag starts scrolling.</summary>
    private const double DragScrollBand = 16;

    /// <summary>The largest scroll of one step of that, in DIPs.</summary>
    private const double DragScrollMaxStep = 40;

    private static readonly TimeSpan DragScrollInterval = TimeSpan.FromMilliseconds(16);

    private static readonly IBrush s_paper = new ImmutableSolidColorBrush(Colors.White);
    private static readonly RenderOptions s_renderOptions = new() { BitmapInterpolationMode = BitmapInterpolationMode.HighQuality };
    private static Cursor? s_panCursor;
    private static Cursor? s_panningCursor;
    private static Geometry? s_placeholderArc;

    private readonly WheelRouter _wheel = new() { Mode = WheelMode.ModifierZoom };
    private PageViewer? _owner;
    private LazyFocusRing? _focusRing;
    private EventHandler? _scrollInvalidated;
    private MotionValue<Vector>? _scrollMotion;
    private DispatcherTimer? _dragScroll;
    private KeyModifiers _dragModifiers;
    private PointerType _pointerType = PointerType.Mouse;
    private IPen? _placeholderPen;
    private Vector _offset;
    private Size _extent;
    private Size _viewport;
    private Cursor? _cursor;
    private bool _isPanning;
    private Point _panStart;
    private Vector _panStartOffset;
    private bool _layerHasDrag;
    private bool _spaceHeld;
    private Point _lastPointer;
    private bool _hasPointer;
    private TopLevel? _topLevel;
    private bool _sizesToContent;

    static PageViewerPresenter()
    {
        FocusableProperty.OverrideDefaultValue<PageViewerPresenter>(true);
        ClipToBoundsProperty.OverrideDefaultValue<PageViewerPresenter>(true);
        AffectsRender<PageViewerPresenter>(PageShadowProperty, PlaceholderInkProperty);
    }

    public PageViewerPresenter()
    {
        AddHandler(PointerTouchPadGestureMagnifyEvent, OnMagnify);
        GestureRecognizers.Add(new PinchGestureRecognizer());
        AddHandler(PinchEvent, OnPinch);
        AddHandler(PinchEndedEvent, OnPinchEnded);
    }

    public BoxShadows PageShadow { get => GetValue(PageShadowProperty); set => SetValue(PageShadowProperty, value); }

    public IBrush? PlaceholderInk { get => GetValue(PlaceholderInkProperty); set => SetValue(PlaceholderInkProperty, value); }

    public double PageSpacing { get => GetValue(PageSpacingProperty); set => SetValue(PageSpacingProperty, value); }

    public double CanvasPadding { get => GetValue(CanvasPaddingProperty); set => SetValue(CanvasPaddingProperty, value); }

    public double CanvasPaddingNarrow { get => GetValue(CanvasPaddingNarrowProperty); set => SetValue(CanvasPaddingNarrowProperty, value); }

    /// <summary>The scroll position.</summary>
    public Vector Offset => _offset;

    /// <summary>The visible size.</summary>
    public Size ViewportSize => _viewport;

    /// <summary>True while the pointer pans the view.</summary>
    public bool IsPanning => _isPanning;

    /// <summary>The device pixels per DIP this presenter draws at.</summary>
    public double RenderScale => LayoutHelper.GetLayoutScale(this);

    /// <summary>Connects the presenter to its viewer (null when the template is replaced).</summary>
    public void SetOwner(PageViewer? owner)
    {
        _owner = owner;
        if (owner is not null)
        {
            ApplyLayoutMetrics(owner.Layout);
            if (_viewport.Width > 0 || _viewport.Height > 0)
            {
                OnLayoutChanged();
            }
        }
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == PlaceholderInkProperty)
        {
            _placeholderPen = null;
        }
        else if (change.Property == PageSpacingProperty || change.Property == CanvasPaddingProperty || change.Property == CanvasPaddingNarrowProperty)
        {
            if (_owner is { } owner && ApplyLayoutMetrics(owner.Layout))
            {
                owner.OnPresenterResized(null);
            }
        }
    }

    private bool ApplyLayoutMetrics(PageLayoutEngine layout)
    {
        var padding = _viewport.Width > 0 && _viewport.Width < NarrowWidth ? CanvasPaddingNarrow : CanvasPadding;
        if (layout.Padding == padding && layout.Spacing == PageSpacing)
        {
            return false;
        }
        layout.Padding = padding;
        layout.Spacing = PageSpacing;
        return true;
    }

    #region Layout and scrolling

    protected override Size MeasureOverride(Size availableSize)
    {
        foreach (var child in VisualChildren)
        {
            if (child is Layoutable layoutable)
            {
                layoutable.Measure(availableSize);
            }
        }
        _sizesToContent = double.IsInfinity(availableSize.Width) || double.IsInfinity(availableSize.Height);
        var content = _owner?.Layout.ContentSize ?? default;
        return new Size(double.IsInfinity(availableSize.Width) ? content.Width : availableSize.Width,
            double.IsInfinity(availableSize.Height) ? content.Height : availableSize.Height);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        foreach (var child in VisualChildren)
        {
            if (child is Layoutable layoutable)
            {
                layoutable.Arrange(new Rect(finalSize));
            }
        }
        if (finalSize != _viewport)
        {
            var old = _viewport;
            PageAnchor? anchor = null;
            if (_owner is { } owner && owner.Layout.PageCount > 0 && old.Width > 0 && old.Height > 0)
            {
                anchor = CaptureAnchor(new Point(old.Width / 2, old.Height / 2)) with
                {
                    ViewportPoint = new Point(finalSize.Width / 2, finalSize.Height / 2)
                };
            }
            _viewport = finalSize;
            if (_owner is { } target)
            {
                ApplyLayoutMetrics(target.Layout);
                target.OnPresenterResized(anchor);
            }
        }
        return finalSize;
    }

    bool ILogicalScrollable.CanHorizontallyScroll { get; set; }

    bool ILogicalScrollable.CanVerticallyScroll { get; set; }

    bool ILogicalScrollable.IsLogicalScrollEnabled => true;

    Size ILogicalScrollable.ScrollSize => new(WheelRouter.DefaultPixelsPerWheelUnit, WheelRouter.DefaultPixelsPerWheelUnit);

    Size ILogicalScrollable.PageScrollSize => _viewport;

    bool IScrollable.CanHorizontallyScroll => ((ILogicalScrollable)this).CanHorizontallyScroll;

    bool IScrollable.CanVerticallyScroll => ((ILogicalScrollable)this).CanVerticallyScroll;

    Size IScrollable.Extent => _extent;

    Size IScrollable.Viewport => _viewport;

    Vector IScrollable.Offset
    {
        get => _offset;
        set
        {
            // The scroll bars, the wheel and touch land here; an animated jump gives way to them.
            if (value != _offset)
            {
                StopScrollMotion();
                SetOffsetCore(value);
            }
        }
    }

    event EventHandler? ILogicalScrollable.ScrollInvalidated
    {
        add => _scrollInvalidated += value;
        remove => _scrollInvalidated -= value;
    }

    bool ILogicalScrollable.BringIntoView(Control target, Rect targetRect) => false;

    Control? ILogicalScrollable.GetControlInDirection(NavigationDirection direction, Control? from) => null;

    void ILogicalScrollable.RaiseScrollInvalidated(EventArgs e) => _scrollInvalidated?.Invoke(this, e);

    /// <summary>A point of the viewport as a point of the page under it, valid across layout changes.</summary>
    public PageAnchor CaptureAnchor(Point viewportPoint)
    {
        var layout = _owner!.Layout;
        var (page, point) = layout.Locate(viewportPoint + _offset, _viewport);
        return new PageAnchor(viewportPoint, page, point);
    }

    /// <summary>The layout changed: new extent, optionally keeping a page point in place, then redraw and replan.</summary>
    public void OnLayoutChanged(PageAnchor? anchor = null)
    {
        if (_owner is not { } owner)
        {
            return;
        }
        var layout = owner.Layout;
        var extent = layout.ContentSize;
        _extent = extent;
        var offset = _offset;
        if (anchor is { } a)
        {
            offset = (Vector)layout.ToContent(a.PageIndex, a.PointInPage, _viewport) - (Vector)a.ViewportPoint;
        }
        _offset = ClampOffset(offset);
        _scrollInvalidated?.Invoke(this, EventArgs.Empty);
        InvalidateVisual();
        if (_sizesToContent)
        {
            // With no limit from the parent, the presenter's own size follows the pages.
            InvalidateMeasure();
        }
        owner.OnPresenterViewChanged();
    }

    /// <summary>Clamps a position to the extent.</summary>
    public Vector ClampOffset(Vector value) => new(
        Math.Clamp(value.X, 0, Math.Max(0, _extent.Width - _viewport.Width)),
        Math.Clamp(value.Y, 0, Math.Max(0, _extent.Height - _viewport.Height)));

    /// <summary>Moves the view, on a spring when asked to (code-driven: an <see cref="ILogicalScrollable"/> offset is no styled property, so ControlTheme cannot express it).</summary>
    public void ScrollTo(Vector target, bool animate)
    {
        target = ClampOffset(target);
        if (!animate || VisualRoot is null)
        {
            StopScrollMotion();
            SetOffsetCore(target);
            return;
        }
        if (_scrollMotion is null)
        {
            _scrollMotion = new MotionValue<Vector>(this, _offset, MotionVectors.Vector);
            _scrollMotion.Changed += SetOffsetCore;
        }
        else if (!_scrollMotion.IsAnimating)
        {
            _scrollMotion.Jump(_offset);
        }
        _ = _scrollMotion.AnimateTo(target, MotionSpec.Spring(ArcSpring.Smooth));
    }

    /// <summary>Scrolls by a distance at once.</summary>
    public void ScrollBy(Vector delta) => ScrollTo(_offset + delta, animate: false);

    /// <summary>Whether a <see cref="ScrollTo"/> spring is still running.</summary>
    public bool IsScrollAnimating => _scrollMotion?.IsAnimating == true;

    /// <summary>Ends a running <see cref="ScrollTo"/> spring where it is.</summary>
    public void StopScrollMotion()
    {
        if (_scrollMotion is { IsAnimating: true })
        {
            _scrollMotion.Stop();
        }
    }

    private void SetOffsetCore(Vector value)
    {
        var clamped = ClampOffset(value);
        if (clamped == _offset)
        {
            return;
        }
        _offset = clamped;
        _scrollInvalidated?.Invoke(this, EventArgs.Empty);
        InvalidateVisual();
        _owner?.OnPresenterViewChanged();
    }

    #endregion

    #region Rendering

    public override void Render(DrawingContext context)
    {
        var bounds = new Rect(Bounds.Size);
        // Pointer input reaches the whole viewport, canvas included.
        context.FillRectangle(Brushes.Transparent, bounds);
        if (_owner is not { } owner)
        {
            return;
        }
        var layout = owner.Layout;
        var (first, last) = layout.GetVisibleRange(_offset.Y, _offset.Y + _viewport.Height, _viewport.Height);
        if (last < first)
        {
            return;
        }
        var scale = RenderScale;
        var layers = owner.LayerList;
        using (context.PushRenderOptions(s_renderOptions))
        {
            for (var page = first; page <= last; page++)
            {
                var rect = Snap(layout.GetPageRect(page, _viewport).Translate(-_offset), scale);
                context.DrawRectangle(s_paper, null, rect, 0, 0, PageShadow);
                owner.Scheduler.GetBitmaps(page, out var baseEntry, out var detail);
                if (baseEntry is null && detail is null)
                {
                    DrawPlaceholder(context, rect);
                }
                else
                {
                    if (baseEntry is not null)
                    {
                        DrawWhole(context, baseEntry.Bitmap, rect);
                    }
                    if (detail is { Region: { } region })
                    {
                        DrawDetail(context, detail, region, rect);
                    }
                }
                if (layers.Count > 0)
                {
                    using (context.PushClip(rect))
                    {
                        var layerContext = new PageLayerContext(page, rect, layout.Zoom);
                        for (var i = 0; i < layers.Count; i++)
                        {
                            layers[i].Render(context, in layerContext);
                        }
                    }
                }
            }
        }
    }

    /// <summary>On whole device pixels, so a bitmap of the right size lands 1:1 and text stays sharp.</summary>
    private static Rect Snap(Rect rect, double scale)
    {
        var left = Math.Round(rect.X * scale);
        var top = Math.Round(rect.Y * scale);
        var right = Math.Round(rect.Right * scale);
        var bottom = Math.Round(rect.Bottom * scale);
        return new Rect(left / scale, top / scale, (right - left) / scale, (bottom - top) / scale);
    }

    private static void DrawWhole(DrawingContext context, Bitmap bitmap, Rect destination)
    {
        // The source rectangle is in pixels, not DIPs (handbook section 13, high DPI).
        var pixels = bitmap.PixelSize;
        context.DrawImage(bitmap, new Rect(0, 0, pixels.Width, pixels.Height), destination);
    }

    private static void DrawDetail(DrawingContext context, PageBitmapEntry detail, PixelRect region, Rect page)
    {
        var target = detail.TargetSize;
        if (target.Width <= 0 || target.Height <= 0)
        {
            return;
        }
        var kx = page.Width / target.Width;
        var ky = page.Height / target.Height;
        var destination = new Rect(page.X + region.X * kx, page.Y + region.Y * ky, region.Width * kx, region.Height * ky);
        DrawWhole(context, detail.Bitmap, destination);
    }

    private void DrawPlaceholder(DrawingContext context, Rect page)
    {
        var ink = PlaceholderInk;
        if (ink is null)
        {
            return;
        }
        // A still ring in the look of ArcSpinner, not an animation: a long document would run a loop per page otherwise.
        _placeholderPen ??= new Pen(ink, 2, lineCap: PenLineCap.Round);
        s_placeholderArc ??= CreatePlaceholderArc();
        var center = page.Center;
        using (context.PushTransform(Matrix.CreateTranslation(center.X, center.Y)))
        {
            context.DrawGeometry(null, _placeholderPen, s_placeholderArc);
        }
    }

    private static Geometry CreatePlaceholderArc()
    {
        var geometry = new StreamGeometry();
        using (var sink = geometry.Open())
        {
            sink.BeginFigure(new Point(0, -PlaceholderRadius), false);
            sink.ArcTo(new Point(-PlaceholderRadius, 0), new Size(PlaceholderRadius, PlaceholderRadius), 0, true, SweepDirection.Clockwise);
        }
        return geometry;
    }

    #endregion

    #region Focus and keys

    /// <summary>Hooks the keyboard focus ring up; the viewport clips, so the ring sits inside it.</summary>
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        if (_focusRing is null)
        {
            _focusRing = new LazyFocusRing(this, PageViewerTokens.Key(PageViewerTokenKind.FocusRingMargin), PageViewerTokens.Key(PageViewerTokenKind.FocusRingRadius));
            _focusRing.Attach(AddRing, RemoveRing);
        }
        // A new monitor density changes the pixels a page needs, while layout sees the same DIPs.
        _topLevel = TopLevel.GetTopLevel(this);
        if (_topLevel is { } topLevel)
        {
            topLevel.ScalingChanged += OnScalingChanged;
        }
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        if (_topLevel is { } topLevel)
        {
            topLevel.ScalingChanged -= OnScalingChanged;
            _topLevel = null;
        }
        StopScrollMotion();
        EndPan();
        CancelLayerDrag();
        _spaceHeld = false;
        _hasPointer = false;
    }

    private void OnScalingChanged(object? sender, EventArgs e) => OnLayoutChanged();

    private void AddRing(Control ring)
    {
        LogicalChildren.Add(ring);
        VisualChildren.Add(ring);
    }

    private void RemoveRing(Control ring)
    {
        VisualChildren.Remove(ring);
        LogicalChildren.Remove(ring);
    }

    protected override void OnGotFocus(FocusChangedEventArgs e)
    {
        base.OnGotFocus(e);
        _focusRing?.Update();
    }

    protected override void OnLostFocus(FocusChangedEventArgs e)
    {
        base.OnLostFocus(e);
        _focusRing?.Update();
        _spaceHeld = false;
        UpdateCursor();
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Handled || _owner is not { } owner)
        {
            return;
        }
        if (e.Key == Key.Space && e.KeyModifiers == KeyModifiers.None)
        {
            _spaceHeld = true;
            UpdateCursor();
            e.Handled = true;
            return;
        }
        if (owner.HandleViewportKey(e))
        {
            e.Handled = true;
        }
    }

    protected override void OnKeyUp(KeyEventArgs e)
    {
        base.OnKeyUp(e);
        if (e.Key == Key.Space)
        {
            _spaceHeld = false;
            UpdateCursor();
            e.Handled = true;
        }
    }

    #endregion

    #region Pointer

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (_owner is not { } owner || e.Handled)
        {
            return;
        }
        var point = e.GetCurrentPoint(this);
        _lastPointer = point.Position;
        _hasPointer = true;
        Focus(NavigationMethod.Pointer);
        var left = point.Properties.IsLeftButtonPressed;
        var middle = point.Properties.IsMiddleButtonPressed;
        // A finger pans through the scroll viewer's gesture recognizer; these are the mouse and pen drags.
        var panDrag = left && e.Pointer.Type != PointerType.Touch && (owner.InteractionMode == PageInteractionMode.Pan || _spaceHeld);
        if (middle || panDrag)
        {
            StopScrollMotion();
            _isPanning = true;
            _panStart = point.Position;
            _panStartOffset = _offset;
            e.Pointer.Capture(this);
            owner.SetPanning(true);
            UpdateCursor();
            e.Handled = true;
            return;
        }
        _pointerType = e.Pointer.Type;
        if (left && DispatchLayers(LayerEvent.Pressed, point.Position, e.KeyModifiers, e.ClickCount))
        {
            _layerHasDrag = true;
            e.Pointer.Capture(this);
            e.Handled = true;
        }
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        if (_owner is null)
        {
            return;
        }
        var position = e.GetPosition(this);
        _lastPointer = position;
        _hasPointer = true;
        if (_isPanning)
        {
            SetOffsetCore(_panStartOffset - (position - _panStart));
            e.Handled = true;
            return;
        }
        _pointerType = e.Pointer.Type;
        if (DispatchLayers(LayerEvent.Moved, position, e.KeyModifiers, 0))
        {
            e.Handled = true;
        }
        if (_layerHasDrag)
        {
            _dragModifiers = e.KeyModifiers;
            UpdateDragScroll();
        }
        UpdateCursor();
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        if (_isPanning)
        {
            EndPan();
            e.Pointer.Capture(null);
            e.Handled = true;
            return;
        }
        if (_layerHasDrag || e.InitialPressMouseButton == MouseButton.Left)
        {
            var handled = DispatchLayers(LayerEvent.Released, e.GetPosition(this), e.KeyModifiers, 0);
            if (_layerHasDrag)
            {
                _layerHasDrag = false;
                _dragScroll?.Stop();
                e.Pointer.Capture(null);
                handled = true;
            }
            e.Handled |= handled;
        }
    }

    protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e)
    {
        base.OnPointerCaptureLost(e);
        EndPan();
        CancelLayerDrag();
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        _hasPointer = false;
    }

    protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
    {
        base.OnPointerWheelChanged(e);
        if (e.Handled || _owner is not { } owner)
        {
            return;
        }
        var action = _wheel.Route(e.Delta, e.KeyModifiers, TimeSpan.FromMilliseconds(e.Timestamp));
        if (action.Kind == WheelActionKind.Zoom)
        {
            owner.ZoomBy(action.ZoomFactor, e.GetPosition(this));
            e.Handled = true;
        }
    }

    private void OnMagnify(object? sender, PointerDeltaEventArgs e)
    {
        if (_owner is { } owner && _wheel.RouteMagnify(e.Delta.X) is { Kind: WheelActionKind.Zoom } action)
        {
            owner.ZoomBy(action.ZoomFactor, e.GetPosition(this));
            e.Handled = true;
        }
    }

    private void OnPinch(object? sender, PinchEventArgs e)
    {
        if (_owner is { } owner && _wheel.RoutePinch(e.Scale) is { Kind: WheelActionKind.Zoom } action)
        {
            EndPan();
            owner.ZoomBy(action.ZoomFactor, e.ScaleOrigin);
            e.Handled = true;
        }
    }

    private void OnPinchEnded(object? sender, PinchEndedEventArgs e) => _wheel.EndPinch();

    private void EndPan()
    {
        if (_isPanning)
        {
            _isPanning = false;
            _owner?.SetPanning(false);
            UpdateCursor();
        }
    }

    /// <summary>A layer's drag ended without a release: nobody may go on extending what it started.</summary>
    private void CancelLayerDrag()
    {
        _dragScroll?.Stop();
        if (!_layerHasDrag)
        {
            return;
        }
        _layerHasDrag = false;
        if (_owner is { } owner)
        {
            var layers = owner.LayerList;
            for (var i = layers.Count - 1; i >= 0; i--)
            {
                layers[i].OnPointerDragCancelled();
            }
        }
    }

    /// <summary>
    /// Scrolls while a layer holds a drag with the pointer at the edge of the viewport or beyond it: a selection has to reach the
    /// pages it cannot see. The pointer may rest there, so a timer does it, and the layer hears of a move at each step.
    /// </summary>
    private void UpdateDragScroll()
    {
        if (DragScrollStep(_lastPointer) == default)
        {
            _dragScroll?.Stop();
            return;
        }
        _dragScroll ??= new DispatcherTimer(DragScrollInterval, DispatcherPriority.Normal, OnDragScrollTick);
        if (!_dragScroll.IsEnabled)
        {
            _dragScroll.Start();
        }
    }

    private void OnDragScrollTick(object? sender, EventArgs e)
    {
        var step = DragScrollStep(_lastPointer);
        if (!_layerHasDrag || step == default)
        {
            _dragScroll?.Stop();
            return;
        }
        var before = _offset;
        ScrollBy(step);
        if (_offset != before)
        {
            DispatchLayers(LayerEvent.Moved, _lastPointer, _dragModifiers, 0);
            UpdateCursor();
        }
    }

    private Vector DragScrollStep(Point pointer) => new(DragScrollAxis(pointer.X, _viewport.Width), DragScrollAxis(pointer.Y, _viewport.Height));

    private static double DragScrollAxis(double position, double length)
    {
        var over = position < DragScrollBand ? position - DragScrollBand
            : position > length - DragScrollBand ? position - (length - DragScrollBand)
            : 0;
        return Math.Clamp(over * 0.5, -DragScrollMaxStep, DragScrollMaxStep);
    }

    private enum LayerEvent
    {
        Pressed,
        Moved,
        Released
    }

    /// <summary>Sends a pointer event to the layers, last first, until one handles it. True when one did.</summary>
    private bool DispatchLayers(LayerEvent kind, Point position, KeyModifiers modifiers, int clickCount)
    {
        var owner = _owner!;
        var layers = owner.LayerList;
        if (layers.Count == 0)
        {
            return false;
        }
        var layout = owner.Layout;
        var content = position + _offset;
        int page;
        Point pointInPage;
        if (_layerHasDrag)
        {
            // A drag keeps going over the canvas: the nearest page takes it, with a point outside that page.
            (page, pointInPage) = layout.Locate(content, _viewport);
        }
        else if (!layout.HitTest(content, _viewport, out page, out pointInPage))
        {
            return false;
        }
        var args = new PageLayerPointerArgs(page, pointInPage, modifiers, clickCount, _pointerType);
        for (var i = layers.Count - 1; i >= 0; i--)
        {
            switch (kind)
            {
                case LayerEvent.Pressed:
                    layers[i].OnPointerPressed(args);
                    break;
                case LayerEvent.Moved:
                    layers[i].OnPointerMoved(args);
                    break;
                default:
                    layers[i].OnPointerReleased(args);
                    break;
            }
            if (args.Handled)
            {
                return true;
            }
        }
        return false;
    }

    /// <summary>The cursor is computed, not styled: which layer's cursor applies depends on the point under the pointer.</summary>
    private void UpdateCursor()
    {
        Cursor? cursor = null;
        if (_owner is { } owner)
        {
            if (_isPanning)
            {
                cursor = s_panningCursor ??= new Cursor(StandardCursorType.SizeAll);
            }
            else if (_spaceHeld || owner.InteractionMode == PageInteractionMode.Pan)
            {
                cursor = s_panCursor ??= new Cursor(StandardCursorType.Hand);
            }
            else if (_hasPointer && owner.LayerList.Count > 0 && owner.Layout.HitTest(_lastPointer + _offset, _viewport, out var page, out var point))
            {
                var layers = owner.LayerList;
                for (var i = layers.Count - 1; i >= 0 && cursor is null; i--)
                {
                    cursor = layers[i].GetCursor(page, point);
                }
            }
        }
        if (ReferenceEquals(cursor, _cursor))
        {
            return;
        }
        _cursor = cursor;
        if (cursor is null)
        {
            ClearValue(CursorProperty);
        }
        else
        {
            SetCurrentValue(CursorProperty, cursor);
        }
    }

    /// <summary>A layer has something new to show, and it may want another cursor (the text under the pointer arrived).</summary>
    public void OnLayerInvalidated()
    {
        UpdateCursor();
        InvalidateVisual();
    }

    /// <summary>The interaction mode changed: the cursor may too.</summary>
    public void OnInteractionModeChanged() => UpdateCursor();

    #endregion
}
