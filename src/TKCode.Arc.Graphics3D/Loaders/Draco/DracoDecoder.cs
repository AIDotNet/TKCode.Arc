// Decoder for Draco compressed triangle meshes, as used by the glTF extension KHR_draco_mesh_compression.
//
// The bitstream format and the decoding algorithms follow Draco (https://github.com/google/draco),
// Copyright Google, licensed under the Apache License 2.0, and its bitstream specification
// (https://google.github.io/draco/spec/). This is an independent C# implementation of that format.

using System;
using System.Collections.Generic;
using System.IO;

namespace TKCode.Arc.Graphics3D;

/// <summary>What an attribute of a Draco mesh holds.</summary>
public enum DracoAttributeType
{
    /// <summary>Vertex positions.</summary>
    Position,

    /// <summary>Vertex normals.</summary>
    Normal,

    /// <summary>Vertex colors.</summary>
    Color,

    /// <summary>Texture coordinates.</summary>
    TexCoord,

    /// <summary>Anything else (tangents, joints, weights, application data).</summary>
    Generic,
}

/// <summary>Component type of a Draco attribute.</summary>
public enum DracoDataType
{
    /// <summary>Signed 8-bit integer.</summary>
    Int8,

    /// <summary>Unsigned 8-bit integer.</summary>
    UInt8,

    /// <summary>Signed 16-bit integer.</summary>
    Int16,

    /// <summary>Unsigned 16-bit integer.</summary>
    UInt16,

    /// <summary>Signed 32-bit integer.</summary>
    Int32,

    /// <summary>Unsigned 32-bit integer.</summary>
    UInt32,

    /// <summary>32-bit floating point number.</summary>
    Float32,
}

/// <summary>One attribute of a decoded Draco mesh, with one entry per point.</summary>
public sealed class DracoAttribute
{
    internal DracoAttribute(DracoAttributeData source, byte[] data)
    {
        UniqueId = source.UniqueId;
        Type = source.Type;
        DataType = source.DataType;
        ComponentCount = source.NumComponents;
        Normalized = source.Normalized;
        Data = data;
    }

    /// <summary>The id the glTF extension's attribute map refers to.</summary>
    public int UniqueId { get; }

    /// <summary>What the attribute holds.</summary>
    public DracoAttributeType Type { get; }

    /// <summary>Type of each component in <see cref="Data"/>.</summary>
    public DracoDataType DataType { get; }

    /// <summary>Number of components per point.</summary>
    public int ComponentCount { get; }

    /// <summary>The "normalized" flag stored with the attribute (integers that stand for 0..1 or -1..1).</summary>
    public bool Normalized { get; }

    /// <summary>Vertex count times <see cref="ComponentCount"/> values of <see cref="DataType"/>, little endian, in point order.</summary>
    public byte[] Data { get; }
}

/// <summary>A decoded Draco triangle mesh.</summary>
public sealed class DracoMesh
{
    private readonly DracoAttribute[] _attributes;

    internal DracoMesh(int vertexCount, uint[] indices, DracoAttribute[] attributes)
    {
        VertexCount = vertexCount;
        Indices = indices;
        _attributes = attributes;
    }

    /// <summary>Number of points; every attribute has this many entries.</summary>
    public int VertexCount { get; }

    /// <summary>Point indices, three per triangle.</summary>
    public uint[] Indices { get; }

    /// <summary>The attributes, in the order the file stores them.</summary>
    public IReadOnlyList<DracoAttribute> Attributes => _attributes;

    /// <summary>Returns the attribute with the given unique id, or null.</summary>
    public DracoAttribute? FindAttribute(int uniqueId)
    {
        foreach (DracoAttribute attribute in _attributes)
        {
            if (attribute.UniqueId == uniqueId)
            {
                return attribute;
            }
        }

        return null;
    }
}

/// <summary>Decodes Draco compressed triangle meshes.</summary>
public static class DracoDecoder
{
    /// <summary>
    /// Decodes one Draco buffer holding a triangle mesh. Throws <see cref="InvalidDataException"/> for damaged
    /// data and <see cref="NotSupportedException"/> for parts of the format that are not implemented.
    /// </summary>
    public static DracoMesh Decode(ReadOnlySpan<byte> data)
    {
        try
        {
            return DracoMeshDecoder.Decode(data.ToArray());
        }
        catch (Exception exception) when (exception is IndexOutOfRangeException or ArgumentException or ArithmeticException)
        {
            // The decoder checks what it reads. This is the net under those checks, so that damaged data
            // can never surface as anything but InvalidDataException.
            throw new InvalidDataException("Draco: the data is damaged.", exception);
        }
    }
}

/// <summary>State of one decode: header, connectivity, then the attributes.</summary>
internal sealed class DracoMeshDecoder
{
    private const int SequentialEncoding = 0;
    private const int EdgebreakerEncoding = 1;
    private const int MetadataFlag = 0x8000;
    private const int MaxMetadataDepth = 1000;

