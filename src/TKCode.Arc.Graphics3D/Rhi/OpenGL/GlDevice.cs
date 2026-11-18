using System.Runtime.InteropServices;
using System.Text;
using static TKCode.Arc.Graphics3D.Rhi.OpenGL.GlConst;

namespace TKCode.Arc.Graphics3D.Rhi.OpenGL;

/// <summary>The OpenGL backend: OpenGL ES 3.0 (ANGLE on Windows; WebGL 2 in a browser, through Emscripten's OpenGL ES
/// layer) and desktop OpenGL 3.3+ / 4.1 core (macOS, Linux). Every call must happen with the context current; in Avalonia
/// that is inside <c>OnOpenGlInit</c> / <c>OnOpenGlRender</c> / <c>OnOpenGlDeinit</c>. These OpenGL versions have no
/// compute shaders: <see cref="DeviceCapabilities.ComputeShaders"/> is false and the compute calls throw.</summary>
public sealed unsafe partial class GlDevice : GraphicsDevice
{
    private const int MaxUnits = 32;
    private const int MaxSlots = 16;

    // Textures are created and written through this unit, which no shader samples (OpenGL ES 3.0 and OpenGL 3.3
    // guarantee at least 32 combined units), so resources created in the middle of a pass leave its bindings alone.
    private const int EditUnit = MaxUnits - 1;

    private readonly GlApi _gl;
    private readonly DeviceCapabilities _caps;
    private readonly bool _isEs;
    private readonly uint _scratchVao;

    private uint _surfaceFbo;
    private int _surfaceWidth;
    private int _surfaceHeight;

    // Cached state. _stateValid false means "unknown": everything is applied on the next use.
    private bool _stateValid;
    private RenderState _state;
    private uint _boundFbo = uint.MaxValue;
    private GlProgram? _program;
    private uint _boundVao = uint.MaxValue;
    private GlVertexInput? _vertexInput;
    private int _activeUnit = -1;
    private readonly uint[] _unitTexture = new uint[MaxUnits];
    private readonly int[] _unitTarget = new int[MaxUnits];
    private readonly uint[] _unitSampler = new uint[MaxUnits];
    private readonly uint[] _slotBuffer = new uint[MaxSlots];
    private readonly int[] _slotOffset = new int[MaxSlots];
    private readonly int[] _slotSize = new int[MaxSlots];
    private bool _scissorEnabled;
    private int _draw0, _draw1, _draw2, _draw3;
    private int _passHeight;

    /// <summary>Creates the device over the current context.</summary>
    /// <param name="getProcAddress">Returns the address of an OpenGL function by name, or 0.</param>
    public GlDevice(Func<string, nint> getProcAddress)
    {
        ArgumentNullException.ThrowIfNull(getProcAddress);
        _gl = new GlApi(getProcAddress);
        var version = _gl.GetStringValue(VERSION);
        if (version.Length == 0 || _gl.GetInteger(MAX_TEXTURE_SIZE) <= 0)
        {
            // The functions resolve but answer nothing: no context is current on this thread, or the functions belong to
            // another copy of the library than the one the context was made current in.
            throw new NotSupportedException("The OpenGL context does not answer: it is not current on this thread.");
        }

        _isEs = version.Contains("OpenGL ES", StringComparison.Ordinal);

        // WebGL 2 is OpenGL ES 3.0 with a few things taken out; Emscripten reports it as "OpenGL ES 3.0 (WebGL 2.0 ...)".
        var isWebGl = version.Contains("WebGL", StringComparison.Ordinal);
        var ext = _gl.GetExtensions();
        float maxAniso = 1f;
        if (ext.Contains("GL_EXT_texture_filter_anisotropic") || ext.Contains("GL_ARB_texture_filter_anisotropic"))
        {
            _gl.GetFloatv(MAX_TEXTURE_MAX_ANISOTROPY, &maxAniso);
        }

        _caps = new DeviceCapabilities
        {
            ShaderLanguage = _isEs ? ShaderLanguage.GlslEs300 : ShaderLanguage.Glsl330,
            ClipDepth = ClipDepthRange.NegativeOneToOne,
            Vendor = _gl.GetStringValue(VENDOR),
            Renderer = _gl.GetStringValue(RENDERER),
            Version = version,
            MaxTextureSize = _gl.GetInteger(MAX_TEXTURE_SIZE),
            MaxCubeTextureSize = _gl.GetInteger(MAX_CUBE_MAP_TEXTURE_SIZE),
            MaxSamples = _gl.GetInteger(MAX_SAMPLES),
            MaxAnisotropy = maxAniso,
            MaxFragmentTextures = Math.Min(EditUnit, _gl.GetInteger(MAX_TEXTURE_IMAGE_UNITS)),
            MaxUniformBlockSize = _gl.GetInteger(MAX_UNIFORM_BLOCK_SIZE),
            UniformBufferOffsetAlignment = _gl.GetInteger(UNIFORM_BUFFER_OFFSET_ALIGNMENT),
            MaxVertexAttributes = _gl.GetInteger(MAX_VERTEX_ATTRIBS),
            MaxColorAttachments = _gl.GetInteger(MAX_COLOR_ATTACHMENTS),
            FloatRenderTargets = !_isEs || ext.Contains("GL_EXT_color_buffer_float"),
            FloatLinearFiltering = !_isEs || ext.Contains("GL_OES_texture_float_linear"),
            TextureCompressionBc = ext.Contains("GL_EXT_texture_compression_s3tc") || ext.Contains("GL_WEBGL_compressed_texture_s3tc"),

            // OpenGL ES 3.0 requires ETC2. WebGL 2 does not (desktop GPUs have no ETC2) and names an extension where it has it.
            TextureCompressionEtc2 = (_isEs && !isWebGl) || ext.Contains("GL_ARB_ES3_compatibility") || ext.Contains("GL_WEBGL_compressed_texture_etc"),
            TextureCompressionAstc = ext.Contains("GL_KHR_texture_compression_astc_ldr") || ext.Contains("GL_WEBGL_compressed_texture_astc"),
        };
        HasBptc = ext.Contains("GL_EXT_texture_compression_bptc") || ext.Contains("GL_ARB_texture_compression_bptc");

        uint vao = 0;
        _gl.GenVertexArrays(1, &vao);
        _scratchVao = vao;
        _gl.PixelStorei(UNPACK_ALIGNMENT, 1);
        _gl.PixelStorei(PACK_ALIGNMENT, 1);
        if (!_isEs)
        {
            _gl.Enable(TEXTURE_CUBE_MAP_SEAMLESS);
            _gl.Enable(PROGRAM_POINT_SIZE);
        }

        // Drain errors left by the host so they are not attributed to the engine.
        while (_gl.GetError() != NO_ERROR)
        {
        }

        InvalidateState();
    }

