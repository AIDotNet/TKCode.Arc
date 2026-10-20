using TKCode.Arc.Motion;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Avalonia.VisualTree;

namespace TKCode.Arc;

/// <summary>A morph shape: top-left corner relative to the trigger's, size and corner radius.</summary>
internal readonly record struct PickerMorphShape(double X, double Y, double Width, double Height, double Radius)
{
    public static PickerMorphShape Lerp(PickerMorphShape a, PickerMorphShape b, double t) => new(
        a.X + (b.X - a.X) * t, a.Y + (b.Y - a.Y) * t, a.Width + (b.Width - a.Width) * t,
        a.Height + (b.Height - a.Height) * t, a.Radius + (b.Radius - a.Radius) * t);
}

/// <summary>
/// The surface a picker trigger grows into (P9 / P29), hosted in the picker's popup. It starts exactly over the
/// closed trigger and springs one progress value from that shape to the panel's shape (top-left corner, width, height
/// and corner radius together), clipping the panel to it; the shadow and the 1px border follow the shape, so a clipped
/// reveal and a growing surface look the same. The popup content keeps a fixed size around the largest shape (with
/// room for the shadow), so the host window never resizes while the shape moves. An overlay layer above the clip
/// carries copies that must not be clipped (DateRangePicker's flying value).
/// </summary>
internal sealed class PickerMorphSurface : Control
{
    private readonly Control _content;
    private readonly ClipHost _clipHost;
    private readonly BorderLayer _border;
    private readonly Canvas _overlay = new() { IsHitTestVisible = false };
    private readonly TranslateTransform _contentShift = new();
    private MotionValue<double>? _progress;
    private MotionValue<double>? _lift;
    private PickerMorphShape _from;
    private PickerMorphShape _to;
    private PickerMorphShape _shape;
    private ImmutableSolidColorBrush? _fillBrush;
    private int _generation;
    private bool _clipOff;

    public PickerMorphSurface(Control content)
    {
        _content = content;
        // The panel rides on the shape's top-left corner by a render transform (no relayout while it moves).
        _content.RenderTransform = _contentShift;
        _clipHost = new ClipHost(this) { Children = { content } };
        _border = new BorderLayer(this);
        VisualChildren.Add(_clipHost);
        VisualChildren.Add(_border);
        VisualChildren.Add(_overlay);
        LogicalChildren.Add(_clipHost);
        LogicalChildren.Add(_overlay);
        ClipToBounds = false;
    }

    /// <summary>Room around the largest shape for its shadow.</summary>
    public Thickness ShadowRoom { get; set; } = new(48, 32, 48, 72);

    /// <summary>Extra space left of the trigger for shapes that move left (a panel kept inside the window's right edge).</summary>
    public double Lead { get; private set; }

    /// <summary>Extra space above the trigger for shapes that move up (a panel kept inside the window's bottom edge).</summary>
    public double Rise { get; private set; }

    /// <summary>Size of the region every shape fits in; its top-left corner is <see cref="Lead"/> and <see cref="Rise"/> before the trigger's.</summary>
    public Size Capacity { get; private set; }

    /// <summary>Where the trigger's top-left corner sits in this control.</summary>
    public Point Origin => new(ShadowRoom.Left + Lead, ShadowRoom.Top + Rise);

    /// <summary>Sizes the region (<see cref="Lead"/>, <see cref="Rise"/>, <see cref="Capacity"/>) to hold both shapes.</summary>
    public void Contain(PickerMorphShape a, PickerMorphShape b)
    {
        var left = Math.Min(0, Math.Min(a.X, b.X));
        var top = Math.Min(0, Math.Min(a.Y, b.Y));
        var right = Math.Max(a.X + a.Width, b.X + b.Width);
        var bottom = Math.Max(a.Y + a.Height, b.Y + b.Height);
        Lead = -left;
        Rise = -top;
        Capacity = new Size(Math.Max(0, right - left), Math.Max(0, bottom - top));
        InvalidateMeasure();
    }

    public Color Fill { get; set; }
    public BoxShadows RestShadow { get; set; }
    public BoxShadows OpenShadow { get; set; }
    public IPen? BorderPen { get; set; }

    /// <summary>Unclipped layer above the surface, in this control's coordinates.</summary>
    public Canvas Overlay => _overlay;

    /// <summary>The shape right now.</summary>
    public PickerMorphShape Shape => _shape;

