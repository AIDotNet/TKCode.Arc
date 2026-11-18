using System.Globalization;
using System.Numerics;

namespace TKCode.Arc.Graphics3D;

// KHR_animation_pointer: an animation channel may target any animatable property of the file by its JSON pointer,
// not only a node's transform. A pointer is resolved to a setter on the objects the loader built from that part of the file.
internal sealed partial class GltfReader
{
    private readonly Dictionary<int, List<Camera>> _camerasByIndex = [];
    private readonly Dictionary<int, List<Light>> _lightsByIndex = [];

    private static void Remember<T>(Dictionary<int, List<T>> map, int index, T value)
    {
        if (!map.TryGetValue(index, out var list))
        {
            list = [];
            map.Add(index, list);
        }

        list.Add(value);
    }

    /// <summary>Resolves a pointer either to a node property (<paramref name="nodeIndex"/> ≥ 0) or to a setter that
    /// takes <paramref name="size"/> floats. False when the pointer names something that cannot be animated here.</summary>
    private bool TryResolvePointer(string pointer, out int nodeIndex, out TrackProperty property, out TrackSetter? setter, out int size)
    {
        nodeIndex = -1;
        property = TrackProperty.Custom;
        setter = null;
        size = 0;
        var parts = pointer.Split('/');
        if (parts.Length < 4 || parts[0].Length != 0)
        {
            return false;
        }

        static bool Index(string text, out int index) => int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out index);
        switch (parts[1])
        {
            case "nodes" when parts.Length == 4 && Index(parts[2], out var node):
                nodeIndex = node;
                switch (parts[3])
                {
                    case "translation":
                        property = TrackProperty.Position;
                        return true;
                    case "rotation":
                        property = TrackProperty.Rotation;
                        return true;
                    case "scale":
                        property = TrackProperty.Scale;
                        return true;
                    case "weights":
                        property = TrackProperty.MorphWeights;
                        return true;
                    default:
                        return false;
                }

            case "materials" when Index(parts[2], out var material):
                setter = MaterialSetter(material, string.Join('/', parts, 3, parts.Length - 3), out size);
                return setter is not null;
            case "cameras" when parts.Length == 5 && Index(parts[2], out var camera):
                setter = CameraSetter(camera, parts[3], parts[4]);
                size = 1;
                return setter is not null;
            case "extensions" when parts.Length >= 6 && parts[2] == "KHR_lights_punctual" && parts[3] == "lights" && Index(parts[4], out var light):
                setter = LightSetter(light, string.Join('/', parts, 5, parts.Length - 5), out size);
                return setter is not null;
            default:
                return false;
        }
    }

    private static TrackSetter Scalar<T>(Material[] targets, Action<T, float> set)
        where T : Material =>
        values =>
        {
            foreach (var material in targets)
            {
                if (material is T typed)
                {
                    set(typed, values[0]);
                }
            }
        };

    private static TrackSetter Rgb<T>(Material[] targets, Action<T, ColorF> set)
        where T : Material =>
        values =>
        {
            var color = new ColorF(values[0], values[1], values[2]);
            foreach (var material in targets)
            {
                if (material is T typed)
                {
                    set(typed, color);
                }
            }
        };

    private TrackSetter? MaterialSetter(int index, string path, out int size)
    {
        size = 1;
        var list = new List<Material>();
        foreach (var (key, material) in _materialCache)
        {
            if (key.Index == index)
            {
                list.Add(material);
            }
        }

        if (list.Count == 0)
        {
            return null;
        }

        // One glTF material can have become several here (with and without vertex colors, ...): animate them all.
        Material[] targets = [.. list];
        const string Transform = "/extensions/KHR_texture_transform/";
        var at = path.IndexOf(Transform, StringComparison.Ordinal);
        if (at > 0)
        {
            return TextureTransformSetter(targets, path[..at], path[(at + Transform.Length)..], out size);
        }

        switch (path)
        {
            case "pbrMetallicRoughness/baseColorFactor":
                size = 4;
                return values =>
                {
                    var color = new ColorF(values[0], values[1], values[2]);
                    foreach (var material in targets)
                    {
                        if (material is BasicMaterial basic)
                        {
                            basic.Color = color;
                        }
                        else if (material is LitMaterial lit)
                        {
                            lit.Color = color;
                        }

                        material.Opacity = values[3];
                    }
                };
            case "pbrMetallicRoughness/metallicFactor":
                return Scalar<StandardMaterial>(targets, static (m, v) => m.Metalness = v);
            case "pbrMetallicRoughness/roughnessFactor":
                return Scalar<StandardMaterial>(targets, static (m, v) => m.Roughness = v);
            case "alphaCutoff":
                return Scalar<Material>(targets, static (m, v) => m.AlphaTest = v);
            case "emissiveFactor":
                size = 3;
                return Rgb<LitMaterial>(targets, static (m, c) => m.Emissive = c);
            case "normalTexture/scale":
                // The sign of Y says which way the loader turned the green axis for this material: keep it.
                return Scalar<LitMaterial>(targets, static (m, v) => m.NormalScale = new Vector2(v, m.NormalScale.Y < 0f ? -v : v));
            case "occlusionTexture/strength":
                return Scalar<LitMaterial>(targets, static (m, v) => m.AoMapIntensity = v);
            case "extensions/KHR_materials_emissive_strength/emissiveStrength":
                return Scalar<LitMaterial>(targets, static (m, v) => m.EmissiveIntensity = v);
            case "extensions/KHR_materials_ior/ior":
                return Scalar<PhysicalMaterial>(targets, static (m, v) => m.Ior = v);
            case "extensions/KHR_materials_clearcoat/clearcoatFactor":
                return Scalar<PhysicalMaterial>(targets, static (m, v) => m.Clearcoat = v);
            case "extensions/KHR_materials_clearcoat/clearcoatRoughnessFactor":
                return Scalar<PhysicalMaterial>(targets, static (m, v) => m.ClearcoatRoughness = v);
            case "extensions/KHR_materials_sheen/sheenRoughnessFactor":
                return Scalar<PhysicalMaterial>(targets, static (m, v) => m.SheenRoughness = v);
            case "extensions/KHR_materials_sheen/sheenColorFactor":
                size = 3;
                return Rgb<PhysicalMaterial>(targets, static (m, c) => m.SheenColor = c);
            case "extensions/KHR_materials_specular/specularFactor":
                return Scalar<PhysicalMaterial>(targets, static (m, v) => m.SpecularIntensity = v);
            case "extensions/KHR_materials_specular/specularColorFactor":
                size = 3;
                return Rgb<PhysicalMaterial>(targets, static (m, c) => m.SpecularColor = c);
            case "extensions/KHR_materials_transmission/transmissionFactor":
                return Scalar<PhysicalMaterial>(targets, static (m, v) => m.Transmission = v);
            case "extensions/KHR_materials_volume/thicknessFactor":
                return Scalar<PhysicalMaterial>(targets, static (m, v) => m.Thickness = v);
            case "extensions/KHR_materials_volume/attenuationDistance":
                return Scalar<PhysicalMaterial>(targets, static (m, v) => m.AttenuationDistance = v);
            case "extensions/KHR_materials_volume/attenuationColor":
                size = 3;
                return Rgb<PhysicalMaterial>(targets, static (m, c) => m.AttenuationColor = c);
            case "extensions/KHR_materials_iridescence/iridescenceFactor":
                return Scalar<PhysicalMaterial>(targets, static (m, v) => m.Iridescence = v);
            case "extensions/KHR_materials_iridescence/iridescenceIor":
                return Scalar<PhysicalMaterial>(targets, static (m, v) => m.IridescenceIor = v);
            case "extensions/KHR_materials_iridescence/iridescenceThicknessMinimum":
                return Scalar<PhysicalMaterial>(targets, static (m, v) => m.IridescenceThicknessRange = new Vector2(v, m.IridescenceThicknessRange.Y));
            case "extensions/KHR_materials_iridescence/iridescenceThicknessMaximum":
                return Scalar<PhysicalMaterial>(targets, static (m, v) => m.IridescenceThicknessRange = new Vector2(m.IridescenceThicknessRange.X, v));
            case "extensions/KHR_materials_anisotropy/anisotropyStrength":
                return Scalar<PhysicalMaterial>(targets, static (m, v) => m.Anisotropy = v);
            default:
                return null;
        }
    }

    private static TrackSetter? TextureTransformSetter(Material[] materials, string textureProperty, string member, out int size)
    {
        size = member == "rotation" ? 1 : 2;
        var textures = new List<Texture>();
        foreach (var material in materials)
        {
            var texture = textureProperty switch
            {
                "pbrMetallicRoughness/baseColorTexture" => material is BasicMaterial basic ? basic.Map : (material as LitMaterial)?.Map,
                "pbrMetallicRoughness/metallicRoughnessTexture" => (material as StandardMaterial)?.RoughnessMap,
                "normalTexture" => (material as LitMaterial)?.NormalMap,
                "occlusionTexture" => (material as LitMaterial)?.AoMap,
                "emissiveTexture" => (material as LitMaterial)?.EmissiveMap,
                _ => null,
            };
            if (texture is not null && !textures.Contains(texture))
            {
                textures.Add(texture);
            }
        }

        if (textures.Count == 0)
        {
            return null;
        }

        Texture[] targets = [.. textures];
        return member switch
        {
            "offset" => values =>
            {
                foreach (var texture in targets)
                {
                    texture.Offset = new Vector2(values[0], values[1]);
                }
            },
            "scale" => values =>
            {
                foreach (var texture in targets)
                {
                    texture.Repeat = new Vector2(values[0], values[1]);
                }
            },
            "rotation" => values =>
            {
                foreach (var texture in targets)
                {
                    texture.Rotation = values[0];
                }
            },
            _ => null,
        };
    }

    private TrackSetter? CameraSetter(int index, string kind, string member)
    {
        if (!_camerasByIndex.TryGetValue(index, out var list))
        {
            return null;
        }

        Camera[] targets = [.. list];
        static TrackSetter Each(Camera[] targets, Action<Camera, float> set) =>
            values =>
            {
                foreach (var camera in targets)
                {
                    set(camera, values[0]);
                }
            };

        return (kind, member) switch
        {
            (_, "znear") => Each(targets, static (c, v) => c.Near = v),
            (_, "zfar") => Each(targets, static (c, v) => c.Far = v),
            ("perspective", "yfov") => Each(targets, static (c, v) =>
            {
                if (c is PerspectiveCamera perspective)
                {
                    perspective.Fov = MathUtil.ToDegrees(v);
                }
            }),
            ("perspective", "aspectRatio") => Each(targets, static (c, v) =>
            {
                if (c is PerspectiveCamera perspective)
                {
                    perspective.Aspect = v;
                }
            }),
            ("orthographic", "xmag") => Each(targets, static (c, v) =>
            {
                if (c is OrthographicCamera orthographic)
                {
                    orthographic.Left = -v;
                    orthographic.Right = v;
                }
            }),
            ("orthographic", "ymag") => Each(targets, static (c, v) =>
            {
                if (c is OrthographicCamera orthographic)
                {
                    orthographic.Top = v;
                    orthographic.Bottom = -v;
                }
            }),
            _ => null,
        };
    }

    private TrackSetter? LightSetter(int index, string path, out int size)
    {
        size = 1;
        if (!_lightsByIndex.TryGetValue(index, out var list))
        {
            return null;
        }

        Light[] targets = [.. list];
        static TrackSetter Each(Light[] targets, Action<Light, float> set) =>
            values =>
            {
                foreach (var light in targets)
                {
                    set(light, values[0]);
                }
            };

        switch (path)
        {
            case "color":
                size = 3;
                return values =>
                {
                    foreach (var light in targets)
                    {
                        light.Color = new ColorF(values[0], values[1], values[2]);
                    }
                };
            case "intensity":
                return Each(targets, static (l, v) => l.Intensity = v);
            case "range":
                return Each(targets, static (l, v) =>
                {
                    if (l is PointLight point)
                    {
                        point.Distance = v;
                    }
                    else if (l is SpotLight spot)
                    {
                        spot.Distance = v;
                    }
                });
            case "spot/outerConeAngle":
                // The inner cone is kept as an angle: the penumbra is its share of the outer one.
                return Each(targets, static (l, v) =>
                {
                    if (l is SpotLight spot)
                    {
                        var inner = spot.Angle * (1f - spot.Penumbra);
                        spot.Angle = v;
                        spot.Penumbra = v > 0f ? Math.Clamp(1f - (inner / v), 0f, 1f) : 0f;
                    }
                });
            case "spot/innerConeAngle":
                return Each(targets, static (l, v) =>
                {
                    if (l is SpotLight spot)
                    {
                        spot.Penumbra = spot.Angle > 0f ? Math.Clamp(1f - (v / spot.Angle), 0f, 1f) : 0f;
                    }
                });
            default:
                return null;
        }
    }
}
