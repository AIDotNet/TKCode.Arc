using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Shouldly;

namespace TKCode.Arc.Tests.AdvancedCharts;

/// <summary>The grid's behavior contracts: keyboard model, editing, clipboard, undo, rows that never jump, deletes, views.</summary>
public class DataGridBehaviorTests
{
    internal static ObservableCollection<DataGridRow> Rows() =>
    [
        new("r1") { { "account", "Lumen Labs" }, { "region", "Europe" }, { "arr", 1200m }, { "health", 50m }, { "renewal", new DateOnly(2026, 1, 31) } },
        new("r2") { { "account", "Basalt Freight" }, { "region", "Asia Pacific" }, { "arr", 800m }, { "health", 70m }, { "renewal", new DateOnly(2026, 3, 1) } },
        new("r3") { { "account", "Cobalt Clinic" }, { "region", "Europe" }, { "arr", 3000m }, { "health", 90m }, { "renewal", null } },
        new("r4") { { "account", "Delta Dental" }, { "region", "North America" }, { "arr", null }, { "health", 40m }, { "renewal", new DateOnly(2027, 5, 9) } }
    ];

    internal static TKCode.Arc.DataGrid Create(IEnumerable<DataGridRow>? rows = null)
    {
        var grid = new TKCode.Arc.DataGrid
        {
            Label = "Accounts",
            ItemsSource = rows ?? Rows(),
            Culture = CultureInfo.GetCultureInfo("en-US"),
            Width = 900,
            MaxViewportHeight = 360
        };
        grid.Columns.Add(new DataGridColumn("account", "Account") { Width = 180 });
        grid.Columns.Add(new DataGridColumn("region", "Region", DataGridColumnType.Select) { Options = ["Europe", "Asia Pacific", "North America"] });
        grid.Columns.Add(new DataGridColumn("arr", "ARR", DataGridColumnType.Currency));
        grid.Columns.Add(new DataGridColumn("health", "Health", DataGridColumnType.Percent));
        grid.Columns.Add(new DataGridColumn("renewal", "Renews", DataGridColumnType.Date));
        return grid;
    }

    internal static Window Show(TKCode.Arc.DataGrid grid)
    {
        var window = TestHelpers.Show(grid);
        window.Width = 1000;
        window.Height = 700;
        Dispatcher.UIThread.RunJobs();
        grid.UpdateLayout();
        grid.Viewport.Focus();
        Dispatcher.UIThread.RunJobs();
        return window;
    }

    private static void Key(Window window, PhysicalKey key, RawInputModifiers modifiers = RawInputModifiers.None)
    {
        window.KeyPressQwerty(key, modifiers);
        window.KeyReleaseQwerty(key, modifiers);
        Dispatcher.UIThread.RunJobs();
    }

    private static void Type(Window window, string text)
    {
        window.KeyTextInput(text);
        Dispatcher.UIThread.RunJobs();
    }

    [AvaloniaFact]
    public void Shows_Rows_Totals_And_The_First_Column_Pinned()
    {
        var grid = Create();
        Show(grid);
        grid.VisibleRowCount.ShouldBe(4);
        grid.PinCount.ShouldBe(1);
        grid.Layout.PinEdge.ShouldBe(grid.Layout.Gutter + 180);
        grid.Totals[2].ShouldBe(5000m);
        grid.Totals[3].ShouldBe(62.5m);
        grid.Totals[0].ShouldBeNull();
        grid.ActiveCell.ShouldBe(new DataGridCellAddress(0, 0));
    }

