using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Imaging;

namespace TKCode.Arc.Primitives;

/// <summary>A decoded picture: the bitmap and the size it was before it was scaled down for its slot.</summary>
internal sealed class ArcImageFrame
{
    public ArcImageFrame(Bitmap bitmap, Size naturalSize, PixelSize naturalPixels, bool isReduced)
    {
        Bitmap = bitmap;
        NaturalSize = naturalSize;
        NaturalPixels = naturalPixels;
        IsReduced = isReduced;
    }

    public Bitmap Bitmap { get; }

    /// <summary>The picture's own size in device-independent pixels (layout uses it, so a reduced picture lays out the same).</summary>
    public Size NaturalSize { get; }

    /// <summary>The picture's own size in pixels.</summary>
    public PixelSize NaturalPixels { get; }

    /// <summary>True when <see cref="Bitmap"/> holds fewer pixels than the original.</summary>
    public bool IsReduced { get; }

    public long Bytes => (long)Bitmap.PixelSize.Width * Bitmap.PixelSize.Height * 4;

    /// <summary>Whether this frame has enough pixels for a slot of <paramref name="slotPixels"/> drawn with <paramref name="stretch"/>.</summary>
    public bool Covers(Size slotPixels, Stretch stretch)
    {
        if (!IsReduced)
        {
            return true;
        }
        var needed = ArcImageDecoder.TargetPixels(NaturalPixels, slotPixels, stretch);
        return needed is null || (Bitmap.PixelSize.Width >= needed.Value.Width - 1 && Bitmap.PixelSize.Height >= needed.Value.Height - 1);
    }
}

/// <summary>
/// The decoded pictures <see cref="ArcImage"/> shares, by <see cref="ImageSource.CacheKey"/>: a picture that is cached shows
/// at once (no loading state), and two images that ask for the same address while it downloads share one request. The
/// least recently used pictures are dropped once the cache holds more than <see cref="MaxBytes"/>.
/// Used on the UI thread.
/// </summary>
public static class ArcImageCache
{
    private sealed class Pending
    {
        public required Task<ArcImageFrame> Task { get; init; }
        public required CancellationTokenSource Cancellation { get; init; }
        public int Waiters;
    }

    private static readonly Dictionary<string, LinkedListNode<(string Key, ArcImageFrame Frame)>> s_entries = new(StringComparer.Ordinal);
    private static readonly LinkedList<(string Key, ArcImageFrame Frame)> s_order = new();
    private static readonly Dictionary<string, Pending> s_pending = new(StringComparer.Ordinal);
    private static long s_bytes;
    private static long s_maxBytes = 64L * 1024 * 1024;

    /// <summary>How many bytes of decoded pixels the cache keeps (default 64 MB); 0 turns caching off.</summary>
    public static long MaxBytes
    {
        get => s_maxBytes;
        set
        {
            s_maxBytes = Math.Max(0, value);
            Trim();
        }
    }

    /// <summary>The number of cached pictures.</summary>
    public static int Count => s_entries.Count;

    /// <summary>Bytes of decoded pixels the cache holds now.</summary>
    public static long Bytes => s_bytes;

    /// <summary>Whether the picture of <paramref name="source"/> is cached.</summary>
    public static bool Contains(ImageSource source) => source.CacheKey is { } key && s_entries.ContainsKey(key);

    /// <summary>Drops the picture of <paramref name="source"/> (images that show it keep it until their source changes).</summary>
    public static void Remove(ImageSource source)
    {
        if (source.CacheKey is { } key && s_entries.Remove(key, out var node))
        {
            s_order.Remove(node);
            s_bytes -= node.Value.Frame.Bytes;
        }
    }

    /// <summary>Drops every cached picture.</summary>
    public static void Clear()
    {
        s_entries.Clear();
        s_order.Clear();
        s_bytes = 0;
    }

    internal static bool TryGet(ImageSource source, out ArcImageFrame frame)
    {
        if (source.CacheKey is { } key && s_entries.TryGetValue(key, out var node))
        {
            s_order.Remove(node);
            s_order.AddFirst(node);
            frame = node.Value.Frame;
            return true;
        }
        frame = null!;
        return false;
    }

    /// <summary>
    /// Loads and decodes <paramref name="source"/> for a slot of <paramref name="slotPixels"/>, joining a download of the same
    /// address that is already running. Cancelling <paramref name="cancellationToken"/> stops waiting; the download itself
    /// is cancelled once nobody waits for it.
    /// </summary>
    internal static async Task<ArcImageFrame> LoadAsync(ImageSource source, IArcImageLoader loader, Size? slotPixels, Stretch stretch,
                                                       CancellationToken cancellationToken)
    {
        var key = source.CacheKey;
        if (key is null || s_maxBytes == 0)
        {
            return await ArcImageDecoder.LoadAsync(source, loader, slotPixels, stretch, cancellationToken);
        }
        if (!s_pending.TryGetValue(key, out var pending))
        {
            var cancellation = new CancellationTokenSource();
            pending = new Pending
            {
                Task = ArcImageDecoder.LoadAsync(source, loader, slotPixels, stretch, cancellation.Token),
                Cancellation = cancellation
            };
            s_pending[key] = pending;
            _ = Settle(key, pending);
        }
        pending.Waiters++;
        try
        {
            return await pending.Task.WaitAsync(cancellationToken);
        }
        finally
        {
            pending.Waiters--;
            if (pending.Waiters == 0 && !pending.Task.IsCompleted)
            {
                pending.Cancellation.Cancel();
                if (s_pending.TryGetValue(key, out var current) && ReferenceEquals(current, pending))
                {
                    s_pending.Remove(key);
                }
            }
        }
    }

