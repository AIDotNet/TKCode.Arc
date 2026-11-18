using System.Numerics;
using System.Runtime.InteropServices;
using TKCode.Arc.Graphics3D.Rhi;
using TKCode.Arc.Graphics3D.Shading;

namespace TKCode.Arc.Graphics3D.Tests;

/// <summary>What the renderer relies on from a <see cref="GraphicsDevice"/> without saying so: where row 0 is, which
/// way textures are sampled, what a blit, a partial clear or a write between two draws does. Each test runs on the
/// device the context gives (OpenGL, OpenGL ES through ANGLE, or WebGPU) and expects the same picture from all of them.</summary>
public class DeviceConventionTests
{
    private const int Size = 8;

    private static readonly UniformBlockBinding[] s_blocks = [new("TestBlock", 3)];

    private const string Block = """
layout(std140) uniform TestBlock {
    vec4 u_p[4];
};

""";

    // One triangle over the target; u_p[3].w is its clip-space depth. v_uv runs 0..1 from the bottom left.
    private const string FullScreenVertex = Block + """
uniform ivec4 u_draw;
out vec2 v_uv;
flat out vec4 v_draw;
void main() {
    vec2 p = vec2(float((gl_VertexID << 1) & 2), float(gl_VertexID & 2));
    v_uv = p;
    v_draw = vec4(u_draw);
    gl_Position = vec4(p * 2.0 - 1.0, u_p[3].w, 1.0);
}

""";

    private const string FragmentInputs = Block + "in vec2 v_uv;\nflat in vec4 v_draw;\nout vec4 o_color;\n";

    /// <summary>The device, a parameter buffer on slot 3, and the objects a test creates, disposed in reverse order.</summary>
    private sealed class Rig : IDisposable
    {
        private readonly GlTestContext _context = GlTestContext.CreateOrSkip();
        private readonly List<IDisposable> _owned = [];
        private readonly float[] _parameters = new float[16];
        private GpuBuffer? _parameterBuffer;
        private GpuVertexInput? _empty;

        public GraphicsDevice Device => _context.Device;

        public T Own<T>(T resource)
            where T : IDisposable
        {
            _owned.Add(resource);
            return resource;
        }

        /// <summary>A program that draws the full-screen triangle with the given fragment <c>main</c>.</summary>
        public GpuProgram Program(string fragmentBody, params string[] samplers) => ProgramFrom(FullScreenVertex, FragmentInputs + fragmentBody, samplers);

        /// <summary>A program from both stages, written in the GLSL the engine's languages share.</summary>
        public GpuProgram ProgramFrom(string vertex, string fragment, params string[] samplers)
        {
            var language = Device.Capabilities.ShaderLanguage;
            var header = ShaderLibrary.Header(language);
            var bindings = new SamplerBinding[samplers.Length];
            for (var i = 0; i < samplers.Length; i++)
            {
                bindings[i] = new SamplerBinding(samplers[i], i);
            }

            vertex = header + vertex;
            fragment = header + fragment;
            if (language == ShaderLanguage.Glsl450)
            {
                ShaderDialect.ToGlsl450(ref vertex, ref fragment, s_blocks, bindings);
            }

            return Own(Device.CreateProgram(new ProgramDescriptor
            {
                VertexSource = vertex,
                FragmentSource = fragment,
                UniformBlocks = s_blocks,
                Samplers = bindings,
                Label = "convention-test",
            }));
        }

        public (GpuTexture Texture, GpuFramebuffer Framebuffer) Target(TextureFormat format = TextureFormat.RGBA8, int size = Size, int samples = 1, int mips = 1)
        {
            var texture = Own(Device.CreateTexture(TextureDescriptor.Texture2D(format, size, size, mips, samples)));
            return (texture, Own(Device.CreateFramebuffer(new FramebufferDescriptor { Colors = [texture] })));
        }

        public GpuSampler Sampler(SamplerDescriptor descriptor) => Own(Device.CreateSampler(descriptor));

        public GpuVertexInput Empty => _empty ??= Own(Device.CreateVertexInput(new VertexInputDescriptor { Attributes = [], Buffers = [] }));

        /// <summary>Writes the four parameters and binds them. <paramref name="clipDepth"/> is a depth buffer value (0
        /// near, 1 far); the device's clip-space z for it goes to the vertex stage.</summary>
        public void Parameters(Vector4 p0 = default, Vector4 p1 = default, Vector4 p2 = default, float clipDepth = 0f)
        {
            var z = Device.Capabilities.ClipDepth == ClipDepthRange.NegativeOneToOne ? (clipDepth * 2f) - 1f : clipDepth;
            p0.CopyTo(_parameters, 0);
            p1.CopyTo(_parameters, 4);
            p2.CopyTo(_parameters, 8);
            new Vector4(0f, 0f, 0f, z).CopyTo(_parameters, 12);
            _parameterBuffer ??= Own(Device.CreateBuffer(BufferUsage.Uniform, 64, true, "test-parameters"));
            Device.WriteBuffer(_parameterBuffer, 0, MemoryMarshal.AsBytes<float>(_parameters));
            Device.SetUniformBuffer(3, _parameterBuffer, 0, 64);
        }

        public void Begin(GpuFramebuffer target, Vector4? clear = null, int size = Size) => Device.BeginPass(new RenderPassDescriptor
        {
            Target = target,
            ViewportWidth = size,
            ViewportHeight = size,
            ClearColor = clear is not null,
            ClearColorValue = clear ?? default,
        });

