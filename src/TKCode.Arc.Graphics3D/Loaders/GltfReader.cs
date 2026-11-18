using System.Buffers.Binary;
using System.Runtime.InteropServices;
using System.Text.Json;

namespace TKCode.Arc.Graphics3D;

/// <summary>Reads one glTF document into scene objects. The parts: this file (container, buffers, accessors, JSON
/// helpers), materials and textures, meshes, and nodes with skins, cameras, lights and animations.</summary>
internal sealed partial class GltfReader : IDisposable
{
    private const uint GlbMagic = 0x46546C67;
    private const uint ChunkJson = 0x4E4F534A;
    private const uint ChunkBin = 0x004E4942;

    private static readonly string[] s_supportedExtensions =
    [
        "KHR_materials_unlit", "KHR_materials_emissive_strength", "KHR_materials_clearcoat", "KHR_materials_sheen",
        "KHR_materials_ior", "KHR_materials_specular", "KHR_materials_transmission", "KHR_materials_volume",
        "KHR_texture_transform", "KHR_lights_punctual", "KHR_mesh_quantization",
        "EXT_meshopt_compression", "KHR_meshopt_compression", "KHR_materials_iridescence", "KHR_materials_anisotropy",
        "KHR_animation_pointer", "KHR_draco_mesh_compression", "KHR_texture_basisu",
    ];

    private readonly Dictionary<int, ReadOnlyMemory<byte>> _decodedViews = [];

    private readonly JsonDocument _document;
    private readonly JsonElement _root;
    private readonly ReadOnlyMemory<byte> _bin;
    private readonly bool _hasBin;
    private readonly Func<string, byte[]?>? _resolve;
    private readonly List<string> _warnings = [];
    private readonly JsonElement[] _buffers;
    private readonly JsonElement[] _bufferViews;
    private readonly JsonElement[] _accessors;
    private readonly ReadOnlyMemory<byte>?[] _bufferData;

    public GltfReader(ReadOnlyMemory<byte> data, Func<string, byte[]?>? resolve)
    {
        _resolve = resolve;
        var span = data.Span;
        ReadOnlyMemory<byte> json;
        if (span.Length >= 12 && BinaryPrimitives.ReadUInt32LittleEndian(span) == GlbMagic)
        {
            var version = BinaryPrimitives.ReadUInt32LittleEndian(span[4..]);
            if (version != 2)
            {
                throw new NotSupportedException($"GLB container version {version} is not supported (only 2).");
            }

            var total = (int)Math.Min(BinaryPrimitives.ReadUInt32LittleEndian(span[8..]), (uint)span.Length);
            json = default;
            var position = 12;
            var sawJson = false;
            while (position + 8 <= total)
            {
                var length = (long)BinaryPrimitives.ReadUInt32LittleEndian(span[position..]);
                var type = BinaryPrimitives.ReadUInt32LittleEndian(span[(position + 4)..]);
                position += 8;
                if (position + length > total)
                {
                    throw new InvalidDataException("The GLB file is truncated.");
                }

                if (type == ChunkJson && !sawJson)
                {
                    json = data.Slice(position, (int)length);
                    sawJson = true;
                }
                else if (type == ChunkBin && !_hasBin)
                {
                    _bin = data.Slice(position, (int)length);
                    _hasBin = true;
                }

                position += (int)((length + 3) & ~3L);
            }

            if (!sawJson)
            {
                throw new InvalidDataException("The GLB file has no JSON chunk.");
            }
        }
        else
        {
            json = data;
            if (span.Length >= 3 && span[0] == 0xEF && span[1] == 0xBB && span[2] == 0xBF)
            {
                json = data[3..];
            }
        }

        try
        {
            _document = JsonDocument.Parse(json, new JsonDocumentOptions { AllowTrailingCommas = true, CommentHandling = JsonCommentHandling.Skip });
        }
        catch (JsonException e)
        {
            throw new InvalidDataException("The data is neither a GLB file nor glTF JSON: " + e.Message, e);
        }

        _root = _document.RootElement;
        if (_root.ValueKind != JsonValueKind.Object)
        {
            _document.Dispose();
            throw new InvalidDataException("The glTF document is not a JSON object.");
        }

        _buffers = Elements(_root, "buffers");
        _bufferViews = Elements(_root, "bufferViews");
        _accessors = Elements(_root, "accessors");
        _bufferData = new ReadOnlyMemory<byte>?[_buffers.Length];
    }

