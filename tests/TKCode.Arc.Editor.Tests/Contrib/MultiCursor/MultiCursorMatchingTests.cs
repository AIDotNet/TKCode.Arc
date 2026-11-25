// Ported from vscode (src/vs/editor/contrib/multicursor/test/browser/multicursor.test.ts: the suites 'Selected text
// occurrence matching', 'Selected text occurrence matching highlights' and 'Find state disassociation', commit
// b57d5c9e), MIT license. See MultiCursorTests.cs for the differences.

using Shouldly;
using TKCode.Arc.Editor.Contrib.MultiCursor;
using TKCode.Arc.Editor.Model;
using Xunit;

namespace TKCode.Arc.Editor.Tests;

public sealed class MultiCursorMatchingTests
{
    private static readonly string[] s_text = ["foo", "FOObar", "foobar", "FOO", "foo"];

    private static TextSelection Sel(int a, int b, int c, int d) => new(a, b, c, d);

    private static string F(IEnumerable<TextSelection> selections) => string.Join(" ", selections.Select(static s => $"[{s.StartLineNumber},{s.StartColumn},{s.EndLineNumber},{s.EndColumn}]"));

    private static string F(IEnumerable<TextRange> ranges) => string.Join(" ", ranges.Select(static s => $"[{s.StartLineNumber},{s.StartColumn},{s.EndLineNumber},{s.EndColumn}]"));

    private static string L(params int[][] ranges) => string.Join(" ", ranges.Select(static r => $"[{r[0]},{r[1]},{r[2]},{r[3]}]"));

    private static string Lines(params int[] lines) => string.Join(" ", lines.Select(static l => $"[{l},1,{l},4]"));

    private static MultiCursorOptions Mode(SelectedTextMatchMode mode) => MultiCursorOptions.Default with { SelectedTextMatchMode = mode };

    private static void TestMulticursor(string[] text, Action<TestMultiCursorEditor, StandaloneMultiCursorFindController> callback, MultiCursorOptions? options = null, bool hasTextFocus = true)
    {
        using var editor = new TestMultiCursorEditor(TestCodeEditor.Create(text), options, hasTextFocus);
        callback(editor, editor.FindControllerImpl);
    }

    public enum Action5
    {
        AddNext,
        AddPrevious,
        MoveNext,
        MovePrevious,
        SelectHighlights,
    }

    private static void Run(Action5 action, IMultiCursorEditor editor)
    {
        switch (action)
        {
            case Action5.AddNext:
                MultiCursorActions.AddSelectionToNextFindMatch(editor);
                break;
            case Action5.AddPrevious:
                MultiCursorActions.AddSelectionToPreviousFindMatch(editor);
                break;
            case Action5.MoveNext:
                MultiCursorActions.MoveSelectionToNextFindMatch(editor);
                break;
            case Action5.MovePrevious:
                MultiCursorActions.MoveSelectionToPreviousFindMatch(editor);
                break;
            default:
                MultiCursorActions.SelectHighlights(editor);
                break;
        }
    }

    private static void AssertSelectedText(Action5 action, int startLine, int[] expectedLines, SelectedTextMatchMode mode, bool isRevealed)
    {
        TestMulticursor(s_text, (editor, findController) =>
        {
            var state = findController.GetState();
            var matchCase = mode == SelectedTextMatchMode.CaseSensitive;
            state.Change(new NewFindState { SearchString = "FOO.*", IsRevealed = isRevealed, MatchCase = !matchCase, WholeWord = true, IsRegex = true }, false);
            editor.SetSelection(Sel(startLine, 1, startLine, 4));

            Run(action, editor);

            F(editor.GetSelections()).ShouldBe(Lines(expectedLines));
            new[] { state.MatchCase, state.WholeWord, state.IsRegex }.ShouldBe([!matchCase, true, true]);
        }, Mode(mode));
    }

