using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using TKCode.Arc.Motion;

namespace TKCode.Arc.Tests.Blocks.Pricing;

public class ComparisonTableTests
{
    private static Window Show(Control control, double width = 1000, ArcMotionLevel level = ArcMotionLevel.Off)
    {
        var window = TestHelpers.Show(control, level);
        window.Width = width;
        window.Height = 800;
        Dispatcher.UIThread.RunJobs();
        return window;
    }

    private static ComparisonTable Small(ComparisonCta? cta = null) => new()
    {
        Cta = cta,
        Columns =
        [
            new ComparisonColumn { Id = "a", Name = "Other A" },
            new ComparisonColumn { Id = "us", Name = "Ours", IsHighlighted = true },
            new ComparisonColumn { Id = "b", Name = "Other B" },
        ],
        Sections =
        [
            new ComparisonSection
            {
                Id = "one", Title = "One",
                Rows =
                [
                    new ComparisonRow { Id = "same", Feature = "Same", Values = { ["us"] = true, ["a"] = true, ["b"] = true } },
                    new ComparisonRow { Id = "diff", Feature = "Differs", Values = { ["us"] = true, ["a"] = false, ["b"] = true } },
                ]
            },
            new ComparisonSection
            {
                Id = "two", Title = "Two",
                Rows =
                [
                    new ComparisonRow { Id = "notes", Feature = "Notes only", Values = { ["us"] = ComparisonValue.Partial("x"), ["a"] = ComparisonValue.Partial("y"), ["b"] = ComparisonValue.Partial() } },
                ]
            }
        ]
    };

