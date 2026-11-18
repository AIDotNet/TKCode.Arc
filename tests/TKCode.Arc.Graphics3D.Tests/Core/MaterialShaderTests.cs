using System.Numerics;
using TKCode.Arc.Graphics3D.Rhi;
using TKCode.Arc.Graphics3D.Shading;

namespace TKCode.Arc.Graphics3D.Tests;

public class MaterialShaderTests
{
    private const ShaderFeatures Mesh = ShaderFeatures.HasNormal | ShaderFeatures.HasUv0;

    private static GeneratedShader Generate(Material material, ShaderFeatures features = Mesh, ShaderLanguage language = ShaderLanguage.Glsl330) =>
        ShaderGenerator.Generate(material, features, language, 11);

    [Fact]
    public void Equal_Materials_Generate_Equal_Programs()
    {
        var a = Generate(new StandardMaterial { Color = ColorF.FromHex(0xff0000), Roughness = 0.2f });
        var b = Generate(new StandardMaterial { Color = ColorF.FromHex(0x00ff00), Roughness = 0.9f, Metalness = 1f });
        b.Vertex.ShouldBe(a.Vertex);
        b.Fragment.ShouldBe(a.Fragment);

        // A structural difference gives another program.
        var c = Generate(new StandardMaterial { Map = TestScenes.Checker(8, 2) });
        c.Fragment.ShouldNotBe(a.Fragment);
        c.Textures.Length.ShouldBe(1);
    }

    [Fact]
    public void Property_Changes_Do_Not_Invalidate_But_Structure_Changes_Do()
    {
        var material = new StandardMaterial();
        var version = material.Version;
        material.Color = ColorF.FromHex(0x123456);
        material.Roughness = 0.3f;
        material.Opacity = 0.5f;
        material.Emissive = ColorF.Black;
        material.Version.ShouldBe(version);

        material.Map = TestScenes.Checker(8, 2);
        material.Version.ShouldBeGreaterThan(version);
        version = material.Version;
        material.Map = TestScenes.Checker(8, 2);
        material.Version.ShouldBe(version);
        material.Emissive = ColorF.White;
        material.Version.ShouldBeGreaterThan(version);
        version = material.Version;
        material.AlphaTest = 0.5f;
        material.Version.ShouldBeGreaterThan(version);
        version = material.Version;
        material.AlphaTest = 0.3f;
        material.Version.ShouldBe(version);
        material.ColorNode = Sl.Color(0xffffff);
        material.Version.ShouldBeGreaterThan(version);
    }

    [Fact]
    public void Each_Language_Gets_Its_Header()
    {
        var material = new StandardMaterial();
        Generate(material, Mesh, ShaderLanguage.Glsl330).Fragment.ShouldStartWith("#version 330 core");
        var es = Generate(material, Mesh, ShaderLanguage.GlslEs300);
        es.Fragment.ShouldStartWith("#version 300 es");
        es.Fragment.ShouldContain("precision highp float;");
        es.Vertex.ShouldStartWith("#version 300 es");
        var vulkan = Generate(material, Mesh, ShaderLanguage.Glsl450);
        vulkan.Vertex.ShouldStartWith("#version 450");
        vulkan.Fragment.ShouldStartWith("#version 450");
    }

    [Fact]
    public void Material_Block_Follows_Std140()
    {
        var a = Sl.Uniform(1f);
        var b = Sl.Uniform(new Vector3(1f, 2f, 3f));
        var c = Sl.Uniform(new Vector2(4f, 5f));
        var d = Sl.Uniform(new Vector4(6f, 7f, 8f, 9f));
        var e = Sl.Uniform(2f);
        var material = new NodeMaterial { Lit = false, ColorNode = b * a + Sl.Vec3(c, e) + d.Xyz };
        var shader = Generate(material);
        // Order of first use: b (vec3 at 0), a (float packs behind it at 3), c (vec2 at 4), e (float at 6), d (vec4 at 8);
        // the material's own opacity uniform comes first of all.
        var offsets = shader.Uniforms.Select((u, i) => (u, shader.UniformOffsets[i])).ToDictionary(x => x.u, x => x.Item2);
        (offsets[a] - offsets[b]).ShouldBe(3);
        (offsets[b] % 4).ShouldBe(0);
        (offsets[c] % 2).ShouldBe(0);
        (offsets[d] % 4).ShouldBe(0);
        (shader.UniformFloats % 4).ShouldBe(0);
        shader.UniformFloats.ShouldBeGreaterThanOrEqualTo(offsets[d] + 4);
    }

