// Ported from vscode (src/vs/editor/contrib/find/test/browser/findModel.test.ts, commit b57d5c9e), MIT license.
// All 45 upstream tests (44 findTest cases and issue #288515). Each findTest runs twice, as upstream: on a model of the
// joined lines and on one built from three chunks with the piece tree builder. The editor is TestFindEditor (the test
// code editor behind the IFindEditor interface find runs on); editor.trigger('mouse', MoveTo, ...) is
// CoreNavigationCommands.MoveTo on the view model. The bodies were converted mechanically and keep upstream's order.

using Shouldly;
using TKCode.Arc.Editor.Commands;
using TKCode.Arc.Editor.Contrib.Find;
using TKCode.Arc.Editor.Model;
using Xunit;

namespace TKCode.Arc.Editor.Tests;

public sealed class FindModelTests
{
    private static readonly string[] s_textArr =
    [
        "// my cool header",
        "#include \"cool.h\"",
        "#include <iostream>",
        "",
        "int main() {",
        "    cout << \"hello world, Hello!\" << endl;",
        "    cout << \"hello world again\" << endl;",
        "    cout << \"Hello world again\" << endl;",
        "    cout << \"helloworld again\" << endl;",
        "}",
        "// blablablaciao",
        "",
    ];

    private static void FindTest(Action<TestFindEditor> callback)
    {
        TestFindEditor.With(s_textArr, callback);

        var text = string.Join('\n', s_textArr);
        var ptBuilder = new PieceTreeTextBufferBuilder();
        ptBuilder.AcceptChunk(text.Substring(0, 94));
        ptBuilder.AcceptChunk(text.Substring(94, 101));
        ptBuilder.AcceptChunk(text.Substring(195, 59));
        var factory = ptBuilder.Finish();
        using var model = TestTextModel.Create(factory);
        TestFindEditor.With(model, callback);
    }

    private static int[] FromRange(TextRange rng) => [rng.StartLineNumber, rng.StartColumn, rng.EndLineNumber, rng.EndColumn];

    private static (List<int[]> Highlighted, List<int[]> FindDecorations) GetFindState(TestFindEditor editor)
    {
        var model = editor.GetModel()!;
        var currentFindMatches = new List<TextRange>();
        var allFindMatches = new List<TextRange>();

        foreach (var dec in model.GetAllDecorations())
        {
            if (dec.Options.ClassName == "currentFindMatch")
            {
                currentFindMatches.Add(dec.Range);
                allFindMatches.Add(dec.Range);
            }
            else if (dec.Options.ClassName == "findMatch")
            {
                allFindMatches.Add(dec.Range);
            }
        }

        currentFindMatches.Sort(TextRange.CompareRangesUsingStarts);
        allFindMatches.Sort(TextRange.CompareRangesUsingStarts);

        return (currentFindMatches.Select(FromRange).ToList(), allFindMatches.Select(FromRange).ToList());
    }

    private static void AssertFindState(TestFindEditor editor, int[] cursor, int[]? highlighted, int[][] findDecorations)
    {
        FromRange(editor.GetSelection()).ShouldBe(cursor, "cursor");

        var (actualHighlighted, actualFindDecorations) = GetFindState(editor);
        actualHighlighted.Select(Format).ShouldBe(highlighted is null ? [] : [Format(highlighted)], "highlighted");
        actualFindDecorations.Select(Format).ShouldBe(findDecorations.Select(Format), "findDecorations");
    }

    private static string Format(int[] range) => "[" + string.Join(", ", range) + "]";

    private static void AssertSelections(TestFindEditor editor, params TextSelection[] expected) =>
        editor.GetSelections().Select(s => s.ToString()).ShouldBe(expected.Select(s => s.ToString()));

    private static void MoveTo(TestFindEditor editor, int lineNumber, int column) =>
        CoreNavigationCommands.MoveTo.RunCoreEditorCommand(editor.Inner.ViewModel, new() { Position = new TextPosition(lineNumber, column) });

    // upstream: incremental find from beginning of file
    [Fact]
    public void Incremental_find_from_beginning_of_file() => FindTest(editor =>
    {
        editor.SetPosition(new TextPosition(1, 1));
        using var findState = new FindReplaceState();
        using var findModel = new FindModelBoundToEditorModel(editor, findState);

        // simulate typing the search string
        findState.Change(new NewFindReplaceState { SearchString = "H" }, true);
        AssertFindState(
            editor,
            [1, 12, 1, 13],
            [1, 12, 1, 13],
            [
                [1, 12, 1, 13],
                [2, 16, 2, 17],
                [6, 14, 6, 15],
                [6, 27, 6, 28],
                [7, 14, 7, 15],
                [8, 14, 8, 15],
                [9, 14, 9, 15]
            ]
        );

        // simulate typing the search string
        findState.Change(new NewFindReplaceState { SearchString = "He" }, true);
        AssertFindState(
            editor,
            [1, 12, 1, 14],
            [1, 12, 1, 14],
            [
                [1, 12, 1, 14],
                [6, 14, 6, 16],
                [6, 27, 6, 29],
                [7, 14, 7, 16],
                [8, 14, 8, 16],
                [9, 14, 9, 16]
            ]
        );

        // simulate typing the search string
        findState.Change(new NewFindReplaceState { SearchString = "Hello" }, true);
        AssertFindState(
            editor,
            [6, 14, 6, 19],
            [6, 14, 6, 19],
            [
                [6, 14, 6, 19],
                [6, 27, 6, 32],
                [7, 14, 7, 19],
                [8, 14, 8, 19],
                [9, 14, 9, 19]
            ]
        );

        // simulate toggling on `matchCase`
        findState.Change(new NewFindReplaceState { MatchCase = true }, true);
        AssertFindState(
            editor,
            [6, 27, 6, 32],
            [6, 27, 6, 32],
            [
                [6, 27, 6, 32],
                [8, 14, 8, 19]
            ]
        );

        // simulate typing the search string
        findState.Change(new NewFindReplaceState { SearchString = "hello" }, true);
        AssertFindState(
            editor,
            [6, 14, 6, 19],
            [6, 14, 6, 19],
            [
                [6, 14, 6, 19],
                [7, 14, 7, 19],
                [9, 14, 9, 19]
            ]
        );

        // simulate toggling on `wholeWord`
        findState.Change(new NewFindReplaceState { WholeWord = true }, true);
        AssertFindState(
            editor,
            [6, 14, 6, 19],
            [6, 14, 6, 19],
            [
                [6, 14, 6, 19],
                [7, 14, 7, 19]
            ]
        );

        // simulate toggling off `matchCase`
        findState.Change(new NewFindReplaceState { MatchCase = false }, true);
        AssertFindState(
            editor,
            [6, 14, 6, 19],
            [6, 14, 6, 19],
            [
                [6, 14, 6, 19],
                [6, 27, 6, 32],
                [7, 14, 7, 19],
                [8, 14, 8, 19]
            ]
        );

        // simulate toggling off `wholeWord`
        findState.Change(new NewFindReplaceState { WholeWord = false }, true);
        AssertFindState(
            editor,
            [6, 14, 6, 19],
            [6, 14, 6, 19],
            [
                [6, 14, 6, 19],
                [6, 27, 6, 32],
                [7, 14, 7, 19],
                [8, 14, 8, 19],
                [9, 14, 9, 19]
            ]
        );

        // simulate adding a search scope
        findState.Change(new NewFindReplaceState { SearchScope = [new TextRange(8, 1, 10, 1)] }, true);
        AssertFindState(
            editor,
            [8, 14, 8, 19],
            [8, 14, 8, 19],
            [
                [8, 14, 8, 19],
                [9, 14, 9, 19]
            ]
        );

        // simulate removing the search scope
        findState.Change(new NewFindReplaceState { SearchScope = null }, true);
        AssertFindState(
            editor,
            [6, 14, 6, 19],
            [6, 14, 6, 19],
            [
                [6, 14, 6, 19],
                [6, 27, 6, 32],
                [7, 14, 7, 19],
                [8, 14, 8, 19],
                [9, 14, 9, 19]
            ]
        );

        findModel.Dispose();
        findState.Dispose();
    });

    // upstream: find model removes its decorations
    [Fact]
    public void Find_model_removes_its_decorations() => FindTest(editor =>
    {
        using var findState = new FindReplaceState();
        findState.Change(new NewFindReplaceState { SearchString = "hello" }, false);
        using var findModel = new FindModelBoundToEditorModel(editor, findState);

        findState.MatchesCount.ShouldBe(5);
        AssertFindState(
            editor,
            [1, 1, 1, 1],
            null,
            [
                [6, 14, 6, 19],
                [6, 27, 6, 32],
                [7, 14, 7, 19],
                [8, 14, 8, 19],
                [9, 14, 9, 19]
            ]
        );

        findModel.Dispose();
        findState.Dispose();

        AssertFindState(
            editor,
            [1, 1, 1, 1],
            null,
            []
        );
    });

