using System.Runtime.CompilerServices;

namespace TKCode.Arc.Graphics3D;

/// <summary>Decodes JPEG images without any other library: baseline and progressive DCT, Huffman coding, 8 bits per
/// sample, grayscale, YCbCr, RGB and (Adobe) CMYK / YCCK, any chroma subsampling, restart intervals. Decoded images
/// are 8-bit RGBA, rows top to bottom. The EXIF orientation is ignored.</summary>
public static class Jpeg
{
    private static readonly byte[] s_zigzag =
    [
        0, 1, 8, 16, 9, 2, 3, 10, 17, 24, 32, 25, 18, 11, 4, 5, 12, 19, 26, 33, 40, 48, 41, 34, 27, 20, 13, 6, 7, 14, 21, 28,
        35, 42, 49, 56, 57, 50, 43, 36, 29, 22, 15, 23, 30, 37, 44, 51, 58, 59, 52, 45, 38, 31, 39, 46, 53, 60, 61, 54, 47, 55, 62, 63,
    ];

    /// <summary>True when the data starts with the JPEG start-of-image marker.</summary>
    public static bool IsJpeg(ReadOnlySpan<byte> data) => data.Length >= 3 && data[0] == 0xFF && data[1] == 0xD8 && data[2] == 0xFF;

    /// <summary>Decodes a JPEG file to an 8-bit RGBA image.</summary>
    public static TextureImage Decode(ReadOnlySpan<byte> data)
    {
        if (!IsJpeg(data))
        {
            throw new InvalidDataException("Not a JPEG file.");
        }

        var decoder = new Decoder(data);
        return decoder.Run();
    }

    private sealed class HuffmanTable
    {
        private const int FastBits = 9;
        private readonly ushort[] _fast = new ushort[1 << FastBits];
        private readonly int[] _maxCode = new int[18];
        private readonly int[] _minCode = new int[17];
        private readonly int[] _valuePointer = new int[17];
        private readonly byte[] _values;

        public HuffmanTable(ReadOnlySpan<byte> counts, ReadOnlySpan<byte> values)
        {
            _values = values.ToArray();
            var code = 0;
            var k = 0;
            for (var length = 1; length <= 16; length++)
            {
                var count = counts[length - 1];
                _valuePointer[length] = k;
                _minCode[length] = code;
                if (code + count > 1 << length)
                {
                    throw new InvalidDataException("A JPEG Huffman table is invalid.");
                }

                if (length <= FastBits)
                {
                    for (var i = 0; i < count; i++)
                    {
                        var first = (code + i) << (FastBits - length);
                        var entry = (ushort)((length << 8) | _values[k + i]);
                        for (var fill = 0; fill < 1 << (FastBits - length); fill++)
                        {
                            _fast[first + fill] = entry;
                        }
                    }
                }

                code += count;
                k += count;
                _maxCode[length] = count == 0 ? -1 : code - 1;
                code <<= 1;
            }

            _maxCode[17] = int.MaxValue;
        }

        /// <summary>Decodes one symbol from the top bits of <paramref name="bits"/> (16 valid bits, left aligned in 32).</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int Decode(uint bits, out int length)
        {
            var entry = _fast[bits >> (32 - FastBits)];
            if (entry != 0)
            {
                length = entry >> 8;
                return entry & 0xFF;
            }

            for (var l = FastBits + 1; l <= 16; l++)
            {
                var code = (int)(bits >> (32 - l));
                if (code <= _maxCode[l])
                {
                    length = l;
                    return _values[_valuePointer[l] + code - _minCode[l]];
                }
            }

            throw new InvalidDataException("The JPEG entropy data is corrupt.");
        }
    }

    private sealed class Component
    {
        public int Id;
        public int H;
        public int V;
        public int QuantizationId;
        public int BlocksPerLine;
        public int BlocksPerColumn;
        public int BlocksPerLineForMcu;
        public int BlocksPerColumnForMcu;
        public short[] Coefficients = [];
        public HuffmanTable? DcTable;
        public HuffmanTable? AcTable;
        public int Prediction;
    }

    private enum BlockMode
    {
        Baseline,
        DcFirst,
        DcRefine,
        AcFirst,
        AcRefine,
    }

