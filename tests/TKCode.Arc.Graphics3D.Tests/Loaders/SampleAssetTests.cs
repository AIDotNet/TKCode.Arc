using System.Numerics;

namespace TKCode.Arc.Graphics3D.Tests;

/// <summary>Loads public sample models when a folder with them is named by the environment variable
/// <c>TKCODE_ARC_GLTF_SAMPLES</c>; skipped otherwise (the repository carries no model files). The folder layout is
/// described in each test.</summary>
public class SampleAssetTests
{
    private static string SampleOrSkip(params string[] parts)
    {
        var root = Environment.GetEnvironmentVariable("TKCODE_ARC_GLTF_SAMPLES");
        Assert.SkipWhen(string.IsNullOrEmpty(root), "TKCODE_ARC_GLTF_SAMPLES is not set.");
        var path = Path.Combine([root!, .. parts]);
        Assert.SkipUnless(File.Exists(path), $"{path} is not there.");
        return path;
    }

    private static (int Vertices, int Indices, BoundingBox Box) Measure(GltfAsset asset)
    {
        int vertices = 0, indices = 0;
        var box = BoundingBox.Empty;
        foreach (var geometry in asset.Geometries)
        {
            vertices += geometry.VertexCount;
            indices += geometry.IndexCount;
            box.Expand(geometry.BoundingBox);
        }

        return (vertices, indices, box);
    }

    /// <summary>Khronos glTF-Sample-Assets "BrainStem": <c>meshopt/BrainStem.gltf</c> + <c>BrainStem.bin</c> (the
    /// glTF-Meshopt variant) and <c>BrainStem-ref.glb</c> (the glTF-Binary variant).</summary>
    [Fact]
    public void Meshopt_Compressed_Model_Decodes_To_The_Same_Mesh_As_The_Uncompressed_One()
    {
        var compressedPath = SampleOrSkip("meshopt", "BrainStem.gltf");
        var referencePath = SampleOrSkip("BrainStem-ref.glb");
        using var compressed = GltfLoader.Load(compressedPath);
        using var reference = GltfLoader.Load(referencePath);

        var a = Measure(compressed);
        var b = Measure(reference);
        a.Indices.ShouldBe(b.Indices);
        // The compressed variant was re-indexed by its optimizer: the same triangles over slightly fewer vertices.
        Math.Abs(a.Vertices - b.Vertices).ShouldBeLessThan(b.Vertices / 50);
        Vector3.Distance(a.Box.Min, b.Box.Min).ShouldBeLessThan(0.01f);
        Vector3.Distance(a.Box.Max, b.Box.Max).ShouldBeLessThan(0.01f);
        compressed.Animations.Count.ShouldBe(reference.Animations.Count);
        // Its animation was resampled at a fixed rate, which rounds the length.
        compressed.Animations[0].Duration.ShouldBe(reference.Animations[0].Duration, 0.1f);

        // Every index is in range, and normals decode to unit length.
        foreach (var geometry in compressed.Geometries)
        {
            for (var i = 0; i < geometry.IndexCount; i++)
            {
                geometry.GetIndex(i).ShouldBeLessThan(geometry.VertexCount);
            }

            var normal = geometry.GetAttribute(BufferGeometry.Normal);
            if (normal is not null)
            {
                for (var i = 0; i < normal.Count; i += 97)
                {
                    normal.GetVector3(i).Length().ShouldBe(1f, 0.03f);
                }
            }
        }

        // The two files draw the same picture, posed by the same animation time.
        using var gl = GlTestContext.CreateOrSkip();
        using var renderer = new Renderer(gl.Device) { ToneMapping = ToneMapping.Neutral };
        TextureImage Draw(GltfAsset asset, string name)
        {
            var scene = new Scene { Background = ColorF.FromHex(0x20242c) };
            scene.Add(asset.Scene, new DirectionalLight(2.5f) { Position = new Vector3(2f, 4f, 5f) }, new HemisphereLight(ColorF.White, ColorF.FromHex(0x444444), 1.5f));
            var mixer = new AnimationMixer();
            mixer.ClipAction(asset.Animations[0]).Play();
            mixer.SetTime(1.2f);
            var box = scene.ComputeBounds();
            var camera = new PerspectiveCamera(40f, 1f, 0.01f, 100f) { Position = box.Center + new Vector3(0f, 0.2f, 1f) * box.Size.Length() * 1.1f };
            camera.LookAt(box.Center);
            var image = RenderHarness.Render(renderer, scene, camera, 400, 400, name);
            scene.Remove(asset.Scene);
            return image;
        }

        var first = Draw(compressed, "sample-brainstem-meshopt");
        var second = Draw(reference, "sample-brainstem-reference");
        gl.Device.DrainErrors().ShouldBeEmpty();
        var different = 0;
        for (var y = 0; y < 400; y += 2)
        {
            for (var x = 0; x < 400; x += 2)
            {
                var p = RenderHarness.Pixel(first, x, y);
                var q = RenderHarness.Pixel(second, x, y);
                if (Math.Abs(p.R - q.R) + Math.Abs(p.G - q.G) + Math.Abs(p.B - q.B) > 40)
                {
                    different++;
                }
            }
        }

        // Quantization moves edges by a fraction of a pixel; anything more would be a decoding error.
        different.ShouldBeLessThan(400);
    }

