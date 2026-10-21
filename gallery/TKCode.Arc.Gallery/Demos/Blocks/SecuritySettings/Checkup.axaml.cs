using Avalonia.Controls;
using Avalonia.Interactivity;

namespace TKCode.Arc.Gallery.Demos;

public partial class SecuritySettingsCheckupDemo : UserControl
{
    public SecuritySettingsCheckupDemo()
    {
        InitializeComponent();
    }

    private void OnAddPasskeyRequested(object? sender, RoutedEventArgs e) => Security.PasskeyCount++;

    private void OnRecoveryCodesRequested(object? sender, RoutedEventArgs e) =>
        Security.RecoveryCodesCreatedAt ??= DateTimeOffset.Now;
}
