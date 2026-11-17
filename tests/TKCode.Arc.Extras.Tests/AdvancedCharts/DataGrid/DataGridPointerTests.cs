using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Shouldly;

namespace TKCode.Arc.Tests.AdvancedCharts;

/// <summary>Pointer contracts: click, Shift-click and drag selection, the fill handle, the gutter, header sorting, double click.</summary>
public class DataGridPointerTests
{
    private static void Settle()
    {
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        Dispatcher.UIThread.RunJobs();
    }

    /// <summary>The window point at the center of a body cell.</summary>
    private static Point CellPoint(TKCode.Arc.DataGrid grid, Window window, int row, int column, double dx = 0, double dy = 0)
    {
        var layout = grid.Layout;
        var viewport = grid.Viewport;
        var x = layout.Lefts[column] + layout.Widths[column] / 2 + dx - (column < layout.PinCount ? 0 : viewport.Offset.X);
        var y = viewport.BodyTop + grid.LineOfRow(row) * grid.RowHeight + grid.RowHeight / 2 + dy - viewport.Offset.Y;
        return viewport.TranslatePoint(new Point(x, y), window)!.Value;
    }

    private static void Click(Window window, Point point, RawInputModifiers modifiers = RawInputModifiers.None)
    {
        window.MouseMove(point, modifiers);
        window.MouseDown(point, MouseButton.Left, modifiers);
        window.MouseUp(point, MouseButton.Left, modifiers);
        Settle();
    }

    [AvaloniaFact]
    public void Click_Selects_Shift_Click_Extends_And_Drag_Selects_A_Range()
    {
        var grid = DataGridBehaviorTests.Create();
        var window = DataGridBehaviorTests.Show(grid);
        Settle();
        Click(window, CellPoint(grid, window, 1, 2));
        grid.ActiveCell.ShouldBe(new DataGridCellAddress(1, 2));
        Click(window, CellPoint(grid, window, 3, 3), RawInputModifiers.Shift);
        grid.SelectedRange.ShouldBe(new DataGridCellRange(1, 2, 3, 3));
        var start = CellPoint(grid, window, 0, 1);
        var end = CellPoint(grid, window, 2, 4);
        window.MouseMove(start);
        window.MouseDown(start, MouseButton.Left);
        window.MouseMove(end, RawInputModifiers.LeftMouseButton);
        window.MouseUp(end, MouseButton.Left);
        Settle();
        grid.SelectedRange.ShouldBe(new DataGridCellRange(0, 1, 2, 4));
        grid.ActiveCell.ShouldBe(new DataGridCellAddress(0, 1));
    }

    [AvaloniaFact]
    public void Dragging_The_Fill_Handle_Continues_The_Series()
    {
        var rows = DataGridBehaviorTests.Rows();
        var grid = DataGridBehaviorTests.Create(rows);
        var window = DataGridBehaviorTests.Show(grid);
        Settle();
        grid.Select(new DataGridCellAddress(0, 3), new DataGridCellAddress(1, 3));
        Settle();
        var layout = grid.Layout;
        var handle = CellPoint(grid, window, 1, 3, layout.Widths[3] / 2 - 1, grid.RowHeight / 2 - 1);
        var target = CellPoint(grid, window, 3, 3);
        window.MouseMove(handle);
        window.MouseDown(handle, MouseButton.Left);
        window.MouseMove(target, RawInputModifiers.LeftMouseButton);
        window.MouseUp(target, MouseButton.Left);
        Settle();
        rows.Select(r => r["health"]).ShouldBe([50m, 70m, 90m, 110m]);
        grid.SelectedRange.ShouldBe(new DataGridCellRange(0, 3, 3, 3));
        grid.CanUndo.ShouldBeTrue();
    }

    [AvaloniaFact]
    public void The_Gutter_Selects_Rows_And_Its_Checkbox_Checks_Them()
    {
        var grid = DataGridBehaviorTests.Create();
        grid.ShowRowNumbers = false;
        var window = DataGridBehaviorTests.Show(grid);
        Settle();
        var viewport = grid.Viewport;
        Point Gutter(int row, double x) => viewport.TranslatePoint(
            new Point(x, viewport.BodyTop + grid.LineOfRow(row) * grid.RowHeight + grid.RowHeight / 2), window)!.Value;
        Click(window, Gutter(2, grid.Layout.Gutter / 2));
        grid.CheckedRowKeys.ShouldBe(["r3"]);
        Click(window, Gutter(1, 2));
        grid.SelectedRange.ShouldBe(new DataGridCellRange(1, 0, 1, 4));
    }

    [AvaloniaFact]
    public void Header_Clicks_Sort_And_Shift_Clicks_Add_A_Key()
    {
        var grid = DataGridBehaviorTests.Create();
        var window = DataGridBehaviorTests.Show(grid);
        Settle();
        DataGridHeaderCell Header(string key) => grid.GetVisualDescendants().OfType<DataGridHeaderCell>().First(h => h.Column!.Key == key);
        Point Center(Control control) => control.TranslatePoint(new Point(20, control.Bounds.Height / 2), window)!.Value;
        Click(window, Center(Header("region")));
        grid.SortDescriptions.ShouldBe([DataGridSort.Ascending("region")]);
        Click(window, Center(Header("arr")), RawInputModifiers.Shift);
        grid.SortDescriptions.ShouldBe([DataGridSort.Ascending("region"), DataGridSort.Ascending("arr")]);
        Header("arr").Classes.Contains(":sorted").ShouldBeTrue();
        Header("arr").SortOrderText.ShouldBe("2");
    }

    [AvaloniaFact]
    public void Double_Click_Edits_The_Cell()
    {
        var grid = DataGridBehaviorTests.Create();
        var window = DataGridBehaviorTests.Show(grid);
        Settle();
        var point = CellPoint(grid, window, 2, 0);
        window.MouseMove(point);
        window.MouseDown(point, MouseButton.Left);
        window.MouseUp(point, MouseButton.Left);
        window.MouseDown(point, MouseButton.Left);
        window.MouseUp(point, MouseButton.Left);
        Settle();
        grid.IsEditing.ShouldBeTrue();
        grid.Edit!.Row.ShouldBe(2);
        grid.Viewport.EditorText.ShouldBe("Cobalt Clinic");
    }
}
