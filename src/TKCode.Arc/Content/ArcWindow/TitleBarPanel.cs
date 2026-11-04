using Avalonia;
using Avalonia.Controls;

namespace TKCode.Arc;

/// <summary>
/// Lays out a <see cref="TitleBar"/>'s three sections, its children in order: the left section at the start, the right
/// section at the end and the center section between them. The right section (with the caption buttons) is measured first
/// and keeps its width; the left one takes what is left. The center is centered on the whole bar while that keeps it clear
/// of the sides, otherwise it moves as little as it can towards the wider side, and fills the gap when the gap is narrower.
/// </summary>
internal sealed class TitleBarPanel : Panel
{
    /// <summary>The smallest gap between two sections.</summary>
    public static readonly StyledProperty<double> SpacingProperty =
        AvaloniaProperty.Register<TitleBarPanel, double>(nameof(Spacing));

    private double _leftWidth;
    private double _rightWidth;

    static TitleBarPanel()
    {
        AffectsMeasure<TitleBarPanel>(SpacingProperty);
    }

    public double Spacing
    {
        get => GetValue(SpacingProperty);
        set => SetValue(SpacingProperty, value);
    }

    private Control? Left => Children.Count > 0 ? Children[0] : null;

    private Control? Center => Children.Count > 1 ? Children[1] : null;

    private Control? Right => Children.Count > 2 ? Children[2] : null;

    protected override Size MeasureOverride(Size availableSize)
    {
        var width = availableSize.Width;
        var height = availableSize.Height;
        var spacing = Spacing;

        var right = Measure(Right, new Size(double.PositiveInfinity, height));
        _rightWidth = double.IsFinite(width) ? Math.Min(right.Width, width) : right.Width;

        var leftAvailable = double.IsFinite(width) ? Math.Max(0, width - _rightWidth - Gap(Right, spacing)) : double.PositiveInfinity;
        var left = Measure(Left, new Size(leftAvailable, height));
        _leftWidth = Math.Min(left.Width, leftAvailable);

        var centerAvailable = double.IsFinite(width)
            ? Math.Max(0, width - _leftWidth - _rightWidth - Gap(Left, spacing) - Gap(Right, spacing))
            : double.PositiveInfinity;
        var center = Measure(Center, new Size(centerAvailable, height));

        var sections = (IsShown(Left) ? 1 : 0) + (IsShown(Center) ? 1 : 0) + (IsShown(Right) ? 1 : 0);
        var desiredWidth = _leftWidth + _rightWidth + Math.Min(center.Width, centerAvailable)
                           + spacing * Math.Max(0, sections - 1);
        var desiredHeight = Math.Max(left.Height, Math.Max(center.Height, right.Height));
        return new Size(double.IsFinite(width) ? Math.Min(desiredWidth, width) : desiredWidth, desiredHeight);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var width = finalSize.Width;
        var height = finalSize.Height;
        var spacing = Spacing;
        var rightWidth = Math.Min(_rightWidth, width);
        var leftWidth = Math.Min(_leftWidth, Math.Max(0, width - rightWidth));

        Left?.Arrange(new Rect(0, 0, leftWidth, height));
        Right?.Arrange(new Rect(width - rightWidth, 0, rightWidth, height));

        if (Center is { } center)
        {
            var start = leftWidth + Gap(Left, spacing);
            var end = width - rightWidth - Gap(Right, spacing);
            var gap = Math.Max(0, end - start);
            var centerWidth = Math.Min(center.DesiredSize.Width, gap);
            var x = Place(width, start, end, centerWidth);
            center.Arrange(new Rect(x, 0, centerWidth, height));
        }
        return finalSize;
    }

    /// <summary>
    /// Where a center section of <paramref name="centerWidth"/> starts: centered on the whole bar, then pushed back inside
    /// [<paramref name="start"/>, <paramref name="end"/>] when that overlaps a side.
    /// </summary>
    internal static double Place(double width, double start, double end, double centerWidth)
    {
        var x = (width - centerWidth) / 2;
        x = Math.Min(x, end - centerWidth);
        return Math.Max(x, start);
    }

    private static Size Measure(Control? child, Size available)
    {
        if (child is null)
        {
            return default;
        }
        child.Measure(available);
        return child.DesiredSize;
    }

    private static bool IsShown(Control? child) => child is { IsVisible: true } && child.DesiredSize.Width > 0;

    /// <summary>The spacing after or before a section that takes room; none for an empty one.</summary>
    private static double Gap(Control? section, double spacing) => IsShown(section) ? spacing : 0;
}
