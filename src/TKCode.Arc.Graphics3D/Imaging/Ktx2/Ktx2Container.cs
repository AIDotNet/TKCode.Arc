// KTX 2.0 container: header, level index, data format descriptor and key/value data.
//
// Follows the KTX File Format Specification 2.0 (https://registry.khronos.org/KTX/specs/2.0/ktxspec.v2.html)
// and the Khronos Data Format Specification for the descriptor layout.

using System.Buffers.Binary;
using System.Numerics;

namespace TKCode.Arc.Graphics3D;

/// <summary>How the pixels of a KTX2 file are stored.</summary>
internal enum Ktx2Codec
{
    /// <summary>Uncompressed 8-bit pixels.</summary>
    Plain,

    /// <summary>ETC1S with BasisLZ supercompression.</summary>
    Etc1s,

    /// <summary>UASTC LDR 4x4 blocks.</summary>
    Uastc,
}

/// <summary>One entry of the level index.</summary>
internal readonly struct Ktx2Level
{
    public Ktx2Level(int offset, int length, ulong uncompressedLength)
    {
        Offset = offset;
        Length = length;
        UncompressedLength = uncompressedLength;
    }

    /// <summary>Offset of the level's bytes in the file.</summary>
    public int Offset { get; }

    /// <summary>Size of the level's bytes in the file.</summary>
    public int Length { get; }

    /// <summary>Size after inflating the level's supercompression, as the file declares it.</summary>
    public ulong UncompressedLength { get; }
}

/// <summary>The validated header fields of a KTX2 file that the decoder needs.</summary>
internal sealed class Ktx2Container
{
    public const int MaxDimension = 8192;
    public const int SchemeNone = 0;
    public const int SchemeBasisLz = 1;
    public const int SchemeZstandard = 2;
    public const int SchemeZlib = 3;

    private const int HeaderSize = 80;
    private const int LevelIndexEntrySize = 24;
    private const int MaxLayers = 1 << 24;
    private const int DfdBasicBlockOffset = 4;
    private const int DfdBasicHeaderSize = 24;
    private const int DfdSampleSize = 16;
    private const int ColorModelEtc1s = 163;
    private const int ColorModelUastc = 166;
    private const int TransferSrgb = 2;
    private const int UastcChannelRgba = 3;
    private const int UastcChannelRrrg = 5;

    private static ReadOnlySpan<byte> Identifier => [0xAB, 0x4B, 0x54, 0x58, 0x20, 0x32, 0x30, 0xBB, 0x0D, 0x0A, 0x1A, 0x0A];

    private Ktx2Container(Ktx2Level[] levels)
    {
        Levels = levels;
    }

    public uint VkFormat { get; private set; }

    public int Width { get; private set; }

    public int Height { get; private set; }

    /// <summary>Array layers, at least 1.</summary>
    public int LayerCount { get; private set; }

    /// <summary>1, or 6 for a cube map.</summary>
    public int FaceCount { get; private set; }

    public int Supercompression { get; private set; }

    public Ktx2Codec Codec { get; private set; }

    public bool IsSrgb { get; private set; }

    public bool HasAlpha { get; private set; }

    /// <summary>The KTXorientation metadata says the first column is the right edge.</summary>
    public bool FlipX { get; private set; }

    /// <summary>The KTXorientation metadata says the first row is the bottom edge.</summary>
    public bool FlipY { get; private set; }

    /// <summary>The file has KTXanimData metadata: its layers are video frames.</summary>
    public bool HasAnimationData { get; private set; }

    public int GlobalDataOffset { get; private set; }

    public int GlobalDataLength { get; private set; }

    public Ktx2Level[] Levels { get; }

    public static bool HasIdentifier(ReadOnlySpan<byte> data)
    {
        return data.Length >= Identifier.Length && data.Slice(0, Identifier.Length).SequenceEqual(Identifier);
    }