    [AvaloniaFact]
    public void Arrows_Move_Shift_Extends_Command_Jumps_And_Escape_Collapses()
    {
        var grid = Create();
        var window = Show(grid);
        Key(window, PhysicalKey.ArrowDown);
        Key(window, PhysicalKey.ArrowRight);
        grid.ActiveCell.ShouldBe(new DataGridCellAddress(1, 1));
        Key(window, PhysicalKey.ArrowDown, RawInputModifiers.Shift);
        Key(window, PhysicalKey.ArrowRight, RawInputModifiers.Shift);
        grid.SelectedRange.ShouldBe(new DataGridCellRange(1, 1, 2, 2));
        Key(window, PhysicalKey.Escape);
        grid.SelectedRange.ShouldBe(new DataGridCellRange(1, 1, 1, 1));
        Key(window, PhysicalKey.End);
        grid.ActiveCell.ShouldBe(new DataGridCellAddress(1, 4));
        Key(window, PhysicalKey.ArrowDown, RawInputModifiers.Meta);
        Key(window, PhysicalKey.ArrowDown, RawInputModifiers.Control);
        grid.ActiveCell!.Value.Row.ShouldBe(3);
        Key(window, PhysicalKey.Home);
        grid.ActiveCell.ShouldBe(new DataGridCellAddress(3, 0));
    }

    [AvaloniaFact]
    public void Typing_Replaces_The_Value_And_Enter_Commits_And_Moves_Down()
    {
        var rows = Rows();
        var grid = Create(rows);
        var window = Show(grid);
        var changes = new List<DataGridCellsChangedEventArgs>();
        grid.CellsChanged += (_, e) => changes.Add(e);
        grid.Select(new DataGridCellAddress(0, 2));
        Type(window, "1");
        grid.IsEditing.ShouldBeTrue();
        Type(window, "500");
        Key(window, PhysicalKey.Enter);
        grid.IsEditing.ShouldBeFalse();
        rows[0]["arr"].ShouldBe(1500m);
        grid.ActiveCell.ShouldBe(new DataGridCellAddress(1, 2));
        changes.Single().Reason.ShouldBe(DataGridChangeReason.Edit);
        changes[0].Changes.Single().OldValue.ShouldBe(1200m);
        grid.Totals[2].ShouldBe(5300m);
        grid.CanUndo.ShouldBeTrue();
    }

    [AvaloniaFact]
    public void An_Unreadable_Number_Keeps_The_Editor_Open_And_Escape_Cancels()
    {
        var rows = Rows();
        var grid = Create(rows);
        var window = Show(grid);
        grid.Select(new DataGridCellAddress(0, 2));
        Type(window, "abc");
        Key(window, PhysicalKey.Enter);
        grid.IsEditing.ShouldBeTrue();
        grid.Edit!.Invalid.ShouldBeTrue();
        Key(window, PhysicalKey.Escape);
        grid.IsEditing.ShouldBeFalse();
        rows[0]["arr"].ShouldBe(1200m);
        grid.CanUndo.ShouldBeFalse();
    }

    [AvaloniaFact]
    public void Cell_Edit_Ending_Can_Refuse_A_Value()
    {
        var rows = Rows();
        var grid = Create(rows);
        var window = Show(grid);
        grid.CellEditEnding += (_, e) =>
        {
            if (e.NewValue is decimal d && d < 0)
            {
                e.Cancel = true;
                e.ErrorMessage = "ARR cannot be negative";
            }
        };
        grid.Select(new DataGridCellAddress(1, 2));
        Type(window, "-5");
        Key(window, PhysicalKey.Tab);
        grid.IsEditing.ShouldBeTrue();
        grid.StatusBar!.MessageText.ShouldBe("ARR cannot be negative");
        grid.CancelEdit();
        rows[1]["arr"].ShouldBe(800m);
    }

