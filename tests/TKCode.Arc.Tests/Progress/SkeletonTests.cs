using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;

namespace TKCode.Arc.Tests.Progress;

public class SkeletonTests
{
    [Fact]
    public void Pulse_Is_A_Staggered_Finite_Wave()
    {
        SkeletonBlocks.PulseOpacity(0, 0).ShouldBe(1);
        SkeletonBlocks.PulseOpacity(0, 0.9).ShouldBe(0.52, 0.001);
        SkeletonBlocks.PulseOpacity(0, 1.8).ShouldBe(1, 0.001);
        // The second block runs 90ms behind the first.
        SkeletonBlocks.PulseOpacity(1, 0.09).ShouldBe(1, 0.001);
        SkeletonBlocks.PulseOpacity(1, 0.99).ShouldBe(0.52, 0.001);
        // Eleven pulses, then rest.
        SkeletonBlocks.PulseOpacity(0, 11 * 1.8 + 0.5).ShouldBe(1);
    }

    [AvaloniaFact]
    public void Lines_Clamp_And_Size_Like_Arc()
    {
        var skeleton = new Skeleton { Lines = 10, Width = 320, VerticalAlignment = Avalonia.Layout.VerticalAlignment.Top };
        var window = TestHelpers.Show(skeleton);
        try
        {
            skeleton.Lines.ShouldBe(6);
            skeleton.Lines = 3;
            Dispatcher.UIThread.RunJobs();
            // 4 top padding + 16 first line + 2 × (12 gap + 12 line).
            skeleton.Bounds.Height.ShouldBe(68);
            skeleton.Lines = 1;
            skeleton.IsAvatarVisible = true;
            Dispatcher.UIThread.RunJobs();
            skeleton.Bounds.Height.ShouldBe(44);
            AutomationProperties.GetName(skeleton).ShouldBe("Loading content");
            AutomationProperties.GetItemStatus(skeleton).ShouldBe("Loading");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Content_Replaces_The_Placeholder_When_Loading_Ends()
    {
        var content = new Border { Height = 120 };
        var skeleton = new Skeleton { Content = content, Width = 320, VerticalAlignment = Avalonia.Layout.VerticalAlignment.Top };
        var window = TestHelpers.Show(skeleton);
        try
        {
            skeleton.Bounds.Height.ShouldBe(68);
            var host = Avalonia.VisualTree.VisualExtensions.GetVisualDescendants(skeleton).OfType<PresenceHost>().Single(h => h.Name == "PART_ContentHost");
            host.Child!.IsVisible.ShouldBeFalse();
            skeleton.IsLoading = false;
            Dispatcher.UIThread.RunJobs();
            host.Child!.IsVisible.ShouldBeTrue();
            skeleton.Bounds.Height.ShouldBe(120);
            TestHelpers.Descendant<SkeletonBlocks>(skeleton).IsVisible.ShouldBeFalse();
            AutomationProperties.GetItemStatus(skeleton).ShouldBe(string.Empty);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Without_Content_The_Placeholder_Always_Shows()
    {
        var skeleton = new Skeleton { IsLoading = false, Width = 320 };
        var window = TestHelpers.Show(skeleton);
        try
        {
            skeleton.IsPlaceholderShown.ShouldBeTrue();
            TestHelpers.Descendant<PresenceHost>(skeleton).IsPresent.ShouldBeTrue();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Pulse_Runs_Only_At_Full_Motion_While_Visible()
    {
        var clock = new ManualFrameClock();
        using var _ = clock.Install();
        var skeleton = new Skeleton { Width = 320 };
        var window = TestHelpers.Show(skeleton, ArcMotionLevel.Full);
        try
        {
            var blocks = TestHelpers.Descendant<SkeletonBlocks>(skeleton);
            blocks.IsPulsing.ShouldBeTrue();
            ArcMotion.SetLevel(window, ArcMotionLevel.Reduced);
            Dispatcher.UIThread.RunJobs();
            blocks.IsPulsing.ShouldBeFalse();
            ArcMotion.SetLevel(window, ArcMotionLevel.Full);
            Dispatcher.UIThread.RunJobs();
            blocks.IsPulsing.ShouldBeTrue();
            skeleton.IsVisible = false;
            clock.Advance(TimeSpan.FromMilliseconds(50));
            blocks.IsPulsing.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }
}
