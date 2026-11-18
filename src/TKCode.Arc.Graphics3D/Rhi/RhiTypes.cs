namespace TKCode.Arc.Graphics3D.Rhi;

/// <summary>The shading language a device compiles.</summary>
public enum ShaderLanguage
{
    /// <summary>GLSL ES 3.00 (OpenGL ES 3.0, WebGL 2, ANGLE).</summary>
    GlslEs300,

    /// <summary>GLSL 3.30 core (desktop OpenGL 3.3 and later; macOS gives a 4.1 context, which takes it too).</summary>
    Glsl330,

    /// <summary>GLSL 4.50 as Vulkan and WebGPU implementations take it: every resource names its binding, textures and
    /// samplers are separate objects, and stage inputs and outputs carry locations. The device's slots map to
    /// bindings like this: the uniform block of slot <c>s</c> is <c>layout(set = 0, binding = s)</c>; the texture of
    /// unit <c>u</c> is <c>layout(set = 1, binding = 2u)</c> and its sampler <c>layout(set = 1, binding = 2u + 1)</c>;
    /// the storage buffer of slot <c>s</c> (compute programs) is <c>layout(set = 2, binding = s)</c>; the draw
    /// constants are one <c>layout(push_constant)</c> block holding an <c>ivec4</c>.</summary>
    Glsl450,
}

/// <summary>The clip-space depth range of a device.</summary>
public enum ClipDepthRange
{
    /// <summary>-1..1 (OpenGL).</summary>
    NegativeOneToOne,

    /// <summary>0..1 (WebGPU, Direct3D, Metal, Vulkan).</summary>
    ZeroToOne,
}

/// <summary>What a buffer is bound as.</summary>
public enum BufferUsage
{
    Vertex,
    Index,
    Uniform,

    /// <summary>Read and written by compute programs (<see cref="GraphicsDevice.SetStorageBuffer"/>), readable with
    /// <see cref="GraphicsDevice.ReadBuffer"/>, and usable as a vertex buffer of a vertex input. Only on devices with
    /// <see cref="DeviceCapabilities.ComputeShaders"/>.</summary>
    Storage,
}

/// <summary>The shape of a texture.</summary>
public enum TextureDimension
{
    Texture2D,
    Cube,
    Texture2DArray,
    Texture3D,
}

/// <summary>Pixel formats. Names give channel order, bits and type; <c>Srgb</c> formats decode to linear when sampled.</summary>
public enum TextureFormat
{
    R8,
    RG8,
    RGBA8,
    RGBA8Srgb,
    R16F,
    RG16F,
    RGBA16F,
    R32F,
    RG32F,
    RGBA32F,
    RG11B10F,
    Depth16,
    Depth24,
    Depth32F,
    Depth24Stencil8,
    Bc1Rgba,
    Bc1RgbaSrgb,
    Bc3Rgba,
    Bc3RgbaSrgb,
    Bc4R,
    Bc5RG,
    Bc6HRgbUFloat,
    Bc7Rgba,
    Bc7RgbaSrgb,
    Etc2Rgb8,
    Etc2Rgb8Srgb,
    Etc2Rgba8,
    Etc2Rgba8Srgb,
    Astc4x4,
    Astc4x4Srgb,
}

/// <summary>Helpers over <see cref="TextureFormat"/>.</summary>
public static class TextureFormatExtensions
{
    /// <summary>True for depth and depth-stencil formats.</summary>
    public static bool IsDepth(this TextureFormat f) => f is >= TextureFormat.Depth16 and <= TextureFormat.Depth24Stencil8;

    /// <summary>True for block-compressed formats.</summary>
    public static bool IsCompressed(this TextureFormat f) => f >= TextureFormat.Bc1Rgba;

