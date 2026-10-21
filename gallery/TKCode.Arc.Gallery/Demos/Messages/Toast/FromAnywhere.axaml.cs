using Avalonia.Controls;
using Avalonia.Interactivity;

namespace TKCode.Arc.Gallery.Demos;

public partial class ToastFromAnywhereDemo : UserControl
{
    public ToastFromAnywhereDemo()
    {
        InitializeComponent();
    }

    // Any control in the window can send a toast to the window's ToastStack.
    private void OnSuccessClick(object? sender, RoutedEventArgs e)
        => Toast.Show(this, "Invite sent", ToastKind.Success, "Maya will get an email shortly.");

    private void OnInfoClick(object? sender, RoutedEventArgs e) => Toast.Show(this, "New comment", ToastKind.Info);

    private void OnErrorClick(object? sender, RoutedEventArgs e)
        => Toast.Show(this, "Upload failed", ToastKind.Error, "The file is larger than 25 MB.");
}
