using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;

namespace TKCode.Arc.Tests.Editors;

/// <summary>Table column widths, cell colors and header rows and columns of the Docs profile: the model, editing, Html and the layout.</summary>
public class RichTextEditorTableStyleTests
{
    private const string Grid3 =
        "<table><tr><th>a</th><th>b</th><th>c</th></tr><tr><td>d</td><td>e</td><td>f</td></tr><tr><td>g</td><td>h</td><td>i</td></tr></table>";

    private static RichTextEditorEngine Engine(string html)
    {
        var engine = new RichTextEditorEngine { Docs = true };
        engine.Load(RichTextEditorHtml.ToDocument(html, docs: true), record: false, caretAtEnd: true);
        return engine;
    }

    private static int Cell(RichTextEditorEngine engine, int row, int col) =>
        engine.Blocks.Select((b, i) => (b, i)).First(x => x.b.IsCell && x.b.Row == row && x.b.Col == col).i;

    private static void Caret(RichTextEditorEngine engine, int row, int col) => engine.SetSelection(RichTextEditorSelection.Caret(Cell(engine, row, col), 0));

    private static int[] Widths(RichTextEditorEngine engine) =>
        engine.Blocks.Where(b => b.IsCell && b.Row == 0).Select(b => b.Width).ToArray();

    [Fact]
    public void Stored_Widths_Fill_The_Table_And_Free_Columns_Share_The_Rest()
    {
        var engine = Engine(Grid3);
        var table = RichTextEditorTables.Extent(engine.Blocks, 0);
        RichTextEditorTables.ColumnWidths(engine.Blocks, table, 300).ShouldBe(new double[] { 100, 100, 100 });
        engine.SetColumnWidths(0, new[] { 60 }).ShouldBeTrue();
        RichTextEditorTables.ColumnWidths(engine.Blocks, table, 300).ShouldBe(new double[] { 60, 120, 120 });
        engine.SetColumnWidths(0, new[] { 100, 100, 200 }).ShouldBeTrue();
        // Stored widths act as proportions when they do not add up to the table's width.
        RichTextEditorTables.ColumnWidths(engine.Blocks, table, 200).ShouldBe(new double[] { 50, 50, 100 });
        // A free column keeps the minimum width even when the stored ones ask for everything.
        engine.SetColumnWidths(0, new[] { 300, 300, 0 }).ShouldBeTrue();
        var widths = RichTextEditorTables.ColumnWidths(engine.Blocks, table, 300);
        widths[2].ShouldBe(RichTextEditorTables.MinColumnWidth);
        widths.Sum().ShouldBe(300, 0.001);
    }

    [Fact]
    public void Widths_Live_On_The_First_Row_And_Follow_Their_Columns()
    {
        var engine = Engine(Grid3);
        engine.SetColumnWidths(Cell(engine, 2, 2), new[] { 50, 150, 100 }).ShouldBeTrue();
        Widths(engine).ShouldBe(new[] { 50, 150, 100 });
        engine.Blocks.Where(b => b.IsCell && b.Row > 0).ShouldAllBe(b => b.Width == 0);
        // Deleting the first row hands its widths to the new first row.
        Caret(engine, 0, 1);
        engine.DeleteTableRow().ShouldBeTrue();
        Widths(engine).ShouldBe(new[] { 50, 150, 100 });
        // A new column shares the free width; deleting a column takes its width with it.
        Caret(engine, 0, 0);
        engine.AddTableColumn().ShouldBeTrue();
        Widths(engine).ShouldBe(new[] { 50, 0, 150, 100 });
        Caret(engine, 1, 2);
        engine.DeleteTableColumn().ShouldBeTrue();
        Widths(engine).ShouldBe(new[] { 50, 0, 100 });
        // Merging across the first row keeps the covered column's width.
        engine.SetSelection(new RichTextEditorSelection(new RichTextEditorPosition(Cell(engine, 0, 0), 0), new RichTextEditorPosition(Cell(engine, 0, 2), 0)));
        engine.MergeCells().ShouldBeTrue();
        Widths(engine).ShouldBe(new[] { 50, 0, 100 });
        engine.Undo();
        engine.Undo();
        engine.Undo();
        Widths(engine).ShouldBe(new[] { 50, 150, 100 });
    }

