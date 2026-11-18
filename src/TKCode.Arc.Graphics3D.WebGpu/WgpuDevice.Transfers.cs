using System.Numerics;
using System.Runtime.InteropServices;
using System.Text;

namespace TKCode.Arc.Graphics3D.Rhi.WebGpu;

// What OpenGL does with one call and WebGPU leaves to the application: mip chains, copies between framebuffers
// (with resolve and scaling), reading pixels back, and clears of part of a target. The device's own shaders are WGSL.
public sealed unsafe partial class WgpuDevice
{
    private readonly Dictionary<string, nint> _utilityModules = new(StringComparer.Ordinal);
    private readonly Dictionary<string, nint> _utilityPipelines = new(StringComparer.Ordinal);
    private readonly Dictionary<int, nint> _utilityLayouts = [];
    private nint _readback;
    private ulong _readbackSize;
    private nint _clearBuffer;

    // One triangle over the target. Row 0 of the target is v = 0, so a copy keeps rows where they are.
    private const string FullScreenWgsl = """
struct VertexOutput {
    @builtin(position) position: vec4f,
    @location(0) uv: vec2f,
}
@vertex fn vs(@builtin(vertex_index) index: u32) -> VertexOutput {
    let p = vec2f(f32((index << 1u) & 2u), f32(index & 2u));
    var result: VertexOutput;
    result.position = vec4f(p * 2.0 - 1.0, 0.0, 1.0);
    result.uv = vec2f(p.x, 1.0 - p.y);
    return result;
}

""";

    // The next mip level: each texel is the mean of the area it covers in the level above. That is 2x2 texels when
    // the size halves exactly, and three texels with unequal weights along an axis of odd size (2k + 1 texels become
    // k), so nothing is dropped at the edge. Texel reads, so it works for every float format.
    private const string MipWgsl = FullScreenWgsl + """
@group(0) @binding(0) var source: texture_2d<f32>;
fn weights(i: i32, n: i32) -> vec3f {
    if (n == 1) {
        return vec3f(1.0, 0.0, 0.0);
    }
    if ((n & 1) == 0) {
        return vec3f(0.5, 0.5, 0.0);
    }
    let k = f32(n / 2);
    return vec3f(k - f32(i), k, f32(i) + 1.0) / f32(n);
}
@fragment fn fs(@builtin(position) position: vec4f) -> @location(0) vec4f {
    let size = vec2i(textureDimensions(source, 0));
    let p = vec2i(position.xy);
    let wx = weights(p.x, size.x);
    let wy = weights(p.y, size.y);
    var sum = vec4f(0.0);
    for (var j = 0; j < 3; j++) {
        for (var i = 0; i < 3; i++) {
            let w = wx[i] * wy[j];
            if (w > 0.0) {
                sum += w * textureLoad(source, min(p * 2 + vec2i(i, j), size - vec2i(1, 1)), 0);
            }
        }
    }
    return sum;
}
""";

    private const string BlitWgsl = FullScreenWgsl + """
@group(0) @binding(0) var source: texture_2d<f32>;
@group(0) @binding(1) var sourceSampler: sampler;
@fragment fn fs(@location(0) uv: vec2f) -> @location(0) vec4f {
    return textureSampleLevel(source, sourceSampler, uv, 0.0);
}
""";

    // WebGPU resolves color only. Depth takes the first sample, as a nearest blit of a multisampled depth buffer does.
    private const string DepthResolveWgsl = FullScreenWgsl + """
@group(0) @binding(0) var source: texture_depth_multisampled_2d;
@fragment fn fs(@builtin(position) position: vec4f) -> @builtin(frag_depth) f32 {
    return textureLoad(source, vec2i(position.xy), 0);
}
""";

