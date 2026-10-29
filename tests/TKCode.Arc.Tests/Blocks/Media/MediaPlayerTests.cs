using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace TKCode.Arc.Tests.Blocks.Media;

/// <summary>
/// <see cref="MediaPlayer"/> against a scripted backend: skipping (wraps, keeps the play state), playlist rows (play,
/// toggle), the end of a track (stops at 0:00), seeking and volume from the keyboard, mute, names for screen readers,
/// the compact layout and the playing bars.
/// </summary>
public class MediaPlayerTests
{
    private static MediaTrack Track(string title, double seconds = 24) => new()
    {
        Title = title,
        Artist = "Low Static",
        Album = "Night ferry",
        Duration = TimeSpan.FromSeconds(seconds),
        Source = AudioSource.FromBytes(WavFile.Encode(new float[8000 * (int)seconds], 8000)),
    };

    private static (MediaPlayer Player, FakeBackend Backend, Window Window) Show(int width = 520, params string[] titles)
    {
        var backend = new FakeBackend();
        var player = new MediaPlayer { AudioBackend = backend, Width = width };
        foreach (var title in titles.Length > 0 ? titles : ["Harbor lights", "Paper kites", "Slow orbit"])
        {
            player.Tracks.Add(Track(title));
        }
        var window = TestHelpers.Show(player);
        window.Width = width + 40;
        window.Height = 900;
        Dispatcher.UIThread.RunJobs();
        return (player, backend, window);
    }

    private static T Part<T>(MediaPlayer player, string name) where T : Control =>
        player.GetVisualDescendants().OfType<T>().First(c => c.Name == name);

    private static MediaPlayerTrackRow[] Rows(MediaPlayer player) =>
        player.GetVisualDescendants().OfType<MediaPlayerTrackRow>().ToArray();

    [AvaloniaFact]
    public void Starts_Paused_On_The_First_Track_With_Its_Texts()
    {
        var (player, backend, _) = Show();
        player.SelectedIndex.ShouldBe(0);
        player.SelectedTrack!.Title.ShouldBe("Harbor lights");
        player.IsPlaying.ShouldBeFalse();
        player.Duration.ShouldBe(TimeSpan.FromSeconds(24));
        backend.Last.Loaded.ShouldBe(1);
        Part<TextBlock>(player, MediaPlayer.ElapsedPart).Text.ShouldBe("0:00");
        Part<TextBlock>(player, MediaPlayer.LengthPart).Text.ShouldBe("0:24");
        Part<TextBlock>(player, MediaPlayer.PlaylistTitlePart).Text.ShouldBe("Playlist");
        AutomationProperties.GetName(player).ShouldBe("Audio player");
        AutomationProperties.GetName(Part<MediaPlayerButton>(player, MediaPlayer.PlayPart)).ShouldBe("Play");
        AutomationProperties.GetName(Part<MediaPlayerButton>(player, MediaPlayer.PreviousPart)).ShouldBe("Previous track");
        AutomationProperties.GetName(Part<MediaPlayerButton>(player, MediaPlayer.NextPart)).ShouldBe("Next track");
        AutomationProperties.GetName(Part<MediaPlayerRange>(player, MediaPlayer.SeekPart)).ShouldBe("Seek Harbor lights");
        AutomationProperties.GetItemStatus(Part<MediaPlayerRange>(player, MediaPlayer.SeekPart)).ShouldBe("0:00 of 0:24");
        var rows = Rows(player);
        rows.Select(r => r.TitleText).ShouldBe(["Harbor lights", "Paper kites", "Slow orbit"]);
        rows.Select(r => r.LengthText).ShouldBe(["0:24", "0:24", "0:24"]);
        rows.Select(r => r.IsCurrent).ShouldBe([true, false, false]);
        rows.Select(r => r.IsSeparated).ShouldBe([false, true, true]);
        rows.Select(AutomationProperties.GetName).ShouldBe(["Play Harbor lights", "Play Paper kites", "Play Slow orbit"]);
    }

