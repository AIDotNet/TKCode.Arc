using System.Collections.Immutable;

namespace TKCode.Arc.Tests.Editors;

/// <summary>The rich text editing core: Arc's Markdown shortcuts, block edges, lists, marks, links, paste and history.</summary>
public class RichTextEditorEngineTests
{
    private static RichTextEditorEngine Engine(string markdown, long[]? clock = null)
    {
        var engine = new RichTextEditorEngine();
        if (clock is not null)
        {
            engine.Clock = () => clock[0];
        }
        engine.Load(RichTextEditorMarkdown.ToDocument(markdown), record: false, caretAtEnd: true);
        return engine;
    }

    private static string Md(RichTextEditorEngine engine) => RichTextEditorMarkdown.Write(engine.Blocks);

    private static void Type(RichTextEditorEngine engine, string text)
    {
        foreach (var c in text)
        {
            engine.InsertText(c.ToString());
        }
    }

    [Fact]
    public void Block_Shortcuts_Convert_The_Paragraph()
    {
        var engine = Engine(string.Empty);
        Type(engine, "# Title");
        engine.Blocks[0].Type.ShouldBe(RichTextBlockType.Heading1);
        engine.Blocks[0].Text.ShouldBe("Title");

        engine = Engine(string.Empty);
        Type(engine, "- one");
        engine.Blocks[0].Type.ShouldBe(RichTextBlockType.BulletedList);

        engine = Engine(string.Empty);
        Type(engine, "3) step");
        engine.Blocks[0].Type.ShouldBe(RichTextBlockType.NumberedList);

        engine = Engine(string.Empty);
        Type(engine, "> said");
        engine.Blocks[0].Type.ShouldBe(RichTextBlockType.Quote);
        Md(engine).ShouldBe("> said");
    }

    [Fact]
    public void Rule_And_Fence_Shortcuts()
    {
        var engine = Engine(string.Empty);
        Type(engine, "---");
        engine.Blocks[0].Type.ShouldBe(RichTextBlockType.Divider);
        engine.Blocks[1].Type.ShouldBe(RichTextBlockType.Paragraph);
        engine.Selection.Focus.ShouldBe(new RichTextEditorPosition(1, 0));

        engine = Engine(string.Empty);
        Type(engine, "```");
        engine.Blocks[0].Type.ShouldBe(RichTextBlockType.CodeBlock);
        engine.Blocks[0].Text.ShouldBe(string.Empty);
    }

    [Fact]
    public void Inline_Shortcuts_Mark_And_Exit_The_Mark()
    {
        var engine = Engine(string.Empty);
        Type(engine, "a **bold** b");
        Md(engine).ShouldBe("a **bold** b");
        engine.Blocks[0].Text.ShouldBe("a bold b");

        engine = Engine(string.Empty);
        Type(engine, "x `code` y _em_ ~~gone~~");
        Md(engine).ShouldBe("x `code` y _em_ ~~gone~~");
        engine.Blocks[0].MarksAt(engine.Blocks[0].Text.IndexOf('y')).ShouldBe(RichTextEditorMarks.None);
    }

    [Fact]
    public void Shortcuts_Do_Not_Run_In_Code_Blocks()
    {
        var engine = Engine("```\n\n```");
        engine.SetSelection(RichTextEditorSelection.Caret(0, 0));
        Type(engine, "# **x** ");
        engine.Blocks[0].Type.ShouldBe(RichTextBlockType.CodeBlock);
        engine.Blocks[0].Text.ShouldBe("# **x** ");
    }

