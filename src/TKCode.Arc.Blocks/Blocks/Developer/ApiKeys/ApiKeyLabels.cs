using System.Globalization;

namespace TKCode.Arc;

/// <summary>
/// Every word <see cref="ApiKeys"/> shows or announces, in English by default. Replace the instance to translate or
/// reword; formatted texts are functions so that word order can change.
/// </summary>
public sealed class ApiKeyLabels
{
    /// <summary>The pill's text and the panel's title while the form is open.</summary>
    public string CreateKey { get; init; } = "Create key";

    /// <summary>The panel's title once the key exists.</summary>
    public string KeyCreated { get; init; } = "Key created";

    /// <summary>The accessible name of the panel's toggle while it is open.</summary>
    public string Close { get; init; } = "Close";

    /// <summary>The words after the count in the toolbar; the argument is the count ("active key" / "active keys").</summary>
    public Func<int, string> CountWords { get; init; } = count => count == 1 ? "active key" : "active keys";

    /// <summary>The count's accessible name; the argument is the count.</summary>
    public Func<int, string> CountName { get; init; } = count => count == 1 ? "1 active key" : $"{count} active keys";

    /// <summary>The accessible name of the list.</summary>
    public string ListName { get; init; } = "Active keys";

    /// <summary>The label of the name field.</summary>
    public string NameLabel { get; init; } = "Name";

    /// <summary>The name field's placeholder.</summary>
    public string NamePlaceholder { get; init; } = "Order webhook worker";

    /// <summary>The error under an empty name.</summary>
    public string NameRequired { get; init; } = "Give the key a name so you can recognise it later.";

    /// <summary>The label of the environment switch.</summary>
    public string EnvironmentLabel { get; init; } = "Environment";

    /// <summary>The test environment's segment and badge.</summary>
    public string Test { get; init; } = "Test";

    /// <summary>The live environment's segment and badge.</summary>
    public string Live { get; init; } = "Live";

    /// <summary>The line at the bottom of the form while Test is chosen.</summary>
    public string TestHint { get; init; } = "Test keys only touch sandbox records";

    /// <summary>The line at the bottom of the form while Live is chosen.</summary>
    public string LiveHint { get; init; } = "Live keys work on real customer data";

    /// <summary>The legend of the scope checkboxes.</summary>
    public string ScopesLabel { get; init; } = "Scopes";

    /// <summary>The error under the scopes when none is ticked.</summary>
    public string ScopeRequired { get; init; } = "Pick at least one scope.";

    /// <summary>The form's cancel button.</summary>
    public string Cancel { get; init; } = "Cancel";

    /// <summary>The form's submit button.</summary>
    public string Generate { get; init; } = "Generate key";

    /// <summary>The error shown in the form when generating failed and the exception has no message.</summary>
    public string GenerateFailed { get; init; } = "The key could not be created. Try again.";

    /// <summary>The label above the one-time secret.</summary>
    public string SecretLabel { get; init; } = "Secret key";

    /// <summary>The warning under the one-time secret.</summary>
    public string SecretOnce { get; init; } =
        "Copy this key now and store it somewhere safe. It is not shown again once this panel closes.";

    /// <summary>The reveal's close button.</summary>
    public string Done { get; init; } = "Done";

    /// <summary>The copy button's label.</summary>
    public string Copy { get; init; } = "Copy";

    /// <summary>The copy button's label after copying.</summary>
    public string Copied { get; init; } = "Copied";

    /// <summary>The dates column for a key that signed no request yet.</summary>
    public string NeverUsed { get; init; } = "Never used";

    /// <summary>The dates column's first line; the argument is how long ago ("4 min ago").</summary>
    public Func<string, string> Used { get; init; } = ago => $"Used {ago}";

    /// <summary>How long ago something happened; the argument is the time since then.</summary>
    public Func<TimeSpan, string> Ago { get; init; } = DefaultAgo;

    /// <summary>A date ("Feb 11, 2026").</summary>
    public Func<DateTimeOffset, string> Date { get; init; } =
        date => date.ToString("MMM d, yyyy", CultureInfo.CurrentCulture);

