// The editor as the ported editor actions of the editing contributions see it: the members of vscode's ICodeEditor
// (src/vs/editor/browser/editorBrowser.ts, commit b57d5c9e, MIT license) that linesOperations, comment,
// bracketMatching, indentation, caretOperations, cursorUndo, insertFinalNewLine, lineSelection, smartSelect and
// wordOperations call. Upstream's actions take the ICodeEditor and a ServicesAccessor; here the logic of each action
// is a static method on an IEditorActionHost, which the CodeEditor control and the engine tests' editor both are, so
// that upstream's tests run on the engine without Avalonia.

using TKCode.Arc.Editor.Commands;
using TKCode.Arc.Editor.Config;
using TKCode.Arc.Editor.Cursor;
using TKCode.Arc.Editor.Languages;
using TKCode.Arc.Editor.Model;
using TKCode.Arc.Editor.ViewModel;

namespace TKCode.Arc.Editor.Contrib;

/// <summary>The comments options (upstream's EditorOption.comments).</summary>
/// <param name="InsertSpace">Insert a space after the line comment token and inside block comment tokens.</param>
/// <param name="IgnoreEmptyLines">Ignore empty lines when toggling, adding or removing line comments.</param>
internal sealed record EditorCommentsOptions(bool InsertSpace = true, bool IgnoreEmptyLines = true)
{
    public static EditorCommentsOptions Default { get; } = new();
}

/// <summary>When the matching bracket is highlighted (upstream's EditorOption.matchBrackets).</summary>
internal enum MatchBracketsMode
{
    Never,
    Near,
    Always,
}

/// <summary>The smart select options (upstream's EditorOption.smartSelect).</summary>
internal sealed record SmartSelectOptions(bool SelectLeadingAndTrailingWhitespace = true, bool SelectSubwords = true)
{
    public static SmartSelectOptions Default { get; } = new();
}

/// <summary>
/// An editor that the editing actions run on (the members of upstream's ICodeEditor they call). The core command
/// members (model, view model, read-only, undo stops, executeCommands) come from <see cref="ICoreCommandsCodeEditor"/>.
/// </summary>
internal interface IEditorActionHost : ICoreCommandsCodeEditor
{
    /// <summary>The selections, the primary first; empty without a model (upstream's getSelections).</summary>
    IReadOnlyList<TextSelection> GetSelections();

    /// <summary>The primary selection, or null without a model (upstream's getSelection).</summary>
    TextSelection? GetSelection();

    /// <summary>Sets the selections (upstream's setSelections).</summary>
    void SetSelections(IReadOnlyList<TextSelection> selections, string source);

    /// <summary>Applies edits as one step and sets the cursors after them; false when read-only (upstream's executeEdits).</summary>
    bool ExecuteEdits(string? source, IReadOnlyList<SingleEditOperation> edits, IReadOnlyList<TextSelection>? endCursorState);

    /// <summary>The language configurations (upstream's ILanguageConfigurationService).</summary>
    LanguageConfigurationService LanguageConfigurationService { get; }

    /// <summary>The language feature registries (upstream's ILanguageFeaturesService); null when the host has none.</summary>
    LanguageFeatures? LanguageFeatures => null;

    /// <summary>Upstream's EditorOption.comments.</summary>
    EditorCommentsOptions CommentsOptions => EditorCommentsOptions.Default;

    /// <summary>Upstream's EditorOption.smartSelect.</summary>
    SmartSelectOptions SmartSelectOptions => SmartSelectOptions.Default;

    /// <summary>Upstream's files.trimTrailingWhitespaceInRegexAndStrings setting.</summary>
    bool TrimTrailingWhitespaceInRegexAndStrings => true;
}

/// <summary>Helpers over <see cref="IEditorActionHost"/> for what upstream reads from the editor's options and view.</summary>
internal static class EditorActionHostExtensions
{
    /// <summary>The cursor options (upstream's getOption for wordSeparators, autoIndent, autoClosing*, ...).</summary>
    public static EditorOptions GetOptions(this IEditorActionHost editor) =>
        editor.GetViewModel()?.Configuration.Options.Cursor ?? EditorOptions.Default;

    /// <summary>Runs one edit command on the cursors (upstream's executeCommand).</summary>
    public static void ExecuteCommand(this IEditorActionHost editor, string? source, ICommand command) =>
        editor.ExecuteCommands(source, [command]);

    /// <summary>Scrolls every cursor into view (upstream's revealAllCursors).</summary>
    public static void RevealAllCursors(this IEditorActionHost editor, bool revealHorizontal) =>
        editor.GetViewModel()?.RevealAllCursors(null, revealHorizontal);

    /// <summary>Scrolls a model range into view (upstream's revealRange).</summary>
    public static void RevealRange(this IEditorActionHost editor, TextRange range)
    {
        if (editor.GetViewModel() is not { } viewModel || editor.GetModel() is not { } model)
        {
            return;
        }

        var viewRange = viewModel.CoordinatesConverter.ConvertModelRangeToViewRange(model.ValidateRange(range));
        viewModel.RevealRange(null, true, viewRange, VerticalRevealType.Simple, ScrollType.Smooth);
    }

    /// <summary>Scrolls a model position into view (upstream's revealPosition).</summary>
    public static void RevealPosition(this IEditorActionHost editor, TextPosition position) =>
        editor.RevealRange(new TextRange(position.LineNumber, position.Column, position.LineNumber, position.Column));

    /// <summary>
    /// The word at a position by the language's word definition (upstream's getConfiguredWordAtPosition, which uses
    /// the word separators option).
    /// </summary>
    public static WordAtPosition? GetConfiguredWordAtPosition(this IEditorActionHost editor, TextPosition position)
    {
        if (editor.GetModel() is not { } model)
        {
            return null;
        }

        var options = editor.GetOptions();
        return Cursor.WordOperations.GetWordAtPosition(model, options.WordSeparators, options.WordSegmenterLocales, position);
    }
}
