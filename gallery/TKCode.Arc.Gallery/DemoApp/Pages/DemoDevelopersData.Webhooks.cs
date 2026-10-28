using System.Globalization;

namespace TKCode.Arc.Gallery.DemoApp;

internal static partial class DemoDevelopersData
{
    /// <summary>The first half hour of the traffic charts: Sep 21 2026, 15:00 UTC (48 buckets up to 15:00 today).</summary>
    private static readonly DateTimeOffset VolumeStart = new(2026, 9, 21, 15, 0, 0, TimeSpan.Zero);

    /// <summary>uiarc.dev's retry latencies, in turn.</summary>
    private static readonly int[] RetryLatencies = [184, 146, 211, 163, 129, 197];

    private static int s_retries;

    public static WebhooksLabels WebhookLabels { get; } = new()
    {
        Title = "Webhooks",
        FailingNote = count => $"{count} with failed deliveries",
        HealthyNote = "all delivering",
        NewEndpoint = "New endpoint",
        EndpointReady = "Endpoint added",
        RateCaption = "delivered",
        NoTrafficBadge = "No events",
        AddedBy = (name, _) => $"Added by {name}",
        ChartFailed = count => $"{count:N0} failed",
        NoFailures = "all delivered",
        NoTrafficYet = "No events yet",
        TryLabel = attempt => $"Attempt {attempt}",
        Retrying = "Sending",
        RetryName = (eventType, time) => $"Retry {eventType} from {time}",
        RetryingName = eventType => $"Sending {eventType} again",
        EmptyHint = host => $"Send a test event to check that {host} answers.",
        InspectorSubtitle = (id, attempt) => $"{id}, attempt {attempt}",
        TabsName = "Request",
        InspectorPlaceholder = "The request body and headers of each delivery show up here.",
        UrlPlaceholder = "https://api.fernway.example/webhooks",
        DescriptionPlaceholder = "Warehouse sync",
        EventsLegend = "Events to send",
        Saving = "Adding",
        UrlRequired = "Enter the URL Halden should post events to",
        UrlInvalid = "Use a public HTTPS URL, such as https://api.fernway.example/webhooks",
        EventsRequired = "Choose at least one event",
        SecretNotice = "Shown once. Use it to check the Halden-Signature header on every request. You can roll it later, but you cannot view it again.",
        RetrySucceeded = (eventType, attempt, host, code, duration) => $"{eventType} delivered on attempt {attempt}. {host} answered {code} in {duration}.",
        EndpointAdded = name => $"{name} added. Copy the signing secret now, it will not be shown again.",
        TestSent = (_, host, code) => $"Test event delivered. {host} answered {code}.",
        CopiedAnnouncement = what => $"{what} copied"
    };

    public static WebhookEventType[] EventTypes() =>
    [
        new() { Name = "order.paid", Description = "An order is paid in full", IsDefaultSelected = true },
        new() { Name = "order.refunded", Description = "An order is refunded, fully or in part" },
        new() { Name = "invoice.paid", Description = "A subscription invoice is paid" },
        new() { Name = "invoice.payment_failed", Description = "A renewal charge is declined" },
        new() { Name = "payout.paid", Description = "A payout reaches your bank" },
        new() { Name = "dispute.created", Description = "A customer disputes a charge" }
    ];

