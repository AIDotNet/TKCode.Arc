using Avalonia.Controls;
using Avalonia.Interactivity;
using TKCode.Arc.Mobile;

namespace TKCode.Arc.Gallery.Demos;

public partial class MobileToastBasicDemo : UserControl
{
    private int _count;

    public MobileToastBasicDemo()
    {
        InitializeComponent();
    }

    private void OnArchive(object? sender, RoutedEventArgs e)
    {
        Toasts.Policy = ReplaceSwitch.IsChecked == true ? ToastPolicy.Replace : ToastPolicy.Stack;
        _count++;
        Toasts.Show($"Message {_count} archived");
    }

    private void OnIcon(object? sender, RoutedEventArgs e)
    {
        Toasts.Policy = ReplaceSwitch.IsChecked == true ? ToastPolicy.Replace : ToastPolicy.Stack;
        Toasts.Show("Reminder set for tomorrow", icon: ArcIcons.Bell);
    }
}
