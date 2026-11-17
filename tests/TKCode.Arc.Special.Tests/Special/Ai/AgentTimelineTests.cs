using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.Media;
using Avalonia.Threading;
using TKCode.Arc.Motion;

namespace TKCode.Arc.Tests.Special.Ai;

/// <summary>
/// The invented runs the timeline tests use (the package ships none): an incident investigation (five agents, 30 spans,
/// eight handoffs; 26.8 s, 49,920 tokens, $0.15, 1.5x parallel speedup) and a small refund summary. Each call returns
/// new objects.
/// </summary>
internal static class AgentTimelineTestRuns
{
    private const string Lead = "lead";
    private const string Status = "status";
    private const string Config = "config";
    private const string Logs = "logs";
    private const string Scribe = "scribe";

    /// <summary>
    /// The default run, "Signup emails bouncing since Mar 3": a coordinator plans three parallel checks (provider status,
    /// mail config, mail logs), waits, combines their reports, has a scribe write the incident note and files it. Five
    /// agents, 30 spans (a failed log scan and its retry among them), eight handoffs; 26.8 s, 49,920 tokens, $0.15,
    /// 1.5x parallel speedup.
    /// </summary>
    public static AgentTimelineRun CreateIncidentRun()
    {
        var run = new AgentTimelineRun { Title = "Signup emails bouncing since Mar 3" };
        run.Agents.Add(new AgentTimelineAgent(Lead, "Coordinator", new AgentModel("Nimbus Pro 3", ArcIcons.Sparkles), 8)
        {
            Role = "Plans the checks and writes the answer"
        });
        run.Agents.Add(new AgentTimelineAgent(Status, "Status watcher", new AgentModel("Vela 4 Swift", ArcIcons.Zap), 2.6)
        {
            Role = "Checks outside services"
        });
        run.Agents.Add(new AgentTimelineAgent(Config, "Config auditor", new AgentModel("Nimbus Core 3", ArcIcons.Sparkles), 2.5)
        {
            Role = "Reads configuration history"
        });
        run.Agents.Add(new AgentTimelineAgent(Logs, "Log reader", new AgentModel("Nimbus Lite 3", ArcIcons.Sparkles), 0.8)
        {
            Role = "Queries the mail logs"
        });
        run.Agents.Add(new AgentTimelineAgent(Scribe, "Scribe", new AgentModel("Orion Flash 2", ArcIcons.Star), 0.6)
        {
            Role = "Writes the incident note"
        });

        var web = ArcIcons.Globe;
        var repo = ArcIcons.GitBranch;
        var mail = ArcIcons.Database;
        var wiki = ArcIcons.BookOpen;

        Think(run, Lead, 0, 2.1, "Planned three parallel checks", 2050);
        Wait(run, Lead, 2.1, 14.9, "Waiting on logs, config and status");
        Think(run, Lead, 14.9, 17.0, "Combined the reports: the SPF edit is the cause", 4120);
        Wait(run, Lead, 17.0, 23.4, "Waiting on the scribe");
        Think(run, Lead, 23.4, 25.5, "Edited the note for clarity", 2980);
        Tool(run, Lead, 25.5, 26.8, "Filed the note in the team wiki", "Wiki", wiki, 240);

        Think(run, Status, 3.6, 4.2, "Made a list of external suspects", 520);
        Tool(run, Status, 4.2, 5.8, "Checked the mail provider status page", "Web search", web, 3600);
        Think(run, Status, 5.8, 6.4, "Moved on to blocklists", 590);
        Tool(run, Status, 6.4, 7.9, "Searched blocklist reports for our domain", "Web search", web, 4210);
        Think(run, Status, 7.9, 9.3, "Provider looks healthy", 1240);

        Think(run, Config, 3.0, 3.6, "Chose which configs to read", 640);
        Tool(run, Config, 3.6, 4.9, "Found 6 mail config commits this month", "Repo", repo, 2300);
        Think(run, Config, 4.9, 5.6, "Picked the two DNS edits", 910);
        Tool(run, Config, 5.6, 7.7, "Compared the SPF record before and after", "Repo", repo, 6400);
        Think(run, Config, 7.7, 8.6, "Found a missing include for the mail relay", 1380);
        Tool(run, Config, 8.6, 9.9, "Found when the edit went live", "Repo", repo, 1690);
        Think(run, Config, 9.9, 12.3, "Summarized the SPF edit and its timing", 2150);

        Think(run, Logs, 2.4, 3.1, "Drafted a March bounce query", 560);
        var failed = Tool(run, Logs, 3.1, 5.2, "Bounce scan took too long", "Mail logs", mail, 430);
        failed.Status = AgentTimelineSpanStatus.Failed;
        failed.Detail = "Full scan of mail events ran past the time limit";
        Think(run, Logs, 5.2, 5.8, "Narrowed the query to hard bounces", 400);
        var retry = Tool(run, Logs, 5.8, 7.4, "Second try: hard bounces per domain", "Mail logs", mail, 2050);
        retry.IsRetry = true;
        retry.Detail = "Worked once the query was narrowed";
        Think(run, Logs, 7.4, 8.4, "Saw bounces from one sender domain only", 700);
        Tool(run, Logs, 8.4, 10.0, "Grouped bounce codes for that domain", "Mail logs", mail, 2280);
        Think(run, Logs, 10.0, 11.9, "Matched the bounces to SPF failures", 880);
        Tool(run, Logs, 11.9, 13.2, "2,316 signups affected", "Mail logs", mail, 1090);
        Think(run, Logs, 13.2, 14.6, "Wrote the log summary", 660);

        Think(run, Scribe, 17.3, 18.1, "Read the coordinator's brief", 1780);
        Tool(run, Scribe, 18.1, 19.0, "Fetched the SPF edit's commit message", "Repo", repo, 1050);
        Think(run, Scribe, 19.0, 23.1, "Wrote a 380-word incident note", 3020);

        // Dispatches go to the deepest row first and results come back from the top, so each vertical crosses idle rows.
        run.Handoffs.Add(new AgentTimelineHandoff(Lead, Logs, 2.1, "Bounce counts by day for March"));
        run.Handoffs.Add(new AgentTimelineHandoff(Lead, Config, 2.7, "Mail config changes since Mar 1"));
        run.Handoffs.Add(new AgentTimelineHandoff(Lead, Status, 3.3, "Is the mail provider down?"));
        run.Handoffs.Add(new AgentTimelineHandoff(Status, Lead, 9.6, "Provider status green all week"));
        run.Handoffs.Add(new AgentTimelineHandoff(Config, Lead, 12.6, "SPF edit on Mar 2 dropped the relay"));
        run.Handoffs.Add(new AgentTimelineHandoff(Logs, Lead, 14.9, "One sender domain, 2,316 signups hit"));
        run.Handoffs.Add(new AgentTimelineHandoff(Lead, Scribe, 17.0, "Write up the incident"));
        run.Handoffs.Add(new AgentTimelineHandoff(Scribe, Lead, 23.4, "Note drafted, please review"));
        return run;
    }

