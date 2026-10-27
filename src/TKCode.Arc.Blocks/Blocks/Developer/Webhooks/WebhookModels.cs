using System.Collections.Specialized;
using System.ComponentModel;
using Avalonia;
using Avalonia.Collections;

namespace TKCode.Arc;

/// <summary>How an endpoint of <see cref="Webhooks"/> is doing, shown as the badge at the end of its row.</summary>
public enum WebhookHealth
{
    /// <summary>Nothing was sent to it yet.</summary>
    NoTraffic,

    /// <summary>Every delivery went through (or was retried until it did).</summary>
    Healthy,

    /// <summary>At least one delivery failed and was not retried yet.</summary>
    Failing
}

/// <summary>The tab of the delivery inspector.</summary>
public enum WebhookInspectorTab
{
    /// <summary>The request body (JSON, with line numbers).</summary>
    Payload,

    /// <summary>The request headers (name and value).</summary>
    Headers
}

/// <summary>The state of the "Add endpoint" panel.</summary>
public enum WebhookAddState
{
    /// <summary>The panel is a pill in the toolbar.</summary>
    Closed,

    /// <summary>The form is open.</summary>
    Editing,

    /// <summary>The form was sent and the block waits for <see cref="Webhooks.CreateEndpointAction"/>.</summary>
    Submitting,

    /// <summary>The endpoint was added and its signing secret is shown once.</summary>
    Revealing
}

/// <summary>What changed a selection of <see cref="Webhooks"/>.</summary>
public enum WebhookChangeSource
{
    /// <summary>A click or tap.</summary>
    Pointer,

    /// <summary>The arrow keys, Home or End.</summary>
    Keyboard,

    /// <summary>The block itself (a new endpoint, a retry that went through, the first endpoint shown).</summary>
    Auto,

    /// <summary>Code or a binding set the property.</summary>
    Programmatic
}

/// <summary>Why the "Add endpoint" panel closed.</summary>
public enum WebhookAddCloseReason
{
    /// <summary>The close button of the panel.</summary>
    Toggle,

    /// <summary>The Cancel button of the form.</summary>
    Cancel,

    /// <summary>Escape inside the panel.</summary>
    Escape,

    /// <summary>The Done button under the signing secret.</summary>
    Done,

    /// <summary><see cref="Webhooks.CloseAddPanel"/> or <see cref="Webhooks.IsAddPanelOpen"/>.</summary>
    Programmatic
}

/// <summary>A server action of <see cref="Webhooks"/>.</summary>
public enum WebhookActionKind
{
    /// <summary>Sending a failed delivery again.</summary>
    Retry,

    /// <summary>Adding an endpoint.</summary>
    CreateEndpoint,

    /// <summary>Sending a test event to an endpoint without deliveries.</summary>
    SendTest
}

/// <summary>What the inspector's Copy button copied.</summary>
public enum WebhookCopiedContent
{
    /// <summary>The request body.</summary>
    Payload,

    /// <summary>The request headers, one "Name: value" per line.</summary>
    Headers
}

/// <summary>A request header of a <see cref="WebhookDelivery"/>.</summary>
public class WebhookHeader : AvaloniaObject
{
    /// <summary>The header name ("Content-Type").</summary>
    public static readonly StyledProperty<string> NameProperty =
        AvaloniaProperty.Register<WebhookHeader, string>(nameof(Name), string.Empty);

    /// <summary>The header value.</summary>
    public static readonly StyledProperty<string> ValueProperty =
        AvaloniaProperty.Register<WebhookHeader, string>(nameof(Value), string.Empty);

    /// <summary>Creates an empty header.</summary>
    public WebhookHeader()
    {
    }

    /// <summary>Creates a header.</summary>
    /// <param name="name">The header name.</param>
    /// <param name="value">The header value.</param>
    public WebhookHeader(string name, string value)
    {
        Name = name;
        Value = value;
    }

    /// <inheritdoc cref="NameProperty"/>
    public string Name { get => GetValue(NameProperty); set => SetValue(NameProperty, value); }

