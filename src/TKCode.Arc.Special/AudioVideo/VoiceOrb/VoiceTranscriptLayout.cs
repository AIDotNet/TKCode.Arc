namespace TKCode.Arc;

/// <summary>
/// Pure text helpers of <see cref="VoiceTranscript"/>: splitting into words and balanced wrapping (CSS
/// <c>text-wrap: balance</c>: the same number of lines as greedy wrapping, at the narrowest width that keeps it).
/// </summary>
internal static class VoiceTranscriptLayout
{
    private static readonly char[] s_space = [' ', '\t', '\n', '\r', ' '];

    /// <summary>The words of <paramref name="text"/> (split on white space).</summary>
    public static string[] Words(string? text) =>
        string.IsNullOrWhiteSpace(text) ? [] : text.Split(s_space, StringSplitOptions.RemoveEmptyEntries);

    /// <summary>
    /// Greedy wrapping of words of <paramref name="widths"/> separated by <paramref name="space"/> into lines no wider
    /// than <paramref name="maxWidth"/> (a word wider than the line gets a line of its own). Writes each word's line index
    /// into <paramref name="lineOf"/> and returns the line count.
    /// </summary>
    public static int Greedy(ReadOnlySpan<double> widths, double space, double maxWidth, Span<int> lineOf)
    {
        if (widths.Length == 0)
        {
            return 0;
        }
        var line = 0;
        var x = 0.0;
        for (var i = 0; i < widths.Length; i++)
        {
            if (i == 0)
            {
                x = widths[0];
            }
            else if (x + space + widths[i] <= maxWidth + 0.01)
            {
                x += space + widths[i];
            }
            else
            {
                line++;
                x = widths[i];
            }
            lineOf[i] = line;
        }
        return line + 1;
    }

    /// <summary>
    /// Balanced wrapping: as many lines as greedy wrapping at <paramref name="maxWidth"/>, at the narrowest width that does
    /// not need more. Returns the line count.
    /// </summary>
    public static int Balance(ReadOnlySpan<double> widths, double space, double maxWidth, Span<int> lineOf)
    {
        var lines = Greedy(widths, space, maxWidth, lineOf);
        if (lines <= 1)
        {
            return lines;
        }
        var widest = 0.0;
        foreach (var w in widths)
        {
            widest = Math.Max(widest, w);
        }
        var lo = Math.Min(widest, maxWidth);
        var hi = maxWidth;
        for (var i = 0; i < 18 && hi - lo > 0.5; i++)
        {
            var mid = (lo + hi) / 2;
            if (Greedy(widths, space, mid, lineOf) <= lines)
            {
                hi = mid;
            }
            else
            {
                lo = mid;
            }
        }
        return Greedy(widths, space, hi, lineOf);
    }
}
