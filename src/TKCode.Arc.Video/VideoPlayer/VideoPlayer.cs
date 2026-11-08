using System.Windows.Input;
using Avalonia;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Interactivity;
using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>When the <see cref="VideoPlayer"/> control bar shows.</summary>
public enum VideoControlsVisibility
{
    /// <summary>Shown on activity and while paused; hidden after <see cref="VideoPlayer.ControlsHideDelay"/> of idle playback.</summary>
    Auto,

    /// <summary>Always shown (W3C MAUR IC-4).</summary>
    Always,

    /// <summary>Never shown (a <see cref="VideoSurface"/> usually fits better).</summary>
    Never,
}

/// <summary>How <see cref="VideoPlayer"/> goes full screen.</summary>
public enum VideoFullScreenMode
{
    /// <summary>The player fills its top level's overlay layer; the window stays as it is.</summary>
    Window,

    /// <summary>As <see cref="Window"/>, and the desktop window also goes full screen (the browser uses the Fullscreen API).</summary>
    System,
}

/// <summary>How a browser backend presents video (ADR 0036 decision 4); other platforms ignore it.</summary>
public enum VideoBrowserPresentation
{
    /// <summary>Frames are copied into the composition tree: controls, captions, corners and motion as on desktop.</summary>
    Canvas,

    /// <summary>
    /// The browser's own video element sits in the picture area: no frame copies, and every format the browser plays (HLS in
    /// Safari, for example). Nothing can be drawn over it: the control bar sits below the picture and always shows, captions
    /// are the browser's, and rounded corners do not apply. Falls back to <see cref="Canvas"/> where the backend has no such
    /// element (desktop, or the net8.0 build of the browser backend).
    /// </summary>
    Native,
}

/// <summary>Carries a <see cref="VideoPlayer"/> failure; set <see cref="RoutedEventArgs.Handled"/> to replace the built-in error layer.</summary>
public sealed class VideoFailedEventArgs(RoutedEvent routedEvent, VideoErrorKind kind, string message) : RoutedEventArgs(routedEvent)
{
    /// <summary>The kind of failure.</summary>
    public VideoErrorKind Kind { get; } = kind;

    /// <summary>The localized message the error layer shows.</summary>
    public string Message { get; } = message;
}

/// <summary>Carries the cues a <see cref="VideoPlayer"/> shows now.</summary>
public sealed class VideoCueChangedEventArgs(RoutedEvent routedEvent, IReadOnlyList<VideoCue> cues) : RoutedEventArgs(routedEvent)
{
    /// <summary>The active cues of the selected captions track, in stacking order; empty between cues.</summary>
    public IReadOnlyList<VideoCue> Cues { get; } = cues;
}

/// <summary>
/// Plays a video with Arc controls over the picture: play, seek (buffered ranges, hover time, chapters), time, volume,
/// speed, captions and full screen, hidden after two seconds of idle playback (design/components/video/video.md). The
/// picture is drawn inside the composition tree by a <see cref="VideoSurface"/>, so captions, controls, rounded corners
/// and motion work over it. Playback comes from <see cref="ArcVideo.Backend"/>; without a platform backend the
/// simulated one keeps time and draws a test picture.
/// </summary>
[TemplatePart(FramePart, typeof(VideoPlayerFrame))]
[TemplatePart(RootPart, typeof(Border))]
[TemplatePart(SurfacePart, typeof(VideoSurface))]
[TemplatePart(CaptionsPart, typeof(VideoCaptionLayer))]
[TemplatePart(StatePart, typeof(VideoStateLayer))]
[TemplatePart(ControlsPart, typeof(VideoControlBar))]
[TemplatePart(SeekPart, typeof(VideoSeekBar))]
[TemplatePart(PlayPart, typeof(VideoToggleButton))]
[TemplatePart(VolumePart, typeof(VideoVolume))]
[TemplatePart(TimePart, typeof(TextBlock))]
[TemplatePart(CaptionsButtonPart, typeof(VideoMenuButton))]
[TemplatePart(SpeedButtonPart, typeof(VideoMenuButton))]
[TemplatePart(FullScreenButtonPart, typeof(VideoToggleButton))]
[TemplatePart(AnnouncerPart, typeof(TextBlock))]
[PseudoClasses(IdlePseudoClass, LoadingPseudoClass, PlayingPseudoClass, PausedPseudoClass, BufferingPseudoClass, EndedPseudoClass,
    FailedPseudoClass, ControlsVisiblePseudoClass, FullScreenPseudoClass, NativePresentationPseudoClass, CompactPseudoClass)]
