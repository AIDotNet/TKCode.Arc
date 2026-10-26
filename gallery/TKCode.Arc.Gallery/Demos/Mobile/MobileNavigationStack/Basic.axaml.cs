using Avalonia.Controls;
using Avalonia.Interactivity;

namespace TKCode.Arc.Gallery.Demos;

public partial class MobileNavigationStackBasicDemo : UserControl
{
    public MobileNavigationStackBasicDemo()
    {
        InitializeComponent();
    }

    private void OnOpen(object? sender, RoutedEventArgs e)
    {
        var title = (string)((Mobile.ListItem)sender!).Content!;
        Stack.Push(new Mobile.Page
        {
            Header = new Mobile.Navbar { Title = title, ShowBack = true, BackText = "Inbox", BackCommand = Stack.BackCommand },
            Content = new TextBlock { Margin = new(16), Text = $"A message from {title}.", TextWrapping = Avalonia.Media.TextWrapping.Wrap },
        });
    }
}
