using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Imaging;

namespace TKCode.Arc;

/// <summary>
/// A stage's downscaled copies of the card pictures. Each picture is drawn from a copy rendered once at the largest size it
/// is currently drawn at on the screen (device pixels, perspective included), rounded up to a step of 1.25 so a flight does
/// not rebuild it every frame.
/// </summary>
/// <remarks>
/// <para>
/// Why: a GPU compositor keeps each picture as a texture at its full pixel size, so large pictures shown small fill the Skia
/// resource budget (Avalonia's default is 28 MB: the Gallery's fifteen 640 × 800 pictures need 30.7 MB, so they were evicted
/// and uploaded again every frame), and a picture shrunk several times aliases (linear sampling without mipmaps). A copy
/// at the drawn size is a fraction of that (about 5 MB for the Gallery's grid) and is never sampled more than 1.25 times
/// smaller.
/// </para>
/// <para>
/// Rules: the stage reports every frame which size each picture is drawn at now and where flying cards land
/// (<see cref="Want"/>). A copy that is too small is rebuilt larger: at rest at once, during motion at most
/// <see cref="BuildsPerFrame"/> a frame (what is drawn now before where cards land; a card without its copy draws the
/// original meanwhile). A copy larger than needed is rebuilt smaller only when the stage asks (after it has rested). A
/// picture needed at its full size or larger has no copy (copies never upscale). Only <see cref="Bitmap"/>s are copied
/// (drawings and vector images stay as they are). Copies go with their pictures (<see cref="Prune"/>) and with the stage
/// (<see cref="Clear"/>); a copy still referenced by a recorded frame stays valid (Avalonia counts the references). A copy
/// is made from the picture once: a bitmap drawn into again keeps its old copy until the item's image changes.
/// </para>
/// </remarks>
internal sealed class LayoutMorphPictures
{
    /// <summary>Size ratio between neighbouring copy sizes.</summary>
    public const double Step = 1.25;

    /// <summary>Copies built per frame while the stage moves (each is a resample of the picture on the UI thread).</summary>
    public const int BuildsPerFrame = 2;

    private const int None = int.MaxValue;

    private readonly Dictionary<IImage, Entry> _entries = new(ReferenceEqualityComparer.Instance);
    private readonly List<IImage> _gone = [];

    private sealed class Entry(Bitmap source)
    {
        public readonly Bitmap Source = source;

        /// <summary>The copy (null: the source is drawn).</summary>
        public RenderTargetBitmap? Copy;

        /// <summary>The copy's size is the source's × 1.25^−Level (0 while there is no copy).</summary>
        public int Level;

        /// <summary>The smallest level asked for by what is drawn this frame.</summary>
        public int Now = None;

        /// <summary>The smallest level asked for by where flying cards land.</summary>
        public int Ahead = None;

        public bool Kept;

        public bool Changed;
    }

    /// <summary>Pictures with an entry (tests).</summary>
    public int Count => _entries.Count;

    /// <summary>Copies built so far (tests).</summary>
    public int Builds { get; private set; }

    /// <summary>Pixel bytes of all copies (tests).</summary>
    public long Bytes
    {
        get
        {
            long bytes = 0;
            foreach (var entry in _entries.Values)
            {
                if (entry.Copy is { } copy)
                {
                    bytes += 4L * copy.PixelSize.Width * copy.PixelSize.Height;
                }
            }
            return bytes;
        }
    }

    /// <summary>What to draw for <paramref name="image"/>: its copy, or the image itself.</summary>
    public IImage Picture(IImage image) => _entries.TryGetValue(image, out var entry) && entry.Copy is { } copy ? copy : image;

    /// <summary>The copy of <paramref name="image"/> (tests; null: none).</summary>
    public RenderTargetBitmap? CopyOf(IImage image) => _entries.TryGetValue(image, out var entry) ? entry.Copy : null;

    /// <summary>
    /// The copy level for <paramref name="density"/> device pixels per source pixel: the largest level whose size is at least
    /// that dense (0, no copy, from a density of 1 up).
    /// </summary>
    public static int LevelFor(double density)
    {
        if (!(density < 1))
        {
            return 0;
        }
        var level = Math.Floor(Math.Log(1 / density) / Math.Log(Step) + 1e-9);
        return (int)Math.Clamp(level, 0, 30);
    }

    /// <summary>The pixel size of a copy of <paramref name="source"/> at <paramref name="level"/>.</summary>
    public static PixelSize CopySize(PixelSize source, int level)
    {
        var scale = Math.Pow(Step, -level);
        return new PixelSize(Math.Max(1, (int)Math.Ceiling(source.Width * scale - 1e-6)),
            Math.Max(1, (int)Math.Ceiling(source.Height * scale - 1e-6)));
    }

    /// <summary>Starts a frame: forgets the previous frame's sizes.</summary>
    public void BeginFrame()
    {
        foreach (var entry in _entries.Values)
        {
            entry.Now = None;
            entry.Ahead = None;
        }
    }

