using Avalonia.Interactivity;

namespace TKCode.Arc;

/// <summary>Arguments of <see cref="McpServers.ServerAdded"/> and <see cref="McpServers.ServerRemoved"/>.</summary>
public sealed class McpServersServerEventArgs : RoutedEventArgs
{
    /// <summary>Creates the arguments.</summary>
    public McpServersServerEventArgs(RoutedEvent routedEvent, object source, McpServer server) : base(routedEvent, source)
    {
        Server = server;
    }

    /// <summary>The server that was added or removed.</summary>
    public McpServer Server { get; }
}

/// <summary>Arguments of <see cref="McpServers.ServerToggled"/>.</summary>
public sealed class McpServersServerToggledEventArgs : RoutedEventArgs
{
    /// <summary>Creates the arguments.</summary>
    public McpServersServerToggledEventArgs(RoutedEvent routedEvent, object source, McpServer server, bool isEnabled)
        : base(routedEvent, source)
    {
        Server = server;
        IsEnabled = isEnabled;
    }

    /// <summary>The server that was turned on or off.</summary>
    public McpServer Server { get; }

    /// <summary>Whether it is now on.</summary>
    public bool IsEnabled { get; }
}

/// <summary>Arguments of <see cref="McpServers.ToolToggled"/>.</summary>
public sealed class McpServersToolToggledEventArgs : RoutedEventArgs
{
    /// <summary>Creates the arguments.</summary>
    public McpServersToolToggledEventArgs(RoutedEvent routedEvent, object source, McpServer server, IReadOnlyList<McpTool> tools,
        bool isEnabled) : base(routedEvent, source)
    {
        Server = server;
        Tools = tools;
        IsEnabled = isEnabled;
    }

    /// <summary>The server the tools belong to.</summary>
    public McpServer Server { get; }

    /// <summary>The tools that changed: one for a tool's switch, several for Enable all or Disable all.</summary>
    public IReadOnlyList<McpTool> Tools { get; }

    /// <summary>Whether they are now enabled.</summary>
    public bool IsEnabled { get; }
}

/// <summary>Arguments of <see cref="McpServers.StatusChanged"/>.</summary>
public sealed class McpServersStatusChangedEventArgs : RoutedEventArgs
{
    /// <summary>Creates the arguments.</summary>
    public McpServersStatusChangedEventArgs(RoutedEvent routedEvent, object source, McpServer server, McpServerStatus oldStatus,
        McpServerStatus newStatus) : base(routedEvent, source)
    {
        Server = server;
        OldStatus = oldStatus;
        NewStatus = newStatus;
    }

    /// <summary>The server whose status changed.</summary>
    public McpServer Server { get; }

    /// <summary>The status before.</summary>
    public McpServerStatus OldStatus { get; }

    /// <summary>The status now.</summary>
    public McpServerStatus NewStatus { get; }
}