    [AvaloniaFact]
    public void An_Edit_Template_Is_Only_Built_With_The_Edit_Context()
    {
        // Closing the editor clears the presenter's content; a template still attached would be built with null (a template that
        // reads context.Value crashed on the click that ended an edit).
        var grid = Create();
        var built = new List<DataGridEditContext?>();
        grid.Columns[3].EditTemplate = new FuncDataTemplate<DataGridEditContext>((context, _) =>
        {
            built.Add(context);
            return new Border { Width = 40, Height = 20 };
        });
        Show(grid);
        grid.Select(new DataGridCellAddress(1, 3));
        grid.BeginEdit();
        grid.IsEditing.ShouldBeTrue();
        grid.CancelEdit();
        grid.IsEditing.ShouldBeFalse();
        grid.Select(new DataGridCellAddress(2, 3));
        grid.BeginEdit();
        grid.IsEditing.ShouldBeTrue();
        grid.CommitEdit(null, null).ShouldBeTrue();
        grid.IsEditing.ShouldBeFalse();
        built.Count.ShouldBe(2);
        built.ShouldAllBe(context => context != null);
    }

    [AvaloniaFact]
    public void Read_Only_Columns_Refuse_Edits_Pastes_And_Clears()
    {
        var rows = Rows();
        var grid = Create(rows);
        grid.Columns[2].IsEditable = false;
        var window = Show(grid);
        grid.Select(new DataGridCellAddress(0, 2));
        Type(window, "9");
        grid.IsEditing.ShouldBeFalse();
        grid.Select(new DataGridCellAddress(0, 1), new DataGridCellAddress(0, 2));
        Key(window, PhysicalKey.Delete);
        rows[0]["arr"].ShouldBe(1200m);
        rows[0]["region"].ShouldBe(string.Empty);
    }

    [AvaloniaFact]
    public void Editing_Never_Moves_Rows_Until_The_Sort_Changes()
    {
        var rows = Rows();
        var grid = Create(rows);
        Show(grid);
        grid.ApplySort([DataGridSort.Descending("arr")]);
        grid.EnsureView();
        Enumerable.Range(0, 4).Select(r => grid.KeyOfRow(r)).ShouldBe(["r3", "r1", "r2", "r4"]);
        grid.WriteCells([(rows.IndexOf(rows[1]), 2, 9999m)], null, DataGridChangeReason.Edit);
        grid.EnsureView();
        Enumerable.Range(0, 4).Select(r => grid.KeyOfRow(r)).ShouldBe(["r3", "r1", "r2", "r4"]);
        grid.ApplySort([DataGridSort.Ascending("arr")]);
        grid.ApplySort([DataGridSort.Descending("arr")]);
        grid.EnsureView();
        Enumerable.Range(0, 4).Select(r => grid.KeyOfRow(r)).ShouldBe(["r2", "r3", "r1", "r4"]);
    }

    [AvaloniaFact]
    public void Header_Clicks_Cycle_And_Shift_Adds_Sort_Keys()
    {
        var grid = Create();
        Show(grid);
        grid.CycleSort("region", add: false);
        grid.SortDescriptions.ShouldBe([DataGridSort.Ascending("region")]);
        grid.CycleSort("arr", add: true);
        grid.SortDescriptions.ShouldBe([DataGridSort.Ascending("region"), DataGridSort.Ascending("arr")]);
        grid.CycleSort("arr", add: true);
        grid.SortDescriptions.ShouldBe([DataGridSort.Ascending("region"), DataGridSort.Descending("arr")]);
        grid.CycleSort("arr", add: true);
        grid.SortDescriptions.ShouldBe([DataGridSort.Ascending("region")]);
        grid.CycleSort("region", add: false);
        grid.CycleSort("region", add: false);
        grid.SortDescriptions.ShouldBeEmpty();
    }

    [AvaloniaFact]
    public void A_Multiple_Selection_Tiles_The_Clipboard_And_Bad_Values_Are_Skipped()
    {
        var rows = Rows();
        var grid = Create(rows);
        Show(grid);
        grid.Select(new DataGridCellAddress(0, 2), new DataGridCellAddress(3, 2));
        grid.Paste([["10"], ["x"]]);
        rows.Select(r => r["arr"]).ShouldBe([10m, 800m, 10m, null]);
        grid.StatusBar!.MessageText!.ShouldContain("skipped 2");
        grid.Select(new DataGridCellAddress(3, 0));
        grid.Paste([["Zephyr", "Europe", "$5", "12%", "2026-02-01", "extra"]]);
        rows[3]["account"].ShouldBe("Zephyr");
        rows[3]["arr"].ShouldBe(5m);
        rows[3]["renewal"].ShouldBe(new DateOnly(2026, 2, 1));
        grid.StatusBar!.MessageText!.ShouldContain("outside the grid");
        grid.SelectedRange.ShouldBe(new DataGridCellRange(3, 0, 3, 4));
    }

