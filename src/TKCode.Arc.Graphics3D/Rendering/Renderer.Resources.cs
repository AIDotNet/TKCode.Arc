using System.Buffers;
using System.Runtime.InteropServices;
using TKCode.Arc.Graphics3D.Rhi;
using TKCode.Arc.Graphics3D.Shading;

namespace TKCode.Arc.Graphics3D;

public sealed partial class Renderer
{
    private readonly Dictionary<string, ProgramEntry> _programs = new(StringComparer.Ordinal);
    private readonly Dictionary<SamplerDescriptor, GpuSampler> _samplers = [];
    private readonly HashSet<ResourceBinding> _bindings = new(ReferenceEqualityComparer.Instance);
    private readonly System.Collections.Concurrent.ConcurrentQueue<ResourceBinding> _destroyQueue = new();
    private GpuTexture? _whiteTexture;
    private GpuTexture? _blackCube;
    private GpuTexture? _blackTexture;
    private GpuTexture? _dummyShadow;
    private GpuVertexInput? _emptyInput;

    internal void QueueDestroy(ResourceBinding binding) => _destroyQueue.Enqueue(binding);

    private void ProcessDestroyQueue()
    {
        while (_destroyQueue.TryDequeue(out var binding))
        {
            if (_bindings.Remove(binding))
            {
                binding.Destroy();
            }
        }
    }

    private void Track(ResourceBinding binding) => _bindings.Add(binding);

    private sealed class ProgramEntry
    {
        public required GpuProgram Program;
        public required string Key;
        public int RefCount;
    }

    // ---- Samplers and default textures ----

    private GpuSampler GetSampler(in SamplerDescriptor descriptor)
    {
        if (!_samplers.TryGetValue(descriptor, out var sampler))
        {
            sampler = Device.CreateSampler(descriptor);
            _samplers.Add(descriptor, sampler);
        }

        return sampler;
    }

    private GpuTexture WhiteTexture
    {
        get
        {
            if (_whiteTexture is null)
            {
                _whiteTexture = Device.CreateTexture(TextureDescriptor.Texture2D(TextureFormat.RGBA8, 1, 1));
                Device.WriteTexture(_whiteTexture, 0, 0, 0, 0, 1, 1, [255, 255, 255, 255]);
            }

            return _whiteTexture;
        }
    }

    private GpuTexture BlackTexture
    {
        get
        {
            if (_blackTexture is null)
            {
                _blackTexture = Device.CreateTexture(TextureDescriptor.Texture2D(TextureFormat.RGBA8, 1, 1));
                Device.WriteTexture(_blackTexture, 0, 0, 0, 0, 1, 1, [0, 0, 0, 255]);
            }

            return _blackTexture;
        }
    }

    private GpuTexture BlackCube
    {
        get
        {
            if (_blackCube is null)
            {
                _blackCube = Device.CreateTexture(TextureDescriptor.Cube(TextureFormat.RGBA8, 1));
                for (var face = 0; face < 6; face++)
                {
                    Device.WriteTexture(_blackCube, 0, face, 0, 0, 1, 1, [0, 0, 0, 255]);
                }
            }

            return _blackCube;
        }
    }

    private GpuTexture DummyShadow => _dummyShadow ??= Device.CreateTexture(TextureDescriptor.Texture2D(TextureFormat.Depth24, 1, 1));

    private GpuVertexInput EmptyInput => _emptyInput ??= Device.CreateVertexInput(new VertexInputDescriptor { Attributes = [], Buffers = [] });

    // ---- Geometry ----

    private struct AttributeBuffer
    {
        public VertexAttribute Attribute;
        public GpuBuffer Buffer;
        public int Version;

        // The device cannot read the attribute's own format: it is uploaded as floats.
        public bool AsFloats;
    }

    /// <summary>An attribute's values as floats, with normalization applied: what a device is given when it cannot read
    /// the attribute's own format (<see cref="GraphicsDevice.SupportsVertexFormat"/>).</summary>
    private static float[] AttributeFloats(VertexAttribute attribute)
    {
        var size = attribute.ItemSize;
        var floats = new float[attribute.Count * size];
        for (var i = 0; i < floats.Length; i++)
        {
            floats[i] = attribute.Get(i / size, i % size);
        }

        return floats;
    }

