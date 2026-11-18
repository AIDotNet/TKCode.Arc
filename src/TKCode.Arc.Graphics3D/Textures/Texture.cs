using System.Numerics;
using System.Runtime.InteropServices;
using TKCode.Arc.Graphics3D.Rhi;

namespace TKCode.Arc.Graphics3D;

/// <summary>How the colors stored in a texture are encoded.</summary>
public enum TextureColorSpace
{
    /// <summary>Data that is used as is (normals, roughness, HDR images).</summary>
    Linear,

    /// <summary>sRGB-encoded colors (base color and emissive images); decoded to linear when sampled.</summary>
    Srgb,
}

/// <summary>How a texture that surrounds the scene maps to directions.</summary>
public enum TextureMapping
{
    /// <summary>An ordinary texture addressed by UV coordinates.</summary>
    Uv,

    /// <summary>A 2:1 latitude/longitude panorama.</summary>
    Equirectangular,

    /// <summary>Six faces (a <see cref="CubeTexture"/>).</summary>
    Cube,
}

/// <summary>Pixels in memory: 8-bit data as bytes, float formats as 32-bit floats per channel. Rows run top to bottom.</summary>
public sealed class TextureImage
{
    private readonly Array _data;

    /// <summary>Creates an image over byte data (<see cref="TextureFormat.RGBA8"/>, <see cref="TextureFormat.R8"/>,
    /// <see cref="TextureFormat.RG8"/> or a compressed format).</summary>
    public TextureImage(int width, int height, TextureFormat format, byte[] data)
    {
        Width = width;
        Height = height;
        Format = format;
        _data = data ?? throw new ArgumentNullException(nameof(data));
    }

    /// <summary>Creates an image over float data (one float per channel).</summary>
    public TextureImage(int width, int height, TextureFormat format, float[] data)
    {
        Width = width;
        Height = height;
        Format = format;
        _data = data ?? throw new ArgumentNullException(nameof(data));
    }

    /// <summary>Creates an RGBA8 image.</summary>
    public TextureImage(int width, int height, byte[] rgba)
        : this(width, height, TextureFormat.RGBA8, rgba)
    {
    }

    public int Width { get; }
    public int Height { get; }
    public TextureFormat Format { get; }

    /// <summary>The backing array: <c>byte[]</c> or <c>float[]</c>.</summary>
    public Array Data => _data;

    /// <summary>Smaller versions of the image, level 1 first. When set, the renderer uploads them instead of generating
    /// mipmaps (required for compressed formats).</summary>
    public TextureImage[]? Mipmaps { get; set; }

    /// <summary>The raw bytes.</summary>
    public ReadOnlySpan<byte> AsBytes() => _data switch
    {
        byte[] b => b,
        float[] f => MemoryMarshal.AsBytes<float>(f),
        _ => default,
    };

    /// <summary>A 1x1 RGBA8 image of one color (components 0..255).</summary>
    public static TextureImage Solid(byte r, byte g, byte b, byte a = 255) => new(1, 1, [r, g, b, a]);
}

/// <summary>An image with sampling settings, used by materials, the scene background and environment lighting.</summary>
public class Texture : IDisposable
{
    private static int s_nextId;
    private TextureImage? _image;

    /// <summary>Creates an empty texture.</summary>
    public Texture()
    {
    }

    /// <summary>Creates a texture from an image.</summary>
    public Texture(TextureImage image, TextureColorSpace colorSpace = TextureColorSpace.Linear)
    {
        _image = image;
        ColorSpace = colorSpace;
    }

    /// <summary>A process-wide unique number.</summary>
    public int Id { get; } = Interlocked.Increment(ref s_nextId);

    /// <summary>A name for debugging.</summary>
    public string Name { get; set; } = "";

    /// <summary>The pixels. Setting it schedules an upload.</summary>
    public TextureImage? Image
    {
        get => _image;
        set
        {
            _image = value;
            Version++;
        }
    }

