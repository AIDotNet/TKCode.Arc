using Avalonia.Interactivity;
using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class SecuritySettingsOverviewDemo : UserControl
{
    public SecuritySettingsOverviewDemo()
    {
        InitializeComponent();
        var now = DateTimeOffset.Now;
        Security.PasswordChangedAt = now.AddMonths(-3);
        Security.RecoveryCodesCreatedAt = now.AddDays(-140);
        Security.Sessions.Add(new SecuritySession
        {
            Device = "MacBook Air", Client = "Firefox", Location = "Rotterdam, NL", Kind = SecurityDeviceKind.Laptop,
            LastActive = now, IsCurrent = true
        });
        Security.Sessions.Add(new SecuritySession
        {
            Device = "Pixel 9", Client = "Larkspur app", Location = "Rotterdam, NL", Kind = SecurityDeviceKind.Phone,
            LastActive = now.AddHours(-2)
        });
        Security.Sessions.Add(new SecuritySession
        {
            Device = "ThinkPad X1", Client = "Edge", Location = "Utrecht, NL", Kind = SecurityDeviceKind.Laptop,
            LastActive = now.AddDays(-4)
        });
        Security.Sessions.Add(new SecuritySession
        {
            Device = "iPad mini", Client = "Safari", Location = "Ghent, BE", Kind = SecurityDeviceKind.Tablet,
            LastActive = now.AddDays(-21)
        });
    }

    // A real app runs the passkey ceremony here; the demo just counts one more.
    private void OnAddPasskeyRequested(object? sender, RoutedEventArgs e) => Security.PasskeyCount++;

    // A real app shows the codes (or creates them first) in a dialog.
    private void OnRecoveryCodesRequested(object? sender, RoutedEventArgs e) =>
        Security.RecoveryCodesCreatedAt ??= DateTimeOffset.Now;
}
