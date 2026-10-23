using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Shouldly;
using TKCode.Arc.Gallery.Demos;
using TKCode.Arc.Motion;
using Xunit;

namespace TKCode.Arc.Tests.Blocks.Projects;

/// <summary>
/// <see cref="ProjectBoard"/>: columns, cards and counts, moves (end of column, events, cancel, buttons, focus), the
/// filter, the add cycle, the sprint line, screen reader names and announcements, the FLIP flight and the slots that
/// follow it, narrow layouts and scrolling, and the motion levels.
/// </summary>
public class ProjectBoardTests
{
    private sealed record Rig(Window Window, ProjectBoard Board, ProjectPerson Rhea, ProjectPerson Tomas, ProjectPerson Ines)
    {
        public ProjectBoardTask Task(string title) => Board.Tasks.First(t => t.Title == title);

        public ProjectBoardCard Card(string title) => Board.CardFor(Task(title))!;

        public ProjectBoardPanel Panel => Board.Panel!;
    }

    private static Rig Show(double width = 1440, double height = 900, ArcMotionLevel level = ArcMotionLevel.Off,
        Action<ProjectBoard>? configure = null)
    {
        var board = new ProjectBoard();
        var (rhea, tomas, ines) = ProjectBoardDemoData.CreateTeam();
        board.Title = ProjectBoardDemoData.Title;
        board.Stages.AddRange(ProjectBoardDemoData.CreateStages());
        board.Team.AddRange([rhea, tomas, ines]);
        board.CurrentUser = rhea;
        board.Tasks.AddRange(ProjectBoardDemoData.CreateTasks(rhea, tomas, ines));
        board.DraftTitles.AddRange(ProjectBoardDemoData.DraftTitles);
        board.DraftArea = ProjectBoardDemoData.DraftArea;
        configure?.Invoke(board);
        var window = TestHelpers.Show(board, level);
        window.Width = width;
        window.Height = height;
        Pump();
        return new Rig(window, board, rhea, tomas, ines);
    }

    private static void Pump(int rounds = 4)
    {
        for (var i = 0; i < rounds; i++)
        {
            Dispatcher.UIThread.RunJobs();
            AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        }
        Dispatcher.UIThread.RunJobs();
    }

    private static List<string> Titles(ProjectBoard board, int column) => board.Panel!.CardsIn(column).Select(c => c.Task.Title).ToList();

    [AvaloniaFact]
    public void The_Board_Shows_Every_Stage_As_A_Column_With_Its_Cards_And_Counts()
    {
        var rig = Show();
        try
        {
            var board = rig.Board;
            board.Panel!.Columns.Count.ShouldBe(4);
            Titles(board, 0).ShouldBe(["Sketch the coupon entry flow", "Write error copy for card declines"]);
            Titles(board, 1).ShouldBe(["Split the settings API", "Add retry to webhook sender"]);
            Titles(board, 2).ShouldBe(["Contrast audit for receipts"]);
            Titles(board, 3).ShouldBe(["Ship the weekly digest email"]);
            board.Panel.Columns.Select(c => c.CountText).ShouldBe(["2", "2", "1", "1"]);
            board.Panel.Columns.All(c => c.EmptyText is null).ShouldBeTrue();
            board.DoneCount.ShouldBe(1);
            board.TaskCount.ShouldBe(6);
            TestHelpers.Descendant<TextBlock>(board, ProjectBoard.SubtitlePart).Text.ShouldBe("1 of 6 shipped this sprint");

            var done = rig.Card("Ship the weekly digest email");
            done.IsDone.ShouldBeTrue();
            done.Classes.Contains(":done").ShouldBeTrue();
            var soon = rig.Card("Sketch the coupon entry flow");
            soon.Classes.Contains(":due-soon").ShouldBeTrue();
            soon.CommentText.ShouldBe("4");
            rig.Card("Add retry to webhook sender").CommentText.ShouldBe("1");
        }
        finally
        {
            rig.Window.Close();
        }
    }

