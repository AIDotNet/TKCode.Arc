using System.Collections.Specialized;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Interactivity;
using TKCode.Arc.Motion;

namespace TKCode.Arc;

/// <summary>
/// A persistent "now playing" player (Arc Now playing, design reference): a 336 × 64 mini bar — cover thumbnail, title
/// and artist, level bars, play and next — that grows in place, in one continuous morph, into the full player with a
/// large cover, a 54-bar waveform scrubber, the elapsed and remaining time and previous / play / next. The cover, play
/// and next buttons fly between the two layouts while the shell's height and radius follow the same spring; the other
/// parts fade out and stagger in. Pull the full cover down (more than 70px, or flick it) or press Escape to fold it back.
/// <para>
/// Give it <see cref="Tracks"/>. Playback runs on its own clock (one second per second, on to the next track at the end,
/// wrapping at both ends), so it works without audio. To drive real audio, follow <see cref="IsPlaying"/> and
/// <see cref="Index"/> (both two-way) or handle <see cref="PlayingChanged"/> and <see cref="IndexChanged"/>, report the
/// audio position through <see cref="CurrentTime"/> (each value corrects the clock, which keeps the waveform moving
/// between updates; ignored during a scrub), and set the audio position in <see cref="SeekRequested"/>.
/// </para>
/// </summary>
[TemplatePart(SlotPart, typeof(Border))]
public sealed partial class NowPlaying : TemplatedControl
{
    // The morph's corner radii (mini and full) come from the token through the theme so the corner style (square, scale)
    // reaches them; the code interpolates between each pair while the shell's height springs.
    internal static readonly StyledProperty<CornerRadius> MiniShellRadiusProperty =
        AvaloniaProperty.Register<NowPlaying, CornerRadius>(nameof(MiniShellRadius), new CornerRadius(22));

    internal static readonly StyledProperty<CornerRadius> FullShellRadiusProperty =
        AvaloniaProperty.Register<NowPlaying, CornerRadius>(nameof(FullShellRadius), new CornerRadius(30));

    internal static readonly StyledProperty<CornerRadius> MiniClipRadiusProperty =
        AvaloniaProperty.Register<NowPlaying, CornerRadius>(nameof(MiniClipRadius), new CornerRadius(21));

    internal static readonly StyledProperty<CornerRadius> FullClipRadiusProperty =
        AvaloniaProperty.Register<NowPlaying, CornerRadius>(nameof(FullClipRadius), new CornerRadius(29));

    internal static readonly StyledProperty<CornerRadius> MiniArtRadiusProperty =
        AvaloniaProperty.Register<NowPlaying, CornerRadius>(nameof(MiniArtRadius), new CornerRadius(12));

    internal static readonly StyledProperty<CornerRadius> FullArtRadiusProperty =
        AvaloniaProperty.Register<NowPlaying, CornerRadius>(nameof(FullArtRadius), new CornerRadius(20));

    internal CornerRadius MiniShellRadius { get => GetValue(MiniShellRadiusProperty); set => SetValue(MiniShellRadiusProperty, value); }

    internal CornerRadius FullShellRadius { get => GetValue(FullShellRadiusProperty); set => SetValue(FullShellRadiusProperty, value); }

    internal CornerRadius MiniClipRadius { get => GetValue(MiniClipRadiusProperty); set => SetValue(MiniClipRadiusProperty, value); }

    internal CornerRadius FullClipRadius { get => GetValue(FullClipRadiusProperty); set => SetValue(FullClipRadiusProperty, value); }

    internal CornerRadius MiniArtRadius { get => GetValue(MiniArtRadiusProperty); set => SetValue(MiniArtRadiusProperty, value); }

    internal CornerRadius FullArtRadius { get => GetValue(FullArtRadiusProperty); set => SetValue(FullArtRadiusProperty, value); }

    internal const string SlotPart = "PART_Slot";

