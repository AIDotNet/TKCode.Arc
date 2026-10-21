using Avalonia.Controls;
using Avalonia.Interactivity;

namespace TKCode.Arc.Gallery.Demos;

public partial class WaffleChartEnergyMixDemo : UserControl
{
    private static readonly WaffleCategory[] s_mix2015 =
    [
        new("coal", "Coal", 9400), new("gas", "Gas", 5500), new("hydro", "Hydro", 3950),
        new("nuclear", "Nuclear", 2570), new("wind-solar", "Wind and solar", 1080), new("other", "Other", 700)
    ];

    private static readonly WaffleCategory[] s_mix2023 =
    [
        new("coal", "Coal", 10430), new("gas", "Gas", 6630), new("hydro", "Hydro", 4210),
        new("nuclear", "Nuclear", 2690), new("wind-solar", "Wind and solar", 3940), new("other", "Other", 760)
    ];

    public WaffleChartEnergyMixDemo()
    {
        InitializeComponent();
        Chart.ItemsSource = s_mix2023;
    }

    // Cells that keep their group stay where they are; the others fly to their new block.
    private void On2015Click(object? sender, RoutedEventArgs e) => Chart.ItemsSource = s_mix2015;

    private void On2023Click(object? sender, RoutedEventArgs e) => Chart.ItemsSource = s_mix2023;

    private void OnActiveKeyChanged(object? sender, WaffleActiveKeyChangedEventArgs e)
        => Pinned.Text = $"Pinned: {e.Category?.Label ?? "none"}";
}
