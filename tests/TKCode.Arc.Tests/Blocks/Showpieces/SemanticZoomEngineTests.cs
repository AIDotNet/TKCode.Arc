using Avalonia;
using Avalonia.Headless.XUnit;
using TKCode.Arc.Motion;

namespace TKCode.Arc.Tests.Blocks.Showpieces;

/// <summary>
/// The pure engine of <see cref="SemanticZoom"/>: layout (team rows, justified widths, card and tile geometry), the
/// level-of-detail table, framing and flight zooms, scope derivation, hit rules, dimming, the cursor's geometry, rollups,
/// the checklist rule and the tour clock. Numbers are the spec's measurements (semantic-zoom.md 4–6).
/// </summary>
public class SemanticZoomEngineTests
{
    private const double Frame = 1 / 60.0;

    /// <summary>An organization shaped like the measured preview: 6 / 4 / 4 / 4 / 3 projects, some with 10 tasks.</summary>
    internal static SemanticZoomOrganization Organization(int[]? projects = null, int tasks = 10)
    {
        projects ??= [6, 4, 4, 4, 3];
        var org = new SemanticZoomOrganization { Title = "Test transit" };
        var person = 0;
        for (var t = 0; t < projects.Length; t++)
        {
            var lead = new SemanticZoomPerson($"Lead {t}", "Lead");
            var team = new SemanticZoomTeam { Title = $"Team {t}", Lead = lead, Description = $"Team {t} description" };
            for (var p = 0; p < projects[t]; p++)
            {
                var project = new SemanticZoomProject
                {
                    Title = $"Project {t}.{p}",
                    Lead = lead,
                    Due = new DateOnly(2026, 10, 1 + p),
                    Description = "Short pitch"
                };
                var count = p == 0 ? tasks : 4 + (p % 3);
                for (var i = 0; i < count; i++)
                {
                    var task = new SemanticZoomTask
                    {
                        Title = $"Task {t}.{p}.{i}",
                        Assignee = new SemanticZoomPerson($"Person {person++}", "Engineer"),
                        Due = new DateOnly(2026, 9, 10 + i),
                        Points = 3
                    };
                    task.Checklist.Add(new SemanticZoomChecklistItem("One"));
                    task.Checklist.Add(new SemanticZoomChecklistItem("Two"));
                    task.Checklist.Add(new SemanticZoomChecklistItem("Three"));
                    project.Tasks.Add(task);
                }
                team.Projects.Add(project);
            }
            org.Teams.Add(team);
        }
        return org;
    }

    private static ShowpieceCamera2D Camera(SemanticZoomLayout layout, Size stage, Thickness? insets = null)
    {
        var camera = new ShowpieceCamera2D
        {
            MinZoom = 1,
            MaxZoom = 53.04,
            Insets = insets ?? SemanticZoomStage.WideInsets,
            ZoomOverflow = ShowpieceZoomOverflow.RubberBand,
            HasInertia = true,
            FlightMode = ShowpieceFlightMode.VanWijk
        };
        camera.SetViewport(stage);
        camera.SetContent(layout.ContentBounds);
        return camera;
    }

    // Layout ------------------------------------------------------------------------------------------------------------

    [Fact]
    public void Team_Rows_Pack_Like_The_Preview()
    {
        // 6 / 4 / 4 | 4 / 3: the first row uses two card rows (3 + 2 + 2 columns), the second one (4 + 3).
        SemanticZoomLayout.PackRows([6, 4, 4, 4, 3]).ShouldBe([0, 0, 0, 1, 1]);
        // The invented dataset (6 / 5 / 4 / 4 / 3) keeps "two rows, the second stretched".
        SemanticZoomLayout.PackRows([6, 5, 4, 4, 3]).ShouldBe([0, 0, 0, 1, 1]);
        SemanticZoomLayout.CardRowsFor(6).ShouldBe(2);
        SemanticZoomLayout.CardRowsFor(4).ShouldBe(1);
        SemanticZoomLayout.ColumnsFor(6, 2).ShouldBe(3);
        SemanticZoomLayout.ColumnsFor(3, 1).ShouldBe(3);
        SemanticZoomLayout.PackRows([5]).ShouldBe([0]);
        SemanticZoomLayout.PackRows([]).ShouldBeEmpty();
    }