    /// <summary>The dates column's second line; the argument is the formatted creation date.</summary>
    public Func<string, string> Created { get; init; } = date => $"Created {date}";

    /// <summary>Read before the stale warning icon by screen readers.</summary>
    public string Idle { get; init; } = "Idle";

    /// <summary>Read before the last four characters ("Key ending in 7c3e").</summary>
    public Func<string, string> KeyEnding { get; init; } = last => $"Key ending in {last}";

    /// <summary>Read before the scope chips.</summary>
    public string ScopesPrefix { get; init; } = "Scopes";

    /// <summary>The accessible name of a row's details; the argument is the key's name.</summary>
    public Func<string, string> DetailsName { get; init; } = name => $"{name} details";

    /// <summary>The "Created by" fact.</summary>
    public string CreatedByLabel { get; init; } = "Created by";

    /// <summary>Shown when the creator is not known.</summary>
    public string UnknownPerson { get; init; } = "Unknown";

    /// <summary>The "Last request" fact.</summary>
    public string LastRequestLabel { get; init; } = "Last request";

    /// <summary>The "Last request" value of a key that was never used.</summary>
    public string NoRequests { get; init; } = "No requests yet";

    /// <summary>The request count fact.</summary>
    public string RequestsLabel { get; init; } = "Requests in 30 days";

    /// <summary>The creation date fact, shown in the details while the row is narrow.</summary>
    public string CreatedLabel { get; init; } = "Created";

    /// <summary>The note next to the revoke button.</summary>
    public string RevokeNote { get; init; } = "Anything signed with this key stops working the moment it is revoked.";

    /// <summary>The note next to the revoke button of a stale key; the argument is the number of idle days.</summary>
    public Func<int, string> StaleNote { get; init; } = days => $"Unused for {days} days. Revoke it if nothing relies on it.";

    /// <summary>The revoke button.</summary>
    public string HoldToRevoke { get; init; } = "Hold to revoke";

    /// <summary>The revoke button once confirmed.</summary>
    public string Revoked { get; init; } = "Revoked";

    /// <summary>The empty list's title.</summary>
    public string EmptyTitle { get; init; } = "No active keys";

    /// <summary>The empty list's line under the title.</summary>
    public string EmptyDescription { get; init; } = "Requests fail until you create a key.";

    /// <summary>Announced when a row opens; the argument is the key's name.</summary>
    public Func<string, string> DetailsShown { get; init; } = name => $"{name}: details shown";

    /// <summary>Announced when a key was created; the argument is its name.</summary>
    public Func<string, string> CreatedAnnouncement { get; init; } = name => $"{name} created. Copy the key before closing.";

    /// <summary>Announced when the secret was copied.</summary>
    public string CopiedAnnouncement { get; init; } = "Key copied. Store it somewhere safe.";

    /// <summary>Announced when a key was revoked; the argument is its name.</summary>
    public Func<string, string> RevokedAnnouncement { get; init; } = name => $"{name} has been revoked.";

    /// <summary>Announced when revoking failed and the key stays active; the arguments are its name and the error.</summary>
    public Func<string, string, string> RevokeFailedAnnouncement { get; init; } =
        (name, error) => $"{name} is still active. {error}";

    /// <summary>The error part of <see cref="RevokeFailedAnnouncement"/> when the exception has no message.</summary>
    public string RevokeFailed { get; init; } = "The key could not be revoked. Try again.";

    /// <summary>The default <see cref="Ago"/>: "just now", "4 min ago", "1 hour ago", "yesterday", "37 days ago".</summary>
    public static string DefaultAgo(TimeSpan span)
    {
        if (span < TimeSpan.FromMinutes(1))
        {
            return "just now";
        }
        if (span < TimeSpan.FromHours(1))
        {
            return $"{(int)span.TotalMinutes} min ago";
        }
        if (span < TimeSpan.FromDays(1))
        {
            var hours = (int)span.TotalHours;
            return hours == 1 ? "1 hour ago" : $"{hours} hours ago";
        }
        var days = (int)span.TotalDays;
        return days == 1 ? "yesterday" : $"{days} days ago";
    }
}