    [Fact]
    public void Nodes_Are_Typed()
    {
        Should.Throw<InvalidOperationException>(() => Sl.Uv + Sl.PositionWorld);
        Should.Throw<InvalidOperationException>(() => Sl.Uv.Z);
        Should.Throw<InvalidOperationException>(() => Sl.Select(Sl.LessThan(Sl.Time, 1f), Sl.Uv, Sl.PositionWorld));
        Should.Throw<ArgumentException>(() => Sl.Texture(TestScenes.Checker(8, 2), Sl.PositionWorld));
        (Sl.Uv * 2f).Type.ShouldBe(ShaderType.Vec2);
        Sl.Dot(Sl.NormalWorld, Sl.ViewDirection).Type.ShouldBe(ShaderType.Float);
        Sl.Mix(0f, Sl.PositionWorld, Sl.Time).Type.ShouldBe(ShaderType.Vec3);
        Sl.Vec4(Sl.PositionWorld, 1f).Type.ShouldBe(ShaderType.Vec4);
    }

    [Fact]
    public void A_Node_Is_Evaluated_Once_Per_Stage()
    {
        var noise = Sl.Fbm(Sl.PositionLocal * 3f);
        var material = new NodeMaterial { ColorNode = Sl.Vec3(noise), RoughnessNode = noise };
        var fragment = Generate(material).Fragment;
        var first = fragment.IndexOf("arcFbm3(", fragment.IndexOf("void main", StringComparison.Ordinal), StringComparison.Ordinal);
        first.ShouldBeGreaterThan(0);
        fragment.IndexOf("arcFbm3(", first + 1, StringComparison.Ordinal).ShouldBe(-1);
    }

    [Fact]
    public void Position_Nodes_Cannot_Use_World_Space()
    {
        var material = new NodeMaterial { PositionNode = Sl.PositionLocal + Sl.PositionWorld };
        Should.Throw<InvalidOperationException>(() => Generate(material)).Message.ShouldContain("position node");
    }

    [Fact]
    public void Features_Select_What_The_Program_Contains()
    {
        var material = new StandardMaterial();
        var plain = Generate(material);
        plain.Fragment.ShouldNotContain("u_shadowMap");
        plain.Fragment.ShouldNotContain("u_envMap");
        var full = Generate(material, Mesh | ShaderFeatures.Shadows | ShaderFeatures.Environment | ShaderFeatures.Fog | ShaderFeatures.SrgbOutput);
        full.UsesShadows.ShouldBeTrue();
        full.UsesEnvironment.ShouldBeTrue();
        full.Fragment.ShouldContain("arcShadow(");
        full.Fragment.ShouldContain("arcEnvSpecular(");
        full.Fragment.ShouldContain("arcFog(");
        full.Fragment.ShouldContain("arcLinearToSrgb(");

        // The depth pass has no color output and no lighting, whatever the scene has.
        var depth = Generate(material, Mesh | ShaderFeatures.DepthPass);
        depth.Fragment.ShouldNotContain("o_color");
        depth.Fragment.ShouldNotContain("arcLights");

        // An unlit material ignores shadows and the environment.
        var basic = Generate(new BasicMaterial(), Mesh | ShaderFeatures.Shadows | ShaderFeatures.Environment);
        basic.UsesShadows.ShouldBeFalse();
        basic.UsesEnvironment.ShouldBeFalse();

        var skinned = Generate(material, Mesh | ShaderFeatures.Skinning | ShaderFeatures.Morph | ShaderFeatures.Instancing);
        skinned.Vertex.ShouldContain("a_joints");
        skinned.Vertex.ShouldContain("arcMorph(");
        skinned.Vertex.ShouldContain("a_instanceMatrix");
    }

