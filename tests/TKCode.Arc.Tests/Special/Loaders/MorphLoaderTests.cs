using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Threading;
using TKCode.Arc.DesignTokens;
using TKCode.Arc.Motion;

namespace TKCode.Arc.Tests.Special.Loaders;

/// <summary>
/// The morph loader as a control: size and footprint, the theme's geometry, the frame loop driven by a manual clock, the
/// result tones, the Settled event, the motion levels and accessibility.
/// </summary>
public class MorphLoaderTests
{
    private static readonly TimeSpan Frame = TimeSpan.FromMilliseconds(1000.0 / 60);

    private static void Pump()
    {
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

    [AvaloniaFact]
    public void Default_is_a_24px_dots_loader_with_the_token_geometry()
    {
        var loader = new MorphLoader();
        var window = TestHelpers.Show(new Border { Child = loader, HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center });
        try
        {
            loader.Variant.ShouldBe(MorphLoaderVariant.Dots);
            loader.Status.ShouldBe(MorphLoaderStatus.Loading);
            loader.Bounds.Size.ShouldBe(new Size(24, 24));
            loader.StrokeWidth.ShouldBe(2.5);
            loader.DotWidth.ShouldBe(4);
            loader.BarWidth.ShouldBe(3);
            loader.Tone.ShouldBeTrue();
            loader.Classes.Contains(":loading").ShouldBeTrue();
            loader.Focusable.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Size_sets_the_footprint_and_results_never_change_it()
    {
        var loader = new MorphLoader { Size = 72 };
        var window = TestHelpers.Show(new Border { Child = loader, HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center });
        try
        {
            loader.Bounds.Size.ShouldBe(new Size(72, 72));
            loader.Status = MorphLoaderStatus.Success;
            Pump();
            loader.Bounds.Size.ShouldBe(new Size(72, 72));
            loader.Classes.Contains(":success").ShouldBeTrue();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Full_motion_loops_while_loading_and_stops_once_a_result_settles()
    {
        var clock = new ManualFrameClock();
        using var _ = clock.Install();
        var loader = new MorphLoader { Size = 72 };
        var window = TestHelpers.Show(loader, ArcMotionLevel.Full);
        try
        {
            Pump();
            loader.IsLooping.ShouldBeTrue();
            var settled = new List<MorphLoaderStatus>();
            loader.Settled += (_, e) => settled.Add(e.Status);
            Run(clock, 500);
            loader.Engine.LoopTime.ShouldBeGreaterThan(0.4);
            loader.Status = MorphLoaderStatus.Success;
            Run(clock, 200);
            settled.ShouldBeEmpty();
            loader.Engine.IsSettling.ShouldBeTrue();
            Run(clock, 1500);
            settled.ShouldBe([MorphLoaderStatus.Success]);
            loader.IsLooping.ShouldBeFalse();
            // Back to loading: the loop runs again.
            loader.Status = MorphLoaderStatus.Loading;
            Pump();
            loader.IsLooping.ShouldBeTrue();
            Run(clock, 1000);
            settled.Count.ShouldBe(1);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Results_take_the_success_and_danger_tones_over_240ms()
    {
        var clock = new ManualFrameClock();
        using var _ = clock.Install();
        var loader = new MorphLoader { Foreground = Brushes.Black };
        var window = TestHelpers.Show(loader, ArcMotionLevel.Full);
        try
        {
            Pump();
            loader.InkColor.ShouldBe(Colors.Black);
            var success = ArcTokens.GetColor(loader, ArcDesignTokenKind.Success);
            var danger = ArcTokens.GetColor(loader, ArcDesignTokenKind.Danger);
            loader.Status = MorphLoaderStatus.Success;
            Run(clock, 100);
            loader.InkColor.ShouldNotBe(Colors.Black);
            loader.InkColor.ShouldNotBe(success);
            Run(clock, 200);
            loader.InkColor.ShouldBe(success);
            loader.Status = MorphLoaderStatus.Error;
            Run(clock, 400);
            loader.InkColor.ShouldBe(danger);
            loader.Status = MorphLoaderStatus.Loading;
            Run(clock, 400);
            loader.InkColor.ShouldBe(Colors.Black);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Tone_off_keeps_the_foreground_for_results()
    {
        var loader = new MorphLoader { Foreground = Brushes.Navy, Tone = false };
        var window = TestHelpers.Show(loader);
        try
        {
            loader.Status = MorphLoaderStatus.Error;
            loader.InkColor.ShouldBe(Colors.Navy);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void The_foreground_is_inherited_like_text_color()
    {
        var loader = new MorphLoader();
        var host = new Border { Child = loader };
        Avalonia.Controls.Documents.TextElement.SetForeground(host, Brushes.Teal);
        var window = TestHelpers.Show(host);
        try
        {
            loader.Foreground.ShouldBe(Brushes.Teal);
            loader.InkColor.ShouldBe(Colors.Teal);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Reduced_motion_shows_static_shapes_and_switches_instantly()
    {
        var loader = new MorphLoader { Variant = MorphLoaderVariant.Ring, Foreground = Brushes.Black };
        var window = TestHelpers.Show(loader, ArcMotionLevel.Reduced);
        try
        {
            Pump();
            loader.IsLooping.ShouldBeFalse();
            loader.Engine.Rotation.ShouldBe(0);
            loader.Engine.Strokes[0].Length.ShouldBe(MorphLoaderShapes.RingChord, 1e-9);
            var settled = 0;
            loader.Settled += (_, _) => settled++;
            loader.Status = MorphLoaderStatus.Success;
            settled.ShouldBe(1);
            loader.Engine.Strokes[2].Angle.ShouldBe(-49.214, 0.001);
            loader.InkColor.ShouldBe(ArcTokens.GetColor(loader, ArcDesignTokenKind.Success));
            loader.Engine.Scale.ShouldBe(1);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Status_is_announced_politely_with_the_labels()
    {
        var loader = new MorphLoader();
        var window = TestHelpers.Show(loader);
        try
        {
            AutomationProperties.GetName(loader).ShouldBe("Loading");
            AutomationProperties.GetLiveSetting(loader).ShouldBe(AutomationLiveSetting.Polite);
            loader.Status = MorphLoaderStatus.Success;
            AutomationProperties.GetName(loader).ShouldBe("Done");
            loader.Status = MorphLoaderStatus.Error;
            AutomationProperties.GetName(loader).ShouldBe("Failed");
            loader.Labels = new MorphLoaderLabels { Loading = "Saving", Success = "Saved", Error = "Couldn't save" };
            AutomationProperties.GetName(loader).ShouldBe("Couldn't save");
            loader.Status = MorphLoaderStatus.Loading;
            AutomationProperties.GetName(loader).ShouldBe("Saving");
            var peer = ControlAutomationPeer.CreatePeerForElement(loader);
            peer.GetName().ShouldBe("Saving");
            peer.IsKeyboardFocusable().ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Decorative_loaders_say_nothing()
    {
        var loader = new MorphLoader { IsDecorative = true };
        var window = TestHelpers.Show(loader);
        try
        {
            AutomationProperties.GetLiveSetting(loader).ShouldBe(AutomationLiveSetting.Off);
            AutomationProperties.GetAccessibilityView(loader).ShouldBe(AccessibilityView.Raw);
            AutomationProperties.GetName(loader).ShouldBe(string.Empty);
            loader.IsDecorative = false;
            AutomationProperties.GetName(loader).ShouldBe("Loading");
            AutomationProperties.GetAccessibilityView(loader).ShouldBe(AccessibilityView.Content);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Off_screen_the_loader_rests_and_resumes_in_the_loop_when_shown_again()
    {
        var clock = new ManualFrameClock();
        using var _ = clock.Install();
        var loader = new MorphLoader { Variant = MorphLoaderVariant.Bars };
        var host = new Border { Child = loader };
        var window = TestHelpers.Show(host, ArcMotionLevel.Full);
        try
        {
            Run(clock, 300);
            host.Child = null;
            Pump();
            loader.IsLooping.ShouldBeFalse();
            loader.Status = MorphLoaderStatus.Success;
            loader.Engine.Strokes[0].Angle.ShouldBe(45, 1e-9);
            loader.Status = MorphLoaderStatus.Loading;
            host.Child = loader;
            Pump();
            loader.IsLooping.ShouldBeTrue();
            loader.Engine.LoopWeight.ShouldBe(1);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Renders_every_state_without_errors()
    {
        var clock = new ManualFrameClock();
        using var _ = clock.Install();
        var loader = new MorphLoader { Size = 48 };
        var window = TestHelpers.Show(loader, ArcMotionLevel.Full);
        try
        {
            foreach (var variant in Enum.GetValues<MorphLoaderVariant>())
            {
                loader.Variant = variant;
                Run(clock, 120);
            }
            loader.Status = MorphLoaderStatus.Error;
            Run(clock, 60);
            loader.Status = MorphLoaderStatus.Success;
            Run(clock, 60);
            loader.Status = MorphLoaderStatus.Loading;
            Run(clock, 60);
            loader.Bounds.Size.ShouldBe(new Size(48, 48));
        }
        finally
        {
            window.Close();
        }
    }
}
