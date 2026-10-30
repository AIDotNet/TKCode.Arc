using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Layout;
using Avalonia.Threading;
using TKCode.Arc.Motion;

namespace TKCode.Arc.Tests.Special.Ai;

/// <summary>
/// The shared measured-height box of the Special › AI components: the default disclosure (spring open, fade, unmount), and
/// the opt-in options the ported components use (following the content, holding, fades under reduced motion, hidden
/// content, zero fades, input while closing, instant changes, the Closed event, settling and opening once loaded).
/// </summary>
public class AiHeightBoxTests
{
    private static readonly TimeSpan Frame = TimeSpan.FromMilliseconds(1000.0 / 60);

    private static void Pump()
    {
        Dispatcher.UIThread.RunJobs();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        Dispatcher.UIThread.RunJobs();
    }

    private static void Run(ManualFrameClock clock, double milliseconds)
    {
        var frames = (int)Math.Round(milliseconds / Frame.TotalMilliseconds);
        for (var i = 0; i < frames; i++)
        {
            clock.Advance(Frame, Frame);
            Pump();
        }
    }

    private static Window Show(AiHeightBox box, ArcMotionLevel level, double width = 300)
    {
        var host = new StackPanel { Width = width, VerticalAlignment = VerticalAlignment.Top, Children = { box } };
        var window = TestHelpers.Show(host, level);
        Pump();
        return window;
    }

    private static Border Block(double height) => new() { Height = height };

