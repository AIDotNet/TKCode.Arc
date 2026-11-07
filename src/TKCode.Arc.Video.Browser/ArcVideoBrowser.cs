using System.Diagnostics;
using System.Runtime.InteropServices.JavaScript;

namespace TKCode.Arc;

/// <summary>Turns on the browser video backend (design/components/video/video.md section 5.3).</summary>
public static class ArcVideoBrowser
{
    private static Task<bool>? s_use;

    /// <summary>
    /// Imports the <c>arc-video</c> JavaScript module and makes <see cref="BrowserVideoBackend"/> the <see cref="ArcVideo.Backend"/>.
    /// Returns false, and leaves the current backend in place, when not running in a browser or the module cannot be imported.
    /// Safe to call more than once.
    /// </summary>
    public static Task<bool> UseAsync() => s_use ??= ImportAsync();

    private static async Task<bool> ImportAsync()
    {
        if (!OperatingSystem.IsBrowser())
        {
            return false;
        }
        try
        {
            await JSHost.ImportAsync(BrowserVideoInterop.Module, ModuleUrl());
            ArcVideo.Backend = BrowserVideoBackend.Instance;
            return true;
        }
        catch (Exception exception)
        {
            Trace.TraceWarning($"TKCode.Arc.Video.Browser: {exception.Message}; video stays simulated.");
            s_use = null;
            return false;
        }
    }

    private static string ModuleUrl()
    {
        using var stream = typeof(ArcVideoBrowser).Assembly.GetManifestResourceStream("TKCode.Arc.Video.Browser.arc-video.js")
                           ?? throw new InvalidOperationException("arc-video.js is missing from the assembly.");
        using var reader = new StreamReader(stream);
        return "data:text/javascript;charset=utf-8," + Uri.EscapeDataString(reader.ReadToEnd());
    }
}

/// <summary>
/// Plays through one HTML video element per player and copies its frames into the Avalonia canvas as RGBA pixels.
/// Turned on by <see cref="ArcVideoBrowser.UseAsync"/>.
/// </summary>
public sealed class BrowserVideoBackend : IVideoBackend
{
    internal static BrowserVideoBackend Instance { get; } = new();

    private BrowserVideoBackend()
    {
    }

    /// <inheritdoc />
    public string Name => "browser";

    /// <inheritdoc />
    public bool IsReal => true;

    /// <inheritdoc />
    public VideoFrameKinds FrameKinds => VideoFrameKinds.CpuBgra;

    /// <inheritdoc />
    public IReadOnlyList<string> GpuHandleTypes => [];

    /// <summary>
    /// The <c>crossorigin</c> value set on the video element for http(s) sources; <c>"anonymous"</c> by default, null for none.
    /// A cross-origin video's frames can only be read when the server answers with CORS headers; without them the browser
    /// still plays it but the Canvas presentation shows no picture. Same-origin and in-memory sources are not affected.
    /// Applies to players created afterwards.
    /// </summary>
    public string? CrossOrigin { get; set; } = "anonymous";

    /// <inheritdoc />
    public VideoSupport CanPlay(string mediaType)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(mediaType);
        return BrowserVideoInterop.CanPlayType(mediaType) switch
        {
            "probably" => VideoSupport.Probably,
            "maybe" => VideoSupport.Maybe,
            _ => VideoSupport.No,
        };
    }

    /// <inheritdoc />
    public IVideoPlayer CreatePlayer(VideoPlayerOptions? options = null) => new BrowserVideoPlayer(this);
}
