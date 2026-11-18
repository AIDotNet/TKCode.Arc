using System.Buffers.Binary;
using System.Runtime.InteropServices;

namespace TKCode.Arc.Graphics3D;

/// <summary>How a meshopt-compressed buffer view is encoded.</summary>
public enum MeshoptMode
{
    /// <summary>Vertex or animation data: <c>count</c> elements of <c>stride</c> bytes.</summary>
    Attributes,

    /// <summary>A triangle list index buffer.</summary>
    Triangles,

    /// <summary>Any other index sequence.</summary>
    Indices,
}

/// <summary>What is applied to decoded attribute data.</summary>
public enum MeshoptFilter
{
    None,

    /// <summary>Unit vectors (normals, tangents) stored as octahedral coordinates.</summary>
    Octahedral,

    /// <summary>Unit quaternions stored as their three smallest components.</summary>
    Quaternion,

    /// <summary>Floats stored as mantissa and exponent.</summary>
    Exponential,
}

/// <summary>Decodes the meshopt compression used by the glTF extensions <c>EXT_meshopt_compression</c> and
/// <c>KHR_meshopt_compression</c> (vertex codec versions 0 and 1, index codec versions 0 and 1). The formats are those
/// of the meshoptimizer library by Arseny Kapoulkine (MIT).</summary>
public static class MeshoptDecoder
{
    private const int VertexBlockSizeBytes = 8192;
    private const int VertexBlockMaxSize = 256;
    private const int ByteGroupSize = 16;
    private const int ByteGroupDecodeLimit = 24;

    /// <summary>Decodes one compressed buffer view into <paramref name="count"/> elements of <paramref name="stride"/> bytes.</summary>
    public static byte[] Decode(ReadOnlySpan<byte> source, int count, int stride, MeshoptMode mode, MeshoptFilter filter = MeshoptFilter.None)
    {
        if (count < 0 || stride <= 0 || (long)count * stride > int.MaxValue)
        {
            throw new InvalidDataException("The meshopt view has an invalid size.");
        }

        var result = new byte[count * stride];
        switch (mode)
        {
            case MeshoptMode.Attributes:
                DecodeVertexBuffer(result, count, stride, source);
                break;
            case MeshoptMode.Triangles:
                DecodeIndexBuffer(result, count, stride, source);
                break;
            default:
                DecodeIndexSequence(result, count, stride, source);
                break;
        }

        switch (filter)
        {
            case MeshoptFilter.Octahedral:
                FilterOctahedral(result, count, stride);
                break;
            case MeshoptFilter.Quaternion:
                FilterQuaternion(result, count, stride);
                break;
            case MeshoptFilter.Exponential:
                FilterExponential(result, count, stride);
                break;
        }

        return result;
    }

    private static InvalidDataException Corrupt(string what) => new($"The meshopt {what} data is corrupt or truncated.");

    // ---- Vertex codec ----

    private static void DecodeVertexBuffer(Span<byte> destination, int vertexCount, int vertexSize, ReadOnlySpan<byte> buffer)
    {
        if (vertexSize is <= 0 or > 256 || vertexSize % 4 != 0)
        {
            throw new InvalidDataException("A meshopt attribute stride must be a multiple of 4 up to 256.");
        }

        if (buffer.Length < 1 || (buffer[0] & 0xF0) != 0xA0)
        {
            throw Corrupt("vertex");
        }

        var version = buffer[0] & 0x0F;
        if (version > 1)
        {
            throw new NotSupportedException($"meshopt vertex codec version {version} is not supported.");
        }

        var tailSize = vertexSize + (version == 0 ? 0 : vertexSize / 4);
        var tailPadded = Math.Max(tailSize, version == 0 ? 32 : 24);
        if (buffer.Length - 1 < tailPadded)
        {
            throw Corrupt("vertex");
        }

        var tail = buffer[^tailSize..];
        Span<byte> last = stackalloc byte[256];
        tail[..vertexSize].CopyTo(last);
        var channels = version == 0 ? default : tail[vertexSize..];
        var blockSize = Math.Min(VertexBlockSizeBytes / vertexSize & ~(ByteGroupSize - 1), VertexBlockMaxSize);
        var position = 1;
        var end = buffer.Length;
        var planes = new byte[VertexBlockMaxSize * 4];
        for (var offset = 0; offset < vertexCount; offset += blockSize)
        {
            var count = Math.Min(blockSize, vertexCount - offset);
            position = DecodeVertexBlock(buffer, position, end, destination.Slice(offset * vertexSize, count * vertexSize), count, vertexSize, last, channels, version, planes);
        }

        if (end - position != tailPadded)
        {
            throw Corrupt("vertex");
        }
    }