    [AvaloniaFact]
    public void Opens_On_The_Spring_With_A_Fade_And_Unmounts_After_Closing()
    {
        var clock = new ManualFrameClock();
        using var _ = clock.Install();
        var content = Block(100);
        var box = new AiHeightBox { Content = content };
        var window = Show(box, ArcMotionLevel.Full);
        try
        {
            box.IsMounted.ShouldBeFalse();
            box.IsExpanded = true;
            Pump();
            box.IsMounted.ShouldBeTrue();
            Run(clock, 100);
            box.DisplayedHeight.ShouldBeInRange(1, 99);
            box.IsMoving.ShouldBeTrue();
            box.Clip.ShouldNotBeNull();
            Run(clock, 1000);
            box.DisplayedHeight.ShouldBe(100, 0.5);
            box.DisplayedOpacity.ShouldBe(1, 0.001);
            box.Clip.ShouldBeNull();

            var unmounted = 0;
            box.ContentUnmounted += (_, _) => unmounted++;
            box.IsExpanded = false;
            Run(clock, 100);
            box.IsMounted.ShouldBeTrue();
            box.DisplayedHeight.ShouldBeInRange(1, 99);
            Run(clock, 1000);
            box.IsMounted.ShouldBeFalse();
            unmounted.ShouldBe(1);
            content.Opacity.ShouldBe(1);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void By_Default_An_Open_Box_Follows_Its_Content_At_Once()
    {
        var clock = new ManualFrameClock();
        using var _ = clock.Install();
        var content = Block(100);
        var box = new AiHeightBox { IsExpanded = true, Content = content };
        var window = Show(box, ArcMotionLevel.Full);
        try
        {
            box.DisplayedHeight.ShouldBe(100, 0.5);
            content.Height = 160;
            Pump();
            box.DisplayedHeight.ShouldBe(160, 0.5);
            box.IsMoving.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Following_The_Content_Springs_To_Its_New_Height_Both_Ways()
    {
        var clock = new ManualFrameClock();
        using var _ = clock.Install();
        var content = Block(100);
        var box = new AiHeightBox { IsExpanded = true, FollowsContent = true, HeightSpring = AiHeightBox.PanelSpring, Content = content };
        var window = Show(box, ArcMotionLevel.Full);
        try
        {
            box.DisplayedHeight.ShouldBe(100, 0.5);
            content.Height = 200;
            Pump();
            Run(clock, 50);
            box.DisplayedHeight.ShouldBeInRange(101, 199);
            box.TargetHeight.ShouldBe(200, 0.5);
            box.Clip.ShouldNotBeNull();
            Run(clock, 800);
            box.DisplayedHeight.ShouldBe(200, 0.5);
            box.Clip.ShouldBeNull();

            content.Height = 60;
            Pump();
            Run(clock, 50);
            box.DisplayedHeight.ShouldBeInRange(61, 199);
            Run(clock, 800);
            box.DisplayedHeight.ShouldBe(60, 0.5);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Width_Change_Cuts_Straight_To_The_New_Height_While_Following()
    {
        var clock = new ManualFrameClock();
        using var _ = clock.Install();
        var text = new TextBlock { Text = string.Join(' ', Enumerable.Repeat("words that wrap", 30)), TextWrapping = Avalonia.Media.TextWrapping.Wrap };
        var box = new AiHeightBox { IsExpanded = true, FollowsContent = true, Content = text };
        var window = Show(box, ArcMotionLevel.Full);
        try
        {
            var wide = box.DisplayedHeight;
            wide.ShouldBeGreaterThan(0);
            ((StackPanel)box.Parent!).Width = 150;
            Pump();
            box.IsMoving.ShouldBeFalse();
            box.DisplayedHeight.ShouldBe(box.NaturalHeight, 0.5);
            box.DisplayedHeight.ShouldBeGreaterThan(wide);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Hold_Keeps_The_Height_From_Dropping_Until_Released()
    {
        var clock = new ManualFrameClock();
        using var _ = clock.Install();
        var content = Block(200);
        var box = new AiHeightBox { IsExpanded = true, FollowsContent = true, Content = content };
        var window = Show(box, ArcMotionLevel.Full);
        try
        {
            box.IsHeld = true;
            content.Height = 40;
            Pump();
            Run(clock, 600);
            box.DisplayedHeight.ShouldBe(200, 0.5);

            content.Height = 260;
            Pump();
            Run(clock, 1000);
            box.DisplayedHeight.ShouldBe(260, 0.5);

            content.Height = 40;
            Pump();
            box.IsHeld = false;
            Pump();
            Run(clock, 50);
            box.DisplayedHeight.ShouldBeInRange(41, 259);
            Run(clock, 1000);
            box.DisplayedHeight.ShouldBe(40, 0.5);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Reduced_Motion_Changes_At_Once_Unless_The_Fades_Are_Kept()
    {
        var clock = new ManualFrameClock();
        using var _ = clock.Install();
        var plain = new AiHeightBox { Content = Block(80) };
        var window = Show(plain, ArcMotionLevel.Reduced);
        try
        {
            plain.IsExpanded = true;
            Pump();
            plain.DisplayedHeight.ShouldBe(80, 0.5);
            plain.DisplayedOpacity.ShouldBe(1);
            plain.IsExpanded = false;
            Pump();
            plain.IsMounted.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }

        var fading = new AiHeightBox { FadesWhenReduced = true, Content = Block(80) };
        window = Show(fading, ArcMotionLevel.Reduced);
        try
        {
            fading.IsExpanded = true;
            Pump();
            Run(clock, 50);
            fading.DisplayedHeight.ShouldBe(80, 0.5);
            fading.DisplayedOpacity.ShouldBeInRange(0.01, 0.99);
            Run(clock, 300);
            fading.DisplayedOpacity.ShouldBe(1, 0.001);

            fading.IsExpanded = false;
            Run(clock, 50);
            fading.IsMounted.ShouldBeTrue();
            fading.DisplayedHeight.ShouldBe(80, 0.5);
            fading.DisplayedOpacity.ShouldBeInRange(0.01, 0.99);
            Run(clock, 300);
            fading.IsMounted.ShouldBeFalse();
            fading.DisplayedHeight.ShouldBe(0);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Kept_Content_Stays_In_The_Tree_Hidden_While_Closed()
    {
        var clock = new ManualFrameClock();
        using var _ = clock.Install();
        var content = Block(90);
        var box = new AiHeightBox { KeepsContent = true, Content = content };
        var window = Show(box, ArcMotionLevel.Full);
        try
        {
            box.IsMounted.ShouldBeTrue();
            content.IsVisible.ShouldBeFalse();
            box.DisplayedHeight.ShouldBe(0);
            box.Bounds.Height.ShouldBe(0);

            box.IsExpanded = true;
            Pump();
            content.IsVisible.ShouldBeTrue();
            Run(clock, 1000);
            box.DisplayedHeight.ShouldBe(90, 0.5);

            box.IsExpanded = false;
            Run(clock, 1000);
            box.IsMounted.ShouldBeTrue();
            content.IsVisible.ShouldBeFalse();
            content.Opacity.ShouldBe(1);
            box.Bounds.Height.ShouldBe(0);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Zero_Fades_Keep_The_Content_Opaque_While_The_Height_Moves()
    {
        var clock = new ManualFrameClock();
        using var _ = clock.Install();
        var content = Block(100);
        var box = new AiHeightBox { FadeInDuration = TimeSpan.Zero, FadeOutDuration = TimeSpan.Zero, Content = content };
        var window = Show(box, ArcMotionLevel.Full);
        try
        {
            box.IsExpanded = true;
            Pump();
            content.Opacity.ShouldBe(1);
            Run(clock, 100);
            box.DisplayedHeight.ShouldBeInRange(1, 99);
            content.Opacity.ShouldBe(1);
            Run(clock, 1000);

            box.IsExpanded = false;
            Run(clock, 100);
            box.DisplayedHeight.ShouldBeInRange(1, 99);
            content.Opacity.ShouldBe(1);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Input_Can_Leave_As_Soon_As_A_Close_Starts()
    {
        var clock = new ManualFrameClock();
        using var _ = clock.Install();
        var box = new AiHeightBox { DisablesInputWhileClosing = true, Content = Block(60) };
        var window = Show(box, ArcMotionLevel.Full);
        try
        {
            box.IsExpanded = true;
            Run(clock, 1000);
            box.IsHitTestVisible.ShouldBeTrue();
            box.IsExpanded = false;
            box.IsHitTestVisible.ShouldBeFalse();
            Run(clock, 100);
            box.IsExpanded = true;
            box.IsHitTestVisible.ShouldBeTrue();
        }
        finally
        {
            window.Close();
        }

        var plain = new AiHeightBox { IsExpanded = true, Content = Block(60) };
        window = Show(plain, ArcMotionLevel.Full);
        try
        {
            plain.IsExpanded = false;
            plain.IsHitTestVisible.ShouldBeTrue();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Instant_Changes_Skip_The_Motion_Both_Ways()
    {
        var clock = new ManualFrameClock();
        using var _ = clock.Install();
        var box = new AiHeightBox { Content = Block(70) };
        var window = Show(box, ArcMotionLevel.Full);
        try
        {
            box.OpenInstantly();
            Pump();
            box.IsExpanded.ShouldBeTrue();
            box.IsMoving.ShouldBeFalse();
            box.DisplayedHeight.ShouldBe(70, 0.5);

            box.IsExpanded = false;
            Run(clock, 50);
            box.IsMoving.ShouldBeTrue();
            box.CloseInstantly();
            Pump();
            box.IsMoving.ShouldBeFalse();
            box.IsMounted.ShouldBeFalse();
            box.DisplayedHeight.ShouldBe(0);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Closed_Is_Raised_Once_A_Close_Finishes_And_Not_When_It_Opened_Again()
    {
        var clock = new ManualFrameClock();
        using var _ = clock.Install();
        var box = new AiHeightBox { KeepsContent = true, IsExpanded = true, Content = Block(50) };
        var window = Show(box, ArcMotionLevel.Full);
        try
        {
            var closed = 0;
            box.Closed += (_, _) => closed++;
            box.IsExpanded = false;
            Run(clock, 50);
            box.IsExpanded = true;
            Run(clock, 1000);
            closed.ShouldBe(0);

            box.IsExpanded = false;
            Run(clock, 1000);
            closed.ShouldBe(1);

            box.OpenInstantly();
            box.CloseInstantly();
            Pump();
            closed.ShouldBe(2);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void WhenSettled_Completes_Once_The_Height_Rests()
    {
        var clock = new ManualFrameClock();
        using var _ = clock.Install();
        var content = Block(100);
        var box = new AiHeightBox { IsExpanded = true, FollowsContent = true, Content = content };
        var window = Show(box, ArcMotionLevel.Full);
        try
        {
            box.WhenSettled().IsCompleted.ShouldBeTrue();
            content.Height = 220;
            Pump();
            var settled = box.WhenSettled();
            settled.IsCompleted.ShouldBeFalse();
            Run(clock, 1000);
            settled.IsCompleted.ShouldBeTrue();
            box.DisplayedHeight.ShouldBe(220, 0.5);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Box_Opened_Once_Loaded_Animates_From_Zero()
    {
        var clock = new ManualFrameClock();
        using var _ = clock.Install();
        var host = new StackPanel { Width = 300, VerticalAlignment = VerticalAlignment.Top };
        var window = TestHelpers.Show(host, ArcMotionLevel.Full);
        try
        {
            var box = new AiHeightBox { Content = Block(80) };
            box.OpenWhenLoaded();
            box.IsExpanded.ShouldBeFalse();
            host.Children.Add(box);
            Pump();
            box.IsExpanded.ShouldBeTrue();
            Run(clock, 50);
            box.DisplayedHeight.ShouldBeInRange(1, 79);
            Run(clock, 1000);
            box.DisplayedHeight.ShouldBe(80, 0.5);
        }
        finally
        {
            window.Close();
        }
    }
}