    [Fact]
    public void Enter_Splits_Exits_Lists_And_Headings_Continue_As_Paragraphs()
    {
        var engine = Engine("# Head");
        engine.InsertParagraph();
        engine.Blocks[1].Type.ShouldBe(RichTextBlockType.Paragraph);

        engine = Engine("- one");
        engine.InsertParagraph();
        engine.Blocks[1].Type.ShouldBe(RichTextBlockType.BulletedList);
        engine.InsertParagraph();
        engine.Blocks.Count.ShouldBe(2);
        engine.Blocks[1].Type.ShouldBe(RichTextBlockType.Paragraph);

        engine = Engine("> q");
        engine.InsertParagraph();
        engine.Blocks[1].Type.ShouldBe(RichTextBlockType.Quote);
        engine.InsertParagraph();
        engine.Blocks[1].Type.ShouldBe(RichTextBlockType.Paragraph);
    }

    [Fact]
    public void Code_Block_Exits_On_Second_Enter_At_The_End()
    {
        var engine = Engine("```\nlet a\n```");
        engine.InsertParagraph();
        engine.Blocks[0].Text.ShouldBe("let a\n");
        engine.InsertParagraph();
        engine.Blocks[0].Text.ShouldBe("let a");
        engine.Blocks[1].Type.ShouldBe(RichTextBlockType.Paragraph);
        engine.Selection.Focus.ShouldBe(new RichTextEditorPosition(1, 0));
    }

    [Fact]
    public void Backspace_At_Block_Start_Follows_Arc()
    {
        var engine = Engine("## Head");
        engine.SetSelection(RichTextEditorSelection.Caret(0, 0));
        engine.DeleteBackward();
        engine.Blocks[0].Type.ShouldBe(RichTextBlockType.Paragraph);

        engine = Engine("- a\n  - b");
        engine.SetSelection(RichTextEditorSelection.Caret(1, 0));
        engine.DeleteBackward();
        engine.Blocks[1].Level.ShouldBe(0);
        engine.DeleteBackward();
        engine.Blocks[1].Type.ShouldBe(RichTextBlockType.Paragraph);

        engine = Engine("one\n\n---\n\ntwo");
        engine.SetSelection(RichTextEditorSelection.Caret(2, 0));
        engine.DeleteBackward();
        engine.Blocks.Select(b => b.Type).ShouldBe(new[] { RichTextBlockType.Paragraph, RichTextBlockType.Paragraph });

        engine.SetSelection(RichTextEditorSelection.Caret(1, 0));
        engine.DeleteBackward();
        engine.Blocks.Count.ShouldBe(1);
        engine.Blocks[0].Text.ShouldBe("onetwo");
        engine.Selection.Focus.ShouldBe(new RichTextEditorPosition(0, 3));
    }

    [Fact]
    public void Tab_Indents_Under_A_Sibling_And_Shift_Tab_Outdents()
    {
        var engine = Engine("1. a\n2. b\n3. c");
        engine.SetSelection(RichTextEditorSelection.Caret(0, 0));
        engine.IndentListItem().ShouldBeTrue();
        engine.Blocks[0].Level.ShouldBe(0);

        engine.SetSelection(RichTextEditorSelection.Caret(1, 0));
        engine.IndentListItem();
        engine.Blocks[1].Level.ShouldBe(1);
        Md(engine).ShouldBe("1. a\n   1. b\n2. c");

        engine.OutdentListItem();
        engine.Blocks[1].Level.ShouldBe(0);
        Md(engine).ShouldBe("1. a\n2. b\n3. c");
    }

    [Fact]
    public void Toggle_Mark_Uses_Exec_Command_Semantics()
    {
        var engine = Engine("hello world");
        engine.SetSelection(RichTextEditorSelection.Range(0, 0, 5));
        engine.ToggleMark(RichTextMark.Bold);
        Md(engine).ShouldBe("**hello** world");
        engine.GetFormats().IsBold.ShouldBeTrue();

        engine.SetSelection(RichTextEditorSelection.Range(0, 0, 11));
        engine.GetFormats().IsBold.ShouldBeFalse();
        engine.ToggleMark(RichTextMark.Bold);
        Md(engine).ShouldBe("**hello world**");
        engine.ToggleMark(RichTextMark.Bold);
        Md(engine).ShouldBe("hello world");
    }