    public static WebhookEndpoint[] Endpoints()
    {
        var ledger = Endpoint("ledger", "Ledger sync", "https://ledger.fernway.example/v1/halden/events", "2026-08-01", "7f3a",
            "Emma Collins", new DateTime(2026, 3, 4), ["invoice.paid", "invoice.payment_failed", "payout.paid", "refund.created"],
            [16, 12, 12, 11, 9, 12, 10, 12, 8, 7, 6, 4, 4, 3, 1, 1, 1, 1, 2, 1, 2, 2, 1, 1, 1, 1, 2, 2, 1, 1, 1, 3, 3, 4, 5, 6, 7, 9, 8, 11, 13, 16, 14, 11, 12, 14, 11, 2]);
        ledger.Deliveries.AddRange(
        [
            Delivery("dlv_8Kq2Lm4", "evt_h1JkxJ1dg1", "invoice.paid", 200, 142, "14:31:08", null,
                """{"id":"in_1Q8vRt2","object":"invoice","number":"FW-2026-1182","customer":"cus_R4bLw9","amount_paid":48000,"currency":"usd","status":"paid"}"""),
            Delivery("dlv_7Hn1Pc9", "evt_GAYU3UijOD", "invoice.payment_failed", 500, 2318, "14:12:44",
                """{"error":"ledger_write_failed","message":"connection pool exhausted"}""",
                """{"id":"in_1Q8uXe7","object":"invoice","number":"FW-2026-1179","customer":"cus_P2mZr6","amount_due":129900,"currency":"usd","attempt_count":2,"next_payment_attempt":1790265600}"""),
            Delivery("dlv_6Gm0Ob8", "evt_0wWBVyMYIJ", "payout.paid", 200, 98, "13:47:02", null,
                """{"id":"po_1Q9aZt4","object":"payout","amount":1845210,"currency":"usd","arrival_date":1790035200,"destination":"ba_1PzR7c","status":"paid"}"""),
            Delivery("dlv_5Fl9Na7", "evt_uorQbbYsED", "invoice.paid", 504, 30000, "11:05:19", "upstream request timeout",
                """{"id":"in_1Q8pLk3","object":"invoice","number":"FW-2026-1164","customer":"cus_K9dQe2","amount_paid":21900,"currency":"usd","status":"paid"}"""),
            Delivery("dlv_4Ek8Mz6", "evt_ugowmjPClV", "refund.created", 200, 121, "10:22:51", null,
                """{"id":"re_1Q8nWb5","object":"refund","charge":"ch_3Q8mTz1","amount":4800,"currency":"usd","reason":"requested_by_customer","status":"succeeded"}"""),
            Delivery("dlv_3Dj7Ly5", "evt_llGpfhwCPU", "invoice.paid", 500, 1904, "09:14:36",
                """{"error":"ledger_write_failed","message":"deadlock detected on ledger_entries"}""",
                """{"id":"in_1Q8kFa9","object":"invoice","number":"FW-2026-1151","customer":"cus_H3vNp8","amount_paid":9900,"currency":"usd","status":"paid"}"""),
            Delivery("dlv_2Ci6Kx4", "evt_tEV5aHXoUd", "invoice.paid", 200, 133, "08:59:10", null,
                """{"id":"in_1Q8jRm0","object":"invoice","number":"FW-2026-1149","customer":"cus_B7xLc4","amount_paid":36000,"currency":"usd","status":"paid"}""")
        ]);

        var orders = Endpoint("orders", "Order fulfilment", "https://api.fernway.example/webhooks/halden", "2026-08-01", "c21e",
            "Marcus Johnson", new DateTime(2026, 1, 18), ["order.paid", "order.refunded", "customer.created"],
            [42, 39, 38, 36, 37, 28, 31, 26, 18, 15, 15, 15, 11, 7, 4, 3, 4, 4, 4, 4, 4, 3, 3, 4, 4, 3, 4, 4, 3, 3, 4, 5, 11, 11, 18, 20, 18, 20, 31, 27, 34, 25, 37, 26, 26, 32, 29, 4]);
        orders.Deliveries.AddRange(
        [
            Delivery("dlv_9Lr3Mn5", "evt_GnKVCUeRi6", "order.paid", 200, 87, "14:30:52", null,
                """{"id":"ord_7Qm2Ks","object":"order","customer":"cus_T7wQx1","amount_total":21400,"currency":"usd","items":3,"shipping":"standard"}"""),
            Delivery("dlv_9Ks2Ln4", "evt_1vqueHwQy5", "customer.created", 200, 64, "14:30:49", null,
                """{"id":"cus_T7wQx1","object":"customer","name":"Priya Raman","email":"priya@lumenhaus.co","country":"US"}"""),
            Delivery("dlv_9Jt1Ko3", "evt_BWN3fal1Ni", "order.paid", 200, 112, "14:26:13", null,
                """{"id":"ord_7Qk8Hd","object":"order","customer":"cus_M2pRz5","amount_total":5800,"currency":"usd","items":1,"shipping":"express"}"""),
            Delivery("dlv_9Iu0Jp2", "evt_KzVfYJ6LjD", "order.refunded", 200, 95, "14:02:37", null,
                """{"id":"ord_7Qh4Wn","object":"order","customer":"cus_D8nVt3","amount_refunded":5800,"currency":"usd","reason":"damaged_in_transit"}"""),
            Delivery("dlv_9Hv9Iq1", "evt_fZRTv5Oykp", "order.paid", 200, 238, "13:58:04", null,
                """{"id":"ord_7Qg1Bc","object":"order","customer":"cus_F4kYs9","amount_total":13250,"currency":"usd","items":2,"shipping":"standard"}""")
        ]);

        var alerts = Endpoint("alerts", "Ops alerts", "https://hooks.fernway.example/ops/halden", "2026-05-12", "90bd",
            "Ava Mitchell", new DateTime(2026, 6, 9), ["dispute.created", "payout.failed"],
            [0, 0, 0, 0, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 2, 0, 0, 0, 0]);
        alerts.Deliveries.AddRange(
        [
            Delivery("dlv_1Aw5Cr8", "evt_mkNG5NHdfK", "dispute.created", 200, 311, "12:40:18", null,
                """{"id":"dp_1Q8tYh6","object":"dispute","charge":"ch_3Q7wPx2","amount":8900,"currency":"usd","reason":"product_not_received","status":"needs_response"}"""),
            Delivery("dlv_1Bx4Ds7", "evt_dNf5shCvrS", "payout.failed", 200, 276, "07:12:55", null,
                """{"id":"po_1Q8hVc2","object":"payout","amount":212000,"currency":"usd","failure_code":"account_closed","destination":"ba_1NcT3q"}""")
        ]);

        // Failed deliveries are drawn over the half hour they were sent in.
        foreach (var endpoint in new[] { ledger, orders, alerts })
        {
            foreach (var failed in endpoint.Deliveries.Where(d => d.IsOpenFailure))
            {
                var bucket = endpoint.Volume.FirstOrDefault(b => failed.SentAt >= b.Start && failed.SentAt < b.End);
                if (bucket is not null)
                {
                    bucket.Failed++;
                }
            }
        }
        return [ledger, orders, alerts];
    }

