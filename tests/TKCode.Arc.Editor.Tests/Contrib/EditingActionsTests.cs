// Not a port: upstream has no tests for these action runs (comment.ts, transpose.ts, insertFinalNewLine.ts, the
// indentation actions, the word command registrations and SmartSelectController.run). They run each action on the
// test editor the way the control does.

using Shouldly;
using TKCode.Arc.Editor.Config;
using TKCode.Arc.Editor.Contrib;
using TKCode.Arc.Editor.Contrib.CaretOperations;
using TKCode.Arc.Editor.Contrib.Comment;
using TKCode.Arc.Editor.Contrib.Indentation;
using TKCode.Arc.Editor.Contrib.InsertFinalNewLine;
using TKCode.Arc.Editor.Contrib.SmartSelect;
using WordCommandsImpl = TKCode.Arc.Editor.Contrib.WordOperations.WordCommands;
using TKCode.Arc.Editor.Languages;
using TKCode.Arc.Editor.Model;
using Xunit;
using static TKCode.Arc.Editor.Tests.Contrib.EditingAssert;

namespace TKCode.Arc.Editor.Tests.Contrib;

public sealed class EditingActionsTests
{
    private const string CommentLanguageId = "commentLang";

    private static void WithCommentEditor(string[] lines, Action<EditingTestEditor> callback)
    {
        using var service = new LanguageConfigurationService();
        service.Register(CommentLanguageId, new LanguageConfiguration
        {
            Comments = new CommentRule { LineComment = "//", BlockComment = ("/*", "*/") },
            Brackets = [("{", "}"), ("(", ")")],
        });
        var options = new TestCodeEditorOptions { LanguageId = CommentLanguageId, LanguageConfigurationService = service };
        EditingTestEditor.WithTestCodeEditor(lines, options, callback);
    }

    [Fact]
    public void ToggleLineCommentAddsAndRemoves()
    {
        WithCommentEditor(["a", "  b", "c"], editor =>
        {
            editor.SetSelection(new TextSelection(1, 1, 2, 2));
            CommentOperations.CommentLine(editor, LineCommentType.Toggle, "editor.action.commentLine");
            editor.GetModel().GetLinesContent().ShouldBe(["// a", "//   b", "c"]);

            CommentOperations.CommentLine(editor, LineCommentType.Toggle, "editor.action.commentLine");
            editor.GetModel().GetLinesContent().ShouldBe(["a", "  b", "c"]);

            // one undo step per action
            editor.Undo();
            editor.GetModel().GetLinesContent().ShouldBe(["// a", "//   b", "c"]);
        });
    }

    [Fact]
    public void AddAndRemoveLineCommentForce()
    {
        WithCommentEditor(["// a", "b"], editor =>
        {
            editor.SetSelection(new TextSelection(1, 1, 2, 2));
            CommentOperations.CommentLine(editor, LineCommentType.ForceAdd, "editor.action.addCommentLine");
            editor.GetModel().GetLinesContent().ShouldBe(["// // a", "// b"]);

            CommentOperations.CommentLine(editor, LineCommentType.ForceRemove, "editor.action.removeCommentLine");
            editor.GetModel().GetLinesContent().ShouldBe(["// a", "b"]);
        });
    }

    [Fact]
    public void ToggleLineCommentWithTwoCursorsOnOneLineCommentsItOnce()
    {
        WithCommentEditor(["abc", "def"], editor =>
        {
            editor.SetSelections([new TextSelection(1, 2, 1, 2), new TextSelection(1, 3, 1, 3)]);
            CommentOperations.CommentLine(editor, LineCommentType.Toggle, "editor.action.commentLine");
            editor.GetModel().GetLinesContent().ShouldBe(["// abc", "def"]);
        });
    }

