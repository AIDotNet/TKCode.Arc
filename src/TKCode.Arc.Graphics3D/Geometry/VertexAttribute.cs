using System.Numerics;
using System.Runtime.InteropServices;
using TKCode.Arc.Graphics3D.Rhi;

namespace TKCode.Arc.Graphics3D;

/// <summary>One per-vertex (or per-instance) data stream of a geometry: a typed array, split into items of
/// <see cref="ItemSize"/> components.</summary>
public sealed class VertexAttribute
{
    private readonly Array _data;

    private VertexAttribute(Array data, int itemSize, bool normalized, VertexFormat format, int componentSize)
    {
        if (itemSize is < 1 or > 4)
        {
            throw new ArgumentOutOfRangeException(nameof(itemSize), "An attribute has 1 to 4 components.");
        }

        _data = data;
        ItemSize = itemSize;
        Normalized = normalized;
        Format = format;
        ComponentSize = componentSize;
    }

    /// <summary>Components per vertex.</summary>
    public int ItemSize { get; }

    /// <summary>True when integer data maps to 0..1 (unsigned) or -1..1 (signed).</summary>
    public bool Normalized { get; }

    /// <summary>The GPU format.</summary>
    public VertexFormat Format { get; }

    /// <summary>Bytes per component.</summary>
    public int ComponentSize { get; }

    /// <summary>Number of vertices (or instances).</summary>
    public int Count => _data.Length / ItemSize;

    /// <summary>Hint that the data changes often.</summary>
    public bool Dynamic { get; set; }

    /// <summary>Changes whenever <see cref="NeedsUpdate"/> is called; the renderer uploads the data again.</summary>
    public int Version { get; private set; }

    /// <summary>The backing array (<c>float[]</c>, <c>byte[]</c>, <c>ushort[]</c>, ...).</summary>
    public Array Data => _data;

    /// <summary>The data as floats; throws when the attribute is not a float attribute.</summary>
    public float[] FloatData => _data as float[] ?? throw new InvalidOperationException("The attribute does not hold floats.");

    /// <summary>Tells the renderer that the array contents changed.</summary>
    public void NeedsUpdate() => Version++;

    /// <summary>A float attribute.</summary>
    public static VertexAttribute Float(float[] data, int itemSize) => new(data, itemSize, false, itemSize switch
    {
        1 => VertexFormat.Float1,
        2 => VertexFormat.Float2,
        3 => VertexFormat.Float3,
        _ => VertexFormat.Float4,
    }, 4);

    /// <summary>A float attribute from vectors.</summary>
    public static VertexAttribute Float(ReadOnlySpan<Vector3> data) => Float(MemoryMarshal.Cast<Vector3, float>(data).ToArray(), 3);

    /// <summary>A float attribute from vectors.</summary>
    public static VertexAttribute Float(ReadOnlySpan<Vector2> data) => Float(MemoryMarshal.Cast<Vector2, float>(data).ToArray(), 2);

    /// <summary>A float attribute from vectors.</summary>
    public static VertexAttribute Float(ReadOnlySpan<Vector4> data) => Float(MemoryMarshal.Cast<Vector4, float>(data).ToArray(), 4);

    /// <summary>An unsigned byte attribute.</summary>
    public static VertexAttribute UByte(byte[] data, int itemSize, bool normalized) => new(data, itemSize, normalized, (itemSize, normalized) switch
    {
        (4, false) => VertexFormat.UByte4,
        (4, true) => VertexFormat.UByte4Norm,
        (3, true) => VertexFormat.UByte3Norm,
        (2, true) => VertexFormat.UByte2Norm,
        _ => throw new NotSupportedException("Unsigned byte attributes are 4 components, or 2 to 4 normalized components."),
    }, 1);

    /// <summary>A signed byte attribute (normalized).</summary>
    public static VertexAttribute SByte(sbyte[] data, int itemSize) => new(data, itemSize, true, itemSize switch
    {
        4 => VertexFormat.SByte4Norm,
        3 => VertexFormat.SByte3Norm,
        2 => VertexFormat.SByte2Norm,
        _ => throw new NotSupportedException("Signed byte attributes have 2 to 4 components."),
    }, 1);

    /// <summary>An unsigned short attribute.</summary>
    public static VertexAttribute UShort(ushort[] data, int itemSize, bool normalized) => new(data, itemSize, normalized, (itemSize, normalized) switch
    {
        (4, false) => VertexFormat.UShort4,
        (2, false) => VertexFormat.UShort2,
        (4, true) => VertexFormat.UShort4Norm,
        (3, true) => VertexFormat.UShort3Norm,
        (2, true) => VertexFormat.UShort2Norm,
        _ => throw new NotSupportedException("Unsigned short attributes have 2 or 4 components, or 2 to 4 normalized components."),
    }, 2);

