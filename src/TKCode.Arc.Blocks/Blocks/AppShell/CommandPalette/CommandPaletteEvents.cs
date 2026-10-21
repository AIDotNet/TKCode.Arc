// Ported from Arc's free command-palette block (MIT, Copyright (c) 2026 Elia Kuratli): onClose of command-palette.tsx.

using Avalonia.Interactivity;

namespace TKCode.Arc;

/// <summary>What asked a <see cref="CommandPalette"/> to close.</summary>
public enum CommandPaletteCloseReason
{
    /// <summary>The Escape key in an empty search field.</summary>
    Escape,

    /// <summary>A click on the "Esc" close button.</summary>
    CloseButton
}

/// <summary>Arguments of <see cref="CommandPalette.CloseRequested"/>.</summary>
public sealed class CommandPaletteCloseRequestedEventArgs : RoutedEventArgs
{
    /// <summary>Creates the arguments for <paramref name="reason"/>.</summary>
    public CommandPaletteCloseRequestedEventArgs(RoutedEvent routedEvent, CommandPaletteCloseReason reason)
        : base(routedEvent)
    {
        Reason = reason;
    }

    /// <summary>What asked to close.</summary>
    public CommandPaletteCloseReason Reason { get; }
}
