using Avalonia;
using Avalonia.Controls;

namespace TKCode.Arc;

/// <summary>
/// The buttons of an <see cref="AiFeatureLaunch"/>: in a row at the end with 8 between them, or, when
/// <see cref="IsStacked"/> (narrow cards), one under the other at full width with the main button on top.
/// </summary>
internal sealed class AiFeatureLaunchFooter : Panel
{
    public static readonly StyledProperty<bool> IsStackedProperty =
        AvaloniaProperty.Register<AiFeatureLaunchFooter, bool>(nameof(IsStacked));

    public static readonly StyledProperty<double> GapProperty =
        AvaloniaProperty.Register<AiFeatureLaunchFooter, double>(nameof(Gap), 8);

    static AiFeatureLaunchFooter()
    {
        AffectsMeasure<AiFeatureLaunchFooter>(IsStackedProperty, GapProperty);
    }

    public bool IsStacked { get => GetValue(IsStackedProperty); set => SetValue(IsStackedProperty, value); }

    public double Gap { get => GetValue(GapProperty); set => SetValue(GapProperty, value); }

    protected override Size MeasureOverride(Size availableSize)
    {
        double width = 0, height = 0;
        var visible = 0;
        foreach (var child in Children)
        {
            child.Measure(IsStacked ? availableSize.WithHeight(double.PositiveInfinity) : Size.Infinity);
            if (!child.IsVisible)
            {
                continue;
            }
            var gap = visible++ > 0 ? Gap : 0;
            if (IsStacked)
            {
                width = Math.Max(width, child.DesiredSize.Width);
                height += child.DesiredSize.Height + gap;
            }
            else
            {
                width += child.DesiredSize.Width + gap;
                height = Math.Max(height, child.DesiredSize.Height);
            }
        }
        return new Size(double.IsFinite(availableSize.Width) && IsStacked ? availableSize.Width : width, height);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        if (IsStacked)
        {
            // The main button (the last child) goes on top.
            double y = 0;
            for (var i = Children.Count - 1; i >= 0; i--)
            {
                var child = Children[i];
                if (!child.IsVisible)
                {
                    continue;
                }
                child.Arrange(new Rect(0, y, finalSize.Width, child.DesiredSize.Height));
                y += child.DesiredSize.Height + Gap;
            }
            return finalSize;
        }
        var x = finalSize.Width;
        for (var i = Children.Count - 1; i >= 0; i--)
        {
            var child = Children[i];
            if (!child.IsVisible)
            {
                continue;
            }
            x -= child.DesiredSize.Width;
            child.Arrange(new Rect(x, (finalSize.Height - child.DesiredSize.Height) / 2, child.DesiredSize.Width, child.DesiredSize.Height));
            x -= Gap;
        }
        return finalSize;
    }
}
