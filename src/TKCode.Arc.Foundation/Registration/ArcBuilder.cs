using Avalonia;
using TKCode.Arc.Localization;

namespace TKCode.Arc;

/// <summary>How control-theme files are registered.</summary>
public enum ArcThemeLoading
{
    /// <summary>A theme file is instantiated the first time one of its keys is looked up (default).</summary>
    Lazy = 0,

    /// <summary>Every theme file is instantiated at startup.</summary>
    Eager = 1
}

/// <summary>
/// Collects the packages and the initial theme of an application. Given to <c>AppBuilder.UseArc(...)</c>; packages add
/// themselves with extension methods (<c>arc.UseArcBlocks()</c>) that call <see cref="UsePackage"/>.
/// </summary>
/// <remarks>
/// The configuration callbacks run before the application exists: they must not touch Avalonia platform objects.
/// <see cref="ArcRuntime.UsePackages"/> hands one to a running application's callback; that one enables packages only.
/// </remarks>
public sealed class ArcBuilder
{
    private readonly List<ArcPackageBuilder> _packages = new();
    private readonly Dictionary<string, ArcPackageBuilder> _byId = new(StringComparer.Ordinal);
    private readonly List<string> _defining = new();
    private readonly List<Action<ArcThemeBuilder>> _theme = new();
    private readonly Dictionary<string, ArcThemePreset> _presets = new(StringComparer.Ordinal);
    private readonly HashSet<string>? _running;

    /// <summary>Creates a builder for <c>UseArc(...)</c> or <see cref="ArcRuntime.Install"/>.</summary>
    public ArcBuilder()
    {
    }

    /// <summary>
    /// A builder for <see cref="ArcRuntime.UsePackages"/>: the packages <paramref name="runtime"/> runs count as enabled,
    /// its presets are known, and its languages are the ones every new catalog must cover.
    /// </summary>
    internal ArcBuilder(ArcRuntime runtime)
    {
        _running = new HashSet<string>(runtime.Packages, StringComparer.Ordinal);
        foreach (var (name, preset) in runtime.Presets)
        {
            _presets.Add(name, preset);
        }
        DefaultLanguage = runtime.Languages.DefaultLanguage;
        SupportedLanguages = runtime.Languages.SupportedLanguages;
    }

    /// <summary>
    /// The enabled packages, dependencies before dependents. In a builder from <see cref="ArcRuntime.UsePackages"/>, only
    /// the packages the running application did not have yet.
    /// </summary>
    public IReadOnlyList<ArcPackageBuilder> Packages => _packages;

    /// <summary>The theme presets the enabled packages registered, by <see cref="ArcThemePreset.Name"/>.</summary>
    public IReadOnlyDictionary<string, ArcThemePreset> Presets => _presets;

    /// <summary>How theme files are registered.</summary>
    public ArcThemeLoading ThemeLoading { get; private set; }

    internal IReadOnlyList<Action<ArcThemeBuilder>> ThemeConfigurations => _theme;

    /// <summary>The language the application starts in and the first fallback for missing text (en-US by default).</summary>
    public LanguageTag DefaultLanguage { get; private set; } = LanguageTags.EnUS;

    /// <summary>The languages the application offers (en-US only by default); every enabled catalog must cover each.</summary>
    public IReadOnlyList<LanguageTag> SupportedLanguages { get; private set; } = [LanguageTags.EnUS];

    /// <summary>
    /// Chooses the application's languages. At startup every enabled package's catalogs must cover each of
    /// <paramref name="supportedLanguages"/> completely, or <c>UseArc</c> throws and lists what is missing.
    /// </summary>
    /// <param name="defaultLanguage">The language to start in; one of <paramref name="supportedLanguages"/>.</param>
    /// <param name="supportedLanguages">The languages <see cref="ArcLanguage.Set(LanguageTag)"/> may switch to.</param>
    /// <exception cref="ArgumentException"><paramref name="defaultLanguage"/> is not one of <paramref name="supportedLanguages"/>.</exception>
    public ArcBuilder UseLanguages(LanguageTag defaultLanguage, IEnumerable<LanguageTag> supportedLanguages)
    {
        ThrowIfRunning(nameof(UseLanguages));
        var supported = supportedLanguages.Distinct().ToArray();
        if (!supported.Contains(defaultLanguage))
        {
            throw new ArgumentException(
                $"'{defaultLanguage}' is not one of the supported languages ({string.Join(", ", supported.Select(t => t.Value))}).", nameof(defaultLanguage));
        }
        DefaultLanguage = defaultLanguage;
        SupportedLanguages = supported;
        return this;
    }