    private static int DecodeVertexBlock(
        ReadOnlySpan<byte> data, int position, int end, Span<byte> vertices, int count, int vertexSize, Span<byte> last, ReadOnlySpan<byte> channels, int version, byte[] planes)
    {
        var aligned = (count + ByteGroupSize - 1) & ~(ByteGroupSize - 1);
        var controlSize = version == 0 ? 0 : vertexSize / 4;
        if (end - position < controlSize)
        {
            throw Corrupt("vertex");
        }

        var control = data.Slice(position, controlSize);
        position += controlSize;
        for (var k = 0; k < vertexSize; k += 4)
        {
            var controlByte = version == 0 ? 0 : control[k / 4];
            for (var j = 0; j < 4; j++)
            {
                var plane = planes.AsSpan(j * VertexBlockMaxSize, aligned);
                var ctrl = (controlByte >> (j * 2)) & 3;
                if (version != 0 && ctrl == 3)
                {
                    // Stored as is.
                    if (end - position < count)
                    {
                        throw Corrupt("vertex");
                    }

                    data.Slice(position, count).CopyTo(plane);
                    position += count;
                }
                else if (version != 0 && ctrl == 2)
                {
                    plane.Clear();
                }
                else
                {
                    position = DecodeBytes(data, position, end, plane, version == 0 ? -1 : ctrl);
                }
            }

            var channel = version == 0 ? 0 : channels[k / 4];
            switch (channel & 3)
            {
                case 0:
                    // Each byte is the zigzag-coded difference to the same byte of the previous vertex.
                    for (var j = 0; j < 4; j++)
                    {
                        var plane = planes.AsSpan(j * VertexBlockMaxSize, count);
                        var p = last[k + j];
                        for (var i = 0; i < count; i++)
                        {
                            var v = plane[i];
                            p = (byte)(((v >> 1) ^ -(v & 1)) + p);
                            vertices[(i * vertexSize) + k + j] = p;
                        }
                    }

                    break;
                case 1:
                    // Two 16-bit values per group, each a zigzag-coded difference.
                    for (var j = 0; j < 4; j += 2)
                    {
                        var lo = planes.AsSpan(j * VertexBlockMaxSize, count);
                        var hi = planes.AsSpan((j + 1) * VertexBlockMaxSize, count);
                        var p = (ushort)(last[k + j] | (last[k + j + 1] << 8));
                        for (var i = 0; i < count; i++)
                        {
                            var v = (ushort)(lo[i] | (hi[i] << 8));
                            p = (ushort)(((v >> 1) ^ -(v & 1)) + p);
                            vertices[(i * vertexSize) + k + j] = (byte)p;
                            vertices[(i * vertexSize) + k + j + 1] = (byte)(p >> 8);
                        }
                    }

                    break;
                case 2:
                {
                    // One 32-bit value per group: rotated, then XOR with the previous vertex.
                    var rotation = (32 - (channel >> 4)) & 31;
                    var p = BinaryPrimitives.ReadUInt32LittleEndian(last.Slice(k, 4));
                    for (var i = 0; i < count; i++)
                    {
                        var v = (uint)(planes[i] | (planes[VertexBlockMaxSize + i] << 8) | (planes[(2 * VertexBlockMaxSize) + i] << 16) | (planes[(3 * VertexBlockMaxSize) + i] << 24));
                        v = ((v << rotation) | (v >> ((32 - rotation) & 31))) ^ p;
                        BinaryPrimitives.WriteUInt32LittleEndian(vertices.Slice((i * vertexSize) + k, 4), v);
                        p = v;
                    }

                    break;
                }

                default:
                    throw Corrupt("vertex");
            }
        }

        vertices.Slice((count - 1) * vertexSize, vertexSize).CopyTo(last);
        return position;
    }

