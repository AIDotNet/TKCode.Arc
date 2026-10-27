using TKCode.Arc.Primitives;
using TKCode.Arc.Motion;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Avalonia.Threading;

namespace TKCode.Arc;

/// <summary>
/// The surface a <see cref="MorphSelect"/> trigger grows into (P9). It lives in the select's popup, starts exactly
/// over the closed trigger and springs its width and height (corners stay at 22) to the open list and back, while the
/// material warms from the resting surface to the raised, floating one. The lid (trigger row) stays at the top and is
/// re-arranged as the width moves; the list face hangs under it at its own fixed width and fades in once the surface
/// opens. Everything outside the shape is clipped. The popup content keeps a fixed size around the largest shape (with
/// room for the shadow), so the host window never resizes per frame.
/// <para>When the popup had to slide up into view, the closed shape sits lower than the top of the popup (the drop), over
/// the trigger, and springs up to the top as it opens and back down as it closes.</para>
/// </summary>
internal sealed class MorphSelectSurface : Control
{
    /// <summary>Area-growing moves: 0.4s, bounce 0.12. Shrinking: 0.34s, no overshoot.</summary>
    internal static readonly SpringSpec Grow = SpringSpec.FromVisual(0.4, 0.12);
    internal static readonly SpringSpec Shrink = SpringSpec.FromVisual(0.34, 0);
    internal static readonly SpringSpec Widen = SpringSpec.FromVisual(0.42, 0.16);
    internal static readonly SpringSpec Narrow = SpringSpec.FromVisual(0.42, 0);

    private readonly Control _lid;
    private readonly Control _face;
    private readonly ClipHost _clipHost;
    private readonly BorderLayer _border;
    private readonly TranslateTransform _faceShift = new();
    private MotionValue<double>? _width;
    private MotionValue<double>? _height;
    private MotionValue<double>? _warm;
    private MotionValue<double>? _lift;
    private MotionValue<double>? _faceOpacity;
    private MotionValue<double>? _faceY;
    private MotionValue<double>? _drop;
    private double _w;
    private double _h;
    private double _d;
    private bool _atRest;
    private int _morphGeneration;
    private ImmutableSolidColorBrush? _fillBrush;

    public MorphSelectSurface(Control lid, Control face)
    {
        _lid = lid;
        _face = face;
        _face.RenderTransform = _faceShift;
        _clipHost = new ClipHost(this) { Children = { _face, _lid } };
        _border = new BorderLayer(this);
        VisualChildren.Add(_clipHost);
        VisualChildren.Add(_border);
        LogicalChildren.Add(_clipHost);
        // The transparent margin still belongs to the popup: a press there is a press outside the shape.
        Background = Brushes.Transparent;
    }

    public static readonly StyledProperty<IBrush?> BackgroundProperty =
        AvaloniaProperty.Register<MorphSelectSurface, IBrush?>(nameof(Background));

    public IBrush? Background { get => GetValue(BackgroundProperty); set => SetValue(BackgroundProperty, value); }

    /// <summary>Room around the shape for its floating shadow.</summary>
    public Thickness ShadowRoom { get; set; } = new(56, 40, 56, 80);

    public double LidHeight { get; set; } = 44;

    public double CornerRadiusValue { get; set; } = 22;

    private Size _capacity;
    private double _faceWidth;

    /// <summary>Largest shape this surface will show (sets the popup content size).</summary>
    public Size Capacity
    {
        get => _capacity;
        set
        {
            _capacity = value;
            InvalidateParts();
        }
    }

    /// <summary>Width of the list face (fixed while open).</summary>
    public double FaceWidth
    {
        get => _faceWidth;
        set
        {
            _faceWidth = value;
            InvalidateParts();
        }
    }

    private void InvalidateParts()
    {
        _clipHost?.InvalidateMeasure();
        InvalidateMeasure();
    }

    public bool AlignEnd { get; set; }

