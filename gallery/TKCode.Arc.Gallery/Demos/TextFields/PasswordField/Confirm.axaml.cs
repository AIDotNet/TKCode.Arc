using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class PasswordFieldConfirmDemo : UserControl
{
    public PasswordFieldConfirmDemo()
    {
        InitializeComponent();
    }

    private void OnTextChanged(object? sender, TextChangedEventArgs e)
    {
        ConfirmPassword.ErrorMessage = !string.IsNullOrEmpty(ConfirmPassword.Text) && ConfirmPassword.Text != NewPassword.Text
            ? "The passwords do not match yet."
            : null;
    }
}
