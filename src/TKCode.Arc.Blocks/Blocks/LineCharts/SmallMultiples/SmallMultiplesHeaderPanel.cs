using Avalonia;
using Avalonia.Controls;

namespace TKCode.Arc;

/// <summary>
/// The header row of a <see cref="SmallMultiples"/>: the heading (first child) on the left and the controls (second child)
/// on the right while both fit at their natural widths with 24 between them; otherwise the controls move under the
/// heading, 16 below it, and wrap there.
/// </summary>
internal sealed class SmallMultiplesHeaderPanel : Panel
{
    private const double ColumnGap = 24;
    private const double RowGap = 16;

    private bool _stacked;

    /// <summary>Whether the controls sit under the heading (tests).</summary>
    internal bool IsStacked => _stacked;

    protected override Size MeasureOverride(Size availableSize)
    {
        if (Children.Count < 2)
        {
            return base.MeasureOverride(availableSize);
        }
        var heading = Children[0];
        var controls = Children[1];
        var width = availableSize.Width;
        heading.Measure(Size.Infinity);
        controls.Measure(Size.Infinity);
        var natural = heading.DesiredSize.Width + ColumnGap + controls.DesiredSize.Width;
        _stacked = double.IsFinite(width) && natural > width;
        if (!_stacked)
        {
            var rest = double.IsFinite(width) ? Math.Max(0, width - controls.DesiredSize.Width - ColumnGap) : double.PositiveInfinity;
            heading.Measure(new Size(rest, availableSize.Height));
            var rowWidth = double.IsFinite(width) ? width : natural;
            return new Size(rowWidth, Math.Max(heading.DesiredSize.Height, controls.DesiredSize.Height));
        }
        heading.Measure(new Size(width, double.PositiveInfinity));
        controls.Measure(new Size(width, double.PositiveInfinity));
        return new Size(width, heading.DesiredSize.Height + RowGap + controls.DesiredSize.Height);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        if (Children.Count < 2)
        {
            return base.ArrangeOverride(finalSize);
        }
        var heading = Children[0];
        var controls = Children[1];
        if (!_stacked)
        {
            var height = finalSize.Height;
            var controlsWidth = controls.DesiredSize.Width;
            heading.Arrange(new Rect(0, (height - heading.DesiredSize.Height) / 2,
                Math.Max(0, finalSize.Width - controlsWidth - ColumnGap), heading.DesiredSize.Height));
            controls.Arrange(new Rect(finalSize.Width - controlsWidth, (height - controls.DesiredSize.Height) / 2, controlsWidth,
                controls.DesiredSize.Height));
            return finalSize;
        }
        heading.Arrange(new Rect(0, 0, finalSize.Width, heading.DesiredSize.Height));
        var top = heading.DesiredSize.Height + RowGap;
        controls.Arrange(new Rect(0, top, finalSize.Width, controls.DesiredSize.Height));
        return finalSize;
    }
}
