using Avalonia;
using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>
/// Shared drawing helpers for the field family: cached blur effects (quantized to 0.25px so animated blurs do
/// not allocate per frame) and the Arc number test used by message copy.
/// </summary>
internal static class FieldDrawing
{
    private const int Steps = 4;
    private static readonly IEffect?[] s_blurs = new IEffect?[8 * Steps + 1];

    /// <summary>A cached blur effect for <paramref name="radius"/> px, or null when it rounds to zero.</summary>
    public static IEffect? Blur(double radius)
    {
        var index = (int)Math.Round(Math.Clamp(radius, 0, 8) * Steps);
        if (index <= 0)
        {
            return null;
        }
        return s_blurs[index] ??= new ImmutableBlurEffect(index / (double)Steps);
    }

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
