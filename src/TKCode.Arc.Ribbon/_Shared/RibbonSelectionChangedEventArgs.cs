using Avalonia.Interactivity;

namespace TKCode.Arc;

/// <summary>Arguments of <see cref="Ribbon.SelectionChanged"/>: the selected tab changed.</summary>
public class RibbonSelectionChangedEventArgs : RoutedEventArgs
{
    /// <summary>Creates the arguments.</summary>
    public RibbonSelectionChangedEventArgs(RoutedEvent routedEvent, RibbonTab? oldItem, RibbonTab? newItem)
        : base(routedEvent)
    {
        OldItem = oldItem;
        NewItem = newItem;
    }

    /// <summary>The tab that was selected before, or null.</summary>
    public RibbonTab? OldItem { get; }

    /// <summary>The tab that is selected now, or null.</summary>
    public RibbonTab? NewItem { get; }
}
