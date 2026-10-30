using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class InstallSnippetYourKeyServiceDemo : UserControl
{
    public InstallSnippetYourKeyServiceDemo()
    {
        InitializeComponent();
        // Your endpoint creates the key; the token is cancelled when the card leaves or a new key is asked for.
        Snippet.KeyGenerator = async token =>
        {
            await Task.Delay(800, token);
            return InstallSnippetDemoData.KeyPrefix + Guid.NewGuid().ToString("N")[..20];
        };
    }

    private void OnFrameworkChanged(object? sender, InstallSnippetFrameworkChangedEventArgs e) =>
        Log.Text = $"onboarding_framework {{ id: \"{e.FrameworkId}\" }}";

    private void OnPackageManagerChanged(object? sender, InstallSnippetPackageManagerChangedEventArgs e) =>
        Log.Text = $"onboarding_package_manager {{ id: \"{e.PackageManager}\" }}";

    private void OnKeyGenerated(object? sender, InstallSnippetKeyGeneratedEventArgs e) =>
        Log.Text = $"onboarding_key {{ simulated: {(e.IsSimulated ? "true" : "false")} }}";

    private void OnCopied(object? sender, InstallSnippetCopiedEventArgs e) =>
        Log.Text = $"onboarding_copy {{ step: \"{e.Step}\", length: {e.Value.Length} }}";
}
