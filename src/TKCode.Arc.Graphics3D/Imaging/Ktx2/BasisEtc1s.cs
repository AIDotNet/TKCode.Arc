// BasisLZ / ETC1S decoder: global endpoint and selector codebooks, Huffman tables and image slices, decoded
// straight to RGBA8 the way the reference transcoder does for its RGBA32 target.
//
// Follows the BasisLZ bitstream specification in the KTX 2.0 specification
// (https://registry.khronos.org/KTX/specs/2.0/ktxspec.v2.html, "BasisLZ Bitstream Specification") and the
// reference transcoder of Basis Universal (transcoder/basisu_transcoder.cpp and basisu_transcoder_internal.h in
// https://github.com/BinomialLLC/basis_universal, Apache License 2.0, Copyright Binomial LLC).
// The bit reader and the Huffman tables are in BasisBitReader.cs.

using System.Buffers.Binary;

namespace TKCode.Arc.Graphics3D;

/// <summary>Decodes the ETC1S slices of one KTX2 file once its global data has been read.</summary>
internal sealed class BasisEtc1sDecoder
{
    private const int EndpointPredRepeatSymbol = 256;
    private const int EndpointPredMinRepeat = 3;
    private const int EndpointPredRepeatVlcBits = 4;
    private const int SelectorRleThreshold = 3;
    private const int SelectorRleLongRunSymbol = 63;
    private const int SelectorRleVlcBits = 7;
    private const int InitialBlockCapacity = 1 << 14;

    // ETC1 intensity modifiers, in the order the selectors index them.
    private static ReadOnlySpan<short> IntensityTables =>
    [
        -8, -2, 2, 8,
        -17, -5, 5, 17,
        -29, -9, 9, 29,
        -42, -13, 13, 42,
        -60, -18, 18, 60,
        -80, -24, 24, 80,
        -106, -33, 33, 106,
        -183, -47, 47, 183,
    ];

    private readonly int _endpointCount;
    private readonly int _selectorCount;

    // Four RGBA colors (alpha 255, red in the low byte) per endpoint: the block colors of its ETC1S block.
    private readonly uint[] _endpointColors;

    // One value per selector: byte y holds row y, two bits per texel from the left.
    private readonly uint[] _selectors;

    private readonly BasisHuffmanTable _endpointPredTable;
    private readonly BasisHuffmanTable _deltaEndpointTable;
    private readonly BasisHuffmanTable _selectorTable;
    private readonly BasisHuffmanTable _selectorRleTable;
    private readonly int _selectorHistorySize;

    /// <summary>Reads the codebooks and tables of the supercompression global data.</summary>
    public BasisEtc1sDecoder(int endpointCount, ReadOnlySpan<byte> endpointData, int selectorCount, ReadOnlySpan<byte> selectorData, ReadOnlySpan<byte> tableData)
    {
        if (endpointCount <= 0 || selectorCount <= 0)
        {
            throw new InvalidDataException("The ETC1S codebooks are empty.");
        }

        _endpointCount = endpointCount;
        _selectorCount = selectorCount;

        var reader = new BasisBitReader(tableData);
        _endpointPredTable = ReadRequiredTable(ref reader);
        _deltaEndpointTable = ReadRequiredTable(ref reader);
        _selectorTable = ReadRequiredTable(ref reader);
        _selectorRleTable = ReadRequiredTable(ref reader);
        _selectorHistorySize = (int)reader.ReadBits(13);
        if (_selectorHistorySize == 0)
        {
            throw new InvalidDataException("The ETC1S selector history size is zero.");
        }

        _endpointColors = ReadEndpoints(endpointCount, endpointData);
        _selectors = ReadSelectors(selectorCount, selectorData);
    }

    private static BasisHuffmanTable ReadRequiredTable(ref BasisBitReader reader)
    {
        BasisHuffmanTable table = BasisHuffmanTable.Read(ref reader);
        if (!table.IsValid)
        {
            throw new InvalidDataException("An ETC1S Huffman table is empty.");
        }

        return table;
    }

