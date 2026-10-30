using System.Collections;
using System.Windows.Input;
using Avalonia;
using Avalonia.Collections;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Interactivity;
using Avalonia.Metadata;

namespace TKCode.Arc;

/// <summary>
/// A webhooks console for a developer settings page: the endpoints with their subscribed events, delivered rate and
/// health; the selected endpoint's details and a 24-hour traffic chart you can hover; its recent deliveries with an
/// inspector for the response, the payload and the headers; failed deliveries retried in place; and an "Add endpoint"
/// pill that unfolds into a form and shows the new endpoint's signing secret once.
/// <code>
/// &lt;arc:Webhooks Description="Events are signed and sent to your endpoints."&gt;
///     &lt;arc:WebhookEndpoint Name="Shipping sync" Url="https://relay.tallowfield.dev/hooks/v2" /&gt;
/// &lt;/arc:Webhooks&gt;
/// </code>
/// Retries, new endpoints and test events call <see cref="RetryAction"/>, <see cref="CreateEndpointAction"/> and
/// <see cref="SendTestAction"/>; without them the block simulates a server that always succeeds. Failures show in
/// place: a failed attempt row, an error in the form, a failed test delivery.
/// </summary>
public partial class Webhooks : TemplatedControl
{
    #region Properties

    /// <summary>The title; null shows <see cref="WebhooksLabels.Title"/>.</summary>
    public static readonly StyledProperty<string?> TitleProperty =
        AvaloniaProperty.Register<Webhooks, string?>(nameof(Title));

    /// <summary>The line under the title (at most 540 wide); empty hides it.</summary>
    public static readonly StyledProperty<string?> DescriptionProperty =
        AvaloniaProperty.Register<Webhooks, string?>(nameof(Description));

    /// <summary>The endpoints, when you give them as a list to follow; it wins over <see cref="Endpoints"/> when set.</summary>
    public static readonly StyledProperty<IEnumerable?> EndpointsSourceProperty =
        AvaloniaProperty.Register<Webhooks, IEnumerable?>(nameof(EndpointsSource));

