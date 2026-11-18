using System.Numerics;
using TKCode.Arc.Graphics3D.Rhi;
using TKCode.Arc.Graphics3D.Shading;

namespace TKCode.Arc.Graphics3D;

public sealed partial class Renderer
{
    private const int EnvironmentSourceSize = 512;
    private const int EnvironmentSize = 256;
    private const int EnvironmentMinSize = 4;

    /// <summary>What image-based lighting needs from an environment texture: the cube prefiltered per roughness
    /// (specular) and nine spherical-harmonics coefficients (diffuse).</summary>
    private sealed class EnvironmentBinding : ResourceBinding
    {
        public GpuTexture? Specular;
        public float MaxMip;
        public readonly Vector3[] Sh = new Vector3[9];
        public int Version = -1;

        public override void Destroy()
        {
            Specular?.Dispose();
            Specular = null;
        }
    }

    private static SamplerDescriptor EnvironmentSampler => new()
    {
        MagFilter = TextureFilter.Linear,
        MinFilter = TextureFilter.Linear,
        MipFilter = MipFilter.Linear,
        WrapU = TextureWrap.ClampToEdge,
        WrapV = TextureWrap.ClampToEdge,
        WrapW = TextureWrap.ClampToEdge,
        MaxAnisotropy = 1f,
    };

    private EnvironmentBinding? EnsureEnvironment(Scene scene)
    {
        var texture = scene.Environment;
        if (texture is null)
        {
            return null;
        }

        var b = ResourceBinding.Find<EnvironmentBinding>(texture.Binding, this);
        if (b is null)
        {
            b = new EnvironmentBinding();
            ResourceBinding.Add(ref texture.Binding, b, this, texture);
            Track(b);
        }

        if (b.Version != texture.Version || b.Specular is null)
        {
            BuildEnvironment(texture, b);
            b.Version = texture.Version;
        }

        return b.Specular is null ? null : b;
    }