    private static uint[] ReadEndpoints(int count, ReadOnlySpan<byte> data)
    {
        var reader = new BasisBitReader(data);

        // The color deltas use one of three tables, chosen by the previous value of the component.
        BasisHuffmanTable deltaLow = ReadRequiredTable(ref reader);
        BasisHuffmanTable deltaMid = ReadRequiredTable(ref reader);
        BasisHuffmanTable deltaHigh = ReadRequiredTable(ref reader);
        BasisHuffmanTable intensityDelta = ReadRequiredTable(ref reader);
        bool grayscale = reader.ReadBits(1) != 0;
        int components = grayscale ? 1 : 3;

        var colors = new uint[count * 4];
        ReadOnlySpan<short> intensityTables = IntensityTables;
        Span<int> color = stackalloc int[3];
        color.Fill(16);
        int intensity = 0;

        for (int i = 0; i < count; i++)
        {
            intensity = (reader.DecodeHuffman(intensityDelta) + intensity) & 7;
            for (int c = 0; c < components; c++)
            {
                int previous = color[c];
                BasisHuffmanTable table = previous <= 9 ? deltaLow : previous <= 21 ? deltaMid : deltaHigh;
                color[c] = (previous + reader.DecodeHuffman(table)) & 31;
            }

            int r5 = color[0];
            int g5 = grayscale ? r5 : color[1];
            int b5 = grayscale ? r5 : color[2];
            int r = (r5 << 3) | (r5 >> 2);
            int g = (g5 << 3) | (g5 >> 2);
            int b = (b5 << 3) | (b5 >> 2);
            for (int s = 0; s < 4; s++)
            {
                int modifier = intensityTables[intensity * 4 + s];
                colors[i * 4 + s] = (uint)Math.Clamp(r + modifier, 0, 255)
                    | ((uint)Math.Clamp(g + modifier, 0, 255) << 8)
                    | ((uint)Math.Clamp(b + modifier, 0, 255) << 16)
                    | 0xFF000000u;
            }
        }

        return colors;
    }

    private static uint[] ReadSelectors(int count, ReadOnlySpan<byte> data)
    {
        var reader = new BasisBitReader(data);

        // The two flags select codebook kinds that only early .basis files used and KTX2 never allowed.
        if (reader.ReadBits(1) != 0 || reader.ReadBits(1) != 0)
        {
            throw new NotSupportedException("ETC1S global selector codebooks are not supported.");
        }

        var selectors = new uint[count];
        if (reader.ReadBits(1) != 0)
        {
            for (int i = 0; i < count; i++)
            {
                uint rows = 0;
                for (int y = 0; y < 4; y++)
                {
                    rows |= reader.ReadBits(8) << (8 * y);
                }

                selectors[i] = rows;
            }

            return selectors;
        }

        BasisHuffmanTable deltaTable = BasisHuffmanTable.Read(ref reader);
        if (count > 1 && !deltaTable.IsValid)
        {
            throw new InvalidDataException("The ETC1S selector delta table is empty.");
        }

        // The first selector is stored as is, every other one as the XOR with its predecessor, row by row.
        uint previous = 0;
        for (int y = 0; y < 4; y++)
        {
            previous |= reader.ReadBits(8) << (8 * y);
        }

        selectors[0] = previous;
        for (int i = 1; i < count; i++)
        {
            uint delta = 0;
            for (int y = 0; y < 4; y++)
            {
                delta |= ((uint)reader.DecodeHuffman(deltaTable) & 0xFF) << (8 * y);
            }

            previous ^= delta;
            selectors[i] = previous;
        }

        return selectors;
    }

