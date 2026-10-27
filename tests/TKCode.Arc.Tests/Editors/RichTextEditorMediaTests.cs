using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;

namespace TKCode.Arc.Tests.Editors;

/// <summary>Images and tables of the Docs profile: converters, engine editing and the Arc profile staying as it was.</summary>
public class RichTextEditorMediaTests
{
    private static RichTextEditorEngine Engine(string html, bool docs = true)
    {
        var engine = new RichTextEditorEngine { Docs = docs };
        engine.Load(RichTextEditorHtml.ToDocument(html, docs), record: false, caretAtEnd: true);
        return engine;
    }

    [Fact]
    public void Image_Html_Round_Trips()
    {
        var html = "<p>a</p><img src=\"https://x.test/a.png\" alt=\"A cat\"><p>b</p>";
        var blocks = RichTextEditorHtml.ToDocument(html, docs: true);
        blocks.Select(b => b.Type).ShouldBe(new[] { RichTextBlockType.Paragraph, RichTextBlockType.Image, RichTextBlockType.Paragraph });
        blocks[1].Src.ShouldBe("https://x.test/a.png");
        blocks[1].Alt.ShouldBe("A cat");
        RichTextEditorHtml.Write(blocks).ShouldBe(html);
    }

    [Fact]
    public void Images_Are_Dropped_Outside_Docs_And_Unsafe_Sources_Are_Refused()
    {
        RichTextEditorHtml.Write(RichTextEditorHtml.ToDocument("<p>a</p><img src=\"https://x.test/a.png\">", docs: false)).ShouldBe("<p>a</p>");
        RichTextEditorHtml.Write(RichTextEditorHtml.ToDocument("<p>a</p><img src=\"javascript:alert(1)\"><img src=\"data:text/html;base64,AAAA\">", docs: true)).ShouldBe("<p>a</p>");
        RichTextEditorHtml.IsSafeImageUrl("data:image/png;base64,AAAA").ShouldBeTrue();
        RichTextEditorHtml.IsSafeImageUrl("myapp://files/1").ShouldBeTrue();
    }

    [Fact]
    public void Image_Markdown_Round_Trips()
    {
        var blocks = RichTextEditorMarkdown.ToDocument("before\n\n![A cat](https://x.test/a.png)\n\nafter", docs: true);
        blocks[1].IsImage.ShouldBeTrue();
        blocks[1].Alt.ShouldBe("A cat");
        RichTextEditorMarkdown.Write(blocks).ShouldBe("before\n\n![A cat](https://x.test/a.png)\n\nafter");
    }

    [Fact]
    public void Table_Html_And_Markdown_Round_Trip()
    {
        var markdown = "| Name | Qty |\n| --- | ---: |\n| **Tea** | 2 |\n| Milk | 10 |";
        var blocks = RichTextEditorMarkdown.ToDocument(markdown, docs: true);
        blocks.Count(b => b.IsCell).ShouldBe(6);
        blocks[3].Align.ShouldBe(RichTextAlign.End);
        RichTextEditorMarkdown.Write(blocks).ShouldBe(markdown);
        var html = RichTextEditorHtml.Write(blocks);
        html.ShouldStartWith("<table><thead><tr><th>Name</th><th data-align=\"end\">Qty</th></tr></thead><tbody>");
        RichTextEditorHtml.Write(RichTextEditorHtml.ToDocument(html, docs: true)).ShouldBe(html);
    }

    [Fact]
    public void Table_Is_Plain_Text_In_The_Arc_Profile()
    {
        RichTextEditorMarkdown.ToDocument("| a | b |\n| - | - |\n| 1 | 2 |", docs: false).Any(b => b.IsCell).ShouldBeFalse();
        RichTextEditorMarkdown.Analyze("| a |\n|---|\n", RichTextEditorProfile.Docs).IsEmpty.ShouldBeTrue();
        RichTextEditorMarkdown.Analyze("![a](b)", RichTextEditorProfile.Docs).IsEmpty.ShouldBeTrue();
        RichTextEditorMarkdown.Analyze("| a |\n|---|\n").HasLosses.ShouldBeTrue();
    }

