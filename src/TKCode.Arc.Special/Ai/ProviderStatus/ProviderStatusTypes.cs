using Avalonia.Interactivity;
using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>The health of a provider, or of one day in its history.</summary>
public enum ProviderStatusHealth
{
    /// <summary>Working normally: a check in the success color.</summary>
    Operational,

    /// <summary>Working, but slower or with some errors: a triangle in the warning color.</summary>
    Degraded,

    /// <summary>Failing: a cross in the danger color.</summary>
    Outage
}

/// <summary>Where an incident update stands, in the order a status page posts them.</summary>
public enum ProviderStatusStage
{
    /// <summary>The provider is looking into it.</summary>
    Investigating,

    /// <summary>The cause is known and a fix is under way.</summary>
    Identified,

    /// <summary>A fix is out and the provider is watching the results.</summary>
    Monitoring,

    /// <summary>It is over.</summary>
    Resolved
}

/// <summary>One AI provider in a <see cref="ProviderStatus"/> card: its mark, health, latency, daily history and incident.</summary>
public sealed class ProviderStatusItem
{
    /// <summary>A stable key: the row is reused while it stays, and <see cref="ProviderStatus.ExpandedId"/> names it.</summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>The provider's name.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// The provider's mark: a 24 × 24 stroked glyph drawn at 16px in the text color (pass your own logo's path). Null shows
    /// the name's first letter in a tinted rounded square.
    /// </summary>
    public Geometry? Mark { get; set; }

    /// <summary>The mark's color, or the monogram's tint; null uses the text color (a monogram: a quiet neutral).</summary>
    public IBrush? MarkBrush { get; set; }

    /// <summary>A short line under the name, such as the models you call.</summary>
    public string? Detail { get; set; }

    /// <summary>The provider's health now.</summary>
    public ProviderStatusHealth Health { get; set; }

    /// <summary>The median latency in milliseconds (shown as "412 ms" or "1.92 s"); null shows a dash.</summary>
    public double? LatencyMs { get; set; }

    /// <summary>Daily history, oldest first. The bar shows the last 30 days and pads missing days as No data; the last day is Today.</summary>
    public IReadOnlyList<ProviderStatusDay> Days { get; set; } = [];

    /// <summary>An open or just resolved incident. Its row becomes a button that opens the updates.</summary>
    public ProviderStatusIncident? Incident { get; set; }
}

/// <summary>One day in a provider's history.</summary>
public sealed class ProviderStatusDay
{
    /// <summary>The day (UTC).</summary>
    public DateOnly Date { get; set; }

    /// <summary>The day's uptime from 0 to 100, or null when there is no data for it.</summary>
    public double? Uptime { get; set; }

    /// <summary>The day's health; null derives it from <see cref="Uptime"/> (below 99.9 degraded, below 99 outage).</summary>
    public ProviderStatusHealth? Health { get; set; }

    /// <summary>A line for the day's tooltip, such as what happened.</summary>
    public string? Note { get; set; }
}

/// <summary>An incident of a provider: a title and its updates.</summary>
public sealed class ProviderStatusIncident
{
    /// <summary>A stable key for the incident.</summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>What is wrong, in a few words ("Elevated error rates").</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>The updates in any order; the card shows them newest first.</summary>
    public IReadOnlyList<ProviderStatusUpdate> Updates { get; set; } = [];

    /// <summary>The incident on the provider's status page; the Status page link opens it when nothing handles <see cref="ProviderStatus.IncidentOpened"/>.</summary>
    public Uri? Url { get; set; }
}

/// <summary>One update of an incident.</summary>
public sealed class ProviderStatusUpdate
{
    /// <summary>Where the incident stood.</summary>
    public ProviderStatusStage Stage { get; set; }

    /// <summary>When it was posted (shown as HH:MM UTC); <see cref="AtText"/> takes its place when set.</summary>
    public DateTimeOffset? At { get; set; }

    /// <summary>A preformatted time to show instead of <see cref="At"/>.</summary>
    public string? AtText { get; set; }

    /// <summary>What the provider said.</summary>
    public string Message { get; set; } = string.Empty;
}

/// <summary>Arguments of <see cref="ProviderStatus.ExpandedChanged"/>.</summary>
public sealed class ProviderStatusExpandedEventArgs : RoutedEventArgs
{
    /// <summary>Creates the arguments.</summary>
    public ProviderStatusExpandedEventArgs(RoutedEvent routedEvent, string? expandedId) : base(routedEvent)
    {
        ExpandedId = expandedId;
    }

    /// <summary>The provider whose incident is now open, or null when none is.</summary>
    public string? ExpandedId { get; }
}

/// <summary>Arguments of <see cref="ProviderStatus.IncidentOpened"/>.</summary>
public sealed class ProviderStatusIncidentEventArgs : RoutedEventArgs
{
    /// <summary>Creates the arguments.</summary>
    public ProviderStatusIncidentEventArgs(RoutedEvent routedEvent, ProviderStatusItem provider, ProviderStatusIncident incident)
        : base(routedEvent)
    {
        Provider = provider;
        Incident = incident;
    }

    /// <summary>The provider whose Status page link was pressed.</summary>
    public ProviderStatusItem Provider { get; }

    /// <summary>Its incident.</summary>
    public ProviderStatusIncident Incident { get; }
}
