namespace TKCode.Arc;

/// <summary>
/// Every word <see cref="SocialProofToast"/> shows or says, in English by default. Replace the whole object to localize
/// or reword; the relative times and the names that include the toast's sentence are format functions.
/// </summary>
public sealed class SocialProofToastLabels
{
    /// <summary>The English defaults.</summary>
    public static SocialProofToastLabels Default { get; } = new();

    /// <summary>The accessible name of the stack's region.</summary>
    public string Region { get; init; } = "Recent activity";

    /// <summary>The verified badge's label when the event does not give its own.</summary>
    public string Verified { get; init; } = "Verified";

    /// <summary>What goes between the meta and the time.</summary>
    public string Separator { get; init; } = " · ";

    /// <summary>A time less than a minute ago.</summary>
    public string JustNow { get; init; } = "Just now";

    /// <summary>A time minutes ago: "4 min ago".</summary>
    public Func<long, string> MinutesAgo { get; init; } = minutes => minutes + " min ago";

    /// <summary>A time hours ago: "2 h ago".</summary>
    public Func<long, string> HoursAgo { get; init; } = hours => hours + " h ago";

    /// <summary>A time days ago: "3 d ago".</summary>
    public Func<long, string> DaysAgo { get; init; } = days => days + " d ago";

    /// <summary>The name of a toast's dismiss button, from its sentence: "Dismiss: Sofia started a Pro trial".</summary>
    public Func<string, string> Dismiss { get; init; } = sentence => "Dismiss: " + sentence;

    /// <summary>What the polite status says after a dismissal, from the toast's sentence.</summary>
    public Func<string, string> DismissedStatus { get; init; } = sentence => "Dismissed: " + sentence;
}
