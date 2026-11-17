using Avalonia;
using Avalonia.Automation;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.Threading;

namespace TKCode.Arc.Tests.AdvancedCharts;

public class WindRoseChartTests
{
    /// <summary>Eight sectors, two bins: north 1 + 1, east 2 + 2, nothing else. Six in all, so east is 66.7 % on a 0–80 % axis.</summary>
    private static WindRoseChart Chart(int sectors = 8) => new()
    {
        Label = "Pier",
        SectorCount = sectors,
        Bins = [new WindRoseBin(0, "Light"), new WindRoseBin(5, "Strong")],
        ItemsSource = new List<WindRoseRow> { new("N", 1, 1), new(90, 2, 2) }
    };

    private static void With(WindRoseChart chart, Action<Window> check, double width = 600, double height = 600)
    {
        var window = TestHelpers.Show(chart);
        window.Width = width;
        window.Height = height;
        Dispatcher.UIThread.RunJobs();
        try
        {
            check(window);
        }
        finally
        {
            window.Close();
        }
    }

    private static Point InWindow(WindRoseChart chart, Window window, Point point) => chart.TranslatePoint(point, window)!.Value;

    private static void ShouldBeAt(Point actual, double x, double y)
    {
        actual.X.ShouldBe(x, 0.001);
        actual.Y.ShouldBe(y, 0.001);
    }

    [AvaloniaFact]
    public void Sectors_Report_Their_Labels_Angles_Amounts_And_Shares()
    {
        var chart = Chart();
        var sectors = chart.Sectors;
        sectors.Count.ShouldBe(8);
        sectors.Select(s => s.Label).ShouldBe(["N", "NE", "E", "SE", "S", "SW", "W", "NW"]);
        sectors[0].Direction.ShouldBe(0);
        sectors[0].From.ShouldBe(337.5);
        sectors[0].To.ShouldBe(22.5);
        sectors[2].Values.ShouldBe([2.0, 2.0]);
        sectors[2].Total.ShouldBe(4);
        sectors[2].Share.ShouldBe(4 / 6.0, 1e-9);
        chart.BinLabels.ShouldBe(["Light", "Strong"]);
    }

    [AvaloniaFact]
    public void Percent_Is_Of_All_Observations_With_Calm_And_Count_Shows_The_Amounts()
    {
        var chart = Chart();
        chart.Calm = 2;
        With(chart, _ =>
        {
            // Eight in all with calm: east is 4 of 8.
            chart.Model.SectorStacks[2].ShouldBe(50, 1e-9);
            chart.Model.CalmDisplay.ShouldBe(25, 1e-9);
            chart.ActiveIndex = 2;
            chart.TooltipLines.ShouldBe(["E 50.0%", "Light 25.0%", "Strong 25.0%"]);
            chart.RingValues.ShouldBe([10.0, 20.0, 30.0, 40.0, 50.0]);

            chart.RadialAxis = WindRoseRadialAxis.Count;
            chart.Model.SectorStacks[2].ShouldBe(4);
            chart.TooltipLines.ShouldBe(["E 4", "Light 2", "Strong 2"]);
            chart.RingValues.ShouldBe([1.0, 2.0, 3.0, 4.0]);
            chart.Summary.ShouldStartWith("Pier. 8 directions, Calm 2. N 2, NE 0, E 4");
        });
    }

    [AvaloniaFact]
    public void A_Fixed_Maximum_And_Step_Set_The_Rings_And_Petals_Above_It_Stop_At_The_Rim()
    {
        var chart = Chart();
        chart.RadialMaximum = 40;
        chart.RadialStep = 10;
        With(chart, _ =>
        {
            chart.RingValues.ShouldBe([10.0, 20.0, 30.0, 40.0]);
            // East reaches 66.7 % on a 40 % axis: drawn to the outer ring, read as given.
            chart.RadiusOf(chart.Model.SectorStacks[2], chart.Model.Maximum).ShouldBe(chart.RoseRadius, 0.001);
            chart.ActiveIndex = 2;
            chart.TooltipLines[0].ShouldBe("E 66.7%");
        });
    }

