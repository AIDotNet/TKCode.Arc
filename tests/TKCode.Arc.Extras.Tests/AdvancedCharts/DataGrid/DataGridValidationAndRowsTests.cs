using System.Collections.ObjectModel;
using System.Globalization;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using Shouldly;

namespace TKCode.Arc.Tests.AdvancedCharts;

/// <summary>Validation rules, the cell editability predicate, conditional styles and adding rows.</summary>
public class DataGridValidationAndRowsTests
{
    private static readonly CultureInfo En = CultureInfo.GetCultureInfo("en-US");

    private static void Key(Avalonia.Controls.Window window, PhysicalKey key, RawInputModifiers modifiers = RawInputModifiers.None)
    {
        window.KeyPressQwerty(key, modifiers);
        window.KeyReleaseQwerty(key, modifiers);
        Dispatcher.UIThread.RunJobs();
    }

    private static void Type(Avalonia.Controls.Window window, string text)
    {
        window.KeyTextInput(text);
        Dispatcher.UIThread.RunJobs();
    }

    private static DataGridCellContext Context(string header, object? value, DataGridColumnType type = DataGridColumnType.Text) =>
        new(new DataGridRow("r"), "r", new DataGridColumn("k", header, type), value);

    [AvaloniaFact]
    public void Built_In_Rules_Pass_And_Fail_With_Localized_Or_Custom_Messages()
    {
        new RequiredRule().Validate(Context("Account", "Lumen")).ShouldBeNull();
        new RequiredRule().Validate(Context("Account", "")).ShouldBe("Account is required");
        new RequiredRule().Validate(Context("Account", null)).ShouldBe("Account is required");
        new RequiredRule { Message = "Name this account" }.Validate(Context("Account", null)).ShouldBe("Name this account");

        var between = new RangeRule { Minimum = 0, Maximum = 100 };
        between.Validate(Context("Health", 50m)).ShouldBeNull();
        between.Validate(Context("Health", 101m)).ShouldBe("Health must be between 0 and 100");
        between.Validate(Context("Health", null)).ShouldBeNull();
        between.Validate(Context("Health", "text")).ShouldBeNull();
        new RangeRule { Minimum = 1 }.Validate(Context("Seats", 0m)).ShouldBe("Seats must be at least 1");
        new RangeRule { Maximum = 9 }.Validate(Context("Seats", 10m)).ShouldBe("Seats must be at most 9");

        new MaxLengthRule { Length = 3 }.Validate(Context("Code", "abc")).ShouldBeNull();
        new MaxLengthRule { Length = 3 }.Validate(Context("Code", "abcd")).ShouldBe("Code can hold at most 3 characters");

        var pattern = new PatternRule { Pattern = "^[A-Z]{2}-\\d+$" };
        pattern.Validate(Context("Sku", "AB-12")).ShouldBeNull();
        pattern.Validate(Context("Sku", "ab-12")).ShouldBe("Sku is not in the expected format");
        pattern.Validate(Context("Sku", "")).ShouldBeNull();
        pattern.Pattern = "^x$";
        pattern.Validate(Context("Sku", "AB-12")).ShouldNotBeNull();

        var custom = DataGridRule.Custom(c => c.Value is "admin" ? "Reserved" : null);
        custom.Validate(Context("User", "admin")).ShouldBe("Reserved");
        custom.Validate(Context("User", "ada")).ShouldBeNull();
    }

