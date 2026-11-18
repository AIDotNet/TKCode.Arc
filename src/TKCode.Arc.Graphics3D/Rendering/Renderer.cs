using System.Diagnostics;
using System.Numerics;
using System.Runtime.InteropServices;
using TKCode.Arc.Graphics3D.Rhi;
using TKCode.Arc.Graphics3D.Shading;

namespace TKCode.Arc.Graphics3D;

/// <summary>Draws a <see cref="Scene"/> from a <see cref="Camera"/> through a <see cref="GraphicsDevice"/>. One renderer
/// belongs to one device (one graphics context); scenes, geometries, materials and textures can be shared between
/// renderers. All methods must be called on the thread that owns the device, with its context current.</summary>
public sealed partial class Renderer : IDisposable
{
    private const int ViewFloats = 64;
    private const int SceneFloats = 820;
    private const int MaxViews = 1 + ShaderLibrary.MaxShadows;

    private readonly Stopwatch _clock = Stopwatch.StartNew();
    private readonly FrameData _frameData = new();
    private readonly ViewPass _mainPass = new();
    private readonly float[] _sceneData = new float[SceneFloats];
    private readonly float[] _viewData;
    private readonly int _viewStride;
    private readonly int _maxMaterialTextures;
    private GpuBuffer? _viewBuffer;
    private GpuBuffer? _sceneBuffer;
    private GpuTexture? _frameTexture;
    private int _frameTextureRows;
    private int[] _visible = new int[256];
    private readonly Culler _culler = new();
    private int _frame;
    private int _surfaceWidth;
    private int _surfaceHeight;
    private RenderTarget? _surfaceProxy;
    private RenderTarget? _hdrTarget;
    private RenderStatistics _stats;
    private bool _disposed;
    private double? _manualTime;

    /// <summary>Creates a renderer over a device.</summary>
    public Renderer(GraphicsDevice device)
    {
        Device = device ?? throw new ArgumentNullException(nameof(device));
        var align = Math.Max(1, device.Capabilities.UniformBufferOffsetAlignment);
        _viewStride = (((ViewFloats * 4) + align - 1) / align) * align;
        _viewData = new float[_viewStride / 4 * MaxViews];
        _maxMaterialTextures = Math.Max(0, device.Capabilities.MaxFragmentTextures - ShaderLibrary.FirstMaterialUnit);
    }

    /// <summary>The device.</summary>
    public GraphicsDevice Device { get; }

    /// <summary>Tone mapping applied when drawing to the screen or to a picture target.</summary>
    public ToneMapping ToneMapping { get; set; } = ToneMapping.None;

    /// <summary>Exposure multiplier applied before tone mapping.</summary>
    public float Exposure { get; set; } = 1f;

    /// <summary>The color the target is cleared to when the scene has no background (linear).</summary>
    public ColorF ClearColor { get; set; } = ColorF.Black;

    /// <summary>The alpha the target is cleared to; 0 lets the host's content show through.</summary>
    public float ClearAlpha { get; set; } = 1f;

    /// <summary>False keeps the target's contents: the scene is drawn over them.</summary>
    public bool AutoClear { get; set; } = true;

    /// <summary>False turns all shadows off.</summary>
    public bool ShadowsEnabled { get; set; } = true;

    /// <summary>Width and height of the shared shadow map, in texels.</summary>
    public int ShadowAtlasSize { get; set; } = 4096;

    /// <summary>Samples per pixel when drawing to the screen (multisample anti-aliasing); 1 turns it off.</summary>
    public int Samples { get; set; } = 4;

    /// <summary>Effects applied to the picture after the scene is drawn (bloom, vignette, color grading, FXAA). They
    /// take effect when drawing to the screen or to a picture target, on devices with float render targets.</summary>
    public PostProcessing PostProcessing { get; set; } = new();

    /// <summary>False draws objects in scene order instead of sorting them.</summary>
    public bool SortObjects { get; set; } = true;

    /// <summary>What the last render call did.</summary>
    public RenderStatistics Statistics => _stats;

    /// <summary>The time materials see as <see cref="Sl.Time"/>, in seconds. Setting it stops the built-in clock (for
    /// reproducible pictures); null starts it again.</summary>
    public double? Time
    {
        get => _manualTime;
        set => _manualTime = value;
    }

