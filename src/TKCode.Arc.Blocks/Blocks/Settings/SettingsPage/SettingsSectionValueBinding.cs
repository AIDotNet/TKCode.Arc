using Avalonia.Data;
using Avalonia.Data.Core;
using Avalonia.Markup.Xaml.MarkupExtensions.CompiledBindings;

namespace TKCode.Arc;

/// <summary>
/// The default <c>SelectedValueBinding</c> of a <see cref="SettingsPage"/>: a section reports its
/// <see cref="SettingsSection.Value"/>, or itself when it has none; any other item reports itself. Built from a compiled
/// binding path (no reflection), so it is trimming and AOT safe.
/// </summary>
internal static class SettingsSectionValueBinding
{
    private static readonly ClrPropertyInfo s_value = new("Value", target => ValueOf(target), null, typeof(object));

    public static BindingBase Create()
    {
        var path = new CompiledBindingPathBuilder()
            .Property(s_value, PropertyInfoAccessorFactory.CreateInpcPropertyAccessor)
            .Build();
        return new CompiledBinding(path);
    }

    /// <summary>The value an item reports (same rule as the binding).</summary>
    public static object? ValueOf(object? item) => item is SettingsSection { Value: { } value } ? value : item;
}