    /// <summary>Whether the package <paramref name="id"/> is enabled.</summary>
    /// <param name="id">The package id (by convention the assembly name).</param>
    public bool IsEnabled(string id) => _byId.ContainsKey(id) || _running?.Contains(id) == true;

    /// <summary>
    /// Enables a package. Idempotent: a package that is already enabled is not defined again, so every package can
    /// enable its dependencies itself. A dependency cycle throws.
    /// </summary>
    /// <param name="id">The package id (by convention the assembly name).</param>
    /// <param name="define">Declares the package: dependencies, generated assets, startup callbacks.</param>
    public ArcBuilder UsePackage(string id, Action<ArcPackageBuilder> define)
    {
        if (IsEnabled(id))
        {
            return this;
        }
        var cycleStart = _defining.IndexOf(id);
        if (cycleStart >= 0)
        {
            throw new InvalidOperationException(
                $"Arc packages depend on each other in a cycle: {string.Join(" → ", _defining.Skip(cycleStart))} → {id}.");
        }
        _defining.Add(id);
        var package = new ArcPackageBuilder(this, id);
        try
        {
            define(package);
        }
        finally
        {
            _defining.RemoveAt(_defining.Count - 1);
        }
        foreach (var preset in package.Presets)
        {
            if (_presets.TryGetValue(preset.Name, out var other) && !ReferenceEquals(other, preset))
            {
                throw new InvalidOperationException(
                    $"Theme preset '{preset.Name}' is registered by both '{PresetOwner(preset.Name)}' and '{id}'.");
            }
            _presets[preset.Name] = preset;
        }
        // Dependencies enabled by DependsOn were appended while define ran, so this order is a topological one.
        _byId.Add(id, package);
        _packages.Add(package);
        return this;
    }

    private string PresetOwner(string name)
        => _packages.FirstOrDefault(p => p.Presets.Any(preset => preset.Name == name))?.Id ?? (_running is null ? "?" : "an enabled package");

    /// <summary>Configures the initial theme; several calls are applied in order.</summary>
    /// <param name="configure">Changes to the default state.</param>
    public ArcBuilder WithTheme(Action<ArcThemeBuilder> configure)
    {
        ThrowIfRunning(nameof(WithTheme));
        _theme.Add(configure);
        return this;
    }

    /// <summary>Chooses how theme files are registered (lazy by default).</summary>
    /// <param name="loading">Lazy or eager.</param>
    public ArcBuilder WithThemeLoading(ArcThemeLoading loading)
    {
        ThrowIfRunning(nameof(WithThemeLoading));
        ThemeLoading = loading;
        return this;
    }

    private void ThrowIfRunning(string method)
    {
        if (_running is not null)
        {
            throw new InvalidOperationException(
                $"{method} configures startup; a running application only enables packages in UsePackages (change the theme with ArcTheme.Apply and the language with ArcLanguage.Set).");
        }
    }
}

/// <summary>Declares one package for <see cref="ArcBuilder.UsePackage"/>.</summary>
public sealed class ArcPackageBuilder
{
    private readonly ArcBuilder _owner;
    private readonly List<ArcTokenDescriptor> _tokens = new();
    private readonly List<ArcThemeAsset> _themes = new();
    private readonly List<ArcLanguageCatalog> _languages = new();
    private readonly List<ArcThemePreset> _presets = new();
    private readonly List<Action<ArcPackageContext>> _onCommit = new();
    private readonly List<Action<ArcPackageContext>> _onFirstUse = new();

    internal ArcPackageBuilder(ArcBuilder owner, string id)
    {
        _owner = owner;
        Id = id;
    }