    [AvaloniaFact]
    public void Layout_Matches_The_Measured_Geometry()
    {
        var layout = SemanticZoomLayout.Build(Organization());
        // Content 1236 × 682.5 wu: widest row 7 cards of 150; rows 417 and 241.5 high, 24 apart.
        layout.ContentBounds.ShouldBe(new Rect(0, 0, 1236, 682.5));
        layout.CardHeight.ShouldBe(163.5);
        SemanticZoomLayout.CardHeightFor(4).ShouldBe(163.5);
        var boxes = layout.Boxes;
        boxes[0].Rect.ShouldBe(new Rect(0, 0, 504, 417));
        boxes[1].Rect.X.ShouldBe(528);
        boxes[3].Rect.Y.ShouldBe(441);
        // Second row stretched to 1236: cards 156 wide.
        var firstCardRow2 = boxes[boxes[3].FirstChild].Rect;
        firstCardRow2.Width.ShouldBe(156, 1e-9);
        (boxes[4].Rect.Right).ShouldBe(1236, 1e-9);
        // Card inside a panel: 15 from the side, 63 below the panel's top.
        var card = boxes[layout.FirstProject].Rect;
        card.ShouldBe(new Rect(15, 63, 150, 163.5));
        // Task blocks 42 × 21 from (9, 45), 45 / 24 apart; 44 wide on a 156 card.
        var tile = boxes[layout.FirstTask].Rect;
        tile.ShouldBe(new Rect(24, 108, 42, 21));
        boxes[layout.FirstTask + 4].Rect.ShouldBe(new Rect(24 + 45, 108 + 24, 42, 21));
        SemanticZoomLayout.TileWidth(156).ShouldBe(44, 1e-9);
        layout.TeamCount.ShouldBe(5);
        layout.ProjectCount.ShouldBe(21);
    }

    [AvaloniaFact]
    public void Fit_Scales_Match_The_Three_Measured_Stages()
    {
        var layout = SemanticZoomLayout.Build(Organization());
        Camera(layout, new Size(663, 400)).FitScale.ShouldBe(0.4044, 0.0001);
        Camera(layout, new Size(1046, 580)).FitScale.ShouldBe(0.6681, 0.0001);
        // 356 × 420 under 600 px: insets 12 / 56, width-bound.
        Camera(layout, new Size(356, 420), SemanticZoomStage.CompactInsets).FitScale.ShouldBe(0.2686, 0.0001);
    }

    [AvaloniaFact]
    public void Flights_Frame_Teams_Projects_And_Tasks_At_The_Measured_Zooms()
    {
        var layout = SemanticZoomLayout.Build(Organization());
        foreach (var stage in new[] { new Size(663, 400), new Size(628, 400), new Size(1046, 580) })
        {
            var camera = Camera(layout, stage);
            camera.FrameRect(layout.Boxes[0].Rect, SemanticZoomStage.TeamFill).Zoom.ShouldBe(1.5876, 0.001);
            if (stage.Width < 1000)
            {
                camera.FrameRect(layout.Boxes[layout.FirstProject].Rect, SemanticZoomStage.TeamFill).Zoom.ShouldBe(4.049, 0.01);
                camera.FrameRect(layout.Boxes[layout.FirstTask].Rect, SemanticZoomStage.TaskFill).Zoom.ShouldBe(31.2, 0.1);
            }
        }
        // Width-bound narrow stage: team 2.38× (fill 0.97 of 332 / 504 at 0.2686).
        var narrow = Camera(layout, new Size(356, 420), SemanticZoomStage.CompactInsets);
        narrow.FrameRect(layout.Boxes[0].Rect, SemanticZoomStage.TeamFill).Zoom.ShouldBe(2.38, 0.01);
        narrow.FrameRect(layout.Boxes[layout.FirstTask].Rect, SemanticZoomStage.TaskFill).Zoom.ShouldBe(28.3, 0.1);
    }

