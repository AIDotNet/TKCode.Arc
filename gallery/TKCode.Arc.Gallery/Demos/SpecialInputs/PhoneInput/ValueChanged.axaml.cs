using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class PhoneInputValueChangedDemo : UserControl
{
    public PhoneInputValueChangedDemo()
    {
        InitializeComponent();
    }

    private void OnValueChanged(object? sender, PhoneInputValueChangedEventArgs e)
    {
        E164.Text = $"E.164: {(e.NewValue.Length > 0 ? e.NewValue : "—")}";
        Formatted.Text = $"Formatted: {(e.Formatted.Length > 0 ? $"+{e.Country.Dial} {e.Formatted}" : "—")}";
        Status.Text = $"Status: {e.Status}{(e.IsValid ? " (valid)" : string.Empty)}";
    }
}
