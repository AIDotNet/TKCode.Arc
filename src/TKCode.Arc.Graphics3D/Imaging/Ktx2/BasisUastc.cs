// UASTC LDR 4x4 block decoder (all 19 modes) to RGBA8.
//
// Follows the UASTC LDR 4x4 specification of Basis Universal (the "UASTC LDR 4x4 Texture Specification" wiki
// page and spec/ of https://github.com/BinomialLLC/basis_universal) and its reference decoder (unpack_uastc
// in transcoder/basisu_transcoder.cpp, Apache License 2.0, Copyright Binomial LLC). The colors are the ones
// that decoder produces for its RGBA32 target: ASTC interpolation with 16-bit endpoint expansion and no sRGB
// rounding. The tables are in BasisUastcTables.cs.

using System.Buffers.Binary;

namespace TKCode.Arc.Graphics3D;

internal static class BasisUastc
{
    public const int BlockBytes = 16;

    /// <summary>
    /// Decodes an image of 4x4 blocks in raster order to <paramref name="width"/> x <paramref name="height"/>
    /// RGBA8 pixels; the last blocks of a row or column are cropped.
    /// </summary>
    public static void DecodeImage(ReadOnlySpan<byte> blocks, int width, int height, Span<byte> rgba)
    {
        int blocksX = (width + 3) >> 2;
        int blocksY = (height + 3) >> 2;
        if (blocks.Length < (long)blocksX * blocksY * BlockBytes)
        {
            throw new InvalidDataException("The UASTC data is smaller than the image.");
        }

        Span<uint> pixels = stackalloc uint[16];
        for (int by = 0; by < blocksY; by++)
        {
            int rows = Math.Min(4, height - by * 4);
            for (int bx = 0; bx < blocksX; bx++)
            {
                int columns = Math.Min(4, width - bx * 4);
                DecodeBlock(blocks.Slice((by * blocksX + bx) * BlockBytes, BlockBytes), pixels);
                for (int y = 0; y < rows; y++)
                {
                    Span<byte> row = rgba.Slice(((by * 4 + y) * width + bx * 4) * 4, columns * 4);
                    for (int x = 0; x < columns; x++)
                    {
                        BinaryPrimitives.WriteUInt32LittleEndian(row.Slice(x * 4), pixels[y * 4 + x]);
                    }
                }
            }
        }
    }

    /// <summary>Decodes one block to 16 pixels in raster order, each with red in the low byte and alpha in the high byte.</summary>
    public static void DecodeBlock(ReadOnlySpan<byte> block, Span<uint> pixels)
    {
        ulong low = BinaryPrimitives.ReadUInt64LittleEndian(block);
        ulong high = BinaryPrimitives.ReadUInt64LittleEndian(block.Slice(8));

        int mode = BasisUastcTables.ModeFromFirstByte[(int)(low & 127)];
        if (mode >= BasisUastcTables.ModeCount)
        {
            throw InvalidBlock();
        }

        int position = BasisUastcTables.ModeCodeBits[mode];
        if (mode == BasisUastcTables.SolidColorMode)
        {
            // Four bytes, red first: already the pixel layout.
            pixels.Slice(0, 16).Fill((uint)((low >> position) & 0xFFFFFFFFu));
            return;
        }

        position += BasisUastcTables.ModeHintBits[mode];

        int subsets = BasisUastcTables.ModeSubsets[mode];
        ReadOnlySpan<byte> partition = default;
        int anchor0 = 0;
        int anchor1 = 0;
        int anchor2 = 0;
        if (subsets > 1)
        {
            ReadOnlySpan<byte> patterns;
            ReadOnlySpan<byte> anchors;
            int pattern;
            int patternCount;
            if (subsets == 3)
            {
                pattern = ReadBits(low, high, ref position, 4);
                patternCount = BasisUastcTables.Patterns3Count;
                patterns = BasisUastcTables.Patterns3;
                anchors = BasisUastcTables.Anchors3;
            }
            else if (mode == 7)
            {
                pattern = ReadBits(low, high, ref position, 5);
                patternCount = BasisUastcTables.Patterns2Mode7Count;
                patterns = BasisUastcTables.Patterns2Mode7;
                anchors = BasisUastcTables.Anchors2Mode7;
            }
            else
            {
                pattern = ReadBits(low, high, ref position, 5);
                patternCount = BasisUastcTables.Patterns2Count;
                patterns = BasisUastcTables.Patterns2;
                anchors = BasisUastcTables.Anchors2;
            }

            if (pattern >= patternCount)
            {
                throw InvalidBlock();
            }

            partition = patterns.Slice(pattern * 16, 16);
            anchor0 = anchors[pattern * 3];
            anchor1 = anchors[pattern * 3 + 1];
            anchor2 = anchors[pattern * 3 + 2];
        }

        // Dual plane modes store which component the second weight plane drives; mode 17 always uses alpha.
        int planes = BasisUastcTables.ModePlanes[mode];
        int secondPlaneComponent = -1;
        if (planes == 2)
        {
            secondPlaneComponent = mode == 17 ? 3 : ReadBits(low, high, ref position, 2);
        }

        int components = BasisUastcTables.ModeComponents[mode];
        int valueCount = components * 2 * subsets;
        Span<byte> endpoints = stackalloc byte[18];
        ReadEndpoints(low, high, ref position, mode, valueCount, endpoints);

        // The first texel of each subset (both planes of texel 0 in dual plane modes) is an anchor: its top
        // weight bit is zero and not stored.
        int weightBits = BasisUastcTables.ModeWeightBits[mode];
        ReadOnlySpan<byte> weightValues = BasisUastcTables.Weights.Slice(weightBits * 32, 32);
        Span<byte> weights = stackalloc byte[32];
        int weightCount = 16 * planes;
        for (int i = 0; i < weightCount; i++)
        {
            bool anchor = planes == 2 ? i < 2 : i == anchor0 || i == anchor1 || i == anchor2;
            weights[i] = weightValues[ReadBits(low, high, ref position, anchor ? weightBits - 1 : weightBits)];
        }

        for (int i = 0; i < 16; i++)
        {
            int e = subsets == 1 ? 0 : partition[i] * components * 2;
            int w0 = weights[i * planes];
            int w1 = planes == 2 ? weights[i * 2 + 1] : w0;
            uint r;
            uint g;
            uint b;
            uint a;
            if (components == 2)
            {
                // Luminance + alpha.
                r = Interpolate(endpoints[e], endpoints[e + 1], w0);
                g = r;
                b = r;
                a = Interpolate(endpoints[e + 2], endpoints[e + 3], secondPlaneComponent == 3 ? w1 : w0);
            }
            else
            {
                r = Interpolate(endpoints[e], endpoints[e + 1], secondPlaneComponent == 0 ? w1 : w0);
                g = Interpolate(endpoints[e + 2], endpoints[e + 3], secondPlaneComponent == 1 ? w1 : w0);
                b = Interpolate(endpoints[e + 4], endpoints[e + 5], secondPlaneComponent == 2 ? w1 : w0);
                a = components == 4 ? Interpolate(endpoints[e + 6], endpoints[e + 7], secondPlaneComponent == 3 ? w1 : w0) : 255u;
            }

            pixels[i] = r | (g << 8) | (b << 16) | (a << 24);
        }
    }

