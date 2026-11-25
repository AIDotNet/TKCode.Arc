// Ported from vscode (src/vs/editor/browser/viewParts/minimap/minimapCharSheet.ts, commit b57d5c9e), MIT license.
//
// Differences: the constants are a static class; AllCharCodes is an immutable array.

namespace TKCode.Arc.Editor.ViewParts;

/// <summary>The character sheet the minimap draws from: the printable ASCII characters and a placeholder.</summary>
internal static class MinimapCharSheet
{
    /// <summary>Space.</summary>
    public const int StartChCode = 32;

    /// <summary>Tilde (~).</summary>
    public const int EndChCode = 126;

    /// <summary>The UTF placeholder code.</summary>
    public const int UnknownCode = 65533;

    public const int CharCount = EndChCode - StartChCode + 2;

    public const int SampledCharHeight = 16;

    public const int SampledCharWidth = 10;

    public const int BaseCharHeight = 2;

    public const int BaseCharWidth = 1;

    public const int RgbaChannelsCnt = 4;

    public const int RgbaSampledRowWidth = RgbaChannelsCnt * CharCount * SampledCharWidth;

    /// <summary>The characters of the sheet, in order.</summary>
    public static readonly IReadOnlyList<int> AllCharCodes = CreateAllCharCodes();

    public static int GetCharIndex(int chCode, int fontScale)
    {
        chCode -= StartChCode;
        if (chCode < 0 || chCode > CharCount)
        {
            if (fontScale <= 2)
            {
                // for smaller scales, we can get away with using any ASCII character...
                return (chCode + CharCount) % CharCount;
            }

            return CharCount - 1; // unknown symbol
        }

        return chCode;
    }

    private static int[] CreateAllCharCodes()
    {
        var v = new List<int>(CharCount);
        for (var i = StartChCode; i <= EndChCode; i++)
        {
            v.Add(i);
        }

        v.Add(UnknownCode);
        return [.. v];
    }
}

/// <summary>An RGBA pixel buffer (the browser's ImageData): <see cref="Data"/> holds 4 bytes per pixel, row by row.</summary>
internal sealed class MinimapImageData
{
    public MinimapImageData(int width, int height)
    {
        Width = width;
        Height = height;
        Data = new byte[width * height * MinimapCharSheet.RgbaChannelsCnt];
    }

    public int Width { get; }

    public int Height { get; }

    public byte[] Data { get; }
}

/// <summary>Stores into byte arrays the way the browser's Uint8ClampedArray and upstream's toUint8 do.</summary>
internal static class Uint8
{
    /// <summary>A Uint8ClampedArray store: clamped to 0..255 and rounded half to even.</summary>
    public static byte Clamped(double v)
    {
        if (double.IsNaN(v) || v <= 0)
        {
            return 0;
        }

        if (v >= 255)
        {
            return 255;
        }

        return (byte)Math.Round(v, MidpointRounding.ToEven);
    }

    /// <summary>Upstream's toUint8 (base/common/uint.ts): clamped to 0..255 and truncated.</summary>
    public static byte ToUint8(double v)
    {
        if (v < 0)
        {
            return 0;
        }

        if (v > 255)
        {
            return 255;
        }

        return (byte)(int)v;
    }
}
