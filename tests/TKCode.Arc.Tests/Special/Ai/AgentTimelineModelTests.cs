using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;

namespace TKCode.Arc.Tests.Special.Ai;

/// <summary>
/// The agent timeline's pure logic, against the numbers of the invented sample run (notes section 11): normalization,
/// connector shape inference and endpoints, the counters and the summary, formats, the tick rule, the live axis, the
/// keyboard model, the pinned path and the playback clock.
/// </summary>
public class AgentTimelineModelTests
{
    private static AgentTimelineModel Sample()
    {
        var run = AgentTimelineTestRuns.CreateIncidentRun();
        return AgentTimelineModel.Build(run.Agents, run.Spans, run.Handoffs, run.Duration);
    }

    private static int SpanOf(AgentTimelineModel model, string agent, double start)
        => model.Spans.Single(s => s.Span.AgentId == agent && Math.Abs(s.Start - start) < 1e-9).Index;

    [Fact]
    public void Sample_Normalizes_Into_Rows_In_Agent_Order()
    {
        var model = Sample();
        model.Rows.Select(r => r.Agent.Id).ShouldBe(["lead", "status", "config", "logs", "scribe"]);
        model.Rows.Select(r => r.Count).ShouldBe([6, 5, 7, 9, 3]);
        model.Spans.Length.ShouldBe(30);
        model.Handoffs.Length.ShouldBe(8);
        model.Duration.ShouldBe(26.8, 1e-9);
        model.DataEnd.ShouldBe(26.8, 1e-9);
        // Each row is sorted by start and its spans follow each other in the array.
        foreach (var row in model.Rows)
        {
            for (var i = row.First + 1; i < row.First + row.Count; i++)
            {
                model.Spans[i].Start.ShouldBeGreaterThanOrEqualTo(model.Spans[i - 1].Start);
                model.Spans[i].Row.ShouldBe(row.Index);
            }
        }
    }

    [Fact]
    public void Unsorted_Spans_And_Unknown_Agents_Are_Handled()
    {
        var agents = new[] { new AgentTimelineAgent("a", "A"), new AgentTimelineAgent("b", "B") };
        var spans = new[]
        {
            new AgentTimelineSpan("b", AgentTimelineSpanKind.Thinking, 3, 4, "late"),
            new AgentTimelineSpan("a", AgentTimelineSpanKind.Thinking, 1, 2, "second"),
            new AgentTimelineSpan("ghost", AgentTimelineSpanKind.Tool, 0, 9, "unknown agent"),
            new AgentTimelineSpan("a", AgentTimelineSpanKind.Thinking, 0, 1, "first"),
            new AgentTimelineSpan("b", AgentTimelineSpanKind.Tool, 2, null, "open")
        };
        var handoffs = new[]
        {
            new AgentTimelineHandoff("a", "ghost", 1),
            new AgentTimelineHandoff("a", "a", 1),
            new AgentTimelineHandoff("a", "b", 1.5)
        };
        var model = AgentTimelineModel.Build(agents, spans, handoffs, null);
        model.Spans.Select(s => s.Span.Label).ShouldBe(["first", "second", "open", "late"]);
        model.Handoffs.Length.ShouldBe(1);
        model.HasOpenSpans.ShouldBeTrue();
        model.DataEnd.ShouldBe(4);
        model.Spans[2].EndAt(6).ShouldBe(6);
        model.Spans[2].EndAt(1).ShouldBe(2);
    }