public partial class VideoPlayer : TemplatedControl
{
    public const string FramePart = "PART_Frame";
    public const string RootPart = "PART_Root";
    public const string SurfacePart = "PART_Surface";
    public const string CaptionsPart = "PART_Captions";
    public const string StatePart = "PART_State";
    public const string ScrimPart = "PART_Scrim";
    public const string TitlePart = "PART_Title";
    public const string ControlsPart = "PART_Controls";
    public const string SeekPart = "PART_Seek";
    public const string SeekTipPart = "PART_SeekTip";
    public const string PlayPart = "PART_Play";
    public const string VolumePart = "PART_Volume";
    public const string TimePart = "PART_Time";
    public const string CaptionsButtonPart = "PART_CaptionsButton";
    public const string SpeedButtonPart = "PART_SpeedButton";
    public const string FullScreenButtonPart = "PART_FullScreenButton";
    public const string FocusRingPart = "PART_RootFocusRing";
    public const string AnnouncerPart = "PART_Announcer";

    public const string IdlePseudoClass = ":idle";
    public const string LoadingPseudoClass = ":loading";
    public const string PlayingPseudoClass = ":playing";
    public const string PausedPseudoClass = ":paused";
    public const string BufferingPseudoClass = ":buffering";
    public const string EndedPseudoClass = ":ended";
    public const string FailedPseudoClass = ":failed";
    public const string ControlsVisiblePseudoClass = ":controls-visible";
    public const string FullScreenPseudoClass = ":fullscreen";
    public const string NativePresentationPseudoClass = ":native-presentation";
    public const string CompactPseudoClass = ":compact";

    private static readonly IReadOnlyList<double> s_defaultRates = [0.5, 0.75, 1, 1.25, 1.5, 2];

    /// <summary>Defines the <see cref="Source"/> property.</summary>
    public static readonly StyledProperty<VideoSource?> SourceProperty =
        AvaloniaProperty.Register<VideoPlayer, VideoSource?>(nameof(Source));

    /// <summary>Defines the <see cref="Poster"/> property.</summary>
    public static readonly StyledProperty<IImage?> PosterProperty =
        AvaloniaProperty.Register<VideoPlayer, IImage?>(nameof(Poster));

    /// <summary>Defines the <see cref="AutoPlay"/> property.</summary>
    public static readonly StyledProperty<bool> AutoPlayProperty =
        AvaloniaProperty.Register<VideoPlayer, bool>(nameof(AutoPlay));

