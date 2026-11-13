using System.Text;

namespace TKCode.Arc;

/// <summary>Error correction level of a <see cref="TwoFactorSetupQrEncoder"/> symbol (the share of codewords that can be restored).</summary>
internal enum TwoFactorSetupQrLevel
{
    /// <summary>About 7% of the codewords can be restored.</summary>
    Low,

    /// <summary>About 15% (the default; what authenticator setup screens usually use).</summary>
    Medium,

    /// <summary>About 25%.</summary>
    Quartile,

    /// <summary>About 30%.</summary>
    High
}

/// <summary>A finished QR symbol: a square of dark and light modules, without the quiet zone.</summary>
internal sealed class TwoFactorSetupQrMatrix
{
    private readonly bool[] _modules;

    internal TwoFactorSetupQrMatrix(int version, int size, bool[] modules, int mask)
    {
        Version = version;
        Size = size;
        Mask = mask;
        _modules = modules;
    }

    /// <summary>The symbol version (1 to 10); the side is 17 + 4 × version modules.</summary>
    public int Version { get; }

    /// <summary>Modules per side.</summary>
    public int Size { get; }

    /// <summary>The data mask (0 to 7) that was chosen.</summary>
    public int Mask { get; }

    /// <summary>True for a dark module at column <paramref name="x"/>, row <paramref name="y"/>.</summary>
    public bool this[int x, int y] => _modules[y * Size + x];

    /// <summary>The symbol as text, one line per row, '#' for dark and '.' for light (tests and debugging).</summary>
    public override string ToString()
    {
        var text = new StringBuilder(Size * (Size + 1));
        for (var y = 0; y < Size; y++)
        {
            for (var x = 0; x < Size; x++)
            {
                text.Append(this[x, y] ? '#' : '.');
            }
            text.Append('\n');
        }
        return text.ToString();
    }
}

/// <summary>
/// A small QR Code Model 2 encoder written for <see cref="TwoFactorSetup"/>: byte mode (UTF-8), versions 1 to 10 (up to
/// 271 bytes at level Low, 213 at Medium), any error correction level, Reed–Solomon over GF(256) and the eight masks
/// scored with the four penalty rules of the standard. Enough for an <c>otpauth://</c> address, and nothing else.
/// </summary>
internal static class TwoFactorSetupQrEncoder
{
    public const int MaxVersion = 10;

    // Error correction codewords per block and number of blocks, versions 1..10, levels L, M, Q, H (from the standard's
    // capacity table).
    private static readonly int[][] s_eccPerBlock =
    [
        [7, 10, 15, 20, 26, 18, 20, 24, 30, 18],
        [10, 16, 26, 18, 24, 16, 18, 22, 22, 26],
        [13, 22, 18, 26, 18, 24, 18, 22, 20, 24],
        [17, 28, 22, 16, 22, 28, 26, 26, 24, 28]
    ];

    private static readonly int[][] s_blocks =
    [
        [1, 1, 1, 1, 1, 2, 2, 2, 2, 4],
        [1, 1, 1, 2, 2, 4, 4, 4, 5, 5],
        [1, 1, 2, 2, 4, 4, 6, 6, 8, 8],
        [1, 1, 2, 4, 4, 4, 5, 6, 8, 8]
    ];

    // The two bits a level writes into the format information.
    private static readonly int[] s_levelBits = [1, 0, 3, 2];

    private static readonly byte[] s_exp = new byte[512];
    private static readonly byte[] s_log = new byte[256];

    static TwoFactorSetupQrEncoder()
    {
        var value = 1;
        for (var i = 0; i < 255; i++)
        {
            s_exp[i] = (byte)value;
            s_log[value] = (byte)i;
            value <<= 1;
            if (value > 255)
            {
                value ^= 0x11D;
            }
        }
        for (var i = 255; i < 512; i++)
        {
            s_exp[i] = s_exp[i - 255];
        }
    }

