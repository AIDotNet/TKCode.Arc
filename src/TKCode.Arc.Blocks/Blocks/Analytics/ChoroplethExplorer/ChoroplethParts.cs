using Avalonia;
using Avalonia.Controls;
using AvaloniaButton = Avalonia.Controls.Button;

namespace TKCode.Arc;

/// <summary>
/// The body of a <see cref="ChoroplethExplorer"/>: the map column and the list column. Wide (from a block width of 820) they
/// stand side by side, the list <see cref="SideWidth"/> wide and exactly as tall as the map column (its rows scroll inside);
/// otherwise the list goes under the map with <see cref="StackGap"/> between them.
/// </summary>
internal sealed class ChoroplethBodyPanel : Panel
{
    public static readonly StyledProperty<bool> IsWideProperty =
        AvaloniaProperty.Register<ChoroplethBodyPanel, bool>(nameof(IsWide), true);

    public static readonly StyledProperty<double> SideWidthProperty =
        AvaloniaProperty.Register<ChoroplethBodyPanel, double>(nameof(SideWidth), 300);

    public static readonly StyledProperty<double> ColumnGapProperty =
        AvaloniaProperty.Register<ChoroplethBodyPanel, double>(nameof(ColumnGap), 32);

    public static readonly StyledProperty<double> StackGapProperty =
        AvaloniaProperty.Register<ChoroplethBodyPanel, double>(nameof(StackGap), 24);

    static ChoroplethBodyPanel()
    {
        AffectsMeasure<ChoroplethBodyPanel>(IsWideProperty, SideWidthProperty, ColumnGapProperty, StackGapProperty);
    }

    public bool IsWide { get => GetValue(IsWideProperty); set => SetValue(IsWideProperty, value); }

    public double SideWidth { get => GetValue(SideWidthProperty); set => SetValue(SideWidthProperty, value); }

    public double ColumnGap { get => GetValue(ColumnGapProperty); set => SetValue(ColumnGapProperty, value); }

    public double StackGap { get => GetValue(StackGapProperty); set => SetValue(StackGapProperty, value); }

    private Control? Main => Children.Count > 0 ? Children[0] : null;

    private Control? Side => Children.Count > 1 ? Children[1] : null;

    protected override Size MeasureOverride(Size availableSize)
    {
        var width = double.IsFinite(availableSize.Width) ? availableSize.Width : 1112;
        if (Main is not { } main)
        {
            return default;
        }
        if (IsWide && Side is { } side)
        {
            var mainWidth = Math.Max(0, width - SideWidth - ColumnGap);
            main.Measure(new Size(mainWidth, double.PositiveInfinity));
            var height = main.DesiredSize.Height;
            side.Measure(new Size(SideWidth, height));
            return new Size(width, height);
        }
        main.Measure(new Size(width, double.PositiveInfinity));
        var total = main.DesiredSize.Height;
        if (Side is { } stacked)
        {
            stacked.Measure(new Size(width, double.PositiveInfinity));
            total += StackGap + stacked.DesiredSize.Height;
        }
        return new Size(width, total);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        if (Main is not { } main)
        {
            return finalSize;
        }
        if (IsWide && Side is { } side)
        {
            var mainWidth = Math.Max(0, finalSize.Width - SideWidth - ColumnGap);
            main.Arrange(new Rect(0, 0, mainWidth, main.DesiredSize.Height));
            side.Arrange(new Rect(mainWidth + ColumnGap, 0, SideWidth, main.DesiredSize.Height));
            return finalSize;
        }
        main.Arrange(new Rect(0, 0, finalSize.Width, main.DesiredSize.Height));
        Side?.Arrange(new Rect(0, main.DesiredSize.Height + StackGap, finalSize.Width, Side.DesiredSize.Height));
        return finalSize;
    }
}

/// <summary>The pill that zooms the map back out ("All states", "Back to all states"): arrow, text, 36 high, surface and border.</summary>
internal sealed class ChoroplethBackButton : AvaloniaButton
{
    protected override Type StyleKeyOverride => typeof(ChoroplethBackButton);
}
