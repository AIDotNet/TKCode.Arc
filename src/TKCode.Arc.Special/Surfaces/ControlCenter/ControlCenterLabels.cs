using System.Globalization;

namespace TKCode.Arc;

/// <summary>
/// The words and formats of a <see cref="ControlCenter"/>: tile titles and states, meter labels, detail titles, button
/// names and the accessible names and value texts. Replace the object (or set its properties) to translate or reword;
/// sentences with numbers or times are delegates.
/// </summary>
public sealed class ControlCenterLabels
{
    /// <summary>The panel's accessible name ("Quick settings").</summary>
    public string PanelName { get; set; } = "Quick settings";

    /// <summary>The Focus tile's title while no session runs, and the Focus detail's title ("Focus").</summary>
    public string FocusTitle { get; set; } = "Focus";

    /// <summary>The Notifications tile's title and the Notifications detail's title ("Notifications").</summary>
    public string NotificationsTitle { get; set; } = "Notifications";

    /// <summary>The Presenting tile's title ("Presenting").</summary>
    public string PresentingTitle { get; set; } = "Presenting";

    /// <summary>The state line of a tile that is off ("Off").</summary>
    public string Off { get; set; } = "Off";

    /// <summary>The Notifications tile's state line while notifications are off ("Paused").</summary>
    public string NotificationsPaused { get; set; } = "Paused";

    /// <summary>The Presenting tile's state line while presenting ("Popups held back").</summary>
    public string PresentingOn { get; set; } = "Popups held back";

    /// <summary>The volume meter's label ("Alert volume").</summary>
    public string VolumeLabel { get; set; } = "Alert volume";

    /// <summary>The text-size meter's label ("Text size").</summary>
    public string TextSizeLabel { get; set; } = "Text size";

    /// <summary>The sample that grows and shrinks with the text size ("Aa").</summary>
    public string TextPreview { get; set; } = "Aa";

    /// <summary>The Focus tile's chevron ("Focus session settings").</summary>
    public string FocusSettingsName { get; set; } = "Focus session settings";

    /// <summary>The Notifications tile's chevron ("Notification channels").</summary>
    public string ChannelSettingsName { get; set; } = "Notification channels";

    /// <summary>The detail's close button ("Close").</summary>
    public string Close { get; set; } = "Close";

    /// <summary>The accessible name of the mode switch ("Mode").</summary>
    public string ModeGroupName { get; set; } = "Mode";

    /// <summary>The accessible name of the dial ("Session length").</summary>
    public string SessionLengthName { get; set; } = "Session length";

    /// <summary>The unit after the dial's number ("min").</summary>
    public string MinutesUnit { get; set; } = "min";

    /// <summary>The button that starts a session ("Start session").</summary>
    public string StartSession { get; set; } = "Start session";

    /// <summary>The button that applies a new mode or length to the running session ("Update session").</summary>
    public string UpdateSession { get; set; } = "Update session";

    /// <summary>The button that ends the running session ("End session").</summary>
    public string EndSession { get; set; } = "End session";

    /// <summary>The master switch's label in the Notifications detail ("Every notification").</summary>
    public string AllNotifications { get; set; } = "Every notification";

    /// <summary>The master switch's second line ("Master switch for every channel").</summary>
    public string AllNotificationsDescription { get; set; } = "Master switch for every channel";

    /// <summary>
    /// Formats a clock time: the end of a session. The default is the short en-US time without a leading zero
    /// ("12:13 PM"), whatever the current culture.
    /// </summary>
    public Func<DateTimeOffset, string> FormatTime { get; set; } =
        static time => time.ToString("h:mm tt", CultureInfo.InvariantCulture);

    /// <summary>The line with the end of a session, given the formatted time ("Until 12:13 PM").</summary>
    public Func<string, string> Until { get; set; } = static time => $"Until {time}";

    /// <summary>The Notifications tile's state line: channels on, channels in all ("2 of 3 channels").</summary>
    public Func<int, int, string> ChannelCount { get; set; } = static (on, total) => $"{on} of {total} channels";

    /// <summary>A meter's value ("64%").</summary>
    public Func<int, string> Percent { get; set; } = static value => value.ToString(CultureInfo.InvariantCulture) + "%";

    /// <summary>The dial's value text for assistive technology: minutes and the formatted end ("50 minutes, until 12:13 PM").</summary>
    public Func<int, string, string> DialValueText { get; set; } =
        static (minutes, time) => $"{minutes.ToString(CultureInfo.InvariantCulture)} minutes, until {time}";

    /// <summary>
    /// The footer of the Notifications detail, given the first names of the people ("Always let through: Mara, Oskar and
    /// Ines"; four or more: "Always let through: Mara, Oskar and 2 more").
    /// </summary>
    public Func<IReadOnlyList<string>, string> PeopleSentence { get; set; } = DefaultPeopleSentence;

    private static string DefaultPeopleSentence(IReadOnlyList<string> names)
    {
        string who;
        switch (names.Count)
        {
            case 0:
                return "";
            case 1:
                who = names[0];
                break;
            case 2:
                who = $"{names[0]} and {names[1]}";
                break;
            case 3:
                who = $"{names[0]}, {names[1]} and {names[2]}";
                break;
            default:
                who = $"{names[0]}, {names[1]} and {(names.Count - 2).ToString(CultureInfo.InvariantCulture)} more";
                break;
        }
        return "Always let through: " + who;
    }
}
