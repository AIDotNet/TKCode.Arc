using System.Collections.ObjectModel;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;

namespace TKCode.Arc.Tests.Charts;

/// <summary>The split bar: shares, the remainder, the legend, springs, keyboard and the invoked event.</summary>
public class SplitBarTests
{
    private static readonly TimeSpan Frame = TimeSpan.FromMilliseconds(1000.0 / 60);

    private static SplitBar Bar(params (string Label, double Value)[] parts)
    {
        var bar = new SplitBar { Width = 480 };
        foreach (var (label, value) in parts)
        {
            bar.Segments.Add(new SplitBarSegment(label, value));
        }
        return bar;
    }

    private static List<SplitBarLegendItem> Legend(SplitBar bar)
        => TestHelpers.Descendant<WrapPanel>(bar, SplitBar.LegendPart).Children.OfType<SplitBarLegendItem>().ToList();

    [AvaloniaFact]
    public void Shares_Are_Each_Value_Over_The_Sum()
    {
        var bar = Bar(("Rent", 60), ("Food", 30), ("Fun", 10));
        var window = TestHelpers.Show(bar);
        try
        {
            bar.Shares.ShouldBe([0.6, 0.3, 0.1], new SplitBarShareComparer(1e-9));
            bar.RemainderShare.ShouldBe(0, 1e-9);
            var legend = Legend(bar);
            legend.Select(l => l.LabelText).ShouldBe(["Rent", "Food", "Fun"]);
            legend.Select(l => l.PercentText).ShouldBe(["60%", "30%", "10%"]);
            TestHelpers.Descendant<RollingDigits>(bar, SplitBar.SumPart).Value.ShouldBe(100);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Larger_Total_Leaves_The_Rest_Empty_And_Names_It()
    {
        var bar = Bar(("Photos", 50), ("Apps", 25));
        bar.Total = 200;
        bar.ValueSuffix = " GB";
        var window = TestHelpers.Show(bar);
        try
        {
            bar.Shares.ShouldBe([0.25, 0.125], new SplitBarShareComparer(1e-9));
            bar.RemainderShare.ShouldBe(0.625, 1e-9);
            var legend = Legend(bar);
            legend.Count.ShouldBe(3);
            legend[^1].LabelText.ShouldBe("Remaining");
            legend[^1].ShownValue.ShouldBe(125);
            TestHelpers.Descendant<TextBlock>(bar, SplitBar.OfTotalPart).Text.ShouldBe("of 200 GB");
            bar.RemainderLabel = null;
            Legend(bar).Count.ShouldBe(2);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Changing_A_Value_Moves_Every_Share_And_The_Legend()
    {
        var bar = Bar(("A", 50), ("B", 50));
        var window = TestHelpers.Show(bar);
        try
        {
            bar.Segments[0].Value = 150;
            bar.Shares.ShouldBe([0.75, 0.25], new SplitBarShareComparer(1e-9));
            Legend(bar).Select(l => l.PercentText).ShouldBe(["75%", "25%"]);
            Legend(bar)[0].ShownValue.ShouldBe(150);
            AutomationProperties.GetName(TestHelpers.Descendant<SplitBarTrack>(bar)).ShouldBe("A, 150, 75 percent; B, 50, 25 percent");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Under_Full_Motion_Shares_Spring_And_A_Removed_Part_Shrinks_Out()
    {
        var clock = new ManualFrameClock();
        using var install = clock.Install();
        var source = new ObservableCollection<SplitBarSegment> { new("A", 50), new("B", 50) };
        var bar = new SplitBar { Width = 480, ItemsSource = source };
        var window = TestHelpers.Show(bar, ArcMotionLevel.Full);
        try
        {
            clock.Advance(TimeSpan.FromMilliseconds(50), Frame);
            Dispatcher.UIThread.RunJobs();
            source[0].Value = 150;
            clock.Advance(TimeSpan.FromMilliseconds(100), Frame);
            Dispatcher.UIThread.RunJobs();
            var moving = bar.Slots[0].Share.Value(bar.Wall);
            moving.ShouldBeGreaterThan(0.5);
            moving.ShouldBeLessThan(0.75);
            clock.Advance(TimeSpan.FromSeconds(1.5), Frame);
            Dispatcher.UIThread.RunJobs();
            bar.Slots[0].Share.Value(bar.Wall).ShouldBe(0.75, 0.002);

            source.RemoveAt(1);
            bar.Slots.Count.ShouldBe(2, "the removed part stays while it shrinks");
            bar.Slots[1].IsLeaving.ShouldBeTrue();
            bar.Shares.ShouldBe([1.0], new SplitBarShareComparer(1e-9));
            clock.Advance(TimeSpan.FromSeconds(1.5), Frame);
            Dispatcher.UIThread.RunJobs();
            bar.Slots.Count.ShouldBe(1);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void The_Arrow_Keys_Highlight_Parts_And_Enter_Invokes_One()
    {
        var bar = Bar(("A", 10), ("B", 30), ("C", 60));
        var window = TestHelpers.Show(bar);
        try
        {
            SplitBarSegmentEventArgs? invoked = null;
            bar.SegmentInvoked += (_, e) => invoked = e;
            var track = TestHelpers.Descendant<SplitBarTrack>(bar);
            track.Focus(NavigationMethod.Tab);
            Dispatcher.UIThread.RunJobs();
            bar.HighlightedIndex.ShouldBe(0);
            window.KeyPressQwerty(PhysicalKey.ArrowRight, RawInputModifiers.None);
            bar.HighlightedIndex.ShouldBe(1);
            AutomationProperties.GetName(track).ShouldBe("B, 30, 30 percent");
            window.KeyPressQwerty(PhysicalKey.End, RawInputModifiers.None);
            bar.HighlightedIndex.ShouldBe(2);
            window.KeyPressQwerty(PhysicalKey.ArrowRight, RawInputModifiers.None);
            bar.HighlightedIndex.ShouldBe(2, "no wrapping");
            window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
            invoked.ShouldNotBeNull();
            invoked.Index.ShouldBe(2);
            invoked.Segment.Label.ShouldBe("C");
            invoked.Share.ShouldBe(0.6, 1e-9);
            window.KeyPressQwerty(PhysicalKey.Escape, RawInputModifiers.None);
            bar.HighlightedIndex.ShouldBe(-1);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void The_Track_Hits_The_Part_Under_The_Pointer()
    {
        var bar = Bar(("A", 25), ("B", 75));
        var window = TestHelpers.Show(bar);
        try
        {
            var track = TestHelpers.Descendant<SplitBarTrack>(bar);
            var rects = track.PartRects();
            rects.Count.ShouldBe(2);
            (rects[0].Width / (rects[0].Width + rects[1].Width)).ShouldBe(0.25, 0.001);
            track.HitTest(rects[0].Center.X).ShouldBe(0);
            track.HitTest(rects[1].Center.X).ShouldBe(1);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void An_Empty_Bar_Says_So()
    {
        var bar = new SplitBar { Title = "Nothing yet" };
        var window = TestHelpers.Show(bar);
        try
        {
            bar.Classes.Contains(SplitBar.EmptyPseudoClass).ShouldBeTrue();
            AutomationProperties.GetName(bar).ShouldBe("Nothing yet");
            bar.Segments.Add(new SplitBarSegment("One", 1));
            bar.Classes.Contains(SplitBar.EmptyPseudoClass).ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }
}

/// <summary>Compares doubles within a tolerance (Shouldly's sequence comparison).</summary>
internal sealed class SplitBarShareComparer(double tolerance) : IEqualityComparer<double>
{
    public bool Equals(double x, double y) => Math.Abs(x - y) <= tolerance;

    public int GetHashCode(double obj) => 0;
}