    [AvaloniaFact]
    public void Sector_Centres_Follow_The_Count_The_Start_Angle_And_The_Direction()
    {
        foreach (var count in new[] { 8, 16, 32 })
        {
            var chart = Chart(count);
            for (var i = 0; i < count; i++)
            {
                chart.CenterTurn(i).ShouldBe((double)i / count, 1e-12);
            }
        }
        var turned = Chart(16);
        turned.StartAngle = 90;
        turned.CenterTurn(0).ShouldBe(0.25, 1e-12);
        turned.CenterTurn(1).ShouldBe(0.25 + 1 / 16.0, 1e-12);
        turned.SweepDirection = SweepDirection.CounterClockwise;
        turned.CenterTurn(1).ShouldBe(0.25 - 1 / 16.0, 1e-12);
    }

    [AvaloniaFact]
    public void Petals_Point_From_The_Centre_Toward_Their_Screen_Angle()
    {
        var chart = Chart();
        With(chart, _ =>
        {
            var c = chart.RoseCenter;
            var r = chart.RoseRadius;
            r.ShouldBeGreaterThan(40);
            ShouldBeAt(chart.PointIn(0, 1), c.X, c.Y - r);
            ShouldBeAt(chart.PointIn(2, 1), c.X + r, c.Y);
            ShouldBeAt(chart.PointIn(4, 1), c.X, c.Y + r);
            chart.StartAngle = 90;
            ShouldBeAt(chart.PointIn(0, 1), c.X + r, c.Y);
            chart.StartAngle = 0;
            chart.SweepDirection = SweepDirection.CounterClockwise;
            ShouldBeAt(chart.PointIn(2, 1), c.X - r, c.Y);
            // Labels sit outside the outer ring on their sector's ray.
            Dispatcher.UIThread.RunJobs();
            chart.LabelBox(2).Right.ShouldBeLessThan(c.X - r);
            chart.LabelBox(6).Left.ShouldBeGreaterThan(c.X + r);
        });
    }

    [AvaloniaFact]
    public void Hit_Testing_Finds_The_Sector_And_The_Band_Under_The_Point()
    {
        var chart = Chart();
        With(chart, _ =>
        {
            // East: light up to 33.3 %, strong up to 66.7 %, on a 0–80 % axis.
            chart.HitTest(chart.PointIn(2, 0.2)).ShouldBe((2, 0));
            chart.HitTest(chart.PointIn(2, 0.6)).ShouldBe((2, 1));
            chart.HitTest(chart.PointIn(2, 0.95)).ShouldBe((2, -1));
            chart.HitTest(chart.PointIn(3, 0.5)).ShouldBe((3, -1));
            chart.HitTest(chart.RoseCenter).ShouldBe((-1, -1));
            chart.HitTest(new Point(1, 1)).ShouldBe((-1, -1));
            // The label belongs to its sector too.
            chart.HitTest(chart.LabelBox(6).Center).ShouldBe((6, -1));
            chart.StartAngle = 90;
            chart.HitTest(chart.PointIn(0, 0.1)).ShouldBe((0, 0));
            chart.SweepDirection = SweepDirection.CounterClockwise;
            chart.HitTest(chart.PointIn(2, 0.2)).ShouldBe((2, 0));
        });
    }

    [AvaloniaFact]
    public void The_Pointer_Reads_A_Sector_Highlights_Its_Band_And_Leaving_Clears_It()
    {
        var chart = Chart();
        With(chart, window =>
        {
            window.MouseMove(InWindow(chart, window, chart.PointIn(2, 0.6)), RawInputModifiers.None);
            chart.ActiveIndex.ShouldBe(2);
            chart.ActiveBin.ShouldBe(1);
            chart.IsBinLit(1).ShouldBeTrue();
            chart.IsTooltipOpen.ShouldBeTrue();
            window.MouseMove(InWindow(chart, window, chart.PointIn(0, 0.1)), RawInputModifiers.None);
            chart.ActiveIndex.ShouldBe(0);
            chart.ActiveBin.ShouldBe(0);
            window.MouseMove(new Point(1, 1), RawInputModifiers.None);
            chart.ActiveIndex.ShouldBe(-1);
            chart.IsTooltipOpen.ShouldBeFalse();
        });
    }

