using System.Buffers.Binary;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;

namespace TKCode.Arc.Graphics3D.Tests;

/// <summary>Builds small glTF documents for tests: collects binary data into one buffer with its buffer views and
/// accessors, and wraps a hand-written JSON body (meshes, nodes, ...) as a <c>.glb</c> or a <c>.gltf</c>.</summary>
internal sealed class GltfBuilder
{
    public const int Byte = 5120;
    public const int UByte = 5121;
    public const int Short = 5122;
    public const int UShort = 5123;
    public const int UInt = 5125;
    public const int Float = 5126;

    private readonly MemoryStream _bin = new();
    private readonly List<string> _views = [];
    private readonly List<string> _accessors = [];

    /// <summary>The binary buffer so far.</summary>
    public byte[] Buffer => _bin.ToArray();

    /// <summary>Adds bytes as a buffer view (aligned to four bytes) and returns its index.</summary>
    public int View(ReadOnlySpan<byte> data, int stride = 0)
    {
        while (_bin.Length % 4 != 0)
        {
            _bin.WriteByte(0);
        }

        var offset = _bin.Length;
        _bin.Write(data);
        var strideJson = stride > 0 ? $",\"byteStride\":{stride}" : "";
        _views.Add(string.Create(CultureInfo.InvariantCulture, $"{{\"buffer\":0,\"byteOffset\":{offset},\"byteLength\":{data.Length}{strideJson}}}"));
        return _views.Count - 1;
    }

    /// <summary>Adds an accessor over an existing view (or no view when negative) and returns its index.</summary>
    public int Accessor(int view, int componentType, string type, int count, bool normalized = false, int byteOffset = 0, string extra = "")
    {
        var sb = new StringBuilder("{");
        if (view >= 0)
        {
            sb.Append(CultureInfo.InvariantCulture, $"\"bufferView\":{view},\"byteOffset\":{byteOffset},");
        }

        sb.Append(CultureInfo.InvariantCulture, $"\"componentType\":{componentType},\"type\":\"{type}\",\"count\":{count}");
        if (normalized)
        {
            sb.Append(",\"normalized\":true");
        }

        sb.Append(extra).Append('}');
        _accessors.Add(sb.ToString());
        return _accessors.Count - 1;
    }

    private static int Components(string type) => type switch
    {
        "SCALAR" => 1,
        "VEC2" => 2,
        "VEC3" => 3,
        "VEC4" or "MAT2" => 4,
        "MAT3" => 9,
        _ => 16,
    };

    /// <summary>Adds data as its own view and accessor.</summary>
    public int Data<T>(ReadOnlySpan<T> data, int componentType, string type, bool normalized = false)
        where T : unmanaged =>
        Accessor(View(MemoryMarshal.AsBytes(data)), componentType, type, data.Length / Components(type), normalized);

    public int Floats(string type, params float[] values) => Data<float>(values, Float, type);

    public int UShorts(string type, bool normalized, params ushort[] values) => Data<ushort>(values, UShort, type, normalized);

    public int Shorts(string type, bool normalized, params short[] values) => Data<short>(values, Short, type, normalized);

    public int UBytes(string type, bool normalized, params byte[] values) => Data<byte>(values, UByte, type, normalized);

    public int UInts(string type, params uint[] values) => Data<uint>(values, UInt, type);

    /// <summary>The complete JSON document around a body of top-level properties such as
    /// <c>"meshes":[...],"nodes":[...]</c>.</summary>
    public string Json(string body, string? bufferUri)
    {
        var sb = new StringBuilder("{\"asset\":{\"version\":\"2.0\"}");
        if (_bin.Length > 0)
        {
            var uri = bufferUri is null ? "" : $",\"uri\":\"{bufferUri}\"";
            sb.Append(CultureInfo.InvariantCulture, $",\"buffers\":[{{\"byteLength\":{_bin.Length}{uri}}}]");
            sb.Append(",\"bufferViews\":[").AppendJoin(',', _views).Append(']');
            sb.Append(",\"accessors\":[").AppendJoin(',', _accessors).Append(']');
        }

        if (body.Length > 0)
        {
            sb.Append(',').Append(body);
        }

        return sb.Append('}').ToString();
    }

    /// <summary>A <c>.gltf</c> file with the buffer embedded as a data URI.</summary>
    public byte[] Gltf(string body) =>
        Encoding.UTF8.GetBytes(Json(body, "data:application/octet-stream;base64," + Convert.ToBase64String(Buffer)));

    /// <summary>A <c>.glb</c> file.</summary>
    public byte[] Glb(string body)
    {
        var json = Encoding.UTF8.GetBytes(Json(body, null));
        var jsonLength = (json.Length + 3) & ~3;
        var bin = Buffer;
        var binLength = (bin.Length + 3) & ~3;
        var total = 12 + 8 + jsonLength + (bin.Length > 0 ? 8 + binLength : 0);
        var file = new byte[total];
        BinaryPrimitives.WriteUInt32LittleEndian(file, 0x46546C67);
        BinaryPrimitives.WriteUInt32LittleEndian(file.AsSpan(4), 2);
        BinaryPrimitives.WriteUInt32LittleEndian(file.AsSpan(8), (uint)total);
        BinaryPrimitives.WriteUInt32LittleEndian(file.AsSpan(12), (uint)jsonLength);
        BinaryPrimitives.WriteUInt32LittleEndian(file.AsSpan(16), 0x4E4F534A);
        file.AsSpan(20, jsonLength).Fill((byte)' ');
        json.CopyTo(file.AsSpan(20));
        if (bin.Length > 0)
        {
            var o = 20 + jsonLength;
            BinaryPrimitives.WriteUInt32LittleEndian(file.AsSpan(o), (uint)binLength);
            BinaryPrimitives.WriteUInt32LittleEndian(file.AsSpan(o + 4), 0x004E4942);
            bin.CopyTo(file.AsSpan(o + 8));
        }

        return file;
    }

    /// <summary>The usual test subject: one triangle with positions and 16-bit indices. Returns the body that shows
    /// it as mesh 0 on node 0 of scene 0.</summary>
    public string Triangle(string nodeExtra = "", string primitiveExtra = "")
    {
        var position = Floats("VEC3", 0, 0, 0, 1, 0, 0, 0, 1, 0);
        var index = UShorts("SCALAR", false, 0, 1, 2);
        return string.Create(
            CultureInfo.InvariantCulture,
            $"\"scene\":0,\"scenes\":[{{\"nodes\":[0]}}],\"nodes\":[{{\"mesh\":0,\"name\":\"tri\"{nodeExtra}}}],\"meshes\":[{{\"name\":\"triMesh\",\"primitives\":[{{\"attributes\":{{\"POSITION\":{position}}},\"indices\":{index}{primitiveExtra}}}]}}]");
    }
}