    // upstream: find model updates state matchesCount
    [Fact]
    public void Find_model_updates_state_matchesCount() => FindTest(editor =>
    {
        using var findState = new FindReplaceState();
        findState.Change(new NewFindReplaceState { SearchString = "hello" }, false);
        using var findModel = new FindModelBoundToEditorModel(editor, findState);

        findState.MatchesCount.ShouldBe(5);
        AssertFindState(
            editor,
            [1, 1, 1, 1],
            null,
            [
                [6, 14, 6, 19],
                [6, 27, 6, 32],
                [7, 14, 7, 19],
                [8, 14, 8, 19],
                [9, 14, 9, 19]
            ]
        );

        findState.Change(new NewFindReplaceState { SearchString = "helloo" }, false);
        findState.MatchesCount.ShouldBe(0);
        AssertFindState(
            editor,
            [1, 1, 1, 1],
            null,
            []
        );

        findModel.Dispose();
        findState.Dispose();
    });

    // upstream: find model reacts to position change
    [Fact]
    public void Find_model_reacts_to_position_change() => FindTest(editor =>
    {
        using var findState = new FindReplaceState();
        findState.Change(new NewFindReplaceState { SearchString = "hello" }, false);
        using var findModel = new FindModelBoundToEditorModel(editor, findState);

        AssertFindState(
            editor,
            [1, 1, 1, 1],
            null,
            [
                [6, 14, 6, 19],
                [6, 27, 6, 32],
                [7, 14, 7, 19],
                [8, 14, 8, 19],
                [9, 14, 9, 19]
            ]
        );

        MoveTo(editor, 6, 20);

        AssertFindState(
            editor,
            [6, 20, 6, 20],
            null,
            [
                [6, 14, 6, 19],
                [6, 27, 6, 32],
                [7, 14, 7, 19],
                [8, 14, 8, 19],
                [9, 14, 9, 19]
            ]
        );

        findState.Change(new NewFindReplaceState { SearchString = "Hello" }, true);
        AssertFindState(
            editor,
            [6, 27, 6, 32],
            [6, 27, 6, 32],
            [
                [6, 14, 6, 19],
                [6, 27, 6, 32],
                [7, 14, 7, 19],
                [8, 14, 8, 19],
                [9, 14, 9, 19]
            ]
        );

        findModel.Dispose();
        findState.Dispose();
    });

    // upstream: find model next
    [Fact]
    public void Find_model_next() => FindTest(editor =>
    {
        using var findState = new FindReplaceState();
        findState.Change(new NewFindReplaceState { SearchString = "hello", WholeWord = true }, false);
        using var findModel = new FindModelBoundToEditorModel(editor, findState);

        AssertFindState(
            editor,
            [1, 1, 1, 1],
            null,
            [
                [6, 14, 6, 19],
                [6, 27, 6, 32],
                [7, 14, 7, 19],
                [8, 14, 8, 19]
            ]
        );

        findModel.MoveToNextMatch();
        AssertFindState(
            editor,
            [6, 14, 6, 19],
            [6, 14, 6, 19],
            [
                [6, 14, 6, 19],
                [6, 27, 6, 32],
                [7, 14, 7, 19],
                [8, 14, 8, 19]
            ]
        );

        findModel.MoveToNextMatch();
        AssertFindState(
            editor,
            [6, 27, 6, 32],
            [6, 27, 6, 32],
            [
                [6, 14, 6, 19],
                [6, 27, 6, 32],
                [7, 14, 7, 19],
                [8, 14, 8, 19]
            ]
        );

        findModel.MoveToNextMatch();
        AssertFindState(
            editor,
            [7, 14, 7, 19],
            [7, 14, 7, 19],
            [
                [6, 14, 6, 19],
                [6, 27, 6, 32],
                [7, 14, 7, 19],
                [8, 14, 8, 19]
            ]
        );

        findModel.MoveToNextMatch();
        AssertFindState(
            editor,
            [8, 14, 8, 19],
            [8, 14, 8, 19],
            [
                [6, 14, 6, 19],
                [6, 27, 6, 32],
                [7, 14, 7, 19],
                [8, 14, 8, 19]
            ]
        );

        findModel.MoveToNextMatch();
        AssertFindState(
            editor,
            [6, 14, 6, 19],
            [6, 14, 6, 19],
            [
                [6, 14, 6, 19],
                [6, 27, 6, 32],
                [7, 14, 7, 19],
                [8, 14, 8, 19]
            ]
        );

        findModel.Dispose();
        findState.Dispose();
    });

    // upstream: find model next stays in scope
    [Fact]
    public void Find_model_next_stays_in_scope() => FindTest(editor =>
    {
        using var findState = new FindReplaceState();
        findState.Change(new NewFindReplaceState { SearchString = "hello", WholeWord = true, SearchScope = [new TextRange(7, 1, 9, 1)] }, false);
        using var findModel = new FindModelBoundToEditorModel(editor, findState);

        AssertFindState(
            editor,
            [1, 1, 1, 1],
            null,
            [
                [7, 14, 7, 19],
                [8, 14, 8, 19]
            ]
        );

        findModel.MoveToNextMatch();
        AssertFindState(
            editor,
            [7, 14, 7, 19],
            [7, 14, 7, 19],
            [
                [7, 14, 7, 19],
                [8, 14, 8, 19]
            ]
        );

        findModel.MoveToNextMatch();
        AssertFindState(
            editor,
            [8, 14, 8, 19],
            [8, 14, 8, 19],
            [
                [7, 14, 7, 19],
                [8, 14, 8, 19]
            ]
        );

        findModel.MoveToNextMatch();
        AssertFindState(
            editor,
            [7, 14, 7, 19],
            [7, 14, 7, 19],
            [
                [7, 14, 7, 19],
                [8, 14, 8, 19]
            ]
        );

        findModel.Dispose();
        findState.Dispose();
    });

    // upstream: multi-selection find model next stays in scope (overlap)
    [Fact]
    public void Multi_selection_find_model_next_stays_in_scope_overlap() => FindTest(editor =>
    {
        using var findState = new FindReplaceState();
        findState.Change(new NewFindReplaceState { SearchString = "hello", WholeWord = true, SearchScope = [new TextRange(7, 1, 8, 2), new TextRange(8, 1, 9, 1)] }, false);
        using var findModel = new FindModelBoundToEditorModel(editor, findState);

        AssertFindState(
            editor,
            [1, 1, 1, 1],
            null,
            [
                [7, 14, 7, 19],
                [8, 14, 8, 19]
            ]
        );

        findModel.MoveToNextMatch();
        AssertFindState(
            editor,
            [7, 14, 7, 19],
            [7, 14, 7, 19],
            [
                [7, 14, 7, 19],
                [8, 14, 8, 19]
            ]
        );

        findModel.MoveToNextMatch();
        AssertFindState(
            editor,
            [8, 14, 8, 19],
            [8, 14, 8, 19],
            [
                [7, 14, 7, 19],
                [8, 14, 8, 19]
            ]
        );

        findModel.MoveToNextMatch();
        AssertFindState(
            editor,
            [7, 14, 7, 19],
            [7, 14, 7, 19],
            [
                [7, 14, 7, 19],
                [8, 14, 8, 19]
            ]
        );

        findModel.Dispose();
        findState.Dispose();
    });

    // upstream: multi-selection find model next stays in scope
    [Fact]
    public void Multi_selection_find_model_next_stays_in_scope() => FindTest(editor =>
    {
        using var findState = new FindReplaceState();
        findState.Change(new NewFindReplaceState { SearchString = "hello", MatchCase = true, WholeWord = false, SearchScope = [new TextRange(6, 1, 7, 38), new TextRange(9, 3, 9, 38)] }, false);
        using var findModel = new FindModelBoundToEditorModel(editor, findState);

        AssertFindState(
            editor,
            [1, 1, 1, 1],
            null,
            [
                [6, 14, 6, 19],
                // `matchCase: false` would
                // find this match as well:
                // [6, 27, 6, 32],
                [7, 14, 7, 19],
                // `wholeWord: true` would
                // exclude this match:
                [9, 14, 9, 19],
            ]
        );

        findModel.MoveToNextMatch();
        AssertFindState(
            editor,
            [6, 14, 6, 19],
            [6, 14, 6, 19],
            [
                [6, 14, 6, 19],
                [7, 14, 7, 19],
                [9, 14, 9, 19],
            ]
        );

        findModel.MoveToNextMatch();
        AssertFindState(
            editor,
            [7, 14, 7, 19],
            [7, 14, 7, 19],
            [
                [6, 14, 6, 19],
                [7, 14, 7, 19],
                [9, 14, 9, 19],
            ]
        );

        findModel.MoveToNextMatch();
        AssertFindState(
            editor,
            [9, 14, 9, 19],
            [9, 14, 9, 19],
            [
                [6, 14, 6, 19],
                [7, 14, 7, 19],
                [9, 14, 9, 19],
            ]
        );

        findModel.MoveToNextMatch();
        AssertFindState(
            editor,
            [6, 14, 6, 19],
            [6, 14, 6, 19],
            [
                [6, 14, 6, 19],
                [7, 14, 7, 19],
                [9, 14, 9, 19],
            ]
        );

        findModel.Dispose();
        findState.Dispose();
    });

