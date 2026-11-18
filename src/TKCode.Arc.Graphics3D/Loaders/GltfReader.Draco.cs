using System.Buffers.Binary;
using System.Runtime.InteropServices;
using System.Text.Json;

namespace TKCode.Arc.Graphics3D;

// KHR_draco_mesh_compression: the indices and some attributes of a primitive come from one Draco buffer instead of
// their accessors' buffer views. The accessors still say what the data is (count, component type, bounds).
internal sealed partial class GltfReader
{
    // Accessors of the primitive being read whose data was decoded from its Draco buffer.
    private Dictionary<int, AccessorData>? _decodedAccessors;

    private Dictionary<int, AccessorData> DecodeDraco(JsonElement primitive, JsonElement extension, string meshName)
    {
        var data = GetBufferView(GetInt(extension, "bufferView", -1), out _);
        DracoMesh mesh;
        try
        {
            mesh = DracoDecoder.Decode(data.Span);
        }
        catch (InvalidDataException ex)
        {
            throw new InvalidDataException($"The Draco data of mesh '{meshName}' could not be decoded: {ex.Message}", ex);
        }

        var decoded = new Dictionary<int, AccessorData>();
        if (TryGet(extension, "attributes", out var ids) && ids.ValueKind == JsonValueKind.Object
            && TryGet(primitive, "attributes", out var attributes) && attributes.ValueKind == JsonValueKind.Object)
        {
            foreach (var entry in ids.EnumerateObject())
            {
                if (entry.Value.ValueKind != JsonValueKind.Number || GetIntOrNull(attributes, entry.Name) is not { } accessorIndex)
                {
                    continue;
                }

                if (mesh.FindAttribute(entry.Value.GetInt32()) is not { } attribute)
                {
                    throw new InvalidDataException($"The Draco data of mesh '{meshName}' has no attribute {entry.Value.GetInt32()} ({entry.Name}).");
                }

                decoded[accessorIndex] = FromDraco(accessorIndex, attribute, mesh.VertexCount, entry.Name, meshName);
            }
        }

        if (GetIntOrNull(primitive, "indices") is { } indexAccessor)
        {
            decoded[indexAccessor] = new AccessorData
            {
                Bytes = MemoryMarshal.AsBytes(mesh.Indices.AsSpan()).ToArray(),
                Count = mesh.Indices.Length,
                Components = 1,
                ComponentType = TypeUInt,
                Normalized = false,
            };
        }

        return decoded;
    }

    /// <summary>The decoded attribute in the component type its accessor declares (Draco may hold it in another).</summary>
    private AccessorData FromDraco(int accessorIndex, DracoAttribute attribute, int vertexCount, string semantic, string meshName)
    {
        if ((uint)accessorIndex >= (uint)_accessors.Length)
        {
            throw new InvalidDataException($"The glTF file refers to accessor {accessorIndex}, which does not exist.");
        }

        var accessor = _accessors[accessorIndex];
        var componentType = GetInt(accessor, "componentType", TypeFloat);
        var components = GetString(accessor, "type") switch
        {
            "SCALAR" => 1,
            "VEC2" => 2,
            "VEC3" => 3,
            "VEC4" => 4,
            var other => throw new InvalidDataException($"glTF accessor type '{other}' cannot hold Draco data."),
        };
        if (attribute.ComponentCount != components)
        {
            throw new InvalidDataException($"Attribute {semantic} of mesh '{meshName}' has {attribute.ComponentCount} components in its Draco data but {components} in its accessor.");
        }

        var sourceType = attribute.DataType switch
        {
            DracoDataType.Int8 => TypeByte,
            DracoDataType.UInt8 => TypeUByte,
            DracoDataType.Int16 => TypeShort,
            DracoDataType.UInt16 => TypeUShort,
            DracoDataType.UInt32 => TypeUInt,
            DracoDataType.Float32 => TypeFloat,
            _ => 0,
        };
        var values = vertexCount * components;
        byte[] bytes;
        if (sourceType == componentType)
        {
            bytes = attribute.Data;
        }
        else
        {
            bytes = new byte[values * ComponentSize(componentType)];
            var source = attribute.Data.AsSpan();
            for (var i = 0; i < values; i++)
            {
                double value = attribute.DataType switch
                {
                    DracoDataType.Int8 => (sbyte)source[i],
                    DracoDataType.UInt8 => source[i],
                    DracoDataType.Int16 => BinaryPrimitives.ReadInt16LittleEndian(source[(i * 2)..]),
                    DracoDataType.UInt16 => BinaryPrimitives.ReadUInt16LittleEndian(source[(i * 2)..]),
                    DracoDataType.Int32 => BinaryPrimitives.ReadInt32LittleEndian(source[(i * 4)..]),
                    DracoDataType.UInt32 => BinaryPrimitives.ReadUInt32LittleEndian(source[(i * 4)..]),
                    _ => BinaryPrimitives.ReadSingleLittleEndian(source[(i * 4)..]),
                };
                var target = bytes.AsSpan(i * ComponentSize(componentType));
                switch (componentType)
                {
                    case TypeByte:
                        target[0] = (byte)(sbyte)Math.Clamp(value, sbyte.MinValue, sbyte.MaxValue);
                        break;
                    case TypeUByte:
                        target[0] = (byte)Math.Clamp(value, byte.MinValue, byte.MaxValue);
                        break;
                    case TypeShort:
                        BinaryPrimitives.WriteInt16LittleEndian(target, (short)Math.Clamp(value, short.MinValue, short.MaxValue));
                        break;
                    case TypeUShort:
                        BinaryPrimitives.WriteUInt16LittleEndian(target, (ushort)Math.Clamp(value, ushort.MinValue, ushort.MaxValue));
                        break;
                    case TypeUInt:
                        BinaryPrimitives.WriteUInt32LittleEndian(target, (uint)Math.Clamp(value, uint.MinValue, uint.MaxValue));
                        break;
                    default:
                        BinaryPrimitives.WriteSingleLittleEndian(target, (float)value);
                        break;
                }
            }
        }

        return new AccessorData
        {
            Bytes = bytes,
            Count = vertexCount,
            Components = components,
            ComponentType = componentType,
            Normalized = GetBool(accessor, "normalized", false),
        };
    }
}