    private const string DepthCopyWgsl = FullScreenWgsl + """
@group(0) @binding(0) var source: texture_depth_2d;
@group(0) @binding(1) var sourceSampler: sampler;
@fragment fn fs(@location(0) uv: vec2f) -> @builtin(frag_depth) f32 {
    return textureSampleLevel(source, sourceSampler, uv, 0);
}
""";

    private enum UtilityLayout
    {
        Texture,
        TextureAndSampler,
        UnfilterableTextureAndSampler,
        MultisampledDepth,
        DepthAndSampler,
        Uniform,
    }

    private BindLayout GetUtilityLayout(UtilityLayout kind)
    {
        var entries = stackalloc WgpuBindGroupLayoutEntry[2];
        var count = 1;
        entries[0] = new WgpuBindGroupLayoutEntry { Binding = 0, Visibility = Wgpu.StageVertex | Wgpu.StageFragment };
        entries[1] = new WgpuBindGroupLayoutEntry { Binding = 1, Visibility = Wgpu.StageFragment };
        switch (kind)
        {
            case UtilityLayout.Texture:
                entries[0].Texture = new WgpuTextureBindingLayout { SampleType = Wgpu.SampleTypeUnfilterableFloat, ViewDimension = Wgpu.ViewDimension2D };
                break;
            case UtilityLayout.TextureAndSampler:
                entries[0].Texture = new WgpuTextureBindingLayout { SampleType = Wgpu.SampleTypeFloat, ViewDimension = Wgpu.ViewDimension2D };
                entries[1].Sampler = new WgpuSamplerBindingLayout { Type = Wgpu.SamplerBindingFiltering };
                count = 2;
                break;
            case UtilityLayout.UnfilterableTextureAndSampler:
                entries[0].Texture = new WgpuTextureBindingLayout { SampleType = Wgpu.SampleTypeUnfilterableFloat, ViewDimension = Wgpu.ViewDimension2D };
                entries[1].Sampler = new WgpuSamplerBindingLayout { Type = Wgpu.SamplerBindingNonFiltering };
                count = 2;
                break;
            case UtilityLayout.MultisampledDepth:
                entries[0].Texture = new WgpuTextureBindingLayout { SampleType = Wgpu.SampleTypeDepth, ViewDimension = Wgpu.ViewDimension2D, Multisampled = 1 };
                break;
            case UtilityLayout.DepthAndSampler:
                entries[0].Texture = new WgpuTextureBindingLayout { SampleType = Wgpu.SampleTypeDepth, ViewDimension = Wgpu.ViewDimension2D };
                entries[1].Sampler = new WgpuSamplerBindingLayout { Type = Wgpu.SamplerBindingNonFiltering };
                count = 2;
                break;
            default:
                entries[0].Buffer = new WgpuBufferBindingLayout { Type = Wgpu.BufferBindingUniform };
                break;
        }

        return InternLayout("utility" + (int)kind, entries, count);
    }

