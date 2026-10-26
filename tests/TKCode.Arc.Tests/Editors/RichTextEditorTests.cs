using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Threading;

namespace TKCode.Arc.Tests.Editors;

public class RichTextEditorTests
{
    private static (RichTextEditor Editor, Window Window) Show(string? markdown = null, double width = 480)
    {
        var editor = new RichTextEditor { Width = width };
        if (markdown is not null)
        {
            editor.Markdown = markdown;
        }
        var window = TestHelpers.Show(new Border { Padding = new Thickness(20, 80, 20, 20), Child = editor });
        window.Width = width + 60;
        window.Height = 600;
        Dispatcher.UIThread.RunJobs();
        return (editor, window);
    }

    private static RichTextEditorSurface Surface(RichTextEditor editor) => TestHelpers.Descendant<RichTextEditorSurface>(editor);

    private static void Focus(RichTextEditor editor)
    {
        editor.FocusEditor();
        Dispatcher.UIThread.RunJobs();
    }

    private static void Key(Window window, PhysicalKey key, RawInputModifiers modifiers = RawInputModifiers.None)
    {
        window.KeyPressQwerty(key, modifiers);
        window.KeyReleaseQwerty(key, modifiers);
        Dispatcher.UIThread.RunJobs();
    }

    private static void Type(Window window, string text)
    {
        foreach (var c in text)
        {
            window.KeyTextInput(c.ToString());
        }
        Dispatcher.UIThread.RunJobs();
    }

