using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Threading;
using TKCode.Arc.Motion;

namespace TKCode.Arc.Tests.Blocks.SocialProof;

public class LogoMarqueeTests
{
    private static Window Show(Control control, double width = 1000, ArcMotionLevel level = ArcMotionLevel.Off)
    {
        var window = TestHelpers.Show(control, level);
        window.Width = width;
        window.Height = 700;
        Dispatcher.UIThread.RunJobs();
        return window;
    }

    [AvaloniaFact]
    public void Defaults_Are_Invented_Marks_And_Arcs_Copy()
    {
        var marquee = new LogoMarquee();
        var window = Show(marquee);
        try
        {
            marquee.Title.ShouldBe("Good work moves between tools");
            marquee.Tone.ShouldBe(LogoMarqueeTone.Color);
            marquee.IsPaused.ShouldBeFalse();
            marquee.Track.ShouldNotBeNull().Brands.ShouldNotBeNull().Count.ShouldBe(7);
            string[] realBrands = ["Figma", "Linear", "Notion", "Slack", "GitHub", "Stripe", "Vercel"];
            marquee.Track.Brands!.Select(b => b.Name).ShouldAllBe(name => !realBrands.Contains(name));
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void At_Full_Motion_It_Travels_One_List_In_39_Seconds_And_Loops()
    {
        var marquee = new LogoMarquee();
        var window = Show(marquee, level: ArcMotionLevel.Full);
        try
        {
            var track = marquee.Track!;
            track.IsStatic.ShouldBeFalse();
            track.CopyCount.ShouldBeGreaterThanOrEqualTo(2);
            track.ListWidth.ShouldBeGreaterThan(0);

            track.Loop.Stop();
            var start = track.Phase;
            track.Step(19.5);
            ((track.Phase - start + 1) % 1).ShouldBe(0.5, 0.0001);
            track.Offset.ShouldBe(-track.Phase * track.ListWidth, 0.01);

            track.Step(19.5);
            track.Phase.ShouldBe(start, 0.0001);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Copies_Always_Fill_The_Width_With_One_To_Spare()
    {
        var marquee = new LogoMarquee { Brands = [new LogoMarqueeBrand { Name = "One", Icon = Geometry.Parse("M0 0h10v10H0z") }] };
        var window = Show(marquee, 1400, ArcMotionLevel.Full);
        try
        {
            var track = marquee.Track!;
            (track.CopyCount * track.ListWidth).ShouldBeGreaterThanOrEqualTo(track.Bounds.Width + track.ListWidth);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Pausing_Holds_The_Place_And_Announces()
    {
        var marquee = new LogoMarquee();
        var window = Show(marquee, level: ArcMotionLevel.Full);
        try
        {
            var track = marquee.Track!;
            track.Loop.Stop();
            track.Step(3.9);
            var place = track.Phase;

            marquee.IsPaused = true;
            Dispatcher.UIThread.RunJobs();
            track.IsHeld.ShouldBeTrue();
            track.Step(10);
            track.Phase.ShouldBe(place);
            marquee.Announcer!.Announcement.ShouldBe("Motion paused");
            AutomationProperties.GetName(marquee.MotionButton!).ShouldBe("Play logo motion");

            marquee.IsPaused = false;
            Dispatcher.UIThread.RunJobs();
            track.IsHeld.ShouldBeFalse();
            AutomationProperties.GetName(marquee.MotionButton!).ShouldBe("Pause logo motion");
            track.Step(3.9);
            track.Phase.ShouldBe(place + 0.1, 0.0001);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void The_Control_Pauses_On_Click()
    {
        var marquee = new LogoMarquee();
        var window = Show(marquee, level: ArcMotionLevel.Full);
        try
        {
            marquee.MotionButton!.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Avalonia.Controls.Button.ClickEvent));
            marquee.IsPaused.ShouldBeTrue();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaTheory]
    [InlineData(ArcMotionLevel.Reduced)]
    [InlineData(ArcMotionLevel.Off)]
    public void Below_Full_Motion_It_Stands_Still_With_Each_Mark_Once(ArcMotionLevel level)
    {
        var marquee = new LogoMarquee();
        var window = Show(marquee, level: level);
        try
        {
            var track = marquee.Track!;
            track.IsStatic.ShouldBeTrue();
            track.CopyCount.ShouldBe(1);
            track.Offset.ShouldBe(0);
            marquee.Classes.Contains(LogoMarquee.StaticPseudoClass).ShouldBeTrue();
            TestHelpers.Descendant<Border>(marquee, "PART_Footer").IsVisible.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Below_640_It_Goes_Narrow()
    {
        var marquee = new LogoMarquee();
        var window = Show(marquee, 500, ArcMotionLevel.Full);
        try
        {
            marquee.Classes.Contains(LogoMarquee.NarrowPseudoClass).ShouldBeTrue();
            marquee.Track!.MarkSize.ShouldBe(20);
            marquee.Track.ItemGap.ShouldBe(40);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Mono_Sets_Its_Pseudo_Class_And_The_Track_Tone()
    {
        var marquee = new LogoMarquee { Tone = LogoMarqueeTone.Mono };
        var window = Show(marquee);
        try
        {
            marquee.Classes.Contains(LogoMarquee.MonoPseudoClass).ShouldBeTrue();
            marquee.Track!.Tone.ShouldBe(LogoMarqueeTone.Mono);
        }
        finally
        {
            window.Close();
        }
    }
}
