using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

/// <summary>A row class for the TypedRows demo.</summary>
public sealed class Product : INotifyPropertyChanged
{
    private string _name = string.Empty;
    private string _category = string.Empty;
    private double _price;
    private int _stock;

    public string Name { get => _name; set => Set(ref _name, value); }

    public string Category { get => _category; set => Set(ref _category, value); }

    public double Price { get => _price; set => Set(ref _price, value); }

    public int Stock { get => _stock; set => Set(ref _stock, value); }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return;
        }
        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}

public partial class DataGridTypedRowsDemo : UserControl
{
    public DataGridTypedRowsDemo()
    {
        InitializeComponent();
        ProductsGrid.Columns[1].Options = ["Lighting", "Seating", "Storage"];
        ProductsGrid.RowKeySelector = item => ((Product)item).Name;
        ProductsGrid.ItemsSource = new ObservableCollection<Product>
        {
            new() { Name = "Arc lamp", Category = "Lighting", Price = 129, Stock = 14 },
            new() { Name = "Desk lamp", Category = "Lighting", Price = 49.5, Stock = 80 },
            new() { Name = "Lounge chair", Category = "Seating", Price = 690, Stock = 3 },
            new() { Name = "Stool", Category = "Seating", Price = 85, Stock = 22 },
            new() { Name = "Shelf", Category = "Storage", Price = 240, Stock = 9 }
        };
        ProductsGrid.CellsChanged += (_, e) =>
        {
            var change = e.Changes[^1];
            var product = (Product)change.Row;
            Log.Text = e.Changes.Count == 1
                ? $"{product.Name}: {change.ColumnKey} is now {change.NewValue ?? "empty"} (price {product.Price}, stock {product.Stock})."
                : $"{e.Reason}: {e.Changes.Count} cells changed.";
        };
    }
}