    /// <summary>three.js example model <c>coffeemat.glb</c>: EXT_meshopt_compression with vertex codec version 0. Its
    /// textures need KTX2, so the file is not loaded as a whole; every compressed view is decoded and checked against
    /// what the file itself declares.</summary>
    [Fact]
    public void Meshopt_Version_0_Views_Decode_To_What_The_File_Declares()
    {
        var bytes = File.ReadAllBytes(SampleOrSkip("coffeemat.glb"));
        var jsonLength = BitConverter.ToInt32(bytes, 12);
        using var document = System.Text.Json.JsonDocument.Parse(bytes.AsMemory(20, jsonLength));
        var bin = bytes.AsSpan(20 + jsonLength + 8);
        var root = document.RootElement;
        var views = root.GetProperty("bufferViews");
        var decoded = new Dictionary<int, byte[]>();
        var versions = new HashSet<int>();
        for (var i = 0; i < views.GetArrayLength(); i++)
        {
            if (!views[i].TryGetProperty("extensions", out var extensions) || !extensions.TryGetProperty("EXT_meshopt_compression", out var e))
            {
                continue;
            }

            var offset = e.TryGetProperty("byteOffset", out var o) ? o.GetInt32() : 0;
            var mode = e.GetProperty("mode").GetString() switch
            {
                "ATTRIBUTES" => MeshoptMode.Attributes,
                "TRIANGLES" => MeshoptMode.Triangles,
                _ => MeshoptMode.Indices,
            };
            var filter = (e.TryGetProperty("filter", out var f) ? f.GetString() : "NONE") switch
            {
                "OCTAHEDRAL" => MeshoptFilter.Octahedral,
                "QUATERNION" => MeshoptFilter.Quaternion,
                "EXPONENTIAL" => MeshoptFilter.Exponential,
                _ => MeshoptFilter.None,
            };
            if (mode == MeshoptMode.Attributes)
            {
                versions.Add(bin[offset] & 15);
            }

            // Decoding checks that the data is consumed exactly; a wrong decoder fails here.
            decoded[i] = MeshoptDecoder.Decode(bin.Slice(offset, e.GetProperty("byteLength").GetInt32()), e.GetProperty("count").GetInt32(), e.GetProperty("byteStride").GetInt32(), mode, filter);
        }

        decoded.Count.ShouldBeGreaterThan(3);
        versions.ShouldContain(0);

        // Positions: the decoded values span exactly the minimum and maximum the accessor declares.
        var accessors = root.GetProperty("accessors");
        var checkedPositions = 0;
        foreach (var mesh in root.GetProperty("meshes").EnumerateArray())
        {
            foreach (var primitive in mesh.GetProperty("primitives").EnumerateArray())
            {
                var accessor = accessors[primitive.GetProperty("attributes").GetProperty("POSITION").GetInt32()];
                var view = accessor.GetProperty("bufferView").GetInt32();
                var data = decoded[view];
                var stride = views[view].GetProperty("byteStride").GetInt32();
                var start = accessor.TryGetProperty("byteOffset", out var ao) ? ao.GetInt32() : 0;
                var count = accessor.GetProperty("count").GetInt32();
                var type = accessor.GetProperty("componentType").GetInt32();
                for (var axis = 0; axis < 3; axis++)
                {
                    double min = double.MaxValue, max = double.MinValue;
                    for (var v = 0; v < count; v++)
                    {
                        var at = start + (v * stride) + (axis * (type == 5126 ? 4 : 2));
                        double value = type switch
                        {
                            5126 => BitConverter.ToSingle(data, at),
                            5122 => BitConverter.ToInt16(data, at),
                            _ => BitConverter.ToUInt16(data, at),
                        };
                        min = Math.Min(min, value);
                        max = Math.Max(max, value);
                    }

                    min.ShouldBe(accessor.GetProperty("min")[axis].GetDouble(), 1e-3);
                    max.ShouldBe(accessor.GetProperty("max")[axis].GetDouble(), 1e-3);
                }

                // Indices stay inside the vertex range.
                var indexAccessor = accessors[primitive.GetProperty("indices").GetInt32()];
                var indexData = decoded[indexAccessor.GetProperty("bufferView").GetInt32()];
                var indexCount = indexAccessor.GetProperty("count").GetInt32();
                var wide = indexAccessor.GetProperty("componentType").GetInt32() == 5125;
                for (var k = 0; k < indexCount; k++)
                {
                    var index = wide ? BitConverter.ToUInt32(indexData, k * 4) : BitConverter.ToUInt16(indexData, k * 2);
                    index.ShouldBeLessThan((uint)count);
                }

                checkedPositions++;
            }
        }

        checkedPositions.ShouldBeGreaterThan(0);
    }

