using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Interactivity;

namespace TKCode.Arc.Gallery.Demos;

public partial class IconMorphSizesDemo : UserControl
{
    public IconMorphSizesDemo()
    {
        InitializeComponent();
    }

    private void OnToggleClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button { Content: IconMorph icon } button)
        {
            return;
        }

        icon.IsActive = !icon.IsActive;
        // Tag holds the name of the other state; swap it in so the button always names what the next press does.
        var name = AutomationProperties.GetName(button);
        AutomationProperties.SetName(button, button.Tag as string);
        button.Tag = name;
    }
}