    [AvaloniaFact]
    public void Clear_Undo_And_Redo_Restore_And_Reapply_Values()
    {
        var rows = Rows();
        var grid = Create(rows);
        var window = Show(grid);
        grid.Select(new DataGridCellAddress(0, 2), new DataGridCellAddress(1, 3));
        Key(window, PhysicalKey.Delete);
        rows[0]["arr"].ShouldBeNull();
        rows[1]["health"].ShouldBeNull();
        grid.Undo();
        rows[0]["arr"].ShouldBe(1200m);
        rows[1]["health"].ShouldBe(70m);
        grid.CanRedo.ShouldBeTrue();
        grid.Redo();
        rows[0]["arr"].ShouldBeNull();
        grid.UndoLimit = 0;
        grid.CanUndo.ShouldBeFalse();
    }

    [AvaloniaFact]
    public void Fill_Down_Copies_The_First_Row_And_The_Fill_Handle_Continues_Series()
    {
        var rows = Rows();
        var grid = Create(rows);
        var window = Show(grid);
        grid.Select(new DataGridCellAddress(0, 1), new DataGridCellAddress(2, 1));
        Key(window, PhysicalKey.D, RawInputModifiers.Control | RawInputModifiers.Meta);
        rows.Take(3).Select(r => r["region"]).ShouldBe(["Europe", "Europe", "Europe"]);
        grid.CompleteFill(new DataGridCellRange(0, 3, 1, 3), new DataGridCellRange(0, 3, 3, 3));
        rows.Select(r => r["health"]).ShouldBe([50m, 70m, 90m, 110m]);
        grid.CompleteFill(new DataGridCellRange(0, 4, 1, 4), new DataGridCellRange(0, 4, 2, 4));
        rows[2]["renewal"].ShouldBe(new DateOnly(2026, 3, 30));
    }

    [AvaloniaFact]
    public void Deleting_Checked_Rows_Removes_Them_And_Undo_Puts_Them_Back()
    {
        var rows = Rows();
        var grid = Create(rows);
        Show(grid);
        grid.ToggleRowChecked(1, extend: false);
        grid.ToggleRowChecked(3, extend: true);
        grid.CheckedRowKeys.ShouldBe(["r2", "r3", "r4"]);
        grid.Toolbar!.IsBulk.ShouldBeTrue();
        var deleted = new List<DataGridRowsEventArgs>();
        grid.RowsDeleted += (_, e) => deleted.Add(e);
        grid.DeleteChecked();
        rows.Select(r => r.Key).ShouldBe(["r1"]);
        deleted.Single().Indices.ShouldBe([1, 2, 3]);
        grid.CheckedRowKeys.ShouldBeEmpty();
        grid.StatusBar!.HasAction.ShouldBeTrue();
        grid.StatusBar.InvokeAction();
        rows.Select(r => r.Key).ShouldBe(["r1", "r2", "r3", "r4"]);
    }

    [AvaloniaFact]
    public void Filters_And_Search_Narrow_The_Rows_And_Clear_Restores_Them()
    {
        var grid = Create();
        Show(grid);
        grid.SetFilter("arr", DataGridFilter.Text(">1000"));
        grid.VisibleRowCount.ShouldBe(2);
        grid.SetFilter("region", DataGridFilter.OneOf(["Europe"]));
        grid.VisibleRowCount.ShouldBe(2);
        grid.SearchText = "cobalt";
        grid.EnsureView();
        grid.VisibleRowCount.ShouldBe(1);
        grid.ActiveFilterCount.ShouldBe(2);
        grid.RowCountText().ShouldBe("1 of 4 rows");
        grid.ClearAllFilters();
        grid.EnsureView();
        grid.VisibleRowCount.ShouldBe(4);
        grid.SearchText.ShouldBe(string.Empty);
    }

