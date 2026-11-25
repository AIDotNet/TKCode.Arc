// Ported from vscode (src/vs/editor/browser/viewParts/minimap/minimapCharRenderer.ts, commit b57d5c9e), MIT license.
//
// Differences: the target is a MinimapImageData (upstream: the canvas ImageData); a request outside the image is
// ignored silently (upstream warns on the console); colors are RgbaColor (upstream's RGBA8).

namespace TKCode.Arc.Editor.ViewParts;

/// <summary>Draws minimap characters (or blocks) of one scale into an RGBA buffer. Immutable; shareable between editors.</summary>
internal sealed class MinimapCharRenderer
{
    private readonly byte[] _charDataNormal;
    private readonly byte[] _charDataLight;

    public MinimapCharRenderer(byte[] charData, int scale)
    {
        Scale = scale;
        _charDataNormal = Soften(charData, 12.0 / 15);
        _charDataLight = Soften(charData, 50.0 / 60);
    }

    public int Scale { get; }

    private static byte[] Soften(byte[] input, double ratio)
    {
        var result = new byte[input.Length];
        for (var i = 0; i < input.Length; i++)
        {
            result[i] = Uint8.ToUint8(input[i] * ratio);
        }

        return result;
    }

    public void RenderChar(
        MinimapImageData target,
        int dx,
        int dy,
        int chCode,
        RgbaColor color,
        int foregroundAlpha,
        RgbaColor backgroundColor,
        int backgroundAlpha,
        int fontScale,
        bool useLighterFont,
        bool force1pxHeight)
    {
        var charWidth = MinimapCharSheet.BaseCharWidth * Scale;
        var charHeight = MinimapCharSheet.BaseCharHeight * Scale;
        var renderHeight = force1pxHeight ? 1 : charHeight;
        if (dx + charWidth > target.Width || dy + renderHeight > target.Height)
        {
            // bad render request outside image data
            return;
        }

        var charData = useLighterFont ? _charDataLight : _charDataNormal;
        var charIndex = MinimapCharSheet.GetCharIndex(chCode, fontScale);

        var destWidth = target.Width * MinimapCharSheet.RgbaChannelsCnt;

        double backgroundR = backgroundColor.R;
        double backgroundG = backgroundColor.G;
        double backgroundB = backgroundColor.B;

        var deltaR = color.R - backgroundR;
        var deltaG = color.G - backgroundG;
        var deltaB = color.B - backgroundB;

        var destAlpha = Uint8.Clamped(Math.Max(foregroundAlpha, backgroundAlpha));

        var dest = target.Data;
        var sourceOffset = charIndex * charWidth * charHeight;

        var row = (dy * destWidth) + (dx * MinimapCharSheet.RgbaChannelsCnt);
        for (var y = 0; y < renderHeight; y++)
        {
            var column = row;
            for (var x = 0; x < charWidth; x++)
            {
                var c = charData[sourceOffset++] / 255.0 * (foregroundAlpha / 255.0);
                dest[column++] = Uint8.Clamped(backgroundR + (deltaR * c));
                dest[column++] = Uint8.Clamped(backgroundG + (deltaG * c));
                dest[column++] = Uint8.Clamped(backgroundB + (deltaB * c));
                dest[column++] = destAlpha;
            }

            row += destWidth;
        }
    }

    public void BlockRenderChar(
        MinimapImageData target,
        int dx,
        int dy,
        RgbaColor color,
        int foregroundAlpha,
        RgbaColor backgroundColor,
        int backgroundAlpha,
        bool force1pxHeight)
    {
        var charWidth = MinimapCharSheet.BaseCharWidth * Scale;
        var charHeight = MinimapCharSheet.BaseCharHeight * Scale;
        var renderHeight = force1pxHeight ? 1 : charHeight;
        if (dx + charWidth > target.Width || dy + renderHeight > target.Height)
        {
            // bad render request outside image data
            return;
        }

        var destWidth = target.Width * MinimapCharSheet.RgbaChannelsCnt;

        var c = 0.5 * (foregroundAlpha / 255.0);

        double backgroundR = backgroundColor.R;
        double backgroundG = backgroundColor.G;
        double backgroundB = backgroundColor.B;

        var deltaR = color.R - backgroundR;
        var deltaG = color.G - backgroundG;
        var deltaB = color.B - backgroundB;

        var colorR = Uint8.Clamped(backgroundR + (deltaR * c));
        var colorG = Uint8.Clamped(backgroundG + (deltaG * c));
        var colorB = Uint8.Clamped(backgroundB + (deltaB * c));

        var destAlpha = Uint8.Clamped(Math.Max(foregroundAlpha, backgroundAlpha));

        var dest = target.Data;

        var row = (dy * destWidth) + (dx * MinimapCharSheet.RgbaChannelsCnt);
        for (var y = 0; y < renderHeight; y++)
        {
            var column = row;
            for (var x = 0; x < charWidth; x++)
            {
                dest[column++] = colorR;
                dest[column++] = colorG;
                dest[column++] = colorB;
                dest[column++] = destAlpha;
            }

            row += destWidth;
        }
    }
}
