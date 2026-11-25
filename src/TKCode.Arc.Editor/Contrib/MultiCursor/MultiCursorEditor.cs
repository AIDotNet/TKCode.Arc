// The editor the multi-cursor feature works on (the parts of upstream's ICodeEditor that
// src/vs/editor/contrib/multicursor/browser/multicursor.ts reads, commit b57d5c9e; MIT license). The control
// (CodeEditor) implements it through an adapter; the engine tests implement it over a test editor, so that the
// feature and upstream's tests run without a view.

using TKCode.Arc.Editor.Cursor;
using TKCode.Arc.Editor.Model;
using TKCode.Arc.Editor.ViewModel;
using EditorViewModel = TKCode.Arc.Editor.ViewModel.ViewModel;

namespace TKCode.Arc.Editor.Contrib.MultiCursor;

/// <summary>How selected text is matched by the multi-cursor actions (upstream's selectedTextMatchMode option).</summary>
internal enum SelectedTextMatchMode
{
    /// <summary>The find widget's match case and whole word options (upstream's 'findOptions', the default).</summary>
    FindOptions,

    /// <summary>Case sensitive, not whole words.</summary>
    CaseSensitive,

    /// <summary>Case insensitive, not whole words.</summary>
    CaseInsensitive,
}

/// <summary>
/// The editor options the multi-cursor feature reads (upstream's EditorOption.selectedTextMatchMode,
/// selectionHighlight, selectionHighlightMultiline, selectionHighlightMaxLength and occurrencesHighlight).
/// </summary>
internal sealed record MultiCursorOptions
{
    public static MultiCursorOptions Default { get; } = new();

    public SelectedTextMatchMode SelectedTextMatchMode { get; init; } = SelectedTextMatchMode.FindOptions;

    /// <summary>Whether other occurrences of the selected text are highlighted.</summary>
    public bool SelectionHighlight { get; init; } = true;

    /// <summary>Whether a selection over several lines is highlighted too.</summary>
    public bool SelectionHighlightMultiline { get; init; }

    /// <summary>The longest selection that is highlighted (0: no limit).</summary>
    public int SelectionHighlightMaxLength { get; init; } = 200;

    /// <summary>Whether occurrences of the symbol at the cursor are highlighted (upstream's occurrencesHighlight not 'off').</summary>
    public bool OccurrencesHighlight { get; init; } = true;
}

/// <summary>The names <see cref="IMultiCursorEditor.DidChangeConfiguration"/> reports (upstream's EditorOption ids).</summary>
internal static class MultiCursorOptionNames
{
    public const string SelectedTextMatchMode = "selectedTextMatchMode";
    public const string SelectionHighlight = "selectionHighlight";
}

/// <summary>The editor the multi-cursor actions and the selection highlighter work on (upstream's ICodeEditor).</summary>
internal interface IMultiCursorEditor
{
    /// <summary>The owner id of the editor's decorations.</summary>
    int EditorId { get; }

    /// <summary>The model, or null (upstream's hasModel).</summary>
    TextModel? Model { get; }

    /// <summary>The view model (upstream's _getViewModel), or null.</summary>
    EditorViewModel? ViewModel { get; }

    /// <summary>Whether the editor's text has focus.</summary>
    bool HasTextFocus { get; }

    /// <summary>The word separators option.</summary>
    string WordSeparators { get; }

    /// <summary>The options of the feature.</summary>
    MultiCursorOptions MultiCursorOptions { get; }

    /// <summary>The find controller of the editor (upstream's CommonFindController.get), or null.</summary>
    IMultiCursorFindController? FindController { get; }

    /// <summary>The editor's multi-cursor controller (upstream's MultiCursorSelectionController.get), or null.</summary>
    MultiCursorSelectionController? MultiCursorController { get; }

    IReadOnlyList<TextSelection> GetSelections();

    void SetSelections(IReadOnlyList<TextSelection> selections, string source = "api", CursorChangeReason reason = CursorChangeReason.NotSet);

    void RevealRangeInCenterIfOutsideViewport(TextRange range, ScrollType scrollType);

    /// <summary>Upstream's onDidChangeCursorSelection.</summary>
    event Action<CursorStateChangedEvent>? DidChangeCursorSelection;

    /// <summary>Upstream's onDidFocusEditorText.</summary>
    event Action? DidFocusEditorText;

    /// <summary>Upstream's onDidBlurEditorText.</summary>
    event Action? DidBlurEditorText;

    /// <summary>Upstream's onDidChangeConfiguration, with the name of the option that changed.</summary>
    event Action<string>? DidChangeConfiguration;

    /// <summary>Upstream's onDidChangeModelContent.</summary>
    event Action? DidChangeModelContent;
}

/// <summary>Helpers of the multi-cursor feature over <see cref="IMultiCursorEditor"/>.</summary>
internal static class MultiCursorEditorExtensions
{
    /// <summary>The primary selection (upstream's getSelection).</summary>
    public static TextSelection GetSelection(this IMultiCursorEditor editor) => editor.GetSelections()[0];

    /// <summary>The word at a position by the word separators option (upstream's getConfiguredWordAtPosition).</summary>
    public static WordAtPosition? GetConfiguredWordAtPosition(this IMultiCursorEditor editor, TextPosition position) =>
        editor.Model is { } model ? Cursor.WordOperations.GetWordAtPosition(model, editor.WordSeparators, [], position) : null;
}
