// Ported from vscode (src/vs/editor/browser/viewParts/minimap/minimapCharRendererFactory.ts, commit b57d5c9e), MIT
// license.
//
// Differences:
//  - upstream keeps the last renderer in a static field; here renderers are memoized per scale in an immutable cache
//    (renderers are immutable, a race creates an equal one), so editors never share mutable state;
//  - upstream samples the font on a canvas for scales without prebaked data (above 2); the engine draws no text, so
//    CreateSampleData is the caller's (a delegate), and without one the scale 2 sheet is enlarged (nearest pixel),
//    which keeps the prebaked look at every scale.

using System.Collections.Concurrent;

namespace TKCode.Arc.Editor.ViewParts;

/// <summary>
/// Creates character renderers. It takes a 'scale' that determines how large characters should be drawn. Using this,
/// it draws data into a canvas and then downsamples the characters as necessary for the current display.
/// </summary>
internal static class MinimapCharRendererFactory
{
    private static readonly ConcurrentDictionary<int, MinimapCharRenderer> s_renderers = new();

    /// <summary>
    /// The renderer of a scale: prebaked data for scales 1 and 2, else the font sampled by
    /// <paramref name="createSampleData"/> (RGBA, <see cref="MinimapCharSheet.SampledCharWidth"/> by
    /// <see cref="MinimapCharSheet.SampledCharHeight"/> per character, the sheet's characters in a row), else the scale 2
    /// sheet enlarged.
    /// </summary>
    public static MinimapCharRenderer Create(int scale, Func<byte[]>? createSampleData = null)
    {
        if (MinimapPreBaked.Get(scale) is { } prebaked)
        {
            return s_renderers.GetOrAdd(scale, s => new MinimapCharRenderer(prebaked, s));
        }

        if (createSampleData is not null)
        {
            return CreateFromSampleData(createSampleData(), scale);
        }

        return s_renderers.GetOrAdd(scale, s => new MinimapCharRenderer(Enlarge(MinimapPreBaked.Get(2)!, 2, s), s));
    }

    /// <summary>Creates a character renderer from the canvas sample data.</summary>
    public static MinimapCharRenderer CreateFromSampleData(byte[] source, int scale)
    {
        const int expectedLength = MinimapCharSheet.SampledCharHeight * MinimapCharSheet.SampledCharWidth * MinimapCharSheet.RgbaChannelsCnt * MinimapCharSheet.CharCount;
        if (source.Length != expectedLength)
        {
            throw new ArgumentException("Unexpected source in MinimapCharRenderer", nameof(source));
        }

        var charData = Downsample(source, scale);
        return new MinimapCharRenderer(charData, scale);
    }

    private static double DownsampleChar(byte[] source, int sourceOffset, byte[] dest, int destOffset, int scale)
    {
        var width = MinimapCharSheet.BaseCharWidth * scale;
        var height = MinimapCharSheet.BaseCharHeight * scale;

        var targetIndex = destOffset;
        double brightest = 0;

        // This is essentially an ad-hoc rescaling algorithm. Standard approaches like bicubic interpolation are awesome
        // for scaling between image sizes, but don't work so well when scaling to very small pixel values, we end up
        // with blurry, indistinct forms.
        //
        // The approach taken here is simply mapping each source pixel to the target pixels, and taking the weighted
        // values for all pixels in each, and then averaging them out. Finally we apply an intensity boost in Downsample,
        // since when scaling to the smallest pixel sizes there's more black space which causes characters to be much
        // less distinct.
        for (var y = 0; y < height; y++)
        {
            // 1. For this destination pixel, get the source pixels we're sampling from (x1, y1) to the next pixel (x2, y2)
            var sourceY1 = (double)y / height * MinimapCharSheet.SampledCharHeight;
            var sourceY2 = (double)(y + 1) / height * MinimapCharSheet.SampledCharHeight;

            for (var x = 0; x < width; x++)
            {
                var sourceX1 = (double)x / width * MinimapCharSheet.SampledCharWidth;
                var sourceX2 = (double)(x + 1) / width * MinimapCharSheet.SampledCharWidth;

                // 2. Sample all of them, summing them up and weighting them. Similar to bilinear interpolation.
                double value = 0;
                double samples = 0;
                for (var sy = sourceY1; sy < sourceY2; sy++)
                {
                    var sourceRow = sourceOffset + ((int)Math.Floor(sy) * MinimapCharSheet.RgbaSampledRowWidth);
                    var yBalance = 1 - (sy - Math.Floor(sy));
                    for (var sx = sourceX1; sx < sourceX2; sx++)
                    {
                        var xBalance = 1 - (sx - Math.Floor(sx));
                        var sourceIndex = sourceRow + ((int)Math.Floor(sx) * MinimapCharSheet.RgbaChannelsCnt);

                        var weight = xBalance * yBalance;
                        samples += weight;
                        value += source[sourceIndex] * source[sourceIndex + 3] / 255.0 * weight;
                    }
                }

                var final = value / samples;
                brightest = Math.Max(brightest, final);
                dest[targetIndex++] = Uint8.ToUint8(final);
            }
        }

        return brightest;
    }

    private static byte[] Downsample(byte[] data, int scale)
    {
        var pixelsPerCharacter = MinimapCharSheet.BaseCharHeight * scale * MinimapCharSheet.BaseCharWidth * scale;
        var resultLen = pixelsPerCharacter * MinimapCharSheet.CharCount;
        var result = new byte[resultLen];

        var resultOffset = 0;
        var sourceOffset = 0;
        double brightest = 0;
        for (var charIndex = 0; charIndex < MinimapCharSheet.CharCount; charIndex++)
        {
            brightest = Math.Max(brightest, DownsampleChar(data, sourceOffset, result, resultOffset, scale));
            resultOffset += pixelsPerCharacter;
            sourceOffset += MinimapCharSheet.SampledCharWidth * MinimapCharSheet.RgbaChannelsCnt;
        }

        if (brightest > 0)
        {
            var adjust = 255 / brightest;
            for (var i = 0; i < resultLen; i++)
            {
                // a Uint8ClampedArray `*=`
                result[i] = Uint8.Clamped(result[i] * adjust);
            }
        }

        return result;
    }

    // a character sheet of one scale drawn at another, nearest pixel (each character is width x height bytes, in order)
    private static byte[] Enlarge(byte[] data, int fromScale, int toScale)
    {
        var fromWidth = MinimapCharSheet.BaseCharWidth * fromScale;
        var fromHeight = MinimapCharSheet.BaseCharHeight * fromScale;
        var toWidth = MinimapCharSheet.BaseCharWidth * toScale;
        var toHeight = MinimapCharSheet.BaseCharHeight * toScale;
        var result = new byte[toWidth * toHeight * MinimapCharSheet.CharCount];
        var target = 0;
        for (var charIndex = 0; charIndex < MinimapCharSheet.CharCount; charIndex++)
        {
            var sourceOffset = charIndex * fromWidth * fromHeight;
            for (var y = 0; y < toHeight; y++)
            {
                var sourceY = y * fromHeight / toHeight;
                for (var x = 0; x < toWidth; x++)
                {
                    var sourceX = x * fromWidth / toWidth;
                    result[target++] = data[sourceOffset + (sourceY * fromWidth) + sourceX];
                }
            }
        }

        return result;
    }
}
