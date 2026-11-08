using System.Globalization;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using TKCode.Arc.Localization;
using TKCode.Arc.Motion;
using AvaloniaButton = Avalonia.Controls.Button;

namespace TKCode.Arc;

public partial class VideoPlayer
{
    private const string HideKey = "hide";
    private const string ClickKey = "click";
    private const string TapKey = "tap";
    private const string TapStreakKey = "tapstreak";
    private const string SpinnerKey = "spinner";
    private const string BufferingKey = "buffering";
    private const string ScrubKey = "scrub";

    private static readonly TimeSpan s_clickWait = TimeSpan.FromMilliseconds(200);
    private static readonly TimeSpan s_spinnerDelay = TimeSpan.FromMilliseconds(300);
    private static readonly TimeSpan s_bufferingAnnounceDelay = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan s_scrubInterval = TimeSpan.FromMilliseconds(100);
    private static readonly TimeSpan s_tapStreak = TimeSpan.FromMilliseconds(600);
    private static readonly TimeSpan s_jump = TimeSpan.FromSeconds(10);

    // Template parts.
    private VideoPlayerFrame? _frame;
    private Border? _root;
    private VideoSurface? _surface;
    private VideoCaptionLayer? _captions;
    private VideoStateLayer? _stateLayer;
    private VideoControlBar? _controls;
    private VideoSeekBar? _seek;
    private Border? _seekTip;
    private TextBlock? _seekTipText;
    private Canvas? _seekTipHost;
    private VideoToggleButton? _play;
    private VideoSwap? _playSwap;
    private VideoVolume? _volume;
    private TextBlock? _time;
    private VideoMenuButton? _captionsButton;
    private VideoMenuButton? _speedButton;
    private VideoToggleButton? _fullScreenButton;
    private VideoGlyph? _fullScreenGlyph;
    private TextBlock? _announcer;
    private Border? _focusRing;
    private VideoScrim? _titleBar;
    private readonly VideoGlyph _playGlyph = new() { Data = VideoGlyphs.Play, IsFilled = true, StrokeThickness = 2 };
    private readonly VideoGlyph _pauseGlyph = new() { Data = VideoGlyphs.Pause, IsFilled = true, StrokeThickness = 2 };

    // Playback.
    private CancellationTokenSource? _loadCancel;
    private int _loadGeneration;
    private bool _attached;
    private bool _isLoaded;
    private bool _hasStarted;
    private TimeSpan? _resumePosition;
    private bool _syncing;
    private bool _scrubbing;
    private bool _scrubWasPlaying;
    private long _lastScrubSeek = long.MinValue;
    private TimeSpan _reported;
    private long _reportedAt;
    private long _shownSecond = -1;
    private bool _errorHandled;
    private bool _spinnerVisible;
    private LiveLoop? _clockLoop;
    private VideoTextTrack? _chapterTrack;
    private VideoTextTrack? _observedCaptions;
    private bool _captionsChosen;
    private string? _lastAnnouncement;
    private ArcLanguageManager? _languages;
    private TimeProvider? _clockOverride;
    private VideoDeadlines? _deadlines;

    // Control bar.
    private bool _controlsVisible;
    private bool _active;
    private int _streakSide;
    private int _streakSeconds;
    private static Cursor? s_noCursor;

    #region Test and internal hooks

    /// <summary>The clock for idle hiding, click timing and position interpolation: a fake one in tests, else the simulated backend's, else the system clock.</summary>
    internal TimeProvider Clock
    {
        get => _clockOverride ?? (ArcVideo.Backend as SimulatedVideoBackend)?.TimeProvider ?? TimeProvider.System;
        set => _clockOverride = value;
    }

    private VideoDeadlines Deadlines => _deadlines ??= new VideoDeadlines(() => Clock);

    /// <summary>Runs the timed reactions that are due on <see cref="Clock"/> (tests advance a fake clock, then call this).</summary>
    internal void RunDueTimers() => Deadlines.RunDue();

    /// <summary>Whether the control bar is showing.</summary>
    internal bool AreControlsVisible => _controlsVisible;

    /// <summary>The last polite announcement for screen readers.</summary>
    internal string? LastAnnouncement => _lastAnnouncement;

    /// <summary>The root that moves into the overlay in full screen.</summary>
    internal Border? Root => _root;

    internal VideoSeekBar? SeekBar => _seek;

    internal VideoVolume? VolumeControl => _volume;

    internal VideoStateLayer? StateLayer => _stateLayer;

    internal VideoMenuButton? CaptionsButton => _captionsButton;

    internal VideoMenuButton? SpeedButton => _speedButton;

    internal VideoToggleButton? PlayButton => _play;

    internal VideoToggleButton? FullScreenButton => _fullScreenButton;

    internal VideoPlayerFrame? Frame => _frame;

    internal VideoCaptionLayer? CaptionLayer => _captions;

    /// <summary>The seek tip's text while it shows, else null.</summary>
    internal string? SeekTipText => _seekTip is { IsVisible: true } ? _seekTipText?.Text : null;

    #endregion

    #region Public methods

    /// <summary>Plays (sets <see cref="IsPlaying"/>); from the start when the video has ended.</summary>
    public void Play()
    {
        if (State == VideoPlaybackState.Ended && IsPlaying)
        {
            _player?.Play();
            return;
        }
        SetCurrentValue(IsPlayingProperty, true);
    }

    /// <summary>Pauses (clears <see cref="IsPlaying"/>).</summary>
    public void Pause() => SetCurrentValue(IsPlayingProperty, false);

    /// <summary>Plays when paused, pauses when playing; replays when ended.</summary>
    public void TogglePlay()
    {
        if (State == VideoPlaybackState.Ended)
        {
            Play();
            return;
        }
        SetCurrentValue(IsPlayingProperty, !IsPlaying);
    }

    /// <summary>Moves to <paramref name="position"/>, clamped to the length.</summary>
    public void Seek(TimeSpan position)
    {
        if (position < TimeSpan.Zero)
        {
            position = TimeSpan.Zero;
        }
        if (Duration > TimeSpan.Zero && position > Duration)
        {
            position = Duration;
        }
        SetCurrentValue(PositionProperty, position);
    }

    /// <summary>Moves by <paramref name="delta"/> from the shown position.</summary>
    public void SeekBy(TimeSpan delta) => Seek(ShownPosition + delta);