    [Fact]
    public void Handoff_Shapes_And_Endpoints_Are_Inferred()
    {
        var model = Sample();
        var dispatch = model.Handoffs.Single(h => h.At == 2.1);
        dispatch.IsDispatch.ShouldBeTrue();
        dispatch.UpperRow.ShouldBe(0);
        dispatch.LowerRow.ShouldBe(3);
        // A dispatch drops at its time into the target's next span (the log reader's first, 2.4 s, 0.3 s later).
        dispatch.UpperSpan.ShouldBe(SpanOf(model, "lead", 2.1));
        dispatch.LowerSpan.ShouldBe(SpanOf(model, "logs", 2.4));

        var result = model.Handoffs.Single(h => h.At == 9.6);
        result.IsDispatch.ShouldBeFalse();
        result.UpperRow.ShouldBe(0);
        result.LowerRow.ShouldBe(1);
        // A result leaves the end of the source's last span (9.3 s) and rises to the orchestrator's span holding 9.6 s.
        result.LowerSpan.ShouldBe(SpanOf(model, "status", 7.9));
        result.UpperSpan.ShouldBe(SpanOf(model, "lead", 2.1));

        // At a boundary the span that starts there holds the handoff ([start, end)).
        var back = model.Handoffs.Single(h => h.At == 14.9);
        back.UpperSpan.ShouldBe(SpanOf(model, "lead", 14.9));
        back.LowerSpan.ShouldBe(SpanOf(model, "logs", 13.2));

        var scribe = model.Handoffs.Single(h => h.At == 17.0);
        scribe.UpperSpan.ShouldBe(SpanOf(model, "lead", 17.0));
        scribe.LowerSpan.ShouldBe(SpanOf(model, "scribe", 17.3));
    }

    [Fact]
    public void Spans_List_Their_Handoffs()
    {
        var model = Sample();
        // The coordinator's long wait holds five handoffs: three dispatches and two results.
        model.Spans[SpanOf(model, "lead", 2.1)].Handoffs.Count.ShouldBe(5);
        model.Spans[SpanOf(model, "lead", 14.9)].Handoffs.Count.ShouldBe(1);
        model.Spans[SpanOf(model, "lead", 0)].Handoffs.Count.ShouldBe(0);
        model.Spans[SpanOf(model, "status", 3.6)].Handoffs.Count.ShouldBe(1);
        model.Spans[SpanOf(model, "status", 7.9)].Handoffs.Count.ShouldBe(1);
        model.Spans[SpanOf(model, "status", 4.2)].Handoffs.Count.ShouldBe(0);
    }

    [Fact]
    public void Summary_Matches_The_Sample_Totals()
    {
        var model = Sample();
        var summary = model.Summary(model.Duration, model.Duration);
        summary.AgentCount.ShouldBe(5);
        summary.Duration.ShouldBe(26.8, 1e-9);
        summary.Tokens.ShouldBe(49_920);
        summary.Cost.ShouldBe(0.150961, 1e-9);
        // Busy time 40.6 s (waiting excluded) over 26.8 s of wall time.
        summary.Speedup.ShouldBe(40.6 / 26.8, 1e-9);
        AgentTimelineMath.Speedup(summary.Speedup).ShouldBe("1.5");
        AgentTimelineMath.Cost(summary.Cost).ShouldBe("$0.15");
        AgentTimelineMath.Tokens(summary.Tokens).ShouldBe("49,920");
    }

    [Fact]
    public void Counters_Accrue_Tokens_Evenly_Over_Each_Span()
    {
        var model = Sample();
        // 2,050 tokens over the first 2.1 s.
        model.CountsAt(1.0, model.Duration).Tokens.ShouldBe(976);
        model.CountsAt(0.0667, model.Duration).Tokens.ShouldBe(65);
        model.CountsAt(0, model.Duration).Tokens.ShouldBe(0);
        model.CountsAt(26.8, model.Duration).Tokens.ShouldBe(49_920);
        // The coordinator's price is $8 per million.
        model.CountsAt(1.0, model.Duration).Cost.ShouldBe(2050 / 2.1 * 8 / 1e6, 1e-12);
        AgentTimelineMath.Cost(model.CountsAt(1.0, model.Duration).Cost).ShouldBe("$0.01");
    }

