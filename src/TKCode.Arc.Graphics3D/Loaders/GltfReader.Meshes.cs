using System.Numerics;
using System.Runtime.InteropServices;
using System.Text.Json;

namespace TKCode.Arc.Graphics3D;

internal sealed partial class GltfReader
{
    private const int ModePoints = 0;
    private const int ModeLines = 1;
    private const int ModeLineLoop = 2;
    private const int ModeLineStrip = 3;
    private const int ModeTriangles = 4;
    private const int ModeTriangleStrip = 5;
    private const int ModeTriangleFan = 6;

    /// <summary>A primitive's geometry, shared by every node that instances its mesh.</summary>
    private sealed class PrimitiveData
    {
        public required BufferGeometry Geometry { get; init; }
        public required int Mode { get; init; }
        public required int Material { get; init; }
        public bool HasColor { get; init; }
        public bool HasTangent { get; init; }
        public bool HasSkin { get; init; }
    }

    private readonly Dictionary<(int Mesh, int Primitive), PrimitiveData> _primitiveCache = [];
    private readonly List<BufferGeometry> _geometries = [];
    private JsonElement[]? _meshElements;

    private JsonElement[] MeshElements => _meshElements ??= Elements(_root, "meshes");

    private static string? AttributeName(string semantic) => semantic switch
    {
        "POSITION" => BufferGeometry.Position,
        "NORMAL" => BufferGeometry.Normal,
        "TANGENT" => BufferGeometry.Tangent,
        "TEXCOORD_0" => BufferGeometry.Uv,
        "TEXCOORD_1" => BufferGeometry.Uv1,
        "COLOR_0" => BufferGeometry.Color,
        "JOINTS_0" => BufferGeometry.SkinIndex,
        "WEIGHTS_0" => BufferGeometry.SkinWeight,
        _ => null,
    };

    private static int ExpectedComponents(string semantic, int actual) => semantic switch
    {
        "POSITION" or "NORMAL" => 3,
        "TANGENT" or "JOINTS_0" or "WEIGHTS_0" => 4,
        "TEXCOORD_0" or "TEXCOORD_1" => 2,
        _ => actual is 3 or 4 ? actual : 3,
    };

    private PrimitiveData GetPrimitive(int meshIndex, int primitiveIndex, JsonElement primitive, string meshName)
    {
        if (_primitiveCache.TryGetValue((meshIndex, primitiveIndex), out var cached))
        {
            return cached;
        }

        if (TryGetExtension(primitive, "KHR_draco_mesh_compression", out var draco))
        {
            _decodedAccessors = DecodeDraco(primitive, draco, meshName);
        }

        try
        {
            return ReadPrimitive(meshIndex, primitiveIndex, primitive, meshName);
        }
        finally
        {
            _decodedAccessors = null;
        }
    }

    private PrimitiveData ReadPrimitive(int meshIndex, int primitiveIndex, JsonElement primitive, string meshName)
    {
        var geometry = new BufferGeometry { Name = meshName };
        _geometries.Add(geometry);
        var vertexCount = -1;
        BoundingBox? quantizedBounds = null;
        bool hasColor = false, hasTangent = false, hasJoints = false, hasWeights = false, hasNormal = false;
        if (TryGet(primitive, "attributes", out var attributes) && attributes.ValueKind == JsonValueKind.Object)
        {
            // Positions first: they define the vertex count the other attributes must match.
            if (GetIntOrNull(attributes, "POSITION") is { } positionAccessor)
            {
                var data = ReadAccessor(positionAccessor);
                if (data.Components == 3)
                {
                    vertexCount = data.Count;
                    geometry.SetAttribute(BufferGeometry.Position, ToAttribute(data));
                    if (data.ComponentType != TypeFloat && vertexCount > 0)
                    {
                        // Quantized positions: take the bounds from the decoded values once, instead of having the
                        // geometry decode every vertex again when it is first culled.
                        quantizedBounds = BoundingBox.FromPoints(MemoryMarshal.Cast<float, Vector3>(ToFloats(data)));
                    }
                }
            }

            foreach (var attribute in attributes.EnumerateObject())
            {
                if (attribute.Name == "POSITION" || attribute.Value.ValueKind != JsonValueKind.Number)
                {
                    continue;
                }

                var name = AttributeName(attribute.Name);
                if (name is null)
                {
                    if (!attribute.Name.StartsWith('_'))
                    {
                        Warn($"Vertex attribute {attribute.Name} is not supported and was ignored.");
                    }

                    continue;
                }

                var data = ReadAccessor(attribute.Value.GetInt32());
                if (data.Count != vertexCount || data.Components != ExpectedComponents(attribute.Name, data.Components))
                {
                    Warn($"Vertex attribute {attribute.Name} of mesh '{meshName}' does not match its positions and was ignored.");
                    continue;
                }

                geometry.SetAttribute(name, ToAttribute(data));
                hasColor |= attribute.Name == "COLOR_0";
                hasTangent |= attribute.Name == "TANGENT";
                hasNormal |= attribute.Name == "NORMAL";
                hasJoints |= attribute.Name == "JOINTS_0";
                hasWeights |= attribute.Name == "WEIGHTS_0";
            }
        }

        if (vertexCount < 0)
        {
            Warn($"A primitive of mesh '{meshName}' has no positions.");
            vertexCount = 0;
        }

        if (quantizedBounds is { } bounds)
        {
            geometry.BoundingBox = bounds;
        }

        var mode = GetInt(primitive, "mode", ModeTriangles);
        int[]? indices = null;
        if (GetIntOrNull(primitive, "indices") is { } indexAccessor)
        {
            indices = ToInts(ReadAccessor(indexAccessor));
            foreach (var i in indices)
            {
                if ((uint)i >= (uint)vertexCount)
                {
                    throw new InvalidDataException($"Mesh '{meshName}' has an index outside its vertices.");
                }
            }
        }

        indices = mode switch
        {
            ModeLineLoop => CloseLoop(indices, vertexCount),
            ModeTriangleStrip => StripToList(indices, vertexCount),
            ModeTriangleFan => FanToList(indices, vertexCount),
            _ => indices,
        };
        if (indices is not null)
        {
            geometry.SetIndex(indices, vertexCount);
        }

        var targets = Elements(primitive, "targets");
        if (targets.Length > 0)
        {
            var names = TryGet(MeshElements[meshIndex], "extras", out var extras) ? Elements(extras, "targetNames") : [];
            for (var t = 0; t < targets.Length; t++)
            {
                var positions = ReadDeltas(targets[t], "POSITION", vertexCount) ?? new float[vertexCount * 3];
                // Normal offsets only help when the geometry has normals to offset.
                var normals = hasNormal ? ReadDeltas(targets[t], "NORMAL", vertexCount) : null;
                var name = t < names.Length && names[t].ValueKind == JsonValueKind.String ? names[t].GetString() ?? "" : "";
                geometry.AddMorphTarget(new MorphTarget(positions, normals, name));
            }
        }

        var result = new PrimitiveData
        {
            Geometry = geometry,
            Mode = mode,
            Material = GetInt(primitive, "material", -1),
            HasColor = hasColor,
            HasTangent = hasTangent,
            HasSkin = hasJoints && hasWeights,
        };
        _primitiveCache.Add((meshIndex, primitiveIndex), result);
        return result;
    }

