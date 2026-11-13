using Avalonia.Interactivity;

namespace TKCode.Arc;

/// <summary>Carries the item or group member someone chose in a <see cref="ToolDock"/>.</summary>
public sealed class ToolDockItemEventArgs : RoutedEventArgs
{
    /// <summary>Creates the arguments for <paramref name="item"/>.</summary>
    public ToolDockItemEventArgs(RoutedEvent routedEvent, ToolDockItem item)
        : base(routedEvent)
    {
        Item = item;
        Value = item.Value;
    }

    /// <summary>The chosen item, or the chosen member of a group.</summary>
    public ToolDockItem Item { get; }

    /// <summary>The chosen item's <see cref="ToolDockItem.Value"/>, now the dock's <see cref="ToolDock.SelectedValue"/>.</summary>
    public object? Value { get; }
}

/// <summary>Carries a change of the selected value of a <see cref="ToolDock"/>.</summary>
public sealed class ToolDockSelectionChangedEventArgs : RoutedEventArgs
{
    /// <summary>Creates the arguments for a change from <paramref name="oldValue"/> to <paramref name="newValue"/>.</summary>
    public ToolDockSelectionChangedEventArgs(RoutedEvent routedEvent, object? oldValue, object? newValue)
        : base(routedEvent)
    {
        OldValue = oldValue;
        NewValue = newValue;
    }

    /// <summary>The <see cref="ToolDock.SelectedValue"/> before.</summary>
    public object? OldValue { get; }

    /// <summary>The <see cref="ToolDock.SelectedValue"/> now.</summary>
    public object? NewValue { get; }
}

/// <summary>Carries a move of an item along a <see cref="ToolDock"/>.</summary>
public sealed class ToolDockReorderedEventArgs : RoutedEventArgs
{
    /// <summary>Creates the arguments for a move of <paramref name="item"/> from <paramref name="oldIndex"/> to <paramref name="newIndex"/>.</summary>
    public ToolDockReorderedEventArgs(RoutedEvent routedEvent, object? item, int oldIndex, int newIndex)
        : base(routedEvent)
    {
        Item = item;
        OldIndex = oldIndex;
        NewIndex = newIndex;
    }

    /// <summary>The item that moved: the <see cref="ToolDockItem"/> written inline, or the data item from <c>ItemsSource</c>.</summary>
    public object? Item { get; }

    /// <summary>Where the item was.</summary>
    public int OldIndex { get; }

    /// <summary>Where the item is now.</summary>
    public int NewIndex { get; }
}
