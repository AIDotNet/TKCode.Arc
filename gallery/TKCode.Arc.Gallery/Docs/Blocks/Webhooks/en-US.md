---
title: Webhooks
summary: A webhooks console where you check each endpoint's health and traffic, inspect what was sent and what came back, retry failed deliveries in place and add endpoints with a one-time signing secret.
---

<Demo name="Preview" />

## When to use

`Webhooks` is the page of a developer settings area where people manage the URLs your product posts events to. From top
to bottom it shows the endpoints (each with the events it subscribes to, the share of events that went through and a
health badge), the selected endpoint's details with a chart of the last day's traffic, its recent deliveries, and an
inspector for the delivery picked in the list: the response, the request body and the request headers. A failed delivery
is sent again in place with Retry, and "Add endpoint" unfolds into a form that shows the new endpoint's signing secret
once.

- **A product with outgoing webhooks.** Payments, shipping, ticketing or scheduling services that notify their
  customers' servers, where support and developers need to see why an endpoint stopped answering.
- **An integration's diagnostics page.** One endpoint and its traffic, with a test event to check the round trip
  before going live.
- **An internal event bus console.** Teams that route events to internal services can watch the failing ones and resend
  them after a fix.

## When not to use

- **Thousands of deliveries.** The list shows the recent ones without virtualization; use a `DataGrid` with paging and
  filters for a full delivery history.
- **Editing endpoints.** The block adds endpoints and shows them; renaming, pausing, deleting and rotating secrets belong
  in a settings page of your own (the signing secret is only ever shown at creation).
- **A plain list of URLs.** When there is no traffic to show, a table or a list of cards is simpler.

## Usage

### Endpoints and deliveries

Each `WebhookEndpoint` has a `Name` (the host is shown when it is empty), the `Url`, the `Events` it subscribes to, who
added it and when (`AddedBy`, `AddedOn`; the person gets a generated avatar), a masked `SecretHint`, an `ApiVersion`, its
traffic as `Volume` buckets (oldest first; 48 half hours make a 24-hour chart) and its recent `Deliveries` (newest
first). `DeliveredRate`, `FailingCount` and `Health` follow the deliveries and the chart as they change: the rate counts
the chart's events that did not fail, rounded down to a tenth of a percent, and an endpoint is failing while one of its
deliveries failed and was not retried yet.

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AIDotNet/TKCode.Arc">
    <arc:Webhooks Description="Corvid signs each event and sends it to your endpoints.">
        <arc:WebhookEndpoint Name="Shipping sync"
                             Url="https://relay.tallowfield.dev/hooks/corvid/v2"
                             AddedBy="Lena Okafor"
                             SecretHint="sig_••••c41e"
                             ApiVersion="Version 2026.09" />
    </arc:Webhooks>