    [Fact]
    public void Toggle_At_A_Caret_Applies_To_The_Next_Typed_Text()
    {
        var engine = Engine("a");
        engine.ToggleMark(RichTextMark.Italic);
        engine.GetFormats().IsItalic.ShouldBeTrue();
        Type(engine, "b");
        Md(engine).ShouldBe("a_b_");
    }

    [Fact]
    public void Inline_Code_Toggle_Wraps_And_Unwraps()
    {
        var engine = Engine("call run now");
        engine.SetSelection(RichTextEditorSelection.Range(0, 5, 8));
        engine.ToggleMark(RichTextMark.Code);
        Md(engine).ShouldBe("call `run` now");
        engine.SetSelection(RichTextEditorSelection.Caret(0, 6));
        engine.ToggleMark(RichTextMark.Code);
        Md(engine).ShouldBe("call run now");
        engine.Selection.ShouldBe(RichTextEditorSelection.Range(0, 5, 8));
    }

    [Fact]
    public void Links_Apply_Edit_And_Remove()
    {
        var engine = Engine("see the docs");
        engine.SetSelection(RichTextEditorSelection.Range(0, 8, 12));
        var range = engine.LinkRange(out var href);
        href.ShouldBeNull();
        engine.SetLink(range!.Value, RichTextEditorMarkdown.NormalizeUrl("example.com/docs"));
        Md(engine).ShouldBe("see the [docs](https://example.com/docs)");

        engine.SetSelection(RichTextEditorSelection.Caret(0, 10));
        range = engine.LinkRange(out href);
        href.ShouldBe("https://example.com/docs");
        range.ShouldBe(RichTextEditorSelection.Range(0, 8, 12));
        engine.SetLink(range!.Value, null);
        Md(engine).ShouldBe("see the docs");
    }

    [Fact]
    public void Block_Type_Toggles_Back_To_Paragraph()
    {
        var engine = Engine("text");
        engine.SetBlockType(RichTextBlockType.Heading2);
        engine.Blocks[0].Type.ShouldBe(RichTextBlockType.Heading2);
        engine.SetBlockType(RichTextBlockType.Heading2);
        engine.Blocks[0].Type.ShouldBe(RichTextBlockType.Paragraph);

        engine.SetBlockType(RichTextBlockType.CodeBlock);
        engine.Blocks[0].Type.ShouldBe(RichTextBlockType.CodeBlock);
        engine.Blocks.Count.ShouldBe(2);
    }

    [Fact]
    public void Slash_Choice_Replaces_The_Query()
    {
        var engine = Engine(string.Empty);
        engine.InsertText("/").SlashTyped.ShouldBeTrue();
        Type(engine, "quo");
        engine.ApplySlashChoice(0, 0, engine.Selection.Focus.Offset, RichTextBlockType.Quote);
        engine.Blocks[0].Type.ShouldBe(RichTextBlockType.Quote);
        engine.Blocks[0].Text.ShouldBe(string.Empty);

        engine = Engine("abc");
        engine.InsertText("/").SlashTyped.ShouldBeFalse();
    }

    [Fact]
    public void Divider_From_Menu_Goes_In_Place_Of_An_Empty_Block()
    {
        var engine = Engine(string.Empty);
        engine.SetBlockType(RichTextBlockType.Divider);
        engine.Blocks.Select(b => b.Type).ShouldBe(new[] { RichTextBlockType.Divider, RichTextBlockType.Paragraph });
        engine.Selection.Focus.ShouldBe(new RichTextEditorPosition(1, 0));
    }

