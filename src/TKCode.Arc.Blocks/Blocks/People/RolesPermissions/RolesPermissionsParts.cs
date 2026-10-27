using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;

namespace TKCode.Arc;

/// <summary>
/// Hosts the matrix's horizontal scroller and tells the grid how wide the visible area is, so the label column can take
/// what the role columns leave (a scroller measures its content with unlimited width).
/// </summary>
internal sealed class RolesPermissionsViewport : Decorator
{
    public RolesPermissionsGrid? Grid { get; set; }

    protected override Size MeasureOverride(Size availableSize)
    {
        if (Grid is not null && !double.IsInfinity(availableSize.Width))
        {
            Grid.ViewportWidth = availableSize.Width;
        }
        return base.MeasureOverride(availableSize);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        if (Grid is not null)
        {
            Grid.ViewportWidth = finalSize.Width;
        }
        return base.ArrangeOverride(finalSize);
    }
}

/// <summary>
/// The header row: titles on the left and the members' avatars on the right, bottom-aligned with 24 between them; when
/// both do not fit, the avatars move under the titles (12 between the lines), like a wrapping flex row.
/// </summary>
internal sealed class RolesPermissionsHeaderPanel : Panel
{
    public static readonly StyledProperty<double> ColumnGapProperty =
        AvaloniaProperty.Register<RolesPermissionsHeaderPanel, double>(nameof(ColumnGap), 24);

    public static readonly StyledProperty<double> RowGapProperty =
        AvaloniaProperty.Register<RolesPermissionsHeaderPanel, double>(nameof(RowGap), 12);

    static RolesPermissionsHeaderPanel()
    {
        AffectsMeasure<RolesPermissionsHeaderPanel>(ColumnGapProperty, RowGapProperty);
    }

    public double ColumnGap { get => GetValue(ColumnGapProperty); set => SetValue(ColumnGapProperty, value); }

    public double RowGap { get => GetValue(RowGapProperty); set => SetValue(RowGapProperty, value); }

    internal bool IsStacked { get; private set; }

    protected override Size MeasureOverride(Size availableSize)
    {
        var visible = Children.Where(c => c.IsVisible).ToList();
        foreach (var child in visible)
        {
            child.Measure(new Size(availableSize.Width, double.PositiveInfinity));
        }
        if (visible.Count < 2)
        {
            IsStacked = false;
            var only = visible.FirstOrDefault()?.DesiredSize ?? default;
            return only;
        }
        var a = visible[0].DesiredSize;
        var b = visible[1].DesiredSize;
        IsStacked = a.Width + ColumnGap + b.Width > availableSize.Width;
        return IsStacked
            ? new Size(Math.Max(a.Width, b.Width), a.Height + RowGap + b.Height)
            : new Size(a.Width + ColumnGap + b.Width, Math.Max(a.Height, b.Height));
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var visible = Children.Where(c => c.IsVisible).ToList();
        if (visible.Count == 0)
        {
            return finalSize;
        }
        if (visible.Count == 1)
        {
            visible[0].Arrange(new Rect(0, 0, finalSize.Width, finalSize.Height));
            return finalSize;
        }
        var a = visible[0].DesiredSize;
        var b = visible[1].DesiredSize;
        if (IsStacked)
        {
            visible[0].Arrange(new Rect(0, 0, finalSize.Width, a.Height));
            visible[1].Arrange(new Rect(0, a.Height + RowGap, b.Width, b.Height));
        }
        else
        {
            var height = Math.Max(a.Height, b.Height);
            visible[0].Arrange(new Rect(0, height - a.Height, Math.Max(0, finalSize.Width - b.Width - ColumnGap), a.Height));
            visible[1].Arrange(new Rect(finalSize.Width - b.Width, height - b.Height, b.Width, b.Height));
        }
        return finalSize;
    }
}
