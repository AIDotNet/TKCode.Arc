using System.Collections.ObjectModel;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Threading;
using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;

namespace TKCode.Arc.Tests.Charts;

/// <summary>
/// The split bar: shares, the remainder, highlighting that widens a part, labels inside or in the lane, switching parts
/// off from the legend and the keyboard, the readout, springs and colors.
/// </summary>
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
            legend.Select(l => l.PercentText).ShouldBe(["60.0%", "30.0%", "10.0%"]);
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
            legend[^1].Focusable.ShouldBeFalse("the rest is not a part that can be switched off");
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
        bar.Title = "Mix";
        var window = TestHelpers.Show(bar);
        try
        {
            bar.Segments[0].Value = 150;
            bar.Shares.ShouldBe([0.75, 0.25], new SplitBarShareComparer(1e-9));
            Legend(bar).Select(l => l.PercentText).ShouldBe(["75.0%", "25.0%"]);
            Legend(bar)[0].ShownValue.ShouldBe(150);
            AutomationProperties.GetName(TestHelpers.Descendant<SplitBarTrack>(bar))
                .ShouldBe("Mix: A 75.0%, B 25.0%. Use the arrow keys to read each share.");
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
    public void The_Edges_Ride_A_Spring_That_Settles_In_About_Four_Tenths_Of_A_Second()
    {
        var clock = new ManualFrameClock();
        using var install = clock.Install();
        var bar = Bar(("A", 50), ("B", 50));
        var window = TestHelpers.Show(bar, ArcMotionLevel.Full);
        try
        {
            clock.Advance(TimeSpan.FromMilliseconds(50), Frame);
            Dispatcher.UIThread.RunJobs();
            bar.Segments[0].Value = 150;
            clock.Advance(TimeSpan.FromMilliseconds(200), Frame);
            Dispatcher.UIThread.RunJobs();
            // Arc Split bar (design reference): about 85% of the way after 0.2 s, settled by 0.45 s, no visible overshoot.
            var progress = (bar.Slots[0].Share.Value(bar.Wall) - 0.5) / 0.25;
            progress.ShouldBeInRange(0.75, 0.92);
            clock.Advance(TimeSpan.FromMilliseconds(250), Frame);
            Dispatcher.UIThread.RunJobs();
            bar.Slots[0].Share.Value(bar.Wall).ShouldBe(0.75, 0.004);
            bar.Slots[0].Share.Value(bar.Wall).ShouldBeLessThan(0.7525);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Reduced_Motion_Moves_At_Once()
    {
        var bar = Bar(("A", 50), ("B", 50));
        var window = TestHelpers.Show(bar, ArcMotionLevel.Reduced);
        try
        {
            bar.Segments[0].Value = 150;
            bar.Slots[0].Share.Value(bar.Wall).ShouldBe(0.75, 1e-9);
            bar.HighlightedIndex = 1;
            bar.Slots[0].Dim.Value(bar.Wall).ShouldBe(1);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Highlighting_Widens_The_Part_And_The_Others_Give_Way()
    {
        var bar = Bar(("Alpha", 50), ("Beta", 30), ("Gamma", 20));
        var window = TestHelpers.Show(bar);
        try
        {
            bar.HighlightedIndex = 1;
            bar.Classes.Contains(SplitBar.HighlightedPseudoClass).ShouldBeTrue();
            bar.Slots[1].TargetShare.ShouldBe(0.345, 1e-9, "a share of 0.30 gains 4.5% of the bar");
            var rest = (1 - 0.345) / 0.7;
            bar.Slots[0].TargetShare.ShouldBe(0.5 * rest, 1e-9);
            bar.Slots[2].TargetShare.ShouldBe(0.2 * rest, 1e-9);
            bar.Slots.Sum(s => s.TargetShare).ShouldBe(1, 1e-9);
            bar.Slots[0].Dim.Value(bar.Wall).ShouldBe(1);
            bar.Slots[1].Dim.Value(bar.Wall).ShouldBe(0);
            bar.Slots[1].Fill.Value(bar.Wall).ShouldBe(1);
            bar.Shares.ShouldBe([0.5, 0.3, 0.2], new SplitBarShareComparer(1e-9));
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Small_Part_Widens_Enough_To_Show_Its_Own_Label()
    {
        var bar = Bar(("Alpha", 90), ("Beta", 9), ("Gamma", 1));
        var window = TestHelpers.Show(bar);
        try
        {
            var gamma = bar.Slots[2];
            gamma.Mode.ShouldBe(SplitBarLabelMode.Lane);
            bar.HighlightedIndex = 2;
            var width = TestHelpers.Descendant<SplitBarTrack>(bar).BarWidth;
            (gamma.TargetShare * width).ShouldBeGreaterThan(bar.MeasureName(gamma) + 5 + bar.MeasureShare(0.01) + 24);
            gamma.TargetShare.ShouldBeGreaterThan(0.01 + 0.045);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Labels_Sit_Inside_When_They_Fit_And_Move_To_The_Lane_When_They_Do_Not()
    {
        // Wide, so the test font's wide figures still leave the "share only" case in between.
        var bar = Bar(("Wide part", 63.5), ("Mid", 28), ("Tiny", 6.5), ("Sliver", 2));
        bar.Width = 1600;
        var window = TestHelpers.Show(bar);
        try
        {
            bar.Slots[0].Mode.ShouldBe(SplitBarLabelMode.Full);
            bar.Slots[1].Mode.ShouldBe(SplitBarLabelMode.Full);
            bar.Slots[2].Mode.ShouldBe(SplitBarLabelMode.Share, "about 100 px holds the share, not the name");
            bar.Slots[3].Mode.ShouldBe(SplitBarLabelMode.Lane);
            bar.LaneHeight.ShouldBe(40);
            var track = TestHelpers.Descendant<SplitBarTrack>(bar);
            track.Bounds.Height.ShouldBe(44 + 12 + 28, "the lane adds its height less the gap it overlaps");
            bar.Slots[3].LaneLabel.ShouldNotBeNull().NameText.ShouldBe("Sliver");
            bar.Slots[3].LaneLabel!.ShareValue.ShouldBe(2.0);
            bar.Slots[0].InsideLabel.ShouldNotBeNull().Open.ShouldBe(1);

            bar.Segments[3].IsHidden = true;
            bar.LaneHeight.ShouldBe(0, "no label left outside: the lane closes");
            Dispatcher.UIThread.RunJobs();
            track.Bounds.Height.ShouldBe(44 + 12);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Lane_Labels_Are_Pushed_Apart_And_Kept_Inside_The_Bar()
    {
        var bar = Bar(("Main", 94), ("One", 2), ("Two", 2), ("Three", 2));
        var window = TestHelpers.Show(bar);
        try
        {
            var lane = bar.Slots.Skip(1).ToList();
            lane.ShouldAllBe(s => s.Mode == SplitBarLabelMode.Lane);
            var width = TestHelpers.Descendant<SplitBarTrack>(bar).BarWidth;
            for (var i = 0; i < lane.Count; i++)
            {
                var label = lane[i].LaneLabel.ShouldNotBeNull();
                var x = lane[i].OutsideX.Value(bar.Wall);
                x.ShouldBeGreaterThanOrEqualTo(0);
                (x + label.DesiredSize.Width).ShouldBeLessThanOrEqualTo(width - 4 + 1);
                if (i > 0)
                {
                    var previous = lane[i - 1];
                    x.ShouldBeGreaterThanOrEqualTo(previous.OutsideX.Value(bar.Wall) + previous.LaneLabel!.DesiredSize.Width + 4 - 1);
                }
            }
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void The_Legend_Switches_A_Part_Off_And_Back_On()
    {
        var bar = Bar(("A", 50), ("B", 30), ("C", 20));
        var window = TestHelpers.Show(bar);
        try
        {
            var changes = new List<(string? Label, bool Hidden)>();
            bar.HiddenChanged += (_, e) => changes.Add((e.Segment.Label, e.IsHidden));
            var b = Legend(bar)[1];
            b.Toggle();
            bar.Segments[1].IsHidden.ShouldBeTrue();
            bar.Shares.ShouldBe([50 / 70.0, 0, 20 / 70.0], new SplitBarShareComparer(1e-9));
            b.PercentText.ShouldBe("Off");
            b.IsOff.ShouldBeTrue();
            AutomationProperties.GetName(b).ShouldBe("B, hidden");
            bar.Slots[1].TargetShare.ShouldBe(0);
            bar.LastAnnouncement.ShouldBe("B hidden.");
            TestHelpers.Descendant<RollingDigits>(bar, SplitBar.SumPart).Value.ShouldBe(70);
            AutomationProperties.GetName(TestHelpers.Descendant<SplitBarTrack>(bar))
                .ShouldBe("A 71.4%, C 28.6%. Use the arrow keys to read each share. B hidden.");

            b.Toggle();
            bar.Segments[1].IsHidden.ShouldBeFalse();
            b.PercentText.ShouldBe("30.0%");
            AutomationProperties.GetName(b).ShouldBe("B, 30.0%");
            bar.LastAnnouncement.ShouldBe("B shown.");
            changes.ShouldBe([("B", true), ("B", false)]);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void The_Last_Part_That_Is_On_Stays_On()
    {
        var clock = new ManualFrameClock();
        using var install = clock.Install();
        var bar = Bar(("A", 50), ("B", 50));
        var window = TestHelpers.Show(bar, ArcMotionLevel.Full);
        try
        {
            clock.Advance(TimeSpan.FromMilliseconds(50), Frame);
            Legend(bar)[1].Toggle();
            clock.Advance(TimeSpan.FromSeconds(1), Frame);
            var a = Legend(bar)[0];
            a.Toggle();
            bar.Segments[0].IsHidden.ShouldBeFalse();
            bar.LastAnnouncement.ShouldBe("A is the only share shown, so it stays on.");
            clock.Advance(TimeSpan.FromMilliseconds(100), Frame);
            Dispatcher.UIThread.RunJobs();
            Math.Abs(a.ShakeOffset).ShouldBeGreaterThan(0.5, "the entry shakes");
            clock.Advance(TimeSpan.FromMilliseconds(400), Frame);
            Dispatcher.UIThread.RunJobs();
            a.ShakeOffset.ShouldBe(0);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void The_Arrow_Keys_Read_Each_Share_And_Enter_Switches_It_Off()
    {
        var bar = Bar(("A", 10), ("B", 30), ("C", 60));
        bar.Unit = "votes";
        var window = TestHelpers.Show(bar);
        try
        {
            SplitBarSegmentEventArgs? invoked = null;
            bar.SegmentInvoked += (_, e) => invoked = e;
            var track = TestHelpers.Descendant<SplitBarTrack>(bar);
            track.Focus(NavigationMethod.Tab);
            Dispatcher.UIThread.RunJobs();
            bar.HighlightedIndex.ShouldBe(-1, "focus alone highlights nothing");
            window.KeyPressQwerty(PhysicalKey.ArrowRight, RawInputModifiers.None);
            bar.HighlightedIndex.ShouldBe(0);
            window.KeyPressQwerty(PhysicalKey.ArrowRight, RawInputModifiers.None);
            bar.HighlightedIndex.ShouldBe(1);
            bar.LastAnnouncement.ShouldBe("B, 30.0%, 30 votes.");
            window.KeyPressQwerty(PhysicalKey.End, RawInputModifiers.None);
            bar.HighlightedIndex.ShouldBe(2);
            window.KeyPressQwerty(PhysicalKey.ArrowRight, RawInputModifiers.None);
            bar.HighlightedIndex.ShouldBe(0, "the arrows wrap");
            window.KeyPressQwerty(PhysicalKey.ArrowLeft, RawInputModifiers.None);
            bar.HighlightedIndex.ShouldBe(2);

            window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
            invoked.ShouldNotBeNull();
            invoked.Index.ShouldBe(2);
            invoked.Segment.Label.ShouldBe("C");
            invoked.Share.ShouldBe(0.6, 1e-9);
            bar.Segments[2].IsHidden.ShouldBeTrue("an unhandled Enter switches the part off");
            bar.HighlightedIndex.ShouldBe(-1);
            bar.LastAnnouncement.ShouldBe("C hidden.");
            window.KeyPressQwerty(PhysicalKey.ArrowRight, RawInputModifiers.None);
            bar.HighlightedIndex.ShouldBe(0, "the next arrow goes on from where the part was");

            window.KeyPressQwerty(PhysicalKey.Escape, RawInputModifiers.None);
            bar.HighlightedIndex.ShouldBe(-1);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Handled_SegmentInvoked_Keeps_The_Part_On()
    {
        var bar = Bar(("A", 40), ("B", 60));
        var window = TestHelpers.Show(bar);
        try
        {
            bar.SegmentInvoked += (_, e) => e.Handled = true;
            TestHelpers.Descendant<SplitBarTrack>(bar).Focus(NavigationMethod.Tab);
            window.KeyPressQwerty(PhysicalKey.ArrowRight, RawInputModifiers.None);
            window.KeyPressQwerty(PhysicalKey.Space, RawInputModifiers.None);
            bar.Segments[0].IsHidden.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void The_Readout_Shows_The_Total_Or_The_Highlighted_Part()
    {
        var bar = Bar(("North", 300), ("South", 100));
        bar.Unit = "orders";
        bar.TotalLabel = "All";
        var window = TestHelpers.Show(bar);
        try
        {
            bar.ReadoutLine.ShouldBe(0);
            TestHelpers.Descendant<TextBlock>(bar, "PART_TotalLabel").Text.ShouldBe("All");
            TestHelpers.Descendant<TextBlock>(bar, "PART_Unit").Text.ShouldBe("orders");
            bar.HighlightedIndex = 1;
            bar.ReadoutLine.ShouldNotBe(0);
            var line = bar.ReadoutSegmentLine.ShouldNotBeNull();
            line.NameText.ShouldBe("South");
            line.ShareValue.ShouldBe(25);
            line.ValueValue.ShouldBe(100);
            bar.HighlightedIndex = -1;
            bar.ReadoutLine.ShouldBe(0);
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
            (rects[0].Width / (rects[0].Width + rects[1].Width)).ShouldBe(0.25, 0.005);
            (rects[1].Left - rects[0].Right).ShouldBe(2, 1e-9, "a 2 px gap between parts");
            track.HitTest(rects[0].Center.X).ShouldBe(0);
            track.HitTest(rects[1].Center.X).ShouldBe(1);
            bar.Segments[1].IsHidden = true;
            track.PartRects()[1].Width.ShouldBe(0);
            track.HitTest(track.BarWidth - 2).ShouldBe(0, "with B off, A fills the bar");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Pointing_At_A_Part_Highlights_It_And_Leaving_Clears_It()
    {
        var bar = Bar(("A", 50), ("B", 50));
        var window = TestHelpers.Show(bar);
        try
        {
            var track = TestHelpers.Descendant<SplitBarTrack>(bar);
            var rects = track.PartRects();
            window.MouseMove(track.TranslatePoint(rects[1].Center, window)!.Value);
            bar.HighlightedIndex.ShouldBe(1);
            window.MouseMove(new Point(1, window.Bounds.Height - 1));
            bar.HighlightedIndex.ShouldBe(-1);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Fills_Mix_The_Series_Color_Into_The_Surface_Like_Arc()
    {
        // Arc Split bar (design reference), dark theme: the orange series over the surface at 42% and 62%.
        var surface = new SolidColorBrush(Color.Parse("#191919"));
        var orange = new SolidColorBrush(Color.Parse("#E66E00"));
        Near(((ISolidColorBrush)SplitBarColors.Mix(orange, surface, 0.42)).Color, Color.FromRgb(105, 61, 4));
        var teal = new SolidColorBrush(Color.Parse("#009E84"));
        var white = new SolidColorBrush(Colors.White);
        Near(((ISolidColorBrush)SplitBarColors.Mix(teal, white, 0.42)).Color, Color.FromRgb(128, 216, 201));
        Near(((ISolidColorBrush)SplitBarColors.Mix(new SolidColorBrush(Color.Parse("#9C37BE")), surface, 0.62)).Color,
            Color.FromRgb(103, 48, 123));

        static void Near(Color actual, Color expected)
        {
            Math.Abs(actual.R - expected.R).ShouldBeLessThanOrEqualTo(1, actual.ToString());
            Math.Abs(actual.G - expected.G).ShouldBeLessThanOrEqualTo(1, actual.ToString());
            Math.Abs(actual.B - expected.B).ShouldBeLessThanOrEqualTo(1, actual.ToString());
        }
    }

    [AvaloniaFact]
    public void A_New_Palette_Reaches_The_Legend()
    {
        var bar = Bar(("A", 50), ("B", 50));
        var window = TestHelpers.Show(bar);
        try
        {
            var brush = new SolidColorBrush(Colors.Red);
            TestHelpers.Descendant<SplitBarTrack>(bar).Series1 = brush;
            TestHelpers.Descendant<SplitBarSwatch>(Legend(bar)[0]).Brush.ShouldBeSameAs(brush);
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
