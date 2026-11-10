using Avalonia.Controls;
using System;

namespace TKCode.Arc.Gallery.Demos;

public partial class DeliveryTrackerNarrowDemo : UserControl
{
    public DeliveryTrackerNarrowDemo()
    {
        InitializeComponent();
        foreach (var item in DeliveryTrackerSamples.CreateEvents(DateTimeOffset.Now.AddHours(-20)))
        {
            Tracker.Events.Add(item);
        }
        Tracker.Events.Add(new DeliveryEvent(DateTimeOffset.Now.AddHours(-3), "Delivery attempt missed", "Home") { IsException = true });
        Tracker.Events.Add(DeliveryTrackerSamples.EventFor(DeliveryStage.Delivered, DateTimeOffset.Now));
    }
}