    public Color RestFill { get; set; }
    public Color OpenFill { get; set; }
    public BoxShadows RestShadow { get; set; }
    public BoxShadows OpenShadow { get; set; }
    public IPen? BorderPen { get; set; }

    /// <summary>Pressed outside the shape (on the shadow margin).</summary>
    public event EventHandler? OutsidePressed;

    public Size ShapeSize => new(_w, _h);

    public bool IsMoving => _width?.IsAnimating == true || _height?.IsAnimating == true;

    /// <summary>The shape's rectangle in this control's coordinates.</summary>
    public Rect ShapeRect
    {
        get
        {
            var room = ShadowRoom;
            var x = AlignEnd ? room.Left + Capacity.Width - _w : room.Left;
            return new Rect(x, room.Top + _d, _w, _h);
        }
    }

    /// <summary>
    /// Places the shape without motion (the closed trigger at open, or any jump). <paramref name="drop"/> is how far
    /// below the top of the popup the shape sits.
    /// </summary>
    public void Jump(Size shape, bool open, double drop)
    {
        Ensure();
        _width!.Jump(shape.Width);
        _height!.Jump(shape.Height);
        _drop!.Jump(drop);
        _warm!.Jump(open ? 1 : 0);
        _lift!.Jump(open ? 1 : 0);
        SetFace(open ? 1 : 0, open ? 0 : -6, open ? 0 : 2);
        _faceOpacity!.Jump(open ? 1 : 0);
        _faceY!.Jump(open ? 0 : 1);
        _morphGeneration++;
        _atRest = open;
        UpdateClip();
    }

    /// <summary>
    /// Springs to <paramref name="target"/> at <paramref name="drop"/> below the top of the popup; returns when the height
    /// settles (the visible end of the morph).
    /// </summary>
    public Task<bool> MorphTo(Size target, bool open, double drop)
    {
        Ensure();
        var lastW = _w;
        var lastH = _h;
        var growing = target.Width * target.Height >= lastW * lastH;
        var area = growing ? Grow : Shrink;
        var widthSpring = open ? area : target.Width < lastW ? Narrow : Widen;
        var generation = ++_morphGeneration;
        _atRest = false;
        UpdateClip();
        var widthDone = _width!.AnimateTo(target.Width, MotionSpec.Spring(widthSpring));
        var done = _height!.AnimateTo(target.Height, MotionSpec.Spring(area));
        if (Math.Abs(_drop!.Value - drop) > 0.01)
        {
            // Shrinking toward the trigger must not overshoot past it; the drop shares the height's area spring.
            _ = _drop.AnimateTo(drop, MotionSpec.Spring(open ? area : Shrink));
        }
        if (open)
        {
            // Settled and open: the shape no longer clips (the face already sits inside it), so hit testing and
            // rendering skip the geometry clip at rest.
            Task.WhenAll(widthDone, done).ContinueWith(t => Dispatcher.UIThread.Post(() =>
            {
                if (generation == _morphGeneration && t.Result.All(r => r))
                {
                    _atRest = true;
                    UpdateClip();
                }
            }), TaskScheduler.Default);
        }
        var level = ArcMotion.GetEffectiveLevel(this);
        var reduced = level != ArcMotionLevel.Full;
        _ = _warm!.AnimateTo(open ? 1 : 0, MotionSpec.Tween(ArcMotionPresets.Fast, ArcEasings.Standard));
        _ = _lift!.AnimateTo(open ? 1 : 0, MotionSpec.Tween(ArcMotionPresets.Considered, ArcEasings.Standard));
        if (open)
        {
            var delay = reduced ? TimeSpan.Zero : TimeSpan.FromMilliseconds(40);
            _ = _faceOpacity!.AnimateTo(1, MotionSpec.Tween(TimeSpan.FromMilliseconds(200), ArcEasings.Enter, delay));
            if (reduced)
            {
                _faceY!.Jump(0);
            }
            else
            {
                _ = _faceY!.AnimateTo(0, MotionSpec.Spring(Grow));
            }
        }
        else
        {
            _ = _faceOpacity!.AnimateTo(0, MotionSpec.Tween(TimeSpan.FromMilliseconds(100), ArcEasings.Standard));
            if (reduced)
            {
                _faceY!.Jump(0);
            }
            else
            {
                _ = _faceY!.AnimateTo(1, MotionSpec.Tween(TimeSpan.FromMilliseconds(100), ArcEasings.Standard));
            }
        }
        return done;
    }

