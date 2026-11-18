using TKCode.Arc.Graphics3D.Rhi;
using TKCode.Arc.Graphics3D.Shading;

namespace TKCode.Arc.Graphics3D.Tests;

/// <summary>GLSL 4.50 (the WebGPU backend's language) is the engine's one shader text with other declarations. These
/// tests pin which declarations, and that nothing else changes. No graphics device is needed.</summary>
public class ShaderDialectTests
{
    private const ShaderFeatures Everything = ShaderFeatures.HasNormal | ShaderFeatures.HasUv0 | ShaderFeatures.Skinning | ShaderFeatures.Instancing
        | ShaderFeatures.Shadows | ShaderFeatures.Environment | ShaderFeatures.Morph;

    private static Material TexturedMaterial() => new StandardMaterial { Map = TestScenes.Checker(8, 2) };

    private static GeneratedShader Generate(ShaderLanguage language, ShaderFeatures features = Everything) =>
        ShaderGenerator.Generate(TexturedMaterial(), features, language, 11);

    private static string[] Lines(string source) => source.Split('\n');

    [Fact]
    public void Every_Resource_Names_Its_Binding()
    {
        var shader = Generate(ShaderLanguage.Glsl450);

        // Uniform blocks: set 0, the slot as the binding.
        shader.Vertex.ShouldContain("layout(std140, set = 0, binding = 0) uniform ViewBlock {");
        shader.Fragment.ShouldContain("layout(std140, set = 0, binding = 1) uniform SceneBlock {");
        shader.Fragment.ShouldContain("layout(std140, set = 0, binding = 2) uniform MaterialBlock {");

        // Samplers: a texture at twice the unit and a sampler behind it, under the old name.
        shader.Vertex.ShouldContain("layout(set = 1, binding = 0) uniform texture2D u_frameData_t;\nlayout(set = 1, binding = 1) uniform sampler u_frameData_s;\n#define u_frameData sampler2D(u_frameData_t, u_frameData_s)\n");
        shader.Fragment.ShouldContain("layout(set = 1, binding = 2) uniform texture2D u_shadowMap_t;\nlayout(set = 1, binding = 3) uniform samplerShadow u_shadowMap_s;\n#define u_shadowMap sampler2DShadow(u_shadowMap_t, u_shadowMap_s)\n");
        shader.Fragment.ShouldContain("layout(set = 1, binding = 4) uniform textureCube u_envMap_t;");
        shader.Vertex.ShouldContain("layout(set = 1, binding = 6) uniform texture2D u_morphTex_t;");
        shader.Fragment.ShouldContain("layout(set = 1, binding = 10) uniform texture2D u_tex0_t;\nlayout(set = 1, binding = 11) uniform sampler u_tex0_s;\n");

        // The draw constants are a push constant block.
        shader.Vertex.ShouldContain("layout(push_constant) uniform DrawConstants {\n    ivec4 u_draw;\n};\n");

        // The instance matrix arrives as four vectors.
        shader.Vertex.ShouldContain("layout(location = 8) in vec4 a_instanceMatrix0;");
        shader.Vertex.ShouldContain("layout(location = 11) in vec4 a_instanceMatrix3;");
        shader.Vertex.ShouldContain("#define a_instanceMatrix mat4(a_instanceMatrix0, a_instanceMatrix1, a_instanceMatrix2, a_instanceMatrix3)");

        // Nothing is left that Vulkan-flavoured GLSL refuses.
        foreach (var source in new[] { shader.Vertex, shader.Fragment })
        {
            foreach (var line in Lines(source))
            {
                line.StartsWith("uniform ", StringComparison.Ordinal).ShouldBeFalse(line);
                line.StartsWith("in ", StringComparison.Ordinal).ShouldBeFalse(line);
                line.StartsWith("out ", StringComparison.Ordinal).ShouldBeFalse(line);
                line.StartsWith("flat ", StringComparison.Ordinal).ShouldBeFalse(line);
                line.Contains(" in mat4 ", StringComparison.Ordinal).ShouldBeFalse(line);
            }
        }
    }

    [Fact]
    public void Values_Between_The_Stages_Share_Locations()
    {
        var shader = Generate(ShaderLanguage.Glsl450);
        static Dictionary<string, string> Interface(string source, string direction) => Lines(source)
            .Where(line => line.StartsWith("layout(location = ", StringComparison.Ordinal) && line.Contains(") " + direction + " ", StringComparison.Ordinal)
                || line.Contains(") flat " + direction + " ", StringComparison.Ordinal))
            .ToDictionary(line => line[(line.LastIndexOf(' ') + 1)..], line => line[..line.IndexOf(')')]);

        var written = Interface(shader.Vertex, "out");
        var read = Interface(shader.Fragment, "in");
        read.Count.ShouldBeGreaterThan(3);
        written.Count.ShouldBe(read.Count);
        foreach (var (name, location) in read)
        {
            written[name].ShouldBe(location, name);
        }

        written.Values.Distinct().Count().ShouldBe(written.Count);
        shader.Fragment.ShouldContain("layout(location = 0) out vec4 o_color;");
        shader.Fragment.ShouldContain(") flat in vec4 v_objParams;");
    }

