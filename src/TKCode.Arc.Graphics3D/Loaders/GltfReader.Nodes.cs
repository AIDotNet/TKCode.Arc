using System.Numerics;
using System.Runtime.InteropServices;
using System.Text.Json;

namespace TKCode.Arc.Graphics3D;

internal sealed partial class GltfReader
{
    private readonly List<Camera> _cameras = [];
    private readonly List<Light> _lights = [];
    private readonly List<(SkinnedMesh Mesh, int Skin)> _skinnedMeshes = [];
    private JsonElement[] _nodes = [];
    private Object3D?[] _nodeObjects = [];
    private List<Mesh>?[] _nodeMeshes = [];
    private bool[] _isJoint = [];
    private bool[] _building = [];

    private GltfAsset BuildScenes()
    {
        _nodes = Elements(_root, "nodes");
        _nodeObjects = new Object3D?[_nodes.Length];
        _nodeMeshes = new List<Mesh>?[_nodes.Length];
        _isJoint = new bool[_nodes.Length];
        _building = new bool[_nodes.Length];
        var skins = Elements(_root, "skins");
        foreach (var skin in skins)
        {
            foreach (var joint in Elements(skin, "joints"))
            {
                if (joint.ValueKind == JsonValueKind.Number && (uint)joint.GetInt32() < (uint)_nodes.Length)
                {
                    _isJoint[joint.GetInt32()] = true;
                }
            }
        }

        var scenes = new List<Group>();
        var sceneElements = Elements(_root, "scenes");
        foreach (var sceneElement in sceneElements)
        {
            var group = new Group { Name = GetString(sceneElement, "name") ?? "" };
            foreach (var rootNode in Elements(sceneElement, "nodes"))
            {
                AddNode(group, rootNode);
            }

            scenes.Add(group);
        }

        if (scenes.Count == 0)
        {
            // No scene: show every node that is not somebody's child.
            var isChild = new bool[_nodes.Length];
            foreach (var node in _nodes)
            {
                foreach (var child in Elements(node, "children"))
                {
                    if (child.ValueKind == JsonValueKind.Number && (uint)child.GetInt32() < (uint)isChild.Length)
                    {
                        isChild[child.GetInt32()] = true;
                    }
                }
            }

            var group = new Group();
            for (var i = 0; i < _nodes.Length; i++)
            {
                if (!isChild[i])
                {
                    group.Add(BuildNode(i));
                }
            }

            scenes.Add(group);
        }

        var defaultIndex = Math.Clamp(GetInt(_root, "scene", 0), 0, scenes.Count - 1);
        BindSkins(skins);
        AimLights();
        var animations = BuildAnimations();
        return new GltfAsset(scenes[defaultIndex], scenes, animations, _cameras, _lights, _materials, _textures, _geometries, _warnings);
    }

    private void AddNode(Object3D parent, JsonElement reference)
    {
        if (reference.ValueKind != JsonValueKind.Number || (uint)reference.GetInt32() >= (uint)_nodes.Length)
        {
            Warn("A node reference points outside the node list and was ignored.");
            return;
        }

        var index = reference.GetInt32();
        if (_nodeObjects[index] is not null || _building[index])
        {
            Warn($"Node {index} is used in more than one place; only its first use was kept.");
            return;
        }

        parent.Add(BuildNode(index));
    }

