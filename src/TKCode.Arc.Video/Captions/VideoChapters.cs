namespace TKCode.Arc;

/// <summary>
/// Chapter lookups over a <see cref="VideoTextTrackKind.Chapters"/> track's cues (sorted by start, as
/// <see cref="VideoTextTrack.Cues"/> is): the chapter at a time for the seek bar's hover tip, and the next / previous
/// chapter start for PageDown / PageUp (design/components/video/video.md section 9). All lookups are binary searches.
/// </summary>
internal static class VideoChapters
{
    /// <summary>How far into a chapter "previous" still means the previous chapter rather than this one's start (own choice, as most players do).</summary>
    public static readonly TimeSpan RestartThreshold = TimeSpan.FromSeconds(1);

    /// <summary>The index of the chapter shown at <paramref name="time"/> (the latest-starting cue, when it contains the time), or -1.</summary>
    public static int IndexAt(IReadOnlyList<VideoCue> chapters, TimeSpan time)
    {
        // Chapters follow one another without overlapping, so only the latest-starting one can contain the time.
        var i = LastStartingAtOrBefore(chapters, time);
        return i >= 0 && chapters[i].IsActiveAt(time) ? i : -1;
    }

    /// <summary>The chapter shown at <paramref name="time"/>, or null (before the first chapter, in a gap, or after the last).</summary>
    public static VideoCue? At(IReadOnlyList<VideoCue> chapters, TimeSpan time)
    {
        var index = IndexAt(chapters, time);
        return index >= 0 ? chapters[index] : null;
    }

    /// <summary>The start of the first chapter that starts after <paramref name="time"/>, or null when there is none (PageDown).</summary>
    public static TimeSpan? NextStart(IReadOnlyList<VideoCue> chapters, TimeSpan time)
    {
        var next = LastStartingAtOrBefore(chapters, time) + 1;
        return next < chapters.Count ? chapters[next].Start : null;
    }

    /// <summary>
    /// Where PageUp goes (null when there is no chapter at or before <paramref name="time"/>): the start of the current
    /// chapter when more than <paramref name="threshold"/> (default <see cref="RestartThreshold"/>) into it, else the start of the one before.
    /// </summary>
    public static TimeSpan? PreviousStart(IReadOnlyList<VideoCue> chapters, TimeSpan time, TimeSpan? threshold = null)
    {
        var current = LastStartingAtOrBefore(chapters, time);
        if (current < 0)
        {
            return null;
        }
        if (time - chapters[current].Start > (threshold ?? RestartThreshold) || current == 0)
        {
            return chapters[current].Start;
        }
        return chapters[current - 1].Start;
    }

    /// <summary>Index of the last cue whose start is at or before <paramref name="time"/>, or -1.</summary>
    internal static int LastStartingAtOrBefore(IReadOnlyList<VideoCue> cues, TimeSpan time)
    {
        var lo = 0;
        var hi = cues.Count;
        while (lo < hi)
        {
            var mid = lo + ((hi - lo) >> 1);
            if (cues[mid].Start <= time)
            {
                lo = mid + 1;
            }
            else
            {
                hi = mid;
            }
        }
        return lo - 1;
    }
}
