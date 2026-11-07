using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;

namespace TKCode.Arc.Gallery.Demos;

public partial class SkeletonMorphStaggerDemo : UserControl
{
    private readonly DispatcherTimer _request = new() { Interval = TimeSpan.FromSeconds(1) };

    public SkeletonMorphStaggerDemo()
    {
        InitializeComponent();
        _request.Tick += (_, _) => SetLoading(false);
        AttachedToVisualTree += (_, _) => OnReload(null, null);
        DetachedFromVisualTree += (_, _) => _request.Stop();
    }

    private void OnReload(object? sender, RoutedEventArgs? e)
    {
        SetLoading(true);
        _request.Stop();
        _request.Start();
    }

    private void SetLoading(bool loading)
    {
        if (!loading)
        {
            _request.Stop();
        }
        Together.IsLoading = loading;
        Cascade.IsLoading = loading;
    }
}