    [AvaloniaFact]
    public void A_Flight_Into_The_Top_Left_Team_Stops_At_The_Bounds()
    {
        var layout = SemanticZoomLayout.Build(Organization());
        var camera = Camera(layout, new Size(663, 400));
        camera.FlyToRect(layout.Boxes[0].Rect, SemanticZoomStage.TeamFill);
        while (camera.Advance(Frame))
        {
        }
        // Held inside the pan bounds: the panel's left edge 24 px and its top 64 px from the stage edge.
        var topLeft = camera.WorldToScreen(layout.Boxes[0].Rect.TopLeft);
        topLeft.X.ShouldBe(24, 0.5);
        topLeft.Y.ShouldBe(64, 0.5);
    }

    // Level of detail ---------------------------------------------------------------------------------------------------

    [Fact]
    public void Detail_Layers_Fade_In_Their_Measured_K_Windows()
    {
        // Windows on k (z × 0.4044): team line z 1.43–1.72, chips → blocks 2.1–3.7, project title 2.50–3.02 …
        (double, double)[] expected =
        [
            (0.58, 0.70), (0.85, 1.50), (1.01, 1.22), (1.27, 1.53), (1.33, 1.60), (1.72, 2.37), (2.55, 3.30), (7.25, 9.9), (10.8, 11.9)
        ];
        for (var i = 0; i < SemanticZoomLod.LayerCount; i++)
        {
            var layer = (SemanticZoomLayer)i;
            SemanticZoomLod.Window(layer).ShouldBe(expected[i]);
            SemanticZoomLod.Alpha(layer, expected[i].Item1).ShouldBe(0, 1e-9);
            if (layer != SemanticZoomLayer.TaskMeta)
            {
                SemanticZoomLod.Alpha(layer, expected[i].Item2).ShouldBe(1, 1e-9);
                SemanticZoomLod.Alpha(layer, (expected[i].Item1 + expected[i].Item2) / 2).ShouldBe(0.5, 1e-9);
            }
        }
        // The block corner cross-fades with the details (≈ 0.54 at 21×, 0 from 24.5×).
        SemanticZoomLod.Alpha(SemanticZoomLayer.TaskMeta, 21 * 0.4044).ShouldBe(0.54, 0.03);
        SemanticZoomLod.Alpha(SemanticZoomLayer.TaskMeta, 24.5 * 0.4044).ShouldBe(0, 0.01);
        SemanticZoomLod.Alpha(SemanticZoomLayer.TaskMeta, 10 * 0.4044).ShouldBe(1, 1e-9);
        // The wide stage's fit (k 0.668) already shows most of the team line; the preview's (0.4044) none.
        SemanticZoomLod.Alpha(SemanticZoomLayer.TeamLine, 0.6681).ShouldBeGreaterThan(0.7);
        SemanticZoomLod.Alpha(SemanticZoomLayer.TeamLine, 0.4044).ShouldBe(0);
    }

    [Fact]
    public void Fonts_And_Radii_Follow_K_With_Clamps()
    {
        SemanticZoomLod.TeamTitleSize(0.4044).ShouldBe(11);
        SemanticZoomLod.TeamTitleSize(0.668).ShouldBe(14.03, 0.01);
        SemanticZoomLod.TeamTitleSize(5).ShouldBe(22);
        SemanticZoomLod.TeamPercentSize(0.4044).ShouldBe(11);
        SemanticZoomLod.TeamLineSize(3).ShouldBe(13);
        SemanticZoomLod.ProjectTitleSize(1.294).ShouldBe(12.62, 0.01);
        SemanticZoomLod.ProjectTitleSize(9).ShouldBe(20);
        SemanticZoomLod.ProjectSmallSize(1.618).ShouldBe(11.52, 0.01);
        SemanticZoomLod.TaskTitleSize(3.26).ShouldBe(11.25, 0.01);
        SemanticZoomLod.TaskTitleSize(5).ShouldBe(14);
        SemanticZoomLod.TaskMetaSize(9).ShouldBe(12.32);
        SemanticZoomLod.DetailTitleSize(12.21).ShouldBe(17.43, 0.05);
        SemanticZoomLod.DetailTitleSize(30).ShouldBe(28);
        // Radii: team 6 px at the preview's fit, 9.02 at the wide fit; cards 3.64; blocks never under 1.5; bars 1.5–6.
        SemanticZoomLod.TeamRadius(0.4044).ShouldBe(6);
        SemanticZoomLod.TeamRadius(0.668).ShouldBe(9.02, 0.01);
        SemanticZoomLod.CardRadius(0.4044).ShouldBe(3.64, 0.01);
        SemanticZoomLod.CardRadius(10).ShouldBe(26);
        SemanticZoomLod.TileRadius(0.4044).ShouldBe(1.5);
        SemanticZoomLod.TileRadius(12.6).ShouldBe(20);
        SemanticZoomLod.BarHeight(0.4044).ShouldBe(1.5);
        SemanticZoomLod.BarHeight(10).ShouldBe(6);
    }