    [Fact]
    public void Header_Flags_Stay_With_The_Table()
    {
        var engine = Engine(Grid3);
        Caret(engine, 1, 1);
        engine.HasHeaderRow.ShouldBeTrue();
        engine.HasHeaderColumn.ShouldBeFalse();
        engine.SetTableHeader(column: true, on: true).ShouldBeTrue();
        engine.SetTableHeader(column: false, on: false).ShouldBeTrue();
        engine.SetTableHeader(column: false, on: false).ShouldBeFalse();
        engine.Blocks[0].Attrs.NoHeaderRow.ShouldBeTrue();
        engine.Blocks[0].Attrs.HeaderColumn.ShouldBeTrue();
        // Deleting the first row and column moves the flags to the new first cell.
        Caret(engine, 0, 0);
        engine.DeleteTableRow().ShouldBeTrue();
        Caret(engine, 0, 0);
        engine.DeleteTableColumn().ShouldBeTrue();
        engine.Blocks[0].Text.ShouldBe("e");
        engine.HasHeaderRow.ShouldBeFalse();
        engine.HasHeaderColumn.ShouldBeTrue();
        engine.Blocks.Skip(1).Where(b => b.IsCell).ShouldAllBe(b => !b.Attrs.NoHeaderRow && !b.Attrs.HeaderColumn);
        var first = engine.Blocks[0];
        RichTextEditorTables.IsHeader(first, engine.Blocks[Cell(engine, 1, 0)]).ShouldBeTrue();
        RichTextEditorTables.IsHeader(first, engine.Blocks[Cell(engine, 0, 1)]).ShouldBeFalse();
    }

    [Fact]
    public void Cell_Colors_Apply_To_The_Selected_Cells()
    {
        var engine = Engine(Grid3);
        engine.SetSelection(new RichTextEditorSelection(new RichTextEditorPosition(Cell(engine, 1, 0), 0), new RichTextEditorPosition(Cell(engine, 2, 1), 0)));
        engine.CellTone.ShouldBe(RichTextColor.Default);
        engine.SetCellTone(RichTextColor.Blue).ShouldBeTrue();
        engine.CellTone.ShouldBe(RichTextColor.Blue);
        engine.Blocks.Where(b => b.IsCell && b.Tone == RichTextColor.Blue).Select(b => b.Text).ShouldBe(new[] { "d", "e", "g", "h" });
        engine.SetCellTone(RichTextColor.Blue).ShouldBeFalse();
        Caret(engine, 1, 1);
        engine.SetCellTone(RichTextColor.Red).ShouldBeTrue();
        engine.SetSelection(new RichTextEditorSelection(new RichTextEditorPosition(Cell(engine, 1, 0), 0), new RichTextEditorPosition(Cell(engine, 1, 1), 0)));
        engine.CellTone.ShouldBe(RichTextColor.Default);
        engine.Undo();
        engine.Blocks[Cell(engine, 1, 1)].Tone.ShouldBe(RichTextColor.Blue);
        // Other blocks cannot carry a tone through a cell's attributes.
        RichTextEditorBlock.Plain(RichTextBlockType.Paragraph, "x").WithAttrs(new RichTextEditorBlockAttrs(Tone: RichTextColor.Red, Width: 40)).Attrs.ShouldBe(default);
    }

    [Fact]
    public void Html_Round_Trips_Widths_Colors_And_Headers()
    {
        var engine = Engine(Grid3);
        engine.SetColumnWidths(0, new[] { 80, 0, 120 });
        Caret(engine, 1, 1);
        engine.SetCellTone(RichTextColor.Green);
        engine.SetTableHeader(column: true, on: true);
        engine.SetTableHeader(column: false, on: false);
        var html = RichTextEditorHtml.Write(engine.Blocks);
        html.ShouldContain("<colgroup><col width=\"80\"><col><col width=\"120\"></colgroup>");
        html.ShouldContain("<td data-bg=\"green\">e</td>");
        html.ShouldNotContain("<thead>");
        html.ShouldContain("<tr><th>a</th><td>b</td>");
        html.ShouldContain("<tr><th>d</th>");

        var back = Engine(html);
        Widths(back).ShouldBe(new[] { 80, 0, 120 });
        back.Blocks[Cell(back, 1, 1)].Tone.ShouldBe(RichTextColor.Green);
        back.Blocks[0].Attrs.NoHeaderRow.ShouldBeTrue();
        back.Blocks[0].Attrs.HeaderColumn.ShouldBeTrue();
        RichTextEditorHtml.Write(back.Blocks).ShouldBe(html);
    }

