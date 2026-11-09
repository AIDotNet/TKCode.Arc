using Avalonia;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Threading;
using TKCode.Arc.Motion;

namespace TKCode.Arc;

/// <summary>
/// A panel whose children repeat end to end without a seam: after the last child comes the first again, in both directions.
/// The viewport shows <see cref="VisibleItemCount"/> equal slots; children scroll with the wheel or touchpad, or from code
/// (<see cref="ScrollToIndex"/>, <see cref="ScrollToIndexCentered"/>, <see cref="ScrollNext"/>, <see cref="ScrollPrevious"/>).
/// Usable on its own or as an <see cref="ItemsControl.ItemsPanel"/>; it scrolls itself, so it needs no <see cref="ScrollViewer"/>.
/// </summary>
/// <remarks>
/// <para>
/// The scroll position (<see cref="Offset"/>) is kept in item units and always wrapped into <c>[0, count)</c> when the panel
/// is at rest, so it never grows however long the panel scrolls. Each child is placed once, at the position nearest the
/// viewport; a control cannot be in two places, so the loop is seamless when there are at least
/// <see cref="VisibleItemCount"/> + 1 visible children. With fewer, every child is still shown once and the rest of the
/// slots stay empty.
/// </para>
/// <para>
/// Indexes are child indexes (for an <see cref="ItemsControl"/>, item indexes). Collapsed children
/// (<c>IsVisible="False"</c>) are left out of the loop.
/// </para>
/// </remarks>
public class CyclicPanel : Panel
{
    /// <summary>The scroll direction: <c>Vertical</c> (the default) stacks items top to bottom, <c>Horizontal</c> left to right.</summary>
    public static readonly StyledProperty<Orientation> OrientationProperty =
        AvaloniaProperty.Register<CyclicPanel, Orientation>(nameof(Orientation), Orientation.Vertical);

    /// <summary>How many item slots the viewport holds; each slot gets an equal share of the panel's length.</summary>
    public static readonly StyledProperty<int> VisibleItemCountProperty =
        AvaloniaProperty.Register<CyclicPanel, int>(nameof(VisibleItemCount), 5, coerce: (_, v) => Math.Max(1, v));

    /// <summary>The gap between neighbouring items.</summary>
    public static readonly StyledProperty<double> SpacingProperty =
        AvaloniaProperty.Register<CyclicPanel, double>(nameof(Spacing), 0,
            coerce: (_, v) => double.IsNaN(v) ? 0 : Math.Max(0, v));

    /// <summary>Whether scrolls from code and wheel notches glide; when false they jump. Reduced or no motion always jumps.</summary>
    public static readonly StyledProperty<bool> IsAnimationEnabledProperty =
        AvaloniaProperty.Register<CyclicPanel, bool>(nameof(IsAnimationEnabled), true);

    /// <summary>The length of an animated scroll.</summary>
    public static readonly StyledProperty<TimeSpan> AnimationDurationProperty =
        AvaloniaProperty.Register<CyclicPanel, TimeSpan>(nameof(AnimationDuration), TimeSpan.FromMilliseconds(240));

    /// <summary>The easing of an animated scroll (Arc's standard ease-out curve by default).</summary>
    public static readonly StyledProperty<Easing> AnimationEasingProperty =
        AvaloniaProperty.Register<CyclicPanel, Easing>(nameof(AnimationEasing), ArcEasings.Standard);

    /// <summary>Whether the mouse wheel and touchpad scroll the panel.</summary>
    public static readonly StyledProperty<bool> IsWheelEnabledProperty =
        AvaloniaProperty.Register<CyclicPanel, bool>(nameof(IsWheelEnabled), true);

    /// <summary>Whether the panel settles on the nearest whole item after a touchpad or smooth-wheel scroll stops.</summary>
    public static readonly StyledProperty<bool> SnapsToItemsProperty =
        AvaloniaProperty.Register<CyclicPanel, bool>(nameof(SnapsToItems), true);

