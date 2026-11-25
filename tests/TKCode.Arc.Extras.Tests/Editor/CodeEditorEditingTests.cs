using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using Shouldly;
using TKCode.Arc.Editor;
using TKCode.Arc.Editor.Contrib;
using TKCode.Arc.Editor.Contrib.BracketMatching;
using TextSelection = TKCode.Arc.Editor.TextSelection;

namespace TKCode.Arc.Tests.Editor;

/// <summary>
/// The editing commands of phase 5 in the control: the line, comment, bracket, indentation, caret, cursor undo, line
/// selection, smart select and word actions are registered with Monaco's keybindings (the macOS variants on macOS) and
/// preconditions, and the matching bracket is highlighted.
/// </summary>
public class CodeEditorEditingTests
{
    // KeyMod.CtrlCmd: Cmd on macOS, Ctrl elsewhere
    private static readonly RawInputModifiers CtrlCmd = OperatingSystem.IsMacOS() ? RawInputModifiers.Meta : RawInputModifiers.Control;

    private static (CodeEditor Editor, Avalonia.Controls.Window Window, CodeEditorView View) Show(string text, string? languageId = null)
    {
        var editor = new CodeEditor { Text = text, Width = 500, Height = 240 };
        if (languageId is not null)
        {
            editor.LanguageId = languageId;
        }
        var window = TestHelpers.Show(editor);
        editor.Focus();
        Dispatcher.UIThread.RunJobs();
        window.CaptureRenderedFrame()?.Dispose();
        Dispatcher.UIThread.RunJobs();
        return (editor, window, TestHelpers.Descendant<CodeEditorView>(editor));
    }

    private static string[] Lines(CodeEditor editor) => editor.Model.GetLinesContent();

    // lets the dispatcher's timers run for a while (the bracket highlight waits 50 ms)
    private static void RunTimers(int milliseconds)
    {
        var frame = new DispatcherFrame();
        DispatcherTimer.RunOnce(() => frame.Continue = false, TimeSpan.FromMilliseconds(milliseconds));
        Dispatcher.UIThread.PushFrame(frame);
    }

    [AvaloniaFact]
    public void Actions_are_registered_with_labels()
    {
        var (editor, _, _) = Show("x");

        editor.GetAction("editor.action.commentLine").ShouldNotBeNull().Label.ShouldBe("Toggle Line Comment");
        editor.GetAction("editor.action.moveLinesUpAction").ShouldNotBeNull().Label.ShouldBe("Move Line Up");
        editor.GetAction("editor.action.jumpToBracket").ShouldNotBeNull().Label.ShouldBe("Go to Bracket");
        editor.GetAction("editor.action.smartSelect.expand").ShouldNotBeNull().Label.ShouldBe("Expand Selection");
        editor.GetAction("editor.action.reindentlines").ShouldNotBeNull().Label.ShouldBe("Reindent Lines");
        editor.GetAction("cursorUndo").ShouldNotBeNull().Label.ShouldBe("Cursor Undo");
        editor.GetAction("deleteInsideWord").ShouldNotBeNull().Label.ShouldBe("Delete Word");
        editor.GetAction("expandLineSelection").ShouldNotBeNull();
        editor.GetActions().Count.ShouldBeGreaterThanOrEqualTo(51); // the editing actions; other features add theirs

        // the word commands are commands, not actions
        editor.GetAction("cursorWordLeft").ShouldBeNull();
        editor.CommandsRegistry.GetCommand("cursorWordLeft").ShouldNotBeNull();
        editor.CommandsRegistry.GetCommand("cursorWordPartStartLeft").ShouldNotBeNull();
    }