    /// <summary>The queue. Each track needs a title, an artist and a length; artwork and peaks are optional.</summary>
    public static readonly StyledProperty<IList<NowPlayingTrack>?> TracksProperty =
        AvaloniaProperty.Register<NowPlaying, IList<NowPlayingTrack>?>(nameof(Tracks));

    /// <summary>The current track's position in <see cref="Tracks"/> (0). Two-way: skips and the end of a track change it.</summary>
    public static readonly StyledProperty<int> IndexProperty =
        AvaloniaProperty.Register<NowPlaying, int>(nameof(Index), defaultBindingMode: BindingMode.TwoWay,
            coerce: static (o, v) => ((NowPlaying)o).CoerceIndex(v));

    /// <summary>Whether the track is playing (false). Two-way: the play button and Space / Enter on it change it.</summary>
    public static readonly StyledProperty<bool> IsPlayingProperty =
        AvaloniaProperty.Register<NowPlaying, bool>(nameof(IsPlaying), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>
    /// The position reported by real audio (for example on each time update). Every new value corrects the built-in clock,
    /// which keeps moving between updates; ignored while the waveform is being dragged.
    /// </summary>
    public static readonly StyledProperty<TimeSpan> CurrentTimeProperty =
        AvaloniaProperty.Register<NowPlaying, TimeSpan>(nameof(CurrentTime));

    /// <summary>The playback position now (read only): updated on every whole second, every seek and every track change.</summary>
    public static readonly DirectProperty<NowPlaying, TimeSpan> PositionProperty =
        AvaloniaProperty.RegisterDirect<NowPlaying, TimeSpan>(nameof(Position), o => o._position);

    /// <summary>Whether the full player is open (false: the mini bar). Two-way: the mini bar, the collapse button, Escape and the pull change it.</summary>
    public static readonly StyledProperty<bool> IsExpandedProperty =
        AvaloniaProperty.Register<NowPlaying, bool>(nameof(IsExpanded), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>The accessible name of the player region ("Now playing").</summary>
    public static readonly StyledProperty<string> LabelProperty =
        AvaloniaProperty.Register<NowPlaying, string>(nameof(Label), "Now playing");

    /// <summary>The words and accessible names; replace to translate or reword.</summary>
    public static readonly StyledProperty<NowPlayingLabels> LabelsProperty =
        AvaloniaProperty.Register<NowPlaying, NowPlayingLabels>(nameof(Labels), new NowPlayingLabels());

    /// <summary>How much must have played for Previous to restart the track instead of going back (3 seconds).</summary>
    public static readonly StyledProperty<TimeSpan> RestartThresholdProperty =
        AvaloniaProperty.Register<NowPlaying, TimeSpan>(nameof(RestartThreshold), TimeSpan.FromSeconds(3));

    /// <summary>The waveform's arrow-key step (5 seconds).</summary>
    public static readonly StyledProperty<TimeSpan> SeekStepProperty =
        AvaloniaProperty.Register<NowPlaying, TimeSpan>(nameof(SeekStep), TimeSpan.FromSeconds(5));

    /// <summary>The waveform's Page Up / Page Down step (30 seconds).</summary>
    public static readonly StyledProperty<TimeSpan> SeekPageStepProperty =
        AvaloniaProperty.Register<NowPlaying, TimeSpan>(nameof(SeekPageStep), TimeSpan.FromSeconds(30));

    /// <summary>The number of waveform bars (54).</summary>
    public static readonly StyledProperty<int> PeakCountProperty =
        AvaloniaProperty.Register<NowPlaying, int>(nameof(PeakCount), NowPlayingPeaks.DefaultCount,
            coerce: static (_, v) => Math.Clamp(v, 8, 240));

    /// <summary>Raised when the track changes by a skip or because a track ended (not when <see cref="Index"/> is set from outside).</summary>
    public static readonly RoutedEvent<NowPlayingIndexChangedEventArgs> IndexChangedEvent =
        RoutedEvent.Register<NowPlaying, NowPlayingIndexChangedEventArgs>(nameof(IndexChanged), RoutingStrategies.Bubble);

    /// <summary>Raised when playback starts or pauses.</summary>
    public static readonly RoutedEvent<NowPlayingPlayingChangedEventArgs> PlayingChangedEvent =
        RoutedEvent.Register<NowPlaying, NowPlayingPlayingChangedEventArgs>(nameof(PlayingChanged), RoutingStrategies.Bubble);

    /// <summary>
    /// Raised with a new position when a waveform drag ends, on every keyboard seek, when Previous restarts the track (zero)
    /// and on <see cref="SeekTo"/>: set your audio position here.
    /// </summary>
    public static readonly RoutedEvent<NowPlayingSeekEventArgs> SeekRequestedEvent =
        RoutedEvent.Register<NowPlaying, NowPlayingSeekEventArgs>(nameof(SeekRequested), RoutingStrategies.Bubble);

    /// <summary>Raised when the full player starts to open or to fold back.</summary>
    public static readonly RoutedEvent<NowPlayingExpandedChangedEventArgs> ExpandedChangedEvent =
        RoutedEvent.Register<NowPlaying, NowPlayingExpandedChangedEventArgs>(nameof(ExpandedChanged), RoutingStrategies.Bubble);

    private readonly NowPlayingClock _clock = new();
    private TimeSpan _position;
    private INotifyCollectionChanged? _observed;
    private NowPlayingIndexChangeReason? _pendingReason;
    private NowPlayingSkipDirection? _pendingDirection;
    private NowPlayingTrack? _shownTrack;

    /// <summary>Creates a player with an empty queue.</summary>
    public NowPlaying()
    {
        BuildParts();
        SetCurrentValue(TracksProperty, new AvaloniaList<NowPlayingTrack>());
    }

    /// <inheritdoc cref="TracksProperty"/>
    public IList<NowPlayingTrack>? Tracks
    {
        get => GetValue(TracksProperty);
        set => SetValue(TracksProperty, value);
    }

    /// <inheritdoc cref="IndexProperty"/>
    public int Index
    {
        get => GetValue(IndexProperty);
        set => SetValue(IndexProperty, value);
    }

    /// <inheritdoc cref="IsPlayingProperty"/>
    public bool IsPlaying
    {
        get => GetValue(IsPlayingProperty);
        set => SetValue(IsPlayingProperty, value);
    }

    /// <inheritdoc cref="CurrentTimeProperty"/>
    public TimeSpan CurrentTime
    {
        get => GetValue(CurrentTimeProperty);
        set => SetValue(CurrentTimeProperty, value);
    }

    /// <inheritdoc cref="PositionProperty"/>
    public TimeSpan Position
    {
        get => _position;
        private set => SetAndRaise(PositionProperty, ref _position, value);
    }

    /// <inheritdoc cref="IsExpandedProperty"/>
    public bool IsExpanded
    {
        get => GetValue(IsExpandedProperty);
        set => SetValue(IsExpandedProperty, value);
    }

    /// <inheritdoc cref="LabelProperty"/>
    public string Label
    {
        get => GetValue(LabelProperty);
        set => SetValue(LabelProperty, value);
    }

    /// <inheritdoc cref="LabelsProperty"/>
    public NowPlayingLabels Labels
    {
        get => GetValue(LabelsProperty);
        set => SetValue(LabelsProperty, value);
    }

    /// <inheritdoc cref="RestartThresholdProperty"/>
    public TimeSpan RestartThreshold
    {
        get => GetValue(RestartThresholdProperty);
        set => SetValue(RestartThresholdProperty, value);
    }

    /// <inheritdoc cref="SeekStepProperty"/>
    public TimeSpan SeekStep
    {
        get => GetValue(SeekStepProperty);
        set => SetValue(SeekStepProperty, value);
    }

    /// <inheritdoc cref="SeekPageStepProperty"/>
    public TimeSpan SeekPageStep
    {
        get => GetValue(SeekPageStepProperty);
        set => SetValue(SeekPageStepProperty, value);
    }

    /// <inheritdoc cref="PeakCountProperty"/>
    public int PeakCount
    {
        get => GetValue(PeakCountProperty);
        set => SetValue(PeakCountProperty, value);
    }

    /// <inheritdoc cref="IndexChangedEvent"/>
    public event EventHandler<NowPlayingIndexChangedEventArgs>? IndexChanged
    {
        add => AddHandler(IndexChangedEvent, value);
        remove => RemoveHandler(IndexChangedEvent, value);
    }

    /// <inheritdoc cref="PlayingChangedEvent"/>
    public event EventHandler<NowPlayingPlayingChangedEventArgs>? PlayingChanged
    {
        add => AddHandler(PlayingChangedEvent, value);
        remove => RemoveHandler(PlayingChangedEvent, value);
    }

    /// <inheritdoc cref="SeekRequestedEvent"/>
    public event EventHandler<NowPlayingSeekEventArgs>? SeekRequested
    {
        add => AddHandler(SeekRequestedEvent, value);
        remove => RemoveHandler(SeekRequestedEvent, value);
    }

    /// <inheritdoc cref="ExpandedChangedEvent"/>
    public event EventHandler<NowPlayingExpandedChangedEventArgs>? ExpandedChanged
    {
        add => AddHandler(ExpandedChangedEvent, value);
        remove => RemoveHandler(ExpandedChangedEvent, value);
    }

    /// <summary>The current track, or null when the queue is empty.</summary>
    public NowPlayingTrack? CurrentTrack => Tracks is { Count: > 0 } tracks ? tracks[Math.Clamp(Index, 0, tracks.Count - 1)] : null;

    /// <summary>Opens the full player (focus goes to the collapse button when it was inside the player).</summary>
    public void Expand() => SetExpanded(true, FocusOrigin.Program);

    /// <summary>Folds the full player back into the mini bar (focus goes to the mini bar when it was inside the player).</summary>
    public void Collapse() => SetExpanded(false, FocusOrigin.Program);

    /// <summary>Opens or folds the player.</summary>
    public void ToggleExpanded() => SetExpanded(!IsExpanded, FocusOrigin.Program);

    /// <summary>Starts playback.</summary>
    public void Play() => SetCurrentValue(IsPlayingProperty, true);

    /// <summary>Pauses playback.</summary>
    public void Pause() => SetCurrentValue(IsPlayingProperty, false);

    /// <summary>Plays when paused, pauses when playing.</summary>
    public void TogglePlay() => SetCurrentValue(IsPlayingProperty, !IsPlaying);

    /// <summary>Moves to the next track (after the last comes the first); playback keeps its state.</summary>
    public void Next() => Skip(NowPlayingSkipDirection.Forward, NowPlayingIndexChangeReason.Skip);

    /// <summary>
    /// Restarts the track when more than <see cref="RestartThreshold"/> has played (raising <see cref="SeekRequested"/>
    /// with zero), otherwise moves to the previous track (before the first comes the last).
    /// </summary>
    public void Previous()
    {
        if (CurrentTrack is null)
        {
            return;
        }
        if (DisplayedSeconds > RestartThreshold.TotalSeconds)
        {
            Restart();
            return;
        }
        Skip(NowPlayingSkipDirection.Backward, NowPlayingIndexChangeReason.Skip);
    }

    /// <summary>Moves the playhead to <paramref name="position"/> (clamped to the track) and raises <see cref="SeekRequested"/>.</summary>
    public void SeekTo(TimeSpan position) => CommitSeek(position.TotalSeconds, NowPlayingSeekReason.Method);

    private int CoerceIndex(int value)
    {
        var count = Tracks?.Count ?? 0;
        return count == 0 ? 0 : Math.Clamp(value, 0, count - 1);
    }

    private void Skip(NowPlayingSkipDirection direction, NowPlayingIndexChangeReason reason)
    {
        var count = Tracks?.Count ?? 0;
        if (count == 0)
        {
            return;
        }
        var old = Index;
        var next = direction == NowPlayingSkipDirection.Forward ? (old + 1) % count : (old - 1 + count) % count;
        _pendingDirection = direction;
        _pendingReason = reason;
        if (next == old)
        {
            // A queue of one: the same track starts over with the same slide.
            OnIndexChanged(old);
            return;
        }
        SetCurrentValue(IndexProperty, next);
    }

    private void OnIndexChanged(int oldIndex)
    {
        var direction = _pendingDirection ?? (Index >= oldIndex ? NowPlayingSkipDirection.Forward : NowPlayingSkipDirection.Backward);
        var reason = _pendingReason;
        _pendingDirection = null;
        _pendingReason = null;
        LoadTrack(IsAttached, direction);
        if (reason is { } r)
        {
            RaiseEvent(new NowPlayingIndexChangedEventArgs(IndexChangedEvent, oldIndex, Index, direction, r));
        }
        Announce();
    }

    private bool IsAttached => TopLevel.GetTopLevel(this) is not null;

    private void OnTracksChanged(IList<NowPlayingTrack>? oldList, IList<NowPlayingTrack>? newList)
    {
        if (_observed is not null)
        {
            _observed.CollectionChanged -= OnTracksCollectionChanged;
        }
        _observed = newList as INotifyCollectionChanged;
        if (_observed is not null)
        {
            _observed.CollectionChanged += OnTracksCollectionChanged;
        }
        _ = oldList;
        RefreshQueue();
    }

    private void OnTracksCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e) => RefreshQueue();

    private void RefreshQueue()
    {
        CoerceValue(IndexProperty);
        if (!ReferenceEquals(CurrentTrack, _shownTrack))
        {
            LoadTrack(false, NowPlayingSkipDirection.Forward);
        }
        UpdateClockLoop();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == MiniShellRadiusProperty || change.Property == FullShellRadiusProperty
            || change.Property == MiniClipRadiusProperty || change.Property == FullClipRadiusProperty
            || change.Property == MiniArtRadiusProperty || change.Property == FullArtRadiusProperty)
        {
            OnRadiiChanged();
        }
        else if (change.Property == TracksProperty)
        {
            OnTracksChanged(change.GetOldValue<IList<NowPlayingTrack>?>(), change.GetNewValue<IList<NowPlayingTrack>?>());
        }
        else if (change.Property == IndexProperty)
        {
            OnIndexChanged(change.GetOldValue<int>());
        }
        else if (change.Property == IsPlayingProperty)
        {
            OnPlayingChanged();
            RaiseEvent(new NowPlayingPlayingChangedEventArgs(PlayingChangedEvent, IsPlaying));
            Announce();
        }
        else if (change.Property == CurrentTimeProperty)
        {
            OnCurrentTimeChanged(CurrentTime.TotalSeconds);
        }
        else if (change.Property == IsExpandedProperty)
        {
            OnExpandedChanged();
            RaiseEvent(new NowPlayingExpandedChangedEventArgs(ExpandedChangedEvent, IsExpanded));
        }
        else if (change.Property == LabelsProperty || change.Property == LabelProperty)
        {
            UpdateNames();
            UpdateDisplay(true);
        }
        else if (change.Property == PeakCountProperty)
        {
            UpdatePeaks();
        }
        else if (change.Property == SeekStepProperty || change.Property == SeekPageStepProperty)
        {
            UpdateSteps();
        }
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        if (_slot is not null)
        {
            _slot.Child = null;
        }
        _slot = e.NameScope.Find<Border>(SlotPart);
        if (_slot is not null)
        {
            _slot.Child = _root;
        }
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new NowPlayingPeer(this);

    private sealed class NowPlayingPeer(NowPlaying owner) : ControlAutomationPeer(owner)
    {
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Group;

        protected override string? GetNameCore() =>
            AutomationProperties.GetName(Owner) is { Length: > 0 } name ? name : ((NowPlaying)Owner).Label;
    }
}