    [Fact]
    public void Working_Counts_Agents_Thinking_Or_Calling_Tools()
    {
        var model = Sample();
        model.CountsAt(1.0, model.Duration).Working.ShouldBe(1);
        model.CountsAt(3.05, model.Duration).Working.ShouldBe(2);
        model.CountsAt(3.65, model.Duration).Working.ShouldBe(3);
        // 14.7 s: the coordinator waits and every subagent is done.
        model.CountsAt(14.7, model.Duration).Working.ShouldBe(0);
    }

    [Fact]
    public void Formats_Round_Like_The_Reference()
    {
        AgentTimelineMath.Seconds(0).ShouldBe("0.0s");
        AgentTimelineMath.Seconds(0.067).ShouldBe("0.1s");
        AgentTimelineMath.Seconds(26.382).ShouldBe("26.4s");
        AgentTimelineMath.Seconds(26.8).ShouldBe("26.8s");
        AgentTimelineMath.Seconds(65.3).ShouldBe("1m 05.3s");
        AgentTimelineMath.Tokens(51_210).ShouldBe("51,210");
        AgentTimelineMath.Tokens(64).ShouldBe("64");
        AgentTimelineMath.Cost(608 * 8 / 1e6).ShouldBe("$0.00");
        AgentTimelineMath.Cost(656 * 8 / 1e6).ShouldBe("$0.01");
        AgentTimelineMath.Speedup(1.508).ShouldBe("1.5");
        AgentTimelineMath.Speed(0.5).ShouldBe("0.5");
        AgentTimelineMath.Speed(2).ShouldBe("2");
        AgentTimelineMath.Tick(0).ShouldBe("0s");
        AgentTimelineMath.Tick(25).ShouldBe("25s");
        AgentTimelineMath.Tick(0.5).ShouldBe("0.5s");
        AgentTimelineMath.Tick(90).ShouldBe("1m 30s");
        AgentTimelineMath.Tick(120).ShouldBe("2m");
    }

    [Fact]
    public void Tick_Step_Gives_At_Most_Six_Ticks_From_Zero()
    {
        // The reference: 26.4 s is 0 to 25 s in 5 s steps at every width from 300 to 532 px.
        AgentTimelineMath.TickStep(26.4, 530).ShouldBe(5);
        AgentTimelineMath.TickStep(26.4, 300).ShouldBe(5);
        AgentTimelineMath.TickCount(26.4, 5).ShouldBe(6);
        AgentTimelineMath.TickStep(26.8, 530).ShouldBe(5);
        AgentTimelineMath.TickStep(6.2, 530).ShouldBe(2);
        AgentTimelineMath.TickStep(5.8, 530).ShouldBe(1);
        AgentTimelineMath.TickCount(5.8, 1).ShouldBe(6);
        AgentTimelineMath.TickStep(30, 530).ShouldBe(10);
        AgentTimelineMath.TickStep(60, 530).ShouldBe(15);
        AgentTimelineMath.TickStep(0.8, 530).ShouldBe(0.2);
        // Too tight: the next step up.
        AgentTimelineMath.TickStep(26.8, 150).ShouldBe(10);
    }

    [Fact]
    public void Live_Axis_Grows_In_Calm_Steps()
    {
        AgentTimelineMath.LiveAxis(0, 0).ShouldBe(5);
        AgentTimelineMath.LiveAxis(3, 0).ShouldBe(5);
        AgentTimelineMath.LiveAxis(4.4, 5).ShouldBe(5);
        AgentTimelineMath.LiveAxis(4.6, 5).ShouldBe(8);
        AgentTimelineMath.LiveAxis(7.3, 8).ShouldBe(12);
        AgentTimelineMath.LiveAxis(11, 12).ShouldBe(20);
        AgentTimelineMath.LiveAxis(18.5, 20).ShouldBe(30);
        // It never shrinks.
        AgentTimelineMath.LiveAxis(1, 30).ShouldBe(30);
        // Joining a run in progress starts with room past the time already elapsed.
        AgentTimelineMath.LiveAxis(14, 0).ShouldBe(25);
    }

