using System.Collections;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.VisualTree;
using TKCode.Arc.Gestures;
using TKCode.Arc.Motion;

namespace TKCode.Arc;

public partial class ToolDock
{
    /// <summary>A sideways move this long starts a drag (a shorter one stays a click).</summary>
    private const double DragThreshold = 3;

    private readonly Dictionary<ToolDockItem, DragTracker> _trackers = new();
    private readonly Dictionary<object, double> _pendingFlip = new(ReferenceEqualityComparer.Instance);
    private DragState? _drag;

    /// <summary>The slot being dragged.</summary>
    private sealed class DragState(ToolDockItem slot, int startIndex, double startX)
    {
        public ToolDockItem Slot { get; } = slot;
        public int StartIndex { get; } = startIndex;
        public double StartX { get; } = startX;
    }

    /// <summary>Whether a slot is being dragged.</summary>
    internal bool IsDragging => _drag is not null;

    // ---- Pointer -------------------------------------------------------------------------------------------------

    /// <summary>A slot was clicked or tapped.</summary>
    internal void OnItemClicked(ToolDockItem slot, PointerType pointerType)
    {
        CoolLabel(pointerType == PointerType.Touch ? null : slot);
        Choose(slot, keyboard: false);
    }

    private void AttachDrag(ToolDockItem slot)
    {
        if (!CanReorder || _trackers.ContainsKey(slot))
        {
            return;
        }
        var tracker = new DragTracker(slot, new DragOptions
        {
            Threshold = DragThreshold,
            Axis = DragAxis.Horizontal,
            HandledEventsToo = true
        })
        {
            CanBegin = _ => slot.IsEffectivelyEnabled
        };
        tracker.Started += (_, _) => BeginSlotDrag(slot);
        tracker.Moved += (_, e) => MoveSlotDrag(slot, e.Offset.X);
        tracker.Completed += (_, _) => EndSlotDrag(slot);
        tracker.Cancelled += (_, _) => EndSlotDrag(slot);
        _trackers[slot] = tracker;
    }

    private void DetachDrag(ToolDockItem slot)
    {
        if (_trackers.Remove(slot, out var tracker))
        {
            if (_drag is { } drag && ReferenceEquals(drag.Slot, slot))
            {
                tracker.Cancel();
            }
            tracker.Dispose();
        }
    }

    private void BeginSlotDrag(ToolDockItem slot)
    {
        var index = IndexFromContainer(slot);
        if (index < 0)
        {
            return;
        }
        CloseTray();
        CoolLabel(null);
        _drag = new DragState(slot, index, slot.Bounds.X);
        slot.BeginDrag();
    }

    /// <summary>
    /// Follows the pointer and swaps the slot with a neighbour once its leading edge passes the neighbour's centre. The
    /// neighbour is what moves in the source, so the dragged slot keeps its container and its pointer capture.
    /// </summary>
    private void MoveSlotDrag(ToolDockItem slot, double dx)
    {
        if (_drag is not { } drag || !ReferenceEquals(drag.Slot, slot))
        {
            return;
        }
        var pitch = SlotPitch(slot);
        var width = slot.Bounds.Width;
        var left = drag.StartX + dx;
        var index = IndexFromContainer(slot);
        while (index >= 0 && index < ItemCount - 1 && left + width > LayoutCenter(index + 1, pitch, width))
        {
            if (!Swap(slot, index, index + 1, pitch))
            {
                break;
            }
            index++;
        }
        while (index > 0 && left < LayoutCenter(index - 1, pitch, width))
        {
            if (!Swap(slot, index, index - 1, pitch))
            {
                break;
            }
            index--;
        }
        slot.JumpOffset(left - LayoutX(index, pitch));
    }

    private void EndSlotDrag(ToolDockItem slot)
    {
        if (_drag is not { } drag || !ReferenceEquals(drag.Slot, slot))
        {
            return;
        }
        _drag = null;
        slot.EndDrag();
    }

    private static double LayoutX(int index, double pitch) => index * pitch;

    private static double LayoutCenter(int index, double pitch, double width) => index * pitch + width / 2;

    private double SlotPitch(ToolDockItem slot)
    {
        var spacing = ItemsPanelRoot is ToolDockPanel panel ? panel.Spacing : 0;
        return slot.Bounds.Width + spacing;
    }

    /// <summary>Moves the item at <paramref name="neighbour"/> to <paramref name="index"/> (where <paramref name="slot"/> is).</summary>
    private bool Swap(ToolDockItem slot, int index, int neighbour, double pitch)
    {
        if (ContainerFromIndex(neighbour) is not ToolDockItem other || ItemsView[neighbour] is not { } otherItem)
        {
            return false;
        }
        var otherVisualX = LayoutX(neighbour, pitch) + other.SlotOffset;
        if (!MoveInSource(neighbour, index))
        {
            return false;
        }
        _pendingFlip[otherItem] = otherVisualX;
        RaiseEvent(new ToolDockReorderedEventArgs(ReorderedEvent, ItemFromContainer(slot) ?? slot, index, neighbour));
        return true;
    }

    /// <summary>Moves an item of the source; false when the source cannot change.</summary>
    private bool MoveInSource(int from, int to)
    {
        if (from == to || from < 0 || to < 0 || from >= ItemCount || to >= ItemCount)
        {
            return false;
        }
        if (ItemsSource is null)
        {
            var item = Items[from];
            Items.RemoveAt(from);
            Items.Insert(to, item);
            return true;
        }
        if (ItemsSource is IList { IsReadOnly: false, IsFixedSize: false } list)
        {
            var item = list[from];
            list.RemoveAt(from);
            list.Insert(to, item);
            return true;
        }
        return false;
    }

