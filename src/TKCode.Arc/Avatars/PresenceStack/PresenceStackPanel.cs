using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using TKCode.Arc.Motion;

namespace TKCode.Arc;

/// <summary>
/// The overlapping row of a <see cref="PresenceStack"/>: the visible people, first one on top, then the "+N" chip. Each
/// part is arranged at its place in the row and moved from there by its own springs, so a join or a leave slides the
/// others without a layout per frame: a newcomer grows in at the front and sends out a ring, someone pushed past the
/// limit slides into the chip and fades, someone who leaves shrinks away where they were, and a person freed from the
/// chip slides out of it. The row's width changes at once; the stack's <see cref="Primitives.SpringSizeDecorator"/>
/// springs it.
/// </summary>
internal sealed class PresenceStackPanel : Control
{
    internal static readonly StyledProperty<double> OverlapProperty =
        AvaloniaProperty.Register<PresenceStackPanel, double>(nameof(Overlap), 10);

    private static readonly TimeSpan s_exit = TimeSpan.FromMilliseconds(180);
    private static readonly TimeSpan s_pulse = TimeSpan.FromMilliseconds(720);

    private readonly List<PresenceStackSlot> _slots = new();
    private readonly PresenceStackOverflow _overflow = new();
    private int _visibleCount;
    private double _slotSize;
    private bool _chipShown;

    static PresenceStackPanel()
    {
        AffectsMeasure<PresenceStackPanel>(OverlapProperty);
        KeyboardNavigation.TabNavigationProperty.OverrideDefaultValue<PresenceStackPanel>(KeyboardNavigationMode.Once);
    }

    public PresenceStackPanel()
    {
        _overflow.IsVisible = false;
        VisualChildren.Add(_overflow);
        LogicalChildren.Add(_overflow);
        ClipToBounds = false;
    }

    /// <summary>How far each person covers the one after them, in px.</summary>
    public double Overlap { get => GetValue(OverlapProperty); set => SetValue(OverlapProperty, value); }

    /// <summary>Every slot drawn, leaving ones included (tests).</summary>
    public IReadOnlyList<PresenceStackSlot> Slots => _slots;

    /// <summary>The visible people's slots in order (not leaving).</summary>
    public IEnumerable<PresenceStackSlot> VisibleSlots => _slots.Where(s => !s.IsLeaving);

    /// <summary>The "+N" chip.</summary>
    public PresenceStackOverflow Overflow => _overflow;

    /// <summary>
    /// Shows <paramref name="order"/> (first on top) with at most <paramref name="maxVisible"/> people and a chip for the
    /// rest. <paramref name="joined"/> are the people new to the stack (they grow in with a ring); others that become
    /// visible slide out of the chip.
    /// </summary>
    public void Sync(IReadOnlyList<PresenceStackViewer> order, int maxVisible, AvatarSize size, ISet<PresenceStackViewer> joined,
        bool animate, string overflowName)
    {
        var visible = Math.Min(order.Count, Math.Max(1, maxVisible));
        var hidden = order.Count - visible;
        var chipX = visible * Step;
        var kept = new HashSet<PresenceStackViewer>();
        for (var i = 0; i < visible; i++)
        {
            kept.Add(order[i]);
        }
        // People who are no longer visible: into the chip if they are still here, away in place if they left (a copy:
        // without motion a leaver is removed at once).
        foreach (var slot in _slots.ToArray())
        {
            if (slot.IsLeaving || slot.Viewer is null || kept.Contains(slot.Viewer))
            {
                continue;
            }
            slot.IsLeaving = true;
            slot.Focusable = false;
            slot.ZIndex = 0;
            var intoChip = order.Contains(slot.Viewer);
            Leave(slot, intoChip ? chipX : (double?)null, animate);
        }
        var rank = 0;
        for (var i = 0; i < visible; i++)
        {
            var viewer = order[i];
            var slot = _slots.FirstOrDefault(s => !s.IsLeaving && ReferenceEquals(s.Viewer, viewer));
            var target = i * Step;
            if (slot is null)
            {
                slot = new PresenceStackSlot();
                slot.SetViewer(viewer, size);
                _slots.Add(slot);
                VisualChildren.Add(slot);
                LogicalChildren.Add(slot);
                var isNew = joined.Contains(viewer);
                Enter(slot, target, isNew ? target : chipX, isNew, animate);
            }
            else
            {
                slot.SetSize(size);
                slot.LayoutX = target;
                if (animate)
                {
                    _ = slot.X!.AnimateTo(target, MotionSpec.Spring(ArcSpring.Smooth));
                }
                else
                {
                    slot.X!.Jump(target);
                }
            }
            slot.LayoutX = target;
            slot.ZIndex = 100 - rank++;
        }
        // Keep the visible people first in the child order, so Tab and the arrow keys follow the row.
        _slots.Sort((a, b) => (a.IsLeaving, b.IsLeaving) switch
        {
            (false, true) => -1,
            (true, false) => 1,
            _ => a.LayoutX.CompareTo(b.LayoutX)
        });
        _visibleCount = visible;
        UpdateChip(hidden, chipX, animate, overflowName);
        InvalidateMeasure();
    }

