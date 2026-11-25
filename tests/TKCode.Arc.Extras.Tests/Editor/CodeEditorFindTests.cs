using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Shouldly;
using TKCode.Arc.Editor;
using TKCode.Arc.Editor.Contrib.Find;

namespace TKCode.Arc.Tests.Editor;

/// <summary>
/// Find and replace in the control (upstream's findController.ts registrations and findWidget.ts): the keybindings,
/// the find widget as an overlay, its inputs, toggles, buttons and match count, and the match decorations.
/// </summary>
public class CodeEditorFindTests
{
    private const string Sample = "hello world\nsay hello\nHello there\nnothing";

    private static readonly bool s_mac = OperatingSystem.IsMacOS();

    private static RawInputModifiers CtrlCmd => s_mac ? RawInputModifiers.Meta : RawInputModifiers.Control;

    private static (CodeEditor Editor, Window Window) Show(string text = Sample, double width = 600, double height = 240)
    {
        var editor = new CodeEditor(CodeEditorContributions.Contributions, CodeEditorContributions.EditorCommands)
        {
            Text = text,
            Width = width,
            Height = height,
        };
        var window = TestHelpers.Show(editor);
        editor.Focus();
        Render(window);

        // the cursor on the last line ("nothing"): Ctrl+F seeds the search string with that word, which matches once
        editor.SetPosition(new TextPosition(editor.Model.GetLineCount(), 1));
        return (editor, window);
    }

    private static void Render(Window window)
    {
        for (var i = 0; i < 2; i++)
        {
            Dispatcher.UIThread.RunJobs();
            window.CaptureRenderedFrame()?.Dispose();
        }

        Dispatcher.UIThread.RunJobs();
    }

    private static FindController Controller(CodeEditor editor) => FindController.Get(editor).ShouldNotBeNull();

    private static CodeEditorFindWidget Widget(CodeEditor editor) => Controller(editor).Widget.ShouldNotBeNull();

    private static void OpenFind(Window window) => window.KeyPressQwerty(PhysicalKey.F, CtrlCmd);

    private static void OpenReplace(Window window)
    {
        if (s_mac)
        {
            window.KeyPressQwerty(PhysicalKey.F, RawInputModifiers.Meta | RawInputModifiers.Alt);
        }
        else
        {
            window.KeyPressQwerty(PhysicalKey.H, RawInputModifiers.Control);
        }
    }

    private static void Type(Window window, string text)
    {
        window.KeyTextInput(text);
        Dispatcher.UIThread.RunJobs();
    }

    private static int[] Sel(CodeEditor editor)
    {
        var s = editor.GetSelection().ShouldNotBeNull();
        return [s.StartLineNumber, s.StartColumn, s.EndLineNumber, s.EndColumn];
    }

    [AvaloniaFact]
    public void Ctrl_F_shows_the_widget_seeded_with_the_word_at_the_cursor_and_focuses_its_input()
    {
        var (editor, window) = Show();
        editor.SetPosition(new TextPosition(1, 8));

        OpenFind(window);
        Render(window);

        var widget = Widget(editor);
        widget.IsVisible.ShouldBeTrue();
        widget.IsRevealed.ShouldBeTrue();
        Controller(editor).State.SearchString.ShouldBe("world");
        widget.FindInput.ShouldNotBeNull().Text.ShouldBe("world");
        widget.FindInput!.IsFocused.ShouldBeTrue();
        CodeEditorContextKeys.FindWidgetVisible.GetValue(editor.ContextKeyService).ShouldBeTrue();
        CodeEditorContextKeys.FindInputFocussed.GetValue(editor.ContextKeyService).ShouldBeTrue();
        widget.MatchesCountText.ShouldBe("1 of 1");

        // the widget sits in the top right corner of the editor
        var bounds = widget.Bounds;
        bounds.Right.ShouldBeLessThanOrEqualTo(editor.Bounds.Width);
        bounds.Left.ShouldBeGreaterThan(editor.Bounds.Width / 4);
        bounds.Top.ShouldBeLessThan(20);
    }

