using System.Numerics;
using TKCode.Arc.Graphics3D.Shading;

namespace TKCode.Arc.Graphics3D;

/// <summary>A material that ignores lights: it shows its color as is.</summary>
public class BasicMaterial : Material
{
    private readonly UniformNode _color = Sl.Uniform(ColorF.White);
    private readonly MaterialMap _map = new();

    /// <summary>The color.</summary>
    public ColorF Color
    {
        get => _color.Color;
        set => _color.Color = value;
    }

    /// <summary>Color texture (sRGB); its alpha multiplies opacity.</summary>
    public Texture? Map
    {
        get => _map.Texture;
        set => SetMap(_map, value);
    }

    internal override void BuildSurface(Surface s)
    {
        s.Model = LightingModel.Unlit;
        ApplyColor(s, _color, _map);
    }
}

/// <summary>Shared properties of the lit materials: color, emissive, normal map and ambient occlusion.</summary>
public abstract class LitMaterial : Material
{
    private readonly UniformNode _color = Sl.Uniform(ColorF.White);
    private readonly UniformNode _emissive = Sl.Uniform(new Vector3(0f, 0f, 0f));
    private readonly UniformNode _normalScale = Sl.Uniform(Vector2.One);
    private readonly UniformNode _aoIntensity = Sl.Uniform(1f);
    private readonly MaterialMap _map = new();
    private readonly MaterialMap _emissiveMap = new();
    private readonly MaterialMap _normalMap = new();
    private readonly MaterialMap _aoMap = new();
    private ColorF _emissiveColor = ColorF.Black;
    private float _emissiveIntensity = 1f;
    private ShaderNode? _emissiveNode;

    /// <summary>The base color (albedo).</summary>
    public ColorF Color
    {
        get => _color.Color;
        set => _color.Color = value;
    }

    /// <summary>Base color texture (sRGB); its alpha multiplies opacity.</summary>
    public Texture? Map
    {
        get => _map.Texture;
        set => SetMap(_map, value);
    }

    /// <summary>Light the surface gives off by itself.</summary>
    public ColorF Emissive
    {
        get => _emissiveColor;
        set
        {
            var had = HasEmissive;
            _emissiveColor = value;
            UpdateEmissive(had);
        }
    }

    /// <summary>Multiplies <see cref="Emissive"/>.</summary>
    public float EmissiveIntensity
    {
        get => _emissiveIntensity;
        set
        {
            var had = HasEmissive;
            _emissiveIntensity = value;
            UpdateEmissive(had);
        }
    }

    /// <summary>Emissive texture (sRGB), multiplied by <see cref="Emissive"/>.</summary>
    public Texture? EmissiveMap
    {
        get => _emissiveMap.Texture;
        set => SetMap(_emissiveMap, value);
    }

    /// <summary>Tangent-space normal map.</summary>
    public Texture? NormalMap
    {
        get => _normalMap.Texture;
        set => SetMap(_normalMap, value);
    }

    /// <summary>Strength of the normal map in x and y.</summary>
    public Vector2 NormalScale
    {
        get => _normalScale.Vector2;
        set => _normalScale.Vector2 = value;
    }

    /// <summary>Ambient occlusion texture (red channel); darkens indirect light. Often laid out on the second UV set:
    /// set the texture's <see cref="Texture.Channel"/> to 1 then.</summary>
    public Texture? AoMap
    {
        get => _aoMap.Texture;
        set => SetMap(_aoMap, value);
    }

    /// <summary>Strength of <see cref="AoMap"/>.</summary>
    public float AoMapIntensity
    {
        get => _aoIntensity.Float;
        set => _aoIntensity.Float = value;
    }

    /// <summary>Replaces emissive light (vec3).</summary>
    public ShaderNode? EmissiveNode
    {
        get => _emissiveNode;
        set => Set(ref _emissiveNode, value);
    }

    private bool HasEmissive => _emissiveIntensity > 0f && (_emissiveColor.R > 0f || _emissiveColor.G > 0f || _emissiveColor.B > 0f);

    private void UpdateEmissive(bool had)
    {
        _emissive.Vector3 = _emissiveColor.ToVector3() * _emissiveIntensity;
        if (had != HasEmissive)
        {
            Invalidate();
        }
    }

