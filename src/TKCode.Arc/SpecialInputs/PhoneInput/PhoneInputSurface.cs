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
/// The single morphing surface of the phone picker (P9). It lives in the field's popup, starts exactly over the closed
/// country trigger and springs its width, height and corner radius (17 → 22) to the open list and back: Arc's
/// <c>GROW</c> (0.4s, bounce 0.12) while open, <c>SHRINK</c> (0.32s, no bounce) while folding back. Open, the shape
/// steps out by (−1, −1) over the shell border, warms from Surface to SurfaceRaised (fast), gains the floating shadow
/// (considered) and a 1px Border edge (fast). The lid (trigger row / search row) stays at the top and is re-arranged as the
/// width moves; the list face hangs under it at its final width, fading in (0.2s, 40ms late) while it slides from −6px on
/// the grow spring and unblurs. Everything outside the shape is clipped. The popup content keeps a fixed size around the
/// largest shape plus room for the shadow, so the host window never resizes per frame. Reduced motion: the shape jumps
/// and the list only fades; Off: everything jumps.
/// </summary>
internal sealed class PhoneInputSurface : Control
{
    /// <summary>Arc's <c>GROW = physical(.4, .12)</c>.</summary>
    internal static readonly SpringSpec Grow = SpringSpec.FromVisual(0.4, 0.12);

    /// <summary>Arc's <c>SHRINK = physical(.32, 0)</c>.</summary>
    internal static readonly SpringSpec Shrink = SpringSpec.FromVisual(0.32, 0);

    internal const double ClosedRadius = 17;
    internal const double OpenRadius = 22;

    private readonly Control _lid;
    private readonly Control _face;
    private readonly ClipHost _clipHost;
    private readonly BorderLayer _border;
    private readonly TranslateTransform _faceShift = new();
    private MotionValue<double>? _width;
    private MotionValue<double>? _height;
    private MotionValue<double>? _radius;
    private MotionValue<double>? _warm;
    private MotionValue<double>? _lift;
    private MotionValue<double>? _edge;
    private MotionValue<double>? _faceOpacity;
    private MotionValue<double>? _faceY;
    private double _w;
    private double _h;
    private double _r = ClosedRadius;
    private bool _open;
    private bool _atRest;
    private int _morphGeneration;
    private ImmutableSolidColorBrush? _fillBrush;
    private Size _capacity;
    private double _faceWidth;

    public PhoneInputSurface(Control lid, Control face)
    {
        _lid = lid;
        _face = face;
        _face.RenderTransform = _faceShift;
        _clipHost = new ClipHost(this) { Children = { _face, _lid } };
        _border = new BorderLayer(this);
        VisualChildren.Add(_clipHost);
        VisualChildren.Add(_border);
        LogicalChildren.Add(_clipHost);
    }

    /// <summary>Room around the shape for its floating shadow.</summary>
    public Thickness ShadowRoom { get; set; } = new(56, 40, 56, 80);

    /// <summary>Height of the lid: the shell height minus its border (Arc: <c>calc(var(--pi-h) - 2px)</c>).</summary>
    public double LidHeight { get; set; } = 42;

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

    /// <summary>Width of the list face (fixed while open, so rows never reflow mid spring).</summary>
    public double FaceWidth
    {
        get => _faceWidth;
        set
        {
            _faceWidth = value;
            InvalidateParts();
        }
    }

    public Color RestFill { get; set; }
    public Color OpenFill { get; set; }
    public BoxShadows OpenShadow { get; set; }
    public IPen? EdgePen { get; set; }

    /// <summary>Pressed outside the shape (on the shadow margin).</summary>
    public event EventHandler? OutsidePressed;

    /// <summary>The current shape size.</summary>
    public Size ShapeSize => new(_w, _h);

    /// <summary>The current corner radius.</summary>
    public double ShapeRadius => _r;

    /// <summary>Whether the shape stands open (stepped out over the shell border).</summary>
    public bool IsOpenShape => _open;

    public bool IsMoving => _width?.IsAnimating == true || _height?.IsAnimating == true || _radius?.IsAnimating == true;

    /// <summary>The list face opacity (0 closed, 1 open).</summary>
    internal double FaceOpacity => _face.Opacity;