    [Fact]
    public void Dot_Layers_Cycle_Every_Fourfold_Zoom()
    {
        // At the preview's fit: 38.8 px and 155 px apart, both at 0.55.
        var k = 0.4044;
        SemanticZoomLod.FirstDotLayer(k).ShouldBe(0);
        SemanticZoomLod.DotAlpha(96 * k).ShouldBe(0.55, 0.001);
        SemanticZoomLod.DotAlpha(384 * k).ShouldBe(0.55, 0.002);
        // z 1.89 and 7.55 share the same set: 18.3 / 73.3 / 293 px at ≈ 0.10 / 0.55 / 0.24.
        SemanticZoomLod.DotAlpha(18.3).ShouldBe(0.10, 0.03);
        SemanticZoomLod.DotAlpha(73.3).ShouldBe(0.55, 1e-9);
        SemanticZoomLod.DotAlpha(293).ShouldBe(0.24, 0.03);
        // Measured fade-in: 0.01 at 15.5 px, 0.29 at 22.3, 0.48 at 26.3; fade-out 0.37 at 248.5, 0.01 at 394.5.
        SemanticZoomLod.DotAlpha(15.5).ShouldBe(0.01, 0.01);
        SemanticZoomLod.DotAlpha(22.3).ShouldBe(0.29, 0.02);
        SemanticZoomLod.DotAlpha(26.3).ShouldBe(0.48, 0.02);
        SemanticZoomLod.DotAlpha(248.5).ShouldBe(0.37, 0.02);
        SemanticZoomLod.DotAlpha(394.5).ShouldBe(0.01, 0.01);
        SemanticZoomLod.DotAlpha(13).ShouldBe(0);
        SemanticZoomLod.DotAlpha(430).ShouldBe(0);
        // Deep zoom uses finer world grids (negative layers).
        SemanticZoomLod.FirstDotLayer(21.45).ShouldBeLessThan(0);
    }

    [Fact]
    public void Chips_Shade_By_Status()
    {
        SemanticZoomLod.ChipAlpha(SemanticZoomTaskStatus.Done).ShouldBe(0.13);
        SemanticZoomLod.ChipAlpha(SemanticZoomTaskStatus.InProgress).ShouldBe(0.85);
        SemanticZoomLod.ChipAlpha(SemanticZoomTaskStatus.InReview).ShouldBe(0.42);
        SemanticZoomLod.ChipAlpha(SemanticZoomTaskStatus.Todo).ShouldBe(0.6);
        SemanticZoomLod.ChipAlpha(SemanticZoomTaskStatus.Blocked).ShouldBe(0.85);
    }

    // Scope, hit rules, dimming ---------------------------------------------------------------------------------------

    [AvaloniaFact]
    public void Scope_Follows_The_Center_And_The_Zoom_Thresholds()
    {
        var layout = SemanticZoomLayout.Build(Organization());
        var scene = new SemanticZoomScene();
        scene.Load(layout, 0.4044);
        var task = layout.Boxes[layout.FirstTask];
        var center = task.Rect.Center;
        SemanticZoomLod.LevelForZoom(1).ShouldBe(SemanticZoomLevel.Organization);
        scene.ScopeAt(center, 1.07).ShouldBe(SemanticZoomScope.Organization);
        scene.ScopeAt(center, 1.08).ShouldBe(new SemanticZoomScope(0, -1, -1));
        scene.ScopeAt(center, 2.35).ShouldBe(new SemanticZoomScope(0, layout.FirstProject, -1));
        scene.ScopeAt(center, 18.29).Level.ShouldBe(SemanticZoomLevel.Project);
        scene.ScopeAt(center, 18.3).ShouldBe(new SemanticZoomScope(0, layout.FirstProject, layout.FirstTask));
        // Between two blocks the scope stays at the project; between two cards at the team; outside teams at the company.
        var gap = new Point(task.Rect.Right + 1.5, task.Rect.Center.Y);
        scene.ScopeAt(gap, 30).ShouldBe(new SemanticZoomScope(0, layout.FirstProject, -1));
        var cardGap = new Point(layout.Boxes[layout.FirstProject].Rect.Right + 6, 100);
        scene.ScopeAt(cardGap, 5).ShouldBe(new SemanticZoomScope(0, -1, -1));
        scene.ScopeAt(new Point(516, 100), 3).ShouldBe(SemanticZoomScope.Organization);
    }

