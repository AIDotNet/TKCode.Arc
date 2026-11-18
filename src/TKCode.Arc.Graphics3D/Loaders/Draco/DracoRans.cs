using System;

namespace TKCode.Arc.Graphics3D;

/// <summary>rANS decoder for symbols with a probability table stored in the stream.</summary>
internal sealed class DracoRansSymbolDecoder
{
    // No encoder writes more: raw symbols are at most 18 bits wide and tags are bit lengths (0..32).
    private const int MaxSymbols = 1 << 20;

    private readonly int _precisionBits;
    private readonly uint _precision;
    private readonly uint _base;
    private uint[] _lookup = Array.Empty<uint>();
    private uint[] _probability = Array.Empty<uint>();
    private uint[] _cumulative = Array.Empty<uint>();
    private byte[] _data = Array.Empty<byte>();
    private int _start;
    private int _offset;
    private uint _state;

    public DracoRansSymbolDecoder(int uniqueSymbolsBitLength)
    {
        _precisionBits = Math.Clamp((3 * uniqueSymbolsBitLength) / 2, 12, 20);
        _precision = 1u << _precisionBits;
        _base = _precision * 4;
    }

    /// <summary>Reads the probability table.</summary>
    public void Create(DracoBuffer buffer)
    {
        uint numSymbols = buffer.ReadCount(DracoVersion.V2_0);
        if (numSymbols == 0)
        {
            throw DracoError.Invalid("a symbol table is empty.");
        }

        // A run of up to 64 zero-probability symbols takes one byte.
        if (numSymbols / 64 > (uint)buffer.Remaining || numSymbols > MaxSymbols)
        {
            throw DracoError.Invalid("a symbol table is larger than the data that holds it.");
        }

        int count = (int)numSymbols;
        DracoLimits limits = buffer.Limits;
        uint[] probability = limits.UInts(count);
        for (int i = 0; i < count; i++)
        {
            byte data = buffer.ReadByte();
            int token = data & 3;
            if (token == 3)
            {
                int run = data >> 2;
                if (i + run >= count)
                {
                    throw DracoError.Invalid("a symbol table run goes past its end.");
                }

                i += run;
            }
            else
            {
                uint value = (uint)(data >> 2);
                for (int b = 0; b < token; b++)
                {
                    value |= (uint)buffer.ReadByte() << ((8 * (b + 1)) - 2);
                }

                probability[i] = value;
            }
        }

        uint[] cumulative = limits.UInts(count);
        uint[] lookup = limits.UInts(_precision);
        uint total = 0;
        for (int i = 0; i < count; i++)
        {
            uint value = probability[i];
            cumulative[i] = total;
            if (value > _precision - total)
            {
                throw DracoError.Invalid("symbol probabilities do not add up.");
            }

            if (value != 0)
            {
                lookup.AsSpan((int)total, (int)value).Fill((uint)i);
                total += value;
            }
        }

        if (total != _precision)
        {
            throw DracoError.Invalid("symbol probabilities do not add up.");
        }

        _probability = probability;
        _cumulative = cumulative;
        _lookup = lookup;
    }

    /// <summary>Reads the size of the coded data and positions the decoder at its end; the buffer moves past it.</summary>
    public void Start(DracoBuffer buffer)
    {
        ulong size = buffer.Version < DracoVersion.V2_0 ? buffer.ReadUInt64() : buffer.ReadVarUInt64();
        if (size > (ulong)buffer.Remaining)
        {
            throw DracoError.Invalid("entropy coded data is longer than the file.");
        }

        _data = buffer.Data;
        _start = buffer.Position;
        int length = (int)size;
        buffer.Skip(length);
        _state = DracoRansBitDecoder.ReadInitialState(_data, _start, length, allowFourBytes: true, out _offset) + _base;
        if (_state >= _base * 256)
        {
            throw DracoError.Invalid("an entropy coder state is out of range.");
        }
    }

    public uint DecodeSymbol()
    {
        uint state = _state;
        while (state < _base)
        {
            // An encoder never leaves the state below the base, so running dry means the stream is damaged.
            // Failing here also keeps a broken stream from producing symbols without consuming input.
            if (_offset <= _start)
            {
                throw DracoError.Invalid("entropy coded data ends unexpectedly.");
            }

            state = (state << 8) | _data[--_offset];
        }

        uint quotient = state >> _precisionBits;
        uint remainder = state & (_precision - 1);
        uint symbol = _lookup[remainder];
        _state = unchecked((quotient * _probability[symbol]) + remainder - _cumulative[symbol]);
        return symbol;
    }
}

/// <summary>rABS decoder for a sequence of bits that share one probability.</summary>
internal sealed class DracoRansBitDecoder
{
    private const uint Base = 4096;

    private readonly byte[] _data;
    private readonly int _start;
    private readonly byte _probabilityZero;
    private int _offset;
    private uint _state;

    private DracoRansBitDecoder(byte[] data, int start, int length, byte probabilityZero)
    {
        _data = data;
        _start = start;
        _probabilityZero = probabilityZero;
        _state = ReadInitialState(data, start, length, allowFourBytes: false, out _offset) + Base;
        if (_state >= Base * 256)
        {
            throw DracoError.Invalid("an entropy coder state is out of range.");
        }
    }

