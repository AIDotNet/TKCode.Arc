using Avalonia.Interactivity;

namespace TKCode.Arc;

/// <summary>The arguments of <see cref="ReasoningTree.Answered"/>: the answer the search reached and how it got there.</summary>
public sealed class ReasoningTreeAnsweredEventArgs : RoutedEventArgs
{
    /// <summary>Creates the arguments.</summary>
    public ReasoningTreeAnsweredEventArgs(RoutedEvent routedEvent, ReasoningThought answer, IReadOnlyList<ReasoningThought> path,
        int beamWidth, double score) : base(routedEvent)
    {
        Answer = answer;
        Path = path;
        BeamWidth = beamWidth;
        Score = score;
    }

    /// <summary>The thought the search chose as its answer.</summary>
    public ReasoningThought Answer { get; }

    /// <summary>The winning path from the first depth to the answer (the answer is last).</summary>
    public IReadOnlyList<ReasoningThought> Path { get; }

    /// <summary>The beam width the search ran with (1 to 3).</summary>
    public int BeamWidth { get; }

    /// <summary>The answer's score: its own, or the seeded one when it has none.</summary>
    public double Score { get; }
}