    private void BuildEnvironment(Texture texture, EnvironmentBinding b)
    {
        b.Specular?.Dispose();
        b.Specular = null;
        if (texture.Dimension != TextureDimension.Cube && texture.Image is null && texture.Owner is null)
        {
            return;
        }

        var (source, sourceSampler) = EnsureTexture(texture);
        var format = Device.Capabilities.FloatRenderTargets ? TextureFormat.RGBA16F : TextureFormat.RGBA8;
        Device.PushDebugGroup("environment");

        // 1. The source as a cube with mips, whatever it was (a panorama, a cube without mips).
        var sourceSize = Math.Min(EnvironmentSourceSize, Device.Capabilities.MaxCubeTextureSize);
        var sourceMips = TextureDescriptor.FullMipCount(sourceSize, sourceSize);
        var cube = Device.CreateTexture(TextureDescriptor.Cube(format, sourceSize, sourceMips));
        var isCube = texture.Dimension == TextureDimension.Cube;
        var toCube = isCube
            ? GetEffect("env-copy-cube", () => CubeDirectionGlsl + """
uniform samplerCube u_src;
void main() {
    o_color = vec4(min(textureLod(u_src, arcCubeDir(int(u_p[0].x), v_uv), 0.0).rgb, vec3(64000.0)), 1.0);
}
""", "u_src")
            : GetEffect("env-equirect-cube", () => CubeDirectionGlsl + EquirectUvGlsl + """
uniform sampler2D u_src;
void main() {
    vec3 d = normalize(arcCubeDir(int(u_p[0].x), v_uv));
    o_color = vec4(min(textureLod(u_src, arcEquirectUv(d), 0.0).rgb, vec3(64000.0)), 1.0);
}
""", "u_src");
        Span<Vector4> p = stackalloc Vector4[1];
        for (var face = 0; face < 6; face++)
        {
            using var fb = Device.CreateFramebuffer(new FramebufferDescriptor { Colors = [new FramebufferAttachment { Texture = cube, Layer = face }] });
            Device.BeginPass(new RenderPassDescriptor { Target = fb, ViewportWidth = sourceSize, ViewportHeight = sourceSize });
            Device.SetTexture(0, source, isCube ? sourceSampler : GetSampler(new SamplerDescriptor
            {
                MagFilter = TextureFilter.Linear,
                MinFilter = TextureFilter.Linear,
                MipFilter = MipFilter.None,
                WrapU = TextureWrap.Repeat,
                WrapV = TextureWrap.ClampToEdge,
                WrapW = TextureWrap.ClampToEdge,
                MaxAnisotropy = 1f,
            }));
            p[0] = new Vector4(face, 0f, 0f, 0f);
            DrawEffect(toCube, EffectState, p, Matrix4x4.Identity);
            Device.EndPass();
        }

        Device.GenerateMipmaps(cube);

        // 2. Diffuse: spherical harmonics from a small level of the cube.
        ComputeSphericalHarmonics(cube, sourceSize, sourceMips, b.Sh);

        // 3. Specular: one GGX-filtered level per roughness step.
        var size = Math.Min(EnvironmentSize, sourceSize);
        var levels = 1;
        for (var s = size; s > EnvironmentMinSize; s >>= 1)
        {
            levels++;
        }

        var specular = Device.CreateTexture(TextureDescriptor.Cube(format, size, levels));
        var prefilter = GetEffect("env-prefilter", () => CubeDirectionGlsl + """
uniform samplerCube u_src;
float arcRadicalInverse(uint bits) {
    bits = (bits << 16u) | (bits >> 16u);
    bits = ((bits & 0x55555555u) << 1u) | ((bits & 0xAAAAAAAAu) >> 1u);
    bits = ((bits & 0x33333333u) << 2u) | ((bits & 0xCCCCCCCCu) >> 2u);
    bits = ((bits & 0x0F0F0F0Fu) << 4u) | ((bits & 0xF0F0F0F0u) >> 4u);
    bits = ((bits & 0x00FF00FFu) << 8u) | ((bits & 0xFF00FF00u) >> 8u);
    return float(bits) * 2.3283064365386963e-10;
}
void main() {
    vec3 N = normalize(arcCubeDir(int(u_p[0].x), v_uv));
    float roughness = u_p[0].y;
    if (roughness <= 0.0) {
        o_color = vec4(textureLod(u_src, N, 0.0).rgb, 1.0);
        return;
    }
    float a = roughness * roughness;
    float a2 = a * a;
    int count = int(u_p[0].w);
    float texelSolidAngle = 4.0 * 3.141592653589793 / (6.0 * u_p[0].z * u_p[0].z);
    vec3 up = abs(N.z) < 0.999 ? vec3(0.0, 0.0, 1.0) : vec3(1.0, 0.0, 0.0);
    vec3 T = normalize(cross(up, N));
    vec3 B = cross(N, T);
    vec3 sum = vec3(0.0);
    float weight = 0.0;
    for (int i = 0; i < count; i++) {
        vec2 xi = vec2(float(i) / float(count), arcRadicalInverse(uint(i)));
        float phi = 6.283185307179586 * xi.x;
        float cosTheta = sqrt((1.0 - xi.y) / (1.0 + (a2 - 1.0) * xi.y));
        float sinTheta = sqrt(max(1.0 - cosTheta * cosTheta, 0.0));
        vec3 H = normalize(T * (sinTheta * cos(phi)) + B * (sinTheta * sin(phi)) + N * cosTheta);
        vec3 L = 2.0 * dot(N, H) * H - N;
        float NoL = dot(N, L);
        if (NoL > 0.0) {
            // Sample a blurrier level where the distribution is sparse, so few samples do not give fireflies.
            float d = cosTheta * cosTheta * (a2 - 1.0) + 1.0;
            float pdf = a2 / (3.141592653589793 * d * d) * 0.25;
            float lod = max(0.5 * log2(1.0 / (float(count) * pdf * texelSolidAngle)) + 1.0, 0.0);
            sum += textureLod(u_src, L, lod).rgb * NoL;
            weight += NoL;
        }
    }
    o_color = vec4(sum / max(weight, 1e-4), 1.0);
}
""", "u_src");
        var cubeSampler = GetSampler(EnvironmentSampler);
        for (var level = 0; level < levels; level++)
        {
            var levelSize = Math.Max(1, size >> level);
            var roughness = levels > 1 ? (float)level / (levels - 1) : 0f;
            for (var face = 0; face < 6; face++)
            {
                using var fb = Device.CreateFramebuffer(new FramebufferDescriptor
                {
                    Colors = [new FramebufferAttachment { Texture = specular, Layer = face, MipLevel = level }],
                });
                Device.BeginPass(new RenderPassDescriptor { Target = fb, ViewportWidth = levelSize, ViewportHeight = levelSize });
                Device.SetTexture(0, cube, cubeSampler);
                p[0] = new Vector4(face, roughness, sourceSize, level == 0 ? 1 : 96);
                DrawEffect(prefilter, EffectState, p, Matrix4x4.Identity);
                Device.EndPass();
            }
        }

        cube.Dispose();
        Device.PopDebugGroup();
        b.Specular = specular;
        b.MaxMip = levels - 1;
    }

