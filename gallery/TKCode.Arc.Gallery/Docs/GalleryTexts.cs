using System.Text;
using Avalonia.Platform;

namespace TKCode.Arc.Gallery.Docs;

/// <summary>Where the gallery reads the text of its pages (<see cref="GalleryTexts"/>).</summary>
public interface IGalleryTextSource
{
    /// <summary>
    /// Reads the file at <paramref name="path"/>, relative to the gallery project (<c>Docs/TextFields/Input/zh-CN.md</c>,
    /// <c>Guides/Theming/en-US.md</c>), or returns null when there is none or it cannot be read. A source that has the
    /// text at hand completes at once, and the page shows its content with its first frame; one that fetches it completes
    /// later.
    /// </summary>
    ValueTask<string?> ReadAsync(string path);
}

/// <summary>
/// The text of the docs pages and of the Docs-section guides. The default reads the files from the
/// <c>TKCode.Arc.Gallery.Content</c> assembly; the WebAssembly host replaces it with one that fetches a page from the
/// server when it is shown, so a visitor downloads the pages they read, in their language, instead of every page in every
/// language at startup.
/// </summary>
public static class GalleryTexts
{
    public static IGalleryTextSource Source { get; set; } = new AssetTextSource();

    /// <summary>
    /// <paramref name="path"/> from <see cref="Source"/> for callers that cannot wait (tests, tools); fails when the source
    /// has to fetch it.
    /// </summary>
    internal static string? Read(string path)
    {
        var pending = Source.ReadAsync(path);
        if (!pending.IsCompleted)
        {
            throw new InvalidOperationException($"{Source.GetType().Name} fetches {path}: read it with the asynchronous method.");
        }
        return pending.Result;
    }

    private sealed class AssetTextSource : IGalleryTextSource
    {
        public ValueTask<string?> ReadAsync(string path)
        {
            var uri = new Uri($"avares://TKCode.Arc.Gallery.Content/{path}");
            if (!AssetLoader.Exists(uri))
            {
                return new ValueTask<string?>((string?)null);
            }
            using var reader = new StreamReader(AssetLoader.Open(uri), Encoding.UTF8);
            return new ValueTask<string?>(reader.ReadToEnd());
        }
    }
}