    /// <summary>The scroll position in items, wrapped into <c>[0, count)</c>: 2.5 means the leading edge is halfway through the third item.</summary>
    public static readonly DirectProperty<CyclicPanel, double> OffsetProperty =
        AvaloniaProperty.RegisterDirect<CyclicPanel, double>(nameof(Offset), o => o.Offset);

    /// <summary>
    /// The child indexes the viewport shows, in display order (leading edge first). An item counts as visible when any part
    /// of its slot is inside the panel.
    /// </summary>
    public static readonly DirectProperty<CyclicPanel, IReadOnlyList<int>> VisibleItemIndicesProperty =
        AvaloniaProperty.RegisterDirect<CyclicPanel, IReadOnlyList<int>>(nameof(VisibleItemIndices), o => o.VisibleItemIndices);

    /// <summary>Pixels a touchpad or smooth wheel moves per Avalonia wheel unit (the same as Avalonia's <c>ScrollViewer</c>).</summary>
    private const double WheelPixelsPerUnit = 50;

    /// <summary>How long a touchpad scroll must pause before the panel snaps to an item.</summary>
    private static readonly TimeSpan s_snapDelay = TimeSpan.FromMilliseconds(140);

    private const double Epsilon = 1e-6;

    // The raw position in item units. Between animations it is wrapped into [0, count); while a glide runs it may leave
    // that range (the glide takes the short way around) and is wrapped again when the glide ends.
    private double _position;
    private MotionValue<double>? _motion;
    private DispatcherTimer? _snapTimer;
    private double _offset;
    private IReadOnlyList<int> _visibleIndices = Array.Empty<int>();
    private readonly List<int> _cycle = [];
    private readonly List<(double Position, int Index)> _scratch = [];

    static CyclicPanel()
    {
        AffectsMeasure<CyclicPanel>(OrientationProperty, VisibleItemCountProperty, SpacingProperty);
        ClipToBoundsProperty.OverrideDefaultValue<CyclicPanel>(true);
        // Parking children depends on the clip (ArrangeOverride).
        AffectsArrange<CyclicPanel>(ClipToBoundsProperty);
    }

    /// <inheritdoc cref="OrientationProperty"/>
    public Orientation Orientation
    {
        get => GetValue(OrientationProperty);
        set => SetValue(OrientationProperty, value);
    }

    /// <inheritdoc cref="VisibleItemCountProperty"/>
    public int VisibleItemCount
    {
        get => GetValue(VisibleItemCountProperty);
        set => SetValue(VisibleItemCountProperty, value);
    }

    /// <inheritdoc cref="SpacingProperty"/>
    public double Spacing
    {
        get => GetValue(SpacingProperty);
        set => SetValue(SpacingProperty, value);
    }

    /// <inheritdoc cref="IsAnimationEnabledProperty"/>
    public bool IsAnimationEnabled
    {
        get => GetValue(IsAnimationEnabledProperty);
        set => SetValue(IsAnimationEnabledProperty, value);
    }

    /// <inheritdoc cref="AnimationDurationProperty"/>
    public TimeSpan AnimationDuration
    {
        get => GetValue(AnimationDurationProperty);
        set => SetValue(AnimationDurationProperty, value);
    }

    /// <inheritdoc cref="AnimationEasingProperty"/>
    public Easing AnimationEasing
    {
        get => GetValue(AnimationEasingProperty);
        set => SetValue(AnimationEasingProperty, value);
    }

    /// <inheritdoc cref="IsWheelEnabledProperty"/>
    public bool IsWheelEnabled
    {
        get => GetValue(IsWheelEnabledProperty);
        set => SetValue(IsWheelEnabledProperty, value);
    }

    /// <inheritdoc cref="SnapsToItemsProperty"/>
    public bool SnapsToItems
    {
        get => GetValue(SnapsToItemsProperty);
        set => SetValue(SnapsToItemsProperty, value);
    }

    /// <inheritdoc cref="OffsetProperty"/>
    public double Offset
    {
        get => _offset;
        private set => SetAndRaise(OffsetProperty, ref _offset, value);
    }

