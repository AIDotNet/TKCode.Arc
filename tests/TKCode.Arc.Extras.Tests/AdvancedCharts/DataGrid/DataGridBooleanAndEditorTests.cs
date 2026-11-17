using System.Collections.ObjectModel;
using System.IO.Compression;
using System.Text;
using System.Windows.Input;
using System.Xml.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using Shouldly;

namespace TKCode.Arc.Tests.AdvancedCharts;

/// <summary>Boolean columns, the actions renderer and custom cell editors.</summary>
public class DataGridBooleanAndEditorTests
{
    private sealed class TestCommand(Func<object?, bool>? canExecute = null) : ICommand
    {
        public List<object?> Executed { get; } = [];

        public event EventHandler? CanExecuteChanged
        {
            add { }
            remove { }
        }

        public bool CanExecute(object? parameter) => canExecute?.Invoke(parameter) ?? true;

        public void Execute(object? parameter) => Executed.Add(parameter);
    }

    private static ObservableCollection<DataGridRow> Rows() =>
    [
        new("a") { { "account", "Lumen" }, { "active", true }, { "score", 10m } },
        new("b") { { "account", "Basalt" }, { "active", false }, { "score", 20m } },
        new("c") { { "account", "Cobalt" }, { "active", null }, { "score", 30m } },
        new("d") { { "account", "Delta" }, { "active", true }, { "score", 40m } }
    ];

    private static TKCode.Arc.DataGrid Grid(ObservableCollection<DataGridRow> rows, bool activeEditable = true)
    {
        var grid = new TKCode.Arc.DataGrid
        {
            Label = "Accounts",
            ItemsSource = rows,
            Culture = System.Globalization.CultureInfo.GetCultureInfo("en-US"),
            Width = 800,
            MaxViewportHeight = 360
        };
        grid.Columns.Add(new DataGridColumn("account", "Account") { Width = 160 });
        grid.Columns.Add(new DataGridColumn("active", "Active", DataGridColumnType.Boolean) { Width = 100, IsEditable = activeEditable });
        grid.Columns.Add(new DataGridColumn("score", "Score", DataGridColumnType.Number) { Width = 120 });
        return grid;
    }

    private static void Settle()
    {
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        Dispatcher.UIThread.RunJobs();
    }

    private static Point CellPoint(TKCode.Arc.DataGrid grid, Window window, int row, int column, double dx = 0, double dy = 0)
    {
        var layout = grid.Layout;
        var viewport = grid.Viewport;
        var x = layout.Lefts[column] + layout.Widths[column] / 2 + dx - (column < layout.PinCount ? 0 : viewport.Offset.X);
        var y = viewport.BodyTop + grid.LineOfRow(row) * grid.RowHeight + grid.RowHeight / 2 + dy - viewport.Offset.Y;
        return viewport.TranslatePoint(new Point(x, y), window)!.Value;
    }

    private static void Click(Window window, Point point)
    {
        window.MouseMove(point);
        window.MouseDown(point, MouseButton.Left);
        window.MouseUp(point, MouseButton.Left);
        Settle();
    }

    private static void Key(Window window, PhysicalKey key, RawInputModifiers modifiers = RawInputModifiers.None)
    {
        window.KeyPressQwerty(key, modifiers);
        window.KeyReleaseQwerty(key, modifiers);
        Dispatcher.UIThread.RunJobs();
    }

    [Theory]
    [InlineData("true", true)]
    [InlineData(" YES ", true)]
    [InlineData("Y", true)]
    [InlineData("1", true)]
    [InlineData("on", true)]
    [InlineData("是", true)]
    [InlineData("√", true)]
    [InlineData("false", false)]
    [InlineData("No", false)]
    [InlineData("0", false)]
    [InlineData("off", false)]
    [InlineData("否", false)]
    [InlineData("×", false)]
    public void TryParseBoolean_Reads_Common_Words_In_English_And_Chinese(string text, bool expected)
    {
        DataGridValueParser.TryParseBoolean(text, out var value).ShouldBeTrue();
        value.ShouldBe(expected);
    }

