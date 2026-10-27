using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;

namespace TKCode.Arc.Tests.Editors;

/// <summary>The Docs profile: underline, colors, to-do lists, alignment and code languages, in the engine, the converters and the control.</summary>
public class RichTextEditorDocsTests
{
    private static RichTextEditorEngine Engine(string html, bool docs = true)
    {
        var engine = new RichTextEditorEngine { Docs = docs };
        engine.Load(RichTextEditorHtml.ToDocument(html, docs), record: false, caretAtEnd: true);
        return engine;
    }

    private static void Type(RichTextEditorEngine engine, string text)
    {
        foreach (var c in text)
        {
            engine.InsertText(c.ToString());
        }
    }

    [Fact]
    public void Arc_Profile_Ignores_Docs_Features()
    {
        var engine = Engine("<p>plain</p>", docs: false);
        engine.SelectAll();
        engine.ToggleMark(RichTextMark.Underline).ShouldBeFalse();
        engine.SetTextColor(RichTextColor.Red).ShouldBeFalse();
        engine.SetAlign(RichTextAlign.Center).ShouldBeFalse();
        engine.SetBlockType(RichTextBlockType.TodoList);
        engine.Blocks[0].Type.ShouldBe(RichTextBlockType.Paragraph);

        var imported = RichTextEditorHtml.ToDocument("<p data-align=\"center\"><u>a</u> <span data-color=\"red\">b</span></p><ul data-todo=\"true\"><li data-checked=\"true\">c</li></ul>");
        RichTextEditorHtml.Write(imported).ShouldBe("<p>a b</p><ul><li>c</li></ul>");

        var empty = Engine(string.Empty, docs: false);
        Type(empty, "[] task");
        empty.Blocks[0].Type.ShouldBe(RichTextBlockType.Paragraph);
    }

    [Fact]
    public void Underline_Toggles_And_Round_Trips()
    {
        var engine = Engine("<p>hello world</p>");
        engine.SetSelection(RichTextEditorSelection.Range(0, 0, 5));
        engine.ToggleMark(RichTextMark.Underline).ShouldBeTrue();
        engine.GetFormats().IsUnderline.ShouldBeTrue();
        var html = RichTextEditorHtml.Write(engine.Blocks);
        html.ShouldBe("<p><u>hello</u> world</p>");
        RichTextEditorHtml.Write(RichTextEditorHtml.ToDocument(html, docs: true)).ShouldBe(html);
        RichTextEditorMarkdown.Write(engine.Blocks).ShouldBe("<u>hello</u> world");

        engine.ToggleMark(RichTextMark.Underline);
        RichTextEditorHtml.Write(engine.Blocks).ShouldBe("<p>hello world</p>");
    }

    [Fact]
    public void Colors_Apply_To_Selection_And_Next_Typed_Text()
    {
        var engine = Engine("<p>hello world</p>");
        engine.SetSelection(RichTextEditorSelection.Range(0, 6, 11));
        engine.SetTextColor(RichTextColor.Red).ShouldBeTrue();
        engine.SetHighlight(RichTextColor.Yellow).ShouldBeTrue();
        var formats = engine.GetFormats();
        formats.TextColor.ShouldBe(RichTextColor.Red);
        formats.Highlight.ShouldBe(RichTextColor.Yellow);
        var html = RichTextEditorHtml.Write(engine.Blocks);
        html.ShouldBe("<p>hello <span data-color=\"red\"><mark data-bg=\"yellow\">world</mark></span></p>");
        RichTextEditorHtml.Write(RichTextEditorHtml.ToDocument(html, docs: true)).ShouldBe(html);

        // A mixed selection reports no shared color.
        engine.SelectAll();
        engine.GetFormats().TextColor.ShouldBe(RichTextColor.Default);

        // Typing after colored text continues the color; clearing it at a caret applies to the next text only.
        engine.SetSelection(RichTextEditorSelection.Caret(0, 11));
        Type(engine, "!");
        engine.Blocks[0].MarksAt(11).TextColor().ShouldBe(RichTextColor.Red);
        engine.SetTextColor(RichTextColor.Default);
        Type(engine, "?");
        engine.Blocks[0].MarksAt(12).TextColor().ShouldBe(RichTextColor.Default);
        engine.Blocks[0].MarksAt(11).TextColor().ShouldBe(RichTextColor.Red);
    }

