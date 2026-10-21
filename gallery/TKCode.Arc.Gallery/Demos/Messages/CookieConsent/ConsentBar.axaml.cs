using Avalonia.Controls;
using Avalonia.Interactivity;

namespace TKCode.Arc.Gallery.Demos;

public partial class CookieConsentConsentBarDemo : UserControl
{
    public CookieConsentConsentBarDemo()
    {
        InitializeComponent();
    }

    private void OnConsentChanged(object? sender, CookieConsentChangedEventArgs e)
        => State.Text = e.Consent is null
            ? "Reset: asking again."
            : $"{e.Action}: " + string.Join(", ", e.Consent.Select(p => $"{p.Key} {(p.Value ? "on" : "off")}"));

    private void OnSettingsClick(object? sender, RoutedEventArgs e) => Consent.OpenPreferences();

    private void OnResetClick(object? sender, RoutedEventArgs e) => Consent.Reset();

    private void OnBottomClick(object? sender, RoutedEventArgs e) => Consent.Position = CookieConsentPosition.Bottom;

    private void OnBottomLeftClick(object? sender, RoutedEventArgs e) => Consent.Position = CookieConsentPosition.BottomLeft;

    private void OnBottomRightClick(object? sender, RoutedEventArgs e) => Consent.Position = CookieConsentPosition.BottomRight;
}