    private static void AssertEmptySelection(Action5 action, int startLine, int[] expectedLines, SelectedTextMatchMode mode)
    {
        TestMulticursor(s_text, (editor, findController) =>
        {
            var state = findController.GetState();
            state.Change(new NewFindState { MatchCase = false, WholeWord = false, IsRegex = true }, false);
            editor.SetSelection(Sel(startLine, 2, startLine, 2));

            Run(action, editor);
            Run(action, editor);

            F(editor.GetSelections()).ShouldBe(Lines(expectedLines));
            new[] { state.MatchCase, state.WholeWord, state.IsRegex }.ShouldBe([true, true, false]);
        }, Mode(mode));
    }

    private static void AssertFindOptionsFeedback(Action5 action, int startLine, int[] expectedLines, bool hasTextFocus)
    {
        TestMulticursor(s_text, (editor, findController) =>
        {
            var state = findController.GetState();
            state.Change(new NewFindState { SearchString = "foo", IsRevealed = !hasTextFocus, MatchCase = true, WholeWord = true, IsRegex = true }, false);
            editor.SetSelection(Sel(startLine, 1, startLine, 4));
            var before = findController.HighlightFindOptionsCount;

            Run(action, editor);

            F(editor.GetSelections()).ShouldBe(Lines(expectedLines));
            new[] { state.MatchCase, state.WholeWord, state.IsRegex }.ShouldBe([true, true, true]);
            (findController.HighlightFindOptionsCount - before).ShouldBe(hasTextFocus ? 0 : 1);
        }, Mode(SelectedTextMatchMode.CaseInsensitive), hasTextFocus);
    }

    [Fact]
    public void Defaults_To_Find_And_Reports_Live_Option_Changes()
    {
        TestMulticursor(s_text, (editor, _) =>
        {
            var values = new List<SelectedTextMatchMode> { editor.MultiCursorOptions.SelectedTextMatchMode };
            var changes = new List<bool>();
            editor.DidChangeConfiguration += option => changes.Add(option == MultiCursorOptionNames.SelectedTextMatchMode);
            foreach (var mode in new[] { SelectedTextMatchMode.CaseSensitive, SelectedTextMatchMode.CaseInsensitive })
            {
                editor.UpdateOptions(Mode(mode));
                values.Add(editor.MultiCursorOptions.SelectedTextMatchMode);
            }

            values.ShouldBe([SelectedTextMatchMode.FindOptions, SelectedTextMatchMode.CaseSensitive, SelectedTextMatchMode.CaseInsensitive]);
            changes.ShouldBe([true, true]);
        });
    }

    [Fact]
    public void Default_Selected_Text_Matching_Uses_Find_Match_Case_And_Whole_Word_Options()
    {
        var actual = new List<string>();
        foreach (var (matchCase, wholeWord) in new[] { (false, false), (false, true), (true, false), (true, true) })
        {
            TestMulticursor(s_text, (editor, findController) =>
            {
                findController.GetState().Change(new NewFindState { MatchCase = matchCase, WholeWord = wholeWord }, false);
                editor.SetSelection(Sel(1, 1, 1, 4));
                MultiCursorActions.AddSelectionToNextFindMatch(editor);
                actual.Add(F(editor.GetSelections()));
            });
        }

        actual.ShouldBe([Lines(1, 2), Lines(1, 4), Lines(1, 3), Lines(1, 5)]);
    }

