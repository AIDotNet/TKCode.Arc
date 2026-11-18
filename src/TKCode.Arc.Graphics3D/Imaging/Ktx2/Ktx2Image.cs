// The result of Ktx2Decoder: the pixels of a KTX 2.0 texture (https://registry.khronos.org/KTX/specs/2.0/ktxspec.v2.html)
// as 8-bit RGBA.

namespace TKCode.Arc.Graphics3D;

/// <summary>A decoded KTX2 texture: 8-bit RGBA pixels for every mip level.</summary>
public sealed class Ktx2Image
{
    internal Ktx2Image(int width, int height, bool isSrgb, bool hasAlpha, byte[][] levels)
    {
        Width = width;
        Height = height;
        IsSrgb = isSrgb;
        HasAlpha = hasAlpha;
        Levels = levels;
    }

    /// <summary>Width of level 0 in pixels.</summary>
    public int Width { get; }

    /// <summary>Height of level 0 in pixels.</summary>
    public int Height { get; }

    /// <summary>True when the file's data format descriptor declares the sRGB transfer function, false for linear data.</summary>
    public bool IsSrgb { get; }

    /// <summary>
    /// True when the file declares a fourth channel: RGBA data, or two-channel data whose second channel is in alpha
    /// (red in RGB, green in A). Otherwise the data is opaque and alpha decodes as 255.
    /// </summary>
    public bool HasAlpha { get; }

    /// <summary>
    /// The mip levels, level 0 first. Level i is max(1, Width >> i) x max(1, Height >> i) pixels of 4 bytes
    /// (red, green, blue, alpha), top row first.
    /// </summary>
    public IReadOnlyList<byte[]> Levels { get; }
}