    private static VertexFormat FloatFormat(int itemSize) => itemSize switch
    {
        1 => VertexFormat.Float1,
        2 => VertexFormat.Float2,
        3 => VertexFormat.Float3,
        _ => VertexFormat.Float4,
    };

    private sealed class GeometryBinding : ResourceBinding
    {
        public required BufferGeometry Geometry;
        public int StructureVersion = -1;
        public int IndexVersion;
        public int MorphVersion = -1;
        public readonly List<AttributeBuffer> Buffers = [];
        public GpuBuffer? Index;
        public GpuVertexInput? Input;
        public ShaderFeatures Features;
        public VertexAttributeBinding[] Layout = [];
        public GpuBuffer[] LayoutBuffers = [];
        public GpuTexture? MorphTexture;
        public int MorphCount;

        public void ReleaseBuffers()
        {
            foreach (var b in Buffers)
            {
                b.Buffer.Dispose();
            }

            Buffers.Clear();
            Index?.Dispose();
            Index = null;
            Input?.Dispose();
            Input = null;
        }

        public override void Destroy()
        {
            ReleaseBuffers();
            MorphTexture?.Dispose();
            MorphTexture = null;
        }
    }

    private static int AttributeLocation(string name) => name switch
    {
        BufferGeometry.Position => 0,
        BufferGeometry.Normal => 1,
        BufferGeometry.Tangent => 2,
        BufferGeometry.Uv => 3,
        BufferGeometry.Uv1 => 4,
        BufferGeometry.Color => 5,
        BufferGeometry.SkinIndex => 6,
        BufferGeometry.SkinWeight => 7,
        _ => -1,
    };

    private static ShaderFeatures AttributeFeature(int location) => location switch
    {
        1 => ShaderFeatures.HasNormal,
        2 => ShaderFeatures.HasTangent,
        3 => ShaderFeatures.HasUv0,
        4 => ShaderFeatures.HasUv1,
        5 => ShaderFeatures.HasColor,
        _ => ShaderFeatures.None,
    };

