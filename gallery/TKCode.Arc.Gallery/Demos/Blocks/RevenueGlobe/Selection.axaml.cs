using Avalonia.Controls;
using Avalonia.Interactivity;

namespace TKCode.Arc.Gallery.Demos;

public partial class RevenueGlobeSelectionDemo : UserControl
{
    public RevenueGlobeSelectionDemo()
    {
        InitializeComponent();
        RevenueGlobeSampleDay.Apply(Globe);
        Globe.SelectedCity = Globe.Cities.First(c => c.Key == "Zurich");
    }

    private void OnZurich(object? sender, RoutedEventArgs e) => Globe.FlyTo(Globe.Cities.First(c => c.Key == "Zurich"));

    private void OnApac(object? sender, RoutedEventArgs e) => Globe.FlyTo(Globe.Regions.First(r => r.Key == "APAC"));

    private void OnReset(object? sender, RoutedEventArgs e) => Globe.ResetView();

    private void OnSelectionChanged(object? sender, RevenueGlobeSelectionChangedEventArgs e) =>
        Status.Text = e.NewCity is { } city ? $"Selected city: {city.Name}"
            : e.NewRegion is { } region ? $"Selected region: {region.Name}"
            : "Nothing selected";
}