    /// <summary>Reads the header of a coded bit sequence and moves the buffer past it.</summary>
    public static DracoRansBitDecoder Start(DracoBuffer buffer)
    {
        byte probabilityZero = buffer.ReadByte();
        uint size = buffer.ReadCount(DracoVersion.V2_2);
        if (size > (uint)buffer.Remaining)
        {
            throw DracoError.Invalid("entropy coded data is longer than the file.");
        }

        int start = buffer.Position;
        buffer.Skip(size);
        return new DracoRansBitDecoder(buffer.Data, start, (int)size, probabilityZero);
    }

    public bool DecodeBit()
    {
        unchecked
        {
            // The probability of a one is kept in a byte, as the format's coder does: 256 wraps to 0.
            uint p = (byte)(256 - _probabilityZero);
            uint state = _state;
            if (state < Base && _offset > _start)
            {
                state = (state << 8) | _data[--_offset];
            }

            uint quotient = state >> 8;
            uint remainder = state & 0xFF;
            uint scaled = quotient * p;
            if (remainder < p)
            {
                _state = scaled + remainder;
                return true;
            }

            _state = state - scaled - p;
            return false;
        }
    }

    /// <summary>The coder state is stored at the end of the data, in one to four bytes; returns it without the base.</summary>
    internal static uint ReadInitialState(byte[] data, int start, int length, bool allowFourBytes, out int offset)
    {
        if (length < 1)
        {
            throw DracoError.Invalid("entropy coded data is empty.");
        }

        int end = start + length;
        int bytes = (data[end - 1] >> 6) + 1;
        if (bytes > length || (bytes == 4 && !allowFourBytes))
        {
            throw DracoError.Invalid("an entropy coder state is damaged.");
        }

        offset = end - bytes;
        uint state = 0;
        for (int i = bytes - 1; i >= 0; i--)
        {
            state = (state << 8) | data[offset + i];
        }

        return state & ((1u << ((8 * bytes) - 2)) - 1);
    }
}

/// <summary>Decodes arrays of unsigned symbols (the "tagged" and "raw" schemes).</summary>
internal static class DracoSymbols
{
    private const int InitialCapacity = 1 << 14;

    /// <summary>
    /// Decodes <paramref name="numValues"/> symbols. The result is charged to the memory budget at once, but
    /// it grows while decoding instead of being allocated from the declared count, so that a damaged
    /// stream fails before it really costs memory.
    /// </summary>
    public static int[] Decode(DracoBuffer buffer, int numValues, int numComponents)
    {
        if (numValues == 0)
        {
            return Array.Empty<int>();
        }

        if (numComponents <= 0 || numValues % numComponents != 0)
        {
            throw DracoError.Invalid("a symbol count does not match its component count.");
        }


        // Twice the result: growing by doubling allocates about as much again.
        buffer.Limits.Charge(2L * numValues * sizeof(int));
        byte scheme = buffer.ReadByte();
        if (scheme == 0)
        {
            DracoTrace.Add("symbols.tagged");
            return DecodeTagged(buffer, numValues, numComponents);
        }

        if (scheme == 1)
        {
            return DecodeRaw(buffer, numValues);
        }

        throw DracoError.Invalid("unknown symbol coding scheme.");
    }

    private static int[] DecodeTagged(DracoBuffer buffer, int numValues, int numComponents)
    {
        // Each tag is the bit length shared by the components of one entry; the values follow as plain bits.
        DracoRansSymbolDecoder tags = new(5);
        tags.Create(buffer);
        tags.Start(buffer);
        DracoBitReader bits = buffer.StartBits();
        int[] values = new int[Math.Min(numValues, Math.Max(InitialCapacity, numComponents))];
        for (int i = 0; i < numValues; i += numComponents)
        {
            if (i + numComponents > values.Length)
            {
                values = Grow(values, numValues);
            }

            int bitLength = (int)tags.DecodeSymbol();
            for (int c = 0; c < numComponents; c++)
            {
                values[i + c] = unchecked((int)bits.ReadBits(bitLength));
            }
        }

        buffer.Skip(bits.BytesConsumed);
        return values;
    }

    private static int[] DecodeRaw(DracoBuffer buffer, int numValues)
    {
        byte maxBitLength = buffer.ReadByte();
        if (maxBitLength < 1 || maxBitLength > 18)
        {
            throw DracoError.Invalid("unsupported symbol bit length.");
        }

        DracoTrace.Add("symbols.raw");
        DracoTrace.Add($"symbols.raw.bitLength{maxBitLength}");
        DracoRansSymbolDecoder decoder = new(maxBitLength);
        decoder.Create(buffer);
        decoder.Start(buffer);
        int[] values = new int[Math.Min(numValues, InitialCapacity)];
        int done = 0;
        while (true)
        {
            for (int i = done; i < values.Length; i++)
            {
                values[i] = unchecked((int)decoder.DecodeSymbol());
            }

            done = values.Length;
            if (done == numValues)
            {
                return values;
            }

            values = Grow(values, numValues);
        }
    }

    private static int[] Grow(int[] values, int limit)
    {
        int length = (int)Math.Min(limit, values.Length * 2L);
        Array.Resize(ref values, length);
        return values;
    }

    /// <summary>Maps the unsigned symbols back to signed values (even: positive, odd: negative).</summary>
    public static void ToSigned(int[] values)
    {
        for (int i = 0; i < values.Length; i++)
        {
            uint symbol = unchecked((uint)values[i]);
            int half = (int)(symbol >> 1);
            values[i] = (symbol & 1) == 0 ? half : -half - 1;
        }
    }
}
