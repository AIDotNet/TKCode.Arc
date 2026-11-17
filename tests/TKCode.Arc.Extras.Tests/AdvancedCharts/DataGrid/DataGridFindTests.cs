using System.Collections.ObjectModel;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using Shouldly;

namespace TKCode.Arc.Tests.AdvancedCharts;

/// <summary>Find and replace: matching on display text, stepping through matches, tinting and replacing as one undo step.</summary>
public class DataGridFindTests
{
    private static void Key(Avalonia.Controls.Window window, PhysicalKey key, RawInputModifiers modifiers = RawInputModifiers.None)
    {
        window.KeyPressQwerty(key, modifiers);
        window.KeyReleaseQwerty(key, modifiers);
        Dispatcher.UIThread.RunJobs();
    }

    private static ObservableCollection<DataGridRow> Rows() =>
    [
        new("r1") { { "account", "Lumen Labs" }, { "region", "Europe" }, { "arr", 1200m }, { "health", 50m }, { "renewal", new DateOnly(2026, 1, 31) } },
        new("r2") { { "account", "Basalt Freight" }, { "region", "Asia Pacific" }, { "arr", 800m }, { "health", 70m }, { "renewal", new DateOnly(2026, 3, 1) } },
        new("r3") { { "account", "Cobalt Clinic labs" }, { "region", "Europe" }, { "arr", 3000m }, { "health", 90m }, { "renewal", null } },
        new("r4") { { "account", "Delta Dental" }, { "region", "North America" }, { "arr", null }, { "health", 40m }, { "renewal", new DateOnly(2027, 5, 9) } }
    ];

    [AvaloniaFact]
    public void Matches_Are_Found_In_The_Display_Text_With_Case_And_Whole_Cell_Options()
    {
        var grid = DataGridBehaviorTests.Create(Rows());
        DataGridBehaviorTests.Show(grid);
        grid.FindMatchCount.ShouldBe(0);
        grid.IsFindOpen = true;
        grid.FindText = "labs";
        grid.FindMatchCount.ShouldBe(2);
        grid.IsFindMatch(0, 0).ShouldBeTrue();
        grid.IsFindMatch(2, 0).ShouldBeTrue();
        grid.IsFindMatch(1, 0).ShouldBeFalse();
        grid.FindMatchCase = true;
        grid.FindMatchCount.ShouldBe(1);
        grid.IsFindMatch(0, 0).ShouldBeFalse();
        grid.IsFindMatch(2, 0).ShouldBeTrue();
        grid.FindMatchCase = false;
        grid.FindText = "1,200";
        grid.FindMatchCount.ShouldBe(1);
        grid.IsFindMatch(0, 2).ShouldBeTrue();
        grid.FindText = "Europe";
        grid.FindMatchWholeCell = true;
        grid.FindMatchCount.ShouldBe(2);
        grid.FindText = "Euro";
        grid.FindMatchCount.ShouldBe(0);
        grid.FindMatchWholeCell = false;
        grid.FindMatchCount.ShouldBe(2);
        grid.FindText = string.Empty;
        grid.FindMatchCount.ShouldBe(0);
    }

    [AvaloniaFact]
    public void Rows_Appended_To_The_Source_Join_The_Grid_Without_A_Reload()
    {
        var rows = Rows();
        var grid = DataGridBehaviorTests.Create(rows);
        DataGridBehaviorTests.Show(grid);
        grid.EnsureView();
        grid.VisibleRowCount.ShouldBe(4);
        rows.Add(new("r5") { { "account", "Quartz Labs" }, { "region", "Europe" }, { "arr", 10m }, { "health", 10m }, { "renewal", null } });
        grid.EnsureView();
        grid.VisibleRowCount.ShouldBe(5);
        grid.IsFindOpen = true;
        grid.FindText = "labs";
        grid.FindMatchCount.ShouldBe(3);
        // A row that is not at the end still takes the full path.
        rows.Insert(0, new("r0") { { "account", "Labs First" }, { "region", "Europe" }, { "arr", 1m }, { "health", 1m }, { "renewal", null } });
        grid.EnsureView();
        grid.VisibleRowCount.ShouldBe(6);
        grid.FindMatchCount.ShouldBe(4);
    }

    [AvaloniaFact]
    public void FindNext_And_FindPrevious_Step_Through_The_Matches_And_Wrap_Round()
    {
        var grid = DataGridBehaviorTests.Create(Rows());
        DataGridBehaviorTests.Show(grid);
        grid.IsFindOpen = true;
        grid.FindText = "Europe";
        grid.Select(new DataGridCellAddress(0, 0));
        grid.FindNext().ShouldBeTrue();
        grid.ActiveCell.ShouldBe(new DataGridCellAddress(0, 1));
        grid.FindNext().ShouldBeTrue();
        grid.ActiveCell.ShouldBe(new DataGridCellAddress(2, 1));
        grid.FindNext().ShouldBeTrue();
        grid.ActiveCell.ShouldBe(new DataGridCellAddress(0, 1));
        grid.FindPrevious().ShouldBeTrue();
        grid.ActiveCell.ShouldBe(new DataGridCellAddress(2, 1));
        grid.FindPosition().ShouldBe((2, 2));
        grid.FindText = "nothing here";
        grid.FindNext().ShouldBeFalse();
        grid.FindPosition().ShouldBe((0, 0));
    }

