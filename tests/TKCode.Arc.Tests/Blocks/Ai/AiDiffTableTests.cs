using TKCode.Arc.DesignTokens;
using Avalonia;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Threading;

namespace TKCode.Arc.Tests.Blocks.Ai;

/// <summary>
/// The table change of the side panel's change card: which cells of the new table are marked as inserted (new columns,
/// new rows, changed texts), how rows and columns are matched, what is left out (removed rows and columns), the summary,
/// the compact grid's geometry and colors, and what screen readers get. Sample tables are invented.
/// </summary>
public class AiDiffTableTests
{
    private static AiPanelTable Table(string[] headers, params string[][] rows)
    {
        var table = new AiPanelTable { Id = "storage", Label = "Storage table" };
        for (var i = 0; i < headers.Length; i++)
        {
            table.Columns.Add(new AiPanelColumn
            {
                Header = headers[i],
                IsRowHeader = i == 0,
                Alignment = i == headers.Length - 1 ? AiPanelColumnAlignment.Right : AiPanelColumnAlignment.Left
            });
        }
        foreach (var cells in rows)
        {
            var row = new AiPanelRow();
            row.Cells.AddRange(cells);
            table.Rows.Add(row);
        }
        return table;
    }

    private static AiPanelTable Storage() => Table(["Tier", "Devices", "Cache"],
        ["Basic", "1 device", "200 MB"],
        ["Plus", "3 devices", "1 GB"],
        ["Org", "Unlimited", "5 GB"]);

    private static AiPanelTable WithSync() => Table(["Tier", "Devices", "Cache", "Sync"],
        ["Basic", "1 device", "200 MB", "15 min"],
        ["Plus", "3 devices", "1 GB", "5 min"],
        ["Org", "Unlimited", "5 GB", "1 min"]);

    private static AiDiffTable Create(AiPanelTable? before, AiPanelTable? after, double width = 320) => new()
    {
        Before = before,
        After = after,
        Width = width,
        AddsColumnFormat = header => $"New column: {header}",
        ChangesText = "Table edited"
    };

    private static Window Show(Control content)
    {
        var window = TestHelpers.Show(new StackPanel { Margin = new Thickness(20), Children = { content } });
        window.UpdateLayout();
        Dispatcher.UIThread.RunJobs();
        return window;
    }

    /// <summary>The marked cells as "row:column" (row 0 is the first body row).</summary>
    private static string[] Inserted(AiDiffTableModel model)
    {
        var marked = new List<string>();
        for (var r = 0; r < model.Rows.Count; r++)
        {
            for (var c = 0; c < model.Rows[r].Count; c++)
            {
                if (model.Rows[r][c].IsInserted)
                {
                    marked.Add($"{r}:{c}");
                }
            }
        }
        return marked.ToArray();
    }

    #region The comparison

    [AvaloniaFact]
    public void A_New_Column_Marks_Its_Header_And_Cells_And_Is_Summarized_By_Name()
    {
        var model = AiDiffTableModel.Compare(Storage(), WithSync());
        model.Columns.Select(c => (c.Header, c.IsInserted)).ShouldBe([("Tier", false), ("Devices", false), ("Cache", false), ("Sync", true)]);
        Inserted(model).ShouldBe(["0:3", "1:3", "2:3"]);
        model.AddedColumns.ShouldBe(["Sync"]);
        model.InsertedCells.ShouldBe(3);
        model.IsSingleAddedColumn.ShouldBeTrue();
        model.HasChanges.ShouldBeTrue();

        var table = Create(Storage(), WithSync());
        table.Summary.ShouldBe("New column: Sync");
        table.InsertedCells.ShouldBe(3);
        table.HasChanges.ShouldBeTrue();
    }

    [AvaloniaFact]
    public void Without_A_Column_Format_The_Summary_Is_The_Changes_Text()
    {
        var table = Create(Storage(), WithSync());
        table.AddsColumnFormat = null;
        table.Summary.ShouldBe("Table edited");
        table.ChangesText = null;
        table.Summary.ShouldBe(string.Empty);
    }

    [AvaloniaFact]
    public void A_Changed_Cell_Is_Marked_And_The_Summary_Is_The_Changes_Text()
    {
        var after = Storage();
        after.Rows[0].Cells[2] = "250 MB";
        var model = AiDiffTableModel.Compare(Storage(), after);
        Inserted(model).ShouldBe(["0:2"]);
        model.ChangedCells.ShouldBe(1);
        model.IsSingleAddedColumn.ShouldBeFalse();
        Create(Storage(), after).Summary.ShouldBe("Table edited");
    }

