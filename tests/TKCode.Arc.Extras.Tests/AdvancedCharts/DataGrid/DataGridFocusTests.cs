using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.VisualTree;
using static TKCode.Arc.Tests.FocusRingInput;

namespace TKCode.Arc.Tests.AdvancedCharts;

/// <summary>
/// The data grid's focus moves follow the input (DataGridFocusModality): the value checklist opened from the filter row
/// focuses its first value with the keyboard ring after a key only.
/// </summary>
public class DataGridFocusTests
{
    private static (TKCode.Arc.DataGrid Grid, Window Window, Avalonia.Controls.Button Select) Show()
    {
        var grid = DataGridBehaviorTests.Create();
        var window = DataGridBehaviorTests.Show(grid);
        window.Width = 1000;
        window.Height = 700;
        grid.IsFilterRowOpen = true;
        Pump();
        var region = grid.Store.AccessorIndex("region");
        var cell = grid.GetVisualDescendants().OfType<DataGridFilterCell>().First(c => c.Accessor == region);
        var select = cell.GetVisualDescendants().OfType<Avalonia.Controls.Button>().First(b => b.Name == DataGridFilterCell.SelectPart);
        return (grid, window, select);
    }

    private static Checkbox FirstValue(Window window) => window.GetVisualDescendants().OfType<Checkbox>().First();

    [AvaloniaFact]
    public void The_Checklist_Opened_With_A_Click_Focuses_Its_First_Value_Without_The_Ring()
    {
        var (_, window, select) = Show();
        try
        {
            Click(window, select);
            FirstValue(window).ShouldBeFocusedWithoutRing();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void The_Checklist_Opened_With_Enter_Focuses_Its_First_Value_With_The_Ring()
    {
        var (_, window, select) = Show();
        try
        {
            select.Focus(NavigationMethod.Tab);
            Press(window, PhysicalKey.Enter);
            FirstValue(window).ShouldBeFocusedWithRing();
            Press(window, PhysicalKey.Escape);
            select.ShouldBeFocusedWithRing();
        }
        finally
        {
            window.Close();
        }
    }
}
