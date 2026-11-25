// Ported from vscode (src/vs/editor/browser/widget/codeEditor/codeEditorWidget.ts: EditorModeContext, commit b57d5c9e),
// MIT license. Differences from upstream: no model-swap event (the editor's model is fixed), the language event comes
// from the model; isInEmbeddedEditor is not set (there is no embedded editor yet); the update is synchronous (upstream
// batches it with bufferChangeEvents).

using TKCode.Arc.Editor;
using TKCode.Arc.Editor.Languages;
using TKCode.Arc.Editor.Model;
using TKCode.Arc.Editor.Platform;

namespace TKCode.Arc;

/// <summary>
/// Keeps the context keys that say which language features the editor's model has (<c>editorLangId</c>,
/// <c>editorHasHoverProvider</c>, ...) up to date as providers register and the language changes, so that keybindings
/// and actions can depend on them.
/// </summary>
internal sealed class CodeEditorModeContext : IDisposable
{
    private readonly TextModel _model;
    private readonly LanguageFeatures _features;
    private readonly List<Action> _unsubscribe = new();
    private readonly IContextKey<string> _langId;
    private readonly IContextKey<bool> _hasCompletionItemProvider;
    private readonly IContextKey<bool> _hasCodeActionsProvider;
    private readonly IContextKey<bool> _hasCodeLensProvider;
    private readonly IContextKey<bool> _hasDefinitionProvider;
    private readonly IContextKey<bool> _hasDeclarationProvider;
    private readonly IContextKey<bool> _hasImplementationProvider;
    private readonly IContextKey<bool> _hasTypeDefinitionProvider;
    private readonly IContextKey<bool> _hasHoverProvider;
    private readonly IContextKey<bool> _hasDocumentHighlightProvider;
    private readonly IContextKey<bool> _hasDocumentSymbolProvider;
    private readonly IContextKey<bool> _hasReferenceProvider;
    private readonly IContextKey<bool> _hasRenameProvider;
    private readonly IContextKey<bool> _hasDocumentFormattingProvider;
    private readonly IContextKey<bool> _hasDocumentSelectionFormattingProvider;
    private readonly IContextKey<bool> _hasMultipleDocumentFormattingProvider;
    private readonly IContextKey<bool> _hasMultipleDocumentSelectionFormattingProvider;
    private readonly IContextKey<bool> _hasSignatureHelpProvider;
    private readonly IContextKey<bool> _hasInlayHintsProvider;

    public CodeEditorModeContext(TextModel model, IContextKeyService contextKeyService, LanguageFeatures features)
    {
        _model = model;
        _features = features;
        _langId = EditorContextKeys.LanguageId.BindTo(contextKeyService);
        _hasCompletionItemProvider = EditorContextKeys.HasCompletionItemProvider.BindTo(contextKeyService);
        _hasCodeActionsProvider = EditorContextKeys.HasCodeActionsProvider.BindTo(contextKeyService);
        _hasCodeLensProvider = EditorContextKeys.HasCodeLensProvider.BindTo(contextKeyService);
        _hasDefinitionProvider = EditorContextKeys.HasDefinitionProvider.BindTo(contextKeyService);
        _hasDeclarationProvider = EditorContextKeys.HasDeclarationProvider.BindTo(contextKeyService);
        _hasImplementationProvider = EditorContextKeys.HasImplementationProvider.BindTo(contextKeyService);
        _hasTypeDefinitionProvider = EditorContextKeys.HasTypeDefinitionProvider.BindTo(contextKeyService);
        _hasHoverProvider = EditorContextKeys.HasHoverProvider.BindTo(contextKeyService);
        _hasDocumentHighlightProvider = EditorContextKeys.HasDocumentHighlightProvider.BindTo(contextKeyService);
        _hasDocumentSymbolProvider = EditorContextKeys.HasDocumentSymbolProvider.BindTo(contextKeyService);
        _hasReferenceProvider = EditorContextKeys.HasReferenceProvider.BindTo(contextKeyService);
        _hasRenameProvider = EditorContextKeys.HasRenameProvider.BindTo(contextKeyService);
        _hasSignatureHelpProvider = EditorContextKeys.HasSignatureHelpProvider.BindTo(contextKeyService);
        _hasInlayHintsProvider = EditorContextKeys.HasInlayHintsProvider.BindTo(contextKeyService);
        _hasDocumentFormattingProvider = EditorContextKeys.HasDocumentFormattingProvider.BindTo(contextKeyService);
        _hasDocumentSelectionFormattingProvider = EditorContextKeys.HasDocumentSelectionFormattingProvider.BindTo(contextKeyService);
        _hasMultipleDocumentFormattingProvider = EditorContextKeys.HasMultipleDocumentFormattingProvider.BindTo(contextKeyService);
        _hasMultipleDocumentSelectionFormattingProvider = EditorContextKeys.HasMultipleDocumentSelectionFormattingProvider.BindTo(contextKeyService);

        // update when the language changes
        Action<ModelLanguageChangedEvent> onLanguage = _ => Update();
        model.DidChangeLanguage += onLanguage;
        _unsubscribe.Add(() => model.DidChangeLanguage -= onLanguage);

        // update when registries change
        Watch(features.CompletionProvider);
        Watch(features.CodeActionProvider);
        Watch(features.CodeLensProvider);
        Watch(features.DefinitionProvider);
        Watch(features.DeclarationProvider);
        Watch(features.ImplementationProvider);
        Watch(features.TypeDefinitionProvider);
        Watch(features.HoverProvider);
        Watch(features.DocumentHighlightProvider);
        Watch(features.DocumentSymbolProvider);
        Watch(features.ReferenceProvider);
        Watch(features.RenameProvider);
        Watch(features.DocumentFormattingEditProvider);
        Watch(features.DocumentRangeFormattingEditProvider);
        Watch(features.SignatureHelpProvider);
        Watch(features.InlayHintsProvider);

        Update();
    }