    [Theory]
    [InlineData("")]
    [InlineData("maybe")]
    [InlineData("2")]
    [InlineData(null)]
    public void TryParseBoolean_Refuses_Anything_Else(string? text) => DataGridValueParser.TryParseBoolean(text, out _).ShouldBeFalse();

    [Fact]
    public void Boolean_Values_Normalize_Format_Parse_Sort_And_Export_As_Yes_Or_No()
    {
        var format = new DataGridCellFormat(DataGridColumnType.Boolean);
        DataGridValueParser.Normalize("yes", DataGridColumnType.Boolean).ShouldBe(true);
        DataGridValueParser.Normalize(0, DataGridColumnType.Boolean).ShouldBe(false);
        DataGridValueParser.Normalize(2.5m, DataGridColumnType.Boolean).ShouldBe(true);
        DataGridValueParser.Normalize("banana", DataGridColumnType.Boolean).ShouldBe("banana");
        DataGridValueParser.Normalize(null, DataGridColumnType.Boolean).ShouldBeNull();
        DataGridValueFormat.Format(true, format, System.Globalization.CultureInfo.GetCultureInfo("en-US")).ShouldBe("Yes");
        DataGridValueFormat.Format(false, format, System.Globalization.CultureInfo.GetCultureInfo("zh-CN")).ShouldBe("否");
        DataGridValueFormat.Format(true, new DataGridCellFormat(DataGridColumnType.Text), System.Globalization.CultureInfo.GetCultureInfo("en-US")).ShouldBe("True");
        DataGridValueParser.TryParse("n", format, null, null, out var parsed).ShouldBeTrue();
        parsed.ShouldBe(false);
        DataGridValueParser.TryParse("maybe", format, null, null, out _).ShouldBeFalse();
        DataGridValueParser.TryParse("", format, null, null, out var empty).ShouldBeTrue();
        empty.ShouldBeNull();
        DataGridSorting.Compare(false, true, null).ShouldBeLessThan(0);
        DataGridCsv.Raw(true).ShouldBe("TRUE");
        DataGridCsv.ToCsv(["On"], [[true], [false], [null]]).ShouldBe("On\r\nTRUE\r\nFALSE\r\n");
    }

    [Fact]
    public void Xlsx_Writes_Booleans_As_Boolean_Cells()
    {
        DataGridXlsxColumn[] columns = [new("On", new DataGridCellFormat(DataGridColumnType.Boolean), DataGridAggregate.None, 80)];
        var bytes = DataGridXlsx.ToBytes(columns, [DataGridExportNode.Row([true]), DataGridExportNode.Row([false])], new DataGridXlsxOptions { Totals = false });
        using var zip = new ZipArchive(new MemoryStream(bytes), ZipArchiveMode.Read);
        using var stream = zip.GetEntry("xl/worksheets/sheet1.xml")!.Open();
        XNamespace main = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
        var cells = XDocument.Load(stream).Descendants(main + "c").ToDictionary(c => c.Attribute("r")!.Value);
        cells["A2"].Attribute("t")!.Value.ShouldBe("b");
        cells["A2"].Value.ShouldBe("1");
        cells["A3"].Value.ShouldBe("0");
    }

    [AvaloniaFact]
    public void A_Click_On_The_Checkbox_Flips_The_Value_As_One_Undo_Step_And_Keeps_The_Selection()
    {
        var rows = Rows();
        var grid = Grid(rows);
        var window = DataGridBehaviorTests.Show(grid);
        Settle();
        var changes = new List<DataGridCellsChangedEventArgs>();
        grid.CellsChanged += (_, e) => changes.Add(e);
        var start = grid.ActiveCell;
        Click(window, CellPoint(grid, window, 1, 1));
        rows[1]["active"].ShouldBe(true);
        changes.Single().Reason.ShouldBe(DataGridChangeReason.Edit);
        changes.Single().Changes.Single().NewValue.ShouldBe(true);
        grid.ActiveCell.ShouldBe(start);
        Click(window, CellPoint(grid, window, 0, 1));
        rows[0]["active"].ShouldBe(false);
        Click(window, CellPoint(grid, window, 2, 1));
        rows[2]["active"].ShouldBe(true);
        grid.Undo();
        rows[2]["active"].ShouldBeNull();
        grid.Redo();
        rows[2]["active"].ShouldBe(true);
        Click(window, CellPoint(grid, window, 1, 1, dx: 40));
        rows[1]["active"].ShouldBe(true);
        grid.ActiveCell.ShouldBe(new DataGridCellAddress(1, 1));
    }

