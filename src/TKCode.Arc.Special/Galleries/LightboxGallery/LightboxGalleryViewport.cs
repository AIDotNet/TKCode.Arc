using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using TKCode.Arc.Gestures;
using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;

namespace TKCode.Arc;

/// <summary>What a drag in the lightbox is doing, decided when it starts.</summary>
internal enum LightboxGalleryDragMode
{
    None,

    /// <summary>Sideways while not zoomed: brings in the next or previous picture.</summary>
    Swipe,

    /// <summary>Up or down while not zoomed: pulls the picture away to close.</summary>
    Dismiss,

    /// <summary>Any direction while zoomed: moves the enlarged picture.</summary>
    Pan
}

/// <summary>
/// The picture area of the lightbox: three pooled photos (previous, current, next) side by side, moved together by one
/// slide offset; the current one also carries the pan and zoom (<see cref="PanZoomTransform"/>) and the pull-to-close
/// offset. Wheel, trackpad pinch and touch pinch zoom about the pointer (<see cref="WheelRouter"/>), a double click or
/// double tap zooms in and out, and one <see cref="DragTracker"/> swipes, pans or pulls depending on how it starts.
/// </summary>
/// <remarks>
/// Code-driven motion: every move is handed the gesture's velocity (springs keep it), and the zoom animates between two
/// views with the anchor held still (<see cref="PanZoomTransform.Interpolate"/>), which ControlTheme transitions cannot
/// express. Nothing is laid out per frame: each frame writes three matrices.
/// </remarks>
internal sealed class LightboxGalleryViewport : Control
{
    /// <summary>Zoom of a double click or double tap.</summary>
    internal const double DoubleTapZoom = 2.5;

    /// <summary>A pull farther than this share of the height closes the lightbox when let go.</summary>
    internal const double DismissDistance = 0.18;

    /// <summary>A pull released faster than this (px/s) closes the lightbox.</summary>
    internal const double DismissVelocity = 700;

    /// <summary>Space between two pictures while they slide, set by the owner.</summary>
    public static readonly StyledProperty<double> GapProperty =
        AvaloniaProperty.Register<LightboxGalleryViewport, double>(nameof(Gap), 32);

    /// <summary>Space between the picture and the edges, set by the theme.</summary>
    public static readonly StyledProperty<Thickness> PhotoMarginProperty =
        AvaloniaProperty.Register<LightboxGalleryViewport, Thickness>(nameof(PhotoMargin));

    private static readonly SpringSpec s_slide = SpringSpec.FromVisual(0.42, 0);
    private static readonly SpringSpec s_back = SpringSpec.FromVisual(0.36, 0.1);
    private static readonly MotionSpec s_zoomSpring = MotionSpec.Spring(SpringSpec.FromVisual(0.36, 0));
    private static readonly MotionSpec s_fade = MotionSpec.Tween(ArcMotionPresets.Fast, ArcEasings.Standard);

    private readonly Decorator[] _holders = new Decorator[3];
    private readonly LightboxGalleryPhoto[] _photos = new LightboxGalleryPhoto[3];
    private readonly MatrixTransform[] _transforms = [new(), new(), new()];
    private readonly DragTracker _drag;
    private readonly WheelRouter _wheel = new() { Mode = WheelMode.Zoom };
    private MotionValue<double>? _offset;
    private MotionValue<double>? _dismiss;
    private MotionValue<double>? _zoomProgress;
    private MotionValue<double>? _fade;
    private PanZoomTransform _view = PanZoomTransform.Identity;
    private PanZoomTransform _zoomFrom = PanZoomTransform.Identity;
    private PanZoomTransform _zoomTo = PanZoomTransform.Identity;
    private PanZoomTransform _panStart = PanZoomTransform.Identity;
    private LightboxGalleryDragMode _mode;
    private double _dragStart;
    private int _generation;
    private bool _zoomed;

    static LightboxGalleryViewport()
    {
        FocusableProperty.OverrideDefaultValue<LightboxGalleryViewport>(true);
        FocusAdornerProperty.OverrideDefaultValue<LightboxGalleryViewport>(null);
        AffectsArrange<LightboxGalleryViewport>(GapProperty);
    }

