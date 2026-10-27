using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;

namespace TKCode.Arc.Tests.Editors;

/// <summary>Headings 4 to 6 and superscript and subscript: Docs profile only, through HTML, Markdown, shortcuts and the layout.</summary>
public class RichTextEditorHeadingScriptTests
{
    private static RichTextEditorEngine Engine(string html, bool docs = true)
    {
        var engine = new RichTextEditorEngine { Docs = docs };
        engine.Load(RichTextEditorHtml.ToDocument(html, docs), record: false, caretAtEnd: true);
        return engine;
    }

    [Fact]
    public void Docs_Keeps_Headings_4_To_6_And_Arc_Folds_Them_Into_Heading_3()
    {
        const string html = "<h4>four</h4><h5>five</h5><h6>six</h6>";
        var docs = Engine(html);
        docs.Blocks.Take(3).Select(b => b.Type).ShouldBe(new[] { RichTextBlockType.Heading4, RichTextBlockType.Heading5, RichTextBlockType.Heading6 });
        docs.Blocks.Take(3).ShouldAllBe(b => b.IsHeading);
        RichTextEditorHtml.Write(docs.Blocks).ShouldBe(html);
        RichTextEditorMarkdown.Write(docs.Blocks).ShouldBe("#### four\n\n##### five\n\n###### six");
        Engine(html, docs: false).Blocks.Take(3).ShouldAllBe(b => b.Type == RichTextBlockType.Heading3);

        RichTextEditorMarkdown.ToDocument("##### five", docs: true)[0].Type.ShouldBe(RichTextBlockType.Heading5);
        RichTextEditorMarkdown.ToDocument("##### five")[0].Type.ShouldBe(RichTextBlockType.Heading3);
        RichTextEditorMarkdown.Analyze("#### four", RichTextEditorProfile.Docs).Issues.ShouldBeEmpty();
        RichTextEditorMarkdown.Analyze("#### four").Issues.Single().Kind.ShouldBe(RichTextImportIssueKind.HeadingLevelDowngraded);
    }

    [Fact]
    public void Shortcuts_And_Block_Types_Reach_Headings_4_To_6_Only_In_Docs()
    {
        var docs = Engine("<p></p>");
        docs.SetSelection(RichTextEditorSelection.Caret(0, 0));
        foreach (var c in "#### ")
        {
            docs.InsertText(c.ToString());
        }
        docs.Blocks[0].Type.ShouldBe(RichTextBlockType.Heading4);
        docs.SetBlockType(RichTextBlockType.Heading6);
        docs.Blocks[0].Type.ShouldBe(RichTextBlockType.Heading6);
        docs.SetAlign(RichTextAlign.Center);
        docs.Blocks[0].Align.ShouldBe(RichTextAlign.Center);

        var arc = Engine("<p></p>", docs: false);
        arc.SetSelection(RichTextEditorSelection.Caret(0, 0));
        foreach (var c in "#### ")
        {
            arc.InsertText(c.ToString());
        }
        arc.Blocks[0].Type.ShouldBe(RichTextBlockType.Paragraph);
        arc.SetBlockType(RichTextBlockType.Heading5);
        arc.Blocks[0].Type.ShouldBe(RichTextBlockType.Paragraph);
        RichTextEditorSlashItem.Filter("heading").Select(i => i.Type).ShouldNotContain(RichTextBlockType.Heading4);
        RichTextEditorSlashItem.Filter("heading", docs: true).Select(i => i.Type).ShouldContain(RichTextBlockType.Heading6);
    }