    /// <summary>True when BC6H / BC7 textures can be used.</summary>
    public bool HasBptc { get; }

    /// <inheritdoc/>
    public override DeviceCapabilities Capabilities => _caps;

    /// <summary>Returns the pending OpenGL error codes (empty when none). For tests and diagnostics; it stalls the
    /// pipeline, so do not call it every frame.</summary>
    public IReadOnlyList<int> DrainErrors()
    {
        List<int>? errors = null;
        for (var i = 0; i < 16; i++)
        {
            var e = _gl.GetError();
            if (e == NO_ERROR)
            {
                break;
            }

            (errors ??= []).Add(e);
        }

        return errors ?? (IReadOnlyList<int>)[];
    }

    /// <summary>Blocks until the GPU finished all submitted work (for measurements).</summary>
    public void Finish() => _gl.Finish();

    // ---- Buffers ----

    /// <inheritdoc/>
    public override GpuBuffer CreateBuffer(BufferUsage usage, int sizeInBytes, bool dynamic, string? label = null)
    {
        if (usage == BufferUsage.Storage)
        {
            // OpenGL 3.3 and OpenGL ES 3.0 have neither compute shaders nor shader storage buffers.
            throw new NotSupportedException("Storage buffers need a device with compute shaders (DeviceCapabilities.ComputeShaders).");
        }

        uint id = 0;
        _gl.GenBuffers(1, &id);
        var buffer = new GlBuffer(this, id, usage, sizeInBytes, dynamic);
        BindForUpload(buffer);
        _gl.BufferData(buffer.Target, sizeInBytes, null, dynamic ? DYNAMIC_DRAW : STATIC_DRAW);
        return buffer;
    }

    private void BindForUpload(GlBuffer buffer)
    {
        if (buffer.Usage == BufferUsage.Index)
        {
            // The element array binding belongs to the vertex array object; never disturb a real one.
            BindVao(_scratchVao);
            _vertexInput = null;
        }

        _gl.BindBuffer(buffer.Target, buffer.Id);
    }

    /// <inheritdoc/>
    public override void WriteBuffer(GpuBuffer buffer, int offset, ReadOnlySpan<byte> data)
    {
        var b = (GlBuffer)buffer;
        if (data.IsEmpty)
        {
            return;
        }

        if (offset < 0 || offset + data.Length > b.SizeInBytes)
        {
            throw new ArgumentOutOfRangeException(nameof(offset), "The write does not fit in the buffer.");
        }

        BindForUpload(b);
        fixed (byte* p = data)
        {
            if (offset == 0 && data.Length == b.SizeInBytes && b.Dynamic)
            {
                // Orphan the old storage so the driver does not wait for draws that still read it.
                _gl.BufferData(b.Target, data.Length, p, DYNAMIC_DRAW);
            }
            else
            {
                _gl.BufferSubData(b.Target, offset, data.Length, p);
            }
        }

        Statistics.BufferUploads++;
        Statistics.BufferUploadBytes += data.Length;
    }

    // ---- Textures ----

    /// <inheritdoc/>
    public override GpuTexture CreateTexture(in TextureDescriptor descriptor)
    {
        var d = descriptor;
        d.MipLevels = Math.Max(1, d.MipLevels);
        d.Samples = Math.Max(1, d.Samples);
        var (internalFormat, format, type) = MapFormat(d.Format);
        if (d.Samples > 1)
        {
            uint rb = 0;
            _gl.GenRenderbuffers(1, &rb);
            _gl.BindRenderbuffer(RENDERBUFFER, rb);
            _gl.RenderbufferStorageMultisample(RENDERBUFFER, Math.Min(d.Samples, _caps.MaxSamples), internalFormat, d.Width, d.Height);
            return new GlTexture(this, rb, RENDERBUFFER, d, isRenderbuffer: true);
        }

        var target = d.Dimension switch
        {
            TextureDimension.Cube => TEXTURE_CUBE_MAP,
            TextureDimension.Texture2DArray => TEXTURE_2D_ARRAY,
            TextureDimension.Texture3D => TEXTURE_3D,
            _ => TEXTURE_2D,
        };
        uint id = 0;
        _gl.GenTextures(1, &id);
        var texture = new GlTexture(this, id, target, d, isRenderbuffer: false);
        BindTextureForEdit(texture);
        if (!d.Format.IsCompressed())
        {
            for (var level = 0; level < d.MipLevels; level++)
            {
                var w = Math.Max(1, d.Width >> level);
                var h = Math.Max(1, d.Height >> level);
                switch (d.Dimension)
                {
                    case TextureDimension.Cube:
                        for (var face = 0; face < 6; face++)
                        {
                            _gl.TexImage2D(TEXTURE_CUBE_MAP_POSITIVE_X + face, level, internalFormat, w, h, 0, format, type, null);
                        }

                        break;
                    case TextureDimension.Texture2DArray:
                        _gl.TexImage3D(target, level, internalFormat, w, h, d.DepthOrLayers, 0, format, type, null);
                        break;
                    case TextureDimension.Texture3D:
                        _gl.TexImage3D(target, level, internalFormat, w, h, Math.Max(1, d.DepthOrLayers >> level), 0, format, type, null);
                        break;
                    default:
                        _gl.TexImage2D(target, level, internalFormat, w, h, 0, format, type, null);
                        break;
                }
            }
        }

        _gl.TexParameteri(target, TEXTURE_BASE_LEVEL, 0);
        _gl.TexParameteri(target, TEXTURE_MAX_LEVEL, d.MipLevels - 1);
        // Complete without a sampler object too.
        var filter = d.Format.IsDepth() ? NEAREST : LINEAR;
        _gl.TexParameteri(target, TEXTURE_MIN_FILTER, d.MipLevels > 1 ? (d.Format.IsDepth() ? NEAREST_MIPMAP_NEAREST : LINEAR_MIPMAP_LINEAR) : filter);
        _gl.TexParameteri(target, TEXTURE_MAG_FILTER, filter);
        return texture;
    }