    /// <summary>Damaged meshopt data (the views of the BrainStem sample, cut short or with bytes changed) either still
    /// decodes or is refused as invalid data; it never reads outside the buffer or runs away.</summary>
    [Fact]
    public void Damaged_Meshopt_Data_Is_Refused_As_Invalid()
    {
        var folder = Path.GetDirectoryName(SampleOrSkip("meshopt", "BrainStem.gltf"))!;
        using var document = System.Text.Json.JsonDocument.Parse(File.ReadAllBytes(Path.Combine(folder, "BrainStem.gltf")));
        var bin = File.ReadAllBytes(Path.Combine(folder, "BrainStem.bin"));
        var random = new Random(20261117);
        var attempts = 0;
        foreach (var view in document.RootElement.GetProperty("bufferViews").EnumerateArray())
        {
            var e = view.GetProperty("extensions").GetProperty("KHR_meshopt_compression");
            var source = bin.AsSpan(e.TryGetProperty("byteOffset", out var o) ? o.GetInt32() : 0, e.GetProperty("byteLength").GetInt32()).ToArray();
            var count = e.GetProperty("count").GetInt32();
            var stride = e.GetProperty("byteStride").GetInt32();
            var mode = e.GetProperty("mode").GetString() switch
            {
                "ATTRIBUTES" => MeshoptMode.Attributes,
                "TRIANGLES" => MeshoptMode.Triangles,
                _ => MeshoptMode.Indices,
            };
            for (var i = 0; i < 150; i++)
            {
                var damaged = (byte[])source.Clone();
                switch (i % 3)
                {
                    case 0:
                        damaged = damaged[..random.Next(damaged.Length)];
                        break;
                    case 1:
                        for (var k = random.Next(1, 8); k > 0; k--)
                        {
                            damaged[random.Next(damaged.Length)] ^= (byte)(1 << random.Next(8));
                        }

                        break;
                    default:
                    {
                        var start = random.Next(damaged.Length);
                        random.NextBytes(damaged.AsSpan(start, Math.Min(16, damaged.Length - start)));
                        break;
                    }
                }

                attempts++;
                try
                {
                    MeshoptDecoder.Decode(damaged, count, stride, mode).Length.ShouldBe(count * stride);
                }
                catch (InvalidDataException)
                {
                }
                catch (NotSupportedException)
                {
                }
            }
        }

        attempts.ShouldBeGreaterThan(500);
    }