        /// <summary>Draws the full-screen triangle with no depth test and no culling.</summary>
        public void Draw(GpuProgram program, RenderState? state = null)
        {
            Device.SetProgram(program);
            Device.SetRenderState(state ?? Flat);
            Device.SetVertexInput(Empty);
            Device.Draw(PrimitiveTopology.Triangles, 0, 3, 1);
        }

        /// <summary>The pixel at column x of row y, row 0 being the first row the device returns (the bottom one).</summary>
        public (byte R, byte G, byte B, byte A) Read(GpuFramebuffer framebuffer, int x, int y, int size = Size)
        {
            var pixels = new byte[size * size * 4];
            Device.ReadPixels(framebuffer, 0, 0, size, size, pixels);
            var o = ((y * size) + x) * 4;
            return (pixels[o], pixels[o + 1], pixels[o + 2], pixels[o + 3]);
        }

        public void Dispose()
        {
            try
            {
                for (var i = _owned.Count - 1; i >= 0; i--)
                {
                    _owned[i].Dispose();
                }

                Device.DrainErrors().ShouldBeEmpty();
            }
            finally
            {
                _context.Dispose();
            }
        }
    }

    private static RenderState Flat => new() { Cull = CullMode.None, DepthCompare = CompareFunction.Always, ColorWrite = true };

    private static readonly Vector4 s_red = new(1f, 0f, 0f, 1f);
    private static readonly Vector4 s_green = new(0f, 1f, 0f, 1f);
    private static readonly Vector4 s_blue = new(0f, 0f, 1f, 1f);

    private const string SolidColor = "void main() {\n    o_color = u_p[0];\n}\n";

    [Fact]
    public void Row_Zero_Is_The_Bottom_Of_Clip_Space_And_Of_The_Fragment_Coordinate()
    {
        using var rig = new Rig();
        var (_, target) = rig.Target();

        // Red in the lower half of clip space; green is the fragment coordinate's y over the height.
        var program = rig.Program("void main() {\n    o_color = vec4(v_uv.y < 0.5 ? 1.0 : 0.0, gl_FragCoord.y / 8.0, v_uv.x, 1.0);\n}\n");
        rig.Parameters();
        rig.Begin(target, s_blue);
        rig.Draw(program);
        rig.Device.EndPass();

        var bottom = rig.Read(target, 1, 0);
        var top = rig.Read(target, 1, Size - 1);
        bottom.R.ShouldBe((byte)255);
        top.R.ShouldBe((byte)0);
        ((int)bottom.G).ShouldBeInRange(14, 18);      // 0.5 / 8
        ((int)top.G).ShouldBeInRange(237, 241);       // 7.5 / 8
        ((int)rig.Read(target, 0, 3).B).ShouldBeLessThan(40);
        ((int)rig.Read(target, 7, 3).B).ShouldBeGreaterThan(215);
    }

    [Fact]
    public void Viewport_Counts_From_The_Bottom_Left()
    {
        using var rig = new Rig();
        var (_, target) = rig.Target();
        var program = rig.Program(SolidColor);
        rig.Parameters(s_red);
        rig.Begin(target, s_blue);
        rig.Device.SetViewport(0, 0, 4, 2);
        rig.Draw(program);
        rig.Device.EndPass();

        rig.Read(target, 3, 1).ShouldBe(((byte)255, (byte)0, (byte)0, (byte)255));
        rig.Read(target, 4, 1).ShouldBe(((byte)0, (byte)0, (byte)255, (byte)255));
        rig.Read(target, 3, 2).ShouldBe(((byte)0, (byte)0, (byte)255, (byte)255));

        // A rectangle read back starts at its own bottom-left corner.
        var pixels = new byte[2 * 2 * 4];
        rig.Device.ReadPixels(target, 3, 1, 2, 2, pixels);
        pixels[0].ShouldBe((byte)255);      // (3, 1): red
        pixels[4].ShouldBe((byte)0);        // (4, 1): blue
        pixels[8].ShouldBe((byte)0);        // (3, 2): blue
    }

    [Fact]
    public void Scissor_Limits_Draws_And_A_Partial_Clear_Leaves_The_Rest()
    {
        using var rig = new Rig();
        var (_, target) = rig.Target();
        var program = rig.Program(SolidColor);
        rig.Begin(target, s_blue);
        rig.Device.EndPass();

        // A pass that clears only its viewport rectangle.
        rig.Device.BeginPass(new RenderPassDescriptor
        {
            Target = target,
            ViewportX = 2,
            ViewportY = 1,
            ViewportWidth = 4,
            ViewportHeight = 3,
            ClearColor = true,
            ClearColorValue = s_green,
            ScissorToViewport = true,
        });
        rig.Device.EndPass();
        rig.Read(target, 2, 1).ShouldBe(((byte)0, (byte)255, (byte)0, (byte)255));
        rig.Read(target, 5, 3).ShouldBe(((byte)0, (byte)255, (byte)0, (byte)255));
        rig.Read(target, 1, 1).ShouldBe(((byte)0, (byte)0, (byte)255, (byte)255));
        rig.Read(target, 6, 3).ShouldBe(((byte)0, (byte)0, (byte)255, (byte)255));
        rig.Read(target, 2, 0).ShouldBe(((byte)0, (byte)0, (byte)255, (byte)255));
        rig.Read(target, 2, 4).ShouldBe(((byte)0, (byte)0, (byte)255, (byte)255));

        // A scissor inside a full-size viewport.
        rig.Parameters(s_red);
        rig.Begin(target);
        rig.Device.SetScissor(0, 6, 3, 2, true);
        rig.Draw(program);
        rig.Device.SetScissor(0, 0, 0, 0, false);
        rig.Device.EndPass();
        rig.Read(target, 2, 7).ShouldBe(((byte)255, (byte)0, (byte)0, (byte)255));
        rig.Read(target, 3, 7).ShouldBe(((byte)0, (byte)0, (byte)255, (byte)255));
        rig.Read(target, 2, 5).ShouldBe(((byte)0, (byte)0, (byte)255, (byte)255));
        rig.Read(target, 3, 2).ShouldBe(((byte)0, (byte)255, (byte)0, (byte)255));
    }