    /// <summary>
    /// <paramref name="image"/> is drawn (<paramref name="ahead"/>: will be drawn when a flight lands) at
    /// <paramref name="density"/> device pixels per source pixel. Other images than bitmaps are ignored.
    /// </summary>
    public void Want(IImage? image, double density, bool ahead)
    {
        if (image is not Bitmap bitmap || !(density > 0) || bitmap.PixelSize.Width <= 0 || bitmap.PixelSize.Height <= 0)
        {
            return;
        }
        if (!_entries.TryGetValue(image, out var entry))
        {
            entry = new Entry(bitmap);
            _entries.Add(image, entry);
        }
        var level = LevelFor(density);
        if (ahead)
        {
            entry.Ahead = Math.Min(entry.Ahead, level);
        }
        else
        {
            entry.Now = Math.Min(entry.Now, level);
        }
    }

    /// <summary>
    /// Ends a frame: builds the copies this frame's sizes need (all of them when <paramref name="resting"/>, otherwise at
    /// most <see cref="BuildsPerFrame"/>), and with <paramref name="shrink"/> rebuilds copies larger than needed. True when
    /// a picture changed (<see cref="IsChanged"/> tells which).
    /// </summary>
    public bool EndFrame(bool resting, bool shrink)
    {
        var budget = resting ? int.MaxValue : BuildsPerFrame;
        var changed = false;
        // First the copies that are drawn larger than they are now (they look soft), then everything else.
        foreach (var entry in _entries.Values)
        {
            if (budget > 0 && entry.Copy is not null && entry.Now < entry.Level)
            {
                changed |= Apply(entry, Math.Min(entry.Now, entry.Ahead), shrink, ref budget);
            }
        }
        foreach (var entry in _entries.Values)
        {
            var wanted = Math.Min(entry.Now, entry.Ahead);
            if (wanted != None)
            {
                changed |= Apply(entry, wanted, shrink, ref budget);
            }
        }
        return changed;
    }

    private bool Apply(Entry entry, int wanted, bool shrink, ref int budget)
    {
        if (wanted == 0)
        {
            // Needed at full size: the source itself (dropping costs nothing).
            if (entry.Copy is null)
            {
                return false;
            }
            Replace(entry, null, 0);
            return true;
        }
        var build = entry.Copy is null || wanted < entry.Level || (shrink && wanted > entry.Level);
        if (!build || budget <= 0)
        {
            return false;
        }
        budget--;
        Replace(entry, Render(entry.Source, wanted), wanted);
        Builds++;
        return true;
    }

    private static void Replace(Entry entry, RenderTargetBitmap? copy, int level)
    {
        entry.Copy?.Dispose();
        entry.Copy = copy;
        entry.Level = level;
        entry.Changed = true;
    }

    /// <summary>
    /// Renders <paramref name="source"/> at <paramref name="level"/>: trilinear (mipmapped) sampling, so the copy does not
    /// alias however far it shrinks. Source rectangles are in pixels (a bitmap's <c>Size</c> is in DIPs).
    /// </summary>
    private static RenderTargetBitmap Render(Bitmap source, int level)
    {
        var pixels = source.PixelSize;
        var size = CopySize(pixels, level);
        var copy = new RenderTargetBitmap(size, new Vector(96, 96));
        using (var context = copy.CreateDrawingContext())
        using (context.PushRenderOptions(new RenderOptions { BitmapInterpolationMode = BitmapInterpolationMode.HighQuality }))
        {
            context.DrawImage(source, new Rect(0, 0, pixels.Width, pixels.Height), new Rect(0, 0, size.Width, size.Height));
        }
        return copy;
    }

    /// <summary>Whether <paramref name="image"/>'s copy changed since <see cref="ClearChanged"/>.</summary>
    public bool IsChanged(IImage image) => _entries.TryGetValue(image, out var entry) && entry.Changed;

    /// <summary>Clears every changed mark (after the stage repainted the cards).</summary>
    public void ClearChanged()
    {
        foreach (var entry in _entries.Values)
        {
            entry.Changed = false;
        }
    }

    /// <summary>Drops the copies of pictures no card shows any more (<paramref name="images"/>: the pictures still shown).</summary>
    public void Prune(IEnumerable<IImage?> images)
    {
        foreach (var entry in _entries.Values)
        {
            entry.Kept = false;
        }
        foreach (var image in images)
        {
            if (image is not null && _entries.TryGetValue(image, out var entry))
            {
                entry.Kept = true;
            }
        }
        foreach (var (image, entry) in _entries)
        {
            if (!entry.Kept)
            {
                _gone.Add(image);
            }
        }
        foreach (var image in _gone)
        {
            _entries[image].Copy?.Dispose();
            _entries.Remove(image);
        }
        _gone.Clear();
    }

    /// <summary>Drops every copy (the stage left the tree).</summary>
    public void Clear()
    {
        foreach (var entry in _entries.Values)
        {
            entry.Copy?.Dispose();
        }
        _entries.Clear();
    }
}
