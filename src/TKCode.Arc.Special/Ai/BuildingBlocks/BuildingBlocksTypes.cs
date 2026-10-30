using Avalonia.Interactivity;

namespace TKCode.Arc;

/// <summary>What the app tells a <see cref="BuildingBlocks"/> about its generation.</summary>
public enum BuildingBlocksStatus
{
    /// <summary>Nothing is being built yet: the frame shows its dashed slots and the idle label.</summary>
    Idle,

    /// <summary>The request runs: the phases play over <see cref="BuildingBlocks.Duration"/> and re-assemble while looping.</summary>
    Building,

    /// <summary>The result has arrived: the remaining phases fast forward and the stage settles on Ready.</summary>
    Done
}

/// <summary>The phase a <see cref="BuildingBlocks"/> shows in its status line.</summary>
public enum BuildingBlocksPhase
{
    /// <summary>Empty dashed slots, waiting for a prompt.</summary>
    Idle,

    /// <summary>The blocks fly in and land as tinted placeholders ("Laying out").</summary>
    Layout,

    /// <summary>The placeholders fill with neutral content ("Adding components").</summary>
    Components,

    /// <summary>The tint, the photos and the switch arrive ("Styling").</summary>
    Styling,

    /// <summary>The app is built ("Ready").</summary>
    Ready
}

/// <summary>The width a <see cref="BuildingBlocks"/> grows to (it never grows past its container).</summary>
public enum BuildingBlocksSize
{
    /// <summary>280px.</summary>
    Small,

    /// <summary>400px.</summary>
    Medium,

    /// <summary>560px.</summary>
    Large
}

/// <summary>Where the tint of a <see cref="BuildingBlocks"/> comes from when no <see cref="BuildingBlocks.Tint"/> is set.</summary>
public enum BuildingBlocksTone
{
    /// <summary>The theme's accent.</summary>
    Accent,

    /// <summary>The foreground color, for a monochrome build.</summary>
    Neutral
}

/// <summary>Arguments of <see cref="BuildingBlocks.PhaseChanged"/>.</summary>
public sealed class BuildingBlocksPhaseChangedEventArgs : RoutedEventArgs
{
    /// <summary>Creates the arguments.</summary>
    public BuildingBlocksPhaseChangedEventArgs(RoutedEvent routedEvent, BuildingBlocksPhase oldPhase, BuildingBlocksPhase phase)
        : base(routedEvent)
    {
        OldPhase = oldPhase;
        Phase = phase;
    }

    /// <summary>The phase shown before.</summary>
    public BuildingBlocksPhase OldPhase { get; }

    /// <summary>The phase shown now.</summary>
    public BuildingBlocksPhase Phase { get; }
}