    private static async Task Settle(string key, Pending pending)
    {
        try
        {
            var frame = await pending.Task;
            Store(key, frame);
        }
        catch (Exception)
        {
            // Failures are not cached: the next request for the address tries again.
        }
        finally
        {
            if (s_pending.TryGetValue(key, out var current) && ReferenceEquals(current, pending))
            {
                s_pending.Remove(key);
            }
        }
    }

    private static void Store(string key, ArcImageFrame frame)
    {
        if (s_entries.Remove(key, out var old))
        {
            s_order.Remove(old);
            s_bytes -= old.Value.Frame.Bytes;
        }
        if (frame.Bytes > s_maxBytes)
        {
            return;
        }
        s_entries[key] = s_order.AddFirst((key, frame));
        s_bytes += frame.Bytes;
        Trim();
    }

    private static void Trim()
    {
        while (s_bytes > s_maxBytes && s_order.Last is { } last)
        {
            s_order.RemoveLast();
            s_entries.Remove(last.Value.Key);
            s_bytes -= last.Value.Frame.Bytes;
        }
    }
}

/// <summary>Reads a picture through a loader and decodes it, scaled down when it is much larger than its slot.</summary>
internal static class ArcImageDecoder
{
    /// <summary>A picture is scaled down only when it is more than this much larger than its slot needs.</summary>
    private const double ReduceThreshold = 1.5;

    public static async Task<ArcImageFrame> LoadAsync(ImageSource source, IArcImageLoader loader, Size? slotPixels, Stretch stretch,
                                                      CancellationToken cancellationToken)
    {
        var stream = await loader.OpenAsync(source, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        // Decoding is CPU work: off the UI thread on the desktop (WebAssembly without threads runs it inline).
        return await Task.Run(() =>
        {
            using (stream)
            {
                return Decode(stream, slotPixels, stretch);
            }
        }, cancellationToken);
    }

    public static ArcImageFrame Decode(Stream stream, Size? slotPixels, Stretch stretch)
    {
        if (!stream.CanSeek)
        {
            var copy = new MemoryStream();
            stream.CopyTo(copy);
            copy.Position = 0;
            stream = copy;
        }
        var bitmap = new Bitmap(stream);
        var natural = bitmap.Size;
        var pixels = bitmap.PixelSize;
        if (slotPixels is { } slot && TargetPixels(pixels, slot, stretch) is { } target &&
            pixels.Width > target.Width * ReduceThreshold && pixels.Height > target.Height * ReduceThreshold)
        {
            var reduced = bitmap.CreateScaledBitmap(target, BitmapInterpolationMode.HighQuality);
            bitmap.Dispose();
            return new ArcImageFrame(reduced, natural, pixels, isReduced: true);
        }
        return new ArcImageFrame(bitmap, natural, pixels, isReduced: false);
    }

    /// <summary>
    /// The pixel size a picture of <paramref name="naturalPixels"/> needs to fill a slot of <paramref name="slotPixels"/> with
    /// <paramref name="stretch"/>, or null when its own size is needed (no stretch, unknown slot, or a slot larger than the picture).
    /// </summary>
    public static PixelSize? TargetPixels(PixelSize naturalPixels, Size slotPixels, Stretch stretch)
    {
        var natural = new Size(naturalPixels.Width, naturalPixels.Height);
        if (stretch == Stretch.None || natural.Width <= 0 || natural.Height <= 0)
        {
            return null;
        }
        var sx = slotPixels.Width > 0 && double.IsFinite(slotPixels.Width) ? slotPixels.Width / natural.Width : double.NaN;
        var sy = slotPixels.Height > 0 && double.IsFinite(slotPixels.Height) ? slotPixels.Height / natural.Height : double.NaN;
        double scale;
        if (double.IsNaN(sx) && double.IsNaN(sy))
        {
            return null;
        }
        if (double.IsNaN(sx) || double.IsNaN(sy))
        {
            scale = double.IsNaN(sx) ? sy : sx;
        }
        else
        {
            // Uniform fits inside the slot; UniformToFill and Fill must cover it on both axes.
            scale = stretch == Stretch.Uniform ? Math.Min(sx, sy) : Math.Max(sx, sy);
        }
        if (scale >= 1)
        {
            return null;
        }
        return new PixelSize(Math.Max(1, (int)Math.Ceiling(natural.Width * scale)), Math.Max(1, (int)Math.Ceiling(natural.Height * scale)));
    }
}