    [Fact]
    public void Paste_Plain_Inline_And_Multi_Block()
    {
        var engine = Engine("ab");
        engine.SetSelection(RichTextEditorSelection.Caret(0, 1));
        engine.Paste("x\ny", null);
        engine.Blocks[0].Text.ShouldBe("ax yb");

        engine = Engine("ab");
        engine.SetSelection(RichTextEditorSelection.Caret(0, 1));
        engine.Paste(string.Empty, RichTextMarkdown("**X**"));
        Md(engine).ShouldBe("a**X**b");

        engine = Engine("ab");
        engine.SetSelection(RichTextEditorSelection.Caret(0, 1));
        engine.Paste(string.Empty, RichTextMarkdown("# H\n\n- i"));
        Md(engine).ShouldBe("a\n\n# H\n\n- i\n\nb");
    }

    private static IReadOnlyList<RichTextEditorBlock> RichTextMarkdown(string markdown) => RichTextEditorMarkdown.ToDocument(markdown);

    [Fact]
    public void Typing_Coalesces_Within_A_Second_And_Splits_On_Space()
    {
        var clock = new long[] { 1000 };
        var engine = Engine(string.Empty, clock);
        Type(engine, "hello");
        clock[0] += 100;
        Type(engine, " world");
        engine.Undo();
        engine.Blocks[0].Text.ShouldBe("hello");
        engine.Undo();
        engine.Blocks[0].Text.ShouldBe(string.Empty);
        engine.CanUndo.ShouldBeFalse();
        engine.Redo();
        engine.Redo();
        engine.Blocks[0].Text.ShouldBe("hello world");

        clock[0] += 5000;
        Type(engine, "!");
        clock[0] += 2000;
        Type(engine, "?");
        engine.Undo();
        engine.Blocks[0].Text.ShouldBe("hello world!");
    }

    [Fact]
    public void Undo_Of_A_Shortcut_Restores_The_Typed_Text()
    {
        var engine = Engine(string.Empty);
        Type(engine, "**b**");
        Md(engine).ShouldBe("**b**");
        engine.Blocks[0].Text.ShouldBe("b");
        engine.Undo();
        engine.Blocks[0].Text.ShouldBe("**b*");
    }

    [Fact]
    public void Deleting_Across_Blocks_Keeps_The_First_Type()
    {
        var engine = Engine("# Title\n\nbody text");
        engine.SetSelection(new RichTextEditorSelection(new RichTextEditorPosition(0, 2), new RichTextEditorPosition(1, 5)));
        engine.DeleteBackward();
        engine.Blocks.Count.ShouldBe(1);
        engine.Blocks[0].Type.ShouldBe(RichTextBlockType.Heading1);
        engine.Blocks[0].Text.ShouldBe("Titext");
    }

    [Fact]
    public void Select_All_Then_Type_Replaces_Everything()
    {
        var engine = Engine("---\n\none\n\n- two");
        engine.SelectAll();
        engine.InsertText("x");
        engine.Blocks.Count.ShouldBe(1);
        engine.Blocks[0].Text.ShouldBe("x");
    }

    [Fact]
    public void Word_Deletion_And_Graphemes()
    {
        var engine = Engine("hello brave world");
        engine.DeleteBackward(word: true);
        engine.Blocks[0].Text.ShouldBe("hello brave ");
        engine.DeleteBackward(word: true);
        engine.Blocks[0].Text.ShouldBe("hello ");

        engine = Engine("a👍🏽");
        engine.DeleteBackward();
        engine.Blocks[0].Text.ShouldBe("a");
    }
}

/// <summary>Arc's converters: Markdown in and out, HTML sanitizing and link normalizing.</summary>
public class RichTextEditorMarkdownTests
{
    [Fact]
    public void Markdown_Round_Trips_The_Supported_Subset()
    {
        const string markdown = "# Title\n\nSome **bold**, _italic_, ~~gone~~, `code` and [a link](https://uiarc.dev).\n\n- one\n  - nested\n- two\n\n1. first\n2. second\n\n> quoted\n\n```\nlet x = 1;\n```\n\n---\n\nEnd";
        var html = RichTextEditorMarkdown.ToHtml(markdown);
        RichTextEditorMarkdown.FromHtml(html).ShouldBe(markdown);
    }

