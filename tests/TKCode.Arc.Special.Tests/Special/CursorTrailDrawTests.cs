using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Threading;
using TKCode.Arc.Motion;

namespace TKCode.Arc.Tests.Special;

/// <summary>
/// Cursor trail draw: defaults, drawing with the pointer and the keyboard, snapping, keep as drawn, undo and clear,
/// the change event and live region, the intro under every motion level, the toolbar and the narrow layout.
/// </summary>
public class CursorTrailDrawTests
{
    private const double Frame = 1.0 / 60;

    private static (Window Window, CursorTrailDraw Board) ShowBoard(bool intro = false, double width = 904,
        ArcMotionLevel level = ArcMotionLevel.Off, Action<CursorTrailDraw>? setup = null)
    {
        var board = new CursorTrailDraw { Width = width, PlaysIntro = intro, VerticalAlignment = Avalonia.Layout.VerticalAlignment.Top };
        setup?.Invoke(board);
        var window = TestHelpers.Show(board, level);
        window.Width = width + 100;
        window.Height = 900;
        Dispatcher.UIThread.RunJobs();
        return (window, board);
    }

    private static Point At(CursorTrailDraw control, double boardX, double boardY) =>
        control.Board!.TranslatePoint(new Point(boardX, boardY), TopLevel.GetTopLevel(control)!) ?? default;

    /// <summary>Drags through board px, stepping the board one frame per move as a real pointer would.</summary>
    private static void Draw(Window window, CursorTrailDraw control, params Point[] path)
    {
        var points = new List<Point>();
        for (var i = 1; i < path.Length; i++)
        {
            var a = path[i - 1];
            var b = path[i];
            var steps = Math.Max(1, (int)(CursorTrailDrawInkGeometry.Distance(a, b) / 6));
            for (var k = 0; k < steps; k++)
            {
                points.Add(a + (b - a) * (k / (double)steps));
            }
        }
        points.Add(path[^1]);
        window.MouseMove(At(control, points[0].X, points[0].Y));
        window.MouseDown(At(control, points[0].X, points[0].Y), MouseButton.Left);
        foreach (var p in points.Skip(1))
        {
            window.MouseMove(At(control, p.X, p.Y), RawInputModifiers.LeftMouseButton);
            control.Board!.Step(Frame);
        }
        for (var i = 0; i < 10; i++)
        {
            control.Board!.Step(Frame);
        }
        window.MouseUp(At(control, path[^1].X, path[^1].Y), MouseButton.Left);
        Dispatcher.UIThread.RunJobs();
    }

    private static Point[] Ring(double cx, double cy, double r, double turns = 1.05)
    {
        var points = new Point[60];
        for (var i = 0; i < points.Length; i++)
        {
            var a = -Math.PI / 2 + Math.PI * 2 * turns * i / (points.Length - 1);
            points[i] = new Point(cx + Math.Cos(a) * r, cy + Math.Sin(a) * r);
        }
        return points;
    }

    private static T Part<T>(CursorTrailDraw control, string name) where T : Control => TestHelpers.Descendant<T>(control, name);

    [AvaloniaFact]
    public void Defaults_match_the_reference_board()
    {
        var board = new CursorTrailDraw();
        board.Label.ShouldBe("Whiteboard");
        board.Tool.ShouldBe(CursorTrailDrawTool.Pen);
        board.IsSnapEnabled.ShouldBeTrue();
        board.PenSize.ShouldBe(4);
        board.Smoothing.ShouldBe(0.5);
        board.LaserFade.ShouldBe(TimeSpan.FromMilliseconds(700));
        board.Speed.ShouldBe(1);
        board.IsPaused.ShouldBeFalse();
        board.PlaysIntro.ShouldBeTrue();
        board.Hint.ShouldNotBeNullOrEmpty();
        board.Items.ShouldBeEmpty();
    }

    [AvaloniaFact]
    public void The_stage_is_16_by_10_and_turns_4_by_5_on_narrow_boards()
    {
        var (_, wide) = ShowBoard();
        var size = wide.Board!.Bounds.Size;
        (size.Width / size.Height).ShouldBe(1.6, 0.01);
        size.Width.ShouldBe(884, 0.5);

        var (_, narrow) = ShowBoard(width: 420);
        narrow.Classes.Contains(":compact").ShouldBeTrue();
        var narrowSize = narrow.Board!.Bounds.Size;
        (narrowSize.Width / narrowSize.Height).ShouldBe(0.8, 0.01);
        Part<Control>(narrow, "PART_Pen").Bounds.Width.ShouldBe(38);

        var (_, phone) = ShowBoard(width: 340);
        phone.Classes.Contains(":narrow").ShouldBeTrue();
        Part<Control>(phone, "PART_Pen").Bounds.Width.ShouldBe(30);
    }

