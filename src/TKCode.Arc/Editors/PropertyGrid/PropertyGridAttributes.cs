namespace TKCode.Arc;

/// <summary>
/// Marks a class or struct whose public properties a <see cref="PropertyGrid"/> edits. The Foundation generator (shipped
/// with TKCode.Arc.Foundation) reads the type at compile time and registers its properties with
/// <see cref="PropertyGridTypes"/>, with typed get and set delegates, so nothing is reflected at run time.
/// </summary>
/// <remarks>
/// Every public instance property with a public getter is listed, inherited ones included; a property without a public
/// setter (or with an <c>init</c> one) is read-only. The generator reads <c>[Browsable(false)]</c>,
/// <c>[DisplayName]</c>, <c>[Category]</c>, <c>[Description]</c> and <c>[ReadOnly]</c> from
/// <c>System.ComponentModel</c>, and <see cref="PropertyGridOptionsAttribute"/> for the grid's own options. A property
/// whose type is also marked expands into its own rows; a struct is copied, changed and written back to its owner.
/// The type must be accessible from its assembly (public or internal, not private nested).
/// </remarks>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, Inherited = false)]
public sealed class PropertyGridModelAttribute : Attribute
{
}

/// <summary>Options of one property in a <see cref="PropertyGrid"/>, read at compile time by the generator.</summary>
[AttributeUsage(AttributeTargets.Property, Inherited = true)]
public sealed class PropertyGridOptionsAttribute : Attribute
{
    /// <summary>Position within its category; lower first, then declaration order. Default 0.</summary>
    public int Order { get; set; }

    /// <summary>Picks a <see cref="PropertyGridEditor"/> with the same <see cref="PropertyGridEditor.EditorKey"/>.</summary>
    public string? EditorKey { get; set; }

    /// <summary>Style classes added to the property's <see cref="PropertyGridItem"/>, separated by spaces.</summary>
    public string? Classes { get; set; }

    /// <summary>Smallest number the number editor accepts; NaN (the default) keeps the type's own limit.</summary>
    public double Minimum { get; set; } = double.NaN;

    /// <summary>Largest number the number editor accepts; NaN (the default) keeps the type's own limit.</summary>
    public double Maximum { get; set; } = double.NaN;

    /// <summary>Amount an arrow key or a step button adds; NaN (the default) is 1 for whole numbers and one unit of the
    /// last fraction digit otherwise.</summary>
    public double Step { get; set; } = double.NaN;

    /// <summary>Fraction digits a floating-point number shows and keeps; -1 (the default) is 4.</summary>
    public int FractionDigits { get; set; } = -1;

    /// <summary>Whether the property expands into rows: <see cref="PropertyGridExpand.Auto"/> (the default) expands
    /// registered types and lists.</summary>
    public PropertyGridExpand Expand { get; set; }
}

/// <summary>Whether a property's value expands into its own rows.</summary>
public enum PropertyGridExpand
{
    /// <summary>Lists and types known to <see cref="PropertyGridTypes"/> expand; Avalonia objects do not.</summary>
    Auto,

    /// <summary>Always expands when the value has properties (Avalonia objects included).</summary>
    Always,

    /// <summary>Never expands; the row shows its editor or its text only.</summary>
    Never
}