    /// <summary>The package id.</summary>
    public string Id { get; }

    internal IReadOnlyList<ArcTokenDescriptor> Tokens => _tokens;
    internal IReadOnlyList<ArcThemeAsset> Themes => _themes;
    internal IReadOnlyList<ArcLanguageCatalog> Languages => _languages;
    internal IReadOnlyList<ArcThemePreset> Presets => _presets;
    internal IReadOnlyList<Action<ArcPackageContext>> CommitCallbacks => _onCommit;
    internal IReadOnlyList<Action<ArcPackageContext>> FirstUseCallbacks => _onFirstUse;

    /// <summary>Enables the packages this one needs (they are committed first).</summary>
    /// <param name="use">Enables the dependencies, for example <c>b => b.UseArcCore()</c>.</param>
    public ArcPackageBuilder DependsOn(Action<ArcBuilder> use)
    {
        use(_owner);
        return this;
    }

    /// <summary>Adds token identities (the generated <c>ArcGeneratedAssets.Tokens</c>).</summary>
    /// <param name="tokens">The descriptors.</param>
    public ArcPackageBuilder AddTokens(IEnumerable<ArcTokenDescriptor> tokens)
    {
        _tokens.AddRange(tokens);
        return this;
    }

    /// <summary>Adds control-theme files (the generated <c>ArcGeneratedAssets.Themes</c>).</summary>
    /// <param name="themes">The theme files.</param>
    public ArcPackageBuilder AddThemes(IEnumerable<ArcThemeAsset> themes)
    {
        _themes.AddRange(themes);
        return this;
    }

    /// <summary>Adds language catalogs (the generated <c>ArcGeneratedAssets.Languages</c>).</summary>
    /// <param name="catalogs">The catalogs.</param>
    public ArcPackageBuilder AddLanguages(IEnumerable<ArcLanguageCatalog> catalogs)
    {
        _languages.AddRange(catalogs);
        return this;
    }

    /// <summary>
    /// Registers theme presets the package ships (for example the Pixel style), so apps and tools can list them through
    /// <see cref="ArcRuntime.Presets"/>. Registering does not apply a preset: the app adds it with
    /// <c>WithTheme(t => t.Preset(preset))</c> or <c>ArcTheme.Apply(t => t.Preset(preset))</c>. Names must be unique.
    /// </summary>
    /// <param name="presets">The presets.</param>
    public ArcPackageBuilder AddPresets(IEnumerable<ArcThemePreset> presets)
    {
        _presets.AddRange(presets);
        return this;
    }

    /// <summary>
    /// Runs once at startup on the UI thread, after the application initialized and the theme was published. Keep it
    /// light (register fonts, services); give disposable objects to <see cref="ArcPackageContext.Own{T}"/>.
    /// </summary>
    /// <param name="callback">The callback.</param>
    public ArcPackageBuilder OnCommit(Action<ArcPackageContext> callback)
    {
        _onCommit.Add(callback);
        return this;
    }

    /// <summary>
    /// Runs once, the first time one of the package's theme files is loaded (a control of the package is about to be
    /// styled). In eager mode it runs at startup.
    /// </summary>
    /// <param name="callback">The callback.</param>
    public ArcPackageBuilder OnFirstUse(Action<ArcPackageContext> callback)
    {
        _onFirstUse.Add(callback);
        return this;
    }
}

/// <summary>What a package callback can reach.</summary>
public sealed class ArcPackageContext
{
    private readonly ArcRuntime _runtime;

    internal ArcPackageContext(ArcRuntime runtime, string packageId)
    {
        _runtime = runtime;
        PackageId = packageId;
    }

    /// <summary>The package id.</summary>
    public string PackageId { get; }

    /// <summary>The application.</summary>
    public Application Application => _runtime.Application;

    /// <summary>The theme manager.</summary>
    public ArcThemeManager Theme => _runtime.Theme;

    /// <summary>Disposes <paramref name="disposable"/> when the application shuts down.</summary>
    /// <param name="disposable">The object to dispose.</param>
    public T Own<T>(T disposable) where T : IDisposable
    {
        _runtime.Own(disposable);
        return disposable;
    }
}
