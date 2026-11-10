using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using TKCode.Arc.Motion;

namespace TKCode.Arc.Tests.Blocks.SocialProof;

public class StatsBandTests
{
    private static Window Show(Control control, double width = 1100, ArcMotionLevel level = ArcMotionLevel.Off)
    {
        var window = TestHelpers.Show(control, level);
        window.Width = width;
        window.Height = 900;
        Dispatcher.UIThread.RunJobs();
        return window;
    }

    [AvaloniaFact]
    public void Empty_Stats_Show_The_Samples_At_Their_Final_Values_Below_Full_Motion()
    {
        var band = new StatsBand();
        Show(band);
        band.Items.Count.ShouldBe(4);
        band.Items[0].Number!.DisplayText.ShouldBe("1.5");
        band.Items[0].Number!.FinalText.ShouldBe("1.5M");
        band.Items[1].Number!.FinalText.ShouldBe("99.98%");
        band.Items[0].Opacity.ShouldBe(1);
        double.IsPositiveInfinity(band.Time).ShouldBeTrue();
    }

    [AvaloniaFact]
    public void Number_Formats_Prefix_Decimals_Suffix_And_Compact_Units()
    {
        var band = new StatsBand();
        band.Stats.Add(new StatsBandStat { Value = 2_334_000_000, Notation = StatsBandNotation.Compact, Decimals = 2, Prefix = "$", Suffix = "+", Label = "A" });
        band.Stats.Add(new StatsBandStat { Value = 12_500, Label = "B" });
        band.Stats.Add(new StatsBandStat { Value = 950, Notation = StatsBandNotation.Compact, Label = "C" });
        Show(band);
        band.Items[0].Number!.FinalText.ShouldBe("$2.33B+");
        band.Items[1].Number!.FinalText.ShouldBe("12,500");
        band.Items[2].Number!.FinalText.ShouldBe("950");
    }

    [AvaloniaFact]
    public void Compact_Number_Counts_Inside_Its_Final_Unit()
    {
        var band = new StatsBand();
        band.Stats.Add(new StatsBandStat { Value = 2_000_000, Notation = StatsBandNotation.Compact, Decimals = 1, Label = "A" });
        Show(band);
        var number = band.Items[0].Number!;
        number.SetProgress(0.5);
        number.DisplayText.ShouldBe("1.0");
        number.SetProgress(0);
        number.DisplayText.ShouldBe("0.0");
        number.SetProgress(1);
        number.DisplayText.ShouldBe("2.0");
    }

    [AvaloniaFact]
    public void Locale_Changes_The_Separators_And_An_Unknown_One_Falls_Back()
    {
        var band = new StatsBand { Locale = "de-DE" };
        band.Stats.Add(new StatsBandStat { Value = 1234.5, Decimals = 1, Label = "A" });
        Show(band);
        band.Items[0].Number!.FinalText.ShouldNotBeNullOrEmpty();
        band.Locale = "not a culture";
        Dispatcher.UIThread.RunJobs();
        band.Items[0].Number!.FinalText.ShouldBe("1,234.5");
    }

    [AvaloniaTheory]
    [InlineData(1100, 4, 4)]
    [InlineData(700, 4, 2)]
    [InlineData(700, 3, 1)]
    [InlineData(400, 4, 1)]
    public void Columns_Follow_The_Width_And_A_Band_Of_Three_Stacks(double width, int stats, int columns)
    {
        var band = new StatsBand();
        for (var i = 0; i < stats; i++)
        {
            band.Stats.Add(new StatsBandStat { Value = i, Label = $"Stat {i}" });
        }
        Show(band, width);
        band.ColumnCount.ShouldBe(columns);
    }

    [AvaloniaFact]
    public void Numbers_Scale_With_The_Width()
    {
        var wide = new StatsBand();
        Show(wide, 1400);
        wide.Items[0].NumberSize.ShouldBe(52);
        var narrow = new StatsBand();
        Show(narrow, 400);
        narrow.Items[0].NumberSize.ShouldBe(40);
    }

    [AvaloniaFact]
    public void A_Stat_With_Context_Takes_Focus_And_One_Without_Does_Not()
    {
        var band = new StatsBand();
        band.Stats.Add(new StatsBandStat { Value = 1, Label = "A", Detail = "d", Context = "c" });
        band.Stats.Add(new StatsBandStat { Value = 2, Label = "B", Detail = "d" });
        Show(band);
        band.Items[0].Focusable.ShouldBeTrue();
        band.Items[1].Focusable.ShouldBeFalse();
        band.Items[0].Classes.Contains(StatsBandItem.SwapsPseudoClass).ShouldBeTrue();
    }

