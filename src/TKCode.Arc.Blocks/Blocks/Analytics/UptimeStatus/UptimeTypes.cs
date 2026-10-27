using Avalonia;
using Avalonia.Collections;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Metadata;

namespace TKCode.Arc;

/// <summary>How a service, a day or a whole page is doing; the order is the order of severity.</summary>
public enum ServiceStatus
{
    /// <summary>Everything works.</summary>
    Operational,

    /// <summary>Works, but slower or partly broken.</summary>
    Degraded,

    /// <summary>Does not work.</summary>
    Outage
}

/// <summary>The colour of an incident update's dot in the timeline (the newest update is filled, older ones are hollow).</summary>
public enum IncidentTone
{
    /// <summary>Foreground.</summary>
    Neutral,

    /// <summary>Success.</summary>
    Success,

    /// <summary>Warning.</summary>
    Warning,

    /// <summary>Danger.</summary>
    Danger
}

/// <summary>One half-hour sample of a service's response time.</summary>
/// <param name="Time">When the sample was taken.</param>
/// <param name="P50">The median response time in milliseconds.</param>
/// <param name="P95">The 95th percentile in milliseconds.</param>
public readonly record struct LatencySample(DateTimeOffset Time, double P50, double P95);

/// <summary>The person leading an incident response.</summary>
public sealed class UptimePerson
{
    /// <summary>Creates a person.</summary>
    public UptimePerson(string name, string? initials = null)
    {
        Name = name;
        Initials = initials;
    }

    /// <summary>The full name.</summary>
    public string Name { get; }

    /// <summary>Two letters for the avatar; null derives them from the name.</summary>
    public string? Initials { get; init; }

    /// <summary>A photo for the avatar; null draws the initials.</summary>
    public IImage? Image { get; init; }

    internal string AvatarText => !string.IsNullOrWhiteSpace(Initials)
        ? Initials!
        : string.Concat(Name.Split(' ', StringSplitOptions.RemoveEmptyEntries).Take(2).Select(p => char.ToUpperInvariant(p[0])));
}

/// <summary>One update in an incident's timeline.</summary>
public sealed class IncidentUpdate : AvaloniaObject
{
    /// <summary>The stage word ("Investigating", "Identified", "Monitoring", "Resolved").</summary>
    public static readonly StyledProperty<string> PhaseProperty = AvaloniaProperty.Register<IncidentUpdate, string>(nameof(Phase), string.Empty);

    /// <summary>When the update was posted.</summary>
    public static readonly StyledProperty<DateTimeOffset> TimeProperty = AvaloniaProperty.Register<IncidentUpdate, DateTimeOffset>(nameof(Time));

    /// <summary>What was said.</summary>
    public static readonly StyledProperty<string?> BodyProperty = AvaloniaProperty.Register<IncidentUpdate, string?>(nameof(Body));

    /// <summary>The colour of the dot while this is the newest update.</summary>
    public static readonly StyledProperty<IncidentTone> ToneProperty = AvaloniaProperty.Register<IncidentUpdate, IncidentTone>(nameof(Tone));

    /// <summary>Creates an empty update.</summary>
    public IncidentUpdate()
    {
    }

    /// <summary>Creates an update.</summary>
    public IncidentUpdate(string phase, DateTimeOffset time, string? body, IncidentTone tone)
    {
        Phase = phase;
        Time = time;
        Body = body;
        Tone = tone;
    }

    /// <inheritdoc cref="PhaseProperty"/>
    public string Phase { get => GetValue(PhaseProperty); set => SetValue(PhaseProperty, value); }

    /// <inheritdoc cref="TimeProperty"/>
    public DateTimeOffset Time { get => GetValue(TimeProperty); set => SetValue(TimeProperty, value); }

    /// <inheritdoc cref="BodyProperty"/>
    public string? Body { get => GetValue(BodyProperty); set => SetValue(BodyProperty, value); }

    /// <inheritdoc cref="ToneProperty"/>
    public IncidentTone Tone { get => GetValue(ToneProperty); set => SetValue(ToneProperty, value); }
}

/// <summary>An incident: something that made one or more services slower or unavailable for a while.</summary>
public sealed class UptimeIncident : AvaloniaObject
{
    /// <summary>A key that tells this incident from the others.</summary>
    public static readonly StyledProperty<string?> KeyProperty = AvaloniaProperty.Register<UptimeIncident, string?>(nameof(Key));

    /// <summary>The headline.</summary>
    public static readonly StyledProperty<string> TitleProperty = AvaloniaProperty.Register<UptimeIncident, string>(nameof(Title), string.Empty);

    /// <summary>Degraded or Outage; counts as downtime for the whole duration either way.</summary>
    public static readonly StyledProperty<ServiceStatus> SeverityProperty =
        AvaloniaProperty.Register<UptimeIncident, ServiceStatus>(nameof(Severity), ServiceStatus.Degraded);