    public LightboxGalleryViewport()
    {
        for (var i = 0; i < 3; i++)
        {
            var photo = new LightboxGalleryPhoto
            {
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                IsFlightTarget = i == 1
            };
            var holder = new Decorator
            {
                Child = photo,
                RenderTransformOrigin = new RelativePoint(0, 0, RelativeUnit.Absolute),
                RenderTransform = _transforms[i]
            };
            _photos[i] = photo;
            _holders[i] = holder;
            VisualChildren.Add(holder);
            LogicalChildren.Add(holder);
        }
        ClipToBounds = true;
        _drag = new DragTracker(this);
        _drag.Started += (_, e) => OnDragStarted(e);
        _drag.Moved += (_, e) => OnDragMoved(e);
        _drag.Completed += (_, e) => OnDragEnded(e.Velocity);
        _drag.Cancelled += (_, _) => OnDragEnded(default);
        GestureRecognizers.Add(new PinchGestureRecognizer());
        AddHandler(PinchEvent, OnPinch);
        AddHandler(PinchEndedEvent, (_, _) => _wheel.EndPinch());
        AddHandler(PointerTouchPadGestureMagnifyEvent, OnMagnify);
        DoubleTapped += OnDoubleTapped;
        Tapped += OnTapped;
    }

    internal LightboxGallery? Owner { get; set; }

    public double Gap
    {
        get => GetValue(GapProperty);
        set => SetValue(GapProperty, value);
    }

    public Thickness PhotoMargin
    {
        get => GetValue(PhotoMarginProperty);
        set => SetValue(PhotoMarginProperty, value);
    }

    /// <summary>The photo in the middle: the one shown, and the one that flies.</summary>
    internal LightboxGalleryPhoto Current => _photos[1];

    /// <summary>The current pan and zoom of the picture.</summary>
    internal PanZoomTransform View => _view;

    internal bool IsZoomed => _view.Zoom > 1.001;

    internal double Offset => _offset?.Value ?? 0;

    internal double DismissOffset => _dismiss?.Value ?? 0;

    private double Pitch => Bounds.Width + Math.Max(0, Gap);

