using TKCode.Arc.Gallery.Demos;
using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;
using AtomUI.Theme.Algorithms;
using AtomUI.Theme.Configuration;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace TKCode.Arc.Tests.Blocks.Showpieces;

/// <summary>
/// <see cref="ActivityTerrain"/>: metrics and the derived summary, highlight and fly-in, the keyboard model, pointer
/// hit testing, orbiting, the heat map, the narrow and wide layouts, motion levels and idle frames, and accessibility.
/// </summary>
public class ActivityTerrainTests
{
    private static readonly ActivityTerrainCell Peak = new(2, 14);

    /// <summary>A plain week: values rise with the hour (and a little with the day); one clear peak on Wednesday 14:00.</summary>
    private static ActivityTerrainMetric Metric(string key = "visits", ActivityTerrainTone tone = ActivityTerrainTone.Accent, bool higherIsBetter = true)
    {
        var values = new double[168];
        var previous = new double[168];
        for (var i = 0; i < 168; i++)
        {
            var cell = ActivityTerrainCell.FromIndex(i);
            values[i] = 100 + 10 * cell.Hour + cell.Day;
            previous[i] = Math.Round(values[i] * 0.8);
        }
        values[Peak.Index] = 1000;
        return new ActivityTerrainMetric
        {
            Key = key, Label = char.ToUpperInvariant(key[0]) + key[1..], Unit = key, Values = values, PreviousValues = previous,
            Tone = tone, HigherIsBetter = higherIsBetter
        };
    }

    private static (Window Window, ActivityTerrain Terrain) Show(double width = 665, ArcMotionLevel level = ArcMotionLevel.Off,
                                                                 Action<ActivityTerrain>? setup = null)
    {
        var terrain = new ActivityTerrain
        {
            Width = width,
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,
            Title = "Weekly rhythm"
        };
        terrain.Metrics.Add(Metric());
        terrain.Metrics.Add(Metric("builds"));
        terrain.Metrics.Add(Metric("incidents", ActivityTerrainTone.Danger, higherIsBetter: false));
        setup?.Invoke(terrain);
        var window = TestHelpers.Show(terrain, level);
        window.Width = width + 40;
        window.Height = 1000;
        Pump();
        return (window, terrain);
    }

    private static void Pump(int frames = 3)
    {
        for (var i = 0; i < frames; i++)
        {
            AvaloniaHeadlessPlatform.ForceRenderTimerTick();
            Dispatcher.UIThread.RunJobs();
        }
    }

    /// <summary>Draws every terrain plot and card bar chart once (headless tests have no window frame to capture).</summary>
    private static void RenderPlots(Window window)
    {
        using var bitmap = new Avalonia.Media.Imaging.RenderTargetBitmap(new PixelSize(800, 600));
        foreach (var visual in window.GetVisualDescendants().Where(v => v is ActivityTerrainCanvas or ActivityTerrainCardBars))
        {
            using var context = bitmap.CreateDrawingContext();
            visual.Render(context);
        }
    }

    private static Point On(Window window, Control control, Point point) => control.TranslatePoint(point, window)!.Value;

    private static ActivityTerrainCanvas Canvas(ActivityTerrain terrain) => terrain.Canvas.ShouldNotBeNull();

    private static void Key(Window window, PhysicalKey key, RawInputModifiers modifiers = RawInputModifiers.None)
    {
        window.KeyPressQwerty(key, modifiers);
        window.KeyReleaseQwerty(key, modifiers);
        Dispatcher.UIThread.RunJobs();
    }

