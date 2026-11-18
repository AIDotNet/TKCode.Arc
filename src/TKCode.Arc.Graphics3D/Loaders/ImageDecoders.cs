namespace TKCode.Arc.Graphics3D;

/// <summary>Turns encoded image files into pixels. PNG, JPEG and Radiance HDR are built in; a host can add formats
/// (WebP, KTX2, or a faster platform codec) with <see cref="Register"/>.</summary>
public static class ImageDecoders
{
    private static readonly object s_gate = new();
    private static Func<ReadOnlyMemory<byte>, TextureImage?>[] s_custom = [];

    /// <summary>Adds a decoder. Registered decoders are asked before the built-in ones, the latest first; a decoder
    /// returns null for data it does not understand.</summary>
    public static void Register(Func<ReadOnlyMemory<byte>, TextureImage?> decoder)
    {
        ArgumentNullException.ThrowIfNull(decoder);
        lock (s_gate)
        {
            var next = new Func<ReadOnlyMemory<byte>, TextureImage?>[s_custom.Length + 1];
            next[0] = decoder;
            s_custom.CopyTo(next, 1);
            s_custom = next;
        }
    }

    /// <summary>Removes a decoder added with <see cref="Register"/>.</summary>
    public static bool Unregister(Func<ReadOnlyMemory<byte>, TextureImage?> decoder)
    {
        lock (s_gate)
        {
            var index = Array.IndexOf(s_custom, decoder);
            if (index < 0)
            {
                return false;
            }

            var next = new Func<ReadOnlyMemory<byte>, TextureImage?>[s_custom.Length - 1];
            Array.Copy(s_custom, 0, next, 0, index);
            Array.Copy(s_custom, index + 1, next, index, next.Length - index);
            s_custom = next;
            return true;
        }
    }

    /// <summary>True when a built-in decoder recognizes the data.</summary>
    public static bool IsKnownFormat(ReadOnlySpan<byte> data) => Png.IsPng(data) || Jpeg.IsJpeg(data) || Hdr.IsHdr(data) || Ktx2Decoder.IsKtx2(data);

    /// <summary>Decodes an image file. PNG, JPEG and KTX2 give 8-bit RGBA, HDR gives float RGBA; rows run top to bottom.</summary>
    /// <exception cref="NotSupportedException">No decoder understands the data.</exception>
    public static TextureImage Decode(ReadOnlyMemory<byte> data)
    {
        var custom = s_custom;
        foreach (var decoder in custom)
        {
            if (decoder(data) is { } image)
            {
                return image;
            }
        }

        return DecodeBuiltIn(data.Span);
    }

    /// <summary>Decodes an image file. PNG, JPEG and KTX2 give 8-bit RGBA, HDR gives float RGBA; rows run top to bottom.</summary>
    /// <exception cref="NotSupportedException">No decoder understands the data.</exception>
    public static TextureImage Decode(ReadOnlySpan<byte> data)
    {
        // Registered decoders take memory, so the data is copied only when there are any.
        return s_custom.Length > 0 ? Decode(new ReadOnlyMemory<byte>(data.ToArray())) : DecodeBuiltIn(data);
    }

    private static TextureImage DecodeBuiltIn(ReadOnlySpan<byte> data)
    {
        if (Png.IsPng(data))
        {
            return Png.Decode(data);
        }

        if (Jpeg.IsJpeg(data))
        {
            return Jpeg.Decode(data);
        }

        if (Ktx2Decoder.IsKtx2(data))
        {
            // The largest level as plain RGBA; the renderer builds the smaller ones itself, as for any other image.
            var ktx = Ktx2Decoder.Decode(data);
            return new TextureImage(ktx.Width, ktx.Height, ktx.Levels[0]);
        }

        if (Hdr.IsHdr(data))
        {
            return Hdr.Decode(data);
        }

        var head = Convert.ToHexString(data[..Math.Min(data.Length, 12)]);
        throw new NotSupportedException(
            $"Unknown image format (the data starts with {head}). PNG, JPEG and Radiance HDR are built in; add others with ImageDecoders.Register.");
    }
}