    public void Dispose()
    {
        foreach (var unsubscribe in _unsubscribe)
        {
            unsubscribe();
        }

        _unsubscribe.Clear();
    }

    private void Watch<T>(LanguageFeatureRegistry<T> registry)
        where T : class
    {
        Action<int> onChange = _ => Update();
        registry.DidChange += onChange;
        _unsubscribe.Add(() => registry.DidChange -= onChange);
    }

    private void Update()
    {
        if (_model.IsDisposed())
        {
            Reset();
            return;
        }

        var model = _model;
        _langId.Set(model.GetLanguageId());
        _hasCompletionItemProvider.Set(_features.CompletionProvider.Has(model));
        _hasCodeActionsProvider.Set(_features.CodeActionProvider.Has(model));
        _hasCodeLensProvider.Set(_features.CodeLensProvider.Has(model));
        _hasDefinitionProvider.Set(_features.DefinitionProvider.Has(model));
        _hasDeclarationProvider.Set(_features.DeclarationProvider.Has(model));
        _hasImplementationProvider.Set(_features.ImplementationProvider.Has(model));
        _hasTypeDefinitionProvider.Set(_features.TypeDefinitionProvider.Has(model));
        _hasHoverProvider.Set(_features.HoverProvider.Has(model));
        _hasDocumentHighlightProvider.Set(_features.DocumentHighlightProvider.Has(model));
        _hasDocumentSymbolProvider.Set(_features.DocumentSymbolProvider.Has(model));
        _hasReferenceProvider.Set(_features.ReferenceProvider.Has(model));
        _hasRenameProvider.Set(_features.RenameProvider.Has(model));
        _hasSignatureHelpProvider.Set(_features.SignatureHelpProvider.Has(model));
        _hasInlayHintsProvider.Set(_features.InlayHintsProvider.Has(model));
        _hasDocumentFormattingProvider.Set(_features.DocumentFormattingEditProvider.Has(model) || _features.DocumentRangeFormattingEditProvider.Has(model));
        _hasDocumentSelectionFormattingProvider.Set(_features.DocumentRangeFormattingEditProvider.Has(model));
        _hasMultipleDocumentFormattingProvider.Set(_features.DocumentFormattingEditProvider.All(model).Count + _features.DocumentRangeFormattingEditProvider.All(model).Count > 1);
        _hasMultipleDocumentSelectionFormattingProvider.Set(_features.DocumentRangeFormattingEditProvider.All(model).Count > 1);
    }

    private void Reset()
    {
        _langId.Reset();
        _hasCompletionItemProvider.Reset();
        _hasCodeActionsProvider.Reset();
        _hasCodeLensProvider.Reset();
        _hasDefinitionProvider.Reset();
        _hasDeclarationProvider.Reset();
        _hasImplementationProvider.Reset();
        _hasTypeDefinitionProvider.Reset();
        _hasHoverProvider.Reset();
        _hasDocumentHighlightProvider.Reset();
        _hasDocumentSymbolProvider.Reset();
        _hasReferenceProvider.Reset();
        _hasRenameProvider.Reset();
        _hasDocumentFormattingProvider.Reset();
        _hasDocumentSelectionFormattingProvider.Reset();
        _hasMultipleDocumentFormattingProvider.Reset();
        _hasMultipleDocumentSelectionFormattingProvider.Reset();
        _hasSignatureHelpProvider.Reset();
        _hasInlayHintsProvider.Reset();
    }
}