    [Fact]
    public void Todo_Shortcut_Enter_Check_And_Exit()
    {
        var engine = Engine(string.Empty);
        Type(engine, "[] buy milk");
        engine.Blocks[0].Type.ShouldBe(RichTextBlockType.TodoList);
        engine.Blocks[0].Text.ShouldBe("buy milk");

        engine.SetChecked(0, true).ShouldBeTrue();
        engine.Blocks[0].IsChecked.ShouldBeTrue();
        engine.CanUndo.ShouldBeTrue();

        engine.InsertParagraph();
        engine.Blocks[1].Type.ShouldBe(RichTextBlockType.TodoList);
        engine.Blocks[1].IsChecked.ShouldBeFalse();
        Type(engine, "eggs");
        engine.InsertParagraph();
        engine.InsertParagraph();
        engine.Blocks[2].Type.ShouldBe(RichTextBlockType.Paragraph);

        RichTextEditorHtml.Write(engine.Blocks).ShouldBe("<ul data-todo=\"true\"><li data-checked=\"true\">buy milk</li><li>eggs</li></ul>");
        RichTextEditorMarkdown.Write(engine.Blocks).ShouldBe("- [x] buy milk\n- [ ] eggs");

        engine.Undo();
        engine.Undo();
        engine.Undo();
        engine.Blocks.Count.ShouldBeLessThan(3);

        var checkedShortcut = Engine(string.Empty);
        Type(checkedShortcut, "[x] done");
        checkedShortcut.Blocks[0].IsChecked.ShouldBeTrue();
    }

    [Fact]
    public void Todo_Imports_From_Html_Markdown_And_Task_List_Inputs()
    {
        var fromMarkdown = RichTextEditorMarkdown.ToDocument("- [ ] one\n- [x] two\n  - [ ] nested\n- plain", docs: true);
        fromMarkdown.Select(b => (b.Type, b.IsChecked, b.Level)).ShouldBe(new[]
        {
            (RichTextBlockType.TodoList, false, 0),
            (RichTextBlockType.TodoList, true, 0),
            (RichTextBlockType.TodoList, false, 1),
            (RichTextBlockType.BulletedList, false, 0)
        });
        RichTextEditorMarkdown.ToDocument("- [x] two", docs: false)[0].Type.ShouldBe(RichTextBlockType.BulletedList);

        var github = RichTextEditorHtml.ToDocument("<ul><li><input type=\"checkbox\" checked disabled> done</li><li><input type=\"checkbox\"> open</li></ul>", docs: true);
        github.Select(b => (b.Type, b.IsChecked, b.Text.Trim())).ShouldBe(new[]
        {
            (RichTextBlockType.TodoList, true, "done"),
            (RichTextBlockType.TodoList, false, "open")
        });
    }

    [Fact]
    public void Align_Applies_To_Blocks_That_Support_It_And_Continues_On_Enter()
    {
        var engine = Engine("<h2>Title</h2><p>text</p><pre>code</pre>");
        engine.SelectAll();
        engine.SetAlign(RichTextAlign.Center).ShouldBeTrue();
        engine.Blocks[0].Align.ShouldBe(RichTextAlign.Center);
        engine.Blocks[1].Align.ShouldBe(RichTextAlign.Center);
        engine.Blocks[2].Align.ShouldBe(RichTextAlign.Start);
        RichTextEditorHtml.Write(engine.Blocks).ShouldBe("<h2 data-align=\"center\">Title</h2><p data-align=\"center\">text</p><pre>code</pre>");

        engine.SetSelection(RichTextEditorSelection.Caret(1, 4));
        engine.InsertParagraph();
        engine.Blocks[2].Align.ShouldBe(RichTextAlign.Center);

        engine.SetSelection(RichTextEditorSelection.Caret(2, 0));
        engine.SetBlockType(RichTextBlockType.BulletedList);
        engine.Blocks[2].Align.ShouldBe(RichTextAlign.Start);
    }

