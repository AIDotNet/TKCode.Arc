using System.Numerics;
using System.Runtime.InteropServices;
using TKCode.Arc.Graphics3D.Rhi;
using TKCode.Arc.Graphics3D.Shading;

namespace TKCode.Arc.Graphics3D;

public sealed partial class Renderer
{
    private const int OffsetAmbient = 0;
    private const int OffsetFogColor = 4;
    private const int OffsetFogParams = 8;
    private const int OffsetEnvParams = 12;
    private const int OffsetSh = 16;
    private const int OffsetLightCounts = 52;
    private const int OffsetDirLights = 56;
    private const int OffsetPointLights = 88;
    private const int OffsetSpotLights = 280;
    private const int OffsetHemiLights = 408;
    private const int OffsetShadowMatrices = 432;
    private const int OffsetShadowParams = 688;
    private const int OffsetShadowRects = 752;
    private const int OffsetShadowAtlas = 816;

    private readonly ShadowView[] _shadowViews = CreateShadowViews();
    private int _shadowViewCount;
    private int _viewCount;
    private GpuTexture? _shadowAtlas;
    private GpuFramebuffer? _shadowFramebuffer;
    private int _shadowAtlasAllocated;

    private sealed class ShadowView
    {
        public readonly ViewPass Pass = new() { DepthOnly = true };
        public int X;
        public int Y;
        public int Size;
        public LightShadow Shadow = null!;
        public Matrix4x4 LightWorld;
        public Matrix4x4 Projection;
        public bool Orthographic;
        public float Near;
        public float Far;
    }

    private static ShadowView[] CreateShadowViews()
    {
        var views = new ShadowView[ShaderLibrary.MaxShadows];
        for (var i = 0; i < views.Length; i++)
        {
            views[i] = new ShadowView();
        }

        return views;
    }

    private GpuSampler ShadowSampler => GetSampler(new SamplerDescriptor
    {
        MagFilter = TextureFilter.Linear,
        MinFilter = TextureFilter.Linear,
        MipFilter = MipFilter.None,
        WrapU = TextureWrap.ClampToEdge,
        WrapV = TextureWrap.ClampToEdge,
        WrapW = TextureWrap.ClampToEdge,
        MaxAnisotropy = 1f,
        CompareEnabled = true,
        Compare = CompareFunction.LessEqual,
    });

    private static Matrix4x4 LookWorld(Vector3 position, Vector3 direction)
    {
        // A camera-style frame: local -Z along the direction.
        var z = -direction;
        var up = MathF.Abs(z.Y) > 0.999f ? Vector3.UnitZ : Vector3.UnitY;
        var x = Vector3.Normalize(Vector3.Cross(up, z));
        var y = Vector3.Cross(z, x);
        return new Matrix4x4(x.X, x.Y, x.Z, 0f, y.X, y.Y, y.Z, 0f, z.X, z.Y, z.Z, 0f, position.X, position.Y, position.Z, 1f);
    }

