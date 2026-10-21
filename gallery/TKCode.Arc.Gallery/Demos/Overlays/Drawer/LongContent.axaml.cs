using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class DrawerLongContentDemo : UserControl
{
    public DrawerLongContentDemo()
    {
        InitializeComponent();
        // Thirty lines are more than the drawer can show, so the body scrolls under the fixed header.
        for (var i = 1; i <= 30; i++)
        {
            var line = new TextBlock { Text = $"Line item {i} · Design review · {i * 12} min" };
            if (i % 2 == 0)
            {
                line.Classes.Add("muted");
            }
            Lines.Children.Add(line);
        }
    }
}
