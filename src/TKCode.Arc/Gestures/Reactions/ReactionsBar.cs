using System.Collections.Specialized;
using System.Diagnostics;
using TKCode.Arc.Motion;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>
/// The reaction bar's wrapping row (Arc's flex-wrap bar). Pills and the add button flow in order; a leaving pill is
/// popped out of the flow (AnimatePresence popLayout) and stays where it was while it exits. After a structural change
/// (<see cref="ArmLayoutMotion"/>) every item that moved glides from where it was on the morph spring (P19, FLIP via a
/// render transform that keeps velocity), and for 700ms the bar's own height follows a new row on the smooth spring
/// (P6); any other reflow (a count growing a digit, a resized window) jumps, so nothing wobbles on load.
/// </summary>
internal sealed class ReactionsBar : Panel
{
    public static readonly StyledProperty<double> ColumnGapProperty =
        AvaloniaProperty.Register<ReactionsBar, double>(nameof(ColumnGap), 6);

    public static readonly StyledProperty<double> RowGapProperty =
        AvaloniaProperty.Register<ReactionsBar, double>(nameof(RowGap), 8);

    public double ColumnGap { get => GetValue(ColumnGapProperty); set => SetValue(ColumnGapProperty, value); }
    public double RowGap { get => GetValue(RowGapProperty); set => SetValue(RowGapProperty, value); }

    private static readonly RelativePoint s_topLeft = new(0, 0, RelativeUnit.Absolute);
    private static readonly long s_armTicks = (long)(0.7 * Stopwatch.Frequency);

    private sealed class Slot
    {
        public Point Natural;
        public bool Placed;
        public MotionValue<Vector>? Offset;
        public readonly MatrixTransform Transform = new();
    }

    private readonly Dictionary<Control, Slot> _slots = new();
    private MotionValue<double>? _height;
    private double _naturalHeight = double.NaN;
    private long _armedUntil;
    private bool _flipArmed;

    static ReactionsBar()
    {
        AffectsMeasure<ReactionsBar>(ColumnGapProperty, RowGapProperty);
    }

    /// <summary>The next layout pass glides moved items, and the height follows for the next 700ms.</summary>
    public void ArmLayoutMotion()
    {
        _flipArmed = true;
        _armedUntil = Stopwatch.GetTimestamp() + s_armTicks;
        InvalidateMeasure();
    }

    /// <summary>Visual offset of an item from its layout position (its FLIP glide).</summary>
    public Vector OffsetOf(Control child) => _slots.TryGetValue(child, out var slot) ? slot.Offset?.Value ?? default : default;

    private bool CanAnimate => IsLoaded && ArcMotion.IsFull(this);

    private static bool InFlow(Control child) => child.IsVisible && child is not ReactionsPill { IsLeaving: true };