    [AvaloniaFact]
    public void Hit_Rules_Allow_Children_And_Siblings_Only()
    {
        var layout = SemanticZoomLayout.Build(Organization());
        var scene = new SemanticZoomScene { Animates = false };
        scene.Load(layout, 0.4044);
        var team0 = layout.Boxes[0];
        var project0 = layout.FirstProject;
        var project1 = layout.FirstProject + 1;
        var task0 = layout.FirstTask;
        var task1 = layout.FirstTask + 1;
        var inCard = layout.Boxes[project0].Rect.Center;
        var inTile = layout.Boxes[task0].Rect.Center;

        // Company: the whole team panel hits the team.
        scene.HitTest(inTile).ShouldBe(0);
        scene.HitTest(new Point(team0.Rect.X + 5, team0.Rect.Y + 5)).ShouldBe(0);
        scene.HitTest(new Point(516, 100)).ShouldBe(-1);

        // Team: its projects and the other teams; not itself.
        scene.SetScope(new SemanticZoomScope(0, -1, -1));
        scene.HitTest(inCard).ShouldBe(project0);
        scene.HitTest(new Point(team0.Rect.X + 5, team0.Rect.Y + 5)).ShouldBe(-1);
        scene.HitTest(layout.Boxes[1].Rect.Center).ShouldBe(1);

        // Project: its tasks and the team's other projects; not other teams.
        scene.SetScope(new SemanticZoomScope(0, project0, -1));
        scene.HitTest(inTile).ShouldBe(task0);
        scene.HitTest(new Point(inCard.X, layout.Boxes[project0].Rect.Y + 5)).ShouldBe(-1);
        scene.HitTest(layout.Boxes[project1].Rect.Center).ShouldBe(project1);
        scene.HitTest(layout.Boxes[1].Rect.Center).ShouldBe(-1);

        // Task: the project's other tasks only.
        scene.SetScope(new SemanticZoomScope(0, project0, task0));
        scene.HitTest(inTile).ShouldBe(-1);
        scene.HitTest(layout.Boxes[task1].Rect.Center).ShouldBe(task1);
        scene.HitTest(layout.Boxes[project1].Rect.Center).ShouldBe(-1);
    }

    [AvaloniaFact]
    public void Dimming_Multiplies_Down_The_Chain_And_Eases()
    {
        var layout = SemanticZoomLayout.Build(Organization());
        var scene = new SemanticZoomScene();
        scene.Load(layout, 0.4044);
        var project0 = layout.FirstProject;
        var project1 = layout.FirstProject + 1;
        var task0 = layout.FirstTask;
        var task1 = layout.FirstTask + 1;
        scene.SetScope(new SemanticZoomScope(0, project0, task0));
        scene.DimTarget(1).ShouldBe(SemanticZoomLod.TeamDim);
        scene.DimTarget(layout.Boxes[1].FirstChild).ShouldBe(SemanticZoomLod.TeamDim);
        scene.DimTarget(project1).ShouldBe(0.5);
        scene.DimTarget(layout.Boxes[project1].FirstChild).ShouldBe(0.5);
        scene.DimTarget(task1).ShouldBe(0.5);
        scene.DimTarget(task0).ShouldBe(1);
        // Smoothed ×0.885 per frame: one frame closes 11.5% of the gap.
        scene.Dim(1).ShouldBe(1);
        scene.Step(Frame);
        scene.Dim(1).ShouldBe(1 - 0.115 * (1 - SemanticZoomLod.TeamDim), 0.001);
        for (var i = 0; i < 120; i++)
        {
            scene.Step(Frame);
        }
        scene.Dim(1).ShouldBe(SemanticZoomLod.TeamDim, 1e-9);
        scene.Step(Frame).ShouldBeFalse();
    }

