// Zstandard decompressor (frames, blocks, literals and sequences).
//
// Written from RFC 8878, "Zstandard Compression and the 'application/zstd' Media Type", and checked against the
// description in the reference "educational decoder" (doc/educational_decoder of https://github.com/facebook/zstd,
// BSD license). The predefined distributions and the length code tables are the ones the RFC lists.
// The entropy tables are in ZstdEntropy.cs.

using System.Buffers.Binary;

namespace TKCode.Arc.Graphics3D;

/// <summary>Decompresses Zstandard frames into one managed buffer. Dictionaries are not supported.</summary>
internal sealed partial class ZstdDecoder
{
    private const uint FrameMagic = 0xFD2FB528;
    private const uint SkippableMagic = 0x184D2A50;
    private const uint SkippableMagicMask = 0xFFFFFFF0;
    private const int MaxBlockSize = 1 << 17;

    private readonly int _maxOutput;
    private byte[] _output;
    private int _outputLength;
    private int _frameStart;

    private readonly byte[] _literals = new byte[MaxBlockSize];
    private readonly long[] _repeatOffsets = new long[3];

    private ZstdDecoder(int sourceLength, int maxOutput)
    {
        _maxOutput = maxOutput;

        // Start small: the output grows as data is produced, so a short input cannot reserve a large buffer
        // just by declaring a large size.
        long initial = Math.Max(4096L, Math.Min((long)sourceLength * 4, int.MaxValue));
        _output = new byte[(int)Math.Min(initial, maxOutput)];
    }

