using System.Numerics;
using System.Runtime.CompilerServices;

namespace TKCode.Arc.Graphics3D;

/// <summary>Scalar helpers and the engine's conventions: right-handed, Y up, meters, radians; matrices follow
/// <see cref="System.Numerics"/> (row vectors: <c>v * World * View * Projection</c>).</summary>
public static class MathUtil
{
    /// <summary>Degrees to radians factor.</summary>
    public const float Deg2Rad = MathF.PI / 180f;

    /// <summary>Radians to degrees factor.</summary>
    public const float Rad2Deg = 180f / MathF.PI;

    /// <summary>Converts degrees to radians.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float ToRadians(float degrees) => degrees * Deg2Rad;

    /// <summary>Converts radians to degrees.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float ToDegrees(float radians) => radians * Rad2Deg;

    /// <summary>Linear interpolation.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float Lerp(float a, float b, float t) => a + ((b - a) * t);

    /// <summary>Hermite smooth step between two edges.</summary>
    public static float SmoothStep(float edge0, float edge1, float x)
    {
        var t = Math.Clamp((x - edge0) / (edge1 - edge0), 0f, 1f);
        return t * t * (3f - (2f * t));
    }

    /// <summary>sRGB transfer function, encoded to linear.</summary>
    public static float SrgbToLinear(float c) => c <= 0.04045f ? c / 12.92f : MathF.Pow((c + 0.055f) / 1.055f, 2.4f);

    /// <summary>sRGB transfer function, linear to encoded.</summary>
    public static float LinearToSrgb(float c) => c <= 0.0031308f ? c * 12.92f : (1.055f * MathF.Pow(c, 1f / 2.4f)) - 0.055f;

    /// <summary>Quaternion from intrinsic XYZ Euler angles in radians (rotate about X, then the new Y, then the new Z).</summary>
    public static Quaternion QuaternionFromEuler(Vector3 euler)
    {
        var (s1, c1) = MathF.SinCos(euler.X * 0.5f);
        var (s2, c2) = MathF.SinCos(euler.Y * 0.5f);
        var (s3, c3) = MathF.SinCos(euler.Z * 0.5f);
        return new Quaternion(
            (s1 * c2 * c3) + (c1 * s2 * s3),
            (c1 * s2 * c3) - (s1 * c2 * s3),
            (c1 * c2 * s3) + (s1 * s2 * c3),
            (c1 * c2 * c3) - (s1 * s2 * s3));
    }

    /// <summary>Intrinsic XYZ Euler angles in radians of a rotation (inverse of <see cref="QuaternionFromEuler"/>).</summary>
    public static Vector3 EulerFromQuaternion(Quaternion q)
    {
        // Elements of the column-vector rotation matrix R = Rx * Ry * Rz.
        float x2 = q.X + q.X, y2 = q.Y + q.Y, z2 = q.Z + q.Z;
        float xx = q.X * x2, xy = q.X * y2, xz = q.X * z2;
        float yy = q.Y * y2, yz = q.Y * z2, zz = q.Z * z2;
        float wx = q.W * x2, wy = q.W * y2, wz = q.W * z2;
        float m11 = 1f - (yy + zz), m12 = xy - wz, m13 = xz + wy;
        float m22 = 1f - (xx + zz), m23 = yz - wx;
        float m32 = yz + wx, m33 = 1f - (xx + yy);
        var y = MathF.Asin(Math.Clamp(m13, -1f, 1f));
        if (MathF.Abs(m13) < 0.9999999f)
        {
            return new Vector3(MathF.Atan2(-m23, m33), y, MathF.Atan2(-m12, m11));
        }

        return new Vector3(MathF.Atan2(m32, m22), y, 0f);
    }

    /// <summary>Scale, rotation and translation composed into a local matrix (scale first, translation last).</summary>
    public static Matrix4x4 Compose(Vector3 position, Quaternion rotation, Vector3 scale)
    {
        var m = Matrix4x4.CreateFromQuaternion(rotation);
        m.M11 *= scale.X;
        m.M12 *= scale.X;
        m.M13 *= scale.X;
        m.M21 *= scale.Y;
        m.M22 *= scale.Y;
        m.M23 *= scale.Y;
        m.M31 *= scale.Z;
        m.M32 *= scale.Z;
        m.M33 *= scale.Z;
        m.M41 = position.X;
        m.M42 = position.Y;
        m.M43 = position.Z;
        return m;
    }

    /// <summary>The largest scale factor of an affine matrix along its three axes.</summary>
    public static float MaxScale(in Matrix4x4 m)
    {
        var sx = (m.M11 * m.M11) + (m.M12 * m.M12) + (m.M13 * m.M13);
        var sy = (m.M21 * m.M21) + (m.M22 * m.M22) + (m.M23 * m.M23);
        var sz = (m.M31 * m.M31) + (m.M32 * m.M32) + (m.M33 * m.M33);
        return MathF.Sqrt(MathF.Max(sx, MathF.Max(sy, sz)));
    }

    /// <summary>Rounds up to the next power of two.</summary>
    public static int NextPowerOfTwo(int v) => v <= 1 ? 1 : (int)BitOperations.RoundUpToPowerOf2((uint)v);
}
