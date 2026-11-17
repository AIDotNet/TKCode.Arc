using TKCode.Arc.DesignTokens;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace TKCode.Arc.Tests.AdvancedCharts;

public class SunburstTests
{
    private static SunburstNode Costs() => new("all", "Costs", children:
    [
        new SunburstNode("people", "People", children:
        [
            new SunburstNode("crew", "Crew", children: [new SunburstNode("deck", "Deck", 30), new SunburstNode("engine", "Engine", 30)]),
            new SunburstNode("shore", "Shore", 20)
        ]),
        new SunburstNode("fleet", "Fleet", children:
        [
            new SunburstNode("fuel", "Fuel", 15),
            new SunburstNode("parts", "Parts", 5)
        ])
    ]);

    private static Sunburst Chart(SunburstNode? root = null) => new() { Label = "Costs", Root = root ?? Costs(), Size = 300 };

    private static void Run(Sunburst chart, Action<Window> act, double width = 800, double height = 600)
    {
        var window = TestHelpers.Show(chart);
        window.Width = width;
        window.Height = height;
        Dispatcher.UIThread.RunJobs();
        try
        {
            act(window);
        }
        finally
        {
            window.Close();
        }
    }

    private static SunburstEntry At(Sunburst chart, string id) => chart.Tree.ById[id];

    private static Point WindowPoint(Sunburst chart, Window window, string id)
        => chart.Stage.TranslatePoint(chart.Stage.PointOf(At(chart, id)), window)!.Value;

    private static void Click(Window window, Point point)
    {
        window.MouseMove(point);
        window.MouseDown(point, MouseButton.Left);
        window.MouseUp(point, MouseButton.Left);
        Dispatcher.UIThread.RunJobs();
    }

    private static void Key(Window window, PhysicalKey key)
    {
        window.KeyPressQwerty(key, RawInputModifiers.None);
        Dispatcher.UIThread.RunJobs();
    }

    // ---- tree and layout maths ---------------------------------------------------------------------------------

    [AvaloniaFact]
    public void Branches_Sum_Their_Children_And_Ignore_Their_Own_Value()
    {
        var root = new SunburstNode("r", "R", children:
        [
            new SunburstNode("a", "A", 999, [new SunburstNode("a1", "A1", 2), new SunburstNode("a2", "A2", 3)]),
            new SunburstNode("b", "B", 5)
        ]);
        var tree = SunburstTree.Build(root);
        tree.ById["a"].Value.ShouldBe(5);
        tree.Root.Value.ShouldBe(10);
        tree.ById["a1"].Path.ShouldBe(["A", "A1"]);
        tree.ById["a1"].Depth.ShouldBe(2);
        tree.Root.Height.ShouldBe(2);
    }

    [AvaloniaFact]
    public void Bad_Values_Count_As_Zero_And_Duplicate_Ids_Are_Skipped()
    {
        var root = new SunburstNode("r", "R", children:
        [
            new SunburstNode("nan", "NaN", double.NaN),
            new SunburstNode("neg", "Negative", -4),
            new SunburstNode("none", "Missing"),
            new SunburstNode("ok", "Fine", 6),
            new SunburstNode("ok", "Again", 100),
            new SunburstNode { Label = "No id", Value = 2 },
            new SunburstNode { Label = "No id either", Value = 2 }
        ]);
        var tree = SunburstTree.Build(root);
        tree.Root.Value.ShouldBe(10);
        tree.Root.Children.Count.ShouldBe(6);
        tree.ById["r/5"].Label.ShouldBe("No id");
        tree.ById["r/6"].Label.ShouldBe("No id either");
    }

    [AvaloniaFact]
    public void Angles_Of_A_Ring_Sum_To_The_Whole_Circle_In_Proportion()
    {
        var tree = SunburstTree.Build(Costs());
        var output = new double[tree.Nodes.Count * SunburstLayout.Slots];
        SunburstLayout.Compute(tree, tree.Root, 3, 0.3, output);
        double Sweep(string id)
        {
            var at = tree.ById[id].Index * SunburstLayout.Slots;
            return output[at + 1] - output[at];
        }
        (Sweep("people") + Sweep("fleet")).ShouldBe(1, 1e-9);
        Sweep("people").ShouldBe(0.8, 1e-9);
        Sweep("crew").ShouldBe(0.6, 1e-9);
        Sweep("deck").ShouldBe(0.3, 1e-9);
        output[tree.ById["people"].Index * SunburstLayout.Slots].ShouldBe(0, 1e-9);
        output[tree.ById["fleet"].Index * SunburstLayout.Slots + 1].ShouldBe(1, 1e-9);
    }