    [Fact]
    public void A_Texture_On_Channel_One_Samples_The_Second_Uv_Set()
    {
        var first = Generate(new BasicMaterial { Map = TestScenes.Checker(8, 2) }, Mesh | ShaderFeatures.HasUv1);
        first.Vertex.ShouldContain("vec3(uv0, 1.0)");
        var lightMap = TestScenes.Checker(8, 2);
        lightMap.Channel = 1;
        var second = Generate(new BasicMaterial { Map = lightMap }, Mesh | ShaderFeatures.HasUv1);
        second.Vertex.ShouldContain("vec3(uv1, 1.0)");
        second.Vertex.ShouldNotContain("vec3(uv0, 1.0)");
    }

    [Fact]
    public void Physical_Layer_Maps_Share_One_Sample_Per_Texture()
    {
        var packed = TestScenes.Checker(8, 2);
        var material = new PhysicalMaterial
        {
            Clearcoat = 1f,
            ClearcoatMap = packed,
            ClearcoatRoughnessMap = packed,
            Sheen = 1f,
            SheenColor = ColorF.White,
            SheenColorMap = TestScenes.Checker(8, 2),
        };
        var shader = Generate(material);
        shader.Textures.Length.ShouldBe(2);
        var main = shader.Fragment[shader.Fragment.IndexOf("void main", StringComparison.Ordinal)..];
        (main.Split("texture(u_tex").Length - 1).ShouldBe(2);

        // Without the layer the maps cost nothing.
        material.Clearcoat = 0f;
        Generate(material).Textures.Length.ShouldBe(1);
    }

    [Fact]
    public void Too_Many_Textures_Is_Reported()
    {
        ShaderNode color = Sl.Color(0xffffff);
        for (var i = 0; i < 4; i++)
        {
            color = color * Sl.Texture(TestScenes.Checker(8, 2)).Xyz;
        }

        var material = new NodeMaterial { Lit = false, ColorNode = color, Name = "many" };
        Should.Throw<InvalidOperationException>(() => ShaderGenerator.Generate(material, Mesh, ShaderLanguage.Glsl330, 3)).Message.ShouldContain("many");
    }

    [Fact]
    public void Uniform_Updates_Are_Versioned()
    {
        var u = Sl.Uniform(1f);
        var version = u.Version;
        u.Float = 1f;
        u.Version.ShouldBe(version);
        u.Float = 2f;
        u.Version.ShouldBe(version + 1);
        var color = Sl.Uniform(ColorF.White);
        color.Color = ColorF.FromHex(0xff0000);
        color.Vector3.X.ShouldBe(1f);
        color.Vector3.Y.ShouldBe(0f);
    }
}

public class GeometryAndImagingTests
{
    [Fact]
    public void Geometry_Bounds_Follow_Its_Positions()
    {
        var geometry = new BufferGeometry();
        geometry.SetAttribute(BufferGeometry.Position, VertexAttribute.Float([0f, 0f, 0f, 2f, 0f, 0f, 0f, 4f, 0f], 3));
        geometry.BoundingBox.Max.ShouldBe(new Vector3(2f, 4f, 0f));
        geometry.BoundingSphere.Center.ShouldBe(new Vector3(1f, 2f, 0f));
        geometry.VertexCount.ShouldBe(3);
        geometry.ElementCount.ShouldBe(3);

        var position = geometry.GetAttribute(BufferGeometry.Position)!;
        position.FloatData[3] = 10f;
        position.NeedsUpdate();
        geometry.InvalidateBounds();
        geometry.BoundingBox.Max.X.ShouldBe(10f);
        geometry.Center();
        geometry.BoundingBox.Center.ShouldBe(Vector3.Zero);
    }