    private GeometryBinding EnsureGeometry(BufferGeometry geometry)
    {
        var b = ResourceBinding.Find<GeometryBinding>(geometry.Binding, this);
        if (b is null)
        {
            if (geometry.IsDisposed)
            {
                throw new ObjectDisposedException(nameof(BufferGeometry), $"Geometry '{geometry.Name}' was disposed but is still in the scene.");
            }

            b = new GeometryBinding { Geometry = geometry };
            ResourceBinding.Add(ref geometry.Binding, b, this, geometry);
            Track(b);
        }

        if (b.LastUsedFrame == _frame)
        {
            return b;
        }

        b.LastUsedFrame = _frame;
        if (b.StructureVersion != geometry.StructureVersion)
        {
            b.ReleaseBuffers();
            var layout = new List<VertexAttributeBinding>();
            var buffers = new List<GpuBuffer>();
            var features = ShaderFeatures.None;
            var hasJoints = false;
            var hasWeights = false;
            foreach (var (name, attribute) in geometry.Attributes)
            {
                var location = AttributeLocation(name);
                if (location < 0)
                {
                    continue;
                }

                var format = attribute.Format;
                var stride = attribute.ItemSize * attribute.ComponentSize;
                var asFloats = !Device.SupportsVertexFormat(format, stride);
                var bytes = attribute.AsBytes();
                if (asFloats)
                {
                    bytes = MemoryMarshal.AsBytes<float>(AttributeFloats(attribute));
                    format = FloatFormat(attribute.ItemSize);
                    stride = attribute.ItemSize * 4;
                }

                var buffer = Device.CreateBuffer(BufferUsage.Vertex, Math.Max(bytes.Length, 4), attribute.Dynamic, name);
                Device.WriteBuffer(buffer, 0, bytes);
                b.Buffers.Add(new AttributeBuffer { Attribute = attribute, Buffer = buffer, Version = attribute.Version, AsFloats = asFloats });
                layout.Add(new VertexAttributeBinding
                {
                    Location = location,
                    BufferIndex = buffers.Count,
                    Format = format,
                    Offset = 0,
                    Stride = stride,
                });
                buffers.Add(buffer);
                features |= AttributeFeature(location);
                hasJoints |= location == 6;
                hasWeights |= location == 7;
            }

            if (hasJoints && hasWeights)
            {
                features |= ShaderFeatures.Skinning;
            }

            if (geometry.Index is not null)
            {
                var bytes = geometry.IndexBytes;
                b.Index = Device.CreateBuffer(BufferUsage.Index, Math.Max(bytes.Length, 4), false, "index");
                Device.WriteBuffer(b.Index, 0, bytes);
            }

            b.Layout = [.. layout];
            b.LayoutBuffers = [.. buffers];
            b.Input = Device.CreateVertexInput(new VertexInputDescriptor
            {
                Attributes = b.Layout,
                Buffers = b.LayoutBuffers,
                IndexBuffer = b.Index,
                IndexFormat = geometry.IndexFormat,
            });
            b.Features = features;
            b.StructureVersion = geometry.StructureVersion;
            b.IndexVersion = geometry.IndexVersion;
        }
        else
        {
            for (var i = 0; i < b.Buffers.Count; i++)
            {
                var ab = b.Buffers[i];
                if (ab.Version != ab.Attribute.Version)
                {
                    Device.WriteBuffer(ab.Buffer, 0, ab.AsFloats ? MemoryMarshal.AsBytes<float>(AttributeFloats(ab.Attribute)) : ab.Attribute.AsBytes());
                    ab.Version = ab.Attribute.Version;
                    b.Buffers[i] = ab;
                }
            }

            if (b.Index is not null && b.IndexVersion != geometry.IndexVersion)
            {
                Device.WriteBuffer(b.Index, 0, geometry.IndexBytes);
                b.IndexVersion = geometry.IndexVersion;
            }
        }

        if (b.MorphVersion != geometry.MorphVersion)
        {
            b.MorphVersion = geometry.MorphVersion;
            b.MorphTexture?.Dispose();
            b.MorphTexture = null;
            b.MorphCount = geometry.MorphTargets.Count;
            b.Features &= ~(ShaderFeatures.Morph | ShaderFeatures.MorphNormals);
            if (b.MorphCount > 0)
            {
                BuildMorphTexture(b, geometry);
            }
        }

        return b;
    }

    private void BuildMorphTexture(GeometryBinding b, BufferGeometry geometry)
    {
        var vertexCount = geometry.VertexCount;
        var targets = geometry.MorphTargets;
        var normals = true;
        foreach (var t in targets)
        {
            normals &= t.NormalDeltas is not null;
        }

        var stride = normals ? 2 : 1;
        var texels = targets.Count * vertexCount * stride;
        const int Width = ShaderLibrary.MorphTextureWidth;
        var height = Math.Max(1, (texels + Width - 1) / Width);
        var data = new float[Width * height * 4];
        for (var t = 0; t < targets.Count; t++)
        {
            var p = targets[t].PositionDeltas;
            var n = targets[t].NormalDeltas;
            for (var i = 0; i < vertexCount; i++)
            {
                var o = ((t * vertexCount) + i) * stride * 4;
                data[o] = p[i * 3];
                data[o + 1] = p[(i * 3) + 1];
                data[o + 2] = p[(i * 3) + 2];
                if (normals)
                {
                    data[o + 4] = n![i * 3];
                    data[o + 5] = n[(i * 3) + 1];
                    data[o + 6] = n[(i * 3) + 2];
                }
            }
        }

        var desc = TextureDescriptor.Texture2D(TextureFormat.RGBA32F, Width, height);
        desc.Label = "morph";
        b.MorphTexture = Device.CreateTexture(desc);
        Device.WriteTexture(b.MorphTexture, 0, 0, 0, 0, Width, height, MemoryMarshal.AsBytes<float>(data));
        b.Features |= ShaderFeatures.Morph | (normals ? ShaderFeatures.MorphNormals : 0);
    }

