using System.Globalization;
using System.Text;

namespace TKCode.Arc;

/// <summary>
/// Text helpers shared by the Developer blocks: counts with a singular form ("1 event" / "3 events", unlike Arc's
/// "1 events"), formatting that never throws on a malformed user format, English name lists, masks and thousands.
/// </summary>
internal static class DeveloperText
{
    /// <summary>The bullet used by masks ("•", U+2022).</summary>
    public const char Bullet = '•';

    /// <summary>
    /// Formats <paramref name="count"/> with <paramref name="one"/> when it is 1, else with <paramref name="other"/>
    /// (both may use {0}); a malformed format is returned as it is.
    /// </summary>
    public static string Count(long count, string one, string other) => Format(count == 1 ? one : other, count);

    /// <summary>Formats with the current culture, returning <paramref name="format"/> itself when it is malformed or null-safe.</summary>
    public static string Format(string? format, params object?[] args)
    {
        if (string.IsNullOrEmpty(format))
        {
            return string.Empty;
        }
        try
        {
            return string.Format(CultureInfo.CurrentCulture, format, args);
        }
        catch (FormatException)
        {
            return format;
        }
    }

    /// <summary>"A", "A and B", "A, B and C" (English list style; <paramref name="andWord"/> is localizable). Blank names are skipped.</summary>
    public static string JoinNames(IEnumerable<string?> names, string andWord = "and")
    {
        var list = new List<string>();
        foreach (var name in names)
        {
            if (!string.IsNullOrWhiteSpace(name))
            {
                list.Add(name);
            }
        }
        return list.Count switch
        {
            0 => string.Empty,
            1 => list[0],
            _ => string.Join(", ", list.Take(list.Count - 1)) + " " + andWord + " " + list[^1],
        };
    }

    /// <summary>
    /// A masked secret hint: <paramref name="prefix"/> + <paramref name="dots"/> bullets + <paramref name="lastFour"/>
    /// ("rk_live_••••7c3e"). Only the parts the caller passes are shown; the secret itself is never needed.
    /// </summary>
    public static string Mask(string? prefix, string? lastFour, int dots = 4)
    {
        var builder = new StringBuilder();
        builder.Append(prefix);
        builder.Append(Bullet, Math.Max(0, dots));
        builder.Append(lastFour);
        return builder.ToString();
    }

    /// <summary>A run of <paramref name="count"/> bullets (Env variables masks a value with 16, whatever its length).</summary>
    public static string Bullets(int count) => new(Bullet, Math.Max(0, count));

    /// <summary>A whole number with the culture's group separators ("942,317").</summary>
    public static string Thousands(long value) => value.ToString("N0", CultureInfo.CurrentCulture);
}