    [Fact]
    public void Format_Rules_Compare_Values_The_Way_The_Column_Reads_Typed_Text()
    {
        var money = new DataGridColumn("arr", "ARR", DataGridColumnType.Currency);
        bool Matches(DataGridFormatCondition condition, object? value, string? a, string? b = null, DataGridColumn? column = null) =>
            new DataGridFormatRule { Condition = condition, Value = a, Value2 = b }.Matches(value, column ?? money, En);
        Matches(DataGridFormatCondition.GreaterThan, 1500m, "$1,000").ShouldBeTrue();
        Matches(DataGridFormatCondition.GreaterThan, 1000m, "1000").ShouldBeFalse();
        Matches(DataGridFormatCondition.GreaterThanOrEqual, 1000m, "1000").ShouldBeTrue();
        Matches(DataGridFormatCondition.LessThan, 5m, "10").ShouldBeTrue();
        Matches(DataGridFormatCondition.LessThanOrEqual, 10m, "10").ShouldBeTrue();
        Matches(DataGridFormatCondition.Equal, 10m, "10").ShouldBeTrue();
        Matches(DataGridFormatCondition.NotEqual, 10m, "11").ShouldBeTrue();
        Matches(DataGridFormatCondition.NotEqual, null, "11").ShouldBeFalse();
        Matches(DataGridFormatCondition.Between, 50m, "10", "100").ShouldBeTrue();
        Matches(DataGridFormatCondition.Between, 50m, "100", "10").ShouldBeTrue();
        Matches(DataGridFormatCondition.Between, 500m, "10", "100").ShouldBeFalse();
        Matches(DataGridFormatCondition.Negative, -1m, null).ShouldBeTrue();
        Matches(DataGridFormatCondition.Negative, 1m, null).ShouldBeFalse();
        Matches(DataGridFormatCondition.IsEmpty, null, null).ShouldBeTrue();
        Matches(DataGridFormatCondition.IsEmpty, "", null).ShouldBeTrue();
        Matches(DataGridFormatCondition.IsNotEmpty, "x", null).ShouldBeTrue();
        Matches(DataGridFormatCondition.GreaterThan, 5m, "banana").ShouldBeFalse();

        var stage = new DataGridColumn("stage", "Stage");
        Matches(DataGridFormatCondition.Contains, "Closed lost", "LOST", column: stage).ShouldBeTrue();
        Matches(DataGridFormatCondition.Contains, "Closed won", "lost", column: stage).ShouldBeFalse();
        Matches(DataGridFormatCondition.Equal, "Lost", "lost", column: stage).ShouldBeTrue();

        var date = new DataGridColumn("renewal", "Renews", DataGridColumnType.Date);
        Matches(DataGridFormatCondition.LessThan, new DateOnly(2026, 1, 31), "2026-03-01", column: date).ShouldBeTrue();
        Matches(DataGridFormatCondition.GreaterThan, new DateOnly(2026, 1, 31), "2026-03-01", column: date).ShouldBeFalse();
        new DataGridFormatRule { Tone = DataGridTone.Warning, Bold = true }.Style.ShouldBe(new DataGridCellStyle(DataGridTone.Warning, true));
    }

    [AvaloniaFact]
    public void Block_Mode_Refuses_An_Invalid_Edit_Keeps_The_Editor_And_Skips_Invalid_Pastes_And_Fills()
    {
        var rows = DataGridBehaviorTests.Rows();
        var grid = DataGridBehaviorTests.Create(rows);
        grid.Columns[2].Rules.Add(new RangeRule { Minimum = 0 });
        var window = DataGridBehaviorTests.Show(grid);
        grid.Select(new DataGridCellAddress(0, 2));
        Type(window, "-5");
        Key(window, PhysicalKey.Enter);
        grid.IsEditing.ShouldBeTrue();
        grid.StatusBar!.MessageText!.ShouldContain("ARR must be at least 0");
        rows[0]["arr"].ShouldBe(1200m);
        grid.CancelEdit();
        grid.Viewport.FocusGrid();
        Dispatcher.UIThread.RunJobs();
        grid.Select(new DataGridCellAddress(0, 2));
        Type(window, "7");
        Key(window, PhysicalKey.Enter);
        grid.IsEditing.ShouldBeFalse();
        rows[0]["arr"].ShouldBe(7m);
        grid.Select(new DataGridCellAddress(1, 2), new DataGridCellAddress(3, 2));
        grid.Paste([["-1"], ["20"], ["-3"]]);
        rows[1]["arr"].ShouldBe(800m);
        rows[2]["arr"].ShouldBe(20m);
        rows[3]["arr"].ShouldBeNull();
        grid.StatusBar!.MessageText!.ShouldContain("skipped 2");
        grid.ErrorCount.ShouldBe(0);
    }

    [AvaloniaFact]
    public void Clearing_Undo_And_Redo_Are_Not_Blocked_By_Rules()
    {
        var rows = DataGridBehaviorTests.Rows();
        var grid = DataGridBehaviorTests.Create(rows);
        grid.Columns[0].Rules.Add(new RequiredRule());
        var window = DataGridBehaviorTests.Show(grid);
        grid.Select(new DataGridCellAddress(0, 0));
        Key(window, PhysicalKey.Delete);
        rows[0]["account"].ShouldBe(string.Empty);
        grid.Undo();
        rows[0]["account"].ShouldBe("Lumen Labs");
        grid.Redo();
        rows[0]["account"].ShouldBe(string.Empty);
        grid.ErrorCount.ShouldBe(1);
        grid.Undo();
        grid.ErrorCount.ShouldBe(0);
    }