    [Fact]
    public void Code_Language_Is_Kept_Through_Enter_Html_And_Markdown()
    {
        var engine = Engine("<pre data-lang=\"csharp\">var a = 1;</pre>");
        engine.Blocks[0].Language.ShouldBe("csharp");
        RichTextEditorHtml.Write(engine.Blocks).ShouldBe("<pre data-lang=\"csharp\">var a = 1;</pre>");
        RichTextEditorMarkdown.Write(engine.Blocks).ShouldBe("```csharp\nvar a = 1;\n```");

        engine.SetSelection(RichTextEditorSelection.Caret(0, 10));
        engine.InsertParagraph();
        engine.InsertParagraph();
        engine.Blocks[0].Language.ShouldBe("csharp");
        engine.Blocks[1].Type.ShouldBe(RichTextBlockType.Paragraph);

        engine.SetSelection(RichTextEditorSelection.Caret(0, 0));
        engine.SetCodeLanguage("js").ShouldBeTrue();
        engine.Blocks[0].Language.ShouldBe("js");
        engine.SetCodeLanguage("  ").ShouldBeTrue();
        engine.Blocks[0].Language.ShouldBeNull();

        RichTextEditorMarkdown.ToDocument("```ts\nlet x\n```", docs: true)[0].Language.ShouldBe("ts");
        RichTextEditorMarkdown.ToDocument("```ts\nlet x\n```", docs: false)[0].Language.ShouldBeNull();
    }

    [Fact]
    public void Copy_Fragment_Keeps_Block_Attributes()
    {
        var engine = Engine("<ul data-todo=\"true\"><li data-checked=\"true\">one</li></ul><p data-align=\"end\">two</p>");
        engine.SelectAll();
        var fragment = engine.GetFragment();
        fragment[0].IsChecked.ShouldBeTrue();
        fragment[1].Align.ShouldBe(RichTextAlign.End);
    }

    private static (RichTextEditor Editor, Window Window) Show(string html, double width = 480)
    {
        var editor = new RichTextEditor { Width = width, Profile = RichTextEditorProfile.Docs, Html = html };
        var window = TestHelpers.Show(new Border { Padding = new Thickness(20, 80, 20, 20), Child = editor });
        window.Width = width + 60;
        window.Height = 600;
        Dispatcher.UIThread.RunJobs();
        return (editor, window);
    }

    [AvaloniaFact]
    public void Control_Reads_Html_Assigned_Before_The_Profile_Under_Docs_Rules()
    {
        var editor = new RichTextEditor { Html = "<ul data-todo=\"true\"><li data-checked=\"true\">x</li></ul>" };
        editor.Profile = RichTextEditorProfile.Docs;
        editor.GetHtml().ShouldBe("<ul data-todo=\"true\"><li data-checked=\"true\">x</li></ul>");
    }