    [AvaloniaFact]
    public void Defaults_Show_The_First_Metric_At_Rest()
    {
        var (window, terrain) = Show();
        try
        {
            terrain.ViewMode.ShouldBe(ActivityTerrainViewMode.Perspective);
            terrain.PeakCount.ShouldBe(3);
            terrain.SelectedMetric.ShouldBe(terrain.Metrics[0]);
            terrain.SelectedMetricKey.ShouldBe("visits");
            terrain.Yaw.ShouldBe(32, 1e-6);
            terrain.Pitch.ShouldBe(30, 1e-6);
            terrain.Zoom.ShouldBe(1, 1e-9);
            terrain.IsViewDefault.ShouldBeTrue();
            terrain.Summary.Peak.ShouldBe(Peak);
            terrain.Summary.Peaks[0].ShouldBe(Peak);
            terrain.Classes.Contains(":narrow").ShouldBeFalse();
            var plot = TestHelpers.Descendant<Panel>(terrain, ActivityTerrain.PlotPart);
            plot.Bounds.Height.ShouldBe(ActivityTerrainMath.PlotHeight(663), 0.5); // 291.7, layout-rounded
            var title = TestHelpers.Descendant<TextBlock>(terrain, "PART_Title");
            title.Text.ShouldBe("Weekly rhythm");
            TestHelpers.Descendant<TextBlock>(terrain, "PART_Subtitle").IsVisible.ShouldBeFalse();
            // The fit for the 623 × 292 plot is Arc's f 801.
            var fit = Canvas(terrain).CurrentFit;
            (fit.Scale * 42).ShouldBe(801, 2);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Selecting_A_Metric_By_Key_Updates_Bars_And_Summary()
    {
        var (window, terrain) = Show();
        try
        {
            var swaps = new List<ActivityTerrainMetric?>();
            terrain.SelectedMetricKey = "incidents";
            terrain.SelectedMetric.ShouldBe(terrain.Metrics[2]);
            terrain.CurrentMetric.ShouldBe(terrain.Metrics[2]);
            var title = TestHelpers.Descendant<ArcContentSwap>(terrain, ActivityTerrain.SummaryTitlePart);
            title.Content.ShouldBe("Incidents this week");
            // A rise of a metric where lower is better is bad news.
            terrain.Classes.Contains(":change-bad").ShouldBeTrue();
            terrain.Classes.Contains(":change-good").ShouldBeFalse();
            terrain.SelectedMetric = terrain.Metrics[1];
            terrain.SelectedMetricKey.ShouldBe("builds");
            terrain.Classes.Contains(":change-good").ShouldBeTrue();
            var metricSwitch = TestHelpers.Descendant<SegmentedControl>(terrain, ActivityTerrain.MetricSwitchPart);
            metricSwitch.SelectedIndex.ShouldBe(1);
            metricSwitch.SelectedIndex = 0;
            terrain.SelectedMetricKey.ShouldBe("visits");
            // Motion off: the bars are at their new heights at once.
            Canvas(terrain).HeightAt(Peak.Index).ShouldBe(ActivityTerrainMath.MaxBarHeight, 1e-6);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Summary_Shows_Total_Change_Peaks_And_Busiest_Day()
    {
        var (window, terrain) = Show();
        try
        {
            var total = TestHelpers.Descendant<ShowpieceNumber>(terrain, ActivityTerrain.TotalPart);
            total.Value.ShouldBe(terrain.Summary.Total);
            var change = TestHelpers.Descendant<ShowpieceNumber>(terrain, ActivityTerrain.ChangePart);
            change.Value.ShouldBe(terrain.Summary.Change!.Value, 1e-9);
            var peaks = terrain.GetVisualDescendants().OfType<ActivityTerrainPeakButton>().ToList();
            peaks.Count.ShouldBe(3);
            peaks[0].Label.ShouldBe("Wed 14:00");
            peaks[0].Value.ShouldBe(1000);
            AutomationProperties.GetName(peaks[0]).ShouldStartWith("Wednesday 14:00, 1,000 visits");
            var busiest = TestHelpers.Descendant<ArcContentSwap>(terrain, ActivityTerrain.BusiestDayPart);
            busiest.Content.ShouldBe("Wednesday");
            var caption = TestHelpers.Descendant<ActivityTerrainCaption>(terrain, ActivityTerrain.BusiestCaptionPart);
            caption.Text.ShouldContain("visits,");
            caption.Text.ShouldContain("during working hours");
            terrain.PeakCount = 5;
            Pump();
            terrain.GetVisualDescendants().OfType<ActivityTerrainPeakButton>().Count().ShouldBe(5);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Highlight_Lifts_The_Bar_Dims_The_Rest_And_Opens_The_Card()
    {
        var (window, terrain) = Show();
        try
        {
            var hovered = new List<ActivityTerrainCell?>();
            terrain.CellHovered += (_, e) => hovered.Add(e.Cell);
            var cell = new ActivityTerrainCell(4, 9);
            terrain.HighlightedCell = cell;
            Pump();
            var canvas = Canvas(terrain);
            canvas.LiftAt(cell.Index).ShouldBe(ActivityTerrainMath.Lift, 1e-9);
            canvas.DimAt(cell.Index).ShouldBe(0);
            canvas.DimAt(new ActivityTerrainCell(4, 3).Index).ShouldBe(0);   // same row
            canvas.DimAt(new ActivityTerrainCell(1, 9).Index).ShouldBe(0);   // same column
            canvas.DimAt(new ActivityTerrainCell(1, 3).Index).ShouldBe(1);   // elsewhere
            hovered.ShouldBe([cell]);
            var card = terrain.Card.ShouldNotBeNull();
            card.IsOpen.ShouldBeTrue();
            var info = terrain.CardView.Info.ShouldNotBeNull();
            info.Day.ShouldBe("Friday");
            info.HourRange.ShouldBe("09:00–10:00");
            info.Value.ShouldBe("194");
            info.PreviousValue.ShouldBe("155");
            info.Change.ShouldBe("+25.2%");
            info.ChangeTone.ShouldBe(1);
            info.ShareLabel.ShouldBe("Fri share");
            terrain.CardView.Bars.Hour.ShouldBe(9);
            // The card sits 22px right of the bar's top and ends 16px above it (or is clamped 8px inside).
            var anchor = canvas.CardAnchor.ShouldNotBeNull();
            card.TargetPosition.X.ShouldBe(anchor.X + 22, 0.5);
            terrain.LiveRegion!.LastAnnouncement.ShouldBe("Friday, 09:00–10:00: 194 visits, +25.2% on the previous week.");

            terrain.HighlightedCell = null;
            Pump();
            canvas.LiftAt(cell.Index).ShouldBe(0);
            canvas.DimAt(new ActivityTerrainCell(1, 3).Index).ShouldBe(0);
            card.IsOpen.ShouldBeFalse();
            hovered[^1].ShouldBeNull();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Flying_In_Zooms_To_The_Bar_And_Escape_Returns()
    {
        var (window, terrain) = Show();
        try
        {
            var focused = new List<ActivityTerrainCell?>();
            terrain.CellFocused += (_, e) => focused.Add(e.Cell);
            var camera = Canvas(terrain).OrbitCamera;
            terrain.FlyTo(Peak);
            Pump();
            terrain.FocusedCell.ShouldBe(Peak);
            camera.TargetZoom.ShouldBe(1.5, 1e-9);
            var target = camera.TargetPoint;
            target.X.ShouldBe(2.5, 1e-9);
            target.Z.ShouldBe(-1, 1e-9);
            target.Y.ShouldBe(ActivityTerrainMath.MaxBarHeight / 2, 1e-9);
            camera.Yaw.ShouldBe(ActivityTerrainMath.Radians(32), 1e-9);
            Canvas(terrain).LiftAt(Peak.Index).ShouldBe(ActivityTerrainMath.Lift, 1e-9);
            terrain.IsViewDefault.ShouldBeFalse();
            TestHelpers.Descendant<PresenceHost>(terrain, ActivityTerrain.ResetPresencePart).IsPresent.ShouldBeTrue();
            var peakButton = terrain.GetVisualDescendants().OfType<ActivityTerrainPeakButton>().First();
            peakButton.IsChecked.ShouldBe(true);
            terrain.LiveRegion!.LastAnnouncement.ShouldBe("Looking closer at Wednesday, 14:00–15:00.");

            // A zoom of 2 is kept when flying to another bar.
            camera.ZoomBy(2 / 1.5);
            terrain.FlyTo(new ActivityTerrainCell(0, 0));
            camera.TargetZoom.ShouldBe(2, 1e-9);

            terrain.ShowWeek();
            terrain.FocusedCell.ShouldBeNull();
            camera.TargetZoom.ShouldBe(1, 1e-9);
            camera.TargetPoint.ShouldBe(ActivityTerrainMath.RestTarget);
            terrain.IsViewDefault.ShouldBeTrue();
            terrain.LiveRegion.LastAnnouncement.ShouldBe("Back to the whole week.");
            focused.ShouldBe([Peak, new ActivityTerrainCell(0, 0), null]);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Peak_Buttons_Toggle_The_Fly_In()
    {
        var (window, terrain) = Show();
        try
        {
            var button = terrain.GetVisualDescendants().OfType<ActivityTerrainPeakButton>().First();
            button.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Avalonia.Controls.Button.ClickEvent));
            terrain.FocusedCell.ShouldBe(Peak);
            terrain.HighlightedCell.ShouldBe(Peak);
            button.IsChecked.ShouldBe(true);
            button.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Avalonia.Controls.Button.ClickEvent));
            terrain.FocusedCell.ShouldBeNull();
            button.IsChecked.ShouldBe(false);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Keyboard_Model_Matches_The_Spec()
    {
        var (window, terrain) = Show();
        try
        {
            var canvas = Canvas(terrain);
            canvas.Focus(NavigationMethod.Tab);
            Dispatcher.UIThread.RunJobs();
            // The first arrow lands on the busiest hour without moving.
            Key(window, PhysicalKey.ArrowRight);
            terrain.HighlightedCell.ShouldBe(Peak);
            Key(window, PhysicalKey.ArrowRight);
            terrain.HighlightedCell.ShouldBe(new ActivityTerrainCell(2, 15));
            Key(window, PhysicalKey.ArrowUp);
            terrain.HighlightedCell.ShouldBe(new ActivityTerrainCell(1, 15));
            Key(window, PhysicalKey.ArrowDown);
            Key(window, PhysicalKey.ArrowDown);
            terrain.HighlightedCell.ShouldBe(new ActivityTerrainCell(3, 15));
            Key(window, PhysicalKey.End);
            terrain.HighlightedCell.ShouldBe(new ActivityTerrainCell(3, 23));
            // Edges stop (no wrap).
            Key(window, PhysicalKey.ArrowRight);
            terrain.HighlightedCell.ShouldBe(new ActivityTerrainCell(3, 23));
            Key(window, PhysicalKey.Home);
            terrain.HighlightedCell.ShouldBe(new ActivityTerrainCell(3, 0));
            Key(window, PhysicalKey.ArrowLeft);
            terrain.HighlightedCell.ShouldBe(new ActivityTerrainCell(3, 0));

            // Enter flies; Escape returns to the week, then clears the highlight, then is not handled.
            Key(window, PhysicalKey.Enter);
            terrain.FocusedCell.ShouldBe(new ActivityTerrainCell(3, 0));
            Key(window, PhysicalKey.Escape);
            terrain.FocusedCell.ShouldBeNull();
            terrain.HighlightedCell.ShouldBe(new ActivityTerrainCell(3, 0));
            Key(window, PhysicalKey.Escape);
            terrain.HighlightedCell.ShouldBeNull();
            var escape = new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Avalonia.Input.Key.Escape };
            canvas.RaiseEvent(escape);
            escape.Handled.ShouldBeFalse();

            // Shift + arrows orbit by 15° / 8°; + and − zoom by 1.25.
            var camera = canvas.OrbitCamera;
            Key(window, PhysicalKey.ArrowRight, RawInputModifiers.Shift);
            camera.TargetYaw.ShouldBe(ActivityTerrainMath.Radians(32 + 15), 1e-9);
            Key(window, PhysicalKey.ArrowLeft, RawInputModifiers.Shift);
            Key(window, PhysicalKey.ArrowLeft, RawInputModifiers.Shift);
            camera.TargetYaw.ShouldBe(ActivityTerrainMath.Radians(32 - 15), 1e-9);
            Key(window, PhysicalKey.ArrowUp, RawInputModifiers.Shift);
            camera.TargetPitch.ShouldBe(ActivityTerrainMath.Radians(38), 1e-9);
            for (var i = 0; i < 10; i++)
            {
                Key(window, PhysicalKey.ArrowUp, RawInputModifiers.Shift);
            }
            camera.TargetPitch.ShouldBe(ActivityTerrainMath.Radians(84), 1e-9);
            Key(window, PhysicalKey.Equal);
            camera.TargetZoom.ShouldBe(1.25, 1e-9);
            Key(window, PhysicalKey.Minus);
            Key(window, PhysicalKey.Minus);
            camera.TargetZoom.ShouldBe(0.8, 1e-9);
            terrain.IsViewDefault.ShouldBeFalse();
            terrain.ResetView();
            camera.TargetYaw.ShouldBe(ActivityTerrainMath.Radians(32), 1e-9);
            camera.TargetPitch.ShouldBe(ActivityTerrainMath.Radians(30), 1e-9);
            camera.TargetZoom.ShouldBe(1, 1e-9);
            terrain.IsViewDefault.ShouldBeTrue();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Hit_Testing_Finds_The_Bar_As_Drawn()
    {
        var (window, terrain) = Show();
        try
        {
            var canvas = Canvas(terrain);
            foreach (var cell in new[] { new ActivityTerrainCell(6, 2), new ActivityTerrainCell(6, 12), Peak, new ActivityTerrainCell(5, 20) })
            {
                var top = canvas.BarTopCenter(cell).ShouldNotBeNull();
                canvas.HitTestIndex(top).ShouldBe(cell.Index, cell.ToString());
            }
            canvas.HitTestIndex(new Point(4, 4)).ShouldBe(-1);
            // A lifted bar is hit where it is drawn (0.42 higher).
            var lifted = new ActivityTerrainCell(6, 2);
            var before = canvas.BarTopCenter(lifted)!.Value;
            terrain.HighlightedCell = lifted;
            var after = canvas.BarTopCenter(lifted)!.Value;
            after.Y.ShouldBeLessThan(before.Y - 5);
            canvas.HitTestIndex(after).ShouldBe(lifted.Index);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Hit_Testing_Allocates_Nothing()
    {
        var (window, terrain) = Show();
        try
        {
            var canvas = Canvas(terrain);
            var point = canvas.BarTopCenter(Peak)!.Value;
            for (var i = 0; i < 5; i++)
            {
                canvas.HitTestIndex(point);
            }
            // Best of three: a one-off runtime allocation (tiered JIT) must not fail the check.
            var least = long.MaxValue;
            for (var attempt = 0; attempt < 3 && least > 0; attempt++)
            {
                var before = GC.GetAllocatedBytesForCurrentThread();
                for (var i = 0; i < 50; i++)
                {
                    canvas.HitTestIndex(point + new Point(i % 7, i % 5));
                }
                least = Math.Min(least, GC.GetAllocatedBytesForCurrentThread() - before);
            }
            least.ShouldBe(0);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Clicking_A_Bar_Flies_In_And_Clicking_Empty_Space_Returns()
    {
        var (window, terrain) = Show();
        try
        {
            var canvas = Canvas(terrain);
            var cell = new ActivityTerrainCell(6, 4);
            var point = On(window, canvas, canvas.BarTopCenter(cell)!.Value);
            window.MouseMove(point);
            Dispatcher.UIThread.RunJobs();
            terrain.HighlightedCell.ShouldBe(cell);
            window.MouseDown(point, MouseButton.Left);
            window.MouseUp(point, MouseButton.Left);
            Dispatcher.UIThread.RunJobs();
            terrain.FocusedCell.ShouldBe(cell);
            canvas.IsFocused.ShouldBeTrue();

            var emptyPoint = Enumerable.Range(0, 60).Select(i => new Point(4 + i * 10, 4)).First(p => canvas.HitTestIndex(p) < 0);
            var empty = On(window, canvas, emptyPoint);
            window.MouseMove(empty);
            window.MouseDown(empty, MouseButton.Left);
            window.MouseUp(empty, MouseButton.Left);
            Dispatcher.UIThread.RunJobs();
            terrain.FocusedCell.ShouldBeNull();
            terrain.HighlightedCell.ShouldBeNull();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Dragging_Orbits_And_Reset_View_Brings_It_Back()
    {
        var (window, terrain) = Show();
        try
        {
            var canvas = Canvas(terrain);
            var start = On(window, canvas, new Point(10, 10));
            window.MouseDown(start, MouseButton.Left);
            window.MouseMove(start + new Point(10, 0), RawInputModifiers.LeftMouseButton);
            window.MouseMove(start + new Point(100, 36), RawInputModifiers.LeftMouseButton);
            Dispatcher.UIThread.RunJobs();
            canvas.IsDragging.ShouldBeTrue();
            // 1/180 rad per px in both axes; dragging right turns, dragging down looks more from above.
            terrain.Yaw.ShouldBeGreaterThan(32 + 90 / Math.PI - 2);
            terrain.Pitch.ShouldBeGreaterThan(30 + 26 / Math.PI - 2);
            terrain.IsViewDefault.ShouldBeFalse();
            window.MouseUp(start + new Point(100, 36), MouseButton.Left);
            Dispatcher.UIThread.RunJobs();
            TestHelpers.Descendant<PresenceHost>(terrain, ActivityTerrain.ResetPresencePart).IsPresent.ShouldBeTrue();
            var resets = 0;
            terrain.ViewReset += (_, _) => resets++;
            terrain.ResetViewCommand.Execute(null);
            terrain.Yaw.ShouldBe(32, 1e-6);
            terrain.Pitch.ShouldBe(30, 1e-6);
            terrain.IsViewDefault.ShouldBeTrue();
            resets.ShouldBe(1);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Heat_Map_Flattens_And_Refuses_Orbit_Drags()
    {
        var (window, terrain) = Show();
        try
        {
            var modes = new List<ActivityTerrainViewMode>();
            terrain.ViewModeChanged += (_, e) => modes.Add(e.ViewMode);
            terrain.SetViewMode(ActivityTerrainViewMode.Top);
            Pump();
            var canvas = Canvas(terrain);
            canvas.OrbitCamera.Pitch.ShouldBe(Math.PI / 2, 1e-9);
            canvas.OrbitCamera.Yaw.ShouldBe(0, 1e-9);
            canvas.OrbitCamera.Distance.ShouldBe(ActivityTerrainMath.TopDistance, 1e-9);
            canvas.HeightAt(Peak.Index).ShouldBe(ActivityTerrainMath.FlatHeight, 1e-9);
            terrain.Classes.Contains(":top-view").ShouldBeTrue();
            terrain.IsViewDefault.ShouldBeTrue(); // a mode, not a moved camera
            modes.ShouldBe([ActivityTerrainViewMode.Top]);
            TestHelpers.Descendant<SegmentedControl>(terrain, ActivityTerrain.ViewSwitchPart).SelectedIndex.ShouldBe(1);
            (canvas.CurrentFit.Scale).ShouldBe(21.46, 0.05);

            var start = On(window, canvas, new Point(10, 10));
            window.MouseDown(start, MouseButton.Left);
            window.MouseMove(start + new Point(80, 30), RawInputModifiers.LeftMouseButton);
            window.MouseUp(start + new Point(80, 30), MouseButton.Left);
            Dispatcher.UIThread.RunJobs();
            canvas.OrbitCamera.Yaw.ShouldBe(0, 1e-9);

            // Squares are hit, the gaps between them are not.
            var center = canvas.BarTopCenter(Peak)!.Value;
            canvas.HitTestIndex(center).ShouldBe(Peak.Index);
            var gap = canvas.BarTopCenter(new ActivityTerrainCell(2, 15))!.Value;
            canvas.HitTestIndex(new Point((center.X + gap.X) / 2, center.Y)).ShouldBe(-1);

            terrain.ViewMode = ActivityTerrainViewMode.Perspective;
            canvas.OrbitCamera.Pitch.ShouldBe(ActivityTerrainMath.Radians(30), 1e-9);
            canvas.HeightAt(Peak.Index).ShouldBe(ActivityTerrainMath.MaxBarHeight, 1e-9);
            terrain.LiveRegion!.LastAnnouncement.ShouldBe("Terrain: bar height shows how busy each hour was.");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Narrow_And_Wide_Containers_Change_The_Layout()
    {
        var (window, terrain) = Show(width: 400);
        try
        {
            terrain.Classes.Contains(":narrow").ShouldBeTrue();
            TestHelpers.Descendant<Panel>(terrain, ActivityTerrain.PlotPart).Bounds.Height.ShouldBe(280);
            Canvas(terrain).IsNarrow.ShouldBeTrue();
            terrain.Yaw.ShouldBe(50, 1e-6);
            TestHelpers.Descendant<ActivityTerrainStatsPanel>(terrain, ActivityTerrain.StatsPart).Layout.ShouldBe(ActivityTerrainStatsLayout.Narrow);
            var view = TestHelpers.Descendant<SegmentedControl>(terrain, ActivityTerrain.ViewSwitchPart);
            var metric = TestHelpers.Descendant<SegmentedControl>(terrain, ActivityTerrain.MetricSwitchPart);
            view.Bounds.Top.ShouldBeGreaterThan(metric.Bounds.Bottom);

            terrain.Width = 1000;
            window.Width = 1040;
            Pump();
            terrain.Classes.Contains(":narrow").ShouldBeFalse();
            terrain.Classes.Contains(":wide").ShouldBeTrue();
            terrain.Yaw.ShouldBe(32, 1e-6);
            TestHelpers.Descendant<Panel>(terrain, ActivityTerrain.PlotPart).Bounds.Height.ShouldBe(0.44 * 998, 1);
            var stats = TestHelpers.Descendant<Border>(terrain, "PART_StatsFrame");
            stats.Bounds.Width.ShouldBe(224, 0.5);
            TestHelpers.Descendant<ActivityTerrainStatsPanel>(terrain, ActivityTerrain.StatsPart).Layout.ShouldBe(ActivityTerrainStatsLayout.Column);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Intro_Swings_Down_From_Above_Then_Frames_Stop()
    {
        var clock = new ManualFrameClock();
        using var _ = clock.Install();
        var (window, terrain) = Show(level: ArcMotionLevel.Full);
        try
        {
            var canvas = Canvas(terrain);
            var camera = canvas.OrbitCamera;
            canvas.IsIntroPending.ShouldBeFalse();
            camera.Pitch.ShouldBe(ActivityTerrainMath.Radians(76), 0.02);
            camera.Zoom.ShouldBe(0.53, 0.01);
            canvas.HeightAt(Peak.Index).ShouldBe(0, 1e-9);
            clock.Advance(TimeSpan.FromMilliseconds(600));
            // About half way (50% at 603ms).
            ActivityTerrainMath.RestPitchDegrees.ShouldBeLessThan(terrain.Pitch);
            terrain.Pitch.ShouldBe((76 + 30) / 2.0, 5);
            clock.Advance(TimeSpan.FromSeconds(4));
            terrain.Pitch.ShouldBe(30, 0.01);
            terrain.Zoom.ShouldBe(1, 1e-3);
            canvas.HeightAt(Peak.Index).ShouldBe(ActivityTerrainMath.MaxBarHeight, 1e-3);
            canvas.IsLoopRunning.ShouldBeFalse();
            var requests = clock.RequestCount;
            clock.Advance(TimeSpan.FromMilliseconds(500));
            clock.RequestCount.ShouldBe(requests);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Metric_Switch_Rises_In_A_Diagonal_Wave()
    {
        var clock = new ManualFrameClock();
        using var _ = clock.Install();
        var (window, terrain) = Show(level: ArcMotionLevel.Full, setup: t =>
        {
            // Builds: a different shape (falls with the hour).
            var builds = t.Metrics[1];
            var values = new double[168];
            for (var i = 0; i < 168; i++)
            {
                values[i] = 400 - 10 * (i % 24);
            }
            builds.Values = values;
        });
        try
        {
            clock.Advance(TimeSpan.FromSeconds(4));
            var canvas = Canvas(terrain);
            var first = new ActivityTerrainCell(0, 0).Index;
            var last = new ActivityTerrainCell(6, 23).Index;
            var lastBefore = canvas.HeightAt(last);
            var firstBefore = canvas.HeightAt(first);
            terrain.SelectedMetricKey = "builds";
            canvas.DelayedCount.ShouldBe(167);
            clock.Advance(TimeSpan.FromMilliseconds(150));
            canvas.HeightAt(first).ShouldNotBe(firstBefore);
            canvas.HeightAt(last).ShouldBe(lastBefore, 1e-9);   // starts after ≈ 320ms
            clock.Advance(TimeSpan.FromMilliseconds(1500));
            canvas.DelayedCount.ShouldBe(0);
            canvas.HeightAt(first).ShouldBe(ActivityTerrainMath.MaxBarHeight, 1e-3);
            canvas.HeightAt(last).ShouldBe(ActivityTerrainMath.BarHeight(170, 400), 1e-3);
            terrain.LiveRegion!.LastAnnouncement!.ShouldStartWith("Showing Builds:");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Hover_Springs_Run_Then_Stop_With_Full_Motion()
    {
        var clock = new ManualFrameClock();
        using var _ = clock.Install();
        var (window, terrain) = Show(level: ArcMotionLevel.Full);
        try
        {
            clock.Advance(TimeSpan.FromSeconds(4));
            var canvas = Canvas(terrain);
            terrain.HighlightedCell = Peak;
            canvas.IsLoopRunning.ShouldBeTrue();
            clock.Advance(TimeSpan.FromMilliseconds(108));
            canvas.LiftAt(Peak.Index).ShouldBe(ActivityTerrainMath.Lift / 2, 0.06);
            clock.Advance(TimeSpan.FromSeconds(1));
            canvas.LiftAt(Peak.Index).ShouldBe(ActivityTerrainMath.Lift, 1e-3);
            canvas.DimAt(new ActivityTerrainCell(0, 0).Index).ShouldBe(1, 1e-3);
            canvas.IsLoopRunning.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Reduced_Motion_Skips_The_Intro_And_Lands_Camera_Moves()
    {
        var (window, terrain) = Show(level: ArcMotionLevel.Reduced);
        try
        {
            var camera = Canvas(terrain).OrbitCamera;
            terrain.Pitch.ShouldBe(30, 1e-6);
            Canvas(terrain).HeightAt(Peak.Index).ShouldBe(ActivityTerrainMath.MaxBarHeight, 1e-9);
            terrain.FlyTo(Peak);
            camera.Zoom.ShouldBe(1.5, 1e-9);
            camera.IsMoving.ShouldBeFalse();
            terrain.HighlightedCell = Peak;
            Canvas(terrain).LiftAt(Peak.Index).ShouldBe(ActivityTerrainMath.Lift, 1e-9);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Screen_Readers_Get_A_Summary_A_Table_And_Announcements()
    {
        var (window, terrain) = Show();
        try
        {
            var canvas = Canvas(terrain);
            var peer = ControlAutomationPeer.CreatePeerForElement(canvas);
            peer.GetName().ShouldStartWith("Visits by hour for the week:");
            peer.GetName().ShouldContain("Busiest hour Wednesday 14:00");
            peer.GetHelpText().ShouldBe(terrain.KeyboardHintText);
            AutomationProperties.GetLiveSetting(terrain.LiveRegion!).ShouldBe(AutomationLiveSetting.Polite);

            var table = ControlAutomationPeer.CreatePeerForElement(terrain.DataTable!);
            table.GetAutomationControlType().ShouldBe(AutomationControlType.Table);
            table.GetName().ShouldBe("Visits per hour, Monday to Sunday");
            var rows = table.GetChildren();
            rows.Count.ShouldBe(7);
            rows[2].GetName().ShouldBe("Wednesday");
            var cells = rows[2].GetChildren();
            cells.Count.ShouldBe(25);
            cells[0].GetAutomationControlType().ShouldBe(AutomationControlType.HeaderItem);
            cells[15].GetName().ShouldBe("14:00: 1,000 visits");
            cells[15].GetParent().ShouldBe(rows[2]);

            terrain.SelectedMetricKey = "builds";
            ControlAutomationPeer.CreatePeerForElement(terrain.DataTable!).GetName().ShouldBe("Builds per hour, Monday to Sunday");
            var reset = TestHelpers.Descendant<ActivityTerrainResetButton>(terrain);
            AutomationProperties.GetName(reset).ShouldBe("Reset view");
            AutomationProperties.GetName(TestHelpers.Descendant<ActivityTerrainStatsPanel>(terrain)).ShouldBe("Summary of the week");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Empty_And_All_Zero_Data_Do_Not_Break()
    {
        var terrain = new ActivityTerrain { Width = 665 };
        var window = TestHelpers.Show(terrain);
        try
        {
            window.Width = 700;
            window.Height = 900;
            Pump();
            terrain.Classes.Contains(":empty").ShouldBeTrue();
            terrain.CurrentMetric.ShouldBeNull();
            Canvas(terrain).HitTestIndex(new Point(300, 150)).ShouldBe(-1);
            terrain.Metrics.Add(new ActivityTerrainMetric { Label = "Nothing", Values = new double[168] });
            Pump();
            terrain.Classes.Contains(":empty").ShouldBeTrue();
            terrain.Classes.Contains(":no-comparison").ShouldBeTrue();
            terrain.SelectedMetricKey.ShouldBe("Nothing");
            terrain.HighlightedCell = new ActivityTerrainCell(1, 1);
            terrain.FlyTo(new ActivityTerrainCell(9, 1));
            terrain.FocusedCell.ShouldBeNull();
            Pump();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Commands_And_Custom_Day_Names()
    {
        var (window, terrain) = Show(setup: t =>
        {
            t.DayNames = ["Montag", "Dienstag", "Mittwoch", "Donnerstag", "Freitag", "Samstag", "Sonntag"];
            t.DayAbbreviations = ["Mo", "Di", "Mi", "Do", "Fr", "Sa", "So"];
        });
        try
        {
            terrain.FlyToCommand.CanExecute(Peak).ShouldBeTrue();
            terrain.FlyToCommand.CanExecute("x").ShouldBeFalse();
            terrain.FlyToCommand.Execute(Peak);
            terrain.FocusedCell.ShouldBe(Peak);
            terrain.GetVisualDescendants().OfType<ActivityTerrainPeakButton>().First().Label.ShouldBe("Mi 14:00");
            TestHelpers.Descendant<ArcContentSwap>(terrain, ActivityTerrain.BusiestDayPart).Content.ShouldBe("Mittwoch");
            terrain.ResetViewCommand.Execute(null);
            terrain.FocusedCell.ShouldBeNull();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public async Task Theme_Change_Reresolves_The_Plot_Colors()
    {
        var (window, terrain) = Show();
        try
        {
            Pump();
            var swatch = TestHelpers.Descendant<Panel>(terrain, ActivityTerrain.SwatchesPart).Children.OfType<Border>().First();
            var light = TestHelpers.ColorOf(swatch.Background);
            await TestHelpers.ApplyAsync(new ThemeConfigBuilder().WithAlgorithms(ThemeAlgorithm.Dark).Build());
            Pump();
            var dark = TestHelpers.ColorOf(swatch.Background);
            dark.ShouldNotBe(light);
            // Dark scale starts dark (L ≈ 0.33) and ends light: bigger values are brighter.
            ActivityTerrainOklab.FromColor(dark).L.ShouldBe(0.33, 0.02);
            RenderPlots(window);
        }
        finally
        {
            window.Close();
            await TestHelpers.ApplyAsync(null);
        }
    }

    [AvaloniaTheory]
    [InlineData("Preview")]
    [InlineData("HeatMap")]
    [InlineData("Incidents")]
    [InlineData("Narrow")]
    [InlineData("LiveData")]
    public async Task Demos_Render_In_Light_And_Dark(string name)
    {
        Control demo = name switch
        {
            "HeatMap" => new ActivityTerrainHeatMapDemo(),
            "Incidents" => new ActivityTerrainIncidentsDemo(),
            "Narrow" => new ActivityTerrainNarrowDemo(),
            "LiveData" => new ActivityTerrainLiveDataDemo(),
            _ => new ActivityTerrainPreviewDemo()
        };
        var window = TestHelpers.Show(demo);
        try
        {
            window.Width = 760;
            window.Height = 1000;
            Pump();
            var terrain = demo.GetVisualDescendants().OfType<ActivityTerrain>().First();
            terrain.CurrentMetric.ShouldNotBeNull();
            terrain.HighlightedCell = terrain.Summary.Peak;
            Pump();
            RenderPlots(window);
            await TestHelpers.ApplyAsync(new ThemeConfigBuilder().WithAlgorithms(ThemeAlgorithm.Dark).Build());
            Pump();
            RenderPlots(window);
        }
        finally
        {
            window.Close();
            await TestHelpers.ApplyAsync(null);
        }
    }
}
