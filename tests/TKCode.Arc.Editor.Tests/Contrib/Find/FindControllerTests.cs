// Ported from vscode (src/vs/editor/contrib/find/test/browser/findController.test.ts, commit b57d5c9e), MIT license.
// Every upstream test that runs (the three clipboard tests upstream comments out are not ported): 6 + 17 cases. The
// controller is upstream's TestFindController (a CommonFindController that records focus and sets findInputFocussed);
// running an action is calling its body in FindActions (executeAction / editor.runAction upstream). The query option
// persistence suite shares one storage upstream, whose suite-level stores all run before the first test; each test
// here starts from the values that storage holds when it runs.

using Shouldly;
using TKCode.Arc.Editor.Contrib.Find;
using TKCode.Arc.Editor.Model;
using TKCode.Arc.Editor.Platform;
using Xunit;

namespace TKCode.Arc.Editor.Tests;

internal sealed class TestFindController : CommonFindController
{
    private readonly IContextKey<bool> _findInputFocused;

    public TestFindController(IFindEditor editor)
        : base(editor)
    {
        _findInputFocused = ContextFindInputFocused.BindTo(editor.ContextKeyService);
    }

    public bool HasFocus { get; set; }

    protected override async Task StartCore(FindStartOptions opts, NewFindReplaceState? newState)
    {
        await base.StartCore(opts, newState);

        if (opts.ShouldFocus != FindStartFocusAction.NoFocusChange)
        {
            HasFocus = true;
        }

        var inputFocused = opts.ShouldFocus == FindStartFocusAction.FocusFindInput;
        _findInputFocused.Set(inputFocused);
    }
}

public sealed class FindControllerTests
{
    private static int[] FromSelection(TextSelection slc) => [slc.StartLineNumber, slc.StartColumn, slc.EndLineNumber, slc.EndColumn];