    [AvaloniaFact]
    public void Flag_Mode_Accepts_The_Value_Flags_The_Cell_And_Tracks_Fixes_Undo_And_ValidateAll()
    {
        var rows = DataGridBehaviorTests.Rows();
        var grid = DataGridBehaviorTests.Create(rows);
        grid.ValidationMode = DataGridValidationMode.Flag;
        grid.Columns[2].Rules.Add(new RangeRule { Minimum = 0, Message = "No negatives" });
        var window = DataGridBehaviorTests.Show(grid);
        grid.Select(new DataGridCellAddress(1, 2), new DataGridCellAddress(2, 2));
        grid.Paste([["-4"], ["50"]]);
        rows[1]["arr"].ShouldBe(-4m);
        grid.ErrorCount.ShouldBe(1);
        grid.HasErrors.ShouldBeTrue();
        var error = grid.GetErrors().Single();
        error.RowKey.ShouldBe("r2");
        error.Column.Key.ShouldBe("arr");
        error.Message.ShouldBe("No negatives");
        grid.ErrorOf("r2", grid.Layout.Accessors[2]).ShouldBe("No negatives");
        grid.Select(new DataGridCellAddress(1, 2));
        grid.Paste([["9"]]);
        grid.ErrorCount.ShouldBe(0);
        grid.Undo();
        grid.ErrorCount.ShouldBe(1);
        grid.Redo();
        grid.ErrorCount.ShouldBe(0);

        rows[3]["arr"] = -10m;
        rows[0]["arr"] = -20m;
        grid.ValidateAll().ShouldBe(2);
        grid.GetErrors().Select(e => e.RowKey).ShouldBe(["r1", "r4"]);
        grid.Select(new DataGridCellAddress(0, 0));
        grid.NextError().ShouldBeTrue();
        grid.ActiveCell.ShouldBe(new DataGridCellAddress(0, 2));
        grid.StatusBar!.MessageText!.ShouldContain("No negatives");
        Key(window, PhysicalKey.F8);
        grid.ActiveCell.ShouldBe(new DataGridCellAddress(3, 2));
        Key(window, PhysicalKey.F8);
        grid.ActiveCell.ShouldBe(new DataGridCellAddress(0, 2));
        rows[0]["arr"] = 1m;
        rows[3]["arr"] = 1m;
        grid.ValidateAll().ShouldBe(0);
        grid.NextError().ShouldBeFalse();
    }

    [AvaloniaFact]
    public void Errors_Of_Deleted_Rows_Are_Forgotten_And_Flagged_Cells_Draw_Without_Throwing()
    {
        var rows = DataGridBehaviorTests.Rows();
        var grid = DataGridBehaviorTests.Create(rows);
        grid.ValidationMode = DataGridValidationMode.Flag;
        grid.Columns[2].Rules.Add(new RangeRule { Minimum = 5000 });
        DataGridBehaviorTests.Show(grid);
        grid.ValidateAll().ShouldBe(3);
        Should.NotThrow(() =>
        {
            AvaloniaHeadlessPlatformTick();
        });
        rows.RemoveAt(0);
        grid.ErrorCount.ShouldBe(2);
        grid.Columns[2].Rules.Clear();
        grid.ValidateAll().ShouldBe(0);
    }

    private static void AvaloniaHeadlessPlatformTick()
    {
        Avalonia.Headless.AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        Dispatcher.UIThread.RunJobs();
    }