    [AvaloniaFact]
    public void A_New_Row_Is_Marked_Whole()
    {
        var after = Table(["Tier", "Devices", "Cache"],
            ["Basic", "1 device", "200 MB"],
            ["Plus", "3 devices", "1 GB"],
            ["Team", "10 devices", "2 GB"],
            ["Org", "Unlimited", "5 GB"]);
        var model = AiDiffTableModel.Compare(Storage(), after);
        Inserted(model).ShouldBe(["2:0", "2:1", "2:2"]);
        model.AddedRows.ShouldBe(1);
        model.InsertedCells.ShouldBe(3);
        model.Columns.ShouldAllBe(c => !c.IsInserted);
    }

    [AvaloniaFact]
    public void Removed_Rows_And_Columns_Are_Not_Shown_But_Count_As_Changes()
    {
        var after = Table(["Tier", "Cache"], ["Basic", "200 MB"], ["Org", "5 GB"]);
        var model = AiDiffTableModel.Compare(Storage(), after);
        model.Columns.Select(c => c.Header).ShouldBe(["Tier", "Cache"]);
        model.Rows.Count.ShouldBe(2);
        Inserted(model).ShouldBeEmpty();
        model.RemovedColumns.ShouldBe(1);
        model.RemovedRows.ShouldBe(1);
        model.HasChanges.ShouldBeTrue();
        var table = Create(Storage(), after);
        table.InsertedCells.ShouldBe(0);
        table.Summary.ShouldBe("Table edited");
    }

    [AvaloniaFact]
    public void Rows_Are_Matched_By_Their_Row_Header_Not_Their_Position()
    {
        var after = Table(["Tier", "Devices", "Cache"],
            ["Org", "Unlimited", "5 GB"],
            ["Basic", "1 device", "200 MB"],
            ["Plus", "3 devices", "2 GB"]);
        var model = AiDiffTableModel.Compare(Storage(), after);
        Inserted(model).ShouldBe(["2:2"]);
        model.AddedRows.ShouldBe(0);
        model.RemovedRows.ShouldBe(0);
    }

    [AvaloniaFact]
    public void Columns_Are_Matched_By_Header_Wherever_They_Are()
    {
        var after = Table(["Tier", "Cache", "Devices"],
            ["Basic", "200 MB", "1 device"],
            ["Plus", "1 GB", "3 devices"],
            ["Org", "5 GB", "Unlimited"]);
        var model = AiDiffTableModel.Compare(Storage(), after);
        model.HasChanges.ShouldBeFalse();
        Inserted(model).ShouldBeEmpty();
        Create(Storage(), after).Summary.ShouldBe(string.Empty);
    }

    [AvaloniaFact]
    public void A_Renamed_Header_Is_A_New_Column_And_A_Removed_One()
    {
        var after = Table(["Tier", "Devices", "Storage"],
            ["Basic", "1 device", "200 MB"],
            ["Plus", "3 devices", "1 GB"],
            ["Org", "Unlimited", "5 GB"]);
        var model = AiDiffTableModel.Compare(Storage(), after);
        model.AddedColumns.ShouldBe(["Storage"]);
        model.RemovedColumns.ShouldBe(1);
        model.IsSingleAddedColumn.ShouldBeFalse();
        Inserted(model).ShouldBe(["0:2", "1:2", "2:2"]);
    }

    [AvaloniaFact]
    public void Missing_Cells_Read_Empty_And_Duplicate_Keys_Match_In_Order()
    {
        var before = Table(["Tier", "Note"], ["Plus", "a"], ["Plus", "b"]);
        var after = Table(["Tier", "Note"], ["Plus", "a"], ["Plus"]);
        var model = AiDiffTableModel.Compare(before, after);
        model.Rows[1][1].Text.ShouldBe(string.Empty);
        Inserted(model).ShouldBe(["1:1"]);
    }

    [AvaloniaFact]
    public void Without_An_Old_Table_Everything_Is_New_And_Without_A_New_One_Nothing_Shows()
    {
        var model = AiDiffTableModel.Compare(null, Storage());
        model.Columns.ShouldAllBe(c => c.IsInserted);
        model.InsertedCells.ShouldBe(9);
        AiDiffTableModel.Compare(Storage(), null).ShouldBeSameAs(AiDiffTableModel.Empty);
        var table = Create(Storage(), null);
        table.HasChanges.ShouldBeFalse();
        table.Summary.ShouldBe(string.Empty);
    }