    private protected void BuildLit(Surface s)
    {
        ApplyColor(s, _color, _map);
        if (_normalMap.Texture is not null)
        {
            s.Normal = Sl.NormalMap(_normalMap.Sample(), _normalScale, _normalMap.TransformedUv());
        }

        if (_aoMap.Texture is not null)
        {
            s.Ao = Sl.Mix(1f, _aoMap.Sample().X, _aoIntensity);
        }

        if (_emissiveNode is not null)
        {
            s.Emissive = Sl.ToVec3(_emissiveNode);
        }
        else if (HasEmissive)
        {
            ShaderNode e = _emissive;
            if (_emissiveMap.Texture is not null)
            {
                e = e * _emissiveMap.Sample().Xyz;
            }

            s.Emissive = e;
        }
    }
}

/// <summary>Matte surfaces: diffuse light only, no highlights. The cheapest lit material.</summary>
public class LambertMaterial : LitMaterial
{
    internal override void BuildSurface(Surface s)
    {
        s.Model = LightingModel.Lambert;
        BuildLit(s);
    }
}

/// <summary>Shiny surfaces with a Blinn-Phong highlight. Not physically based; cheap.</summary>
public class PhongMaterial : LitMaterial
{
    private readonly UniformNode _specular = Sl.Uniform(ColorF.FromHex(0x111111));
    private readonly UniformNode _shininess = Sl.Uniform(30f);

    /// <summary>The highlight color.</summary>
    public ColorF Specular
    {
        get => _specular.Color;
        set => _specular.Color = value;
    }

    /// <summary>How tight the highlight is.</summary>
    public float Shininess
    {
        get => _shininess.Float;
        set => _shininess.Float = value;
    }

    internal override void BuildSurface(Surface s)
    {
        s.Model = LightingModel.Phong;
        BuildLit(s);
        s.SpecularColor = _specular;
        s.Shininess = _shininess;
    }
}

/// <summary>Cartoon shading: light falls off in a few flat steps.</summary>
public class ToonMaterial : LitMaterial
{
    private int _steps = 3;

    /// <summary>Number of brightness steps (2 to 8).</summary>
    public int Steps
    {
        get => _steps;
        set => Set(ref _steps, Math.Clamp(value, 2, 8));
    }

    internal override void BuildSurface(Surface s)
    {
        s.Model = LightingModel.Toon;
        s.ToonSteps = _steps;
        BuildLit(s);
    }
}

/// <summary>The physically based material (metallic-roughness workflow, the glTF model): the default choice for
/// realistic surfaces.</summary>
public class StandardMaterial : LitMaterial
{
    private readonly UniformNode _roughness = Sl.Uniform(1f);
    private readonly UniformNode _metalness = Sl.Uniform(0f);
    private readonly UniformNode _envIntensity = Sl.Uniform(1f);
    private readonly MaterialMap _roughnessMap = new();
    private readonly MaterialMap _metalnessMap = new();
    private ShaderNode? _roughnessNode;
    private ShaderNode? _metalnessNode;

    /// <summary>0 is a mirror, 1 is fully diffuse.</summary>
    public float Roughness
    {
        get => _roughness.Float;
        set => _roughness.Float = value;
    }

    /// <summary>0 for non-metals, 1 for metals.</summary>
    public float Metalness
    {
        get => _metalness.Float;
        set => _metalness.Float = value;
    }

    /// <summary>Roughness texture (green channel), multiplied by <see cref="Roughness"/>.</summary>
    public Texture? RoughnessMap
    {
        get => _roughnessMap.Texture;
        set
        {
            SetMap(_roughnessMap, value);
            Invalidate();
        }
    }

    /// <summary>Metalness texture (blue channel), multiplied by <see cref="Metalness"/>.</summary>
    public Texture? MetalnessMap
    {
        get => _metalnessMap.Texture;
        set
        {
            SetMap(_metalnessMap, value);
            Invalidate();
        }
    }

    /// <summary>Multiplies the scene environment's contribution to this material.</summary>
    public float EnvMapIntensity
    {
        get => _envIntensity.Float;
        set => _envIntensity.Float = value;
    }

    /// <summary>Replaces roughness (float).</summary>
    public ShaderNode? RoughnessNode
    {
        get => _roughnessNode;
        set => Set(ref _roughnessNode, value);
    }

    /// <summary>Replaces metalness (float).</summary>
    public ShaderNode? MetalnessNode
    {
        get => _metalnessNode;
        set => Set(ref _metalnessNode, value);
    }

