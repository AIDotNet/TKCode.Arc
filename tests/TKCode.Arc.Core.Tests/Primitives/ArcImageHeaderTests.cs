using System.Buffers.Binary;
using Avalonia;
using Shouldly;
using TKCode.Arc.Primitives;
using Xunit;

namespace TKCode.Arc.Core.Tests.Primitives;

/// <summary>The header reader that lets a large picture decode straight to its slot's size.</summary>
public class ArcImageHeaderTests
{
    [Fact]
    public void Reads_Png()
    {
        var bytes = new byte[33];
        new byte[] { 0x89, (byte)'P', (byte)'N', (byte)'G', 0x0D, 0x0A, 0x1A, 0x0A }.CopyTo(bytes, 0);
        BinaryPrimitives.WriteUInt32BigEndian(bytes.AsSpan(8), 13);
        "IHDR"u8.CopyTo(bytes.AsSpan(12));
        BinaryPrimitives.WriteUInt32BigEndian(bytes.AsSpan(16), 4000);
        BinaryPrimitives.WriteUInt32BigEndian(bytes.AsSpan(20), 3000);
        ArcImageHeader.TryReadSize(bytes, out var size).ShouldBeTrue();
        size.ShouldBe(new PixelSize(4000, 3000));
    }

    [Fact]
    public void Reads_Gif_And_Bmp()
    {
        var gif = new byte[16];
        "GIF89a"u8.CopyTo(gif);
        BinaryPrimitives.WriteUInt16LittleEndian(gif.AsSpan(6), 640);
        BinaryPrimitives.WriteUInt16LittleEndian(gif.AsSpan(8), 400);
        ArcImageHeader.TryReadSize(gif, out var gifSize).ShouldBeTrue();
        gifSize.ShouldBe(new PixelSize(640, 400));

        var bmp = new byte[40];
        bmp[0] = (byte)'B';
        bmp[1] = (byte)'M';
        BinaryPrimitives.WriteUInt32LittleEndian(bmp.AsSpan(14), 40);
        BinaryPrimitives.WriteInt32LittleEndian(bmp.AsSpan(18), 1280);
        BinaryPrimitives.WriteInt32LittleEndian(bmp.AsSpan(22), -720);
        ArcImageHeader.TryReadSize(bmp, out var bmpSize).ShouldBeTrue();
        bmpSize.ShouldBe(new PixelSize(1280, 720));
    }

    [Fact]
    public void Reads_WebP_Variants()
    {
        static byte[] Riff(string fourcc)
        {
            var bytes = new byte[40];
            "RIFF"u8.CopyTo(bytes);
            "WEBP"u8.CopyTo(bytes.AsSpan(8));
            System.Text.Encoding.ASCII.GetBytes(fourcc).CopyTo(bytes, 12);
            return bytes;
        }

        var lossy = Riff("VP8 ");
        lossy[23] = 0x9D;
        lossy[24] = 0x01;
        lossy[25] = 0x2A;
        BinaryPrimitives.WriteUInt16LittleEndian(lossy.AsSpan(26), 1024);
        BinaryPrimitives.WriteUInt16LittleEndian(lossy.AsSpan(28), 768);
        ArcImageHeader.TryReadSize(lossy, out var lossySize).ShouldBeTrue();
        lossySize.ShouldBe(new PixelSize(1024, 768));

        var lossless = Riff("VP8L");
        lossless[20] = 0x2F;
        BinaryPrimitives.WriteUInt32LittleEndian(lossless.AsSpan(21), (uint)((1023) | (767 << 14)));
        ArcImageHeader.TryReadSize(lossless, out var losslessSize).ShouldBeTrue();
        losslessSize.ShouldBe(new PixelSize(1024, 768));

        var extended = Riff("VP8X");
        extended[24] = 0xFF;
        extended[25] = 0x03;
        extended[27] = 0xFF;
        extended[28] = 0x02;
        ArcImageHeader.TryReadSize(extended, out var extendedSize).ShouldBeTrue();
        extendedSize.ShouldBe(new PixelSize(1024, 768));
    }

    [Fact]
    public void Reads_Jpeg_Past_Other_Segments()
    {
        var bytes = new List<byte> { 0xFF, 0xD8 };
        // APP0 (JFIF) segment of 16 bytes, then a comment, then the baseline frame.
        bytes.AddRange([0xFF, 0xE0, 0x00, 0x10]);
        bytes.AddRange(new byte[14]);
        bytes.AddRange([0xFF, 0xFE, 0x00, 0x05, (byte)'a', (byte)'b', (byte)'c']);
        bytes.AddRange([0xFF, 0xC0, 0x00, 0x11, 0x08]);
        bytes.AddRange([0x0B, 0xB8, 0x0F, 0xA0]); // height 3000, width 4000
        bytes.AddRange(new byte[10]);
        ArcImageHeader.TryReadSize(bytes.ToArray(), out var size).ShouldBeTrue();
        size.ShouldBe(new PixelSize(4000, 3000));
    }

    [Fact]
    public void Rejects_What_It_Does_Not_Know_And_Restores_The_Position()
    {
        ArcImageHeader.TryReadSize(new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14 }, out _).ShouldBeFalse();
        ArcImageHeader.TryReadSize(new byte[] { 0xFF, 0xD8, 0xFF, 0xD9 }, out _).ShouldBeFalse();
        var zeroPng = new byte[33];
        new byte[] { 0x89, (byte)'P', (byte)'N', (byte)'G', 0x0D, 0x0A, 0x1A, 0x0A }.CopyTo(zeroPng, 0);
        "IHDR"u8.CopyTo(zeroPng.AsSpan(12));
        ArcImageHeader.TryReadSize(zeroPng, out _).ShouldBeFalse();

        using var stream = new MemoryStream(new byte[64]);
        stream.Position = 5;
        ArcImageHeader.TryReadSize(stream, out _).ShouldBeFalse();
        stream.Position.ShouldBe(5);
    }
}