    /// <summary>True for formats that decode sRGB when sampled.</summary>
    public static bool IsSrgb(this TextureFormat f) => f is TextureFormat.RGBA8Srgb or TextureFormat.Bc1RgbaSrgb or TextureFormat.Bc3RgbaSrgb
        or TextureFormat.Bc7RgbaSrgb or TextureFormat.Etc2Rgb8Srgb or TextureFormat.Etc2Rgba8Srgb or TextureFormat.Astc4x4Srgb;

    /// <summary>Bytes per pixel of an uncompressed color format; 0 for depth and compressed formats.</summary>
    public static int BytesPerPixel(this TextureFormat f) => f switch
    {
        TextureFormat.R8 => 1,
        TextureFormat.RG8 => 2,
        TextureFormat.RGBA8 or TextureFormat.RGBA8Srgb => 4,
        TextureFormat.R16F => 2,
        TextureFormat.RG16F => 4,
        TextureFormat.RGBA16F => 8,
        TextureFormat.R32F => 4,
        TextureFormat.RG32F => 8,
        TextureFormat.RGBA32F => 16,
        TextureFormat.RG11B10F => 4,
        _ => 0,
    };
}

/// <summary>Describes a texture to create.</summary>
public struct TextureDescriptor
{
    public TextureDimension Dimension;
    public TextureFormat Format;
    public int Width;
    public int Height;

    /// <summary>Layers of an array texture or depth of a 3D texture; ignored otherwise.</summary>
    public int DepthOrLayers;

    /// <summary>Number of mip levels to allocate (at least 1).</summary>
    public int MipLevels;

    /// <summary>Samples per pixel. Above 1 the texture is a render target that cannot be sampled; resolve it with
    /// <see cref="GraphicsDevice.Blit"/>.</summary>
    public int Samples;

    /// <summary>A short name shown by graphics debuggers.</summary>
    public string? Label;

    /// <summary>A 2D texture descriptor.</summary>
    public static TextureDescriptor Texture2D(TextureFormat format, int width, int height, int mipLevels = 1, int samples = 1) => new()
    {
        Dimension = TextureDimension.Texture2D,
        Format = format,
        Width = width,
        Height = height,
        DepthOrLayers = 1,
        MipLevels = Math.Max(1, mipLevels),
        Samples = Math.Max(1, samples),
    };

    /// <summary>A cube texture descriptor.</summary>
    public static TextureDescriptor Cube(TextureFormat format, int size, int mipLevels = 1) => new()
    {
        Dimension = TextureDimension.Cube,
        Format = format,
        Width = size,
        Height = size,
        DepthOrLayers = 6,
        MipLevels = Math.Max(1, mipLevels),
        Samples = 1,
    };

    /// <summary>The number of levels of a full mip chain.</summary>
    public static int FullMipCount(int width, int height) => 1 + (int)MathF.Floor(MathF.Log2(Math.Max(1, Math.Max(width, height))));
}

/// <summary>Texture coordinate wrapping.</summary>
public enum TextureWrap
{
    Repeat,
    ClampToEdge,
    MirroredRepeat,
}

/// <summary>Texel filtering.</summary>
public enum TextureFilter
{
    Nearest,
    Linear,
}

/// <summary>How mip levels are chosen.</summary>
public enum MipFilter
{
    /// <summary>Sample the base level only.</summary>
    None,
    Nearest,
    Linear,
}

/// <summary>Comparison functions for depth tests and shadow samplers.</summary>
public enum CompareFunction
{
    Never,
    Less,
    Equal,
    LessEqual,
    Greater,
    NotEqual,
    GreaterEqual,
    Always,
}

/// <summary>Describes a sampler.</summary>
public struct SamplerDescriptor : IEquatable<SamplerDescriptor>
{
    public TextureFilter MagFilter;
    public TextureFilter MinFilter;
    public MipFilter MipFilter;
    public TextureWrap WrapU;
    public TextureWrap WrapV;
    public TextureWrap WrapW;

    /// <summary>Maximum anisotropy; 1 or less turns it off.</summary>
    public float MaxAnisotropy;

