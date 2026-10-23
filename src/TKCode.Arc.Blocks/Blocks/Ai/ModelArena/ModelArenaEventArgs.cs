using Avalonia.Interactivity;

namespace TKCode.Arc;

/// <summary>Arguments of <c>ModelArena.VoteCast</c>.</summary>
public class ArenaVoteEventArgs : RoutedEventArgs
{
    /// <summary>Creates the arguments.</summary>
    public ArenaVoteEventArgs(RoutedEvent routedEvent, object source, ArenaVote vote)
        : base(routedEvent, source)
    {
        Vote = vote;
    }

    /// <summary>The vote.</summary>
    public ArenaVote Vote { get; }
}

/// <summary>Arguments of <c>ModelArena.RoundStarted</c> and <c>RunStopped</c>.</summary>
public class ArenaRoundEventArgs : RoutedEventArgs
{
    /// <summary>Creates the arguments.</summary>
    public ArenaRoundEventArgs(RoutedEvent routedEvent, object source, int round, string prompt, AiModel modelA, AiModel modelB)
        : base(routedEvent, source)
    {
        Round = round;
        Prompt = prompt;
        ModelA = modelA;
        ModelB = modelB;
    }

    /// <summary>The round's number.</summary>
    public int Round { get; }

    /// <summary>The prompt.</summary>
    public string Prompt { get; }

    /// <summary>The model in slot A.</summary>
    public AiModel ModelA { get; }

    /// <summary>The model in slot B.</summary>
    public AiModel ModelB { get; }
}

/// <summary>Arguments of <c>ModelArena.PaneFailed</c>.</summary>
public class ArenaPaneFailedEventArgs : RoutedEventArgs
{
    /// <summary>Creates the arguments.</summary>
    public ArenaPaneFailedEventArgs(RoutedEvent routedEvent, object source, ArenaSlot slot, AiModel model, Exception error)
        : base(routedEvent, source)
    {
        Slot = slot;
        Model = model;
        Error = error;
    }

    /// <summary>The slot that failed.</summary>
    public ArenaSlot Slot { get; }

    /// <summary>The model that failed.</summary>
    public AiModel Model { get; }

    /// <summary>What went wrong.</summary>
    public Exception Error { get; }
}
