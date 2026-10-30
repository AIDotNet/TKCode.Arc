using Avalonia.Controls;
using Avalonia.Interactivity;

namespace TKCode.Arc.Gallery.Demos;

/// <summary>The default demo: the trial banner with 9 days, 2 days, an ended trial or none, as a banner, a card or a pill.</summary>
public partial class TrialBannerPreviewDemo : UserControl
{
    private readonly bool _ready;

    public TrialBannerPreviewDemo()
    {
        InitializeComponent();
        TrialBannerDemoData.Apply(Banner);
        _ready = true;
        Apply();
    }

    private void OnChanged(object? sender, SelectionChangedEventArgs e)
    {
        // The segmented controls apply SelectedValue while the XAML loads, before the banner field is set.
        if (_ready)
        {
            Apply();
        }
    }

    private void Apply()
    {
        // "No trial" leaves the days unset: the banner's empty state.
        Banner.DaysLeft = (Days.SelectedValue as string) switch
        {
            "none" => null,
            var value => int.TryParse(value, out var days) ? days : 9
        };
        Banner.Variant = (Variant.SelectedValue as string) switch
        {
            "card" => TrialBannerVariant.Card,
            "pill" => TrialBannerVariant.Pill,
            _ => TrialBannerVariant.Banner
        };
    }

    private void OnReset(object? sender, RoutedEventArgs e)
    {
        Banner.IsUpgraded = false;
        Banner.IsCollapsed = false;
    }
}