    /// <summary>uiarc.dev's retry: 1.1 s, then a 200 on the next attempt at 14:32:05 + 9 s per retry.</summary>
    public static async Task<WebhookDelivery> RetryAsync(WebhookDelivery original, CancellationToken token)
    {
        await Task.Delay(1100, token);
        var n = s_retries++;
        var attempt = original.Attempt + 1;
        var id = "dlv_r" + Hex(original.EventId + attempt, 6);
        var sentAt = new DateTimeOffset(2026, 9, 22, 14, 32, 5, TimeSpan.Zero).AddSeconds(9 * n);
        var delivery = new WebhookDelivery
        {
            EventType = original.EventType,
            EventId = original.EventId,
            Attempt = attempt,
            IsTest = original.IsTest,
            StatusCode = 200,
            Duration = TimeSpan.FromMilliseconds(RetryLatencies[n % RetryLatencies.Length]),
            SentAt = sentAt,
            ResponseBody = """{"received":true}""",
            Payload = original.Payload
        };
        delivery.Headers.AddRange(Headers(id, original.EventId ?? string.Empty, original.EventType, attempt, sentAt));
        return delivery;
    }

    /// <summary>uiarc.dev's test event: 0.9 s, then the endpoint's first event answered 200 in 172 ms.</summary>
    public static async Task<WebhookDelivery> SendTestAsync(WebhookEndpoint endpoint, CancellationToken token)
    {
        await Task.Delay(900, token);
        var type = endpoint.Events.FirstOrDefault() ?? "order.paid";
        var eventId = "evt_test_" + Hex(endpoint.Url, 6);
        var sentAt = new DateTimeOffset(2026, 9, 22, 14, 33, 12, TimeSpan.Zero);
        var delivery = new WebhookDelivery
        {
            EventType = type,
            EventId = eventId,
            IsTest = true,
            StatusCode = 200,
            Duration = TimeSpan.FromMilliseconds(172),
            SentAt = sentAt,
            ResponseBody = """{"received":true}""",
            Payload = Payload(eventId, type, sentAt, TestData(type))
        };
        delivery.Headers.AddRange(Headers("dlv_test" + Hex(eventId, 4), eventId, type, 1, sentAt));
        return delivery;
    }