    private protected void BuildStandard(Surface s)
    {
        BuildLit(s);
        ShaderNode roughness = _roughness;
        ShaderNode metalness = _metalness;
        if (_roughnessMap.Texture is not null)
        {
            roughness = roughness * _roughnessMap.Sample().Y;
        }

        if (_metalnessMap.Texture is not null)
        {
            // glTF packs both into one image: sample it once when it is the same texture with the same transform.
            var sample = ReferenceEquals(_metalnessMap.Texture, _roughnessMap.Texture) ? _roughnessMap.Sample() : _metalnessMap.Sample();
            metalness = metalness * sample.Z;
        }

        s.Roughness = _roughnessNode is not null ? Sl.ToFloat(_roughnessNode) : roughness;
        s.Metalness = _metalnessNode is not null ? Sl.ToFloat(_metalnessNode) : metalness;
        s.EnvIntensity = _envIntensity;
    }

    internal override void BuildSurface(Surface s)
    {
        s.Model = LightingModel.Standard;
        BuildStandard(s);
    }
}

/// <summary><see cref="StandardMaterial"/> plus the layers of the glTF material extensions: clear coat, sheen,
/// specular tint, index of refraction, transmission, thin-film iridescence and anisotropy.</summary>
public class PhysicalMaterial : StandardMaterial
{
    private readonly UniformNode _clearcoat = Sl.Uniform(0f);
    private readonly UniformNode _clearcoatRoughness = Sl.Uniform(0f);
    private readonly UniformNode _sheenColor = Sl.Uniform(new Vector3(0f, 0f, 0f));
    private readonly UniformNode _sheenRoughness = Sl.Uniform(1f);
    private readonly UniformNode _specularIntensity = Sl.Uniform(1f);
    private readonly UniformNode _specularColor = Sl.Uniform(ColorF.White);
    private readonly UniformNode _ior = Sl.Uniform(1.5f);
    private readonly UniformNode _transmission = Sl.Uniform(0f);
    private readonly UniformNode _thickness = Sl.Uniform(0f);
    private readonly UniformNode _attenuationColor = Sl.Uniform(ColorF.White);
    private readonly UniformNode _attenuationDistance = Sl.Uniform(1e9f);
    private readonly MaterialMap _clearcoatMap = new();
    private readonly MaterialMap _clearcoatRoughnessMap = new();
    private readonly MaterialMap _sheenColorMap = new();
    private readonly MaterialMap _sheenRoughnessMap = new();
    private readonly MaterialMap _specularIntensityMap = new();
    private readonly MaterialMap _specularColorMap = new();
    private readonly MaterialMap _transmissionMap = new();
    private readonly MaterialMap _thicknessMap = new();
    private readonly MaterialMap _clearcoatNormalMap = new();
    private readonly MaterialMap _iridescenceMap = new();
    private readonly MaterialMap _iridescenceThicknessMap = new();
    private readonly MaterialMap _anisotropyMap = new();
    private readonly UniformNode _clearcoatNormalScale = Sl.Uniform(Vector2.One);
    private readonly UniformNode _iridescence = Sl.Uniform(0f);
    private readonly UniformNode _iridescenceIor = Sl.Uniform(1.3f);
    private readonly UniformNode _iridescenceThickness = Sl.Uniform(new Vector2(100f, 400f));
    private readonly UniformNode _anisotropyVector = Sl.Uniform(Vector2.Zero);
    private bool _hasIridescence;
    private bool _hasAnisotropy;
    private float _anisotropy;
    private float _anisotropyRotation;
    private bool _hasClearcoat;
    private bool _hasSheen;
    private bool _hasTransmission;
    private float _sheen;
    private ColorF _sheenTint = ColorF.Black;

    /// <summary>Clear coat strength texture (red channel), multiplied by <see cref="Clearcoat"/>.</summary>
    public Texture? ClearcoatMap
    {
        get => _clearcoatMap.Texture;
        set => SetLayerMap(_clearcoatMap, value);
    }

    /// <summary>Clear coat roughness texture (green channel), multiplied by <see cref="ClearcoatRoughness"/>.</summary>
    public Texture? ClearcoatRoughnessMap
    {
        get => _clearcoatRoughnessMap.Texture;
        set => SetLayerMap(_clearcoatRoughnessMap, value);
    }

    /// <summary>Sheen color texture (sRGB), multiplied by <see cref="SheenColor"/>.</summary>
    public Texture? SheenColorMap
    {
        get => _sheenColorMap.Texture;
        set => SetLayerMap(_sheenColorMap, value);
    }