    private static FindStartOptions StartOptions(FindStartFocusAction shouldFocus, bool updateSearchScope = false) => new()
    {
        ForceRevealReplace = false,
        SeedSearchStringFromSelection = SeedSearchStringMode.None,
        SeedSearchStringFromNonEmptySelection = false,
        SeedSearchStringFromGlobalClipboard = false,
        ShouldFocus = shouldFocus,
        ShouldAnimate = false,
        UpdateSearchScope = updateSearchScope,
        Loop = true,
    };

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("explicit")]
    public async Task Issue_151311_findWithArgs_respects_searchString(string? searchString)
    {
        await TestFindEditor.WithAsync(["needle"], async editor =>
        {
            using var controller = new TestFindController(editor);
            editor.SetSelection(new TextSelection(1, 1, 1, 7));
            for (var i = 0; i < 3; i++)
            {
                await FindActions.StartFindWithArgs(controller, new FindStartArguments { SearchString = searchString, IsRegex = true });
                controller.GetState().SearchString.ShouldBe(searchString ?? "needle");
            }
        }, EditorFindOptions.Default with { GlobalFindClipboard = false });
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("explicit")]
    public async Task Issue_151311_findWithArgs_respects_clipboard_searchString(string? searchString)
    {
        var clipboard = new TestFindClipboard();
        await TestFindEditor.WithAsync(["needle"], async editor =>
        {
            using var controller = new TestFindController(editor);
            for (var i = 0; i < 3; i++)
            {
                clipboard.WriteFindText("clipboard");
                await FindActions.StartFindWithArgs(controller, new FindStartArguments { SearchString = searchString, IsRegex = true });
                controller.GetState().SearchString.ShouldBe(searchString ?? "clipboard");
            }
        }, EditorFindOptions.Default with { GlobalFindClipboard = true, SeedSearchStringFromSelection = SeedSearchStringFromSelection.Never }, clipboard: clipboard);
    }

    [Fact]
    public async Task Issue_1857_F3_find_next_acts_like_find_under_cursor()
    {
        await TestFindEditor.WithAsync(["ABC", "ABC", "XYZ", "ABC"], async editor =>
        {
            // The cursor is at the very top, of the file, at the first ABC
            using var findController = new TestFindController(editor);
            var findState = findController.GetState();

            // I hit Ctrl+F to show the Find dialog
            await FindActions.StartFind(findController);

            // I type ABC.
            findState.Change(new NewFindReplaceState { SearchString = "A" }, true);
            findState.Change(new NewFindReplaceState { SearchString = "AB" }, true);
            findState.Change(new NewFindReplaceState { SearchString = "ABC" }, true);

            // The first ABC is highlighted.
            FromSelection(editor.GetSelection()).ShouldBe([1, 1, 1, 4]);

            // I hit Esc to exit the Find dialog.
            findController.CloseFindWidget();
            findController.HasFocus = false;

            // The cursor is now at end of the first line, with ABC on that line highlighted.
            FromSelection(editor.GetSelection()).ShouldBe([1, 1, 1, 4]);

            // I hit delete to remove it and change the text to XYZ.
            editor.PushUndoStop();
            editor.Inner.ExecuteEdits("test", [EditOperation.Delete(new TextRange(1, 1, 1, 4))]);
            editor.Inner.ExecuteEdits("test", [EditOperation.Insert(new TextPosition(1, 1), "XYZ")]);
            editor.PushUndoStop();

            // At this point the text editor looks like this:
            //   XYZ
            //   ABC
            //   XYZ
            //   ABC
            editor.GetModel()!.GetLineContent(1).ShouldBe("XYZ");

            // The cursor is at end of the first line.
            FromSelection(editor.GetSelection()).ShouldBe([1, 4, 1, 4]);

            // I hit F3 to "Find Next" to find the next occurrence of ABC, but instead it searches for XYZ.
            await FindActions.MatchFind(findController, true);

            findState.SearchString.ShouldBe("ABC");
            findController.HasFocus.ShouldBeFalse();
        });
    }

    [Fact]
    public async Task Issue_3090_F3_does_not_loop_with_two_matches_on_a_single_line()
    {
        await TestFindEditor.WithAsync(["import nls = require('vs/nls');"], async editor =>
        {
            using var findController = new TestFindController(editor);

            editor.SetPosition(new TextPosition(1, 9));

            await FindActions.MatchFind(findController, true);
            FromSelection(editor.GetSelection()).ShouldBe([1, 26, 1, 29]);

            await FindActions.MatchFind(findController, true);
            FromSelection(editor.GetSelection()).ShouldBe([1, 8, 1, 11]);
        });
    }

    [Fact]
    public async Task Issue_6149_auto_escape_highlighted_text_for_search_and_replace_regex_mode()
    {
        await TestFindEditor.WithAsync(["var x = (3 * 5)", "var y = (3 * 5)", "var z = (3  * 5)"], async editor =>
        {
            using var findController = new TestFindController(editor);

            editor.SetSelection(new TextSelection(1, 9, 1, 13));

            findController.ToggleRegex();
            await FindActions.StartFind(findController);

            await FindActions.MatchFind(findController, true);
            FromSelection(editor.GetSelection()).ShouldBe([2, 9, 2, 13]);

            await FindActions.MatchFind(findController, true);
            FromSelection(editor.GetSelection()).ShouldBe([1, 9, 1, 13]);
        });
    }

    [Fact]
    public async Task Issue_41027_do_not_replace_find_input_value_on_replace_action_if_find_input_is_active()
    {
        // Upstream's suite shares one storage service, and the test before this one (#6149) leaves isRegex stored as
        // true, so toggleRegex here turns regex off and setSearchString keeps the string as typed. The storage is
        // seeded the same way here, so the test does not depend on the order the tests run in.
        var storage = new InMemoryFindStorage();
        storage.Store("editor.isRegex", true);
        await TestFindEditor.WithAsync(["test"], async editor =>
        {
            const string testRegexString = "tes.";
            using var findController = new TestFindController(editor);

            findController.ToggleRegex();
            findController.SetSearchString(testRegexString);
            await findController.Start(StartOptions(FindStartFocusAction.FocusFindInput));
            await FindActions.MatchFind(findController, true);
            await FindActions.StartFindReplace(findController);

            findController.GetState().SearchString.ShouldBe(testRegexString);
        }, storage: storage);
    }

    [Fact]
    public async Task CloseOnResult_closes_find_widget_when_a_match_is_found_from_explicit_navigation()
    {
        await TestFindEditor.WithAsync(["ABC", "ABC", "XYZ"], async editor =>
        {
            using var findController = new TestFindController(editor);
            var findState = findController.GetState();

            await FindActions.StartFind(findController);
            findState.IsRevealed.ShouldBeTrue();

            findState.Change(new NewFindReplaceState { SearchString = "ABC" }, true);
            await FindActions.MatchFind(findController, true);

            findState.IsRevealed.ShouldBeFalse();
        }, EditorFindOptions.Default with { CloseOnResult = true });
    }

    [Fact]
    public async Task CloseOnResult_keeps_find_widget_open_when_no_match_is_found()
    {
        await TestFindEditor.WithAsync(["ABC", "DEF", "XYZ"], async editor =>
        {
            using var findController = new TestFindController(editor);
            var findState = findController.GetState();

            await FindActions.StartFind(findController);
            findState.IsRevealed.ShouldBeTrue();

            findState.Change(new NewFindReplaceState { SearchString = "NO_MATCH" }, true);
            await FindActions.MatchFind(findController, true);

            findState.MatchesCount.ShouldBe(0);
            findState.IsRevealed.ShouldBeTrue();
        }, EditorFindOptions.Default with { CloseOnResult = true });
    }

    [Fact]
    public async Task CloseOnResult_disabled_keeps_find_widget_open_after_navigation()
    {
        await TestFindEditor.WithAsync(["ABC", "ABC", "XYZ"], async editor =>
        {
            using var findController = new TestFindController(editor);
            var findState = findController.GetState();

            await FindActions.StartFind(findController);
            findState.IsRevealed.ShouldBeTrue();

            findState.Change(new NewFindReplaceState { SearchString = "ABC" }, true);
            await FindActions.MatchFind(findController, true);

            findState.IsRevealed.ShouldBeTrue();
        }, EditorFindOptions.Default with { CloseOnResult = false });
    }

    [Fact]
    public async Task Issue_9043_clear_search_scope_when_find_widget_is_hidden()
    {
        await TestFindEditor.WithAsync(["var x = (3 * 5)", "var y = (3 * 5)", "var z = (3 * 5)"], async editor =>
        {
            using var findController = new TestFindController(editor);
            await findController.Start(StartOptions(FindStartFocusAction.NoFocusChange));

            findController.GetState().SearchScope.ShouldBeNull();

            findController.GetState().Change(new NewFindReplaceState { SearchScope = [new TextRange(1, 1, 1, 5)] }, false);

            findController.GetState().SearchScope.ShouldBe([new TextRange(1, 1, 1, 5)]);

            findController.CloseFindWidget();
            findController.GetState().SearchScope.ShouldBeNull();
        });
    }

    [Fact]
    public async Task Issue_18111_regex_replace_with_single_space_replaces_with_no_space()
    {
        await TestFindEditor.WithAsync(["HRESULT OnAmbientPropertyChange(DISPID   dispid);"], async editor =>
        {
            using var findController = new TestFindController(editor);

            await FindActions.StartFind(findController);

            findController.GetState().Change(new NewFindReplaceState { SearchString = "\\b\\s{3}\\b", ReplaceString = " ", IsRegex = true }, false);
            findController.MoveToNextMatch();

            editor.GetSelections().Select(FromSelection).ShouldBe([[1, 39, 1, 42]]);

            findController.Replace();

            editor.GetValue().ShouldBe("HRESULT OnAmbientPropertyChange(DISPID dispid);");
        });
    }

    [Fact]
    public async Task Issue_24714_regular_expression_with_caret_in_search_and_replace()
    {
        await TestFindEditor.WithAsync(["", "line2", "line3"], async editor =>
        {
            using var findController = new TestFindController(editor);

            await FindActions.StartFind(findController);

            findController.GetState().Change(new NewFindReplaceState { SearchString = "^", ReplaceString = "x", IsRegex = true }, false);
            findController.MoveToNextMatch();

            editor.GetSelections().Select(FromSelection).ShouldBe([[2, 1, 2, 1]]);

            findController.Replace();

            editor.GetValue().ShouldBe("\nxline2\nline3");
        });
    }

    [Fact]
    public async Task Issue_38232_find_next_selection_regex_enabled()
    {
        await TestFindEditor.WithAsync(["([funny]", "", "([funny]"], async editor =>
        {
            using var findController = new TestFindController(editor);

            // toggle regex
            findController.GetState().Change(new NewFindReplaceState { IsRegex = true }, false);

            // change selection
            editor.SetSelection(new TextSelection(1, 1, 1, 9));

            // cmd+f3
            await FindActions.SelectionMatchFind(findController, true);

            editor.GetSelections().Select(FromSelection).ShouldBe([[3, 1, 3, 9]]);
        });
    }

    [Fact]
    public async Task Issue_38232_find_next_selection_regex_enabled_find_widget_open()
    {
        await TestFindEditor.WithAsync(["([funny]", "", "([funny]"], async editor =>
        {
            using var findController = new TestFindController(editor);

            // cmd+f - open find widget
            await FindActions.StartFind(findController);

            // toggle regex
            findController.GetState().Change(new NewFindReplaceState { IsRegex = true }, false);

            // change selection
            editor.SetSelection(new TextSelection(1, 1, 1, 9));

            // cmd+f3
            await FindActions.SelectionMatchFind(findController, true);

            editor.GetSelections().Select(FromSelection).ShouldBe([[3, 1, 3, 9]]);
        });
    }

    [Fact]
    public async Task Issue_47400_cmd_e_supports_feeding_multiple_lines_of_text_into_the_find_widget()
    {
        await TestFindEditor.WithAsync(["ABC", "ABC", "XYZ", "ABC", "ABC"], async editor =>
        {
            using var findController = new TestFindController(editor);

            // change selection
            editor.SetSelection(new TextSelection(1, 1, 1, 1));

            // cmd+f - open find widget
            await FindActions.StartFind(findController);

            editor.SetSelection(new TextSelection(1, 1, 2, 4));
            await FindActions.StartFindWithSelection(findController);
            var findState = findController.GetState();

            findState.SearchString.Split(["\r\n", "\r", "\n"], StringSplitOptions.None).ShouldBe(["ABC", "ABC"]);

            editor.SetSelection(new TextSelection(3, 1, 3, 1));
            await FindActions.StartFindWithSelection(findController);
        });
    }

    [Fact]
    public async Task Issue_109756_cmd_e_with_empty_cursor_should_always_work()
    {
        await TestFindEditor.WithAsync(["ABC", "ABC", "XYZ", "ABC", "ABC"], editor =>
        {
            using var findController = new TestFindController(editor);
            editor.SetSelection(new TextSelection(1, 2, 1, 2));

            _ = FindActions.StartFindWithSelection(findController);

            findController.GetState().SearchString.ShouldBe("ABC");
            return Task.CompletedTask;
        });
    }

    // ------ FindController query options persistence

    private static InMemoryFindStorage Storage(bool isRegex, bool matchCase, bool wholeWord)
    {
        var storage = new InMemoryFindStorage();
        storage.Store("editor.isRegex", isRegex);
        storage.Store("editor.matchCase", matchCase);
        storage.Store("editor.wholeWord", wholeWord);
        return storage;
    }

    [Fact]
    public async Task Persistence_matchCase()
    {
        var storage = Storage(isRegex: false, matchCase: false, wholeWord: true);
        await TestFindEditor.WithAsync(["abc", "ABC", "XYZ", "ABC"], async editor =>
        {
            storage.Store("editor.matchCase", true);
            // The cursor is at the very top, of the file, at the first ABC
            using var findController = new TestFindController(editor);
            var findState = findController.GetState();

            // I hit Ctrl+F to show the Find dialog
            await FindActions.StartFind(findController);

            // I type ABC.
            findState.Change(new NewFindReplaceState { SearchString = "ABC" }, true);
            // The second ABC is highlighted as matchCase is true.
            FromSelection(editor.GetSelection()).ShouldBe([2, 1, 2, 4]);
        }, storage: storage);
    }

    [Fact]
    public async Task Persistence_wholeWord()
    {
        var storage = Storage(isRegex: false, matchCase: true, wholeWord: true);
        await TestFindEditor.WithAsync(["ABC", "AB", "XYZ", "ABC"], async editor =>
        {
            // The cursor is at the very top, of the file, at the first ABC
            using var findController = new TestFindController(editor);
            var findState = findController.GetState();

            // I hit Ctrl+F to show the Find dialog
            await FindActions.StartFind(findController);

            // I type AB.
            findState.Change(new NewFindReplaceState { SearchString = "AB" }, true);
            // The second AB is highlighted as wholeWord is true.
            FromSelection(editor.GetSelection()).ShouldBe([2, 1, 2, 3]);
        }, storage: storage);
    }

    [Fact]
    public async Task Persistence_toggling_options_is_saved()
    {
        var storage = Storage(isRegex: false, matchCase: true, wholeWord: true);
        await TestFindEditor.WithAsync(["ABC", "AB", "XYZ", "ABC"], editor =>
        {
            // The cursor is at the very top, of the file, at the first ABC
            using var findController = new TestFindController(editor);
            findController.ToggleRegex();
            storage.GetBoolean("editor.isRegex", false).ShouldBeTrue();
            return Task.CompletedTask;
        }, storage: storage);
    }

    [Fact]
    public async Task Issue_27083_update_search_scope_once_find_widget_becomes_visible()
    {
        await TestFindEditor.WithAsync(["var x = (3 * 5)", "var y = (3 * 5)", "var z = (3 * 5)"], async editor =>
        {
            using var findController = new TestFindController(editor);
            var findConfig = StartOptions(FindStartFocusAction.NoFocusChange, updateSearchScope: true);

            editor.SetSelection(new TextRange(1, 1, 2, 1));
            await findController.Start(findConfig);
            findController.GetState().SearchScope.ShouldBe([new TextRange(1, 1, 2, 1)]);

            findController.CloseFindWidget();

            editor.SetSelections([new TextSelection(1, 1, 2, 1), new TextSelection(2, 1, 2, 5)]);
            await findController.Start(findConfig);
            findController.GetState().SearchScope.ShouldBe([new TextRange(1, 1, 2, 1), new TextRange(2, 1, 2, 5)]);
        }, EditorFindOptions.Default with { AutoFindInSelection = AutoFindInSelection.Always, GlobalFindClipboard = false });
    }

    [Fact]
    public async Task Issue_58604_do_not_update_searchScope_if_it_is_empty()
    {
        await TestFindEditor.WithAsync(["var x = (3 * 5)", "var y = (3 * 5)", "var z = (3 * 5)"], async editor =>
        {
            editor.SetSelection(new TextRange(1, 2, 1, 2));
            using var findController = new TestFindController(editor);

            await findController.Start(StartOptions(FindStartFocusAction.NoFocusChange, updateSearchScope: true));

            findController.GetState().SearchScope.ShouldBeNull();
        }, EditorFindOptions.Default with { AutoFindInSelection = AutoFindInSelection.Always, GlobalFindClipboard = false });
    }

    [Fact]
    public async Task Issue_58604_update_searchScope_if_it_is_not_empty()
    {
        await TestFindEditor.WithAsync(["var x = (3 * 5)", "var y = (3 * 5)", "var z = (3 * 5)"], async editor =>
        {
            editor.SetSelection(new TextRange(1, 2, 1, 3));
            using var findController = new TestFindController(editor);

            await findController.Start(StartOptions(FindStartFocusAction.NoFocusChange, updateSearchScope: true));

            findController.GetState().SearchScope.ShouldBe([new TextRange(1, 2, 1, 3)]);
        }, EditorFindOptions.Default with { AutoFindInSelection = AutoFindInSelection.Always, GlobalFindClipboard = false });
    }

    [Fact]
    public async Task Issue_27083_find_in_selection_when_multiple_lines_are_selected()
    {
        await TestFindEditor.WithAsync(["var x = (3 * 5)", "var y = (3 * 5)", "var z = (3 * 5)"], async editor =>
        {
            editor.SetSelection(new TextRange(1, 6, 2, 1));
            using var findController = new TestFindController(editor);

            await findController.Start(StartOptions(FindStartFocusAction.NoFocusChange, updateSearchScope: true));

            findController.GetState().SearchScope.ShouldBe([new TextRange(1, 6, 2, 1)]);
        }, EditorFindOptions.Default with { AutoFindInSelection = AutoFindInSelection.Multiline, GlobalFindClipboard = false });
    }
}
