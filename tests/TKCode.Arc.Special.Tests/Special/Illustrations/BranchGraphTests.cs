using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using TKCode.Arc.Motion;

namespace TKCode.Arc.Tests.Special.Illustrations;

/// <summary>The branch graph: the four previews, picks, the autoplay cycle, inspection, keyboard and the kept API.</summary>
public class BranchGraphTests
{
    private static readonly TimeSpan Frame = TimeSpan.FromMilliseconds(1000.0 / 60);

    private static Window Open(ManualFrameClock clock, Control card, double width = 904)
    {
        var window = TestHelpers.Show(card, ArcMotionLevel.Full);
        window.Width = width + 40;
        window.Height = 900;
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

    private static BranchGraphModel SampleModel() =>
        BranchGraphModel.Build(BranchGraphSamples.CreateCommits(), new BranchGraphLabels(), "Search filters", 317, null, null);

    private static int ItemOf(BranchGraphModel model, string message) => model.Items.First(i => i.Message == message).Index;

    [AvaloniaFact]
    public void Model_Finds_The_Pull_Request_And_Counts_Ahead_And_Behind()
    {
        var model = SampleModel();
        model.Target.ShouldBe("main");
        model.Source.ShouldBe("feat/filters");
        model.PullRequest.Length.ShouldBe(3);
        model.Ahead.ShouldBe(3);
        model.Behind.ShouldBe(3, "the merge, the fix it took in and the newest main commit");
        model.LaneCount.ShouldBe(3);
        var before = model[BranchGraphStrategy.Before];
        before.RowCount.ShouldBe(8);
        model.Items[before.RowItems[0]].Message.ShouldBe("Remember filters in the link", "newest on top");
        model.Items[before.RowItems[^1]].Message.ShouldBe("Set up the catalog page");
        before.Pills[ItemOf(model, "Upgrade the image loader")].Select(p => p.Text).ShouldBe(["main"]);
        before.Pills[ItemOf(model, "Format prices per locale")].ShouldBeEmpty("a merged branch shows no pill");
        before.Summary.ShouldBe("feat/filters is 3 commits ahead of main and 3 behind.");
    }

    [AvaloniaFact]
    public void Model_Previews_Merge_Rebase_And_Squash()
    {
        var model = SampleModel();
        var merge = model[BranchGraphStrategy.Merge];
        merge.RowCount.ShouldBe(9);
        merge.RowItems[0].ShouldBe(model.MergeItem);
        model.Items[model.MergeItem].Message.ShouldBe("Merge pull request #317 from feat/filters");
        model.Items[model.MergeItem].IsMerge.ShouldBeTrue();
        model.Items[model.MergeItem].Additions.ShouldBe(212 + 97 + 131);
        merge.Edges.Count(e => e.Child == model.MergeItem).ShouldBe(2);
        merge.Pills[model.MergeItem].Select(p => p.Text).ShouldBe(["main"]);

        var rebase = model[BranchGraphStrategy.Rebase];
        rebase.RowCount.ShouldBe(8);
        rebase.RowItems.Take(3).ShouldBe(Enumerable.Reverse(model.PullRequest));
        foreach (var i in model.PullRequest)
        {
            rebase.Lane[i].ShouldBe(0, "replayed in the target's lane");
            rebase.Hash[i].ShouldNotBe(model.Items[i].Hash, "with new hashes");
            rebase.Hash[i].Length.ShouldBe(7);
        }
        rebase.Pills[model.PullRequest[^1]].Select(p => p.Text).ShouldBe(["main", "feat/filters"]);
        var tip = ItemOf(model, "Upgrade the image loader");
        rebase.Edges.ShouldContain(e => e.Parent == tip && e.Child == model.PullRequest[0]);

        var squash = model[BranchGraphStrategy.Squash];
        squash.RowCount.ShouldBe(6);
        squash.RowItems[0].ShouldBe(model.SquashItem);
        model.Items[model.SquashItem].Message.ShouldBe("Search filters (#317)");
        model.PullRequest.ShouldAllBe(i => !squash.Shows(i));
        squash.Edges.ShouldContain(e => e.Parent == tip && e.Child == model.SquashItem);
    }

    [AvaloniaFact]
    public void Graph_Opens_Drawn_When_Motion_Is_Off_And_Raises_CommitDrawn_Oldest_First()
    {
        var card = new BranchGraph();
        var drawn = new List<string>();
        card.CommitDrawn += (_, e) => drawn.Add(e.Commit.Message!);
        var window = TestHelpers.Show(card);
        try
        {
            TestHelpers.PumpUntil(() => card.RevealedCount == 8).ShouldBeTrue();
            drawn.First().ShouldBe("Set up the catalog page");
            drawn.Last().ShouldBe("Remember filters in the link");
            card.SelectedStrategy.ShouldBe(BranchGraphStrategy.Before);
            card.IsPlaying.ShouldBeFalse("nothing cycles below full motion");
            card.SummaryText.ShouldBe("feat/filters is 3 commits ahead of main and 3 behind.");
            card.HeadingText.ShouldBe("Search filters");
            var stage = card.Stage.ShouldNotBeNull();
            stage.Bounds.Height.ShouldBe(8 * 44 + 12, 0.5);
            card.StrategyControl.ShouldNotBeNull().IsVisible.ShouldBeTrue();
            AutomationProperties.GetName(card).ShouldBe("Search filters, history of feat/filters and main");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Picking_A_Strategy_Shows_It_And_Raises_SelectionChanged()
    {
        var card = new BranchGraph { AutoPlay = false };
        var changes = new List<BranchGraphSelectionChangedEventArgs>();
        card.SelectionChanged += (_, e) => changes.Add(e);
        var window = TestHelpers.Show(card);
        try
        {
            TestHelpers.PumpUntil(() => card.RevealedCount == 8).ShouldBeTrue();
            var strategy = card.StrategyControl.ShouldNotBeNull();
            strategy.SelectedIndex = (int)BranchGraphStrategy.Squash;
            Dispatcher.UIThread.RunJobs();
            card.SelectedStrategy.ShouldBe(BranchGraphStrategy.Squash);
            changes.Count.ShouldBe(1);
            changes[0].OldValue.ShouldBe(BranchGraphStrategy.Before);
            changes[0].NewValue.ShouldBe(BranchGraphStrategy.Squash);
            changes[0].IsUserChange.ShouldBeTrue();
            card.Announcer.ShouldNotBeNull().Announcement.ShouldBe("3 commits become a single commit on main.");
            var stage = card.Stage!;
            stage.Strategy.ShouldBe(BranchGraphStrategy.Squash);
            Dispatcher.UIThread.RunJobs();
            stage.Bounds.Height.ShouldBe(6 * 44 + 12, 0.5);
            var model = card.GraphModel!;
            stage.PoseOf(model.SquashItem).Opacity.ShouldBe(1);
            stage.PoseOf(model.PullRequest[0]).Opacity.ShouldBe(0);

            card.SelectedStrategy = BranchGraphStrategy.Rebase;
            changes[^1].IsUserChange.ShouldBeFalse("set by code");
            strategy.SelectedIndex.ShouldBe((int)BranchGraphStrategy.Rebase);
            stage.HashShownBy(model.PullRequest[0]).ShouldBe(model.Items[model.PullRequest[0]].RebasedHash);
            stage.PoseOf(model.PullRequest[0]).X.ShouldBe(stage.PoseOf(ItemOf(model, "Add product cards")).X, "in the target's lane");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Rebase_Lifts_And_Flies_The_Commits_Then_Rewrites_Their_Hashes()
    {
        var clock = new ManualFrameClock();
        using var install = clock.Install();
        var card = new BranchGraph { AutoPlay = false };
        var window = Open(clock, card);
        try
        {
            Run(clock, 2);
            card.RevealedCount.ShouldBe(8);
            var stage = card.Stage!;
            var model = card.GraphModel!;
            var oldest = model.PullRequest[0];
            var start = stage.PoseOf(oldest);
            card.SelectedStrategy = BranchGraphStrategy.Rebase;
            Run(clock, 0.5);
            var mid = stage.PoseOf(oldest);
            mid.Lift.ShouldBe(1, 0.01, "lifted into a card");
            mid.Scale.ShouldBe(BranchGraphMotion.LiftScale, 0.02);
            mid.X.ShouldBeLessThan(start.X, "on its way to the target's lane");
            stage.HashShownBy(oldest).ShouldBe(model.Items[oldest].Hash, "the hash rewrites as it lands");
            Run(clock, 2.5);
            var end = stage.PoseOf(oldest);
            end.Lift.ShouldBe(0, 0.001);
            end.Scale.ShouldBe(1, 0.001);
            end.RowY.ShouldBe(2 * 44, 0.01, "third row: the oldest of the three replayed commits");
            stage.HashShownBy(oldest).ShouldBe(model.Items[oldest].RebasedHash);
            stage.IsMoving.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void First_Sight_Sweeps_The_Graph_In_From_The_Oldest_Commit()
    {
        var clock = new ManualFrameClock();
        using var install = clock.Install();
        var card = new BranchGraph { AutoPlay = false };
        var window = Open(clock, card);
        try
        {
            Run(clock, 0.15);
            card.RevealedCount.ShouldBeInRange(1, 3, "the oldest commits land first");
            var stage = card.Stage!;
            var model = card.GraphModel!;
            stage.PoseOf(ItemOf(model, "Remember filters in the link")).Opacity.ShouldBe(0, "the newest lands last");
            Run(clock, 1.5);
            card.RevealedCount.ShouldBe(8);
            stage.IsSweeping.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Autoplay_Cycles_Until_The_Reader_Picks_And_Hover_Holds_It()
    {
        var clock = new ManualFrameClock();
        using var install = clock.Install();
        var card = new BranchGraph();
        var window = Open(clock, card);
        try
        {
            Run(clock, 1.5);
            card.IsPlaying.ShouldBeTrue();
            card.SelectedStrategy.ShouldBe(BranchGraphStrategy.Before);
            Run(clock, 3);
            card.SelectedStrategy.ShouldBe(BranchGraphStrategy.Rebase, "first stop: rebase");
            card.Announcer!.History.ShouldBeEmpty("the cycle is not announced");
            card.OnHoverChanged(true);
            Run(clock, 10);
            card.SelectedStrategy.ShouldBe(BranchGraphStrategy.Rebase, "a resting pointer holds the cycle");
            card.OnHoverChanged(false);
            Run(clock, 1.3);
            card.SelectedStrategy.ShouldBe(BranchGraphStrategy.Merge, "it goes on soon after the pointer leaves");
            card.StrategyControl!.SelectedIndex = (int)BranchGraphStrategy.Before;
            Dispatcher.UIThread.RunJobs();
            card.IsPlaying.ShouldBeFalse();
            Run(clock, 20);
            card.SelectedStrategy.ShouldBe(BranchGraphStrategy.Before, "the reader's pick stops it for good");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void The_Cycle_Repeats_And_StepForward_Moves_To_The_Next_Strategy()
    {
        var clock = new ManualFrameClock();
        using var install = clock.Install();
        var card = new BranchGraph();
        var finished = 0;
        card.Finished += (_, _) => finished++;
        var window = Open(clock, card);
        try
        {
            Run(clock, 22);
            finished.ShouldBeGreaterThanOrEqualTo(1);
            Run(clock, 1);
            card.IsPlaying.ShouldBeTrue("the cycle goes round again");
            card.Pause();
            var at = card.SelectedStrategy;
            card.StepForward();
            Dispatcher.UIThread.RunJobs();
            card.SelectedStrategy.ShouldNotBe(at);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Keyboard_Moves_Through_The_Commits_And_Pins_The_Card()
    {
        var card = new BranchGraph { AutoPlay = false };
        var window = TestHelpers.Show(card);
        try
        {
            TestHelpers.PumpUntil(() => card.RevealedCount == 8).ShouldBeTrue();
            var stage = card.Stage!;
            stage.Focus(NavigationMethod.Tab);
            Dispatcher.UIThread.RunJobs();
            card.SelectedCommit.ShouldNotBeNull().Message.ShouldBe("Remember filters in the link", "arriving with the keyboard inspects the newest");
            stage.CardItem.ShouldBe(stage.ActiveItem);
            window.KeyPressQwerty(PhysicalKey.ArrowDown, RawInputModifiers.None);
            card.SelectedCommit!.Message.ShouldBe("Upgrade the image loader");
            AutomationProperties.GetName(stage).ShouldBe(
                "Upgrade the image loader, e6c1a84 on main by Sam Ortega, 6 hours ago. 18 added, 18 removed, 2 files. Head of main.");
            window.KeyPressQwerty(PhysicalKey.End, RawInputModifiers.None);
            card.SelectedCommit!.Message.ShouldBe("Set up the catalog page");
            window.KeyPressQwerty(PhysicalKey.Home, RawInputModifiers.None);
            card.SelectedCommit!.Message.ShouldBe("Remember filters in the link");
            window.KeyPressQwerty(PhysicalKey.ArrowUp, RawInputModifiers.None);
            card.SelectedCommit!.Message.ShouldBe("Remember filters in the link", "no wrap");
            window.KeyPressQwerty(PhysicalKey.Escape, RawInputModifiers.None);
            card.SelectedCommit.ShouldBeNull();
            stage.ActiveItem.ShouldBe(-1);
            AutomationProperties.GetName(stage).ShouldStartWith("Commits, newest on top. ");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Inspecting_A_Commit_Dims_The_Other_Branches()
    {
        var card = new BranchGraph { AutoPlay = false };
        var window = TestHelpers.Show(card);
        try
        {
            TestHelpers.PumpUntil(() => card.RevealedCount == 8).ShouldBeTrue();
            var commits = BranchGraphSamples.CreateCommits();
            var model = card.GraphModel!;
            card.SelectedCommit = card.Commits.Count > 0 ? card.Commits[0] : model.Items[ItemOf(model, "Upgrade the image loader")].Commit;
            Dispatcher.UIThread.RunJobs();
            var stage = card.Stage!;
            stage.InspectedItem.ShouldBe(ItemOf(model, "Upgrade the image loader"));
            stage.CardItem.ShouldBe(stage.InspectedItem);
            card.SelectedCommit = null;
            Dispatcher.UIThread.RunJobs();
            stage.InspectedItem.ShouldBe(-1);
            commits.Count.ShouldBe(8);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void StartsFinished_Opens_Drawn_Without_The_Cycle()
    {
        var clock = new ManualFrameClock();
        using var install = clock.Install();
        var card = new BranchGraph { StartsFinished = true };
        var window = Open(clock, card);
        try
        {
            card.RevealedCount.ShouldBe(8);
            card.AutoPlay.ShouldBeFalse();
            Run(clock, 8);
            card.IsPlaying.ShouldBeFalse();
            card.SelectedStrategy.ShouldBe(BranchGraphStrategy.Before);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Own_Commits_And_Pull_Request_Drive_The_Previews()
    {
        var card = new BranchGraph
        {
            AutoPlay = false,
            PullRequestTitle = "Article search",
            PullRequestNumber = 1024,
            SourceBranch = "feat/search",
            AreControlsVisible = true
        };
        card.Commits.Add(new BranchGraphCommit("a41c0e7", "main", "Publish the help center"));
        card.Commits.Add(new BranchGraphCommit("6d2e9b1", "feat/search", "Index articles on save"));
        card.Commits.Add(new BranchGraphCommit("f03b58c", "main", "Upgrade to .NET 10", parents: "a41c0e7"));
        card.Commits.Add(new BranchGraphCommit("b7a4e20", "feat/search", "Rank results by freshness"));
        var window = TestHelpers.Show(card);
        try
        {
            TestHelpers.PumpUntil(() => card.RevealedCount == 4).ShouldBeTrue();
            card.HeadingText.ShouldBe("Article search");
            card.SummaryText.ShouldBe("feat/search is 2 commits ahead of main and 1 behind.");
            card.SelectedStrategy = BranchGraphStrategy.Squash;
            card.GraphModel!.Items[card.GraphModel.SquashItem].Message.ShouldBe("Article search (#1024)");
            card.Title = "Custom";
            card.HeadingText.ShouldBe("Custom", "a set Title wins over the pull request's title");
            card.AreControlsVisible = false;
            card.StrategyControl!.IsVisible.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Narrow_Cards_Drop_Columns_And_Put_The_Strategy_Control_On_Its_Own_Row()
    {
        var card = new BranchGraph { AutoPlay = false };
        var window = TestHelpers.Show(card);
        try
        {
            window.Width = 380;
            Dispatcher.UIThread.RunJobs();
            TestHelpers.PumpUntil(() => card.RevealedCount == 8).ShouldBeTrue();
            Grid.GetRow(card.StrategyControl!).ShouldBe(1);
            card.StrategyControl!.Bounds.Width.ShouldBeGreaterThan(card.Bounds.Width * 0.8);
            window.Width = 940;
            Dispatcher.UIThread.RunJobs();
            Grid.GetRow(card.StrategyControl!).ShouldBe(0);
        }
        finally
        {
            window.Close();
        }
    }

#pragma warning disable CS0618
    [AvaloniaFact]
    public void Obsolete_Members_Still_Work()
    {
        var card = new BranchGraph { IsNewestFirst = true };
        card.IsNewestFirst.ShouldBeTrue("kept as a value, with no effect");
        var labels = new BranchGraphLabels();
        labels.Ready.ShouldBe("Ready");
        labels.Drawing.ShouldBe("Drawing");
        labels.Paused.ShouldBe("Paused");
        labels.Done.ShouldBe("Up to date");
        labels.Speed.ShouldBe("Speed");
        labels.ReadyCaption(3).ShouldContain("3 commits");
        labels.FirstCaption("main").ShouldContain("main");
        labels.CommitCaption("Fix", "main").ShouldContain("Fix");
        labels.BranchCaption("b", "main", "a1").ShouldContain("a1");
        labels.MergeCaption("b", "main").ShouldContain("merged");
        labels.TagCaption("v1").ShouldContain("v1");
        labels.DoneCaption(3, 2, 1).ShouldContain("3 commits");
        var window = TestHelpers.Show(card);
        try
        {
            TestHelpers.PumpUntil(() => card.RevealedCount == 8).ShouldBeTrue();
            var model = card.GraphModel!;
            card.Stage!.PoseOf(model[BranchGraphStrategy.Before].RowItems[0]).RowY.ShouldBe(0, "newest on top either way");
            card.IsNewestFirst = false;
            card.Stage!.PoseOf(model[BranchGraphStrategy.Before].RowItems[0]).RowY.ShouldBe(0);
        }
        finally
        {
            window.Close();
        }
    }
#pragma warning restore CS0618
}