    // upstream: find model prev
    [Fact]
    public void Find_model_prev() => FindTest(editor =>
    {
        using var findState = new FindReplaceState();
        findState.Change(new NewFindReplaceState { SearchString = "hello", WholeWord = true }, false);
        using var findModel = new FindModelBoundToEditorModel(editor, findState);

        AssertFindState(
            editor,
            [1, 1, 1, 1],
            null,
            [
                [6, 14, 6, 19],
                [6, 27, 6, 32],
                [7, 14, 7, 19],
                [8, 14, 8, 19]
            ]
        );

        findModel.MoveToPrevMatch();
        AssertFindState(
            editor,
            [8, 14, 8, 19],
            [8, 14, 8, 19],
            [
                [6, 14, 6, 19],
                [6, 27, 6, 32],
                [7, 14, 7, 19],
                [8, 14, 8, 19]
            ]
        );

        findModel.MoveToPrevMatch();
        AssertFindState(
            editor,
            [7, 14, 7, 19],
            [7, 14, 7, 19],
            [
                [6, 14, 6, 19],
                [6, 27, 6, 32],
                [7, 14, 7, 19],
                [8, 14, 8, 19]
            ]
        );

        findModel.MoveToPrevMatch();
        AssertFindState(
            editor,
            [6, 27, 6, 32],
            [6, 27, 6, 32],
            [
                [6, 14, 6, 19],
                [6, 27, 6, 32],
                [7, 14, 7, 19],
                [8, 14, 8, 19]
            ]
        );

        findModel.MoveToPrevMatch();
        AssertFindState(
            editor,
            [6, 14, 6, 19],
            [6, 14, 6, 19],
            [
                [6, 14, 6, 19],
                [6, 27, 6, 32],
                [7, 14, 7, 19],
                [8, 14, 8, 19]
            ]
        );

        findModel.MoveToPrevMatch();
        AssertFindState(
            editor,
            [8, 14, 8, 19],
            [8, 14, 8, 19],
            [
                [6, 14, 6, 19],
                [6, 27, 6, 32],
                [7, 14, 7, 19],
                [8, 14, 8, 19]
            ]
        );

        findModel.Dispose();
        findState.Dispose();
    });

    // upstream: find model prev stays in scope
    [Fact]
    public void Find_model_prev_stays_in_scope() => FindTest(editor =>
    {
        using var findState = new FindReplaceState();
        findState.Change(new NewFindReplaceState { SearchString = "hello", WholeWord = true, SearchScope = [new TextRange(7, 1, 9, 1)] }, false);
        using var findModel = new FindModelBoundToEditorModel(editor, findState);

        AssertFindState(
            editor,
            [1, 1, 1, 1],
            null,
            [
                [7, 14, 7, 19],
                [8, 14, 8, 19]
            ]
        );

        findModel.MoveToPrevMatch();
        AssertFindState(
            editor,
            [8, 14, 8, 19],
            [8, 14, 8, 19],
            [
                [7, 14, 7, 19],
                [8, 14, 8, 19]
            ]
        );

        findModel.MoveToPrevMatch();
        AssertFindState(
            editor,
            [7, 14, 7, 19],
            [7, 14, 7, 19],
            [
                [7, 14, 7, 19],
                [8, 14, 8, 19]
            ]
        );

        findModel.MoveToPrevMatch();
        AssertFindState(
            editor,
            [8, 14, 8, 19],
            [8, 14, 8, 19],
            [
                [7, 14, 7, 19],
                [8, 14, 8, 19]
            ]
        );

        findModel.Dispose();
        findState.Dispose();
    });

    // upstream: find model next/prev with no matches
    [Fact]
    public void Find_model_next_prev_with_no_matches() => FindTest(editor =>
    {
        using var findState = new FindReplaceState();
        findState.Change(new NewFindReplaceState { SearchString = "helloo", WholeWord = true }, false);
        using var findModel = new FindModelBoundToEditorModel(editor, findState);

        AssertFindState(
            editor,
            [1, 1, 1, 1],
            null,
            []
        );

        findModel.MoveToNextMatch();
        AssertFindState(
            editor,
            [1, 1, 1, 1],
            null,
            []
        );

        findModel.MoveToPrevMatch();
        AssertFindState(
            editor,
            [1, 1, 1, 1],
            null,
            []
        );

        findModel.Dispose();
        findState.Dispose();
    });

    // upstream: find model next/prev respects cursor position
    [Fact]
    public void Find_model_next_prev_respects_cursor_position() => FindTest(editor =>
    {
        using var findState = new FindReplaceState();
        findState.Change(new NewFindReplaceState { SearchString = "hello", WholeWord = true }, false);
        using var findModel = new FindModelBoundToEditorModel(editor, findState);

        AssertFindState(
            editor,
            [1, 1, 1, 1],
            null,
            [
                [6, 14, 6, 19],
                [6, 27, 6, 32],
                [7, 14, 7, 19],
                [8, 14, 8, 19]
            ]
        );

        MoveTo(editor, 6, 20);
        AssertFindState(
            editor,
            [6, 20, 6, 20],
            null,
            [
                [6, 14, 6, 19],
                [6, 27, 6, 32],
                [7, 14, 7, 19],
                [8, 14, 8, 19]
            ]
        );

        findModel.MoveToNextMatch();
        AssertFindState(
            editor,
            [6, 27, 6, 32],
            [6, 27, 6, 32],
            [
                [6, 14, 6, 19],
                [6, 27, 6, 32],
                [7, 14, 7, 19],
                [8, 14, 8, 19]
            ]
        );

        findModel.Dispose();
        findState.Dispose();
    });

    // upstream: find ^
    [Fact]
    public void Find() => FindTest(editor =>
    {
        using var findState = new FindReplaceState();
        findState.Change(new NewFindReplaceState { SearchString = "^", IsRegex = true }, false);
        using var findModel = new FindModelBoundToEditorModel(editor, findState);

        AssertFindState(
            editor,
            [1, 1, 1, 1],
            null,
            [
                [1, 1, 1, 1],
                [2, 1, 2, 1],
                [3, 1, 3, 1],
                [4, 1, 4, 1],
                [5, 1, 5, 1],
                [6, 1, 6, 1],
                [7, 1, 7, 1],
                [8, 1, 8, 1],
                [9, 1, 9, 1],
                [10, 1, 10, 1],
                [11, 1, 11, 1],
                [12, 1, 12, 1],
            ]
        );

        findModel.MoveToNextMatch();
        AssertFindState(
            editor,
            [2, 1, 2, 1],
            [2, 1, 2, 1],
            [
                [1, 1, 1, 1],
                [2, 1, 2, 1],
                [3, 1, 3, 1],
                [4, 1, 4, 1],
                [5, 1, 5, 1],
                [6, 1, 6, 1],
                [7, 1, 7, 1],
                [8, 1, 8, 1],
                [9, 1, 9, 1],
                [10, 1, 10, 1],
                [11, 1, 11, 1],
                [12, 1, 12, 1],
            ]
        );

        findModel.MoveToNextMatch();
        AssertFindState(
            editor,
            [3, 1, 3, 1],
            [3, 1, 3, 1],
            [
                [1, 1, 1, 1],
                [2, 1, 2, 1],
                [3, 1, 3, 1],
                [4, 1, 4, 1],
                [5, 1, 5, 1],
                [6, 1, 6, 1],
                [7, 1, 7, 1],
                [8, 1, 8, 1],
                [9, 1, 9, 1],
                [10, 1, 10, 1],
                [11, 1, 11, 1],
                [12, 1, 12, 1],
            ]
        );

        findModel.Dispose();
        findState.Dispose();
    });

    // upstream: find $
    [Fact]
    public void Find_2() => FindTest(editor =>
    {
        using var findState = new FindReplaceState();
        findState.Change(new NewFindReplaceState { SearchString = "$", IsRegex = true }, false);
        using var findModel = new FindModelBoundToEditorModel(editor, findState);

        AssertFindState(
            editor,
            [1, 1, 1, 1],
            null,
            [
                [1, 18, 1, 18],
                [2, 18, 2, 18],
                [3, 20, 3, 20],
                [4, 1, 4, 1],
                [5, 13, 5, 13],
                [6, 43, 6, 43],
                [7, 41, 7, 41],
                [8, 41, 8, 41],
                [9, 40, 9, 40],
                [10, 2, 10, 2],
                [11, 17, 11, 17],
                [12, 1, 12, 1],
            ]
        );

        findModel.MoveToNextMatch();
        AssertFindState(
            editor,
            [1, 18, 1, 18],
            [1, 18, 1, 18],
            [
                [1, 18, 1, 18],
                [2, 18, 2, 18],
                [3, 20, 3, 20],
                [4, 1, 4, 1],
                [5, 13, 5, 13],
                [6, 43, 6, 43],
                [7, 41, 7, 41],
                [8, 41, 8, 41],
                [9, 40, 9, 40],
                [10, 2, 10, 2],
                [11, 17, 11, 17],
                [12, 1, 12, 1],
            ]
        );

        findModel.MoveToNextMatch();
        AssertFindState(
            editor,
            [2, 18, 2, 18],
            [2, 18, 2, 18],
            [
                [1, 18, 1, 18],
                [2, 18, 2, 18],
                [3, 20, 3, 20],
                [4, 1, 4, 1],
                [5, 13, 5, 13],
                [6, 43, 6, 43],
                [7, 41, 7, 41],
                [8, 41, 8, 41],
                [9, 40, 9, 40],
                [10, 2, 10, 2],
                [11, 17, 11, 17],
                [12, 1, 12, 1],
            ]
        );

        findModel.MoveToNextMatch();
        AssertFindState(
            editor,
            [3, 20, 3, 20],
            [3, 20, 3, 20],
            [
                [1, 18, 1, 18],
                [2, 18, 2, 18],
                [3, 20, 3, 20],
                [4, 1, 4, 1],
                [5, 13, 5, 13],
                [6, 43, 6, 43],
                [7, 41, 7, 41],
                [8, 41, 8, 41],
                [9, 40, 9, 40],
                [10, 2, 10, 2],
                [11, 17, 11, 17],
                [12, 1, 12, 1],
            ]
        );

        findModel.Dispose();
        findState.Dispose();
    });

