using Avalonia.Controls;
using Avalonia.Interactivity;

namespace TKCode.Arc.Gallery.Demos;

public partial class UndoToastCountdownDemo : UserControl
{
    public UndoToastCountdownDemo()
    {
        InitializeComponent();
    }

    private void OnMove(object? sender, RoutedEventArgs e) => Long.Show("3 files moved to Archive");

    private void OnRead(object? sender, RoutedEventArgs e) => Quiet.Show("12 messages marked as read");

    private void OnClosed(object? sender, UndoToastClosedEventArgs e) =>
        Log.Text = $"{e.Message}: {e.Reason}";
}
