using Avalonia.Controls;
using Avalonia.Interactivity;

namespace TKCode.Arc.Gallery.Demos;

public partial class NumberFilterBoxCombinedDemo : UserControl
{
    private sealed record Product(string Name, decimal Price, int Stock);

    private static readonly Product[] s_products =
    [
        new("MacBook Pro 14", 1999m, 12),
        new("iPad Pro 11", 999m, 4),
        new("AirPods Pro", 249m, 40),
        new("Magic Keyboard", 99m, 25),
        new("Studio Display", 1599m, 3),
        new("Pro Stand", 999m, 8),
        new("USB-C Cable", 19m, 120)
    ];

    public NumberFilterBoxCombinedDemo()
    {
        InitializeComponent();
        // The boxes only hold the conditions; the page combines them. FilterChanged bubbles, so one handler hears all three.
        AddHandler(FilterBox.FilterChangedEvent, (object? _, RoutedEventArgs _) => Apply());
        Apply();
    }

    private void Apply()
    {
        var matches = s_products
            .Where(p => PriceFilter.Matches(p.Price) && NameFilter.Matches(p.Name) && StockFilter.Matches(p.Stock))
            .Select(p => $"{p.Name}  ·  {p.Price:0.00}  ·  {p.Stock} in stock")
            .ToList();
        Count.Text = matches.Count == 1 ? "1 result" : $"{matches.Count} results";
        Results.ItemsSource = matches;
    }
}
