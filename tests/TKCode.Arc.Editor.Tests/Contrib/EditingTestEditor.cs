// Adaptation of vscode's test code editor (src/vs/editor/test/browser/testCodeEditor.ts: withTestCodeEditor, commit
// b57d5c9e), MIT license, for the tests of the editing contributions: a TestCodeEditor seen as the IEditorActionHost the
// ported actions run on, with upstream's test helpers executeAction and assertSelection.

using Shouldly;
using TKCode.Arc.Editor.Commands;
using TKCode.Arc.Editor.Contrib;
using TKCode.Arc.Editor.Cursor;
using TKCode.Arc.Editor.Languages;
using TKCode.Arc.Editor.Model;
using TKCode.Arc.Editor.ViewModel;

namespace TKCode.Arc.Editor.Tests.Contrib;

/// <summary>An action of a ported contribution, as the tests run it (upstream's EditorAction.run).</summary>
internal class TestEditorAction(Action<EditingTestEditor> run)
{
    public void Run(EditingTestEditor editor) => run(editor);
}

/// <summary>A <see cref="TestCodeEditor"/> as the editing actions see it.</summary>
internal sealed class EditingTestEditor : IEditorActionHost, IDisposable
{
    private readonly IDisposable? _owned;

    public EditingTestEditor(TestCodeEditor editor, IDisposable? owned = null)
    {
        Editor = editor;
        _owned = owned;
    }

    public TestCodeEditor Editor { get; }

    public TestViewModel ViewModel => Editor.ViewModel;

    public EditorCommentsOptions CommentsOptions { get; set; } = EditorCommentsOptions.Default;

    public SmartSelectOptions SmartSelectOptions { get; set; } = SmartSelectOptions.Default;

    public LanguageFeatures? LanguageFeatures { get; set; }

    public bool TrimTrailingWhitespaceInRegexAndStrings { get; set; } = true;

    public LanguageConfigurationService LanguageConfigurationService => Editor.LanguageConfigurationService;

    public bool IsReadOnly => Editor.IsReadOnly;

    public IViewModel? GetViewModel() => Editor.ViewModel;

    public TextModel GetModel() => Editor.Model;

    TextModel? ICoreCommandsCodeEditor.GetModel() => Editor.Model;

    public bool PushUndoStop() => Editor.PushUndoStop();

    public void ExecuteCommands(string? source, IReadOnlyList<ICommand?> commands) => Editor.ExecuteCommands(source, commands);

    public IReadOnlyList<TextSelection> GetSelections() => Editor.GetSelections();

    public TextSelection GetSelection() => Editor.GetSelection();

    TextSelection? IEditorActionHost.GetSelection() => Editor.GetSelection();

    public TextPosition GetPosition() => Editor.GetPosition();

    public void SetSelections(IReadOnlyList<TextSelection> selections, string source) => Editor.SetSelections(selections, source);

    public void SetSelections(IReadOnlyList<TextSelection> selections) => Editor.SetSelections(selections);

    public void SetSelection(TextSelection selection) => Editor.SetSelection(selection);

    public void SetSelection(TextRange range) => Editor.SetSelection(range);

    public void SetPosition(TextPosition position) => Editor.SetPosition(position);

    public bool ExecuteEdits(string? source, IReadOnlyList<SingleEditOperation> edits, IReadOnlyList<TextSelection>? endCursorState) =>
        Editor.ExecuteEdits(source, edits, endCursorState);

    public string GetValue() => Editor.GetValue();

    public void Type(string text) => Editor.Type(text);

    public void Undo() => Editor.Undo().AsTask().GetAwaiter().GetResult();

    public void Redo() => Editor.Redo().AsTask().GetAwaiter().GetResult();

    public void Dispose()
    {
        Editor.Dispose();
        _owned?.Dispose();
    }

    /// <summary>Upstream's withTestCodeEditor.</summary>
    public static void WithTestCodeEditor(string[] lines, TestCodeEditorOptions? options, Action<EditingTestEditor> callback) =>
        TestCodeEditor.WithTestCodeEditor(lines, options, (editor, _) => callback(new EditingTestEditor(editor)));

    /// <summary>Upstream's withTestCodeEditor on a text.</summary>
    public static void WithTestCodeEditor(string text, TestCodeEditorOptions? options, Action<EditingTestEditor> callback) =>
        TestCodeEditor.WithTestCodeEditor(text, options, (editor, _) => callback(new EditingTestEditor(editor)));

    /// <summary>Upstream's withTestCodeEditor on a model the test owns.</summary>
    public static void WithTestCodeEditor(TextModel model, TestCodeEditorOptions? options, Action<EditingTestEditor> callback) =>
        TestCodeEditor.WithTestCodeEditor(model, options, (editor, _) => callback(new EditingTestEditor(editor)));

    /// <summary>Upstream's withTestCodeEditor, with the view model.</summary>
    public static void WithTestCodeEditor(string[] lines, TestCodeEditorOptions? options, Action<EditingTestEditor, TestViewModel> callback) =>
        TestCodeEditor.WithTestCodeEditor(lines, options, (editor, viewModel) => callback(new EditingTestEditor(editor), viewModel));
}

/// <summary>The assertions of the ported tests.</summary>
internal static class EditingAssert
{
    public static void ExecuteAction(TestEditorAction action, EditingTestEditor editor) => action.Run(editor);

    public static void AssertSelection(EditingTestEditor editor, TextSelection expected) =>
        editor.GetSelections().ShouldBe([expected]);

    public static void AssertSelection(EditingTestEditor editor, TextSelection[] expected) =>
        editor.GetSelections().ShouldBe(expected);

    public static void AssertDeepStrictEqual(IReadOnlyList<string> actual, string[] expected) => actual.ShouldBe(expected);

    public static void AssertDeepStrictEqual(IReadOnlyList<TextSelection> actual, TextSelection[] expected) => actual.ShouldBe(expected);

    public static void AssertDeepStrictEqual(TextSelection? actual, TextSelection expected) => actual.ShouldBe(expected);

    public static void AssertDeepStrictEqual(TextPosition? actual, TextPosition expected) => actual.ShouldBe(expected);

    public static void AssertDeepStrictEqual(string? actual, string? expected) => actual.ShouldBe(expected);

    public static void AssertDeepStrictEqual(string? actual, string? expected, string message) => actual.ShouldBe(expected, message);

    public static void AssertDeepStrictEqual(int actual, int expected) => actual.ShouldBe(expected);

    public static void AssertDeepStrictEqual(int[] actual, int[] expected) => actual.ShouldBe(expected);

    public static void AssertDeepStrictEqual(bool actual, bool expected) => actual.ShouldBe(expected);
}