    [Fact]
    public void Uploaded_Texture_Row_Zero_Is_Sampled_At_V_Zero()
    {
        using var rig = new Rig();
        var (_, target) = rig.Target();
        var texture = rig.Own(rig.Device.CreateTexture(TextureDescriptor.Texture2D(TextureFormat.RGBA8, 2, 2)));

        // Row 0: red, green. Row 1: blue, white.
        rig.Device.WriteTexture(texture, 0, 0, 0, 0, 2, 2, [255, 0, 0, 255, 0, 255, 0, 255, 0, 0, 255, 255, 255, 255, 255, 255]);
        var program = rig.Program("uniform sampler2D u_source;\nvoid main() {\n    o_color = texture(u_source, v_uv);\n}\n", "u_source");
        rig.Parameters();
        rig.Begin(target, s_blue);
        rig.Device.SetTexture(0, texture, rig.Sampler(SamplerDescriptor.NearestClamp));
        rig.Draw(program);
        rig.Device.EndPass();

        rig.Read(target, 1, 1).ShouldBe(((byte)255, (byte)0, (byte)0, (byte)255));
        rig.Read(target, 6, 1).ShouldBe(((byte)0, (byte)255, (byte)0, (byte)255));
        rig.Read(target, 1, 6).ShouldBe(((byte)0, (byte)0, (byte)255, (byte)255));
        rig.Read(target, 6, 6).ShouldBe(((byte)255, (byte)255, (byte)255, (byte)255));
    }

    [Fact]
    public void Rendered_Texture_Is_Sampled_The_Way_It_Was_Drawn()
    {
        using var rig = new Rig();
        var (first, firstTarget) = rig.Target();
        var (_, second) = rig.Target();
        var pattern = rig.Program("void main() {\n    o_color = vec4(v_uv.x < 0.5 ? 1.0 : 0.0, v_uv.y < 0.5 ? 1.0 : 0.0, 0.0, 1.0);\n}\n");
        var copy = rig.Program("uniform sampler2D u_source;\nvoid main() {\n    o_color = texture(u_source, v_uv);\n}\n", "u_source");
        rig.Parameters();
        rig.Begin(firstTarget, s_blue);
        rig.Draw(pattern);
        rig.Device.EndPass();
        rig.Begin(second, s_blue);
        rig.Device.SetTexture(0, first, rig.Sampler(SamplerDescriptor.NearestClamp));
        rig.Draw(copy);
        rig.Device.EndPass();

        foreach (var target in new[] { firstTarget, second })
        {
            rig.Read(target, 1, 1).ShouldBe(((byte)255, (byte)255, (byte)0, (byte)255));
            rig.Read(target, 6, 1).ShouldBe(((byte)0, (byte)255, (byte)0, (byte)255));
            rig.Read(target, 1, 6).ShouldBe(((byte)255, (byte)0, (byte)0, (byte)255));
            rig.Read(target, 6, 6).ShouldBe(((byte)0, (byte)0, (byte)0, (byte)255));
        }
    }

    [Fact]
    public void Cube_Faces_Are_Sampled_Where_They_Were_Drawn()
    {
        using var rig = new Rig();
        var device = rig.Device;
        var cube = rig.Own(device.CreateTexture(TextureDescriptor.Cube(TextureFormat.RGBA8, Size)));
        var (_, target) = rig.Target();

        // Each face: its index in blue, and the position on the face in red and green.
        var paint = rig.Program("void main() {\n    o_color = vec4(v_uv, u_p[0].x / 5.0, 1.0);\n}\n");
        for (var face = 0; face < 6; face++)
        {
            var framebuffer = rig.Own(device.CreateFramebuffer(new FramebufferDescriptor { Colors = [new FramebufferAttachment { Texture = cube, Layer = face }] }));
            rig.Parameters(new Vector4(face, 0f, 0f, 0f));
            rig.Begin(framebuffer, s_blue);
            rig.Draw(paint);
            device.EndPass();
        }

        // The direction of a point (s, t) on a face, as the renderer's environment code has it.
        static Vector3 Direction(int face, float s, float t)
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

        var sample = rig.Program("uniform samplerCube u_cube;\nvoid main() {\n    o_color = textureLod(u_cube, u_p[0].xyz, 0.0);\n}\n", "u_cube");
        var sampler = rig.Sampler(SamplerDescriptor.NearestClamp);
        for (var face = 0; face < 6; face++)
        {
            foreach (var (s, t) in new[] { (0.1875f, 0.1875f), (0.8125f, 0.1875f), (0.1875f, 0.8125f) })
            {
                rig.Parameters(new Vector4(Direction(face, s, t), 0f));
                rig.Begin(target, s_blue);
                device.SetTexture(0, cube, sampler);
                rig.Draw(sample);
                device.EndPass();
                var pixel = rig.Read(target, 4, 4);
                ((int)pixel.R).ShouldBeInRange((int)(s * 255f) - 3, (int)(s * 255f) + 3, $"face {face} at ({s}, {t})");
                ((int)pixel.G).ShouldBeInRange((int)(t * 255f) - 3, (int)(t * 255f) + 3, $"face {face} at ({s}, {t})");
                ((int)pixel.B).ShouldBeInRange((face * 51) - 2, (face * 51) + 2, $"face {face}");
            }
        }
    }

