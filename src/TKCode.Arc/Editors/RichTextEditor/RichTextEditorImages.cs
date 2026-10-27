using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;

namespace TKCode.Arc;

internal enum RichTextEditorImageState
{
    Loading,
    Ready,
    Failed
}

/// <summary>One image the editor shows: the picture once it is loaded, or why there is none.</summary>
internal sealed class RichTextEditorImageEntry
{
    public IImage? Image { get; set; }

    public RichTextEditorImageState State { get; set; }
}

/// <summary>
/// Loads and keeps the pictures of the document's image blocks, by address. The host may load an address itself (the editor's
/// <c>ImageLoading</c> event); otherwise <c>data:</c> addresses, http(s), <c>avares:</c> resources and local files are read here.
/// </summary>
internal sealed class RichTextEditorImageCache
{
    private static readonly HttpClient s_http = new();

    private readonly Dictionary<string, RichTextEditorImageEntry> _entries = new(StringComparer.Ordinal);

    /// <summary>Asks the host for the picture of an address; null (or a null task) means the editor loads it.</summary>
    public Func<string, Task<Bitmap?>?>? HostLoader { get; set; }

    /// <summary>Raised on the UI thread when an address finished loading (or failed).</summary>
    public event Action<string>? Changed;

    public RichTextEditorImageEntry Get(string src)
    {
        if (_entries.TryGetValue(src, out var entry))
        {
            return entry;
        }
        entry = new RichTextEditorImageEntry();
        _entries[src] = entry;
        _ = LoadAsync(src, entry);
        return entry;
    }

    /// <summary>Forgets what was loaded for an address (or everything), so the next draw loads it again.</summary>
    public void Reset(string? src = null)
    {
        if (src is null)
        {
            _entries.Clear();
            return;
        }
        _entries.Remove(src);
    }

    private async Task LoadAsync(string src, RichTextEditorImageEntry entry)
    {
        try
        {
            var task = HostLoader?.Invoke(src) ?? Task.Run(() => LoadDefaultAsync(src));
            var bitmap = await task;
            entry.Image = bitmap;
            entry.State = bitmap is null ? RichTextEditorImageState.Failed : RichTextEditorImageState.Ready;
        }
        catch (Exception)
        {
            entry.State = RichTextEditorImageState.Failed;
        }
        Changed?.Invoke(src);
    }

    private static async Task<Bitmap?> LoadDefaultAsync(string src)
    {
        var value = src.Trim();
        if (value.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
        {
            var comma = value.IndexOf(',');
            if (comma < 0)
            {
                return null;
            }
            var header = value[..comma];
            var body = value[(comma + 1)..];
            var bytes = header.Contains(";base64", StringComparison.OrdinalIgnoreCase) ? Convert.FromBase64String(body) : System.Text.Encoding.UTF8.GetBytes(Uri.UnescapeDataString(body));
            return new Bitmap(new MemoryStream(bytes));
        }
        if (value.StartsWith("http:", StringComparison.OrdinalIgnoreCase) || value.StartsWith("https:", StringComparison.OrdinalIgnoreCase))
        {
            var bytes = await s_http.GetByteArrayAsync(value);
            return new Bitmap(new MemoryStream(bytes));
        }
        if (value.StartsWith("avares:", StringComparison.OrdinalIgnoreCase))
        {
            using var stream = AssetLoader.Open(new Uri(value));
            return new Bitmap(stream);
        }
        var path = value.StartsWith("file:", StringComparison.OrdinalIgnoreCase) ? new Uri(value).LocalPath : value;
        if (Path.IsPathRooted(path) && File.Exists(path))
        {
            return new Bitmap(path);
        }
        return null;
    }

    /// <summary>The size an image is drawn at in a column of <paramref name="width"/>: its own size, shrunk to fit.</summary>
    public static Size Fit(Size natural, double width)
    {
        if (natural.Width <= 0 || natural.Height <= 0)
        {
            return new Size(Math.Min(width, 240), 160);
        }
        var scale = natural.Width > width ? width / natural.Width : 1;
        return new Size(natural.Width * scale, natural.Height * scale);
    }
}