    [AvaloniaFact]
    public void Grouping_Adds_Group_Rows_With_Subtotals_That_Fold()
    {
        var grid = Create();
        Show(grid);
        grid.GroupBy = ["region"];
        grid.EnsureView();
        grid.View.Groups!.Select(g => g.Value).ShouldBe(["Europe", "Asia Pacific", "North America"]);
        grid.LineCount.ShouldBe(7);
        var europe = grid.View.Groups![0];
        grid.SubtotalOf(europe, grid.Store.AccessorIndex("arr")).ShouldBe(4200m);
        grid.ToggleGroup(europe);
        grid.LineCount.ShouldBe(5);
        grid.DisplayCount.ShouldBe(2);
        grid.Totals[2].ShouldBe(5000m);
        grid.SetAllGroupsCollapsed(false);
        grid.DisplayCount.ShouldBe(4);
        var window = (Window)TopLevel.GetTopLevel(grid)!;
        grid.Viewport.Focus();
        grid.Select(new DataGridCellAddress(1, 0));
        window.KeyPressQwerty(PhysicalKey.ArrowLeft, RawInputModifiers.Alt);
        Dispatcher.UIThread.RunJobs();
        grid.IsCollapsed(europe).ShouldBeTrue();
        window.KeyPressQwerty(PhysicalKey.ArrowRight, RawInputModifiers.Alt);
        Dispatcher.UIThread.RunJobs();
        grid.IsCollapsed(europe).ShouldBeFalse();
    }

    [AvaloniaFact]
    public void Hidden_And_Pinned_Columns_Reorder_The_Visible_Columns()
    {
        var grid = Create();
        Show(grid);
        grid.TogglePinned("arr");
        grid.VisibleAccessors.Select(a => grid.ColumnOf(a).Key).ShouldBe(["account", "arr", "region", "health", "renewal"]);
        grid.PinCount.ShouldBe(2);
        grid.ToggleHidden("region");
        grid.HiddenColumns.ShouldBe(["region"]);
        grid.VisibleAccessors.Length.ShouldBe(4);
        grid.ShowAllColumns();
        grid.VisibleAccessors.Length.ShouldBe(5);
    }

    [AvaloniaFact]
    public void Only_Columns_In_View_Get_Header_Controls()
    {
        var rows = Enumerable.Range(0, 50).Select(i =>
        {
            var row = new DataGridRow($"r{i}");
            for (var c = 0; c < 60; c++)
            {
                row[$"c{c}"] = (decimal)(i * c);
            }
            return row;
        }).ToList();
        var grid = new TKCode.Arc.DataGrid { ItemsSource = rows, Width = 800, MaxViewportHeight = 300 };
        for (var c = 0; c < 60; c++)
        {
            grid.Columns.Add(new DataGridColumn($"c{c}", $"C{c}", DataGridColumnType.Number) { Width = 120 });
        }
        Show(grid);
        var headers = grid.GetVisualDescendants().OfType<DataGridHeaderCell>().Count();
        headers.ShouldBeGreaterThan(5);
        headers.ShouldBeLessThan(20);
    }

    public sealed class Account : System.ComponentModel.INotifyPropertyChanged
    {
        private decimal _arr;

        public string Name { get; set; } = string.Empty;

        public decimal Arr
        {
            get => _arr;
            set
            {
                _arr = value;
                PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(nameof(Arr)));
            }
        }

        public double Health { get; set; }