    [Fact]
    public void Blit_Copies_Scales_And_Resolves_With_Rows_In_Place()
    {
        using var rig = new Rig();
        var device = rig.Device;
        var (_, source) = rig.Target();
        var (_, same) = rig.Target();
        var (_, half) = rig.Target(size: 4);
        var program = rig.Program(SolidColor);

        // Bottom half red, top half green.
        rig.Parameters(s_red);
        rig.Begin(source, s_green);
        device.SetViewport(0, 0, Size, 4);
        rig.Draw(program);
        device.EndPass();

        device.Blit(source, same, color: true, depth: false, linear: false);
        rig.Read(same, 2, 3).ShouldBe(((byte)255, (byte)0, (byte)0, (byte)255));
        rig.Read(same, 2, 4).ShouldBe(((byte)0, (byte)255, (byte)0, (byte)255));

        device.Blit(source, half, color: true, depth: false, linear: true);
        rig.Read(half, 1, 1, 4).ShouldBe(((byte)255, (byte)0, (byte)0, (byte)255));
        rig.Read(half, 1, 2, 4).ShouldBe(((byte)0, (byte)255, (byte)0, (byte)255));

        // Multisampled: a triangle edge through pixel centers resolves to a mix.
        var (_, multisampled) = rig.Target(samples: 4);
        var (_, resolved) = rig.Target();
        var triangle = rig.ProgramFrom(
            "layout(location = 0) in vec2 a_position;\nvoid main() {\n    gl_Position = vec4(a_position, 0.0, 1.0);\n}\n",
            "out vec4 o_color;\nvoid main() {\n    o_color = vec4(1.0, 0.0, 0.0, 1.0);\n}\n");
        var vertices = rig.Own(device.CreateBuffer(BufferUsage.Vertex, 24, false));
        device.WriteBuffer(vertices, 0, MemoryMarshal.AsBytes<float>([-1f, -1f, 1f, -1f, -1f, 1f]));
        var input = rig.Own(device.CreateVertexInput(new VertexInputDescriptor
        {
            Attributes = [new VertexAttributeBinding { Location = 0, BufferIndex = 0, Format = VertexFormat.Float2, Stride = 8 }],
            Buffers = [vertices],
        }));
        rig.Begin(multisampled, s_blue);
        device.SetProgram(triangle);
        device.SetRenderState(Flat);
        device.SetVertexInput(input);
        device.Draw(PrimitiveTopology.Triangles, 0, 3, 1);
        device.EndPass();
        device.Blit(multisampled, resolved, color: true, depth: false, linear: false);
        rig.Read(resolved, 1, 1).ShouldBe(((byte)255, (byte)0, (byte)0, (byte)255));
        rig.Read(resolved, 6, 6).ShouldBe(((byte)0, (byte)0, (byte)255, (byte)255));
        var edge = rig.Read(resolved, 3, 4);
        ((int)edge.R).ShouldBeInRange(40, 215);
        ((int)edge.B).ShouldBeInRange(40, 215);
    }

    [Fact]
    public void Depth_Runs_From_Zero_Near_To_One_Far_And_Compare_Sampling_Reads_It()
    {
        using var rig = new Rig();
        var device = rig.Device;
        var depth = rig.Own(device.CreateTexture(TextureDescriptor.Texture2D(TextureFormat.Depth24, Size, Size)));
        var depthTarget = rig.Own(device.CreateFramebuffer(new FramebufferDescriptor { Depth = depth }));
        var (_, target) = rig.Target();

        // A surface at depth 0.25, then one behind it at 0.75 that the depth test rejects.
        var write = rig.ProgramFrom(FullScreenVertex, Block + "in vec2 v_uv;\nflat in vec4 v_draw;\nvoid main() {\n}\n");
        var state = new RenderState { Cull = CullMode.None, DepthTest = true, DepthWrite = true, DepthCompare = CompareFunction.LessEqual, ColorWrite = true };
        device.BeginPass(new RenderPassDescriptor { Target = depthTarget, ViewportWidth = Size, ViewportHeight = Size, ClearDepth = true, ClearDepthValue = 1f });
        rig.Parameters(clipDepth: 0.25f);
        rig.Draw(write, state);
        device.EndPass();
        device.BeginPass(new RenderPassDescriptor { Target = depthTarget, ViewportWidth = Size, ViewportHeight = Size });
        rig.Parameters(clipDepth: 0.75f);
        rig.Draw(write, state);
        device.EndPass();

        var compare = rig.Program("uniform sampler2DShadow u_depth;\nvoid main() {\n    o_color = vec4(texture(u_depth, vec3(v_uv, u_p[0].x)));\n}\n", "u_depth");
        var sampler = rig.Sampler(new SamplerDescriptor
        {
            MagFilter = TextureFilter.Linear,
            MinFilter = TextureFilter.Linear,
            WrapU = TextureWrap.ClampToEdge,
            WrapV = TextureWrap.ClampToEdge,
            WrapW = TextureWrap.ClampToEdge,
            CompareEnabled = true,
            Compare = CompareFunction.LessEqual,
        });
        foreach (var (reference, lit) in new[] { (0.2f, 255), (0.3f, 0) })
        {
            rig.Parameters(new Vector4(reference, 0f, 0f, 0f));
            rig.Begin(target, s_blue);
            device.SetTexture(0, depth, sampler);
            rig.Draw(compare);
            device.EndPass();
            ((int)rig.Read(target, 4, 4).R).ShouldBe(lit, $"reference depth {reference} against a stored 0.25");
        }

        // The same texture read as numbers.
        var read = rig.Program("uniform sampler2D u_depth;\nvoid main() {\n    o_color = vec4(texture(u_depth, v_uv).r, 0.0, 0.0, 1.0);\n}\n", "u_depth");
        rig.Parameters();
        rig.Begin(target, s_blue);
        device.SetTexture(0, depth, rig.Sampler(SamplerDescriptor.NearestClamp));
        rig.Draw(read);
        device.EndPass();
        ((int)rig.Read(target, 4, 4).R).ShouldBeInRange(62, 66);
    }