    /// <summary>When true the sampler compares against depth (shadow sampler) with <see cref="Compare"/>.</summary>
    public bool CompareEnabled;
    public CompareFunction Compare;

    /// <summary>Linear filtering, trilinear mips, repeat.</summary>
    public static SamplerDescriptor LinearRepeat => new()
    {
        MagFilter = TextureFilter.Linear,
        MinFilter = TextureFilter.Linear,
        MipFilter = MipFilter.Linear,
        WrapU = TextureWrap.Repeat,
        WrapV = TextureWrap.Repeat,
        WrapW = TextureWrap.Repeat,
        MaxAnisotropy = 1f,
    };

    /// <summary>Linear filtering, no mips, clamped.</summary>
    public static SamplerDescriptor LinearClamp => new()
    {
        MagFilter = TextureFilter.Linear,
        MinFilter = TextureFilter.Linear,
        MipFilter = MipFilter.None,
        WrapU = TextureWrap.ClampToEdge,
        WrapV = TextureWrap.ClampToEdge,
        WrapW = TextureWrap.ClampToEdge,
        MaxAnisotropy = 1f,
    };

    /// <summary>Nearest filtering, no mips, clamped.</summary>
    public static SamplerDescriptor NearestClamp => new()
    {
        MagFilter = TextureFilter.Nearest,
        MinFilter = TextureFilter.Nearest,
        MipFilter = MipFilter.None,
        WrapU = TextureWrap.ClampToEdge,
        WrapV = TextureWrap.ClampToEdge,
        WrapW = TextureWrap.ClampToEdge,
        MaxAnisotropy = 1f,
    };

    /// <inheritdoc/>
    public readonly bool Equals(SamplerDescriptor o) => MagFilter == o.MagFilter && MinFilter == o.MinFilter && MipFilter == o.MipFilter
        && WrapU == o.WrapU && WrapV == o.WrapV && WrapW == o.WrapW && MaxAnisotropy == o.MaxAnisotropy
        && CompareEnabled == o.CompareEnabled && Compare == o.Compare;

    /// <inheritdoc/>
    public override readonly bool Equals(object? obj) => obj is SamplerDescriptor o && Equals(o);

    /// <inheritdoc/>
    public override readonly int GetHashCode() => HashCode.Combine(
        HashCode.Combine(MagFilter, MinFilter, MipFilter, WrapU, WrapV, WrapW), MaxAnisotropy, CompareEnabled, Compare);
}

/// <summary>The component type and count of one vertex attribute.</summary>
public enum VertexFormat
{
    Float1,
    Float2,
    Float3,
    Float4,
    UByte4,
    UByte4Norm,
    UShort2Norm,
    UShort4,
    UShort4Norm,
    Short2Norm,
    Short4Norm,
    SByte4Norm,
    UShort2,
    UByte2Norm,
    SByte2Norm,
    Short3Norm,
    SByte3Norm,
    UByte3Norm,
    UShort3Norm,
    UInt4,
}

/// <summary>One attribute of a vertex input.</summary>
public struct VertexAttributeBinding
{
    /// <summary>The shader location.</summary>
    public int Location;

    /// <summary>Index into <see cref="VertexInputDescriptor.Buffers"/>.</summary>
    public int BufferIndex;
    public VertexFormat Format;

    /// <summary>Byte offset of the attribute in a vertex.</summary>
    public int Offset;

    /// <summary>Byte distance between vertices.</summary>
    public int Stride;

    /// <summary>True when the attribute advances once per instance.</summary>
    public bool PerInstance;
}

/// <summary>Index element size.</summary>
public enum IndexFormat
{
    UInt16,
    UInt32,
}

/// <summary>Buffers and attribute layout of a draw.</summary>
public struct VertexInputDescriptor
{
    public VertexAttributeBinding[] Attributes;
    public GpuBuffer[] Buffers;
    public GpuBuffer? IndexBuffer;
    public IndexFormat IndexFormat;
}