    /// <summary>Khronos glTF-Sample-Assets models that come with a Draco variant, laid out as in that repository:
    /// <c>draco/&lt;Model&gt;/glTF-Draco/&lt;Model&gt;.gltf</c> and <c>draco/&lt;Model&gt;/glTF/&lt;Model&gt;.gltf</c>, each with
    /// its buffers and images.</summary>
    [Theory]
    [InlineData("Box")]
    [InlineData("Duck")]
    [InlineData("Avocado")]
    [InlineData("BarramundiFish")]
    [InlineData("BoomBox")]
    [InlineData("Corset")]
    [InlineData("Lantern")]
    [InlineData("WaterBottle")]
    [InlineData("CesiumMilkTruck")]
    [InlineData("VirtualCity")]
    [InlineData("RiggedSimple")]
    [InlineData("RiggedFigure")]
    [InlineData("CesiumMan")]
    [InlineData("BrainStem")]
    [InlineData("MorphPrimitivesTest")]
    public void Draco_Compressed_Model_Decodes_To_The_Same_Mesh_As_The_Uncompressed_One(string model)
    {
        var compressedPath = SampleOrSkip("draco", model, "glTF-Draco", model + ".gltf");
        var referencePath = SampleOrSkip("draco", model, "glTF", model + ".gltf");
        using var compressed = GltfLoader.Load(compressedPath);
        using var reference = GltfLoader.Load(referencePath);

        // The same triangles; the encoder merges and reorders vertices, so their number may differ.
        var a = Measure(compressed);
        var b = Measure(reference);
        a.Indices.ShouldBe(b.Indices);
        a.Vertices.ShouldBeGreaterThan(0);
        var tolerance = Math.Max(b.Box.Size.Length() * 2e-3f, 1e-5f);
        Vector3.Distance(a.Box.Min, b.Box.Min).ShouldBeLessThan(tolerance);
        Vector3.Distance(a.Box.Max, b.Box.Max).ShouldBeLessThan(tolerance);
        compressed.Animations.Count.ShouldBe(reference.Animations.Count);
        foreach (var geometry in compressed.Geometries)
        {
            for (var i = 0; i < geometry.IndexCount; i++)
            {
                geometry.GetIndex(i).ShouldBeLessThan(geometry.VertexCount);
            }
        }

        // The two files draw the same picture (posed by the same animation time when they are animated).
        using var gl = GlTestContext.CreateOrSkip();
        using var renderer = new Renderer(gl.Device) { ToneMapping = ToneMapping.Neutral };
        TextureImage Draw(GltfAsset asset, string name)
        {
            var scene = new Scene { Background = ColorF.FromHex(0x20242c) };
            scene.Add(asset.Scene, new DirectionalLight(2.5f) { Position = new Vector3(2f, 4f, 5f) }, new HemisphereLight(ColorF.White, ColorF.FromHex(0x444444), 1.5f));
            if (asset.Animations.Count > 0)
            {
                var mixer = new AnimationMixer();
                mixer.ClipAction(asset.Animations[0]).Play();
                mixer.SetTime(asset.Animations[0].Duration * 0.4f);
            }

            var box = scene.ComputeBounds();
            var camera = new PerspectiveCamera(40f, 1f, box.Size.Length() * 0.01f, box.Size.Length() * 20f)
            {
                Position = box.Center + (Vector3.Normalize(new Vector3(0.6f, 0.4f, 1f)) * box.Size.Length() * 1.3f),
            };
            camera.LookAt(box.Center);
            var image = RenderHarness.Render(renderer, scene, camera, 320, 320, name);
            scene.Remove(asset.Scene);
            return image;
        }

        var first = Draw(compressed, "sample-draco-" + model.ToLowerInvariant());
        var second = Draw(reference, "sample-draco-" + model.ToLowerInvariant() + "-reference");
        gl.Device.DrainErrors().ShouldBeEmpty();
        int different = 0, covered = 0;
        for (var y = 0; y < 320; y += 2)
        {
            for (var x = 0; x < 320; x += 2)
            {
                var p = RenderHarness.Pixel(first, x, y);
                var q = RenderHarness.Pixel(second, x, y);
                if (Math.Abs(p.R - q.R) + Math.Abs(p.G - q.G) + Math.Abs(p.B - q.B) > 48)
                {
                    different++;
                }

                if (Math.Abs(q.R - 0x20) + Math.Abs(q.G - 0x24) + Math.Abs(q.B - 0x2c) > 24)
                {
                    covered++;
                }
            }
        }

        // The model is in the picture, and quantization moves edges and shading by no more than a sliver.
        covered.ShouldBeGreaterThan(200);
        different.ShouldBeLessThan(Math.Max(covered / 25, 40));
    }