    [Fact]
    public void Float_Target_Keeps_Values_Outside_Zero_To_One()
    {
        using var rig = new Rig();
        if (!rig.Device.Capabilities.FloatRenderTargets)
        {
            return;
        }

        var (_, target) = rig.Target(TextureFormat.RGBA16F);
        var program = rig.Program(SolidColor);
        rig.Parameters(new Vector4(2.5f, 0.25f, -1f, 1f));
        rig.Begin(target, s_blue);
        rig.Draw(program);
        rig.Device.EndPass();

        var pixels = new float[2 * 2 * 4];
        rig.Device.ReadPixelsFloat(target, 3, 3, 2, 2, pixels);
        for (var i = 0; i < 4; i++)
        {
            pixels[i * 4].ShouldBe(2.5f);
            pixels[(i * 4) + 1].ShouldBe(0.25f);
            pixels[(i * 4) + 2].ShouldBe(-1f);
            pixels[(i * 4) + 3].ShouldBe(1f);
        }

        // Read as bytes the values are clamped. (OpenGL ES reads a float framebuffer as floats only.)
        if (rig.Device.Capabilities.ShaderLanguage != ShaderLanguage.GlslEs300)
        {
            rig.Read(target, 4, 4).ShouldBe(((byte)255, (byte)64, (byte)0, (byte)255));
        }
    }

    [Theory]
    [InlineData(TextureFormat.R16F, 1)]
    [InlineData(TextureFormat.RG16F, 2)]
    [InlineData(TextureFormat.RGBA16F, 4)]
    [InlineData(TextureFormat.R32F, 1)]
    [InlineData(TextureFormat.RG32F, 2)]
    [InlineData(TextureFormat.RGBA32F, 4)]
    [InlineData(TextureFormat.RG11B10F, 3)]
    public void Float_Textures_Are_Written_As_32_Bit_Floats(TextureFormat format, int channels)
    {
        using var rig = new Rig();
        var device = rig.Device;
        if (!device.Capabilities.FloatRenderTargets)
        {
            return;
        }

        // One pixel of (3.5, 0.125, 10, 0.5), cut to the channels the format has.
        ReadOnlySpan<float> value = [3.5f, 0.125f, 10f, 0.5f];
        var texture = rig.Own(device.CreateTexture(TextureDescriptor.Texture2D(format, 1, 1)));
        device.WriteTexture(texture, 0, 0, 0, 0, 1, 1, MemoryMarshal.AsBytes(value[..channels]));
        var (_, target) = rig.Target(TextureFormat.RGBA16F);
        var program = rig.Program("uniform sampler2D u_source;\nvoid main() {\n    o_color = texture(u_source, v_uv);\n}\n", "u_source");
        rig.Parameters();
        rig.Begin(target, s_blue);
        device.SetTexture(0, texture, rig.Sampler(SamplerDescriptor.NearestClamp));
        rig.Draw(program);
        device.EndPass();

        var pixel = new float[4];
        device.ReadPixelsFloat(target, 4, 4, 1, 1, pixel);

        // Missing channels read as 0, and alpha as 1. The packed 11/11/10-bit format keeps five or six bits of mantissa.
        var tolerance = format == TextureFormat.RG11B10F ? 0.04f : 0f;
        for (var c = 0; c < 4; c++)
        {
            var expected = c < channels ? value[c] : c == 3 ? 1f : 0f;
            pixel[c].ShouldBe(expected, expected * tolerance, $"channel {c} of {format}");
        }
    }

    [Fact]
    public void Compressed_Blocks_Are_Uploaded_As_They_Are()
    {
        using var rig = new Rig();
        var device = rig.Device;
        if (!device.Capabilities.TextureCompressionBc)
        {
            return;
        }

        // One BC1 block: color 0 is red (565: F800), color 1 blue (001F), every texel takes color 0.
        var texture = rig.Own(device.CreateTexture(TextureDescriptor.Texture2D(TextureFormat.Bc1Rgba, 4, 4)));
        device.WriteTexture(texture, 0, 0, 0, 0, 4, 4, [0x00, 0xF8, 0x1F, 0x00, 0x00, 0x00, 0x00, 0x00]);
        var (_, target) = rig.Target();
        var program = rig.Program("uniform sampler2D u_source;\nvoid main() {\n    o_color = texture(u_source, v_uv);\n}\n", "u_source");
        rig.Parameters();
        rig.Begin(target, s_blue);
        device.SetTexture(0, texture, rig.Sampler(SamplerDescriptor.NearestClamp));
        rig.Draw(program);
        device.EndPass();
        rig.Read(target, 4, 4).ShouldBe(((byte)255, (byte)0, (byte)0, (byte)255));
    }