    [AvaloniaFact]
    public void A_Read_Only_Boolean_Column_Does_Not_Flip_And_Double_Click_Never_Opens_An_Editor()
    {
        var rows = Rows();
        var grid = Grid(rows, activeEditable: false);
        var window = DataGridBehaviorTests.Show(grid);
        Settle();
        Click(window, CellPoint(grid, window, 1, 1));
        rows[1]["active"].ShouldBe(false);
        var editable = Grid(Rows());
        var second = DataGridBehaviorTests.Show(editable);
        Settle();
        var point = CellPoint(editable, second, 3, 1, dx: 38);
        second.MouseMove(point);
        second.MouseDown(point, MouseButton.Left);
        second.MouseUp(point, MouseButton.Left);
        second.MouseDown(point, MouseButton.Left);
        second.MouseUp(point, MouseButton.Left);
        Settle();
        editable.IsEditing.ShouldBeFalse();
    }

    [AvaloniaFact]
    public void Space_Enter_And_Typing_Set_The_Active_Boolean_Cell_Without_An_Editor()
    {
        var rows = Rows();
        var grid = Grid(rows);
        var window = DataGridBehaviorTests.Show(grid);
        Settle();
        grid.Select(new DataGridCellAddress(1, 1));
        window.KeyTextInput(" ");
        Dispatcher.UIThread.RunJobs();
        rows[1]["active"].ShouldBe(true);
        Key(window, PhysicalKey.Enter);
        rows[1]["active"].ShouldBe(false);
        window.KeyTextInput("y");
        Dispatcher.UIThread.RunJobs();
        rows[1]["active"].ShouldBe(true);
        window.KeyTextInput("x");
        Dispatcher.UIThread.RunJobs();
        rows[1]["active"].ShouldBe(true);
        grid.IsEditing.ShouldBeFalse();
    }

    [AvaloniaFact]
    public void CellEditEnding_Can_Refuse_A_Flip_And_Paste_Reads_Yes_And_No()
    {
        var rows = Rows();
        var grid = Grid(rows);
        var window = DataGridBehaviorTests.Show(grid);
        Settle();
        grid.CellEditEnding += (_, e) =>
        {
            if (e.NewValue is false)
            {
                e.Cancel = true;
                e.ErrorMessage = "Stays active";
            }
        };
        Click(window, CellPoint(grid, window, 0, 1));
        rows[0]["active"].ShouldBe(true);
        grid.StatusBar!.MessageText!.ShouldContain("Stays active");
        grid.Select(new DataGridCellAddress(1, 1), new DataGridCellAddress(3, 1));
        grid.Paste([["yes"], ["0"], ["maybe"]]);
        rows[1]["active"].ShouldBe(true);
        rows[2]["active"].ShouldBe(false);
        rows[3]["active"].ShouldBe(true);
        grid.StatusBar!.MessageText!.ShouldContain("skipped 1");
    }