    [AvaloniaFact]
    public void A_scribble_lands_as_a_stroke_and_raises_ItemsChanged()
    {
        var (window, board) = ShowBoard();
        var events = new List<CursorTrailDrawItemsChangedEventArgs>();
        board.ItemsChanged += (_, e) => events.Add(e);
        Draw(window, board, new(100, 300), new(160, 440), new(200, 320), new(240, 450), new(280, 310), new(320, 400));
        board.Items.Count.ShouldBe(1);
        var item = board.Items[0];
        item.Shape.ShouldBe(CursorTrailDrawShape.Stroke);
        item.InkId.ShouldBe("ink");
        item.Points.Count.ShouldBeGreaterThan(20);
        events.Single().Change.ShouldBe(CursorTrailDrawChange.Added);
        events[0].Items.Count.ShouldBe(1);
        board.LastAnnouncement.ShouldBe(board.Texts.Added);
    }

    [AvaloniaFact]
    public void Points_are_in_board_units_1000_wide()
    {
        var (window, board) = ShowBoard();
        Draw(window, board, new(0, 100), new(884, 100));
        var line = board.Items.Single();
        line.Shape.ShouldBe(CursorTrailDrawShape.Line);
        line.Points[0].X.ShouldBe(0, 0.5);
        line.Points[^1].X.ShouldBe(1000, 0.5);
    }

    [AvaloniaFact]
    public void A_rough_circle_snaps_and_the_chip_offers_to_keep_it_as_drawn()
    {
        var (window, board) = ShowBoard();
        var events = new List<CursorTrailDrawItemsChangedEventArgs>();
        board.ItemsChanged += (_, e) => events.Add(e);
        Draw(window, board, Ring(400, 260, 110));
        board.Items.Single().Shape.ShouldBe(CursorTrailDrawShape.Circle);
        events.Single().Change.ShouldBe(CursorTrailDrawChange.Snapped);
        board.IsChipShown.ShouldBeTrue();
        Part<Border>(board, "PART_Chip").IsVisible.ShouldBeTrue();
        board.LastAnnouncement.ShouldContain("circle");

        board.KeepAsDrawn();
        board.Items.Single().Shape.ShouldBe(CursorTrailDrawShape.Stroke);
        events[^1].Change.ShouldBe(CursorTrailDrawChange.KeptAsDrawn);
        board.IsChipShown.ShouldBeFalse();
        board.LastAnnouncement.ShouldBe(board.Texts.Kept);
    }

    [AvaloniaFact]
    public void With_snapping_off_shapes_stay_as_drawn()
    {
        var (window, board) = ShowBoard(setup: b => b.IsSnapEnabled = false);
        Draw(window, board, Ring(400, 260, 110));
        board.Items.Single().Shape.ShouldBe(CursorTrailDrawShape.Stroke);
        board.IsChipShown.ShouldBeFalse();
    }

    [AvaloniaFact]
    public void Undo_and_clear_remove_items_and_say_so()
    {
        var (window, board) = ShowBoard();
        var undo = Part<Control>(board, "PART_Undo");
        undo.IsEnabled.ShouldBeFalse();
        Draw(window, board, new(100, 100), new(400, 100));
        Draw(window, board, Ring(600, 300, 90));
        undo.IsEnabled.ShouldBeTrue();
        board.Undo();
        board.Items.Single().Shape.ShouldBe(CursorTrailDrawShape.Line);
        board.LastAnnouncement.ShouldContain("circle");
        board.Clear();
        board.Items.ShouldBeEmpty();
        board.LastAnnouncement.ShouldBe(board.Texts.Cleared);
        undo.IsEnabled.ShouldBeFalse();
        board.Undo();
        board.LastAnnouncement.ShouldBe(board.Texts.NothingToUndo);
    }

