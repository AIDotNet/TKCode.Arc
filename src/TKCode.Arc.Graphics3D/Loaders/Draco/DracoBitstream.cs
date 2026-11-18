using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;

namespace TKCode.Arc.Graphics3D;

/// <summary>Creates the two exception kinds the decoder throws.</summary>
internal static class DracoError
{
    public static InvalidDataException Invalid(string message)
    {
        return new InvalidDataException("Draco: " + message);
    }

    public static NotSupportedException Unsupported(string message)
    {
        return new NotSupportedException("Draco: " + message);
    }
}

/// <summary>Bitstream versions as <c>major &lt;&lt; 8 | minor</c>, the form the format's branches compare.</summary>
internal static class DracoVersion
{
    public const int V1_2 = (1 << 8) | 2;
    public const int V1_3 = (1 << 8) | 3;
    public const int V2_0 = (2 << 8) | 0;
    public const int V2_1 = (2 << 8) | 1;
    public const int V2_2 = (2 << 8) | 2;
}

/// <summary>
/// Memory budget of one decode. A Draco stream can legitimately expand a lot (a flat, regular grid costs
/// almost nothing per face), so no fixed ratio is right for every file; but the working memory a decode
/// may ask for is tied to the input size, so that a small file cannot ask for gigabytes. Every array whose
/// size comes from the file is charged here before it is allocated.
/// </summary>
internal sealed class DracoLimits
{
    /// <summary>Most working memory a decode may charge, whatever the input size (about ten million triangles).</summary>
    public const long HardMaxBytes = 1L << 32;

    /// <summary>What every input may use.</summary>
    public const long FreeBytes = 4L << 20;

    /// <summary>
    /// Additional budget per input byte. The decoder needs 250 to 400 bytes per triangle, so this accepts
    /// files down to about 0.01 bits per triangle; real files use between one and thirty bits.
    /// </summary>
    public const long BytesPerInputByte = 1L << 18;

    private readonly bool _hardLimited;
    private long _charged;

    public DracoLimits(int inputLength)
    {
        long proportional = FreeBytes + (BytesPerInputByte * inputLength);
        _hardLimited = proportional > HardMaxBytes;
        Budget = Math.Min(proportional, HardMaxBytes);
    }

    public long Budget { get; }

    public void Charge(long bytes)
    {
        _charged += bytes;
        if (bytes < 0 || _charged > Budget)
        {
            throw Exceeded();
        }
    }

    /// <summary>Rejects a declared count early: that many elements of at least <paramref name="bytesEach"/> bytes cannot fit any more.</summary>
    public void CheckDeclared(long count, int bytesEach)
    {
        if (count < 0 || count > (Budget - _charged) / bytesEach)
        {
            throw Exceeded();
        }
    }

    public int[] Ints(long count)
    {
        Charge(count * sizeof(int));
        return new int[count];
    }

    public uint[] UInts(long count)
    {
        Charge(count * sizeof(uint));
        return new uint[count];
    }

    public bool[] Bools(long count)
    {
        Charge(count);
        return new bool[CheckLength(count)];
    }

    public byte[] Bytes(long count)
    {
        Charge(count);
        return new byte[CheckLength(count)];
    }

    private static long CheckLength(long count)
    {
        if (count > Array.MaxLength)
        {
            throw DracoError.Unsupported("an array would be larger than the runtime allows.");
        }

        return count;
    }

    private Exception Exceeded()
    {
        return _hardLimited
            ? DracoError.Unsupported($"the mesh needs more than {HardMaxBytes >> 20} MB of working memory.")
            : DracoError.Invalid("the file asks for more memory than a file of its size can need.");
    }
}

/// <summary>
/// Records which code paths a decode takes, for the coverage report of the verification harness.
/// The calls exist only in builds that define DRACO_TRACE; without it the compiler drops them.
/// </summary>
internal static class DracoTrace
{
    [ThreadStatic]
    private static HashSet<string>? s_paths;

    /// <summary>Starts (or, with null, stops) recording on the current thread.</summary>
    [Conditional("DRACO_TRACE")]
    public static void Record(HashSet<string>? paths)
    {
        s_paths = paths;
    }

    [Conditional("DRACO_TRACE")]
    public static void Add(string path)
    {
        s_paths?.Add(path);
    }
}

/// <summary>Bounds-checked little-endian reader over the whole input.</summary>
internal sealed class DracoBuffer
{
    private readonly byte[] _data;
    private int _position;

