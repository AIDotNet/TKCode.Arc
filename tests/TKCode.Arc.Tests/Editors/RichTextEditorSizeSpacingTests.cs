using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;

namespace TKCode.Arc.Tests.Editors;

/// <summary>Text sizes and line spacing of the Docs profile: the engine, HTML, Markdown and the layout.</summary>
public class RichTextEditorSizeSpacingTests
{
    private static RichTextEditorEngine Engine(string html, bool docs = true)
    {
        var engine = new RichTextEditorEngine { Docs = docs };
        engine.Load(RichTextEditorHtml.ToDocument(html, docs), record: false, caretAtEnd: true);
        return engine;
    }

    [Fact]
    public void Sizes_Apply_To_The_Selection_And_Round_Trip_Through_Html()
    {
        var engine = Engine("<p>small big huge</p>");
        engine.SetSelection(RichTextEditorSelection.Range(0, 0, 5));
        engine.SetSize(RichTextSize.Small).ShouldBeTrue();
        engine.SetSelection(RichTextEditorSelection.Range(0, 6, 9));
        engine.SetSize(RichTextSize.Large);
        engine.SetSelection(RichTextEditorSelection.Range(0, 10, 14));
        engine.SetSize(RichTextSize.ExtraLarge);
        engine.GetFormats().Size.ShouldBe(RichTextSize.ExtraLarge);
        var html = RichTextEditorHtml.Write(engine.Blocks);
        html.ShouldBe("<p><span data-size=\"small\">small</span> <span data-size=\"large\">big</span> <span data-size=\"xlarge\">huge</span></p>");
        RichTextEditorHtml.Write(Engine(html).Blocks).ShouldBe(html);
        // Mixed sizes report the default; Markdown and the Arc profile keep the text only.
        engine.SetSelection(RichTextEditorSelection.Range(0, 0, 14));
        engine.GetFormats().Size.ShouldBe(RichTextSize.Default);
        RichTextEditorMarkdown.Write(engine.Blocks).ShouldBe("small big huge");
        RichTextEditorHtml.Write(Engine(html, docs: false).Blocks).ShouldBe("<p>small big huge</p>");
        // Sizes combine with the other marks.
        engine.SetSelection(RichTextEditorSelection.Range(0, 6, 9));
        engine.ToggleMark(RichTextMark.Bold);
        RichTextEditorHtml.Write(engine.Blocks).ShouldContain("<strong><span data-size=\"large\">big</span></strong>");
        engine.SetSize(RichTextSize.Default);
        RichTextEditorHtml.Write(engine.Blocks).ShouldContain("<strong>big</strong>");
    }

    [Fact]
    public void A_Size_At_The_Caret_Carries_Into_Typed_Text_And_Only_Docs_Sizes()
    {
        var engine = Engine("<p>a</p>");
        engine.SetSelection(RichTextEditorSelection.Caret(0, 1));
        engine.SetSize(RichTextSize.Large).ShouldBeTrue();
        engine.GetFormats().Size.ShouldBe(RichTextSize.Large);
        engine.InsertText("bc");
        RichTextEditorHtml.Write(engine.Blocks).ShouldBe("<p>a<span data-size=\"large\">bc</span></p>");

        var arc = Engine("<p>a</p>", docs: false);
        arc.SetSelection(RichTextEditorSelection.Range(0, 0, 1));
        arc.SetSize(RichTextSize.Large).ShouldBeFalse();
        arc.SetLineSpacing(RichTextLineSpacing.Loose).ShouldBeFalse();
    }

    [Fact]
    public void Line_Spacing_Applies_To_Text_Blocks_And_Round_Trips_Through_Html()
    {
        var engine = Engine("<h2>Title</h2><p>Body</p><ul><li>Item</li></ul><blockquote>Quote</blockquote><pre>code</pre><aside data-callout=\"blue\" data-icon=\"💡\"><p>Note</p></aside>");
        engine.SetSelection(new RichTextEditorSelection(new RichTextEditorPosition(0, 0), new RichTextEditorPosition(5, 4)));
        engine.SetLineSpacing(RichTextLineSpacing.Relaxed).ShouldBeTrue();
        engine.Blocks.Take(6).Select(b => b.Spacing).ShouldBe(new[]
        {
            RichTextLineSpacing.Relaxed, RichTextLineSpacing.Relaxed, RichTextLineSpacing.Relaxed, RichTextLineSpacing.Relaxed,
            RichTextLineSpacing.Default, RichTextLineSpacing.Relaxed
        });
        engine.GetFormats().LineSpacing.ShouldBe(RichTextLineSpacing.Relaxed);
        var html = RichTextEditorHtml.Write(engine.Blocks);
        html.ShouldBe("<h2 data-spacing=\"relaxed\">Title</h2><p data-spacing=\"relaxed\">Body</p><ul><li data-spacing=\"relaxed\">Item</li></ul>"
            + "<blockquote data-spacing=\"relaxed\">Quote</blockquote><pre>code</pre><aside data-callout=\"blue\" data-icon=\"💡\"><p data-spacing=\"relaxed\">Note</p></aside>");
        RichTextEditorHtml.Write(Engine(html).Blocks).ShouldBe(html);
        RichTextEditorHtml.Write(Engine(html, docs: false).Blocks).ShouldNotContain("data-spacing");
        // Enter keeps the spacing for the next block, and the block menu sets a whole unit.
        engine.SetSelection(RichTextEditorSelection.Caret(1, 4));
        engine.InsertParagraph();
        engine.Blocks[2].Spacing.ShouldBe(RichTextLineSpacing.Relaxed);
        engine.SetLineSpacingAt(2, RichTextLineSpacing.Compact).ShouldBeTrue();
        engine.Blocks[2].Spacing.ShouldBe(RichTextLineSpacing.Compact);
    }

    [AvaloniaFact]
    public void Lines_Grow_With_Their_Largest_Text_And_Their_Spacing()
    {
        var editor = new RichTextEditor
        {
            Width = 480, Profile = RichTextEditorProfile.Docs,
            Html = "<p>plain</p><p>big <span data-size=\"xlarge\">text</span></p><p data-spacing=\"loose\">loose</p><p data-spacing=\"compact\">compact</p><pre data-spacing=\"loose\">code</pre>"
        };
        var window = TestHelpers.Show(new Border { Padding = new Thickness(20), Child = editor });
        Dispatcher.UIThread.RunJobs();
        try
        {
            var surface = TestHelpers.Descendant<RichTextEditorSurface>(editor);
            var plain = surface.ViewForTests(0).LineHeight;
            surface.ViewForTests(1).LineHeight.ShouldBe(plain * 1.5, 0.01);
            surface.ViewForTests(2).LineHeight.ShouldBe(plain * 1.45, 0.01);
            surface.ViewForTests(3).LineHeight.ShouldBe(plain * 0.85, 0.01);
            surface.BlockHeightForTests(1).ShouldBeGreaterThan(surface.BlockHeightForTests(0));
            // A code block keeps its own spacing.
            editor.Engine.Blocks[4].Spacing.ShouldBe(RichTextLineSpacing.Default);

            editor.Engine.SetSelection(RichTextEditorSelection.Range(0, 0, 5));
            editor.SetTextSize(RichTextSize.Large);
            Dispatcher.UIThread.RunJobs();
            surface.ViewForTests(0).LineHeight.ShouldBe(plain * 1.25, 0.01);
        }
        finally
        {
            window.Close();
        }
    }
}
