using System.Collections.ObjectModel;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace TKCode.Arc.Tests.Blocks.Ai;

/// <summary><see cref="ResearchRun"/>: the sample run's phases, app data, citations, folding and Stop.</summary>
public class ResearchRunTests
{
    private static void Settle(Window window)
    {
        Dispatcher.UIThread.RunJobs();
        window.UpdateLayout();
        Dispatcher.UIThread.RunJobs();
    }

    private static (ResearchRun Run, Window Window) ShowSample()
    {
        var run = new ResearchRun { Width = 800, Schedule = static (_, _) => new NoTimer() };
        var window = TestHelpers.Show(run);
        Settle(window);
        return (run, window);
    }

    private sealed class NoTimer : IDisposable
    {
        public void Dispose()
        {
        }
    }

    private static ResearchRun Finished()
    {
        var sources = ResearchRunSamples.Sources();
        foreach (var source in sources)
        {
            source.Status = ResearchRunSourceStatus.Read;
            source.Progress = 1;
        }
        return new ResearchRun
        {
            Width = 800,
            Plan = ResearchRunSamples.Plan(),
            CompletedPlanSteps = 4,
            Sources = sources,
            Notes = [new ResearchRunNote("A finding.", [1])],
            Report = ResearchRunSamples.Report,
            Phase = ResearchRunPhase.Done
        };
    }

    [AvaloniaFact]
    public void Sample_Run_Goes_Through_Every_Phase_To_A_Cited_Report()
    {
        var (run, window) = ShowSample();
        run.Phase.ShouldBe(ResearchRunPhase.Planning);
        var phases = new List<ResearchRunPhase>();
        run.PropertyChanged += (_, e) =>
        {
            if (e.Property == ResearchRun.PhaseProperty)
            {
                phases.Add(run.Phase);
            }
        };
        for (var i = 0; i < 2000 && run.Phase != ResearchRunPhase.Done; i++)
        {
            run.SimulationTick();
        }
        Settle(window);
        phases.ShouldBe([ResearchRunPhase.Reading, ResearchRunPhase.Writing, ResearchRunPhase.Done]);
        run.ShownSources.Count.ShouldBe(7);
        run.ShownSources.Count(s => s.Status == ResearchRunSourceStatus.Read).ShouldBe(6);
        run.ShownSources.Single(s => s.Index == 6).Status.ShouldBe(ResearchRunSourceStatus.Skipped);
        run.ShownNotes.Count.ShouldBe(6);
        run.ShownReport.ShouldBe(ResearchRunSamples.Report);
        run.IsProcessExpanded.ShouldBeFalse();
        run.Announcer!.Announcement.ShouldBe("Report ready");
    }

    [AvaloniaFact]
    public void Report_Citations_Become_Buttons_That_Raise_SourceInvoked()
    {
        var run = Finished();
        var window = TestHelpers.Show(run);
        Settle(window);
        var citations = run.ReportHost!.Children.OfType<TextBlock>()
            .SelectMany(t => t.Inlines!.OfType<InlineUIContainer>()).Select(c => c.Child).OfType<ResearchRunCitation>().ToList();
        citations.Count.ShouldBe(7);
        citations[0].Content.ShouldBe("1");
        AutomationProperties.GetName(citations[0]).ShouldBe("Source 1: Two winters of heat pumps in row houses");
        ResearchRunSourceEventArgs? invoked = null;
        run.SourceInvoked += (_, e) =>
        {
            invoked = e;
            e.Handled = true;
        };
        run.OpenSource(citations[0].Source!);
        invoked!.ResearchSource.Index.ShouldBe(1);
        // The text around the marks stays, without the brackets.
        var first = (TextBlock)run.ReportHost.Children[0];
        string.Concat(first.Inlines!.OfType<Run>().Select(r => r.Text)).ShouldNotContain("[1]");
    }

    [AvaloniaFact]
    public void Finished_Report_Folds_The_Process_And_Can_Show_It()
    {
        var run = Finished();
        var window = TestHelpers.Show(run);
        Settle(window);
        run.IsProcessExpanded.ShouldBeFalse(); // a run shown as done starts folded
        run.Phase = ResearchRunPhase.Reading;
        run.Phase = ResearchRunPhase.Done;
        run.IsProcessExpanded.ShouldBeFalse();
        run.ToggleButton!.IsVisible.ShouldBeTrue();
        run.SummaryText.ShouldStartWith("Read 7 sources, took 1 notes in");
        run.IsProcessExpanded = true;
        run.ToggleButton.Content.ShouldBe("Hide process");
    }

    [AvaloniaFact]
    public void Source_Rows_Follow_Status_And_Progress()
    {
        var sources = new ObservableCollection<ResearchRunSource> { new() { Index = 1, Title = "Field notes", Domain = "notes.example" } };
        var run = new ResearchRun { Width = 800, Sources = sources, Phase = ResearchRunPhase.Reading, Question = "Why?" };
        var window = TestHelpers.Show(run);
        Settle(window);
        var row = run.RowOf(sources[0])!;
        AutomationProperties.GetName(row).ShouldBe("Source 1: Field notes, notes.example, Queued");
        sources[0].Status = ResearchRunSourceStatus.Reading;
        sources[0].Progress = 0.4;
        row.Progress.ShouldBe(0.4);
        row.Classes.ShouldContain(":reading");
        run.SourcesCountText.ShouldBe("0 of 1 read");
        sources[0].Status = ResearchRunSourceStatus.Read;
        run.SourcesCountText.ShouldBe("1 of 1 read");
        sources.Add(new ResearchRunSource { Index = 2, Title = "Second" });
        run.SourcesCountText.ShouldBe("1 of 2 read");
    }

    [AvaloniaFact]
    public void Stop_Raises_The_Event_And_Keeps_What_Was_Read()
    {
        var (run, window) = ShowSample();
        for (var i = 0; i < 40 && run.Phase != ResearchRunPhase.Reading; i++)
        {
            run.SimulationTick();
        }
        for (var i = 0; i < 20; i++)
        {
            run.SimulationTick();
        }
        var stops = 0;
        run.StopRequested += (_, _) => stops++;
        var notes = run.ShownNotes.Count;
        run.Stop();
        Settle(window);
        stops.ShouldBe(1);
        run.Phase.ShouldBe(ResearchRunPhase.Stopped);
        run.ShownNotes.Count.ShouldBe(notes);
        run.ShownSources.ShouldNotContain(s => s.Status == ResearchRunSourceStatus.Reading);
        run.SimulationTick();
        run.Phase.ShouldBe(ResearchRunPhase.Stopped);
        run.Restart();
        run.Phase.ShouldBe(ResearchRunPhase.Planning);
    }

    [AvaloniaFact]
    public void Narrow_Run_Is_Compact()
    {
        var run = Finished();
        run.Width = 500;
        var window = TestHelpers.Show(run);
        Settle(window);
        run.IsCompact.ShouldBeTrue();
    }
}
