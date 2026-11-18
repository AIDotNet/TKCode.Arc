using System.Numerics;
using System.Runtime.InteropServices;
using TKCode.Arc.Graphics3D.Rhi;
using TKCode.Arc.Graphics3D.Shading;

namespace TKCode.Arc.Graphics3D;

public sealed partial class Renderer
{
    private const int MaxBloomLevels = 8;
    private readonly GpuTexture?[] _bloomDown = new GpuTexture?[MaxBloomLevels];
    private readonly GpuTexture?[] _bloomUp = new GpuTexture?[MaxBloomLevels];
    private readonly GpuFramebuffer?[] _bloomDownFb = new GpuFramebuffer?[MaxBloomLevels];
    private readonly GpuFramebuffer?[] _bloomUpFb = new GpuFramebuffer?[MaxBloomLevels];
    private GpuTexture? _ldr;
    private GpuFramebuffer? _ldrFb;
    private GpuTexture? _ao;
    private GpuFramebuffer? _aoFb;
    private GpuTexture? _aoBlur;
    private GpuFramebuffer? _aoBlurFb;
    private GpuTexture? _dof;
    private GpuFramebuffer? _dofFb;
    private GpuTexture? _mask;
    private GpuFramebuffer? _maskFb;
    private readonly RenderList _outlineList = new();
    private int _outlineBase;
    private BasicMaterial? _outlineMaterial;
    private int _postWidth;
    private int _postHeight;
    private int _postLevels;

    private void DropPostChain(bool release)
    {
        for (var i = 0; i < MaxBloomLevels; i++)
        {
            if (release)
            {
                _bloomDownFb[i]?.Dispose();
                _bloomUpFb[i]?.Dispose();
                _bloomDown[i]?.Dispose();
                _bloomUp[i]?.Dispose();
            }

            _bloomDownFb[i] = null;
            _bloomUpFb[i] = null;
            _bloomDown[i] = null;
            _bloomUp[i] = null;
        }

        if (release)
        {
            _ldrFb?.Dispose();
            _ldr?.Dispose();
            _aoFb?.Dispose();
            _ao?.Dispose();
            _aoBlurFb?.Dispose();
            _aoBlur?.Dispose();
            _dofFb?.Dispose();
            _dof?.Dispose();
            _maskFb?.Dispose();
            _mask?.Dispose();
            _customFb[0]?.Dispose();
            _custom[0]?.Dispose();
            _customFb[1]?.Dispose();
            _custom[1]?.Dispose();
        }

        _customFb[0] = null;
        _custom[0] = null;
        _customFb[1] = null;
        _custom[1] = null;

        _ldrFb = null;
        _ldr = null;
        _aoFb = null;
        _ao = null;
        _aoBlurFb = null;
        _aoBlur = null;
        _dofFb = null;
        _dof = null;
        _maskFb = null;
        _mask = null;
        _postWidth = 0;
        _postHeight = 0;
        _postLevels = 0;
    }

    private void EnsurePostSize(int width, int height)
    {
        if (_postWidth != width || _postHeight != height)
        {
            DropPostChain(release: true);
            _postWidth = width;
            _postHeight = height;
        }
    }

    private void EnsurePostChain(int width, int height, int levels, bool ldr)
    {
        EnsurePostSize(width, height);
        if (_postLevels != levels)
        {
            for (var i = 0; i < MaxBloomLevels; i++)
            {
                _bloomDownFb[i]?.Dispose();
                _bloomUpFb[i]?.Dispose();
                _bloomDown[i]?.Dispose();
                _bloomUp[i]?.Dispose();
                _bloomDownFb[i] = null;
                _bloomUpFb[i] = null;
                _bloomDown[i] = null;
                _bloomUp[i] = null;
            }

            _postLevels = levels;
            for (var i = 0; i < levels; i++)
            {
                var w = Math.Max(1, width >> (i + 1));
                var h = Math.Max(1, height >> (i + 1));
                _bloomDown[i] = Device.CreateTexture(TextureDescriptor.Texture2D(TextureFormat.RGBA16F, w, h));
                _bloomDownFb[i] = Device.CreateFramebuffer(new FramebufferDescriptor { Colors = [_bloomDown[i]!] });
                _bloomUp[i] = Device.CreateTexture(TextureDescriptor.Texture2D(TextureFormat.RGBA16F, w, h));
                _bloomUpFb[i] = Device.CreateFramebuffer(new FramebufferDescriptor { Colors = [_bloomUp[i]!] });
            }
        }

        if (ldr && _ldr is null)
        {
            _ldr = Device.CreateTexture(TextureDescriptor.Texture2D(TextureFormat.RGBA8, width, height));
            _ldrFb = Device.CreateFramebuffer(new FramebufferDescriptor { Colors = [_ldr] });
        }
    }