    /// <summary>Tells the renderer what "the screen" is: the host's framebuffer and its size in pixels. Call before
    /// rendering each frame to the screen.</summary>
    public void SetSurface(nint nativeFramebuffer, int width, int height)
    {
        _surfaceWidth = width;
        _surfaceHeight = height;
        Device.SetSurface(nativeFramebuffer, width, height);
    }

    private sealed class ViewPass
    {
        public Matrix4x4 View;
        public Matrix4x4 Projection;
        public Matrix4x4 ViewProjection;
        public Vector3 Position;
        public Vector3 Forward;
        public bool Orthographic;
        public float Near;
        public float Far;
        public int ViewportX;
        public int ViewportY;
        public int ViewportWidth;
        public int ViewportHeight;
        public readonly RenderList Opaque = new();
        public readonly RenderList Transparent = new();

        // Surfaces that show what is behind them (PhysicalMaterial.Transmission): drawn after the opaque ones, against
        // a picture of those.
        public readonly RenderList Transmissive = new();
        public int OpaqueBase;
        public int TransparentBase;
        public int TransmissiveBase;
        public int ViewIndex;
        public bool DepthOnly;
        public float DepthBiasSlope;
        public float DepthBiasConstant;

        public void SetCamera(in Matrix4x4 world, in Matrix4x4 projection, bool orthographic, float near, float far)
        {
            Matrix4x4.Invert(world, out View);
            Projection = projection;
            ViewProjection = View * projection;
            Position = world.Translation;
            Forward = Vector3.Normalize(new Vector3(-world.M31, -world.M32, -world.M33));
            Orthographic = orthographic;
            Near = near;
            Far = far;
        }
    }

