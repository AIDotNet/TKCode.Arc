using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using Shouldly;
using TKCode.Arc.Editor;
using TKCode.Arc.Editor.Contrib.Snippet;
using TextSelection = TKCode.Arc.Editor.TextSelection;

namespace TKCode.Arc.Tests.Editor;

/// <summary>
/// The snippet feature in the control (upstream's snippetController2.ts contribution): CodeEditor.InsertSnippet, Tab /
/// Shift+Tab / Escape through the keybindings, mirrors while typing, the placeholder decorations, the choice list and
/// the ISnippetInserter seam, headless.
/// </summary>
public class CodeEditorSnippetTests
{
    private static (CodeEditor Editor, Avalonia.Controls.Window Window) Show(string text, CodeEditor? editor = null)
    {
        editor ??= new CodeEditor();
        editor.Text = text;
        editor.Width = 480;
        editor.Height = 240;
        var window = TestHelpers.Show(editor);
        editor.Focus();
        Dispatcher.UIThread.RunJobs();
        window.CaptureRenderedFrame()?.Dispose();
        Dispatcher.UIThread.RunJobs();
        return (editor, window);
    }

    private static bool InSnippetMode(CodeEditor editor) => SnippetController2.InSnippetMode.GetValue(editor.ContextKeyService);

    [AvaloniaFact]
    public void InsertSnippet_selects_the_first_tab_stop_and_Tab_moves_through_them()
    {
        var (editor, window) = Show("");

        editor.InsertSnippet("for (let ${1:i} = 0; $1 < ${2:n}; $1++) {\n\t$0\n}");

        editor.Text.ShouldBe("for (let i = 0; i < n; i++) {\n    \n}");
        editor.GetSelections().ShouldBe([new TextSelection(1, 10, 1, 11), new TextSelection(1, 17, 1, 18), new TextSelection(1, 24, 1, 25)]);
        InSnippetMode(editor).ShouldBeTrue();

        // typing changes the tab stop and its mirrors
        window.KeyTextInput("idx");
        editor.Text.ShouldBe("for (let idx = 0; idx < n; idx++) {\n    \n}");

        window.KeyPressQwerty(PhysicalKey.Tab, RawInputModifiers.None);
        editor.GetSelections().ShouldBe([new TextSelection(1, 25, 1, 26)]);

        window.KeyPressQwerty(PhysicalKey.Tab, RawInputModifiers.Shift);
        editor.GetSelections().Count.ShouldBe(3);
        editor.GetSelection().ShouldBe(new TextSelection(1, 10, 1, 13));

        window.KeyPressQwerty(PhysicalKey.Tab, RawInputModifiers.None);
        window.KeyPressQwerty(PhysicalKey.Tab, RawInputModifiers.None);
        editor.GetSelections().ShouldBe([new TextSelection(2, 5, 2, 5)]);
        InSnippetMode(editor).ShouldBeFalse();

        // out of snippet mode Tab indents again
        window.KeyPressQwerty(PhysicalKey.Tab, RawInputModifiers.None);
        editor.Text.ShouldBe("for (let idx = 0; idx < n; idx++) {\n        \n}");
    }

    [AvaloniaFact]
    public void Escape_leaves_snippet_mode_and_keeps_the_primary_cursor()
    {
        var (editor, window) = Show("");
        editor.InsertSnippet("${1:a} ${1:a} $0");
        editor.GetSelections().Count.ShouldBe(2);

        window.KeyPressQwerty(PhysicalKey.Escape, RawInputModifiers.None);

        InSnippetMode(editor).ShouldBeFalse();
        editor.GetSelections().ShouldBe([new TextSelection(1, 1, 1, 2)]);
    }

    [AvaloniaFact]
    public void A_snippet_is_inserted_at_every_cursor_with_its_variables()
    {
        var (editor, _) = Show("one\ntwo");
        editor.SetSelections([new TextSelection(1, 1, 1, 4), new TextSelection(2, 1, 2, 4)]);

        editor.InsertSnippet("<$TM_LINE_NUMBER:${TM_SELECTED_TEXT/(.*)/${1:/upcase}/}>$0");

        editor.Text.ShouldBe("<1:ONE>\n<2:TWO>");
        InSnippetMode(editor).ShouldBeFalse();
    }

    [AvaloniaFact]
    public void A_snippet_inserted_before_the_editor_is_shown_is_inserted_when_it_is()
    {
        var editor = new CodeEditor();
        editor.InsertSnippet("hello ${1:world}$0");
        editor.Text.ShouldBe("");

        Show("", editor);

        editor.Text.ShouldBe("hello world");
        editor.GetSelection().ShouldBe(new TextSelection(1, 7, 1, 12));
        InSnippetMode(editor).ShouldBeTrue();
    }

    [AvaloniaFact]
    public void Placeholders_are_drawn_with_the_snippet_styles()
    {
        var (editor, window) = Show("");
        editor.InsertSnippet("${1:first} ${2:second}");
        window.CaptureRenderedFrame()?.Dispose();

        var rects = editor.View!.GetDecorationRects(1, 1);
        rects.Count(r => r.ClassName == SnippetDecorationClassNames.Placeholder).ShouldBe(2);
        rects.ShouldAllBe(r => r.Fill != null || r.Stroke != null);
    }

