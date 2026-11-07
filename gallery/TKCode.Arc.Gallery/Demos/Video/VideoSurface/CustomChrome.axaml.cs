using Avalonia.Controls;
using Avalonia.Interactivity;

namespace TKCode.Arc.Gallery.Demos;

/// <summary>Drives an IVideoPlayer with plain buttons and shows it on a VideoSurface.</summary>
public partial class VideoSurfaceCustomChromeDemo : UserControl
{
    private IVideoPlayer? _player;

    public VideoSurfaceCustomChromeDemo()
    {
        InitializeComponent();
        AttachedToVisualTree += async (_, _) =>
        {
            var player = ArcVideo.CreatePlayer();
            player.StateChanged += (_, _) => UpdateLabels();
            player.PositionChanged += (_, _) => UpdateLabels();
            player.DurationChanged += (_, _) => UpdateLabels();
            _player = player;
            Surface.Player = player;
            await player.LoadAsync(VideoDemoMedia.Aurora);
            UpdateLabels();
        };
        DetachedFromVisualTree += (_, _) =>
        {
            Surface.Player = null;
            _player?.Dispose();
            _player = null;
        };
    }

    private void OnPlay(object? sender, RoutedEventArgs e)
    {
        if (_player is null)
        {
            return;
        }
        if (_player.State is VideoPlaybackState.Playing or VideoPlaybackState.Buffering)
        {
            _player.Pause();
        }
        else
        {
            _player.Play();
        }
    }

    private void OnBack(object? sender, RoutedEventArgs e) => _player?.Seek(_player.Position - TimeSpan.FromSeconds(5));

    private void OnForward(object? sender, RoutedEventArgs e) => _player?.Seek(_player.Position + TimeSpan.FromSeconds(5));

    private void UpdateLabels()
    {
        if (_player is null)
        {
            return;
        }
        PlayButton.Content = _player.State is VideoPlaybackState.Playing or VideoPlaybackState.Buffering ? "Pause" : "Play";
        Time.Text = $"{_player.Position:m\\:ss} / {_player.Duration:m\\:ss}";
    }
}