    /// <summary>Progress of the running morph from its start shape to its target (0..1, may overshoot).</summary>
    public double Progress => _progress?.Value ?? 1;

    /// <summary>The shape's rectangle in this control's coordinates.</summary>
    public Rect ShapeRect => new(Origin.X + _shape.X, Origin.Y + _shape.Y, Math.Max(0, _shape.Width), Math.Max(0, _shape.Height));

    public bool IsMoving => _progress?.IsAnimating == true;

    /// <summary>Raised on every shape step (owners map progress to content fades).</summary>
    public event EventHandler? ShapeChanged;

    /// <summary>A press landed on the shadow room, outside the shape.</summary>
    public event EventHandler? OutsidePressed;

    /// <summary>Places the shape without motion.</summary>
    public void Jump(PickerMorphShape shape, double lift)
    {
        Ensure();
        _generation++;
        _from = _to = shape;
        _progress!.Jump(1);
        _lift!.Jump(lift);
        _clipOff = false;
        Apply(shape);
    }

    /// <summary>Springs from the current shape to <paramref name="target"/>; resolves when it settles (false if interrupted).</summary>
    public async Task<bool> MorphTo(PickerMorphShape target, SpringSpec spring, double lift, MotionSpec liftSpec)
    {
        Ensure();
        var generation = ++_generation;
        _from = _shape;
        _to = target;
        _clipOff = false;
        _progress!.Jump(0);
        _ = _lift!.AnimateTo(lift, liftSpec);
        var reached = await _progress.AnimateTo(1, MotionSpec.Spring(spring));
        if (reached && generation == _generation)
        {
            // At rest the shape equals the panel: drop the clip so hit testing and rendering skip the geometry.
            _clipOff = true;
            UpdateClip();
        }
        return reached && generation == _generation;
    }

    /// <summary>Moves the target of a running or settled morph (window resize, new panel size) without restarting at zero velocity.</summary>
    public void Retarget(PickerMorphShape target, SpringSpec spring)
    {
        if (_progress is null)
        {
            return;
        }
        if (!IsMoving)
        {
            _ = MorphTo(target, spring, _lift?.Target ?? 1, MotionSpec.Instant);
            return;
        }
        _to = target;
        Apply(PickerMorphShape.Lerp(_from, _to, _progress.Value));
    }

    private void Ensure()
    {
        if (_progress is not null)
        {
            return;
        }
        _progress = new MotionValue<double>(this, 1, MotionVectors.Double);
        _progress.Changed += p => Apply(PickerMorphShape.Lerp(_from, _to, p));
        _lift = new MotionValue<double>(this, 0, MotionVectors.Double);
        _lift.Changed += _ => InvalidateVisual();
    }