    // ---- Passes of the application's own ----

    private sealed class PostPassProgram
    {
        public required GpuProgram Program;
        public required GeneratedShader Shader;
        public GpuBuffer? Buffer;
        public float[] Staging = [];
        public int Version;
        public int UsedFrame;
    }

    private readonly Dictionary<PostPass, PostPassProgram> _postPasses = new(ReferenceEqualityComparer.Instance);
    private readonly GpuTexture?[] _custom = new GpuTexture?[2];
    private readonly GpuFramebuffer?[] _customFb = new GpuFramebuffer?[2];
    private readonly List<PostPass> _stalePasses = [];

    private PostPassProgram GetPostPass(PostPass pass)
    {
        if (_postPasses.TryGetValue(pass, out var entry) && entry.Version == pass.Version)
        {
            return entry;
        }

        if (entry is not null)
        {
            entry.Program.Dispose();
            entry.Buffer?.Dispose();
        }

        var shader = ShaderGenerator.GeneratePostPass(pass.Color, Device.Capabilities.ShaderLanguage, Device.Capabilities.MaxFragmentTextures - ShaderLibrary.FirstMaterialUnit);
        var samplers = ShaderGenerator.PostPassSamplers(shader.Textures.Length);
        entry = new PostPassProgram
        {
            Program = Device.CreateProgram(new ProgramDescriptor
            {
                VertexSource = shader.Vertex,
                FragmentSource = shader.Fragment,
                Label = "post-pass",
                UniformBlocks = GeneratedShader.MaterialBlocks,
                Samplers = samplers,
            }),
            Shader = shader,
            Version = pass.Version,
        };
        if (shader.UniformFloats > 0)
        {
            entry.Staging = new float[shader.UniformFloats];
            entry.Buffer = Device.CreateBuffer(BufferUsage.Uniform, shader.UniformFloats * 4, true, "post-pass");
        }

        _postPasses[pass] = entry;
        _stats.ProgramsCompiled++;
        return entry;
    }

    /// <summary>Runs the enabled passes of <see cref="PostProcessing.Passes"/> in order, each reading the result of
    /// the one before, and returns the texture that holds the last result.</summary>
    private GpuTexture RunCustomPasses(PostProcessing settings, GpuTexture source, int width, int height)
    {
        var linear = GetSampler(SamplerDescriptor.LinearClamp);
        var target = 0;
        foreach (var pass in settings.Passes)
        {
            if (!pass.Enabled)
            {
                continue;
            }

            var entry = GetPostPass(pass);
            entry.UsedFrame = _frame;
            var uniforms = entry.Shader.Uniforms;
            if (entry.Buffer is not null)
            {
                for (var i = 0; i < uniforms.Length; i++)
                {
                    uniforms[i].OnUpdate?.Invoke(uniforms[i]);
                    uniforms[i].Write(entry.Staging.AsSpan(entry.Shader.UniformOffsets[i]));
                }

                Device.WriteBuffer(entry.Buffer, 0, MemoryMarshal.AsBytes<float>(entry.Staging));
            }

            EnsureColorTarget(ref _custom[target], ref _customFb[target], TextureFormat.RGBA16F, width, height);
            Device.BeginPass(new RenderPassDescriptor { Target = _customFb[target], ViewportWidth = width, ViewportHeight = height });
            Device.SetProgram(entry.Program);
            Device.SetRenderState(EffectState);
            if (entry.Buffer is not null)
            {
                Device.SetUniformBuffer(ShaderLibrary.SlotMaterial, entry.Buffer, 0, entry.Shader.UniformFloats * 4);
            }

            Device.SetTexture(0, source, linear);
            var textures = entry.Shader.Textures;
            for (var t = 0; t < textures.Length; t++)
            {
                if (textures[t].Texture is { } texture)
                {
                    var (gpu, sampler) = EnsureTexture(texture);
                    Device.SetTexture(ShaderLibrary.FirstMaterialUnit + t, gpu, sampler);
                }
                else
                {
                    Device.SetTexture(ShaderLibrary.FirstMaterialUnit + t, textures[t].IsCube ? BlackCube : WhiteTexture, linear);
                }
            }

            Device.SetVertexInput(EmptyInput);
            Device.SetDrawConstants(0, 0, 0, 0);
            Device.Draw(PrimitiveTopology.Triangles, 0, 3, 1);
            Device.EndPass();
            source = _custom[target]!;
            target ^= 1;
        }

        // Programs of passes that left the list.
        if (_postPasses.Count > 0)
        {
            foreach (var (pass, entry) in _postPasses)
            {
                if (entry.UsedFrame != _frame && !settings.Passes.Contains(pass))
                {
                    _stalePasses.Add(pass);
                }
            }

            foreach (var pass in _stalePasses)
            {
                var entry = _postPasses[pass];
                entry.Program.Dispose();
                entry.Buffer?.Dispose();
                _postPasses.Remove(pass);
            }

            _stalePasses.Clear();
        }

        return source;
    }

