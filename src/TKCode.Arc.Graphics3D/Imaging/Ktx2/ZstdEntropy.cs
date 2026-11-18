// Zstandard entropy coding: FSE tables, Huffman literals and the bit readers they share.
//
// Written from RFC 8878 sections 3.1.1.3 (blocks), 4.1 (FSE) and 4.2 (Huffman), and checked against the
// reference "educational decoder" (doc/educational_decoder of https://github.com/facebook/zstd, BSD license).

using System.Buffers.Binary;
using System.Numerics;

namespace TKCode.Arc.Graphics3D;

internal sealed partial class ZstdDecoder
{
    private const int MaxHuffmanLog = 12;
    private const int MaxHuffmanWeightLog = 6;
    private const int LiteralLengthCodes = 36;
    private const int MatchLengthCodes = 53;
    private const int OffsetCodes = 32;

    private static readonly int[] LiteralLengthBase =
    [
        0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15,
        16, 18, 20, 22, 24, 28, 32, 40, 48, 64, 128, 256, 512, 1024, 2048, 4096,
        8192, 16384, 32768, 65536,
    ];

    private static readonly byte[] LiteralLengthBits =
    [
        0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
        1, 1, 1, 1, 2, 2, 3, 3, 4, 6, 7, 8, 9, 10, 11, 12,
        13, 14, 15, 16,
    ];

    private static readonly int[] MatchLengthBase =
    [
        3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18,
        19, 20, 21, 22, 23, 24, 25, 26, 27, 28, 29, 30, 31, 32, 33, 34,
        35, 37, 39, 41, 43, 47, 51, 59, 67, 83, 99, 131, 259, 515, 1027, 2051,
        4099, 8195, 16387, 32771, 65539,
    ];

    private static readonly byte[] MatchLengthBits =
    [
        0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
        0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
        1, 1, 1, 1, 2, 2, 3, 3, 4, 4, 5, 7, 8, 9, 10, 11,
        12, 13, 14, 15, 16,
    ];

    private static readonly FseTable DefaultLiteralLengths = FseTable.FromDistribution(
        [4, 3, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 1, 1, 1, 2, 2, 2, 2, 2, 2, 2, 2, 2, 3, 2, 1, 1, 1, 1, 1, -1, -1, -1, -1],
        6);

    private static readonly FseTable DefaultMatchLengths = FseTable.FromDistribution(
        [
            1, 4, 3, 2, 2, 2, 2, 2, 2, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1,
            1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, -1, -1, -1, -1, -1, -1, -1,
        ],
        6);

    private static readonly FseTable DefaultOffsets = FseTable.FromDistribution(
        [1, 1, 1, 1, 1, 1, 2, 2, 2, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, -1, -1, -1, -1, -1],
        5);

    // The tables a block uses, kept for "repeat" mode in the next block of the same frame. Each is the
    // predefined table or the decoder's own table of that kind.
    private FseTable? _literalLengthTable;
    private FseTable? _offsetTable;
    private FseTable? _matchLengthTable;
    private readonly FseTable _ownLiteralLengthTable = new(9);
    private readonly FseTable _ownOffsetTable = new(8);
    private readonly FseTable _ownMatchLengthTable = new(9);
    private readonly FseTable _weightTable = new(MaxHuffmanWeightLog);

    private readonly byte[] _huffmanSymbols = new byte[1 << MaxHuffmanLog];
    private readonly byte[] _huffmanBits = new byte[1 << MaxHuffmanLog];
    private int _huffmanLog;
    private bool _huffmanValid;

    private void ResetEntropy()
    {
        _literalLengthTable = null;
        _offsetTable = null;
        _matchLengthTable = null;
        _huffmanValid = false;
    }

    /// <summary>An FSE decoding table: for every state the symbol it emits and how the next state is read.</summary>
    private sealed class FseTable
    {
        public readonly byte[] Symbols;
        public readonly byte[] Bits;
        public readonly ushort[] Base;
        public int Log;

        public FseTable(int maxLog)
        {
            Symbols = new byte[1 << maxLog];
            Bits = new byte[1 << maxLog];
            Base = new ushort[1 << maxLog];
        }

        public static FseTable FromDistribution(ReadOnlySpan<short> distribution, int log)
        {
            var table = new FseTable(log);
            table.Build(distribution, log);
            return table;
        }

        public void SetSingleSymbol(byte symbol)
        {
            Log = 0;
            Symbols[0] = symbol;
            Bits[0] = 0;
            Base[0] = 0;
        }

