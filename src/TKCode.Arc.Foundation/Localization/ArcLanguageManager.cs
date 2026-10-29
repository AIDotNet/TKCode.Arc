using System.Globalization;
using Avalonia.Controls;

namespace TKCode.Arc.Localization;

/// <summary>Arguments of <see cref="ArcLanguageManager.Changed"/>.</summary>
public sealed class LanguageChangedEventArgs : EventArgs
{
    /// <summary>Creates the arguments.</summary>
    /// <param name="oldLanguage">The language before the change.</param>
    /// <param name="newLanguage">The language after the change.</param>
    public LanguageChangedEventArgs(LanguageTag oldLanguage, LanguageTag newLanguage)
    {
        OldLanguage = oldLanguage;
        NewLanguage = newLanguage;
    }

    /// <summary>The language before the change.</summary>
    public LanguageTag OldLanguage { get; }

    /// <summary>The language after the change.</summary>
    public LanguageTag NewLanguage { get; }
}

/// <summary>
/// The language of one application: which languages it offers, the current one, and the text of the enabled packages'
/// catalogs in it. The text is published in <see cref="Resources"/> (merged into the application's resources) under the
/// boxed catalog enum members, so <c>{arc:XxxLangResource Member}</c> and <c>GetResourceObservable(kind)</c> follow a
/// language change by themselves; C# reads it with the generated <c>XxxLangResources.Get(kind)</c>.
/// </summary>
public sealed class ArcLanguageManager
{
    private readonly Dictionary<ArcLanguageCatalog, string[]> _resolved = new();
    private readonly Dictionary<object, object?> _values = new();

    internal ArcLanguageManager(LanguageTag defaultLanguage, IReadOnlyList<LanguageTag> supportedLanguages,
                                IReadOnlyList<ArcLanguageCatalog> catalogs)
    {
        DefaultLanguage = defaultLanguage;
        SupportedLanguages = supportedLanguages;
        Catalogs = catalogs;
        Language = defaultLanguage;
        Culture = defaultLanguage.ToCulture();
        Publish();
    }

    /// <summary>The manager of <see cref="Avalonia.Application.Current"/>, or null before <c>UseArc(...)</c> set it up.</summary>
    public static ArcLanguageManager? Current => ArcRuntime.Current?.Languages;

    /// <summary>The language the application starts in and the first fallback for missing text.</summary>
    public LanguageTag DefaultLanguage { get; }

    /// <summary>The languages the application offers (<c>UseLanguages</c>); every enabled catalog covers each of them.</summary>
    public IReadOnlyList<LanguageTag> SupportedLanguages { get; }

    /// <summary>The catalogs of the enabled packages, dependencies first.</summary>
    public IReadOnlyList<ArcLanguageCatalog> Catalogs { get; }

    /// <summary>The current language.</summary>
    public LanguageTag Language { get; private set; }

    /// <summary>The culture of the current language, for formatting numbers and dates.</summary>
    public CultureInfo Culture { get; private set; }

    /// <summary>The text of every enabled catalog in the current language, keyed by the boxed enum members.</summary>
    public ResourceDictionary Resources { get; } = new();

    /// <summary>Raised on the UI thread after the text of a new language was published.</summary>
    public event EventHandler<LanguageChangedEventArgs>? Changed;

    /// <summary>
    /// Switches to <paramref name="language"/>: publishes the text that differs in one resource change, then raises
    /// <see cref="Changed"/>. Setting the current language does nothing. Call it on the UI thread.
    /// </summary>
    /// <param name="language">One of <see cref="SupportedLanguages"/>.</param>
    /// <exception cref="ArgumentException">The language is not one of <see cref="SupportedLanguages"/>.</exception>
    public void Set(LanguageTag language)
    {
        if (!SupportedLanguages.Contains(language))
        {
            throw new ArgumentException(
                $"'{language}' is not one of the application's languages ({string.Join(", ", SupportedLanguages.Select(t => t.Value))}); add it to UseLanguages.",
                nameof(language));
        }
        if (language == Language)
        {
            return;
        }
        var old = Language;
        Language = language;
        Culture = language.ToCulture();
        _resolved.Clear();
        Publish();
        var args = new LanguageChangedEventArgs(old, language);
        Changed?.Invoke(this, args);
        ArcLanguage.RaiseChanged(this, args);
    }

    /// <summary>The text of entry <paramref name="index"/> of <paramref name="catalog"/> in the current language.</summary>
    /// <param name="catalog">The catalog; one that no enabled package registered is resolved the same way.</param>
    /// <param name="index">The member's declaration index.</param>
    public string Get(ArcLanguageCatalog catalog, int index)
    {
        if (!_resolved.TryGetValue(catalog, out var texts))
        {
            texts = Resolve(catalog);
            _resolved.Add(catalog, texts);
        }
        return texts[index];
    }

