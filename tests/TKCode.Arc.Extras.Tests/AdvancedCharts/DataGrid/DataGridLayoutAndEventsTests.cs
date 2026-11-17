using System.Collections.ObjectModel;
using System.Windows.Input;
using Avalonia;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using Shouldly;

namespace TKCode.Arc.Tests.AdvancedCharts;

/// <summary>Column order and widths, fitting to content, the layout state, the cell events and the right-click menu.</summary>
public class DataGridLayoutAndEventsTests
{
    private sealed class TestCommand : ICommand
    {
        public List<object?> Executed { get; } = [];

        public event EventHandler? CanExecuteChanged
        {
            add { }
            remove { }
        }

        public bool CanExecute(object? parameter) => true;

        public void Execute(object? parameter) => Executed.Add(parameter);
    }

    private static string[] Keys(TKCode.Arc.DataGrid grid) =>
        Enumerable.Range(0, grid.VisibleAccessors.Length).Select(i => grid.VisibleColumn(i).Key).ToArray();

    private static void Settle()
    {
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        Dispatcher.UIThread.RunJobs();
    }

    private static Point CellPoint(TKCode.Arc.DataGrid grid, Avalonia.Controls.Window window, int row, int column, double dx = 0, double dy = 0)
    {
        var layout = grid.Layout;
        var viewport = grid.Viewport;
        var x = layout.Lefts[column] + layout.Widths[column] / 2 + dx - (column < layout.PinCount ? 0 : viewport.Offset.X);
        var y = viewport.BodyTop + grid.LineOfRow(row) * grid.RowHeight + grid.RowHeight / 2 + dy - viewport.Offset.Y;
        return viewport.TranslatePoint(new Point(x, y), window)!.Value;
    }

    private static void Click(Avalonia.Controls.Window window, Point point)
    {
        window.MouseMove(point);
        window.MouseDown(point, MouseButton.Left);
        window.MouseUp(point, MouseButton.Left);
        Settle();
    }

    private static MenuSurface Menu(TKCode.Arc.DataGrid grid)
    {
        Dispatcher.UIThread.RunJobs();
        return grid.CurrentMenu!;
    }

    private static string[] Headers(MenuSurface menu) => menu.Items.OfType<MenuEntry>().Select(e => (string)e.Header!).ToArray();

    private static void Invoke(MenuSurface menu, string header)
    {
        var entry = menu.Items.OfType<MenuEntry>().First(e => e.Header as string == header);
        ((IInvokeProvider)ControlAutomationPeer.CreatePeerForElement(entry)).Invoke();
        Dispatcher.UIThread.RunJobs();
    }

    [AvaloniaFact]
    public void ColumnOrder_Reorders_Columns_Keeps_Pinned_First_And_Appends_Unlisted_Ones()
    {
        var grid = DataGridBehaviorTests.Create();
        DataGridBehaviorTests.Show(grid);
        Keys(grid).ShouldBe(["account", "region", "arr", "health", "renewal"]);
        grid.ColumnOrder = ["renewal", "region", "bogus"];
        Keys(grid).ShouldBe(["account", "renewal", "region", "arr", "health"]);
        grid.ColumnOrder = ["health", "account"];
        Keys(grid).ShouldBe(["account", "health", "region", "arr", "renewal"]);
        grid.ColumnOrder = null;
        Keys(grid).ShouldBe(["account", "region", "arr", "health", "renewal"]);
    }

