using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class ThemeSwitcherHandledByAppDemo : UserControl
{
    public ThemeSwitcherHandledByAppDemo()
    {
        InitializeComponent();
    }

    // Controlled: the app handles the request itself; here it only flips a local appearance.
    private void OnThemeChangeRequested(object? sender, ThemeSwitcherChangeRequestedEventArgs e)
    {
        e.Handled = true;
        Switcher.Appearance = e.Next;
        Status.Text = $"Requested {e.Next} ({e.Variant}) from {e.Origin.X:0},{e.Origin.Y:0}; the app decides what to apply.";
    }
}
