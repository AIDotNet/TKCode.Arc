namespace TKCode.Arc;

/// <summary>
/// Shared helpers for the field family: the Arc number test used by message copy. Animated blurs use the Core
/// <see cref="Primitives.ArcBlur"/> cache.
/// </summary>
internal static class FieldDrawing
{
    /// <summary>Arc's number test (<c>/^\d[\d.,/:]*%?$/</c>): counts like "12", "1,204", "3/5", "50%".</summary>
    public static bool IsNumber(string word)
    {
        if (word.Length == 0 || !char.IsAsciiDigit(word[0]))
        {
            return false;
        }
        var end = word.Length;
        if (word[end - 1] == '%')
        {
            end--;
        }
        for (var i = 1; i < end; i++)
        {
            var c = word[i];
            if (!char.IsAsciiDigit(c) && c != '.' && c != ',' && c != '/' && c != ':')
            {
                return false;
            }
        }
        return true;
    }

    /// <summary>JavaScript <c>parseFloat(word.replace(/,/g, ""))</c>: the leading numeric part.</summary>
    public static double LeadingNumber(string word)
    {
        Span<char> buffer = stackalloc char[Math.Min(word.Length, 64)];
        var length = 0;
        var seenDot = false;
        foreach (var c in word)
        {
            if (c == ',')
            {
                continue;
            }
            if (char.IsAsciiDigit(c) || (c == '.' && !seenDot))
            {
                seenDot |= c == '.';
                if (length < buffer.Length)
                {
                    buffer[length++] = c;
                }
                continue;
            }
            break;
        }
        return double.TryParse(buffer[..length], System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.InvariantCulture, out var value) ? value : double.NaN;
    }

    public static double Lerp(double from, double to, double t) => from + (to - from) * t;
}