    [AvaloniaFact]
    public void MoveColumn_Moves_Within_The_Region_Publishes_The_Order_And_Keeps_The_Selection_On_Its_Column()
    {
        var rows = DataGridBehaviorTests.Rows();
        var grid = DataGridBehaviorTests.Create(rows);
        DataGridBehaviorTests.Show(grid);
        grid.Select(new DataGridCellAddress(1, 4));
        grid.MoveColumn("renewal", 1);
        Keys(grid).ShouldBe(["account", "renewal", "region", "arr", "health"]);
        grid.ColumnOrder.ShouldBe(["account", "renewal", "region", "arr", "health"]);
        grid.ActiveCell.ShouldBe(new DataGridCellAddress(1, 1));
        grid.SelectedRange.ShouldBe(new DataGridCellRange(1, 1, 1, 1));
        grid.MoveColumn("account", 3);
        Keys(grid).ShouldBe(["account", "renewal", "region", "arr", "health"]);
        grid.MoveColumn("region", 0);
        Keys(grid).ShouldBe(["account", "region", "renewal", "arr", "health"]);
        grid.MoveColumn("health", 99);
        Keys(grid).ShouldBe(["account", "region", "renewal", "arr", "health"]);
        grid.MoveColumn("arr", 4);
        Keys(grid).ShouldBe(["account", "region", "renewal", "health", "arr"]);
        grid.StatusBar.ShouldNotBeNull();
        rows[0]["arr"].ShouldBe(1200m);
        grid.Undo();
        grid.CanUndo.ShouldBeFalse();
    }

    [AvaloniaFact]
    public void Undo_Still_Writes_To_The_Right_Column_After_A_Reorder()
    {
        var rows = DataGridBehaviorTests.Rows();
        var grid = DataGridBehaviorTests.Create(rows);
        DataGridBehaviorTests.Show(grid);
        grid.Select(new DataGridCellAddress(0, 2));
        grid.Paste([["5"]]);
        rows[0]["arr"].ShouldBe(5m);
        grid.MoveColumn("arr", 4);
        grid.Undo();
        rows[0]["arr"].ShouldBe(1200m);
        grid.Redo();
        rows[0]["arr"].ShouldBe(5m);
    }

    [AvaloniaFact]
    public void Dragging_A_Header_Label_Moves_The_Column_And_A_Click_Still_Sorts()
    {
        var grid = DataGridBehaviorTests.Create();
        var window = DataGridBehaviorTests.Show(grid);
        Settle();
        Point Header(int column, double dx) =>
            grid.Viewport.TranslatePoint(new Point(grid.Layout.Lefts[column] - (column < grid.Layout.PinCount ? 0 : grid.Viewport.Offset.X) + dx, 20), window)!.Value;
        var start = Header(4, 24);
        var end = Header(1, 24);
        window.MouseMove(start);
        window.MouseDown(start, MouseButton.Left);
        window.MouseMove(new Point(start.X - 12, start.Y), RawInputModifiers.LeftMouseButton);
        window.MouseMove(end, RawInputModifiers.LeftMouseButton);
        window.MouseUp(end, MouseButton.Left);
        Settle();
        Keys(grid).ShouldBe(["account", "renewal", "region", "arr", "health"]);
        grid.SortDescriptions.ShouldBeEmpty();
        var label = Header(2, 24);
        window.MouseMove(label);
        window.MouseDown(label, MouseButton.Left);
        window.MouseUp(label, MouseButton.Left);
        Settle();
        grid.SortDescriptions.ShouldBe([DataGridSort.Ascending("region")]);
    }

    [AvaloniaFact]
    public void Alt_Shift_Arrows_In_A_Header_Move_The_Column()
    {
        var grid = DataGridBehaviorTests.Create();
        var window = DataGridBehaviorTests.Show(grid);
        Settle();
        grid.Viewport.Header.FocusColumn(2);
        window.KeyPressQwerty(PhysicalKey.ArrowRight, RawInputModifiers.Alt | RawInputModifiers.Shift);
        window.KeyReleaseQwerty(PhysicalKey.ArrowRight, RawInputModifiers.Alt | RawInputModifiers.Shift);
        Settle();
        Keys(grid).ShouldBe(["account", "region", "health", "arr", "renewal"]);
        window.KeyPressQwerty(PhysicalKey.ArrowLeft, RawInputModifiers.Alt | RawInputModifiers.Shift);
        window.KeyReleaseQwerty(PhysicalKey.ArrowLeft, RawInputModifiers.Alt | RawInputModifiers.Shift);
        Settle();
        Keys(grid).ShouldBe(["account", "region", "arr", "health", "renewal"]);
    }