    [AvaloniaFact]
    public void Defaults_Show_All_Columns_And_Rows()
    {
        var table = new ComparisonTable();
        var window = Show(table);
        try
        {
            table.VisibleColumns.Count.ShouldBe(4);
            table.OwnColumn!.IsHighlighted.ShouldBeTrue();
            table.ExpandedRowIds.Count().ShouldBe(10);
            table.ExpandedSectionIds.Count().ShouldBe(3);
            table.IsDifferencesOnly.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void The_First_Column_Is_Yours_Without_A_Highlight()
    {
        var table = new ComparisonTable { Columns = [new ComparisonColumn { Id = "x", Name = "X" }, new ComparisonColumn { Id = "y", Name = "Y" }] };
        var window = Show(table);
        try
        {
            table.OwnColumn!.Id.ShouldBe("x");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Only_Differences_Hides_Matching_Rows_And_Empty_Sections_And_Ignores_Notes()
    {
        var table = Small();
        var window = Show(table);
        try
        {
            table.IsDifferencesOnly = true;
            Dispatcher.UIThread.RunJobs();
            table.ExpandedRowIds.ShouldBe(["diff"]);
            table.ExpandedSectionIds.ShouldBe(["one"]);
            table.Announcer!.Announcement.ShouldBe("Showing only rows that differ");

            table.IsDifferencesOnly = false;
            Dispatcher.UIThread.RunJobs();
            table.ExpandedRowIds.Count().ShouldBe(3);
            table.Announcer.Announcement.ShouldBe("Showing all rows");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void The_Switch_And_The_Property_Follow_Each_Other()
    {
        var table = Small();
        var window = Show(table);
        try
        {
            table.DifferencesSwitch!.IsChecked = true;
            Dispatcher.UIThread.RunJobs();
            table.IsDifferencesOnly.ShouldBeTrue();
            table.IsDifferencesOnly = false;
            Dispatcher.UIThread.RunJobs();
            table.DifferencesSwitch.IsChecked.ShouldBe(false);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Below_StackBelow_Your_Column_Stands_Beside_One_Competitor_In_Column_Order()
    {
        var table = Small();
        var window = Show(table, 500);
        try
        {
            table.IsStacked.ShouldBeTrue();
            table.VisibleColumns.Select(c => c.Id).ShouldBe(["a", "us"]);
            table.Picker!.IsVisible.ShouldBeTrue();
            table.Picker.Buttons.Select(b => b.Content).ShouldBe(["Other A", "Other B"]);
            table.Picker.SelectedIndex.ShouldBe(0);

            table.CompareWith = "b";
            Dispatcher.UIThread.RunJobs();
            table.VisibleColumns.Select(c => c.Id).ShouldBe(["us", "b"]);
            table.HeadColumnIds.ShouldBe(["us", "b"]);
            table.Picker.SelectedIndex.ShouldBe(1);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Differences_Are_Judged_Among_The_Visible_Columns()
    {
        var table = Small();
        var window = Show(table, 500);
        try
        {
            table.CompareWith = "b";
            table.IsDifferencesOnly = true;
            Dispatcher.UIThread.RunJobs();
            // us and b agree on "diff": only a differed.
            table.ExpandedRowIds.ShouldBeEmpty();

            table.CompareWith = "a";
            Dispatcher.UIThread.RunJobs();
            table.ExpandedRowIds.ShouldBe(["diff"]);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Picking_A_Competitor_Sets_CompareWith_And_Cells()
    {
        var table = Small();
        var window = Show(table, 500);
        try
        {
            table.Picker!.Buttons[1].Focus();
            window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
            table.CompareWith.ShouldBe("b");
            AutomationProperties.GetName(table.CellsOf("diff")[1]).ShouldBe("Other B: Included");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Cells_Have_Text_Alternatives()
    {
        var table = Small();
        var window = Show(table);
        try
        {
            AutomationProperties.GetName(table.CellsOf("diff")[0]).ShouldBe("Other A: Not included");
            AutomationProperties.GetName(table.CellsOf("notes")[0]).ShouldBe("Other A: Partial, y");
            AutomationProperties.GetName(table.CellsOf("notes")[2]).ShouldBe("Other B: Partial");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Value_Converts_From_Bool_And_String()
    {
        ComparisonValue check = true;
        ComparisonValue dash = false;
        ComparisonValue text = "Up to 50";
        check.Mark.ShouldBe(ComparisonMark.Included);
        dash.Mark.ShouldBe(ComparisonMark.NotIncluded);
        text.Mark.ShouldBe(ComparisonMark.Text);
        text.Text.ShouldBe("Up to 50");
        ComparisonValue.Partial("n").Note.ShouldBe("n");
    }

    [AvaloniaFact]
    public void A_Check_Draws_Itself_Only_At_Full_Motion()
    {
        var table = Small();
        var window = Show(table);
        try
        {
            table.CellsOf("diff")[1].DrawAmount.ShouldBe(1);
        }
        finally
        {
            window.Close();
        }

        var full = Small();
        var fullWindow = Show(full, level: ArcMotionLevel.Full);
        try
        {
            TestHelpers.PumpUntil(() => full.CellsOf("diff")[1].DrawAmount > 0.99).ShouldBeTrue();
        }
        finally
        {
            fullWindow.Close();
        }
    }

    [AvaloniaFact]
    public void The_Cta_Runs_And_Morphs_To_Its_Done_Label()
    {
        var table = Small(new ComparisonCta { Label = "Start", DoneLabel = "Started" });
        var raised = 0;
        table.CtaInvoked += (_, _) => raised++;
        var window = Show(table);
        try
        {
            var button = table.CtaButton.ShouldNotBeNull();
            button.CurrentText.ShouldBe("Start");
            button.RaiseEvent(new RoutedEventArgs(Avalonia.Controls.Button.ClickEvent));
            Dispatcher.UIThread.RunJobs();
            raised.ShouldBe(1);
            button.IsDone.ShouldBeTrue();
            button.CurrentText.ShouldBe("Started");
            AutomationProperties.GetName(button).ShouldBe("Started");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Link_Cta_Does_Not_Turn_Done()
    {
        var table = Small(new ComparisonCta { Label = "Start", DoneLabel = "Started", Href = "https://example.com" });
        table.CtaInvoked += (_, e) => e.Handled = true;
        var window = Show(table);
        try
        {
            var button = table.CtaButton!;
            button.RaiseEvent(new RoutedEventArgs(Avalonia.Controls.Button.ClickEvent));
            Dispatcher.UIThread.RunJobs();
            button.IsDone.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void The_Header_Stays_Pinned_While_The_Table_Scrolls()
    {
        var table = Small();
        table.MaxTableHeight = 200;
        var window = Show(table);
        try
        {
            var scroller = TestHelpers.Descendant<ScrollViewer>(table, ComparisonTable.ScrollerPart);
            scroller.Bounds.Height.ShouldBe(200, 0.5);
            table.HeadOffset.ShouldBe(0);
            scroller.Offset = new Avalonia.Vector(0, 40);
            Dispatcher.UIThread.RunJobs();
            table.HeadOffset.ShouldBe(40, 0.5);
        }
        finally
        {
            window.Close();
        }
    }
}