    [AvaloniaFact]
    public void Ctrl_slash_toggles_a_line_comment()
    {
        var (editor, window, _) = Show("let a = 1;\nlet b = 2;", "javascript");
        editor.SetSelection(new TextSelection(1, 1, 2, 3));

        window.KeyPressQwerty(PhysicalKey.Slash, CtrlCmd);
        Lines(editor).ShouldBe(["// let a = 1;", "// let b = 2;"]);

        window.KeyPressQwerty(PhysicalKey.Slash, CtrlCmd);
        Lines(editor).ShouldBe(["let a = 1;", "let b = 2;"]);
    }

    [AvaloniaFact]
    public void Shift_alt_a_toggles_a_block_comment()
    {
        var (editor, window, _) = Show("let a = 1;", "javascript");
        editor.SetSelection(new TextSelection(1, 5, 1, 6));

        var modifiers = OperatingSystem.IsLinux() ? CtrlCmd | RawInputModifiers.Shift : RawInputModifiers.Shift | RawInputModifiers.Alt;
        window.KeyPressQwerty(PhysicalKey.A, modifiers);
        editor.Model.GetValue().ShouldBe("let /* a */ = 1;");
    }

    [AvaloniaFact]
    public void Alt_arrows_move_and_copy_lines()
    {
        var (editor, window, _) = Show("one\ntwo\nthree");
        editor.SetPosition(new TextPosition(1, 2));

        window.KeyPressQwerty(PhysicalKey.ArrowDown, RawInputModifiers.Alt);
        Lines(editor).ShouldBe(["two", "one", "three"]);
        editor.GetPosition().ShouldBe(new TextPosition(2, 2));

        window.KeyPressQwerty(PhysicalKey.ArrowUp, RawInputModifiers.Alt);
        Lines(editor).ShouldBe(["one", "two", "three"]);

        var copy = OperatingSystem.IsLinux() ? CtrlCmd | RawInputModifiers.Alt | RawInputModifiers.Shift : RawInputModifiers.Alt | RawInputModifiers.Shift;
        window.KeyPressQwerty(PhysicalKey.ArrowDown, copy);
        Lines(editor).ShouldBe(["one", "one", "two", "three"]);
        editor.GetPosition().ShouldBe(new TextPosition(2, 2));
    }

    [AvaloniaFact]
    public void Line_keys_delete_indent_insert_and_expand()
    {
        var (editor, window, _) = Show("a\nb\nc");
        editor.SetPosition(new TextPosition(2, 1));

        // Ctrl+Shift+K deletes the line
        window.KeyPressQwerty(PhysicalKey.K, CtrlCmd | RawInputModifiers.Shift);
        Lines(editor).ShouldBe(["a", "c"]);

        // Ctrl+] / Ctrl+[ indent and outdent
        window.KeyPressQwerty(PhysicalKey.BracketRight, CtrlCmd);
        Lines(editor).ShouldBe(["a", "    c"]);
        window.KeyPressQwerty(PhysicalKey.BracketLeft, CtrlCmd);
        Lines(editor).ShouldBe(["a", "c"]);

        // Ctrl+Enter / Ctrl+Shift+Enter insert a line below / above
        editor.SetPosition(new TextPosition(1, 1));
        window.KeyPressQwerty(PhysicalKey.Enter, CtrlCmd);
        Lines(editor).ShouldBe(["a", "", "c"]);
        editor.GetPosition().ShouldBe(new TextPosition(2, 1));
        window.KeyPressQwerty(PhysicalKey.Enter, CtrlCmd | RawInputModifiers.Shift);
        Lines(editor).ShouldBe(["a", "", "", "c"]);

        // Ctrl+L selects the line, then the next
        editor.SetPosition(new TextPosition(1, 1));
        window.KeyPressQwerty(PhysicalKey.L, CtrlCmd);
        editor.GetSelection().ShouldBe(new TextSelection(1, 1, 2, 1));
        window.KeyPressQwerty(PhysicalKey.L, CtrlCmd);
        editor.GetSelection().ShouldBe(new TextSelection(1, 1, 3, 1));
    }