    /// <summary>Draws the scene to the screen, or to a render target.</summary>
    public void Render(Scene scene, Camera camera, RenderTarget? target = null)
    {
        ArgumentNullException.ThrowIfNull(scene);
        ArgumentNullException.ThrowIfNull(camera);
        ObjectDisposedException.ThrowIf(_disposed, this);
        var started = Stopwatch.GetTimestamp();
        _frame++;
        var deviceBefore = Device.Statistics;
        _stats = default;
        ProcessDestroyQueue();

        // Where to draw.
        int width, height;
        TargetBinding? targetBinding = null;
        var display = target is null || target.ColorSpace == TextureColorSpace.Srgb;
        if (target is not null)
        {
            targetBinding = EnsureTarget(target);
            width = target.Width;
            height = target.Height;
        }
        else
        {
            width = _surfaceWidth;
            height = _surfaceHeight;
            if (width <= 0 || height <= 0)
            {
                return;
            }
        }

        // With post-processing the scene goes to a linear HDR target first; the effect chain then writes the picture.
        var post = display && PostProcessing.IsActive && Device.Capabilities.FloatRenderTargets;
        GpuFramebuffer? outputFramebuffer = null;
        TargetBinding? sceneBinding = targetBinding;
        var sceneDisplay = display;
        if (post)
        {
            outputFramebuffer = targetBinding is null ? null : targetBinding.Resolve ?? targetBinding.Framebuffer;
            var samples = target?.Samples ?? Samples;
            var needsDepth = PostProcessing.NeedsDepth;
            if (_hdrTarget is null || _hdrTarget.Samples != samples || (_hdrTarget.DepthTexture is not null) != needsDepth)
            {
                _hdrTarget?.Dispose();
                _hdrTarget = new RenderTarget(width, height, TextureFormat.RGBA16F, samples, depthTexture: needsDepth);
            }

            _hdrTarget.Resize(width, height);
            sceneBinding = EnsureTarget(_hdrTarget);
            sceneDisplay = false;
        }
        else if (target is null && Samples > 1 && Device.Capabilities.MaxSamples > 1)
        {
            if (_surfaceProxy is null || _surfaceProxy.Samples != Samples)
            {
                _surfaceProxy?.Dispose();
                _surfaceProxy = new RenderTarget(width, height, TextureFormat.RGBA8, Samples) { SurfaceProxy = true };
            }

            _surfaceProxy.Resize(width, height);
            sceneBinding = EnsureTarget(_surfaceProxy);
        }

        var world = scene.World;
        world.Sync();
        scene.AcknowledgeChanges();
        _stats.Objects = world.Count;

        // Main view: cull and build the draw lists.
        var main = _mainPass;
        main.SetCamera(camera.WorldMatrix, camera.ProjectionMatrix, camera.IsOrthographic, camera.Near, camera.Far);
        main.ViewportX = 0;
        main.ViewportY = 0;
        main.ViewportWidth = width;
        main.ViewportHeight = height;
        main.ViewIndex = 0;
        main.DepthOnly = false;
        var visibleCount = _culler.Cull(world, Frustum.FromViewProjection(main.ViewProjection), camera.Layers.Mask, ProxyFlags.None, ref _visible);
        _stats.Visible = visibleCount;
        BuildLists(main, scene, visibleCount, shadowPass: false);

        // Lights, shadow views, environment.
        _frameData.Reset();
        _viewCount = 1;
        var environment = EnsureEnvironment(scene);
        SetupLights(scene, camera, environment);
        var sceneFeatures = ShaderFeatures.None;
        if (scene.Fog is not null)
        {
            sceneFeatures |= ShaderFeatures.Fog;
        }

        if (_shadowViewCount > 0)
        {
            sceneFeatures |= ShaderFeatures.Shadows;
        }

        if (environment is not null)
        {
            sceneFeatures |= ShaderFeatures.Environment;
        }

        if (sceneDisplay)
        {
            sceneFeatures |= ShaderFeatures.SrgbOutput | (ShaderFeatures)((uint)ToneMapping << (int)ShaderFeatures.ToneMappingShift);
        }

        // One contiguous block of object records per pass, in draw order.
        for (var i = 0; i < _shadowViewCount; i++)
        {
            WriteRecords(_shadowViews[i].Pass, world);
        }

        WriteRecords(main, world);
        var outline = post && PostProcessing.Outline.Enabled && BuildOutlineList(scene, world);
        UploadFrameData();
        WriteView(main);
        for (var i = 0; i < _shadowViewCount; i++)
        {
            WriteView(_shadowViews[i].Pass);
        }

        _viewBuffer ??= Device.CreateBuffer(BufferUsage.Uniform, _viewData.Length * 4, true, "views");
        Device.WriteBuffer(_viewBuffer, 0, MemoryMarshal.AsBytes(_viewData.AsSpan(0, _viewStride / 4 * _viewCount)));
        _sceneBuffer ??= Device.CreateBuffer(BufferUsage.Uniform, SceneFloats * 4, true, "scene");
        Device.WriteBuffer(_sceneBuffer, 0, MemoryMarshal.AsBytes<float>(_sceneData));

        RenderShadows(world);
        var transmission = main.Transmissive.Count > 0 && Device.Capabilities.FloatRenderTargets;
        if (transmission)
        {
            RenderSceneColor(scene, main, environment, sceneFeatures, world, width, height);
        }

        // Main pass.
        var background = scene.Background ?? ClearColor;
        var clear = sceneDisplay ? background.ToSrgb() : background.ToVector3();
        var alpha = scene.Background is null ? ClearAlpha : 1f;
        Device.BeginPass(new RenderPassDescriptor
        {
            Target = sceneBinding?.Framebuffer,
            ViewportWidth = width,
            ViewportHeight = height,
            ClearColor = AutoClear || post,
            // With post-processing the background color is laid in behind the scene by the last pass, so that it
            // reaches the screen exactly as given instead of going through tone mapping.
            ClearColorValue = post ? Vector4.Zero : new Vector4(clear * alpha, alpha),
            ClearDepth = AutoClear || post,
            ClearDepthValue = 1f,
        });
        BindFrameResources(main, environment);
        DrawList(main, main.Opaque, main.OpaqueBase, sceneFeatures, world);
        DrawBackground(scene, main, environment, sceneDisplay);
        if (main.Transmissive.Count > 0)
        {
            if (transmission)
            {
                Device.SetTexture(ShaderLibrary.UnitSceneColor, _sceneColor!, GetSampler(SceneColorSampler));
            }

            DrawList(main, main.Transmissive, main.TransmissiveBase, transmission ? sceneFeatures | ShaderFeatures.SceneColor : sceneFeatures, world);
        }

        DrawList(main, main.Transparent, main.TransparentBase, sceneFeatures, world);
        Device.EndPass();

        if (post)
        {
            if (sceneBinding!.Resolve is not null)
            {
                Device.Blit(sceneBinding.Framebuffer, sceneBinding.Resolve, color: true, depth: sceneBinding.Depth is not null, linear: false);
            }

            if (outline)
            {
                RenderOutlineMask(main, environment, world, width, height);
            }

            RunPostProcessing(sceneBinding.Color!, sceneBinding.Depth, outline, main, outputFramebuffer, width, height, new Vector4(background.ToSrgb() * alpha, alpha));
            _outlineList.Clear();
        }
        else if (sceneBinding is not null)
        {
            if (target is null)
            {
                Device.Blit(sceneBinding.Framebuffer, null, color: true, depth: false, linear: false);
            }
            else if (sceneBinding.Resolve is not null)
            {
                Device.Blit(sceneBinding.Framebuffer, sceneBinding.Resolve, color: true, depth: target.DepthTexture is not null, linear: false);
            }
        }

        main.Opaque.Clear();
        main.Transparent.Clear();
        main.Transmissive.Clear();
        for (var i = 0; i < _shadowViewCount; i++)
        {
            _shadowViews[i].Pass.Opaque.Clear();
        }

        var after = Device.Statistics;
        _stats.DrawCalls = after.DrawCalls - deviceBefore.DrawCalls;
        _stats.Triangles = after.Triangles - deviceBefore.Triangles;
        _stats.UploadBytes = after.BufferUploadBytes - deviceBefore.BufferUploadBytes;
        _stats.ShadowViews = _shadowViewCount;

        // With a render thread the frame is only recorded so far: hand it over.
        (Device as ThreadedGraphicsDevice)?.Submit();
        _stats.CpuMilliseconds = Stopwatch.GetElapsedTime(started).TotalMilliseconds;
    }