    [AvaloniaFact]
    public void A_choice_shows_its_options_and_Enter_takes_the_selected_one()
    {
        var (editor, window) = Show("");
        editor.InsertSnippet("let x = ${1|one,two,three|};$0");
        Dispatcher.UIThread.RunJobs();
        window.CaptureRenderedFrame()?.Dispose();

        var controller = CodeEditorSnippetController.Get(editor).ShouldNotBeNull();
        var list = controller.VisibleChoiceList.ShouldNotBeNull();
        list.Items.ShouldBe(["one", "two", "three"]);
        list.SelectedIndex.ShouldBe(0);
        list.Bounds.Height.ShouldBeGreaterThan(0);
        editor.GetContentWidgetPlacement("editor.contrib.snippetChoiceWidget").ShouldNotBeNull();

        window.KeyPressQwerty(PhysicalKey.ArrowDown, RawInputModifiers.None);
        list.SelectedIndex.ShouldBe(1);
        window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);

        editor.Text.ShouldBe("let x = two;");
        editor.GetSelection().ShouldBe(new TextSelection(1, 13, 1, 13));
        controller.VisibleChoiceList.ShouldBeNull();
        InSnippetMode(editor).ShouldBeFalse();
    }

    [AvaloniaFact]
    public void Escape_closes_the_choice_list_first_then_leaves_the_snippet()
    {
        var (editor, window) = Show("");
        editor.InsertSnippet("${1|a,b|} ${2:x}");
        Dispatcher.UIThread.RunJobs();
        var controller = CodeEditorSnippetController.Get(editor).ShouldNotBeNull();
        controller.VisibleChoiceList.ShouldNotBeNull();

        window.KeyPressQwerty(PhysicalKey.Escape, RawInputModifiers.None);
        controller.VisibleChoiceList.ShouldBeNull();
        InSnippetMode(editor).ShouldBeTrue();

        window.KeyPressQwerty(PhysicalKey.Escape, RawInputModifiers.None);
        InSnippetMode(editor).ShouldBeFalse();
    }

    [AvaloniaFact]
    public void The_suggest_feature_can_take_the_choices()
    {
        var (editor, _) = Show("");
        var controller = CodeEditorSnippetController.Get(editor).ShouldNotBeNull();
        var shown = new List<TKCode.Arc.Editor.Languages.ICompletionItemProvider>();
        controller.ShowSimpleSuggestions = provider =>
        {
            shown.Add(provider);
            return true;
        };

        editor.InsertSnippet("${1|a,b|}");
        Dispatcher.UIThread.RunJobs();

        shown.Count.ShouldBe(1);
        controller.VisibleChoiceList.ShouldBeNull();
        editor.LanguageFeatures.CompletionProvider.Ordered(editor.Model).ShouldContain(shown[0]);
    }

    [AvaloniaFact]
    public void The_snippet_inserter_seam_inserts_with_overwrite_and_falls_back_to_plain_text()
    {
        var (editor, _) = Show("con");
        editor.SetSelection(new TextSelection(1, 4, 1, 4));
        var inserter = SnippetInserter.Get(editor).ShouldNotBeNull();
        inserter.Insert("console.log(${1:value})$0", new SnippetInsertOptions { OverwriteBefore = 3 });
        editor.Text.ShouldBe("console.log(value)");
        inserter.IsInSnippet.ShouldBeTrue();

        // an editor without the snippet feature takes the snippet's text
        var (plain, _) = Show("con", new CodeEditor([], []));
        plain.SetSelection(new TextSelection(1, 4, 1, 4));
        SnippetInserter.Get(plain).ShouldBeNull();
        SnippetInserter.InsertOrPlainText(plain, "console.log(${1:value})$0", new SnippetInsertOptions { OverwriteBefore = 3 });
        plain.Text.ShouldBe("console.log(value)");
        plain.InsertSnippet("!$0");
        plain.Text.ShouldBe("console.log(value)!");
    }

    [AvaloniaFact]
    public void Undo_after_a_snippet_restores_the_text()
    {
        var (editor, window) = Show("x");
        editor.SetSelection(new TextSelection(1, 2, 1, 2));
        editor.InsertSnippet(" + ${1:y}$0");
        editor.Text.ShouldBe("x + y");
        InSnippetMode(editor).ShouldBeTrue();

        var modifier = OperatingSystem.IsMacOS() ? RawInputModifiers.Meta : RawInputModifiers.Control;
        window.KeyPressQwerty(PhysicalKey.Z, modifier);
        Dispatcher.UIThread.RunJobs();

        editor.Text.ShouldBe("x");
        editor.GetSelection().ShouldBe(new TextSelection(1, 2, 1, 2));

        // as upstream, the cursor is still inside the (now empty) placeholders, so snippet mode lasts until it leaves
        window.KeyPressQwerty(PhysicalKey.Escape, RawInputModifiers.None);
        InSnippetMode(editor).ShouldBeFalse();
    }
}