    // upstream: find next ^$
    [Fact]
    public void Find_next() => FindTest(editor =>
    {
        using var findState = new FindReplaceState();
        findState.Change(new NewFindReplaceState { SearchString = "^$", IsRegex = true }, false);
        using var findModel = new FindModelBoundToEditorModel(editor, findState);

        AssertFindState(
            editor,
            [1, 1, 1, 1],
            null,
            [
                [4, 1, 4, 1],
                [12, 1, 12, 1],
            ]
        );

        findModel.MoveToNextMatch();
        AssertFindState(
            editor,
            [4, 1, 4, 1],
            [4, 1, 4, 1],
            [
                [4, 1, 4, 1],
                [12, 1, 12, 1],
            ]
        );

        findModel.MoveToNextMatch();
        AssertFindState(
            editor,
            [12, 1, 12, 1],
            [12, 1, 12, 1],
            [
                [4, 1, 4, 1],
                [12, 1, 12, 1],
            ]
        );

        findModel.MoveToNextMatch();
        AssertFindState(
            editor,
            [4, 1, 4, 1],
            [4, 1, 4, 1],
            [
                [4, 1, 4, 1],
                [12, 1, 12, 1],
            ]
        );

        findModel.Dispose();
        findState.Dispose();
    });

    // upstream: find .*
    [Fact]
    public void Find_3() => FindTest(editor =>
    {
        using var findState = new FindReplaceState();
        findState.Change(new NewFindReplaceState { SearchString = ".*", IsRegex = true }, false);
        using var findModel = new FindModelBoundToEditorModel(editor, findState);

        AssertFindState(
            editor,
            [1, 1, 1, 1],
            null,
            [
                [1, 1, 1, 18],
                [2, 1, 2, 18],
                [3, 1, 3, 20],
                [4, 1, 4, 1],
                [5, 1, 5, 13],
                [6, 1, 6, 43],
                [7, 1, 7, 41],
                [8, 1, 8, 41],
                [9, 1, 9, 40],
                [10, 1, 10, 2],
                [11, 1, 11, 17],
                [12, 1, 12, 1],
            ]
        );

        findModel.Dispose();
        findState.Dispose();
    });

    // upstream: find next ^.*$
    [Fact]
    public void Find_next_2() => FindTest(editor =>
    {
        using var findState = new FindReplaceState();
        findState.Change(new NewFindReplaceState { SearchString = "^.*$", IsRegex = true }, false);
        using var findModel = new FindModelBoundToEditorModel(editor, findState);

        AssertFindState(
            editor,
            [1, 1, 1, 1],
            null,
            [
                [1, 1, 1, 18],
                [2, 1, 2, 18],
                [3, 1, 3, 20],
                [4, 1, 4, 1],
                [5, 1, 5, 13],
                [6, 1, 6, 43],
                [7, 1, 7, 41],
                [8, 1, 8, 41],
                [9, 1, 9, 40],
                [10, 1, 10, 2],
                [11, 1, 11, 17],
                [12, 1, 12, 1],
            ]
        );

        findModel.MoveToNextMatch();
        AssertFindState(
            editor,
            [1, 1, 1, 18],
            [1, 1, 1, 18],
            [
                [1, 1, 1, 18],
                [2, 1, 2, 18],
                [3, 1, 3, 20],
                [4, 1, 4, 1],
                [5, 1, 5, 13],
                [6, 1, 6, 43],
                [7, 1, 7, 41],
                [8, 1, 8, 41],
                [9, 1, 9, 40],
                [10, 1, 10, 2],
                [11, 1, 11, 17],
                [12, 1, 12, 1],
            ]
        );

        findModel.MoveToNextMatch();
        AssertFindState(
            editor,
            [2, 1, 2, 18],
            [2, 1, 2, 18],
            [
                [1, 1, 1, 18],
                [2, 1, 2, 18],
                [3, 1, 3, 20],
                [4, 1, 4, 1],
                [5, 1, 5, 13],
                [6, 1, 6, 43],
                [7, 1, 7, 41],
                [8, 1, 8, 41],
                [9, 1, 9, 40],
                [10, 1, 10, 2],
                [11, 1, 11, 17],
                [12, 1, 12, 1],
            ]
        );

        findModel.Dispose();
        findState.Dispose();
    });

    // upstream: find prev ^.*$
    [Fact]
    public void Find_prev() => FindTest(editor =>
    {
        using var findState = new FindReplaceState();
        findState.Change(new NewFindReplaceState { SearchString = "^.*$", IsRegex = true }, false);
        using var findModel = new FindModelBoundToEditorModel(editor, findState);

        AssertFindState(
            editor,
            [1, 1, 1, 1],
            null,
            [
                [1, 1, 1, 18],
                [2, 1, 2, 18],
                [3, 1, 3, 20],
                [4, 1, 4, 1],
                [5, 1, 5, 13],
                [6, 1, 6, 43],
                [7, 1, 7, 41],
                [8, 1, 8, 41],
                [9, 1, 9, 40],
                [10, 1, 10, 2],
                [11, 1, 11, 17],
                [12, 1, 12, 1],
            ]
        );

        findModel.MoveToPrevMatch();
        AssertFindState(
            editor,
            [12, 1, 12, 1],
            [12, 1, 12, 1],
            [
                [1, 1, 1, 18],
                [2, 1, 2, 18],
                [3, 1, 3, 20],
                [4, 1, 4, 1],
                [5, 1, 5, 13],
                [6, 1, 6, 43],
                [7, 1, 7, 41],
                [8, 1, 8, 41],
                [9, 1, 9, 40],
                [10, 1, 10, 2],
                [11, 1, 11, 17],
                [12, 1, 12, 1],
            ]
        );

        findModel.MoveToPrevMatch();
        AssertFindState(
            editor,
            [11, 1, 11, 17],
            [11, 1, 11, 17],
            [
                [1, 1, 1, 18],
                [2, 1, 2, 18],
                [3, 1, 3, 20],
                [4, 1, 4, 1],
                [5, 1, 5, 13],
                [6, 1, 6, 43],
                [7, 1, 7, 41],
                [8, 1, 8, 41],
                [9, 1, 9, 40],
                [10, 1, 10, 2],
                [11, 1, 11, 17],
                [12, 1, 12, 1],
            ]
        );

        findModel.Dispose();
        findState.Dispose();
    });

    // upstream: find prev ^$
    [Fact]
    public void Find_prev_2() => FindTest(editor =>
    {
        using var findState = new FindReplaceState();
        findState.Change(new NewFindReplaceState { SearchString = "^$", IsRegex = true }, false);
        using var findModel = new FindModelBoundToEditorModel(editor, findState);

        AssertFindState(
            editor,
            [1, 1, 1, 1],
            null,
            [
                [4, 1, 4, 1],
                [12, 1, 12, 1],
            ]
        );

        findModel.MoveToPrevMatch();
        AssertFindState(
            editor,
            [12, 1, 12, 1],
            [12, 1, 12, 1],
            [
                [4, 1, 4, 1],
                [12, 1, 12, 1],
            ]
        );

        findModel.MoveToPrevMatch();
        AssertFindState(
            editor,
            [4, 1, 4, 1],
            [4, 1, 4, 1],
            [
                [4, 1, 4, 1],
                [12, 1, 12, 1],
            ]
        );

        findModel.MoveToPrevMatch();
        AssertFindState(
            editor,
            [12, 1, 12, 1],
            [12, 1, 12, 1],
            [
                [4, 1, 4, 1],
                [12, 1, 12, 1],
            ]
        );

        findModel.Dispose();
        findState.Dispose();
    });

