using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>
/// What a floating <see cref="RadialMenu"/> mounts on the overlay layer: a transparent layer as large as the overlay
/// (a press on it, outside the ring, closes the menu, and nothing under it reacts) with the ring placed at
/// <see cref="Center"/>. The ring is a visual child only: its logical parent is the menu's target.
/// </summary>
internal sealed class RadialMenuHost : Control
{
    private Point _center;

    public RadialMenuHost(RadialMenu menu)
    {
        Menu = menu;
        VisualChildren.Add(menu);
    }

    public RadialMenu Menu { get; }

    /// <summary>The ring's centre in host coordinates (after edge avoidance).</summary>
    public Point Center
    {
        get => _center;
        set
        {
            if (_center != value)
            {
                _center = value;
                InvalidateArrange();
            }
        }
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        Menu.Measure(Size.Infinity);
        var width = double.IsFinite(availableSize.Width) ? availableSize.Width : 0;
        var height = double.IsFinite(availableSize.Height) ? availableSize.Height : 0;
        return new Size(width, height);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var size = Menu.DesiredSize;
        Menu.Arrange(new Rect(_center.X - size.Width / 2, _center.Y - size.Height / 2, size.Width, size.Height));
        return finalSize;
    }

    public override void Render(DrawingContext context)
    {
        // Transparent, but hit-testable everywhere: presses outside the ring land here.
        context.FillRectangle(Brushes.Transparent, new Rect(Bounds.Size));
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (!ReferenceEquals(e.Source, this))
        {
            return;
        }
        e.Handled = true;
        Menu.OnOutsidePressed(e);
    }

    protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
    {
        base.OnPointerWheelChanged(e);
        // Content under an open menu does not scroll away from it.
        e.Handled = true;
    }
}
