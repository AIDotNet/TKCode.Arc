using Avalonia;
using Avalonia.Automation;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Interactivity;
using Avalonia.Metadata;

namespace TKCode.Arc;

/// <summary>Which change moved a <see cref="MediaPlayer"/> to another track.</summary>
public enum MediaTrackChangeSource
{
    /// <summary>Code set <see cref="MediaPlayer.SelectedIndex"/> or called a method.</summary>
    Code,

    /// <summary>The previous or next button.</summary>
    Skip,

    /// <summary>A playlist row.</summary>
    Playlist,
}

/// <summary>Carries the track a <see cref="MediaPlayer"/> moved away from and the one it moved to.</summary>
public sealed class MediaTrackChangedEventArgs(RoutedEvent routedEvent, MediaTrack? oldTrack, MediaTrack? newTrack, MediaTrackChangeSource changeSource)
    : RoutedEventArgs(routedEvent)
{
    /// <summary>The previous track; null when there was none.</summary>
    public MediaTrack? OldTrack { get; } = oldTrack;

    /// <summary>The new current track; null when the playlist is empty.</summary>
    public MediaTrack? NewTrack { get; } = newTrack;

    /// <summary>What moved the player.</summary>
    public MediaTrackChangeSource ChangeSource { get; } = changeSource;
}

/// <summary>Carries a track and, for failures, the backend's message.</summary>
public sealed class MediaTrackEventArgs(RoutedEvent routedEvent, MediaTrack track, string? message = null) : RoutedEventArgs(routedEvent)
{
    /// <summary>The track concerned.</summary>
    public MediaTrack Track { get; } = track;

    /// <summary>Why playback failed (<see cref="MediaPlayer.PlaybackFailed"/> only).</summary>
    public string? Message { get; } = message;
}

/// <summary>
/// An audio player with a playlist: the current track's cover, title and artist, previous / play / next buttons, a seek
/// bar with the elapsed and total time, a mute button with a volume slider, and the tracks as rows that play when pressed.
/// Playback goes through <see cref="ArcAudio.Backend"/> (or <see cref="AudioBackend"/>): add <c>TKCode.Arc.Audio.Native</c>
/// on desktop or <c>TKCode.Arc.Audio.Browser</c> in WebAssembly for sound; the default backend keeps time silently.
/// Skipping wraps around and keeps playing or paused as it was; a track that reaches its end stops and returns to 0:00.
/// <code>
/// &lt;arc:MediaPlayer&gt;
///     &lt;arc:MediaTrack Title="Harbor lights" Artist="Low Static" Album="Night ferry" /&gt;
/// &lt;/arc:MediaPlayer&gt;
/// </code>
/// </summary>
[TemplatePart(CoverPart, typeof(MediaPlayerSwap))]
[TemplatePart(TitlePart, typeof(MediaPlayerSwap))]
[TemplatePart(PlayIconPart, typeof(MediaPlayerSwap))]
[TemplatePart(PreviousPart, typeof(MediaPlayerButton))]
[TemplatePart(PlayPart, typeof(MediaPlayerButton))]
[TemplatePart(NextPart, typeof(MediaPlayerButton))]
[TemplatePart(SeekPart, typeof(MediaPlayerRange))]
[TemplatePart(ElapsedPart, typeof(TextBlock))]
[TemplatePart(LengthPart, typeof(TextBlock))]
[TemplatePart(MutePart, typeof(MediaPlayerButton))]
[TemplatePart(MuteGlyphPart, typeof(MediaPlayerGlyph))]
[TemplatePart(VolumePart, typeof(MediaPlayerRange))]
[TemplatePart(PlaylistTitlePart, typeof(TextBlock))]
[TemplatePart(QueuePart, typeof(MediaPlayerQueue))]
[PseudoClasses(PlayingPseudoClass, MutedPseudoClass, CompactPseudoClass, FramedPseudoClass)]
public partial class MediaPlayer : TemplatedControl
{
    internal const string CoverPart = "PART_Cover";
    internal const string TitlePart = "PART_Title";
    internal const string PlayIconPart = "PART_PlayIcon";
    internal const string PreviousPart = "PART_Previous";
    internal const string PlayPart = "PART_Play";
    internal const string NextPart = "PART_Next";
    internal const string SeekPart = "PART_Seek";
    internal const string ElapsedPart = "PART_Elapsed";
    internal const string LengthPart = "PART_Length";
    internal const string MutePart = "PART_Mute";
    internal const string MuteGlyphPart = "PART_MuteGlyph";
    internal const string VolumePart = "PART_Volume";
    internal const string PlaylistTitlePart = "PART_PlaylistTitle";
    internal const string QueuePart = "PART_Queue";
    internal const string PlayingPseudoClass = ":playing";
    internal const string MutedPseudoClass = ":muted";
    internal const string CompactPseudoClass = ":compact";
    internal const string FramedPseudoClass = ":framed";

