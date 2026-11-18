using System.Globalization;
using System.Text;
using TKCode.Arc.Graphics3D.Rhi;

namespace TKCode.Arc.Graphics3D;

/// <summary>Reads and writes Radiance HDR images (<c>.hdr</c>, RGBE): the usual container of equirectangular
/// environment maps. Decoded images are <see cref="TextureFormat.RGBA32F"/>, linear, rows top to bottom, alpha 1.</summary>
public static class Hdr
{
    /// <summary>True when the data starts with a Radiance signature.</summary>
    public static bool IsHdr(ReadOnlySpan<byte> data) => data.StartsWith("#?RADIANCE"u8) || data.StartsWith("#?RGBE"u8);

    /// <summary>Decodes a Radiance HDR file.</summary>
    public static TextureImage Decode(ReadOnlySpan<byte> data)
    {
        if (!IsHdr(data))
        {
            throw new InvalidDataException("Not a Radiance HDR file.");
        }

        var position = 0;
        var format = "32-bit_rle_rgbe";
        var exposure = 1f;
        string? resolution = null;
        var headerDone = false;
        while (position < data.Length)
        {
            var line = ReadLine(data, ref position);
            if (!headerDone)
            {
                if (line.Length == 0)
                {
                    headerDone = true;
                }
                else if (line.StartsWith("FORMAT=", StringComparison.Ordinal))
                {
                    format = line[7..].Trim();
                }
                else if (line.StartsWith("EXPOSURE=", StringComparison.Ordinal)
                    && float.TryParse(line[9..], NumberStyles.Float, CultureInfo.InvariantCulture, out var e) && e > 0f)
                {
                    exposure *= e;
                }

                continue;
            }

            resolution = line;
            break;
        }

        if (resolution is null)
        {
            throw new InvalidDataException("The HDR file has no resolution line.");
        }

        if (format != "32-bit_rle_rgbe")
        {
            throw new NotSupportedException($"HDR pixel format '{format}' is not supported (only 32-bit_rle_rgbe).");
        }

        var parts = resolution.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 4 || parts[0].Length != 2 || parts[2].Length != 2 || parts[0][1] != 'Y' || parts[2][1] != 'X'
            || !int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var height)
            || !int.TryParse(parts[3], NumberStyles.Integer, CultureInfo.InvariantCulture, out var width))
        {
            throw new NotSupportedException($"HDR resolution line '{resolution}' is not supported (expected '-Y height +X width').");
        }

        if (width <= 0 || height <= 0 || (long)width * height > 1 << 28)
        {
            throw new InvalidDataException("The HDR image size is invalid.");
        }

        var flipY = parts[0][0] == '+';
        var flipX = parts[2][0] == '-';
        var pixels = new float[width * height * 4];
        var scanline = new byte[width * 4];
        var scale = 1f / exposure;
        for (var row = 0; row < height; row++)
        {
            ReadScanline(data, ref position, scanline, width);
            var y = flipY ? height - 1 - row : row;
            for (var x = 0; x < width; x++)
            {
                var source = x * 4;
                var o = ((y * width) + (flipX ? width - 1 - x : x)) * 4;
                var exponent = scanline[source + 3];
                if (exponent != 0)
                {
                    var f = MathF.ScaleB(1f, exponent - 136) * scale;
                    pixels[o] = scanline[source] * f;
                    pixels[o + 1] = scanline[source + 1] * f;
                    pixels[o + 2] = scanline[source + 2] * f;
                }

                pixels[o + 3] = 1f;
            }
        }

        return new TextureImage(width, height, TextureFormat.RGBA32F, pixels);
    }

    private static string ReadLine(ReadOnlySpan<byte> data, ref int position)
    {
        var start = position;
        while (position < data.Length && data[position] != (byte)'\n')
        {
            position++;
        }

        var line = Encoding.ASCII.GetString(data[start..position]).TrimEnd('\r');
        position = Math.Min(position + 1, data.Length);
        return line;
    }

    private static void ReadScanline(ReadOnlySpan<byte> data, ref int position, byte[] scanline, int width)
    {
        if (position + 4 > data.Length)
        {
            throw new InvalidDataException("The HDR file is truncated.");
        }

        var newRle = width is >= 8 and <= 32767 && data[position] == 2 && data[position + 1] == 2 && (data[position + 2] & 0x80) == 0;
        if (newRle)
        {
            if (((data[position + 2] << 8) | data[position + 3]) != width)
            {
                throw new InvalidDataException("An HDR scanline has the wrong width.");
            }

            position += 4;
            for (var channel = 0; channel < 4; channel++)
            {
                var x = 0;
                while (x < width)
                {
                    if (position >= data.Length)
                    {
                        throw new InvalidDataException("The HDR file is truncated.");
                    }

                    int count = data[position++];
                    if (count > 128)
                    {
                        count -= 128;
                        if (count == 0 || x + count > width || position >= data.Length)
                        {
                            throw new InvalidDataException("An HDR scanline is corrupt.");
                        }

                        var value = data[position++];
                        for (var i = 0; i < count; i++)
                        {
                            scanline[((x++) * 4) + channel] = value;
                        }
                    }
                    else
                    {
                        if (count == 0 || x + count > width || position + count > data.Length)
                        {
                            throw new InvalidDataException("An HDR scanline is corrupt.");
                        }

                        for (var i = 0; i < count; i++)
                        {
                            scanline[((x++) * 4) + channel] = data[position++];
                        }
                    }
                }
            }

            return;
        }

        // Flat pixels, with the old run-length scheme: a pixel (1, 1, 1, n) repeats the previous pixel.
        var pixel = 0;
        var shift = 0;
        while (pixel < width)
        {
            if (position + 4 > data.Length)
            {
                throw new InvalidDataException("The HDR file is truncated.");
            }

            byte r = data[position], g = data[position + 1], b = data[position + 2], e = data[position + 3];
            position += 4;
            if (r == 1 && g == 1 && b == 1 && pixel > 0)
            {
                var repeat = e << shift;
                for (var i = 0; i < repeat && pixel < width; i++, pixel++)
                {
                    scanline.AsSpan((pixel - 1) * 4, 4).CopyTo(scanline.AsSpan(pixel * 4, 4));
                }

                shift += 8;
            }
            else
            {
                var o = pixel * 4;
                scanline[o] = r;
                scanline[o + 1] = g;
                scanline[o + 2] = b;
                scanline[o + 3] = e;
                pixel++;
                shift = 0;
            }
        }
    }

    /// <summary>Encodes a float image (<see cref="TextureFormat.RGBA32F"/> or <see cref="TextureFormat.RGBA16F"/>
    /// data, four floats per pixel) as a Radiance HDR file. Alpha is dropped.</summary>
    public static byte[] Encode(TextureImage image)
    {
        ArgumentNullException.ThrowIfNull(image);
        if (image.Data is not float[] pixels || image.Format is not (TextureFormat.RGBA32F or TextureFormat.RGBA16F)
            || pixels.Length < image.Width * image.Height * 4)
        {
            throw new NotSupportedException("Only RGBA float images can be encoded as HDR.");
        }

        int width = image.Width, height = image.Height;
        using var output = new MemoryStream();
        output.Write(Encoding.ASCII.GetBytes(
            "#?RADIANCE\nFORMAT=32-bit_rle_rgbe\n\n-Y " + height.ToString(CultureInfo.InvariantCulture) + " +X " + width.ToString(CultureInfo.InvariantCulture) + "\n"));
        var scanline = new byte[width * 4];
        var runLength = width is >= 8 and <= 32767;
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var o = ((y * width) + x) * 4;
                ToRgbe(pixels[o], pixels[o + 1], pixels[o + 2], scanline.AsSpan(x * 4, 4));
            }

            if (!runLength)
            {
                output.Write(scanline);
                continue;
            }

            output.WriteByte(2);
            output.WriteByte(2);
            output.WriteByte((byte)(width >> 8));
            output.WriteByte((byte)width);
            for (var channel = 0; channel < 4; channel++)
            {
                WriteRle(output, scanline, channel, width);
            }
        }

        return output.ToArray();
    }

    private static void ToRgbe(float r, float g, float b, Span<byte> rgbe)
    {
        r = float.IsFinite(r) ? MathF.Max(r, 0f) : 0f;
        g = float.IsFinite(g) ? MathF.Max(g, 0f) : 0f;
        b = float.IsFinite(b) ? MathF.Max(b, 0f) : 0f;
        var max = MathF.Max(r, MathF.Max(g, b));
        if (max < 1e-32f)
        {
            rgbe.Clear();
            return;
        }

        // max = mantissa * 2^exponent with the mantissa in [0.5, 1).
        var exponent = MathF.ILogB(max) + 1;
        var scale = MathF.ScaleB(256f, -exponent);
        if (max * scale >= 255.5f)
        {
            exponent++;
            scale *= 0.5f;
        }

        rgbe[0] = (byte)Math.Clamp((int)((r * scale) + 0.5f), 0, 255);
        rgbe[1] = (byte)Math.Clamp((int)((g * scale) + 0.5f), 0, 255);
        rgbe[2] = (byte)Math.Clamp((int)((b * scale) + 0.5f), 0, 255);
        rgbe[3] = (byte)Math.Clamp(exponent + 128, 1, 255);
    }

    private static void WriteRle(Stream output, byte[] scanline, int channel, int width)
    {
        Span<byte> literal = stackalloc byte[128];
        var x = 0;
        while (x < width)
        {
            // Length of the run starting here.
            var value = scanline[(x * 4) + channel];
            var run = 1;
            while (x + run < width && run < 127 && scanline[((x + run) * 4) + channel] == value)
            {
                run++;
            }

            if (run >= 3)
            {
                output.WriteByte((byte)(128 + run));
                output.WriteByte(value);
                x += run;
                continue;
            }

            // Literals up to the next run of three or more.
            var count = 0;
            while (x < width && count < 128)
            {
                var v = scanline[(x * 4) + channel];
                if (x + 2 < width && scanline[((x + 1) * 4) + channel] == v && scanline[((x + 2) * 4) + channel] == v)
                {
                    break;
                }

                literal[count++] = v;
                x++;
            }

            output.WriteByte((byte)count);
            output.Write(literal[..count]);
        }
    }
}
