using Avalonia.Controls;
using System;

namespace TKCode.Arc.Gallery.Demos;

public partial class DeliveryTrackerPreviewDemo : UserControl
{
    public DeliveryTrackerPreviewDemo()
    {
        InitializeComponent();
        DeliveryTrackerSamples.Apply(Tracker, DateTimeOffset.Now);
        Scan.Click += (_, _) =>
        {
            if (Tracker.Stage == DeliveryStage.Delivered)
            {
                DeliveryTrackerSamples.Apply(Tracker, DateTimeOffset.Now);
                return;
            }
            if (Tracker.Stage == DeliveryStage.Shipped && Tracker.RouteProgress < 0.7)
            {
                Tracker.RouteProgress = 0.72;
                Tracker.Events.Add(new DeliveryEvent(DateTimeOffset.Now, "Arrived at local depot", "Alder Street depot"));
                return;
            }
            Tracker.Advance();
            Tracker.RouteProgress = Tracker.Stage == DeliveryStage.OutForDelivery ? 0.86 : Tracker.RouteProgress;
            Tracker.Events.Add(DeliveryTrackerSamples.EventFor(Tracker.Stage, DateTimeOffset.Now));
            Scan.Content = Tracker.Stage == DeliveryStage.Delivered ? "Start over" : "Next scan";
        };
    }
}