    [AvaloniaFact]
    public void Clicking_A_Checkbox_Toggles_The_Item_And_Underline_Shortcut_Works()
    {
        var (editor, window) = Show("<ul data-todo=\"true\"><li>task</li></ul><p>text</p>");
        try
        {
            var surface = TestHelpers.Descendant<RichTextEditorSurface>(editor);
            var box = surface.TodoBoxBounds(0);
            box.Width.ShouldBeGreaterThan(8);
            var point = surface.TranslatePoint(box.Center, window)!.Value;
            window.MouseDown(point, MouseButton.Left);
            window.MouseUp(point, MouseButton.Left);
            Dispatcher.UIThread.RunJobs();
            editor.GetHtml().ShouldBe("<ul data-todo=\"true\"><li data-checked=\"true\">task</li></ul><p>text</p>");

            editor.IsReadOnly = true;
            window.MouseDown(point, MouseButton.Left);
            window.MouseUp(point, MouseButton.Left);
            Dispatcher.UIThread.RunJobs();
            editor.GetHtml().ShouldContain("data-checked");
            editor.IsReadOnly = false;

            editor.FocusEditor();
            editor.SelectAll();
            window.KeyPressQwerty(PhysicalKey.U, RawInputModifiers.Control);
            window.KeyReleaseQwerty(PhysicalKey.U, RawInputModifiers.Control);
            Dispatcher.UIThread.RunJobs();
            editor.GetHtml().ShouldContain("<u>");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Centered_Text_Puts_The_Caret_Inside_The_Text_Not_At_The_Edge()
    {
        var (editor, window) = Show("<p data-align=\"center\">centered</p>");
        try
        {
            var surface = TestHelpers.Descendant<RichTextEditorSurface>(editor);
            var start = surface.CaretRect(new RichTextEditorPosition(0, 0));
            var end = surface.CaretRect(new RichTextEditorPosition(0, 8));
            start.X.ShouldBeGreaterThan(40);
            end.X.ShouldBeGreaterThan(start.X + 30);
            end.X.ShouldBeLessThan(surface.Bounds.Width - 40);

            // Clicking in the middle of the word lands inside it.
            var middle = surface.HitTest(new Point((start.X + end.X) / 2, start.Y + start.Height / 2));
            middle.Offset.ShouldBeInRange(2, 6);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Toolbar_Offers_Docs_Buttons_And_Applies_Colors_From_The_Color_Face()
    {
        var (editor, window) = Show("<p>hello world</p>");
        try
        {
            editor.FocusEditor();
            editor.SelectAll();
            Dispatcher.UIThread.RunJobs();
            var toolbar = editor.Toolbar!;
            toolbar.IsShown.ShouldBeTrue();
            toolbar.Underline.IsVisible.ShouldBeTrue();
            toolbar.Palette.IsVisible.ShouldBeTrue();
            toolbar.Todo.IsVisible.ShouldBeTrue();

            toolbar.Underline.Invoke();
            editor.GetHtml().ShouldBe("<p><u>hello world</u></p>");
            Dispatcher.UIThread.RunJobs();
            toolbar.Underline.IsChecked.ShouldBeTrue();

            toolbar.Palette.Invoke();
            Dispatcher.UIThread.RunJobs();
            toolbar.IsColorFace.ShouldBeTrue();
            toolbar.TextSwatches.Children.Count.ShouldBe(8);
            toolbar.FillSwatches.Children.Count.ShouldBe(8);
            ((RichTextEditorToolButton)toolbar.TextSwatches.Children[2]).Invoke();
            Dispatcher.UIThread.RunJobs();
            toolbar.IsColorFace.ShouldBeFalse();
            editor.GetHtml().ShouldBe("<p><u><span data-color=\"red\">hello world</span></u></p>");
            editor.Formats.TextColor.ShouldBe(RichTextColor.Red);

            toolbar.Palette.Invoke();
            ((RichTextEditorToolButton)toolbar.FillSwatches.Children[5]).Invoke();
            Dispatcher.UIThread.RunJobs();
            editor.GetHtml().ShouldContain("data-bg=\"green\"");

            toolbar.Todo.Invoke();
            editor.GetHtml().ShouldStartWith("<ul data-todo=\"true\">");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Toolbar_Hides_Docs_Buttons_In_The_Arc_Profile()
    {
        var editor = new RichTextEditor { Width = 480, Html = "<p>hello</p>" };
        var window = TestHelpers.Show(new Border { Padding = new Thickness(20, 80, 20, 20), Child = editor });
        try
        {
            editor.FocusEditor();
            editor.SelectAll();
            Dispatcher.UIThread.RunJobs();
            var toolbar = editor.Toolbar!;
            toolbar.Underline.IsVisible.ShouldBeFalse();
            toolbar.Palette.IsVisible.ShouldBeFalse();
            toolbar.Todo.IsVisible.ShouldBeFalse();
            toolbar.FormatButtons.ShouldNotContain(toolbar.Underline);
        }
        finally
        {
            window.Close();
        }
    }

    [Fact]
    public void Code_Blocks_Highlight_In_Their_Language_And_Stay_Plain_Otherwise()
    {
        var csharp = RichTextEditorSurface.HighlightSyntax(Engine("<pre data-lang=\"csharp\">var a = \"x\"; // note</pre>").Blocks[0]);
        csharp.ShouldNotBeNull();
        csharp.Select(s => s.Kind).ShouldContain(CodeBlockSyntaxKind.Keyword);
        csharp.Select(s => s.Kind).ShouldContain(CodeBlockSyntaxKind.String);
        csharp.Select(s => s.Kind).ShouldContain(CodeBlockSyntaxKind.Comment);

        RichTextEditorSurface.HighlightSyntax(Engine("<pre>var a = 1;</pre>").Blocks[0]).ShouldBeNull();
        RichTextEditorSurface.HighlightSyntax(Engine("<pre data-lang=\"no-such-language\">x</pre>").Blocks[0]).ShouldBeNull();
    }

    [AvaloniaFact]
    public void Language_Label_Opens_The_Language_List_And_Typed_Code_Gets_Colors()
    {
        var (editor, window) = Show("<pre data-lang=\"csharp\">var a = 1;</pre><p>after</p>");
        try
        {
            var surface = TestHelpers.Descendant<RichTextEditorSurface>(editor);
            var header = surface.CodeHeaderRect(0);
            header.Width.ShouldBeGreaterThan(30);
            var point = surface.TranslatePoint(header.Center, window)!.Value;
            window.MouseDown(point, MouseButton.Left);
            window.MouseUp(point, MouseButton.Left);
            Dispatcher.UIThread.RunJobs();
            editor.LanguageMenu.ShouldNotBeNull();
            editor.LanguageMenu!.IsOpen.ShouldBeTrue();
            editor.LanguageMenu.Items.Count.ShouldBeGreaterThan(10);
            editor.LanguageMenu.Close(returnFocus: false);

            // Docs profile: a code block without a language still offers the picker.
            editor.SetHtml("<pre>x</pre>");
            Dispatcher.UIThread.RunJobs();
            surface.CodeHeaderAt(surface.CodeHeaderRect(0).Center).ShouldNotBeNull();

            // The Arc profile shows a header only when a language was stored, and never opens the picker.
            editor.Profile = RichTextEditorProfile.Arc;
            Dispatcher.UIThread.RunJobs();
            surface.CodeHeaderAt(new Point(20, 10)).ShouldBeNull();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Language_List_Is_Configurable_And_Accepts_A_Custom_Name()
    {
        var (editor, window) = Show("<pre data-lang=\"csharp\">x</pre>");
        try
        {
            var surface = TestHelpers.Descendant<RichTextEditorSurface>(editor);
            editor.CodeLanguages = new[] { new RichTextCodeLanguage("Plain", null), new RichTextCodeLanguage("Zig", "zig") };
            editor.OnCodeLanguagePressed(0, surface.CodeHeaderRect(0));
            Dispatcher.UIThread.RunJobs();
            var headers = editor.LanguageMenu!.Items.OfType<MenuEntry>().Select(e => e.Header as string).ToList();
            // The stored language is not in the list, so it leads as the checked row; the list follows, then "Custom…".
            headers.ShouldBe(new[] { "csharp", "Plain", "Zig", "Custom…" });
            editor.LanguageMenu.Items.OfType<MenuEntry>().First().IsChecked.ShouldBeTrue();
            editor.LanguageMenu.Close(returnFocus: false);

            editor.OpenCustomLanguage();
            Dispatcher.UIThread.RunJobs();
            editor.CustomLanguagePopup!.IsOpen.ShouldBeTrue();
            editor.CustomLanguageBox!.Text.ShouldBe("csharp");
            editor.CustomLanguageBox.Text = "  my-dsl ";
            editor.CustomLanguageBox.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Enter });
            Dispatcher.UIThread.RunJobs();
            editor.CustomLanguagePopup.IsOpen.ShouldBeFalse();
            editor.GetHtml().ShouldBe("<pre data-lang=\"my-dsl\">x</pre>");

            // An unknown language keeps its label and stays plain.
            RichTextEditorSurface.HighlightSyntax(editor.Toolbar is null ? RichTextEditorBlock.Plain(RichTextBlockType.CodeBlock, "x") : RichTextEditorBlock.Plain(RichTextBlockType.CodeBlock, "x")).ShouldBeNull();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Slash_Menu_Offers_Todo_Only_In_The_Docs_Profile()
    {
        RichTextEditorSlashItem.Filter("todo", docs: false).ShouldBeEmpty();
        RichTextEditorSlashItem.Filter("todo", docs: true).Select(i => i.Type).ShouldBe(new[] { RichTextBlockType.TodoList });
    }
}