    /// <summary>Decodes one byte plane: groups of 16 bytes, each stored with 0, 1, 2, 4 or 8 bits per byte as its
    /// two-bit header says; a value that does not fit is all ones, followed later by the full byte.</summary>
    private static int DecodeBytes(ReadOnlySpan<byte> data, int position, int end, Span<byte> buffer, int ctrl)
    {
        var headerSize = ((buffer.Length / ByteGroupSize) + 3) / 4;
        if (end - position < headerSize)
        {
            throw Corrupt("vertex");
        }

        var header = data.Slice(position, headerSize);
        position += headerSize;
        for (var i = 0; i < buffer.Length; i += ByteGroupSize)
        {
            if (end - position < ByteGroupDecodeLimit)
            {
                throw Corrupt("vertex");
            }

            var group = i / ByteGroupSize;
            var selector = (header[group / 4] >> (group % 4 * 2)) & 3;

            // Version 0: 0, 2, 4, 8 bits. Version 1: 0, 1, 2, 4 (control 0) or 1, 2, 4, 8 (control 1).
            var bits = ctrl < 0 ? selector switch
            {
                0 => 0,
                1 => 2,
                2 => 4,
                _ => 8,
            }
            : (selector + ctrl) switch
            {
                0 => 0,
                1 => 1,
                2 => 2,
                3 => 4,
                _ => 8,
            };
            var target = buffer.Slice(i, ByteGroupSize);
            switch (bits)
            {
                case 0:
                    target.Clear();
                    break;
                case 8:
                    data.Slice(position, ByteGroupSize).CopyTo(target);
                    position += ByteGroupSize;
                    break;
                case 1:
                {
                    // Two bytes of 8 one-bit values, lowest bit first.
                    var extra = position + 2;
                    for (var b = 0; b < 16; b++)
                    {
                        var value = (data[position + (b >> 3)] >> (b & 7)) & 1;
                        target[b] = value == 1 ? data[extra++] : (byte)0;
                    }

                    position = extra;
                    break;
                }

                default:
                {
                    var perByte = 8 / bits;
                    var sentinel = (1 << bits) - 1;
                    var extra = position + (ByteGroupSize / perByte);
                    for (var b = 0; b < ByteGroupSize; b++)
                    {
                        var packed = data[position + (b / perByte)];
                        var shift = 8 - bits - (b % perByte * bits);
                        var value = (packed >> shift) & sentinel;
                        target[b] = value == sentinel ? data[extra++] : (byte)value;
                    }

                    position = extra;
                    break;
                }
            }
        }

        return position;
    }

    // ---- Index codecs ----

    private static uint ReadVarint(ReadOnlySpan<byte> data, ref int position)
    {
        uint lead = data[position++];
        if (lead < 128)
        {
            return lead;
        }

        var result = lead & 127;
        var shift = 7;
        for (var i = 0; i < 4; i++)
        {
            uint group = data[position++];
            result |= (group & 127) << shift;
            shift += 7;
            if (group < 128)
            {
                break;
            }
        }

        return result;
    }

    private static uint ReadIndex(ReadOnlySpan<byte> data, ref int position, uint last)
    {
        var v = ReadVarint(data, ref position);
        return last + ((v >> 1) ^ (uint)-(int)(v & 1));
    }

    private static void WriteIndex(Span<byte> destination, int index, int size, uint value)
    {
        if (size == 2)
        {
            BinaryPrimitives.WriteUInt16LittleEndian(destination.Slice(index * 2, 2), (ushort)value);
        }
        else
        {
            BinaryPrimitives.WriteUInt32LittleEndian(destination.Slice(index * 4, 4), value);
        }
    }

