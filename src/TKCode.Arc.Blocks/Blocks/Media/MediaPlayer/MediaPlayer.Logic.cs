using System.Diagnostics;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using TKCode.Arc.Motion;

namespace TKCode.Arc;

public partial class MediaPlayer
{
    private MediaPlayerSwap? _cover;
    private MediaPlayerSwap? _title;
    private MediaPlayerSwap? _playIcon;
    private MediaPlayerButton? _previous;
    private MediaPlayerButton? _play;
    private MediaPlayerButton? _next;
    private MediaPlayerRange? _seek;
    private TextBlock? _elapsed;
    private TextBlock? _length;
    private MediaPlayerButton? _mute;
    private MediaPlayerGlyph? _muteGlyph;
    private MediaPlayerRange? _volume;
    private TextBlock? _playlistTitle;
    private MediaPlayerQueue? _queue;

    private readonly MediaPlayerGlyph _playGlyph = new() { IsFilled = true, StrokeThickness = 2 };
    private readonly MediaPlayerGlyph _pauseGlyph = new() { IsFilled = true, StrokeThickness = 2 };

    private IAudioPlayer? _player;
    private MediaTrack? _loadedTrack;
    private CancellationTokenSource? _loadCancellation;
    private bool _isLoaded;
    private bool _syncing;
    private bool _seeking;
    private double _rememberedVolume = -1;
    private TimeSpan _reported;
    private long _reportedAt;
    private long _shownSecond = -1;
    private LiveLoop? _clock;
    private MediaTrackChangeSource _pendingSource = MediaTrackChangeSource.Code;

    #region Commands

    /// <summary>Starts or resumes the current track.</summary>
    public void Play() => SetCurrentValue(IsPlayingProperty, true);

    /// <summary>Pauses the current track.</summary>
    public void Pause() => SetCurrentValue(IsPlayingProperty, false);

    /// <summary>Plays when paused and pauses when playing.</summary>
    public void TogglePlay() => SetCurrentValue(IsPlayingProperty, !IsPlaying);

    /// <summary>Moves to the next track (after the last comes the first), playing it if the player was playing.</summary>
    public void NextTrack() => Skip(1, MediaTrackChangeSource.Code);

    /// <summary>Moves to the previous track (before the first comes the last), playing it if the player was playing.</summary>
    public void PreviousTrack() => Skip(-1, MediaTrackChangeSource.Code);

    /// <summary>Makes <paramref name="index"/> the current track and plays it from the start; on the current track it plays or pauses.</summary>
    public void PlayTrack(int index) => PlayTrack(index, MediaTrackChangeSource.Code);

    /// <summary>Moves the current track to <paramref name="position"/> (clamped to its length).</summary>
    public void Seek(TimeSpan position)
    {
        var length = Duration;
        if (position < TimeSpan.Zero)
        {
            position = TimeSpan.Zero;
        }
        if (length > TimeSpan.Zero && position > length)
        {
            position = length;
        }
        if (_isLoaded)
        {
            _player?.Seek(position);
        }
        Report(position);
    }

    /// <summary>Sets the volume to zero, remembering it; when already zero, restores the remembered volume (or <see cref="DefaultVolume"/>).</summary>
    public void ToggleMute()
    {
        if (Volume > 0)
        {
            _rememberedVolume = Volume;
            SetCurrentValue(VolumeProperty, 0.0);
            return;
        }
        SetCurrentValue(VolumeProperty, _rememberedVolume > 0 ? _rememberedVolume : DefaultVolume);
    }

    #endregion

