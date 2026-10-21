using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Interactivity;

namespace TKCode.Arc.Gallery.Demos;

public partial class SecuritySettingsSessionsDemo : UserControl
{
    public SecuritySettingsSessionsDemo()
    {
        InitializeComponent();
        Reset();
    }

    private void OnSessionSignedOut(object? sender, SecuritySessionEventArgs e) =>
        Log.Text = $"Signed out {e.Session.Device}. {Security.Sessions.Count} sessions left.";

    private void OnOtherSessionsSignedOut(object? sender, SecuritySessionsEventArgs e) =>
        Log.Text = $"Signed out {e.Sessions.Count} other sessions. Only this device is left.";

    private void OnResetClick(object? sender, RoutedEventArgs e)
    {
        Reset();
        Log.Text = "Sessions restored.";
    }

    // A new list replaces the old one; the block lists it current first, then by most recent activity.
    private void Reset()
    {
        var now = DateTimeOffset.Now;
        Security.Sessions = new AvaloniaList<SecuritySession>
        {
            new() { Device = "ThinkPad X1", Client = "Edge", Location = "Utrecht, NL", Kind = SecurityDeviceKind.Laptop, LastActive = now.AddDays(-4) },
            new() { Device = "MacBook Air", Client = "Firefox", Location = "Rotterdam, NL", Kind = SecurityDeviceKind.Laptop, LastActive = now, IsCurrent = true },
            new() { Device = "iPad mini", Client = "Safari", Location = "Ghent, BE", Kind = SecurityDeviceKind.Tablet, LastActive = now.AddDays(-21) },
            new() { Device = "Pixel 9", Client = "Larkspur app", Location = "Rotterdam, NL", Kind = SecurityDeviceKind.Phone, LastActive = now.AddHours(-2) },
            new() { Device = "Studio iMac", Client = "Chrome", Location = "Rotterdam, NL", Kind = SecurityDeviceKind.Desktop, LastActive = now.AddDays(-1) }
        };
    }
}