    /// <summary>Defines the <see cref="IsPlaying"/> property.</summary>
    public static readonly StyledProperty<bool> IsPlayingProperty =
        AvaloniaProperty.Register<VideoPlayer, bool>(nameof(IsPlaying), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>Defines the <see cref="Position"/> property.</summary>
    public static readonly StyledProperty<TimeSpan> PositionProperty =
        AvaloniaProperty.Register<VideoPlayer, TimeSpan>(nameof(Position), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>Defines the <see cref="Duration"/> property.</summary>
    public static readonly DirectProperty<VideoPlayer, TimeSpan> DurationProperty =
        AvaloniaProperty.RegisterDirect<VideoPlayer, TimeSpan>(nameof(Duration), o => o.Duration);

    /// <summary>Defines the <see cref="Volume"/> property.</summary>
    public static readonly StyledProperty<double> VolumeProperty =
        AvaloniaProperty.Register<VideoPlayer, double>(nameof(Volume), 1, defaultBindingMode: BindingMode.TwoWay,
            coerce: static (_, v) => double.IsNaN(v) ? 0 : Math.Clamp(v, 0, 1));

    /// <summary>Defines the <see cref="IsMuted"/> property.</summary>
    public static readonly StyledProperty<bool> IsMutedProperty =
        AvaloniaProperty.Register<VideoPlayer, bool>(nameof(IsMuted), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>Defines the <see cref="IsLooping"/> property.</summary>
    public static readonly StyledProperty<bool> IsLoopingProperty =
        AvaloniaProperty.Register<VideoPlayer, bool>(nameof(IsLooping));

    /// <summary>Defines the <see cref="PlaybackRate"/> property.</summary>
    public static readonly StyledProperty<double> PlaybackRateProperty =
        AvaloniaProperty.Register<VideoPlayer, double>(nameof(PlaybackRate), 1, defaultBindingMode: BindingMode.TwoWay,
            coerce: static (_, v) => double.IsNaN(v) || v <= 0 ? 1 : Math.Clamp(v, VideoPlayerBase.MinPlaybackRate, VideoPlayerBase.MaxPlaybackRate));

    /// <summary>Defines the <see cref="PlaybackRates"/> property.</summary>
    public static readonly StyledProperty<IReadOnlyList<double>> PlaybackRatesProperty =
        AvaloniaProperty.Register<VideoPlayer, IReadOnlyList<double>>(nameof(PlaybackRates), s_defaultRates);

    /// <summary>Defines the <see cref="SelectedTextTrack"/> property.</summary>
    public static readonly StyledProperty<VideoTextTrack?> SelectedTextTrackProperty =
        AvaloniaProperty.Register<VideoPlayer, VideoTextTrack?>(nameof(SelectedTextTrack), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>Defines the <see cref="CaptionScale"/> property.</summary>
    public static readonly StyledProperty<double> CaptionScaleProperty =
        AvaloniaProperty.Register<VideoPlayer, double>(nameof(CaptionScale), 1, defaultBindingMode: BindingMode.TwoWay);

    /// <summary>Defines the <see cref="CaptionBackgroundOpacity"/> property.</summary>
    public static readonly StyledProperty<double> CaptionBackgroundOpacityProperty =
        AvaloniaProperty.Register<VideoPlayer, double>(nameof(CaptionBackgroundOpacity), 0.8);

    /// <summary>Defines the <see cref="CaptionAnnouncements"/> property.</summary>
    public static readonly StyledProperty<bool> CaptionAnnouncementsProperty =
        AvaloniaProperty.Register<VideoPlayer, bool>(nameof(CaptionAnnouncements));

    /// <summary>Defines the <see cref="ShowControls"/> property.</summary>
    public static readonly StyledProperty<VideoControlsVisibility> ShowControlsProperty =
        AvaloniaProperty.Register<VideoPlayer, VideoControlsVisibility>(nameof(ShowControls));

    /// <summary>Defines the <see cref="ControlsHideDelay"/> property.</summary>
    public static readonly StyledProperty<TimeSpan> ControlsHideDelayProperty =
        AvaloniaProperty.Register<VideoPlayer, TimeSpan>(nameof(ControlsHideDelay), TimeSpan.FromSeconds(2));

    /// <summary>Defines the <see cref="SeekStep"/> property.</summary>
    public static readonly StyledProperty<TimeSpan> SeekStepProperty =
        AvaloniaProperty.Register<VideoPlayer, TimeSpan>(nameof(SeekStep), TimeSpan.FromSeconds(5));

    /// <summary>Defines the <see cref="Stretch"/> property.</summary>
    public static readonly StyledProperty<Stretch> StretchProperty =
        AvaloniaProperty.Register<VideoPlayer, Stretch>(nameof(Stretch), Stretch.Uniform);

    /// <summary>Defines the <see cref="AspectRatio"/> property.</summary>
    public static readonly StyledProperty<double?> AspectRatioProperty =
        AvaloniaProperty.Register<VideoPlayer, double?>(nameof(AspectRatio));

    /// <summary>Defines the <see cref="IsFullScreen"/> property.</summary>
    public static readonly StyledProperty<bool> IsFullScreenProperty =
        AvaloniaProperty.Register<VideoPlayer, bool>(nameof(IsFullScreen), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>Defines the <see cref="FullScreenMode"/> property.</summary>
    public static readonly StyledProperty<VideoFullScreenMode> FullScreenModeProperty =
        AvaloniaProperty.Register<VideoPlayer, VideoFullScreenMode>(nameof(FullScreenMode));

    /// <summary>Defines the <see cref="Title"/> property.</summary>
    public static readonly StyledProperty<string?> TitleProperty =
        AvaloniaProperty.Register<VideoPlayer, string?>(nameof(Title));

    /// <summary>Defines the <see cref="State"/> property.</summary>
    public static readonly DirectProperty<VideoPlayer, VideoPlaybackState> StateProperty =
        AvaloniaProperty.RegisterDirect<VideoPlayer, VideoPlaybackState>(nameof(State), o => o.State);

    /// <summary>Defines the <see cref="ErrorMessage"/> property.</summary>
    public static readonly DirectProperty<VideoPlayer, string?> ErrorMessageProperty =
        AvaloniaProperty.RegisterDirect<VideoPlayer, string?>(nameof(ErrorMessage), o => o.ErrorMessage);

    /// <summary>Defines the <see cref="BrowserPresentation"/> property.</summary>
    public static readonly StyledProperty<VideoBrowserPresentation> BrowserPresentationProperty =
        AvaloniaProperty.Register<VideoPlayer, VideoBrowserPresentation>(nameof(BrowserPresentation));

    /// <summary>Defines the <see cref="Player"/> property.</summary>
    public static readonly DirectProperty<VideoPlayer, IVideoPlayer?> PlayerProperty =
        AvaloniaProperty.RegisterDirect<VideoPlayer, IVideoPlayer?>(nameof(Player), o => o.Player);

    /// <summary>Defines the <see cref="CompactWidth"/> property.</summary>
    public static readonly StyledProperty<double> CompactWidthProperty =
        AvaloniaProperty.Register<VideoPlayer, double>(nameof(CompactWidth), 420);

    /// <summary>Defines the <see cref="StateChanged"/> event.</summary>
    public static readonly RoutedEvent<RoutedEventArgs> StateChangedEvent =
        RoutedEvent.Register<VideoPlayer, RoutedEventArgs>(nameof(StateChanged), RoutingStrategies.Bubble);

    /// <summary>Defines the <see cref="Ended"/> event.</summary>
    public static readonly RoutedEvent<RoutedEventArgs> EndedEvent =
        RoutedEvent.Register<VideoPlayer, RoutedEventArgs>(nameof(Ended), RoutingStrategies.Bubble);

    /// <summary>Defines the <see cref="Failed"/> event.</summary>
    public static readonly RoutedEvent<VideoFailedEventArgs> FailedEvent =
        RoutedEvent.Register<VideoPlayer, VideoFailedEventArgs>(nameof(Failed), RoutingStrategies.Bubble);

    /// <summary>Defines the <see cref="FullScreenChanged"/> event.</summary>
    public static readonly RoutedEvent<RoutedEventArgs> FullScreenChangedEvent =
        RoutedEvent.Register<VideoPlayer, RoutedEventArgs>(nameof(FullScreenChanged), RoutingStrategies.Bubble);

    /// <summary>Defines the <see cref="CueChanged"/> event.</summary>
    public static readonly RoutedEvent<VideoCueChangedEventArgs> CueChangedEvent =
        RoutedEvent.Register<VideoPlayer, VideoCueChangedEventArgs>(nameof(CueChanged), RoutingStrategies.Bubble);

    private readonly AvaloniaList<VideoTextTrack> _textTracks = [];
    private TimeSpan _duration;
    private VideoPlaybackState _state = VideoPlaybackState.Idle;
    private string? _errorMessage;
    private IVideoPlayer? _player;

    static VideoPlayer()
    {
        FocusableProperty.OverrideDefaultValue<VideoPlayer>(false);
        ClipToBoundsProperty.OverrideDefaultValue<VideoPlayer>(false);
    }

    /// <summary>Creates a player.</summary>
    public VideoPlayer()
    {
        _textTracks.CollectionChanged += (_, _) => OnTextTracksChanged();
        PlayPauseCommand = new VideoCommand(TogglePlay);
        ToggleMuteCommand = new VideoCommand(() => SetCurrentValue(IsMutedProperty, !IsMuted));
        ToggleFullScreenCommand = new VideoCommand(ToggleFullScreen);
    }

    /// <summary>The video to play; changing it loads the new one (cancelling a load in progress). A string in XAML is an http(s), avares or file address.</summary>
    public VideoSource? Source
    {
        get => GetValue(SourceProperty);
        set => SetValue(SourceProperty, value);
    }

    /// <summary>The picture shown before playback starts (until the first play or seek); without one the first frame shows once loaded.</summary>
    public IImage? Poster
    {
        get => GetValue(PosterProperty);
        set => SetValue(PosterProperty, value);
    }

    /// <summary>Starts playing once loaded, only while <see cref="IsMuted"/> is true (WCAG 1.4.2); a muted looping video does not start under reduced motion.</summary>
    public bool AutoPlay
    {
        get => GetValue(AutoPlayProperty);
        set => SetValue(AutoPlayProperty, value);
    }

    /// <summary>Whether the video should play. Setting it before the video has loaded plays it once loaded; the player writes false back when playback pauses, ends or fails.</summary>
    public bool IsPlaying
    {
        get => GetValue(IsPlayingProperty);
        set => SetValue(IsPlayingProperty, value);
    }

    /// <summary>The playback position; writing it seeks.</summary>
    public TimeSpan Position
    {
        get => GetValue(PositionProperty);
        set => SetValue(PositionProperty, value);
    }

    /// <summary>The length of the loaded video; zero until known.</summary>
    public TimeSpan Duration
    {
        get => _duration;
        private set => SetAndRaise(DurationProperty, ref _duration, value);
    }

    /// <summary>Output volume from 0 to 1.</summary>
    public double Volume
    {
        get => GetValue(VolumeProperty);
        set => SetValue(VolumeProperty, value);
    }

    /// <summary>Whether the sound is off (without changing <see cref="Volume"/>).</summary>
    public bool IsMuted
    {
        get => GetValue(IsMutedProperty);
        set => SetValue(IsMutedProperty, value);
    }

    /// <summary>Whether playback starts again at the end.</summary>
    public bool IsLooping
    {
        get => GetValue(IsLoopingProperty);
        set => SetValue(IsLoopingProperty, value);
    }

    /// <summary>Playback speed, 0.25 to 4.</summary>
    public double PlaybackRate
    {
        get => GetValue(PlaybackRateProperty);
        set => SetValue(PlaybackRateProperty, value);
    }

    /// <summary>The speeds the speed menu offers (0.5, 0.75, 1, 1.25, 1.5, 2).</summary>
    public IReadOnlyList<double> PlaybackRates
    {
        get => GetValue(PlaybackRatesProperty);
        set => SetValue(PlaybackRatesProperty, value);
    }

    /// <summary>The captions, subtitles and chapters tracks (spec section 9).</summary>
    public IList<VideoTextTrack> TextTracks => _textTracks;

    /// <summary>The captions or subtitles track shown; null shows none. The first track marked <see cref="VideoTextTrack.IsDefault"/> is selected on load.</summary>
    public VideoTextTrack? SelectedTextTrack
    {
        get => GetValue(SelectedTextTrackProperty);
        set => SetValue(SelectedTextTrackProperty, value);
    }

    /// <summary>Caption text size multiplier; the captions menu offers 0.75, 1, 1.5 and 2.</summary>
    public double CaptionScale
    {
        get => GetValue(CaptionScaleProperty);
        set => SetValue(CaptionScaleProperty, value);
    }

    /// <summary>Opacity of the black box behind caption text, 0 to 1.</summary>
    public double CaptionBackgroundOpacity
    {
        get => GetValue(CaptionBackgroundOpacityProperty);
        set => SetValue(CaptionBackgroundOpacityProperty, value);
    }

    /// <summary>Whether screen readers read each caption as it appears (a polite live region; off by default).</summary>
    public bool CaptionAnnouncements
    {
        get => GetValue(CaptionAnnouncementsProperty);
        set => SetValue(CaptionAnnouncementsProperty, value);
    }

    /// <summary>When the control bar shows: <see cref="VideoControlsVisibility.Auto"/> hides it during idle playback.</summary>
    public VideoControlsVisibility ShowControls
    {
        get => GetValue(ShowControlsProperty);
        set => SetValue(ShowControlsProperty, value);
    }

    /// <summary>How long playback runs without activity before the control bar hides (2 s).</summary>
    public TimeSpan ControlsHideDelay
    {
        get => GetValue(ControlsHideDelayProperty);
        set => SetValue(ControlsHideDelayProperty, value);
    }

    /// <summary>How far the left and right arrow keys seek (5 s); J and L seek 10 s.</summary>
    public TimeSpan SeekStep
    {
        get => GetValue(SeekStepProperty);
        set => SetValue(SeekStepProperty, value);
    }

    /// <summary>How the picture fills the player (passed to the <see cref="VideoSurface"/>).</summary>
    public Stretch Stretch
    {
        get => GetValue(StretchProperty);
        set => SetValue(StretchProperty, value);
    }

    /// <summary>Width ÷ height of the player. Null uses 16:9 until the video has loaded, then the video's own ratio.</summary>
    public double? AspectRatio
    {
        get => GetValue(AspectRatioProperty);
        set => SetValue(AspectRatioProperty, value);
    }

    /// <summary>Whether the player fills its top level (spec section 10).</summary>
    public bool IsFullScreen
    {
        get => GetValue(IsFullScreenProperty);
        set => SetValue(IsFullScreenProperty, value);
    }

    /// <summary>Whether full screen also makes the window full screen.</summary>
    public VideoFullScreenMode FullScreenMode
    {
        get => GetValue(FullScreenModeProperty);
        set => SetValue(FullScreenModeProperty, value);
    }

    /// <summary>The video's title: the player's accessible name and the heading shown at the top left in full screen.</summary>
    public string? Title
    {
        get => GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    /// <summary>The playback state.</summary>
    public VideoPlaybackState State
    {
        get => _state;
        private set => SetAndRaise(StateProperty, ref _state, value);
    }

    /// <summary>The localized error text while <see cref="State"/> is <see cref="VideoPlaybackState.Failed"/>; null otherwise.</summary>
    public string? ErrorMessage
    {
        get => _errorMessage;
        private set => SetAndRaise(ErrorMessageProperty, ref _errorMessage, value);
    }

    /// <summary>How a browser backend presents the video (ADR 0036 decision 4): frames drawn by Arc (<see cref="VideoBrowserPresentation.Canvas"/>, the default) or the browser's own video element (<see cref="VideoBrowserPresentation.Native"/>); ignored elsewhere.</summary>
    public VideoBrowserPresentation BrowserPresentation
    {
        get => GetValue(BrowserPresentationProperty);
        set => SetValue(BrowserPresentationProperty, value);
    }

    /// <summary>The underlying player while one exists (attached with a source); for advanced use.</summary>
    public IVideoPlayer? Player
    {
        get => _player;
        private set => SetAndRaise(PlayerProperty, ref _player, value);
    }

    /// <summary>Below this width the player uses its compact layout (420; from the theme token).</summary>
    public double CompactWidth
    {
        get => GetValue(CompactWidthProperty);
        set => SetValue(CompactWidthProperty, value);
    }

    /// <summary>Plays or pauses (for an external button).</summary>
    public ICommand PlayPauseCommand { get; }

    /// <summary>Mutes or unmutes (for an external button).</summary>
    public ICommand ToggleMuteCommand { get; }

    /// <summary>Enters or leaves full screen (for an external button).</summary>
    public ICommand ToggleFullScreenCommand { get; }

    /// <summary>Raised when <see cref="State"/> changes.</summary>
    public event EventHandler<RoutedEventArgs>? StateChanged
    {
        add => AddHandler(StateChangedEvent, value);
        remove => RemoveHandler(StateChangedEvent, value);
    }

    /// <summary>Raised when playback reaches the end (not while looping).</summary>
    public event EventHandler<RoutedEventArgs>? Ended
    {
        add => AddHandler(EndedEvent, value);
        remove => RemoveHandler(EndedEvent, value);
    }

    /// <summary>Raised when loading or playback fails; handle it to replace the built-in error layer.</summary>
    public event EventHandler<VideoFailedEventArgs>? Failed
    {
        add => AddHandler(FailedEvent, value);
        remove => RemoveHandler(FailedEvent, value);
    }

    /// <summary>Raised when <see cref="IsFullScreen"/> changes.</summary>
    public event EventHandler<RoutedEventArgs>? FullScreenChanged
    {
        add => AddHandler(FullScreenChangedEvent, value);
        remove => RemoveHandler(FullScreenChangedEvent, value);
    }

    /// <summary>Raised when the shown captions change (for a transcript panel).</summary>
    public event EventHandler<VideoCueChangedEventArgs>? CueChanged
    {
        add => AddHandler(CueChangedEvent, value);
        remove => RemoveHandler(CueChangedEvent, value);
    }

    private sealed class VideoCommand(Action execute) : ICommand
    {
        public event EventHandler? CanExecuteChanged
        {
            add { }
            remove { }
        }

        public bool CanExecute(object? parameter) => true;

        public void Execute(object? parameter) => execute();
    }
}