    /// <summary>A signed short attribute (normalized).</summary>
    public static VertexAttribute Short(short[] data, int itemSize) => new(data, itemSize, true, itemSize switch
    {
        4 => VertexFormat.Short4Norm,
        3 => VertexFormat.Short3Norm,
        2 => VertexFormat.Short2Norm,
        _ => throw new NotSupportedException("Signed short attributes have 2 to 4 components."),
    }, 2);

    /// <summary>The raw bytes of the array.</summary>
    public ReadOnlySpan<byte> AsBytes() => _data switch
    {
        float[] f => MemoryMarshal.AsBytes<float>(f),
        byte[] b => b,
        sbyte[] sb => MemoryMarshal.AsBytes<sbyte>(sb),
        ushort[] us => MemoryMarshal.AsBytes<ushort>(us),
        short[] s => MemoryMarshal.AsBytes<short>(s),
        uint[] ui => MemoryMarshal.AsBytes<uint>(ui),
        _ => default,
    };

    /// <summary>One component as a float, with normalization applied.</summary>
    public float Get(int index, int component)
    {
        var i = (index * ItemSize) + component;

        // Not a type switch on the array: the runtime treats sbyte[] as byte[] and short[] as ushort[] (array
        // covariance between same-sized integers), which would read signed data as unsigned.
        switch (Format)
        {
            case VertexFormat.Float1:
            case VertexFormat.Float2:
            case VertexFormat.Float3:
            case VertexFormat.Float4:
                return ((float[])_data)[i];
            case VertexFormat.UByte4:
                return ((byte[])_data)[i];
            case VertexFormat.UByte4Norm:
            case VertexFormat.UByte3Norm:
            case VertexFormat.UByte2Norm:
                return ((byte[])_data)[i] / 255f;
            case VertexFormat.SByte4Norm:
            case VertexFormat.SByte3Norm:
            case VertexFormat.SByte2Norm:
                return MathF.Max(((sbyte[])_data)[i] / 127f, -1f);
            case VertexFormat.UShort4:
            case VertexFormat.UShort2:
                return ((ushort[])_data)[i];
            case VertexFormat.UShort4Norm:
            case VertexFormat.UShort3Norm:
            case VertexFormat.UShort2Norm:
                return ((ushort[])_data)[i] / 65535f;
            case VertexFormat.Short4Norm:
            case VertexFormat.Short3Norm:
            case VertexFormat.Short2Norm:
                return MathF.Max(((short[])_data)[i] / 32767f, -1f);
            case VertexFormat.UInt4:
                return ((uint[])_data)[i];
            default:
                return 0f;
        }
    }

    /// <summary>The first three components of an item.</summary>
    public Vector3 GetVector3(int index)
    {
        if (ComponentSize == 4 && Format <= VertexFormat.Float4 && ItemSize >= 3)
        {
            var f = (float[])_data;
            var i = index * ItemSize;
            return new Vector3(f[i], f[i + 1], f[i + 2]);
        }

        return new Vector3(Get(index, 0), ItemSize > 1 ? Get(index, 1) : 0f, ItemSize > 2 ? Get(index, 2) : 0f);
    }

    /// <summary>The first two components of an item.</summary>
    public Vector2 GetVector2(int index) => new(Get(index, 0), ItemSize > 1 ? Get(index, 1) : 0f);

    /// <summary>The item padded to four components (missing ones are 0, the fourth is 1).</summary>
    public Vector4 GetVector4(int index) => new(
        Get(index, 0), ItemSize > 1 ? Get(index, 1) : 0f, ItemSize > 2 ? Get(index, 2) : 0f, ItemSize > 3 ? Get(index, 3) : 1f);
}

/// <summary>A range of a geometry drawn with one material of a multi-material object.</summary>
public readonly record struct GeometryGroup(int Start, int Count, int MaterialIndex);

/// <summary>Per-vertex offsets blended into a geometry by a morph weight.</summary>
public sealed class MorphTarget
{
    /// <summary>Creates a target from position offsets (xyz per vertex) and optional normal offsets.</summary>
    public MorphTarget(float[] positionDeltas, float[]? normalDeltas = null, string name = "")
    {
        PositionDeltas = positionDeltas;
        NormalDeltas = normalDeltas;
        Name = name;
    }

    public string Name { get; }

    /// <summary>xyz per vertex, added to the position.</summary>
    public float[] PositionDeltas { get; }

    /// <summary>xyz per vertex, added to the normal; may be null.</summary>
    public float[]? NormalDeltas { get; }
}
