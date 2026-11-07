namespace TKCode.Arc;

/// <summary>Hand-written timestamp readers for WebVTT and SRT timing lines (no regular expressions).</summary>
internal static class CaptionTimestamps
{
    private const int MaxDigits = 9;

    /// <summary>
    /// Reads a WebVTT timestamp <c>[hh:]mm:ss.ttt</c> at <paramref name="pos"/> (W3C WebVTT "collect a WebVTT timestamp"):
    /// hours take two or more digits, minutes and seconds exactly two (at most 59), fractions exactly three.
    /// </summary>
    public static bool TryReadVtt(ReadOnlySpan<char> s, ref int pos, out TimeSpan time)
    {
        time = default;
        var p = pos;
        if (!TryDigits(s, ref p, out var first, out var firstCount))
        {
            return false;
        }
        var hoursFirst = firstCount != 2 || first > 59;
        if (!Expect(s, ref p, ':') || !TryDigits(s, ref p, out var second, out var secondCount) || secondCount != 2)
        {
            return false;
        }
        long hours;
        long minutes;
        long seconds;
        if (hoursFirst || (p < s.Length && s[p] == ':'))
        {
            if (!Expect(s, ref p, ':') || !TryDigits(s, ref p, out var third, out var thirdCount) || thirdCount != 2)
            {
                return false;
            }
            (hours, minutes, seconds) = (first, second, third);
        }
        else
        {
            (hours, minutes, seconds) = (0, first, second);
        }
        if (!Expect(s, ref p, '.') || !TryDigits(s, ref p, out var millis, out var millisCount) || millisCount != 3)
        {
            return false;
        }
        if (minutes > 59 || seconds > 59)
        {
            return false;
        }
        time = Compose(hours, minutes, seconds, millis);
        pos = p;
        return true;
    }

    /// <summary>
    /// Reads an SRT timestamp <c>HH:MM:SS,mmm</c> at <paramref name="pos"/>, tolerating a '.' separator, one-digit fields,
    /// a missing hours field and one to three fraction digits.
    /// </summary>
    public static bool TryReadSrt(ReadOnlySpan<char> s, ref int pos, out TimeSpan time)
    {
        time = default;
        var p = pos;
        if (!TryDigits(s, ref p, out var first, out _) || !Expect(s, ref p, ':'))
        {
            return false;
        }
        if (!TryDigits(s, ref p, out var second, out var secondCount) || secondCount > 2)
        {
            return false;
        }
        long hours = 0;
        long minutes;
        long seconds;
        if (p < s.Length && s[p] == ':')
        {
            p++;
            if (!TryDigits(s, ref p, out var third, out var thirdCount) || thirdCount > 2)
            {
                return false;
            }
            (hours, minutes, seconds) = (first, second, third);
        }
        else
        {
            (minutes, seconds) = (first, second);
        }
        long millis = 0;
        if (p < s.Length && s[p] is ',' or '.')
        {
            p++;
            if (!TryDigits(s, ref p, out var fraction, out var fractionCount) || fractionCount > 3)
            {
                return false;
            }
            millis = fractionCount switch
            {
                1 => fraction * 100,
                2 => fraction * 10,
                _ => fraction,
            };
        }
        if (minutes > 59 || seconds > 59)
        {
            return false;
        }
        time = Compose(hours, minutes, seconds, millis);
        pos = p;
        return true;
    }

    /// <summary>Formats a time as <c>hh:mm:ss.ttt</c> (for snapshots and warnings).</summary>
    public static string Format(TimeSpan time) =>
        $"{(long)time.TotalHours:00}:{time.Minutes:00}:{time.Seconds:00}.{time.Milliseconds:000}";

    private static TimeSpan Compose(long hours, long minutes, long seconds, long millis) =>
        TimeSpan.FromMilliseconds((((hours * 60) + minutes) * 60 + seconds) * 1000 + millis);

    private static bool Expect(ReadOnlySpan<char> s, ref int p, char c)
    {
        if (p < s.Length && s[p] == c)
        {
            p++;
            return true;
        }
        return false;
    }

    private static bool TryDigits(ReadOnlySpan<char> s, ref int p, out long value, out int count)
    {
        value = 0;
        count = 0;
        while (p < s.Length && char.IsAsciiDigit(s[p]))
        {
            if (count >= MaxDigits)
            {
                return false;
            }
            value = (value * 10) + (s[p] - '0');
            count++;
            p++;
        }
        return count > 0;
    }
}