    private void DisposeCustomPasses()
    {
        foreach (var entry in _postPasses.Values)
        {
            entry.Program.Dispose();
            entry.Buffer?.Dispose();
        }

        _postPasses.Clear();
    }

    private void EnsureColorTarget(ref GpuTexture? texture, ref GpuFramebuffer? framebuffer, TextureFormat format, int width, int height)
    {
        if (texture is null)
        {
            texture = Device.CreateTexture(TextureDescriptor.Texture2D(format, width, height));
            framebuffer = Device.CreateFramebuffer(new FramebufferDescriptor { Colors = [texture] });
        }
    }

    /// <summary>Collects the renderables of the outlined objects, drawn later as a white mask. Their records join the
    /// frame data, so it runs before the upload.</summary>
    private bool BuildOutlineList(Scene scene, RenderWorld world)
    {
        _outlineList.Clear();
        _outlineMaterial ??= new BasicMaterial { Name = "outline-mask", DepthTest = false, DepthWrite = false, Side = Side.Double, Fog = false, ToneMapped = false };
        var material = _outlineMaterial;
        var list = _outlineList;
        foreach (var root in PostProcessing.Outline.Objects)
        {
            if (!ReferenceEquals(root.Scene, scene) && !ReferenceEquals(root, scene))
            {
                continue;
            }

            Collect(root);
        }

        void Collect(Object3D node)
        {
            if (!node.Visible)
            {
                return;
            }

            if (node is Renderable { ProxyIndex: >= 0 } renderable && (world.Flags[renderable.ProxyIndex] & ProxyFlags.Visible) != 0)
            {
                var elements = renderable.Geometry.ElementCount;
                if (elements > 0)
                {
                    list.Add(
                        new RenderItem { Object = renderable, Geometry = renderable.Geometry, Material = material, Start = 0, Count = elements, Proxy = renderable.ProxyIndex },
                        (ulong)list.Count);
                }
            }

            var children = node.Children;
            for (var i = 0; i < children.Count; i++)
            {
                Collect(children[i]);
            }
        }

        if (list.Count == 0)
        {
            return false;
        }

        _outlineBase = WriteRecords(list, world);
        return true;
    }

    private void RenderOutlineMask(ViewPass main, EnvironmentBinding? environment, RenderWorld world, int width, int height)
    {
        EnsurePostSize(width, height);
        EnsureColorTarget(ref _mask, ref _maskFb, TextureFormat.RGBA8, width, height);
        Device.BeginPass(new RenderPassDescriptor
        {
            Target = _maskFb,
            ViewportWidth = width,
            ViewportHeight = height,
            ClearColor = true,
            ClearColorValue = Vector4.Zero,
        });
        BindFrameResources(main, environment);
        DrawList(main, _outlineList, _outlineBase, ShaderFeatures.None, world);
        Device.EndPass();
    }