    [Fact]
    public void Insert_Image_Splits_A_Paragraph_And_Leaves_A_Caret_After_It()
    {
        var engine = Engine("<p>hello world</p>");
        engine.SetSelection(RichTextEditorSelection.Caret(0, 5));
        engine.InsertImage("https://x.test/a.png", "a").ShouldBe(1);
        engine.Blocks.Select(b => b.Type).ShouldBe(new[] { RichTextBlockType.Paragraph, RichTextBlockType.Image, RichTextBlockType.Paragraph });
        engine.Blocks[0].Text.ShouldBe("hello");
        engine.Blocks[2].Text.ShouldBe(" world");
        engine.Selection.Focus.Block.ShouldBe(2);
    }

    [Fact]
    public void Image_Width_Is_Kept_In_Html_And_Markdown_And_Undone()
    {
        var engine = Engine("<img src=\"x.png\" alt=\"a\">");
        engine.SetImageWidth(0, 320).ShouldBeTrue();
        engine.Blocks[0].Width.ShouldBe(320);
        var html = RichTextEditorHtml.Write(engine.Blocks);
        html.ShouldBe("<img src=\"x.png\" alt=\"a\" width=\"320\">");
        var markdown = RichTextEditorMarkdown.Write(engine.Blocks);
        RichTextEditorMarkdown.ToDocument(markdown, docs: true)[0].Width.ShouldBe(320);
        RichTextEditorMarkdown.Analyze(markdown, RichTextEditorProfile.Docs).IsEmpty.ShouldBeTrue();
        engine.Undo().ShouldBeTrue();
        engine.Blocks[0].Width.ShouldBe(0);
    }

    [Fact]
    public void Images_Need_The_Docs_Profile_And_A_Safe_Source()
    {
        Engine("<p>x</p>", docs: false).InsertImage("https://x.test/a.png", null).ShouldBe(-1);
        Engine("<p>x</p>").InsertImage("javascript:alert(1)", null).ShouldBe(-1);
    }

    [Fact]
    public void Selected_Image_Is_Deleted_And_Undone()
    {
        var engine = Engine("<p>a</p><img src=\"x.png\"><p>b</p>");
        engine.SelectImage(1);
        engine.IsImageSelected.ShouldBeTrue();
        engine.DeleteBackward();
        engine.Blocks.Count.ShouldBe(2);
        engine.Undo().ShouldBeTrue();
        engine.Blocks[1].IsImage.ShouldBeTrue();
    }

    [Fact]
    public void Backspace_At_The_Start_Of_The_Next_Paragraph_Removes_The_Image()
    {
        var engine = Engine("<img src=\"x.png\"><p>b</p>");
        engine.SetSelection(RichTextEditorSelection.Caret(1, 0));
        engine.DeleteBackward();
        engine.Blocks.Any(b => b.IsImage).ShouldBeFalse();
    }

    [Fact]
    public void Typing_Markdown_Image_Syntax_Makes_An_Image()
    {
        var engine = Engine("<p></p>");
        foreach (var c in "![cat](https://x.test/c.png)")
        {
            engine.InsertText(c.ToString());
        }
        engine.Blocks[0].IsImage.ShouldBeTrue();
        engine.Blocks[0].Alt.ShouldBe("cat");
    }

    [Fact]
    public void Insert_Table_Moves_Into_The_First_Cell_And_Keeps_A_Paragraph_After()
    {
        var engine = Engine("<p>x</p>");
        engine.InsertTable(2, 3).ShouldBeTrue();
        engine.Blocks.Count(b => b.IsCell).ShouldBe(6);
        engine.Blocks[^1].Type.ShouldBe(RichTextBlockType.Paragraph);
        engine.IsInTable.ShouldBeTrue();
        engine.Blocks[engine.Selection.Focus.Block].StartsTable.ShouldBeTrue();
        engine.InsertTable(0, 3).ShouldBeFalse();
    }

