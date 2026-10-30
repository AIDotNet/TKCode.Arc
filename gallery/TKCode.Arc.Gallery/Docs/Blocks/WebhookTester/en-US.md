---
title: Webhook tester
summary: A webhook tester where you pick an event, edit its JSON payload and send a signed test event, watch it go from signed to sent to the endpoint's answer, read the response and retry failed deliveries from a recent list.
---

<Demo name="Preview" />

## When to use

`WebhookTester` is the "Test endpoint" screen of a product that sends webhooks (billing, auth, email, CI). A person picks
an event, adjusts its JSON payload and sends a signed test event; the block shows it going from signed to sent to the
answer, and keeps the last attempts in a list.

- **Testing before going live.** Customers prove that their handler answers with a 2xx and that it checks the signature.
- **Integration onboarding.** The block signs with the endpoint's secret in the app, so what the handler receives is what
  it will receive in production.
- **Replaying failed deliveries.** A failed row has a Retry button that sends the same body again under a fresh timestamp
  and signature.

## When not to use

- **Managing many endpoints, filters and event subscriptions.** Use the Webhooks block for that and link here to test one
  endpoint.
- **A general HTTP client.** The block always sends a signed POST with a JSON body; it has no method or header editor.
- **Event logs with thousands of rows.** The list keeps the last 20 attempts. A paginated log belongs in a data table.

## Usage

The block derives from Avalonia's `TemplatedControl`. Give it the endpoint, the events a person can pick (a type and a
default JSON payload each) and the endpoint's signing secret.

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:arc="https://github.com/AIDotNet/TKCode.Arc">
    <arc:WebhookTester x:Name="Tester"
                       Endpoint="https://api.acme.dev/webhooks/billing"
                       Secret="{Binding SigningSecret}" />
</UserControl>
```

```csharp
Tester.Events.Add(new WebhookTestEvent("invoice.paid", """{"id":"evt_1","type":"invoice.paid","amount":4900}"""));
Tester.Events.Add(new WebhookTestEvent("invoice.failed", """{"id":"evt_2","type":"invoice.failed","amount":4900}"""));
```

While `Events` is empty the block offers five sample events. Choosing an event loads its payload: valid JSON is pretty
printed, anything else is kept as is. `SelectedEvent` and `Payload` are two-way, so a page can read or preset them.

### Sending on your server

The block signs the body in the app (HMAC SHA-256 of `timestamp.body`, sent as `t=…,v1=…` in `SignatureHeader`) but never
posts to your customer's endpoint itself: a browser or a desktop app should not call an arbitrary URL on a customer's
behalf. `SendAction` receives the signed `WebhookTestRequest` and passes it to your server, which does the real POST and
returns a `WebhookTestResponse`. The server can sign the same body again and check that the signatures are identical.

```csharp
Tester.SendAction = async (request, cancellation) =>
{
    var result = await api.PostAsJsonAsync($"endpoints/{endpointId}/test", request, cancellation);
    return await result.Content.ReadFromJsonAsync<WebhookTestResponse>(cancellation)
           ?? new WebhookTestResponse { Error = "Empty answer" };
};
```

A `Status` of 0 with `Error` (and `TimedOut` when the endpoint ran out of time) means no answer arrived; an exception counts
as a failed delivery too. Either way the delivery joins the list as failed, the timeline ends in red and the failure is
announced. `RetryAction` sends a failed delivery again; without it Retry uses `SendAction` with a fresh signature.

<Demo name="Failing" />

Without a `SendAction` every delivery is simulated: a 200 after `SimulatedLatency`, and the announcements say that nothing
was sent. With `AutoPlay` (default) the simulated block sends one event about a second after it scrolls half into view,
unless the person has already used it. Call `SendAsync` and `RetryAsync` to do the same from code.

### Other products

`Source` names the product that sends the events (the subtitle reads "Signed test events from Relay Mail") and can supply
its own 16 px mark with `MarkFactory`; without one the block draws a rounded square with the first letter. Set
`SignatureHeader` to the header your product uses.

<Demo name="OtherProduct" />

### The payload editor

The editor tints JSON as it is typed, shows line numbers, and says in words whether the payload parses and how many bytes
it is ("Valid JSON · 327 bytes"; when it does not parse, the line and the problem). Enter keeps the indentation and indents
after `{` or `[`. **Format** pretty prints valid JSON. A payload that does not parse cannot be sent: the editor takes the
focus and the problem is announced. The endpoint must be https; http is allowed for `localhost` and loopback addresses.

### Narrow containers

The block follows its own width. From 680 the request (endpoint, event, secret, payload) and the response (timeline, tabs)
sit side by side and the editor stretches to match the response column; below, they stack. Below 420 the delivery times
leave the list and the paddings tighten from 20 to 16.

<Demo name="Narrow" />

### Texts

Every word the block shows or announces is in `Labels`, a `WebhookTesterLabels` with English defaults. Texts that include a
value are functions, so word order can change:

```csharp
Tester.Labels = new WebhookTesterLabels
{
    Send = "Send",
    FailedCount = count => $"{count} did not arrive",
};
```

## API reference

<!-- api: WebhookTester -->

<!-- api: WebhookTestEvent -->

<!-- api: WebhookTestRequest -->

<!-- api: WebhookTestResponse -->

<!-- api: WebhookTestDelivery -->

<!-- api: WebhookTesterSource -->

<!-- api: WebhookTesterLabels -->

## Accessibility

- Every field has a visible label. The payload editor is named by its event ("Payload for order.paid"); while the JSON
  does not parse, its help text is the problem and the line, which the footer also shows.
- The signing secret is hidden from assistive technology until it is revealed; the reveal button is a toggle.
- The response tabs are a tab list with a roving tab stop; the panel is focusable and read only.
- Delivery rows are buttons named by event, outcome and time, in a list; the selected row is checked. Status chips pair
  the code or word with colour.
- Each send and its outcome are announced politely, with "Simulated, nothing was sent." added while no `SendAction` is set.

### Keyboard

- **Ctrl+Enter** (**Command+Enter** on a Mac) sends from anywhere in the block; **Enter** in the endpoint field sends too.
- **Enter** in the payload editor starts a new line with the same indentation.
- **Left** and **Right** arrows, **Home** and **End** move between the Body, Headers and Request tabs.
- **Tab** visits the endpoint, the event list, the reveal button, the copy buttons, the payload, Format, Send, the
  selected tab, the response panel and each failed row's Retry button.

## Motion

- The timeline's circles change with a short fade; the line between two steps fills from left to right, and while the
  request is in flight a dot runs along the last line until the answer arrives.
- A new delivery joins the top of the list and is selected; the response text swaps at once.
- Reduced motion keeps the end states and drops the dot and the fills' travel; Off applies every change at once.

## Related

- `Webhooks`, the console for many endpoints, and `ApiKeys`, the other Developer blocks.
- `CopyButton`, `Button`, `Input` and `Select`, the controls the block is made of.