    [Fact]
    public void BlockCommentWrapsTheSelection()
    {
        WithCommentEditor(["let a = 1;"], editor =>
        {
            editor.SetSelection(new TextSelection(1, 5, 1, 6));
            CommentOperations.BlockComment(editor, "editor.action.blockComment");
            editor.GetValue().ShouldBe("let /* a */ = 1;");

            editor.CommentsOptions = new EditorCommentsOptions(InsertSpace: false);
            editor.SetSelection(new TextSelection(1, 13, 1, 14));
            CommentOperations.BlockComment(editor, "editor.action.blockComment");
            editor.GetValue().ShouldBe("let /* a */ /*=*/ 1;");
        });
    }

    [Fact]
    public void TransposeLetters()
    {
        EditingTestEditor.WithTestCodeEditor(["abcd", "x"], null, editor =>
        {
            // between b and c: swaps them, the cursor moves right
            editor.SetPosition(new TextPosition(1, 3));
            CaretOperations.TransposeLetters(editor, "editor.action.transposeLetters");
            editor.GetModel().GetLineContent(1).ShouldBe("acbd");
            editor.GetPosition().ShouldBe(new TextPosition(1, 4));

            // at the end of the line: swaps the two characters before it
            editor.SetPosition(new TextPosition(1, 5));
            CaretOperations.TransposeLetters(editor, "editor.action.transposeLetters");
            editor.GetModel().GetLineContent(1).ShouldBe("acdb");

            // at the beginning of the file: nothing
            editor.SetPosition(new TextPosition(1, 1));
            CaretOperations.TransposeLetters(editor, "editor.action.transposeLetters");
            editor.GetModel().GetLineContent(1).ShouldBe("acdb");

            // at the start of line 2: swaps the line break and the first character
            editor.SetPosition(new TextPosition(2, 1));
            CaretOperations.TransposeLetters(editor, "editor.action.transposeLetters");
            editor.GetValue().ShouldBe("acdbx\n");
        });
    }

    [Fact]
    public void MoveCaretMovesTheSelectedText()
    {
        EditingTestEditor.WithTestCodeEditor(["012345"], null, editor =>
        {
            editor.SetSelection(new TextSelection(1, 3, 1, 5));
            CaretOperations.MoveCaret(editor, true, "editor.action.moveCarretLeftAction");
            editor.GetValue().ShouldBe("023145");
            CaretOperations.MoveCaret(editor, false, "editor.action.moveCarretRightAction");
            editor.GetValue().ShouldBe("012345");
        });
    }

    [Fact]
    public void InsertFinalNewLine()
    {
        EditingTestEditor.WithTestCodeEditor(["a", "b"], null, editor =>
        {
            editor.SetPosition(new TextPosition(2, 2));
            InsertFinalNewLineCommand.Run(editor, "editor.action.insertFinalNewLine");
            editor.GetValue().ShouldBe("a\nb\n");
            editor.GetPosition().ShouldBe(new TextPosition(2, 2));
        });
    }

    [Fact]
    public void IndentationToSpacesAndTabsUpdateTheModelOptions()
    {
        EditingTestEditor.WithTestCodeEditor(["\tx", "\t\ty"], null, editor =>
        {
            IndentationOperations.IndentationToSpaces(editor, "editor.action.indentationToSpaces");
            editor.GetValue().ShouldBe("    x\n        y");
            editor.GetModel().GetOptions().InsertSpaces.ShouldBeTrue();

            IndentationOperations.IndentationToTabs(editor, "editor.action.indentationToTabs");
            editor.GetValue().ShouldBe("\tx\n\t\ty");
            editor.GetModel().GetOptions().InsertSpaces.ShouldBeFalse();
        });
    }

