using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>The round button inside <see cref="BackToTop"/>: an Arc button (same theme) that the BackToTop names and enables.</summary>
internal sealed class BackToTopButton : Button
{
    protected override Type StyleKeyOverride => typeof(Button);
}

/// <summary>
/// The raised frame around <see cref="BackToTopButton"/> that carries the show / hide pose. <see cref="Scale"/> and
/// <see cref="OffsetY"/> are plain doubles so the theme can set them from tokens and give them spring transitions (a
/// transform string in a setter could not read the token values).
/// </summary>
internal sealed class BackToTopPresenter : Border
{
    /// <summary>Uniform scale of the button around its centre.</summary>
    public static readonly StyledProperty<double> ScaleProperty =
        AvaloniaProperty.Register<BackToTopPresenter, double>(nameof(Scale), 1);

    /// <summary>Vertical shift of the button in pixels (positive moves it down).</summary>
    public static readonly StyledProperty<double> OffsetYProperty =
        AvaloniaProperty.Register<BackToTopPresenter, double>(nameof(OffsetY));

    private readonly MatrixTransform _transform = new();

    public BackToTopPresenter()
    {
        RenderTransform = _transform;
        RenderTransformOrigin = RelativePoint.Center;
    }

    public double Scale { get => GetValue(ScaleProperty); set => SetValue(ScaleProperty, value); }

    public double OffsetY { get => GetValue(OffsetYProperty); set => SetValue(OffsetYProperty, value); }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ScaleProperty || change.Property == OffsetYProperty)
        {
            _transform.Matrix = Matrix.CreateScale(Scale, Scale) * Matrix.CreateTranslation(0, OffsetY);
        }
    }
}

/// <summary>
/// Hosts the button of <c>BackToTop.IsAttached</c> in the container's adorner layer. The layer sizes it to the container;
/// it puts the button in the configured corner of the container's ScrollViewer, inset by <c>BackToTop.Inset</c>.
/// </summary>
internal sealed class BackToTopAdorner : Control
{
    private readonly Control _host;
    private readonly IDisposable _threshold;

    public BackToTopAdorner(Control host)
    {
        _host = host;
        Button = new BackToTop { Target = host };
        _threshold = Button.Bind(BackToTop.ThresholdProperty, host.GetObservable(BackToTop.ThresholdProperty));
        Button.ViewportChanged += InvalidateArrange;
        LogicalChildren.Add(Button);
        VisualChildren.Add(Button);
    }

    public BackToTop Button { get; }

    /// <summary>Unhooks the button from the host (it was removed from the adorner layer).</summary>
    public void Release()
    {
        _threshold.Dispose();
        Button.ViewportChanged -= InvalidateArrange;
        Button.Target = null;
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        Button.Measure(Size.Infinity);
        return default;
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var viewer = Button.Viewer;
        var viewport = new Rect(finalSize);
        if (viewer is not null && viewer.TranslatePoint(default, _host) is { } origin)
        {
            viewport = new Rect(origin, viewer.Bounds.Size);
        }
        var inset = BackToTop.GetInset(_host);
        var placement = BackToTop.GetPlacement(_host);
        var size = Button.DesiredSize;
        var right = placement is BackToTopPlacement.BottomRight or BackToTopPlacement.TopRight;
        var bottom = placement is BackToTopPlacement.BottomRight or BackToTopPlacement.BottomLeft;
        var x = right ? viewport.Right - inset.Right - size.Width : viewport.X + inset.Left;
        var y = bottom ? viewport.Bottom - inset.Bottom - size.Height : viewport.Y + inset.Top;
        Button.Arrange(new Rect(new Point(x, y), size));
        return finalSize;
    }
}
