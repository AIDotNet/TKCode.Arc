using System.Buffers.Binary;
using System.Numerics;
using System.Text;
using TKCode.Arc.Graphics3D.Rhi;

namespace TKCode.Arc.Graphics3D.Tests;

public class GltfLoaderTests
{
    private static void ShouldBeNear(Vector3 actual, Vector3 expected, float tolerance = 1e-4f) =>
        Vector3.Distance(actual, expected).ShouldBeLessThan(tolerance, $"{actual} should be near {expected}");

    // ---- Containers and buffers ----

    [Fact]
    public void Glb_With_An_Indexed_Triangle_Loads()
    {
        var b = new GltfBuilder();
        using var asset = GltfLoader.Load(b.Glb(b.Triangle()));

        asset.Warnings.ShouldBeEmpty();
        asset.Scenes.Count.ShouldBe(1);
        asset.Scene.Children.Count.ShouldBe(1);
        var mesh = asset.Scene.Children[0].ShouldBeOfType<Mesh>();
        mesh.Name.ShouldBe("tri");
        mesh.Geometry.Name.ShouldBe("triMesh");
        mesh.Geometry.VertexCount.ShouldBe(3);
        mesh.Geometry.IndexCount.ShouldBe(3);
        mesh.Geometry.Index.ShouldBeOfType<ushort[]>();
        mesh.Geometry.GetAttribute(BufferGeometry.Position)!.GetVector3(1).ShouldBe(new Vector3(1, 0, 0));
        asset.Geometries.ShouldHaveSingleItem().ShouldBeSameAs(mesh.Geometry);

        // No material: the glTF default, a white fully metallic and rough surface.
        var material = mesh.Material.ShouldBeOfType<StandardMaterial>();
        material.Metalness.ShouldBe(1f);
        material.Roughness.ShouldBe(1f);
        material.Color.ShouldBe(ColorF.White);
    }

    [Fact]
    public void Gltf_Json_With_A_Data_Uri_Buffer_Loads()
    {
        var b = new GltfBuilder();
        var body = b.Triangle();
        using var asset = GltfLoader.Load(b.Gltf(body));
        asset.Scene.Find<Mesh>()!.Geometry.VertexCount.ShouldBe(3);

        // A byte order mark in front of the JSON is fine.
        byte[] withBom = [0xEF, 0xBB, 0xBF, .. b.Gltf(body)];
        using var second = GltfLoader.Load(withBom);
        second.Scene.Find<Mesh>()!.Geometry.IndexCount.ShouldBe(3);
    }

    [Fact]
    public void External_Buffer_Comes_From_The_Resolver()
    {
        var b = new GltfBuilder();
        var json = Encoding.UTF8.GetBytes(b.Json(b.Triangle(), "my%20data.bin"));
        var asked = new List<string>();
        using var asset = GltfLoader.Load(json, uri =>
        {
            asked.Add(uri);
            return b.Buffer;
        });

        asked.ShouldBe(["my data.bin"]);
        asset.Scene.Find<Mesh>()!.Geometry.VertexCount.ShouldBe(3);

        Should.Throw<FileNotFoundException>(() => GltfLoader.Load(json)).Message.ShouldContain("my data.bin");
        Should.Throw<FileNotFoundException>(() => GltfLoader.Load(json, _ => null));
    }

