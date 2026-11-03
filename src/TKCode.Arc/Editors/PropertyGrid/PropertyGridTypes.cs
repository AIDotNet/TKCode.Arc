using System.Collections.Concurrent;
using Avalonia;

namespace TKCode.Arc;

/// <summary>
/// The types a <see cref="PropertyGrid"/> knows how to edit. Types marked <see cref="PropertyGridModelAttribute"/>
/// register themselves when their assembly loads (generated code); other types, such as <c>System.Numerics.Vector3</c> or
/// types from another library, are registered in code with <see cref="Register{T}(Action{PropertyGridTypeBuilder{T}})"/>.
/// A registration made later replaces an earlier one for the same type.
/// </summary>
public static class PropertyGridTypes
{
    private static readonly ConcurrentDictionary<Type, Lazy<PropertyGridTypeInfo>> s_types = new();

    /// <summary>Raised after a registration, so open grids can rebuild their rows.</summary>
    public static event EventHandler<Type>? Registered;

    /// <summary>Registers a type whose description is created on first use.</summary>
    public static void Register(Type type, Func<PropertyGridTypeInfo> factory)
    {
        ArgumentNullException.ThrowIfNull(type);
        ArgumentNullException.ThrowIfNull(factory);
        s_types[type] = new Lazy<PropertyGridTypeInfo>(factory, LazyThreadSafetyMode.ExecutionAndPublication);
        Registered?.Invoke(null, type);
    }

    /// <summary>Registers a description.</summary>
    public static void Register(PropertyGridTypeInfo info)
    {
        ArgumentNullException.ThrowIfNull(info);
        Register(info.Type, () => info);
    }

    /// <summary>Registers <typeparamref name="T"/> with the properties <paramref name="build"/> declares.</summary>
    /// <example>
    /// <code>
    /// PropertyGridTypes.Register&lt;Vector3&gt;(t =&gt;
    /// {
    ///     t.Property("X", v =&gt; v.X).Update((v, x) =&gt; { v.X = x; return v; });
    ///     t.Property("Y", v =&gt; v.Y).Update((v, y) =&gt; { v.Y = y; return v; });
    ///     t.Summary(v =&gt; $"{v.X:0.##}, {v.Y:0.##}");
    /// });
    /// </code>
    /// </example>
    public static void Register<T>(Action<PropertyGridTypeBuilder<T>> build)
    {
        ArgumentNullException.ThrowIfNull(build);
        var builder = new PropertyGridTypeBuilder<T>();
        build(builder);
        Register(builder.Build());
    }

    /// <summary>Removes a registration; returns whether there was one.</summary>
    public static bool Unregister(Type type)
    {
        ArgumentNullException.ThrowIfNull(type);
        var removed = s_types.TryRemove(type, out _);
        if (removed)
        {
            Registered?.Invoke(null, type);
        }
        return removed;
    }

    /// <summary>Whether <paramref name="type"/> itself is registered (base types are not consulted).</summary>
    public static bool IsRegistered(Type type) => s_types.ContainsKey(type);

    /// <summary>The description of <paramref name="type"/>, or of its nearest registered base type; null when none.</summary>
    public static PropertyGridTypeInfo? Find(Type type)
    {
        ArgumentNullException.ThrowIfNull(type);
        for (var current = Nullable.GetUnderlyingType(type) ?? type; current is not null; current = current.BaseType)
        {
            if (s_types.TryGetValue(current, out var info))
            {
                return info.Value;
            }
        }
        return null;
    }

    /// <summary>
    /// Describes an Avalonia object by its registered Avalonia properties (styled and direct), grouped by the type that
    /// owns each. Read from Avalonia's property registry, so no reflection is involved.
    /// </summary>
    public static PropertyGridTypeInfo DescribeAvaloniaObject(AvaloniaObject target)
    {
        ArgumentNullException.ThrowIfNull(target);
        var properties = new List<PropertyGridProperty>();
        foreach (var property in AvaloniaPropertyRegistry.Instance.GetRegistered(target))
        {
            if (property.IsAttached)
            {
                continue;
            }
            var avaloniaProperty = property;
            properties.Add(new PropertyGridProperty(property.Name, property.PropertyType,
                owner => ((AvaloniaObject)owner).GetValue(avaloniaProperty),
                property.IsReadOnly ? null : (owner, value) =>
                {
                    ((AvaloniaObject)owner).SetValue(avaloniaProperty, value);
                    return owner;
                })
            {
                Category = property.OwnerType.Name,
                Choices = PropertyGridValues.EnumChoices(property.PropertyType)
            });
        }
        properties.Sort(static (a, b) =>
        {
            var byCategory = string.CompareOrdinal(a.Category, b.Category);
            return byCategory != 0 ? byCategory : string.CompareOrdinal(a.Name, b.Name);
        });
        return new PropertyGridTypeInfo(target.GetType(), properties);
    }
}

/// <summary>Declares the properties of <typeparamref name="T"/> for <see cref="PropertyGridTypes.Register{T}"/>.</summary>
public sealed class PropertyGridTypeBuilder<T>
{
    private readonly List<IPropertyGridPropertyBuilder> _properties = [];
    private Func<object, string?>? _summary;

    internal PropertyGridTypeBuilder()
    {
    }

