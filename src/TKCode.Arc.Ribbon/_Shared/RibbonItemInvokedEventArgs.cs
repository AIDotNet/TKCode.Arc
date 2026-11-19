using Avalonia.Controls;
using Avalonia.Interactivity;

namespace TKCode.Arc;

/// <summary>
/// Arguments of <see cref="Ribbon.ItemInvoked"/>: the user ran a command of the ribbon, from the ribbon itself, from a group's
/// drop-down or from the <see cref="QuickAccessToolBar"/>.
/// </summary>
public class RibbonItemInvokedEventArgs : RoutedEventArgs
{
    /// <summary>Creates the arguments.</summary>
    public RibbonItemInvokedEventArgs(RoutedEvent routedEvent, Control item, MenuEntry? entry)
        : base(routedEvent)
    {
        Item = item;
        Entry = entry;
    }

    /// <summary>The command that ran: a <see cref="RibbonButton"/> (or one of its derived buttons).</summary>
    public Control Item { get; }

    /// <summary>The menu row that was chosen, when the command ran from the menu of a drop-down or split button; otherwise null.</summary>
    public MenuEntry? Entry { get; }
}
