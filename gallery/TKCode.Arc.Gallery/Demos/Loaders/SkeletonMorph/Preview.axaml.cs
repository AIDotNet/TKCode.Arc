using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;

namespace TKCode.Arc.Gallery.Demos;

public partial class SkeletonMorphPreviewDemo : UserControl
{
    // A pretend request: the first load takes 1.2 s, a reload 1.5 s.
    private static readonly TimeSpan FirstLoad = TimeSpan.FromMilliseconds(1200);
    private static readonly TimeSpan ReloadTime = TimeSpan.FromMilliseconds(1500);

    private readonly DispatcherTimer _request = new();

    public SkeletonMorphPreviewDemo()
    {
        InitializeComponent();
        _request.Tick += (_, _) => SetLoading(false);
        AttachedToVisualTree += (_, _) => StartRequest(FirstLoad);
        DetachedFromVisualTree += (_, _) => _request.Stop();
        SizeChanged += (_, _) => LayOutCards();
        Foot.SizeChanged += (_, _) => WrapFoot();
        Stats.SizeChanged += (_, _) => WrapFoot();
    }

    private void OnReload(object? sender, RoutedEventArgs e)
    {
        // Ignored while loading (the button stays focusable, so keyboard focus is not lost).
        if (!Profile.IsLoading)
        {
            StartRequest(ReloadTime);
        }
    }

    private void StartRequest(TimeSpan duration)
    {
        SetLoading(true);
        _request.Stop();
        _request.Interval = duration;
        _request.Start();
    }

    private void SetLoading(bool loading)
    {
        if (!loading)
        {
            _request.Stop();
        }
        Profile.IsLoading = loading;
        Transfers.IsLoading = loading;
        ReloadHost.Classes.Set("busy", loading);
    }

    // Two 312-wide columns while both fit at 300 or more, otherwise one column as wide as the stage (at most 640).
    private void LayOutCards()
    {
        var width = Math.Min(640, Bounds.Width - 32);
        if (width <= 0)
        {
            return;
        }
        var column = (width - 16) / 2;
        var twoColumns = column >= 300;
        Cards.Width = twoColumns ? 2 * Math.Min(312, column) + 16 : width;
        Cards.ColumnDefinitions[1].Width = twoColumns ? new GridLength(1, GridUnitType.Star) : new GridLength(0);
        Cards.ColumnSpacing = twoColumns ? 16 : 0;
        Grid.SetColumn(Transfers, twoColumns ? 1 : 0);
        Grid.SetRow(Transfers, twoColumns ? 0 : 1);
    }

    // The footer is a wrapping row: stats on the left, Follow on the right, or under the stats when both do not fit.
    private void WrapFoot()
    {
        var fits = Stats.DesiredSize.Width + 8 + FollowBlock.DesiredSize.Width <= Foot.Bounds.Width;
        Grid.SetColumnSpan(Stats, fits ? 1 : 2);
        Grid.SetRow(FollowBlock, fits ? 0 : 1);
        Grid.SetColumn(FollowBlock, fits ? 1 : 0);
        FollowBlock.Margin = fits ? default : new Thickness(0, 8, 0, 0);
    }
}
