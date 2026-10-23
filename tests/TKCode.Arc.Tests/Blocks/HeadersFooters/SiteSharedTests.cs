using Avalonia;
using Avalonia.Automation;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using TKCode.Arc.Motion;

namespace TKCode.Arc.Tests.Blocks.HeadersFooters;

public class SiteSharedTests
{
    private static Window Show(Control control, ArcMotionLevel level = ArcMotionLevel.Off)
    {
        var window = TestHelpers.Show(control, level);
        window.Width = 600;
        window.Height = 400;
        Dispatcher.UIThread.RunJobs();
        return window;
    }

    [Theory]
    [InlineData("ada@example.com", true)]
    [InlineData("  ada.lin@mail.example.org  ", true)]
    [InlineData("ada@example.c", false)]
    [InlineData("ada example@example.com", false)]
    [InlineData("ada@@example.com", false)]
    [InlineData("ada", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void Email_Follows_Arcs_Pattern_On_The_Trimmed_Text(string? text, bool valid)
    {
        SiteEmail.IsValid(text).ShouldBe(valid);
    }

    [Theory]
    [InlineData("https://example.com/pricing", true)]
    [InlineData("http://example.com", true)]
    [InlineData("mailto:team@example.com", true)]
    [InlineData("/pricing", false)]
    [InlineData("#privacy", false)]
    [InlineData("javascript:alert(1)", false)]
    [InlineData("file:///etc/hosts", false)]
    [InlineData(null, false)]
    public void Only_Absolute_Web_And_Mail_Addresses_Are_Launched(string? href, bool launchable)
    {
        (SiteLinks.ToLaunchable(href) is not null).ShouldBe(launchable);
    }

    [Fact]
    public void Brand_Palette_Keeps_Arcs_Fixed_Hues_In_Both_Themes()
    {
        var light = SiteBrandPalette.For(false);
        var dark = SiteBrandPalette.For(true);
        Oklch.FromColor(light.From).L.ShouldBe(0.90, 0.01);
        Oklch.FromColor(light.To).L.ShouldBe(0.92, 0.01);
        Oklch.FromColor(dark.From).L.ShouldBe(0.38, 0.01);
        Oklch.FromColor(dark.SoftTo).L.ShouldBe(0.24, 0.01);
        // Violet on the left, amber on the right: the blue channel leads at the start, the red channel at the end.
        light.From.B.ShouldBeGreaterThan(light.From.R);
        light.To.R.ShouldBeGreaterThan(light.To.B);
    }

    [AvaloniaFact]
    public void Arc_Mark_Is_A_Decorative_22px_Glyph()
    {
        var mark = new SiteArcMark { Foreground = Brushes.Black };
        var window = Show(new StackPanel { Children = { mark } });
        try
        {
            mark.Bounds.Width.ShouldBe(22);
            mark.Bounds.Height.ShouldBe(22);
            mark.IsHitTestVisible.ShouldBeFalse();
            AutomationProperties.GetAccessibilityView(mark).ShouldBe(AccessibilityView.Raw);
            SiteArcMark.Geometry.Bounds.Width.ShouldBeGreaterThan(40);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Face_Stack_Is_As_Wide_As_Its_Overlapping_Faces()
    {
        var images = new AvaloniaList<IImage>(Enumerable.Range(0, 4).Select(_ => (IImage)new RenderTargetBitmap(new PixelSize(8, 8))));
        var stack = new SiteFaceStack { Images = images, Diameter = 26, Overlap = 6, HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Left };
        var window = Show(new StackPanel { Children = { stack } });
        try
        {
            // Four 26px faces, each overlapping the previous one by 6: 26 + 3 × 20.
            stack.Bounds.Width.ShouldBe(86);
            stack.Bounds.Height.ShouldBe(26);

            stack.MaxCount = 3;
            window.UpdateLayout();
            stack.Bounds.Width.ShouldBe(66);

            images.Clear();
            window.UpdateLayout();
            stack.Bounds.Width.ShouldBe(0);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Swap_Replaces_At_Once_When_Motion_Is_Off()
    {
        var swap = new SiteSwap { Content = "One" };
        var window = Show(new StackPanel { Children = { swap } });
        try
        {
            swap.Content = "Two";
            Dispatcher.UIThread.RunJobs();
            swap.ExitingCount.ShouldBe(0);
            swap.CurrentPresenter!.Content.ShouldBe("Two");
            swap.CurrentPresenter.Opacity.ShouldBe(1);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Swap_Lifts_The_Old_Line_Away_While_The_New_One_Rises()
    {
        var clock = new ManualFrameClock();
        using var installed = clock.Install();
        var swap = new SiteSwap { Content = "Note" };
        var window = Show(new StackPanel { Children = { swap } }, ArcMotionLevel.Full);
        try
        {
            swap.Content = "Error";
            swap.ExitingCount.ShouldBe(1);
            var incoming = swap.CurrentPresenter!;
            incoming.Opacity.ShouldBe(0);
            ((TranslateTransform)incoming.RenderTransform!).Y.ShouldBe(6);

            clock.Advance(TimeSpan.FromMilliseconds(80));
            incoming.Opacity.ShouldBeInRange(0.05, 0.99);
            ((TranslateTransform)incoming.RenderTransform!).Y.ShouldBeInRange(0.05, 5.95);

            clock.Advance(TimeSpan.FromMilliseconds(400));
            Dispatcher.UIThread.RunJobs();
            incoming.Opacity.ShouldBe(1);
            ((TranslateTransform)incoming.RenderTransform!).Y.ShouldBe(0);
            incoming.Effect.ShouldBeNull();
            swap.ExitingCount.ShouldBe(0);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Swap_Only_Fades_Under_Reduced_Motion()
    {
        var clock = new ManualFrameClock();
        using var installed = clock.Install();
        var swap = new SiteSwap { Content = "Note" };
        var window = Show(new StackPanel { Children = { swap } }, ArcMotionLevel.Reduced);
        try
        {
            swap.Content = "Done";
            var incoming = swap.CurrentPresenter!;
            ((TranslateTransform)incoming.RenderTransform!).Y.ShouldBe(0);
            incoming.Effect.ShouldBeNull();
            clock.Advance(TimeSpan.FromMilliseconds(60));
            incoming.Opacity.ShouldBeInRange(0.05, 0.99);
            clock.Advance(TimeSpan.FromMilliseconds(200));
            Dispatcher.UIThread.RunJobs();
            incoming.Opacity.ShouldBe(1);
            swap.ExitingCount.ShouldBe(0);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Announcer_Says_A_Repeated_Message_Again()
    {
        var announcer = new SiteAnnouncer();
        announcer.Announce("Opened Pricing");
        var first = AutomationProperties.GetName(announcer);
        announcer.Announce("Opened Pricing");
        AutomationProperties.GetName(announcer).ShouldNotBe(first);
        announcer.History.Count.ShouldBe(2);
        announcer.Clear();
        announcer.Announcement.ShouldBeNull();
    }
}