        public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;
    }

    [AvaloniaFact]
    public void Typed_Rows_Read_And_Write_Through_Accessors()
    {
        var accounts = new ObservableCollection<Account> { new() { Name = "Lumen", Arr = 10, Health = 0.5 }, new() { Name = "Basalt", Arr = 20 } };
        var grid = new TKCode.Arc.DataGrid { ItemsSource = accounts, Width = 600, RowKeySelector = a => ((Account)a).Name };
        grid.Columns.Add(DataGridColumn.Create<Account>("name", "Name", a => a.Name, (a, v) => a.Name = v as string ?? string.Empty));
        grid.Columns.Add(DataGridColumn.Create<Account>("arr", "ARR", a => a.Arr, (a, v) => a.Arr = v is decimal d ? d : 0, DataGridColumnType.Number));
        grid.Columns.Add(DataGridColumn.Create<Account>("health", "Health", a => a.Health, type: DataGridColumnType.Number));
        Show(grid);
        grid.ValueAt(0, 1).ShouldBe(10m);
        grid.ValueAt(0, 2).ShouldBe(0.5m);
        grid.WriteCells([(0, 1, 15m), (0, 2, 9m)], null, DataGridChangeReason.Edit).ShouldBe(1);
        accounts[0].Arr.ShouldBe(15m);
        accounts[1].Arr = 99;
        grid.ValueAt(1, 1).ShouldBe(99m);
        grid.Totals[1].ShouldBe(114m);
    }

    [AvaloniaFact]
    public void Controlled_View_State_Round_Trips()
    {
        var grid = Create();
        Show(grid);
        grid.CheckedRowKeys = ["r4", "r1"];
        grid.IsChecked("r1").ShouldBeTrue();
        grid.CheckedCount.ShouldBe(2);
        grid.Density = DataGridDensity.Compact;
        grid.RowHeight.ShouldBe(32);
        grid.Filters = new Dictionary<string, DataGridFilter> { ["health"] = DataGridFilter.Text(">=70") };
        grid.EnsureView();
        grid.VisibleRowCount.ShouldBe(2);
        grid.HiddenColumns = ["account"];
        grid.PinCount.ShouldBe(0);
        grid.ToggleAllChecked();
        grid.CheckedRowKeys.ShouldBe(["r1", "r2", "r3", "r4"]);
    }

    /// <summary>An observable collection that counts its listeners.</summary>
    private sealed class CountedRows(IEnumerable<DataGridRow> rows) : ObservableCollection<DataGridRow>(rows)
    {
        public int Listeners { get; private set; }

        public override event NotifyCollectionChangedEventHandler? CollectionChanged
        {
            add
            {
                base.CollectionChanged += value;
                Listeners++;
            }
            remove
            {
                base.CollectionChanged -= value;
                Listeners--;
            }
        }
    }

    [AvaloniaFact]
    public void A_Grid_Is_Collected_When_Its_Rows_Were_Swapped_While_It_Was_Away()
    {
        var rows = Rows();
        var weak = TestHelpers.ShowAndClose(() => Create(), grid =>
        {
            // A cached page: the grid leaves the tree, then its view model swaps the collection behind it.
            ((Decorator)grid.Parent!).Child = null;
            grid.ItemsSource = rows;
        });
        TestHelpers.Collect(weak);
        weak.IsAlive.ShouldBeFalse();
        rows.Add(new DataGridRow("r5") { { "account", "Echo Energy" } });
        rows[0]["arr"] = 1m;
    }

    [AvaloniaFact]
    public void The_First_Show_Reads_The_Rows_Once_And_Follows_What_Changed_Before_It()
    {
        // Reading 100k rows twice doubled the first show: the grid follows its rows from the moment they are set, so
        // showing it has nothing to catch up with.
        var rows = new CountedRows(Rows());
        var grid = Create(rows);
        var version = grid.Store.Version;
        rows.Listeners.ShouldBe(1);
        rows[0]["account"] = "Lumen Group";
        var window = Show(grid);
        try
        {
            grid.Store.Version.ShouldBe(version);
            grid.Store.ItemAt(0).ShouldBeSameAs(rows[0]);
            grid.EnsureView();
            grid.VisibleRowCount.ShouldBe(4);
            rows.Add(new DataGridRow("r5") { { "account", "Echo Energy" } });
            Dispatcher.UIThread.RunJobs();
            grid.EnsureView();
            grid.VisibleRowCount.ShouldBe(5);
            rows.Listeners.ShouldBe(1);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Grid_Put_Back_Shows_What_Changed_While_It_Was_Away_And_Follows_Its_Rows_Again()
    {
        var first = new CountedRows(Rows());
        var grid = Create(first);
        var host = new Border { Child = grid };
        TestHelpers.Show(host);
        grid.EnsureView();
        grid.VisibleRowCount.ShouldBe(4);
        first.Listeners.ShouldBe(1);

        // Away, the same rows change.
        host.Child = null;
        first.Listeners.ShouldBe(0);
        first.RemoveAt(3);
        first[0]["account"] = "Lumen Group";
        host.Child = grid;
        Dispatcher.UIThread.RunJobs();
        first.Listeners.ShouldBe(1);
        grid.EnsureView();
        grid.VisibleRowCount.ShouldBe(3);
        grid.CellText(0, 0).ShouldBe("Lumen Group");

        // Away, the view model swaps in other rows and changes them.
        host.Child = null;
        var second = new CountedRows(Rows());
        grid.ItemsSource = second;
        second.Listeners.ShouldBe(0);
        second.RemoveAt(0);
        second[0]["account"] = "Basalt Group";
        host.Child = grid;
        Dispatcher.UIThread.RunJobs();
        first.Listeners.ShouldBe(0);
        second.Listeners.ShouldBe(1);
        grid.EnsureView();
        grid.VisibleRowCount.ShouldBe(3);
        grid.CellText(0, 0).ShouldBe("Basalt Group");

        // Back in the tree it follows the rows again, once, however often it went away; the old rows are left alone.
        host.Child = null;
        host.Child = grid;
        Dispatcher.UIThread.RunJobs();
        second.Listeners.ShouldBe(1);
        second.Add(new DataGridRow("r5") { { "account", "Echo Energy" } });
        var edits = grid.EditVersion;
        second[1]["account"] = "Cobalt Group";
        grid.EditVersion.ShouldBe(edits + 1);
        first.Add(new DataGridRow("r9") { { "account", "Old" } });
        grid.EnsureView();
        grid.VisibleRowCount.ShouldBe(4);
        grid.CellText(1, 0).ShouldBe("Cobalt Group");
        grid.CellText(3, 0).ShouldBe("Echo Energy");

        // Swapping while shown moves the listener at once.
        grid.ItemsSource = first;
        first.Listeners.ShouldBe(1);
        second.Listeners.ShouldBe(0);
    }

    private sealed class Server(int count) : IDataGridDataSource
    {
        public List<DataGridRowsRequest> Requests { get; } = new();

        public int BlockSize => 50;

        public Task<DataGridRowsResult> GetRowsAsync(DataGridRowsRequest request, CancellationToken cancellation)
        {
            Requests.Add(request);
            var rows = Enumerable.Range(request.Start, Math.Max(0, Math.Min(request.End, count) - request.Start))
                .Select(i => (object)new DataGridRow($"s{i}") { { "account", $"Server {i}" }, { "arr", (decimal)i } })
                .ToList();
            return Task.FromResult(new DataGridRowsResult(rows, count, new Dictionary<string, decimal?> { ["arr"] = 42m }));
        }
    }

    [AvaloniaFact]
    public void A_Data_Source_Loads_Blocks_In_View_And_Is_Read_Only()
    {
        var server = new Server(1000);
        var grid = new TKCode.Arc.DataGrid { DataSource = server, Width = 600, MaxViewportHeight = 300 };
        grid.Columns.Add(new DataGridColumn("account", "Account"));
        grid.Columns.Add(new DataGridColumn("arr", "ARR", DataGridColumnType.Number));
        var window = Show(grid);
        Dispatcher.UIThread.RunJobs();
        grid.UpdateLayout();
        Dispatcher.UIThread.RunJobs();
        grid.VisibleRowCount.ShouldBe(1000);
        grid.CellText(3, 0).ShouldBe("Server 3");
        grid.Totals[1].ShouldBe(42m);
        server.Requests.ShouldContain(r => r.Start == 0 && r.End == 50);
        grid.Select(new DataGridCellAddress(2, 1));
        Type(window, "5");
        grid.IsEditing.ShouldBeFalse();
        grid.SortDescriptions = [DataGridSort.Descending("arr")];
        grid.EnsureView();
        grid.UpdateLayout();
        Dispatcher.UIThread.RunJobs();
        server.Requests.Last().Sort.ShouldBe([DataGridSort.Descending("arr")]);
    }

    /// <summary>A data source that answers only when the test says so.</summary>
    private sealed class SlowServer : IDataGridDataSource
    {
        public List<(TaskCompletionSource<DataGridRowsResult> Reply, CancellationToken Cancellation)> Requests { get; } = new();

        public int BlockSize => 50;

        public Task<DataGridRowsResult> GetRowsAsync(DataGridRowsRequest request, CancellationToken cancellation)
        {
            var reply = new TaskCompletionSource<DataGridRowsResult>();
            Requests.Add((reply, cancellation));
            return reply.Task;
        }

        public void Answer(int index) => Requests[index].Reply.SetResult(new DataGridRowsResult(
            Enumerable.Range(0, 50).Select(i => (object)new DataGridRow($"s{i}") { { "account", $"Server {i}" } }).ToList(), 1000));
    }

    [AvaloniaFact]
    public void Dropped_Data_Source_Requests_Are_Cancelled_And_Their_Late_Answers_Ignored()
    {
        var server = new SlowServer();
        var grid = new TKCode.Arc.DataGrid { DataSource = server, Width = 600, MaxViewportHeight = 300 };
        grid.Columns.Add(new DataGridColumn("account", "Account"));
        grid.Columns.Add(new DataGridColumn("arr", "ARR", DataGridColumnType.Number));
        var host = new Border { Child = grid };
        TestHelpers.Show(host);
        grid.UpdateLayout();
        Dispatcher.UIThread.RunJobs();
        var shown = server.Requests.Count;
        shown.ShouldBeGreaterThan(0);
        server.Requests.Take(shown - 1).ShouldAllBe(r => r.Cancellation.IsCancellationRequested);

        // A new sort drops the request in flight and asks again; leaving the tree drops that one too.
        grid.SortDescriptions = [DataGridSort.Descending("arr")];
        grid.UpdateLayout();
        Dispatcher.UIThread.RunJobs();
        server.Requests.Count.ShouldBe(shown + 1);
        server.Requests[shown - 1].Cancellation.IsCancellationRequested.ShouldBeTrue();
        host.Child = null;
        server.Requests[shown].Cancellation.IsCancellationRequested.ShouldBeTrue();
        for (var i = 0; i <= shown; i++)
        {
            server.Answer(i);
        }
        Dispatcher.UIThread.RunJobs();
        grid.VisibleRowCount.ShouldNotBe(1000);

        // Back in the tree it asks again and shows the answer.
        host.Child = grid;
        grid.UpdateLayout();
        Dispatcher.UIThread.RunJobs();
        server.Requests.Count.ShouldBe(shown + 2);
        server.Answer(shown + 1);
        Dispatcher.UIThread.RunJobs();
        grid.EnsureView();
        grid.VisibleRowCount.ShouldBe(1000);
        grid.CellText(3, 0).ShouldBe("Server 3");
    }
}
