// Ported from vscode (src/vs/editor/common/services/semanticTokensStylingService.ts and the getTokenStyleMetadata /
// semanticHighlighting members of StandaloneTheme in src/vs/editor/standalone/browser/standaloneThemeService.ts, commit
// b57d5c9e), MIT license.
//
// Differences:
//  - the cache is a ConditionalWeakTable per provider (upstream's WeakMap), cleared when the theme changes;
//  - the service is an instance per editor (upstream: a singleton), told the model's language id codec when styling is
//    created;
//  - the standalone theme's semantic rules are its token rules: a semantic token "type.modifier1.modifier2" is matched
//    against the theme's trie as upstream's tokenTheme._match does, so a rule such as { token: "variable.readonly" }
//    styles readonly variables; the adapter is StandaloneSemanticTokensThemeService. Upstream's standalone theme never
//    asks for semantic highlighting (semanticHighlighting = false); the adapter takes it as an option.

using System.Runtime.CompilerServices;
using TKCode.Arc.Editor.Languages;
using TKCode.Arc.Editor.Standalone;
using TKCode.Arc.Editor.Tokens;

namespace TKCode.Arc.Editor.Services;

/// <summary>Creates and caches the styling of each semantic tokens provider (upstream's ISemanticTokensStylingService).</summary>
internal sealed class SemanticTokensStylingService : IDisposable
{
    private readonly ISemanticTokensThemeService _themeService;
    private readonly Action<string>? _log;
    private ConditionalWeakTable<object, SemanticTokensProviderStyling> _caches = new();

    public SemanticTokensStylingService(ISemanticTokensThemeService themeService, Action<string>? log = null)
    {
        _themeService = themeService;
        _log = log;
        _themeService.DidColorThemeChange += OnThemeChanged;
    }

    /// <summary>The theme the stylings read.</summary>
    public ISemanticTokensThemeService ThemeService => _themeService;

    private void OnThemeChanged() => _caches = new ConditionalWeakTable<object, SemanticTokensProviderStyling>();

    public SemanticTokensProviderStyling GetStyling(IDocumentSemanticTokensProvider provider, ILanguageIdCodec languageIdCodec) =>
        GetStyling(provider, provider.GetLegend, languageIdCodec);

    public SemanticTokensProviderStyling GetStyling(IDocumentRangeSemanticTokensProvider provider, ILanguageIdCodec languageIdCodec) =>
        GetStyling(provider, provider.GetLegend, languageIdCodec);

    private SemanticTokensProviderStyling GetStyling(object provider, Func<SemanticTokensLegend> getLegend, ILanguageIdCodec languageIdCodec)
    {
        if (!_caches.TryGetValue(provider, out var styling))
        {
            styling = new SemanticTokensProviderStyling(getLegend(), _themeService, languageIdCodec, _log);
            _caches.AddOrUpdate(provider, styling);
        }

        return styling;
    }

    public void Dispose() => _themeService.DidColorThemeChange -= OnThemeChanged;
}

/// <summary>A standalone theme as semantic tokens see it (upstream: StandaloneTheme's getTokenStyleMetadata).</summary>
internal sealed class StandaloneSemanticTokensTheme(IStandaloneTheme theme, bool semanticHighlighting) : ISemanticTokensColorTheme
{
    public bool SemanticHighlighting { get; } = semanticHighlighting;

    public TokenStyle? GetTokenStyleMetadata(string type, IReadOnlyList<string> modifiers, string modelLanguage)
    {
        // use theme rules match
        var token = modifiers.Count == 0 ? type : type + "." + string.Join('.', modifiers);
        var style = theme.TokenTheme.MatchRule(token);
        var metadata = style.Metadata;
        var foreground = TokenMetadata.GetForeground(metadata);
        var fontStyle = TokenMetadata.GetFontStyle(metadata);
        return new TokenStyle(
            (int)foreground,
            Bold: (fontStyle & FontStyle.Bold) != 0,
            Italic: (fontStyle & FontStyle.Italic) != 0,
            Underline: (fontStyle & FontStyle.Underline) != 0,
            Strikethrough: (fontStyle & FontStyle.Strikethrough) != 0);
    }
}

/// <summary>The standalone theme service as semantic tokens see it.</summary>
internal sealed class StandaloneSemanticTokensThemeService : ISemanticTokensThemeService, IDisposable
{
    private readonly StandaloneThemeService _themeService;

    public StandaloneSemanticTokensThemeService(StandaloneThemeService themeService)
    {
        _themeService = themeService;
        _themeService.DidColorThemeChange += OnThemeChanged;
    }

    public event Action? DidColorThemeChange;

    /// <summary>
    /// Upstream's standalone theme answers false; an editor may ask for semantic highlighting by theme here. A change
    /// is announced as a theme change.
    /// </summary>
    public bool ThemeSemanticHighlighting
    {
        get => _themeSemanticHighlighting;
        set
        {
            if (_themeSemanticHighlighting != value)
            {
                _themeSemanticHighlighting = value;
                DidColorThemeChange?.Invoke();
            }
        }
    }

    private bool _themeSemanticHighlighting;

    public ISemanticTokensColorTheme GetColorTheme() => new StandaloneSemanticTokensTheme(_themeService.GetColorTheme(), ThemeSemanticHighlighting);

    private void OnThemeChanged(StandaloneTheme theme) => DidColorThemeChange?.Invoke();

    public void Dispose() => _themeService.DidColorThemeChange -= OnThemeChanged;
}