    [Fact]
    public void Only_Declarations_Differ_From_The_OpenGl_Text()
    {
        // Take the declarations and the preprocessor lines away, and both languages have the same statements.
        static List<string> Statements(string source)
        {
            var kept = new List<string>();
            var skip = 0;
            foreach (var line in Lines(source))
            {
                if (skip > 0)
                {
                    skip--;
                    continue;
                }

                if (line.StartsWith("layout(push_constant)", StringComparison.Ordinal))
                {
                    // The block's member and its closing brace.
                    skip = 2;
                    continue;
                }

                if (line.StartsWith('#') || line.StartsWith("precision ", StringComparison.Ordinal)
                    || line.StartsWith("uniform ", StringComparison.Ordinal) || line.StartsWith("layout(", StringComparison.Ordinal)
                    || line.StartsWith("in ", StringComparison.Ordinal) || line.StartsWith("out ", StringComparison.Ordinal)
                    || line.StartsWith("flat ", StringComparison.Ordinal))
                {
                    continue;
                }

                kept.Add(line);
            }

            return kept;
        }

        var gl = Generate(ShaderLanguage.Glsl330);
        var vulkan = Generate(ShaderLanguage.Glsl450);
        Statements(vulkan.Vertex).ShouldBe(Statements(gl.Vertex));
        Statements(vulkan.Fragment).ShouldBe(Statements(gl.Fragment));

        // And OpenGL's own two languages differ in the header only (the version and the default precisions).
        var es = Generate(ShaderLanguage.GlslEs300);
        Statements(es.Fragment).ShouldBe(Statements(gl.Fragment));
    }

    [Fact]
    public void Vertex_Stage_Reads_Textures_At_Level_Zero_And_Names_The_Indices_The_Old_Way()
    {
        var shader = Generate(ShaderLanguage.Glsl450);
        shader.Vertex.ShouldContain("#define gl_VertexID int(gl_VertexIndex)");
        shader.Vertex.ShouldContain("#define gl_InstanceID int(gl_InstanceIndex)");
        shader.Vertex.ShouldContain("#define texture(s, uv) textureLod(s, uv, 0.0)");
        shader.Fragment.ShouldNotContain("#define texture(");
    }

    [Fact]
    public void Post_Pass_Program_Is_Rewritten_Too()
    {
        var shader = ShaderGenerator.GeneratePostPass(Sl.Vec3(1f, 0.5f, 0.25f) * Sl.Texture(TestScenes.Checker(8, 2), PostPass.Uv).Xyz, ShaderLanguage.Glsl450, 11);
        shader.Vertex.ShouldContain("layout(location = 0) out vec2 v_uv0;");
        shader.Fragment.ShouldContain("layout(location = 0) in vec2 v_uv0;");
        shader.Fragment.ShouldContain("layout(set = 1, binding = 0) uniform texture2D u_input_t;");
        shader.Fragment.ShouldContain("layout(set = 1, binding = 10) uniform texture2D u_tex0_t;");
    }

    [Fact]
    public void A_Sampler_Without_A_Unit_Or_A_Value_Nobody_Writes_Is_An_Error()
    {
        var vertex = "#version 450\nout vec2 v_uv;\nvoid main() {\n}\n";
        var fragment = "#version 450\nuniform sampler2D u_missing;\nin vec2 v_uv;\nout vec4 o_color;\nvoid main() {\n}\n";
        Should.Throw<InvalidOperationException>(() => ShaderDialect.ToGlsl450(ref vertex, ref fragment, [], [])).Message.ShouldContain("u_missing");

        fragment = "#version 450\nin vec2 v_other;\nout vec4 o_color;\nvoid main() {\n}\n";
        Should.Throw<InvalidOperationException>(() => ShaderDialect.ToGlsl450(ref vertex, ref fragment, [], [])).Message.ShouldContain("v_other");

        fragment = "#version 450\nlayout(std140) uniform Unknown {\n    vec4 u_value;\n};\nvoid main() {\n}\n";
        Should.Throw<InvalidOperationException>(() => ShaderDialect.ToGlsl450(ref vertex, ref fragment, [], [])).Message.ShouldContain("Unknown");
    }

    [Fact]
    public void Clip_Depth_Of_A_Depth_Value_Follows_The_Device()
    {
        ShaderLibrary.ClipDepthOf(ClipDepthRange.NegativeOneToOne, "d").ShouldBe("d * 2.0 - 1.0");
        ShaderLibrary.ClipDepthOf(ClipDepthRange.ZeroToOne, "d").ShouldBe("d");
    }

    [Fact]
    public void Compute_Declarations_Use_The_Storage_Set()
    {
        ShaderDialect.StorageBuffer(1, "vec4", "velocity", readOnly: false)
            .ShouldBe("layout(std430, set = 2, binding = 1) buffer velocityBuffer {\n    vec4 velocity[];\n};\n");
        ShaderDialect.StorageBuffer(0, "mat4", "local", readOnly: true).ShouldStartWith("layout(std430, set = 2, binding = 0) readonly buffer localBuffer {");
        ShaderDialect.UniformBlock(4, "ComputeBlock", "    ivec4 u_range;\n")
            .ShouldBe("layout(std140, set = 0, binding = 4) uniform ComputeBlock {\n    ivec4 u_range;\n};\n");
    }
}