    [AvaloniaFact]
    public void A_Click_Invokes_The_Sector_With_Its_Band_And_Selects_Only_When_Enabled()
    {
        var chart = Chart();
        var invoked = new List<(int Index, string Label, int Bin)>();
        var changes = new List<WindRoseSelectionChangedEventArgs>();
        chart.ItemInvoked += (_, e) => invoked.Add((e.Index, e.Item.Label, e.Bin));
        chart.SelectionChanged += (_, e) => changes.Add(e);
        With(chart, window =>
        {
            var at = InWindow(chart, window, chart.PointIn(2, 0.6));
            window.MouseMove(at, RawInputModifiers.None);
            window.MouseDown(at, MouseButton.Left);
            window.MouseUp(at, MouseButton.Left);
            invoked.ShouldBe([(2, "E", 1)]);
            chart.SelectedIndex.ShouldBe(-1);
            changes.ShouldBeEmpty();

            chart.IsSelectionEnabled = true;
            window.MouseDown(at, MouseButton.Left);
            window.MouseUp(at, MouseButton.Left);
            chart.SelectedIndex.ShouldBe(2);
            changes.Count.ShouldBe(1);
            changes[0].OldIndex.ShouldBe(-1);
            changes[0].NewIndex.ShouldBe(2);
            changes[0].OldItem.ShouldBeNull();
            changes[0].NewItem!.Label.ShouldBe("E");

            // Choosing the selected sector again invokes it without a selection change.
            window.MouseDown(at, MouseButton.Left);
            window.MouseUp(at, MouseButton.Left);
            invoked.Count.ShouldBe(3);
            changes.Count.ShouldBe(1);

            // Code changes report too.
            chart.SelectedIndex = 0;
            changes.Count.ShouldBe(2);
            changes[1].OldItem!.Label.ShouldBe("E");
            changes[1].NewItem!.Label.ShouldBe("N");
        });
    }

    [AvaloniaFact]
    public void Keyboard_Reads_The_Sectors_In_Screen_Order_And_Chooses_With_Enter()
    {
        var chart = Chart();
        var invoked = new List<(int Index, int Bin)>();
        chart.ItemInvoked += (_, e) => invoked.Add((e.Index, e.Bin));
        With(chart, window =>
        {
            window.KeyPressQwerty(PhysicalKey.Tab, RawInputModifiers.None);
            chart.IsFocused.ShouldBeTrue();
            chart.ActiveIndex.ShouldBe(0);
            window.KeyPressQwerty(PhysicalKey.ArrowRight, RawInputModifiers.None);
            chart.ActiveIndex.ShouldBe(1);
            window.KeyPressQwerty(PhysicalKey.ArrowDown, RawInputModifiers.None);
            chart.ActiveIndex.ShouldBe(2);
            window.KeyPressQwerty(PhysicalKey.ArrowLeft, RawInputModifiers.None);
            window.KeyPressQwerty(PhysicalKey.ArrowUp, RawInputModifiers.None);
            window.KeyPressQwerty(PhysicalKey.ArrowUp, RawInputModifiers.None);
            chart.ActiveIndex.ShouldBe(7);
            window.KeyPressQwerty(PhysicalKey.Home, RawInputModifiers.None);
            chart.ActiveIndex.ShouldBe(0);
            window.KeyPressQwerty(PhysicalKey.End, RawInputModifiers.None);
            chart.ActiveIndex.ShouldBe(7);
            window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
            invoked.ShouldBe([(7, -1)]);
            chart.SelectedIndex.ShouldBe(-1);

            chart.IsSelectionEnabled = true;
            window.KeyPressQwerty(PhysicalKey.Space, RawInputModifiers.None);
            chart.SelectedIndex.ShouldBe(7);
            window.KeyPressQwerty(PhysicalKey.Escape, RawInputModifiers.None);
            chart.ActiveIndex.ShouldBe(-1);
            chart.SelectedIndex.ShouldBe(7);
            window.KeyPressQwerty(PhysicalKey.Escape, RawInputModifiers.None);
            chart.SelectedIndex.ShouldBe(-1);

            // Counter-clockwise sectors: Right still moves clockwise on screen, which is back one sector.
            chart.SweepDirection = SweepDirection.CounterClockwise;
            window.KeyPressQwerty(PhysicalKey.Home, RawInputModifiers.None);
            window.KeyPressQwerty(PhysicalKey.ArrowRight, RawInputModifiers.None);
            chart.ActiveIndex.ShouldBe(7);
        });
    }

