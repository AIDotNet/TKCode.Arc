using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;

namespace TKCode.Arc.Tests.Editors;

/// <summary>Rectangles of cells in the Docs profile: selecting, deleting, copying and pasting them, and moving rows and columns.</summary>
public class RichTextEditorCellSelectionTests
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
        engine.SetSelection(new RichTextEditorSelection(new RichTextEditorPosition(Cell(engine, from.Row, from.Col), 0), new RichTextEditorPosition(Cell(engine, to.Row, to.Col), 1)));

    private static string Texts(RichTextEditorEngine engine) =>
        string.Join("|", engine.Blocks.Where(b => b.IsCell).GroupBy(b => b.Row).Select(row => string.Join(",", row.Select(b => b.Text))));

    [Fact]
    public void A_Selection_Between_Two_Cells_Is_The_Rectangle_Between_Them()
    {
        var engine = Engine(Grid3);
        Select(engine, (0, 1), (1, 2));
        engine.CellSelection.ShouldNotBeNull();
        var rect = engine.CellSelection!.Value;
        (rect.R1, rect.C1, rect.R2, rect.C2).ShouldBe((0, 1, 1, 2));
        // Backwards gives the same rectangle.
        Select(engine, (1, 0), (0, 1));
        (engine.CellSelection!.Value.R1, engine.CellSelection.Value.C1, engine.CellSelection.Value.R2, engine.CellSelection.Value.C2).ShouldBe((0, 0, 1, 1));
        // Inside one cell, or from the first character of the table to its last, is not a rectangle of cells.
        engine.SetSelection(RichTextEditorSelection.Range(Cell(engine, 1, 1), 0, 1));
        engine.CellSelection.ShouldBeNull();
        engine.SetSelection(new RichTextEditorSelection(new RichTextEditorPosition(0, 0), new RichTextEditorPosition(Cell(engine, 2, 2), 1)));
        engine.CellSelection.ShouldBeNull();
    }

    [Fact]
    public void Deleting_Or_Typing_Over_A_Rectangle_Empties_Only_Its_Cells()
    {
        var engine = Engine(Grid3);
        Select(engine, (0, 1), (1, 2));
        engine.DeleteBackward();
        Texts(engine).ShouldBe("a,,|d,,|g,h,i");
        engine.Selection.Focus.ShouldBe(new RichTextEditorPosition(Cell(engine, 0, 1), 0));
        engine.Undo();
        Select(engine, (1, 0), (2, 1));
        engine.InsertText("x");
        Texts(engine).ShouldBe("a,b,c|x,,f|,,i");
        engine.Selection.Focus.ShouldBe(new RichTextEditorPosition(Cell(engine, 1, 0), 1));
    }

    [Fact]
    public void Copying_A_Rectangle_Gives_Tab_Separated_Text_And_A_Table()
    {
        var engine = Engine(Grid3);
        Select(engine, (1, 1), (2, 2));
        engine.GetSelectedText().ShouldBe("e\tf\nh\ti");
        var fragment = engine.GetFragment();
        fragment.Count.ShouldBe(4);
        fragment.ShouldAllBe(b => b.IsCell);
        fragment.Select(b => (b.Row, b.Col, b.Text)).ShouldBe(new[] { (0, 0, "e"), (0, 1, "f"), (1, 0, "h"), (1, 1, "i") });
        RichTextEditorMarkdown.Write(fragment).ShouldContain("| e | f |");
    }

    [Fact]
    public void Pasting_A_Table_Into_A_Cell_Fills_Cells_And_Grows_The_Table()
    {
        var engine = Engine(Grid3);
        Select(engine, (0, 0), (1, 1));
        var fragment = engine.GetFragment();
        engine.SetSelection(RichTextEditorSelection.Caret(Cell(engine, 2, 2), 0));
        engine.Paste(engine.GetSelectedText(), fragment);
        Texts(engine).ShouldBe("a,b,c,|d,e,f,|g,h,a,b|,,d,e");
        engine.Selection.Focus.ShouldBe(new RichTextEditorPosition(Cell(engine, 3, 3), 1));
        engine.Undo();
        Texts(engine).ShouldBe("a,b,c|d,e,f|g,h,i");
    }

    [Fact]
    public void Pasting_Tab_Separated_Text_Starts_At_The_Selected_Rectangle()
    {
        var engine = Engine(Grid3);
        Select(engine, (1, 1), (2, 2));
        engine.Paste("1\t2\r\n3\t4\r\n", null);
        Texts(engine).ShouldBe("a,b,c|d,1,2|g,3,4");
        // Text without tabs still goes into the cell as text.
        engine.SetSelection(RichTextEditorSelection.Caret(Cell(engine, 0, 0), 1));
        engine.Paste("z", null);
        engine.Blocks[Cell(engine, 0, 0)].Text.ShouldBe("az");
    }

    [Fact]
    public void Pasting_Skips_Covered_Cells()
    {
        var engine = Engine(Grid3);
        Select(engine, (1, 0), (1, 1));
        engine.MergeCells().ShouldBeTrue();
        engine.SetSelection(RichTextEditorSelection.Caret(Cell(engine, 1, 0), 0));
        engine.Paste("p\tq\tr", null);
        engine.Blocks[Cell(engine, 1, 0)].Text.ShouldBe("p");
        engine.Blocks[Cell(engine, 1, 1)].IsCovered.ShouldBeTrue();
        engine.Blocks[Cell(engine, 1, 2)].Text.ShouldBe("r");
    }

    [Fact]
    public void A_Copied_Rectangle_Pasted_Outside_A_Table_Is_A_Table()
    {
        var engine = Engine(Grid3 + "<p>after</p>");
        Select(engine, (0, 0), (1, 1));
        var fragment = engine.GetFragment();
        var paragraph = engine.Blocks.Count - 1;
        engine.SetSelection(RichTextEditorSelection.Caret(paragraph, 5));
        engine.Paste(engine.GetSelectedText(), fragment);
        engine.Blocks.Count(b => b.StartsTable).ShouldBe(2);
        RichTextEditorHtml.Write(engine.Blocks).ShouldContain("<table><thead><tr><th>a</th><th>b</th></tr></thead><tbody><tr><td>d</td><td>e</td></tr></tbody></table>");
    }

    [Fact]
    public void Selecting_A_Row_Or_Column_Picks_All_Its_Cells()
    {
        var engine = Engine(Grid3);
        engine.SelectTableCells(Cell(engine, 2, 2), 1, -1);
        var rect = engine.CellSelection!.Value;
        (rect.R1, rect.C1, rect.R2, rect.C2).ShouldBe((1, 0, 1, 2));
        engine.SelectTableCells(0, -1, 2);
        rect = engine.CellSelection!.Value;
        (rect.R1, rect.C1, rect.R2, rect.C2).ShouldBe((0, 2, 2, 2));
        engine.GetSelectedText().ShouldBe("c\nf\ni");
    }

    [Fact]
    public void Rows_And_Columns_Move_With_The_Caret_And_The_Widths()
    {
        var engine = Engine(Grid3);
        engine.SetColumnWidths(0, new[] { 10, 20, 30 });
        engine.SetSelection(RichTextEditorSelection.Caret(Cell(engine, 1, 1), 1));
        engine.CanMoveTableRow(-1).ShouldBeTrue();
        engine.MoveTableRow(1, 3).ShouldBeTrue();
        Texts(engine).ShouldBe("a,b,c|g,h,i|d,e,f");
        engine.Selection.Focus.ShouldBe(new RichTextEditorPosition(Cell(engine, 2, 1), 1));
        engine.MoveTableRow(2, 0).ShouldBeTrue();
        Texts(engine).ShouldBe("d,e,f|a,b,c|g,h,i");
        // The first row is the header whatever lands there, and the widths stay with their columns.
        engine.HasHeaderRow.ShouldBeTrue();
        engine.MoveTableColumn(1, 0).ShouldBeTrue();
        Texts(engine).ShouldBe("e,d,f|b,a,c|h,g,i");
        engine.Blocks.Where(b => b.IsCell && b.Row == 0).Select(b => b.Width).ShouldBe(new[] { 20, 10, 30 });
        engine.Selection.Focus.ShouldBe(new RichTextEditorPosition(Cell(engine, 0, 0), 1));
        engine.MoveTableColumn(0, 1).ShouldBeFalse();
        engine.Undo();
        Texts(engine).ShouldBe("d,e,f|a,b,c|g,h,i");
    }

    [Fact]
    public void Merged_Cells_Hold_The_Rows_And_Columns_They_Span()
    {
        var engine = Engine(Grid3);
        Select(engine, (0, 1), (1, 1));
        engine.MergeCells().ShouldBeTrue();
        // Rows 0 and 1 are held together; row 2 cannot move between them but can move above both.
        engine.SetSelection(RichTextEditorSelection.Caret(Cell(engine, 2, 0), 0));
        engine.MoveTableRow(2, 1).ShouldBeFalse();
        engine.MoveTableRow(0, 3).ShouldBeFalse();
        engine.MoveTableRow(2, 0).ShouldBeTrue();
        Texts(engine).ShouldBe("g,h,i|a,b\ne,c|d,,f");
        // A merged cell spanning one column does not hold the columns.
        engine.MoveTableColumn(1, 3).ShouldBeTrue();
        Texts(engine).ShouldBe("g,i,h|a,c,b\ne|d,f,");
    }

    [AvaloniaFact]
    public void The_Grips_Select_And_Move_Rows_And_Columns()
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
            var top = surface.CaretRect(new RichTextEditorPosition(0, 0)).Y;
            var row1 = surface.CaretRect(new RichTextEditorPosition(3, 0));
            var row2 = surface.CaretRect(new RichTextEditorPosition(6, 0));
            Point InWindow(Point p) => surface.TranslatePoint(p, window)!.Value;
            var columnGrip = new Point(width / 2, top - 4);
            // The grips show where the pointer is.
            window.MouseMove(InWindow(new Point(width / 2, row1.Center.Y)));
            surface.GripAt(columnGrip).ShouldBe((0, true, 1));
            surface.GripAt(new Point(4, row1.Center.Y)).ShouldBe((0, false, 1));

            window.MouseDown(InWindow(columnGrip), MouseButton.Left);
            window.MouseUp(InWindow(columnGrip), MouseButton.Left);
            Dispatcher.UIThread.RunJobs();
            var rect = editor.Engine.CellSelection!.Value;
            (rect.R1, rect.C1, rect.R2, rect.C2).ShouldBe((0, 1, 2, 1));

            // Drag row 2's grip above row 1.
            var rowGrip = new Point(4, row2.Center.Y);
            window.MouseMove(InWindow(rowGrip));
            surface.GripAt(rowGrip).ShouldBe((0, false, 2));
            window.MouseDown(InWindow(rowGrip), MouseButton.Left);
            window.MouseMove(InWindow(new Point(4, row1.Y - 2)));
            window.MouseUp(InWindow(new Point(4, row1.Y - 2)), MouseButton.Left);
            Dispatcher.UIThread.RunJobs();
            editor.Html.ShouldContain("<tbody><tr><td>g</td><td>h</td><td>i</td></tr><tr><td>d</td>");
            editor.Engine.Blocks[editor.Engine.Selection.Focus.Block].Text.ShouldBe("g");
        }
        finally
        {
            window.Close();
        }
    }
}
