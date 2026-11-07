using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using TKCode.Arc.Motion;

namespace TKCode.Arc.Gallery.Demos;

public partial class MorphLoaderPreviewDemo : UserControl
{
    // The pill under the chosen shape glides on a critically damped spring.
    private static readonly SpringSpec s_glide = SpringSpec.FromVisual(0.42, 0);

    // How long a result stays before the demo starts loading again.
    private static readonly TimeSpan s_resultHold = TimeSpan.FromMilliseconds(1700);

    private readonly DispatcherTimer _reset;
    private Button? _chosen;
    private Rect _placed;

    public MorphLoaderPreviewDemo()
    {
        InitializeComponent();
        _reset = new DispatcherTimer { Interval = s_resultHold };
        _reset.Tick += (_, _) => SetStatus(MorphLoaderStatus.Loading);
        _chosen = Options.Children.OfType<Button>().First();
        foreach (var option in Options.Children.OfType<Button>())
        {
            AutomationProperties.SetName(option, option.Content as string);
        }
        // The pill lands under the first option once it has a size, then follows every choice.
        Options.LayoutUpdated += (_, _) => PlacePill(false);
        DetachedFromVisualTree += (_, _) => _reset.Stop();
    }

    private void OnShape(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: MorphLoaderVariant variant } option)
        {
            return;
        }
        _chosen?.Classes.Remove("chosen");
        _chosen = option;
        option.Classes.Add("chosen");
        Loader.Variant = variant;
        PlacePill(true);
    }

    private void OnResolve(object? sender, RoutedEventArgs e) => ShowResult(MorphLoaderStatus.Success);

    private void OnReject(object? sender, RoutedEventArgs e) => ShowResult(MorphLoaderStatus.Error);

    private void ShowResult(MorphLoaderStatus status)
    {
        SetStatus(status);
        _reset.Stop();
        _reset.Start();
    }

    private void SetStatus(MorphLoaderStatus status)
    {
        _reset.Stop();
        Loader.Status = status;
        // While a result shows, only the shape can change.
        var loading = status == MorphLoaderStatus.Loading;
        Resolve.IsEnabled = loading;
        Reject.IsEnabled = loading;
    }

    private void PlacePill(bool animate)
    {
        if (_chosen is not { } option || option.Bounds.Width <= 0)
        {
            return;
        }
        // A layout pass only moves the pill when the option itself moved (first layout, a resize): then it snaps.
        var target = option.Bounds;
        if (target == _placed)
        {
            return;
        }
        _placed = target;
        Pill.MoveTo(target, s_glide, animate && Pill.HasTarget);
    }
}
