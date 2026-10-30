using Avalonia.Interactivity;

namespace TKCode.Arc;

/// <summary>What the assistant behind a <see cref="GhostMascot"/> is doing; each status has its own pose, face and marks.</summary>
public enum GhostMascotStatus
{
    /// <summary>Ready: the ghost floats, blinks now and then and glances around.</summary>
    Idle,

    /// <summary>Listening (a microphone is open): it comes forward with wide eyes and a small head tilt.</summary>
    Listening,

    /// <summary>Thinking (waiting for the first token): it drifts up, turns translucent and looks up at a swirl that curls in and out.</summary>
    Thinking,

    /// <summary>Writing (streaming): it leans toward a line it writes with a tiny pen, eyes on the tip.</summary>
    Writing,

    /// <summary>Done: it lifts with happy eyes, a small smile and a sparkle.</summary>
    Done,

    /// <summary>Failed: it wobbles once, sinks and droops its eyes.</summary>
    Error
}

/// <summary>The theme color a <see cref="GhostMascot"/>'s sheet takes when no <see cref="GhostMascot.SheetBrush"/> is set.</summary>
public enum GhostMascotTone
{
    /// <summary>The accent color with accent-foreground eyes.</summary>
    Accent,

    /// <summary>The classic pale ghost: a raised surface with a hairline edge and foreground eyes; the marks keep the accent.</summary>
    Sheet,

    /// <summary>The foreground ink with background-colored eyes.</summary>
    Ink
}

/// <summary>Arguments of <see cref="GhostMascot.StatusChanged"/>: the status the self-playing ghost moved to.</summary>
public sealed class GhostMascotStatusChangedEventArgs : RoutedEventArgs
{
    /// <summary>Creates the arguments for <paramref name="status"/>.</summary>
    public GhostMascotStatusChangedEventArgs(GhostMascotStatus status)
        : base(GhostMascot.StatusChangedEvent)
    {
        Status = status;
    }

    /// <summary>The status the ghost shows now.</summary>
    public GhostMascotStatus Status { get; }
}