    [AvaloniaFact]
    public void Shortcuts_pick_tools_toggle_snapping_and_undo()
    {
        var (window, board) = ShowBoard();
        board.Board!.Focus(NavigationMethod.Tab);
        window.KeyPressQwerty(PhysicalKey.L, RawInputModifiers.None);
        board.Tool.ShouldBe(CursorTrailDrawTool.Laser);
        board.Classes.Contains(":laser").ShouldBeTrue();
        window.KeyPressQwerty(PhysicalKey.P, RawInputModifiers.None);
        board.Tool.ShouldBe(CursorTrailDrawTool.Pen);
        window.KeyPressQwerty(PhysicalKey.S, RawInputModifiers.None);
        board.IsSnapEnabled.ShouldBeFalse();
        board.LastAnnouncement.ShouldBe(board.Texts.SnapOff);
        Draw(window, board, new(100, 100), new(300, 100));
        board.Board!.Focus(NavigationMethod.Tab);
        window.KeyPressQwerty(PhysicalKey.Z, RawInputModifiers.Control);
        board.Items.ShouldBeEmpty();
    }

    [AvaloniaFact]
    public void The_keyboard_draws_with_held_arrows_and_space()
    {
        var (window, board) = ShowBoard();
        var surface = board.Board!;
        surface.Focus(NavigationMethod.Tab);
        surface.IsKeyboardMode.ShouldBeTrue();
        var start = surface.CursorPosition;
        start.ShouldBe(new Point(500, 312.5));
        window.KeyPressQwerty(PhysicalKey.Space, RawInputModifiers.None);
        surface.IsDrawing.ShouldBeTrue();
        board.LastAnnouncement.ShouldBe(board.Texts.PenDown);
        window.KeyPressQwerty(PhysicalKey.ArrowRight, RawInputModifiers.None);
        for (var i = 0; i < 30; i++)
        {
            surface.Step(Frame);
        }
        window.KeyReleaseQwerty(PhysicalKey.ArrowRight, RawInputModifiers.None);
        // 340 px/s for half a second on an 884 px board.
        (surface.CursorPosition.X - start.X).ShouldBe(340 * 0.5 / 0.884, 15);
        window.KeyPressQwerty(PhysicalKey.Space, RawInputModifiers.None);
        surface.IsDrawing.ShouldBeFalse();
        board.Items.Single().Shape.ShouldBe(CursorTrailDrawShape.Line);
    }

    [AvaloniaFact]
    public void Escape_drops_the_stroke_in_progress()
    {
        var (window, board) = ShowBoard();
        board.Board!.Focus(NavigationMethod.Tab);
        window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
        board.Board.IsDrawing.ShouldBeTrue();
        window.KeyPressQwerty(PhysicalKey.Escape, RawInputModifiers.None);
        board.Board.IsDrawing.ShouldBeFalse();
        board.Items.ShouldBeEmpty();
        board.LastAnnouncement.ShouldBe(board.Texts.Cancelled);
    }

    [AvaloniaFact]
    public void Without_motion_the_intro_sketch_is_shown_at_once()
    {
        var (_, board) = ShowBoard(intro: true);
        TestHelpers.PumpUntil(() => board.Items.Count == 3, 3000).ShouldBeTrue();
        board.Items.Select(i => i.Shape).ShouldBe(
            [CursorTrailDrawShape.Rectangle, CursorTrailDrawShape.Circle, CursorTrailDrawShape.Line]);
        board.Items[2].InkId.ShouldBe("accent");
        board.Board!.IsIntroPlaying.ShouldBeFalse();
    }

    [AvaloniaFact]
    public void The_animated_intro_draws_through_the_same_pipeline_and_a_press_takes_over()
    {
        var (window, board) = ShowBoard(intro: true, level: ArcMotionLevel.Full);
        var surface = board.Board!;
        TestHelpers.PumpUntil(() => surface.IsIntroPlaying, 3000).ShouldBeTrue();
        for (var t = 0.0; t < 2.3; t += Frame)
        {
            surface.Step(Frame);
        }
        board.Items.Count.ShouldBe(1);
        board.Items[0].Shape.ShouldBe(CursorTrailDrawShape.Rectangle);
        window.MouseDown(At(board, 700, 480), MouseButton.Left);
        window.MouseUp(At(board, 700, 480), MouseButton.Left);
        surface.IsIntroPlaying.ShouldBeFalse();
        board.Items.Count.ShouldBe(2);
        board.Items[1].Shape.ShouldBe(CursorTrailDrawShape.Stroke);
    }