    private void BindTextureForEdit(GlTexture texture)
    {
        SetActiveUnit(EditUnit);
        _gl.BindTexture(texture.Target, texture.Id);
        _unitTexture[EditUnit] = texture.Id;
        _unitTarget[EditUnit] = texture.Target;
    }

    /// <inheritdoc/>
    public override void WriteTexture(GpuTexture texture, int mipLevel, int layer, int x, int y, int width, int height, ReadOnlySpan<byte> data)
    {
        var t = (GlTexture)texture;
        if (t.IsRenderbuffer)
        {
            throw new InvalidOperationException("A multisampled texture cannot be written.");
        }

        var d = t.Descriptor;
        var (internalFormat, format, type) = MapFormat(d.Format);
        BindTextureForEdit(t);
        fixed (byte* p = data)
        {
            if (d.Format.IsCompressed())
            {
                var faceTarget = d.Dimension == TextureDimension.Cube ? TEXTURE_CUBE_MAP_POSITIVE_X + layer : t.Target;
                _gl.CompressedTexImage2D(faceTarget, mipLevel, internalFormat, width, height, 0, data.Length, p);
            }
            else
            {
                var expected = (long)width * height * UploadBytesPerPixel(d.Format);
                if (data.Length < expected)
                {
                    throw new ArgumentException($"The texture write needs {expected} bytes but got {data.Length}.", nameof(data));
                }

                switch (d.Dimension)
                {
                    case TextureDimension.Cube:
                        _gl.TexSubImage2D(TEXTURE_CUBE_MAP_POSITIVE_X + layer, mipLevel, x, y, width, height, format, type, p);
                        break;
                    case TextureDimension.Texture2DArray:
                    case TextureDimension.Texture3D:
                        _gl.TexSubImage3D(t.Target, mipLevel, x, y, layer, width, height, 1, format, type, p);
                        break;
                    default:
                        _gl.TexSubImage2D(t.Target, mipLevel, x, y, width, height, format, type, p);
                        break;
                }
            }
        }

        Statistics.BufferUploads++;
        Statistics.BufferUploadBytes += data.Length;
    }

    /// <summary>Bytes per pixel that <see cref="WriteTexture"/> expects: float formats are always written as 32-bit
    /// floats per channel (the driver converts to half floats), RG11B10F as three floats.</summary>
    public static int UploadBytesPerPixel(TextureFormat format) => format switch
    {
        TextureFormat.R8 => 1,
        TextureFormat.RG8 => 2,
        TextureFormat.RGBA8 or TextureFormat.RGBA8Srgb => 4,
        TextureFormat.R16F or TextureFormat.R32F => 4,
        TextureFormat.RG16F or TextureFormat.RG32F => 8,
        TextureFormat.RGBA16F or TextureFormat.RGBA32F => 16,
        TextureFormat.RG11B10F => 12,
        TextureFormat.Depth16 => 2,
        TextureFormat.Depth24 or TextureFormat.Depth32F or TextureFormat.Depth24Stencil8 => 4,
        _ => 0,
    };

    /// <inheritdoc/>
    public override void GenerateMipmaps(GpuTexture texture)
    {
        var t = (GlTexture)texture;
        if (t.IsRenderbuffer || t.Descriptor.MipLevels <= 1)
        {
            return;
        }

        BindTextureForEdit(t);
        _gl.GenerateMipmap(t.Target);
    }

    private static (int Internal, int Format, int Type) MapFormat(TextureFormat f) => f switch
    {
        TextureFormat.R8 => (R8, RED, UNSIGNED_BYTE),
        TextureFormat.RG8 => (RG8, RG, UNSIGNED_BYTE),
        TextureFormat.RGBA8 => (RGBA8, RGBA, UNSIGNED_BYTE),
        TextureFormat.RGBA8Srgb => (SRGB8_ALPHA8, RGBA, UNSIGNED_BYTE),
        TextureFormat.R16F => (R16F, RED, FLOAT),
        TextureFormat.RG16F => (RG16F, RG, FLOAT),
        TextureFormat.RGBA16F => (RGBA16F, RGBA, FLOAT),
        TextureFormat.R32F => (R32F, RED, FLOAT),
        TextureFormat.RG32F => (RG32F, RG, FLOAT),
        TextureFormat.RGBA32F => (RGBA32F, RGBA, FLOAT),
        TextureFormat.RG11B10F => (R11F_G11F_B10F, RGB, FLOAT),
        TextureFormat.Depth16 => (DEPTH_COMPONENT16, DEPTH_COMPONENT, UNSIGNED_SHORT),
        TextureFormat.Depth24 => (DEPTH_COMPONENT24, DEPTH_COMPONENT, UNSIGNED_INT),
        TextureFormat.Depth32F => (DEPTH_COMPONENT32F, DEPTH_COMPONENT, FLOAT),
        TextureFormat.Depth24Stencil8 => (DEPTH24_STENCIL8, DEPTH_STENCIL, UNSIGNED_INT_24_8),
        TextureFormat.Bc1Rgba => (0x83F1, 0, 0),
        TextureFormat.Bc1RgbaSrgb => (0x8C4D, 0, 0),
        TextureFormat.Bc3Rgba => (0x83F3, 0, 0),
        TextureFormat.Bc3RgbaSrgb => (0x8C4F, 0, 0),
        TextureFormat.Bc4R => (0x8DBB, 0, 0),
        TextureFormat.Bc5RG => (0x8DBD, 0, 0),
        TextureFormat.Bc6HRgbUFloat => (0x8E8F, 0, 0),
        TextureFormat.Bc7Rgba => (0x8E8C, 0, 0),
        TextureFormat.Bc7RgbaSrgb => (0x8E8D, 0, 0),
        TextureFormat.Etc2Rgb8 => (0x9274, 0, 0),
        TextureFormat.Etc2Rgb8Srgb => (0x9275, 0, 0),
        TextureFormat.Etc2Rgba8 => (0x9278, 0, 0),
        TextureFormat.Etc2Rgba8Srgb => (0x9279, 0, 0),
        TextureFormat.Astc4x4 => (0x93B0, 0, 0),
        TextureFormat.Astc4x4Srgb => (0x93D0, 0, 0),
        _ => throw new NotSupportedException($"Texture format {f} is not supported."),
    };