    [Theory]
    [InlineData(Action5.AddNext, 1, new[] { 1, 2 }, false, false)]
    [InlineData(Action5.AddNext, 1, new[] { 1, 2 }, false, true)]
    [InlineData(Action5.AddNext, 1, new[] { 1, 3 }, true, false)]
    [InlineData(Action5.AddNext, 1, new[] { 1, 3 }, true, true)]
    [InlineData(Action5.AddPrevious, 5, new[] { 5, 4 }, false, false)]
    [InlineData(Action5.AddPrevious, 5, new[] { 5, 4 }, false, true)]
    [InlineData(Action5.AddPrevious, 5, new[] { 5, 3 }, true, false)]
    [InlineData(Action5.AddPrevious, 5, new[] { 5, 3 }, true, true)]
    [InlineData(Action5.MoveNext, 1, new[] { 2 }, false, false)]
    [InlineData(Action5.MoveNext, 1, new[] { 2 }, false, true)]
    [InlineData(Action5.MoveNext, 1, new[] { 3 }, true, false)]
    [InlineData(Action5.MoveNext, 1, new[] { 3 }, true, true)]
    [InlineData(Action5.MovePrevious, 5, new[] { 4 }, false, false)]
    [InlineData(Action5.MovePrevious, 5, new[] { 4 }, false, true)]
    [InlineData(Action5.MovePrevious, 5, new[] { 3 }, true, false)]
    [InlineData(Action5.MovePrevious, 5, new[] { 3 }, true, true)]
    [InlineData(Action5.SelectHighlights, 1, new[] { 1, 2, 3, 4, 5 }, false, false)]
    [InlineData(Action5.SelectHighlights, 1, new[] { 1, 2, 3, 4, 5 }, false, true)]
    [InlineData(Action5.SelectHighlights, 1, new[] { 1, 3, 5 }, true, false)]
    [InlineData(Action5.SelectHighlights, 1, new[] { 1, 3, 5 }, true, true)]
    public void Selected_Text_Match_Case_And_Find_Visible(Action5 action, int startLine, int[] expectedLines, bool matchCase, bool findVisible) =>
        AssertSelectedText(action, startLine, expectedLines, matchCase ? SelectedTextMatchMode.CaseSensitive : SelectedTextMatchMode.CaseInsensitive, findVisible);

    [Theory]
    [InlineData(Action5.AddNext, 1, new[] { 1, 5 }, false)]
    [InlineData(Action5.AddNext, 1, new[] { 1, 5 }, true)]
    [InlineData(Action5.AddPrevious, 5, new[] { 5, 1 }, false)]
    [InlineData(Action5.AddPrevious, 5, new[] { 5, 1 }, true)]
    [InlineData(Action5.MoveNext, 1, new[] { 5 }, false)]
    [InlineData(Action5.MoveNext, 1, new[] { 5 }, true)]
    [InlineData(Action5.MovePrevious, 5, new[] { 1 }, false)]
    [InlineData(Action5.MovePrevious, 5, new[] { 1 }, true)]
    [InlineData(Action5.SelectHighlights, 1, new[] { 1, 5 }, false)]
    [InlineData(Action5.SelectHighlights, 1, new[] { 1, 5 }, true)]
    public void Empty_Selection_Always_Starts_Whole_Word_And_Case_Sensitive(Action5 action, int startLine, int[] expectedLines, bool caseSensitiveSetting) =>
        AssertEmptySelection(action, startLine, expectedLines, caseSensitiveSetting ? SelectedTextMatchMode.CaseSensitive : SelectedTextMatchMode.CaseInsensitive);

    [Fact]
    public void Issue_18239_Replacing_Selected_Prefixes_Preserves_Differently_Cased_Identifiers()
    {
        TestMulticursor(["accountQryConn *grpc.ClientConn", "accountQryClient proto.AccountQueryClient"], (editor, findController) =>
        {
            findController.GetState().Change(new NewFindState { MatchCase = false, WholeWord = true }, false);
            editor.SetSelection(Sel(1, 1, 1, 8));
            MultiCursorActions.AddSelectionToNextFindMatch(editor);
            editor.Editor.Type("voucher", "test");

            editor.Editor.GetValue().ShouldBe("voucherQryConn *grpc.ClientConn\nvoucherQryClient proto.AccountQueryClient");
        }, Mode(SelectedTextMatchMode.CaseSensitive));
    }

    [Fact]
    public void Select_All_Replaces_Only_Selected_Text_Occurrences_While_Regex_Find_Is_Visible()
    {
        string? actual = null;
        TestMulticursor(s_text, (editor, findController) =>
        {
            findController.GetState().Change(new NewFindState { SearchString = "FOO.*", IsRevealed = true, MatchCase = false, WholeWord = true, IsRegex = true }, false);
            editor.SetSelection(Sel(1, 1, 1, 4));

            MultiCursorActions.SelectHighlights(editor);
            editor.Editor.Type("baz", "test");

            actual = editor.Editor.GetValue();
        }, Mode(SelectedTextMatchMode.CaseSensitive));

        actual.ShouldBe("baz\nFOObar\nbazbar\nFOO\nbaz");
    }

