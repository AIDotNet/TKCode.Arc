using Avalonia.Data;
using Avalonia.Data.Core;
using Avalonia.Markup.Xaml.MarkupExtensions.CompiledBindings;
using Avalonia.Media;
using Avalonia.Media.Immutable;

namespace TKCode.Arc;

/// <summary>An option data item of a toggle group (RadioOption, RadioCardOption, SegmentedOption, BillingToggleOption).</summary>
internal interface ITogglesValueSource
{
    /// <summary>The value the group reports as <c>SelectedValue</c> when this option is selected.</summary>
    object? ToggleValue { get; }
}

/// <summary>
/// The default <c>SelectedValueBinding</c> of the toggle groups: an option reports its <c>Value</c>, any other item
/// reports itself. Built from a compiled binding path (no reflection), so it is trimming and AOT safe.
/// </summary>
internal static class TogglesValueBinding
{
    private static readonly ClrPropertyInfo s_value = new("Value", GetValue, null, typeof(object));

    private static object? GetValue(object target) => target is ITogglesValueSource source ? source.ToggleValue : target;

    public static BindingBase Create()
    {
        var path = new CompiledBindingPathBuilder()
            .Property(s_value, PropertyInfoAccessorFactory.CreateInpcPropertyAccessor)
            .Build();
        return new CompiledBinding(path);
    }

    /// <summary>The value an item reports (same rule as the binding).</summary>
    public static object? ValueOf(object? item) => item is ITogglesValueSource source ? source.ToggleValue : item;
}

/// <summary>Brush helpers for accent-dependent tints that a control token cannot precompute.</summary>
internal static class TogglesBrushes
{
    /// <summary>The same color at <paramref name="opacity"/> (CSS <c>color-mix(in oklab, c x%, transparent)</c>).</summary>
    public static IBrush? WithOpacity(IBrush? brush, double opacity)
        => brush is ISolidColorBrush solid ? new ImmutableSolidColorBrush(solid.Color, solid.Opacity * opacity) : brush;
}