    /// <summary>The selected endpoint (two-way). Null selects the first one.</summary>
    public static readonly StyledProperty<WebhookEndpoint?> SelectedEndpointProperty =
        AvaloniaProperty.Register<Webhooks, WebhookEndpoint?>(nameof(SelectedEndpoint), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>
    /// The delivery shown in the inspector (two-way). Selecting an endpoint picks its first failing delivery, else its
    /// newest one.
    /// </summary>
    public static readonly StyledProperty<WebhookDelivery?> SelectedDeliveryProperty =
        AvaloniaProperty.Register<Webhooks, WebhookDelivery?>(nameof(SelectedDelivery), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>The inspector's tab (two-way); it stays when the delivery or the endpoint changes.</summary>
    public static readonly StyledProperty<WebhookInspectorTab> InspectorTabProperty =
        AvaloniaProperty.Register<Webhooks, WebhookInspectorTab>(nameof(InspectorTab), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>Whether the "Add endpoint" panel is open (two-way).</summary>
    public static readonly StyledProperty<bool> IsAddPanelOpenProperty =
        AvaloniaProperty.Register<Webhooks, bool>(nameof(IsAddPanelOpen), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>The state of the "Add endpoint" panel.</summary>
    public static readonly DirectProperty<Webhooks, WebhookAddState> AddPanelStateProperty =
        AvaloniaProperty.RegisterDirect<Webhooks, WebhookAddState>(nameof(AddPanelState), w => w.AddPanelState);

    /// <summary>
    /// Sends a failed delivery again and returns the new attempt (a failed attempt is shown as one). Null simulates a
    /// server that answers 200 after about 1.1s.
    /// </summary>
    public static readonly StyledProperty<Func<WebhookDelivery, CancellationToken, Task<WebhookDelivery>>?> RetryActionProperty =
        AvaloniaProperty.Register<Webhooks, Func<WebhookDelivery, CancellationToken, Task<WebhookDelivery>>?>(nameof(RetryAction));

    /// <summary>
    /// Adds an endpoint from the form and returns it with its signing secret; throwing shows the message in the form.
    /// Null simulates it after about 0.76s with a random secret.
    /// </summary>
    public static readonly StyledProperty<Func<WebhookEndpointDraft, CancellationToken, Task<WebhookEndpointCreated>>?> CreateEndpointActionProperty =
        AvaloniaProperty.Register<Webhooks, Func<WebhookEndpointDraft, CancellationToken, Task<WebhookEndpointCreated>>?>(nameof(CreateEndpointAction));

    /// <summary>
    /// Sends a test event to an endpoint that has no deliveries yet and returns the delivery. Null simulates a 200
    /// after about 0.9s.
    /// </summary>
    public static readonly StyledProperty<Func<WebhookEndpoint, CancellationToken, Task<WebhookDelivery>>?> SendTestActionProperty =
        AvaloniaProperty.Register<Webhooks, Func<WebhookEndpoint, CancellationToken, Task<WebhookDelivery>>?>(nameof(SendTestAction));

    /// <summary>
    /// Checks the form's URL and returns the error to show, or null. Null uses the built-in check: required, https, not
    /// localhost.
    /// </summary>
    public static readonly StyledProperty<Func<string, string?>?> UrlValidatorProperty =
        AvaloniaProperty.Register<Webhooks, Func<string, string?>?>(nameof(UrlValidator));

    /// <summary>Who adds endpoints in the simulated create (shown as "Added … by" with a generated avatar).</summary>
    public static readonly StyledProperty<string?> CurrentUserProperty =
        AvaloniaProperty.Register<Webhooks, string?>(nameof(CurrentUser));

    /// <summary>Whether failed deliveries have a Retry button.</summary>
    public static readonly StyledProperty<bool> ShowsRetryProperty =
        AvaloniaProperty.Register<Webhooks, bool>(nameof(ShowsRetry), true);

    /// <summary>Every word the block shows or announces.</summary>
    public static readonly StyledProperty<WebhooksLabels> LabelsProperty =
        AvaloniaProperty.Register<Webhooks, WebhooksLabels>(nameof(Labels), new WebhooksLabels());

    /// <summary>Run when a retry starts, with the <see cref="WebhookDelivery"/> as the parameter.</summary>
    public static readonly StyledProperty<ICommand?> RetryCommandProperty =
        AvaloniaProperty.Register<Webhooks, ICommand?>(nameof(RetryCommand));

    /// <summary>Run when a test event is sent, with the <see cref="WebhookEndpoint"/> as the parameter.</summary>
    public static readonly StyledProperty<ICommand?> SendTestCommandProperty =
        AvaloniaProperty.Register<Webhooks, ICommand?>(nameof(SendTestCommand));

    /// <summary>Run after an endpoint was added, with the <see cref="WebhookEndpoint"/> as the parameter.</summary>
    public static readonly StyledProperty<ICommand?> EndpointCreatedCommandProperty =
        AvaloniaProperty.Register<Webhooks, ICommand?>(nameof(EndpointCreatedCommand));

    #endregion

    #region Events

    /// <summary>Raised when the selected endpoint changes.</summary>
    public static readonly RoutedEvent<WebhookEndpointSelectionChangedEventArgs> EndpointSelectionChangedEvent =
        RoutedEvent.Register<Webhooks, WebhookEndpointSelectionChangedEventArgs>(nameof(EndpointSelectionChanged), RoutingStrategies.Bubble);

    /// <summary>Raised when the inspected delivery changes.</summary>
    public static readonly RoutedEvent<WebhookDeliverySelectionChangedEventArgs> DeliverySelectionChangedEvent =
        RoutedEvent.Register<Webhooks, WebhookDeliverySelectionChangedEventArgs>(nameof(DeliverySelectionChanged), RoutingStrategies.Bubble);

    /// <summary>Raised when a retry starts.</summary>
    public static readonly RoutedEvent<WebhookDeliveryEventArgs> RetryStartedEvent =
        RoutedEvent.Register<Webhooks, WebhookDeliveryEventArgs>(nameof(RetryStarted), RoutingStrategies.Bubble);

    /// <summary>Raised when a retry went through.</summary>
    public static readonly RoutedEvent<WebhookRetryCompletedEventArgs> RetryCompletedEvent =
        RoutedEvent.Register<Webhooks, WebhookRetryCompletedEventArgs>(nameof(RetryCompleted), RoutingStrategies.Bubble);

    /// <summary>Raised when a retry, a new endpoint or a test event failed.</summary>
    public static readonly RoutedEvent<WebhookActionFailedEventArgs> ActionFailedEvent =
        RoutedEvent.Register<Webhooks, WebhookActionFailedEventArgs>(nameof(ActionFailed), RoutingStrategies.Bubble);

    /// <summary>Raised after an endpoint was added.</summary>
    public static readonly RoutedEvent<WebhookEndpointEventArgs> EndpointCreatedEvent =
        RoutedEvent.Register<Webhooks, WebhookEndpointEventArgs>(nameof(EndpointCreated), RoutingStrategies.Bubble);

    /// <summary>Raised after a test event went through.</summary>
    public static readonly RoutedEvent<WebhookDeliveryEventArgs> TestEventSentEvent =
        RoutedEvent.Register<Webhooks, WebhookDeliveryEventArgs>(nameof(TestEventSent), RoutingStrategies.Bubble);

    /// <summary>Raised after the inspector copied the payload or the headers.</summary>
    public static readonly RoutedEvent<WebhookContentCopiedEventArgs> ContentCopiedEvent =
        RoutedEvent.Register<Webhooks, WebhookContentCopiedEventArgs>(nameof(ContentCopied), RoutingStrategies.Bubble);

    /// <summary>Raised when the "Add endpoint" panel opens.</summary>
    public static readonly RoutedEvent<RoutedEventArgs> AddPanelOpenedEvent =
        RoutedEvent.Register<Webhooks, RoutedEventArgs>(nameof(AddPanelOpened), RoutingStrategies.Bubble);

    /// <summary>Raised when the "Add endpoint" panel closes.</summary>
    public static readonly RoutedEvent<WebhookAddPanelClosedEventArgs> AddPanelClosedEvent =
        RoutedEvent.Register<Webhooks, WebhookAddPanelClosedEventArgs>(nameof(AddPanelClosed), RoutingStrategies.Bubble);

    /// <summary>Raised when the selected endpoint changes.</summary>
    public event EventHandler<WebhookEndpointSelectionChangedEventArgs>? EndpointSelectionChanged
    {
        add => AddHandler(EndpointSelectionChangedEvent, value);
        remove => RemoveHandler(EndpointSelectionChangedEvent, value);
    }

    /// <summary>Raised when the inspected delivery changes.</summary>
    public event EventHandler<WebhookDeliverySelectionChangedEventArgs>? DeliverySelectionChanged
    {
        add => AddHandler(DeliverySelectionChangedEvent, value);
        remove => RemoveHandler(DeliverySelectionChangedEvent, value);
    }

    /// <summary>Raised when a retry starts.</summary>
    public event EventHandler<WebhookDeliveryEventArgs>? RetryStarted
    {
        add => AddHandler(RetryStartedEvent, value);
        remove => RemoveHandler(RetryStartedEvent, value);
    }

    /// <summary>Raised when a retry went through.</summary>
    public event EventHandler<WebhookRetryCompletedEventArgs>? RetryCompleted
    {
        add => AddHandler(RetryCompletedEvent, value);
        remove => RemoveHandler(RetryCompletedEvent, value);
    }

    /// <summary>Raised when a retry, a new endpoint or a test event failed.</summary>
    public event EventHandler<WebhookActionFailedEventArgs>? ActionFailed
    {
        add => AddHandler(ActionFailedEvent, value);
        remove => RemoveHandler(ActionFailedEvent, value);
    }

    /// <summary>Raised after an endpoint was added.</summary>
    public event EventHandler<WebhookEndpointEventArgs>? EndpointCreated
    {
        add => AddHandler(EndpointCreatedEvent, value);
        remove => RemoveHandler(EndpointCreatedEvent, value);
    }

    /// <summary>Raised after a test event went through.</summary>
    public event EventHandler<WebhookDeliveryEventArgs>? TestEventSent
    {
        add => AddHandler(TestEventSentEvent, value);
        remove => RemoveHandler(TestEventSentEvent, value);
    }

    /// <summary>Raised after the inspector copied the payload or the headers.</summary>
    public event EventHandler<WebhookContentCopiedEventArgs>? ContentCopied
    {
        add => AddHandler(ContentCopiedEvent, value);
        remove => RemoveHandler(ContentCopiedEvent, value);
    }

    /// <summary>Raised when the "Add endpoint" panel opens.</summary>
    public event EventHandler<RoutedEventArgs>? AddPanelOpened
    {
        add => AddHandler(AddPanelOpenedEvent, value);
        remove => RemoveHandler(AddPanelOpenedEvent, value);
    }

    /// <summary>Raised when the "Add endpoint" panel closes.</summary>
    public event EventHandler<WebhookAddPanelClosedEventArgs>? AddPanelClosed
    {
        add => AddHandler(AddPanelClosedEvent, value);
        remove => RemoveHandler(AddPanelClosedEvent, value);
    }

    #endregion

    private WebhookAddState _addPanelState;

    /// <summary>Creates the block.</summary>
    public Webhooks()
    {
        BlockFocusModality.Watch();
        Endpoints = new AvaloniaList<WebhookEndpoint>();
        EventTypes = new AvaloniaList<WebhookEventType>();
        InitializeView();
    }

    /// <summary>The endpoints, when no <see cref="EndpointsSource"/> is set. New endpoints are added at the top.</summary>
    [Content]
    public AvaloniaList<WebhookEndpoint> Endpoints { get; }

    /// <summary>The event types the "Add endpoint" form offers.</summary>
    public AvaloniaList<WebhookEventType> EventTypes { get; }

    /// <inheritdoc cref="TitleProperty"/>
    public string? Title { get => GetValue(TitleProperty); set => SetValue(TitleProperty, value); }

    /// <inheritdoc cref="DescriptionProperty"/>
    public string? Description { get => GetValue(DescriptionProperty); set => SetValue(DescriptionProperty, value); }

    /// <inheritdoc cref="EndpointsSourceProperty"/>
    public IEnumerable? EndpointsSource { get => GetValue(EndpointsSourceProperty); set => SetValue(EndpointsSourceProperty, value); }

    /// <inheritdoc cref="SelectedEndpointProperty"/>
    public WebhookEndpoint? SelectedEndpoint { get => GetValue(SelectedEndpointProperty); set => SetValue(SelectedEndpointProperty, value); }

    /// <inheritdoc cref="SelectedDeliveryProperty"/>
    public WebhookDelivery? SelectedDelivery { get => GetValue(SelectedDeliveryProperty); set => SetValue(SelectedDeliveryProperty, value); }

    /// <inheritdoc cref="InspectorTabProperty"/>
    public WebhookInspectorTab InspectorTab { get => GetValue(InspectorTabProperty); set => SetValue(InspectorTabProperty, value); }

    /// <inheritdoc cref="IsAddPanelOpenProperty"/>
    public bool IsAddPanelOpen { get => GetValue(IsAddPanelOpenProperty); set => SetValue(IsAddPanelOpenProperty, value); }

    /// <inheritdoc cref="AddPanelStateProperty"/>
    public WebhookAddState AddPanelState { get => _addPanelState; private set => SetAndRaise(AddPanelStateProperty, ref _addPanelState, value); }

    /// <inheritdoc cref="RetryActionProperty"/>
    public Func<WebhookDelivery, CancellationToken, Task<WebhookDelivery>>? RetryAction
    {
        get => GetValue(RetryActionProperty);
        set => SetValue(RetryActionProperty, value);
    }

    /// <inheritdoc cref="CreateEndpointActionProperty"/>
    public Func<WebhookEndpointDraft, CancellationToken, Task<WebhookEndpointCreated>>? CreateEndpointAction
    {
        get => GetValue(CreateEndpointActionProperty);
        set => SetValue(CreateEndpointActionProperty, value);
    }

    /// <inheritdoc cref="SendTestActionProperty"/>
    public Func<WebhookEndpoint, CancellationToken, Task<WebhookDelivery>>? SendTestAction
    {
        get => GetValue(SendTestActionProperty);
        set => SetValue(SendTestActionProperty, value);
    }

    /// <inheritdoc cref="UrlValidatorProperty"/>
    public Func<string, string?>? UrlValidator { get => GetValue(UrlValidatorProperty); set => SetValue(UrlValidatorProperty, value); }

    /// <inheritdoc cref="CurrentUserProperty"/>
    public string? CurrentUser { get => GetValue(CurrentUserProperty); set => SetValue(CurrentUserProperty, value); }

    /// <inheritdoc cref="ShowsRetryProperty"/>
    public bool ShowsRetry { get => GetValue(ShowsRetryProperty); set => SetValue(ShowsRetryProperty, value); }

    /// <inheritdoc cref="LabelsProperty"/>
    public WebhooksLabels Labels { get => GetValue(LabelsProperty); set => SetValue(LabelsProperty, value); }

    /// <inheritdoc cref="RetryCommandProperty"/>
    public ICommand? RetryCommand { get => GetValue(RetryCommandProperty); set => SetValue(RetryCommandProperty, value); }

    /// <inheritdoc cref="SendTestCommandProperty"/>
    public ICommand? SendTestCommand { get => GetValue(SendTestCommandProperty); set => SetValue(SendTestCommandProperty, value); }

    /// <inheritdoc cref="EndpointCreatedCommandProperty"/>
    public ICommand? EndpointCreatedCommand { get => GetValue(EndpointCreatedCommandProperty); set => SetValue(EndpointCreatedCommandProperty, value); }
}
