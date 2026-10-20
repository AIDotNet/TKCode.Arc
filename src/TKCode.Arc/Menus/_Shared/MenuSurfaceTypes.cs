using Avalonia.Interactivity;

namespace TKCode.Arc;

/// <summary>
/// Geometry and motion family of a <see cref="MenuSurface"/>. Each variant reproduces the Arc component it comes from.
/// </summary>
public enum MenuSurfaceVariant
{
    /// <summary>
    /// Dropdown menu, split button and button group menus: 36px rows, 26px panel radius, 12rem minimum width.
    /// Enters from the trigger edge (5px, 0.97 scale) on Arc's 580ms CSS spring with a 160ms fade; closes in 130ms.
    /// </summary>
    Dropdown,

    /// <summary>
    /// Context menu: 34px rows, 13rem minimum width, 300px maximum height, grows from the pointer on a snappy spring,
    /// pressed rows tint, icon-less rows show a check when checked.
    /// </summary>
    Context,

    /// <summary>
    /// Account panel (user menu): 15.5rem wide, 10px radius, 4px inset, 32px rows with 13px text, a quiet highlight
    /// and a critically damped spring from the avatar.
    /// </summary>
    Account,

    /// <summary>
    /// Bottom sheet for narrow windows (user menu below 640px): full width, top-rounded, 46px rows with 16px text,
    /// a drag handle and a scrim. Rises on a smooth spring, follows a downward drag and closes past 80px or a quick
    /// flick (dragging is off under reduced motion).
    /// </summary>
    Sheet
}

/// <summary>Side of the anchor the menu opens on (flips automatically when there is no room).</summary>
public enum MenuSide
{
    Bottom,
    Top,
    Left,
    Right
}

/// <summary>Alignment of the menu against the anchor edge it opens from.</summary>
public enum MenuAlignment
{
    Start,
    Center,
    End
}

/// <summary>How a <see cref="MenuEntry"/> toggles when it is activated.</summary>
public enum MenuEntryToggleType
{
    /// <summary>A command row.</summary>
    None,

    /// <summary>Flips <see cref="MenuEntry.IsChecked"/> and shows a check.</summary>
    CheckBox,

    /// <summary>Checks the entry and unchecks the other entries of its <see cref="MenuEntry.GroupName"/>; shows a dot.</summary>
    Radio
}

/// <summary>Where the keyboard position starts when a menu opens.</summary>
public enum MenuOpenFocus
{
    /// <summary>The surface takes focus without a highlight (pointer opens).</summary>
    Surface,

    /// <summary>The first enabled row is highlighted (Enter, Space, ArrowDown on the trigger).</summary>
    First,

    /// <summary>The last enabled row is highlighted (ArrowUp on the trigger).</summary>
    Last,

    /// <summary>Focus stays where it is (a menu that starts open never steals focus).</summary>
    None
}

/// <summary>Arguments of <see cref="MenuSurface.ItemClickedEvent"/> and the owners' <c>ItemClicked</c> events.</summary>
public class MenuEntryClickedEventArgs : RoutedEventArgs
{
    public MenuEntryClickedEventArgs(RoutedEvent routedEvent, MenuEntry entry, object? item)
        : base(routedEvent)
    {
        Entry = entry;
        Item = item;
    }

    /// <summary>The activated row.</summary>
    public MenuEntry Entry { get; }

    /// <summary>The data item behind the row (the entry itself for entries declared inline).</summary>
    public object? Item { get; }

    /// <summary>The entry's <see cref="MenuEntry.Header"/>.</summary>
    public object? Header => Entry.Header;
}
