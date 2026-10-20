using System.Collections.Specialized;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;

namespace TKCode.Arc;

/// <summary>
/// Base of the Media items panels (Carousel track, CardStack stage): realizes containers only for the indices the
/// owner asks for (<see cref="ShouldRealize"/>), recycles the rest into a pool, and leaves layout and per-frame
/// transforms to the subclass. Any change to the items recycles everything; the owner re-derives its state.
/// </summary>
internal abstract class MediaVirtualPanel : VirtualizingPanel
{
    private static readonly object s_ownContainer = new();

    private static readonly AttachedProperty<object?> RecycleKeyProperty =
        AvaloniaProperty.RegisterAttached<MediaVirtualPanel, Control, object?>("RecycleKey");

    private readonly SortedDictionary<int, Control> _realized = new();
    private readonly List<int> _scratch = new();
    private Dictionary<object, Stack<Control>>? _pool;

    /// <summary>Realized containers by item index, in index order.</summary>
    protected SortedDictionary<int, Control> Realized => _realized;

    /// <summary>Number of items in the owner.</summary>
    protected int ItemCount => ItemsControl?.ItemCount ?? 0;

    /// <summary>Whether the item at <paramref name="index"/> should have a container right now.</summary>
    protected abstract bool ShouldRealize(int index);

    /// <summary>Called after the items changed and every container was recycled.</summary>
    protected virtual void OnItemsReset()
    {
    }

    /// <summary>Recycles containers that are no longer wanted.</summary>
    protected void RecycleUnwanted()
    {
        _scratch.Clear();
        foreach (var index in _realized.Keys)
        {
            if (index >= ItemCount || !ShouldRealize(index))
            {
                _scratch.Add(index);
            }
        }
        foreach (var index in _scratch)
        {
            Recycle(index);
        }
    }

    /// <summary>Returns the container of <paramref name="index"/>, creating or reusing one when needed.</summary>
    protected Control? Realize(int index)
    {
        if (_realized.TryGetValue(index, out var existing))
        {
            return existing;
        }
        var generator = ItemContainerGenerator;
        var items = Items;
        if (generator is null || index < 0 || index >= items.Count)
        {
            return null;
        }
        var item = items[index];
        Control control;
        if (!generator.NeedsContainer(item, index, out var recycleKey))
        {
            control = (Control)item!;
            if (!control.IsSet(RecycleKeyProperty))
            {
                generator.PrepareItemContainer(control, control, index);
                AddInternalChild(control);
                control.SetValue(RecycleKeyProperty, s_ownContainer);
                generator.ItemContainerPrepared(control, item, index);
            }
            control.IsVisible = true;
        }
        else if (recycleKey is not null && _pool is not null && _pool.TryGetValue(recycleKey, out var stack) && stack.Count > 0)
        {
            control = stack.Pop();
            control.IsVisible = true;
            generator.PrepareItemContainer(control, item, index);
            generator.ItemContainerPrepared(control, item, index);
        }
        else
        {
            control = generator.CreateContainer(item, index, recycleKey);
            control.SetValue(RecycleKeyProperty, recycleKey);
            generator.PrepareItemContainer(control, item, index);
            AddInternalChild(control);
            generator.ItemContainerPrepared(control, item, index);
        }
        _realized[index] = control;
        OnRealized(control, index);
        return control;
    }

    /// <summary>A container was realized for <paramref name="index"/> (prepared and visible).</summary>
    protected virtual void OnRealized(Control container, int index)
    {
    }

    /// <summary>A container is about to be recycled.</summary>
    protected virtual void OnRecycling(Control container, int index)
    {
    }

    protected void Recycle(int index)
    {
        if (!_realized.Remove(index, out var control))
        {
            return;
        }
        OnRecycling(control, index);
        control.IsVisible = false;
        var key = control.GetValue(RecycleKeyProperty);
        if (ReferenceEquals(key, s_ownContainer))
        {
            return;
        }
        ItemContainerGenerator?.ClearItemContainer(control);
        if (key is null)
        {
            RemoveInternalChild(control);
            return;
        }
        _pool ??= new Dictionary<object, Stack<Control>>();
        if (!_pool.TryGetValue(key, out var stack))
        {
            stack = new Stack<Control>();
            _pool.Add(key, stack);
        }
        stack.Push(control);
    }

    protected void RecycleAll()
    {
        _scratch.Clear();
        _scratch.AddRange(_realized.Keys);
        foreach (var index in _scratch)
        {
            Recycle(index);
        }
    }

    protected override void OnItemsChanged(IReadOnlyList<object?> items, NotifyCollectionChangedEventArgs e)
    {
        base.OnItemsChanged(items, e);
        RecycleAll();
        OnItemsReset();
        InvalidateMeasure();
    }

    protected override Control? ScrollIntoView(int index) => ContainerFromIndex(index);

    protected override Control? ContainerFromIndex(int index) => _realized.TryGetValue(index, out var control) ? control : null;

    protected override int IndexFromContainer(Control container)
    {
        foreach (var pair in _realized)
        {
            if (ReferenceEquals(pair.Value, container))
            {
                return pair.Key;
            }
        }
        return -1;
    }

    protected override IEnumerable<Control>? GetRealizedContainers() => _realized.Values;

    // Keyboard navigation between items is the owner's job (Arc handles arrows on the root).
    protected override IInputElement? GetControl(NavigationDirection direction, IInputElement? from, bool wrap) => null;
}