    /// <summary>A pipeline of the device's own: a full-screen triangle with one bind group, writing color, depth or both.</summary>
    private nint GetUtilityPipeline(string name, string wgsl, UtilityLayout layoutKind, PassFormats formats, bool writeDepth, ulong colorMask = Wgpu.ColorWriteAll)
    {
        var key = string.Create(System.Globalization.CultureInfo.InvariantCulture, $"{name}|{formats.Colors:X}|{formats.ColorCount}|{formats.Depth}|{formats.Samples}|{writeDepth}|{colorMask}");
        if (_utilityPipelines.TryGetValue(key, out var pipeline))
        {
            return pipeline;
        }

        if (!_utilityModules.TryGetValue(name, out var module))
        {
            module = CompileWgsl(wgsl);
            _utilityModules.Add(name, module);
        }

        if (!_utilityLayouts.TryGetValue((int)layoutKind, out var pipelineLayout))
        {
            var group = GetUtilityLayout(layoutKind).Handle;
            var layoutDescriptor = new WgpuPipelineLayoutDescriptor { BindGroupLayoutCount = 1, BindGroupLayouts = &group };
            pipelineLayout = _api.DeviceCreatePipelineLayout(_device, &layoutDescriptor);
            _utilityLayouts.Add((int)layoutKind, pipelineLayout);
        }

        var vs = stackalloc byte[2] { (byte)'v', (byte)'s' };
        var fs = stackalloc byte[2] { (byte)'f', (byte)'s' };
        var targets = stackalloc WgpuColorTargetState[Math.Max(1, formats.ColorCount)];
        for (var i = 0; i < formats.ColorCount; i++)
        {
            targets[i] = new WgpuColorTargetState { Format = (uint)((formats.Colors >> (i * 8)) & 0xFF), WriteMask = colorMask };
        }

        var fragment = new WgpuFragmentState
        {
            Module = module,
            EntryPoint = new WgpuStringView { Data = fs, Length = 2 },
            TargetCount = (nuint)formats.ColorCount,
            Targets = targets,
        };
        var keep = new WgpuStencilFaceState { Compare = Wgpu.CompareAlways, FailOp = Wgpu.StencilKeep, DepthFailOp = Wgpu.StencilKeep, PassOp = Wgpu.StencilKeep };
        var depth = new WgpuDepthStencilState
        {
            Format = formats.Depth,
            DepthWriteEnabled = writeDepth ? Wgpu.OptionalBoolTrue : Wgpu.OptionalBoolFalse,
            DepthCompare = Wgpu.CompareAlways,
            StencilFront = keep,
            StencilBack = keep,
            StencilReadMask = 0xFFFFFFFF,
        };
        var descriptor = new WgpuRenderPipelineDescriptor
        {
            Layout = pipelineLayout,
            Vertex = new WgpuVertexState { Module = module, EntryPoint = new WgpuStringView { Data = vs, Length = 2 } },
            Primitive = new WgpuPrimitiveState { Topology = Wgpu.TopologyTriangleList, FrontFace = Wgpu.FrontFaceCcw, CullMode = Wgpu.CullNone },
            DepthStencil = formats.Depth == 0 ? null : &depth,
            Multisample = new WgpuMultisampleState { Count = (uint)formats.Samples, Mask = 0xFFFFFFFF },
            Fragment = &fragment,
        };
        pipeline = _api.DeviceCreateRenderPipeline(_device, &descriptor);
        _utilityPipelines.Add(key, pipeline);
        return pipeline;
    }

    private nint TemporaryGroup(UtilityLayout kind, nint view, nint sampler, nint buffer = 0, ulong bufferSize = 0)
    {
        var entries = stackalloc WgpuBindGroupEntry[2];
        entries[0] = new WgpuBindGroupEntry { Binding = 0, TextureView = view, Buffer = buffer, Size = bufferSize };
        entries[1] = new WgpuBindGroupEntry { Binding = 1, Sampler = sampler };
        var descriptor = new WgpuBindGroupDescriptor { Layout = GetUtilityLayout(kind).Handle, EntryCount = sampler != 0 ? 2u : 1u, Entries = entries };
        return _api.DeviceCreateBindGroup(_device, &descriptor);
    }

    /// <summary>Draws the full-screen triangle of a utility pipeline into one view.</summary>
    private void DrawUtility(nint encoder, nint pipeline, nint group, nint colorView, nint depthView, bool hasStencil)
    {
        var color = new WgpuRenderPassColorAttachment { View = colorView, DepthSlice = Wgpu.DepthSliceUndefined, LoadOp = Wgpu.LoadOpClear, StoreOp = Wgpu.StoreOpStore };
        var depth = new WgpuRenderPassDepthStencilAttachment
        {
            View = depthView,
            DepthLoadOp = Wgpu.LoadOpClear,
            DepthStoreOp = Wgpu.StoreOpStore,
            DepthClearValue = 1f,
            StencilLoadOp = hasStencil ? Wgpu.LoadOpLoad : 0,
            StencilStoreOp = hasStencil ? Wgpu.StoreOpStore : 0,
        };
        var descriptor = new WgpuRenderPassDescriptor
        {
            ColorAttachmentCount = colorView == 0 ? 0u : 1u,
            ColorAttachments = colorView == 0 ? null : &color,
            DepthStencilAttachment = depthView == 0 ? null : &depth,
        };
        var pass = _api.CommandEncoderBeginRenderPass(encoder, &descriptor);
        _api.RenderPassSetPipeline(pass, pipeline);
        _api.RenderPassSetBindGroup(pass, 0, group, 0, null);
        _api.RenderPassDraw(pass, 3, 1, 0, 0);
        _api.RenderPassEnd(pass);
        _api.RenderPassRelease(pass);
    }

