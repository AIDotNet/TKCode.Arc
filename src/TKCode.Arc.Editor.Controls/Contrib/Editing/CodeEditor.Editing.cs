// The CodeEditor as the editing actions of the engine see it (TKCode.Arc.Editor.Contrib.IEditorActionHost: the members
// of vscode's ICodeEditor that linesOperations, comment, bracketMatching, indentation, caretOperations, cursorUndo,
// insertFinalNewLine, lineSelection, smartSelect and wordOperations call; src/vs/editor/browser/editorBrowser.ts,
// commit b57d5c9e, MIT license), and the editor options those features read (upstream's EditorOption.matchBrackets,
// comments, smartSelect, autoIndentOnPaste and autoIndentOnPasteWithinString).
// Differences from upstream: the options are an internal record on the control (CodeEditorEditingOptions) rather than
// members of the editor's option set; setting it raises DidChangeEditingOptions (upstream's onDidChangeConfiguration).

using TKCode.Arc.Editor;
using TKCode.Arc.Editor.Contrib;
using TKCode.Arc.Editor.Languages;
using TKCode.Arc.Editor.Model;

namespace TKCode.Arc;

/// <summary>The options of the editing features (upstream's EditorOptions of the same names, with their defaults).</summary>
internal sealed record CodeEditorEditingOptions
{
    public static CodeEditorEditingOptions Default { get; } = new();

    /// <summary>Upstream's matchBrackets: highlight the matching bracket (near) or also the enclosing ones (always).</summary>
    public MatchBracketsMode MatchBrackets { get; init; } = MatchBracketsMode.Always;

    /// <summary>Upstream's comments.</summary>
    public EditorCommentsOptions Comments { get; init; } = EditorCommentsOptions.Default;

    /// <summary>Upstream's smartSelect.</summary>
    public SmartSelectOptions SmartSelect { get; init; } = SmartSelectOptions.Default;

    /// <summary>Upstream's autoIndentOnPaste: re-indent pasted lines (needs autoIndent full).</summary>
    public bool AutoIndentOnPaste { get; init; }

    /// <summary>Upstream's autoIndentOnPasteWithinString.</summary>
    public bool AutoIndentOnPasteWithinString { get; init; } = true;
}

public sealed partial class CodeEditor : IEditorActionHost
{
    private CodeEditorEditingOptions _editingOptions = CodeEditorEditingOptions.Default;

    /// <summary>The options of the editing features; setting them raises <see cref="DidChangeEditingOptions"/>.</summary>
    internal CodeEditorEditingOptions EditingOptions
    {
        get => _editingOptions;
        set
        {
            if (_editingOptions == value)
            {
                return;
            }

            _editingOptions = value;
            DidChangeEditingOptions?.Invoke(value);
        }
    }

    /// <summary>The editing options changed (upstream's onDidChangeConfiguration for those options).</summary>
    internal event Action<CodeEditorEditingOptions>? DidChangeEditingOptions;

    IReadOnlyList<TextSelection> IEditorActionHost.GetSelections() => GetSelections();

    TextSelection? IEditorActionHost.GetSelection() => GetSelection();

    void IEditorActionHost.SetSelections(IReadOnlyList<TextSelection> selections, string source) => SetSelections(selections, source);

    bool IEditorActionHost.ExecuteEdits(string? source, IReadOnlyList<SingleEditOperation> edits, IReadOnlyList<TextSelection>? endCursorState) =>
        ExecuteEdits(source, edits, endCursorState);

    LanguageConfigurationService IEditorActionHost.LanguageConfigurationService => LanguageConfigurationService;

    LanguageFeatures? IEditorActionHost.LanguageFeatures => LanguageFeatures;

    EditorCommentsOptions IEditorActionHost.CommentsOptions => _editingOptions.Comments;

    SmartSelectOptions IEditorActionHost.SmartSelectOptions => _editingOptions.SmartSelect;
}
