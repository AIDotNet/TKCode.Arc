// The server's features as language feature providers: one registration per registry set (an editor's
// LanguageFeatures) and language, shared by the attached documents that use it, registering only what the server's
// capabilities announce.

using TKCode.Arc.Editor.Languages;
using TKCode.Arc.Editor.Lsp.Protocol;

namespace TKCode.Arc.Editor.Lsp;

internal sealed class LspFeatureRegistration
{
    private readonly List<IDisposable> _registrations = new();
    private int _references;

    public LspFeatureRegistration(LanguageClient client, LanguageFeatures features, string languageId, LspServerCapabilities capabilities)
    {
        Features = features;
        LanguageId = languageId;
        Providers = new LspProviders(client, capabilities);
        LanguageSelector selector = languageId;
        var p = Providers;
        var c = capabilities;

        Add(c.Completion, () => features.CompletionProvider.Register(selector, p));
        Add(c.Hover, () => features.HoverProvider.Register(selector, p));
        Add(c.SignatureHelp, () => features.SignatureHelpProvider.Register(selector, p));
        Add(c.Definition, () => features.DefinitionProvider.Register(selector, p));
        Add(c.Declaration, () => features.DeclarationProvider.Register(selector, p));
        Add(c.Implementation, () => features.ImplementationProvider.Register(selector, p));
        Add(c.TypeDefinition, () => features.TypeDefinitionProvider.Register(selector, p));
        Add(c.References, () => features.ReferenceProvider.Register(selector, p));
        Add(c.DocumentHighlight, () => features.DocumentHighlightProvider.Register(selector, p));
        Add(c.Rename, () => features.RenameProvider.Register(selector, p));
        Add(c.CodeAction, () => features.CodeActionProvider.Register(selector, p));
        Add(c.CodeLens, () => features.CodeLensProvider.Register(selector, p));
        Add(c.DocumentFormatting, () => features.DocumentFormattingEditProvider.Register(selector, p));
        Add(c.DocumentRangeFormatting, () => features.DocumentRangeFormattingEditProvider.Register(selector, p));
        Add(c.OnTypeFormattingTriggerCharacters is { Count: > 0 }, () => features.OnTypeFormattingEditProvider.Register(selector, p));
        Add(c.DocumentSymbol, () => features.DocumentSymbolProvider.Register(selector, p));
        Add(c.FoldingRange, () => features.FoldingRangeProvider.Register(selector, p));
        Add(c.SelectionRange, () => features.SelectionRangeProvider.Register(selector, p));
        Add(c.SemanticTokensLegend is not null && c.SemanticTokensFull, () => features.DocumentSemanticTokensProvider.Register(selector, p));
        Add(c.SemanticTokensLegend is not null && c.SemanticTokensRange, () => features.DocumentRangeSemanticTokensProvider.Register(selector, p));
        Add(c.InlayHint, () => features.InlayHintsProvider.Register(selector, p));
        Add(c.Color, () => features.ColorProvider.Register(selector, p));
        Add(c.LinkedEditingRange, () => features.LinkedEditingRangeProvider.Register(selector, p));
    }

    public LanguageFeatures Features { get; }

    public string LanguageId { get; }

    public LspProviders Providers { get; }

    /// <summary>How many providers are registered (for tests).</summary>
    public int Count => _registrations.Count;

    public void AddReference() => _references++;

    /// <summary>Drops a reference; true (and the providers are unregistered) when it was the last.</summary>
    public bool Release()
    {
        if (--_references > 0)
        {
            return false;
        }
        for (var i = _registrations.Count - 1; i >= 0; i--)
        {
            _registrations[i].Dispose();
        }
        _registrations.Clear();
        return true;
    }

    private void Add(bool announced, Func<IDisposable> register)
    {
        if (announced)
        {
            _registrations.Add(register());
        }
    }
}
