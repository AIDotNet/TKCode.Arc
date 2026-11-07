using System.Globalization;
using TKCode.Arc.Localization;

namespace TKCode.Arc;

/// <summary>The video controls' built-in text in the current Arc language (<c>Localization/*.xlf</c>) and time formats.</summary>
internal static class VideoText
{
    public static ArcLanguageManager? Languages => ArcLanguageManager.Current;

    public static string Get(VideoLangResourceKind kind) => VideoLangResources.Get(kind);

    public static string Format(VideoLangResourceKind kind, params object?[] arguments) => VideoLangResources.Format(kind, arguments);

    /// <summary>Whole seconds, rounded down: "m:ss", or "h:mm:ss" from one hour on; negative times read as zero.</summary>
    public static string Clock(TimeSpan time)
    {
        var seconds = time <= TimeSpan.Zero || double.IsNaN(time.TotalSeconds) ? 0L : (long)Math.Floor(time.TotalSeconds);
        var hours = seconds / 3600;
        var minutes = seconds / 60 % 60;
        var rest = seconds % 60;
        return hours > 0
            ? string.Create(CultureInfo.InvariantCulture, $"{hours}:{minutes:00}:{rest:00}")
            : string.Create(CultureInfo.InvariantCulture, $"{seconds / 60}:{rest:00}");
    }

    /// <summary>A time read aloud: "1 minute 5 seconds", "1 hour 2 minutes", "0 seconds" (zh-CN "1 分 5 秒").</summary>
    public static string Spoken(TimeSpan time)
    {
        var seconds = time <= TimeSpan.Zero || double.IsNaN(time.TotalSeconds) ? 0L : (long)Math.Floor(time.TotalSeconds);
        var hours = seconds / 3600;
        var minutes = seconds / 60 % 60;
        var rest = seconds % 60;
        var parts = new List<string>(3);
        if (hours > 0)
        {
            parts.Add(Format(hours == 1 ? VideoLangResourceKind.DurationHour : VideoLangResourceKind.DurationHours, hours));
        }
        if (minutes > 0)
        {
            parts.Add(Format(minutes == 1 ? VideoLangResourceKind.DurationMinute : VideoLangResourceKind.DurationMinutes, minutes));
        }
        if (rest > 0 || parts.Count == 0)
        {
            parts.Add(Format(rest == 1 ? VideoLangResourceKind.DurationSecond : VideoLangResourceKind.DurationSeconds, rest));
        }
        return string.Join(" ", parts);
    }

    /// <summary>A playback rate as the speed menu shows it: "Normal" for 1, otherwise "1.5×".</summary>
    public static string Rate(double rate) =>
        Math.Abs(rate - 1) < 0.001 ? Get(VideoLangResourceKind.SpeedNormal) : rate.ToString("0.##", CultureInfo.InvariantCulture) + "×";

    /// <summary>A label with its keyboard shortcut: "Pause (K)".</summary>
    public static string WithShortcut(string label, string key) => Format(VideoLangResourceKind.Shortcut, label, key);

    /// <summary>The localized message for a failure kind.</summary>
    public static string Error(VideoErrorKind kind) => Get(kind switch
    {
        VideoErrorKind.Aborted => VideoLangResourceKind.ErrorAborted,
        VideoErrorKind.Network => VideoLangResourceKind.ErrorNetwork,
        VideoErrorKind.Decode => VideoLangResourceKind.ErrorDecode,
        VideoErrorKind.Unsupported => VideoLangResourceKind.ErrorUnsupported,
        _ => VideoLangResourceKind.ErrorBackend,
    });
}
