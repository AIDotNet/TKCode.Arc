using Avalonia.Interactivity;
using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>
/// One track of a <see cref="NowPlaying"/> queue: its id, title, artist, length, artwork and waveform peaks. Set the
/// values before the track is shown; the player reads them when the track becomes current (replace the item in
/// <see cref="NowPlaying.Tracks"/> to change a track that is showing).
/// </summary>
public sealed class NowPlayingTrack
{
    /// <summary>Creates an empty track.</summary>
    public NowPlayingTrack()
    {
    }

    /// <summary>Creates a track with an id, title, artist and length.</summary>
    public NowPlayingTrack(string id, string title, string artist, TimeSpan duration)
    {
        Id = id;
        Title = title;
        Artist = artist;
        Duration = duration;
    }

    /// <summary>A stable id; it also seeds the generated waveform when <see cref="Peaks"/> is not set.</summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>The track title (one line, ends with an ellipsis when it does not fit).</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>The artist line under the title.</summary>
    public string Artist { get; set; } = string.Empty;

    /// <summary>The length of the track; the clock moves to the next track when it is reached.</summary>
    public TimeSpan Duration { get; set; }

    /// <summary>The cover image, shown square and center-cropped; null shows the muted surface.</summary>
    public IImage? Artwork { get; set; }

    /// <summary>A description of the cover for screen readers; null makes the cover decorative.</summary>
    public string? ArtworkAlt { get; set; }

    /// <summary>
    /// The waveform bar heights from 0 to 1, one per bar. Null generates a fixed shape from <see cref="Id"/>
    /// (<see cref="NowPlaying.PeakCount"/> bars); a list of another length is resampled to the bar count.
    /// </summary>
    public IReadOnlyList<double>? Peaks { get; set; }
}

/// <summary>Which way the queue moved: decides the side the new artwork slides in from.</summary>
public enum NowPlayingSkipDirection
{
    /// <summary>To the next track: the old cover leaves to the left and the new one comes in from the right.</summary>
    Forward,

    /// <summary>To the previous track: the old cover leaves to the right and the new one comes in from the left.</summary>
    Backward
}

/// <summary>Why the current track changed.</summary>
public enum NowPlayingIndexChangeReason
{
    /// <summary>Next or Previous (a button, a key or a method call).</summary>
    Skip,

    /// <summary>The track played to its end and the next one started.</summary>
    Ended
}

/// <summary>What asked for a new playback position.</summary>
public enum NowPlayingSeekReason
{
    /// <summary>A press or drag on the waveform ended (the position where it was released).</summary>
    Drag,

    /// <summary>An arrow, Page Up / Page Down, Home or End key on the waveform.</summary>
    Keyboard,

    /// <summary>Previous restarted the track because more than the restart threshold had played (always zero).</summary>
    Restart,

    /// <summary>A call to <see cref="NowPlaying.SeekTo"/>.</summary>
    Method
}

/// <summary>Arguments of <see cref="NowPlaying.IndexChanged"/>.</summary>
public sealed class NowPlayingIndexChangedEventArgs : RoutedEventArgs
{
    /// <summary>Creates the arguments.</summary>
    public NowPlayingIndexChangedEventArgs(RoutedEvent routedEvent, int oldIndex, int newIndex,
        NowPlayingSkipDirection direction, NowPlayingIndexChangeReason reason)
        : base(routedEvent)
    {
        OldIndex = oldIndex;
        NewIndex = newIndex;
        Direction = direction;
        Reason = reason;
    }

    /// <summary>The index before the change.</summary>
    public int OldIndex { get; }

    /// <summary>The index of the track that is now current.</summary>
    public int NewIndex { get; }

    /// <summary>Which way the queue moved (it wraps at both ends).</summary>
    public NowPlayingSkipDirection Direction { get; }

    /// <summary>Whether a skip or the end of a track caused the change.</summary>
    public NowPlayingIndexChangeReason Reason { get; }
}

/// <summary>Arguments of <see cref="NowPlaying.SeekRequested"/>: set your audio position here.</summary>
public sealed class NowPlayingSeekEventArgs : RoutedEventArgs
{
    /// <summary>Creates the arguments.</summary>
    public NowPlayingSeekEventArgs(RoutedEvent routedEvent, TimeSpan position, NowPlayingSeekReason reason)
        : base(routedEvent)
    {
        Position = position;
        Reason = reason;
    }

    /// <summary>The new position from the start of the track.</summary>
    public TimeSpan Position { get; }

    /// <summary>What asked for it.</summary>
    public NowPlayingSeekReason Reason { get; }
}

/// <summary>Arguments of <see cref="NowPlaying.PlayingChanged"/>.</summary>
public sealed class NowPlayingPlayingChangedEventArgs : RoutedEventArgs
{
    /// <summary>Creates the arguments.</summary>
    public NowPlayingPlayingChangedEventArgs(RoutedEvent routedEvent, bool isPlaying)
        : base(routedEvent)
    {
        IsPlaying = isPlaying;
    }

    /// <summary>True when playback started, false when it paused.</summary>
    public bool IsPlaying { get; }
}

/// <summary>Arguments of <see cref="NowPlaying.ExpandedChanged"/>.</summary>
public sealed class NowPlayingExpandedChangedEventArgs : RoutedEventArgs
{
    /// <summary>Creates the arguments.</summary>
    public NowPlayingExpandedChangedEventArgs(RoutedEvent routedEvent, bool isExpanded)
        : base(routedEvent)
    {
        IsExpanded = isExpanded;
    }

    /// <summary>True when the full player opens, false when it folds back into the mini bar.</summary>
    public bool IsExpanded { get; }
}