    /// <summary>The shape's rectangle in this control's coordinates.</summary>
    public Rect ShapeRect
    {
        get
        {
            var room = ShadowRoom;
            var step = _open ? 1 : 0;
            return new Rect(room.Left - step, room.Top - step, _w, _h);
        }
    }

    private void InvalidateParts()
    {
        _clipHost.InvalidateMeasure();
        InvalidateMeasure();
    }

    /// <summary>Places the shape without motion.</summary>
    public void Jump(Size shape, bool open)
    {
        Ensure();
        _open = open;
        _width!.Jump(shape.Width);
        _height!.Jump(shape.Height);
        _radius!.Jump(open ? OpenRadius : ClosedRadius);
        _warm!.Jump(open ? 1 : 0);
        _lift!.Jump(open ? 1 : 0);
        _edge!.Jump(open ? 1 : 0);
        _faceOpacity!.Jump(open ? 1 : 0);
        _faceY!.Jump(open ? 0 : 1);
        ApplyFace();
        _morphGeneration++;
        _atRest = open;
        UpdateClip();
        InvalidateVisual();
        _border.InvalidateVisual();
    }

    /// <summary>
    /// Springs to <paramref name="target"/>; <paramref name="open"/> picks GROW (and the open material) or SHRINK.
    /// Returns when the height settles (the visible end of the morph).
    /// </summary>
    public Task<bool> MorphTo(Size target, bool open)
    {
        Ensure();
        var level = ArcMotion.GetEffectiveLevel(this);
        var wasOpen = _open;
        _open = open;
        var spec = MotionSpec.Spring(open ? Grow : Shrink);
        var generation = ++_morphGeneration;
        _atRest = false;
        UpdateClip();
        var widthDone = _width!.AnimateTo(target.Width, spec);
        var radiusDone = _radius!.AnimateTo(open ? OpenRadius : ClosedRadius, spec);
        var done = _height!.AnimateTo(target.Height, spec);
        if (open)
        {
            // Settled and open: the face sits inside the shape, so rendering and hit testing skip the geometry clip.
            Task.WhenAll(widthDone, done, radiusDone).ContinueWith(t => Dispatcher.UIThread.Post(() =>
            {
                if (generation == _morphGeneration && t.Result.All(r => r))
                {
                    _atRest = true;
                    UpdateClip();
                }
            }), TaskScheduler.Default);
        }
        if (wasOpen != open)
        {
            // Material: Arc's reduced CSS drops the shape's transitions (transition-duration: 0ms).
            if (level == ArcMotionLevel.Full)
            {
                _ = _warm!.AnimateTo(open ? 1 : 0, MotionSpec.Tween(ArcMotionPresets.Fast, ArcEasings.Standard));
                _ = _lift!.AnimateTo(open ? 1 : 0, MotionSpec.Tween(ArcMotionPresets.Considered, ArcEasings.Standard));
                _ = _edge!.AnimateTo(open ? 1 : 0, MotionSpec.Tween(ArcMotionPresets.Fast, ArcEasings.Standard));
            }
            else
            {
                _warm!.Jump(open ? 1 : 0);
                _lift!.Jump(open ? 1 : 0);
                _edge!.Jump(open ? 1 : 0);
            }
            var full = level == ArcMotionLevel.Full;
            if (open)
            {
                var delay = full ? TimeSpan.FromMilliseconds(40) : TimeSpan.Zero;
                _ = _faceOpacity!.AnimateTo(1, MotionSpec.Tween(TimeSpan.FromMilliseconds(200), ArcEasings.Enter, delay));
                if (full)
                {
                    _ = _faceY!.AnimateTo(0, MotionSpec.Spring(Grow));
                }
                else
                {
                    _faceY!.Jump(0);
                }
            }
            else
            {
                _ = _faceOpacity!.AnimateTo(0, MotionSpec.Tween(TimeSpan.FromMilliseconds(100), ArcEasings.Standard));
                if (full)
                {
                    _ = _faceY!.AnimateTo(1, MotionSpec.Tween(TimeSpan.FromMilliseconds(100), ArcEasings.Standard));
                }
                else
                {
                    _faceY!.Jump(1);
                }
            }
        }
        OnShapeChanged(arrangeLid: true);
        return done;
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
        _radius = new MotionValue<double>(this, ClosedRadius, MotionVectors.Double);
        _radius.Changed += v =>
        {
            _r = Math.Max(0, v);
            OnShapeChanged(arrangeLid: false);
        };
        _warm = new MotionValue<double>(this, 0, MotionVectors.Double);
        _warm.Changed += _ => InvalidateVisual();
        _lift = new MotionValue<double>(this, 0, MotionVectors.Double);
        _lift.Changed += _ => InvalidateVisual();
        _edge = new MotionValue<double>(this, 0, MotionVectors.Double);
        _edge.Changed += _ => _border.InvalidateVisual();
        _faceOpacity = new MotionValue<double>(this, 0, MotionVectors.Double);
        _faceOpacity.Changed += _ => ApplyFace();
        _faceY = new MotionValue<double>(this, 1, MotionVectors.Double);
        _faceY.Changed += _ => ApplyFace();
    }