    // upstream: replace hello
    [Fact]
    public void Replace_hello() => FindTest(editor =>
    {
        using var findState = new FindReplaceState();
        findState.Change(new NewFindReplaceState { SearchString = "hello", ReplaceString = "hi", WholeWord = true }, false);
        using var findModel = new FindModelBoundToEditorModel(editor, findState);

        AssertFindState(
            editor,
            [1, 1, 1, 1],
            null,
            [
                [6, 14, 6, 19],
                [6, 27, 6, 32],
                [7, 14, 7, 19],
                [8, 14, 8, 19]
            ]
        );

        MoveTo(editor, 6, 20);
        AssertFindState(
            editor,
            [6, 20, 6, 20],
            null,
            [
                [6, 14, 6, 19],
                [6, 27, 6, 32],
                [7, 14, 7, 19],
                [8, 14, 8, 19]
            ]
        );
        editor.GetModel()!.GetLineContent(6).ShouldBe("    cout << \"hello world, Hello!\" << endl;");

        findModel.Replace();
        AssertFindState(
            editor,
            [6, 27, 6, 32],
            [6, 27, 6, 32],
            [
                [6, 14, 6, 19],
                [6, 27, 6, 32],
                [7, 14, 7, 19],
                [8, 14, 8, 19]
            ]
        );
        editor.GetModel()!.GetLineContent(6).ShouldBe("    cout << \"hello world, Hello!\" << endl;");

        findModel.Replace();
        AssertFindState(
            editor,
            [7, 14, 7, 19],
            [7, 14, 7, 19],
            [
                [6, 14, 6, 19],
                [7, 14, 7, 19],
                [8, 14, 8, 19]
            ]
        );
        editor.GetModel()!.GetLineContent(6).ShouldBe("    cout << \"hello world, hi!\" << endl;");

        findModel.Replace();
        AssertFindState(
            editor,
            [8, 14, 8, 19],
            [8, 14, 8, 19],
            [
                [6, 14, 6, 19],
                [8, 14, 8, 19]
            ]
        );
        editor.GetModel()!.GetLineContent(7).ShouldBe("    cout << \"hi world again\" << endl;");

        findModel.Replace();
        AssertFindState(
            editor,
            [6, 14, 6, 19],
            [6, 14, 6, 19],
            [
                [6, 14, 6, 19]
            ]
        );
        editor.GetModel()!.GetLineContent(8).ShouldBe("    cout << \"hi world again\" << endl;");

        findModel.Replace();
        AssertFindState(
            editor,
            [6, 16, 6, 16],
            null,
            []
        );
        editor.GetModel()!.GetLineContent(6).ShouldBe("    cout << \"hi world, hi!\" << endl;");

        findModel.Dispose();
        findState.Dispose();
    });

    // upstream: replace bla
    [Fact]
    public void Replace_bla() => FindTest(editor =>
    {
        using var findState = new FindReplaceState();
        findState.Change(new NewFindReplaceState { SearchString = "bla", ReplaceString = "ciao" }, false);
        using var findModel = new FindModelBoundToEditorModel(editor, findState);

        AssertFindState(
            editor,
            [1, 1, 1, 1],
            null,
            [
                [11, 4, 11, 7],
                [11, 7, 11, 10],
                [11, 10, 11, 13]
            ]
        );

        findModel.Replace();
        AssertFindState(
            editor,
            [11, 4, 11, 7],
            [11, 4, 11, 7],
            [
                [11, 4, 11, 7],
                [11, 7, 11, 10],
                [11, 10, 11, 13]
            ]
        );
        editor.GetModel()!.GetLineContent(11).ShouldBe("// blablablaciao");

        findModel.Replace();
        AssertFindState(
            editor,
            [11, 8, 11, 11],
            [11, 8, 11, 11],
            [
                [11, 8, 11, 11],
                [11, 11, 11, 14]
            ]
        );
        editor.GetModel()!.GetLineContent(11).ShouldBe("// ciaoblablaciao");

        findModel.Replace();
        AssertFindState(
            editor,
            [11, 12, 11, 15],
            [11, 12, 11, 15],
            [
                [11, 12, 11, 15]
            ]
        );
        editor.GetModel()!.GetLineContent(11).ShouldBe("// ciaociaoblaciao");

        findModel.Replace();
        AssertFindState(
            editor,
            [11, 16, 11, 16],
            null,
            []
        );
        editor.GetModel()!.GetLineContent(11).ShouldBe("// ciaociaociaociao");

        findModel.Dispose();
        findState.Dispose();
    });

    // upstream: replaceAll hello
    [Fact]
    public void ReplaceAll_hello() => FindTest(editor =>
    {
        using var findState = new FindReplaceState();
        findState.Change(new NewFindReplaceState { SearchString = "hello", ReplaceString = "hi", WholeWord = true }, false);
        using var findModel = new FindModelBoundToEditorModel(editor, findState);

        AssertFindState(
            editor,
            [1, 1, 1, 1],
            null,
            [
                [6, 14, 6, 19],
                [6, 27, 6, 32],
                [7, 14, 7, 19],
                [8, 14, 8, 19]
            ]
        );

        MoveTo(editor, 6, 20);
        AssertFindState(
            editor,
            [6, 20, 6, 20],
            null,
            [
                [6, 14, 6, 19],
                [6, 27, 6, 32],
                [7, 14, 7, 19],
                [8, 14, 8, 19]
            ]
        );
        editor.GetModel()!.GetLineContent(6).ShouldBe("    cout << \"hello world, Hello!\" << endl;");

        findModel.ReplaceAll();
        AssertFindState(
            editor,
            [6, 17, 6, 17],
            null,
            []
        );
        editor.GetModel()!.GetLineContent(6).ShouldBe("    cout << \"hi world, hi!\" << endl;");
        editor.GetModel()!.GetLineContent(7).ShouldBe("    cout << \"hi world again\" << endl;");
        editor.GetModel()!.GetLineContent(8).ShouldBe("    cout << \"hi world again\" << endl;");

        findModel.Dispose();
        findState.Dispose();
    });

    // upstream: replaceAll two spaces with one space
    [Fact]
    public void ReplaceAll_two_spaces_with_one_space() => FindTest(editor =>
    {
        using var findState = new FindReplaceState();
        findState.Change(new NewFindReplaceState { SearchString = "  ", ReplaceString = " " }, false);
        using var findModel = new FindModelBoundToEditorModel(editor, findState);

        AssertFindState(
            editor,
            [1, 1, 1, 1],
            null,
            [
                [6, 1, 6, 3],
                [6, 3, 6, 5],
                [7, 1, 7, 3],
                [7, 3, 7, 5],
                [8, 1, 8, 3],
                [8, 3, 8, 5],
                [9, 1, 9, 3],
                [9, 3, 9, 5]
            ]
        );

        findModel.ReplaceAll();
        AssertFindState(
            editor,
            [1, 1, 1, 1],
            null,
            [
                [6, 1, 6, 3],
                [7, 1, 7, 3],
                [8, 1, 8, 3],
                [9, 1, 9, 3]
            ]
        );
        editor.GetModel()!.GetLineContent(6).ShouldBe("  cout << \"hello world, Hello!\" << endl;");
        editor.GetModel()!.GetLineContent(7).ShouldBe("  cout << \"hello world again\" << endl;");
        editor.GetModel()!.GetLineContent(8).ShouldBe("  cout << \"Hello world again\" << endl;");
        editor.GetModel()!.GetLineContent(9).ShouldBe("  cout << \"helloworld again\" << endl;");

        findModel.Dispose();
        findState.Dispose();
    });

    // upstream: replaceAll bla
    [Fact]
    public void ReplaceAll_bla() => FindTest(editor =>
    {
        using var findState = new FindReplaceState();
        findState.Change(new NewFindReplaceState { SearchString = "bla", ReplaceString = "ciao" }, false);
        using var findModel = new FindModelBoundToEditorModel(editor, findState);

        AssertFindState(
            editor,
            [1, 1, 1, 1],
            null,
            [
                [11, 4, 11, 7],
                [11, 7, 11, 10],
                [11, 10, 11, 13]
            ]
        );

        findModel.ReplaceAll();
        AssertFindState(
            editor,
            [1, 1, 1, 1],
            null,
            []
        );
        editor.GetModel()!.GetLineContent(11).ShouldBe("// ciaociaociaociao");

        findModel.Dispose();
        findState.Dispose();
    });

    // upstream: replaceAll bla with \\t\\n
    [Fact]
    public void ReplaceAll_bla_with_t_n() => FindTest(editor =>
    {
        using var findState = new FindReplaceState();
        findState.Change(new NewFindReplaceState { SearchString = "bla", ReplaceString = "<\\n\\t>", IsRegex = true }, false);
        using var findModel = new FindModelBoundToEditorModel(editor, findState);

        AssertFindState(
            editor,
            [1, 1, 1, 1],
            null,
            [
                [11, 4, 11, 7],
                [11, 7, 11, 10],
                [11, 10, 11, 13]
            ]
        );

        findModel.ReplaceAll();
        AssertFindState(
            editor,
            [1, 1, 1, 1],
            null,
            []
        );
        editor.GetModel()!.GetLineContent(11).ShouldBe("// <");
        editor.GetModel()!.GetLineContent(12).ShouldBe("\t><");
        editor.GetModel()!.GetLineContent(13).ShouldBe("\t><");
        editor.GetModel()!.GetLineContent(14).ShouldBe("\t>ciao");

        findModel.Dispose();
        findState.Dispose();
    });

    // upstream: issue #3516: "replace all" moves page/cursor/focus/scroll to the place of the last replacement
    [Fact]
    public void Issue_3516_replace_all_moves_page_cursor_focus_scroll_to_the_place_of_the_last_replacement() => FindTest(editor =>
    {
        using var findState = new FindReplaceState();
        findState.Change(new NewFindReplaceState { SearchString = "include", ReplaceString = "bar" }, false);
        using var findModel = new FindModelBoundToEditorModel(editor, findState);

        AssertFindState(
            editor,
            [1, 1, 1, 1],
            null,
            [
                [2, 2, 2, 9],
                [3, 2, 3, 9]
            ]
        );

        findModel.ReplaceAll();
        AssertFindState(
            editor,
            [1, 1, 1, 1],
            null,
            []
        );

        editor.GetModel()!.GetLineContent(2).ShouldBe("#bar \"cool.h\"");
        editor.GetModel()!.GetLineContent(3).ShouldBe("#bar <iostream>");

        findModel.Dispose();
        findState.Dispose();
    });

