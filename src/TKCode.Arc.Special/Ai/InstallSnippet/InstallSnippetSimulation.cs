namespace TKCode.Arc;

/// <summary>
/// A simulated SDK for an <see cref="InstallSnippet"/> that has no <see cref="InstallSnippet.Frameworks"/>, for demos and
/// prototypes: the framework tabs it shows and the prefix of the keys its simulated Generate key invents (nothing is
/// created or sent, and step 2 says so). The app's own frameworks set it aside. Create one per card.
/// </summary>
public sealed class InstallSnippetSimulation
{
    /// <summary>The framework tabs, in order (write <c>{{key}}</c> where the key goes).</summary>
    public IReadOnlyList<InstallSnippetFramework> Frameworks { get; set; } = [];

    /// <summary>The prefix of an invented key, such as "demo_": make it obvious that the key is not real.</summary>
    public string KeyPrefix { get; set; } = InstallSnippetBuiltIns.DefaultKeyPrefix;
}
