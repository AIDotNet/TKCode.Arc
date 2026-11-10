using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace TKCode.Arc.Tests.Blocks.People;

/// <summary><see cref="LeadScoring"/>: scores from factors, filters, the detail panel, moves and the compact layout.</summary>
public class LeadScoringTests
{
    private static LeadScoring Own()
    {
        var board = new LeadScoring { Title = "Leads" };
        board.Stages.Add(new LeadScoringStage("new", "New"));
        board.Stages.Add(new LeadScoringStage("won", "Won"));
        board.Leads.Add(new LeadScoringLead("a", "Ann Hot", "Alpha", "new")
        {
            Factors = [new("Fit", 50, LeadScoringFactorKind.Fit), new("Demo", 40, LeadScoringFactorKind.Intent)],
        });
        board.Leads.Add(new LeadScoringLead("b", "Bo Warm", "Beta", "new")
        {
            Factors = [new("Fit", 40, LeadScoringFactorKind.Fit), new("Opened", 20, LeadScoringFactorKind.Engagement), new("Bad fit", -5, LeadScoringFactorKind.Fit)],
        });
        board.Leads.Add(new LeadScoringLead("c", "Cy Cold", "Gamma", "won") { Factors = [new("Opened", 10, LeadScoringFactorKind.Engagement)] });
        return board;
    }

    [AvaloniaFact]
    public void Scores_Add_Up_And_Clamp()
    {
        var lead = new LeadScoringLead { Factors = [new("x", 80, LeadScoringFactorKind.Fit), new("y", 40, LeadScoringFactorKind.Intent)] };
        lead.Score.ShouldBe(100);
        lead.Factors.Add(new LeadScoringFactor("z", -200, LeadScoringFactorKind.Fit));
        lead.Score.ShouldBe(0);
        lead.ScoreOverride = 42;
        lead.Score.ShouldBe(42);
        lead.TopFactor!.Label.ShouldBe("x");
    }

    [AvaloniaFact]
    public void Shows_Samples_Without_Leads()
    {
        var board = new LeadScoring();
        var window = TestHelpers.Show(board);
        window.Width = 1100;
        Dispatcher.UIThread.RunJobs();
        board.IsShowingSamples.ShouldBeTrue();
        board.VisibleLeads.Count.ShouldBe(board.EffectiveLeads.Count);
        board.VisibleLeads.Select(l => l.Score).ShouldBeInOrder(SortDirection.Descending);
        window.Close();
    }

    [AvaloniaFact]
    public void Filters_Narrow_The_Table()
    {
        var board = Own();
        var window = TestHelpers.Show(board);
        window.Width = 1100;
        Dispatcher.UIThread.RunJobs();
        board.VisibleLeads.Select(l => l.Key).ShouldBe(["a", "b", "c"]);
        board.Band = LeadScoringBand.Hot;
        board.VisibleLeads.Select(l => l.Key).ShouldBe(["a"]);
        board.Band = LeadScoringBand.All;
        board.StageFilter = "won";
        board.VisibleLeads.Select(l => l.Key).ShouldBe(["c"]);
        board.StageTabs!.SelectedIndex.ShouldBe(2);
        board.StageFilter = null;
        board.SearchText = "beta";
        board.VisibleLeads.Select(l => l.Key).ShouldBe(["b"]);
        board.Table!.ItemsSource.ShouldBe(board.VisibleLeads);
        window.Close();
    }

    [AvaloniaFact]
    public void Moving_Leads_Writes_The_Stage_Raises_And_Announces()
    {
        var board = Own();
        var events = new List<LeadScoringLeadsMovedEventArgs>();
        board.LeadsMoved += (_, e) => events.Add(e);
        var window = TestHelpers.Show(board);
        window.Width = 1100;
        Dispatcher.UIThread.RunJobs();
        board.MoveLeads([board.Leads[0], board.Leads[2]], "won").ShouldBe(1);
        board.Leads[0].StageKey.ShouldBe("won");
        var e = events.Single();
        e.Leads.Single().Key.ShouldBe("a");
        e.FromStageKeys.ShouldBe(["new"]);
        e.Stage.Key.ShouldBe("won");
        board.Announcer!.Announcement.ShouldBe("Moved Ann Hot to Won");
        board.MoveLeads([board.Leads[0]], "nowhere").ShouldBe(0);
        window.Close();
    }

    [AvaloniaFact]
    public void The_Panel_Explains_The_Active_Lead()
    {
        var board = Own();
        var window = TestHelpers.Show(board);
        window.Width = 1100;
        Dispatcher.UIThread.RunJobs();
        board.ActiveLead = board.Leads[1];
        AutomationProperties.GetName(board.Detail!).ShouldBe("Bo Warm");
        board.Detail!.GetVisualDescendants().OfType<LeadScoringBar>().Count(b => b.IsSigned).ShouldBe(3);
        window.Close();
    }

    [AvaloniaFact]
    public void Narrow_Width_Moves_The_Panel_Under_The_Table()
    {
        var board = Own();
        var window = TestHelpers.Show(new Border { Width = 700, Child = board });
        Dispatcher.UIThread.RunJobs();
        board.IsCompact.ShouldBeTrue();
        Grid.GetRow(board.Detail!).ShouldBe(1);
        window.Close();
    }
}
