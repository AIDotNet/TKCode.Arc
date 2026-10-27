using System.Collections.Immutable;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;

namespace TKCode.Arc.Tests.Editors;

/// <summary>Merged table cells of the Docs profile: the model, editing, navigation, Html and the layout.</summary>
public class RichTextEditorMergedCellTests
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

    private static void Select(RichTextEditorEngine engine, (int Row, int Col) from, (int Row, int Col) to) =>
        engine.SetSelection(new RichTextEditorSelection(new RichTextEditorPosition(Cell(engine, from.Row, from.Col), 0), new RichTextEditorPosition(Cell(engine, to.Row, to.Col), 0)));

    [Fact]
    public void Merging_Joins_The_Texts_And_Covers_The_Other_Cells()
    {
        var engine = Engine(Grid3);
        Select(engine, (0, 0), (1, 1));
        engine.CanMergeCells.ShouldBeTrue();
        engine.MergeCells().ShouldBeTrue();
        var head = engine.Blocks[Cell(engine, 0, 0)];
        (head.RowSpan, head.ColSpan).ShouldBe((2, 2));
        head.Text.ShouldBe("a\nb\nd\ne");
        new[] { (0, 1), (1, 0), (1, 1) }.ShouldAllBe(p => engine.Blocks[Cell(engine, p.Item1, p.Item2)].IsCovered && engine.Blocks[Cell(engine, p.Item1, p.Item2)].Length == 0);
        engine.Blocks.Count(b => b.IsCell).ShouldBe(9);
        engine.Selection.Focus.Block.ShouldBe(Cell(engine, 0, 0));
    }

    [Fact]
    public void A_Single_Cell_Or_An_Already_Merged_Cell_Cannot_Merge()
    {
        var engine = Engine(Grid3);
        engine.SetSelection(RichTextEditorSelection.Caret(Cell(engine, 1, 1), 0));
        engine.CanMergeCells.ShouldBeFalse();
        engine.MergeCells().ShouldBeFalse();
        Select(engine, (0, 0), (0, 1));
        engine.MergeCells().ShouldBeTrue();
        engine.SetSelection(RichTextEditorSelection.Caret(Cell(engine, 0, 0), 0));
        engine.CanMergeCells.ShouldBeFalse();
        engine.CanSplitCell.ShouldBeTrue();
    }

    [Fact]
    public void The_Merge_Rectangle_Grows_To_Whole_Merged_Cells()
    {
        var engine = Engine(Grid3);
        Select(engine, (1, 1), (2, 2));
        engine.MergeCells().ShouldBeTrue();
        // Reaching only a corner of the merged cell still merges all of it with the new neighbours.
        Select(engine, (0, 0), (1, 1));
        engine.MergeCells().ShouldBeTrue();
        var head = engine.Blocks[Cell(engine, 0, 0)];
        (head.RowSpan, head.ColSpan).ShouldBe((3, 3));
        engine.Blocks.Count(b => b.IsCovered).ShouldBe(8);
    }

    [Fact]
    public void Splitting_Restores_Empty_Cells_And_Keeps_The_Text_In_The_First()
    {
        var engine = Engine(Grid3);
        Select(engine, (0, 0), (0, 2));
        engine.MergeCells();
        engine.SplitCell().ShouldBeTrue();
        engine.Blocks.Any(b => b.IsCovered || b.IsMerged).ShouldBeFalse();
        engine.Blocks[Cell(engine, 0, 0)].Text.ShouldBe("a\nb\nc");
        engine.Blocks[Cell(engine, 0, 1)].Length.ShouldBe(0);
        engine.SplitCell().ShouldBeFalse();
    }

    [Fact]
    public void Merging_And_Splitting_Are_One_Undo_Step_Each()
    {
        var engine = Engine(Grid3);
        var before = engine.Blocks;
        Select(engine, (0, 0), (1, 0));
        engine.MergeCells();
        engine.Undo();
        RichTextEditorHistory.SameContent(engine.Blocks, before).ShouldBeTrue();
    }

    [Fact]
    public void Tab_Skips_Covered_Cells_And_Enter_Leaves_The_Merged_Cell()
    {
        var engine = Engine(Grid3);
        Select(engine, (0, 0), (1, 0));
        engine.MergeCells();
        // Right of the merged cell: (0,1), (0,2), then the row below skips the covered (1,0).
        engine.SetSelection(RichTextEditorSelection.Caret(Cell(engine, 0, 2), 0));
        engine.TabInTable(backward: false);
        engine.Selection.Focus.Block.ShouldBe(Cell(engine, 1, 1));
        engine.TabInTable(backward: true);
        engine.Selection.Focus.Block.ShouldBe(Cell(engine, 0, 2));
        // Enter in the merged cell goes below all of its rows.
        engine.SetSelection(RichTextEditorSelection.Caret(Cell(engine, 0, 0), 0));
        engine.InsertParagraph();
        engine.Selection.Focus.Block.ShouldBe(Cell(engine, 2, 0));
    }

    [Fact]
    public void Tab_Past_The_Last_Cell_Still_Adds_A_Row()
    {
        var engine = Engine(Grid3);
        Select(engine, (1, 2), (2, 2));
        engine.MergeCells();
        // The last cell that is shown is (2, 1): the covered (2, 2) is skipped and the table grows.
        engine.SetSelection(RichTextEditorSelection.Caret(Cell(engine, 2, 1), 0));
        engine.TabInTable(backward: false);
        var cell = engine.Blocks[engine.Selection.Focus.Block];
        (cell.Row, cell.Col).ShouldBe((3, 0));
        engine.Blocks.Count(b => b.IsCell).ShouldBe(12);
    }

    [Fact]
    public void Inserting_A_Row_Inside_A_Merged_Cell_Grows_It()
    {
        var engine = Engine(Grid3);
        Select(engine, (0, 0), (2, 0));
        engine.MergeCells();
        engine.SetSelection(RichTextEditorSelection.Caret(Cell(engine, 1, 1), 0));
        engine.AddTableRow(below: false).ShouldBeTrue();
        var head = engine.Blocks[Cell(engine, 0, 0)];
        head.RowSpan.ShouldBe(4);
        engine.Blocks[Cell(engine, 1, 0)].IsCovered.ShouldBeTrue();
        engine.Blocks[Cell(engine, 1, 1)].IsCovered.ShouldBeFalse();
        engine.Blocks.Count(b => b.IsCell).ShouldBe(12);
    }

    [Fact]
    public void Adding_A_Row_Below_A_Merged_Cell_Goes_After_Its_Last_Row_And_Does_Not_Grow_It()
    {
        var engine = Engine(Grid3);
        Select(engine, (1, 0), (2, 0));
        engine.MergeCells();
        engine.SetSelection(RichTextEditorSelection.Caret(Cell(engine, 1, 0), 0));
        engine.AddTableRow().ShouldBeTrue();
        engine.Blocks[Cell(engine, 1, 0)].RowSpan.ShouldBe(2);
        engine.Blocks[Cell(engine, 3, 0)].IsCovered.ShouldBeFalse();
    }

    [Fact]
    public void Inserting_A_Column_Inside_A_Merged_Cell_Grows_It()
    {
        var engine = Engine(Grid3);
        Select(engine, (0, 0), (0, 2));
        engine.MergeCells();
        engine.SetSelection(RichTextEditorSelection.Caret(Cell(engine, 1, 0), 0));
        engine.AddTableColumn(after: true).ShouldBeTrue();
        engine.Blocks[Cell(engine, 0, 0)].ColSpan.ShouldBe(4);
        engine.Blocks[Cell(engine, 0, 2)].IsCovered.ShouldBeTrue();
        engine.Blocks[Cell(engine, 1, 1)].IsCovered.ShouldBeFalse();
    }

    [Fact]
    public void Deleting_The_First_Row_Of_A_Merged_Cell_Moves_It_Down()
    {
        var engine = Engine(Grid3);
        Select(engine, (0, 0), (1, 1));
        engine.MergeCells();
        engine.SetSelection(RichTextEditorSelection.Caret(Cell(engine, 0, 0), 0));
        engine.DeleteTableRow().ShouldBeTrue();
        var head = engine.Blocks[Cell(engine, 0, 0)];
        head.Text.ShouldBe("a\nb\nd\ne");
        (head.RowSpan, head.ColSpan).ShouldBe((1, 2));
        engine.Blocks[Cell(engine, 0, 1)].IsCovered.ShouldBeTrue();
        engine.Blocks.Count(b => b.IsCell).ShouldBe(6);
    }

    [Fact]
    public void Deleting_A_Row_Across_A_Merged_Cell_Shrinks_It()
    {
        var engine = Engine(Grid3);
        Select(engine, (0, 0), (2, 0));
        engine.MergeCells();
        engine.SetSelection(RichTextEditorSelection.Caret(Cell(engine, 1, 1), 0));
        engine.DeleteTableRow().ShouldBeTrue();
        engine.Blocks[Cell(engine, 0, 0)].RowSpan.ShouldBe(2);
        engine.Blocks.Count(b => b.IsCell).ShouldBe(6);
    }

    [Fact]
    public void Deleting_Columns_Moves_And_Shrinks_Merged_Cells()
    {
        var engine = Engine(Grid3);
        Select(engine, (0, 0), (1, 1));
        engine.MergeCells();
        engine.SetSelection(RichTextEditorSelection.Caret(Cell(engine, 2, 1), 0));
        engine.DeleteTableColumn().ShouldBeTrue();
        var head = engine.Blocks[Cell(engine, 0, 0)];
        (head.RowSpan, head.ColSpan).ShouldBe((2, 1));
        engine.Blocks.Count(b => b.IsCell).ShouldBe(6);
        engine.Blocks.Count(b => b.IsCovered).ShouldBe(1);
        engine.SetSelection(RichTextEditorSelection.Caret(Cell(engine, 0, 0), 0));
        engine.DeleteTableColumn().ShouldBeTrue();
        var moved = engine.Blocks[Cell(engine, 0, 0)];
        moved.IsMerged.ShouldBeFalse();
        moved.IsCovered.ShouldBeFalse();
    }

    [Fact]
    public void Html_Round_Trips_Colspan_And_Rowspan()
    {
        var html = "<table><thead><tr><th colspan=\"2\">A</th><th>B</th></tr></thead><tbody><tr><td rowspan=\"2\">c</td><td>d</td><td>e</td></tr><tr><td colspan=\"2\">f</td></tr></tbody></table>";
        var blocks = RichTextEditorHtml.ToDocument(html, docs: true);
        blocks.Count(b => b.IsCell).ShouldBe(9);
        blocks.Count(b => b.IsCovered).ShouldBe(3);
        RichTextEditorHtml.Write(blocks).ShouldStartWith("<table><thead><tr><th colspan=\"2\">A</th><th>B</th></tr></thead><tbody><tr><td rowspan=\"2\">c</td><td>d</td><td>e</td></tr><tr><td colspan=\"2\">f</td></tr>");
        RichTextEditorHtml.Write(RichTextEditorHtml.ToDocument(RichTextEditorHtml.Write(blocks), docs: true)).ShouldBe(RichTextEditorHtml.Write(blocks));
    }

    [Fact]
    public void Html_Spans_Are_Clamped_To_The_Grid()
    {
        var blocks = RichTextEditorHtml.ToDocument("<table><tr><th colspan=\"9\">a</th></tr><tr><td rowspan=\"7\">b</td><td>c</td></tr></table>", docs: true);
        var cells = blocks.Where(b => b.IsCell).ToList();
        cells.Count.ShouldBe(cells.Max(c => c.Row + 1) * (cells.Max(c => c.Col) + 1));
        cells.Max(c => c.Row + c.RowSpan).ShouldBeLessThanOrEqualTo(cells.Max(c => c.Row) + 1);
        cells.Max(c => c.Col + c.ColSpan).ShouldBeLessThanOrEqualTo(cells.Max(c => c.Col) + 1);
    }

    [Fact]
    public void Repair_Fixes_Overlaps_And_Stray_Covered_Cells()
    {
        // The second merged cell overlaps the first; a covered cell with no owner is a normal cell again.
        var cells = RichTextEditorTables.Create(2, 3);
        cells[0] = cells[0].WithAttrs(cells[0].Attrs with { ColSpan = 2, RowSpan = 2 });
        cells[1] = cells[1].WithAttrs(cells[1].Attrs with { Covered = true });
        cells[2] = cells[2].WithAttrs(cells[2].Attrs with { Covered = true });
        cells[4] = cells[4].WithAttrs(cells[4].Attrs with { ColSpan = 2 });
        var blocks = RichTextEditorEngine.Normalize(cells.Append(RichTextEditorBlock.EmptyParagraph).ToImmutableList());
        blocks[2].IsCovered.ShouldBeFalse();
        blocks[3].IsCovered.ShouldBeTrue();
        blocks[4].IsCovered.ShouldBeTrue();
        blocks[4].IsMerged.ShouldBeFalse();
        blocks[5].IsCovered.ShouldBeFalse();
    }

    [Fact]
    public void Markdown_Writes_Merged_Cells_As_A_Plain_Pipe_Table()
    {
        var blocks = RichTextEditorHtml.ToDocument("<table><tr><th colspan=\"2\">A</th></tr><tr><td>b</td><td>c</td></tr></table>", docs: true);
        RichTextEditorMarkdown.Write(blocks).ShouldBe("| A |  |\n| --- | --- |\n| b | c |");
    }

    [Fact]
    public void Arc_Profile_Drops_Tables_And_So_Their_Spans()
    {
        RichTextEditorHtml.Write(RichTextEditorHtml.ToDocument("<table><tr><th colspan=\"2\">A</th></tr></table>", docs: false)).ShouldNotContain("colspan");
    }

    [AvaloniaFact]
    public void A_Merged_Cell_Spans_Its_Columns_And_Rows_And_Clicks_Land_In_It()
    {
        var editor = new RichTextEditor { Width = 480, Profile = RichTextEditorProfile.Docs, Html = Grid3 + "<p>after</p>" };
        var window = TestHelpers.Show(new Border { Padding = new Thickness(20, 80, 20, 20), Child = editor });
        window.Width = 540;
        window.Height = 600;
        Dispatcher.UIThread.RunJobs();
        try
        {
            var surface = TestHelpers.Descendant<RichTextEditorSurface>(editor);
            // Merge the top left 2 x 2 block through the public API.
            var engineBlocks = editor.Engine.Blocks;
            engineBlocks.Count(b => b.IsCell).ShouldBe(9);
            var before = surface.CaretRect(new RichTextEditorPosition(2, 0));
            var firstRow = surface.CaretRect(new RichTextEditorPosition(0, 0));
            before.X.ShouldBeGreaterThan(firstRow.X);

            editor.Engine.SetSelection(new RichTextEditorSelection(new RichTextEditorPosition(0, 0), new RichTextEditorPosition(4, 0)));
            editor.CanMergeTableCells.ShouldBeTrue();
            editor.MergeTableCells().ShouldBeTrue();
            Dispatcher.UIThread.RunJobs();

            var merged = editor.Engine.Blocks[0];
            (merged.RowSpan, merged.ColSpan).ShouldBe((2, 2));
            // The third column's header now lines up with the merged cell's right edge, and the third row sits below both merged rows.
            var third = surface.CaretRect(new RichTextEditorPosition(2, 0));
            Math.Abs(third.X - before.X).ShouldBeLessThan(1);
            var bottom = surface.CaretRect(new RichTextEditorPosition(6, 0));
            var middleRight = surface.CaretRect(new RichTextEditorPosition(5, 0));
            bottom.Y.ShouldBeGreaterThan(middleRight.Y);
            // A click in the covered area resolves to the merged cell; so does moving down out of its first row.
            surface.HitTest(new Point(firstRow.X + surface.Bounds.Width / 3 + 6, middleRight.Y + 2)).Block.ShouldBe(0);
            surface.HitTest(new Point(firstRow.X + 4, middleRight.Y + 2)).Block.ShouldBe(0);
            surface.MoveVertical(new RichTextEditorPosition(0, merged.Length), firstRow.X, 1).Block.ShouldBe(6);
            surface.MoveVertical(new RichTextEditorPosition(6, 0), firstRow.X, -1).Block.ShouldBe(0);
        }
        finally
        {
            window.Close();
        }
    }
}
