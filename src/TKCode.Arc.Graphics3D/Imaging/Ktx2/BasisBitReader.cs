// Bit reader and Huffman tables of the BasisLZ (ETC1S) bitstream.
//
// Follows the BasisLZ bitstream specification in the KTX 2.0 specification
// (https://registry.khronos.org/KTX/specs/2.0/ktxspec.v2.html) and the reference transcoder of Basis Universal
// (bitwise_decoder and huffman_decoding_table in transcoder/basisu_transcoder_internal.h,
// https://github.com/BinomialLLC/basis_universal, Apache License 2.0, Copyright Binomial LLC).

namespace TKCode.Arc.Graphics3D;

/// <summary>Reads a BasisLZ stream least significant bit first. Reading past the end is an error.</summary>
internal ref struct BasisBitReader
{
    private readonly ReadOnlySpan<byte> _data;
    private int _position;
    private ulong _buffer;
    private int _bitCount;

    // Zero bits appended above the real ones once the data ran out; consuming any of them is an error.
    private int _paddingBits;

    public BasisBitReader(ReadOnlySpan<byte> data)
    {
        _data = data;
        _position = 0;
        _buffer = 0;
        _bitCount = 0;
        _paddingBits = 0;
    }

    private void Fill()
    {
        while (_bitCount <= 56)
        {
            if (_position < _data.Length)
            {
                _buffer |= (ulong)_data[_position++] << _bitCount;
            }
            else
            {
                _paddingBits += 8;
            }

            _bitCount += 8;
        }
    }

    private void Consume(int count)
    {
        _buffer >>= count;
        _bitCount -= count;
        if (_bitCount < _paddingBits)
        {
            throw new InvalidDataException("An ETC1S bitstream is truncated.");
        }
    }

    /// <summary>Reads 0 to 32 bits.</summary>
    public uint ReadBits(int count)
    {
        if (_bitCount < count)
        {
            Fill();
        }

        uint value = (uint)(_buffer & ((1UL << count) - 1));
        Consume(count);
        return value;
    }

    /// <summary>Reads a variable length value: chunks of <paramref name="chunkBits"/> bits, each followed by a "more" bit.</summary>
    public uint ReadVlc(int chunkBits)
    {
        uint value = 0;
        for (int shift = 0; shift < 32; shift += chunkBits)
        {
            uint chunk = ReadBits(chunkBits + 1);
            value |= (chunk & ((1u << chunkBits) - 1)) << shift;
            if ((chunk >> chunkBits) == 0)
            {
                break;
            }
        }

        return value;
    }

    /// <summary>Decodes one symbol of a non-empty table.</summary>
    public int DecodeHuffman(BasisHuffmanTable table)
    {
        if (_bitCount < BasisHuffmanTable.MaxCodeLength)
        {
            Fill();
        }

        int entry = table.Lookup((uint)(_buffer & 0xFFFF));
        if (entry == 0)
        {
            throw new InvalidDataException("An ETC1S bitstream holds an invalid Huffman code.");
        }

        Consume(entry >> 16);
        return entry & 0xFFFF;
    }
}

/// <summary>A canonical Huffman code of the BasisLZ bitstream, as one lookup table over the longest code.</summary>
internal sealed class BasisHuffmanTable
{
    public const int MaxCodeLength = 16;
    private const int MaxSymbols = 1 << 14;
    private const int CodeLengthSymbols = 21;
    private const int SmallZeroRunCode = 17;
    private const int BigZeroRunCode = 18;
    private const int SmallRepeatCode = 19;

    private static readonly BasisHuffmanTable Empty = new([0], 0);

    // The order in which the lengths of the code length codes are stored.
    private static ReadOnlySpan<byte> CodeLengthOrder => [17, 18, 19, 20, 0, 8, 7, 9, 6, 10, 5, 11, 4, 12, 3, 13, 2, 14, 1, 15, 16];

    // (code length << 16) | symbol for every bit pattern of the longest code's length; 0 where no code matches.
    // The empty table is a single 0, so decoding with it fails like any other invalid code.
    private readonly int[] _lookup;
    private readonly uint _mask;