    private void SetupLights(Scene scene, Camera camera, EnvironmentBinding? environment)
    {
        var d = _sceneData;
        Array.Clear(d);
        _shadowViewCount = 0;
        var ambient = Vector3.Zero;
        int dirCount = 0, pointCount = 0, spotCount = 0, hemiCount = 0;
        var shadows = ShadowsEnabled;
        var lights = scene.Lights;
        for (var i = 0; i < lights.Count; i++)
        {
            var light = lights[i];
            if (light.Intensity == 0f || !light.IsEffectivelyVisible())
            {
                continue;
            }

            var color = light.Color.ToVector3() * light.Intensity;
            switch (light)
            {
                case AmbientLight:
                    ambient += color;
                    break;
                case HemisphereLight hemi when hemiCount < ShaderLibrary.MaxHemisphereLights:
                {
                    var o = OffsetHemiLights + (hemiCount * 12);
                    Put(d, o, color, 0f);
                    Put(d, o + 4, hemi.GroundColor.ToVector3() * hemi.Intensity, 0f);
                    var up = hemi.WorldPosition;
                    Put(d, o + 8, up.LengthSquared() > 1e-12f ? Vector3.Normalize(up) : Vector3.UnitY, 0f);
                    hemiCount++;
                    break;
                }

                case DirectionalLight dir when dirCount < ShaderLibrary.MaxDirectionalLights:
                {
                    var o = OffsetDirLights + (dirCount * 8);
                    var direction = dir.Direction;
                    var shadowIndex = -1f;
                    if (shadows && dir.CastShadow && _shadowViewCount < ShaderLibrary.MaxShadows)
                    {
                        shadowIndex = _shadowViewCount;
                        var view = _shadowViews[_shadowViewCount++];
                        var s = dir.Shadow;
                        view.Shadow = s;
                        view.Size = s.MapSize;
                        view.Orthographic = true;
                        if (s.AutoFit && TryGetCasterBounds(scene.World, out var bounds))
                        {
                            var radius = MathF.Max(bounds.Radius, 1e-3f);
                            view.Near = MathF.Max(s.Near, 0.01f);
                            view.Far = view.Near + (radius * 2f);
                            view.LightWorld = LookWorld(bounds.Center - (direction * (radius + view.Near)), direction);
                            view.Projection = Matrix4x4.CreateOrthographicOffCenter(-radius, radius, -radius, radius, view.Near, view.Far);
                        }
                        else
                        {
                            view.Near = s.Near;
                            view.Far = s.Far;
                            view.LightWorld = LookWorld(dir.WorldPosition, direction);
                            view.Projection = Matrix4x4.CreateOrthographicOffCenter(-s.OrthoSize, s.OrthoSize, -s.OrthoSize, s.OrthoSize, s.Near, s.Far);
                        }
                    }

                    Put(d, o, -direction, shadowIndex);
                    Put(d, o + 4, color, 0f);
                    dirCount++;
                    break;
                }

                case PointLight point when pointCount < ShaderLibrary.MaxPointLights:
                {
                    var o = OffsetPointLights + (pointCount * 12);
                    var position = point.WorldPosition;
                    var shadowIndex = -1f;
                    if (shadows && point.CastShadow && _shadowViewCount + 6 <= ShaderLibrary.MaxShadows)
                    {
                        shadowIndex = _shadowViewCount;
                        var s = point.Shadow;
                        var far = point.Distance > 0f ? point.Distance : s.Far;
                        var projection = Matrix4x4.CreatePerspectiveFieldOfView(MathUtil.ToRadians(92f), 1f, s.Near, far);
                        for (var face = 0; face < 6; face++)
                        {
                            var view = _shadowViews[_shadowViewCount++];
                            view.Shadow = s;
                            view.Size = s.MapSize;
                            view.Orthographic = false;
                            view.Near = s.Near;
                            view.Far = far;
                            view.LightWorld = LookWorld(position, CubeDirection(face));
                            view.Projection = projection;
                        }
                    }

                    Put(d, o, position, point.Distance);
                    Put(d, o + 4, color, point.Decay);
                    d[o + 8] = shadowIndex;
                    pointCount++;
                    break;
                }

                case SpotLight spot when spotCount < ShaderLibrary.MaxSpotLights:
                {
                    var o = OffsetSpotLights + (spotCount * 16);
                    var position = spot.WorldPosition;
                    var direction = spot.Direction;
                    var angle = Math.Clamp(spot.Angle, 0.001f, (MathF.PI / 2f) - 0.01f);
                    var shadowIndex = -1f;
                    if (shadows && spot.CastShadow && _shadowViewCount < ShaderLibrary.MaxShadows)
                    {
                        shadowIndex = _shadowViewCount;
                        var view = _shadowViews[_shadowViewCount++];
                        var s = spot.Shadow;
                        view.Shadow = s;
                        view.Size = s.MapSize;
                        view.Orthographic = false;
                        view.Near = s.Near;
                        view.Far = spot.Distance > 0f ? spot.Distance : s.Far;
                        view.LightWorld = LookWorld(position, direction);
                        view.Projection = Matrix4x4.CreatePerspectiveFieldOfView(MathF.Min(angle * 2.1f, MathUtil.ToRadians(170f)), 1f, view.Near, view.Far);
                    }

                    Put(d, o, position, spot.Distance);
                    Put(d, o + 4, -direction, spot.Decay);
                    Put(d, o + 8, color, MathF.Cos(angle));
                    d[o + 12] = MathF.Cos(angle * (1f - Math.Clamp(spot.Penumbra, 0f, 1f)));
                    d[o + 13] = shadowIndex;
                    spotCount++;
                    break;
                }
            }
        }

        Put(d, OffsetAmbient, ambient, Exposure);
        if (scene.Fog is { } fog)
        {
            Put(d, OffsetFogColor, fog.Color.ToVector3(), fog.Mode == FogMode.Linear ? 1f : 2f);
            d[OffsetFogParams] = fog.Near;
            d[OffsetFogParams + 1] = fog.Far;
            d[OffsetFogParams + 2] = fog.Density;
        }

        // The last mip level of the scene picture that transmissive surfaces look through.
        d[OffsetFogParams + 3] = _sceneColorMaxLod;

        if (environment is not null)
        {
            var (sin, cos) = MathF.SinCos(scene.EnvironmentRotation);
            d[OffsetEnvParams] = scene.EnvironmentIntensity;
            d[OffsetEnvParams + 1] = environment.MaxMip;
            d[OffsetEnvParams + 2] = sin;
            d[OffsetEnvParams + 3] = cos;
            for (var i = 0; i < 9; i++)
            {
                Put(d, OffsetSh + (i * 4), environment.Sh[i], 0f);
            }
        }

        var counts = MemoryMarshal.Cast<float, int>(d.AsSpan(OffsetLightCounts, 4));
        counts[0] = dirCount;
        counts[1] = pointCount;
        counts[2] = spotCount;
        counts[3] = hemiCount;
        if (_shadowViewCount > 0)
        {
            PlaceShadowViews(scene);
        }
    }