    /// <summary>Sheen roughness texture (alpha channel), multiplied by <see cref="SheenRoughness"/>.</summary>
    public Texture? SheenRoughnessMap
    {
        get => _sheenRoughnessMap.Texture;
        set => SetLayerMap(_sheenRoughnessMap, value);
    }

    /// <summary>Specular strength texture (alpha channel), multiplied by <see cref="SpecularIntensity"/>.</summary>
    public Texture? SpecularIntensityMap
    {
        get => _specularIntensityMap.Texture;
        set => SetLayerMap(_specularIntensityMap, value);
    }

    /// <summary>Specular tint texture (sRGB), multiplied by <see cref="SpecularColor"/>.</summary>
    public Texture? SpecularColorMap
    {
        get => _specularColorMap.Texture;
        set => SetLayerMap(_specularColorMap, value);
    }

    /// <summary>Transmission texture (red channel), multiplied by <see cref="Transmission"/>.</summary>
    public Texture? TransmissionMap
    {
        get => _transmissionMap.Texture;
        set => SetLayerMap(_transmissionMap, value);
    }

    /// <summary>Thickness texture (green channel), multiplied by <see cref="Thickness"/>.</summary>
    public Texture? ThicknessMap
    {
        get => _thicknessMap.Texture;
        set => SetLayerMap(_thicknessMap, value);
    }

    /// <summary>Normal map of the clear coat layer alone (tangent space), for a coat that is smoother or bumpier
    /// than what it covers. Without it the coat follows the geometry's normal.</summary>
    public Texture? ClearcoatNormalMap
    {
        get => _clearcoatNormalMap.Texture;
        set => SetLayerMap(_clearcoatNormalMap, value);
    }

    /// <summary>Strength of <see cref="ClearcoatNormalMap"/> along the tangent and bitangent.</summary>
    public Vector2 ClearcoatNormalScale
    {
        get => _clearcoatNormalScale.Vector2;
        set => _clearcoatNormalScale.Vector2 = value;
    }

    /// <summary>Strength of the thin-film effect (soap bubbles, oil, tempered metal), 0..1: the reflection changes
    /// color with the viewing angle and the film's thickness.</summary>
    public float Iridescence
    {
        get => _iridescence.Float;
        set
        {
            _iridescence.Float = value;
            Set(ref _hasIridescence, value > 0f);
        }
    }

    /// <summary>Index of refraction of the thin film.</summary>
    public float IridescenceIor
    {
        get => _iridescenceIor.Float;
        set => _iridescenceIor.Float = value;
    }

    /// <summary>Thickness of the thin film in nanometers: X where <see cref="IridescenceThicknessMap"/> is 0, Y where
    /// it is 1. Without the map the film has thickness Y everywhere.</summary>
    public Vector2 IridescenceThicknessRange
    {
        get => _iridescenceThickness.Vector2;
        set => _iridescenceThickness.Vector2 = value;
    }

    /// <summary>Iridescence strength texture (red channel), multiplied by <see cref="Iridescence"/>.</summary>
    public Texture? IridescenceMap
    {
        get => _iridescenceMap.Texture;
        set => SetLayerMap(_iridescenceMap, value);
    }

    /// <summary>Film thickness texture (green channel), mapped onto <see cref="IridescenceThicknessRange"/>.</summary>
    public Texture? IridescenceThicknessMap
    {
        get => _iridescenceThicknessMap.Texture;
        set => SetLayerMap(_iridescenceThicknessMap, value);
    }

    /// <summary>How far highlights stretch across one direction of the surface (brushed metal, hair, satin), 0..1.</summary>
    public float Anisotropy
    {
        get => _anisotropy;
        set
        {
            _anisotropy = value;
            UpdateAnisotropy();
        }
    }

    /// <summary>Angle of the anisotropy direction from the tangent toward the bitangent, in radians.</summary>
    public float AnisotropyRotation
    {
        get => _anisotropyRotation;
        set
        {
            _anisotropyRotation = value;
            UpdateAnisotropy();
        }
    }

    /// <summary>Anisotropy texture: red and green hold the direction in tangent space (0.5 is zero), blue the
    /// strength; both are combined with <see cref="AnisotropyRotation"/> and <see cref="Anisotropy"/>.</summary>
    public Texture? AnisotropyMap
    {
        get => _anisotropyMap.Texture;
        set => SetLayerMap(_anisotropyMap, value);
    }

