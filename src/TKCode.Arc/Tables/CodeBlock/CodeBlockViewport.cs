using Avalonia;
using Avalonia.Controls;

namespace TKCode.Arc;

/// <summary>
/// The source area inside the height spring. Its desired height is the collapsed height (Arc: <c>maxLines</c>
/// lines plus padding) or the full source height, but the source is always arranged at full height: the
/// surrounding <see cref="Primitives.SpringSizeDecorator"/> clips, so collapsing slides the bottom edge up over
/// the code instead of cutting it first (Arc animates the viewport height of a full-height <c>pre</c>).
/// </summary>
internal sealed class CodeBlockViewport : Decorator
{
    public static readonly StyledProperty<bool> IsCollapsedProperty =
        AvaloniaProperty.Register<CodeBlockViewport, bool>(nameof(IsCollapsed));

    public static readonly StyledProperty<double> CollapsedHeightProperty =
        AvaloniaProperty.Register<CodeBlockViewport, double>(nameof(CollapsedHeight), double.NaN);

    static CodeBlockViewport()
    {
        AffectsMeasure<CodeBlockViewport>(IsCollapsedProperty, CollapsedHeightProperty);
    }

    public bool IsCollapsed
    {
        get => GetValue(IsCollapsedProperty);
        set => SetValue(IsCollapsedProperty, value);
    }

    public double CollapsedHeight
    {
        get => GetValue(CollapsedHeightProperty);
        set => SetValue(CollapsedHeightProperty, value);
    }

    /// <summary>Height of the whole source (padding included) from the last measure.</summary>
    public double FullHeight { get; private set; }

    protected override Size MeasureOverride(Size availableSize)
    {
        if (Child is not { } child)
        {
            return default;
        }
        child.Measure(availableSize.WithHeight(double.PositiveInfinity));
        var desired = child.DesiredSize;
        FullHeight = desired.Height;
        var height = IsCollapsed && !double.IsNaN(CollapsedHeight) ? Math.Min(desired.Height, CollapsedHeight) : desired.Height;
        return new Size(desired.Width, height);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        Child?.Arrange(new Rect(0, 0, finalSize.Width, Math.Max(finalSize.Height, FullHeight)));
        return finalSize;
    }
}
