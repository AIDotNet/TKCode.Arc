using Avalonia.Interactivity;

namespace TKCode.Arc;

/// <summary>What the assistant behind an <see cref="AiMascot"/> is doing.</summary>
public enum AiMascotStatus
{
    /// <summary>Waiting: it breathes, floats, blinks and glances around while the dot drifts beside it.</summary>
    Idle,

    /// <summary>The microphone is open: it leans in with wider eyes while the dot pulses like a voice level.</summary>
    Listening,

    /// <summary>Waiting for the first token: the head tilts, the eyes look up and the dot orbits the head.</summary>
    Thinking,

    /// <summary>Streaming: it looks down and reads along the line the dot writes under it.</summary>
    Writing,

    /// <summary>Finished: a happy squint while the dot turns into a sparkle.</summary>
    Done,

    /// <summary>Failed: one shake that dies down, worried brows and a drooping dot.</summary>
    Error
}

/// <summary>Where an <see cref="AiMascot"/>'s body color comes from.</summary>
public enum AiMascotTone
{
    /// <summary>The accent with accent-foreground eyes.</summary>
    Accent,

    /// <summary>The foreground ink with background eyes.</summary>
    Ink,

    /// <summary>A muted surface with a hairline border and foreground eyes.</summary>
    Soft
}

/// <summary>When an <see cref="AiMascot"/>'s eyes follow the pointer.</summary>
public enum AiMascotPointerFollow
{
    /// <summary>While the pointer is over the mascot.</summary>
    Hover,

    /// <summary>Anywhere in the window (good for an empty state).</summary>
    Page,

    /// <summary>Never.</summary>
    Never
}

/// <summary>Arguments of <see cref="AiMascot.StatusChanged"/>: the state the self-playing tour moved to and the one it left.</summary>
public sealed class AiMascotStatusChangedEventArgs : RoutedEventArgs
{
    /// <summary>Creates the arguments.</summary>
    public AiMascotStatusChangedEventArgs(RoutedEvent routedEvent, AiMascotStatus status, AiMascotStatus previous)
        : base(routedEvent)
    {
        Status = status;
        Previous = previous;
    }

    /// <summary>The state now shown.</summary>
    public AiMascotStatus Status { get; }

    /// <summary>The state shown before.</summary>
    public AiMascotStatus Previous { get; }
}