    private BasisHuffmanTable(int[] lookup, int bits)
    {
        _lookup = lookup;
        _mask = (1u << bits) - 1;
    }

    /// <summary>False for a table that was stored with no symbols.</summary>
    public bool IsValid => _mask != 0;

    public int Lookup(uint bits)
    {
        return _lookup[bits & _mask];
    }

    /// <summary>Reads a table: the code lengths are themselves Huffman coded, with run lengths.</summary>
    public static BasisHuffmanTable Read(ref BasisBitReader reader)
    {
        int symbolCount = (int)reader.ReadBits(14);
        if (symbolCount == 0)
        {
            return Empty;
        }

        int codeLengthCount = (int)reader.ReadBits(5);
        if (codeLengthCount < 1 || codeLengthCount > CodeLengthSymbols)
        {
            throw Invalid();
        }

        Span<byte> codeLengthSizes = stackalloc byte[CodeLengthSymbols];
        codeLengthSizes.Clear();
        ReadOnlySpan<byte> order = CodeLengthOrder;
        for (int i = 0; i < codeLengthCount; i++)
        {
            codeLengthSizes[order[i]] = (byte)reader.ReadBits(3);
        }

        BasisHuffmanTable codeLengths = Build(codeLengthSizes);

        var sizes = new byte[symbolCount];
        int current = 0;
        while (current < symbolCount)
        {
            int code = reader.DecodeHuffman(codeLengths);
            if (code <= MaxCodeLength)
            {
                sizes[current++] = (byte)code;
            }
            else if (code == SmallZeroRunCode)
            {
                current += (int)reader.ReadBits(3) + 3;
            }
            else if (code == BigZeroRunCode)
            {
                current += (int)reader.ReadBits(7) + 11;
            }
            else
            {
                if (current == 0)
                {
                    throw Invalid();
                }

                int run = code == SmallRepeatCode ? (int)reader.ReadBits(2) + 3 : (int)reader.ReadBits(7) + 7;
                byte previous = sizes[current - 1];
                if (previous == 0 || run > symbolCount - current)
                {
                    throw Invalid();
                }

                sizes.AsSpan(current, run).Fill(previous);
                current += run;
            }
        }

        if (current != symbolCount)
        {
            throw Invalid();
        }

        return Build(sizes);
    }

    private static BasisHuffmanTable Build(ReadOnlySpan<byte> sizes)
    {
        Span<int> counts = stackalloc int[MaxCodeLength + 1];
        counts.Clear();
        int maxLength = 0;
        foreach (byte size in sizes)
        {
            counts[size]++;
            maxLength = Math.Max(maxLength, size);
        }

        // The code must be complete, except that a table with a single symbol is allowed.
        int used = sizes.Length - counts[0];
        long space = 0;
        for (int length = 1; length <= MaxCodeLength; length++)
        {
            space += (long)counts[length] << (MaxCodeLength - length);
        }

        if (space != 1L << MaxCodeLength && used != 1)
        {
            throw Invalid();
        }

        Span<int> nextCode = stackalloc int[MaxCodeLength + 2];
        int code = 0;
        for (int length = 1; length <= MaxCodeLength; length++)
        {
            nextCode[length] = code;
            code = (code + counts[length]) << 1;
        }

        var lookup = new int[1 << maxLength];
        for (int symbol = 0; symbol < sizes.Length; symbol++)
        {
            int length = sizes[symbol];
            if (length == 0)
            {
                continue;
            }

            // Codes are assigned in symbol order within a length and are read from the low bit up, so the
            // table is indexed by the bit-reversed code.
            int assigned = nextCode[length]++;
            int reversed = 0;
            for (int bit = 0; bit < length; bit++)
            {
                reversed = (reversed << 1) | ((assigned >> bit) & 1);
            }

            int entry = (length << 16) | symbol;
            for (int index = reversed; index < lookup.Length; index += 1 << length)
            {
                lookup[index] = entry;
            }
        }

        return new BasisHuffmanTable(lookup, maxLength);
    }

    private static InvalidDataException Invalid()
    {
        return new InvalidDataException("An ETC1S Huffman table is corrupt.");
    }
}