    [AvaloniaFact]
    public void Zooming_Spreads_The_Centre_And_Closes_Everything_Else()
    {
        var tree = SunburstTree.Build(Costs());
        var output = new double[tree.Nodes.Count * SunburstLayout.Slots];
        SunburstLayout.Compute(tree, tree.ById["people"], 3, 0.3, output);
        var crew = tree.ById["crew"].Index * SunburstLayout.Slots;
        (output[crew + 1] - output[crew]).ShouldBe(0.75, 1e-9);
        output[crew + 4].ShouldBe(1);
        var fleet = tree.ById["fleet"].Index * SunburstLayout.Slots;
        (output[fleet + 1] - output[fleet]).ShouldBe(0, 1e-9);
        output[fleet + 4].ShouldBe(0);
        // People itself leaves through the hole.
        var people = tree.ById["people"].Index * SunburstLayout.Slots;
        output[people + 2].ShouldBe(0.3, 1e-9);
        output[people + 3].ShouldBe(0.3, 1e-9);
        output[people + 4].ShouldBe(0);
    }

    [AvaloniaFact]
    public void Rings_Grow_When_The_Centre_Has_Fewer_Levels_Than_Depth()
    {
        var tree = SunburstTree.Build(Costs());
        SunburstLayout.Rings(tree.Root, 3).ShouldBe(3);
        SunburstLayout.Rings(tree.Root, 2).ShouldBe(2);
        SunburstLayout.Rings(tree.ById["fleet"], 3).ShouldBe(1);
        var output = new double[tree.Nodes.Count * SunburstLayout.Slots];
        SunburstLayout.Compute(tree, tree.ById["fleet"], 3, 0.4, output);
        var fuel = tree.ById["fuel"].Index * SunburstLayout.Slots;
        output[fuel + 2].ShouldBe(0.4, 1e-9);
        output[fuel + 3].ShouldBe(1, 1e-9);
    }

    [AvaloniaFact]
    public void Labels_Fit_Along_The_Arc_Across_The_Ring_Or_Not_At_All()
    {
        SunburstLayout.Fit(40, 14, 60, 100, 0.25).ShouldBe(SunburstLabelFit.Tangent);
        SunburstLayout.Fit(30, 14, 60, 100, 0.05).ShouldBe(SunburstLabelFit.Radial);
        SunburstLayout.Fit(60, 14, 60, 100, 0.05).ShouldBe(SunburstLabelFit.None);
        SunburstLayout.Fit(30, 14, 60, 100, 0.002).ShouldBe(SunburstLabelFit.None);
    }

    [AvaloniaTheory]
    [InlineData("$1,284K", "$", 1284, 0, true, "K")]
    [InlineData("1284", "", 1284, 0, false, "")]
    [InlineData("12.5 GB", "", 12.5, 1, false, " GB")]
    [InlineData("0", "", 0, 0, false, "")]
    [InlineData("1.234,5", "", 1.234, 3, false, ",5")]
    public void Formatted_Values_Split_Around_Their_Number(string text, string prefix, double value, int decimals, bool grouped, string suffix)
    {
        SunburstLayout.TryParseNumber(text, out var p, out var v, out var d, out var g, out var s).ShouldBeTrue();
        p.ShouldBe(prefix);
        v.ShouldBe(value, 1e-9);
        d.ShouldBe(decimals);
        g.ShouldBe(grouped);
        s.ShouldBe(suffix);
    }

    [AvaloniaTheory]
    [InlineData("n/a")]
    [InlineData("007")]
    [InlineData("1,2345")]
    public void Text_That_Cannot_Read_Back_From_A_Number_Does_Not_Roll(string text)
        => SunburstLayout.TryParseNumber(text, out _, out _, out _, out _, out _).ShouldBeFalse();

