using System.Globalization;
using Avalonia.Controls;
using Avalonia.Interactivity;

namespace TKCode.Arc.Gallery.Demos;

public partial class SlotTextPriceDemo : UserControl
{
    private static readonly double[] Prices = [12480, 999, 1000, 48250];

    private int _index;

    public SlotTextPriceDemo()
    {
        InitializeComponent();
        Amount.Format = price => "$" + price.ToString("#,##0", CultureInfo.GetCultureInfo("en-US"));
        Amount.Value = Prices[0];
    }

    private void OnChange(object? sender, RoutedEventArgs e)
    {
        Amount.Value = Prices[_index = (_index + 1) % Prices.Length];
    }
}