    /// <summary>
    /// What is missing for an application that offers <paramref name="supported"/> with <paramref name="catalogs"/>
    /// enabled: one message per catalog and language that lacks a table or some entries. A language is covered by its
    /// own table and its parents' (<c>zh-Hant</c> covers <c>zh-Hant-TW</c>), never by the fallback languages.
    /// </summary>
    /// <param name="supported">The application's languages.</param>
    /// <param name="catalogs">The enabled catalogs with the package that registered each.</param>
    public static IReadOnlyList<string> FindCoverageGaps(IReadOnlyList<LanguageTag> supported,
                                                         IEnumerable<(string Package, ArcLanguageCatalog Catalog)> catalogs)
    {
        var gaps = new List<string>();
        foreach (var (package, catalog) in catalogs)
        {
            foreach (var language in supported)
            {
                var hasTable = false;
                for (var tag = language; tag is not null && !hasTable; tag = tag.Parent)
                {
                    hasTable = catalog.FindTable(tag) is not null;
                }
                if (!hasTable)
                {
                    gaps.Add($"Package '{package}' has no '{language}' text for '{catalog.Id}' (add Localization/{language}.xlf).");
                    continue;
                }
                var missing = new List<string>();
                for (var i = 0; i < catalog.Keys.Count; i++)
                {
                    if (catalog.Lookup(i, language) is null)
                    {
                        missing.Add(catalog.Names[i]);
                    }
                }
                if (missing.Count > 0)
                {
                    gaps.Add($"Package '{package}' is missing {missing.Count} '{language}' entries of '{catalog.Id}': {string.Join(", ", missing)}.");
                }
            }
        }
        return gaps;
    }

    private string[] Resolve(ArcLanguageCatalog catalog)
    {
        var texts = new string[catalog.Keys.Count];
        for (var i = 0; i < texts.Length; i++)
        {
            texts[i] = catalog.Resolve(i, Language, DefaultLanguage);
        }
        return texts;
    }

    private void Publish()
    {
        _values.Clear();
        foreach (var catalog in Catalogs)
        {
            var texts = Resolve(catalog);
            _resolved[catalog] = texts;
            for (var i = 0; i < texts.Length; i++)
            {
                _values[catalog.Keys[i]] = texts[i];
            }
        }
        // Only the entries whose text differs are rewritten, in one SetItems: one ResourcesChanged for the whole tree,
        // and bindings to text that two languages share do not change.
        ArcTokenCalculator.Publish(Resources, _values);
    }
}

/// <summary>
/// The language of the current application: <c>ArcLanguage.Set(LanguageTags.ZhCN)</c>. Before <c>UseArc(...)</c> set
/// the application up (design time, plain unit tests) text comes from the catalogs' source language.
/// </summary>
public static class ArcLanguage
{
    /// <summary>The current application's language manager, or null.</summary>
    public static ArcLanguageManager? Manager => ArcLanguageManager.Current;

    /// <summary>The current language (en-US before the application is set up).</summary>
    public static LanguageTag Current => Manager?.Language ?? LanguageTags.EnUS;

    /// <summary>The culture of the current language (the current culture before the application is set up).</summary>
    public static CultureInfo Culture => Manager?.Culture ?? CultureInfo.CurrentCulture;

    /// <summary>Raised on the UI thread after any application's language changed (the sender is its manager).</summary>
    public static event EventHandler<LanguageChangedEventArgs>? Changed;

    /// <summary>Switches the current application to <paramref name="language"/>.</summary>
    /// <param name="language">One of the application's languages.</param>
    /// <exception cref="InvalidOperationException">Arc is not set up in the current application.</exception>
    public static void Set(LanguageTag language)
        => (Manager ?? throw new InvalidOperationException("Arc is not set up in the current application (UseArc).")).Set(language);

    /// <summary>Switches the current application to the language <paramref name="tag"/>.</summary>
    /// <param name="tag">A language tag, for example <c>zh-CN</c>.</param>
    public static void Set(string tag) => Set(LanguageTag.Parse(tag));

    /// <summary>The text of entry <paramref name="index"/> of <paramref name="catalog"/> in the current language.</summary>
    /// <param name="catalog">The catalog.</param>
    /// <param name="index">The member's declaration index.</param>
    public static string Get(ArcLanguageCatalog catalog, int index)
        => Manager is { } manager ? manager.Get(catalog, index) : catalog.SourceText(index);

    /// <summary>The text of an entry with its <c>{0}</c> placeholders filled, formatted in <see cref="Culture"/>.</summary>
    /// <param name="catalog">The catalog.</param>
    /// <param name="index">The member's declaration index.</param>
    /// <param name="arguments">The placeholder values.</param>
    public static string Format(ArcLanguageCatalog catalog, int index, object?[] arguments)
        => string.Format(Culture, Get(catalog, index), arguments);

    internal static void RaiseChanged(ArcLanguageManager sender, LanguageChangedEventArgs args) => Changed?.Invoke(sender, args);
}