    [AvaloniaFact]
    public void Hover_Outline_Eases_At_Full_Motion_And_Snaps_At_Reduced()
    {
        var layout = SemanticZoomLayout.Build(Organization());
        var scene = new SemanticZoomScene();
        scene.Load(layout, 0.4044);
        scene.SetHovered(2);
        scene.Step(Frame);
        scene.Hover(2).ShouldBe(0.17, 0.005);
        for (var i = 0; i < 17; i++)
        {
            scene.Step(Frame);
        }
        // ≈ 95% after ~270 ms (0.83^18 ≈ 0.035).
        scene.Hover(2).ShouldBeGreaterThan(0.95);
        scene.Animates = false;
        scene.SetHovered(-1);
        scene.Hover(2).ShouldBe(0);
    }

    [AvaloniaFact]
    public void Detail_Layers_Lag_Behind_The_Zoom()
    {
        var layout = SemanticZoomLayout.Build(Organization());
        var scene = new SemanticZoomScene();
        scene.Load(layout, 0.4044);
        scene.SetScale(1.5);
        scene.LodTarget(SemanticZoomLayer.ProjectTitle).ShouldBe(1);
        scene.Step(Frame);
        scene.Lod(SemanticZoomLayer.ProjectTitle).ShouldBe(0.115, 0.002);
    }

    [Fact]
    public void Cursor_Moves_To_The_Nearest_Item_In_A_Direction()
    {
        // Team centers of the preview layout: three on top, two below.
        Point[] centers = [new(252, 208), new(780, 208), new(1150, 208), new(345, 561), new(1060, 561)];
        SemanticZoomScene.Nearest(centers, centers[0], SemanticZoomDirection.Right).ShouldBe(1);
        SemanticZoomScene.Nearest(centers, centers[1], SemanticZoomDirection.Right).ShouldBe(2);
        SemanticZoomScene.Nearest(centers, centers[2], SemanticZoomDirection.Down).ShouldBe(4);
        SemanticZoomScene.Nearest(centers, centers[4], SemanticZoomDirection.Left).ShouldBe(3);
        SemanticZoomScene.Nearest(centers, centers[3], SemanticZoomDirection.Up).ShouldBe(0);
        SemanticZoomScene.Nearest(centers, centers[2], SemanticZoomDirection.Right).ShouldBe(-1);
    }

    [AvaloniaFact]
    public void Cursor_Candidates_Are_Children_Then_Siblings()
    {
        var layout = SemanticZoomLayout.Build(Organization());
        var scene = new SemanticZoomScene();
        scene.Load(layout, 0.4044);
        Span<int> buffer = stackalloc int[64];
        scene.CandidateCount(buffer).ShouldBe(5);
        scene.SetScope(new SemanticZoomScope(0, -1, -1));
        var count = scene.CandidateCount(buffer);
        count.ShouldBe(6 + 4);
        buffer[0].ShouldBe(layout.FirstProject);
        buffer[6].ShouldBe(1);
        scene.SetScope(new SemanticZoomScope(0, layout.FirstProject, layout.FirstTask));
        scene.CandidateCount(buffer).ShouldBe(9);
        scene.IsCandidate(layout.FirstTask).ShouldBeFalse();
        scene.IsCandidate(layout.FirstTask + 1).ShouldBeTrue();
    }

    // Rollups and the checklist ----------------------------------------------------------------------------------------