    /// <inheritdoc cref="VisibleItemIndicesProperty"/>
    public IReadOnlyList<int> VisibleItemIndices
    {
        get => _visibleIndices;
        private set => SetAndRaise(VisibleItemIndicesProperty, ref _visibleIndices, value);
    }

    /// <summary>Whether a scroll animation is running.</summary>
    public bool IsScrolling => _motion?.IsAnimating == true;

    /// <summary>
    /// Scrolls so that the child at <paramref name="index"/> is at the leading edge (top or left), the short way around.
    /// Returns false, and does nothing, for an index outside <c>Children</c> or a collapsed child.
    /// </summary>
    /// <param name="index">The child (item) index.</param>
    /// <param name="animated">Glide (when <see cref="IsAnimationEnabled"/> and motion allow it) or jump.</param>
    public bool ScrollToIndex(int index, bool animated = true) => ScrollToCyclePosition(index, 0, animated);

    /// <summary>
    /// Scrolls so that the child at <paramref name="index"/> is in the middle of the viewport, the short way around. With an
    /// even <see cref="VisibleItemCount"/> the middle falls between two slots, and the item is centred on it. Returns false,
    /// and does nothing, for an index outside <c>Children</c> or a collapsed child.
    /// </summary>
    /// <param name="index">The child (item) index.</param>
    /// <param name="animated">Glide (when <see cref="IsAnimationEnabled"/> and motion allow it) or jump.</param>
    public bool ScrollToIndexCentered(int index, bool animated = true)
        => ScrollToCyclePosition(index, (VisibleItemCount - 1) / 2.0, animated);

    /// <summary>Scrolls forward (down or right) by one item.</summary>
    public void ScrollNext() => ScrollBy(1);

    /// <summary>Scrolls back (up or left) by one item.</summary>
    public void ScrollPrevious() => ScrollBy(-1);

    /// <summary>
    /// Scrolls by <paramref name="items"/> items (negative scrolls back). Calls made while a glide runs add to its target, so
    /// quick repeated calls are never lost.
    /// </summary>
    /// <param name="items">How many items to move by.</param>
    /// <param name="animated">Glide (when <see cref="IsAnimationEnabled"/> and motion allow it) or jump.</param>
    public void ScrollBy(double items, bool animated = true)
    {
        if (_cycle.Count == 0 && CountCycle() == 0)
        {
            return;
        }
        var from = _motion is { IsAnimating: true } running ? running.Target : _position;
        MoveTo(from + items, animated);
    }

    private bool ScrollToCyclePosition(int index, double lead, bool animated)
    {
        if (index < 0 || index >= Children.Count || !Children[index].IsVisible)
        {
            return false;
        }
        var count = CountCycle();
        var k = _cycle.IndexOf(index);
        if (count == 0 || k < 0)
        {
            return false;
        }
        var from = _motion is { IsAnimating: true } running ? running.Target : _position;
        // Shortest way around: the wrapped difference, moved into (-count/2, count/2].
        var delta = Wrap(k - lead - from, count);
        if (delta > count / 2.0)
        {
            delta -= count;
        }
        MoveTo(from + delta, animated);
        return true;
    }

    private void MoveTo(double target, bool animated)
    {
        StopSnapTimer();
        // A glide moves the content, so reduced motion jumps instead (no fade substitute for a scroll).
        if (animated && IsAnimationEnabled && ArcMotion.IsFull(this) && AnimationDuration > TimeSpan.Zero)
        {
            _ = GlideAsync(target);
            return;
        }
        JumpTo(target);
    }

    /// <summary>
    /// The glide runs on a <see cref="MotionValue{T}"/> rather than a ControlTheme transition: the position is not a styled
    /// property, a glide across the seam must leave the wrapped range and be wrapped again when it ends, and retargeting a
    /// running glide (quick wheel notches) must start from where it is.
    /// </summary>
    private async Task GlideAsync(double target)
    {
        _motion ??= CreateMotion();
        if (!_motion.IsAnimating)
        {
            _motion.Jump(_position);
        }
        var reached = await _motion.AnimateTo(target, MotionSpec.Tween(AnimationDuration, AnimationEasing));
        if (reached)
        {
            Normalize();
        }
    }