    /// <summary>Fills the top level (or the screen, with <see cref="VideoFullScreenMode.System"/>).</summary>
    public void EnterFullScreen() => SetCurrentValue(IsFullScreenProperty, true);

    /// <summary>Returns from full screen.</summary>
    public void ExitFullScreen() => SetCurrentValue(IsFullScreenProperty, false);

    /// <summary>Shows the control bar now and restarts the idle countdown.</summary>
    public void ShowControlsNow() => Activity();

    private void ToggleFullScreen() => SetCurrentValue(IsFullScreenProperty, !IsFullScreen);

    #endregion

    #region Template

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        if (IsFullScreen)
        {
            LeaveFullScreen();
        }
        Unhook();
        base.OnApplyTemplate(e);
        var scope = e.NameScope;
        _frame = scope.Find<VideoPlayerFrame>(FramePart);
        _root = scope.Find<Border>(RootPart);
        _surface = scope.Find<VideoSurface>(SurfacePart);
        _captions = scope.Find<VideoCaptionLayer>(CaptionsPart);
        _stateLayer = scope.Find<VideoStateLayer>(StatePart);
        _controls = scope.Find<VideoControlBar>(ControlsPart);
        _seek = scope.Find<VideoSeekBar>(SeekPart);
        _seekTip = scope.Find<Border>(SeekTipPart);
        _seekTipText = _seekTip?.Child as TextBlock;
        _seekTipHost = _seekTip?.Parent as Canvas;
        _play = scope.Find<VideoToggleButton>(PlayPart);
        _volume = scope.Find<VideoVolume>(VolumePart);
        _time = scope.Find<TextBlock>(TimePart);
        _captionsButton = scope.Find<VideoMenuButton>(CaptionsButtonPart);
        _speedButton = scope.Find<VideoMenuButton>(SpeedButtonPart);
        _fullScreenButton = scope.Find<VideoToggleButton>(FullScreenButtonPart);
        _fullScreenGlyph = _fullScreenButton?.Content as VideoGlyph;
        _announcer = scope.Find<TextBlock>(AnnouncerPart);
        _focusRing = scope.Find<Border>(FocusRingPart);
        _titleBar = scope.Find<VideoScrim>("PART_TitleBar");
        _playSwap = _play?.Content as VideoSwap;