    [AvaloniaFact]
    public void Tab_Starts_At_The_Selected_Sector_And_Then_Walks_The_Legend()
    {
        var chart = Chart();
        chart.SelectedIndex = 2;
        With(chart, window =>
        {
            window.KeyPressQwerty(PhysicalKey.Tab, RawInputModifiers.None);
            chart.ActiveIndex.ShouldBe(2);
            window.KeyPressQwerty(PhysicalKey.Tab, RawInputModifiers.None);
            chart.ActiveIndex.ShouldBe(-1);
            chart.Legend.Children[0].IsFocused.ShouldBeTrue();
            chart.HighlightedBin.ShouldBe(0);
            window.KeyPressQwerty(PhysicalKey.Tab, RawInputModifiers.None);
            chart.HighlightedBin.ShouldBe(1);
            chart.IsBinLit(1).ShouldBeTrue();
            chart.IsBinLit(0).ShouldBeFalse();
        });
    }

    [AvaloniaFact]
    public void Readings_Name_The_Sector_And_Its_Bands_For_Assistive_Technology()
    {
        var chart = Chart();
        With(chart, _ =>
        {
            AutomationProperties.GetName(chart).ShouldStartWith("Pier. Arrow keys move between the directions");
            chart.ActiveIndex = 2;
            chart.Reading.ShouldBe("E: 66.7%. Light 33.3%, Strong 33.3%");
            AutomationProperties.GetName(chart).ShouldBe(chart.Reading);
            chart.ActiveIndex = 3;
            chart.Reading.ShouldBe("SE: 0.0%");
            chart.SelectedIndex = 3;
            chart.Reading.ShouldBe("SE: 0.0%, selected");
            chart.ActiveIndex = 99;
            chart.Reading.ShouldBeNull();
            chart.IsTooltipOpen.ShouldBeFalse();
        });
    }

    [AvaloniaFact]
    public void The_Legend_Lists_Every_Bin_With_Its_Range_And_Unit()
    {
        var chart = new WindRoseChart { ItemsSource = new List<WindRoseRow> { new(0, 1, 2, 3, 4, 5, 6) } };
        With(chart, _ =>
        {
            chart.Legend.Children.Cast<WindRoseLegendItem>().Select(i => i.Text)
                 .ShouldBe(["0.5–2 m/s", "2–4 m/s", "4–6 m/s", "6–8 m/s", "8–10 m/s", "≥10 m/s"]);
            AutomationProperties.GetName(chart.Legend.Children[5]).ShouldBe("≥10 m/s, 28.6% of observations");
            chart.Unit = "kn";
            chart.Legend.Children.Cast<WindRoseLegendItem>().First().Text.ShouldBe("0.5–2 kn");
            chart.Unit = "";
            chart.Legend.Children.Cast<WindRoseLegendItem>().Last().Text.ShouldBe("≥10");
            chart.IsLegendVisible = false;
            Dispatcher.UIThread.RunJobs();
            chart.Legend.IsVisible.ShouldBeFalse();
        });
    }

    [AvaloniaFact]
    public void Hovering_A_Legend_Entry_Highlights_Its_Bin()
    {
        var chart = Chart();
        With(chart, window =>
        {
            var entry = chart.Legend.Children[1];
            var center = entry.TranslatePoint(new Point(entry.Bounds.Width / 2, entry.Bounds.Height / 2), window)!.Value;
            window.MouseMove(center, RawInputModifiers.None);
            chart.HighlightedBin.ShouldBe(1);
            window.MouseMove(new Point(1, 1), RawInputModifiers.None);
            chart.HighlightedBin.ShouldBe(-1);
        });
    }

    [AvaloniaFact]
    public void Custom_Labels_Replace_The_Defaults_And_Missing_Ones_Fall_Back()
    {
        var chart = Chart(4);
        chart.DirectionLabels = ["Nord", "", "Süd"];
        chart.Labels.ShouldBe(["Nord", "E", "Süd", "W"]);
        // Data can name a sector by its custom label.
        chart.ItemsSource = new List<WindRoseRow> { new("Süd", 5, 0) };
        chart.Sectors[2].Total.ShouldBe(5);
        chart.SectorCount = 36;
        chart.Labels[1].ShouldBe("10°");
    }