    [Fact]
    public void ChangeIndentationSize()
    {
        EditingTestEditor.WithTestCodeEditor(["x"], null, editor =>
        {
            IndentationOperations.ChangeIndentationSize(editor, insertSpaces: false, displaySizeOnly: false, tabSize: 2);
            var options = editor.GetModel().GetOptions();
            options.InsertSpaces.ShouldBeFalse();
            options.TabSize.ShouldBe(2);
            options.IndentSize.ShouldBe(2);

            IndentationOperations.ChangeIndentationSize(editor, insertSpaces: true, displaySizeOnly: true, tabSize: 12);
            options = editor.GetModel().GetOptions();
            options.InsertSpaces.ShouldBeFalse();
            options.TabSize.ShouldBe(8);

            IndentationOperations.ParseTabSizeArg(3).ShouldBe(3);
            IndentationOperations.ParseTabSizeArg("6").ShouldBe(6);
            IndentationOperations.ParseTabSizeArg(null).ShouldBeNull();
        });
    }

    [Fact]
    public void DetectIndentationReadsTheText()
    {
        EditingTestEditor.WithTestCodeEditor(["a {", "  b", "  c {", "    d", "  }", "}"], null, editor =>
        {
            IndentationOperations.DetectIndentation(editor);
            var options = editor.GetModel().GetOptions();
            options.InsertSpaces.ShouldBeTrue();
            options.TabSize.ShouldBe(2);
        });
    }

    [Fact]
    public void ReindentLinesAndSelectedLines()
    {
        using var service = new LanguageConfigurationService();
        IndentationTestUtils.RegisterLanguageConfiguration(service, Language.TypeScript);
        var options = new TestCodeEditorOptions { LanguageId = Language.TypeScript, LanguageConfigurationService = service };
        EditingTestEditor.WithTestCodeEditor(["function f() {", "return 1;", "      }", "if (x) {", "y();", "}"], options, editor =>
        {
            // the line above the selection is the base (upstream reindents from startLineNumber - 1)
            editor.SetSelection(new TextSelection(2, 1, 2, 3));
            IndentationOperations.ReindentSelectedLines(editor, "editor.action.reindentselectedlines");
            editor.GetModel().GetLinesContent().ShouldBe(["function f() {", "    return 1;", "      }", "if (x) {", "y();", "}"]);

            IndentationOperations.ReindentLines(editor, "editor.action.reindentlines");
            editor.GetModel().GetLinesContent().ShouldBe(["function f() {", "    return 1;", "}", "if (x) {", "    y();", "}"]);
        });
    }

    [Fact]
    public void WordCommandsRunUpstreamsOperations()
    {
        EditingTestEditor.WithTestCodeEditor(["foo bar_baz qux"], null, editor =>
        {
            editor.SetPosition(new TextPosition(1, 16));
            WordCommandsImpl.Get("cursorWordLeft")!.Run(editor);
            editor.GetPosition().ShouldBe(new TextPosition(1, 13));
            WordCommandsImpl.Get("cursorWordLeftSelect")!.Run(editor);
            editor.GetSelection().ShouldBe(new TextSelection(1, 13, 1, 5));
            WordCommandsImpl.Get("cursorWordEndRight")!.Run(editor);
            editor.GetPosition().ShouldBe(new TextPosition(1, 12));

            WordCommandsImpl.Get("cursorWordPartLeft")!.Run(editor);
            editor.GetPosition().ShouldBe(new TextPosition(1, 9));
            WordCommandsImpl.Get("cursorWordPartStartLeft")!.Run(editor);
            editor.GetPosition().ShouldBe(new TextPosition(1, 5));

            editor.SetPosition(new TextPosition(1, 16));
            WordCommandsImpl.Get("deleteWordLeft")!.Run(editor);
            editor.GetValue().ShouldBe("foo bar_baz ");
            WordCommandsImpl.Get("deleteWordPartLeft")!.Run(editor);
            editor.GetValue().ShouldBe("foo bar_baz"); // the whitespace first
            WordCommandsImpl.Get("deleteWordPartLeft")!.Run(editor);
            editor.GetValue().ShouldBe("foo bar_");

            editor.SetPosition(new TextPosition(1, 2));
            WordCommandsImpl.DeleteInsideWord(editor, onlyWord: false);
            editor.GetValue().ShouldBe("bar_");
        });
    }