    [AvaloniaFact]
    public void Play_And_Pause_Drive_The_Backend_And_The_Labels()
    {
        var (player, backend, _) = Show();
        player.Play();
        backend.Last.State.ShouldBe(AudioPlaybackState.Playing);
        player.IsPlaying.ShouldBeTrue();
        player.Classes.Contains(":playing").ShouldBeTrue();
        AutomationProperties.GetName(Part<MediaPlayerButton>(player, MediaPlayer.PlayPart)).ShouldBe("Pause");
        var rows = Rows(player);
        AutomationProperties.GetName(rows[0]).ShouldBe("Pause Harbor lights");
        rows[0].Classes.Contains(":playing").ShouldBeTrue();

        player.TogglePlay();
        backend.Last.State.ShouldBe(AudioPlaybackState.Paused);
        AutomationProperties.GetName(Part<MediaPlayerButton>(player, MediaPlayer.PlayPart)).ShouldBe("Play");
        AutomationProperties.GetName(rows[0]).ShouldBe("Play Harbor lights");
    }

    [AvaloniaFact]
    public void The_Backend_Pausing_On_Its_Own_Is_Reflected()
    {
        var (player, backend, _) = Show();
        player.Play();
        backend.Last.Pause();
        Dispatcher.UIThread.RunJobs();
        player.IsPlaying.ShouldBeFalse();
    }

    [AvaloniaFact]
    public void Skipping_Wraps_Around_And_Keeps_The_Play_State()
    {
        var (player, backend, _) = Show();
        var changes = new List<MediaTrackChangedEventArgs>();
        player.SelectedTrackChanged += (_, e) => changes.Add(e);

        player.PreviousTrack();
        player.SelectedIndex.ShouldBe(2);
        player.IsPlaying.ShouldBeFalse();
        backend.Last.State.ShouldBe(AudioPlaybackState.Paused);

        player.Play();
        player.NextTrack();
        player.SelectedIndex.ShouldBe(0);
        player.IsPlaying.ShouldBeTrue();
        backend.Last.State.ShouldBe(AudioPlaybackState.Playing);
        player.NextTrack();
        player.SelectedTrack!.Title.ShouldBe("Paper kites");
        player.IsPlaying.ShouldBeTrue();

        changes.Select(c => c.NewTrack!.Title).ShouldBe(["Slow orbit", "Harbor lights", "Paper kites"]);
        changes[0].OldTrack!.Title.ShouldBe("Harbor lights");
        changes.ShouldAllBe(c => c.ChangeSource == MediaTrackChangeSource.Code);
    }

    [AvaloniaFact]
    public void Previous_Never_Restarts_The_Track_First()
    {
        var (player, backend, _) = Show();
        player.SelectedIndex = 1;
        player.Seek(TimeSpan.FromSeconds(12));
        player.PreviousTrack();
        player.SelectedIndex.ShouldBe(0);
        player.Position.ShouldBe(TimeSpan.Zero);
    }

    [AvaloniaFact]
    public void The_Skip_Buttons_Report_Skip_As_The_Source()
    {
        var (player, _, window) = Show();
        MediaTrackChangeSource? source = null;
        player.SelectedTrackChanged += (_, e) => source = e.ChangeSource;
        var next = Part<MediaPlayerButton>(player, MediaPlayer.NextPart);
        next.Focus();
        window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
        Dispatcher.UIThread.RunJobs();
        player.SelectedIndex.ShouldBe(1);
        source.ShouldBe(MediaTrackChangeSource.Skip);
    }

    [AvaloniaFact]
    public void A_Row_Plays_Its_Track_And_The_Current_Row_Toggles()
    {
        var (player, backend, window) = Show();
        MediaTrackChangeSource? source = null;
        player.SelectedTrackChanged += (_, e) => source = e.ChangeSource;
        var rows = Rows(player);
        rows[2].Focus();
        window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
        Dispatcher.UIThread.RunJobs();
        player.SelectedIndex.ShouldBe(2);
        player.IsPlaying.ShouldBeTrue();
        source.ShouldBe(MediaTrackChangeSource.Playlist);
        backend.Last.State.ShouldBe(AudioPlaybackState.Playing);
        Rows(player).Select(r => r.IsCurrent).ShouldBe([false, false, true]);

        player.PlayTrack(2);
        player.IsPlaying.ShouldBeFalse();
        player.PlayTrack(2);
        player.IsPlaying.ShouldBeTrue();
    }

