using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;

namespace TKCode.Arc.Tests.AdvancedCharts;

public class RadarChartTests
{
    private static RadarAxis[] Axes() =>
    [
        new("a", "Alpha"),
        new("b", "Beta"),
        new("c", "Gamma"),
        new("d", "Delta")
    ];

    private static RadarSeries Profile(string key, string label, double a, double b, double c, double d) =>
        new(key, label, new Dictionary<string, double> { ["a"] = a, ["b"] = b, ["c"] = c, ["d"] = d });

    private static RadarChart Chart() => new()
    {
        Label = "Skills",
        Axes = Axes(),
        Series = [Profile("one", "One", 50, 100, 25, 75), Profile("two", "Two", 10, 20, 30, 40)]
    };

    private static void With(RadarChart chart, Action<Window> check, double width = 600, double height = 600)
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

    private static void ShouldBeAt(Point actual, double x, double y)
    {
        actual.X.ShouldBe(x, 0.001);
        actual.Y.ShouldBe(y, 0.001);
    }

    [AvaloniaFact]
    public void Vertices_Sit_Clockwise_From_The_Top_At_Their_Share_Of_The_Radius()
    {
        var chart = Chart();
        With(chart, _ =>
        {
            var c = chart.WebCenter;
            var r = chart.WebRadius;
            r.ShouldBeGreaterThan(0);
            ShouldBeAt(chart.VertexOf("one", "a"), c.X, c.Y - r * 0.5);
            ShouldBeAt(chart.VertexOf("one", "b"), c.X + r, c.Y);
            ShouldBeAt(chart.VertexOf("one", "c"), c.X, c.Y + r * 0.25);
            ShouldBeAt(chart.VertexOf("one", "d"), c.X - r * 0.75, c.Y);
        });
    }

    [AvaloniaFact]
    public void An_Axis_Maximum_Wins_And_Values_Above_It_Clamp_To_The_Rim()
    {
        var chart = Chart();
        chart.Maximum = 200;
        chart.Axes = [new RadarAxis("a", "Alpha", 10), new RadarAxis("b", "Beta"), new RadarAxis("c", "Gamma")];
        chart.Series = [new RadarSeries("one", "One", new Dictionary<string, double> { ["a"] = 5, ["b"] = 100, ["c"] = 900 })];
        With(chart, _ =>
        {
            var c = chart.WebCenter;
            var r = chart.WebRadius;
            // 5 of the axis' own 10 and 100 of the chart's 200.
            ShouldBeAt(chart.VertexOf("one", "a"), c.X, c.Y - r * 0.5);
            var b = chart.VertexOf("one", "b");
            Math.Sqrt(Math.Pow(b.X - c.X, 2) + Math.Pow(b.Y - c.Y, 2)).ShouldBe(r * 0.5, 0.001);
            var far = chart.VertexOf("one", "c");
            Math.Sqrt(Math.Pow(far.X - c.X, 2) + Math.Pow(far.Y - c.Y, 2)).ShouldBe(r, 0.001);
            // The reading stays the value that was given.
            chart.ActiveAxis = "c";
            chart.TooltipLines.ShouldBe(["Gamma", "One 900"]);
        });
    }

    [AvaloniaFact]
    public void Missing_And_NaN_Values_Draw_At_The_Centre_And_Read_As_A_Dash()
    {
        var chart = Chart();
        chart.Series =
        [
            new RadarSeries("gap", "Gap", new Dictionary<string, double> { ["a"] = double.NaN, ["b"] = 50 })
        ];
        With(chart, _ =>
        {
            var c = chart.WebCenter;
            ShouldBeAt(chart.VertexOf("gap", "a"), c.X, c.Y);
            ShouldBeAt(chart.VertexOf("gap", "c"), c.X, c.Y);
            chart.ActiveAxis = "a";
            chart.TooltipLines.ShouldBe(["Alpha", "Gap —"]);
            chart.Reading.ShouldBe("Alpha: Gap no value");
            chart.ActiveAxis = "b";
            chart.TooltipLines.ShouldBe(["Beta", "Gap 50"]);
        });
    }