    /// <inheritdoc cref="ValueProperty"/>
    public string Value { get => GetValue(ValueProperty); set => SetValue(ValueProperty, value); }
}

/// <summary>An event type the "Add endpoint" form offers as a checkbox.</summary>
public class WebhookEventType : AvaloniaObject
{
    /// <summary>The event name, shown in a monospaced font ("shipment.created").</summary>
    public static readonly StyledProperty<string> NameProperty =
        AvaloniaProperty.Register<WebhookEventType, string>(nameof(Name), string.Empty);

    /// <summary>A short line under the name saying when the event is sent.</summary>
    public static readonly StyledProperty<string?> DescriptionProperty =
        AvaloniaProperty.Register<WebhookEventType, string?>(nameof(Description));

    /// <summary>Whether the box is ticked when the form opens.</summary>
    public static readonly StyledProperty<bool> IsDefaultSelectedProperty =
        AvaloniaProperty.Register<WebhookEventType, bool>(nameof(IsDefaultSelected));

    /// <inheritdoc cref="NameProperty"/>
    public string Name { get => GetValue(NameProperty); set => SetValue(NameProperty, value); }

    /// <inheritdoc cref="DescriptionProperty"/>
    public string? Description { get => GetValue(DescriptionProperty); set => SetValue(DescriptionProperty, value); }

    /// <inheritdoc cref="IsDefaultSelectedProperty"/>
    public bool IsDefaultSelected { get => GetValue(IsDefaultSelectedProperty); set => SetValue(IsDefaultSelectedProperty, value); }
}

/// <summary>
/// One slice of an endpoint's traffic chart (Arc shows 48 half hours): how many events were sent in it and how many of
/// those still fail.
/// </summary>
public class WebhookVolumeBucket : AvaloniaObject
{
    /// <summary>Where the slice starts.</summary>
    public static readonly StyledProperty<DateTimeOffset> StartProperty =
        AvaloniaProperty.Register<WebhookVolumeBucket, DateTimeOffset>(nameof(Start));

    /// <summary>Where the slice ends.</summary>
    public static readonly StyledProperty<DateTimeOffset> EndProperty =
        AvaloniaProperty.Register<WebhookVolumeBucket, DateTimeOffset>(nameof(End));

    /// <summary>Events sent in the slice, failed ones included.</summary>
    public static readonly StyledProperty<int> TotalProperty =
        AvaloniaProperty.Register<WebhookVolumeBucket, int>(nameof(Total));

    /// <summary>Events of the slice that failed and were not delivered by a retry yet.</summary>
    public static readonly StyledProperty<int> FailedProperty =
        AvaloniaProperty.Register<WebhookVolumeBucket, int>(nameof(Failed));

    /// <inheritdoc cref="StartProperty"/>
    public DateTimeOffset Start { get => GetValue(StartProperty); set => SetValue(StartProperty, value); }

    /// <inheritdoc cref="EndProperty"/>
    public DateTimeOffset End { get => GetValue(EndProperty); set => SetValue(EndProperty, value); }

    /// <inheritdoc cref="TotalProperty"/>
    public int Total { get => GetValue(TotalProperty); set => SetValue(TotalProperty, value); }

    /// <inheritdoc cref="FailedProperty"/>
    public int Failed { get => GetValue(FailedProperty); set => SetValue(FailedProperty, value); }
}

/// <summary>One attempt to deliver an event to an endpoint, with what was sent and what came back.</summary>
public class WebhookDelivery : AvaloniaObject
{
    /// <summary>A key of your own for the delivery (the block never reads it).</summary>
    public static readonly StyledProperty<string?> KeyProperty =
        AvaloniaProperty.Register<WebhookDelivery, string?>(nameof(Key));

    /// <summary>The event type ("shipment.created").</summary>
    public static readonly StyledProperty<string> EventTypeProperty =
        AvaloniaProperty.Register<WebhookDelivery, string>(nameof(EventType), string.Empty);

    /// <summary>The event id shown under the inspector title ("evt_7Qm2Lx9Rd").</summary>
    public static readonly StyledProperty<string?> EventIdProperty =
        AvaloniaProperty.Register<WebhookDelivery, string?>(nameof(EventId));