    [AvaloniaFact]
    public void Typing_in_the_find_input_searches_and_decorates_every_match()
    {
        var (editor, window) = Show();
        OpenFind(window);
        Type(window, "hello");
        Render(window);

        var state = Controller(editor).State;
        state.SearchString.ShouldBe("hello");
        state.MatchesCount.ShouldBe(3);
        Widget(editor).MatchesCountText.ShouldBe("1 of 3");

        // the current match and the others, drawn with the find styles
        var view = TestHelpers.Descendant<CodeEditorView>(editor);
        var rects = view.GetDecorationRects(1, 4);
        rects.Count(r => r.ClassName.Contains("currentFindMatch", StringComparison.Ordinal)).ShouldBe(1);
        rects.Count(r => r.ClassName.Split(' ').Contains("findMatch")).ShouldBeGreaterThanOrEqualTo(2);
        rects.ShouldContain(r => r.ClassName.Contains("currentFindMatch", StringComparison.Ordinal) && r.Fill == view.FindMatchBrush);
        rects.ShouldContain(r => r.ClassName.Split(' ').Contains("findMatch") && r.Fill == view.FindMatchHighlightBrush);
    }

    [AvaloniaFact]
    public void Enter_and_Shift_Enter_in_the_find_input_go_to_the_next_and_previous_match()
    {
        var (editor, window) = Show();
        OpenFind(window);
        Type(window, "hello");
        Sel(editor).ShouldBe([1, 1, 1, 6]);

        window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
        Dispatcher.UIThread.RunJobs();
        Sel(editor).ShouldBe([2, 5, 2, 10]);
        Widget(editor).MatchesCountText.ShouldBe("2 of 3");

        window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
        Dispatcher.UIThread.RunJobs();
        Sel(editor).ShouldBe([3, 1, 3, 6]);

        window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.Shift);
        Dispatcher.UIThread.RunJobs();
        Sel(editor).ShouldBe([2, 5, 2, 10]);