    // ---- Mipmaps ----

    /// <summary>Fills the mip chain from level 0, one draw per level and layer (WebGPU has no call for it). Formats
    /// that cannot be rendered to (compressed, 3D) keep their levels as they are.</summary>
    public override void GenerateMipmaps(GpuTexture texture)
    {
        var t = (WgpuTexture)texture;
        var d = t.Descriptor;
        if (d.Samples > 1 || d.MipLevels <= 1 || d.Format.IsDepth() || d.Dimension == TextureDimension.Texture3D || !IsRenderable(d.Format))
        {
            return;
        }

        if (t.LastUsedPass == _passSerial && _pass != 0)
        {
            SplitPass();
        }

        var layers = d.Dimension switch
        {
            TextureDimension.Cube => 6,
            TextureDimension.Texture2DArray => Math.Max(1, d.DepthOrLayers),
            _ => 1,
        };
        var pipeline = GetUtilityPipeline("mip", MipWgsl, UtilityLayout.Texture, new PassFormats(t.Format, 1, 0, 1), writeDepth: false);
        var encoder = _api.DeviceCreateCommandEncoder(_device, null);
        var groups = new nint[layers * (d.MipLevels - 1)];
        var next = 0;
        for (var layer = 0; layer < layers; layer++)
        {
            for (var level = 1; level < d.MipLevels; level++)
            {
                var group = TemporaryGroup(UtilityLayout.Texture, t.LevelView(level - 1, layer), 0);
                groups[next++] = group;
                DrawUtility(encoder, pipeline, group, t.LevelView(level, layer), 0, false);
            }
        }

        Submit(encoder);
        foreach (var group in groups)
        {
            _api.BindGroupRelease(group);
        }
    }

    // ---- Blit ----

