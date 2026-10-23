using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using TKCode.Arc.Motion;

namespace TKCode.Arc.Tests.Special;

/// <summary>The <see cref="ReasoningTree"/> card: story beats, playback, beam width, layouts, keyboard and accessibility.</summary>
public class ReasoningTreeTests
{
    private static readonly TimeSpan Frame = TimeSpan.FromMilliseconds(1000.0 / 60);

    private static (ReasoningTree Tree, Window Window) Open(ManualFrameClock clock, double width = 760,
        ArcMotionLevel level = ArcMotionLevel.Full, bool autoPlay = false, Action<ReasoningTree>? setup = null)
    {
        var tree = new ReasoningTree { AutoPlay = autoPlay, Width = width, VerticalAlignment = Avalonia.Layout.VerticalAlignment.Top };
        setup?.Invoke(tree);
        var window = TestHelpers.Show(tree, level);
        window.Width = width + 80;
        window.Height = 1000;
        Dispatcher.UIThread.RunJobs();
        clock.Advance(TimeSpan.FromMilliseconds(50), Frame);
        return (tree, window);
    }

    private static ReasoningTreeStage Stage(ReasoningTree tree) => tree.Stage!;

    private static int IndexOf(ReasoningTree tree, string label) => tree.SearchModel!.Nodes.First(n => n.Label == label).Index;

    private static ReasoningTreeNode Node(ReasoningTree tree, string label) => Stage(tree).Nodes[IndexOf(tree, label)];

    private static ReasoningThoughtFate Fate(ReasoningTree tree, string label) => tree.Scene.Nodes[IndexOf(tree, label)].Fate;

