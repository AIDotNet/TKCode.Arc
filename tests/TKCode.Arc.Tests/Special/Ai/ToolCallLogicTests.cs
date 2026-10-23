using Avalonia.Headless.XUnit;
using Avalonia.Threading;

namespace TKCode.Arc.Tests.Special.Ai;

/// <summary>Pure logic of the tool call parts: formats, header phrasing, totals, JSON, outputs and the simulation script.</summary>
public class ToolCallLogicTests
{
    [Theory]
    [InlineData(760, "760ms")]
    [InlineData(420, "420ms")]
    [InlineData(1180, "1.2s")]
    [InlineData(2970, "3.0s")]
    [InlineData(2360, "2.4s")]
    [InlineData(1000, "1.0s")]
    [InlineData(65000, "1m 05s")]
    public void Durations_Match_The_Measured_Formats(double ms, string text) =>
        ToolCallFormat.FormatDuration(TimeSpan.FromMilliseconds(ms)).ShouldBe(text);

    [Fact]
    public void Total_Counts_Only_Successful_Calls()
    {
        var calls = ToolCallSamples.SettledCalls();
        ToolCallFormat.TotalDuration(calls).ShouldBe(TimeSpan.FromMilliseconds(2970));
        calls[3].Status = ToolCallStatus.Denied;
        ToolCallFormat.TotalDuration(calls).ShouldBe(TimeSpan.FromMilliseconds(2360));
        calls[2].Status = ToolCallStatus.Error;
        ToolCallFormat.TotalDuration(calls).ShouldBe(TimeSpan.FromMilliseconds(1940));
    }

    [Fact]
    public void Header_Phrasing_Priority()
    {
        var labels = ToolCallLabels.Default;
        var calls = ToolCallSamples.SettledCalls().ToList();
        var header = ToolCallFormat.Header(calls, null, labels);
        header.Label.ShouldBe("Used 4 tools");
        header.Meta.ShouldBe("3.0s");
        header.Shimmers.ShouldBeFalse();

        calls[2].Status = ToolCallStatus.Running;
        header = ToolCallFormat.Header(calls, null, labels);
        header.Label.ShouldBe("Using 4 tools");
        header.Shimmers.ShouldBeTrue();
        header.Meta.ShouldBeNull();

        calls[2].Status = ToolCallStatus.Error;
        header = ToolCallFormat.Header(calls, null, labels);
        header.Label.ShouldBe("Used 4 tools");
        header.Meta.ShouldBe("1 failed");
        header.MetaIsFailure.ShouldBeTrue();

        calls[3].Status = ToolCallStatus.Approval;
        ToolCallFormat.Header(calls, null, labels).Label.ShouldBe("Waiting for approval");
        ToolCallFormat.Header(calls.Take(1).ToList(), true, labels).Label.ShouldBe("Using 1 tool");
    }

    [Fact]
    public void Meta_And_Status_Words()
    {
        var item = ToolCallSamples.TicketSearch();
        var labels = ToolCallLabels.Default;
        ToolCallFormat.MetaText(item, labels).ShouldBe("4 results · 1.2s");
        ToolCallFormat.MetaText(item, labels, compact: true).ShouldBe("1.2s");
        ToolCallFormat.MetaText(item, labels, tight: true).ShouldBeNull();
        ToolCallFormat.RowName(item, labels).ShouldBe("Tracker: Searched tickets in finance/close Done, 4 results, 1.2s");
        var error = ToolCallSamples.JobLogQuery(ToolCallStatus.Error);
        ToolCallFormat.RowName(error, labels).ShouldBe("Warehouse: Could not read the export job log Failed: Connection to reporting_replica timed out after 5s");
        ToolCallFormat.MetaText(error, labels).ShouldBeNull();
        var denied = ToolCallSamples.StatusNote(ToolCallStatus.Denied);
        ToolCallFormat.MetaText(denied, labels).ShouldBe("Denied");
    }