    /// <inheritdoc/>
    public override GpuSampler CreateSampler(in SamplerDescriptor d)
    {
        uint id = 0;
        _gl.GenSamplers(1, &id);
        var min = d.MipFilter switch
        {
            MipFilter.Nearest => d.MinFilter == TextureFilter.Linear ? LINEAR_MIPMAP_NEAREST : NEAREST_MIPMAP_NEAREST,
            MipFilter.Linear => d.MinFilter == TextureFilter.Linear ? LINEAR_MIPMAP_LINEAR : NEAREST_MIPMAP_LINEAR,
            _ => d.MinFilter == TextureFilter.Linear ? LINEAR : NEAREST,
        };
        _gl.SamplerParameteri(id, TEXTURE_MIN_FILTER, min);
        _gl.SamplerParameteri(id, TEXTURE_MAG_FILTER, d.MagFilter == TextureFilter.Linear ? LINEAR : NEAREST);
        _gl.SamplerParameteri(id, TEXTURE_WRAP_S, MapWrap(d.WrapU));
        _gl.SamplerParameteri(id, TEXTURE_WRAP_T, MapWrap(d.WrapV));
        _gl.SamplerParameteri(id, TEXTURE_WRAP_R, MapWrap(d.WrapW));
        if (d.MaxAnisotropy > 1f && _caps.MaxAnisotropy > 1f)
        {
            _gl.SamplerParameterf(id, TEXTURE_MAX_ANISOTROPY, MathF.Min(d.MaxAnisotropy, _caps.MaxAnisotropy));
        }

        if (d.CompareEnabled)
        {
            _gl.SamplerParameteri(id, TEXTURE_COMPARE_MODE, COMPARE_REF_TO_TEXTURE);
            _gl.SamplerParameteri(id, TEXTURE_COMPARE_FUNC, NEVER + (int)d.Compare);
        }

        return new GlSampler(this, id);
    }

    private static int MapWrap(TextureWrap w) => w switch
    {
        TextureWrap.ClampToEdge => CLAMP_TO_EDGE,
        TextureWrap.MirroredRepeat => MIRRORED_REPEAT,
        _ => REPEAT,
    };

    // ---- Programs ----

    /// <inheritdoc/>
    public override GpuProgram CreateProgram(ProgramDescriptor descriptor)
    {
        var vs = Compile(VERTEX_SHADER, descriptor.VertexSource, descriptor.Label);
        var fs = Compile(FRAGMENT_SHADER, descriptor.FragmentSource, descriptor.Label);
        var id = _gl.CreateProgram();
        _gl.AttachShader(id, vs);
        _gl.AttachShader(id, fs);
        _gl.LinkProgram(id);
        _gl.DeleteShader(vs);
        _gl.DeleteShader(fs);
        int ok = 0;
        _gl.GetProgramiv(id, LINK_STATUS, &ok);
        if (ok == 0)
        {
            int len = 0;
            _gl.GetProgramiv(id, INFO_LOG_LENGTH, &len);
            var log = new byte[Math.Max(len, 1)];
            fixed (byte* p = log)
            {
                _gl.GetProgramInfoLog(id, log.Length, &len, p);
            }

            _gl.DeleteProgram(id);
            throw new ShaderCompilationException(
                $"Program '{descriptor.Label}' failed to link: {Encoding.UTF8.GetString(log, 0, Math.Max(0, len))}",
                descriptor.VertexSource + "\n// ---- fragment ----\n" + descriptor.FragmentSource);
        }

        _gl.UseProgram(id);
        _program = null;
        foreach (var block in descriptor.UniformBlocks)
        {
            fixed (byte* name = GlApi.Utf8Z(block.Name))
            {
                var index = _gl.GetUniformBlockIndex(id, name);
                if (index != INVALID_INDEX)
                {
                    _gl.UniformBlockBinding(id, index, (uint)block.Slot);
                }
            }
        }

        foreach (var sampler in descriptor.Samplers)
        {
            fixed (byte* name = GlApi.Utf8Z(sampler.Name))
            {
                var location = _gl.GetUniformLocation(id, name);
                if (location >= 0)
                {
                    _gl.Uniform1i(location, sampler.Unit);
                }
            }
        }

        int drawLocation;
        fixed (byte* name = "u_draw\0"u8)
        {
            drawLocation = _gl.GetUniformLocation(id, name);
        }

        return new GlProgram(this, id, drawLocation);
    }