    /// <summary>Which attempt this is (1 for the first send; a retry adds one).</summary>
    public static readonly StyledProperty<int> AttemptProperty =
        AvaloniaProperty.Register<WebhookDelivery, int>(nameof(Attempt), 1);

    /// <summary>Whether this is a test event (marked "Test" in the list).</summary>
    public static readonly StyledProperty<bool> IsTestProperty =
        AvaloniaProperty.Register<WebhookDelivery, bool>(nameof(IsTest));

    /// <summary>The HTTP status the endpoint answered with; 0 means it did not answer.</summary>
    public static readonly StyledProperty<int> StatusCodeProperty =
        AvaloniaProperty.Register<WebhookDelivery, int>(nameof(StatusCode), 200);

    /// <summary>The status text ("OK", "Internal Server Error"); empty shows a standard one for the code.</summary>
    public static readonly StyledProperty<string?> StatusTextProperty =
        AvaloniaProperty.Register<WebhookDelivery, string?>(nameof(StatusText));

    /// <summary>How long the endpoint took to answer.</summary>
    public static readonly StyledProperty<TimeSpan> DurationProperty =
        AvaloniaProperty.Register<WebhookDelivery, TimeSpan>(nameof(Duration));

    /// <summary>When the request was sent.</summary>
    public static readonly StyledProperty<DateTimeOffset> SentAtProperty =
        AvaloniaProperty.Register<WebhookDelivery, DateTimeOffset>(nameof(SentAt));

    /// <summary>The body the endpoint answered with, shown in one line under the response.</summary>
    public static readonly StyledProperty<string?> ResponseBodyProperty =
        AvaloniaProperty.Register<WebhookDelivery, string?>(nameof(ResponseBody));

    /// <summary>The request body; JSON is indented and colored, other text is shown as it is.</summary>
    public static readonly StyledProperty<string?> PayloadProperty =
        AvaloniaProperty.Register<WebhookDelivery, string?>(nameof(Payload));

    /// <summary>Whether a later retry of this failed delivery went through.</summary>
    public static readonly StyledProperty<bool> IsResolvedProperty =
        AvaloniaProperty.Register<WebhookDelivery, bool>(nameof(IsResolved));

    /// <summary>The later attempt that took over from this failed one (set by a retry, whatever its result).</summary>
    public static readonly StyledProperty<WebhookDelivery?> RetriedByProperty =
        AvaloniaProperty.Register<WebhookDelivery, WebhookDelivery?>(nameof(RetriedBy));

    /// <summary>Whether the endpoint did not accept the event (status 400 and up, or no answer).</summary>
    public static readonly DirectProperty<WebhookDelivery, bool> IsFailureProperty =
        AvaloniaProperty.RegisterDirect<WebhookDelivery, bool>(nameof(IsFailure), d => d.IsFailure);

    private bool _isFailure;

    /// <summary>Creates a delivery.</summary>
    public WebhookDelivery()
    {
        Headers = new AvaloniaList<WebhookHeader>();
    }

    /// <inheritdoc cref="KeyProperty"/>
    public string? Key { get => GetValue(KeyProperty); set => SetValue(KeyProperty, value); }

    /// <inheritdoc cref="EventTypeProperty"/>
    public string EventType { get => GetValue(EventTypeProperty); set => SetValue(EventTypeProperty, value); }

    /// <inheritdoc cref="EventIdProperty"/>
    public string? EventId { get => GetValue(EventIdProperty); set => SetValue(EventIdProperty, value); }

    /// <inheritdoc cref="AttemptProperty"/>
    public int Attempt { get => GetValue(AttemptProperty); set => SetValue(AttemptProperty, value); }

    /// <inheritdoc cref="IsTestProperty"/>
    public bool IsTest { get => GetValue(IsTestProperty); set => SetValue(IsTestProperty, value); }

    /// <inheritdoc cref="StatusCodeProperty"/>
    public int StatusCode { get => GetValue(StatusCodeProperty); set => SetValue(StatusCodeProperty, value); }