    private Object3D BuildNode(int index)
    {
        if (_nodeObjects[index] is { } existing)
        {
            return existing;
        }

        _building[index] = true;
        var node = _nodes[index];
        var name = GetString(node, "name") ?? "";
        var meshIndex = GetIntOrNull(node, "mesh");
        var skinIndex = GetIntOrNull(node, "skin");
        var cameraIndex = GetIntOrNull(node, "camera");
        var lightIndex = TryGetExtension(node, "KHR_lights_punctual", out var lightReference) ? GetIntOrNull(lightReference, "light") : null;
        JsonElement mesh = default;
        JsonElement[] primitives = [];
        if (meshIndex is { } m)
        {
            if ((uint)m < (uint)MeshElements.Length)
            {
                mesh = MeshElements[m];
                primitives = Elements(mesh, "primitives");
            }
            else
            {
                Warn($"Node '{name}' refers to mesh {m}, which does not exist.");
                meshIndex = null;
            }
        }

        var meshName = GetString(mesh, "name") ?? name;
        var weights = GetFloats(node, "weights", 1) ?? GetFloats(mesh, "weights", 1);
        Object3D obj;
        if (primitives.Length == 1 && !_isJoint[index] && cameraIndex is null && lightIndex is null)
        {
            // The common case: the node is the mesh.
            obj = AddPrimitive(null, index, meshIndex!.Value, 0, primitives[0], meshName, skinIndex, weights);
        }
        else
        {
            obj = _isJoint[index] ? new Bone() : new Group();
            for (var p = 0; p < primitives.Length; p++)
            {
                AddPrimitive(obj, index, meshIndex!.Value, p, primitives[p], meshName, skinIndex, weights);
            }

            if (cameraIndex is { } c && CreateCamera(c) is { } camera)
            {
                obj.Add(camera);
            }

            if (lightIndex is { } l && CreateLight(l) is { } light)
            {
                obj.Add(light);
            }
        }

        obj.Name = name.Length > 0 ? name : obj.Name;
        if (GetFloats(node, "matrix", 16) is { } matrix)
        {
            // glTF stores column vectors column by column, which is the memory layout of a row-vector Matrix4x4.
            var local = MemoryMarshal.Read<Matrix4x4>(MemoryMarshal.AsBytes(matrix.AsSpan(0, 16)));
            if (Matrix4x4.Decompose(local, out _, out _, out _))
            {
                obj.SetLocalMatrix(local);
            }
            else
            {
                Warn($"The matrix of node '{name}' cannot be split into translation, rotation and scale; the node was left untransformed.");
            }
        }
        else
        {
            if (GetFloats(node, "translation", 3) is { } t)
            {
                obj.Position = new Vector3(t[0], t[1], t[2]);
            }

            if (GetFloats(node, "rotation", 4) is { } r)
            {
                var q = new Quaternion(r[0], r[1], r[2], r[3]);
                obj.Rotation = q.LengthSquared() > 1e-12f ? Quaternion.Normalize(q) : Quaternion.Identity;
            }

            if (GetFloats(node, "scale", 3) is { } s)
            {
                obj.Scale = new Vector3(s[0], s[1], s[2]);
            }
        }

        _nodeObjects[index] = obj;
        foreach (var child in Elements(node, "children"))
        {
            AddNode(obj, child);
        }

        _building[index] = false;
        return obj;
    }

    private Renderable AddPrimitive(
        Object3D? parent, int nodeIndex, int meshIndex, int primitiveIndex, JsonElement primitive, string meshName, int? skinIndex, float[]? weights)
    {
        var obj = CreatePrimitiveObject(meshIndex, primitiveIndex, primitive, meshName, skinIndex is not null, weights);
        if (obj is Mesh mesh)
        {
            (_nodeMeshes[nodeIndex] ??= []).Add(mesh);
            if (mesh is SkinnedMesh skinned && skinIndex is { } skin)
            {
                _skinnedMeshes.Add((skinned, skin));
            }
        }

        parent?.Add(obj);
        return obj;
    }

    // ---- Skins ----

    private void BindSkins(JsonElement[] skins)
    {
        var skeletons = new Skeleton?[skins.Length];
        foreach (var (mesh, skinIndex) in _skinnedMeshes)
        {
            if ((uint)skinIndex >= (uint)skins.Length)
            {
                Warn($"Mesh '{mesh.Name}' refers to skin {skinIndex}, which does not exist.");
                continue;
            }

            mesh.Skeleton = skeletons[skinIndex] ??= CreateSkeleton(skins[skinIndex]);
        }
    }

