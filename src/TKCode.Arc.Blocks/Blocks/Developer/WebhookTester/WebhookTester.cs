using System.Windows.Input;
using Avalonia;
using Avalonia.Collections;
using Avalonia.Controls.Primitives;
using Avalonia.Data;

namespace TKCode.Arc;

/// <summary>
/// A tester for the endpoints of a product that sends webhooks: pick an event, edit its JSON payload and send a signed test
/// event. The request timeline walks from signed to sent to the answer, the response panel shows the body, the headers and
/// the exact request, and a list of recent deliveries keeps the last attempts, failed ones with a Retry button.
/// <para>
/// The block signs in the app (HMAC SHA-256 of <c>timestamp.body</c>, sent as <c>t=…,v1=…</c> in
/// <see cref="SignatureHeader"/>) but never posts to your customer's endpoint itself: <see cref="SendAction"/> receives the
/// signed <see cref="WebhookTestRequest"/> and passes it to your server, which does the real POST. Without a
/// <see cref="SendAction"/> every delivery is simulated and says so in its announcements.
/// </para>
/// <code>
/// &lt;arc:WebhookTester Endpoint="https://api.acme.dev/webhooks/billing" Secret="{Binding SigningSecret}" /&gt;
/// </code>
/// </summary>
public partial class WebhookTester : TemplatedControl
{
    #region Properties

    /// <summary>The endpoint test events go to (two-way). https is required, except http for localhost.</summary>
    public static readonly StyledProperty<string?> EndpointProperty =
        AvaloniaProperty.Register<WebhookTester, string?>(nameof(Endpoint), "https://api.northwind.dev/webhooks/billing",
            defaultBindingMode: BindingMode.TwoWay);

    /// <summary>The events to choose from; while it is empty five sample events are offered. Choosing an event loads its payload.</summary>
    public static readonly DirectProperty<WebhookTester, AvaloniaList<WebhookTestEvent>> EventsProperty =
        AvaloniaProperty.RegisterDirect<WebhookTester, AvaloniaList<WebhookTestEvent>>(nameof(Events), t => t._events);