    private static void DecodeIndexBuffer(Span<byte> destination, int indexCount, int indexSize, ReadOnlySpan<byte> buffer)
    {
        if (indexCount % 3 != 0 || indexSize is not (2 or 4))
        {
            throw new InvalidDataException("A meshopt triangle view holds 16- or 32-bit indices in multiples of three.");
        }

        if (buffer.Length < 1 + (indexCount / 3) + 16 || (buffer[0] & 0xF0) != 0xE0)
        {
            throw Corrupt("index");
        }

        var version = buffer[0] & 0x0F;
        if (version > 1)
        {
            throw new NotSupportedException($"meshopt index codec version {version} is not supported.");
        }

        Span<uint> edges = stackalloc uint[32];
        Span<uint> vertices = stackalloc uint[16];
        edges.Fill(uint.MaxValue);
        vertices.Fill(uint.MaxValue);
        int edgeOffset = 0, vertexOffset = 0;
        uint next = 0, last = 0;
        var freeLimit = version >= 1 ? 13 : 15;
        var code = 1;
        var data = code + (indexCount / 3);
        var safeEnd = buffer.Length - 16;
        var aux = buffer[safeEnd..];
        for (var i = 0; i < indexCount; i += 3)
        {
            if (data > safeEnd)
            {
                throw Corrupt("index");
            }

            var tri = buffer[code++];
            uint a, b, c;
            if (tri < 0xF0)
            {
                // Shares an edge with one of the last 16 triangles.
                var fe = tri >> 4;
                var slot = ((edgeOffset - 1 - fe) & 15) * 2;
                a = edges[slot];
                b = edges[slot + 1];
                var fec = tri & 15;
                if (fec < freeLimit)
                {
                    var fromFifo = vertices[(vertexOffset - 1 - fec) & 15];
                    c = fec == 0 ? next : fromFifo;
                    var isNew = fec == 0 ? 1 : 0;
                    next += (uint)isNew;
                    vertices[vertexOffset] = c;
                    vertexOffset = (vertexOffset + isNew) & 15;
                }
                else
                {
                    last = c = fec != 15 ? (uint)(last + (fec - (fec ^ 3))) : ReadIndex(buffer, ref data, last);
                    vertices[vertexOffset] = c;
                    vertexOffset = (vertexOffset + 1) & 15;
                }

                PushEdge(edges, ref edgeOffset, c, b);
                PushEdge(edges, ref edgeOffset, a, c);
            }
            else
            {
                if (tri < 0xFE)
                {
                    var auxCode = aux[tri & 15];
                    var feb = auxCode >> 4;
                    var fec = auxCode & 15;
                    a = next++;
                    var bf = vertices[(vertexOffset - feb) & 15];
                    b = feb == 0 ? next : bf;
                    var bNew = feb == 0 ? 1 : 0;
                    next += (uint)bNew;
                    var cf = vertices[(vertexOffset - fec) & 15];
                    c = fec == 0 ? next : cf;
                    var cNew = fec == 0 ? 1 : 0;
                    next += (uint)cNew;
                    PushVertex(vertices, ref vertexOffset, a, 1);
                    PushVertex(vertices, ref vertexOffset, b, bNew);
                    PushVertex(vertices, ref vertexOffset, c, cNew);
                }
                else
                {
                    var auxCode = buffer[data++];
                    var fea = tri == 0xFE ? 0 : 15;
                    var feb = auxCode >> 4;
                    var fec = auxCode & 15;
                    if (auxCode == 0)
                    {
                        next = 0;
                    }

                    a = fea == 0 ? next++ : 0;
                    b = feb == 0 ? next++ : vertices[(vertexOffset - feb) & 15];
                    c = fec == 0 ? next++ : vertices[(vertexOffset - fec) & 15];
                    if (fea == 15)
                    {
                        last = a = ReadIndex(buffer, ref data, last);
                    }

                    if (feb == 15)
                    {
                        last = b = ReadIndex(buffer, ref data, last);
                    }

                    if (fec == 15)
                    {
                        last = c = ReadIndex(buffer, ref data, last);
                    }

                    PushVertex(vertices, ref vertexOffset, a, 1);
                    PushVertex(vertices, ref vertexOffset, b, feb is 0 or 15 ? 1 : 0);
                    PushVertex(vertices, ref vertexOffset, c, fec is 0 or 15 ? 1 : 0);
                }

                PushEdge(edges, ref edgeOffset, b, a);
                PushEdge(edges, ref edgeOffset, c, b);
                PushEdge(edges, ref edgeOffset, a, c);
            }

            WriteIndex(destination, i, indexSize, a);
            WriteIndex(destination, i + 1, indexSize, b);
            WriteIndex(destination, i + 2, indexSize, c);
        }

        if (data != safeEnd)
        {
            throw Corrupt("index");
        }
    }

    private static void PushEdge(Span<uint> edges, ref int offset, uint a, uint b)
    {
        edges[offset * 2] = a;
        edges[(offset * 2) + 1] = b;
        offset = (offset + 1) & 15;
    }

    private static void PushVertex(Span<uint> vertices, ref int offset, uint v, int advance)
    {
        vertices[offset] = v;
        offset = (offset + advance) & 15;
    }

    private static void DecodeIndexSequence(Span<byte> destination, int indexCount, int indexSize, ReadOnlySpan<byte> buffer)
    {
        if (indexSize is not (2 or 4))
        {
            throw new InvalidDataException("A meshopt index view holds 16- or 32-bit indices.");
        }

        if (buffer.Length < 1 + indexCount + 4 || (buffer[0] & 0xF0) != 0xD0)
        {
            throw Corrupt("index");
        }

        if ((buffer[0] & 0x0F) > 1)
        {
            throw new NotSupportedException($"meshopt index sequence version {buffer[0] & 0x0F} is not supported.");
        }

        var data = 1;
        var safeEnd = buffer.Length - 4;
        Span<uint> last = stackalloc uint[2];
        for (var i = 0; i < indexCount; i++)
        {
            if (data >= safeEnd)
            {
                throw Corrupt("index");
            }

            var v = ReadVarint(buffer, ref data);
            var baseline = (int)(v & 1);
            v >>= 1;
            var delta = (v & 1) != 0 ? ~(v >> 1) : v >> 1;
            var index = last[baseline] + delta;
            last[baseline] = index;
            WriteIndex(destination, i, indexSize, index);
        }

        if (data != safeEnd)
        {
            throw Corrupt("index");
        }
    }

