using System.Runtime.CompilerServices;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using TKCode.Arc.Localization;

namespace TKCode.Arc;

/// <summary>Arc's state in one application: packages, theme manager and theme provider.</summary>
public sealed class ArcRuntime
{
    private static readonly ConditionalWeakTable<Application, ArcRuntime> s_runtimes = new();
    private readonly List<IDisposable> _owned = new();

    private ArcRuntime(Application application, ArcThemeManager theme, ArcThemeProvider themes, ArcLanguageManager languages,
                       IReadOnlyList<string> packages, IReadOnlyDictionary<string, ArcThemePreset> presets)
    {
        Application = application;
        Theme = theme;
        Themes = themes;
        Languages = languages;
        Packages = packages;
        Presets = presets;
    }

    /// <summary>The application.</summary>
    public Application Application { get; }

    /// <summary>The theme manager.</summary>
    public ArcThemeManager Theme { get; }

    /// <summary>The control-theme provider (lazy loading).</summary>
    public ArcThemeProvider Themes { get; }

    /// <summary>The language manager: current language and the text of the enabled packages' catalogs.</summary>
    public ArcLanguageManager Languages { get; }

    /// <summary>The enabled packages, dependencies first.</summary>
    public IReadOnlyList<string> Packages { get; }

    /// <summary>The theme presets the enabled packages registered, by name.</summary>
    public IReadOnlyDictionary<string, ArcThemePreset> Presets { get; }

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

        var descriptors = new List<ArcTokenDescriptor>();
        var identities = new Dictionary<string, string>(StringComparer.Ordinal);
        ArcTokenDescriptor? global = null;
        foreach (var package in builder.Packages)
        {
            foreach (var descriptor in package.Tokens)
            {
                if (!identities.TryAdd(descriptor.Identity, package.Id))
                {
                    throw new InvalidOperationException(
                        $"Token identity '{descriptor.Identity}' is registered by both '{identities[descriptor.Identity]}' and '{package.Id}'.");
                }
                if (descriptor.IsGlobal)
                {
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

        var languages = CreateLanguages(builder);
        var theme = new ArcThemeManager(application, descriptors);
        var themes = new ArcThemeProvider();
        var runtime = new ArcRuntime(application, theme, themes, languages, builder.Packages.Select(p => p.Id).ToArray(),
            new Dictionary<string, ArcThemePreset>(builder.Presets, StringComparer.Ordinal));
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

    internal void Own(IDisposable disposable) => _owned.Add(disposable);

    /// <summary>
    /// Collects the enabled packages' language catalogs and checks, before anything is installed, that each covers
    /// every language the application offers.
    /// </summary>
    private static ArcLanguageManager CreateLanguages(ArcBuilder builder)
    {
        var catalogs = new List<(string Package, ArcLanguageCatalog Catalog)>();
        var owners = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var package in builder.Packages)
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
        var gaps = ArcLanguageManager.FindCoverageGaps(builder.SupportedLanguages, catalogs);
        if (gaps.Count > 0)
        {
            throw new InvalidOperationException(
                "The application's languages are not covered by every enabled package:\n- " + string.Join("\n- ", gaps));
        }
        return new ArcLanguageManager(builder.DefaultLanguage, builder.SupportedLanguages, catalogs.Select(c => c.Catalog).ToArray());
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
