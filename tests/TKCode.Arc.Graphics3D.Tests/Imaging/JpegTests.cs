namespace TKCode.Arc.Graphics3D.Tests;

public class JpegTests
{
    private static readonly (int R, int G, int B)[] s_quadrants = [(220, 30, 30), (30, 200, 40), (30, 60, 220), (240, 240, 240)];

    private static void ShouldShowQuadrants(TextureImage image, int width, int height, int tolerance, (int R, int G, int B)[]? expected = null)
    {
        image.Width.ShouldBe(width);
        image.Height.ShouldBe(height);
        image.Format.ShouldBe(Rhi.TextureFormat.RGBA8);
        expected ??= s_quadrants;
        (int X, int Y)[] points = [(width / 4, height / 4), (3 * width / 4, height / 4), (width / 4, 3 * height / 4), (3 * width / 4, 3 * height / 4)];
        for (var i = 0; i < 4; i++)
        {
            var (r, g, b, a) = RenderHarness.Pixel(image, points[i].X, points[i].Y);
            Math.Abs(r - expected[i].R).ShouldBeLessThanOrEqualTo(tolerance, $"red of quadrant {i}");
            Math.Abs(g - expected[i].G).ShouldBeLessThanOrEqualTo(tolerance, $"green of quadrant {i}");
            Math.Abs(b - expected[i].B).ShouldBeLessThanOrEqualTo(tolerance, $"blue of quadrant {i}");
            a.ShouldBe((byte)255);
        }
    }

    [Fact]
    public void Baseline_With_420_Subsampling_Decodes()
    {
        ShouldShowQuadrants(Jpeg.Decode(JpegFixtures.Bytes(JpegFixtures.Baseline420)), 32, 24, 6);
    }

    [Fact]
    public void Progressive_Decodes_Like_Baseline()
    {
        var progressive = Jpeg.Decode(JpegFixtures.Bytes(JpegFixtures.Progressive420));
        ShouldShowQuadrants(progressive, 32, 24, 6);

        // The same picture at the same quality: the two coding modes must give the same pixels.
        var baseline = (byte[])Jpeg.Decode(JpegFixtures.Bytes(JpegFixtures.Baseline420)).Data;
        var pixels = (byte[])progressive.Data;
        for (var i = 0; i < pixels.Length; i++)
        {
            Math.Abs(pixels[i] - baseline[i]).ShouldBeLessThanOrEqualTo(1);
        }
    }

    [Fact]
    public void Grayscale_Decodes_To_Gray_Rgba()
    {
        var image = Jpeg.Decode(JpegFixtures.Bytes(JpegFixtures.Gray));
        ShouldShowQuadrants(image, 32, 24, 3, [(87, 87, 87), (131, 131, 131), (69, 69, 69), (240, 240, 240)]);
    }

    [Fact]
    public void Restart_Intervals_Without_Subsampling_Decode()
    {
        ShouldShowQuadrants(Jpeg.Decode(JpegFixtures.Bytes(JpegFixtures.Restart444)), 32, 24, 4);
    }

    [Fact]
    public void Horizontal_Only_Subsampling_Decodes()
    {
        ShouldShowQuadrants(Jpeg.Decode(JpegFixtures.Bytes(JpegFixtures.H2v1)), 32, 24, 6);
    }

    [Fact]
    public void Size_That_Is_Not_A_Multiple_Of_The_Block_Size_Decodes()
    {
        var image = Jpeg.Decode(JpegFixtures.Bytes(JpegFixtures.Odd420));
        ShouldShowQuadrants(image, 21, 13, 8);

        // The last column and row come from partial blocks: they must hold picture, not padding.
        var corner = RenderHarness.Pixel(image, 20, 12);
        corner.R.ShouldBeGreaterThan((byte)215);
        corner.G.ShouldBeGreaterThan((byte)215);
        corner.B.ShouldBeGreaterThan((byte)215);
    }

    [Fact]
    public void Adobe_Cmyk_Decodes_To_Rgb()
    {
        ShouldShowQuadrants(Jpeg.Decode(JpegFixtures.Bytes(JpegFixtures.Cmyk)), 32, 24, 6);
    }

    [Fact]
    public void Arithmetic_Coding_Is_Rejected_With_A_Clear_Message()
    {
        var e = Should.Throw<NotSupportedException>(() => Jpeg.Decode(JpegFixtures.Bytes(JpegFixtures.Arith)));
        e.Message.ShouldContain("arithmetic");
    }

    [Fact]
    public void Twelve_Bit_Samples_Are_Rejected_With_A_Clear_Message()
    {
        // Patch the sample precision of the frame header (the byte after the segment length of SOF0).
        var data = JpegFixtures.Bytes(JpegFixtures.Baseline420);
        var patched = false;
        for (var i = 2; i + 4 < data.Length; i++)
        {
            if (data[i] == 0xFF && data[i + 1] == 0xC0)
            {
                data[i + 4] = 12;
                patched = true;
                break;
            }
        }

        patched.ShouldBeTrue();
        var e = Should.Throw<NotSupportedException>(() => Jpeg.Decode(data));
        e.Message.ShouldContain("12 bits");
    }

    [Fact]
    public void Data_That_Is_Not_Jpeg_Is_Rejected()
    {
        Jpeg.IsJpeg([1, 2, 3, 4]).ShouldBeFalse();
        Should.Throw<InvalidDataException>(() => Jpeg.Decode([1, 2, 3, 4]));
    }

    [Theory]
    [InlineData(0.9)]
    [InlineData(0.6)]
    [InlineData(0.35)]
    public void Truncated_Files_Do_Not_Crash(double keep)
    {
        foreach (var fixture in (string[])[JpegFixtures.Baseline420, JpegFixtures.Progressive420, JpegFixtures.Restart444])
        {
            var data = JpegFixtures.Bytes(fixture);
            var cut = data.AsSpan(0, (int)(data.Length * keep)).ToArray();
            try
            {
                var image = Jpeg.Decode(cut);
                image.Width.ShouldBe(32);
                image.Height.ShouldBe(24);
            }
            catch (InvalidDataException)
            {
                // Cut inside a header: a clean rejection is fine too.
            }
        }
    }
}
