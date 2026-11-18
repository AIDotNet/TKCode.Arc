using System.Buffers.Binary;
using System.IO.Compression;

namespace TKCode.Arc.Graphics3D;

/// <summary>Reads and writes PNG images without any other library: 8 and 16 bits per channel, grayscale, RGB, palette
/// and alpha, interlaced or not. Decoded images are always 8-bit RGBA, rows top to bottom.</summary>
public static class Png
{
    private static readonly byte[] s_signature = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];
    private static readonly uint[] s_crcTable = BuildCrcTable();

    private static uint[] BuildCrcTable()
    {
        var table = new uint[256];
        for (uint n = 0; n < 256; n++)
        {
            var c = n;
            for (var k = 0; k < 8; k++)
            {
                c = (c & 1) != 0 ? 0xEDB88320u ^ (c >> 1) : c >> 1;
            }

            table[n] = c;
        }

        return table;
    }

    private static uint Crc(uint crc, ReadOnlySpan<byte> data)
    {
        foreach (var b in data)
        {
            crc = s_crcTable[(crc ^ b) & 0xFF] ^ (crc >> 8);
        }

        return crc;
    }

    /// <summary>True when the data starts with the PNG signature.</summary>
    public static bool IsPng(ReadOnlySpan<byte> data) => data.Length >= 8 && data[..8].SequenceEqual(s_signature);

    /// <summary>Encodes 8-bit RGBA pixels (rows top to bottom) as a PNG file.</summary>
    public static byte[] Encode(int width, int height, ReadOnlySpan<byte> rgba)
    {
        if (rgba.Length < width * height * 4)
        {
            throw new ArgumentException("The pixel data is too small.", nameof(rgba));
        }

        using var output = new MemoryStream();
        output.Write(s_signature);
        Span<byte> header = stackalloc byte[13];
        BinaryPrimitives.WriteInt32BigEndian(header, width);
        BinaryPrimitives.WriteInt32BigEndian(header[4..], height);
        header[8] = 8;
        header[9] = 6;
        WriteChunk(output, "IHDR"u8, header);
        using (var body = new MemoryStream())
        {
            using (var z = new ZLibStream(body, CompressionLevel.Fastest, leaveOpen: true))
            {
                var row = width * 4;
                for (var y = 0; y < height; y++)
                {
                    z.WriteByte(0);
                    z.Write(rgba.Slice(y * row, row));
                }
            }

            WriteChunk(output, "IDAT"u8, body.GetBuffer().AsSpan(0, (int)body.Length));
        }

        WriteChunk(output, "IEND"u8, default);
        return output.ToArray();
    }

    /// <summary>Encodes an RGBA8 image as a PNG file.</summary>
    public static byte[] Encode(TextureImage image)
    {
        if (image.Format is not (Rhi.TextureFormat.RGBA8 or Rhi.TextureFormat.RGBA8Srgb))
        {
            throw new NotSupportedException("Only RGBA8 images can be encoded.");
        }

        return Encode(image.Width, image.Height, image.AsBytes());
    }

    private static void WriteChunk(Stream output, ReadOnlySpan<byte> type, ReadOnlySpan<byte> data)
    {
        Span<byte> word = stackalloc byte[4];
        BinaryPrimitives.WriteInt32BigEndian(word, data.Length);
        output.Write(word);
        output.Write(type);
        output.Write(data);
        var crc = Crc(Crc(0xFFFFFFFFu, type), data) ^ 0xFFFFFFFFu;
        BinaryPrimitives.WriteUInt32BigEndian(word, crc);
        output.Write(word);
    }

    /// <summary>Decodes a PNG file to an 8-bit RGBA image.</summary>
    public static TextureImage Decode(ReadOnlySpan<byte> data)
    {
        if (!IsPng(data))
        {
            throw new InvalidDataException("Not a PNG file.");
        }

        int width = 0, height = 0, bitDepth = 0, colorType = 0, interlace = 0;
        byte[]? palette = null;
        byte[]? transparency = null;
        using var compressed = new MemoryStream();
        var position = 8;
        var sawHeader = false;
        while (position + 12 <= data.Length)
        {
            var length = BinaryPrimitives.ReadInt32BigEndian(data[position..]);
            var type = data.Slice(position + 4, 4);
            if (length < 0 || position + 12 + length > data.Length)
            {
                throw new InvalidDataException("The PNG file is truncated.");
            }

            var body = data.Slice(position + 8, length);
            if (type.SequenceEqual("IHDR"u8))
            {
                width = BinaryPrimitives.ReadInt32BigEndian(body);
                height = BinaryPrimitives.ReadInt32BigEndian(body[4..]);
                bitDepth = body[8];
                colorType = body[9];
                interlace = body[12];
                sawHeader = true;
            }
            else if (type.SequenceEqual("PLTE"u8))
            {
                palette = body.ToArray();
            }
            else if (type.SequenceEqual("tRNS"u8))
            {
                transparency = body.ToArray();
            }
            else if (type.SequenceEqual("IDAT"u8))
            {
                compressed.Write(body);
            }
            else if (type.SequenceEqual("IEND"u8))
            {
                break;
            }

            position += 12 + length;
        }

        if (!sawHeader || width <= 0 || height <= 0 || (long)width * height > 1 << 28)
        {
            throw new InvalidDataException("The PNG header is invalid.");
        }

        var channels = colorType switch
        {
            0 => 1,
            2 => 3,
            3 => 1,
            4 => 2,
            6 => 4,
            _ => throw new InvalidDataException($"PNG color type {colorType} is invalid."),
        };
        var bitsPerPixel = channels * bitDepth;
        var bytesPerPixel = Math.Max(1, bitsPerPixel / 8);
        compressed.Position = 0;
        using var z = new ZLibStream(compressed, CompressionMode.Decompress);
        var rgba = new byte[width * height * 4];
        if (interlace == 0)
        {
            var stride = ((width * bitsPerPixel) + 7) / 8;
            var raw = ReadPass(z, stride, height, bytesPerPixel);
            for (var y = 0; y < height; y++)
            {
                Expand(raw.AsSpan(y * stride, stride), rgba, y, 0, 1, width, width, bitDepth, colorType, palette, transparency);
            }
        }
        else
        {
            ReadOnlySpan<int> startX = [0, 4, 0, 2, 0, 1, 0];
            ReadOnlySpan<int> startY = [0, 0, 4, 0, 2, 0, 1];
            ReadOnlySpan<int> stepX = [8, 8, 4, 4, 2, 2, 1];
            ReadOnlySpan<int> stepY = [8, 8, 8, 4, 4, 2, 2];
            for (var pass = 0; pass < 7; pass++)
            {
                var pw = (width - startX[pass] + stepX[pass] - 1) / stepX[pass];
                var ph = (height - startY[pass] + stepY[pass] - 1) / stepY[pass];
                if (pw <= 0 || ph <= 0)
                {
                    continue;
                }

                var stride = ((pw * bitsPerPixel) + 7) / 8;
                var raw = ReadPass(z, stride, ph, bytesPerPixel);
                for (var y = 0; y < ph; y++)
                {
                    Expand(raw.AsSpan(y * stride, stride), rgba, startY[pass] + (y * stepY[pass]), startX[pass], stepX[pass], pw, width, bitDepth, colorType, palette, transparency);
                }
            }
        }

        return new TextureImage(width, height, rgba);
    }

    private static byte[] ReadPass(Stream z, int stride, int rows, int bpp)
    {
        var raw = new byte[stride * rows];
        var line = new byte[stride + 1];
        for (var y = 0; y < rows; y++)
        {
            z.ReadExactly(line);
            var current = raw.AsSpan(y * stride, stride);
            line.AsSpan(1).CopyTo(current);
            var previous = y > 0 ? raw.AsSpan((y - 1) * stride, stride) : default;
            switch (line[0])
            {
                case 1:
                    for (var i = bpp; i < stride; i++)
                    {
                        current[i] += current[i - bpp];
                    }

                    break;
                case 2:
                    if (y > 0)
                    {
                        for (var i = 0; i < stride; i++)
                        {
                            current[i] += previous[i];
                        }
                    }

                    break;
                case 3:
                    for (var i = 0; i < stride; i++)
                    {
                        var left = i >= bpp ? current[i - bpp] : 0;
                        var up = y > 0 ? previous[i] : 0;
                        current[i] += (byte)((left + up) >> 1);
                    }

                    break;
                case 4:
                    for (var i = 0; i < stride; i++)
                    {
                        int a = i >= bpp ? current[i - bpp] : 0;
                        int b = y > 0 ? previous[i] : 0;
                        int c = i >= bpp && y > 0 ? previous[i - bpp] : 0;
                        var p = a + b - c;
                        int pa = Math.Abs(p - a), pb = Math.Abs(p - b), pc = Math.Abs(p - c);
                        current[i] += (byte)(pa <= pb && pa <= pc ? a : pb <= pc ? b : c);
                    }

                    break;
            }
        }

        return raw;
    }

    private static void Expand(
        ReadOnlySpan<byte> row, byte[] rgba, int y, int x0, int dx, int count, int width, int bitDepth, int colorType, byte[]? palette, byte[]? transparency)
    {
        for (var i = 0; i < count; i++)
        {
            var o = ((y * width) + x0 + (i * dx)) * 4;
            byte r, g, b, a = 255;
            switch (colorType)
            {
                case 0:
                {
                    var v = Sample(row, i, 0, 1, bitDepth, out var rawValue);
                    r = g = b = v;
                    if (transparency is { Length: >= 2 } && rawValue == ((transparency[0] << 8) | transparency[1]))
                    {
                        a = 0;
                    }

                    break;
                }

                case 2:
                {
                    r = Sample(row, i, 0, 3, bitDepth, out var r0);
                    g = Sample(row, i, 1, 3, bitDepth, out var g0);
                    b = Sample(row, i, 2, 3, bitDepth, out var b0);
                    if (transparency is { Length: >= 6 } && r0 == ((transparency[0] << 8) | transparency[1])
                        && g0 == ((transparency[2] << 8) | transparency[3]) && b0 == ((transparency[4] << 8) | transparency[5]))
                    {
                        a = 0;
                    }

                    break;
                }

                case 3:
                {
                    Sample(row, i, 0, 1, bitDepth, out var index);
                    if (palette is null || (index * 3) + 2 >= palette.Length)
                    {
                        r = g = b = 0;
                    }
                    else
                    {
                        r = palette[index * 3];
                        g = palette[(index * 3) + 1];
                        b = palette[(index * 3) + 2];
                    }

                    if (transparency is not null && index < transparency.Length)
                    {
                        a = transparency[index];
                    }

                    break;
                }

                case 4:
                    r = g = b = Sample(row, i, 0, 2, bitDepth, out _);
                    a = Sample(row, i, 1, 2, bitDepth, out _);
                    break;
                default:
                    r = Sample(row, i, 0, 4, bitDepth, out _);
                    g = Sample(row, i, 1, 4, bitDepth, out _);
                    b = Sample(row, i, 2, 4, bitDepth, out _);
                    a = Sample(row, i, 3, 4, bitDepth, out _);
                    break;
            }

            rgba[o] = r;
            rgba[o + 1] = g;
            rgba[o + 2] = b;
            rgba[o + 3] = a;
        }
    }

    /// <summary>One channel of one pixel scaled to 8 bits; <paramref name="raw"/> is the unscaled value.</summary>
    private static byte Sample(ReadOnlySpan<byte> row, int pixel, int channel, int channels, int bitDepth, out int raw)
    {
        switch (bitDepth)
        {
            case 8:
                raw = row[(pixel * channels) + channel];
                return (byte)raw;
            case 16:
            {
                var o = ((pixel * channels) + channel) * 2;
                raw = (row[o] << 8) | row[o + 1];
                return row[o];
            }

            default:
            {
                var bit = pixel * bitDepth;
                var shift = 8 - bitDepth - (bit & 7);
                raw = (row[bit >> 3] >> shift) & ((1 << bitDepth) - 1);
                return (byte)(raw * 255 / ((1 << bitDepth) - 1));
            }
        }
    }
}
