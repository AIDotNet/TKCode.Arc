using Avalonia.Interactivity;

namespace TKCode.Arc;

/// <summary>What the assistant behind a <see cref="CubeMascot"/> is doing; each status has its own pose, face and motion.</summary>
public enum CubeMascotStatus
{
    /// <summary>Ready: the cube tilts, sways and floats on slow sines, glances around and blinks now and then.</summary>
    Idle,

    /// <summary>Thinking (waiting for the first token): eyes up, the cube turns a quarter turn in eased steps.</summary>
    Thinking,

    /// <summary>Writing (streaming): the face tips down and lines print onto the ground in front of it.</summary>
    Writing,

    /// <summary>Done: the cube dips once and settles with a happy squint and a sparkle.</summary>
    Done,

    /// <summary>Failed: the cube shakes once and shows worried brows and a frown.</summary>
    Error
}

/// <summary>Arguments of <see cref="CubeMascot.StatusChanged"/>: the status the self-playing loop moved to.</summary>
public sealed class CubeMascotStatusChangedEventArgs : RoutedEventArgs
{
    /// <summary>Creates the arguments.</summary>
    public CubeMascotStatusChangedEventArgs(RoutedEvent routedEvent, CubeMascotStatus status)
        : base(routedEvent)
    {
        Status = status;
    }

    /// <summary>The status the cube shows now.</summary>
    public CubeMascotStatus Status { get; }
}
