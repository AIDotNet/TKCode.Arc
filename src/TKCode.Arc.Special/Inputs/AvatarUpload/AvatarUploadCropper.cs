using TKCode.Arc.Gestures;
using TKCode.Arc.Motion;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;

namespace TKCode.Arc;

/// <summary>
/// The framing area of an <see cref="AvatarUpload"/>: the picture under a dimmed square with a clear circle. Drag (or the
/// arrow keys) moves the picture, the wheel, a pinch, plus and minus zoom it; the picture always covers the circle. The
/// view is a <see cref="PanZoomTransform"/> whose world is the picture's pixels, so the crop is exact at any scale.
/// </summary>
internal sealed class AvatarUploadCropper : Control
{
    public static readonly StyledProperty<double> RelativeZoomProperty =
        AvaloniaProperty.Register<AvatarUploadCropper, double>(nameof(RelativeZoom), 1);

    public static readonly StyledProperty<double> MaxZoomProperty =
        AvaloniaProperty.Register<AvatarUploadCropper, double>(nameof(MaxZoom), 4);

    public static readonly StyledProperty<double> InsetProperty =
        AvaloniaProperty.Register<AvatarUploadCropper, double>(nameof(Inset), 16);

    public static readonly StyledProperty<IBrush?> BackgroundProperty =
        AvaloniaProperty.Register<AvatarUploadCropper, IBrush?>(nameof(Background));

    public static readonly StyledProperty<IBrush?> ScrimProperty =
        AvaloniaProperty.Register<AvatarUploadCropper, IBrush?>(nameof(Scrim));

    public static readonly StyledProperty<IBrush?> RingProperty =
        AvaloniaProperty.Register<AvatarUploadCropper, IBrush?>(nameof(Ring));

    public static readonly StyledProperty<IBrush?> FocusRingProperty =
        AvaloniaProperty.Register<AvatarUploadCropper, IBrush?>(nameof(FocusRing));

    public static readonly StyledProperty<CornerRadius> CornerRadiusProperty =
        AvaloniaProperty.Register<AvatarUploadCropper, CornerRadius>(nameof(CornerRadius));

    /// <summary>A zoom step of the buttons and the plus and minus keys, relative to the covering zoom.</summary>
    public const double ZoomStep = 0.25;

    /// <summary>The arrow keys move the picture by this many pixels (four times as far with Shift).</summary>
    public const double KeyStep = 8;

    private PanZoomTransform _view = PanZoomTransform.Identity;
    private Bitmap? _image;
    private Point? _dragLast;
    private bool _applyingZoom;
    private double _pinchStartZoom;
    private MotionValue<double>? _zoomMotion;
    private StreamGeometry? _scrim;
    private Size _scrimSize;
    private Pen? _ringPen;
    private Pen? _focusPen;

    static AvatarUploadCropper()
    {
        FocusableProperty.OverrideDefaultValue<AvatarUploadCropper>(true);
        AffectsRender<AvatarUploadCropper>(BackgroundProperty, ScrimProperty, RingProperty, FocusRingProperty, CornerRadiusProperty);
        CursorProperty.OverrideDefaultValue<AvatarUploadCropper>(new Cursor(StandardCursorType.SizeAll));
    }

    public AvatarUploadCropper()
    {
        GestureRecognizers.Add(new PinchGestureRecognizer());
        AddHandler(InputElement.PinchEvent, OnPinch);
        AddHandler(InputElement.PinchEndedEvent, OnPinchEnded);
    }

    /// <summary>Zoom relative to the zoom where the picture just covers the circle (1 to <see cref="MaxZoom"/>).</summary>
    public double RelativeZoom { get => GetValue(RelativeZoomProperty); set => SetValue(RelativeZoomProperty, value); }

    public double MaxZoom { get => GetValue(MaxZoomProperty); set => SetValue(MaxZoomProperty, value); }

    /// <summary>The space between the circle and the frame's edges.</summary>
    public double Inset { get => GetValue(InsetProperty); set => SetValue(InsetProperty, value); }

    public IBrush? Background { get => GetValue(BackgroundProperty); set => SetValue(BackgroundProperty, value); }