    private MotionValue<double> CreateMotion()
    {
        var motion = new MotionValue<double>(this, _position, MotionVectors.Double);
        motion.Changed += value => SetPosition(value);
        return motion;
    }

    private void JumpTo(double target)
    {
        var count = CountCycle();
        var wrapped = count > 0 ? Wrap(target, count) : 0;
        if (_motion is { } motion)
        {
            motion.Jump(wrapped);
        }
        else
        {
            SetPosition(wrapped);
        }
    }

    private void Normalize()
    {
        var count = _cycle.Count;
        if (count == 0)
        {
            return;
        }
        var wrapped = Wrap(_position, count);
        if (Math.Abs(wrapped - _position) > Epsilon)
        {
            JumpTo(wrapped);
        }
    }

    private void SetPosition(double value)
    {
        _position = value;
        var count = _cycle.Count;
        Offset = count > 0 ? Wrap(value, count) : 0;
        InvalidateArrange();
    }

    private static double Wrap(double value, int count)
    {
        var wrapped = value - count * Math.Floor(value / count);
        return wrapped >= count - Epsilon || Math.Abs(wrapped) < Epsilon ? 0 : wrapped;
    }

    /// <summary>Rebuilds the list of children in the loop (the visible ones) and returns its length.</summary>
    private int CountCycle()
    {
        _cycle.Clear();
        var children = Children;
        for (var i = 0; i < children.Count; i++)
        {
            if (children[i].IsVisible)
            {
                _cycle.Add(i);
            }
        }
        return _cycle.Count;
    }

    protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
    {
        base.OnPointerWheelChanged(e);
        if (e.Handled || !IsWheelEnabled || CountCycle() == 0)
        {
            return;
        }
        var vertical = Orientation == Orientation.Vertical;
        var main = vertical ? e.Delta.Y : e.Delta.X;
        var other = vertical ? e.Delta.X : e.Delta.Y;
        var delta = Math.Abs(main) > Epsilon ? main : other;
        if (Math.Abs(delta) < Epsilon)
        {
            return;
        }
        e.Handled = true;
        if (Math.Abs(delta - Math.Round(delta)) < Epsilon)
        {
            // A wheel notch (whole units): one item per notch. Wheel down or right moves forward.
            ScrollBy(-Math.Round(delta));
            return;
        }
        // Touchpad or smooth wheel: follow the fingers, then settle on an item once they stop.
        var pitch = Pitch(vertical ? Bounds.Height : Bounds.Width);
        if (pitch <= 0)
        {
            return;
        }
        var from = _motion is { IsAnimating: true } running ? running.Value : _position;
        _motion?.Stop();
        SetPosition(from - delta * WheelPixelsPerUnit / pitch);
        if (SnapsToItems)
        {
            RestartSnapTimer();
        }
        else
        {
            Normalize();
        }
    }

    private void RestartSnapTimer()
    {
        if (_snapTimer is null)
        {
            _snapTimer = new DispatcherTimer { Interval = s_snapDelay };
            _snapTimer.Tick += (_, _) =>
            {
                StopSnapTimer();
                MoveTo(Math.Round(_position), true);
            };
        }
        _snapTimer.Stop();
        _snapTimer.Start();
    }

