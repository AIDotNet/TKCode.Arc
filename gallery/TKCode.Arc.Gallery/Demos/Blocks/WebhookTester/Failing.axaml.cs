using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class WebhookTesterFailingDemo : UserControl
{
    public WebhookTesterFailingDemo()
    {
        InitializeComponent();

        // A real app passes the signed request to its server, which does the POST. Here the "server" is a delay and a rule:
        // paths ending in /legacy answer 500, /slow never answers in time, everything else is a 200.
        Block.SendAction = async (request, cancellation) =>
        {
            await Task.Delay(700, cancellation);
            if (request.Url.EndsWith("/slow", StringComparison.Ordinal))
            {
                return new WebhookTestResponse { Status = 0, DurationMs = 10000, TimedOut = true, Error = "No answer within 10 seconds." };
            }
            if (request.Url.EndsWith("/legacy", StringComparison.Ordinal))
            {
                return new WebhookTestResponse
                {
                    Status = 500,
                    DurationMs = 701,
                    Body = "{\"error\":\"legacy handler is switched off\"}",
                    Headers = [new("content-type", "application/json")],
                };
            }
            return new WebhookTestResponse
            {
                Status = 200,
                DurationMs = 143,
                Body = "{\"received\":true}",
                Headers = [new("content-type", "application/json")],
            };
        };

        // Retry goes to the same server, here always through: the retry reaches the (now fixed) handler.
        Block.RetryAction = async (delivery, cancellation) =>
        {
            await Task.Delay(500, cancellation);
            return new WebhookTestResponse { Status = 200, DurationMs = 97, Body = "{\"received\":true}" };
        };

        Block.Delivered += (_, e) =>
        {
            var response = e.Delivery.Response;
            Log.Text = response.IsSuccess
                ? $"{e.Delivery.Event}: {response.Status} in {response.DurationMs} ms."
                : $"{e.Delivery.Event} failed ({(response.TimedOut ? "timed out" : response.Status)}). Press Retry in the list.";
        };
    }
}
