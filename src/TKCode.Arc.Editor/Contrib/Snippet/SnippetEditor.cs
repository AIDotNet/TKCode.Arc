// The editor a snippet session and controller work on (the parts of upstream's IActiveCodeEditor that
// src/vs/editor/contrib/snippet/browser/snippetSession.ts and snippetController2.ts use, commit b57d5c9e), MIT license.
// Upstream's snippet code talks to the code editor widget directly; the engine has no widget, so the host implements
// this interface: TKCode.Arc's CodeEditor (Contrib/Snippet/CodeEditorSnippetEditor.cs) and the engine tests' test editor.

using TKCode.Arc.Editor.Config;
using TKCode.Arc.Editor.Languages;
using TKCode.Arc.Editor.Model;
using TKCode.Arc.Editor.Platform;

namespace TKCode.Arc.Editor.Contrib.Snippet;

/// <summary>The platform services the snippet variables read (upstream's label and workspace services, the clock, random).</summary>
internal sealed record SnippetVariableServices
{
    /// <summary>The services with the defaults: file system paths, no workspace, the system clock, shared random numbers.</summary>
    public static SnippetVariableServices Default { get; } = new();

    /// <summary>Turns URIs into TM_FILEPATH, TM_DIRECTORY and RELATIVE_FILEPATH.</summary>
    public ISnippetLabelService LabelService { get; init; } = SnippetLabelService.Default;

    /// <summary>WORKSPACE_NAME and WORKSPACE_FOLDER; none by default.</summary>
    public ISnippetWorkspaceService? WorkspaceService { get; init; }

    /// <summary>The clock of the CURRENT_* variables.</summary>
    public TimeProvider TimeProvider { get; init; } = TimeProvider.System;

    /// <summary>RANDOM, RANDOM_HEX and UUID; <see cref="Random.Shared"/> when null.</summary>
    public Random? Random { get; init; }
}

/// <summary>What snippet sessions and the snippet controller need from an editor (upstream's IActiveCodeEditor).</summary>
internal interface ISnippetEditor
{
    /// <summary>The editor's model.</summary>
    TextModel Model { get; }

    /// <summary>The owner id of the editor's decorations (0: every editor of the model shows them).</summary>
    int DecorationOwnerId { get; }

    /// <summary>The language configurations (comments, word definitions).</summary>
    LanguageConfigurationService LanguageConfigurationService { get; }

    /// <summary>The editor's language feature registries (the choice completions register there); null for none.</summary>
    LanguageFeatures? LanguageFeatures { get; }

    /// <summary>The editor's context keys (inSnippetMode, hasNextTabstop, ...).</summary>
    IContextKeyService ContextKeyService { get; }

    /// <summary>The <c>multiCursorPaste</c> option: <see cref="MultiCursorPaste.Spread"/> gives each cursor a clipboard line.</summary>
    MultiCursorPaste MultiCursorPaste { get; }

    /// <summary>The services of the snippet variables.</summary>
    SnippetVariableServices VariableServices { get; }

    /// <summary>The primary selection.</summary>
    TextSelection GetSelection();

    /// <summary>Every selection, the primary first.</summary>
    IReadOnlyList<TextSelection> GetSelections();

    /// <summary>Sets the selections, the primary first.</summary>
    void SetSelections(IReadOnlyList<TextSelection> selections);

    /// <summary>Applies edits as one undoable step; <paramref name="cursorStateComputer"/> gives the selections afterwards.</summary>
    bool ExecuteEdits(string source, IReadOnlyList<SingleEditOperation> edits, CursorStateComputer cursorStateComputer);

    /// <summary>Scrolls a range into view (upstream's revealRange).</summary>
    void RevealRange(TextRange range);

    /// <summary>Scrolls a position into the center when it is outside the viewport.</summary>
    void RevealPositionInCenterIfOutsideViewport(TextPosition position);

    /// <summary>Calls <paramref name="callback"/> after the selections changed (upstream's onDidChangeCursorSelection).</summary>
    IDisposable OnDidChangeCursorSelection(Action callback);

    /// <summary>Calls <paramref name="callback"/> after the model's content changed (upstream's onDidChangeModelContent).</summary>
    IDisposable OnDidChangeModelContent(Action<ModelContentChangedEvent> callback);

    /// <summary>
    /// Shows the items of <paramref name="provider"/> at the cursor, soon (not inside the current call; upstream's
    /// queueMicrotask + showSimpleSuggestions): the choices of a choice placeholder.
    /// </summary>
    void ShowSimpleSuggestions(ICompletionItemProvider provider);
}
