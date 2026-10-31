using System.Runtime.CompilerServices;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Threading;
using TKCode.Arc.Localization;

namespace TKCode.Arc;

/// <summary>Arc's state in one application: packages, theme manager and theme provider.</summary>
public sealed class ArcRuntime
{
    private static readonly ConditionalWeakTable<Application, ArcRuntime> s_runtimes = new();
    private readonly List<IDisposable> _owned = new();
    private readonly List<string> _packages;
    private readonly Dictionary<string, ArcThemePreset> _presets;
    private readonly Dictionary<string, string> _tokenOwners;
    private readonly Dictionary<string, string> _catalogOwners;
    private readonly ArcThemeLoading _themeLoading;

    private ArcRuntime(Application application, ArcThemeManager theme, ArcThemeProvider themes, ArcLanguageManager languages,
                       ArcBuilder builder, Dictionary<string, string> tokenOwners, Dictionary<string, string> catalogOwners)
    {
        Application = application;
        Theme = theme;
        Themes = themes;
        Languages = languages;
        _packages = builder.Packages.Select(p => p.Id).ToList();
        _presets = new Dictionary<string, ArcThemePreset>(builder.Presets, StringComparer.Ordinal);
        _tokenOwners = tokenOwners;
        _catalogOwners = catalogOwners;
        _themeLoading = builder.ThemeLoading;
    }

    /// <summary>The application.</summary>
    public Application Application { get; }

    /// <summary>The theme manager.</summary>
    public ArcThemeManager Theme { get; }

    /// <summary>The control-theme provider (lazy loading).</summary>
    public ArcThemeProvider Themes { get; }

    /// <summary>The language manager: current language and the text of the enabled packages' catalogs.</summary>
    public ArcLanguageManager Languages { get; }

    /// <summary>The enabled packages, dependencies first; packages enabled by <see cref="UsePackages"/> are appended.</summary>
    public IReadOnlyList<string> Packages => _packages;

    /// <summary>The theme presets the enabled packages registered, by name.</summary>
    public IReadOnlyDictionary<string, ArcThemePreset> Presets => _presets;

    /// <summary>The runtime of <see cref="Application.Current"/>, or null before <c>UseArc(...)</c> set it up.</summary>
    public static ArcRuntime? Current => Application.Current is { } application ? Find(application) : null;

    /// <summary>The runtime of <paramref name="application"/>, or null.</summary>
    /// <param name="application">The application.</param>
    public static ArcRuntime? Find(Application application)
        => s_runtimes.TryGetValue(application, out var runtime) ? runtime : null;

    /// <summary>
    /// Sets Arc up in <paramref name="application"/>: registers token identities and theme files, publishes the initial
    /// theme and runs the packages' commit callbacks. <c>UseArc(...)</c> calls it after the application initialized;
    /// call it directly only for an application that was not built with <see cref="AppBuilder"/>.
    /// </summary>
    /// <param name="application">The application.</param>
    /// <param name="builder">The configured builder.</param>
    public static ArcRuntime Install(Application application, ArcBuilder builder)
    {
        if (Find(application) is not null)
        {
            throw new InvalidOperationException("Arc is already set up for this application.");
        }

        var tokenOwners = new Dictionary<string, string>(StringComparer.Ordinal);
        var descriptors = CollectTokens(builder.Packages, tokenOwners, allowGlobal: true);
        var catalogOwners = new Dictionary<string, string>(StringComparer.Ordinal);
        var catalogs = CollectCatalogs(builder.Packages, catalogOwners, builder.SupportedLanguages);
        var languages = new ArcLanguageManager(builder.DefaultLanguage, builder.SupportedLanguages, catalogs);
        var theme = new ArcThemeManager(application, descriptors);
        var themes = new ArcThemeProvider();
        var runtime = new ArcRuntime(application, theme, themes, languages, builder, tokenOwners, catalogOwners);
        s_runtimes.AddOrUpdate(application, runtime);

        foreach (var package in builder.Packages)
        {
            var onFirstUse = package.FirstUseCallbacks.Count == 0 ? null : runtime.FirstUse(package);
            foreach (var asset in package.Themes)
            {
                themes.Add(asset, onFirstUse);
            }
        }

        var initial = ArcThemeState.Default;
        foreach (var configure in builder.ThemeConfigurations)
        {
            initial = initial.With(configure);
        }
        theme.Apply(initial);

        // Tokens before themes in lookup order does not matter (their keys are disjoint); the application's own
        // resources stay above both.
        application.Resources.MergedDictionaries.Add(theme.Resources);
        application.Resources.MergedDictionaries.Add(themes);
        application.Resources.MergedDictionaries.Add(languages.Resources);

        if (builder.ThemeLoading == ArcThemeLoading.Eager)
        {
            themes.LoadAll();
        }

        foreach (var package in builder.Packages)
        {
            var context = new ArcPackageContext(runtime, package.Id);
            foreach (var callback in package.CommitCallbacks)
            {
                callback(context);
            }
        }

        if (application.ApplicationLifetime is IControlledApplicationLifetime lifetime)
        {
            lifetime.Exit += (_, _) => runtime.Shutdown();
        }
        return runtime;
    }