    /// <summary>The dimming over the picture outside the circle.</summary>
    public IBrush? Scrim { get => GetValue(ScrimProperty); set => SetValue(ScrimProperty, value); }

    /// <summary>The circle's outline.</summary>
    public IBrush? Ring { get => GetValue(RingProperty); set => SetValue(RingProperty, value); }

    public IBrush? FocusRing { get => GetValue(FocusRingProperty); set => SetValue(FocusRingProperty, value); }

    public CornerRadius CornerRadius { get => GetValue(CornerRadiusProperty); set => SetValue(CornerRadiusProperty, value); }

    public Bitmap? Image => _image;

    internal PanZoomTransform View => _view;

    /// <summary>Raised on Enter (save) and Escape (cancel).</summary>
    public event Action<bool>? Committed;

    /// <summary>The circle in the frame's coordinates.</summary>
    public Rect Circle
    {
        get
        {
            var size = Bounds.Size;
            var d = Math.Max(0, Math.Min(size.Width, size.Height) - 2 * Inset);
            return new Rect((size.Width - d) / 2, (size.Height - d) / 2, d, d);
        }
    }

    /// <summary>The zoom at which the picture just covers the circle.</summary>
    private double CoverZoom
    {
        get
        {
            if (_image is null)
            {
                return 1;
            }
            var circle = Circle;
            var px = _image.PixelSize;
            return circle.Width <= 0 ? 1 : circle.Width / Math.Max(1, Math.Min(px.Width, px.Height));
        }
    }

    /// <summary>Shows <paramref name="image"/> centered at the covering zoom.</summary>
    public void SetImage(Bitmap? image)
    {
        _image = image;
        _zoomMotion?.Stop();
        Reset();
    }

    /// <summary>Centers the picture at the covering zoom.</summary>
    public void Reset()
    {
        _applyingZoom = true;
        try
        {
            SetCurrentValue(RelativeZoomProperty, 1.0);
        }
        finally
        {
            _applyingZoom = false;
        }
        FitCover();
        InvalidateVisual();
    }

    private void FitCover()
    {
        if (_image is null || Bounds.Width <= 0)
        {
            _view = PanZoomTransform.Identity;
            return;
        }
        var zoom = CoverZoom * Math.Clamp(RelativeZoom, 1, Math.Max(1, MaxZoom));
        var px = _image.PixelSize;
        var center = Circle.Center;
        _view = new PanZoomTransform(new Point(px.Width / 2.0 - center.X / zoom, px.Height / 2.0 - center.Y / zoom), zoom);
        Clamp();
    }

    /// <summary>Keeps the circle inside the picture.</summary>
    private void Clamp()
    {
        if (_image is null)
        {
            return;
        }
        var px = _image.PixelSize;
        var world = _view.ScreenToWorld(Circle);
        var origin = _view.Origin;
        if (world.Width >= px.Width)
        {
            origin = origin.WithX(origin.X + (px.Width - world.Width) / 2 - world.X);
        }
        else if (world.X < 0)
        {
            origin = origin.WithX(origin.X - world.X);
        }
        else if (world.Right > px.Width)
        {
            origin = origin.WithX(origin.X - (world.Right - px.Width));
        }
        if (world.Height >= px.Height)
        {
            origin = origin.WithY(origin.Y + (px.Height - world.Height) / 2 - world.Y);
        }
        else if (world.Y < 0)
        {
            origin = origin.WithY(origin.Y - world.Y);
        }
        else if (world.Bottom > px.Height)
        {
            origin = origin.WithY(origin.Y - (world.Bottom - px.Height));
        }
        _view = _view with { Origin = origin };
    }

    /// <summary>Moves the picture by a distance on screen.</summary>
    public void PanBy(Vector screenDelta)
    {
        if (_image is null)
        {
            return;
        }
        _view = _view.PanBy(screenDelta);
        Clamp();
        InvalidateVisual();
    }

    /// <summary>Zooms around a point of the frame (the circle's center when null), within 1 to <see cref="MaxZoom"/>.</summary>
    public void ZoomTo(double relative, Point? anchor = null)
    {
        relative = Math.Clamp(relative, 1, Math.Max(1, MaxZoom));
        if (_image is null)
        {
            return;
        }
        var cover = CoverZoom;
        _view = _view.ZoomTo(cover * relative, anchor ?? Circle.Center);
        Clamp();
        _applyingZoom = true;
        try
        {
            SetCurrentValue(RelativeZoomProperty, relative);
        }
        finally
        {
            _applyingZoom = false;
        }
        InvalidateVisual();
    }