        /// <summary>Builds the table from normalized counts that sum to 1 &lt;&lt; log (-1 counts as one).</summary>
        public void Build(ReadOnlySpan<short> distribution, int log)
        {
            int size = 1 << log;
            Span<ushort> next = stackalloc ushort[256];

            // Symbols with a "less than one" probability take single states at the end of the table.
            int high = size;
            for (int s = 0; s < distribution.Length; s++)
            {
                if (distribution[s] == -1)
                {
                    Symbols[--high] = (byte)s;
                    next[s] = 1;
                }
            }

            // The step is odd and the size a power of two, so the walk visits every state once and the
            // inner loop always finds a free state below `high`.
            int step = (size >> 1) + (size >> 3) + 3;
            int mask = size - 1;
            int pos = 0;
            for (int s = 0; s < distribution.Length; s++)
            {
                int count = distribution[s];
                if (count <= 0)
                {
                    continue;
                }

                next[s] = (ushort)count;
                for (int i = 0; i < count; i++)
                {
                    Symbols[pos] = (byte)s;
                    do
                    {
                        pos = (pos + step) & mask;
                    }
                    while (pos >= high);
                }
            }

            if (pos != 0)
            {
                throw Corrupt("invalid FSE distribution");
            }

            for (int i = 0; i < size; i++)
            {
                int state = next[Symbols[i]]++;
                int bits = log - BitOperations.Log2((uint)state);
                Bits[i] = (byte)bits;
                Base[i] = (ushort)((state << bits) - size);
            }

            Log = log;
        }
    }

    /// <summary>Selects the table of one sequence field for this block; returns the bytes its description used.</summary>
    private static int SelectTable(ref FseTable? table, FseTable own, int mode, ReadOnlySpan<byte> source, int maxLog, int symbolLimit, FseTable predefined)
    {
        switch (mode)
        {
            case 0:
                table = predefined;
                return 0;

            case 1:
                if (source.Length < 1)
                {
                    throw Truncated();
                }

                if (source[0] >= symbolLimit)
                {
                    throw Corrupt("sequence code out of range");
                }

                own.SetSingleSymbol(source[0]);
                table = own;
                return 1;

            case 2:
                Span<short> distribution = stackalloc short[symbolLimit];
                int used = ReadFseDistribution(source, maxLog, distribution, out int log, out int symbols);
                own.Build(distribution.Slice(0, symbols), log);
                table = own;
                return used;

            default:
                if (table is null)
                {
                    throw Corrupt("sequences reuse a table that was never sent");
                }

                return 0;
        }
    }

    /// <summary>Reads an FSE table description (a forward bitstream); returns the bytes it occupies.</summary>
    private static int ReadFseDistribution(ReadOnlySpan<byte> source, int maxLog, Span<short> distribution, out int log, out int symbols)
    {
        int bitPos = 0;
        log = 5 + ForwardBits(source, ref bitPos, 4);
        if (log > maxLog)
        {
            throw Corrupt("FSE accuracy too high");
        }

        int remaining = 1 << log;
        int symbol = 0;
        while (remaining > 0 && symbol < distribution.Length)
        {
            // A value below the threshold is written with one bit less.
            int bits = BitOperations.Log2((uint)(remaining + 1)) + 1;
            int peekPos = bitPos;
            int value = ForwardBits(source, ref peekPos, bits);
            int lowerMask = (1 << (bits - 1)) - 1;
            int threshold = (1 << bits) - 1 - (remaining + 1);
            if ((value & lowerMask) < threshold)
            {
                bitPos += bits - 1;
                value &= lowerMask;
            }
            else
            {
                bitPos += bits;
                if (value > lowerMask)
                {
                    value -= threshold;
                }
            }

            int probability = value - 1;
            remaining -= probability < 0 ? 1 : probability;
            distribution[symbol++] = (short)probability;

            if (probability == 0)
            {
                // Runs of zero probabilities: two bits each, 3 meaning "three more and another count follows".
                // Bits past the end read as zero, which ends the run.
                int repeat;
                do
                {
                    repeat = ForwardBits(source, ref bitPos, 2);
                    for (int i = 0; i < repeat && symbol < distribution.Length; i++)
                    {
                        distribution[symbol++] = 0;
                    }
                }
                while (repeat == 3);
            }
        }

        int bytes = (bitPos + 7) >> 3;
        if (remaining != 0 || bytes > source.Length)
        {
            throw Corrupt("invalid FSE table description");
        }

        symbols = symbol;
        return bytes;
    }

