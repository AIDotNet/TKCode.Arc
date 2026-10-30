using Avalonia.Interactivity;

namespace TKCode.Arc;

/// <summary>What the assistant behind a <see cref="PixelMascot"/> is doing.</summary>
public enum PixelMascotStatus
{
    /// <summary>Waiting: the robot bobs, blinks and glances now and then.</summary>
    Idle,

    /// <summary>Working on an answer: a thought bubble cycles three dots while the eyes look up at it.</summary>
    Thinking,

    /// <summary>Writing the answer: a keyboard steps in, the hands type and a bar on the chest fills stroke by stroke.</summary>
    Writing,

    /// <summary>Finished: the robot jumps with its arms up, happy eyes and a sparkle.</summary>
    Done,

    /// <summary>Something failed: crossed eyes on a tinted screen and one shake.</summary>
    Error
}

/// <summary>Arguments of <see cref="PixelMascot.ShownStatusChanged"/>.</summary>
public sealed class PixelMascotStatusChangedEventArgs : RoutedEventArgs
{
    /// <summary>Creates the arguments.</summary>
    public PixelMascotStatusChangedEventArgs(RoutedEvent routedEvent, PixelMascotStatus oldStatus, PixelMascotStatus newStatus)
        : base(routedEvent)
    {
        OldStatus = oldStatus;
        NewStatus = newStatus;
    }

    /// <summary>The status the mascot showed before.</summary>
    public PixelMascotStatus OldStatus { get; }

    /// <summary>The status the mascot shows now.</summary>
    public PixelMascotStatus NewStatus { get; }
}
