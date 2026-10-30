namespace TKCode.Arc;

/// <summary>
/// Every word <see cref="InstallSnippet"/> shows or says, in English by default (the card's title, description and version
/// are properties of the card). Replace the whole object to localize or reword; announcements that name something are
/// format functions.
/// </summary>
public sealed class InstallSnippetLabels
{
    /// <summary>The English defaults.</summary>
    public static InstallSnippetLabels Default { get; } = new();

    /// <summary>The title of step 1.</summary>
    public string InstallTitle { get; init; } = "Install";

    /// <summary>The title of step 2.</summary>
    public string KeyTitle { get; init; } = "Add your key";

    /// <summary>The title of step 3.</summary>
    public string CodeTitle { get; init; } = "Ship";

    /// <summary>Step 1's line when the snippet has no description of its own.</summary>
    public string InstallDescription { get; init; } = "Add the SDK to your project.";

    /// <summary>Step 2's line when the snippet has no description of its own.</summary>
    public string KeyDescription { get; init; } = "Put your key where the SDK can read it.";

    /// <summary>Step 3's line when the snippet has no description of its own.</summary>
    public string CodeDescription { get; init; } = "Send your first event.";

    /// <summary>The file name of a package install command.</summary>
    public string Terminal { get; init; } = "Terminal";

    /// <summary>The Generate key button before a key exists.</summary>
    public string GenerateKey { get; init; } = "Generate key";

    /// <summary>The button once a key was generated (it asks for another).</summary>
    public string NewKey { get; init; } = "New key";

    /// <summary>The button after the key generator failed.</summary>
    public string TryAgain { get; init; } = "Try again";

    /// <summary>The line under the key snippet before a key exists, when the app creates real keys.</summary>
    public string KeyHint { get; init; } = "Create a key here, or paste one you already have.";

    /// <summary>The line under the key snippet when no key generator is set: keys are invented on the spot.</summary>
    public string SimulatedHint { get; init; } = "Sample keys only: nothing is created or sent.";

    /// <summary>The line while the key generator runs.</summary>
    public string Creating { get; init; } = "Creating a key…";

    /// <summary>The line once a real key is in the snippets.</summary>
    public string Created { get; init; } = "Key created. Keep it out of version control.";

    /// <summary>The line once an invented key is in the snippets.</summary>
    public string CreatedSimulated { get; init; } = "Sample key added. Nothing was sent.";

    /// <summary>The line when the key generator failed without a message of its own.</summary>
    public string KeyError { get; init; } = "Could not create a key.";

    /// <summary>The name of the framework tab list.</summary>
    public string Frameworks { get; init; } = "Framework";

    /// <summary>The name of the package manager tab list.</summary>
    public string PackageManagers { get; init; } = "Package manager";

    /// <summary>The name of the steps list (the framework tab panel).</summary>
    public string Steps { get; init; } = "Setup steps";

    /// <summary>The name of a step: its number and the count ("Step 1 of 3").</summary>
    public Func<int, int, string> StepName { get; init; } = static (number, count) => $"Step {number} of {count}";

    /// <summary>The spoken title of a finished step ("Install, done").</summary>
    public Func<string, string> DoneTitle { get; init; } = static title => title + ", done";

    /// <summary>The copy button of step 1.</summary>
    public string CopyInstall { get; init; } = "Copy install command";

    /// <summary>The copy button of step 2.</summary>
    public string CopyKey { get; init; } = "Copy key snippet";

    /// <summary>The copy button of step 3.</summary>
    public string CopyCode { get; init; } = "Copy code";

    /// <summary>Announced when the reader picks a framework ("Showing Python").</summary>
    public Func<string, string> FrameworkShown { get; init; } = static name => "Showing " + name;

    /// <summary>Announced when the reader picks a package manager ("Using pnpm").</summary>
    public Func<string, string> PackageManagerShown { get; init; } = static name => "Using " + name;

    /// <summary>Announced when a real key is in the snippets.</summary>
    public string KeyAnnouncement { get; init; } = "New key added to the snippets.";

    /// <summary>Announced when an invented key is in the snippets.</summary>
    public string SimulatedKeyAnnouncement { get; init; } = "Sample key added to the snippets. It is simulated and nothing was sent.";

    /// <summary>The names of the package managers, in the order of <see cref="InstallSnippetPackageManager"/>.</summary>
    public IReadOnlyList<string> PackageManagerNames { get; init; } = ["npm", "pnpm", "yarn", "bun"];

    /// <summary>The name of a package manager.</summary>
    internal string NameOf(InstallSnippetPackageManager manager)
    {
        var index = (int)manager;
        return index >= 0 && index < PackageManagerNames.Count ? PackageManagerNames[index] : manager.ToString().ToLowerInvariant();
    }

    /// <summary>The title of a step.</summary>
    internal string TitleOf(InstallSnippetStep step) => step switch
    {
        InstallSnippetStep.Install => InstallTitle,
        InstallSnippetStep.Key => KeyTitle,
        _ => CodeTitle,
    };

    /// <summary>The default line of a step.</summary>
    internal string DescriptionOf(InstallSnippetStep step) => step switch
    {
        InstallSnippetStep.Install => InstallDescription,
        InstallSnippetStep.Key => KeyDescription,
        _ => CodeDescription,
    };

    /// <summary>The name of a step's copy button.</summary>
    internal string CopyOf(InstallSnippetStep step) => step switch
    {
        InstallSnippetStep.Install => CopyInstall,
        InstallSnippetStep.Key => CopyKey,
        _ => CopyCode,
    };
}