    // upstream: listens to model content changes
    [Fact]
    public void Listens_to_model_content_changes() => FindTest(editor =>
    {
        using var findState = new FindReplaceState();
        findState.Change(new NewFindReplaceState { SearchString = "hello", ReplaceString = "hi", WholeWord = true }, false);
        using var findModel = new FindModelBoundToEditorModel(editor, findState);

        AssertFindState(
            editor,
            [1, 1, 1, 1],
            null,
            [
                [6, 14, 6, 19],
                [6, 27, 6, 32],
                [7, 14, 7, 19],
                [8, 14, 8, 19]
            ]
        );

        editor.GetModel()!.SetValue("hello\nhi");
        AssertFindState(
            editor,
            [1, 1, 1, 1],
            null,
            []
        );

        findModel.Dispose();
        findState.Dispose();
    });

    // upstream: selectAllMatches
    [Fact]
    public void SelectAllMatches() => FindTest(editor =>
    {
        using var findState = new FindReplaceState();
        findState.Change(new NewFindReplaceState { SearchString = "hello", ReplaceString = "hi", WholeWord = true }, false);
        using var findModel = new FindModelBoundToEditorModel(editor, findState);

        AssertFindState(
            editor,
            [1, 1, 1, 1],
            null,
            [
                [6, 14, 6, 19],
                [6, 27, 6, 32],
                [7, 14, 7, 19],
                [8, 14, 8, 19]
            ]
        );

        findModel.SelectAllMatches();

        AssertSelections(editor,
            new TextSelection(6, 14, 6, 19),
            new TextSelection(6, 27, 6, 32),
            new TextSelection(7, 14, 7, 19),
            new TextSelection(8, 14, 8, 19));

        AssertFindState(
            editor,
            [6, 14, 6, 19],
            null,
            [
                [6, 14, 6, 19],
                [6, 27, 6, 32],
                [7, 14, 7, 19],
                [8, 14, 8, 19]
            ]
        );

        findModel.Dispose();
        findState.Dispose();
    });

    // upstream: issue #14143 selectAllMatches should maintain primary cursor if feasible
    [Fact]
    public void Issue_14143_selectAllMatches_should_maintain_primary_cursor_if_feasible() => FindTest(editor =>
    {
        using var findState = new FindReplaceState();
        findState.Change(new NewFindReplaceState { SearchString = "hello", ReplaceString = "hi", WholeWord = true }, false);
        using var findModel = new FindModelBoundToEditorModel(editor, findState);

        AssertFindState(
            editor,
            [1, 1, 1, 1],
            null,
            [
                [6, 14, 6, 19],
                [6, 27, 6, 32],
                [7, 14, 7, 19],
                [8, 14, 8, 19]
            ]
        );

        editor.SetSelection(new TextRange(7, 14, 7, 19));

        findModel.SelectAllMatches();

        AssertSelections(editor,
            new TextSelection(7, 14, 7, 19),
            new TextSelection(6, 14, 6, 19),
            new TextSelection(6, 27, 6, 32),
            new TextSelection(8, 14, 8, 19));

        editor.GetSelection().ShouldBe(new TextSelection(7, 14, 7, 19));

        AssertFindState(
            editor,
            [7, 14, 7, 19],
            null,
            [
                [6, 14, 6, 19],
                [6, 27, 6, 32],
                [7, 14, 7, 19],
                [8, 14, 8, 19]
            ]
        );

        findModel.Dispose();
        findState.Dispose();
    });

    // upstream: issue #1914: NPE when there is only one find match
    [Fact]
    public void Issue_1914_NPE_when_there_is_only_one_find_match() => FindTest(editor =>
    {
        using var findState = new FindReplaceState();
        findState.Change(new NewFindReplaceState { SearchString = "cool.h" }, false);
        using var findModel = new FindModelBoundToEditorModel(editor, findState);

        AssertFindState(
            editor,
            [1, 1, 1, 1],
            null,
            [
                [2, 11, 2, 17]
            ]
        );

        findModel.MoveToNextMatch();
        AssertFindState(
            editor,
            [2, 11, 2, 17],
            [2, 11, 2, 17],
            [
                [2, 11, 2, 17]
            ]
        );

        findModel.MoveToNextMatch();
        AssertFindState(
            editor,
            [2, 11, 2, 17],
            [2, 11, 2, 17],
            [
                [2, 11, 2, 17]
            ]
        );

        findModel.Dispose();
        findState.Dispose();
    });

    // upstream: replace when search string has look ahed regex
    [Fact]
    public void Replace_when_search_string_has_look_ahed_regex() => FindTest(editor =>
    {
        using var findState = new FindReplaceState();
        findState.Change(new NewFindReplaceState { SearchString = "hello(?=\\sworld)", ReplaceString = "hi", IsRegex = true }, false);
        using var findModel = new FindModelBoundToEditorModel(editor, findState);

        AssertFindState(
            editor,
            [1, 1, 1, 1],
            null,
            [
                [6, 14, 6, 19],
                [7, 14, 7, 19],
                [8, 14, 8, 19]
            ]
        );

        findModel.Replace();

        AssertFindState(
            editor,
            [6, 14, 6, 19],
            [6, 14, 6, 19],
            [
                [6, 14, 6, 19],
                [7, 14, 7, 19],
                [8, 14, 8, 19]
            ]
        );
        editor.GetModel()!.GetLineContent(6).ShouldBe("    cout << \"hello world, Hello!\" << endl;");

        findModel.Replace();
        AssertFindState(
            editor,
            [7, 14, 7, 19],
            [7, 14, 7, 19],
            [
                [7, 14, 7, 19],
                [8, 14, 8, 19]
            ]
        );
        editor.GetModel()!.GetLineContent(6).ShouldBe("    cout << \"hi world, Hello!\" << endl;");

        findModel.Replace();
        AssertFindState(
            editor,
            [8, 14, 8, 19],
            [8, 14, 8, 19],
            [
                [8, 14, 8, 19]
            ]
        );
        editor.GetModel()!.GetLineContent(7).ShouldBe("    cout << \"hi world again\" << endl;");

        findModel.Replace();
        AssertFindState(
            editor,
            [8, 16, 8, 16],
            null,
            []
        );
        editor.GetModel()!.GetLineContent(8).ShouldBe("    cout << \"hi world again\" << endl;");

        findModel.Dispose();
        findState.Dispose();
    });

    // upstream: replace when search string has look ahed regex and cursor is at the last find match
    [Fact]
    public void Replace_when_search_string_has_look_ahed_regex_and_cursor_is_at_the_last_find_match() => FindTest(editor =>
    {
        using var findState = new FindReplaceState();
        findState.Change(new NewFindReplaceState { SearchString = "hello(?=\\sworld)", ReplaceString = "hi", IsRegex = true }, false);
        using var findModel = new FindModelBoundToEditorModel(editor, findState);

        MoveTo(editor, 8, 14);

        AssertFindState(
            editor,
            [8, 14, 8, 14],
            null,
            [
                [6, 14, 6, 19],
                [7, 14, 7, 19],
                [8, 14, 8, 19]
            ]
        );

        findModel.Replace();

        AssertFindState(
            editor,
            [8, 14, 8, 19],
            [8, 14, 8, 19],
            [
                [6, 14, 6, 19],
                [7, 14, 7, 19],
                [8, 14, 8, 19]
            ]
        );

        editor.GetModel()!.GetLineContent(8).ShouldBe("    cout << \"Hello world again\" << endl;");

        findModel.Replace();
        AssertFindState(
            editor,
            [6, 14, 6, 19],
            [6, 14, 6, 19],
            [
                [6, 14, 6, 19],
                [7, 14, 7, 19],
            ]
        );
        editor.GetModel()!.GetLineContent(8).ShouldBe("    cout << \"hi world again\" << endl;");

        findModel.Replace();
        AssertFindState(
            editor,
            [7, 14, 7, 19],
            [7, 14, 7, 19],
            [
                [7, 14, 7, 19]
            ]
        );
        editor.GetModel()!.GetLineContent(6).ShouldBe("    cout << \"hi world, Hello!\" << endl;");

        findModel.Replace();
        AssertFindState(
            editor,
            [7, 16, 7, 16],
            null,
            []
        );
        editor.GetModel()!.GetLineContent(7).ShouldBe("    cout << \"hi world again\" << endl;");

        findModel.Dispose();
        findState.Dispose();
    });

