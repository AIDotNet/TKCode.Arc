namespace TKCode.Arc;

/// <summary>
/// Every word <see cref="Webhooks"/> shows or announces, in English by default. Replace the instance to translate or
/// reword; texts with values are functions so that word order and plural forms can change.
/// </summary>
public sealed class WebhooksLabels
{
    // ---- Header and toolbar ----

    /// <summary>The title, used while <see cref="Webhooks.Title"/> is null.</summary>
    public string Title { get; init; } = "Webhooks";

    /// <summary>The word after the endpoint count ("endpoint" / "endpoints"); the argument is the count.</summary>
    public Func<int, string> EndpointsWord { get; init; } = count => count == 1 ? "endpoint" : "endpoints";

    /// <summary>The note after the count while some endpoints have failing deliveries; the argument is how many endpoints.</summary>
    public Func<int, string> FailingNote { get; init; } = count => $"{count} failing";

    /// <summary>The note after the count while no endpoint has failing deliveries.</summary>
    public string HealthyNote { get; init; } = "all healthy";

    /// <summary>The pill that opens the form, and the form's submit button.</summary>
    public string AddEndpoint { get; init; } = "Add endpoint";

    /// <summary>The panel title while the form is open.</summary>
    public string NewEndpoint { get; init; } = "New endpoint";

    /// <summary>The panel title once the endpoint was added.</summary>
    public string EndpointReady { get; init; } = "Endpoint ready";

    /// <summary>The accessible name of the panel's toggle while open.</summary>
    public string Close { get; init; } = "Close";

    // ---- Endpoint rows ----

    /// <summary>The accessible name of the endpoint list.</summary>
    public string EndpointsListName { get; init; } = "Endpoints";

    /// <summary>The small word under an endpoint's rate.</summary>
    public string RateCaption { get; init; } = "success";

    /// <summary>The badge of an endpoint with failing deliveries; the argument is how many.</summary>
    public Func<int, string> FailingBadge { get; init; } = count => $"{count} failing";

    /// <summary>The badge of an endpoint whose deliveries all went through.</summary>
    public string HealthyBadge { get; init; } = "Healthy";

    /// <summary>The badge of an endpoint nothing was sent to yet.</summary>
    public string NoTrafficBadge { get; init; } = "No traffic";

    /// <summary>
    /// The accessible name of an endpoint row; the arguments are the name, the host, the rate text ("98.9%" or "–"), the
    /// badge text and the subscribed events joined with commas.
    /// </summary>
    public Func<string, string, string, string, string, string> EndpointRowName { get; init; } =
        (name, host, rate, badge, events) => $"{name}, {host}. {rate} success, {badge}. Events: {events}";

    // ---- Detail and chart ----

    /// <summary>Who added the endpoint and when; the arguments are the name and the formatted date.</summary>
    public Func<string, string, string> AddedBy { get; init; } = (name, date) => $"Added {date} by {name}";

    /// <summary>When the endpoint was added, when nobody is named; the argument is the formatted date.</summary>
    public Func<string, string> AddedOn { get; init; } = date => $"Added {date}";

    /// <summary>The format of the date in the detail line (standard .NET date format).</summary>
    public string DateFormat { get; init; } = "MMM d, yyyy";

    /// <summary>The events in the chart; the argument is the count.</summary>
    public Func<int, string> ChartEvents { get; init; } = count => count == 1 ? "1 event" : $"{count:N0} events";

    /// <summary>The failed legend of the chart; the argument is the count.</summary>
    public Func<int, string> ChartFailed { get; init; } = count => $"{count:N0} failed";

    /// <summary>The chart's note while nothing fails.</summary>
    public string NoFailures { get; init; } = "no failures";

    /// <summary>The chart's title while nothing was sent.</summary>
    public string NoTrafficYet { get; init; } = "No traffic yet";

    /// <summary>The time range of a hovered bar; the arguments are its start and end ("11:00 to 11:30").</summary>
    public Func<string, string, string> BucketRange { get; init; } = (start, end) => $"{start} to {end}";

