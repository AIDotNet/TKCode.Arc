using System.Globalization;
using System.Text;

namespace TKCode.Arc;

/// <summary>The date strings of a PDF's metadata, which backends hand over as they are in the file.</summary>
internal static class PdfDateText
{
    /// <summary>The PDF date form, <c>D:YYYYMMDDHHmmSSOHH'mm'</c>, where everything after the year is optional. Anything else is null.</summary>
    public static DateTimeOffset? Parse(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }
        var value = text.AsSpan().Trim();
        if (value.StartsWith("D:", StringComparison.Ordinal))
        {
            value = value[2..];
        }
        var digits = 0;
        while (digits < value.Length && char.IsAsciiDigit(value[digits]))
        {
            digits++;
        }
        if (digits < 4 || digits > 14 || digits % 2 != 0)
        {
            return null;
        }
        var numbers = value[..digits].ToString();
        int Part(int start, int length, int missing) =>
            numbers.Length >= start + length ? int.Parse(numbers.AsSpan(start, length), NumberStyles.None, CultureInfo.InvariantCulture) : missing;
        var (year, month, day) = (Part(0, 4, 0), Part(4, 2, 1), Part(6, 2, 1));
        var (hour, minute, second) = (Part(8, 2, 0), Part(10, 2, 0), Part(12, 2, 0));

        var offset = TimeSpan.Zero;
        var zone = value[digits..];
        if (zone.Length > 0 && (zone[0] is '+' or '-'))
        {
            var zoneDigits = new StringBuilder(4);
            foreach (var c in zone[1..])
            {
                if (char.IsAsciiDigit(c))
                {
                    zoneDigits.Append(c);
                }
            }
            if (zoneDigits.Length is not (2 or 4))
            {
                return null;
            }
            var zoneText = zoneDigits.ToString();
            var hours = int.Parse(zoneText.AsSpan(0, 2), NumberStyles.None, CultureInfo.InvariantCulture);
            var minutes = zoneText.Length == 4 ? int.Parse(zoneText.AsSpan(2, 2), NumberStyles.None, CultureInfo.InvariantCulture) : 0;
            offset = new TimeSpan(hours, minutes, 0);
            if (zone[0] == '-')
            {
                offset = -offset;
            }
        }
        try
        {
            return new DateTimeOffset(year, month, day, hour, minute, second, offset);
        }
        catch (ArgumentOutOfRangeException)
        {
            return null;
        }
    }
}