    // ---- Lists ----

    private void BuildLists(ViewPass pass, Scene scene, int visibleCount, bool shadowPass)
    {
        var world = scene.World;
        var overrideMaterial = shadowPass ? null : scene.OverrideMaterial;
        var position = pass.Position;
        var forward = pass.Forward;
        var invFar = 1f / MathF.Max(pass.Far, 1e-6f);
        for (var n = 0; n < visibleCount; n++)
        {
            var proxy = _visible[n];
            var obj = world.Objects[proxy]!;
            var geometry = obj.Geometry;
            var elements = geometry.ElementCount;
            if (elements == 0)
            {
                continue;
            }

            var depth = Vector3.Dot(world.Spheres[proxy].Center - position, forward);
            var start = Math.Clamp(geometry.DrawStart, 0, elements);
            var count = geometry.DrawCount < 0 ? elements - start : Math.Min(geometry.DrawCount, elements - start);
            var groups = geometry.Groups;
            var materials = obj.Materials;
            if (groups.Count == 0)
            {
                AddItem(pass, obj, geometry, overrideMaterial ?? materials[0], start, count, proxy, depth, invFar, shadowPass);
                continue;
            }

            // Neighbouring groups that end up with the same material (a box with one material, an override material)
            // are drawn as one range.
            Material? runMaterial = null;
            int runStart = 0, runEnd = 0;
            for (var g = 0; g < groups.Count; g++)
            {
                var group = groups[g];
                var s = Math.Max(group.Start, start);
                var e = Math.Min(group.Start + group.Count, start + count);
                if (e <= s)
                {
                    continue;
                }

                var material = overrideMaterial ?? materials[Math.Min(group.MaterialIndex, materials.Length - 1)];
                if (ReferenceEquals(material, runMaterial) && s == runEnd)
                {
                    runEnd = e;
                    continue;
                }

                if (runMaterial is not null)
                {
                    AddItem(pass, obj, geometry, runMaterial, runStart, runEnd - runStart, proxy, depth, invFar, shadowPass);
                }

                runMaterial = material;
                runStart = s;
                runEnd = e;
            }

            if (runMaterial is not null)
            {
                AddItem(pass, obj, geometry, runMaterial, runStart, runEnd - runStart, proxy, depth, invFar, shadowPass);
            }
        }

        if (SortObjects)
        {
            pass.Opaque.Sort();
            pass.Transparent.Sort();
            pass.Transmissive.Sort();
        }
    }