    /// <summary>
    /// Decodes one slice to an endpoint index (low 16 bits) and a selector index (high 16 bits) per block, in
    /// raster order. Nothing the size of the image is allocated before the stream has proved to hold it.
    /// In a <paramref name="video"/> file predictor 2 means "same as the previous frame", which for the first
    /// frame is endpoint 0 and selector 0.
    /// </summary>
    public uint[] DecodeSlice(ReadOnlySpan<byte> data, int blocksX, int blocksY, bool video)
    {
        int total = blocksX * blocksY;
        var blocks = new uint[Math.Min(total, InitialBlockCapacity)];
        var reader = new BasisBitReader(data);

        int endpointCount = _endpointCount;
        int selectorCount = _selectorCount;
        int historySize = _selectorHistorySize;
        int selectorRleSymbol = selectorCount + historySize;

        // Recently used selectors: new ones enter the second half, a reused one moves halfway to the front.
        var history = new int[historySize];
        int historyNext = historySize / 2;

        var previousRow = new int[blocksX];
        var currentRow = new int[blocksX];
        var oddRowPredictors = new byte[blocksX];

        long predictorRepeat = 0;
        int previousPredictors = 0;
        int predictors = 0;
        int previousEndpoint = 0;
        int selectorRun = 0;

        for (int y = 0; y < blocksY; y++)
        {
            int rowStart = y * blocksX;
            if (rowStart + blocksX > blocks.Length)
            {
                Array.Resize(ref blocks, (int)Math.Min(total, Math.Max((long)blocks.Length * 2, rowStart + blocksX)));
            }

            for (int x = 0; x < blocksX; x++)
            {
                // One symbol holds the predictors of a 2x2 group of blocks, two bits each.
                if ((x & 1) == 0)
                {
                    if ((y & 1) == 0)
                    {
                        if (predictorRepeat != 0)
                        {
                            predictorRepeat--;
                            predictors = previousPredictors;
                        }
                        else
                        {
                            predictors = reader.DecodeHuffman(_endpointPredTable);
                            if (predictors == EndpointPredRepeatSymbol)
                            {
                                predictorRepeat = (long)reader.ReadVlc(EndpointPredRepeatVlcBits) + EndpointPredMinRepeat - 1;
                                predictors = previousPredictors;
                            }
                            else
                            {
                                previousPredictors = predictors;
                            }
                        }

                        oddRowPredictors[x] = (byte)((predictors >> 4) & 0xFF);
                    }
                    else
                    {
                        predictors = oddRowPredictors[x];
                    }
                }

                int predictor = predictors & 3;
                predictors >>= 2;

                int endpoint;
                int selector = 0;
                bool hasSelector = true;
                switch (predictor)
                {
                    case 0:
                        if (x == 0)
                        {
                            throw InvalidSlice();
                        }

                        endpoint = previousEndpoint;
                        break;

                    case 1:
                        if (y == 0)
                        {
                            throw InvalidSlice();
                        }

                        endpoint = previousRow[x];
                        break;

                    case 2:
                        if (video)
                        {
                            endpoint = 0;
                            hasSelector = false;
                        }
                        else
                        {
                            if (x == 0 || y == 0)
                            {
                                throw InvalidSlice();
                            }

                            endpoint = previousRow[x - 1];
                        }

                        break;

                    default:
                        endpoint = reader.DecodeHuffman(_deltaEndpointTable) + previousEndpoint;
                        if (endpoint >= endpointCount)
                        {
                            endpoint -= endpointCount;
                        }

                        break;
                }

                if (endpoint >= endpointCount)
                {
                    throw InvalidSlice();
                }

                currentRow[x] = endpoint;
                previousEndpoint = endpoint;

                if (hasSelector)
                {
                    int symbol;
                    if (selectorRun > 0)
                    {
                        selectorRun--;
                        symbol = selectorCount;
                    }
                    else
                    {
                        symbol = reader.DecodeHuffman(_selectorTable);
                        if (symbol == selectorRleSymbol)
                        {
                            // A run of the most recent selector in the history.
                            int run = reader.DecodeHuffman(_selectorRleTable);
                            long length = run == SelectorRleLongRunSymbol
                                ? (long)reader.ReadVlc(SelectorRleVlcBits) + SelectorRleThreshold
                                : run + SelectorRleThreshold;
                            if (length > total)
                            {
                                throw InvalidSlice();
                            }

                            selectorRun = (int)length - 1;
                            symbol = selectorCount;
                        }
                    }

                    if (symbol >= selectorCount)
                    {
                        int index = symbol - selectorCount;
                        if (index >= historySize)
                        {
                            throw InvalidSlice();
                        }

                        selector = history[index];
                        if (index != 0)
                        {
                            (history[index / 2], history[index]) = (history[index], history[index / 2]);
                        }
                    }
                    else
                    {
                        selector = symbol;
                        history[historyNext++] = symbol;
                        if (historyNext == historySize)
                        {
                            historyNext = historySize / 2;
                        }
                    }
                }

                blocks[rowStart + x] = (uint)endpoint | ((uint)selector << 16);
            }

            (previousRow, currentRow) = (currentRow, previousRow);
        }

        if (predictorRepeat != 0)
        {
            throw InvalidSlice();
        }

        return blocks;
    }