    [AvaloniaFact]
    public void Rollups_Count_Open_Blocked_Next_Due_Progress_And_People()
    {
        var org = Organization([2], tasks: 4);
        var team = org.Teams[0];
        var project = team.Projects[0];
        project.Tasks[0].Status = SemanticZoomTaskStatus.Done;
        project.Tasks[1].Status = SemanticZoomTaskStatus.Blocked;
        project.Tasks[2].Checklist[0].IsDone = true;
        project.Health = SemanticZoomHealth.AtRisk;

        project.TaskCount.ShouldBe(4);
        project.OpenTasks.ShouldBe(3);
        project.BlockedTasks.ShouldBe(1);
        // Next due skips done tasks: task 0 (Sep 10) is done, so Sep 11.
        project.NextDue.ShouldBe(new DateOnly(2026, 9, 11));
        // Progress is the mean of the tasks' own progress: (1 + 0 + 1/3 + 0) / 4.
        project.Progress.ShouldBe((1 + 1 / 3.0) / 4, 1e-9);
        // People: the lead, then every assignee, without repeats.
        project.People.Count.ShouldBe(5);
        project.People[0].ShouldBe(team.Lead);
        team.NeedAttention.ShouldBe(1);
        org.NeedAttention.ShouldBe(1);
        org.ProjectCount.ShouldBe(2);
        org.OpenTasks.ShouldBe(team.OpenTasks);
        // Team and company average every task below them (task-weighted).
        var all = org.AllTasks.ToList();
        org.Progress.ShouldBe(all.Sum(t => t.Status == SemanticZoomTaskStatus.Done ? 1 : t.ChecklistDone / 3.0) / all.Count, 1e-9);
        // A done task has no next due date.
        project.Tasks[0].NextDue.ShouldBeNull();
    }

    [AvaloniaFact]
    public void Ticking_The_Last_Item_Finishes_A_Task_And_Unticking_Puts_It_Back_In_Review()
    {
        var org = Organization([1], tasks: 4);
        var project = org.Teams[0].Projects[0];
        var task = project.Tasks[0];
        task.Status = SemanticZoomTaskStatus.InProgress;
        var before = project.Progress;
        foreach (var item in task.Checklist)
        {
            item.IsDone = true;
        }
        task.Status.ShouldBe(SemanticZoomTaskStatus.Done);
        project.Progress.ShouldBeGreaterThan(before);
        project.OpenTasks.ShouldBe(3);
        task.Checklist[1].IsDone = false;
        task.Status.ShouldBe(SemanticZoomTaskStatus.InReview);
        task.Progress.ShouldBe(2 / 3.0, 1e-9);
        project.OpenTasks.ShouldBe(4);
    }

    [AvaloniaFact]
    public void Structure_Changes_Keep_Parents_And_Rollups()
    {
        var org = Organization([1], tasks: 4);
        var project = org.Teams[0].Projects[0];
        var changed = 0;
        org.StructureChanged += () => changed++;
        var task = new SemanticZoomTask { Title = "New", Status = SemanticZoomTaskStatus.Blocked };
        project.Tasks.Add(task);
        task.Parent.ShouldBe(project);
        task.Organization.ShouldBe(org);
        task.GetPath().ShouldBe([org, org.Teams[0], project, task]);
        org.BlockedTasks.ShouldBe(1);
        project.Tasks.Remove(task);
        task.Parent.ShouldBeNull();
        org.BlockedTasks.ShouldBe(0);
        changed.ShouldBe(2);
        new SemanticZoomPerson("Mara Ostrov").DisplayInitials.ShouldBe("MO");
        new SemanticZoomPerson("Kenji").DisplayInitials.ShouldBe("K");
        new SemanticZoomPerson("Aroha Tane") { Initials = "AT!" }.DisplayInitials.ShouldBe("AT!");
    }

    [AvaloniaFact]
    public void Labels_Format_Rollups_And_Generated_Summaries()
    {
        var org = Organization([1], tasks: 4);
        var project = org.Teams[0].Projects[0];
        var task = project.Tasks[0];
        task.Status = SemanticZoomTaskStatus.InReview;
        task.Checklist[0].IsDone = true;
        task.CommentCount = 1;
        var texts = new SemanticZoomTexts();
        var formatter = SemanticZoomFormatter.Default;
        SemanticZoomLabels.TaskMeta(task, texts, formatter).ShouldBe("1/3 · Sep 10");
        SemanticZoomLabels.TaskDetail(task, texts, formatter).ShouldBe("· Due Sep 10 · 3 points");
        SemanticZoomLabels.ProjectStatus(project, texts, formatter).ShouldBe("On track · Due Oct 1");
        SemanticZoomLabels.PeopleText(project.People, texts).ShouldBe("Lead 0 and 4 others");
        SemanticZoomLabels.Summary(task, texts, formatter).ShouldContain("Lead");
        task.Status = SemanticZoomTaskStatus.Blocked;
        SemanticZoomLabels.TaskMeta(task, texts, formatter).ShouldBe("Blocked");
        task.Summary = "Given";
        SemanticZoomLabels.Summary(task, texts, formatter).ShouldBe("Given");
        formatter.FormatPercent(0.594).ShouldBe("59%");
    }