    private static void AddItem(
        ViewPass pass, Renderable obj, BufferGeometry geometry, Material material, int start, int count, int proxy, float depth, float invFar, bool shadowPass)
    {
        if (!material.Visible || count <= 0)
        {
            return;
        }

        var item = new RenderItem { Object = obj, Geometry = geometry, Material = material, Start = start, Count = count, Proxy = proxy };
        if (material.Transparent && !shadowPass)
        {
            pass.Transparent.Add(item, RenderList.TransparentKey(obj.RenderOrder, depth, obj.Id));
        }
        else if (!shadowPass && material is PhysicalMaterial { HasTransmission: true })
        {
            pass.Transmissive.Add(item, RenderList.TransparentKey(obj.RenderOrder, depth, obj.Id));
        }
        else if (!shadowPass || !material.Transparent || material.AlphaTest > 0f)
        {
            pass.Opaque.Add(item, RenderList.OpaqueKey(obj.RenderOrder, material.Id, geometry.Id, depth * invFar));
        }
    }

    private void WriteRecords(ViewPass pass, RenderWorld world)
    {
        pass.OpaqueBase = WriteRecords(pass.Opaque, world);
        pass.TransparentBase = WriteRecords(pass.Transparent, world);
        pass.TransmissiveBase = WriteRecords(pass.Transmissive, world);
    }

    private int WriteRecords(RenderList list, RenderWorld world)
    {
        if (list.Count == 0)
        {
            return 0;
        }

        // Aux data of the list's objects first, so the records themselves stay contiguous.
        for (var i = 0; i < list.Count; i++)
        {
            if (list.Items[i].Object is Mesh mesh)
            {
                PrepareAux(mesh);
            }
        }

        // The vertex stage addresses records by index (four texels each), so the block starts on a multiple of four.
        _frameData.Allocate((4 - (_frameData.Texels & 3)) & 3);
        var first = _frameData.Allocate(list.Count * 4);
        for (var i = 0; i < list.Count; i++)
        {
            ref var item = ref list.Items[i];
            var texel = first + (i * 4);
            _frameData.WriteAffine(texel, world.Matrices[item.Proxy]);
            var receive = (world.Flags[item.Proxy] & ProxyFlags.ReceiveShadow) != 0 ? 1f : 0f;
            if (item.Object is Mesh mesh && mesh.AuxFrame == _frame && ReferenceEquals(mesh.AuxOwner, this))
            {
                _frameData.Write(texel + 3, mesh.BoneBase, mesh.MorphBase, receive, 0f);
            }
            else
            {
                _frameData.Write(texel + 3, 0f, 0f, receive, 0f);
            }
        }

        return first / 4;
    }

    /// <summary>Writes a mesh's bone matrices and morph weights into the frame data, once per frame.</summary>
    private void PrepareAux(Mesh mesh)
    {
        var skinned = mesh as SkinnedMesh;
        var hasSkin = skinned?.Skeleton is not null;
        var morphCount = mesh.Geometry.MorphTargets.Count;
        if ((!hasSkin && morphCount == 0) || (mesh.AuxFrame == _frame && ReferenceEquals(mesh.AuxOwner, this)))
        {
            return;
        }

        mesh.AuxOwner = this;
        mesh.AuxFrame = _frame;
        mesh.BoneBase = 0;
        mesh.MorphBase = 0;
        if (hasSkin)
        {
            var skeleton = skinned!.Skeleton!;
            var bones = skeleton.Bones;
            var texel = _frameData.Allocate(bones.Length * 3);
            mesh.BoneBase = texel;
            var bind = skinned.BindMatrix;
            for (var i = 0; i < bones.Length; i++)
            {
                _frameData.WriteAffine(texel + (i * 3), bind * skeleton.InverseBindMatrices[i] * bones[i].WorldMatrix);
            }
        }

        if (morphCount > 0)
        {
            var texel = _frameData.Allocate(morphCount);
            mesh.MorphBase = texel;
            var weights = mesh.MorphWeights;
            for (var i = 0; i < morphCount; i++)
            {
                _frameData.Write(texel + i, weights is not null && i < weights.Length ? weights[i] : 0f, 0f, 0f, 0f);
            }
        }
    }

