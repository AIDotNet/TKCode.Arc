using System.Collections;
using System.Collections.ObjectModel;
using System.Globalization;
using TKCode.Arc.Motion;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;

namespace TKCode.Arc;

/// <summary>A row of a <see cref="ReorderableList"/> as Arc models it: an id, a title and an optional detail line.</summary>
public class ReorderableListEntry
{
    public ReorderableListEntry()
    {
    }

    public ReorderableListEntry(string id, string title, string? detail = null)
    {
        Id = id;
        Title = title;
        Detail = detail;
    }

    public string Id { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;

    public string? Detail { get; set; }

    public override string ToString() => Title;
}

/// <summary>Arguments of <see cref="ReorderableList.Reordered"/>.</summary>
public sealed class ReorderableListReorderedEventArgs : RoutedEventArgs
{
    public ReorderableListReorderedEventArgs(RoutedEvent routedEvent, object? item, int oldIndex, int newIndex)
        : base(routedEvent)
    {
        Item = item;
        OldIndex = oldIndex;
        NewIndex = newIndex;
    }

    public object? Item { get; }

    public int OldIndex { get; }

    public int NewIndex { get; }
}

/// <summary>
/// Arc's reorderable list: organize items with direct drag and drop. Rows are dragged by their grip (pointer
/// capture): the row lifts, follows the pointer, the rows it passes slide aside on springs and the drop settles on
/// a quick spring with the release velocity; holding a row near the edge of a scrolling ancestor scrolls it.
/// Keyboard: ↑/↓ on a grip moves the row one place at once (Arc), Space / Enter picks it up so arrows preview the
/// move, Space / Enter drops it and Escape cancels; every step is announced. A mutable <see cref="ItemsControl.ItemsSource"/>
/// (or inline items) is reordered in place; <see cref="Reordered"/> reports each move. Drag motion is render
/// transforms only.
/// </summary>
[TemplatePart(AnnouncerPart, typeof(TextBlock))]
public class ReorderableList : ItemsControl
{
    public const string AnnouncerPart = "PART_Announcer";

    public static readonly StyledProperty<string> LabelProperty =
        AvaloniaProperty.Register<ReorderableList, string>(nameof(Label), "Reorder items");

    public static readonly StyledProperty<bool> MovesItemsProperty =
        AvaloniaProperty.Register<ReorderableList, bool>(nameof(MovesItems), true);

    public static readonly StyledProperty<Func<object, string>?> ItemTitleSelectorProperty =
        AvaloniaProperty.Register<ReorderableList, Func<object, string>?>(nameof(ItemTitleSelector));

    public static readonly RoutedEvent<ReorderableListReorderedEventArgs> ReorderedEvent =
        RoutedEvent.Register<ReorderableList, ReorderableListReorderedEventArgs>(nameof(Reordered), RoutingStrategies.Bubble);

    /// <summary>Drop settle: a spring close to snappy, quick with a hint of give (Arc bounceStiffness 520, bounceDamping 40).</summary>
    private static readonly SpringSpec s_settle = SpringSpec.FromPhysical(520, 40);

    /// <summary>Accessible name of the list.</summary>
    public string Label { get => GetValue(LabelProperty); set => SetValue(LabelProperty, value); }

    /// <summary>When true (default) a mutable source is reordered in place; otherwise only <see cref="Reordered"/> is raised.</summary>
    public bool MovesItems { get => GetValue(MovesItemsProperty); set => SetValue(MovesItemsProperty, value); }

    /// <summary>Title used in the grip's accessible name and in announcements (defaults to <see cref="ReorderableListEntry.Title"/> or the item text).</summary>
    public Func<object, string>? ItemTitleSelector { get => GetValue(ItemTitleSelectorProperty); set => SetValue(ItemTitleSelectorProperty, value); }

    /// <summary>Raised after a row was moved by dragging or from the keyboard.</summary>
    public event EventHandler<ReorderableListReorderedEventArgs>? Reordered
    {
        add => AddHandler(ReorderedEvent, value);
        remove => RemoveHandler(ReorderedEvent, value);
    }