    /// <summary>The chosen event (two-way); null selects the first one.</summary>
    public static readonly StyledProperty<WebhookTestEvent?> SelectedEventProperty =
        AvaloniaProperty.Register<WebhookTester, WebhookTestEvent?>(nameof(SelectedEvent), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>The payload as it stands in the editor (two-way), valid JSON or not.</summary>
    public static readonly StyledProperty<string?> PayloadProperty =
        AvaloniaProperty.Register<WebhookTester, string?>(nameof(Payload), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>The signing secret. It is shown masked with its prefix visible; the body is signed with it in the app.</summary>
    public static readonly StyledProperty<string> SecretProperty =
        AvaloniaProperty.Register<WebhookTester, string>(nameof(Secret), "sigsec_9vXc2Lq8RkT4mN7bP1sZ");

    /// <summary>The product that sends the events: its name and mark in the header.</summary>
    public static readonly StyledProperty<WebhookTesterSource> SourceProperty =
        AvaloniaProperty.Register<WebhookTester, WebhookTesterSource>(nameof(Source), new WebhookTesterSource());

    /// <summary>The header that carries the signature (<c>x-webhook-signature</c>).</summary>
    public static readonly StyledProperty<string> SignatureHeaderProperty =
        AvaloniaProperty.Register<WebhookTester, string>(nameof(SignatureHeader), "x-webhook-signature");

    /// <summary>
    /// Sends the signed request on your server: awaited when the person sends, with a token that leaving the window cancels.
    /// Return the endpoint's answer; a status of 0 with <see cref="WebhookTestResponse.Error"/> (and
    /// <see cref="WebhookTestResponse.TimedOut"/>) says no answer arrived, and an exception counts as a failed delivery.
    /// Null simulates the endpoint (a 200 after <see cref="SimulatedLatency"/>).
    /// </summary>
    public static readonly StyledProperty<Func<WebhookTestRequest, CancellationToken, Task<WebhookTestResponse>>?> SendActionProperty =
        AvaloniaProperty.Register<WebhookTester, Func<WebhookTestRequest, CancellationToken, Task<WebhookTestResponse>>?>(nameof(SendAction));

    /// <summary>
    /// Sends a failed delivery again on your server: awaited when Retry is pressed, with the delivery's body under a fresh
    /// timestamp and signature. Null uses <see cref="SendAction"/>.
    /// </summary>
    public static readonly StyledProperty<Func<WebhookTestDelivery, CancellationToken, Task<WebhookTestResponse>>?> RetryActionProperty =
        AvaloniaProperty.Register<WebhookTester, Func<WebhookTestDelivery, CancellationToken, Task<WebhookTestResponse>>?>(nameof(RetryAction));

    /// <summary>The recent deliveries, newest first; a send or a retry inserts at the top.</summary>
    public static readonly DirectProperty<WebhookTester, AvaloniaList<WebhookTestDelivery>> DeliveriesProperty =
        AvaloniaProperty.RegisterDirect<WebhookTester, AvaloniaList<WebhookTestDelivery>>(nameof(Deliveries), t => t._deliveries);

    /// <summary>The delivery whose response the panel shows (two-way); a send selects the new delivery.</summary>
    public static readonly StyledProperty<WebhookTestDelivery?> SelectedDeliveryProperty =
        AvaloniaProperty.Register<WebhookTester, WebhookTestDelivery?>(nameof(SelectedDelivery), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>Sends one sample event about a second after the block scrolls half into view, unless someone used it first. Only while <see cref="SendAction"/> is null.</summary>
    public static readonly StyledProperty<bool> AutoPlayProperty =
        AvaloniaProperty.Register<WebhookTester, bool>(nameof(AutoPlay), true);

    /// <summary>How long a simulated delivery takes to answer (0.65 s).</summary>
    public static readonly StyledProperty<TimeSpan> SimulatedLatencyProperty =
        AvaloniaProperty.Register<WebhookTester, TimeSpan>(nameof(SimulatedLatency), TimeSpan.FromMilliseconds(650));

    /// <summary>Run with a <see cref="WebhookTestDelivery"/> after a send or a retry finished.</summary>
    public static readonly StyledProperty<ICommand?> DeliveredCommandProperty =
        AvaloniaProperty.Register<WebhookTester, ICommand?>(nameof(DeliveredCommand));

    /// <summary>Draws a bordered card with rounded corners (true); false lays the block flat on the page.</summary>
    public static readonly StyledProperty<bool> IsFramedProperty =
        AvaloniaProperty.Register<WebhookTester, bool>(nameof(IsFramed), true);

    /// <summary>Every word the block shows or announces.</summary>
    public static readonly StyledProperty<WebhookTesterLabels> LabelsProperty =
        AvaloniaProperty.Register<WebhookTester, WebhookTesterLabels>(nameof(Labels), new WebhookTesterLabels());

    /// <summary>Where the timeline stands.</summary>
    public static readonly DirectProperty<WebhookTester, WebhookTestPhase> PhaseProperty =
        AvaloniaProperty.RegisterDirect<WebhookTester, WebhookTestPhase>(nameof(Phase), t => t._phase);

    /// <summary>The widest the card gets (760); the block fills narrower containers.</summary>
    public static readonly StyledProperty<double> CardMaxWidthProperty =
        AvaloniaProperty.Register<WebhookTester, double>(nameof(CardMaxWidth), 760);

    /// <summary>From this width of the block the request and the response sit side by side (680).</summary>
    public static readonly StyledProperty<double> SplitWidthProperty =
        AvaloniaProperty.Register<WebhookTester, double>(nameof(SplitWidth), 680);

    private readonly AvaloniaList<WebhookTestEvent> _events = new();
    private readonly AvaloniaList<WebhookTestDelivery> _deliveries = new();
    private WebhookTestPhase _phase;

    #endregion

    /// <inheritdoc cref="EndpointProperty"/>
    public string? Endpoint { get => GetValue(EndpointProperty); set => SetValue(EndpointProperty, value); }

    /// <inheritdoc cref="EventsProperty"/>
    public AvaloniaList<WebhookTestEvent> Events => _events;

    /// <inheritdoc cref="SelectedEventProperty"/>
    public WebhookTestEvent? SelectedEvent { get => GetValue(SelectedEventProperty); set => SetValue(SelectedEventProperty, value); }

    /// <inheritdoc cref="PayloadProperty"/>
    public string? Payload { get => GetValue(PayloadProperty); set => SetValue(PayloadProperty, value); }

    /// <inheritdoc cref="SecretProperty"/>
    public string Secret { get => GetValue(SecretProperty); set => SetValue(SecretProperty, value); }

    /// <inheritdoc cref="SourceProperty"/>
    public WebhookTesterSource Source { get => GetValue(SourceProperty); set => SetValue(SourceProperty, value); }

    /// <inheritdoc cref="SignatureHeaderProperty"/>
    public string SignatureHeader { get => GetValue(SignatureHeaderProperty); set => SetValue(SignatureHeaderProperty, value); }

    /// <inheritdoc cref="SendActionProperty"/>
    public Func<WebhookTestRequest, CancellationToken, Task<WebhookTestResponse>>? SendAction
    {
        get => GetValue(SendActionProperty);
        set => SetValue(SendActionProperty, value);
    }

    /// <inheritdoc cref="RetryActionProperty"/>
    public Func<WebhookTestDelivery, CancellationToken, Task<WebhookTestResponse>>? RetryAction
    {
        get => GetValue(RetryActionProperty);
        set => SetValue(RetryActionProperty, value);
    }

    /// <inheritdoc cref="DeliveriesProperty"/>
    public AvaloniaList<WebhookTestDelivery> Deliveries => _deliveries;

    /// <inheritdoc cref="SelectedDeliveryProperty"/>
    public WebhookTestDelivery? SelectedDelivery { get => GetValue(SelectedDeliveryProperty); set => SetValue(SelectedDeliveryProperty, value); }

    /// <inheritdoc cref="AutoPlayProperty"/>
    public bool AutoPlay { get => GetValue(AutoPlayProperty); set => SetValue(AutoPlayProperty, value); }

    /// <inheritdoc cref="SimulatedLatencyProperty"/>
    public TimeSpan SimulatedLatency { get => GetValue(SimulatedLatencyProperty); set => SetValue(SimulatedLatencyProperty, value); }

    /// <inheritdoc cref="DeliveredCommandProperty"/>
    public ICommand? DeliveredCommand { get => GetValue(DeliveredCommandProperty); set => SetValue(DeliveredCommandProperty, value); }

    /// <inheritdoc cref="IsFramedProperty"/>
    public bool IsFramed { get => GetValue(IsFramedProperty); set => SetValue(IsFramedProperty, value); }

    /// <inheritdoc cref="LabelsProperty"/>
    public WebhookTesterLabels Labels { get => GetValue(LabelsProperty); set => SetValue(LabelsProperty, value); }

    /// <inheritdoc cref="PhaseProperty"/>
    public WebhookTestPhase Phase => _phase;

    /// <inheritdoc cref="CardMaxWidthProperty"/>
    public double CardMaxWidth { get => GetValue(CardMaxWidthProperty); set => SetValue(CardMaxWidthProperty, value); }

    /// <inheritdoc cref="SplitWidthProperty"/>
    public double SplitWidth { get => GetValue(SplitWidthProperty); set => SetValue(SplitWidthProperty, value); }

    /// <summary>Raised after a send or a retry finished, with the new delivery (already at the top of the list).</summary>
    public event EventHandler<WebhookTesterDeliveryEventArgs>? Delivered;
}