    /// <summary>Moves the shape down by <paramref name="drop"/> at once and springs it back up to the top of the popup (0: stays at the top).</summary>
    public void RiseFrom(double drop)
    {
        Ensure();
        _drop!.Jump(drop);
        if (drop > 0)
        {
            _ = _drop.AnimateTo(0, MotionSpec.Spring(Grow));
        }
    }

    private void Ensure()
    {
        if (_width is not null)
        {
            return;
        }
        _width = new MotionValue<double>(this, 0, MotionVectors.Double);
        _width.Changed += v =>
        {
            _w = Math.Max(0, v);
            OnShapeChanged(arrangeLid: true);
        };
        _height = new MotionValue<double>(this, 0, MotionVectors.Double);
        _height.Changed += v =>
        {
            _h = Math.Max(0, v);
            OnShapeChanged(arrangeLid: false);
        };
        _warm = new MotionValue<double>(this, 0, MotionVectors.Double);
        _warm.Changed += _ => InvalidateVisual();
        _lift = new MotionValue<double>(this, 0, MotionVectors.Double);
        _lift.Changed += _ => InvalidateVisual();
        _faceOpacity = new MotionValue<double>(this, 0, MotionVectors.Double);
        _faceOpacity.Changed += _ => ApplyFace();
        _faceY = new MotionValue<double>(this, 1, MotionVectors.Double);
        _faceY.Changed += _ => ApplyFace();
        _drop = new MotionValue<double>(this, 0, MotionVectors.Double);
        _drop.Changed += v =>
        {
            _d = Math.Max(0, v);
            OnShapeChanged(arrangeLid: true);
        };
    }

    /// <summary>
    /// Children are clipped to the current shape. A new geometry per change: the composition clip and hit testing
    /// follow a replaced Clip, not a mutated one.
    /// </summary>
    private void UpdateClip()
    {
        if (_atRest)
        {
            _clipHost.Clip = null;
            return;
        }
        var shape = ShapeRect;
        _clipHost.Clip = new RectangleGeometry(shape, CornerRadiusValue, CornerRadiusValue);
    }

    private void ApplyFace()
    {
        var opacity = _faceOpacity?.Value ?? 1;
        var y = _faceY?.Value ?? 0;
        var full = ArcMotion.GetEffectiveLevel(this) == ArcMotionLevel.Full;
        SetFace(opacity, full ? -6 * y : 0, full ? 2 * Math.Clamp(1 - opacity, 0, 1) : 0);
    }

    private void SetFace(double opacity, double y, double blur)
    {
        _face.Opacity = Math.Clamp(opacity, 0, 1);
        _faceShift.Y = y;
        if (blur < 0.05)
        {
            _face.Effect = null;
        }
        else
        {
            _face.Effect = ArcBlur.Get(blur);
        }
    }

    private void OnShapeChanged(bool arrangeLid)
    {
        UpdateClip();
        if (arrangeLid)
        {
            _clipHost.InvalidateArrange();
        }
        InvalidateVisual();
        _border.InvalidateVisual();
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var room = ShadowRoom;
        var size = new Size(Capacity.Width + room.Left + room.Right, Capacity.Height + room.Top + room.Bottom);
        _clipHost.Measure(size);
        _border.Measure(size);
        return size;
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        _clipHost.Arrange(new Rect(finalSize));
        _border.Arrange(new Rect(finalSize));
        UpdateClip();
        return finalSize;
    }

