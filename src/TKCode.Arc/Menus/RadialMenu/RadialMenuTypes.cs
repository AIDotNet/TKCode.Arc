using Avalonia.Interactivity;

namespace TKCode.Arc;

/// <summary>Which gestures open a <see cref="RadialMenu"/> attached to a control with <see cref="RadialMenu.MenuProperty"/>.</summary>
[Flags]
public enum RadialMenuTrigger
{
    /// <summary>No pointer gesture opens the menu (the ContextMenu key, Shift+F10, <see cref="RadialMenu.IsOpen"/> and the open methods still do).</summary>
    None = 0,

    /// <summary>A right-button press opens the menu at the pointer; hold, move toward an item and release to choose it.</summary>
    RightPress = 1,

    /// <summary>
    /// Pressing and holding still for 400ms (mouse, touch or pen) opens the menu at the press point; keep holding, move
    /// toward an item and release to choose it.
    /// </summary>
    Hold = 2,

    /// <summary>
    /// A primary click or tap on the control opens the menu (on a <see cref="Avalonia.Controls.Button"/>, its <c>Click</c>,
    /// keyboard included), centred as <see cref="RadialMenu.Placement"/> says.
    /// </summary>
    Click = 4
}

/// <summary>Where <see cref="RadialMenu.Open()"/>, <see cref="RadialMenu.IsOpen"/> and the click trigger centre the menu.</summary>
public enum RadialMenuPlacement
{
    /// <summary>On the centre of the target.</summary>
    Center,

    /// <summary>Where the pointer last was over the target (the centre of the target when it is not known).</summary>
    Pointer
}

/// <summary>Arguments of <see cref="RadialMenu.ItemInvoked"/>.</summary>
public sealed class RadialMenuItemInvokedEventArgs : RoutedEventArgs
{
    /// <summary>Creates the arguments for the chosen <paramref name="item"/> shown by <paramref name="container"/>.</summary>
    public RadialMenuItemInvokedEventArgs(RoutedEvent routedEvent, object? item, RadialMenuItem container)
        : base(routedEvent)
    {
        Item = item;
        Container = container;
    }

    /// <summary>The chosen item: the data item from <see cref="Avalonia.Controls.ItemsControl.ItemsSource"/>, or the declared <see cref="RadialMenuItem"/>.</summary>
    public object? Item { get; }

    /// <summary>The sector that shows the item (the item itself when it was declared as a <see cref="RadialMenuItem"/>).</summary>
    public RadialMenuItem Container { get; }
}

/// <summary>Arguments of <see cref="RadialMenu.SelectionChanged"/>.</summary>
public sealed class RadialMenuSelectionChangedEventArgs : RoutedEventArgs
{
    /// <summary>Creates the arguments for a change from <paramref name="oldItem"/> to <paramref name="newItem"/>.</summary>
    public RadialMenuSelectionChangedEventArgs(RoutedEvent routedEvent, object? oldItem, object? newItem)
        : base(routedEvent)
    {
        OldItem = oldItem;
        NewItem = newItem;
    }

    /// <summary>The item that was highlighted before; null when none was.</summary>
    public object? OldItem { get; }

    /// <summary>The item that is highlighted now; null when the highlight was cleared.</summary>
    public object? NewItem { get; }
}
