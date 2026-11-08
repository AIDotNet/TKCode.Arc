using System.Collections.Concurrent;

namespace TKCode.Arc;

/// <summary>
/// Asks the Foundation generator (shipped with TKCode.Arc.Foundation) to describe an enum at compile time: its members in
/// declaration order, their values, the <c>[Description]</c> or <c>[Display(Name)]</c> text and whether it is
/// <c>[Flags]</c>. The description is registered with <see cref="EnumTypes"/> when the assembly loads, so an
/// <see cref="EnumSelector"/> lists the enum without reflection (AOT and trimming safe).
/// </summary>
/// <remarks>
/// Put it on the enum, or name an enum of another assembly with <c>[assembly: EnumMetadata(typeof(DayOfWeek))]</c>.
/// Members marked <c>[Browsable(false)]</c> are left out, and of two members with the same value only the first is
/// listed. With <see cref="LanguageCatalog"/>, each member's text comes from that <c>[LanguageCatalog]</c> enum (member
/// <see cref="KeyPrefix"/> + member name) and follows the application language.
/// </remarks>
[AttributeUsage(AttributeTargets.Enum | AttributeTargets.Assembly, AllowMultiple = true, Inherited = false)]
public sealed class EnumMetadataAttribute : Attribute
{
    /// <summary>Describes the enum the attribute is placed on.</summary>
    public EnumMetadataAttribute()
    {
    }

    /// <summary>Describes <paramref name="enumType"/> (an assembly-level attribute, typically for an enum of another assembly).</summary>
    public EnumMetadataAttribute(Type enumType)
    {
        EnumType = enumType;
    }

    /// <summary>The enum an assembly-level attribute describes; null on an enum.</summary>
    public Type? EnumType { get; }

    /// <summary>
    /// A <c>[LanguageCatalog]</c> enum holding the members' texts. The text of member <c>Low</c> of enum <c>Priority</c> is
    /// the catalog member <c>PriorityLow</c> (<see cref="KeyPrefix"/> + member name); a member without one keeps its
    /// <c>[Description]</c> or name, and the generator warns (ARCF037).
    /// </summary>
    public Type? LanguageCatalog { get; set; }

    /// <summary>The start of the catalog member names; the enum's name by default. Set it to an empty string to use the
    /// member names as they are.</summary>
    public string? KeyPrefix { get; set; }
}

/// <summary>One member of an enum described by <see cref="EnumTypeInfo"/>.</summary>
public sealed class EnumMemberInfo
{
    /// <summary>Creates a member description.</summary>
    /// <param name="name">The member's name in code.</param>
    /// <param name="value">The boxed enum value.</param>
    /// <param name="bits">The value as 64 bits (signed values sign-extended), used for flag arithmetic.</param>
    public EnumMemberInfo(string name, object value, ulong bits)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(value);
        Name = name;
        Value = value;
        Bits = bits;
    }

    /// <summary>The member's name in code.</summary>
    public string Name { get; }

    /// <summary>The boxed enum value.</summary>
    public object Value { get; }

    /// <summary>The value as 64 bits (signed values sign-extended).</summary>
    public ulong Bits { get; }

    /// <summary>The <c>[Display(Name)]</c> or <c>[Description]</c> text; null when the member has neither.</summary>
    public string? DisplayName { get; init; }

    /// <summary>A resource key (a language catalog member) whose text replaces <see cref="DisplayName"/> and follows the
    /// application language; null when the enum names no catalog.</summary>
    public object? TextKey { get; init; }

    /// <inheritdoc />
    public override string ToString() => Name;
}

/// <summary>
/// What a control needs to know about an enum without reflecting over it: its members, whether it is <c>[Flags]</c>,
/// and conversions between its values and their bits. Written by the generator for <see cref="EnumMetadataAttribute"/>
/// enums, or by hand for <see cref="EnumTypes.Register(EnumTypeInfo)"/>.
/// </summary>
public sealed class EnumTypeInfo
{
    private readonly Func<object, ulong> _toBits;
    private readonly Func<ulong, object> _fromBits;