    /// <summary>
    /// A small finished run, "Monthly refund summary": a planner asks a data clerk which refund reasons grew and waits for
    /// the answer. Two agents, four spans, two handoffs, 5.8 s.
    /// </summary>
    public static AgentTimelineRun CreateRefundRun()
    {
        var run = new AgentTimelineRun { Title = "Monthly refund summary" };
        run.Agents.Add(new AgentTimelineAgent("planner", "Planner", new AgentModel("Nimbus Pro 3", ArcIcons.Sparkles), 8));
        run.Agents.Add(new AgentTimelineAgent("clerk", "Data clerk", new AgentModel("Nimbus Lite 3", ArcIcons.Sparkles), 0.2));
        Think(run, "planner", 0, 1.2, "Outlined the summary", 1100);
        Wait(run, "planner", 1.2, 5.8, "Waiting on the data clerk");
        Tool(run, "clerk", 1.5, 3.9, "Exported last month's refunds", "Ledger", ArcIcons.Database, 2200);
        Think(run, "clerk", 3.9, 5.5, "Ranked the refund reasons", 800);
        run.Handoffs.Add(new AgentTimelineHandoff("planner", "clerk", 1.2, "Which refund reasons grew?"));
        run.Handoffs.Add(new AgentTimelineHandoff("clerk", "planner", 5.8, "Damaged items lead the refunds"));
        return run;
    }

    private static AgentTimelineSpan Think(AgentTimelineRun run, string agent, double start, double end, string label, int tokens)
        => Add(run, new AgentTimelineSpan(agent, AgentTimelineSpanKind.Thinking, start, end, label) { Tokens = tokens });

    private static AgentTimelineSpan Wait(AgentTimelineRun run, string agent, double start, double end, string label)
        => Add(run, new AgentTimelineSpan(agent, AgentTimelineSpanKind.Waiting, start, end, label));

    private static AgentTimelineSpan Tool(AgentTimelineRun run, string agent, double start, double end, string label, string tool,
        Geometry mark, int tokens)
        => Add(run, new AgentTimelineSpan(agent, AgentTimelineSpanKind.Tool, start, end, label) { Tool = tool, ToolMark = mark, Tokens = tokens });