    [AvaloniaFact]
    public void Records_And_Rows_Mix_And_Duplicates_Add_Up()
    {
        var chart = Chart(4);
        chart.ItemsSource = new object[]
        {
            new WindRoseRecord("N", 0, 1),
            new WindRoseRecord(0.0, 1, 2),
            new WindRoseRow("N", 3, 4),
            new WindRoseRecord("E", 7, 100),      // no such bin
            new WindRoseRecord("nowhere", 0, 100),
            new WindRoseRecord(180, 0, double.NaN)
        };
        chart.Sectors[0].Values.ShouldBe([4.0, 6.0]);
        chart.Sectors[1].Total.ShouldBe(0);
        chart.Sectors[2].Total.ShouldBe(0);
    }

    public sealed record Reading(string Heading, int Band, double Count);

    public sealed record Spread(double Heading, double[] Counts);

    [AvaloniaFact]
    public void Member_Bindings_Read_Any_Objects_In_Long_Or_Wide_Form()
    {
        var chart = Chart(4);
        chart.DirectionMemberBinding = new Binding(nameof(Reading.Heading));
        chart.BinMemberBinding = new Binding(nameof(Reading.Band));
        chart.ValueMemberBinding = new Binding(nameof(Reading.Count));
        chart.ItemsSource = new[] { new Reading("N", 0, 3), new Reading("NNE", 1, 2), new Reading("W", 1, 5) };
        chart.Sectors[0].Values.ShouldBe([3.0, 2.0]);
        chart.Sectors[3].Values.ShouldBe([0.0, 5.0]);

        var wide = Chart(4);
        wide.DirectionMemberBinding = new Binding(nameof(Spread.Heading));
        wide.ValuesMemberBinding = new Binding(nameof(Spread.Counts));
        wide.ItemsSource = new[] { new Spread(90, [1, 2, 99]), new Spread(270, [4]) };
        wide.Sectors[1].Values.ShouldBe([1.0, 2.0]);
        wide.Sectors[3].Values.ShouldBe([4.0, 0.0]);
    }

    [AvaloniaFact]
    public void The_Binning_Helper_Feeds_The_Chart()
    {
        var observations = Enumerable.Range(0, 360).Select(i => new WindObservation(i, i % 12 * 0.5)).ToArray();
        var data = WindRoseBinning.Bin(observations, 16);
        var chart = new WindRoseChart { ItemsSource = data.Rows, Calm = data.Calm };
        chart.Model.Total.ShouldBe(360);
        chart.Model.Calm.ShouldBe(data.Calm);
        chart.Sectors.Sum(s => s.Total).ShouldBe(360 - data.Calm);
    }

    [AvaloniaFact]
    public void Collection_Changes_Are_Followed_And_A_Burst_Rebuilds_Once()
    {
        var rows = new AvaloniaList<WindRoseRow> { new("N", 1, 1) };
        var chart = Chart();
        chart.ItemsSource = rows;
        With(chart, _ =>
        {
            chart.Sectors[0].Total.ShouldBe(2);
            rows.Add(new WindRoseRow("S", 3, 0));
            Dispatcher.UIThread.RunJobs();
            chart.Sectors[4].Total.ShouldBe(3);
            rows[0] = new WindRoseRow("N", 5, 5);
            Dispatcher.UIThread.RunJobs();
            chart.Sectors[0].Total.ShouldBe(10);

            var before = chart.RebuildCount;
            rows.Clear();
            for (var i = 0; i < 8; i++)
            {
                rows.Add(new WindRoseRow(i * 45.0, 1, 0));
            }
            chart.IsShowingEmptyLabel.ShouldBeFalse();
            Dispatcher.UIThread.RunJobs();
            chart.RebuildCount.ShouldBe(before + 1);
            chart.Sectors.Select(s => s.Total).ShouldAllBe(t => t == 1);

            // Replacing the source reads it at once.
            chart.ItemsSource = new List<WindRoseRow> { new("W", 2, 2) };
            chart.Sectors[6].Total.ShouldBe(4);
            chart.Sectors[0].Total.ShouldBe(0);
        });
    }