    [Fact]
    public void Html_Without_Header_Cells_Has_No_Header_Row_But_A_New_Table_Has_One()
    {
        var engine = Engine("<table><tr><td>a</td><td>b</td></tr><tr><td>c</td><td>d</td></tr></table>");
        engine.Blocks[0].Attrs.NoHeaderRow.ShouldBeTrue();
        Engine(Grid3).Blocks[0].Attrs.NoHeaderRow.ShouldBeFalse();
        var fresh = new RichTextEditorEngine { Docs = true };
        fresh.InsertTable(2, 2).ShouldBeTrue();
        fresh.HasHeaderRow.ShouldBeTrue();
        RichTextEditorHtml.Write(fresh.Blocks).ShouldContain("<thead>");
    }

    [Fact]
    public void Markdown_Writes_A_Header_Row_Whatever_The_Flags()
    {
        var engine = Engine(Grid3);
        Caret(engine, 1, 1);
        engine.SetTableHeader(column: false, on: false);
        var markdown = RichTextEditorMarkdown.Write(engine.Blocks);
        markdown.ShouldContain("| a | b | c |");
        markdown.ShouldContain("| --- | --- | --- |");
    }

    [AvaloniaFact]
    public void Dragging_A_Column_Border_Stores_The_Widths_And_A_Double_Click_Resets_Them()
    {
        var editor = new RichTextEditor { Width = 480, Profile = RichTextEditorProfile.Docs, Html = Grid3 + "<p>after</p>" };
        var window = TestHelpers.Show(new Border { Padding = new Thickness(20, 80, 20, 20), Child = editor });
        window.Width = 540;
        window.Height = 600;
        Dispatcher.UIThread.RunJobs();
        try
        {
            var surface = TestHelpers.Descendant<RichTextEditorSurface>(editor);
            var width = surface.Bounds.Width;
            var a = surface.CaretRect(new RichTextEditorPosition(0, 0));
            var b = surface.CaretRect(new RichTextEditorPosition(1, 0));
            (b.X - a.X).ShouldBe(width / 3, 0.5);

            var border = new Point(width / 3, a.Y + 4);
            surface.ColumnBorderAt(border).ShouldBe((0, 1));
            surface.ColumnBorderAt(new Point(width / 6, a.Y + 4)).ShouldBeNull();
            Point InWindow(Point p) => surface.TranslatePoint(p, window)!.Value;

            window.MouseDown(InWindow(border), MouseButton.Left);
            window.MouseMove(InWindow(border + new Point(40, 0)));
            Dispatcher.UIThread.RunJobs();
            // The preview moves the border before anything is stored.
            (surface.CaretRect(new RichTextEditorPosition(1, 0)).X - a.X).ShouldBe(width / 3 + 40, 0.5);
            editor.Engine.Blocks[0].Width.ShouldBe(0);
            window.MouseUp(InWindow(border + new Point(40, 0)), MouseButton.Left);
            Dispatcher.UIThread.RunJobs();
            var widths = editor.Engine.Blocks.Where(x => x.IsCell && x.Row == 0).Select(x => x.Width).ToArray();
            Math.Abs(widths[0] - (width / 3 + 40)).ShouldBeLessThan(1.5);
            Math.Abs(widths[1] - (width / 3 - 40)).ShouldBeLessThan(1.5);
            editor.Html.ShouldContain("<colgroup>");
            (surface.CaretRect(new RichTextEditorPosition(1, 0)).X - a.X).ShouldBe(width / 3 + 40, 1);

            editor.Engine.SetSelection(RichTextEditorSelection.Caret(4, 0));
            editor.DistributeTableColumns().ShouldBeTrue();
            editor.Engine.Blocks.Where(x => x.IsCell).ShouldAllBe(x => x.Width == 0);
            editor.Undo();
            editor.Engine.Blocks[0].Width.ShouldBeGreaterThan(0);
            var moved = new Point(widths[0] * width / widths.Sum(), a.Y + 4);
            window.MouseDown(InWindow(moved), MouseButton.Left);
            window.MouseUp(InWindow(moved), MouseButton.Left);
            window.MouseDown(InWindow(moved), MouseButton.Left);
            window.MouseUp(InWindow(moved), MouseButton.Left);
            Dispatcher.UIThread.RunJobs();
            editor.Engine.Blocks.Where(x => x.IsCell).ShouldAllBe(x => x.Width == 0);
        }
        finally
        {
            window.Close();
        }
    }
}