    private DracoMeshDecoder(byte[] data)
    {
        Buffer = new DracoBuffer(data);
    }

    public DracoBuffer Buffer { get; }

    /// <summary>The point of every corner, three per face.</summary>
    public int[] Faces { get; private set; } = Array.Empty<int>();

    public int NumPoints { get; private set; }

    /// <summary>All attributes, in file order.</summary>
    public List<DracoAttributeData> Attributes { get; } = new();

    /// <summary>Decodes without the safety net of <see cref="DracoDecoder.Decode"/>; tests use it to see what the net catches.</summary>
    public static DracoMesh Decode(byte[] data)
    {
        return Decode(data, out _);
    }

    /// <summary>As <see cref="Decode(byte[])"/>, and also hands out the attributes' decoding state (quantization and so on).</summary>
    public static DracoMesh Decode(byte[] data, out IReadOnlyList<DracoAttributeData> attributes)
    {
        DracoMeshDecoder decoder = new(data);
        attributes = decoder.Attributes;
        return decoder.Decode();
    }

    private DracoMesh Decode()
    {
        DracoBuffer buffer = Buffer;
        ReadOnlySpan<byte> magic = buffer.Take(5);
        if (!magic.SequenceEqual("DRACO"u8))
        {
            throw DracoError.Invalid("not a Draco file.");
        }

        byte major = buffer.ReadByte();
        byte minor = buffer.ReadByte();
        byte geometryType = buffer.ReadByte();
        byte method = buffer.ReadByte();
        ushort flags = buffer.ReadUInt16();
        if (geometryType == 0)
        {
            throw DracoError.Unsupported("point clouds are not supported.");
        }

        if (geometryType != 1)
        {
            throw DracoError.Invalid("unknown geometry type.");
        }

        // Mesh bitstreams 1.0 to 2.2 exist; 2.2 is what every glTF exporter writes.
        if (major < 1 || major > 2 || (major == 2 && minor > 2))
        {
            throw DracoError.Unsupported($"bitstream version {major}.{minor} is not supported.");
        }

        buffer.Version = (major << 8) | minor;
        DracoTrace.Add($"version.{major}.{minor}");
        if (buffer.Version >= DracoVersion.V1_3 && (flags & MetadataFlag) != 0)
        {
            DracoTrace.Add("metadata");
            SkipMetadata();
        }

        DracoEdgebreakerDecoder? edgebreaker = null;
        if (method == SequentialEncoding)
        {
            DecodeSequentialConnectivity();
        }
        else if (method == EdgebreakerEncoding)
        {
            edgebreaker = new DracoEdgebreakerDecoder(buffer);
            edgebreaker.DecodeConnectivity();
            Faces = edgebreaker.Faces;
            NumPoints = edgebreaker.NumPoints;
        }
        else
        {
            throw DracoError.Invalid("unknown mesh encoding method.");
        }

        int numDecoders = buffer.ReadByte();
        DracoAttributesDecoder[] decoders = new DracoAttributesDecoder[numDecoders];
        for (int i = 0; i < numDecoders; i++)
        {
            decoders[i] = new DracoAttributesDecoder(this);
            edgebreaker?.ConfigureAttributesDecoder(i, decoders[i]);
        }

        foreach (DracoAttributesDecoder decoder in decoders)
        {
            decoder.DecodeDescriptors();
        }

        foreach (DracoAttributesDecoder decoder in decoders)
        {
            decoder.DecodeAttributes();
        }

        return BuildMesh();
    }

    private DracoMesh BuildMesh()
    {
        int numPoints = NumPoints;
        int[] faces = Faces;
        uint[] indices = Buffer.Limits.UInts(faces.Length);
        for (int i = 0; i < faces.Length; i++)
        {
            int point = faces[i];
            if ((uint)point >= (uint)numPoints)
            {
                throw DracoError.Invalid("a face refers to a point that does not exist.");
            }

            indices[i] = (uint)point;
        }

        DracoAttribute[] attributes = new DracoAttribute[Attributes.Count];
        for (int i = 0; i < attributes.Length; i++)
        {
            attributes[i] = new DracoAttribute(Attributes[i], ExpandToPoints(Attributes[i], numPoints, Buffer.Limits));
        }

        return new DracoMesh(numPoints, indices, attributes);
    }

    // Points that differ in another attribute may share a value of this one; give every point its own copy.
    private static byte[] ExpandToPoints(DracoAttributeData attribute, int numPoints, DracoLimits limits)
    {
        byte[] values = attribute.Values ?? throw DracoError.Invalid("an attribute was not decoded.");
        int stride = attribute.ByteStride;
        int numEntries = attribute.NumEntries;
        int[]? map = attribute.PointToValue;
        if (map == null)
        {
            if (numEntries != numPoints)
            {
                throw DracoError.Invalid("an attribute does not have one value per point.");
            }

            return values;
        }

        byte[] data = limits.Bytes((long)numPoints * stride);
        for (int point = 0; point < numPoints; point++)
        {
            int entry = map[point];
            if ((uint)entry >= (uint)numEntries)
            {
                throw DracoError.Invalid("a point refers to an attribute value that does not exist.");
            }

            values.AsSpan(entry * stride, stride).CopyTo(data.AsSpan(point * stride, stride));
        }

        return data;
    }

