using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class TwoFactorSetupPreviewDemo : UserControl
{
    public TwoFactorSetupPreviewDemo()
    {
        InitializeComponent();
    }

    // No phone is texted in the gallery: the code that would be sent shows as a toast. It is the current
    // authenticator code of the example secret, which the built-in check accepts.
    private void OnTextMessage(object? sender, TwoFactorSetupTextMessageEventArgs e)
        => Toast.Show(this, $"Text to {e.PhoneNumber}: your code is {TwoFactorSetup.ComputeCode(Setup.Secret, DateTimeOffset.UtcNow)}");

    private void OnCompleted(object? sender, TwoFactorSetupCompletedEventArgs e)
        => Toast.Show(this, "Two-factor sign-in is on", ToastKind.Success);
}