    /// <summary>Reads up to 16 bits, least significant first; bits past the end of the data are zero.</summary>
    private static int ForwardBits(ReadOnlySpan<byte> source, ref int bitPos, int count)
    {
        int index = bitPos >> 3;
        uint value = 0;
        for (int i = 0; i < 3; i++)
        {
            if (index + i < source.Length)
            {
                value |= (uint)source[index + i] << (8 * i);
            }
        }

        value >>= bitPos & 7;
        bitPos += count;
        return (int)(value & ((1u << count) - 1));
    }

    /// <summary>Position of the first bit of a backward bitstream: just below the marker bit of its last byte.</summary>
    private static long BackwardStart(ReadOnlySpan<byte> stream)
    {
        if (stream.Length == 0)
        {
            throw Truncated();
        }

        int last = stream[stream.Length - 1];
        if (last == 0)
        {
            throw Corrupt("bitstream end marker missing");
        }

        return (long)(stream.Length - 1) * 8 + BitOperations.Log2((uint)last);
    }

    /// <summary>
    /// The <paramref name="count"/> (0 to 32) bits below <paramref name="bitEnd"/> of a backward bitstream.
    /// Bits before the start of the stream read as zero; callers detect that by the position going negative.
    /// </summary>
    private static uint PeekBits(ReadOnlySpan<byte> stream, long bitEnd, int count)
    {
        long start = bitEnd - count;
        if (start >= 0)
        {
            return (uint)(BitsFrom(stream, start) & ((1UL << count) - 1));
        }

        if (bitEnd <= 0)
        {
            return 0;
        }

        int available = (int)bitEnd;
        return (uint)((BitsFrom(stream, 0) & ((1UL << available) - 1)) << (count - available));
    }

    /// <summary>At least 57 bits of the stream starting at bit <paramref name="start"/>, least significant first.</summary>
    private static ulong BitsFrom(ReadOnlySpan<byte> stream, long start)
    {
        int index = (int)(start >> 3);
        int shift = (int)(start & 7);
        if (index <= stream.Length - 8)
        {
            return BinaryPrimitives.ReadUInt64LittleEndian(stream.Slice(index)) >> shift;
        }

        ulong value = 0;
        for (int i = stream.Length - 1; i >= index; i--)
        {
            value = (value << 8) | stream[i];
        }

        return value >> shift;
    }

    /// <summary>Reads a Huffman tree description and builds the decoding table; returns the bytes it occupies.</summary>
    private int ReadHuffmanTree(ReadOnlySpan<byte> source)
    {
        if (source.Length < 1)
        {
            throw Truncated();
        }

        Span<byte> weights = stackalloc byte[256];
        int header = source[0];
        int count;
        int used;
        if (header >= 128)
        {
            // Direct: four bits per weight.
            count = header - 127;
            used = 1 + (count + 1) / 2;
            if (source.Length < used)
            {
                throw Truncated();
            }

            for (int i = 0; i < count; i++)
            {
                int pair = source[1 + (i >> 1)];
                weights[i] = (byte)((i & 1) == 0 ? pair >> 4 : pair & 15);
            }
        }
        else
        {
            used = 1 + header;
            if (source.Length < used)
            {
                throw Truncated();
            }

            count = DecodeHuffmanWeights(source.Slice(1, header), weights);
        }

        // The last weight is not stored: it completes the sum of 2^(weight - 1) to a power of two.
        int sum = 0;
        for (int i = 0; i < count; i++)
        {
            int weight = weights[i];
            if (weight > MaxHuffmanLog)
            {
                throw Corrupt("Huffman weight out of range");
            }

            if (weight > 0)
            {
                sum += 1 << (weight - 1);
            }
        }

        if (sum == 0)
        {
            throw Corrupt("empty Huffman tree");
        }

        int log = BitOperations.Log2((uint)sum) + 1;
        int rest = (1 << log) - sum;
        if (log > MaxHuffmanLog || (rest & (rest - 1)) != 0)
        {
            throw Corrupt("invalid Huffman weights");
        }

        weights[count++] = (byte)(BitOperations.Log2((uint)rest) + 1);

        // Codes are assigned by increasing weight (longest codes first), then by symbol value; a symbol of
        // weight w owns 2^(w - 1) consecutive entries of the table.
        Span<int> start = stackalloc int[MaxHuffmanLog + 2];
        for (int i = 0; i < count; i++)
        {
            start[weights[i] + 1] += weights[i] == 0 ? 0 : 1 << (weights[i] - 1);
        }

        start[1] = 0;
        for (int w = 2; w <= MaxHuffmanLog; w++)
        {
            start[w] += start[w - 1];
        }

        for (int i = 0; i < count; i++)
        {
            int weight = weights[i];
            if (weight == 0)
            {
                continue;
            }

            int length = 1 << (weight - 1);
            _huffmanSymbols.AsSpan(start[weight], length).Fill((byte)i);
            _huffmanBits.AsSpan(start[weight], length).Fill((byte)(log + 1 - weight));
            start[weight] += length;
        }

        _huffmanLog = log;
        _huffmanValid = true;
        return used;
    }