    [Fact]
    public void Generated_Mip_Levels_Average_The_Level_Above()
    {
        using var rig = new Rig();
        var device = rig.Device;
        var texture = rig.Own(device.CreateTexture(TextureDescriptor.Texture2D(TextureFormat.RGBA8, 4, 4, 3)));
        var pixels = new byte[4 * 4 * 4];
        for (var y = 0; y < 4; y++)
        {
            for (var x = 0; x < 4; x++)
            {
                // Left half: a checker of black and white. Right half: red.
                var o = ((y * 4) + x) * 4;
                var white = ((x + y) & 1) == 0;
                pixels[o] = (byte)(x >= 2 || white ? 255 : 0);
                pixels[o + 1] = (byte)(x < 2 && white ? 255 : 0);
                pixels[o + 2] = (byte)(x < 2 && white ? 255 : 0);
                pixels[o + 3] = 255;
            }
        }

        device.WriteTexture(texture, 0, 0, 0, 0, 4, 4, pixels);
        device.GenerateMipmaps(texture);
        var (_, target) = rig.Target();
        var program = rig.Program("uniform sampler2D u_source;\nvoid main() {\n    o_color = textureLod(u_source, v_uv, u_p[0].x);\n}\n", "u_source");
        var sampler = rig.Sampler(new SamplerDescriptor
        {
            MagFilter = TextureFilter.Nearest,
            MinFilter = TextureFilter.Nearest,
            MipFilter = MipFilter.Nearest,
            WrapU = TextureWrap.ClampToEdge,
            WrapV = TextureWrap.ClampToEdge,
            WrapW = TextureWrap.ClampToEdge,
        });

        // Level 1 (2x2): gray on the left, red on the right. Level 2 (1x1): their mean.
        rig.Parameters(new Vector4(1f, 0f, 0f, 0f));
        rig.Begin(target, s_blue);
        device.SetTexture(0, texture, sampler);
        rig.Draw(program);
        device.EndPass();
        var left = rig.Read(target, 1, 4);
        ((int)left.R).ShouldBeInRange(126, 129);
        ((int)left.G).ShouldBeInRange(126, 129);
        rig.Read(target, 6, 4).ShouldBe(((byte)255, (byte)0, (byte)0, (byte)255));

        rig.Parameters(new Vector4(2f, 0f, 0f, 0f));
        rig.Begin(target, s_blue);
        device.SetTexture(0, texture, sampler);
        rig.Draw(program);
        device.EndPass();
        var all = rig.Read(target, 4, 4);
        ((int)all.R).ShouldBeInRange(189, 193);
        ((int)all.G).ShouldBeInRange(62, 66);
    }

    [Fact]
    public void Buffer_Written_Between_Two_Draws_Of_A_Pass_Reaches_Each_Draw()
    {
        using var rig = new Rig();
        var device = rig.Device;
        var (_, target) = rig.Target();
        var program = rig.Program(SolidColor);
        rig.Begin(target, s_blue);
        rig.Parameters(s_red);
        device.SetViewport(0, 0, 4, Size);
        rig.Draw(program);

        // The same buffer gets new contents while the pass is open: the first draw keeps what it saw.
        rig.Parameters(s_green);
        device.SetViewport(4, 0, 4, Size);
        rig.Draw(program);
        device.EndPass();

        rig.Read(target, 1, 4).ShouldBe(((byte)255, (byte)0, (byte)0, (byte)255));
        rig.Read(target, 6, 4).ShouldBe(((byte)0, (byte)255, (byte)0, (byte)255));
    }

    [Fact]
    public void Draw_Constants_Reach_The_Vertex_Stage_And_Change_Between_Draws()
    {
        using var rig = new Rig();
        var device = rig.Device;
        var (_, target) = rig.Target();
        var program = rig.Program("void main() {\n    o_color = v_draw / 255.0;\n}\n");
        rig.Parameters();
        rig.Begin(target, s_blue);
        device.SetDrawConstants(10, 20, 30, 255);
        device.SetViewport(0, 0, 4, Size);
        rig.Draw(program);
        device.SetDrawConstants(200, 100, 50, 255);
        device.SetViewport(4, 0, 4, Size);
        rig.Draw(program);
        device.EndPass();

        rig.Read(target, 1, 4).ShouldBe(((byte)10, (byte)20, (byte)30, (byte)255));
        rig.Read(target, 6, 4).ShouldBe(((byte)200, (byte)100, (byte)50, (byte)255));
    }