    /// <summary>Encodes <paramref name="text"/> in the smallest version that holds it; null when it is too long for version 10.</summary>
    /// <param name="text">The text, encoded as UTF-8 bytes.</param>
    /// <param name="level">The error correction level.</param>
    /// <param name="mask">A fixed mask (0 to 7), or -1 to choose the one with the lowest penalty.</param>
    public static TwoFactorSetupQrMatrix? Encode(string text, TwoFactorSetupQrLevel level = TwoFactorSetupQrLevel.Medium, int mask = -1)
    {
        var data = Encoding.UTF8.GetBytes(text);
        for (var version = 1; version <= MaxVersion; version++)
        {
            var capacityBits = DataCodewords(version, level) * 8;
            var countBits = version <= 9 ? 8 : 16;
            if (data.Length < 1 << countBits && 4 + countBits + data.Length * 8 <= capacityBits)
            {
                return Build(data, version, level, mask);
            }
        }
        return null;
    }

    /// <summary>The number of data codewords (bytes) a version holds at a level.</summary>
    internal static int DataCodewords(int version, TwoFactorSetupQrLevel level)
    {
        var l = (int)level;
        return RawCodewords(version) - s_eccPerBlock[l][version - 1] * s_blocks[l][version - 1];
    }

    /// <summary>Every codeword a version holds (data and error correction): the modules left after the function patterns, over 8.</summary>
    internal static int RawCodewords(int version)
    {
        var modules = (16 * version + 128) * version + 64;
        if (version >= 2)
        {
            var align = version / 7 + 2;
            modules -= (25 * align - 10) * align - 55;
            if (version >= 7)
            {
                modules -= 36;
            }
        }
        return modules / 8;
    }

    private static TwoFactorSetupQrMatrix Build(byte[] data, int version, TwoFactorSetupQrLevel level, int fixedMask)
    {
        var codewords = Interleave(DataBits(data, version, level), version, level);
        var size = version * 4 + 17;
        var grid = new Grid(size);
        DrawFunctionPatterns(grid, version);
        PlaceCodewords(grid, codewords);

        var best = fixedMask;
        if (best < 0)
        {
            var lowest = int.MaxValue;
            for (var mask = 0; mask < 8; mask++)
            {
                ApplyMask(grid, mask);
                DrawFormat(grid, level, mask);
                var penalty = Penalty(grid);
                if (penalty < lowest)
                {
                    lowest = penalty;
                    best = mask;
                }
                // XOR again to undo the mask.
                ApplyMask(grid, mask);
            }
        }
        ApplyMask(grid, best);
        DrawFormat(grid, level, best);
        return new TwoFactorSetupQrMatrix(version, size, grid.Dark, best);
    }

    /// <summary>Mode, character count, the bytes, a terminator and the alternating pad bytes.</summary>
    private static byte[] DataBits(byte[] data, int version, TwoFactorSetupQrLevel level)
    {
        var capacity = DataCodewords(version, level);
        var bytes = new byte[capacity];
        var bit = 0;

        void Append(int value, int count)
        {
            for (var i = count - 1; i >= 0; i--)
            {
                if (((value >> i) & 1) != 0)
                {
                    bytes[bit >> 3] |= (byte)(0x80 >> (bit & 7));
                }
                bit++;
            }
        }

        Append(0b0100, 4);
        Append(data.Length, version <= 9 ? 8 : 16);
        foreach (var b in data)
        {
            Append(b, 8);
        }
        Append(0, Math.Min(4, capacity * 8 - bit));
        bit = (bit + 7) & ~7;
        for (var pad = 0xEC; bit < capacity * 8; pad ^= 0xEC ^ 0x11)
        {
            Append(pad, 8);
        }
        return bytes;
    }

