// Ported from vscode (src/vs/editor/common/services/languageFeatures.ts and languageFeaturesService.ts, commit
// b57d5c9e), MIT license.
// Differences from upstream:
//  - an instance per LanguageServices (one per editor unless an application shares one), not a service from the
//    instantiation container: providers register on the instance their editor uses;
//  - the registries of features that are not ported yet are left out: documentPasteEditProvider, newSymbolNamesProvider,
//    structuralDiffProvider, multiDocumentHighlightProvider, inlineValuesProvider, evaluatableExpressionProvider,
//    documentDropEditProvider.

namespace TKCode.Arc.Editor.Languages;

/// <summary>
/// The language feature providers (upstream's ILanguageFeaturesService): one <see cref="LanguageFeatureRegistry{T}"/>
/// per feature. Register a provider with a selector; the features ask the registry for the providers of their model.
/// </summary>
internal sealed class LanguageFeatures
{
    private NotebookInfoResolver? _notebookTypeResolver;

    public LanguageFeatures()
    {
        NotebookInfoResolver resolver = uri => _notebookTypeResolver?.Invoke(uri);
        ReferenceProvider = new(resolver);
        DefinitionProvider = new(resolver);
        TypeDefinitionProvider = new(resolver);
        DeclarationProvider = new(resolver);
        ImplementationProvider = new(resolver);
        CodeActionProvider = new(resolver);
        RenameProvider = new(resolver);
        DocumentFormattingEditProvider = new(resolver);
        DocumentRangeFormattingEditProvider = new(resolver);
        OnTypeFormattingEditProvider = new(resolver);
        DocumentSymbolProvider = new(resolver);
        InlayHintsProvider = new(resolver);
        ColorProvider = new(resolver);
        CodeLensProvider = new(resolver);
        SignatureHelpProvider = new(resolver);
        HoverProvider = new(resolver);
        DocumentHighlightProvider = new(resolver);
        DocumentRangeSemanticTokensProvider = new(resolver);
        DocumentSemanticTokensProvider = new(resolver);
        SelectionRangeProvider = new(resolver);
        FoldingRangeProvider = new(resolver);
        LinkProvider = new(resolver);
        InlineCompletionsProvider = new(resolver);
        CompletionProvider = new(resolver);
        LinkedEditingRangeProvider = new(resolver);
    }

    public LanguageFeatureRegistry<IReferenceProvider> ReferenceProvider { get; }

    public LanguageFeatureRegistry<IDefinitionProvider> DefinitionProvider { get; }

    public LanguageFeatureRegistry<ITypeDefinitionProvider> TypeDefinitionProvider { get; }

    public LanguageFeatureRegistry<IDeclarationProvider> DeclarationProvider { get; }

    public LanguageFeatureRegistry<IImplementationProvider> ImplementationProvider { get; }

    public LanguageFeatureRegistry<ICodeActionProvider> CodeActionProvider { get; }

    public LanguageFeatureRegistry<IRenameProvider> RenameProvider { get; }

    public LanguageFeatureRegistry<IDocumentFormattingEditProvider> DocumentFormattingEditProvider { get; }

    public LanguageFeatureRegistry<IDocumentRangeFormattingEditProvider> DocumentRangeFormattingEditProvider { get; }

    public LanguageFeatureRegistry<IOnTypeFormattingEditProvider> OnTypeFormattingEditProvider { get; }

    public LanguageFeatureRegistry<IDocumentSymbolProvider> DocumentSymbolProvider { get; }

    public LanguageFeatureRegistry<IInlayHintsProvider> InlayHintsProvider { get; }

    public LanguageFeatureRegistry<IDocumentColorProvider> ColorProvider { get; }

    public LanguageFeatureRegistry<ICodeLensProvider> CodeLensProvider { get; }

    public LanguageFeatureRegistry<ISignatureHelpProvider> SignatureHelpProvider { get; }

    public LanguageFeatureRegistry<IHoverProvider> HoverProvider { get; }

    public LanguageFeatureRegistry<IDocumentHighlightProvider> DocumentHighlightProvider { get; }

    public LanguageFeatureRegistry<IDocumentRangeSemanticTokensProvider> DocumentRangeSemanticTokensProvider { get; }

    public LanguageFeatureRegistry<IDocumentSemanticTokensProvider> DocumentSemanticTokensProvider { get; }

    public LanguageFeatureRegistry<ISelectionRangeProvider> SelectionRangeProvider { get; }

    public LanguageFeatureRegistry<IFoldingRangeProvider> FoldingRangeProvider { get; }

    public LanguageFeatureRegistry<ILinkProvider> LinkProvider { get; }

    public LanguageFeatureRegistry<IInlineCompletionsProvider> InlineCompletionsProvider { get; }

    public LanguageFeatureRegistry<ICompletionItemProvider> CompletionProvider { get; }

    public LanguageFeatureRegistry<ILinkedEditingRangeProvider> LinkedEditingRangeProvider { get; }

    /// <summary>Tells the registries which notebook a document belongs to (null: none).</summary>
    public void SetNotebookTypeResolver(NotebookInfoResolver? resolver) => _notebookTypeResolver = resolver;
}
