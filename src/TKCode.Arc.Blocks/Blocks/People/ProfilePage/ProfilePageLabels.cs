namespace TKCode.Arc;

/// <summary>
/// Every word <see cref="ProfilePage"/> shows or announces, in English by default. Replace the instance to translate or
/// reword; formatted texts are functions so that word order can change.
/// </summary>
public sealed class ProfilePageLabels
{
    /// <summary>The share button.</summary>
    public string Share { get; init; } = "Share";

    /// <summary>The share button after the link was copied.</summary>
    public string Copied { get; init; } = "Copied";

    /// <summary>The share button when the clipboard refused the link.</summary>
    public string CopyFailed { get; init; } = "Failed";

    /// <summary>Announced after the link was copied; the argument is the link.</summary>
    public Func<string, string> CopiedAnnouncement { get; init; } = url => $"Profile link copied: {url}";

    /// <summary>Announced when the link could not be copied.</summary>
    public string CopyFailedAnnouncement { get; init; } = "Could not copy the profile link";

    /// <summary>The button that opens the edit sheet, and the sheet's title.</summary>
    public string EditProfile { get; init; } = "Edit profile";

    /// <summary>The Activity tab.</summary>
    public string ActivityTab { get; init; } = "Activity";

    /// <summary>The Projects tab.</summary>
    public string ProjectsTab { get; init; } = "Projects";

    /// <summary>The accessible name of the tab row.</summary>
    public string TabListName { get; init; } = "Profile sections";

    /// <summary>The accessible name of the left column.</summary>
    public string AboutName { get; init; } = "About";

    /// <summary>The accessible name of the tabbed column.</summary>
    public string ContentName { get; init; } = "Profile content";

    /// <summary>The line after the calendar icon; the argument is <see cref="ProfileDetails.JoinedText"/>.</summary>
    public Func<string, string> Joined { get; init; } = text => $"Joined {text}";

    /// <summary>The accessible name of the avatar; the argument is the name.</summary>
    public Func<string, string> AvatarName { get; init; } = name => name;

    /// <summary>The word for a public project.</summary>
    public string Public { get; init; } = "Public";

    /// <summary>The word for a private project.</summary>
    public string Private { get; init; } = "Private";

    /// <summary>The star button's accessible name while not starred; arguments: project title, star count.</summary>
    public Func<string, int, string> StarName { get; init; } = (title, count) => $"Star {title}, {count:N0} stars";

    /// <summary>The star button's accessible name while starred; arguments: project title, star count.</summary>
    public Func<string, int, string> UnstarName { get; init; } = (title, count) => $"Unstar {title}, {count:N0} stars";

    /// <summary>The name field.</summary>
    public string NameField { get; init; } = "Name";

    /// <summary>The headline field.</summary>
    public string HeadlineField { get; init; } = "Headline";

    /// <summary>The location field.</summary>
    public string LocationField { get; init; } = "Location";

    /// <summary>The website field.</summary>
    public string WebsiteField { get; init; } = "Website";

    /// <summary>The bio field.</summary>
    public string BioField { get; init; } = "Bio";

    /// <summary>The counter next to the bio label; the argument is how many characters are left.</summary>
    public Func<int, string> Remaining { get; init; } = left => $"{left} left";

    /// <summary>The error under an empty name.</summary>
    public string NameRequired { get; init; } = "Enter a name.";

    /// <summary>The error under a website that is not a web address.</summary>
    public string WebsiteInvalid { get; init; } = "Use a web address such as example.com.";

    /// <summary>The sheet's cancel button.</summary>
    public string Cancel { get; init; } = "Cancel";

    /// <summary>The sheet's save button.</summary>
    public string Save { get; init; } = "Save changes";

    /// <summary>The save button while the save runs.</summary>
    public string Saving { get; init; } = "Saving";

    /// <summary>The accessible name of the sheet's close button.</summary>
    public string Close { get; init; } = "Close";

    /// <summary>Shown in the sheet when the save failed and the exception has no message.</summary>
    public string SaveFailed { get; init; } = "Your changes could not be saved. Try again.";

    /// <summary>Announced after a successful save.</summary>
    public string SavedAnnouncement { get; init; } = "Profile saved";
}
