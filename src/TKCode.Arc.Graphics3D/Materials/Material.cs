using System.Numerics;
using TKCode.Arc.Graphics3D.Rhi;
using TKCode.Arc.Graphics3D.Shading;

namespace TKCode.Arc.Graphics3D;

/// <summary>Which faces of a triangle are drawn.</summary>
public enum Side
{
    Front,
    Back,
    Double,
}

/// <summary>How a surface responds to light.</summary>
internal enum LightingModel
{
    Unlit,
    Lambert,
    Phong,
    Toon,
    Standard,
    Physical,
    Normal,
    ShadowCatcher,
}

/// <summary>How the vertex stage places a material's geometry.</summary>
internal enum VertexKind
{
    Mesh,
    Points,
    Sprite,
}

/// <summary>What a material hands to the shader generator: one node per surface property. Null means "the default".</summary>
internal sealed class Surface
{
    public LightingModel Model;
    public VertexKind Kind;
    public ShaderNode? Position;
    public ShaderNode Color = new Vector3(1f, 1f, 1f);
    public ShaderNode Opacity = 1f;
    public ShaderNode? AlphaTest;
    public ShaderNode? Normal;
    public ShaderNode? Emissive;
    public ShaderNode? Roughness;
    public ShaderNode? Metalness;
    public ShaderNode? Ao;
    public ShaderNode? EnvIntensity;
    public ShaderNode? SpecularColor;
    public ShaderNode? SpecularIntensity;
    public ShaderNode? Shininess;
    public ShaderNode? Ior;
    public ShaderNode? Clearcoat;
    public ShaderNode? ClearcoatRoughness;
    public ShaderNode? ClearcoatNormal;
    public ShaderNode? Iridescence;
    public ShaderNode? IridescenceIor;
    public ShaderNode? IridescenceThickness;
    public ShaderNode? Anisotropy;
    public ShaderNode? SheenColor;
    public ShaderNode? SheenRoughness;
    public ShaderNode? Transmission;
    public ShaderNode? Thickness;
    public ShaderNode? AttenuationColor;
    public ShaderNode? AttenuationDistance;
    public ShaderNode? PointSize;
    public ShaderNode? SpriteRotation;
    public bool SizeAttenuation = true;
    public bool FlatShading;
    public bool Fog = true;
    public bool ToneMapped = true;
    public int ToonSteps = 3;
}

/// <summary>A texture property of a built-in material: the slot, plus the texture's UV transform as uniforms that
/// follow <see cref="Texture.Offset"/>, <see cref="Texture.Repeat"/> and <see cref="Texture.Rotation"/>.</summary>
internal sealed class MaterialMap
{
    private readonly UniformNode _row0 = Sl.Uniform(new Vector3(1f, 0f, 0f));
    private readonly UniformNode _row1 = Sl.Uniform(new Vector3(0f, 1f, 0f));
    private ShaderNode? _sample;
    private int _sampleChannel = -1;

    public MaterialMap()
    {
        _row0.OnUpdate = u => u.Vector3 = Slot.Texture?.GetUvTransform().Row0 ?? new Vector3(1f, 0f, 0f);
        _row1.OnUpdate = u => u.Vector3 = Slot.Texture?.GetUvTransform().Row1 ?? new Vector3(0f, 1f, 0f);
    }

    public TextureSlot Slot { get; } = new();

    public Texture? Texture => Slot.Texture;

    /// <summary>Which UV set the map uses: 0 or 1 (<see cref="Graphics3D.Texture.Channel"/>).</summary>
    public int Channel => Slot.Texture is { Channel: > 0 } ? 1 : 0;

    /// <summary>The RGBA sample (one node, however often it is used).</summary>
    public ShaderNode Sample()
    {
        if (_sample is null || _sampleChannel != Channel)
        {
            _sample = Sl.Texture(Slot, TransformedUv());
            _sampleChannel = Channel;
        }

        return _sample;
    }

    /// <summary>The UV coordinate after the texture's transform, computed per vertex.</summary>
    public ShaderNode TransformedUv()
    {
        var uv = Sl.Vec3(Channel == 0 ? Sl.Uv : Sl.Uv1, 1f);
        return Sl.Varying(Sl.Vec2(Sl.Dot(_row0, uv), Sl.Dot(_row1, uv)));
    }
}