    // ---- theme ---------------------------------------------------------------------------------------------------

    [AvaloniaFact]
    public void Theme_Supplies_Every_Token()
    {
        var chart = Chart();
        Run(chart, _ =>
        {
            chart.TryFindResource(SunburstTokenKind.HoleRatio, chart.ActualThemeVariant, out var hole).ShouldBeTrue();
            hole.ShouldBe(0.3);
            foreach (var kind in Enum.GetValues<SunburstTokenKind>())
            {
                chart.TryFindResource(kind, chart.ActualThemeVariant, out var value).ShouldBeTrue(kind.ToString());
                value.ShouldNotBeNull();
            }
            // The value comes from the theme's setter, not the registered default.
            chart.IsSet(Sunburst.HoleRatioProperty).ShouldBeTrue();
            chart.GetValue(Sunburst.LegendRowHeightProperty).ShouldBe(36);
        });
    }

    // ---- focus -----------------------------------------------------------------------------------------------------

    [AvaloniaFact]
    public void Starts_At_The_Root_With_Its_Trail_Total_And_Legend()
    {
        var chart = Chart();
        Run(chart, _ =>
        {
            chart.Center.Id.ShouldBe("all");
            chart.FocusedId.ShouldBeNull();
            chart.Trail.Select(e => e.Id).ShouldBe(["all"]);
            chart.Stage.Digits.Value.ShouldBe(100);
            chart.LegendRows.Select(r => r.Entry!.Id).ShouldBe(["people", "fleet"]);
        });
    }

    [AvaloniaFact]
    public void Clicking_A_Branch_Makes_It_The_Centre_And_Reports_The_Segment()
    {
        var chart = Chart();
        var events = new List<SunburstFocusedIdChangedEventArgs>();
        chart.FocusedIdChanged += (_, e) => events.Add(e);
        Run(chart, window =>
        {
            Click(window, WindowPoint(chart, window, "people"));
            chart.FocusedId.ShouldBe("people");
            chart.Center.Id.ShouldBe("people");
            var e = events.ShouldHaveSingleItem();
            e.Id.ShouldBe("people");
            e.PreviousId.ShouldBe("all");
            e.Node.Label.ShouldBe("People");
            e.ChangeSource.ShouldBe(SunburstFocusChangeSource.Segment);
            chart.Stage.Digits.Value.ShouldBe(80);
            chart.Trail.Select(t => t.Id).ShouldBe(["all", "people"]);
            chart.LegendRows.Select(r => r.Entry!.Id).ShouldBe(["crew", "shore"]);
            // Arcs reach their new angles at once with motion off.
            var (t0, t1, _, _, alpha) = chart.Stage.ValuesOf(At(chart, "crew"));
            (t1 - t0).ShouldBe(0.75, 1e-9);
            alpha.ShouldBe(1);
            chart.Stage.ValuesOf(At(chart, "fleet")).Alpha.ShouldBe(0);
        });
    }

    [AvaloniaFact]
    public void Clicking_A_Leaf_Highlights_It_Without_Zooming()
    {
        var chart = Chart();
        Run(chart, window =>
        {
            Click(window, WindowPoint(chart, window, "shore"));
            chart.Center.Id.ShouldBe("all");
            chart.Stage.Active!.Id.ShouldBe("shore");
            chart.Stage.IsTooltipOpen.ShouldBeTrue();
        });
    }

    [AvaloniaFact]
    public void Clicking_The_Centre_Zooms_Out_One_Level()
    {
        var chart = Chart();
        chart.FocusedId = "crew";
        var sources = new List<SunburstFocusChangeSource>();
        chart.FocusedIdChanged += (_, e) => sources.Add(e.ChangeSource);
        Run(chart, window =>
        {
            chart.Center.Id.ShouldBe("crew");
            var middle = chart.Stage.TranslatePoint(new Point(chart.Stage.Bounds.Width / 2, chart.Stage.Bounds.Height / 2), window)!.Value;
            Click(window, middle);
            chart.Center.Id.ShouldBe("people");
            Click(window, middle);
            chart.Center.Id.ShouldBe("all");
            chart.FocusedId.ShouldBeNull();
            Click(window, middle);
            chart.Center.Id.ShouldBe("all");
            sources.ShouldBe([SunburstFocusChangeSource.Center, SunburstFocusChangeSource.Center]);
        });
    }

