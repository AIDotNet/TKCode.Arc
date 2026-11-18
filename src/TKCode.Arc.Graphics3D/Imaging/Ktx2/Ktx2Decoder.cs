// KTX 2.0 texture decoder to RGBA8: Basis Universal ETC1S (BasisLZ) and UASTC LDR 4x4, as the glTF extension
// KHR_texture_basisu uses them, and uncompressed 8-bit formats.
//
// Follows the KTX File Format Specification 2.0 (https://registry.khronos.org/KTX/specs/2.0/ktxspec.v2.html)
// and produces the pixels of the Basis Universal reference transcoder's RGBA32 target
// (https://github.com/BinomialLLC/basis_universal, Apache License 2.0). See BasisEtc1s.cs, BasisUastc.cs and
// ZstdDecoder.cs for the codecs.

using System.Buffers.Binary;
using System.IO.Compression;
using System.Runtime.InteropServices;

namespace TKCode.Arc.Graphics3D;

/// <summary>Decodes KTX2 textures (Basis Universal ETC1S and UASTC LDR, or plain 8-bit formats) to RGBA8 without any other library.</summary>
public static class Ktx2Decoder
{
    private const int GlobalHeaderSize = 20;
    private const int ImageDescriptorSize = 20;
    private const uint PFrameFlag = 2;

    /// <summary>True when the data starts with the KTX2 identifier.</summary>
    public static bool IsKtx2(ReadOnlySpan<byte> data) => Ktx2Container.HasIdentifier(data);

    /// <summary>
    /// Decodes a KTX2 file to RGBA8, all mip levels. Of an array, a cube map or a video it decodes the first
    /// image (layer 0, face 0). Throws <see cref="InvalidDataException"/> for a corrupt file and
    /// <see cref="NotSupportedException"/> for a format this decoder does not handle.
    /// </summary>
    public static Ktx2Image Decode(ReadOnlySpan<byte> data)
    {
        try
        {
            return DecodeCore(data);
        }
        catch (Exception e) when (e is IndexOutOfRangeException or ArgumentException or OverflowException)
        {
            // Every read is checked where it happens; this only keeps the contract if a check is ever missed.
            throw new InvalidDataException("The KTX2 file is corrupt.", e);
        }
    }

    /// <summary>The decoder without the exception translation of <see cref="Decode"/>.</summary>
    internal static Ktx2Image DecodeCore(ReadOnlySpan<byte> data)
    {
        Ktx2Container container = Ktx2Container.Parse(data);
        var levels = new byte[container.Levels.Length][];

        if (container.Codec == Ktx2Codec.Etc1s)
        {
            DecodeEtc1s(data, container, levels);
        }
        else
        {
            DecodeBlocksOrPixels(data, container, levels);
        }

        if (container.FlipX || container.FlipY)
        {
            // Hand out every file the same way: first row at the top, first pixel at the left.
            for (int i = 0; i < levels.Length; i++)
            {
                Reorient(levels[i], LevelSize(container.Width, i), LevelSize(container.Height, i), container.FlipX, container.FlipY);
            }
        }

        return new Ktx2Image(container.Width, container.Height, container.IsSrgb, container.HasAlpha, levels);
    }

    private static int LevelSize(int size, int level)
    {
        return Math.Max(size >> level, 1);
    }

