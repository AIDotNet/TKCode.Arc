using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using TKCode.Arc.Motion;

namespace TKCode.Arc.Tests.Blocks.Ai;

/// <summary><see cref="AiFeatureLaunch"/>: highlights, try and dismiss with their reasons, Escape, pausing and the demo's motion gate.</summary>
public class AiFeatureLaunchTests
{
    private static (AiFeatureLaunch Launch, Window Window) Show(ArcMotionLevel level = ArcMotionLevel.Off)
    {
        var launch = new AiFeatureLaunch();
        var window = TestHelpers.Show(launch, level);
        Dispatcher.UIThread.RunJobs();
        return (launch, window);
    }

    private static void Click(Button button) => button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

    [AvaloniaFact]
    public void Sample_shows_three_highlights_and_the_built_in_demo()
    {
        var (launch, window) = Show();
        try
        {
            launch.HighlightRows.Count.ShouldBe(3);
            AutomationProperties.GetName(launch.HighlightRows[0]).ShouldBe(AiFeatureLaunchSamples.Highlights()[0]);
            launch.BuiltInDemo.ShouldNotBeNull().IsVisible.ShouldBeTrue();
            TestHelpers.Descendant<Button>(launch, AiFeatureLaunch.TryPart).Content.ShouldBe("Try it now");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Each_button_dismisses_with_its_reason()
    {
        var (launch, window) = Show();
        var reasons = new List<AiFeatureLaunchDismissReason>();
        var tried = 0;
        launch.Dismissed += (_, e) => reasons.Add(e.Reason);
        launch.TryRequested += (_, _) => tried++;
        try
        {
            Click(TestHelpers.Descendant<Button>(launch, AiFeatureLaunch.LaterPart));
            launch.IsOpen.ShouldBeFalse();
            launch.Show();
            Click(TestHelpers.Descendant<Button>(launch, AiFeatureLaunch.TryPart));
            tried.ShouldBe(1);
            launch.Show();
            Click(TestHelpers.Descendant<Button>(launch, AiFeatureLaunch.ClosePart));
            reasons.ShouldBe([AiFeatureLaunchDismissReason.Later, AiFeatureLaunchDismissReason.Tried, AiFeatureLaunchDismissReason.Closed]);

            launch.Show();
            launch.DismissOnTry = false;
            launch.TryFeature();
            launch.IsOpen.ShouldBeTrue();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Escape_dismisses_and_setting_IsOpen_closes_quietly()
    {
        var (launch, window) = Show();
        var count = 0;
        launch.Dismissed += (_, _) => count++;
        try
        {
            TestHelpers.Descendant<Button>(launch, AiFeatureLaunch.TryPart).Focus(NavigationMethod.Tab);
            window.KeyPressQwerty(PhysicalKey.Escape, RawInputModifiers.None);
            launch.IsOpen.ShouldBeFalse();
            count.ShouldBe(1);
            launch.Show();
            launch.IsOpen = false;
            count.ShouldBe(1);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void The_pause_button_flips_playing_and_its_name()
    {
        var (launch, window) = Show();
        try
        {
            var play = TestHelpers.Descendant<Button>(launch, AiFeatureLaunch.PlayPart);
            AutomationProperties.GetName(play).ShouldBe("Pause the demo");
            Click(play);
            launch.IsDemoPlaying.ShouldBeFalse();
            launch.Classes.Contains(":paused").ShouldBeTrue();
            AutomationProperties.GetName(play).ShouldBe("Play the demo");
            launch.BuiltInDemo!.IsPlaying.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void The_demo_rests_on_the_finished_scene_unless_motion_is_full()
    {
        var (launch, window) = Show(ArcMotionLevel.Reduced);
        try
        {
            var demo = launch.BuiltInDemo.ShouldNotBeNull();
            demo.IsRunning.ShouldBeFalse();
            demo.Time.ShouldBe(AiFeatureLaunchDemo.RestTime);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Own_demo_content_replaces_the_built_in_one()
    {
        var (launch, window) = Show();
        try
        {
            launch.Demo = new TextBlock { Text = "Video" };
            Dispatcher.UIThread.RunJobs();
            launch.Classes.Contains(":custom-demo").ShouldBeTrue();
            launch.BuiltInDemo!.IsVisible.ShouldBeFalse();
            TestHelpers.Descendant<Button>(launch, AiFeatureLaunch.PlayPart).IsVisible.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }
}