    private sealed class DragState
    {
        public required ReorderableListItem Container { get; init; }
        public required object? Item { get; init; }
        public required int From { get; init; }
        public required bool Keyboard { get; init; }
        public IPointer? Pointer { get; init; }
        public required ReorderableListItem[] Containers { get; init; }
        public required double[] Tops { get; init; }
        public required double[] Heights { get; init; }
        public required double[] Targets { get; init; }
        public required int[] Order { get; init; }
        public double Gap { get; init; }
        public double StartY { get; init; }
        public int Position { get; set; }
        public Point LastPointer { get; set; }
        public double Offset { get; set; }
    }

    private readonly ReorderableListVelocityTracker _velocity = new();
    private readonly ReorderableListAutoScroller _autoScroll;
    private DragState? _drag;
    private TextBlock? _announcer;
    private bool _finishing;

    public ReorderableList()
    {
        _autoScroll = new ReorderableListAutoScroller(this, () =>
        {
            if (_drag is { Keyboard: false } drag)
            {
                FollowPointer(drag);
            }
        });
    }

    internal bool IsDragging => _drag is not null;

    internal string TitleOf(object? item)
    {
        if (item is null)
        {
            return string.Empty;
        }
        if (ItemTitleSelector is { } selector)
        {
            return selector(item);
        }
        return item is ReorderableListEntry entry ? entry.Title : Convert.ToString(item, CultureInfo.CurrentCulture) ?? string.Empty;
    }

    #region Containers

    protected override Control CreateContainerForItemOverride(object? item, int index, object? recycleKey) =>
        new ReorderableListItem(this);

    protected override bool NeedsContainerOverride(object? item, int index, out object? recycleKey) =>
        NeedsContainer<ReorderableListItem>(item, out recycleKey);

    protected override void PrepareContainerForItemOverride(Control container, object? item, int index)
    {
        base.PrepareContainerForItemOverride(container, item, index);
        if (container is ReorderableListItem row)
        {
            row.ResetMotion();
            if (ItemTemplate is null && row.ContentTemplate is null)
            {
                row.ContentTemplate = ReorderableListEntryTemplate.Instance;
            }
        }
    }

    protected override void ContainerForItemPreparedOverride(Control container, object? item, int index)
    {
        base.ContainerForItemPreparedOverride(container, item, index);
        PostRefresh();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == LabelProperty)
        {
            AutomationProperties.SetName(this, Label);
        }
        else if (change.Property == ItemCountProperty)
        {
            if (_drag is not null && !_finishing)
            {
                // The source changed under the drag: let every row settle home.
                Finish(commit: false);
            }
            PostRefresh();
        }
    }

    private bool _refreshPosted;

    /// <summary>Coalesces position refreshes after container changes into one pass.</summary>
    private void PostRefresh()
    {
        if (_refreshPosted)
        {
            return;
        }
        _refreshPosted = true;
        Dispatcher.UIThread.Post(() =>
        {
            _refreshPosted = false;
            RefreshPositions();
        }, DispatcherPriority.Loaded);
    }