    private static void DecodeEtc1s(ReadOnlySpan<byte> data, Ktx2Container container, byte[][] levels)
    {
        // Global data: a header, one descriptor per image (level, layer, face), then the endpoint codebook,
        // the selector codebook and the Huffman tables.
        ReadOnlySpan<byte> global = data.Slice(container.GlobalDataOffset, container.GlobalDataLength);
        long imagesPerLevel = (long)container.LayerCount * container.FaceCount;
        long imageCount = imagesPerLevel * levels.Length;
        if (global.Length < GlobalHeaderSize)
        {
            throw new InvalidDataException("The ETC1S global data is truncated.");
        }

        int endpointCount = BinaryPrimitives.ReadUInt16LittleEndian(global);
        int selectorCount = BinaryPrimitives.ReadUInt16LittleEndian(global.Slice(2));
        long endpointsLength = BinaryPrimitives.ReadUInt32LittleEndian(global.Slice(4));
        long selectorsLength = BinaryPrimitives.ReadUInt32LittleEndian(global.Slice(8));
        long tablesLength = BinaryPrimitives.ReadUInt32LittleEndian(global.Slice(12));
        long codebooksOffset = GlobalHeaderSize + imageCount * ImageDescriptorSize;
        if (codebooksOffset + endpointsLength + selectorsLength + tablesLength > global.Length)
        {
            throw new InvalidDataException("The ETC1S global data is truncated.");
        }

        ReadOnlySpan<byte> descriptors = global.Slice(GlobalHeaderSize, (int)(imageCount * ImageDescriptorSize));
        ReadOnlySpan<byte> codebooks = global.Slice((int)codebooksOffset);
        var decoder = new BasisEtc1sDecoder(
            endpointCount,
            codebooks.Slice(0, (int)endpointsLength),
            selectorCount,
            codebooks.Slice((int)endpointsLength, (int)selectorsLength),
            codebooks.Slice((int)(endpointsLength + selectorsLength), (int)tablesLength));

        bool video = container.HasAnimationData || HasPFrames(container, descriptors);

        for (int i = 0; i < levels.Length; i++)
        {
            int width = LevelSize(container.Width, i);
            int height = LevelSize(container.Height, i);
            int blocksX = (width + 3) >> 2;
            int blocksY = (height + 3) >> 2;

            // The first image of the level; slice offsets are relative to the level's data.
            ReadOnlySpan<byte> descriptor = descriptors.Slice((int)(i * imagesPerLevel * ImageDescriptorSize), ImageDescriptorSize);
            ReadOnlySpan<byte> level = data.Slice(container.Levels[i].Offset, container.Levels[i].Length);
            if ((BinaryPrimitives.ReadUInt32LittleEndian(descriptor) & PFrameFlag) != 0)
            {
                throw new NotSupportedException("The first frame of the ETC1S video depends on another frame.");
            }

            ReadOnlySpan<byte> colorSlice = Slice(level, descriptor, 4);
            uint[] color = decoder.DecodeSlice(colorSlice, blocksX, blocksY, video);
            uint[]? alpha = null;
            if (container.HasAlpha)
            {
                alpha = decoder.DecodeSlice(Slice(level, descriptor, 12), blocksX, blocksY, video);
            }

            var rgba = new byte[width * height * 4];
            decoder.WritePixels(color, alpha, width, height, rgba);
            levels[i] = rgba;
        }
    }

    private static ReadOnlySpan<byte> Slice(ReadOnlySpan<byte> level, ReadOnlySpan<byte> descriptor, int field)
    {
        uint offset = BinaryPrimitives.ReadUInt32LittleEndian(descriptor.Slice(field));
        uint length = BinaryPrimitives.ReadUInt32LittleEndian(descriptor.Slice(field + 4));
        if (length == 0 || offset > (uint)level.Length || length > (uint)level.Length - offset)
        {
            throw new InvalidDataException("An ETC1S slice is missing or lies outside its mip level.");
        }

        return level.Slice((int)offset, (int)length);
    }

    /// <summary>An array whose images have predicted frames is a video even without the animation metadata.</summary>
    private static bool HasPFrames(Ktx2Container container, ReadOnlySpan<byte> descriptors)
    {
        if (container.FaceCount != 1 || container.LayerCount <= 1)
        {
            return false;
        }

        for (int offset = 0; offset < descriptors.Length; offset += ImageDescriptorSize)
        {
            if ((BinaryPrimitives.ReadUInt32LittleEndian(descriptors.Slice(offset)) & PFrameFlag) != 0)
            {
                return true;
            }
        }

        return false;
    }