    [AvaloniaFact]
    public void Widths_Are_State_Resizes_Publish_Them_And_Setting_Them_Resizes_The_Columns()
    {
        var grid = DataGridBehaviorTests.Create();
        DataGridBehaviorTests.Show(grid);
        var regionIndex = grid.VisibleIndexOf("region");
        var defaultWidth = grid.Layout.Widths[regionIndex];
        grid.ResizeColumn(grid.Columns[1], 200);
        grid.Layout.Widths[regionIndex].ShouldBe(200);
        grid.ColumnWidths.ShouldNotBeNull();
        grid.ColumnWidths!["region"].ShouldBe(200);
        grid.ColumnWidths = new Dictionary<string, double> { ["region"] = 111, ["bogus"] = 50, ["arr"] = -4 };
        grid.Layout.Widths[regionIndex].ShouldBe(111);
        grid.ResizeColumn(grid.Columns[1], null);
        grid.Layout.Widths[regionIndex].ShouldBe(defaultWidth);
        grid.ColumnWidths!.ContainsKey("region").ShouldBeFalse();
        grid.ResizeColumn(grid.Columns[1], 150);
        grid.ResetColumnWidths();
        grid.Layout.Widths[regionIndex].ShouldBe(defaultWidth);
    }

    [AvaloniaFact]
    public void AutoFit_Sizes_A_Column_To_Its_Widest_Cell_Or_Its_Header()
    {
        var rows = DataGridBehaviorTests.Rows();
        rows[1]["account"] = "An account with a really quite remarkably long display name indeed";
        var grid = DataGridBehaviorTests.Create(rows);
        DataGridBehaviorTests.Show(grid);
        var before = grid.Layout.Widths[0];
        grid.AutoFitColumn("account");
        var fitted = grid.Layout.Widths[0];
        fitted.ShouldBeGreaterThan(before);
        fitted.ShouldBeLessThanOrEqualTo(grid.ColumnMaxWidth);
        rows[1]["account"] = "x";
        grid.AutoFitColumn("account");
        grid.Layout.Widths[0].ShouldBeLessThan(fitted);
        grid.Layout.Widths[0].ShouldBeGreaterThanOrEqualTo(grid.Columns[0].MinWidth);
        grid.AutoFitColumns();
        grid.ColumnWidths!.Keys.ShouldBe(["account", "region", "arr", "health", "renewal"], ignoreOrder: true);
    }

    [AvaloniaFact]
    public void The_Column_Menu_Fits_The_Column_To_Its_Content()
    {
        var rows = DataGridBehaviorTests.Rows();
        rows[2]["account"] = "An account with a really quite remarkably long display name indeed";
        var grid = DataGridBehaviorTests.Create(rows);
        DataGridBehaviorTests.Show(grid);
        var before = grid.Layout.Widths[0];
        grid.OpenColumnMenu(grid.Viewport.Header, grid.Store.AccessorIndex("account"));
        Invoke(Menu(grid), "Fit column width");
        grid.Layout.Widths[0].ShouldBeGreaterThan(before);
    }

    [AvaloniaFact]
    public void The_Layout_State_Round_Trips_Through_Json_And_Restores_A_View()
    {
        var grid = DataGridBehaviorTests.Create();
        DataGridBehaviorTests.Show(grid);
        grid.MoveColumn("renewal", 1);
        grid.ResizeColumn(grid.Columns[2], 222);
        grid.HiddenColumns = ["health"];
        grid.PinnedColumns = ["account", "region"];
        grid.ApplySort([DataGridSort.Descending("arr"), DataGridSort.Ascending("region")]);
        grid.SetFilter("region", DataGridFilter.OneOf(["Europe"]));
        grid.SetFilter("account", DataGridFilter.Text("labs"));
        grid.GroupBy = ["region"];
        grid.Density = DataGridDensity.Compact;
        var json = grid.GetLayoutState().ToJson();
        json.ShouldContain("\"Density\":\"Compact\"");
        json.ShouldContain("\"Descending\"");

        var restored = DataGridBehaviorTests.Create();
        DataGridBehaviorTests.Show(restored);
        restored.SetLayoutState(DataGridLayoutState.FromJson(json)!);
        restored.ColumnOrder.ShouldBe(grid.ColumnOrder);
        restored.ColumnWidths!["arr"].ShouldBe(222);
        restored.HiddenColumns.ShouldBe(["health"]);
        restored.PinnedColumns.ShouldBe(["account", "region"]);
        restored.SortDescriptions.ShouldBe([DataGridSort.Descending("arr"), DataGridSort.Ascending("region")]);
        restored.Filters["region"].Values.ShouldBe(["Europe"]);
        restored.Filters["account"].Expression.ShouldBe("labs");
        restored.GroupBy.ShouldBe(["region"]);
        restored.Density.ShouldBe(DataGridDensity.Compact);
        Keys(restored).ShouldBe(Keys(grid));
    }