    [AvaloniaFact]
    public void CanEditCell_Locks_Cells_For_Edits_Pastes_Clears_And_Flips()
    {
        var rows = DataGridBehaviorTests.Rows();
        var grid = DataGridBehaviorTests.Create(rows);
        grid.CanEditCell = context => context.Row != rows[1];
        var window = DataGridBehaviorTests.Show(grid);
        grid.IsCellEditable(1, grid.Layout.Accessors[2]).ShouldBeFalse();
        grid.IsCellEditable(0, grid.Layout.Accessors[2]).ShouldBeTrue();
        grid.Select(new DataGridCellAddress(1, 2));
        Key(window, PhysicalKey.F2);
        grid.IsEditing.ShouldBeFalse();
        grid.StatusBar!.MessageText!.ShouldContain("read only");
        grid.Select(new DataGridCellAddress(0, 2), new DataGridCellAddress(2, 2));
        grid.Paste([["1"], ["2"], ["3"]]);
        rows.Select(r => r["arr"]).ShouldBe([1m, 800m, 3m, null]);
        Key(window, PhysicalKey.Delete);
        rows.Select(r => r["arr"]).ShouldBe([null, 800m, null, null]);
        grid.SetCellValue(1, grid.Layout.Accessors[2], 5m).ShouldBeFalse();
        grid.SetCellValue(0, grid.Layout.Accessors[2], 5m).ShouldBeTrue();
        grid.CanEditCell = null;
        grid.SetCellValue(1, grid.Layout.Accessors[2], 5m).ShouldBeTrue();
    }

    [AvaloniaFact]
    public void Style_Selectors_And_Format_Rules_Pick_Tones_With_The_Selector_First()
    {
        var rows = DataGridBehaviorTests.Rows();
        var grid = DataGridBehaviorTests.Create(rows);
        grid.HasStyling.ShouldBeFalse();
        grid.Columns[2].FormatRules.Add(new DataGridFormatRule { Condition = DataGridFormatCondition.GreaterThan, Value = "1000", Tone = DataGridTone.Success });
        grid.Columns[2].FormatRules.Add(new DataGridFormatRule { Condition = DataGridFormatCondition.IsEmpty, Tone = DataGridTone.Warning, Bold = true });
        grid.HasStyling.ShouldBeTrue();
        var window = DataGridBehaviorTests.Show(grid);
        var accessor = grid.Layout.Accessors[2];
        grid.StyleOf(rows[0], "r1", accessor, 1200m).ShouldBe(new DataGridCellStyle(DataGridTone.Success));
        grid.StyleOf(rows[1], "r2", accessor, 800m).ShouldBeNull();
        grid.StyleOf(rows[3], "r4", accessor, null).ShouldBe(new DataGridCellStyle(DataGridTone.Warning, true));
        grid.CellStyleSelector = context => context.Column.Key == "arr" && context.Value is 800m ? new DataGridCellStyle(DataGridTone.Danger) : null;
        grid.StyleOf(rows[1], "r2", accessor, 800m).ShouldBe(new DataGridCellStyle(DataGridTone.Danger));
        grid.StyleOf(rows[0], "r1", accessor, 1200m).ShouldBe(new DataGridCellStyle(DataGridTone.Success));
        grid.RowStyleSelector = row => ReferenceEquals(row, rows[2]) ? DataGridTone.Accent : null;
        grid.Columns[0].Renderer = new BadgeRenderer();
        Should.NotThrow(() =>
        {
            AvaloniaHeadlessPlatformTick();
            AvaloniaHeadlessPlatformTick();
        });
        window.ShouldNotBeNull();
    }

    [AvaloniaFact]
    public void AddRow_Appends_Selects_The_New_Row_Raises_Events_And_Undoes()
    {
        var rows = DataGridBehaviorTests.Rows();
        var grid = DataGridBehaviorTests.Create(rows);
        grid.AddRow().ShouldBeNull();
        grid.CanInsertRows = true;
        DataGridBehaviorTests.Show(grid);
        var inserted = new List<DataGridRowsEventArgs>();
        var deleted = new List<DataGridRowsEventArgs>();
        grid.RowsInserted += (_, e) => inserted.Add(e);
        grid.RowsDeleted += (_, e) => deleted.Add(e);
        var added = (DataGridRow)grid.AddRow()!;
        rows.Count.ShouldBe(5);
        rows[4].ShouldBe(added);
        added.Key.ShouldNotBeNullOrEmpty();
        inserted.Single().Rows.ShouldBe([added]);
        inserted.Single().Indices.ShouldBe([4]);
        grid.ActiveCell.ShouldBe(new DataGridCellAddress(4, 0));
        grid.VisibleRowCount.ShouldBe(5);
        grid.CanUndo.ShouldBeTrue();
        grid.Undo();
        rows.Count.ShouldBe(4);
        deleted.Single().Rows.ShouldBe([added]);
        grid.Redo();
        rows.Count.ShouldBe(5);
        rows[4].ShouldBe(added);
        inserted.Count.ShouldBe(2);
    }

