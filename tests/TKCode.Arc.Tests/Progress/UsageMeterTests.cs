using TKCode.Arc.Motion;
using Avalonia.Automation;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Threading;

namespace TKCode.Arc.Tests.Progress;

public class UsageMeterTests
{
    private static UsageMeter Storage(out UsageSegment media)
    {
        media = new UsageSegment("media", "Media", 14.2);
        return new UsageMeter
        {
            Label = "Workspace storage",
            Limit = 50,
            Unit = "GB",
            Width = 460,
            Segments = { media, new UsageSegment("docs", "Documents", 9.6), new UsageSegment("backups", "Backups", 6.1), new UsageSegment("other", "Other", 2.4) }
        };
    }

    [Fact]
    public void Status_Follows_The_Limit_And_Warning_Share()
    {
        UsageMeter.StatusFor(10, 50, 0.9).ShouldBe(UsageMeterStatus.Ok);
        UsageMeter.StatusFor(45, 50, 0.9).ShouldBe(UsageMeterStatus.Near);
        UsageMeter.StatusFor(50.1, 50, 0.9).ShouldBe(UsageMeterStatus.Over);
        UsageMeterLegendPanel.ColumnsFor(460, 5).ShouldBe(4);
        UsageMeterLegendPanel.ColumnsFor(200, 5).ShouldBe(1);
        UsageMeterLegendPanel.ColumnsFor(900, 3).ShouldBe(3);
        var steps = UsageMeter.SegmentColors(Colors.Black, Colors.White);
        steps[0].ShouldBe(Colors.Black);
        steps[3].R.ShouldBeGreaterThan(steps[1].R);
    }

    [AvaloniaFact]
    public void Shows_Total_Badge_Caption_And_Legend()
    {
        var meter = Storage(out var media);
        var window = TestHelpers.Show(meter);
        window.Width = 520;
        Dispatcher.UIThread.RunJobs();
        try
        {
            meter.Total.ShouldBe(32.3, 1e-9);
            meter.Status.ShouldBe(UsageMeterStatus.Ok);
            meter.Headline.ShouldBe(32.3, 1e-9);
            meter.BadgeText.ShouldBe("17.7 GB free");
            meter.Caption.ShouldBe("GB of 50 GB used");
            meter.LimitText.ShouldBe("50 GB");
            meter.LegendItems.Count.ShouldBe(5);
            meter.LegendItems[4].Label.ShouldBe("Free");
            meter.LegendItems[4].Value.ShouldBe(17.7, 1e-9);
            AutomationProperties.GetName(meter.LegendItems[0]).ShouldBe("Media, 14.2 GB");
            AutomationProperties.GetName(meter.Bar!).ShouldBe(
                "Workspace storage: 32.3 GB of 50 GB used. Media 14.2 GB, Documents 9.6 GB, Backups 6.1 GB, Other 2.4 GB. 17.7 GB free.");

            media.Value = 30;
            Dispatcher.UIThread.RunJobs();
            meter.Status.ShouldBe(UsageMeterStatus.Near);
            meter.Classes.ShouldContain(UsageMeter.NearPseudoClass);
            meter.BadgeText.ShouldBe("Almost full");

            media.Value = 40;
            Dispatcher.UIThread.RunJobs();
            meter.Status.ShouldBe(UsageMeterStatus.Over);
            meter.BadgeText.ShouldBe("8.1 GB over");
            meter.LegendItems[4].Label.ShouldBe("Over limit");
            meter.Bar!.MarkerOpacity.ShouldBe(1);
            AutomationProperties.GetItemStatus(meter).ShouldBe("Over the plan limit by 8.1 GB.");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Pinning_Previews_A_Category_Until_Escape()
    {
        var meter = Storage(out _);
        var window = TestHelpers.Show(meter);
        window.Width = 520;
        Dispatcher.UIThread.RunJobs();
        try
        {
            var items = meter.LegendItems;
            items[0].IsTabStop.ShouldBeTrue();
            items[1].IsTabStop.ShouldBeFalse();
            items[0].Focus(NavigationMethod.Tab);
            Dispatcher.UIThread.RunJobs();
            meter.ActiveKey.ShouldBe("media");
            meter.Caption.ShouldBe("GB in Media · 28% of plan");
            meter.Headline.ShouldBe(14.2, 1e-9);
            items[1].IsDimmed.ShouldBeTrue();

            window.KeyPressQwerty(PhysicalKey.ArrowRight, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
            items[1].IsFocused.ShouldBeTrue();
            meter.ActiveKey.ShouldBe("docs");
            window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
            meter.PinnedKey.ShouldBe("docs");
            items[1].IsChecked.ShouldBe(true);

            window.KeyPressQwerty(PhysicalKey.End, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
            items[4].IsFocused.ShouldBeTrue();
            meter.Caption.ShouldBe("GB free");
            window.KeyPressQwerty(PhysicalKey.Escape, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
            meter.PinnedKey.ShouldBeNull();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void First_Reveal_Waits_For_The_Meter_To_Be_Seen()
    {
        var clock = new ManualFrameClock();
        using var install = clock.Install();
        var meter = Storage(out _);
        var window = TestHelpers.Show(meter, ArcMotionLevel.Full);
        window.Width = 520;
        Dispatcher.UIThread.RunJobs();
        try
        {
            clock.Advance(TimeSpan.FromMilliseconds(100));
            meter.Bar!.Used.ShouldBeGreaterThan(0);
            meter.Bar.Used.ShouldBeLessThan(32.3);
            clock.Advance(TimeSpan.FromSeconds(2));
            meter.Bar.Used.ShouldBe(32.3, 0.01);
        }
        finally
        {
            window.Close();
        }
    }
}