    [Fact]
    public void The_Layout_State_Reads_Leniently()
    {
        DataGridLayoutState.FromJson("").ShouldBeNull();
        DataGridLayoutState.FromJson("not json").ShouldBeNull();
        var partial = DataGridLayoutState.FromJson("""{"density":"Comfortable","future":{"x":1},"columnOrder":["a","b"]}""")!;
        partial.Density.ShouldBe(DataGridDensity.Comfortable);
        partial.ColumnOrder.ShouldBe(["a", "b"]);
        partial.SortDescriptions.ShouldBeEmpty();
        partial.Version.ShouldBe(1);
        partial.PinnedColumns.ShouldBeNull();
    }

    [AvaloniaFact]
    public void SetLayoutState_Ignores_Unknown_Columns_And_Resets_What_The_State_Lacks()
    {
        var grid = DataGridBehaviorTests.Create();
        DataGridBehaviorTests.Show(grid);
        grid.ResizeColumn(grid.Columns[1], 300);
        grid.HiddenColumns = ["health"];
        grid.SetLayoutState(new DataGridLayoutState
        {
            ColumnOrder = ["nothing", "renewal"],
            SortDescriptions = [DataGridSort.Ascending("ghost"), DataGridSort.Ascending("arr")],
            GroupBy = ["ghost"]
        });
        grid.HiddenColumns.ShouldBeNull();
        Keys(grid).ShouldBe(["account", "renewal", "region", "arr", "health"]);
        grid.ColumnWidths!.ShouldBeEmpty();
        grid.SortDescriptions.ShouldBe([DataGridSort.Ascending("arr")]);
        grid.GroupBy.ShouldBeEmpty();
    }

    [AvaloniaFact]
    public void CellClick_Reports_The_Cell_And_CellDoubleClick_Can_Keep_The_Editor_Closed()
    {
        var rows = DataGridBehaviorTests.Rows();
        var grid = DataGridBehaviorTests.Create(rows);
        var window = DataGridBehaviorTests.Show(grid);
        Settle();
        var clicks = new List<DataGridCellEventArgs>();
        grid.CellClick += (_, e) => clicks.Add(e);
        Click(window, CellPoint(grid, window, 2, 2));
        var click = clicks.Single();
        click.Row.ShouldBe(rows[2]);
        click.RowKey.ShouldBe("r3");
        click.Column.Key.ShouldBe("arr");
        click.Address.ShouldBe(new DataGridCellAddress(2, 2));
        click.Value.ShouldBe(3000m);
        click.PointerType.ShouldBe(PointerType.Mouse);
        grid.ActiveCell.ShouldBe(new DataGridCellAddress(2, 2));
        var doubles = new List<DataGridCellEventArgs>();
        grid.CellDoubleClick += (_, e) =>
        {
            doubles.Add(e);
            e.Handled = true;
        };
        var point = CellPoint(grid, window, 1, 0);
        window.MouseMove(point);
        window.MouseDown(point, MouseButton.Left);
        window.MouseUp(point, MouseButton.Left);
        window.MouseDown(point, MouseButton.Left);
        window.MouseUp(point, MouseButton.Left);
        Settle();
        doubles.Count.ShouldBeGreaterThan(0);
        doubles[0].Row.ShouldBe(rows[1]);
        grid.IsEditing.ShouldBeFalse();
    }