    private void UploadFrameData()
    {
        var rows = Math.Max(1, _frameData.Rows);
        if (_frameTexture is null || _frameTextureRows < rows)
        {
            _frameTexture?.Dispose();
            _frameTextureRows = MathUtil.NextPowerOfTwo(Math.Max(rows, 16));
            var desc = TextureDescriptor.Texture2D(TextureFormat.RGBA32F, FrameData.Width, _frameTextureRows);
            desc.Label = "frameData";
            _frameTexture = Device.CreateTexture(desc);
        }

        if (_frameData.Texels > 0)
        {
            Device.WriteTexture(_frameTexture, 0, 0, 0, 0, FrameData.Width, _frameData.Rows, _frameData.RowBytes);
        }
    }

    private void WriteView(ViewPass pass)
    {
        var d = _viewData.AsSpan(pass.ViewIndex * (_viewStride / 4), ViewFloats);
        var projection = pass.Projection;
        if (Device.Capabilities.ClipDepth == ClipDepthRange.NegativeOneToOne)
        {
            // Canonical clip depth is 0..1; OpenGL wants -1..1: z' = 2z - w.
            var fix = Matrix4x4.Identity;
            fix.M33 = 2f;
            fix.M43 = -1f;
            projection *= fix;
        }

        var viewProjection = pass.View * projection;
        MemoryMarshal.CreateReadOnlySpan(ref viewProjection.M11, 16).CopyTo(d);
        MemoryMarshal.CreateReadOnlySpan(ref pass.View.M11, 16).CopyTo(d[16..]);
        MemoryMarshal.CreateReadOnlySpan(ref projection.M11, 16).CopyTo(d[32..]);
        var time = (float)(_manualTime ?? _clock.Elapsed.TotalSeconds);
        d[48] = pass.Position.X;
        d[49] = pass.Position.Y;
        d[50] = pass.Position.Z;
        d[51] = time;
        d[52] = pass.ViewportWidth;
        d[53] = pass.ViewportHeight;
        d[54] = 1f / Math.Max(1, pass.ViewportWidth);
        d[55] = 1f / Math.Max(1, pass.ViewportHeight);
        d[56] = pass.Forward.X;
        d[57] = pass.Forward.Y;
        d[58] = pass.Forward.Z;
        d[59] = pass.Orthographic ? 1f : 0f;
        d[60] = pass.Near;
        d[61] = pass.Far;
        d[62] = pass.Projection.M22;
        d[63] = 0f;
    }

    private void BindFrameResources(ViewPass pass, EnvironmentBinding? environment)
    {
        Device.SetUniformBuffer(ShaderLibrary.SlotView, _viewBuffer!, pass.ViewIndex * _viewStride, ViewFloats * 4);
        Device.SetUniformBuffer(ShaderLibrary.SlotScene, _sceneBuffer!, 0, SceneFloats * 4);
        Device.SetTexture(ShaderLibrary.UnitFrameData, _frameTexture!, GetSampler(SamplerDescriptor.NearestClamp));
        if (!pass.DepthOnly)
        {
            Device.SetTexture(ShaderLibrary.UnitShadowMap, _shadowAtlas ?? DummyShadow, ShadowSampler);
            Device.SetTexture(ShaderLibrary.UnitEnvironment, environment?.Specular ?? BlackCube, GetSampler(EnvironmentSampler));
        }
    }

    // ---- Drawing ----