    private uint Compile(int stage, string source, string? label)
    {
        var id = _gl.CreateShader(stage);
        var bytes = Encoding.UTF8.GetBytes(source);
        fixed (byte* p = bytes)
        {
            var ptr = p;
            var length = bytes.Length;
            _gl.ShaderSource(id, 1, &ptr, &length);
        }

        _gl.CompileShader(id);
        int ok = 0;
        _gl.GetShaderiv(id, COMPILE_STATUS, &ok);
        if (ok != 0)
        {
            return id;
        }

        int len = 0;
        _gl.GetShaderiv(id, INFO_LOG_LENGTH, &len);
        var log = new byte[Math.Max(len, 1)];
        fixed (byte* p = log)
        {
            _gl.GetShaderInfoLog(id, log.Length, &len, p);
        }

        _gl.DeleteShader(id);
        var kind = stage == VERTEX_SHADER ? "vertex" : "fragment";
        throw new ShaderCompilationException(
            $"The {kind} shader of '{label}' failed to compile: {Encoding.UTF8.GetString(log, 0, Math.Max(0, len))}", source);
    }

    // ---- Vertex input ----

    /// <inheritdoc/>
    public override GpuVertexInput CreateVertexInput(in VertexInputDescriptor descriptor)
    {
        uint vao = 0;
        _gl.GenVertexArrays(1, &vao);
        BindVao(vao);
        foreach (var a in descriptor.Attributes)
        {
            var buffer = (GlBuffer)descriptor.Buffers[a.BufferIndex];
            _gl.BindBuffer(ARRAY_BUFFER, buffer.Id);
            _gl.EnableVertexAttribArray((uint)a.Location);
            var (size, type, normalized, integer) = MapVertexFormat(a.Format);
            if (integer)
            {
                _gl.VertexAttribIPointer((uint)a.Location, size, type, a.Stride, a.Offset);
            }
            else
            {
                _gl.VertexAttribPointer((uint)a.Location, size, type, (byte)(normalized ? 1 : 0), a.Stride, a.Offset);
            }

            if (a.PerInstance)
            {
                _gl.VertexAttribDivisor((uint)a.Location, 1);
            }
        }

        var indexSize = 0;
        if (descriptor.IndexBuffer is GlBuffer index)
        {
            _gl.BindBuffer(ELEMENT_ARRAY_BUFFER, index.Id);
            indexSize = descriptor.IndexFormat == IndexFormat.UInt16 ? 2 : 4;
        }

        var input = new GlVertexInput(this, vao, indexSize);
        _vertexInput = input;
        return input;
    }

    private static (int Size, int Type, bool Normalized, bool Integer) MapVertexFormat(VertexFormat f) => f switch
    {
        VertexFormat.Float1 => (1, FLOAT, false, false),
        VertexFormat.Float2 => (2, FLOAT, false, false),
        VertexFormat.Float3 => (3, FLOAT, false, false),
        VertexFormat.Float4 => (4, FLOAT, false, false),
        VertexFormat.UByte4 => (4, UNSIGNED_BYTE, false, false),
        VertexFormat.UByte4Norm => (4, UNSIGNED_BYTE, true, false),
        VertexFormat.UByte3Norm => (3, UNSIGNED_BYTE, true, false),
        VertexFormat.UByte2Norm => (2, UNSIGNED_BYTE, true, false),
        VertexFormat.SByte4Norm => (4, BYTE, true, false),
        VertexFormat.SByte3Norm => (3, BYTE, true, false),
        VertexFormat.SByte2Norm => (2, BYTE, true, false),
        VertexFormat.UShort2 => (2, UNSIGNED_SHORT, false, false),
        VertexFormat.UShort4 => (4, UNSIGNED_SHORT, false, false),
        VertexFormat.UShort2Norm => (2, UNSIGNED_SHORT, true, false),
        VertexFormat.UShort3Norm => (3, UNSIGNED_SHORT, true, false),
        VertexFormat.UShort4Norm => (4, UNSIGNED_SHORT, true, false),
        VertexFormat.Short2Norm => (2, SHORT, true, false),
        VertexFormat.Short3Norm => (3, SHORT, true, false),
        VertexFormat.Short4Norm => (4, SHORT, true, false),
        VertexFormat.UInt4 => (4, UNSIGNED_INT, false, true),
        _ => throw new NotSupportedException($"Vertex format {f} is not supported."),
    };

    private void BindVao(uint vao)
    {
        if (_boundVao != vao)
        {
            _gl.BindVertexArray(vao);
            _boundVao = vao;
        }
    }

    // ---- Framebuffers ----

    /// <inheritdoc/>
    public override GpuFramebuffer CreateFramebuffer(in FramebufferDescriptor descriptor)
    {
        uint id = 0;
        _gl.GenFramebuffers(1, &id);
        var previous = _boundFbo;
        BindFbo(id);
        var colors = descriptor.Colors ?? [];
        int width = 0, height = 0;
        for (var i = 0; i < colors.Length; i++)
        {
            Attach(COLOR_ATTACHMENT0 + i, colors[i]);
            width = Math.Max(1, colors[i].Texture.Width >> colors[i].MipLevel);
            height = Math.Max(1, colors[i].Texture.Height >> colors[i].MipLevel);
        }

        if (descriptor.Depth is { } depth)
        {
            var stencil = depth.Texture.Descriptor.Format == TextureFormat.Depth24Stencil8;
            Attach(stencil ? DEPTH_STENCIL_ATTACHMENT : DEPTH_ATTACHMENT, depth);
            if (colors.Length == 0)
            {
                width = Math.Max(1, depth.Texture.Width >> depth.MipLevel);
                height = Math.Max(1, depth.Texture.Height >> depth.MipLevel);
            }
        }

        if (colors.Length == 0)
        {
            int none = NONE;
            _gl.DrawBuffers(1, &none);
            _gl.ReadBuffer(NONE);
        }
        else
        {
            var buffers = stackalloc int[colors.Length];
            for (var i = 0; i < colors.Length; i++)
            {
                buffers[i] = COLOR_ATTACHMENT0 + i;
            }

            _gl.DrawBuffers(colors.Length, buffers);
        }

        var status = _gl.CheckFramebufferStatus(FRAMEBUFFER);
        if (status != FRAMEBUFFER_COMPLETE)
        {
            _gl.DeleteFramebuffers(1, &id);
            _boundFbo = uint.MaxValue;
            throw new InvalidOperationException($"Framebuffer '{descriptor.Label}' is incomplete (status 0x{status:X}).");
        }

        // A framebuffer created in the middle of a pass must not redirect that pass.
        if (previous != uint.MaxValue)
        {
            BindFbo(previous);
        }

        return new GlFramebuffer(this, id, width, height);
    }