    [Fact]
    public void Tab_Walks_The_Cells_And_Adds_A_Row_At_The_End()
    {
        var engine = Engine("<p>x</p>");
        engine.InsertTable(2, 2);
        for (var i = 0; i < 3; i++)
        {
            engine.TabInTable(backward: false);
        }
        engine.Blocks.Count(b => b.IsCell).ShouldBe(4);
        engine.TabInTable(backward: false);
        engine.Blocks.Count(b => b.IsCell).ShouldBe(6);
        var cell = engine.Blocks[engine.Selection.Focus.Block];
        (cell.Row, cell.Col).ShouldBe((2, 0));
        engine.TabInTable(backward: true);
        engine.Blocks[engine.Selection.Focus.Block].Col.ShouldBe(1);
    }

    [Fact]
    public void Enter_In_A_Cell_Moves_Down_The_Column()
    {
        var engine = Engine("<p>x</p>");
        engine.InsertTable(2, 2);
        engine.InsertText("a");
        engine.InsertParagraph();
        var cell = engine.Blocks[engine.Selection.Focus.Block];
        (cell.Row, cell.Col).ShouldBe((1, 0));
        engine.InsertParagraph();
        engine.Blocks.Count(b => b.IsCell).ShouldBe(6);
    }

    [Fact]
    public void Rows_And_Columns_Are_Added_And_Deleted()
    {
        var engine = Engine("<p>x</p>");
        engine.InsertTable(2, 2);
        engine.AddTableColumn().ShouldBeTrue();
        engine.AddTableRow().ShouldBeTrue();
        engine.Blocks.Count(b => b.IsCell).ShouldBe(9);
        engine.Blocks.Where(b => b.IsCell).Select((b, i) => (b.Row, b.Col) == (i / 3, i % 3)).ShouldAllBe(ok => ok);
        engine.DeleteTableColumn().ShouldBeTrue();
        engine.DeleteTableRow().ShouldBeTrue();
        engine.Blocks.Count(b => b.IsCell).ShouldBe(4);
        engine.DeleteTable().ShouldBeTrue();
        engine.Blocks.Any(b => b.IsCell).ShouldBeFalse();
    }

    [Fact]
    public void Deleting_Across_Cells_Empties_Them_Without_Merging()
    {
        var engine = Engine("<table><tr><th>a</th><th>b</th></tr><tr><td>c</td><td>d</td></tr></table>");
        var cells = engine.Blocks.Select((b, i) => (b, i)).Where(x => x.b.IsCell).Select(x => x.i).ToList();
        engine.SetSelection(new RichTextEditorSelection(new RichTextEditorPosition(cells[0], 0), new RichTextEditorPosition(cells[3], 1)));
        engine.DeleteBackward();
        engine.Blocks.Count(b => b.IsCell).ShouldBe(4);
        engine.Blocks.Where(b => b.IsCell).ShouldAllBe(b => b.Length == 0);
    }

    [Fact]
    public void Selecting_Over_A_Whole_Table_Deletes_It()
    {
        var engine = Engine("<p>a</p><table><tr><th>h</th></tr><tr><td>c</td></tr></table><p>b</p>");
        engine.SelectAll();
        engine.DeleteBackward();
        engine.Blocks.Any(b => b.IsCell).ShouldBeFalse();
    }

    [Fact]
    public void Backspace_Below_A_Table_Does_Not_Merge_Into_Its_Last_Cell()
    {
        var engine = Engine("<table><tr><th>h</th></tr></table><p>after</p>");
        var paragraph = engine.Blocks.Count - 1;
        engine.SetSelection(RichTextEditorSelection.Caret(paragraph, 0));
        engine.DeleteBackward();
        engine.Blocks[paragraph - 1].Text.ShouldBe("h");
        engine.Blocks[paragraph].Text.ShouldBe("after");
    }

    [Fact]
    public void Alignment_Applies_To_The_Whole_Column()
    {
        var engine = Engine("<table><tr><th>a</th><th>b</th></tr><tr><td>c</td><td>d</td></tr></table>");
        var first = engine.Blocks.Select((b, i) => (b, i)).First(x => x.b.IsCell).i;
        engine.SetSelection(RichTextEditorSelection.Caret(first, 0));
        engine.SetAlign(RichTextAlign.Center).ShouldBeTrue();
        engine.Blocks.Where(b => b.IsCell && b.Col == 0).ShouldAllBe(b => b.Align == RichTextAlign.Center);
        engine.Blocks.Where(b => b.IsCell && b.Col == 1).ShouldAllBe(b => b.Align == RichTextAlign.Start);
    }

