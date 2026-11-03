namespace TKCode.Arc;

/// <summary>One option of a property with a fixed set of values (an enum, or <see cref="PropertyGridProperty.Choices"/>).</summary>
/// <param name="Value">The value written to the property.</param>
/// <param name="Text">What the menu shows.</param>
public sealed record PropertyGridChoice(object? Value, string Text)
{
    /// <inheritdoc />
    public override string ToString() => Text;
}

/// <summary>
/// Describes one property a <see cref="PropertyGrid"/> can show: its name and text, its type, and delegates that read and
/// write it. The generator creates these for <see cref="PropertyGridModelAttribute"/> types; code can create them with
/// <see cref="PropertyGridTypes.Register{T}(Action{PropertyGridTypeBuilder{T}})"/> or directly.
/// </summary>
public sealed class PropertyGridProperty
{
    private readonly bool _readOnly;
    private readonly string? _displayName;

    /// <summary>Creates a descriptor.</summary>
    /// <param name="name">The property name (unique within its type).</param>
    /// <param name="propertyType">The declared type; picks the editor.</param>
    /// <param name="getter">Reads the value from an owner.</param>
    /// <param name="setter">Writes a value into an owner and returns the owner: the same instance for a class, the changed
    /// copy for a struct (the grid writes it back to the struct's own owner). Null makes the property read-only.</param>
    public PropertyGridProperty(string name, Type propertyType, Func<object, object?> getter, Func<object, object?, object>? setter = null)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(propertyType);
        ArgumentNullException.ThrowIfNull(getter);
        Name = name;
        PropertyType = propertyType;
        Getter = getter;
        Setter = setter;
    }

    /// <summary>The property name.</summary>
    public string Name { get; }

    /// <summary>The declared type of the property.</summary>
    public Type PropertyType { get; }

    /// <summary>Reads the value from an owner.</summary>
    public Func<object, object?> Getter { get; }

    /// <summary>Writes a value and returns the owner (the changed copy for a struct); null when read-only.</summary>
    public Func<object, object?, object>? Setter { get; }

    /// <summary>The label of the row; <see cref="Name"/> when not set.</summary>
    public string DisplayName { get => _displayName ?? Name; init => _displayName = value; }

    /// <summary>The category the row is grouped under; null puts it under <see cref="PropertyGrid.MiscCategoryText"/>.</summary>
    public string? Category { get; init; }

    /// <summary>Text shown in the description area when the row is selected.</summary>
    public string? Description { get; init; }

    /// <summary>Whether the property cannot be written: always true without a <see cref="Setter"/>.</summary>
    public bool IsReadOnly { get => _readOnly || Setter is null; init => _readOnly = value; }

    /// <summary>Position within its category; lower first, then the order the properties were given.</summary>
    public int Order { get; init; }

    /// <summary>Picks a <see cref="PropertyGridEditor"/> with the same <see cref="PropertyGridEditor.EditorKey"/>.</summary>
    public string? EditorKey { get; init; }

    /// <summary>Style classes added to the row's <see cref="PropertyGridItem"/>, separated by spaces.</summary>
    public string? Classes { get; init; }

    /// <summary>Smallest number the number editor accepts; null keeps the type's own limit.</summary>
    public double? Minimum { get; init; }

    /// <summary>Largest number the number editor accepts; null keeps the type's own limit.</summary>
    public double? Maximum { get; init; }

    /// <summary>Amount an arrow key adds in the number editor; null is 1 for whole numbers and one unit of the last
    /// fraction digit otherwise.</summary>
    public double? Step { get; init; }

    /// <summary>Fraction digits a floating-point number shows and keeps; null is 4.</summary>
    public int? FractionDigits { get; init; }

    /// <summary>The values the property accepts, shown in a menu. Enums get theirs from the generator.</summary>
    public IReadOnlyList<PropertyGridChoice>? Choices { get; init; }

    /// <summary>Whether the value expands into its own rows.</summary>
    public PropertyGridExpand Expand { get; init; }

    /// <summary>For a list property: the type of its items (picks their editor).</summary>
    public Type? ElementType { get; init; }

    /// <summary>For a list property: creates the item the add button appends; null hides the button.</summary>
    public Func<object?>? CreateElement { get; init; }

    /// <inheritdoc />
    public override string ToString() => Name;
}

/// <summary>The properties a <see cref="PropertyGrid"/> shows for one type.</summary>
public sealed class PropertyGridTypeInfo
{
    /// <summary>Creates the description of <paramref name="type"/>.</summary>
    public PropertyGridTypeInfo(Type type, IReadOnlyList<PropertyGridProperty> properties)
    {
        ArgumentNullException.ThrowIfNull(type);
        ArgumentNullException.ThrowIfNull(properties);
        Type = type;
        Properties = properties;
    }

    /// <summary>The described type.</summary>
    public Type Type { get; }

    /// <summary>Its properties, in the order they were given.</summary>
    public IReadOnlyList<PropertyGridProperty> Properties { get; }

    /// <summary>Text of a collapsed row holding a value of this type; <c>ToString()</c> when null.</summary>
    public Func<object, string?>? Summary { get; init; }
}
