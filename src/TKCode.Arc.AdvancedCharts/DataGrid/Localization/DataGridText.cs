using System.Globalization;
using TKCode.Arc.Localization;

namespace TKCode.Arc;

/// <summary>The grid's built-in text in the current Arc language (<c>Localization/*.xlf</c>).</summary>
internal static class DataGridText
{
    public static ArcLanguageManager? Languages => ArcLanguageManager.Current;

    /// <summary>The culture of the current Arc language (the current culture before the application starts).</summary>
    public static CultureInfo FormattingCulture => ArcLanguage.Culture;

    public static string Get(DataGridLangResourceKind kind) => DataGridLangResources.Get(kind);

    public static string Format(DataGridLangResourceKind kind, params object?[] arguments) =>
        DataGridLangResources.Format(kind, arguments);

    /// <summary>Picks the singular or plural entry for <paramref name="count"/> and formats it.</summary>
    public static string Count(DataGridLangResourceKind one, DataGridLangResourceKind other, int count, CultureInfo culture) =>
        Format(count == 1 ? one : other, count.ToString("N0", culture));

    /// <summary>Splits a <c>{0}</c> pattern into the text before and after the number (for rolling counters).</summary>
    public static (string Prefix, string Suffix) Around(DataGridLangResourceKind kind)
    {
        var pattern = Get(kind);
        var at = pattern.IndexOf("{0}", StringComparison.Ordinal);
        return at < 0 ? (string.Empty, pattern) : (pattern[..at], pattern[(at + 3)..]);
    }
}