    /// <summary>Splits the data into blocks, adds each block's error correction and interleaves them column by column.</summary>
    private static byte[] Interleave(byte[] data, int version, TwoFactorSetupQrLevel level)
    {
        var l = (int)level;
        var blockCount = s_blocks[l][version - 1];
        var eccLength = s_eccPerBlock[l][version - 1];
        var raw = RawCodewords(version);
        var shortBlocks = blockCount - raw % blockCount;
        var shortLength = raw / blockCount;
        var generator = Generator(eccLength);

        var blocks = new byte[blockCount][];
        var offset = 0;
        for (var i = 0; i < blockCount; i++)
        {
            var dataLength = shortLength - eccLength + (i < shortBlocks ? 0 : 1);
            var block = new byte[shortLength + 1];
            Array.Copy(data, offset, block, 0, dataLength);
            offset += dataLength;
            var ecc = Remainder(block.AsSpan(0, dataLength), generator);
            // Short blocks leave one gap before their error correction, so every block lines up column by column.
            ecc.CopyTo(block.AsSpan(shortLength + 1 - eccLength));
            blocks[i] = block;
        }

        var result = new byte[raw];
        var k = 0;
        for (var column = 0; column < shortLength + 1; column++)
        {
            for (var i = 0; i < blockCount; i++)
            {
                if (column != shortLength - eccLength || i >= shortBlocks)
                {
                    result[k++] = blocks[i][column];
                }
            }
        }
        return result;
    }

    private static byte Multiply(byte a, byte b) => a == 0 || b == 0 ? (byte)0 : s_exp[s_log[a] + s_log[b]];

    /// <summary>The generator polynomial (x − α⁰)(x − α¹)…(x − αⁿ⁻¹), highest power first, its leading 1 left out.</summary>
    private static byte[] Generator(int degree)
    {
        var result = new byte[degree];
        result[degree - 1] = 1;
        byte root = 1;
        for (var i = 0; i < degree; i++)
        {
            for (var j = 0; j < degree; j++)
            {
                result[j] = Multiply(result[j], root);
                if (j + 1 < degree)
                {
                    result[j] ^= result[j + 1];
                }
            }
            root = Multiply(root, 2);
        }
        return result;
    }

    /// <summary>The remainder of the data polynomial divided by the generator: the error correction codewords.</summary>
    private static byte[] Remainder(ReadOnlySpan<byte> data, byte[] generator)
    {
        var result = new byte[generator.Length];
        foreach (var b in data)
        {
            var factor = (byte)(b ^ result[0]);
            Array.Copy(result, 1, result, 0, result.Length - 1);
            result[^1] = 0;
            for (var i = 0; i < result.Length; i++)
            {
                result[i] ^= Multiply(generator[i], factor);
            }
        }
        return result;
    }

    private sealed class Grid
    {
        public Grid(int size)
        {
            Size = size;
            Dark = new bool[size * size];
            Function = new bool[size * size];
        }

        public int Size { get; }
        public bool[] Dark { get; }
        public bool[] Function { get; }

        public bool this[int x, int y] => Dark[y * Size + x];

        public void Set(int x, int y, bool dark)
        {
            Dark[y * Size + x] = dark;
            Function[y * Size + x] = true;
        }
    }