    /// <summary>Creates an enum description.</summary>
    /// <param name="type">The enum type.</param>
    /// <param name="isFlags">Whether the enum is marked <c>[Flags]</c>.</param>
    /// <param name="toBits">Converts a boxed value of <paramref name="type"/> to 64 bits (signed values sign-extended).</param>
    /// <param name="fromBits">Converts 64 bits back to a boxed value of <paramref name="type"/>.</param>
    /// <param name="members">The members to list, in order.</param>
    public EnumTypeInfo(Type type, bool isFlags, Func<object, ulong> toBits, Func<ulong, object> fromBits, IReadOnlyList<EnumMemberInfo> members)
    {
        ArgumentNullException.ThrowIfNull(type);
        ArgumentNullException.ThrowIfNull(toBits);
        ArgumentNullException.ThrowIfNull(fromBits);
        ArgumentNullException.ThrowIfNull(members);
        Type = type;
        IsFlags = isFlags;
        _toBits = toBits;
        _fromBits = fromBits;
        Members = members;
    }

    /// <summary>The enum type.</summary>
    public Type Type { get; }

    /// <summary>Whether the enum is marked <c>[Flags]</c>.</summary>
    public bool IsFlags { get; }

    /// <summary>The members, in declaration order.</summary>
    public IReadOnlyList<EnumMemberInfo> Members { get; }

    /// <summary>Whether <paramref name="value"/> is a value of <see cref="Type"/>.</summary>
    public bool IsValue(object? value) => value is not null && value.GetType() == Type;

    /// <summary>The bits of <paramref name="value"/>, a value of <see cref="Type"/>.</summary>
    public ulong ToBits(object value) => _toBits(value);

    /// <summary>The value of <see cref="Type"/> with these bits (bits beyond the underlying type are dropped).</summary>
    public object FromBits(ulong bits) => _fromBits(bits);

    /// <summary>The member with exactly the value <paramref name="value"/>; null when no listed member has it.</summary>
    public EnumMemberInfo? FindMember(object? value)
    {
        if (!IsValue(value))
        {
            return null;
        }
        var bits = ToBits(value!);
        foreach (var member in Members)
        {
            if (member.Bits == bits)
            {
                return member;
            }
        }
        return null;
    }
}

/// <summary>
/// The enums controls can describe without reflection. Enums marked <see cref="EnumMetadataAttribute"/> register
/// themselves when their assembly loads (generated code); others can be registered in code with
/// <see cref="Register(EnumTypeInfo)"/>. A registration made later replaces an earlier one for the same type.
/// </summary>
public static class EnumTypes
{
    private static readonly ConcurrentDictionary<Type, Lazy<EnumTypeInfo>> s_types = new();

    /// <summary>Raised after a registration, so controls showing that enum can rebuild.</summary>
    public static event EventHandler<Type>? Registered;

    /// <summary>Registers an enum whose description is created on first use.</summary>
    public static void Register(Type type, Func<EnumTypeInfo> factory)
    {
        ArgumentNullException.ThrowIfNull(type);
        ArgumentNullException.ThrowIfNull(factory);
        s_types[type] = new Lazy<EnumTypeInfo>(factory, LazyThreadSafetyMode.ExecutionAndPublication);
        Registered?.Invoke(null, type);
    }

    /// <summary>Registers a description.</summary>
    public static void Register(EnumTypeInfo info)
    {
        ArgumentNullException.ThrowIfNull(info);
        Register(info.Type, () => info);
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

    /// <summary>Whether <paramref name="type"/> is registered.</summary>
    public static bool IsRegistered(Type type)
    {
        ArgumentNullException.ThrowIfNull(type);
        return s_types.ContainsKey(type);
    }

    /// <summary>The description of <paramref name="type"/> (or of the enum a nullable type wraps); null when it is not registered.</summary>
    public static EnumTypeInfo? Find(Type? type)
    {
        if (type is null)
        {
            return null;
        }
        type = Nullable.GetUnderlyingType(type) ?? type;
        return s_types.TryGetValue(type, out var info) ? info.Value : null;
    }
}