    [AvaloniaFact]
    public void Boolean_Columns_Sort_Group_And_Filter_Through_The_Checklist_Values()
    {
        var rows = Rows();
        var grid = Grid(rows);
        DataGridBehaviorTests.Show(grid);
        grid.ApplySort([DataGridSort.Descending("active")]);
        grid.EnsureView();
        grid.Store.Count.ShouldBe(4);
        Enumerable.Range(0, 4).Select(i => grid.ValueAt(i, grid.Layout.Accessors[1])).ShouldBe([true, true, false, null]);
        grid.ApplySort([]);
        grid.SetFilter("active", DataGridFilter.OneOf(["false", ""]));
        grid.EnsureView();
        grid.VisibleRowCount.ShouldBe(2);
        grid.FilterLabelOf(grid.Columns[1], "true").ShouldBe("Yes");
        grid.FilterLabelOf(grid.Columns[1], "false").ShouldBe("No");
        grid.OptionsOf(grid.Layout.Accessors[1]).ShouldBe(["true", "false"]);
        grid.SetFilter("active", null);
        grid.GroupBy = ["active"];
        grid.EnsureView();
        grid.View.Groups!.Select(g => g.Rows.Length).Order().ShouldBe([1, 1, 2]);
    }

    [AvaloniaFact]
    public void Booleans_Copy_As_Text_And_Export_As_True_And_False()
    {
        var rows = Rows();
        var grid = Grid(rows);
        DataGridBehaviorTests.Show(grid);
        grid.Matrix(new DataGridCellRange(0, 1, 2, 1), header: false, formatted: false).Select(r => r[0]).ShouldBe(["TRUE", "FALSE", ""]);
        grid.Matrix(new DataGridCellRange(0, 1, 2, 1), header: false, formatted: true).Select(r => r[0]).ShouldBe(["Yes", "No", ""]);
        grid.EnsureView();
        using var csv = new MemoryStream();
        grid.WriteExport(csv, DataGridExportFormat.Csv, grid.View.Filtered.Select(i => grid.Store.ItemAt(i)).ToArray(), grouped: false);
        Encoding.UTF8.GetString(csv.ToArray()).ShouldContain("Lumen,TRUE,10");
    }

    [AvaloniaFact]
    public void Typed_Rows_Read_And_Write_A_Boolean_Property()
    {
        var items = new ObservableCollection<Flagged> { new("a", true), new("b", false) };
        var grid = new TKCode.Arc.DataGrid { ItemsSource = items, RowKeySelector = o => ((Flagged)o).Name, Width = 600 };
        grid.Columns.Add(DataGridColumn.Create<Flagged>("name", "Name", f => f.Name));
        grid.Columns.Add(DataGridColumn.Create<Flagged>("on", "On", f => f.On, (f, v) => f.On = v is true, DataGridColumnType.Boolean));
        var window = DataGridBehaviorTests.Show(grid);
        Settle();
        Click(window, CellPoint(grid, window, 1, 1));
        items[1].On.ShouldBeTrue();
        Click(window, CellPoint(grid, window, 0, 1));
        items[0].On.ShouldBeFalse();
    }

    private sealed class Flagged(string name, bool on)
    {
        public string Name { get; } = name;

        public bool On { get; set; } = on;
    }

    [AvaloniaFact]
    public void Action_Buttons_Run_Their_Command_With_The_Row_Raise_CellInvoked_And_Respect_CanExecute()
    {
        var rows = Rows();
        var grid = Grid(rows);
        var open = new TestCommand();
        var delete = new TestCommand(row => !ReferenceEquals(row, rows[2]));
        var actions = new ActionsRenderer();
        actions.Actions.Add(new DataGridCellAction { Label = "open", Icon = ArcIcons.Globe, Command = open });
        actions.Actions.Add(new DataGridCellAction { Label = "delete", Icon = ArcIcons.Check, IsDanger = true, Command = delete });
        grid.Columns.Add(new DataGridColumn("actions", "Actions") { Width = 120, Renderer = actions, IsEditable = false });
        var window = DataGridBehaviorTests.Show(grid);
        Settle();
        var invoked = new List<DataGridCellInvokedEventArgs>();
        grid.CellInvoked += (_, e) => invoked.Add(e);
        var start = grid.ActiveCell;
        var left = -grid.Layout.Widths[3] / 2 + grid.CellInlinePadding;
        Click(window, CellPoint(grid, window, 1, 3, dx: left + 14));
        open.Executed.ShouldBe([rows[1]]);
        invoked.Single().Part.ShouldBe("open");
        Click(window, CellPoint(grid, window, 1, 3, dx: left + 30 + 14));
        delete.Executed.ShouldBe([rows[1]]);
        invoked.Last().Part.ShouldBe("delete");
        Click(window, CellPoint(grid, window, 2, 3, dx: left + 30 + 14));
        delete.Executed.Count.ShouldBe(1);
        grid.ActiveCell.ShouldBe(start);
        Click(window, CellPoint(grid, window, 1, 3, dx: grid.Layout.Widths[3] / 2 - 6));
        invoked.Count.ShouldBe(2);
        grid.ActiveCell.ShouldBe(new DataGridCellAddress(1, 3));
        actions.GetText(null, new DataGridCellFormat(DataGridColumnType.Text), System.Globalization.CultureInfo.InvariantCulture).ShouldBe("open, delete");
    }

