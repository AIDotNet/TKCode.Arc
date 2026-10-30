namespace TKCode.Arc.Gallery.Demos;

/// <summary>Sample deliveries and a fake endpoint for the WebhookTester demos (all names and values are made up).</summary>
public static class WebhookTesterDemoData
{
    private static readonly DateTimeOffset s_day = new(2026, 10, 28, 0, 0, 0, TimeSpan.Zero);

    /// <summary>Five earlier attempts, newest first: two of them failed.</summary>
    public static IEnumerable<WebhookTestDelivery> Deliveries(string url = "https://api.northwind.dev/webhooks/billing")
    {
        yield return Make("dlv_5", "order.paid", new TimeSpan(14, 28, 6), url, 200, 184, "{\n  \"received\": true\n}");
        yield return Make("dlv_4", "subscription.updated", new TimeSpan(14, 11, 42), url, 500, 1204,
            "{\n  \"error\": \"upstream unavailable\"\n}");
        yield return Make("dlv_3", "customer.created", new TimeSpan(13, 57, 19), url, 200, 96, "{\n  \"received\": true\n}");
        yield return Make("dlv_2", "payment.failed", new TimeSpan(13, 40, 3), url, 0, 10000, null, timedOut: true);
        yield return Make("dlv_1", "checkout.completed", new TimeSpan(13, 22, 51), url, 200, 212, "{\n  \"received\": true\n}");
    }

    private static WebhookTestDelivery Make(string id, string type, TimeSpan at, string url, int status, int ms, string? body,
        bool timedOut = false)
    {
        var time = s_day + at;
        var payload = $"{{\n  \"id\": \"evt_{id}\",\n  \"type\": \"{type}\"\n}}";
        var timestamp = time.ToUnixTimeSeconds();
        var signature = $"t={timestamp},v1={WebhookTesterDemoSigner.Hex(id)}";
        var request = new WebhookTestRequest(url, type, payload, timestamp, signature, "x-webhook-signature",
        [
            new("content-type", "application/json"),
            new("user-agent", "Acme-Webhooks/1.0"),
            new("x-webhook-event", type),
            new("x-webhook-signature", signature),
        ]);
        var response = new WebhookTestResponse
        {
            Status = status,
            DurationMs = ms,
            Body = body,
            TimedOut = timedOut,
            Error = timedOut ? "The endpoint did not answer within 10 seconds." : null,
            Headers = status == 0
                ? null
                : [new("content-type", "application/json"), new("x-request-id", "req_" + id)],
        };
        return new WebhookTestDelivery(id, type, time, request, response);
    }

    /// <summary>The events of the "other product" demo: made-up names, nothing from a real service.</summary>
    public static IEnumerable<WebhookTestEvent> RelayEvents()
    {
        yield return new WebhookTestEvent("message.delivered", """{"id":"evt_9f2a","type":"message.delivered","data":{"message":"msg_0184","to":"ada@northwind.dev","latency_ms":412}}""");
        yield return new WebhookTestEvent("message.bounced", """{"id":"evt_9f2b","type":"message.bounced","data":{"message":"msg_0185","to":"nobody@example.org","reason":"mailbox full"}}""");
        yield return new WebhookTestEvent("domain.verified", """{"id":"evt_9f2c","type":"domain.verified","data":{"domain":"mail.northwind.dev","checked":["spf","dkim","dmarc"]}}""");
    }
}

/// <summary>A stand-in for a signature in the sample deliveries (not a real HMAC; the block signs real sends itself).</summary>
internal static class WebhookTesterDemoSigner
{
    public static string Hex(string seed)
    {
        var hash = System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(seed));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}