    [AvaloniaFact]
    public void A_New_Track_Starts_From_The_Beginning()
    {
        var (player, backend, _) = Show();
        player.Seek(TimeSpan.FromSeconds(9.5));
        player.Position.ShouldBe(TimeSpan.FromSeconds(9.5));
        backend.Last.Position.ShouldBe(TimeSpan.FromSeconds(9.5));
        player.NextTrack();
        player.Position.ShouldBe(TimeSpan.Zero);
        backend.Last.Position.ShouldBe(TimeSpan.Zero);
        Part<TextBlock>(player, MediaPlayer.ElapsedPart).Text.ShouldBe("0:00");
    }

    [AvaloniaFact]
    public void The_End_Of_A_Track_Stops_At_The_Start_Of_The_Same_Track()
    {
        var (player, backend, _) = Show();
        MediaTrack? ended = null;
        player.TrackEnded += (_, e) => ended = e.Track;
        player.SelectedIndex = 1;
        player.Play();
        backend.Last.FinishTrack();
        Dispatcher.UIThread.RunJobs();
        player.SelectedIndex.ShouldBe(1);
        player.IsPlaying.ShouldBeFalse();
        player.Position.ShouldBe(TimeSpan.Zero);
        backend.Last.Position.ShouldBe(TimeSpan.Zero);
        ended!.Title.ShouldBe("Paper kites");
        AutomationProperties.GetName(Part<MediaPlayerButton>(player, MediaPlayer.PlayPart)).ShouldBe("Play");
    }

    [AvaloniaFact]
    public void Seek_Follows_The_Native_Range_Keys()
    {
        var (player, backend, window) = Show();
        var seek = Part<MediaPlayerRange>(player, MediaPlayer.SeekPart);
        seek.Focus();
        window.KeyPressQwerty(PhysicalKey.ArrowRight, RawInputModifiers.None);
        Dispatcher.UIThread.RunJobs();
        player.Position.TotalSeconds.ShouldBe(0.1, 1e-9);
        window.KeyPressQwerty(PhysicalKey.PageUp, RawInputModifiers.None);
        Dispatcher.UIThread.RunJobs();
        // A tenth of 24 s is 2.4 s, snapped to the 0.1 s step.
        player.Position.TotalSeconds.ShouldBe(2.5, 1e-9);
        backend.Last.Position.TotalSeconds.ShouldBe(2.5, 1e-9);
        window.KeyPressQwerty(PhysicalKey.End, RawInputModifiers.None);
        Dispatcher.UIThread.RunJobs();
        player.Position.ShouldBe(TimeSpan.FromSeconds(24));
        AutomationProperties.GetItemStatus(seek).ShouldBe("0:24 of 0:24");
        window.KeyPressQwerty(PhysicalKey.Home, RawInputModifiers.None);
        Dispatcher.UIThread.RunJobs();
        player.Position.ShouldBe(TimeSpan.Zero);
        window.KeyPressQwerty(PhysicalKey.ArrowLeft, RawInputModifiers.None);
        Dispatcher.UIThread.RunJobs();
        player.Position.ShouldBe(TimeSpan.Zero);
    }

    [AvaloniaFact]
    public void Volume_Keys_Move_By_One_Percent_And_Reach_The_Backend()
    {
        var (player, backend, window) = Show();
        var volume = Part<MediaPlayerRange>(player, MediaPlayer.VolumePart);
        volume.Value.ShouldBe(0.7, 1e-9);
        AutomationProperties.GetItemStatus(volume).ShouldBe("70%");
        volume.Focus();
        window.KeyPressQwerty(PhysicalKey.ArrowLeft, RawInputModifiers.None);
        Dispatcher.UIThread.RunJobs();
        player.Volume.ShouldBe(0.69, 1e-9);
        backend.Last.Volume.ShouldBe(0.69, 1e-9);
        window.KeyPressQwerty(PhysicalKey.PageUp, RawInputModifiers.None);
        Dispatcher.UIThread.RunJobs();
        player.Volume.ShouldBe(0.79, 1e-9);
        window.KeyPressQwerty(PhysicalKey.End, RawInputModifiers.None);
        Dispatcher.UIThread.RunJobs();
        player.Volume.ShouldBe(1);
        AutomationProperties.GetItemStatus(volume).ShouldBe("100%");
    }

