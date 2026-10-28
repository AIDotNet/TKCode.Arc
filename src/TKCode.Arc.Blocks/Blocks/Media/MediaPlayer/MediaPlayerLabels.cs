namespace TKCode.Arc;

/// <summary>
/// Every word <see cref="MediaPlayer"/> shows or reads to screen readers, in English by default. Replace the instance to
/// translate or reword; texts that take values are functions so that word order can change.
/// </summary>
public sealed class MediaPlayerLabels
{
    /// <summary>The accessible name of the whole player.</summary>
    public string PlayerName { get; init; } = "Audio player";

    /// <summary>The heading above the track list.</summary>
    public string Playlist { get; init; } = "Playlist";

    /// <summary>The play button's name while paused.</summary>
    public string Play { get; init; } = "Play";

    /// <summary>The play button's name while playing.</summary>
    public string Pause { get; init; } = "Pause";

    /// <summary>The previous-track button's name.</summary>
    public string PreviousTrack { get; init; } = "Previous track";

    /// <summary>The next-track button's name.</summary>
    public string NextTrack { get; init; } = "Next track";

    /// <summary>The mute button's name while the volume is above zero.</summary>
    public string Mute { get; init; } = "Mute audio";

    /// <summary>The mute button's name while the volume is zero.</summary>
    public string Unmute { get; init; } = "Unmute audio";

    /// <summary>The volume slider's name.</summary>
    public string Volume { get; init; } = "Volume";

    /// <summary>The name of the playing indicator in the current row.</summary>
    public string Playing { get; init; } = "Playing";

    /// <summary>The seek slider's name; the argument is the current track's title.</summary>
    public Func<string, string> Seek { get; init; } = title => $"Seek {title}";

    /// <summary>A row's name; the arguments are the title and whether pressing the row pauses it (the current, playing track).</summary>
    public Func<string, bool, string> TrackAction { get; init; } = (title, pauses) => pauses ? $"Pause {title}" : $"Play {title}";

    /// <summary>The seek slider's spoken value; the arguments are the position and the length, already formatted.</summary>
    public Func<string, string, string> SeekValue { get; init; } = (position, length) => $"{position} of {length}";

    /// <summary>The volume slider's spoken value; the argument is the volume from 0 to 100.</summary>
    public Func<int, string> VolumeValue { get; init; } = percent => $"{percent}%";

    /// <summary>Formats a time for the labels and the playlist ("0:24", "1:02:05").</summary>
    public Func<TimeSpan, string> FormatTime { get; init; } = MediaPlayerTime.Format;
}

/// <summary>Formats playback times the way the player shows them.</summary>
public static class MediaPlayerTime
{
    /// <summary>Whole seconds, rounded down: "m:ss", with hours ("h:mm:ss") from one hour on; negative times read as zero.</summary>
    public static string Format(TimeSpan time)
    {
        var seconds = time <= TimeSpan.Zero || double.IsNaN(time.TotalSeconds) ? 0L : (long)Math.Floor(time.TotalSeconds);
        var hours = seconds / 3600;
        var minutes = seconds / 60 % 60;
        var rest = seconds % 60;
        return hours > 0 ? $"{hours}:{minutes:00}:{rest:00}" : $"{seconds / 60}:{rest:00}";
    }
}
