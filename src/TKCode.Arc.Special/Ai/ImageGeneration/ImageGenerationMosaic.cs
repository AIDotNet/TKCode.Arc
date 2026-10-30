using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Media.Immutable;
using Avalonia.Media.Imaging;
using Avalonia.Platform;

namespace TKCode.Arc;

/// <summary>
/// The pixel mosaic of one image: five levels of colour blocks (4, 8, 16, 32 and 64 columns, rows to match the batch's
/// shape), each a true average of the image, built once by averaging the finest level from the pixels and halving it up.
/// Every level is also kept as a tiny bitmap, so a whole level draws as one scaled image with nearest-neighbour sampling.
/// The coarse level keeps a brush per block and the seeded centre-first order its blocks fade in.
/// </summary>
internal sealed class ImageGenerationMosaic
{
    /// <summary>How many levels: 4, 8, 16, 32 and 64 columns.</summary>
    public const int LevelCount = 5;

    /// <summary>The coarse level's columns; each level doubles it.</summary>
    public const int CoarseColumns = 4;

    /// <summary>
    /// Where each step starts on the progress line: the coarse blocks fade in, then 8, 16, 32 and 64 columns land, then the
    /// full image crossfades in and is whole at 1.
    /// </summary>
    internal static readonly double[] StageStarts = [0, 0.18, 0.34, 0.5, 0.66, 0.82, 1];

    private static readonly ConditionalWeakTable<Bitmap, PixelSource> s_sources = new();
    private static readonly ConditionalWeakTable<Bitmap, ImageGenerationMosaic> s_cache = new();

    private ImageGenerationMosaic(ImageGenerationMosaicLevel[] levels, PixelRect crop, double aspect, int[] coarseRank)
    {
        Levels = levels;
        Crop = crop;
        Aspect = aspect;
        CoarseRank = coarseRank;
    }

    /// <summary>The levels, coarse first.</summary>
    public ImageGenerationMosaicLevel[] Levels { get; }

    /// <summary>The part of the source the tile shows (the centre, cropped to the batch's shape), in pixels.</summary>
    public PixelRect Crop { get; }

    /// <summary>Height over width of the shape the mosaic was built for.</summary>
    public double Aspect { get; }

    /// <summary>For each coarse block, its place in the fade-in order (0 first): nearest the centre first, seeded jitter.</summary>
    public int[] CoarseRank { get; }

    /// <summary>The rows of the coarse level for a shape (height over width): at least 1, doubled with every level.</summary>
    public static int CoarseRows(double aspect) => Math.Max(1, (int)Math.Round(CoarseColumns * aspect));

    /// <summary>Remembers the pixels a bitmap was made from, so its mosaic does not have to read them back.</summary>
    public static void Register(Bitmap bitmap, byte[] bgra, int width, int height) =>
        s_sources.AddOrUpdate(bitmap, new PixelSource(bgra, width, height));

    /// <summary>The step a progress falls in (0: coarse blocks, 1–4: finer levels, 5: the full image) and how far into it.</summary>
    public static (int Stage, double Local) Phase(double progress)
    {
        progress = Math.Clamp(double.IsFinite(progress) ? progress : 0, 0, 1);
        for (var stage = 0; stage < StageStarts.Length - 1; stage++)
        {
            var end = StageStarts[stage + 1];
            if (progress < end || stage == StageStarts.Length - 2)
            {
                var start = StageStarts[stage];
                return (stage, Math.Clamp((progress - start) / (end - start), 0, 1));
            }
        }
        return (StageStarts.Length - 2, 1);
    }

    /// <summary>The mosaic of <paramref name="bitmap"/> for a shape, built once and cached; null when its pixels cannot be read.</summary>
    public static ImageGenerationMosaic? For(Bitmap bitmap, double aspect)
    {
        if (s_cache.TryGetValue(bitmap, out var cached) && Math.Abs(cached.Aspect - aspect) < 0.001)
        {
            return cached;
        }
        var source = s_sources.TryGetValue(bitmap, out var registered) ? registered : Read(bitmap);
        if (source is null)
        {
            return null;
        }
        var mosaic = Build(source.Bgra, source.Width, source.Height, aspect, seed: (uint)source.Width * 31u + (uint)source.Height);
        mosaic.CreateBitmaps();
        s_cache.AddOrUpdate(bitmap, mosaic);
        return mosaic;
    }

