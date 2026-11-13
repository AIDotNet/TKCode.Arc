using Avalonia.Interactivity;

namespace TKCode.Arc;

/// <summary>Arguments of the <see cref="AgentGallery"/> events about one agent (opened, installed, uninstalled).</summary>
public sealed class AgentGalleryAgentEventArgs : RoutedEventArgs
{
    /// <summary>Creates the arguments.</summary>
    /// <param name="routedEvent">The event.</param>
    /// <param name="agent">The agent.</param>
    public AgentGalleryAgentEventArgs(RoutedEvent routedEvent, AgentGalleryItem agent)
        : base(routedEvent)
    {
        Agent = agent;
    }

    /// <summary>The agent the event is about.</summary>
    public AgentGalleryItem Agent { get; }
}

/// <summary>Arguments of <see cref="AgentGallery.InstallFailed"/>.</summary>
public sealed class AgentGalleryInstallFailedEventArgs : RoutedEventArgs
{
    /// <summary>Creates the arguments.</summary>
    /// <param name="routedEvent">The event.</param>
    /// <param name="agent">The agent that did not install.</param>
    /// <param name="message">The message shown in the detail view.</param>
    public AgentGalleryInstallFailedEventArgs(RoutedEvent routedEvent, AgentGalleryItem agent, string message)
        : base(routedEvent)
    {
        Agent = agent;
        Message = message;
    }

    /// <summary>The agent that did not install.</summary>
    public AgentGalleryItem Agent { get; }

    /// <summary>The message shown (the exception's message).</summary>
    public string Message { get; }
}

/// <summary>Arguments of <see cref="AgentGallery.PromptChosen"/>.</summary>
public sealed class AgentGalleryPromptEventArgs : RoutedEventArgs
{
    /// <summary>Creates the arguments.</summary>
    /// <param name="routedEvent">The event.</param>
    /// <param name="agent">The agent whose prompt was chosen.</param>
    /// <param name="prompt">The prompt.</param>
    public AgentGalleryPromptEventArgs(RoutedEvent routedEvent, AgentGalleryItem agent, string prompt)
        : base(routedEvent)
    {
        Agent = agent;
        Prompt = prompt;
    }

    /// <summary>The agent whose sample prompt was chosen.</summary>
    public AgentGalleryItem Agent { get; }

    /// <summary>The prompt text.</summary>
    public string Prompt { get; }
}