    public static Ktx2Container Parse(ReadOnlySpan<byte> data)
    {
        if (!HasIdentifier(data))
        {
            throw new InvalidDataException("The data is not a KTX2 file.");
        }

        if (data.Length < HeaderSize)
        {
            throw Truncated();
        }

        uint vkFormat = ReadUInt32(data, 12);
        uint width = ReadUInt32(data, 20);
        uint height = ReadUInt32(data, 24);
        uint depth = ReadUInt32(data, 28);
        uint layerCount = ReadUInt32(data, 32);
        uint faceCount = ReadUInt32(data, 36);
        uint levelCount = ReadUInt32(data, 40);
        uint scheme = ReadUInt32(data, 44);

        if (depth != 0)
        {
            throw new NotSupportedException("3D KTX2 textures are not supported.");
        }

        if (width == 0)
        {
            throw new InvalidDataException("The KTX2 texture has no width.");
        }

        if (height == 0)
        {
            throw new NotSupportedException("1D KTX2 textures are not supported.");
        }

        if (width > MaxDimension || height > MaxDimension)
        {
            throw new InvalidDataException($"The KTX2 texture is larger than {MaxDimension} pixels ({width} x {height}).");
        }

        if (faceCount != 1 && faceCount != 6)
        {
            throw new InvalidDataException("The KTX2 face count must be 1 or 6.");
        }

        if (layerCount > MaxLayers)
        {
            throw new InvalidDataException("The KTX2 layer count is too large.");
        }

        // No levels means "one level, generate the rest". A file cannot hold more levels than a full pyramid.
        levelCount = Math.Max(levelCount, 1);
        if (levelCount > (uint)BitOperations.Log2(Math.Max(width, height)) + 1)
        {
            throw new InvalidDataException("The KTX2 file declares more mip levels than its size allows.");
        }

        if (data.Length < HeaderSize + (long)levelCount * LevelIndexEntrySize)
        {
            throw Truncated();
        }

        var levels = new Ktx2Level[levelCount];
        for (int i = 0; i < levels.Length; i++)
        {
            int entry = HeaderSize + i * LevelIndexEntrySize;
            ulong offset = BinaryPrimitives.ReadUInt64LittleEndian(data.Slice(entry));
            ulong length = BinaryPrimitives.ReadUInt64LittleEndian(data.Slice(entry + 8));
            if (!InFile(data, offset, length))
            {
                throw new InvalidDataException("A KTX2 mip level lies outside the file.");
            }

            levels[i] = new Ktx2Level((int)offset, (int)length, BinaryPrimitives.ReadUInt64LittleEndian(data.Slice(entry + 16)));
        }

        var container = new Ktx2Container(levels)
        {
            VkFormat = vkFormat,
            Width = (int)width,
            Height = (int)height,
            LayerCount = (int)Math.Max(layerCount, 1),
            FaceCount = (int)faceCount,
            Supercompression = (int)Math.Min(scheme, int.MaxValue),
        };

        container.ReadFormat(data, ReadUInt32(data, 48), ReadUInt32(data, 52));
        container.ReadKeyValues(data, ReadUInt32(data, 56), ReadUInt32(data, 60));

        if (scheme > SchemeZlib)
        {
            throw new NotSupportedException($"KTX2 supercompression scheme {scheme} is not supported.");
        }

        if ((scheme == SchemeBasisLz) != (container.Codec == Ktx2Codec.Etc1s))
        {
            throw container.Codec == Ktx2Codec.Etc1s
                ? new NotSupportedException("ETC1S data without BasisLZ supercompression is not supported.")
                : new InvalidDataException("BasisLZ supercompression is only valid for ETC1S data.");
        }

        if (scheme == SchemeBasisLz)
        {
            ulong offset = BinaryPrimitives.ReadUInt64LittleEndian(data.Slice(64));
            ulong length = BinaryPrimitives.ReadUInt64LittleEndian(data.Slice(72));
            if (!InFile(data, offset, length))
            {
                throw new InvalidDataException("The KTX2 supercompression global data lies outside the file.");
            }

            container.GlobalDataOffset = (int)offset;
            container.GlobalDataLength = (int)length;
        }

        return container;
    }

    private static InvalidDataException Truncated()
    {
        return new InvalidDataException("The KTX2 file is truncated.");
    }