    #endregion

    #region The grid

    [AvaloniaFact]
    public void The_Grid_Holds_The_Header_Row_Then_The_Rows_With_Inserted_Cells_Tinted()
    {
        var table = Create(Storage(), WithSync());
        var window = Show(table);
        try
        {
            var grid = table.Grid!;
            grid.ColumnCount.ShouldBe(4);
            var cells = grid.Children.Cast<AiDiffTableCell>().ToArray();
            cells.Length.ShouldBe(16);
            cells.Take(4).ShouldAllBe(c => c.IsHeader);
            cells.Select(c => c.Text).Take(5).ShouldBe(["Tier", "Devices", "Cache", "Sync", "Basic"]);
            cells.Where(c => c.Tint is not null).Select(c => c.Text).ShouldBe(["Sync", "15 min", "5 min", "1 min"]);
            var tinted = cells.First(c => c.Tint is not null);
            tinted.Padding.ShouldBe(new Thickness(2, 0, 2, 0));
            cells[0].Padding.ShouldBe(default);

            Color Token(ArcDesignTokenKind kind) => ArcTokens.GetColor(table, kind);
            TestHelpers.ColorOf(cells[0].Foreground).ShouldBe(Token(ArcDesignTokenKind.TextMuted));
            TestHelpers.ColorOf(cells[4].Foreground).ShouldBe(Token(ArcDesignTokenKind.TextSecondary));
            TestHelpers.ColorOf(tinted.Foreground).ShouldBe(Token(ArcDesignTokenKind.Foreground));
            TestHelpers.ColorOf(tinted.Tint).ShouldBe(PromptTone.WithOpacity(Token(ArcDesignTokenKind.Accent), 0.13));
            cells.ShouldAllBe(c => c.FontSize == 12 && c.LineHeight == 16.8);
            cells[0].FontFeatures!.ShouldContain(f => f.Tag == "tnum");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Columns_Fill_The_Width_In_Proportion_With_Cell_Padding_And_No_Row_Lines()
    {
        var table = Create(Storage(), WithSync(), 400);
        var window = Show(table);
        try
        {
            var grid = table.Grid!;
            grid.Bounds.Width.ShouldBe(400, 0.01);
            var widths = grid.ColumnWidths;
            (widths.Sum() + 3 * AiDiffTableGrid.CellPadRight).ShouldBe(400, 0.01);
            // Headless text advances 1em a character: "Unlimited" and "3 devices" make Devices the widest column.
            widths[1].ShouldBeGreaterThan(widths[0]);
            var cells = grid.Children.Cast<AiDiffTableCell>().ToArray();
            // Bounds are rounded to whole pixels by layout rounding.
            cells[1].Bounds.Left.ShouldBe(widths[0] + 8, 1);
            cells[0].Bounds.Top.ShouldBe(3, 1);
            // One line a row: 16.8 + 3 + 3.
            cells[4].Bounds.Top.ShouldBe(22.8 + 3, 1);
            grid.Bounds.Height.ShouldBe(4 * 22.8, 1);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Narrow_Columns_Shrink_And_Wrap()
    {
        var table = Create(Storage(), WithSync(), 160);
        var window = Show(table);
        try
        {
            var grid = table.Grid!;
            (grid.ColumnWidths.Sum() + 24).ShouldBe(160, 0.01);
            grid.Bounds.Height.ShouldBeGreaterThan(4 * 22.8);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Refresh_Reads_A_Table_Changed_In_Place()
    {
        var after = Storage();
        var table = Create(Storage(), after);
        var window = Show(table);
        try
        {
            table.HasChanges.ShouldBeFalse();
            after.Rows[1].Cells[1] = "5 devices";
            table.Refresh();
            table.InsertedCells.ShouldBe(1);
            table.Grid!.Children.Cast<AiDiffTableCell>().Single(c => c.Tint is not null).Text.ShouldBe("5 devices");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Screen_Readers_Get_A_Table_Named_By_The_Summary()
    {
        var table = Create(Storage(), WithSync());
        var window = Show(table);
        try
        {
            var peer = ControlAutomationPeer.CreatePeerForElement(table);
            peer.GetAutomationControlType().ShouldBe(AutomationControlType.Table);
            peer.GetName().ShouldBe("New column: Sync");
            Avalonia.Automation.AutomationProperties.SetName(table, "Storage table change");
            peer.GetName().ShouldBe("Storage table change");
        }
        finally
        {
            window.Close();
        }
    }

    #endregion
}
