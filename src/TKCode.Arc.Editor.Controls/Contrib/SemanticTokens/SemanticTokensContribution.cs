// Ported from vscode (src/vs/editor/contrib/semanticTokens/browser/documentSemanticTokens.ts: the editor feature
// registration, and viewportSemanticTokens.ts: ViewportSemanticTokensContribution; commit b57d5c9e), MIT license.
//
// Differences: upstream's DocumentSemanticTokensFeature is one per workbench over every model; here each editor has
// its own (an editor owns its model), created with this contribution, together with the viewport part. The semantic
// highlighting setting is a property here (upstream: editor.semanticHighlighting.enabled per language), and the
// standalone theme's "semanticHighlighting" (false upstream) can be turned on with ThemeSemanticHighlighting.

using TKCode.Arc.Editor;
using TKCode.Arc.Editor.Contrib.SemanticTokens;
using TKCode.Arc.Editor.Model;
using TKCode.Arc.Editor.Services;

namespace TKCode.Arc;

/// <summary>Colors the text with the semantic tokens of the providers, over the syntax colors.</summary>
internal sealed class SemanticTokensContribution : ICodeEditorContribution
{
    public const string Id = "editor.contrib.viewportSemanticTokens";

    public static readonly EditorContributionDescription Description =
        new(Id, editor => new SemanticTokensContribution(editor), EditorContributionInstantiation.AfterFirstRender);

    private readonly CodeEditor _editor;
    private readonly StandaloneSemanticTokensThemeService _themeService;
    private readonly SemanticTokensStylingService _stylingService;
    private readonly DocumentSemanticTokensFeature _documentFeature;
    private readonly IDisposable _modelRegistration;
    private readonly ViewportSemanticTokens _viewport;

    private SemanticTokensContribution(CodeEditor editor)
    {
        _editor = editor;
        _themeService = new StandaloneSemanticTokensThemeService(editor.LanguageServices.ThemeService);
        _stylingService = new SemanticTokensStylingService(_themeService, message => UnexpectedErrors.Report(new InvalidOperationException(message)));
        _documentFeature = new DocumentSemanticTokensFeature(_stylingService, editor.LanguageFeatures.DocumentSemanticTokensProvider, EditorSemanticHighlighting.ConfiguredByTheme, CodeEditor.IdleScheduler);
        _modelRegistration = _documentFeature.AddModel(editor.Model);
        _viewport = new ViewportSemanticTokens(
            () => _editor.Model,
            () => (IReadOnlyList<TextRange>?)_editor.ViewModel?.GetVisibleRangesPlusViewportAboveBelow() ?? [],
            () => _documentFeature.Setting,
            _stylingService,
            editor.LanguageFeatures.DocumentRangeSemanticTokensProvider,
            CodeEditor.IdleScheduler);

        editor.DidScrollChange += OnScroll;
        editor.DidChangeModelContent += OnContentChanged;
        editor.DidChangeModelLanguage += OnLanguageChanged;
    }

    public static SemanticTokensContribution? Get(CodeEditor editor) => editor.GetContribution<SemanticTokensContribution>(Id);

    /// <summary>Upstream's editor.semanticHighlighting.enabled: on, off, or as the theme says (the default).</summary>
    public EditorSemanticHighlighting Setting
    {
        get => _documentFeature.Setting;
        set
        {
            if (_documentFeature.Setting != value)
            {
                _documentFeature.Setting = value;
                _viewport.HandleSettingChanged();
            }
        }
    }

    /// <summary>What the theme answers for semantic highlighting when the setting is <see cref="EditorSemanticHighlighting.ConfiguredByTheme"/>.</summary>
    public bool ThemeSemanticHighlighting
    {
        get => _themeService.ThemeSemanticHighlighting;
        set => _themeService.ThemeSemanticHighlighting = value; // announced as a theme change
    }

    /// <summary>The coloring of the editor's model (for tests).</summary>
    internal ModelSemanticColoring? DocumentColoring => _documentFeature.GetWatcher(_editor.Model);

    /// <summary>The viewport part (for tests).</summary>
    internal ViewportSemanticTokens Viewport => _viewport;

    private void OnScroll(TKCode.Arc.Editor.ViewModel.ScrollChangedEvent e) => _viewport.ScheduleTokenizeViewport();

    private void OnContentChanged(ModelContentChangedEvent e) => _viewport.HandleContentChanged();

    private void OnLanguageChanged(ModelLanguageChangedEvent e) => _viewport.HandleModelOrLanguageChanged();

    public void Dispose()
    {
        _editor.DidScrollChange -= OnScroll;
        _editor.DidChangeModelContent -= OnContentChanged;
        _editor.DidChangeModelLanguage -= OnLanguageChanged;
        _viewport.Dispose();
        _modelRegistration.Dispose();
        _documentFeature.Dispose();
        _stylingService.Dispose();
        _themeService.Dispose();
    }
}
