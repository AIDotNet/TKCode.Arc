using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace TKCode.Arc.Tests.Expand;

public class ExpandableTextTests
{
    private const string Long =
        "We moved our whole design team onto the new workspace in a single afternoon. The import kept every comment and version, " +
        "and the shared libraries meant nobody had to rebuild components from scratch. Two weeks in, review cycles are noticeably " +
        "shorter: feedback lands on the exact frame and threads resolve themselves when the change ships.";

    private static ExpandableTextViewport Viewport(ExpandableText text) => text.GetVisualDescendants().OfType<ExpandableTextViewport>().First();

    private static ExpandableTextToggle Toggle(ExpandableText text) => text.GetVisualDescendants().OfType<ExpandableTextToggle>().First();

    [AvaloniaFact]
    public void Long_Text_Clamps_To_Three_Lines_With_A_Toggle()
    {
        var text = new ExpandableText { Text = Long, Width = 320 };
        var window = TestHelpers.Show(text);
        try
        {
            text.IsOverflowing.ShouldBeTrue();
            Viewport(text).Bounds.Height.ShouldBe(3 * 14 * 1.4, 1);
            Toggle(text).IsVisible.ShouldBeTrue();
            Viewport(text).OpacityMask.ShouldNotBeNull();
            Avalonia.Automation.AutomationProperties.GetName(Toggle(text)).ShouldBe("Read more");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Short_Text_Has_No_Toggle_And_No_Fade()
    {
        var text = new ExpandableText { Text = "Fits on one line.", Width = 320 };
        var window = TestHelpers.Show(text);
        try
        {
            text.IsOverflowing.ShouldBeFalse();
            Toggle(text).IsVisible.ShouldBeFalse();
            Viewport(text).OpacityMask.ShouldBeNull();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Toggle_Opens_To_Full_Height_And_Raises_The_Event()
    {
        var text = new ExpandableText { Text = Long, Width = 320 };
        var states = new List<bool>();
        text.ExpandedChanged += (_, e) => states.Add(e.IsExpanded);
        var window = TestHelpers.Show(text);
        try
        {
            Toggle(text).Focus(NavigationMethod.Tab);
            window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
            text.IsExpanded.ShouldBeTrue();
            var viewport = Viewport(text);
            viewport.Bounds.Height.ShouldBeGreaterThan(3 * 14 * 1.4 + 1);
            viewport.OpacityMask.ShouldBeNull();
            Avalonia.Automation.AutomationProperties.GetName(Toggle(text)).ShouldBe("Show less");
            Toggle(text).Classes.Contains(":expanded").ShouldBeTrue();
            text.IsExpanded = false;
            Dispatcher.UIThread.RunJobs();
            viewport.Bounds.Height.ShouldBe(3 * 14 * 1.4, 1);
            states.ShouldBe([true, false]);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Lines_Sets_The_Clamp()
    {
        var text = new ExpandableText { Text = Long, Width = 320, Lines = 2 };
        var window = TestHelpers.Show(text);
        try
        {
            Viewport(text).Bounds.Height.ShouldBe(2 * 14 * 1.4, 1);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Focusing_A_Link_Below_The_Clamp_Opens_The_Text()
    {
        var link = new Button { Content = "privacy policy" };
        var text = new ExpandableText { Width = 320 };
        var inlines = new InlineCollection { new Run(Long + " Read the ") };
        inlines.Add(new InlineUIContainer(link));
        text.Inlines = inlines;
        var window = TestHelpers.Show(text);
        try
        {
            text.IsOverflowing.ShouldBeTrue();
            link.Focus(NavigationMethod.Tab);
            Dispatcher.UIThread.RunJobs();
            text.IsExpanded.ShouldBeTrue();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Opening_Glides_With_Full_Motion_And_Reverses_Mid_Flight()
    {
        var clock = new Motion.ManualFrameClock();
        using var _ = clock.Install();
        var text = new ExpandableText { Text = Long, Width = 320 };
        var window = TestHelpers.Show(text, Motion.ArcMotionLevel.Full);
        try
        {
            clock.Advance(TimeSpan.FromMilliseconds(50));
            Dispatcher.UIThread.RunJobs();
            var viewport = Viewport(text);
            var clamp = viewport.Bounds.Height;
            text.IsExpanded = true;
            clock.Advance(TimeSpan.FromMilliseconds(150));
            Dispatcher.UIThread.RunJobs();
            var mid = viewport.Bounds.Height;
            mid.ShouldBeGreaterThan(clamp);
            text.IsExpanded = false;
            Dispatcher.UIThread.RunJobs();
            clock.Advance(TimeSpan.FromMilliseconds(16));
            Dispatcher.UIThread.RunJobs();
            // The reversal starts from the current height, not from either end.
            viewport.Bounds.Height.ShouldBeGreaterThan(clamp);
            viewport.Bounds.Height.ShouldBeLessThanOrEqualTo(mid + 0.5);
            clock.Advance(TimeSpan.FromSeconds(1));
            Dispatcher.UIThread.RunJobs();
            viewport.Bounds.Height.ShouldBe(clamp, 0.01);
        }
        finally
        {
            window.Close();
        }
    }
}
