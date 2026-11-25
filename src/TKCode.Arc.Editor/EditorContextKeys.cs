// Ported from vscode (src/vs/editor/common/editorContextKeys.ts, commit b57d5c9e), MIT license.
// Differences from upstream:
//  - the namespace EditorContextKeys is a static class and the members are PascalCase.
//  - the keys whose default upstream is undefined (inCompositeEditor, multiDiffEditorAllCollapsed) are RawContextKey<bool?>
//    so that undefined (null) stays apart from false.
//  - the descriptions are the English texts of upstream's nls strings (the engine has no localization yet) and are
//    also each key's XML summary; upstream's own doc comments are the remarks. Upstream adds every key to a process-wide
//    registry (RawContextKey.all()); here each key carries its Info and All lists the ones that are not hidden.

using System.Collections.Immutable;
using TKCode.Arc.Editor.Platform;

namespace TKCode.Arc.Editor;

/// <summary>The context keys an editor sets, for <c>when</c> clauses of keybindings and commands.</summary>
internal static class EditorContextKeys
{
    /// <summary>Key <c>editorSimpleInput</c>; hidden upstream, so it has no description and no <see cref="RawContextKey{T}.Info"/>.</summary>
    public static readonly RawContextKey<bool> EditorSimpleInput = new("editorSimpleInput", false, hide: true);
    /// <summary>Whether the editor text has focus (cursor is blinking).</summary>
    /// <remarks>A context key that is set when the editor's text has focus (cursor is blinking). Is false when focus is in simple editor widgets (repl input, scm commit input).</remarks>
    public static readonly RawContextKey<bool> EditorTextFocus = new("editorTextFocus", false, "Whether the editor text has focus (cursor is blinking)");
    /// <summary>Whether the editor or an editor widget has focus (e.g. focus is in the find widget).</summary>
    /// <remarks>A context key that is set when the editor's text or an editor's widget has focus.</remarks>
    public static readonly RawContextKey<bool> Focus = new("editorFocus", false, "Whether the editor or an editor widget has focus (e.g. focus is in the find widget)");

    /// <summary>Whether an editor or a rich text input has focus (cursor is blinking).</summary>
    /// <remarks>A context key that is set when any editor input has focus (regular editor, repl input...).</remarks>
    public static readonly RawContextKey<bool> TextInputFocus = new("textInputFocus", false, "Whether an editor or a rich text input has focus (cursor is blinking)");

    /// <summary>Whether the editor is read-only.</summary>
    public static readonly RawContextKey<bool> ReadOnly = new("editorReadonly", false, "Whether the editor is read-only");
    /// <summary>Whether the context is a diff editor.</summary>
    public static readonly RawContextKey<bool> InDiffEditor = new("inDiffEditor", false, "Whether the context is a diff editor");
    /// <summary>Whether the context is an embedded diff editor.</summary>
    public static readonly RawContextKey<bool> IsEmbeddedDiffEditor = new("isEmbeddedDiffEditor", false, "Whether the context is an embedded diff editor");
    /// <summary>Whether the context is a multi diff editor.</summary>
    public static readonly RawContextKey<bool> InMultiDiffEditor = new("inMultiDiffEditor", false, "Whether the context is a multi diff editor");
    /// <summary>Whether all files in multi diff editor are collapsed.</summary>
    public static readonly RawContextKey<bool?> MultiDiffEditorAllCollapsed = new("multiDiffEditorAllCollapsed", null, "Whether all files in multi diff editor are collapsed");
    /// <summary>Whether all unchanged regions of a multi diff editor file are revealed.</summary>
    public static readonly RawContextKey<bool> MultiDiffEditorItemAllUnchangedRegionsShown = new("multiDiffEditorItemAllUnchangedRegionsShown", false, "Whether all unchanged regions of a multi diff editor file are revealed");
    /// <summary>Whether the multi diff editor renders diffs side by side instead of inline.</summary>
    public static readonly RawContextKey<bool> MultiDiffEditorRenderSideBySide = new("multiDiffEditorRenderSideBySide", true, "Whether the multi diff editor renders diffs side by side instead of inline");
    /// <summary>Whether the diff editor has changes.</summary>
    public static readonly RawContextKey<bool> HasChanges = new("diffEditorHasChanges", false, "Whether the diff editor has changes");
    /// <summary>Whether a moved code block is selected for comparison.</summary>
    public static readonly RawContextKey<bool> ComparingMovedCode = new("comparingMovedCode", false, "Whether a moved code block is selected for comparison");
    /// <summary>Whether the accessible diff viewer is visible.</summary>
    public static readonly RawContextKey<bool> AccessibleDiffViewerVisible = new("accessibleDiffViewerVisible", false, "Whether the accessible diff viewer is visible");
    /// <summary>Whether the diff editor render side by side inline breakpoint is reached.</summary>
    public static readonly RawContextKey<bool> DiffEditorRenderSideBySideInlineBreakpointReached = new("diffEditorRenderSideBySideInlineBreakpointReached", false, "Whether the diff editor render side by side inline breakpoint is reached");
    /// <summary>Whether inline mode is active.</summary>
    public static readonly RawContextKey<bool> DiffEditorInlineMode = new("diffEditorInlineMode", false, "Whether inline mode is active");
    /// <summary>Whether inline mode is temporarily active after manually resizing the diff editor.</summary>
    public static readonly RawContextKey<bool> DiffEditorTemporaryInlineMode = new("diffEditorTemporaryInlineMode", false, "Whether inline mode is temporarily active after manually resizing the diff editor");
    /// <summary>Whether automatic diff editor layout would render side by side.</summary>
    public static readonly RawContextKey<bool> DiffEditorAutomaticRenderSideBySide = new("diffEditorAutomaticRenderSideBySide", true, "Whether automatic diff editor layout would render side by side");

