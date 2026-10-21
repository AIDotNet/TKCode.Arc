using System.Collections.ObjectModel;
using Avalonia.Controls;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace TKCode.Arc.Gallery.Demos;

public partial class ReorderableListCustomItemTemplateDemo : UserControl
{
    public static readonly FuncValueConverter<string?, IBrush?> SwatchBrush = new(name => new SolidColorBrush(name switch
    {
        "Coral" => Color.Parse("#F07167"),
        "Saffron" => Color.Parse("#F4B942"),
        "Mint" => Color.Parse("#57CC99"),
        "Azure" => Color.Parse("#4EA8DE"),
        _ => Color.Parse("#9381FF")
    }));

    public ReorderableListCustomItemTemplateDemo()
    {
        InitializeComponent();
        Colors.ItemsSource = new ObservableCollection<string> { "Coral", "Saffron", "Mint", "Azure", "Iris" };
    }
}