    [Fact]
    public void Normals_And_Tangents_Are_Computed()
    {
        var geometry = new PlaneGeometry(2f, 2f);
        geometry.RemoveAttribute(BufferGeometry.Normal);
        geometry.ComputeVertexNormals();
        var normal = geometry.GetAttribute(BufferGeometry.Normal)!;
        for (var i = 0; i < normal.Count; i++)
        {
            Vector3.Distance(normal.GetVector3(i), Vector3.UnitZ).ShouldBeLessThan(1e-5f);
        }

        geometry.ComputeTangents();
        var tangent = geometry.GetAttribute(BufferGeometry.Tangent)!;
        tangent.ItemSize.ShouldBe(4);
        for (var i = 0; i < tangent.Count; i++)
        {
            // U grows along +X and V along +Y on a plane: tangent +X, right-handed.
            Vector3.Distance(tangent.GetVector3(i), Vector3.UnitX).ShouldBeLessThan(1e-5f);
            tangent.Get(i, 3).ShouldBe(1f);
        }
    }

    [Fact]
    public void Index_Size_Follows_The_Vertex_Count()
    {
        new SphereGeometry(1f, 16, 8).IndexFormat.ShouldBe(IndexFormat.UInt16);
        new SphereGeometry(1f, 400, 200).IndexFormat.ShouldBe(IndexFormat.UInt32);
    }

    [Fact]
    public void Signed_Attributes_Read_As_Signed()
    {
        var shorts = VertexAttribute.Short([-32767, 0, 32767, 16384], 4);
        shorts.Get(0, 0).ShouldBe(-1f);
        shorts.Get(0, 2).ShouldBe(1f);
        var bytes = VertexAttribute.SByte([-127, 127, 0, 64], 4);
        bytes.Get(0, 0).ShouldBe(-1f);
        bytes.Get(0, 1).ShouldBe(1f);
        VertexAttribute.UShort([65535, 0], 2, true).Get(0, 0).ShouldBe(1f);
        VertexAttribute.UShort([65535, 0], 2, false).Get(0, 0).ShouldBe(65535f);
    }

    [Fact]
    public void Png_Round_Trips()
    {
        var random = new Random(3);
        var pixels = new byte[37 * 21 * 4];
        random.NextBytes(pixels);
        var decoded = Png.Decode(Png.Encode(37, 21, pixels));
        decoded.Width.ShouldBe(37);
        decoded.Height.ShouldBe(21);
        ((byte[])decoded.Data).ShouldBe(pixels);
        Should.Throw<InvalidDataException>(() => Png.Decode([1, 2, 3]));
    }

    [Fact]
    public void Texture_Transform_Rows_Match_Offset_Repeat_And_Rotation()
    {
        var texture = new Texture { Repeat = new Vector2(2f, 3f), Offset = new Vector2(0.5f, 0.25f) };
        var (r0, r1) = texture.GetUvTransform();
        var uv = new Vector3(0.25f, 0.5f, 1f);
        Vector3.Dot(r0, uv).ShouldBe(1f, 1e-6f);
        Vector3.Dot(r1, uv).ShouldBe(1.75f, 1e-6f);
        texture.Rotation = MathF.PI / 2f;
        texture.Repeat = Vector2.One;
        texture.Offset = Vector2.Zero;
        texture.Center = new Vector2(0.5f, 0.5f);
        (r0, r1) = texture.GetUvTransform();
        // The center is the pivot: it stays where it is.
        Vector3.Dot(r0, new Vector3(0.5f, 0.5f, 1f)).ShouldBe(0.5f, 1e-6f);
        Vector3.Dot(r1, new Vector3(0.5f, 0.5f, 1f)).ShouldBe(0.5f, 1e-6f);
    }
}
