using Avalonia.Controls;
using Avalonia.Interactivity;

namespace TKCode.Arc.Gallery.Demos;

public partial class TextMorphPrefixAndSuffixDemo : UserControl
{
    private static readonly string[] Prices = ["$12 / month", "$120 / year", "$10 / month"];

    private int _index;

    public TextMorphPrefixAndSuffixDemo()
    {
        InitializeComponent();
    }

    private void OnChangePlanClick(object? sender, RoutedEventArgs e)
    {
        Price.Text = Prices[_index = (_index + 1) % Prices.Length];
    }
}
