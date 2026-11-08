using System.Runtime.InteropServices.JavaScript;

namespace TKCode.Arc;

/// <summary>The <c>arc-video</c> module (arc-video.js). The player handle is the module's per-player state object.</summary>
internal static partial class BrowserVideoInterop
{
    public const string Module = "arc-video";

    [JSImport("create", Module)]
    public static partial JSObject Create([JSMarshalAs<JSType.Function<JSType.String, JSType.Number>>] Action<string, double> callback);

    /// <summary>Resolves to the duration (0 when unknown or live), or to minus the <c>MediaError</c> code when loading fails.</summary>
    [JSImport("loadUrl", Module)]
    [return: JSMarshalAs<JSType.Promise<JSType.Number>>]
    public static partial Task<double> LoadUrl(JSObject player, string url, bool owned);

    [JSImport("blobUrl", Module)]
    public static partial string BlobUrl([JSMarshalAs<JSType.MemoryView>] Span<byte> data, string mediaType);

    [JSImport("setCrossOrigin", Module)]
    public static partial void SetCrossOrigin(JSObject player, string? value);

    [JSImport("canPlayType", Module)]
    public static partial string CanPlayType(string mediaType);

    [JSImport("play", Module)]
    [return: JSMarshalAs<JSType.Promise<JSType.Boolean>>]
    public static partial Task<bool> Play(JSObject player);

    [JSImport("pause", Module)]
    public static partial void Pause(JSObject player);

    [JSImport("isPaused", Module)]
    public static partial bool IsPaused(JSObject player);

    [JSImport("seek", Module)]
    public static partial void Seek(JSObject player, double seconds, bool fast);

    [JSImport("currentTime", Module)]
    public static partial double CurrentTime(JSObject player);

    [JSImport("isSeekable", Module)]
    public static partial bool IsSeekable(JSObject player);

    /// <summary>Start and end of every buffered range, in seconds, flattened.</summary>
    [JSImport("buffered", Module)]
    [return: JSMarshalAs<JSType.Array<JSType.Number>>]
    public static partial double[] Buffered(JSObject player);

    [JSImport("setVolume", Module)]
    public static partial void SetVolume(JSObject player, double volume);

    [JSImport("setMuted", Module)]
    public static partial void SetMuted(JSObject player, bool muted);

    [JSImport("setLoop", Module)]
    public static partial void SetLoop(JSObject player, bool loop);

    [JSImport("setRate", Module)]
    public static partial void SetRate(JSObject player, double rate);

    /// <summary>0 when there is no new frame, else its width * 65536 + height.</summary>
    [JSImport("peekFrame", Module)]
    public static partial double PeekFrame(JSObject player, int targetWidth, int targetHeight);

    /// <summary>Makes the next <see cref="PeekFrame"/> report the current frame again.</summary>
    [JSImport("resendFrame", Module)]
    public static partial void ResendFrame(JSObject player);

    /// <summary>Copies the frame <see cref="PeekFrame"/> described into <paramref name="pixels"/>; returns its media time in seconds.</summary>
    [JSImport("takeFrame", Module)]
    public static partial double TakeFrame(JSObject player, [JSMarshalAs<JSType.MemoryView>] Span<byte> pixels);

    [JSImport("attachNative", Module)]
    public static partial JSObject AttachNative(JSObject player);

    [JSImport("detachNative", Module)]
    public static partial void DetachNative(JSObject player);

    [JSImport("watchFullscreen", Module)]
    public static partial void WatchFullscreen([JSMarshalAs<JSType.Function>] Action onExit);

    [JSImport("enterFullscreen", Module)]
    public static partial void EnterFullscreen();

    [JSImport("exitFullscreen", Module)]
    public static partial void ExitFullscreen();

    [JSImport("setCaptions", Module)]
    public static partial void SetCaptions(JSObject player, [JSMarshalAs<JSType.Array<JSType.Number>>] double[] starts,
        [JSMarshalAs<JSType.Array<JSType.Number>>] double[] ends, string[] texts);

    [JSImport("dispose", Module)]
    public static partial void Dispose(JSObject player);
}
