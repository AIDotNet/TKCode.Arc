using System.Globalization;
using System.Text;

namespace TKCode.Arc.Gallery.Demos;

/// <summary>
/// The Webhooks demos' data (all invented): Corvid, a shipping service, posts events to three endpoints of a
/// fictional store (Shipping sync with three failed deliveries, Help desk, Ops pager), the six event types its form
/// offers, and a just-added endpoint with no traffic yet. Times are relative to now, so the chart ends at "Now".
/// </summary>
public static class WebhooksDemoData
{
    public const string Description =
        "Corvid signs each event and sends it to your endpoints. Failed deliveries are retried for 72 hours, or you can resend them now.";

    public const string CurrentUser = "Ana Duarte";

    /// <summary>The event types the "Add endpoint" form offers; the first one is ticked.</summary>
    public static WebhookEventType[] EventTypes() =>
    [
        new() { Name = "shipment.created", Description = "A label is printed", IsDefaultSelected = true },
        new() { Name = "shipment.delayed", Description = "A carrier reports a delay" },
        new() { Name = "shipment.delivered", Description = "The parcel is signed for" },
        new() { Name = "refund.issued", Description = "Money goes back to a customer" },
        new() { Name = "ticket.opened", Description = "A customer writes in" },
        new() { Name = "booking.cancelled", Description = "A reservation is called off" },
    ];

    /// <summary>The three endpoints with their traffic and recent deliveries, as of <paramref name="now"/>.</summary>
    public static WebhookEndpoint[] Endpoints(DateTimeOffset now)
    {
        var day = Day(now);
        var shipping = new WebhookEndpoint
        {
            Name = "Shipping sync",
            Url = "https://relay.tallowfield.dev/hooks/corvid/v2",
            AddedBy = "Lena Okafor",
            AddedOn = new DateTimeOffset(2026, 2, 11, 9, 0, 0, now.Offset),
            SecretHint = "sig_••••c41e",
            ApiVersion = "Version 2026.09",
        };
        shipping.Events.AddRange(["shipment.created", "shipment.delayed", "shipment.delivered", "refund.issued"]);
        shipping.Deliveries.AddRange(
        [
            Delivery(now, 18, "shipment.created", 200, 128, "{\"ok\":true}", "evt_7Qm2Lx9Rd", 1),
            Delivery(now, 40, "shipment.delayed", 500, 2604, "{\"error\":\"queue_unavailable\",\"detail\":\"worker pool is draining\"}", "evt_3Fh8Wq2Kb", 2),
            Delivery(now, 63, "refund.issued", 200, 91, "{\"ok\":true}", "evt_9Tz4Pn6Ca", 3),
            Delivery(now, 230, "shipment.created", 504, 30000, "gateway gave up after 30 s", "evt_5Gd1Hs7Vx", 4),
            Delivery(now, 265, "shipment.delivered", 200, 117, "{\"ok\":true}", "evt_2Kw6Rb8Nm", 5),
            Delivery(now, 355, "shipment.created", 503, 1733, "{\"error\":\"maintenance_window\"}", "evt_8Lp3Ty5Qe", 6),
            Delivery(now, 372, "shipment.delivered", 200, 140, "{\"ok\":true}", "evt_4Nc7Jv1Xs", 7),
        ]);
        int[] shippingTotals =
        [
            14, 12, 12, 11, 9, 11, 9, 10, 7, 6, 5, 4, 4, 3, 2, 1, 1, 1, 1, 1, 2, 1, 2, 1,
            1, 1, 2, 1, 1, 2, 3, 3, 4, 5, 6, 8, 9, 7, 10, 12, 13, 15, 14, 12, 13, 15, 13, 3,
        ];
        shipping.Volume.AddRange(Buckets(day, shippingTotals));
        // The three open failures sit in the slices their deliveries were sent in.
        foreach (var failed in shipping.Deliveries.Where(d => d.IsFailure))
        {
            var bucket = shipping.Volume.First(b => failed.SentAt >= b.Start && failed.SentAt < b.End);
            bucket.Failed++;
        }

        var desk = new WebhookEndpoint
        {
            Name = "Help desk",
            Url = "https://desk.tallowfield.dev/in/corvid",
            AddedBy = "Tomas Reyes",
            AddedOn = new DateTimeOffset(2026, 5, 2, 14, 0, 0, now.Offset),
            SecretHint = "sig_••••8b07",
            ApiVersion = "Version 2026.06",
        };
        desk.Events.AddRange(["ticket.opened", "ticket.escalated", "member.joined"]);
        desk.Deliveries.AddRange(
        [
            Delivery(now, 9, "ticket.opened", 200, 212, "{\"received\":true}", "evt_6Hx2Mb9Ld", 11),
            Delivery(now, 52, "member.joined", 200, 98, "{\"received\":true}", "evt_1Rv8Kc4Tw", 12),
            Delivery(now, 140, "ticket.escalated", 200, 176, "{\"received\":true}", "evt_7Bn3Qs6Zp", 13),
            Delivery(now, 310, "ticket.opened", 200, 154, "{\"received\":true}", "evt_2Yd5Fw8Gh", 14),
        ]);
        int[] deskTotals =
        [
            6, 5, 5, 4, 4, 3, 3, 2, 2, 1, 1, 1, 0, 0, 0, 1, 0, 0, 0, 0, 1, 0, 1, 1,
            1, 2, 2, 3, 4, 5, 6, 6, 7, 8, 7, 6, 7, 8, 9, 8, 7, 6, 7, 6, 5, 6, 5, 2,
        ];
        desk.Volume.AddRange(Buckets(day, deskTotals));

        var pager = new WebhookEndpoint
        {
            Name = "Ops pager",
            Url = "https://pager.tallowfield.dev/corvid",
            AddedBy = "Mira Patel",
            AddedOn = new DateTimeOffset(2026, 7, 19, 11, 0, 0, now.Offset),
            SecretHint = "sig_••••e5d2",
            ApiVersion = "Version 2026.06",
        };
        pager.Events.AddRange(["booking.cancelled", "plan.changed"]);
        pager.Deliveries.AddRange(
        [
            Delivery(now, 75, "plan.changed", 200, 88, "{\"ok\":true}", "evt_3Js9Dk2Vb", 21),
            Delivery(now, 610, "booking.cancelled", 200, 132, "{\"ok\":true}", "evt_8Wq4Lm7Cn", 22),
        ]);
        var pagerTotals = new int[48];
        pagerTotals[3] = 1;
        pagerTotals[11] = 2;
        pagerTotals[27] = 1;
        pagerTotals[35] = 1;
        pagerTotals[46] = 1;
        pager.Volume.AddRange(Buckets(day, pagerTotals));

        return [shipping, desk, pager];
    }