    private static Vector3 CubeTexelDirection(int face, float s, float t)
    {
        float x = (s * 2f) - 1f, y = (t * 2f) - 1f;
        return face switch
        {
            0 => new Vector3(1f, -y, -x),
            1 => new Vector3(-1f, -y, x),
            2 => new Vector3(x, 1f, y),
            3 => new Vector3(x, -1f, -y),
            4 => new Vector3(x, -y, 1f),
            _ => new Vector3(-x, -y, -1f),
        };
    }

    /// <summary>Projects the cube onto nine spherical-harmonics coefficients, pre-multiplied so that the shader's
    /// polynomial yields irradiance / π directly.</summary>
    private void ComputeSphericalHarmonics(GpuTexture cube, int size, int mips, Vector3[] sh)
    {
        Array.Clear(sh);
        var level = 0;
        var levelSize = size;
        while (levelSize > 16 && level < mips - 1)
        {
            levelSize >>= 1;
            level++;
        }

        var pixels = new float[levelSize * levelSize * 4];
        var total = 0f;
        Span<float> basis = stackalloc float[9];
        for (var face = 0; face < 6; face++)
        {
            using var fb = Device.CreateFramebuffer(new FramebufferDescriptor
            {
                Colors = [new FramebufferAttachment { Texture = cube, Layer = face, MipLevel = level }],
            });
            Device.ReadPixelsFloat(fb, 0, 0, levelSize, levelSize, pixels);
            for (var y = 0; y < levelSize; y++)
            {
                for (var x = 0; x < levelSize; x++)
                {
                    var dir = CubeTexelDirection(face, (x + 0.5f) / levelSize, (y + 0.5f) / levelSize);
                    var lengthSq = dir.LengthSquared();
                    var weight = 4f / (MathF.Sqrt(lengthSq) * lengthSq);
                    dir = Vector3.Normalize(dir);
                    var o = ((y * levelSize) + x) * 4;
                    var color = new Vector3(pixels[o], pixels[o + 1], pixels[o + 2]);
                    if (float.IsNaN(color.X + color.Y + color.Z))
                    {
                        continue;
                    }

                    basis[0] = 0.282095f;
                    basis[1] = 0.488603f * dir.Y;
                    basis[2] = 0.488603f * dir.Z;
                    basis[3] = 0.488603f * dir.X;
                    basis[4] = 1.092548f * dir.X * dir.Y;
                    basis[5] = 1.092548f * dir.Y * dir.Z;
                    basis[6] = 0.315392f * ((3f * dir.Z * dir.Z) - 1f);
                    basis[7] = 1.092548f * dir.X * dir.Z;
                    basis[8] = 0.546274f * ((dir.X * dir.X) - (dir.Y * dir.Y));
                    for (var i = 0; i < 9; i++)
                    {
                        sh[i] += color * (basis[i] * weight);
                    }

                    total += weight;
                }
            }
        }

        if (total <= 0f)
        {
            return;
        }

        // Normalize the projection, convolve with the cosine lobe, divide by π, and fold in the constants of the
        // polynomial the shader evaluates (ShaderLibrary.Environment).
        var norm = 4f * MathF.PI / total / MathF.PI;
        ReadOnlySpan<float> bake = [0.886227f, 1.023328f, 1.023328f, 1.023328f, 0.858086f, 0.858086f, 0.247708f, 0.858086f, 0.429043f];
        for (var i = 0; i < 9; i++)
        {
            sh[i] *= norm * bake[i];
        }
    }