    [Theory]
    [InlineData(true, 3)]
    [InlineData(false, 2)]
    public void Changing_Find_Options_Ends_Caret_Started_Sessions_Before_Applying_The_Selected_Text_Matching_Setting(bool caseSensitive, int expectedLine)
    {
        var mode = caseSensitive ? SelectedTextMatchMode.CaseSensitive : SelectedTextMatchMode.CaseInsensitive;
        TestMulticursor(s_text, (editor, findController) =>
        {
            editor.SetSelection(Sel(1, 2, 1, 2));
            MultiCursorActions.AddSelectionToNextFindMatch(editor);
            findController.GetState().Change(new NewFindState { MatchCase = true, WholeWord = true }, false);
            findController.GetState().Change(new NewFindState { MatchCase = false, WholeWord = false }, false);
            MultiCursorActions.AddSelectionToNextFindMatch(editor);

            F(editor.GetSelections()).ShouldBe(Lines(1, expectedLine));
        }, Mode(mode));
    }

    [Fact]
    public void Mixed_Case_Initial_Selections_Are_Compared_Using_The_Selection_Setting_Not_Find()
    {
        TestMulticursor(["foo", "FOO", "fooBar"], (editor, findController) =>
        {
            findController.GetState().Change(new NewFindState { MatchCase = true }, false);
            editor.SetSelections([Sel(1, 1, 1, 4), Sel(2, 1, 2, 4)]);
            MultiCursorActions.AddSelectionToNextFindMatch(editor);

            F(editor.GetSelections()).ShouldBe(Lines(1, 2, 3));
        }, Mode(SelectedTextMatchMode.CaseInsensitive));
    }

    [Fact]
    public void Find_Focused_Mixed_Case_Initial_Selections_Respect_Find_Match_Case_False()
    {
        TestMulticursor(["foo", "FOO", "fooBar"], (editor, findController) =>
        {
            findController.GetState().Change(new NewFindState { SearchString = "foo", IsRevealed = true, MatchCase = false }, false);
            editor.SetSelections([Sel(1, 1, 1, 4), Sel(2, 1, 2, 4)]);
            MultiCursorActions.AddSelectionToNextFindMatch(editor);

            F(editor.GetSelections()).ShouldBe(Lines(1, 2, 3));
        }, Mode(SelectedTextMatchMode.CaseSensitive), hasTextFocus: false);
    }

    [Fact]
    public void Find_Focused_Mixed_Case_Initial_Selections_Respect_Find_Match_Case_True()
    {
        TestMulticursor(["foo", "FOO", "fooBar"], (editor, findController) =>
        {
            findController.GetState().Change(new NewFindState { SearchString = "foo", IsRevealed = true, MatchCase = true }, false);
            editor.SetSelections([Sel(1, 1, 1, 4), Sel(2, 1, 2, 4)]);
            MultiCursorActions.AddSelectionToNextFindMatch(editor);

            F(editor.GetSelections()).ShouldBe(Lines(1, 2));
        }, Mode(SelectedTextMatchMode.CaseInsensitive), hasTextFocus: false);
    }

    [Fact]
    public void Switching_Focus_Transfers_Matching_Rules_Between_Selection_And_Find_Sessions()
    {
        TestMulticursor(["foo", "FOO", "fooqux", "foobar", "bar", "BAR", "barista"], (editor, findController) =>
        {
            editor.SetSelection(Sel(1, 1, 1, 4));
            MultiCursorActions.MoveSelectionToNextFindMatch(editor);
            var actual = new List<string> { F(editor.GetSelections()) };

            findController.GetState().Change(new NewFindState { SearchString = "bar", IsRevealed = true, MatchCase = false, WholeWord = true }, false);
            editor.SetTextFocus(false);
            MultiCursorActions.MoveSelectionToNextFindMatch(editor);
            actual.Add(F(editor.GetSelections()));

            editor.SetTextFocus(true);
            MultiCursorActions.MoveSelectionToNextFindMatch(editor);
            actual.Add(F(editor.GetSelections()));

            actual.ShouldBe([Lines(3), Lines(5), Lines(7)]);
        }, Mode(SelectedTextMatchMode.CaseSensitive));
    }

