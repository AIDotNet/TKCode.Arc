namespace TKCode.Arc;

/// <summary>
/// Every word <see cref="ProviderKeys"/> shows or says, in English by default. Replace the whole object to localize or
/// reword; words that name a provider, a prefix, a time or an error are format functions.
/// </summary>
public sealed class ProviderKeysLabels
{
    /// <summary>The English defaults.</summary>
    public static ProviderKeysLabels Default { get; } = new();

    /// <summary>The status of a key that passed its last test.</summary>
    public string Connected { get; init; } = "Connected";

    /// <summary>The status of a key the provider rejected.</summary>
    public string Invalid { get; init; } = "Invalid";

    /// <summary>The status of a row without a key.</summary>
    public string NotSet { get; init; } = "Not set";

    /// <summary>The status while a stored key is tested again.</summary>
    public string Testing { get; init; } = "Testing";

    /// <summary>The key line of a row without a key.</summary>
    public string NoKey { get; init; } = "No key added";

    /// <summary>The key line after a removal, while Undo is on screen.</summary>
    public string KeyRemoved { get; init; } = "Key removed";

    /// <summary>The last-used line of a key that has never been used.</summary>
    public string NeverUsed { get; init; } = "Never used";

    /// <summary>The last-used line, from how long ago the key was used: "Used just now", "Used 5 min ago".</summary>
    public Func<TimeSpan, string> LastUsed { get; init; } = age => "Used " + Ago(age);

    /// <summary>The last-used line of an invalid key without an error of its own.</summary>
    public string Rejected { get; init; } = "Rejected";

    /// <summary>The Add key button of an empty row.</summary>
    public string AddKey { get; init; } = "Add key";

    /// <summary>The Undo button after a removal.</summary>
    public string Undo { get; init; } = "Undo";

    /// <summary>The form's Cancel button.</summary>
    public string Cancel { get; init; } = "Cancel";

    /// <summary>The form's Save button.</summary>
    public string Save { get; init; } = "Save";

    /// <summary>The form's input label, from the provider's name: "{provider} API key".</summary>
    public Func<string, string> InputLabel { get; init; } = provider => provider + " API key";

    /// <summary>The helper line before anything is typed, from the provider's name and prefix.</summary>
    public Func<string, string, string> FormHint { get; init; } = (provider, prefix) =>
        prefix.Length > 0 ? $"Paste a {provider} key. It starts with {prefix}" : $"Paste a {provider} key.";

    /// <summary>The format error of a key with the wrong start, from the provider's name and prefix.</summary>
    public Func<string, string, string> WrongPrefix { get; init; } = (provider, prefix) => $"{provider} keys start with {prefix}";

    /// <summary>The format error of a key with characters keys never have.</summary>
    public string BadCharacters { get; init; } = "Keys only use letters, numbers, dashes and underscores.";

    /// <summary>The format error of a key that is too short.</summary>
    public string TooShort { get; init; } = "This key looks too short.";

    /// <summary>The helper line of a key whose format looks right.</summary>
    public string LooksRight { get; init; } = "Looks right. Save tests the connection.";

    /// <summary>The helper line while a pasted key is tested.</summary>
    public string TestingConnection { get; init; } = "Testing connection…";

    /// <summary>The helper line once a pasted key passed, with the test's note when it gave one.</summary>
    public Func<string?, string> ConnectedDetail { get; init; } = detail =>
        string.IsNullOrEmpty(detail) ? "Connected" : "Connected · " + detail;

    /// <summary>The helper line of a rejected key, from the error.</summary>
    public Func<string, string> RejectedDetail { get; init; } = error => "Rejected: " + error;

    /// <summary>The error used when a test fails without saying why.</summary>
    public string UnknownError { get; init; } = "The provider did not accept this key";

    /// <summary>The reason a test fails when the card has neither a key test nor a simulation.</summary>
    public string NoKeyTest { get; init; } = "No key test is set up";

