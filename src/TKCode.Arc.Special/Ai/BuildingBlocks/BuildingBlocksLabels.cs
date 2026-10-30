namespace TKCode.Arc;

/// <summary>
/// Every word <see cref="BuildingBlocks"/> shows or says, in English by default. Replace the whole object to localize or
/// to name the thing being built (<c>Ready = "Your dashboard is ready"</c>).
/// </summary>
public sealed class BuildingBlocksLabels
{
    /// <summary>The English defaults.</summary>
    public static BuildingBlocksLabels Default { get; } = new();

    /// <summary>The label of an empty frame, before the first prompt.</summary>
    public string Idle { get; init; } = "Waiting for a prompt";

    /// <summary>The label while the blocks fly in.</summary>
    public string Layout { get; init; } = "Laying out";

    /// <summary>The label while the placeholders fill with content.</summary>
    public string Components { get; init; } = "Adding components";

    /// <summary>The label of the styling pass.</summary>
    public string Styling { get; init; } = "Styling";

    /// <summary>The label once the app is built.</summary>
    public string Ready { get; init; } = "Ready";

    /// <summary>The Replay button on Ready.</summary>
    public string Replay { get; init; } = "Replay";

    /// <summary>What assistive technology hears about the built-in sample: nothing was really generated.</summary>
    public string Simulated { get; init; } = "Simulated build, nothing was generated";

    /// <summary>The label of a phase.</summary>
    public string Of(BuildingBlocksPhase phase) => phase switch
    {
        BuildingBlocksPhase.Layout => Layout,
        BuildingBlocksPhase.Components => Components,
        BuildingBlocksPhase.Styling => Styling,
        BuildingBlocksPhase.Ready => Ready,
        _ => Idle
    };
}
