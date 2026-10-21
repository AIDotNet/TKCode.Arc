using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class SecuritySettingsChangePasswordDemo : UserControl
{
    private int _requests;

    public SecuritySettingsChangePasswordDemo()
    {
        InitializeComponent();
        Security.PasswordChangedAt = DateTimeOffset.Now.AddMonths(-3);
    }

    // Check the current password; an error message keeps the form open and shows the message under that field.
    private void OnPasswordChangeRequested(object? sender, SecurityPasswordChangeEventArgs e)
    {
        _requests++;
        if (e.CurrentPassword != "correct horse")
        {
            e.ErrorMessage = "That isn't your current password.";
            Log.Text = $"Request {_requests}: rejected, the current password did not match.";
            return;
        }
        Log.Text = $"Request {_requests}: accepted, the new password is {e.NewPassword.Length} characters long.";
    }
}
