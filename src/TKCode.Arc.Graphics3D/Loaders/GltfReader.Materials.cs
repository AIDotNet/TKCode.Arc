using System.Numerics;
using System.Text.Json;
using TKCode.Arc.Graphics3D.Rhi;

namespace TKCode.Arc.Graphics3D;

internal sealed partial class GltfReader
{
    /// <summary>What a material is drawn with: decides the material class.</summary>
    private enum MaterialKind
    {
        Surface,
        Points,
        Lines,
    }

    private readonly record struct TextureKey(int Index, bool Srgb, int Channel, float OffsetX, float OffsetY, float Rotation, float ScaleX, float ScaleY);

    private readonly record struct MaterialKey(int Index, bool VertexColors, bool FlipNormalY, MaterialKind Kind);

    private readonly Dictionary<TextureKey, Texture?> _textureCache = [];
    private readonly Dictionary<MaterialKey, Material> _materialCache = [];
    private readonly Dictionary<int, TextureImage?> _imageCache = [];
    private readonly List<Texture> _textures = [];
    private readonly List<Material> _materials = [];
    private JsonElement[]? _materialElements;
    private JsonElement[]? _textureElements;
    private JsonElement[]? _imageElements;
    private JsonElement[]? _samplerElements;

    // ---- Images and textures ----