    private void Apply(PickerMorphShape shape)
    {
        _shape = shape;
        _contentShift.X = shape.X;
        _contentShift.Y = shape.Y;
        UpdateClip();
        InvalidateVisual();
        _border.InvalidateVisual();
        ShapeChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>The children are clipped to the shape (a replaced geometry: composition follows a new clip, not a mutated one).</summary>
    private void UpdateClip()
    {
        // At rest over the whole panel no clip is needed (and hit testing then skips the geometry).
        var content = _content.Bounds.Size;
        var covers = content.Width > 0 && _shape.Width >= content.Width - 0.5 && _shape.Height >= content.Height - 0.5;
        if (_clipOff || covers)
        {
            _clipHost.Clip = null;
            return;
        }
        var rect = ShapeRect;
        var radius = Math.Min(Math.Max(0, _shape.Radius), Math.Min(rect.Width, rect.Height) / 2);
        _clipHost.Clip = new RectangleGeometry(rect, radius, radius);
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var room = ShadowRoom;
        var size = new Size(Capacity.Width + room.Left + room.Right, Capacity.Height + room.Top + room.Bottom);
        _clipHost.Measure(size);
        _border.Measure(size);
        _overlay.Measure(size);
        return size;
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        _clipHost.Arrange(new Rect(finalSize));
        _border.Arrange(new Rect(finalSize));
        _overlay.Arrange(new Rect(finalSize));
        UpdateClip();
        return finalSize;
    }

    public override void Render(DrawingContext context)
    {
        context.FillRectangle(Brushes.Transparent, new Rect(Bounds.Size));
        var rect = ShapeRect;
        if (rect.Width <= 0 || rect.Height <= 0)
        {
            return;
        }
        var radius = Math.Min(Math.Max(0, _shape.Radius), Math.Min(rect.Width, rect.Height) / 2);
        var shape = new RoundedRect(rect, radius);
        var lift = Math.Clamp(_lift?.Value ?? 0, 0, 1);
        if (lift < 0.999 && RestShadow.Count > 0)
        {
            using (context.PushOpacity(1 - lift))
            {
                context.DrawRectangle(null, null, shape, RestShadow);
            }
        }
        if (lift > 0.001 && OpenShadow.Count > 0)
        {
            using (context.PushOpacity(lift))
            {
                context.DrawRectangle(null, null, shape, OpenShadow);
            }
        }
        context.DrawRectangle(PickerDrawing.Brush(ref _fillBrush, Fill), null, shape);
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (!e.Handled && !ShapeRect.Contains(e.GetPosition(this)))
        {
            e.Handled = true;
            OutsidePressed?.Invoke(this, EventArgs.Empty);
        }
    }

    private Size _lastContentSize;

    /// <summary>The panel measured to a new size while shown (late template or font resolution, wrapped text).</summary>
    public event EventHandler? ContentResized;

    private void MeasureContent()
    {
        _content.Measure(Size.Infinity);
        var size = _content.DesiredSize;
        if (Math.Abs(size.Width - _lastContentSize.Width) > 0.5 || Math.Abs(size.Height - _lastContentSize.Height) > 0.5)
        {
            var first = _lastContentSize == default;
            _lastContentSize = size;
            if (!first)
            {
                ContentResized?.Invoke(this, EventArgs.Empty);
            }
        }
    }

    /// <summary>Records the panel size the owner planned the shapes with.</summary>
    public void ExpectContentSize(Size size) => _lastContentSize = size;

    private void ArrangeContent()
    {
        var origin = Origin;
        _content.Arrange(new Rect(origin, _content.DesiredSize));
    }

    /// <summary>Hosts the panel at the trigger's top-left corner (a plain panel would stretch it).</summary>
    private sealed class ClipHost : Panel
    {
        private readonly PickerMorphSurface _owner;

        public ClipHost(PickerMorphSurface owner) => _owner = owner;

        protected override Size MeasureOverride(Size availableSize)
        {
            _owner.MeasureContent();
            return availableSize;
        }

        protected override Size ArrangeOverride(Size finalSize)
        {
            _owner.ArrangeContent();
            return finalSize;
        }
    }

    /// <summary>The 1px border is an overlay above the content, so it never changes a measurement.</summary>
    private sealed class BorderLayer : Control
    {
        private readonly PickerMorphSurface _owner;

        public BorderLayer(PickerMorphSurface owner)
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
            var radius = Math.Min(Math.Max(0, _owner._shape.Radius), Math.Min(rect.Width, rect.Height) / 2);
            context.DrawRectangle(null, pen, new RoundedRect(rect.Deflate(0.5), Math.Max(0, radius - 0.5)));
        }
    }
}

/// <summary>How a panel that opens at its trigger fits the window vertically.</summary>
/// <param name="Rise">The panel's top relative to the trigger's: 0, or negative when it rises to stay inside the window.</param>
/// <param name="Scroll">How far <paramref name="Viewer"/> scrolls down before the panel needs to rise.</param>
/// <param name="Viewer">The scroll viewer around the trigger that can make room, if any.</param>
internal readonly record struct PickerMorphFit(double Rise, double Scroll, ScrollViewer? Viewer);

/// <summary>Placement helpers for popups that host a <see cref="PickerMorphSurface"/>.</summary>
internal static class PickerMorphPlacement
{
    /// <summary>Shadow room wanted around the shape (the floating shadow's reach on each side).</summary>
    public static readonly Thickness DefaultRoom = new(56, 40, 56, 80);

    /// <summary>Distance a panel keeps from the window's edges (Arc's EDGE).</summary>
    public const double Edge = 8;

    /// <summary>
    /// The shadow room trimmed where the window ends. <paramref name="scroll"/> is the page scroll still to come, so
    /// the room fits where the trigger will rest.
    /// </summary>
    public static Thickness Room(Visual owner, Control? anchor, PickerMorphSurface surface, double scroll = 0)
    {
        var wanted = DefaultRoom;
        var top = TopLevel.GetTopLevel(owner);
        if (top is null || anchor is null || anchor.TranslatePoint(default, top) is not { } origin)
        {
            return wanted;
        }
        var client = top.ClientSize;
        var left = origin.X - surface.Lead;
        var upper = origin.Y - scroll - surface.Rise;
        var capacity = surface.Capacity;
        static double Fit(double want, double space) => Math.Clamp(Math.Min(want, Math.Floor(space)), 0, want);
        return new Thickness(
            Fit(wanted.Left, left),
            Fit(wanted.Top, upper),
            Fit(wanted.Right, client.Width - (left + capacity.Width)),
            Fit(wanted.Bottom, client.Height - (upper + capacity.Height)));
    }