    protected override void ChildrenChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        base.ChildrenChanged(sender, e);
        if (e.OldItems is null || e.Action == NotifyCollectionChangedAction.Move)
        {
            return;
        }
        foreach (var item in e.OldItems)
        {
            if (item is Control child && !Children.Contains(child) && _slots.Remove(child, out var slot))
            {
                slot.Offset?.Dispose();
                child.ClearValue(RenderTransformProperty);
                child.ClearValue(RenderTransformOriginProperty);
            }
        }
    }

    private Slot SlotOf(Control child)
    {
        if (!_slots.TryGetValue(child, out var slot))
        {
            slot = new Slot();
            _slots[child] = slot;
        }
        return slot;
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        foreach (var child in Children)
        {
            child.Measure(Size.Infinity);
        }
        var size = Flow(availableSize.Width, arrange: false);
        if (double.IsNaN(_naturalHeight) || !_naturalHeight.Equals(size.Height))
        {
            var first = double.IsNaN(_naturalHeight);
            _naturalHeight = size.Height;
            _height ??= CreateHeight(size.Height);
            // Wrapping onto a new row follows on a spring after a change; anything else (a late font, a reflow) jumps.
            if (first || !CanAnimate || Stopwatch.GetTimestamp() > _armedUntil)
            {
                _height.Jump(size.Height);
            }
            else
            {
                _ = _height.AnimateTo(size.Height, MotionSpec.Spring(ArcSpring.Smooth));
            }
        }
        return new Size(size.Width, Math.Max(0, _height?.Value ?? size.Height));
    }

    private MotionValue<double> CreateHeight(double initial)
    {
        var value = new MotionValue<double>(this, initial, MotionVectors.Double);
        value.Changed += _ => InvalidateMeasure();
        return value;
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        Flow(finalSize.Width, arrange: true);
        _flipArmed = false;
        return finalSize;
    }

    /// <summary>Lays the in-flow items out in wrapping rows (vertically centered per row); arranges when asked.</summary>
    private Size Flow(double width, bool arrange)
    {
        var limit = double.IsFinite(width) ? width : double.PositiveInfinity;
        var columnGap = ColumnGap;
        var rowGap = RowGap;
        double x = 0, y = 0, rowHeight = 0, widest = 0;
        var rowStart = 0;
        var children = Children;
        for (var i = 0; i <= children.Count; i++)
        {
            var child = i < children.Count ? children[i] : null;
            if (child is not null && !InFlow(child))
            {
                continue;
            }
            var desired = child?.DesiredSize ?? default;
            var wraps = child is not null && x > 0 && x + desired.Width > limit + 0.01;
            if (child is null || wraps)
            {
                // Close the row: arrange its items centered in the row height.
                if (arrange)
                {
                    ArrangeRow(rowStart, i, y, rowHeight, columnGap);
                }
                widest = Math.Max(widest, x > 0 ? x - columnGap : 0);
                if (child is null)
                {
                    break;
                }
                y += rowHeight + rowGap;
                x = 0;
                rowHeight = 0;
                rowStart = i;
            }
            x += desired.Width + columnGap;
            rowHeight = Math.Max(rowHeight, desired.Height);
        }
        if (arrange)
        {
            ArrangeLeaving();
        }
        return new Size(widest, y + rowHeight);
    }

    private void ArrangeRow(int start, int end, double y, double rowHeight, double columnGap)
    {
        var x = 0.0;
        var animate = _flipArmed && CanAnimate;
        for (var i = start; i < end; i++)
        {
            var child = Children[i];
            if (!InFlow(child))
            {
                continue;
            }
            var desired = child.DesiredSize;
            var natural = new Point(x, y + (rowHeight - desired.Height) / 2);
            child.Arrange(new Rect(natural, desired));
            var slot = SlotOf(child);
            if (slot.Placed && animate && slot.Natural != natural)
            {
                // FLIP: start where the item was drawn, glide to its new place, keeping any velocity it had.
                slot.Offset ??= CreateOffset(child);
                var velocity = slot.Offset.Velocity;
                var from = slot.Natural + slot.Offset.Value - natural;
                slot.Offset.Jump(from);
                _ = slot.Offset.AnimateTo(default, MotionSpec.Spring(ArcSpring.Morph), velocity);
            }
            slot.Natural = natural;
            slot.Placed = true;
            UpdateTransform(child);
            x += desired.Width + columnGap;
        }
    }

    private void ArrangeLeaving()
    {
        foreach (var child in Children)
        {
            if (child is ReactionsPill { IsLeaving: true } && _slots.TryGetValue(child, out var slot))
            {
                child.Arrange(new Rect(slot.Natural, child.DesiredSize));
                UpdateTransform(child);
            }
        }
    }

    private MotionValue<Vector> CreateOffset(Control child)
    {
        var value = new MotionValue<Vector>(this, default, MotionVectors.Vector);
        value.Changed += _ => UpdateTransform(child);
        return value;
    }

    /// <summary>Writes the item's transform: presence scale about its center, then its FLIP offset.</summary>
    public void UpdateTransform(Control child)
    {
        var slot = SlotOf(child);
        var offset = slot.Offset?.Value ?? default;
        var scale = child is ReactionsPill pill ? pill.PresenceScale : 1;
        var size = child.Bounds.Size;
        var cx = size.Width / 2;
        var cy = size.Height / 2;
        slot.Transform.Matrix = Matrix.CreateTranslation(-cx, -cy) * Matrix.CreateScale(scale, scale) *
                                Matrix.CreateTranslation(cx + offset.X, cy + offset.Y);
        if (!ReferenceEquals(child.RenderTransform, slot.Transform))
        {
            child.RenderTransformOrigin = s_topLeft;
            child.RenderTransform = slot.Transform;
        }
    }
}