    // ---- Instances ----

    private sealed class InstanceBinding : ResourceBinding
    {
        public GpuBuffer? Matrices;
        public GpuBuffer? Colors;
        public int MatricesVersion = -1;
        public int ColorsVersion = -1;
        public int Capacity;
        public GpuVertexInput? Input;
        public GeometryBinding? Geometry;
        public int GeometryStructure = -1;
        public bool HasColors;

        public override void Destroy()
        {
            Matrices?.Dispose();
            Colors?.Dispose();
            Input?.Dispose();
            Matrices = null;
            Colors = null;
            Input = null;
        }
    }

    private InstanceBinding EnsureInstances(InstancedMesh mesh, GeometryBinding geometry)
    {
        var b = ResourceBinding.Find<InstanceBinding>(mesh.InstanceBinding, this);
        if (b is null)
        {
            b = new InstanceBinding();
            ResourceBinding.Add(ref mesh.InstanceBinding, b, this, mesh);
            Track(b);
        }

        var rebuild = false;
        if (b.Matrices is null || b.Capacity != mesh.Matrices.Length)
        {
            b.Matrices?.Dispose();
            b.Capacity = mesh.Matrices.Length;
            b.Matrices = Device.CreateBuffer(BufferUsage.Vertex, b.Capacity * 64, true, "instanceMatrix");
            b.MatricesVersion = -1;
            rebuild = true;
        }

        if (b.MatricesVersion != mesh.MatricesVersion)
        {
            Device.WriteBuffer(b.Matrices, 0, MemoryMarshal.AsBytes(mesh.Matrices.AsSpan()));
            b.MatricesVersion = mesh.MatricesVersion;
        }

        if (mesh.Colors is not null)
        {
            if (b.Colors is null)
            {
                b.Colors = Device.CreateBuffer(BufferUsage.Vertex, b.Capacity * 12, true, "instanceColor");
                b.ColorsVersion = -1;
                rebuild = true;
            }

            if (b.ColorsVersion != mesh.ColorsVersion)
            {
                Device.WriteBuffer(b.Colors, 0, MemoryMarshal.AsBytes(mesh.Colors.AsSpan()));
                b.ColorsVersion = mesh.ColorsVersion;
            }
        }

        b.HasColors = b.Colors is not null;
        if (rebuild || b.Input is null || !ReferenceEquals(b.Geometry, geometry) || b.GeometryStructure != geometry.StructureVersion)
        {
            b.Input?.Dispose();
            var attributes = new List<VertexAttributeBinding>(geometry.Layout);
            var buffers = new List<GpuBuffer>(geometry.LayoutBuffers);
            var matrixIndex = buffers.Count;
            buffers.Add(b.Matrices);
            for (var column = 0; column < 4; column++)
            {
                attributes.Add(new VertexAttributeBinding
                {
                    Location = 8 + column,
                    BufferIndex = matrixIndex,
                    Format = VertexFormat.Float4,
                    Offset = column * 16,
                    Stride = 64,
                    PerInstance = true,
                });
            }

            if (b.Colors is not null)
            {
                attributes.Add(new VertexAttributeBinding
                {
                    Location = 12,
                    BufferIndex = buffers.Count,
                    Format = VertexFormat.Float3,
                    Offset = 0,
                    Stride = 12,
                    PerInstance = true,
                });
                buffers.Add(b.Colors);
            }

            b.Input = Device.CreateVertexInput(new VertexInputDescriptor
            {
                Attributes = [.. attributes],
                Buffers = [.. buffers],
                IndexBuffer = geometry.Index,
                IndexFormat = geometry.Geometry.IndexFormat,
            });
            b.Geometry = geometry;
            b.GeometryStructure = geometry.StructureVersion;
        }

        return b;
    }

    // ---- Textures ----

