namespace TKCode.Arc;

/// <summary>
/// Invented sample content for the <see cref="DeliveryTracker"/> preview: a parcel with an invented carrier, shipped and
/// on its way, with five scans. Replace it with your own data; none of it is required.
/// </summary>
public static class DeliveryTrackerSamples
{
    /// <summary>Fills <paramref name="tracker"/> with the sample order, relative to <paramref name="now"/>.</summary>
    public static void Apply(DeliveryTracker tracker, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(tracker);
        tracker.OrderNumber = "4821-0937";
        tracker.Carrier = "Larkpost";
        tracker.Stage = DeliveryStage.Shipped;
        tracker.RouteProgress = 0.46;
        tracker.EstimatedArrival = now.AddDays(1).ToString("dddd, MMM d", System.Globalization.CultureInfo.GetCultureInfo("en-US"));
        tracker.ArrivalWindow = "2 – 6 pm";
        tracker.Origin = "Riverside hub";
        tracker.Destination = "214 Alder Street";
        tracker.Events.Clear();
        tracker.Events.AddRange(CreateEvents(now));
    }

    /// <summary>Five scans over the last two days, oldest first.</summary>
    public static IReadOnlyList<DeliveryEvent> CreateEvents(DateTimeOffset now) =>
    [
        new(now.AddHours(-46), "Order placed", "Online"),
        new(now.AddHours(-30), "Packed and labelled", "Fernhill warehouse"),
        new(now.AddHours(-22), "Picked up by Larkpost", "Fernhill warehouse"),
        new(now.AddHours(-9), "Arrived at sorting center", "Riverside hub"),
        new(now.AddHours(-2), "Departed sorting center", "Riverside hub"),
    ];

    /// <summary>The scan the preview adds when it moves the parcel to <paramref name="stage"/>.</summary>
    public static DeliveryEvent EventFor(DeliveryStage stage, DateTimeOffset at) => stage switch
    {
        DeliveryStage.OutForDelivery => new DeliveryEvent(at, "Out for delivery", "Alder Street route"),
        DeliveryStage.Delivered => new DeliveryEvent(at, "Delivered, left at front door", "214 Alder Street"),
        DeliveryStage.Shipped => new DeliveryEvent(at, "In transit", "Riverside hub"),
        DeliveryStage.Packed => new DeliveryEvent(at, "Packed and labelled", "Fernhill warehouse"),
        _ => new DeliveryEvent(at, "Order placed", "Online"),
    };
}