    private static void DrawFunctionPatterns(Grid grid, int version)
    {
        var size = grid.Size;
        for (var i = 0; i < size; i++)
        {
            grid.Set(6, i, i % 2 == 0);
            grid.Set(i, 6, i % 2 == 0);
        }
        DrawFinder(grid, 3, 3);
        DrawFinder(grid, size - 4, 3);
        DrawFinder(grid, 3, size - 4);

        var positions = AlignmentPositions(version);
        var last = positions.Length - 1;
        for (var i = 0; i <= last; i++)
        {
            for (var j = 0; j <= last; j++)
            {
                // Not over the three finder corners.
                if ((i == 0 && j == 0) || (i == 0 && j == last) || (i == last && j == 0))
                {
                    continue;
                }
                for (var dy = -2; dy <= 2; dy++)
                {
                    for (var dx = -2; dx <= 2; dx++)
                    {
                        grid.Set(positions[i] + dx, positions[j] + dy, Math.Max(Math.Abs(dx), Math.Abs(dy)) != 1);
                    }
                }
            }
        }

        // Reserve the format areas now (the real bits come with the mask) and draw the version blocks.
        DrawFormat(grid, TwoFactorSetupQrLevel.Medium, 0);
        if (version >= 7)
        {
            var remainder = version;
            for (var i = 0; i < 12; i++)
            {
                remainder = (remainder << 1) ^ ((remainder >> 11) * 0x1F25);
            }
            var bits = (version << 12) | remainder;
            for (var i = 0; i < 18; i++)
            {
                var dark = ((bits >> i) & 1) != 0;
                var a = size - 11 + i % 3;
                var b = i / 3;
                grid.Set(a, b, dark);
                grid.Set(b, a, dark);
            }
        }
    }

    /// <summary>A 7×7 finder pattern centred on (<paramref name="cx"/>, <paramref name="cy"/>) with its light separator.</summary>
    private static void DrawFinder(Grid grid, int cx, int cy)
    {
        for (var dy = -4; dy <= 4; dy++)
        {
            for (var dx = -4; dx <= 4; dx++)
            {
                var x = cx + dx;
                var y = cy + dy;
                if (x < 0 || y < 0 || x >= grid.Size || y >= grid.Size)
                {
                    continue;
                }
                var ring = Math.Max(Math.Abs(dx), Math.Abs(dy));
                grid.Set(x, y, ring != 2 && ring != 4);
            }
        }
    }

    /// <summary>The centre coordinates of the alignment patterns (version 1 has none).</summary>
    internal static int[] AlignmentPositions(int version)
    {
        if (version == 1)
        {
            return [];
        }
        var count = version / 7 + 2;
        var step = (version * 4 + 4 + count * 2 - 3) / (count * 2 - 2) * 2;
        var result = new int[count];
        result[0] = 6;
        for (int i = count - 1, position = version * 4 + 10; i >= 1; i--, position -= step)
        {
            result[i] = position;
        }
        return result;
    }

    /// <summary>The 15 format bits (level and mask, BCH-protected and XOR-masked), twice, and the dark module.</summary>
    private static void DrawFormat(Grid grid, TwoFactorSetupQrLevel level, int mask)
    {
        var data = (s_levelBits[(int)level] << 3) | mask;
        var remainder = data;
        for (var i = 0; i < 10; i++)
        {
            remainder = (remainder << 1) ^ ((remainder >> 9) * 0x537);
        }
        var bits = ((data << 10) | remainder) ^ 0x5412;
        bool Bit(int i) => ((bits >> i) & 1) != 0;

        var size = grid.Size;
        for (var i = 0; i <= 5; i++)
        {
            grid.Set(8, i, Bit(i));
        }
        grid.Set(8, 7, Bit(6));
        grid.Set(8, 8, Bit(7));
        grid.Set(7, 8, Bit(8));
        for (var i = 9; i < 15; i++)
        {
            grid.Set(14 - i, 8, Bit(i));
        }
        for (var i = 0; i < 8; i++)
        {
            grid.Set(size - 1 - i, 8, Bit(i));
        }
        for (var i = 8; i < 15; i++)
        {
            grid.Set(8, size - 15 + i, Bit(i));
        }
        grid.Set(8, size - 8, true);
    }

