// Ported from vscode (src/vs/base/common/color.ts: RGBA, Color.fromHex, Color.Format.CSS.parseHex and formatHex,
// commit b57d5c9e), MIT license.
//
// Differences: only the RGBA form the tokenizer, the token themes and the color map use (no HSLA / HSVA, no blending);
// a value type with bytes for the channels (upstream keeps alpha as a 0..1 number: here 0..255).

using System.Globalization;

namespace TKCode.Arc.Editor;

/// <summary>A color in sRGB with alpha, each channel 0..255 (upstream's Color with its RGBA).</summary>
internal readonly record struct RgbaColor(byte R, byte G, byte B, byte A = 255)
{
    public static readonly RgbaColor White = new(255, 255, 255);

    public static readonly RgbaColor Black = new(0, 0, 0);

    public static readonly RgbaColor Transparent = new(0, 0, 0, 0);

    /// <summary>
    /// Parses <c>#RGB</c>, <c>#RGBA</c>, <c>#RRGGBB</c> or <c>#RRGGBBAA</c> (upstream's parseHex); null when the text is
    /// not one of them. The leading <c>#</c> is required, as upstream.
    /// </summary>
    public static RgbaColor? TryParseHex(string? hex)
    {
        if (hex is null || hex.Length < 4 || hex[0] != '#')
        {
            return null;
        }

        var digits = hex.AsSpan(1);
        foreach (var ch in digits)
        {
            if (!char.IsAsciiHexDigit(ch))
            {
                return null;
            }
        }

        switch (digits.Length)
        {
            case 3:
                return new RgbaColor(Short(digits[0]), Short(digits[1]), Short(digits[2]));
            case 4:
                return new RgbaColor(Short(digits[0]), Short(digits[1]), Short(digits[2]), Short(digits[3]));
            case 6:
                return new RgbaColor(Byte(digits[..2]), Byte(digits[2..4]), Byte(digits[4..6]));
            case 8:
                return new RgbaColor(Byte(digits[..2]), Byte(digits[2..4]), Byte(digits[4..6]), Byte(digits[6..8]));
            default:
                return null;
        }
    }

    /// <summary>Parses like <see cref="TryParseHex"/> and throws when the text is not a hex color (upstream's fromHex returns red).</summary>
    public static RgbaColor FromHex(string hex) =>
        TryParseHex(hex) ?? throw new FormatException($"'{hex}' is not a hex color");

    /// <summary><c>#rrggbb</c>, or <c>#rrggbbaa</c> when not opaque (upstream's formatHexA with compact false).</summary>
    public string ToHex() => A == 255
        ? string.Create(CultureInfo.InvariantCulture, $"#{R:x2}{G:x2}{B:x2}")
        : string.Create(CultureInfo.InvariantCulture, $"#{R:x2}{G:x2}{B:x2}{A:x2}");

    /// <inheritdoc />
    public override string ToString() => ToHex();

    private static byte Short(char digit)
    {
        var value = Convert.ToByte(digit.ToString(), 16);
        return (byte)((value << 4) | value);
    }

    private static byte Byte(ReadOnlySpan<char> digits) => byte.Parse(digits, NumberStyles.HexNumber, CultureInfo.InvariantCulture);
}
