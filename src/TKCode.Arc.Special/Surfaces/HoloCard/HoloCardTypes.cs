using Avalonia.Interactivity;

namespace TKCode.Arc;

/// <summary>What turned a <see cref="HoloCard"/> over, and so which way it spun.</summary>
public enum HoloCardFlipSide
{
    /// <summary>A click or tap on the right half: the card spins a further half turn to the right (+180°).</summary>
    Right,

    /// <summary>A click or tap on the left half: the card spins half a turn back to the left (−180°).</summary>
    Left,

    /// <summary>Enter or Space: the card spins to the right (+180°).</summary>
    Keyboard,

    /// <summary><see cref="HoloCard.Flip"/> or a change of <see cref="HoloCard.IsFlipped"/> from code or a binding.</summary>
    Programmatic,
}

/// <summary>Arguments of <see cref="HoloCard.FlippedChanged"/>.</summary>
public sealed class HoloCardFlippedEventArgs : RoutedEventArgs
{
    /// <summary>Creates the arguments for a turn to <paramref name="isFlipped"/> caused by <paramref name="side"/>.</summary>
    public HoloCardFlippedEventArgs(RoutedEvent routedEvent, bool isFlipped, HoloCardFlipSide side)
        : base(routedEvent)
    {
        IsFlipped = isFlipped;
        Side = side;
    }

    /// <summary>Whether the back now faces the viewer.</summary>
    public bool IsFlipped { get; }

    /// <summary>What turned the card over: a click on the left or right half, the keyboard, or code.</summary>
    public HoloCardFlipSide Side { get; }
}