    [AvaloniaFact]
    public void FocusedId_Drives_The_Centre_With_Code_As_The_Source()
    {
        var chart = Chart();
        var events = new List<SunburstFocusedIdChangedEventArgs>();
        chart.FocusedIdChanged += (_, e) => events.Add(e);
        Run(chart, _ =>
        {
            chart.FocusedId = "fleet";
            chart.Center.Id.ShouldBe("fleet");
            events.ShouldHaveSingleItem().ChangeSource.ShouldBe(SunburstFocusChangeSource.Code);
            // A leaf has nothing to open: its parent stays at the centre, and no move is reported.
            chart.FocusedId = "fuel";
            chart.Center.Id.ShouldBe("fleet");
            events.Count.ShouldBe(1);
            // The root's own id and null are the same place; an unknown id falls back to it.
            chart.FocusedId = "all";
            chart.Center.Id.ShouldBe("all");
            events.Count.ShouldBe(2);
            events[1].Id.ShouldBe("all");
            chart.FocusedId = "missing";
            chart.Center.Id.ShouldBe("all");
            events.Count.ShouldBe(2);
        });
    }

    [AvaloniaFact]
    public void A_TwoWay_Binding_Survives_A_Zoom()
    {
        var host = new Border { Width = 10 };
        var chart = Chart();
        chart.Bind(Sunburst.FocusedIdProperty, new Avalonia.Data.Binding(nameof(Border.Tag)) { Source = host, Mode = Avalonia.Data.BindingMode.TwoWay });
        Run(chart, window =>
        {
            Click(window, WindowPoint(chart, window, "people"));
            host.Tag.ShouldBe("people");
            host.Tag = "fleet";
            chart.Center.Id.ShouldBe("fleet");
        });
    }

    // ---- breadcrumb -----------------------------------------------------------------------------------------------

    [AvaloniaFact]
    public void Breadcrumb_Marks_The_Current_Level_And_Zooms_Back()
    {
        var chart = Chart();
        chart.FocusedId = "crew";
        var sources = new List<SunburstFocusChangeSource>();
        chart.FocusedIdChanged += (_, e) => sources.Add(e.ChangeSource);
        Run(chart, window =>
        {
            var crumb = (int i) => (BreadcrumbItem)chart.Breadcrumb.ContainerFromIndex(i)!;
            chart.Breadcrumb.ItemCount.ShouldBe(3);
            crumb(2).IsCurrent.ShouldBeTrue();
            crumb(2).Content.ShouldBe("Crew");
            crumb(0).Focus(NavigationMethod.Tab);
            Key(window, PhysicalKey.Enter);
            chart.Center.Id.ShouldBe("all");
            sources.ShouldBe([SunburstFocusChangeSource.Breadcrumb]);
            chart.Breadcrumb.ItemCount.ShouldBe(1);
            crumb(0).IsCurrent.ShouldBeTrue();
            // The chosen crumb can no longer hold focus: the keyboard user continues in the ring.
            chart.Stage.IsFocused.ShouldBeTrue();
        });
    }

    // ---- keyboard --------------------------------------------------------------------------------------------------

