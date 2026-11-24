using Avalonia.Interactivity;

namespace TKCode.Arc;

/// <summary>
/// Arguments of <see cref="Tabs.TabCloseRequested"/>: the user asked to close a tab (its close button, a middle click or
/// Delete on the focused tab). The tabs never remove the tab themselves: remove <see cref="Item"/> from
/// <see cref="Avalonia.Controls.ItemsControl.Items"/> or from the <see cref="Avalonia.Controls.ItemsControl.ItemsSource"/>
/// collection to close it, or leave it to keep it open (for example while asking to save).
/// </summary>
public sealed class TabCloseRequestedEventArgs : RoutedEventArgs
{
    /// <summary>Creates the arguments for <paramref name="routedEvent"/>.</summary>
    public TabCloseRequestedEventArgs(RoutedEvent routedEvent, object? item, int index, TabItem container)
        : base(routedEvent)
    {
        Item = item;
        Index = index;
        Container = container;
    }

    /// <summary>The item to remove: the data item of a generated tab, or the <see cref="TabItem"/> itself when written inline.</summary>
    public object? Item { get; }

    /// <summary>The index of the tab in the list when the request was made.</summary>
    public int Index { get; }

    /// <summary>The tab's container.</summary>
    public TabItem Container { get; }
}