    private static IDataTemplate Editor(Action<DataGridEditContext>? onBuild = null) =>
        new FuncDataTemplate<DataGridEditContext>((context, _) =>
        {
            onBuild?.Invoke(context);
            return new TextBox { Text = "x" };
        });

    [AvaloniaFact]
    public void An_Edit_Template_Replaces_The_Text_Box_And_Commits_The_Context_Value()
    {
        var rows = Rows();
        var grid = Grid(rows);
        DataGridEditContext? seen = null;
        grid.Columns[2].EditTemplate = Editor(c => seen = c);
        var window = DataGridBehaviorTests.Show(grid);
        Settle();
        var changes = new List<DataGridCellsChangedEventArgs>();
        grid.CellsChanged += (_, e) => changes.Add(e);
        grid.Select(new DataGridCellAddress(1, 2));
        Key(window, PhysicalKey.Enter);
        Settle();
        grid.IsEditing.ShouldBeTrue();
        seen.ShouldNotBeNull();
        seen!.Value.ShouldBe(20m);
        seen.Row.ShouldBe(rows[1]);
        seen.Column.Key.ShouldBe("score");
        seen.Value = 55;
        Key(window, PhysicalKey.Enter);
        grid.IsEditing.ShouldBeFalse();
        rows[1]["score"].ShouldBe(55m);
        changes.Single().Changes.Single().NewValue.ShouldBe(55m);
        grid.ActiveCell.ShouldBe(new DataGridCellAddress(2, 2));
        grid.Undo();
        rows[1]["score"].ShouldBe(20m);
    }

    [AvaloniaFact]
    public void Escape_Cancels_A_Template_Edit_And_A_Refused_Or_Mistyped_Value_Keeps_It_Open()
    {
        var rows = Rows();
        var grid = Grid(rows);
        DataGridEditContext? seen = null;
        grid.Columns[2].EditTemplate = Editor(c => seen = c);
        var window = DataGridBehaviorTests.Show(grid);
        Settle();
        grid.Select(new DataGridCellAddress(0, 2));
        Key(window, PhysicalKey.F2);
        seen!.Value = 99m;
        Key(window, PhysicalKey.Escape);
        grid.IsEditing.ShouldBeFalse();
        rows[0]["score"].ShouldBe(10m);
        grid.CellEditEnding += (_, e) =>
        {
            if (e.NewValue is decimal d && d > 100)
            {
                e.Cancel = true;
                e.ErrorMessage = "Too big";
            }
        };
        Key(window, PhysicalKey.F2);
        seen!.Value = 500m;
        Key(window, PhysicalKey.Enter);
        grid.IsEditing.ShouldBeTrue();
        grid.StatusBar!.MessageText!.ShouldContain("Too big");
        seen.Value = new DateOnly(2026, 1, 1);
        seen.ErrorMessage = "Numbers only";
        Key(window, PhysicalKey.Enter);
        grid.IsEditing.ShouldBeTrue();
        grid.StatusBar!.MessageText!.ShouldContain("Numbers only");
        seen.Value = 7;
        seen.Commit().ShouldBeTrue();
        rows[0]["score"].ShouldBe(7m);
        grid.IsEditing.ShouldBeFalse();
    }
}