    [Fact]
    public void Ragged_Tables_Are_Padded_To_A_Grid()
    {
        var blocks = RichTextEditorHtml.ToDocument("<table><tr><th>a</th><th>b</th></tr><tr><td>c</td></tr></table>", docs: true);
        blocks.Count(b => b.IsCell).ShouldBe(4);
    }

    private const string Pixel = "data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR4nGP4z8DwHwAFAAH/q842iQAAAABJRU5ErkJggg==";

    private static (RichTextEditor Editor, Window Window) Show(string html)
    {
        var editor = new RichTextEditor { Width = 480, Profile = RichTextEditorProfile.Docs, Html = html };
        var window = TestHelpers.Show(new Border { Padding = new Thickness(20, 80, 20, 20), Child = editor });
        window.Width = 540;
        window.Height = 600;
        Dispatcher.UIThread.RunJobs();
        return (editor, window);
    }

    [AvaloniaFact]
    public void Table_Lays_Out_As_A_Grid_And_Clicks_Land_In_The_Cell()
    {
        var (editor, window) = Show("<table><tr><th>a</th><th>b</th></tr><tr><td>c</td><td>d</td></tr></table><p>after</p>");
        try
        {
            var surface = TestHelpers.Descendant<RichTextEditorSurface>(editor);
            var second = surface.CaretRect(new RichTextEditorPosition(1, 0));
            var third = surface.CaretRect(new RichTextEditorPosition(2, 0));
            second.X.ShouldBeGreaterThan(surface.Bounds.Width / 2 - 1);
            third.Y.ShouldBeGreaterThan(second.Y);
            surface.HitTest(new Point(second.X + 4, second.Y + 2)).ShouldBe(new RichTextEditorPosition(1, 0));
            surface.HitTest(new Point(third.X + 4, third.Y + 2)).ShouldBe(new RichTextEditorPosition(2, 0));
            // Down from the header's second cell goes to the second row's second cell.
            surface.MoveVertical(new RichTextEditorPosition(1, 0), second.X, 1).Block.ShouldBe(3);
            surface.MoveVertical(new RichTextEditorPosition(3, 0), second.X, 1).Block.ShouldBe(4);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Image_Loads_Through_The_Host_And_Clicking_Selects_It()
    {
        var (editor, window) = Show("<p>before</p><img src=\"app://one\" alt=\"one\"><p>after</p>");
        try
        {
            var asked = new List<string>();
            editor.ImageLoading += (_, e) =>
            {
                asked.Add(e.Source);
                e.StreamTask = Task.FromResult<Stream?>(new MemoryStream(Convert.FromBase64String(Pixel[22..])));
            };
            editor.ReloadImages();
            Dispatcher.UIThread.RunJobs();
            for (var i = 0; i < 50 && asked.Count == 0; i++)
            {
                Thread.Sleep(10);
                Dispatcher.UIThread.RunJobs();
            }
            asked.ShouldContain("app://one");
            var surface = TestHelpers.Descendant<RichTextEditorSurface>(editor);
            var top = surface.BlockTop(1);
            var point = surface.TranslatePoint(new Point(10, top + 4), window)!.Value;
            window.MouseDown(point, MouseButton.Left);
            window.MouseUp(point, MouseButton.Left);
            Dispatcher.UIThread.RunJobs();
            editor.Engine.IsImageSelected.ShouldBeTrue();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public async Task Picked_Files_Ask_The_Host_Where_To_Keep_Them()
    {
        var (editor, window) = Show("<p>x</p>");
        try
        {
            RichTextImageSourceRequestedEventArgs? seen = null;
            editor.ImageSourceRequested += (_, e) =>
            {
                seen = e;
                e.SourceTask = Task.FromResult<string?>("https://cdn.test/up/1.png");
            };
            editor.InsertImage("https://cdn.test/up/0.png", "zero").ShouldBeTrue();
            editor.GetHtml().ShouldContain("up/0.png");
            seen.ShouldBeNull();
            await Task.CompletedTask;
        }
        finally
        {
            window.Close();
        }
    }
}