    private void UpdateAnisotropy()
    {
        var strength = Math.Clamp(_anisotropy, 0f, 1f);
        _anisotropyVector.Vector2 = new Vector2(MathF.Cos(_anisotropyRotation), MathF.Sin(_anisotropyRotation)) * strength;
        Set(ref _hasAnisotropy, strength > 0f);
    }

    // Layer maps are often channels of one image: always rebuild, so that equal textures are sampled once.
    private void SetLayerMap(MaterialMap map, Texture? texture)
    {
        SetMap(map, texture);
        Invalidate();
    }

    /// <summary>Strength of a clear lacquer layer on top, 0..1.</summary>
    public float Clearcoat
    {
        get => _clearcoat.Float;
        set
        {
            _clearcoat.Float = value;
            Set(ref _hasClearcoat, value > 0f);
        }
    }

    /// <summary>Roughness of the clear coat layer.</summary>
    public float ClearcoatRoughness
    {
        get => _clearcoatRoughness.Float;
        set => _clearcoatRoughness.Float = value;
    }

    /// <summary>Strength of the fabric-like sheen layer, 0..1.</summary>
    public float Sheen
    {
        get => _sheen;
        set
        {
            _sheen = value;
            UpdateSheen();
        }
    }

    /// <summary>Color of the sheen layer.</summary>
    public ColorF SheenColor
    {
        get => _sheenTint;
        set
        {
            _sheenTint = value;
            UpdateSheen();
        }
    }

    /// <summary>Roughness of the sheen layer.</summary>
    public float SheenRoughness
    {
        get => _sheenRoughness.Float;
        set => _sheenRoughness.Float = value;
    }

    /// <summary>Strength of the specular reflection of non-metals, 0..1.</summary>
    public float SpecularIntensity
    {
        get => _specularIntensity.Float;
        set => _specularIntensity.Float = value;
    }

    /// <summary>Tint of the specular reflection of non-metals.</summary>
    public ColorF SpecularColor
    {
        get => _specularColor.Color;
        set => _specularColor.Color = value;
    }

    /// <summary>Index of refraction (1 to 2.333).</summary>
    public float Ior
    {
        get => _ior.Float;
        set => _ior.Float = Math.Clamp(value, 1f, 2.333f);
    }

    /// <summary>How much light passes through the surface, 0..1 (glass, water). Needs <see cref="Material.Transparent"/>
    /// to be false: the renderer draws transmissive objects in their own pass.</summary>
    public float Transmission
    {
        get => _transmission.Float;
        set
        {
            _transmission.Float = value;
            Set(ref _hasTransmission, value > 0f);
        }
    }

    /// <summary>Thickness of the volume behind the surface, in world units; 0 means thin-walled.</summary>
    public float Thickness
    {
        get => _thickness.Float;
        set => _thickness.Float = value;
    }

    /// <summary>The color white light takes after travelling <see cref="AttenuationDistance"/> through the volume.</summary>
    public ColorF AttenuationColor
    {
        get => _attenuationColor.Color;
        set => _attenuationColor.Color = value;
    }

    /// <summary>The distance at which <see cref="AttenuationColor"/> is reached.</summary>
    public float AttenuationDistance
    {
        get => _attenuationDistance.Float;
        set => _attenuationDistance.Float = value;
    }

    internal bool HasTransmission => _hasTransmission;

    private void UpdateSheen()
    {
        _sheenColor.Vector3 = _sheenTint.ToVector3() * _sheen;
        Set(ref _hasSheen, _sheen > 0f);
    }