    private void DrawList(ViewPass pass, RenderList list, int recordBase, ShaderFeatures sceneFeatures, RenderWorld world)
    {
        var items = list.Items;
        var count = list.Count;
        var i = 0;
        while (i < count)
        {
            ref var item = ref items[i];
            var run = 1;
            var instanced = item.Object as InstancedMesh;
            if (instanced is null)
            {
                var mirrored = world.Flags[item.Proxy] & ProxyFlags.Mirrored;
                while (i + run < count)
                {
                    ref var next = ref items[i + run];
                    if (!ReferenceEquals(next.Geometry, item.Geometry) || !ReferenceEquals(next.Material, item.Material)
                        || next.Start != item.Start || next.Count != item.Count || next.Object is InstancedMesh
                        || next.Object.Topology != item.Object.Topology || (world.Flags[next.Proxy] & ProxyFlags.Mirrored) != mirrored
                        || (next.Object is SkinnedMesh { Skeleton: not null }) != (item.Object is SkinnedMesh { Skeleton: not null }))
                    {
                        break;
                    }

                    run++;
                }
            }

            DrawBatch(pass, in item, recordBase + i, run, instanced, sceneFeatures, world);
            _stats.MergedDraws += run - 1;
            i += run;
        }
    }

    private void DrawBatch(
        ViewPass pass, in RenderItem item, int record, int run, InstancedMesh? instanced, ShaderFeatures sceneFeatures, RenderWorld world)
    {
        var geometry = EnsureGeometry(item.Geometry);
        if (geometry.Input is null)
        {
            return;
        }

        var material = item.Material;
        var features = geometry.Features | sceneFeatures;
        if (item.Object is not SkinnedMesh { Skeleton: not null })
        {
            features &= ~ShaderFeatures.Skinning;
        }

        var input = geometry.Input;
        var instances = run;
        var stride = 1;
        if (instanced is not null)
        {
            if (instanced.Count == 0)
            {
                return;
            }

            var ib = EnsureInstances(instanced, geometry);
            input = ib.Input!;
            instances = instanced.Count;
            stride = 0;
            features |= ShaderFeatures.Instancing;
            if (ib.HasColors)
            {
                features |= ShaderFeatures.InstanceColor;
            }
        }

        if (pass.DepthOnly)
        {
            features = (features & ~(ShaderFeatures.Fog | ShaderFeatures.Shadows | ShaderFeatures.Environment | ShaderFeatures.SrgbOutput
                | ShaderFeatures.ToneMappingMask | ShaderFeatures.InstanceColor)) | ShaderFeatures.DepthPass;
        }
        else if (!material.Transparent)
        {
            features |= ShaderFeatures.OpaqueOutput;
        }

        var variant = GetVariant(material, features);
        if (!variant.Program.Program.IsReady)
        {
            return;
        }

        UpdateUniforms(variant);
        Device.SetProgram(variant.Program.Program);
        var state = material.GetRenderState((world.Flags[item.Proxy] & ProxyFlags.Mirrored) != 0, pass.DepthOnly);
        if (pass.DepthOnly)
        {
            state.DepthBiasSlope = pass.DepthBiasSlope;
            state.DepthBiasConstant = pass.DepthBiasConstant;
        }

        Device.SetRenderState(state);
        if (variant.Buffer is not null)
        {
            Device.SetUniformBuffer(ShaderLibrary.SlotMaterial, variant.Buffer, 0, variant.Shader.UniformFloats * 4);
        }

        var textures = variant.Shader.Textures;
        for (var t = 0; t < textures.Length; t++)
        {
            var slot = textures[t];
            if (slot.Texture is { } texture)
            {
                var (gpu, sampler) = EnsureTexture(texture);
                Device.SetTexture(ShaderLibrary.FirstMaterialUnit + t, gpu, sampler);
            }
            else
            {
                Device.SetTexture(ShaderLibrary.FirstMaterialUnit + t, slot.IsCube ? BlackCube : WhiteTexture, GetSampler(SamplerDescriptor.LinearClamp));
            }
        }

        if (variant.Shader.UsesMorph && geometry.MorphTexture is not null)
        {
            Device.SetTexture(ShaderLibrary.UnitMorph, geometry.MorphTexture, GetSampler(SamplerDescriptor.NearestClamp));
        }

        Device.SetVertexInput(input);
        Device.SetDrawConstants(record, stride, geometry.MorphCount, item.Geometry.VertexCount);
        var topology = item.Object.Topology;
        if (item.Geometry.Index is not null)
        {
            Device.DrawIndexed(topology, item.Start, item.Count, instances);
        }
        else
        {
            Device.Draw(topology, item.Start, item.Count, instances);
        }
    }

