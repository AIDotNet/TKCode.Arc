using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using TKCode.Arc.Motion;

namespace TKCode.Arc.Tests.Blocks.Commerce;

/// <summary><see cref="DeliveryTracker"/>: stages, the route, the history order, the pulse loop and the layout.</summary>
public class DeliveryTrackerTests
{
    private static readonly DateTimeOffset Now = new(new DateTime(2026, 11, 9, 15, 0, 0));

    private static (DeliveryTracker Tracker, Window Window) Show(double width = 900, ArcMotionLevel level = ArcMotionLevel.Off)
    {
        var tracker = new DeliveryTracker { Today = Now.LocalDateTime.Date };
        DeliveryTrackerSamples.Apply(tracker, Now);
        var window = TestHelpers.Show(new Border { Width = width, Child = tracker }, level);
        Pump();
        return (tracker, window);
    }

    private static void Pump()
    {
        for (var i = 0; i < 3; i++)
        {
            Dispatcher.UIThread.RunJobs();
            AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        }
    }

    [AvaloniaFact]
    public void Header_Steps_And_Route_Reflect_The_Order()
    {
        var (tracker, window) = Show();
        try
        {
            TestHelpers.Descendant<TextBlock>(tracker, "PART_OrderLine").Text.ShouldBe("Order #4821-0937 · Larkpost");
            TestHelpers.Descendant<TextBlock>(tracker, "PART_ArrivalHeading").Text.ShouldBe("Estimated arrival");
            var steps = TestHelpers.Descendant<DeliveryTrackerSteps>(tracker);
            steps.Current.ShouldBe(2);
            AutomationProperties.GetName(steps).ShouldBe("Shipped, step 3 of 5");
            var route = TestHelpers.Descendant<DeliveryTrackerRoute>(tracker);
            route.Progress.ShouldBe(0.46);
            AutomationProperties.GetName(route).ShouldBe("Route from Riverside hub to 214 Alder Street, 46% of the way");
            route.Bounds.Width.ShouldBeGreaterThan(100);
            var start = route.PointAt(0);
            var end = route.PointAt(1);
            start.X.ShouldBeLessThan(end.X);
            var mid = route.PointAt(0.5);
            mid.X.ShouldBeGreaterThan(start.X);
            mid.X.ShouldBeLessThan(end.X);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Advancing_Announces_Raises_And_Delivered_Puts_The_Van_At_The_End()
    {
        var (tracker, window) = Show();
        try
        {
            var changes = new List<(DeliveryStage, DeliveryStage)>();
            tracker.StageChanged += (_, e) => changes.Add((e.OldStage, e.NewStage));
            tracker.Advance();
            changes.ShouldBe([(DeliveryStage.Shipped, DeliveryStage.OutForDelivery)]);
            tracker.LastAnnouncement.ShouldBe("Now: Out for delivery");
            TestHelpers.Descendant<DeliveryTrackerRoute>(tracker).IsPulsing.ShouldBeTrue();
            tracker.Advance();
            tracker.Stage.ShouldBe(DeliveryStage.Delivered);
            tracker.EffectiveRouteProgress.ShouldBe(1);
            TestHelpers.Descendant<TextBlock>(tracker, "PART_ArrivalHeading").Text.ShouldBe("Delivered");
            tracker.Advance();
            changes.Count.ShouldBe(2);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void History_Lists_Newest_First_And_Delayed_Pill_Hides_Once_Delivered()
    {
        var (tracker, window) = Show();
        try
        {
            tracker.Events.Add(new DeliveryEvent(Now.AddMinutes(-5), "Arrived at local depot", "Alder Street depot"));
            var rows = TestHelpers.Descendant<StackPanel>(tracker, "PART_History").Children;
            rows.Count.ShouldBe(6);
            AutomationProperties.GetName(rows[0]).ShouldBe("Arrived at local depot, Today, 2:55 PM · Alder Street depot");
            AutomationProperties.GetName(rows[^1]).ShouldStartWith("Order placed");

            tracker.IsDelayed = true;
            var pill = TestHelpers.Descendant<Badge>(tracker, "PART_Delayed");
            pill.IsVisible.ShouldBeTrue();
            tracker.Stage = DeliveryStage.Delivered;
            pill.IsVisible.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Pulse_Runs_Only_In_Full_Motion_Out_For_Delivery()
    {
        var (tracker, window) = Show(level: ArcMotionLevel.Full);
        try
        {
            var route = TestHelpers.Descendant<DeliveryTrackerRoute>(tracker);
            route.IsLoopRunning.ShouldBeFalse();
            tracker.Stage = DeliveryStage.OutForDelivery;
            Pump();
            route.IsLoopRunning.ShouldBeTrue();
            tracker.Stage = DeliveryStage.Delivered;
            Pump();
            route.IsLoopRunning.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Narrow_Block_Puts_The_Route_Above_The_History()
    {
        var (tracker, window) = Show(width: 400);
        try
        {
            tracker.IsCompact.ShouldBeTrue();
            var route = TestHelpers.Descendant<DeliveryTrackerRoute>(tracker);
            var history = TestHelpers.Descendant<StackPanel>(tracker, "PART_History");
            var routeBottom = route.TranslatePoint(new Point(0, route.Bounds.Height), tracker)!.Value.Y;
            var historyTop = history.TranslatePoint(default, tracker)!.Value.Y;
            historyTop.ShouldBeGreaterThan(routeBottom);
        }
        finally
        {
            window.Close();
        }
    }
}