    public DracoBuffer(byte[] data)
    {
        _data = data;
        Limits = new DracoLimits(data.Length);
    }

    public DracoLimits Limits { get; }

    public byte[] Data => _data;

    public int Length => _data.Length;

    public int Position => _position;

    public int Remaining => _data.Length - _position;

    /// <summary>Bitstream version as <c>major &lt;&lt; 8 | minor</c>.</summary>
    public int Version { get; set; }

    public void Seek(long position)
    {
        if (position < 0 || position > _data.Length)
        {
            throw Truncated();
        }

        _position = (int)position;
    }

    public void Skip(long count)
    {
        if (count < 0 || count > Remaining)
        {
            throw Truncated();
        }

        _position += (int)count;
    }

    public byte ReadByte()
    {
        if (_position >= _data.Length)
        {
            throw Truncated();
        }

        return _data[_position++];
    }

    public sbyte ReadSByte()
    {
        return unchecked((sbyte)ReadByte());
    }

    public ushort ReadUInt16()
    {
        return BinaryPrimitives.ReadUInt16LittleEndian(Take(2));
    }

    public uint ReadUInt32()
    {
        return BinaryPrimitives.ReadUInt32LittleEndian(Take(4));
    }

    public int ReadInt32()
    {
        return BinaryPrimitives.ReadInt32LittleEndian(Take(4));
    }

    public ulong ReadUInt64()
    {
        return BinaryPrimitives.ReadUInt64LittleEndian(Take(8));
    }

    public float ReadSingle()
    {
        return BinaryPrimitives.ReadSingleLittleEndian(Take(4));
    }

    public ReadOnlySpan<byte> Take(int count)
    {
        if (count < 0 || count > Remaining)
        {
            throw Truncated();
        }

        ReadOnlySpan<byte> span = _data.AsSpan(_position, count);
        _position += count;
        return span;
    }

    public uint ReadVarUInt32()
    {
        // The reference keeps the low 32 bits of at most five bytes.
        return unchecked((uint)ReadVarint(5));
    }

    public ulong ReadVarUInt64()
    {
        return ReadVarint(10);
    }

    /// <summary>A count that is a fixed 32-bit value before <paramref name="varintSince"/> and a varint from it on.</summary>
    public uint ReadCount(int varintSince)
    {
        return Version < varintSince ? ReadUInt32() : ReadVarUInt32();
    }

    /// <summary>Starts reading single bits at the current position; the caller skips what the reader consumed.</summary>
    public DracoBitReader StartBits()
    {
        return new DracoBitReader(_data, _position, _data.Length);
    }

    private ulong ReadVarint(int maxBytes)
    {
        ulong result = 0;
        for (int i = 0; i < maxBytes; i++)
        {
            byte value = ReadByte();
            int shift = 7 * i;
            if (shift < 64)
            {
                result |= (ulong)(value & 0x7F) << shift;
            }

            if ((value & 0x80) == 0)
            {
                return result;
            }
        }

        throw DracoError.Invalid("a variable-length integer is too long.");
    }

    private static InvalidDataException Truncated()
    {
        return DracoError.Invalid("the data ends unexpectedly.");
    }
}

/// <summary>Reads bit fields, least significant bit first, from a byte range.</summary>
internal sealed class DracoBitReader
{
    private readonly byte[] _data;
    private readonly int _start;
    private readonly long _bitLength;
    private long _bit;

    public DracoBitReader(byte[] data, int start, int end)
    {
        _data = data;
        _start = start;
        _bitLength = (long)(end - start) << 3;
    }

    /// <summary>Whole bytes touched so far.</summary>
    public int BytesConsumed => (int)((_bit + 7) >> 3);

    public uint ReadBits(int count)
    {
        if ((uint)count > 32)
        {
            throw DracoError.Invalid("a bit field is wider than 32 bits.");
        }

        if (count == 0)
        {
            return 0;
        }

        long end = _bit + count;
        if (end > _bitLength)
        {
            throw DracoError.Invalid("a bit sequence ends unexpectedly.");
        }

        int index = _start + (int)(_bit >> 3);
        int shift = (int)(_bit & 7);
        int bytes = (shift + count + 7) >> 3;
        ulong accumulator = 0;
        for (int i = 0; i < bytes; i++)
        {
            accumulator |= (ulong)_data[index + i] << (8 * i);
        }

        _bit = end;
        return (uint)((accumulator >> shift) & ((1UL << count) - 1));
    }
}