    /// <summary>Turns the linear HDR picture of the scene into the final picture in <paramref name="output"/> (null:
    /// the screen): bloom, vignette, grading, tone mapping and encoding, then FXAA.</summary>
    private void RunPostProcessing(
        GpuTexture hdr, GpuTexture? depth, bool outline, ViewPass view, GpuFramebuffer? output, int width, int height, Vector4 background)
    {
        var settings = PostProcessing;
        var bloom = settings.Bloom.Enabled && settings.Bloom.Strength > 0f;
        var levels = bloom ? Math.Clamp(settings.Bloom.Levels, 3, MaxBloomLevels) : 0;

        // Stop where a level would be smaller than two pixels.
        while (levels > 1 && Math.Min(width, height) >> levels < 2)
        {
            levels--;
        }

        EnsurePostChain(width, height, levels, settings.Fxaa);
        var linear = GetSampler(SamplerDescriptor.LinearClamp);
        var nearest = GetSampler(SamplerDescriptor.NearestClamp);
        Span<Vector4> p = stackalloc Vector4[7];
        Device.PushDebugGroup("post");

        // Depth-based effects work in view space: positions come back from depth through the inverse projection.
        var projection = DeviceProjection(view.Projection);
        Matrix4x4.Invert(projection, out var inverseProjection);
        var ambientOcclusion = settings.AmbientOcclusion.Enabled && depth is not null;
        var depthOfField = settings.DepthOfField.Enabled && depth is not null;
        GpuTexture occlusion = WhiteTexture;
        if (ambientOcclusion)
        {
            EnsureColorTarget(ref _ao, ref _aoFb, TextureFormat.RGBA8, width, height);
            EnsureColorTarget(ref _aoBlur, ref _aoBlurFb, TextureFormat.RGBA8, width, height);
            var ssao = GetEffect("post-ssao", () => DepthGlsl + """
float arcHash(vec2 p) {
    vec3 p3 = fract(vec3(p.xyx) * 0.1031);
    p3 += dot(p3, p3.yzx + 33.33);
    return fract((p3.x + p3.y) * p3.z);
}
void main() {
    float d = texture(u_depth, v_uv).r;
    if (d >= 1.0) {
        o_color = vec4(1.0);
        return;
    }
    vec3 P = arcViewPosition(v_uv);
    // The surface normal from neighbouring depths, using the nearer neighbour on each axis so edges stay clean.
    vec2 t = u_p[1].xy;
    vec3 px = arcViewPosition(v_uv + vec2(t.x, 0.0)) - P;
    vec3 nx = P - arcViewPosition(v_uv - vec2(t.x, 0.0));
    vec3 py = arcViewPosition(v_uv + vec2(0.0, t.y)) - P;
    vec3 ny = P - arcViewPosition(v_uv - vec2(0.0, t.y));
    vec3 N = normalize(cross(abs(px.z) < abs(nx.z) ? px : nx, abs(py.z) < abs(ny.z) ? py : ny));
    vec3 T = normalize(cross(N, abs(N.y) < 0.99 ? vec3(0.0, 1.0, 0.0) : vec3(1.0, 0.0, 0.0)));
    vec3 B = cross(N, T);
    mat4 proj = mat4(u_p[3], u_p[4], u_p[5], u_p[6]);
    float radius = u_p[0].x;
    int count = int(u_p[0].w);
    float spin = arcHash(gl_FragCoord.xy) * 6.2831853;
    float occluded = 0.0;
    for (int i = 0; i < count; i++) {
        float f = (float(i) + 0.5) / float(count);
        float a = float(i) * 2.3999632 + spin;
        float r = sqrt(f);
        vec3 dir = T * (cos(a) * r) + B * (sin(a) * r) + N * sqrt(1.0 - f);
        vec3 s = P + dir * radius * mix(0.2, 1.0, fract(f * 7.31 + spin));
        vec4 clip = proj * vec4(s, 1.0);
        vec2 uv = clip.xy / clip.w * 0.5 + 0.5;
        float surface = arcViewPosition(uv).z;
        // Occluded when the real surface there is in front of the sample; ignore surfaces far outside the radius.
        float range = smoothstep(0.0, 1.0, radius / max(abs(P.z - surface), 1e-4));
        occluded += (surface >= s.z + u_p[0].y ? 1.0 : 0.0) * range;
    }
    float ao = clamp(1.0 - occluded / float(count) * u_p[0].z, 0.0, 1.0);
    o_color = vec4(vec3(ao), 1.0);
}
""", "u_depth");
            var blur = GetEffect("post-ssao-blur", static () => """
uniform sampler2D u_src;
uniform sampler2D u_depth;
void main() {
    vec2 t = u_p[0].xy;
    float center = texture(u_depth, v_uv).r;
    float sum = 0.0;
    float weight = 0.0;
    for (int y = -2; y <= 2; y++) {
        for (int x = -2; x <= 2; x++) {
            vec2 uv = v_uv + vec2(float(x), float(y)) * t;
            // Keep the blur from crossing depth edges.
            float w = 1.0 / (1.0 + abs(texture(u_depth, uv).r - center) * u_p[0].z);
            sum += texture(u_src, uv).r * w;
            weight += w;
        }
    }
    o_color = vec4(vec3(sum / weight), 1.0);
}
""", "u_src", "u_depth");
            var ao = settings.AmbientOcclusion;
            p[0] = new Vector4(MathF.Max(ao.Radius, 1e-4f), ao.Bias, MathF.Max(ao.Intensity, 0f), Math.Clamp(ao.Samples, 8, 32));
            p[1] = new Vector4(1f / width, 1f / height, 0f, 0f);
            p[2] = default;
            WriteMatrixRows(p, 3, projection);
            Device.BeginPass(new RenderPassDescriptor { Target = _aoFb, ViewportWidth = width, ViewportHeight = height });
            Device.SetTexture(0, depth!, nearest);
            DrawEffect(ssao, EffectState, p, inverseProjection);
            Device.EndPass();
            p[0] = new Vector4(1f / width, 1f / height, 4000f, 0f);
            Device.BeginPass(new RenderPassDescriptor { Target = _aoBlurFb, ViewportWidth = width, ViewportHeight = height });
            Device.SetTexture(0, _ao!, nearest);
            Device.SetTexture(1, depth!, nearest);
            DrawEffect(blur, EffectState, p[..1], Matrix4x4.Identity);
            Device.EndPass();
            occlusion = _aoBlur!;
        }

        if (depthOfField)
        {
            EnsureColorTarget(ref _dof, ref _dofFb, TextureFormat.RGBA16F, width, height);
            var dof = GetEffect("post-dof", () => DepthGlsl + """
uniform sampler2D u_src;
uniform sampler2D u_ao;
// Blur radius in pixels at a view distance: 0 inside the sharp zone, growing to the maximum over the falloff.
float arcCoc(vec2 uv) {
    float distance = -arcViewPosition(uv).z;
    return clamp((abs(distance - u_p[0].x) - u_p[0].y * 0.5) / max(u_p[0].z, 1e-4), 0.0, 1.0) * u_p[0].w;
}
vec4 arcShaded(vec2 uv) {
    vec4 c = texture(u_src, uv);
    return vec4(c.rgb * texture(u_ao, uv).r, c.a);
}
void main() {
    float radius = arcCoc(v_uv);
    vec4 sum = arcShaded(v_uv);
    if (radius < 0.5) {
        o_color = sum;
        return;
    }
    float weight = 1.0;
    for (int i = 0; i < 32; i++) {
        float f = (float(i) + 0.5) / 32.0;
        float a = float(i) * 2.3999632;
        float r = sqrt(f) * radius;
        vec2 uv = v_uv + vec2(cos(a), sin(a)) * r * u_p[1].xy;
        // A sample spreads onto this pixel only as far as its own blur reaches: sharp foreground does not smear.
        float w = clamp(arcCoc(uv) - r + 1.0, 0.0, 1.0);
        sum += arcShaded(uv) * w;
        weight += w;
    }
    o_color = sum / weight;
}
""", "u_depth", "u_src", "u_ao");
            var d = settings.DepthOfField;
            p[0] = new Vector4(d.FocusDistance, MathF.Max(d.FocusRange, 0f), MathF.Max(d.Falloff, 1e-4f), MathF.Max(d.MaxBlur, 0f));
            p[1] = new Vector4(1f / width, 1f / height, 0f, 0f);
            Device.BeginPass(new RenderPassDescriptor { Target = _dofFb, ViewportWidth = width, ViewportHeight = height });
            Device.SetTexture(0, depth!, nearest);
            Device.SetTexture(1, hdr, linear);
            Device.SetTexture(2, occlusion, linear);
            DrawEffect(dof, EffectState, p[..2], inverseProjection);
            Device.EndPass();
            hdr = _dof!;

            // The occlusion is in the blurred picture now.
            occlusion = WhiteTexture;
        }

        hdr = RunCustomPasses(settings, hdr, width, height);
        if (bloom)
        {
            var prefilter = GetEffect("post-bloom-prefilter", static () => """
uniform sampler2D u_src;
void main() {
    vec3 c = min(texture(u_src, v_uv).rgb, vec3(64.0));
    float brightness = max(c.r, max(c.g, c.b));
    float knee = max(u_p[0].y, 1e-4);
    float soft = clamp(brightness - u_p[0].x + knee, 0.0, 2.0 * knee);
    soft = soft * soft / (4.0 * knee);
    float contribution = max(soft, brightness - u_p[0].x) / max(brightness, 1e-4);
    o_color = vec4(c * max(contribution, 0.0), 1.0);
}
""", "u_src");
            var down = GetEffect("post-bloom-down", static () => """
uniform sampler2D u_src;
void main() {
    vec2 t = u_p[0].xy;
    vec3 c = texture(u_src, v_uv + t * vec2(-1.0, -1.0)).rgb + texture(u_src, v_uv + t * vec2(1.0, -1.0)).rgb
        + texture(u_src, v_uv + t * vec2(-1.0, 1.0)).rgb + texture(u_src, v_uv + t * vec2(1.0, 1.0)).rgb;
    o_color = vec4(c * 0.25, 1.0);
}
""", "u_src");
            var up = GetEffect("post-bloom-up", static () => """
uniform sampler2D u_low;
uniform sampler2D u_high;
void main() {
    vec2 t = u_p[0].xy;
    vec3 c = texture(u_low, v_uv).rgb * 4.0;
    c += (texture(u_low, v_uv + vec2(-t.x, 0.0)).rgb + texture(u_low, v_uv + vec2(t.x, 0.0)).rgb
        + texture(u_low, v_uv + vec2(0.0, -t.y)).rgb + texture(u_low, v_uv + vec2(0.0, t.y)).rgb) * 2.0;
    c += texture(u_low, v_uv + vec2(-t.x, -t.y)).rgb + texture(u_low, v_uv + vec2(t.x, -t.y)).rgb
        + texture(u_low, v_uv + vec2(-t.x, t.y)).rgb + texture(u_low, v_uv + vec2(t.x, t.y)).rgb;
    o_color = vec4(mix(texture(u_high, v_uv).rgb, c * 0.0625, u_p[0].z), 1.0);
}
""", "u_low", "u_high");

            // Bright parts at half size, then smaller and smaller.
            p[0] = new Vector4(settings.Bloom.Threshold, settings.Bloom.Knee, 0f, 0f);
            Pass(_bloomDownFb[0]!, _bloomDown[0]!);
            Device.SetTexture(0, hdr, linear);
            DrawEffect(prefilter, EffectState, p[..1], Matrix4x4.Identity);
            Device.EndPass();
            for (var i = 1; i < levels; i++)
            {
                var source = _bloomDown[i - 1]!;
                p[0] = new Vector4(0.5f / source.Width, 0.5f / source.Height, 0f, 0f);
                Pass(_bloomDownFb[i]!, _bloomDown[i]!);
                Device.SetTexture(0, source, linear);
                DrawEffect(down, EffectState, p[..1], Matrix4x4.Identity);
                Device.EndPass();
            }

            // Back up, blending each level with the blurred one below it.
            var radius = Math.Clamp(settings.Bloom.Radius, 0f, 1f);
            for (var i = levels - 2; i >= 0; i--)
            {
                var low = i == levels - 2 ? _bloomDown[i + 1]! : _bloomUp[i + 1]!;
                p[0] = new Vector4(1f / low.Width, 1f / low.Height, radius, 0f);
                Pass(_bloomUpFb[i]!, _bloomUp[i]!);
                Device.SetTexture(0, low, linear);
                Device.SetTexture(1, _bloomDown[i]!, linear);
                DrawEffect(up, EffectState, p[..1], Matrix4x4.Identity);
                Device.EndPass();
            }
        }

        var tone = ShaderLibrary.ToneMappingFunction(ToneMapping);
        var composite = GetEffect("post-composite-" + (int)ToneMapping, () => ShaderLibrary.ToneMapping + """
uniform sampler2D u_hdr;
uniform sampler2D u_bloom;
uniform sampler2D u_ao;
uniform sampler2D u_mask;
vec3 arcGrade(vec3 c) {
    // Saturation about luminance, contrast about middle gray, then the renderer's tone mapping and encoding.
    float luma = dot(c, vec3(0.2126, 0.7152, 0.0722));
    c = mix(vec3(luma), c, u_p[1].x);
    c = max((c - 0.18) * u_p[1].y + 0.18, vec3(0.0));

""" + (tone is null ? "" : $"    c = {tone}(c * u_p[1].z);\n") + """
    return arcLinearToSrgb(c);
}
void main() {
    // The scene is premultiplied by its coverage (alpha is 0 where nothing was drawn). Grade the surface color itself,
    // then put the background color behind it, so the background keeps exactly the color it was given.
    vec4 scene = texture(u_hdr, v_uv);
    scene.rgb *= texture(u_ao, v_uv).r;
    vec3 glow = texture(u_bloom, v_uv).rgb * u_p[0].x;
    float a = clamp(scene.a, 0.0, 1.0);
    vec3 c = a > 0.001 ? arcGrade((scene.rgb + glow) / a) * a : arcGrade(scene.rgb + glow);
    vec2 d = v_uv - 0.5;
    float v = smoothstep(0.8, 0.8 - 0.75 * max(u_p[0].z, 1e-3), length(d) * 1.414);
    vec3 result = (c + u_p[2].rgb * (1.0 - a)) * mix(1.0, v, u_p[0].y);
    float alpha = a + u_p[2].a * (1.0 - a);
    // Outline: where the mask is empty here but covered within the line width.
    if (u_p[3].a > 0.0) {
        vec2 t = u_p[4].xy;
        float inside = texture(u_mask, v_uv).r;
        float near = 0.0;
        for (int i = 0; i < 8; i++) {
            float a8 = float(i) * 0.7853982;
            vec2 dir = vec2(cos(a8), sin(a8)) * t;
            near = max(near, max(texture(u_mask, v_uv + dir).r, texture(u_mask, v_uv + dir * 0.5).r));
        }
        float edge = clamp(near - inside, 0.0, 1.0) * u_p[3].a;
        result = mix(result, u_p[3].rgb, edge);
        alpha = max(alpha, edge);
    }
    o_color = vec4(result, alpha);
}
""", "u_hdr", "u_bloom", "u_ao", "u_mask");
        p[0] = new Vector4(bloom ? settings.Bloom.Strength : 0f, settings.Vignette.Enabled ? settings.Vignette.Intensity : 0f, settings.Vignette.Smoothness, 0f);
        p[1] = new Vector4(settings.Saturation, settings.Contrast, Exposure, 0f);
        p[2] = background;
        var line = settings.Outline;
        p[3] = outline ? new Vector4(line.Color.ToSrgb(), Math.Clamp(line.Strength, 0f, 1f)) : default;
        p[4] = new Vector4(line.Thickness / width, line.Thickness / height, 0f, 0f);
        Device.BeginPass(new RenderPassDescriptor { Target = settings.Fxaa ? _ldrFb : output, ViewportWidth = width, ViewportHeight = height });
        Device.SetTexture(0, hdr, linear);
        Device.SetTexture(1, bloom && levels > 1 ? _bloomUp[0]! : bloom ? _bloomDown[0]! : BlackTexture, linear);
        Device.SetTexture(2, occlusion, linear);
        Device.SetTexture(3, outline && _mask is not null ? _mask : BlackTexture, linear);
        DrawEffect(composite, EffectState, p[..5], Matrix4x4.Identity);
        Device.EndPass();

        if (settings.Fxaa)
        {
            var fxaa = GetEffect("post-fxaa", static () => """
uniform sampler2D u_src;
float arcLuma(vec3 c) { return dot(c, vec3(0.299, 0.587, 0.114)); }
void main() {
    vec2 t = u_p[0].xy;
    vec4 center = texture(u_src, v_uv);
    float lumaM = arcLuma(center.rgb);
    float lumaNW = arcLuma(texture(u_src, v_uv + vec2(-t.x, -t.y)).rgb);
    float lumaNE = arcLuma(texture(u_src, v_uv + vec2(t.x, -t.y)).rgb);
    float lumaSW = arcLuma(texture(u_src, v_uv + vec2(-t.x, t.y)).rgb);
    float lumaSE = arcLuma(texture(u_src, v_uv + vec2(t.x, t.y)).rgb);
    float lumaMin = min(lumaM, min(min(lumaNW, lumaNE), min(lumaSW, lumaSE)));
    float lumaMax = max(lumaM, max(max(lumaNW, lumaNE), max(lumaSW, lumaSE)));
    if (lumaMax - lumaMin < max(0.0312, lumaMax * 0.125)) {
        o_color = center;
        return;
    }
    vec2 dir = vec2(-((lumaNW + lumaNE) - (lumaSW + lumaSE)), (lumaNW + lumaSW) - (lumaNE + lumaSE));
    float reduce = max((lumaNW + lumaNE + lumaSW + lumaSE) * 0.03125, 0.0078125);
    float scale = 1.0 / (min(abs(dir.x), abs(dir.y)) + reduce);
    dir = clamp(dir * scale, vec2(-8.0), vec2(8.0)) * t;
    vec4 a = 0.5 * (texture(u_src, v_uv + dir * (1.0 / 3.0 - 0.5)) + texture(u_src, v_uv + dir * (2.0 / 3.0 - 0.5)));
    vec4 b = a * 0.5 + 0.25 * (texture(u_src, v_uv + dir * -0.5) + texture(u_src, v_uv + dir * 0.5));
    float lumaB = arcLuma(b.rgb);
    o_color = lumaB < lumaMin || lumaB > lumaMax ? a : b;
}
""", "u_src");
            p[0] = new Vector4(1f / width, 1f / height, 0f, 0f);
            Device.BeginPass(new RenderPassDescriptor { Target = output, ViewportWidth = width, ViewportHeight = height });
            Device.SetTexture(0, _ldr!, linear);
            DrawEffect(fxaa, EffectState, p[..1], Matrix4x4.Identity);
            Device.EndPass();
        }

        Device.PopDebugGroup();
    }