    [Fact]
    public void The_Default_Find_Session_Survives_Returning_Focus_To_The_Editor()
    {
        TestMulticursor(["foo", "bar", "bar"], (editor, findController) =>
        {
            editor.SetTextFocus(false);
            editor.SetSelection(Sel(1, 1, 1, 4));
            findController.GetState().Change(new NewFindState { SearchString = "bar", IsRevealed = true }, false);
            MultiCursorActions.AddSelectionToNextFindMatch(editor);

            editor.SetTextFocus(true);
            MultiCursorActions.AddSelectionToNextFindMatch(editor);

            F(editor.GetSelections()).ShouldBe(Lines(1, 2, 3));
        });
    }

    [Fact]
    public void Multiline_CRLF_Selections_Match_Literal_Substrings_With_Match_Case_False()
    {
        TestMulticursor(["foo.", "bar", "FOO.", "BAR", "xfoo.", "bar"], (editor, findController) =>
        {
            editor.Model!.SetEOL(EndOfLineSequence.CRLF);
            findController.GetState().Change(new NewFindState { SearchString = "unrelated.*", IsRevealed = true, MatchCase = true, WholeWord = true, IsRegex = true }, false);
            editor.SetSelection(Sel(1, 1, 2, 4));
            MultiCursorActions.SelectHighlights(editor);

            F(editor.GetSelections()).ShouldBe(L([1, 1, 2, 4], [3, 1, 4, 4], [5, 2, 6, 4]));
        }, Mode(SelectedTextMatchMode.CaseInsensitive));
    }

    [Fact]
    public void Multiline_CRLF_Selections_Match_Literal_Substrings_With_Match_Case_True()
    {
        TestMulticursor(["foo.", "bar", "FOO.", "BAR", "xfoo.", "bar"], (editor, findController) =>
        {
            editor.Model!.SetEOL(EndOfLineSequence.CRLF);
            findController.GetState().Change(new NewFindState { SearchString = "unrelated.*", IsRevealed = true, MatchCase = false, WholeWord = true, IsRegex = true }, false);
            editor.SetSelection(Sel(1, 1, 2, 4));
            MultiCursorActions.SelectHighlights(editor);

            F(editor.GetSelections()).ShouldBe(L([1, 1, 2, 4], [5, 2, 6, 4]));
        }, Mode(SelectedTextMatchMode.CaseSensitive));
    }

    [Theory]
    [InlineData(Action5.AddNext, 1, new[] { 1, 2 })]
    [InlineData(Action5.AddPrevious, 5, new[] { 5, 3 })]
    [InlineData(Action5.SelectHighlights, 1, new[] { 2, 3 })]
    public void Find_Focused_Commands_Retain_Find_Matching_Rules(Action5 action, int startLine, int[] expectedLines)
    {
        TestMulticursor(["foo", "BAR", "bar", "barista", "foo"], (editor, findController) =>
        {
            editor.SetSelection(Sel(startLine, 1, startLine, 4));
            findController.GetState().Change(new NewFindState { SearchString = "bar", IsRevealed = true, MatchCase = false, WholeWord = true }, false);
            editor.UpdateOptions(Mode(SelectedTextMatchMode.CaseInsensitive));
            editor.UpdateOptions(Mode(SelectedTextMatchMode.CaseSensitive));
            Run(action, editor);

            F(editor.GetSelections()).ShouldBe(Lines(expectedLines));
        }, Mode(SelectedTextMatchMode.CaseSensitive), hasTextFocus: false);
    }

    [Theory]
    [InlineData(true, false, new[] { 1, 4, 5 })]
    [InlineData(false, true, new[] { 1, 5 })]
    public void Find_Focused_Regex_Select_All_Follows_Find_Options_In_Both_Independent_Matching_Modes(bool caseSensitiveMode, bool matchCase, int[] expectedLines)
    {
        var mode = caseSensitiveMode ? SelectedTextMatchMode.CaseSensitive : SelectedTextMatchMode.CaseInsensitive;
        TestMulticursor(s_text, (editor, findController) =>
        {
            findController.GetState().Change(new NewFindState { SearchString = "^foo$", IsRevealed = true, MatchCase = matchCase, IsRegex = true }, false);
            editor.SetSelection(Sel(1, 1, 1, 4));

            MultiCursorActions.SelectHighlights(editor);

            F(editor.GetSelections()).ShouldBe(Lines(expectedLines));
        }, Mode(mode), hasTextFocus: false);
    }

