using Avalonia.Controls;
using Avalonia.Interactivity;

namespace TKCode.Arc.Gallery.Demos;

/// <summary>The default demo: the simulated feed with two toasts prefilled, a pause switch and a restart button.</summary>
public partial class SocialProofToastPreviewDemo : UserControl
{
    public SocialProofToastPreviewDemo()
    {
        InitializeComponent();
        Paused.IsCheckedChanged += (_, _) => Stack.IsPaused = Paused.IsChecked == true;
    }

    private void OnRestart(object? sender, RoutedEventArgs e) => Stack.Restart();
}
