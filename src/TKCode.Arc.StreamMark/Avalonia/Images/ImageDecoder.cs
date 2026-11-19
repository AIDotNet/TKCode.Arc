using System.Buffers.Binary;
using Avalonia;
using Avalonia.Media.Imaging;

namespace StreamMark.Avalonia.Images;

/// <summary>图片格式。</summary>
internal enum ImageFormat
{
    Unknown,
    Png,
    Jpeg,
    Gif,
    WebP,
    Bmp,
    Ico,
    Svg,
}

/// <summary>
/// 识别图片格式、只读文件头取像素尺寸（不解码），以及按宽度档位解码。
/// 解码可在任意线程调用（Skia 后端允许后台创建位图）。
/// </summary>
internal static class ImageDecoder
{
    public static ImageFormat Sniff(ReadOnlySpan<byte> data, string? contentType)
    {
        if (data.Length >= 8 && data[..8].SequenceEqual((ReadOnlySpan<byte>)[0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]))
        {
            return ImageFormat.Png;
        }
        if (data.Length >= 3 && data[0] == 0xFF && data[1] == 0xD8 && data[2] == 0xFF)
        {
            return ImageFormat.Jpeg;
        }
        if (data.Length >= 6 && data[0] == 'G' && data[1] == 'I' && data[2] == 'F' && data[3] == '8')
        {
            return ImageFormat.Gif;
        }
        if (data.Length >= 12 && data[..4].SequenceEqual("RIFF"u8) && data.Slice(8, 4).SequenceEqual("WEBP"u8))
        {
            return ImageFormat.WebP;
        }
        if (data.Length >= 2 && data[0] == 'B' && data[1] == 'M')
        {
            return ImageFormat.Bmp;
        }
        if (data.Length >= 4 && data[0] == 0 && data[1] == 0 && data[2] == 1 && data[3] == 0)
        {
            return ImageFormat.Ico;
        }
        if (contentType is not null && contentType.StartsWith("image/svg", StringComparison.OrdinalIgnoreCase))
        {
            return ImageFormat.Svg;
        }
        var head = data[..Math.Min(data.Length, 512)];
        var start = 0;
        while (start < head.Length && (head[start] is (byte)' ' or (byte)'\t' or (byte)'\r' or (byte)'\n' or 0xEF or 0xBB or 0xBF))
        {
            start++;
        }
        head = head[start..];
        if (head.StartsWith("<svg"u8) || (head.StartsWith("<?xml"u8) && head.IndexOf("<svg"u8) >= 0))
        {
            return ImageFormat.Svg;
        }
        return ImageFormat.Unknown;
    }

    /// <summary>从文件头读取像素尺寸；不支持或损坏时返回 false。</summary>
    public static bool TryReadSize(ReadOnlySpan<byte> data, ImageFormat format, out PixelSize size)
    {
        size = default;
        int width = 0, height = 0;
        try
        {
            switch (format)
            {
                case ImageFormat.Png when data.Length >= 24:
                    width = BinaryPrimitives.ReadInt32BigEndian(data[16..]);
                    height = BinaryPrimitives.ReadInt32BigEndian(data[20..]);
                    break;
                case ImageFormat.Gif when data.Length >= 10:
                    width = BinaryPrimitives.ReadUInt16LittleEndian(data[6..]);
                    height = BinaryPrimitives.ReadUInt16LittleEndian(data[8..]);
                    break;
                case ImageFormat.Bmp when data.Length >= 26:
                    var headerSize = BinaryPrimitives.ReadInt32LittleEndian(data[14..]);
                    if (headerSize == 12)
                    {
                        width = BinaryPrimitives.ReadUInt16LittleEndian(data[18..]);
                        height = BinaryPrimitives.ReadUInt16LittleEndian(data[20..]);
                    }
                    else
                    {
                        width = BinaryPrimitives.ReadInt32LittleEndian(data[18..]);
                        height = Math.Abs(BinaryPrimitives.ReadInt32LittleEndian(data[22..]));
                    }
                    break;
                case ImageFormat.WebP when data.Length >= 30:
                    var chunk = data.Slice(12, 4);
                    if (chunk.SequenceEqual("VP8 "u8))
                    {
                        width = BinaryPrimitives.ReadUInt16LittleEndian(data[26..]) & 0x3FFF;
                        height = BinaryPrimitives.ReadUInt16LittleEndian(data[28..]) & 0x3FFF;
                    }
                    else if (chunk.SequenceEqual("VP8L"u8))
                    {
                        var bits = BinaryPrimitives.ReadUInt32LittleEndian(data[21..]);
                        width = (int)(bits & 0x3FFF) + 1;
                        height = (int)((bits >> 14) & 0x3FFF) + 1;
                    }
                    else if (chunk.SequenceEqual("VP8X"u8))
                    {
                        width = (data[24] | (data[25] << 8) | (data[26] << 16)) + 1;
                        height = (data[27] | (data[28] << 8) | (data[29] << 16)) + 1;
                    }
                    break;
                case ImageFormat.Jpeg:
                    return TryReadJpegSize(data, out size);
            }
        }
        catch (ArgumentOutOfRangeException)
        {
            return false;
        }
        if (width <= 0 || height <= 0 || width > 1 << 16 || height > 1 << 16)
        {
            return false;
        }
        size = new PixelSize(width, height);
        return true;
    }

    private static bool TryReadJpegSize(ReadOnlySpan<byte> data, out PixelSize size)
    {
        size = default;
        var i = 2;
        while (i + 9 < data.Length)
        {
            if (data[i] != 0xFF)
            {
                i++;
                continue;
            }
            var marker = data[i + 1];
            if (marker == 0xFF)
            {
                i++;
                continue;
            }
            if (marker is 0xD8 or 0x01 or (>= 0xD0 and <= 0xD7))
            {
                i += 2;
                continue;
            }
            var length = BinaryPrimitives.ReadUInt16BigEndian(data[(i + 2)..]);
            // SOF0..SOF15，排除 DHT(C4)、JPG(C8)、DAC(CC)
            if (marker is >= 0xC0 and <= 0xCF and not 0xC4 and not 0xC8 and not 0xCC)
            {
                var height = BinaryPrimitives.ReadUInt16BigEndian(data[(i + 5)..]);
                var width = BinaryPrimitives.ReadUInt16BigEndian(data[(i + 7)..]);
                if (width == 0 || height == 0)
                {
                    return false;
                }
                size = new PixelSize(width, height);
                return true;
            }
            i += 2 + length;
        }
        return false;
    }

    /// <summary>宽度档位：按 256px 向上取整；0 或负数表示原始尺寸（档位 0）。</summary>
    public static int Bucket(int decodeWidth) => decodeWidth <= 0 ? 0 : (decodeWidth + 255) / 256 * 256;

    /// <summary>
    /// 解码。<paramref name="bucket"/> 大于 0 且原图更宽时按档位宽度缩小解码；否则完整解码。
    /// 返回 null 表示无法解码。
    /// </summary>
    public static Bitmap? Decode(byte[] data, int bucket, PixelSize? naturalSize)
    {
        try
        {
            using var stream = new MemoryStream(data, writable: false);
            if (bucket > 0 && naturalSize is { } natural && natural.Width > bucket)
            {
                return Bitmap.DecodeToWidth(stream, bucket, BitmapInterpolationMode.HighQuality);
            }
            return new Bitmap(stream);
        }
        catch (Exception)
        {
            return null;
        }
    }
}