    private static void DecodeBlocksOrPixels(ReadOnlySpan<byte> data, Ktx2Container container, byte[][] levels)
    {
        bool uastc = container.Codec == Ktx2Codec.Uastc;
        int bytesPerPixel = uastc ? 0 : Ktx2Formats.BytesPerPixel(container.VkFormat);

        for (int i = 0; i < levels.Length; i++)
        {
            int width = LevelSize(container.Width, i);
            int height = LevelSize(container.Height, i);
            long imageSize = uastc
                ? (long)((width + 3) >> 2) * ((height + 3) >> 2) * BasisUastc.BlockBytes
                : (long)width * height * bytesPerPixel;

            // Layers and faces follow each other in the level; the first image is at its start.
            Ktx2Level level = container.Levels[i];
            ReadOnlySpan<byte> stored = data.Slice(level.Offset, level.Length);
            ReadOnlySpan<byte> image;
            if (container.Supercompression == Ktx2Container.SchemeNone)
            {
                image = stored;
            }
            else
            {
                // The declared size has to be what the header implies before anything of that size is decoded.
                long levelSize = imageSize * container.LayerCount * container.FaceCount;
                if (level.UncompressedLength != (ulong)levelSize)
                {
                    throw new InvalidDataException("A KTX2 mip level declares a size that does not match the texture.");
                }

                if (levelSize > Array.MaxLength)
                {
                    throw new NotSupportedException("A KTX2 mip level is too large to decompress.");
                }

                image = container.Supercompression == Ktx2Container.SchemeZstandard
                    ? ZstdDecoder.Decompress(stored, (int)levelSize)
                    : InflateZlib(stored, (int)levelSize);
                if (image.Length != levelSize)
                {
                    throw new InvalidDataException("A KTX2 mip level decompresses to less than its declared size.");
                }
            }

            if (image.Length < imageSize)
            {
                throw new InvalidDataException("A KTX2 mip level is smaller than its image.");
            }

            var rgba = new byte[width * height * 4];
            if (uastc)
            {
                BasisUastc.DecodeImage(image, width, height, rgba);
            }
            else
            {
                Ktx2Formats.ToRgba(container.VkFormat, image, rgba);
            }

            levels[i] = rgba;
        }
    }

    private static byte[] InflateZlib(ReadOnlySpan<byte> source, int expectedSize)
    {
        // The buffer grows with the data, so a short stream cannot reserve the declared size up front.
        var buffer = new byte[(int)Math.Min(expectedSize, Math.Max(65536L, (long)source.Length * 4))];
        int length = 0;
        try
        {
            using var input = new MemoryStream(source.ToArray(), writable: false);
            using var inflater = new ZLibStream(input, CompressionMode.Decompress);
            while (true)
            {
                if (length == buffer.Length)
                {
                    if (length == expectedSize)
                    {
                        if (inflater.ReadByte() >= 0)
                        {
                            throw new InvalidDataException("A KTX2 mip level inflates beyond its declared size.");
                        }

                        break;
                    }

                    Array.Resize(ref buffer, (int)Math.Min(expectedSize, (long)buffer.Length * 2));
                }

                int read = inflater.Read(buffer, length, buffer.Length - length);
                if (read <= 0)
                {
                    break;
                }

                length += read;
            }
        }
        catch (IOException e)
        {
            throw new InvalidDataException("A KTX2 mip level holds corrupt zlib data.", e);
        }

        if (length != expectedSize)
        {
            throw new InvalidDataException("A KTX2 mip level inflates to less than its declared size.");
        }

        return buffer;
    }

    private static void Reorient(byte[] rgba, int width, int height, bool flipX, bool flipY)
    {
        int stride = width * 4;
        if (flipY)
        {
            var swap = new byte[stride];
            for (int top = 0, bottom = height - 1; top < bottom; top++, bottom--)
            {
                Span<byte> first = rgba.AsSpan(top * stride, stride);
                Span<byte> second = rgba.AsSpan(bottom * stride, stride);
                first.CopyTo(swap);
                second.CopyTo(first);
                swap.CopyTo(second);
            }
        }

        if (flipX)
        {
            for (int y = 0; y < height; y++)
            {
                Span<uint> row = MemoryMarshal.Cast<byte, uint>(rgba.AsSpan(y * stride, stride));
                row.Reverse();
            }
        }
    }
}
