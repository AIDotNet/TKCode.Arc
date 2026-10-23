using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using TKCode.Arc.Motion;

namespace TKCode.Arc.Tests.Special.Ai;

/// <summary>
/// The agent timeline as a control: the sample replay and its first sight, data opening finished, playback, speed, the end,
/// the scrubber and span keyboard models, pinning and dimming, the span card, narrow containers, reduced motion and live
/// mode.
/// </summary>
public class AgentTimelineTests
{
    private static readonly TimeSpan Frame = TimeSpan.FromMilliseconds(1000.0 / 60);

    private sealed class FakeTime : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = new(2026, 10, 23, 12, 0, 0, TimeSpan.Zero);

        public override DateTimeOffset GetUtcNow() => Now;
    }

    private static Window Open(ManualFrameClock clock, AgentTimeline timeline, ArcMotionLevel level = ArcMotionLevel.Full,
        double width = 800, double height = 620)
    {
        var window = TestHelpers.Show(timeline, level);
        window.Width = width;
        window.Height = height;
        Dispatcher.UIThread.RunJobs();
        clock.Advance(TimeSpan.FromMilliseconds(50), Frame);
        Dispatcher.UIThread.RunJobs();
        return window;
    }

    private static void Run(ManualFrameClock clock, double seconds)
    {
        clock.Advance(TimeSpan.FromSeconds(seconds), Frame);
        Dispatcher.UIThread.RunJobs();
    }

    private static void Click(Avalonia.Controls.Button button) => button.RaiseEvent(new RoutedEventArgs(Avalonia.Controls.Button.ClickEvent));

    private static int SpanOf(AgentTimeline timeline, string agent, double start)
        => timeline.Model.Spans.Single(s => s.Span.AgentId == agent && Math.Abs(s.Start - start) < 1e-9).Index;

    private static Point Center(AgentTimeline timeline, Window window, int span)
    {
        var plot = timeline.Plot!;
        var rect = plot.VisibleRect(span, plot.Scale);
        return plot.TranslatePoint(rect.Center, window)!.Value;
    }

    [AvaloniaFact]
    public void Sample_Replays_When_It_Comes_Into_View()
    {
        var clock = new ManualFrameClock();
        using var install = clock.Install();
        var timeline = new AgentTimeline();
        var window = Open(clock, timeline);
        try
        {
            timeline.UsesSample.ShouldBeTrue();
            timeline.ActualTitle.ShouldBe("Signup emails bouncing since Mar 3");
            timeline.Model.Rows.Length.ShouldBe(5);
            timeline.Phase.ShouldBe(AgentTimelinePhase.Running);
            timeline.IsPlaying.ShouldBeTrue();
            Run(clock, 2);
            timeline.Now.ShouldBe(2.05, 0.05);
            timeline.StatusLine!.WordIndex.ShouldBe(0);
            timeline.StatusLine.ShowsSummary.ShouldBeFalse();
            // The first replay starts quietly.
            timeline.Announcer!.History.ShouldBeEmpty();
            timeline.Plot!.PlayheadAlpha.ShouldBe(0.5, 0.01);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Counters_Reserve_The_Final_Widths()
    {
        var clock = new ManualFrameClock();
        using var install = clock.Install();
        var timeline = new AgentTimeline { AutoPlay = false };
        var window = Open(clock, timeline);
        try
        {
            timeline.StatusLine!.Finals.ShouldBe(("26.8s", "49,920", "$0.15"));
            timeline.StatusLine.SummaryText.ShouldBe("5 agents · 26.8s · 1.5x parallel speedup · $0.15");
            timeline.Summary.Tokens.ShouldBe(49_920);
            timeline.Seek(1.0);
            timeline.StatusLine.Counters.ShouldBe(("1.0s", "976", "$0.01"));
            timeline.Phase.ShouldBe(AgentTimelinePhase.Paused);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Data_Opens_Finished_And_Waits_For_Play()
    {
        var clock = new ManualFrameClock();
        using var install = clock.Install();
        var timeline = new AgentTimeline { Run = AgentTimelineSamples.CreateRefundRun() };
        var window = Open(clock, timeline);
        try
        {
            timeline.UsesSample.ShouldBeFalse();
            timeline.Phase.ShouldBe(AgentTimelinePhase.Finished);
            timeline.IsPlaying.ShouldBeFalse();
            timeline.Duration.ShouldBe(5.8, 1e-9);
            timeline.CurrentTime.ShouldBe(5.8, 1e-9);
            timeline.StatusLine!.ShowsSummary.ShouldBeTrue();
            timeline.Plot!.PlayheadAlpha.ShouldBe(0);
            Run(clock, 1);
            timeline.Phase.ShouldBe(AgentTimelinePhase.Finished);

            timeline.Play();
            timeline.Phase.ShouldBe(AgentTimelinePhase.Running);
            timeline.Now.ShouldBe(0);
            Run(clock, 1);
            timeline.Now.ShouldBe(1, 0.05);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Play_Pause_And_Restart_Announce_And_Raise_Events()
    {
        var clock = new ManualFrameClock();
        using var install = clock.Install();
        var timeline = new AgentTimeline { Run = AgentTimelineSamples.CreateIncidentRun() };
        var actions = new List<AgentTimelinePlaybackAction>();
        timeline.PlaybackChanged += (_, e) => actions.Add(e.Action);
        var window = Open(clock, timeline);
        try
        {
            timeline.Seek(4);
            Click(timeline.PlayButton!);
            timeline.Phase.ShouldBe(AgentTimelinePhase.Running);
            timeline.Announcer!.Announcement.ShouldBe("Playing at 1x");
            Run(clock, 1);
            Click(timeline.PlayButton!);
            timeline.Phase.ShouldBe(AgentTimelinePhase.Paused);
            timeline.Announcer.Announcement.ShouldStartWith("Paused at 5.");
            Avalonia.Automation.AutomationProperties.GetName(timeline.PlayButton!).ShouldBe("Play");
            var paused = timeline.Now;
            Run(clock, 1);
            timeline.Now.ShouldBe(paused);

            var restart = TestHelpers.Descendant<Avalonia.Controls.Button>(timeline, AgentTimeline.RestartButtonPart);
            Click(restart);
            timeline.Now.ShouldBe(0);
            timeline.Phase.ShouldBe(AgentTimelinePhase.Running);
            Avalonia.Automation.AutomationProperties.GetName(timeline.PlayButton!).ShouldBe("Pause");
            actions.ShouldBe([AgentTimelinePlaybackAction.Play, AgentTimelinePlaybackAction.Pause, AgentTimelinePlaybackAction.Restart]);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Speed_Scales_The_Replay_And_The_Control_Offers_The_Speeds()
    {
        var clock = new ManualFrameClock();
        using var install = clock.Install();
        var timeline = new AgentTimeline { Run = AgentTimelineSamples.CreateIncidentRun(), Speed = 2 };
        var window = Open(clock, timeline);
        try
        {
            timeline.SpeedControl!.ItemCount.ShouldBe(2);
            timeline.SpeedControl.SelectedIndex.ShouldBe(1);
            timeline.Restart();
            Run(clock, 1);
            timeline.Now.ShouldBe(2, 0.1);

            timeline.Speeds = [0.5, 1, 2, 4];
            timeline.SpeedControl.ItemCount.ShouldBe(4);
            timeline.SpeedControl.SelectedIndex = 3;
            Dispatcher.UIThread.RunJobs();
            timeline.Speed.ShouldBe(4);
            timeline.Announcer!.Announcement.ShouldBe("Playing at 4x");

            timeline.Speeds = [1];
            timeline.SpeedControl.IsVisible.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Reaching_The_End_Finishes_Once()
    {
        var clock = new ManualFrameClock();
        using var install = clock.Install();
        var timeline = new AgentTimeline { Run = AgentTimelineSamples.CreateRefundRun() };
        var completed = 0;
        timeline.Completed += (_, _) => completed++;
        var window = Open(clock, timeline);
        try
        {
            Click(timeline.PlayButton!);
            Run(clock, 6.2);
            timeline.Phase.ShouldBe(AgentTimelinePhase.Finished);
            timeline.IsPlaying.ShouldBeFalse();
            completed.ShouldBe(1);
            timeline.Now.ShouldBe(5.8);
            timeline.StatusLine!.ShowsSummary.ShouldBeTrue();
            Run(clock, 0.4);
            timeline.Plot!.PlayheadAlpha.ShouldBe(0, 0.001);
            timeline.Announcer!.Announcement.ShouldBe("Run complete: 2 agents in 5.8s, no parallel speedup, $0.01");
            Avalonia.Automation.AutomationProperties.GetName(timeline.PlayButton!).ShouldBe("Replay");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Scrubber_Keys_Step_The_Time_And_Announce()
    {
        var clock = new ManualFrameClock();
        using var install = clock.Install();
        var timeline = new AgentTimeline { Run = AgentTimelineSamples.CreateIncidentRun() };
        var window = Open(clock, timeline);
        try
        {
            timeline.Seek(10);
            var scrubber = timeline.Plot!.Scrubber;
            scrubber.Focus(NavigationMethod.Tab);
            window.KeyPressQwerty(PhysicalKey.ArrowRight, RawInputModifiers.None);
            timeline.Now.ShouldBe(10.5, 1e-9);
            timeline.Announcer!.Announcement.ShouldBe("Paused at 10.5s");
            window.KeyPressQwerty(PhysicalKey.ArrowRight, RawInputModifiers.Shift);
            timeline.Now.ShouldBe(15.5, 1e-9);
            window.KeyPressQwerty(PhysicalKey.PageDown, RawInputModifiers.None);
            timeline.Now.ShouldBe(10.5, 1e-9);
            window.KeyPressQwerty(PhysicalKey.ArrowDown, RawInputModifiers.None);
            timeline.Now.ShouldBe(10, 1e-9);
            window.KeyPressQwerty(PhysicalKey.PageUp, RawInputModifiers.None);
            timeline.Now.ShouldBe(15, 1e-9);
            window.KeyPressQwerty(PhysicalKey.Home, RawInputModifiers.None);
            timeline.Now.ShouldBe(0);
            timeline.Phase.ShouldBe(AgentTimelinePhase.Ready);
            timeline.StatusLine!.Counters.ShouldBe(("0.0s", "0", "$0.00"));
            window.KeyPressQwerty(PhysicalKey.End, RawInputModifiers.None);
            timeline.Phase.ShouldBe(AgentTimelinePhase.Finished);
            timeline.Announcer.Announcement.ShouldStartWith("Run complete: 5 agents in 26.8s");
            Run(clock, 0.3);
            timeline.Plot.KnobScale.ShouldBe(AgentTimelineLayout.KnobFocusScale, 0.01);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Dragging_On_The_Axis_Seeks_And_Pauses()
    {
        var clock = new ManualFrameClock();
        using var install = clock.Install();
        var timeline = new AgentTimeline { Run = AgentTimelineSamples.CreateIncidentRun() };
        var window = Open(clock, timeline);
        try
        {
            timeline.Restart();
            Run(clock, 1);
            var plot = timeline.Plot!;
            var rows = AgentTimelineLayout.RowsHeight(5);
            var at = plot.TranslatePoint(new Point(10 * plot.Scale, rows + 15), window)!.Value;
            AvaloniaHeadlessPlatform.ForceRenderTimerTick();
            Dispatcher.UIThread.RunJobs();
            window.MouseDown(at, MouseButton.Left, RawInputModifiers.None);
            timeline.Phase.ShouldBe(AgentTimelinePhase.Paused);
            timeline.Now.ShouldBe(10, 0.01);
            var later = plot.TranslatePoint(new Point(12 * plot.Scale, rows + 15), window)!.Value;
            window.MouseMove(later, RawInputModifiers.LeftMouseButton);
            timeline.Now.ShouldBe(12, 0.01);
            window.MouseUp(later, MouseButton.Left, RawInputModifiers.None);
            timeline.Announcer!.Announcement.ShouldBe("Paused at 12.0s");
            plot.Scrubber.IsFocused.ShouldBeTrue();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Span_Keys_Rove_Pin_And_Unpin()
    {
        var clock = new ManualFrameClock();
        using var install = clock.Install();
        var timeline = new AgentTimeline { Run = AgentTimelineSamples.CreateIncidentRun() };
        var selected = new List<AgentTimelineSpan?>();
        timeline.SpanSelected += (_, e) => selected.Add(e.Span);
        var window = Open(clock, timeline);
        try
        {
            var plot = timeline.Plot!;
            plot.ActiveProxy.Focus(NavigationMethod.Tab);
            Dispatcher.UIThread.RunJobs();
            plot.Roving.ShouldBe(SpanOf(timeline, "lead", 0));
            plot.HasVisibleSpanFocus.ShouldBeTrue();
            window.KeyPressQwerty(PhysicalKey.ArrowRight, RawInputModifiers.None);
            plot.Roving.ShouldBe(SpanOf(timeline, "lead", 2.1));
            plot.ActiveProxy.IsFocused.ShouldBeTrue();
            Avalonia.Automation.AutomationProperties.GetName(plot.ActiveProxy).ShouldBe("Coordinator, Waiting: Waiting on logs, config and status, 2.1s to 14.9s");
            window.KeyPressQwerty(PhysicalKey.ArrowDown, RawInputModifiers.None);
            plot.Roving.ShouldBe(SpanOf(timeline, "status", 7.9));
            window.KeyPressQwerty(PhysicalKey.Home, RawInputModifiers.None);
            plot.Roving.ShouldBe(SpanOf(timeline, "status", 3.6));
            window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
            var span = timeline.Model.Spans[SpanOf(timeline, "status", 3.6)].Span;
            timeline.PinnedSpan.ShouldBe(span);
            plot.ActiveProxy.IsPressed.ShouldBeTrue();
            window.KeyPressQwerty(PhysicalKey.Escape, RawInputModifiers.None);
            timeline.PinnedSpan.ShouldBeNull();
            selected.ShouldBe([span, null]);
            // The card follows keyboard focus.
            Run(clock, 0.3);
            timeline.Card!.IsShown.ShouldBeTrue();
            timeline.Card.Span.ShouldBe(SpanOf(timeline, "status", 3.6));
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Pinning_Dims_Everything_Off_The_Handoff_Path()
    {
        var clock = new ManualFrameClock();
        using var install = clock.Install();
        var timeline = new AgentTimeline { Run = AgentTimelineSamples.CreateIncidentRun() };
        var window = Open(clock, timeline);
        try
        {
            var plot = timeline.Plot!;
            timeline.Pin(timeline.Model.Spans[SpanOf(timeline, "status", 3.6)].Span);
            Run(clock, 0.3);
            timeline.Classes.Contains(AgentTimeline.PinnedPseudoClass).ShouldBeTrue();
            plot.SpanOpacity(SpanOf(timeline, "lead", 0)).ShouldBe(0.28, 0.01);
            plot.SpanOpacity(SpanOf(timeline, "lead", 2.1)).ShouldBe(1, 0.01);
            plot.SpanOpacity(SpanOf(timeline, "status", 6.4)).ShouldBe(1, 0.01);
            plot.LinkOpacity(0).ShouldBe(0.16, 0.01);
            plot.LinkOpacity(2).ShouldBe(1, 0.01);
            // The pinned card stays without hover or focus.
            timeline.Card!.IsShown.ShouldBeTrue();
            timeline.Unpin();
            Run(clock, 0.3);
            plot.SpanOpacity(SpanOf(timeline, "lead", 0)).ShouldBe(1, 0.01);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Clicking_A_Span_Pins_It_And_Clicking_Again_Unpins()
    {
        var clock = new ManualFrameClock();
        using var install = clock.Install();
        var timeline = new AgentTimeline { Run = AgentTimelineSamples.CreateIncidentRun() };
        var window = Open(clock, timeline);
        try
        {
            AvaloniaHeadlessPlatform.ForceRenderTimerTick();
            Dispatcher.UIThread.RunJobs();
            var index = SpanOf(timeline, "logs", 3.1);
            var at = Center(timeline, window, index);
            window.MouseMove(at, RawInputModifiers.None);
            timeline.Plot!.Hovered.ShouldBe(index);
            window.MouseDown(at, MouseButton.Left, RawInputModifiers.None);
            window.MouseUp(at, MouseButton.Left, RawInputModifiers.None);
            timeline.PinnedSpan.ShouldBe(timeline.Model.Spans[index].Span);
            timeline.Plot.Roving.ShouldBe(index);
            timeline.Plot.HasVisibleSpanFocus.ShouldBeFalse();
            window.MouseDown(at, MouseButton.Left, RawInputModifiers.None);
            window.MouseUp(at, MouseButton.Left, RawInputModifiers.None);
            timeline.PinnedSpan.ShouldBeNull();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Hover_Card_Shows_The_Final_Numbers_And_Leaves_After_A_Grace()
    {
        var clock = new ManualFrameClock();
        using var install = clock.Install();
        var timeline = new AgentTimeline { Run = AgentTimelineSamples.CreateIncidentRun() };
        var window = Open(clock, timeline);
        try
        {
            timeline.Seek(3);
            AvaloniaHeadlessPlatform.ForceRenderTimerTick();
            Dispatcher.UIThread.RunJobs();
            var index = SpanOf(timeline, "lead", 2.1);
            window.MouseMove(Center(timeline, window, index), RawInputModifiers.None);
            Run(clock, 0.3);
            var card = timeline.Card!;
            card.IsShown.ShouldBeTrue();
            card.IsVisible.ShouldBeTrue();
            card.Opacity.ShouldBe(1, 0.01);
            var texts = Texts(card.CurrentContent!);
            texts.ShouldContain("Waiting on logs, config and status");
            // The span has only grown to 3 s, but the card reads its whole length and every handoff.
            texts.ShouldContain("12.8s");
            texts.ShouldContain("5 handoffs in this span");
            texts.ShouldContain("Nimbus Pro 3");
            // The orchestrator row has no room above: the card sits below its row.
            card.Target.Y.ShouldBeGreaterThan(timeline.Plot!.TranslatePoint(new Point(0, AgentTimelineLayout.BarBottom(0)), card.Parent as Visual ?? timeline)!.Value.Y);

            window.MouseMove(new Point(2, 2), RawInputModifiers.None);
            Run(clock, 0.05);
            card.Opacity.ShouldBe(1, 0.01);
            Run(clock, 0.4);
            card.IsVisible.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    private static List<string> Texts(Control root)
        => root.GetLogicalDescendants().OfType<TextBlock>().Select(t => t.Text ?? "").ToList();

    [AvaloniaFact]
    public void Failed_Span_Card_Says_Why()
    {
        var clock = new ManualFrameClock();
        using var install = clock.Install();
        var timeline = new AgentTimeline { Run = AgentTimelineSamples.CreateIncidentRun() };
        var window = Open(clock, timeline);
        try
        {
            timeline.Pin(timeline.Model.Spans[SpanOf(timeline, "logs", 3.1)].Span);
            Run(clock, 0.3);
            var texts = Texts(timeline.Card!.CurrentContent!);
            texts.ShouldContain("Failed: Full scan of mail events ran past the time limit");
            texts.ShouldContain("Mail logs");
            texts.ShouldContain("2.1s");
            texts.ShouldContain("430 tokens");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Narrow_Container_Narrows_The_Labels_And_Scrolls_The_Plot()
    {
        var clock = new ManualFrameClock();
        using var install = clock.Install();
        var timeline = new AgentTimeline { Run = AgentTimelineSamples.CreateIncidentRun() };
        var window = Open(clock, timeline, width: 420);
        try
        {
            timeline.Classes.Contains(AgentTimeline.NarrowPseudoClass).ShouldBeTrue();
            timeline.Classes.Contains(AgentTimeline.ScrollablePseudoClass).ShouldBeTrue();
            timeline.Plot!.PlotWidth.ShouldBe(300);
            timeline.LabelColumn!.Bounds.Width.ShouldBe(132);

            window.Width = 800;
            Dispatcher.UIThread.RunJobs();
            Run(clock, 0.1);
            timeline.Classes.Contains(AgentTimeline.NarrowPseudoClass).ShouldBeFalse();
            timeline.Classes.Contains(AgentTimeline.ScrollablePseudoClass).ShouldBeFalse();
            timeline.Plot.PlotWidth.ShouldBe(530);
            timeline.LabelColumn.Bounds.Width.ShouldBe(176);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Reduced_Motion_Opens_Finished_And_Still_Plays()
    {
        var clock = new ManualFrameClock();
        using var install = clock.Install();
        var timeline = new AgentTimeline();
        var window = Open(clock, timeline, ArcMotionLevel.Reduced);
        try
        {
            timeline.UsesSample.ShouldBeTrue();
            timeline.Phase.ShouldBe(AgentTimelinePhase.Finished);
            Run(clock, 0.5);
            timeline.Phase.ShouldBe(AgentTimelinePhase.Finished);
            timeline.Play();
            Run(clock, 1);
            timeline.Phase.ShouldBe(AgentTimelinePhase.Running);
            timeline.Now.ShouldBe(1, 0.05);
            timeline.Plot!.HasShine.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Spans_Complete_With_A_Transition_While_Playing()
    {
        var clock = new ManualFrameClock();
        using var install = clock.Install();
        var timeline = new AgentTimeline { Run = AgentTimelineSamples.CreateIncidentRun() };
        var window = Open(clock, timeline);
        try
        {
            timeline.Restart();
            Run(clock, 1);
            var plot = timeline.Plot!;
            var first = SpanOf(timeline, "lead", 0);
            plot.SpanPhase(first).ShouldBe(1);
            plot.HasShine.ShouldBeTrue();
            plot.SpanPhase(SpanOf(timeline, "lead", 2.1)).ShouldBe(0);
            Run(clock, 1.3);
            plot.SpanPhase(first).ShouldBe(2);
            plot.SpanPhase(SpanOf(timeline, "lead", 2.1)).ShouldBe(1);
            // The dispatch at 2.1 s has mounted and is drawing in over 360 ms.
            plot.LinkReveal(0).ShouldBeGreaterThan(0);
            Run(clock, 0.5);
            plot.LinkReveal(0).ShouldBe(1, 1e-6);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Live_Mode_Follows_The_Run_And_Settles_On_The_Summary()
    {
        var clock = new ManualFrameClock();
        using var install = clock.Install();
        var time = new FakeTime();
        var timeline = new AgentTimeline(time);
        var completed = 0;
        timeline.Completed += (_, _) => completed++;
        var window = Open(clock, timeline);
        using var driver = new AgentTimelineLiveDriver(AgentTimelineSamples.CreateRefundRun(), time);
        try
        {
            driver.Start(timeline);
            Dispatcher.UIThread.RunJobs();
            timeline.Phase.ShouldBe(AgentTimelinePhase.Live);
            timeline.Classes.Contains(AgentTimeline.LivePseudoClass).ShouldBeTrue();
            timeline.Model.Spans.Length.ShouldBe(1);
            timeline.Model.Spans[0].IsOpen.ShouldBeTrue();
            timeline.LiveAxis.ShouldBe(5);

            time.Now = time.Now.AddSeconds(2);
            driver.Update();
            Run(clock, 0.1);
            timeline.Now.ShouldBe(2, 0.01);
            timeline.Model.Spans.Length.ShouldBe(3);
            timeline.Model.Handoffs.Length.ShouldBe(1);
            // The clerk's export is open and grows to now.
            var export = timeline.Model.Spans.Single(s => s.Span.Label == "Exported last month's refunds");
            export.IsOpen.ShouldBeTrue();
            timeline.Plot!.SpanPhase(export.Index).ShouldBe(1);
            timeline.Plot.VisibleRect(export.Index, timeline.Plot.Scale).Width.ShouldBeGreaterThan(0);

            time.Now = time.Now.AddSeconds(4);
            driver.Update();
            Run(clock, 0.1);
            timeline.IsLive.ShouldBeFalse();
            driver.IsRunning.ShouldBeFalse();
            timeline.Phase.ShouldBe(AgentTimelinePhase.Finished);
            completed.ShouldBe(1);
            timeline.Duration.ShouldBe(5.8, 1e-9);
            timeline.Model.Spans.Length.ShouldBe(4);
            timeline.Model.HasOpenSpans.ShouldBeFalse();
            timeline.Announcer!.Announcement.ShouldStartWith("Run complete: 2 agents in 5.8s");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Labels_Rename_Everything()
    {
        var clock = new ManualFrameClock();
        using var install = clock.Install();
        var timeline = new AgentTimeline
        {
            Run = AgentTimelineSamples.CreateRefundRun(),
            Title = "Refunds",
            Labels = new AgentTimelineLabels { Replay = "Again", Agents = n => $"{n} bots" }
        };
        var window = Open(clock, timeline);
        try
        {
            timeline.ActualTitle.ShouldBe("Refunds");
            Avalonia.Automation.AutomationProperties.GetName(timeline).ShouldBe("Refunds");
            Avalonia.Automation.AutomationProperties.GetName(timeline.PlayButton!).ShouldBe("Again");
            timeline.StatusLine!.SummaryText.ShouldStartWith("2 bots · 5.8s");
        }
        finally
        {
            window.Close();
        }
    }
}