    [Fact]
    public void Add_Next_Occurrence_Does_Not_Highlight_Unrelated_Find_Options() => AssertFindOptionsFeedback(Action5.AddNext, 1, [1, 2], true);

    [Fact]
    public void Add_Previous_Occurrence_Does_Not_Highlight_Unrelated_Find_Options() => AssertFindOptionsFeedback(Action5.AddPrevious, 5, [5, 4], true);

    [Fact]
    public void Select_All_Occurrences_Does_Not_Highlight_Unrelated_Find_Options() => AssertFindOptionsFeedback(Action5.SelectHighlights, 1, [1, 2, 3, 4, 5], true);

    [Fact]
    public void Find_Focused_Occurrence_Selection_Still_Highlights_Find_Options() => AssertFindOptionsFeedback(Action5.AddNext, 1, [1, 5], false);

    // ---- suite 'Selected text occurrence matching highlights'

    private static readonly string[] s_highlightText = ["foo", "FOO", "fooBar", "FOOBar"];

    private static void AssertSelectionHighlighting(SelectedTextMatchMode mode, bool hasTextFocus, int[] expectedLines)
    {
        TestMulticursor(s_highlightText, (editor, findController) =>
        {
            editor.AddSelectionHighlighter();
            findController.GetState().Change(new NewFindState { SearchString = "f.*", IsRevealed = true, MatchCase = mode != SelectedTextMatchMode.CaseSensitive, WholeWord = true, IsRegex = true }, false);
            editor.SetSelection(Sel(1, 1, 1, 4));

            F(editor.SelectionHighlights()).ShouldBe(Lines(expectedLines));
        }, Mode(mode), hasTextFocus);
    }

    private static void AssertDuplicateFindHighlights(SelectedTextMatchMode mode, int[] expectedDifferentCaseLines)
    {
        TestMulticursor(s_highlightText, (editor, findController) =>
        {
            editor.AddSelectionHighlighter();
            editor.SetSelection(Sel(1, 1, 1, 4));
            var matchCase = mode == SelectedTextMatchMode.CaseSensitive;
            findController.GetState().Change(new NewFindState { SearchString = "foo", IsRevealed = true, MatchCase = matchCase, WholeWord = false }, false);
            var sameOptions = F(editor.SelectionHighlights());

            findController.GetState().Change(new NewFindState { MatchCase = !matchCase }, false);
            sameOptions.ShouldBe(string.Empty);
            F(editor.SelectionHighlights()).ShouldBe(Lines(expectedDifferentCaseLines));
        }, Mode(mode));
    }

    [Fact]
    public void Selection_Highlighting_Uses_Match_Case_False_Editor_Focused_True() => AssertSelectionHighlighting(SelectedTextMatchMode.CaseInsensitive, true, [2, 3, 4]);

    [Fact]
    public void Only_Suppress_Duplicate_Find_Highlights_With_The_Same_Matching_Rules_Match_Case_False() => AssertDuplicateFindHighlights(SelectedTextMatchMode.CaseInsensitive, [2, 3, 4]);

    [Fact]
    public void Selection_Highlighting_Uses_Match_Case_True_Editor_Focused_True() => AssertSelectionHighlighting(SelectedTextMatchMode.CaseSensitive, true, [3]);

    [Fact]
    public void Only_Suppress_Duplicate_Find_Highlights_With_The_Same_Matching_Rules_Match_Case_True() => AssertDuplicateFindHighlights(SelectedTextMatchMode.CaseSensitive, [3]);