    private double Radius(Rect rect) => Math.Min(_r, Math.Min(rect.Width, rect.Height) / 2);

    /// <summary>
    /// Children are clipped to the current shape. A new geometry per change: the composition clip and hit testing follow
    /// a replaced Clip, not a mutated one.
    /// </summary>
    private void UpdateClip()
    {
        if (_atRest)
        {
            _clipHost.Clip = null;
            return;
        }
        var shape = ShapeRect;
        var radius = Radius(shape);
        _clipHost.Clip = new RectangleGeometry(shape, radius, radius);
    }

    private void ApplyFace()
    {
        var opacity = Math.Clamp(_faceOpacity?.Value ?? 1, 0, 1);
        var y = _faceY?.Value ?? 0;
        var full = ArcMotion.GetEffectiveLevel(this) == ArcMotionLevel.Full;
        _face.Opacity = opacity;
        _faceShift.Y = full ? -6 * y : 0;
        var blur = full ? 2 * (1 - opacity) : 0;
        _face.IsHitTestVisible = _open;
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
        _lid.Measure(new Size(Math.Max(Capacity.Width, _w), LidHeight));
        _face.Measure(new Size(FaceWidth, double.PositiveInfinity));
    }

    private void ArrangeParts()
    {
        var shape = ShapeRect;
        // The lid spans the current width; the face keeps its own width, so it measures the same mid spring.
        _lid.Arrange(new Rect(shape.X, shape.Y, Math.Max(0, shape.Width), LidHeight));
        _face.Arrange(new Rect(shape.X, shape.Y + LidHeight, FaceWidth, _face.DesiredSize.Height));
    }

    /// <summary>Hosts the lid and face at the surface's rectangles (a plain panel would re-centre them).</summary>
    private sealed class ClipHost : Panel
    {
        private readonly PhoneInputSurface _owner;

        public ClipHost(PhoneInputSurface owner) => _owner = owner;

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
        // The transparent margin still belongs to the popup: a press there is a press outside the shape.
        context.FillRectangle(Brushes.Transparent, new Rect(Bounds.Size));
        var rect = ShapeRect;
        if (rect.Width <= 0 || rect.Height <= 0)
        {
            return;
        }
        var shape = new RoundedRect(rect, Radius(rect));
        var lift = Math.Clamp(_lift?.Value ?? 0, 0, 1);
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

    /// <summary>The 1px edge is an overlay above the content (Arc's <c>.shape::after</c>), so it never changes a measurement.</summary>
    private sealed class BorderLayer : Control
    {
        private readonly PhoneInputSurface _owner;

        public BorderLayer(PhoneInputSurface owner)
        {
            _owner = owner;
            IsHitTestVisible = false;
        }

        public override void Render(DrawingContext context)
        {
            var rect = _owner.ShapeRect;
            var edge = Math.Clamp(_owner._edge?.Value ?? 0, 0, 1);
            if (_owner.EdgePen is not { } pen || edge <= 0.001 || rect.Width <= 1 || rect.Height <= 1)
            {
                return;
            }
            var radius = _owner.Radius(rect);
            using (context.PushOpacity(edge))
            {
                context.DrawRectangle(null, pen, new RoundedRect(rect.Deflate(0.5), Math.Max(0, radius - 0.5)));
            }
        }
    }
}