    // View-space position of a pixel from the depth texture and the inverse projection (in u_matrix). The depth
    // buffer value becomes clip-space z the way the device's clip depth range says.
    private string DepthGlsl => DepthGlslTemplate.Replace("%CLIP_Z%", ShaderLibrary.ClipDepthOf(Device.Capabilities.ClipDepth, "d"), StringComparison.Ordinal);

    private const string DepthGlslTemplate = """
uniform sampler2D u_depth;
vec3 arcViewPosition(vec2 uv) {
    float d = texture(u_depth, uv).r;
    vec4 p = u_matrix * vec4(uv * 2.0 - 1.0, %CLIP_Z%, 1.0);
    return p.xyz / p.w;
}

""";

    private static void WriteMatrixRows(Span<Vector4> p, int start, in Matrix4x4 m)
    {
        p[start] = new Vector4(m.M11, m.M12, m.M13, m.M14);
        p[start + 1] = new Vector4(m.M21, m.M22, m.M23, m.M24);
        p[start + 2] = new Vector4(m.M31, m.M32, m.M33, m.M34);
        p[start + 3] = new Vector4(m.M41, m.M42, m.M43, m.M44);
    }

    private void Pass(GpuFramebuffer framebuffer, GpuTexture texture) =>
        Device.BeginPass(new RenderPassDescriptor { Target = framebuffer, ViewportWidth = texture.Width, ViewportHeight = texture.Height });
}