    // Tour ---------------------------------------------------------------------------------------------------------------

    [Fact]
    public void Tour_Leaves_At_Once_Dwells_And_Ends_After_The_Last_Stop()
    {
        var tour = new SemanticZoomTour();
        tour.Start(3).ShouldBe(SemanticZoomTourAction.GoToStop);
        tour.StopIndex.ShouldBe(0);
        // Travelling while the camera flies, then 1.5 s at the stop.
        tour.Advance(Frame, cameraMoving: true).ShouldBe(SemanticZoomTourAction.None);
        tour.Advance(Frame, cameraMoving: false).ShouldBe(SemanticZoomTourAction.None);
        tour.IsDwelling.ShouldBeTrue();
        var dwell = 0.0;
        SemanticZoomTourAction action;
        do
        {
            action = tour.Advance(Frame, false);
            dwell += Frame;
        }
        while (action == SemanticZoomTourAction.None);
        action.ShouldBe(SemanticZoomTourAction.GoToStop);
        dwell.ShouldBe(1.5, Frame);
        tour.StopIndex.ShouldBe(1);
        tour.Advance(Frame, false);
        for (var i = 0; i < 91; i++)
        {
            tour.Advance(Frame, false);
        }
        tour.StopIndex.ShouldBe(2);
        // The last stop ends ≈ 200 ms after arriving.
        tour.Advance(Frame, false).ShouldBe(SemanticZoomTourAction.None);
        var end = 0.0;
        do
        {
            action = tour.Advance(Frame, false);
            end += Frame;
        }
        while (action == SemanticZoomTourAction.None);
        action.ShouldBe(SemanticZoomTourAction.Finished);
        end.ShouldBe(0.2, Frame);
        tour.IsPlaying.ShouldBeFalse();
    }

    [Fact]
    public void Tour_At_Reduced_Motion_Holds_Each_Stop_For_2_4_Seconds()
    {
        var tour = new SemanticZoomTour { IsReduced = true };
        tour.Start(2);
        tour.Advance(Frame, false);
        tour.Remaining.ShouldBe(SemanticZoomTour.ReducedDwellSeconds);
        tour.Stop();
        tour.IsPlaying.ShouldBeFalse();
        tour.Advance(Frame, false).ShouldBe(SemanticZoomTourAction.None);
        tour.Start(0).ShouldBe(SemanticZoomTourAction.None);
    }

    [AvaloniaFact]
    public void Default_Tour_Visits_Team_Project_Two_Tasks_Team_Project_Then_Everything()
    {
        var org = Organization();
        var stops = SemanticZoomTour.DefaultStops(org);
        stops.Count.ShouldBe(7);
        stops[0].ShouldBe(org.Teams[0]);
        stops[1].ShouldBe(org.Teams[0].Projects[0]);
        stops[2].ShouldBe(org.Teams[0].Projects[1].Tasks[0]);
        stops[3].ShouldBe(org.Teams[0].Projects[1].Tasks[1]);
        stops[4].ShouldBe(org.Teams[1]);
        stops[5].ShouldBe(org.Teams[1].Projects[0]);
        stops[6].ShouldBe(org);
        // An empty organization tours only "everything".
        SemanticZoomTour.DefaultStops(new SemanticZoomOrganization()).Count.ShouldBe(1);
    }

    [AvaloniaFact]
    public void Flight_Durations_Follow_The_Path_Length()
    {
        var layout = SemanticZoomLayout.Build(Organization());
        var camera = Camera(layout, new Size(663, 400));
        var extent = 663.0;
        var (center, zoom) = camera.FrameRect(layout.Boxes[0].Rect, SemanticZoomStage.TeamFill);
        var path = VanWijkPath.Create(camera.Center, extent / camera.Scale, center, extent / (camera.FitScale * zoom));
        // Fit → team: S ≈ 0.5 → ≈ 650 ms (460 + 380·S).
        path.Length.ShouldBeInRange(0.35, 0.7);
        path.Duration.TotalMilliseconds.ShouldBe(460 + 380 * path.Length, 0.001);
    }
}