    public void Dispose() => _document.Dispose();

    public GltfAsset Read()
    {
        if (TryGet(_root, "asset", out var asset))
        {
            var version = GetString(asset, "version") ?? "2.0";
            if (!version.StartsWith("2.", StringComparison.Ordinal) && version != "2")
            {
                throw new NotSupportedException($"glTF version {version} is not supported (only 2.x).");
            }
        }

        foreach (var required in Elements(_root, "extensionsRequired"))
        {
            var name = required.GetString() ?? "";
            if (Array.IndexOf(s_supportedExtensions, name) < 0)
            {
                throw new NotSupportedException($"The glTF file requires the extension {name}, which is not supported.");
            }
        }

        foreach (var used in Elements(_root, "extensionsUsed"))
        {
            var name = used.GetString() ?? "";
            if (Array.IndexOf(s_supportedExtensions, name) < 0)
            {
                Warn($"Extension {name} is not supported and was ignored.");
            }
        }

        return BuildScenes();
    }

    private void Warn(string message)
    {
        if (!_warnings.Contains(message))
        {
            _warnings.Add(message);
        }
    }

    // ---- JSON helpers ----

    private static bool TryGet(JsonElement element, string name, out JsonElement value)
    {
        if (element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out value) && value.ValueKind != JsonValueKind.Null)
        {
            return true;
        }