    private ref struct Decoder
    {
        private readonly ReadOnlySpan<byte> _data;
        private int _position;
        private uint _bitBuffer;
        private int _bitCount;
        private bool _atMarker;
        private int _width;
        private int _height;
        private bool _progressive;
        private bool _sawFrame;
        private int _maxH;
        private int _maxV;
        private int _mcusPerLine;
        private int _mcusPerColumn;
        private Component[] _components;
        private readonly ushort[]?[] _quantization;
        private readonly HuffmanTable?[] _dcTables;
        private readonly HuffmanTable?[] _acTables;
        private int _restartInterval;
        private int _adobeTransform;
        private bool _adobe;
        private int _eobRun;
        private int _refineState;
        private int _refineNext;
        private int _spectralStart;
        private int _spectralEnd;
        private int _successive;

        public Decoder(ReadOnlySpan<byte> data)
        {
            _data = data;
            _position = 2;
            _components = [];
            _quantization = new ushort[]?[4];
            _dcTables = new HuffmanTable?[4];
            _acTables = new HuffmanTable?[4];
            _adobeTransform = -1;
        }

        public TextureImage Run()
        {
            var done = false;
            while (!done)
            {
                var marker = NextMarker();
                switch (marker)
                {
                    case 0xD9:
                    case -1:
                        done = true;
                        break;
                    case 0xDB:
                        ReadQuantization();
                        break;
                    case 0xC4:
                        ReadHuffman();
                        break;
                    case 0xC0:
                    case 0xC1:
                    case 0xC2:
                        ReadFrame(marker == 0xC2);
                        break;
                    case 0xC3:
                    case 0xC5:
                    case 0xC6:
                    case 0xC7:
                    case 0xCB:
                    case 0xCD:
                    case 0xCE:
                    case 0xCF:
                        throw new NotSupportedException("Lossless and hierarchical JPEG files are not supported.");
                    case 0xC9:
                    case 0xCA:
                        throw new NotSupportedException("JPEG files with arithmetic coding are not supported.");
                    case 0xDD:
                    {
                        var segment = Segment();
                        if (segment.Length < 2)
                        {
                            throw new InvalidDataException("The JPEG restart interval is invalid.");
                        }

                        _restartInterval = (segment[0] << 8) | segment[1];
                        break;
                    }

                    case 0xDA:
                        ReadScan();
                        break;
                    case 0xDC:
                        throw new NotSupportedException("JPEG files that define their height after the first scan (DNL) are not supported.");
                    case 0xEE:
                    {
                        var segment = Segment();
                        if (segment.Length >= 12 && segment[..5].SequenceEqual("Adobe"u8))
                        {
                            _adobe = true;
                            _adobeTransform = segment[11];
                        }

                        break;
                    }

                    default:
                        if (marker is >= 0xD0 and <= 0xD7 or 0x01)
                        {
                            break;
                        }

                        Segment();
                        break;
                }
            }

            if (!_sawFrame)
            {
                throw new InvalidDataException("The JPEG file has no image frame.");
            }

            return BuildImage();
        }

        /// <summary>Moves to the next marker and returns its code, or -1 at the end of the data.</summary>
        private int NextMarker()
        {
            while (_position + 1 < _data.Length)
            {
                if (_data[_position] == 0xFF && _data[_position + 1] != 0x00 && _data[_position + 1] != 0xFF)
                {
                    var marker = _data[_position + 1];
                    _position += 2;
                    return marker;
                }

                _position++;
            }

            _position = _data.Length;
            return -1;
        }

        private ReadOnlySpan<byte> Segment()
        {
            if (_position + 2 > _data.Length)
            {
                throw new InvalidDataException("The JPEG file is truncated.");
            }

            var length = (_data[_position] << 8) | _data[_position + 1];
            if (length < 2 || _position + length > _data.Length)
            {
                throw new InvalidDataException("The JPEG file is truncated.");
            }

            var segment = _data.Slice(_position + 2, length - 2);
            _position += length;
            return segment;
        }

        private void ReadQuantization()
        {
            var segment = Segment();
            var i = 0;
            while (i < segment.Length)
            {
                var info = segment[i++];
                var id = info & 15;
                var wide = info >> 4 != 0;
                var size = wide ? 128 : 64;
                if (id > 3 || i + size > segment.Length)
                {
                    throw new InvalidDataException("A JPEG quantization table is invalid.");
                }

                var table = new ushort[64];
                for (var k = 0; k < 64; k++)
                {
                    table[s_zigzag[k]] = wide ? (ushort)((segment[i + (k * 2)] << 8) | segment[i + (k * 2) + 1]) : segment[i + k];
                }

                _quantization[id] = table;
                i += size;
            }
        }

        private void ReadHuffman()
        {
            var segment = Segment();
            var i = 0;
            while (i < segment.Length)
            {
                if (i + 17 > segment.Length)
                {
                    throw new InvalidDataException("A JPEG Huffman table is truncated.");
                }

                var info = segment[i];
                var counts = segment.Slice(i + 1, 16);
                var total = 0;
                foreach (var c in counts)
                {
                    total += c;
                }

                i += 17;
                var id = info & 15;
                if (id > 3 || total > 256 || i + total > segment.Length)
                {
                    throw new InvalidDataException("A JPEG Huffman table is invalid.");
                }

                var table = new HuffmanTable(counts, segment.Slice(i, total));
                if (info >> 4 == 0)
                {
                    _dcTables[id] = table;
                }
                else
                {
                    _acTables[id] = table;
                }

                i += total;
            }
        }

        private void ReadFrame(bool progressive)
        {
            if (_sawFrame)
            {
                throw new NotSupportedException("JPEG files with more than one frame are not supported.");
            }

            var segment = Segment();
            if (segment.Length < 6)
            {
                throw new InvalidDataException("The JPEG frame header is truncated.");
            }

            if (segment[0] != 8)
            {
                throw new NotSupportedException($"JPEG files with {segment[0]} bits per sample are not supported (only 8).");
            }

            _height = (segment[1] << 8) | segment[2];
            _width = (segment[3] << 8) | segment[4];
            var count = segment[5];
            if (_width == 0 || _height == 0 || (long)_width * _height > 1 << 28)
            {
                throw new InvalidDataException("The JPEG image size is invalid.");
            }

            if (count is not (1 or 3 or 4) || segment.Length < 6 + (count * 3))
            {
                throw new NotSupportedException($"JPEG files with {count} components are not supported.");
            }

            _progressive = progressive;
            _components = new Component[count];
            _maxH = 1;
            _maxV = 1;
            for (var i = 0; i < count; i++)
            {
                var o = 6 + (i * 3);
                var c = new Component { Id = segment[o], H = segment[o + 1] >> 4, V = segment[o + 1] & 15, QuantizationId = segment[o + 2] };
                if (c.H is < 1 or > 4 || c.V is < 1 or > 4 || c.QuantizationId > 3)
                {
                    throw new InvalidDataException("A JPEG component is invalid.");
                }

                _maxH = Math.Max(_maxH, c.H);
                _maxV = Math.Max(_maxV, c.V);
                _components[i] = c;
            }

            _mcusPerLine = (_width + (8 * _maxH) - 1) / (8 * _maxH);
            _mcusPerColumn = (_height + (8 * _maxV) - 1) / (8 * _maxV);
            foreach (var c in _components)
            {
                c.BlocksPerLine = ((((_width * c.H) + _maxH - 1) / _maxH) + 7) / 8;
                c.BlocksPerColumn = ((((_height * c.V) + _maxV - 1) / _maxV) + 7) / 8;
                c.BlocksPerLineForMcu = _mcusPerLine * c.H;
                c.BlocksPerColumnForMcu = _mcusPerColumn * c.V;
                c.Coefficients = new short[64 * c.BlocksPerLineForMcu * c.BlocksPerColumnForMcu];
            }

            _sawFrame = true;
        }

        // ---- Entropy-coded data ----

        private void FillBits()
        {
            while (_bitCount <= 24)
            {
                uint b = 0;
                if (!_atMarker && _position < _data.Length)
                {
                    b = _data[_position];
                    if (b == 0xFF)
                    {
                        var next = _position + 1 < _data.Length ? _data[_position + 1] : (byte)0xD9;
                        if (next == 0)
                        {
                            _position += 2;
                        }
                        else if (next == 0xFF)
                        {
                            // A fill byte before a marker.
                            _position++;
                            continue;
                        }
                        else
                        {
                            _atMarker = true;
                            b = 0;
                        }
                    }
                    else
                    {
                        _position++;
                    }
                }

                _bitBuffer |= b << (24 - _bitCount);
                _bitCount += 8;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private int DecodeSymbol(HuffmanTable table)
        {
            if (_bitCount < 16)
            {
                FillBits();
            }

            var symbol = table.Decode(_bitBuffer, out var length);
            _bitBuffer <<= length;
            _bitCount -= length;
            return symbol;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private int ReadBits(int count)
        {
            if (count == 0)
            {
                return 0;
            }

            if (_bitCount < count)
            {
                FillBits();
            }

            var value = (int)(_bitBuffer >> (32 - count));
            _bitBuffer <<= count;
            _bitCount -= count;
            return value;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private int ReceiveExtend(int size)
        {
            if (size == 0)
            {
                return 0;
            }

            var value = ReadBits(size);
            return value < 1 << (size - 1) ? value - (1 << size) + 1 : value;
        }

        private void ReadScan()
        {
            if (!_sawFrame)
            {
                throw new InvalidDataException("A JPEG scan comes before the frame header.");
            }

            var segment = Segment();
            if (segment.Length < 1)
            {
                throw new InvalidDataException("The JPEG scan header is truncated.");
            }

            var count = segment[0];
            if (count is < 1 or > 4 || segment.Length < 4 + (count * 2))
            {
                throw new InvalidDataException("The JPEG scan header is invalid.");
            }

            var scan = new Component[count];
            for (var i = 0; i < count; i++)
            {
                var id = segment[1 + (i * 2)];
                var tables = segment[2 + (i * 2)];
                Component? component = null;
                foreach (var c in _components)
                {
                    if (c.Id == id)
                    {
                        component = c;
                        break;
                    }
                }

                if (component is null || tables >> 4 > 3 || (tables & 15) > 3)
                {
                    throw new InvalidDataException("A JPEG scan refers to an unknown component or table.");
                }

                component.DcTable = _dcTables[tables >> 4];
                component.AcTable = _acTables[tables & 15];
                scan[i] = component;
            }

            var o = 1 + (count * 2);
            _spectralStart = segment[o];
            _spectralEnd = segment[o + 1];
            var approximation = segment[o + 2];
            var successivePrevious = approximation >> 4;
            _successive = approximation & 15;
            BlockMode mode;
            if (!_progressive)
            {
                mode = BlockMode.Baseline;
                _spectralStart = 0;
                _spectralEnd = 63;
            }
            else if (_spectralStart == 0)
            {
                mode = successivePrevious == 0 ? BlockMode.DcFirst : BlockMode.DcRefine;
            }
            else
            {
                if (count != 1 || _spectralEnd > 63 || _spectralStart > _spectralEnd)
                {
                    throw new InvalidDataException("A progressive JPEG scan is invalid.");
                }

                mode = successivePrevious == 0 ? BlockMode.AcFirst : BlockMode.AcRefine;
            }

            foreach (var c in scan)
            {
                var needDc = mode is BlockMode.Baseline or BlockMode.DcFirst;
                var needAc = mode is BlockMode.Baseline or BlockMode.AcFirst or BlockMode.AcRefine;
                if ((needDc && c.DcTable is null) || (needAc && c.AcTable is null))
                {
                    throw new InvalidDataException("A JPEG scan uses a Huffman table that was not defined.");
                }
            }

            _bitBuffer = 0;
            _bitCount = 0;
            _atMarker = false;
            var single = count == 1;
            var total = single ? scan[0].BlocksPerLine * scan[0].BlocksPerColumn : _mcusPerLine * _mcusPerColumn;
            var mcu = 0;
            while (mcu < total)
            {
                var chunk = _restartInterval > 0 ? Math.Min(total - mcu, _restartInterval) : total;
                foreach (var c in scan)
                {
                    c.Prediction = 0;
                }

                _eobRun = 0;
                _refineState = 0;
                for (var n = 0; n < chunk; n++, mcu++)
                {
                    if (single)
                    {
                        var c = scan[0];
                        var row = mcu / c.BlocksPerLine;
                        var column = mcu % c.BlocksPerLine;
                        DecodeBlock(c, 64 * ((row * c.BlocksPerLineForMcu) + column), mode);
                        continue;
                    }

                    var mcuRow = mcu / _mcusPerLine;
                    var mcuColumn = mcu % _mcusPerLine;
                    foreach (var c in scan)
                    {
                        for (var v = 0; v < c.V; v++)
                        {
                            for (var h = 0; h < c.H; h++)
                            {
                                var row = (mcuRow * c.V) + v;
                                var column = (mcuColumn * c.H) + h;
                                DecodeBlock(c, 64 * ((row * c.BlocksPerLineForMcu) + column), mode);
                            }
                        }
                    }
                }

                if (mcu < total)
                {
                    // A restart marker follows: drop the padding bits and step over it.
                    _bitBuffer = 0;
                    _bitCount = 0;
                    _atMarker = false;
                    while (_position + 1 < _data.Length && !(_data[_position] == 0xFF && _data[_position + 1] != 0 && _data[_position + 1] != 0xFF))
                    {
                        _position++;
                    }

                    if (_position + 1 < _data.Length && _data[_position + 1] is >= 0xD0 and <= 0xD7)
                    {
                        _position += 2;
                    }
                    else
                    {
                        // Truncated or damaged: keep what was decoded.
                        break;
                    }
                }
            }

            _bitBuffer = 0;
            _bitCount = 0;
            _atMarker = false;
        }

        private void DecodeBlock(Component c, int offset, BlockMode mode)
        {
            var coefficients = c.Coefficients;
            switch (mode)
            {
                case BlockMode.Baseline:
                {
                    var t = DecodeSymbol(c.DcTable!);
                    if (t > 15)
                    {
                        throw new InvalidDataException("The JPEG entropy data is corrupt.");
                    }

                    c.Prediction += ReceiveExtend(t);
                    coefficients[offset] = (short)c.Prediction;
                    var k = 1;
                    while (k < 64)
                    {
                        var rs = DecodeSymbol(c.AcTable!);
                        var s = rs & 15;
                        var r = rs >> 4;
                        if (s == 0)
                        {
                            if (r < 15)
                            {
                                break;
                            }

                            k += 16;
                            continue;
                        }

                        k += r;
                        if (k > 63)
                        {
                            break;
                        }

                        coefficients[offset + s_zigzag[k]] = (short)ReceiveExtend(s);
                        k++;
                    }

                    break;
                }

                case BlockMode.DcFirst:
                {
                    var t = DecodeSymbol(c.DcTable!);
                    if (t > 15)
                    {
                        throw new InvalidDataException("The JPEG entropy data is corrupt.");
                    }

                    c.Prediction += ReceiveExtend(t) * (1 << _successive);
                    coefficients[offset] = (short)c.Prediction;
                    break;
                }

                case BlockMode.DcRefine:
                    coefficients[offset] |= (short)(ReadBits(1) << _successive);
                    break;
                case BlockMode.AcFirst:
                {
                    if (_eobRun > 0)
                    {
                        _eobRun--;
                        break;
                    }

                    var k = _spectralStart;
                    while (k <= _spectralEnd)
                    {
                        var rs = DecodeSymbol(c.AcTable!);
                        var s = rs & 15;
                        var r = rs >> 4;
                        if (s == 0)
                        {
                            if (r < 15)
                            {
                                _eobRun = ReadBits(r) + (1 << r) - 1;
                                break;
                            }

                            k += 16;
                            continue;
                        }

                        k += r;
                        if (k > 63)
                        {
                            break;
                        }

                        coefficients[offset + s_zigzag[k]] = (short)(ReceiveExtend(s) * (1 << _successive));
                        k++;
                    }

                    break;
                }

                default:
                    RefineAc(c, offset);
                    break;
            }
        }

        private void RefineAc(Component c, int offset)
        {
            var coefficients = c.Coefficients;
            var k = _spectralStart;
            var e = _spectralEnd;
            var r = 0;
            var bit = 1 << _successive;
            while (k <= e)
            {
                var z = offset + s_zigzag[k];
                var sign = coefficients[z] < 0 ? -1 : 1;
                switch (_refineState)
                {
                    case 0:
                    {
                        var rs = DecodeSymbol(c.AcTable!);
                        var s = rs & 15;
                        r = rs >> 4;
                        if (s == 0)
                        {
                            if (r < 15)
                            {
                                _eobRun = ReadBits(r) + (1 << r);
                                _refineState = 4;
                            }
                            else
                            {
                                r = 16;
                                _refineState = 1;
                            }
                        }
                        else
                        {
                            if (s != 1)
                            {
                                throw new InvalidDataException("The JPEG entropy data is corrupt.");
                            }

                            _refineNext = ReceiveExtend(s);
                            _refineState = r != 0 ? 2 : 3;
                        }

                        continue;
                    }

                    case 1:
                    case 2:
                        if (coefficients[z] != 0)
                        {
                            if ((coefficients[z] & bit) == 0)
                            {
                                coefficients[z] += (short)(sign * (ReadBits(1) << _successive));
                            }
                            else
                            {
                                ReadBits(1);
                            }
                        }
                        else
                        {
                            r--;
                            if (r == 0)
                            {
                                _refineState = _refineState == 2 ? 3 : 0;
                            }
                        }

                        break;
                    case 3:
                        if (coefficients[z] != 0)
                        {
                            if ((coefficients[z] & bit) == 0)
                            {
                                coefficients[z] += (short)(sign * (ReadBits(1) << _successive));
                            }
                            else
                            {
                                ReadBits(1);
                            }
                        }
                        else
                        {
                            coefficients[z] = (short)(_refineNext * bit);
                            _refineState = 0;
                        }

                        break;
                    default:
                        if (coefficients[z] != 0)
                        {
                            if ((coefficients[z] & bit) == 0)
                            {
                                coefficients[z] += (short)(sign * (ReadBits(1) << _successive));
                            }
                            else
                            {
                                ReadBits(1);
                            }
                        }

                        break;
                }

                k++;
            }

            if (_refineState == 4)
            {
                _eobRun--;
                if (_eobRun == 0)
                {
                    _refineState = 0;
                }
            }
            else if (_refineState != 0)
            {
                // Zero runs never cross a block boundary in a valid stream.
                _refineState = 0;
            }
        }

        // ---- Reconstruction ----

        private readonly TextureImage BuildImage()
        {
            var planes = new byte[_components.Length][];
            for (var i = 0; i < _components.Length; i++)
            {
                var c = _components[i];
                var table = _quantization[c.QuantizationId] ?? throw new InvalidDataException("A JPEG quantization table was not defined.");
                var plane = DecodePlane(c, table, out var planeWidth, out var planeHeight);
                planes[i] = c.H == _maxH && c.V == _maxV && planeWidth >= _width && planeHeight >= _height
                    ? Crop(plane, planeWidth, _width, _height)
                    : Upsample(plane, planeWidth, planeHeight, c.H, c.V);
            }

            var pixels = new byte[_width * _height * 4];
            var count = _width * _height;
            switch (_components.Length)
            {
                case 1:
                {
                    var y = planes[0];
                    for (var i = 0; i < count; i++)
                    {
                        var o = i * 4;
                        pixels[o] = pixels[o + 1] = pixels[o + 2] = y[i];
                        pixels[o + 3] = 255;
                    }

                    break;
                }

                case 3:
                {
                    // Adobe transform 0 and component ids 'R', 'G', 'B' mean the data is RGB already.
                    var rgb = (_adobe && _adobeTransform == 0)
                        || (_components[0].Id == 'R' && _components[1].Id == 'G' && _components[2].Id == 'B');
                    byte[] a = planes[0], b = planes[1], c = planes[2];
                    for (var i = 0; i < count; i++)
                    {
                        var o = i * 4;
                        if (rgb)
                        {
                            pixels[o] = a[i];
                            pixels[o + 1] = b[i];
                            pixels[o + 2] = c[i];
                        }
                        else
                        {
                            YccToRgb(a[i], b[i], c[i], out pixels[o], out pixels[o + 1], out pixels[o + 2]);
                        }

                        pixels[o + 3] = 255;
                    }

                    break;
                }

                default:
                {
                    byte[] a = planes[0], b = planes[1], c = planes[2], k = planes[3];
                    var ycck = _adobe && _adobeTransform == 2;
                    for (var i = 0; i < count; i++)
                    {
                        var o = i * 4;
                        byte cc = a[i], mm = b[i], yy = c[i];
                        if (ycck)
                        {
                            YccToRgb(a[i], b[i], c[i], out cc, out mm, out yy);
                        }

                        int kk = k[i];
                        if (!_adobe)
                        {
                            // Plain CMYK: ink amounts. Adobe files store them inverted, which the product below expects.
                            cc = (byte)(255 - cc);
                            mm = (byte)(255 - mm);
                            yy = (byte)(255 - yy);
                            kk = 255 - kk;
                        }

                        pixels[o] = (byte)(((cc * kk) + 127) / 255);
                        pixels[o + 1] = (byte)(((mm * kk) + 127) / 255);
                        pixels[o + 2] = (byte)(((yy * kk) + 127) / 255);
                        pixels[o + 3] = 255;
                    }

                    break;
                }
            }

            return new TextureImage(_width, _height, pixels);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void YccToRgb(int y, int cb, int cr, out byte r, out byte g, out byte b)
        {
            // 16.16 fixed point of the JFIF conversion.
            cb -= 128;
            cr -= 128;
            var y16 = (y << 16) + 32768;
            r = Clamp((y16 + (91881 * cr)) >> 16);
            g = Clamp((y16 - (22554 * cb) - (46802 * cr)) >> 16);
            b = Clamp((y16 + (116130 * cb)) >> 16);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static byte Clamp(int v) => (byte)(v < 0 ? 0 : v > 255 ? 255 : v);

        private static byte[] DecodePlane(Component c, ushort[] quantization, out int width, out int height)
        {
            width = c.BlocksPerLine * 8;
            height = c.BlocksPerColumn * 8;
            var plane = new byte[width * height];
            Span<int> work = stackalloc int[64];
            var coefficients = c.Coefficients;
            for (var row = 0; row < c.BlocksPerColumn; row++)
            {
                for (var column = 0; column < c.BlocksPerLine; column++)
                {
                    var block = coefficients.AsSpan(64 * ((row * c.BlocksPerLineForMcu) + column), 64);
                    InverseDct(block, quantization, work, plane.AsSpan((row * 8 * width) + (column * 8)), width);
                }
            }

            return plane;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void Dct1D(int s0, int s1, int s2, int s3, int s4, int s5, int s6, int s7, out int x0, out int x1, out int x2, out int x3, out int t0, out int t1, out int t2, out int t3)
        {
            var p2 = s2;
            var p3 = s6;
            var p1 = (p2 + p3) * 2217;
            t2 = p1 + (p3 * -7567);
            t3 = p1 + (p2 * 3135);
            p2 = s0;
            p3 = s4;
            t0 = (p2 + p3) * 4096;
            t1 = (p2 - p3) * 4096;
            x0 = t0 + t3;
            x3 = t0 - t3;
            x1 = t1 + t2;
            x2 = t1 - t2;
            t0 = s7;
            t1 = s5;
            t2 = s3;
            t3 = s1;
            p3 = t0 + t2;
            var p4 = t1 + t3;
            p1 = t0 + t3;
            p2 = t1 + t2;
            var p5 = (p3 + p4) * 4816;
            t0 *= 1223;
            t1 *= 8410;
            t2 *= 12586;
            t3 *= 6149;
            p1 = p5 + (p1 * -3685);
            p2 = p5 + (p2 * -10497);
            p3 *= -8034;
            p4 *= -1597;
            t3 += p1 + p4;
            t2 += p2 + p3;
            t1 += p2 + p4;
            t0 += p1 + p3;
        }

        /// <summary>Dequantizes one block and writes its 8x8 samples (the integer inverse DCT of the Independent JPEG
        /// Group's "slow" method, as used by stb_image).</summary>
        private static void InverseDct(ReadOnlySpan<short> block, ushort[] q, Span<int> work, Span<byte> output, int stride)
        {
            for (var i = 0; i < 8; i++)
            {
                if (block[i + 8] == 0 && block[i + 16] == 0 && block[i + 24] == 0 && block[i + 32] == 0
                    && block[i + 40] == 0 && block[i + 48] == 0 && block[i + 56] == 0)
                {
                    var dc = block[i] * q[i] * 4;
                    work[i] = work[i + 8] = work[i + 16] = work[i + 24] = work[i + 32] = work[i + 40] = work[i + 48] = work[i + 56] = dc;
                    continue;
                }

                Dct1D(
                    block[i] * q[i], block[i + 8] * q[i + 8], block[i + 16] * q[i + 16], block[i + 24] * q[i + 24],
                    block[i + 32] * q[i + 32], block[i + 40] * q[i + 40], block[i + 48] * q[i + 48], block[i + 56] * q[i + 56],
                    out var x0, out var x1, out var x2, out var x3, out var t0, out var t1, out var t2, out var t3);
                x0 += 512;
                x1 += 512;
                x2 += 512;
                x3 += 512;
                work[i] = (x0 + t3) >> 10;
                work[i + 56] = (x0 - t3) >> 10;
                work[i + 8] = (x1 + t2) >> 10;
                work[i + 48] = (x1 - t2) >> 10;
                work[i + 16] = (x2 + t1) >> 10;
                work[i + 40] = (x2 - t1) >> 10;
                work[i + 24] = (x3 + t0) >> 10;
                work[i + 32] = (x3 - t0) >> 10;
            }

            for (var row = 0; row < 8; row++)
            {
                var w = work.Slice(row * 8, 8);
                Dct1D(w[0], w[1], w[2], w[3], w[4], w[5], w[6], w[7], out var x0, out var x1, out var x2, out var x3, out var t0, out var t1, out var t2, out var t3);
                // Rounding, plus the level shift of 128.
                const int Bias = 65536 + (128 << 17);
                x0 += Bias;
                x1 += Bias;
                x2 += Bias;
                x3 += Bias;
                var o = output.Slice(row * stride, 8);
                o[0] = Clamp((x0 + t3) >> 17);
                o[7] = Clamp((x0 - t3) >> 17);
                o[1] = Clamp((x1 + t2) >> 17);
                o[6] = Clamp((x1 - t2) >> 17);
                o[2] = Clamp((x2 + t1) >> 17);
                o[5] = Clamp((x2 - t1) >> 17);
                o[3] = Clamp((x3 + t0) >> 17);
                o[4] = Clamp((x3 - t0) >> 17);
            }
        }

        private static byte[] Crop(byte[] plane, int planeWidth, int width, int height)
        {
            if (planeWidth == width && plane.Length == width * height)
            {
                return plane;
            }

            var result = new byte[width * height];
            for (var y = 0; y < height; y++)
            {
                plane.AsSpan(y * planeWidth, width).CopyTo(result.AsSpan(y * width, width));
            }

            return result;
        }

        /// <summary>Stretches a subsampled plane to the image size with bilinear filtering, sample centers aligned as
        /// JFIF defines them.</summary>
        private readonly byte[] Upsample(byte[] plane, int planeWidth, int planeHeight, int h, int v)
        {
            int width = _width, height = _height;
            var result = new byte[width * height];
            // Only the part of the plane that holds image samples takes part; the rest is padding of the last blocks.
            var validWidth = Math.Min(planeWidth, ((width * h) + _maxH - 1) / _maxH);
            var validHeight = Math.Min(planeHeight, ((height * v) + _maxV - 1) / _maxV);
            var scaleX = (float)h / _maxH;
            var scaleY = (float)v / _maxV;
            var x0s = new int[width];
            var x1s = new int[width];
            var fxs = new int[width];
            for (var x = 0; x < width; x++)
            {
                var sx = Math.Clamp(((x + 0.5f) * scaleX) - 0.5f, 0f, validWidth - 1);
                var x0 = (int)sx;
                x0s[x] = x0;
                x1s[x] = Math.Min(x0 + 1, validWidth - 1);
                fxs[x] = (int)((sx - x0) * 256f);
            }

            for (var y = 0; y < height; y++)
            {
                var sy = Math.Clamp(((y + 0.5f) * scaleY) - 0.5f, 0f, validHeight - 1);
                var y0 = (int)sy;
                var y1 = Math.Min(y0 + 1, validHeight - 1);
                var fy = (int)((sy - y0) * 256f);
                var row0 = plane.AsSpan(y0 * planeWidth, planeWidth);
                var row1 = plane.AsSpan(y1 * planeWidth, planeWidth);
                var output = result.AsSpan(y * width, width);
                for (var x = 0; x < width; x++)
                {
                    int x0 = x0s[x], x1 = x1s[x], fx = fxs[x];
                    var top = (row0[x0] * (256 - fx)) + (row0[x1] * fx);
                    var bottom = (row1[x0] * (256 - fx)) + (row1[x1] * fx);
                    output[x] = (byte)(((top * (256 - fy)) + (bottom * fy) + 32768) >> 16);
                }
            }

            return result;
        }
    }
}
