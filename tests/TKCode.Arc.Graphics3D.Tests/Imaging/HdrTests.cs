using System.Text;

namespace TKCode.Arc.Graphics3D.Tests;

public class HdrTests
{
    private static TextureImage Gradient(int width, int height)
    {
        var pixels = new float[width * height * 4];
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var o = ((y * width) + x) * 4;
                // A wide range, with runs (constant blue per row) and literals (red changes every pixel).
                pixels[o] = 0.01f * (x + 1) * (y + 1);
                pixels[o + 1] = y == 0 ? 500f : 0.25f;
                pixels[o + 2] = y * 3f;
                pixels[o + 3] = 1f;
            }
        }

        return new TextureImage(width, height, Rhi.TextureFormat.RGBA32F, pixels);
    }

    private static void ShouldMatch(TextureImage expected, TextureImage actual)
    {
        actual.Width.ShouldBe(expected.Width);
        actual.Height.ShouldBe(expected.Height);
        actual.Format.ShouldBe(Rhi.TextureFormat.RGBA32F);
        var a = (float[])expected.Data;
        var b = (float[])actual.Data;
        for (var i = 0; i < a.Length; i += 4)
        {
            // RGBE shares one exponent per pixel: the error is relative to the brightest channel.
            var tolerance = (MathF.Max(a[i], MathF.Max(a[i + 1], a[i + 2])) / 128f) + 1e-6f;
            b[i].ShouldBe(a[i], tolerance);
            b[i + 1].ShouldBe(a[i + 1], tolerance);
            b[i + 2].ShouldBe(a[i + 2], tolerance);
            b[i + 3].ShouldBe(1f);
        }
    }

    [Fact]
    public void Run_Length_Encoded_Round_Trip_Keeps_Values()
    {
        var source = Gradient(40, 7);
        var file = Hdr.Encode(source);
        Hdr.IsHdr(file).ShouldBeTrue();
        // The new run-length format starts every scanline with 2, 2, width.
        var header = Encoding.ASCII.GetString(file, 0, 64);
        header.ShouldContain("-Y 7 +X 40");
        ShouldMatch(source, Hdr.Decode(file));
    }

    [Fact]
    public void Narrow_Images_Are_Stored_Flat_And_Round_Trip()
    {
        var source = Gradient(5, 3);
        ShouldMatch(source, Hdr.Decode(Hdr.Encode(source)));
    }

    [Fact]
    public void Flat_File_Decodes_To_Known_Values()
    {
        // (128, 64, 32, 129) = (1.0, 0.5, 0.25): mantissa / 256 * 2^(129 - 128).
        var header = Encoding.ASCII.GetBytes("#?RADIANCE\n# made by a test\nFORMAT=32-bit_rle_rgbe\n\n-Y 1 +X 2\n");
        byte[] file = [.. header, 128, 64, 32, 129, 0, 0, 0, 0];
        var image = Hdr.Decode(file);
        image.Width.ShouldBe(2);
        image.Height.ShouldBe(1);
        var pixels = (float[])image.Data;
        pixels[0].ShouldBe(1f, 1e-6f);
        pixels[1].ShouldBe(0.5f, 1e-6f);
        pixels[2].ShouldBe(0.25f, 1e-6f);
        pixels[3].ShouldBe(1f);
        pixels[4].ShouldBe(0f);
        pixels[5].ShouldBe(0f);
        pixels[6].ShouldBe(0f);
        pixels[7].ShouldBe(1f);
    }

    [Fact]
    public void Old_Run_Length_Pixels_Repeat_The_Previous_Pixel()
    {
        var header = Encoding.ASCII.GetBytes("#?RGBE\nFORMAT=32-bit_rle_rgbe\n\n-Y 1 +X 4\n");
        // One pixel, then "repeat it three times".
        byte[] file = [.. header, 128, 128, 128, 129, 1, 1, 1, 3];
        var pixels = (float[])Hdr.Decode(file).Data;
        for (var x = 0; x < 4; x++)
        {
            pixels[x * 4].ShouldBe(1f, 1e-6f);
        }
    }

    [Fact]
    public void Bottom_Up_Files_Are_Flipped_To_Top_Down()
    {
        var header = Encoding.ASCII.GetBytes("#?RADIANCE\nFORMAT=32-bit_rle_rgbe\n\n+Y 2 +X 1\n");
        // First stored row is the bottom row.
        byte[] file = [.. header, 128, 0, 0, 129, 0, 128, 0, 129];
        var pixels = (float[])Hdr.Decode(file).Data;
        pixels[1].ShouldBe(1f, 1e-6f, "the top pixel is the second stored row (green)");
        pixels[4].ShouldBe(1f, 1e-6f, "the bottom pixel is the first stored row (red)");
    }

    [Fact]
    public void Invalid_Data_Is_Rejected()
    {
        Should.Throw<InvalidDataException>(() => Hdr.Decode([1, 2, 3]));
        Should.Throw<InvalidDataException>(() => Hdr.Decode(Encoding.ASCII.GetBytes("#?RADIANCE\nFORMAT=32-bit_rle_rgbe\n\n-Y 4 +X 4\n")));
        Should.Throw<NotSupportedException>(() => Hdr.Decode(Encoding.ASCII.GetBytes("#?RADIANCE\nFORMAT=32-bit_rle_xyze\n\n-Y 1 +X 1\n\0\0\0\0")));
        Should.Throw<NotSupportedException>(() => Hdr.Encode(new TextureImage(1, 1, [0, 0, 0, 255])));
    }
}