    private static uint ReadUInt32(ReadOnlySpan<byte> data, int offset)
    {
        return BinaryPrimitives.ReadUInt32LittleEndian(data.Slice(offset));
    }

    private static bool InFile(ReadOnlySpan<byte> data, ulong offset, ulong length)
    {
        return offset <= (ulong)data.Length && length <= (ulong)data.Length - offset;
    }

    /// <summary>Works out the codec, transfer function and alpha from vkFormat and the data format descriptor.</summary>
    private void ReadFormat(ReadOnlySpan<byte> data, uint dfdOffset, uint dfdLength)
    {
        if (!InFile(data, dfdOffset, dfdLength) || dfdLength < DfdBasicBlockOffset + DfdBasicHeaderSize)
        {
            throw new InvalidDataException("The KTX2 data format descriptor is missing or lies outside the file.");
        }

        ReadOnlySpan<byte> block = data.Slice((int)dfdOffset + DfdBasicBlockOffset, (int)dfdLength - DfdBasicBlockOffset);
        int blockSize = BinaryPrimitives.ReadUInt16LittleEndian(block.Slice(6));
        int colorModel = block[8];
        int transferFunction = block[10];
        int sampleCount = (Math.Min(blockSize, block.Length) - DfdBasicHeaderSize) / DfdSampleSize;
        int firstChannel = sampleCount > 0 ? block[DfdBasicHeaderSize + 3] & 15 : 0;

        IsSrgb = transferFunction == TransferSrgb;

        if (VkFormat != 0)
        {
            Codec = Ktx2Codec.Plain;
            if (Ktx2Formats.BytesPerPixel(VkFormat) == 0)
            {
                throw new NotSupportedException($"KTX2 textures in format {Ktx2Formats.Name(VkFormat)} are not supported.");
            }

            HasAlpha = Ktx2Formats.HasAlpha(VkFormat);
            return;
        }

        switch (colorModel)
        {
            case ColorModelEtc1s:
                // One slice per sample: a second sample is the alpha (or second channel) slice.
                Codec = Ktx2Codec.Etc1s;
                HasAlpha = sampleCount >= 2;
                break;

            case ColorModelUastc:
                Codec = Ktx2Codec.Uastc;
                HasAlpha = firstChannel == UastcChannelRgba || firstChannel == UastcChannelRrrg;
                break;

            default:
                throw new NotSupportedException($"KTX2 textures with color model {Ktx2Formats.ColorModelName(colorModel)} are not supported.");
        }
    }

    private void ReadKeyValues(ReadOnlySpan<byte> data, uint kvdOffset, uint kvdLength)
    {
        if (kvdLength == 0)
        {
            return;
        }

        if (!InFile(data, kvdOffset, kvdLength))
        {
            throw new InvalidDataException("The KTX2 key/value data lies outside the file.");
        }

        // Entries are a length, a NUL-terminated key, the value and padding to four bytes.
        ReadOnlySpan<byte> entries = data.Slice((int)kvdOffset, (int)kvdLength);
        int pos = 0;
        while (entries.Length - pos >= 4)
        {
            uint length = ReadUInt32(entries, pos);
            pos += 4;
            if (length > (uint)(entries.Length - pos))
            {
                throw new InvalidDataException("A KTX2 key/value entry lies outside the key/value data.");
            }

            ReadOnlySpan<byte> entry = entries.Slice(pos, (int)length);
            int keyLength = entry.IndexOf((byte)0);
            if (keyLength >= 0)
            {
                ReadOnlySpan<byte> key = entry.Slice(0, keyLength);
                ReadOnlySpan<byte> value = entry.Slice(keyLength + 1);
                if (key.SequenceEqual("KTXorientation"u8))
                {
                    FlipX = value.Length > 0 && value[0] == (byte)'l';
                    FlipY = value.Length > 1 && value[1] == (byte)'u';
                }
                else if (key.SequenceEqual("KTXanimData"u8))
                {
                    HasAnimationData = true;
                }
            }

            pos += (int)(((long)length + 3) & ~3L);
        }
    }
}
