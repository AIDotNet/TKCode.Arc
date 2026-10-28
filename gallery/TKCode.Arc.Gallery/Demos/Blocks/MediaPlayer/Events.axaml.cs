using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class MediaPlayerEventsDemo : UserControl
{
    private readonly List<string> _log = [];

    public MediaPlayerEventsDemo()
    {
        InitializeComponent();
        Player.Tracks.AddRange(MediaPlayerDemoData.Tracks());
        Previous.Click += (_, _) => Player.PreviousTrack();
        Toggle.Click += (_, _) => Player.TogglePlay();
        Skip.Click += (_, _) => Player.Seek(TimeSpan.FromSeconds(20));
        Mute.Click += (_, _) => Player.ToggleMute();
        Player.SelectedTrackChanged += (_, e) => Add($"{e.ChangeSource}: {e.OldTrack?.Title} → {e.NewTrack?.Title}");
        Player.TrackEnded += (_, e) => Add($"Ended: {e.Track.Title}");
        Player.PlaybackFailed += (_, e) => Add($"Failed: {e.Track.Title} ({e.Message})");
        Player.PropertyChanged += (_, e) =>
        {
            if (e.Property == MediaPlayer.IsPlayingProperty || e.Property == MediaPlayer.VolumeProperty
                || e.Property == MediaPlayer.PositionProperty || e.Property == MediaPlayer.IsAudibleProperty)
            {
                ShowState();
            }
        };
        ShowState();
    }

    private void ShowState()
    {
        var sound = Player.IsAudible ? "audible backend" : "silent backend";
        State.Text = $"{(Player.IsPlaying ? "Playing" : "Paused")} at {MediaPlayerTime.Format(Player.Position)} · volume {Player.Volume:P0} · {sound}";
    }

    private void Add(string line)
    {
        _log.Insert(0, line);
        if (_log.Count > 4)
        {
            _log.RemoveAt(_log.Count - 1);
        }
        Log.Text = string.Join("\n", _log);
    }
}
