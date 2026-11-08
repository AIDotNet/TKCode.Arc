using Avalonia.Controls;
using Avalonia.Interactivity;

namespace ArcItemNamespace.Views;

/// <summary>
/// A secondary window. Open it over its owner with <c>await new ArcItemWindow().ShowDialog(owner)</c>, or beside it with
/// <c>new ArcItemWindow().Show(owner)</c>.
/// </summary>
public partial class ArcItemWindow : Window
{
    public ArcItemWindow()
    {
        InitializeComponent();
    }

    private void OnClose(object? sender, RoutedEventArgs e) => Close();
}