    [Fact]
    public void Keyboard_Moves_By_Row_And_By_Time()
    {
        var model = Sample();
        var end = model.Duration;
        var first = SpanOf(model, "lead", 0);
        model.FirstMounted(end).ShouldBe(first);
        model.Navigate(first, AgentTimelineKeyMove.Previous, end, end).ShouldBe(first);
        model.Navigate(first, AgentTimelineKeyMove.Next, end, end).ShouldBe(SpanOf(model, "lead", 2.1));
        model.Navigate(first, AgentTimelineKeyMove.Up, end, end).ShouldBe(first);
        // Down: the nearest middle in the next row.
        model.Navigate(SpanOf(model, "lead", 2.1), AgentTimelineKeyMove.Down, end, end).ShouldBe(SpanOf(model, "status", 7.9));
        model.Navigate(first, AgentTimelineKeyMove.Down, end, end).ShouldBe(SpanOf(model, "status", 3.6));
        var logs = SpanOf(model, "logs", 8.4);
        model.Navigate(logs, AgentTimelineKeyMove.First, end, end).ShouldBe(SpanOf(model, "logs", 2.4));
        model.Navigate(logs, AgentTimelineKeyMove.Last, end, end).ShouldBe(SpanOf(model, "logs", 13.2));
        model.Navigate(SpanOf(model, "scribe", 19.0), AgentTimelineKeyMove.Down, end, end).ShouldBe(SpanOf(model, "scribe", 19.0));
        model.Navigate(SpanOf(model, "scribe", 19.0), AgentTimelineKeyMove.Next, end, end).ShouldBe(SpanOf(model, "scribe", 19.0));
    }

    [Fact]
    public void Keyboard_Only_Reaches_Spans_That_Have_Started()
    {
        var model = Sample();
        var wait = SpanOf(model, "lead", 2.1);
        // At 3.0 s the status watcher has not started: Down skips its row to the config auditor.
        model.Navigate(wait, AgentTimelineKeyMove.Down, 3.0, 3.0).ShouldBe(SpanOf(model, "config", 3.0));
        // End stops at the last span that has started.
        model.Navigate(SpanOf(model, "logs", 2.4), AgentTimelineKeyMove.Last, 3.2, 3.2).ShouldBe(SpanOf(model, "logs", 3.1));
        model.Navigate(SpanOf(model, "logs", 3.1), AgentTimelineKeyMove.Next, 3.2, 3.2).ShouldBe(SpanOf(model, "logs", 3.1));
        model.IsMounted(SpanOf(model, "scribe", 17.3), 10).ShouldBeFalse();
    }

    [Fact]
    public void Pinning_A_Subagent_Lights_Its_Row_And_Its_Handoffs()
    {
        var model = Sample();
        var spans = new bool[model.Spans.Length];
        var links = new bool[model.Handoffs.Length];
        model.PathOf(SpanOf(model, "status", 3.6), spans, links);
        for (var i = model.Rows[1].First; i < model.Rows[1].First + model.Rows[1].Count; i++)
        {
            spans[i].ShouldBeTrue();
        }
        spans[SpanOf(model, "lead", 2.1)].ShouldBeTrue();
        spans[SpanOf(model, "lead", 0)].ShouldBeFalse();
        spans[SpanOf(model, "logs", 2.4)].ShouldBeFalse();
        links.Count(on => on).ShouldBe(2);
        model.Handoffs.Where(h => links[h.Index]).Select(h => h.At).ShouldBe([3.3, 9.6]);
    }

    [Fact]
    public void Pinning_An_Orchestrator_Span_Lights_The_Ends_Of_Its_Handoffs()
    {
        var model = Sample();
        var spans = new bool[model.Spans.Length];
        var links = new bool[model.Handoffs.Length];
        model.PathOf(SpanOf(model, "lead", 2.1), spans, links);
        links.Count(on => on).ShouldBe(5);
        spans.Count(on => on).ShouldBe(6);
        spans[SpanOf(model, "logs", 2.4)].ShouldBeTrue();
        spans[SpanOf(model, "config", 3.0)].ShouldBeTrue();
        spans[SpanOf(model, "config", 9.9)].ShouldBeTrue();
        spans[SpanOf(model, "status", 7.9)].ShouldBeTrue();
        spans[SpanOf(model, "logs", 13.2)].ShouldBeFalse();

        // A span without handoffs lights only itself.
        model.PathOf(SpanOf(model, "lead", 0), spans, links);
        spans.Count(on => on).ShouldBe(1);
        links.Count(on => on).ShouldBe(0);
    }

