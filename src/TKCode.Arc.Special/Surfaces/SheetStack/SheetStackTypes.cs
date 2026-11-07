using System.ComponentModel;
using Avalonia.Interactivity;

namespace TKCode.Arc;

/// <summary>How a <see cref="SheetStack"/> presents its sheets.</summary>
public enum SheetStackMode
{
    /// <summary>Bottom sheets while the layer is narrower than <see cref="SheetStack.Breakpoint"/>, centered dialogs at or above it.</summary>
    Auto,

    /// <summary>Always bottom sheets: anchored to the bottom edge, at most 560 wide, with a grabber.</summary>
    Sheet,

    /// <summary>Always centered dialogs, 440 wide.</summary>
    Dialog
}

/// <summary>What changed the open stack of a <see cref="SheetStack"/>.</summary>
public enum SheetStackChangeReason
{
    /// <summary>A sheet was pushed (a trigger, <see cref="SheetStack.Push"/>).</summary>
    Push,

    /// <summary>The top sheet's back button.</summary>
    Back,

    /// <summary>The close button while a single sheet is open.</summary>
    Close,

    /// <summary>The "Close all" button: every sheet left together.</summary>
    CloseAll,

    /// <summary>The Escape key closed the top sheet.</summary>
    Escape,

    /// <summary>A press on the scrim (or on the edge of a sheet below) closed the top sheet.</summary>
    ScrimPress,

    /// <summary>The top sheet was dragged or flicked down.</summary>
    Drag,

    /// <summary><see cref="SheetStack.Pop"/>, <see cref="SheetStack.PopTo"/>, <see cref="SheetStack.Close"/> or a new <see cref="SheetStack.Stack"/> value.</summary>
    Programmatic
}

/// <summary>Event data of <see cref="SheetStack.StackChanged"/>.</summary>
public sealed class SheetStackChangedEventArgs : RoutedEventArgs
{
    /// <summary>Creates the event data.</summary>
    public SheetStackChangedEventArgs(RoutedEvent routedEvent, IReadOnlyList<string> oldStack, IReadOnlyList<string> newStack,
                                      SheetStackChangeReason reason)
        : base(routedEvent)
    {
        OldStack = oldStack;
        NewStack = newStack;
        Reason = reason;
    }

    /// <summary>The open sheet ids before the change, bottom first.</summary>
    public IReadOnlyList<string> OldStack { get; }

    /// <summary>The open sheet ids after the change, bottom first.</summary>
    public IReadOnlyList<string> NewStack { get; }

    /// <summary>What caused the change.</summary>
    public SheetStackChangeReason Reason { get; }
}

/// <summary>Event data of <see cref="SheetStack.Pushing"/>: set <see cref="CancelEventArgs.Cancel"/> to keep the sheet closed.</summary>
public sealed class SheetStackPushingEventArgs : CancelEventArgs
{
    /// <summary>Creates the event data.</summary>
    public SheetStackPushingEventArgs(string id, object? trigger)
    {
        Id = id;
        Trigger = trigger;
    }

    /// <summary>The id of the sheet about to open.</summary>
    public string Id { get; }

    /// <summary>The button that asked for it (a <see cref="SheetStack.OpenSheetProperty"/> trigger), or null for <see cref="SheetStack.Push"/>.</summary>
    public object? Trigger { get; }
}