    // ---- Filters ----

    private static void FilterOctahedral(Span<byte> data, int count, int stride)
    {
        if (stride == 4)
        {
            var values = MemoryMarshal.Cast<byte, sbyte>(data);
            for (var i = 0; i < count; i++)
            {
                OctahedralDecode(values[i * 4], values[(i * 4) + 1], values[(i * 4) + 2], 127f, out var x, out var y, out var z);
                values[i * 4] = (sbyte)x;
                values[(i * 4) + 1] = (sbyte)y;
                values[(i * 4) + 2] = (sbyte)z;
            }
        }
        else if (stride == 8)
        {
            var values = MemoryMarshal.Cast<byte, short>(data);
            for (var i = 0; i < count; i++)
            {
                OctahedralDecode(values[i * 4], values[(i * 4) + 1], values[(i * 4) + 2], 32767f, out var x, out var y, out var z);
                values[i * 4] = (short)x;
                values[(i * 4) + 1] = (short)y;
                values[(i * 4) + 2] = (short)z;
            }
        }
        else
        {
            throw new InvalidDataException("The meshopt octahedral filter needs a stride of 4 or 8.");
        }
    }

    private static void OctahedralDecode(float x, float y, float one, float max, out int xo, out int yo, out int zo)
    {
        // The third stored value is "1" at the same precision: z is what is left of it.
        var z = one - MathF.Abs(x) - MathF.Abs(y);
        var t = z >= 0f ? 0f : z;
        x += x >= 0f ? t : -t;
        y += y >= 0f ? t : -t;
        var s = max / MathF.Sqrt((x * x) + (y * y) + (z * z));
        xo = (int)((x * s) + (x >= 0f ? 0.5f : -0.5f));
        yo = (int)((y * s) + (y >= 0f ? 0.5f : -0.5f));
        zo = (int)((z * s) + (z >= 0f ? 0.5f : -0.5f));
    }

    private static void FilterQuaternion(Span<byte> data, int count, int stride)
    {
        if (stride != 8)
        {
            throw new InvalidDataException("The meshopt quaternion filter needs a stride of 8.");
        }

        var values = MemoryMarshal.Cast<byte, short>(data);
        var scale = 1f / MathF.Sqrt(2f);
        for (var i = 0; i < count; i++)
        {
            // The last value holds the scale in its upper bits and the index of the dropped component in its lowest two.
            int packed = values[(i * 4) + 3];
            var ss = scale / (packed | 3);
            float x = values[i * 4] * ss, y = values[(i * 4) + 1] * ss, z = values[(i * 4) + 2] * ss;
            var ww = 1f - (x * x) - (y * y) - (z * z);
            var w = MathF.Sqrt(ww >= 0f ? ww : 0f);
            var largest = packed & 3;
            values[(i * 4) + ((largest + 1) & 3)] = (short)((x * 32767f) + (x >= 0f ? 0.5f : -0.5f));
            values[(i * 4) + ((largest + 2) & 3)] = (short)((y * 32767f) + (y >= 0f ? 0.5f : -0.5f));
            values[(i * 4) + ((largest + 3) & 3)] = (short)((z * 32767f) + (z >= 0f ? 0.5f : -0.5f));
            values[(i * 4) + largest] = (short)((w * 32767f) + 0.5f);
        }
    }

    private static void FilterExponential(Span<byte> data, int count, int stride)
    {
        if (stride % 4 != 0)
        {
            throw new InvalidDataException("The meshopt exponential filter needs a stride that is a multiple of 4.");
        }

        var values = MemoryMarshal.Cast<byte, uint>(data);
        for (var i = 0; i < values.Length; i++)
        {
            // 24 bits of signed mantissa, 8 bits of signed exponent: mantissa * 2^exponent.
            var v = values[i];
            var mantissa = (int)(v << 8) >> 8;
            var exponent = (int)v >> 24;
            values[i] = BitConverter.SingleToUInt32Bits(BitConverter.UInt32BitsToSingle((uint)(exponent + 127) << 23) * mantissa);
        }
    }
}