    public TextureWrap WrapU { get; set; } = TextureWrap.Repeat;
    public TextureWrap WrapV { get; set; } = TextureWrap.Repeat;
    public TextureFilter MagFilter { get; set; } = TextureFilter.Linear;
    public TextureFilter MinFilter { get; set; } = TextureFilter.Linear;
    public MipFilter MipFilter { get; set; } = MipFilter.Linear;

    /// <summary>Anisotropic filtering samples; 1 turns it off.</summary>
    public float Anisotropy { get; set; } = 1f;

    /// <summary>When true (default) the renderer builds the mip chain after each upload.</summary>
    public bool GenerateMipmaps { get; set; } = true;

    /// <summary>How the stored colors are encoded.</summary>
    public TextureColorSpace ColorSpace { get; set; }

    /// <summary>When true (default) V = 0 is the bottom image row, the convention of the built-in geometries. glTF
    /// uses V = 0 at the top; its loader sets this to false.</summary>
    public bool FlipY { get; set; } = true;

    /// <summary>How the texture maps to directions when it is a background or environment.</summary>
    public TextureMapping Mapping { get; set; } = TextureMapping.Uv;

    /// <summary>UV offset applied by built-in material maps.</summary>
    public Vector2 Offset { get; set; }

    /// <summary>UV scale applied by built-in material maps.</summary>
    public Vector2 Repeat { get; set; } = Vector2.One;

    /// <summary>UV rotation in radians about <see cref="Center"/>, applied by built-in material maps.</summary>
    public float Rotation { get; set; }

    /// <summary>The pivot of <see cref="Rotation"/> and <see cref="Repeat"/>.</summary>
    public Vector2 Center { get; set; }

    /// <summary>Which UV set of the geometry built-in material maps sample this texture with: 0 (<c>uv</c>, default)
    /// or 1 (<c>uv1</c>, typically a light map or occlusion layout). Set it before giving the texture to a material;
    /// afterwards call <see cref="Material.Invalidate"/> on the materials that use it.</summary>
    public int Channel { get; set; }

    /// <summary>Changes whenever the pixels need to be uploaded again.</summary>
    public int Version { get; private set; }

    /// <summary>True after <see cref="Dispose"/>.</summary>
    public bool IsDisposed { get; private set; }

    /// <summary>The GPU shape.</summary>
    public virtual TextureDimension Dimension => TextureDimension.Texture2D;

    /// <summary>Width of the image in pixels, or 0.</summary>
    public virtual int Width => _image?.Width ?? 0;

    /// <summary>Height of the image in pixels, or 0.</summary>
    public virtual int Height => _image?.Height ?? 0;

    internal ResourceBinding? Binding;

    /// <summary>The render target that owns this texture, when it is a render target attachment.</summary>
    internal RenderTarget? Owner;

    /// <summary>True when this is the depth attachment of <see cref="Owner"/>.</summary>
    internal bool IsDepthAttachment;

    /// <summary>Tells the renderer that the pixel data changed.</summary>
    public void NeedsUpdate() => Version++;

    /// <summary>The two rows of the UV transform: <c>uv' = (dot(row0, (u, v, 1)), dot(row1, (u, v, 1)))</c>.</summary>
    public (Vector3 Row0, Vector3 Row1) GetUvTransform()
    {
        var (s, c) = MathF.SinCos(Rotation);
        float sx = Repeat.X, sy = Repeat.Y, cx = Center.X, cy = Center.Y;
        return (
            new Vector3(sx * c, sx * s, (-sx * ((c * cx) + (s * cy))) + cx + Offset.X),
            new Vector3(-sy * s, sy * c, (-sy * ((-s * cx) + (c * cy))) + cy + Offset.Y));
    }

