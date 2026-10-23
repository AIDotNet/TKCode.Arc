using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Threading;
using TKCode.Arc.Motion;

namespace TKCode.Arc;

/// <summary>
/// The animated render state of one item in a Projects panel (a board card, a calendar event): an offset from its layout
/// position, a scale and a rotation about its centre, and an opacity. Every value is a <see cref="MotionValue{T}"/>, so a
/// flight that is retargeted keeps going from where it is drawn. The item's <see cref="Visual.RenderTransform"/> and
/// <see cref="Visual.Opacity"/> belong to this object while it is attached; do not set them elsewhere.
/// </summary>
internal sealed class ProjectsItemMotion : IDisposable
{
    private readonly MatrixTransform _transform = new();
    private bool _disposed;

    public ProjectsItemMotion(Control target)
    {
        Target = target;
        Offset = new MotionValue<Vector>(target, default, MotionVectors.Vector);
        Scale = new MotionValue<double>(target, 1, MotionVectors.Double);
        Rotation = new MotionValue<double>(target, 0, MotionVectors.Double);
        Opacity = new MotionValue<double>(target, 1, MotionVectors.Double);
        Offset.Changed += _ => Apply();
        Scale.Changed += _ => Apply();
        Rotation.Changed += _ => Apply();
        Opacity.Changed += v => target.Opacity = v;
        target.RenderTransformOrigin = RelativePoint.Center;
        target.RenderTransform = _transform;
    }

    /// <summary>The item.</summary>
    public Control Target { get; }

    /// <summary>Offset from the layout position, in the panel's coordinates.</summary>
    public MotionValue<Vector> Offset { get; }

    /// <summary>Scale about the item's centre (1 = rest).</summary>
    public MotionValue<double> Scale { get; }

    /// <summary>Rotation about the item's centre, in degrees (a lifted card's tilt).</summary>
    public MotionValue<double> Rotation { get; }

    /// <summary>The item's opacity.</summary>
    public MotionValue<double> Opacity { get; }

    /// <summary>Where the owner last arranged the item (panel coordinates), or null before the first arrange.</summary>
    public Rect? Layout { get; set; }

    /// <summary>Where the item is drawn right now: its layout position plus <see cref="Offset"/>.</summary>
    public Point VisualTopLeft => (Layout?.TopLeft ?? default) + Offset.Value;

    /// <summary>True while any value is moving.</summary>
    public bool IsMoving => Offset.IsAnimating || Scale.IsAnimating || Rotation.IsAnimating || Opacity.IsAnimating;

    /// <summary>Owner-defined flag: the item is leaving (kept for its exit, not laid out with the others).</summary>
    public bool IsLeaving { get; set; }

    /// <summary>Jumps every value to rest (no offset, scale 1, no rotation, opaque).</summary>
    public void Rest()
    {
        Offset.Jump(default);
        Scale.Jump(1);
        Rotation.Jump(0);
        Opacity.Jump(1);
    }

    private void Apply()
    {
        if (_disposed)
        {
            return;
        }
        var offset = Offset.Value;
        var matrix = Matrix.CreateScale(Scale.Value, Scale.Value)
                     * Matrix.CreateRotation(Rotation.Value * Math.PI / 180)
                     * Matrix.CreateTranslation(offset.X, offset.Y);
        _transform.Matrix = matrix;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }
        _disposed = true;
        Offset.Dispose();
        Scale.Dispose();
        Rotation.Dispose();
        Opacity.Dispose();
        if (ReferenceEquals(Target.RenderTransform, _transform))
        {
            Target.RenderTransform = null;
        }
        Target.Opacity = 1;
    }
}

/// <summary>
/// FLIP for a custom panel: the panel reports every child's arranged rect through <see cref="Place"/>; after
/// <see cref="Arm"/>, a child whose layout position changed jumps back to where it was drawn and springs to its new place
/// (render transform only, no relayout). Unarmed arranges (a resize) move children directly, keeping any flight that is
/// already running relative to the new layout. Reduced and Off motion jump (the springs in <see cref="MotionValue{T}"/>
/// honour the motion level).
/// </summary>
internal sealed class ProjectsFlip
{
    private readonly Control _owner;
    private readonly Dictionary<Control, ProjectsItemMotion> _items = new();
    private bool _armed;
    private bool _disarmPosted;
    private MotionSpec _spec = MotionSpec.Spring(ProjectsSprings.Card);
    private bool _keepVelocity;

    public ProjectsFlip(Control owner)
    {
        _owner = owner;
    }

    /// <summary>True between <see cref="Arm"/> and the end of the next layout pass.</summary>
    public bool IsArmed => _armed;

    /// <summary>The tracked items.</summary>
    public IEnumerable<ProjectsItemMotion> Items => _items.Values;

    /// <summary>The motion state of <paramref name="child"/>, created on first use.</summary>
    public ProjectsItemMotion Get(Control child)
    {
        if (!_items.TryGetValue(child, out var motion))
        {
            motion = new ProjectsItemMotion(child);
            _items[child] = motion;
        }
        return motion;
    }

    /// <summary>The motion state of <paramref name="child"/> if it is tracked.</summary>
    public bool TryGet(Control child, out ProjectsItemMotion motion) => _items.TryGetValue(child, out motion!);

    /// <summary>
    /// Animates the layout changes of the next arrange pass with <paramref name="spec"/> (default: the card spring).
    /// <paramref name="keepVelocity"/> carries a running flight's speed into the new one; false restarts from rest
    /// (Arc's layout animations).
    /// </summary>
    public void Arm(MotionSpec? spec = null, bool keepVelocity = false)
    {
        _armed = true;
        _spec = spec ?? MotionSpec.Spring(ProjectsSprings.Card);
        _keepVelocity = keepVelocity;
        _owner.InvalidateArrange();
        if (!_disarmPosted)
        {
            _disarmPosted = true;
            // Layout runs before background jobs; if nothing moved, the arm must not leak into a later resize.
            Dispatcher.UIThread.Post(() =>
            {
                _disarmPosted = false;
                _armed = false;
            }, DispatcherPriority.Background);
        }
    }

    /// <summary>
    /// Records that <paramref name="child"/> is arranged at <paramref name="rect"/>; when armed and the position changed,
    /// starts its flight from where it is drawn now. Call it from the panel's ArrangeOverride for every laid out child.
    /// </summary>
    /// <returns>The child's motion state.</returns>
    public ProjectsItemMotion Place(Control child, Rect rect)
    {
        var motion = Get(child);
        if (motion.Layout is { } old && _armed && old.TopLeft != rect.TopLeft)
        {
            var velocity = _keepVelocity ? motion.Offset.Velocity : default;
            var start = motion.Offset.Value + (old.TopLeft - rect.TopLeft);
            motion.Offset.Jump(start);
            _ = motion.Offset.AnimateTo(default, _spec, velocity);
        }
        motion.Layout = rect;
        return motion;
    }

    /// <summary>Stops tracking <paramref name="child"/> and gives its transform and opacity back.</summary>
    public void Forget(Control child)
    {
        if (_items.Remove(child, out var motion))
        {
            motion.Dispose();
        }
    }

    /// <summary>Stops tracking every child.</summary>
    public void Clear()
    {
        foreach (var motion in _items.Values)
        {
            motion.Dispose();
        }
        _items.Clear();
    }

    /// <summary>Jumps every running flight to its end (used when motion is switched off mid-flight).</summary>
    public void Settle()
    {
        foreach (var motion in _items.Values)
        {
            if (!motion.IsLeaving)
            {
                motion.Rest();
            }
        }
    }
}