/// <summary>Base of all materials: the state shared by every way of shading a surface. Concrete materials add their
/// own properties; all of them are built from shader nodes, and the <c>...Node</c> properties replace a built-in
/// property with any node graph.</summary>
public abstract class Material : IDisposable
{
    private static int s_nextId;
    private readonly UniformNode _opacity = Sl.Uniform(1f);
    private readonly UniformNode _alphaTest = Sl.Uniform(0f);
    private bool _vertexColors;
    private bool _flatShading;
    private bool _fog = true;
    private bool _toneMapped = true;
    private ShaderNode? _positionNode;
    private ShaderNode? _colorNode;
    private ShaderNode? _opacityNode;
    private ShaderNode? _normalNode;
    private Surface? _surface;
    private int _surfaceVersion = -1;

    /// <summary>A process-wide unique number (used in sort keys).</summary>
    public int Id { get; } = Interlocked.Increment(ref s_nextId);

    /// <summary>A name for debugging.</summary>
    public string Name { get; set; } = "";

    /// <summary>Which faces are drawn.</summary>
    public Side Side { get; set; } = Side.Front;

    /// <summary>True draws the object blended, after all opaque objects, back to front.</summary>
    public bool Transparent { get; set; }

    /// <summary>How a transparent object is blended.</summary>
    public BlendMode Blending { get; set; } = BlendMode.Alpha;

    /// <summary>Overall opacity, 0..1. Has a visible effect only with <see cref="Transparent"/> or <see cref="AlphaTest"/>.</summary>
    public float Opacity
    {
        get => _opacity.Float;
        set => _opacity.Float = value;
    }

    /// <summary>Pixels with opacity below this value are discarded; 0 turns the test off.</summary>
    public float AlphaTest
    {
        get => _alphaTest.Float;
        set
        {
            var had = _alphaTest.Float > 0f;
            _alphaTest.Float = value;
            if (had != value > 0f)
            {
                Invalidate();
            }
        }
    }

    /// <summary>With multisampling, turns opacity into coverage: smooth cut-out edges without sorting.</summary>
    public bool AlphaToCoverage { get; set; }

    public bool DepthTest { get; set; } = true;
    public bool DepthWrite { get; set; } = true;
    public CompareFunction DepthCompare { get; set; } = CompareFunction.LessEqual;
    public bool ColorWrite { get; set; } = true;

    /// <summary>Slope-scaled depth offset, to draw coplanar surfaces without flicker.</summary>
    public float PolygonOffsetFactor { get; set; }

    /// <summary>Constant depth offset.</summary>
    public float PolygonOffsetUnits { get; set; }

    /// <summary>False skips every object drawn with the material.</summary>
    public bool Visible { get; set; } = true;

    /// <summary>True multiplies the color by the geometry's <c>color</c> attribute.</summary>
    public bool VertexColors
    {
        get => _vertexColors;
        set => Set(ref _vertexColors, value);
    }

    /// <summary>True shades each triangle with its face normal.</summary>
    public bool FlatShading
    {
        get => _flatShading;
        set => Set(ref _flatShading, value);
    }

    /// <summary>False keeps scene fog off this material.</summary>
    public bool Fog
    {
        get => _fog;
        set => Set(ref _fog, value);
    }

    /// <summary>False bypasses the renderer's tone mapping (for UI-like overlays).</summary>
    public bool ToneMapped
    {
        get => _toneMapped;
        set => Set(ref _toneMapped, value);
    }

    /// <summary>Replaces the vertex position (vec3, object space). Use <see cref="Sl.PositionLocal"/> for the original.</summary>
    public ShaderNode? PositionNode
    {
        get => _positionNode;
        set => Set(ref _positionNode, value);
    }

    /// <summary>Replaces the base color (vec3, or vec4 whose alpha multiplies opacity).</summary>
    public ShaderNode? ColorNode
    {
        get => _colorNode;
        set => Set(ref _colorNode, value);
    }

    /// <summary>Replaces opacity (float).</summary>
    public ShaderNode? OpacityNode
    {
        get => _opacityNode;
        set => Set(ref _opacityNode, value);
    }