    [AvaloniaFact]
    public void Keyboard_Walks_The_Tree_And_Zooms()
    {
        var chart = Chart();
        var sources = new List<SunburstFocusChangeSource>();
        chart.FocusedIdChanged += (_, e) => sources.Add(e.ChangeSource);
        Run(chart, window =>
        {
            chart.Stage.Focus(NavigationMethod.Tab);
            chart.Stage.Active!.Id.ShouldBe("people");
            Key(window, PhysicalKey.ArrowRight);
            chart.Stage.Active!.Id.ShouldBe("fleet");
            Key(window, PhysicalKey.ArrowRight);
            chart.Stage.Active!.Id.ShouldBe("people");
            Key(window, PhysicalKey.ArrowLeft);
            chart.Stage.Active!.Id.ShouldBe("fleet");
            Key(window, PhysicalKey.ArrowLeft);
            Key(window, PhysicalKey.ArrowDown);
            chart.Stage.Active!.Id.ShouldBe("crew");
            Key(window, PhysicalKey.ArrowUp);
            chart.Stage.Active!.Id.ShouldBe("people");
            Key(window, PhysicalKey.Enter);
            chart.Center.Id.ShouldBe("people");
            chart.Stage.Active!.Id.ShouldBe("crew");
            Key(window, PhysicalKey.Space);
            chart.Center.Id.ShouldBe("crew");
            chart.Stage.Active!.Id.ShouldBe("deck");
            // Enter on a leaf does nothing.
            Key(window, PhysicalKey.Enter);
            chart.Center.Id.ShouldBe("crew");
            Key(window, PhysicalKey.Escape);
            chart.Center.Id.ShouldBe("people");
            chart.Stage.Active!.Id.ShouldBe("crew");
            Key(window, PhysicalKey.Backspace);
            chart.Center.Id.ShouldBe("all");
            chart.Stage.Active!.Id.ShouldBe("people");
            Key(window, PhysicalKey.Escape);
            chart.Center.Id.ShouldBe("all");
            chart.Stage.Active.ShouldBeNull();
            sources.ShouldBe([
                SunburstFocusChangeSource.Segment, SunburstFocusChangeSource.Segment,
                SunburstFocusChangeSource.Center, SunburstFocusChangeSource.Center
            ]);
        });
    }

    [AvaloniaFact]
    public void Up_From_The_First_Ring_Zooms_Out_And_Down_Past_The_Last_Ring_Moves_The_Centre()
    {
        var chart = Chart();
        chart.Depth = 1;
        Run(chart, window =>
        {
            chart.Stage.Focus(NavigationMethod.Tab);
            chart.Stage.Active!.Id.ShouldBe("people");
            Key(window, PhysicalKey.ArrowDown);
            chart.Center.Id.ShouldBe("people");
            chart.Stage.Active!.Id.ShouldBe("crew");
            Key(window, PhysicalKey.ArrowUp);
            chart.Center.Id.ShouldBe("all");
            chart.Stage.Active!.Id.ShouldBe("people");
        });
    }

    // ---- legend ----------------------------------------------------------------------------------------------------

    [AvaloniaFact]
    public void Legend_Rows_Are_Named_Buttons_That_Zoom()
    {
        var chart = Chart();
        chart.ValueFormatter = v => $"${v:0}";
        var sources = new List<SunburstFocusChangeSource>();
        chart.FocusedIdChanged += (_, e) => sources.Add(e.ChangeSource);
        Run(chart, _ =>
        {
            var rows = chart.LegendRows.ToList();
            rows[0].AccessibleName.ShouldBe("People, $80, 80%");
            rows[1].AccessibleName.ShouldBe("Fleet, $20, 20%");
            var peer = ControlAutomationPeer.CreatePeerForElement(rows[0]);
            peer.GetAutomationControlType().ShouldBe(AutomationControlType.Button);
            peer.GetName().ShouldBe("People, $80, 80%");
            ((IInvokeProvider)peer).Invoke();
            chart.Center.Id.ShouldBe("people");
            sources.ShouldBe([SunburstFocusChangeSource.Legend]);
            chart.LegendRows.First().AccessibleName.ShouldBe("Crew, $60, 75%");
        });
    }

    [AvaloniaFact]
    public void Legend_Can_Be_Hidden_And_Skips_Empty_Children()
    {
        var chart = Chart(new SunburstNode("r", "R", children: [new SunburstNode("a", "A", 1), new SunburstNode("z", "Zero", 0)]));
        Run(chart, _ =>
        {
            chart.LegendRows.Select(r => r.Entry!.Id).ShouldBe(["a"]);
            chart.IsLegendVisible = false;
            chart.LegendRows.ShouldBeEmpty();
            Dispatcher.UIThread.RunJobs();
            chart.IsLegendBeside.ShouldBeFalse();
        });
    }