    /// <inheritdoc cref="StatusTextProperty"/>
    public string? StatusText { get => GetValue(StatusTextProperty); set => SetValue(StatusTextProperty, value); }

    /// <inheritdoc cref="DurationProperty"/>
    public TimeSpan Duration { get => GetValue(DurationProperty); set => SetValue(DurationProperty, value); }

    /// <inheritdoc cref="SentAtProperty"/>
    public DateTimeOffset SentAt { get => GetValue(SentAtProperty); set => SetValue(SentAtProperty, value); }

    /// <inheritdoc cref="ResponseBodyProperty"/>
    public string? ResponseBody { get => GetValue(ResponseBodyProperty); set => SetValue(ResponseBodyProperty, value); }

    /// <inheritdoc cref="PayloadProperty"/>
    public string? Payload { get => GetValue(PayloadProperty); set => SetValue(PayloadProperty, value); }

    /// <summary>The request headers, in order.</summary>
    public AvaloniaList<WebhookHeader> Headers { get; }

    /// <inheritdoc cref="IsResolvedProperty"/>
    public bool IsResolved { get => GetValue(IsResolvedProperty); set => SetValue(IsResolvedProperty, value); }

    /// <inheritdoc cref="RetriedByProperty"/>
    public WebhookDelivery? RetriedBy { get => GetValue(RetriedByProperty); set => SetValue(RetriedByProperty, value); }

    /// <inheritdoc cref="IsFailureProperty"/>
    public bool IsFailure { get => _isFailure; private set => SetAndRaise(IsFailureProperty, ref _isFailure, value); }

    /// <summary>Whether the delivery still counts as failing: it failed, no retry went through and no later attempt took over.</summary>
    public bool IsOpenFailure => IsFailure && !IsResolved && RetriedBy is null;

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == StatusCodeProperty)
        {
            IsFailure = WebhookMath.IsFailureCode(StatusCode);
        }
    }
}

/// <summary>
/// An endpoint of <see cref="Webhooks"/>: where events are sent, which ones, who added it, its traffic chart and its
/// recent deliveries. <see cref="DeliveredRate"/>, <see cref="FailingCount"/> and <see cref="Health"/> follow the
/// deliveries and the chart as they change.
/// </summary>
public class WebhookEndpoint : AvaloniaObject
{
    /// <summary>A key of your own for the endpoint (the block never reads it).</summary>
    public static readonly StyledProperty<string?> KeyProperty =
        AvaloniaProperty.Register<WebhookEndpoint, string?>(nameof(Key));

    /// <summary>The name; empty shows the URL's host.</summary>
    public static readonly StyledProperty<string?> NameProperty =
        AvaloniaProperty.Register<WebhookEndpoint, string?>(nameof(Name));

    /// <summary>The URL events are posted to.</summary>
    public static readonly StyledProperty<string> UrlProperty =
        AvaloniaProperty.Register<WebhookEndpoint, string>(nameof(Url), string.Empty);

    /// <summary>Who added the endpoint (shown with a generated avatar).</summary>
    public static readonly StyledProperty<string?> AddedByProperty =
        AvaloniaProperty.Register<WebhookEndpoint, string?>(nameof(AddedBy));

    /// <summary>When the endpoint was added.</summary>
    public static readonly StyledProperty<DateTimeOffset?> AddedOnProperty =
        AvaloniaProperty.Register<WebhookEndpoint, DateTimeOffset?>(nameof(AddedOn));

    /// <summary>A masked hint of the signing secret ("sig_••••c41e"); the secret itself never reaches the block.</summary>
    public static readonly StyledProperty<string?> SecretHintProperty =
        AvaloniaProperty.Register<WebhookEndpoint, string?>(nameof(SecretHint));

    /// <summary>The payload version the endpoint receives ("Version 2026.09").</summary>
    public static readonly StyledProperty<string?> ApiVersionProperty =
        AvaloniaProperty.Register<WebhookEndpoint, string?>(nameof(ApiVersion));