    /// <summary>Replaces the shading normal (vec3, world space).</summary>
    public ShaderNode? NormalNode
    {
        get => _normalNode;
        set => Set(ref _normalNode, value);
    }

    /// <summary>Changes whenever the generated shader has to change.</summary>
    public int Version { get; private set; }

    /// <summary>True after <see cref="Dispose"/>.</summary>
    public bool IsDisposed { get; private set; }

    internal ResourceBinding? Binding;

    internal UniformNode OpacityUniform => _opacity;

    /// <summary>Forces the shader to be generated again. Property setters call it when needed.</summary>
    public void Invalidate() => Version++;

    private protected void Set<T>(ref T field, T value)
    {
        if (!EqualityComparer<T>.Default.Equals(field, value))
        {
            field = value;
            Invalidate();
        }
    }

    /// <summary>Sets a map, invalidating the shader when a texture appears or disappears.</summary>
    private protected void SetMap(MaterialMap map, Texture? texture)
    {
        var had = map.Slot.Texture is not null;
        map.Slot.Texture = texture;
        if (had != texture is not null)
        {
            Invalidate();
        }
    }

    /// <summary>The surface description, rebuilt when the material changed.</summary>
    internal Surface GetSurface()
    {
        if (_surface is null || _surfaceVersion != Version)
        {
            var s = new Surface { FlatShading = _flatShading, Fog = _fog, ToneMapped = _toneMapped, Position = _positionNode };
            BuildSurface(s);
            if (_colorNode is not null)
            {
                s.Color = Sl.ToVec3(_colorNode);
                if (_colorNode.Type == ShaderType.Vec4)
                {
                    s.Opacity = s.Opacity * _colorNode.W;
                }
            }

            if (_opacityNode is not null)
            {
                s.Opacity = Sl.ToFloat(_opacityNode);
            }

            if (_normalNode is not null)
            {
                s.Normal = Sl.ToVec3(_normalNode);
            }

            if (_alphaTest.Float > 0f)
            {
                s.AlphaTest = _alphaTest;
            }

            _surface = s;
            _surfaceVersion = Version;
        }

        return _surface;
    }

    /// <summary>Fills the surface from the material's properties.</summary>
    internal abstract void BuildSurface(Surface surface);

    /// <summary>Base color and opacity from a color uniform, an optional map and optional vertex colors.</summary>
    private protected void ApplyColor(Surface s, UniformNode color, MaterialMap map)
    {
        ShaderNode c = color;
        ShaderNode a = _opacity;
        if (map.Texture is not null)
        {
            var t = map.Sample();
            c = c * t.Xyz;
            a = a * t.W;
        }

        if (_vertexColors)
        {
            c = c * Sl.VertexColor.Xyz;
            a = a * Sl.VertexColor.W;
        }

        s.Color = c;
        s.Opacity = a;
    }

    /// <summary>The fixed-function state the material asks for.</summary>
    internal RenderState GetRenderState(bool mirrored, bool depthPass)
    {
        var state = new RenderState
        {
            Cull = Side switch
            {
                Side.Double => CullMode.None,
                Side.Back => CullMode.Front,
                _ => CullMode.Back,
            },
            FrontFaceClockwise = mirrored,
            DepthTest = DepthTest,
            DepthWrite = DepthWrite,
            DepthCompare = DepthCompare,
            ColorWrite = ColorWrite,
            Blend = Transparent ? Blending : BlendMode.Opaque,
            AlphaToCoverage = AlphaToCoverage,
            DepthBiasSlope = PolygonOffsetFactor,
            DepthBiasConstant = PolygonOffsetUnits,
        };
        if (depthPass)
        {
            state.Blend = BlendMode.Opaque;
            state.ColorWrite = false;
            state.DepthWrite = true;
            state.DepthTest = true;
            state.DepthCompare = CompareFunction.LessEqual;
            state.AlphaToCoverage = false;
        }

        return state;
    }

    /// <summary>Frees the GPU objects of this material in every renderer that used it.</summary>
    public void Dispose()
    {
        if (IsDisposed)
        {
            return;
        }

        IsDisposed = true;
        ResourceBinding.ReleaseAll(ref Binding);
        GC.SuppressFinalize(this);
    }
}
