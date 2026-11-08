using Android.Media;

namespace TKCode.Arc;

/// <summary>
/// The Android backend (<c>"mediaplayer"</c>): the platform's <c>android.media.MediaPlayer</c> decodes and plays the sound;
/// its pictures go to an <c>ImageReader</c> and are converted to BGRA at the size they are shown. No third-party player is
/// used (the package depends only on Avalonia). Created by <see cref="ArcVideoNative.Use"/> on Android.
/// </summary>
public sealed class AndroidVideoBackend : IVideoBackend
{
    internal static AndroidVideoBackend Instance { get; } = new();

    private AndroidVideoBackend()
    {
    }

    /// <inheritdoc />
    public string Name => "mediaplayer";

    /// <inheritdoc />
    public bool IsReal => true;

    /// <inheritdoc />
    public VideoFrameKinds FrameKinds => VideoFrameKinds.CpuBgra;

    /// <inheritdoc />
    public IReadOnlyList<string> GpuHandleTypes => [];

    /// <inheritdoc />
    public VideoSupport CanPlay(string mediaType)
    {
        if (string.IsNullOrWhiteSpace(mediaType))
        {
            return VideoSupport.No;
        }
        var bare = mediaType.Split(';', 2)[0].Trim().ToLowerInvariant();
        if (bare is not ("video/mp4" or "video/webm" or "video/3gpp" or "video/x-matroska" or "video/mp2t" or "audio/mp4" or "audio/mpeg"))
        {
            return VideoSupport.No;
        }
        // MP4 with H.264 / AAC plays on every Android device; the rest depends on the device's codecs.
        return bare == "video/mp4" && HasDecoder(MediaFormat.MimetypeVideoAvc) ? VideoSupport.Probably : VideoSupport.Maybe;
    }

    /// <inheritdoc />
    public IVideoPlayer CreatePlayer(VideoPlayerOptions? options = null) => new AndroidVideoPlayer(this);

    private static bool HasDecoder(string mime)
    {
        using var list = new MediaCodecList(MediaCodecListKind.RegularCodecs);
        foreach (var info in list.GetCodecInfos() ?? [])
        {
            if (info.IsEncoder)
            {
                continue;
            }
            foreach (var type in info.GetSupportedTypes() ?? [])
            {
                if (string.Equals(type, mime, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
        }
        return false;
    }
}