    [Fact]
    public void Selection_Highlighting_Refreshes_On_Case_Only_Configuration_Changes_Without_Cursor_Movement()
    {
        var actual = new List<string>();
        TestMulticursor(s_highlightText, (editor, _) =>
        {
            editor.AddSelectionHighlighter();
            editor.SetSelection(Sel(1, 1, 1, 4));

            // Drain the initial scheduled update so it cannot mask missing configuration invalidation.
            editor.RunAllTimers();
            actual.Add(F(editor.SelectionHighlights()));

            editor.UpdateOptions(Mode(SelectedTextMatchMode.CaseSensitive));
            editor.RunAllTimers();
            actual.Add(F(editor.SelectionHighlights()));

            editor.UpdateOptions(Mode(SelectedTextMatchMode.CaseInsensitive));
            editor.RunAllTimers();
            actual.Add(F(editor.SelectionHighlights()));
        });

        actual.ShouldBe([Lines(2, 3, 4), Lines(3), Lines(2, 3, 4)]);
    }

    [Fact]
    public void Find_Regex_Select_All_Discards_An_Existing_Selection_Driven_Session()
    {
        TestMulticursor(["foo", "fooBar", "bar", "barista"], (editor, findController) =>
        {
            editor.AddSelectionHighlighter();
            editor.SetSelection(Sel(1, 1, 1, 4));
            MultiCursorActions.AddSelectionToNextFindMatch(editor);

            findController.GetState().Change(new NewFindState { SearchString = "bar$", IsRevealed = true, IsRegex = true, MatchCase = true }, false);
            editor.SetTextFocus(false);
            MultiCursorActions.SelectHighlights(editor);
            findController.GetState().Change(new NewFindState { IsRevealed = false }, false);
            var afterFind = F(editor.SelectionHighlights());

            editor.SetTextFocus(true);
            MultiCursorActions.AddSelectionToNextFindMatch(editor);

            afterFind.ShouldBe(L([2, 4, 2, 7], [4, 1, 4, 4]));
            F(editor.GetSelections()).ShouldBe(L([3, 1, 3, 4], [4, 1, 4, 4]));
        }, Mode(SelectedTextMatchMode.CaseInsensitive));
    }

    // ---- suite 'Find state disassociation'

    private static readonly string[] s_appText = ["app", "apples", "whatsapp", "app", "App", " app"];

    public enum EndSession
    {
        Selection,
        Blur,
        Dispose,
        Configuration,
    }

    [Theory]
    [InlineData(-1, EndSession.Selection)]
    [InlineData(-1, EndSession.Blur)]
    [InlineData(-1, EndSession.Dispose)]
    [InlineData(-1, EndSession.Configuration)]
    [InlineData(0, EndSession.Selection)]
    [InlineData(0, EndSession.Blur)]
    [InlineData(0, EndSession.Dispose)]
    [InlineData(0, EndSession.Configuration)]
    [InlineData(1, EndSession.Selection)]
    [InlineData(1, EndSession.Blur)]
    [InlineData(1, EndSession.Dispose)]
    [InlineData(1, EndSession.Configuration)]
    [InlineData(2, EndSession.Selection)]
    [InlineData(2, EndSession.Blur)]
    [InlineData(2, EndSession.Dispose)]
    [InlineData(2, EndSession.Configuration)]
    public void Caret_Started_Sessions_Temporarily_Override_And_Restore_Find_Options_In_Every_Matching_Mode(int modeIndex, EndSession endSession)
    {
        SelectedTextMatchMode? mode = modeIndex < 0 ? null : (SelectedTextMatchMode)modeIndex;
        TestMulticursor(s_appText, (editor, findController) =>
        {
            var state = findController.GetState();
            state.Change(new NewFindState { MatchCase = false, WholeWord = false, IsRegex = true }, false);
            bool[] Effective() => [state.MatchCase, state.WholeWord, state.IsRegex];
            bool[] Stored() => [state.ActualMatchCase, state.ActualWholeWord, state.ActualIsRegex];
            var before = findController.HighlightFindOptionsCount;
            editor.SetSelection(Sel(1, 2, 1, 2));
            MultiCursorActions.AddSelectionToNextFindMatch(editor);
            MultiCursorActions.AddSelectionToNextFindMatch(editor);

            state.SearchString.ShouldBe("app");
            Effective().ShouldBe([true, true, false]);
            Stored().ShouldBe([false, false, true]);
            F(editor.GetSelections()).ShouldBe(Lines(1, 4));
            (findController.HighlightFindOptionsCount - before).ShouldBe(1);

            switch (endSession)
            {
                case EndSession.Selection:
                    editor.SetSelection(Sel(2, 1, 2, 4));
                    break;
                case EndSession.Blur:
                    editor.SetTextFocus(false);
                    break;
                case EndSession.Dispose:
                    editor.DisposeController();
                    break;
                case EndSession.Configuration:
                    editor.UpdateOptions(Mode(editor.MultiCursorOptions.SelectedTextMatchMode == SelectedTextMatchMode.FindOptions ? SelectedTextMatchMode.CaseInsensitive : SelectedTextMatchMode.FindOptions));
                    break;
            }

            Effective().ShouldBe([false, false, true]);
            Stored().ShouldBe([false, false, true]);
        }, mode is { } m ? Mode(m) : null);
    }

