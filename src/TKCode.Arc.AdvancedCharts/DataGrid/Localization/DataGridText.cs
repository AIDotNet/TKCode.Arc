using System.Globalization;
using AtomUI;
using AtomUI.Localization;
using Avalonia;
using TKCode.Arc.Localization;

namespace TKCode.Arc;

/// <summary>The grid's built-in text in the current AtomUI language (<c>Localization/*.xlf</c>).</summary>
internal static class DataGridText
{
    public static ILanguageManager? Languages => Application.Current?.GetLanguageManager();

    /// <summary>The AtomUI formatting culture, or the current culture before the application starts.</summary>
    public static CultureInfo FormattingCulture => Languages?.Current.FormattingCulture ?? CultureInfo.CurrentCulture;

    public static string Get(DataGridLangResourceKind kind) =>
        Application.Current?.GetLocalizer()?.Get(kind) ?? kind.ToString();

    public static string Format(DataGridLangResourceKind kind, params object?[] arguments)
    {
        if (Application.Current?.GetLocalizer() is { } localizer)
        {
            return localizer.Format(kind, arguments);
        }
        return kind + " " + string.Join(' ', arguments);
    }

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