    /// <summary>Decodes FSE-compressed Huffman weights (two interleaved states); returns how many there are.</summary>
    private int DecodeHuffmanWeights(ReadOnlySpan<byte> source, Span<byte> weights)
    {
        Span<short> distribution = stackalloc short[256];
        int used = ReadFseDistribution(source, MaxHuffmanWeightLog, distribution, out int log, out int symbols);
        FseTable table = _weightTable;
        table.Build(distribution.Slice(0, symbols), log);

        ReadOnlySpan<byte> stream = source.Slice(used);
        long bitPos = BackwardStart(stream);
        int state1 = (int)PeekBits(stream, bitPos, log);
        bitPos -= log;
        int state2 = (int)PeekBits(stream, bitPos, log);
        bitPos -= log;

        // The stream ends when a state update needs more bits than are left; the other state then holds
        // the last symbol. At most 255 weights can be stored (the 256th is implicit).
        const int Limit = 255;
        int count = 0;
        while (true)
        {
            if (count > Limit - 2)
            {
                throw Corrupt("too many Huffman weights");
            }

            weights[count++] = table.Symbols[state1];
            int bits = table.Bits[state1];
            state1 = table.Base[state1] + (int)PeekBits(stream, bitPos, bits);
            bitPos -= bits;
            if (bitPos < 0)
            {
                weights[count++] = table.Symbols[state2];
                break;
            }

            if (count > Limit - 2)
            {
                throw Corrupt("too many Huffman weights");
            }

            weights[count++] = table.Symbols[state2];
            bits = table.Bits[state2];
            state2 = table.Base[state2] + (int)PeekBits(stream, bitPos, bits);
            bitPos -= bits;
            if (bitPos < 0)
            {
                weights[count++] = table.Symbols[state1];
                break;
            }
        }

        return count;
    }

    private void DecodeHuffmanLiterals(ReadOnlySpan<byte> source, Span<byte> literals, bool fourStreams)
    {
        if (!fourStreams)
        {
            DecodeHuffmanStream(source, literals);
            return;
        }

        // Three little-endian sizes; the fourth stream takes the rest. Each of the first three streams
        // holds a quarter of the literals, rounded up.
        if (source.Length < 6)
        {
            throw Truncated();
        }

        int size1 = source[0] | (source[1] << 8);
        int size2 = source[2] | (source[3] << 8);
        int size3 = source[4] | (source[5] << 8);
        int size4 = source.Length - 6 - size1 - size2 - size3;
        int quarter = (literals.Length + 3) / 4;
        int lastCount = literals.Length - 3 * quarter;
        if (size4 < 0 || lastCount < 0)
        {
            throw Corrupt("invalid literal stream sizes");
        }

        int pos = 6;
        DecodeHuffmanStream(source.Slice(pos, size1), literals.Slice(0, quarter));
        pos += size1;
        DecodeHuffmanStream(source.Slice(pos, size2), literals.Slice(quarter, quarter));
        pos += size2;
        DecodeHuffmanStream(source.Slice(pos, size3), literals.Slice(2 * quarter, quarter));
        pos += size3;
        DecodeHuffmanStream(source.Slice(pos, size4), literals.Slice(3 * quarter, lastCount));
    }

    private void DecodeHuffmanStream(ReadOnlySpan<byte> stream, Span<byte> literals)
    {
        long bitPos = BackwardStart(stream);
        int log = _huffmanLog;
        byte[] symbols = _huffmanSymbols;
        byte[] lengths = _huffmanBits;
        for (int i = 0; i < literals.Length; i++)
        {
            uint index = PeekBits(stream, bitPos, log);
            literals[i] = symbols[index];
            bitPos -= lengths[index];
        }

        // A stream holds exactly the bits of its symbols.
        if (bitPos != 0)
        {
            throw Corrupt("literal bitstream length mismatch");
        }
    }
}