    [AvaloniaFact]
    public void With_Full_Motion_New_Data_Springs_From_What_Is_On_Screen()
    {
        var chart = Chart();
        var window = TestHelpers.Show(chart, TKCode.Arc.Motion.ArcMotionLevel.Full);
        try
        {
            bool Pump(Func<bool> done) => TestHelpers.PumpUntil(() =>
            {
                AvaloniaHeadlessPlatform.ForceRenderTimerTick();
                return done();
            });
            Pump(() => chart.IsLoaded && chart.IsRevealed && !chart.IsMorphing).ShouldBeTrue();
            chart.CurrentTop(2, 1).ShouldBe(chart.Model.Top[2 * 2 + 1], 1e-6);
            var before = chart.CurrentTop(2, 1);
            chart.ItemsSource = new List<WindRoseRow> { new("N", 1, 1), new(90, 6, 6) };
            chart.IsMorphing.ShouldBeTrue();
            chart.CurrentTop(2, 1).ShouldBe(before, 1e-6);
            Pump(() => !chart.IsMorphing).ShouldBeTrue();
            chart.CurrentTop(2, 1).ShouldBe(chart.Model.Top[2 * 2 + 1], 1e-6);

            // Sixteen sectors: ENE (67.5°) and E (90°) start from the old east petal, then ENE shrinks away.
            var east = chart.CurrentTop(2, 1);
            east.ShouldBeGreaterThan(50);
            chart.SectorCount = 16;
            chart.CurrentTop(3, 1).ShouldBe(east, 1e-6);
            chart.CurrentTop(4, 1).ShouldBe(east, 1e-6);
            Pump(() => !chart.IsMorphing).ShouldBeTrue();
            chart.CurrentTop(3, 1).ShouldBe(0, 1e-6);
            chart.CurrentTop(4, 1).ShouldBe(chart.Model.Top[4 * 2 + 1], 1e-6);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Without_Motion_The_Bands_Are_At_Their_Values_At_Once()
    {
        var chart = Chart();
        With(chart, _ =>
        {
            chart.IsRevealed.ShouldBeTrue();
            chart.IsMorphing.ShouldBeFalse();
            chart.CurrentTop(2, 1).ShouldBe(chart.Model.Top[5], 1e-9);
            chart.ItemsSource = new List<WindRoseRow> { new(90, 1, 0) };
            chart.IsMorphing.ShouldBeFalse();
            chart.CurrentTop(2, 0).ShouldBe(chart.Model.Top[4], 1e-9);
        });
    }

    [AvaloniaFact]
    public void Inline_Xaml_Rows_And_Bins_Load()
    {
        const string xaml = """
            <arc:WindRoseChart xmlns="https://github.com/avaloniaui" xmlns:arc="https://github.com/AIDotNet/TKCode.Arc"
                               Label="Pier" SectorCount="4" Calm="2" Unit="kn">
                <arc:WindRoseChart.Bins>
                    <arc:WindRoseBin Minimum="0" Label="Breeze" Color="#FF8800" />
                    <arc:WindRoseBin Minimum="11" />
                </arc:WindRoseChart.Bins>
                <arc:WindRoseRow Direction="N" Values="1 2" />
                <arc:WindRoseRow Direction="90" Values="3,4" />
            </arc:WindRoseChart>
            """;
        var chart = (WindRoseChart)AvaloniaRuntimeXamlLoader.Parse(xaml, typeof(WindRoseChart).Assembly);
        With(chart, _ =>
        {
            chart.Sectors[0].Values.ShouldBe([1.0, 2.0]);
            chart.Sectors[1].Values.ShouldBe([3.0, 4.0]);
            chart.Model.Total.ShouldBe(12);
            chart.BinLabels.ShouldBe(["Breeze", "≥11 kn"]);
            chart.BinColor(0).ShouldBe(Color.Parse("#FF8800"));
        });
    }

    [AvaloniaFact]
    public void The_Default_Ramp_Orders_Bins_By_Lightness_In_Both_Themes()
    {
        var chart = new WindRoseChart { ItemsSource = new List<WindRoseRow> { new(0, 1, 1, 1, 1, 1, 1) } };
        With(chart, _ =>
        {
            var lightness = Enumerable.Range(0, 6).Select(k => Oklch.FromColor(chart.BinColor(k)).L).ToArray();
            // Light surface: from a pale tint to a deep shade.
            for (var k = 1; k < lightness.Length; k++)
            {
                lightness[k].ShouldBeLessThan(lightness[k - 1]);
            }
        });
    }

    [AvaloniaFact]
    public void Small_Widths_Thin_The_Labels_But_Keep_The_Cardinal_Points()
    {
        var chart = Chart(32);
        With(chart, _ =>
        {
            chart.IsCompact.ShouldBeTrue();
            chart.LabelStep.ShouldBeGreaterThan(1);
            foreach (var cardinal in new[] { 0, 8, 16, 24 })
            {
                chart.IsLabelShown(cardinal).ShouldBeTrue();
            }
            chart.IsLabelShown(1).ShouldBeFalse();
            // The sector being read always shows its label.
            chart.ActiveIndex = 1;
            chart.IsLabelShown(1).ShouldBeTrue();
            chart.RoseRadius.ShouldBeLessThanOrEqualTo(130);
        }, width: 260, height: 500);
    }

    [AvaloniaFact]
    public void The_Rose_Fits_Its_Box_And_Never_Passes_Size()
    {
        var chart = Chart(16);
        chart.Size = 200;
        With(chart, _ =>
        {
            chart.RoseRadius.ShouldBe(100, 0.001);
            for (var i = 0; i < 16; i++)
            {
                var box = chart.LabelBox(i);
                box.Left.ShouldBeGreaterThanOrEqualTo(-0.001);
                box.Right.ShouldBeLessThanOrEqualTo(chart.Bounds.Width + 0.001);
                box.Top.ShouldBeGreaterThanOrEqualTo(-0.001);
            }
        });
    }

    [AvaloniaFact]
    public void Nothing_To_Draw_Shows_The_Empty_Label_And_Calm_Alone_Draws()
    {
        var chart = new WindRoseChart { Label = "Quiet" };
        With(chart, _ =>
        {
            chart.IsShowingEmptyLabel.ShouldBeTrue();
            chart.Focusable.ShouldBeFalse();
            chart.Legend.IsVisible.ShouldBeFalse();
            chart.Summary.ShouldBe("Quiet. No data.");
            chart.EmptyLabel = "Nothing yet";
            chart.Summary.ShouldBe("Quiet. Nothing yet.");
            chart.Calm = 4;
            chart.IsShowingEmptyLabel.ShouldBeFalse();
            chart.Focusable.ShouldBeTrue();
            chart.Model.CalmDisplay.ShouldBe(100);
        });
    }

    [AvaloniaFact]
    public void A_Shrinking_Sector_Count_Clears_An_Index_Past_The_End()
    {
        var chart = Chart(16);
        var changes = new List<WindRoseSelectionChangedEventArgs>();
        chart.SelectionChanged += (_, e) => changes.Add(e);
        chart.SelectedIndex = 12;
        chart.ActiveIndex = 14;
        chart.SectorCount = 8;
        chart.SelectedIndex.ShouldBe(-1);
        chart.ActiveIndex.ShouldBe(-1);
        changes.Select(c => c.NewIndex).ShouldBe([12, -1]);
        chart.SectorCount = 2;
        chart.SectorCount.ShouldBe(4);
    }

    [AvaloniaFact]
    public void Renders_With_Every_Option()
    {
        var chart = Chart(32);
        chart.Calm = 1;
        chart.IsSelectionEnabled = true;
        chart.SelectedIndex = 8;
        chart.StartAngle = 45;
        chart.SweepDirection = SweepDirection.CounterClockwise;
        chart.ValueFormatter = v => $"{v:0}‰";
        With(chart, window =>
        {
            chart.ActiveIndex = 8;
            chart.Focus(NavigationMethod.Tab);
            Dispatcher.UIThread.RunJobs();
            AvaloniaHeadlessPlatform.ForceRenderTimerTick();
            Dispatcher.UIThread.RunJobs();
            chart.IsFocused.ShouldBeTrue();
            chart.TooltipLines[0].ShouldEndWith("‰");
        });
    }
}