    /// <summary>The share of events that went through, from 0 to 1 (rounded down to a tenth of a percent); null when nothing was sent.</summary>
    public static readonly DirectProperty<WebhookEndpoint, double?> DeliveredRateProperty =
        AvaloniaProperty.RegisterDirect<WebhookEndpoint, double?>(nameof(DeliveredRate), e => e.DeliveredRate);

    /// <summary>How many deliveries still fail (failed, not delivered by a retry, not taken over by a later attempt).</summary>
    public static readonly DirectProperty<WebhookEndpoint, int> FailingCountProperty =
        AvaloniaProperty.RegisterDirect<WebhookEndpoint, int>(nameof(FailingCount), e => e.FailingCount);

    /// <summary>How the endpoint is doing.</summary>
    public static readonly DirectProperty<WebhookEndpoint, WebhookHealth> HealthProperty =
        AvaloniaProperty.RegisterDirect<WebhookEndpoint, WebhookHealth>(nameof(Health), e => e.Health);

    private double? _deliveredRate;
    private int _failingCount;
    private WebhookHealth _health;
    private readonly HashSet<WebhookDelivery> _watchedDeliveries = new();
    private readonly HashSet<WebhookVolumeBucket> _watchedBuckets = new();

    /// <summary>Creates an endpoint.</summary>
    public WebhookEndpoint()
    {
        Events = new AvaloniaList<string>();
        Volume = new AvaloniaList<WebhookVolumeBucket>();
        Deliveries = new AvaloniaList<WebhookDelivery>();
        Volume.CollectionChanged += OnVolumeChanged;
        Deliveries.CollectionChanged += OnDeliveriesChanged;
    }

    /// <inheritdoc cref="KeyProperty"/>
    public string? Key { get => GetValue(KeyProperty); set => SetValue(KeyProperty, value); }

    /// <inheritdoc cref="NameProperty"/>
    public string? Name { get => GetValue(NameProperty); set => SetValue(NameProperty, value); }

    /// <inheritdoc cref="UrlProperty"/>
    public string Url { get => GetValue(UrlProperty); set => SetValue(UrlProperty, value); }

    /// <inheritdoc cref="AddedByProperty"/>
    public string? AddedBy { get => GetValue(AddedByProperty); set => SetValue(AddedByProperty, value); }

    /// <inheritdoc cref="AddedOnProperty"/>
    public DateTimeOffset? AddedOn { get => GetValue(AddedOnProperty); set => SetValue(AddedOnProperty, value); }

    /// <inheritdoc cref="SecretHintProperty"/>
    public string? SecretHint { get => GetValue(SecretHintProperty); set => SetValue(SecretHintProperty, value); }

    /// <inheritdoc cref="ApiVersionProperty"/>
    public string? ApiVersion { get => GetValue(ApiVersionProperty); set => SetValue(ApiVersionProperty, value); }

    /// <summary>The event types the endpoint subscribes to (the row shows up to three and "+N").</summary>
    public AvaloniaList<string> Events { get; }

    /// <summary>The traffic chart, oldest slice first.</summary>
    public AvaloniaList<WebhookVolumeBucket> Volume { get; }

    /// <summary>The recent deliveries, newest first.</summary>
    public AvaloniaList<WebhookDelivery> Deliveries { get; }

    /// <inheritdoc cref="DeliveredRateProperty"/>
    public double? DeliveredRate { get => _deliveredRate; private set => SetAndRaise(DeliveredRateProperty, ref _deliveredRate, value); }

    /// <inheritdoc cref="FailingCountProperty"/>
    public int FailingCount { get => _failingCount; private set => SetAndRaise(FailingCountProperty, ref _failingCount, value); }

    /// <inheritdoc cref="HealthProperty"/>
    public WebhookHealth Health { get => _health; private set => SetAndRaise(HealthProperty, ref _health, value); }

    /// <summary>The host of <see cref="Url"/> ("relay.tallowfield.dev"), or the URL itself when it has none.</summary>
    public string Host => WebhookMath.HostOf(Url);

    /// <summary>The name shown for the endpoint: <see cref="Name"/>, or the host when it is empty.</summary>
    public string DisplayName => string.IsNullOrWhiteSpace(Name) ? Host : Name!;