/// <summary>Primitive assembly.</summary>
public enum PrimitiveTopology
{
    Triangles,
    TriangleStrip,
    Lines,
    LineStrip,
    Points,
}

/// <summary>Which faces are discarded.</summary>
public enum CullMode
{
    None,
    Back,
    Front,
}

/// <summary>Color blending presets (premultiplied alpha is not assumed).</summary>
public enum BlendMode
{
    /// <summary>No blending; the fragment replaces the target.</summary>
    Opaque,

    /// <summary>src.rgb * src.a + dst.rgb * (1 - src.a).</summary>
    Alpha,

    /// <summary>src.rgb + dst.rgb * (1 - src.a).</summary>
    PremultipliedAlpha,

    /// <summary>src.rgb * src.a + dst.rgb.</summary>
    Additive,

    /// <summary>src.rgb * dst.rgb.</summary>
    Multiply,

    /// <summary>dst.rgb * (1 - src.rgb).</summary>
    Subtractive,
}

/// <summary>Fixed-function state of a draw. A value type so state changes are compared, not re-applied.</summary>
public struct RenderState : IEquatable<RenderState>
{
    public CullMode Cull;

    /// <summary>True when clockwise triangles are front faces (set for mirrored transforms).</summary>
    public bool FrontFaceClockwise;
    public bool DepthTest;
    public bool DepthWrite;
    public CompareFunction DepthCompare;
    public BlendMode Blend;
    public bool ColorWrite;
    public bool AlphaToCoverage;

    /// <summary>Slope-scaled depth bias; 0 turns polygon offset off together with <see cref="DepthBiasConstant"/>.</summary>
    public float DepthBiasSlope;
    public float DepthBiasConstant;

    /// <summary>Opaque defaults: back-face culling, depth test and write with <see cref="CompareFunction.LessEqual"/>.</summary>
    public static RenderState Default => new()
    {
        Cull = CullMode.Back,
        DepthTest = true,
        DepthWrite = true,
        DepthCompare = CompareFunction.LessEqual,
        Blend = BlendMode.Opaque,
        ColorWrite = true,
    };

    /// <inheritdoc/>
    public readonly bool Equals(RenderState o) => Cull == o.Cull && FrontFaceClockwise == o.FrontFaceClockwise && DepthTest == o.DepthTest
        && DepthWrite == o.DepthWrite && DepthCompare == o.DepthCompare && Blend == o.Blend && ColorWrite == o.ColorWrite
        && AlphaToCoverage == o.AlphaToCoverage && DepthBiasSlope == o.DepthBiasSlope && DepthBiasConstant == o.DepthBiasConstant;

    /// <inheritdoc/>
    public override readonly bool Equals(object? obj) => obj is RenderState o && Equals(o);

    /// <inheritdoc/>
    public override readonly int GetHashCode() => HashCode.Combine(
        HashCode.Combine(Cull, FrontFaceClockwise, DepthTest, DepthWrite, DepthCompare, Blend, ColorWrite, AlphaToCoverage),
        DepthBiasSlope, DepthBiasConstant);
}

/// <summary>A uniform block name and the buffer slot it reads from.</summary>
public readonly record struct UniformBlockBinding(string Name, int Slot);

/// <summary>A sampler uniform name and the texture unit it reads from.</summary>
public readonly record struct SamplerBinding(string Name, int Unit);

/// <summary>Shader sources and the binding tables a device needs to link them.</summary>
public sealed class ProgramDescriptor
{
    public required string VertexSource { get; init; }
    public required string FragmentSource { get; init; }
    public UniformBlockBinding[] UniformBlocks { get; init; } = [];
    public SamplerBinding[] Samplers { get; init; } = [];

    /// <summary>A short name shown in compile errors and graphics debuggers.</summary>
    public string? Label { get; init; }
}