    private void DrawBackground(Scene scene, ViewPass pass, EnvironmentBinding? environment, bool display)
    {
        var texture = scene.BackgroundTexture;
        if (texture is null)
        {
            return;
        }

        var blur = Math.Clamp(scene.BackgroundBlurriness, 0f, 1f);
        var useEnvironment = blur > 0f && environment is not null && ReferenceEquals(texture, scene.Environment);
        var cube = useEnvironment || texture.Dimension == TextureDimension.Cube;
        var tone = display ? ShaderLibrary.ToneMappingFunction(ToneMapping) : null;
        var key = $"background-{(cube ? "cube" : "equirect")}-{tone}-{display}";
        var program = GetEffect(key, () =>
        {
            var body = (cube ? "uniform samplerCube u_src;\n" : "uniform sampler2D u_src;\n" + EquirectUvGlsl) + (display ? ShaderLibrary.ToneMapping : "");
            body += "void main() {\n    vec4 w = u_matrix * vec4(v_uv * 2.0 - 1.0, 1.0, 1.0);\n    vec3 d = normalize(w.xyz / w.w);\n";
            body += "    d = vec3(u_p[0].w * d.x + u_p[0].z * d.z, d.y, u_p[0].w * d.z - u_p[0].z * d.x);\n";
            body += cube
                ? "    vec3 c = textureLod(u_src, d, u_p[0].y).rgb * u_p[0].x;\n"
                : "    vec3 c = textureLod(u_src, arcEquirectUv(d), 0.0).rgb * u_p[0].x;\n";
            if (tone is not null)
            {
                body += $"    c = {tone}(c * u_p[1].x);\n";
            }

            if (display)
            {
                body += "    c = arcLinearToSrgb(c);\n";
            }

            return body + "    o_color = vec4(c, 1.0);\n}\n";
        }, "u_src");

        // Clip space back to a world direction: the view's rotation only, so the background stays at infinity.
        var view = pass.View;
        view.M41 = 0f;
        view.M42 = 0f;
        view.M43 = 0f;
        if (!Matrix4x4.Invert(view * DeviceProjection(pass.Projection), out var inverse))
        {
            return;
        }

        if (useEnvironment)
        {
            Device.SetTexture(0, environment!.Specular!, GetSampler(EnvironmentSampler));
        }
        else
        {
            var (gpu, sampler) = EnsureTexture(texture);
            Device.SetTexture(0, gpu, cube ? sampler : GetSampler(new SamplerDescriptor
            {
                MagFilter = TextureFilter.Linear,
                MinFilter = TextureFilter.Linear,
                MipFilter = MipFilter.None,
                WrapU = TextureWrap.Repeat,
                WrapV = TextureWrap.ClampToEdge,
                WrapW = TextureWrap.ClampToEdge,
                MaxAnisotropy = 1f,
            }));
        }

        var (sin, cos) = MathF.SinCos(scene.EnvironmentRotation);
        Span<Vector4> p = stackalloc Vector4[2];
        p[0] = new Vector4(scene.BackgroundIntensity, useEnvironment ? blur * environment!.MaxMip : 0f, sin, cos);
        p[1] = new Vector4(Exposure, 0f, 0f, 0f);
        var state = EffectState;
        state.DepthTest = true;
        state.DepthCompare = CompareFunction.LessEqual;
        DrawEffect(program, state, p, inverse, depth: 1f);

        // The effect replaced the frame's bindings on unit 0 and its vertex input; put the frame data back.
        Device.SetTexture(ShaderLibrary.UnitFrameData, _frameTexture!, GetSampler(SamplerDescriptor.NearestClamp));
    }
}