    private double Step => Math.Max(1, _slotSize - Overlap);

    private void Enter(PresenceStackSlot slot, double target, double from, bool withRing, bool animate)
    {
        slot.LayoutX = target;
        slot.X = new MotionValue<double>(slot, animate ? from : target, MotionVectors.Double);
        slot.Presence = new MotionValue<double>(slot, animate ? 0 : 1, MotionVectors.Double);
        slot.X.Changed += _ => slot.ApplyMotion(ArcMotion.IsFull(slot));
        slot.Presence.Changed += _ => slot.ApplyMotion(ArcMotion.IsFull(slot));
        if (!animate)
        {
            return;
        }
        _ = slot.X.AnimateTo(target, MotionSpec.Spring(ArcSpring.Smooth));
        // Reduced motion: no growth or travel, only a short fade in (a spring would jump straight to the end).
        _ = slot.Presence.AnimateTo(1, ArcMotion.IsFull(this) ? MotionSpec.Spring(ArcSpring.Snappy) : MotionSpec.Tween(s_exit, ArcEasings.Enter));
        if (withRing && ArcMotion.IsFull(this))
        {
            slot.Pulse = new MotionValue<double>(slot, 0, MotionVectors.Double);
            slot.Pulse.Changed += _ => slot.InvalidateVisual();
            _ = slot.Pulse.AnimateTo(1, MotionSpec.Tween(s_pulse, ArcEasings.Exit, TimeSpan.FromMilliseconds(80)));
        }
    }

    private async void Leave(PresenceStackSlot slot, double? into, bool animate)
    {
        if (!animate || slot.Presence is null || slot.X is null)
        {
            Remove(slot);
            return;
        }
        if (into is { } x)
        {
            _ = slot.X.AnimateTo(x, MotionSpec.Spring(ArcSpring.Smooth));
        }
        var done = await slot.Presence.AnimateTo(0, MotionSpec.Tween(s_exit, ArcEasings.Exit));
        if (done || slot.Presence.Value <= 0.001)
        {
            Remove(slot);
        }
    }

    private void Remove(PresenceStackSlot slot)
    {
        if (!_slots.Remove(slot))
        {
            return;
        }
        VisualChildren.Remove(slot);
        LogicalChildren.Remove(slot);
        slot.X?.Dispose();
        slot.Presence?.Dispose();
        slot.Pulse?.Dispose();
        InvalidateMeasure();
    }

    private void UpdateChip(int hidden, double chipX, bool animate, string name)
    {
        var chip = _overflow;
        chip.X ??= CreateChipValue(chipX);
        chip.Presence ??= CreateChipValue(0);
        chip.SetCount(hidden, name, animate && _chipShown);
        if (hidden > 0)
        {
            chip.IsVisible = true;
            chip.Focusable = true;
            chip.IsLeaving = false;
            var wasShown = _chipShown;
            _chipShown = true;
            chip.LayoutX = chipX;
            if (animate && wasShown)
            {
                _ = chip.X.AnimateTo(chipX, MotionSpec.Spring(ArcSpring.Smooth));
            }
            else
            {
                chip.X.Jump(chipX);
            }
            if (animate && !wasShown)
            {
                _ = chip.Presence.AnimateTo(1, MotionSpec.Spring(ArcSpring.Snappy));
            }
            else if (!animate)
            {
                chip.Presence.Jump(1);
            }
            chip.ApplyMotion(ArcMotion.IsFull(this));
        }
        else if (_chipShown)
        {
            _chipShown = false;
            chip.IsLeaving = true;
            chip.Focusable = false;
            HideChip(animate);
        }
    }