    [AvaloniaFact]
    public void Writable_actions_do_nothing_in_a_read_only_editor()
    {
        var (editor, window, _) = Show("one\ntwo");
        editor.IsReadOnly = true;
        Dispatcher.UIThread.RunJobs();
        editor.SetPosition(new TextPosition(1, 1));

        window.KeyPressQwerty(PhysicalKey.ArrowDown, RawInputModifiers.Alt);
        window.KeyPressQwerty(PhysicalKey.K, CtrlCmd | RawInputModifiers.Shift);
        editor.GetAction("editor.action.sortLinesDescending")!.Run().GetAwaiter().GetResult();
        Lines(editor).ShouldBe(["one", "two"]);

        // the line selection is not an edit
        window.KeyPressQwerty(PhysicalKey.L, CtrlCmd);
        editor.GetSelection().ShouldBe(new TextSelection(1, 1, 2, 1));
    }

    [AvaloniaFact]
    public void Brackets_jump_select_and_remove()
    {
        var (editor, window, _) = Show("var x = (3 + (5-7)); y();", "javascript");
        editor.SetPosition(new TextPosition(1, 9));

        // Ctrl+Shift+\ goes to the matching bracket
        window.KeyPressQwerty(PhysicalKey.Backslash, CtrlCmd | RawInputModifiers.Shift);
        editor.GetPosition().ShouldBe(new TextPosition(1, 19));

        editor.SetPosition(new TextPosition(1, 16));
        editor.ExecuteCommand("editor.action.selectToBracket").GetAwaiter().GetResult();
        editor.GetSelection().ShouldBe(new TextSelection(1, 14, 1, 19));
        editor.SetPosition(new TextPosition(1, 16));
        editor.ExecuteCommand("editor.action.selectToBracket", new BracketMatchingController.SelectToBracketArgs(false)).GetAwaiter().GetResult();
        editor.GetSelection().ShouldBe(new TextSelection(1, 15, 1, 18));

        // Ctrl+Alt+Backspace removes the enclosing brackets
        editor.SetPosition(new TextPosition(1, 16));
        window.KeyPressQwerty(PhysicalKey.Backspace, CtrlCmd | RawInputModifiers.Alt);
        editor.Model.GetValue().ShouldBe("var x = (3 + 5-7); y();");
    }

    [AvaloniaFact]
    public void The_matching_bracket_is_highlighted()
    {
        var (editor, _, view) = Show("if (a) {\n  b(c);\n}", "javascript");
        var controller = BracketMatchingController.Get(editor).ShouldNotBeNull();

        editor.SetPosition(new TextPosition(1, 8));
        controller.UpdateBrackets();
        controller.HighlightedRanges.ShouldBe([new TextRange(1, 8, 1, 9), new TextRange(3, 1, 3, 2)]);

        var rects = view.GetDecorationRects(1, 3).Where(r => r.ClassName == BracketMatching.BracketMatchClassName).ToList();
        rects.Count.ShouldBe(2);
        rects.ShouldAllBe(r => r.Fill == view.BracketMatchBrush && r.Stroke == view.BracketMatchBorderBrush);
        view.BracketMatchBrush.ShouldNotBeNull();

        // the decorations carry the overview ruler mark
        editor.Model.GetDecorationsInRange(new TextRange(1, 1, 3, 2))
            .Where(d => d.Options.ClassName == BracketMatching.BracketMatchClassName)
            .ShouldAllBe(d => d.Options.OverviewRuler != null);

        // inside the braces but not on a bracket: the enclosing pair ('always'), without an overview ruler mark
        editor.SetPosition(new TextPosition(2, 2));
        controller.UpdateBrackets();
        controller.HighlightedRanges.ShouldBe([new TextRange(1, 8, 1, 9), new TextRange(3, 1, 3, 2)]);

        // matchBrackets 'near': nothing there
        editor.EditingOptions = editor.EditingOptions with { MatchBrackets = MatchBracketsMode.Near };
        controller.UpdateBrackets();
        controller.HighlightedRanges.ShouldBeEmpty();

        // never: cleared
        editor.SetPosition(new TextPosition(2, 4));
        controller.UpdateBrackets();
        controller.HighlightedRanges.ShouldBe([new TextRange(2, 4, 2, 5), new TextRange(2, 6, 2, 7)]);
        editor.EditingOptions = editor.EditingOptions with { MatchBrackets = MatchBracketsMode.Never };
        controller.UpdateBrackets();
        controller.HighlightedRanges.ShouldBeEmpty();
    }

