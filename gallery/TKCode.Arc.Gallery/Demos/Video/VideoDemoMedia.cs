using Avalonia.Media.Imaging;
using Avalonia.Platform;

namespace TKCode.Arc.Gallery.Demos;

/// <summary>
/// The media of the video demos (gallery/TKCode.Arc.Gallery/Media/Video, made by tools/video/make-demo.sh): a 12 second clip of
/// generated gradients in three chapters with a synthesized chord, its poster, and captions and chapters in three languages.
/// The clip is a resource of TKCode.Arc.Gallery.Shots on desktop; the WebAssembly host serves it as a file and sets <see cref="HttpRoot"/>.
/// </summary>
public static class VideoDemoMedia
{
    private static Bitmap? s_poster;

    /// <summary>The page's base address when the clip is served over HTTP (<c>media/Video/</c> under it); null for the resource.</summary>
    public static Uri? HttpRoot { get; set; }

    /// <summary>The demo clip: 1280×720, 12 s, chapters Dawn (0–4 s), Ember (4–8 s) and Tide (8–12 s).</summary>
    public static VideoSource Aurora => HttpRoot is { } root
        ? VideoSource.FromUri(new Uri(root, "media/Video/aurora.mp4"))
        : VideoSource.FromUri(new Uri("avares://TKCode.Arc.Gallery.Shots/Media/Video/aurora.mp4"));

    /// <summary>The clip's first chapter at 1 s.</summary>
    public static Bitmap AuroraPoster => s_poster ??= new Bitmap(AssetLoader.Open(new Uri("avares://TKCode.Arc.Gallery/Media/Video/aurora-poster.webp")));
}