    private sealed class TextureBinding : ResourceBinding
    {
        public GpuTexture? Texture;
        public int Version = -1;
        public bool HasMips;

        public override void Destroy()
        {
            Texture?.Dispose();
            Texture = null;
        }
    }

    private GpuSampler SamplerFor(Texture t, bool hasMips) => GetSampler(new SamplerDescriptor
    {
        MagFilter = t.MagFilter,
        MinFilter = t.MinFilter,
        MipFilter = hasMips ? t.MipFilter : MipFilter.None,
        WrapU = t.WrapU,
        WrapV = t.WrapV,
        WrapW = TextureWrap.ClampToEdge,
        MaxAnisotropy = hasMips ? t.Anisotropy : 1f,
    });

    /// <summary>The GPU texture and sampler of a texture, uploading its pixels when they changed.</summary>
    private (GpuTexture Texture, GpuSampler Sampler) EnsureTexture(Texture t)
    {
        if (t.Owner is { } target)
        {
            var tb = EnsureTarget(target);
            var gpu = t.IsDepthAttachment ? tb.Depth : tb.Color;
            return (gpu ?? WhiteTexture, SamplerFor(t, false));
        }

        var b = ResourceBinding.Find<TextureBinding>(t.Binding, this);
        if (b is null)
        {
            if (t.IsDisposed)
            {
                throw new ObjectDisposedException(nameof(Texture), $"Texture '{t.Name}' was disposed but is still used by a material.");
            }

            b = new TextureBinding();
            ResourceBinding.Add(ref t.Binding, b, this, t);
            Track(b);
        }

        if (b.Version != t.Version || b.Texture is null)
        {
            Upload(t, b);
            b.Version = t.Version;
        }

        if (b.Texture is null)
        {
            return (t.Dimension == TextureDimension.Cube ? BlackCube : WhiteTexture, SamplerFor(t, false));
        }

        return (b.Texture, SamplerFor(t, b.HasMips));
    }

    private static TextureFormat GpuFormat(TextureImage image, TextureColorSpace colorSpace)
    {
        if (colorSpace != TextureColorSpace.Srgb)
        {
            return image.Format;
        }

        return image.Format switch
        {
            TextureFormat.RGBA8 => TextureFormat.RGBA8Srgb,
            TextureFormat.Bc1Rgba => TextureFormat.Bc1RgbaSrgb,
            TextureFormat.Bc3Rgba => TextureFormat.Bc3RgbaSrgb,
            TextureFormat.Bc7Rgba => TextureFormat.Bc7RgbaSrgb,
            TextureFormat.Etc2Rgb8 => TextureFormat.Etc2Rgb8Srgb,
            TextureFormat.Etc2Rgba8 => TextureFormat.Etc2Rgba8Srgb,
            TextureFormat.Astc4x4 => TextureFormat.Astc4x4Srgb,
            _ => image.Format,
        };
    }

    private void Upload(Texture t, TextureBinding b)
    {
        var cube = t as CubeTexture;
        var image = cube is not null ? cube.Faces[0] : t.Image;
        if (image is null)
        {
            b.Texture?.Dispose();
            b.Texture = null;
            return;
        }

        var format = GpuFormat(image, t.ColorSpace);
        var compressed = format.IsCompressed();
        var provided = image.Mipmaps?.Length ?? 0;
        var generate = !compressed && provided == 0 && t.GenerateMipmaps && t.MipFilter != MipFilter.None && (image.Width > 1 || image.Height > 1);
        var mips = generate ? TextureDescriptor.FullMipCount(image.Width, image.Height) : 1 + provided;
        var old = b.Texture;
        if (old is null || old.Width != image.Width || old.Height != image.Height || old.Descriptor.Format != format || old.Descriptor.MipLevels != mips)
        {
            old?.Dispose();
            var desc = cube is not null
                ? TextureDescriptor.Cube(format, image.Width, mips)
                : TextureDescriptor.Texture2D(format, image.Width, image.Height, mips);
            desc.Label = t.Name;
            b.Texture = Device.CreateTexture(desc);
        }

        var faces = cube is not null ? 6 : 1;
        for (var face = 0; face < faces; face++)
        {
            var img = cube is not null ? cube.Faces[face] : image;
            WriteImage(b.Texture!, 0, face, img, t.FlipY && cube is null && !compressed);
            for (var level = 0; level < (img.Mipmaps?.Length ?? 0) && level + 1 < mips; level++)
            {
                WriteImage(b.Texture!, level + 1, face, img.Mipmaps![level], t.FlipY && cube is null && !compressed);
            }
        }

        if (generate)
        {
            Device.GenerateMipmaps(b.Texture!);
        }

        b.HasMips = mips > 1;
    }

