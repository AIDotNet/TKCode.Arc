using Avalonia.Controls.Templates;
using Avalonia.Metadata;

namespace TKCode.Arc;

/// <summary>
/// A custom editor of a <see cref="PropertyGrid"/>: a data template built for the matching rows, with the
/// <see cref="PropertyGridRow"/> as its data context (bind to <c>Value</c>, <c>IsReadOnly</c>, <c>IsMixed</c>).
/// A row is matched by <see cref="EditorKey"/> first, then by <see cref="PropertyName"/>, then by
/// <see cref="PropertyType"/>; the first editor of the best kind wins, and rows nothing matches use the built-in editors.
/// </summary>
/// <example>
/// <code language="xml">
/// &lt;arc:PropertyGrid.Editors&gt;
///     &lt;arc:PropertyGridEditor EditorKey="LayerColor"&gt;
///         &lt;DataTemplate x:DataType="arc:PropertyGridRow"&gt;
///             &lt;arc:Select Size="Small" SelectedItem="{Binding Value}" ... /&gt;
///         &lt;/DataTemplate&gt;
///     &lt;/arc:PropertyGridEditor&gt;
/// &lt;/arc:PropertyGrid.Editors&gt;
/// </code>
/// </example>
public sealed class PropertyGridEditor
{
    /// <summary>Matches rows whose <see cref="PropertyGridProperty.EditorKey"/> is this key.</summary>
    public string? EditorKey { get; set; }

    /// <summary>Matches rows of properties with this name (and of <see cref="PropertyType"/> when that is set too).</summary>
    public string? PropertyName { get; set; }

    /// <summary>Matches rows whose property type is this type or derives from it (nullable types match their underlying
    /// type).</summary>
    public Type? PropertyType { get; set; }

    /// <summary>The editor; its data context is the <see cref="PropertyGridRow"/>.</summary>
    [Content]
    public IDataTemplate? Template { get; set; }

    internal bool MatchesKey(PropertyGridRow row) => EditorKey is { } key && string.Equals(key, row.Property.EditorKey, StringComparison.Ordinal);

    internal bool MatchesName(PropertyGridRow row) => EditorKey is null && PropertyName is { } name
                                                     && string.Equals(name, row.Name, StringComparison.Ordinal)
                                                     && (PropertyType is null || MatchesType(row.PropertyType));

    internal bool MatchesTypeOnly(PropertyGridRow row) => EditorKey is null && PropertyName is null && PropertyType is not null
                                                         && MatchesType(row.PropertyType);

    private bool MatchesType(Type type)
        => PropertyType is { } target && (target == type || target == PropertyGridValues.Underlying(type) || target.IsAssignableFrom(type));
}
