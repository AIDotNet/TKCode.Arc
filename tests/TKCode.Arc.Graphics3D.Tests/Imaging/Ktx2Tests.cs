using System.Numerics;

namespace TKCode.Arc.Graphics3D.Tests;

/// <summary>KTX2 decoding on four tiny files written by the Basis Universal encoder (2.50); the expected pixels are
/// what its own transcoder gives for RGBA32. The full comparison with the reference (187 files) lives outside the
/// repository; these keep the four combinations of codec and supercompression from breaking unnoticed.</summary>
public class Ktx2Tests
{
    private const string Etc1sWithAlpha =
        "q0tUWCAyMLsNChoKAAAAAAEAAAABAAAAAQAAAAAAAAAAAAAAAQAAAAEAAAABAAAAaAAAADwAAACkAAAAJAAAAMgAAAAAAAAAhgAAAAAAAABOAQAAAAAAAAIAAAAAAAAAAAAAAAAAAAA8AAAAAAAAAAIAOACjAQIAAwMAAAgIAAAAAAAAAAA/AAAAAAAAAAAA/////0AAPw8AAAAAAAAAAP////8fAAAAS1RYd3JpdGVyAEJhc2lzIFVuaXZlcnNhbCAyLjUwAAACAAEALgAAAAUAAAArAAAAAAAAAAAAAAAAAAAAAQAAAAEAAAABAAAAAcAEAAAAAAAAogOIGjAAAAAICs7CCQwLYCIAAAAAAACpIABMAAAAAAAAIOjwAFRVVVUFAMFEAAAAAAAA8l9NAJgAAAAAAABAEAAmAAQAAAAAEAOACQAAAAAAAAQQAAAC";

    private const string UastcZstd =
        "q0tUWCAyMLsNChoKAAAAAAEAAABAAAAAQAAAAAAAAAAAAAAAAQAAAAEAAAACAAAAaAAAACwAAACUAAAAJAAAAAAAAAAAAAAAAAAAAAAAAAC4AAAAAAAAABsAAAAAAAAAABAAAAAAAAAsAAAAAAAAAAIAKACmAQIAAwMAABAAAAAAAAAAAAB/AAAAAAAAAAAA/////x8AAABLVFh3cml0ZXIAQmFzaXMgVW5pdmVyc2FsIDIuNTAAACi1L/1gAA+NAABAFxkK5T/IywACAO2f6cELuA==";

    private const string Etc1s =
        "q0tUWCAyMLsNChoKAAAAAAEAAABAAAAAQAAAAAAAAAAAAAAAAQAAAAEAAAABAAAAaAAAACwAAACUAAAAJAAAALgAAAAAAAAAhgAAAAAAAAA+AQAAAAAAAA4AAAAAAAAAAAAAAAAAAAAsAAAAAAAAAAIAKACjAQIAAwMAAAgAAAAAAAAAAAA/AAAAAAAAAAAA/////x8AAABLVFh3cml0ZXIAQmFzaXMgVW5pdmVyc2FsIDIuNTAAAAEAAQArAAAABQAAAC4AAAAAAAAAAAAAAAAAAAAOAAAAAAAAAAAAAAABwAQAAAAAAAAiA5gSAAAAAACCogMsBAATAAAAAAAACAJgBgAAAAAAAAlkVFVVVQUBQYQAAQAAAIgL8v+rAGACAAAAAAAAIQRMBAAAAAAAIG2AgIkAAAAAAAAkDRAAoT+AhzzkIQ95yEMe8gA=";

    private const string UastcWithAlpha =
        "q0tUWCAyMLsNChoKAAAAAAEAAAABAAAAAQAAAAAAAAAAAAAAAQAAAAEAAAAAAAAAaAAAACwAAACUAAAALAAAAAAAAAAAAAAAAAAAAAAAAADAAAAAAAAAABAAAAAAAAAAEAAAAAAAAAAsAAAAAAAAAAIAKACmAQIAAwMAABAAAAAAAAAAAAB/AwAAAAAAAAAA/////x8AAABLVFh3cml0ZXIAQmFzaXMgVW5pdmVyc2FsIDIuNTAAAAQAAAB/fwAAVwuUG3APKgIAAAAAAAAAAA==";