    // upstream: replaceAll when search string has look ahed regex
    [Fact]
    public void ReplaceAll_when_search_string_has_look_ahed_regex() => FindTest(editor =>
    {
        using var findState = new FindReplaceState();
        findState.Change(new NewFindReplaceState { SearchString = "hello(?=\\sworld)", ReplaceString = "hi", IsRegex = true }, false);
        using var findModel = new FindModelBoundToEditorModel(editor, findState);

        AssertFindState(
            editor,
            [1, 1, 1, 1],
            null,
            [
                [6, 14, 6, 19],
                [7, 14, 7, 19],
                [8, 14, 8, 19]
            ]
        );

        findModel.ReplaceAll();

        editor.GetModel()!.GetLineContent(6).ShouldBe("    cout << \"hi world, Hello!\" << endl;");
        editor.GetModel()!.GetLineContent(7).ShouldBe("    cout << \"hi world again\" << endl;");
        editor.GetModel()!.GetLineContent(8).ShouldBe("    cout << \"hi world again\" << endl;");

        AssertFindState(
            editor,
            [1, 1, 1, 1],
            null,
            []
        );

        findModel.Dispose();
        findState.Dispose();
    });

    // upstream: replace when search string has look ahed regex and replace string has capturing groups
    [Fact]
    public void Replace_when_search_string_has_look_ahed_regex_and_replace_string_has_capturing_groups() => FindTest(editor =>
    {
        using var findState = new FindReplaceState();
        findState.Change(new NewFindReplaceState { SearchString = "hel(lo)(?=\\sworld)", ReplaceString = "hi$1", IsRegex = true }, false);
        using var findModel = new FindModelBoundToEditorModel(editor, findState);

        AssertFindState(
            editor,
            [1, 1, 1, 1],
            null,
            [
                [6, 14, 6, 19],
                [7, 14, 7, 19],
                [8, 14, 8, 19]
            ]
        );

        findModel.Replace();

        AssertFindState(
            editor,
            [6, 14, 6, 19],
            [6, 14, 6, 19],
            [
                [6, 14, 6, 19],
                [7, 14, 7, 19],
                [8, 14, 8, 19]
            ]
        );
        editor.GetModel()!.GetLineContent(6).ShouldBe("    cout << \"hello world, Hello!\" << endl;");

        findModel.Replace();
        AssertFindState(
            editor,
            [7, 14, 7, 19],
            [7, 14, 7, 19],
            [
                [7, 14, 7, 19],
                [8, 14, 8, 19]
            ]
        );
        editor.GetModel()!.GetLineContent(6).ShouldBe("    cout << \"hilo world, Hello!\" << endl;");

        findModel.Replace();
        AssertFindState(
            editor,
            [8, 14, 8, 19],
            [8, 14, 8, 19],
            [
                [8, 14, 8, 19]
            ]
        );
        editor.GetModel()!.GetLineContent(7).ShouldBe("    cout << \"hilo world again\" << endl;");

        findModel.Replace();
        AssertFindState(
            editor,
            [8, 18, 8, 18],
            null,
            []
        );
        editor.GetModel()!.GetLineContent(8).ShouldBe("    cout << \"hilo world again\" << endl;");

        findModel.Dispose();
        findState.Dispose();
    });

    // upstream: replaceAll when search string has look ahed regex and replace string has capturing groups
    [Fact]
    public void ReplaceAll_when_search_string_has_look_ahed_regex_and_replace_string_has_capturing_groups() => FindTest(editor =>
    {
        using var findState = new FindReplaceState();
        findState.Change(new NewFindReplaceState { SearchString = "wo(rl)d(?=.*;$)", ReplaceString = "gi$1", IsRegex = true }, false);
        using var findModel = new FindModelBoundToEditorModel(editor, findState);

        AssertFindState(
            editor,
            [1, 1, 1, 1],
            null,
            [
                [6, 20, 6, 25],
                [7, 20, 7, 25],
                [8, 20, 8, 25],
                [9, 19, 9, 24]
            ]
        );

        findModel.ReplaceAll();

        editor.GetModel()!.GetLineContent(6).ShouldBe("    cout << \"hello girl, Hello!\" << endl;");
        editor.GetModel()!.GetLineContent(7).ShouldBe("    cout << \"hello girl again\" << endl;");
        editor.GetModel()!.GetLineContent(8).ShouldBe("    cout << \"Hello girl again\" << endl;");
        editor.GetModel()!.GetLineContent(9).ShouldBe("    cout << \"hellogirl again\" << endl;");

        AssertFindState(
            editor,
            [1, 1, 1, 1],
            null,
            []
        );

        findModel.Dispose();
        findState.Dispose();
    });

    // upstream: replaceAll when search string is multiline and has look ahed regex and replace string has capturing groups
    [Fact]
    public void ReplaceAll_when_search_string_is_multiline_and_has_look_ahed_regex_and_replace_string_has_capturing_groups() => FindTest(editor =>
    {
        using var findState = new FindReplaceState();
        findState.Change(new NewFindReplaceState { SearchString = "wo(rl)d(.*;\\n)(?=.*hello)", ReplaceString = "gi$1$2", IsRegex = true, MatchCase = true }, false);
        using var findModel = new FindModelBoundToEditorModel(editor, findState);

        AssertFindState(
            editor,
            [1, 1, 1, 1],
            null,
            [
                [6, 20, 7, 1],
                [8, 20, 9, 1]
            ]
        );

        findModel.ReplaceAll();

        editor.GetModel()!.GetLineContent(6).ShouldBe("    cout << \"hello girl, Hello!\" << endl;");
        editor.GetModel()!.GetLineContent(8).ShouldBe("    cout << \"Hello girl again\" << endl;");

        AssertFindState(
            editor,
            [1, 1, 1, 1],
            null,
            []
        );

        findModel.Dispose();
        findState.Dispose();
    });

    // upstream: replaceAll preserving case
    [Fact]
    public void ReplaceAll_preserving_case() => FindTest(editor =>
    {
        using var findState = new FindReplaceState();
        findState.Change(new NewFindReplaceState { SearchString = "hello", ReplaceString = "goodbye", IsRegex = false, MatchCase = false, PreserveCase = true }, false);
        using var findModel = new FindModelBoundToEditorModel(editor, findState);

        AssertFindState(
            editor,
            [1, 1, 1, 1],
            null,
            [
                [6, 14, 6, 19],
                [6, 27, 6, 32],
                [7, 14, 7, 19],
                [8, 14, 8, 19],
                [9, 14, 9, 19],
            ]
        );

        findModel.ReplaceAll();

        editor.GetModel()!.GetLineContent(6).ShouldBe("    cout << \"goodbye world, Goodbye!\" << endl;");
        editor.GetModel()!.GetLineContent(7).ShouldBe("    cout << \"goodbye world again\" << endl;");
        editor.GetModel()!.GetLineContent(8).ShouldBe("    cout << \"Goodbye world again\" << endl;");
        editor.GetModel()!.GetLineContent(9).ShouldBe("    cout << \"goodbyeworld again\" << endl;");

        AssertFindState(
            editor,
            [1, 1, 1, 1],
            null,
            []
        );

        findModel.Dispose();
        findState.Dispose();
    });

    // upstream: issue #18711 replaceAll with empty string
    [Fact]
    public void Issue_18711_replaceAll_with_empty_string() => FindTest(editor =>
    {
        using var findState = new FindReplaceState();
        findState.Change(new NewFindReplaceState { SearchString = "hello", ReplaceString = "", WholeWord = true }, false);
        using var findModel = new FindModelBoundToEditorModel(editor, findState);

        AssertFindState(
            editor,
            [1, 1, 1, 1],
            null,
            [
                [6, 14, 6, 19],
                [6, 27, 6, 32],
                [7, 14, 7, 19],
                [8, 14, 8, 19]
            ]
        );

        findModel.ReplaceAll();
        AssertFindState(
            editor,
            [1, 1, 1, 1],
            null,
            []
        );
        editor.GetModel()!.GetLineContent(6).ShouldBe("    cout << \" world, !\" << endl;");
        editor.GetModel()!.GetLineContent(7).ShouldBe("    cout << \" world again\" << endl;");
        editor.GetModel()!.GetLineContent(8).ShouldBe("    cout << \" world again\" << endl;");

        findModel.Dispose();
        findState.Dispose();
    });

    // upstream: issue #32522 replaceAll with ^ on more than 1000 matches
    [Fact]
    public void Issue_32522_replaceAll_with_on_more_than_1000_matches() => FindTest(editor =>
    {
        var initialText = "";
        for (var i = 0; i < 1100; i++)
        {
            initialText += "line" + i + "\n";
        }
        editor.GetModel()!.SetValue(initialText);
        using var findState = new FindReplaceState();
        findState.Change(new NewFindReplaceState { SearchString = "^", ReplaceString = "a ", IsRegex = true }, false);
        using var findModel = new FindModelBoundToEditorModel(editor, findState);

        findModel.ReplaceAll();

        var expectedText = "";
        for (var i = 0; i < 1100; i++)
        {
            expectedText += "a line" + i + "\n";
        }
        expectedText += "a ";
        editor.GetModel()!.GetValue().ShouldBe(expectedText);

        findModel.Dispose();
        findState.Dispose();
    });