    /// <summary>Whether modified is writable in the diff editor.</summary>
    public static readonly RawContextKey<bool> DiffEditorOriginalWritable = new("diffEditorOriginalWritable", false, "Whether modified is writable in the diff editor");
    /// <summary>Whether modified is writable in the diff editor.</summary>
    public static readonly RawContextKey<bool> DiffEditorModifiedWritable = new("diffEditorModifiedWritable", false, "Whether modified is writable in the diff editor");
    /// <summary>The uri of the original document.</summary>
    public static readonly RawContextKey<string> DiffEditorOriginalUri = new("diffEditorOriginalUri", "", "The uri of the original document");
    /// <summary>The uri of the modified document.</summary>
    public static readonly RawContextKey<string> DiffEditorModifiedUri = new("diffEditorModifiedUri", "", "The uri of the modified document");

    /// <summary>Whether <c>editor.columnSelection</c> is enabled.</summary>
    public static readonly RawContextKey<bool> ColumnSelection = new("editorColumnSelection", false, "Whether `editor.columnSelection` is enabled");
    /// <summary>The negation of <see cref="ReadOnly"/>.</summary>
    public static readonly ContextKeyExpression Writable = ReadOnly.ToNegated();
    /// <summary>Whether the editor has text selected.</summary>
    public static readonly RawContextKey<bool> HasNonEmptySelection = new("editorHasSelection", false, "Whether the editor has text selected");
    /// <summary>The negation of <see cref="HasNonEmptySelection"/>.</summary>
    public static readonly ContextKeyExpression HasOnlyEmptySelection = HasNonEmptySelection.ToNegated();
    /// <summary>Whether the editor has multiple selections.</summary>
    public static readonly RawContextKey<bool> HasMultipleSelections = new("editorHasMultipleSelections", false, "Whether the editor has multiple selections");
    /// <summary>The negation of <see cref="HasMultipleSelections"/>.</summary>
    public static readonly ContextKeyExpression HasSingleSelection = HasMultipleSelections.ToNegated();
    /// <summary>Whether <c>Tab</c> will move focus out of the editor.</summary>
    public static readonly RawContextKey<bool> TabMovesFocus = new("editorTabMovesFocus", false, "Whether `Tab` will move focus out of the editor");
    /// <summary>The negation of <see cref="TabMovesFocus"/>.</summary>
    public static readonly ContextKeyExpression TabDoesNotMoveFocus = TabMovesFocus.ToNegated();
    /// <summary>Key <c>isInEmbeddedEditor</c>; hidden upstream, so it has no description and no <see cref="RawContextKey{T}.Info"/>.</summary>
    public static readonly RawContextKey<bool> IsInEmbeddedEditor = new("isInEmbeddedEditor", false, hide: true);
    /// <summary>Key <c>canUndo</c>; hidden upstream, so it has no description and no <see cref="RawContextKey{T}.Info"/>.</summary>
    public static readonly RawContextKey<bool> CanUndo = new("canUndo", false, hide: true);
    /// <summary>Key <c>canRedo</c>; hidden upstream, so it has no description and no <see cref="RawContextKey{T}.Info"/>.</summary>
    public static readonly RawContextKey<bool> CanRedo = new("canRedo", false, hide: true);

    /// <summary>Whether the editor hover is visible.</summary>
    public static readonly RawContextKey<bool> HoverVisible = new("editorHoverVisible", false, "Whether the editor hover is visible");
    /// <summary>Whether the editor hover is focused.</summary>
    public static readonly RawContextKey<bool> HoverFocused = new("editorHoverFocused", false, "Whether the editor hover is focused");