    /// <summary>Adds a property read by <paramref name="get"/>; it stays read-only until
    /// <see cref="PropertyGridPropertyBuilder{T, TValue}.Set"/> or <see cref="PropertyGridPropertyBuilder{T, TValue}.Update"/>
    /// says how to write it.</summary>
    public PropertyGridPropertyBuilder<T, TValue> Property<TValue>(string name, Func<T, TValue> get)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(get);
        var builder = new PropertyGridPropertyBuilder<T, TValue>(name, get);
        _properties.Add(builder);
        return builder;
    }

    /// <summary>Text of a collapsed row holding a <typeparamref name="T"/>; <c>ToString()</c> by default.</summary>
    public PropertyGridTypeBuilder<T> Summary(Func<T, string?> summary)
    {
        ArgumentNullException.ThrowIfNull(summary);
        _summary = value => summary((T)value);
        return this;
    }

    internal PropertyGridTypeInfo Build()
    {
        var list = new PropertyGridProperty[_properties.Count];
        for (var i = 0; i < list.Length; i++)
        {
            list[i] = _properties[i].Build();
        }
        return new PropertyGridTypeInfo(typeof(T), list) { Summary = _summary };
    }
}

internal interface IPropertyGridPropertyBuilder
{
    PropertyGridProperty Build();
}

/// <summary>Options of one property declared with <see cref="PropertyGridTypeBuilder{T}.Property{TValue}"/>.</summary>
public sealed class PropertyGridPropertyBuilder<T, TValue> : IPropertyGridPropertyBuilder
{
    private readonly string _name;
    private readonly Func<T, TValue> _get;
    private Func<object, object?, object>? _set;
    private string? _displayName;
    private string? _category;
    private string? _description;
    private bool _readOnly;
    private int _order;
    private string? _editorKey;
    private string? _classes;
    private double? _minimum;
    private double? _maximum;
    private double? _step;
    private int? _fractionDigits;
    private IReadOnlyList<PropertyGridChoice>? _choices;
    private PropertyGridExpand _expand;
    private Type? _elementType;
    private Func<object?>? _createElement;

    internal PropertyGridPropertyBuilder(string name, Func<T, TValue> get)
    {
        _name = name;
        _get = get;
    }

    /// <summary>Writes the property of a class instance.</summary>
    public PropertyGridPropertyBuilder<T, TValue> Set(Action<T, TValue> set)
    {
        ArgumentNullException.ThrowIfNull(set);
        _set = (owner, value) =>
        {
            set((T)owner, (TValue)value!);
            return owner;
        };
        return this;
    }

    /// <summary>Writes the property of a struct: returns the changed copy, which the grid writes back to its owner.</summary>
    public PropertyGridPropertyBuilder<T, TValue> Update(Func<T, TValue, T> update)
    {
        ArgumentNullException.ThrowIfNull(update);
        _set = (owner, value) => update((T)owner, (TValue)value!)!;
        return this;
    }

    /// <summary>The row label.</summary>
    public PropertyGridPropertyBuilder<T, TValue> DisplayName(string displayName)
    {
        _displayName = displayName;
        return this;
    }

    /// <summary>The category the row is grouped under.</summary>
    public PropertyGridPropertyBuilder<T, TValue> Category(string? category)
    {
        _category = category;
        return this;
    }

    /// <summary>Text of the description area.</summary>
    public PropertyGridPropertyBuilder<T, TValue> Description(string? description)
    {
        _description = description;
        return this;
    }

    /// <summary>Shows the value without letting it be edited.</summary>
    public PropertyGridPropertyBuilder<T, TValue> ReadOnly(bool readOnly = true)
    {
        _readOnly = readOnly;
        return this;
    }

    /// <summary>Position within the category.</summary>
    public PropertyGridPropertyBuilder<T, TValue> Order(int order)
    {
        _order = order;
        return this;
    }

    /// <summary>Picks the <see cref="PropertyGridEditor"/> with this key.</summary>
    public PropertyGridPropertyBuilder<T, TValue> Editor(string? editorKey)
    {
        _editorKey = editorKey;
        return this;
    }

    /// <summary>Style classes of the row, separated by spaces.</summary>
    public PropertyGridPropertyBuilder<T, TValue> Classes(string? classes)
    {
        _classes = classes;
        return this;
    }

    /// <summary>Limits and step of the number editor.</summary>
    public PropertyGridPropertyBuilder<T, TValue> Range(double? minimum, double? maximum, double? step = null)
    {
        _minimum = minimum;
        _maximum = maximum;
        _step = step;
        return this;
    }

    /// <summary>Fraction digits of a floating-point number.</summary>
    public PropertyGridPropertyBuilder<T, TValue> FractionDigits(int digits)
    {
        _fractionDigits = digits;
        return this;
    }

    /// <summary>The values the property accepts, shown in a menu.</summary>
    public PropertyGridPropertyBuilder<T, TValue> Choices(params PropertyGridChoice[] choices)
    {
        _choices = choices;
        return this;
    }

    /// <summary>Whether the value expands into its own rows.</summary>
    public PropertyGridPropertyBuilder<T, TValue> Expand(PropertyGridExpand expand)
    {
        _expand = expand;
        return this;
    }

    /// <summary>For a list: the item type, and how the add button creates an item (null hides the button).</summary>
    public PropertyGridPropertyBuilder<T, TValue> Items<TElement>(Func<TElement>? create = null)
    {
        _elementType = typeof(TElement);
        _createElement = create is null ? null : () => create();
        return this;
    }

    PropertyGridProperty IPropertyGridPropertyBuilder.Build()
    {
        var get = _get;
        return new PropertyGridProperty(_name, typeof(TValue), owner => get((T)owner), _set)
        {
            DisplayName = _displayName ?? _name,
            Category = _category,
            Description = _description,
            IsReadOnly = _readOnly,
            Order = _order,
            EditorKey = _editorKey,
            Classes = _classes,
            Minimum = _minimum,
            Maximum = _maximum,
            Step = _step,
            FractionDigits = _fractionDigits,
            Choices = _choices ?? PropertyGridValues.EnumChoices(typeof(TValue)),
            Expand = _expand,
            ElementType = _elementType,
            CreateElement = _createElement
        };
    }
}