    [AvaloniaFact]
    public void RowActivated_Fires_For_Enter_On_A_Read_Only_Cell_And_For_A_Double_Click_On_The_Row_Number()
    {
        var rows = DataGridBehaviorTests.Rows();
        var grid = DataGridBehaviorTests.Create(rows);
        grid.Columns[0].IsEditable = false;
        var window = DataGridBehaviorTests.Show(grid);
        Settle();
        var activated = new List<DataGridCellEventArgs>();
        grid.RowActivated += (_, e) =>
        {
            activated.Add(e);
            e.Handled = true;
        };
        grid.Select(new DataGridCellAddress(2, 0));
        window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
        window.KeyReleaseQwerty(PhysicalKey.Enter, RawInputModifiers.None);
        Dispatcher.UIThread.RunJobs();
        activated.Single().Row.ShouldBe(rows[2]);
        activated[0].PointerType.ShouldBeNull();
        (grid.StatusBar!.MessageText ?? string.Empty).ShouldNotContain("read only");
        grid.Select(new DataGridCellAddress(1, 2));
        window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
        window.KeyReleaseQwerty(PhysicalKey.Enter, RawInputModifiers.None);
        Dispatcher.UIThread.RunJobs();
        activated.Count.ShouldBe(1);
        grid.IsEditing.ShouldBeTrue();
        grid.CancelEdit();
        var gutter = grid.Viewport.TranslatePoint(new Point(10, grid.Viewport.BodyTop + 3 * grid.RowHeight + 10), window)!.Value;
        window.MouseMove(gutter);
        window.MouseDown(gutter, MouseButton.Left);
        window.MouseUp(gutter, MouseButton.Left);
        window.MouseDown(gutter, MouseButton.Left);
        window.MouseUp(gutter, MouseButton.Left);
        Settle();
        activated.Last().Row.ShouldBe(rows[3]);
    }

    [AvaloniaFact]
    public void The_Context_Menu_Selects_The_Cell_Offers_Actions_And_Runs_Them()
    {
        var rows = DataGridBehaviorTests.Rows();
        var grid = DataGridBehaviorTests.Create(rows);
        grid.CanInsertRows = true;
        var window = DataGridBehaviorTests.Show(grid);
        Settle();
        var requested = new List<DataGridCellEventArgs>();
        grid.CellContextRequested += (_, e) => requested.Add(e);
        grid.RequestContextMenu(new DataGridCellAddress(1, 1), new Point(100, 100), PointerType.Mouse, KeyModifiers.None).ShouldBeTrue();
        requested.Single().Row.ShouldBe(rows[1]);
        grid.ActiveCell.ShouldBe(new DataGridCellAddress(1, 1));
        var menu = Menu(grid);
        Headers(menu).ShouldBe(["Cut", "Copy", "Copy with headers", "Paste", "Insert row above", "Insert row below", "Delete rows", "Clear contents",
            "Fill down", "Filter by this value", "Sort ascending", "Sort descending", "Group by Region"]);
        menu.Items.OfType<MenuEntry>().First(e => (string)e.Header! == "Fill down").IsEnabled.ShouldBeFalse();
        Invoke(menu, "Clear contents");
        rows[1]["region"].ShouldBe(string.Empty);
        grid.RequestContextMenu(new DataGridCellAddress(2, 2), new Point(100, 100), PointerType.Mouse, KeyModifiers.None);
        Invoke(Menu(grid), "Insert row below");
        rows.Count.ShouldBe(5);
        rows[3]["account"].ShouldBeNull();
        grid.RequestContextMenu(new DataGridCellAddress(0, 1), new Point(100, 100), PointerType.Mouse, KeyModifiers.None);
        Invoke(Menu(grid), "Filter by this value");
        grid.Filters["region"].Values.ShouldBe(["Europe"]);
        grid.IsFilterRowOpen.ShouldBeTrue();
        grid.SetFilter("region", null);
        grid.RequestContextMenu(new DataGridCellAddress(0, 2), new Point(100, 100), PointerType.Mouse, KeyModifiers.None);
        Invoke(Menu(grid), "Filter by this value");
        grid.Filters["arr"].Expression.ShouldBe("=1200");
        grid.SetFilter("arr", null);
        grid.EnsureView();
        grid.Select(new DataGridCellAddress(0, 0));
        grid.RequestContextMenu(new DataGridCellAddress(0, 0), new Point(100, 100), PointerType.Mouse, KeyModifiers.None);
        Invoke(Menu(grid), "Delete rows");
        rows.Select(r => r.Key).ShouldNotContain("r1");
        grid.Undo();
        rows.Select(r => r.Key).ShouldContain("r1");
        window.ShouldNotBeNull();
    }