    [AvaloniaFact]
    public void Mute_Sets_The_Volume_To_Zero_And_Restores_It()
    {
        var (player, backend, _) = Show();
        var mute = Part<MediaPlayerButton>(player, MediaPlayer.MutePart);
        var glyph = Part<MediaPlayerGlyph>(player, MediaPlayer.MuteGlyphPart);
        AutomationProperties.GetName(mute).ShouldBe("Mute audio");
        glyph.Data.ShouldBeSameAs(MediaPlayerGlyphs.Volume);

        player.Volume = 0.46;
        player.ToggleMute();
        player.Volume.ShouldBe(0);
        player.IsMuted.ShouldBeTrue();
        backend.Last.Volume.ShouldBe(0);
        AutomationProperties.GetName(mute).ShouldBe("Unmute audio");
        glyph.Data.ShouldBeSameAs(MediaPlayerGlyphs.VolumeOff);
        Part<MediaPlayerRange>(player, MediaPlayer.VolumePart).Value.ShouldBe(0);

        player.ToggleMute();
        player.Volume.ShouldBe(0.46, 1e-9);
        player.IsMuted.ShouldBeFalse();
        AutomationProperties.GetName(mute).ShouldBe("Mute audio");
    }

    [AvaloniaFact]
    public void Unmuting_A_Volume_Dragged_To_Zero_Restores_The_Default()
    {
        var (player, _, _) = Show();
        player.Volume = 0;
        player.IsMuted.ShouldBeTrue();
        player.ToggleMute();
        player.Volume.ShouldBe(0.7, 1e-9);
    }

    [AvaloniaFact]
    public void Labels_Can_Be_Replaced()
    {
        var (player, _, _) = Show();
        player.Labels = new MediaPlayerLabels { Playlist = "播放列表", Play = "播放", TrackAction = (t, p) => (p ? "暂停 " : "播放 ") + t };
        Part<TextBlock>(player, MediaPlayer.PlaylistTitlePart).Text.ShouldBe("播放列表");
        AutomationProperties.GetName(Part<MediaPlayerButton>(player, MediaPlayer.PlayPart)).ShouldBe("播放");
        AutomationProperties.GetName(Rows(player)[1]).ShouldBe("播放 Paper kites");
    }

    [AvaloniaFact]
    public void The_Loaded_Length_Replaces_The_Hint()
    {
        var (player, backend, _) = Show();
        backend.Last.SetLength(TimeSpan.FromSeconds(61));
        Dispatcher.UIThread.RunJobs();
        player.Duration.ShouldBe(TimeSpan.FromSeconds(61));
        player.Tracks[0].Duration.ShouldBe(TimeSpan.FromSeconds(61));
        Part<TextBlock>(player, MediaPlayer.LengthPart).Text.ShouldBe("1:01");
        Rows(player)[0].LengthText.ShouldBe("1:01");
    }

    [AvaloniaFact]
    public void A_Failure_Pauses_And_Is_Reported()
    {
        var (player, backend, _) = Show();
        string? message = null;
        player.PlaybackFailed += (_, e) => message = e.Message;
        player.Play();
        backend.Last.FailNow("decoder");
        Dispatcher.UIThread.RunJobs();
        player.IsPlaying.ShouldBeFalse();
        message.ShouldBe("decoder");
    }