    private TextureImage? GetImage(int index)
    {
        if (_imageCache.TryGetValue(index, out var cached))
        {
            return cached;
        }

        _imageElements ??= Elements(_root, "images");
        TextureImage? image = null;
        if ((uint)index >= (uint)_imageElements.Length)
        {
            Warn($"Image {index} does not exist.");
        }
        else
        {
            var element = _imageElements[index];
            var name = GetString(element, "name") ?? GetString(element, "uri") ?? index.ToString(System.Globalization.CultureInfo.InvariantCulture);
            try
            {
                ReadOnlyMemory<byte> bytes;
                if (GetIntOrNull(element, "bufferView") is { } view)
                {
                    bytes = GetBufferView(view, out _);
                }
                else if (GetString(element, "uri") is { } uri)
                {
                    bytes = ResolveUri(uri, "image");
                    if (name.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
                    {
                        name = "embedded image " + index.ToString(System.Globalization.CultureInfo.InvariantCulture);
                    }
                }
                else
                {
                    throw new InvalidDataException("it has neither a URI nor a buffer view");
                }

                image = ImageDecoders.Decode(bytes);
            }
            catch (Exception e) when (e is NotSupportedException or InvalidDataException or FileNotFoundException or FormatException or EndOfStreamException)
            {
                Warn($"Image '{name}' could not be loaded: {e.Message}");
            }
        }

        _imageCache.Add(index, image);
        return image;
    }

    /// <summary>The texture a glTF texture info object refers to, or null. One glTF texture gives one
    /// <see cref="Texture"/> per color space, UV set and UV transform it is used with.</summary>
    private Texture? GetTexture(JsonElement parent, string property, bool srgb, out int texCoord)
    {
        texCoord = 0;
        if (!TryGet(parent, property, out var info) || GetIntOrNull(info, "index") is not { } index)
        {
            return null;
        }

        texCoord = GetInt(info, "texCoord", 0);
        Vector2 offset = default, scale = Vector2.One;
        var rotation = 0f;
        if (TryGetExtension(info, "KHR_texture_transform", out var transform))
        {
            if (GetFloats(transform, "offset", 2) is { } o)
            {
                offset = new Vector2(o[0], o[1]);
            }

            if (GetFloats(transform, "scale", 2) is { } s)
            {
                scale = new Vector2(s[0], s[1]);
            }

            rotation = GetFloat(transform, "rotation", 0f);
            texCoord = GetInt(transform, "texCoord", texCoord);
            if (rotation != 0f && scale.X != scale.Y)
            {
                Warn("A texture transform combines rotation with a non-uniform scale; the result is approximate.");
            }
        }

        if (texCoord > 1)
        {
            Warn($"The '{property}' texture uses UV set {texCoord}; only sets 0 and 1 are supported, so set 1 is used.");
        }

        var channel = texCoord > 0 ? 1 : 0;
        var key = new TextureKey(index, srgb, channel, offset.X, offset.Y, rotation, scale.X, scale.Y);
        if (_textureCache.TryGetValue(key, out var cached))
        {
            return cached;
        }

        _textureElements ??= Elements(_root, "textures");
        Texture? texture = null;
        if ((uint)index >= (uint)_textureElements.Length)
        {
            Warn($"Texture {index} does not exist.");
        }
        else
        {
            var element = _textureElements[index];
            // A PNG or JPEG source when there is one; else the KTX2 image of KHR_texture_basisu.
            var source = GetIntOrNull(element, "source")
                ?? (TryGetExtension(element, "KHR_texture_basisu", out var basisu) ? GetIntOrNull(basisu, "source") : null);
            if (source is null)
            {
                Warn($"Texture {index} has no PNG, JPEG or KTX2 source (its image format needs an extension that is not supported).");
            }
            else if (GetImage(source.Value) is { } image)
            {
                texture = new Texture(image, srgb ? TextureColorSpace.Srgb : TextureColorSpace.Linear)
                {
                    Name = GetString(element, "name") ?? "",
                    FlipY = false,
                    Channel = channel,
                    Offset = offset,
                    Repeat = scale,
                    Rotation = rotation,
                };
                ApplySampler(texture, GetIntOrNull(element, "sampler"));
                _textures.Add(texture);
            }
        }

        _textureCache.Add(key, texture);
        return texture;
    }

    private void ApplySampler(Texture texture, int? samplerIndex)
    {
        _samplerElements ??= Elements(_root, "samplers");
        var sampler = samplerIndex is { } i && (uint)i < (uint)_samplerElements.Length ? _samplerElements[i] : default;
        texture.MagFilter = GetInt(sampler, "magFilter", 9729) == 9728 ? TextureFilter.Nearest : TextureFilter.Linear;
        (texture.MinFilter, texture.MipFilter) = GetInt(sampler, "minFilter", 9987) switch
        {
            9728 => (TextureFilter.Nearest, MipFilter.None),
            9729 => (TextureFilter.Linear, MipFilter.None),
            9984 => (TextureFilter.Nearest, MipFilter.Nearest),
            9985 => (TextureFilter.Linear, MipFilter.Nearest),
            9986 => (TextureFilter.Nearest, MipFilter.Linear),
            _ => (TextureFilter.Linear, MipFilter.Linear),
        };
        texture.GenerateMipmaps = texture.MipFilter != MipFilter.None;
        texture.WrapU = Wrap(GetInt(sampler, "wrapS", 10497));
        texture.WrapV = Wrap(GetInt(sampler, "wrapT", 10497));

        static TextureWrap Wrap(int mode) => mode switch
        {
            33071 => TextureWrap.ClampToEdge,
            33648 => TextureWrap.MirroredRepeat,
            _ => TextureWrap.Repeat,
        };
    }

    /// <summary>A texture info's texture; the UV set it names is carried by <see cref="Texture.Channel"/>.</summary>
    private Texture? GetTexture0(JsonElement parent, string property, bool srgb, string what)
    {
        _ = what;
        return GetTexture(parent, property, srgb, out _);
    }

    // ---- Materials ----

    /// <summary>The material of a primitive. A glTF material is created once per combination of what the primitive
    /// brings: vertex colors, tangents, and whether it is a surface, points or lines.</summary>
    private Material GetMaterial(int index, bool vertexColors, bool hasTangents, MaterialKind kind)
    {
        _materialElements ??= Elements(_root, "materials");
        JsonElement definition = default;
        if (index >= 0)
        {
            if (index >= _materialElements.Length)
            {
                Warn($"Material {index} does not exist; the default material is used.");
                index = -1;
            }
            else
            {
                definition = _materialElements[index];
            }
        }

        // Without vertex tangents the normal map's green axis follows the V direction of the UVs, which in glTF
        // points down the image: flip it, as the tangent frame from derivatives has no handedness to correct it.
        // The same holds for everything else given in tangent space: the clear coat's normal map and the anisotropy
        // direction.
        var flipNormalY = kind == MaterialKind.Surface && !hasTangents
            && (TryGet(definition, "normalTexture", out _)
                || (TryGetExtension(definition, "KHR_materials_clearcoat", out var coat) && TryGet(coat, "clearcoatNormalTexture", out _))
                || TryGetExtension(definition, "KHR_materials_anisotropy", out _));
        var key = new MaterialKey(index, vertexColors, flipNormalY, kind);
        if (_materialCache.TryGetValue(key, out var cached))
        {
            return cached;
        }

        var material = CreateMaterial(definition, kind, flipNormalY);
        material.Name = GetString(definition, "name") ?? "";
        material.VertexColors = vertexColors;
        if (GetBool(definition, "doubleSided", false))
        {
            material.Side = Side.Double;
        }

        switch (GetString(definition, "alphaMode"))
        {
            case "MASK":
                material.AlphaTest = GetFloat(definition, "alphaCutoff", 0.5f);
                break;
            case "BLEND":
                material.Transparent = true;
                material.DepthWrite = false;
                break;
        }

        _materialCache.Add(key, material);
        _materials.Add(material);
        return material;
    }

    private Material CreateMaterial(JsonElement definition, MaterialKind kind, bool flipNormalY)
    {
        TryGet(definition, "pbrMetallicRoughness", out var pbr);
        var baseColor = GetFloats(pbr, "baseColorFactor", 4) ?? [1f, 1f, 1f, 1f];
        var color = new ColorF(baseColor[0], baseColor[1], baseColor[2]);
        var map = GetTexture0(pbr, "baseColorTexture", srgb: true, "base color");
        if (kind == MaterialKind.Points)
        {
            return new PointsMaterial { Color = color, Opacity = baseColor[3], Map = map, Size = 1f, SizeAttenuation = false };
        }

        if (kind == MaterialKind.Lines)
        {
            return new LineMaterial { Color = color, Opacity = baseColor[3] };
        }

        if (TryGetExtension(definition, "KHR_materials_unlit", out _))
        {
            return new BasicMaterial { Color = color, Opacity = baseColor[3], Map = map };
        }

        var physical = HasAnyExtension(
            definition, "KHR_materials_clearcoat", "KHR_materials_sheen", "KHR_materials_ior", "KHR_materials_specular",
            "KHR_materials_transmission", "KHR_materials_volume", "KHR_materials_iridescence", "KHR_materials_anisotropy");
        var material = physical ? new PhysicalMaterial() : new StandardMaterial();
        material.Color = color;
        material.Opacity = baseColor[3];
        material.Map = map;
        material.Metalness = GetFloat(pbr, "metallicFactor", 1f);
        material.Roughness = GetFloat(pbr, "roughnessFactor", 1f);
        if (GetTexture0(pbr, "metallicRoughnessTexture", srgb: false, "metallic-roughness") is { } metallicRoughness)
        {
            material.RoughnessMap = metallicRoughness;
            material.MetalnessMap = metallicRoughness;
        }

        if (GetTexture0(definition, "normalTexture", srgb: false, "normal") is { } normalMap)
        {
            material.NormalMap = normalMap;
            var scale = TryGet(definition, "normalTexture", out var normalInfo) ? GetFloat(normalInfo, "scale", 1f) : 1f;
            material.NormalScale = new Vector2(scale, flipNormalY ? -scale : scale);
        }

        if (GetTexture(definition, "occlusionTexture", srgb: false, out var aoChannel) is { } aoMap)
        {
            material.AoMap = aoMap;
            _ = aoChannel;

            material.AoMapIntensity = TryGet(definition, "occlusionTexture", out var aoInfo) ? GetFloat(aoInfo, "strength", 1f) : 1f;
        }

        if (GetFloats(definition, "emissiveFactor", 3) is { } emissive)
        {
            material.Emissive = new ColorF(emissive[0], emissive[1], emissive[2]);
        }

        if (TryGetExtension(definition, "KHR_materials_emissive_strength", out var strength))
        {
            material.EmissiveIntensity = GetFloat(strength, "emissiveStrength", 1f);
        }

        material.EmissiveMap = GetTexture0(definition, "emissiveTexture", srgb: true, "emissive");
        if (material is PhysicalMaterial p)
        {
            ApplyPhysical(p, definition, flipNormalY);
        }

        return material;
    }

    private static bool HasAnyExtension(JsonElement element, params string[] names)
    {
        foreach (var name in names)
        {
            if (TryGetExtension(element, name, out _))
            {
                return true;
            }
        }

        return false;
    }

    private void ApplyPhysical(PhysicalMaterial material, JsonElement definition, bool flipTangentY)
    {
        if (TryGetExtension(definition, "KHR_materials_clearcoat", out var clearcoat))
        {
            material.Clearcoat = GetFloat(clearcoat, "clearcoatFactor", 0f);
            material.ClearcoatRoughness = GetFloat(clearcoat, "clearcoatRoughnessFactor", 0f);
            material.ClearcoatMap = GetTexture0(clearcoat, "clearcoatTexture", srgb: false, "clearcoat");
            material.ClearcoatRoughnessMap = GetTexture0(clearcoat, "clearcoatRoughnessTexture", srgb: false, "clearcoat roughness");
            if (GetTexture0(clearcoat, "clearcoatNormalTexture", srgb: false, "clearcoat normal") is { } clearcoatNormal)
            {
                material.ClearcoatNormalMap = clearcoatNormal;
                var scale = TryGet(clearcoat, "clearcoatNormalTexture", out var info) ? GetFloat(info, "scale", 1f) : 1f;
                material.ClearcoatNormalScale = new Vector2(scale, flipTangentY ? -scale : scale);
            }
        }

        if (TryGetExtension(definition, "KHR_materials_iridescence", out var iridescence))
        {
            material.Iridescence = GetFloat(iridescence, "iridescenceFactor", 0f);
            material.IridescenceIor = GetFloat(iridescence, "iridescenceIor", 1.3f);
            material.IridescenceThicknessRange = new Vector2(GetFloat(iridescence, "iridescenceThicknessMinimum", 100f), GetFloat(iridescence, "iridescenceThicknessMaximum", 400f));
            material.IridescenceMap = GetTexture0(iridescence, "iridescenceTexture", srgb: false, "iridescence");
            material.IridescenceThicknessMap = GetTexture0(iridescence, "iridescenceThicknessTexture", srgb: false, "iridescence thickness");
        }

        if (TryGetExtension(definition, "KHR_materials_anisotropy", out var anisotropy))
        {
            material.Anisotropy = GetFloat(anisotropy, "anisotropyStrength", 0f);
            // With the bitangent mirrored the angle turns the other way. A direction texture cannot be mirrored from
            // here: geometry that uses one should carry tangents (the specification asks for them).
            var rotation = GetFloat(anisotropy, "anisotropyRotation", 0f);
            material.AnisotropyRotation = flipTangentY ? -rotation : rotation;
            material.AnisotropyMap = GetTexture0(anisotropy, "anisotropyTexture", srgb: false, "anisotropy");
        }

        if (TryGetExtension(definition, "KHR_materials_sheen", out var sheen))
        {
            var tint = GetFloats(sheen, "sheenColorFactor", 3) ?? [0f, 0f, 0f];
            material.SheenColor = new ColorF(tint[0], tint[1], tint[2]);
            material.SheenRoughness = GetFloat(sheen, "sheenRoughnessFactor", 0f);
            material.Sheen = 1f;
            material.SheenColorMap = GetTexture0(sheen, "sheenColorTexture", srgb: true, "sheen color");
            material.SheenRoughnessMap = GetTexture0(sheen, "sheenRoughnessTexture", srgb: false, "sheen roughness");
        }

        if (TryGetExtension(definition, "KHR_materials_ior", out var ior))
        {
            material.Ior = GetFloat(ior, "ior", 1.5f);
        }

        if (TryGetExtension(definition, "KHR_materials_specular", out var specular))
        {
            material.SpecularIntensity = GetFloat(specular, "specularFactor", 1f);
            if (GetFloats(specular, "specularColorFactor", 3) is { } tint)
            {
                material.SpecularColor = new ColorF(tint[0], tint[1], tint[2]);
            }

            material.SpecularIntensityMap = GetTexture0(specular, "specularTexture", srgb: false, "specular");
            material.SpecularColorMap = GetTexture0(specular, "specularColorTexture", srgb: true, "specular color");
        }

        if (TryGetExtension(definition, "KHR_materials_transmission", out var transmission))
        {
            material.Transmission = GetFloat(transmission, "transmissionFactor", 0f);
            material.TransmissionMap = GetTexture0(transmission, "transmissionTexture", srgb: false, "transmission");
        }

        if (TryGetExtension(definition, "KHR_materials_volume", out var volume))
        {
            material.Thickness = GetFloat(volume, "thicknessFactor", 0f);
            if (GetFloatOrNull(volume, "attenuationDistance") is { } distance && float.IsFinite(distance) && distance > 0f)
            {
                material.AttenuationDistance = distance;
            }

            if (GetFloats(volume, "attenuationColor", 3) is { } tint)
            {
                material.AttenuationColor = new ColorF(tint[0], tint[1], tint[2]);
            }

            material.ThicknessMap = GetTexture0(volume, "thicknessTexture", srgb: false, "thickness");
        }
    }

    private void WarnTextures(JsonElement extension, string what, params string[] properties)
    {
        foreach (var property in properties)
        {
            if (TryGet(extension, property, out _))
            {
                Warn($"The {what} layer's {property} is not supported; only its factors are used.");
            }
        }
    }
}