    [AvaloniaFact]
    public void InsertRow_Goes_Above_Or_Below_The_Active_Row_And_Ctrl_Shift_Plus_Inserts_Above()
    {
        var rows = DataGridBehaviorTests.Rows();
        var grid = DataGridBehaviorTests.Create(rows);
        grid.CanInsertRows = true;
        var window = DataGridBehaviorTests.Show(grid);
        grid.Select(new DataGridCellAddress(1, 0));
        var below = grid.InsertRow(below: true);
        rows.IndexOf((DataGridRow)below!).ShouldBe(2);
        grid.Select(new DataGridCellAddress(0, 0));
        var above = grid.InsertRow(below: false);
        rows.IndexOf((DataGridRow)above!).ShouldBe(0);
        grid.Select(new DataGridCellAddress(0, 1));
        Key(window, PhysicalKey.Equal, RawInputModifiers.Control | RawInputModifiers.Shift);
        rows.Count.ShouldBe(7);
        rows[0]["account"].ShouldBeNull();
        grid.Undo();
        grid.Undo();
        grid.Undo();
        rows.Count.ShouldBe(4);
        rows.Select(r => r.Key).ShouldBe(["r1", "r2", "r3", "r4"]);
    }

    private sealed class Account(string name)
    {
        public string Name { get; set; } = name;

        public decimal Arr { get; set; }
    }

    [AvaloniaFact]
    public void Typed_Rows_Need_A_Factory_And_Dynamic_Rows_Do_Not()
    {
        var items = new ObservableCollection<Account> { new("a") { Arr = 1 } };
        var grid = new TKCode.Arc.DataGrid { ItemsSource = items, RowKeySelector = o => ((Account)o).Name, CanInsertRows = true, Width = 600 };
        grid.Columns.Add(DataGridColumn.Create<Account>("name", "Name", a => a.Name, (a, v) => a.Name = v as string ?? string.Empty));
        grid.Columns.Add(DataGridColumn.Create<Account>("arr", "ARR", a => a.Arr, (a, v) => a.Arr = v as decimal? ?? 0, DataGridColumnType.Currency));
        DataGridBehaviorTests.Show(grid);
        grid.CanCreateRows.ShouldBeFalse();
        grid.AddRow().ShouldBeNull();
        var counter = 0;
        grid.RowFactory = () => new Account("new-" + ++counter);
        grid.CanCreateRows.ShouldBeTrue();
        grid.AddRow().ShouldBeOfType<Account>().Name.ShouldBe("new-1");
        items.Count.ShouldBe(2);
        grid.Undo();
        items.Count.ShouldBe(1);

        var readOnly = new TKCode.Arc.DataGrid { ItemsSource = new[] { new DataGridRow("x") }, CanInsertRows = true, Width = 400 };
        readOnly.Columns.Add(new DataGridColumn("name", "Name"));
        DataGridBehaviorTests.Show(readOnly);
        readOnly.CanCreateRows.ShouldBeFalse();
    }

    [AvaloniaFact]
    public void PasteOverflow_Appends_Rows_When_Asked_And_Truncates_By_Default()
    {
        var rows = DataGridBehaviorTests.Rows();
        var grid = DataGridBehaviorTests.Create(rows);
        grid.CanInsertRows = true;
        DataGridBehaviorTests.Show(grid);
        grid.Select(new DataGridCellAddress(3, 0));
        grid.Paste([["Delta2"], ["Echo"], ["Foxtrot"]]);
        rows.Count.ShouldBe(4);
        grid.StatusBar!.MessageText!.ShouldContain("outside the grid");
        grid.PasteOverflow = DataGridPasteOverflow.AppendRows;
        grid.Select(new DataGridCellAddress(3, 0));
        grid.Paste([["Delta3"], ["Echo"], ["Foxtrot"]]);
        rows.Count.ShouldBe(6);
        rows.Select(r => r["account"]).ShouldBe(["Lumen Labs", "Basalt Freight", "Cobalt Clinic", "Delta3", "Echo", "Foxtrot"]);
        grid.Undo();
        rows.Select(r => r["account"]).ShouldBe(["Lumen Labs", "Basalt Freight", "Cobalt Clinic", "Delta2", "", ""]);
        grid.Undo();
        rows.Count.ShouldBe(4);
    }
}
