using Avalonia.Interactivity;

namespace TKCode.Arc;

/// <summary>Arguments of <see cref="AgentOrchestrator.MessagePosted"/>.</summary>
public sealed class OrchestratorMessageEventArgs : RoutedEventArgs
{
    internal OrchestratorMessageEventArgs(RoutedEvent routedEvent, object source, OrchestratorMessage message)
        : base(routedEvent, source)
    {
        Message = message;
    }

    /// <summary>The message, with its <see cref="OrchestratorMessage.Sequence"/> and <see cref="OrchestratorMessage.Elapsed"/> set.</summary>
    public OrchestratorMessage Message { get; }
}

/// <summary>Arguments of <see cref="AgentOrchestrator.AgentStatusChanged"/>.</summary>
public sealed class OrchestratorAgentStatusEventArgs : RoutedEventArgs
{
    internal OrchestratorAgentStatusEventArgs(RoutedEvent routedEvent, object source, OrchestratorAgent agent,
        OrchestratorAgentStatus oldStatus, OrchestratorAgentStatus newStatus)
        : base(routedEvent, source)
    {
        Agent = agent;
        OldStatus = oldStatus;
        NewStatus = newStatus;
    }

    /// <summary>The agent whose status changed.</summary>
    public OrchestratorAgent Agent { get; }

    /// <summary>The status before.</summary>
    public OrchestratorAgentStatus OldStatus { get; }

    /// <summary>The status now.</summary>
    public OrchestratorAgentStatus NewStatus { get; }
}

/// <summary>Arguments of <see cref="AgentOrchestrator.RunStateChanged"/>.</summary>
public sealed class OrchestratorRunStateEventArgs : RoutedEventArgs
{
    internal OrchestratorRunStateEventArgs(RoutedEvent routedEvent, object source, OrchestratorRunState oldState, OrchestratorRunState newState)
        : base(routedEvent, source)
    {
        OldState = oldState;
        NewState = newState;
    }

    /// <summary>The run state before.</summary>
    public OrchestratorRunState OldState { get; }

    /// <summary>The run state now.</summary>
    public OrchestratorRunState NewState { get; }
}
