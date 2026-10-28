using System.Runtime.InteropServices.JavaScript;

namespace TKCode.Arc;

/// <summary>The <c>arc-audio</c> module (arc-audio.js).</summary>
internal static partial class BrowserAudioInterop
{
    public const string Module = "arc-audio";

    [JSImport("create", Module)]
    public static partial JSObject Create([JSMarshalAs<JSType.Function<JSType.String, JSType.Number>>] Action<string, double> callback);

    [JSImport("loadUrl", Module)]
    [return: JSMarshalAs<JSType.Promise<JSType.Number>>]
    public static partial Task<double> LoadUrl(JSObject audio, string url, bool owned);

    [JSImport("blobUrl", Module)]
    public static partial string BlobUrl([JSMarshalAs<JSType.MemoryView>] Span<byte> data, string mediaType);

    [JSImport("play", Module)]
    [return: JSMarshalAs<JSType.Promise<JSType.Boolean>>]
    public static partial Task<bool> Play(JSObject audio);

    [JSImport("pause", Module)]
    public static partial void Pause(JSObject audio);

    [JSImport("seek", Module)]
    public static partial void Seek(JSObject audio, double seconds);

    [JSImport("currentTime", Module)]
    public static partial double CurrentTime(JSObject audio);

    [JSImport("setVolume", Module)]
    public static partial void SetVolume(JSObject audio, double volume);

    [JSImport("setMuted", Module)]
    public static partial void SetMuted(JSObject audio, bool muted);

    [JSImport("setLoop", Module)]
    public static partial void SetLoop(JSObject audio, bool loop);

    [JSImport("dispose", Module)]
    public static partial void Dispose(JSObject audio);
}