    private void WriteImage(GpuTexture texture, int level, int face, TextureImage image, bool flipY)
    {
        var bytes = image.AsBytes();
        if (!flipY || image.Height <= 1)
        {
            Device.WriteTexture(texture, level, face, 0, 0, image.Width, image.Height, bytes);
            return;
        }

        var rowBytes = bytes.Length / image.Height;
        var rented = ArrayPool<byte>.Shared.Rent(bytes.Length);
        try
        {
            for (var y = 0; y < image.Height; y++)
            {
                bytes.Slice(y * rowBytes, rowBytes).CopyTo(rented.AsSpan((image.Height - 1 - y) * rowBytes, rowBytes));
            }

            Device.WriteTexture(texture, level, face, 0, 0, image.Width, image.Height, rented.AsSpan(0, bytes.Length));
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(rented);
        }
    }

    // ---- Render targets ----

    private sealed class TargetBinding : ResourceBinding
    {
        public int Version = -1;
        public GpuTexture? Color;
        public GpuTexture? Depth;
        public GpuTexture? MsColor;
        public GpuTexture? MsDepth;
        public GpuFramebuffer? Framebuffer;
        public GpuFramebuffer? Resolve;

        public override void Destroy()
        {
            Framebuffer?.Dispose();
            Resolve?.Dispose();
            Color?.Dispose();
            Depth?.Dispose();
            MsColor?.Dispose();
            MsDepth?.Dispose();
            Framebuffer = null;
            Resolve = null;
            Color = null;
            Depth = null;
            MsColor = null;
            MsDepth = null;
        }
    }

    private TargetBinding EnsureTarget(RenderTarget target)
    {
        var b = ResourceBinding.Find<TargetBinding>(target.Binding, this);
        if (b is null)
        {
            b = new TargetBinding();
            ResourceBinding.Add(ref target.Binding, b, this, target);
            Track(b);
        }

        if (b.Version == target.Version && b.Framebuffer is not null)
        {
            return b;
        }

        b.Destroy();
        b.Version = target.Version;
        var samples = Math.Min(target.Samples, Math.Max(1, Device.Capabilities.MaxSamples));
        var wantDepth = target.DepthBuffer || target.DepthTexture is not null;
        if (!target.SurfaceProxy)
        {
            b.Color = Device.CreateTexture(TextureDescriptor.Texture2D(target.Format, target.Width, target.Height));
        }

        if (samples <= 1)
        {
            if (wantDepth)
            {
                b.Depth = Device.CreateTexture(TextureDescriptor.Texture2D(TextureFormat.Depth24, target.Width, target.Height));
            }

            b.Framebuffer = Device.CreateFramebuffer(new FramebufferDescriptor
            {
                Colors = [b.Color!],
                Depth = b.Depth is null ? (FramebufferAttachment?)null : b.Depth,
            });
            return b;
        }

        b.MsColor = Device.CreateTexture(TextureDescriptor.Texture2D(target.Format, target.Width, target.Height, 1, samples));
        if (wantDepth)
        {
            b.MsDepth = Device.CreateTexture(TextureDescriptor.Texture2D(TextureFormat.Depth24, target.Width, target.Height, 1, samples));
        }

        b.Framebuffer = Device.CreateFramebuffer(new FramebufferDescriptor
        {
            Colors = [b.MsColor],
            Depth = b.MsDepth is null ? (FramebufferAttachment?)null : b.MsDepth,
        });
        if (!target.SurfaceProxy)
        {
            if (target.DepthTexture is not null)
            {
                b.Depth = Device.CreateTexture(TextureDescriptor.Texture2D(TextureFormat.Depth24, target.Width, target.Height));
            }

            b.Resolve = Device.CreateFramebuffer(new FramebufferDescriptor
            {
                Colors = [b.Color!],
                Depth = b.Depth is null ? (FramebufferAttachment?)null : b.Depth,
            });
        }

        return b;
    }