    [Fact]
    public void Default_Span_Name_Reads_Everything_The_Card_Shows()
    {
        var run = AgentTimelineTestRuns.CreateIncidentRun();
        var labels = new AgentTimelineLabels();
        var logs = run.Agents.Single(a => a.Id == "logs");
        var failed = run.Spans.Single(s => s.Status == AgentTimelineSpanStatus.Failed);
        labels.DescribeSpan(logs, failed).ShouldBe("Log reader, Tool call, Mail logs, failed: Bounce scan took too long, 3.1s to 5.2s, 430 tokens");
        var wait = run.Spans.First(s => s.Kind == AgentTimelineSpanKind.Waiting);
        labels.DescribeSpan(run.Agents[0], wait).ShouldBe("Coordinator, Waiting: Waiting on logs, config and status, 2.1s to 14.9s");
        var open = new AgentTimelineSpan("lead", AgentTimelineSpanKind.Thinking, 3, null, "Still thinking");
        labels.DescribeSpan(run.Agents[0], open).ShouldBe("Coordinator, Thinking: Still thinking, from 3.0s, still running");
    }

    [Fact]
    public void Layout_Rules_Match_The_Measurements()
    {
        // 744 px card: 742 inside the border, 176 labels, 12 + 24 track padding, 530 px plot.
        AgentTimelineLayout.PlotWidth(742, narrow: false).ShouldBe(530);
        AgentTimelineLayout.PlotWidth(470, narrow: true).ShouldBe(310);
        AgentTimelineLayout.PlotWidth(430, narrow: true).ShouldBe(300);
        AgentTimelineLayout.AvailablePlotWidth(430, narrow: true).ShouldBeLessThan(300);
        AgentTimelineLayout.RowsHeight(5).ShouldBe(226);
        AgentTimelineLayout.PlotHeight(5).ShouldBe(256);
        AgentTimelineLayout.BarBottom(0).ShouldBe(36);
        AgentTimelineLayout.BarCenter(3).ShouldBe(6 + 3 * 44 + 22);
        var rect = AgentTimelineLayout.SpanRect(2, 4, 20, 1);
        rect.X.ShouldBe(40.75);
        rect.Width.ShouldBe(38.5);
        rect.Y.ShouldBe(64);
        rect.Height.ShouldBe(16);
        AgentTimelineLayout.SpanRect(2, 2.05, 20, 1).Width.ShouldBe(0);
    }

    [Fact]
    public void Shine_Sweeps_For_1_05_Seconds_Then_Rests()
    {
        AgentTimelineLayout.ShineOffset(0, 100).ShouldBe(-28, 1e-9);
        AgentTimelineLayout.ShineOffset(1.05 - 1e-9, 100).ShouldBe(100, 1e-3);
        AgentTimelineLayout.ShineOffset(1.2, 100).ShouldBe(100);
        AgentTimelineLayout.ShineOffset(1.5, 100).ShouldBe(-28, 1e-9);
        // Half way on the in-out curve: the band is centered on the span.
        AgentTimelineLayout.ShineOffset(0.525, 100).ShouldBe(-28 + 0.5 * 128, 1);
    }

    // ---- Clock ---------------------------------------------------------------------------------------------------

    private sealed class FakeTime : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = new(2026, 10, 23, 12, 0, 0, TimeSpan.Zero);