    private static Vector3 CubeDirection(int face) => face switch
    {
        0 => Vector3.UnitX,
        1 => -Vector3.UnitX,
        2 => Vector3.UnitY,
        3 => -Vector3.UnitY,
        4 => Vector3.UnitZ,
        _ => -Vector3.UnitZ,
    };

    private static void Put(float[] d, int offset, Vector3 v, float w)
    {
        d[offset] = v.X;
        d[offset + 1] = v.Y;
        d[offset + 2] = v.Z;
        d[offset + 3] = w;
    }

    private static bool TryGetCasterBounds(RenderWorld world, out BoundingSphere bounds)
    {
        var box = BoundingBox.Empty;
        const ProxyFlags Need = ProxyFlags.Alive | ProxyFlags.Visible | ProxyFlags.CastShadow;
        for (var i = 0; i < world.HighWater; i++)
        {
            if ((world.Flags[i] & Need) == Need)
            {
                var s = world.Spheres[i];
                if (!s.IsEmpty)
                {
                    box.Expand(s.Center - new Vector3(s.Radius));
                    box.Expand(s.Center + new Vector3(s.Radius));
                }
            }
        }

        bounds = BoundingSphere.FromBox(box);
        return !box.IsEmpty;
    }

    /// <summary>Gives every shadow view a square of the atlas, builds its draw list and writes the sampling data.</summary>
    private void PlaceShadowViews(Scene scene)
    {
        var atlas = Math.Clamp(ShadowAtlasSize, 256, Device.Capabilities.MaxTextureSize);
        var count = _shadowViewCount;
        Span<int> order = stackalloc int[count];
        for (var i = 0; i < count; i++)
        {
            order[i] = i;
            _shadowViews[i].Size = Math.Clamp(MathUtil.NextPowerOfTwo(_shadowViews[i].Size), 64, atlas);
        }

        // Largest first, on shelves; when the atlas overflows every region is halved and packing starts again.
        for (var attempt = 0; attempt < 8; attempt++)
        {
            for (var i = 1; i < count; i++)
            {
                var v = order[i];
                var j = i - 1;
                while (j >= 0 && _shadowViews[order[j]].Size < _shadowViews[v].Size)
                {
                    order[j + 1] = order[j];
                    j--;
                }

                order[j + 1] = v;
            }

            int x = 0, y = 0, shelf = 0;
            var fits = true;
            for (var i = 0; i < count; i++)
            {
                var view = _shadowViews[order[i]];
                if (x + view.Size > atlas)
                {
                    x = 0;
                    y += shelf;
                    shelf = 0;
                }

                if (y + view.Size > atlas)
                {
                    fits = false;
                    break;
                }

                view.X = x;
                view.Y = y;
                x += view.Size;
                shelf = Math.Max(shelf, view.Size);
            }

            if (fits)
            {
                break;
            }

            for (var i = 0; i < count; i++)
            {
                _shadowViews[i].Size = Math.Max(32, _shadowViews[i].Size / 2);
            }
        }

        _shadowAtlasAllocated = atlas;
        var d = _sceneData;
        var world = scene.World;
        var inv = 1f / atlas;
        for (var i = 0; i < count; i++)
        {
            var view = _shadowViews[i];
            var pass = view.Pass;
            pass.SetCamera(view.LightWorld, view.Projection, view.Orthographic, view.Near, view.Far);
            pass.ViewIndex = _viewCount++;
            pass.ViewportX = view.X;
            pass.ViewportY = view.Y;
            pass.ViewportWidth = view.Size;
            pass.ViewportHeight = view.Size;
            var visible = _culler.Cull(world, Frustum.FromViewProjection(pass.ViewProjection), uint.MaxValue, ProxyFlags.CastShadow, ref _visible);
            BuildLists(pass, scene, visible, shadowPass: true);

            // Clip space to the view's square of the atlas (xy) and to depth 0..1 (z, already canonical).
            float sw = view.Size * inv, sx = view.X * inv, sy = view.Y * inv;
            var toAtlas = new Matrix4x4(
                0.5f * sw, 0f, 0f, 0f,
                0f, 0.5f * sw, 0f, 0f,
                0f, 0f, 1f, 0f,
                (0.5f * sw) + sx, (0.5f * sw) + sy, 0f, 1f);
            var matrix = pass.ViewProjection * toAtlas;
            MemoryMarshal.CreateReadOnlySpan(ref matrix.M11, 16).CopyTo(d.AsSpan(OffsetShadowMatrices + (i * 16), 16));
            var s = view.Shadow;
            var o = OffsetShadowParams + (i * 4);
            d[o] = s.Bias;
            d[o + 1] = s.NormalBias;
            d[o + 2] = s.Radius;
            d[o + 3] = Math.Clamp(s.Intensity, 0f, 1f);
            o = OffsetShadowRects + (i * 4);
            d[o] = sx;
            d[o + 1] = sy;
            d[o + 2] = sx + sw;
            d[o + 3] = sy + sw;
        }

        d[OffsetShadowAtlas] = inv;
        d[OffsetShadowAtlas + 1] = inv;
    }