    [Fact]
    public void Superscript_And_Subscript_Exclude_Each_Other()
    {
        var engine = Engine("<p>x2 H2O</p>");
        engine.SetSelection(RichTextEditorSelection.Range(0, 1, 2));
        engine.ToggleMark(RichTextMark.Superscript).ShouldBeTrue();
        engine.GetFormats().IsSuperscript.ShouldBeTrue();
        engine.SetSelection(RichTextEditorSelection.Range(0, 4, 5));
        engine.ToggleMark(RichTextMark.Superscript);
        engine.ToggleMark(RichTextMark.Subscript);
        var formats = engine.GetFormats();
        (formats.IsSuperscript, formats.IsSubscript).ShouldBe((false, true));
        RichTextEditorHtml.Write(engine.Blocks).ShouldBe("<p>x<sup>2</sup> H<sub>2</sub>O</p>");
        engine.ToggleMark(RichTextMark.Subscript);
        RichTextEditorHtml.Write(engine.Blocks).ShouldBe("<p>x<sup>2</sup> H2O</p>");

        // At a caret the next typed text takes one of the two.
        engine.SetSelection(RichTextEditorSelection.Caret(0, 7));
        engine.ToggleMark(RichTextMark.Subscript);
        engine.ToggleMark(RichTextMark.Superscript);
        engine.InsertText("n");
        RichTextEditorHtml.Write(engine.Blocks).ShouldBe("<p>x<sup>2</sup> H2O<sup>n</sup></p>");

        var arc = Engine("<p>x2</p>", docs: false);
        arc.SetSelection(RichTextEditorSelection.Range(0, 1, 2));
        arc.ToggleMark(RichTextMark.Superscript).ShouldBeFalse();
    }

    [Fact]
    public void Scripts_And_Underline_Round_Trip_Through_Html_And_Markdown()
    {
        const string html = "<p>E = mc<sup>2</sup>, CO<sub>2</sub> and <u>under</u></p>";
        var docs = Engine(html);
        RichTextEditorHtml.Write(docs.Blocks).ShouldBe(html);
        var markdown = RichTextEditorMarkdown.Write(docs.Blocks);
        markdown.ShouldBe("E = mc<sup>2</sup>, CO<sub>2</sub> and <u>under</u>");
        RichTextEditorHtml.Write(RichTextEditorMarkdown.ToDocument(markdown, docs: true)).ShouldBe(html);
        // The Arc profile keeps the text and drops the marks.
        RichTextEditorHtml.Write(Engine(html, docs: false).Blocks).ShouldBe("<p>E = mc2, CO2 and under</p>");
        // A sub inside a sup: the inner one wins.
        RichTextEditorHtml.Write(Engine("<p><sup>a<sub>b</sub></sup></p>").Blocks).ShouldBe("<p><sup>a</sup><sub>b</sub></p>");
    }

    [AvaloniaFact]
    public void Scripts_Draw_Smaller_And_Raised_Or_Lowered_And_Smaller_Headings_Get_Smaller()
    {
        var editor = new RichTextEditor
        {
            Width = 480, Profile = RichTextEditorProfile.Docs,
            Html = "<p>x<sup>2</sup> y<sub>i</sub></p><h3>three</h3><h4>four</h4><h5>five</h5><h6>six</h6>"
        };
        var window = TestHelpers.Show(new Border { Padding = new Thickness(20), Child = editor });
        Dispatcher.UIThread.RunJobs();
        try
        {
            var surface = TestHelpers.Descendant<RichTextEditorSurface>(editor);
            var view = surface.ViewForTests(0);
            view.ScriptRuns.ShouldNotBeNull();
            view.ScriptRuns!.Count.ShouldBe(2);
            var (sup, supAt) = view.ScriptRuns[0];
            var (sub, subAt) = view.ScriptRuns[1];
            sup.TextLines[0].Length.ShouldBe(1);
            sub.TextLines[0].Length.ShouldBe(1);
            sup.Height.ShouldBeLessThan(view.Layout!.Height);
            // Raised above, lowered below where text on the line's baseline would sit.
            var line = view.Layout.TextLines[0];
            (supAt.Y + sup.TextLines[0].Baseline).ShouldBeLessThan(line.Baseline - 2);
            (subAt.Y + sub.TextLines[0].Baseline).ShouldBeGreaterThan(line.Baseline + 1);
            // Each sits over the room the main layout keeps for it.
            Math.Abs(supAt.X - view.Layout.HitTestTextPosition(1).X).ShouldBeLessThan(0.5);

            var sizes = Enumerable.Range(1, 4).Select(i => surface.ViewForTests(i).FontSize).ToArray();
            sizes.ShouldBe(sizes.OrderByDescending(s => s).ToArray());
            sizes.Distinct().Count().ShouldBe(4);
        }
        finally
        {
            window.Close();
        }
    }
}