    [AvaloniaFact]
    public void Fewer_Than_Three_Axes_Or_No_Series_Show_The_Empty_Label()
    {
        var chart = new RadarChart { Label = "Nothing", Axes = [new RadarAxis("a", "A"), new RadarAxis("b", "B")], Series = [Profile("one", "One", 1, 2, 3, 4)] };
        With(chart, window =>
        {
            chart.IsShowingEmptyLabel.ShouldBeTrue();
            chart.Focusable.ShouldBeFalse();
            chart.Legend.IsVisible.ShouldBeFalse();
            chart.Summary.ShouldBe("Nothing. No data.");
            chart.EmptyLabel = "Nothing to compare";
            chart.Summary.ShouldBe("Nothing. Nothing to compare.");

            chart.Axes = Axes();
            chart.IsShowingEmptyLabel.ShouldBeFalse();
            chart.Focusable.ShouldBeTrue();
            Dispatcher.UIThread.RunJobs();
            chart.Legend.IsVisible.ShouldBeTrue();

            chart.Series = [];
            chart.IsShowingEmptyLabel.ShouldBeTrue();
            chart.ActiveAxis = "a";
            chart.Reading.ShouldBeNull();
        });
    }

    [AvaloniaFact]
    public void Keyboard_Tab_Reads_The_First_Axis_Then_Walks_The_Legend()
    {
        var chart = Chart();
        var button = new Button();
        var host = new StackPanel { Children = { button, chart } };
        var window = TestHelpers.Show(host);
        try
        {
            button.Focus(NavigationMethod.Tab);
            window.KeyPressQwerty(PhysicalKey.Tab, RawInputModifiers.None);
            chart.IsFocused.ShouldBeTrue();
            chart.ActiveAxis.ShouldBe("a");
            window.KeyPressQwerty(PhysicalKey.Tab, RawInputModifiers.None);
            var first = (RadarChartLegendItem)chart.Legend.Children[0];
            first.IsFocused.ShouldBeTrue();
            chart.ActiveAxis.ShouldBeNull();
            chart.ForwardSeries.ShouldBe("one");
            window.KeyPressQwerty(PhysicalKey.ArrowRight, RawInputModifiers.None);
            chart.ActiveAxis.ShouldBeNull();
            window.KeyPressQwerty(PhysicalKey.Tab, RawInputModifiers.None);
            ((RadarChartLegendItem)chart.Legend.Children[1]).IsFocused.ShouldBeTrue();
            chart.ForwardSeries.ShouldBe("two");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Keyboard_Arrows_Move_Clockwise_And_Wrap_And_Escape_Clears()
    {
        var chart = Chart();
        var seen = new List<string?>();
        chart.ActiveAxisChanged += (_, e) => seen.Add(e.Key);
        With(chart, window =>
        {
            chart.Focus(NavigationMethod.Tab);
            chart.ActiveAxis.ShouldBe("a");
            window.KeyPressQwerty(PhysicalKey.ArrowRight, RawInputModifiers.None);
            chart.ActiveAxis.ShouldBe("b");
            window.KeyPressQwerty(PhysicalKey.ArrowDown, RawInputModifiers.None);
            chart.ActiveAxis.ShouldBe("c");
            window.KeyPressQwerty(PhysicalKey.ArrowLeft, RawInputModifiers.None);
            chart.ActiveAxis.ShouldBe("b");
            window.KeyPressQwerty(PhysicalKey.ArrowUp, RawInputModifiers.None);
            chart.ActiveAxis.ShouldBe("a");
            window.KeyPressQwerty(PhysicalKey.ArrowUp, RawInputModifiers.None);
            chart.ActiveAxis.ShouldBe("d");
            window.KeyPressQwerty(PhysicalKey.ArrowRight, RawInputModifiers.None);
            chart.ActiveAxis.ShouldBe("a");
            window.KeyPressQwerty(PhysicalKey.End, RawInputModifiers.None);
            chart.ActiveAxis.ShouldBe("d");
            window.KeyPressQwerty(PhysicalKey.Home, RawInputModifiers.None);
            chart.ActiveAxis.ShouldBe("a");
            window.KeyPressQwerty(PhysicalKey.Escape, RawInputModifiers.None);
            chart.ActiveAxis.ShouldBeNull();
            seen.ShouldBe(["a", "b", "c", "b", "a", "d", "a", "d", "a", null]);
        });
    }

    [AvaloniaFact]
    public void The_Event_Reports_The_Axis_And_A_Cleared_Reading()
    {
        var chart = Chart();
        var args = new List<RadarChartActiveAxisChangedEventArgs>();
        chart.ActiveAxisChanged += (_, e) => args.Add(e);
        With(chart, _ =>
        {
            chart.ActiveAxis = "b";
            chart.ActiveAxis = "missing";
            chart.ActiveAxis = null;
            args.Count.ShouldBe(3);
            args[0].Key.ShouldBe("b");
            args[0].Axis!.Label.ShouldBe("Beta");
            args[1].Key.ShouldBe("missing");
            args[1].Axis.ShouldBeNull();
            args[2].Key.ShouldBeNull();
            args[2].Axis.ShouldBeNull();
        });
    }

    [AvaloniaFact]
    public void The_Grid_Is_A_Circle_By_Default_And_A_Polygon_On_Request()
    {
        var chart = Chart();
        chart.GridShape.ShouldBe(RadarGridShape.Circle);
        chart.Rings.ShouldBe(4);
        chart.Maximum.ShouldBe(100);
        chart.Size.ShouldBe(320);
        chart.FillOpacity.ShouldBeInRange(0.01, 0.5);
        chart.GridShape = RadarGridShape.Polygon;
        chart.Rings = 6;
        With(chart, _ =>
        {
            // Four axes make a diamond whose corners sit on the rim.
            var bounds = chart.OuterPolygonBounds();
            bounds.Width.ShouldBe(chart.WebRadius * 2, 0.001);
            bounds.Height.ShouldBe(chart.WebRadius * 2, 0.001);
            bounds.Center.X.ShouldBe(chart.WebCenter.X, 0.001);
            bounds.Center.Y.ShouldBe(chart.WebCenter.Y, 0.001);
        });
    }

    [AvaloniaFact]
    public void The_Formatter_Gets_The_Value_And_Its_Axis_And_Skips_Missing_Values()
    {
        var chart = Chart();
        chart.Series = [new RadarSeries("one", "One", new Dictionary<string, double> { ["a"] = 42 })];
        var calls = new List<(double, string)>();
        chart.ValueFormatter = (value, axis) =>
        {
            calls.Add((value, axis.Key));
            return $"{value} pts on {axis.Label}";
        };
        With(chart, _ =>
        {
            calls.Clear();
            chart.ActiveAxis = "a";
            chart.TooltipLines.ShouldBe(["Alpha", "One 42 pts on Alpha"]);
            calls.ShouldContain((42.0, "a"));
            calls.Clear();
            chart.ActiveAxis = "b";
            chart.TooltipLines.ShouldBe(["Beta", "One —"]);
            calls.ShouldBeEmpty();
        });
    }

    [AvaloniaFact]
    public void The_Legend_Lists_Every_Profile_With_A_Named_Line_And_Can_Hide()
    {
        var chart = Chart();
        With(chart, _ =>
        {
            chart.Legend.IsVisible.ShouldBeTrue();
            var items = chart.Legend.Children.Cast<RadarChartLegendItem>().ToArray();
            items.Select(i => i.Text).ShouldBe(["One", "Two"]);
            AutomationProperties.GetName(items[0]).ShouldBe("One, solid line");
            AutomationProperties.GetName(items[1]).ShouldBe("Two, dashed line");
            items[1].Focus(NavigationMethod.Tab);
            chart.ForwardSeries.ShouldBe("two");
            chart.Focus();
            chart.ForwardSeries.ShouldBeNull();
            var withLegend = chart.WebRadius;
            chart.IsLegendVisible = false;
            Dispatcher.UIThread.RunJobs();
            chart.Legend.IsVisible.ShouldBeFalse();
            chart.WebRadius.ShouldBeGreaterThanOrEqualTo(withLegend);
        });
    }

    [AvaloniaFact]
    public void More_Series_Than_Hues_Cycle_Without_Failing()
    {
        var chart = Chart();
        chart.Series = Enumerable.Range(0, 9).Select(i => Profile($"s{i}", $"S{i}", i, i, i, i)).ToArray();
        With(chart, _ =>
        {
            chart.ActiveAxis = "a";
            chart.TooltipLines.Count.ShouldBe(10);
            chart.Legend.Children.Count.ShouldBe(9);
        });
    }

    [AvaloniaFact]
    public void Accessibility_Announces_The_Axis_And_Every_Profile()
    {
        var chart = Chart();
        With(chart, _ =>
        {
            AutomationProperties.GetName(chart).ShouldStartWith("Skills.");
            chart.Summary.ShouldBe("Skills. One: Alpha 50, Beta 100, Gamma 25, Delta 75; Two: Alpha 10, Beta 20, Gamma 30, Delta 40.");
            chart.ActiveAxis = "b";
            AutomationProperties.GetName(chart).ShouldBe("Beta: One 100, Two 20");
            chart.ActiveAxis = null;
            AutomationProperties.GetName(chart).ShouldStartWith("Skills.");
        });
    }

    [AvaloniaFact]
    public void The_Tooltip_Stays_Inside_The_Chart_On_Every_Axis()
    {
        foreach (var width in new[] { 240.0, 330, 520, 900 })
        {
            var chart = Chart();
            With(chart, _ =>
            {
                foreach (var axis in new[] { "a", "b", "c", "d" })
                {
                    chart.ActiveAxis = axis;
                    chart.IsTooltipOpen.ShouldBeTrue();
                    var tip = chart.TooltipRect;
                    tip.X.ShouldBeGreaterThanOrEqualTo(0);
                    tip.Y.ShouldBeGreaterThanOrEqualTo(0);
                    tip.Right.ShouldBeLessThanOrEqualTo(chart.Bounds.Width + 0.001);
                    tip.Bottom.ShouldBeLessThanOrEqualTo(chart.Bounds.Height + 0.001);
                }
                chart.ActiveAxis = null;
                chart.IsTooltipOpen.ShouldBeFalse();
            }, width);
        }
    }

    [AvaloniaFact]
    public void The_Tooltip_Does_Not_Cover_The_Label_It_Reads()
    {
        foreach (var width in new[] { 320.0, 640, 900 })
        {
            var chart = Chart();
            With(chart, _ =>
            {
                for (var i = 0; i < 4; i++)
                {
                    chart.ActiveAxis = new[] { "a", "b", "c", "d" }[i];
                    chart.TooltipRect.Intersects(chart.LabelBox(i)).ShouldBeFalse($"axis {i} at width {width}");
                }
            }, width, 700);
        }
    }

    [AvaloniaFact]
    public void The_Web_Shrinks_To_Fit_And_Never_Passes_Size()
    {
        var wide = Chart();
        With(wide, _ => wide.WebRadius.ShouldBe(160, 0.001), 900, 900);

        var small = Chart();
        small.Size = 200;
        With(small, _ => small.WebRadius.ShouldBe(100, 0.001), 900, 900);

        var narrow = Chart();
        With(narrow, _ =>
        {
            narrow.WebRadius.ShouldBeLessThan(160);
            for (var i = 0; i < 4; i++)
            {
                var box = narrow.LabelBox(i);
                box.X.ShouldBeGreaterThanOrEqualTo(0);
                box.Right.ShouldBeLessThanOrEqualTo(narrow.Bounds.Width + 0.001);
            }
        }, 300, 900);

        var low = Chart();
        With(low, _ => (low.Bounds.Height + 0.001).ShouldBeGreaterThanOrEqualTo(low.WebRadius * 2), 900, 260);
        low.WebRadius.ShouldBeLessThan(160);
    }

    [AvaloniaFact]
    public void Below_360_The_Label_Ring_Tightens_And_Labels_Wrap()
    {
        var chart = Chart();
        chart.Axes = [new RadarAxis("a", "Alpha"), new RadarAxis("b", "A very long label for an axis"), new RadarAxis("c", "Gamma")];
        With(chart, _ =>
        {
            chart.IsCompact.ShouldBeFalse();
            chart.CurrentLabelGap.ShouldBe(12);
            chart.LabelLineCount(1).ShouldBe(1);
        }, 600);

        var compact = Chart();
        compact.Axes = [new RadarAxis("a", "Alpha"), new RadarAxis("b", "A very long label for an axis"), new RadarAxis("c", "Gamma")];
        With(compact, _ =>
        {
            compact.IsCompact.ShouldBeTrue();
            compact.CurrentLabelGap.ShouldBe(6);
            compact.LabelLineCount(1).ShouldBe(2);
        }, 320);
    }

    [AvaloniaFact]
    public void A_Replaced_Profile_Ends_On_Its_New_Values_And_Keeps_Its_Place()
    {
        var chart = Chart();
        With(chart, _ =>
        {
            var c = chart.WebCenter;
            var r = chart.WebRadius;
            chart.Series = [Profile("one", "One", 50, 100, 25, 75), Profile("three", "Three", 100, 0, 0, 0)];
            chart.IsMorphing.ShouldBeFalse();
            ShouldBeAt(chart.VertexOf("three", "a"), c.X, c.Y - r);
            chart.Legend.Children.Cast<RadarChartLegendItem>().Select(i => i.Text).ShouldBe(["One", "Three"]);
        });
    }

    [AvaloniaFact]
    public void With_Full_Motion_A_Replaced_Profile_Starts_From_The_Old_Shape_And_Settles_On_The_New()
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
            Pump(() => chart.IsLoaded && chart.IsRevealed).ShouldBeTrue();
            var before = chart.VertexOf("two", "a");
            chart.Series = [Profile("one", "One", 50, 100, 25, 75), Profile("three", "Three", 100, 0, 0, 0)];
            chart.IsMorphing.ShouldBeTrue();
            ShouldBeAt(chart.VertexOf("three", "a"), before.X, before.Y);
            Pump(() => !chart.IsMorphing).ShouldBeTrue();
            ShouldBeAt(chart.VertexOf("three", "a"), chart.WebCenter.X, chart.WebCenter.Y - chart.WebRadius);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void An_Observable_List_Is_Followed()
    {
        var series = new Avalonia.Collections.AvaloniaList<RadarSeries> { Profile("one", "One", 50, 50, 50, 50) };
        var chart = Chart();
        chart.Series = series;
        With(chart, _ =>
        {
            chart.Legend.Children.Count.ShouldBe(1);
            series.Add(Profile("two", "Two", 1, 2, 3, 4));
            chart.Legend.Children.Count.ShouldBe(2);
            series.RemoveAt(0);
            chart.Legend.Children.Cast<RadarChartLegendItem>().Single().Text.ShouldBe("Two");
        });
    }

    [AvaloniaFact]
    public void Inline_Xaml_Items_Load()
    {
        const string xaml = """
            <arc:RadarChart xmlns="https://github.com/avaloniaui" xmlns:arc="https://github.com/AIDotNet/TKCode.Arc" Label="Routes">
                <arc:RadarChart.Axes>
                    <arc:RadarAxis Key="x" Label="X" Maximum="10" />
                    <arc:RadarAxis Key="y" Label="Y" />
                    <arc:RadarAxis Key="z" Label="Z" />
                </arc:RadarChart.Axes>
                <arc:RadarChart.Series>
                    <arc:RadarSeries Key="one" Label="One">
                        <arc:RadarValue Axis="x" Value="5" />
                        <arc:RadarValue Axis="y" Value="50" />
                    </arc:RadarSeries>
                </arc:RadarChart.Series>
            </arc:RadarChart>
            """;
        var chart = (RadarChart)AvaloniaRuntimeXamlLoader.Parse(xaml, typeof(RadarChart).Assembly);
        With(chart, _ =>
        {
            chart.IsShowingEmptyLabel.ShouldBeFalse();
            chart.Axes!.Count.ShouldBe(3);
            chart.Axes![0].Maximum.ShouldBe(10);
            ShouldBeAt(chart.VertexOf("one", "x"), chart.WebCenter.X, chart.WebCenter.Y - chart.WebRadius * 0.5);
        });
    }

    [AvaloniaFact]
    public void Pointer_Reads_The_Nearest_Axis_And_Leaving_The_Web_Clears_It()
    {
        var chart = Chart();
        With(chart, window =>
        {
            var c = chart.TranslatePoint(chart.WebCenter, window)!.Value;
            var r = chart.WebRadius;
            window.MouseMove(new Point(c.X + r * 0.6, c.Y), RawInputModifiers.None);
            chart.ActiveAxis.ShouldBe("b");
            window.MouseMove(new Point(c.X, c.Y + r * 0.6), RawInputModifiers.None);
            chart.ActiveAxis.ShouldBe("c");
            window.MouseMove(new Point(1, 1), RawInputModifiers.None);
            chart.ActiveAxis.ShouldBeNull();
        });
    }

    [AvaloniaFact]
    public void Renders_With_Every_Option_In_Both_Grid_Shapes()
    {
        foreach (var shape in new[] { RadarGridShape.Circle, RadarGridShape.Polygon })
        {
            var chart = Chart();
            chart.GridShape = shape;
            With(chart, window =>
            {
                chart.ActiveAxis = "a";
                chart.Focus(NavigationMethod.Tab);
                Dispatcher.UIThread.RunJobs();
                AvaloniaHeadlessPlatform.ForceRenderTimerTick();
                Dispatcher.UIThread.RunJobs();
                chart.IsFocused.ShouldBeTrue();
            });
        }
    }
}