    /// <summary>The last label under the chart.</summary>
    public string AxisNow { get; init; } = "Now";

    /// <summary>
    /// The chart's accessible description; the arguments are the event count, the failing count and the busiest range
    /// with its count (empty when there is no traffic).
    /// </summary>
    public Func<int, int, string, int, string> ChartSummary { get; init; } = (total, failed, busiest, busiestCount) =>
        total == 0
            ? "No traffic in this period."
            : $"{total:N0} events, {failed:N0} still failing. Busiest: {busiest} with {busiestCount:N0} events.";

    // ---- Deliveries ----

    /// <summary>The heading of the delivery list.</summary>
    public string RecentDeliveries { get; init; } = "Recent deliveries";

    /// <summary>The accessible name of the delivery list; the argument is the endpoint's name.</summary>
    public Func<string, string> DeliveriesListName { get; init; } = name => $"Recent deliveries to {name}";

    /// <summary>The note after a retried delivery's event; the argument is the attempt.</summary>
    public Func<int, string> TryLabel { get; init; } = attempt => $"Try {attempt}";

    /// <summary>The note after a test delivery's event.</summary>
    public string TestLabel { get; init; } = "Test";

    /// <summary>The button that sends a failed delivery again.</summary>
    public string Retry { get; init; } = "Retry";

    /// <summary>The retry button while it waits.</summary>
    public string Retrying { get; init; } = "Resending";

    /// <summary>What replaces the retry button once a retry went through.</summary>
    public string Resolved { get; init; } = "Resolved";

    /// <summary>What replaces the retry button once a later attempt took over (and failed too).</summary>
    public string Retried { get; init; } = "Retried";

    /// <summary>The accessible name of a retry button; the arguments are the event and the time it was sent.</summary>
    public Func<string, string, string> RetryName { get; init; } = (eventType, time) => $"Retry {eventType} sent at {time}";

    /// <summary>The accessible name of a retry button while it waits; the argument is the event.</summary>
    public Func<string, string> RetryingName { get; init; } = eventType => $"Resending {eventType}";

    /// <summary>
    /// The accessible name of a delivery row; the arguments are the status code, the status text, the event, the
    /// duration and the time.
    /// </summary>
    public Func<string, string, string, string, string, string> DeliveryRowName { get; init; } =
        (code, status, eventType, duration, time) => $"{code} {status}, {eventType}, {duration}, sent at {time}";

    /// <summary>Added to a delivery row's name once a retry went through.</summary>
    public string ResolvedSuffix { get; init; } = ", resolved by a retry";

    /// <summary>The status text of a delivery that got no answer.</summary>
    public string NoResponse { get; init; } = "No response";

    /// <summary>The title of the empty delivery list.</summary>
    public string EmptyTitle { get; init; } = "No deliveries yet";

    /// <summary>The line under it; the argument is the endpoint's host.</summary>
    public Func<string, string> EmptyHint { get; init; } = host => $"Send a test event to confirm {host} replies with a 2xx status.";

    /// <summary>The button of the empty delivery list.</summary>
    public string SendTest { get; init; } = "Send test event";

    /// <summary>That button while it waits.</summary>
    public string SendingTest { get; init; } = "Sending";

    // ---- Inspector ----

    /// <summary>The inspector's line under the event; the arguments are the event id and the attempt.</summary>
    public Func<string, int, string> InspectorSubtitle { get; init; } = (id, attempt) => $"{id} · try {attempt}";

    /// <summary>The label before the response status.</summary>
    public string Response { get; init; } = "Response";

    /// <summary>The response line after the status badge; the arguments are the status text and the duration.</summary>
    public Func<string, string, string> ResponseLine { get; init; } = (status, duration) => $"{status} in {duration}";

    /// <summary>The first tab.</summary>
    public string Payload { get; init; } = "Payload";

    /// <summary>The second tab.</summary>
    public string Headers { get; init; } = "Headers";