    [AvaloniaFact]
    public void Ctrl_F_Opens_The_Bar_F3_Steps_And_Escape_In_The_Field_Closes_It()
    {
        var grid = DataGridBehaviorTests.Create(Rows());
        var window = DataGridBehaviorTests.Show(grid);
        grid.IsFindOpen.ShouldBeFalse();
        Key(window, PhysicalKey.F, RawInputModifiers.Control);
        grid.IsFindOpen.ShouldBeTrue();
        grid.Viewport.FindBar.IsVisible.ShouldBeTrue();
        grid.Viewport.FindBar.IsReplaceVisible.ShouldBeFalse();
        grid.FindText = "Europe";
        grid.Select(new DataGridCellAddress(0, 0));
        grid.Viewport.Focus();
        Dispatcher.UIThread.RunJobs();
        Key(window, PhysicalKey.F3);
        grid.ActiveCell.ShouldBe(new DataGridCellAddress(0, 1));
        Key(window, PhysicalKey.F3, RawInputModifiers.Shift);
        grid.ActiveCell.ShouldBe(new DataGridCellAddress(2, 1));
        Key(window, PhysicalKey.H, RawInputModifiers.Control);
        grid.Viewport.FindBar.IsReplaceVisible.ShouldBeTrue();
        grid.CloseFind();
        grid.IsFindOpen.ShouldBeFalse();
        grid.Viewport.FindBar.IsVisible.ShouldBeFalse();
        grid.FindMatchCount.ShouldBe(2);
        grid.IsFindHighlighting.ShouldBeFalse();
    }

    [AvaloniaFact]
    public void ReplaceAll_Is_One_Undo_Step_Reads_Numbers_Back_And_Reports_What_It_Skipped()
    {
        var rows = Rows();
        var grid = DataGridBehaviorTests.Create(rows);
        DataGridBehaviorTests.Show(grid);
        var changes = new List<DataGridCellsChangedEventArgs>();
        grid.CellsChanged += (_, e) => changes.Add(e);
        grid.IsFindOpen = true;
        grid.FindText = "labs";
        grid.ReplaceAll("Group").ShouldBe(2);
        rows[0]["account"].ShouldBe("Lumen Group");
        rows[2]["account"].ShouldBe("Cobalt Clinic Group");
        changes.Single().Reason.ShouldBe(DataGridChangeReason.Replace);
        changes.Single().Changes.Count.ShouldBe(2);
        grid.StatusBar!.MessageText!.ShouldContain("Replaced in 2 cells");
        grid.FindMatchCount.ShouldBe(0);
        grid.Undo();
        rows[0]["account"].ShouldBe("Lumen Labs");
        rows[2]["account"].ShouldBe("Cobalt Clinic labs");
        grid.CanUndo.ShouldBeFalse();

        grid.FindText = "800";
        grid.ReplaceAll("900").ShouldBe(1);
        rows[1]["arr"].ShouldBe(900m);

        grid.FindText = "Europe";
        grid.ReplaceAll("EU").ShouldBe(0);
        rows[0]["region"].ShouldBe("Europe");
        grid.StatusBar!.MessageText!.ShouldContain("skipped 2");

        grid.FindText = "zzz";
        grid.ReplaceAll("x").ShouldBe(0);
        grid.StatusBar!.MessageText!.ShouldContain("Nothing to replace");
    }

    [AvaloniaFact]
    public void ReplaceCurrent_Replaces_The_Active_Match_Then_Moves_On_And_Skips_Read_Only_Cells()
    {
        var rows = Rows();
        var grid = DataGridBehaviorTests.Create(rows);
        DataGridBehaviorTests.Show(grid);
        grid.IsFindOpen = true;
        grid.FindText = "labs";
        grid.Select(new DataGridCellAddress(3, 0));
        grid.ReplaceCurrent("X").ShouldBeFalse();
        grid.ActiveCell.ShouldBe(new DataGridCellAddress(0, 0));
        grid.ReplaceCurrent("Works").ShouldBeTrue();
        rows[0]["account"].ShouldBe("Lumen Works");
        grid.ActiveCell.ShouldBe(new DataGridCellAddress(2, 0));
        grid.Columns[0].IsEditable = false;
        grid.ReplaceCurrent("Nope").ShouldBeFalse();
        rows[2]["account"].ShouldBe("Cobalt Clinic labs");
        grid.Columns[0].IsEditable = true;
        grid.FindMatchWholeCell = true;
        grid.FindText = "Delta Dental";
        grid.ReplaceAll("Delta Inc").ShouldBe(1);
        rows[3]["account"].ShouldBe("Delta Inc");
    }

    [AvaloniaFact]
    public void Matches_Are_Tinted_While_The_Bar_Is_Open_Without_Throwing()
    {
        var grid = DataGridBehaviorTests.Create(Rows());
        DataGridBehaviorTests.Show(grid);
        grid.OpenFind(replace: true);
        grid.FindText = "e";
        grid.IsFindHighlighting.ShouldBeTrue();
        Should.NotThrow(() =>
        {
            AvaloniaHeadlessPlatform.ForceRenderTimerTick();
            Dispatcher.UIThread.RunJobs();
            AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        });
        grid.Viewport.FindBar.IsVisible.ShouldBeTrue();
        grid.Viewport.FindBar.IsReplaceVisible.ShouldBeTrue();
    }
}