    private void RenderShadows(RenderWorld world)
    {
        if (_shadowViewCount == 0)
        {
            return;
        }

        var size = _shadowAtlasAllocated;
        if (_shadowAtlas is null || _shadowAtlas.Width != size)
        {
            _shadowFramebuffer?.Dispose();
            _shadowAtlas?.Dispose();
            var desc = TextureDescriptor.Texture2D(TextureFormat.Depth24, size, size);
            desc.Label = "shadowAtlas";
            _shadowAtlas = Device.CreateTexture(desc);
            _shadowFramebuffer = Device.CreateFramebuffer(new FramebufferDescriptor { Depth = _shadowAtlas, Label = "shadowAtlas" });
        }

        Device.BeginPass(new RenderPassDescriptor
        {
            Target = _shadowFramebuffer,
            ViewportWidth = size,
            ViewportHeight = size,
            ClearDepth = true,
            ClearDepthValue = 1f,
        });
        for (var i = 0; i < _shadowViewCount; i++)
        {
            var view = _shadowViews[i];
            var pass = view.Pass;
            if (pass.Opaque.Count == 0)
            {
                continue;
            }

            Device.SetViewport(view.X, view.Y, view.Size, view.Size);
            BindFrameResources(pass, null);
            DrawList(pass, pass.Opaque, pass.OpaqueBase, ShaderFeatures.None, world);
        }

        Device.EndPass();
    }
}