    private static AgentTimelineSpan Add(AgentTimelineRun run, AgentTimelineSpan span)
    {
        run.Spans.Add(span);
        return span;
    }
}

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
    public void With_No_Data_The_Timeline_Is_Empty_And_Plays_Nothing()
    {
        var clock = new ManualFrameClock();
        using var install = clock.Install();
        var timeline = new AgentTimeline { AutoPlay = true };
        var window = Open(clock, timeline);
        try
        {
            timeline.IsEmpty.ShouldBeTrue();
            timeline.ActualTitle.ShouldBe("Agent run");
            timeline.Model.Rows.Length.ShouldBe(0);
            timeline.Duration.ShouldBe(0);
            timeline.Phase.ShouldBe(AgentTimelinePhase.Finished);
            timeline.IsPlaying.ShouldBeFalse();
            timeline.PlayButton!.IsEnabled.ShouldBeFalse("there is nothing to replay");
            Run(clock, 1);
            timeline.IsPlaying.ShouldBeFalse();
            timeline.Summary.Tokens.ShouldBe(0);

            // Data arrives: the run can be replayed; taking it away empties the timeline again and stops the replay.
            timeline.Run = AgentTimelineTestRuns.CreateRefundRun();
            timeline.IsEmpty.ShouldBeFalse();
            timeline.PlayButton.IsEnabled.ShouldBeTrue();
            timeline.Play();
            Run(clock, 1);
            timeline.IsPlaying.ShouldBeTrue();
            timeline.Run = null;
            Run(clock, 0.5);
            timeline.IsEmpty.ShouldBeTrue();
            timeline.Model.Rows.Length.ShouldBe(0);
            timeline.IsPlaying.ShouldBeFalse();
            timeline.ActualTitle.ShouldBe("Agent run");
            timeline.PlayButton.IsEnabled.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Run_With_AutoPlay_Replays_When_It_Comes_Into_View()
    {
        var clock = new ManualFrameClock();
        using var install = clock.Install();
        var timeline = new AgentTimeline { Run = AgentTimelineTestRuns.CreateIncidentRun(), AutoPlay = true };
        var window = Open(clock, timeline);
        try
        {
            timeline.IsEmpty.ShouldBeFalse();
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
        var timeline = new AgentTimeline { Run = AgentTimelineTestRuns.CreateIncidentRun(), AutoPlay = false };
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
        var timeline = new AgentTimeline { Run = AgentTimelineTestRuns.CreateRefundRun() };
        var window = Open(clock, timeline);
        try
        {
            timeline.IsEmpty.ShouldBeFalse();
            timeline.AutoPlay.ShouldBeNull();
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
        var timeline = new AgentTimeline { Run = AgentTimelineTestRuns.CreateIncidentRun() };
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
        var timeline = new AgentTimeline { Run = AgentTimelineTestRuns.CreateIncidentRun(), Speed = 2 };
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
        var timeline = new AgentTimeline { Run = AgentTimelineTestRuns.CreateRefundRun() };
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
        var timeline = new AgentTimeline { Run = AgentTimelineTestRuns.CreateIncidentRun() };
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
        var timeline = new AgentTimeline { Run = AgentTimelineTestRuns.CreateIncidentRun() };
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
        var timeline = new AgentTimeline { Run = AgentTimelineTestRuns.CreateIncidentRun() };
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
        var timeline = new AgentTimeline { Run = AgentTimelineTestRuns.CreateIncidentRun() };
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
        var timeline = new AgentTimeline { Run = AgentTimelineTestRuns.CreateIncidentRun() };
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
        var timeline = new AgentTimeline { Run = AgentTimelineTestRuns.CreateIncidentRun() };
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
        var timeline = new AgentTimeline { Run = AgentTimelineTestRuns.CreateIncidentRun() };
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
        var timeline = new AgentTimeline { Run = AgentTimelineTestRuns.CreateIncidentRun() };
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
        var timeline = new AgentTimeline { Run = AgentTimelineTestRuns.CreateIncidentRun(), AutoPlay = true };
        var window = Open(clock, timeline, ArcMotionLevel.Reduced);
        try
        {
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
        var timeline = new AgentTimeline { Run = AgentTimelineTestRuns.CreateIncidentRun() };
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
        using var driver = new AgentTimelineLiveDriver(AgentTimelineTestRuns.CreateRefundRun(), time);
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
            Run = AgentTimelineTestRuns.CreateRefundRun(),
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