    [AvaloniaFact]
    public void Defaults_Match_Arc()
    {
        var (editor, window) = Show();
        try
        {
            editor.Placeholder.ShouldBe("Start writing");
            editor.BlockHint.ShouldBe("Type / for blocks");
            editor.IsDocumentEmpty.ShouldBeTrue();
            editor.GetHtml().ShouldBe("<p><br></p>");
            Surface(editor).Bounds.Height.ShouldBeGreaterThanOrEqualTo(128);
            Avalonia.Automation.AutomationProperties.GetName(editor).ShouldBe("Editor");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Typing_With_Shortcuts_Builds_Blocks_And_Updates_Html()
    {
        var (editor, window) = Show();
        try
        {
            Focus(editor);
            Type(window, "# Notes");
            Key(window, PhysicalKey.Enter);
            Type(window, "- first **bold**");
            Key(window, PhysicalKey.Enter);
            Type(window, "second");
            Dispatcher.UIThread.RunJobs();
            editor.Markdown.ShouldBe("# Notes\n\n- first **bold**\n- second");
            editor.Html.ShouldBe("<h1>Notes</h1><ul><li>first <strong>bold</strong></li><li>second</li></ul>");
            editor.IsDocumentEmpty.ShouldBeFalse();
            editor.CanUndo.ShouldBeTrue();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Undo_And_Redo_Through_Shortcuts_And_Announcements()
    {
        var (editor, window) = Show();
        try
        {
            Focus(editor);
            Type(window, "hello");
            Key(window, PhysicalKey.Z, RawInputModifiers.Control);
            editor.GetText().ShouldBe(string.Empty);
            editor.LiveRegion!.Message.ShouldBe("Undone");
            Key(window, PhysicalKey.Z, RawInputModifiers.Control | RawInputModifiers.Shift);
            editor.GetText().ShouldBe("hello");
            editor.LiveRegion!.Message.ShouldBe("Redone");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Html_Property_Loads_Sanitized_Content_And_Ignores_Its_Own_Echo()
    {
        var (editor, window) = Show();
        try
        {
            var changes = 0;
            editor.ContentChanged += (_, _) => changes++;
            editor.Html = "<p>Hi <b>there</b><script>x</script></p>";
            Dispatcher.UIThread.RunJobs();
            editor.GetHtml().ShouldBe("<p>Hi <strong>there</strong></p>");
            editor.Html.ShouldBe("<p>Hi <strong>there</strong></p>");
            changes.ShouldBe(1);
            editor.CanUndo.ShouldBeTrue();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Marks_Toggle_With_Mod_Keys_And_Formats_Follow()
    {
        var (editor, window) = Show("hello world");
        try
        {
            Focus(editor);
            editor.SelectAll();
            Key(window, PhysicalKey.B, RawInputModifiers.Control);
            editor.Formats.IsBold.ShouldBeTrue();
            editor.GetMarkdown().ShouldBe("**hello world**");
            Key(window, PhysicalKey.U, RawInputModifiers.Control);
            editor.GetMarkdown().ShouldBe("**hello world**");
            Key(window, PhysicalKey.B, RawInputModifiers.Control);
            editor.GetMarkdown().ShouldBe("hello world");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Selecting_Text_Shows_The_Floating_Toolbar_And_It_Applies_Marks()
    {
        var (editor, window) = Show("pick some words here");
        try
        {
            Focus(editor);
            editor.Engine.SetSelection(RichTextEditorSelection.Range(0, 5, 9));
            Dispatcher.UIThread.RunJobs();
            var toolbar = editor.Toolbar.ShouldNotBeNull();
            toolbar.IsShown.ShouldBeTrue();
            toolbar.IsVisible.ShouldBeTrue();
            toolbar.Bold.IsChecked.ShouldBeFalse();
            toolbar.Italic.Invoke();
            Dispatcher.UIThread.RunJobs();
            editor.GetMarkdown().ShouldBe("pick _some_ words here");
            toolbar.Italic.IsChecked.ShouldBeTrue();
            // The editor keeps focus: toolbar presses never take it.
            Surface(editor).IsKeyboardFocusWithin.ShouldBeTrue();

            editor.Engine.SetSelection(RichTextEditorSelection.Caret(0, 2));
            Dispatcher.UIThread.RunJobs();
            toolbar.IsShown.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Toolbar_Press_Does_Not_Move_Focus()
    {
        var (editor, window) = Show("pick some words here");
        try
        {
            Focus(editor);
            editor.Engine.SetSelection(RichTextEditorSelection.Range(0, 0, 4));
            Dispatcher.UIThread.RunJobs();
            var toolbar = editor.Toolbar!;
            AvaloniaHeadlessPlatform.ForceRenderTimerTick();
            var center = toolbar.Bold.TranslatePoint(new Point(16, 16), window)!.Value;
            window.MouseDown(center, MouseButton.Left);
            window.MouseUp(center, MouseButton.Left);
            Dispatcher.UIThread.RunJobs();
            Surface(editor).IsKeyboardFocusWithin.ShouldBeTrue();
            editor.GetMarkdown().ShouldBe("**pick** some words here");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Link_Field_Applies_A_Normalized_Link()
    {
        var (editor, window) = Show("read the docs");
        try
        {
            Focus(editor);
            editor.Engine.SetSelection(RichTextEditorSelection.Range(0, 9, 13));
            Dispatcher.UIThread.RunJobs();
            Key(window, PhysicalKey.K, RawInputModifiers.Control);
            var toolbar = editor.Toolbar!;
            toolbar.IsLinkFace.ShouldBeTrue();
            toolbar.LinkBox.IsKeyboardFocusWithin.ShouldBeTrue();
            window.KeyTextInput("uiarc.dev");
            Dispatcher.UIThread.RunJobs();
            toolbar.Apply.Classes.ShouldContain("ready");
            Key(window, PhysicalKey.Enter);
            editor.GetMarkdown().ShouldBe("read the [docs](https://uiarc.dev)");
            editor.LiveRegion!.Message.ShouldBe("Link added");
            Surface(editor).IsKeyboardFocusWithin.ShouldBeTrue();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Slash_Menu_Filters_And_Chooses_With_The_Keyboard()
    {
        var (editor, window) = Show();
        try
        {
            Focus(editor);
            Type(window, "/");
            var menu = editor.SlashMenu.ShouldNotBeNull();
            menu.IsShown.ShouldBeTrue();
            menu.Items.Count.ShouldBe(9);
            Type(window, "li");
            menu.Items.Select(i => i.Label).ShouldBe(new[] { "Bulleted list", "Numbered list", "Divider" });
            Key(window, PhysicalKey.ArrowDown);
            menu.ActiveIndex.ShouldBe(1);
            Key(window, PhysicalKey.Enter);
            menu.IsShown.ShouldBeFalse();
            editor.Engine.Blocks[0].Type.ShouldBe(RichTextBlockType.NumberedList);
            editor.Engine.Blocks[0].Text.ShouldBe(string.Empty);
            editor.LiveRegion!.Message.ShouldBe("Numbered list added");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Escape_Closes_The_Slash_Menu()
    {
        var (editor, window) = Show();
        try
        {
            Focus(editor);
            Type(window, "/");
            editor.SlashMenu!.IsShown.ShouldBeTrue();
            Key(window, PhysicalKey.Escape);
            editor.SlashMenu!.IsShown.ShouldBeFalse();
            editor.GetText().ShouldBe("/");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Arrow_Keys_Move_Across_Blocks_And_Lines()
    {
        var (editor, window) = Show("first line\n\nsecond");
        try
        {
            Focus(editor);
            editor.Engine.SetSelection(RichTextEditorSelection.Caret(0, 3));
            Key(window, PhysicalKey.ArrowDown);
            editor.Engine.Selection.Focus.Block.ShouldBe(1);
            Key(window, PhysicalKey.ArrowUp);
            editor.Engine.Selection.Focus.ShouldBe(new RichTextEditorPosition(0, 3));
            Key(window, PhysicalKey.End);
            editor.Engine.Selection.Focus.ShouldBe(new RichTextEditorPosition(0, 10));
            Key(window, PhysicalKey.ArrowRight);
            editor.Engine.Selection.Focus.ShouldBe(new RichTextEditorPosition(1, 0));
            Key(window, PhysicalKey.ArrowLeft, RawInputModifiers.Shift);
            editor.Engine.Selection.Anchor.ShouldBe(new RichTextEditorPosition(1, 0));
            editor.Engine.Selection.Focus.ShouldBe(new RichTextEditorPosition(0, 10));
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Clicking_Places_The_Caret_Under_The_Pointer()
    {
        var (editor, window) = Show("alpha\n\nbeta");
        try
        {
            var surface = Surface(editor);
            AvaloniaHeadlessPlatform.ForceRenderTimerTick();
            var second = surface.LineRect(new RichTextEditorPosition(1, 2));
            var point = surface.TranslatePoint(second.Center, window)!.Value;
            window.MouseDown(point, MouseButton.Left);
            window.MouseUp(point, MouseButton.Left);
            Dispatcher.UIThread.RunJobs();
            surface.IsKeyboardFocusWithin.ShouldBeTrue();
            editor.Engine.Selection.Focus.ShouldBe(new RichTextEditorPosition(1, 2));
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Block_Spacing_Follows_Arc()
    {
        var (editor, window) = Show("# Title\n\nbody\n\n- a\n- b");
        try
        {
            var surface = Surface(editor);
            surface.Views[0].FontSize.ShouldBe(28);
            surface.Views[0].LineHeight.ShouldBe(35, 0.01);
            surface.Views[1].LineHeight.ShouldBe(26.4, 0.01);
            // Heading then paragraph: .45em of the paragraph (7.2px).
            (surface.BlockTop(1) - surface.BlockTop(0) - surface.Views[0].Height).ShouldBe(7.2, 0.01);
            // Paragraph then list: .6em (9.6px); list items: .2em (3.2px).
            (surface.BlockTop(2) - surface.BlockTop(1) - surface.Views[1].Height).ShouldBe(9.6, 0.01);
            (surface.BlockTop(3) - surface.BlockTop(2) - surface.Views[2].Height).ShouldBe(3.2, 0.01);
            surface.Views[2].TextX.ShouldBe(26.4, 0.01);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Editing_Reflows_Only_The_Changed_Block()
    {
        var markdown = string.Join("\n\n", Enumerable.Range(0, 400).Select(i => $"Paragraph number {i} with some text."));
        var (editor, window) = Show(markdown);
        try
        {
            var surface = Surface(editor);
            var before = surface.Views.ToList();
            Focus(editor);
            editor.Engine.SetSelection(RichTextEditorSelection.Caret(200, 3));
            Type(window, "x");
            var after = surface.Views;
            after.Count.ShouldBe(400);
            var rebuilt = after.Where((view, i) => !ReferenceEquals(view, before[i])).Count();
            rebuilt.ShouldBe(1);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Read_Only_Blocks_Editing_But_Allows_Selection()
    {
        var (editor, window) = Show("fixed");
        try
        {
            editor.IsReadOnly = true;
            Focus(editor);
            Type(window, "abc");
            editor.GetText().ShouldBe("fixed");
            Key(window, PhysicalKey.A, RawInputModifiers.Control);
            editor.Engine.Selection.IsCollapsed.ShouldBeFalse();
            Dispatcher.UIThread.RunJobs();
            (editor.Toolbar?.IsShown ?? false).ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Composition_Text_Skips_Shortcuts()
    {
        var (editor, window) = Show();
        try
        {
            Focus(editor);
            var surface = Surface(editor);
            surface.Ime.SetPreeditText("# ");
            Dispatcher.UIThread.RunJobs();
            surface.IsComposing.ShouldBeTrue();
            surface.Views[0].Preedit.ShouldBe("# ");
            surface.Ime.SetPreeditText(null);
            window.KeyTextInput("# ");
            Dispatcher.UIThread.RunJobs();
            editor.Engine.Blocks[0].Type.ShouldBe(RichTextBlockType.Paragraph);
            editor.GetText().ShouldBe("#");
            editor.Engine.Blocks[0].Text.ShouldBe("# ");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Composition_Commits_Chinese_Text()
    {
        var (editor, window) = Show();
        try
        {
            Focus(editor);
            var surface = Surface(editor);
            surface.Ime.SetPreeditText("nin h", 5);
            Dispatcher.UIThread.RunJobs();
            surface.Views[0].Preedit.ShouldBe("nin h");
            surface.Ime.SetPreeditText(null);
            window.KeyTextInput("您好");
            Dispatcher.UIThread.RunJobs();
            editor.Engine.Blocks[0].Text.ShouldBe("您好");
            surface.IsComposing.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Paste_Content_Follows_Arc_Rules()
    {
        var (editor, window) = Show("ab");
        try
        {
            Focus(editor);
            editor.Engine.SetSelection(RichTextEditorSelection.Caret(0, 1));
            editor.PasteContent("## Head\n\n- one", null, null);
            editor.GetMarkdown().ShouldBe("a\n\n## Head\n\n- one\n\nb");

            editor.Clear();
            editor.PasteContent("x", null, "<meta charset=utf-8><b style=\"font-weight:normal\"><p>Hello <span style=\"font-weight:700\">bold</span></p></b>");
            editor.GetMarkdown().ShouldBe("Hello **bold**");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public async Task Copy_And_Paste_Keep_Marks_Through_The_Clipboard()
    {
        var (editor, window) = Show("say **bold** words");
        try
        {
            Focus(editor);
            editor.Engine.SetSelection(RichTextEditorSelection.Range(0, 4, 8));
            Key(window, PhysicalKey.C, RawInputModifiers.Control);
            await Task.Delay(20);
            Dispatcher.UIThread.RunJobs();
            var clipboard = TopLevel.GetTopLevel(editor)!.Clipboard!;
            (await clipboard.TryGetTextAsync()).ShouldBe("bold");
            editor.Engine.SetSelection(RichTextEditorSelection.Caret(0, editor.Engine.Blocks[0].Length));
            Key(window, PhysicalKey.V, RawInputModifiers.Control);
            for (var i = 0; i < 5; i++)
            {
                await Task.Delay(10);
                Dispatcher.UIThread.RunJobs();
            }
            editor.GetMarkdown().ShouldBe("say **bold** words**bold**");

            editor.Engine.SetSelection(RichTextEditorSelection.Range(0, 0, 4));
            Key(window, PhysicalKey.X, RawInputModifiers.Control);
            for (var i = 0; i < 5; i++)
            {
                await Task.Delay(10);
                Dispatcher.UIThread.RunJobs();
            }
            editor.GetText().ShouldStartWith("bold");
        }
        finally
        {
            window.Close();
        }
    }
}
