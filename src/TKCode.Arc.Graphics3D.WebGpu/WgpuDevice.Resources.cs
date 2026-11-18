using System.Buffers;
using System.Runtime.InteropServices;

namespace TKCode.Arc.Graphics3D.Rhi.WebGpu;

// Buffers, textures, samplers, vertex inputs and framebuffers.
public sealed unsafe partial class WgpuDevice
{
    private const int MaxUnits = 16;
    private const int MaxSlots = 8;
    private const int MaxStorageSlots = 8;
    private const int LabelBytes = 128;

    private int _nextId = 1;
    private WgpuTexture _dummy2D = null!;
    private WgpuTexture _dummyCube = null!;
    private WgpuTexture _dummyArray = null!;
    private WgpuTexture _dummy3D = null!;
    private WgpuTexture _dummyDepth = null!;
    private WgpuSampler _linearSampler = null!;
    private WgpuSampler _compareSampler = null!;
    private WgpuBuffer _dummyUniform = null!;

    private void CreateDefaults()
    {
        ReadOnlySpan<byte> white = [255, 255, 255, 255];
        _dummy2D = (WgpuTexture)CreateTexture(TextureDescriptor.Texture2D(TextureFormat.RGBA8, 1, 1));
        WriteTexture(_dummy2D, 0, 0, 0, 0, 1, 1, white);
        _dummyCube = (WgpuTexture)CreateTexture(TextureDescriptor.Cube(TextureFormat.RGBA8, 1));
        var layered = TextureDescriptor.Texture2D(TextureFormat.RGBA8, 1, 1);
        layered.Dimension = TextureDimension.Texture2DArray;
        _dummyArray = (WgpuTexture)CreateTexture(layered);
        layered.Dimension = TextureDimension.Texture3D;
        _dummy3D = (WgpuTexture)CreateTexture(layered);
        _dummyDepth = (WgpuTexture)CreateTexture(TextureDescriptor.Texture2D(TextureFormat.Depth24, 1, 1));
        _linearSampler = (WgpuSampler)CreateSampler(SamplerDescriptor.LinearClamp);
        var compare = SamplerDescriptor.LinearClamp;
        compare.CompareEnabled = true;
        compare.Compare = CompareFunction.LessEqual;
        _compareSampler = (WgpuSampler)CreateSampler(compare);

        // Stands in for a uniform block the program declares but nothing was bound to. wgpu fills new buffers with zeros.
        _dummyUniform = (WgpuBuffer)CreateBuffer(BufferUsage.Uniform, 65536, false, "unbound");
    }

    private void DisposeDefaults()
    {
        _dummy2D.Dispose();
        _dummyCube.Dispose();
        _dummyArray.Dispose();
        _dummy3D.Dispose();
        _dummyDepth.Dispose();
        _linearSampler.Dispose();
        _compareSampler.Dispose();
        _dummyUniform.Dispose();
        _surface?.Dispose();
        _surfaceColor?.Dispose();
        _surfaceDepth?.Dispose();
    }

    // ---- Buffers ----

    /// <inheritdoc/>
    public override GpuBuffer CreateBuffer(BufferUsage usage, int sizeInBytes, bool dynamic, string? label = null)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(sizeInBytes);
        var labelBytes = stackalloc byte[LabelBytes];
        var descriptor = new WgpuBufferDescriptor
        {
            Label = Label(label, labelBytes, LabelBytes),
            Usage = Wgpu.BufferUsageCopyDst | usage switch
            {
                BufferUsage.Index => Wgpu.BufferUsageIndex,
                BufferUsage.Uniform => Wgpu.BufferUsageUniform,

                // The output of a compute program can be drawn from (instance data) and read back.
                BufferUsage.Storage => Wgpu.BufferUsageStorage | Wgpu.BufferUsageVertex | Wgpu.BufferUsageCopySrc,
                _ => Wgpu.BufferUsageVertex,
            },

            // Queue writes and copies work in units of four bytes.
            Size = (ulong)Math.Max(4, (sizeInBytes + 3) & ~3),
        };
        var handle = _api.DeviceCreateBuffer(_device, &descriptor);
        if (handle == 0)
        {
            throw new InvalidOperationException($"wgpu could not create the buffer '{label}' ({sizeInBytes} bytes).");
        }