    private void Attach(int point, in FramebufferAttachment a)
    {
        var t = (GlTexture)a.Texture;
        if (t.IsRenderbuffer)
        {
            _gl.FramebufferRenderbuffer(FRAMEBUFFER, point, RENDERBUFFER, t.Id);
            return;
        }

        switch (t.Descriptor.Dimension)
        {
            case TextureDimension.Cube:
                _gl.FramebufferTexture2D(FRAMEBUFFER, point, TEXTURE_CUBE_MAP_POSITIVE_X + a.Layer, t.Id, a.MipLevel);
                break;
            case TextureDimension.Texture2DArray:
            case TextureDimension.Texture3D:
                _gl.FramebufferTextureLayer(FRAMEBUFFER, point, t.Id, a.MipLevel, a.Layer);
                break;
            default:
                _gl.FramebufferTexture2D(FRAMEBUFFER, point, TEXTURE_2D, t.Id, a.MipLevel);
                break;
        }
    }

    private void BindFbo(uint id)
    {
        if (_boundFbo != id)
        {
            _gl.BindFramebuffer(FRAMEBUFFER, id);
            _boundFbo = id;
        }
    }

    /// <inheritdoc/>
    public override void SetSurface(nint nativeHandle, int width, int height)
    {
        _surfaceFbo = (uint)nativeHandle;
        _surfaceWidth = width;
        _surfaceHeight = height;
    }

    // ---- Passes and state ----

    /// <inheritdoc/>
    public override void BeginPass(in RenderPassDescriptor pass)
    {
        var fb = (GlFramebuffer?)pass.Target;
        BindFbo(fb?.Id ?? _surfaceFbo);
        _passHeight = fb?.Height ?? _surfaceHeight;
        var w = pass.ViewportWidth > 0 ? pass.ViewportWidth : fb?.Width ?? _surfaceWidth;
        var h = pass.ViewportHeight > 0 ? pass.ViewportHeight : fb?.Height ?? _surfaceHeight;
        _gl.Viewport(pass.ViewportX, pass.ViewportY, w, h);
        if (pass.ScissorToViewport)
        {
            _gl.Enable(SCISSOR_TEST);
            _gl.Scissor(pass.ViewportX, pass.ViewportY, w, h);
            _scissorEnabled = true;
        }
        else if (_scissorEnabled || !_stateValid)
        {
            _gl.Disable(SCISSOR_TEST);
            _scissorEnabled = false;
        }

        var mask = 0;
        if (pass.ClearColor)
        {
            _gl.ColorMask(1, 1, 1, 1);
            _state.ColorWrite = true;
            var c = pass.ClearColorValue;
            _gl.ClearColor(c.X, c.Y, c.Z, c.W);
            mask |= COLOR_BUFFER_BIT;
        }

        if (pass.ClearDepth)
        {
            _gl.DepthMask(1);
            _state.DepthWrite = true;
            if (_gl.ClearDepth is not null)
            {
                _gl.ClearDepth(pass.ClearDepthValue);
            }
            else
            {
                _gl.ClearDepthf(pass.ClearDepthValue);
            }
            mask |= DEPTH_BUFFER_BIT;
        }

        if (pass.ClearStencil)
        {
            _gl.ClearStencil(0);
            mask |= STENCIL_BUFFER_BIT;
        }

        if (mask != 0)
        {
            _gl.Clear(mask);
        }

        Statistics.Passes++;
    }

    /// <inheritdoc/>
    public override void EndPass()
    {
    }

    /// <inheritdoc/>
    public override void SetViewport(int x, int y, int width, int height) => _gl.Viewport(x, y, width, height);

    /// <inheritdoc/>
    public override void SetScissor(int x, int y, int width, int height, bool enabled)
    {
        if (enabled)
        {
            _gl.Enable(SCISSOR_TEST);
            _gl.Scissor(x, y, width, height);
        }
        else
        {
            _gl.Disable(SCISSOR_TEST);
        }

        _scissorEnabled = enabled;
    }

    /// <inheritdoc/>
    public override void SetProgram(GpuProgram program)
    {
        var p = (GlProgram)program;
        if (!ReferenceEquals(_program, p))
        {
            _gl.UseProgram(p.Id);
            _program = p;
            Statistics.ProgramSwitches++;
        }
    }

