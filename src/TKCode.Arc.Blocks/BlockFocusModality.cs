using System.Runtime.CompilerServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace TKCode.Arc;

/// <summary>
/// The kind of the last input each window saw: a pointer press or a key. Code that moves focus because of an action (a
/// pick closes a menu and focus goes back to its trigger, a button leaves and focus goes to its neighbour) takes its
/// navigation method from here, so the keyboard focus ring (<c>:focus-visible</c>) shows after keyboard use only, as
/// browsers do. Input in a popup counts for the window that hosts it.
/// </summary>
/// <remarks>
/// The blocks' copy of Core's internal <c>FocusModality</c>, which this package cannot see; keep the two alike.
/// A control that calls <see cref="For"/> calls <see cref="Watch"/> when it is created, so the input that leads to its
/// first focus move is already seen. Before any input a window counts as keyboard (the method stays what it was).
/// </remarks>
internal static class BlockFocusModality
{
    private static readonly ConditionalWeakTable<TopLevel, StrongBox<bool>> s_pointer = new();

    static BlockFocusModality()
    {
        InputElement.PointerPressedEvent.AddClassHandler<TopLevel>(static (root, _) => Set(root, pointer: true),
            RoutingStrategies.Tunnel, handledEventsToo: true);
        InputElement.KeyDownEvent.AddClassHandler<TopLevel>(static (root, _) => Set(root, pointer: false),
            RoutingStrategies.Tunnel, handledEventsToo: true);
    }

    /// <summary>Starts watching input (the first call does; later ones do nothing).</summary>
    public static void Watch()
    {
    }

    /// <summary>
    /// Whether the last press or key in the window of <paramref name="visual"/> was a pointer press; null when the window
    /// has seen neither yet.
    /// </summary>
    public static bool? LastWasPointer(Visual? visual) =>
        WindowOf(visual is null ? null : TopLevel.GetTopLevel(visual)) is { } window && s_pointer.TryGetValue(window, out var slot)
            ? slot.Value
            : null;

    /// <summary>
    /// <see cref="NavigationMethod.Pointer"/> after a pointer press in the window of <paramref name="visual"/>, else
    /// <paramref name="keyboard"/>.
    /// </summary>
    public static NavigationMethod For(Visual? visual, NavigationMethod keyboard = NavigationMethod.Tab) =>
        LastWasPointer(visual) == true ? NavigationMethod.Pointer : keyboard;

    private static void Set(TopLevel root, bool pointer)
    {
        if (WindowOf(root) is { } window)
        {
            s_pointer.GetValue(window, static _ => new StrongBox<bool>()).Value = pointer;
        }
    }

    /// <summary>A native popup root stands for the window of its popup (the root's logical parent).</summary>
    private static TopLevel? WindowOf(TopLevel? root)
    {
        for (var depth = 0; depth < 8 && root is PopupRoot popupRoot; depth++)
        {
            TopLevel? owner = null;
            for (var element = popupRoot.Parent; element is not null && owner is null; element = element.Parent)
            {
                if (element is Visual visual && TopLevel.GetTopLevel(visual) is { } top && !ReferenceEquals(top, root))
                {
                    owner = top;
                }
            }
            if (owner is null)
            {
                break;
            }
            root = owner;
        }
        return root;
    }
}