    [Fact]
    public void Markdown_To_Html_Matches_Arc()
    {
        RichTextEditorMarkdown.ToHtml("#### Deep").ShouldBe("<h3>Deep</h3>");
        RichTextEditorMarkdown.ToHtml("a\nb").ShouldBe("<p>a b</p>");
        RichTextEditorMarkdown.ToHtml("- a\n  1. b").ShouldBe("<ul><li>a<ol><li>b</li></ol></li></ul>");
        RichTextEditorMarkdown.ToHtml("> a\n> b").ShouldBe("<blockquote>a<br>b</blockquote>");
        RichTextEditorMarkdown.ToHtml("[x](javascript:alert)").ShouldBe("<p>x</p>");
        RichTextEditorMarkdown.ToHtml(string.Empty).ShouldBe("<p><br></p>");
    }

    [Fact]
    public void Sanitize_Keeps_Only_Editor_Tags()
    {
        RichTextEditorMarkdown.SanitizeHtml("<p style=\"color:red\">Hi <b>there</b><script>x</script></p>")
            .ShouldBe("<p>Hi <strong>there</strong></p>");
        RichTextEditorMarkdown.SanitizeHtml("<span style=\"font-weight:700\">bold</span> <a href=\"javascript:x\">bad</a>")
            .ShouldBe("<p><strong>bold</strong> bad</p>");
        RichTextEditorMarkdown.SanitizeHtml("<b style=\"font-weight:normal\"><p>plain</p></b>").ShouldBe("<p>plain</p>");
        RichTextEditorMarkdown.SanitizeHtml("<h5>x</h5><ul>\n  <li><p>a</p></li>\n</ul>").ShouldBe("<h3>x</h3><ul><li>a</li></ul>");
        RichTextEditorMarkdown.SanitizeHtml("<pre>\nline 1\nline 2\n</pre>").ShouldBe("<pre>line 1\nline 2</pre>");
        RichTextEditorMarkdown.SanitizeHtml("a &amp; b &lt;c&gt;").ShouldBe("<p>a &amp; b &lt;c&gt;</p>");
    }

    [Fact]
    public void Html_Output_Drops_Trailing_Empty_Blocks()
    {
        var blocks = ImmutableList.Create(RichTextEditorBlock.Plain(RichTextBlockType.Paragraph, "a"), RichTextEditorBlock.EmptyParagraph, RichTextEditorBlock.EmptyParagraph);
        RichTextEditorHtml.Write(blocks).ShouldBe("<p>a</p>");
        RichTextEditorHtml.Write(ImmutableList.Create(RichTextEditorBlock.EmptyParagraph)).ShouldBe("<p><br></p>");
    }

    [Fact]
    public void Soft_Breaks_Round_Trip_Through_Html()
    {
        var html = RichTextEditorHtml.Write(ImmutableList.Create(RichTextEditorBlock.Plain(RichTextBlockType.Paragraph, "a\nb\n")));
        html.ShouldBe("<p>a<br>b<br><br></p>");
        RichTextEditorHtml.ToDocument(html)[0].Text.ShouldBe("a\nb\n");
    }

    [Fact]
    public void Normalize_Url_Adds_Schemes()
    {
        RichTextEditorMarkdown.NormalizeUrl(" example.com/docs ").ShouldBe("https://example.com/docs");
        RichTextEditorMarkdown.NormalizeUrl("me@example.com").ShouldBe("mailto:me@example.com");
        RichTextEditorMarkdown.NormalizeUrl("/path").ShouldBe("/path");
        RichTextEditorMarkdown.NormalizeUrl("ftp://x").ShouldBe("ftp://x");
        RichTextEditorMarkdown.NormalizeUrl("  ").ShouldBe(string.Empty);
    }
}
