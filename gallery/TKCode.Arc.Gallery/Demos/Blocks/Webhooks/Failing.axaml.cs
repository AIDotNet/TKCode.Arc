using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class WebhooksFailingDemo : UserControl
{
    private int _retries;
    private int _creates;

    public WebhooksFailingDemo()
    {
        InitializeComponent();
        Hooks.Description = WebhooksDemoData.Description;
        Hooks.EventTypes.AddRange(WebhooksDemoData.EventTypes());
        Hooks.Endpoints.AddRange(WebhooksDemoData.Endpoints(DateTimeOffset.Now));

        // Odd retries time out (the block adds a failed attempt with its own Retry); even ones are answered with 200.
        Hooks.RetryAction = async (delivery, cancellation) =>
        {
            await Task.Delay(1100, cancellation);
            if (++_retries % 2 == 1)
            {
                throw new TimeoutException("The endpoint did not answer within 30 seconds.");
            }
            var attempt = new WebhookDelivery
            {
                EventType = delivery.EventType,
                EventId = delivery.EventId,
                Attempt = delivery.Attempt + 1,
                StatusCode = 200,
                Duration = TimeSpan.FromMilliseconds(143),
                SentAt = DateTimeOffset.Now,
                ResponseBody = "{\"ok\":true}",
                Payload = delivery.Payload,
            };
            attempt.Headers.AddRange(delivery.Headers.Select(h => new WebhookHeader(h.Name, h.Value)));
            return attempt;
        };

        // The first new endpoint is refused (the URL did not answer the check); the next one is added.
        Hooks.CreateEndpointAction = async (draft, cancellation) =>
        {
            await Task.Delay(760, cancellation);
            if (++_creates == 1)
            {
                throw new InvalidOperationException("The URL answered 404 to the verification request.");
            }
            var endpoint = new WebhookEndpoint
            {
                Name = draft.Description,
                Url = draft.Url,
                AddedBy = WebhooksDemoData.CurrentUser,
                AddedOn = DateTimeOffset.Now,
                SecretHint = "sig_••••51fb",
                ApiVersion = "Version 2026.09",
            };
            endpoint.Events.AddRange(draft.Events);
            return new WebhookEndpointCreated(endpoint, "sig_Hq4Vn8Lc2Tx6Rb9Zd3Wk7Mp5Ys1Fj0Ae");
        };

        Hooks.ActionFailed += (_, e) => Log.Text = $"{e.Action} failed: {e.Exception?.Message ?? "the endpoint refused it"}";
        Hooks.RetryCompleted += (_, e) => Log.Text = $"{e.Original.EventType} went through on try {e.Result.Attempt}.";
        Hooks.EndpointCreated += (_, e) => Log.Text = $"Added {e.Endpoint.DisplayName}.";
    }
}
