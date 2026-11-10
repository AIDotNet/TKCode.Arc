using Avalonia.Interactivity;

namespace TKCode.Arc;

/// <summary>Whether a <see cref="TreeSelect"/> picks one node or checks several.</summary>
public enum TreeSelectMode
{
    /// <summary>A click picks one node and closes the menu.</summary>
    Single,

    /// <summary>Every row has a checkbox; checking a parent checks its enabled descendants and the menu stays open.</summary>
    Multiple,
}

/// <summary>Which checked nodes a <see cref="TreeSelect"/> in <see cref="TreeSelectMode.Multiple"/> reports.</summary>
public enum TreeSelectCheckStrategy
{
    /// <summary>Only the checked leaves.</summary>
    Leaves,

    /// <summary>A fully checked parent stands for its whole subtree; leaves are reported only when their parent is not fully checked.</summary>
    Parents,

    /// <summary>Every fully checked node, parents and leaves.</summary>
    All,
}

/// <summary>Data of <see cref="TreeSelect.SelectionChanged"/>.</summary>
public sealed class TreeSelectSelectionChangedEventArgs : RoutedEventArgs
{
    public TreeSelectSelectionChangedEventArgs(RoutedEvent routedEvent, IReadOnlyList<object> addedItems, IReadOnlyList<object> removedItems)
        : base(routedEvent)
    {
        AddedItems = addedItems;
        RemovedItems = removedItems;
    }

    /// <summary>Nodes that joined <see cref="TreeSelect.SelectedItems"/>, in tree order.</summary>
    public IReadOnlyList<object> AddedItems { get; }

    /// <summary>Nodes that left <see cref="TreeSelect.SelectedItems"/>, in tree order.</summary>
    public IReadOnlyList<object> RemovedItems { get; }
}