    private static InvalidDataException InvalidSlice()
    {
        return new InvalidDataException("An ETC1S slice is corrupt.");
    }

    /// <summary>
    /// Expands decoded slices to RGBA8. With <paramref name="alphaBlocks"/> the alpha comes from the green
    /// channel of the alpha slice's block colors; without it alpha is 255.
    /// </summary>
    public void WritePixels(uint[] colorBlocks, uint[]? alphaBlocks, int width, int height, Span<byte> rgba)
    {
        int blocksX = (width + 3) >> 2;
        int blocksY = (height + 3) >> 2;
        uint[] colors = _endpointColors;
        uint[] selectors = _selectors;
        Span<uint> blockColors = stackalloc uint[4];

        for (int by = 0; by < blocksY; by++)
        {
            int rows = Math.Min(4, height - by * 4);
            for (int bx = 0; bx < blocksX; bx++)
            {
                int columns = Math.Min(4, width - bx * 4);
                uint block = colorBlocks[by * blocksX + bx];
                colors.AsSpan((int)(block & 0xFFFF) * 4, 4).CopyTo(blockColors);
                uint selector = selectors[block >> 16];

                if (alphaBlocks is null)
                {
                    for (int y = 0; y < rows; y++)
                    {
                        Span<byte> row = rgba.Slice(((by * 4 + y) * width + bx * 4) * 4, columns * 4);
                        uint bits = selector >> (8 * y);
                        for (int x = 0; x < columns; x++)
                        {
                            BinaryPrimitives.WriteUInt32LittleEndian(row.Slice(x * 4), blockColors[(int)((bits >> (2 * x)) & 3)]);
                        }
                    }

                    continue;
                }

                uint alphaBlock = alphaBlocks[by * blocksX + bx];
                int alphaColors = (int)(alphaBlock & 0xFFFF) * 4;
                uint alphaSelector = selectors[alphaBlock >> 16];
                for (int y = 0; y < rows; y++)
                {
                    Span<byte> row = rgba.Slice(((by * 4 + y) * width + bx * 4) * 4, columns * 4);
                    uint bits = selector >> (8 * y);
                    uint alphaBits = alphaSelector >> (8 * y);
                    for (int x = 0; x < columns; x++)
                    {
                        uint color = blockColors[(int)((bits >> (2 * x)) & 3)] & 0x00FFFFFFu;
                        uint alpha = (colors[alphaColors + (int)((alphaBits >> (2 * x)) & 3)] >> 8) & 0xFF;
                        BinaryPrimitives.WriteUInt32LittleEndian(row.Slice(x * 4), color | (alpha << 24));
                    }
                }
            }
        }
    }
}