    internal override void BuildSurface(Surface s)
    {
        s.Model = LightingModel.Physical;
        BuildStandard(s);
        s.SpecularIntensity = Times(_specularIntensity, _specularIntensityMap, "w");
        s.SpecularColor = Times(_specularColor, _specularColorMap, "xyz");
        s.Ior = _ior;
        if (_hasClearcoat)
        {
            s.Clearcoat = Times(_clearcoat, _clearcoatMap, "x");
            s.ClearcoatRoughness = Times(_clearcoatRoughness, _clearcoatRoughnessMap, "y");
            if (_clearcoatNormalMap.Texture is not null)
            {
                s.ClearcoatNormal = Sl.NormalMap(_clearcoatNormalMap.Sample(), _clearcoatNormalScale, _clearcoatNormalMap.TransformedUv());
            }
        }

        if (_hasIridescence)
        {
            s.Iridescence = _iridescenceMap.Texture is null ? _iridescence : _iridescence * _iridescenceMap.Sample().X;
            s.IridescenceIor = _iridescenceIor;
            if (_iridescenceThicknessMap.Texture is null)
            {
                s.IridescenceThickness = _iridescenceThickness.Y;
            }
            else
            {
                // glTF packs strength and thickness into one image: sample it once.
                var sample = ReferenceEquals(_iridescenceThicknessMap.Texture, _iridescenceMap.Texture) ? _iridescenceMap.Sample() : _iridescenceThicknessMap.Sample();
                s.IridescenceThickness = Sl.Mix(_iridescenceThickness.X, _iridescenceThickness.Y, sample.Y);
            }
        }

        if (_hasAnisotropy)
        {
            ShaderNode direction = _anisotropyVector;
            ShaderNode? uv = null;
            if (_anisotropyMap.Texture is not null)
            {
                // The texture's direction, turned by the material's rotation and scaled by both strengths.
                var sample = _anisotropyMap.Sample();
                var texel = Sl.Normalize((sample.Swizzle("xy") * 2f) - 1f);
                ShaderNode v = _anisotropyVector;
                direction = Sl.Vec2((v.X * texel.X) - (v.Y * texel.Y), (v.Y * texel.X) + (v.X * texel.Y)) * sample.Z;
                uv = _anisotropyMap.TransformedUv();
            }

            s.Anisotropy = Sl.AnisotropyDirection(direction, uv);
        }

        if (_hasSheen)
        {
            s.SheenColor = Times(_sheenColor, _sheenColorMap, "xyz");
            s.SheenRoughness = Times(_sheenRoughness, _sheenRoughnessMap, "w");
        }

        if (_hasTransmission)
        {
            s.Transmission = Times(_transmission, _transmissionMap, "x");
            s.Thickness = Times(_thickness, _thicknessMap, "y");
            s.AttenuationColor = _attenuationColor;
            s.AttenuationDistance = _attenuationDistance;
        }
    }

    /// <summary>A factor times the named channels of its map, when it has one. Maps that are the same texture share
    /// one sample.</summary>
    private ShaderNode Times(UniformNode factor, MaterialMap map, string channels)
    {
        if (map.Texture is null)
        {
            return factor;
        }

        MaterialMap[] all = [_clearcoatMap, _clearcoatRoughnessMap, _sheenColorMap, _sheenRoughnessMap, _specularIntensityMap, _specularColorMap, _transmissionMap, _thicknessMap];
        foreach (var other in all)
        {
            if (ReferenceEquals(other, map))
            {
                break;
            }

            if (ReferenceEquals(other.Texture, map.Texture))
            {
                return factor * other.Sample().Swizzle(channels);
            }
        }

        return factor * map.Sample().Swizzle(channels);
    }
}

/// <summary>Shows the surface normal as a color; for debugging.</summary>
public class NormalMaterial : Material
{
    internal override void BuildSurface(Surface s) => s.Model = LightingModel.Normal;
}

/// <summary>Invisible except where shadows fall: draws received shadows as a transparent dark layer (for a ground
/// plane under a model shown over a UI background).</summary>
public class ShadowMaterial : Material
{
    private readonly UniformNode _color = Sl.Uniform(ColorF.Black);

    /// <summary>Creates the material; it is transparent by default.</summary>
    public ShadowMaterial()
    {
        Transparent = true;
        Opacity = 0.5f;
    }

    /// <summary>The shadow color.</summary>
    public ColorF Color
    {
        get => _color.Color;
        set => _color.Color = value;
    }

    internal override void BuildSurface(Surface s)
    {
        s.Model = LightingModel.ShadowCatcher;
        s.Color = _color;
        s.Opacity = OpacityUniform;
    }
}

/// <summary>A material defined entirely by nodes. Set <see cref="Material.ColorNode"/> and the other node properties;
/// <see cref="Lit"/> chooses between unlit output and physically based lighting.</summary>
public class NodeMaterial : StandardMaterial
{
    private bool _lit = true;

    /// <summary>True (default) lights the surface like <see cref="StandardMaterial"/>; false outputs the color as is.</summary>
    public bool Lit
    {
        get => _lit;
        set => Set(ref _lit, value);
    }

    internal override void BuildSurface(Surface s)
    {
        if (_lit)
        {
            base.BuildSurface(s);
        }
        else
        {
            s.Model = LightingModel.Unlit;
            s.Opacity = OpacityUniform;
        }
    }
}