    private Skeleton CreateSkeleton(JsonElement skin)
    {
        var joints = Elements(skin, "joints");
        var bones = new Object3D[joints.Length];
        for (var i = 0; i < joints.Length; i++)
        {
            var index = joints[i].ValueKind == JsonValueKind.Number ? joints[i].GetInt32() : -1;
            if ((uint)index >= (uint)_nodes.Length)
            {
                throw new InvalidDataException("A glTF skin refers to a joint node that does not exist.");
            }

            // A joint outside every scene still poses the mesh: build it on its own.
            bones[i] = BuildNode(index);
        }

        var inverse = new Matrix4x4[bones.Length];
        Array.Fill(inverse, Matrix4x4.Identity);
        if (GetIntOrNull(skin, "inverseBindMatrices") is { } accessor)
        {
            var data = ReadAccessor(accessor);
            if (data.ComponentType == TypeFloat && data.Components == 16 && data.Count >= bones.Length)
            {
                MemoryMarshal.Cast<byte, Matrix4x4>(data.Bytes.AsSpan(0, bones.Length * 64)).CopyTo(inverse);
            }
            else
            {
                Warn("A skin's inverse bind matrices are malformed; identity matrices are used.");
            }
        }

        return new Skeleton(bones, inverse);
    }

    // ---- Cameras and lights ----

    private Camera? CreateCamera(int index)
    {
        var cameras = Elements(_root, "cameras");
        if ((uint)index >= (uint)cameras.Length)
        {
            Warn($"Camera {index} does not exist.");
            return null;
        }

        var definition = cameras[index];
        Camera camera;
        if (GetString(definition, "type") == "orthographic" && TryGet(definition, "orthographic", out var orthographic))
        {
            var xmag = GetFloat(orthographic, "xmag", 1f);
            var ymag = GetFloat(orthographic, "ymag", 1f);
            camera = new OrthographicCamera(-xmag, xmag, ymag, -ymag, GetFloat(orthographic, "znear", 0.01f), GetFloat(orthographic, "zfar", 2000f));
        }
        else
        {
            TryGet(definition, "perspective", out var perspective);
            camera = new PerspectiveCamera(
                MathUtil.ToDegrees(GetFloat(perspective, "yfov", 0.8f)),
                GetFloat(perspective, "aspectRatio", 1f),
                GetFloat(perspective, "znear", 0.1f),
                // Without zfar the projection is infinite; a very distant plane stands in for it.
                GetFloat(perspective, "zfar", 2e6f));
        }

        camera.Name = GetString(definition, "name") ?? "";
        _cameras.Add(camera);
        Remember(_camerasByIndex, index, camera);
        return camera;
    }

    private Light? CreateLight(int index)
    {
        var lights = TryGetExtension(_root, "KHR_lights_punctual", out var extension) ? Elements(extension, "lights") : [];
        if ((uint)index >= (uint)lights.Length)
        {
            Warn($"Light {index} does not exist.");
            return null;
        }

        var definition = lights[index];
        var rgb = GetFloats(definition, "color", 3) ?? [1f, 1f, 1f];
        var color = new ColorF(rgb[0], rgb[1], rgb[2]);
        var intensity = GetFloat(definition, "intensity", 1f);
        var range = GetFloat(definition, "range", 0f);
        Light light;
        switch (GetString(definition, "type"))
        {
            case "directional":
                light = new DirectionalLight(color, intensity);
                break;
            case "point":
                light = new PointLight(color, intensity, range);
                break;
            case "spot":
            {
                TryGet(definition, "spot", out var spot);
                var outer = GetFloat(spot, "outerConeAngle", MathF.PI / 4f);
                var inner = GetFloat(spot, "innerConeAngle", 0f);
                light = new SpotLight(color, intensity, range, outer, outer > 0f ? Math.Clamp(1f - (inner / outer), 0f, 1f) : 0f);
                break;
            }

            default:
                Warn($"Light type '{GetString(definition, "type")}' is not supported.");
                return null;
        }

        // The light sits on its node and shines along the node's -Z axis.
        light.Position = Vector3.Zero;
        light.Name = GetString(definition, "name") ?? "";
        _lights.Add(light);
        Remember(_lightsByIndex, index, light);
        return light;
    }

    /// <summary>glTF lights shine along their node's -Z axis: no target, so they follow the node.</summary>
    private void AimLights()
    {
        foreach (var light in _lights)
        {
            switch (light)
            {
                case DirectionalLight directional:
                    directional.Target = null;
                    break;
                case SpotLight spot:
                    spot.Target = null;
                    break;
            }
        }
    }

    // ---- Animations ----