    /// <summary>Copies color and/or depth between framebuffers: a resolve when the source is multisampled, a texture
    /// copy when sizes and formats agree, a filtered draw otherwise. Rows keep their place.</summary>
    public override void Blit(GpuFramebuffer? source, GpuFramebuffer? destination, bool color, bool depth, bool linear)
    {
        var s = Target(source);
        var d = Target(destination);
        var suspended = SuspendPass();
        var encoder = _api.DeviceCreateCommandEncoder(_device, null);
        nint group = 0;
        nint depthGroup = 0;
        var sameSize = s.Width == d.Width && s.Height == d.Height;
        if (color && s.Colors.Length > 0 && d.Colors.Length > 0)
        {
            var st = s.Colors[0];
            var dt = d.Colors[0];
            if (s.Samples > 1)
            {
                if (d.Samples > 1 || !sameSize || st.Format != dt.Format)
                {
                    throw new NotSupportedException("A multisampled framebuffer resolves into a single-sampled one of the same size and format.");
                }

                var attachment = new WgpuRenderPassColorAttachment
                {
                    View = s.ColorViews[0],
                    DepthSlice = Wgpu.DepthSliceUndefined,
                    ResolveTarget = d.ColorViews[0],
                    LoadOp = Wgpu.LoadOpLoad,
                    StoreOp = Wgpu.StoreOpStore,
                };
                var descriptor = new WgpuRenderPassDescriptor { ColorAttachmentCount = 1, ColorAttachments = &attachment };
                var pass = _api.CommandEncoderBeginRenderPass(encoder, &descriptor);
                _api.RenderPassEnd(pass);
                _api.RenderPassRelease(pass);
            }
            else if (d.Samples == 1 && sameSize && st.Format == dt.Format && !ReferenceEquals(st, dt))
            {
                CopyTexture(encoder, st, s.ColorMips[0], s.ColorLayers[0], dt, d.ColorMips[0], d.ColorLayers[0], s.Width, s.Height);
            }
            else if (d.Samples == 1)
            {
                var filter = linear && IsFilterable(st);
                var kind = IsFilterable(st) ? UtilityLayout.TextureAndSampler : UtilityLayout.UnfilterableTextureAndSampler;
                var pipeline = GetUtilityPipeline("blit", BlitWgsl, kind, new PassFormats(dt.Format, 1, 0, 1), writeDepth: false);
                group = TemporaryGroup(kind, st.LevelView(s.ColorMips[0], s.ColorLayers[0]), filter ? _linearSampler.Handle : _linearSampler.NonFiltering);
                DrawUtility(encoder, pipeline, group, d.ColorViews[0], 0, false);
            }
            else
            {
                throw new NotSupportedException("A blit into a multisampled framebuffer is not supported.");
            }
        }

        if (depth && s.Depth is { } sd && d.Depth is { } dd)
        {
            if (d.Samples > 1)
            {
                throw new NotSupportedException("A blit into a multisampled framebuffer is not supported.");
            }

            if (s.Samples == 1 && sameSize && sd.Format == dd.Format)
            {
                CopyTexture(encoder, sd, s.DepthMip, s.DepthLayer, dd, d.DepthMip, d.DepthLayer, s.Width, s.Height);
            }
            else
            {
                var multisampled = s.Samples > 1;
                var kind = multisampled ? UtilityLayout.MultisampledDepth : UtilityLayout.DepthAndSampler;
                var pipeline = GetUtilityPipeline(
                    multisampled ? "depth-resolve" : "depth-copy", multisampled ? DepthResolveWgsl : DepthCopyWgsl, kind, new PassFormats(0, 0, dd.Format, 1), writeDepth: true);
                depthGroup = TemporaryGroup(kind, sd.LevelView(s.DepthMip, s.DepthLayer), multisampled ? 0 : _linearSampler.NonFiltering);
                DrawUtility(encoder, pipeline, depthGroup, 0, d.DepthView, d.HasStencil);
            }
        }

        Submit(encoder);
        if (group != 0)
        {
            _api.BindGroupRelease(group);
        }

        if (depthGroup != 0)
        {
            _api.BindGroupRelease(depthGroup);
        }

        ResumePass(suspended);
    }

    private void CopyTexture(nint encoder, WgpuTexture source, int sourceMip, int sourceLayer, WgpuTexture destination, int destinationMip, int destinationLayer, int width, int height)
    {
        var from = new WgpuTexelCopyTextureInfo { Texture = source.Handle, MipLevel = (uint)sourceMip, Origin = new WgpuOrigin3D { Z = (uint)sourceLayer }, Aspect = Wgpu.AspectAll };
        var to = new WgpuTexelCopyTextureInfo { Texture = destination.Handle, MipLevel = (uint)destinationMip, Origin = new WgpuOrigin3D { Z = (uint)destinationLayer }, Aspect = Wgpu.AspectAll };
        var extent = new WgpuExtent3D { Width = (uint)width, Height = (uint)height, DepthOrArrayLayers = 1 };
        _api.CommandEncoderCopyTextureToTexture(encoder, &from, &to, &extent);
    }

    // ---- Partial clear ----