    [AvaloniaFact]
    public void Legend_Sits_Beside_When_Wide_And_Below_When_Narrow()
    {
        var chart = Chart();
        var width = new Border { Child = chart, Width = 800 };
        var window = TestHelpers.Show(width);
        window.Width = 900;
        window.Height = 900;
        Dispatcher.UIThread.RunJobs();
        try
        {
            chart.IsLegendBeside.ShouldBeTrue();
            chart.Diameter.ShouldBe(300);
            width.Width = 380;
            Dispatcher.UIThread.RunJobs();
            chart.IsLegendBeside.ShouldBeFalse();
            chart.Diameter.ShouldBe(300);
            width.Width = 200;
            Dispatcher.UIThread.RunJobs();
            chart.Diameter.ShouldBe(200);
        }
        finally
        {
            window.Close();
        }
    }

    // ---- accessibility ---------------------------------------------------------------------------------------------

    [AvaloniaFact]
    public void Automation_Announces_Path_Value_And_Share_Of_Parent()
    {
        var chart = Chart();
        chart.ValueFormatter = v => $"${v:0}K";
        Run(chart, window =>
        {
            var peer = ControlAutomationPeer.CreatePeerForElement(chart.Stage);
            peer.GetAutomationControlType().ShouldBe(AutomationControlType.Group);
            peer.GetLocalizedControlType().ShouldBe("sunburst chart");
            peer.GetName().ShouldStartWith("Costs, Costs.");
            peer.IsKeyboardFocusable().ShouldBeTrue();
            chart.Stage.Focus(NavigationMethod.Tab);
            Key(window, PhysicalKey.ArrowDown);
            peer.GetName().ShouldBe("People, Crew, $60K, 75% of People");
            peer.GetHelpText().ShouldBe("People, Crew, $60K, 75% of People");
        });
        AutomationProperties.GetName(chart).ShouldBe("Costs");
    }

    // ---- formatter, colors, labels ---------------------------------------------------------------------------------

    [AvaloniaFact]
    public void Formatter_Reaches_The_Centre_Tooltip_And_Legend()
    {
        var chart = Chart();
        Run(chart, _ =>
        {
            chart.Stage.Digits.DisplayText.ShouldBe("100");
            chart.ValueFormatter = v => $"${v * 10:#,0}K";
            chart.Stage.Digits.Prefix.ShouldBe("$");
            chart.Stage.Digits.Suffix.ShouldBe("K");
            chart.Stage.Digits.Value.ShouldBe(1000);
            chart.Stage.Digits.UseGrouping.ShouldBeTrue();
            chart.Stage.Digits.DisplayText.ShouldBe("$1,000K");
            chart.LegendRows.First().AccessibleName.ShouldBe("People, $800K, 80%");
            chart.Describe(At(chart, "deck")).ShouldBe("People, Crew, Deck, $300K, 50% of Crew");
            chart.ValueFormatter = _ => "n/a";
            chart.Stage.HasRollingValue.ShouldBeFalse();
        });
    }

    [AvaloniaFact]
    public void Top_Level_Branches_Take_Series_Hues_And_Descendants_Are_Tinted()
    {
        var chart = Chart();
        Run(chart, _ =>
        {
            var p = chart.Palette;
            chart.Stage.FillOf(At(chart, "people")).ShouldBe(ChartsBMath.MixIn(p.Series(0), 0.6, p.Surface));
            chart.Stage.FillOf(At(chart, "fleet")).ShouldBe(ChartsBMath.MixIn(p.Series(1), 0.6, p.Surface));
            var crew = At(chart, "crew");
            crew.Branch.ShouldBe(0);
            crew.Tint.ShouldBeLessThan(At(chart, "people").Tint);
            At(chart, "deck").Tint.ShouldBeLessThan(crew.Tint);
            chart.Stage.FillOf(crew).ShouldNotBe(chart.Stage.FillOf(At(chart, "people")));
        });
    }

    [AvaloniaFact]
    public void Labels_Show_Only_Where_They_Fit()
    {
        var chart = Chart(new SunburstNode("r", "R", children:
        [
            new SunburstNode("big", "Big", 90),
            new SunburstNode("tiny", "A label far too long for a sliver of the ring", 0.2)
        ]));
        Run(chart, _ =>
        {
            chart.Stage.FitOf(At(chart, "big")).ShouldBe(SunburstLabelFit.Tangent);
            chart.Stage.FitOf(At(chart, "tiny")).ShouldBe(SunburstLabelFit.None);
        });
    }