    /// <summary>
    /// The panel's left edge relative to the trigger's: 0, or shifted left just enough to stay <paramref name="edge"/>
    /// inside the window's right edge (never past its left edge), as Arc keeps the date range panel inside its boundary.
    /// </summary>
    public static double Shift(Visual owner, Control? anchor, double width, double edge)
    {
        var top = TopLevel.GetTopLevel(owner);
        if (top is null || anchor is null || anchor.TranslatePoint(default, top) is not { } origin)
        {
            return 0;
        }
        var shift = Math.Min(0, top.ClientSize.Width - edge - (origin.X + width));
        return Math.Max(shift, Math.Min(0, edge - origin.X));
    }

    /// <summary>
    /// Fits a panel of <paramref name="height"/> that opens at the trigger's top edge and grows down (Arc never flips
    /// it): when it would cross the window's bottom edge, the nearest ancestor ScrollViewer that can scroll down
    /// brings its bottom into view, never past the trigger's top; what scrolling cannot reveal, the panel takes by
    /// rising over the trigger, never past the window's top edge. The surface still starts and ends on the trigger.
    /// </summary>
    public static PickerMorphFit Fit(Visual owner, Control? anchor, double height, double edge)
    {
        var top = TopLevel.GetTopLevel(owner);
        if (top is null || anchor is null || anchor.TranslatePoint(default, top) is not { } origin)
        {
            return default;
        }
        var bottom = top.ClientSize.Height - edge;
        var overflow = origin.Y + height - bottom;
        if (overflow <= 0)
        {
            return default;
        }
        var scroll = 0.0;
        ScrollViewer? viewer = null;
        foreach (var candidate in anchor.GetVisualAncestors().OfType<ScrollViewer>())
        {
            var room = candidate.Extent.Height - candidate.Viewport.Height - candidate.Offset.Y;
            if (room < 0.5 || candidate.TranslatePoint(default, top) is not { } view)
            {
                continue;
            }
            scroll = Math.Max(0, Math.Min(Math.Min(overflow, room), origin.Y - Math.Max(0, view.Y) - edge));
            viewer = scroll >= 0.5 ? candidate : null;
            break;
        }
        if (viewer is null)
        {
            scroll = 0;
        }
        var rest = origin.Y - scroll + height - bottom;
        var rise = rest > 0 ? -Math.Min(rest, Math.Max(0, origin.Y - scroll - edge)) : 0;
        return new PickerMorphFit(rise, scroll, viewer);
    }
}

/// <summary>
/// Scrolls a page by the distance <see cref="PickerMorphPlacement.Fit"/> asked for (Arc's smooth scrollBy). It is
/// code-driven because the target is a ScrollViewer's offset, which a ControlTheme transition cannot reach; the popup
/// follows its trigger as the page moves, so the surface keeps growing from the trigger.
/// </summary>
internal sealed class PickerRevealScroll
{
    private readonly Visual _owner;
    private MotionValue<double>? _offset;
    private ScrollViewer? _viewer;

    public PickerRevealScroll(Visual owner) => _owner = owner;

    public void Start(PickerMorphFit fit, bool animate)
    {
        if (fit.Viewer is not { } viewer || fit.Scroll <= 0)
        {
            return;
        }
        var from = viewer.Offset.Y;
        var to = from + fit.Scroll;
        if (!animate)
        {
            Stop();
            viewer.Offset = viewer.Offset.WithY(to);
            return;
        }
        if (!ReferenceEquals(viewer, _viewer) || _offset is null)
        {
            _offset?.Dispose();
            _viewer = viewer;
            _offset = new MotionValue<double>(_owner, from, MotionVectors.Double);
            _offset.Changed += y =>
            {
                if (_viewer is { } target)
                {
                    target.Offset = target.Offset.WithY(y);
                }
            };
        }
        _offset.Jump(from);
        _ = _offset.AnimateTo(to, MotionSpec.Tween(ArcMotionPresets.Considered, ArcEasings.InOut));
    }

    public void Stop() => _offset?.Stop();
}
