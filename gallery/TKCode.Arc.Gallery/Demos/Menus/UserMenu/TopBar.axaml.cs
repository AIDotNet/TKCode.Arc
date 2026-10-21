using Avalonia.Controls;
using Avalonia.Interactivity;

namespace TKCode.Arc.Gallery.Demos;

public partial class UserMenuTopBarDemo : UserControl
{
    public UserMenuTopBarDemo()
    {
        InitializeComponent();
        // Sign out shows a spinner on its row until the task settles.
        Account.SignOutAction = () => Task.Delay(1200);
    }

    private void OnThemePreferenceChanged(object? sender, UserMenuThemePreferenceChangedEventArgs e)
        => Status.Text = $"Theme: {e.ThemePreference}";

    private void OnItemClicked(object? sender, MenuEntryClickedEventArgs e) => Status.Text = $"Opened {e.Header}";

    private void OnSignOutRequested(object? sender, RoutedEventArgs e) => Status.Text = "Signing out…";
}