    [AvaloniaFact]
    public void The_highlight_follows_the_cursor_after_the_delay()
    {
        var (editor, _, _) = Show("(a) [b]", "javascript");
        var controller = BracketMatchingController.Get(editor).ShouldNotBeNull();

        editor.SetPosition(new TextPosition(1, 5));
        controller.HighlightedRanges.ShouldBeEmpty();
        RunTimers(150);
        controller.HighlightedRanges.ShouldBe([new TextRange(1, 5, 1, 6), new TextRange(1, 7, 1, 8)]);

        // an edit schedules an update too: "(a) x[b]" with the cursor before [
        editor.Trigger("keyboard", EditorHandler.Type, new TypePayload("x"));
        RunTimers(150);
        controller.HighlightedRanges.ShouldBe([new TextRange(1, 6, 1, 7), new TextRange(1, 8, 1, 9)]);
        editor.SetPosition(new TextPosition(1, 1));
        RunTimers(150);
        controller.HighlightedRanges.ShouldBe([new TextRange(1, 1, 1, 2), new TextRange(1, 3, 1, 4)]);
    }

    [AvaloniaFact]
    public void Ctrl_u_undoes_the_cursor()
    {
        var (editor, window, _) = Show("hello world");
        editor.SetPosition(new TextPosition(1, 3));
        editor.SetPosition(new TextPosition(1, 9));

        window.KeyPressQwerty(PhysicalKey.U, CtrlCmd);
        editor.GetPosition().ShouldBe(new TextPosition(1, 3));

        editor.ExecuteCommand("cursorRedo").GetAwaiter().GetResult();
        editor.GetPosition().ShouldBe(new TextPosition(1, 9));
    }

    [AvaloniaFact]
    public void Shift_alt_arrows_expand_and_shrink_the_selection()
    {
        var (editor, window, _) = Show("call(fooBar, 1)", "javascript");
        editor.SetPosition(new TextPosition(1, 7));

        // macOS: Ctrl+Cmd+Shift+Right; elsewhere Shift+Alt+Right
        var expand = OperatingSystem.IsMacOS() ? RawInputModifiers.Control | RawInputModifiers.Meta | RawInputModifiers.Shift : RawInputModifiers.Shift | RawInputModifiers.Alt;
        window.KeyPressQwerty(PhysicalKey.ArrowRight, expand);
        Dispatcher.UIThread.RunJobs();
        editor.GetSelection().ShouldBe(new TextSelection(1, 6, 1, 9)); // foo
        window.KeyPressQwerty(PhysicalKey.ArrowRight, expand);
        Dispatcher.UIThread.RunJobs();
        editor.GetSelection().ShouldBe(new TextSelection(1, 6, 1, 12)); // fooBar
        window.KeyPressQwerty(PhysicalKey.ArrowRight, expand);
        Dispatcher.UIThread.RunJobs();
        editor.GetSelection().ShouldBe(new TextSelection(1, 6, 1, 15)); // inside ()

        window.KeyPressQwerty(PhysicalKey.ArrowLeft, expand);
        Dispatcher.UIThread.RunJobs();
        editor.GetSelection().ShouldBe(new TextSelection(1, 6, 1, 12));
    }

