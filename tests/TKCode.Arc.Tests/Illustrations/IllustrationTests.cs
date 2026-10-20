using TKCode.Arc.Motion;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;

namespace TKCode.Arc.Tests.Illustrations;

/// <summary>The shared illustration behaviour: steps on the scene clock, the poster frame, playback and accessibility.</summary>
public class IllustrationTests
{
    private static Window Show(Control illustration, ArcMotionLevel level = ArcMotionLevel.Full)
    {
        var window = TestHelpers.Show(illustration, level);
        window.Width = 900;
        window.Height = 500;
        Dispatcher.UIThread.RunJobs();
        return window;
    }

    [AvaloniaFact]
    public void Defaults()
    {
        var flow = new DataFlow();
        flow.IsPlaying.ShouldBeTrue();
        flow.Speed.ShouldBe(1);
        flow.Label.ShouldBe("Data flow illustration");
        flow.IsCaptionVisible.ShouldBeTrue();
        flow.IsPlaybackButtonVisible.ShouldBeTrue();
        flow.StepCount.ShouldBe(3);
        flow.EventsPerSecond.ShouldBe(18);
        flow.FilterRatio.ShouldBe(0.14);
        new PaymentFlow().Label.ShouldBe("Payment flow illustration");
        new MultiRegionFailover().Label.ShouldBe("Multi-region failover illustration");
        new VectorSearch().Label.ShouldBe("Vector search illustration");
        flow.Speed = 10;
        flow.Speed.ShouldBe(4);
        flow.EventsPerSecond = 0;
        flow.EventsPerSecond.ShouldBe(1);
    }

    [AvaloniaFact]
    public void The_Story_Moves_Through_Its_Steps_On_The_Scene_Clock()
    {
        var flow = new DataFlow();
        var steps = new List<int>();
        flow.StepChanged += (_, e) => steps.Add(e.Step);
        var window = Show(flow);
        try
        {
            flow.Step.ShouldBe(0);
            flow.Caption.ShouldBe(flow.Labels.Ingest(3));
            flow.Advance(2.3);
            flow.Step.ShouldBe(1);
            flow.Caption.ShouldBe(flow.Labels.TransformStep);
            flow.Advance(2.2);
            flow.Step.ShouldBe(2);
            // The loop wraps back to the first step.
            flow.Advance(2.3);
            flow.Step.ShouldBe(0);
            // Subscribed before it was shown: the opening step counts too.
            steps.ShouldBe([0, 1, 2, 0]);
            AutomationProperties.GetHelpText(flow).ShouldBe(flow.Caption);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Packets_Come_From_A_Fixed_Pool_And_Events_Reach_The_Warehouse()
    {
        var flow = new DataFlow { EventsPerSecond = 60, FilterRatio = 0.2 };
        var window = Show(flow);
        try
        {
            var stored = flow.RowsStored;
            for (var i = 0; i < 600; i++)
            {
                flow.Advance(1 / 60.0);
                flow.PacketCount.ShouldBeLessThanOrEqualTo(64);
            }
            flow.RowsStored.ShouldBeGreaterThan(stored);
            flow.EventsFiltered.ShouldBeGreaterThan(0);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Reduced_Motion_Shows_The_Still_Poster_Frame()
    {
        var flow = new DataFlow();
        var window = Show(flow, ArcMotionLevel.Reduced);
        try
        {
            flow.Classes.ShouldContain(Illustration.PosterPseudoClass);
            flow.Caption.ShouldBe(flow.Labels.Summary);
            var packets = flow.PacketCount;
            packets.ShouldBeGreaterThan(0);
            flow.Advance(3);
            flow.PacketCount.ShouldBe(packets);
            flow.Caption.ShouldBe(flow.Labels.Summary);

            // Back at full motion the story starts again.
            ArcMotion.SetLevel(flow, ArcMotionLevel.Full);
            Dispatcher.UIThread.RunJobs();
            flow.Classes.ShouldNotContain(Illustration.PosterPseudoClass);
            flow.Caption.ShouldBe(flow.Labels.Ingest(3));
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Paused_Illustrations_Hold_Their_Frame()
    {
        var clock = new ManualFrameClock();
        using var install = clock.Install();
        var flow = new DataFlow();
        var window = Show(flow);
        try
        {
            clock.Advance(TimeSpan.FromSeconds(2.4), TimeSpan.FromMilliseconds(1000.0 / 60));
            flow.Step.ShouldBe(1);
            flow.Pause();
            flow.IsPlaying.ShouldBeFalse();
            flow.Classes.ShouldNotContain(Illustration.PlayingPseudoClass);
            clock.Advance(TimeSpan.FromSeconds(3), TimeSpan.FromMilliseconds(1000.0 / 60));
            flow.Step.ShouldBe(1);
            flow.Play();
            clock.Advance(TimeSpan.FromSeconds(2.3), TimeSpan.FromMilliseconds(1000.0 / 60));
            flow.Step.ShouldBe(2);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Keyboard_Plays_Pauses_And_Starts_Over()
    {
        var flow = new DataFlow();
        var window = Show(flow);
        try
        {
            flow.Focus();
            flow.Advance(2.3);
            flow.Step.ShouldBe(1);
            window.KeyPressQwerty(PhysicalKey.Space, RawInputModifiers.None);
            flow.IsPlaying.ShouldBeFalse();
            window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
            flow.IsPlaying.ShouldBeTrue();
            window.KeyPressQwerty(PhysicalKey.R, RawInputModifiers.None);
            flow.Step.ShouldBe(0);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void It_Is_An_Image_Named_By_Its_Label_With_The_Caption_As_Help()
    {
        var flow = new DataFlow { Label = "How events reach the warehouse" };
        var window = Show(flow);
        try
        {
            var peer = ControlAutomationPeer.CreatePeerForElement(flow);
            peer.GetAutomationControlType().ShouldBe(AutomationControlType.Image);
            peer.GetName().ShouldBe("How events reach the warehouse");
            peer.GetHelpText().ShouldBe(flow.Caption);
            flow.Focusable.ShouldBeTrue();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Changing_What_The_Diagram_Shows_Rebuilds_It()
    {
        var flow = new DataFlow();
        var window = Show(flow);
        try
        {
            var nodes = flow.SceneNodeCount;
            flow.Sources = [new DataFlowSource("Checkout"), new DataFlowSource("CRM")];
            Dispatcher.UIThread.RunJobs();
            flow.SceneNodeCount.ShouldBe(nodes - 1);
            flow.Labels = new DataFlowLabels { Ingest = static n => $"{n} feeds" };
            Dispatcher.UIThread.RunJobs();
            flow.Caption.ShouldBe("2 feeds");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Every_Illustration_Renders_A_Few_Seconds_In_Light_And_Dark()
    {
        foreach (var level in new[] { ArcMotionLevel.Full, ArcMotionLevel.Reduced })
        {
            Control[] all = [new DataFlow(), new PaymentFlow(), new MultiRegionFailover(), new VectorSearch()];
            var window = Show(new StackPanel { Children = { all[0], all[1], all[2], all[3] } }, level);
            try
            {
                for (var i = 0; i < 40; i++)
                {
                    foreach (var illustration in all.Cast<Illustration>())
                    {
                        illustration.Advance(0.25);
                    }
                    AvaloniaHeadlessPlatform.ForceRenderTimerTick();
                    Dispatcher.UIThread.RunJobs();
                }
                all.Cast<Illustration>().ShouldAllBe(x => !string.IsNullOrEmpty(x.Caption));
            }
            finally
            {
                window.Close();
            }
        }
    }
}
