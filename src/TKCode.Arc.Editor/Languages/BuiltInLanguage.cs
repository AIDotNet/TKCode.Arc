// The built-in languages (Languages/BuiltIn/*.g.cs, generated from monaco-editor by tools/editor/monarch-convert) and
// how a host registers them. Modelled on monaco-editor's src/languages/definitions/_.contribution.ts (commit a310cd1,
// MIT license): a language is known at once (id, extensions, aliases), and its configuration and tokenizer are created
// the first time a model uses it (upstream: the lazy loader, run on onLanguage).

using TKCode.Arc.Editor.Standalone;
using TKCode.Arc.Editor.Standalone.Monarch;

namespace TKCode.Arc.Editor.Languages;

/// <summary>The services a language registers with; one set per host (an app, or one editor).</summary>
internal sealed class LanguageServices : IDisposable
{
    public LanguageServices()
    {
        LanguageService = new LanguageService();
        LanguageConfigurationService = new LanguageConfigurationService();
        TokenizationRegistry = new TokenizationRegistry<ITokenizationSupport>();
        ThemeService = new StandaloneThemeService(TokenizationRegistry);
        LanguageFeatures = new LanguageFeatures();
    }

    public LanguageService LanguageService { get; }

    public LanguageConfigurationService LanguageConfigurationService { get; }

    public TokenizationRegistry<ITokenizationSupport> TokenizationRegistry { get; }

    public StandaloneThemeService ThemeService { get; }

    /// <summary>The language feature providers (hover, completion, definitions, ...; upstream's ILanguageFeaturesService).</summary>
    public LanguageFeatures LanguageFeatures { get; }

    public void Dispose() => LanguageConfigurationService.Dispose();
}

/// <summary>A built-in language: its registration, language configuration and Monarch definition.</summary>
internal sealed class BuiltInLanguage(
    LanguageExtensionPoint registration,
    Func<LanguageConfiguration> createConfiguration,
    Func<MonarchLanguage> createMonarchLanguage)
{
    public string Id => Registration.Id;

    public LanguageExtensionPoint Registration { get; } = registration;

    /// <summary>A new copy of the language configuration (brackets, comments, auto-closing pairs, indentation, ...).</summary>
    public LanguageConfiguration CreateConfiguration() => createConfiguration();

    /// <summary>A new copy of the Monarch definition.</summary>
    public MonarchLanguage CreateMonarchLanguage() => createMonarchLanguage();

    /// <summary>
    /// Registers the language with <paramref name="services"/>: its id, extensions and names at once; its configuration
    /// and its tokenizer when a model first uses it (a lazy tokenization support, which the model resolves).
    /// </summary>
    public IDisposable Register(LanguageServices services, int maxTokenizationLineLength = MonarchTokenizer.DefaultMaxTokenizationLineLength)
    {
        var registrations = new List<IDisposable> { services.LanguageService.RegisterLanguage(Registration) };
        var gate = new object();
        IDisposable? configurationRegistration = null;
        var factory = new LazyTokenizationSupport<ITokenizationSupport>(() =>
        {
            lock (gate)
            {
                configurationRegistration ??= services.LanguageConfigurationService.Register(Id, CreateConfiguration());
            }

            var lexer = MonarchCompile.Compile(Id, CreateMonarchLanguage());
            ITokenizationSupport tokenizer = new MonarchTokenizer(
                services.LanguageService, services.ThemeService, services.TokenizationRegistry, Id, lexer, maxTokenizationLineLength);
            return Task.FromResult<ITokenizationSupport?>(tokenizer);
        });
        registrations.Add(services.TokenizationRegistry.RegisterFactory(Id, factory));
        registrations.Add(factory);
        return Platform.RegistrationDisposable.ToDisposable(() =>
        {
            foreach (var registration in registrations)
            {
                registration.Dispose();
            }

            lock (gate)
            {
                configurationRegistration?.Dispose();
            }
        });
    }
}