    /// <summary>
    /// Builds the levels from opaque BGRA pixels (pure; no bitmaps): the finest level averages the pixels of each block, and
    /// each coarser level averages 2 × 2 blocks of the one below it.
    /// </summary>
    public static ImageGenerationMosaic Build(byte[] bgra, int width, int height, double aspect, uint seed)
    {
        aspect = double.IsFinite(aspect) && aspect > 0 ? aspect : 1;
        // Crop the centre to the batch's shape (cover).
        var cropWidth = width;
        var cropHeight = (int)Math.Round(width * aspect);
        if (cropHeight > height)
        {
            cropHeight = height;
            cropWidth = Math.Max(1, (int)Math.Round(height / aspect));
        }
        cropHeight = Math.Max(1, cropHeight);
        var crop = new PixelRect((width - cropWidth) / 2, (height - cropHeight) / 2, cropWidth, cropHeight);

        var rows0 = CoarseRows(aspect);
        var levels = new ImageGenerationMosaicLevel[LevelCount];
        var finestCols = CoarseColumns << (LevelCount - 1);
        var finestRows = rows0 << (LevelCount - 1);
        var finest = new int[finestCols * finestRows];
        for (var row = 0; row < finestRows; row++)
        {
            var y0 = crop.Y + row * crop.Height / finestRows;
            var y1 = Math.Max(y0 + 1, crop.Y + (row + 1) * crop.Height / finestRows);
            for (var col = 0; col < finestCols; col++)
            {
                var x0 = crop.X + col * crop.Width / finestCols;
                var x1 = Math.Max(x0 + 1, crop.X + (col + 1) * crop.Width / finestCols);
                long b = 0, g = 0, r = 0, n = 0;
                for (var y = y0; y < y1 && y < height; y++)
                {
                    var i = (y * width + x0) * 4;
                    for (var x = x0; x < x1 && x < width; x++, i += 4)
                    {
                        b += bgra[i];
                        g += bgra[i + 1];
                        r += bgra[i + 2];
                        n++;
                    }
                }
                n = Math.Max(1, n);
                finest[row * finestCols + col] = Pack((int)(b / n), (int)(g / n), (int)(r / n));
            }
        }
        levels[LevelCount - 1] = new ImageGenerationMosaicLevel(finestCols, finestRows, finest);
        for (var level = LevelCount - 2; level >= 0; level--)
        {
            var below = levels[level + 1];
            var cols = below.Columns / 2;
            var rows = below.Rows / 2;
            var colors = new int[cols * rows];
            for (var row = 0; row < rows; row++)
            {
                for (var col = 0; col < cols; col++)
                {
                    int b = 0, g = 0, r = 0;
                    for (var k = 0; k < 4; k++)
                    {
                        var c = below.Colors[(row * 2 + k / 2) * below.Columns + col * 2 + k % 2];
                        b += c & 0xFF;
                        g += (c >> 8) & 0xFF;
                        r += (c >> 16) & 0xFF;
                    }
                    colors[row * cols + col] = Pack(b / 4, g / 4, r / 4);
                }
            }
            levels[level] = new ImageGenerationMosaicLevel(cols, rows, colors);
        }
        return new ImageGenerationMosaic(levels, crop, aspect, Rank(CoarseColumns, rows0, seed));
    }