    protected override Avalonia.Automation.Peers.AutomationPeer OnCreateAutomationPeer() =>
        new Avalonia.Automation.Peers.ItemsControlAutomationPeer(this);

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _announcer = e.NameScope.Find<TextBlock>(AnnouncerPart);
        AutomationProperties.SetName(this, Label);
    }

    /// <summary>Updates the position numbers and grip names (outside a drag).</summary>
    internal void RefreshPositions()
    {
        if (_drag is not null)
        {
            return;
        }
        var count = ItemCount;
        for (var i = 0; i < count; i++)
        {
            if (ContainerFromIndex(i) is ReorderableListItem row)
            {
                row.SetPosition(i + 1, count);
            }
        }
    }

    #endregion

    #region Moving items

    /// <summary>Moves the item at <paramref name="from"/> to <paramref name="to"/> (in place when the source allows it) with the slide animation, and raises <see cref="Reordered"/>.</summary>
    public void MoveItem(int from, int to)
    {
        var count = ItemCount;
        if (from < 0 || from >= count || to < 0 || to >= count || from == to)
        {
            return;
        }
        var item = Items[from];
        var visual = CaptureVisual(out var velocities);
        MoveInSource(from, to);
        Land(visual, velocities, item, null, 0);
        RaiseReordered(item, from, to);
    }

    private bool MoveInSource(int from, int to)
    {
        if (!MovesItems)
        {
            return false;
        }
        var list = ItemsSource is null ? Items : ItemsSource as IList;
        if (list is null || list.IsReadOnly || list.IsFixedSize)
        {
            return false;
        }
        switch (list)
        {
            case ObservableCollection<ReorderableListEntry> entries:
                entries.Move(from, to);
                return true;
            case ObservableCollection<object> objects:
                objects.Move(from, to);
                return true;
            case ObservableCollection<string> strings:
                strings.Move(from, to);
                return true;
            case AvaloniaList<object> avaloniaList:
                avaloniaList.Move(from, to);
                return true;
        }
        var item = list[from];
        list.RemoveAt(from);
        list.Insert(to, item);
        return true;
    }

    private Dictionary<object, double> CaptureVisual(out Dictionary<object, double> velocities)
    {
        var visual = new Dictionary<object, double>(ReferenceEqualityComparer.Instance);
        velocities = new Dictionary<object, double>(ReferenceEqualityComparer.Instance);
        var count = ItemCount;
        for (var i = 0; i < count; i++)
        {
            if (ContainerFromIndex(i) is ReorderableListItem row && Items[i] is { } item)
            {
                visual.TryAdd(item, row.Bounds.Y + row.Offset);
                velocities.TryAdd(item, row.OffsetVelocity);
            }
        }
        return visual;
    }

    /// <summary>FLIP after a move: every row starts where it was drawn and springs into its new slot.</summary>
    private void Land(Dictionary<object, double> visual, Dictionary<object, double> velocities, object? moved,
                      double? movedVelocity, double movedScale)
    {
        UpdateLayout();
        var count = ItemCount;
        for (var i = 0; i < count; i++)
        {
            if (ContainerFromIndex(i) is not ReorderableListItem row || Items[i] is not { } item)
            {
                continue;
            }
            if (!visual.TryGetValue(item, out var before))
            {
                row.JumpOffset(0);
                continue;
            }
            row.JumpOffset(before - row.Bounds.Y);
            if (ReferenceEquals(item, moved) && movedVelocity is { } velocity)
            {
                row.ZIndex = 1;
                row.AnimateOffset(0, s_settle, velocity);
            }
            else
            {
                if (ReferenceEquals(item, moved))
                {
                    row.ZIndex = 1;
                }
                row.AnimateOffset(0, ArcMotionPresets.Smooth, velocities.TryGetValue(item, out var v) ? v : 0);
            }
            if (ReferenceEquals(item, moved))
            {
                row.SetLifted(false);
            }
        }
        _ = movedScale;
        RefreshPositions();
    }

    private void RaiseReordered(object? item, int from, int to)
    {
        Announce($"{TitleOf(item)} moved to position {to + 1} of {ItemCount}.");
        RaiseEvent(new ReorderableListReorderedEventArgs(ReorderedEvent, item, from, to));
    }

    private void Announce(string message)
    {
        if (_announcer is null)
        {
            return;
        }
        _announcer.Text = message;
        AutomationProperties.SetName(_announcer, message);
    }

    #endregion

    #region Drag sessions

    private DragState? Start(ReorderableListItem container, bool keyboard, IPointer? pointer, Point pointerInPanel)
    {
        var index = IndexFromContainer(container);
        var count = ItemCount;
        if (index < 0 || count == 0)
        {
            return null;
        }
        var containers = new ReorderableListItem[count];
        var tops = new double[count];
        var heights = new double[count];
        for (var i = 0; i < count; i++)
        {
            if (ContainerFromIndex(i) is not ReorderableListItem row)
            {
                return null;
            }
            containers[i] = row;
            tops[i] = row.Bounds.Y;
            heights[i] = row.Bounds.Height;
        }
        var order = new int[count];
        for (var i = 0; i < count; i++)
        {
            order[i] = i;
        }
        var gap = count > 1 ? Math.Max(0, tops[1] - tops[0] - heights[0]) : 8;
        _velocity.Reset();
        var drag = new DragState
        {
            Container = container,
            Item = Items[index],
            From = index,
            Keyboard = keyboard,
            Pointer = pointer,
            Containers = containers,
            Tops = tops,
            Heights = heights,
            Targets = new double[count],
            Order = order,
            Gap = gap,
            StartY = pointerInPanel.Y - container.Offset,
            Position = index,
            Offset = container.Offset
        };
        container.SetLifted(true);
        container.SetDragging(!keyboard);
        return drag;
    }

    internal bool BeginPointerDrag(ReorderableListItem container, PointerPressedEventArgs e)
    {
        if (_drag is not null || !IsEffectivelyEnabled || ItemsPanelRoot is not { } panel)
        {
            return _drag is not null;
        }
        var drag = Start(container, keyboard: false, e.Pointer, e.GetPosition(panel));
        if (drag is null)
        {
            return false;
        }
        _drag = drag;
        container.Handle?.Focus(NavigationMethod.Pointer);
        e.Pointer.Capture(container.Handle);
        if (TopLevel.GetTopLevel(this) is { } topLevel)
        {
            drag.LastPointer = e.GetPosition(topLevel);
        }
        _velocity.Add(e.Timestamp / 1000.0, drag.Offset);
        return true;
    }

    internal void ContinuePointerDrag(PointerEventArgs e)
    {
        if (_drag is not { Keyboard: false } drag || !ReferenceEquals(drag.Pointer, e.Pointer) || TopLevel.GetTopLevel(this) is not { } topLevel)
        {
            return;
        }
        drag.LastPointer = e.GetPosition(topLevel);
        FollowPointer(drag);
        _velocity.Add(e.Timestamp / 1000.0, drag.Offset);
        _autoScroll.Update(drag.LastPointer);
    }

    /// <summary>The row follows the pointer directly (no spring), clamped to a little past the list's ends.</summary>
    private void FollowPointer(DragState drag)
    {
        if (ItemsPanelRoot is not { } panel || TopLevel.GetTopLevel(this) is not { } topLevel ||
            topLevel.TranslatePoint(drag.LastPointer, panel) is not { } point)
        {
            return;
        }
        var from = drag.From;
        var offset = point.Y - drag.StartY;
        var last = drag.Tops.Length - 1;
        var min = drag.Tops[0] - drag.Tops[from] - 24;
        var max = drag.Tops[last] + drag.Heights[last] - drag.Tops[from] - drag.Heights[from] + 24;
        offset = Math.Clamp(offset, min, max);
        drag.Offset = offset;
        drag.Container.JumpOffset(offset);
        Reorder(drag);
    }

    internal void EndPointerDrag(IPointer pointer, bool cancel)
    {
        if (_drag is not { Keyboard: false } drag || !ReferenceEquals(drag.Pointer, pointer) || _finishing)
        {
            return;
        }
        Finish(commit: !cancel);
    }

    /// <summary>Top of a slot in the current arrangement.</summary>
    private static double SlotTop(DragState drag, int position)
    {
        var top = drag.Tops[0];
        for (var p = 0; p < position; p++)
        {
            top += drag.Heights[drag.Order[p]] + drag.Gap;
        }
        return top;
    }

    /// <summary>Arc (motion Reorder): the dragged row swaps with a neighbor once its leading edge passes the neighbor's center.</summary>
    private void Reorder(DragState drag)
    {
        var count = drag.Order.Length;
        var top = drag.Tops[drag.From] + drag.Offset;
        var bottom = top + drag.Heights[drag.From];
        var changed = false;
        for (var guard = 0; guard < count; guard++)
        {
            var position = drag.Position;
            if (position < count - 1)
            {
                var next = drag.Order[position + 1];
                var center = SlotTop(drag, position + 1) + drag.Heights[next] / 2;
                if (bottom > center)
                {
                    Swap(drag, position, position + 1);
                    changed = true;
                    continue;
                }
            }
            if (position > 0)
            {
                var previous = drag.Order[position - 1];
                var center = SlotTop(drag, position - 1) + drag.Heights[previous] / 2;
                if (top < center)
                {
                    Swap(drag, position, position - 1);
                    changed = true;
                    continue;
                }
            }
            break;
        }
        if (changed)
        {
            ApplyArrangement(drag);
        }
    }

    private static void Swap(DragState drag, int a, int b)
    {
        (drag.Order[a], drag.Order[b]) = (drag.Order[b], drag.Order[a]);
        drag.Position = b;
    }

    /// <summary>Slides every other row to its slot in the current arrangement and updates the position numbers.</summary>
    private void ApplyArrangement(DragState drag)
    {
        var count = drag.Order.Length;
        for (var position = 0; position < count; position++)
        {
            var index = drag.Order[position];
            var row = drag.Containers[index];
            row.SetPosition(position + 1, count);
            if (index == drag.From)
            {
                continue;
            }
            var target = SlotTop(drag, position) - drag.Tops[index];
            if (Math.Abs(target - drag.Targets[index]) > 0.01 || !row.IsSettling && Math.Abs(row.Offset - target) > 0.01)
            {
                drag.Targets[index] = target;
                row.AnimateOffset(target, ArcMotionPresets.Smooth);
            }
        }
    }

    private void Finish(bool commit)
    {
        if (_drag is not { } drag || _finishing)
        {
            return;
        }
        _finishing = true;
        try
        {
            _autoScroll.Stop();
            _drag = null;
            if (drag.Pointer is { } pointer && ReferenceEquals(pointer.Captured, drag.Container.Handle))
            {
                pointer.Capture(null);
            }
            drag.Container.SetDragging(false);
            var from = drag.From;
            var to = commit ? drag.Position : from;
            var item = drag.Item;
            var handleFocused = drag.Container.Handle?.IsKeyboardFocusWithin == true;
            var visual = CaptureVisual(out var velocities);
            var releaseVelocity = drag.Keyboard ? drag.Container.OffsetVelocity : _velocity.Velocity;
            var moved = to != from && MoveInSource(from, to);
            if (!moved)
            {
                // Nothing moved in the source: the rows return to their slots (cancel, or an immutable source).
                for (var i = 0; i < drag.Containers.Length; i++)
                {
                    var row = drag.Containers[i];
                    if (ReferenceEquals(row, drag.Container))
                    {
                        row.AnimateOffset(0, s_settle, releaseVelocity);
                        row.SetLifted(false);
                    }
                    else
                    {
                        row.AnimateOffset(0, ArcMotionPresets.Smooth);
                    }
                }
                RefreshPositions();
            }
            else
            {
                Land(visual, velocities, item, releaseVelocity, 1.02);
            }
            if (to != from && commit)
            {
                if (drag.Keyboard)
                {
                    Announce($"{TitleOf(item)} dropped at position {to + 1} of {ItemCount}.");
                    RaiseEvent(new ReorderableListReorderedEventArgs(ReorderedEvent, item, from, to));
                }
                else
                {
                    RaiseReordered(item, from, to);
                }
            }
            else if (drag.Keyboard)
            {
                Announce(commit
                    ? $"{TitleOf(item)} dropped at position {from + 1} of {ItemCount}."
                    : $"Reorder cancelled. {TitleOf(item)} returned to position {from + 1} of {ItemCount}.");
            }
            if (handleFocused || drag.Keyboard)
            {
                RefocusHandle(item, drag.Keyboard ? NavigationMethod.Directional : NavigationMethod.Pointer);
            }
        }
        finally
        {
            _finishing = false;
        }
    }

    /// <summary>Reordering can recreate containers; focus stays on the moved row's grip.</summary>
    private void RefocusHandle(object? item, NavigationMethod method)
    {
        if (item is null)
        {
            return;
        }
        var index = Items.IndexOf(item);
        if (index >= 0 && ContainerFromIndex(index) is ReorderableListItem row && row.Handle is { } handle && !handle.IsFocused)
        {
            handle.Focus(method);
        }
    }

    #endregion

    #region Keyboard

    internal bool HandleKey(ReorderableListItem container, Key key)
    {
        if (_drag is { } drag)
        {
            if (!ReferenceEquals(drag.Container, container))
            {
                return false;
            }
            switch (key)
            {
                case Key.Escape:
                    Finish(commit: false);
                    return true;
                case Key.Space when drag.Keyboard:
                case Key.Enter when drag.Keyboard:
                    Finish(commit: true);
                    return true;
                case Key.Up when drag.Keyboard:
                    MovePicked(drag, drag.Position - 1);
                    return true;
                case Key.Down when drag.Keyboard:
                    MovePicked(drag, drag.Position + 1);
                    return true;
                case Key.Home when drag.Keyboard:
                    MovePicked(drag, 0);
                    return true;
                case Key.End when drag.Keyboard:
                    MovePicked(drag, drag.Order.Length - 1);
                    return true;
                default:
                    return false;
            }
        }
        var index = IndexFromContainer(container);
        if (index < 0)
        {
            return false;
        }
        switch (key)
        {
            case Key.Up:
            case Key.Down:
            {
                var target = index + (key == Key.Up ? -1 : 1);
                if (target >= 0 && target < ItemCount)
                {
                    MoveByKeyboard(index, target);
                }
                return true;
            }
            case Key.Space:
            case Key.Enter:
            {
                var picked = Start(container, keyboard: true, null, default);
                if (picked is not null)
                {
                    _drag = picked;
                    Announce($"Picked up {TitleOf(picked.Item)}. Position {index + 1} of {ItemCount}. " +
                             "Use the arrow keys to move, Space to drop, Escape to cancel.");
                }
                return true;
            }
            default:
                return false;
        }
    }

    private void MovePicked(DragState drag, int position)
    {
        position = Math.Clamp(position, 0, drag.Order.Length - 1);
        if (position == drag.Position)
        {
            return;
        }
        var step = position > drag.Position ? 1 : -1;
        while (drag.Position != position)
        {
            Swap(drag, drag.Position, drag.Position + step);
        }
        ApplyArrangement(drag);
        drag.Offset = SlotTop(drag, drag.Position) - drag.Tops[drag.From];
        drag.Container.AnimateOffset(drag.Offset, ArcMotionPresets.Smooth);
        Announce($"{TitleOf(drag.Item)}, position {drag.Position + 1} of {drag.Order.Length}.");
    }

    /// <summary>Arc: an arrow on the grip moves the row one place at once; the row stays raised for a moment and keeps focus.</summary>
    private void MoveByKeyboard(int from, int to)
    {
        var item = Items[from];
        var visual = CaptureVisual(out var velocities);
        if (!MoveInSource(from, to))
        {
            RaiseReordered(item, from, to);
            return;
        }
        Land(visual, velocities, item, null, 1);
        RefocusHandle(item, NavigationMethod.Directional);
        if (ContainerFromIndex(to) is ReorderableListItem row)
        {
            row.SetLifted(true);
            DispatcherTimer.RunOnce(() => row.SetLifted(false), TimeSpan.FromMilliseconds(260));
        }
        RaiseReordered(item, from, to);
    }

    internal void OnHandleLostFocus(ReorderableListItem container)
    {
        if (_drag is { Keyboard: true } drag && ReferenceEquals(drag.Container, container) && !_finishing)
        {
            Dispatcher.UIThread.Post(() =>
            {
                if (_drag is { Keyboard: true } current && ReferenceEquals(current, drag) && current.Container.Handle?.IsFocused != true)
                {
                    Finish(commit: false);
                }
            });
        }
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (!e.Handled && e.Key == Key.Escape && _drag is not null)
        {
            Finish(commit: false);
            e.Handled = true;
        }
    }

    #endregion
}