    private double MaxZoom => Math.Max(1, Owner?.MaxZoom ?? 4);

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == PhotoMarginProperty)
        {
            foreach (var photo in _photos)
            {
                photo.Margin = change.GetNewValue<Thickness>();
            }
        }
    }

    // Pictures -----------------------------------------------------------------------------------------------------------

    private MotionValue<double> OffsetValue => _offset ??= Value(0, _ => Apply());

    private MotionValue<double> DismissValue => _dismiss ??= Value(0, v =>
    {
        Apply();
        Owner?.OnDismissProgress(DismissProgress(v));
    });

    private MotionValue<double> ZoomProgress => _zoomProgress ??= Value(1, p =>
    {
        _view = PanZoomTransform.Interpolate(_zoomFrom, _zoomTo, p);
        Apply();
    });

    private MotionValue<double> Fade => _fade ??= Value(1, v => _holders[1].Opacity = Math.Clamp(v, 0, 1));

    private MotionValue<double> Value(double initial, Action<double> changed)
    {
        var value = new MotionValue<double>(this, initial, MotionVectors.Double);
        value.Changed += changed;
        return value;
    }

    private double DismissProgress(double offset) => Bounds.Height > 0 ? Math.Clamp(Math.Abs(offset) / (Bounds.Height * 0.5), 0, 1) : 0;

    /// <summary>Shows the pictures around <paramref name="index"/> at rest: no slide, no zoom, no pull.</summary>
    internal void ShowAtRest(int index)
    {
        _generation++;
        OffsetValue.Jump(0);
        DismissValue.Jump(0);
        Fade.Jump(1);
        ResetView();
        SetSources(index, null, 0);
        Apply();
    }

    /// <summary>
    /// Moves from <paramref name="previous"/> to <paramref name="index"/>, keeping the old picture in the slot it leaves by
    /// (see <see cref="ProductGalleryStage.Show"/>), starting from <paramref name="startOffset"/> with
    /// <paramref name="velocity"/> px/s.
    /// </summary>
    internal void Show(int index, int previous, double startOffset = 0, double velocity = 0)
    {
        var direction = Math.Sign(index - previous);
        if (direction == 0 || previous < 0)
        {
            ShowAtRest(index);
            return;
        }
        ResetView();
        DismissValue.Jump(0);
        SetSources(index, previous, direction);
        var generation = ++_generation;
        if (ArcMotion.IsFull(this) && Bounds.Width > 0)
        {
            OffsetValue.Jump(startOffset + direction * Pitch);
            Fade.Jump(1);
            Apply();
            _ = SettleAsync(generation, velocity);
            return;
        }
        OffsetValue.Jump(0);
        Apply();
        if (ArcMotion.GetEffectiveLevel(this) == ArcMotionLevel.Reduced)
        {
            Fade.Jump(0);
            _ = FadeInAsync(generation);
        }
        else
        {
            Fade.Jump(1);
            RestoreNeighbours();
        }
    }

    private async Task SettleAsync(int generation, double velocity)
    {
        var arrived = await MomentumProjector.SettleTo(OffsetValue, 0, velocity, s_slide);
        if (arrived && generation == _generation)
        {
            RestoreNeighbours();
        }
    }

    private async Task FadeInAsync(int generation)
    {
        var arrived = await Fade.AnimateTo(1, s_fade);
        if (arrived && generation == _generation)
        {
            RestoreNeighbours();
        }
    }

    private void RestoreNeighbours()
    {
        if (Owner is { } owner)
        {
            SetSources(owner.OpenIndex, null, 0);
        }
    }

    private void SetSources(int index, int? behind, int direction)
    {
        Load(_photos[1], index);
        Load(_photos[0], direction > 0 && behind is { } a ? a : index - 1);
        Load(_photos[2], direction < 0 && behind is { } b ? b : index + 1);
    }

    private void Load(LightboxGalleryPhoto photo, int index)
    {
        if (Owner?.ImageAt(index) is { } image)
        {
            photo.SetSources(image.Source ?? image.Thumbnail, image.Thumbnail, image.AspectRatio);
        }
        else
        {
            photo.SetSources(null, null, 1);
        }
    }

    // Zoom ---------------------------------------------------------------------------------------------------------------

    private void ResetView()
    {
        _zoomProgress?.Stop();
        _view = PanZoomTransform.Identity;
        SetZoomed();
    }

    private void SetZoomed()
    {
        var zoomed = IsZoomed;
        if (zoomed != _zoomed)
        {
            _zoomed = zoomed;
            Owner?.OnZoomChanged(zoomed);
        }
    }

    /// <summary>Zooms by <paramref name="factor"/> about <paramref name="anchor"/> (viewport coordinates), animated unless told not to.</summary>
    internal void ZoomBy(double factor, Point anchor, bool animate)
    {
        var current = _zoomProgress is { IsAnimating: true } ? _zoomTo : _view;
        var target = Clamp(current.ZoomBy(factor, anchor, 1, MaxZoom));
        GoTo(target, animate);
    }

    /// <summary>Zooms about the middle of the viewport.</summary>
    internal void ZoomBy(double factor) => ZoomBy(factor, new Point(Bounds.Width / 2, Bounds.Height / 2), animate: true);

    /// <summary>Returns to the whole picture.</summary>
    internal void ResetZoom(bool animate = true) => GoTo(PanZoomTransform.Identity, animate);

    /// <summary>Moves a zoomed picture by <paramref name="delta"/> px.</summary>
    internal void PanBy(Vector delta)
    {
        if (!IsZoomed)
        {
            return;
        }
        GoTo(Clamp(_view.PanBy(delta)), animate: true);
    }

    private void GoTo(PanZoomTransform target, bool animate)
    {
        if (animate && ArcMotion.IsFull(this))
        {
            _zoomFrom = _view;
            _zoomTo = target;
            ZoomProgress.Jump(0);
            _ = ZoomProgress.AnimateTo(1, s_zoomSpring);
        }
        else
        {
            _zoomProgress?.Stop();
            _zoomFrom = _zoomTo = target;
            _view = target;
            Apply();
        }
        // The state the view is heading to decides the zoomed flag and the zoom buttons, which follow at once.
        _zoomed = target.Zoom > 1.001;
        Owner?.OnZoomChanged(_zoomed);
    }

    /// <summary>Keeps the picture covering the viewport where it can, and centred along an axis where it is smaller.</summary>
    internal PanZoomTransform Clamp(PanZoomTransform view)
    {
        var content = _photos[1].Bounds;
        if (content.Width <= 0 || content.Height <= 0 || Bounds.Width <= 0)
        {
            return view;
        }
        var screen = view.WorldToScreen(content);
        var dx = Fit(screen.Left, screen.Right, Bounds.Width);
        var dy = Fit(screen.Top, screen.Bottom, Bounds.Height);
        return dx == 0 && dy == 0 ? view : view.PanBy(new Vector(dx, dy));
    }

    private static double Fit(double start, double end, double extent)
    {
        var size = end - start;
        if (size <= extent)
        {
            return (extent - size) / 2 - start;
        }
        if (start > 0)
        {
            return -start;
        }
        return end < extent ? extent - end : 0;
    }

    protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
    {
        base.OnPointerWheelChanged(e);
        var action = _wheel.Route(e.Delta, e.KeyModifiers, TimeSpan.FromMilliseconds(e.Timestamp));
        if (action.Kind == WheelActionKind.Zoom && Math.Abs(Offset) < 0.5)
        {
            ZoomBy(action.ZoomFactor, e.GetPosition(this), animate: false);
            e.Handled = true;
        }
    }

    private void OnMagnify(object? sender, PointerDeltaEventArgs e)
    {
        var action = _wheel.RouteMagnify(e.Delta.X);
        if (action.Kind == WheelActionKind.Zoom)
        {
            ZoomBy(action.ZoomFactor, e.GetPosition(this), animate: false);
            e.Handled = true;
        }
    }

    private void OnPinch(object? sender, PinchEventArgs e)
    {
        var action = _wheel.RoutePinch(e.Scale);
        if (action.Kind == WheelActionKind.Zoom)
        {
            if (_drag.IsDragging)
            {
                _drag.Cancel();
            }
            ZoomBy(action.ZoomFactor, e.ScaleOrigin, animate: false);
            e.Handled = true;
        }
    }

    private void OnDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (IsZoomed)
        {
            ResetZoom();
        }
        else
        {
            ZoomBy(DoubleTapZoom, e.GetPosition(this), animate: true);
        }
        e.Handled = true;
    }

    private void OnTapped(object? sender, TappedEventArgs e)
    {
        // A click on the backdrop beside the picture closes the lightbox.
        if (IsZoomed || Owner is not { } owner)
        {
            return;
        }
        var p = e.GetPosition(_photos[1]);
        if (!new Rect(_photos[1].Bounds.Size).Contains(p))
        {
            owner.RequestClose(DismissReason.PressOutside);
        }
    }

    // Dragging -----------------------------------------------------------------------------------------------------------

    private void OnDragStarted(DragGestureEventArgs e)
    {
        _generation++;
        _offset?.Stop();
        _dismiss?.Stop();
        _zoomProgress?.Stop();
        _zoomFrom = _zoomTo = _view;
        var offset = e.Offset;
        if (IsZoomed)
        {
            _mode = LightboxGalleryDragMode.Pan;
            _panStart = _view;
        }
        else if (Math.Abs(offset.X) >= Math.Abs(offset.Y) && Owner is { Count: > 1 })
        {
            _mode = LightboxGalleryDragMode.Swipe;
            _dragStart = Offset;
            Fade.Jump(1);
        }
        else
        {
            _mode = LightboxGalleryDragMode.Dismiss;
            _dragStart = DismissOffset;
        }
    }

    private void OnDragMoved(DragGestureEventArgs e)
    {
        switch (_mode)
        {
            case LightboxGalleryDragMode.Pan:
            {
                // Past the picture's edge the pan resists.
                var raw = _panStart.PanBy(e.Offset);
                var clamped = Clamp(raw);
                var over = raw.WorldToScreen(new Point()) - clamped.WorldToScreen(new Point());
                var soft = new Vector(RubberBand.Apply(over.X, Math.Max(1, Bounds.Width)), RubberBand.Apply(over.Y, Math.Max(1, Bounds.Height)));
                _view = clamped.PanBy(soft);
                Apply();
                break;
            }
            case LightboxGalleryDragMode.Swipe when Owner is { } owner:
            {
                var index = owner.OpenIndex;
                var min = index >= owner.Count - 1 ? 0 : -Pitch;
                var max = index <= 0 ? 0 : Pitch;
                OffsetValue.Jump(RubberBand.Clamp(_dragStart + e.Offset.X, min, max, Math.Max(1, Bounds.Width)));
                break;
            }
            case LightboxGalleryDragMode.Dismiss:
                DismissValue.Jump(_dragStart + e.Offset.Y);
                break;
        }
    }

    private void OnDragEnded(Vector velocity)
    {
        var mode = _mode;
        _mode = LightboxGalleryDragMode.None;
        switch (mode)
        {
            case LightboxGalleryDragMode.Pan:
            {
                // Coast in the direction of the flick, then settle inside the picture.
                var coast = new Vector(MomentumProjector.Project(velocity.X, 0.995), MomentumProjector.Project(velocity.Y, 0.995));
                GoTo(Clamp(_view.PanBy(coast)), animate: true);
                break;
            }
            case LightboxGalleryDragMode.Swipe when Owner is { } owner:
            {
                Span<double> points = [-Pitch, 0, Pitch];
                var choice = MomentumProjector.SelectSnapPoint(points, Offset, velocity.X,
                    SnapProjection.Linear(TimeSpan.FromMilliseconds(200), fromIndex: 1, flickVelocity: 360));
                var index = owner.OpenIndex;
                var target = choice switch
                {
                    0 when index < owner.Count - 1 => index + 1,
                    2 when index > 0 => index - 1,
                    _ => index
                };
                if (target == index)
                {
                    _ = SettleAsync(++_generation, velocity.X);
                }
                else
                {
                    owner.SelectFromSwipe(target, Offset, velocity.X);
                }
                break;
            }
            case LightboxGalleryDragMode.Dismiss:
            {
                var offset = DismissOffset;
                var far = Bounds.Height > 0 && Math.Abs(offset) > Bounds.Height * DismissDistance;
                var fast = Math.Abs(velocity.Y) > DismissVelocity && Math.Sign(velocity.Y) == Math.Sign(offset);
                if ((far || fast) && Owner is { } owner)
                {
                    owner.RequestClose(DismissReason.Gesture);
                }
                else
                {
                    _ = MomentumProjector.SettleTo(DismissValue, 0, velocity.Y, s_back);
                }
                break;
            }
        }
    }

    /// <summary>Lets go of a pull that is in progress (the lightbox is closing from it).</summary>
    internal void ReleasePull()
    {
        if (_drag.IsDragging)
        {
            _drag.Cancel();
        }
    }

    /// <summary>Returns a pulled picture to the middle (a close that was refused).</summary>
    internal void SettlePull()
    {
        if (Math.Abs(DismissOffset) > 0.5)
        {
            _ = MomentumProjector.SettleTo(DismissValue, 0, 0, s_back);
        }
    }

    // Layout and drawing -------------------------------------------------------------------------------------------------

    protected override Size MeasureOverride(Size availableSize)
    {
        var size = new Size(double.IsFinite(availableSize.Width) ? availableSize.Width : 800,
            double.IsFinite(availableSize.Height) ? availableSize.Height : 600);
        foreach (var holder in _holders)
        {
            holder.Measure(size);
        }
        return size;
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var pitch = finalSize.Width + Math.Max(0, Gap);
        for (var i = 0; i < _holders.Length; i++)
        {
            _holders[i].Arrange(new Rect((i - 1) * pitch, 0, finalSize.Width, finalSize.Height));
        }
        // A new size keeps the zoomed picture inside the new viewport.
        _view = Clamp(_view);
        Apply();
        return finalSize;
    }

    public override void Render(DrawingContext context)
    {
        // The whole area takes the pointer: drags start anywhere and a click beside the picture closes.
        context.FillRectangle(Brushes.Transparent, new Rect(Bounds.Size));
    }

    private void Apply()
    {
        var offset = Offset;
        var pull = DismissOffset;
        var progress = DismissProgress(pull);
        for (var i = 0; i < _holders.Length; i++)
        {
            Matrix matrix;
            if (i == 1)
            {
                matrix = _view.Matrix;
                if (pull != 0)
                {
                    // A pulled picture shrinks a little as it leaves, about its own centre.
                    var center = _photos[1].Bounds.Center;
                    var scale = 1 - 0.18 * progress;
                    matrix *= Matrix.CreateTranslation(-center.X, -center.Y) * Matrix.CreateScale(scale, scale) *
                              Matrix.CreateTranslation(center.X, center.Y + pull);
                }
                matrix *= Matrix.CreateTranslation(offset, 0);
            }
            else
            {
                matrix = Matrix.CreateTranslation(offset, 0);
            }
            _transforms[i].Matrix = matrix;
        }
        if (_photos[1].RenderTransform is not null)
        {
            _photos[1].OnFlightMoved();
        }
    }
}
