using Avalonia.Interactivity;

namespace TKCode.Arc;

/// <summary>The steps of a delivery in <see cref="DeliveryTracker"/>, in order.</summary>
public enum DeliveryStage
{
    /// <summary>The order was placed.</summary>
    Ordered,

    /// <summary>The parcel was packed at the warehouse.</summary>
    Packed,

    /// <summary>The carrier has the parcel and it is on its way.</summary>
    Shipped,

    /// <summary>The parcel is on the van to the address.</summary>
    OutForDelivery,

    /// <summary>The parcel was handed over.</summary>
    Delivered
}

/// <summary>One scan or update in a <see cref="DeliveryTracker"/>'s history.</summary>
public sealed class DeliveryEvent
{
    /// <summary>Creates an empty event (set the properties).</summary>
    public DeliveryEvent()
    {
    }

    /// <summary>Creates an event.</summary>
    public DeliveryEvent(DateTimeOffset at, string title, string? location = null)
    {
        At = at;
        Title = title;
        Location = location;
    }

    /// <summary>When it happened.</summary>
    public DateTimeOffset At { get; set; }

    /// <summary>What happened ("Arrived at sorting center").</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>Where it happened ("Millbrook, VT").</summary>
    public string? Location { get; set; }

    /// <summary>True for a problem (a missed attempt, a delay): the event's dot takes the warning color.</summary>
    public bool IsException { get; set; }
}

/// <summary>A <see cref="DeliveryTracker"/> moved to another stage.</summary>
public sealed class DeliveryStageChangedEventArgs : RoutedEventArgs
{
    /// <summary>Creates the arguments.</summary>
    public DeliveryStageChangedEventArgs(RoutedEvent routedEvent, DeliveryStage oldStage, DeliveryStage newStage) : base(routedEvent)
    {
        OldStage = oldStage;
        NewStage = newStage;
    }

    /// <summary>The stage before.</summary>
    public DeliveryStage OldStage { get; }

    /// <summary>The stage now.</summary>
    public DeliveryStage NewStage { get; }
}
