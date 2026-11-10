using System.Globalization;

namespace TKCode.Arc;

/// <summary>The built-in text of <see cref="DeliveryTracker"/>. Replace the whole object to localize the block.</summary>
public sealed class DeliveryTrackerLabels
{
    /// <summary>Culture for times and dates (en-US by default).</summary>
    public CultureInfo Culture { get; init; } = CultureInfo.GetCultureInfo("en-US");

    /// <summary>The name of each stage.</summary>
    public Func<DeliveryStage, string> Stage { get; init; } = stage => stage switch
    {
        DeliveryStage.Packed => "Packed",
        DeliveryStage.Shipped => "Shipped",
        DeliveryStage.OutForDelivery => "Out for delivery",
        DeliveryStage.Delivered => "Delivered",
        _ => "Ordered",
    };

    /// <summary>Over the arrival: "Estimated arrival", or "Delivered" once it has arrived.</summary>
    public Func<DeliveryStage, string> ArrivalHeading { get; init; } = stage => stage == DeliveryStage.Delivered ? "Delivered" : "Estimated arrival";

    /// <summary>The order line: (order number, carrier) ⇒ "Order #4821 · Larkpost".</summary>
    public Func<string, string, string> OrderLine { get; init; } = (order, carrier) =>
        string.IsNullOrEmpty(carrier) ? $"Order #{order}" : $"Order #{order} · {carrier}";

    /// <summary>The pill shown when the delivery is late: "Delayed".</summary>
    public string Delayed { get; init; } = "Delayed";

    /// <summary>Heading of the history: "Tracking history".</summary>
    public string History { get; init; } = "Tracking history";

    /// <summary>Shown when there is no event yet: "No scans yet".</summary>
    public string NoHistory { get; init; } = "No scans yet";

    /// <summary>Screen reader name of the route: (origin, destination, percent) ⇒ "Route from Riverside hub to Home, 62% of the way".</summary>
    public Func<string, string, int, string> RouteName { get; init; } = (from, to, percent) => $"Route from {from} to {to}, {percent}% of the way";

    /// <summary>Screen reader name of the steps: (stage, step, count) ⇒ "Shipped, step 3 of 5".</summary>
    public Func<string, int, int, string> StepsName { get; init; } = (stage, step, count) => $"{stage}, step {step} of {count}";

    /// <summary>Announced when the stage changes: stage ⇒ "Now: Out for delivery".</summary>
    public Func<string, string> StageAnnouncement { get; init; } = stage => $"Now: {stage}";

    /// <summary>The time of an event: (time, today) ⇒ "Today, 9:41 AM" or "Nov 8, 6:02 PM".</summary>
    public Func<DateTimeOffset, DateTime, string> Time { get; init; } = (at, today) =>
    {
        var local = at.LocalDateTime;
        var culture = CultureInfo.GetCultureInfo("en-US");
        var time = local.ToString("h:mm tt", culture);
        if (local.Date == today)
        {
            return "Today, " + time;
        }
        return local.Date == today.AddDays(-1) ? "Yesterday, " + time : local.ToString("MMM d", culture) + ", " + time;
    };
}