    #region Template

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        Unhook();
        _cover = e.NameScope.Find<MediaPlayerSwap>(CoverPart);
        _title = e.NameScope.Find<MediaPlayerSwap>(TitlePart);
        _playIcon = e.NameScope.Find<MediaPlayerSwap>(PlayIconPart);
        _previous = e.NameScope.Find<MediaPlayerButton>(PreviousPart);
        _play = e.NameScope.Find<MediaPlayerButton>(PlayPart);
        _next = e.NameScope.Find<MediaPlayerButton>(NextPart);
        _seek = e.NameScope.Find<MediaPlayerRange>(SeekPart);
        _elapsed = e.NameScope.Find<TextBlock>(ElapsedPart);
        _length = e.NameScope.Find<TextBlock>(LengthPart);
        _mute = e.NameScope.Find<MediaPlayerButton>(MutePart);
        _muteGlyph = e.NameScope.Find<MediaPlayerGlyph>(MuteGlyphPart);
        _volume = e.NameScope.Find<MediaPlayerRange>(VolumePart);
        _playlistTitle = e.NameScope.Find<TextBlock>(PlaylistTitlePart);
        _queue = e.NameScope.Find<MediaPlayerQueue>(QueuePart);
        if (_previous is not null)
        {
            _previous.Click += OnPreviousClick;
        }
        if (_play is not null)
        {
            _play.Click += OnPlayClick;
        }
        if (_next is not null)
        {
            _next.Click += OnNextClick;
        }
        if (_mute is not null)
        {
            _mute.Click += OnMuteClick;
        }
        if (_seek is not null)
        {
            _seek.UserChanged += OnSeekChanged;
            _seek.DraggingChanged += OnSeekDragging;
        }
        if (_volume is not null)
        {
            _volume.UserChanged += OnVolumeChanged;
        }
        _playGlyph.Data = MediaPlayerGlyphs.Play;
        _pauseGlyph.Data = MediaPlayerGlyphs.Pause;
        if (_playIcon is { Width: > 0 and var size })
        {
            _playGlyph.Size = _pauseGlyph.Size = size;
        }
        if (_playIcon is not null)
        {
            // The glyphs move between swaps of a new template, so release them from the old one first.
            (_playGlyph.Parent as Panel)?.Children.Remove(_playGlyph);
            (_pauseGlyph.Parent as Panel)?.Children.Remove(_pauseGlyph);
        }
        RebuildRows();
        ShowTrack(animate: false);
        UpdatePlayState(animate: false);
        UpdateVolume();
        UpdateTimes(force: true);
        UpdateTexts();
    }

    private void Unhook()
    {
        if (_previous is not null)
        {
            _previous.Click -= OnPreviousClick;
        }
        if (_play is not null)
        {
            _play.Click -= OnPlayClick;
        }
        if (_next is not null)
        {
            _next.Click -= OnNextClick;
        }
        if (_mute is not null)
        {
            _mute.Click -= OnMuteClick;
        }
        if (_seek is not null)
        {
            _seek.UserChanged -= OnSeekChanged;
            _seek.DraggingChanged -= OnSeekDragging;
        }
        if (_volume is not null)
        {
            _volume.UserChanged -= OnVolumeChanged;
        }
        if (_queue is not null)
        {
            foreach (var row in _queue.Rows.ToList())
            {
                row.Click -= OnRowClick;
                _queue.Children.Remove(row);
            }
        }
    }

    #endregion

    #region Lifecycle

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _clock ??= new LiveLoop(this, OnClockFrame);
        EnsurePlayer();
        LoadCurrent();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _clock?.Stop();
        ReleasePlayer();
        if (IsPlaying)
        {
            _syncing = true;
            SetCurrentValue(IsPlayingProperty, false);
            _syncing = false;
        }
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == SelectedIndexProperty)
        {
            OnSelectedIndexChanged(change.GetOldValue<int>());
        }
        else if (change.Property == IsPlayingProperty)
        {
            OnIsPlayingChanged(change.GetNewValue<bool>());
        }
        else if (change.Property == VolumeProperty)
        {
            UpdateVolume();
        }
        else if (change.Property == IsFramedProperty)
        {
            PseudoClasses.Set(FramedPseudoClass, IsFramed);
        }
        else if (change.Property == LabelsProperty)
        {
            UpdateTexts();
            UpdateTimes(force: true);
            UpdateRows();
        }
        else if (change.Property == AudioBackendProperty)
        {
            if (this.IsAttachedToVisualTree())
            {
                ReleasePlayer();
                EnsurePlayer();
                LoadCurrent();
            }
        }
        else if (change.Property == BoundsProperty || change.Property == CompactWidthProperty)
        {
            var compact = Bounds.Width > 0 && Bounds.Width < CompactWidth;
            if (compact != IsCompact)
            {
                IsCompact = compact;
                PseudoClasses.Set(CompactPseudoClass, compact);
                if (_title is not null)
                {
                    foreach (var heading in _title.Children.OfType<MediaPlayerHeading>())
                    {
                        heading.IsCompact = compact;
                    }
                }
            }
        }
    }

    protected override void OnInitialized()
    {
        base.OnInitialized();
        PseudoClasses.Set(FramedPseudoClass, IsFramed);
        CoerceSelection();
        SyncSelection();
    }

    #endregion

    #region Tracks and selection

    private void OnTracksChanged()
    {
        var track = _selectedTrack;
        // The current track may have moved within the list; stay on it while it is still there.
        if (track is not null && Tracks.IndexOf(track) is var index and >= 0 && index != SelectedIndex)
        {
            SetCurrentValue(SelectedIndexProperty, index);
        }
        else
        {
            CoerceSelection();
        }
        RebuildRows();
        SyncSelection();
    }

    /// <summary>Keeps <see cref="SelectedIndex"/> inside the list (−1 for an empty list, else 0 to the last index).</summary>
    private void CoerceSelection()
    {
        var count = Tracks.Count;
        var index = SelectedIndex;
        var coerced = count == 0 ? -1 : Math.Clamp(index, 0, count - 1);
        if (coerced != index)
        {
            SetCurrentValue(SelectedIndexProperty, coerced);
        }
    }

    private MediaTrack? TrackAt(int index) => index >= 0 && index < Tracks.Count ? Tracks[index] : null;

    private void OnSelectedIndexChanged(int oldIndex)
    {
        var count = Tracks.Count;
        var index = SelectedIndex;
        if (count == 0 ? index != -1 : index < 0 || index >= count)
        {
            CoerceSelection();
            return;
        }
        SyncSelection();
    }

    /// <summary>Moves the player to the track at <see cref="SelectedIndex"/> when it is not the current one already.</summary>
    private void SyncSelection()
    {
        var old = _selectedTrack;
        var next = TrackAt(SelectedIndex);
        if (ReferenceEquals(old, next))
        {
            return;
        }
        SelectedTrack = next;
        var source = _pendingSource;
        _pendingSource = MediaTrackChangeSource.Code;
        // A new track always starts from the beginning, playing or paused as the player was.
        Report(TimeSpan.Zero);
        Duration = next?.Duration ?? TimeSpan.Zero;
        LoadCurrent();
        // The first track a player gets appears in place; later changes animate.
        ShowTrack(animate: old is not null);
        UpdateRows();
        UpdateTexts();
        UpdateTimes(force: true);
        if (next is null && IsPlaying)
        {
            Pause();
        }
        RaiseEvent(new MediaTrackChangedEventArgs(SelectedTrackChangedEvent, old, next, source));
    }

    private void Skip(int delta, MediaTrackChangeSource source)
    {
        var count = Tracks.Count;
        if (count == 0)
        {
            return;
        }
        var index = ((SelectedIndex + delta) % count + count) % count;
        if (index == SelectedIndex)
        {
            // A single track: skipping restarts it.
            Seek(TimeSpan.Zero);
            return;
        }
        _pendingSource = source;
        SetCurrentValue(SelectedIndexProperty, index);
    }

    private void PlayTrack(int index, MediaTrackChangeSource source)
    {
        if (index < 0 || index >= Tracks.Count)
        {
            return;
        }
        if (index == SelectedIndex)
        {
            TogglePlay();
            return;
        }
        _pendingSource = source;
        SetCurrentValue(SelectedIndexProperty, index);
        Play();
    }

    #endregion

    #region Playback

    private void EnsurePlayer()
    {
        if (_player is not null)
        {
            return;
        }
        var backend = AudioBackend ?? ArcAudio.Backend;
        IsAudible = backend.IsAudible;
        _player = backend.CreatePlayer();
        _player.StateChanged += OnPlayerStateChanged;
        _player.PositionChanged += OnPlayerPositionChanged;
        _player.DurationChanged += OnPlayerDurationChanged;
        _player.Failed += OnPlayerFailed;
        _player.Volume = Volume;
    }

    private void ReleasePlayer()
    {
        _loadCancellation?.Cancel();
        _loadCancellation = null;
        if (_player is { } player)
        {
            player.StateChanged -= OnPlayerStateChanged;
            player.PositionChanged -= OnPlayerPositionChanged;
            player.DurationChanged -= OnPlayerDurationChanged;
            player.Failed -= OnPlayerFailed;
            player.Dispose();
        }
        _player = null;
        _loadedTrack = null;
        _isLoaded = false;
    }

    private void LoadCurrent()
    {
        var track = _selectedTrack;
        if (_player is null || ReferenceEquals(track, _loadedTrack))
        {
            return;
        }
        _loadCancellation?.Cancel();
        _loadCancellation = null;
        _loadedTrack = track;
        _isLoaded = false;
        if (track?.Source is not { } source)
        {
            return;
        }
        var cancellation = new CancellationTokenSource();
        _loadCancellation = cancellation;
        _ = LoadAsync(_player, track, source, cancellation.Token);
    }

    private async Task LoadAsync(IAudioPlayer player, MediaTrack track, AudioSource source, CancellationToken cancellation)
    {
        try
        {
            await player.LoadAsync(source, cancellation);
        }
        catch (OperationCanceledException)
        {
            return;
        }
        catch (Exception exception)
        {
            if (!cancellation.IsCancellationRequested && ReferenceEquals(player, _player))
            {
                OnLoadFailed(track, exception.Message);
            }
            return;
        }
        if (cancellation.IsCancellationRequested || !ReferenceEquals(player, _player) || !ReferenceEquals(track, _loadedTrack))
        {
            return;
        }
        _isLoaded = true;
        player.Volume = Volume;
        if (player.Duration > TimeSpan.Zero)
        {
            ApplyDuration(player.Duration);
        }
        if (_reported > TimeSpan.Zero)
        {
            player.Seek(_reported);
        }
        if (IsPlaying)
        {
            player.Play();
        }
    }

    private void OnLoadFailed(MediaTrack track, string message)
    {
        _syncing = true;
        SetCurrentValue(IsPlayingProperty, false);
        _syncing = false;
        UpdatePlayState(animate: true);
        RaiseEvent(new MediaTrackEventArgs(PlaybackFailedEvent, track, message));
    }

    private void OnIsPlayingChanged(bool playing)
    {
        if (!_syncing && _player is { } player && _isLoaded)
        {
            if (playing)
            {
                player.Play();
            }
            else
            {
                player.Pause();
            }
        }
        if (!_syncing && playing && _selectedTrack?.Source is null)
        {
            // Nothing to play: stay paused.
            Dispatcher.UIThread.Post(() =>
            {
                if (IsPlaying && _selectedTrack?.Source is null)
                {
                    _syncing = true;
                    SetCurrentValue(IsPlayingProperty, false);
                    _syncing = false;
                    UpdatePlayState(animate: true);
                }
            });
        }
        if (playing)
        {
            _reportedAt = Stopwatch.GetTimestamp();
        }
        UpdatePlayState(animate: true);
    }

    private void OnPlayerStateChanged(object? sender, EventArgs e)
    {
        if (!ReferenceEquals(sender, _player) || _player is not { } player)
        {
            return;
        }
        switch (player.State)
        {
            case AudioPlaybackState.Playing:
                SyncPlaying(true);
                UpdateClock();
                break;
            case AudioPlaybackState.Paused when _isLoaded:
                SyncPlaying(false);
                UpdateClock();
                break;
            case AudioPlaybackState.Ended:
                // Arc's player stops at the end and returns to the start of the same track.
                player.Seek(TimeSpan.Zero);
                Report(TimeSpan.Zero);
                SyncPlaying(false);
                if (_selectedTrack is { } track)
                {
                    RaiseEvent(new MediaTrackEventArgs(TrackEndedEvent, track));
                }
                break;
        }
    }

    private void SyncPlaying(bool playing)
    {
        if (IsPlaying == playing)
        {
            return;
        }
        _syncing = true;
        SetCurrentValue(IsPlayingProperty, playing);
        _syncing = false;
        UpdatePlayState(animate: true);
    }

    private void OnPlayerPositionChanged(object? sender, EventArgs e)
    {
        if (ReferenceEquals(sender, _player) && _player is { } player && _isLoaded && !_seeking)
        {
            Report(player.Position);
        }
    }

    private void OnPlayerDurationChanged(object? sender, EventArgs e)
    {
        if (ReferenceEquals(sender, _player) && _player is { } player && player.Duration > TimeSpan.Zero && _isLoaded)
        {
            ApplyDuration(player.Duration);
        }
    }

    private void OnPlayerFailed(object? sender, AudioErrorEventArgs e)
    {
        if (ReferenceEquals(sender, _player) && _selectedTrack is { } track)
        {
            OnLoadFailed(track, e.Message);
        }
    }

    private void ApplyDuration(TimeSpan duration)
    {
        Duration = duration;
        if (_loadedTrack is { } track && track.Duration != duration)
        {
            track.SetCurrentValue(MediaTrack.DurationProperty, duration);
        }
        UpdateRows();
        UpdateTimes(force: true);
    }

    /// <summary>Records a position the backend (or a seek) reported; the clock interpolates from it while playing.</summary>
    private void Report(TimeSpan position)
    {
        _reported = position;
        _reportedAt = Stopwatch.GetTimestamp();
        Position = position;
        UpdateTimes(force: false);
    }

    private TimeSpan ShownPosition
    {
        get
        {
            if (!IsPlaying || !_isLoaded || _seeking || _player?.State != AudioPlaybackState.Playing)
            {
                return _reported;
            }
            var shown = _reported + Stopwatch.GetElapsedTime(_reportedAt);
            var length = Duration;
            return length > TimeSpan.Zero && shown > length ? length : shown;
        }
    }

    private bool OnClockFrame(double elapsed)
    {
        UpdateTimes(force: false);
        return IsPlaying;
    }

    #endregion

    #region Input

    private void OnPreviousClick(object? sender, RoutedEventArgs e) => Skip(-1, MediaTrackChangeSource.Skip);

    private void OnNextClick(object? sender, RoutedEventArgs e) => Skip(1, MediaTrackChangeSource.Skip);

    private void OnPlayClick(object? sender, RoutedEventArgs e) => TogglePlay();

    private void OnMuteClick(object? sender, RoutedEventArgs e) => ToggleMute();

    private void OnRowClick(object? sender, RoutedEventArgs e)
    {
        if (sender is MediaPlayerTrackRow { Track: { } track })
        {
            PlayTrack(Tracks.IndexOf(track), MediaTrackChangeSource.Playlist);
        }
    }

    private void OnSeekChanged(double seconds) => Seek(TimeSpan.FromSeconds(seconds));

    private void OnSeekDragging(bool dragging)
    {
        _seeking = dragging;
        if (!dragging)
        {
            _reportedAt = Stopwatch.GetTimestamp();
        }
    }

    private void OnVolumeChanged(double volume) => SetCurrentValue(VolumeProperty, volume);

    #endregion

    #region Visual state

    private void ShowTrack(bool animate)
    {
        var track = _selectedTrack;
        if (_cover is not null)
        {
            _cover.Show(track is null ? null : new MediaPlayerCover(track), animate);
        }
        if (_title is not null)
        {
            _title.Show(track is null ? null : new MediaPlayerHeading { Track = track, IsCompact = IsCompact }, animate);
        }
        _queue?.SetCurrent(SelectedIndex, animate);
    }

    private void UpdatePlayState(bool animate)
    {
        var playing = IsPlaying;
        PseudoClasses.Set(PlayingPseudoClass, playing);
        _playIcon?.Show(playing ? _pauseGlyph : _playGlyph, animate);
        if (_play is not null)
        {
            AutomationProperties.SetName(_play, playing ? Labels.Pause : Labels.Play);
        }
        UpdateClock();
        UpdateRows();
    }

    /// <summary>Runs the clock that moves the seek bar between the backend's reports, only while audio is actually playing.</summary>
    private void UpdateClock()
    {
        if (IsPlaying && _isLoaded && _player?.State == AudioPlaybackState.Playing)
        {
            if (_clock is { IsActive: false })
            {
                // Playback (re)starts from the last report, so time is counted from now.
                _reportedAt = Stopwatch.GetTimestamp();
                _clock.Start();
            }
        }
        else
        {
            _clock?.Stop();
        }
    }

    private void UpdateVolume()
    {
        var volume = Volume;
        if (_player is not null)
        {
            _player.Volume = volume;
        }
        var muted = volume <= 0;
        IsMuted = muted;
        PseudoClasses.Set(MutedPseudoClass, muted);
        if (_volume is not null)
        {
            _volume.SetCurrentValue(RangeBase.ValueProperty, volume);
            AutomationProperties.SetItemStatus(_volume, Labels.VolumeValue((int)Math.Round(volume * 100)));
        }
        if (_muteGlyph is not null)
        {
            _muteGlyph.Data = muted ? MediaPlayerGlyphs.VolumeOff : MediaPlayerGlyphs.Volume;
        }
        if (_mute is not null)
        {
            AutomationProperties.SetName(_mute, muted ? Labels.Unmute : Labels.Mute);
        }
    }

    private void UpdateTexts()
    {
        var labels = Labels;
        if (AutomationProperties.GetName(this) is null or "")
        {
            AutomationProperties.SetName(this, labels.PlayerName);
        }
        if (_previous is not null)
        {
            AutomationProperties.SetName(_previous, labels.PreviousTrack);
        }
        if (_next is not null)
        {
            AutomationProperties.SetName(_next, labels.NextTrack);
        }
        if (_play is not null)
        {
            AutomationProperties.SetName(_play, IsPlaying ? labels.Pause : labels.Play);
        }
        if (_volume is not null)
        {
            AutomationProperties.SetName(_volume, labels.Volume);
        }
        if (_mute is not null)
        {
            AutomationProperties.SetName(_mute, IsMuted ? labels.Unmute : labels.Mute);
        }
        if (_seek is not null)
        {
            AutomationProperties.SetName(_seek, labels.Seek(_selectedTrack?.Title ?? string.Empty));
        }
        if (_playlistTitle is not null)
        {
            _playlistTitle.Text = labels.Playlist;
        }
    }

    /// <summary>Moves the seek bar and, when the whole second changed (or <paramref name="force"/>), the time labels.</summary>
    private void UpdateTimes(bool force)
    {
        var length = Duration;
        var shown = ShownPosition;
        if (_seek is not null)
        {
            var max = Math.Max(0, length.TotalSeconds);
            if (_seek.Maximum != max)
            {
                _seek.Maximum = max;
            }
            if (!_seeking)
            {
                _seek.SetCurrentValue(RangeBase.ValueProperty, Math.Clamp(shown.TotalSeconds, 0, max));
            }
        }
        var second = (long)Math.Floor(Math.Max(0, (_seeking && _seek is not null ? _seek.Value : shown.TotalSeconds)));
        if (!force && second == _shownSecond)
        {
            return;
        }
        _shownSecond = second;
        var labels = Labels;
        var elapsed = labels.FormatTime(TimeSpan.FromSeconds(second));
        var total = labels.FormatTime(length);
        if (_elapsed is not null)
        {
            _elapsed.Text = elapsed;
        }
        if (_length is not null)
        {
            _length.Text = total;
        }
        if (_seek is not null)
        {
            AutomationProperties.SetItemStatus(_seek, labels.SeekValue(elapsed, total));
        }
    }

    private void RebuildRows()
    {
        if (_queue is null)
        {
            return;
        }
        foreach (var row in _queue.Rows.ToList())
        {
            row.Click -= OnRowClick;
            _queue.Children.Remove(row);
        }
        var index = 0;
        foreach (var track in Tracks)
        {
            var row = new MediaPlayerTrackRow { Track = track, IsSeparated = index > 0 };
            row.Click += OnRowClick;
            _queue.Children.Add(row);
            index++;
        }
        UpdateRows();
        _queue.SetCurrent(SelectedIndex, animate: false);
    }

    private void UpdateRows()
    {
        if (_queue is null)
        {
            return;
        }
        var labels = Labels;
        var current = _selectedTrack;
        foreach (var row in _queue.Rows)
        {
            var isCurrent = ReferenceEquals(row.Track, current);
            var pauses = isCurrent && IsPlaying;
            row.IsCurrent = isCurrent;
            row.TitleText = row.Track?.Title;
            row.IsPlaying = pauses;
            row.LengthText = labels.FormatTime(row.Track?.Duration ?? TimeSpan.Zero);
            row.PlayingText = labels.Playing;
            AutomationProperties.SetName(row, labels.TrackAction(row.Track?.Title ?? string.Empty, pauses));
        }
    }

    #endregion
}
