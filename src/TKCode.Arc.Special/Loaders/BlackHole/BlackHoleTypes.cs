using Avalonia;
using Avalonia.Controls;

namespace TKCode.Arc;

/// <summary>Which way a <see cref="BlackHole"/> transition runs.</summary>
public enum BlackHoleDirection
{
    /// <summary>The items spiral into the hole and stay hidden.</summary>
    Swallow,

    /// <summary>The items spiral back out of the hole into their places.</summary>
    Release
}

/// <summary>Options for one <see cref="BlackHole.SwallowAsync"/> or <see cref="BlackHole.ReleaseAsync"/> call.</summary>
public sealed record BlackHoleOptions
{
    /// <summary>
    /// Where the hole opens, in the black hole's own coordinates. When neither this nor <see cref="OriginElement"/> is
    /// set, the hole opens at the centre of the targets' combined bounds.
    /// </summary>
    public Point? Origin { get; init; }

    /// <summary>A control whose centre is where the hole opens (for example the delete button). Ignored when <see cref="Origin"/> is set.</summary>
    public Control? OriginElement { get; init; }

    /// <summary>Length of the swallow; the release runs <see cref="BlackHole.ReleaseSpeed"/> times as long. Null uses <see cref="BlackHole.Duration"/>.</summary>
    public TimeSpan? Duration { get; init; }
}

/// <summary>Data of the <see cref="BlackHole.TransitionStarted"/> and <see cref="BlackHole.TransitionCompleted"/> events.</summary>
public sealed class BlackHoleTransitionEventArgs : EventArgs
{
    /// <summary>Creates the event data.</summary>
    /// <param name="direction">Which way the transition runs.</param>
    /// <param name="count">How many items it moves.</param>
    public BlackHoleTransitionEventArgs(BlackHoleDirection direction, int count)
    {
        Direction = direction;
        Count = count;
    }

    /// <summary>Which way the transition runs.</summary>
    public BlackHoleDirection Direction { get; }

    /// <summary>How many items the transition moves.</summary>
    public int Count { get; }
}