</UserControl>
```

A `WebhookDelivery` holds the `EventType` and `EventId`, the `Attempt`, the `StatusCode` (0 means the endpoint did not
answer) and `StatusText`, the `Duration`, `SentAt`, the `ResponseBody`, the request `Payload` (JSON is indented and
colored, other text is shown as it is) and its `Headers`. Use `EndpointsSource` for a collection you own: an
`ObservableCollection` is observed and new endpoints are inserted at its top.

`SelectedEndpoint`, `SelectedDelivery` and `InspectorTab` are two-way. Selecting an endpoint picks its first failing
delivery, or its newest one; the inspector's tab stays when the delivery changes.

### Retries, new endpoints and test events

The three server actions are asynchronous delegates. Without them the block simulates a server that always succeeds, with
fixed delays (1.1 s, 0.76 s and 0.9 s), which is what the preview does.

- `RetryAction` sends a failed delivery again and returns the new attempt. It is added at the top of the list with a
  short flash; when it went through, the original is marked resolved, the endpoint's rate and badge update, and the
  selection moves to the new attempt. When the delegate throws (or returns a failed attempt), a failed attempt with its
  own Retry button is added instead, the original shows "Retried", and the failure is announced.
- `CreateEndpointAction` receives the form as a `WebhookEndpointDraft` and returns the endpoint with its signing secret
  (`WebhookEndpointCreated`). The endpoint is added at the top of the list, flashing until the panel closes, and the
  secret is shown once with a Copy button; it is dropped when the panel closes and never stored on the endpoint. When the
  delegate throws, the form stays open with the message above its buttons. Escape while it waits cancels the request.
- `SendTestAction` is called by the "Send test event" button that an endpoint without deliveries shows. A failed test is
  added as a failed delivery.

`AppendDelivery` adds a delivery your server reported, for a live feed, and counts it in the last slice of the chart.

```csharp
var client = new HttpClient();
hooks.RetryAction = async (delivery, cancellation) =>
{
    var started = DateTimeOffset.Now;
    using var response = await client.PostAsync(
        $"https://api.example.dev/deliveries/{delivery.EventId}/resend", null, cancellation);
    return new WebhookDelivery
    {
        EventType = delivery.EventType,
        EventId = delivery.EventId,
        Attempt = delivery.Attempt + 1,
        StatusCode = (int)response.StatusCode,
        Duration = DateTimeOffset.Now - started,
        SentAt = started,
        ResponseBody = await response.Content.ReadAsStringAsync(cancellation),
        Payload = delivery.Payload,
    };
};
```

<Demo name="Failing" />

### A new endpoint

An endpoint without deliveries shows a dash for its rate, "No traffic" as its badge, an empty chart and the test button.
`EventTypes` lists the checkboxes of the form; `IsDefaultSelected` ticks one when the form opens (the first one otherwise).
`UrlValidator` replaces the built-in URL check (required, https, not localhost). `CurrentUser` names who adds endpoints
when the block simulates the server.

<Demo name="NewEndpoint" />

### Narrow containers

The layout follows the block's own width. At 880 and below the inspector moves under the deliveries; at 760 and below the
endpoint rows keep the name, the rate and the badge; at 540 and below the paddings tighten and the toolbar note hides. The
form takes two columns of events at 800, and one column for everything at 560. A delivery list 460 wide or less puts the
duration and the time under the event, and narrow headers put each name above its value.

<Demo name="Compact" />

### Events and bound state

Every change is a bubbling routed event with what changed: `EndpointSelectionChanged` and `DeliverySelectionChanged`
(with the old and new item and whether the pointer, the keyboard or the block changed it), `RetryStarted`,
`RetryCompleted` (with the old and new rate), `ActionFailed`, `EndpointCreated`, `TestEventSent`, `ContentCopied`,
`AddPanelOpened` and `AddPanelClosed` (with the reason). `RetryCommand`, `SendTestCommand` and `EndpointCreatedCommand`
run alongside. `OpenAddPanel`, `CloseAddPanel`, `RetryAsync` and `SendTestAsync` do from code what the buttons do; every
text is in `Labels`.

<Demo name="EventLog" />

## API reference

<!-- api: Webhooks -->

<!-- api: WebhookEndpoint -->

<!-- api: WebhookDelivery -->

<!-- api: WebhookVolumeBucket -->

<!-- api: WebhookEventType -->

<!-- api: WebhookHeader -->

<!-- api: WebhooksLabels -->

## Accessibility

- **Tab** goes through the "Add endpoint" pill, each endpoint row, each delivery row and its Retry button, the Copy
  button, the selected tab and the payload panel.
- **Up** and **Down** on an endpoint or a delivery row move to the previous or next row and select it; **Home** and **End**
  go to the first and the last. They do not wrap, and they skip the Retry buttons.
- **Left** and **Right** on the tabs switch between Payload and Headers; the payload panel scrolls with the arrow keys.
- **Enter** or **Space** selects a row or presses a button. In the form, **Enter** in a field submits it.
- **Escape** inside the panel closes it and returns the focus to the pill; while the form waits for the server, it cancels
  the request instead.
- The endpoints and the deliveries are lists with names ("Endpoints", "Recent deliveries to Shipping sync"). An endpoint
  row reads its name, host, rate, badge and events; a delivery row reads its status, event, duration and time, and
  whether a retry resolved it. A Retry button names the event and the time it was sent, and says "Resending" while it
  waits.
- The chart is a picture described in one sentence: the events, how many still fail and the busiest half hour.
- Results are announced politely: a retry that went through or failed, a new endpoint, a test event, a copy, a form
  error.
- The focus never drops: after a retry it moves from the Retry button to the new attempt, after a test event to the
  test delivery, after the panel closes to the pill, and when the endpoint changes while a delivery row has the focus, to
  the selected delivery of the new endpoint. A failed create keeps it on the submit button.
- Rings show only for keyboard focus; on rows they take the shape of the selection plate.

## Motion

- **Selecting**: the selection plate glides to the new row on a critically damped spring (about 550 ms) and follows rows
  that move under it. The endpoint's name, its details and the chart's title swap with a short blur; the bars spring to
  their new heights one after the other (4.2 ms apart, about 200 ms from the first to the last). The lower half
  crossfades, the new one rising 6 px; the old one leaves the layout at once, so nothing under it jumps.
- **Inspecting**: the title, the response and the panel swap with a short blur. The old response is lifted out of the
  layout while it fades, so the tabs never move during the swap.
- **Retrying**: the button shows a spinner and "Resending" while its width springs to the new label. The new attempt's
  row grows in (Smooth spring, opacity after 80 ms) with a flash that holds 1.4 s and fades over 1.2 s; "Resolved" blurs in
  where Retry was; the changed digits of the rate swap one slot at a time, right to left, 30 ms apart; about 500 ms later
  the selection follows.
- **Adding an endpoint**: the pill grows into the full-width panel on the same spring while the plus turns into a close
  cross, and the form fades in once the panel is half open. The secret develops out of a soft blur. The new row grows in
  at the top and keeps its flash until the panel closes.
- **Hovering the chart**: the bar under the pointer brightens and the others dim to 45% in 160 ms; the title shows that
  half hour at once.
- Reduced motion keeps only short fades: the plates, the bars, the rows and the panel move at once and the digits change
  in place. Off applies every change at once.

## Related

- API keys, Integrations and Env variables, the other Developer blocks.
- `Badge`, `Button`, `CopyButton`, `Input`, `Checkbox` and `SegmentedControl`, the controls the block is made of.
- `JsonViewer` for exploring a large JSON document as a tree.