    /// <summary>When it began.</summary>
    public static readonly StyledProperty<DateTimeOffset> StartProperty = AvaloniaProperty.Register<UptimeIncident, DateTimeOffset>(nameof(Start));

    /// <summary>How long it has lasted (so far, while it is not resolved).</summary>
    public static readonly StyledProperty<TimeSpan> DurationProperty = AvaloniaProperty.Register<UptimeIncident, TimeSpan>(nameof(Duration));

    /// <summary>True once it is over.</summary>
    public static readonly StyledProperty<bool> IsResolvedProperty = AvaloniaProperty.Register<UptimeIncident, bool>(nameof(IsResolved), true);

    /// <summary>The person leading the response.</summary>
    public static readonly StyledProperty<UptimePerson?> LeadProperty = AvaloniaProperty.Register<UptimeIncident, UptimePerson?>(nameof(Lead));

    /// <summary>The timeline is open (two-way).</summary>
    public static readonly StyledProperty<bool> IsExpandedProperty =
        AvaloniaProperty.Register<UptimeIncident, bool>(nameof(IsExpanded), defaultBindingMode: Avalonia.Data.BindingMode.TwoWay);

    /// <inheritdoc cref="KeyProperty"/>
    public string? Key { get => GetValue(KeyProperty); set => SetValue(KeyProperty, value); }

    /// <inheritdoc cref="TitleProperty"/>
    public string Title { get => GetValue(TitleProperty); set => SetValue(TitleProperty, value); }

    /// <inheritdoc cref="SeverityProperty"/>
    public ServiceStatus Severity { get => GetValue(SeverityProperty); set => SetValue(SeverityProperty, value); }

    /// <inheritdoc cref="StartProperty"/>
    public DateTimeOffset Start { get => GetValue(StartProperty); set => SetValue(StartProperty, value); }

    /// <inheritdoc cref="DurationProperty"/>
    public TimeSpan Duration { get => GetValue(DurationProperty); set => SetValue(DurationProperty, value); }

    /// <inheritdoc cref="IsResolvedProperty"/>
    public bool IsResolved { get => GetValue(IsResolvedProperty); set => SetValue(IsResolvedProperty, value); }

    /// <inheritdoc cref="LeadProperty"/>
    public UptimePerson? Lead { get => GetValue(LeadProperty); set => SetValue(LeadProperty, value); }

    /// <inheritdoc cref="IsExpandedProperty"/>
    public bool IsExpanded { get => GetValue(IsExpandedProperty); set => SetValue(IsExpandedProperty, value); }

    /// <summary>The keys of the services it affected.</summary>
    public AvaloniaList<string> ServiceKeys { get; } = new();

    /// <summary>The updates in any order; the timeline shows the newest first.</summary>
    [Content]
    public AvaloniaList<IncidentUpdate> Updates { get; } = new();
}

/// <summary>One day of a service's history, derived from the incidents.</summary>
public sealed class UptimeDay
{
    internal UptimeDay(DateOnly date, ServiceStatus status, double uptime, double downtimeMinutes, IReadOnlyList<UptimeDayIncident> incidents)
    {
        Date = date;
        Status = status;
        Uptime = uptime;
        DowntimeMinutes = downtimeMinutes;
        Incidents = incidents;
    }

    /// <summary>The (UTC) day.</summary>
    public DateOnly Date { get; }

    /// <summary>The most severe incident of the day; Operational when there was none.</summary>
    public ServiceStatus Status { get; }

    /// <summary>1 minus the day's downtime minutes over 1,440 (0 to 1).</summary>
    public double Uptime { get; }

    /// <summary>The minutes of incidents that fell on this day.</summary>
    public double DowntimeMinutes { get; }

    /// <summary>The incidents of the day with the minutes each one took on it.</summary>
    public IReadOnlyList<UptimeDayIncident> Incidents { get; }
}

/// <summary>An incident's share of one day.</summary>
/// <param name="Incident">The incident.</param>
/// <param name="Minutes">The minutes of it that fell on the day.</param>
public readonly record struct UptimeDayIncident(UptimeIncident Incident, double Minutes);

/// <summary>A service: a name, a response-time target and half-hourly latency samples.</summary>
public sealed class UptimeService : AvaloniaObject
{
    /// <summary>A key that tells this service from the others; incidents refer to it.</summary>
    public static readonly StyledProperty<string> KeyProperty = AvaloniaProperty.Register<UptimeService, string>(nameof(Key), string.Empty);

    /// <summary>The name.</summary>
    public static readonly StyledProperty<string> NameProperty = AvaloniaProperty.Register<UptimeService, string>(nameof(Name), string.Empty);

    /// <summary>A short description after the name.</summary>
    public static readonly StyledProperty<string?> DescriptionProperty = AvaloniaProperty.Register<UptimeService, string?>(nameof(Description));

    /// <summary>The p95 response time the service should stay under, in milliseconds (the dashed line).</summary>
    public static readonly StyledProperty<double> TargetP95Property = AvaloniaProperty.Register<UptimeService, double>(nameof(TargetP95));