    /// <inheritdoc/>
    public override void SetRenderState(in RenderState s)
    {
        if (_stateValid && _state.Equals(s))
        {
            return;
        }

        var all = !_stateValid;
        Statistics.StateChanges++;
        if (all || s.Cull != _state.Cull)
        {
            if (s.Cull == CullMode.None)
            {
                _gl.Disable(CULL_FACE);
            }
            else
            {
                _gl.Enable(CULL_FACE);
                _gl.CullFace(s.Cull == CullMode.Back ? BACK : FRONT);
            }
        }

        if (all || s.FrontFaceClockwise != _state.FrontFaceClockwise)
        {
            _gl.FrontFace(s.FrontFaceClockwise ? CW : CCW);
        }

        if (all || s.DepthTest != _state.DepthTest)
        {
            if (s.DepthTest)
            {
                _gl.Enable(DEPTH_TEST);
            }
            else
            {
                _gl.Disable(DEPTH_TEST);
            }
        }

        if (all || s.DepthWrite != _state.DepthWrite)
        {
            _gl.DepthMask((byte)(s.DepthWrite ? 1 : 0));
        }

        if (all || s.DepthCompare != _state.DepthCompare)
        {
            _gl.DepthFunc(NEVER + (int)s.DepthCompare);
        }

        if (all || s.ColorWrite != _state.ColorWrite)
        {
            var m = (byte)(s.ColorWrite ? 1 : 0);
            _gl.ColorMask(m, m, m, m);
        }

        if (all || s.Blend != _state.Blend)
        {
            if (s.Blend == BlendMode.Opaque)
            {
                _gl.Disable(BLEND);
            }
            else
            {
                _gl.Enable(BLEND);
                _gl.BlendEquation(FUNC_ADD);
                switch (s.Blend)
                {
                    case BlendMode.Alpha:
                        _gl.BlendFuncSeparate(SRC_ALPHA, ONE_MINUS_SRC_ALPHA, ONE, ONE_MINUS_SRC_ALPHA);
                        break;
                    case BlendMode.PremultipliedAlpha:
                        _gl.BlendFuncSeparate(ONE, ONE_MINUS_SRC_ALPHA, ONE, ONE_MINUS_SRC_ALPHA);
                        break;
                    case BlendMode.Additive:
                        _gl.BlendFuncSeparate(SRC_ALPHA, ONE, ONE, ONE);
                        break;
                    case BlendMode.Multiply:
                        _gl.BlendFuncSeparate(DST_COLOR, ZERO, ZERO, ONE);
                        break;
                    default:
                        _gl.BlendFuncSeparate(ZERO, ONE_MINUS_SRC_COLOR, ZERO, ONE);
                        break;
                }
            }
        }

        if (all || s.AlphaToCoverage != _state.AlphaToCoverage)
        {
            if (s.AlphaToCoverage)
            {
                _gl.Enable(SAMPLE_ALPHA_TO_COVERAGE);
            }
            else
            {
                _gl.Disable(SAMPLE_ALPHA_TO_COVERAGE);
            }
        }

        if (all || s.DepthBiasSlope != _state.DepthBiasSlope || s.DepthBiasConstant != _state.DepthBiasConstant)
        {
            if (s.DepthBiasSlope != 0f || s.DepthBiasConstant != 0f)
            {
                _gl.Enable(POLYGON_OFFSET_FILL);
                _gl.PolygonOffset(s.DepthBiasSlope, s.DepthBiasConstant);
            }
            else
            {
                _gl.Disable(POLYGON_OFFSET_FILL);
            }
        }

        _state = s;
        _stateValid = true;
    }

    /// <inheritdoc/>
    public override void SetUniformBuffer(int slot, GpuBuffer buffer, int offset, int size)
    {
        var b = (GlBuffer)buffer;
        if (_slotBuffer[slot] == b.Id && _slotOffset[slot] == offset && _slotSize[slot] == size)
        {
            return;
        }

        _gl.BindBufferRange(UNIFORM_BUFFER, (uint)slot, b.Id, offset, size);
        _slotBuffer[slot] = b.Id;
        _slotOffset[slot] = offset;
        _slotSize[slot] = size;
    }

    private void SetActiveUnit(int unit)
    {
        if (_activeUnit != unit)
        {
            _gl.ActiveTexture(TEXTURE0 + unit);
            _activeUnit = unit;
        }
    }

    /// <inheritdoc/>
    public override void SetTexture(int unit, GpuTexture texture, GpuSampler sampler)
    {
        var t = (GlTexture)texture;
        var s = (GlSampler)sampler;
        if (_unitTexture[unit] != t.Id || _unitTarget[unit] != t.Target)
        {
            SetActiveUnit(unit);
            _gl.BindTexture(t.Target, t.Id);
            _unitTexture[unit] = t.Id;
            _unitTarget[unit] = t.Target;
            Statistics.TextureBinds++;
        }

        if (_unitSampler[unit] != s.Id)
        {
            _gl.BindSampler((uint)unit, s.Id);
            _unitSampler[unit] = s.Id;
        }
    }

    /// <inheritdoc/>
    public override void SetDrawConstants(int c0, int c1, int c2, int c3)
    {
        _draw0 = c0;
        _draw1 = c1;
        _draw2 = c2;
        _draw3 = c3;
    }

    /// <inheritdoc/>
    public override void SetVertexInput(GpuVertexInput input)
    {
        var v = (GlVertexInput)input;
        BindVao(v.Id);
        _vertexInput = v;
    }

    private bool PrepareDraw()
    {
        var p = _program;
        if (p is null || _vertexInput is null)
        {
            return false;
        }

        if (p.DrawLocation >= 0 && (p.Draw0 != _draw0 || p.Draw1 != _draw1 || p.Draw2 != _draw2 || p.Draw3 != _draw3))
        {
            _gl.Uniform4i(p.DrawLocation, _draw0, _draw1, _draw2, _draw3);
            p.Draw0 = _draw0;
            p.Draw1 = _draw1;
            p.Draw2 = _draw2;
            p.Draw3 = _draw3;
        }

        return true;
    }

    private static int MapTopology(PrimitiveTopology t) => t switch
    {
        PrimitiveTopology.TriangleStrip => TRIANGLE_STRIP,
        PrimitiveTopology.Lines => LINES,
        PrimitiveTopology.LineStrip => LINE_STRIP,
        PrimitiveTopology.Points => POINTS,
        _ => TRIANGLES,
    };

    private void Count(PrimitiveTopology topology, int count, int instances)
    {
        Statistics.DrawCalls++;
        if (topology == PrimitiveTopology.Triangles)
        {
            Statistics.Triangles += (long)(count / 3) * instances;
        }
        else if (topology == PrimitiveTopology.TriangleStrip)
        {
            Statistics.Triangles += (long)Math.Max(0, count - 2) * instances;
        }
    }