    [AvaloniaFact]
    public void Narrow_Players_Use_The_Compact_Layout()
    {
        var (wide, _, _) = Show(520);
        wide.IsCompact.ShouldBeFalse();
        var (narrow, _, _) = Show(360);
        narrow.IsCompact.ShouldBeTrue();
        narrow.Classes.Contains(":compact").ShouldBeTrue();
        Part<MediaPlayerButton>(narrow, MediaPlayer.PlayPart).Bounds.Width.ShouldBe(42);
        Part<MediaPlayerButton>(wide, MediaPlayer.PlayPart).Bounds.Width.ShouldBe(44);
        Part<MediaPlayerRange>(narrow, MediaPlayer.VolumePart).Bounds.Width.ShouldBe(64);
        Part<MediaPlayerRange>(wide, MediaPlayer.VolumePart).Bounds.Width.ShouldBe(88);
        var cover = narrow.GetVisualDescendants().OfType<MediaPlayerCoverFrame>().Single();
        (cover.Bounds.Width / cover.Bounds.Height).ShouldBe(1.7, 0.01);
        var wideCover = wide.GetVisualDescendants().OfType<MediaPlayerCoverFrame>().Single();
        (wideCover.Bounds.Width / wideCover.Bounds.Height).ShouldBe(2.2, 0.01);
    }

    [AvaloniaFact]
    public void Rows_Are_44_High_With_A_Line_Between_Them()
    {
        var (player, _, _) = Show();
        Rows(player).Select(r => r.Bounds.Height).ShouldBe([44.0, 45.0, 45.0]);
    }

    [AvaloniaFact]
    public void The_Playing_Bars_Show_Only_On_The_Playing_Row()
    {
        var (player, _, _) = Show();
        static MediaPlayerBars Bars(MediaPlayerTrackRow row) => row.GetVisualDescendants().OfType<MediaPlayerBars>().Single();
        Rows(player).Select(r => Bars(r).IsVisible).ShouldBe([false, false, false]);
        player.Play();
        Dispatcher.UIThread.RunJobs();
        var rows = Rows(player);
        Bars(rows[0]).IsVisible.ShouldBeTrue();
        Bars(rows[1]).IsVisible.ShouldBeFalse();
        // Motion is off in tests, so the bars rest at 60% instead of looping.
        Bars(rows[0]).IsLooping.ShouldBeFalse();
    }

    [AvaloniaFact]
    public void Bars_Sweep_Between_Thirty_Percent_And_Full_Height_And_Back()
    {
        MediaPlayerBars.ScaleAt(0, 0.9).ShouldBe(0.3, 1e-9);
        MediaPlayerBars.ScaleAt(0.9, 0.9).ShouldBe(1, 1e-9);
        MediaPlayerBars.ScaleAt(0.45, 0.9).ShouldBe(0.65, 1e-6);
        MediaPlayerBars.ScaleAt(1.8, 0.9).ShouldBe(0.3, 1e-9);
        MediaPlayerBars.ScaleAt(1.35, 0.9).ShouldBe(0.65, 1e-6);
    }

    [AvaloniaFact]
    public void Times_Are_Whole_Seconds()
    {
        MediaPlayerTime.Format(TimeSpan.FromSeconds(23.9)).ShouldBe("0:23");
        MediaPlayerTime.Format(TimeSpan.FromSeconds(61)).ShouldBe("1:01");
        MediaPlayerTime.Format(TimeSpan.FromSeconds(3725)).ShouldBe("1:02:05");
        MediaPlayerTime.Format(TimeSpan.FromSeconds(-3)).ShouldBe("0:00");
    }

    [AvaloniaFact]
    public void Tracks_Without_Audio_Stay_Paused()
    {
        var backend = new FakeBackend();
        var player = new MediaPlayer { AudioBackend = backend, Width = 520 };
        player.Tracks.Add(new MediaTrack { Title = "Silent", Duration = TimeSpan.FromSeconds(10) });
        TestHelpers.Show(player);
        player.Play();
        Dispatcher.UIThread.RunJobs();
        player.IsPlaying.ShouldBeFalse();
    }