    /// <summary>Khronos glTF-Sample-Assets "AnisotropyBarnLamp": <c>ktx/AnisotropyBarnLamp/glTF</c> (PNG textures)
    /// and <c>ktx/AnisotropyBarnLamp/glTF-KTX-BasisU</c> (the same textures as KTX2 with UASTC and Zstandard). Basis
    /// compression loses a little, so the pictures are close, not equal.</summary>
    [Fact]
    public void Ktx2_Textured_Model_Draws_Like_The_Png_One()
    {
        var compressedPath = SampleOrSkip("ktx", "AnisotropyBarnLamp", "glTF-KTX-BasisU", "AnisotropyBarnLamp.gltf");
        var referencePath = SampleOrSkip("ktx", "AnisotropyBarnLamp", "glTF", "AnisotropyBarnLamp.gltf");
        using var compressed = GltfLoader.Load(compressedPath);
        using var reference = GltfLoader.Load(referencePath);
        compressed.Warnings.ShouldBe(reference.Warnings);
        compressed.Textures.Count.ShouldBe(reference.Textures.Count);
        compressed.Textures.Count.ShouldBeGreaterThan(3);
        for (var i = 0; i < compressed.Textures.Count; i++)
        {
            compressed.Textures[i].Image!.Width.ShouldBe(reference.Textures[i].Image!.Width);
            compressed.Textures[i].ColorSpace.ShouldBe(reference.Textures[i].ColorSpace);
        }

        using var gl = GlTestContext.CreateOrSkip();
        using var renderer = new Renderer(gl.Device) { ToneMapping = ToneMapping.Neutral };
        using var environment = TestScenes.SkyEnvironment();
        TextureImage Draw(GltfAsset asset, string name)
        {
            var scene = new Scene { Background = ColorF.FromHex(0x20242c), Environment = environment };
            scene.Add(asset.Scene, new DirectionalLight(2.5f) { Position = new Vector3(2f, 4f, 5f) });
            var box = scene.ComputeBounds();
            var camera = new PerspectiveCamera(40f, 1f, box.Size.Length() * 0.01f, box.Size.Length() * 20f)
            {
                Position = box.Center + (Vector3.Normalize(new Vector3(0.6f, 0.3f, 1f)) * box.Size.Length() * 1.2f),
            };
            camera.LookAt(box.Center);
            var image = RenderHarness.Render(renderer, scene, camera, 480, 480, name);
            scene.Remove(asset.Scene);
            return image;
        }

        var first = Draw(compressed, "sample-ktx2-barnlamp");
        var second = Draw(reference, "sample-ktx2-barnlamp-reference");
        gl.Device.DrainErrors().ShouldBeEmpty();
        long difference = 0;
        int different = 0, covered = 0, count = 0;
        for (var y = 0; y < 480; y += 2)
        {
            for (var x = 0; x < 480; x += 2)
            {
                var p = RenderHarness.Pixel(first, x, y);
                var q = RenderHarness.Pixel(second, x, y);
                var d = Math.Abs(p.R - q.R) + Math.Abs(p.G - q.G) + Math.Abs(p.B - q.B);
                difference += d;
                count++;
                if (d > 60)
                {
                    different++;
                }

                if (Math.Abs(q.R - 0x20) + Math.Abs(q.G - 0x24) + Math.Abs(q.B - 0x2c) > 24)
                {
                    covered++;
                }
            }
        }

        covered.ShouldBeGreaterThan(2000);
        ((double)difference / count).ShouldBeLessThan(4.0);
        different.ShouldBeLessThan(covered / 20);
    }
}