    /// <inheritdoc/>
    public override void Draw(PrimitiveTopology topology, int firstVertex, int vertexCount, int instanceCount)
    {
        if (vertexCount <= 0 || instanceCount <= 0 || !PrepareDraw())
        {
            return;
        }

        if (instanceCount == 1)
        {
            _gl.DrawArrays(MapTopology(topology), firstVertex, vertexCount);
        }
        else
        {
            _gl.DrawArraysInstanced(MapTopology(topology), firstVertex, vertexCount, instanceCount);
        }

        Count(topology, vertexCount, instanceCount);
    }

    /// <inheritdoc/>
    public override void DrawIndexed(PrimitiveTopology topology, int firstIndex, int indexCount, int instanceCount)
    {
        if (indexCount <= 0 || instanceCount <= 0 || !PrepareDraw())
        {
            return;
        }

        var size = _vertexInput!.IndexSize;
        if (size == 0)
        {
            throw new InvalidOperationException("DrawIndexed needs a vertex input with an index buffer.");
        }

        var type = size == 2 ? UNSIGNED_SHORT : UNSIGNED_INT;
        if (instanceCount == 1)
        {
            _gl.DrawElements(MapTopology(topology), indexCount, type, firstIndex * size);
        }
        else
        {
            _gl.DrawElementsInstanced(MapTopology(topology), indexCount, type, firstIndex * size, instanceCount);
        }

        Count(topology, indexCount, instanceCount);
    }

    /// <inheritdoc/>
    public override void Blit(GpuFramebuffer? source, GpuFramebuffer? destination, bool color, bool depth, bool linear)
    {
        var s = (GlFramebuffer?)source;
        var d = (GlFramebuffer?)destination;
        _gl.BindFramebuffer(READ_FRAMEBUFFER, s?.Id ?? _surfaceFbo);
        _gl.BindFramebuffer(DRAW_FRAMEBUFFER, d?.Id ?? _surfaceFbo);
        _boundFbo = uint.MaxValue;
        if (_scissorEnabled)
        {
            _gl.Disable(SCISSOR_TEST);
            _scissorEnabled = false;
        }

        var mask = (color ? COLOR_BUFFER_BIT : 0) | (depth ? DEPTH_BUFFER_BIT : 0);
        _gl.BlitFramebuffer(
            0, 0, s?.Width ?? _surfaceWidth, s?.Height ?? _surfaceHeight,
            0, 0, d?.Width ?? _surfaceWidth, d?.Height ?? _surfaceHeight,
            mask, linear && !depth ? LINEAR : NEAREST);
    }

    /// <inheritdoc/>
    public override void ReadPixels(GpuFramebuffer? source, int x, int y, int width, int height, Span<byte> rgba)
    {
        if (rgba.Length < width * height * 4)
        {
            throw new ArgumentException("The destination is too small.", nameof(rgba));
        }

        BindForRead(source);
        fixed (byte* p = rgba)
        {
            _gl.ReadPixels(x, y, width, height, RGBA, UNSIGNED_BYTE, p);
        }
    }

    /// <inheritdoc/>
    public override void ReadPixelsFloat(GpuFramebuffer? source, int x, int y, int width, int height, Span<float> rgba)
    {
        if (rgba.Length < width * height * 4)
        {
            throw new ArgumentException("The destination is too small.", nameof(rgba));
        }

        BindForRead(source);
        fixed (float* p = rgba)
        {
            _gl.ReadPixels(x, y, width, height, RGBA, FLOAT, p);
        }
    }

    private void BindForRead(GpuFramebuffer? source)
    {
        var id = ((GlFramebuffer?)source)?.Id ?? _surfaceFbo;
        BindFbo(id);
        if (id != 0)
        {
            _gl.ReadBuffer(COLOR_ATTACHMENT0);
        }
    }

    /// <inheritdoc/>
    public override void InvalidateState()
    {
        _stateValid = false;
        _boundFbo = uint.MaxValue;
        _boundVao = uint.MaxValue;
        _program = null;
        _vertexInput = null;
        _activeUnit = -1;
        Array.Fill(_unitTexture, uint.MaxValue);
        Array.Fill(_unitSampler, uint.MaxValue);
        Array.Fill(_slotBuffer, uint.MaxValue);
        _scissorEnabled = true;
    }

    /// <summary>Leaves the context in the state a host expects after the engine drew: no program, vertex array, sampler
    /// objects or buffers bound, default pixel store and fixed-function state.</summary>
    public void RestoreHostState()
    {
        _gl.UseProgram(0);
        _gl.BindVertexArray(0);
        _gl.BindBuffer(ARRAY_BUFFER, 0);
        _gl.BindBuffer(UNIFORM_BUFFER, 0);
        for (var i = 0; i < _caps.MaxFragmentTextures; i++)
        {
            if (_unitSampler[i] != 0)
            {
                _gl.BindSampler((uint)i, 0);
            }
        }

        _gl.ActiveTexture(TEXTURE0);
        _gl.Disable(SCISSOR_TEST);
        _gl.Disable(BLEND);
        _gl.Disable(CULL_FACE);
        _gl.Disable(DEPTH_TEST);
        _gl.Disable(POLYGON_OFFSET_FILL);
        _gl.Disable(SAMPLE_ALPHA_TO_COVERAGE);
        _gl.DepthMask(1);
        _gl.ColorMask(1, 1, 1, 1);
        _gl.FrontFace(CCW);
        _gl.PixelStorei(UNPACK_ALIGNMENT, 4);
        InvalidateState();
    }

    /// <summary>Applies the engine's pixel store settings again after <see cref="RestoreHostState"/>.</summary>
    public void BeginFrame()
    {
        _gl.PixelStorei(UNPACK_ALIGNMENT, 1);
        _gl.PixelStorei(PACK_ALIGNMENT, 1);
    }

    /// <inheritdoc/>
    public override void Dispose()
    {
        var vao = _scratchVao;
        _gl.DeleteVertexArrays(1, &vao);
    }
}