    [Theory]
    [InlineData(Etc1sWithAlpha, 1, 1, true, 87, 161, 219, 128)]
    [InlineData(UastcWithAlpha, 1, 1, true, 90, 160, 220, 128)]
    [InlineData(Etc1s, 64, 64, false, 202, 79, 37, 255)]
    [InlineData(UastcZstd, 64, 64, false, 200, 80, 40, 255)]
    public void Basis_Files_Decode_To_The_Reference_Pixels(string base64, int width, int height, bool alpha, int r, int g, int b, int a)
    {
        var data = Convert.FromBase64String(base64);
        Ktx2Decoder.IsKtx2(data).ShouldBeTrue();
        var image = Ktx2Decoder.Decode(data);
        image.Width.ShouldBe(width);
        image.Height.ShouldBe(height);
        image.HasAlpha.ShouldBe(alpha);
        image.IsSrgb.ShouldBeTrue();
        var level = image.Levels[0];
        level.Length.ShouldBe(width * height * 4);
        for (var i = 0; i < level.Length; i += 4)
        {
            (level[i], level[i + 1], level[i + 2], level[i + 3]).ShouldBe(((byte)r, (byte)g, (byte)b, (byte)a));
        }

        // The image decoders take it like a PNG.
        ImageDecoders.IsKnownFormat(data).ShouldBeTrue();
        var decoded = ImageDecoders.Decode(data);
        decoded.Width.ShouldBe(width);
        ((byte[])decoded.Data)[..4].ShouldBe([(byte)r, (byte)g, (byte)b, (byte)a]);
    }

    [Fact]
    public void Damaged_Files_Are_Refused_As_Invalid()
    {
        Ktx2Decoder.IsKtx2([1, 2, 3]).ShouldBeFalse();
        var random = new Random(20261117);
        foreach (var base64 in (string[])[Etc1sWithAlpha, UastcWithAlpha, Etc1s, UastcZstd])
        {
            var source = Convert.FromBase64String(base64);
            for (var i = 0; i < 400; i++)
            {
                var damaged = (byte[])source.Clone();
                if (i % 2 == 0)
                {
                    damaged = damaged[..random.Next(damaged.Length)];
                }
                else
                {
                    for (var k = random.Next(1, 6); k > 0; k--)
                    {
                        damaged[random.Next(12, damaged.Length)] ^= (byte)(1 << random.Next(8));
                    }
                }

                try
                {
                    Ktx2Decoder.Decode(damaged).Levels.Count.ShouldBeGreaterThan(0);
                }
                catch (InvalidDataException)
                {
                }
                catch (NotSupportedException)
                {
                }
            }
        }
    }

    [Fact]
    public void A_Gltf_Texture_Takes_Its_Image_From_The_Basis_Extension()
    {
        var b = new GltfBuilder();
        var image = b.View(Convert.FromBase64String(UastcZstd));
        var position = b.Floats("VEC3", 0, 0, 0, 1, 0, 0, 0, 1, 0);
        var uv = b.Floats("VEC2", 0, 0, 1, 0, 0, 1);
        var body = "\"extensionsUsed\":[\"KHR_texture_basisu\"],\"extensionsRequired\":[\"KHR_texture_basisu\"],"
            + $"\"images\":[{{\"bufferView\":{image},\"mimeType\":\"image/ktx2\"}}],"
            + "\"textures\":[{\"extensions\":{\"KHR_texture_basisu\":{\"source\":0}}}],"
            + "\"materials\":[{\"pbrMetallicRoughness\":{\"baseColorTexture\":{\"index\":0}}}],"
            + "\"scenes\":[{\"nodes\":[0]}],\"nodes\":[{\"mesh\":0}],"
            + $"\"meshes\":[{{\"primitives\":[{{\"attributes\":{{\"POSITION\":{position},\"TEXCOORD_0\":{uv}}},\"material\":0}}]}}]";
        using var asset = GltfLoader.Load(b.Glb(body));

        asset.Warnings.ShouldBeEmpty();
        var map = asset.Materials.ShouldHaveSingleItem().ShouldBeOfType<StandardMaterial>().Map.ShouldNotBeNull();
        map.Image.ShouldNotBeNull().Width.ShouldBe(64);
        map.ColorSpace.ShouldBe(TextureColorSpace.Srgb);
        ((byte[])map.Image.Data)[..3].ShouldBe([(byte)200, (byte)80, (byte)40]);
    }
}