    private void DecodeSequentialConnectivity()
    {
        DracoBuffer buffer = Buffer;
        uint numFaces = buffer.ReadCount(DracoVersion.V2_2);
        uint numPoints = buffer.ReadCount(DracoVersion.V2_2);

        // The reference decoder's rule: an index takes at least a byte, compressed or not.
        if (numFaces > (uint)(buffer.Remaining / 3))
        {
            throw DracoError.Invalid("more faces than the file can hold.");
        }

        // Per face the corners' points and the final indices; a point is worth nothing without four bytes of some attribute.
        DracoLimits limits = buffer.Limits;
        limits.CheckDeclared(numFaces, 6 * sizeof(int));
        limits.CheckDeclared(numPoints, sizeof(int));
        int numIndices = (int)numFaces * 3;
        int[] faces;
        byte connectivityMethod = buffer.ReadByte();
        if (connectivityMethod == 0)
        {
            // Differences to the previous index, sign in the lowest bit, entropy coded.
            DracoTrace.Add("sequential.compressedIndices");
            faces = DracoSymbols.Decode(buffer, numIndices, 1);
            int last = 0;
            for (int i = 0; i < faces.Length; i++)
            {
                uint encoded = unchecked((uint)faces[i]);
                int difference = (int)(encoded >> 1);
                if ((encoded & 1) != 0)
                {
                    if (difference > last)
                    {
                        throw DracoError.Invalid("a face index is negative.");
                    }

                    difference = -difference;
                }
                else if (difference > int.MaxValue - last)
                {
                    throw DracoError.Invalid("a face index overflows.");
                }

                last += difference;
                faces[i] = last;
            }
        }
        else
        {
            int size;
            if (numPoints < 256)
            {
                size = 1;
                DracoTrace.Add("sequential.indices8");
            }
            else if (numPoints < (1 << 16))
            {
                size = 2;
                DracoTrace.Add("sequential.indices16");
            }
            else if (numPoints < (1 << 21) && buffer.Version >= DracoVersion.V2_2)
            {
                size = 0;
                DracoTrace.Add("sequential.indicesVarint");
            }
            else
            {
                size = 4;
                DracoTrace.Add("sequential.indices32");
            }

            if ((long)numIndices * Math.Max(size, 1) > buffer.Remaining)
            {
                throw DracoError.Invalid("more faces than the file can hold.");
            }

            faces = limits.Ints(numIndices);
            for (int i = 0; i < numIndices; i++)
            {
                uint value = size switch
                {
                    1 => buffer.ReadByte(),
                    2 => buffer.ReadUInt16(),
                    4 => buffer.ReadUInt32(),
                    _ => buffer.ReadVarUInt32(),
                };
                faces[i] = unchecked((int)value);
            }
        }

        foreach (int index in faces)
        {
            if ((uint)index >= numPoints)
            {
                throw DracoError.Invalid("a face refers to a point that does not exist.");
            }
        }

        Faces = faces;
        NumPoints = (int)numPoints;
    }

    // Metadata is application data (names and so on); the mesh does not depend on it.
    private void SkipMetadata()
    {
        DracoBuffer buffer = Buffer;
        uint numAttributeMetadata = buffer.ReadVarUInt32();
        for (uint i = 0; i < numAttributeMetadata; i++)
        {
            buffer.ReadVarUInt32();
            SkipMetadataTree();
        }

        SkipMetadataTree();
    }

    private void SkipMetadataTree()
    {
        // Nested metadata is walked with an explicit stack of nesting levels; -1 marks the root.
        DracoBuffer buffer = Buffer;
        List<int> pending = new() { -1 };
        while (pending.Count > 0)
        {
            int level = pending[pending.Count - 1];
            pending.RemoveAt(pending.Count - 1);
            if (level >= 0)
            {
                if (level > MaxMetadataDepth)
                {
                    throw DracoError.Invalid("metadata is nested too deeply.");
                }

                buffer.Skip(buffer.ReadByte());
            }

            uint numEntries = buffer.ReadVarUInt32();
            for (uint i = 0; i < numEntries; i++)
            {
                buffer.Skip(buffer.ReadByte());
                uint size = buffer.ReadVarUInt32();
                if (size == 0)
                {
                    throw DracoError.Invalid("a metadata entry is empty.");
                }

                buffer.Skip(size);
            }

            uint numChildren = buffer.ReadVarUInt32();
            if (numChildren > (uint)buffer.Remaining)
            {
                throw DracoError.Invalid("more metadata than the file can hold.");
            }

            for (uint i = 0; i < numChildren; i++)
            {
                pending.Add(level + 1);
            }
        }
    }
}