    /// <summary>The accessible name of the tabs.</summary>
    public string TabsName { get; init; } = "Request details";

    /// <summary>The inspector's Copy button.</summary>
    public string Copy { get; init; } = "Copy";

    /// <summary>The Copy button once it copied.</summary>
    public string Copied { get; init; } = "Copied";

    /// <summary>What the inspector shows while no delivery is selected.</summary>
    public string InspectorPlaceholder { get; init; } = "Pick a delivery to see its body and headers.";

    // ---- Form ----

    /// <summary>The URL field's label.</summary>
    public string UrlLabel { get; init; } = "Endpoint URL";

    /// <summary>The URL field's placeholder.</summary>
    public string UrlPlaceholder { get; init; } = "https://relay.yourdomain.dev/hooks";

    /// <summary>The description field's label.</summary>
    public string DescriptionLabel { get; init; } = "Description";

    /// <summary>The description field's placeholder.</summary>
    public string DescriptionPlaceholder { get; init; } = "Inventory mirror";

    /// <summary>The line under the description field.</summary>
    public string Optional { get; init; } = "Optional";

    /// <summary>The heading of the event checkboxes.</summary>
    public string EventsLegend { get; init; } = "Events";

    /// <summary>The count of ticked events; the argument is the count.</summary>
    public Func<int, string> EventsSelected { get; init; } = count => count == 1 ? "1 event selected" : $"{count} events selected";

    /// <summary>The form's cancel button.</summary>
    public string Cancel { get; init; } = "Cancel";

    /// <summary>The submit button while it waits.</summary>
    public string Saving { get; init; } = "Saving";

    /// <summary>The URL error while the field is empty.</summary>
    public string UrlRequired { get; init; } = "Add the URL to send events to";

    /// <summary>The URL error while it is not https on a public host.</summary>
    public string UrlInvalid { get; init; } = "Use an https:// address on a public host";

    /// <summary>The error while no event is ticked.</summary>
    public string EventsRequired { get; init; } = "Pick at least one event";

    /// <summary>The form's error when the endpoint could not be added; the argument is the reason.</summary>
    public Func<string, string> CreateFailed { get; init; } = reason => $"The endpoint was not added. {reason}";

    /// <summary>The label of the one-time secret.</summary>
    public string SigningSecret { get; init; } = "Signing secret";

    /// <summary>The warning under the one-time secret.</summary>
    public string SecretNotice { get; init; } = "Copy it now. It will not be shown again, but you can rotate it later.";

    /// <summary>The button that closes the panel after the secret.</summary>
    public string Done { get; init; } = "Done";

    // ---- Announcements ----

    /// <summary>A retry went through; the arguments are the event, the attempt, the host, the status code and the duration.</summary>
    public Func<string, int, string, string, string, string> RetrySucceeded { get; init; } =
        (eventType, attempt, host, code, duration) => $"{eventType} went through on try {attempt}. {host} replied {code} in {duration}.";

    /// <summary>A retry failed; the arguments are the event, the attempt and the reason.</summary>
    public Func<string, int, string, string> RetryFailed { get; init; } =
        (eventType, attempt, reason) => $"{eventType} failed again on try {attempt}. {reason}";

    /// <summary>An endpoint was added; the argument is its name.</summary>
    public Func<string, string> EndpointAdded { get; init; } = name => $"{name} added. Copy the signing secret now.";

    /// <summary>A test event went through; the arguments are the event, the host and the status code.</summary>
    public Func<string, string, string, string> TestSent { get; init; } =
        (eventType, host, code) => $"Test {eventType} sent. {host} replied {code}.";

    /// <summary>A test event failed; the arguments are the host and the reason.</summary>
    public Func<string, string, string> TestFailed { get; init; } = (host, reason) => $"The test event to {host} failed. {reason}";

    /// <summary>Something was copied; the argument is the tab name.</summary>
    public Func<string, string> CopiedAnnouncement { get; init; } = what => $"{what} copied";
}