    /// <summary>An endpoint added a moment ago: no deliveries, an empty chart.</summary>
    public static WebhookEndpoint NewEndpoint(DateTimeOffset now)
    {
        var endpoint = new WebhookEndpoint
        {
            Name = "Inventory mirror",
            Url = "https://stock.tallowfield.dev/in",
            AddedBy = CurrentUser,
            AddedOn = now,
            SecretHint = "sig_••••9d3a",
            ApiVersion = "Version 2026.09",
        };
        endpoint.Events.AddRange(["shipment.created", "shipment.delivered"]);
        endpoint.Volume.AddRange(Buckets(Day(now), new int[48]));
        return endpoint;
    }

    /// <summary>A delivery a live feed could push (200, or a 502 every fourth time).</summary>
    public static WebhookDelivery LiveDelivery(DateTimeOffset now, int sequence, string eventType)
    {
        var failed = sequence % 4 == 3;
        return Delivery(now, 0, eventType, failed ? 502 : 200, failed ? 3120 : 96 + sequence * 7 % 80,
            failed ? "{\"error\":\"upstream_closed\"}" : "{\"ok\":true}", $"evt_live{sequence:000}", 100 + sequence);
    }

    private static DateTimeOffset Day(DateTimeOffset now)
    {
        // 48 half hours ending at the half hour after now.
        var end = new DateTimeOffset(now.Year, now.Month, now.Day, now.Hour, now.Minute >= 30 ? 30 : 0, 0, now.Offset).AddMinutes(30);
        return end.AddHours(-24);
    }