    #region Properties

    /// <summary>The index of the current track in <see cref="Tracks"/> (two-way); −1 when the playlist is empty.</summary>
    public static readonly StyledProperty<int> SelectedIndexProperty =
        AvaloniaProperty.Register<MediaPlayer, int>(nameof(SelectedIndex), 0, defaultBindingMode: BindingMode.TwoWay);

    /// <summary>Whether the current track is playing (two-way): set it to play or pause.</summary>
    public static readonly StyledProperty<bool> IsPlayingProperty =
        AvaloniaProperty.Register<MediaPlayer, bool>(nameof(IsPlaying), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>The output volume from 0 to 1 (two-way, 0.7); 0 is muted.</summary>
    public static readonly StyledProperty<double> VolumeProperty =
        AvaloniaProperty.Register<MediaPlayer, double>(nameof(Volume), 0.7, defaultBindingMode: BindingMode.TwoWay,
            coerce: (_, value) => double.IsNaN(value) ? 0 : Math.Clamp(value, 0, 1));

    /// <summary>The volume the mute button restores when nothing louder was muted before (0.7).</summary>
    public static readonly StyledProperty<double> DefaultVolumeProperty =
        AvaloniaProperty.Register<MediaPlayer, double>(nameof(DefaultVolume), 0.7);

    /// <summary>Draws a bordered card with rounded corners (true); false lays the player flat on the page.</summary>
    public static readonly StyledProperty<bool> IsFramedProperty =
        AvaloniaProperty.Register<MediaPlayer, bool>(nameof(IsFramed), true);

    /// <summary>The backend that plays the audio; null uses <see cref="ArcAudio.Backend"/>. Set it before the player is shown.</summary>
    public static readonly StyledProperty<IAudioBackend?> AudioBackendProperty =
        AvaloniaProperty.Register<MediaPlayer, IAudioBackend?>(nameof(AudioBackend));

    /// <summary>Below this width the player tightens (smaller paddings, buttons and title; a taller cover). 420.</summary>
    public static readonly StyledProperty<double> CompactWidthProperty =
        AvaloniaProperty.Register<MediaPlayer, double>(nameof(CompactWidth), 420);

    /// <summary>Every word the player shows or reads to screen readers.</summary>
    public static readonly StyledProperty<MediaPlayerLabels> LabelsProperty =
        AvaloniaProperty.Register<MediaPlayer, MediaPlayerLabels>(nameof(Labels), new MediaPlayerLabels());

    /// <summary>The current track; null when the playlist is empty.</summary>
    public static readonly DirectProperty<MediaPlayer, MediaTrack?> SelectedTrackProperty =
        AvaloniaProperty.RegisterDirect<MediaPlayer, MediaTrack?>(nameof(SelectedTrack), o => o.SelectedTrack);

    /// <summary>How far into the current track playback is, as last reported by the backend.</summary>
    public static readonly DirectProperty<MediaPlayer, TimeSpan> PositionProperty =
        AvaloniaProperty.RegisterDirect<MediaPlayer, TimeSpan>(nameof(Position), o => o.Position);

    /// <summary>The current track's length: what the backend reports once it is loaded, the track's own <see cref="MediaTrack.Duration"/> before.</summary>
    public static readonly DirectProperty<MediaPlayer, TimeSpan> DurationProperty =
        AvaloniaProperty.RegisterDirect<MediaPlayer, TimeSpan>(nameof(Duration), o => o.Duration);

    /// <summary>Whether the volume is zero (the mute button then offers to unmute).</summary>
    public static readonly DirectProperty<MediaPlayer, bool> IsMutedProperty =
        AvaloniaProperty.RegisterDirect<MediaPlayer, bool>(nameof(IsMuted), o => o.IsMuted);

    /// <summary>Whether the compact layout applies (the player is narrower than <see cref="CompactWidth"/>).</summary>
    public static readonly DirectProperty<MediaPlayer, bool> IsCompactProperty =
        AvaloniaProperty.RegisterDirect<MediaPlayer, bool>(nameof(IsCompact), o => o.IsCompact);

    /// <summary>Whether the backend makes sound (false for the silent default backend).</summary>
    public static readonly DirectProperty<MediaPlayer, bool> IsAudibleProperty =
        AvaloniaProperty.RegisterDirect<MediaPlayer, bool>(nameof(IsAudible), o => o.IsAudible);

    #endregion

    #region Events

    /// <summary>Raised when the current track changes.</summary>
    public static readonly RoutedEvent<MediaTrackChangedEventArgs> SelectedTrackChangedEvent =
        RoutedEvent.Register<MediaPlayer, MediaTrackChangedEventArgs>(nameof(SelectedTrackChanged), RoutingStrategies.Bubble);

    /// <summary>Raised when the current track played to its end (the player has stopped and returned to the start).</summary>
    public static readonly RoutedEvent<MediaTrackEventArgs> TrackEndedEvent =
        RoutedEvent.Register<MediaPlayer, MediaTrackEventArgs>(nameof(TrackEnded), RoutingStrategies.Bubble);

    /// <summary>Raised when the backend could not load or play the current track.</summary>
    public static readonly RoutedEvent<MediaTrackEventArgs> PlaybackFailedEvent =
        RoutedEvent.Register<MediaPlayer, MediaTrackEventArgs>(nameof(PlaybackFailed), RoutingStrategies.Bubble);

    /// <inheritdoc cref="SelectedTrackChangedEvent"/>
    public event EventHandler<MediaTrackChangedEventArgs>? SelectedTrackChanged
    {
        add => AddHandler(SelectedTrackChangedEvent, value);
        remove => RemoveHandler(SelectedTrackChangedEvent, value);
    }

    /// <inheritdoc cref="TrackEndedEvent"/>
    public event EventHandler<MediaTrackEventArgs>? TrackEnded
    {
        add => AddHandler(TrackEndedEvent, value);
        remove => RemoveHandler(TrackEndedEvent, value);
    }

    /// <inheritdoc cref="PlaybackFailedEvent"/>
    public event EventHandler<MediaTrackEventArgs>? PlaybackFailed
    {
        add => AddHandler(PlaybackFailedEvent, value);
        remove => RemoveHandler(PlaybackFailedEvent, value);
    }

    #endregion

    private MediaTrack? _selectedTrack;
    private TimeSpan _position;
    private TimeSpan _duration;
    private bool _isMuted;
    private bool _isCompact;
    private bool _isAudible;

    public MediaPlayer()
    {
        Tracks.CollectionChanged += (_, _) => OnTracksChanged();
    }

    /// <summary>The playlist, in order.</summary>
    [Content]
    public AvaloniaList<MediaTrack> Tracks { get; } = [];

    /// <inheritdoc cref="SelectedIndexProperty"/>
    public int SelectedIndex { get => GetValue(SelectedIndexProperty); set => SetValue(SelectedIndexProperty, value); }

    /// <inheritdoc cref="IsPlayingProperty"/>
    public bool IsPlaying { get => GetValue(IsPlayingProperty); set => SetValue(IsPlayingProperty, value); }

    /// <inheritdoc cref="VolumeProperty"/>
    public double Volume { get => GetValue(VolumeProperty); set => SetValue(VolumeProperty, value); }

    /// <inheritdoc cref="DefaultVolumeProperty"/>
    public double DefaultVolume { get => GetValue(DefaultVolumeProperty); set => SetValue(DefaultVolumeProperty, value); }

    /// <inheritdoc cref="IsFramedProperty"/>
    public bool IsFramed { get => GetValue(IsFramedProperty); set => SetValue(IsFramedProperty, value); }

    /// <inheritdoc cref="AudioBackendProperty"/>
    public IAudioBackend? AudioBackend { get => GetValue(AudioBackendProperty); set => SetValue(AudioBackendProperty, value); }

    /// <inheritdoc cref="CompactWidthProperty"/>
    public double CompactWidth { get => GetValue(CompactWidthProperty); set => SetValue(CompactWidthProperty, value); }

    /// <inheritdoc cref="LabelsProperty"/>
    public MediaPlayerLabels Labels { get => GetValue(LabelsProperty); set => SetValue(LabelsProperty, value); }

    /// <inheritdoc cref="SelectedTrackProperty"/>
    public MediaTrack? SelectedTrack { get => _selectedTrack; private set => SetAndRaise(SelectedTrackProperty, ref _selectedTrack, value); }

    /// <inheritdoc cref="PositionProperty"/>
    public TimeSpan Position { get => _position; private set => SetAndRaise(PositionProperty, ref _position, value); }

    /// <inheritdoc cref="DurationProperty"/>
    public TimeSpan Duration { get => _duration; private set => SetAndRaise(DurationProperty, ref _duration, value); }

    /// <inheritdoc cref="IsMutedProperty"/>
    public bool IsMuted { get => _isMuted; private set => SetAndRaise(IsMutedProperty, ref _isMuted, value); }

    /// <inheritdoc cref="IsCompactProperty"/>
    public bool IsCompact { get => _isCompact; private set => SetAndRaise(IsCompactProperty, ref _isCompact, value); }

    /// <inheritdoc cref="IsAudibleProperty"/>
    public bool IsAudible { get => _isAudible; private set => SetAndRaise(IsAudibleProperty, ref _isAudible, value); }
}