    /// <summary>
    /// Enables packages in the running application, typically the package of an assembly that was loaded on demand (a
    /// browser app that downloads part of itself when a page needs it). Packages that are already enabled are skipped, as
    /// they are by <c>UsePackage</c>. The new ones are set up as at startup: their token identities, theme files, language
    /// catalogs and presets are registered, the applied theme is published again with their tokens (the state stays, so
    /// <see cref="ArcThemeManager.StateChanged"/> is not raised), and their commit callbacks run. Call it on the UI thread.
    /// </summary>
    /// <remarks>
    /// Everything is checked before anything is registered: a token identity, theme key or language catalog another
    /// package already registered, a catalog that does not cover every language of the application, or a global token
    /// identity throws <see cref="InvalidOperationException"/>; a preset value the new tokens reject throws
    /// <see cref="ArcThemeException"/>. Either way the application stays as it was. <paramref name="use"/> only enables
    /// packages: <c>UseLanguages</c>, <c>WithTheme</c> and <c>WithThemeLoading</c> throw there.
    /// </remarks>
    /// <param name="use">Enables the packages, for example <c>arc => arc.UseArcBlocks()</c>.</param>
    /// <returns>The ids of the packages this call enabled, dependencies first; empty when all of them were enabled already.</returns>
    public IReadOnlyList<string> UsePackages(Action<ArcBuilder> use)
    {
        Dispatcher.UIThread.VerifyAccess();
        var builder = new ArcBuilder(this);
        use(builder);
        var added = builder.Packages;
        if (added.Count == 0)
        {
            return Array.Empty<string>();
        }

        var tokenOwners = new Dictionary<string, string>(_tokenOwners, StringComparer.Ordinal);
        var descriptors = CollectTokens(added, tokenOwners, allowGlobal: false);
        var catalogOwners = new Dictionary<string, string>(_catalogOwners, StringComparer.Ordinal);
        var catalogs = CollectCatalogs(added, catalogOwners, Languages.SupportedLanguages);
        var keys = new Dictionary<object, string>();
        foreach (var package in added)
        {
            foreach (var asset in package.Themes)
            {
                foreach (var key in asset.Keys)
                {
                    if ((Themes.OwnerOf(key) ?? (keys.TryGetValue(key, out var earlier) ? earlier : null)) is { } other)
                    {
                        throw new InvalidOperationException($"Theme key '{key}' is defined by both '{other}' and '{asset.Id}'.");
                    }
                    keys.Add(key, asset.Id);
                }
            }
        }

        // Tokens first: computing them is the one step that can still fail, and it changes nothing when it does.
        Theme.Add(descriptors);
        foreach (var package in added)
        {
            var onFirstUse = package.FirstUseCallbacks.Count == 0 ? null : FirstUse(package);
            foreach (var asset in package.Themes)
            {
                Themes.Add(asset, onFirstUse);
            }
            foreach (var preset in package.Presets)
            {
                _presets[preset.Name] = preset;
            }
            _packages.Add(package.Id);
        }
        if (catalogs.Count > 0)
        {
            Languages.Add(catalogs);
        }
        foreach (var (identity, owner) in tokenOwners)
        {
            _tokenOwners[identity] = owner;
        }
        foreach (var (id, owner) in catalogOwners)
        {
            _catalogOwners[id] = owner;
        }
        if (_themeLoading == ArcThemeLoading.Eager)
        {
            Themes.LoadAll();
        }
        foreach (var package in added)
        {
            var context = new ArcPackageContext(this, package.Id);
            foreach (var callback in package.CommitCallbacks)
            {
                callback(context);
            }
        }
        return added.Select(p => p.Id).ToArray();
    }