    /// <summary>
    /// Decompresses every frame in <paramref name="source"/> (skippable frames are skipped) and returns the
    /// concatenated content. Throws <see cref="InvalidDataException"/> when the data is corrupt or would
    /// produce more than <paramref name="maxOutputSize"/> bytes.
    /// </summary>
    public static byte[] Decompress(ReadOnlySpan<byte> source, int maxOutputSize)
    {
        if (maxOutputSize < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxOutputSize));
        }

        var decoder = new ZstdDecoder(source.Length, maxOutputSize);
        int pos = 0;
        while (pos < source.Length)
        {
            if (source.Length - pos < 4)
            {
                throw Truncated();
            }

            uint magic = BinaryPrimitives.ReadUInt32LittleEndian(source.Slice(pos));
            if ((magic & SkippableMagicMask) == SkippableMagic)
            {
                if (source.Length - pos < 8)
                {
                    throw Truncated();
                }

                uint size = BinaryPrimitives.ReadUInt32LittleEndian(source.Slice(pos + 4));
                if (size > (uint)(source.Length - pos - 8))
                {
                    throw Truncated();
                }

                pos += 8 + (int)size;
                continue;
            }

            if (magic != FrameMagic)
            {
                throw new InvalidDataException("The data is not a Zstandard frame.");
            }

            pos = decoder.DecodeFrame(source, pos + 4);
        }

        return decoder.TakeOutput();
    }

    private byte[] TakeOutput()
    {
        if (_output.Length != _outputLength)
        {
            Array.Resize(ref _output, _outputLength);
        }

        return _output;
    }

    private static InvalidDataException Truncated()
    {
        return new InvalidDataException("The Zstandard data is truncated.");
    }

    private static InvalidDataException Corrupt(string what)
    {
        return new InvalidDataException("The Zstandard data is corrupt: " + what + ".");
    }

    private void Reserve(int count)
    {
        long needed = (long)_outputLength + count;
        if (needed > _maxOutput)
        {
            throw new InvalidDataException("The Zstandard data expands beyond the expected size.");
        }

        if (needed > _output.Length)
        {
            long capacity = Math.Max(needed, Math.Min((long)_output.Length * 2, _maxOutput));
            Array.Resize(ref _output, (int)capacity);
        }
    }

    private int DecodeFrame(ReadOnlySpan<byte> source, int pos)
    {
        if (pos >= source.Length)
        {
            throw Truncated();
        }

        int descriptor = source[pos++];
        int contentSizeFlag = descriptor >> 6;
        bool singleSegment = (descriptor & 0x20) != 0;
        bool hasChecksum = (descriptor & 0x04) != 0;
        int dictionaryFlag = descriptor & 3;
        if ((descriptor & 0x08) != 0)
        {
            throw Corrupt("reserved frame header bit set");
        }

        long windowSize = 0;
        if (!singleSegment)
        {
            if (pos >= source.Length)
            {
                throw Truncated();
            }

            int window = source[pos++];
            long windowBase = 1L << (10 + (window >> 3));
            windowSize = windowBase + (windowBase >> 3) * (window & 7);
        }

        int dictionaryBytes = dictionaryFlag == 3 ? 4 : dictionaryFlag;
        int contentSizeBytes = contentSizeFlag switch
        {
            0 => singleSegment ? 1 : 0,
            1 => 2,
            2 => 4,
            _ => 8,
        };
        if (source.Length - pos < dictionaryBytes + contentSizeBytes)
        {
            throw Truncated();
        }

        uint dictionaryId = 0;
        for (int i = 0; i < dictionaryBytes; i++)
        {
            dictionaryId |= (uint)source[pos++] << (8 * i);
        }

        if (dictionaryId != 0)
        {
            throw new NotSupportedException("Zstandard frames that need a dictionary are not supported.");
        }

        long contentSize = -1;
        if (contentSizeBytes > 0)
        {
            ulong value = 0;
            for (int i = 0; i < contentSizeBytes; i++)
            {
                value |= (ulong)source[pos++] << (8 * i);
            }

            if (contentSizeBytes == 2)
            {
                value += 256;
            }

            if (value > (ulong)(_maxOutput - _outputLength))
            {
                throw new InvalidDataException("The Zstandard data expands beyond the expected size.");
            }

            contentSize = (long)value;
        }

        if (singleSegment)
        {
            windowSize = contentSize;
        }

        int blockLimit = (int)Math.Min(windowSize, MaxBlockSize);

        _frameStart = _outputLength;
        _repeatOffsets[0] = 1;
        _repeatOffsets[1] = 4;
        _repeatOffsets[2] = 8;
        ResetEntropy();

        // Every pass consumes at least the three header bytes, so the loop ends with the input.
        while (true)
        {
            if (source.Length - pos < 3)
            {
                throw Truncated();
            }

            int header = source[pos] | (source[pos + 1] << 8) | (source[pos + 2] << 16);
            pos += 3;
            bool last = (header & 1) != 0;
            int type = (header >> 1) & 3;
            int size = header >> 3;
            if (type == 3)
            {
                throw Corrupt("reserved block type");
            }

            if (size > blockLimit)
            {
                throw Corrupt("block larger than the frame allows");
            }

            if (type == 1)
            {
                if (pos >= source.Length)
                {
                    throw Truncated();
                }

                Reserve(size);
                _output.AsSpan(_outputLength, size).Fill(source[pos]);
                _outputLength += size;
                pos++;
            }
            else
            {
                if (source.Length - pos < size)
                {
                    throw Truncated();
                }

                ReadOnlySpan<byte> block = source.Slice(pos, size);
                if (type == 0)
                {
                    Reserve(size);
                    block.CopyTo(_output.AsSpan(_outputLength));
                    _outputLength += size;
                }
                else
                {
                    DecodeCompressedBlock(block);
                }

                pos += size;
            }

            if (last)
            {
                break;
            }
        }

        if (contentSize >= 0 && _outputLength - _frameStart != contentSize)
        {
            throw Corrupt("frame content size mismatch");
        }

        if (hasChecksum)
        {
            if (source.Length - pos < 4)
            {
                throw Truncated();
            }

            uint expected = BinaryPrimitives.ReadUInt32LittleEndian(source.Slice(pos));
            uint actual = (uint)XxHash64(_output.AsSpan(_frameStart, _outputLength - _frameStart));
            if (expected != actual)
            {
                throw Corrupt("content checksum mismatch");
            }

            pos += 4;
        }

        return pos;
    }

    private void DecodeCompressedBlock(ReadOnlySpan<byte> block)
    {
        if (block.Length < 1)
        {
            throw Truncated();
        }

        int first = block[0];
        int type = first & 3;
        int sizeFormat = (first >> 2) & 3;
        int headerSize;
        int regeneratedSize;
        int compressedSize = 0;
        bool fourStreams = false;

        if (type < 2)
        {
            // Raw and RLE literals: 5, 12 or 20 bits of size.
            headerSize = sizeFormat == 1 ? 2 : sizeFormat == 3 ? 3 : 1;
            if (block.Length < headerSize)
            {
                throw Truncated();
            }

            regeneratedSize = headerSize switch
            {
                1 => first >> 3,
                2 => (first >> 4) | (block[1] << 4),
                _ => (first >> 4) | (block[1] << 4) | (block[2] << 12),
            };
        }
        else
        {
            // Huffman literals: two sizes of 10, 14 or 18 bits each.
            headerSize = sizeFormat < 2 ? 3 : sizeFormat + 2;
            if (block.Length < headerSize)
            {
                throw Truncated();
            }

            ulong header = 0;
            for (int i = 0; i < headerSize; i++)
            {
                header |= (ulong)block[i] << (8 * i);
            }

            int sizeBits = sizeFormat < 2 ? 10 : sizeFormat == 2 ? 14 : 18;
            ulong mask = (1UL << sizeBits) - 1;
            regeneratedSize = (int)((header >> 4) & mask);
            compressedSize = (int)((header >> (4 + sizeBits)) & mask);
            fourStreams = sizeFormat != 0;
        }

        if (regeneratedSize > MaxBlockSize)
        {
            throw Corrupt("literals larger than a block");
        }

        int pos = headerSize;
        Span<byte> literals = _literals.AsSpan(0, regeneratedSize);
        switch (type)
        {
            case 0:
                if (block.Length - pos < regeneratedSize)
                {
                    throw Truncated();
                }

                block.Slice(pos, regeneratedSize).CopyTo(literals);
                pos += regeneratedSize;
                break;

            case 1:
                if (block.Length - pos < 1)
                {
                    throw Truncated();
                }

                literals.Fill(block[pos]);
                pos++;
                break;

            default:
                if (block.Length - pos < compressedSize)
                {
                    throw Truncated();
                }

                ReadOnlySpan<byte> compressed = block.Slice(pos, compressedSize);
                if (type == 2)
                {
                    compressed = compressed.Slice(ReadHuffmanTree(compressed));
                }
                else if (!_huffmanValid)
                {
                    throw Corrupt("literals reuse a Huffman table that was never sent");
                }

                DecodeHuffmanLiterals(compressed, literals, fourStreams);
                pos += compressedSize;
                break;
        }

        DecodeSequences(block.Slice(pos), regeneratedSize);
    }

    private void DecodeSequences(ReadOnlySpan<byte> section, int literalCount)
    {
        if (section.Length < 1)
        {
            throw Truncated();
        }

        int blockStart = _outputLength;
        int pos = 1;
        int count = section[0];
        if (count == 255)
        {
            if (section.Length < 3)
            {
                throw Truncated();
            }

            count = section[1] + (section[2] << 8) + 0x7F00;
            pos = 3;
        }
        else if (count >= 128)
        {
            if (section.Length < 2)
            {
                throw Truncated();
            }

            count = ((count - 128) << 8) + section[1];
            pos = 2;
        }

        if (count == 0)
        {
            if (pos != section.Length)
            {
                throw Corrupt("data after an empty sequence section");
            }

            Reserve(literalCount);
            _literals.AsSpan(0, literalCount).CopyTo(_output.AsSpan(_outputLength));
            _outputLength += literalCount;
            return;
        }

        if (pos >= section.Length)
        {
            throw Truncated();
        }

        int modes = section[pos++];
        if ((modes & 3) != 0)
        {
            throw Corrupt("reserved sequence mode bits set");
        }

        pos += SelectTable(ref _literalLengthTable, _ownLiteralLengthTable, modes >> 6, section.Slice(pos), 9, LiteralLengthCodes, DefaultLiteralLengths);
        pos += SelectTable(ref _offsetTable, _ownOffsetTable, (modes >> 4) & 3, section.Slice(pos), 8, OffsetCodes, DefaultOffsets);
        pos += SelectTable(ref _matchLengthTable, _ownMatchLengthTable, (modes >> 2) & 3, section.Slice(pos), 9, MatchLengthCodes, DefaultMatchLengths);

        FseTable literalLengths = _literalLengthTable!;
        FseTable offsets = _offsetTable!;
        FseTable matchLengths = _matchLengthTable!;

        ReadOnlySpan<byte> stream = section.Slice(pos);
        long bitPos = BackwardStart(stream);
        int literalLengthState = (int)PeekBits(stream, bitPos, literalLengths.Log);
        bitPos -= literalLengths.Log;
        int offsetState = (int)PeekBits(stream, bitPos, offsets.Log);
        bitPos -= offsets.Log;
        int matchLengthState = (int)PeekBits(stream, bitPos, matchLengths.Log);
        bitPos -= matchLengths.Log;
        if (bitPos < 0)
        {
            throw Truncated();
        }

        long repeat1 = _repeatOffsets[0];
        long repeat2 = _repeatOffsets[1];
        long repeat3 = _repeatOffsets[2];
        int literalPos = 0;

        for (int i = 0; i < count; i++)
        {
            int offsetCode = offsets.Symbols[offsetState];
            int matchLengthCode = matchLengths.Symbols[matchLengthState];
            int literalLengthCode = literalLengths.Symbols[literalLengthState];

            long offsetValue = (1L << offsetCode) + PeekBits(stream, bitPos, offsetCode);
            bitPos -= offsetCode;
            int extraBits = MatchLengthBits[matchLengthCode];
            int matchLength = MatchLengthBase[matchLengthCode] + (int)PeekBits(stream, bitPos, extraBits);
            bitPos -= extraBits;
            extraBits = LiteralLengthBits[literalLengthCode];
            int literalLength = LiteralLengthBase[literalLengthCode] + (int)PeekBits(stream, bitPos, extraBits);
            bitPos -= extraBits;

            // The states are not updated after the last sequence: the stream ends with its extra bits.
            if (i + 1 < count)
            {
                int bits = literalLengths.Bits[literalLengthState];
                literalLengthState = literalLengths.Base[literalLengthState] + (int)PeekBits(stream, bitPos, bits);
                bitPos -= bits;
                bits = matchLengths.Bits[matchLengthState];
                matchLengthState = matchLengths.Base[matchLengthState] + (int)PeekBits(stream, bitPos, bits);
                bitPos -= bits;
                bits = offsets.Bits[offsetState];
                offsetState = offsets.Base[offsetState] + (int)PeekBits(stream, bitPos, bits);
                bitPos -= bits;
            }

            if (bitPos < 0)
            {
                throw Truncated();
            }

            long offset;
            if (offsetValue > 3)
            {
                offset = offsetValue - 3;
                repeat3 = repeat2;
                repeat2 = repeat1;
                repeat1 = offset;
            }
            else
            {
                // With no literals the three repeat codes shift by one and the last means "repeat1 - 1".
                int index = (int)offsetValue - (literalLength == 0 ? 0 : 1);
                if (index == 0)
                {
                    offset = repeat1;
                }
                else
                {
                    offset = index == 1 ? repeat2 : index == 2 ? repeat3 : repeat1 - 1;
                    if (offset == 0)
                    {
                        throw Corrupt("zero match offset");
                    }

                    if (index != 1)
                    {
                        repeat3 = repeat2;
                    }

                    repeat2 = repeat1;
                    repeat1 = offset;
                }
            }

            if (literalLength > literalCount - literalPos)
            {
                throw Corrupt("sequences use more literals than the block has");
            }

            if ((long)_outputLength - blockStart + literalLength + matchLength > MaxBlockSize)
            {
                throw Corrupt("block content larger than a block");
            }

            Reserve(literalLength + matchLength);
            _literals.AsSpan(literalPos, literalLength).CopyTo(_output.AsSpan(_outputLength));
            literalPos += literalLength;
            _outputLength += literalLength;

            if (offset > _outputLength - _frameStart)
            {
                throw Corrupt("match offset before the start of the frame");
            }

            CopyMatch((int)offset, matchLength);
        }

        if (bitPos != 0)
        {
            throw Corrupt("sequence bitstream not fully consumed");
        }

        _repeatOffsets[0] = repeat1;
        _repeatOffsets[1] = repeat2;
        _repeatOffsets[2] = repeat3;

        int rest = literalCount - literalPos;
        if ((long)_outputLength - blockStart + rest > MaxBlockSize)
        {
            throw Corrupt("block content larger than a block");
        }

        Reserve(rest);
        _literals.AsSpan(literalPos, rest).CopyTo(_output.AsSpan(_outputLength));
        _outputLength += rest;
    }

    private void CopyMatch(int offset, int length)
    {
        byte[] output = _output;
        int to = _outputLength;
        int from = to - offset;
        if (offset >= length)
        {
            output.AsSpan(from, length).CopyTo(output.AsSpan(to));
        }
        else if (offset == 1)
        {
            output.AsSpan(to, length).Fill(output[from]);
        }
        else
        {
            // The match overlaps what it writes: it repeats the last `offset` bytes.
            for (int i = 0; i < length; i++)
            {
                output[to + i] = output[from + i];
            }
        }

        _outputLength = to + length;
    }

    private static ulong XxHash64(ReadOnlySpan<byte> data)
    {
        const ulong Prime1 = 11400714785074694791UL;
        const ulong Prime2 = 14029467366897019727UL;
        const ulong Prime3 = 1609587929392839161UL;
        const ulong Prime4 = 9650029242287828579UL;
        const ulong Prime5 = 2870177450012600261UL;

        static ulong Round(ulong accumulator, ulong input)
        {
            return unchecked(ulong.RotateLeft(accumulator + input * Prime2, 31) * Prime1);
        }

        // The hash relies on 64-bit wrap-around, whatever the project's overflow checking is.
        unchecked
        {
            int pos = 0;
            ulong hash;
            if (data.Length >= 32)
            {
                ulong v1 = Prime1 + Prime2;
                ulong v2 = Prime2;
                ulong v3 = 0;
                ulong v4 = 0UL - Prime1;
                while (data.Length - pos >= 32)
                {
                    v1 = Round(v1, BinaryPrimitives.ReadUInt64LittleEndian(data.Slice(pos)));
                    v2 = Round(v2, BinaryPrimitives.ReadUInt64LittleEndian(data.Slice(pos + 8)));
                    v3 = Round(v3, BinaryPrimitives.ReadUInt64LittleEndian(data.Slice(pos + 16)));
                    v4 = Round(v4, BinaryPrimitives.ReadUInt64LittleEndian(data.Slice(pos + 24)));
                    pos += 32;
                }

                hash = ulong.RotateLeft(v1, 1) + ulong.RotateLeft(v2, 7) + ulong.RotateLeft(v3, 12) + ulong.RotateLeft(v4, 18);
                hash = (hash ^ Round(0, v1)) * Prime1 + Prime4;
                hash = (hash ^ Round(0, v2)) * Prime1 + Prime4;
                hash = (hash ^ Round(0, v3)) * Prime1 + Prime4;
                hash = (hash ^ Round(0, v4)) * Prime1 + Prime4;
            }
            else
            {
                hash = Prime5;
            }

            hash += (ulong)data.Length;
            while (data.Length - pos >= 8)
            {
                hash ^= Round(0, BinaryPrimitives.ReadUInt64LittleEndian(data.Slice(pos)));
                hash = ulong.RotateLeft(hash, 27) * Prime1 + Prime4;
                pos += 8;
            }

            if (data.Length - pos >= 4)
            {
                hash ^= BinaryPrimitives.ReadUInt32LittleEndian(data.Slice(pos)) * Prime1;
                hash = ulong.RotateLeft(hash, 23) * Prime2 + Prime3;
                pos += 4;
            }

            while (pos < data.Length)
            {
                hash ^= data[pos] * Prime5;
                hash = ulong.RotateLeft(hash, 11) * Prime1;
                pos++;
            }

            hash ^= hash >> 33;
            hash *= Prime2;
            hash ^= hash >> 29;
            hash *= Prime3;
            hash ^= hash >> 32;
            return hash;
        }
    }
}