    /// <summary>Clears the screen to <see cref="ClearColor"/> and <see cref="ClearAlpha"/> without drawing a scene.</summary>
    public void Clear()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_surfaceWidth <= 0 || _surfaceHeight <= 0)
        {
            return;
        }

        var color = ClearColor.ToSrgb() * ClearAlpha;
        Device.BeginPass(new RenderPassDescriptor
        {
            ViewportWidth = _surfaceWidth,
            ViewportHeight = _surfaceHeight,
            ClearColor = true,
            ClearColorValue = new Vector4(color, ClearAlpha),
            ClearDepth = true,
            ClearDepthValue = 1f,
        });
        Device.EndPass();
    }

    /// <summary>Reads the screen back as an RGBA8 image, rows top to bottom (after <see cref="Render"/>; for screenshots).</summary>
    public TextureImage ReadSurface()
    {
        var width = _surfaceWidth;
        var height = _surfaceHeight;
        var bottomUp = new byte[Math.Max(0, width * height * 4)];
        if (bottomUp.Length == 0)
        {
            return new TextureImage(0, 0, bottomUp);
        }

        Device.ReadPixels(null, 0, 0, width, height, bottomUp);
        var pixels = new byte[bottomUp.Length];
        var row = width * 4;
        for (var y = 0; y < height; y++)
        {
            bottomUp.AsSpan((height - 1 - y) * row, row).CopyTo(pixels.AsSpan(y * row, row));
        }

        return new TextureImage(width, height, pixels);
    }

    /// <summary>Reads the color of a render target back as an RGBA8 image, rows top to bottom. Slow: it waits for the
    /// GPU. For screenshots and tests.</summary>
    public TextureImage ReadPixels(RenderTarget target)
    {
        ArgumentNullException.ThrowIfNull(target);
        var binding = EnsureTarget(target);
        var source = binding.Resolve ?? binding.Framebuffer;
        var width = target.Width;
        var height = target.Height;
        var bottomUp = new byte[width * height * 4];
        Device.ReadPixels(source, 0, 0, width, height, bottomUp);
        var pixels = new byte[bottomUp.Length];
        var row = width * 4;
        for (var y = 0; y < height; y++)
        {
            bottomUp.AsSpan((height - 1 - y) * row, row).CopyTo(pixels.AsSpan(y * row, row));
        }

        return new TextureImage(width, height, pixels);
    }

    /// <summary>Frees every GPU object the renderer created. The device itself is not disposed.</summary>
    public void Dispose() => Dispose(releaseGpuObjects: true);

    /// <summary>Forgets every GPU object without touching the device: for a graphics context that was lost, where the
    /// objects are already gone and no call may be made. The scene resources can then be drawn by a new renderer.</summary>
    public void Abandon() => Dispose(releaseGpuObjects: false);

    private void Dispose(bool releaseGpuObjects)
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        while (_destroyQueue.TryDequeue(out _))
        {
        }

        foreach (var binding in _bindings)
        {
            binding.Detach();
            if (releaseGpuObjects)
            {
                binding.Destroy();
            }
        }

        _bindings.Clear();
        _surfaceProxy = null;
        _hdrTarget = null;
        DropPostChain(releaseGpuObjects);
        if (releaseGpuObjects)
        {
            DisposeCustomPasses();
        }
        else
        {
            _postPasses.Clear();
        }
        if (!releaseGpuObjects)
        {
            DropSceneColor(release: false);
        }

        if (!releaseGpuObjects)
        {
            _programs.Clear();
            _samplers.Clear();
            _effects.Clear();
            return;
        }

        foreach (var entry in _programs.Values)
        {
            entry.Program.Dispose();
        }

        _programs.Clear();
        foreach (var sampler in _samplers.Values)
        {
            sampler.Dispose();
        }

        _samplers.Clear();
        _viewBuffer?.Dispose();
        _sceneBuffer?.Dispose();
        _frameTexture?.Dispose();
        _whiteTexture?.Dispose();
        _blackCube?.Dispose();
        _blackTexture?.Dispose();
        _dummyShadow?.Dispose();
        _emptyInput?.Dispose();
        _shadowAtlas?.Dispose();
        _shadowFramebuffer?.Dispose();
        DropSceneColor(release: true);
        DisposeEffects();
    }
}