    [Fact]
    public void Counter_Clockwise_Triangles_Face_Front()
    {
        using var rig = new Rig();
        var device = rig.Device;
        var (_, target) = rig.Target();

        // Red where the fragment faces front, green where it faces back.
        var program = rig.ProgramFrom(
            "layout(location = 0) in vec2 a_position;\nvoid main() {\n    gl_Position = vec4(a_position, 0.0, 1.0);\n}\n",
            "out vec4 o_color;\nvoid main() {\n    o_color = gl_FrontFacing ? vec4(1.0, 0.0, 0.0, 1.0) : vec4(0.0, 1.0, 0.0, 1.0);\n}\n");
        var vertices = rig.Own(device.CreateBuffer(BufferUsage.Vertex, 24, false));

        // Counter-clockwise as seen with +Y up, covering the whole target.
        device.WriteBuffer(vertices, 0, MemoryMarshal.AsBytes<float>([-1f, -1f, 3f, -1f, -1f, 3f]));
        var input = rig.Own(device.CreateVertexInput(new VertexInputDescriptor
        {
            Attributes = [new VertexAttributeBinding { Location = 0, BufferIndex = 0, Format = VertexFormat.Float2, Stride = 8 }],
            Buffers = [vertices],
        }));

        (byte R, byte G, byte B, byte A) Draw(CullMode cull, bool clockwiseIsFront)
        {
            rig.Begin(target, s_blue);
            device.SetProgram(program);
            device.SetRenderState(new RenderState { Cull = cull, FrontFaceClockwise = clockwiseIsFront, DepthCompare = CompareFunction.Always, ColorWrite = true });
            device.SetVertexInput(input);
            device.Draw(PrimitiveTopology.Triangles, 0, 3, 1);
            device.EndPass();
            return rig.Read(target, 4, 4);
        }

        Draw(CullMode.None, false).ShouldBe(((byte)255, (byte)0, (byte)0, (byte)255));
        Draw(CullMode.Back, false).ShouldBe(((byte)255, (byte)0, (byte)0, (byte)255));
        Draw(CullMode.Front, false).ShouldBe(((byte)0, (byte)0, (byte)255, (byte)255));
        Draw(CullMode.None, true).ShouldBe(((byte)0, (byte)255, (byte)0, (byte)255));
        Draw(CullMode.Back, true).ShouldBe(((byte)0, (byte)0, (byte)255, (byte)255));
    }

    [Fact]
    public void Blend_Modes_Combine_Source_And_Target_As_Documented()
    {
        using var rig = new Rig();
        var device = rig.Device;
        var (_, target) = rig.Target();
        var program = rig.Program(SolidColor);

        (byte R, byte G, byte B, byte A) Blend(BlendMode mode)
        {
            // Half-transparent (0.8, 0.4, 0.2) over an opaque (0.2, 0.4, 0.6).
            rig.Parameters(new Vector4(0.8f, 0.4f, 0.2f, 0.5f));
            rig.Begin(target, new Vector4(0.2f, 0.4f, 0.6f, 1f));
            var state = Flat;
            state.Blend = mode;
            rig.Draw(program, state);
            device.EndPass();
            return rig.Read(target, 4, 4);
        }

        static void Near((byte R, byte G, byte B, byte A) actual, float r, float g, float b, string mode)
        {
            ((int)actual.R).ShouldBeInRange((int)(r * 255f) - 2, (int)(r * 255f) + 2, mode);
            ((int)actual.G).ShouldBeInRange((int)(g * 255f) - 2, (int)(g * 255f) + 2, mode);
            ((int)actual.B).ShouldBeInRange((int)(b * 255f) - 2, (int)(b * 255f) + 2, mode);
        }

        Near(Blend(BlendMode.Opaque), 0.8f, 0.4f, 0.2f, "opaque");
        Near(Blend(BlendMode.Alpha), 0.5f, 0.4f, 0.4f, "alpha");
        Near(Blend(BlendMode.PremultipliedAlpha), 0.9f, 0.6f, 0.5f, "premultiplied");
        Near(Blend(BlendMode.Additive), 0.6f, 0.6f, 0.7f, "additive");
        Near(Blend(BlendMode.Multiply), 0.16f, 0.16f, 0.12f, "multiply");
        Near(Blend(BlendMode.Subtractive), 0.04f, 0.24f, 0.48f, "subtractive");
    }

    [Fact]
    public void Multisampled_Float_Target_Resolves_Whatever_The_Device_Does_With_It()
    {
        // OpenGL multisamples 32-bit float targets; WebGPU has no such textures and the device gives one sample. The
        // renderer blits either into the single-sampled picture. (OpenGL ES 3.0 does not promise these at all.)
        using var rig = new Rig();
        var device = rig.Device;
        if (!device.Capabilities.FloatRenderTargets || device.Capabilities.ShaderLanguage == ShaderLanguage.GlslEs300)
        {
            return;
        }

        var (_, multisampled) = rig.Target(TextureFormat.RGBA32F, samples: 4);
        var (_, resolved) = rig.Target(TextureFormat.RGBA32F);
        var program = rig.Program(SolidColor);
        rig.Parameters(new Vector4(4f, 0.5f, 0.25f, 1f));
        rig.Begin(multisampled, s_blue);
        rig.Draw(program);
        device.EndPass();
        device.Blit(multisampled, resolved, color: true, depth: false, linear: false);
        var pixel = new float[4];
        device.ReadPixelsFloat(resolved, 4, 4, 1, 1, pixel);
        pixel.ShouldBe([4f, 0.5f, 0.25f, 1f]);
    }

    [Fact]
    public void WebGpu_Device_Reports_A_Validation_Error_And_Goes_On()
    {
        // wgpu-native ends the process when an invalid command buffer is submitted; the device must not let one through.
        using var rig = new Rig();
        if (rig.Device.Capabilities.ShaderLanguage != ShaderLanguage.Glsl450)
        {
            return;
        }

        var device = rig.Device;
        var (_, target) = rig.Target();
        var program = rig.Program(SolidColor);

        // A uniform buffer smaller than the block the program reads.
        var small = rig.Own(device.CreateBuffer(BufferUsage.Uniform, 16, true, "too-small"));
        rig.Begin(target, s_blue);
        device.SetUniformBuffer(3, small, 0, 16);
        rig.Draw(program);
        device.EndPass();
        var errors = device.DrainErrors();
        errors.ShouldNotBeEmpty();

        // The device still draws.
        rig.Parameters(s_red);
        rig.Begin(target, s_blue);
        rig.Draw(program);
        device.EndPass();
        rig.Read(target, 4, 4).ShouldBe(((byte)255, (byte)0, (byte)0, (byte)255));
    }