    private static IEnumerable<WebhookVolumeBucket> Buckets(DateTimeOffset start, int[] totals)
    {
        for (var i = 0; i < totals.Length; i++)
        {
            var from = start.AddMinutes(30 * i);
            yield return new WebhookVolumeBucket { Start = from, End = from.AddMinutes(30), Total = totals[i] };
        }
    }

    private static WebhookDelivery Delivery(DateTimeOffset now, int minutesAgo, string type, int code, int milliseconds,
        string response, string eventId, int seed)
    {
        var sentAt = now.AddMinutes(-minutesAgo).AddSeconds(-(seed * 17 % 59));
        var delivery = new WebhookDelivery
        {
            EventType = type,
            EventId = eventId,
            StatusCode = code,
            Duration = TimeSpan.FromMilliseconds(milliseconds),
            SentAt = sentAt,
            ResponseBody = response,
            Payload = Payload(type, eventId, sentAt, seed),
        };
        var deliveryId = "dlv_" + Code(seed * 7919, 7);
        delivery.Headers.AddRange(
        [
            new WebhookHeader("Content-Type", "application/json; charset=utf-8"),
            new WebhookHeader("User-Agent", "Corvid-Hooks/2026.09"),
            new WebhookHeader("Corvid-Event", type),
            new WebhookHeader("Corvid-Delivery", deliveryId),
            new WebhookHeader("Corvid-Attempt", "1"),
            new WebhookHeader("Corvid-Signature", $"t={sentAt.ToUnixTimeSeconds()},v1={Hex(seed, 64)}"),
        ]);
        return delivery;
    }

    private static string Payload(string type, string eventId, DateTimeOffset sentAt, int seed)
    {
        var created = sentAt.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture);
        var kind = type.Split('.')[0];
        var objectJson = kind switch
        {
            "shipment" =>
                $"{{\"id\":\"shp_{Code(seed * 31, 5)}\",\"object\":\"shipment\",\"reference\":\"TF-2026-{400 + seed:0000}\",\"account\":\"acct_Vn42\"," +
                $"\"weight_grams\":{1200 + seed * 430},\"carrier\":\"kestrel\",\"attempt\":1,\"eta\":{sentAt.AddDays(2).ToUnixTimeSeconds()}}}",
            "refund" => $"{{\"id\":\"rf_{Code(seed * 37, 5)}\",\"object\":\"refund\",\"amount\":{1850 + seed * 10},\"currency\":\"eur\",\"reason\":\"damaged_item\"}}",
            "ticket" => $"{{\"id\":\"tk_{Code(seed * 41, 5)}\",\"object\":\"ticket\",\"priority\":\"normal\",\"channel\":\"email\",\"open\":true}}",
            "member" => $"{{\"id\":\"mb_{Code(seed * 43, 5)}\",\"object\":\"member\",\"role\":\"agent\",\"seats\":12}}",
            "booking" => $"{{\"id\":\"bk_{Code(seed * 47, 5)}\",\"object\":\"booking\",\"slot\":\"2026-11-03T09:30\",\"refundable\":false}}",
            _ => $"{{\"id\":\"pl_{Code(seed * 53, 5)}\",\"object\":\"plan\",\"tier\":\"team\",\"seats\":25}}",
        };
        return $"{{\"id\":\"{eventId}\",\"type\":\"{type}\",\"created\":{created},\"version\":\"2026.09\",\"live\":true,\"data\":{{\"object\":{objectJson}}}}}";
    }

    private static string Code(int seed, int length)
    {
        const string alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnpqrstuvwxyz23456789";
        var builder = new StringBuilder();
        var value = (uint)(seed * 2654435761u);
        for (var i = 0; i < length; i++)
        {
            builder.Append(alphabet[(int)(value % (uint)alphabet.Length)]);
            value = value * 1103515245u + 12345u;
        }
        return builder.ToString();
    }

    private static string Hex(int seed, int length)
    {
        var builder = new StringBuilder();
        var value = (uint)(seed * 40503 + 7);
        while (builder.Length < length)
        {
            value = value * 1664525u + 1013904223u;
            builder.Append((value >> 8).ToString("x6", CultureInfo.InvariantCulture));
        }
        return builder.ToString(0, length);
    }
}