    /// <summary>The 90-day uptime, 0 to 1 (set by the block).</summary>
    public static readonly DirectProperty<UptimeService, double> UptimeProperty =
        AvaloniaProperty.RegisterDirect<UptimeService, double>(nameof(Uptime), o => o.Uptime);

    /// <summary>Today's status (set by the block).</summary>
    public static readonly DirectProperty<UptimeService, ServiceStatus> CurrentStatusProperty =
        AvaloniaProperty.RegisterDirect<UptimeService, ServiceStatus>(nameof(CurrentStatus), o => o.CurrentStatus);

    private double _uptime = 1;
    private ServiceStatus _currentStatus;
    private IReadOnlyList<UptimeDay> _days = [];

    /// <summary>Creates an empty service.</summary>
    public UptimeService()
    {
    }

    /// <summary>Creates a service.</summary>
    public UptimeService(string key, string name, string? description, double targetP95, IEnumerable<LatencySample> latency)
    {
        Key = key;
        Name = name;
        Description = description;
        TargetP95 = targetP95;
        Latency.AddRange(latency);
    }

    /// <inheritdoc cref="KeyProperty"/>
    public string Key { get => GetValue(KeyProperty); set => SetValue(KeyProperty, value); }

    /// <inheritdoc cref="NameProperty"/>
    public string Name { get => GetValue(NameProperty); set => SetValue(NameProperty, value); }

    /// <inheritdoc cref="DescriptionProperty"/>
    public string? Description { get => GetValue(DescriptionProperty); set => SetValue(DescriptionProperty, value); }

    /// <inheritdoc cref="TargetP95Property"/>
    public double TargetP95 { get => GetValue(TargetP95Property); set => SetValue(TargetP95Property, value); }

    /// <summary>The samples, oldest first (48 half-hour points fill the chart).</summary>
    [Content]
    public AvaloniaList<LatencySample> Latency { get; } = new();

    /// <summary>The days of the window, oldest first (derived from the incidents).</summary>
    public IReadOnlyList<UptimeDay> Days => _days;

    /// <inheritdoc cref="UptimeProperty"/>
    public double Uptime => _uptime;

    /// <inheritdoc cref="CurrentStatusProperty"/>
    public ServiceStatus CurrentStatus => _currentStatus;

    internal void SetDerived(IReadOnlyList<UptimeDay> days, double uptime, ServiceStatus status)
    {
        _days = days;
        SetAndRaise(UptimeProperty, ref _uptime, uptime);
        SetAndRaise(CurrentStatusProperty, ref _currentStatus, status);
    }
}

/// <summary>Event data for a change of the selected service.</summary>
public sealed class UptimeServiceEventArgs : RoutedEventArgs
{
    internal UptimeServiceEventArgs(RoutedEvent routedEvent, UptimeService? service) : base(routedEvent)
    {
        Service = service;
    }

    /// <summary>The service now charted.</summary>
    public UptimeService? Service { get; }
}

/// <summary>Event data for a day being inspected in a service's bars.</summary>
public sealed class UptimeDayEventArgs : RoutedEventArgs
{
    internal UptimeDayEventArgs(RoutedEvent routedEvent, UptimeService service, UptimeDay? day, int index) : base(routedEvent)
    {
        Service = service;
        Day = day;
        Index = index;
    }

    /// <summary>The service whose bars are inspected.</summary>
    public UptimeService Service { get; }

    /// <summary>The day under the pointer or the keyboard cursor; null when the inspection ended.</summary>
    public UptimeDay? Day { get; }

    /// <summary>The day's index in the window (0 oldest), -1 when the inspection ended.</summary>
    public int Index { get; }
}

/// <summary>Event data for a latency sample being inspected in the chart.</summary>
public sealed class UptimePointEventArgs : RoutedEventArgs
{
    internal UptimePointEventArgs(RoutedEvent routedEvent, UptimeService? service, int index, LatencySample? sample) : base(routedEvent)
    {
        Service = service;
        Index = index;
        Sample = sample;
    }

    /// <summary>The charted service.</summary>
    public UptimeService? Service { get; }

    /// <summary>The sample's index (0 oldest), -1 when the inspection ended.</summary>
    public int Index { get; }

    /// <summary>The sample; null when the inspection ended.</summary>
    public LatencySample? Sample { get; }
}

/// <summary>Event data for an incident being opened or closed.</summary>
public sealed class UptimeIncidentEventArgs : RoutedEventArgs
{
    internal UptimeIncidentEventArgs(RoutedEvent routedEvent, UptimeIncident incident) : base(routedEvent)
    {
        Incident = incident;
    }

    /// <summary>The incident.</summary>
    public UptimeIncident Incident { get; }

    /// <summary>Whether its timeline is now open.</summary>
    public bool IsExpanded => Incident.IsExpanded;
}