        public override DateTimeOffset GetUtcNow() => Now;
    }

    [AvaloniaFact]
    public void Clock_Plays_Pauses_Seeks_And_Scales_With_Speed()
    {
        using var clock = new AiPlaybackClock(new Border()) { Duration = 26.8 };
        clock.Play();
        clock.IsPlaying.ShouldBeTrue();
        for (var i = 0; i < 60; i++)
        {
            clock.Step(1.0 / 60);
        }
        clock.Time.ShouldBe(1.0, 1e-9);
        clock.Speed = 2;
        for (var i = 0; i < 30; i++)
        {
            clock.Step(1.0 / 60);
        }
        clock.Time.ShouldBe(2.0, 1e-9);
        clock.Pause();
        clock.Step(0.05);
        clock.Time.ShouldBe(2.0, 1e-9);
        clock.Seek(11.1);
        clock.Time.ShouldBe(11.1);
        clock.IsPlaying.ShouldBeFalse();
        clock.Seek(-3);
        clock.Time.ShouldBe(0);
        clock.Seek(99);
        clock.Time.ShouldBe(26.8);
        clock.IsAtEnd.ShouldBeTrue();
    }

    [AvaloniaFact]
    public void Clock_Caps_A_Stalled_Frame()
    {
        using var clock = new AiPlaybackClock(new Border()) { Duration = 26.8 };
        clock.Play();
        // A 516 ms stall adds only 0.1 s of run time at 1x (0.2 s at 2x).
        clock.Step(0.516);
        clock.Time.ShouldBe(0.1, 1e-9);
        clock.Speed = 2;
        clock.Step(0.516);
        clock.Time.ShouldBe(0.3, 1e-9);
        clock.WallTime.ShouldBe(1.032, 1e-9);
    }

    [AvaloniaFact]
    public void Clock_Stops_At_The_End_And_Replays_From_Zero()
    {
        using var clock = new AiPlaybackClock(new Border()) { Duration = 1 };
        var ends = 0;
        clock.ReachedEnd += () => ends++;
        clock.Play();
        for (var i = 0; i < 90; i++)
        {
            clock.Step(1.0 / 60);
        }
        clock.Time.ShouldBe(1);
        clock.IsPlaying.ShouldBeFalse();
        ends.ShouldBe(1);
        clock.Play();
        clock.Time.ShouldBe(0);
        clock.IsPlaying.ShouldBeTrue();
        clock.PlayFrom(0.5);
        clock.Time.ShouldBe(0.5);
        clock.IsPlaying.ShouldBeTrue();
    }

    [AvaloniaFact]
    public void Clock_Follows_The_Wall_Clock_In_Live_Mode()
    {
        var time = new FakeTime();
        using var clock = new AiPlaybackClock(new Border(), time) { Duration = 5 };
        clock.StartLive(time.Now.AddSeconds(-4));
        clock.Time.ShouldBe(4, 1e-9);
        time.Now = time.Now.AddSeconds(3);
        clock.Step(1.0 / 60);
        clock.Time.ShouldBe(7, 1e-9);
        // Seeking and playing do nothing while live.
        clock.Seek(1);
        clock.Play();
        clock.Time.ShouldBe(7, 1e-9);
        clock.IsLive.ShouldBeTrue();
        clock.Duration = 7.5;
        clock.StopLive();
        clock.IsLive.ShouldBeFalse();
        clock.Time.ShouldBe(7.5);
        clock.IsAtEnd.ShouldBeTrue();

        // Without a start time the clock counts from now.
        clock.StartLive(null);
        clock.Time.ShouldBe(0, 1e-9);
        time.Now = time.Now.AddSeconds(2);
        clock.Step(0.016);
        clock.Time.ShouldBe(2, 1e-9);
    }

    [Fact]
    public void Run_Lists_Are_Observable_Collections()
    {
        var run = new AgentTimelineRun();
        run.Spans.ShouldBeOfType<AvaloniaList<AgentTimelineSpan>>();
        run.Agents.ShouldBeEmpty();
        run.Handoffs.ShouldBeEmpty();
    }
}
