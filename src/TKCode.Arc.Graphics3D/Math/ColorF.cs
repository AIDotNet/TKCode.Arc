using System.Numerics;

namespace TKCode.Arc.Graphics3D;

/// <summary>An RGB color in the linear sRGB working space (what lighting is computed in). Create it from display
/// values with <see cref="FromSrgb(float, float, float)"/> or <see cref="FromHex"/>.</summary>
public readonly struct ColorF : IEquatable<ColorF>
{
    /// <summary>Red, linear.</summary>
    public readonly float R;

    /// <summary>Green, linear.</summary>
    public readonly float G;

    /// <summary>Blue, linear.</summary>
    public readonly float B;

    /// <summary>Creates a color from linear components.</summary>
    public ColorF(float r, float g, float b)
    {
        R = r;
        G = g;
        B = b;
    }

    /// <summary>White.</summary>
    public static ColorF White => new(1f, 1f, 1f);

    /// <summary>Black.</summary>
    public static ColorF Black => default;

    /// <summary>Creates a color from sRGB-encoded components in 0..1.</summary>
    public static ColorF FromSrgb(float r, float g, float b) =>
        new(MathUtil.SrgbToLinear(r), MathUtil.SrgbToLinear(g), MathUtil.SrgbToLinear(b));

    /// <summary>Creates a color from sRGB bytes.</summary>
    public static ColorF FromSrgb(byte r, byte g, byte b) => FromSrgb(r / 255f, g / 255f, b / 255f);

    /// <summary>Creates a color from an sRGB hex value such as <c>0xFF8800</c>.</summary>
    public static ColorF FromHex(uint rgb) => FromSrgb((byte)(rgb >> 16), (byte)(rgb >> 8), (byte)rgb);

    /// <summary>Creates a color from hue (0..1), saturation and lightness, like CSS <c>hsl()</c>.</summary>
    public static ColorF FromHsl(float h, float s, float l)
    {
        h -= MathF.Floor(h);
        s = Math.Clamp(s, 0f, 1f);
        l = Math.Clamp(l, 0f, 1f);
        if (s == 0f)
        {
            return FromSrgb(l, l, l);
        }

        var q = l <= 0.5f ? l * (1f + s) : l + s - (l * s);
        var p = (2f * l) - q;
        return FromSrgb(Hue(p, q, h + (1f / 3f)), Hue(p, q, h), Hue(p, q, h - (1f / 3f)));

        static float Hue(float p, float q, float t)
        {
            if (t < 0f)
            {
                t += 1f;
            }

            if (t > 1f)
            {
                t -= 1f;
            }

            if (t < 1f / 6f)
            {
                return p + ((q - p) * 6f * t);
            }

            if (t < 0.5f)
            {
                return q;
            }

            if (t < 2f / 3f)
            {
                return p + ((q - p) * 6f * ((2f / 3f) - t));
            }

            return p;
        }
    }

    /// <summary>The sRGB-encoded components in 0..1.</summary>
    public Vector3 ToSrgb() => new(MathUtil.LinearToSrgb(R), MathUtil.LinearToSrgb(G), MathUtil.LinearToSrgb(B));

    /// <summary>Relative luminance (Rec. 709).</summary>
    public float Luminance => (0.2126f * R) + (0.7152f * G) + (0.0722f * B);

    /// <summary>The linear components as a vector.</summary>
    public Vector3 ToVector3() => new(R, G, B);

    /// <summary>Linear interpolation between two colors.</summary>
    public static ColorF Lerp(ColorF a, ColorF b, float t) =>
        new(MathUtil.Lerp(a.R, b.R, t), MathUtil.Lerp(a.G, b.G, t), MathUtil.Lerp(a.B, b.B, t));

    public static ColorF operator *(ColorF c, float s) => new(c.R * s, c.G * s, c.B * s);

    public static ColorF operator *(ColorF a, ColorF b) => new(a.R * b.R, a.G * b.G, a.B * b.B);

    public static ColorF operator +(ColorF a, ColorF b) => new(a.R + b.R, a.G + b.G, a.B + b.B);

    public static bool operator ==(ColorF a, ColorF b) => a.Equals(b);

    public static bool operator !=(ColorF a, ColorF b) => !a.Equals(b);

    public static implicit operator Vector3(ColorF c) => new(c.R, c.G, c.B);

    public static explicit operator ColorF(Vector3 v) => new(v.X, v.Y, v.Z);

    /// <inheritdoc/>
    public bool Equals(ColorF other) => R == other.R && G == other.G && B == other.B;

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is ColorF other && Equals(other);

    /// <inheritdoc/>
    public override int GetHashCode() => HashCode.Combine(R, G, B);

    /// <inheritdoc/>
    public override string ToString() => $"ColorF({R:0.###}, {G:0.###}, {B:0.###})";
}