        // the focus stays in the find input, the text stays as it was
        Widget(editor).FindInput!.IsFocused.ShouldBeTrue();
        editor.Text.ShouldBe(Sample);
    }

    [AvaloniaFact]
    public void F3_finds_the_word_at_the_cursor_without_the_widget_taking_focus()
    {
        var (editor, window) = Show();
        editor.SetPosition(new TextPosition(2, 6));

        window.KeyPressQwerty(PhysicalKey.F3, RawInputModifiers.None);
        Dispatcher.UIThread.RunJobs();

        // upstream: the word at the cursor becomes the search string, and the next match is selected
        Controller(editor).State.SearchString.ShouldBe("hello");
        Sel(editor).ShouldBe([3, 1, 3, 6]);
        editor.IsTextFocused.ShouldBeTrue();
    }

    [AvaloniaFact]
    public void Escape_closes_the_widget_gives_the_focus_back_and_removes_the_decorations()
    {
        var (editor, window) = Show();
        OpenFind(window);
        Type(window, "hello");
        editor.Model.GetAllDecorations(editor.EditorId).ShouldNotBeEmpty();

        window.KeyPressQwerty(PhysicalKey.Escape, RawInputModifiers.None);
        Render(window);

        var widget = Widget(editor);
        widget.IsVisible.ShouldBeFalse();
        Controller(editor).State.IsRevealed.ShouldBeFalse();
        CodeEditorContextKeys.FindWidgetVisible.GetValue(editor.ContextKeyService).ShouldBeFalse();
        CodeEditorContextKeys.FindInputFocussed.GetValue(editor.ContextKeyService).ShouldBeFalse();
        editor.IsTextFocused.ShouldBeTrue();
        editor.Model.GetAllDecorations(editor.EditorId).Where(d => d.Options.Description.Contains("find", StringComparison.Ordinal)).ShouldBeEmpty();

        // the selection stays on the last match
        Sel(editor).ShouldBe([1, 1, 1, 6]);
    }

    [AvaloniaFact]
    public void The_replace_keybinding_shows_the_replace_row_and_Ctrl_Alt_Enter_replaces_every_match()
    {
        var (editor, window) = Show();
        OpenReplace(window);
        Render(window);

        var widget = Widget(editor);
        widget.IsReplaceVisible.ShouldBeTrue();
        widget.ReplaceInput.ShouldNotBeNull().IsEffectivelyVisible.ShouldBeTrue();

        // nothing was selected: the find input has the focus
        widget.FindInput!.IsFocused.ShouldBeTrue();
        Type(window, "hello");
        widget.FocusReplaceInput();
        Dispatcher.UIThread.RunJobs();
        CodeEditorContextKeys.ReplaceInputFocussed.GetValue(editor.ContextKeyService).ShouldBeTrue();
        Type(window, "bye");
        Controller(editor).State.ReplaceString.ShouldBe("bye");

        window.KeyPressQwerty(PhysicalKey.Enter, CtrlCmd | RawInputModifiers.Alt);
        Dispatcher.UIThread.RunJobs();

        // case-insensitive by default: all three
        editor.Text.ShouldBe("bye world\nsay bye\nbye there\nnothing");
        widget.MatchesCountText.ShouldBe("No results");
    }

    [AvaloniaFact]
    public void Enter_in_the_replace_input_replaces_the_current_match_and_moves_on()
    {
        var (editor, window) = Show();
        OpenReplace(window);
        Type(window, "hello");
        Widget(editor).FocusReplaceInput();
        Type(window, "bye");

        window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
        Dispatcher.UIThread.RunJobs();
        editor.Model.GetLineContent(1).ShouldBe("bye world");
        Sel(editor).ShouldBe([2, 5, 2, 10]);
        Controller(editor).State.MatchesCount.ShouldBe(2);
    }

    [AvaloniaFact]
    public void Alt_Enter_selects_every_match()
    {
        var (editor, window) = Show();
        OpenFind(window);
        Type(window, "hello");

        window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.Alt);
        Dispatcher.UIThread.RunJobs();

        editor.GetSelections().Select(s => (s.StartLineNumber, s.StartColumn, s.EndColumn)).OrderBy(s => s.StartLineNumber)
            .ShouldBe([(1, 1, 6), (2, 5, 10), (3, 1, 6)]);
    }

    [AvaloniaFact]
    public void The_toggles_follow_the_state_and_change_it()
    {
        var (editor, window) = Show();
        OpenFind(window);
        Type(window, "hello");
        var widget = Widget(editor);
        var state = Controller(editor).State;

        // a click on match case: only the two lowercase matches
        widget.MatchCaseToggle.ShouldNotBeNull().RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();
        state.MatchCase.ShouldBeTrue();
        widget.MatchCaseToggle!.IsOn.ShouldBeTrue();
        state.MatchesCount.ShouldBe(2);
        widget.FindInput!.IsFocused.ShouldBeTrue();

        // the keybinding (Alt+W, Cmd+Alt+W on macOS) toggles whole word, and the button shows it
        window.KeyPressQwerty(PhysicalKey.W, s_mac ? RawInputModifiers.Meta | RawInputModifiers.Alt : RawInputModifiers.Alt);
        Dispatcher.UIThread.RunJobs();
        state.WholeWord.ShouldBeTrue();
        widget.WholeWordToggle!.IsOn.ShouldBeTrue();

        // regex from the state
        Controller(editor).ToggleRegex();
        widget.RegexToggle!.IsOn.ShouldBeTrue();
    }

    [AvaloniaFact]
    public void No_results_marks_the_count_and_disables_previous_and_next()
    {
        var (editor, window) = Show();
        OpenFind(window);
        Type(window, "absent");

        var widget = Widget(editor);
        widget.MatchesCountText.ShouldBe("No results");
        widget.Classes.Contains(CodeEditorFindWidget.NoResultsPseudoClass).ShouldBeTrue();
        widget.FindInput!.IsInvalid.ShouldBeTrue();
        widget.NextButton!.IsEnabled.ShouldBeFalse();
        widget.PreviousButton!.IsEnabled.ShouldBeFalse();

        widget.FindInput.Text = "say";
        Dispatcher.UIThread.RunJobs();
        widget.Classes.Contains(CodeEditorFindWidget.NoResultsPseudoClass).ShouldBeFalse();
        widget.NextButton.IsEnabled.ShouldBeTrue();
    }

    [AvaloniaFact]
    public void The_buttons_go_to_matches_and_close()
    {
        var (editor, window) = Show();
        OpenFind(window);
        Type(window, "hello");
        var widget = Widget(editor);

        widget.NextButton!.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();
        Sel(editor).ShouldBe([2, 5, 2, 10]);

        widget.PreviousButton!.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();
        Sel(editor).ShouldBe([1, 1, 1, 6]);

        widget.CloseButton!.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();
        widget.IsVisible.ShouldBeFalse();
    }

    [AvaloniaFact]
    public void Find_in_selection_is_enabled_by_a_selection_and_limits_the_search_to_it()
    {
        var (editor, window) = Show();
        OpenFind(window);
        Type(window, "hello");
        var widget = Widget(editor);
        editor.SetSelection(new TextSelection(1, 1, 1, 1));
        Dispatcher.UIThread.RunJobs();
        widget.FindInSelectionButton!.IsEnabled.ShouldBeFalse();

        editor.SetSelection(new TextSelection(2, 1, 3, 12));
        Dispatcher.UIThread.RunJobs();
        widget.FindInSelectionButton.IsEnabled.ShouldBeTrue();

        widget.FindInSelectionButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();
        var state = Controller(editor).State;
        state.SearchScope.ShouldNotBeNull().Single().ShouldBe(new TextRange(2, 1, 3, 12));
        state.MatchesCount.ShouldBe(2);
        widget.FindInSelectionButton.IsOn.ShouldBeTrue();

        // the scope is decorated
        var view = TestHelpers.Descendant<CodeEditorView>(editor);
        view.GetDecorationRects(1, 4).ShouldContain(r => r.ClassName.Contains("findScope", StringComparison.Ordinal));
    }

    [AvaloniaFact]
    public void The_widget_makes_room_above_the_first_line_while_it_shows()
    {
        var (editor, window) = Show();
        OpenFind(window);
        Render(window);

        var widget = Widget(editor);
        widget.ViewZoneId.ShouldNotBeNull();

        // a zone as high as the widget (with its margin) above line 1
        var zoneHeight = editor.GetTopForLineNumber(1);
        zoneHeight.ShouldBe(widget.Bounds.Height + widget.Margin.Top, 0.5);

        // the text did not move: the view scrolled down by the zone's height, and scrolling up shows line 1 below the widget
        editor.ViewModel!.ViewLayout.GetCurrentScrollTop().ShouldBe(zoneHeight, 0.5);

        window.KeyPressQwerty(PhysicalKey.Escape, RawInputModifiers.None);
        Render(window);
        widget.ViewZoneId.ShouldBeNull();
        editor.GetTopForLineNumber(1).ShouldBe(0);
        editor.ViewModel!.ViewLayout.GetCurrentScrollTop().ShouldBe(0, 0.5);
    }

    [AvaloniaFact]
    public void A_narrow_editor_hides_the_match_count()
    {
        var (editor, window) = Show(width: 360);
        OpenFind(window);
        Render(window);

        var widget = Widget(editor);
        widget.Classes.Contains(CodeEditorFindWidget.NarrowPseudoClass).ShouldBeTrue();
        widget.Bounds.Width.ShouldBeLessThanOrEqualTo(360);
        widget.Bounds.Left.ShouldBeGreaterThanOrEqualTo(0);
    }

    [AvaloniaFact]
    public void A_read_only_editor_has_no_replace()
    {
        var (editor, window) = Show();
        editor.IsReadOnly = true;
        OpenReplace(window);
        Render(window);

        // StartFindReplaceAction does nothing in a read-only editor; Ctrl+F still works, without replace
        Controller(editor).State.IsRevealed.ShouldBeFalse();
        OpenFind(window);
        Render(window);
        var widget = Widget(editor);
        widget.IsVisible.ShouldBeTrue();
        widget.ToggleReplaceButton!.IsEnabled.ShouldBeFalse();
        widget.IsReplaceVisible.ShouldBeFalse();
    }

    [AvaloniaFact]
    public void The_toggle_replace_button_shows_and_hides_the_replace_row()
    {
        var (editor, window) = Show();
        OpenFind(window);
        Render(window);
        var widget = Widget(editor);
        widget.IsReplaceVisible.ShouldBeFalse();

        widget.ToggleReplaceButton!.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Render(window);
        widget.IsReplaceVisible.ShouldBeTrue();
        widget.Classes.Contains(CodeEditorFindWidget.ReplacePseudoClass).ShouldBeTrue();
        widget.ReplaceInput!.IsFocused.ShouldBeTrue();

        widget.ToggleReplaceButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Render(window);
        widget.IsReplaceVisible.ShouldBeFalse();
        widget.ReplaceInput.IsEffectivelyVisible.ShouldBeFalse();
    }

    [AvaloniaFact]
    public void The_find_actions_are_registered_and_leaving_the_tree_disposes_the_widget()
    {
        var (editor, window) = Show();
        editor.GetAction(FindIds.StartFindAction).ShouldNotBeNull().Label.ShouldBe("Find");
        editor.GetAction(FindIds.StartFindReplaceAction).ShouldNotBeNull();
        editor.GetAction(FindIds.NextMatchFindAction).ShouldNotBeNull();

        OpenFind(window);
        Type(window, "hello");
        var widget = Widget(editor);

        window.Content = null;
        Dispatcher.UIThread.RunJobs();
        widget.Parent.ShouldBeNull();
        editor.Model.GetAllDecorations(editor.EditorId).Where(d => d.Options.Description.Contains("find", StringComparison.Ordinal)).ShouldBeEmpty();
    }
}
