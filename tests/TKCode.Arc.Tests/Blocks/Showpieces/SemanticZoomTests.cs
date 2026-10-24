using AtomUI.Theme.Algorithms;
using AtomUI.Theme.Configuration;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using TKCode.Arc.Gallery.Demos;
using TKCode.Arc.Motion;

namespace TKCode.Arc.Tests.Blocks.Showpieces;

/// <summary>
/// <see cref="SemanticZoom"/>: defaults, the scope derived from the camera, flights, clicks and hit rules, the keyboard
/// model, checklist ticks and rollups, the tour, the hover card, container widths, motion levels, idle frames, the panel,
/// announcements and theme colors.
/// </summary>
public class SemanticZoomTests
{
    private static void Settle(int ticks = 3)
    {
        for (var i = 0; i < ticks; i++)
        {
            Dispatcher.UIThread.RunJobs();
            AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        }
        Dispatcher.UIThread.RunJobs();
    }

    private static (SemanticZoom Map, Window Window) Show(double width = 665, ArcMotionLevel level = ArcMotionLevel.Off,
                                                          SemanticZoomOrganization? organization = null)
    {
        var map = new SemanticZoom { Header = "Network map", Organization = organization ?? SemanticZoomDemoData.Create() };
        var window = TestHelpers.Show(map, level);
        window.Width = width;
        window.Height = 1100;
        Settle();
        return (map, window);
    }

    private static SemanticZoomStage Stage(SemanticZoom map) => map.Stage.ShouldNotBeNull();

    private static Point OnWindow(Window window, Visual visual, Point local) => visual.TranslatePoint(local, window)!.Value;

    private static void Click(Window window, Point point)
    {
        window.MouseMove(point);
        window.MouseDown(point, MouseButton.Left);
        window.MouseUp(point, MouseButton.Left);
        Settle();
    }

    private static void Key(Window window, PhysicalKey key)
    {
        window.KeyPressQwerty(key, RawInputModifiers.None);
        window.KeyReleaseQwerty(key, RawInputModifiers.None);
        Settle();
    }

    [AvaloniaFact]
    public void Defaults_Match_The_Measured_Preview()
    {
        var map = new SemanticZoom();
        map.MaxZoom.ShouldBe(53.04);
        map.Zoom.ShouldBe(1);
        map.ZoomText.ShouldBe("1.0×");
        map.TourDwell.ShouldBe(TimeSpan.FromSeconds(1.5));
        map.DetailPanelPlacement.ShouldBe(SemanticZoomPanelPlacement.Auto);
        map.StageHeight.ShouldBeNull();
        map.IsBreadcrumbVisible.ShouldBeTrue();
        map.IsTourButtonVisible.ShouldBeTrue();
        map.IsMinimapVisible.ShouldBeTrue();
        map.IsZoomControlsVisible.ShouldBeTrue();
        map.IsHintVisible.ShouldBeTrue();
        map.IsTourPlaying.ShouldBeFalse();
        map.TourStops.ShouldBeNull();
        map.Scope.ShouldBeNull();
        // Every control owns its texts (changing one never changes another's).
        map.Texts.ShouldNotBeSameAs(new SemanticZoom().Texts);
        map.Texts.ShouldNotBeSameAs(SemanticZoomTexts.Default);
        map.FlyToCommand.CanExecute(null).ShouldBeFalse();
        map.ResetViewCommand.CanExecute(null).ShouldBeTrue();
    }