    /// <summary>Clears the scissor rectangle of the open pass by drawing over it.</summary>
    private void DrawClear(WgpuFramebuffer target, bool clearColor, Vector4 color, bool clearDepth, float depth)
    {
        var count = target.Colors.Length;
        var wgsl = new StringBuilder("""
struct Clear {
    color: vec4f,
    depth: vec4f,
}
@group(0) @binding(0) var<uniform> clear: Clear;
@vertex fn vs(@builtin(vertex_index) index: u32) -> @builtin(position) vec4f {
    let p = vec2f(f32((index << 1u) & 2u), f32(index & 2u));
    return vec4f(p * 2.0 - 1.0, clear.depth.x, 1.0);
}

""");
        if (count == 0)
        {
            wgsl.Append("@fragment fn fs() {\n}\n");
        }
        else
        {
            wgsl.Append("struct Colors {\n");
            for (var i = 0; i < count; i++)
            {
                wgsl.Append("    @location(").Append(i).Append(") c").Append(i).Append(": vec4f,\n");
            }

            wgsl.Append("}\n@fragment fn fs() -> Colors {\n    var result: Colors;\n");
            for (var i = 0; i < count; i++)
            {
                wgsl.Append("    result.c").Append(i).Append(" = clear.color;\n");
            }

            wgsl.Append("    return result;\n}\n");
        }

        var name = "clear" + count.ToString(System.Globalization.CultureInfo.InvariantCulture);
        var pipeline = GetUtilityPipeline(name, wgsl.ToString(), UtilityLayout.Uniform, target.Formats, clearDepth && target.Depth is not null, clearColor ? Wgpu.ColorWriteAll : 0);
        if (_clearBuffer == 0)
        {
            var bufferDescriptor = new WgpuBufferDescriptor { Usage = Wgpu.BufferUsageUniform | Wgpu.BufferUsageCopyDst, Size = 32 };
            _clearBuffer = _api.DeviceCreateBuffer(_device, &bufferDescriptor);
        }

        // Written before the pass is sent; the next partial clear begins another pass, after this one went out.
        var data = stackalloc float[8] { color.X, color.Y, color.Z, color.W, Math.Clamp(depth, 0f, 1f), 0f, 0f, 0f };
        _api.QueueWriteBuffer(_queue, _clearBuffer, 0, data, 32);
        var group = TemporaryGroup(UtilityLayout.Uniform, 0, 0, _clearBuffer, 32);
        _api.RenderPassSetPipeline(_pass, pipeline);
        _api.RenderPassSetBindGroup(_pass, 0, group, 0, null);
        _api.RenderPassDraw(_pass, 3, 1, 0, 0);
        _api.BindGroupRelease(group);

        // The renderer's pipeline and bind groups have to be set again.
        _appliedPipeline = 0;
        _appliedUniforms = null;
        _appliedTextures = null;
    }

    // ---- Reading back ----

    /// <inheritdoc/>
    public override void ReadPixels(GpuFramebuffer? source, int x, int y, int width, int height, Span<byte> rgba)
    {
        if (rgba.Length < width * height * 4)
        {
            throw new ArgumentException("The destination is too small.", nameof(rgba));
        }

        var format = ReadColor(source, x, y, width, height, out var data, out var bytesPerRow);
        try
        {
            var stored = WgpuConversions.StoredBytesPerPixel(format);
            for (var row = 0; row < height; row++)
            {
                var from = data + ((long)row * bytesPerRow);
                var to = rgba.Slice(row * width * 4, width * 4);
                if (format is TextureFormat.RGBA8 or TextureFormat.RGBA8Srgb)
                {
                    new ReadOnlySpan<byte>(from, width * 4).CopyTo(to);
                    continue;
                }

                for (var i = 0; i < width; i++)
                {
                    var pixel = Decode(format, from + (i * stored));
                    to[i * 4] = ToByte(pixel.X);
                    to[(i * 4) + 1] = ToByte(pixel.Y);
                    to[(i * 4) + 2] = ToByte(pixel.Z);
                    to[(i * 4) + 3] = ToByte(pixel.W);
                }
            }
        }
        finally
        {
            _api.BufferUnmap(_readback);
        }

        static byte ToByte(float value) => (byte)((Math.Clamp(float.IsNaN(value) ? 0f : value, 0f, 1f) * 255f) + 0.5f);
    }