    /// <summary>Whether the sticky scroll is focused.</summary>
    public static readonly RawContextKey<bool> StickyScrollFocused = new("stickyScrollFocused", false, "Whether the sticky scroll is focused");
    /// <summary>Whether the sticky scroll is visible.</summary>
    public static readonly RawContextKey<bool> StickyScrollVisible = new("stickyScrollVisible", false, "Whether the sticky scroll is visible");

    /// <summary>Whether the standalone color picker is visible.</summary>
    public static readonly RawContextKey<bool> StandaloneColorPickerVisible = new("standaloneColorPickerVisible", false, "Whether the standalone color picker is visible");
    /// <summary>Whether the standalone color picker is focused.</summary>
    public static readonly RawContextKey<bool> StandaloneColorPickerFocused = new("standaloneColorPickerFocused", false, "Whether the standalone color picker is focused");

    /// <summary>Whether the editor is in the composition mode.</summary>
    public static readonly RawContextKey<bool> IsComposing = new("isComposing", false, "Whether the editor is in the composition mode");

    /// <summary>Whether the editor is part of a larger editor (e.g. notebooks).</summary>
    /// <remarks>A context key that is set when an editor is part of a larger editor, like notebooks or (future) a diff editor</remarks>
    public static readonly RawContextKey<bool?> InCompositeEditor = new("inCompositeEditor", null, "Whether the editor is part of a larger editor (e.g. notebooks)");
    /// <summary>The negation of <see cref="InCompositeEditor"/>.</summary>
    public static readonly ContextKeyExpression NotInCompositeEditor = InCompositeEditor.ToNegated();

    // -- mode context keys
    /// <summary>The language identifier of the editor.</summary>
    public static readonly RawContextKey<string> LanguageId = new("editorLangId", "", "The language identifier of the editor");
    /// <summary>Whether the editor has a completion item provider.</summary>
    public static readonly RawContextKey<bool> HasCompletionItemProvider = new("editorHasCompletionItemProvider", false, "Whether the editor has a completion item provider");
    /// <summary>Whether the editor has a code actions provider.</summary>
    public static readonly RawContextKey<bool> HasCodeActionsProvider = new("editorHasCodeActionsProvider", false, "Whether the editor has a code actions provider");
    /// <summary>Whether the editor has a code lens provider.</summary>
    public static readonly RawContextKey<bool> HasCodeLensProvider = new("editorHasCodeLensProvider", false, "Whether the editor has a code lens provider");
    /// <summary>Whether the editor has a definition provider.</summary>
    public static readonly RawContextKey<bool> HasDefinitionProvider = new("editorHasDefinitionProvider", false, "Whether the editor has a definition provider");
    /// <summary>Whether the editor has a declaration provider.</summary>
    public static readonly RawContextKey<bool> HasDeclarationProvider = new("editorHasDeclarationProvider", false, "Whether the editor has a declaration provider");
    /// <summary>Whether the editor has an implementation provider.</summary>
    public static readonly RawContextKey<bool> HasImplementationProvider = new("editorHasImplementationProvider", false, "Whether the editor has an implementation provider");
    /// <summary>Whether the editor has a type definition provider.</summary>
    public static readonly RawContextKey<bool> HasTypeDefinitionProvider = new("editorHasTypeDefinitionProvider", false, "Whether the editor has a type definition provider");
    /// <summary>Whether the editor has a hover provider.</summary>
    public static readonly RawContextKey<bool> HasHoverProvider = new("editorHasHoverProvider", false, "Whether the editor has a hover provider");
    /// <summary>Whether the editor has a document highlight provider.</summary>
    public static readonly RawContextKey<bool> HasDocumentHighlightProvider = new("editorHasDocumentHighlightProvider", false, "Whether the editor has a document highlight provider");
    /// <summary>Whether the editor has a document symbol provider.</summary>
    public static readonly RawContextKey<bool> HasDocumentSymbolProvider = new("editorHasDocumentSymbolProvider", false, "Whether the editor has a document symbol provider");
    /// <summary>Whether the editor has a reference provider.</summary>
    public static readonly RawContextKey<bool> HasReferenceProvider = new("editorHasReferenceProvider", false, "Whether the editor has a reference provider");
    /// <summary>Whether the editor has a rename provider.</summary>
    public static readonly RawContextKey<bool> HasRenameProvider = new("editorHasRenameProvider", false, "Whether the editor has a rename provider");
    /// <summary>Whether the editor has a signature help provider.</summary>
    public static readonly RawContextKey<bool> HasSignatureHelpProvider = new("editorHasSignatureHelpProvider", false, "Whether the editor has a signature help provider");
    /// <summary>Whether the editor has an inline hints provider.</summary>
    public static readonly RawContextKey<bool> HasInlayHintsProvider = new("editorHasInlayHintsProvider", false, "Whether the editor has an inline hints provider");