    private void MeasureParts()
    {
        _lid.Measure(new Size(Capacity.Width, LidHeight));
        _face.Measure(new Size(FaceWidth, double.PositiveInfinity));
    }

    private void ArrangeParts()
    {
        var room = ShadowRoom;
        var shape = ShapeRect;
        // The lid spans the current width; the face keeps its own width so it measures the same mid spring.
        _lid.Arrange(new Rect(shape.X, shape.Y, Math.Max(0, shape.Width), LidHeight));
        var faceX = AlignEnd ? room.Left + Capacity.Width - FaceWidth : room.Left;
        _face.Arrange(new Rect(faceX, shape.Y + LidHeight, FaceWidth, _face.DesiredSize.Height));
    }

    /// <summary>Hosts the lid and face at the surface's rectangles (a plain panel would re-centre them).</summary>
    private sealed class ClipHost : Panel
    {
        private readonly MorphSelectSurface _owner;

        public ClipHost(MorphSelectSurface owner) => _owner = owner;

        protected override Size MeasureOverride(Size availableSize)
        {
            _owner.MeasureParts();
            return availableSize;
        }

        protected override Size ArrangeOverride(Size finalSize)
        {
            _owner.ArrangeParts();
            return finalSize;
        }
    }

    public override void Render(DrawingContext context)
    {
        if (Background is { } background)
        {
            context.FillRectangle(background, new Rect(Bounds.Size));
        }
        var rect = ShapeRect;
        if (rect.Width <= 0 || rect.Height <= 0)
        {
            return;
        }
        var radius = Math.Min(CornerRadiusValue, Math.Min(rect.Width, rect.Height) / 2);
        var shape = new RoundedRect(rect, radius);
        var lift = Math.Clamp(_lift?.Value ?? 0, 0, 1);
        if (lift < 0.999)
        {
            using (context.PushOpacity(1 - lift))
            {
                context.DrawRectangle(null, null, shape, RestShadow);
            }
        }
        if (lift > 0.001)
        {
            using (context.PushOpacity(lift))
            {
                context.DrawRectangle(null, null, shape, OpenShadow);
            }
        }
        var warm = Math.Clamp(_warm?.Value ?? 0, 0, 1);
        var fill = Mix(RestFill, OpenFill, warm);
        if (_fillBrush is null || _fillBrush.Color != fill)
        {
            _fillBrush = new ImmutableSolidColorBrush(fill);
        }
        context.DrawRectangle(_fillBrush, null, shape);
    }

    private static Color Mix(Color a, Color b, double t) => Color.FromArgb(
        (byte)Math.Round(a.A + (b.A - a.A) * t),
        (byte)Math.Round(a.R + (b.R - a.R) * t),
        (byte)Math.Round(a.G + (b.G - a.G) * t),
        (byte)Math.Round(a.B + (b.B - a.B) * t));

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (!e.Handled && !ShapeRect.Contains(e.GetPosition(this)))
        {
            e.Handled = true;
            OutsidePressed?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>The 1px border is an overlay above the content, so it never changes a measurement.</summary>
    private sealed class BorderLayer : Control
    {
        private readonly MorphSelectSurface _owner;

        public BorderLayer(MorphSelectSurface owner)
        {
            _owner = owner;
            IsHitTestVisible = false;
        }

        public override void Render(DrawingContext context)
        {
            var rect = _owner.ShapeRect;
            if (_owner.BorderPen is not { } pen || rect.Width <= 1 || rect.Height <= 1)
            {
                return;
            }
            var radius = Math.Min(_owner.CornerRadiusValue, Math.Min(rect.Width, rect.Height) / 2);
            context.DrawRectangle(null, pen, new RoundedRect(rect.Deflate(0.5), Math.Max(0, radius - 0.5)));
        }
    }
}