    // ---- data and empty state --------------------------------------------------------------------------------------

    [AvaloniaFact]
    public void Empty_Total_Shows_The_Empty_State()
    {
        var chart = new Sunburst { Label = "Costs", Root = new SunburstNode("r", "R", children: [new SunburstNode("a", "A", 0)]) };
        Run(chart, _ =>
        {
            chart.IsEmpty.ShouldBeTrue();
            chart.EmptyLabel.ShouldBe("No data yet");
            chart.LegendRows.ShouldBeEmpty();
            chart.Breadcrumb.IsVisible.ShouldBeFalse();
            chart.Stage.Focusable.ShouldBeFalse();
            chart.Stage.HasRollingValue.ShouldBeFalse();
            chart.Root = Costs();
            chart.IsEmpty.ShouldBeFalse();
            chart.Breadcrumb.IsVisible.ShouldBeTrue();
            chart.Stage.Focusable.ShouldBeTrue();
            chart.Stage.HasRollingValue.ShouldBeTrue();
        });
    }

    [AvaloniaFact]
    public void Inline_Items_Hang_Under_A_Root_Named_By_The_Label()
    {
        var chart = new Sunburst { Label = "Hours" };
        chart.Items.Add(new SunburstNode("a", "A", 3));
        chart.Items.Add(new SunburstNode("b", "B", 1));
        Run(chart, _ =>
        {
            chart.Center.Label.ShouldBe("Hours");
            chart.Center.Value.ShouldBe(4);
            chart.Items.Add(new SunburstNode("c", "C", 4));
            chart.Center.Value.ShouldBe(8);
            chart.LegendRows.Count().ShouldBe(3);
        });
    }

    [AvaloniaFact]
    public void Replacing_The_Data_Keeps_The_Centre_When_Its_Id_Survives()
    {
        var chart = Chart();
        chart.FocusedId = "people";
        var events = 0;
        chart.FocusedIdChanged += (_, _) => events++;
        Run(chart, _ =>
        {
            chart.Root = new SunburstNode("all", "Costs", children:
            [
                new SunburstNode("people", "People", children: [new SunburstNode("crew", "Crew", 1), new SunburstNode("shore", "Shore", 3)])
            ]);
            chart.Center.Id.ShouldBe("people");
            chart.Center.Value.ShouldBe(4);
            var (t0, t1, _, _, _) = chart.Stage.ValuesOf(At(chart, "crew"));
            (t1 - t0).ShouldBe(0.25, 1e-9);
            events.ShouldBe(0);
            chart.Root = new SunburstNode("other", "Other", children: [new SunburstNode("x", "X", 1)]);
            chart.Center.Id.ShouldBe("other");
        });
    }

    [AvaloniaFact]
    public void Depth_Limits_The_Rings_On_Screen()
    {
        var chart = Chart();
        chart.Depth = 2;
        Run(chart, _ =>
        {
            chart.Stage.ValuesOf(At(chart, "crew")).Alpha.ShouldBe(1);
            chart.Stage.ValuesOf(At(chart, "deck")).Alpha.ShouldBe(0);
            chart.Depth = 3;
            chart.Stage.ValuesOf(At(chart, "deck")).Alpha.ShouldBe(1);
        });
    }

    [AvaloniaFact]
    public void Draws_At_Rest_And_Keeps_Its_Geometry_Between_Draws()
    {
        var chart = Chart();
        Run(chart, _ =>
        {
            using var bitmap = new Avalonia.Media.Imaging.RenderTargetBitmap(new PixelSize(300, 300));
            bitmap.Render(chart.Stage);
            var first = At(chart, "people").Geometry;
            first.ShouldNotBeNull();
            bitmap.Render(chart.Stage);
            At(chart, "people").Geometry.ShouldBeSameAs(first);
            chart.GetVisualDescendants().OfType<Control>().ShouldContain(chart.Stage.Digits);
        });
    }

    [Fact]
    public void Legend_shares_always_show_one_decimal()
    {
        Assert.Equal("44.0%", Sunburst.LegendShare(0.44));
        Assert.Equal("38.8%", Sunburst.LegendShare(0.3883));
        Assert.Equal("100.0%", Sunburst.LegendShare(1));
    }
}