    /// <inheritdoc/>
    public override void ReadPixelsFloat(GpuFramebuffer? source, int x, int y, int width, int height, Span<float> rgba)
    {
        if (rgba.Length < width * height * 4)
        {
            throw new ArgumentException("The destination is too small.", nameof(rgba));
        }

        var format = ReadColor(source, x, y, width, height, out var data, out var bytesPerRow);
        try
        {
            var stored = WgpuConversions.StoredBytesPerPixel(format);
            for (var row = 0; row < height; row++)
            {
                var from = data + ((long)row * bytesPerRow);
                var to = rgba.Slice(row * width * 4, width * 4);
                if (format == TextureFormat.RGBA32F)
                {
                    new ReadOnlySpan<float>(from, width * 4).CopyTo(to);
                    continue;
                }

                for (var i = 0; i < width; i++)
                {
                    var pixel = Decode(format, from + (i * stored));
                    to[i * 4] = pixel.X;
                    to[(i * 4) + 1] = pixel.Y;
                    to[(i * 4) + 2] = pixel.Z;
                    to[(i * 4) + 3] = pixel.W;
                }
            }
        }
        finally
        {
            _api.BufferUnmap(_readback);
        }
    }

    private static Vector4 Decode(TextureFormat format, byte* p)
    {
        switch (format)
        {
            case TextureFormat.R8:
                return new Vector4(p[0] / 255f, 0f, 0f, 1f);
            case TextureFormat.RG8:
                return new Vector4(p[0] / 255f, p[1] / 255f, 0f, 1f);
            case TextureFormat.RGBA8:
            case TextureFormat.RGBA8Srgb:
                return new Vector4(p[0], p[1], p[2], p[3]) / 255f;
            case TextureFormat.R16F:
                return new Vector4(WgpuConversions.HalfToFloat(*(ushort*)p), 0f, 0f, 1f);
            case TextureFormat.RG16F:
                return new Vector4(WgpuConversions.HalfToFloat(*(ushort*)p), WgpuConversions.HalfToFloat(((ushort*)p)[1]), 0f, 1f);
            case TextureFormat.RGBA16F:
                var h = (ushort*)p;
                return new Vector4(WgpuConversions.HalfToFloat(h[0]), WgpuConversions.HalfToFloat(h[1]), WgpuConversions.HalfToFloat(h[2]), WgpuConversions.HalfToFloat(h[3]));
            case TextureFormat.R32F:
                return new Vector4(*(float*)p, 0f, 0f, 1f);
            case TextureFormat.RG32F:
                return new Vector4(*(float*)p, ((float*)p)[1], 0f, 1f);
            case TextureFormat.RGBA32F:
                var f = (float*)p;
                return new Vector4(f[0], f[1], f[2], f[3]);
            case TextureFormat.RG11B10F:
                WgpuConversions.UnpackRg11b10(*(uint*)p, out var r, out var g, out var b);
                return new Vector4(r, g, b, 1f);
            default:
                return default;
        }
    }