    [AvaloniaFact]
    public void Under_full_motion_a_snap_morphs_on_the_spring_before_the_clean_shape()
    {
        var (window, board) = ShowBoard(level: ArcMotionLevel.Full);
        Draw(window, board, new(100, 100), new(400, 130));
        var item = board.Board!.Items.Single();
        item.Morph.ShouldNotBeNull();
        for (var t = 0.0; t < 0.7; t += Frame)
        {
            board.Board.Step(Frame);
        }
        item.Morph.ShouldBeNull();
        item.Clean.ShouldNotBeNull();
    }

    [Fact]
    public void The_snap_spring_overshoots_about_ten_percent()
    {
        var morph = CursorTrailDrawBoard.Morph.Create([new Point(0, 0)], [new Point(1, 0)], [1], [1], toClean: true);
        var peak = 0.0;
        for (var t = 0.0; t < 0.7; t += 0.004)
        {
            morph.Time = t;
            peak = Math.Max(peak, morph.Progress);
        }
        peak.ShouldBe(1.105, 0.01);
    }

    [Fact]
    public void Mouse_ink_is_full_when_slow_and_thins_to_a_third_when_fast()
    {
        CursorTrailDrawBoard.PressureForSpeed(60).ShouldBe(1);
        CursorTrailDrawBoard.PressureForSpeed(800).ShouldBe(0.87, 0.03);
        CursorTrailDrawBoard.PressureForSpeed(2400).ShouldBe(0.35, 1e-9);
        CursorTrailDrawBoard.TipTime(0.5).ShouldBe(0.021, 0.001);
        CursorTrailDrawBoard.TipTime(0).ShouldBe(0);
    }

    [AvaloniaFact]
    public void Custom_inks_fill_the_toolbar_and_pick_the_first()
    {
        var (window, board) = ShowBoard(setup: b =>
        {
            b.Inks.Add(new CursorTrailDrawInk("ink", "Ink"));
            b.Inks.Add(new CursorTrailDrawInk("accent", "Highlight"));
        });
        var swatches = Part<Panel>(board, "PART_Swatches").Children;
        swatches.Count.ShouldBe(2);
        AutomationProperties.GetName(swatches[1]).ShouldBe("Highlight color");
        ((CursorTrailDrawToolButton)swatches[0]).IsChecked.ShouldBeTrue();
        board.SelectedInkId = "accent";
        ((CursorTrailDrawToolButton)swatches[1]).IsChecked.ShouldBeTrue();
        Draw(window, board, new(100, 300), new(160, 440), new(200, 320), new(240, 450));
        board.Items.Single().InkId.ShouldBe("accent");
    }

    [AvaloniaFact]
    public void Saved_items_load_without_an_event()
    {
        var (_, board) = ShowBoard();
        var raised = false;
        board.ItemsChanged += (_, _) => raised = true;
        board.SetItems(
        [
            new CursorTrailDrawItem("s1", CursorTrailDrawShape.Square, "ink", 3.8, [new(100, 100), new(200, 100), new(200, 200), new(100, 200)]),
            new CursorTrailDrawItem("s2", CursorTrailDrawShape.Stroke, "red", 4, [new(300, 300), new(350, 320), new(400, 300)]),
        ]);
        raised.ShouldBeFalse();
        board.Items.Count.ShouldBe(2);
        Part<TextBlock>(board, "PART_Count").Text.ShouldBe("2 items");
    }

    [AvaloniaFact]
    public void The_board_is_a_named_whiteboard_with_help_and_a_summary()
    {
        var (window, board) = ShowBoard(setup: b => b.Label = "Launch plan");
        AutomationProperties.GetName(board).ShouldBe("Launch plan");
        AutomationProperties.GetName(board.Board!).ShouldBe("Launch plan drawing area");
        AutomationProperties.GetHelpText(board.Board!)!.ShouldContain("Nothing on the board yet.");
        Draw(window, board, Ring(400, 260, 110));
        AutomationProperties.GetHelpText(board.Board!)!.ShouldContain("Board contents: 1 circle.");
        AutomationProperties.GetLiveSetting(Part<TextBlock>(board, "PART_Status")).ShouldBe(AutomationLiveSetting.Polite);
    }

    [AvaloniaFact]
    public void The_hint_shows_only_on_an_empty_board()
    {
        var (window, board) = ShowBoard();
        var hint = Part<TextBlock>(board, "PART_Hint");
        hint.Opacity.ShouldBe(1);
        Draw(window, board, new(100, 100), new(300, 100));
        hint.Opacity.ShouldBe(0);
        board.Hint = string.Empty;
        board.Clear();
        hint.Opacity.ShouldBe(0);
    }
}
