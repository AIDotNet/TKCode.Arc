using System.Buffers.Binary;
using Avalonia;

namespace TKCode.Arc.Primitives;

/// <summary>
/// Reads a picture's pixel size from the first bytes of its file (PNG, JPEG, GIF, BMP, WebP), so a picture much larger
/// than its slot can be decoded straight to the slot's size (<c>Bitmap.DecodeToWidth</c>) without first decoding every
/// pixel. Anything it does not recognise reports false and the picture is decoded whole, as before.
/// </summary>
internal static class ArcImageHeader
{
    private const int MaxDimension = 1 << 16;

    /// <summary>Reads the size from <paramref name="stream"/> (seekable; its position is restored).</summary>
    public static bool TryReadSize(Stream stream, out PixelSize size)
    {
        size = default;
        if (!stream.CanSeek)
        {
            return false;
        }
        var position = stream.Position;
        try
        {
            Span<byte> header = stackalloc byte[32];
            var read = Fill(stream, header);
            return read >= 12 && TryParse(header[..read], stream, position, out size);
        }
        catch (IOException)
        {
            return false;
        }
        finally
        {
            stream.Position = position;
        }
    }

    /// <summary>Reads the size from the start of <paramref name="bytes"/> (a whole file, or at least its header).</summary>
    public static bool TryReadSize(ReadOnlySpan<byte> bytes, out PixelSize size)
    {
        using var stream = new MemoryStream(bytes.ToArray(), writable: false);
        return TryReadSize(stream, out size);
    }

    private static bool TryParse(ReadOnlySpan<byte> h, Stream stream, long start, out PixelSize size)
    {
        size = default;
        // PNG: signature, then the IHDR chunk with width and height as big-endian 32-bit integers.
        if (h.Length >= 24 && h[0] == 0x89 && h[1] == (byte)'P' && h[2] == (byte)'N' && h[3] == (byte)'G'
            && h[12] == (byte)'I' && h[13] == (byte)'H' && h[14] == (byte)'D' && h[15] == (byte)'R')
        {
            return Accept((int)BinaryPrimitives.ReadUInt32BigEndian(h[16..]), (int)BinaryPrimitives.ReadUInt32BigEndian(h[20..]), out size);
        }
        // GIF: "GIF87a" / "GIF89a", then the logical screen size as little-endian 16-bit integers.
        if (h.Length >= 10 && h[0] == (byte)'G' && h[1] == (byte)'I' && h[2] == (byte)'F' && h[3] == (byte)'8'
            && (h[4] == (byte)'7' || h[4] == (byte)'9') && h[5] == (byte)'a')
        {
            return Accept(BinaryPrimitives.ReadUInt16LittleEndian(h[6..]), BinaryPrimitives.ReadUInt16LittleEndian(h[8..]), out size);
        }
        // BMP: "BM", a 14-byte file header, then the info header (BITMAPINFOHEADER and later: 32-bit signed size;
        // the old BITMAPCOREHEADER: 16-bit). A negative height means top-down rows.
        if (h.Length >= 26 && h[0] == (byte)'B' && h[1] == (byte)'M')
        {
            var infoSize = BinaryPrimitives.ReadUInt32LittleEndian(h[14..]);
            if (infoSize == 12)
            {
                return Accept(BinaryPrimitives.ReadUInt16LittleEndian(h[18..]), BinaryPrimitives.ReadUInt16LittleEndian(h[20..]), out size);
            }
            var width = BinaryPrimitives.ReadInt32LittleEndian(h[18..]);
            var height = BinaryPrimitives.ReadInt32LittleEndian(h[22..]);
            return Accept(width, Math.Abs(height), out size);
        }
        // WebP: RIFF container; the first chunk says which bitstream: VP8 (lossy), VP8L (lossless) or VP8X (extended).
        if (h.Length >= 30 && h[0] == (byte)'R' && h[1] == (byte)'I' && h[2] == (byte)'F' && h[3] == (byte)'F'
            && h[8] == (byte)'W' && h[9] == (byte)'E' && h[10] == (byte)'B' && h[11] == (byte)'P')
        {
            return TryParseWebP(h, out size);
        }
        // JPEG: SOI, then marker segments until a start-of-frame segment carries the height and width (big-endian).
        if (h[0] == 0xFF && h[1] == 0xD8)
        {
            return TryParseJpeg(stream, start + 2, out size);
        }
        return false;
    }

