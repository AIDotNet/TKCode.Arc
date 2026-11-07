using Avalonia;
using Avalonia.Controls;

namespace TKCode.Arc.Primitives;

/// <summary>
/// A horizontal row like a horizontal <see cref="StackPanel"/>, except that one child (marked with
/// <see cref="IsShrinkableProperty"/>) gets only the width left by the others. A <see cref="TextBlock"/> marked so can
/// end with an ellipsis or wrap, which it never does in a horizontal StackPanel: that panel offers its children
/// infinite width, so their trimming and wrapping never apply and the end of the text is cut off by whatever clips.
/// </summary>
/// <remarks>
/// With room to spare it lays out exactly like a horizontal StackPanel: children in order at their desired widths,
/// <see cref="Spacing"/> between visible children only, every child as tall as the row. Children after the shrinkable one
/// stay right after it.
/// <code>
/// &lt;arc:ArcShrinkRow Spacing="6"&gt;
///     &lt;arc:ArcGlyph Data="{x:Static arc:ArcIcons.Info}" Size="14" /&gt;
///     &lt;TextBlock Text="{Binding Title}" TextTrimming="CharacterEllipsis" arc:ArcShrinkRow.IsShrinkable="True" /&gt;
///     &lt;arc:Badge Content="New" /&gt;
/// &lt;/arc:ArcShrinkRow&gt;
/// </code>
/// </remarks>
public class ArcShrinkRow : Panel
{
    /// <summary>The gap between visible children.</summary>
    public static readonly StyledProperty<double> SpacingProperty =
        AvaloniaProperty.Register<ArcShrinkRow, double>(nameof(Spacing));

    /// <summary>Marks the child that gets the width left by the others (the first marked visible child counts).</summary>
    public static readonly AttachedProperty<bool> IsShrinkableProperty =
        AvaloniaProperty.RegisterAttached<ArcShrinkRow, Control, bool>("IsShrinkable");

    static ArcShrinkRow()
    {
        AffectsMeasure<ArcShrinkRow>(SpacingProperty);
        AffectsParentMeasure<ArcShrinkRow>(IsShrinkableProperty);
    }

    /// <inheritdoc cref="SpacingProperty"/>
    public double Spacing
    {
        get => GetValue(SpacingProperty);
        set => SetValue(SpacingProperty, value);
    }

    /// <summary>Whether <paramref name="control"/> is the child that shrinks.</summary>
    public static bool GetIsShrinkable(Control control) => control.GetValue(IsShrinkableProperty);

    /// <summary>Makes <paramref name="control"/> the child that shrinks.</summary>
    public static void SetIsShrinkable(Control control, bool value) => control.SetValue(IsShrinkableProperty, value);

    private Control? Shrinkable()
    {
        foreach (var child in Children)
        {
            if (child.IsVisible && GetIsShrinkable(child))
            {
                return child;
            }
        }
        return null;
    }

    /// <inheritdoc />
    protected override Size MeasureOverride(Size availableSize)
    {
        var shrinkable = Shrinkable();
        var fixedWidth = 0.0;
        var height = 0.0;
        var visible = 0;
        foreach (var child in Children)
        {
            if (!child.IsVisible)
            {
                continue;
            }
            visible++;
            if (ReferenceEquals(child, shrinkable))
            {
                continue;
            }
            child.Measure(new Size(double.PositiveInfinity, availableSize.Height));
            fixedWidth += child.DesiredSize.Width;
            height = Math.Max(height, child.DesiredSize.Height);
        }
        var gaps = Math.Max(0, visible - 1) * Spacing;
        var width = fixedWidth + gaps;
        if (shrinkable is not null)
        {
            var room = double.IsInfinity(availableSize.Width) ? double.PositiveInfinity : Math.Max(0, availableSize.Width - width);
            shrinkable.Measure(new Size(room, availableSize.Height));
            width += shrinkable.DesiredSize.Width;
            height = Math.Max(height, shrinkable.DesiredSize.Height);
        }
        return new Size(width, height);
    }

    /// <inheritdoc />
    protected override Size ArrangeOverride(Size finalSize)
    {
        var shrinkable = Shrinkable();
        var others = 0.0;
        var visible = 0;
        foreach (var child in Children)
        {
            if (!child.IsVisible)
            {
                continue;
            }
            visible++;
            if (!ReferenceEquals(child, shrinkable))
            {
                others += child.DesiredSize.Width;
            }
        }
        var room = Math.Max(0, finalSize.Width - others - Math.Max(0, visible - 1) * Spacing);
        var x = 0.0;
        var first = true;
        foreach (var child in Children)
        {
            if (!child.IsVisible)
            {
                continue;
            }
            if (!first)
            {
                x += Spacing;
            }
            first = false;
            var width = ReferenceEquals(child, shrinkable) ? Math.Min(child.DesiredSize.Width, room) : child.DesiredSize.Width;
            child.Arrange(new Rect(x, 0, width, finalSize.Height));
            x += width;
        }
        return finalSize;
    }
}
