using Avalonia.Controls;

namespace TKCode.Arc;

/// <summary>
/// Implemented by players that can show their picture as a platform element on the page instead of handing frames to
/// <see cref="VideoSurface"/> (the browser's Native presentation, design/components/video/video.md section 5.3). The element
/// decodes and draws with no copies and plays every format the browser does, but nothing can be drawn over it: controls go
/// below the picture, captions are drawn by the platform view, and rounded corners and overlays are not available.
/// <see cref="VideoPlayer"/> uses it when <see cref="VideoPlayer.BrowserPresentation"/> is
/// <see cref="VideoBrowserPresentation.Native"/>. The view must let pointer input through to the controls under it.
/// </summary>
public interface IVideoNativePresentation
{
    /// <summary>Whether <see cref="CreateNativeView"/> works in this process and build.</summary>
    bool CanPresentNatively { get; }

    /// <summary>
    /// Creates a control (a <see cref="NativeControlHost"/>) that shows the player's picture. While it is in the visual tree the
    /// player hands out no frames (<see cref="IVideoPlayer.TryAcquireFrame"/> returns false); when it leaves, frames resume and
    /// <see cref="IVideoPlayer.FrameAvailable"/> is raised. Show one such control per player at a time.
    /// </summary>
    /// <exception cref="NotSupportedException"><see cref="CanPresentNatively"/> is false.</exception>
    Control CreateNativeView();

    /// <summary>
    /// Shows <paramref name="track"/>'s cues in the platform view, which draws its own captions (Arc's caption layer cannot be
    /// drawn over it); null shows none. Cues loaded or changed later are picked up. <see cref="VideoPlayer"/> calls this
    /// while its picture is the platform view.
    /// </summary>
    void SetNativeCaptions(VideoTextTrack? track);
}