    /// <summary>Writes the codewords in the two-column zigzag from the bottom right, skipping the vertical timing column.</summary>
    private static void PlaceCodewords(Grid grid, byte[] codewords)
    {
        var size = grid.Size;
        var bit = 0;
        var total = codewords.Length * 8;
        for (var right = size - 1; right >= 1; right -= 2)
        {
            if (right == 6)
            {
                right = 5;
            }
            for (var step = 0; step < size; step++)
            {
                for (var j = 0; j < 2; j++)
                {
                    var x = right - j;
                    var upward = ((right + 1) & 2) == 0;
                    var y = upward ? size - 1 - step : step;
                    var index = y * size + x;
                    if (!grid.Function[index] && bit < total)
                    {
                        grid.Dark[index] = ((codewords[bit >> 3] >> (7 - (bit & 7))) & 1) != 0;
                        bit++;
                    }
                }
            }
        }
    }

    private static bool MaskBit(int mask, int x, int y) => mask switch
    {
        0 => (x + y) % 2 == 0,
        1 => y % 2 == 0,
        2 => x % 3 == 0,
        3 => (x + y) % 3 == 0,
        4 => (x / 3 + y / 2) % 2 == 0,
        5 => x * y % 2 + x * y % 3 == 0,
        6 => (x * y % 2 + x * y % 3) % 2 == 0,
        _ => ((x + y) % 2 + x * y % 3) % 2 == 0
    };

    private static void ApplyMask(Grid grid, int mask)
    {
        var size = grid.Size;
        for (var y = 0; y < size; y++)
        {
            for (var x = 0; x < size; x++)
            {
                var index = y * size + x;
                if (!grid.Function[index] && MaskBit(mask, x, y))
                {
                    grid.Dark[index] = !grid.Dark[index];
                }
            }
        }
    }

    /// <summary>The standard's four penalty rules: long runs, 2×2 blocks, finder look-alikes and the dark share.</summary>
    private static int Penalty(Grid grid)
    {
        var size = grid.Size;
        var penalty = 0;
        for (var pass = 0; pass < 2; pass++)
        {
            for (var a = 0; a < size; a++)
            {
                var run = 1;
                for (var b = 1; b <= size; b++)
                {
                    var same = b < size && At(grid, pass, a, b) == At(grid, pass, a, b - 1);
                    if (same)
                    {
                        run++;
                        continue;
                    }
                    if (run >= 5)
                    {
                        penalty += run - 2;
                    }
                    run = 1;
                }
                for (var b = 0; b + 7 <= size; b++)
                {
                    if (At(grid, pass, a, b) && !At(grid, pass, a, b + 1) && At(grid, pass, a, b + 2) && At(grid, pass, a, b + 3)
                        && At(grid, pass, a, b + 4) && !At(grid, pass, a, b + 5) && At(grid, pass, a, b + 6)
                        && (Light(grid, pass, a, b - 4, b - 1) || Light(grid, pass, a, b + 7, b + 10)))
                    {
                        penalty += 40;
                    }
                }
            }
        }
        var dark = 0;
        for (var y = 0; y < size; y++)
        {
            for (var x = 0; x < size; x++)
            {
                if (grid[x, y])
                {
                    dark++;
                }
                if (x + 1 < size && y + 1 < size && grid[x, y] == grid[x + 1, y] && grid[x, y] == grid[x, y + 1] && grid[x, y] == grid[x + 1, y + 1])
                {
                    penalty += 3;
                }
            }
        }
        var total = size * size;
        var deviation = Math.Abs(dark * 20 - total * 10);
        penalty += (deviation + total - 1) / total - 1 > 0 ? ((deviation + total - 1) / total - 1) * 10 : 0;
        return penalty;
    }

    /// <summary>Pass 0 reads rows (a = row), pass 1 columns (a = column).</summary>
    private static bool At(Grid grid, int pass, int a, int b) => pass == 0 ? grid[b, a] : grid[a, b];

    /// <summary>True when every module from <paramref name="from"/> to <paramref name="to"/> is light; outside the symbol counts as light.</summary>
    private static bool Light(Grid grid, int pass, int a, int from, int to)
    {
        for (var b = from; b <= to; b++)
        {
            if (b >= 0 && b < grid.Size && At(grid, pass, a, b))
            {
                return false;
            }
        }
        return true;
    }
}
