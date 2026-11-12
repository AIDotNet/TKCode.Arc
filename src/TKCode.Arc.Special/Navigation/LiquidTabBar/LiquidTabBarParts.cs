using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Data.Core;
using Avalonia.Markup.Xaml.MarkupExtensions.CompiledBindings;
using Avalonia.Media;
using Avalonia.VisualTree;

namespace TKCode.Arc;

/// <summary>Which tabs of a <see cref="LiquidTabBar"/> show their label.</summary>
public enum LiquidTabBarLabels
{
    /// <summary>Only the selected tab: it opens to show its label, the others show their glyph alone.</summary>
    Active,

    /// <summary>Every tab shows its label.</summary>
    Always,

    /// <summary>No tab shows its label (glyphs only); screen readers still hear the labels.</summary>
    None
}

/// <summary>
/// The default items panel of a <see cref="LiquidTabBar"/>: lays the tabs out in a row at their current (animated)
/// widths, inside a box as wide as the tabs will be once they settle.
/// </summary>
internal sealed class LiquidTabBarPanel : Panel
{
    private LiquidTabBar? _owner;

    private LiquidTabBar? Owner => _owner ??= this.FindAncestorOfType<LiquidTabBar>();

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _owner = null;
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        foreach (var child in Children)
        {
            child.Measure(Size.Infinity);
        }
        return Owner?.MeasureTabs(Children) ?? new Size(0, LiquidTabBarMetrics.TrackHeight);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        Owner?.ArrangeTabs(Children, finalSize);
        return finalSize;
    }
}

/// <summary>
/// The lens of a <see cref="LiquidTabBar"/>, drawn above the tabs: inside its rounded rectangle it fills the lens color
/// and redraws the tabs it covers inverted. Nothing outside the lens is drawn, and it takes no input.
/// </summary>
internal sealed class LiquidTabBarLens : Control
{
    static LiquidTabBarLens()
    {
        IsHitTestVisibleProperty.OverrideDefaultValue<LiquidTabBarLens>(false);
    }

    public override void Render(DrawingContext context) => (TemplatedParent as LiquidTabBar)?.RenderLens(context);
}

/// <summary>
/// The default <c>SelectedValueBinding</c> of a <see cref="LiquidTabBar"/>: a tab reports its <c>Value</c>, any other
/// item reports itself. Built from a compiled binding path (no reflection), so it is trimming and AOT safe.
/// </summary>
internal static class LiquidTabBarValueBinding
{
    private static readonly ClrPropertyInfo s_value = new("Value", GetValue, null, typeof(object));

    private static object? GetValue(object target) => ValueOf(target);

    public static BindingBase Create()
    {
        var path = new CompiledBindingPathBuilder()
            .Property(s_value, PropertyInfoAccessorFactory.CreateInpcPropertyAccessor)
            .Build();
        return new CompiledBinding(path);
    }

    /// <summary>The value an item reports (same rule as the binding).</summary>
    public static object? ValueOf(object? item) => item is LiquidTabBarItem tab ? tab.Value : item;
}
