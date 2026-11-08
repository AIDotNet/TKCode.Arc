using Avalonia;
using Avalonia.Controls;
using TKCode.Arc.Primitives;

namespace TKCode.Arc;

/// <summary>
/// The card surface of <see cref="CheckoutSummary"/>: its padding follows the viewport like the reference's
/// <c>clamp(20px, 5vw, 28px)</c> (a viewport rule there, so the viewport width is read here too, through
/// <see cref="ArcViewport"/> so phone previews count).
/// </summary>
internal sealed class CheckoutSummaryCard : Border
{
    public static readonly StyledProperty<double> PaddingMinProperty =
        AvaloniaProperty.Register<CheckoutSummaryCard, double>(nameof(PaddingMin), 20);

    public static readonly StyledProperty<double> PaddingMaxProperty =
        AvaloniaProperty.Register<CheckoutSummaryCard, double>(nameof(PaddingMax), 28);

    public static readonly StyledProperty<double> PaddingFactorProperty =
        AvaloniaProperty.Register<CheckoutSummaryCard, double>(nameof(PaddingFactor), 0.05);

    private IDisposable? _watch;
    private double _viewportWidth = double.NaN;

    public double PaddingMin { get => GetValue(PaddingMinProperty); set => SetValue(PaddingMinProperty, value); }

    public double PaddingMax { get => GetValue(PaddingMaxProperty); set => SetValue(PaddingMaxProperty, value); }

    public double PaddingFactor { get => GetValue(PaddingFactorProperty); set => SetValue(PaddingFactorProperty, value); }

    /// <summary>Padding for a viewport <paramref name="width"/> wide (NaN = unknown, the largest).</summary>
    public static double PaddingFor(double width, double min, double max, double factor)
        => double.IsNaN(width) || width <= 0 ? max : Math.Clamp(width * factor, min, Math.Max(min, max));

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _watch?.Dispose();
        _watch = ArcViewport.Observe(this, size =>
        {
            _viewportWidth = size.Width;
            UpdatePadding();
        });
        UpdatePadding();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _watch?.Dispose();
        _watch = null;
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == PaddingMinProperty || change.Property == PaddingMaxProperty || change.Property == PaddingFactorProperty)
        {
            UpdatePadding();
        }
    }

    private void UpdatePadding()
    {
        var p = PaddingFor(_viewportWidth, PaddingMin, PaddingMax, PaddingFactor);
        Padding = new Thickness(p);
    }
}