        if (_root is not null)
        {
            _root.AddHandler(PointerMovedEvent, OnRootPointerMoved, RoutingStrategies.Bubble, handledEventsToo: true);
            _root.AddHandler(PointerExitedEvent, OnRootPointerExited, RoutingStrategies.Direct);
            _root.AddHandler(KeyDownEvent, OnRootKeyDown, RoutingStrategies.Bubble);
            _root.GotFocus += OnRootFocusChanged;
            _root.LostFocus += OnRootFocusChanged;
            _root.PropertyChanged += OnRootPropertyChanged;
        }
        if (_stateLayer is not null)
        {
            _stateLayer.AddHandler(AvaloniaButton.ClickEvent, OnStateClick);
            _stateLayer.AddHandler(PointerPressedEvent, OnPicturePressed);
            _stateLayer.AddHandler(PointerReleasedEvent, OnPictureReleased);
        }
        if (_controls is not null)
        {
            _controls.PropertyChanged += OnControlsPropertyChanged;
        }
        if (_seek is not null)
        {
            _seek.UserChanged += OnSeekUserChanged;
            _seek.DraggingChanged += OnSeekDragging;
            _seek.HoverChanged += OnSeekHover;
            _seek.GotFocus += OnSeekFocus;
        }
        if (_play is not null)
        {
            _play.Click += OnPlayClick;
        }
        if (_volume is not null)
        {
            _volume.MuteButton.Click += OnMuteClick;
            _volume.Slider.UserChanged += OnVolumeUserChanged;
            _volume.WheelStep += OnVolumeWheel;
        }
        if (_fullScreenButton is not null)
        {
            _fullScreenButton.Click += OnFullScreenClick;
        }
        if (_captionsButton is not null)
        {
            _captionsButton.BuildItems = BuildCaptionsMenu;
            _captionsButton.MenuOpenChanged += OnMenuOpenChanged;
        }
        if (_speedButton is not null)
        {
            _speedButton.BuildItems = BuildSpeedMenu;
            _speedButton.MenuOpenChanged += OnMenuOpenChanged;
        }
        if (_captions is not null)
        {
            _captions.ActiveCuesChanged += OnActiveCuesChanged;
        }
        if (_surface is not null)
        {
            _surface.Player = _player;
        }
        _playSwap?.Show(IsPlaying ? _pauseGlyph : _playGlyph, animate: false);
        UpdateAspect();
        UpdateChrome();
        UpdateCaptions();
        UpdateChapters();
        UpdateStatePseudoClasses();
        UpdateTexts();
        UpdateVolume();
        UpdateTimes(force: true);
        UpdateStateLayer();
        UpdateControls();
        UpdateCompact();
        UpdateNativePresentation();
    }

    private void Unhook()
    {
        RemoveNativeView();
        if (_root is not null)
        {
            _root.RemoveHandler(PointerMovedEvent, OnRootPointerMoved);
            _root.RemoveHandler(PointerExitedEvent, OnRootPointerExited);
            _root.RemoveHandler(KeyDownEvent, OnRootKeyDown);
            _root.GotFocus -= OnRootFocusChanged;
            _root.LostFocus -= OnRootFocusChanged;
            _root.PropertyChanged -= OnRootPropertyChanged;
        }
        if (_stateLayer is not null)
        {
            _stateLayer.RemoveHandler(AvaloniaButton.ClickEvent, OnStateClick);
            _stateLayer.RemoveHandler(PointerPressedEvent, OnPicturePressed);
            _stateLayer.RemoveHandler(PointerReleasedEvent, OnPictureReleased);
        }
        if (_controls is not null)
        {
            _controls.PropertyChanged -= OnControlsPropertyChanged;
        }
        if (_seek is not null)
        {
            _seek.UserChanged -= OnSeekUserChanged;
            _seek.DraggingChanged -= OnSeekDragging;
            _seek.HoverChanged -= OnSeekHover;
            _seek.GotFocus -= OnSeekFocus;
        }
        if (_play is not null)
        {
            _play.Click -= OnPlayClick;
        }
        if (_volume is not null)
        {
            _volume.MuteButton.Click -= OnMuteClick;
            _volume.Slider.UserChanged -= OnVolumeUserChanged;
            _volume.WheelStep -= OnVolumeWheel;
        }
        if (_fullScreenButton is not null)
        {
            _fullScreenButton.Click -= OnFullScreenClick;
        }
        if (_captionsButton is not null)
        {
            _captionsButton.BuildItems = null;
            _captionsButton.MenuOpenChanged -= OnMenuOpenChanged;
        }
        if (_speedButton is not null)
        {
            _speedButton.BuildItems = null;
            _speedButton.MenuOpenChanged -= OnMenuOpenChanged;
        }
        if (_captions is not null)
        {
            _captions.ActiveCuesChanged -= OnActiveCuesChanged;
        }
        if (_surface is not null)
        {
            _surface.Player = null;
        }
        _playSwap?.Show(null, animate: false);
    }

    #endregion

    #region Lifecycle

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _attached = true;
        _clockLoop ??= new LiveLoop(this, OnClockFrame) { MinimumLevel = ArcMotionLevel.Off };
        _languages = VideoText.Languages;
        if (_languages is not null)
        {
            _languages.Changed += OnLanguageChanged;
        }
        UpdateTexts();
        EnsurePlayerAndLoad();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        if (IsFullScreen)
        {
            SetCurrentValue(IsFullScreenProperty, false);
        }
        base.OnDetachedFromVisualTree(e);
        _attached = false;
        if (_languages is not null)
        {
            _languages.Changed -= OnLanguageChanged;
            _languages = null;
        }
        // Remember where the video was; coming back reloads and seeks there without playing (spec section 19, question 5).
        if (_player is not null && _isLoaded && State != VideoPlaybackState.Failed)
        {
            var position = _player.Position;
            _resumePosition = position > TimeSpan.Zero ? position : null;
        }
        ReleasePlayer();
        SyncIsPlaying(false);
        Deadlines.Clear();
        _clockLoop?.Stop();
        _captionsButton?.CloseMenu();
        _speedButton?.CloseMenu();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        var property = change.Property;
        if (property == SourceProperty)
        {
            OnSourceChanged();
        }
        else if (property == IsPlayingProperty)
        {
            OnIsPlayingChanged(change.GetNewValue<bool>());
        }
        else if (property == PositionProperty)
        {
            if (!_syncing)
            {
                OnPositionWritten(change.GetNewValue<TimeSpan>());
            }
        }
        else if (property == VolumeProperty || property == IsMutedProperty)
        {
            if (_player is not null)
            {
                _player.Volume = Volume;
                _player.IsMuted = IsMuted;
            }
            UpdateVolume();
            if (property == IsMutedProperty && _isLoaded && AutoPlay && IsMuted && !IsPlaying && !_hasStarted)
            {
                ApplyAutoPlay();
            }
        }
        else if (property == IsLoopingProperty)
        {
            if (_player is not null)
            {
                _player.IsLooping = IsLooping;
            }
        }
        else if (property == PlaybackRateProperty)
        {
            if (_player is not null)
            {
                _player.PlaybackRate = PlaybackRate;
            }
            // Re-base the interpolation so the new rate applies from now.
            _reported = ShownPosition;
            _reportedAt = Clock.GetTimestamp();
            if (_attached && _isLoaded)
            {
                Announce(VideoText.Format(VideoLangResourceKind.SpeedAnnounce, VideoText.Rate(PlaybackRate)));
            }
        }
        else if (property == SelectedTextTrackProperty)
        {
            UpdateCaptions();
            if (_attached)
            {
                Announce(SelectedTextTrack is { } track
                    ? VideoText.Format(VideoLangResourceKind.CaptionsOnAnnounce, TrackLabel(track))
                    : VideoText.Get(VideoLangResourceKind.CaptionsOffAnnounce));
            }
        }
        else if (property == CaptionScaleProperty || property == CaptionBackgroundOpacityProperty)
        {
            UpdateCaptions();
        }
        else if (property == IsFullScreenProperty)
        {
            OnIsFullScreenChanged(change.GetNewValue<bool>());
        }
        else if (property == AspectRatioProperty)
        {
            UpdateAspect();
        }
        else if (property == PosterProperty)
        {
            UpdateStateLayer();
        }
        else if (property == ShowControlsProperty || property == ControlsHideDelayProperty)
        {
            UpdateControls();
        }
        else if (property == TitleProperty)
        {
            UpdateTexts();
            UpdateChrome();
        }
        else if (property == CornerRadiusProperty)
        {
            UpdateChrome();
        }
        else if (property == BoundsProperty || property == CompactWidthProperty)
        {
            UpdateCompact();
        }
        else if (property == BrowserPresentationProperty)
        {
            UpdateNativePresentation();
        }
        else if (property == SeekStepProperty)
        {
            if (_seek is not null)
            {
                _seek.SmallChange = SeekStep.TotalSeconds;
            }
        }
    }

    private void OnSourceChanged()
    {
        _resumePosition = null;
        _hasStarted = false;
        _captionsChosen = false;
        if (Source is null)
        {
            ReleasePlayer();
            UpdateStateLayer();
            UpdateControls();
            return;
        }
        EnsurePlayerAndLoad();
    }

    /// <summary>Creates the backend player when attached with a source, then loads the source (a source set earlier waits here for the attach).</summary>
    private void EnsurePlayerAndLoad()
    {
        if (!_attached || Source is null)
        {
            return;
        }
        if (_player is null)
        {
            var player = ArcVideo.Backend.CreatePlayer();
            player.Volume = Volume;
            player.IsMuted = IsMuted;
            player.IsLooping = IsLooping;
            player.PlaybackRate = PlaybackRate;
            player.StateChanged += OnPlayerStateChanged;
            player.PositionChanged += OnPlayerPositionChanged;
            player.DurationChanged += OnPlayerDurationChanged;
            player.NaturalSizeChanged += OnPlayerNaturalSizeChanged;
            player.BufferedChanged += OnPlayerBufferedChanged;
            player.Failed += OnPlayerFailed;
            Player = player;
            if (_surface is not null)
            {
                _surface.Player = player;
            }
            UpdateNativePresentation();
        }
        _ = LoadAsync();
    }

    private void ReleasePlayer()
    {
        _loadCancel?.Cancel();
        _loadCancel = null;
        _loadGeneration++;
        if (_player is { } player)
        {
            player.StateChanged -= OnPlayerStateChanged;
            player.PositionChanged -= OnPlayerPositionChanged;
            player.DurationChanged -= OnPlayerDurationChanged;
            player.NaturalSizeChanged -= OnPlayerNaturalSizeChanged;
            player.BufferedChanged -= OnPlayerBufferedChanged;
            player.Failed -= OnPlayerFailed;
            // The platform view belongs to the player: take it out before the player goes.
            RemoveNativeView();
            if (_surface is not null)
            {
                _surface.Player = null;
            }
            Player = null;
            player.Dispose();
        }
        _isLoaded = false;
        _scrubbing = false;
        Duration = TimeSpan.Zero;
        ErrorMessage = null;
        SetState(VideoPlaybackState.Idle);
        _clockLoop?.Stop();
    }

    private async Task LoadAsync()
    {
        if (_player is not { } player || Source is not { } source)
        {
            return;
        }
        _loadCancel?.Cancel();
        var cancel = new CancellationTokenSource();
        _loadCancel = cancel;
        var generation = ++_loadGeneration;
        _isLoaded = false;
        _errorHandled = false;
        ErrorMessage = null;
        Duration = TimeSpan.Zero;
        Report(_resumePosition ?? TimeSpan.Zero);
        SetState(VideoPlaybackState.Loading);
        try
        {
            await player.LoadAsync(source, cancel.Token);
        }
        catch (OperationCanceledException)
        {
            return;
        }
        catch (Exception exception)
        {
            if (generation == _loadGeneration && State != VideoPlaybackState.Failed)
            {
                OnFailure(KindOf(exception));
            }
            return;
        }
        if (generation != _loadGeneration || !ReferenceEquals(player, _player) || player.State == VideoPlaybackState.Failed)
        {
            return;
        }
        _isLoaded = true;
        Duration = player.Duration;
        UpdateAspect();
        if (_resumePosition is { } resume)
        {
            _resumePosition = null;
            _hasStarted = true;
            player.Seek(resume);
        }
        Report(player.Position);
        SetState(player.State);
        UpdateBuffered();
        if (IsPlaying)
        {
            player.Play();
        }
        else
        {
            ApplyAutoPlay();
        }
        UpdateTexts();
        UpdateStateLayer();
        UpdateControls();
    }

    private static VideoErrorKind KindOf(Exception exception) => exception switch
    {
        NotSupportedException => VideoErrorKind.Unsupported,
        IOException or HttpRequestException or UnauthorizedAccessException => VideoErrorKind.Network,
        _ => VideoErrorKind.Decode,
    };

    /// <summary>
    /// Starts a muted <see cref="AutoPlay"/> video (never one with sound, WCAG 1.4.2); a decorative muted loop does not start
    /// under reduced or no motion (spec section 8).
    /// </summary>
    private void ApplyAutoPlay()
    {
        if (!AutoPlay || !IsMuted || !_isLoaded)
        {
            return;
        }
        if (IsLooping && ArcMotion.GetEffectiveLevel(this) != ArcMotionLevel.Full)
        {
            return;
        }
        SetCurrentValue(IsPlayingProperty, true);
    }

    private void OnIsPlayingChanged(bool playing)
    {
        _playSwap?.Show(playing ? _pauseGlyph : _playGlyph, animate: true);
        if (!_syncing)
        {
            if (playing)
            {
                if (_player is null)
                {
                    EnsurePlayerAndLoad();
                }
                else if (_isLoaded && State != VideoPlaybackState.Failed)
                {
                    _hasStarted = true;
                    _player.Play();
                }
            }
            else if (_isLoaded)
            {
                _player?.Pause();
            }
        }
        UpdateTexts();
        UpdateStateLayer();
        UpdateClockLoop();
        UpdateControls();
    }

    private void OnPositionWritten(TimeSpan position)
    {
        if (_player is null || !_isLoaded)
        {
            return;
        }
        _hasStarted = true;
        _player.Seek(position);
        Report(_player.Position);
        UpdateStateLayer();
    }

    private void SyncIsPlaying(bool playing)
    {
        if (IsPlaying == playing)
        {
            return;
        }
        _syncing = true;
        try
        {
            SetCurrentValue(IsPlayingProperty, playing);
        }
        finally
        {
            _syncing = false;
        }
    }

    private void SetState(VideoPlaybackState state)
    {
        if (State == state)
        {
            return;
        }
        State = state;
        UpdateStatePseudoClasses();
        if (state is VideoPlaybackState.Loading or VideoPlaybackState.Buffering)
        {
            if (!Deadlines.IsPending(SpinnerKey) && !_spinnerVisible)
            {
                Deadlines.Schedule(SpinnerKey, s_spinnerDelay, () =>
                {
                    _spinnerVisible = State is VideoPlaybackState.Loading or VideoPlaybackState.Buffering;
                    UpdateStateLayer();
                });
            }
            if (state == VideoPlaybackState.Buffering)
            {
                Deadlines.Schedule(BufferingKey, s_bufferingAnnounceDelay, () =>
                {
                    if (State == VideoPlaybackState.Buffering)
                    {
                        Announce(VideoText.Get(VideoLangResourceKind.Buffering));
                    }
                });
            }
        }
        else
        {
            Deadlines.Cancel(SpinnerKey);
            Deadlines.Cancel(BufferingKey);
            _spinnerVisible = false;
        }
        RaiseEvent(new RoutedEventArgs(StateChangedEvent, this));
        UpdateStateLayer();
        UpdateClockLoop();
        UpdateControls();
    }

    private void UpdateStatePseudoClasses()
    {
        var state = State;
        PseudoClasses.Set(IdlePseudoClass, state == VideoPlaybackState.Idle);
        PseudoClasses.Set(LoadingPseudoClass, state == VideoPlaybackState.Loading);
        PseudoClasses.Set(PlayingPseudoClass, state == VideoPlaybackState.Playing);
        PseudoClasses.Set(PausedPseudoClass, state == VideoPlaybackState.Paused);
        PseudoClasses.Set(BufferingPseudoClass, state == VideoPlaybackState.Buffering);
        PseudoClasses.Set(EndedPseudoClass, state == VideoPlaybackState.Ended);
        PseudoClasses.Set(FailedPseudoClass, state == VideoPlaybackState.Failed);
    }

    #endregion

    #region Backend events

    private void OnPlayerStateChanged(object? sender, EventArgs e)
    {
        if (!ReferenceEquals(sender, _player) || _player is null)
        {
            return;
        }
        var state = _player.State;
        if (!_isLoaded && state is VideoPlaybackState.Loading or VideoPlaybackState.Paused or VideoPlaybackState.Idle)
        {
            // Loading reports its own progress; the load continuation settles the state.
            if (state == VideoPlaybackState.Loading)
            {
                SetState(state);
            }
            return;
        }
        switch (state)
        {
            case VideoPlaybackState.Playing:
                _hasStarted = true;
                if (!_scrubbing)
                {
                    SyncIsPlaying(true);
                }
                Activity();
                break;
            case VideoPlaybackState.Paused:
                if (!_scrubbing)
                {
                    SyncIsPlaying(false);
                }
                break;
            case VideoPlaybackState.Ended:
                SyncIsPlaying(false);
                Report(Duration);
                break;
            case VideoPlaybackState.Failed:
                SyncIsPlaying(false);
                break;
        }
        Report(_player.Position);
        SetState(state);
        if (state == VideoPlaybackState.Ended)
        {
            RaiseEvent(new RoutedEventArgs(EndedEvent, this));
        }
    }

    private void OnPlayerPositionChanged(object? sender, EventArgs e)
    {
        if (ReferenceEquals(sender, _player) && _player is not null && _isLoaded && !_scrubbing)
        {
            Report(_player.Position);
        }
    }

    private void OnPlayerDurationChanged(object? sender, EventArgs e)
    {
        if (ReferenceEquals(sender, _player) && _player is not null && _isLoaded)
        {
            Duration = _player.Duration;
            UpdateTimes(force: true);
        }
    }

    private void OnPlayerNaturalSizeChanged(object? sender, EventArgs e) => UpdateAspect();

    private void OnPlayerBufferedChanged(object? sender, EventArgs e) => UpdateBuffered();

    private void OnPlayerFailed(object? sender, VideoErrorEventArgs e)
    {
        if (ReferenceEquals(sender, _player))
        {
            OnFailure(e.Kind);
        }
    }

    private void OnFailure(VideoErrorKind kind)
    {
        _lastErrorKind = kind;
        var message = VideoText.Error(kind);
        ErrorMessage = message;
        SyncIsPlaying(false);
        SetState(VideoPlaybackState.Failed);
        var args = new VideoFailedEventArgs(FailedEvent, kind, message);
        RaiseEvent(args);
        _errorHandled = args.Handled;
        Announce(message);
        UpdateStateLayer();
    }

    /// <summary>Loads the source again after a failure and plays it (the error layer's Retry).</summary>
    internal void Retry()
    {
        if (Source is null)
        {
            return;
        }
        _resumePosition = null;
        SyncIsPlaying(true);
        if (_player is null)
        {
            EnsurePlayerAndLoad();
        }
        else
        {
            _ = LoadAsync();
        }
    }

    private void UpdateBuffered()
    {
        if (_seek is not null)
        {
            _seek.Buffered = _player?.Buffered ?? [];
        }
    }

    #endregion

    #region Position and time

    /// <summary>Records a position the backend (or a seek) reported; the clock interpolates from it while playing.</summary>
    private void Report(TimeSpan position)
    {
        _reported = position;
        _reportedAt = Clock.GetTimestamp();
        if (Position != position)
        {
            _syncing = true;
            try
            {
                SetCurrentValue(PositionProperty, position);
            }
            finally
            {
                _syncing = false;
            }
        }
        UpdateTimes(force: false);
    }

    /// <summary>The position shown: the last report, moved on by the clock at the playback rate while playing.</summary>
    private TimeSpan ShownPosition
    {
        get
        {
            if (!_isLoaded || _scrubbing || _player?.State != VideoPlaybackState.Playing)
            {
                return _reported;
            }
            var shown = _reported + Clock.GetElapsedTime(_reportedAt) * PlaybackRate;
            var length = Duration;
            return length > TimeSpan.Zero && shown > length ? length : shown;
        }
    }

    private bool OnClockFrame(double elapsed)
    {
        UpdateTimes(force: false);
        return true;
    }

    private void UpdateClockLoop()
    {
        if (_clockLoop is null)
        {
            return;
        }
        if (_isLoaded && State == VideoPlaybackState.Playing)
        {
            _clockLoop.Start();
        }
        else
        {
            _clockLoop.Stop();
        }
    }

    /// <summary>Moves the seek bar and captions and, when the whole second changed (or <paramref name="force"/>), the time text.</summary>
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
            if (!_seek.IsDragging)
            {
                _seek.SetCurrentValue(RangeBase.ValueProperty, Math.Clamp(shown.TotalSeconds, 0, max));
            }
        }
        if (_captions is not null)
        {
            _captions.Position = shown;
        }
        var displayed = _seek is { IsDragging: true } ? TimeSpan.FromSeconds(_seek.Value) : shown;
        var second = (long)Math.Floor(Math.Max(0, displayed.TotalSeconds));
        if (!force && second == _shownSecond)
        {
            return;
        }
        _shownSecond = second;
        if (_time is not null)
        {
            _time.Text = VideoText.Clock(TimeSpan.FromSeconds(second)) + " / " + VideoText.Clock(length);
        }
        if (_seek is not null && !_seek.IsKeyboardFocusWithin)
        {
            // While playing the value text only says where the video is; the "of" part is read on focus and at the end of a drag (APG seek slider).
            _seek.ValueText = VideoText.Spoken(TimeSpan.FromSeconds(second));
        }
    }

    private void UpdateSeekValueTextFull()
    {
        if (_seek is null)
        {
            return;
        }
        var position = _seek.IsDragging ? TimeSpan.FromSeconds(_seek.Value) : ShownPosition;
        _seek.ValueText = VideoText.Format(VideoLangResourceKind.TimeOf, VideoText.Spoken(position), VideoText.Spoken(Duration));
    }

    #endregion

    #region Captions and chapters

    private void OnTextTracksChanged()
    {
        if (SelectedTextTrack is null && !_captionsChosen && DefaultCaptionsTrack(onlyMarked: true) is { } track)
        {
            SetCurrentValue(SelectedTextTrackProperty, track);
        }
        else if (SelectedTextTrack is { } selected && !_textTracks.Contains(selected))
        {
            SetCurrentValue(SelectedTextTrackProperty, null);
        }
        UpdateChapters();
    }

    /// <summary>The track C turns on: the first default captions or subtitles track, else (unless <paramref name="onlyMarked"/>) the first one.</summary>
    private VideoTextTrack? DefaultCaptionsTrack(bool onlyMarked)
    {
        VideoTextTrack? first = null;
        foreach (var track in _textTracks)
        {
            if (track.Kind is not (VideoTextTrackKind.Captions or VideoTextTrackKind.Subtitles))
            {
                continue;
            }
            if (track.IsDefault)
            {
                return track;
            }
            first ??= track;
        }
        return onlyMarked ? null : first;
    }

    /// <summary>The C key: between the default captions track and off.</summary>
    internal void ToggleCaptions()
    {
        _captionsChosen = true;
        SetCurrentValue(SelectedTextTrackProperty, SelectedTextTrack is null ? DefaultCaptionsTrack(onlyMarked: false) : null);
    }

    private void UpdateCaptions()
    {
        var track = SelectedTextTrack;
        if (_captions is not null)
        {
            _captions.Track = track;
            _captions.CaptionScale = CaptionScale;
            _captions.BackgroundOpacity = CaptionBackgroundOpacity;
        }
        _nativeOwner?.SetNativeCaptions(track);
        if (!ReferenceEquals(_observedCaptions, track))
        {
            _observedCaptions = track;
            if (track is not null)
            {
                _ = LoadTrackAsync(track);
            }
        }
        UpdateTexts();
    }

    private static async Task LoadTrackAsync(VideoTextTrack track)
    {
        try
        {
            await track.LoadAsync();
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            // Bad tracks never break playback; the track keeps its warnings.
            _ = exception;
        }
    }

    private void UpdateChapters()
    {
        var chapters = _textTracks.FirstOrDefault(static t => t.Kind == VideoTextTrackKind.Chapters);
        if (!ReferenceEquals(chapters, _chapterTrack))
        {
            if (_chapterTrack is not null)
            {
                _chapterTrack.CuesChanged -= OnChapterCuesChanged;
            }
            _chapterTrack = chapters;
            if (chapters is not null)
            {
                chapters.CuesChanged += OnChapterCuesChanged;
                _ = LoadTrackAsync(chapters);
            }
        }
        OnChapterCuesChanged(null, EventArgs.Empty);
    }

    private void OnChapterCuesChanged(object? sender, EventArgs e)
    {
        if (!Dispatcher.UIThread.CheckAccess())
        {
            Dispatcher.UIThread.Post(() => OnChapterCuesChanged(sender, e));
            return;
        }
        if (_seek is not null)
        {
            _seek.Chapters = _chapterTrack?.Cues.Select(static c => c.Start).ToArray() ?? [];
        }
    }

    /// <summary>The chapter title at <paramref name="time"/>, if there are chapters.</summary>
    private string? ChapterAt(TimeSpan time) => _chapterTrack is null ? null : VideoChapters.At(_chapterTrack.Cues, time)?.Text;

    private void OnActiveCuesChanged(object? sender, EventArgs e)
    {
        var cues = _captions?.ActiveCues ?? [];
        RaiseEvent(new VideoCueChangedEventArgs(CueChangedEvent, cues));
        if (CaptionAnnouncements && cues.Count > 0)
        {
            Announce(string.Join(" ", cues.Select(static c => c.Text)));
        }
    }

    private static string TrackLabel(VideoTextTrack track)
    {
        if (!string.IsNullOrWhiteSpace(track.Label))
        {
            return track.Label!;
        }
        if (!string.IsNullOrWhiteSpace(track.Language))
        {
            try
            {
                return CultureInfo.GetCultureInfo(track.Language!).NativeName;
            }
            catch (CultureNotFoundException)
            {
                return track.Language!;
            }
        }
        return track.Kind.ToString();
    }

    #endregion

    #region Menus

    private IEnumerable<Control> BuildSpeedMenu()
    {
        yield return new MenuEntryLabel { Text = VideoText.Get(VideoLangResourceKind.Speed) };
        foreach (var rate in PlaybackRates)
        {
            var value = rate;
            var entry = new MenuEntry
            {
                Header = VideoText.Rate(rate),
                ToggleType = MenuEntryToggleType.Radio,
                GroupName = "rate",
                IsChecked = Math.Abs(rate - PlaybackRate) < 0.001,
            };
            entry.Click += (_, _) => SetCurrentValue(PlaybackRateProperty, value);
            yield return entry;
        }
    }

    private IEnumerable<Control> BuildCaptionsMenu()
    {
        yield return new MenuEntryLabel { Text = VideoText.Get(VideoLangResourceKind.Captions) };
        var off = new MenuEntry
        {
            Header = VideoText.Get(VideoLangResourceKind.CaptionsOff),
            ToggleType = MenuEntryToggleType.Radio,
            GroupName = "track",
            IsChecked = SelectedTextTrack is null,
        };
        off.Click += (_, _) =>
        {
            _captionsChosen = true;
            SetCurrentValue(SelectedTextTrackProperty, null);
        };
        yield return off;
        foreach (var track in _textTracks)
        {
            if (track.Kind is not (VideoTextTrackKind.Captions or VideoTextTrackKind.Subtitles))
            {
                continue;
            }
            var chosen = track;
            var entry = new MenuEntry
            {
                Header = TrackLabel(track),
                ToggleType = MenuEntryToggleType.Radio,
                GroupName = "track",
                IsChecked = ReferenceEquals(track, SelectedTextTrack),
            };
            entry.Click += (_, _) =>
            {
                _captionsChosen = true;
                SetCurrentValue(SelectedTextTrackProperty, chosen);
            };
            yield return entry;
        }
        yield return new MenuEntrySeparator();
        yield return new MenuEntryLabel { Text = VideoText.Get(VideoLangResourceKind.CaptionSize) };
        foreach (var scale in s_captionScales)
        {
            var value = scale;
            var entry = new MenuEntry
            {
                Header = (scale * 100).ToString("0", CultureInfo.InvariantCulture) + "%",
                ToggleType = MenuEntryToggleType.Radio,
                GroupName = "size",
                IsChecked = Math.Abs(scale - CaptionScale) < 0.001,
            };
            entry.Click += (_, _) => SetCurrentValue(CaptionScaleProperty, value);
            yield return entry;
        }
    }

    private static readonly double[] s_captionScales = [0.75, 1, 1.5, 2];

    private void OnMenuOpenChanged(bool open)
    {
        if (!open)
        {
            Activity();
        }
        UpdateControls();
    }

    private bool IsMenuOpen => _captionsButton?.IsMenuOpen == true || _speedButton?.IsMenuOpen == true;

    /// <summary>The next (<paramref name="direction"/> 1) or previous (-1) speed of <see cref="PlaybackRates"/>.</summary>
    internal void StepRate(int direction)
    {
        var rates = PlaybackRates.OrderBy(static r => r).ToArray();
        if (rates.Length == 0)
        {
            return;
        }
        var current = PlaybackRate;
        double? next = direction > 0
            ? rates.Where(r => r > current + 0.001).Cast<double?>().FirstOrDefault()
            : rates.Where(r => r < current - 0.001).Cast<double?>().LastOrDefault();
        if (next is { } rate)
        {
            SetCurrentValue(PlaybackRateProperty, rate);
        }
    }

    #endregion

    #region Visual state

    private void UpdateAspect()
    {
        if (_frame is null)
        {
            return;
        }
        double aspect = 16.0 / 9.0;
        if (AspectRatio is { } ratio && double.IsFinite(ratio) && ratio > 0)
        {
            aspect = ratio;
        }
        else if (_player is { NaturalSize: { Width: > 0, Height: > 0 } size } && _isLoaded)
        {
            aspect = size.Width / (double)size.Height;
        }
        _frame.Aspect = aspect;
    }

    /// <summary>The root's corners (none in full screen) and the full-screen title.</summary>
    private void UpdateChrome()
    {
        var radius = IsFullScreen ? default : CornerRadius;
        if (_root is not null)
        {
            _root.CornerRadius = radius;
        }
        if (_focusRing is not null)
        {
            _focusRing.CornerRadius = radius;
        }
        if (_titleBar is not null)
        {
            _titleBar.IsVisible = IsFullScreen && !string.IsNullOrWhiteSpace(Title);
        }
    }

    private void UpdateCompact()
    {
        var width = IsFullScreen && _root is not null ? _root.Bounds.Width : Bounds.Width;
        var compact = width > 0 && width < CompactWidth;
        PseudoClasses.Set(CompactPseudoClass, compact);
        if (_volume is not null)
        {
            _volume.IsCompact = compact;
        }
    }

    private void UpdateStateLayer()
    {
        if (_stateLayer is null)
        {
            return;
        }
        var state = State;
        _stateLayer.Poster = Poster;
        _stateLayer.PosterStretch = Stretch;
        _stateLayer.ShowPoster = Poster is not null && !_hasStarted && state != VideoPlaybackState.Failed;
        _stateLayer.ShowSpinner = _spinnerVisible;
        _stateLayer.ShowBigPlay = !_hasStarted && !IsPlaying && !_spinnerVisible && state is VideoPlaybackState.Idle or VideoPlaybackState.Paused;
        _stateLayer.ShowReplay = state == VideoPlaybackState.Ended;
        _stateLayer.ShowError = state == VideoPlaybackState.Failed && !_errorHandled;
        _stateLayer.ErrorText = ErrorMessage;
        _stateLayer.HintText = _player is { Backend.IsReal: false } && Source is not null and not SimulatedVideoSource && state != VideoPlaybackState.Failed
            ? VideoText.Get(VideoLangResourceKind.BackendHint)
            : null;
        UpdateNativeLayout();
    }

    private void UpdateVolume()
    {
        if (_volume is null)
        {
            return;
        }
        _volume.Slider.SetCurrentValue(RangeBase.ValueProperty, IsMuted ? 0 : Volume);
        _volume.Glyph.Data = IsMuted || Volume <= 0 ? VideoGlyphs.VolumeOff : Volume < 0.5 ? VideoGlyphs.VolumeLow : VideoGlyphs.Volume;
        _volume.MuteButton.IsChecked = IsMuted;
        var mute = VideoText.Get(IsMuted ? VideoLangResourceKind.Unmute : VideoLangResourceKind.Mute);
        AutomationProperties.SetName(_volume.MuteButton, mute);
        AutomationProperties.SetAcceleratorKey(_volume.MuteButton, "M");
        Tooltip.SetTip(_volume.MuteButton, VideoText.WithShortcut(mute, "M"));
        AutomationProperties.SetName(_volume.Slider, VideoText.Get(VideoLangResourceKind.Volume));
        _volume.Slider.ValueText = IsMuted
            ? VideoText.Get(VideoLangResourceKind.Muted)
            : VideoText.Format(VideoLangResourceKind.VolumeValue, (int)Math.Round(Volume * 100));
    }

    /// <summary>Names, tooltips and toggle states of the buttons in the current language.</summary>
    private void UpdateTexts()
    {
        var playing = IsPlaying;
        if (_play is not null)
        {
            var name = VideoText.Get(playing ? VideoLangResourceKind.Pause : VideoLangResourceKind.Play);
            AutomationProperties.SetName(_play, name);
            AutomationProperties.SetAcceleratorKey(_play, "K");
            Tooltip.SetTip(_play, VideoText.WithShortcut(name, "K"));
            _play.IsChecked = playing;
        }
        if (_seek is not null)
        {
            AutomationProperties.SetName(_seek, VideoText.Get(VideoLangResourceKind.Seek));
            _seek.SmallChange = SeekStep.TotalSeconds;
        }
        if (_captionsButton is not null)
        {
            var name = VideoText.Get(VideoLangResourceKind.Captions);
            AutomationProperties.SetName(_captionsButton, name);
            AutomationProperties.SetAcceleratorKey(_captionsButton, "C");
            Tooltip.SetTip(_captionsButton, VideoText.WithShortcut(name, "C"));
            _captionsButton.Classes.Set("on", SelectedTextTrack is not null);
            _captionsButton.IsVisible = _textTracks.Any(static t => t.Kind is VideoTextTrackKind.Captions or VideoTextTrackKind.Subtitles);
        }
        if (_speedButton is not null)
        {
            var name = VideoText.Get(VideoLangResourceKind.Speed);
            AutomationProperties.SetName(_speedButton, name);
            Tooltip.SetTip(_speedButton, name);
        }
        if (_fullScreenButton is not null)
        {
            var name = VideoText.Get(IsFullScreen ? VideoLangResourceKind.ExitFullScreen : VideoLangResourceKind.FullScreen);
            AutomationProperties.SetName(_fullScreenButton, name);
            AutomationProperties.SetAcceleratorKey(_fullScreenButton, "F");
            Tooltip.SetTip(_fullScreenButton, VideoText.WithShortcut(name, "F"));
            _fullScreenButton.IsChecked = IsFullScreen;
            if (_fullScreenGlyph is not null)
            {
                _fullScreenGlyph.Data = IsFullScreen ? VideoGlyphs.ExitFullScreen : VideoGlyphs.FullScreen;
            }
        }
        if (_stateLayer is not null)
        {
            _stateLayer.BigPlayText = VideoText.Get(VideoLangResourceKind.Play);
            _stateLayer.ReplayText = VideoText.Get(VideoLangResourceKind.Replay);
            _stateLayer.RetryText = VideoText.Get(VideoLangResourceKind.Retry);
            if (State == VideoPlaybackState.Failed && ErrorMessage is not null)
            {
                _stateLayer.ErrorText = ErrorMessage;
            }
        }
        if (_root is not null)
        {
            AutomationProperties.SetName(_root, AccessibleName);
        }
        UpdateTimes(force: true);
    }

    private void OnLanguageChanged(object? sender, LanguageChangedEventArgs e)
    {
        if (Dispatcher.UIThread.CheckAccess())
        {
            RefreshLanguage();
        }
        else
        {
            Dispatcher.UIThread.Post(RefreshLanguage);
        }
    }

    private void RefreshLanguage()
    {
        if (State == VideoPlaybackState.Failed && ErrorMessage is not null && _lastErrorKind is { } kind)
        {
            ErrorMessage = VideoText.Error(kind);
        }
        UpdateTexts();
        UpdateVolume();
        UpdateStateLayer();
    }

    private VideoErrorKind? _lastErrorKind;

    /// <summary>The player's accessible name: <c>AutomationProperties.Name</c>, else <see cref="Title"/>, else "Video".</summary>
    internal string AccessibleName =>
        AutomationProperties.GetName(this) is { Length: > 0 } name ? name
        : !string.IsNullOrWhiteSpace(Title) ? Title!
        : VideoText.Get(VideoLangResourceKind.Video);

    /// <summary>Tells screen readers about a change (polite live region).</summary>
    internal void Announce(string text)
    {
        _lastAnnouncement = text;
        if (_announcer is null)
        {
            return;
        }
        _announcer.Text = text;
        AutomationProperties.SetName(_announcer, text);
    }

    #endregion

    #region Controls visibility

    /// <summary>Pointer movement, a key or a click: shows the control bar and restarts the idle countdown.</summary>
    private void Activity()
    {
        _active = true;
        Deadlines.Schedule(HideKey, ControlsHideDelay, () =>
        {
            _active = false;
            UpdateControls();
        });
        UpdateControls();
    }

    /// <summary>Whether the bar shows (spec sections 2, 6, 11 and 12).</summary>
    private bool ComputeControlsVisible()
    {
        switch (ShowControls)
        {
            case VideoControlsVisibility.Never:
                return false;
            case VideoControlsVisibility.Always:
                return true;
        }
        if (_nativeView is not null)
        {
            // Below the picture: nothing to uncover, and hiding it would make the frame jump.
            return true;
        }
        if (IsMenuOpen || _seek is { IsDragging: true } || _volume?.Slider.IsDragging == true || IsKeyboardFocusInBar())
        {
            return true;
        }
        if (!_hasStarted)
        {
            return false;
        }
        if (State is not (VideoPlaybackState.Playing or VideoPlaybackState.Buffering) || !IsPlaying)
        {
            return true;
        }
        return _active || _controls?.IsPointerOver == true;
    }

    /// <summary>
    /// Whether keyboard focus (shown with a focus ring) is inside the bar. Focus a mouse click leaves on a button does not
    /// count, or the bar would never hide after clicking one.
    /// </summary>
    private bool IsKeyboardFocusInBar()
    {
        if (_controls is not { IsKeyboardFocusWithin: true } controls)
        {
            return false;
        }
        var focused = TopLevel.GetTopLevel(controls)?.FocusManager?.GetFocusedElement() as Control;
        return focused is not null && focused.Classes.Contains(":focus-visible");
    }

    private void UpdateControls()
    {
        var visible = ComputeControlsVisible();
        _controlsVisible = visible;
        PseudoClasses.Set(ControlsVisiblePseudoClass, visible);
        if (_controls is not null)
        {
            _controls.IsHitTestVisible = visible;
        }
        if (_captions is not null)
        {
            _captions.BottomInset = visible && _controls is not null ? _controls.Bounds.Height : 0;
        }
        if (!visible)
        {
            HideSeekTip();
        }
        UpdateNativeLayout();
        if (_root is not null)
        {
            var hideCursor = !visible && ShowControls != VideoControlsVisibility.Always && State == VideoPlaybackState.Playing;
            _root.Cursor = hideCursor ? NoCursor() : null;
        }
    }

    private static Cursor? NoCursor()
    {
        try
        {
            return s_noCursor ??= new Cursor(StandardCursorType.None);
        }
        catch (Exception exception) when (exception is InvalidOperationException or NotSupportedException)
        {
            // No cursor factory (some headless hosts).
            return null;
        }
    }

    private void OnControlsPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == IsPointerOverProperty || e.Property == IsKeyboardFocusWithinProperty)
        {
            UpdateControls();
        }
        else if (e.Property == BoundsProperty)
        {
            UpdateControls();
        }
    }

    private void OnRootPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == BoundsProperty && IsFullScreen)
        {
            UpdateCompact();
        }
    }

    private void OnRootFocusChanged(object? sender, RoutedEventArgs e)
    {
        if (_focusRing is not null && _root is not null)
        {
            _focusRing.IsVisible = _root.IsFocused && _root.Classes.Contains(":focus-visible");
        }
        UpdateControls();
    }

    #endregion
}