    /// <summary>Frees the GPU texture in every renderer that used it.</summary>
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

/// <summary>Six square images that surround the scene: +X, -X, +Y, -Y, +Z, -Z.</summary>
public sealed class CubeTexture : Texture
{
    /// <summary>Creates a cube texture from six faces in the order +X, -X, +Y, -Y, +Z, -Z.</summary>
    public CubeTexture(TextureImage[] faces, TextureColorSpace colorSpace = TextureColorSpace.Linear)
    {
        if (faces is null || faces.Length != 6)
        {
            throw new ArgumentException("A cube texture needs six faces.", nameof(faces));
        }

        Faces = faces;
        ColorSpace = colorSpace;
        Mapping = TextureMapping.Cube;
        FlipY = false;
        WrapU = TextureWrap.ClampToEdge;
        WrapV = TextureWrap.ClampToEdge;
    }

    /// <summary>The faces: +X, -X, +Y, -Y, +Z, -Z.</summary>
    public TextureImage[] Faces { get; }

    /// <inheritdoc/>
    public override TextureDimension Dimension => TextureDimension.Cube;

    /// <inheritdoc/>
    public override int Width => Faces[0].Width;

    /// <inheritdoc/>
    public override int Height => Faces[0].Height;
}

/// <summary>An off-screen target to render into. Its <see cref="Texture"/> can then be used like any other texture.</summary>
public sealed class RenderTarget : IDisposable
{
    /// <summary>Creates a render target.</summary>
    /// <param name="width">Width in pixels.</param>
    /// <param name="height">Height in pixels.</param>
    /// <param name="format">Color format; <see cref="TextureFormat.RGBA16F"/> keeps HDR values.</param>
    /// <param name="samples">Samples per pixel for anti-aliasing (resolved into <see cref="Texture"/>).</param>
    /// <param name="depthTexture">True to make depth readable through <see cref="DepthTexture"/>.</param>
    public RenderTarget(int width, int height, TextureFormat format = TextureFormat.RGBA8, int samples = 1, bool depthTexture = false)
    {
        Width = Math.Max(1, width);
        Height = Math.Max(1, height);
        Format = format;
        Samples = Math.Max(1, samples);
        Texture = new Texture
        {
            Owner = this,
            FlipY = false,
            GenerateMipmaps = false,
            MipFilter = MipFilter.None,
            WrapU = TextureWrap.ClampToEdge,
            WrapV = TextureWrap.ClampToEdge,
        };
        if (depthTexture)
        {
            DepthTexture = new Texture
            {
                Owner = this,
                IsDepthAttachment = true,
                FlipY = false,
                GenerateMipmaps = false,
                MipFilter = MipFilter.None,
                MagFilter = TextureFilter.Nearest,
                MinFilter = TextureFilter.Nearest,
                WrapU = TextureWrap.ClampToEdge,
                WrapV = TextureWrap.ClampToEdge,
            };
        }
    }

    public int Width { get; private set; }
    public int Height { get; private set; }
    public TextureFormat Format { get; }
    public int Samples { get; }

    /// <summary>False to render without a depth buffer.</summary>
    public bool DepthBuffer { get; init; } = true;

    /// <summary>The color result.</summary>
    public Texture Texture { get; }

    /// <summary>The depth result, when requested.</summary>
    public Texture? DepthTexture { get; }

    /// <summary>Changes when the size changes.</summary>
    public int Version { get; private set; }

    internal ResourceBinding? Binding;

    /// <summary>True for the renderer's own multisampled stand-in for the surface: it has no resolve texture.</summary>
    internal bool SurfaceProxy;

    /// <summary>How the colors written to the target are encoded. <see cref="TextureColorSpace.Linear"/> (default)
    /// stores scene-referred linear values, for use as a texture or for post-processing.
    /// <see cref="TextureColorSpace.Srgb"/> makes the target a picture: tone mapping and sRGB encoding are applied, as
    /// they are when rendering to the screen.</summary>
    public TextureColorSpace ColorSpace { get; init; }

    /// <summary>Changes the size; the contents are lost.</summary>
    public void Resize(int width, int height)
    {
        width = Math.Max(1, width);
        height = Math.Max(1, height);
        if (width != Width || height != Height)
        {
            Width = width;
            Height = height;
            Version++;
        }
    }

    /// <summary>Frees the GPU objects in every renderer that used the target.</summary>
    public void Dispose() => ResourceBinding.ReleaseAll(ref Binding);
}