    private List<AnimationClip> BuildAnimations()
    {
        var clips = new List<AnimationClip>();
        var animations = Elements(_root, "animations");
        for (var a = 0; a < animations.Length; a++)
        {
            var animation = animations[a];
            var name = GetString(animation, "name") ?? "animation_" + a.ToString(System.Globalization.CultureInfo.InvariantCulture);
            var samplers = Elements(animation, "samplers");
            var tracks = new List<KeyframeTrack>();
            // Channels often share their key times: read each input accessor once.
            var inputs = new Dictionary<int, float[]>();
            foreach (var channel in Elements(animation, "channels"))
            {
                if (!TryGet(channel, "target", out var target) || GetIntOrNull(channel, "sampler") is not { } samplerIndex
                    || (uint)samplerIndex >= (uint)samplers.Length)
                {
                    continue;
                }

                var path = GetString(target, "path");
                int nodeIndex;
                TrackProperty property;
                TrackSetter? setter = null;
                var setterSize = 0;
                if (path == "pointer")
                {
                    // KHR_animation_pointer: the target is any animatable property, named by its JSON pointer.
                    var pointer = TryGetExtension(target, "KHR_animation_pointer", out var pointerExtension) ? GetString(pointerExtension, "pointer") : null;
                    if (pointer is null || !TryResolvePointer(pointer, out nodeIndex, out property, out setter, out setterSize))
                    {
                        Warn($"Animation '{name}' animates '{pointer ?? "pointer"}', which is not supported.");
                        continue;
                    }
                }
                else
                {
                    if (GetIntOrNull(target, "node") is not { } targetNode)
                    {
                        continue;
                    }

                    nodeIndex = targetNode;
                    switch (path)
                    {
                        case "translation":
                            property = TrackProperty.Position;
                            break;
                        case "rotation":
                            property = TrackProperty.Rotation;
                            break;
                        case "scale":
                            property = TrackProperty.Scale;
                            break;
                        case "weights":
                            property = TrackProperty.MorphWeights;
                            break;
                        default:
                            Warn($"Animation '{name}' animates '{path}', which is not supported.");
                            continue;
                    }
                }

                if (setter is null && (uint)nodeIndex >= (uint)_nodes.Length)
                {
                    Warn($"Animation '{name}' targets node {nodeIndex}, which does not exist.");
                    continue;
                }

                var sampler = samplers[samplerIndex];
                if (GetIntOrNull(sampler, "input") is not { } input || GetIntOrNull(sampler, "output") is not { } output)
                {
                    continue;
                }

                var interpolation = GetString(sampler, "interpolation") switch
                {
                    "STEP" => Interpolation.Step,
                    "CUBICSPLINE" => Interpolation.CubicSpline,
                    _ => Interpolation.Linear,
                };
                if (!inputs.TryGetValue(input, out var times))
                {
                    times = ToFloats(ReadAccessor(input));
                    inputs.Add(input, times);
                }

                var values = ToFloats(ReadAccessor(output));
                var perKey = interpolation == Interpolation.CubicSpline ? 3 : 1;
                var keys = times.Length * perKey;
                var size = property switch
                {
                    TrackProperty.Custom => setterSize,
                    TrackProperty.Rotation => 4,
                    TrackProperty.MorphWeights => keys > 0 ? values.Length / keys : 0,
                    _ => 3,
                };
                if (times.Length == 0 || size == 0 || values.Length != keys * size)
                {
                    Warn($"A channel of animation '{name}' has key data that does not match its key times and was ignored.");
                    continue;
                }

                if (setter is not null)
                {
                    tracks.Add(new KeyframeTrack(setter, size, times, values, interpolation));
                    continue;
                }

                // A node of the animation may lie outside every scene.
                var node = BuildNode(nodeIndex);
                if (property != TrackProperty.MorphWeights)
                {
                    tracks.Add(new KeyframeTrack(node, property, times, values, interpolation));
                    continue;
                }

                var meshes = _nodeMeshes[nodeIndex];
                if (meshes is null)
                {
                    Warn($"Animation '{name}' animates the morph weights of a node without a mesh.");
                    continue;
                }

                foreach (var mesh in meshes)
                {
                    if (mesh.Geometry.MorphTargets.Count == size)
                    {
                        tracks.Add(new KeyframeTrack(mesh, TrackProperty.MorphWeights, times, values, interpolation));
                    }
                }
            }

            clips.Add(new AnimationClip(name, [.. tracks]));
        }

        return clips;
    }
}