        return new WgpuBuffer(this, handle, _nextId++, usage, sizeInBytes);
    }

    /// <inheritdoc/>
    public override void WriteBuffer(GpuBuffer buffer, int offset, ReadOnlySpan<byte> data)
    {
        var b = (WgpuBuffer)buffer;
        if (data.IsEmpty)
        {
            return;
        }

        if (offset < 0 || offset + data.Length > b.SizeInBytes)
        {
            throw new ArgumentOutOfRangeException(nameof(offset), "The write does not fit in the buffer.");
        }

        if ((offset & 3) != 0)
        {
            throw new ArgumentException("WebGPU writes buffers at offsets that are multiples of four bytes.", nameof(offset));
        }

        // The write takes effect at once, ahead of the draws recorded so far: if those read the buffer, send them first.
        if (b.LastUsedPass == _passSerial && _pass != 0)
        {
            SplitPass();
        }

        var aligned = data.Length & ~3;
        fixed (byte* p = data)
        {
            if (aligned > 0)
            {
                _api.QueueWriteBuffer(_queue, b.Handle, (ulong)offset, p, (nuint)aligned);
            }

            if (aligned != data.Length)
            {
                uint tail = 0;
                data[aligned..].CopyTo(new Span<byte>(&tail, 4));
                _api.QueueWriteBuffer(_queue, b.Handle, (ulong)(offset + aligned), &tail, 4);
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

        // WebGPU knows one and four samples per pixel, and not every format has four (32-bit floats do not): such a
        // texture is single-sampled, and a blit from it is a copy instead of a resolve.
        d.Samples = d.Samples > 1 && Supports(d.Format, _multisampled, multisampled: true) ? 4 : 1;
        var format = WgpuConversions.Texture(d.Format);
        var renderable = d.Dimension != TextureDimension.Texture3D && IsRenderable(d.Format);
        var labelBytes = stackalloc byte[LabelBytes];
        var native = new WgpuTextureDescriptor
        {
            Label = Label(d.Label, labelBytes, LabelBytes),
            Usage = Wgpu.TextureUsageTextureBinding | (renderable ? Wgpu.TextureUsageRenderAttachment : 0)
                | (d.Samples > 1 ? 0 : Wgpu.TextureUsageCopyDst | Wgpu.TextureUsageCopySrc),
            Dimension = d.Dimension == TextureDimension.Texture3D ? Wgpu.TextureDimension3D : Wgpu.TextureDimension2D,
            Size = new WgpuExtent3D
            {
                Width = (uint)d.Width,
                Height = (uint)d.Height,
                DepthOrArrayLayers = d.Dimension switch
                {
                    TextureDimension.Cube => 6,
                    TextureDimension.Texture2D => 1,
                    _ => (uint)Math.Max(1, d.DepthOrLayers),
                },
            },
            Format = format,
            MipLevelCount = (uint)d.MipLevels,
            SampleCount = (uint)d.Samples,
        };
        var handle = _api.DeviceCreateTexture(_device, &native);
        if (handle == 0)
        {
            throw new InvalidOperationException($"wgpu could not create the texture '{d.Label}' ({d.Format}, {d.Width}x{d.Height}).");
        }

        return new WgpuTexture(this, handle, _nextId++, d, format);
    }

    // Per format, 0: not asked yet, 1: yes, 2: no.
    private readonly byte[] _renderable = new byte[64];
    private readonly byte[] _multisampled = new byte[64];

    /// <summary>True when textures of the format can be render targets on this adapter. WebGPU fixes that per format,
    /// but adapters below its baseline (OpenGL ES) render to fewer, so the device asks once by trying.</summary>
    private bool IsRenderable(TextureFormat format)
    {
        if (format.IsCompressed() || (format == TextureFormat.RG11B10F && !_rg11b10Renderable))
        {
            return false;
        }

        return Supports(format, _renderable, multisampled: false);
    }

    private bool Supports(TextureFormat format, byte[] known, bool multisampled)
    {
        ref var answer = ref known[(int)format];
        if (answer == 0)
        {
            answer = !format.IsCompressed() && CaptureError((WgpuConversions.Texture(format), multisampled ? 4u : 1u), &TryRenderTarget) is null ? (byte)1 : (byte)2;
        }

        return answer == 1;
    }

    private static void TryRenderTarget(WgpuDevice device, (uint Format, uint Samples) request)
    {
        var descriptor = new WgpuTextureDescriptor
        {
            Usage = Wgpu.TextureUsageTextureBinding | Wgpu.TextureUsageRenderAttachment,
            Dimension = Wgpu.TextureDimension2D,
            Size = new WgpuExtent3D { Width = 4, Height = 4, DepthOrArrayLayers = 1 },
            Format = request.Format,
            MipLevelCount = 1,
            SampleCount = request.Samples,
        };
        var texture = device._api.DeviceCreateTexture(device._device, &descriptor);
        if (texture != 0)
        {
            device._api.TextureRelease(texture);
        }
    }

    /// <summary>Bytes per pixel that <see cref="WriteTexture"/> expects: float formats are always written as 32-bit
    /// floats per channel (the device converts to half floats), RG11B10F as three floats. Same as the OpenGL device.</summary>
    public static int UploadBytesPerPixel(TextureFormat format) => WgpuConversions.UploadBytesPerPixel(format);

    /// <inheritdoc/>
    public override void WriteTexture(GpuTexture texture, int mipLevel, int layer, int x, int y, int width, int height, ReadOnlySpan<byte> data)
    {
        var t = (WgpuTexture)texture;
        var d = t.Descriptor;
        if (d.Samples > 1)
        {
            throw new InvalidOperationException("A multisampled texture cannot be written.");
        }

        if (t.LastUsedPass == _passSerial && _pass != 0)
        {
            SplitPass();
        }

        var destination = new WgpuTexelCopyTextureInfo
        {
            Texture = t.Handle,
            MipLevel = (uint)mipLevel,
            Origin = new WgpuOrigin3D { X = (uint)x, Y = (uint)y, Z = (uint)layer },
            Aspect = Wgpu.AspectAll,
        };
        if (d.Format.IsCompressed())
        {
            // Whole 4x4 blocks: a small level still occupies one block.
            var blocksX = (width + 3) / 4;
            var blocksY = (height + 3) / 4;
            var blockBytes = WgpuConversions.BlockBytes(d.Format);
            if (data.Length < blocksX * blocksY * blockBytes)
            {
                throw new ArgumentException($"The texture write needs {blocksX * blocksY * blockBytes} bytes but got {data.Length}.", nameof(data));
            }

            Write(destination, data, blocksX * blockBytes, blocksY, blocksX * 4, blocksY * 4);
            return;
        }

        var upload = WgpuConversions.UploadBytesPerPixel(d.Format);
        if (upload == 0)
        {
            throw new NotSupportedException($"Textures of format {d.Format} cannot be written.");
        }

        var expected = (long)width * height * upload;
        if (data.Length < expected)
        {
            throw new ArgumentException($"The texture write needs {expected} bytes but got {data.Length}.", nameof(data));
        }

        data = data[..(int)expected];
        var stored = WgpuConversions.StoredBytesPerPixel(d.Format);
        if (stored == upload)
        {
            Write(destination, data, width * stored, height, width, height);
            return;
        }

        // Half-float and packed-float formats arrive as 32-bit floats.
        var floats = MemoryMarshal.Cast<byte, float>(data);
        var rented = ArrayPool<byte>.Shared.Rent(width * height * stored);
        try
        {
            var converted = rented.AsSpan(0, width * height * stored);
            if (d.Format == TextureFormat.RG11B10F)
            {
                var packed = MemoryMarshal.Cast<byte, uint>(converted);
                for (var i = 0; i < packed.Length; i++)
                {
                    packed[i] = WgpuConversions.PackRg11b10(floats[i * 3], floats[(i * 3) + 1], floats[(i * 3) + 2]);
                }
            }
            else
            {
                WgpuConversions.FloatToHalf(floats, MemoryMarshal.Cast<byte, ushort>(converted));
            }

            Write(destination, converted, width * stored, height, width, height);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(rented);
        }

        void Write(WgpuTexelCopyTextureInfo target, ReadOnlySpan<byte> bytes, int bytesPerRow, int rows, int copyWidth, int copyHeight)
        {
            var layout = new WgpuTexelCopyBufferLayout { Offset = 0, BytesPerRow = (uint)bytesPerRow, RowsPerImage = (uint)rows };
            var extent = new WgpuExtent3D { Width = (uint)copyWidth, Height = (uint)copyHeight, DepthOrArrayLayers = 1 };
            fixed (byte* p = bytes)
            {
                _api.QueueWriteTexture(_queue, &target, p, (nuint)bytes.Length, &layout, &extent);
            }

            Statistics.BufferUploads++;
            Statistics.BufferUploadBytes += bytes.Length;
        }
    }

    /// <inheritdoc/>
    public override GpuSampler CreateSampler(in SamplerDescriptor descriptor) => new WgpuSampler(this, descriptor, CreateNativeSampler(descriptor, filtering: true), _nextId++);

    private nint CreateNativeSampler(in SamplerDescriptor d, bool filtering)
    {
        var linear = filtering && d.MagFilter == TextureFilter.Linear && d.MinFilter == TextureFilter.Linear && d.MipFilter != MipFilter.Nearest;
        var native = new WgpuSamplerDescriptor
        {
            AddressModeU = MapWrap(d.WrapU),
            AddressModeV = MapWrap(d.WrapV),
            AddressModeW = MapWrap(d.WrapW),
            MagFilter = filtering && d.MagFilter == TextureFilter.Linear ? Wgpu.FilterLinear : Wgpu.FilterNearest,
            MinFilter = filtering && d.MinFilter == TextureFilter.Linear ? Wgpu.FilterLinear : Wgpu.FilterNearest,
            MipmapFilter = filtering && d.MipFilter == MipFilter.Linear ? Wgpu.FilterLinear : Wgpu.FilterNearest,
            LodMinClamp = 0f,

            // WebGPU has no "base level only" filter: clamping the level does the same.
            LodMaxClamp = d.MipFilter == MipFilter.None ? 0f : 32f,
            Compare = d.CompareEnabled ? Wgpu.CompareNever + (uint)d.Compare : Wgpu.CompareUndefined,

            // Anisotropic filtering is only defined over linear filters.
            MaxAnisotropy = (ushort)(linear && d.MipFilter == MipFilter.Linear && d.MaxAnisotropy > 1f ? Math.Clamp((int)d.MaxAnisotropy, 1, 16) : 1),
        };
        return _api.DeviceCreateSampler(_device, &native);
    }

    private static uint MapWrap(TextureWrap wrap) => wrap switch
    {
        TextureWrap.ClampToEdge => Wgpu.AddressClampToEdge,
        TextureWrap.MirroredRepeat => Wgpu.AddressMirrorRepeat,
        _ => Wgpu.AddressRepeat,
    };

    // ---- Vertex input ----

    /// <summary>WebGPU reads normalized two- and four-component integers and floats, at byte distances that are
    /// multiples of four. Everything else (three-component integers, integers read as unnormalized floats, tightly
    /// packed two-byte attributes) the renderer uploads as floats.</summary>
    public override bool SupportsVertexFormat(VertexFormat format, int stride) => WgpuConversions.Vertex(format) != 0 && (stride & 3) == 0;

    /// <inheritdoc/>
    public override GpuVertexInput CreateVertexInput(in VertexInputDescriptor descriptor)
    {
        var attributes = descriptor.Attributes ?? [];

        // One WebGPU vertex buffer per (buffer, stride, step) of the attributes.
        var slots = new List<(int Buffer, int Stride, bool PerInstance)>();
        var slotOf = new int[attributes.Length];
        for (var i = 0; i < attributes.Length; i++)
        {
            var a = attributes[i];
            if (WgpuConversions.Vertex(a.Format) == 0 || (a.Stride & 3) != 0)
            {
                throw new NotSupportedException($"WebGPU cannot read a vertex attribute of format {a.Format} with a stride of {a.Stride} bytes (GraphicsDevice.SupportsVertexFormat).");
            }

            var key = (a.BufferIndex, a.Stride, a.PerInstance);
            var slot = slots.IndexOf(key);
            if (slot < 0)
            {
                slot = slots.Count;
                slots.Add(key);
            }

            slotOf[i] = slot;
        }

        var signature = new System.Text.StringBuilder();
        var layout = new VertexLayout { Buffers = new WgpuVertexBufferLayout[slots.Count], Attributes = new WgpuVertexAttribute[attributes.Length], Counts = new int[slots.Count] };
        var buffers = new WgpuBuffer[slots.Count];
        var next = 0;
        for (var s = 0; s < slots.Count; s++)
        {
            buffers[s] = (WgpuBuffer)descriptor.Buffers[slots[s].Buffer];
            layout.Buffers[s] = new WgpuVertexBufferLayout
            {
                StepMode = slots[s].PerInstance ? Wgpu.StepModeInstance : Wgpu.StepModeVertex,
                ArrayStride = (ulong)slots[s].Stride,
            };
            signature.Append('|').Append(slots[s].Stride).Append(slots[s].PerInstance ? 'i' : 'v');
            for (var i = 0; i < attributes.Length; i++)
            {
                if (slotOf[i] != s)
                {
                    continue;
                }

                var a = attributes[i];
                layout.Attributes[next++] = new WgpuVertexAttribute { Format = WgpuConversions.Vertex(a.Format), Offset = (ulong)a.Offset, ShaderLocation = (uint)a.Location };
                layout.Counts[s]++;
                signature.Append(' ').Append(a.Location).Append(':').Append((int)a.Format).Append('@').Append(a.Offset);
            }
        }

        var key2 = signature.ToString();
        if (!_vertexLayouts.TryGetValue(key2, out var interned))
        {
            layout.Id = _vertexLayouts.Count + 1;
            _vertexLayouts.Add(key2, layout);
            interned = layout;
        }

        var index = descriptor.IndexBuffer as WgpuBuffer;
        return new WgpuVertexInput(this, interned, buffers, index, index is null ? Wgpu.IndexFormatUndefined
            : descriptor.IndexFormat == IndexFormat.UInt16 ? Wgpu.IndexFormatUint16 : Wgpu.IndexFormatUint32);
    }

    private readonly Dictionary<string, VertexLayout> _vertexLayouts = new(StringComparer.Ordinal);

    /// <summary>The vertex buffer layouts of a vertex input, shared by all inputs with the same attributes: part of
    /// what identifies a render pipeline.</summary>
    private sealed class VertexLayout
    {
        public int Id;
        public required WgpuVertexBufferLayout[] Buffers;
        public required WgpuVertexAttribute[] Attributes;

        // Attributes of each buffer; they sit in Attributes in buffer order.
        public required int[] Counts;
    }

    // ---- Framebuffers ----

    /// <inheritdoc/>
    public override GpuFramebuffer CreateFramebuffer(in FramebufferDescriptor descriptor)
    {
        var colors = descriptor.Colors ?? [];
        if (colors.Length > 8)
        {
            throw new NotSupportedException("A framebuffer has at most 8 color attachments.");
        }

        var views = new nint[colors.Length];
        var slices = new uint[colors.Length];
        var textures = new WgpuTexture[colors.Length];
        ulong formats = 0;
        int width = 0, height = 0, samples = 1;
        for (var i = 0; i < colors.Length; i++)
        {
            var t = (WgpuTexture)colors[i].Texture;
            textures[i] = t;
            views[i] = t.AttachmentView(colors[i].MipLevel, colors[i].Layer, out slices[i]);
            formats |= (ulong)t.Format << (i * 8);
            width = Math.Max(1, t.Width >> colors[i].MipLevel);
            height = Math.Max(1, t.Height >> colors[i].MipLevel);
            samples = t.Descriptor.Samples;
        }

        WgpuTexture? depthTexture = null;
        nint depthView = 0;
        if (descriptor.Depth is { } depth)
        {
            depthTexture = (WgpuTexture)depth.Texture;
            depthView = depthTexture.AttachmentView(depth.MipLevel, depth.Layer, out _);
            if (colors.Length == 0)
            {
                width = Math.Max(1, depthTexture.Width >> depth.MipLevel);
                height = Math.Max(1, depthTexture.Height >> depth.MipLevel);
                samples = depthTexture.Descriptor.Samples;
            }
        }

        var key = new PassFormats(formats, colors.Length, depthTexture?.Format ?? 0, samples);
        if (!_passFormats.TryGetValue(key, out var formatsId))
        {
            formatsId = _passFormats.Count + 1;
            _passFormats.Add(key, formatsId);
        }

        return new WgpuFramebuffer(this, width, height)
        {
            Colors = textures,
            ColorViews = views,
            ColorSlices = slices,
            ColorMips = Array.ConvertAll(colors, c => c.MipLevel),
            ColorLayers = Array.ConvertAll(colors, c => c.Layer),
            Depth = depthTexture,
            DepthView = depthView,
            DepthMip = descriptor.Depth?.MipLevel ?? 0,
            DepthLayer = descriptor.Depth?.Layer ?? 0,
            Samples = samples,
            Formats = key,
            FormatsId = formatsId,
            Label = descriptor.Label,
        };
    }

    private readonly Dictionary<PassFormats, int> _passFormats = [];

    /// <summary>The attachment formats and sample count of a framebuffer: what a render pipeline is built for. Color
    /// formats are packed eight bits each.</summary>
    private readonly record struct PassFormats(ulong Colors, int ColorCount, uint Depth, int Samples);

    private WgpuFramebuffer? _surface;
    private WgpuTexture? _surfaceColor;
    private WgpuTexture? _surfaceDepth;

    /// <summary>Sizes the surface that passes with a null target draw to. The device owns it: an off-screen RGBA8
    /// picture with a depth buffer, read with <see cref="ReadPixels"/> (null source). <paramref name="nativeHandle"/>
    /// is not used; pass 0.</summary>
    public override void SetSurface(nint nativeHandle, int width, int height)
    {
        width = Math.Max(1, width);
        height = Math.Max(1, height);
        if (_surface is not null && _surface.Width == width && _surface.Height == height)
        {
            return;
        }

        _surface?.Dispose();
        _surfaceColor?.Dispose();
        _surfaceDepth?.Dispose();
        var color = TextureDescriptor.Texture2D(TextureFormat.RGBA8, width, height);
        color.Label = "surface";
        _surfaceColor = (WgpuTexture)CreateTexture(color);
        _surfaceDepth = (WgpuTexture)CreateTexture(TextureDescriptor.Texture2D(TextureFormat.Depth24, width, height));
        _surface = (WgpuFramebuffer)CreateFramebuffer(new FramebufferDescriptor { Colors = [_surfaceColor], Depth = _surfaceDepth, Label = "surface" });
    }

    private WgpuFramebuffer Target(GpuFramebuffer? framebuffer) => (WgpuFramebuffer?)framebuffer ?? _surface
        ?? throw new InvalidOperationException("The device has no surface yet: call SetSurface before drawing to a null target.");

    // ---- Resource classes ----

    private sealed class WgpuBuffer : GpuBuffer
    {
        private readonly WgpuDevice _device;

        public WgpuBuffer(WgpuDevice device, nint handle, int id, BufferUsage usage, int size)
        {
            _device = device;
            Handle = handle;
            Id = id;
            Usage = usage;
            SizeInBytes = size;
        }

        public nint Handle { get; }

        public int Id { get; }

        /// <summary>The serial of the last pass that drew with the buffer.</summary>
        public int LastUsedPass;

        protected override void Release()
        {
            _device._bindGroupsStale = true;
            _device.ForgetBuffer(this);
            _device._api.BufferRelease(Handle);
        }
    }

    private sealed class WgpuTexture : GpuTexture
    {
        private readonly WgpuDevice _device;
        private readonly Dictionary<int, nint> _views = [];
        private nint _defaultView;

        public WgpuTexture(WgpuDevice device, nint handle, int id, TextureDescriptor descriptor, uint format)
        {
            _device = device;
            Handle = handle;
            Id = id;
            Descriptor = descriptor;
            Format = format;
        }

        public nint Handle { get; }

        public int Id { get; }

        public uint Format { get; }

        public int LastUsedPass;

        /// <summary>The whole texture as shaders sample it.</summary>
        public nint DefaultView
        {
            get
            {
                if (_defaultView == 0)
                {
                    var d = Descriptor;
                    var view = new WgpuTextureViewDescriptor
                    {
                        Format = Format,
                        Dimension = d.Dimension switch
                        {
                            TextureDimension.Cube => Wgpu.ViewDimensionCube,
                            TextureDimension.Texture2DArray => Wgpu.ViewDimension2DArray,
                            TextureDimension.Texture3D => Wgpu.ViewDimension3D,
                            _ => Wgpu.ViewDimension2D,
                        },
                        BaseMipLevel = 0,
                        MipLevelCount = (uint)d.MipLevels,
                        BaseArrayLayer = 0,
                        ArrayLayerCount = d.Dimension switch
                        {
                            TextureDimension.Cube => 6,
                            TextureDimension.Texture2DArray => (uint)Math.Max(1, d.DepthOrLayers),
                            _ => 1,
                        },

                        // Shaders read the depth of a depth-stencil texture.
                        Aspect = d.Format == TextureFormat.Depth24Stencil8 ? Wgpu.AspectDepthOnly : Wgpu.AspectAll,
                    };
                    _defaultView = _device._api.TextureCreateView(Handle, &view);
                }

                return _defaultView;
            }
        }

        /// <summary>One level of one layer (or of a 3D texture, with the layer as the depth slice) as a render
        /// target.</summary>
        public nint AttachmentView(int mip, int layer, out uint depthSlice)
        {
            var is3D = Descriptor.Dimension == TextureDimension.Texture3D;
            depthSlice = is3D ? (uint)layer : Wgpu.DepthSliceUndefined;
            return LevelView(mip, is3D ? 0 : layer, is3D);
        }

        /// <summary>One level of one layer as a 2D texture.</summary>
        public nint LevelView(int mip, int layer, bool whole3D = false)
        {
            var key = (mip << 16) | layer;
            if (!_views.TryGetValue(key, out var handle))
            {
                var view = new WgpuTextureViewDescriptor
                {
                    Format = Format,
                    Dimension = whole3D ? Wgpu.ViewDimension3D : Wgpu.ViewDimension2D,
                    BaseMipLevel = (uint)mip,
                    MipLevelCount = 1,
                    BaseArrayLayer = (uint)layer,
                    ArrayLayerCount = 1,
                    Aspect = Wgpu.AspectAll,
                };
                handle = _device._api.TextureCreateView(Handle, &view);
                _views.Add(key, handle);
            }

            return handle;
        }

        protected override void Release()
        {
            _device._bindGroupsStale = true;
            _device.ForgetTexture(this);
            foreach (var view in _views.Values)
            {
                _device._api.TextureViewRelease(view);
            }

            if (_defaultView != 0)
            {
                _device._api.TextureViewRelease(_defaultView);
            }

            // Released, not destroyed: draws that are recorded but not sent yet may still read it.
            _device._api.TextureRelease(Handle);
        }
    }

    private sealed class WgpuSampler : GpuSampler
    {
        private readonly WgpuDevice _device;
        private readonly SamplerDescriptor _descriptor;
        private nint _nonFiltering;

        public WgpuSampler(WgpuDevice device, in SamplerDescriptor descriptor, nint handle, int id)
        {
            _device = device;
            _descriptor = descriptor;
            Handle = handle;
            Id = id;
            IsComparison = descriptor.CompareEnabled;
            IsFiltering = descriptor.MagFilter == TextureFilter.Linear || descriptor.MinFilter == TextureFilter.Linear || descriptor.MipFilter == MipFilter.Linear;
        }

        public nint Handle { get; }

        public int Id { get; }

        public bool IsComparison { get; }

        public bool IsFiltering { get; }

        /// <summary>The same sampler with nearest filtering, for textures WebGPU does not filter (depth read as
        /// numbers, 32-bit floats without the feature).</summary>
        public nint NonFiltering
        {
            get
            {
                if (!IsFiltering)
                {
                    return Handle;
                }

                if (_nonFiltering == 0)
                {
                    _nonFiltering = _device.CreateNativeSampler(_descriptor, filtering: false);
                }

                return _nonFiltering;
            }
        }

        protected override void Release()
        {
            _device._bindGroupsStale = true;
            _device.ForgetSampler(this);
            if (_nonFiltering != 0)
            {
                _device._api.SamplerRelease(_nonFiltering);
            }

            _device._api.SamplerRelease(Handle);
        }
    }

    private sealed class WgpuVertexInput : GpuVertexInput
    {
        private readonly WgpuDevice _device;

        public WgpuVertexInput(WgpuDevice device, VertexLayout layout, WgpuBuffer[] buffers, WgpuBuffer? index, uint indexFormat)
        {
            _device = device;
            Layout = layout;
            Buffers = buffers;
            Index = index;
            IndexFormat = indexFormat;
        }

        public VertexLayout Layout { get; }

        public WgpuBuffer[] Buffers { get; }

        public WgpuBuffer? Index { get; }

        public uint IndexFormat { get; }

        protected override void Release()
        {
            if (ReferenceEquals(_device._vertexInput, this))
            {
                _device._vertexInput = null;
            }

            if (ReferenceEquals(_device._appliedInput, this))
            {
                _device._appliedInput = null;
            }
        }
    }

    private sealed class WgpuFramebuffer : GpuFramebuffer
    {
        private readonly WgpuDevice _device;

        public WgpuFramebuffer(WgpuDevice device, int width, int height)
        {
            _device = device;
            Width = width;
            Height = height;
        }

        public required WgpuTexture[] Colors { get; init; }

        public required nint[] ColorViews { get; init; }

        public required uint[] ColorSlices { get; init; }

        public required int[] ColorMips { get; init; }

        public required int[] ColorLayers { get; init; }

        public WgpuTexture? Depth { get; init; }

        public nint DepthView { get; init; }

        public int DepthMip { get; init; }

        public int DepthLayer { get; init; }

        public int Samples { get; init; }

        public PassFormats Formats { get; init; }

        public int FormatsId { get; init; }

        public string? Label { get; init; }

        public bool HasStencil => Depth?.Descriptor.Format == TextureFormat.Depth24Stencil8;

        protected override void Release()
        {
            // The views belong to the textures. A pass that still draws to it ends first.
            if (ReferenceEquals(_device._passTarget, this) && _device._pass != 0)
            {
                _device.EndPass();
            }
        }
    }
}
