namespace TKCode.Arc;

/// <summary>
/// Implemented by an <see cref="IVideoPlayer"/> that reads the subtitles and captions stored in the media itself (MP4 timed
/// text, Matroska text tracks). <see cref="VideoPlayer"/> turns the tracks into <see cref="VideoTextTrack"/>s with
/// <see cref="VideoTextTrack.IsEmbedded"/> set, and draws their cues like any other track.
/// </summary>
/// <remarks>
/// The events are raised on the player's context (the UI thread for players created there). Nothing is delivered until a
/// track is selected; the player then raises <see cref="EmbeddedCue"/> with that track's cues as the media reaches them
/// (a backend may send them ahead of time). Loading other media clears the tracks and the selection.
/// </remarks>
public interface IVideoEmbeddedTextTracks
{
    /// <summary>The media's text tracks, known some time after loading starts (empty before and for media without any).</summary>
    IReadOnlyList<VideoEmbeddedTextTrackInfo> EmbeddedTextTracks { get; }

    /// <summary>Raised when <see cref="EmbeddedTextTracks"/> changes.</summary>
    event EventHandler? EmbeddedTextTracksChanged;

    /// <summary>Raised with a cue of the selected track.</summary>
    event EventHandler<VideoEmbeddedCueEventArgs>? EmbeddedCue;

    /// <summary>Selects the track whose cues are delivered, by <see cref="VideoEmbeddedTextTrackInfo.Index"/>; -1 selects none.</summary>
    void SelectEmbeddedTextTrack(int index);
}

/// <summary>A text track stored in the media.</summary>
/// <param name="Index">The track's index for <see cref="IVideoEmbeddedTextTracks.SelectEmbeddedTextTrack"/>.</param>
/// <param name="Kind">Subtitles or captions.</param>
/// <param name="Language">The language tag (BCP 47 or ISO 639-2), or null when the media names none.</param>
/// <param name="Label">The track's name in the media or the system's name for its language, or null.</param>
public sealed record VideoEmbeddedTextTrackInfo(int Index, VideoTextTrackKind Kind, string? Language, string? Label);

/// <summary>A cue of the selected embedded text track.</summary>
public sealed class VideoEmbeddedCueEventArgs : EventArgs
{
    /// <summary>Creates the arguments.</summary>
    public VideoEmbeddedCueEventArgs(int track, TimeSpan start, TimeSpan? end, string text)
    {
        Track = track;
        Start = start;
        End = end;
        Text = text;
    }

    /// <summary>The track's <see cref="VideoEmbeddedTextTrackInfo.Index"/>.</summary>
    public int Track { get; }

    /// <summary>When the cue starts showing (media time).</summary>
    public TimeSpan Start { get; }

    /// <summary>When it stops, or null when the backend does not know: it shows until the next cue of the track, which ends it.</summary>
    public TimeSpan? End { get; }

    /// <summary>
    /// The text as WebVTT cue text: <c>&lt;b&gt;</c>, <c>&lt;i&gt;</c>, <c>&lt;u&gt;</c> and the entities <c>&amp;amp;</c>,
    /// <c>&amp;lt;</c>, <c>&amp;gt;</c>; lines separated by <c>\n</c>. Empty when only the previous open cue ends.
    /// </summary>
    public string Text { get; }
}