    [AvaloniaFact]
    public void Keyboard_Focus_Reveals_The_Context_And_Leaving_Hides_It()
    {
        var band = new StatsBand();
        band.Stats.Add(new StatsBandStat { Value = 1, Label = "A", Detail = "d", Context = "c" });
        band.Stats.Add(new StatsBandStat { Value = 2, Label = "B", Detail = "e", Context = "f" });
        Show(band);
        var first = band.Items[0];
        first.Focus(NavigationMethod.Tab);
        Dispatcher.UIThread.RunJobs();
        first.IsActive.ShouldBeTrue();
        band.Items[1].Focus(NavigationMethod.Tab);
        Dispatcher.UIThread.RunJobs();
        first.IsActive.ShouldBeFalse();
        band.Items[1].IsActive.ShouldBeTrue();
    }

    [AvaloniaFact]
    public void The_Accent_Mix_Follows_The_Active_Amount()
    {
        var band = new StatsBand();
        Show(band);
        var view = band.Items[0].VisualView!;
        var rest = view.InkColor;
        view.ActiveAmount = 1;
        view.InkColor.ShouldNotBe(rest);
        view.ActiveAmount = 0;
        view.InkColor.ShouldBe(rest);
    }

    [AvaloniaFact]
    public void Accessible_Name_Reads_The_Final_Value_And_The_Lines()
    {
        var band = new StatsBand();
        band.Stats.Add(new StatsBandStat { Value = 38, Suffix = "ms", Label = "Median", Detail = "Edge", Context = "p95" });
        Show(band);
        AutomationProperties.GetName(band.Items[0]).ShouldBe("38ms Median. Edge. p95");
    }

    [AvaloniaFact]
    public void The_Band_Is_Named_By_Its_Title_Or_By_The_Accessible_Label()
    {
        var band = new StatsBand();
        Show(band);
        AutomationProperties.GetName(band).ShouldBe("Key numbers");
        band.Title = "Our numbers";
        AutomationProperties.GetName(band).ShouldBe("Our numbers");
    }

    [Fact]
    public void Nearest_Land_Snaps_A_Sea_Point_And_Keeps_A_Land_Point()
    {
        var land = StatsBandWorld.NearestLand(2.35, 48.9);
        StatsBandWorld.Land[land].ShouldBeTrue();
        StatsBandWorld.Land.Length.ShouldBe(80 * 30);
        // Just off the coast of Lisbon.
        StatsBandWorld.Land[StatsBandWorld.NearestLand(-11, 38.7)].ShouldBeTrue();
    }

    [AvaloniaFact]
    public void Full_Motion_Waits_For_The_View_And_Then_Counts_To_The_Final_Value()
    {
        var band = new StatsBand { CountDuration = TimeSpan.FromMilliseconds(60) };
        band.Stats.Add(new StatsBandStat { Value = 100, Label = "A" });
        Show(band, 1100, ArcMotionLevel.Full);
        TestHelpers.PumpUntil(() => double.IsPositiveInfinity(band.Time), 6000).ShouldBeTrue();
        band.Items[0].Number!.DisplayText.ShouldBe("100");
        band.Items[0].Opacity.ShouldBe(1);
    }

    [AvaloniaFact]
    public void Divided_Layout_Removes_The_Gaps_And_Pads_The_Inner_Edges()
    {
        var band = new StatsBand { Layout = StatsBandLayout.Divided };
        Show(band, 1100);
        band.Items[0].Padding.Left.ShouldBe(0);
        band.Items[1].Padding.Left.ShouldBe(32);
        band.Items[3].Padding.Right.ShouldBe(0);
        band.Layout = StatsBandLayout.Plain;
        Dispatcher.UIThread.RunJobs();
        band.Items[1].Padding.ShouldBe(default);
    }

    [AvaloniaFact]
    public void Removing_Every_Stat_Falls_Back_To_The_Samples()
    {
        var band = new StatsBand();
        band.Stats.Add(new StatsBandStat { Value = 1, Label = "A" });
        Show(band);
        band.Items.Count.ShouldBe(1);
        band.Stats.Clear();
        band.Items.Count.ShouldBe(4);
    }
}