    [Fact]
    public void Storage_Buffer_Filled_By_A_Compute_Program_Is_Drawn_As_Vertices()
    {
        using var rig = new Rig();
        var device = rig.Device;
        if (!device.Capabilities.ComputeShaders)
        {
            return;
        }

        // The kernel writes the corners of a triangle over the lower-left half of the target.
        using var compute = new GpuCompute(device, new GpuComputeDescriptor
        {
            Kernel = "    corners[index] = vec2(index == 1u ? 1.0 : -1.0, index == 2u ? 1.0 : -1.0);",
            Buffers = [new ComputeBufferLayout("corners", ComputeElement.Vec2)],
        });
        var corners = rig.Own(compute.CreateBuffer(0, 3));
        compute.Run(3, [corners]);

        var program = rig.ProgramFrom(
            "layout(location = 0) in vec2 a_position;\nvoid main() {\n    gl_Position = vec4(a_position, 0.0, 1.0);\n}\n",
            "out vec4 o_color;\nvoid main() {\n    o_color = vec4(1.0, 0.0, 0.0, 1.0);\n}\n");
        var input = rig.Own(device.CreateVertexInput(new VertexInputDescriptor
        {
            Attributes = [new VertexAttributeBinding { Location = 0, BufferIndex = 0, Format = VertexFormat.Float2, Stride = 8 }],
            Buffers = [corners],
        }));
        var (_, target) = rig.Target();
        rig.Begin(target, s_blue);
        device.SetProgram(program);
        device.SetRenderState(Flat);
        device.SetVertexInput(input);
        device.Draw(PrimitiveTopology.Triangles, 0, 3, 1);
        device.EndPass();
        rig.Read(target, 1, 1).ShouldBe(((byte)255, (byte)0, (byte)0, (byte)255));
        rig.Read(target, 6, 6).ShouldBe(((byte)0, (byte)0, (byte)255, (byte)255));
    }

    [Fact]
    public void WebGpu_Device_Takes_Hand_Written_Glsl450_With_Any_Block_Name()
    {
        // No rewriting by the engine here: the bindings are the ones the source declares (ShaderLanguage.Glsl450). The
        // device tells textures, samplers and uniform blocks apart by their type, whatever a block is called.
        using var rig = new Rig();
        if (rig.Device.Capabilities.ShaderLanguage != ShaderLanguage.Glsl450)
        {
            return;
        }

        const string Settings = "layout(std140, set = 0, binding = 3) uniform samplerSettings {\n    vec4 u_color;\n};\n";
        var program = rig.Own(rig.Device.CreateProgram(new ProgramDescriptor
        {
            VertexSource = "#version 450\n" + Settings
                + "void main() {\n    vec2 p = vec2(float((gl_VertexIndex << 1) & 2), float(gl_VertexIndex & 2));\n    gl_Position = vec4(p * 2.0 - 1.0, 0.0, 1.0);\n}\n",
            FragmentSource = "#version 450\n" + Settings + "layout(location = 0) out vec4 o_color;\nvoid main() {\n    o_color = u_color;\n}\n",
            Label = "hand-written",
        }));
        var (_, target) = rig.Target();
        rig.Parameters(s_green);
        rig.Begin(target, s_blue);
        rig.Draw(program);
        rig.Device.EndPass();
        rig.Read(target, 4, 4).ShouldBe(((byte)0, (byte)255, (byte)0, (byte)255));
    }

    [Fact]
    public void Surface_Of_The_WebGpu_Device_Is_Drawn_To_And_Read_Back()
    {
        // OpenGL's surface is the host's framebuffer, which an off-screen test context does not have.
        using var rig = new Rig();
        if (rig.Device.Capabilities.ShaderLanguage != ShaderLanguage.Glsl450)
        {
            return;
        }

        var device = rig.Device;
        device.SetSurface(0, Size, Size);
        var program = rig.Program(SolidColor);
        rig.Parameters(s_red);
        device.BeginPass(new RenderPassDescriptor { ClearColor = true, ClearColorValue = s_blue, ClearDepth = true, ClearDepthValue = 1f });
        device.SetViewport(0, 0, Size, 2);
        rig.Draw(program);
        device.EndPass();
        var pixels = new byte[Size * Size * 4];
        device.ReadPixels(null, 0, 0, Size, Size, pixels);
        pixels[0].ShouldBe((byte)255);
        pixels[(Size * Size * 4) - 2].ShouldBe((byte)255);
        pixels[(Size * Size * 4) - 4].ShouldBe((byte)0);

        // A multisampled framebuffer resolves into it, as the renderer does when it draws to the screen.
        var (_, multisampled) = rig.Target(samples: 4);
        rig.Parameters(s_green);
        rig.Begin(multisampled, s_blue);
        rig.Draw(program);
        device.EndPass();
        device.Blit(multisampled, null, color: true, depth: false, linear: false);
        device.ReadPixels(null, 0, 0, Size, Size, pixels);
        pixels[1].ShouldBe((byte)255);
        pixels[0].ShouldBe((byte)0);
    }
}