    [AvaloniaFact]
    public void Word_keys_move_and_delete_by_word()
    {
        var (editor, window, _) = Show("foo bar baz");
        editor.SetPosition(new TextPosition(1, 12));

        // Ctrl+Left (Alt+Left on macOS)
        var word = OperatingSystem.IsMacOS() ? RawInputModifiers.Alt : RawInputModifiers.Control;
        window.KeyPressQwerty(PhysicalKey.ArrowLeft, word);
        editor.GetPosition().ShouldBe(new TextPosition(1, 9));
        window.KeyPressQwerty(PhysicalKey.ArrowLeft, word | RawInputModifiers.Shift);
        editor.GetSelection().ShouldBe(new TextSelection(1, 9, 1, 5));
        window.KeyPressQwerty(PhysicalKey.ArrowRight, word);
        editor.GetPosition().ShouldBe(new TextPosition(1, 8));

        // Ctrl+Backspace / Ctrl+Delete
        editor.SetPosition(new TextPosition(1, 12));
        window.KeyPressQwerty(PhysicalKey.Backspace, word);
        editor.Model.GetValue().ShouldBe("foo bar ");
        editor.SetPosition(new TextPosition(1, 1));
        window.KeyPressQwerty(PhysicalKey.Delete, word);
        editor.Model.GetValue().ShouldBe(" bar ");
    }

    [AvaloniaFact]
    public void Indentation_and_case_actions_run_by_id()
    {
        var (editor, _, _) = Show("\tfoo_bar\n\tbaz");

        editor.ExecuteCommand("editor.action.indentationToSpaces").GetAwaiter().GetResult();
        Lines(editor).ShouldBe(["    foo_bar", "    baz"]);
        editor.Model.GetOptions().InsertSpaces.ShouldBeTrue();

        editor.ExecuteCommand("editor.action.indentUsingTabs", 2).GetAwaiter().GetResult();
        editor.Model.GetOptions().InsertSpaces.ShouldBeFalse();
        editor.Model.GetOptions().TabSize.ShouldBe(2);

        editor.SetSelection(new TextSelection(1, 5, 1, 12));
        editor.ExecuteCommand("editor.action.transformToCamelcase").GetAwaiter().GetResult();
        Lines(editor)[0].ShouldBe("    fooBar");
        editor.SetSelection(new TextSelection(1, 5, 1, 11));
        editor.ExecuteCommand("editor.action.transformToUppercase").GetAwaiter().GetResult();
        Lines(editor)[0].ShouldBe("    FOOBAR");

        editor.ExecuteCommand("editor.action.insertFinalNewLine").GetAwaiter().GetResult();
        editor.Model.GetValue().ShouldEndWith("baz\n");
    }

    [AvaloniaFact]
    public void Auto_indent_on_paste_follows_its_option()
    {
        var (editor, _, _) = Show("function f() {\n\n}", "javascript");
        // Monaco's javascript configuration has no indentation rules; add some
        editor.LanguageConfigurationService.Register("javascript", new TKCode.Arc.Editor.Languages.LanguageConfiguration
        {
            IndentationRules = new TKCode.Arc.Editor.Languages.IndentationRule(
                new System.Text.RegularExpressions.Regex(@"^\s*\}"),
                new System.Text.RegularExpressions.Regex(@"\{[^}]*$")),
        });
        editor.SetPosition(new TextPosition(2, 1));
        editor.Trigger("keyboard", EditorHandler.Paste, new PastePayload("x();"));
        Lines(editor)[1].ShouldBe("x();");
        editor.Model.Undo();

        editor.EditingOptions = editor.EditingOptions with { AutoIndentOnPaste = true };
        editor.SetPosition(new TextPosition(2, 1));
        editor.Trigger("keyboard", EditorHandler.Paste, new PastePayload("x();\ny();"));
        Lines(editor).ShouldBe(["function f() {", "    x();", "    y();", "}"]);
    }
}