/// <summary>The source of a compute program.</summary>
public sealed class ComputeProgramDescriptor
{
    /// <summary>The compute stage, in the device's <see cref="DeviceCapabilities.ShaderLanguage"/>. Its bindings follow
    /// the slot mapping of that language (<see cref="ShaderLanguage.Glsl450"/>).</summary>
    public required string Source { get; init; }

    /// <summary>A short name shown in compile errors and graphics debuggers.</summary>
    public string? Label { get; init; }
}

/// <summary>One color attachment of a framebuffer.</summary>
public struct FramebufferAttachment
{
    public GpuTexture Texture;
    public int MipLevel;

    /// <summary>Cube face (0..5: +X, -X, +Y, -Y, +Z, -Z) or array layer.</summary>
    public int Layer;

    /// <summary>Attaches mip 0, layer 0 of a texture.</summary>
    public static implicit operator FramebufferAttachment(GpuTexture texture) => new() { Texture = texture };
}

/// <summary>Describes a framebuffer.</summary>
public struct FramebufferDescriptor
{
    public FramebufferAttachment[]? Colors;
    public FramebufferAttachment? Depth;
    public string? Label;
}

/// <summary>What a render pass does to its target before drawing.</summary>
public struct RenderPassDescriptor
{
    /// <summary>The target; null means the device's current surface (<see cref="GraphicsDevice.SetSurface"/>).</summary>
    public GpuFramebuffer? Target;
    public int ViewportX;
    public int ViewportY;
    public int ViewportWidth;
    public int ViewportHeight;
    public bool ClearColor;
    public System.Numerics.Vector4 ClearColorValue;
    public bool ClearDepth;
    public float ClearDepthValue;
    public bool ClearStencil;

    /// <summary>When true, clears and draws are limited to the viewport rectangle.</summary>
    public bool ScissorToViewport;
}

/// <summary>What a device supports. Filled once when the device is created.</summary>
public sealed class DeviceCapabilities
{
    public ShaderLanguage ShaderLanguage { get; init; }
    public ClipDepthRange ClipDepth { get; init; }
    public string Vendor { get; init; } = "";
    public string Renderer { get; init; } = "";
    public string Version { get; init; } = "";
    public int MaxTextureSize { get; init; }
    public int MaxCubeTextureSize { get; init; }
    public int MaxSamples { get; init; }
    public float MaxAnisotropy { get; init; }
    public int MaxFragmentTextures { get; init; }
    public int MaxUniformBlockSize { get; init; }
    public int UniformBufferOffsetAlignment { get; init; }
    public int MaxVertexAttributes { get; init; }
    public int MaxColorAttachments { get; init; }

    /// <summary>RGBA16F / RGBA32F / RG11B10F can be rendered to.</summary>
    public bool FloatRenderTargets { get; init; }

    /// <summary>32-bit float textures can be sampled with linear filtering.</summary>
    public bool FloatLinearFiltering { get; init; }
    public bool TextureCompressionBc { get; init; }
    public bool TextureCompressionEtc2 { get; init; }
    public bool TextureCompressionAstc { get; init; }

    /// <summary>Shaders can be compiled without blocking (<see cref="GpuProgram.IsReady"/> turns true later).</summary>
    public bool ParallelShaderCompile { get; init; }

    /// <summary>Compute programs, storage buffers and buffer readback are available
    /// (<see cref="GraphicsDevice.CreateComputeProgram"/>, <see cref="GraphicsDevice.DispatchCompute"/>,
    /// <see cref="GraphicsDevice.ReadBuffer"/>).</summary>
    public bool ComputeShaders { get; init; }

    /// <summary>The largest <c>local_size_x</c> of a compute program; 0 without compute.</summary>
    public int MaxComputeWorkGroupSize { get; init; }

    /// <summary>The largest work group count per dimension of one dispatch; 0 without compute.</summary>
    public int MaxComputeWorkGroups { get; init; }

    /// <summary>The number of storage buffer slots a compute program can use; 0 without compute.</summary>
    public int MaxStorageBuffers { get; init; }
}