    [Fact]
    public void Changing_Find_Options_Ends_A_Default_Caret_Started_Session_Without_Reverting_User_Preferences()
    {
        TestMulticursor(s_appText, (editor, findController) =>
        {
            var state = findController.GetState();
            state.Change(new NewFindState { MatchCase = false, WholeWord = false, IsRegex = true }, false);
            editor.SetSelection(Sel(1, 2, 1, 2));
            MultiCursorActions.AddSelectionToNextFindMatch(editor);

            state.Change(new NewFindState { MatchCase = false, WholeWord = true }, false);
            MultiCursorActions.AddSelectionToNextFindMatch(editor);
            MultiCursorActions.AddSelectionToNextFindMatch(editor);

            F(editor.GetSelections()).ShouldBe(Lines(1, 4, 5));
            new[] { state.MatchCase, state.WholeWord, state.IsRegex }.ShouldBe([false, true, true]);
            new[] { state.ActualMatchCase, state.ActualWholeWord, state.ActualIsRegex }.ShouldBe([false, true, true]);
        });
    }

    [Fact]
    public void Enters_Mode()
    {
        TestMulticursor(s_appText, (editor, _) =>
        {
            editor.SetSelections([Sel(1, 2, 1, 2)]);

            MultiCursorActions.AddSelectionToNextFindMatch(editor);
            editor.GetSelections().ShouldBe([Sel(1, 1, 1, 4)]);

            MultiCursorActions.AddSelectionToNextFindMatch(editor);
            editor.GetSelections().ShouldBe([Sel(1, 1, 1, 4), Sel(4, 1, 4, 4)]);

            MultiCursorActions.AddSelectionToNextFindMatch(editor);
            editor.GetSelections().ShouldBe([Sel(1, 1, 1, 4), Sel(4, 1, 4, 4), Sel(6, 2, 6, 5)]);
        });
    }

    [Fact]
    public void Leaves_Mode_When_Selection_Changes()
    {
        TestMulticursor(s_appText, (editor, _) =>
        {
            editor.SetSelections([Sel(1, 2, 1, 2)]);

            MultiCursorActions.AddSelectionToNextFindMatch(editor);
            editor.GetSelections().ShouldBe([Sel(1, 1, 1, 4)]);

            MultiCursorActions.AddSelectionToNextFindMatch(editor);
            editor.GetSelections().ShouldBe([Sel(1, 1, 1, 4), Sel(4, 1, 4, 4)]);

            // change selection
            editor.SetSelections([Sel(1, 1, 1, 4)]);

            MultiCursorActions.AddSelectionToNextFindMatch(editor);
            editor.GetSelections().ShouldBe([Sel(1, 1, 1, 4), Sel(2, 1, 2, 4)]);
        });
    }

    [Fact]
    public void Select_Highlights_Respects_Mode()
    {
        TestMulticursor(s_appText, (editor, _) =>
        {
            editor.SetSelections([Sel(1, 2, 1, 2)]);

            MultiCursorActions.SelectHighlights(editor);
            editor.GetSelections().ShouldBe([Sel(1, 1, 1, 4), Sel(4, 1, 4, 4), Sel(6, 2, 6, 5)]);

            MultiCursorActions.SelectHighlights(editor);
            editor.GetSelections().ShouldBe([Sel(1, 1, 1, 4), Sel(4, 1, 4, 4), Sel(6, 2, 6, 5)]);
        });
    }
}
