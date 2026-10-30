using Avalonia.Controls;

namespace TKCode.Arc;

/// <summary>An event type the tester can send, with the JSON payload it starts from.</summary>
public sealed class WebhookTestEvent
{
    /// <summary>Creates an event.</summary>
    /// <param name="type">The event's type, such as <c>invoice.paid</c>.</param>
    /// <param name="payload">The default payload as JSON text; valid JSON is pretty printed when the event is chosen, anything else is kept as is.</param>
    public WebhookTestEvent(string type, string payload)
    {
        Type = type;
        Payload = payload;
    }

    /// <summary>The event's type, shown in the event list and in the delivery rows.</summary>
    public string Type { get; }

    /// <summary>The default payload as JSON text.</summary>
    public string Payload { get; }

    /// <inheritdoc />
    public override string ToString() => Type;
}

/// <summary>
/// The signed request <see cref="WebhookTester"/> hands to <see cref="WebhookTester.SendAction"/>. Send it from your server, not
/// from the browser: the server can re-sign the same body and check that the signature matches.
/// </summary>
public sealed class WebhookTestRequest
{
    /// <summary>Creates a request.</summary>
    public WebhookTestRequest(string url, string eventType, string body, long timestamp, string signature, string signatureHeader,
        IReadOnlyList<KeyValuePair<string, string>> headers)
    {
        Url = url;
        Event = eventType;
        Body = body;
        Timestamp = timestamp;
        Signature = signature;
        SignatureHeader = signatureHeader;
        Headers = headers;
    }

    /// <summary>The endpoint the request is meant for.</summary>
    public string Url { get; }

    /// <summary>The event's type.</summary>
    public string Event { get; }

    /// <summary>The JSON body exactly as it was signed.</summary>
    public string Body { get; }

    /// <summary>Unix time in seconds that went into the signature.</summary>
    public long Timestamp { get; }

    /// <summary>The value of the signature header: <c>t=timestamp,v1=hex</c>.</summary>
    public string Signature { get; }

    /// <summary>The name of the header that carries <see cref="Signature"/>.</summary>
    public string SignatureHeader { get; }

    /// <summary>Every header of the request, the signature header included.</summary>
    public IReadOnlyList<KeyValuePair<string, string>> Headers { get; }
}

/// <summary>
/// What an endpoint answered. A <see cref="Status"/> of 0 means no answer arrived: set <see cref="Error"/> (and
/// <see cref="TimedOut"/> when it ran out of time).
/// </summary>
public sealed class WebhookTestResponse
{
    /// <summary>The HTTP status; 0 when nothing was received.</summary>
    public int Status { get; init; }

    /// <summary>How long the delivery took, in milliseconds.</summary>
    public int DurationMs { get; init; }

    /// <summary>The response headers, if you have them.</summary>
    public IReadOnlyList<KeyValuePair<string, string>>? Headers { get; init; }

    /// <summary>The response body, if you have it.</summary>
    public string? Body { get; init; }

    /// <summary>Why no answer arrived (status 0).</summary>
    public string? Error { get; init; }

    /// <summary>True when the endpoint did not answer in time.</summary>
    public bool TimedOut { get; init; }

    /// <summary>Whether the status is 2xx.</summary>
    public bool IsSuccess => Status is >= 200 and < 300;
}

/// <summary>One attempt in the recent list: what was sent, and what came back.</summary>
public sealed class WebhookTestDelivery
{
    /// <summary>Creates a delivery.</summary>
    public WebhookTestDelivery(string id, string eventType, DateTimeOffset time, WebhookTestRequest request, WebhookTestResponse response)
    {
        Id = id;
        Event = eventType;
        Time = time;
        Request = request;
        Response = response;
    }

    /// <summary>A key that is unique in the list.</summary>
    public string Id { get; }

    /// <summary>The event's type.</summary>
    public string Event { get; }

    /// <summary>When the attempt was made (shown as a clock time).</summary>
    public DateTimeOffset Time { get; }

    /// <summary>What was sent.</summary>
    public WebhookTestRequest Request { get; }

    /// <summary>What came back.</summary>
    public WebhookTestResponse Response { get; }

    /// <summary>True unless the status is 2xx.</summary>
    public bool IsFailed => !Response.IsSuccess;
}

/// <summary>The product that sends the events: a name for the subtitle and a mark for the header.</summary>
public sealed class WebhookTesterSource
{
    /// <summary>The product's name.</summary>
    public string Name { get; init; } = "Acme";

    /// <summary>Makes the 16px mark; called once per header. Null draws a rounded square with the name's first letter.</summary>
    public Func<Control>? MarkFactory { get; init; }
}

/// <summary>Where the timeline of the last send stands.</summary>
public enum WebhookTestPhase
{
    /// <summary>Nothing was sent yet.</summary>
    Idle,

    /// <summary>The body is being signed.</summary>
    Signing,

    /// <summary>The request is on its way.</summary>
    Sending,

    /// <summary>A 2xx answer arrived.</summary>
    Succeeded,

    /// <summary>The endpoint answered with another status, did not answer, or the send threw.</summary>
    Failed
}

/// <summary>Raised after a test event was sent or a delivery was retried.</summary>
public sealed class WebhookTesterDeliveryEventArgs(WebhookTestDelivery delivery) : EventArgs
{
    /// <summary>The new delivery, already at the top of the list.</summary>
    public WebhookTestDelivery Delivery { get; } = delivery;
}