    [Fact]
    public void Summary_Is_Derived_From_The_Output()
    {
        var labels = ToolCallLabels.Default;
        new ToolCallListOutput([new("1", "a")]).DeriveSummary(labels).ShouldBe("1 result");
        new ToolCallTableOutput(["a"], [["x"], ["y"]]).DeriveSummary(labels).ShouldBe("2 rows");
        ToolCallJsonOutput.Parse("{\"a\":1,\"b\":2}").DeriveSummary(labels).ShouldBe("2 fields");
        new ToolCallTextOutput("x").DeriveSummary(labels).ShouldBeNull();
    }

    [Fact]
    public void Clipboard_Formats()
    {
        new ToolCallListOutput([new("#1", "One", "tag"), new(null, "Two")]).ToClipboardText().ShouldBe("#1 One tag\nTwo");
        new ToolCallTableOutput(["a", "b"], [["1", null]]).ToClipboardText().ShouldBe("a\tb\n1\tnull");
        new ToolCallTextOutput("hello\nworld").ToClipboardText().ShouldBe("hello\nworld");
    }

    [Fact]
    public void Sample_Checklist_Is_Thirty_Five_Lines_And_Tinted()
    {
        var text = ((ToolCallJsonOutput)ToolCallSamples.ChecklistRead().Output!).Json;
        text.Split('\n').Length.ShouldBe(35);
        var line = ToolCallJson.TokenizeLine("    \"name\": \"Ledger lock\", ");
        line.Select(t => t.Kind).ShouldBe([ToolCallJsonTokenKind.Punctuation, ToolCallJsonTokenKind.Key,
            ToolCallJsonTokenKind.Punctuation, ToolCallJsonTokenKind.String, ToolCallJsonTokenKind.Punctuation]);
        ToolCallJson.TokenizeLine("\"progress\": 0.72,").Select(t => t.Kind).ShouldContain(ToolCallJsonTokenKind.Number);
        ToolCallJson.TokenizeLine("\"x\": null").Last().Kind.ShouldBe(ToolCallJsonTokenKind.Literal);
        string.Concat(ToolCallJson.TokenizeLine("  \"a\": [true, -1.5e3],").Select(t => t.Text)).ShouldBe("  \"a\": [true, -1.5e3],");
    }

    [Fact]
    public void Titles_Fall_Back_To_The_Settled_Phrase()
    {
        var item = ToolCallSamples.StatusNote();
        item.TitleFor(ToolCallStatus.Approval).ShouldBe("Post a status note to #finance-ops");
        item.TitleFor(ToolCallStatus.Running).ShouldBe("Posting to #finance-ops");
        item.TitleFor(ToolCallStatus.Success).ShouldBe("Posted to #finance-ops");
        item.TitleFor(ToolCallStatus.Error).ShouldBe("Posted to #finance-ops");
    }

    private sealed class ManualTime : TimeProvider
    {
        private readonly List<(DateTimeOffset Due, Action Run, bool Live)> _timers = new();
        private DateTimeOffset _now = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

        public override DateTimeOffset GetUtcNow() => _now;

        public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
        {
            var timer = new Handle(this);
            timer.Index = _timers.Count;
            _timers.Add((_now + dueTime, () => callback(state), true));
            return timer;
        }

        public void Advance(TimeSpan by)
        {
            var target = _now + by;
            while (true)
            {
                var next = _timers.Select((t, i) => (t, i)).Where(x => x.t.Live && x.t.Due <= target).OrderBy(x => x.t.Due).FirstOrDefault();
                if (next.t.Run is null)
                {
                    break;
                }
                _now = next.t.Due;
                _timers[next.i] = (next.t.Due, next.t.Run, false);
                next.t.Run();
                Dispatcher.UIThread.RunJobs();
            }
            _now = target;
        }

        private sealed class Handle(ManualTime owner) : ITimer
        {
            public int Index;
            public bool Change(TimeSpan dueTime, TimeSpan period) => false;
            public void Dispose() => owner._timers[Index] = (owner._timers[Index].Due, owner._timers[Index].Run, false);
            public ValueTask DisposeAsync() { Dispose(); return ValueTask.CompletedTask; }
        }
    }