    [AvaloniaFact]
    public void The_Story_Has_The_Measured_Beats()
    {
        var clock = new ManualFrameClock();
        using var install = clock.Install();
        var (tree, window) = Open(clock);
        try
        {
            // Read 0.56, then propose 0.62, score 0.88, keep 0.76 per depth; the last depth goes from score to answer.
            tree.Script.Count.ShouldBe(9);
            tree.Script.Length.ShouldBe(6.58, 0.0001);
            tree.IsPlaying.ShouldBeFalse();
            tree.Status!.Text.ShouldBe("Ready");
            tree.Scene.Beat.ShouldBe(ReasoningTreeBeat.Ready);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Nine_Steps_Walk_From_Ready_To_The_Answer()
    {
        var clock = new ManualFrameClock();
        using var install = clock.Install();
        var (tree, window) = Open(clock);
        try
        {
            var expected = new[]
            {
                (ReasoningTreeBeat.Propose, 1, "Drafted 3 thoughts"),
                (ReasoningTreeBeat.Score, 1, "Scored 3 thoughts"),
                (ReasoningTreeBeat.Keep, 1, "Kept the top 2"),
                (ReasoningTreeBeat.Propose, 2, "Drafted 4 thoughts"),
                (ReasoningTreeBeat.Score, 2, "Scored 4 thoughts"),
                (ReasoningTreeBeat.Keep, 2, "Kept the top 2"),
                (ReasoningTreeBeat.Propose, 3, "Drafted 4 thoughts"),
                (ReasoningTreeBeat.Score, 3, "Scored 4 thoughts"),
                (ReasoningTreeBeat.Answer, 3, "Answer found")
            };
            foreach (var (beat, depth, status) in expected)
            {
                tree.CanStep.ShouldBeTrue();
                tree.StepForward();
                // A step moves to the next beat at once (the story is a sequence of fixed beats).
                tree.IsStepping.ShouldBeFalse();
                tree.Scene.Beat.ShouldBe(beat);
                tree.Scene.BeatDepth.ShouldBe(depth);
                tree.Status!.Text.ShouldBe(status);
            }
            tree.Status!.Icon.ShouldBe(ArcIcons.CircleCheck);
            tree.IsFinished.ShouldBeTrue();
            tree.CanStep.ShouldBeFalse();
            tree.Answer!.Label.ShouldBe("Small region first");
            Fate(tree, "Mirror, then switch").ShouldBe(ReasoningThoughtFate.OnPath);
            Fate(tree, "Switch by region").ShouldBe(ReasoningThoughtFate.OnPath);
            Fate(tree, "Small region first").ShouldBe(ReasoningThoughtFate.Answer);
            Fate(tree, "Copy in batches").ShouldBe(ReasoningThoughtFate.Kept);
            Fate(tree, "Pause uploads").ShouldBe(ReasoningThoughtFate.Pruned);
            Fate(tree, "Done by Sunday").ShouldBe(ReasoningThoughtFate.Pruned);
            Fate(tree, "Freeze all weekend").ShouldBe(ReasoningThoughtFate.Hidden);
            tree.Scene.CountText.ShouldBe([null, "2 of 3 kept", "2 of 4 kept", "Best of 4"]);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Steps_Are_Announced_And_Playing_Stays_Quiet()
    {
        var clock = new ManualFrameClock();
        using var install = clock.Install();
        var (tree, window) = Open(clock);
        try
        {
            var announcer = tree.Announcer!;
            tree.StepForward();
            announcer.Announcement.ShouldBe("Depth 1: 3 thoughts proposed.");
            tree.StepForward();
            announcer.Announcement.ShouldBe("Depth 1: 3 scored. Highest is Copy in batches at 0.80.");
            tree.StepForward();
            announcer.Announcement.ShouldBe("Depth 1: kept 2 of 3, pruned 1.");
            tree.Play();
            var heard = announcer.History.Count;
            clock.Advance(TimeSpan.FromSeconds(1.5), Frame);
            // While it plays on its own the region says nothing more.
            announcer.History.Count.ShouldBe(heard);
            tree.Status!.IsBusy.ShouldBeTrue();
            clock.Advance(TimeSpan.FromSeconds(4), Frame);
            tree.IsFinished.ShouldBeTrue();
            announcer.Announcement.ShouldBe(
                "Answer found with a score of 0.93: Switch the smallest region Saturday and the rest Sunday if error rates stay flat.");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Playing_Reaches_The_Answer_And_Raises_Answered()
    {
        var clock = new ManualFrameClock();
        using var install = clock.Install();
        var (tree, window) = Open(clock);
        try
        {
            var raised = new List<ReasoningTreeAnsweredEventArgs>();
            tree.Answered += (_, e) => raised.Add(e);
            tree.Play();
            tree.Status!.Text.ShouldBe("Studying the question");
            tree.Status.IsBusy.ShouldBeTrue();
            clock.Advance(TimeSpan.FromSeconds(0.7), Frame);
            tree.Status.Text.ShouldBe("Drafting 3 thoughts");
            clock.Advance(TimeSpan.FromSeconds(6.2), Frame);
            tree.IsFinished.ShouldBeTrue();
            tree.IsPlaying.ShouldBeFalse();
            raised.Count.ShouldBe(1);
            raised[0].Answer.Label.ShouldBe("Small region first");
            raised[0].Path.Select(t => t.Label).ShouldBe(["Mirror, then switch", "Switch by region", "Small region first"]);
            raised[0].BeamWidth.ShouldBe(2);
            raised[0].Score.ShouldBe(0.93);
            // The path keeps drawing after the last beat, on frames of its own.
            tree.Scene.HasPath.ShouldBeTrue();
            clock.Advance(TimeSpan.FromSeconds(1.5), Frame);
            tree.Scene.PathDrawn.Value(Stage(tree).Now).ShouldBe(1);
            tree.Scene.SheenOpacity.Value(Stage(tree).Now).ShouldBe(0);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Resuming_Continues_Where_The_Pause_Left_The_Beat()
    {
        var clock = new ManualFrameClock();
        using var install = clock.Install();
        var (tree, window) = Open(clock);
        try
        {
            tree.Play();
            clock.Advance(TimeSpan.FromSeconds(0.85), Frame);
            tree.Scene.Beat.ShouldBe(ReasoningTreeBeat.Propose);
            tree.Pause();
            tree.Status!.Text.ShouldBe("Drafted 3 thoughts");
            tree.Status.Icon.ShouldBe(ArcIcons.Pause);
            // The reader paused: the region says where the search stands.
            Dispatcher.UIThread.RunJobs();
            tree.Announcer!.Announcement.ShouldBe("Depth 1: 3 thoughts proposed.");
            clock.Advance(TimeSpan.FromSeconds(2), Frame);
            tree.Scene.Beat.ShouldBe(ReasoningTreeBeat.Propose);
            tree.Play();
            // 1.18 − 0.85 = 0.33 s were left of the beat; the reference would start it over (0.62 s).
            clock.Advance(TimeSpan.FromSeconds(0.4), Frame);
            tree.Scene.Beat.ShouldBe(ReasoningTreeBeat.Score);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_New_Beam_Width_Starts_The_Search_Over_And_Plays()
    {
        var clock = new ManualFrameClock();
        using var install = clock.Install();
        var (tree, window) = Open(clock);
        try
        {
            tree.JumpToEnd();
            tree.Answer!.Label.ShouldBe("Small region first");
            var beam = TestHelpers.Descendant<SegmentedControl>(tree, ReasoningTree.BeamPart);
            beam.SelectedIndex.ShouldBe(1);
            beam.SelectedIndex = 0;
            tree.BeamWidth.ShouldBe(1);
            tree.IsPlaying.ShouldBeTrue();
            tree.IsFinished.ShouldBeFalse();
            tree.Answer.ShouldBeNull();
            tree.Announcer!.Announcement.ShouldBe("Beam width 1. The search starts over.");
            clock.Advance(TimeSpan.FromSeconds(7), Frame);
            tree.Answer!.Label.ShouldBe("Done by Sunday");
            tree.Scene.CountText.ShouldBe([null, "1 of 3 kept", "1 of 2 kept", "Best of 2"]);
            tree.BeamWidth = 9;
            tree.BeamWidth.ShouldBe(3);
            beam.SelectedIndex.ShouldBe(2);
            tree.BeamWidth = 0;
            tree.BeamWidth.ShouldBe(1);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Restart_Hides_The_Thoughts_And_Plays_From_The_Question()
    {
        var clock = new ManualFrameClock();
        using var install = clock.Install();
        var (tree, window) = Open(clock);
        try
        {
            tree.JumpToEnd();
            clock.Advance(TimeSpan.FromSeconds(1.5), Frame);
            tree.Restart();
            tree.IsPlaying.ShouldBeTrue();
            tree.Scene.Beat.ShouldBe(ReasoningTreeBeat.Ready);
            tree.Scene.Nodes.Skip(1).ShouldAllBe(n => n.Fate == ReasoningThoughtFate.Hidden);
            tree.Scene.HasPath.ShouldBeFalse();
            tree.Status!.Text.ShouldBe("Studying the question");
            clock.Advance(TimeSpan.FromSeconds(0.3), Frame);
            // Hidden thoughts collapse once their fade has finished (and the next beat has not come yet).
            Stage(tree).Nodes.Skip(1).ShouldAllBe(n => !n.IsVisible);
            Stage(tree).Nodes[0].IsVisible.ShouldBeTrue();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Wide_Cards_Draw_Four_Columns_And_Narrow_Cards_An_Outline()
    {
        var clock = new ManualFrameClock();
        using var install = clock.Install();
        var (wide, window) = Open(clock, 744);
        try
        {
            var stage = Stage(wide);
            stage.IsWide.ShouldBeTrue();
            stage.Bounds.Width.ShouldBe(694, 0.5);
            stage.Bounds.Height.ShouldBe(394, 0.5);
            wide.Classes.ShouldNotContain(ReasoningTree.CompactPseudoClass);
            stage.RectOf(0).ShouldBe(new Rect(0, 170, 694 * 0.19, 84));
        }
        finally
        {
            window.Close();
        }
        var (narrow, window2) = Open(clock, 500);
        try
        {
            var stage = Stage(narrow);
            stage.IsWide.ShouldBeFalse();
            narrow.Classes.ShouldContain(ReasoningTree.CompactPseudoClass);
            // 62 + 50 · 11 thoughts for a beam of 2.
            stage.Bounds.Height.ShouldBe(612, 0.5);
            narrow.JumpToEnd();
            clock.Advance(TimeSpan.FromSeconds(1.5), Frame);
            var answer = IndexOf(narrow, "Small region first");
            stage.RectOf(answer).X.ShouldBe(48);
            stage.RectOf(IndexOf(narrow, "Copy in batches")).Y.ShouldBe(68);
            stage.RectOf(IndexOf(narrow, "Oldest albums first")).Y.ShouldBe(118);
            TestHelpers.Descendant<TextBlock>(narrow, ReasoningTree.BeamLabelPart).Text.ShouldBe("Beam");
        }
        finally
        {
            window2.Close();
        }
    }

    [AvaloniaFact]
    public void Narrow_Rows_Glide_Down_When_Children_Are_Inserted()
    {
        var clock = new ManualFrameClock();
        using var install = clock.Install();
        var (tree, window) = Open(clock, 500);
        try
        {
            var stage = Stage(tree);
            tree.StepForward();
            tree.StepForward();
            tree.StepForward();
            clock.Advance(TimeSpan.FromSeconds(1), Frame);
            var mirror = IndexOf(tree, "Mirror, then switch");
            stage.RectOf(mirror).Y.ShouldBe(168);
            tree.StepForward();
            // Two rows went in under "Copy in batches": the rows below move down 100px, on a spring.
            stage.RectOf(mirror).Y.ShouldBe(268);
            clock.Advance(TimeSpan.FromMilliseconds(100), Frame);
            var mid = stage.CurrentTop(mirror);
            mid.ShouldBeGreaterThan(168);
            mid.ShouldBeLessThan(268);
            clock.Advance(TimeSpan.FromSeconds(1), Frame);
            stage.CurrentTop(mirror).ShouldBe(268, 0.1);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void The_Tree_Is_One_Tab_Stop_And_Arrows_Move_Through_It()
    {
        var clock = new ManualFrameClock();
        using var install = clock.Install();
        var (tree, window) = Open(clock);
        try
        {
            tree.JumpToEnd();
            clock.Advance(TimeSpan.FromSeconds(1.5), Frame);
            var stage = Stage(tree);
            window.KeyPressQwerty(PhysicalKey.Tab, RawInputModifiers.None);
            stage.Nodes[0].IsFocused.ShouldBeTrue();
            stage.Nodes.Count(n => KeyboardNavigation.GetIsTabStop(n)).ShouldBe(1);
            window.KeyPressQwerty(PhysicalKey.ArrowRight, RawInputModifiers.None);
            Node(tree, "Mirror, then switch").IsFocused.ShouldBeTrue();
            window.KeyPressQwerty(PhysicalKey.ArrowRight, RawInputModifiers.None);
            Node(tree, "Switch by region").IsFocused.ShouldBeTrue();
            window.KeyPressQwerty(PhysicalKey.ArrowUp, RawInputModifiers.None);
            Node(tree, "Dual-write a week").IsFocused.ShouldBeTrue();
            window.KeyPressQwerty(PhysicalKey.ArrowDown, RawInputModifiers.None);
            window.KeyPressQwerty(PhysicalKey.ArrowDown, RawInputModifiers.None);
            // The last shown thought of the column: no wrap.
            Node(tree, "Switch by region").IsFocused.ShouldBeTrue();
            window.KeyPressQwerty(PhysicalKey.ArrowLeft, RawInputModifiers.None);
            Node(tree, "Mirror, then switch").IsFocused.ShouldBeTrue();
            window.KeyPressQwerty(PhysicalKey.End, RawInputModifiers.None);
            Node(tree, "Small region first").IsFocused.ShouldBeTrue();
            stage.Current.ShouldBe(IndexOf(tree, "Small region first"));
            stage.Nodes.Count(n => KeyboardNavigation.GetIsTabStop(n)).ShouldBe(1);
            KeyboardNavigation.GetIsTabStop(Node(tree, "Small region first")).ShouldBeTrue();
            window.KeyPressQwerty(PhysicalKey.Home, RawInputModifiers.None);
            stage.Nodes[0].IsFocused.ShouldBeTrue();
            // Focus shows the tag with the full thought and its fate.
            window.KeyPressQwerty(PhysicalKey.ArrowRight, RawInputModifiers.None);
            stage.Active.ShouldBe(IndexOf(tree, "Mirror, then switch"));
            stage.TagView.IsVisible.ShouldBeTrue();
            stage.TagView.Body.ShouldBe("Mirror every new write to both clusters, backfill the old photos, then move reads over.");
            stage.TagView.Meta.ShouldBe("Score 0.72 · On the winning path");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Before_The_Answer_End_Goes_To_The_Deepest_Shown_Thought()
    {
        var clock = new ManualFrameClock();
        using var install = clock.Install();
        var (tree, window) = Open(clock);
        try
        {
            for (var i = 0; i < 4; i++)
            {
                tree.StepForward();
            }
            var stage = Stage(tree);
            stage.FocusNode(0, NavigationMethod.Tab);
            window.KeyPressQwerty(PhysicalKey.End, RawInputModifiers.None);
            Node(tree, "Oldest albums first").IsFocused.ShouldBeTrue();
            // Right goes to the best shown child; before scoring, the first one.
            window.KeyPressQwerty(PhysicalKey.Home, RawInputModifiers.None);
            window.KeyPressQwerty(PhysicalKey.ArrowRight, RawInputModifiers.None);
            Node(tree, "Copy in batches").IsFocused.ShouldBeTrue();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Thoughts_Are_Tree_Items_Named_With_Text_Score_And_Fate()
    {
        var clock = new ManualFrameClock();
        using var install = clock.Install();
        var (tree, window) = Open(clock);
        try
        {
            var stage = Stage(tree);
            var stagePeer = ControlAutomationPeer.CreatePeerForElement(stage);
            stagePeer.GetAutomationControlType().ShouldBe(AutomationControlType.Tree);
            stagePeer.GetName().ShouldBe("Search tree for " + ReasoningTreeSamples.QuestionDetail);
            AutomationProperties.GetName(stage.Nodes[0]).ShouldBe("Question: " + ReasoningTreeSamples.QuestionDetail);
            tree.StepForward();
            var copy = Node(tree, "Copy in batches");
            AutomationProperties.GetName(copy).ShouldBe(
                "Copy the archive in nightly batches of about 5 TB and keep the old cluster as the source of truth. Not scored yet.");
            tree.JumpToEnd();
            AutomationProperties.GetName(Node(tree, "Small region first")).ShouldBe(
                "Switch the smallest region Saturday and the rest Sunday if error rates stay flat. Score 0.93 · Answer.");
            AutomationProperties.GetName(Node(tree, "Pause uploads")).ShouldEndWith("Score 0.35 · Pruned.");
            AutomationProperties.GetName(copy).ShouldEndWith("Score 0.80 · Kept.");
            var peer = ControlAutomationPeer.CreatePeerForElement(copy);
            peer.GetAutomationControlType().ShouldBe(AutomationControlType.TreeItem);
            peer.GetHelpText().ShouldBe("Depth 1, 1 of 3");
            stage.TagView.IsHitTestVisible.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Reduced_Motion_Opens_On_The_Answer_And_A_New_Beam_Shows_Its_Answer()
    {
        var clock = new ManualFrameClock();
        using var install = clock.Install();
        var (tree, window) = Open(clock, level: ArcMotionLevel.Reduced, autoPlay: true);
        try
        {
            clock.Advance(TimeSpan.FromMilliseconds(300), Frame);
            tree.IsFinished.ShouldBeTrue();
            tree.IsPlaying.ShouldBeFalse();
            tree.Answer!.Label.ShouldBe("Small region first");
            tree.Status!.Text.ShouldBe("Answer found");
            var now = Stage(tree).Now;
            tree.Scene.PathDrawn.Value(now).ShouldBe(1);
            tree.Scene.Nodes[IndexOf(tree, "Small region first")].AnswerMix.Value(now).ShouldBe(1);
            tree.BeamWidth = 1;
            tree.IsFinished.ShouldBeTrue();
            tree.Answer!.Label.ShouldBe("Done by Sunday");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Own_Thoughts_Without_Scores_Get_Seeded_Ones_And_Changes_Search_Again()
    {
        var clock = new ManualFrameClock();
        using var install = clock.Install();
        var b = new ReasoningThought("Add a read replica") { Key = "b" };
        var (tree, window) = Open(clock, setup: t =>
        {
            t.Question = "Which cache plan cuts latency?";
            t.Thoughts.Add(new ReasoningThought("Cache at the edge") { Children = { new ReasoningThought("Purge on publish") } });
            t.Thoughts.Add(b);
        });
        try
        {
            var model = tree.SearchModel!;
            model.ThoughtCount.ShouldBe(3);
            model.Root.Text.ShouldBe("Which cache plan cuts latency?");
            model.Nodes.Skip(1).ShouldAllBe(n => n.IsSeeded);
            var seeded = model.Nodes[IndexOf(tree, "Add a read replica")].Score;
            seeded.ShouldBe(ReasoningTreeSearch.SeededScore(7, "b"));
            b.Score = 0.99;
            Dispatcher.UIThread.RunJobs();
            tree.SearchModel.ShouldNotBeSameAs(model);
            tree.SearchModel!.Nodes[IndexOf(tree, "Add a read replica")].Score.ShouldBe(0.99);
            tree.JumpToEnd();
            tree.Answer.ShouldNotBeNull();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void New_Data_Before_The_Card_Is_Seen_Still_Autoplays()
    {
        var clock = new ManualFrameClock();
        using var install = clock.Install();
        var host = new Panel { Width = 800, Height = 900 };
        var window = TestHelpers.Show(host, ArcMotionLevel.Full);
        try
        {
            window.Width = 900;
            window.Height = 1000;
            var tree = new ReasoningTree { Width = 760, VerticalAlignment = Avalonia.Layout.VerticalAlignment.Top };
            host.Children.Add(tree);
            // Data set from code after the card is attached but before its first layout (a Loaded handler, a view model).
            tree.Thoughts.Add(new ReasoningThought("Only idea", 0.6) { Children = { new ReasoningThought("Follow-up", 0.7) } });
            tree.IsPlaying.ShouldBeFalse();
            Dispatcher.UIThread.RunJobs();
            clock.Advance(TimeSpan.FromMilliseconds(300), Frame);
            tree.IsPlaying.ShouldBeTrue();
            tree.SearchModel!.ThoughtCount.ShouldBe(2);
            clock.Advance(TimeSpan.FromSeconds(4), Frame);
            tree.Answer!.Label.ShouldBe("Follow-up");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void An_Empty_Source_Has_Nothing_To_Search()
    {
        var clock = new ManualFrameClock();
        using var install = clock.Install();
        var (tree, window) = Open(clock, setup: t => t.ThoughtsSource = Array.Empty<ReasoningThought>());
        try
        {
            tree.Script.Count.ShouldBe(0);
            tree.Status!.Text.ShouldBe("No thoughts to search");
            tree.JumpToEnd();
            tree.Answer.ShouldBeNull();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Steady_Scene_Stops_Building_Text()
    {
        var clock = new ManualFrameClock();
        using var install = clock.Install();
        var (tree, window) = Open(clock);
        try
        {
            tree.Play();
            clock.Advance(TimeSpan.FromSeconds(8.5), Frame);
            AvaloniaHeadlessPlatform.ForceRenderTimerTick();
            Dispatcher.UIThread.RunJobs();
            var texts = Stage(tree).Palette.Texts;
            var misses = texts.Misses;
            for (var i = 0; i < 5; i++)
            {
                Stage(tree).OnFrame(Stage(tree).Now + 0.016);
                foreach (var node in Stage(tree).Nodes)
                {
                    node.InvalidateVisual();
                }
                AvaloniaHeadlessPlatform.ForceRenderTimerTick();
                Dispatcher.UIThread.RunJobs();
            }
            texts.Misses.ShouldBe(misses);
        }
        finally
        {
            window.Close();
        }
    }
}