    // -- mode context keys: formatting
    /// <summary>Whether the editor has a document formatting provider.</summary>
    public static readonly RawContextKey<bool> HasDocumentFormattingProvider = new("editorHasDocumentFormattingProvider", false, "Whether the editor has a document formatting provider");
    /// <summary>Whether the editor has a document selection formatting provider.</summary>
    public static readonly RawContextKey<bool> HasDocumentSelectionFormattingProvider = new("editorHasDocumentSelectionFormattingProvider", false, "Whether the editor has a document selection formatting provider");
    /// <summary>Whether the editor has multiple document formatting providers.</summary>
    public static readonly RawContextKey<bool> HasMultipleDocumentFormattingProvider = new("editorHasMultipleDocumentFormattingProvider", false, "Whether the editor has multiple document formatting providers");
    /// <summary>Whether the editor has multiple document selection formatting providers.</summary>
    public static readonly RawContextKey<bool> HasMultipleDocumentSelectionFormattingProvider = new("editorHasMultipleDocumentSelectionFormattingProvider", false, "Whether the editor has multiple document selection formatting providers");

    /// <summary>Whether any diagnostic is present in the current editor selection.</summary>
    public static readonly RawContextKey<bool> SelectionHasDiagnostics = new("editorSelectionHasDiagnostics", false, "Whether any diagnostic is present in the current editor selection");

    /// <summary>
    /// The infos of the keys above that are not hidden, in declaration order (what upstream's <c>RawContextKey.all()</c>
    /// returns for this file). Declared last: static fields initialize in textual order.
    /// </summary>
    public static readonly ImmutableArray<ContextKeyInfo> All = CollectInfos(
        [
            EditorSimpleInput,
            EditorTextFocus,
            Focus,
            TextInputFocus,
            ReadOnly,
            InDiffEditor,
            IsEmbeddedDiffEditor,
            InMultiDiffEditor,
            MultiDiffEditorAllCollapsed,
            MultiDiffEditorItemAllUnchangedRegionsShown,
            MultiDiffEditorRenderSideBySide,
            HasChanges,
            ComparingMovedCode,
            AccessibleDiffViewerVisible,
            DiffEditorRenderSideBySideInlineBreakpointReached,
            DiffEditorInlineMode,
            DiffEditorTemporaryInlineMode,
            DiffEditorAutomaticRenderSideBySide,
            DiffEditorOriginalWritable,
            DiffEditorModifiedWritable,
            DiffEditorOriginalUri,
            DiffEditorModifiedUri,
            ColumnSelection,
            HasNonEmptySelection,
            HasMultipleSelections,
            TabMovesFocus,
            IsInEmbeddedEditor,
            CanUndo,
            CanRedo,
            HoverVisible,
            HoverFocused,
            StickyScrollFocused,
            StickyScrollVisible,
            StandaloneColorPickerVisible,
            StandaloneColorPickerFocused,
            IsComposing,
            InCompositeEditor,
            LanguageId,
            HasCompletionItemProvider,
            HasCodeActionsProvider,
            HasCodeLensProvider,
            HasDefinitionProvider,
            HasDeclarationProvider,
            HasImplementationProvider,
            HasTypeDefinitionProvider,
            HasHoverProvider,
            HasDocumentHighlightProvider,
            HasDocumentSymbolProvider,
            HasReferenceProvider,
            HasRenameProvider,
            HasSignatureHelpProvider,
            HasInlayHintsProvider,
            HasDocumentFormattingProvider,
            HasDocumentSelectionFormattingProvider,
            HasMultipleDocumentFormattingProvider,
            HasMultipleDocumentSelectionFormattingProvider,
            SelectionHasDiagnostics,
        ]);

    private static ImmutableArray<ContextKeyInfo> CollectInfos(ContextKeyDefinedExpr[] keys)
    {
        var builder = ImmutableArray.CreateBuilder<ContextKeyInfo>(keys.Length);
        foreach (var key in keys)
        {
            var info = key switch
            {
                RawContextKey<bool> k => k.Info,
                RawContextKey<bool?> k => k.Info,
                RawContextKey<string> k => k.Info,
                _ => null,
            };
            if (info is not null)
            {
                builder.Add(info);
            }
        }
        return builder.ToImmutable();
    }
}