    [AvaloniaFact]
    public void Loads_The_Organization_At_Fit()
    {
        var (map, window) = Show();
        try
        {
            var org = map.Organization!;
            map.Scope.ShouldBe(org);
            map.ScopePath.ShouldBe([org]);
            map.Classes.Contains(SemanticZoom.FitPseudoClass).ShouldBeTrue();
            var stage = Stage(map);
            stage.Bounds.Height.ShouldBe(400, 0.5);
            stage.Layout.TeamCount.ShouldBe(5);
            stage.Layout.ProjectCount.ShouldBe(22);
            map.MapLayout.TaskCount.ShouldBe(org.TaskCount);
            org.TaskCount.ShouldBeInRange(148, 156);
            TestHelpers.Descendant<TextBlock>(map, SemanticZoom.SubtitlePart).Text
                .ShouldBe($"5 teams, 22 projects, {org.TaskCount} tasks. Zoom in to see more.");
            map.Table.ShouldNotBeNull().Rows.Count.ShouldBe(22);
            map.Crumbs.ShouldNotBeNull().Items.Count.ShouldBe(1);
            map.DetailPanel.ShouldNotBeNull().Scope.ShouldBe(org);
            // At rest nothing asks for frames.
            stage.IsLoopRunning.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Flying_In_And_Out_Follows_The_Scope_Chain()
    {
        var (map, window) = Show();
        try
        {
            var org = map.Organization!;
            var team = org.Teams[1];
            var project = team.Projects[1];
            var task = project.Tasks[0];
            var changes = new List<SemanticZoomScopeChangedEventArgs>();
            map.ScopeChanged += (_, e) => changes.Add(e);

            map.FlyTo(team);
            Settle();
            map.Scope.ShouldBe(team);
            map.ScopePath.ShouldBe([org, team]);
            map.Zoom.ShouldBeInRange(1.4, 3);
            map.Classes.Contains(SemanticZoom.FitPseudoClass).ShouldBeFalse();
            map.Crumbs!.Items.Count.ShouldBe(2);
            changes.Last().NewScope.ShouldBe(team);
            changes.Last().Path.ShouldBe([org, team]);

            // Setting Scope flies there.
            map.Scope = task;
            Settle();
            map.Scope.ShouldBe(task);
            map.ScopePath.ShouldBe([org, team, project, task]);
            map.Zoom.ShouldBeGreaterThan(SemanticZoomLod.TaskZoom);
            map.DetailPanel!.Scope.ShouldBe(task);

            map.FlyOut();
            Settle();
            map.Scope.ShouldBe(project);
            map.FlyOut();
            Settle();
            map.Scope.ShouldBe(team);
            map.FlyOut();
            Settle();
            map.Scope.ShouldBe(org);
            map.Zoom.ShouldBe(1, 1e-6);
            map.Crumbs.Items.Count.ShouldBe(1);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Clicks_Fly_Into_Children_And_Siblings_But_Not_The_Scope()
    {
        var (map, window) = Show();
        try
        {
            var org = map.Organization!;
            var stage = Stage(map);
            var invoked = new List<SemanticZoomNode>();
            map.NodeInvoked += (_, e) => invoked.Add(e.Node);
            var layout = stage.Layout;

            // Company: anywhere in a team panel hits the team.
            var team0 = stage.BoxOnScreen(0);
            Click(window, OnWindow(window, stage, team0.TopLeft + new Point(10, 10)));
            invoked.ShouldBe([org.Teams[0]]);
            map.Scope.ShouldBe(org.Teams[0]);

            // The scope itself does nothing; its project flies in.
            Click(window, OnWindow(window, stage, stage.BoxOnScreen(0).TopLeft + new Point(4, 4)));
            invoked.Count.ShouldBe(1);
            var project = org.Teams[0].Projects[2];
            Click(window, OnWindow(window, stage, stage.BoxOnScreen(layout.IndexOf(project)).Center));
            map.Scope.ShouldBe(project);
            invoked.Last().ShouldBe(project);

            // A handled NodeInvoked keeps the camera where it is.
            map.NodeInvoked += (_, e) => e.Handled = true;
            var task = project.Tasks[0];
            Click(window, OnWindow(window, stage, stage.BoxOnScreen(layout.IndexOf(task)).Center));
            invoked.Last().ShouldBe(task);
            map.Scope.ShouldBe(project);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Arrow_Keys_Move_A_Cursor_Enter_Flies_In_Escape_And_Backspace_Fly_Out()
    {
        var (map, window) = Show();
        try
        {
            var org = map.Organization!;
            var stage = Stage(map);
            stage.Focus(NavigationMethod.Tab);
            Settle();

            // First press: the first child; then the nearest team that way.
            Key(window, PhysicalKey.ArrowRight);
            map.CursorNode.ShouldBe(org.Teams[0]);
            map.Card!.IsOpen.ShouldBeTrue();
            map.CardContent!.Node.ShouldBe(org.Teams[0]);
            Key(window, PhysicalKey.ArrowRight);
            map.CursorNode.ShouldBe(org.Teams[1]);
            Key(window, PhysicalKey.ArrowRight);
            map.CursorNode.ShouldBe(org.Teams[2]);
            Key(window, PhysicalKey.ArrowRight);
            map.CursorNode.ShouldBe(org.Teams[2]);
            Key(window, PhysicalKey.ArrowDown);
            map.CursorNode.ShouldBe(org.Teams[4]);
            Key(window, PhysicalKey.ArrowLeft);
            map.CursorNode.ShouldBe(org.Teams[3]);
            Key(window, PhysicalKey.ArrowUp);
            map.CursorNode.ShouldBe(org.Teams[0]);
            map.LastAnnouncement.ShouldNotBeNull().ShouldEndWith("Press Enter to zoom in.");

            Key(window, PhysicalKey.Enter);
            map.Scope.ShouldBe(org.Teams[0]);
            map.CursorNode.ShouldBeNull();
            map.LastAnnouncement.ShouldNotBeNull().ShouldStartWith("Now showing Operations team");

            // Escape clears the cursor first, then flies out; at fit it does nothing.
            Key(window, PhysicalKey.ArrowDown);
            map.CursorNode.ShouldBe(org.Teams[0].Projects[0]);
            Key(window, PhysicalKey.Escape);
            map.CursorNode.ShouldBeNull();
            map.Scope.ShouldBe(org.Teams[0]);
            Key(window, PhysicalKey.Escape);
            map.Scope.ShouldBe(org);

            // Backspace flies out at once (cursor or not).
            map.FlyTo(org.Teams[0].Projects[0]);
            Settle();
            Key(window, PhysicalKey.Backspace);
            map.Scope.ShouldBe(org.Teams[0]);

            // + / − zoom by 1.5 about the center; 0 fits.
            var zoom = map.Zoom;
            Key(window, PhysicalKey.Equal);
            map.Zoom.ShouldBe(zoom * 1.5, 1e-6);
            Key(window, PhysicalKey.Minus);
            map.Zoom.ShouldBe(zoom, 1e-6);
            Key(window, PhysicalKey.Digit0);
            map.Zoom.ShouldBe(1, 1e-9);
            map.Scope.ShouldBe(org);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Checklist_Items_Toggle_From_The_Keyboard_And_Update_Every_Total()
    {
        var (map, window) = Show();
        try
        {
            var org = map.Organization!;
            var stage = Stage(map);
            var task = org.AllTasks.First(t => t.Status == SemanticZoomTaskStatus.InReview);
            var project = task.Project!;
            map.FlyTo(task);
            Settle();
            // Deep enough for the rows to fit on the card.
            stage.View.JumpTo(stage.View.Center + new Vector(0, task.Checklist.Count * 0.6), 53);
            Settle();
            map.Scope.ShouldBe(task);
            stage.ChecklistRowsShown().ShouldBe(task.Checklist.Count);
            var toggles = new List<SemanticZoomChecklistEventArgs>();
            map.ChecklistItemToggled += (_, e) => toggles.Add(e);
            var openBefore = project.OpenTasks;
            var progressBefore = project.Progress;
            stage.Focus(NavigationMethod.Tab);

            // The cursor walks the rows; the last one is the open item of a task in review.
            Key(window, PhysicalKey.ArrowDown);
            map.CursorItem.ShouldBe(task.Checklist[0]);
            for (var i = 1; i < task.Checklist.Count; i++)
            {
                Key(window, PhysicalKey.ArrowDown);
            }
            var last = task.Checklist[^1];
            map.CursorItem.ShouldBe(last);
            last.IsDone.ShouldBeFalse();
            Key(window, PhysicalKey.Enter);
            last.IsDone.ShouldBeTrue();
            task.Status.ShouldBe(SemanticZoomTaskStatus.Done);
            project.OpenTasks.ShouldBe(openBefore - 1);
            project.Progress.ShouldBeGreaterThan(progressBefore);
            toggles.Single().IsDone.ShouldBeTrue();
            toggles.Single().Task.ShouldBe(task);
            map.LastAnnouncement.ShouldNotBeNull().ShouldContain("ticked");
            map.Labels.Line(stage.Layout.IndexOf(task)).ShouldBe($"{task.Checklist.Count}/{task.Checklist.Count} · {SemanticZoomFormatter.Default.FormatDate(task.Due!.Value)}");

            // Unticking a done task puts it back in review.
            Key(window, PhysicalKey.Space);
            last.IsDone.ShouldBeFalse();
            task.Status.ShouldBe(SemanticZoomTaskStatus.InReview);
            map.LastAnnouncement.ShouldNotBeNull().ShouldContain("now in review");

            // A click on a row toggles it too.
            var row = stage.ChecklistItemRect(0);
            var before = task.Checklist[0].IsDone;
            // (right of the minimap, which sits in the bottom-left corner)
            Click(window, OnWindow(window, stage, new Point(Math.Max(row.X + 8, 260), row.Center.Y)));
            task.Checklist[0].IsDone.ShouldBe(!before);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void The_Tour_Visits_Every_Stop_And_Ends_At_The_Whole_Map()
    {
        var (map, window) = Show();
        try
        {
            var org = map.Organization!;
            var started = 0;
            var stopped = new List<bool>();
            map.TourStarted += (_, _) => started++;
            map.TourStopped += (_, e) => stopped.Add(e.Completed);
            var visited = new List<SemanticZoomNode?>();
            map.ScopeChanged += (_, e) => visited.Add(e.NewScope);

            map.IsTourPlaying = true;
            started.ShouldBe(1);
            map.Classes.Contains(SemanticZoom.TouringPseudoClass).ShouldBeTrue();
            map.Tour.IsReduced.ShouldBeTrue();
            map.Scope.ShouldBe(org.Teams[0]);
            for (var i = 0; i < 60 * 25 && map.IsTourPlaying; i++)
            {
                map.AdvanceTour(1 / 60.0);
            }
            map.IsTourPlaying.ShouldBeFalse();
            stopped.ShouldBe([true]);
            map.Scope.ShouldBe(org);
            visited.ShouldContain(org.Teams[0].Projects[1].Tasks[1]);
            visited.ShouldContain(org.Teams[1].Projects[0]);
            map.Classes.Contains(SemanticZoom.TouringPseudoClass).ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Manual_Input_And_The_Button_Stop_The_Tour_Where_It_Is()
    {
        var (map, window) = Show(level: ArcMotionLevel.Full);
        try
        {
            var stopped = new List<bool>();
            map.TourStopped += (_, e) => stopped.Add(e.Completed);
            map.StartTour();
            map.IsTourPlaying.ShouldBeTrue();
            Stage(map).View.IsFlying.ShouldBeTrue();
            // A wheel notch takes the camera over.
            var stage = Stage(map);
            window.MouseWheel(OnWindow(window, stage, new Point(300, 200)), new Vector(0, 1));
            Settle();
            map.IsTourPlaying.ShouldBeFalse();
            stopped.ShouldBe([false]);

            // The button toggles; custom stops are honoured.
            var org = map.Organization!;
            map.TourStops = [org.Teams[3], org];
            map.ToggleTourCommand.Execute(null);
            map.IsTourPlaying.ShouldBeTrue();
            map.ToggleTour();
            map.IsTourPlaying.ShouldBeFalse();
            stopped.ShouldBe([false, false]);
            stage.View.IsFlying.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Hovering_A_Node_Opens_Its_Card_And_Leaving_Closes_It()
    {
        var (map, window) = Show();
        try
        {
            var org = map.Organization!;
            var stage = Stage(map);
            var rect = stage.BoxOnScreen(1);
            window.MouseMove(OnWindow(window, stage, rect.Center));
            Settle();
            map.Card!.IsOpen.ShouldBeTrue();
            map.CardContent!.Node.ShouldBe(org.Teams[1]);
            var lines = map.CardContent.Lines;
            lines.ShouldContain(org.Teams[1].Title);
            lines.ShouldContain("Lead");
            lines.ShouldContain(org.Teams[1].OpenTasks.ToString());
            stage.Scene.Hovered.ShouldBe(1);
            stage.Scene.Hover(1).ShouldBe(1);
            // Empty space between teams: no card.
            window.MouseMove(OnWindow(window, stage, new Point(3, 3)));
            Settle();
            map.Card.IsOpen.ShouldBeFalse();
            stage.Scene.Hovered.ShouldBe(-1);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Container_Width_Picks_The_Panel_Placement_And_The_Compact_Layout()
    {
        var (map, window) = Show(width: 1312);
        try
        {
            var stage = Stage(map);
            map.Classes.Contains(SemanticZoom.SidePanelPseudoClass).ShouldBeTrue();
            stage.Bounds.Width.ShouldBe(1310 - 264, 1);
            stage.Bounds.Height.ShouldBe(580, 0.5);
            map.DetailPanel!.Layout.ShouldBe(SemanticZoomPanelLayout.Stack);
            // The team line already shows at the wide fit (k ≈ 0.67).
            stage.Scene.Lod(SemanticZoomLayer.TeamLine).ShouldBeGreaterThan(0.5);

            window.Width = 900;
            Settle();
            map.Classes.Contains(SemanticZoom.SidePanelPseudoClass).ShouldBeFalse();
            stage.Bounds.Height.ShouldBe(0.48 * 898, 1);
            map.DetailPanel.Layout.ShouldBe(SemanticZoomPanelLayout.Grid);

            window.Width = 390;
            Settle();
            map.Classes.Contains(SemanticZoom.CompactPseudoClass).ShouldBeTrue();
            stage.Bounds.Height.ShouldBe(420, 0.5);
            stage.IsCompact.ShouldBeTrue();
            stage.View.Insets.ShouldBe(SemanticZoomStage.CompactInsets);
            stage.View.IsAtFit.ShouldBeTrue();
            map.DetailPanel.Layout.ShouldBe(SemanticZoomPanelLayout.Stack);
            TestHelpers.Descendant<SemanticZoomTourButton>(map).Bounds.Width.ShouldBe(32, 0.5);

            map.StageHeight = 300;
            map.DetailPanelPlacement = SemanticZoomPanelPlacement.None;
            Settle();
            stage.Bounds.Height.ShouldBe(300, 0.5);
            ((Control)map.DetailPanel.GetVisualParent()!).IsVisible.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Reduced_Motion_Lands_Flights_At_Once_And_Idle_Requests_No_Frames()
    {
        var (map, window) = Show(level: ArcMotionLevel.Reduced);
        try
        {
            var org = map.Organization!;
            var stage = Stage(map);
            map.FlyTo(org.Teams[2].Projects[0]);
            stage.View.IsMoving.ShouldBeFalse();
            map.Scope.ShouldBe(org.Teams[2].Projects[0]);
            stage.Scene.Animates.ShouldBeFalse();
            stage.Scene.Dim(0).ShouldBe(SemanticZoomLod.TeamDim);
            Settle();
            stage.IsLoopRunning.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Full_Motion_Flies_On_The_Van_Wijk_Path_And_Then_Rests()
    {
        var (map, window) = Show(level: ArcMotionLevel.Full);
        try
        {
            var org = map.Organization!;
            var stage = Stage(map);
            map.FlyTo(org.Teams[0]);
            stage.View.IsFlying.ShouldBeTrue();
            for (var i = 0; i < 120 && stage.View.IsMoving; i++)
            {
                stage.View.Advance(1 / 60.0);
            }
            stage.View.IsMoving.ShouldBeFalse();
            map.Scope.ShouldBe(org.Teams[0]);
            // Settled: the scene's smoothing finishes and nothing moves any more.
            for (var i = 0; i < 120; i++)
            {
                stage.Scene.Step(1 / 60.0);
            }
            stage.Scene.Step(1 / 60.0).ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void The_Panel_Shows_The_Numbers_Of_The_Scope()
    {
        var (map, window) = Show();
        try
        {
            var org = map.Organization!;
            var panel = map.DetailPanel!;
            TestHelpers.Descendant<ShowpieceNumber>(panel, SemanticZoomPanel.OpenPart).Value.ShouldBe(org.OpenTasks);
            TestHelpers.Descendant<ShowpieceNumber>(panel, SemanticZoomPanel.BlockedPart).Value.ShouldBe(org.BlockedTasks);
            TestHelpers.Descendant<ShowpieceNumber>(panel, SemanticZoomPanel.PeopleCountPart).Value.ShouldBe(org.People.Count);
            panel.Faces.Count.ShouldBe(SemanticZoomPanel.MaxFaces + 1);
            panel.Faces[^1].Label.ShouldBe($"+{org.People.Count - SemanticZoomPanel.MaxFaces}");
            panel.NoteText.ShouldBe($"{org.ProjectCount} projects, {org.NeedAttention} needing attention");

            var task = org.AllTasks.First(t => t.Status == SemanticZoomTaskStatus.Done);
            map.FlyTo(task);
            Settle();
            TestHelpers.Descendant<TextBlock>(panel, "PART_ProgressLabel").Text.ShouldBe("Checklist complete");
            TestHelpers.Descendant<TextBlock>(panel, SemanticZoomPanel.DueMonthPart).Text.ShouldBe("None");
            TestHelpers.Descendant<ShowpieceNumber>(panel, SemanticZoomPanel.OpenPart).Value.ShouldBe(0);
            panel.Faces.Count.ShouldBe(1);
            panel.NoteText.ShouldBe($"Owner: {task.Assignee!.Name}");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void The_Panel_Works_On_Its_Own()
    {
        var org = SemanticZoomEngineTests.Organization([2], tasks: 4);
        var panel = new SemanticZoomPanel { Scope = org.Teams[0].Projects[0], Layout = SemanticZoomPanelLayout.Stack };
        var window = TestHelpers.Show(panel);
        try
        {
            Settle();
            panel.Classes.Contains(SemanticZoomPanel.StackPseudoClass).ShouldBeTrue();
            TestHelpers.Descendant<ShowpieceNumber>(panel, SemanticZoomPanel.OpenPart).Value.ShouldBe(4);
            org.Teams[0].Projects[0].Tasks[0].Status = SemanticZoomTaskStatus.Done;
            Settle();
            TestHelpers.Descendant<ShowpieceNumber>(panel, SemanticZoomPanel.OpenPart).Value.ShouldBe(3);
            TestHelpers.Descendant<TextBlock>(panel, SemanticZoomPanel.DueMonthPart).Text.ShouldBe("Sep");
            TestHelpers.Descendant<ShowpieceNumber>(panel, SemanticZoomPanel.DueDayPart).Value.ShouldBe(11);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Data_Changes_Relabel_And_Relayout_The_Map()
    {
        var (map, window) = Show();
        try
        {
            var org = map.Organization!;
            var stage = Stage(map);
            var project = org.Teams[0].Projects[0];
            var box = stage.Layout.IndexOf(project);
            project.Title = "Renamed project";
            project.Health = SemanticZoomHealth.OffTrack;
            map.Labels.Line(box).ShouldStartWith("Off track");
            map.Table!.Rows.ShouldContain(r => r.StartsWith("Renamed project"));

            var team = new SemanticZoomTeam { Title = "Security" };
            team.Projects.Add(new SemanticZoomProject { Title = "Camera upgrade" });
            org.Teams.Add(team);
            Settle();
            stage.Layout.TeamCount.ShouldBe(6);
            map.Table.Rows.Count.ShouldBe(23);

            map.Organization = null;
            Settle();
            stage.Layout.IsEmpty.ShouldBeTrue();
            map.Scope.ShouldBeNull();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Screen_Readers_Get_A_Summary_A_Table_And_Announcements()
    {
        var (map, window) = Show();
        try
        {
            var org = map.Organization!;
            var stage = Stage(map);
            var peer = Avalonia.Automation.Peers.ControlAutomationPeer.CreatePeerForElement(stage);
            peer.GetName().ShouldStartWith($"Map of {org.Title}: 5 teams, 22 projects");
            peer.GetLocalizedControlType().ShouldBe("zoomable map");
            Avalonia.Automation.AutomationProperties.GetHelpText(stage).ShouldNotBeNullOrEmpty();
            map.Table!.Rows[0].ShouldStartWith(org.Teams[0].Projects[0].Title + ", Operations team");
            map.FlyTo(org.Teams[1].Projects[0]);
            Settle();
            map.LastAnnouncement.ShouldNotBeNull().ShouldStartWith($"Now showing {org.Teams[1].Projects[0].Title}:");
            map.Describe(org.Teams[1]).ShouldContain("Fleet team");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public async Task Theme_Changes_Recolor_The_Map()
    {
        var (map, window) = Show();
        try
        {
            var stage = Stage(map);
            var light = stage.Palette.Surface;
            await TestHelpers.ApplyAsync(new ThemeConfigBuilder().WithAlgorithms(ThemeAlgorithm.Dark).Build());
            Settle();
            stage.Palette.Surface.ShouldNotBe(light);
            using var bitmap = new Avalonia.Media.Imaging.RenderTargetBitmap(new PixelSize(64, 64));
            using (var context = bitmap.CreateDrawingContext())
            {
                stage.Render(context);
            }
        }
        finally
        {
            window.Close();
            await TestHelpers.ApplyAsync(null);
        }
    }

    [AvaloniaFact]
    public void A_Steady_Frame_Draws_Every_Label_From_The_Stage_Text_Cache()
    {
        var (map, window) = Show();
        try
        {
            var stage = Stage(map);
            using var bitmap = new Avalonia.Media.Imaging.RenderTargetBitmap(new PixelSize(700, 500));
            using var context = bitmap.CreateDrawingContext();
            stage.Render(context);
            stage.Render(context);
            // The map's labels are laid out once by the stage's shared cache (no private cache any more) …
            var misses = stage.TextCacheMisses;
            misses.ShouldBeGreaterThan(10);
            var before = GC.GetAllocatedBytesForCurrentThread();
            for (var i = 0; i < 10; i++)
            {
                stage.Render(context);
            }
            // … and drawn again without laying any out or allocating per label.
            stage.TextCacheMisses.ShouldBe(misses);
            ((GC.GetAllocatedBytesForCurrentThread() - before) / 10.0).ShouldBeLessThan(16 * 1024);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Header_Template_Is_Never_Built_With_Null()
    {
        var probe = new NullTemplateProbe<HeaderModel>();
        var map = new SemanticZoom { Organization = SemanticZoomEngineTests.Organization([1], tasks: 4) };
        map.DataTemplates.Add(probe.Template);
        map.Header = new HeaderModel();
        var window = TestHelpers.Show(map);
        try
        {
            Settle();
            map.Header = null;
            Settle();
            map.Header = new HeaderModel();
            Settle();
            probe.Built.Count.ShouldBeGreaterThan(0);
            probe.NullBuilds.ShouldBe(0);
        }
        finally
        {
            window.Close();
        }
    }

    private sealed class HeaderModel
    {
    }

    [AvaloniaFact]
    public void Scene_Steps_And_Hit_Tests_Allocate_Nothing()
    {
        var (map, window) = Show(level: ArcMotionLevel.Full);
        try
        {
            var stage = Stage(map);
            var scene = stage.Scene;
            scene.SetScope(new SemanticZoomScope(0, stage.Layout.FirstProject, -1));
            scene.SetHovered(3);
            for (var i = 0; i < 5; i++)
            {
                scene.Step(1 / 60.0);
                scene.HitTest(new Point(100, 100));
            }
            var before = GC.GetAllocatedBytesForCurrentThread();
            for (var i = 0; i < 200; i++)
            {
                scene.Step(1 / 60.0);
                scene.HitTest(new Point(100 + i, 100));
                scene.ScopeAt(new Point(100 + i, 100), 3);
            }
            (GC.GetAllocatedBytesForCurrentThread() - before).ShouldBe(0);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Map_And_Its_Panel_Are_Collected_While_The_Organization_Lives_On()
    {
        var org = SemanticZoomDemoData.Create();
        var map = MapOver(org);
        var panel = PanelOver(org.Teams[0].Projects[0]);
        for (var i = 0; i < 4 && (map.IsAlive || panel.IsAlive); i++)
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            Settle();
        }
        map.IsAlive.ShouldBeFalse();
        panel.IsAlive.ShouldBeFalse();
        // The organization goes on without them.
        org.Teams[0].Projects[0].Title = "Renamed later";
        org.Teams.Add(new SemanticZoomTeam { Title = "Later" });
        Settle();
    }

    [AvaloniaFact]
    public void A_Map_Put_Back_Shows_What_Changed_While_It_Was_Away()
    {
        var map = new SemanticZoom { Header = "Network map", Organization = SemanticZoomDemoData.Create() };
        var host = new Border { Child = map };
        var window = TestHelpers.Show(host);
        window.Width = 665;
        window.Height = 1100;
        Settle();
        try
        {
            var org = map.Organization!;
            host.Child = null;
            Settle();
            org.Teams[0].Projects[0].Title = "Renamed while away";
            var team = new SemanticZoomTeam { Title = "Security" };
            team.Projects.Add(new SemanticZoomProject { Title = "Camera upgrade" });
            org.Teams.Add(team);
            host.Child = map;
            Settle();
            Stage(map).Layout.TeamCount.ShouldBe(6);
            map.Table!.Rows.ShouldContain(r => r.StartsWith("Renamed while away"));
            // Back in the window, it listens again.
            org.Teams[1].Projects[0].Title = "Renamed after";
            map.Table!.Rows.ShouldContain(r => r.StartsWith("Renamed after"));
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void The_Panel_Put_Back_Shows_What_Changed_While_It_Was_Away()
    {
        var org = SemanticZoomEngineTests.Organization([2], tasks: 4);
        var panel = new SemanticZoomPanel { Scope = org.Teams[0].Projects[0], Layout = SemanticZoomPanelLayout.Stack };
        var host = new Border { Child = panel };
        var window = TestHelpers.Show(host);
        try
        {
            Settle();
            TestHelpers.Descendant<ShowpieceNumber>(panel, SemanticZoomPanel.OpenPart).Value.ShouldBe(4);
            host.Child = null;
            Settle();
            org.Teams[0].Projects[0].Tasks[0].Status = SemanticZoomTaskStatus.Done;
            host.Child = panel;
            Settle();
            TestHelpers.Descendant<ShowpieceNumber>(panel, SemanticZoomPanel.OpenPart).Value.ShouldBe(3);
            org.Teams[0].Projects[0].Tasks[1].Status = SemanticZoomTaskStatus.Done;
            Settle();
            TestHelpers.Descendant<ShowpieceNumber>(panel, SemanticZoomPanel.OpenPart).Value.ShouldBe(2);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Baked_Avatars_Are_Freed_When_The_Map_Leaves_The_Window_Or_The_Theme_Changes()
    {
        var org = SemanticZoomDemoData.Create();
        // One bitmap the host owns behind every avatar: only the map's round copies of it may be freed.
        var source = new Avalonia.Media.Imaging.RenderTargetBitmap(new PixelSize(16, 16));
        foreach (var person in org.Teams.SelectMany(t => t.People))
        {
            person.Avatar = source;
        }
        var map = new SemanticZoom { Header = "Network map", Organization = org };
        var host = new Border { Child = map };
        var window = TestHelpers.Show(host);
        window.Width = 665;
        window.Height = 1100;
        Settle();
        try
        {
            var stage = Stage(map);
            map.FlyTo(org.Teams[0]);
            Settle(8);
            var baked = stage.BakedAvatars.OfType<Avalonia.Media.Imaging.RenderTargetBitmap>().ToList();
            baked.ShouldNotBeEmpty();
            baked.ShouldNotContain(source);

            host.Child = null;
            Settle();
            stage.BakedAvatars.ShouldBeEmpty();
            foreach (var bitmap in baked)
            {
                Should.Throw<ObjectDisposedException>(() => bitmap.CreateDrawingContext());
            }
            Should.NotThrow(() => source.CreateDrawingContext(clear: false).Dispose());

            // Back in the window it bakes them again; a theme switch frees them as well.
            host.Child = map;
            Settle(8);
            var again = stage.BakedAvatars.OfType<Avalonia.Media.Imaging.RenderTargetBitmap>().ToList();
            again.ShouldNotBeEmpty();
            window.RequestedThemeVariant = Avalonia.Styling.ThemeVariant.Dark;
            Settle();
            Should.Throw<ObjectDisposedException>(() => again[0].CreateDrawingContext());
        }
        finally
        {
            window.Close();
        }
    }

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private static WeakReference MapOver(SemanticZoomOrganization org)
    {
        var (map, window) = Show(organization: org);
        map.FlyTo(org.Teams[1]);
        Settle();
        window.Content = null;
        window.Close();
        Settle();
        return new WeakReference(map);
    }

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private static WeakReference PanelOver(SemanticZoomNode scope)
    {
        var panel = new SemanticZoomPanel { Scope = scope };
        var window = TestHelpers.Show(panel);
        Settle();
        window.Content = null;
        window.Close();
        Settle();
        return new WeakReference(panel);
    }
}