    /// <summary>Zooms by a step on a spring (Full motion) or at once.</summary>
    public void StepZoom(int direction)
    {
        var target = Math.Clamp((_zoomMotion?.IsAnimating == true ? _zoomTarget : RelativeZoom) + direction * ZoomStep, 1, Math.Max(1, MaxZoom));
        _zoomTarget = target;
        if (!ArcMotion.IsFull(this))
        {
            ZoomTo(target);
            return;
        }
        if (_zoomMotion is null)
        {
            // Code-driven: every frame of the zoom must clamp the pan again, which a style transition cannot do.
            _zoomMotion = new MotionValue<double>(this, RelativeZoom, MotionVectors.Double);
            _zoomMotion.Changed += v => ZoomTo(v);
        }
        if (!_zoomMotion.IsAnimating)
        {
            _zoomMotion.Jump(RelativeZoom);
        }
        _ = _zoomMotion.AnimateTo(target, MotionSpec.Spring(ArcSpring.Snappy));
    }

    private double _zoomTarget = 1;

    /// <summary>The square of the picture, in its pixels, under the circle.</summary>
    public PixelRect CropRect()
    {
        if (_image is null)
        {
            return default;
        }
        var px = _image.PixelSize;
        var world = _view.ScreenToWorld(Circle);
        var side = (int)Math.Round(Math.Min(world.Width, Math.Min(px.Width, px.Height)));
        side = Math.Max(1, side);
        var x = (int)Math.Round(world.X);
        var y = (int)Math.Round(world.Y);
        x = Math.Clamp(x, 0, Math.Max(0, px.Width - side));
        y = Math.Clamp(y, 0, Math.Max(0, px.Height - side));
        return new PixelRect(x, y, side, side);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == RelativeZoomProperty && !_applyingZoom)
        {
            // The slider (or code) set the zoom: zoom around the circle's center.
            _zoomMotion?.Stop();
            ZoomTo(change.GetNewValue<double>());
        }
        else if (change.Property == BoundsProperty)
        {
            var oldSize = change.GetOldValue<Rect>().Size;
            if (oldSize != Bounds.Size)
            {
                FitCover();
            }
        }
        else if (change.Property == RingProperty)
        {
            _ringPen = null;
        }
        else if (change.Property == FocusRingProperty)
        {
            _focusPen = null;
        }
    }

    #region Input

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        var point = e.GetCurrentPoint(this);
        if (!point.Properties.IsLeftButtonPressed || _image is null)
        {
            return;
        }
        _zoomMotion?.Stop();
        _dragLast = point.Position;
        e.Pointer.Capture(this);
        Focus(NavigationMethod.Pointer);
        e.Handled = true;
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        if (_dragLast is not { } last)
        {
            return;
        }
        var position = e.GetPosition(this);
        PanBy(position - last);
        _dragLast = position;
        e.Handled = true;
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        if (_dragLast is not null)
        {
            _dragLast = null;
            e.Pointer.Capture(null);
            e.Handled = true;
        }
    }

    protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e)
    {
        base.OnPointerCaptureLost(e);
        _dragLast = null;
    }

    protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
    {
        base.OnPointerWheelChanged(e);
        if (_image is null || e.Delta.Y == 0)
        {
            return;
        }
        _zoomMotion?.Stop();
        // One notch is 10%; a touchpad's small deltas zoom smoothly.
        var factor = Math.Pow(1.1, Math.Clamp(e.Delta.Y, -3, 3));
        ZoomTo(RelativeZoom * factor, e.GetPosition(this));
        e.Handled = true;
    }

    private void OnPinch(object? sender, PinchEventArgs e)
    {
        if (_image is null)
        {
            return;
        }
        if (_pinchStartZoom <= 0)
        {
            _pinchStartZoom = RelativeZoom;
        }
        _zoomMotion?.Stop();
        ZoomTo(_pinchStartZoom * e.Scale, e.ScaleOrigin);
        e.Handled = true;
    }

    private void OnPinchEnded(object? sender, PinchEndedEventArgs e) => _pinchStartZoom = 0;

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Handled)
        {
            return;
        }
        var step = e.KeyModifiers.HasFlag(KeyModifiers.Shift) ? KeyStep * 4 : KeyStep;
        switch (e.Key)
        {
            // An arrow moves the view that way, so the picture slides the other way under the circle.
            case Key.Left:
                PanBy(new Vector(step, 0));
                break;
            case Key.Right:
                PanBy(new Vector(-step, 0));
                break;
            case Key.Up:
                PanBy(new Vector(0, step));
                break;
            case Key.Down:
                PanBy(new Vector(0, -step));
                break;
            case Key.OemPlus or Key.Add:
                StepZoom(1);
                break;
            case Key.OemMinus or Key.Subtract:
                StepZoom(-1);
                break;
            case Key.D0 or Key.NumPad0:
                _zoomMotion?.Stop();
                Reset();
                break;
            case Key.Enter:
                Committed?.Invoke(true);
                break;
            case Key.Escape:
                Committed?.Invoke(false);
                break;
            default:
                return;
        }
        e.Handled = true;
    }

    protected override void OnGotFocus(FocusChangedEventArgs e)
    {
        base.OnGotFocus(e);
        InvalidateVisual();
    }

    protected override void OnLostFocus(FocusChangedEventArgs e)
    {
        base.OnLostFocus(e);
        InvalidateVisual();
    }

    #endregion

    #region Drawing

    public override void Render(DrawingContext context)
    {
        var bounds = new Rect(Bounds.Size);
        var radius = CornerRadius;
        var frame = new RoundedRect(bounds, radius);
        if (Background is { } background)
        {
            context.DrawRectangle(background, null, frame);
        }
        else
        {
            context.FillRectangle(Brushes.Transparent, bounds);
        }
        var circle = Circle;
        using (context.PushClip(frame))
        {
            if (_image is { } image)
            {
                var px = image.PixelSize;
                var dest = _view.WorldToScreen(new Rect(0, 0, px.Width, px.Height));
                // The source rectangle is in pixels (see the implementation guide's DrawImage pitfall).
                context.DrawImage(image, new Rect(0, 0, px.Width, px.Height), dest);
            }
            if (Scrim is { } scrim)
            {
                context.DrawGeometry(scrim, null, ScrimGeometry(bounds.Size, circle));
            }
        }
        if (Ring is { } ring)
        {
            _ringPen ??= new Pen(ring, 2);
            context.DrawEllipse(null, _ringPen, circle.Center, circle.Width / 2, circle.Height / 2);
        }
        if (IsFocused && Classes.Contains(":focus-visible") && FocusRing is { } focus)
        {
            _focusPen ??= new Pen(focus, 2);
            context.DrawEllipse(null, _focusPen, circle.Center, circle.Width / 2 + 4, circle.Height / 2 + 4);
        }
    }

    private StreamGeometry ScrimGeometry(Size size, Rect circle)
    {
        if (_scrim is not null && _scrimSize == size)
        {
            return _scrim;
        }
        _scrimSize = size;
        var geometry = new StreamGeometry();
        using (var ctx = geometry.Open())
        {
            ctx.SetFillRule(FillRule.EvenOdd);
            ctx.BeginFigure(new Point(0, 0), true);
            ctx.LineTo(new Point(size.Width, 0));
            ctx.LineTo(new Point(size.Width, size.Height));
            ctx.LineTo(new Point(0, size.Height));
            ctx.EndFigure(true);
            var r = circle.Width / 2;
            if (r > 0)
            {
                var c = circle.Center;
                ctx.BeginFigure(new Point(c.X + r, c.Y), true);
                ctx.ArcTo(new Point(c.X - r, c.Y), new Size(r, r), 0, false, SweepDirection.Clockwise);
                ctx.ArcTo(new Point(c.X + r, c.Y), new Size(r, r), 0, false, SweepDirection.Clockwise);
                ctx.EndFigure(true);
            }
        }
        _scrim = geometry;
        return geometry;
    }

    #endregion
}