    private void StopSnapTimer() => _snapTimer?.Stop();

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        StopSnapTimer();
        if (_motion is { IsAnimating: true } motion)
        {
            motion.Jump(motion.Target);
        }
        Normalize();
    }

    /// <summary>The slot length (item plus spacing) for a panel length.</summary>
    private double Pitch(double length)
    {
        var slot = Slot(length);
        return slot + Spacing;
    }

    private double Slot(double length)
    {
        var visible = VisibleItemCount;
        return Math.Max(0, (length - (visible - 1) * Spacing) / visible);
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var vertical = Orientation == Orientation.Vertical;
        var length = vertical ? availableSize.Height : availableSize.Width;
        var cross = vertical ? availableSize.Width : availableSize.Height;
        var visible = VisibleItemCount;
        var spacing = Spacing;
        var count = CountCycle();
        var children = Children;

        double slot;
        if (double.IsFinite(length))
        {
            slot = Slot(length);
        }
        else
        {
            // Unbounded along the scroll: every slot is as long as the longest item.
            slot = 0;
            foreach (var child in children)
            {
                if (child.IsVisible)
                {
                    child.Measure(vertical ? new Size(cross, double.PositiveInfinity) : new Size(double.PositiveInfinity, cross));
                    slot = Math.Max(slot, vertical ? child.DesiredSize.Height : child.DesiredSize.Width);
                }
            }
        }

        var crossDesired = 0.0;
        foreach (var child in children)
        {
            if (!child.IsVisible)
            {
                continue;
            }
            child.Measure(vertical ? new Size(cross, slot) : new Size(slot, cross));
            crossDesired = Math.Max(crossDesired, vertical ? child.DesiredSize.Width : child.DesiredSize.Height);
        }

        // Keep the position inside the loop when children come and go.
        if (count > 0 && _motion is not { IsAnimating: true } && (_position < 0 || _position >= count))
        {
            JumpTo(_position);
        }
        else if (count == 0 && _position != 0)
        {
            JumpTo(0);
        }

        var desiredLength = double.IsFinite(length) ? length : visible * slot + (visible - 1) * spacing;
        return vertical ? new Size(crossDesired, desiredLength) : new Size(desiredLength, crossDesired);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var vertical = Orientation == Orientation.Vertical;
        var length = vertical ? finalSize.Height : finalSize.Width;
        var cross = vertical ? finalSize.Width : finalSize.Height;
        var slot = Slot(length);
        var pitch = slot + Spacing;
        var count = _cycle.Count;
        var children = Children;
        _scratch.Clear();

        // Children more than three slots past either edge share one parking rect while the panel clips: an unchanged rect lets
        // Avalonia skip their arrange and composition update, so a scroll frame costs the items near the viewport, not every
        // child. Without the clip they would be drawn, so they keep their true positions.
        var park = ClipToBounds;
        var reach = 3 * pitch;
        var parking = vertical ? new Rect(0, -reach - slot, cross, slot) : new Rect(-reach - slot, 0, slot, cross);

        var next = 0;
        for (var i = 0; i < children.Count; i++)
        {
            var child = children[i];
            if (next >= count || _cycle[next] != i)
            {
                child.Arrange(default);
                continue;
            }
            var k = next++;
            // Place each child once, nearest the viewport: an item just past the end of the loop shows before the start.
            var q = Wrap(k - _position, count);
            if (q > count - 1 + Epsilon)
            {
                q -= count;
            }
            var start = q * pitch;
            if (park && (start >= length + reach || start + slot <= -reach))
            {
                child.Arrange(parking);
                continue;
            }
            child.Arrange(vertical ? new Rect(0, start, cross, slot) : new Rect(start, 0, slot, cross));
            if (start < length - Epsilon && start + slot > Epsilon)
            {
                _scratch.Add((start, i));
            }
        }

        UpdateVisibleIndices();
        return finalSize;
    }

    private void UpdateVisibleIndices()
    {
        _scratch.Sort(static (a, b) => a.Position.CompareTo(b.Position));
        var current = _visibleIndices;
        var same = current.Count == _scratch.Count;
        for (var i = 0; same && i < _scratch.Count; i++)
        {
            same = current[i] == _scratch[i].Index;
        }
        if (same)
        {
            return;
        }
        var indices = new int[_scratch.Count];
        for (var i = 0; i < indices.Length; i++)
        {
            indices[i] = _scratch[i].Index;
        }
        VisibleItemIndices = indices;
    }
}