    /// <summary>uiarc.dev's add: 0.76 s, then the endpoint (added today by Sofia, no traffic) and a <c>whsec_</c> secret.</summary>
    public static async Task<WebhookEndpointCreated> CreateEndpointAsync(WebhookEndpointDraft draft, CancellationToken token)
    {
        await Task.Delay(760, token);
        var secret = "whsec_" + RandomText(32, "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789");
        var endpoint = Endpoint("new-" + Guid.NewGuid().ToString("N"), draft.Description, draft.Url, "2026-08-01", secret[^4..],
            CurrentUser, new DateTime(2026, 9, 22), draft.Events, new int[48]);
        return new WebhookEndpointCreated(endpoint, secret);
    }

    private static WebhookEndpoint Endpoint(string key, string? name, string url, string version, string tail, string addedBy,
                                            DateTime added, IEnumerable<string> events, int[] volume)
    {
        var endpoint = new WebhookEndpoint
        {
            Key = key,
            Name = name,
            Url = url,
            AddedBy = addedBy,
            AddedOn = new DateTimeOffset(added, TimeSpan.Zero),
            SecretHint = $"whsec_••••{tail}",
            ApiVersion = $"API {version}"
        };
        endpoint.Events.AddRange(events);
        for (var i = 0; i < volume.Length; i++)
        {
            var from = VolumeStart.AddMinutes(30 * i);
            endpoint.Volume.Add(new WebhookVolumeBucket { Start = from, End = from.AddMinutes(30), Total = volume[i] });
        }
        return endpoint;
    }

    private static WebhookDelivery Delivery(string id, string eventId, string type, int status, int milliseconds, string time,
                                            string? response, string data)
    {
        var clock = TimeSpan.ParseExact(time, @"hh\:mm\:ss", CultureInfo.InvariantCulture);
        var sentAt = new DateTimeOffset(2026, 9, 22, 0, 0, 0, TimeSpan.Zero).Add(clock);
        var delivery = new WebhookDelivery
        {
            Key = id,
            EventType = type,
            EventId = eventId,
            StatusCode = status,
            Duration = TimeSpan.FromMilliseconds(milliseconds),
            SentAt = sentAt,
            ResponseBody = response ?? """{"received":true}""",
            Payload = Payload(eventId, type, sentAt, data)
        };
        delivery.Headers.AddRange(Headers(id, eventId, type, 1, sentAt));
        return delivery;
    }

    private static string Payload(string eventId, string type, DateTimeOffset created, string data) =>
        $$"""{"id":"{{eventId}}","type":"{{type}}","created":{{created.ToUnixTimeSeconds()}},"data":{{data}}}""";

    private static WebhookHeader[] Headers(string deliveryId, string eventId, string type, int attempt, DateTimeOffset sentAt) =>
    [
        new("Content-Type", "application/json; charset=utf-8"),
        new("User-Agent", "Halden-Webhooks/2026-08"),
        new("Halden-Event-Id", eventId),
        new("Halden-Delivery-Id", deliveryId),
        new("Halden-Attempt", attempt.ToString(CultureInfo.InvariantCulture)),
        new("Halden-Signature", $"t={sentAt.ToUnixTimeSeconds()},v1={Hex(deliveryId + type, 64)}")
    ];

    /// <summary>The sample body of a test event, by type (test ids, amounts of 2500 or 100000).</summary>
    private static string TestData(string type) => type switch
    {
        "order.paid" => """{"id":"ord_test_4Tq9","object":"order","customer":"cus_test_7Lm2","amount_total":2500,"currency":"usd","items":1}""",
        "order.refunded" => """{"id":"ord_test_4Tq9","object":"order","customer":"cus_test_7Lm2","amount_refunded":2500,"currency":"usd"}""",
        "invoice.paid" => """{"id":"in_test_8Kd2","object":"invoice","number":"TEST-0001","customer":"cus_test_7Lm2","amount_paid":2500,"currency":"usd","status":"paid"}""",
        "invoice.payment_failed" => """{"id":"in_test_8Kd2","object":"invoice","number":"TEST-0001","customer":"cus_test_7Lm2","amount_due":2500,"currency":"usd","attempt_count":1}""",
        "payout.paid" => """{"id":"po_test_3Hv8","object":"payout","amount":100000,"currency":"usd","destination":"ba_test_1Qx4","status":"paid"}""",
        "dispute.created" => """{"id":"dp_test_6Rn1","object":"dispute","charge":"ch_test_2Wp5","amount":2500,"currency":"usd","status":"needs_response"}""",
        _ => """{"id":"obj_test_1","object":"test","amount":2500,"currency":"usd"}"""
    };
}
