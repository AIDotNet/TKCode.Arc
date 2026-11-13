using Avalonia.Interactivity;

namespace TKCode.Arc.Mobile;

/// <summary>Arguments of <see cref="Picker.SelectionChanged"/>.</summary>
public sealed class PickerSelectionChangedEventArgs : RoutedEventArgs
{
    /// <summary>Creates the arguments for a change from <paramref name="oldItem"/> to <paramref name="newItem"/>.</summary>
    public PickerSelectionChangedEventArgs(RoutedEvent routedEvent, object? oldItem, object? newItem, int oldIndex, int newIndex)
        : base(routedEvent)
    {
        OldItem = oldItem;
        NewItem = newItem;
        OldIndex = oldIndex;
        NewIndex = newIndex;
    }

    /// <summary>The item that was selected before; null when none was.</summary>
    public object? OldItem { get; }

    /// <summary>The item that is selected now; null when the selection was cleared.</summary>
    public object? NewItem { get; }

    /// <summary>The index of <see cref="OldItem"/> among the choices, or -1.</summary>
    public int OldIndex { get; }

    /// <summary>The index of <see cref="NewItem"/> among the choices, or -1 (no selection, or an item that is not one of the choices).</summary>
    public int NewIndex { get; }
}

/// <summary>Arguments of <see cref="Picker.ItemInvoked"/>.</summary>
public sealed class PickerItemInvokedEventArgs : RoutedEventArgs
{
    /// <summary>Creates the arguments for the choice at <paramref name="index"/>.</summary>
    public PickerItemInvokedEventArgs(RoutedEvent routedEvent, object? item, int index)
        : base(routedEvent)
    {
        Item = item;
        Index = index;
    }

    /// <summary>The choice the person picked (it is now <see cref="Picker.SelectedItem"/>).</summary>
    public object? Item { get; }

    /// <summary>The index of <see cref="Item"/> among the choices.</summary>
    public int Index { get; }
}
