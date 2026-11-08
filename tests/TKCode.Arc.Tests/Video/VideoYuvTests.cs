using Shouldly;

namespace TKCode.Arc.Tests.Video;

/// <summary>YUV 4:2:0 to BGRA with scaling (the Android backend's frame path).</summary>
public class VideoYuvTests
{
    [Theory]
    // Limited-range BT.601 (SD) reference colours: (Y, U, V) -> (R, G, B).
    [InlineData(16, 128, 128, 0, 0, 0)]
    [InlineData(235, 128, 128, 255, 255, 255)]
    [InlineData(81, 90, 240, 255, 0, 0)]
    [InlineData(145, 54, 34, 0, 255, 0)]
    [InlineData(41, 240, 110, 0, 0, 255)]
    public void Sd_Colours_Convert_With_Bt601(int y, int u, int v, int r, int g, int b)
    {
        var pixel = ConvertSolid(y, u, v, 64, 48, chromaPixelStride: 1);
        Math.Abs(pixel.R - r).ShouldBeLessThanOrEqualTo(3);
        Math.Abs(pixel.G - g).ShouldBeLessThanOrEqualTo(3);
        Math.Abs(pixel.B - b).ShouldBeLessThanOrEqualTo(3);
        pixel.A.ShouldBe((byte)255);
    }

    [Fact]
    public void Hd_Pictures_Use_Bt709()
    {
        // BT.709 limited-range red is Y 63, U 102, V 240.
        var pixel = ConvertSolid(63, 102, 240, 1280, 720, chromaPixelStride: 2);
        Math.Abs(pixel.R - 255).ShouldBeLessThanOrEqualTo(3);
        Math.Abs(pixel.G - 0).ShouldBeLessThanOrEqualTo(3);
        Math.Abs(pixel.B - 0).ShouldBeLessThanOrEqualTo(3);
    }

    [Fact]
    public void Scales_Down_And_Reads_Interleaved_Chroma_With_Padded_Rows()
    {
        // Left half white, right half black, NV12-like chroma (pixel stride 2), rows padded to 80 bytes.
        const int width = 64;
        const int height = 32;
        const int stride = 80;
        var yPlane = new byte[stride * height];
        for (var row = 0; row < height; row++)
        {
            for (var x = 0; x < width; x++)
            {
                yPlane[row * stride + x] = x < width / 2 ? (byte)235 : (byte)16;
            }
        }
        var chroma = Enumerable.Repeat((byte)128, stride * height / 2).ToArray();
        var destination = new byte[16 * 8 * 4];
        VideoYuv.ToBgra(yPlane, stride, chroma, chroma.AsSpan(1), stride, 2, width, height, destination, 16, 8);
        for (var row = 0; row < 8; row++)
        {
            for (var x = 0; x < 16; x++)
            {
                var expected = x < 8 ? 255 : 0;
                Math.Abs(destination[(row * 16 + x) * 4] - expected).ShouldBeLessThanOrEqualTo(2, $"pixel {x},{row}");
            }
        }
    }

    [Fact]
    public void A_Too_Small_Destination_Is_Refused() =>
        Should.Throw<ArgumentException>(() => VideoYuv.ToBgra(new byte[4], 2, new byte[1], new byte[1], 1, 1, 2, 2, new byte[3], 1, 1));

    private static (byte B, byte G, byte R, byte A) ConvertSolid(int y, int u, int v, int width, int height, int chromaPixelStride)
    {
        var yPlane = Enumerable.Repeat((byte)y, width * height).ToArray();
        var chromaWidth = width / 2 * chromaPixelStride;
        var uPlane = Enumerable.Repeat((byte)u, chromaWidth * height / 2).ToArray();
        var vPlane = Enumerable.Repeat((byte)v, chromaWidth * height / 2).ToArray();
        var destination = new byte[8 * 6 * 4];
        VideoYuv.ToBgra(yPlane, width, uPlane, vPlane, chromaWidth, chromaPixelStride, width, height, destination, 8, 6);
        return (destination[0], destination[1], destination[2], destination[3]);
    }
}