        value = default;
        return false;
    }

    private static JsonElement[] Elements(JsonElement element, string name)
    {
        if (!TryGet(element, name, out var array) || array.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        var result = new JsonElement[array.GetArrayLength()];
        var i = 0;
        foreach (var item in array.EnumerateArray())
        {
            result[i++] = item;
        }

        return result;
    }

    private static int? GetIntOrNull(JsonElement element, string name)
    {
        if (!TryGet(element, name, out var value) || value.ValueKind != JsonValueKind.Number)
        {
            return null;
        }

        return value.TryGetInt32(out var i) ? i : (int)value.GetDouble();
    }

    private static int GetInt(JsonElement element, string name, int fallback) => GetIntOrNull(element, name) ?? fallback;

    private static float GetFloat(JsonElement element, string name, float fallback) =>
        TryGet(element, name, out var value) && value.ValueKind == JsonValueKind.Number ? (float)value.GetDouble() : fallback;

    private static float? GetFloatOrNull(JsonElement element, string name) =>
        TryGet(element, name, out var value) && value.ValueKind == JsonValueKind.Number ? (float)value.GetDouble() : null;

    private static bool GetBool(JsonElement element, string name, bool fallback) =>
        TryGet(element, name, out var value) && value.ValueKind is JsonValueKind.True or JsonValueKind.False ? value.GetBoolean() : fallback;

    private static string? GetString(JsonElement element, string name) =>
        TryGet(element, name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

    /// <summary>A number array with at least <paramref name="minimum"/> entries, or null.</summary>
    private static float[]? GetFloats(JsonElement element, string name, int minimum)
    {
        if (!TryGet(element, name, out var array) || array.ValueKind != JsonValueKind.Array || array.GetArrayLength() < minimum)
        {
            return null;
        }

        var result = new float[array.GetArrayLength()];
        var i = 0;
        foreach (var item in array.EnumerateArray())
        {
            result[i++] = item.ValueKind == JsonValueKind.Number ? (float)item.GetDouble() : 0f;
        }

        return result;
    }

    private static bool TryGetExtension(JsonElement element, string name, out JsonElement extension)
    {
        if (TryGet(element, "extensions", out var extensions) && TryGet(extensions, name, out extension) && extension.ValueKind == JsonValueKind.Object)
        {
            return true;
        }

        extension = default;
        return false;
    }

    // ---- Buffers ----

    private static byte[]? DecodeDataUri(string uri)
    {
        if (!uri.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var comma = uri.IndexOf(',');
        if (comma < 0)
        {
            throw new InvalidDataException("A data URI in the glTF file is malformed.");
        }

        var header = uri.AsSpan(5, comma - 5);
        var payload = uri[(comma + 1)..];
        return header.EndsWith(";base64", StringComparison.OrdinalIgnoreCase)
            ? Convert.FromBase64String(payload)
            : System.Text.Encoding.UTF8.GetBytes(Uri.UnescapeDataString(payload));
    }

    /// <summary>The bytes behind a URI: embedded, or fetched through the resolver.</summary>
    private byte[] ResolveUri(string uri, string what)
    {
        if (DecodeDataUri(uri) is { } embedded)
        {
            return embedded;
        }

        var unescaped = Uri.UnescapeDataString(uri);
        var bytes = _resolve?.Invoke(unescaped);
        if (bytes is null)
        {
            throw new FileNotFoundException(
                _resolve is null
                    ? $"The glTF file refers to the external {what} '{unescaped}'; load it from a path or pass a resolver."
                    : $"The {what} '{unescaped}' that the glTF file refers to was not found.",
                unescaped);
        }

        return bytes;
    }

    private ReadOnlyMemory<byte> GetBuffer(int index)
    {
        if ((uint)index >= (uint)_buffers.Length)
        {
            throw new InvalidDataException($"The glTF file refers to buffer {index}, which does not exist.");
        }

        if (_bufferData[index] is { } cached)
        {
            return cached;
        }

        ReadOnlyMemory<byte> data;
        var uri = GetString(_buffers[index], "uri");
        if (uri is null)
        {
            if (index != 0 || !_hasBin)
            {
                throw new InvalidDataException($"glTF buffer {index} has no URI and the file has no binary chunk.");
            }

            data = _bin;
        }
        else
        {
            data = ResolveUri(uri, "buffer");
        }

        _bufferData[index] = data;
        return data;
    }

    /// <summary>The bytes of a buffer view (without regard to its stride).</summary>
    private ReadOnlyMemory<byte> GetBufferView(int index, out int stride)
    {
        if ((uint)index >= (uint)_bufferViews.Length)
        {
            throw new InvalidDataException($"The glTF file refers to buffer view {index}, which does not exist.");
        }

        var view = _bufferViews[index];
        stride = GetInt(view, "byteStride", 0);

        // meshopt: the view's own buffer is a placeholder; its bytes come from decoding another range.
        if (TryGetExtension(view, "KHR_meshopt_compression", out var meshopt) || TryGetExtension(view, "EXT_meshopt_compression", out meshopt))
        {
            if (!_decodedViews.TryGetValue(index, out var decoded))
            {
                decoded = DecodeMeshoptView(index, meshopt);
                _decodedViews.Add(index, decoded);
            }

            return decoded;
        }

        var buffer = GetBuffer(GetInt(view, "buffer", -1));
        var offset = GetInt(view, "byteOffset", 0);
        var length = GetInt(view, "byteLength", 0);
        if (offset < 0 || length < 0 || (long)offset + length > buffer.Length)
        {
            throw new InvalidDataException($"glTF buffer view {index} reaches outside its buffer.");
        }

        return buffer.Slice(offset, length);
    }

    private byte[] DecodeMeshoptView(int index, JsonElement meshopt)
    {
        var source = GetBuffer(GetInt(meshopt, "buffer", -1));
        var offset = GetInt(meshopt, "byteOffset", 0);
        var length = GetInt(meshopt, "byteLength", 0);
        if (offset < 0 || length < 0 || (long)offset + length > source.Length)
        {
            throw new InvalidDataException($"The compressed data of glTF buffer view {index} reaches outside its buffer.");
        }

        var mode = GetString(meshopt, "mode") switch
        {
            "ATTRIBUTES" => MeshoptMode.Attributes,
            "TRIANGLES" => MeshoptMode.Triangles,
            "INDICES" => MeshoptMode.Indices,
            var other => throw new NotSupportedException($"meshopt mode '{other}' of buffer view {index} is not supported."),
        };
        var filter = GetString(meshopt, "filter") switch
        {
            null or "NONE" => MeshoptFilter.None,
            "OCTAHEDRAL" => MeshoptFilter.Octahedral,
            "QUATERNION" => MeshoptFilter.Quaternion,
            "EXPONENTIAL" => MeshoptFilter.Exponential,
            var other => throw new NotSupportedException($"meshopt filter '{other}' of buffer view {index} is not supported."),
        };
        return MeshoptDecoder.Decode(source.Span.Slice(offset, length), GetInt(meshopt, "count", 0), GetInt(meshopt, "byteStride", 0), mode, filter);
    }

    // ---- Accessors ----

    private const int TypeByte = 5120;
    private const int TypeUByte = 5121;
    private const int TypeShort = 5122;
    private const int TypeUShort = 5123;
    private const int TypeUInt = 5125;
    private const int TypeFloat = 5126;

    /// <summary>The elements of an accessor, tightly packed in their own component type (little endian).</summary>
    private readonly struct AccessorData
    {
        public required byte[] Bytes { get; init; }
        public required int Count { get; init; }
        public required int Components { get; init; }
        public required int ComponentType { get; init; }
        public required bool Normalized { get; init; }
    }

    private static int ComponentSize(int componentType) => componentType switch
    {
        TypeByte or TypeUByte => 1,
        TypeShort or TypeUShort => 2,
        TypeUInt or TypeFloat => 4,
        _ => throw new InvalidDataException($"glTF component type {componentType} is invalid."),
    };

    private AccessorData ReadAccessor(int index)
    {
        if ((uint)index >= (uint)_accessors.Length)
        {
            throw new InvalidDataException($"The glTF file refers to accessor {index}, which does not exist.");
        }

        if (_decodedAccessors is not null && _decodedAccessors.TryGetValue(index, out var decoded))
        {
            return decoded;
        }

        var accessor = _accessors[index];
        var count = GetInt(accessor, "count", 0);
        var componentType = GetInt(accessor, "componentType", 0);
        var components = GetString(accessor, "type") switch
        {
            "SCALAR" => 1,
            "VEC2" => 2,
            "VEC3" => 3,
            "VEC4" or "MAT2" => 4,
            "MAT3" => 9,
            "MAT4" => 16,
            var other => throw new InvalidDataException($"glTF accessor type '{other}' is invalid."),
        };
        var componentSize = ComponentSize(componentType);
        var elementSize = componentSize * components;
        if (count < 0 || (long)count * elementSize > int.MaxValue)
        {
            throw new InvalidDataException($"glTF accessor {index} has an invalid count.");
        }

        var bytes = new byte[count * elementSize];
        if (GetIntOrNull(accessor, "bufferView") is { } viewIndex && count > 0)
        {
            var view = GetBufferView(viewIndex, out var stride).Span;
            var offset = GetInt(accessor, "byteOffset", 0);
            if (stride == 0)
            {
                stride = elementSize;
            }

            if (offset < 0 || (long)offset + ((long)(count - 1) * stride) + elementSize > view.Length)
            {
                throw new InvalidDataException($"glTF accessor {index} reaches outside its buffer view.");
            }

            if (stride == elementSize)
            {
                view.Slice(offset, bytes.Length).CopyTo(bytes);
            }
            else
            {
                for (var i = 0; i < count; i++)
                {
                    view.Slice(offset + (i * stride), elementSize).CopyTo(bytes.AsSpan(i * elementSize, elementSize));
                }
            }
        }

        if (TryGet(accessor, "sparse", out var sparse))
        {
            var sparseCount = GetInt(sparse, "count", 0);
            if (sparseCount > 0 && TryGet(sparse, "indices", out var indices) && TryGet(sparse, "values", out var values))
            {
                var indexType = GetInt(indices, "componentType", TypeUInt);
                var indexSize = ComponentSize(indexType);
                var indexView = GetBufferView(GetInt(indices, "bufferView", -1), out _).Span;
                var indexOffset = GetInt(indices, "byteOffset", 0);
                var valueView = GetBufferView(GetInt(values, "bufferView", -1), out _).Span;
                var valueOffset = GetInt(values, "byteOffset", 0);
                if ((long)indexOffset + ((long)sparseCount * indexSize) > indexView.Length
                    || (long)valueOffset + ((long)sparseCount * elementSize) > valueView.Length)
                {
                    throw new InvalidDataException($"The sparse data of glTF accessor {index} reaches outside its buffer view.");
                }

                for (var i = 0; i < sparseCount; i++)
                {
                    var at = indexView[(indexOffset + (i * indexSize))..];
                    var target = indexType switch
                    {
                        TypeUByte => at[0],
                        TypeUShort => BinaryPrimitives.ReadUInt16LittleEndian(at),
                        _ => (long)BinaryPrimitives.ReadUInt32LittleEndian(at),
                    };
                    if (target >= count)
                    {
                        throw new InvalidDataException($"The sparse data of glTF accessor {index} has an index out of range.");
                    }

                    valueView.Slice(valueOffset + (i * elementSize), elementSize).CopyTo(bytes.AsSpan((int)target * elementSize, elementSize));
                }
            }
        }

        return new AccessorData
        {
            Bytes = bytes,
            Count = count,
            Components = components,
            ComponentType = componentType,
            Normalized = GetBool(accessor, "normalized", false),
        };
    }

    /// <summary>The accessor as floats, with normalized integers mapped to 0..1 or -1..1.</summary>
    private static float[] ToFloats(in AccessorData a)
    {
        var n = a.Count * a.Components;
        var result = new float[n];
        var bytes = a.Bytes.AsSpan();
        switch (a.ComponentType)
        {
            case TypeFloat:
                MemoryMarshal.Cast<byte, float>(bytes).CopyTo(result);
                break;
            case TypeUByte:
                for (var i = 0; i < n; i++)
                {
                    result[i] = a.Normalized ? bytes[i] / 255f : bytes[i];
                }

                break;
            case TypeByte:
                for (var i = 0; i < n; i++)
                {
                    var v = (sbyte)bytes[i];
                    result[i] = a.Normalized ? MathF.Max(v / 127f, -1f) : v;
                }

                break;
            case TypeUShort:
            {
                var values = MemoryMarshal.Cast<byte, ushort>(bytes);
                for (var i = 0; i < n; i++)
                {
                    result[i] = a.Normalized ? values[i] / 65535f : values[i];
                }

                break;
            }

            case TypeShort:
            {
                var values = MemoryMarshal.Cast<byte, short>(bytes);
                for (var i = 0; i < n; i++)
                {
                    result[i] = a.Normalized ? MathF.Max(values[i] / 32767f, -1f) : values[i];
                }

                break;
            }

            default:
            {
                var values = MemoryMarshal.Cast<byte, uint>(bytes);
                for (var i = 0; i < n; i++)
                {
                    result[i] = values[i];
                }

                break;
            }
        }

        return result;
    }

    /// <summary>The accessor as integers (indices).</summary>
    private static int[] ToInts(in AccessorData a)
    {
        var n = a.Count * a.Components;
        var result = new int[n];
        var bytes = a.Bytes.AsSpan();
        switch (a.ComponentType)
        {
            case TypeUByte:
                for (var i = 0; i < n; i++)
                {
                    result[i] = bytes[i];
                }

                break;
            case TypeByte:
                for (var i = 0; i < n; i++)
                {
                    result[i] = (sbyte)bytes[i];
                }

                break;
            case TypeUShort:
            {
                var values = MemoryMarshal.Cast<byte, ushort>(bytes);
                for (var i = 0; i < n; i++)
                {
                    result[i] = values[i];
                }

                break;
            }

            case TypeShort:
            {
                var values = MemoryMarshal.Cast<byte, short>(bytes);
                for (var i = 0; i < n; i++)
                {
                    result[i] = values[i];
                }

                break;
            }

            case TypeUInt:
                MemoryMarshal.Cast<byte, int>(bytes).CopyTo(result);
                break;
            default:
            {
                var values = MemoryMarshal.Cast<byte, float>(bytes);
                for (var i = 0; i < n; i++)
                {
                    result[i] = (int)values[i];
                }

                break;
            }
        }

        return result;
    }

    /// <summary>A vertex attribute over the accessor's data. Integer data stays in its own type when the GPU has a
    /// matching vertex format (quantized meshes stay small); otherwise it becomes floats.</summary>
    private static VertexAttribute ToAttribute(in AccessorData a)
    {
        var size = a.Components;
        var bytes = a.Bytes.AsSpan();
        if (size is >= 1 and <= 4)
        {
            switch (a.ComponentType)
            {
                case TypeUByte when (a.Normalized && size >= 2) || (!a.Normalized && size == 4):
                    return VertexAttribute.UByte(a.Bytes, size, a.Normalized);
                case TypeByte when a.Normalized && size >= 2:
                    return VertexAttribute.SByte(MemoryMarshal.Cast<byte, sbyte>(bytes).ToArray(), size);
                case TypeUShort when (a.Normalized && size >= 2) || (!a.Normalized && size is 2 or 4):
                    return VertexAttribute.UShort(MemoryMarshal.Cast<byte, ushort>(bytes).ToArray(), size, a.Normalized);
                case TypeShort when a.Normalized && size >= 2:
                    return VertexAttribute.Short(MemoryMarshal.Cast<byte, short>(bytes).ToArray(), size);
            }
        }

        return VertexAttribute.Float(ToFloats(a), Math.Clamp(size, 1, 4));
    }
}
