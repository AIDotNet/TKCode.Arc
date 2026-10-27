using Avalonia.Interactivity;

namespace TKCode.Arc;

/// <summary>Arguments of the <see cref="Integrations"/> events about one app (the drawer opening and closing).</summary>
public class IntegrationEventArgs : RoutedEventArgs
{
    /// <summary>Creates the arguments.</summary>
    public IntegrationEventArgs(RoutedEvent routedEvent, object source, IntegrationItem item)
        : base(routedEvent, source)
    {
        Item = item;
    }

    /// <summary>The app.</summary>
    public IntegrationItem Item { get; }
}

/// <summary>Arguments of <c>Integrations.ConnectRequested</c>: a connect is about to start; set <see cref="Cancel"/> to stop it.</summary>
public sealed class IntegrationConnectRequestedEventArgs : IntegrationEventArgs
{
    /// <summary>Creates the arguments.</summary>
    public IntegrationConnectRequestedEventArgs(RoutedEvent routedEvent, object source, IntegrationItem item, int attempt)
        : base(routedEvent, source, item)
    {
        Attempt = attempt;
    }

    /// <summary>1 for the first attempt on this app, 2 for the next (a retry), and so on.</summary>
    public int Attempt { get; }

    /// <summary>Set to true to keep the connect from starting (the app stays as it is).</summary>
    public bool Cancel { get; set; }
}

/// <summary>Arguments of <c>Integrations.StatusChanged</c>: an app moved from <see cref="OldStatus"/> to <see cref="NewStatus"/>.</summary>
public sealed class IntegrationStatusChangedEventArgs : IntegrationEventArgs
{
    /// <summary>Creates the arguments.</summary>
    public IntegrationStatusChangedEventArgs(RoutedEvent routedEvent, object source, IntegrationItem item,
        IntegrationStatus oldStatus, IntegrationStatus newStatus, Exception? exception)
        : base(routedEvent, source, item)
    {
        OldStatus = oldStatus;
        NewStatus = newStatus;
        Exception = exception;
    }

    /// <summary>The status before.</summary>
    public IntegrationStatus OldStatus { get; }

    /// <summary>The status now.</summary>
    public IntegrationStatus NewStatus { get; }

    /// <summary>What the connect action threw when <see cref="NewStatus"/> is Error; null otherwise.</summary>
    public Exception? Exception { get; }
}

/// <summary>Arguments of <c>Integrations.FilterChanged</c>: the selected filter changed.</summary>
public sealed class IntegrationFilterChangedEventArgs : RoutedEventArgs
{
    /// <summary>Creates the arguments.</summary>
    public IntegrationFilterChangedEventArgs(RoutedEvent routedEvent, object source, string oldFilter, string newFilter, int visibleCount)
        : base(routedEvent, source)
    {
        OldFilter = oldFilter;
        NewFilter = newFilter;
        VisibleCount = visibleCount;
    }

    /// <summary>The filter before ("all", "connected" or a category key).</summary>
    public string OldFilter { get; }

    /// <summary>The filter now.</summary>
    public string NewFilter { get; }

    /// <summary>How many apps the list shows now.</summary>
    public int VisibleCount { get; }
}

/// <summary>Arguments of <c>Integrations.SearchChanged</c>: the search text changed.</summary>
public sealed class IntegrationSearchChangedEventArgs : RoutedEventArgs
{
    /// <summary>Creates the arguments.</summary>
    public IntegrationSearchChangedEventArgs(RoutedEvent routedEvent, object source, string text, int visibleCount)
        : base(routedEvent, source)
    {
        Text = text;
        VisibleCount = visibleCount;
    }

    /// <summary>The search text now.</summary>
    public string Text { get; }

    /// <summary>How many apps the list shows now.</summary>
    public int VisibleCount { get; }
}

/// <summary>Arguments of <c>Integrations.SyncFrequencyChanged</c>: the person chose another sync frequency for an app.</summary>
public sealed class IntegrationSyncFrequencyChangedEventArgs : IntegrationEventArgs
{
    /// <summary>Creates the arguments.</summary>
    public IntegrationSyncFrequencyChangedEventArgs(RoutedEvent routedEvent, object source, IntegrationItem item, string? oldKey, string newKey)
        : base(routedEvent, source, item)
    {
        OldKey = oldKey;
        NewKey = newKey;
    }

    /// <summary>The option's key before (null when none was stored).</summary>
    public string? OldKey { get; }

    /// <summary>The option's key now.</summary>
    public string NewKey { get; }
}