    private static bool TryParseWebP(ReadOnlySpan<byte> h, out PixelSize size)
    {
        size = default;
        if (h[12] == (byte)'V' && h[13] == (byte)'P' && h[14] == (byte)'8' && h[15] == (byte)' ')
        {
            // Lossy: a 10-byte frame header after the chunk header, the start code 9D 01 2A, then 14-bit width and height.
            if (h[23] != 0x9D || h[24] != 0x01 || h[25] != 0x2A)
            {
                return false;
            }
            var width = BinaryPrimitives.ReadUInt16LittleEndian(h[26..]) & 0x3FFF;
            var height = BinaryPrimitives.ReadUInt16LittleEndian(h[28..]) & 0x3FFF;
            return Accept(width, height, out size);
        }
        if (h[12] == (byte)'V' && h[13] == (byte)'P' && h[14] == (byte)'8' && h[15] == (byte)'L')
        {
            // Lossless: signature 2F, then 14-bit width − 1 and 14-bit height − 1 packed little-endian.
            if (h[20] != 0x2F)
            {
                return false;
            }
            var bits = BinaryPrimitives.ReadUInt32LittleEndian(h[21..]);
            var width = (int)(bits & 0x3FFF) + 1;
            var height = (int)((bits >> 14) & 0x3FFF) + 1;
            return Accept(width, height, out size);
        }
        if (h[12] == (byte)'V' && h[13] == (byte)'P' && h[14] == (byte)'8' && h[15] == (byte)'X')
        {
            // Extended: flags, then 24-bit canvas width − 1 and height − 1.
            var width = (h[24] | (h[25] << 8) | (h[26] << 16)) + 1;
            var height = (h[27] | (h[28] << 8) | (h[29] << 16)) + 1;
            return Accept(width, height, out size);
        }
        return false;
    }

    private static bool TryParseJpeg(Stream stream, long position, out PixelSize size)
    {
        size = default;
        Span<byte> buffer = stackalloc byte[9];
        stream.Position = position;
        // Each segment: FF, marker, big-endian length (including the two length bytes). Frame markers are C0–CF except
        // C4 (Huffman tables), C8 (reserved) and CC (arithmetic conditioning).
        for (var i = 0; i < 1024; i++)
        {
            if (Fill(stream, buffer[..2]) < 2 || buffer[0] != 0xFF)
            {
                return false;
            }
            var marker = buffer[1];
            while (marker == 0xFF)
            {
                if (Fill(stream, buffer[..1]) < 1)
                {
                    return false;
                }
                marker = buffer[0];
            }
            if (marker == 0xD8 || (marker >= 0xD0 && marker <= 0xD7) || marker == 0x01)
            {
                // Markers without a segment.
                continue;
            }
            if (marker == 0xD9 || marker == 0xDA)
            {
                // End of image, or scan data without a frame before it.
                return false;
            }
            if (Fill(stream, buffer[..2]) < 2)
            {
                return false;
            }
            var length = BinaryPrimitives.ReadUInt16BigEndian(buffer);
            if (length < 2)
            {
                return false;
            }
            if (marker >= 0xC0 && marker <= 0xCF && marker != 0xC4 && marker != 0xC8 && marker != 0xCC)
            {
                if (length < 7 || Fill(stream, buffer[..5]) < 5)
                {
                    return false;
                }
                var height = BinaryPrimitives.ReadUInt16BigEndian(buffer[1..]);
                var width = BinaryPrimitives.ReadUInt16BigEndian(buffer[3..]);
                return Accept(width, height, out size);
            }
            stream.Position += length - 2;
        }
        return false;
    }

    private static bool Accept(int width, int height, out PixelSize size)
    {
        if (width <= 0 || height <= 0 || width > MaxDimension || height > MaxDimension)
        {
            size = default;
            return false;
        }
        size = new PixelSize(width, height);
        return true;
    }

    private static int Fill(Stream stream, Span<byte> buffer)
    {
        var total = 0;
        while (total < buffer.Length)
        {
            var read = stream.Read(buffer[total..]);
            if (read <= 0)
            {
                break;
            }
            total += read;
        }
        return total;
    }
}