    internal void Own(IDisposable disposable) => _owned.Add(disposable);

    /// <summary>
    /// The token identities of <paramref name="packages"/>, the global one first, checked against each other and against
    /// <paramref name="owners"/> (identity → package), which receives them.
    /// </summary>
    private static List<ArcTokenDescriptor> CollectTokens(IReadOnlyList<ArcPackageBuilder> packages, Dictionary<string, string> owners,
                                                          bool allowGlobal)
    {
        var descriptors = new List<ArcTokenDescriptor>();
        ArcTokenDescriptor? global = null;
        foreach (var package in packages)
        {
            foreach (var descriptor in package.Tokens)
            {
                if (!owners.TryAdd(descriptor.Identity, package.Id))
                {
                    throw new InvalidOperationException(
                        $"Token identity '{descriptor.Identity}' is registered by both '{owners[descriptor.Identity]}' and '{package.Id}'.");
                }
                if (descriptor.IsGlobal)
                {
                    if (!allowGlobal)
                    {
                        throw new InvalidOperationException(
                            $"Package '{package.Id}' registers the global token identity '{descriptor.Identity}'; that is only possible at startup.");
                    }
                    if (global is not null)
                    {
                        throw new InvalidOperationException(
                            $"Only one global token identity is allowed: '{global.Identity}' and '{descriptor.Identity}'.");
                    }
                    global = descriptor;
                    descriptors.Insert(0, descriptor);
                }
                else
                {
                    descriptors.Add(descriptor);
                }
            }
        }
        return descriptors;
    }

    /// <summary>
    /// The language catalogs of <paramref name="packages"/>, checked against <paramref name="owners"/> (catalog id →
    /// package, which receives them) and, before anything is installed, for covering every language the application offers.
    /// </summary>
    private static List<ArcLanguageCatalog> CollectCatalogs(IReadOnlyList<ArcPackageBuilder> packages, Dictionary<string, string> owners,
                                                            IReadOnlyList<LanguageTag> supported)
    {
        var catalogs = new List<(string Package, ArcLanguageCatalog Catalog)>();
        foreach (var package in packages)
        {
            foreach (var catalog in package.Languages)
            {
                if (!owners.TryAdd(catalog.Id, package.Id))
                {
                    throw new InvalidOperationException(
                        $"Language catalog '{catalog.Id}' is registered by both '{owners[catalog.Id]}' and '{package.Id}'.");
                }
                catalogs.Add((package.Id, catalog));
            }
        }
        var gaps = ArcLanguageManager.FindCoverageGaps(supported, catalogs);
        if (gaps.Count > 0)
        {
            throw new InvalidOperationException(
                "The application's languages are not covered by every enabled package:\n- " + string.Join("\n- ", gaps));
        }
        return catalogs.Select(c => c.Catalog).ToList();
    }

    private Action FirstUse(ArcPackageBuilder package)
    {
        var done = false;
        return () =>
        {
            if (done)
            {
                return;
            }
            done = true;
            var context = new ArcPackageContext(this, package.Id);
            foreach (var callback in package.FirstUseCallbacks)
            {
                callback(context);
            }
        };
    }

    private void Shutdown()
    {
        Theme.Detach();
        for (var i = _owned.Count - 1; i >= 0; i--)
        {
            _owned[i].Dispose();
        }
        _owned.Clear();
    }
}

/// <summary>The Arc entry point on <see cref="AppBuilder"/>.</summary>
public static class ArcAppBuilderExtensions
{
    /// <summary>
    /// Enables Arc: packages are declared in <paramref name="configure"/> (<c>arc => arc.UseArcBlocks()</c>) and set up
    /// after the application initialized. No <c>partial Application</c> hook is needed.
    /// </summary>
    /// <param name="builder">The app builder.</param>
    /// <param name="configure">Enables packages and sets the initial theme.</param>
    public static AppBuilder UseArc(this AppBuilder builder, Action<ArcBuilder>? configure = null)
    {
        var arc = new ArcBuilder();
        configure?.Invoke(arc);
        return builder.AfterSetup(b =>
        {
            var application = b.Instance ?? throw new InvalidOperationException("The application was not created.");
            ArcRuntime.Install(application, arc);
        });
    }
}