    /// <summary>The coarse blocks' fade-in order: by distance from the centre, with a seeded nudge so it is not a perfect ring.</summary>
    internal static int[] Rank(int cols, int rows, uint seed)
    {
        var count = cols * rows;
        var keys = new double[count];
        var order = new int[count];
        for (var i = 0; i < count; i++)
        {
            var dx = (i % cols + 0.5) / cols - 0.5;
            var dy = (i / cols + 0.5) / rows - 0.5;
            keys[i] = Math.Sqrt(dx * dx + dy * dy) + Jitter(i, seed) * 0.35;
            order[i] = i;
        }
        Array.Sort(keys, order);
        var rank = new int[count];
        for (var i = 0; i < count; i++)
        {
            rank[order[i]] = i;
        }
        return rank;
    }

    private static double Jitter(int i, uint seed)
    {
        unchecked
        {
            var h = (uint)i * 2654435761u ^ seed * 40503u;
            h ^= h >> 15;
            h *= 2246822519u;
            h ^= h >> 13;
            return (h & 0xFFFF) / 65535.0;
        }
    }

    private static int Pack(int b, int g, int r) => unchecked((int)0xFF000000) | (r << 16) | (g << 8) | b;

    /// <summary>Makes each level's bitmap and the coarse level's brushes (on the UI thread).</summary>
    private void CreateBitmaps()
    {
        foreach (var level in Levels)
        {
            level.CreateBitmap();
        }
        var coarse = Levels[0];
        coarse.Brushes = new ImmutableSolidColorBrush[coarse.Colors.Length];
        for (var i = 0; i < coarse.Colors.Length; i++)
        {
            coarse.Brushes[i] = new ImmutableSolidColorBrush((uint)coarse.Colors[i]);
        }
    }

    /// <summary>Reads a bitmap's pixels (BGRA or RGBA, 8 bits); null when the platform cannot.</summary>
    private static PixelSource? Read(Bitmap bitmap)
    {
        try
        {
            var size = bitmap.PixelSize;
            var format = bitmap.Format;
            if (size.Width <= 0 || size.Height <= 0 || (format != PixelFormat.Bgra8888 && format != PixelFormat.Rgba8888))
            {
                return null;
            }
            var stride = size.Width * 4;
            var buffer = new byte[stride * size.Height];
            var handle = GCHandle.Alloc(buffer, GCHandleType.Pinned);
            try
            {
                bitmap.CopyPixels(new PixelRect(size), handle.AddrOfPinnedObject(), buffer.Length, stride);
            }
            finally
            {
                handle.Free();
            }
            if (format == PixelFormat.Rgba8888)
            {
                for (var i = 0; i < buffer.Length; i += 4)
                {
                    (buffer[i], buffer[i + 2]) = (buffer[i + 2], buffer[i]);
                }
            }
            return new PixelSource(buffer, size.Width, size.Height);
        }
        catch (Exception)
        {
            // No pixel access on this platform (or a format we do not read): the tile shows the quiet placeholder grid and
            // fades the image in when it arrives.
            return null;
        }
    }

    private sealed record PixelSource(byte[] Bgra, int Width, int Height);
}

/// <summary>One level of a mosaic: its blocks' colours (packed BGRA), a tiny bitmap of them and, on the coarse level, brushes.</summary>
internal sealed class ImageGenerationMosaicLevel
{
    public ImageGenerationMosaicLevel(int columns, int rows, int[] colors)
    {
        Columns = columns;
        Rows = rows;
        Colors = colors;
    }

    public int Columns { get; }

    public int Rows { get; }

    public int[] Colors { get; }

    public WriteableBitmap? Bitmap { get; private set; }

    public ImmutableSolidColorBrush[]? Brushes { get; set; }

    public void CreateBitmap()
    {
        try
        {
            var bitmap = new WriteableBitmap(new PixelSize(Columns, Rows), new Vector(96, 96), PixelFormat.Bgra8888, AlphaFormat.Opaque);
            using (var locked = bitmap.Lock())
            {
                for (var row = 0; row < Rows; row++)
                {
                    Marshal.Copy(Colors, row * Columns, locked.Address + row * locked.RowBytes, Columns);
                }
            }
            Bitmap = bitmap;
        }
        catch (Exception)
        {
            // No bitmaps on this platform: the level is drawn block by block from its colours instead.
            Bitmap = null;
        }
    }
}