    [AvaloniaFact]
    public void Columns_And_Cards_Follow_The_Measured_Geometry()
    {
        var rig = Show(width: 1440);
        try
        {
            var panel = rig.Panel;
            panel.Grid.ColumnWidth.ShouldBe(342.5, 0.01);
            panel.Grid.Scrolls.ShouldBeFalse();
            rig.Board.Classes.Contains(":scrolling").ShouldBeFalse();
            var columns = panel.Columns;
            columns[1].Bounds.X.ShouldBe(372.5, 0.5); // layout rounding: Arc's half pixels land on whole ones
            columns[0].TargetSlotHeight.ShouldBe(320);
            var first = rig.Card("Sketch the coupon entry flow");
            first.Bounds.X.ShouldBe(26, 0.01);
            first.Bounds.Y.ShouldBe(34, 0.01);
            first.Bounds.Width.ShouldBe(330.5, 0.5);
            var second = rig.Card("Write error copy for card declines");
            second.Bounds.Y.ShouldBe(first.Bounds.Bottom + 6, 0.01);
            panel.Bounds.Height.ShouldBe(28 + 320 + 20, 0.01);
        }
        finally
        {
            rig.Window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Move_Puts_The_Task_At_The_End_Of_The_Next_Column_And_Raises_The_Events()
    {
        var rig = Show();
        try
        {
            var board = rig.Board;
            var task = rig.Task("Sketch the coupon entry flow");
            ProjectBoardTaskMovingEventArgs? moving = null;
            ProjectBoardTaskMovedEventArgs? moved = null;
            ProjectBoardMoveRequest? request = null;
            board.TaskMoving += (_, e) => moving = e;
            board.TaskMoved += (_, e) =>
            {
                // Raised as the flight starts: the data is already in its new state.
                task.StageKey.ShouldBe("progress");
                moved = e;
            };
            board.MoveCommand = new RelayCommand(p => request = p as ProjectBoardMoveRequest);

            board.MoveNext(task).ShouldBeTrue();
            Pump();
            task.StageKey.ShouldBe("progress");
            board.Tasks[^1].ShouldBe(task);
            Titles(board, 0).ShouldBe(["Write error copy for card declines"]);
            Titles(board, 1).ShouldBe(["Split the settings API", "Add retry to webhook sender", "Sketch the coupon entry flow"]);
            moving.ShouldNotBeNull();
            moving!.FromStage.Key.ShouldBe("planned");
            moving.ToStage.Key.ShouldBe("progress");
            moved.ShouldNotBeNull();
            moved!.FromIndex.ShouldBe(0);
            moved.ToIndex.ShouldBe(2);
            request.ShouldBeNull(); // the command follows the arrow buttons only
            board.Announcer!.Announcement.ShouldBe("Moved Sketch the coupon entry flow to In progress.");
            board.Panel!.Columns.Select(c => c.CountText).ShouldBe(["1", "3", "1", "1"]);
            AutomationProperties.GetName(board.Panel.Columns[0]).ShouldBe("Planned, 1 task");
            AutomationProperties.GetName(board.Panel.Columns[1]).ShouldBe("In progress, 3 tasks");

            // Back to the left also lands at the end (Arc), and the progress follows the finished stage.
            var digest = rig.Task("Ship the weekly digest email");
            board.MovePrevious(digest).ShouldBeTrue();
            Titles(board, 2).ShouldBe(["Contrast audit for receipts", "Ship the weekly digest email"]);
            board.DoneCount.ShouldBe(0);
            rig.Card("Ship the weekly digest email").IsDone.ShouldBeFalse();
            board.MoveTask(digest, "done", 0).ShouldBeTrue();
            board.DoneCount.ShouldBe(1);
            board.MoveTask(rig.Task("Contrast audit for receipts"), "done", 0).ShouldBeTrue();
            Titles(board, 3).ShouldBe(["Contrast audit for receipts", "Ship the weekly digest email"]);
            board.DoneCount.ShouldBe(2);
            var line = TestHelpers.Descendant<ProjectsProgressLine>(board);
            line.Value.ShouldBe(2);
            line.Maximum.ShouldBe(6);

            // Edges and unknown stages do nothing.
            board.MoveNext(digest).ShouldBeFalse();
            board.MovePrevious(rig.Task("Write error copy for card declines")).ShouldBeFalse();
            board.MoveTask(digest, "nowhere").ShouldBeFalse();
        }
        finally
        {
            rig.Window.Close();
        }
    }

    [AvaloniaFact]
    public void TaskMoving_Can_Keep_A_Task_Where_It_Is()
    {
        var rig = Show();
        try
        {
            var moved = 0;
            rig.Board.TaskMoving += (_, e) => e.Cancel = e.ToStage.Key == "review";
            rig.Board.TaskMoved += (_, _) => moved++;
            var task = rig.Task("Split the settings API");
            rig.Board.MoveNext(task).ShouldBeFalse();
            task.StageKey.ShouldBe("progress");
            moved.ShouldBe(0);
            rig.Board.Announcer!.Announcement.ShouldBeNull();
            rig.Board.MovePrevious(task).ShouldBeTrue();
            moved.ShouldBe(1);
        }
        finally
        {
            rig.Window.Close();
        }
    }

    [AvaloniaFact]
    public void The_Arrows_Are_Named_After_Their_Stage_And_Disabled_At_The_Edges()
    {
        var rig = Show();
        try
        {
            var first = rig.Card("Sketch the coupon entry flow");
            first.PreviousButton!.IsEnabled.ShouldBeFalse();
            AutomationProperties.GetName(first.PreviousButton).ShouldBe("Move Sketch the coupon entry flow to the previous stage");
            AutomationProperties.GetName(first.NextButton!).ShouldBe("Move Sketch the coupon entry flow to In progress");
            var last = rig.Card("Ship the weekly digest email");
            last.NextButton!.IsEnabled.ShouldBeFalse();
            AutomationProperties.GetName(last.NextButton).ShouldBe("Move Ship the weekly digest email to the next stage");
            AutomationProperties.GetName(last.PreviousButton!).ShouldBe("Move Ship the weekly digest email to Review");
            AutomationProperties.GetName(first).ShouldBe("Sketch the coupon entry flow, Checkout, due Today, assigned to Rhea Calloway, 4 comments");
            AutomationProperties.GetName(rig.Card("Add retry to webhook sender"))
                .ShouldBe("Add retry to webhook sender, Platform, due Fri, assigned to Tomas Vierra, 1 comment");
            ControlAutomationPeer.CreatePeerForElement(first).GetAutomationControlType().ShouldBe(AutomationControlType.ListItem);
            var columnPeer = ControlAutomationPeer.CreatePeerForElement(rig.Panel.Columns[0]);
            columnPeer.GetAutomationControlType().ShouldBe(AutomationControlType.Group);
            columnPeer.GetName().ShouldBe("Planned, 2 tasks");
            columnPeer.GetChildren().Count.ShouldBe(2);
            ControlAutomationPeer.CreatePeerForElement(rig.Panel).GetChildren().Count.ShouldBe(4);
            AutomationProperties.GetName(rig.Board.FilterBar!).ShouldBe("Show");
            AutomationProperties.GetName(TestHelpers.Descendant<ProjectsAvatarStack>(rig.Board)).ShouldBe("Team");
            AutomationProperties.GetName(TestHelpers.Descendant<ProjectBoardButton>(rig.Board, ProjectBoard.AddPart)).ShouldBe("Add task");
            AutomationProperties.GetName(rig.Board).ShouldBe("Project board");
            AutomationProperties.GetLiveSetting(rig.Board.Announcer!).ShouldBe(AutomationLiveSetting.Polite);
        }
        finally
        {
            rig.Window.Close();
        }
    }

    [AvaloniaFact]
    public void Enter_On_An_Arrow_Moves_The_Card_And_Keeps_Focus_On_It()
    {
        var rig = Show();
        try
        {
            var card = rig.Card("Contrast audit for receipts");
            var command = 0;
            rig.Board.MoveCommand = new RelayCommand(p =>
            {
                command++;
                var request = p.ShouldBeOfType<ProjectBoardMoveRequest>();
                request.Direction.ShouldBe(ProjectBoardMoveDirection.Previous);
                request.ToStage.Key.ShouldBe("progress");
            });
            card.PreviousButton!.Focus(NavigationMethod.Tab);
            rig.Window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
            Pump();
            rig.Task("Contrast audit for receipts").StageKey.ShouldBe("progress");
            command.ShouldBe(1);
            // The same card, the same button: still enabled, still focused.
            card.PreviousButton.IsFocused.ShouldBeTrue();

            // Into the last stage the right arrow turns off: focus goes to the left arrow of the same card.
            rig.Board.MoveCommand = null;
            card.NextButton!.Focus(NavigationMethod.Tab);
            rig.Window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
            Pump();
            rig.Window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
            Pump();
            rig.Task("Contrast audit for receipts").StageKey.ShouldBe("done");
            card.NextButton.IsEnabled.ShouldBeFalse();
            card.PreviousButton.IsFocused.ShouldBeTrue();
            card.PreviousButton.Classes.Contains(":focus-visible").ShouldBeTrue();
            rig.Board.Announcer!.History.ShouldContain("Moved Contrast audit for receipts to Done.");
        }
        finally
        {
            rig.Window.Close();
        }
    }

    [AvaloniaFact]
    public void Tab_Visits_The_Arrows_Column_By_Column_In_Board_Order()
    {
        var rig = Show();
        try
        {
            var add = TestHelpers.Descendant<ProjectBoardButton>(rig.Board, ProjectBoard.AddPart);
            add.Focus(NavigationMethod.Tab);
            var order = new List<string>();
            for (var i = 0; i < 6; i++)
            {
                rig.Window.KeyPressQwerty(PhysicalKey.Tab, RawInputModifiers.None);
                Pump(1);
                order.Add(AutomationProperties.GetName((Control)rig.Window.FocusManager!.GetFocusedElement()!) ?? "?");
            }
            order.ShouldBe([
                "Move Sketch the coupon entry flow to In progress",
                "Move Write error copy for card declines to In progress",
                "Move Split the settings API to Planned",
                "Move Split the settings API to Review",
                "Move Add retry to webhook sender to Planned",
                "Move Add retry to webhook sender to Review",
            ]);

            // After a move the order follows the board, not the order the cards were made in.
            rig.Board.MoveNext(rig.Task("Sketch the coupon entry flow"));
            Pump();
            add.Focus(NavigationMethod.Tab);
            rig.Window.KeyPressQwerty(PhysicalKey.Tab, RawInputModifiers.None);
            Pump(1);
            AutomationProperties.GetName((Control)rig.Window.FocusManager!.GetFocusedElement()!)
                .ShouldBe("Move Write error copy for card declines to In progress");
        }
        finally
        {
            rig.Window.Close();
        }
    }

    [AvaloniaFact]
    public void My_Work_Shows_The_Current_Users_Tasks_And_Keeps_The_Header_Counts()
    {
        var rig = Show();
        try
        {
            var board = rig.Board;
            var changes = new List<(ProjectBoardFilter, ProjectBoardFilter)>();
            board.FilterChanged += (_, e) => changes.Add((e.OldFilter, e.NewFilter));
            var bar = board.FilterBar!;
            bar.AllButton!.IsChecked.ShouldBe(true);
            bar.MineButton!.Focus(NavigationMethod.Tab);
            rig.Window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
            Pump();
            board.Filter.ShouldBe(ProjectBoardFilter.Mine);
            board.Classes.Contains(":filtered").ShouldBeTrue();
            bar.MineButton.IsChecked.ShouldBe(true);
            bar.AllButton.IsChecked.ShouldBe(false);
            changes.ShouldBe([(ProjectBoardFilter.All, ProjectBoardFilter.Mine)]);
            Titles(board, 0).ShouldBe(["Sketch the coupon entry flow"]);
            Titles(board, 1).ShouldBe(["Split the settings API"]);
            Titles(board, 2).ShouldBeEmpty();
            board.Panel!.Columns.Select(c => c.CountText).ShouldBe(["1", "1", "0", "0"]);
            board.Panel.Columns[2].EmptyText.ShouldBe("Nothing assigned to you");
            AutomationProperties.GetName(board.Panel.Columns[2]).ShouldBe("Review, 0 tasks");
            board.TaskCount.ShouldBe(6);
            board.DoneCount.ShouldBe(1);
            board.Panel.Children.OfType<ProjectBoardCard>().Count().ShouldBe(2);
            board.Announcer!.Announcement.ShouldBeNull();

            // Pressing the chosen button again changes nothing; a move under My work still works.
            bar.MineButton.Focus(NavigationMethod.Tab);
            rig.Window.KeyPressQwerty(PhysicalKey.Space, RawInputModifiers.None);
            rig.Window.KeyReleaseQwerty(PhysicalKey.Space, RawInputModifiers.None);
            Pump();
            board.Filter.ShouldBe(ProjectBoardFilter.Mine);
            bar.MineButton.IsChecked.ShouldBe(true);
            board.MoveNext(rig.Task("Split the settings API")).ShouldBeTrue();
            Titles(board, 2).ShouldBe(["Split the settings API"]);

            board.Filter = ProjectBoardFilter.All;
            Pump();
            board.Panel.Columns[1].EmptyText.ShouldBeNull();
            board.Panel.Children.OfType<ProjectBoardCard>().Count().ShouldBe(6);
            board.Panel.Columns[3].EmptyText.ShouldBeNull();

            // Without a current user the switch is hidden and every task shows.
            board.CurrentUser = null;
            board.Filter = ProjectBoardFilter.Mine;
            Pump();
            bar.IsVisible.ShouldBeFalse();
            board.Panel.Children.OfType<ProjectBoardCard>().Count().ShouldBe(6);
        }
        finally
        {
            rig.Window.Close();
        }
    }

    [AvaloniaFact]
    public void Add_Cycles_Through_The_Draft_Titles_And_Announces_Each_Task()
    {
        var rig = Show();
        try
        {
            var board = rig.Board;
            var added = new List<ProjectBoardTask>();
            object? commandParameter = null;
            var stages = new List<string>();
            board.TaskAdded += (_, e) =>
            {
                stages.Add(e.Stage.Key);
                added.Add(e.Task);
            };
            board.AddCommand = new RelayCommand(p => commandParameter = p);
            var add = TestHelpers.Descendant<ProjectBoardButton>(board, ProjectBoard.AddPart);
            add.Focus(NavigationMethod.Tab);
            for (var i = 0; i < 5; i++)
            {
                rig.Window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
                Pump();
            }
            added.Select(t => t.Title).ShouldBe([
                "Map churn interview themes", "Draft the pricing FAQ", "Plan the beta feedback call", "Tidy the empty-state icons",
                "Map churn interview themes",
            ]);
            stages.ShouldAllBe(k => k == "planned");
            var draft = added[0];
            draft.Area.ShouldBe("Insights");
            draft.DueText.ShouldBe("No date");
            draft.Owner.ShouldBe(rig.Rhea);
            draft.CommentCount.ShouldBe(0);
            draft.StageKey.ShouldBe("planned");
            commandParameter.ShouldBe(added[^1]);
            add.IsFocused.ShouldBeTrue();
            Titles(board, 0).Count.ShouldBe(7);
            Titles(board, 0)[^1].ShouldBe("Map churn interview themes");
            board.TaskCount.ShouldBe(11);
            board.DoneCount.ShouldBe(1);
            TestHelpers.Descendant<ProjectsProgressLine>(board).Maximum.ShouldBe(11);
            board.Announcer!.Announcement.ShouldBe("Added Map churn interview themes to Planned.");
            rig.Board.CardFor(draft)!.CommentText.ShouldBeNull();

            // A handler can supply the task, or take over and add nothing.
            board.AddRequested += Supply;
            board.AddTask()!.Title.ShouldBe("From the form");
            board.AddRequested -= Supply;
            board.AddRequested += TakeOver;
            board.AddTask().ShouldBeNull();
            board.TaskCount.ShouldBe(12);

            // AddTask(task, stage) puts it at the end of that stage.
            board.AddRequested -= TakeOver;
            var reviewTask = new ProjectBoardTask("Check the refund email") { Owner = rig.Tomas };
            board.AddTask(reviewTask, "review").ShouldBeTrue();
            Titles(board, 2)[^1].ShouldBe("Check the refund email");
            board.AddTask(reviewTask).ShouldBeFalse();
        }
        finally
        {
            rig.Window.Close();
        }

        static void Supply(object? sender, ProjectBoardAddRequestedEventArgs e) => e.Task = new ProjectBoardTask("From the form");

        static void TakeOver(object? sender, ProjectBoardAddRequestedEventArgs e) => e.Handled = true;
    }

    [AvaloniaFact]
    public void A_Moved_Card_Flies_From_Where_It_Was_Drawn_Above_The_Others()
    {
        var clock = new ManualFrameClock();
        using var installed = clock.Install();
        var rig = Show(level: ArcMotionLevel.Full);
        try
        {
            var task = rig.Task("Sketch the coupon entry flow");
            var card = rig.Card("Sketch the coupon entry flow");
            var flip = rig.Panel.Flip;
            var before = flip.Get(card).VisualTopLeft;
            var below = rig.Card("Write error copy for card declines");
            var belowBefore = flip.Get(below).VisualTopLeft;

            var pitch = rig.Card("Split the settings API").Bounds.Height + 6;
            var pitchBelow = card.Bounds.Height + 6;
            var dx = rig.Panel.Grid.ColumnX(0) - rig.Panel.Grid.ColumnX(1);
            var dy = before.Y - (rig.Card("Add retry to webhook sender").Bounds.Bottom + 6);

            rig.Board.MoveNext(task);
            rig.Board.UpdateLayout();
            var motion = flip.Get(card);
            motion.VisualTopLeft.ShouldBe(before);
            motion.Offset.Value.X.ShouldBe(dx, 0.5);
            motion.Offset.Value.Y.ShouldBe(dy, 0.5);
            dy.ShouldBeLessThan(-pitch);
            card.IsFlying.ShouldBeTrue();
            card.ZIndex.ShouldBe(1);
            card.Classes.Contains(":moving").ShouldBeTrue();
            // The card below closes up from where it was, on the same spring.
            flip.Get(below).VisualTopLeft.ShouldBe(belowBefore);
            flip.Get(below).Offset.Value.Y.ShouldBe(pitchBelow, 0.5);

            clock.Advance(TimeSpan.FromMilliseconds(100));
            var x = motion.Offset.Value.X;
            x.ShouldBeInRange(dx * 0.6, dx * 0.2);
            // A straight line: both axes move by the same fraction.
            (motion.Offset.Value.Y / dy).ShouldBe(x / dx, 0.01);

            clock.Advance(TimeSpan.FromSeconds(1.5));
            motion.Offset.Value.ShouldBe(default);
            card.IsFlying.ShouldBeFalse();
            card.ZIndex.ShouldBe(0);
            flip.Get(below).Offset.Value.Y.ShouldBe(0, 0.01);
        }
        finally
        {
            rig.Window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Second_Move_Mid_Flight_Starts_From_The_Drawn_Position_At_Rest()
    {
        var clock = new ManualFrameClock();
        using var installed = clock.Install();
        var rig = Show(level: ArcMotionLevel.Full);
        try
        {
            var task = rig.Task("Sketch the coupon entry flow");
            var card = rig.Card("Sketch the coupon entry flow");
            var motion = rig.Panel.Flip.Get(card);
            rig.Board.MoveNext(task);
            rig.Board.UpdateLayout();
            clock.Advance(TimeSpan.FromMilliseconds(150));
            var drawn = motion.VisualTopLeft;
            motion.Offset.Velocity.X.ShouldNotBe(0);

            rig.Board.MoveNext(task);
            rig.Board.UpdateLayout();
            motion.VisualTopLeft.X.ShouldBe(drawn.X, 0.01);
            motion.VisualTopLeft.Y.ShouldBe(drawn.Y, 0.01);
            motion.Offset.Velocity.ShouldBe(default);
            card.IsFlying.ShouldBeTrue();
            clock.Advance(TimeSpan.FromSeconds(1.5));
            motion.Offset.Value.ShouldBe(default);
            card.Bounds.X.ShouldBe(rig.Panel.Grid.ColumnX(2) + 6, 0.01);
            card.IsFlying.ShouldBeFalse();
        }
        finally
        {
            rig.Window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Closing_Slot_Follows_Its_Cards_And_The_Board_Keeps_Its_Height_Until_They_Rest()
    {
        var clock = new ManualFrameClock();
        using var installed = clock.Install();
        var rig = Show(level: ArcMotionLevel.Full, configure: b =>
        {
            for (var i = 0; i < 3; i++)
            {
                b.Tasks.Add(new ProjectBoardTask("Extra " + i, "planned") { Area = "Insights", DueText = "No date" });
            }
        });
        try
        {
            var column = rig.Panel.Columns[0];
            var tall = column.TargetSlotHeight;
            var cards = rig.Panel.CardsIn(0);
            tall.ShouldBe(6 + cards.Sum(c => c.Bounds.Height) + (cards.Count - 1) * 6 + 6, 0.5);
            var height = rig.Panel.Bounds.Height;
            var last = rig.Card("Extra 2");
            var lastMotion = rig.Panel.Flip.Get(last);
            var gone = rig.Card("Sketch the coupon entry flow").Bounds.Height + 6;

            rig.Board.MoveNext(rig.Task("Sketch the coupon entry flow"));
            rig.Board.UpdateLayout();
            column.TargetSlotHeight.ShouldBe(tall - gone, 0.5);
            column.DrawnSlotHeight.ShouldBe(tall, 0.5);
            rig.Panel.HeldHeight.ShouldBe(height, 0.5);
            rig.Panel.Bounds.Height.ShouldBe(height, 0.5);

            clock.Advance(TimeSpan.FromMilliseconds(120));
            // The slot's bottom stays under the last card's drawn bottom all the way up.
            var slotBottom = 28 + column.DrawnSlotHeight;
            var cardBottom = lastMotion.VisualTopLeft.Y + last.Bounds.Height;
            slotBottom.ShouldBeGreaterThanOrEqualTo(cardBottom + 6 - 0.5);
            column.DrawnSlotHeight.ShouldBeInRange(tall - gone + 1, tall - 1);

            clock.Advance(TimeSpan.FromSeconds(1.5));
            Pump();
            column.DrawnSlotHeight.ShouldBe(tall - gone, 0.01);
            rig.Panel.HeldHeight.ShouldBe(0);
            rig.Panel.Bounds.Height.ShouldBe(height - gone, 0.5);
        }
        finally
        {
            rig.Window.Close();
        }
    }

    [AvaloniaFact]
    public void Filtered_Out_Cards_Fade_In_Place_While_The_Others_Close_Up()
    {
        var clock = new ManualFrameClock();
        using var installed = clock.Install();
        var rig = Show(level: ArcMotionLevel.Full);
        try
        {
            // Rhea's Sketch goes under Tomas's Retry in In progress, then My work hides Retry: Sketch closes up.
            rig.Board.MoveNext(rig.Task("Sketch the coupon entry flow"));
            rig.Board.UpdateLayout();
            clock.Advance(TimeSpan.FromSeconds(1.5));
            var retry = rig.Card("Add retry to webhook sender");
            var sketch = rig.Card("Sketch the coupon entry flow");
            var retryRect = retry.Bounds;

            rig.Board.Filter = ProjectBoardFilter.Mine;
            rig.Board.UpdateLayout();
            retry.IsLeaving.ShouldBeTrue();
            retry.IsHitTestVisible.ShouldBeFalse();
            retry.Bounds.ShouldBe(retryRect);
            rig.Panel.Children.ShouldContain(retry);
            var sketchMotion = rig.Panel.Flip.Get(sketch);
            sketchMotion.Offset.Value.Y.ShouldBe(retryRect.Height + 6, 0.5);

            clock.Advance(TimeSpan.FromMilliseconds(100));
            var retryMotion = rig.Panel.Flip.Get(retry);
            retryMotion.Opacity.Value.ShouldBeInRange(0.2, 0.9);
            retryMotion.Scale.Value.ShouldBeInRange(0.98, 0.999);
            sketchMotion.Offset.Value.Y.ShouldBeInRange(1, 100);

            clock.Advance(TimeSpan.FromSeconds(1.5));
            Pump();
            rig.Panel.Children.ShouldNotContain(retry);
            rig.Board.CardFor(rig.Task("Add retry to webhook sender")).ShouldBeNull();
            sketchMotion.Offset.Value.Y.ShouldBe(0, 0.01);

            // Coming back: a new card fades and grows in.
            rig.Board.Filter = ProjectBoardFilter.All;
            rig.Board.UpdateLayout();
            var back = rig.Card("Add retry to webhook sender");
            var backMotion = rig.Panel.Flip.Get(back);
            backMotion.Opacity.Value.ShouldBe(0, 0.01);
            backMotion.Scale.Value.ShouldBe(0.98, 0.001);
            clock.Advance(TimeSpan.FromSeconds(1.5));
            backMotion.Opacity.Value.ShouldBe(1, 0.01);
            backMotion.Scale.Value.ShouldBe(1, 0.001);
        }
        finally
        {
            rig.Window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Card_That_Returns_Before_It_Has_Gone_Is_Brought_Back()
    {
        var clock = new ManualFrameClock();
        using var installed = clock.Install();
        var rig = Show(level: ArcMotionLevel.Full);
        try
        {
            var card = rig.Card("Write error copy for card declines");
            rig.Board.Filter = ProjectBoardFilter.Mine;
            rig.Board.UpdateLayout();
            clock.Advance(TimeSpan.FromMilliseconds(80));
            card.IsLeaving.ShouldBeTrue();
            rig.Board.Filter = ProjectBoardFilter.All;
            rig.Board.UpdateLayout();
            card.IsLeaving.ShouldBeFalse();
            rig.Board.CardFor(rig.Task("Write error copy for card declines")).ShouldBe(card);
            clock.Advance(TimeSpan.FromSeconds(1.5));
            Pump();
            rig.Panel.Children.ShouldContain(card);
            rig.Panel.Flip.Get(card).Opacity.Value.ShouldBe(1, 0.01);
        }
        finally
        {
            rig.Window.Close();
        }
    }

    [AvaloniaFact]
    public void An_Added_Card_Fades_And_Grows_In_And_The_Line_Springs_Back()
    {
        var clock = new ManualFrameClock();
        using var installed = clock.Install();
        var rig = Show(level: ArcMotionLevel.Full);
        try
        {
            var line = TestHelpers.Descendant<ProjectsProgressLine>(rig.Board);
            line.ShownFraction.ShouldBe(1.0 / 6, 0.0001);
            var task = rig.Board.AddTask()!;
            rig.Board.UpdateLayout();
            var motion = rig.Panel.Flip.Get(rig.Board.CardFor(task)!);
            motion.Opacity.Value.ShouldBe(0, 0.01);
            motion.Scale.Value.ShouldBe(0.98, 0.001);
            clock.Advance(TimeSpan.FromMilliseconds(100));
            motion.Opacity.Value.ShouldBeInRange(0.1, 0.9);
            line.ShownFraction.ShouldBeInRange(1.0 / 7, 1.0 / 6);
            clock.Advance(TimeSpan.FromSeconds(1.5));
            motion.Opacity.Value.ShouldBe(1, 0.01);
            line.ShownFraction.ShouldBe(1.0 / 7, 0.0001);
        }
        finally
        {
            rig.Window.Close();
        }
    }

    [AvaloniaTheory]
    [InlineData(ArcMotionLevel.Reduced)]
    [InlineData(ArcMotionLevel.Off)]
    public void Reduced_And_Off_Motion_Change_Everything_In_Place(ArcMotionLevel level)
    {
        var clock = new ManualFrameClock();
        using var installed = clock.Install();
        var rig = Show(level: level);
        try
        {
            var card = rig.Card("Sketch the coupon entry flow");
            rig.Board.MoveNext(card.Task);
            rig.Board.UpdateLayout();
            var motion = rig.Panel.Flip.Get(card);
            motion.Offset.Value.ShouldBe(default);
            motion.IsMoving.ShouldBeFalse();
            card.IsFlying.ShouldBeFalse();
            rig.Panel.Columns[0].DrawnSlotHeight.ShouldBe(320);
            rig.Panel.HeldHeight.ShouldBe(0);

            rig.Board.Filter = ProjectBoardFilter.Mine;
            rig.Board.UpdateLayout();
            rig.Panel.LeavingCards.ShouldBeEmpty();
            rig.Panel.Children.OfType<ProjectBoardCard>().Count().ShouldBe(2);

            rig.Board.Filter = ProjectBoardFilter.All;
            var task = rig.Board.AddTask()!;
            rig.Board.UpdateLayout();
            rig.Panel.Flip.Get(rig.Board.CardFor(task)!).Opacity.Value.ShouldBe(1);
            rig.Panel.Children.OfType<ProjectBoardCard>().Count().ShouldBe(7);
        }
        finally
        {
            rig.Window.Close();
        }
    }

    [AvaloniaFact]
    public void AnimatesMoves_False_Changes_In_Place_At_Full_Motion()
    {
        var clock = new ManualFrameClock();
        using var installed = clock.Install();
        var rig = Show(level: ArcMotionLevel.Full, configure: b => b.AnimatesMoves = false);
        try
        {
            var card = rig.Card("Sketch the coupon entry flow");
            rig.Board.MoveNext(card.Task);
            rig.Board.UpdateLayout();
            rig.Panel.Flip.Get(card).Offset.Value.ShouldBe(default);
            rig.Board.Filter = ProjectBoardFilter.Mine;
            rig.Board.UpdateLayout();
            rig.Panel.LeavingCards.ShouldBeEmpty();
        }
        finally
        {
            rig.Window.Close();
        }
    }

    [AvaloniaFact]
    public void Narrow_Boards_Scroll_Column_By_Column_And_Bring_A_Moved_Card_Into_View()
    {
        var rig = Show(width: 390, height: 800);
        try
        {
            var board = rig.Board;
            board.Classes.Contains(":narrow").ShouldBeTrue();
            board.Classes.Contains(":compact").ShouldBeTrue();
            board.Classes.Contains(":scrolling").ShouldBeTrue();
            rig.Panel.Grid.ColumnWidth.ShouldBe(314, 0.01);
            TestHelpers.Descendant<ProjectsAvatarStack>(board).IsVisible.ShouldBeFalse();
            TestHelpers.Descendant<TextBlock>(board, "PART_AddLabel").IsVisible.ShouldBeFalse();
            var add = TestHelpers.Descendant<ProjectBoardButton>(board, ProjectBoard.AddPart);
            add.Bounds.Width.ShouldBe(40, 0.01);
            add.Bounds.Height.ShouldBe(40, 0.01);
            TestHelpers.Descendant<ProjectBoardHeaderPanel>(board).IsWrapped.ShouldBeTrue();
            var card = rig.Card("Sketch the coupon entry flow");
            card.IsNarrow.ShouldBeTrue();
            card.NextButton!.Bounds.Width.ShouldBe(40, 0.01);
            var scroll = TestHelpers.Descendant<ScrollViewer>(board, ProjectBoard.ScrollPart);
            scroll.Offset.X.ShouldBe(0);

            // A press on the right arrow sends the card to In progress, off screen: its column scrolls in.
            card.NextButton.Focus(NavigationMethod.Tab);
            rig.Window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
            Pump();
            scroll.Offset.X.ShouldBe(324, 0.5);
            card.NextButton.IsFocused.ShouldBeTrue();

            // ScrollToStage and the proximity snap.
            board.ScrollToStage("planned", animate: false);
            Pump();
            scroll.Offset.X.ShouldBe(0, 0.5);
            scroll.Offset = new Vector(90, 0);
            Pump();
            board.SnapScroll!.Threshold.ShouldBe(314 / 3.0, 0.01);
            board.SnapScroll.SnapNow();
            Pump();
            scroll.Offset.X.ShouldBe(0, 0.5);
            scroll.Offset = new Vector(160, 0);
            Pump();
            board.SnapScroll.SnapNow();
            Pump();
            scroll.Offset.X.ShouldBe(160, 0.5);
            scroll.Offset = new Vector(560, 0);
            Pump();
            board.SnapScroll.SnapNow();
            Pump();
            scroll.Offset.X.ShouldBe(648, 0.5);
            board.FocusTask(rig.Task("Ship the weekly digest email")).ShouldBeTrue();
            Pump();
            scroll.Offset.X.ShouldBe(scroll.Extent.Width - scroll.Viewport.Width, 0.5);
        }
        finally
        {
            rig.Window.Close();
        }
    }

    [AvaloniaTheory]
    [InlineData(1440, false, false, false)]
    [InlineData(820, false, false, true)]
    [InlineData(700, true, false, true)]
    [InlineData(400, true, true, true)]
    public void Breakpoints_Follow_The_Boards_Own_Width(double width, bool narrow, bool compact, bool scrolling)
    {
        var rig = Show(width: width);
        try
        {
            rig.Board.Classes.Contains(":narrow").ShouldBe(narrow);
            rig.Board.Classes.Contains(":compact").ShouldBe(compact);
            rig.Board.Classes.Contains(":scrolling").ShouldBe(scrolling);
            var filter = rig.Board.FilterBar!;
            filter.AllButton!.Bounds.Height.ShouldBe(narrow ? 36 : 28, 0.01);
            TestHelpers.Descendant<ProjectsAvatarStack>(rig.Board).IsVisible.ShouldBe(!narrow);
        }
        finally
        {
            rig.Window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Writable_Source_Is_Reordered_And_External_Changes_Move_The_Cards()
    {
        var (rhea, tomas, ines) = ProjectBoardDemoData.CreateTeam();
        var source = new System.Collections.ObjectModel.ObservableCollection<ProjectBoardTask>(ProjectBoardDemoData.CreateTasks(rhea, tomas, ines));
        var rig = Show(configure: b => b.TasksSource = source);
        try
        {
            var board = rig.Board;
            var task = source[0];
            board.MoveNext(task).ShouldBeTrue();
            source[^1].ShouldBe(task);
            board.Tasks.Count.ShouldBe(6); // Tasks is ignored while TasksSource is set

            // The app changes a stage itself: the card follows (in collection order) without an announcement.
            var digest = source.First(t => t.Title == "Ship the weekly digest email");
            board.Announcer!.History.Clear();
            digest.StageKey = "planned";
            Pump();
            Titles(board, 0).ShouldBe(["Write error copy for card declines", "Ship the weekly digest email"]);
            board.Announcer.History.ShouldBeEmpty();
            board.DoneCount.ShouldBe(0);
            source.RemoveAt(0);
            Pump();
            board.TaskCount.ShouldBe(5);
            source.Add(new ProjectBoardTask("Late arrival", "review"));
            Pump();
            Titles(board, 2)[^1].ShouldBe("Late arrival");

            // A read-only source only has its stages changed.
            var fixedSource = ProjectBoardDemoData.CreateTasks(rhea, tomas, ines).ToArray();
            board.TasksSource = fixedSource;
            Pump();
            board.MoveNext(fixedSource[0]).ShouldBeTrue();
            fixedSource[0].StageKey.ShouldBe("progress");
            Titles(board, 1).ShouldBe(["Sketch the coupon entry flow", "Split the settings API", "Add retry to webhook sender"]);
            board.AddTask().ShouldBeNull();
        }
        finally
        {
            rig.Window.Close();
        }
    }

    [AvaloniaFact]
    public void Stages_Decide_The_Finished_Stage_And_The_Empty_Texts()
    {
        var rig = Show(configure: b =>
        {
            b.Stages.Clear();
            b.Stages.AddRange([new ProjectBoardStage("planned", "Planned"), new ProjectBoardStage("progress", "In progress"),
                new ProjectBoardStage("review", "Review") { EmptyText = "Nothing to review" }, new ProjectBoardStage("done", "Done")]);
        });
        try
        {
            // No stage is marked: the last one finishes tasks.
            rig.Board.DoneCount.ShouldBe(1);
            rig.Board.MoveNext(rig.Task("Contrast audit for receipts"));
            rig.Board.Panel!.Columns[2].EmptyText.ShouldBe("Nothing to review");
            rig.Board.Stages[1].IsDone = true;
            Pump();
            rig.Board.DoneCount.ShouldBe(2);
            rig.Card("Split the settings API").IsDone.ShouldBeTrue();
            rig.Card("Ship the weekly digest email").IsDone.ShouldBeFalse();

            // A task with an unknown stage shows in the first column.
            rig.Board.Tasks.Add(new ProjectBoardTask("Somewhere", "unknown"));
            Pump();
            Titles(rig.Board, 0)[^1].ShouldBe("Somewhere");
            rig.Board.Stages.Add(new ProjectBoardStage("archive", "Archive"));
            Pump();
            rig.Board.Panel.Columns.Count.ShouldBe(5);
            rig.Card("Ship the weekly digest email").CanMoveNext.ShouldBeTrue();
        }
        finally
        {
            rig.Window.Close();
        }
    }

    [AvaloniaFact]
    public void The_Gallery_Demos_Build_In_Light_And_Dark()
    {
        Control[] demos =
        [
            new ProjectBoardPreviewDemo(), new ProjectBoardFramedDemo(), new ProjectBoardPersistedDemo(),
            new ProjectBoardCustomStagesDemo(), new ProjectBoardNarrowDemo(),
        ];
        foreach (var demo in demos)
        {
            var window = TestHelpers.Show(demo);
            window.Width = 1100;
            window.Height = 900;
            Pump();
            var board = demo.GetVisualDescendants().OfType<ProjectBoard>().First();
            board.Panel!.Children.OfType<ProjectBoardCard>().Count().ShouldBeGreaterThan(0);
            window.Close();
        }
        var custom = new ProjectBoardCustomStagesDemo();
        var w = TestHelpers.Show(custom);
        try
        {
            Pump();
            var board = custom.GetVisualDescendants().OfType<ProjectBoard>().First();
            board.Panel!.Columns.Count.ShouldBe(5);
            board.DoneCount.ShouldBe(1);
            board.Panel.Columns[3].EmptyText.ShouldBe("Nothing waiting for QA");
        }
        finally
        {
            w.Close();
        }
    }

    private sealed class RelayCommand(Action<object?> execute) : System.Windows.Input.ICommand
    {
        public event EventHandler? CanExecuteChanged
        {
            add { }
            remove { }
        }

        public bool CanExecute(object? parameter) => true;

        public void Execute(object? parameter) => execute(parameter);
    }
}
