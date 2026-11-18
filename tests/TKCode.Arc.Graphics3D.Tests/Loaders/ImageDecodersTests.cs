using System.Text;

namespace TKCode.Arc.Graphics3D.Tests;

public class ImageDecodersTests
{
    [Fact]
    public void Built_In_Formats_Are_Recognized_By_Their_Signature()
    {
        var png = Png.Encode(2, 1, [255, 0, 0, 255, 0, 255, 0, 128]);
        var fromPng = ImageDecoders.Decode(png);
        fromPng.Width.ShouldBe(2);
        RenderHarness.Pixel(fromPng, 1, 0).ShouldBe(((byte)0, (byte)255, (byte)0, (byte)128));

        var fromJpeg = ImageDecoders.Decode(JpegFixtures.Bytes(JpegFixtures.Gray));
        fromJpeg.Width.ShouldBe(32);
        fromJpeg.Format.ShouldBe(Rhi.TextureFormat.RGBA8);

        byte[] hdr = [.. Encoding.ASCII.GetBytes("#?RADIANCE\nFORMAT=32-bit_rle_rgbe\n\n-Y 1 +X 1\n"), 128, 128, 128, 129];
        ImageDecoders.Decode(hdr).Format.ShouldBe(Rhi.TextureFormat.RGBA32F);
        ImageDecoders.IsKnownFormat(hdr).ShouldBeTrue();
    }

    [Fact]
    public void Unknown_Format_Names_The_First_Bytes_And_The_Way_Out()
    {
        byte[] webp = [.. "RIFF"u8, 0, 0, 0, 0, .. "WEBP"u8];
        ImageDecoders.IsKnownFormat(webp).ShouldBeFalse();
        var e = Should.Throw<NotSupportedException>(() => ImageDecoders.Decode(webp));
        e.Message.ShouldContain("52494646");
        e.Message.ShouldContain("ImageDecoders.Register");
    }

    [Fact]
    public void Registered_Decoder_Is_Asked_First_And_Can_Be_Removed()
    {
        // A made-up format that only this test produces, so other tests running in parallel are not affected.
        byte[] custom = [.. "ARCTESTIMG"u8, 7];
        TextureImage? Decoder(ReadOnlyMemory<byte> data) =>
            data.Span.StartsWith("ARCTESTIMG"u8) ? TextureImage.Solid(data.Span[10], 0, 0) : null;
        Func<ReadOnlyMemory<byte>, TextureImage?> decoder = Decoder;
        ImageDecoders.Register(decoder);
        try
        {
            RenderHarness.Pixel(ImageDecoders.Decode(custom), 0, 0).R.ShouldBe((byte)7);
            RenderHarness.Pixel(ImageDecoders.Decode(new ReadOnlyMemory<byte>(custom)), 0, 0).R.ShouldBe((byte)7);

            // Data the custom decoder declines still reaches the built-in ones.
            ImageDecoders.Decode(Png.Encode(1, 1, [1, 2, 3, 4])).Width.ShouldBe(1);
        }
        finally
        {
            ImageDecoders.Unregister(decoder).ShouldBeTrue();
        }

        ImageDecoders.Unregister(decoder).ShouldBeFalse();
        Should.Throw<NotSupportedException>(() => ImageDecoders.Decode(custom));
    }
}