    /// <summary>Called after the panel arranged: slots that a move put somewhere else spring from their old place.</summary>
    internal void OnPanelArranged()
    {
        if (_pendingFlip.Count > 0)
        {
            foreach (var (item, oldX) in _pendingFlip)
            {
                if (ContainerFromItem(item) is ToolDockItem slot && !ReferenceEquals(slot, _drag?.Slot))
                {
                    var delta = oldX - slot.Bounds.X;
                    if (Math.Abs(delta) > 0.5)
                    {
                        slot.JumpOffset(delta);
                        _ = slot.AnimateOffset(0, ArcMotionPresets.Smooth);
                    }
                }
            }
            _pendingFlip.Clear();
            if (_labelTarget is ToolDockItem target && _label is { IsShown: true })
            {
                ShowLabel(target);
            }
        }
    }

    // ---- Keyboard ------------------------------------------------------------------------------------------------

    /// <inheritdoc />
    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (!e.Handled && e.Source is Visual source)
        {
            if (Find<ToolDockTrayButton>(source) is { } button)
            {
                OnTrayKey(button, e);
            }
            else if (Find<ToolDockItem>(source) is { } slot && IndexFromContainer(slot) >= 0)
            {
                OnSlotKey(slot, e);
            }
        }
        base.OnKeyDown(e);
    }

    private T? Find<T>(Visual source) where T : Visual
    {
        for (Visual? v = source; v is not null && !ReferenceEquals(v, this); v = v.GetVisualParent())
        {
            if (v is T found)
            {
                return found;
            }
        }
        return null;
    }

    private void OnSlotKey(ToolDockItem slot, KeyEventArgs e)
    {
        var alt = (e.KeyModifiers & KeyModifiers.Alt) != 0;
        switch (e.Key)
        {
            case Key.Left or Key.Right when alt:
                if (CanReorder)
                {
                    e.Handled = true;
                    MoveSlot(slot, e.Key == Key.Right ? 1 : -1);
                }
                break;
            case Key.Left:
                e.Handled = true;
                FocusSlot(Step(IndexFromContainer(slot), -1));
                break;
            case Key.Right:
                e.Handled = true;
                FocusSlot(Step(IndexFromContainer(slot), 1));
                break;
            case Key.Home:
                e.Handled = true;
                FocusSlot(Slots.FirstOrDefault(s => s.IsEffectivelyEnabled));
                break;
            case Key.End:
                e.Handled = true;
                FocusSlot(Slots.LastOrDefault(s => s.IsEffectivelyEnabled));
                break;
            case Key.Enter or Key.Space when e.KeyModifiers == KeyModifiers.None:
                e.Handled = true;
                Choose(slot, keyboard: true);
                break;
            case Key.Up when slot.IsGroup:
                e.Handled = true;
                OpenTray(slot, focusMember: true);
                break;
            case Key.Escape when _openGroup is not null:
                e.Handled = true;
                var group = _openGroup;
                CloseTray();
                FocusSlot(group);
                break;
        }
    }

    private void OnTrayKey(ToolDockTrayButton button, KeyEventArgs e)
    {
        if (_tray is not { } tray)
        {
            return;
        }
        var buttons = tray.Buttons.Where(b => b.IsEffectivelyEnabled).ToList();
        var index = buttons.IndexOf(button);
        switch (e.Key)
        {
            case Key.Left or Key.Right when buttons.Count > 0:
                e.Handled = true;
                var next = buttons[((index + (e.Key == Key.Right ? 1 : -1)) % buttons.Count + buttons.Count) % buttons.Count];
                next.Focus(NavigationMethod.Directional);
                break;
            case Key.Home when buttons.Count > 0:
                e.Handled = true;
                buttons[0].Focus(NavigationMethod.Directional);
                break;
            case Key.End when buttons.Count > 0:
                e.Handled = true;
                buttons[^1].Focus(NavigationMethod.Directional);
                break;
            case Key.Enter or Key.Space when e.KeyModifiers == KeyModifiers.None:
                e.Handled = true;
                ChooseMember(button.Member, keyboard: true);
                break;
            case Key.Down or Key.Escape:
            {
                e.Handled = true;
                var group = _openGroup;
                CloseTray();
                FocusSlot(group);
                break;
            }
            case Key.Tab:
                // Close and let the Tab move on.
                CloseTray();
                break;
        }
    }

    /// <summary>The next enabled slot from <paramref name="from"/> in <paramref name="direction"/>, wrapping round.</summary>
    private ToolDockItem? Step(int from, int direction)
    {
        var count = ItemCount;
        for (var i = 1; i <= count; i++)
        {
            var index = ((from + direction * i) % count + count) % count;
            if (ContainerFromIndex(index) is ToolDockItem { IsEffectivelyEnabled: true, IsVisible: true } slot)
            {
                return slot;
            }
        }
        return null;
    }

    /// <summary>Alt + arrow: moves the focused slot one place (the neighbour moves in the source, so the focus stays).</summary>
    private void MoveSlot(ToolDockItem slot, int direction)
    {
        var from = IndexFromContainer(slot);
        var to = from + direction;
        if (from < 0 || to < 0 || to >= ItemCount || ContainerFromIndex(to) is not ToolDockItem other)
        {
            return;
        }
        var pitch = SlotPitch(slot);
        var slotItem = ItemFromContainer(slot) ?? slot;
        var otherItem = ItemsView[to];
        var slotX = LayoutX(from, pitch) + slot.SlotOffset;
        var otherX = LayoutX(to, pitch) + other.SlotOffset;
        if (otherItem is null || !MoveInSource(to, from))
        {
            return;
        }
        _pendingFlip[slotItem] = slotX;
        _pendingFlip[otherItem] = otherX;
        RaiseEvent(new ToolDockReorderedEventArgs(ReorderedEvent, slotItem, from, to));
    }
}