    // upstream: issue #19740 Find and replace capture group/backreference inserts `undefined` instead of empty string
    [Fact]
    public void Issue_19740_Find_and_replace_capture_group_backreference_inserts_undefined_instead_of_empty_string() => FindTest(editor =>
    {
        using var findState = new FindReplaceState();
        findState.Change(new NewFindReplaceState { SearchString = "hello(z)?", ReplaceString = "hi$1", IsRegex = true, MatchCase = true }, false);
        using var findModel = new FindModelBoundToEditorModel(editor, findState);

        AssertFindState(
            editor,
            [1, 1, 1, 1],
            null,
            [
                [6, 14, 6, 19],
                [7, 14, 7, 19],
                [9, 14, 9, 19]
            ]
        );

        findModel.ReplaceAll();
        AssertFindState(
            editor,
            [1, 1, 1, 1],
            null,
            []
        );
        editor.GetModel()!.GetLineContent(6).ShouldBe("    cout << \"hi world, Hello!\" << endl;");
        editor.GetModel()!.GetLineContent(7).ShouldBe("    cout << \"hi world again\" << endl;");
        editor.GetModel()!.GetLineContent(9).ShouldBe("    cout << \"hiworld again\" << endl;");

        findModel.Dispose();
        findState.Dispose();
    });

    // upstream: issue #27083. search scope works even if it is a single line
    [Fact]
    public void Issue_27083_search_scope_works_even_if_it_is_a_single_line() => FindTest(editor =>
    {
        using var findState = new FindReplaceState();
        findState.Change(new NewFindReplaceState { SearchString = "hello", WholeWord = true, SearchScope = [new TextRange(7, 1, 8, 1)] }, false);
        using var findModel = new FindModelBoundToEditorModel(editor, findState);

        AssertFindState(
            editor,
            [1, 1, 1, 1],
            null,
            [
                [7, 14, 7, 19]
            ]
        );

        findModel.Dispose();
        findState.Dispose();
    });

    // upstream: issue #3516: Control behavior of "Next" operations (not looping back to beginning)
    [Fact]
    public void Issue_3516_Control_behavior_of_Next_operations_not_looping_back_to_beginning() => FindTest(editor =>
    {
        using var findState = new FindReplaceState();
        findState.Change(new NewFindReplaceState { SearchString = "hello", Loop = false }, false);
        using var findModel = new FindModelBoundToEditorModel(editor, findState);

        findState.MatchesCount.ShouldBe(5);

        // Test next operations
        findState.MatchesPosition.ShouldBe(0);
        findState.CanNavigateForward().ShouldBe(true);
        findState.CanNavigateBack().ShouldBe(true);

        findModel.MoveToNextMatch();
        findState.MatchesPosition.ShouldBe(1);
        findState.CanNavigateForward().ShouldBe(true);
        findState.CanNavigateBack().ShouldBe(false);

        findModel.MoveToNextMatch();
        findState.MatchesPosition.ShouldBe(2);
        findState.CanNavigateForward().ShouldBe(true);
        findState.CanNavigateBack().ShouldBe(true);

        findModel.MoveToNextMatch();
        findState.MatchesPosition.ShouldBe(3);
        findState.CanNavigateForward().ShouldBe(true);
        findState.CanNavigateBack().ShouldBe(true);

        findModel.MoveToNextMatch();
        findState.MatchesPosition.ShouldBe(4);
        findState.CanNavigateForward().ShouldBe(true);
        findState.CanNavigateBack().ShouldBe(true);

        findModel.MoveToNextMatch();
        findState.MatchesPosition.ShouldBe(5);
        findState.CanNavigateForward().ShouldBe(false);
        findState.CanNavigateBack().ShouldBe(true);

        findModel.MoveToNextMatch();
        findState.MatchesPosition.ShouldBe(5);
        findState.CanNavigateForward().ShouldBe(false);
        findState.CanNavigateBack().ShouldBe(true);

        findModel.MoveToNextMatch();
        findState.MatchesPosition.ShouldBe(5);
        findState.CanNavigateForward().ShouldBe(false);
        findState.CanNavigateBack().ShouldBe(true);

        // Test previous operations
        findModel.MoveToPrevMatch();
        findState.MatchesPosition.ShouldBe(4);
        findState.CanNavigateForward().ShouldBe(true);
        findState.CanNavigateBack().ShouldBe(true);

        findModel.MoveToPrevMatch();
        findState.MatchesPosition.ShouldBe(3);
        findState.CanNavigateForward().ShouldBe(true);
        findState.CanNavigateBack().ShouldBe(true);

        findModel.MoveToPrevMatch();
        findState.MatchesPosition.ShouldBe(2);
        findState.CanNavigateForward().ShouldBe(true);
        findState.CanNavigateBack().ShouldBe(true);

        findModel.MoveToPrevMatch();
        findState.MatchesPosition.ShouldBe(1);
        findState.CanNavigateForward().ShouldBe(true);
        findState.CanNavigateBack().ShouldBe(false);

        findModel.MoveToPrevMatch();
        findState.MatchesPosition.ShouldBe(1);
        findState.CanNavigateForward().ShouldBe(true);
        findState.CanNavigateBack().ShouldBe(false);

        findModel.MoveToPrevMatch();
        findState.MatchesPosition.ShouldBe(1);
        findState.CanNavigateForward().ShouldBe(true);
        findState.CanNavigateBack().ShouldBe(false);

    });

    // upstream: issue #3516: Control behavior of "Next" operations (looping back to beginning)
    [Fact]
    public void Issue_3516_Control_behavior_of_Next_operations_looping_back_to_beginning() => FindTest(editor =>
    {
        using var findState = new FindReplaceState();
        findState.Change(new NewFindReplaceState { SearchString = "hello" }, false);
        using var findModel = new FindModelBoundToEditorModel(editor, findState);

        findState.MatchesCount.ShouldBe(5);

        // Test next operations
        findState.MatchesPosition.ShouldBe(0);
        findState.CanNavigateForward().ShouldBe(true);
        findState.CanNavigateBack().ShouldBe(true);

        findModel.MoveToNextMatch();
        findState.MatchesPosition.ShouldBe(1);
        findState.CanNavigateForward().ShouldBe(true);
        findState.CanNavigateBack().ShouldBe(true);

        findModel.MoveToNextMatch();
        findState.MatchesPosition.ShouldBe(2);
        findState.CanNavigateForward().ShouldBe(true);
        findState.CanNavigateBack().ShouldBe(true);

        findModel.MoveToNextMatch();
        findState.MatchesPosition.ShouldBe(3);
        findState.CanNavigateForward().ShouldBe(true);
        findState.CanNavigateBack().ShouldBe(true);

        findModel.MoveToNextMatch();
        findState.MatchesPosition.ShouldBe(4);
        findState.CanNavigateForward().ShouldBe(true);
        findState.CanNavigateBack().ShouldBe(true);

        findModel.MoveToNextMatch();
        findState.MatchesPosition.ShouldBe(5);
        findState.CanNavigateForward().ShouldBe(true);
        findState.CanNavigateBack().ShouldBe(true);

        findModel.MoveToNextMatch();
        findState.MatchesPosition.ShouldBe(1);
        findState.CanNavigateForward().ShouldBe(true);
        findState.CanNavigateBack().ShouldBe(true);

        findModel.MoveToNextMatch();
        findState.MatchesPosition.ShouldBe(2);
        findState.CanNavigateForward().ShouldBe(true);
        findState.CanNavigateBack().ShouldBe(true);

        // Test previous operations
        findModel.MoveToPrevMatch();
        findState.MatchesPosition.ShouldBe(1);
        findState.CanNavigateForward().ShouldBe(true);
        findState.CanNavigateBack().ShouldBe(true);

        findModel.MoveToPrevMatch();
        findState.MatchesPosition.ShouldBe(5);
        findState.CanNavigateForward().ShouldBe(true);
        findState.CanNavigateBack().ShouldBe(true);

        findModel.MoveToPrevMatch();
        findState.MatchesPosition.ShouldBe(4);
        findState.CanNavigateForward().ShouldBe(true);
        findState.CanNavigateBack().ShouldBe(true);

        findModel.MoveToPrevMatch();
        findState.MatchesPosition.ShouldBe(3);
        findState.CanNavigateForward().ShouldBe(true);
        findState.CanNavigateBack().ShouldBe(true);

        findModel.MoveToPrevMatch();
        findState.MatchesPosition.ShouldBe(2);
        findState.CanNavigateForward().ShouldBe(true);
        findState.CanNavigateBack().ShouldBe(true);

        findModel.MoveToPrevMatch();
        findState.MatchesPosition.ShouldBe(1);
        findState.CanNavigateForward().ShouldBe(true);
        findState.CanNavigateBack().ShouldBe(true);

    });


    // upstream: issue #288515: Wrong current index in find widget if matches > 1000
    [Fact]
    public void Issue_288515_wrong_current_index_in_find_widget_if_matches_over_1000()
    {
        // Create 1001 lines of 'hello'
        var textArr = Enumerable.Repeat("hello", 1001).ToArray();
        TestFindEditor.With(textArr, editor =>
        {
            // Place cursor at line 900, selecting 'hello'
            editor.SetSelection(new TextSelection(900, 1, 900, 6));

            using var findState = new FindReplaceState();
            findState.Change(new NewFindReplaceState { SearchString = "hello" }, false);
            using var findModel = new FindModelBoundToEditorModel(editor, findState);

            findState.MatchesCount.ShouldBe(1001);
            // With cursor selecting 'hello' at line 900, matchesPosition should be 900
            findState.MatchesPosition.ShouldBe(900);
        });
    }
}
