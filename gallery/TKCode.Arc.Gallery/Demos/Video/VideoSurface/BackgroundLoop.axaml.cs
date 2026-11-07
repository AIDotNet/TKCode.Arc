using Avalonia.Controls;
using TKCode.Arc.Motion;

namespace TKCode.Arc.Gallery.Demos;

/// <summary>Plays the demo clip muted and looping on a bare VideoSurface while the demo is shown.</summary>
public partial class VideoSurfaceBackgroundLoopDemo : UserControl
{
    private IVideoPlayer? _player;

    public VideoSurfaceBackgroundLoopDemo()
    {
        InitializeComponent();
        AttachedToVisualTree += async (_, _) =>
        {
            var player = ArcVideo.CreatePlayer();
            player.IsMuted = true;
            player.IsLooping = true;
            _player = player;
            Surface.Player = player;
            await player.LoadAsync(VideoDemoMedia.Aurora);
            // A looping background is decoration: it stays still when motion is reduced.
            if (ReferenceEquals(_player, player) && ArcMotion.GetEffectiveLevel(this) == ArcMotionLevel.Full)
            {
                player.Play();
            }
        };
        DetachedFromVisualTree += (_, _) =>
        {
            Surface.Player = null;
            _player?.Dispose();
            _player = null;
        };
    }
}
