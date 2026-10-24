using Avalonia.Interactivity;
using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class OtpInputVerifyDemo : UserControl
{
    private const string DemoCode = "123456";

    public OtpInputVerifyDemo()
    {
        InitializeComponent();
    }

    // The code is checked when the last slot is filled; the slots stay focused but ignore typing meanwhile.
    private async void OnCompleted(object? sender, OtpInputCompletedEventArgs e)
    {
        Code.IsReadOnly = true;
        await Task.Delay(700);
        Code.IsReadOnly = false;
        if (e.Code == DemoCode)
        {
            Code.IsSuccess = true;
            Code.Description = "Code verified.";
            return;
        }
        Code.Value = string.Empty;
        Code.ErrorMessage = "That code didn't match. Check the latest email and try again.";
        Code.FocusSlot(0);
    }

    // Typing again clears the error, as Arc's sign-in does.
    private void OnValueChanged(object? sender, OtpInputValueChangedEventArgs e)
    {
        if (e.IsUserEdit && e.NewValue.Length > 0)
        {
            Code.ErrorMessage = null;
        }
    }

    private void OnRestart(object? sender, RoutedEventArgs e)
    {
        Code.IsSuccess = false;
        Code.ErrorMessage = null;
        Code.Description = "For this demo, the code is 123456.";
        Code.Value = string.Empty;
        Code.FocusSlot(0);
    }
}