    [Fact]
    public async Task File_With_A_Buffer_Next_To_It_Loads_From_A_Path()
    {
        var folder = Path.Combine(Path.GetTempPath(), "arc-gltf-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        try
        {
            var b = new GltfBuilder();
            var path = Path.Combine(folder, "model.gltf");
            await File.WriteAllTextAsync(path, b.Json(b.Triangle(), "model.bin"), TestContext.Current.CancellationToken);
            await File.WriteAllBytesAsync(Path.Combine(folder, "model.bin"), b.Buffer, TestContext.Current.CancellationToken);

            using var sync = GltfLoader.Load(path);
            sync.Scene.Find<Mesh>()!.Geometry.VertexCount.ShouldBe(3);
            using var async = await GltfLoader.LoadAsync(path, TestContext.Current.CancellationToken);
            async.Scene.Find<Mesh>()!.Geometry.IndexCount.ShouldBe(3);
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    [Fact]
    public async Task Async_Load_Of_Data_Gives_The_Same_Result_And_Honors_Cancellation()
    {
        var b = new GltfBuilder();
        var glb = b.Glb(b.Triangle());
        using var asset = await GltfLoader.LoadAsync(glb, cancellationToken: TestContext.Current.CancellationToken);
        asset.Scene.Find<Mesh>()!.Geometry.VertexCount.ShouldBe(3);

        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();
        await Should.ThrowAsync<OperationCanceledException>(() => GltfLoader.LoadAsync(glb, cancellationToken: cancelled.Token));
    }

    [Fact]
    public void Invalid_Containers_Are_Rejected()
    {
        Should.Throw<InvalidDataException>(() => GltfLoader.Load(new byte[] { 1, 2, 3, 4, 5 }));
        Should.Throw<InvalidDataException>(() => GltfLoader.Load(Encoding.UTF8.GetBytes("[1, 2]")));

        var b = new GltfBuilder();
        var glb = b.Glb(b.Triangle());
        var versionOne = (byte[])glb.Clone();
        BinaryPrimitives.WriteUInt32LittleEndian(versionOne.AsSpan(4), 1);
        Should.Throw<NotSupportedException>(() => GltfLoader.Load(versionOne)).Message.ShouldContain("version 1");

        var old = Encoding.UTF8.GetBytes("{\"asset\":{\"version\":\"1.0\"}}");
        Should.Throw<NotSupportedException>(() => GltfLoader.Load(old)).Message.ShouldContain("1.0");
    }

    [Fact]
    public void Accessors_That_Reach_Outside_Their_Data_Are_Rejected()
    {
        var b = new GltfBuilder();
        var view = b.View(new byte[24]);
        var position = b.Accessor(view, GltfBuilder.Float, "VEC3", 3);
        var body = $"\"scenes\":[{{\"nodes\":[0]}}],\"nodes\":[{{\"mesh\":0}}],\"meshes\":[{{\"primitives\":[{{\"attributes\":{{\"POSITION\":{position}}}}}]}}]";
        Should.Throw<InvalidDataException>(() => GltfLoader.Load(b.Glb(body))).Message.ShouldContain("accessor 0");

        var c = new GltfBuilder();
        var positions = c.Floats("VEC3", 0, 0, 0, 1, 0, 0, 0, 1, 0);
        var indices = c.UShorts("SCALAR", false, 0, 1, 3);
        body = $"\"scenes\":[{{\"nodes\":[0]}}],\"nodes\":[{{\"mesh\":0}}],\"meshes\":[{{\"name\":\"bad\",\"primitives\":[{{\"attributes\":{{\"POSITION\":{positions}}},\"indices\":{indices}}}]}}]";
        Should.Throw<InvalidDataException>(() => GltfLoader.Load(c.Glb(body))).Message.ShouldContain("'bad'");
    }

    // ---- Accessors ----

    [Fact]
    public void Sparse_Accessor_Replaces_Some_Elements()
    {
        var b = new GltfBuilder();
        var sparseIndices = b.View([1, 0, 2, 0]);
        var sparseValues = b.View(System.Runtime.InteropServices.MemoryMarshal.AsBytes<float>([5f, 6f, 7f, 8f, 9f, 10f]));
        // No buffer view: three zero positions, of which the second and third are replaced.
        var position = b.Accessor(-1, GltfBuilder.Float, "VEC3", 3, extra:
            $",\"sparse\":{{\"count\":2,\"indices\":{{\"bufferView\":{sparseIndices},\"componentType\":5123}},\"values\":{{\"bufferView\":{sparseValues}}}}}");
        var body = $"\"scenes\":[{{\"nodes\":[0]}}],\"nodes\":[{{\"mesh\":0}}],\"meshes\":[{{\"primitives\":[{{\"attributes\":{{\"POSITION\":{position}}}}}]}}]";
        using var asset = GltfLoader.Load(b.Glb(body));

        var attribute = asset.Scene.Find<Mesh>()!.Geometry.GetAttribute(BufferGeometry.Position)!;
        attribute.GetVector3(0).ShouldBe(Vector3.Zero);
        attribute.GetVector3(1).ShouldBe(new Vector3(5, 6, 7));
        attribute.GetVector3(2).ShouldBe(new Vector3(8, 9, 10));
    }

    [Fact]
    public void Interleaved_Buffer_View_Is_Split_By_Its_Stride()
    {
        var b = new GltfBuilder();
        // Per vertex: position (12 bytes) then UV (8 bytes).
        float[] vertices = [0, 0, 0, 0.1f, 0.2f, 1, 0, 0, 0.3f, 0.4f, 0, 1, 0, 0.5f, 0.6f];
        var view = b.View(System.Runtime.InteropServices.MemoryMarshal.AsBytes<float>(vertices), stride: 20);
        var position = b.Accessor(view, GltfBuilder.Float, "VEC3", 3);
        var uv = b.Accessor(view, GltfBuilder.Float, "VEC2", 3, byteOffset: 12);
        var body = $"\"scenes\":[{{\"nodes\":[0]}}],\"nodes\":[{{\"mesh\":0}}],\"meshes\":[{{\"primitives\":[{{\"attributes\":{{\"POSITION\":{position},\"TEXCOORD_0\":{uv}}}}}]}}]";
        using var asset = GltfLoader.Load(b.Glb(body));

        var geometry = asset.Scene.Find<Mesh>()!.Geometry;
        geometry.GetAttribute(BufferGeometry.Position)!.GetVector3(2).ShouldBe(new Vector3(0, 1, 0));
        geometry.GetAttribute(BufferGeometry.Uv)!.GetVector2(1).ShouldBe(new Vector2(0.3f, 0.4f));
        geometry.Index.ShouldBeNull();
    }

    [Fact]
    public void Quantized_Attributes_Keep_Their_Integer_Form_When_The_Gpu_Has_A_Format()
    {
        var b = new GltfBuilder();
        var position = b.Shorts("VEC3", true, 32767, 0, -32767, 0, 16384, 0, 0, 0, 0);
        var normal = b.Data<sbyte>([0, 0, 127, 0, 0, 127, 0, 0, 127], GltfBuilder.Byte, "VEC3", normalized: true);
        var uv = b.UShorts("VEC2", true, 0, 65535, 32768, 0, 65535, 65535);
        var color = b.UBytes("VEC4", true, 255, 0, 0, 255, 0, 255, 0, 255, 0, 0, 255, 128);
        var body = "\"extensionsUsed\":[\"KHR_mesh_quantization\"],\"extensionsRequired\":[\"KHR_mesh_quantization\"],"
            + $"\"scenes\":[{{\"nodes\":[0]}}],\"nodes\":[{{\"mesh\":0}}],\"meshes\":[{{\"primitives\":[{{\"attributes\":{{\"POSITION\":{position},\"NORMAL\":{normal},\"TEXCOORD_0\":{uv},\"COLOR_0\":{color}}}}}]}}]";
        using var asset = GltfLoader.Load(b.Glb(body));

        asset.Warnings.ShouldBeEmpty();
        var geometry = asset.Scene.Find<Mesh>()!.Geometry;
        var p = geometry.GetAttribute(BufferGeometry.Position)!;
        p.Format.ShouldBe(VertexFormat.Short3Norm);
        p.Normalized.ShouldBeTrue();
        p.Count.ShouldBe(3);
        ((short[])p.Data)[..6].ShouldBe([(short)32767, (short)0, (short)-32767, (short)0, (short)16384, (short)0]);
        ShouldBeNear(p.GetVector3(0), new Vector3(1, 0, -1));
        ShouldBeNear(p.GetVector3(1), new Vector3(0, 0.5f, 0), 1e-3f);
        var n = geometry.GetAttribute(BufferGeometry.Normal)!;
        n.Format.ShouldBe(VertexFormat.SByte3Norm);
        ((sbyte[])n.Data)[2].ShouldBe((sbyte)127);
        n.GetVector3(0).ShouldBe(new Vector3(0, 0, 1));
        geometry.GetAttribute(BufferGeometry.Uv)!.Format.ShouldBe(VertexFormat.UShort2Norm);
        geometry.GetAttribute(BufferGeometry.Uv)!.GetVector2(0).ShouldBe(new Vector2(0, 1));
        var c = geometry.GetAttribute(BufferGeometry.Color)!;
        c.Format.ShouldBe(VertexFormat.UByte4Norm);
        c.GetVector4(2).W.ShouldBe(128 / 255f, 1e-6f);
        // The bounds come from the normalized values: -1..1 for signed data.
        ShouldBeNear(geometry.BoundingBox.Max, new Vector3(1, 0.5f, 0), 1e-3f);
        ShouldBeNear(geometry.BoundingBox.Min, new Vector3(0, 0, -1), 1e-3f);
    }

    [Fact]
    public void Integer_Data_Without_A_Matching_Format_Becomes_Float()
    {
        var b = new GltfBuilder();
        // Unnormalized 16-bit positions (allowed by KHR_mesh_quantization): there is no such three-component format.
        var position = b.UShorts("VEC3", false, 0, 0, 0, 10, 0, 0, 0, 20, 0);
        var body = "\"extensionsUsed\":[\"KHR_mesh_quantization\"],"
            + $"\"scenes\":[{{\"nodes\":[0]}}],\"nodes\":[{{\"mesh\":0}}],\"meshes\":[{{\"primitives\":[{{\"attributes\":{{\"POSITION\":{position}}}}}]}}]";
        using var asset = GltfLoader.Load(b.Glb(body));

        var p = asset.Scene.Find<Mesh>()!.Geometry.GetAttribute(BufferGeometry.Position)!;
        p.Format.ShouldBe(VertexFormat.Float3);
        p.GetVector3(2).ShouldBe(new Vector3(0, 20, 0));
    }

    [Fact]
    public void Thirty_Two_Bit_Indices_Are_Kept_Only_When_Needed()
    {
        var b = new GltfBuilder();
        var position = b.Floats("VEC3", 0, 0, 0, 1, 0, 0, 0, 1, 0);
        var index = b.UInts("SCALAR", 2, 1, 0);
        var body = $"\"scenes\":[{{\"nodes\":[0]}}],\"nodes\":[{{\"mesh\":0}}],\"meshes\":[{{\"primitives\":[{{\"attributes\":{{\"POSITION\":{position}}},\"indices\":{index}}}]}}]";
        using var asset = GltfLoader.Load(b.Glb(body));

        var geometry = asset.Scene.Find<Mesh>()!.Geometry;
        geometry.Index.ShouldBeOfType<ushort[]>();
        geometry.GetIndex(0).ShouldBe(2);
    }

    // ---- Meshes ----

    [Fact]
    public void Primitive_Modes_Become_The_Matching_Objects()
    {
        var b = new GltfBuilder();
        var position = b.Floats("VEC3", 0, 0, 0, 1, 0, 0, 0, 1, 0, 1, 1, 0);
        var primitives = string.Join(',', new[] { 0, 1, 2, 3, 5, 6 }.Select(mode => $"{{\"attributes\":{{\"POSITION\":{position}}},\"mode\":{mode}}}"));
        var body = $"\"scenes\":[{{\"nodes\":[0]}}],\"nodes\":[{{\"mesh\":0,\"name\":\"shapes\"}}],\"meshes\":[{{\"primitives\":[{primitives}]}}]";
        using var asset = GltfLoader.Load(b.Glb(body));

        var group = asset.Scene.Children[0].ShouldBeOfType<Group>();
        group.Name.ShouldBe("shapes");
        group.Children.Count.ShouldBe(6);
        var points = group.Children[0].ShouldBeOfType<Points>();
        points.Material.ShouldBeOfType<PointsMaterial>().SizeAttenuation.ShouldBeFalse();
        group.Children[1].ShouldBeOfType<LineSegments>().Material.ShouldBeOfType<LineMaterial>();

        // A line loop is a strip that returns to its first vertex.
        var loop = group.Children[2].ShouldBeOfType<Line>();
        loop.Geometry.IndexCount.ShouldBe(5);
        loop.Geometry.GetIndex(4).ShouldBe(0);
        group.Children[3].ShouldBeOfType<Line>().Geometry.Index.ShouldBeNull();

        var strip = group.Children[4].ShouldBeOfType<Mesh>().Geometry;
        Enumerable.Range(0, strip.IndexCount).Select(strip.GetIndex).ShouldBe([0, 1, 2, 2, 1, 3]);
        var fan = group.Children[5].ShouldBeOfType<Mesh>().Geometry;
        Enumerable.Range(0, fan.IndexCount).Select(fan.GetIndex).ShouldBe([0, 1, 2, 0, 2, 3]);
    }

    [Fact]
    public void Nodes_That_Instance_One_Mesh_Share_Its_Geometry_And_Material()
    {
        var b = new GltfBuilder();
        var position = b.Floats("VEC3", 0, 0, 0, 1, 0, 0, 0, 1, 0);
        var body = "\"scenes\":[{\"nodes\":[0,1]}],\"nodes\":[{\"mesh\":0,\"name\":\"a\"},{\"mesh\":0,\"name\":\"b\",\"translation\":[5,0,0]}],"
            + $"\"meshes\":[{{\"primitives\":[{{\"attributes\":{{\"POSITION\":{position}}},\"material\":0}}]}}],\"materials\":[{{\"name\":\"shared\"}}]";
        using var asset = GltfLoader.Load(b.Glb(body));

        var first = asset.Scene.Children[0].ShouldBeOfType<Mesh>();
        var second = asset.Scene.Children[1].ShouldBeOfType<Mesh>();
        first.ShouldNotBeSameAs(second);
        second.Geometry.ShouldBeSameAs(first.Geometry);
        second.Material.ShouldBeSameAs(first.Material);
        second.Material.Name.ShouldBe("shared");
        asset.Geometries.Count.ShouldBe(1);
        asset.Materials.Count.ShouldBe(1);
    }

    [Fact]
    public void Morph_Targets_And_Default_Weights_Are_Loaded()
    {
        var b = new GltfBuilder();
        var position = b.Floats("VEC3", 0, 0, 0, 1, 0, 0, 0, 1, 0);
        var normal = b.Floats("VEC3", 0, 0, 1, 0, 0, 1, 0, 0, 1);
        var up = b.Floats("VEC3", 0, 1, 0, 0, 1, 0, 0, 1, 0);
        var tilt = b.Floats("VEC3", 0, 0.5f, 0, 0, 0.5f, 0, 0, 0.5f, 0);
        var wide = b.Floats("VEC3", 2, 0, 0, 2, 0, 0, 2, 0, 0);
        var body = "\"scenes\":[{\"nodes\":[0]}],\"nodes\":[{\"mesh\":0}],"
            + $"\"meshes\":[{{\"weights\":[0.25,0.75],\"extras\":{{\"targetNames\":[\"up\",\"wide\"]}},\"primitives\":[{{\"attributes\":{{\"POSITION\":{position},\"NORMAL\":{normal}}},"
            + $"\"targets\":[{{\"POSITION\":{up},\"NORMAL\":{tilt}}},{{\"POSITION\":{wide},\"NORMAL\":{tilt}}}]}}]}}]";
        using var asset = GltfLoader.Load(b.Glb(body));

        var mesh = asset.Scene.Find<Mesh>()!;
        mesh.MorphWeights.ShouldBe([0.25f, 0.75f]);
        var targets = mesh.Geometry.MorphTargets;
        targets.Count.ShouldBe(2);
        targets[0].Name.ShouldBe("up");
        targets[1].Name.ShouldBe("wide");
        targets[1].PositionDeltas[..3].ShouldBe([2f, 0f, 0f]);
        targets[0].NormalDeltas.ShouldNotBeNull()[1].ShouldBe(0.5f);
    }

    [Fact]
    public void Unsupported_Required_Extensions_And_Damaged_Draco_Data_Are_Refused_By_Name()
    {
        var b = new GltfBuilder();
        var required = "\"extensionsRequired\":[\"EXT_texture_webp\"]," + b.Triangle();
        Should.Throw<NotSupportedException>(() => GltfLoader.Load(b.Glb(required))).Message.ShouldContain("EXT_texture_webp");

        // A primitive whose Draco buffer is not Draco data: the mesh is named, and nothing is read out of bounds.
        var c = new GltfBuilder();
        var garbage = c.View([1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12]);
        var damaged = c.Triangle(primitiveExtra: $",\"extensions\":{{\"KHR_draco_mesh_compression\":{{\"bufferView\":{garbage},\"attributes\":{{\"POSITION\":0}}}}}}");
        Should.Throw<InvalidDataException>(() => GltfLoader.Load(c.Glb(damaged))).Message.ShouldContain("Draco");
    }

    [Fact]
    public void Unknown_Optional_Extensions_And_Attributes_Become_Warnings()
    {
        var b = new GltfBuilder();
        var position = b.Floats("VEC3", 0, 0, 0, 1, 0, 0, 0, 1, 0);
        var body = "\"extensionsUsed\":[\"EXT_made_up\"],\"scenes\":[{\"nodes\":[0]}],\"nodes\":[{\"mesh\":0}],"
            + $"\"meshes\":[{{\"primitives\":[{{\"attributes\":{{\"POSITION\":{position},\"TEXCOORD_5\":{position},\"_CUSTOM\":{position}}}}}]}}]";
        using var asset = GltfLoader.Load(b.Glb(body));

        asset.Warnings.Count.ShouldBe(2);
        asset.Warnings.ShouldContain(w => w.Contains("EXT_made_up"));
        asset.Warnings.ShouldContain(w => w.Contains("TEXCOORD_5"));
    }

    // ---- Materials ----

    private static string MaterialScene(GltfBuilder b, string materials, int count, string attributes = "")
    {
        var position = b.Floats("VEC3", 0, 0, 0, 1, 0, 0, 0, 1, 0);
        var primitives = string.Join(',', Enumerable.Range(0, count).Select(i => $"{{\"attributes\":{{\"POSITION\":{position}{attributes}}},\"material\":{i}}}"));
        return $"\"scenes\":[{{\"nodes\":[0]}}],\"nodes\":[{{\"mesh\":0}}],\"meshes\":[{{\"primitives\":[{primitives},{{\"attributes\":{{\"POSITION\":{position}}}}}]}}],\"materials\":[{materials}]";
    }

    [Fact]
    public void Factors_Alpha_Modes_And_Sides_Map_To_Material_Properties()
    {
        var b = new GltfBuilder();
        var materials = """
            {"name":"opaque","pbrMetallicRoughness":{"baseColorFactor":[0.5,0.25,0.125,0.5],"metallicFactor":0.3,"roughnessFactor":0.6},"emissiveFactor":[1,0.5,0.25]},
            {"name":"mask","alphaMode":"MASK","alphaCutoff":0.3,"doubleSided":true},
            {"name":"maskDefault","alphaMode":"MASK"},
            {"name":"blend","alphaMode":"BLEND","pbrMetallicRoughness":{"baseColorFactor":[1,1,1,0.4]}}
            """;
        using var asset = GltfLoader.Load(b.Glb(MaterialScene(b, materials, 4)));

        var group = asset.Scene.Children[0];
        var opaque = ((Mesh)group.Children[0]).Material.ShouldBeOfType<StandardMaterial>();
        opaque.Name.ShouldBe("opaque");
        opaque.Color.ShouldBe(new ColorF(0.5f, 0.25f, 0.125f));
        opaque.Opacity.ShouldBe(0.5f);
        opaque.Metalness.ShouldBe(0.3f);
        opaque.Roughness.ShouldBe(0.6f);
        opaque.Emissive.ShouldBe(new ColorF(1f, 0.5f, 0.25f));
        opaque.EmissiveIntensity.ShouldBe(1f);
        opaque.Transparent.ShouldBeFalse();
        opaque.AlphaTest.ShouldBe(0f);
        opaque.Side.ShouldBe(Side.Front);

        var mask = ((Mesh)group.Children[1]).Material;
        mask.AlphaTest.ShouldBe(0.3f);
        mask.Side.ShouldBe(Side.Double);
        mask.Transparent.ShouldBeFalse();
        ((Mesh)group.Children[2]).Material.AlphaTest.ShouldBe(0.5f);

        var blend = ((Mesh)group.Children[3]).Material;
        blend.Transparent.ShouldBeTrue();
        blend.DepthWrite.ShouldBeFalse();
        blend.Opacity.ShouldBe(0.4f);
        blend.AlphaTest.ShouldBe(0f);
    }

    [Fact]
    public void Material_Extensions_Choose_The_Material_Class_And_Set_Its_Layers()
    {
        var b = new GltfBuilder();
        var materials = """
            {"name":"unlit","extensions":{"KHR_materials_unlit":{}},"pbrMetallicRoughness":{"baseColorFactor":[1,0,0,1]}},
            {"name":"glow","emissiveFactor":[1,1,1],"extensions":{"KHR_materials_emissive_strength":{"emissiveStrength":4}}},
            {"name":"physical","extensions":{
              "KHR_materials_clearcoat":{"clearcoatFactor":0.8,"clearcoatRoughnessFactor":0.1,"clearcoatTexture":{"index":0}},
              "KHR_materials_sheen":{"sheenColorFactor":[0.1,0.2,0.3],"sheenRoughnessFactor":0.4},
              "KHR_materials_ior":{"ior":1.33},
              "KHR_materials_specular":{"specularFactor":0.5,"specularColorFactor":[1,0.5,0.25]},
              "KHR_materials_transmission":{"transmissionFactor":0.9},
              "KHR_materials_volume":{"thicknessFactor":2,"attenuationDistance":5,"attenuationColor":[0.9,0.8,0.7]}}}
            """;
        using var asset = GltfLoader.Load(b.Glb(MaterialScene(b, materials, 3)));

        var group = asset.Scene.Children[0];
        var unlit = ((Mesh)group.Children[0]).Material.ShouldBeOfType<BasicMaterial>();
        unlit.Color.ShouldBe(new ColorF(1f, 0f, 0f));

        var glow = ((Mesh)group.Children[1]).Material.ShouldBeOfType<StandardMaterial>();
        glow.EmissiveIntensity.ShouldBe(4f);

        var physical = ((Mesh)group.Children[2]).Material.ShouldBeOfType<PhysicalMaterial>();
        physical.Clearcoat.ShouldBe(0.8f);
        physical.ClearcoatRoughness.ShouldBe(0.1f);
        physical.Sheen.ShouldBe(1f);
        physical.SheenColor.ShouldBe(new ColorF(0.1f, 0.2f, 0.3f));
        physical.SheenRoughness.ShouldBe(0.4f);
        physical.Ior.ShouldBe(1.33f);
        physical.SpecularIntensity.ShouldBe(0.5f);
        physical.SpecularColor.ShouldBe(new ColorF(1f, 0.5f, 0.25f));
        physical.Transmission.ShouldBe(0.9f);
        physical.Thickness.ShouldBe(2f);
        physical.AttenuationDistance.ShouldBe(5f);
        physical.AttenuationColor.ShouldBe(new ColorF(0.9f, 0.8f, 0.7f));

        // The clear coat names a texture this file does not have: said once, and the factor still applies.
        asset.Warnings.ShouldHaveSingleItem().ShouldContain("Texture 0 does not exist");
        physical.ClearcoatMap.ShouldBeNull();
    }

    [Fact]
    public void Animation_Pointers_Drive_Materials_Lights_Cameras_And_Texture_Transforms()
    {
        var b = new GltfBuilder();
        var png = Png.Encode(1, 1, [255, 255, 255, 255]);
        var image = b.View(png);
        var position = b.Floats("VEC3", 0, 0, 0, 1, 0, 0, 0, 1, 0);
        var uv = b.Floats("VEC2", 0, 0, 1, 0, 0, 1);
        var times = b.Floats("SCALAR", 0, 2);
        var colors = b.Floats("VEC4", 1, 0, 0, 1, 0, 0, 1, 0.5f);
        var scalars = b.Floats("SCALAR", 1, 3);
        var pairs = b.Floats("VEC2", 0, 0, 1, 0.5f);
        var angles = b.Floats("SCALAR", 0.5f, 1f);
        var moves = b.Floats("VEC3", 0, 0, 0, 0, 4, 0);
        string Channel(int sampler, string pointer) =>
            $"{{\"sampler\":{sampler},\"target\":{{\"path\":\"pointer\",\"extensions\":{{\"KHR_animation_pointer\":{{\"pointer\":\"{pointer}\"}}}}}}}}";
        var body = "\"extensionsUsed\":[\"KHR_animation_pointer\",\"KHR_lights_punctual\",\"KHR_materials_emissive_strength\",\"KHR_texture_transform\"],"
            + "\"extensions\":{\"KHR_lights_punctual\":{\"lights\":[{\"type\":\"point\",\"intensity\":1}]}},"
            + "\"cameras\":[{\"type\":\"perspective\",\"perspective\":{\"yfov\":0.5,\"znear\":0.1}}],"
            + $"\"images\":[{{\"bufferView\":{image},\"mimeType\":\"image/png\"}}],\"textures\":[{{\"source\":0}}],"
            + "\"materials\":[{\"pbrMetallicRoughness\":{\"baseColorTexture\":{\"index\":0}},\"alphaMode\":\"BLEND\"}],"
            + "\"scenes\":[{\"nodes\":[0,1,2]}],"
            + "\"nodes\":[{\"name\":\"mesh\",\"mesh\":0},{\"name\":\"lamp\",\"extensions\":{\"KHR_lights_punctual\":{\"light\":0}}},{\"name\":\"eye\",\"camera\":0}],"
            + $"\"meshes\":[{{\"primitives\":[{{\"attributes\":{{\"POSITION\":{position},\"TEXCOORD_0\":{uv}}},\"material\":0}}]}}],"
            + "\"animations\":[{\"name\":\"all\",\"channels\":["
            + Channel(0, "/materials/0/pbrMetallicRoughness/baseColorFactor") + ","
            + Channel(1, "/materials/0/extensions/KHR_materials_emissive_strength/emissiveStrength") + ","
            + Channel(2, "/materials/0/pbrMetallicRoughness/baseColorTexture/extensions/KHR_texture_transform/offset") + ","
            + Channel(1, "/extensions/KHR_lights_punctual/lights/0/intensity") + ","
            + Channel(3, "/cameras/0/perspective/yfov") + ","
            + Channel(4, "/nodes/0/translation") + ","
            + Channel(1, "/materials/0/somethingElse") + "],"
            + $"\"samplers\":[{{\"input\":{times},\"output\":{colors}}},{{\"input\":{times},\"output\":{scalars}}},{{\"input\":{times},\"output\":{pairs}}},"
            + $"{{\"input\":{times},\"output\":{angles}}},{{\"input\":{times},\"output\":{moves}}}]}}]";
        using var asset = GltfLoader.Load(b.Glb(body));

        // What cannot be animated is named; everything else became a track.
        asset.Warnings.ShouldHaveSingleItem().ShouldContain("/materials/0/somethingElse");
        var clip = asset.Animations.ShouldHaveSingleItem();
        clip.Tracks.Length.ShouldBe(6);
        var node = asset.Scene.Find("mesh")!;
        var mesh = node as Mesh ?? (Mesh)node.Children[0];
        var material = mesh.Material.ShouldBeOfType<StandardMaterial>();
        var light = asset.Lights.ShouldHaveSingleItem();
        var camera = asset.Cameras.ShouldHaveSingleItem().ShouldBeOfType<PerspectiveCamera>();

        var mixer = new AnimationMixer();
        mixer.ClipAction(clip).Play();
        mixer.SetTime(1f);

        // Halfway between the two keys.
        material.Color.R.ShouldBe(0.5f, 1e-4f);
        material.Color.B.ShouldBe(0.5f, 1e-4f);
        material.Opacity.ShouldBe(0.75f, 1e-4f);
        material.EmissiveIntensity.ShouldBe(2f, 1e-4f);
        material.Map.ShouldNotBeNull().Offset.ShouldBe(new Vector2(0.5f, 0.25f));
        light.Intensity.ShouldBe(2f, 1e-4f);
        camera.Fov.ShouldBe(MathUtil.ToDegrees(0.75f), 1e-3f);
        asset.Scene.Find("mesh")!.Position.Y.ShouldBe(2f, 1e-4f);
    }

    [Fact]
    public void Iridescence_And_Anisotropy_Extensions_Set_The_Physical_Layers()
    {
        var b = new GltfBuilder();
        var materials = """
            {"name":"film","extensions":{"KHR_materials_iridescence":{"iridescenceFactor":0.7,"iridescenceIor":1.8,"iridescenceThicknessMinimum":150,"iridescenceThicknessMaximum":650}}},
            {"name":"filmDefaults","extensions":{"KHR_materials_iridescence":{"iridescenceFactor":1}}},
            {"name":"brushed","extensions":{"KHR_materials_anisotropy":{"anisotropyStrength":0.6,"anisotropyRotation":0.5}}}
            """;
        using var asset = GltfLoader.Load(b.Glb("\"extensionsUsed\":[\"KHR_materials_iridescence\",\"KHR_materials_anisotropy\"]," + MaterialScene(b, materials, 3)));
        asset.Warnings.ShouldBeEmpty();

        var group = asset.Scene.Children[0];
        var film = ((Mesh)group.Children[0]).Material.ShouldBeOfType<PhysicalMaterial>();
        film.Iridescence.ShouldBe(0.7f);
        film.IridescenceIor.ShouldBe(1.8f);
        film.IridescenceThicknessRange.ShouldBe(new Vector2(150f, 650f));

        var defaults = ((Mesh)group.Children[1]).Material.ShouldBeOfType<PhysicalMaterial>();
        defaults.IridescenceIor.ShouldBe(1.3f);
        defaults.IridescenceThicknessRange.ShouldBe(new Vector2(100f, 400f));

        // The triangle has no tangents: its frame comes from derivatives, whose bitangent is mirrored, so the angle
        // turns the other way.
        var brushed = ((Mesh)group.Children[2]).Material.ShouldBeOfType<PhysicalMaterial>();
        brushed.Anisotropy.ShouldBe(0.6f);
        brushed.AnisotropyRotation.ShouldBe(-0.5f);
    }

    private static string TextureScene(GltfBuilder b, string material, string attributes = "", string secondPrimitiveAttributes = "")
    {
        // 2 x 2: red, green / blue, white.
        var png = Png.Encode(2, 2, [255, 0, 0, 255, 0, 255, 0, 255, 0, 0, 255, 255, 255, 255, 255, 255]);
        var image = b.View(png);
        var position = b.Floats("VEC3", 0, 0, 0, 1, 0, 0, 0, 1, 0);
        var tangent = b.Floats("VEC4", 1, 0, 0, 1, 1, 0, 0, 1, 1, 0, 0, 1);
        var normal = b.Floats("VEC3", 0, 0, 1, 0, 0, 1, 0, 0, 1);
        var second = secondPrimitiveAttributes.Replace("{T}", tangent.ToString()).Replace("{N}", normal.ToString());
        return "\"scenes\":[{\"nodes\":[0]}],\"nodes\":[{\"mesh\":0}],"
            + $"\"meshes\":[{{\"primitives\":[{{\"attributes\":{{\"POSITION\":{position}{attributes}}},\"material\":0}},{{\"attributes\":{{\"POSITION\":{position}{second}}},\"material\":0}}]}}],"
            + $"\"images\":[{{\"bufferView\":{image},\"mimeType\":\"image/png\",\"name\":\"quad\"}}],"
            + "\"samplers\":[{\"magFilter\":9728,\"minFilter\":9729,\"wrapS\":33071,\"wrapT\":33648}],"
            + "\"textures\":[{\"source\":0,\"sampler\":0,\"name\":\"pixelated\"},{\"source\":0}],"
            + $"\"materials\":[{material}]";
    }

    [Fact]
    public void Textures_Get_Their_Color_Space_Sampler_And_Share_By_Use()
    {
        var b = new GltfBuilder();
        var material = """
            {"pbrMetallicRoughness":{"baseColorTexture":{"index":0},"metallicRoughnessTexture":{"index":1}},
             "normalTexture":{"index":1,"scale":0.5},
             "occlusionTexture":{"index":1,"texCoord":1,"strength":0.7},
             "emissiveFactor":[1,1,1],
             "emissiveTexture":{"index":0,"extensions":{"KHR_texture_transform":{"offset":[0.5,0.25],"scale":[2,2],"rotation":0.3}}}}
            """;
        using var asset = GltfLoader.Load(b.Glb(TextureScene(b, material, secondPrimitiveAttributes: ",\"NORMAL\":{N},\"TANGENT\":{T}")));

        asset.Warnings.ShouldBeEmpty();
        var group = asset.Scene.Children[0];
        var m = ((Mesh)group.Children[0]).Material.ShouldBeOfType<StandardMaterial>();
        var map = m.Map.ShouldNotBeNull();
        map.Name.ShouldBe("pixelated");
        map.ColorSpace.ShouldBe(TextureColorSpace.Srgb);
        map.FlipY.ShouldBeFalse();
        map.MagFilter.ShouldBe(TextureFilter.Nearest);
        map.MinFilter.ShouldBe(TextureFilter.Linear);
        map.MipFilter.ShouldBe(MipFilter.None);
        map.GenerateMipmaps.ShouldBeFalse();
        map.WrapU.ShouldBe(TextureWrap.ClampToEdge);
        map.WrapV.ShouldBe(TextureWrap.MirroredRepeat);
        map.Image.ShouldNotBeNull().Width.ShouldBe(2);
        RenderHarness.Pixel(map.Image!, 1, 0).ShouldBe(((byte)0, (byte)255, (byte)0, (byte)255));

        // One glTF texture used three times as linear data: one Texture object, with the default sampler.
        var data = m.RoughnessMap.ShouldNotBeNull();
        m.MetalnessMap.ShouldBeSameAs(data);
        m.NormalMap.ShouldBeSameAs(data);

        // The occlusion texture names the second UV set: its own Texture object over the same pixels, on channel 1.
        var ao = m.AoMap.ShouldNotBeNull();
        ao.ShouldNotBeSameAs(data);
        ao.Image.ShouldBeSameAs(data.Image);
        ao.Channel.ShouldBe(1);
        data.Channel.ShouldBe(0);
        data.ColorSpace.ShouldBe(TextureColorSpace.Linear);
        data.MagFilter.ShouldBe(TextureFilter.Linear);
        data.MipFilter.ShouldBe(MipFilter.Linear);
        data.GenerateMipmaps.ShouldBeTrue();
        data.WrapU.ShouldBe(TextureWrap.Repeat);
        m.AoMapIntensity.ShouldBe(0.7f);

        // The same glTF texture with a transform is its own Texture over the same pixels.
        var emissive = m.EmissiveMap.ShouldNotBeNull();
        emissive.ShouldNotBeSameAs(map);
        emissive.Image.ShouldBeSameAs(map.Image);
        emissive.Offset.ShouldBe(new Vector2(0.5f, 0.25f));
        emissive.Repeat.ShouldBe(new Vector2(2f, 2f));
        emissive.Rotation.ShouldBe(0.3f);
        map.Repeat.ShouldBe(Vector2.One);
        asset.Textures.Count.ShouldBe(4);

        // Without tangents the green axis of the normal map is flipped; with tangents it is not. That makes two
        // materials from one glTF material.
        m.NormalScale.ShouldBe(new Vector2(0.5f, -0.5f));
        var withTangents = ((Mesh)group.Children[1]).Material.ShouldBeOfType<StandardMaterial>();
        withTangents.ShouldNotBeSameAs(m);
        withTangents.NormalScale.ShouldBe(new Vector2(0.5f, 0.5f));
        withTangents.NormalMap.ShouldBeSameAs(data);
        asset.Materials.Count.ShouldBe(2);
    }

    [Fact]
    public void Any_Map_Can_Use_The_Second_Uv_Set()
    {
        var b = new GltfBuilder();
        using var asset = GltfLoader.Load(b.Glb(TextureScene(b, "{\"pbrMetallicRoughness\":{\"baseColorTexture\":{\"index\":0,\"texCoord\":1}}}")));

        asset.Scene.Find<Mesh>()!.Material.ShouldBeOfType<StandardMaterial>().Map.ShouldNotBeNull().Channel.ShouldBe(1);
        asset.Warnings.ShouldBeEmpty();
    }

    [Fact]
    public void A_Third_Uv_Set_Falls_Back_To_The_Second_With_A_Warning()
    {
        var b = new GltfBuilder();
        using var asset = GltfLoader.Load(b.Glb(TextureScene(b, "{\"pbrMetallicRoughness\":{\"baseColorTexture\":{\"index\":0,\"texCoord\":2}}}")));

        asset.Scene.Find<Mesh>()!.Material.ShouldBeOfType<StandardMaterial>().Map.ShouldNotBeNull().Channel.ShouldBe(1);
        asset.Warnings.ShouldHaveSingleItem().ShouldContain("UV set 2");
    }

    [Fact]
    public void Vertex_Colors_Turn_On_Vertex_Colors_Only_Where_Present()
    {
        var b = new GltfBuilder();
        var color = b.Floats("VEC3", 1, 0, 0, 0, 1, 0, 0, 0, 1);
        using var asset = GltfLoader.Load(b.Glb(TextureScene(b, "{\"name\":\"paint\"}", attributes: $",\"COLOR_0\":{color}")));

        var group = asset.Scene.Children[0];
        var colored = ((Mesh)group.Children[0]).Material;
        var plain = ((Mesh)group.Children[1]).Material;
        colored.VertexColors.ShouldBeTrue();
        plain.VertexColors.ShouldBeFalse();
        colored.ShouldNotBeSameAs(plain);
        colored.Name.ShouldBe("paint");
        ((Mesh)group.Children[0]).Geometry.GetAttribute(BufferGeometry.Color)!.ItemSize.ShouldBe(3);
    }

    [Fact]
    public void An_Image_That_Cannot_Be_Decoded_Is_Skipped_With_A_Warning()
    {
        var b = new GltfBuilder();
        var position = b.Floats("VEC3", 0, 0, 0, 1, 0, 0, 0, 1, 0);
        var notAnImage = b.View("RIFF....WEBPVP8 "u8);
        var body = "\"scenes\":[{\"nodes\":[0]}],\"nodes\":[{\"mesh\":0}],"
            + $"\"meshes\":[{{\"primitives\":[{{\"attributes\":{{\"POSITION\":{position}}},\"material\":0}}]}}],"
            + $"\"images\":[{{\"bufferView\":{notAnImage},\"name\":\"photo\"}},{{\"uri\":\"missing.png\"}}],\"textures\":[{{\"source\":0}},{{\"source\":1}},{{}}],"
            + "\"materials\":[{\"pbrMetallicRoughness\":{\"baseColorTexture\":{\"index\":0}},\"emissiveTexture\":{\"index\":1},\"normalTexture\":{\"index\":2}}]";
        using var asset = GltfLoader.Load(b.Glb(body));

        var material = asset.Scene.Find<Mesh>()!.Material.ShouldBeOfType<StandardMaterial>();
        material.Map.ShouldBeNull();
        material.EmissiveMap.ShouldBeNull();
        material.NormalMap.ShouldBeNull();
        asset.Warnings.Count.ShouldBe(3);
        asset.Warnings.ShouldContain(w => w.Contains("'photo'") && w.Contains("ImageDecoders.Register"));
        asset.Warnings.ShouldContain(w => w.Contains("missing.png"));
        asset.Warnings.ShouldContain(w => w.Contains("Texture 2"));
    }

    [Fact]
    public void Image_From_A_Data_Uri_Loads()
    {
        var b = new GltfBuilder();
        var position = b.Floats("VEC3", 0, 0, 0, 1, 0, 0, 0, 1, 0);
        var png = Convert.ToBase64String(Png.Encode(1, 1, [10, 20, 30, 255]));
        var body = "\"scenes\":[{\"nodes\":[0]}],\"nodes\":[{\"mesh\":0}],"
            + $"\"meshes\":[{{\"primitives\":[{{\"attributes\":{{\"POSITION\":{position}}},\"material\":0}}]}}],"
            + $"\"images\":[{{\"uri\":\"data:image/png;base64,{png}\"}}],\"textures\":[{{\"source\":0}}],"
            + "\"materials\":[{\"extensions\":{\"KHR_materials_unlit\":{}},\"pbrMetallicRoughness\":{\"baseColorTexture\":{\"index\":0}}}]";
        using var asset = GltfLoader.Load(b.Gltf(body));

        var map = asset.Scene.Find<Mesh>()!.Material.ShouldBeOfType<BasicMaterial>().Map.ShouldNotBeNull();
        RenderHarness.Pixel(map.Image!, 0, 0).ShouldBe(((byte)10, (byte)20, (byte)30, (byte)255));
    }

    // ---- Nodes ----

    [Fact]
    public void Matrix_And_Trs_Nodes_Give_The_Same_Transform()
    {
        var b = new GltfBuilder();
        // Scale 2, then a quarter turn about Y, then a move to (1, 2, 3): column vectors, stored column by column.
        var body = "\"scenes\":[{\"nodes\":[0,1]}],\"nodes\":["
            + "{\"name\":\"trs\",\"translation\":[1,2,3],\"rotation\":[0,0.70710678,0,0.70710678],\"scale\":[2,2,2],\"children\":[2]},"
            + "{\"name\":\"matrix\",\"matrix\":[0,0,-2,0, 0,2,0,0, 2,0,0,0, 1,2,3,1]},"
            + "{\"name\":\"child\",\"translation\":[1,0,0]}]";
        using var asset = GltfLoader.Load(Encoding.UTF8.GetBytes(b.Json(body, null)));

        var trs = asset.Scene.Find("trs").ShouldNotBeNull();
        var matrix = asset.Scene.Find("matrix").ShouldNotBeNull();
        ShouldBeNear(trs.LocalToWorld(new Vector3(1, 0, 0)), new Vector3(1, 2, 1));
        ShouldBeNear(matrix.LocalToWorld(new Vector3(1, 0, 0)), new Vector3(1, 2, 1));
        ShouldBeNear(matrix.LocalToWorld(new Vector3(0, 1, 1)), trs.LocalToWorld(new Vector3(0, 1, 1)));
        ShouldBeNear(matrix.Scale, new Vector3(2, 2, 2));

        var child = asset.Scene.Find("child").ShouldNotBeNull();
        child.Parent.ShouldBeSameAs(trs);
        ShouldBeNear(child.WorldPosition, new Vector3(1, 2, 1));
        trs.ShouldBeOfType<Group>();
    }

    [Fact]
    public void Default_Scene_Is_Selected_And_A_File_Without_Scenes_Shows_Its_Root_Nodes()
    {
        var b = new GltfBuilder();
        var body = "\"scene\":1,\"scenes\":[{\"name\":\"first\",\"nodes\":[0]},{\"name\":\"second\",\"nodes\":[1]}],\"nodes\":[{\"name\":\"a\"},{\"name\":\"b\"}]";
        using var asset = GltfLoader.Load(Encoding.UTF8.GetBytes(b.Json(body, null)));
        asset.Scenes.Count.ShouldBe(2);
        asset.Scene.Name.ShouldBe("second");
        asset.Scene.Children.ShouldHaveSingleItem().Name.ShouldBe("b");
        asset.Scenes[0].Children.ShouldHaveSingleItem().Name.ShouldBe("a");

        body = "\"nodes\":[{\"name\":\"root\",\"children\":[1]},{\"name\":\"leaf\"},{\"name\":\"other\"}]";
        using var bare = GltfLoader.Load(Encoding.UTF8.GetBytes(b.Json(body, null)));
        bare.Scene.Children.Select(c => c.Name).ShouldBe(["root", "other"]);
        bare.Scene.Find("leaf")!.Parent!.Name.ShouldBe("root");
    }

    [Fact]
    public void A_Node_Used_Twice_Or_In_A_Cycle_Is_Kept_Once()
    {
        var b = new GltfBuilder();
        var body = "\"scenes\":[{\"nodes\":[0,1,1]}],\"nodes\":[{\"name\":\"a\",\"children\":[0,2]},{\"name\":\"b\"},{\"name\":\"c\",\"children\":[9]}]";
        using var asset = GltfLoader.Load(Encoding.UTF8.GetBytes(b.Json(body, null)));

        asset.Scene.Children.Select(c => c.Name).ShouldBe(["a", "b"]);
        asset.Scene.Find("a")!.Children.ShouldHaveSingleItem().Name.ShouldBe("c");
        asset.Warnings.Count.ShouldBe(3);
    }

    [Fact]
    public void Cameras_Become_Children_Of_Their_Nodes()
    {
        var b = new GltfBuilder();
        var body = "\"scenes\":[{\"nodes\":[0,1,2]}],\"nodes\":[{\"name\":\"view\",\"camera\":0,\"translation\":[0,1,5]},{\"camera\":1},{\"camera\":2}],"
            + "\"cameras\":[{\"name\":\"main\",\"type\":\"perspective\",\"perspective\":{\"yfov\":0.5,\"aspectRatio\":1.5,\"znear\":0.2,\"zfar\":50}},"
            + "{\"type\":\"perspective\",\"perspective\":{\"yfov\":1.0,\"znear\":0.1}},"
            + "{\"type\":\"orthographic\",\"orthographic\":{\"xmag\":2,\"ymag\":1,\"znear\":0.1,\"zfar\":10}}]";
        using var asset = GltfLoader.Load(Encoding.UTF8.GetBytes(b.Json(body, null)));

        asset.Cameras.Count.ShouldBe(3);
        var main = asset.Cameras[0].ShouldBeOfType<PerspectiveCamera>();
        main.Name.ShouldBe("main");
        main.Parent!.Name.ShouldBe("view");
        main.Fov.ShouldBe(MathUtil.ToDegrees(0.5f), 1e-4f);
        main.Aspect.ShouldBe(1.5f);
        main.Near.ShouldBe(0.2f);
        main.Far.ShouldBe(50f);
        ShouldBeNear(main.WorldPosition, new Vector3(0, 1, 5));

        var infinite = asset.Cameras[1].ShouldBeOfType<PerspectiveCamera>();
        infinite.Far.ShouldBeGreaterThan(1e5f);
        infinite.Aspect.ShouldBe(1f);

        var orthographic = asset.Cameras[2].ShouldBeOfType<OrthographicCamera>();
        (orthographic.Left, orthographic.Right, orthographic.Top, orthographic.Bottom).ShouldBe((-2f, 2f, 1f, -1f));
        orthographic.Far.ShouldBe(10f);
    }

    [Fact]
    public void Punctual_Lights_Shine_Along_Their_Nodes_Negative_Z()
    {
        var b = new GltfBuilder();
        var body = "\"extensionsUsed\":[\"KHR_lights_punctual\"],"
            + "\"extensions\":{\"KHR_lights_punctual\":{\"lights\":["
            + "{\"name\":\"sun\",\"type\":\"directional\",\"color\":[1,0.5,0.25],\"intensity\":3},"
            + "{\"type\":\"point\",\"intensity\":100,\"range\":7},"
            + "{\"type\":\"spot\",\"spot\":{\"innerConeAngle\":0.2,\"outerConeAngle\":0.4}},"
            + "{\"type\":\"spot\"}]}},"
            + "\"scenes\":[{\"nodes\":[0,1,2,3]}],\"nodes\":["
            + "{\"translation\":[0,4,0],\"rotation\":[0,0.70710678,0,0.70710678],\"extensions\":{\"KHR_lights_punctual\":{\"light\":0}}},"
            + "{\"translation\":[1,2,3],\"extensions\":{\"KHR_lights_punctual\":{\"light\":1}}},"
            + "{\"rotation\":[-0.70710678,0,0,0.70710678],\"extensions\":{\"KHR_lights_punctual\":{\"light\":2}}},"
            + "{\"extensions\":{\"KHR_lights_punctual\":{\"light\":3}}}]";
        using var asset = GltfLoader.Load(Encoding.UTF8.GetBytes(b.Json(body, null)));

        asset.Warnings.ShouldBeEmpty();
        asset.Lights.Count.ShouldBe(4);
        var sun = asset.Lights[0].ShouldBeOfType<DirectionalLight>();
        sun.Name.ShouldBe("sun");
        sun.Color.ShouldBe(new ColorF(1f, 0.5f, 0.25f));
        sun.Intensity.ShouldBe(3f);
        ShouldBeNear(sun.WorldPosition, new Vector3(0, 4, 0));
        // -Z turned a quarter about Y points along -X.
        ShouldBeNear(sun.Direction, new Vector3(-1, 0, 0));

        var point = asset.Lights[1].ShouldBeOfType<PointLight>();
        point.Intensity.ShouldBe(100f);
        point.Distance.ShouldBe(7f);
        point.Decay.ShouldBe(2f);
        ShouldBeNear(point.WorldPosition, new Vector3(1, 2, 3));

        var spot = asset.Lights[2].ShouldBeOfType<SpotLight>();
        spot.Angle.ShouldBe(0.4f);
        spot.Penumbra.ShouldBe(0.5f, 1e-6f);
        // -Z turned a quarter down about X points along -Y.
        ShouldBeNear(spot.Direction, new Vector3(0, -1, 0));

        var plain = asset.Lights[3].ShouldBeOfType<SpotLight>();
        plain.Angle.ShouldBe(MathF.PI / 4f);
        plain.Penumbra.ShouldBe(1f);
        ShouldBeNear(plain.Direction, new Vector3(0, 0, -1));
    }

    // ---- Skins and animations ----

    private static string SkinnedScene(GltfBuilder b, string extra = "")
    {
        var position = b.Floats("VEC3", 0, 0, 0, 1, 0, 0, 0, 2, 0);
        var joints = b.UBytes("VEC4", false, 0, 0, 0, 0, 0, 1, 0, 0, 1, 0, 0, 0);
        var weights = b.Floats("VEC4", 1, 0, 0, 0, 0.5f, 0.5f, 0, 0, 1, 0, 0, 0);
        var inverse = b.Floats("MAT4", 1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1, 1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1, 0, 0, -1, 0, 1);
        return "\"scenes\":[{\"nodes\":[0,1,3]}],\"nodes\":["
            + "{\"name\":\"body\",\"mesh\":0,\"skin\":0},"
            + "{\"name\":\"hip\",\"children\":[2]},"
            + "{\"name\":\"knee\",\"translation\":[0,1,0]},"
            + "{\"name\":\"twin\",\"mesh\":0,\"skin\":0}],"
            + $"\"meshes\":[{{\"primitives\":[{{\"attributes\":{{\"POSITION\":{position},\"JOINTS_0\":{joints},\"WEIGHTS_0\":{weights}}}}}]}}],"
            + $"\"skins\":[{{\"joints\":[1,2],\"inverseBindMatrices\":{inverse}}}]" + extra;
    }

    [Fact]
    public void Skins_Become_Skeletons_Of_Bones()
    {
        var b = new GltfBuilder();
        using var asset = GltfLoader.Load(b.Glb(SkinnedScene(b)));

        asset.Warnings.ShouldBeEmpty();
        var body = asset.Scene.Find("body").ShouldBeOfType<SkinnedMesh>();
        var skeleton = body.Skeleton.ShouldNotBeNull();
        skeleton.Bones.Length.ShouldBe(2);
        skeleton.Bones[0].ShouldBeOfType<Bone>().Name.ShouldBe("hip");
        skeleton.Bones[1].ShouldBeOfType<Bone>().Name.ShouldBe("knee");
        skeleton.Bones[1].Parent.ShouldBeSameAs(skeleton.Bones[0]);
        skeleton.InverseBindMatrices[0].ShouldBe(Matrix4x4.Identity);
        skeleton.InverseBindMatrices[1].Translation.ShouldBe(new Vector3(0, -1, 0));

        // In the bind pose, bone world * inverse bind is the identity: the mesh is undeformed.
        (skeleton.InverseBindMatrices[1] * skeleton.Bones[1].WorldMatrix).ShouldBe(Matrix4x4.Identity);

        var geometry = body.Geometry;
        geometry.GetAttribute(BufferGeometry.SkinIndex)!.Format.ShouldBe(VertexFormat.UByte4);
        geometry.GetAttribute(BufferGeometry.SkinWeight)!.GetVector4(1).ShouldBe(new Vector4(0.5f, 0.5f, 0, 0));

        // Two nodes with the same skin share the skeleton.
        asset.Scene.Find("twin").ShouldBeOfType<SkinnedMesh>().Skeleton.ShouldBeSameAs(skeleton);
    }

    [Fact]
    public void A_Mesh_With_Skin_Attributes_But_No_Skin_Is_A_Plain_Mesh()
    {
        var b = new GltfBuilder();
        var body = SkinnedScene(b).Replace(",\"skin\":0", "");
        using var asset = GltfLoader.Load(b.Glb(body));

        asset.Scene.Find("body").ShouldBeOfType<Mesh>();
    }

    [Fact]
    public void Animations_Become_Clips_With_Tracks_Of_Each_Interpolation()
    {
        var b = new GltfBuilder();
        var position = b.Floats("VEC3", 0, 0, 0, 1, 0, 0, 0, 1, 0);
        var up = b.Floats("VEC3", 0, 1, 0, 0, 1, 0, 0, 1, 0);
        var wide = b.Floats("VEC3", 2, 0, 0, 2, 0, 0, 2, 0, 0);
        var times = b.Floats("SCALAR", 0, 1);
        var translation = b.Floats("VEC3", 0, 0, 0, 2, 0, 0);
        var scale = b.Floats("VEC3", 1, 1, 1, 2, 2, 2);
        // Cubic spline: in-tangent, value, out-tangent per key.
        var rotation = b.Floats("VEC4", 0, 0, 0, 0, 0, 0, 0, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0.70710678f, 0, 0.70710678f, 0, 0, 0, 0);
        // Normalized bytes are valid weight data.
        var weights = b.UBytes("SCALAR", true, 0, 0, 255, 51);
        var body = "\"scenes\":[{\"nodes\":[0]}],\"nodes\":[{\"name\":\"mover\",\"mesh\":0}],"
            + $"\"meshes\":[{{\"primitives\":[{{\"attributes\":{{\"POSITION\":{position}}},\"targets\":[{{\"POSITION\":{up}}},{{\"POSITION\":{wide}}}]}}]}}],"
            + "\"animations\":[{\"name\":\"move\",\"channels\":["
            + "{\"sampler\":0,\"target\":{\"node\":0,\"path\":\"translation\"}},"
            + "{\"sampler\":1,\"target\":{\"node\":0,\"path\":\"scale\"}},"
            + "{\"sampler\":2,\"target\":{\"node\":0,\"path\":\"rotation\"}},"
            + "{\"sampler\":3,\"target\":{\"node\":0,\"path\":\"weights\"}},"
            + "{\"sampler\":0,\"target\":{\"node\":0,\"path\":\"pointer\"}},"
            + "{\"sampler\":0,\"target\":{\"path\":\"translation\"}}],"
            + $"\"samplers\":[{{\"input\":{times},\"output\":{translation}}},{{\"input\":{times},\"output\":{scale},\"interpolation\":\"STEP\"}},"
            + $"{{\"input\":{times},\"output\":{rotation},\"interpolation\":\"CUBICSPLINE\"}},{{\"input\":{times},\"output\":{weights},\"interpolation\":\"LINEAR\"}}]}},"
            + "{\"channels\":[],\"samplers\":[]}]";
        using var asset = GltfLoader.Load(b.Glb(body));

        asset.Animations.Count.ShouldBe(2);
        asset.Animations[1].Name.ShouldBe("animation_1");
        asset.Animations[1].Tracks.ShouldBeEmpty();
        var clip = asset.Animations[0];
        clip.Name.ShouldBe("move");
        clip.Duration.ShouldBe(1f);
        clip.Tracks.Length.ShouldBe(4);
        var mover = asset.Scene.Find("mover").ShouldBeOfType<Mesh>();
        Span<float> value = stackalloc float[4];

        var move = clip.Tracks[0];
        move.Target.ShouldBeSameAs(mover);
        move.Property.ShouldBe(TrackProperty.Position);
        move.Interpolation.ShouldBe(Interpolation.Linear);
        move.Sample(0.5f, value);
        value[0].ShouldBe(1f, 1e-6f);

        var grow = clip.Tracks[1];
        grow.Property.ShouldBe(TrackProperty.Scale);
        grow.Interpolation.ShouldBe(Interpolation.Step);
        grow.Sample(0.9f, value);
        value[0].ShouldBe(1f);

        var turn = clip.Tracks[2];
        turn.Property.ShouldBe(TrackProperty.Rotation);
        turn.Interpolation.ShouldBe(Interpolation.CubicSpline);
        turn.ValueSize.ShouldBe(4);
        turn.Sample(1f, value);
        value[1].ShouldBe(0.70710678f, 1e-5f);
        turn.Sample(0.5f, value);
        new Quaternion(value[0], value[1], value[2], value[3]).Length().ShouldBe(1f, 1e-5f);

        var morph = clip.Tracks[3];
        morph.Target.ShouldBeSameAs(mover);
        morph.Property.ShouldBe(TrackProperty.MorphWeights);
        morph.ValueSize.ShouldBe(2);
        morph.Sample(1f, value);
        value[0].ShouldBe(1f);
        value[1].ShouldBe(0.2f, 1e-6f);

        asset.Warnings.ShouldHaveSingleItem().ShouldContain("pointer");
    }

    [Fact]
    public void Animation_Data_That_Does_Not_Fit_Its_Keys_Is_Skipped()
    {
        var b = new GltfBuilder();
        var times = b.Floats("SCALAR", 0, 1, 2);
        var translation = b.Floats("VEC3", 0, 0, 0, 2, 0, 0);
        var body = "\"scenes\":[{\"nodes\":[0]}],\"nodes\":[{\"name\":\"n\"}],"
            + "\"animations\":[{\"channels\":[{\"sampler\":0,\"target\":{\"node\":0,\"path\":\"translation\"}},{\"sampler\":0,\"target\":{\"node\":0,\"path\":\"weights\"}}],"
            + $"\"samplers\":[{{\"input\":{times},\"output\":{translation}}}]}}]";
        using var asset = GltfLoader.Load(b.Glb(body));

        asset.Animations.ShouldHaveSingleItem().Tracks.ShouldBeEmpty();
        asset.Warnings.Count.ShouldBe(2);
    }

    // ---- Lifetime ----

    [Fact]
    public void Disposing_The_Asset_Disposes_Its_Resources()
    {
        var b = new GltfBuilder();
        var asset = GltfLoader.Load(b.Glb(TextureScene(b, "{\"pbrMetallicRoughness\":{\"baseColorTexture\":{\"index\":0}}}")));
        var mesh = asset.Scene.Find<Mesh>()!;
        var texture = mesh.Material.ShouldBeOfType<StandardMaterial>().Map!;

        asset.Dispose();
        asset.Dispose();

        mesh.Geometry.IsDisposed.ShouldBeTrue();
        mesh.Material.IsDisposed.ShouldBeTrue();
        texture.IsDisposed.ShouldBeTrue();
    }
}
