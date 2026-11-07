using System.Globalization;

namespace TKCode.Arc;

/// <summary>
/// Every word <see cref="NowPlaying"/> shows or says, with English defaults. Patterns use <c>{0}</c>, <c>{1}</c> ... as
/// noted on each; replace the object (or set its properties before the player is shown) to translate or reword.
/// </summary>
public sealed class NowPlayingLabels
{
    /// <summary>The mini bar's main button, which opens the full player: <c>{0}</c> title, <c>{1}</c> artist ("Expand player, {0} by {1}").</summary>
    public string ExpandButton { get; set; } = "Expand player, {0} by {1}";

    /// <summary>The round button on the artwork that folds the player back ("Collapse player").</summary>
    public string CollapseButton { get; set; } = "Collapse player";

    /// <summary>The play button while paused: <c>{0}</c> title ("Play {0}").</summary>
    public string PlayButton { get; set; } = "Play {0}";

    /// <summary>The play button while playing: <c>{0}</c> title ("Pause {0}").</summary>
    public string PauseButton { get; set; } = "Pause {0}";

    /// <summary>The next button ("Next track").</summary>
    public string NextButton { get; set; } = "Next track";

    /// <summary>The previous button ("Previous track").</summary>
    public string PreviousButton { get; set; } = "Previous track";

    /// <summary>The waveform slider's name ("Position").</summary>
    public string Position { get; set; } = "Position";

    /// <summary>The waveform slider's spoken value: <c>{0}</c> elapsed, <c>{1}</c> length, both m:ss ("{0} of {1}").</summary>
    public string PositionValue { get; set; } = "{0} of {1}";

    /// <summary>What the live region says on a track change, play and pause: <c>{0}</c> title, <c>{1}</c> artist, <c>{2}</c> state ("{0} by {1}, {2}").</summary>
    public string Announcement { get; set; } = "{0} by {1}, {2}";

    /// <summary>The state word while playing ("playing").</summary>
    public string Playing { get; set; } = "playing";

    /// <summary>The state word while paused ("paused").</summary>
    public string Paused { get; set; } = "paused";

    /// <summary>The sign before the remaining time at the right of the time row (U+2212 minus).</summary>
    public string RemainingSign { get; set; } = "−";

    internal static string Format(string pattern, params object?[] args)
    {
        try
        {
            return string.Format(CultureInfo.CurrentCulture, pattern, args);
        }
        catch (FormatException)
        {
            return pattern;
        }
    }
}