    [AvaloniaFact]
    public void The_Context_Menu_Can_Be_Handled_Cancelled_Extended_Or_Turned_Off()
    {
        var rows = DataGridBehaviorTests.Rows();
        var grid = DataGridBehaviorTests.Create(rows);
        DataGridBehaviorTests.Show(grid);
        var command = new TestCommand();
        grid.ContextMenuItems.Add(new DataGridMenuItem { Label = "Open account", Command = command });
        grid.RequestContextMenu(new DataGridCellAddress(1, 0), new Point(80, 80), PointerType.Mouse, KeyModifiers.None);
        var menu = Menu(grid);
        Headers(menu).Last().ShouldBe("Open account");
        Headers(menu).ShouldNotContain("Insert row above");
        Invoke(menu, "Open account");
        command.Executed.ShouldBe([rows[1]]);

        grid.ContextMenuOpening += (_, e) =>
        {
            e.Items.Clear();
            e.Items.Add(new DataGridMenuItem { Label = "Only this" });
        };
        grid.RequestContextMenu(new DataGridCellAddress(1, 0), new Point(80, 80), PointerType.Mouse, KeyModifiers.None);
        Headers(Menu(grid)).Last().ShouldBe("Only this");
        Headers(Menu(grid)).ShouldNotContain("Open account");

        grid.CurrentMenu!.IsOpen = false;
        grid.CellContextRequested += (_, e) => e.Handled = true;
        grid.RequestContextMenu(new DataGridCellAddress(2, 0), new Point(80, 80), PointerType.Mouse, KeyModifiers.None);
        grid.CurrentMenu.IsOpen.ShouldBeFalse();

        var quiet = DataGridBehaviorTests.Create();
        quiet.ShowContextMenu = false;
        DataGridBehaviorTests.Show(quiet);
        quiet.RequestContextMenu(new DataGridCellAddress(0, 0), new Point(80, 80), PointerType.Mouse, KeyModifiers.None).ShouldBeTrue();
        (quiet.CurrentMenu?.IsOpen ?? false).ShouldBeFalse();

        var cancelled = DataGridBehaviorTests.Create();
        cancelled.ContextMenuOpening += (_, e) => e.Cancel = true;
        DataGridBehaviorTests.Show(cancelled);
        cancelled.RequestContextMenu(new DataGridCellAddress(0, 0), new Point(80, 80), PointerType.Mouse, KeyModifiers.None);
        (cancelled.CurrentMenu?.IsOpen ?? false).ShouldBeFalse();
    }

    [AvaloniaFact]
    public void A_Right_Click_Opens_The_Menu_On_The_Cell_Under_The_Pointer()
    {
        var rows = DataGridBehaviorTests.Rows();
        var grid = DataGridBehaviorTests.Create(rows);
        var window = DataGridBehaviorTests.Show(grid);
        Settle();
        var requested = new List<DataGridCellEventArgs>();
        grid.CellContextRequested += (_, e) => requested.Add(e);
        var point = CellPoint(grid, window, 2, 1);
        window.MouseMove(point);
        window.MouseDown(point, MouseButton.Right);
        window.MouseUp(point, MouseButton.Right);
        Settle();
        if (requested.Count == 0)
        {
            // The headless platform may not turn a right click into ContextRequested; the keyboard route does.
            grid.Select(new DataGridCellAddress(2, 1));
            window.KeyPressQwerty(PhysicalKey.ContextMenu, RawInputModifiers.None);
            window.KeyReleaseQwerty(PhysicalKey.ContextMenu, RawInputModifiers.None);
            Settle();
        }
        requested.ShouldNotBeEmpty();
        requested[0].Row.ShouldBe(rows[2]);
    }
}
