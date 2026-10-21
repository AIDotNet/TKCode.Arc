using Avalonia.Controls;
using Avalonia.Interactivity;

namespace TKCode.Arc.Gallery.Demos;

public partial class PasswordStrengthErrorsDemo : UserControl
{
    private int _attempts;

    public PasswordStrengthErrorsDemo()
    {
        InitializeComponent();
    }

    // Each new error shakes the field once.
    private void OnSubmitClick(object? sender, RoutedEventArgs e)
    {
        _attempts++;
        Password.ErrorMessage = Password.Level < 3
            ? (_attempts % 2 == 1 ? "Choose a stronger password." : "That password is too easy to guess.")
            : null;
    }
}