    [AvaloniaFact]
    public void An_Empty_Playlist_Selects_Nothing()
    {
        var player = new MediaPlayer { AudioBackend = new FakeBackend() };
        TestHelpers.Show(player);
        player.SelectedIndex.ShouldBe(-1);
        player.SelectedTrack.ShouldBeNull();
        player.NextTrack();
        player.SelectedIndex.ShouldBe(-1);
        player.Tracks.Add(new MediaTrack { Title = "First", Duration = TimeSpan.FromSeconds(75) });
        Dispatcher.UIThread.RunJobs();
        player.SelectedIndex.ShouldBe(0);
        player.SelectedTrack!.Title.ShouldBe("First");
        player.Duration.ShouldBe(TimeSpan.FromSeconds(75));
        Part<TextBlock>(player, MediaPlayer.LengthPart).Text.ShouldBe("1:15");
        player.GetVisualDescendants().OfType<MediaPlayerHeading>().Single().TitleText.ShouldBe("First");
        Rows(player).Single().IsCurrent.ShouldBeTrue();
    }

    [AvaloniaFact]
    public void Leaving_The_Window_Releases_The_Backend_Player()
    {
        var (player, backend, window) = Show();
        player.Play();
        var first = backend.Last;
        window.Content = null;
        Dispatcher.UIThread.RunJobs();
        first.IsDisposed.ShouldBeTrue();
        player.IsPlaying.ShouldBeFalse();
    }

    [AvaloniaFact]
    public void The_Simulated_Backend_Plays_Silently()
    {
        var player = new MediaPlayer { AudioBackend = SimulatedAudioBackend.Instance, Width = 520 };
        player.Tracks.Add(Track("Harbor lights", 2));
        TestHelpers.Show(player);
        player.IsAudible.ShouldBeFalse();
        player.Duration.ShouldBe(TimeSpan.FromSeconds(2));
        player.Play();
        // The simulated player reports from a thread-pool timer every 250ms; on a loaded runner that report can take
        // seconds to arrive, so wait with PumpUntil's default bound (it returns as soon as the report lands).
        TestHelpers.PumpUntil(() => player.Position > TimeSpan.Zero).ShouldBeTrue();
        TestHelpers.PumpUntil(() => !player.IsPlaying).ShouldBeTrue();
        player.Position.ShouldBe(TimeSpan.Zero);
    }

    /// <summary>A backend whose players do exactly what they are told and let the test end or fail a track.</summary>
    private sealed class FakeBackend : IAudioBackend
    {
        public List<FakePlayer> Players { get; } = [];

        public FakePlayer Last => Players[^1];

        public string Name => "fake";

        public bool IsAudible => true;

        public IAudioPlayer CreatePlayer()
        {
            var player = new FakePlayer();
            Players.Add(player);
            return player;
        }
    }

    private sealed class FakePlayer : AudioPlayerBase
    {
        private TimeSpan _position;

        public int Loaded { get; private set; }

        public new bool IsDisposed => base.IsDisposed;

        public override TimeSpan Position => _position;

        public override Task LoadAsync(AudioSource source, CancellationToken cancellationToken = default)
        {
            Loaded++;
            _position = TimeSpan.Zero;
            var length = source is BytesAudioSource bytes ? WavFile.TryGetDuration(bytes.Data.Span) : null;
            SetDuration(length ?? TimeSpan.FromSeconds(30));
            SetState(AudioPlaybackState.Paused);
            return Task.CompletedTask;
        }

        public override void Play()
        {
            if (State is AudioPlaybackState.Paused or AudioPlaybackState.Ended)
            {
                SetState(AudioPlaybackState.Playing);
            }
        }

        public override void Pause()
        {
            if (State == AudioPlaybackState.Playing)
            {
                SetState(AudioPlaybackState.Paused);
            }
        }

        public override void Seek(TimeSpan position)
        {
            _position = position;
            if (State == AudioPlaybackState.Ended)
            {
                SetState(AudioPlaybackState.Paused);
            }
            RaisePositionChanged();
        }

        public void FinishTrack()
        {
            _position = Duration;
            SetState(AudioPlaybackState.Ended);
        }

        public void SetLength(TimeSpan length) => SetDuration(length);

        public void FailNow(string message) => Fail(message);
    }
}