    /// <summary>The name of the reveal toggle, from the provider's name.</summary>
    public Func<string, string> RevealName { get; init; } = provider => $"Reveal the {provider} key";

    /// <summary>The name of the copy button, from the provider's name.</summary>
    public Func<string, string> CopyName { get; init; } = provider => $"Copy the {provider} key";

    /// <summary>The name of the rotate button, from the provider's name.</summary>
    public Func<string, string> RotateName { get; init; } = provider => $"Rotate the {provider} key";

    /// <summary>The name of the remove button, from the provider's name.</summary>
    public Func<string, string> RemoveName { get; init; } = provider => $"Remove the {provider} key";

    /// <summary>The name of the Add key button, from the provider's name.</summary>
    public Func<string, string> AddName { get; init; } = provider => $"Add a {provider} key";

    /// <summary>The name of the Undo button, from the provider's name.</summary>
    public Func<string, string> UndoName { get; init; } = provider => $"Undo removing the {provider} key";

    /// <summary>The name of the status button, from the provider's name, the status and the rejection reason (or null).</summary>
    public Func<string, string, string?, string> RetestName { get; init; } = (provider, status, error) =>
        string.IsNullOrEmpty(error)
            ? $"{provider} key: {status}. Test again"
            : $"{provider} key: {status}, {error}. Test again";

    /// <summary>Why reveal and copy are off when only the last four characters are known.</summary>
    public string ValueHidden { get; init; } = "Only the last four characters are stored here";

    /// <summary>Announced when a key is revealed, from the provider's name and the seconds it stays.</summary>
    public Func<string, int, string> RevealedAnnouncement { get; init; } = (provider, seconds) =>
        $"{provider} key shown for {seconds} seconds";

    /// <summary>Announced when a revealed key is masked again.</summary>
    public Func<string, string> HiddenAnnouncement { get; init; } = provider => $"{provider} key hidden";

    /// <summary>Announced when a key was copied.</summary>
    public Func<string, string> CopiedAnnouncement { get; init; } = provider => $"{provider} key copied";

    /// <summary>Announced when a key passed its test.</summary>
    public Func<string, string> PassedAnnouncement { get; init; } = provider => $"{provider} key connected";

    /// <summary>Announced when a key was rejected, from the provider's name and the error.</summary>
    public Func<string, string, string> FailedAnnouncement { get; init; } = (provider, error) => $"{provider} key rejected: {error}";

    /// <summary>Announced when a new key was saved.</summary>
    public Func<string, ProviderKeySaveMode, string> SavedAnnouncement { get; init; } = (provider, mode) =>
        mode == ProviderKeySaveMode.Add ? $"{provider} key added and connected" : $"{provider} key rotated and connected";

    /// <summary>Announced when a key was removed, from the provider's name and the seconds Undo stays.</summary>
    public Func<string, int, string> RemovedAnnouncement { get; init; } = (provider, seconds) =>
        $"{provider} key removed. Undo is available for {seconds} seconds";

    /// <summary>Announced when Undo put a key back.</summary>
    public Func<string, string> RestoredAnnouncement { get; init; } = provider => $"{provider} key restored";

    /// <summary>Added to test announcements when no <see cref="ProviderKeys.TestKey"/> is set.</summary>
    public string Simulated { get; init; } = "Simulated, nothing was sent.";

    /// <summary>"just now", "5 min ago", "3 h ago", "2 days ago".</summary>
    internal static string Ago(TimeSpan age)
    {
        if (age < TimeSpan.FromMinutes(1))
        {
            return "just now";
        }
        if (age < TimeSpan.FromHours(1))
        {
            return $"{(int)age.TotalMinutes} min ago";
        }
        if (age < TimeSpan.FromDays(1))
        {
            return $"{(int)age.TotalHours} h ago";
        }
        var days = (int)age.TotalDays;
        return days == 1 ? "yesterday" : $"{days} days ago";
    }
}
