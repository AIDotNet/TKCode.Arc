using System.Globalization;

namespace TKCode.Arc;

/// <summary>Small text helpers shared by the Projects blocks (name lists, counts with singular forms).</summary>
internal static class ProjectsText
{
    /// <summary>"A", "A and B", "A, B and C" (English list style; <paramref name="andWord"/> is localizable).</summary>
    public static string JoinNames(IEnumerable<string> names, string andWord = "and")
    {
        var list = names.Where(n => !string.IsNullOrWhiteSpace(n)).ToList();
        return list.Count switch
        {
            0 => string.Empty,
            1 => list[0],
            _ => string.Join(", ", list.Take(list.Count - 1)) + " " + andWord + " " + list[^1],
        };
    }

    /// <summary>Formats <paramref name="count"/> with <paramref name="one"/> when it is 1, else with <paramref name="other"/>.</summary>
    /// <param name="count">The number.</param>
    /// <param name="one">Format for 1 (may omit the number), e.g. "1 task".</param>
    /// <param name="other">Format for other numbers with {0}, e.g. "{0} tasks".</param>
    public static string Count(int count, string one, string other) =>
        count == 1 ? string.Format(CultureInfo.CurrentCulture, one, count) : string.Format(CultureInfo.CurrentCulture, other, count);

    /// <summary>Formats with the current culture, returning the format itself when it is malformed.</summary>
    public static string Format(string format, params object?[] args)
    {
        try
        {
            return string.Format(CultureInfo.CurrentCulture, format, args);
        }
        catch (FormatException)
        {
            return format;
        }
    }
}
