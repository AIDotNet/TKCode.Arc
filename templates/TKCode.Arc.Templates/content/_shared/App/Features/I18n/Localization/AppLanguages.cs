using TKCode.Arc.Localization;
#if (UsesSettings)
using ArcStarterApp.Settings;
#endif

namespace ArcStarterApp.Localization;

/// <summary>
/// The languages the app offers. Every package the app uses must have text for each of them (Arc's packages have en-US,
/// zh-CN and zh-TW); to add one, add a Localization/&lt;tag&gt;.xlf file with every unit translated and list it here.
/// </summary>
public static class AppLanguages
{
    /// <summary>The languages, with the name each shows in its own language.</summary>
    public static IReadOnlyList<(LanguageTag Tag, string Name)> All { get; } =
    [
        (LanguageTags.EnUS, "English"),
        (LanguageTags.ZhCN, "简体中文"),
        (LanguageTags.ZhTW, "繁體中文"),
    ];

    /// <summary>The tags of <see cref="All"/>, for UseLanguages.</summary>
    public static IEnumerable<LanguageTag> Supported => All.Select(language => language.Tag);

#if (UsesSettings)
    /// <summary>
    /// The language the app starts in: the one chosen last time (Settings/AppSettings.cs), otherwise the one the app was
    /// created with.
    /// </summary>
#else
    /// <summary>The language the app starts in: the one the app was created with.</summary>
#endif
    public static LanguageTag Initial
    {
        get
        {
#if (UsesSettings)
            if (LanguageTag.TryParse(AppSettings.Current.Language, out var saved) && Supported.Contains(saved))
            {
                return saved;
            }
#endif
            return LanguageTag.Parse("ArcStartLanguage");
        }
    }
}