    [AvaloniaFact]
    public void Simulation_Plays_The_Measured_Script()
    {
        var time = new ManualTime();
        using var sim = new ToolCallSimulation(null, time);
        var phases = new List<ToolCallSimulationPhase>();
        sim.Changed += (_, e) => phases.Add(e.Phase);
        sim.Start();
        sim.Items.Count.ShouldBe(0);
        time.Advance(TimeSpan.FromMilliseconds(629));
        sim.Items.Count.ShouldBe(0);
        time.Advance(TimeSpan.FromMilliseconds(2));
        sim.Items.Count.ShouldBe(1);
        sim.Items[0].Status.ShouldBe(ToolCallStatus.Running);
        sim.IsWorking.ShouldBeTrue();
        time.Advance(TimeSpan.FromMilliseconds(1180));
        sim.Items[0].Status.ShouldBe(ToolCallStatus.Success);
        time.Advance(TimeSpan.FromMilliseconds(223));
        sim.Items.Count.ShouldBe(1);
        time.Advance(TimeSpan.FromMilliseconds(2));
        sim.Items.Count.ShouldBe(2);
        time.Advance(TimeSpan.FromMilliseconds(760 + 226));
        sim.Items.Count.ShouldBe(3);
        time.Advance(TimeSpan.FromMilliseconds(1400));
        sim.Items[2].Status.ShouldBe(ToolCallStatus.Error);
        sim.State.ShouldBe(ToolCallSimulationState.WaitingForRetry);
        sim.IsWorking.ShouldBeFalse();
        time.Advance(TimeSpan.FromSeconds(30));
        sim.Items.Count.ShouldBe(3);
        sim.Retry(sim.Items[1]).ShouldBeFalse();
        sim.Retry(sim.Items[2]).ShouldBeTrue();
        sim.Items[2].Status.ShouldBe(ToolCallStatus.Running);
        time.Advance(TimeSpan.FromMilliseconds(700));
        sim.Items[2].Status.ShouldBe(ToolCallStatus.Success);
        time.Advance(TimeSpan.FromMilliseconds(266));
        sim.Items.Count.ShouldBe(4);
        sim.Items[3].Status.ShouldBe(ToolCallStatus.Approval);
        sim.State.ShouldBe(ToolCallSimulationState.WaitingForApproval);
        sim.Approve(sim.Items[3]).ShouldBeTrue();
        time.Advance(TimeSpan.FromMilliseconds(610));
        sim.Items[3].Status.ShouldBe(ToolCallStatus.Success);
        sim.State.ShouldBe(ToolCallSimulationState.Finished);
        ToolCallFormat.FormatDuration(ToolCallFormat.TotalDuration(sim.Items)).ShouldBe("3.0s");
        phases.Last().ShouldBe(ToolCallSimulationPhase.Finished);
    }

    [AvaloniaFact]
    public void Simulation_Deny_Finishes_With_The_Shorter_Total()
    {
        var time = new ManualTime();
        using var sim = new ToolCallSimulation(null, time);
        sim.Start();
        time.Advance(TimeSpan.FromSeconds(5));
        sim.Retry(sim.WaitingCall!);
        time.Advance(TimeSpan.FromSeconds(2));
        sim.State.ShouldBe(ToolCallSimulationState.WaitingForApproval);
        sim.Deny(sim.WaitingCall!).ShouldBeTrue();
        sim.Items[3].Status.ShouldBe(ToolCallStatus.Denied);
        sim.WasDenied.ShouldBeTrue();
        ToolCallFormat.FormatDuration(ToolCallFormat.TotalDuration(sim.Items)).ShouldBe("2.4s");
        sim.State.ShouldBe(ToolCallSimulationState.Finished);
        sim.Start();
        sim.Items.Count.ShouldBe(0);
    }
}
