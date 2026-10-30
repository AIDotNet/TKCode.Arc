using Avalonia.Controls;
using Avalonia.Interactivity;

namespace TKCode.Arc.Gallery.Demos;

public partial class UpgradePromptPreviewDemo : UserControl
{
    public UpgradePromptPreviewDemo()
    {
        InitializeComponent();
        // The second card runs the built-in simulated upgrade as soon as it is shown, so it lands on the success state.
        Upgraded.Loaded += OnUpgradedLoaded;
    }

    private async void OnUpgradedLoaded(object? sender, RoutedEventArgs e)
    {
        Upgraded.Loaded -= OnUpgradedLoaded;
        await Upgraded.UpgradeAsync();
    }
}