    /// <summary>Raised after the rate, the failing count or the health changed (the block also listens to the properties).</summary>
    internal event EventHandler? StatsChanged;

    private void OnDeliveriesChanged(object? sender, NotifyCollectionChangedEventArgs e) =>
        Rewatch(Deliveries, _watchedDeliveries, OnDeliveryChanged);

    private void OnVolumeChanged(object? sender, NotifyCollectionChangedEventArgs e) =>
        Rewatch(Volume, _watchedBuckets, OnBucketChanged);

    private void Rewatch<T>(IEnumerable<T> items, HashSet<T> watched, EventHandler<AvaloniaPropertyChangedEventArgs> handler)
        where T : AvaloniaObject
    {
        var current = new HashSet<T>(items);
        foreach (var gone in watched.Where(w => !current.Contains(w)).ToList())
        {
            gone.PropertyChanged -= handler;
            watched.Remove(gone);
        }
        foreach (var added in current.Where(c => !watched.Contains(c)))
        {
            added.PropertyChanged += handler;
            watched.Add(added);
        }
        Recompute();
    }

    private void OnDeliveryChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == WebhookDelivery.StatusCodeProperty || e.Property == WebhookDelivery.IsResolvedProperty ||
            e.Property == WebhookDelivery.RetriedByProperty)
        {
            Recompute();
        }
    }

    private void OnBucketChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == WebhookVolumeBucket.TotalProperty || e.Property == WebhookVolumeBucket.FailedProperty)
        {
            Recompute();
        }
    }

    private void Recompute()
    {
        var failing = 0;
        foreach (var delivery in Deliveries)
        {
            if (delivery.IsOpenFailure)
            {
                failing++;
            }
        }
        var (total, failed) = WebhookMath.Sum(Volume);
        var rate = WebhookMath.Rate(total, failed, Deliveries.Count, failing);
        var health = failing > 0 ? WebhookHealth.Failing
            : Deliveries.Count == 0 && total == 0 ? WebhookHealth.NoTraffic
            : WebhookHealth.Healthy;
        var changed = rate != DeliveredRate || failing != FailingCount || health != Health;
        DeliveredRate = rate;
        FailingCount = failing;
        Health = health;
        if (changed)
        {
            StatsChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == UrlProperty || change.Property == NameProperty)
        {
            StatsChanged?.Invoke(this, EventArgs.Empty);
        }
    }
}

/// <summary>What the "Add endpoint" form sends to <see cref="Webhooks.CreateEndpointAction"/>.</summary>
public sealed class WebhookEndpointDraft
{
    /// <summary>Creates a draft.</summary>
    /// <param name="url">The URL (trimmed, already validated).</param>
    /// <param name="description">The optional description (null when empty).</param>
    /// <param name="events">The ticked event types, in the form's order.</param>
    public WebhookEndpointDraft(string url, string? description, IReadOnlyList<string> events)
    {
        Url = url;
        Description = description;
        Events = events;
    }

    /// <summary>The URL (trimmed, already validated).</summary>
    public string Url { get; }

    /// <summary>The optional description; the block uses it as the name, or the host when it is null.</summary>
    public string? Description { get; }

    /// <summary>The ticked event types, in the form's order.</summary>
    public IReadOnlyList<string> Events { get; }
}

/// <summary>What <see cref="Webhooks.CreateEndpointAction"/> returns: the new endpoint and its signing secret.</summary>
public sealed class WebhookEndpointCreated
{
    /// <summary>Creates the result.</summary>
    /// <param name="endpoint">The endpoint to add at the top of the list.</param>
    /// <param name="secret">The signing secret, shown once and then dropped.</param>
    public WebhookEndpointCreated(WebhookEndpoint endpoint, string secret)
    {
        Endpoint = endpoint;
        Secret = secret;
    }

    /// <summary>The endpoint to add at the top of the list.</summary>
    public WebhookEndpoint Endpoint { get; }

    /// <summary>The signing secret: shown once in the panel and dropped when it closes (never stored on the endpoint).</summary>
    public string Secret { get; }
}