    [Fact]
    public void WordCommandsAreUpstreamsList()
    {
        WordCommandsImpl.All.Count.ShouldBe(28);
        WordCommandsImpl.All.Select(c => c.Id).Distinct().Count().ShouldBe(28);
        WordCommandsImpl.All.Where(c => c.Writable).Select(c => c.Id).ShouldBe(
            ["deleteWordStartLeft", "deleteWordEndLeft", "deleteWordLeft", "deleteWordStartRight", "deleteWordEndRight", "deleteWordRight", "deleteWordPartLeft", "deleteWordPartRight"]);
    }

    [Fact]
    public void SmartSelectExpandsAndShrinks()
    {
        using var service = new LanguageConfigurationService();
        service.Register("js", new LanguageConfiguration { Brackets = [("(", ")"), ("{", "}")] });
        var options = new TestCodeEditorOptions { LanguageId = "js", LanguageConfigurationService = service };
        EditingTestEditor.WithTestCodeEditor(["f(barBaz, 1)"], options, editor =>
        {
            var state = new SmartSelectState();
            editor.SetPosition(new TextPosition(1, 4));

            state.Run(editor, true).GetAwaiter().GetResult();
            editor.GetSelection().ShouldBe(new TextSelection(1, 3, 1, 6)); // bar
            state.Run(editor, true).GetAwaiter().GetResult();
            editor.GetSelection().ShouldBe(new TextSelection(1, 3, 1, 9)); // barBaz
            state.Run(editor, true).GetAwaiter().GetResult();
            editor.GetSelection().ShouldBe(new TextSelection(1, 3, 1, 12)); // inside ()
            state.Run(editor, true).GetAwaiter().GetResult();
            editor.GetSelection().ShouldBe(new TextSelection(1, 2, 1, 13)); // ()

            state.Run(editor, false).GetAwaiter().GetResult();
            editor.GetSelection().ShouldBe(new TextSelection(1, 3, 1, 12));

            // the caret moved: the controller resets, and the next expand starts from the new cursor
            state.Reset();
            editor.SetPosition(new TextPosition(1, 1));
            state.Run(editor, true).GetAwaiter().GetResult();
            editor.GetSelection().ShouldBe(new TextSelection(1, 1, 1, 2)); // f
        });
    }

    [Fact]
    public void SmartSelectUsesRegisteredProviders()
    {
        EditingTestEditor.WithTestCodeEditor(["abc def"], null, editor =>
        {
            var features = new LanguageFeatures();
            editor.LanguageFeatures = features;
            using var registration = features.SelectionRangeProvider.Register("*", new FixedProvider(new TextRange(1, 2, 1, 6)));

            var state = new SmartSelectState();
            editor.SetPosition(new TextPosition(1, 3));
            state.Run(editor, true).GetAwaiter().GetResult();
            // the provider's range starts later than the word's, so it comes first and the word (which does not contain
            // it) is dropped
            editor.GetSelection().ShouldBe(new TextSelection(1, 2, 1, 6));
            state.Run(editor, true).GetAwaiter().GetResult();
            editor.GetSelection().ShouldBe(new TextSelection(1, 1, 1, 8)); // the document
        });
    }

    private sealed class FixedProvider(TextRange range) : ISelectionRangeProvider
    {
        public Task<IReadOnlyList<IReadOnlyList<SelectionRange>>?> ProvideSelectionRanges(TextModel model, IReadOnlyList<TextPosition> positions, CancellationToken token) =>
            Task.FromResult<IReadOnlyList<IReadOnlyList<SelectionRange>>?>(positions.Select(_ => (IReadOnlyList<SelectionRange>)[new SelectionRange(range)]).ToList());
    }
}