    private async void HideChip(bool animate)
    {
        var chip = _overflow;
        if (animate && chip.Presence is { } presence)
        {
            var done = await presence.AnimateTo(0, MotionSpec.Tween(s_exit, ArcEasings.Exit));
            if (!done || _chipShown)
            {
                return;
            }
        }
        else
        {
            chip.Presence?.Jump(0);
        }
        if (!_chipShown)
        {
            chip.IsVisible = false;
            InvalidateMeasure();
        }
    }

    private MotionValue<double> CreateChipValue(double value)
    {
        var motion = new MotionValue<double>(_overflow, value, MotionVectors.Double);
        motion.Changed += _ => _overflow.ApplyMotion(ArcMotion.IsFull(this));
        return motion;
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        double size = 0;
        foreach (var slot in _slots)
        {
            slot.Measure(Size.Infinity);
            size = Math.Max(size, slot.DesiredSize.Width);
        }
        if (size <= 0)
        {
            // Nobody yet: measure a stand-in so the chip and the row height match the avatars.
            var probe = new PresenceStackSlot();
            probe.Measure(Size.Infinity);
            size = Math.Max(size, probe.DesiredSize.Width);
        }
        _slotSize = size;
        _overflow.Width = size;
        _overflow.Height = size;
        _overflow.Measure(Size.Infinity);
        var parts = _visibleCount + (_chipShown ? 1 : 0);
        var width = parts == 0 ? 0 : (parts - 1) * Step + size;
        return new Size(width, size);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        foreach (var slot in _slots)
        {
            slot.Arrange(new Rect(slot.LayoutX, 0, _slotSize, _slotSize));
            slot.ApplyMotion(ArcMotion.IsFull(slot));
        }
        if (_overflow.IsVisible)
        {
            _overflow.Arrange(new Rect(_overflow.LayoutX, 0, _slotSize, _slotSize));
            _overflow.ApplyMotion(ArcMotion.IsFull(this));
        }
        return finalSize;
    }

    /// <summary>Re-sets every slot's position after the row's step changed (a new size).</summary>
    public void Relayout()
    {
        var i = 0;
        foreach (var slot in _slots)
        {
            if (slot.IsLeaving)
            {
                continue;
            }
            slot.LayoutX = i * Step;
            slot.X?.Jump(slot.LayoutX);
            i++;
        }
        _overflow.LayoutX = i * Step;
        _overflow.X?.Jump(_overflow.LayoutX);
        InvalidateArrange();
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Handled)
        {
            return;
        }
        var parts = new List<Control>(VisibleSlots);
        if (_overflow.IsVisible && !_overflow.IsLeaving)
        {
            parts.Add(_overflow);
        }
        var current = parts.FindIndex(p => p.IsFocused);
        if (current < 0 || parts.Count == 0)
        {
            return;
        }
        int? next = e.Key switch
        {
            Key.Right or Key.Down => Math.Min(parts.Count - 1, current + 1),
            Key.Left or Key.Up => Math.Max(0, current - 1),
            Key.Home => 0,
            Key.End => parts.Count - 1,
            _ => null
        };
        if (next is { } index)
        {
            parts[index].Focus(NavigationMethod.Directional);
            e.Handled = true;
        }
    }

    protected override void OnGotFocus(FocusChangedEventArgs e)
    {
        base.OnGotFocus(e);
        if (e.Source is PresenceStackPanel)
        {
            VisibleSlots.FirstOrDefault()?.Focus(e.NavigationMethod);
        }
    }
}