    // ---- Materials and programs ----

    private sealed class Variant
    {
        public ShaderFeatures Features;
        public required GeneratedShader Shader;
        public required ProgramEntry Program;
        public GpuBuffer? Buffer;
        public float[] Staging = [];
        public long Stamp = -1;
        public int UpdatedFrame = -1;
    }

    private sealed class MaterialBinding : ResourceBinding
    {
        public int Version = -1;
        public readonly List<Variant> Variants = [];
        public Renderer Renderer = null!;

        public void Clear()
        {
            foreach (var v in Variants)
            {
                v.Buffer?.Dispose();
                Renderer.ReleaseProgram(v.Program);
            }

            Variants.Clear();
        }

        public override void Destroy() => Clear();
    }

    private void ReleaseProgram(ProgramEntry entry)
    {
        if (--entry.RefCount <= 0)
        {
            _programs.Remove(entry.Key);
            entry.Program.Dispose();
        }
    }

    private Variant GetVariant(Material material, ShaderFeatures features)
    {
        var b = ResourceBinding.Find<MaterialBinding>(material.Binding, this);
        if (b is null)
        {
            b = new MaterialBinding { Renderer = this };
            ResourceBinding.Add(ref material.Binding, b, this, material);
            Track(b);
        }

        if (b.Version != material.Version)
        {
            b.Clear();
            b.Version = material.Version;
        }

        var variants = b.Variants;
        for (var i = 0; i < variants.Count; i++)
        {
            if (variants[i].Features == features)
            {
                return variants[i];
            }
        }

        var shader = ShaderGenerator.Generate(material, features, Device.Capabilities.ShaderLanguage, _maxMaterialTextures);
        var key = string.Concat(shader.Vertex, "\u0001", shader.Fragment);
        if (!_programs.TryGetValue(key, out var entry))
        {
            var label = material.Name.Length > 0 ? material.Name : material.GetType().Name;
            entry = new ProgramEntry { Program = Device.CreateProgram(shader.ToDescriptor(label)), Key = key };
            _programs.Add(key, entry);
            _stats.ProgramsCompiled++;
        }

        entry.RefCount++;
        var variant = new Variant { Features = features, Shader = shader, Program = entry };
        if (shader.UniformFloats > 0)
        {
            variant.Staging = new float[shader.UniformFloats];
            variant.Buffer = Device.CreateBuffer(BufferUsage.Uniform, shader.UniformFloats * 4, true, "material");
        }

        variants.Add(variant);
        return variant;
    }

    /// <summary>Uploads the material's uniform values when any changed. Once per variant and frame.</summary>
    private void UpdateUniforms(Variant variant)
    {
        if (variant.UpdatedFrame == _frame)
        {
            return;
        }

        variant.UpdatedFrame = _frame;
        var uniforms = variant.Shader.Uniforms;
        if (uniforms.Length == 0)
        {
            return;
        }

        long stamp = 0;
        for (var i = 0; i < uniforms.Length; i++)
        {
            var u = uniforms[i];
            u.OnUpdate?.Invoke(u);
            stamp += u.Version;
        }

        if (stamp == variant.Stamp)
        {
            return;
        }

        variant.Stamp = stamp;
        var offsets = variant.Shader.UniformOffsets;
        for (var i = 0; i < uniforms.Length; i++)
        {
            uniforms[i].Write(variant.Staging.AsSpan(offsets[i]));
        }

        Device.WriteBuffer(variant.Buffer!, 0, MemoryMarshal.AsBytes<float>(variant.Staging));
    }
}
