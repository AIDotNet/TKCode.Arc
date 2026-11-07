using Avalonia.Controls;
using Avalonia.Interactivity;

namespace TKCode.Arc.Gallery.Demos;

public partial class SkeletonMorphControlledDemo : UserControl
{
    private int _resolved;

    public SkeletonMorphControlledDemo()
    {
        InitializeComponent();
    }

    private void OnResolved(object? sender, RoutedEventArgs e)
    {
        _resolved++;
        Log.Text = _resolved == 1 ? "Resolved once." : $"Resolved {_resolved} times.";
    }
}