    /// <summary>Copies a rectangle of a framebuffer's first color attachment into the readback buffer and maps it.
    /// The caller unmaps <c>_readback</c>.</summary>
    private TextureFormat ReadColor(GpuFramebuffer? source, int x, int y, int width, int height, out byte* data, out int bytesPerRow)
    {
        var framebuffer = Target(source);
        if (framebuffer.Colors.Length == 0)
        {
            throw new InvalidOperationException("The framebuffer has no color attachment to read.");
        }

        var texture = framebuffer.Colors[0];
        var format = texture.Descriptor.Format;
        var stored = WgpuConversions.StoredBytesPerPixel(format);
        if (texture.Descriptor.Samples > 1 || stored == 0)
        {
            throw new NotSupportedException("Pixels are read from single-sampled, uncompressed color attachments; resolve a multisampled framebuffer first.");
        }

        Flush();

        // Rows of a texture-to-buffer copy start at multiples of 256 bytes.
        bytesPerRow = ((width * stored) + 255) & ~255;
        var size = (ulong)bytesPerRow * (ulong)height;
        EnsureReadback(size);
        var encoder = _api.DeviceCreateCommandEncoder(_device, null);
        var from = new WgpuTexelCopyTextureInfo
        {
            Texture = texture.Handle,
            MipLevel = (uint)framebuffer.ColorMips[0],
            Origin = new WgpuOrigin3D { X = (uint)x, Y = (uint)y, Z = (uint)framebuffer.ColorLayers[0] },
            Aspect = Wgpu.AspectAll,
        };
        var to = new WgpuTexelCopyBufferInfo
        {
            Layout = new WgpuTexelCopyBufferLayout { BytesPerRow = (uint)bytesPerRow, RowsPerImage = (uint)height },
            Buffer = _readback,
        };
        var extent = new WgpuExtent3D { Width = (uint)width, Height = (uint)height, DepthOrArrayLayers = 1 };
        _api.CommandEncoderCopyTextureToBuffer(encoder, &from, &to, &extent);
        Submit(encoder);
        data = MapReadback(size);
        return format;
    }

    private void EnsureReadback(ulong size)
    {
        if (_readback != 0 && _readbackSize >= size)
        {
            return;
        }

        if (_readback != 0)
        {
            _api.BufferRelease(_readback);
        }

        _readbackSize = Math.Max(size, 65536);
        var descriptor = new WgpuBufferDescriptor { Usage = Wgpu.BufferUsageMapRead | Wgpu.BufferUsageCopyDst, Size = _readbackSize };
        _readback = _api.DeviceCreateBuffer(_device, &descriptor);
    }

    private byte* MapReadback(ulong size)
    {
        var box = new CallbackBox();
        var handle = GCHandle.Alloc(box);
        try
        {
            _api.BufferMapAsync(_readback, Wgpu.MapModeRead, 0, (nuint)size, new WgpuCallbackInfo
            {
                Mode = Wgpu.CallbackModeAllowProcessEvents,
                Callback = (delegate* unmanaged[Cdecl]<uint, WgpuStringView, void*, void*, void>)&OnStatus,
                Userdata1 = (void*)GCHandle.ToIntPtr(handle),
            });

            // The map completes once the copy before it has run.
            for (var i = 0; i < 1000 && !box.Completed; i++)
            {
                _api.DevicePoll(_device, 1, null);
                if (!box.Completed)
                {
                    _api.InstanceProcessEvents(_instance);
                }
            }
        }
        finally
        {
            handle.Free();
        }

        if (!box.Completed || box.Status != Wgpu.MapAsyncSuccess)
        {
            throw new InvalidOperationException($"wgpu could not map the readback buffer: {box.Message}");
        }

        return (byte*)_api.BufferGetConstMappedRange(_readback, 0, (nuint)size);
    }

    private void DisposeTransfers()
    {
        foreach (var pipeline in _utilityPipelines.Values)
        {
            _api.RenderPipelineRelease(pipeline);
        }

        foreach (var module in _utilityModules.Values)
        {
            _api.ShaderModuleRelease(module);
        }

        foreach (var layout in _utilityLayouts.Values)
        {
            _api.PipelineLayoutRelease(layout);
        }

        if (_readback != 0)
        {
            _api.BufferRelease(_readback);
        }

        if (_clearBuffer != 0)
        {
            _api.BufferRelease(_clearBuffer);
        }
    }
}