    private static void ReadEndpoints(ulong low, ulong high, ref int position, int mode, int valueCount, Span<byte> endpoints)
    {
        int plainBits = BasisUastcTables.ModeEndpointBits[mode];
        int radix = BasisUastcTables.ModeEndpointRadix[mode];
        ReadOnlySpan<byte> unquant = BasisUastcTables.EndpointUnquant.Slice(BasisUastcTables.ModeUnquantRow[mode] * 256, 256);

        if (radix == 0)
        {
            for (int i = 0; i < valueCount; i++)
            {
                endpoints[i] = unquant[ReadBits(low, high, ref position, plainBits)];
            }

            return;
        }

        // Ranges with a trit or quint: the digits of 5 trits (8 bits) or 3 quints (7 bits) are packed as one
        // number per group, all groups first, then the plain bits of every value. The last group is shorter
        // when it holds fewer digits.
        int groupSize = radix == 3 ? 5 : 3;
        int groupCount = (valueCount + groupSize - 1) / groupSize;
        Span<int> groups = stackalloc int[4];
        for (int g = 0; g < groupCount; g++)
        {
            int digits = Math.Min(groupSize, valueCount - g * groupSize);
            int bits = radix == 3
                ? digits switch { 1 => 2, 2 => 4, 3 => 5, 4 => 7, _ => 8 }
                : digits switch { 1 => 3, 2 => 5, _ => 7 };
            groups[g] = ReadBits(low, high, ref position, bits);
        }

        int packed = 0;
        for (int i = 0; i < valueCount; i++)
        {
            if (i % groupSize == 0)
            {
                packed = groups[i / groupSize];
            }

            int value = ReadBits(low, high, ref position, plainBits) | ((packed % radix) << plainBits);
            packed /= radix;
            endpoints[i] = unquant[value];
        }
    }

    /// <summary>ASTC interpolation of two 8-bit endpoints expanded to 16 bits, weight 0 to 64.</summary>
    private static uint Interpolate(int low, int high, int weight)
    {
        return (uint)(((low * 257 * (64 - weight)) + (high * 257 * weight) + 32) >> 14);
    }

    /// <summary>Reads 0 to 8 bits of the 128-bit block, least significant first; bits past the end are zero.</summary>
    private static int ReadBits(ulong low, ulong high, ref int position, int count)
    {
        int at = position;
        position = at + count;
        ulong value;
        if (at >= 64)
        {
            value = at >= 128 ? 0 : high >> (at - 64);
        }
        else
        {
            // Two shifts, because a shift by 64 (for position 0) would not clear the value.
            value = (low >> at) | ((high << 1) << (63 - at));
        }

        return (int)(value & ((1UL << count) - 1));
    }

    private static InvalidDataException InvalidBlock()
    {
        return new InvalidDataException("A UASTC block is corrupt.");
    }
}
