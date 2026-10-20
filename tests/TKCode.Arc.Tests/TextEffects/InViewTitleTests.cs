using TKCode.Arc.Motion;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;

namespace TKCode.Arc.Tests.TextEffects;

public class InViewTitleTests
{
    [Fact]
    public void In_View_Title_Timing_Follows_Arc()
    {
        InViewTitle.WordStep(4).ShouldBe(0.06, 1e-9);
        InViewTitle.WordStep(10).ShouldBe(0.04, 1e-9);
        InViewTitle.DelayFor(InViewTitleVariant.Line, 2, 3).ShouldBe(0.16, 1e-9);
        InViewTitle.DelayFor(InViewTitleVariant.Tracking, 3, 5).ShouldBe(0.096, 1e-9);
        InViewTitle.DelayFor(InViewTitleVariant.Wipe, 0, 1).ShouldBe(0);
        InViewTitle.SettleFor(InViewTitleVariant.Line).ShouldBe(0.86);
        InViewTitle.SettleFor(InViewTitleVariant.Blur).ShouldBe(0.8);
    }

    [AvaloniaFact]
    public void In_View_Title_Reveals_When_Seen()
    {
        var clock = new ManualFrameClock();
        using var _ = clock.Install();
        var title = new InViewTitle { Text = "Lines rise", Variant = InViewTitleVariant.Line, Lines = ["Lines rise", "one by one"] };
        var below = new InViewTitle { Text = "Far below", Variant = InViewTitleVariant.Word };
        var scroller = new ScrollViewer
        {
            Height = 200,
            Content = new StackPanel { Children = { title, new Border { Height = 600 }, below } }
        };
        var window = TestHelpers.Show(scroller, ArcMotionLevel.Full);
        try
        {
            title.IsRevealed.ShouldBeTrue();
            below.IsRevealed.ShouldBeFalse();
            title.Bounds.Height.ShouldBe(2 * title.FontSize * title.Leading, 1);
            AutomationProperties.GetName(title).ShouldBe("Lines rise");
            scroller.Offset = new Avalonia.Vector(0, 600);
            Dispatcher.UIThread.RunJobs();
            below.IsRevealed.ShouldBeTrue();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void In_View_Title_Shows_At_Once_Under_Reduced_Motion()
    {
        var title = new InViewTitle { Text = "Tracking opens up", Variant = InViewTitleVariant.Tracking, Level = TextLevel.H1 };
        var window = TestHelpers.Show(title, ArcMotionLevel.Reduced);
        try
        {
            title.IsRevealed.ShouldBeTrue();
            AutomationProperties.GetHeadingLevel(title).ShouldBe(1);
        }
        finally
        {
            window.Close();
        }
    }
}