    private float[]? ReadDeltas(JsonElement target, string semantic, int vertexCount)
    {
        if (GetIntOrNull(target, semantic) is not { } accessor)
        {
            return null;
        }

        var data = ReadAccessor(accessor);
        if (data.Count != vertexCount || data.Components != 3)
        {
            Warn($"A morph target's {semantic} data does not match the mesh and was ignored.");
            return null;
        }

        return ToFloats(data);
    }

    private static int[] CloseLoop(int[]? indices, int vertexCount)
    {
        var n = indices?.Length ?? vertexCount;
        if (n == 0)
        {
            return [];
        }

        var result = new int[n + 1];
        for (var i = 0; i < n; i++)
        {
            result[i] = indices?[i] ?? i;
        }

        result[n] = result[0];
        return result;
    }

    private static int[] StripToList(int[]? indices, int vertexCount)
    {
        var n = indices?.Length ?? vertexCount;
        if (n < 3)
        {
            return [];
        }

        var result = new int[(n - 2) * 3];
        for (var i = 0; i < n - 2; i++)
        {
            int a = indices?[i] ?? i, b = indices?[i + 1] ?? (i + 1), c = indices?[i + 2] ?? (i + 2);
            // Every other triangle of a strip has the opposite winding.
            result[i * 3] = (i & 1) == 0 ? a : b;
            result[(i * 3) + 1] = (i & 1) == 0 ? b : a;
            result[(i * 3) + 2] = c;
        }

        return result;
    }

    private static int[] FanToList(int[]? indices, int vertexCount)
    {
        var n = indices?.Length ?? vertexCount;
        if (n < 3)
        {
            return [];
        }

        var result = new int[(n - 2) * 3];
        for (var i = 1; i < n - 1; i++)
        {
            result[(i - 1) * 3] = indices?[0] ?? 0;
            result[((i - 1) * 3) + 1] = indices?[i] ?? i;
            result[((i - 1) * 3) + 2] = indices?[i + 1] ?? (i + 1);
        }

        return result;
    }

    /// <summary>Creates the object that draws one primitive: a new object per node, over shared geometry and material.</summary>
    private Renderable CreatePrimitiveObject(int meshIndex, int primitiveIndex, JsonElement primitive, string meshName, bool skinned, float[]? weights)
    {
        var data = GetPrimitive(meshIndex, primitiveIndex, primitive, meshName);
        var kind = data.Mode switch
        {
            ModePoints => MaterialKind.Points,
            ModeLines or ModeLineLoop or ModeLineStrip => MaterialKind.Lines,
            _ => MaterialKind.Surface,
        };
        var material = GetMaterial(data.Material, data.HasColor, data.HasTangent, kind);
        Renderable obj;
        switch (data.Mode)
        {
            case ModePoints:
                obj = new Points(data.Geometry, material);
                break;
            case ModeLines:
                obj = new LineSegments(data.Geometry, material);
                break;
            case ModeLineLoop:
            case ModeLineStrip:
                obj = new Line(data.Geometry, material);
                break;
            default:
            {
                var mesh = skinned && data.HasSkin ? new SkinnedMesh(data.Geometry, material) : new Mesh(data.Geometry, material);
                var targetCount = data.Geometry.MorphTargets.Count;
                if (targetCount > 0)
                {
                    mesh.MorphWeights = new float[targetCount];
                    weights?.AsSpan(0, Math.Min(weights.Length, targetCount)).CopyTo(mesh.MorphWeights);
                }

                obj = mesh;
                break;
            }
        }

        obj.Name = meshName;
        return obj;
    }
}
